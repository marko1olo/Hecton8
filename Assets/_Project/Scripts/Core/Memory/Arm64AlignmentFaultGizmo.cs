using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Memory
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Arm64AlignmentFaultGizmo : MonoBehaviour
    {
        [SerializeField] private Vector3 boxSize = new Vector3(1.5f, 1.5f, 1.5f);
        [SerializeField] private float pulseSpeed = 5f;

        private void OnDrawGizmos()
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            if (!Arm64AlignmentTelemetry.TryGetNewestFault(vault, out AlignmentTelemetryEntry entry))
                return;

            if ((entry.Flags & (AlignmentTelemetryFlags.MisalignedEightByteField | AlignmentTelemetryFlags.InvalidStride | AlignmentTelemetryFlags.DynamicCastFault)) == 0u)
                return;

            double3 p = entry.AupOrRuntimePosition;
            if (!math.all(math.isfinite(p)))
                return;

            float pulse = 0.55f + (0.45f * math.abs(math.sin(Time.realtimeSinceStartup * math.max(0.01f, pulseSpeed))));
            Gizmos.color = new Color(1f, 0.05f, 0.02f, pulse);
            Gizmos.DrawWireCube(new Vector3((float)p.x, (float)p.y, (float)p.z), boxSize);
        }
    }
}
