using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Animation.FaunaProcedural.Editor
{
    public sealed class ProceduralRigTunerWindow : EditorWindow
    {
        private const string DefaultCsvRelativePath = "Assets/_Project/Data/skeletal_profiles.csv";

        [MenuItem("HECTON-8/Animation/Procedural Rig Tuner")]
        private static void Open()
        {
            GetWindow<ProceduralRigTunerWindow>("Procedural Rig Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnDrawGizmosSceneHook;
            SceneView.duringSceneGui += OnDrawGizmosSceneHook;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmosSceneHook;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying ||
                !ProceduralBoneBlenderRuntime.TryGetActiveRuntimeInstance(out ProceduralBoneBlenderRuntime runtime))
            {
                EditorGUILayout.HelpBox("Enter Play Mode with ProceduralBoneBlenderRuntime active.", MessageType.Info);
                return;
            }

            if (!runtime.TryResolveTuningForEditor(out NativeArray<ProceduralBoneRigTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                EditorGUILayout.HelpBox("GlobalDataVault tuning buffer is not available.", MessageType.Warning);
                return;
            }

            ProceduralBoneRigTuningDTO dto = ProceduralBoneSanitizer.SanitizeTuning(tuning[0]);
            EditorGUI.BeginChangeCheck();
            dto.SineFrequency = EditorGUILayout.Slider("Sine Frequency", dto.SineFrequency, 0.05f, 8f);
            dto.WaveAmplitudeRadians = EditorGUILayout.Slider("Wave Amplitude", dto.WaveAmplitudeRadians, 0f, 1.4f);
            dto.PhaseOffset = EditorGUILayout.Slider("Phase Offset Per Bone", dto.PhaseOffset, 0.05f, 2.5f);
            dto.DampingHz = EditorGUILayout.Slider("Damping", dto.DampingHz, 0.1f, 18f);
            dto.GlobalQualityWeight = EditorGUILayout.Slider("Global Quality Weight", dto.GlobalQualityWeight, 0f, 1f);
            dto.SecondaryBoneStart01 = EditorGUILayout.Slider("Secondary Bone Start", dto.SecondaryBoneStart01, 0.05f, 0.95f);
            dto.JawIkWeight = EditorGUILayout.Slider("Jaw IK Weight", dto.JawIkWeight, 0f, 1f);
            dto.MockSignalWeight = EditorGUILayout.Slider("Mock AI Weight", dto.MockSignalWeight, 0f, 1f);
            dto.TraumaFrequencyHz = EditorGUILayout.Slider("Trauma Frequency", dto.TraumaFrequencyHz, 1f, 40f);
            dto.TraumaAmplitudeRadians = EditorGUILayout.Slider("Trauma Amplitude", dto.TraumaAmplitudeRadians, 0f, 0.8f);
            dto.LowQualityUpdateHz = EditorGUILayout.Slider("Low Quality Hz", dto.LowQualityUpdateHz, 1f, 30f);
            dto.HighQualityUpdateHz = EditorGUILayout.Slider("High Quality Hz", dto.HighQualityUpdateHz, 30f, 120f);
            dto.ActiveSkeletonCount = EditorGUILayout.IntSlider("Active Skeletons", dto.ActiveSkeletonCount, 0, ProceduralBoneBlenderConstants.DefaultSkeletonCapacity);
            if (EditorGUI.EndChangeCheck())
            {
                tuning[0] = ProceduralBoneSanitizer.SanitizeTuning(dto);
                Repaint();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate Emergency Mock Rig"))
                runtime.GenerateEmergencyMockRigs();

            if (GUILayout.Button("Load skeletal_profiles.csv"))
                TryLoadCsv(runtime);

            DrawRuntimeSnapshot(runtime);
        }

        private static void TryLoadCsv(ProceduralBoneBlenderRuntime runtime)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), DefaultCsvRelativePath);
            if (!File.Exists(path))
                return;

            string csv = File.ReadAllText(path);
            runtime.TryApplyCsvProfile(csv);
        }

        private static void DrawRuntimeSnapshot(ProceduralBoneBlenderRuntime runtime)
        {
            if (!runtime.TryGetProceduralBoneGraphicsBuffer(out GraphicsBuffer buffer, out int matrixCount))
            {
                EditorGUILayout.LabelField("GPU Buffer", "No valid upload yet");
                return;
            }

            EditorGUILayout.LabelField("GPU Buffer Count", buffer.count.ToString());
            EditorGUILayout.LabelField("Matrix Upload Count", matrixCount.ToString());
            if (runtime.TryResolveTuningForEditor(out NativeArray<ProceduralBoneRigTuningDTO> tuning) &&
                tuning.IsCreated &&
                tuning.Length > 0)
            {
                EditorGUILayout.LabelField("Vault Quality", math.saturate(tuning[0].GlobalQualityWeight).ToString("0.000"));
            }
        }

        private void OnDrawGizmosSceneHook(SceneView sceneView)
        {
            if (!ProceduralBoneBlenderRuntime.TryGetActiveRuntimeInstance(out ProceduralBoneBlenderRuntime runtime) ||
                !runtime.TryResolveMatricesForEditor(out NativeArray<float4x4>.ReadOnly matrices, out NativeArray<int>.ReadOnly parents, out int count))
            {
                return;
            }

            int drawCount = math.min(count, 512);
            Handles.color = Color.cyan;
            for (int i = 0; i < drawCount; i++)
            {
                int parent = parents[i];
                if (parent < 0 || parent >= drawCount)
                    continue;

                Vector3 a = (Vector3)matrices[i].c3.xyz;
                Vector3 b = (Vector3)matrices[parent].c3.xyz;
                Handles.DrawLine(a, b);
            }
        }
    }
}
