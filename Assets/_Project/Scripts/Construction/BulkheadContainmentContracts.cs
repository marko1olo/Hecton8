using System.Runtime.InteropServices;
#if UNITY_EDITOR
using System.Reflection;
#endif
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    public static class BulkheadContainmentConstants
    {
        public const int DefaultBulkheadCapacity = 256;
        public const int TelemetryFrameCount = 300;
        public const int ProfileCapacity = 32;
        public const int ShaderUploadCapacity = 256;
        public const uint MaxIntentAgeFrames = 120u;
        public const float PlaneCrossEpsilonMeters = 0.0001f;
        public const uint SystemHash = 0x53483232u;
        public const uint PreSimulationHash = 0x53483250u;
        public const uint SimulationHash = 0x53483253u;
        public const uint PostSimulationHash = 0x5348324Fu;
        public const uint VisualSyncHash = 0x53483256u;
        public const uint OverrideToolHash = 0x42484F56u;
    }

    public static class BulkheadStateFlags
    {
        public const uint None = 0u;
        public const uint Active = 1u << 0;
        public const uint Closing = 1u << 1;
        public const uint Sealed = 1u << 2;
        public const uint ManualOverride = 1u << 3;
        public const uint Jammed = 1u << 4;
        public const uint Destroyed = 1u << 5;
        public const uint CatastrophicDamage = 1u << 6;
        public const uint NonFinite = 1u << 31;
    }

    public static class BulkheadTelemetryFlags
    {
        public const uint None = 0u;
        public const uint NonFinite = 1u << 0;
        public const uint DumpRequested = 1u << 1;
        public const uint ScheduleTimeOnly = 1u << 2;
        public const uint IntentRejected = 1u << 3;
        public const uint IntentOverflowCompensated = 1u << 4;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadStateLayoutGuard.StateSizeBytes)]
    public struct BulkheadStateDTO
    {
        [FieldOffset(0)] public uint EdgeHashID;
        [FieldOffset(4)] public float ClosureProgress;
        [FieldOffset(8)] public uint AssociatedLock;
        [FieldOffset(12)] public uint SiblingNodeHash;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] private byte _pad0;
        [FieldOffset(21)] private byte _pad1;
        [FieldOffset(22)] private byte _pad2;
        [FieldOffset(23)] private byte _pad3;
        [FieldOffset(24)] private byte _pad4;
        [FieldOffset(25)] private byte _pad5;
        [FieldOffset(26)] private byte _pad6;
        [FieldOffset(27)] private byte _pad7;
        [FieldOffset(28)] private byte _pad8;
        [FieldOffset(29)] private byte _pad9;
        [FieldOffset(30)] private byte _pad10;
        [FieldOffset(31)] private byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadStateLayoutGuard.PlaneSizeBytes)]
    public struct BulkheadPlaneDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float WidthMeters;
        [FieldOffset(40)] public float HeightMeters;
        [FieldOffset(44)] public float HalfThicknessMeters;
        [FieldOffset(48)] public uint EdgeHashID;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint IntegrityIndex;
        [FieldOffset(60)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadStateLayoutGuard.CsrEdgeSizeBytes)]
    public struct BulkheadCsrEdgeDTO
    {
        [FieldOffset(0)] public uint EdgeHashID;
        [FieldOffset(4)] public int ConductivityIndex;
        [FieldOffset(8)] public int FluidFlowIndex;
        [FieldOffset(12)] public float OpenConductivity;
        [FieldOffset(16)] public float OpenFluidFlow;
        [FieldOffset(20)] public int IntegrityIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadStateLayoutGuard.TuningSizeBytes)]
    public struct BulkheadTuningDTO
    {
        [FieldOffset(0)] public float CloseSpeedPerSecond;
        [FieldOffset(4)] public float OpenSpeedPerSecond;
        [FieldOffset(8)] public float OverrideDistanceMeters;
        [FieldOffset(12)] public float CatastrophicIntegrity01;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float AuthorityCadenceHz;
        [FieldOffset(24)] public uint ActiveCount;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadStateLayoutGuard.ProfileSizeBytes)]
    public struct BulkheadProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float CloseSpeedPerSecond;
        [FieldOffset(8)] public float OpenSpeedPerSecond;
        [FieldOffset(12)] public float OverrideDistanceMeters;
        [FieldOffset(16)] public float CatastrophicIntegrity01;
        [FieldOffset(20)] public float WidthMeters;
        [FieldOffset(24)] public float HeightMeters;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = BulkheadStateLayoutGuard.TelemetrySizeBytes)]
    public struct BulkheadTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveCount;
        [FieldOffset(8)] public uint SealedCount;
        [FieldOffset(12)] public uint JammedCount;
        [FieldOffset(16)] public float AverageClosure;
        [FieldOffset(20)] public float AuthorityCadenceHz;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float LastScheduleMicroseconds;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint CollisionEdgeHash;
        [FieldOffset(40)] public float CollisionDepthMeters;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    public static class BulkheadStateLayoutGuard
    {
        public const int StateSizeBytes = 32;
        public const int PlaneSizeBytes = 64;
        public const int CsrEdgeSizeBytes = 32;
        public const int TuningSizeBytes = 32;
        public const int ProfileSizeBytes = 32;
        public const int TelemetrySizeBytes = 64;
        public const int SizeBytes = StateSizeBytes;

        public static bool ValidateLayout()
        {
            return ValidateStateLayout() &&
                   ValidatePlaneLayout() &&
                   ValidateCsrEdgeLayout() &&
                   ValidateTuningLayout() &&
                   ValidateProfileLayout() &&
                   ValidateTelemetryLayout();
        }

        private static bool ValidateStateLayout()
        {
            if (UnsafeUtility.SizeOf<BulkheadStateDTO>() != StateSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<BulkheadStateDTO>(nameof(BulkheadStateDTO.EdgeHashID)) == 0 &&
                   GetOffset<BulkheadStateDTO>(nameof(BulkheadStateDTO.ClosureProgress)) == 4 &&
                   GetOffset<BulkheadStateDTO>(nameof(BulkheadStateDTO.AssociatedLock)) == 8 &&
                   GetOffset<BulkheadStateDTO>(nameof(BulkheadStateDTO.SiblingNodeHash)) == 12 &&
                   GetOffset<BulkheadStateDTO>(nameof(BulkheadStateDTO.Flags)) == 16 &&
                   GetOffset<BulkheadStateDTO>("_pad0") == 20 &&
                   GetOffset<BulkheadStateDTO>("_pad11") == 31;
#else
            return true;
#endif
        }

        private static bool ValidatePlaneLayout()
        {
            if (UnsafeUtility.SizeOf<BulkheadPlaneDTO>() != PlaneSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.CenterAup)) == 0 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.Normal)) == 24 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.WidthMeters)) == 36 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.HeightMeters)) == 40 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.HalfThicknessMeters)) == 44 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.EdgeHashID)) == 48 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.Flags)) == 52 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.IntegrityIndex)) == 56 &&
                   GetOffset<BulkheadPlaneDTO>(nameof(BulkheadPlaneDTO.Reserved)) == 60;
#else
            return true;
#endif
        }

        private static bool ValidateCsrEdgeLayout()
        {
            if (UnsafeUtility.SizeOf<BulkheadCsrEdgeDTO>() != CsrEdgeSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.EdgeHashID)) == 0 &&
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.ConductivityIndex)) == 4 &&
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.FluidFlowIndex)) == 8 &&
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.OpenConductivity)) == 12 &&
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.OpenFluidFlow)) == 16 &&
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.IntegrityIndex)) == 20 &&
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.Flags)) == 24 &&
                   GetOffset<BulkheadCsrEdgeDTO>(nameof(BulkheadCsrEdgeDTO.Reserved)) == 28;
#else
            return true;
#endif
        }

        private static bool ValidateTuningLayout()
        {
            if (UnsafeUtility.SizeOf<BulkheadTuningDTO>() != TuningSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.CloseSpeedPerSecond)) == 0 &&
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.OpenSpeedPerSecond)) == 4 &&
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.OverrideDistanceMeters)) == 8 &&
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.CatastrophicIntegrity01)) == 12 &&
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.GlobalQualityWeight)) == 16 &&
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.AuthorityCadenceHz)) == 20 &&
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.ActiveCount)) == 24 &&
                   GetOffset<BulkheadTuningDTO>(nameof(BulkheadTuningDTO.Flags)) == 28;
#else
            return true;
#endif
        }

        private static bool ValidateProfileLayout()
        {
            if (UnsafeUtility.SizeOf<BulkheadProfileDTO>() != ProfileSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.ProfileHash)) == 0 &&
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.CloseSpeedPerSecond)) == 4 &&
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.OpenSpeedPerSecond)) == 8 &&
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.OverrideDistanceMeters)) == 12 &&
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.CatastrophicIntegrity01)) == 16 &&
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.WidthMeters)) == 20 &&
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.HeightMeters)) == 24 &&
                   GetOffset<BulkheadProfileDTO>(nameof(BulkheadProfileDTO.Flags)) == 28;
#else
            return true;
#endif
        }

        private static bool ValidateTelemetryLayout()
        {
            if (UnsafeUtility.SizeOf<BulkheadTelemetryEntry>() != TelemetrySizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.Frame)) == 0 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.ActiveCount)) == 4 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.SealedCount)) == 8 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.JammedCount)) == 12 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.AverageClosure)) == 16 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.AuthorityCadenceHz)) == 20 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.GlobalQualityWeight)) == 24 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.LastScheduleMicroseconds)) == 28 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.StateHash)) == 32 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.CollisionEdgeHash)) == 36 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.CollisionDepthMeters)) == 40 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.Flags)) == 44 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.Reserved0)) == 48 &&
                   GetOffset<BulkheadTelemetryEntry>(nameof(BulkheadTelemetryEntry.Reserved1)) == 56;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        private static int GetOffset<T>(string fieldName)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
#endif
    }
}
