using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Blittable parameter block for the sandbox tectonic shelf height function.
    /// </summary>
    public struct HectonSandboxAbyssalShelfParams
    {
        public double AupCellSizeMeters;
        public double DescentRadiusMeters;
        public double PlateCellSizeMeters;
        public float HighWorldY;
        public float LowWorldY;
        public float RidgeHeightMeters;
        public float RidgeMultiplier;
        public float RidgeWidthMeters;
        public float JunctionWidthMeters;
        public float PlateUniformity;
        public float DomainWarpMeters;
        public float DomainWarpFrequency;
        public float MacroExponentialFalloff;
        public float IslandCenterRadiusMeters;
        public float IslandJunctionThreshold;
        public uint Seed;
    }

    public struct HectonSandboxAbyssalShelfRidgeData
    {
        public float RidgeMask;
        public float EdgeMask;
        public float JunctionMask;
        public float IslandMask;
    }

    /// <summary>
    /// Blittable smoke-test sample output for the sandbox shelf height field.
    /// </summary>
    public struct HectonSandboxAbyssalShelfAuditSample
    {
        public double2 PositionAupXZ;
        public float HeightMeters;
        public float NeighborHeightXMeters;
        public float NeighborHeightZMeters;
        public float SlopeAngleDegrees;
        public byte Flags;
    }

    public struct HectonSandboxAbyssalShelfSampleReduction
    {
        public int InvalidSampleCount;
        public int CliffSampleCount;
        public int PlateauSampleCount;
        public float MinHeightMeters;
        public float MaxHeightMeters;
        public float MaxSlopeDegrees;
        public float SlopeAngleSumDegrees;
        public float ActiveSlopeAngleSumDegrees;
        public int Slope30SampleCount;
        public int ActiveSlopeSampleCount;
    }

    public struct HectonSandboxAbyssalShelfSmokeSummary
    {
        public int SampleCount;
        public int InvalidSampleCount;
        public int CliffSampleCount;
        public int PlateauSampleCount;
        public float MinHeightMeters;
        public float MaxHeightMeters;
        public float MaxSlopeDegrees;
        public float AverageSlopeDegrees;
        public float AverageActiveSlopeDegrees;
        public int Slope30SampleCount;
        public float AupDeterminismDeltaMeters;
        public float AupBoundaryDeltaMeters;
        public int OriginChunkInvalidSampleCount;
        public int FarChunkInvalidSampleCount;
        public float HighChunkAupDeltaMeters;
        public byte Passed;
    }

    /// <summary>
    /// Burst-safe terrain math for the HECTON sandbox planetary shelf.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public static class HectonSandboxAbyssalShelfMath
    {
        /// <summary>
        /// Evaluates absolute world height in meters from AUP XZ meters.
        /// </summary>
        /// <param name="absoluteX">Absolute Universe Position X in meters.</param>
        /// <param name="absoluteZ">Absolute Universe Position Z in meters.</param>
        /// <param name="parameters">Terrain function parameters.</param>
        /// <returns>Absolute world Y in meters.</returns>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateHeightMeters(
            double absoluteX,
            double absoluteZ,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            AbsoluteUniversePosition position = BuildAupXZ(
                absoluteX,
                absoluteZ,
                math.max(1.0, parameters.AupCellSizeMeters));
            return EvaluateHeightMeters(in position, in parameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateSeededHeightMeters(
            double2 aupXZ,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            float heightRange = math.max(0.001f, parameters.HighWorldY - parameters.LowWorldY);
            float macro01 = EvaluateGreatDescent01(
                aupXZ,
                parameters.DescentRadiusMeters,
                parameters.MacroExponentialFalloff);
            float baseY = math.lerp(parameters.HighWorldY, parameters.LowWorldY, macro01);
            float base01 = math.saturate((baseY - parameters.LowWorldY) / heightRange);

            HectonSandboxAbyssalShelfRidgeData ridge = EvaluateVoronoiRidgeData(aupXZ, in parameters);
            float ridgeMask = ridge.RidgeMask;
            float ridgeAttenuation = math.smoothstep(0.04f, 0.42f, base01);
            float ridgeLift01 = math.saturate(parameters.RidgeHeightMeters / heightRange) * ridgeMask * ridgeAttenuation;
            float multiplied01 = base01 * (1f + math.max(0f, parameters.RidgeMultiplier) * ridgeMask * ridgeAttenuation);
            float ridged01 = math.saturate(multiplied01 + ridgeLift01);
            float heightMeters = parameters.LowWorldY + ridged01 * heightRange;

            if (heightMeters > 0f)
                heightMeters *= ridge.IslandMask;

            return math.clamp(heightMeters, parameters.LowWorldY, parameters.HighWorldY);
        }

        /// <summary>
        /// Evaluates absolute world height from an AUP payload without using presentation transform space.
        /// </summary>
        /// <param name="position">Absolute Universe Position payload.</param>
        /// <param name="parameters">Terrain function parameters.</param>
        /// <returns>Absolute world Y in meters.</returns>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateHeightMeters(
            in AbsoluteUniversePosition position,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            double2 aupXZ = ResolveAupXZ(in position, math.max(1.0, parameters.AupCellSizeMeters));
            HectonSandboxAbyssalShelfParams seededParameters = parameters;
            seededParameters.Seed = DeriveAupGridSeed(
                parameters.Seed,
                position.GridX,
                position.GridZ);
            return EvaluateSeededHeightMeters(aupXZ, in seededParameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CombineWorldSeed(uint authoringSeed, int runtimeWorldSeed)
        {
            return Hash((int)authoringSeed, runtimeWorldSeed, 0x4D3C2B1Au);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint DeriveAupGridSeed(uint worldSeed, long gridX, long gridZ)
        {
            const long macroChunkGridCells = 20L;
            long chunkX = FloorDiv(gridX, macroChunkGridCells);
            long chunkZ = FloorDiv(gridZ, macroChunkGridCells);
            return Hash((int)chunkX, (int)chunkZ, worldSeed ^ 0x73C6A91Fu);
        }

        /// <summary>
        /// Converts absolute height in meters to MapMagic normalized terrain height.
        /// </summary>
        /// <param name="heightMeters">Absolute world height in meters.</param>
        /// <param name="lowWorldY">Minimum world Y represented by normalized 0.</param>
        /// <param name="highWorldY">Maximum world Y represented by normalized 1.</param>
        /// <returns>Normalized height in [0,1].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float NormalizeHeight01(float heightMeters, float lowWorldY, float highWorldY)
        {
            return math.saturate((heightMeters - lowWorldY) / math.max(0.001f, highWorldY - lowWorldY));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteUniversePosition BuildAupXZ(double absoluteX, double absoluteZ, double cellSizeMeters)
        {
            double safeCellSize = math.max(1.0, cellSizeMeters);
            long gridX = (long)math.floor(absoluteX / safeCellSize);
            long gridZ = (long)math.floor(absoluteZ / safeCellSize);

            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = 0L,
                GridZ = gridZ,
                LocalX = (float)(absoluteX - gridX * safeCellSize),
                LocalY = 0f,
                LocalZ = (float)(absoluteZ - gridZ * safeCellSize)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 ResolveSampleAupXZ(
            in AbsoluteUniversePosition origin,
            double localOffsetX,
            double localOffsetZ,
            double cellSizeMeters)
        {
            double safeCellSize = math.max(1.0, cellSizeMeters);
            return new double2(
                origin.GridX * safeCellSize + origin.LocalX + localOffsetX,
                origin.GridZ * safeCellSize + origin.LocalZ + localOffsetZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double2 ResolveAupAlignedXZ(double2 absoluteXZ, double cellSizeMeters)
        {
            long gridX = (long)math.floor(absoluteXZ.x / cellSizeMeters);
            long gridZ = (long)math.floor(absoluteXZ.y / cellSizeMeters);
            double localX = absoluteXZ.x - gridX * cellSizeMeters;
            double localZ = absoluteXZ.y - gridZ * cellSizeMeters;
            return new double2(gridX * cellSizeMeters + localX, gridZ * cellSizeMeters + localZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double2 ResolveAupXZ(in AbsoluteUniversePosition position, double cellSizeMeters)
        {
            return new double2(
                position.GridX * cellSizeMeters + position.LocalX,
                position.GridZ * cellSizeMeters + position.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateGreatDescent01(
            double2 aupXZ,
            double descentRadiusMeters,
            float macroExponentialFalloff)
        {
            double radius = math.sqrt(aupXZ.x * aupXZ.x + aupXZ.y * aupXZ.y);
            double t = math.saturate(radius / math.max(1.0, descentRadiusMeters));
            double falloff = math.max(0.1, macroExponentialFalloff);
            double curved = 1.0 - math.exp(-falloff * t * t);
            double normalization = 1.0 - math.exp(-falloff);
            return (float)(curved / math.max(0.000001, normalization));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static HectonSandboxAbyssalShelfRidgeData EvaluateVoronoiRidgeData(
            double2 aupXZ,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            double2 warpedXZ = aupXZ + EvaluateDomainWarp(aupXZ, in parameters);
            double safePlateSize = math.max(1.0, parameters.PlateCellSizeMeters);
            double2 platePosition = warpedXZ / safePlateSize;
            int2 baseCell = (int2)math.floor(platePosition);

            double first = double.MaxValue;
            double second = double.MaxValue;
            double third = double.MaxValue;
            uint nearestHash = 0u;

            for (int dz = -2; dz <= 2; dz++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int2 cell = baseCell + new int2(dx, dz);
                    double2 feature = new double2(cell.x, cell.y) + ResolveFeatureOffset(cell, parameters.Seed, parameters.PlateUniformity);
                    double2 delta = platePosition - feature;
                    double distSq = delta.x * delta.x + delta.y * delta.y;

                    if (distSq < first)
                    {
                        third = second;
                        second = first;
                        first = distSq;
                        nearestHash = Hash(cell.x, cell.y, parameters.Seed);
                    }
                    else if (distSq < second)
                    {
                        third = second;
                        second = distSq;
                    }
                    else if (distSq < third)
                    {
                        third = distSq;
                    }
                }
            }

            double firstDistance = math.sqrt(first);
            double secondDistance = math.sqrt(second);
            double thirdDistance = math.sqrt(third);
            float edgeDeltaMeters = (float)((secondDistance - firstDistance) * safePlateSize);
            float junctionDeltaMeters = (float)((thirdDistance - secondDistance) * safePlateSize);

            float edgeWidth = math.max(0.001f, parameters.RidgeWidthMeters);
            float junctionWidth = math.max(0.001f, parameters.JunctionWidthMeters);
            float edgeMask = 1f - math.smoothstep(edgeWidth * 0.18f, edgeWidth, edgeDeltaMeters);
            float junctionMask = 1f - math.smoothstep(junctionWidth * 0.22f, junctionWidth, junctionDeltaMeters);
            float forkNoise = FractalPerlinNoise(
                new float2((float)(warpedXZ.x * 0.00021), (float)(warpedXZ.y * 0.00021)),
                parameters.Seed ^ 0x51633E2Du);
            float irregularity = math.lerp(0.86f, 1.14f, HashToUnitFloat(nearestHash ^ 0xA24BAED5u));
            float branched = math.saturate(edgeMask * 0.82f + junctionMask * 0.72f + forkNoise * 0.10f);
            float ridgeMask = math.saturate(branched * irregularity);
            float islandNoise = FractalPerlinNoise(
                new float2((float)(warpedXZ.x * 0.000083), (float)(warpedXZ.y * 0.000083)),
                parameters.Seed ^ 0xDB4F0B91u);
            float junctionThreshold = math.saturate(parameters.IslandJunctionThreshold);
            float junctionIsland = junctionMask *
                math.smoothstep(junctionThreshold, math.min(0.999f, junctionThreshold + 0.22f), islandNoise);
            double radius = math.sqrt(aupXZ.x * aupXZ.x + aupXZ.y * aupXZ.y);
            float centerRadius = math.max(1f, parameters.IslandCenterRadiusMeters);
            float centerIsland = 1f - math.smoothstep(centerRadius * 0.35f, centerRadius, (float)radius);

            return new HectonSandboxAbyssalShelfRidgeData
            {
                RidgeMask = ridgeMask,
                EdgeMask = edgeMask,
                JunctionMask = junctionMask,
                IslandMask = math.saturate(math.max(centerIsland, junctionIsland))
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double2 EvaluateDomainWarp(
            double2 aupXZ,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            float amplitude = math.max(0f, parameters.DomainWarpMeters);
            if (amplitude <= 0.0001f)
                return double2.zero;

            float2 sample = new float2((float)aupXZ.x, (float)aupXZ.y) * math.max(0.000001f, parameters.DomainWarpFrequency);
            float lowX = FractalPerlinNoise(sample, parameters.Seed ^ 0x5F356495u) * 2f - 1f;
            float lowZ = FractalPerlinNoise(sample + new float2(17.317f, -41.113f), parameters.Seed ^ 0xC2B2AE35u) * 2f - 1f;
            float highX = FractalPerlinNoise(sample * 2.37f + new float2(-61.7f, 8.31f), parameters.Seed ^ 0xB5297A4Du) * 2f - 1f;
            float highZ = FractalPerlinNoise(sample * 2.11f + new float2(4.89f, 73.2f), parameters.Seed ^ 0x68E31DA4u) * 2f - 1f;
            float twist = FractalPerlinNoise(sample * 0.73f + new float2(31.19f, -22.7f), parameters.Seed ^ 0x1B56C4E9u) * 2f - 1f;
            float angle = twist * 1.0471976f;
            float s = math.sin(angle);
            float c = math.cos(angle);
            float2 warp = new float2(lowX, lowZ) * 0.72f + new float2(highX, highZ) * 0.28f;
            float2 twisted = new float2(warp.x * c - warp.y * s, warp.x * s + warp.y * c);
            return new double2(twisted.x * amplitude, twisted.y * amplitude);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double2 ResolveFeatureOffset(int2 cell, uint seed, float uniformity)
        {
            float u = math.saturate(uniformity);
            float2 hash = new float2(
                Hash01(cell.x, cell.y, seed),
                Hash01(cell.x, cell.y, seed ^ 0x9E3779B9u));
            float2 offset = math.lerp(new float2(0.5f, 0.5f), hash, u);
            return new double2(offset.x, offset.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FractalValueNoise(float2 sample, uint seed)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float normalization = 0f;

            for (int octave = 0; octave < 4; octave++)
            {
                total += ValueNoise(sample * frequency, seed + (uint)octave * 0x85EBCA6Bu) * amplitude;
                normalization += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.07f;
            }

            return total / math.max(0.0001f, normalization);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FractalPerlinNoise(float2 sample, uint seed)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float normalization = 0f;

            for (int octave = 0; octave < 4; octave++)
            {
                total += PerlinNoise(sample * frequency, seed + (uint)octave * 0x85EBCA6Bu) * amplitude;
                normalization += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.07f;
            }

            return total / math.max(0.0001f, normalization);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float PerlinNoise(float2 sample, uint seed)
        {
            float2 floorSample = math.floor(sample);
            int2 cell = (int2)floorSample;
            float2 local = sample - floorSample;
            float2 smooth = local * local * local * (local * (local * 6f - 15f) + 10f);

            float a = GradientDot(cell.x, cell.y, local, seed);
            float b = GradientDot(cell.x + 1, cell.y, local - new float2(1f, 0f), seed);
            float c = GradientDot(cell.x, cell.y + 1, local - new float2(0f, 1f), seed);
            float d = GradientDot(cell.x + 1, cell.y + 1, local - new float2(1f, 1f), seed);
            float value = math.lerp(math.lerp(a, b, smooth.x), math.lerp(c, d, smooth.x), smooth.y);
            return math.saturate(value * 0.70710678f + 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GradientDot(int x, int y, float2 delta, uint seed)
        {
            uint direction = Hash(x, y, seed) & 7u;
            float2 gradient =
                direction == 0u ? new float2(1f, 0f) :
                direction == 1u ? new float2(-1f, 0f) :
                direction == 2u ? new float2(0f, 1f) :
                direction == 3u ? new float2(0f, -1f) :
                direction == 4u ? new float2(0.70710678f, 0.70710678f) :
                direction == 5u ? new float2(-0.70710678f, 0.70710678f) :
                direction == 6u ? new float2(0.70710678f, -0.70710678f) :
                new float2(-0.70710678f, -0.70710678f);

            return math.dot(gradient, delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ValueNoise(float2 sample, uint seed)
        {
            float2 floorSample = math.floor(sample);
            int2 cell = (int2)floorSample;
            float2 local = sample - floorSample;
            float2 smooth = local * local * (3f - 2f * local);

            float a = Hash01(cell.x, cell.y, seed);
            float b = Hash01(cell.x + 1, cell.y, seed);
            float c = Hash01(cell.x, cell.y + 1, seed);
            float d = Hash01(cell.x + 1, cell.y + 1, seed);

            return math.lerp(
                math.lerp(a, b, smooth.x),
                math.lerp(c, d, smooth.x),
                smooth.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(int x, int y, uint seed)
        {
            return HashToUnitFloat(Hash(x, y, seed));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(int x, int y, uint seed)
        {
            uint hash = (uint)x * 0x8DA6B343u;
            hash ^= (uint)y * 0xD8163841u;
            hash ^= seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToUnitFloat(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            return remainder != 0L && ((remainder < 0L) != (divisor < 0L))
                ? quotient - 1L
                : quotient;
        }
    }

    /// <summary>
    /// Generates raw normalized heights for the sandbox abyssal shelf.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfBaseJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> OutputHeights01;
        public HectonSandboxAbyssalShelfParams Parameters;
        public int Width;
        public AbsoluteUniversePosition WorldOriginAup;
        public double CellSizeMeters;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            double2 world = HectonSandboxAbyssalShelfMath.ResolveSampleAupXZ(
                in WorldOriginAup,
                x * math.max(0.001, CellSizeMeters),
                z * math.max(0.001, CellSizeMeters),
                math.max(1.0, Parameters.AupCellSizeMeters));
            float heightMeters = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(world.x, world.y, in Parameters);
            OutputHeights01[index] = HectonSandboxAbyssalShelfMath.NormalizeHeight01(
                heightMeters,
                Parameters.LowWorldY,
                Parameters.HighWorldY);
        }
    }

    /// <summary>
    /// Quantizes shallow slopes into shelves and steep slopes into cliffs.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxSlopeQuantizationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputHeights01;
        [WriteOnly] public NativeArray<float> OutputHeights01;
        public int Width;
        public int Height;
        public float CellSizeMeters;
        public float LowWorldY;
        public float HighWorldY;
        public float PlateauSourceAngleDegrees;
        public float PlateauTargetAngleDegrees;
        public float CliffSourceAngleDegrees;
        public float CliffTargetAngleDegrees;
        public float Strength;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            float center01 = math.saturate(InputHeights01[index]);

            if (x <= 0 || z <= 0 || x >= Width - 1 || z >= Height - 1)
            {
                OutputHeights01[index] = center01;
                return;
            }

            float heightRange = math.max(0.001f, HighWorldY - LowWorldY);
            float invCellSize = 1f / math.max(0.001f, CellSizeMeters);
            float left = ToMeters(InputHeights01[index - 1], heightRange);
            float right = ToMeters(InputHeights01[index + 1], heightRange);
            float back = ToMeters(InputHeights01[index - Width], heightRange);
            float forward = ToMeters(InputHeights01[index + Width], heightRange);
            float center = ToMeters(center01, heightRange);

            float dx = (right - left) * 0.5f * invCellSize;
            float dz = (forward - back) * 0.5f * invCellSize;
            float gradient = math.max(0.0001f, math.sqrt(dx * dx + dz * dz));
            float angle = math.degrees(math.atan(gradient));
            float average = (left + right + back + forward) * 0.25f;
            float delta = center - average;

            float targetAngle = math.clamp(PlateauTargetAngleDegrees, 1f, 60f);
            float plateauSource = math.clamp(PlateauSourceAngleDegrees, targetAngle + 0.001f, 45f);
            float cliffSource = math.clamp(CliffSourceAngleDegrees, plateauSource + 0.001f, 88f);
            float cliffTarget = math.clamp(CliffTargetAngleDegrees, cliffSource + 0.001f, 89f);
            float plateauMask = 1f - math.smoothstep(targetAngle, plateauSource, angle);
            float cliffMask = math.smoothstep(cliffSource, cliffSource + math.max(1f, cliffTarget - cliffSource) * 0.25f, angle);
            float resolvedTargetAngle = math.lerp(angle, targetAngle, plateauMask);
            resolvedTargetAngle = math.lerp(resolvedTargetAngle, cliffTarget, cliffMask);
            float targetGradient = math.tan(math.radians(resolvedTargetAngle));
            float targetFactor = targetGradient / gradient;
            float quantizeStrength = math.saturate(Strength);

            float adjustMask = math.saturate(math.max(plateauMask, cliffMask));
            float factor = math.lerp(1f, targetFactor, adjustMask * quantizeStrength);
            factor = math.clamp(factor, 0.02f, 16f);

            float resolved = average + delta * factor;
            OutputHeights01[index] = math.saturate(resolved / heightRange);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ToMeters(float height01, float heightRange)
        {
            return math.saturate(height01) * heightRange;
        }
    }

    /// <summary>
    /// Samples the shelf function over AUP stress positions for smoke validation.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfSmokeSampleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AbsoluteUniversePosition> PositionsAup;
        [WriteOnly] public NativeArray<HectonSandboxAbyssalShelfAuditSample> OutputSamples;
        public HectonSandboxAbyssalShelfParams Parameters;
        public double SlopeProbeMeters;

        public void Execute(int index)
        {
            AbsoluteUniversePosition positionAup = PositionsAup[index];
            double2 position = HectonSandboxAbyssalShelfMath.ResolveSampleAupXZ(
                in positionAup,
                0.0,
                0.0,
                math.max(1.0, Parameters.AupCellSizeMeters));
            double probe = math.max(0.001, SlopeProbeMeters);
            AbsoluteUniversePosition neighborXAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                position.x + probe,
                position.y,
                math.max(1.0, Parameters.AupCellSizeMeters));
            AbsoluteUniversePosition neighborZAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                position.x,
                position.y + probe,
                math.max(1.0, Parameters.AupCellSizeMeters));
            float center = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in positionAup, in Parameters);
            float neighborX = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in neighborXAup, in Parameters);
            float neighborZ = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in neighborZAup, in Parameters);
            float dx = (neighborX - center) / (float)probe;
            float dz = (neighborZ - center) / (float)probe;
            float gradient = math.sqrt(dx * dx + dz * dz);
            float slopeAngle = math.degrees(math.atan(gradient));
            byte flags = 0;

            if (!math.isfinite(center) || !math.isfinite(neighborX) || !math.isfinite(neighborZ))
                flags |= 1;

            if (center < Parameters.LowWorldY - 0.5f || center > Parameters.HighWorldY + 0.5f)
                flags |= 2;

            if (slopeAngle >= 45f)
                flags |= 4;

            if (slopeAngle <= 15f)
                flags |= 8;

            if (slopeAngle >= 24f && slopeAngle <= 36f)
                flags |= 16;

            OutputSamples[index] = new HectonSandboxAbyssalShelfAuditSample
            {
                PositionAupXZ = position,
                HeightMeters = center,
                NeighborHeightXMeters = neighborX,
                NeighborHeightZMeters = neighborZ,
                SlopeAngleDegrees = slopeAngle,
                Flags = flags
            };
        }
    }

    /// <summary>
    /// Converts smoke samples into per-sample reduction records without managed loops.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfSmokeReductionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<HectonSandboxAbyssalShelfAuditSample> Samples;
        [WriteOnly] public NativeArray<HectonSandboxAbyssalShelfSampleReduction> Reductions;

        public void Execute(int index)
        {
            HectonSandboxAbyssalShelfAuditSample sample = Samples[index];
            Reductions[index] = new HectonSandboxAbyssalShelfSampleReduction
            {
                InvalidSampleCount = (sample.Flags & 0x03) != 0 ? 1 : 0,
                CliffSampleCount = (sample.Flags & 0x04) != 0 ? 1 : 0,
                PlateauSampleCount = (sample.Flags & 0x08) != 0 ? 1 : 0,
                MinHeightMeters = sample.HeightMeters,
                MaxHeightMeters = sample.HeightMeters,
                MaxSlopeDegrees = sample.SlopeAngleDegrees,
                SlopeAngleSumDegrees = sample.SlopeAngleDegrees,
                ActiveSlopeAngleSumDegrees = sample.SlopeAngleDegrees > 15f && sample.SlopeAngleDegrees < 58f ? sample.SlopeAngleDegrees : 0f,
                Slope30SampleCount = (sample.Flags & 0x10) != 0 ? 1 : 0,
                ActiveSlopeSampleCount = sample.SlopeAngleDegrees > 15f && sample.SlopeAngleDegrees < 58f ? 1 : 0
            };
        }
    }

    /// <summary>
    /// Final cold-path smoke summary reduction. Runs under Burst after the parallel sample pass.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfSmokeSummaryJob : IJob
    {
        [ReadOnly] public NativeArray<HectonSandboxAbyssalShelfSampleReduction> Reductions;
        [WriteOnly] public NativeArray<HectonSandboxAbyssalShelfSmokeSummary> Summary;

        public HectonSandboxAbyssalShelfParams Parameters;
        public int RequiredSampleCount;
        public float RequiredMinHeightMeters;
        public float RequiredMaxHeightMeters;
        public float MaxAllowedSlopeDegrees;
        public float AupDeterminismToleranceMeters;
        public float AupBoundaryContinuityToleranceMeters;
        public double AupBoundaryProbeMeters;
        public int ChunkAuditResolution;
        public double ChunkAuditSizeMeters;
        public double FarChunkOriginMeters;

        public void Execute()
        {
            int sampleCount = Reductions.Length;
            int invalidCount = 0;
            int cliffCount = 0;
            int plateauCount = 0;
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            float maxSlope = 0f;
            float slopeSum = 0f;
            float activeSlopeSum = 0f;
            int slope30Count = 0;
            int activeSlopeCount = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                HectonSandboxAbyssalShelfSampleReduction reduction = Reductions[i];
                invalidCount += reduction.InvalidSampleCount;
                cliffCount += reduction.CliffSampleCount;
                plateauCount += reduction.PlateauSampleCount;
                minHeight = math.min(minHeight, reduction.MinHeightMeters);
                maxHeight = math.max(maxHeight, reduction.MaxHeightMeters);
                maxSlope = math.max(maxSlope, reduction.MaxSlopeDegrees);
                slopeSum += reduction.SlopeAngleSumDegrees;
                activeSlopeSum += reduction.ActiveSlopeAngleSumDegrees;
                slope30Count += reduction.Slope30SampleCount;
                activeSlopeCount += reduction.ActiveSlopeSampleCount;
            }

            double cellSize = math.max(1.0, Parameters.AupCellSizeMeters);
            AbsoluteUniversePosition shiftedAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                100125.0,
                -99625.0,
                cellSize);
            float shiftedA = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(100125.0, -99625.0, in Parameters);
            float shiftedB = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in shiftedAup, in Parameters);
            float aupDelta = math.abs(shiftedA - shiftedB);
            double boundaryProbe = math.max(0.001, AupBoundaryProbeMeters);
            AbsoluteUniversePosition boundaryLeftAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                cellSize - boundaryProbe,
                375.125,
                cellSize);
            AbsoluteUniversePosition boundaryRightAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                cellSize + boundaryProbe,
                375.125,
                cellSize);
            float boundaryLeft = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in boundaryLeftAup, in Parameters);
            float boundaryRight = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in boundaryRightAup, in Parameters);
            float boundaryDelta = math.abs(boundaryLeft - boundaryRight);
            double farOrigin = FarChunkOriginMeters;
            AbsoluteUniversePosition highChunkAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                farOrigin + 125.0,
                farOrigin + 375.0,
                cellSize);
            float highChunkDirect = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(
                farOrigin + 125.0,
                farOrigin + 375.0,
                in Parameters);
            float highChunkAupHeight = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in highChunkAup, in Parameters);
            float highChunkDelta = math.abs(highChunkDirect - highChunkAupHeight);
            int chunkResolution = math.max(2, ChunkAuditResolution);
            double chunkSize = math.max(1.0, ChunkAuditSizeMeters);
            int originChunkInvalid = CountInvalidChunk(0.0, 0.0, chunkResolution, chunkSize, in Parameters);
            int farChunkInvalid = CountInvalidChunk(farOrigin, farOrigin, chunkResolution, chunkSize, in Parameters);
            float averageSlope = slopeSum / math.max(1, sampleCount);
            float averageActiveSlope = activeSlopeSum / math.max(1, activeSlopeCount);
            bool passed =
                sampleCount == RequiredSampleCount &&
                invalidCount == 0 &&
                plateauCount > 0 &&
                slope30Count > 0 &&
                minHeight <= RequiredMinHeightMeters &&
                maxHeight >= RequiredMaxHeightMeters &&
                maxSlope <= MaxAllowedSlopeDegrees &&
                averageActiveSlope >= 24f &&
                averageActiveSlope <= 42f &&
                aupDelta <= AupDeterminismToleranceMeters &&
                boundaryDelta <= AupBoundaryContinuityToleranceMeters &&
                highChunkDelta <= AupDeterminismToleranceMeters &&
                originChunkInvalid == 0 &&
                farChunkInvalid == 0;

            Summary[0] = new HectonSandboxAbyssalShelfSmokeSummary
            {
                SampleCount = sampleCount,
                InvalidSampleCount = invalidCount,
                CliffSampleCount = cliffCount,
                PlateauSampleCount = plateauCount,
                MinHeightMeters = minHeight,
                MaxHeightMeters = maxHeight,
                MaxSlopeDegrees = maxSlope,
                AverageSlopeDegrees = averageSlope,
                AverageActiveSlopeDegrees = averageActiveSlope,
                Slope30SampleCount = slope30Count,
                AupDeterminismDeltaMeters = aupDelta,
                AupBoundaryDeltaMeters = boundaryDelta,
                OriginChunkInvalidSampleCount = originChunkInvalid,
                FarChunkInvalidSampleCount = farChunkInvalid,
                HighChunkAupDeltaMeters = highChunkDelta,
                Passed = passed ? (byte)1 : (byte)0
            };
        }

        private static int CountInvalidChunk(
            double originX,
            double originZ,
            int resolution,
            double chunkSizeMeters,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            int invalidCount = 0;
            double cellSize = math.max(1.0, parameters.AupCellSizeMeters);
            double step = chunkSizeMeters / math.max(1, resolution - 1);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    AbsoluteUniversePosition sampleAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                        originX + x * step,
                        originZ + z * step,
                        cellSize);
                    float h = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in sampleAup, in parameters);
                    if (!math.isfinite(h) || h < parameters.LowWorldY - 0.5f || h > parameters.HighWorldY + 0.5f)
                        invalidCount++;
                }
            }

            return invalidCount;
        }
    }
}
