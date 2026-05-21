using Hecton8.World.OfflineHadalTrenchBaker;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockTrenchJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<float> Densities;
        [ReadOnly] public HadalTrenchBakeConfigDTO Config;

        public void Execute(int index)
        {
            float* densityPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Densities);
            ref float density = ref UnsafeUtility.AsRef<float>(densityPtr + index);
            int3 voxel = HadalTrenchJobMath.Unflatten(index, Config.Resolution);
            double3 sampleAUP = HadalTrenchJobMath.ResolveSampleAUP(voxel, Config);
            double lengthMeters = math.max(1.0d, (double)(Config.Resolution.x - 1) * Config.VoxelSizeMeters);
            double3 start = Config.SectorOriginAUP + new double3(lengthMeters * 0.12d, 0.0d, lengthMeters * 0.18d);
            double3 end = Config.SectorOriginAUP + new double3(lengthMeters * 0.88d, 0.0d, lengthMeters * 0.82d);
            float sineOffset = math.sin((float)(sampleAUP.x * 0.004d) + Config.Seed * 0.00013f) * Config.DefaultWidthMeters * 0.35f;
            FaultLineParamsDTO fault = new FaultLineParamsDTO
            {
                StartAUP = start,
                EndAUP = end + new double3(0.0d, 0.0d, sineOffset),
                Depth = math.max(Config.DefaultDepthMeters, Config.VoxelSizeMeters * 8f),
                Width = math.max(Config.DefaultWidthMeters, Config.VoxelSizeMeters * 4f),
                NoiseIntensity = math.max(Config.NoiseIntensity, 0f),
                _pad0 = 0u
            };

            float baseDensity = -64f;
            float voidSdf = HadalTrenchJobMath.EvaluateTrenchVoidSdf(sampleAUP, in fault, Config);
            density = math.max(baseDensity, -voidSdf);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateTectonicNetworkJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<FaultLineParamsDTO> Faults;
        [ReadOnly] public HadalTrenchBakeConfigDTO Config;

        public void Execute(int index)
        {
            int gridX = math.max(1, Config.FaultGridX);
            int gridZ = math.max(1, Config.FaultGridZ);
            int x = index % gridX;
            int z = index / gridX;
            if (z >= gridZ)
                return;

            int baseIndex = index << 1;
            if (baseIndex + 1 >= Faults.Length)
                return;

            double2 seed = ResolveFeaturePoint(x, z, Config);
            double2 seedRight = ResolveFeaturePoint(x + 1, z, Config);
            double2 seedUp = ResolveFeaturePoint(x, z + 1, Config);
            FaultLineParamsDTO* faultPtr = (FaultLineParamsDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Faults);
            UnsafeUtility.AsRef<FaultLineParamsDTO>(faultPtr + baseIndex) = BuildVoronoiEdge(seed, seedRight, x, z, Config, 0u);
            UnsafeUtility.AsRef<FaultLineParamsDTO>(faultPtr + baseIndex + 1) = BuildVoronoiEdge(seed, seedUp, x, z, Config, 1u);
        }

        private static double2 ResolveFeaturePoint(int cellX, int cellZ, HadalTrenchBakeConfigDTO config)
        {
            float jitterX = HadalTrenchBakeMath.Hash01(cellX, 0, cellZ, config.Seed ^ 0x8B7Du);
            float jitterZ = HadalTrenchBakeMath.Hash01(cellX, 1, cellZ, config.Seed ^ 0xC5A3u);
            double cell = math.max(1.0d, config.VoronoiCellSizeMeters);
            double x = config.WorldMinAUP.x + (cellX + 0.18d + jitterX * 0.64d) * cell;
            double z = config.WorldMinAUP.z + (cellZ + 0.18d + jitterZ * 0.64d) * cell;
            return new double2(x, z);
        }

        private static FaultLineParamsDTO BuildVoronoiEdge(double2 a, double2 b, int cellX, int cellZ, HadalTrenchBakeConfigDTO config, uint axis)
        {
            double2 delta = b - a;
            double length = math.max(0.001d, math.sqrt(delta.x * delta.x + delta.y * delta.y));
            double2 normal = new double2(-delta.y / length, delta.x / length);
            double edgeHalfLength = math.max(1.0d, config.VoronoiCellSizeMeters * 0.46d);
            double2 midpoint = (a + b) * 0.5d;
            float q = math.saturate(config.GlobalQualityWeight);
            float width = math.max(config.DefaultWidthMeters, config.VoxelSizeMeters * 4f) * math.lerp(0.82f, 1.18f, q);
            float depth = math.max(config.DefaultDepthMeters, config.VoxelSizeMeters * 16f) * math.lerp(0.85f, 1.12f, q);
            float noise = math.max(config.NoiseIntensity, 0f) * (0.75f + HadalTrenchBakeMath.Hash01(cellX, (int)axis, cellZ, config.Seed ^ 0xA17Fu) * 0.5f);
            double2 start = midpoint - normal * edgeHalfLength;
            double2 end = midpoint + normal * edgeHalfLength;
            return new FaultLineParamsDTO
            {
                StartAUP = new double3(start.x, config.SeaFloorAUPY, start.y),
                EndAUP = new double3(end.x, config.SeaFloorAUPY, end.y),
                Depth = depth,
                Width = width,
                NoiseIntensity = noise,
                _pad0 = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ExecuteTrenchSubtractionJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<float> Densities;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<float> ExcavatedMeters3;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<byte> NonFiniteFlags;
        [ReadOnly] [NoAlias] public NativeArray<FaultLineParamsDTO> Faults;
        [ReadOnly] public HadalTrenchBakeConfigDTO Config;

        public void Execute(int index)
        {
            float* densityPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Densities);
            float* volumePtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ExcavatedMeters3);
            byte* finitePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(NonFiniteFlags);
            ref float density = ref UnsafeUtility.AsRef<float>(densityPtr + index);
            ref float excavated = ref UnsafeUtility.AsRef<float>(volumePtr + index);
            ref byte nonFinite = ref UnsafeUtility.AsRef<byte>(finitePtr + index);
            int3 voxel = HadalTrenchJobMath.Unflatten(index, Config.Resolution);
            double3 sampleAUP = HadalTrenchJobMath.ResolveSampleAUP(voxel, Config);
            float before = density;
            float result = before;
            int faultCount = math.min(math.max(Config.FaultCount, 0), Faults.Length);
            FaultLineParamsDTO* faultPtr = faultCount > 0 ? (FaultLineParamsDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Faults) : null;

            for (int i = 0; i < faultCount; i++)
            {
                ref FaultLineParamsDTO fault = ref UnsafeUtility.AsRef<FaultLineParamsDTO>(faultPtr + i);
                float outsideLowerBound = HadalTrenchJobMath.EvaluateTrenchOutsideLowerBound(sampleAUP, in fault, Config);
                if (outsideLowerBound > -result)
                    continue;

                float voidSdf = HadalTrenchJobMath.EvaluateTrenchVoidSdf(sampleAUP, in fault, Config);
                result = math.max(result, -voidSdf);
            }

            bool finite = math.isfinite(result);
            density = finite ? result : 127f;
            float voxelVolume = Config.VoxelSizeMeters * Config.VoxelSizeMeters * Config.VoxelSizeMeters;
            excavated = before < 0f && density > 0f ? voxelVolume : 0f;
            nonFinite = finite ? (byte)0 : (byte)1;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct QuantizeTrenchDensityJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<float> Densities;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<sbyte> Quantized;

        public void Execute(int index)
        {
            float* densityPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Densities);
            sbyte* quantizedPtr = (sbyte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Quantized);
            float density = UnsafeUtility.AsRef<float>(densityPtr + index);
            UnsafeUtility.AsRef<sbyte>(quantizedPtr + index) = HadalTrenchBakeMath.QuantizeDensity(density);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BuildTrenchAdaptiveBlocksJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<sbyte> Quantized;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<HadalTrenchAdaptiveBlockDTO> Blocks;
        [ReadOnly] public HadalTrenchBakeConfigDTO Config;
        [ReadOnly] public int BlockSize;
        [ReadOnly] public int3 BlockGrid;

        public void Execute(int index)
        {
            int3 blockCoord = HadalTrenchJobMath.Unflatten(index, BlockGrid);
            int3 minVoxel = blockCoord * BlockSize;
            int3 maxVoxel = math.min(minVoxel + new int3(BlockSize), Config.Resolution);
            sbyte minDensity = 127;
            sbyte maxDensity = -127;
            uint count = 0u;
            uint hash = 2166136261u;
            sbyte* quantizedPtr = (sbyte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Quantized);

            for (int z = minVoxel.z; z < maxVoxel.z; z++)
            {
                for (int y = minVoxel.y; y < maxVoxel.y; y++)
                {
                    int row = z * Config.Resolution.x * Config.Resolution.y + y * Config.Resolution.x;
                    for (int x = minVoxel.x; x < maxVoxel.x; x++)
                    {
                        sbyte density = UnsafeUtility.AsRef<sbyte>(quantizedPtr + row + x);
                        minDensity = (sbyte)math.min(minDensity, density);
                        maxDensity = (sbyte)math.max(maxDensity, density);
                        hash = HadalTrenchBakeMath.Mix(hash ^ (byte)density);
                        count++;
                    }
                }
            }

            byte flags = (byte)(minDensity == maxDensity ? 1 : 0);
            float error = math.abs((float)maxDensity - minDensity) * Config.VoxelSizeMeters;
            Blocks[index] = new HadalTrenchAdaptiveBlockDTO
            {
                MinVoxel = minVoxel,
                BlockSizeVoxels = (byte)math.clamp(BlockSize, 1, 255),
                MinDensity = minDensity,
                MaxDensity = maxDensity,
                Flags = flags,
                VoxelCount = count,
                ErrorMeters = error,
                StateHash = hash,
                _pad0 = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateThermalVentNodesJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<FaultLineParamsDTO> Faults;
        [NativeDisableParallelForRestriction] [NoAlias] public NativeArray<ThermalVentSpawnDTO> Vents;
        [ReadOnly] public HadalTrenchBakeConfigDTO Config;

        public void Execute(int index)
        {
            if (index >= Faults.Length || index >= Vents.Length)
                return;

            FaultLineParamsDTO* faultPtr = (FaultLineParamsDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Faults);
            ref FaultLineParamsDTO fault = ref UnsafeUtility.AsRef<FaultLineParamsDTO>(faultPtr + index);
            double3 midpoint = (fault.StartAUP + fault.EndAUP) * 0.5d;
            float q = math.saturate(Config.GlobalQualityWeight);
            double3 ventAUP = new double3(midpoint.x, Config.SeaFloorAUPY - math.max(1f, fault.Depth), midpoint.z);
            Vents[index] = new ThermalVentSpawnDTO
            {
                PositionAUP = ventAUP,
                RadiusMeters = math.lerp(3.5f, 11f, q),
                HeatCelsius = math.lerp(260f, 475f, q),
                PressureKPa = math.lerp(22000f, 52000f, q),
                LootAffinity01 = math.saturate(0.35f + fault.Depth * 0.00008f),
                FaultHash = HadalTrenchBakeMath.HashFault(in fault),
                Flags = 1u,
                _pad0 = 0ul,
                _pad1 = 0ul
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RleCompressTrenchDensityJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<sbyte> Quantized;
        [NoAlias] public NativeList<HadalTrenchRleRunDTO> Runs;

        public void Execute()
        {
            Runs.Clear();
            int count = Quantized.Length;
            if (count <= 0)
                return;

            sbyte* qPtr = (sbyte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Quantized);
            sbyte current = UnsafeUtility.AsRef<sbyte>(qPtr);
            uint start = 0u;
            uint length = 1u;

            for (int i = 1; i < count; i++)
            {
                sbyte next = UnsafeUtility.AsRef<sbyte>(qPtr + i);
                if (next == current && length < uint.MaxValue)
                {
                    length++;
                    continue;
                }

                Runs.AddNoResize(new HadalTrenchRleRunDTO
                {
                    StartVoxel = start,
                    RunLength = length,
                    Density = current,
                    MaterialId = current < 0 ? (byte)1 : (byte)0,
                    Flags = 0,
                    _pad0 = 0u
                });
                current = next;
                start = (uint)i;
                length = 1u;
            }

            Runs.AddNoResize(new HadalTrenchRleRunDTO
            {
                StartVoxel = start,
                RunLength = length,
                Density = current,
                MaterialId = current < 0 ? (byte)1 : (byte)0,
                Flags = 0,
                _pad0 = 0u
            });
        }
    }

    internal static class HadalTrenchJobMath
    {
        public static int3 Unflatten(int index, int3 resolution)
        {
            int xy = math.max(1, resolution.x * resolution.y);
            int z = index / xy;
            int rem = index - z * xy;
            int y = rem / math.max(1, resolution.x);
            int x = rem - y * math.max(1, resolution.x);
            return new int3(x, y, z);
        }

        public static double3 ResolveSampleAUP(int3 voxel, HadalTrenchBakeConfigDTO config)
        {
            double voxelSize = math.max(0.001d, config.VoxelSizeMeters);
            return config.SectorOriginAUP + new double3(voxel.x * voxelSize, voxel.y * voxelSize, voxel.z * voxelSize);
        }

        public static float EvaluateTrenchVoidSdf(double3 sampleAUP, in FaultLineParamsDTO fault, HadalTrenchBakeConfigDTO config)
        {
            float lateral = DistanceToFaultSegmentXZ(sampleAUP, in fault, out float along01);
            float depth = math.max(fault.Depth, config.VoxelSizeMeters * 4f);
            float width = math.max(fault.Width, config.VoxelSizeMeters * 2f);
            float belowSeaFloor = (float)(config.SeaFloorAUPY - sampleAUP.y);
            float insideVertical = math.max(-belowSeaFloor, belowSeaFloor - depth);
            float depth01 = math.saturate(belowSeaFloor / math.max(0.001f, depth));
            float wallTightness = math.lerp(0.72f, 0.34f, depth01);
            float q = math.saturate(config.GlobalQualityWeight);
            float uBlend = math.lerp(0.78f, 0.92f, q);
            float effectiveWidth = width * math.lerp(wallTightness, 0.82f, uBlend * (1f - depth01 * 0.35f));
            float ridge = RidgedMultifractal(sampleAUP, config.Seed ^ HadalTrenchBakeMath.HashFault(in fault), math.max(config.NoiseFrequency, 0.0001f));
            float longitudinalPulse = math.sin((along01 * 34.0f) + ridge * 2.3f) * 0.5f + 0.5f;
            float roughness = (ridge * 2f - 1f) * math.max(0f, fault.NoiseIntensity) * math.lerp(0.18f, 0.46f, q);
            float lateralSdf = lateral - effectiveWidth - roughness - longitudinalPulse * fault.NoiseIntensity * 0.08f;
            return math.max(lateralSdf, insideVertical);
        }

        public static float EvaluateTrenchOutsideLowerBound(double3 sampleAUP, in FaultLineParamsDTO fault, HadalTrenchBakeConfigDTO config)
        {
            float ignoredAlong01;
            float lateral = DistanceToFaultSegmentXZ(sampleAUP, in fault, out ignoredAlong01);
            float depth = math.max(fault.Depth, config.VoxelSizeMeters * 4f);
            float width = math.max(fault.Width, config.VoxelSizeMeters * 2f);
            float belowSeaFloor = (float)(config.SeaFloorAUPY - sampleAUP.y);
            float insideVertical = math.max(-belowSeaFloor, belowSeaFloor - depth);
            float depth01 = math.saturate(belowSeaFloor / math.max(0.001f, depth));
            float wallTightness = math.lerp(0.72f, 0.34f, depth01);
            float q = math.saturate(config.GlobalQualityWeight);
            float uBlend = math.lerp(0.78f, 0.92f, q);
            float effectiveWidth = width * math.lerp(wallTightness, 0.82f, uBlend * (1f - depth01 * 0.35f));
            float roughnessBound = math.max(0f, fault.NoiseIntensity) * 0.54f;
            float lateralLowerBound = lateral - effectiveWidth - roughnessBound;
            return math.max(lateralLowerBound, insideVertical);
        }

        private static float DistanceToFaultSegmentXZ(double3 sampleAUP, in FaultLineParamsDTO fault, out float along01)
        {
            double2 sample = new double2(sampleAUP.x, sampleAUP.z);
            double2 start = new double2(fault.StartAUP.x, fault.StartAUP.z);
            double2 end = new double2(fault.EndAUP.x, fault.EndAUP.z);
            double2 ab = end - start;
            double2 ap = sample - start;
            double lenSq = math.max(0.000001d, ab.x * ab.x + ab.y * ab.y);
            double t = math.clamp((ap.x * ab.x + ap.y * ab.y) / lenSq, 0.0d, 1.0d);
            double2 closest = start + ab * t;
            double2 delta = sample - closest;
            along01 = (float)t;
            return (float)math.sqrt(delta.x * delta.x + delta.y * delta.y);
        }

        private static float RidgedMultifractal(double3 sampleAUP, uint seed, float baseFrequency)
        {
            float sum = 0f;
            float amplitude = 0.58f;
            float frequency = math.max(0.00001f, baseFrequency);
            for (int octave = 0; octave < 4; octave++)
            {
                float value = ValueNoise3(sampleAUP, frequency, seed + (uint)(octave * 92821));
                float ridge = 1f - math.abs(value * 2f - 1f);
                sum += ridge * ridge * amplitude;
                frequency *= 2.07f;
                amplitude *= 0.52f;
            }

            return math.saturate(sum);
        }

        private static float ValueNoise3(double3 sampleAUP, float frequency, uint seed)
        {
            double sx = sampleAUP.x * frequency;
            double sy = sampleAUP.y * frequency;
            double sz = sampleAUP.z * frequency;
            int ix = (int)math.floor(sx);
            int iy = (int)math.floor(sy);
            int iz = (int)math.floor(sz);
            float fx = (float)(sx - ix);
            float fy = (float)(sy - iy);
            float fz = (float)(sz - iz);
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float v000 = HadalTrenchBakeMath.Hash01(ix, iy, iz, seed);
            float v100 = HadalTrenchBakeMath.Hash01(ix + 1, iy, iz, seed);
            float v010 = HadalTrenchBakeMath.Hash01(ix, iy + 1, iz, seed);
            float v110 = HadalTrenchBakeMath.Hash01(ix + 1, iy + 1, iz, seed);
            float v001 = HadalTrenchBakeMath.Hash01(ix, iy, iz + 1, seed);
            float v101 = HadalTrenchBakeMath.Hash01(ix + 1, iy, iz + 1, seed);
            float v011 = HadalTrenchBakeMath.Hash01(ix, iy + 1, iz + 1, seed);
            float v111 = HadalTrenchBakeMath.Hash01(ix + 1, iy + 1, iz + 1, seed);
            float x00 = math.lerp(v000, v100, fx);
            float x10 = math.lerp(v010, v110, fx);
            float x01 = math.lerp(v001, v101, fx);
            float x11 = math.lerp(v011, v111, fx);
            float y0 = math.lerp(x00, x10, fy);
            float y1 = math.lerp(x01, x11, fy);
            return math.lerp(y0, y1, fz);
        }
    }
}
