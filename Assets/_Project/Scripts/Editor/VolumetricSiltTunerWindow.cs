#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.VFX;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class VolumetricSiltTunerWindow : EditorWindow
    {
        private bool _drawWakeGizmos = true;
        private string _status = "Vault not sampled.";

        [MenuItem("Hecton8/VFX/Volumetric Silt Tuner")]
        private static void Open()
        {
            GetWindow<VolumetricSiltTunerWindow>("Volumetric Silt Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawWakeGizmos;
            SceneView.duringSceneGui += DrawWakeGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawWakeGizmos;
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                EditorGUILayout.HelpBox("GlobalDataVault unavailable.", MessageType.Warning);
                return;
            }

            NativeArray<VfxConfigurationDTO> tuning = vault.GetBuffer<VfxConfigurationDTO>(
                BufferID.MarineSnowTuningConstants,
                1,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            if (!tuning.IsCreated || tuning.Length <= 0)
            {
                EditorGUILayout.HelpBox("Silt tuning buffer unavailable.", MessageType.Warning);
                return;
            }

            VfxConfigurationDTO snapshot = tuning[0];
            if (snapshot.Version == 0u)
            {
                snapshot = VolumetricSiltConfigurationAccess.CreateDefault(100000);
                tuning[0] = snapshot;
            }

            EditorGUI.BeginChangeCheck();
            snapshot.ParticleCount = EditorGUILayout.IntSlider("Particle Count", snapshot.ParticleCount, 1000, 100000);
            snapshot.CurlNoiseStrength = EditorGUILayout.Slider("Curl Noise Strength", snapshot.CurlNoiseStrength, 0f, 4f);
            snapshot.WakeInfluence = EditorGUILayout.Slider("Wake Influence", snapshot.WakeInfluence, 0f, 4f);
            snapshot.GravitySinkingSpeed = EditorGUILayout.Slider("Gravity Sinking Speed", snapshot.GravitySinkingSpeed, 0.05f, 6f);
            snapshot.AmbientSize = EditorGUILayout.Slider("Ambient Silt Size", snapshot.AmbientSize, 0.0005f, 0.03f);
            snapshot.DensityScale = EditorGUILayout.Slider("Density Scale", snapshot.DensityScale, 0f, 3f);
            if (EditorGUI.EndChangeCheck())
            {
                snapshot.Version++;
                tuning[0] = snapshot;
                _status = "Vault tuning updated.";
                SceneView.RepaintAll();
            }

            _drawWakeGizmos = EditorGUILayout.Toggle("Draw Wake Gizmos", _drawWakeGizmos);
            EditorGUILayout.LongField("Version", snapshot.Version);
            EditorGUILayout.LongField("CSV Hash", snapshot.CsvProfileHash);
            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void DrawWakeGizmos(SceneView sceneView)
        {
            if (!_drawWakeGizmos)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!vault.TryGetBuffer<DynamicWakeDTO>(BufferID.MarineSnowDynamicWakes, out NativeArray<DynamicWakeDTO> wakes) ||
                !wakes.IsCreated)
                return;

            Handles.color = Color.yellow;
            int count = Mathf.Min(wakes.Length, 16);
            for (int i = 0; i < count; i++)
            {
                DynamicWakeDTO wake = wakes[i];
                if (wake.Radius <= 0.001f || wake.Falloff <= 0f)
                    continue;

                Vector3 center = new Vector3(wake.Position.x, wake.Position.y, wake.Position.z);
                float radius = Mathf.Max(0.001f, wake.Radius);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
                Vector3 force = new Vector3(wake.Force.x, wake.Force.y, wake.Force.z);
                Handles.DrawLine(center, center + force);
            }
        }
    }
}
#endif
