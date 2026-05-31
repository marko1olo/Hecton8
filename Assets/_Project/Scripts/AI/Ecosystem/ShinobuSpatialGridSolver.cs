using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Ecosystem
{
    public static class ShinobuSpatialGridConstants
    {
        public const int EntrySizeBytes = 16;
        public const int BucketRangeCapacity = 131072;
        public const int BucketRangeMask = BucketRangeCapacity - 1;
        public const int StructuralBucketProbeCount = 128;
        public const int TelemetryCapacity = 300;
        public const int ProfileCapacity = 16;
        public const int CsvMaxBytes = 8192;
        public const int DefaultMaxQueryResults = 48;
        public const int CounterSpatialGridOverflow = 7;
        public const float TelemetryTimingUnavailableMicroseconds = -1f;
        public const uint TelemetryFlagOverflow = 1u;
        public const uint TelemetryFlagTimingUnavailable = 2u;
        public const uint TelemetryFlagQueryCountPatched = 4u;
        public const uint TelemetryFlagInvalidInput = 8u;
        public const uint TelemetryFlagQueryResolveFailed = 16u;
        public const uint InvalidEntityRowIndex = 0xFFFFFFFFu;
        public const uint DefaultHashMultiplierX = 73856093u;
        public const uint DefaultHashMultiplierY = 19349663u;
        public const uint DefaultHashMultiplierZ = 83492791u;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_301.bin";
        public const string Agent1301DumpRelativePath = "Docs/AgentLogs/Dump_1301_AIEcology.bin";
        public const string Agent1419DumpRelativePath = "Docs/AgentLogs/Dump_1419_EcosystemSpatialGrid.bin";
        public const string ProfileCsvRelativePath = "spatial_grid_profiles.csv";
        public const string ProfileCsvPrecomputedRelativePath = "Data/Precomputed/spatial_grid_profiles.csv";
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SpatialGridEntryDTO
    {
        [FieldOffset(0)] public uint EntityHashID;
        [FieldOffset(0)] public uint EntityRowIndex;
        [FieldOffset(4)] public uint CellHash;
        [FieldOffset(8)] public float2 LocalCellOffset;
        [FieldOffset(8)] public uint2 CellFingerprint;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpatialGridBucketRangeDTO
    {
        [FieldOffset(0)] public uint CellHash;
        [FieldOffset(4)] public uint CellFingerprintX;
        [FieldOffset(8)] public uint CellFingerprintY;
        [FieldOffset(12)] public int StartIndex;
        [FieldOffset(16)] public int Count;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Pad0;
        [FieldOffset(28)] public uint Pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SpatialGridTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int EntityCount;
        [FieldOffset(8)] public int MaxBucketOccupancy;
        [FieldOffset(12)] public int QueryCount;
        [FieldOffset(16)] public float QuantizeMicroseconds;
        [FieldOffset(20)] public float SortMicroseconds;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float CellSizeMeters;
        [FieldOffset(32)] public uint OverflowCount;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public int MaxProbeCount;
        [FieldOffset(48)] public int MaxQueryResults;
        [FieldOffset(52)] public int BucketRangeCount;
        [FieldOffset(56)] public int InvalidInputCount;
        [FieldOffset(60)] public uint Pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpatialGridTuningDTO
    {
        [FieldOffset(0)] public float BaseGridCellSize;
        [FieldOffset(4)] public float MinGridCellSize;
        [FieldOffset(8)] public float MaxGridCellSize;
        [FieldOffset(12)] public int MaxQueryResultsLimit;
        [FieldOffset(16)] public uint HashMultiplierX;
        [FieldOffset(20)] public uint HashMultiplierY;
        [FieldOffset(24)] public uint HashMultiplierZ;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpatialGridProfileDTO
    {
        [FieldOffset(0)] public uint LayerHash;
        [FieldOffset(4)] public float BaseGridCellSize;
        [FieldOffset(8)] public float MinGridCellSize;
        [FieldOffset(12)] public float MaxGridCellSize;
        [FieldOffset(16)] public int MaxQueryResultsLimit;
        [FieldOffset(20)] public int MaxProbeCount;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct SpatialGridCell64
    {
        [FieldOffset(0)] public long X;
        [FieldOffset(8)] public long Y;
        [FieldOffset(16)] public long Z;
    }

    public static class ShinobuSpatialGridMath
    {
        private const double QuantizationEpsilon = 0.00000000025d;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpatialGridTuningDTO CreateDefaultTuning()
        {
            return new SpatialGridTuningDTO
            {
                BaseGridCellSize = 10f,
                MinGridCellSize = 5f,
                MaxGridCellSize = 32f,
                MaxQueryResultsLimit = ShinobuSpatialGridConstants.DefaultMaxQueryResults,
                HashMultiplierX = ShinobuSpatialGridConstants.DefaultHashMultiplierX,
                HashMultiplierY = ShinobuSpatialGridConstants.DefaultHashMultiplierY,
                HashMultiplierZ = ShinobuSpatialGridConstants.DefaultHashMultiplierZ,
                Flags = 1u
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpatialGridTuningDTO Sanitize(SpatialGridTuningDTO tuning)
        {
            SpatialGridTuningDTO fallback = CreateDefaultTuning();
            tuning.BaseGridCellSize = SanitizePositive(tuning.BaseGridCellSize, fallback.BaseGridCellSize);
            tuning.MinGridCellSize = SanitizePositive(tuning.MinGridCellSize, fallback.MinGridCellSize);
            tuning.MaxGridCellSize = SanitizePositive(tuning.MaxGridCellSize, fallback.MaxGridCellSize);
            if (tuning.MaxGridCellSize < tuning.MinGridCellSize)
                tuning.MaxGridCellSize = tuning.MinGridCellSize;
            tuning.BaseGridCellSize = math.clamp(tuning.BaseGridCellSize, tuning.MinGridCellSize, tuning.MaxGridCellSize);
            tuning.MaxQueryResultsLimit = math.clamp(tuning.MaxQueryResultsLimit <= 0 ? fallback.MaxQueryResultsLimit : tuning.MaxQueryResultsLimit, 1, 256);
            tuning.HashMultiplierX = tuning.HashMultiplierX != 0u ? tuning.HashMultiplierX : fallback.HashMultiplierX;
            tuning.HashMultiplierY = tuning.HashMultiplierY != 0u ? tuning.HashMultiplierY : fallback.HashMultiplierY;
            tuning.HashMultiplierZ = tuning.HashMultiplierZ != 0u ? tuning.HashMultiplierZ : fallback.HashMultiplierZ;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCellSizeMeters(in SpatialGridTuningDTO tuning, float globalQualityWeight, float systemStress01)
        {
            SpatialGridTuningDTO safe = Sanitize(tuning);
            float q = ShinobuEcosystemBalancer.Smooth01(math.saturate(globalQualityWeight));
            float stress = ShinobuEcosystemBalancer.Smooth01(math.saturate(systemStress01));
            float qualityScale = math.lerp(1.85f, 0.72f, q);
            float stressScale = math.lerp(1f, 1.55f, stress);
            return math.clamp(safe.BaseGridCellSize * qualityScale * stressScale, safe.MinGridCellSize, safe.MaxGridCellSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveMaxQueryResults(int maxResultsLimit, float globalQualityWeight)
        {
            int safeLimit = math.clamp(maxResultsLimit <= 0 ? ShinobuSpatialGridConstants.DefaultMaxQueryResults : maxResultsLimit, 1, 256);
            float q = ShinobuEcosystemBalancer.Smooth01(math.saturate(globalQualityWeight));
            return math.clamp((int)math.round(math.lerp(6f, safeLimit, q)), 1, safeLimit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveProbeCount(float globalQualityWeight)
        {
            float q = ShinobuEcosystemBalancer.Smooth01(math.saturate(globalQualityWeight));
            return math.clamp((int)math.round(math.lerp(2f, 24f, q)), 1, 64);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveStructuralProbeCount(int bucketRangeLength)
        {
            return math.clamp(ShinobuSpatialGridConstants.StructuralBucketProbeCount, 1, math.max(1, bucketRangeLength));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveAdjacentCellRadius(float neighborRadiusMeters, float cellSizeMeters, float globalQualityWeight)
        {
            int geometricRadius = math.max(1, (int)math.ceil(math.max(0.001f, neighborRadiusMeters) / math.max(0.25f, cellSizeMeters)));
            float q = ShinobuEcosystemBalancer.Smooth01(math.saturate(globalQualityWeight));
            int qualityRadius = math.clamp((int)math.round(math.lerp(1f, 3f, q)), 1, 3);
            return math.clamp(geometricRadius, 1, qualityRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolvePublicQueryCellRadius(float radiusMeters, float cellSizeMeters, int maxVisitedCells)
        {
            int geometricRadius = math.max(1, (int)math.ceil(math.max(0.001f, radiusMeters) / math.max(0.25f, cellSizeMeters)));
            int safeVisited = math.clamp(maxVisitedCells <= 0 ? 27 : maxVisitedCells, 1, 343);
            int budgetRadius = 1 + math.select(0, 1, safeVisited >= 125) + math.select(0, 1, safeVisited >= 343);
            return math.clamp(geometricRadius, 1, budgetRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpatialGridCell64 QuantizeCell(double3 absoluteAup, double cellSizeMeters)
        {
            double inv = 1.0d / math.max(0.0001d, cellSizeMeters);
            return new SpatialGridCell64
            {
                X = QuantizeAxis(absoluteAup.x, inv),
                Y = QuantizeAxis(absoluteAup.y, inv),
                Z = QuantizeAxis(absoluteAup.z, inv)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashCell(in SpatialGridCell64 cell, uint mx, uint my, uint mz)
        {
            return HashCellFromFingerprint(FingerprintCell(in cell, mx, my, mz));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 FingerprintCell(in SpatialGridCell64 cell, uint mx, uint my, uint mz)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                hash = (hash ^ FoldLong(cell.X * (long)(mx | 1u))) * 1099511628211UL;
                hash = (hash ^ FoldLong(cell.Y * (long)(my | 1u))) * 1099511628211UL;
                hash = (hash ^ FoldLong(cell.Z * (long)(mz | 1u))) * 1099511628211UL;
                hash = hash != 0UL ? hash : 1UL;
                return new uint2((uint)hash, (uint)(hash >> 32));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashCell(long x, long y, long z, uint mx, uint my, uint mz)
        {
            SpatialGridCell64 cell = new SpatialGridCell64 { X = x, Y = y, Z = z };
            return HashCell(in cell, mx, my, mz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 FingerprintCell(long x, long y, long z, uint mx, uint my, uint mz)
        {
            SpatialGridCell64 cell = new SpatialGridCell64 { X = x, Y = y, Z = z };
            return FingerprintCell(in cell, mx, my, mz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashCellFromFingerprint(uint2 fingerprint)
        {
            uint folded = fingerprint.x ^ fingerprint.y;
            return folded != 0u ? folded : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CellFingerprintEquals(uint2 left, uint2 right)
        {
            return left.x == right.x && left.y == right.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CellFingerprintEquals(uint leftX, uint leftY, uint2 right)
        {
            return leftX == right.x && leftY == right.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MixStateHash(uint hash, uint value)
        {
            unchecked
            {
                hash = (hash ^ value) * 16777619u;
                hash ^= hash >> 15;
                return hash != 0u ? hash : 2166136261u;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long QuantizeAxis(double value, double invCellSize)
        {
            if (!math.isfinite(value))
                return 0L;

            double scaled = value * invCellSize;
            double nearest = math.round(scaled);
            double stable = math.select(scaled, nearest, math.abs(scaled - nearest) <= QuantizationEpsilon);
            double floored = math.floor(stable);
            const double min = -9223372036854770000.0d;
            const double max = 9223372036854770000.0d;
            floored = math.clamp(floored, min, max);
            return (long)floored;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong FoldLong(long value)
        {
            unchecked
            {
                ulong x = (ulong)value;
                x ^= x >> 33;
                x *= 0xff51afd7ed558ccdUL;
                x ^= x >> 33;
                return x;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct SpatialHashQuery
    {
        [FieldOffset(0)] public double3 CenterAbsolute;
        [FieldOffset(24)] public VaultGenerationHandle<SpatialGridEntryDTO> EntriesHandle;
        [FieldOffset(40)] public VaultGenerationHandle<SpatialGridBucketRangeDTO> BucketRangesHandle;
        [FieldOffset(56)] public VaultGenerationHandle<AmbientEntityAupDTO> AupSnapshotHandle;
        [FieldOffset(72)] public VaultGenerationHandle<SpatialGridTelemetryEntry> TelemetryHandle;
        [FieldOffset(88)] public VaultGenerationHandle<int> TelemetryCursorHandle;
        [FieldOffset(104)] public int EntryCount;
        [FieldOffset(108)] public int BucketMask;
        [FieldOffset(112)] public uint Frame;
        [FieldOffset(116)] public float CellSizeMeters;
        [FieldOffset(120)] public uint HashMultiplierX;
        [FieldOffset(124)] public uint HashMultiplierY;
        [FieldOffset(128)] public uint HashMultiplierZ;
        [FieldOffset(132)] public int MaxResults;
        [FieldOffset(136)] public int MaxProbeCount;
        [FieldOffset(140)] private uint _pad0;
        private static readonly ulong TelemetryMutationGuardMask =
            SpatialGridMutationGuardBit(BufferID.ShinobuSpatialGridTelemetryCursor) |
            SpatialGridMutationGuardBit(BufferID.ShinobuSpatialGridTelemetryRing);

        public int CollectEntitiesInRadius(IDataVault vault, double3 centerAup, float radiusMeters, NativeArray<uint> results)
        {
            int resultLength = results.IsCreated ? results.Length : 0;
            if (resultLength <= 0 ||
                !math.all(math.isfinite(centerAup)) ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(CellSizeMeters))
            {
                uint invalidInputHash = 2166136261u;
                invalidInputHash = ShinobuSpatialGridMath.MixStateHash(invalidInputHash, 5u);
                invalidInputHash = ShinobuSpatialGridMath.MixStateHash(invalidInputHash, (uint)math.max(0, resultLength));
                invalidInputHash = ShinobuSpatialGridMath.MixStateHash(invalidInputHash, math.all(math.isfinite(centerAup)) ? 1u : 0u);
                invalidInputHash = ShinobuSpatialGridMath.MixStateHash(invalidInputHash, math.isfinite(radiusMeters) ? 1u : 0u);
                invalidInputHash = ShinobuSpatialGridMath.MixStateHash(invalidInputHash, math.isfinite(CellSizeMeters) ? 1u : 0u);
                RecordQueryFailure(vault, invalidInputHash);
                return 0;
            }

            if (!TryResolveViews(
                    vault,
                    out NativeArray<SpatialGridEntryDTO>.ReadOnly entries,
                    out NativeArray<SpatialGridBucketRangeDTO>.ReadOnly bucketRanges,
                    out NativeArray<AmbientEntityAupDTO>.ReadOnly aupSnapshot,
                    out uint failureHash))
            {
                RecordQueryFailure(vault, failureHash);
                return 0;
            }

            int safeCount = math.min(math.max(0, EntryCount), math.min(entries.Length, aupSnapshot.Length));
            if (safeCount <= 0)
                return 0;

            float safeRadius = math.max(0.001f, radiusMeters);
            float safeCellSize = math.max(0.25f, CellSizeMeters);
            float radiusSq = safeRadius * safeRadius;
            SpatialGridCell64 baseCell = ShinobuSpatialGridMath.QuantizeCell(centerAup, safeCellSize);
            int cellProbeBudget = math.clamp(MaxProbeCount <= 0 ? 27 : MaxProbeCount, 1, 343);
            int cellRadius = ShinobuSpatialGridMath.ResolvePublicQueryCellRadius(safeRadius, safeCellSize, cellProbeBudget);
            int written = 0;
            int maxResults = math.min(resultLength, math.max(1, MaxResults));
            int evaluated = 0;
            int evaluationBudget = maxResults > int.MaxValue / 4 ? int.MaxValue : maxResults * 4;
            int maxEvaluated = math.min(safeCount, math.max(maxResults, evaluationBudget));
            uint2 centerFingerprint = ShinobuSpatialGridMath.FingerprintCell(
                in baseCell,
                HashMultiplierX,
                HashMultiplierY,
                HashMultiplierZ);
            uint centerHash = ShinobuSpatialGridMath.HashCellFromFingerprint(centerFingerprint);
            CollectRange(
                entries,
                bucketRanges,
                aupSnapshot,
                centerHash,
                centerFingerprint,
                centerAup,
                radiusSq,
                safeCount,
                maxResults,
                maxEvaluated,
                results,
                ref written,
                ref evaluated);
            int visitedCells = 1;
            int maxDistanceSq = cellRadius * cellRadius * 3;
            for (int distanceSq = 1; distanceSq <= maxDistanceSq && written < maxResults && visitedCells < cellProbeBudget && evaluated < maxEvaluated; distanceSq++)
            {
                for (int x = -cellRadius; x <= cellRadius && written < maxResults && visitedCells < cellProbeBudget && evaluated < maxEvaluated; x++)
                {
                    int xSq = x * x;
                    for (int y = -cellRadius; y <= cellRadius && written < maxResults && visitedCells < cellProbeBudget && evaluated < maxEvaluated; y++)
                    {
                        int xySq = xSq + (y * y);
                        for (int z = -cellRadius; z <= cellRadius && written < maxResults && visitedCells < cellProbeBudget && evaluated < maxEvaluated; z++)
                        {
                            if (xySq + (z * z) != distanceSq)
                                continue;

                            visitedCells++;
                            SpatialGridCell64 queryCell = default;
                            queryCell.X = baseCell.X + x;
                            queryCell.Y = baseCell.Y + y;
                            queryCell.Z = baseCell.Z + z;
                            uint2 fingerprint = ShinobuSpatialGridMath.FingerprintCell(
                                in queryCell,
                                HashMultiplierX,
                                HashMultiplierY,
                                HashMultiplierZ);
                            uint hash = ShinobuSpatialGridMath.HashCellFromFingerprint(fingerprint);

                            CollectRange(
                                entries,
                                bucketRanges,
                                aupSnapshot,
                                hash,
                                fingerprint,
                                centerAup,
                                radiusSq,
                                safeCount,
                                maxResults,
                                maxEvaluated,
                                results,
                                ref written,
                                ref evaluated);
                        }
                    }
                }
            }

            return written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CollectRange(
            NativeArray<SpatialGridEntryDTO>.ReadOnly entries,
            NativeArray<SpatialGridBucketRangeDTO>.ReadOnly bucketRanges,
            NativeArray<AmbientEntityAupDTO>.ReadOnly aupSnapshot,
            uint cellHash,
            uint2 cellFingerprint,
            double3 centerAup,
            float radiusSq,
            int safeCount,
            int maxResults,
            int maxEvaluated,
            NativeArray<uint> results,
            ref int written,
            ref int evaluated)
        {
            if (!TryFindRange(bucketRanges, cellHash, cellFingerprint, out SpatialGridBucketRangeDTO range))
                return;

            int start = math.clamp(range.StartIndex, 0, safeCount);
            int available = safeCount - start;
            int count = math.min(math.max(0, range.Count), available);
            int end = start + count;
            for (int i = start; i < end && written < maxResults && evaluated < maxEvaluated; i++)
            {
                SpatialGridEntryDTO entry = entries[i];
                if (!ShinobuSpatialGridMath.CellFingerprintEquals(entry.CellFingerprint, cellFingerprint))
                    continue;

                evaluated++;
                int entityIndex = (int)entry.EntityRowIndex;
                if ((uint)entityIndex >= (uint)aupSnapshot.Length)
                    continue;

                AmbientEntityAupDTO meta = aupSnapshot[entityIndex];
                if (!ShinobuEcosystemBalancer.IsFiniteAup(in meta.PositionAup))
                    continue;

                double3 deltaAup = ShinobuEcosystemBalancer.ToAbsoluteDouble3(in meta.PositionAup) - centerAup;
                float3 localDelta = (float3)deltaAup;
                if (!math.all(math.isfinite(deltaAup)) ||
                    !math.all(math.isfinite(localDelta)) ||
                    math.lengthsq(localDelta) > radiusSq)
                    continue;

                results[written++] = entry.EntityRowIndex;
            }
        }

        public bool TryFindRange(IDataVault vault, uint cellHash, uint2 cellFingerprint, out SpatialGridBucketRangeDTO range)
        {
            if (TryResolveBucketRanges(vault, out NativeArray<SpatialGridBucketRangeDTO>.ReadOnly bucketRanges, out _))
                return TryFindRange(bucketRanges, cellHash, cellFingerprint, out range);

            range = default;
            return false;
        }

        private bool TryFindRange(
            NativeArray<SpatialGridBucketRangeDTO>.ReadOnly bucketRanges,
            uint cellHash,
            uint2 cellFingerprint,
            out SpatialGridBucketRangeDTO range)
        {
            range = default;
            if (cellHash == 0u || !bucketRanges.IsCreated || bucketRanges.Length <= 0)
                return false;

            int mask = BucketMask > 0 ? BucketMask : bucketRanges.Length - 1;
            int maxProbe = ShinobuSpatialGridMath.ResolveStructuralProbeCount(bucketRanges.Length);
            for (int probe = 0; probe < maxProbe; probe++)
            {
                int slot = (int)((cellHash + (uint)probe) & (uint)mask);
                if ((uint)slot >= (uint)bucketRanges.Length)
                    return false;

                SpatialGridBucketRangeDTO candidate = bucketRanges[slot];
                if (candidate.Flags != Frame)
                    return false;
                if (candidate.CellHash == cellHash &&
                    ShinobuSpatialGridMath.CellFingerprintEquals(candidate.CellFingerprintX, candidate.CellFingerprintY, cellFingerprint))
                {
                    range = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveViews(
            IDataVault vault,
            out NativeArray<SpatialGridEntryDTO>.ReadOnly entries,
            out NativeArray<SpatialGridBucketRangeDTO>.ReadOnly bucketRanges,
            out NativeArray<AmbientEntityAupDTO>.ReadOnly aupSnapshot,
            out uint failureHash)
        {
            entries = default;
            bucketRanges = default;
            aupSnapshot = default;
            failureHash = 2166136261u;

            if (vault == null)
            {
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, 1u);
                return false;
            }

            if (!ValidateVaultHandle(in EntriesHandle, BufferID.ShinobuSpatialGridEntries) ||
                !ValidateVaultHandle(in BucketRangesHandle, BufferID.ShinobuSpatialGridBucketRanges) ||
                !ValidateVaultHandle(in AupSnapshotHandle, BufferID.ShinobuAmbientAupSnapshot))
            {
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, 2u);
                return false;
            }

            if (!vault.TryReadOnlyHandle(in EntriesHandle, out entries) ||
                !vault.TryReadOnlyHandle(in BucketRangesHandle, out bucketRanges) ||
                !vault.TryReadOnlyHandle(in AupSnapshotHandle, out aupSnapshot) ||
                !entries.IsCreated ||
                !bucketRanges.IsCreated ||
                !aupSnapshot.IsCreated)
            {
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, 3u);
                return false;
            }

            int requiredCount = math.max(0, EntryCount);
            if (entries.Length < requiredCount ||
                aupSnapshot.Length < requiredCount ||
                bucketRanges.Length <= 0 ||
                (BucketMask > 0 && BucketMask >= bucketRanges.Length))
            {
                int resolvedEntryLength = entries.IsCreated ? entries.Length : 0;
                int resolvedAupLength = aupSnapshot.IsCreated ? aupSnapshot.Length : 0;
                int resolvedBucketLength = bucketRanges.IsCreated ? bucketRanges.Length : 0;
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, 4u);
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, (uint)requiredCount);
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, (uint)resolvedEntryLength);
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, (uint)resolvedAupLength);
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, (uint)resolvedBucketLength);
                return false;
            }

            return true;
        }

        private bool TryResolveBucketRanges(
            IDataVault vault,
            out NativeArray<SpatialGridBucketRangeDTO>.ReadOnly bucketRanges,
            out uint failureHash)
        {
            bucketRanges = default;
            failureHash = 2166136261u;
            if (vault == null)
            {
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, 1u);
                return false;
            }

            if (!ValidateVaultHandle(in BucketRangesHandle, BufferID.ShinobuSpatialGridBucketRanges))
            {
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, 2u);
                return false;
            }

            if (!vault.TryReadOnlyHandle(in BucketRangesHandle, out bucketRanges) ||
                !bucketRanges.IsCreated ||
                bucketRanges.Length <= 0 ||
                (BucketMask > 0 && BucketMask >= bucketRanges.Length))
            {
                int resolvedBucketLength = bucketRanges.IsCreated ? bucketRanges.Length : 0;
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, 3u);
                failureHash = ShinobuSpatialGridMath.MixStateHash(failureHash, (uint)resolvedBucketLength);
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValidateVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.AIEcology &&
                   handle.Generation != 0u;
        }

        private void RecordQueryFailure(IDataVault vault, uint failureHash)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !ValidateVaultHandle(in TelemetryHandle, BufferID.ShinobuSpatialGridTelemetryRing) ||
                !ValidateVaultHandle(in TelemetryCursorHandle, BufferID.ShinobuSpatialGridTelemetryCursor))
                return;

            if (!vault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                return;

            try
            {
                if (!vault.TryResolveHandle(in TelemetryCursorHandle, out NativeArray<int> telemetryCursor) ||
                    !telemetryCursor.IsCreated ||
                    telemetryCursor.Length <= 0 ||
                    !vault.TryResolveHandle(in TelemetryHandle, out NativeArray<SpatialGridTelemetryEntry> telemetry) ||
                    !telemetry.IsCreated ||
                    telemetry.Length <= 0)
                {
                    return;
                }

                int cursor = telemetryCursor[0];
                if (cursor < 0 ||
                    cursor >= int.MaxValue - ShinobuSpatialGridConstants.TelemetryCapacity)
                {
                    cursor = 0;
                }

                telemetryCursor[0] = cursor + 1;

                int slot = cursor % telemetry.Length;

                SpatialGridTelemetryEntry entry = default;
                entry.Frame = Frame;
                entry.EntityCount = math.max(0, EntryCount);
                entry.MaxBucketOccupancy = 0;
                entry.QueryCount = 0;
                entry.QuantizeMicroseconds = ShinobuSpatialGridConstants.TelemetryTimingUnavailableMicroseconds;
                entry.SortMicroseconds = ShinobuSpatialGridConstants.TelemetryTimingUnavailableMicroseconds;
                entry.GlobalQualityWeight = 0f;
                entry.CellSizeMeters = math.isfinite(CellSizeMeters) ? math.max(0.25f, CellSizeMeters) : 0.25f;
                entry.OverflowCount = 0u;
                entry.Flags = ShinobuSpatialGridConstants.TelemetryFlagInvalidInput |
                              ShinobuSpatialGridConstants.TelemetryFlagQueryResolveFailed |
                              ShinobuSpatialGridConstants.TelemetryFlagTimingUnavailable;
                entry.StateHash = ShinobuSpatialGridMath.MixStateHash(failureHash, (uint)EntryCount);
                entry.MaxProbeCount = MaxProbeCount;
                entry.MaxQueryResults = MaxResults;
                entry.BucketRangeCount = 0;
                entry.InvalidInputCount = 1;
                entry.Pad1 = 0u;
                telemetry[slot] = entry;
            }
            finally
            {
                vault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

        private static ulong SpatialGridMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockEntityCoordinatesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AmbientEntityDTO> Entities;
        [NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [NoAlias] public NativeArray<BoidStateDTO> BoidStates;
        public double3 CenterAbsolute;
        public float GlobalQualityWeight;
        public uint Frame;
        public int Count;

        public void Execute(int index)
        {
            if (index >= Count || index >= Entities.Length || index >= Aups.Length || index >= BoidStates.Length)
                return;

            uint seed = Hash32((uint)index ^ (Frame * 747796405u) ^ 0x53473331u);
            int cluster = (int)(seed & 31u);
            uint clusterSeed = Hash32((uint)cluster * 0x9E3779B9u);
            double3 clusterCenter = new double3(
                ResolveSigned01(clusterSeed) * 50000.0d,
                ResolveSigned01(Hash32(clusterSeed ^ 0x59434C53u)) * 2200.0d,
                ResolveSigned01(Hash32(clusterSeed ^ 0x5A434C53u)) * 50000.0d);

            seed = Hash32(seed ^ 0x4A495454u);
            float q = ShinobuEcosystemBalancer.Smooth01(GlobalQualityWeight);
            double denseRadius = math.lerp(7.5f, 42f, q);
            double sparseRadius = math.lerp(120.0f, 800.0f, q);
            double radius = (cluster & 7) == 0 ? sparseRadius : denseRadius;
            double angle = (seed & 65535u) * (6.28318530717958647692d / 65535.0d);
            double height = ResolveSigned01(Hash32(seed ^ 0x48454947u)) * math.lerp(5.0d, 35.0d, q);
            double spread = math.sqrt(math.saturate((seed >> 8) * (1.0d / 16777215.0d))) * radius;
            float angleF = (float)angle;
            MathLodApproximation.ApproxSinCosBhaskara(angleF, out float angleSin, out float angleCos);
            double3 absolute = CenterAbsolute + clusterCenter + new double3(angleCos * spread, height, angleSin * spread);
            AbsoluteUniversePosition aup = ShinobuEcosystemBalancer.FromAbsoluteDouble3(absolute);
            float3 local = ShinobuEcosystemBalancer.ToFiniteLocalFloat3(absolute - CenterAbsolute);
            uint species = (index % 5) == 0 ? 0x4341524Eu : 0x48455242u;
            float3 tangent = ShinobuEcosystemBalancer.SafeNormalize(new float3(-local.z, 0f, local.x), new float3(0f, 0f, 1f));
            float speed = math.lerp(2.5f, 7.5f, q);
            Entities[index] = new AmbientEntityDTO
            {
                Position = local,
                Velocity = tangent * speed,
                SpeciesHash = species,
                Biomass = species == 0x4341524Eu ? 2.4f : 1f
            };
            Aups[index] = new AmbientEntityAupDTO
            {
                PositionAup = aup,
                Flags = ShinobuEcosystemBalancer.EntityFlagActive |
                        ShinobuEcosystemBalancer.EntityFlagHydrated |
                        (species == 0x4341524Eu ? ShinobuEcosystemBalancer.EntityFlagCarnivore : ShinobuEcosystemBalancer.EntityFlagHerbivore),
                SectorHash = 0u,
                SpatialCellHash = 0,
                StableSeed = seed
            };
            BoidStates[index] = ShinobuEcosystemBalancer.BuildBoidState(local, tangent * speed, species, index, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value != 0u ? value : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ResolveSigned01(uint value)
        {
            return ((value & 0xFFFFFFu) * (2.0d / 16777215.0d)) - 1.0d;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct QuantizeEntityCoordinatesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        [NoAlias] public NativeArray<SpatialGridEntryDTO> Entries;
        public float CellSizeMeters;
        public uint HashMultiplierX;
        public uint HashMultiplierY;
        public uint HashMultiplierZ;
        public int Count;

        public unsafe void Execute(int index)
        {
            if (index >= Count || index >= AupSnapshot.Length || index >= Entries.Length)
                return;

            SpatialGridEntryDTO* entries = (SpatialGridEntryDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Entries);
            ref SpatialGridEntryDTO entry = ref UnsafeUtility.AsRef<SpatialGridEntryDTO>(entries + index);
            AmbientEntityAupDTO meta = AupSnapshot[index];
            uint flags = meta.Flags;
            if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) != 0u ||
                !ShinobuEcosystemBalancer.IsFiniteAup(in meta.PositionAup))
            {
                entry = new SpatialGridEntryDTO
                {
                    EntityRowIndex = ShinobuSpatialGridConstants.InvalidEntityRowIndex,
                    CellHash = 0u,
                    CellFingerprint = default
                };
                return;
            }

            double3 absolute = ShinobuEcosystemBalancer.ToAbsoluteDouble3(in meta.PositionAup);
            SpatialGridCell64 cell = ShinobuSpatialGridMath.QuantizeCell(absolute, math.max(0.25f, CellSizeMeters));
            uint2 fingerprint = ShinobuSpatialGridMath.FingerprintCell(in cell, HashMultiplierX, HashMultiplierY, HashMultiplierZ);
            uint hash = ShinobuSpatialGridMath.HashCellFromFingerprint(fingerprint);
            entry = new SpatialGridEntryDTO
            {
                EntityRowIndex = (uint)index,
                CellHash = hash,
                CellFingerprint = fingerprint
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct SortSpatialGridJob : IJob
    {
        [NoAlias] public NativeArray<SpatialGridEntryDTO> Entries;
        [NoAlias] public NativeArray<SpatialGridEntryDTO> Scratch;
        public int Count;

        public unsafe void Execute()
        {
            int count = math.min(math.max(0, Count), math.min(Entries.Length, Scratch.Length));
            if (count <= 1)
                return;

            SpatialGridEntryDTO* src = (SpatialGridEntryDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Entries);
            SpatialGridEntryDTO* dst = (SpatialGridEntryDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Scratch);
            int* counts = stackalloc int[256];
            int* offsets = stackalloc int[256];
            SpatialGridEntryDTO* inPtr = src;
            SpatialGridEntryDTO* outPtr = dst;
            for (int pass = 0; pass < 8; pass++)
            {
                for (int i = 0; i < 256; i++)
                    counts[i] = 0;

                for (int i = 0; i < count; i++)
                    counts[ResolveSortByte(inPtr[i], pass)]++;

                int sum = 0;
                for (int i = 0; i < 256; i++)
                {
                    int c = counts[i];
                    offsets[i] = sum;
                    sum += c;
                }

                for (int i = 0; i < count; i++)
                {
                    SpatialGridEntryDTO entry = inPtr[i];
                    int bucket = ResolveSortByte(entry, pass);
                    outPtr[offsets[bucket]++] = entry;
                }

                SpatialGridEntryDTO* swap = inPtr;
                inPtr = outPtr;
                outPtr = swap;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveSortByte(SpatialGridEntryDTO entry, int pass)
        {
            int shift = (pass & 3) << 3;
            uint key = pass < 4 ? entry.CellFingerprint.x : entry.CellFingerprint.y;
            return (int)((key >> shift) & 255u);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildSpatialGridRangesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SpatialGridEntryDTO> Entries;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        [NoAlias] public NativeArray<SpatialGridBucketRangeDTO> BucketRanges;
        [NoAlias] public NativeArray<int> Counters;
        [WriteOnly, NoAlias] public NativeArray<SpatialGridTelemetryEntry> TelemetryOutput;
        public uint Frame;
        public float CellSizeMeters;
        public float GlobalQualityWeight;
        public int MaxProbeCount;
        public int MaxQueryResults;
        public int Count;
        public int CounterOverflowIndex;
        public int CounterInvalidIndex;

        public void Execute()
        {
            int safeCount = math.min(math.max(0, Count), Entries.Length);
            int invalidInputCount = CountInvalidInputs(safeCount);
            int maxProbe = ShinobuSpatialGridMath.ResolveStructuralProbeCount(BucketRanges.Length);
            int overflow = 0;
            int maxOccupancy = 0;
            int rangeCount = 0;
            uint stateHash = ShinobuSpatialGridMath.MixStateHash(2166136261u, (uint)invalidInputCount);
            int i = 0;
            while (i < safeCount)
            {
                SpatialGridEntryDTO start = Entries[i];
                uint hash = start.CellHash;
                if (hash == 0u)
                {
                    i++;
                    continue;
                }

                int rangeStart = i;
                int occupancy = 0;
                uint2 rangeFingerprint = start.CellFingerprint;
                uint rangeFingerprintHash = hash;
                while (i < safeCount &&
                       Entries[i].CellHash == hash &&
                       ShinobuSpatialGridMath.CellFingerprintEquals(Entries[i].CellFingerprint, rangeFingerprint))
                {
                    uint2 fingerprint = Entries[i].CellFingerprint;
                    rangeFingerprintHash = ShinobuSpatialGridMath.MixStateHash(rangeFingerprintHash, fingerprint.x ^ fingerprint.y);
                    occupancy++;
                    i++;
                }

                maxOccupancy = math.max(maxOccupancy, occupancy);
                stateHash = ShinobuSpatialGridMath.MixStateHash(stateHash, rangeFingerprintHash ^ (uint)occupancy);
                if (TryInsertRange(hash, rangeFingerprint, rangeStart, occupancy, maxProbe))
                    rangeCount++;
                else
                    overflow++;
            }

            if (overflow != 0 && Counters.IsCreated && (uint)CounterOverflowIndex < (uint)Counters.Length)
                Counters[CounterOverflowIndex] += overflow;

            if (invalidInputCount != 0 && Counters.IsCreated && (uint)CounterInvalidIndex < (uint)Counters.Length)
                Counters[CounterInvalidIndex] += invalidInputCount;

            if (TelemetryOutput.IsCreated && TelemetryOutput.Length > 0)
            {
                SpatialGridTelemetryEntry entry = default;
                entry.Frame = Frame;
                entry.EntityCount = safeCount;
                entry.MaxBucketOccupancy = maxOccupancy;
                entry.QueryCount = 0;
                entry.QuantizeMicroseconds = ShinobuSpatialGridConstants.TelemetryTimingUnavailableMicroseconds;
                entry.SortMicroseconds = ShinobuSpatialGridConstants.TelemetryTimingUnavailableMicroseconds;
                entry.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
                entry.CellSizeMeters = math.max(0.25f, CellSizeMeters);
                entry.OverflowCount = (uint)overflow;
                entry.Flags = (overflow != 0 ? ShinobuSpatialGridConstants.TelemetryFlagOverflow : 0u) |
                              ShinobuSpatialGridConstants.TelemetryFlagTimingUnavailable |
                              (invalidInputCount != 0 ? ShinobuSpatialGridConstants.TelemetryFlagInvalidInput : 0u);
                entry.StateHash = stateHash;
                entry.MaxProbeCount = maxProbe;
                entry.MaxQueryResults = MaxQueryResults;
                entry.BucketRangeCount = rangeCount;
                entry.InvalidInputCount = invalidInputCount;
                entry.Pad1 = 0u;
                TelemetryOutput[0] = entry;
            }
        }

        private int CountInvalidInputs(int safeCount)
        {
            if (!AupSnapshot.IsCreated || AupSnapshot.Length <= 0)
                return 0;

            int invalid = 0;
            int limit = math.min(safeCount, AupSnapshot.Length);
            for (int i = 0; i < limit; i++)
            {
                AmbientEntityAupDTO meta = AupSnapshot[i];
                invalid += math.select(
                    0,
                    1,
                    (meta.Flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) != 0u ||
                    !ShinobuEcosystemBalancer.IsFiniteAup(in meta.PositionAup));
            }

            return invalid;
        }

        private bool TryInsertRange(uint cellHash, uint2 cellFingerprint, int startIndex, int count, int maxProbe)
        {
            if (!BucketRanges.IsCreated || BucketRanges.Length <= 0)
                return false;

            int mask = BucketRanges.Length - 1;
            for (int probe = 0; probe < maxProbe; probe++)
            {
                int slot = (int)((cellHash + (uint)probe) & (uint)mask);
                if ((uint)slot >= (uint)BucketRanges.Length)
                    return false;

                SpatialGridBucketRangeDTO current = BucketRanges[slot];
                if (current.Flags != Frame ||
                    (current.CellHash == cellHash &&
                     ShinobuSpatialGridMath.CellFingerprintEquals(current.CellFingerprintX, current.CellFingerprintY, cellFingerprint)))
                {
                    BucketRanges[slot] = new SpatialGridBucketRangeDTO
                    {
                        CellHash = cellHash,
                        CellFingerprintX = cellFingerprint.x,
                        CellFingerprintY = cellFingerprint.y,
                        StartIndex = startIndex,
                        Count = count,
                        Flags = Frame,
                        Pad0 = 0u,
                        Pad1 = 0u
                    };
                    return true;
                }
            }

            return false;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildSpatialGridDebugCellsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SpatialGridBucketRangeDTO> BucketRanges;
        [ReadOnly, NoAlias] public NativeArray<SpatialGridEntryDTO> Entries;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        [NoAlias] public NativeArray<ShinobuSpatialHashDebugCell> DebugCells;
        [NoAlias] public NativeArray<int> DebugCellCount;
        public double3 CenterAbsolute;
        public uint Frame;
        public float CellSizeMeters;
        public int Count;
        public int Capacity;

        public void Execute()
        {
            int safeCapacity = math.min(math.max(0, Capacity), DebugCells.Length);
            for (int i = 0; i < safeCapacity; i++)
                DebugCells[i] = default;

            int safeCount = math.min(math.max(0, Count), math.min(Entries.Length, AupSnapshot.Length));
            int debugCount = 0;
            for (int rangeIndex = 0; rangeIndex < BucketRanges.Length && debugCount < safeCapacity; rangeIndex++)
            {
                SpatialGridBucketRangeDTO range = BucketRanges[rangeIndex];
                if (range.Flags != Frame || range.CellHash == 0u || range.Count <= 0 || (uint)range.StartIndex >= (uint)safeCount)
                    continue;

                SpatialGridEntryDTO first = Entries[range.StartIndex];
                int entityIndex = (int)first.EntityRowIndex;
                if ((uint)entityIndex >= (uint)AupSnapshot.Length)
                    continue;

                float cell = math.max(0.25f, CellSizeMeters);
                AmbientEntityAupDTO meta = AupSnapshot[entityIndex];
                if (!ShinobuEcosystemBalancer.IsFiniteAup(in meta.PositionAup))
                    continue;

                double3 absolute = ShinobuEcosystemBalancer.ToAbsoluteDouble3(in meta.PositionAup);
                SpatialGridCell64 gridCell = ShinobuSpatialGridMath.QuantizeCell(absolute, cell);
                double3 absoluteCenter = math.double3(
                    (gridCell.X + 0.5d) * cell,
                    (gridCell.Y + 0.5d) * cell,
                    (gridCell.Z + 0.5d) * cell);
                if (!ShinobuEcosystemBalancer.TryToFiniteLocalFloat3(absoluteCenter - CenterAbsolute, out float3 centerLocal))
                    continue;

                ShinobuSpatialHashDebugCell debugCell = default;
                debugCell.CenterLocal = centerLocal;
                debugCell.CellHash = (int)range.CellHash;
                debugCell.Occupancy = range.Count;
                debugCell.CellSizeMeters = cell;
                debugCell.Flags = 2u;
                DebugCells[debugCount++] = debugCell;
            }

            if (DebugCellCount.IsCreated && DebugCellCount.Length > 0)
                DebugCellCount[0] = debugCount;
        }
    }

#if UNITY_EDITOR
    public static unsafe class SpatialGridProfileCsv
    {
        public static int Parse(
            NativeArray<byte> bytes,
            int length,
            NativeArray<SpatialGridProfileDTO> profiles,
            NativeArray<SpatialGridTuningDTO> tuning)
        {
            if (!bytes.IsCreated || !profiles.IsCreated || length <= 0)
                return 0;

            int limit = math.min(length, bytes.Length);
            byte* data = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            int row = 0;
            int lineStart = 0;
            for (int i = 0; i <= limit && row < profiles.Length; i++)
            {
                bool end = i == limit || data[i] == (byte)'\n';
                if (!end)
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && data[lineEnd - 1] == (byte)'\r')
                    lineEnd--;

                if (TryParseLine(data, lineStart, lineEnd - lineStart, out SpatialGridProfileDTO profile))
                {
                    profiles[row++] = profile;
                    if (row == 1 && tuning.IsCreated && tuning.Length > 0)
                    {
                        SpatialGridTuningDTO next = ShinobuSpatialGridMath.CreateDefaultTuning();
                        next.BaseGridCellSize = profile.BaseGridCellSize;
                        next.MinGridCellSize = profile.MinGridCellSize;
                        next.MaxGridCellSize = profile.MaxGridCellSize;
                        next.MaxQueryResultsLimit = profile.MaxQueryResultsLimit;
                        tuning[0] = ShinobuSpatialGridMath.Sanitize(next);
                    }
                }

                lineStart = i + 1;
            }

            for (int i = row; i < profiles.Length; i++)
                profiles[i] = default;
            return row;
        }

        public static int Parse(
            ReadOnlySpan<byte> bytes,
            Span<SpatialGridProfileDTO> profiles,
            out SpatialGridTuningDTO tuning)
        {
            tuning = default;
            if (profiles.Length <= 0 || bytes.Length <= 0)
                return 0;

            int row = 0;
            int lineStart = 0;
            fixed (byte* data = bytes)
            {
                int limit = bytes.Length;
                for (int i = 0; i <= limit && row < profiles.Length; i++)
                {
                    bool end = i == limit || data[i] == (byte)'\n';
                    if (!end)
                        continue;

                    int lineEnd = i;
                    if (lineEnd > lineStart && data[lineEnd - 1] == (byte)'\r')
                        lineEnd--;

                    if (TryParseLine(data, lineStart, lineEnd - lineStart, out SpatialGridProfileDTO profile))
                    {
                        profiles[row++] = profile;
                        if (row == 1)
                        {
                            SpatialGridTuningDTO next = ShinobuSpatialGridMath.CreateDefaultTuning();
                            next.BaseGridCellSize = profile.BaseGridCellSize;
                            next.MinGridCellSize = profile.MinGridCellSize;
                            next.MaxGridCellSize = profile.MaxGridCellSize;
                            next.MaxQueryResultsLimit = profile.MaxQueryResultsLimit;
                            tuning = ShinobuSpatialGridMath.Sanitize(next);
                        }
                    }

                    lineStart = i + 1;
                }
            }

            for (int i = row; i < profiles.Length; i++)
                profiles[i] = default;
            return row;
        }

        private static bool TryParseLine(byte* data, int start, int length, out SpatialGridProfileDTO profile)
        {
            profile = default;
            if (length <= 0)
                return false;

            int end = start + length;
            int cursor = start;
            int fieldStart = cursor;
            int field = 0;
            uint layerHash = 0u;
            float baseCell = 0f;
            float minCell = 0f;
            float maxCell = 0f;
            int maxResults = 0;
            int maxProbe = 0;
            while (cursor <= end)
            {
                bool delimiter = cursor == end || data[cursor] == (byte)',';
                if (!delimiter)
                {
                    cursor++;
                    continue;
                }

                int fieldLength = TrimField(data, ref fieldStart, cursor - fieldStart);
                if (field == 0)
                {
                    if (fieldLength <= 0 || IsHeader(data, fieldStart, fieldLength))
                        return false;
                    layerHash = HashBytes(data, fieldStart, fieldLength);
                }
                else if (field == 1)
                {
                    TryParseFloat(data, fieldStart, fieldLength, out baseCell);
                }
                else if (field == 2)
                {
                    TryParseFloat(data, fieldStart, fieldLength, out minCell);
                }
                else if (field == 3)
                {
                    TryParseFloat(data, fieldStart, fieldLength, out maxCell);
                }
                else if (field == 4)
                {
                    TryParseInt(data, fieldStart, fieldLength, out maxResults);
                }
                else if (field == 5)
                {
                    TryParseInt(data, fieldStart, fieldLength, out maxProbe);
                }

                field++;
                cursor++;
                fieldStart = cursor;
            }

            if (layerHash == 0u)
                return false;

            SpatialGridTuningDTO defaults = ShinobuSpatialGridMath.CreateDefaultTuning();
            profile = new SpatialGridProfileDTO
            {
                LayerHash = layerHash,
                BaseGridCellSize = baseCell > 0f ? baseCell : defaults.BaseGridCellSize,
                MinGridCellSize = minCell > 0f ? minCell : defaults.MinGridCellSize,
                MaxGridCellSize = maxCell > 0f ? maxCell : defaults.MaxGridCellSize,
                MaxQueryResultsLimit = maxResults > 0 ? maxResults : defaults.MaxQueryResultsLimit,
                MaxProbeCount = maxProbe > 0 ? maxProbe : 0,
                Flags = 1u
            };
            if (profile.MaxGridCellSize < profile.MinGridCellSize)
                profile.MaxGridCellSize = profile.MinGridCellSize;
            profile.BaseGridCellSize = math.clamp(profile.BaseGridCellSize, profile.MinGridCellSize, profile.MaxGridCellSize);
            return true;
        }

        private static int TrimField(byte* data, ref int start, int length)
        {
            int s = start;
            int e = start + length;
            while (s < e && data[s] <= 32)
                s++;
            while (e > s && data[e - 1] <= 32)
                e--;
            start = s;
            return e - s;
        }

        private static bool IsHeader(byte* data, int start, int length)
        {
            return length >= 5 &&
                   ToLower(data[start]) == (byte)'l' &&
                   ToLower(data[start + 1]) == (byte)'a' &&
                   ToLower(data[start + 2]) == (byte)'y' &&
                   ToLower(data[start + 3]) == (byte)'e' &&
                   ToLower(data[start + 4]) == (byte)'r';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static uint HashBytes(byte* data, int start, int length)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < length; i++)
                hash = (hash ^ ToLower(data[start + i])) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        private static bool TryParseInt(byte* data, int start, int length, out int value)
        {
            value = 0;
            if (length <= 0)
                return false;
            int cursor = start;
            int end = start + length;
            int sign = 1;
            if (data[cursor] == (byte)'-')
            {
                sign = -1;
                cursor++;
            }

            int acc = 0;
            bool any = false;
            while (cursor < end)
            {
                byte c = data[cursor++];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                acc = math.min(1000000, (acc * 10) + (c - (byte)'0'));
                any = true;
            }

            value = acc * sign;
            return any;
        }

        private static bool TryParseFloat(byte* data, int start, int length, out float value)
        {
            value = 0f;
            if (length <= 0)
                return false;
            int cursor = start;
            int end = start + length;
            float sign = 1f;
            if (data[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            double whole = 0.0d;
            bool any = false;
            while (cursor < end)
            {
                byte c = data[cursor];
                if (c == (byte)'.')
                    break;
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                whole = math.min(1000000.0d, whole * 10.0d + (c - (byte)'0'));
                cursor++;
                any = true;
            }

            double frac = 0.0d;
            double place = 0.1d;
            if (cursor < end && data[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < end)
                {
                    byte c = data[cursor++];
                    if (c < (byte)'0' || c > (byte)'9')
                        return false;
                    frac += (c - (byte)'0') * place;
                    place *= 0.1d;
                    any = true;
                }
            }

            value = (float)((whole + frac) * sign);
            return any && math.isfinite(value);
        }
    }
#endif

    public static unsafe class ShinobuSpatialGridForensics
    {
        private const ulong DumpMagic = 0x3130335F47505348UL;
        private const int DumpVersion = 1;
        private const int DumpHeaderBytes = 24;
        private const int DumpStateIdle = 0;
        private const int DumpStateSnapshotting = 1;
        private const int DumpStatePending = 2;
        private const int DumpStateWriting = 3;
        private const int DumpWorkerJoinMilliseconds = 500;
        private const int DumpWorkerPollMilliseconds = 100;
        private const int DumpFailureOwnerPath = 1;
        private const int DumpFailureAgentPath = 2;
        private const int DumpFailureQueue = 4;
        private const int DumpFailureAgent1419Path = 8;
        public const int DumpSnapshotBytes =
            DumpHeaderBytes + (ShinobuSpatialGridConstants.TelemetryCapacity * 64);

        private static IDataVault s_dumpVault;
        private static VaultGenerationHandle<byte> s_dumpSnapshotHandle;
        private static Thread s_dumpWorker;
        private static AutoResetEvent s_dumpSignal;
        private static NativeArray<byte> s_snapshotBuffer;
#pragma warning disable CS0414
        private static string s_ownerDumpPath;
        private static string s_agentDumpPath;
        private static string s_agent1419DumpPath;
#pragma warning restore CS0414
        private static int s_dumpState;
        private static int s_stopRequested;
        private static int s_pendingByteCount;
        private static int s_lastDumpFailureFlags;
        private static int s_totalDumpWriteFailures;

        public static int LastDumpFailureFlags => Volatile.Read(ref s_lastDumpFailureFlags);

        public static int TotalDumpWriteFailures => Volatile.Read(ref s_totalDumpWriteFailures);

        public static void RecordQueueFailure()
        {
            AddDumpFailureFlags(DumpFailureQueue);
            Interlocked.Increment(ref s_totalDumpWriteFailures);
        }

        private static void AddDumpFailureFlags(int flags)
        {
            if (flags == 0)
                return;

            int observed;
            int updated;
            do
            {
                observed = Volatile.Read(ref s_lastDumpFailureFlags);
                updated = observed | flags;
                if (updated == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref s_lastDumpFailureFlags, updated, observed) != observed);
        }

        public static void WriteTelemetryDump(string projectRoot, NativeArray<SpatialGridTelemetryEntry> telemetry, int cursor)
        {
            if (!TryQueueTelemetryDump(s_dumpVault, in s_dumpSnapshotHandle, telemetry, cursor))
                RecordQueueFailure();
        }

        public static bool TryWriteTelemetryDump(string projectRoot, NativeArray<SpatialGridTelemetryEntry> telemetry, int cursor)
        {
            return TryQueueTelemetryDump(s_dumpVault, in s_dumpSnapshotHandle, telemetry, cursor);
        }

        public static bool TryQueueTelemetryDump(
            IDataVault vault,
            in VaultGenerationHandle<byte> snapshotHandle,
            NativeArray<SpatialGridTelemetryEntry> telemetry,
            int cursor)
        {
            if (!IsDumpWorkerPrepared(vault, in snapshotHandle))
                return false;

            return TryQueueTelemetryDumpPrepared(vault, in snapshotHandle, telemetry, cursor);
        }

        public static bool EnsureDumpWorker(
            string projectRoot,
            IDataVault vault,
            in VaultGenerationHandle<byte> snapshotHandle)
        {
            if (projectRoot == null ||
                projectRoot.Length == 0 ||
                vault == null ||
                !ValidateSnapshotHandle(in snapshotHandle))
                return false;

            try
            {
                if (s_dumpWorker != null && s_dumpWorker.IsAlive)
                {
                    ShutdownDumpWorker();
                    if (s_dumpWorker != null && s_dumpWorker.IsAlive)
                        return false;
                }

                s_dumpVault = vault;
                s_dumpSnapshotHandle = snapshotHandle;
                s_ownerDumpPath = null;
                s_agentDumpPath = null;
                s_agent1419DumpPath = null;
                EnsureSnapshotBuffer();

                Volatile.Write(ref s_stopRequested, 0);
                s_dumpSignal = null;
                s_dumpWorker = null;

                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ThreadStateException)
            {
                return false;
            }
            catch (OutOfMemoryException)
            {
                return false;
            }
        }

        public static void ShutdownDumpWorker()
        {
            Volatile.Write(ref s_stopRequested, 1);
            AutoResetEvent signal = s_dumpSignal;
            if (signal != null)
            {
                try
                {
                    signal.Set();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            Thread worker = s_dumpWorker;
            bool workerStopped = worker == null || !worker.IsAlive;
            if (worker != null && worker.IsAlive)
                workerStopped = worker.Join(DumpWorkerJoinMilliseconds);

            if (!workerStopped)
                return;

            DrainPendingDump();
            s_dumpWorker = null;
            if (signal != null)
                signal.Dispose();
            s_dumpSignal = null;
            s_dumpVault = null;
            s_dumpSnapshotHandle = default;
            if (s_snapshotBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(s_snapshotBuffer);
                s_snapshotBuffer.Dispose();
            }
            s_snapshotBuffer = default;
            Volatile.Write(ref s_pendingByteCount, 0);
            Volatile.Write(ref s_dumpState, DumpStateIdle);
            Volatile.Write(ref s_stopRequested, 0);
        }

        private static bool TryQueueTelemetryDumpPrepared(
            IDataVault vault,
            in VaultGenerationHandle<byte> snapshotHandle,
            NativeArray<SpatialGridTelemetryEntry> telemetry,
            int cursor)
        {
            if (vault == null ||
                !ValidateSnapshotHandle(in snapshotHandle) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
                return false;

            if (Volatile.Read(ref s_stopRequested) != 0)
                return false;

            if (Interlocked.CompareExchange(ref s_dumpState, DumpStateSnapshotting, DumpStateIdle) != DumpStateIdle)
                return false;

            if (!TryResolveSnapshotBuffer(out NativeArray<byte> snapshot))
            {
                Volatile.Write(ref s_dumpState, DumpStateIdle);
                return false;
            }

            int capacity = telemetry.Length;
            int count = math.min(capacity, ShinobuSpatialGridConstants.TelemetryCapacity);
            int safeCursor = cursor;
            if (safeCursor < 0 || safeCursor >= int.MaxValue - capacity)
                safeCursor = 0;

            long start = (long)safeCursor - count;
            if (start < 0L)
                start = 0L;

            int entrySize = UnsafeUtility.SizeOf<SpatialGridTelemetryEntry>();
            int byteCount = DumpHeaderBytes + (count * entrySize);
            if (entrySize != 64 ||
                byteCount <= DumpHeaderBytes ||
                byteCount > DumpSnapshotBytes)
            {
                Volatile.Write(ref s_dumpState, DumpStateIdle);
                return false;
            }

            Span<byte> bytes = AsSpan(snapshot, DumpSnapshotBytes);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(0, 8), DumpMagic);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8, 4), DumpVersion);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(12, 4), count);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(16, 4), safeCursor);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(20, 4), entrySize);

            int offset = DumpHeaderBytes;
            for (int i = 0; i < count; i++)
            {
                int slot = (int)((start + i) % capacity);
                SpatialGridTelemetryEntry entry = telemetry[slot];
                ReadOnlySpan<SpatialGridTelemetryEntry> entrySpan =
                    MemoryMarshal.CreateReadOnlySpan(ref entry, 1);
                MemoryMarshal.AsBytes(entrySpan).CopyTo(bytes.Slice(offset, entrySize));
                offset += entrySize;
            }

            if (byteCount < DumpSnapshotBytes)
                bytes.Slice(byteCount).Clear();

            Volatile.Write(ref s_pendingByteCount, byteCount);

            Thread.MemoryBarrier();
            Volatile.Write(ref s_lastDumpFailureFlags, 0);
            bool ownerWritten = TryWriteQueuedDumpFile(ShinobuSpatialGridConstants.DumpRelativePath);
            bool agentWritten = TryWriteQueuedDumpFile(ShinobuSpatialGridConstants.Agent1301DumpRelativePath);
            bool agent1419Written = TryWriteQueuedDumpFile(ShinobuSpatialGridConstants.Agent1419DumpRelativePath);
            int failureFlags = ownerWritten ? 0 : DumpFailureOwnerPath;
            failureFlags |= agentWritten ? 0 : DumpFailureAgentPath;
            failureFlags |= agent1419Written ? 0 : DumpFailureAgent1419Path;
            AddDumpFailureFlags(failureFlags);
            if (failureFlags != 0)
                Interlocked.Increment(ref s_totalDumpWriteFailures);

            Volatile.Write(ref s_dumpState, DumpStateIdle);
            return ownerWritten || agentWritten || agent1419Written;
        }

        private static void DumpWorkerLoop()
        {
            while (Volatile.Read(ref s_stopRequested) == 0)
            {
                AutoResetEvent signal = s_dumpSignal;
                if (signal == null)
                    return;

                try
                {
                    signal.WaitOne(DumpWorkerPollMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                DrainPendingDump();
            }

            DrainPendingDump();
        }

        private static void DrainPendingDump()
        {
            if (Interlocked.CompareExchange(ref s_dumpState, DumpStateWriting, DumpStatePending) != DumpStatePending)
                return;

            bool ownerWritten = TryWriteQueuedDumpFile(ShinobuSpatialGridConstants.DumpRelativePath);
            bool agentWritten = TryWriteQueuedDumpFile(ShinobuSpatialGridConstants.Agent1301DumpRelativePath);
            bool agent1419Written = TryWriteQueuedDumpFile(ShinobuSpatialGridConstants.Agent1419DumpRelativePath);
            int failureFlags = ownerWritten ? 0 : DumpFailureOwnerPath;
            failureFlags |= agentWritten ? 0 : DumpFailureAgentPath;
            failureFlags |= agent1419Written ? 0 : DumpFailureAgent1419Path;
            AddDumpFailureFlags(failureFlags);
            if (failureFlags != 0)
                Interlocked.Increment(ref s_totalDumpWriteFailures);

            Volatile.Write(ref s_dumpState, DumpStateIdle);
        }

        private static bool TryWriteQueuedDumpFile(string path)
        {
            int byteCount = Volatile.Read(ref s_pendingByteCount);
            if (byteCount <= DumpHeaderBytes ||
                byteCount > DumpSnapshotBytes ||
                !s_snapshotBuffer.IsCreated ||
                s_snapshotBuffer.Length < byteCount)
            {
                return false;
            }

            return NativeFaultDumpWriter.TryWriteAll(path, s_snapshotBuffer, byteCount);
        }

        private static void EnsureSnapshotBuffer()
        {
            if (s_snapshotBuffer.IsCreated && s_snapshotBuffer.Length >= DumpSnapshotBytes)
                return;

            if (s_snapshotBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(s_snapshotBuffer);
                s_snapshotBuffer.Dispose();
            }

            s_snapshotBuffer = new NativeArray<byte>(DumpSnapshotBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(s_snapshotBuffer, nameof(ShinobuSpatialGridForensics), nameof(s_snapshotBuffer), NativeAllocationLifetime.Session);
        }

        private static bool TryResolveSnapshotBuffer(out NativeArray<byte> snapshot)
        {
            snapshot = s_snapshotBuffer;
            return snapshot.IsCreated && snapshot.Length >= DumpSnapshotBytes;
        }

        private static bool IsDumpWorkerPrepared(
            IDataVault vault,
            in VaultGenerationHandle<byte> snapshotHandle)
        {
            return s_snapshotBuffer.IsCreated &&
                   s_snapshotBuffer.Length >= DumpSnapshotBytes &&
                   Volatile.Read(ref s_stopRequested) == 0 &&
                   s_dumpVault == vault &&
                   SameSnapshotHandle(in snapshotHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValidateSnapshotHandle(in VaultGenerationHandle<byte> handle)
        {
            return handle.BufferID == (uint)BufferID.ShinobuSpatialGridDumpSnapshot &&
                   handle.SystemID == (uint)SystemID.AIEcology &&
                   handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SameSnapshotHandle(in VaultGenerationHandle<byte> handle)
        {
            return s_dumpSnapshotHandle.BufferID == handle.BufferID &&
                   s_dumpSnapshotHandle.SystemID == handle.SystemID &&
                   s_dumpSnapshotHandle.Generation == handle.Generation &&
                   s_dumpSnapshotHandle.Flags == handle.Flags;
        }

        private static Span<byte> AsSpan(NativeArray<byte> buffer, int byteCount)
        {
            int safeCount = math.clamp(byteCount, 0, buffer.Length);
            return new Span<byte>(NativeArrayUnsafeUtility.GetUnsafePtr(buffer), safeCount);
        }

        private static ReadOnlySpan<byte> AsReadOnlySpan(NativeArray<byte> buffer, int byteCount)
        {
            int safeCount = math.clamp(byteCount, 0, buffer.Length);
            return new ReadOnlySpan<byte>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer), safeCount);
        }
    }
}
