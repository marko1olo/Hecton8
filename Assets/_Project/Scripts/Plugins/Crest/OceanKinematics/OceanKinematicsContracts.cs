using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// Constants for Burst ocean-kinematics sampling.
    /// </summary>
    public static class OceanKinematicsConstants
    {
        public const int RequestCapacity = 50000;
        public const int WaveCapacity = 8;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 65536;
        public const int FluidSampleResultBytes = 16;
        public const int OceanSampleRequestBytes = 40;
        public const int GerstnerWaveBytes = 40;
        public const int TuningBytes = 64;
        public const int MacroStateBytes = 32;
        public const int TelemetryBytes = 64;
        public const int CachedSampleBytes = 32;
        public const int QueueCountersBytes = 32;
        public const int RollbackFenceBytes = 32;
        public const int QueueCounterCapacity = 8;
        public const float TwoPi = 6.2831853071795864769f;
        public const float RcpTwoPi = 0.15915494309189533577f;
        public const float DefaultDepthCullMeters = 50f;
        public const float DefaultAmplitudeMultiplier = 1f;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_261.bin";

        public const uint FlagActive = 1u << 0;
        public const uint FlagDepthCulled = 1u << 1;
        public const uint FlagMockWave = 1u << 2;
        public const uint FlagAnalyticalWave = 1u << 3;
        public const uint FlagAsyncCached = 1u << 4;
        public const uint FlagGpuReadbackQueued = 1u << 5;
        public const uint FlagCacheMiss = 1u << 6;
        public const uint FlagNonFinite = 1u << 31;

        public const int QueueCounterPacked = 0;
        public const int QueueCounterDropped = 1;
        public const int QueueCounterDuplicate = 2;
        public const int QueueCounterCacheHit = 3;
        public const int QueueCounterCacheMiss = 4;
        public const int QueueCounterDepthCulled = 5;
        public const int QueueCounterActiveOctaves = 6;
        public const int QueueCounterNonFinite = 7;
    }

    /// <summary>
    /// Vault buffer IDs reserved for SHINOBU_261 ocean kinematics.
    /// </summary>
    public static class OceanKinematicsBufferIds
    {
        public const BufferID Requests = (BufferID)72940;
        public const BufferID Results = (BufferID)72941;
        public const BufferID GerstnerWaves = (BufferID)72942;
        public const BufferID Tuning = (BufferID)72943;
        public const BufferID MacroState = (BufferID)72944;
        public const BufferID TelemetryRing = (BufferID)72945;
        public const BufferID TelemetryCursor = (BufferID)72946;
        public const BufferID GpuCachedResults = (BufferID)72947;
        public const BufferID CsvScratch = (BufferID)72948;
        public const BufferID QueueCounters = (BufferID)72949;
        public const BufferID RollbackFence = (BufferID)72950;
    }

    /// <summary>
    /// 16-byte water sample result consumed by downstream buoyancy solvers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.FluidSampleResultBytes)]
    public struct FluidSampleResultDTO
    {
        [FieldOffset(0)] public float WaterHeight;
        [FieldOffset(4)] public float3 SurfaceVelocity;
    }

    /// <summary>
    /// Raw AUP request row for Burst ocean sampling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.OceanSampleRequestBytes)]
    public struct OceanKinematicsSampleRequestDTO
    {
        [FieldOffset(0)] public double3 RequestedAUP;
        [FieldOffset(24)] public uint RequestHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public int ResultIndex;
        [FieldOffset(36)] public float MinSpatialLength;
    }

    /// <summary>
    /// Unmanaged Gerstner wave row used by analytical ocean sampling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.GerstnerWaveBytes)]
    public struct GerstnerWaveDTO
    {
        [FieldOffset(0)] public float2 DirectionXZ;
        [FieldOffset(8)] public float Amplitude;
        [FieldOffset(12)] public float Steepness;
        [FieldOffset(16)] public float Frequency;
        [FieldOffset(20)] public float PhaseOffset;
        [FieldOffset(24)] public float Wavelength;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint _pad0;
    }

    /// <summary>
    /// Per-frame ocean-kinematics tuning snapshot passed into Burst jobs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.TuningBytes)]
    public struct OceanKinematicsTuningDTO
    {
        [FieldOffset(0)] public double3 OceanRootAUP;
        [FieldOffset(24)] public float OceanSurfaceY;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float TimeSeconds;
        [FieldOffset(36)] public float DepthCullingThresholdMeters;
        [FieldOffset(40)] public int MaxOctaveLimit;
        [FieldOffset(44)] public float WaveAmplitudeMultiplier;
        [FieldOffset(48)] public int RequestCount;
        [FieldOffset(52)] public uint FrameIndex;
        [FieldOffset(56)] public float MaxPeakHeight;
        [FieldOffset(60)] public uint Flags;
    }

    /// <summary>
    /// O(1) ocean macro-state row for systems that do not need full sampling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.MacroStateBytes)]
    public struct OceanMacroStateDTO
    {
        [FieldOffset(0)] public float RestingWaterHeight;
        [FieldOffset(4)] public float MaxWavePeakHeight;
        [FieldOffset(8)] public float OceanSurfaceY;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public uint FrameIndex;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
    }

    /// <summary>
    /// Previous-frame complex wave result used by the Dear Lie latency path. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.CachedSampleBytes)]
    public struct OceanCachedFluidSampleDTO
    {
        [FieldOffset(0)] public uint RequestHash;
        [FieldOffset(4)] public uint FrameIndex;
        [FieldOffset(8)] public FluidSampleResultDTO Result;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public float AgeSeconds;
    }

    /// <summary>
    /// Packed queue counters written by pre-simulation orchestration. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.QueueCountersBytes)]
    public struct OceanKinematicsQueueCountersDTO
    {
        [FieldOffset(0)] public int PackedCount;
        [FieldOffset(4)] public int DroppedCount;
        [FieldOffset(8)] public int DuplicateCount;
        [FieldOffset(12)] public int CacheHitCount;
        [FieldOffset(16)] public int CacheMissCount;
        [FieldOffset(20)] public int DepthCulledCount;
        [FieldOffset(24)] public int ActiveOctaves;
        [FieldOffset(28)] public int NonFiniteCount;
    }

    /// <summary>
    /// Network/rollback fence for synchronized ocean macro state. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.RollbackFenceBytes)]
    public struct OceanKinematicsRollbackFenceDTO
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint MacroStateHash;
        [FieldOffset(8)] public uint ResultStateHash;
        [FieldOffset(12)] public int QueryCount;
        [FieldOffset(16)] public float OceanSurfaceY;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint ActiveOctaves;
    }

    /// <summary>
    /// 300-frame black-box telemetry row for ocean kinematics.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OceanKinematicsConstants.TelemetryBytes)]
    public struct OceanKinematicsTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int QueryCount;
        [FieldOffset(8)] public int DepthCulledCount;
        [FieldOffset(12)] public int ActiveOctaves;
        [FieldOffset(16)] public float BurstExecutionMicros;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public float OceanSurfaceY;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint LastRequestHash;
        [FieldOffset(36)] public float MaxWavePeakHeight;
        [FieldOffset(40)] public float3 LastSurfaceVelocity;
        [FieldOffset(52)] public uint NonFiniteCount;
        [FieldOffset(56)] public ulong _pad0;
    }

    /// <summary>
    /// Static layout audit for ocean kinematics DTOs.
    /// </summary>
    public static class OceanKinematicsLayout
    {
        private static readonly bool s_validateOnce = ValidateInternal();

        /// <summary>
        /// Returns true when all ocean kinematics DTO sizes match their fixed contracts.
        /// </summary>
        public static bool Validate()
        {
            return s_validateOnce;
        }

        /// <summary>
        /// Returns the static byte offset for a field in <see cref="FluidSampleResultDTO"/>.
        /// </summary>
        public static int OffsetOfFluidSampleResult(string fieldName)
        {
            if (fieldName == nameof(FluidSampleResultDTO.WaterHeight)) return 0;
            if (fieldName == nameof(FluidSampleResultDTO.SurfaceVelocity)) return 4;
            return -1;
        }

        public static int OffsetOfGerstnerWave(string fieldName)
        {
            if (fieldName == nameof(GerstnerWaveDTO.DirectionXZ)) return 0;
            if (fieldName == nameof(GerstnerWaveDTO.Amplitude)) return 8;
            if (fieldName == nameof(GerstnerWaveDTO.Steepness)) return 12;
            if (fieldName == nameof(GerstnerWaveDTO.Frequency)) return 16;
            if (fieldName == nameof(GerstnerWaveDTO.PhaseOffset)) return 20;
            if (fieldName == nameof(GerstnerWaveDTO.Wavelength)) return 24;
            if (fieldName == nameof(GerstnerWaveDTO.StateHash)) return 28;
            if (fieldName == nameof(GerstnerWaveDTO.Flags)) return 32;
            return -1;
        }

        private static bool ValidateInternal()
        {
            return UnsafeUtility.SizeOf<FluidSampleResultDTO>() == OceanKinematicsConstants.FluidSampleResultBytes &&
                   UnsafeUtility.AlignOf<FluidSampleResultDTO>() >= 4 &&
                   UnsafeUtility.SizeOf<OceanKinematicsSampleRequestDTO>() == OceanKinematicsConstants.OceanSampleRequestBytes &&
                   UnsafeUtility.SizeOf<GerstnerWaveDTO>() == OceanKinematicsConstants.GerstnerWaveBytes &&
                   OffsetOfGerstnerWave(nameof(GerstnerWaveDTO.StateHash)) == 28 &&
                   OffsetOfGerstnerWave(nameof(GerstnerWaveDTO.Flags)) == 32 &&
                   UnsafeUtility.SizeOf<OceanKinematicsTuningDTO>() == OceanKinematicsConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<OceanMacroStateDTO>() == OceanKinematicsConstants.MacroStateBytes &&
                   UnsafeUtility.SizeOf<OceanCachedFluidSampleDTO>() == OceanKinematicsConstants.CachedSampleBytes &&
                   UnsafeUtility.SizeOf<OceanKinematicsQueueCountersDTO>() == OceanKinematicsConstants.QueueCountersBytes &&
                   UnsafeUtility.SizeOf<OceanKinematicsRollbackFenceDTO>() == OceanKinematicsConstants.RollbackFenceBytes &&
                   UnsafeUtility.SizeOf<OceanKinematicsTelemetryEntry>() == OceanKinematicsConstants.TelemetryBytes &&
                   OffsetOfFluidSampleResult(nameof(FluidSampleResultDTO.WaterHeight)) == 0 &&
                   OffsetOfFluidSampleResult(nameof(FluidSampleResultDTO.SurfaceVelocity)) == 4;
        }
    }
}
