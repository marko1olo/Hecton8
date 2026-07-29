using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Blittable parameter block for the sandbox tectonic shelf height function.
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct HectonSandboxAbyssalShelfParams
    {
        [FieldOffset(0)]
        public double AupCellSizeMeters;
        [FieldOffset(8)]
        public double DescentRadiusMeters;
        [FieldOffset(16)]
        public double PlateCellSizeMeters;
        [FieldOffset(24)]
        public float HighWorldY;
        [FieldOffset(28)]
        public float LowWorldY;
        [FieldOffset(32)]
        public float RidgeHeightMeters;
        [FieldOffset(36)]
        public float RidgeMultiplier;
        [FieldOffset(40)]
        public float RidgeWidthMeters;
        [FieldOffset(44)]
        public float JunctionWidthMeters;
        [FieldOffset(48)]
        public float PlateUniformity;
        [FieldOffset(52)]
        public float DomainWarpMeters;
        [FieldOffset(56)]
        public float DomainWarpFrequency;
        [FieldOffset(60)]
        public float SlopeNoiseFrequency;
        [FieldOffset(64)]
        public float MacroExponentialFalloff;
        [FieldOffset(68)]
        public float ShelfRunMeters;
        [FieldOffset(72)]
        public float ShelfTargetSlopeDegrees;
        [FieldOffset(76)]
        public float TrenchDepthMeters;
        [FieldOffset(80)]
        public float TrenchWidthMeters;
        [FieldOffset(84)]
        public float TrenchSharpness;
        [FieldOffset(88)]
        public float IslandCenterRadiusMeters;
        [FieldOffset(92)]
        public float IslandJunctionThreshold;
        [FieldOffset(96)]
        public uint Seed;
        [FieldOffset(100)]
        public uint MacroGeologyArtifactVersion;
        [FieldOffset(104)]
        public int BenchmarkStage;
    }

    public struct HectonSandboxAbyssalShelfRidgeData
    {
        public float RidgeMask;
        public float EdgeMask;
        public float JunctionMask;
        public float IslandMask;
        public float TrenchMask;
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
        public float ActiveSlopeMinDegrees;
        public float ActiveSlopeMaxDegrees;
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
        public float ActiveSlopeMinDegrees;
        public float ActiveSlopeMaxDegrees;
        public int ActiveSlopeSampleCount;
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
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public static class HectonSandboxAbyssalShelfMath
    {
        /// <summary>
        /// Evaluates absolute world height in meters from AUP XZ meters.
        /// </summary>
        /// <param name="absoluteX">Absolute Universe Position X in meters.</param>
        /// <param name="absoluteZ">Absolute Universe Position Z in meters.</param>
        /// <param name="parameters">Terrain function parameters.</param>
        /// <returns>Absolute world Y in meters.</returns>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        /// <summary>
        /// R99 HEIGHT-IDENTITY FIX: this used to be a SECOND, disagreeing height function.
        /// It ran with <c>DetailProbeMeters = max(64, cell)</c> instead of <c>max(1, cell)</c>, skipped the
        /// meso detail layer entirely, and truncated the 64-bit AUP coordinate to float before sampling —
        /// so the same world point resolved to a different height depending on which entry point asked.
        /// The determinism smoke test only exercises <see cref="EvaluateFullHeightMeters"/>, so the gate
        /// never caught it. There is now exactly ONE height function; this is a forwarding shim.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateSeededHeightMeters(
            double2 aupXZ,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            return EvaluateFullHeightMeters(aupXZ.x, aupXZ.y, in parameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateFullHeightMeters(
            double absoluteX,
            double absoluteZ,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            float highWorldY = ResolveSlopeLockedHighWorldY(in parameters);
            WorldMacroGeologyParams macroParams = WorldMacroGeologyParams.CreateDefault(parameters.Seed);
            // The AUTHORED ocean datum, not 0. Geology computes height as WaterSurfaceY - depth
            // (WorldMacroGeologyFields.cs:1370), so a datum of 0 made every depth this generator produces
            // 14.02 m shallower than the depth the running game measures against the calibrated ocean.
            //
            // WorldWaterLevelCalibrationMath.DefaultWaterLevelY is 14.02f
            // (World/Contracts/WorldWaterLevelCalibrationContracts.cs:166), and
            // Tests/Editor/WaterlineFallbackRuntimeEditTests.cs already polices that exact constant name in
            // other files - this path evaded the rule rather than being exempt from it.
            macroParams.WaterSurfaceY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
            macroParams.DetailProbeMeters = (float)math.max(1.0, parameters.AupCellSizeMeters);
            macroParams.RidgeHeightMeters = parameters.RidgeHeightMeters;
            macroParams.RidgeWidthMeters = parameters.RidgeWidthMeters;
            macroParams.TrenchDepthMeters = parameters.TrenchDepthMeters;
            macroParams.TrenchWidthMeters = parameters.TrenchWidthMeters;

            WorldMacroGeologyFields.MacroMasks masks;
            float heightMeters = WorldMacroGeologyFields.EvaluateHeightMeters(absoluteX, absoluteZ, in macroParams, out masks);

            WorldMacroGeologySample macroSample = WorldMacroGeologyFields.BuildSample(
                absoluteX,
                absoluteZ,
                in macroParams,
                heightMeters,
                0f, 0f, 0f, 0f,
                in masks);

            WorldTerrainMesoDetailParams mesoParams = WorldTerrainMesoDetailFields.CreateDefaultParams(parameters.Seed);
            mesoParams.MaxMesoDeltaMeters = 70f;

            WorldTerrainMesoDetailSample mesoSample = WorldTerrainMesoDetailFields.Evaluate(
                in macroSample, (float)absoluteX, (float)absoluteZ, in mesoParams);

            heightMeters += mesoSample.HeightDeltaMeters;

            return math.clamp(heightMeters, parameters.LowWorldY, highWorldY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSlopeLockedHighWorldY(in HectonSandboxAbyssalShelfParams parameters)
        {
            return math.max(parameters.HighWorldY, parameters.LowWorldY + 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastTrenchSharpness01(float value, float sharpness)
        {
            float x = math.saturate(value);
            float x2 = x * x;
            float x3 = x2 * x;
            float wide = x * (2f - x);
            float steep = math.saturate((math.max(0.35f, sharpness) - 1f) * 0.5f);
            float shallow = math.saturate(1f - sharpness);
            return math.lerp(math.lerp(x2, x3, steep), wide, shallow);
        }

        /// <summary>
        /// Evaluates absolute world height from an AUP payload without using presentation transform space.
        /// </summary>
        /// <param name="position">Absolute Universe Position payload.</param>
        /// <param name="parameters">Terrain function parameters.</param>
        /// <returns>Absolute world Y in meters.</returns>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateHeightMeters(
            in AbsoluteUniversePosition position,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            double2 aupXZ = ResolveAupXZ(in position, math.max(1.0, parameters.AupCellSizeMeters));
            return EvaluateSeededHeightMeters(aupXZ, in parameters);
        }

        /// <summary>
        /// Evaluates the Voronoi ridge/intersection masks used by shelf generation for site gating.
        /// </summary>
        /// <param name="position">Absolute Universe Position payload.</param>
        /// <param name="parameters">Terrain function parameters.</param>
        /// <returns>Voronoi edge, junction, island, and trench masks in [0,1].</returns>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EvaluateVoronoiRidgeData(
            in AbsoluteUniversePosition position,
            in HectonSandboxAbyssalShelfParams parameters,
            out HectonSandboxAbyssalShelfRidgeData ridgeData)
        {
            double2 aupXZ = ResolveAupXZ(in position, math.max(1.0, parameters.AupCellSizeMeters));
            HectonSandboxAbyssalShelfParams seededParameters = parameters;
            // Removed legacy DeriveAupGridSeed to prevent chunk-boundary seams.
            EvaluateVoronoiRidgeData(aupXZ, in seededParameters, out ridgeData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CombineWorldSeed(uint authoringSeed, int runtimeWorldSeed)
        {
            return Hash((int)authoringSeed, runtimeWorldSeed, 0x4D3C2B1Au);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint DeriveAupGridSeed(uint worldSeed, long gridX, long gridZ)
        {
            // Legacy method, kept for backward compatibility if needed by older artifact versions.
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
        public static float SlopeAngleDegreesToGradient(float angleDegrees)
        {
            return global::Hecton8.Core.MathLodApproximation.ApproxTanClamped(math.radians(math.clamp(angleDegrees, 0.001f, 89f)), 4096f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateSlopeTargetAngleDegrees(
            double2 aupXZ,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            float frequency = parameters.SlopeNoiseFrequency > 0f
                ? parameters.SlopeNoiseFrequency
                : 0.00003125f;
            float seedOffset = HashToUnitFloat(parameters.Seed ^ 0xA12F49E7u) * 2048f;
            float2 sample = new float2((float)aupXZ.x, (float)aupXZ.y) * math.max(0.000001f, frequency);
            float n0 = noise.snoise(sample + new float2(seedOffset, -seedOffset * 0.6180339f));
            float n1 = noise.snoise(sample * 0.47f + new float2(-seedOffset * 0.37f, seedOffset * 1.21f));
            float shaped = math.saturate((n0 * 0.72f + n1 * 0.28f) * 0.5f + 0.5f);
            return math.lerp(22f, 38f, shaped);
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
            double radiusSq = aupXZ.x * aupXZ.x + aupXZ.y * aupXZ.y;
            double safeRadius = math.max(1.0, descentRadiusMeters);
            double tSq = math.saturate(radiusSq / (safeRadius * safeRadius));
            double falloff = math.max(0.1, macroExponentialFalloff);
            double curved = 1.0 - math.exp(-falloff * tSq);
            double normalization = 1.0 - math.exp(-falloff);
            return (float)(curved / math.max(0.000001, normalization));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double FastExpNegPade(double x)
        {
            return math.exp(-math.max(0.0, x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSqrtPositive(float value)
        {
            return math.sqrt(math.max(0f, value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastAtanDegreesPositive(float value)
        {
            float x = math.max(0f, value);
            float reciprocal = 1f / math.max(x, 0.000001f);
            bool useReciprocal = x > 1f;
            float y = math.select(x, reciprocal, useReciprocal);
            float radians = y / (1f + 0.280872f * y * y);
            radians = math.select(radians, 1.5707964f - radians, useReciprocal);
            return radians * 57.29578f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EvaluateVoronoiRidgeData(
            double2 aupXZ,
            in HectonSandboxAbyssalShelfParams parameters,
            out HectonSandboxAbyssalShelfRidgeData ridgeData)
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

            float firstDistance = FastSqrtPositive((float)first);
            float secondDistance = FastSqrtPositive((float)second);
            float thirdDistance = FastSqrtPositive((float)third);
            float safePlateSizeMeters = (float)safePlateSize;
            float edgeDeltaMeters = (secondDistance - firstDistance) * safePlateSizeMeters;
            float junctionDeltaMeters = (thirdDistance - secondDistance) * safePlateSizeMeters;

            float edgeWidth = math.max(0.001f, parameters.RidgeWidthMeters);
            float junctionWidth = math.max(0.001f, parameters.JunctionWidthMeters);
            float branchNoise = FractalPerlinNoise(
                new float2((float)(warpedXZ.x * 0.00037), (float)(warpedXZ.y * 0.00037)),
                parameters.Seed ^ 0x31D9A7B5u);
            float branchWidthScale = math.lerp(0.58f, 1.42f, branchNoise);
            float edgeMask = 1f - math.smoothstep(edgeWidth * 0.10f, edgeWidth * branchWidthScale, edgeDeltaMeters);
            float junctionWidthScale = math.lerp(0.72f, 1.32f, branchNoise);
            float junctionMask = 1f - math.smoothstep(junctionWidth * 0.14f, junctionWidth * junctionWidthScale, junctionDeltaMeters);
            float forkNoise = FractalPerlinNoise(
                new float2((float)(warpedXZ.x * 0.00021), (float)(warpedXZ.y * 0.00021)),
                parameters.Seed ^ 0x51633E2Du);
            float irregularity = math.lerp(0.86f, 1.14f, HashToUnitFloat(nearestHash ^ 0xA24BAED5u));
            float forkLift = math.smoothstep(0.38f, 0.92f, junctionMask + forkNoise * 0.28f);
            float branched = math.saturate(edgeMask * 0.76f + junctionMask * 0.82f + forkLift * 0.18f);
            float ridgeMask = math.saturate(branched * irregularity);
            float centerDistanceMeters = firstDistance * safePlateSizeMeters;
            float trenchWidth = parameters.TrenchWidthMeters > 0.001f
                ? parameters.TrenchWidthMeters
                : edgeWidth * 0.58f;
            trenchWidth = math.max(1f, trenchWidth);
            float centerMask = 1f - math.smoothstep(trenchWidth * 0.12f, trenchWidth, centerDistanceMeters);
            float lowCenterToken = FastLowCenterToken01(1f - HashToUnitFloat(nearestHash ^ 0x6C8E9CF5u));
            float trenchCandidate = math.smoothstep(0.46f, 0.92f, lowCenterToken);
            float trenchNoise = FractalPerlinNoise(
                new float2((float)(warpedXZ.x * 0.00043), (float)(warpedXZ.y * 0.00043)),
                parameters.Seed ^ 0x91E83B37u);
            float trenchMask = math.saturate(centerMask * trenchCandidate * math.lerp(0.74f, 1.18f, trenchNoise));
            float islandNoise = FractalPerlinNoise(
                new float2((float)(warpedXZ.x * 0.000083), (float)(warpedXZ.y * 0.000083)),
                parameters.Seed ^ 0xDB4F0B91u);
            float junctionThreshold = math.saturate(parameters.IslandJunctionThreshold);
            float junctionIsland = junctionMask *
                math.smoothstep(junctionThreshold, math.min(0.999f, junctionThreshold + 0.22f), islandNoise);
            float centerRadius = math.max(1f, parameters.IslandCenterRadiusMeters);
            double centerRadiusSq = centerRadius * (double)centerRadius;
            float centerIsland = 1f - math.smoothstep(
                (float)(centerRadiusSq * 0.1225),
                (float)centerRadiusSq,
                (float)(aupXZ.x * aupXZ.x + aupXZ.y * aupXZ.y));

            ridgeData = new HectonSandboxAbyssalShelfRidgeData
            {
                RidgeMask = ridgeMask,
                EdgeMask = edgeMask,
                JunctionMask = junctionMask,
                IslandMask = math.saturate(math.max(centerIsland, junctionIsland)),
                TrenchMask = trenchMask
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
            float octave0X = FractalPerlinNoise(sample, parameters.Seed ^ 0x5F356495u) * 2f - 1f;
            float octave0Z = FractalPerlinNoise(sample + new float2(17.317f, -41.113f), parameters.Seed ^ 0xC2B2AE35u) * 2f - 1f;
            float2 octave1Sample = sample * 2f;
            float octave1X = FractalPerlinNoise(octave1Sample + new float2(-61.7f, 8.31f), parameters.Seed ^ 0xB5297A4Du) * 2f - 1f;
            float octave1Z = FractalPerlinNoise(octave1Sample + new float2(4.89f, 73.2f), parameters.Seed ^ 0x68E31DA4u) * 2f - 1f;
            float twist = FractalPerlinNoise(sample * 0.73f + new float2(31.19f, -22.7f), parameters.Seed ^ 0x1B56C4E9u) * 2f - 1f;
            float angle = twist * 1.0471976f;
            float s = math.sin(angle);
            float c = math.cos(angle);
            float2 warp = (new float2(octave0X, octave0Z) + new float2(octave1X, octave1Z) * 0.5f) * 0.6666667f;
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
        private static float FastLowCenterToken01(float value)
        {
            float x = math.saturate(value);
            float x2 = x * x;
            return x2 * math.lerp(1f, x, 0.35f);
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
    /// <summary>
    /// Node holding the presampled macro height and masks for differential calculation.
    /// </summary>
    public struct PresampledMacroNode
    {
        public float HeightMeters;
        public WorldMacroGeologyFields.MacroMasks Masks;
    }

    /// <summary>
    /// Phase 1: Generates a presampled grid of (Width+2)x(Width+2) macro height and masks.
    /// This avoids 5x redundant evaluations in the differential job, reducing tile time from 95ms to <3ms.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfPresampleJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<PresampledMacroNode> PresampledNodes;
        public HectonSandboxAbyssalShelfParams Parameters;
        public int PresampledWidth; // This must be (Width + 2)
        public AbsoluteUniversePosition WorldOriginAup;
        public double CellSizeMeters;

        public void Execute(int index)
        {
            int x = index % PresampledWidth;
            int z = index / PresampledWidth;

            // Offset by -1 to sample the borders for curvature differences
            double2 world = HectonSandboxAbyssalShelfMath.ResolveSampleAupXZ(
                in WorldOriginAup,
                (x - 1) * math.max(0.001, CellSizeMeters),
                (z - 1) * math.max(0.001, CellSizeMeters),
                math.max(1.0, Parameters.AupCellSizeMeters));

            if (Parameters.MacroGeologyArtifactVersion == WorldMacroGeologyFields.ArtifactVersion)
            {
                WorldMacroGeologyParams macroParams = WorldMacroGeologyParams.CreateDefault(Parameters.Seed);
                // The AUTHORED ocean datum, not 0. Geology computes height as WaterSurfaceY - depth
            // (WorldMacroGeologyFields.cs:1370), so a datum of 0 made every depth this generator produces
            // 14.02 m shallower than the depth the running game measures against the calibrated ocean.
            //
            // WorldWaterLevelCalibrationMath.DefaultWaterLevelY is 14.02f
            // (World/Contracts/WorldWaterLevelCalibrationContracts.cs:166), and
            // Tests/Editor/WaterlineFallbackRuntimeEditTests.cs already polices that exact constant name in
            // other files - this path evaded the rule rather than being exempt from it.
            macroParams.WaterSurfaceY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
                macroParams.DetailProbeMeters = (float)math.max(1.0, CellSizeMeters);

                WorldMacroGeologyFields.MacroMasks masks;
                float height = WorldMacroGeologyFields.EvaluateHeightMeters(world.x, world.y, in macroParams, out masks);

                PresampledNodes[index] = new PresampledMacroNode
                {
                    HeightMeters = height,
                    Masks = masks
                };
            }
            else
            {
                // R99: keep the 64-bit AUP coordinate. The float cast here quantized the noise domain
                // into 6.25 cm steps at 1000 km and 50 cm steps at 8000 km — terrain.md requires 64-bit
                // AUP in this file, and the matching branch above already honors it.
                float height = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(world.x, world.y, in Parameters);
                PresampledNodes[index] = new PresampledMacroNode
                {
                    HeightMeters = height,
                    Masks = default
                };
            }
        }
    }

    /// <summary>
    /// Phase 2: Calculates slope, curvature, applies meso detail, and normalizes heights.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfDifferentialJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<PresampledMacroNode> PresampledNodes;
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;
        [WriteOnly, NoAlias] public NativeArray<float> OutputReef;
        [WriteOnly, NoAlias] public NativeArray<float> OutputLake;
        [WriteOnly, NoAlias] public NativeArray<float> OutputSteepRock;
        [WriteOnly, NoAlias] public NativeArray<float> OutputContinentality;
        [WriteOnly, NoAlias] public NativeArray<float> OutputLedge;
        [WriteOnly, NoAlias] public NativeArray<float> OutputCaveEntrance;
        [WriteOnly, NoAlias] public NativeArray<float> OutputBrinePool;
        
        public HectonSandboxAbyssalShelfParams Parameters;
        public int Width;
        public int PresampledWidth;
        public AbsoluteUniversePosition WorldOriginAup;
        public double CellSizeMeters;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            
            // The central pixel in the presampled grid is offset by +1
            int px = x + 1;
            int pz = z + 1;

            int centerIndex = pz * PresampledWidth + px;
            int westIndex = pz * PresampledWidth + (px - 1);
            int eastIndex = pz * PresampledWidth + (px + 1);
            int southIndex = (pz - 1) * PresampledWidth + px;
            int northIndex = (pz + 1) * PresampledWidth + px;

            PresampledMacroNode centerNode = PresampledNodes[centerIndex];
            float heightMeters = centerNode.HeightMeters;

            if (Parameters.MacroGeologyArtifactVersion == WorldMacroGeologyFields.ArtifactVersion)
            {
                WorldMacroGeologyParams macroParams = WorldMacroGeologyParams.CreateDefault(Parameters.Seed);
                // The AUTHORED ocean datum, not 0. Geology computes height as WaterSurfaceY - depth
            // (WorldMacroGeologyFields.cs:1370), so a datum of 0 made every depth this generator produces
            // 14.02 m shallower than the depth the running game measures against the calibrated ocean.
            //
            // WorldWaterLevelCalibrationMath.DefaultWaterLevelY is 14.02f
            // (World/Contracts/WorldWaterLevelCalibrationContracts.cs:166), and
            // Tests/Editor/WaterlineFallbackRuntimeEditTests.cs already polices that exact constant name in
            // other files - this path evaded the rule rather than being exempt from it.
            macroParams.WaterSurfaceY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
                macroParams.DetailProbeMeters = (float)math.max(1.0, CellSizeMeters);

                float west = PresampledNodes[westIndex].HeightMeters;
                float east = PresampledNodes[eastIndex].HeightMeters;
                float south = PresampledNodes[southIndex].HeightMeters;
                float north = PresampledNodes[northIndex].HeightMeters;

                float probe = macroParams.DetailProbeMeters;
                float safeProbe = math.max(0.001f, probe);
                float dx = (east - west) / (safeProbe * 2f);
                float dz = (north - south) / (safeProbe * 2f);
                float slope = Unity.Mathematics.math.sqrt(math.max(0f, dx * dx + dz * dz));
                float curvature = (west + east + south + north - heightMeters * 4f) / math.max(0.001f, safeProbe * safeProbe);

                double2 world = HectonSandboxAbyssalShelfMath.ResolveSampleAupXZ(
                    in WorldOriginAup,
                    x * CellSizeMeters,
                    z * CellSizeMeters,
                    math.max(1.0, Parameters.AupCellSizeMeters));

                float absoluteX = (float)world.x;
                float absoluteZ = (float)world.y;

                WorldMacroGeologySample macroSample = WorldMacroGeologyFields.BuildSample(
                    world.x,
                    world.y,
                    in macroParams,
                    heightMeters,
                    math.saturate(slope / 1.25f),
                    math.saturate(math.abs(curvature) * 280f),
                    math.saturate(math.max(0f, curvature) * 280f),
                    math.saturate(math.max(0f, -curvature) * 280f),
                    in centerNode.Masks);

                // Write mask channels if output arrays are bound
                if (OutputReef.IsCreated && index < OutputReef.Length)
                    OutputReef[index] = centerNode.Masks.Reef;
                if (OutputLake.IsCreated && index < OutputLake.Length)
                    OutputLake[index] = centerNode.Masks.Lake;
                if (OutputSteepRock.IsCreated && index < OutputSteepRock.Length)
                    OutputSteepRock[index] = centerNode.Masks.HardRock;
                if (OutputContinentality.IsCreated && index < OutputContinentality.Length)
                    OutputContinentality[index] = centerNode.Masks.Continentality;
                if (OutputLedge.IsCreated && index < OutputLedge.Length)
                    OutputLedge[index] = centerNode.Masks.Ledge;
                if (OutputCaveEntrance.IsCreated && index < OutputCaveEntrance.Length)
                    OutputCaveEntrance[index] = centerNode.Masks.CaveEntrance;
                if (OutputBrinePool.IsCreated && index < OutputBrinePool.Length)
                    OutputBrinePool[index] = centerNode.Masks.BrinePool;

                // R99 SEAM FIX — terrain height is now LOD-INDEPENDENT and POSITION-INDEPENDENT.
                //
                // The removed code scaled the meso amplitude by two factors that both violate terrain law:
                //   1. `borderDistFraction` — distance to the nearest CHUNK EDGE. The outer 12% of every
                //      chunk had its geology faded out, printing a lattice of flat frames across the world
                //      (on a 128-cell chunk that is a ~15-cell dead ring per side). That is precisely the
                //      "straight lines at chunk borders" signature terrain.md classifies as a seam bug, and
                //      it also made height depend on WHICH CHUNK asked, so overlapping evaluations of the
                //      same world point disagreed.
                //   2. `lodStrength` — cell size. Height that changes with LOD guarantees cracks wherever
                //      two LODs meet, and it made this job disagree with EvaluateFullHeightMeters (which
                //      always uses the full 70 m budget) — the two were never reconciled because the
                //      determinism smoke test only exercises the latter.
                //
                // Amplitude ramps are the wrong tool for LOD: height must be identical at every LOD and
                // detail must be resolved by FILTERING (octave/frequency band), never by fading geology in
                // and out by position. Budget is now the same constant the full-height path uses.
                //
                // PENDING VERIFICATION: distant-LOD shimmer from the finest meso octave (~1.5 m content at
                // cell sizes >= 2 m) is now unattenuated. If capture shows shimmer, the fix is band-limited
                // octave selection inside WorldTerrainMesoDetailFields.Evaluate — NOT a position ramp.
                WorldTerrainMesoDetailParams mesoParams = WorldTerrainMesoDetailFields.CreateDefaultParams(Parameters.Seed);
                mesoParams.MaxMesoDeltaMeters = 70f;

                WorldTerrainMesoDetailSample mesoSample = WorldTerrainMesoDetailFields.Evaluate(
                    in macroSample, absoluteX, absoluteZ, in mesoParams);

                heightMeters += mesoSample.HeightDeltaMeters;
            }

            OutputHeights01[index] = HectonSandboxAbyssalShelfMath.NormalizeHeight01(
                heightMeters,
                Parameters.LowWorldY,
                Parameters.HighWorldY);
        }
    }

    /// <summary>
    /// Quantizes shallow slopes into shelves and steep slopes into cliffs.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxSlopeQuantizationJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;
        public int Width;
        public int Height;
        public float CellSizeMeters;
        public float LowWorldY;
        public float HighWorldY;
        public float PlateauSourceGradient;
        public float PlateauTargetGradient;
        public float CliffSourceGradient;
        public float CliffRampEndGradient;
        public float CliffTargetGradient;
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
            float gradient = math.max(0.0001f, HectonSandboxAbyssalShelfMath.FastSqrtPositive(dx * dx + dz * dz));
            float average = (left + right + back + forward) * 0.25f;
            float delta = center - average;

            float targetGradient = math.max(0.0001f, PlateauTargetGradient);
            float plateauSourceGradient = math.max(targetGradient + 0.0001f, PlateauSourceGradient);
            float cliffSourceGradient = math.max(plateauSourceGradient + 0.0001f, CliffSourceGradient);
            float cliffRampEndGradient = math.max(cliffSourceGradient + 0.0001f, CliffRampEndGradient);
            float cliffTargetGradient = math.max(0.0001f, CliffTargetGradient);
            float plateauMask = 1f - math.smoothstep(targetGradient, plateauSourceGradient, gradient);
            float cliffMask = math.smoothstep(cliffSourceGradient, cliffRampEndGradient, gradient);
            float resolvedTargetGradient = math.lerp(gradient, targetGradient, plateauMask);
            resolvedTargetGradient = math.lerp(resolvedTargetGradient, cliffTargetGradient, cliffMask);
            float targetFactor = resolvedTargetGradient / gradient;
            float quantizeStrength = math.saturate(Strength);

            float adjustMask = math.saturate(math.max(plateauMask, cliffMask));
            float factor = math.lerp(1f, targetFactor, adjustMask * quantizeStrength);
            factor = math.clamp(factor, 0.2f, 3.5f);

            float resolved = average + delta * factor;
            OutputHeights01[index] = SoftClamp01(resolved / heightRange);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SoftClamp01(float normalizedHeight)
        {
            if (normalizedHeight <= 0.05f)
            {
                float t = math.saturate(normalizedHeight / 0.05f);
                return 0.05f * t * t;
            }
            if (normalizedHeight >= 0.95f)
            {
                float excess = normalizedHeight - 0.95f;
                float compressedExcess = 0.05f * (1f - math.exp(-excess * 20f));
                return 0.95f + compressedExcess;
            }
            return math.saturate(normalizedHeight);
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
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfSmokeSampleJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> PositionsAup;
        [WriteOnly, NoAlias] public NativeArray<HectonSandboxAbyssalShelfAuditSample> OutputSamples;
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
            float center = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters(position.x, position.y, in Parameters);
            float neighborX = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters(position.x + probe, position.y, in Parameters);
            float neighborZ = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters(position.x, position.y + probe, in Parameters);
            float dx = (neighborX - center) / (float)probe;
            float dz = (neighborZ - center) / (float)probe;
            float gradient = HectonSandboxAbyssalShelfMath.FastSqrtPositive(dx * dx + dz * dz);
            float slopeAngle = HectonSandboxAbyssalShelfMath.FastAtanDegreesPositive(gradient);
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
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfSmokeReductionJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<HectonSandboxAbyssalShelfAuditSample> Samples;
        [WriteOnly, NoAlias] public NativeArray<HectonSandboxAbyssalShelfSampleReduction> Reductions;

        public void Execute(int index)
        {
            HectonSandboxAbyssalShelfAuditSample sample = Samples[index];
            bool activeSlope = sample.SlopeAngleDegrees > 15f && sample.SlopeAngleDegrees < 45f;
            Reductions[index] = new HectonSandboxAbyssalShelfSampleReduction
            {
                InvalidSampleCount = (sample.Flags & 0x03) != 0 ? 1 : 0,
                CliffSampleCount = (sample.Flags & 0x04) != 0 ? 1 : 0,
                PlateauSampleCount = (sample.Flags & 0x08) != 0 ? 1 : 0,
                MinHeightMeters = sample.HeightMeters,
                MaxHeightMeters = sample.HeightMeters,
                MaxSlopeDegrees = sample.SlopeAngleDegrees,
                SlopeAngleSumDegrees = sample.SlopeAngleDegrees,
                ActiveSlopeAngleSumDegrees = activeSlope ? sample.SlopeAngleDegrees : 0f,
                ActiveSlopeMinDegrees = activeSlope ? sample.SlopeAngleDegrees : float.MaxValue,
                ActiveSlopeMaxDegrees = activeSlope ? sample.SlopeAngleDegrees : float.MinValue,
                Slope30SampleCount = (sample.Flags & 0x10) != 0 ? 1 : 0,
                ActiveSlopeSampleCount = activeSlope ? 1 : 0
            };
        }
    }

    /// <summary>
    /// Final cold-path smoke summary reduction. Runs under Burst after the parallel sample pass.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HectonSandboxAbyssalShelfSmokeSummaryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<HectonSandboxAbyssalShelfSampleReduction> Reductions;
        [WriteOnly, NoAlias] public NativeArray<HectonSandboxAbyssalShelfSmokeSummary> Summary;

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
            float activeSlopeMin = float.MaxValue;
            float activeSlopeMax = float.MinValue;
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
                activeSlopeMin = math.min(activeSlopeMin, reduction.ActiveSlopeMinDegrees);
                activeSlopeMax = math.max(activeSlopeMax, reduction.ActiveSlopeMaxDegrees);
                slope30Count += reduction.Slope30SampleCount;
                activeSlopeCount += reduction.ActiveSlopeSampleCount;
            }

            double cellSize = math.max(1.0, Parameters.AupCellSizeMeters);
            AbsoluteUniversePosition shiftedAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                100125.0,
                -99625.0,
                cellSize);
            double2 shiftedAupXZ = HectonSandboxAbyssalShelfMath.ResolveSampleAupXZ(in shiftedAup, 0.0, 0.0, cellSize);
            float shiftedA = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters(100125.0, -99625.0, in Parameters);
            float shiftedB = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters(shiftedAupXZ.x, shiftedAupXZ.y, in Parameters);
            float aupDelta = math.abs(shiftedA - shiftedB);
            double boundaryProbe = math.max(0.001, AupBoundaryProbeMeters);
            float boundaryLeft = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters((cellSize - boundaryProbe), 375.125, in Parameters);
            float boundaryRight = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters((cellSize + boundaryProbe), 375.125, in Parameters);
            float boundaryDelta = math.abs(boundaryLeft - boundaryRight);
            double farOrigin = FarChunkOriginMeters;
            AbsoluteUniversePosition highChunkAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                farOrigin + 125.0,
                farOrigin + 375.0,
                cellSize);
            double2 highChunkAupXZ = HectonSandboxAbyssalShelfMath.ResolveSampleAupXZ(in highChunkAup, 0.0, 0.0, cellSize);
            float highChunkDirect = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters(
                farOrigin + 125.0,
                farOrigin + 375.0,
                in Parameters);
            float highChunkAupHeight = HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters(highChunkAupXZ.x, highChunkAupXZ.y, in Parameters);
            float highChunkDelta = math.abs(highChunkDirect - highChunkAupHeight);
            int chunkResolution = math.max(2, ChunkAuditResolution);
            double chunkSize = math.max(1.0, ChunkAuditSizeMeters);
            int originChunkInvalid = CountInvalidChunk(0.0, 0.0, chunkResolution, chunkSize, in Parameters);
            int farChunkInvalid = CountInvalidChunk(farOrigin, farOrigin, chunkResolution, chunkSize, in Parameters);
            float averageSlope = slopeSum / math.max(1, sampleCount);
            float averageActiveSlope = activeSlopeSum / math.max(1, activeSlopeCount);
            float resolvedActiveSlopeMin = activeSlopeCount > 0 ? activeSlopeMin : 0f;
            float resolvedActiveSlopeMax = activeSlopeCount > 0 ? activeSlopeMax : 0f;
            bool passed =
                sampleCount == RequiredSampleCount &&
                invalidCount == 0 &&
                plateauCount > 0 &&
                slope30Count > 0 &&
                activeSlopeCount > 0 &&
                minHeight <= RequiredMinHeightMeters &&
                maxHeight >= RequiredMaxHeightMeters &&
                maxSlope <= MaxAllowedSlopeDegrees &&
                resolvedActiveSlopeMin >= 15f &&
                resolvedActiveSlopeMax <= 45f &&
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
                ActiveSlopeMinDegrees = resolvedActiveSlopeMin,
                ActiveSlopeMaxDegrees = resolvedActiveSlopeMax,
                ActiveSlopeSampleCount = activeSlopeCount,
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
