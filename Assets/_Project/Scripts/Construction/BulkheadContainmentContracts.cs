using System.Runtime.InteropServices;
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
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BulkheadStateDTO
    {
        [FieldOffset(0)] public uint EdgeHashID;
        [FieldOffset(4)] public float ClosureProgress;
        [FieldOffset(8)] public uint AssociatedLock;
        [FieldOffset(12)] public uint SiblingNodeHash;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
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
        public const int SizeBytes = 32;

        public static bool ValidateLayout()
        {
            return UnsafeUtility.SizeOf<BulkheadStateDTO>() == SizeBytes &&
                   GetOffset(nameof(BulkheadStateDTO.EdgeHashID)) == 0 &&
                   GetOffset(nameof(BulkheadStateDTO.ClosureProgress)) == 4 &&
                   GetOffset(nameof(BulkheadStateDTO.AssociatedLock)) == 8 &&
                   GetOffset(nameof(BulkheadStateDTO.SiblingNodeHash)) == 12 &&
                   GetOffset(nameof(BulkheadStateDTO.Flags)) == 16 &&
                   GetOffset(nameof(BulkheadStateDTO._pad0)) == 20 &&
                   GetOffset(nameof(BulkheadStateDTO._pad11)) == 31;
        }

        private static int GetOffset(string fieldName)
        {
            var field = typeof(BulkheadStateDTO).GetField(fieldName);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }
}
