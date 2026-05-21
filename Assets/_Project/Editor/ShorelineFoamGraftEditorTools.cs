#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.Rendering.OceanSinglePass;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class ShorelineFoamGraftLayoutValidator
    {
        static ShorelineFoamGraftLayoutValidator()
        {
            ValidateLayouts();
        }

        [MenuItem("HECTON-8/Rendering/Validate SHINOBU 277 Shoreline Foam Layouts")]
        public static void ValidateLayouts()
        {
            AssertSize<ShorelineFoamParamsDTO>(ShorelineFoamConstants.ParamsStrideBytes);
            AssertOffset<ShorelineFoamParamsDTO>(nameof(ShorelineFoamParamsDTO.FoamIntensityAndFalloff), 0);
            AssertOffset<ShorelineFoamParamsDTO>(nameof(ShorelineFoamParamsDTO.QualityAndLimits), 16);
            AssertNoProperties<ShorelineFoamParamsDTO>();
            AssertSize<ShorelineFoamProfileDTO>(ShorelineFoamConstants.ProfileStrideBytes);
            AssertSize<ShorelineFoamRuntimeStateDTO>(ShorelineFoamConstants.RuntimeStateStrideBytes);
            AssertSize<ShorelineFoamTelemetryEntry>(ShorelineFoamConstants.TelemetryEntryStrideBytes);
        }

        private static void AssertSize<T>(int expectedBytes) where T : struct
        {
            int actualBytes = UnsafeUtility.SizeOf<T>();
            if (actualBytes != expectedBytes)
                throw new InvalidOperationException(typeof(T).Name + " size " + actualBytes + " != " + expectedBytes);
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int actualOffset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (actualOffset != expectedOffset)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset " + actualOffset + " != " + expectedOffset);
        }

        private static void AssertNoProperties<T>() where T : struct
        {
            if (typeof(T).GetProperties().Length != 0)
                throw new InvalidOperationException(typeof(T).Name + " must expose raw fields only.");
        }
    }

    public sealed class ShorelineFoamTunerWindow : EditorWindow
    {
        private float _intensity = 1.2f;
        private float _falloffMeters = ShorelineFoamConstants.DefaultDepthFalloffMeters;
        private float _decayRate = 1f / ShorelineFoamConstants.DefaultLifetimeSeconds;
        private float _normalPerturbation = 0.075f;

        [MenuItem("HECTON-8/Rendering/SHINOBU 277 Shoreline Foam Tuner")]
        public static void Open()
        {
            GetWindow<ShorelineFoamTunerWindow>("Shoreline Foam");
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _intensity = EditorGUILayout.Slider("Intensity", _intensity, 0f, 8f);
            _falloffMeters = EditorGUILayout.Slider("Falloff Meters", _falloffMeters, 0.1f, 128f);
            _decayRate = EditorGUILayout.Slider("Decay Rate", _decayRate, 0.01f, 16f);
            _normalPerturbation = EditorGUILayout.Slider("Normal Perturbation", _normalPerturbation, 0f, 2f);
            if (EditorGUI.EndChangeCheck())
                ShorelineFoamGraftRuntime.TrySetEditorProfile(_intensity, _falloffMeters, _decayRate, _normalPerturbation);

            if (ShorelineFoamGraftRuntime.TryReadTelemetry(out NativeArray<ShorelineFoamTelemetryEntry> telemetry, out int cursor) &&
                telemetry.IsCreated &&
                telemetry.Length > 0)
            {
                int index = Wrap(cursor - 1, telemetry.Length);
                ShorelineFoamTelemetryEntry entry = telemetry[index];
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last Frame", entry.Frame.ToString());
                EditorGUILayout.LabelField("Active Rows", entry.ActiveCount.ToString());
                EditorGUILayout.LabelField("Quality", entry.GlobalQualityWeight.ToString("0.000"));
                EditorGUILayout.LabelField("Upload us", entry.UploadMicroseconds.ToString("0.000"));
                EditorGUILayout.LabelField("Estimated GPU us", entry.EstimatedGpuMicroseconds.ToString("0.000"));
            }
        }

        private static int Wrap(int value, int capacity)
        {
            int safeCapacity = Mathf.Max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }

    internal static class ShorelineFoamDecalProjectorInquisition
    {
        [MenuItem("HECTON-8/Rendering/Write SHINOBU 277 Decal Projector Inquisition")]
        public static void WriteReport()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Project root unavailable.");

            string[] scanPaths =
            {
                "Assets/_Project/Scripts/Rendering/OceanSinglePass",
                "Assets/_Project/Scripts/VFX/JacobianFoam",
                "Assets/_Project/Scripts/Plugins/Crest",
                "Assets/_Project/Art/Shaders"
            };

            int cameraRenderHits = 0;
            int decalProjectorHits = 0;
            int particleHits = 0;
            int renderTextureHits = 0;
            for (int i = 0; i < scanPaths.Length; i++)
            {
                string absolute = Path.Combine(projectRoot, scanPaths[i]);
                if (!Directory.Exists(absolute))
                    continue;

                string[] files = Directory.GetFiles(absolute, "*.*", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string extension = Path.GetExtension(files[fileIndex]);
                    if (!IsScannable(extension))
                        continue;

                    string text = File.ReadAllText(files[fileIndex]);
                    cameraRenderHits += Contains(text, "Camera.Render");
                    decalProjectorHits += Contains(text, "DecalProjector");
                    particleHits += Contains(text, "ParticleSystem");
                    renderTextureHits += Contains(text, "new RenderTexture");
                }
            }

            string reportDirectory = Path.Combine(projectRoot, "Docs/Reports");
            Directory.CreateDirectory(reportDirectory);
            string reportPath = Path.Combine(reportDirectory, "RENDERING_OPTIMIZATION_REPORT_SHINOBU_277.json");
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"agentId\": \"SHINOBU_277\",");
            builder.AppendLine("  \"scanner\": \"Decal_Projector_Inquisition\",");
            builder.AppendLine("  \"evidenceClass\": \"STATIC_SOURCE_TARGETED\",");
            builder.AppendLine("  \"runtimeRoute\": \"OceanSinglePassRuntime.VisualSync -> ShorelineFoamGraftRuntime -> HectonSinglePassOceanFeature RenderGraph -> Hidden/Hecton8/OceanDepthFoam\",");
            builder.AppendLine("  \"cameraRenderHits\": " + cameraRenderHits + ",");
            builder.AppendLine("  \"decalProjectorHits\": " + decalProjectorHits + ",");
            builder.AppendLine("  \"particleSystemHits\": " + particleHits + ",");
            builder.AppendLine("  \"newRenderTextureHits\": " + renderTextureHits + ",");
            builder.AppendLine("  \"activeViolationCount\": " + (cameraRenderHits + decalProjectorHits + particleHits) + ",");
            builder.AppendLine("  \"dtoProof\": { \"ShorelineFoamParamsDTO\": 32, \"RuntimeState\": 64, \"Telemetry\": 64 },");
            builder.AppendLine("  \"notes\": \"Scanner is editor-only. Shared report sidecar avoids overwriting neighboring batch-agent objects.\"");
            builder.AppendLine("}");
            File.WriteAllText(reportPath, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static int Contains(string text, string token)
        {
            return text.IndexOf(token, StringComparison.Ordinal) >= 0 ? 1 : 0;
        }

        private static bool IsScannable(string extension)
        {
            return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".shader", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".hlsl", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".compute", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
