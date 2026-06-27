using UnityEngine;
using UnityEditor;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
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
        private const string OutputPath = "Assets/StreamingAssets/Hecton8/DataMonolith/visual_tuning.h8bin";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var facade = (VisualTuningFacadeSO)target;

            GUILayout.Space(20);
            if (GUILayout.Button("Bake to Binary (h8bin)", GUILayout.Height(30)))
            {
                Bake(facade);
            }
        }

        private void Bake(VisualTuningFacadeSO facade)
        {
            try
            {
                var dir = Path.GetDirectoryName(OutputPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                VisualTuningState state = facade.BakeToUnmanaged();
                
                int size = UnsafeUtility.SizeOf<VisualTuningState>();
                if (size % 8 != 0)
                {
                    Debug.LogError($"[VisualTuningBaker] FATAL: VisualTuningState size ({size}) is not a multiple of 8. ARM64 alignment violated.");
                    return;
                }

                byte[] buffer = new byte[size];
                unsafe
                {
                    fixed (byte* ptr = buffer)
                    {
                        UnsafeUtility.CopyStructureToPtr(ref state, ptr);
                    }
                }

                // Compute Hash
                using (var sha = SHA256.Create())
                {
                    byte[] hashBytes = sha.ComputeHash(buffer);
                    string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                    facade.lastBakedHash = hashString;
                    facade.lastBakedTime = System.DateTime.UtcNow.ToString("O");
                    EditorUtility.SetDirty(facade);
                }

                // Atomic temp-write, validate, rename
                string tempPath = OutputPath + ".tmp";
                File.WriteAllBytes(tempPath, buffer);

                // "Validate" could be checking file length
                if (new FileInfo(tempPath).Length != size)
                {
                    Debug.LogError("[VisualTuningBaker] Temp file size mismatch.");
                    return;
                }

                if (File.Exists(OutputPath))
                {
                    File.Delete(OutputPath);
                }
                File.Move(tempPath, OutputPath);

                AssetDatabase.Refresh();
                Debug.Log($"[VisualTuningBaker] Baked successfully to {OutputPath}. Size: {size} bytes.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VisualTuningBaker] Bake failed: {e.Message}");
            }
        }
    }
}
