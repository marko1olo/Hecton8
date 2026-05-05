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
        public uint Seed;
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
            double2 aupXZ = ResolveAupAlignedXZ(
                new double2(absoluteX, absoluteZ),
                math.max(1.0, parameters.AupCellSizeMeters));

            float heightRange = math.max(0.001f, parameters.HighWorldY - parameters.LowWorldY);
            float macro01 = EvaluateGreatDescent01(aupXZ, parameters.DescentRadiusMeters);
            float baseY = math.lerp(parameters.HighWorldY, parameters.LowWorldY, macro01);
            float base01 = math.saturate((baseY - parameters.LowWorldY) / heightRange);

            float ridgeMask = EvaluateVoronoiRidgeMask(aupXZ, in parameters);
            float ridgeLift01 = math.saturate(parameters.RidgeHeightMeters / heightRange) * ridgeMask;
            float multiplied01 = base01 * (1f + math.max(0f, parameters.RidgeMultiplier) * ridgeMask);
            float ridged01 = math.saturate(multiplied01 + ridgeLift01);

            return parameters.LowWorldY + ridged01 * heightRange;
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
        private static double2 ResolveAupAlignedXZ(double2 absoluteXZ, double cellSizeMeters)
        {
            long gridX = (long)math.floor(absoluteXZ.x / cellSizeMeters);
            long gridZ = (long)math.floor(absoluteXZ.y / cellSizeMeters);
            double localX = absoluteXZ.x - gridX * cellSizeMeters;
            double localZ = absoluteXZ.y - gridZ * cellSizeMeters;
            return new double2(gridX * cellSizeMeters + localX, gridZ * cellSizeMeters + localZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateGreatDescent01(double2 aupXZ, double descentRadiusMeters)
        {
            double radius = math.sqrt(aupXZ.x * aupXZ.x + aupXZ.y * aupXZ.y);
            double t = math.saturate(radius / math.max(1.0, descentRadiusMeters));
            return (float)(t * t * (3.0 - 2.0 * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateVoronoiRidgeMask(
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
            float edgeMask = 1f - math.smoothstep(edgeWidth * 0.25f, edgeWidth, edgeDeltaMeters);
            float junctionMask = 1f - math.smoothstep(junctionWidth * 0.35f, junctionWidth, junctionDeltaMeters);
            float irregularity = math.lerp(0.82f, 1.18f, HashToUnitFloat(nearestHash ^ 0xA24BAED5u));
            float branched = math.saturate(edgeMask + junctionMask * 0.55f);

            return math.saturate(branched * irregularity);
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
            float warpX = FractalValueNoise(sample, parameters.Seed ^ 0x5F356495u) * 2f - 1f;
            float warpZ = FractalValueNoise(sample + new float2(17.317f, -41.113f), parameters.Seed ^ 0xC2B2AE35u) * 2f - 1f;
            return new double2(warpX * amplitude, warpZ * amplitude);
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
        public double2 WorldOriginXZ;
        public double CellSizeMeters;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            double2 world = WorldOriginXZ + new double2(x, z) * math.max(0.001, CellSizeMeters);
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

            float plateauMask = 1f - math.smoothstep(
                math.max(0f, PlateauTargetAngleDegrees),
                math.max(PlateauTargetAngleDegrees + 0.001f, PlateauSourceAngleDegrees),
                angle);
            float cliffMask = math.smoothstep(
                math.max(0f, CliffSourceAngleDegrees),
                math.max(CliffSourceAngleDegrees + 0.001f, CliffTargetAngleDegrees),
                angle);

            float plateauGradient = math.tan(math.radians(math.clamp(PlateauTargetAngleDegrees, 0.1f, 89f)));
            float cliffGradient = math.tan(math.radians(math.clamp(CliffTargetAngleDegrees, 1f, 89f)));
            float plateauFactor = plateauGradient / gradient;
            float cliffFactor = cliffGradient / gradient;
            float factor = 1f;
            float quantizeStrength = math.saturate(Strength);

            factor = math.lerp(factor, plateauFactor, plateauMask * quantizeStrength);
            factor = math.lerp(factor, cliffFactor, cliffMask * quantizeStrength);
            factor = math.clamp(factor, 0.02f, 8f);

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
        [ReadOnly] public NativeArray<double2> PositionsAupXZ;
        [WriteOnly] public NativeArray<HectonSandboxAbyssalShelfAuditSample> OutputSamples;
        public HectonSandboxAbyssalShelfParams Parameters;
        public double SlopeProbeMeters;

        public void Execute(int index)
        {
            double2 position = PositionsAupXZ[index];
            double probe = math.max(0.001, SlopeProbeMeters);
            float center = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(position.x, position.y, in Parameters);
            float neighborX = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(position.x + probe, position.y, in Parameters);
            float neighborZ = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(position.x, position.y + probe, in Parameters);
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
}
