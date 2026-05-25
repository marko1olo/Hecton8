using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
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
        public const uint InvalidEntityRowIndex = 0xFFFFFFFFu;
        public const uint DefaultHashMultiplierX = 73856093u;
        public const uint DefaultHashMultiplierY = 19349663u;
        public const uint DefaultHashMultiplierZ = 83492791u;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_301.bin";
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

    public struct SpatialHashQuery
    {
        [ReadOnly] public NativeArray<SpatialGridEntryDTO> Entries;
        [ReadOnly] public NativeArray<SpatialGridBucketRangeDTO> BucketRanges;
        [ReadOnly] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        public int EntryCount;
        public int BucketMask;
        public uint Frame;
        public float CellSizeMeters;
        public uint HashMultiplierX;
        public uint HashMultiplierY;
        public uint HashMultiplierZ;
        public double3 CenterAbsolute;
        public int MaxResults;
        public int MaxProbeCount;

        public int CollectEntitiesInRadius(double3 centerAup, float radiusMeters, NativeArray<uint> results)
        {
            if (!Entries.IsCreated || !BucketRanges.IsCreated || !AupSnapshot.IsCreated || !results.IsCreated || results.Length <= 0)
                return 0;

            int safeCount = math.min(math.max(0, EntryCount), math.min(Entries.Length, AupSnapshot.Length));
            if (safeCount <= 0)
                return 0;

            float safeRadius = math.max(0.001f, radiusMeters);
            float radiusSq = safeRadius * safeRadius;
            SpatialGridCell64 baseCell = ShinobuSpatialGridMath.QuantizeCell(centerAup, math.max(0.25f, CellSizeMeters));
            int cellProbeBudget = math.clamp(MaxProbeCount <= 0 ? 27 : MaxProbeCount, 1, 343);
            int cellRadius = ShinobuSpatialGridMath.ResolvePublicQueryCellRadius(safeRadius, CellSizeMeters, cellProbeBudget);
            int written = 0;
            int maxResults = math.min(results.Length, math.max(1, MaxResults));
            int evaluated = 0;
            int maxEvaluated = math.min(safeCount, math.max(maxResults, maxResults * 4));
            uint2 centerFingerprint = ShinobuSpatialGridMath.FingerprintCell(
                in baseCell,
                HashMultiplierX,
                HashMultiplierY,
                HashMultiplierZ);
            uint centerHash = ShinobuSpatialGridMath.HashCellFromFingerprint(centerFingerprint);
            CollectRange(centerHash, centerFingerprint, centerAup, radiusSq, safeCount, maxResults, maxEvaluated, results, ref written, ref evaluated);
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
                            SpatialGridCell64 queryCell = new SpatialGridCell64
                            {
                                X = baseCell.X + x,
                                Y = baseCell.Y + y,
                                Z = baseCell.Z + z
                            };
                            uint2 fingerprint = ShinobuSpatialGridMath.FingerprintCell(
                                in queryCell,
                                HashMultiplierX,
                                HashMultiplierY,
                                HashMultiplierZ);
                            uint hash = ShinobuSpatialGridMath.HashCellFromFingerprint(fingerprint);

                            CollectRange(hash, fingerprint, centerAup, radiusSq, safeCount, maxResults, maxEvaluated, results, ref written, ref evaluated);
                        }
                    }
                }
            }

            return written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CollectRange(
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
            if (!TryFindRange(cellHash, cellFingerprint, out SpatialGridBucketRangeDTO range))
                return;

            int end = math.min(safeCount, range.StartIndex + math.max(0, range.Count));
            for (int i = math.max(0, range.StartIndex); i < end && written < maxResults && evaluated < maxEvaluated; i++)
            {
                SpatialGridEntryDTO entry = Entries[i];
                if (!ShinobuSpatialGridMath.CellFingerprintEquals(entry.CellFingerprint, cellFingerprint))
                    continue;

                evaluated++;
                int entityIndex = (int)entry.EntityRowIndex;
                if ((uint)entityIndex >= (uint)AupSnapshot.Length)
                    continue;

                AmbientEntityAupDTO meta = AupSnapshot[entityIndex];
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

        public bool TryFindRange(uint cellHash, uint2 cellFingerprint, out SpatialGridBucketRangeDTO range)
        {
            range = default;
            if (cellHash == 0u || !BucketRanges.IsCreated || BucketRanges.Length <= 0)
                return false;

            int mask = BucketMask > 0 ? BucketMask : BucketRanges.Length - 1;
            int maxProbe = ShinobuSpatialGridMath.ResolveStructuralProbeCount(BucketRanges.Length);
            for (int probe = 0; probe < maxProbe; probe++)
            {
                int slot = (int)((cellHash + (uint)probe) & (uint)mask);
                if ((uint)slot >= (uint)BucketRanges.Length)
                    return false;

                SpatialGridBucketRangeDTO candidate = BucketRanges[slot];
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
            float3 local = (float3)(absolute - CenterAbsolute);
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
        [NoAlias] public NativeArray<SpatialGridTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
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

            if (TelemetryRing.IsCreated && TelemetryCursor.IsCreated && TelemetryRing.Length > 0 && TelemetryCursor.Length > 0)
            {
                int cursor = TelemetryCursor[0];
                if (cursor < 0 || cursor >= int.MaxValue - TelemetryRing.Length)
                    cursor = 0;

                int slot = cursor % TelemetryRing.Length;
                TelemetryCursor[0] = cursor + 1;
                TelemetryRing[slot] = new SpatialGridTelemetryEntry
                {
                    Frame = Frame,
                    EntityCount = safeCount,
                    MaxBucketOccupancy = maxOccupancy,
                    QueryCount = 0,
                    QuantizeMicroseconds = ShinobuSpatialGridConstants.TelemetryTimingUnavailableMicroseconds,
                    SortMicroseconds = ShinobuSpatialGridConstants.TelemetryTimingUnavailableMicroseconds,
                    GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                    CellSizeMeters = math.max(0.25f, CellSizeMeters),
                    OverflowCount = (uint)overflow,
                    Flags = (overflow != 0 ? ShinobuSpatialGridConstants.TelemetryFlagOverflow : 0u) |
                            ShinobuSpatialGridConstants.TelemetryFlagTimingUnavailable |
                            (invalidInputCount != 0 ? ShinobuSpatialGridConstants.TelemetryFlagInvalidInput : 0u),
                    StateHash = stateHash,
                    MaxProbeCount = maxProbe,
                    MaxQueryResults = MaxQueryResults,
                    BucketRangeCount = rangeCount,
                    InvalidInputCount = invalidInputCount,
                    Pad1 = 0u
                };
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
        [NoAlias] public NativeArray<int> Counters;
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
                double3 absoluteCenter = new double3(
                    (gridCell.X + 0.5d) * cell,
                    (gridCell.Y + 0.5d) * cell,
                    (gridCell.Z + 0.5d) * cell);
                float3 centerLocal = (float3)(absoluteCenter - CenterAbsolute);
                if (!math.all(math.isfinite(centerLocal)))
                    continue;

                DebugCells[debugCount++] = new ShinobuSpatialHashDebugCell
                {
                    CenterLocal = centerLocal,
                    CellHash = (int)range.CellHash,
                    Occupancy = range.Count,
                    CellSizeMeters = cell,
                    Flags = 2u
                };
            }

            if (Counters.IsCreated && Counters.Length > 8)
                Counters[8] = debugCount;
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

    public static unsafe class ShinobuSpatialGridForensics
    {
        private const ulong DumpMagic = 0x3130335F47505348UL;
        private const int DumpVersion = 1;

        public static void WriteTelemetryDump(string projectRoot, NativeArray<SpatialGridTelemetryEntry> telemetry, int cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0 || string.IsNullOrEmpty(projectRoot))
                return;

            string path = Path.Combine(projectRoot, ShinobuSpatialGridConstants.DumpRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> header = stackalloc byte[24];
                BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), telemetry.Length);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), cursor);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(20, 4), UnsafeUtility.SizeOf<SpatialGridTelemetryEntry>());
                stream.Write(header);

                int start = cursor - telemetry.Length;
                if (start < 0)
                    start = 0;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    int slot = (start + i) % telemetry.Length;
                    SpatialGridTelemetryEntry entry = telemetry[slot];
                    ReadOnlySpan<SpatialGridTelemetryEntry> entrySpan = MemoryMarshal.CreateReadOnlySpan(ref entry, 1);
                    stream.Write(MemoryMarshal.AsBytes(entrySpan));
                }
            }
        }
    }
#endif
}
