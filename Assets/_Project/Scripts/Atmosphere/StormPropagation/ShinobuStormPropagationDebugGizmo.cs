#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Atmosphere/Shinobu Storm Propagation Gizmo")]
    public sealed unsafe class ShinobuStormPropagationDebugGizmo : MonoBehaviour
    {
        private const SystemID OwnerSystem = SystemID.HabitatAtmosphere;

        [SerializeField] private float radiusMeters = 12f;
        [SerializeField] private float heightMeters = 36f;

        private void OnDrawGizmos()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryLockBuffer(BufferID.ShinobuStormPropagationState, OwnerSystem))
            {
                return;
            }

            StormPropagationDTO dto;
            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.ShinobuStormPropagationState, out VaultGenerationHandle<StormPropagationDTO> handle) ||
                    !vault.TryResolveHandle(in handle, out NativeArray<StormPropagationDTO> state) ||
                    !state.IsCreated ||
                    state.Length <= 0)
                {
                    return;
                }

                dto = ShinobuStormPropagationNative.ReadElement(state, 0);
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationState, OwnerSystem);
            }

            float turbidity = math.clamp(math.isfinite(dto.TurbidityScalar) ? dto.TurbidityScalar : 1f, 1f, 4f);
            Camera camera = Camera.current;
            Vector3 origin = camera != null ? camera.transform.position : transform.position;
            Vector3 top = origin + Vector3.up * math.max(1f, heightMeters);
            Vector3 surge = new Vector3(dto.SurgeVector.x, dto.SurgeVector.y, dto.SurgeVector.z);
            Gizmos.color = new Color(0.12f, 0.42f, 0.58f, math.saturate(0.18f * turbidity));
            DrawCircle(origin, radiusMeters);
            DrawCircle(top, radiusMeters);
            Gizmos.DrawLine(origin + Vector3.right * radiusMeters, top + Vector3.right * radiusMeters);
            Gizmos.DrawLine(origin - Vector3.right * radiusMeters, top - Vector3.right * radiusMeters);
            Gizmos.DrawLine(origin + Vector3.forward * radiusMeters, top + Vector3.forward * radiusMeters);
            Gizmos.DrawLine(origin - Vector3.forward * radiusMeters, top - Vector3.forward * radiusMeters);
            Gizmos.color = new Color(0.05f, 0.85f, 1f, 0.95f);
            Gizmos.DrawLine(origin, origin + surge);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            float safeRadius = math.max(0.1f, radius);
            Vector3 previous = center + new Vector3(safeRadius, 0f, 0f);
            for (int i = 1; i <= 32; i++)
            {
                float angle = i * (math.PI * 2f / 32f);
                Vector3 current = center + new Vector3(math.cos(angle) * safeRadius, 0f, math.sin(angle) * safeRadius);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }
}
#endif
