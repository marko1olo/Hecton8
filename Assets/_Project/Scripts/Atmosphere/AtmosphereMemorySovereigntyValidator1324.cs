#if UNITY_EDITOR
using Hecton8.Core;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Atmosphere
{
    internal static class AtmosphereMemorySovereigntyValidator1324
    {
        [InitializeOnLoadMethod]
        private static void Validate()
        {
            uint failureMask = 0u;
            uint auditFailureMask = 0u;

            failureMask |= UnsafeUtility.SizeOf<GasDynamicsSolver.PendingBaseTransitionSignal>() == 64 ? 0u : 1u << 0;
            failureMask |= OffsetOf<GasDynamicsSolver.PendingBaseTransitionSignal>(nameof(GasDynamicsSolver.PendingBaseTransitionSignal.BaseCenterAup)) == 0 ? 0u : 1u << 1;
            failureMask |= OffsetOf<GasDynamicsSolver.PendingBaseTransitionSignal>(nameof(GasDynamicsSolver.PendingBaseTransitionSignal.BaseId)) == 48 ? 0u : 1u << 2;
            failureMask |= OffsetOf<GasDynamicsSolver.PendingBaseTransitionSignal>(nameof(GasDynamicsSolver.PendingBaseTransitionSignal.RoomId)) == 52 ? 0u : 1u << 3;
            failureMask |= OffsetOf<GasDynamicsSolver.PendingBaseTransitionSignal>("_pad0") == 56 ? 0u : 1u << 4;
            failureMask |= OffsetOf<GasDynamicsSolver.PendingBaseTransitionSignal>(nameof(GasDynamicsSolver.PendingBaseTransitionSignal.Flags)) == 60 ? 0u : 1u << 5;
            failureMask |= OffsetOf<GasDynamicsSolver.PendingBaseTransitionSignal>(nameof(GasDynamicsSolver.PendingBaseTransitionSignal.IsEnter)) == 62 ? 0u : 1u << 6;
            failureMask |= OffsetOf<GasDynamicsSolver.PendingBaseTransitionSignal>("_pad1") == 63 ? 0u : 1u << 7;

            failureMask |= UnsafeUtility.SizeOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>() == 64 ? 0u : 1u << 8;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.PackedOwner)) == 0 ? 0u : 1u << 9;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.FrameIndex)) == 8 ? 0u : 1u << 10;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.RoomCount)) == 12 ? 0u : 1u << 11;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.TotalO2KPa)) == 16 ? 0u : 1u << 12;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.TotalCO2KPa)) == 20 ? 0u : 1u << 13;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.TotalNitrogenKPa)) == 24 ? 0u : 1u << 14;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.MaxPressureKPa)) == 28 ? 0u : 1u << 15;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.StateHash)) == 32 ? 0u : 1u << 16;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.BufferId)) == 36 ? 0u : 1u << 17;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.SystemId)) == 40 ? 0u : 1u << 18;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.Generation)) == 44 ? 0u : 1u << 19;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.DroppedUpdates)) == 48 ? 0u : 1u << 20;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.CpuMicroseconds)) == 52 ? 0u : 1u << 21;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>("_pad0") == 56 ? 0u : 1u << 22;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.Flags)) == 60 ? 0u : 1u << 23;
            failureMask |= OffsetOf<GasDynamicsSolver.GasDynamicsTelemetryEntry>(nameof(GasDynamicsSolver.GasDynamicsTelemetryEntry.Reserved)) == 62 ? 0u : 1u << 24;

            auditFailureMask |= UnsafeUtility.SizeOf<GasDynamicsNativeMemoryAudit>() == 48 ? 0u : 1u << 0;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.LocalRegisteredBytes)) == 0 ? 0u : 1u << 1;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.LargestAllocationBytes)) == 8 ? 0u : 1u << 2;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.SentinelTrackedBytes)) == 16 ? 0u : 1u << 3;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.RoomCapacity)) == 24 ? 0u : 1u << 4;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.BulkheadCapacity)) == 28 ? 0u : 1u << 5;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.LocalAllocationCount)) == 32 ? 0u : 1u << 6;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.SentinelActiveAllocationCount)) == 36 ? 0u : 1u << 7;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>(nameof(GasDynamicsNativeMemoryAudit.LargestAllocationLabelHash)) == 40 ? 0u : 1u << 8;
            auditFailureMask |= OffsetOf<GasDynamicsNativeMemoryAudit>("_pad0") == 44 ? 0u : 1u << 9;

            if (failureMask != 0u || auditFailureMask != 0u)
                UnityEngine.Assertions.Assert.IsTrue(false, "1324 gas dynamics DTO layout violation");
        }

        private static int OffsetOf<T>(string fieldName)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }
}
#endif
