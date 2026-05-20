#if UNITY_EDITOR
using System;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Diagnostics.Visuals
{
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/Core/Vault Memory Gizmo Visualizer")]
    public sealed class VaultMemoryGizmoVisualizer : MonoBehaviour
    {
        private const int DefaultMaxDrawn = 256;
        private const float DefaultWireSizeMeters = 1.25f;
        private const uint SwapPopFlashFrames = 90u;

        [SerializeField] private bool drawVaultMemoryGizmos = true;
        [SerializeField, Range(1, 4096)] private int maxDrawn = DefaultMaxDrawn;
        [SerializeField, Min(0.1f)] private float wireSizeMeters = DefaultWireSizeMeters;

        private void OnDrawGizmos()
        {
            if (!drawVaultMemoryGizmos ||
                !GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) ||
                !vault.TryGetBuffer(BufferID.VaultAup64, out NativeArray<VaultAup64> aups) ||
                !vault.TryGetBuffer(BufferID.VaultHotEntityData, out NativeArray<VaultHotEntityData> hotEntities) ||
                !aups.IsCreated ||
                !hotEntities.IsCreated)
            {
                return;
            }

            int count = math.min(math.min(aups.Length, hotEntities.Length), math.max(1, maxDrawn));
            if (count <= 0)
                return;

            uint frame = unchecked((uint)Time.frameCount);
            ReadOnlySpan<MemoryAddressShiftSignal> shifts = SignalBus<MemoryAddressShiftSignal>.GetFrameSnapshot();
            Vector3 size = new Vector3(wireSizeMeters, wireSizeMeters, wireSizeMeters);
            DrawLastPointerFault(vault, aups, count, frame);
            for (int i = 0; i < count; i++)
            {
                VaultHotEntityData hot = hotEntities[i];
                if (hot.EntityId == 0u)
                    continue;

                Vector3 runtimePosition = ReconstructRuntimePosition(in aups[i]);
                Gizmos.color = WasMovedBySwapPop(i, hot.EntityId, frame, shifts)
                    ? Color.yellow
                    : Color.green;
                Gizmos.DrawWireCube(runtimePosition, size);
            }
        }

        private static void DrawLastPointerFault(
            GlobalDataVault vault,
            NativeArray<VaultAup64> aups,
            int count,
            uint frame)
        {
            if (count <= 0 ||
                !vault.TryGetVaultTelemetrySnapshot(0, out VaultTelemetrySnapshot telemetry) ||
                telemetry.GenerationMismatchCount == 0u ||
                telemetry.LastFaultBufferID <= 0)
            {
                return;
            }

            int index = math.abs(telemetry.LastFaultBufferID) % count;
            float pulse = 0.75f + (0.25f * math.sin(frame * 0.21f));
            Vector3 position = ReconstructRuntimePosition(in aups[index]);
            Gizmos.color = new Color(1f, 0f, 0f, 0.85f);
            Gizmos.DrawWireSphere(position, DefaultWireSizeMeters * (2f + pulse));
        }

        private static Vector3 ReconstructRuntimePosition(in VaultAup64 aup)
        {
            const double sectorSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double3 absolute = new double3(
                (aup.SectorX * sectorSize) + aup.LocalX,
                (aup.SectorY * sectorSize) + aup.LocalY,
                (aup.SectorZ * sectorSize) + aup.LocalZ);
            double3 runtime = absolute - HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double3 clamped = math.clamp(runtime, new double3(-100000.0d), new double3(100000.0d));
            return math.all(math.isfinite(clamped))
                ? new Vector3((float)clamped.x, (float)clamped.y, (float)clamped.z)
                : Vector3.zero;
        }

        private static bool WasMovedBySwapPop(
            int currentIndex,
            uint entityId,
            uint frame,
            ReadOnlySpan<MemoryAddressShiftSignal> shifts)
        {
            for (int i = 0; i < shifts.Length; i++)
            {
                MemoryAddressShiftSignal shift = shifts[i];
                if ((shift.Flags & MemoryAddressShiftSignal.FlagSwapPopIndexMove) == 0 ||
                    shift.BufferId != (int)BufferID.VaultHotEntityData ||
                    shift.NewIndex != currentIndex ||
                    shift.MovedEntityId != entityId)
                {
                    continue;
                }

                uint age = frame >= shift.SourceFrame ? frame - shift.SourceFrame : 0u;
                return age <= SwapPopFlashFrames;
            }

            return false;
        }
    }
}
#endif
