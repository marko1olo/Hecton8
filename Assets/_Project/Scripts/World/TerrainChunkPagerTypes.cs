using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    public static class TerrainChunkPagerConstants
    {
        public const int TelemetryCapacity = 300;
        public const int DefaultMaxChunkSlots = 256;
        public const int DefaultQueueCapacity = 64;
        public const int DefaultChunkBytes = 256 * 1024;
        public const int MaxEvaluatedRingRadius = 5;
        public const float DefaultSectorSizeMeters = 512f;
        public const uint FileMagic = 0x42433848u; // H8CB little endian.
        public const uint FileVersion = 1u;
        public const uint FileFlagsMask = 0u;
        public const uint FileCompressionRaw = 0u;
        public const uint FileCompressionLz4 = 1u;
        public const uint TelemetryFaultMissingFile = 1u << 0;
        public const uint TelemetryFaultIo = 1u << 1;
        public const uint TelemetryFaultQueueOverflow = 1u << 2;
        public const uint TelemetryFaultLz4 = 1u << 3;
        public const uint TelemetryFaultLayout = 1u << 4;
        public const uint TelemetryFaultNonFiniteAup = 1u << 5;
        public const uint TelemetryFaultVaultUnavailable = 1u << 6;
        public const uint TelemetryFaultInvalidHeader = 1u << 7;
        public const uint TelemetryFaultChecksum = 1u << 8;
        public const uint TelemetryFaultCapacityOverflow = 1u << 9;
        public const uint RequestFlagMock = 1u << 0;
        public const uint RequestFlagForceMock = 1u << 1;
        public const uint RequestFlagsMask = RequestFlagForceMock;
        public const uint ResultFlagSuccess = 1u << 0;
        public const uint ResultFlagMock = 1u << 1;
        public const uint ResultFlagMissingFile = 1u << 2;
        public const uint ResultFlagIoError = 1u << 3;
        public const uint ResultFlagLz4Error = 1u << 4;
        public const uint ResultFlagPartialRead = 1u << 5;
        public const uint ResultFlagInvalidHeader = 1u << 6;
        public const uint ResultFlagChecksumMismatch = 1u << 7;
    }

    public static class TerrainChunkStateFlags
    {
        public const uint None = 0u;
        public const uint Loading = 1u << 0;
        public const uint Active = 1u << 1;
        public const uint Stale = 1u << 2;
        public const uint ReadyToCommit = 1u << 3;
        public const uint Pinned = 1u << 4;
        public const uint MockPayload = 1u << 5;
        public const uint MissingFile = 1u << 6;
        public const uint NetcodeExcluded = 1u << 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TerrainChunkSectorCoordDTO
    {
        [FieldOffset(0)] public long X;
        [FieldOffset(8)] public long Z;

        public TerrainChunkSectorCoordDTO(long x, long z)
        {
            X = x;
            Z = z;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ChunkMetadataDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint BufferIdRef;
        [FieldOffset(12)] public uint FileOffset;
        [FieldOffset(16)] public uint StateFlags;
        [FieldOffset(20)] public float DistanceSq;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TerrainChunkFileHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint StoredBytes;
        [FieldOffset(12)] public uint UncompressedBytes;
        [FieldOffset(16)] public uint Compression;
        [FieldOffset(20)] public uint PayloadOffset;
        [FieldOffset(24)] public uint Crc32;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TerrainChunkWorkerRequestDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public long SectorX;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public int SlotIndex;
        [FieldOffset(28)] public int ChunkByteCapacity;
        [FieldOffset(32)] public uint RequestFrame;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float DistanceSq;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public uint Sequence;
        [FieldOffset(52)] public int WorkerMockDelayMinMs;
        [FieldOffset(56)] public int WorkerMockDelayMaxMs;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TerrainChunkWorkerResultDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public long SectorX;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public int SlotIndex;
        [FieldOffset(28)] public int BytesWritten;
        [FieldOffset(32)] public float LatencyMs;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint Sequence;
        [FieldOffset(44)] public uint RequestFrame;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct TerrainChunkPagerTuningDTO
    {
        [FieldOffset(0)] public float SectorSizeMeters;
        [FieldOffset(4)] public float MinRingRadius;
        [FieldOffset(8)] public float MaxRingRadius;
        [FieldOffset(12)] public float EvictionHysteresisSectors;
        [FieldOffset(16)] public float SafeLatencyMs;
        [FieldOffset(20)] public float CriticalLatencyMs;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float LatencyEwmaMs;
        [FieldOffset(32)] public float EffectiveRingRadius;
        [FieldOffset(36)] public int MaxQueuedLoads;
        [FieldOffset(40)] public int MaxCommitsPerVisualSync;
        [FieldOffset(44)] public int ChunkByteCapacity;
        [FieldOffset(48)] public int WorkerMockDelayMinMs;
        [FieldOffset(52)] public int WorkerMockDelayMaxMs;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint CsvProfileHash;
        [FieldOffset(64)] public float CommitByteBudgetPerFrame;
        [FieldOffset(68)] public uint LayoutVersion;
        [FieldOffset(72)] public uint _pad0;
        [FieldOffset(76)] public uint _pad1;

        public static TerrainChunkPagerTuningDTO CreateDefault()
        {
            TerrainChunkPagerTuningDTO tuning = default;
            tuning.SectorSizeMeters = TerrainChunkPagerConstants.DefaultSectorSizeMeters;
            tuning.MinRingRadius = 1.25f;
            tuning.MaxRingRadius = 2.65f;
            tuning.EvictionHysteresisSectors = 1.0f;
            tuning.SafeLatencyMs = 80f;
            tuning.CriticalLatencyMs = 220f;
            tuning.GlobalQualityWeight = 1f;
            tuning.LatencyEwmaMs = 80f;
            tuning.EffectiveRingRadius = 2.65f;
            tuning.MaxQueuedLoads = 8;
            tuning.MaxCommitsPerVisualSync = 2;
            tuning.ChunkByteCapacity = TerrainChunkPagerConstants.DefaultChunkBytes;
            tuning.WorkerMockDelayMinMs = 6;
            tuning.WorkerMockDelayMaxMs = 220;
            tuning.Flags = 0u;
            tuning.CommitByteBudgetPerFrame = TerrainChunkPagerConstants.DefaultChunkBytes * 2f;
            tuning.LayoutVersion = 1u;
            return TerrainChunkPagerMath.Sanitize(tuning);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TerrainChunkPagerCountersDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int ActiveChunks;
        [FieldOffset(8)] public int LoadingChunks;
        [FieldOffset(12)] public int StaleChunks;
        [FieldOffset(16)] public int PendingRequests;
        [FieldOffset(20)] public int PendingResults;
        [FieldOffset(24)] public float LatencyEwmaMs;
        [FieldOffset(28)] public float EffectiveRingRadius;
        [FieldOffset(32)] public uint LastFaultFlags;
        [FieldOffset(36)] public uint WorkerSequence;
        [FieldOffset(40)] public uint MissingFileCount;
        [FieldOffset(44)] public uint IoErrorCount;
        [FieldOffset(48)] public uint Lz4ErrorCount;
        [FieldOffset(52)] public uint QueueOverflowCount;
        [FieldOffset(56)] public uint LayoutValid;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PagerTelemetryEntry
    {
        [FieldOffset(0)] public double3 CameraAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public ushort ActiveChunks;
        [FieldOffset(34)] public ushort LoadingChunks;
        [FieldOffset(36)] public ushort StaleChunks;
        [FieldOffset(38)] public ushort PendingLoads;
        [FieldOffset(40)] public float LatencyEwmaMs;
        [FieldOffset(44)] public uint ResidencyEvalMicros;
        [FieldOffset(48)] public float EffectiveRingRadius;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint MissingFileCount;
        [FieldOffset(60)] public uint WorkerSequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StreamingHardwareProfileDTO
    {
        [FieldOffset(0)] public uint TargetHash;
        [FieldOffset(4)] public int MaxQueuedLoads;
        [FieldOffset(8)] public int ChunkByteCapacity;
        [FieldOffset(12)] public float MinRingRadius;
        [FieldOffset(16)] public float MaxRingRadius;
        [FieldOffset(20)] public float SafeLatencyMs;
        [FieldOffset(24)] public float CriticalLatencyMs;
        [FieldOffset(28)] public uint Flags;
    }

    public static class ChunkMetadataLayoutGuard
    {
        public const int SizeBytes = 32;
        public const int SectorHashOffset = 0;
        public const int BufferIdRefOffset = 8;
        public const int FileOffsetOffset = 12;
        public const int StateFlagsOffset = 16;
        public const int DistanceSqOffset = 20;
        public const int Pad0Offset = 24;
        public const int Pad7Offset = 31;

        public static bool ValidateLayout()
        {
            bool constantsValid =
                SectorHashOffset == 0 &&
                BufferIdRefOffset == 8 &&
                FileOffsetOffset == 12 &&
                StateFlagsOffset == 16 &&
                DistanceSqOffset == 20 &&
                Pad0Offset == 24 &&
                Pad7Offset == 31;
#if UNITY_EDITOR
            return constantsValid && ValidateEditorOffsets();
#else
            return constantsValid && UnsafeUtility.SizeOf<ChunkMetadataDTO>() == SizeBytes;
#endif
        }

#if UNITY_EDITOR
        public static bool ValidateEditorOffsets()
        {
            return UnsafeUtility.SizeOf<ChunkMetadataDTO>() == SizeBytes &&
                   GetOffset(nameof(ChunkMetadataDTO.SectorHash)) == SectorHashOffset &&
                   GetOffset(nameof(ChunkMetadataDTO.BufferIdRef)) == BufferIdRefOffset &&
                   GetOffset(nameof(ChunkMetadataDTO.FileOffset)) == FileOffsetOffset &&
                   GetOffset(nameof(ChunkMetadataDTO.StateFlags)) == StateFlagsOffset &&
                   GetOffset(nameof(ChunkMetadataDTO.DistanceSq)) == DistanceSqOffset &&
                   GetOffset(nameof(ChunkMetadataDTO._pad0)) == Pad0Offset &&
                   GetOffset(nameof(ChunkMetadataDTO._pad7)) == Pad7Offset;
        }

        private static int GetOffset(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(ChunkMetadataDTO).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif
    }

    public static class TerrainChunkPagerMath
    {
        private const ulong FnvaOffset64 = 14695981039346656037UL;
        private const ulong FnvaPrime64 = 1099511628211UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TerrainChunkPagerTuningDTO Sanitize(TerrainChunkPagerTuningDTO tuning)
        {
            tuning.SectorSizeMeters = math.max(1f, FiniteOr(tuning.SectorSizeMeters, TerrainChunkPagerConstants.DefaultSectorSizeMeters));
            tuning.MinRingRadius = math.clamp(FiniteOr(tuning.MinRingRadius, 1f), 1f, TerrainChunkPagerConstants.MaxEvaluatedRingRadius);
            tuning.MaxRingRadius = math.clamp(FiniteOr(tuning.MaxRingRadius, tuning.MinRingRadius), tuning.MinRingRadius, TerrainChunkPagerConstants.MaxEvaluatedRingRadius);
            tuning.EvictionHysteresisSectors = math.max(0.5f, FiniteOr(tuning.EvictionHysteresisSectors, 1f));
            tuning.SafeLatencyMs = math.max(1f, FiniteOr(tuning.SafeLatencyMs, 80f));
            tuning.CriticalLatencyMs = math.max(tuning.SafeLatencyMs + 1f, FiniteOr(tuning.CriticalLatencyMs, 220f));
            tuning.GlobalQualityWeight = math.saturate(FiniteOr(tuning.GlobalQualityWeight, 1f));
            tuning.LatencyEwmaMs = math.max(0f, FiniteOr(tuning.LatencyEwmaMs, tuning.SafeLatencyMs));
            tuning.MaxQueuedLoads = math.clamp(tuning.MaxQueuedLoads, 1, 256);
            tuning.MaxCommitsPerVisualSync = math.clamp(tuning.MaxCommitsPerVisualSync, 1, 16);
            tuning.ChunkByteCapacity = math.max(4096, tuning.ChunkByteCapacity);
            tuning.WorkerMockDelayMinMs = math.clamp(tuning.WorkerMockDelayMinMs, 0, 10000);
            tuning.WorkerMockDelayMaxMs = math.max(tuning.WorkerMockDelayMinMs, math.clamp(tuning.WorkerMockDelayMaxMs, 0, 30000));
            tuning.Flags &= TerrainChunkPagerConstants.RequestFlagsMask;
            // R97 FIX (commit livelock): floor was 4096 while ChunkByteCapacity can be far larger —
            // a tuned budget in [4096, chunkBytes) made every chunk fail `bytes > byteBudget` in
            // VisualSyncTick forever: never committed, never Stale, never evicted, slot leaked until
            // total pager deadlock. The budget must always admit at least one full chunk.
            tuning.CommitByteBudgetPerFrame = math.max((float)tuning.ChunkByteCapacity, FiniteOr(tuning.CommitByteBudgetPerFrame, tuning.ChunkByteCapacity));
            tuning.EffectiveRingRadius = ResolveContinuousRingRadius(tuning.GlobalQualityWeight, tuning.LatencyEwmaMs, in tuning);
            tuning._pad0 = 0u;
            tuning._pad1 = 0u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ResolveSectorCoord(double absoluteMeters, float sectorSizeMeters)
        {
            double safeSize = math.max(1.0d, (double)sectorSizeMeters);
            double scaled = absoluteMeters / safeSize;
            if (!math.isfinite(scaled))
                return 0L;

            double floored = math.floor(scaled);
            if (floored >= long.MaxValue)
                return long.MaxValue;
            if (floored <= long.MinValue)
                return long.MinValue;
            return (long)floored;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeSectorHash(long sectorX, long sectorZ)
        {
            ulong hash = FnvaOffset64;
            hash = MixLong(hash, sectorX);
            hash = MixLong(hash, sectorZ);
            return hash == 0UL ? 1UL : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveContinuousRingRadius(float qualityWeight, float latencyMs, in TerrainChunkPagerTuningDTO tuning)
        {
            float q = Smooth01(math.saturate(FiniteOr(qualityWeight, 1f)));
            float qualityRadius = math.lerp(tuning.MinRingRadius, tuning.MaxRingRadius, q);
            float latencyDenom = math.max(1f, tuning.CriticalLatencyMs - tuning.SafeLatencyMs);
            float latencyDebt01 = Smooth01(math.saturate((FiniteOr(latencyMs, tuning.SafeLatencyMs) - tuning.SafeLatencyMs) / latencyDenom));
            return math.clamp(math.lerp(qualityRadius, tuning.MinRingRadius, latencyDebt01), tuning.MinRingRadius, tuning.MaxRingRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveDiscreteRingRadius(float effectiveRingRadius)
        {
            return math.clamp((int)math.round(FiniteOr(effectiveRingRadius, 1f)), 1, TerrainChunkPagerConstants.MaxEvaluatedRingRadius);
        }

        /// <summary>
        /// Cull radius, in sectors, that provably encloses everything load admission can admit.
        /// </summary>
        /// <remarks>
        /// R100 FIX (stationary-camera eviction thrash): load admission is a Chebyshev square ring
        /// over sector indices, but eviction is a Euclidean squared-distance test. The binding
        /// constraint is therefore the ring CORNER at radius * sqrt(2), not the axis distance. The
        /// previous form added a fixed `+ 1.0f` margin, which cannot hold the invariant in general
        /// because the corner overhang grows as 0.4142 * radius: at hysteresis 0.5 - the Sanitize
        /// floor, and also the tuner slider minimum - with the shipped MaxRingRadius of 2.65, the
        /// corner sat at 4.243 sectors while the cull radius was only 4.15, so the four corner
        /// sectors of the load ring were freed and immediately re-requested forever with a
        /// stationary camera, costing a full chunk file read plus slab copy per corner per cycle.
        /// Taking the max against the raw ring radius also keeps this at or beyond the soft stale
        /// threshold (EffectiveRingRadius + EvictionHysteresisSectors), so the two bands cannot invert.
        /// Retention footprint scales with the square of this value. At shipped defaults the admitted
        /// ring is 49 sectors against DefaultMaxChunkSlots (256), so there is ample headroom. A tuned
        /// MaxRingRadius of 5 with large hysteresis can still out-run slot capacity - as it could
        /// before this change - and remains bounded by FindFreeSlot yielding no slot and the sector
        /// retrying on a later tick.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCullRadiusSectors(float effectiveRingRadius, float evictionHysteresisSectors)
        {
            const float Sqrt2 = 1.41421356f;
            float safeRing = math.max(1f, FiniteOr(effectiveRingRadius, 1f));
            float hysteresis = math.max(0f, FiniteOr(evictionHysteresisSectors, 1f));
            float admittedCornerSectors = ResolveDiscreteRingRadius(effectiveRingRadius) * Sqrt2;
            return math.max(admittedCornerSectors, safeRing) + hysteresis;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint HashMetadata(ChunkMetadataDTO* metadata, int count)
        {
            ulong hash = FnvaOffset64;
            for (int i = 0; i < count; i++)
            {
                ChunkMetadataDTO* entry = metadata + i;
                hash ^= entry->SectorHash;
                hash *= FnvaPrime64;
                hash ^= entry->StateFlags;
                hash *= FnvaPrime64;
            }

            uint folded = unchecked((uint)(hash ^ (hash >> 32)));
            return folded == 0u ? 1u : folded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashMetadata(NativeArray<ChunkMetadataDTO> metadata, int count)
        {
            ulong hash = FnvaOffset64;
            int safeCount = math.min(count, metadata.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChunkMetadataDTO entry = metadata[i];
                hash ^= entry.SectorHash;
                hash *= FnvaPrime64;
                hash ^= entry.StateFlags;
                hash *= FnvaPrime64;
            }

            uint folded = unchecked((uint)(hash ^ (hash >> 32)));
            return folded == 0u ? 1u : folded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixLong(ulong hash, long value)
        {
            ulong v = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
                hash = (hash ^ ((v >> shift) & 0xFFUL)) * FnvaPrime64;
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateChunkResidencyJob : IJob
    {
        [NoAlias] public NativeArray<ChunkMetadataDTO> Metadata;
        [ReadOnly, NoAlias] public NativeArray<TerrainChunkSectorCoordDTO> SectorCoords;
        public int MetadataCapacity;
        [NoAlias] public NativeArray<TerrainChunkWorkerRequestDTO> LoadRequests;
        [NoAlias] public NativeArray<int> LoadRequestCount;
        [NoAlias] public NativeArray<int> StaleSlots;
        [NoAlias] public NativeArray<int> StaleSlotCount;
        public double3 CameraAup;
        public TerrainChunkPagerTuningDTO Tuning;
        public uint Frame;
        public uint SequenceBase;

        public void Execute()
        {
            if (!Metadata.IsCreated || !SectorCoords.IsCreated || MetadataCapacity <= 0)
                return;

            int loadCount = 0;
            int staleCount = 0;
            int metadataCapacity = math.min(MetadataCapacity, math.min(Metadata.Length, SectorCoords.Length));
            int ringRadius = TerrainChunkPagerMath.ResolveDiscreteRingRadius(Tuning.EffectiveRingRadius);
            float sectorSize = math.max(1f, Tuning.SectorSizeMeters);
            long cameraSectorX = TerrainChunkPagerMath.ResolveSectorCoord(CameraAup.x, sectorSize);
            long cameraSectorZ = TerrainChunkPagerMath.ResolveSectorCoord(CameraAup.z, sectorSize);

            for (int slot = 0; slot < metadataCapacity; slot++)
            {
                ChunkMetadataDTO meta = Metadata[slot];
                if (meta.SectorHash == 0UL)
                    continue;

                TerrainChunkSectorCoordDTO coord = SectorCoords[slot];
                double dx = ((double)coord.X - (double)cameraSectorX) * sectorSize;
                double dz = ((double)coord.Z - (double)cameraSectorZ) * sectorSize;
                double distSqD = (dx * dx) + (dz * dz);
                meta.DistanceSq = distSqD >= float.MaxValue || !math.isfinite(distSqD) ? float.MaxValue : (float)distSqD;
                bool active = (meta.StateFlags & TerrainChunkStateFlags.Active) != 0u;
                bool loading = (meta.StateFlags & TerrainChunkStateFlags.Loading) != 0u;
                bool pinned = (meta.StateFlags & TerrainChunkStateFlags.Pinned) != 0u;
                bool stale = (meta.StateFlags & TerrainChunkStateFlags.Stale) != 0u;
                float evictSectors = Tuning.EffectiveRingRadius + Tuning.EvictionHysteresisSectors;
                double evictMeters = (double)math.max(1f, evictSectors) * sectorSize;
                if (active && !loading && !pinned && !stale && distSqD > evictMeters * evictMeters)
                {
                    meta.StateFlags = (meta.StateFlags | TerrainChunkStateFlags.Stale) & ~TerrainChunkStateFlags.ReadyToCommit;
                    if (staleCount < StaleSlots.Length)
                        StaleSlots[staleCount++] = slot;
                }
                else if (stale && distSqD <= evictMeters * evictMeters)
                {
                    meta.StateFlags &= ~TerrainChunkStateFlags.Stale;
                }

                Metadata[slot] = meta;
            }

            for (int z = -ringRadius; z <= ringRadius; z++)
            {
                for (int x = -ringRadius; x <= ringRadius; x++)
                {
                    long sectorX = AddSmallSectorOffset(cameraSectorX, x);
                    long sectorZ = AddSmallSectorOffset(cameraSectorZ, z);
                    ulong hash = TerrainChunkPagerMath.ComputeSectorHash(sectorX, sectorZ);
                    if (FindSlotByHash(Metadata, hash, metadataCapacity) >= 0)
                        continue;

                    if (loadCount >= LoadRequests.Length)
                        continue;

                    float dx = (float)x * sectorSize;
                    float dz = (float)z * sectorSize;
                    TerrainChunkWorkerRequestDTO request = default;
                    request.SectorHash = hash;
                    request.SectorX = sectorX;
                    request.SectorZ = sectorZ;
                    request.SlotIndex = -1;
                    request.ChunkByteCapacity = Tuning.ChunkByteCapacity;
                    request.RequestFrame = Frame;
                    request.Flags = Tuning.Flags;
                    request.DistanceSq = (dx * dx) + (dz * dz);
                    request.GlobalQualityWeight = Tuning.GlobalQualityWeight;
                    request.Sequence = SequenceBase + (uint)loadCount;
                    request.WorkerMockDelayMinMs = Tuning.WorkerMockDelayMinMs;
                    request.WorkerMockDelayMaxMs = Tuning.WorkerMockDelayMaxMs;
                    LoadRequests[loadCount++] = request;
                }
            }

            if (LoadRequestCount.IsCreated && LoadRequestCount.Length > 0)
                LoadRequestCount[0] = loadCount;
            if (StaleSlotCount.IsCreated && StaleSlotCount.Length > 0)
                StaleSlotCount[0] = staleCount;
        }

        private static int FindSlotByHash(NativeArray<ChunkMetadataDTO> metadata, ulong hash, int count)
        {
            for (int i = 0; i < count; i++)
            {
                ChunkMetadataDTO meta = metadata[i];
                // R97 FIX: MissingFile added to the occupancy mask. Without it, the residency job
                // re-emitted a load request for every failed sector EVERY evaluation, which the
                // dispatch side (whose mask does include MissingFile) rejected every frame —
                // steady-state busywork per failed sector. Failed slots are resident until the
                // retry/backoff lane (TerrainChunkPagerRuntime.VisualSyncTick) releases them.
                if (meta.SectorHash == hash &&
                    (meta.StateFlags & (TerrainChunkStateFlags.Active | TerrainChunkStateFlags.Loading | TerrainChunkStateFlags.ReadyToCommit | TerrainChunkStateFlags.MissingFile)) != 0u)
                {
                    return i;
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long AddSmallSectorOffset(long sector, int offset)
        {
            if (offset > 0 && sector > long.MaxValue - offset)
                return long.MaxValue;
            if (offset < 0 && sector < long.MinValue - offset)
                return long.MinValue;
            return sector + offset;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvictStaleChunksJob : IJob
    {
        [NoAlias] public NativeArray<ChunkMetadataDTO> Metadata;
        [NoAlias] public NativeArray<TerrainChunkSectorCoordDTO> SectorCoords;
        public int MetadataCapacity;
        [NoAlias] public NativeArray<int> FreedSlots;
        [NoAlias] public NativeArray<int> FreedSlotCount;
        public long CameraSectorX;
        public long CameraSectorZ;
        public float SectorSizeMeters;
        public float CullRadiusSectors;

        public void Execute()
        {
            if (!Metadata.IsCreated || !SectorCoords.IsCreated || MetadataCapacity <= 0)
                return;

            int metadataCapacity = math.min(MetadataCapacity, math.min(Metadata.Length, SectorCoords.Length));
            double cullMeters = (double)math.max(1f, CullRadiusSectors) * math.max(1f, SectorSizeMeters);
            double cullSq = cullMeters * cullMeters;
            int freeCount = 0;
            for (int i = 0; i < metadataCapacity; i++)
            {
                ChunkMetadataDTO meta = Metadata[i];
                if (meta.SectorHash == 0UL ||
                    (meta.StateFlags & TerrainChunkStateFlags.Stale) == 0u ||
                    (meta.StateFlags & TerrainChunkStateFlags.Loading) != 0u ||
                    (meta.StateFlags & TerrainChunkStateFlags.Pinned) != 0u)
                {
                    continue;
                }

                TerrainChunkSectorCoordDTO coord = SectorCoords[i];
                double dx = ((double)coord.X - (double)CameraSectorX) * SectorSizeMeters;
                double dz = ((double)coord.Z - (double)CameraSectorZ) * SectorSizeMeters;
                if ((dx * dx) + (dz * dz) <= cullSq)
                    continue;

                Metadata[i] = default;
                SectorCoords[i] = default;
                if (freeCount < FreedSlots.Length)
                    FreedSlots[freeCount++] = i;
            }

            if (FreedSlotCount.IsCreated && FreedSlotCount.Length > 0)
                FreedSlotCount[0] = freeCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CommitStagedChunkJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> Source;
        [NoAlias] public NativeArray<byte> Destination;
        public int ByteCount;

        public void Execute()
        {
            if (!Source.IsCreated || !Destination.IsCreated || ByteCount <= 0)
                return;

            int byteCount = math.min(ByteCount, math.min(Source.Length, Destination.Length));
            for (int i = 0; i < byteCount; i++)
                Destination[i] = Source[i];
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockDiskLoadJob : IJob
    {
        [NoAlias] public NativeArray<byte> Destination;
        public int ByteCount;
        public ulong SectorHash;
        public uint Sequence;

        public void Execute()
        {
            Fill(Destination, ByteCount, SectorHash, Sequence);
        }

        public static void Fill(NativeArray<byte> destination, int byteCount, ulong sectorHash, uint sequence)
        {
            if (!destination.IsCreated || byteCount <= 0)
                return;

            int safeByteCount = math.min(byteCount, destination.Length);
            uint seed = unchecked((uint)(sectorHash ^ (sectorHash >> 32)) ^ sequence ^ 0xA341316Cu);
            for (int i = 0; i < safeByteCount; i++)
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                destination[i] = (byte)(seed >> 24);
            }
        }

        public static unsafe void Fill(byte* destination, int byteCount, ulong sectorHash, uint sequence)
        {
            if (destination == null || byteCount <= 0)
                return;

            uint seed = unchecked((uint)(sectorHash ^ (sectorHash >> 32)) ^ sequence ^ 0xA341316Cu);
            for (int i = 0; i < byteCount; i++)
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                destination[i] = (byte)(seed >> 24);
            }
        }
    }

    public static unsafe class TerrainChunkLz4Codec
    {
        public static bool TryDecompress(byte* source, int sourceLength, byte* destination, int destinationCapacity, out int bytesWritten)
        {
            bytesWritten = 0;
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 0)
                return false;

            byte* ip = source;
            byte* iend = source + sourceLength;
            byte* op = destination;
            byte* oend = destination + destinationCapacity;

            while (ip < iend)
            {
                uint token = *ip++;
                int literalLength = (int)(token >> 4);
                long literalOutput = oend - op;
                int maxLiteralLength = literalOutput > int.MaxValue ? int.MaxValue : (int)literalOutput;
                if (!ReadLength(ref ip, iend, ref literalLength, maxLiteralLength))
                    return false;

                if (literalLength > iend - ip || literalLength > oend - op)
                    return false;

                UnsafeUtility.MemCpy(op, ip, literalLength);
                ip += literalLength;
                op += literalLength;

                if (ip >= iend)
                {
                    bytesWritten = (int)(op - destination);
                    return true;
                }

                if (iend - ip < 2)
                    return false;

                int offset = ip[0] | (ip[1] << 8);
                ip += 2;
                if (offset <= 0 || offset > op - destination)
                    return false;

                int matchLength = (int)(token & 0x0F);
                long remainingOutput = oend - op;
                int maxMatchBase = remainingOutput <= 4L
                    ? 0
                    : (remainingOutput - 4L > int.MaxValue ? int.MaxValue : (int)(remainingOutput - 4L));
                if (!ReadLength(ref ip, iend, ref matchLength, maxMatchBase))
                    return false;
                matchLength += 4;

                if (matchLength > oend - op)
                    return false;

                byte* match = op - offset;
                for (int i = 0; i < matchLength; i++)
                    op[i] = match[i];
                op += matchLength;
            }

            bytesWritten = (int)(op - destination);
            return true;
        }

        private static bool ReadLength(ref byte* input, byte* end, ref int length, int maxLength)
        {
            if (length < 0 || length > maxLength)
                return false;

            if (length != 15)
                return true;

            long total = length;
            while (input < end)
            {
                int value = *input++;
                total += value;
                if (total > maxLength || total > int.MaxValue)
                    return false;
                if (value != 255)
                {
                    length = (int)total;
                    return true;
                }
            }

            return false;
        }
    }

    #if UNITY_EDITOR
    public static class TerrainChunkStreamingProfileCsvParser
    {
        public static bool TryParse(ReadOnlySpan<byte> csv, ref TerrainChunkPagerTuningDTO tuning, NativeArray<StreamingHardwareProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            bool changed = false;
            int cursor = 0;
            while (cursor < csv.Length)
            {
                ReadOnlySpan<byte> row = NextLine(csv, ref cursor);
                row = Trim(row);
                if (row.Length == 0 || row[0] == (byte)'#')
                    continue;

                StreamingHardwareProfileDTO profile = default;
                if (!TryParseRow(row, out profile))
                    continue;

                if (profiles.IsCreated && profileCount < profiles.Length)
                    profiles[profileCount] = profile;
                profileCount++;

                if (profileCount == 1)
                {
                    tuning.MaxQueuedLoads = profile.MaxQueuedLoads;
                    tuning.ChunkByteCapacity = profile.ChunkByteCapacity;
                    tuning.MinRingRadius = profile.MinRingRadius;
                    tuning.MaxRingRadius = profile.MaxRingRadius;
                    tuning.SafeLatencyMs = profile.SafeLatencyMs;
                    tuning.CriticalLatencyMs = profile.CriticalLatencyMs;
                    tuning.CsvProfileHash = profile.TargetHash;
                    changed = true;
                }
            }

            if (changed)
                tuning = TerrainChunkPagerMath.Sanitize(tuning);

            return changed;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> row, out StreamingHardwareProfileDTO profile)
        {
            profile = default;
            int next = 0;
            ReadOnlySpan<byte> target = NextToken(row, next, out next);
            if (target.Length == 0 || EqualsAscii(target, "target"))
                return false;

            profile.TargetHash = HashFnv1A32(target);
            if (!TryParseInt(NextToken(row, next, out next), out profile.MaxQueuedLoads))
                return false;
            if (!TryParseInt(NextToken(row, next, out next), out profile.ChunkByteCapacity))
                return false;
            if (!TryParseFloat(NextToken(row, next, out next), out profile.MinRingRadius))
                return false;
            if (!TryParseFloat(NextToken(row, next, out next), out profile.MaxRingRadius))
                return false;
            if (!TryParseFloat(NextToken(row, next, out next), out profile.SafeLatencyMs))
                return false;
            if (!TryParseFloat(NextToken(row, next, out next), out profile.CriticalLatencyMs))
                return false;
            profile.Flags = 1u;
            return true;
        }

        private static ReadOnlySpan<byte> NextLine(ReadOnlySpan<byte> csv, ref int cursor)
        {
            int start = cursor;
            while (cursor < csv.Length && csv[cursor] != (byte)'\n' && csv[cursor] != (byte)'\r')
                cursor++;

            int end = cursor;
            while (cursor < csv.Length && (csv[cursor] == (byte)'\n' || csv[cursor] == (byte)'\r'))
                cursor++;

            return csv.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> NextToken(ReadOnlySpan<byte> row, int start, out int next)
        {
            int begin = math.clamp(start, 0, row.Length);
            while (begin < row.Length && IsWhitespace(row[begin]))
                begin++;

            int end = begin;
            while (end < row.Length && row[end] != (byte)',' && row[end] != (byte)'=' && row[end] != (byte)':')
                end++;

            int trimmedEnd = end;
            while (trimmedEnd > begin && IsWhitespace(row[trimmedEnd - 1]))
                trimmedEnd--;

            next = end < row.Length ? end + 1 : row.Length;
            return row.Slice(begin, trimmedEnd - begin);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool TryParseInt(ReadOnlySpan<byte> bytes, out int value)
        {
            value = 0;
            if (bytes.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (bytes[0] == (byte)'-')
            {
                negative = true;
                index = 1;
            }

            if (index >= bytes.Length)
                return false;

            long result = 0L;
            for (; index < bytes.Length; index++)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                result = (result * 10) + (c - (byte)'0');
                long signed = negative ? -result : result;
                if (signed < int.MinValue || signed > int.MaxValue)
                    return false;
            }

            value = (int)(negative ? -result : result);
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            if (bytes.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (bytes[index] == (byte)'-' || bytes[index] == (byte)'+')
            {
                negative = bytes[index] == (byte)'-';
                index++;
            }

            double result = 0d;
            bool hasDigit = false;
            for (; index < bytes.Length; index++)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                hasDigit = true;
                result = (result * 10d) + (c - (byte)'0');
            }

            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                for (; index < bytes.Length; index++)
                {
                    byte c = bytes[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        return false;
                    hasDigit = true;
                    result += (c - (byte)'0') * scale;
                    scale *= 0.1d;
                }
            }
            else if (index != bytes.Length)
                return false;

            if (!hasDigit)
                return false;

            if (negative)
                result = -result;

            if (!math.isfinite(result) || result > float.MaxValue || result < -float.MaxValue)
                return false;

            value = (float)result;
            return math.isfinite(value);
        }

        private static uint HashFnv1A32(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = unchecked((hash ^ c) * 16777619u);
            }

            return hash == 0u ? 1u : hash;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> bytes, string ascii)
        {
            if (bytes.Length != ascii.Length)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte a = bytes[i];
                byte b = (byte)ascii[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }

        private static bool IsWhitespace(byte c)
        {
            return c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r';
        }
    }
    #endif
}
