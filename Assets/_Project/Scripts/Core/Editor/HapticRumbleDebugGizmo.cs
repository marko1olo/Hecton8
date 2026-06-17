#if UNITY_EDITOR
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Editor
{
    [InitializeOnLoad]
    internal static class HapticRumbleDebugGizmo
    {
        private static bool s_enabled;

        static HapticRumbleDebugGizmo()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem("Hecton8/Diagnostics/Haptic Rumble Gizmo")]
        private static void Toggle()
        {
            s_enabled = !s_enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("Hecton8/Diagnostics/Haptic Rumble Gizmo", true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked("HECTON-8/Diagnostics/Haptic Rumble Gizmo", s_enabled);
            return true;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!s_enabled || !Application.isPlaying)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            IPlayerRuntimeContext player = GlobalRegistry.Player;
            if (vault == null ||
                player == null ||
                !player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuHapticSynthesisFinalPulse, out VaultGenerationHandle<HapticPulseSignal> handle) ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<HapticPulseSignal>.ReadOnly pulseBuffer) ||
                !pulseBuffer.IsCreated ||
                pulseBuffer.Length <= 0)
            {
                return;
            }

            HapticPulseSignal pulse = pulseBuffer[0];
            float low = Mathf.Clamp01(pulse.LowFrequencyMotor01);
            float high = Mathf.Clamp01(pulse.HighFrequencyMotor01);
            if (low <= 0.001f && high <= 0.001f)
                return;

            Vector3 center = new Vector3(pose.RuntimePosition.x, pose.RuntimePosition.y, pose.RuntimePosition.z);
            float radius = Mathf.Lerp(0.35f, 4.5f, low);
            Handles.color = new Color(0.2f, 0.75f, 1f, Mathf.Lerp(0.15f, 0.85f, high));
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius * 0.75f);
            Handles.DrawWireDisc(center, Vector3.forward, radius * 0.75f);
        }
    }
}
#endif
