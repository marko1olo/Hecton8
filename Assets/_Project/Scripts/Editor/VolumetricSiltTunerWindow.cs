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

            if (!TryReadFirst(vault, BufferID.MarineSnowTuningConstants, out VfxConfigurationDTO snapshot))
            {
                snapshot = VolumetricSiltConfigurationAccess.CreateDefault(100000);
                if (!TryWriteFirstOrAcquire(vault, BufferID.MarineSnowTuningConstants, snapshot))
                {
                    EditorGUILayout.HelpBox("Silt tuning buffer unavailable.", MessageType.Warning);
                    return;
                }
            }

            if (snapshot.Version == 0u)
            {
                snapshot = VolumetricSiltConfigurationAccess.CreateDefault(100000);
                TryWriteFirstOrAcquire(vault, BufferID.MarineSnowTuningConstants, snapshot);
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
                if (TryWriteFirstOrAcquire(vault, BufferID.MarineSnowTuningConstants, snapshot))
                {
                    _status = "Vault tuning updated.";
                    SceneView.RepaintAll();
                }
                else
                {
                    _status = "Vault tuning write rejected.";
                }
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

            if (!TryReadExistingVaultView(vault, BufferID.MarineSnowDynamicWakes, out NativeArray<DynamicWakeDTO> wakes))
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

        private static bool TryReadFirst<T>(IDataVault vault, BufferID bufferId, out T value)
            where T : struct
        {
            value = default;
            if (!TryReadExistingVaultView(vault, bufferId, out NativeArray<T> buffer) || buffer.Length <= 0)
                return false;

            value = buffer[0];
            return true;
        }

        private static bool TryWriteFirstOrAcquire<T>(IDataVault vault, BufferID bufferId, in T value)
            where T : struct
        {
            if (!TryAcquireEditorWriteView(vault, bufferId, 1, out VaultGenerationHandle<T> handle, out NativeArray<T> buffer))
                return false;

            try
            {
                if (buffer.Length <= 0)
                    return false;

                buffer[0] = value;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryReadExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out handle))
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                    return false;

                if (buffer.IsCreated && buffer.Length >= requiredLength)
                    return true;

                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
                buffer = default;
            }

            if (vault.IsAllocationLocked)
                return false;

            handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            return vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }
    }
}
#endif
