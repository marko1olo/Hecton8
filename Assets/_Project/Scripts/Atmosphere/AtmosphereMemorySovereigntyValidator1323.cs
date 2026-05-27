#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Atmosphere
{
    internal static class AtmosphereMemorySovereigntyValidator1323
    {
        [InitializeOnLoadMethod]
        private static void Validate()
        {
            ulong failureMask = 0ul;
            failureMask |= UnsafeUtility.SizeOf<HighPressureEventPayload>() == 32 ? 0ul : 1ul << 0;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.RuntimePositionX)) == 0 ? 0ul : 1ul << 1;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.RuntimePositionY)) == 4 ? 0ul : 1ul << 2;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.RuntimePositionZ)) == 8 ? 0ul : 1ul << 3;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.PressureAKPa)) == 12 ? 0ul : 1ul << 4;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.PressureBKPa)) == 16 ? 0ul : 1ul << 5;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.DoorIndex)) == 20 ? 0ul : 1ul << 6;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.RoomA)) == 24 ? 0ul : 1ul << 7;
            failureMask |= OffsetOf<HighPressureEventPayload>(nameof(HighPressureEventPayload.RoomB)) == 28 ? 0ul : 1ul << 8;

            failureMask |= UnsafeUtility.SizeOf<FatalPressureImplosionEventPayload>() == 32 ? 0ul : 1ul << 9;
            failureMask |= OffsetOf<FatalPressureImplosionEventPayload>(nameof(FatalPressureImplosionEventPayload.RuntimePositionX)) == 0 ? 0ul : 1ul << 10;
            failureMask |= OffsetOf<FatalPressureImplosionEventPayload>(nameof(FatalPressureImplosionEventPayload.RuntimePositionY)) == 4 ? 0ul : 1ul << 11;
            failureMask |= OffsetOf<FatalPressureImplosionEventPayload>(nameof(FatalPressureImplosionEventPayload.RuntimePositionZ)) == 8 ? 0ul : 1ul << 12;
            failureMask |= OffsetOf<FatalPressureImplosionEventPayload>(nameof(FatalPressureImplosionEventPayload.TemperatureCelsius)) == 12 ? 0ul : 1ul << 13;
            failureMask |= OffsetOf<FatalPressureImplosionEventPayload>(nameof(FatalPressureImplosionEventPayload.NodeId)) == 16 ? 0ul : 1ul << 14;
            failureMask |= OffsetOf<FatalPressureImplosionEventPayload>(nameof(FatalPressureImplosionEventPayload.RoomIndex)) == 20 ? 0ul : 1ul << 15;

            failureMask |= UnsafeUtility.SizeOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>() == 64 ? 0ul : 1ul << 16;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.PackedOwner)) == 0 ? 0ul : 1ul << 17;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.FrameIndex)) == 8 ? 0ul : 1ul << 18;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.RoomCount)) == 12 ? 0ul : 1ul << 19;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.DeltaTimeSeconds)) == 16 ? 0ul : 1ul << 20;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.TotalO2KPa)) == 20 ? 0ul : 1ul << 21;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.TotalCO2KPa)) == 24 ? 0ul : 1ul << 22;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.TotalNitrogenKPa)) == 28 ? 0ul : 1ul << 23;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.MaxPressureKPa)) == 32 ? 0ul : 1ul << 24;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.StateHash)) == 36 ? 0ul : 1ul << 25;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.BufferId)) == 40 ? 0ul : 1ul << 26;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.SystemId)) == 44 ? 0ul : 1ul << 27;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.Generation)) == 48 ? 0ul : 1ul << 28;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.RuntimeRoomStatusMask)) == 52 ? 0ul : 1ul << 29;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.DroppedSignals)) == 56 ? 0ul : 1ul << 30;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.Flags)) == 60 ? 0ul : 1ul << 31;
            failureMask |= OffsetOf<SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry>(nameof(SubmarineAtmosphereSystem.SubmarineAtmosphereTelemetryEntry.FailureCode)) == 62 ? 0ul : 1ul << 32;

            if (failureMask != 0ul)
                throw new FatalArchitectureException("1323 atmosphere memory sovereignty DTO layout violation mask=" + failureMask);
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return (int)Marshal.OffsetOf<T>(fieldName);
        }
    }
}
#endif
