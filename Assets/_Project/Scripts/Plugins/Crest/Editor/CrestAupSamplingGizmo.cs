#if UNITY_EDITOR
using Hecton8.Crest.Bridge;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment.Fluids;
using Unity.Collections;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class CrestAupSamplingGizmo
    {
        private const string ToggleMenuPath = "Tools/Hecton8/Crest/Toggle AUP Sampling Gizmo";
        private static bool s_enabled;

        static CrestAupSamplingGizmo()
        {
            SceneView.duringSceneGui += DrawSceneGizmo;
        }

        [MenuItem(ToggleMenuPath, priority = 4201)]
        private static void Toggle()
        {
            s_enabled = !s_enabled;
            SceneView.RepaintAll();
        }

        [MenuItem(ToggleMenuPath, validate = true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(ToggleMenuPath, s_enabled);
            return true;
        }

        private static void DrawSceneGizmo(SceneView sceneView)
        {
            if (!s_enabled || sceneView == null)
                return;

            CrestOceanRuntimeAdapter[] adapters = Object.FindObjectsByType<CrestOceanRuntimeAdapter>(
                FindObjectsInactive.Include);
            Color previous = Handles.color;
            Handles.color = new Color(0.1f, 0.9f, 0.45f, 0.85f);
            for (int i = 0; i < adapters.Length; i++)
            {
                CrestOceanRuntimeAdapter adapter = adapters[i];
                if (adapter == null)
                    continue;

                Vector3 position = adapter.transform.position;
                Handles.DrawWireDisc(position, Vector3.up, 8f);
                Handles.DrawLine(position + Vector3.left * 8f, position + Vector3.right * 8f);
                Handles.DrawLine(position + Vector3.forward * 8f, position + Vector3.back * 8f);
            }

            DrawVaultSamples();
            Handles.color = previous;
        }

        private static void DrawVaultSamples()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle<OceanSampleRequestDTO>(
                    OceanAdapterVaultRoute.RequestBufferID,
                    out VaultGenerationHandle<OceanSampleRequestDTO> requestHandle) ||
                !vault.TryResolveHandle(in requestHandle, out NativeArray<OceanSampleRequestDTO> requests) ||
                !requests.IsCreated ||
                requests.Length == 0)
            {
                return;
            }

            NativeArray<OceanSampleResultDTO> results = default;
            bool hasResults = vault.TryGetGenerationHandle<OceanSampleResultDTO>(
                                  OceanAdapterVaultRoute.ResultBufferID,
                                  out VaultGenerationHandle<OceanSampleResultDTO> resultHandle) &&
                              vault.TryResolveHandle(in resultHandle, out results) &&
                              results.IsCreated &&
                              results.Length > 0;
            int count = Mathf.Min(requests.Length, 512);
            double3 runtimeOriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            for (int i = 0; i < count; i++)
            {
                OceanSampleRequestDTO request = requests[i];
                double3 aup = request.RequestAUP;
                if (!math.all(math.isfinite(aup)))
                    continue;

                double3 localAUP = aup - runtimeOriginAUP;
                if (!math.all(math.isfinite(localAUP)))
                    continue;

                Color color = Color.green;
                if (hasResults && i < results.Length)
                {
                    uint flags = results[i].StatusFlags;
                    if ((flags & (uint)OceanSampleStatus.SimplifiedByQualityBudget) != 0u)
                        color = Color.yellow;
                    if ((flags & (uint)OceanSampleStatus.NonFiniteInput) != 0u)
                        color = Color.red;
                }

                Handles.color = color;
                Handles.DrawWireDisc(new Vector3((float)localAUP.x, (float)localAUP.y, (float)localAUP.z), Vector3.up, 0.35f);
            }
        }
    }
}
#endif
