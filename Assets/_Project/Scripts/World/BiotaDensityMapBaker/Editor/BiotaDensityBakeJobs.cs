#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.BiotaDensityMapBaker.Editor
{
    public static class BiotaDensityBakeConstants
    {
        public const int DefaultResolution = 512;
        public const int PreviewResolution = 256;
        public const int MinimumPreviewResolution = 96;
        public const int MaxResolution = 4096;
        public const int MaxRuleCount = 64;
        public const int DefaultRuleCount = 5;
        public const int DefaultLayerCount = 4;
        public const int MaxLayerCount = 8;
        public const int TelemetryFrames = 300;
        public const int HeaderSizeBytes = 128;
        public const uint FileMagic = 0x44423848u; // H8BD little-endian
        public const uint DumpMagic = 0x44384242u; // BB8D little-endian
        public const uint FileVersion = 1u;
        public const uint EndianTag = 0x01020304u;
        public const uint RollbackExcludedFlag = 1u;
        public const uint WarningNonFiniteDensity = 1u << 1;
        public const uint WarningRleExpanded = 1u << 2;
        public const uint WarningScannerIncomplete = 1u << 3;
        public const float DefaultCellSizeMeters = 4f;
        public const float DefaultThermalFalloffMeters = 420f;
        public const float DefaultNoiseFrequency = 0.004f;
        public const float DefaultNoiseOffset = 0.58f;
        public const float DefaultDensityMultiplier = 1f;

        public const uint BiomeAny = 0xFFFFFFFFu;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BiotaSpawnRuleDTO
    {
        [FieldOffset(0)] public float MinDepth;
        [FieldOffset(4)] public float MaxDepth;
        [FieldOffset(8)] public float MinSlope;
        [FieldOffset(12)] public float MaxSlope;
        [FieldOffset(16)] public uint RequiredBiomeHash;
        [FieldOffset(20)] public float PreferredTemperature;
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
    public struct BiotaRuleWeightDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float SpawnWeight;
        [FieldOffset(8)] public float TemperatureTolerance;
        [FieldOffset(12)] public float SiltAffinity;
        [FieldOffset(16)] public float ThermalAffinity;
        [FieldOffset(20)] public uint LayerIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BiotaDensityBakeConfigDTO
    {
        [FieldOffset(0)] public double3 SectorOriginAUP;
        [FieldOffset(24)] public int Width;
        [FieldOffset(28)] public int Height;
        [FieldOffset(32)] public int LayerCount;
        [FieldOffset(36)] public uint Seed;
        [FieldOffset(40)] public float CellSizeMeters;
        [FieldOffset(44)] public float NoiseFrequency;
        [FieldOffset(48)] public float NoiseOffset;
        [FieldOffset(52)] public float GlobalDensityMultiplier;
        [FieldOffset(56)] public float ThermalFalloffMeters;
        [FieldOffset(60)] public float BaseTemperatureCelsius;
        [FieldOffset(64)] public float DepthScaleMeters;
        [FieldOffset(68)] public float SlopeSoftnessDegrees;
        [FieldOffset(72)] public float TemperatureSoftnessCelsius;
        [FieldOffset(76)] public float GlobalQualityWeight;
        [FieldOffset(80)] public uint Flags;
        [FieldOffset(84)] public uint EdgeSampleFlags;
        [FieldOffset(88)] public uint RuleCount;
        [FieldOffset(92)] public uint VentCount;
        [FieldOffset(96)] public ulong _pad0;
        [FieldOffset(104)] public ulong _pad1;
        [FieldOffset(112)] public ulong _pad2;
        [FieldOffset(120)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BiotaThermalVentDTO
    {
        [FieldOffset(0)] public double X;
        [FieldOffset(8)] public double Z;
        [FieldOffset(16)] public float HeatCelsius;
        [FieldOffset(20)] public float RadiusMeters;
        [FieldOffset(24)] public uint VentHash;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct BiotaDensityRleRunDTO
    {
        [FieldOffset(0)] public uint Count;
        [FieldOffset(4)] public byte Value;
        [FieldOffset(5)] public byte Layer;
        [FieldOffset(6)] public ushort _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiotaDensityBakeTelemetryEntry
    {
        [FieldOffset(0)] public uint Stage;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint WarningFlags;
        [FieldOffset(12)] public uint RawByteCount;
        [FieldOffset(16)] public double SectorOriginX;
        [FieldOffset(24)] public double SectorOriginY;
        [FieldOffset(32)] public double SectorOriginZ;
        [FieldOffset(40)] public int Width;
        [FieldOffset(44)] public int Height;
        [FieldOffset(48)] public int LayerCount;
        [FieldOffset(52)] public int NonFiniteCount;
        [FieldOffset(56)] public int RleRunCount;
        [FieldOffset(60)] public uint BiomassByteSum;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockTerrainDataJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> DepthMeters;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Silt01;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> BiomeHashes;
        [ReadOnly] public BiotaDensityBakeConfigDTO Config;

        public void Execute(int index)
        {
            int width = math.max(1, Config.Width);
            int height = math.max(1, Config.Height);
            if ((uint)index >= (uint)(width * height))
                return;

            int x = index % width;
            int z = index / width;
            BiotaDensityBakeMath.SampleMockTerrain(in Config, x, z, out float depth, out float silt, out float ventBasin, out float cliff);

            uint biome = ResolveMockBiomeHash(depth, silt, ventBasin, cliff);
            float* depthPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(DepthMeters);
            float* siltPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Silt01);
            uint* biomePtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(BiomeHashes);
            UnsafeUtility.AsRef<float>(depthPtr + index) = depth;
            UnsafeUtility.AsRef<float>(siltPtr + index) = silt;
            UnsafeUtility.AsRef<uint>(biomePtr + index) = biome;
        }

        private static uint ResolveMockBiomeHash(float depth, float silt, float ventBasin, float cliff)
        {
            if (ventBasin > 0.42f)
                return 0x56454E54u; // VENT
            if (silt > 0.58f)
                return 0x53494C54u; // SILT
            if (depth > 2200f || cliff > 0.72f)
                return 0x4841444Cu; // HADL
            return 0x52454546u; // REEF
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockTerrainEdgeDepthJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> EdgeDepthMeters;
        [ReadOnly] public BiotaDensityBakeConfigDTO Config;
        [ReadOnly] public int Side;

        public void Execute(int index)
        {
            int width = math.max(1, Config.Width);
            int height = math.max(1, Config.Height);
            int x = index;
            int z = index;
            if (Side == 0)
            {
                x = -1;
                z = math.clamp(index, 0, height - 1);
            }
            else if (Side == 1)
            {
                x = width;
                z = math.clamp(index, 0, height - 1);
            }
            else if (Side == 2)
            {
                x = math.clamp(index, 0, width - 1);
                z = -1;
            }
            else
            {
                x = math.clamp(index, 0, width - 1);
                z = height;
            }

            BiotaDensityBakeMath.SampleMockTerrain(in Config, x, z, out float depth, out _, out _, out _);
            float* edgePtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(EdgeDepthMeters);
            UnsafeUtility.AsRef<float>(edgePtr + index) = depth;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateThermalGradientJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<BiotaThermalVentDTO> Vents;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> TemperatureCelsius;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Thermal01;
        [ReadOnly] public BiotaDensityBakeConfigDTO Config;

        public void Execute(int index)
        {
            int width = math.max(1, Config.Width);
            int height = math.max(1, Config.Height);
            if ((uint)index >= (uint)(width * height))
                return;

            int x = index % width;
            int z = index / width;
            double cell = math.max(0.001d, Config.CellSizeMeters);
            double px = Config.SectorOriginAUP.x + x * cell;
            double pz = Config.SectorOriginAUP.z + z * cell;
            float best = 0f;
            float thermal = 0f;
            BiotaThermalVentDTO* vents = Vents.Length > 0 ? (BiotaThermalVentDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Vents) : null;

            for (int i = 0; i < Vents.Length; i++)
            {
                ref BiotaThermalVentDTO vent = ref UnsafeUtility.AsRef<BiotaThermalVentDTO>(vents + i);
                double dx = px - vent.X;
                double dz = pz - vent.Z;
                float radius = math.max(1f, math.max(vent.RadiusMeters, Config.ThermalFalloffMeters));
                float distSq = (float)math.min(3.4028234663852886e38d, dx * dx + dz * dz);
                float invRadiusSq = 1f / math.max(1f, radius * radius);
                float falloff = math.saturate(1f - distSq * invRadiusSq);
                falloff *= falloff;
                best = math.max(best, vent.HeatCelsius * falloff);
                thermal = math.max(thermal, falloff);
            }

            if (Vents.Length == 0)
            {
                double3 aup = Config.SectorOriginAUP + new double3(x * cell, 0d, z * cell);
                float mock = BiotaDensityBakeMath.FractalSimplex01(aup, Config.Seed ^ 0x54484552u, 0.00065f, 2);
                thermal = math.saturate(math.smoothstep(0.72f, 0.96f, mock));
                best = thermal * 68f;
            }

            float* tempPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TemperatureCelsius);
            float* thermalPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Thermal01);
            UnsafeUtility.AsRef<float>(tempPtr + index) = Config.BaseTemperatureCelsius + best;
            UnsafeUtility.AsRef<float>(thermalPtr + index) = thermal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateBiotaDensityJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> DepthMeters;
        [ReadOnly, NoAlias] public NativeArray<float> TemperatureCelsius;
        [ReadOnly, NoAlias] public NativeArray<float> Silt01;
        [ReadOnly, NoAlias] public NativeArray<float> Thermal01;
        [ReadOnly, NoAlias] public NativeArray<uint> BiomeHashes;
        [ReadOnly, NoAlias] public NativeArray<float> WestEdgeDepthMeters;
        [ReadOnly, NoAlias] public NativeArray<float> EastEdgeDepthMeters;
        [ReadOnly, NoAlias] public NativeArray<float> SouthEdgeDepthMeters;
        [ReadOnly, NoAlias] public NativeArray<float> NorthEdgeDepthMeters;
        [ReadOnly, NoAlias] public NativeArray<BiotaSpawnRuleDTO> Rules;
        [ReadOnly, NoAlias] public NativeArray<BiotaRuleWeightDTO> Weights;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> DensityBytes;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> NonFiniteFlags;
        [ReadOnly] public BiotaDensityBakeConfigDTO Config;

        public void Execute(int index)
        {
            int width = math.max(1, Config.Width);
            int height = math.max(1, Config.Height);
            int pixelCount = width * height;
            if ((uint)index >= (uint)pixelCount)
                return;

            int x = index % width;
            int z = index / width;
            float depth = ReadDepth(x, z, width, height);
            float west = ReadDepth(x - 1, z, width, height);
            float east = ReadDepth(x + 1, z, width, height);
            float south = ReadDepth(x, z - 1, width, height);
            float north = ReadDepth(x, z + 1, width, height);
            float invSpan = 0.5f / math.max(0.001f, Config.CellSizeMeters);
            float slopeDegrees = math.degrees(global::Hecton8.Core.MathLodApproximation.ApproxAtanFast(math.length(new float2((east - west) * invSpan, (north - south) * invSpan))));
            float temperature = ReadFloat(TemperatureCelsius, index, Config.BaseTemperatureCelsius);
            float silt = math.saturate(ReadFloat(Silt01, index, 0f));
            float thermal = math.saturate(ReadFloat(Thermal01, index, 0f));
            uint biome = ReadBiome(index);
            double cell = math.max(0.001d, Config.CellSizeMeters);
            double3 pixelAup = Config.SectorOriginAUP + new double3(x * cell, -depth, z * cell);
            float organicNoise = BiotaDensityBakeMath.SimplexNoise01(pixelAup, Config.Seed ^ 0x44454152u, math.max(0.000001f, Config.NoiseFrequency));
            float organicMask = math.saturate(organicNoise + Config.NoiseOffset);
            int layers = math.clamp(Config.LayerCount, 1, BiotaDensityBakeConstants.MaxLayerCount);
            float finiteOk = (math.isfinite(depth) & math.isfinite(slopeDegrees) & math.isfinite(temperature)) ? 1f : 0f;
            byte nonFinite = finiteOk > 0f ? (byte)0 : (byte)1;

            byte* densityPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(DensityBytes);
            for (int layer = 0; layer < layers; layer++)
            {
                float rawWeight = EvaluateLayer(layer, depth, slopeDegrees, temperature, silt, thermal, biome);
                rawWeight = math.select(0f, rawWeight, math.isfinite(rawWeight) && finiteOk > 0f);
                rawWeight *= organicMask * math.max(0f, Config.GlobalDensityMultiplier);
                rawWeight = math.select(0f, rawWeight, math.isfinite(rawWeight));
                int packed = math.clamp((int)math.round(math.saturate(rawWeight) * 255f), 0, 255);
                UnsafeUtility.AsRef<byte>(densityPtr + (layer * pixelCount) + index) = (byte)packed;
            }

            if ((uint)index < (uint)NonFiniteFlags.Length)
            {
                byte* flagPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(NonFiniteFlags);
                UnsafeUtility.AsRef<byte>(flagPtr + index) = nonFinite;
            }
        }

        private float EvaluateLayer(int layer, float depth, float slopeDegrees, float temperature, float silt, float thermal, uint biome)
        {
            float sum = 0f;
            BiotaSpawnRuleDTO* rules = Rules.Length > 0 ? (BiotaSpawnRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Rules) : null;
            BiotaRuleWeightDTO* weights = Weights.Length > 0 ? (BiotaRuleWeightDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Weights) : null;
            int count = math.min(Rules.Length, Weights.Length);
            for (int i = 0; i < count; i++)
            {
                ref BiotaSpawnRuleDTO rule = ref UnsafeUtility.AsRef<BiotaSpawnRuleDTO>(rules + i);
                ref BiotaRuleWeightDTO weight = ref UnsafeUtility.AsRef<BiotaRuleWeightDTO>(weights + i);
                float layerOk = (int)(weight.LayerIndex % (uint)BiotaDensityBakeConstants.MaxLayerCount) == layer ? 1f : 0f;
                float depthWeight = BiotaDensityBakeMath.SoftWindow(rule.MinDepth, rule.MaxDepth, depth, math.max(0.001f, Config.DepthScaleMeters * 0.005f));
                float slopeWeight = BiotaDensityBakeMath.SoftWindow(rule.MinSlope, rule.MaxSlope, slopeDegrees, math.max(0.001f, Config.SlopeSoftnessDegrees));
                float tempDelta = math.abs(temperature - rule.PreferredTemperature);
                float tempTolerance = math.max(0.001f, math.max(weight.TemperatureTolerance, Config.TemperatureSoftnessCelsius));
                float tempWeight = math.saturate(1f - tempDelta / tempTolerance);
                float biomeOk = (rule.RequiredBiomeHash == 0u ||
                                  rule.RequiredBiomeHash == BiotaDensityBakeConstants.BiomeAny ||
                                  rule.RequiredBiomeHash == biome) ? 1f : 0f;
                float siltBoost = math.lerp(1f, 1f + math.saturate(weight.SiltAffinity), silt);
                float thermalBoost = math.lerp(1f, 1f + math.saturate(weight.ThermalAffinity), thermal);
                sum += layerOk * depthWeight * slopeWeight * tempWeight * biomeOk * math.max(0f, weight.SpawnWeight) * siltBoost * thermalBoost;
            }

            return sum;
        }

        private float ReadDepth(int x, int z, int width, int height)
        {
            if (x < 0)
            {
                if ((Config.EdgeSampleFlags & 1u) != 0u && (uint)z < (uint)WestEdgeDepthMeters.Length)
                    return WestEdgeDepthMeters[z];
                x = 0;
            }
            else if (x >= width)
            {
                if ((Config.EdgeSampleFlags & 2u) != 0u && (uint)z < (uint)EastEdgeDepthMeters.Length)
                    return EastEdgeDepthMeters[z];
                x = width - 1;
            }

            if (z < 0)
            {
                if ((Config.EdgeSampleFlags & 4u) != 0u && (uint)x < (uint)SouthEdgeDepthMeters.Length)
                    return SouthEdgeDepthMeters[x];
                z = 0;
            }
            else if (z >= height)
            {
                if ((Config.EdgeSampleFlags & 8u) != 0u && (uint)x < (uint)NorthEdgeDepthMeters.Length)
                    return NorthEdgeDepthMeters[x];
                z = height - 1;
            }

            int sourceIndex = z * width + x;
            return ReadFloat(DepthMeters, sourceIndex, 0f);
        }

        private static float ReadFloat(NativeArray<float> values, int index, float fallback)
        {
            if ((uint)index >= (uint)values.Length)
                return fallback;
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(values);
            return UnsafeUtility.AsRef<float>(ptr + index);
        }

        private uint ReadBiome(int index)
        {
            if ((uint)index >= (uint)BiomeHashes.Length)
                return BiotaDensityBakeConstants.BiomeAny;
            uint* ptr = (uint*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(BiomeHashes);
            return UnsafeUtility.AsRef<uint>(ptr + index);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CountDensityRleRunsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> DensityBytes;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> RunCount;
        [ReadOnly] public int PixelCount;
        [ReadOnly] public int LayerCount;

        public void Execute()
        {
            int pixelCount = math.max(0, PixelCount);
            int layers = math.max(1, LayerCount);
            int sourceLength = math.min(DensityBytes.Length, pixelCount * layers);
            int runCount = 0;
            byte* source = sourceLength > 0 ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(DensityBytes) : null;

            for (int layer = 0; layer < layers; layer++)
            {
                int layerStart = layer * pixelCount;
                int layerEnd = math.min(sourceLength, layerStart + pixelCount);
                if (layerStart >= layerEnd)
                    continue;

                byte current = UnsafeUtility.AsRef<byte>(source + layerStart);
                runCount++;
                for (int i = layerStart + 1; i < layerEnd; i++)
                {
                    byte value = UnsafeUtility.AsRef<byte>(source + i);
                    if (value == current)
                        continue;

                    current = value;
                    runCount++;
                }
            }

            if (RunCount.IsCreated && RunCount.Length > 0)
                RunCount[0] = runCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CompressDensityRleJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> DensityBytes;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<BiotaDensityRleRunDTO> Runs;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> RunCount;
        [ReadOnly] public int PixelCount;
        [ReadOnly] public int LayerCount;

        public void Execute()
        {
            int pixelCount = math.max(0, PixelCount);
            int layers = math.max(1, LayerCount);
            int sourceLength = math.min(DensityBytes.Length, pixelCount * layers);
            int runCount = 0;
            byte* source = sourceLength > 0 ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(DensityBytes) : null;
            BiotaDensityRleRunDTO* runs = Runs.Length > 0 ? (BiotaDensityRleRunDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Runs) : null;

            for (int layer = 0; layer < layers; layer++)
            {
                int layerStart = layer * pixelCount;
                int layerEnd = math.min(sourceLength, layerStart + pixelCount);
                if (layerStart >= layerEnd)
                    continue;

                byte current = UnsafeUtility.AsRef<byte>(source + layerStart);
                uint count = 1u;
                for (int i = layerStart + 1; i < layerEnd; i++)
                {
                    byte value = UnsafeUtility.AsRef<byte>(source + i);
                    if (value == current && count < uint.MaxValue)
                    {
                        count++;
                        continue;
                    }

                    WriteRun(runs, ref runCount, current, (byte)layer, count);
                    current = value;
                    count = 1u;
                }

                WriteRun(runs, ref runCount, current, (byte)layer, count);
            }

            if (RunCount.IsCreated && RunCount.Length > 0)
                RunCount[0] = runCount;
        }

        private void WriteRun(BiotaDensityRleRunDTO* runs, ref int runCount, byte value, byte layer, uint count)
        {
            if ((uint)runCount >= (uint)Runs.Length)
                return;

            UnsafeUtility.AsRef<BiotaDensityRleRunDTO>(runs + runCount) = new BiotaDensityRleRunDTO
            {
                Count = count,
                Value = value,
                Layer = layer,
                _pad0 = 0
            };
            runCount++;
        }
    }

    public static class BiotaDensityBakeMath
    {
        public static void SampleMockTerrain(
            in BiotaDensityBakeConfigDTO config,
            int x,
            int z,
            out float depth,
            out float silt,
            out float ventBasin,
            out float cliff)
        {
            int width = math.max(1, config.Width);
            int height = math.max(1, config.Height);
            float nx = ((float)x / math.max(1f, width - 1f)) * 2f - 1f;
            float nz = ((float)z / math.max(1f, height - 1f)) * 2f - 1f;
            double cell = math.max(0.001d, config.CellSizeMeters);
            double3 aup = config.SectorOriginAUP + new double3(x * cell, 0.0d, z * cell);
            float macro = FractalSimplex01(aup, config.Seed ^ 0x4D4F434Bu, 0.00031f, 3);
            float detail = FractalSimplex01(aup, config.Seed ^ 0x44455441u, 0.0019f, 2);
            float canyonAxis = math.abs(nx + Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(nz * 4.9f) * 0.23f);
            float canyon = 1f - math.smoothstep(0.035f, 0.19f, canyonAxis);
            cliff = math.smoothstep(0.50f, 0.82f, math.abs(nx - 0.18f));
            ventBasin = 1f - math.smoothstep(0.10f, 0.62f, math.length(new float2(nx + 0.46f, nz - 0.25f)));
            float shelf = math.smoothstep(-0.92f, 0.60f, nz);
            depth = 60f + shelf * 3350f + macro * 520f + cliff * 760f + canyon * 470f - ventBasin * 290f + detail * 90f;
            depth = math.max(5f, depth);
            silt = math.saturate(canyon * 0.78f + (1f - cliff) * 0.22f + detail * 0.16f + ventBasin * 0.12f);
        }

        public static float SoftWindow(float minValue, float maxValue, float value, float softness)
        {
            float minEdge = math.min(minValue, maxValue);
            float maxEdge = math.max(minValue, maxValue);
            float soft = math.max(0.000001f, softness);
            float enter = math.smoothstep(minEdge - soft, minEdge + soft, value);
            float exit = 1f - math.smoothstep(maxEdge - soft, maxEdge + soft, value);
            return math.saturate(enter * exit);
        }

        public static float FractalSimplex01(double3 aup, uint seed, float frequency, int octaves)
        {
            float sum = 0f;
            float amp = 0.56f;
            float total = 0f;
            float freq = math.max(0.000001f, frequency);
            int count = math.clamp(octaves, 1, 5);
            for (int i = 0; i < count; i++)
            {
                sum += SimplexNoise01(aup, seed + (uint)(i * 1013), freq) * amp;
                total += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }

            return math.saturate(sum / math.max(0.000001f, total));
        }

        public static float SimplexNoise01(double3 aup, uint seed, float frequency)
        {
            const double F2 = 0.36602540378443864676d;
            const double G2 = 0.21132486540518711775d;
            double x = aup.x * frequency;
            double y = aup.z * frequency;
            double s = (x + y) * F2;
            int i = (int)math.floor(x + s);
            int j = (int)math.floor(y + s);
            double t = (i + j) * G2;
            double x0 = x - (i - t);
            double y0 = y - (j - t);
            int i1 = x0 > y0 ? 1 : 0;
            int j1 = x0 > y0 ? 0 : 1;
            double x1 = x0 - i1 + G2;
            double y1 = y0 - j1 + G2;
            double x2 = x0 - 1.0d + 2.0d * G2;
            double y2 = y0 - 1.0d + 2.0d * G2;
            float n0 = Corner(i, j, x0, y0, seed);
            float n1 = Corner(i + i1, j + j1, x1, y1, seed);
            float n2 = Corner(i + 1, j + 1, x2, y2, seed);
            return math.saturate(0.5f + 35f * (n0 + n1 + n2));
        }

        private static float Corner(int ix, int iy, double x, double y, uint seed)
        {
            double t = 0.5d - x * x - y * y;
            if (t <= 0.0d)
                return 0f;

            uint h = Mix(seed ^ (uint)ix * 374761393u ^ (uint)iy * 668265263u);
            int g = (int)(h & 7u);
            double gx = ((g & 1) == 0 ? 1.0d : -1.0d) * (((g & 2) == 0) ? 1.0d : 0.70710678118d);
            double gy = ((g & 4) == 0 ? 1.0d : -1.0d) * (((g & 2) == 0) ? 0.70710678118d : 1.0d);
            double tt = t * t;
            return (float)(tt * tt * (gx * x + gy * y));
        }

        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        public static uint HashAscii(string value)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(value))
                return hash;
            for (int i = 0; i < value.Length; i++)
            {
                byte ascii = (byte)value[i];
                if (ascii >= (byte)'A' && ascii <= (byte)'Z')
                    ascii = (byte)(ascii + 32);
                hash = Mix(hash ^ ascii);
            }
            return hash == 0u ? 1u : hash;
        }
    }
}
#endif
