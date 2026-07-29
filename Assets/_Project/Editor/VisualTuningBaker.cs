using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Hecton8.Graphics.Authoring
{
    [CustomPropertyDrawer(typeof(VisualTuningFacadeSO.ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }

    [CustomEditor(typeof(VisualTuningFacadeSO))]
    public class VisualTuningBaker : UnityEditor.Editor
    {
        // The output path is NOT declared here. It lives once, in StreamingAssets-relative form, at
        // VisualTuningBinaryContract.StreamingAssetsRelativePath. It was previously declared twice - here and
        // as a local in HectonArchitectureBinder.cs - which is the same drift class as a duplicated byte
        // layout.

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var facade = (VisualTuningFacadeSO)target;

            GUILayout.Space(20);
            if (GUILayout.Button("Bake to Binary (h8bin)", GUILayout.Height(30)))
            {
                if (!TryBake(facade, out string error))
                {
                    Debug.LogError($"[VisualTuningBaker] Bake failed: {error}");
                }
            }
        }

        /// <summary>
        /// Bakes <paramref name="facade"/> to <see cref="VisualTuningBinaryContract.OutputAssetPath"/>.
        /// Static and error-returning so the headless entry point at
        /// <see cref="VisualTuningBinaryContract.BakeFromFacadeAsset"/> can share exactly this routine
        /// instead of carrying a second copy of it - HectonArchitectureBinder.cs:68-84 already holds a second
        /// copy that has silently diverged from this one (it has no layout guard, no atomic write, no
        /// post-write length check and does not record the bake hash).
        /// </summary>
        internal static bool TryBake(VisualTuningFacadeSO facade, out string error)
        {
            if (facade == null)
            {
                error = "facade is null.";
                return false;
            }

            // Layout gate BEFORE any bytes are written. The runtime reader
            // (HectonVisualsOrchestrator.cs:59-65) only checks total length, which cannot detect a field
            // REORDER - the struct stays 64 bytes and transposed finite floats still pass ValidateFinite.
            // A reorder must therefore fail here or it never fails anywhere.
            if (!VisualTuningBinaryContract.TryValidateLayout(out string layoutError))
            {
                error = layoutError;
                return false;
            }

            string outputPath = VisualTuningBinaryContract.OutputAssetPath;
            string tempPath = outputPath + ".tmp";

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                VisualTuningState state = facade.BakeToUnmanaged();
                byte[] buffer = VisualTuningBinaryContract.ToBytes(ref state);

                // Provenance. Recorded on the facade only - the artifact itself has no checksum field, and
                // no reader verifies this hash. Logged as well so the bake leaves a trace even when the
                // facade asset is not saved.
                string hashString;
                using (var sha = SHA256.Create())
                {
                    byte[] hashBytes = sha.ComputeHash(buffer);
                    hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                    facade.lastBakedHash = hashString;
                    facade.lastBakedTime = System.DateTime.UtcNow.ToString("O");
                    EditorUtility.SetDirty(facade);
                }

                // Atomic temp-write, validate, rename.
                File.WriteAllBytes(tempPath, buffer);

                long written = new FileInfo(tempPath).Length;
                if (written != buffer.Length)
                {
                    error = $"temp file is {written} bytes, expected {buffer.Length}.";
                    return false;
                }

                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.Move(tempPath, outputPath);

                AssetDatabase.Refresh();

                VisualTuningState defaults = VisualTuningState.Default();
                byte[] defaultImage = VisualTuningBinaryContract.ToBytes(ref defaults);
                bool identicalToDefault = true;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] == defaultImage[i])
                    {
                        continue;
                    }

                    identicalToDefault = false;
                    break;
                }

                if (identicalToDefault)
                {
                    Debug.LogWarning(
                        $"[VisualTuningBaker] Baked artifact is byte-identical to VisualTuningState.Default(). " +
                        "The binary carries zero information - the data-driven path and the hardcoded fallback " +
                        "now produce the same visuals, so nothing about this bake is observable. Tune the " +
                        "facade away from its defaults before treating this as data-driven visual tuning.");
                }

                Debug.Log(
                    $"[VisualTuningBaker] Baked successfully to {outputPath}. Size: {buffer.Length} bytes. " +
                    $"identicalToDefault={identicalToDefault} sha256={hashString}");

                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
            finally
            {
                // Never leave a .tmp behind in StreamingAssets: Tools/h8bin_validator.py rglobs the whole
                // target directory in sanitize_runtime_artifacts() and treats stray runtime artifacts as
                // findings.
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception cleanup)
                    {
                        Debug.LogWarning($"[VisualTuningBaker] Could not remove {tempPath}: {cleanup.Message}");
                    }
                }
            }
        }
    }
}
