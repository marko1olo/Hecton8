using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public static class AsyncBuoyancyReadbackConstants
    {
        public const int RequestCapacity = 512;
        public const int TelemetryCapacity = 300;
        public const int ReadbackRingSize = 3;
        public const int MockLatencyFrames = 3;
        public const int MaxFreshAgeFrames = 5;
        public const int VehicleProfileCapacity = 64;
#if UNITY_EDITOR
        public const int CsvScratchBytes = 65536;
#endif
        public const float AuthoritativeQualityWeight = 1f;
        public const int WaveCapacity = 2;
        public const int ReadbackRequestBytes = 16;
        public const int ReadbackResolvedHeightBytes = 32;
        public const int ReadbackResultStateBytes = 64;
        public const int ReadbackTuningBytes = 64;
        public const int ReadbackTelemetryBytes = 64;
        public const int VehicleSamplingProfileBytes = 32;
        public const int WaveParametersBytes = 64;
#if UNITY_EDITOR
        public const string CsvRelativePath = "Data/Physics/vehicle_sampling_profiles.csv";
#endif

        public const uint FlagActive = 1u << 0;
        public const uint FlagGpuPath = 1u << 1;
        public const uint FlagMockPath = 1u << 2;
        public const uint FlagStale = 1u << 3;
        public const uint FlagDeadReckoned = 1u << 4;
        public const uint FlagReadbackError = 1u << 5;
        public const uint FlagDroppedSlot = 1u << 6;
        public const uint FlagDumpedLatency = 1u << 7;
        public const uint FlagNonFinite = 1u << 31;
    }

    public static class AsyncBuoyancyReadbackBufferIds
    {
        public const BufferID Requests = (BufferID)71820;
        public const BufferID CompletedRequests = (BufferID)71821;
        public const BufferID ResolvedHeights = (BufferID)71822;
        public const BufferID ResultStates = (BufferID)71823;
        public const BufferID Tuning = (BufferID)71824;
        public const BufferID TelemetryRing = (BufferID)71825;
        public const BufferID TelemetryCursor = (BufferID)71826;
        public const BufferID MockRing = (BufferID)71827;
        public const BufferID FallbackWaves = (BufferID)71828;
        public const BufferID VehicleSamplingProfiles = (BufferID)71829;
#if UNITY_EDITOR
        public const BufferID CsvScratch = (BufferID)71830;
#endif
        public const BufferID Counter = (BufferID)71831;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsyncBuoyancyReadbackConstants.ReadbackRequestBytes)]
    public struct ReadbackRequestDTO
    {
        [FieldOffset(0)] public float2 LocalXZ;
        [FieldOffset(8)] public float ResultHeight;
        [FieldOffset(12)] public uint EntityHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsyncBuoyancyReadbackConstants.ReadbackResolvedHeightBytes)]
    public struct ReadbackResolvedHeightDTO
    {
        [FieldOffset(0)] public double HeightAupY;
        [FieldOffset(8)] public float LocalHeight;
        [FieldOffset(12)] public float VelocityY;
        [FieldOffset(16)] public uint EntityHash;
        [FieldOffset(20)] public uint FrameIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsyncBuoyancyReadbackConstants.ReadbackResultStateBytes)]
    public struct ReadbackResultStateDTO
    {
        [FieldOffset(0)] public double LastHeightAupY;
        [FieldOffset(8)] public double CameraAupY;
        [FieldOffset(16)] public float LastLocalHeight;
        [FieldOffset(20)] public float PreviousLocalHeight;
        [FieldOffset(24)] public float VelocityY;
        [FieldOffset(28)] public float LastLocalX;
        [FieldOffset(32)] public float LastLocalZ;
        [FieldOffset(36)] public uint EntityHash;
        [FieldOffset(40)] public uint LastFrameIndex;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public int StaleFrames;
        [FieldOffset(52)] public float SmoothedLocalHeight;
        [FieldOffset(56)] public float DeadReckonedLocalHeight;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsyncBuoyancyReadbackConstants.ReadbackTuningBytes)]
    public struct ReadbackTuningDTO
    {
        [FieldOffset(0)] public double3 CameraAup;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float FixedDeltaTime;
        [FieldOffset(32)] public int ActiveRequestCount;
        [FieldOffset(36)] public int ActiveCompletedCount;
        [FieldOffset(40)] public int MinSampleCount;
        [FieldOffset(44)] public int MaxSampleCount;
        [FieldOffset(48)] public uint FrameIndex;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float SmoothingAlpha;
        [FieldOffset(60)] public float DeadReckoningDecayRate;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsyncBuoyancyReadbackConstants.ReadbackTelemetryBytes)]
    public struct ReadbackTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int RequestedSamples;
        [FieldOffset(8)] public int CompletedSamples;
        [FieldOffset(12)] public int ActiveRingSlots;
        [FieldOffset(16)] public int ReadbackLatencyFrames;
        [FieldOffset(20)] public int DroppedRequests;
        [FieldOffset(24)] public int FailedRequests;
        [FieldOffset(28)] public int MaxStaleFrames;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float ApplyMicros;
        [FieldOffset(40)] public float DispatchMicros;
        [FieldOffset(44)] public float SmoothedAlpha;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint LastEntityHash;
        [FieldOffset(56)] public float LastLocalHeight;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsyncBuoyancyReadbackConstants.VehicleSamplingProfileBytes)]
    public struct VehicleSamplingProfileDTO
    {
        [FieldOffset(0)] public uint VehicleHash;
        [FieldOffset(4)] public float LengthMeters;
        [FieldOffset(8)] public float BeamMeters;
        [FieldOffset(12)] public float DraftMeters;
        [FieldOffset(16)] public int MinSamples;
        [FieldOffset(20)] public int MaxSamples;
        [FieldOffset(24)] public float InsetMeters;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsyncBuoyancyReadbackConstants.WaveParametersBytes)]
    public struct AsyncBuoyancyWaveParametersDTO
    {
        [FieldOffset(0)] public float4 Wave1;
        [FieldOffset(16)] public float4 Wave2;
        [FieldOffset(32)] public float4 Wave3;
        [FieldOffset(48)] public float4 GlobalWindAndStorm;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AsyncReadbackCounterDTO
    {
        [FieldOffset(0)] public int QueuedCount;
        [FieldOffset(4)] public int DispatchCount;
        [FieldOffset(8)] public int CompletedCount;
        [FieldOffset(12)] public int DroppedRequests;
        [FieldOffset(16)] public int FailedRequests;
        [FieldOffset(20)] public int ActiveRingSlots;
        [FieldOffset(24)] public int LastLatencyFrames;
        [FieldOffset(28)] public int MaxStaleFrames;
        [FieldOffset(32)] public uint FrameIndex;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint LastEntityHash;
        [FieldOffset(44)] public float LastLocalHeight;
        [FieldOffset(48)] public float ApplyMicros;
        [FieldOffset(52)] public float DispatchMicros;
        [FieldOffset(56)] public ulong _pad0;
    }

    public static class AsyncBuoyancyReadbackLayout
    {
        private static readonly bool s_validateOnce = ValidateInternal();

        public static bool Validate()
        {
            return s_validateOnce;
        }

        private static bool ValidateInternal()
        {
            const int VectorLaneAlignmentBytes = 16;
            const int CacheLineAlignmentBytes = 64;

            return UnsafeUtility.SizeOf<ReadbackRequestDTO>() == AsyncBuoyancyReadbackConstants.ReadbackRequestBytes &&
                   UnsafeUtility.AlignOf<ReadbackRequestDTO>() >= 4 &&
                   IsMultipleOf(UnsafeUtility.SizeOf<ReadbackRequestDTO>(), VectorLaneAlignmentBytes) &&
                   IsMultipleOf(UnsafeUtility.SizeOf<ReadbackResolvedHeightDTO>(), VectorLaneAlignmentBytes) &&
                   IsMultipleOf(UnsafeUtility.SizeOf<ReadbackResultStateDTO>(), CacheLineAlignmentBytes) &&
                   IsMultipleOf(UnsafeUtility.SizeOf<ReadbackTuningDTO>(), CacheLineAlignmentBytes) &&
                   IsMultipleOf(UnsafeUtility.SizeOf<ReadbackTelemetryEntry>(), CacheLineAlignmentBytes) &&
                   IsMultipleOf(UnsafeUtility.SizeOf<AsyncReadbackCounterDTO>(), CacheLineAlignmentBytes) &&
                   UnsafeUtility.SizeOf<ReadbackResolvedHeightDTO>() == AsyncBuoyancyReadbackConstants.ReadbackResolvedHeightBytes &&
                   UnsafeUtility.SizeOf<ReadbackResultStateDTO>() == AsyncBuoyancyReadbackConstants.ReadbackResultStateBytes &&
                   UnsafeUtility.SizeOf<ReadbackTuningDTO>() == AsyncBuoyancyReadbackConstants.ReadbackTuningBytes &&
                   UnsafeUtility.SizeOf<ReadbackTelemetryEntry>() == AsyncBuoyancyReadbackConstants.ReadbackTelemetryBytes &&
                   UnsafeUtility.SizeOf<VehicleSamplingProfileDTO>() == AsyncBuoyancyReadbackConstants.VehicleSamplingProfileBytes &&
                   UnsafeUtility.SizeOf<AsyncBuoyancyWaveParametersDTO>() == AsyncBuoyancyReadbackConstants.WaveParametersBytes &&
                   UnsafeUtility.SizeOf<AsyncReadbackCounterDTO>() == CacheLineAlignmentBytes &&
                   OffsetOfReadbackRequest(nameof(ReadbackRequestDTO.LocalXZ)) == 0 &&
                   OffsetOfReadbackRequest(nameof(ReadbackRequestDTO.ResultHeight)) == 8 &&
                   OffsetOfReadbackRequest(nameof(ReadbackRequestDTO.EntityHash)) == 12;
        }

        private static int OffsetOfReadbackRequest(string fieldName)
        {
            return Marshal.OffsetOf<ReadbackRequestDTO>(fieldName).ToInt32();
        }

        private static bool IsMultipleOf(int value, int alignment)
        {
            return alignment > 0 && value > 0 && (value % alignment) == 0;
        }
    }
}
