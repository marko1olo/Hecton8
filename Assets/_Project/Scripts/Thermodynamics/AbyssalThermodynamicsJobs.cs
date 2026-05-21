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
        public const uint CellFlagDivergent = 1u << 30;
        public const uint CellFlagNaN = 1u << 31;
        public const uint TelemetryFlagNaN = 1u << 0;
        public const uint TelemetryFlagShift = 1u << 1;
        public const uint TelemetryFlagMockSources = 1u << 2;
        public const uint TelemetryFlagEnergyDrift = 1u << 3;
        public const uint TelemetryFlagDivergent = 1u << 4;
        public const uint TelemetryFlagMaxIterations = 1u << 5;
        public const ushort SolverFlagConverged = 1 << 0;
        public const ushort SolverFlagDivergent = 1 << 1;
        public const ushort SolverFlagNonFinite = 1 << 2;
        public const ushort SolverFlagMaxIterations = 1 << 3;
        public const int TelemetryCapacity = 300;
        public const int ResidualThreadSlotCount = 128;
        public const int MaxThermalSourceCapacity = 128;
        public const int MaxSafeResolution = 32;
        public const int MaxSafeCellCount = MaxSafeResolution * MaxSafeResolution * MaxSafeResolution;
        public const uint ResidualSlotFaultNonFinite = 1u;
        public const float AuthoritativeQualityWeight = 1f;
        public const int AuthoritativeJacobiIterations = 6;
        public const float AuthoritativeSolverOmega = 1f;
        public const float AuthoritativeSolverTargetTolerance = 0.001f;
        public const int AuthoritativeResidualSampleMask = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 SafeResolution(int3 resolution)
        {
            return math.clamp(
                resolution,
                new int3(1, 1, 1),
                new int3(MaxSafeResolution, MaxSafeResolution, MaxSafeResolution));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index(int x, int y, int z, int3 resolution)
        {
            int3 safeResolution = SafeResolution(resolution);
            int ix = math.clamp(x, 0, safeResolution.x - 1);
            int iy = math.clamp(y, 0, safeResolution.y - 1);
            int iz = math.clamp(z, 0, safeResolution.z - 1);
            return (iz * safeResolution.y * safeResolution.x) + (iy * safeResolution.x) + ix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 DecodeIndex(int index, int3 resolution)
        {
            int3 safeResolution = SafeResolution(resolution);
            int maxIndex = math.max(0, (safeResolution.x * safeResolution.y * safeResolution.z) - 1);
            int safeIndex = math.clamp(index, 0, maxIndex);
            int xy = math.max(1, safeResolution.x * safeResolution.y);
            int z = math.min(safeResolution.z - 1, safeIndex / xy);
            int rem = safeIndex - (z * xy);
            int y = math.min(safeResolution.y - 1, rem / safeResolution.x);
            int x = rem - (y * safeResolution.x);
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PositiveModulo(int value, int modulus)
        {
            int safeModulus = math.max(1, modulus);
            int result = value % safeModulus;
            return result < 0 ? result + safeModulus : result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 MapAupToWrappedCell(double3 sampleAup, double3 originAup, float cellSizeMeters, int3 resolution)
        {
            int3 safeResolution = SafeResolution(resolution);
            double3 localDouble = sampleAup - originAup;
            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            int3 raw = (int3)math.floor(local / math.max(0.001f, cellSizeMeters));
            return new int3(
                PositiveModulo(raw.x, safeResolution.x),
                PositiveModulo(raw.y, safeResolution.y),
                PositiveModulo(raw.z, safeResolution.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CellCenterLocal(int3 cell, float cellSizeMeters)
        {
            return (new float3(cell.x, cell.y, cell.z) + 0.5f) * cellSizeMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveJacobiIterations(float globalQualityWeight)
        {
            return AuthoritativeJacobiIterations;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSolverOmega(float globalQualityWeight)
        {
            return AuthoritativeSolverOmega;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSolverTargetTolerance(float globalQualityWeight)
        {
            return AuthoritativeSolverTargetTolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveResidualSampleMask(float globalQualityWeight)
        {
            return AuthoritativeResidualSampleMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveResolution(float globalQualityWeight, int minResolution, int maxResolution)
        {
            return math.clamp(maxResolution, minResolution, maxResolution);
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
    public unsafe struct InitializeThermalSolverConvergenceJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalSolverConvergenceStateDTO* SolverState;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalResidualSlot64* ResidualSamples;
        public int ResidualSlotCount;
        public float BaseOmega;

        public void Execute(int index)
        {
            int slotCount = math.clamp(ResidualSlotCount, 1, AbyssalThermalMath.ResidualThreadSlotCount);
            if ((uint)index < (uint)slotCount)
                ResidualSamples[index] = default;
            if (index == 0)
            {
                SolverState[0] = new ThermalSolverConvergenceStateDTO
                {
                    MaxResidualFloat = 0f,
                    PreviousResidualFloat = 0f,
                    Omega = math.clamp(AbyssalThermalMath.FiniteOr(BaseOmega, 1f), 0.55f, 1f),
                    IterationCount = 0,
                    FaultFlags = 0
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ClearThermalSolverResidualSlotsJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalResidualSlot64* ResidualSamples;
        public int ResidualSlotCount;

        public void Execute(int index)
        {
            int slotCount = math.clamp(ResidualSlotCount, 1, AbyssalThermalMath.ResidualThreadSlotCount);
            if ((uint)index < (uint)slotCount)
                ResidualSamples[index] = default;
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
            cell.TemperatureCelsius = AbyssalThermalMath.FiniteOr(AmbientTemperatureCelsius, 0f);
            cell.ThermalConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(WaterThermalConductivity, 0.18f));
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
            cell.ThermalConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(WaterThermalConductivity, 0.18f));
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
            int3 safeResolution = AbyssalThermalMath.SafeResolution(Tuning.GridResolution);
            float cellSize = math.max(0.001f, AbyssalThermalMath.FiniteOr(Tuning.CellSizeMeters, 8f));
            float radius = math.max(cellSize, AbyssalThermalMath.FiniteOr(Tuning.MockVolcanoRadiusMeters, 42f));
            float intensity = math.max(1f, AbyssalThermalMath.FiniteOr(Tuning.MockVolcanoIntensity, 180f));
            float conductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(Tuning.WaterThermalConductivity, 0.18f));
            float span = math.max(cellSize, cellSize * (safeResolution.x - 2));
            double3 origin = Tuning.GridOriginAup;

            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / (count + 1f);
                float x = (t * span) + (math.sin((Frame * 0.013f) + (i * 3.17f)) * cellSize * 1.5f);
                float z = ((1f - t) * span) + (math.cos((Frame * 0.011f) + (i * 2.41f)) * cellSize * 1.5f);
                float y = cellSize * math.lerp(0.35f, 1.75f, math.frac(t * 2.37f));

                HeatSourceDTO source;
                source.Aup = origin + new double3(x, y, z);
                source.IntensityCelsiusPerSecond = intensity * math.lerp(0.7f, 1.35f, t);
                source.RadiusMeters = radius * math.lerp(0.75f, 1.2f, math.frac(t * 1.91f));
                source.FalloffExponent = 1.55f;
                source.ProfileHash = ProfileHash;
                source.SourceId = 0xAB710000u + (uint)i;
                source.Flags = HeatSourceDTO.FlagMock;
                source.ConductivityOverride = conductivity;
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
        public int SourceCapacity;

        public void Execute()
        {
            int sourceCapacity = math.clamp(SourceCapacity, 0, AbyssalThermalMath.MaxThermalSourceCapacity);
            int count = math.clamp(*SourceCount, 0, sourceCapacity);
            int3 resolution = AbyssalThermalMath.SafeResolution(Tuning.GridResolution);
            float cellSize = math.max(0.001f, AbyssalThermalMath.FiniteOr(Tuning.CellSizeMeters, 8f));
            float safeDeltaTime = math.clamp(math.isfinite(DeltaTime) ? DeltaTime : 1f / 60f, 1f / 60f, 0.2f);
            double3 origin = Tuning.GridOriginAup;

            for (int sourceIndex = 0; sourceIndex < count; sourceIndex++)
            {
                HeatSourceDTO source = Sources[sourceIndex];
                float radiusMeters = math.max(0f, AbyssalThermalMath.FiniteOr(source.RadiusMeters, 0f));
                float intensity = AbyssalThermalMath.FiniteOr(source.IntensityCelsiusPerSecond, 0f);
                if (radiusMeters <= 0f || intensity == 0f)
                    continue;
                if ((source.Flags & HeatSourceDTO.FlagPersistent) == 0u &&
                    SourceTtlFrames > 0u &&
                    Frame - source.LastTouchedFrame > SourceTtlFrames)
                {
                    continue;
                }

                int3 centerCell = AbyssalThermalMath.MapAupToWrappedCell(source.Aup, origin, cellSize, resolution);
                int radiusCells = math.clamp((int)math.ceil(radiusMeters / cellSize), 1, math.max(resolution.x, math.max(resolution.y, resolution.z)));
                float invRadius = math.rcp(math.max(0.001f, radiusMeters));
                float falloff = math.max(0.25f, AbyssalThermalMath.FiniteOr(source.FalloffExponent, 1f));
                float tuningConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(Tuning.WaterThermalConductivity, 0.18f));
                float conductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(source.ConductivityOverride, tuningConductivity));
                float convectionGain = AbyssalThermalMath.FiniteOr(source.ConvectionGain, 0f);

                for (int z = -radiusCells; z <= radiusCells; z++)
                {
                    for (int y = -radiusCells; y <= radiusCells; y++)
                    {
                        for (int x = -radiusCells; x <= radiusCells; x++)
                        {
                            float3 offsetMeters = new float3(x, y, z) * cellSize;
                            float distanceSq = math.lengthsq(offsetMeters);
                            float radiusSq = radiusMeters * radiusMeters;
                            if (distanceSq > radiusSq)
                                continue;

                            float distance = distanceSq <= 0.000001f ? 0f : distanceSq * math.rsqrt(math.max(distanceSq, 0.000001f));
                            int ix = AbyssalThermalMath.PositiveModulo(centerCell.x + x, resolution.x);
                            int iy = AbyssalThermalMath.PositiveModulo(centerCell.y + y, resolution.y);
                            int iz = AbyssalThermalMath.PositiveModulo(centerCell.z + z, resolution.z);
                            int index = AbyssalThermalMath.Index(ix, iy, iz, resolution);
                            float weight = math.pow(math.saturate(1f - (distance * invRadius)), falloff);
                            float heat = intensity * safeDeltaTime * weight;
                            if (!math.isfinite(heat) || heat == 0f)
                                continue;

                            ThermalCellDTO* cell = Injection + index;
                            AbyssalThermalMath.AddFinite(&cell->TemperatureCelsius, heat);
                            cell->ThermalConductivity = conductivity;
                            cell->ConvectionVelocityY = math.max(cell->ConvectionVelocityY, heat * convectionGain);
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
            int3 resolution = AbyssalThermalMath.SafeResolution(Tuning.GridResolution);
            float cellSize = math.max(0.001f, AbyssalThermalMath.FiniteOr(Tuning.CellSizeMeters, 8f));
            int3 cellIndex = AbyssalThermalMath.DecodeIndex(index, resolution);
            float3 local = AbyssalThermalMath.CellCenterLocal(cellIndex, cellSize);
            float3 halfGrid = new float3(resolution.x, resolution.y, resolution.z) * cellSize * 0.5f;
            float3 halfHull = new float3(
                math.max(0f, AbyssalThermalMath.FiniteOr(Tuning.SubmarineHalfExtentX, 0f)),
                math.max(0f, AbyssalThermalMath.FiniteOr(Tuning.SubmarineHalfExtentY, 0f)),
                math.max(0f, AbyssalThermalMath.FiniteOr(Tuning.SubmarineHalfExtentZ, 0f)));

            if (math.all(halfHull > 0f) && math.all(math.abs(local - halfGrid) <= halfHull))
            {
                ThermalCellDTO cell = Cells[index];
                cell.ThermalConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(Tuning.HullInsulationConductivity, 0.0025f));
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
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalSolverConvergenceStateDTO* SolverState;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalResidualSlot64* ResidualSamples;
        public ThermalGridTuningDTO Tuning;
        public int ResidualSampleMask;
        public int ResidualSlotCount;
        public byte ApplyInjection;
        [NativeSetThreadIndex] public int ThreadIndex;

        public void Execute(int index)
        {
            int3 resolution = AbyssalThermalMath.SafeResolution(Tuning.GridResolution);
            int3 c = AbyssalThermalMath.DecodeIndex(index, resolution);
            ThermalCellDTO currentCell = Front[index];
            ThermalSolverConvergenceStateDTO state = SolverState[0];
            const ushort terminalFlags = (ushort)(
                AbyssalThermalMath.SolverFlagConverged |
                AbyssalThermalMath.SolverFlagDivergent |
                AbyssalThermalMath.SolverFlagNonFinite);
            if ((state.FaultFlags & terminalFlags) != 0)
            {
                Back[index] = currentCell;
                return;
            }

            ThermalCellDTO injected = ApplyInjection != 0 ? Injection[index] : default;
            float ambient = AbyssalThermalMath.FiniteOr(Tuning.AmbientTemperatureCelsius, 0f);
            float waterConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(Tuning.WaterThermalConductivity, 0.18f));
            float dissipation = math.saturate(AbyssalThermalMath.FiniteOr(Tuning.DissipationPerStep, 0.0025f));
            float maxStable = math.max(ambient + 1f, AbyssalThermalMath.FiniteOr(Tuning.MaxStableTemperatureCelsius, 200f));
            float convectionSpeed = math.max(0f, AbyssalThermalMath.FiniteOr(Tuning.ConvectionSpeed, 0f));
            float current = AbyssalThermalMath.FiniteOr(currentCell.TemperatureCelsius, ambient) + AbyssalThermalMath.FiniteOr(injected.TemperatureCelsius, 0f);
            float conductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(currentCell.ThermalConductivity, waterConductivity));
            float omega = math.clamp(AbyssalThermalMath.FiniteOr(state.Omega, AbyssalThermalMath.AuthoritativeSolverOmega), 0.55f, 1f);
            bool divergent = false;
            bool nonFinite = false;
            float maxResidual = 0f;

            float weighted = current;
            float weight = 1f;
            AccumulateNeighbor(c.x - 1, c.y, c.z, resolution, conductivity, ref weighted, ref weight);
            AccumulateNeighbor(c.x + 1, c.y, c.z, resolution, conductivity, ref weighted, ref weight);
            AccumulateNeighbor(c.x, c.y - 1, c.z, resolution, conductivity, ref weighted, ref weight);
            AccumulateNeighbor(c.x, c.y + 1, c.z, resolution, conductivity, ref weighted, ref weight);
            AccumulateNeighbor(c.x, c.y, c.z - 1, resolution, conductivity, ref weighted, ref weight);
            AccumulateNeighbor(c.x, c.y, c.z + 1, resolution, conductivity, ref weighted, ref weight);

            float jacobi = weighted / math.max(0.0001f, weight);
            float next = current + (jacobi - current) * omega;
            float ambientAbs = math.abs(ambient) + 1f;
            float stableLimit = math.max(1f, math.max(ambientAbs, maxStable)) * 4f;
            if (!math.isfinite(next) || math.abs(next) > stableLimit)
            {
                nonFinite = !math.isfinite(next);
                divergent = true;
                next = current;
            }

            float residual = math.abs(next - current);
            maxResidual = math.max(maxResidual, math.max(0f, residual));
            current = next;

            current = math.lerp(current, ambient, dissipation);
            ThermalCellDTO output = currentCell;
            output.TemperatureCelsius = math.clamp(current, -273.15f, maxStable);
            output.ConvectionVelocityY = math.max(0f, (output.TemperatureCelsius - ambient) * convectionSpeed);
            output.Flags = (output.Flags & AbyssalThermalMath.CellFlagHullInsulated) | (injected.Flags & AbyssalThermalMath.CellFlagInjected);
            if (divergent)
                output.Flags |= AbyssalThermalMath.CellFlagDivergent;

            if (!math.isfinite(output.TemperatureCelsius))
            {
                output.TemperatureCelsius = ambient;
                output.Flags |= AbyssalThermalMath.CellFlagNaN;
                nonFinite = true;
            }

            Back[index] = output;
            float sampledResidual = math.max(0f, AbyssalThermalMath.FiniteOr(maxResidual, 1f));
            if (nonFinite)
                sampledResidual = math.max(sampledResidual, 1f);
            if (sampledResidual > 0f)
            {
                int slotCount = math.clamp(ResidualSlotCount, 1, AbyssalThermalMath.ResidualThreadSlotCount);
                int slot = math.clamp(ThreadIndex, 0, slotCount - 1);
                ref ThermalResidualSlot64 slotRef = ref UnsafeUtility.AsRef<ThermalResidualSlot64>(ResidualSamples + slot);
                slotRef.MaxResidualFloat = math.max(slotRef.MaxResidualFloat, sampledResidual);
                if (nonFinite)
                    slotRef.FaultFlags |= AbyssalThermalMath.ResidualSlotFaultNonFinite;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AccumulateNeighbor(int x, int y, int z, int3 resolution, float centerConductivity, ref float weighted, ref float weight)
        {
            if ((uint)x >= (uint)resolution.x || (uint)y >= (uint)resolution.y || (uint)z >= (uint)resolution.z)
                return;

            int neighborIndex = AbyssalThermalMath.Index(x, y, z, resolution);
            ThermalCellDTO neighbor = Front[neighborIndex];
            float waterConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(Tuning.WaterThermalConductivity, 0.18f));
            float neighborConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(neighbor.ThermalConductivity, waterConductivity));
            float pairConductivity = math.max(0.0001f, math.min(centerConductivity, neighborConductivity) * waterConductivity);
            float ambient = AbyssalThermalMath.FiniteOr(Tuning.AmbientTemperatureCelsius, 0f);
            weighted += AbyssalThermalMath.FiniteOr(neighbor.TemperatureCelsius, ambient) * pairConductivity;
            weight += pairConductivity;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ThermalSolverResidualReductionJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalSolverConvergenceStateDTO* SolverState;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalResidualSlot64* ResidualSamples;
        public float TargetTolerance;
        public float BaseOmega;
        public int ResidualSlotCount;
        public byte FinalIteration;

        public void Execute()
        {
            ref ThermalSolverConvergenceStateDTO state = ref UnsafeUtility.AsRef<ThermalSolverConvergenceStateDTO>(SolverState);
            ushort flags = state.FaultFlags;
            const ushort terminalFlags = (ushort)(
                AbyssalThermalMath.SolverFlagConverged |
                AbyssalThermalMath.SolverFlagDivergent |
                AbyssalThermalMath.SolverFlagNonFinite);
            if ((flags & terminalFlags) != 0)
                return;

            float maxResidual = 0f;
            bool nonFiniteResidual = false;
            int slotCount = math.clamp(ResidualSlotCount, 1, AbyssalThermalMath.ResidualThreadSlotCount);
            for (int i = 0; i < slotCount; i++)
            {
                ThermalResidualSlot64 slot = ResidualSamples[i];
                float residual = slot.MaxResidualFloat;
                if ((slot.FaultFlags & AbyssalThermalMath.ResidualSlotFaultNonFinite) != 0u ||
                    !math.isfinite(residual) ||
                    residual >= float.MaxValue * 0.5f)
                {
                    nonFiniteResidual = true;
                    maxResidual = math.max(maxResidual, 1f);
                    break;
                }

                maxResidual = math.max(maxResidual, math.max(0f, residual));
            }

            float tolerance = math.max(0.0001f, AbyssalThermalMath.FiniteOr(TargetTolerance, 0.001f));
            float baseOmega = math.clamp(AbyssalThermalMath.FiniteOr(BaseOmega, 1f), 0.55f, 1f);
            float previous = AbyssalThermalMath.FiniteOr(state.PreviousResidualFloat, maxResidual);
            bool previousValid = state.IterationCount > 0 && previous < float.MaxValue * 0.5f;
            bool grew = previousValid && maxResidual > math.max(previous + tolerance * 0.25f, previous * 1.08f);
            bool runaway = previousValid && maxResidual > math.max(2f, previous * 2f);
            float omega = math.clamp(AbyssalThermalMath.FiniteOr(state.Omega, baseOmega), 0.55f, 1f);

            if (nonFiniteResidual)
            {
                flags = (ushort)(flags | AbyssalThermalMath.SolverFlagNonFinite | AbyssalThermalMath.SolverFlagDivergent);
                omega = 0.55f;
            }
            else if (runaway)
            {
                flags = (ushort)(flags | AbyssalThermalMath.SolverFlagDivergent);
                omega = 0.55f;
            }
            else if (grew)
            {
                omega = math.max(0.55f, omega * 0.86f);
            }
            else
            {
                omega = math.min(baseOmega, omega + (baseOmega - omega) * 0.125f);
            }

            if (!nonFiniteResidual && maxResidual <= tolerance)
                flags = (ushort)(flags | AbyssalThermalMath.SolverFlagConverged);
            else if (FinalIteration != 0)
                flags = (ushort)(flags | AbyssalThermalMath.SolverFlagMaxIterations);

            state.MaxResidualFloat = maxResidual;
            state.PreviousResidualFloat = maxResidual;
            state.Omega = omega;
            state.IterationCount = (ushort)math.min(ushort.MaxValue, state.IterationCount + 1);
            state.FaultFlags = flags;
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
            int3 resolution = AbyssalThermalMath.SafeResolution(Tuning.GridResolution);
            float cellSize = math.max(0.001f, AbyssalThermalMath.FiniteOr(Tuning.CellSizeMeters, 8f));
            float ambient = AbyssalThermalMath.FiniteOr(Tuning.AmbientTemperatureCelsius, 0f);
            float waterConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(Tuning.WaterThermalConductivity, 0.18f));
            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            float3 grid = local / cellSize;
            int3 baseCell = (int3)math.floor(grid);
            float3 fraction = math.frac(grid);
            int3 nearestCell = new int3(
                AbyssalThermalMath.PositiveModulo(baseCell.x, resolution.x),
                AbyssalThermalMath.PositiveModulo(baseCell.y, resolution.y),
                AbyssalThermalMath.PositiveModulo(baseCell.z, resolution.z));
            int cellIndex = AbyssalThermalMath.Index(nearestCell.x, nearestCell.y, nearestCell.z, resolution);
            ThermalCellDTO nearest = Cells[cellIndex];

            float temperature = nearest.TemperatureCelsius;
            float convection = nearest.ConvectionVelocityY;
            float conductivity = nearest.ThermalConductivity;
            uint flags = nearest.Flags;
            SampleTrilinear(baseCell, fraction, resolution, out float triTemperature, out float triConvection, out float triConductivity);
            temperature = triTemperature;
            convection = triConvection;
            conductivity = triConductivity;

            if (!math.isfinite(temperature))
            {
                temperature = ambient;
                flags |= AbyssalThermalMath.CellFlagNaN;
            }

            if (!math.isfinite(convection))
                convection = 0f;

            if (!math.isfinite(conductivity))
                conductivity = waterConductivity;

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
        private void SampleTrilinear(int3 baseCell, float3 fraction, int3 resolution, out float temperature, out float convection, out float conductivity)
        {
            ThermalCellDTO c000 = ReadCell(baseCell.x, baseCell.y, baseCell.z, resolution);
            ThermalCellDTO c100 = ReadCell(baseCell.x + 1, baseCell.y, baseCell.z, resolution);
            ThermalCellDTO c010 = ReadCell(baseCell.x, baseCell.y + 1, baseCell.z, resolution);
            ThermalCellDTO c110 = ReadCell(baseCell.x + 1, baseCell.y + 1, baseCell.z, resolution);
            ThermalCellDTO c001 = ReadCell(baseCell.x, baseCell.y, baseCell.z + 1, resolution);
            ThermalCellDTO c101 = ReadCell(baseCell.x + 1, baseCell.y, baseCell.z + 1, resolution);
            ThermalCellDTO c011 = ReadCell(baseCell.x, baseCell.y + 1, baseCell.z + 1, resolution);
            ThermalCellDTO c111 = ReadCell(baseCell.x + 1, baseCell.y + 1, baseCell.z + 1, resolution);

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
        private ThermalCellDTO ReadCell(int x, int y, int z, int3 resolution)
        {
            int ix = AbyssalThermalMath.PositiveModulo(x, resolution.x);
            int iy = AbyssalThermalMath.PositiveModulo(y, resolution.y);
            int iz = AbyssalThermalMath.PositiveModulo(z, resolution.z);
            return Cells[AbyssalThermalMath.Index(ix, iy, iz, resolution)];
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
            int3 resolution = AbyssalThermalMath.SafeResolution(Tuning.GridResolution);
            int resolutionCellCount = math.max(1, resolution.x * resolution.y * resolution.z);
            int cellCount = math.clamp(Tuning.ActiveCellCount, 0, resolutionCellCount);
            long byteCount = (long)cellCount * UnsafeUtility.SizeOf<ThermalCellDTO>();
            UnsafeUtility.MemMove(Scratch, Cells, byteCount);

            ThermalCellDTO ambient;
            ambient.TemperatureCelsius = AbyssalThermalMath.FiniteOr(Tuning.AmbientTemperatureCelsius, 0f);
            ambient.ThermalConductivity = math.max(0.0001f, AbyssalThermalMath.FiniteOr(Tuning.WaterThermalConductivity, 0.18f));
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
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalSolverConvergenceStateDTO* SolverState;
        public ThermalGridTuningDTO Tuning;
        public float SolverMicroseconds;
        public uint Frame;
        public uint ExtraFlags;

        public void Execute()
        {
            int3 resolution = AbyssalThermalMath.SafeResolution(Tuning.GridResolution);
            int resolutionCellCount = math.max(1, resolution.x * resolution.y * resolution.z);
            int cellCount = math.clamp(Tuning.ActiveCellCount, 0, math.min(AbyssalThermalMath.MaxSafeCellCount, resolutionCellCount));
            float ambient = AbyssalThermalMath.FiniteOr(Tuning.AmbientTemperatureCelsius, 0f);
            float dissipation = math.saturate(AbyssalThermalMath.FiniteOr(Tuning.DissipationPerStep, 0.0025f));
            float maxTemp = ambient;
            float energyBefore = 0f;
            float energyAfter = 0f;
            uint flags = ExtraFlags;
            uint nanIndex = 0u;

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
                    beforeTemp = ambient;
                }

                if (!math.isfinite(afterTemp) || (afterCell.Flags & AbyssalThermalMath.CellFlagNaN) != 0u)
                {
                    flags |= AbyssalThermalMath.TelemetryFlagNaN;
                    nanIndex = (uint)i;
                    afterTemp = ambient;
                }
                if ((afterCell.Flags & AbyssalThermalMath.CellFlagDivergent) != 0u)
                {
                    flags |= AbyssalThermalMath.TelemetryFlagDivergent;
                    nanIndex = (uint)i;
                }

                maxTemp = math.max(maxTemp, afterTemp);
                energyBefore += beforeTemp;
                energyAfter += afterTemp;
            }

            float ambientEnergy = ambient * cellCount;
            float dissipatedBudget = math.abs(energyBefore - ambientEnergy) * dissipation * 1.5f;
            float driftTolerance = math.max(1f, dissipatedBudget + (math.abs(energyBefore) * 0.01f));
            if (math.abs(energyAfter - energyBefore) > driftTolerance)
                flags |= AbyssalThermalMath.TelemetryFlagEnergyDrift;
            ThermalSolverConvergenceStateDTO solverState = SolverState[0];
            if ((solverState.FaultFlags & AbyssalThermalMath.SolverFlagNonFinite) != 0)
                flags |= AbyssalThermalMath.TelemetryFlagNaN;
            if ((solverState.FaultFlags & AbyssalThermalMath.SolverFlagDivergent) != 0)
                flags |= AbyssalThermalMath.TelemetryFlagDivergent;
            if ((solverState.FaultFlags & AbyssalThermalMath.SolverFlagMaxIterations) != 0)
                flags |= AbyssalThermalMath.TelemetryFlagMaxIterations;

            int ringIndex = (int)(Frame % AbyssalThermalMath.TelemetryCapacity);
            ThermalTelemetryEntry entry;
            entry.MaxTemperatureCelsius = maxTemp;
            entry.EnergyBefore = energyBefore;
            entry.EnergyAfter = energyAfter;
            entry.SolverMicroseconds = SolverMicroseconds;
            entry.GridOriginAup = Tuning.GridOriginAup;
            entry.Frame = Frame;
            entry.Flags = flags;
            entry.ActiveSourceCount = (uint)math.clamp(*SourceCount, 0, AbyssalThermalMath.MaxThermalSourceCapacity);
            entry.JacobiIterations = (uint)(solverState.IterationCount > 0 ? solverState.IterationCount : math.max(1, Tuning.JacobiIterations));
            entry.NaNCellIndex = nanIndex;
            entry.ActiveResolution = (uint)resolution.x;
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
