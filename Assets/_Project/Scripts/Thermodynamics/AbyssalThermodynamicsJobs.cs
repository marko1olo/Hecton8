using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Thermodynamics
{
    public static unsafe class AbyssalThermalMath
    {
        public const uint CellFlagInjected = 1u << 0;
        public const uint CellFlagHullInsulated = 1u << 1;
        public const uint CellFlagNaN = 1u << 31;
        public const uint TelemetryFlagNaN = 1u << 0;
        public const uint TelemetryFlagShift = 1u << 1;
        public const uint TelemetryFlagMockSources = 1u << 2;
        public const uint TelemetryFlagEnergyDrift = 1u << 3;
        public const int TelemetryCapacity = 300;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index(int x, int y, int z, int3 resolution)
        {
            return (z * resolution.y * resolution.x) + (y * resolution.x) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 DecodeIndex(int index, int3 resolution)
        {
            int xy = resolution.x * resolution.y;
            int z = index / xy;
            int rem = index - (z * xy);
            int y = rem / resolution.x;
            int x = rem - (y * resolution.x);
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 MapAupToWrappedCell(double3 sampleAup, double3 originAup, float cellSizeMeters, int3 resolution)
        {
            double3 localDouble = sampleAup - originAup;
            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            int3 raw = (int3)math.floor(local / math.max(0.001f, cellSizeMeters));
            return new int3(
                PositiveModulo(raw.x, resolution.x),
                PositiveModulo(raw.y, resolution.y),
                PositiveModulo(raw.z, resolution.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CellCenterLocal(int3 cell, float cellSizeMeters)
        {
            return (new float3(cell.x, cell.y, cell.z) + 0.5f) * cellSizeMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveJacobiIterations(float globalQualityWeight)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return math.clamp((int)math.lerp(1f, 6f, q), 1, 6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveResolution(float globalQualityWeight, int minResolution, int maxResolution)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float curved = q * q * (3f - (2f * q));
            int resolution = (int)math.round(math.lerp(minResolution, maxResolution, curved));
            return math.clamp(resolution, minResolution, maxResolution);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'a' && value <= (byte)'z')
                    value = (byte)(value - 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(uint seed, uint value)
        {
            uint hash = seed == 0u ? 2166136261u : seed;
            hash ^= value & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 8) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 16) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 24) & 0xFFu;
            hash *= 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddFinite(float* target, float value)
        {
            if (!math.isfinite(value) || value == 0f)
                return;

            float current = *target;
            *target = math.isfinite(current) ? current + value : value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ThermalGridInitializeJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Front;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Back;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Injection;
        public float AmbientTemperatureCelsius;
        public float WaterThermalConductivity;

        public void Execute(int index)
        {
            ThermalCellDTO cell;
            cell.TemperatureCelsius = AmbientTemperatureCelsius;
            cell.ThermalConductivity = WaterThermalConductivity;
            cell.ConvectionVelocityY = 0f;
            cell.Flags = 0u;

            Front[index] = cell;
            Back[index] = cell;

            cell.TemperatureCelsius = 0f;
            cell.Flags = 0u;
            Injection[index] = cell;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ClearThermalInjectionJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Injection;
        public float WaterThermalConductivity;

        public void Execute(int index)
        {
            ThermalCellDTO cell = Injection[index];
            cell.TemperatureCelsius = 0f;
            cell.ThermalConductivity = WaterThermalConductivity;
            cell.ConvectionVelocityY = 0f;
            cell.Flags = 0u;
            Injection[index] = cell;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockThermalSourcesJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public HeatSourceDTO* Sources;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* SourceCount;
        public ThermalGridTuningDTO Tuning;
        public uint Frame;
        public uint ProfileHash;

        public void Execute()
        {
            int count = math.clamp(Tuning.MockVolcanoCount, 1, 16);
            float radius = math.max(Tuning.CellSizeMeters, Tuning.MockVolcanoRadiusMeters);
            float intensity = math.max(1f, Tuning.MockVolcanoIntensity);
            float span = math.max(Tuning.CellSizeMeters, Tuning.CellSizeMeters * (Tuning.GridResolution.x - 2));
            double3 origin = Tuning.GridOriginAup;

            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / (count + 1f);
                float x = (t * span) + (math.sin((Frame * 0.013f) + (i * 3.17f)) * Tuning.CellSizeMeters * 1.5f);
                float z = ((1f - t) * span) + (math.cos((Frame * 0.011f) + (i * 2.41f)) * Tuning.CellSizeMeters * 1.5f);
                float y = Tuning.CellSizeMeters * math.lerp(0.35f, 1.75f, math.frac(t * 2.37f));

                HeatSourceDTO source;
                source.Aup = origin + new double3(x, y, z);
                source.IntensityCelsiusPerSecond = intensity * math.lerp(0.7f, 1.35f, t);
                source.RadiusMeters = radius * math.lerp(0.75f, 1.2f, math.frac(t * 1.91f));
                source.FalloffExponent = 1.55f;
                source.ProfileHash = ProfileHash;
                source.SourceId = 0xAB710000u + (uint)i;
                source.Flags = HeatSourceDTO.FlagMock;
                source.ConductivityOverride = Tuning.WaterThermalConductivity;
                source.ConvectionGain = 1f;
                source.Phase01 = math.frac((Frame * 0.001f) + t);
                source.LastTouchedFrame = Frame;
                Sources[i] = source;
            }

            *SourceCount = count;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ThermalInjectionJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Injection;
        [NativeDisableUnsafePtrRestriction, NoAlias] public HeatSourceDTO* Sources;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* SourceCount;
        public ThermalGridTuningDTO Tuning;
        public float DeltaTime;
        public uint Frame;
        public uint SourceTtlFrames;

        public void Execute()
        {
            int count = math.max(0, *SourceCount);
            int3 resolution = Tuning.GridResolution;
            float cellSize = math.max(0.001f, Tuning.CellSizeMeters);
            double3 origin = Tuning.GridOriginAup;

            for (int sourceIndex = 0; sourceIndex < count; sourceIndex++)
            {
                HeatSourceDTO source = Sources[sourceIndex];
                if (source.RadiusMeters <= 0f || source.IntensityCelsiusPerSecond == 0f)
                    continue;
                if ((source.Flags & HeatSourceDTO.FlagPersistent) == 0u &&
                    SourceTtlFrames > 0u &&
                    Frame - source.LastTouchedFrame > SourceTtlFrames)
                {
                    continue;
                }

                int3 centerCell = AbyssalThermalMath.MapAupToWrappedCell(source.Aup, origin, cellSize, resolution);
                int radiusCells = math.clamp((int)math.ceil(source.RadiusMeters / cellSize), 1, math.max(resolution.x, math.max(resolution.y, resolution.z)));
                float invRadius = math.rcp(math.max(0.001f, source.RadiusMeters));
                float falloff = math.max(0.25f, source.FalloffExponent);

                for (int z = -radiusCells; z <= radiusCells; z++)
                {
                    for (int y = -radiusCells; y <= radiusCells; y++)
                    {
                        for (int x = -radiusCells; x <= radiusCells; x++)
                        {
                            float3 offsetMeters = new float3(x, y, z) * cellSize;
                            float distance = math.length(offsetMeters);
                            if (distance > source.RadiusMeters)
                                continue;

                            int ix = AbyssalThermalMath.PositiveModulo(centerCell.x + x, resolution.x);
                            int iy = AbyssalThermalMath.PositiveModulo(centerCell.y + y, resolution.y);
                            int iz = AbyssalThermalMath.PositiveModulo(centerCell.z + z, resolution.z);
                            int index = AbyssalThermalMath.Index(ix, iy, iz, resolution);
                            float weight = math.pow(math.saturate(1f - (distance * invRadius)), falloff);
                            float heat = source.IntensityCelsiusPerSecond * DeltaTime * weight;
                            if (!math.isfinite(heat) || heat == 0f)
                                continue;

                            ThermalCellDTO* cell = Injection + index;
                            AbyssalThermalMath.AddFinite(&cell->TemperatureCelsius, heat);
                            cell->ThermalConductivity = math.select(Tuning.WaterThermalConductivity, source.ConductivityOverride, source.ConductivityOverride > 0f);
                            cell->ConvectionVelocityY = math.max(cell->ConvectionVelocityY, heat * source.ConvectionGain);
                            cell->Flags |= AbyssalThermalMath.CellFlagInjected;
                        }
                    }
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct SubmarineHullInsulationJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Cells;
        public ThermalGridTuningDTO Tuning;

        public void Execute(int index)
        {
            int3 cellIndex = AbyssalThermalMath.DecodeIndex(index, Tuning.GridResolution);
            float3 local = AbyssalThermalMath.CellCenterLocal(cellIndex, Tuning.CellSizeMeters);
            float3 halfGrid = new float3(Tuning.GridResolution.x, Tuning.GridResolution.y, Tuning.GridResolution.z) * Tuning.CellSizeMeters * 0.5f;
            float3 halfHull = new float3(
                math.max(0f, Tuning.SubmarineHalfExtentX),
                math.max(0f, Tuning.SubmarineHalfExtentY),
                math.max(0f, Tuning.SubmarineHalfExtentZ));

            if (math.all(halfHull > 0f) && math.all(math.abs(local - halfGrid) <= halfHull))
            {
                ThermalCellDTO cell = Cells[index];
                cell.ThermalConductivity = math.max(0.0001f, Tuning.HullInsulationConductivity);
                cell.Flags |= AbyssalThermalMath.CellFlagHullInsulated;
                Cells[index] = cell;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct HeatDiffusionSolverJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Front;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Back;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Injection;
        public ThermalGridTuningDTO Tuning;
        public byte ApplyInjection;

        public void Execute(int index)
        {
            int3 resolution = Tuning.GridResolution;
            int3 c = AbyssalThermalMath.DecodeIndex(index, resolution);
            ThermalCellDTO currentCell = Front[index];
            ThermalCellDTO injected = ApplyInjection != 0 ? Injection[index] : default;
            float current = currentCell.TemperatureCelsius + injected.TemperatureCelsius;
            float conductivity = math.max(0.0001f, currentCell.ThermalConductivity);
            int iterations = math.max(1, Tuning.JacobiIterations);

            for (int i = 0; i < iterations; i++)
            {
                float weighted = current;
                float weight = 1f;
                AccumulateNeighbor(c.x - 1, c.y, c.z, resolution, conductivity, ref weighted, ref weight);
                AccumulateNeighbor(c.x + 1, c.y, c.z, resolution, conductivity, ref weighted, ref weight);
                AccumulateNeighbor(c.x, c.y - 1, c.z, resolution, conductivity, ref weighted, ref weight);
                AccumulateNeighbor(c.x, c.y + 1, c.z, resolution, conductivity, ref weighted, ref weight);
                AccumulateNeighbor(c.x, c.y, c.z - 1, resolution, conductivity, ref weighted, ref weight);
                AccumulateNeighbor(c.x, c.y, c.z + 1, resolution, conductivity, ref weighted, ref weight);

                current = weighted / math.max(0.0001f, weight);
            }

            current = math.lerp(current, Tuning.AmbientTemperatureCelsius, math.saturate(Tuning.DissipationPerStep));
            ThermalCellDTO output = currentCell;
            output.TemperatureCelsius = math.clamp(current, -273.15f, Tuning.MaxStableTemperatureCelsius);
            output.ConvectionVelocityY = math.max(0f, (output.TemperatureCelsius - Tuning.AmbientTemperatureCelsius) * Tuning.ConvectionSpeed);
            output.Flags = (output.Flags & AbyssalThermalMath.CellFlagHullInsulated) | (injected.Flags & AbyssalThermalMath.CellFlagInjected);

            if (!math.isfinite(output.TemperatureCelsius))
            {
                output.TemperatureCelsius = Tuning.AmbientTemperatureCelsius;
                output.Flags |= AbyssalThermalMath.CellFlagNaN;
            }

            Back[index] = output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AccumulateNeighbor(int x, int y, int z, int3 resolution, float centerConductivity, ref float weighted, ref float weight)
        {
            if ((uint)x >= (uint)resolution.x || (uint)y >= (uint)resolution.y || (uint)z >= (uint)resolution.z)
                return;

            int neighborIndex = AbyssalThermalMath.Index(x, y, z, resolution);
            ThermalCellDTO neighbor = Front[neighborIndex];
            float pairConductivity = math.max(0.0001f, math.min(centerConductivity, neighbor.ThermalConductivity) * Tuning.WaterThermalConductivity);
            weighted += neighbor.TemperatureCelsius * pairConductivity;
            weight += pairConductivity;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct SampleTemperatureJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Cells;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* SampleAups;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalSampleResultDTO* Results;
        public ThermalGridTuningDTO Tuning;

        public void Execute(int index)
        {
            double3 aup = SampleAups[index];
            double3 localDouble = aup - Tuning.GridOriginAup;
            float cellSize = math.max(0.001f, Tuning.CellSizeMeters);
            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            float3 grid = local / cellSize;
            int3 baseCell = (int3)math.floor(grid);
            float3 fraction = math.frac(grid);
            int3 nearestCell = new int3(
                AbyssalThermalMath.PositiveModulo(baseCell.x, Tuning.GridResolution.x),
                AbyssalThermalMath.PositiveModulo(baseCell.y, Tuning.GridResolution.y),
                AbyssalThermalMath.PositiveModulo(baseCell.z, Tuning.GridResolution.z));
            int cellIndex = AbyssalThermalMath.Index(nearestCell.x, nearestCell.y, nearestCell.z, Tuning.GridResolution);
            ThermalCellDTO nearest = Cells[cellIndex];

            float temperature = nearest.TemperatureCelsius;
            float convection = nearest.ConvectionVelocityY;
            float conductivity = nearest.ThermalConductivity;
            uint flags = nearest.Flags;
            float interpolationWeight = ResolveInterpolationWeight(Tuning.GlobalQualityWeight);
            if (interpolationWeight > 0f)
            {
                SampleTrilinear(baseCell, fraction, out float triTemperature, out float triConvection, out float triConductivity);
                temperature = math.lerp(temperature, triTemperature, interpolationWeight);
                convection = math.lerp(convection, triConvection, interpolationWeight);
                conductivity = math.lerp(conductivity, triConductivity, interpolationWeight);
            }

            if (!math.isfinite(temperature))
            {
                temperature = Tuning.AmbientTemperatureCelsius;
                flags |= AbyssalThermalMath.CellFlagNaN;
            }

            if (!math.isfinite(convection))
                convection = 0f;

            if (!math.isfinite(conductivity))
                conductivity = Tuning.WaterThermalConductivity;

            ThermalSampleResultDTO result;
            result.TemperatureCelsius = temperature;
            result.ConvectionVelocityY = convection;
            result.CellIndex = (uint)cellIndex;
            result.Flags = flags;
            result.LocalGridPosition = local;
            result.Conductivity = conductivity;
            Results[index] = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveInterpolationWeight(float globalQualityWeight)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float t = math.saturate((q - 0.15f) * math.rcp(0.65f));
            float smooth = t * t * (3f - (2f * t));
            return smooth * math.step(0.15f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SampleTrilinear(int3 baseCell, float3 fraction, out float temperature, out float convection, out float conductivity)
        {
            ThermalCellDTO c000 = ReadCell(baseCell.x, baseCell.y, baseCell.z);
            ThermalCellDTO c100 = ReadCell(baseCell.x + 1, baseCell.y, baseCell.z);
            ThermalCellDTO c010 = ReadCell(baseCell.x, baseCell.y + 1, baseCell.z);
            ThermalCellDTO c110 = ReadCell(baseCell.x + 1, baseCell.y + 1, baseCell.z);
            ThermalCellDTO c001 = ReadCell(baseCell.x, baseCell.y, baseCell.z + 1);
            ThermalCellDTO c101 = ReadCell(baseCell.x + 1, baseCell.y, baseCell.z + 1);
            ThermalCellDTO c011 = ReadCell(baseCell.x, baseCell.y + 1, baseCell.z + 1);
            ThermalCellDTO c111 = ReadCell(baseCell.x + 1, baseCell.y + 1, baseCell.z + 1);

            temperature = Lerp8(
                c000.TemperatureCelsius,
                c100.TemperatureCelsius,
                c010.TemperatureCelsius,
                c110.TemperatureCelsius,
                c001.TemperatureCelsius,
                c101.TemperatureCelsius,
                c011.TemperatureCelsius,
                c111.TemperatureCelsius,
                fraction);
            convection = Lerp8(
                c000.ConvectionVelocityY,
                c100.ConvectionVelocityY,
                c010.ConvectionVelocityY,
                c110.ConvectionVelocityY,
                c001.ConvectionVelocityY,
                c101.ConvectionVelocityY,
                c011.ConvectionVelocityY,
                c111.ConvectionVelocityY,
                fraction);
            conductivity = Lerp8(
                c000.ThermalConductivity,
                c100.ThermalConductivity,
                c010.ThermalConductivity,
                c110.ThermalConductivity,
                c001.ThermalConductivity,
                c101.ThermalConductivity,
                c011.ThermalConductivity,
                c111.ThermalConductivity,
                fraction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ThermalCellDTO ReadCell(int x, int y, int z)
        {
            int ix = AbyssalThermalMath.PositiveModulo(x, Tuning.GridResolution.x);
            int iy = AbyssalThermalMath.PositiveModulo(y, Tuning.GridResolution.y);
            int iz = AbyssalThermalMath.PositiveModulo(z, Tuning.GridResolution.z);
            return Cells[AbyssalThermalMath.Index(ix, iy, iz, Tuning.GridResolution)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Lerp8(float c000, float c100, float c010, float c110, float c001, float c101, float c011, float c111, float3 t)
        {
            float x00 = math.lerp(c000, c100, t.x);
            float x10 = math.lerp(c010, c110, t.x);
            float x01 = math.lerp(c001, c101, t.x);
            float x11 = math.lerp(c011, c111, t.x);
            float y0 = math.lerp(x00, x10, t.y);
            float y1 = math.lerp(x01, x11, t.y);
            return math.lerp(y0, y1, t.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ShiftThermalGridJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Cells;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Scratch;
        public int3 ShiftCells;
        public ThermalGridTuningDTO Tuning;

        public void Execute()
        {
            int3 resolution = Tuning.GridResolution;
            int cellCount = Tuning.ActiveCellCount;
            long byteCount = (long)cellCount * UnsafeUtility.SizeOf<ThermalCellDTO>();
            UnsafeUtility.MemMove(Scratch, Cells, byteCount);

            ThermalCellDTO ambient;
            ambient.TemperatureCelsius = Tuning.AmbientTemperatureCelsius;
            ambient.ThermalConductivity = Tuning.WaterThermalConductivity;
            ambient.ConvectionVelocityY = 0f;
            ambient.Flags = 0u;

            for (int i = 0; i < cellCount; i++)
            {
                int3 dst = AbyssalThermalMath.DecodeIndex(i, resolution);
                int3 src = dst + ShiftCells;
                if ((uint)src.x < (uint)resolution.x && (uint)src.y < (uint)resolution.y && (uint)src.z < (uint)resolution.z)
                {
                    int srcIndex = AbyssalThermalMath.Index(src.x, src.y, src.z, resolution);
                    Cells[i] = Scratch[srcIndex];
                }
                else
                {
                    Cells[i] = ambient;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ThermalTelemetryRecorderJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Front;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Back;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Injection;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* SourceCount;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalTelemetryEntry* Ring;
        public ThermalGridTuningDTO Tuning;
        public float SolverMicroseconds;
        public uint Frame;
        public uint ExtraFlags;

        public void Execute()
        {
            int cellCount = math.max(0, Tuning.ActiveCellCount);
            float maxTemp = Tuning.AmbientTemperatureCelsius;
            float energyBefore = 0f;
            float energyAfter = 0f;
            uint flags = ExtraFlags;
            uint nanIndex = uint.MaxValue;

            for (int i = 0; i < cellCount; i++)
            {
                ThermalCellDTO beforeCell = Front[i];
                ThermalCellDTO injectedCell = Injection[i];
                ThermalCellDTO afterCell = Back[i];
                float beforeTemp = beforeCell.TemperatureCelsius + injectedCell.TemperatureCelsius;
                float afterTemp = afterCell.TemperatureCelsius;
                if (!math.isfinite(beforeTemp))
                {
                    flags |= AbyssalThermalMath.TelemetryFlagNaN;
                    nanIndex = (uint)i;
                    beforeTemp = Tuning.AmbientTemperatureCelsius;
                }

                if (!math.isfinite(afterTemp) || (afterCell.Flags & AbyssalThermalMath.CellFlagNaN) != 0u)
                {
                    flags |= AbyssalThermalMath.TelemetryFlagNaN;
                    nanIndex = (uint)i;
                    afterTemp = Tuning.AmbientTemperatureCelsius;
                }

                maxTemp = math.max(maxTemp, afterTemp);
                energyBefore += beforeTemp;
                energyAfter += afterTemp;
            }

            float ambientEnergy = Tuning.AmbientTemperatureCelsius * cellCount;
            float dissipatedBudget = math.abs(energyBefore - ambientEnergy) * math.saturate(Tuning.DissipationPerStep) * 1.5f;
            float driftTolerance = math.max(1f, dissipatedBudget + (math.abs(energyBefore) * 0.01f));
            if (math.abs(energyAfter - energyBefore) > driftTolerance)
                flags |= AbyssalThermalMath.TelemetryFlagEnergyDrift;

            int ringIndex = (int)(Frame % AbyssalThermalMath.TelemetryCapacity);
            ThermalTelemetryEntry entry;
            entry.MaxTemperatureCelsius = maxTemp;
            entry.EnergyBefore = energyBefore;
            entry.EnergyAfter = energyAfter;
            entry.SolverMicroseconds = SolverMicroseconds;
            entry.GridOriginAup = Tuning.GridOriginAup;
            entry.Frame = Frame;
            entry.Flags = flags;
            entry.ActiveSourceCount = (uint)math.max(0, *SourceCount);
            entry.JacobiIterations = (uint)math.max(1, Tuning.JacobiIterations);
            entry.NaNCellIndex = nanIndex;
            entry.ActiveResolution = (uint)Tuning.GridResolution.x;
            Ring[ringIndex] = entry;
        }
    }

    public static unsafe class HeatSourceProfileCsvParser
    {
        public static int Parse(ReadOnlySpan<byte> csvBytes, HeatSourceProfileDTO* profiles, int maxProfiles)
        {
            int count = 0;
            int lineStart = 0;
            for (int i = 0; i <= csvBytes.Length && count < maxProfiles; i++)
            {
                bool end = i == csvBytes.Length || csvBytes[i] == (byte)'\n';
                if (!end)
                    continue;

                ReadOnlySpan<byte> line = Trim(csvBytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#' || StartsWithHeader(line))
                    continue;

                if (TryParseLine(line, out HeatSourceProfileDTO profile))
                    profiles[count++] = profile;
            }

            return count;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out HeatSourceProfileDTO profile)
        {
            profile = default;
            int c0 = IndexOf(line, (byte)',', 0);
            if (c0 <= 0)
                return false;
            int c1 = IndexOf(line, (byte)',', c0 + 1);
            int c2 = IndexOf(line, (byte)',', c1 + 1);
            int c3 = IndexOf(line, (byte)',', c2 + 1);
            if (c1 <= c0 || c2 <= c1 || c3 <= c2)
                return false;

            ReadOnlySpan<byte> name = Trim(line.Slice(0, c0));
            ReadOnlySpan<byte> intensity = Trim(line.Slice(c0 + 1, c1 - c0 - 1));
            ReadOnlySpan<byte> radius = Trim(line.Slice(c1 + 1, c2 - c1 - 1));
            ReadOnlySpan<byte> falloff = Trim(line.Slice(c2 + 1, c3 - c2 - 1));
            ReadOnlySpan<byte> convection = Trim(line.Slice(c3 + 1));

            if (name.Length == 0 ||
                !TryParseFloat(intensity, out float intensityValue) ||
                !TryParseFloat(radius, out float radiusValue) ||
                !TryParseFloat(falloff, out float falloffValue) ||
                !TryParseFloat(convection, out float convectionValue))
            {
                return false;
            }

            profile.NameHash = AbyssalThermalMath.Fnv1A(name);
            profile.IntensityCelsiusPerSecond = intensityValue;
            profile.RadiusMeters = radiusValue;
            profile.FalloffExponent = math.max(0.25f, falloffValue);
            profile.ConvectionGain = math.max(0f, convectionValue);
            profile.Flags = 0u;
            profile._pad0 = 0u;
            profile._pad1 = 0u;
            return true;
        }

        private static bool StartsWithHeader(ReadOnlySpan<byte> line)
        {
            return line.Length >= 4 &&
                   (line[0] == (byte)'n' || line[0] == (byte)'N') &&
                   (line[1] == (byte)'a' || line[1] == (byte)'A') &&
                   (line[2] == (byte)'m' || line[2] == (byte)'M') &&
                   (line[3] == (byte)'e' || line[3] == (byte)'E');
        }

        private static int IndexOf(ReadOnlySpan<byte> bytes, byte value, int start)
        {
            if (start < 0)
                return -1;
            for (int i = start; i < bytes.Length; i++)
            {
                if (bytes[i] == value)
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start < bytes.Length && IsSpace(bytes[start]))
                start++;
            while (end >= start && IsSpace(bytes[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            if (bytes.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (bytes[0] == (byte)'-')
            {
                sign = -1f;
                index = 1;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < bytes.Length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                hasDigit = true;
                integer = (integer * 10f) + (bytes[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < bytes.Length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction = (fraction * 10f) + (bytes[index] - (byte)'0');
                    divisor *= 10f;
                    index++;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (integer + (fraction / divisor));
            return true;
        }
    }
}
