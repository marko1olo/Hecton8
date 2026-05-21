#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Editor
{
    /// <summary>
    /// Scene View x-ray for vault-backed AUP sample requests and 16-byte fluid results.
    /// </summary>
    [InitializeOnLoad]
    public static class OceanKinematicsAupSamplingGizmo
    {
        private const string EnabledKey = "Hecton8.OceanKinematics.AupGizmo.Enabled";
        private const int MaxDrawnSamples = 512;
        private static bool _enabled;

        static OceanKinematicsAupSamplingGizmo()
        {
            _enabled = EditorPrefs.GetBool(EnabledKey, false);
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem("Hecton/Physics/Toggle Ocean AUP Sampling Gizmo")]
        public static void Toggle()
        {
            _enabled = !_enabled;
            EditorPrefs.SetBool(EnabledKey, _enabled);
            SceneView.RepaintAll();
        }

        [MenuItem("Hecton/Physics/Toggle Ocean AUP Sampling Gizmo", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("Hecton/Physics/Toggle Ocean AUP Sampling Gizmo", _enabled);
            return true;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!_enabled)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !OceanKinematicsVaultRuntime.EnsureBuffers(vault, out OceanKinematicsVaultRuntime.Views views) ||
                !views.Requests.IsCreated ||
                !views.Results.IsCreated)
            {
                return;
            }

            int count = ResolveSampleCount(views);
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            Handles.color = new Color(0.1f, 0.65f, 1f, 0.85f);
            for (int i = 0; i < count; i++)
            {
                OceanKinematicsSampleRequestDTO request = views.Requests[i];
                FluidSampleResultDTO result = views.Results[i];
                if (!math.all(math.isfinite(request.RequestedAUP)) || !math.isfinite(result.WaterHeight))
                    continue;

                double3 local = request.RequestedAUP - origin;
                Vector3 position = new Vector3((float)local.x, result.WaterHeight, (float)local.z);
                float radius = math.clamp(0.18f + math.length(result.SurfaceVelocity) * 0.04f, 0.18f, 1.2f);
                Handles.color = new Color(0.08f, 0.6f, 1f, 0.85f);
                Handles.DrawWireDisc(position, Vector3.up, radius);
                Handles.DrawWireDisc(position, Vector3.right, radius);
                Handles.DrawWireDisc(position, Vector3.forward, radius);
                Vector3 velocity = new Vector3(result.SurfaceVelocity.x, result.SurfaceVelocity.y, result.SurfaceVelocity.z);
                Handles.color = new Color(0.1f, 0.25f, 1f, 0.9f);
                Handles.DrawLine(position, position + velocity);
            }
        }

        private static int ResolveSampleCount(OceanKinematicsVaultRuntime.Views views)
        {
            if (!views.QueueCounters.IsCreated ||
                views.QueueCounters.Length <= OceanKinematicsConstants.QueueCounterPacked)
            {
                return 0;
            }

            int packedCount = views.QueueCounters[OceanKinematicsConstants.QueueCounterPacked];
            if (packedCount <= 0)
                return 0;

            int count = math.min(views.Requests.Length, views.Results.Length);
            return math.min(math.min(count, packedCount), MaxDrawnSamples);
        }
    }
}
#endif
