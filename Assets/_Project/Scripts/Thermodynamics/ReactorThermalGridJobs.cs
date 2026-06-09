using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Gameplay.AirlockPressurization;
using Hecton8.Power;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using FluidCompartmentDTO = global::Hecton8.Core.Contracts.Physics.FluidCompartmentDTO;

namespace Hecton8.Thermodynamics
{
    public static unsafe class ReactorThermalMath
    {
        public const int TelemetryCapacity = 300;
        public const int MaxReactors = 16;
        public const int MaxProfiles = 16;
        public const int CsvScratchBytes = 4096;
        public const uint SourceHash = 0x53333337u; // S337
        public const uint DamageTypeReactorMeltdown = 0x524D454Cu; // RMEL
        public const uint ProfileHashDefault = 0x52505448u; // RPTH
        public const uint TelemetryFlagNonFinite = 1u << 0;
        public const uint TelemetryFlagOutOfGrid = 1u << 1;
        public const uint TelemetryFlagMeltdown = 1u << 2;
        public const uint TelemetryFlagMockLoad = 1u << 3;
        public const uint TelemetryFlagCostOverBudget = 1u << 4;
        public const uint TelemetryFlagSignalOverflowRisk = 1u << 5;
        public const uint TelemetryFlagTimingProxy = 1u << 6;
        public const uint TelemetryFlagNoCoolant = 1u << 7;
        public const uint TelemetryFlagAtomicAbort = 1u << 8;
        public const uint SourceHashShinobu342 = 0x53333432u; // S342
        public const uint ProfileHashNuclearDefault = 0x4E524854u; // NRHT
        private const double DefaultSeaLevelAupY = 14.02d;
        private const float LengthEpsilonSq = 0.000000000001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteOr(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastLengthFromSq(float lengthSq)
        {
            if (!math.isfinite(lengthSq))
                return float.NaN;

            return lengthSq * math.rsqrt(math.max(lengthSq, LengthEpsilonSq));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSeaLevelDepthMeters(double3 aup, double seaLevelAupY)
        {
            double resolvedSeaLevelAupY = math.isfinite(seaLevelAupY) &&
                                         math.abs(seaLevelAupY) > 0.0001d &&
                                         math.abs(seaLevelAupY) <= 1000d
                ? seaLevelAupY
                : DefaultSeaLevelAupY;
            return math.isfinite(aup.y)
                ? (float)math.max(0d, resolvedSeaLevelAupY - aup.y)
                : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MixHash(uint hash, uint value)
        {
            hash = hash == 0u ? 2166136261u : hash;
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
        public static int ResolveInjectionDiameter(float globalQualityWeight)
        {
            float quality = math.saturate(FiniteOr(globalQualityWeight, 1f));
            int shell = math.clamp((int)math.round(quality * quality * (3f - 2f * quality)), 0, 1);
            return 1 + (shell << 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveContinuousKernelRadius(float globalQualityWeight)
        {
            float quality = math.saturate(FiniteOr(globalQualityWeight, 1f));
            return math.lerp(0.51f, 1.75f, quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMapAupToCell(double3 aup, double3 originAup, float cellSizeMeters, int3 resolution, out int3 cell)
        {
            double3 localDouble = aup - originAup;
            float safeCell = math.max(0.001f, FiniteOr(cellSizeMeters, 8f));
            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            int3 raw = (int3)math.floor(local * math.rcp(safeCell));
            bool inside = math.all(raw >= int3.zero) && math.all(raw < resolution);
            cell = math.clamp(raw, int3.zero, math.max(int3.zero, resolution - 1));
            return inside && math.all(math.isfinite(local));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CellKernelWeight(int3 offset, float radiusCells, float globalQualityWeight)
        {
            float quality = math.saturate(FiniteOr(globalQualityWeight, 1f));
            float lengthSq = (offset.x * offset.x) + (offset.y * offset.y) + (offset.z * offset.z);
            float distance = FastLengthFromSq(lengthSq);
            int manhattan = math.abs(offset.x) + math.abs(offset.y) + math.abs(offset.z);
            float axialMask = math.step((float)manhattan, 1.5f);
            float diagonalShell = math.smoothstep(0.55f, 0.95f, quality);
            float shellMask = math.max(axialMask, diagonalShell);
            return math.saturate(1f - distance * math.rcp(math.max(0.0001f, radiusCells))) * shellMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AtomicAddFloat(float* target, float delta)
        {
            if (!math.isfinite(delta) || delta == 0f || target == null)
                return;

            int* bits = (int*)target;
            int oldBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
            do
            {
                float oldValue = math.asfloat((uint)oldBits);
                float nextValue = math.isfinite(oldValue) ? oldValue + delta : delta;
                int nextBits = unchecked((int)math.asuint(nextValue));
                int observed = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, oldBits);
                if (observed == oldBits)
                    return;

                oldBits = observed;
            } while (true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AtomicAddFloatClamped(float* target, float delta, float min, float max, out float applied)
        {
            applied = 0f;
            if (!math.isfinite(delta) || target == null || !math.isfinite(min) || !math.isfinite(max) || max < min)
                return false;

            int* bits = (int*)target;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int oldBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
                float oldValue = math.asfloat(oldBits);
                if (!math.isfinite(oldValue))
                    return false;

                float nextValue = math.clamp(oldValue + delta, min, max);
                int nextBits = math.asint(nextValue);
                int observed = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, oldBits);
                if (observed == oldBits)
                {
                    applied = nextValue - oldValue;
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AtomicReadFloat(float* target)
        {
            if (target == null)
                return 0f;

            int* bits = (int*)target;
            int observedBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
            float value = math.asfloat(observedBits);
            return math.isfinite(value) ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AtomicOrUInt(uint* target, uint mask)
        {
            if (mask == 0u || target == null)
                return;

            int* bits = (int*)target;
            int oldBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
            do
            {
                int nextBits = oldBits | unchecked((int)mask);
                int observed = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, oldBits);
                if (observed == oldBits)
                    return;

                oldBits = observed;
            } while (true);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockReactorLoadJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorStateDTO* Reactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorKinematicStateDTO* Kinematics;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* ReactorCount;
        public ReactorThermalTuningDTO Tuning;
        public ThermalGridTuningDTO GridTuning;
        public uint Frame;

        public void Execute(int index)
        {
            int desired = math.clamp(Tuning.MockReactorCount, 1, ReactorThermalMath.MaxReactors);
            if (index == 0)
                *ReactorCount = desired;

            if ((uint)index >= (uint)ReactorThermalMath.MaxReactors)
                return;

            if (index >= desired)
            {
                Reactors[index] = default;
                Kinematics[index] = default;
                return;
            }

            uint hash = ReactorThermalMath.MixHash(ReactorThermalMath.SourceHash, (uint)(index + 1));
            float phase = math.frac((Frame * 0.0037f) + (index * 0.271f));
            float overload01 = math.smoothstep(0f, 1f, 1f - math.abs(phase * 2f - 1f));
            float baseCore = math.max(300f, ReactorThermalMath.FiniteOr(Tuning.MockCoreTempCelsius, 720f));
            float mockPhase = phase * 6.283185f;
            MathLodApproximation.ApproxSinCosBhaskara(mockPhase, out float mockSin, out float mockCos);
            ReactorStateDTO reactor = default;
            reactor.CurrentCoreTempCelsius = math.clamp(math.lerp(baseCore, 2000f, overload01) + mockSin * 35f, 300f, 2050f);
            reactor.TargetPowerOutputMW = math.max(0.1f, ReactorThermalMath.FiniteOr(Tuning.MockPowerMW, 14f) * math.lerp(0.82f, 1.18f, phase));
            reactor.ThermalDissipationRate = math.max(0.0001f, ReactorThermalMath.FiniteOr(Tuning.MockThermalDissipationRate, 0.08f));
            reactor.ReactorHashID = hash;
            reactor.Flags = ReactorStateDTO.FlagActive | ReactorStateDTO.FlagMock;
            Reactors[index] = reactor;

            float cell = math.max(0.001f, ReactorThermalMath.FiniteOr(GridTuning.CellSizeMeters, 8f));
            int3 resolution = AbyssalThermalMath.SafeResolution(GridTuning.GridResolution);
            float spanX = cell * math.max(1, resolution.x - 4);
            float spanZ = cell * math.max(1, resolution.z - 4);
            float x = cell * 2f + spanX * ((index + 1f) / (desired + 1f));
            float y = cell * math.lerp(0.35f, 0.65f, math.frac(index * 0.41f));
            float z = cell * 2f + spanZ * math.frac(0.33f + index * 0.29f);
            ReactorKinematicStateDTO kinematic = default;
            kinematic.Aup = GridTuning.GridOriginAup + new double3(x, y, z);
            kinematic.LinearVelocity = new float3(mockSin * 3.5f, 0f, mockCos * 2.25f);
            kinematic.ReactorHashID = hash;
            kinematic.EntityHashID = hash;
            kinematic.Flags = ReactorStateDTO.FlagMock;
            Kinematics[index] = kinematic;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockThermalRunawayJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BaseReactorStateDTO* Reactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorKinematicStateDTO* Kinematics;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* ReactorCount;
        public NuclearReactorThermalTuningDTO Tuning;
        public ThermalGridTuningDTO GridTuning;
        public uint Frame;

        public void Execute(int index)
        {
            int desired = math.clamp(Tuning.MockRunawayCount, 1, ReactorThermalMath.MaxReactors);
            if (index == 0)
                *ReactorCount = desired;

            if ((uint)index >= (uint)ReactorThermalMath.MaxReactors)
                return;

            if (index >= desired)
            {
                Reactors[index] = default;
                if (Kinematics != null)
                    Kinematics[index] = default;
                return;
            }

            uint reactorHash = ReactorThermalMath.MixHash(ReactorThermalMath.SourceHashShinobu342, (uint)(index + 1));
            uint powerHash = ReactorThermalMath.MixHash(0x504E4F44u, (uint)(index + 1)); // PNOD
            uint roomHash = ReactorThermalMath.MixHash(0x524D434Cu, (uint)(index + 1)); // RMCL
            float phase = math.frac((Frame * 0.0029f) + (index * 0.211f));
            float runaway01 = math.smoothstep(0.15f, 1f, phase);

            BaseReactorStateDTO reactor = default;
            reactor.PowerNodeHashID = powerHash;
            reactor.FluidRoomHashID = roomHash;
            reactor.CoreTemperatureCelsius = math.lerp(900f, 2650f, runaway01);
            reactor.FuelRemainingScalar = 1f;
            reactor.ControlRodInsertion01 = math.lerp(0.18f, 0.02f, runaway01);
            reactor.ReactorFlags = BaseReactorStateDTO.FlagActive | BaseReactorStateDTO.FlagMock;
            Reactors[index] = reactor;

            if (Kinematics == null)
                return;

            float cell = math.max(0.001f, ReactorThermalMath.FiniteOr(GridTuning.CellSizeMeters, 8f));
            int3 resolution = AbyssalThermalMath.SafeResolution(GridTuning.GridResolution);
            float spanX = cell * math.max(1, resolution.x - 4);
            float spanZ = cell * math.max(1, resolution.z - 4);
            ReactorKinematicStateDTO kinematic = default;
            kinematic.Aup = GridTuning.GridOriginAup + new double3(
                cell * 2f + spanX * ((index + 1f) / (desired + 1f)),
                cell * math.lerp(0.35f, 0.65f, math.frac(index * 0.37f)),
                cell * 2f + spanZ * math.frac(0.22f + index * 0.31f));
            kinematic.ReactorHashID = reactorHash;
            kinematic.EntityHashID = reactorHash;
            kinematic.Flags = BaseReactorStateDTO.FlagMock;
            Kinematics[index] = kinematic;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateFissionReactionJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BaseReactorStateDTO* Reactors;
        public NuclearReactorThermalTuningDTO Tuning;
        public int ReactorCount;
        public int ReactorCapacity;
        public float DeltaTime;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ReactorCapacity || (uint)index >= (uint)ReactorThermalMath.MaxReactors)
                return;

            int count = math.clamp(ReactorCount, 0, math.min(ReactorCapacity, ReactorThermalMath.MaxReactors));
            if (index >= count)
                return;

            ref BaseReactorStateDTO reactor = ref UnsafeUtility.AsRef<BaseReactorStateDTO>(Reactors + index);
            uint flags = reactor.ReactorFlags;
            if ((flags & BaseReactorStateDTO.FlagActive) == 0u ||
                (flags & (BaseReactorStateDTO.FlagMeltdown | BaseReactorStateDTO.FlagScrammed)) != 0u)
            {
                return;
            }

            float dt = math.clamp(ReactorThermalMath.FiniteOr(DeltaTime, 1f / 60f), 0.0001f, 0.25f);
            float heatCapacity = math.max(1f, ReactorThermalMath.FiniteOr(Tuning.CoreHeatCapacityJoulesPerCelsius, 1250000f));
            float baseHeat = math.max(0f, ReactorThermalMath.FiniteOr(Tuning.BaseFissionHeatJoulesPerSecond, 42000000f));
            float fuel = math.saturate(ReactorThermalMath.FiniteOr(reactor.FuelRemainingScalar, 1f));
            float rods01 = math.saturate(ReactorThermalMath.FiniteOr(reactor.ControlRodInsertion01, 1f));
            float controlGain = math.saturate(1f - rods01);
            float generatedJoules = baseHeat * fuel * controlGain * dt;
            float coreTemp = ReactorThermalMath.FiniteOr(reactor.CoreTemperatureCelsius, Tuning.AmbientCoolantTempCelsius);
            float nextCore = coreTemp + generatedJoules * math.rcp(heatCapacity);
            float fuelBurn = math.max(0f, ReactorThermalMath.FiniteOr(Tuning.FuelBurnPerMegawattSecond, 0.00000025f));
            float nextFuel = math.max(0f, fuel - (generatedJoules * 0.000001f * fuelBurn));

            if (!math.isfinite(nextCore + nextFuel + generatedJoules))
            {
                reactor.ReactorFlags = flags | BaseReactorStateDTO.FlagNonFinite;
                return;
            }

            reactor.CoreTemperatureCelsius = nextCore;
            reactor.FuelRemainingScalar = nextFuel;
            reactor.ControlRodInsertion01 = rods01;
            reactor.ReactorFlags = flags;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct HydrateBaseReactorFromLegacyJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BaseReactorStateDTO* BaseReactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorStateDTO* LegacyReactors;
        public int ReactorCount;
        public int ReactorCapacity;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ReactorCapacity || (uint)index >= (uint)ReactorThermalMath.MaxReactors)
                return;

            int count = math.clamp(ReactorCount, 0, math.min(ReactorCapacity, ReactorThermalMath.MaxReactors));
            if (index >= count)
                return;

            ref BaseReactorStateDTO dst = ref UnsafeUtility.AsRef<BaseReactorStateDTO>(BaseReactors + index);
            if ((dst.ReactorFlags & BaseReactorStateDTO.FlagActive) != 0u)
                return;

            ReactorStateDTO src = LegacyReactors[index];
            if ((src.Flags & ReactorStateDTO.FlagActive) == 0u)
                return;

            uint hash = src.ReactorHashID != 0u
                ? src.ReactorHashID
                : ReactorThermalMath.MixHash(ReactorThermalMath.SourceHashShinobu342, (uint)(index + 1));
            dst.PowerNodeHashID = hash;
            dst.FluidRoomHashID = hash;
            dst.CoreTemperatureCelsius = ReactorThermalMath.FiniteOr(src.CurrentCoreTempCelsius, 900f);
            dst.FuelRemainingScalar = 1f;
            dst.ControlRodInsertion01 = 0.35f;
            dst.ReactorFlags = BaseReactorStateDTO.FlagActive |
                               ((src.Flags & ReactorStateDTO.FlagMock) != 0u ? BaseReactorStateDTO.FlagMock : 0u) |
                               ((src.Flags & ReactorStateDTO.FlagMeltdown) != 0u ? BaseReactorStateDTO.FlagMeltdown : 0u);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateThermoelectricPowerJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BaseReactorStateDTO* Reactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorStateDTO* LegacyReactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorKinematicStateDTO* Kinematics;
        [NativeDisableUnsafePtrRestriction, NoAlias] public PowerNodeDTO* PowerNodes;
        [NativeDisableUnsafePtrRestriction, NoAlias] public FluidCompartmentDTO* FluidCompartments;
        [NativeDisableUnsafePtrRestriction, NoAlias] public AirlockStateDTO* Airlocks;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorPowerInjectionDTO* PowerLedger;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorThermalVisualDTO* Visuals;

        public NuclearReactorThermalTuningDTO Tuning;
        public ThermalGridTuningDTO GridTuning;
        public int ReactorCount;
        public int ReactorCapacity;
        public int PowerNodeCount;
        public int FluidCompartmentCount;
        public int AirlockCount;
        public float DeltaTime;
        public uint Frame;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ReactorCapacity || (uint)index >= (uint)ReactorThermalMath.MaxReactors)
                return;

            ReactorPowerInjectionDTO ledger = default;
            ReactorThermalVisualDTO visual = default;
            int count = math.clamp(ReactorCount, 0, math.min(ReactorCapacity, ReactorThermalMath.MaxReactors));
            if (index >= count)
            {
                WriteOutputs(index, in ledger, in visual);
                return;
            }

            ref BaseReactorStateDTO reactor = ref UnsafeUtility.AsRef<BaseReactorStateDTO>(Reactors + index);
            uint flags = reactor.ReactorFlags;
            if ((flags & BaseReactorStateDTO.FlagActive) == 0u)
            {
                WriteOutputs(index, in ledger, in visual);
                return;
            }

            double3 aup;
            uint reactorHash;
            ResolvePose(index, out aup, out reactorHash);

            float dt = math.clamp(ReactorThermalMath.FiniteOr(DeltaTime, 1f / 60f), 0.0001f, 0.25f);
            float heatCapacity = math.max(1f, ReactorThermalMath.FiniteOr(Tuning.CoreHeatCapacityJoulesPerCelsius, 1250000f));
            float safeCore = math.max(100f, ReactorThermalMath.FiniteOr(Tuning.SafeCoreTempCelsius, 1100f));
            float meltdownCore = math.max(safeCore + 1f, ReactorThermalMath.FiniteOr(Tuning.MeltdownCoreTempCelsius, 2500f));
            float coreTemp = ReactorThermalMath.FiniteOr(reactor.CoreTemperatureCelsius, Tuning.AmbientCoolantTempCelsius);
            float coolantLiters = ResolveCoolantLiters(reactor.FluidRoomHashID);
            float coolant01 = math.saturate(coolantLiters * math.rcp(math.max(1f, ReactorThermalMath.FiniteOr(Tuning.CoolantLitersForNominalColdSink, 4000f))));
            float coldCelsius = math.lerp(
                ReactorThermalMath.FiniteOr(Tuning.DryCoolantTempCelsius, 3200f),
                ReactorThermalMath.FiniteOr(Tuning.AmbientCoolantTempCelsius, 18f),
                coolant01);
            flags = coolantLiters <= 0.001f ? flags | BaseReactorStateDTO.FlagNoCoolant : flags & ~BaseReactorStateDTO.FlagNoCoolant;

            float hotK = math.max(1f, coreTemp + 273.15f);
            float coldK = math.max(1f, coldCelsius + 273.15f);
            float carnot01 = hotK > coldK ? math.saturate(1f - coldK * math.rcp(hotK)) : 0f;
            float thermalHeadroomJoules = math.max(0f, (coreTemp - coldCelsius) * heatCapacity);
            float drawJoules = math.min(thermalHeadroomJoules, math.max(0f, ReactorThermalMath.FiniteOr(Tuning.TurbineThermalDrawWatts, 30000000f)) * dt);
            float generatedWatts = drawJoules * math.rcp(dt) * carnot01;
            float nextCore = math.max(coldCelsius, coreTemp - drawJoules * math.rcp(heatCapacity));
            float boiledLiters = 0f;
            uint atomicAbort = 0u;

            if (nextCore > 100f)
            {
                float latent = math.max(1f, ReactorThermalMath.FiniteOr(Tuning.LatentHeatJoulesPerLiter, 2256000f));
                float excessJoules = math.max(0f, (nextCore - 100f) * heatCapacity);
                float desiredLiters = math.min(excessJoules * math.rcp(latent), math.max(0f, Tuning.MaxBoilOffLitersPerSecond) * dt);
                if (desiredLiters > 0.0001f)
                {
                    boiledLiters = DeductCoolantWaterLiters(reactor.FluidRoomHashID, desiredLiters, ref atomicAbort);
                    nextCore = math.max(100f, nextCore - (boiledLiters * latent * math.rcp(heatCapacity)));
                }
            }

            if (!math.isfinite(nextCore + generatedWatts + carnot01 + boiledLiters))
            {
                flags |= BaseReactorStateDTO.FlagNonFinite;
                nextCore = meltdownCore;
                generatedWatts = 0f;
                carnot01 = 0f;
            }

            if (atomicAbort != 0u)
                flags |= BaseReactorStateDTO.FlagAtomicAbort;

            bool wasMeltdown = (flags & BaseReactorStateDTO.FlagMeltdown) != 0u;
            bool meltdown = nextCore >= meltdownCore || (flags & BaseReactorStateDTO.FlagNonFinite) != 0u;
            if (meltdown)
                flags |= BaseReactorStateDTO.FlagMeltdown;

            reactor.CoreTemperatureCelsius = nextCore;
            reactor.FuelRemainingScalar = math.saturate(ReactorThermalMath.FiniteOr(reactor.FuelRemainingScalar, 0f));
            reactor.ControlRodInsertion01 = math.saturate(ReactorThermalMath.FiniteOr(reactor.ControlRodInsertion01, 1f));
            reactor.ReactorFlags = flags;

            ApplyPowerInjection(reactor.PowerNodeHashID, reactorHash, generatedWatts, dt, flags, out uint powerFlags);
            if ((powerFlags & BaseReactorStateDTO.FlagAtomicAbort) != 0u)
            {
                flags |= BaseReactorStateDTO.FlagAtomicAbort;
                reactor.ReactorFlags = flags;
            }

            uint ledgerFlags = flags | powerFlags;
            if (boiledLiters > 0f)
                ledgerFlags |= ReactorPowerInjectionDTO.FlagCoolantBoiledThisTick;
            if (meltdown && !wasMeltdown)
                ledgerFlags |= ReactorPowerInjectionDTO.FlagMeltdownEnteredThisTick;
            if (meltdown && ShouldEmitMeltdown(index))
                ledgerFlags |= ReactorPowerInjectionDTO.FlagMeltdownSignalTick;

            ledger.PowerNodeHashID = reactor.PowerNodeHashID;
            ledger.GeneratedWatts = generatedWatts;
            ledger.GeneratedWattSeconds = generatedWatts * dt;
            ledger.CarnotEfficiency01 = carnot01;
            ledger.Frame = Frame;
            ledger.ReactorHashID = reactorHash;
            ledger.Flags = ledgerFlags;
            ledger.BoiledLiters = boiledLiters;

            SyncLegacyReactor(index, reactorHash, nextCore, generatedWatts, flags);

            visual.Aup = aup;
            visual.CoreTemperatureCelsius = nextCore;
            visual.GeneratedMegawatts = generatedWatts * 0.000001f;
            visual.CarnotEfficiency01 = carnot01;
            visual.BoiledLiters = boiledLiters;
            visual.Flags = flags;
            visual.ReactorHashID = reactorHash;
            visual.ControlRodInsertion01 = reactor.ControlRodInsertion01;
            visual.FuelRemainingScalar = reactor.FuelRemainingScalar;
            visual.PowerNodeHashID = reactor.PowerNodeHashID;
            visual.FluidRoomHashID = reactor.FluidRoomHashID;

            WriteOutputs(index, in ledger, in visual);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteOutputs(int index, in ReactorPowerInjectionDTO ledger, in ReactorThermalVisualDTO visual)
        {
            if (PowerLedger != null)
                PowerLedger[index] = ledger;
            if (Visuals != null)
                Visuals[index] = visual;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResolvePose(int index, out double3 aup, out uint reactorHash)
        {
            if (Kinematics == null)
            {
                aup = GridTuning.GridOriginAup;
                reactorHash = ReactorThermalMath.MixHash(ReactorThermalMath.SourceHashShinobu342, (uint)(index + 1));
                return;
            }

            ReactorKinematicStateDTO kinematic = Kinematics[index];
            aup = math.all(math.isfinite(kinematic.Aup)) ? kinematic.Aup : GridTuning.GridOriginAup;
            reactorHash = kinematic.ReactorHashID != 0u
                ? kinematic.ReactorHashID
                : ReactorThermalMath.MixHash(ReactorThermalMath.SourceHashShinobu342, (uint)(index + 1));
        }

        private float ResolveCoolantLiters(uint roomHash)
        {
            if (roomHash == 0u)
                return 0f;

            float liters = 0f;
            if (Airlocks != null)
            {
                int airlockLimit = math.min(math.max(0, AirlockCount), 50);
                for (int i = 0; i < airlockLimit; i++)
                {
                    AirlockStateDTO airlock = Airlocks[i];
                    if (airlock.InnerRoomHashID == roomHash || airlock.OuterRoomHashID == roomHash)
                        liters += math.max(0f, ReactorThermalMath.FiniteOr(airlock.CurrentWaterVolumeLiters, 0f));
                }
            }

            if (FluidCompartments != null)
            {
                int fluidLimit = math.min(math.max(0, FluidCompartmentCount), 5000);
                for (int i = 0; i < fluidLimit; i++)
                {
                    FluidCompartmentDTO compartment = FluidCompartments[i];
                    if (compartment.NodeHashID != roomHash)
                        continue;

                    float waterM3 = math.max(0f, ReactorThermalMath.FiniteOr(compartment.CurrentWaterVolume, 0f));
                    liters += waterM3 * 1000f;
                    break;
                }
            }

            return math.max(0f, liters);
        }

        private float DeductCoolantWaterLiters(uint roomHash, float requestedLiters, ref uint atomicAbort)
        {
            if (roomHash == 0u || requestedLiters <= 0f || !math.isfinite(requestedLiters))
                return 0f;

            float remaining = requestedLiters;
            float boiled = 0f;
            if (Airlocks != null)
            {
                int airlockLimit = math.min(math.max(0, AirlockCount), 50);
                for (int i = 0; i < airlockLimit && remaining > 0.0001f; i++)
                {
                    AirlockStateDTO* airlock = Airlocks + i;
                    if (airlock->InnerRoomHashID != roomHash && airlock->OuterRoomHashID != roomHash)
                        continue;

                    float current = math.max(0f, ReactorThermalMath.FiniteOr(ReactorThermalMath.AtomicReadFloat(&airlock->CurrentWaterVolumeLiters), 0f));
                    float request = math.min(current, remaining);
                    if (request <= 0.0001f)
                        continue;

                    if (ReactorThermalMath.AtomicAddFloatClamped(&airlock->CurrentWaterVolumeLiters, -request, 0f, current, out float applied))
                    {
                        float removed = math.max(0f, -applied);
                        boiled += removed;
                        remaining -= removed;
                    }
                    else
                    {
                        atomicAbort = 1u;
                    }
                }
            }

            if (FluidCompartments != null && remaining > 0.0001f)
            {
                int fluidLimit = math.min(math.max(0, FluidCompartmentCount), 5000);
                for (int i = 0; i < fluidLimit && remaining > 0.0001f; i++)
                {
                    FluidCompartmentDTO* compartment = FluidCompartments + i;
                    if (compartment->NodeHashID != roomHash)
                        continue;

                    float currentM3 = math.max(0f, ReactorThermalMath.FiniteOr(ReactorThermalMath.AtomicReadFloat(&compartment->CurrentWaterVolume), 0f));
                    float requestM3 = math.min(currentM3, remaining * 0.001f);
                    if (requestM3 <= 0.0000001f)
                        break;

                    if (ReactorThermalMath.AtomicAddFloatClamped(&compartment->CurrentWaterVolume, -requestM3, 0f, currentM3, out float appliedM3))
                    {
                        float removedLiters = math.max(0f, -appliedM3) * 1000f;
                        boiled += removedLiters;
                        remaining -= removedLiters;
                    }
                    else
                    {
                        atomicAbort = 1u;
                    }

                    break;
                }
            }

            return math.max(0f, boiled);
        }

        private void ApplyPowerInjection(uint nodeHash, uint reactorHash, float generatedWatts, float dt, uint flags, out uint powerFlags)
        {
            powerFlags = 0u;
            if (nodeHash == 0u || generatedWatts <= 0f || PowerNodes == null || PowerNodeCount <= 0)
                return;

            int nodeLimit = math.min(PowerNodeCount, 4096);
            float wattSeconds = math.max(0f, generatedWatts * dt);
            for (int i = 0; i < nodeLimit; i++)
            {
                PowerNodeDTO* node = PowerNodes + i;
                if (node->NodeHash != nodeHash)
                    continue;

                float capacity = math.max(wattSeconds, ReactorThermalMath.FiniteOr(node->MaxCapacity, wattSeconds));
                if (capacity > 0f)
                {
                    if (!ReactorThermalMath.AtomicAddFloatClamped(&node->CurrentStorage, wattSeconds, 0f, capacity, out _))
                        powerFlags |= BaseReactorStateDTO.FlagAtomicAbort;
                }

                float potentialDelta = math.saturate(generatedWatts * 0.00000005f);
                if (potentialDelta > 0f &&
                    !ReactorThermalMath.AtomicAddFloatClamped(&node->Potential, potentialDelta, 0f, 1f, out _))
                {
                    powerFlags |= BaseReactorStateDTO.FlagAtomicAbort;
                }

                ReactorThermalMath.AtomicOrUInt(&node->Flags, PowerGridJacobiConstants.NodeFlagActive | PowerGridJacobiConstants.NodeFlagSource);
                return;
            }

            powerFlags = 0u;
        }

        private void SyncLegacyReactor(int index, uint reactorHash, float coreTemp, float generatedWatts, uint flags)
        {
            if (LegacyReactors == null)
                return;

            ReactorStateDTO legacy = LegacyReactors[index];
            legacy.CurrentCoreTempCelsius = coreTemp;
            legacy.TargetPowerOutputMW = math.max(0f, generatedWatts) * 0.000001f;
            legacy.ThermalDissipationRate = math.max(0f, ReactorThermalMath.FiniteOr(Tuning.ThermalLeakToGrid01, 0.035f));
            legacy.ReactorHashID = reactorHash;
            uint legacyFlags = ReactorStateDTO.FlagActive;
            legacyFlags |= (flags & BaseReactorStateDTO.FlagMock) != 0u ? ReactorStateDTO.FlagMock : 0u;
            legacyFlags |= (flags & BaseReactorStateDTO.FlagMeltdown) != 0u ? ReactorStateDTO.FlagMeltdown : 0u;
            legacyFlags |= (flags & BaseReactorStateDTO.FlagNonFinite) != 0u ? ReactorStateDTO.FlagNonFinite : 0u;
            legacy.Flags = legacyFlags;
            LegacyReactors[index] = legacy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldEmitMeltdown(int index)
        {
            float quality = math.saturate(ReactorThermalMath.FiniteOr(Tuning.GlobalQualityWeight, 1f));
            uint stride = (uint)math.max(1, (int)math.round(math.lerp(4f, 1f, quality)));
            return ((Frame + (uint)index) % stride) == 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct PublishNuclearReactorMeltdownSignalsJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BaseReactorStateDTO* Reactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorPowerInjectionDTO* PowerLedger;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorThermalVisualDTO* Visuals;

        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // The serial publisher runs after the parallel thermodynamic mutation job.
        // It only appends unmanaged packets to pre-initialized SignalBus queues.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Signal emission is separated from IJobParallelFor so queue order is
        // deterministic by ascending reactor index under rollback replay.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // No queue storage is read, retained, or aliased with reactor Vault rows;
        // the only persistent truth remains BaseReactorStateDTO/ledger rows.
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<BaseModuleCompromisedSignal>.ParallelWriter BaseModuleWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> BaseModuleWriterBudget;
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<RadiationSourceSignal>.ParallelWriter RadiationWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> RadiationWriterBudget;
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<CombatDamageSignal>.ParallelWriter DamageWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> DamageWriterBudget;
        public NuclearReactorThermalTuningDTO Tuning;
        public ThermalGridTuningDTO GridTuning;
        public double SeaLevelAupY;
        public int ReactorCount;
        public int ReactorCapacity;
        public uint Frame;

        public void Execute()
        {
            int count = math.clamp(ReactorCount, 0, math.min(ReactorCapacity, ReactorThermalMath.MaxReactors));
            float safeCore = math.max(100f, ReactorThermalMath.FiniteOr(Tuning.SafeCoreTempCelsius, 1100f));
            float meltdownCore = math.max(safeCore + 1f, ReactorThermalMath.FiniteOr(Tuning.MeltdownCoreTempCelsius, 2500f));
            for (int i = 0; i < count; i++)
            {
                BaseReactorStateDTO reactor = Reactors[i];
                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagMeltdown) == 0u)
                    continue;

                ReactorPowerInjectionDTO ledger = PowerLedger != null ? PowerLedger[i] : default;
                uint publishMask = ledger.Flags & (ReactorPowerInjectionDTO.FlagMeltdownEnteredThisTick | ReactorPowerInjectionDTO.FlagMeltdownSignalTick);
                if (publishMask == 0u)
                    continue;

                ReactorThermalVisualDTO visual = Visuals != null ? Visuals[i] : default;
                double3 aup = math.all(math.isfinite(visual.Aup)) ? visual.Aup : GridTuning.GridOriginAup;
                uint reactorHash = visual.ReactorHashID != 0u
                    ? visual.ReactorHashID
                    : (ledger.ReactorHashID != 0u ? ledger.ReactorHashID : ReactorThermalMath.MixHash(ReactorThermalMath.SourceHashShinobu342, (uint)(i + 1)));
                float coreTemp = ReactorThermalMath.FiniteOr(visual.CoreTemperatureCelsius, reactor.CoreTemperatureCelsius);

                bool overflowed = false;
                if ((ledger.Flags & ReactorPowerInjectionDTO.FlagMeltdownEnteredThisTick) != 0u)
                    overflowed |= !EnqueueBaseCompromised(aup, reactorHash, coreTemp, meltdownCore);

                if ((ledger.Flags & ReactorPowerInjectionDTO.FlagMeltdownSignalTick) != 0u)
                {
                    overflowed |= !EnqueueRadiation(aup, reactorHash, coreTemp, meltdownCore);
                    overflowed |= !EnqueueCombatDamage(aup, reactorHash, coreTemp, safeCore);
                }

                if (overflowed)
                {
                    if (PowerLedger != null)
                    {
                        ledger.Flags |= ReactorPowerInjectionDTO.FlagSignalOverflow;
                        PowerLedger[i] = ledger;
                    }
                    reactor.ReactorFlags |= BaseReactorStateDTO.FlagSignalOverflow;
                    Reactors[i] = reactor;
                }
            }
        }

        private bool EnqueueBaseCompromised(double3 aup, uint reactorHash, float coreTemp, float meltdownTemp)
        {
            double3 local = math.all(math.isfinite(aup - GridTuning.GridOriginAup)) ? aup - GridTuning.GridOriginAup : double3.zero;
            float3 center = new float3(
                (float)math.clamp(local.x, -100000d, 100000d),
                (float)math.clamp(local.y, -100000d, 100000d),
                (float)math.clamp(local.z, -100000d, 100000d));
            float stress01 = math.saturate(coreTemp * math.rcp(math.max(1f, meltdownTemp)));
            BaseModuleCompromisedSignal signal = default;
            signal.ModuleCenter = center;
            signal.Stress01 = stress01;
            signal.PeakStress01 = stress01;
            signal.DepthMeters = ReactorThermalMath.ResolveSeaLevelDepthMeters(aup, SeaLevelAupY);
            signal.NodeId = reactorHash;
            signal.ModuleHash = reactorHash;
            signal.Frame = Frame;
            signal.Sequence = Frame ^ reactorHash;
            signal.SourceId = unchecked((ushort)ReactorThermalMath.SourceHashShinobu342);
            signal.Flags = BaseModuleCompromisedSignal.MaxDeformationFlag;
            signal.StressIndex = (byte)math.clamp((int)math.round(stress01 * byte.MaxValue), 0, byte.MaxValue);
            signal.QualityTier = (byte)math.clamp((int)math.round(math.saturate(Tuning.GlobalQualityWeight) * 4f), 0, 4);
            return SignalBus<BaseModuleCompromisedSignal>.TryEnqueueBounded(BaseModuleWriter, BaseModuleWriterBudget, signal);
        }

        private bool EnqueueRadiation(double3 aup, uint reactorHash, float coreTemp, float meltdownTemp)
        {
            RadiationSourceSignal signal = default;
            signal.PositionAup = math.all(math.isfinite(aup)) ? AbsoluteUniversePosition.FromAbsolutePosition(aup) : default;
            float severity = math.max(1f, coreTemp * math.rcp(math.max(1f, meltdownTemp)));
            signal.Intensity = math.max(1f, ReactorThermalMath.FiniteOr(Tuning.RadiationIntensityBase, 48f) * severity);
            signal.RadiusMeters = math.max(1f, ReactorThermalMath.FiniteOr(Tuning.RadiationRadiusMeters, 120f) * ReactorThermalMath.FastLengthFromSq(severity));
            signal.SourceId = unchecked((int)reactorHash);
            signal.Operation = RadiationSourceSignal.OperationUpsert;
            signal.Flags = 0;
            return SignalBus<RadiationSourceSignal>.TryEnqueueBounded(RadiationWriter, RadiationWriterBudget, signal);
        }

        private bool EnqueueCombatDamage(double3 aup, uint reactorHash, float coreTemp, float safeCore)
        {
            CombatDamageSignal signal = default;
            signal.ImpactAup = aup;
            signal.Direction = new float3(0f, 1f, 0f);
            signal.Magnitude = math.max(1f, coreTemp - safeCore);
            signal.DamageType = Tuning.DamageTypeHash != 0u ? Tuning.DamageTypeHash : ReactorThermalMath.DamageTypeReactorMeltdown;
            signal.TargetHash = reactorHash;
            signal.SourceHash = Tuning.SourceHash != 0u ? Tuning.SourceHash : ReactorThermalMath.SourceHashShinobu342;
            signal.Frame = Frame;
            signal.SourceId = unchecked((ushort)ReactorThermalMath.SourceHashShinobu342);
            signal.TargetId = unchecked((ushort)reactorHash);
            signal.Channel = 0;
            signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
            signal.IntegrityDelta = byte.MaxValue;
            return SignalBus<CombatDamageSignal>.TryEnqueueBounded(DamageWriter, DamageWriterBudget, signal);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InjectReactorHeatJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorStateDTO* Reactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorKinematicStateDTO* FallbackKinematics;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Front;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ThermalCellDTO* Injection;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorThermalScratchDTO* Scratch;

        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SignalBus exposes bounded MPSC ring writer lanes. Unity's
        // container safety cannot see the bus owner-phase initialization performed
        // before this job is scheduled, so the write-only writer field is otherwise
        // rejected even though this job never reads from the lane or retains it.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Alternatives considered: (a) direct managed event publication, rejected
        // because it is non-Burst and heap-facing; (b) per-reactor managed callback
        // buffers, rejected because they create cross-domain ownership and frame
        // allocations. A future coalesced scratch-lane publisher can replace this
        // bridge when the shared SignalBus API exposes a first-class job candidate
        // buffer, but this route keeps the current task bounded and deterministic.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: Execute(index) writes at most one thermal and one damage
        // signal per reactor, capped by MaxReactors=16 and gated by frame strides.
        // The owner calls SignalBus<T>.EnsureInitialized in cold setup, and no job
        // reads either lane while this producer handle is live.
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<ThermalStateChangedSignal>.ParallelWriter ThermalWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> ThermalWriterBudget;
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<CombatDamageSignal>.ParallelWriter DamageWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> DamageWriterBudget;
        public ReactorThermalTuningDTO Tuning;
        public ThermalGridTuningDTO GridTuning;
        public int ReactorCount;
        public int ReactorCapacity;
        public float DeltaTime;
        public uint Frame;

        public void Execute(int index)
        {
            ReactorThermalScratchDTO scratch = default;
            if ((uint)index >= (uint)ReactorCapacity || (uint)index >= (uint)ReactorThermalMath.MaxReactors)
            {
                return;
            }

            int count = math.clamp(ReactorCount, 0, math.min(ReactorCapacity, ReactorThermalMath.MaxReactors));
            if (index >= count)
            {
                Scratch[index] = scratch;
                return;
            }

            ref ReactorStateDTO reactor = ref UnsafeUtility.AsRef<ReactorStateDTO>(Reactors + index);
            uint flags = reactor.Flags;
            if ((flags & ReactorStateDTO.FlagActive) == 0u)
            {
                Scratch[index] = scratch;
                return;
            }

            if ((flags & ReactorStateDTO.FlagMeltdown) != 0u)
            {
                scratch.Flags = flags;
                scratch.ReactorHashID = reactor.ReactorHashID;
                scratch.CoreTempCelsius = reactor.CurrentCoreTempCelsius;
                Scratch[index] = scratch;
                return;
            }

            double3 aup;
            float3 velocity;
            uint entityHash;
            ResolvePose(index, in reactor, out aup, out velocity, out entityHash);

            int3 resolution = AbyssalThermalMath.SafeResolution(GridTuning.GridResolution);
            float cellSize = math.max(0.001f, ReactorThermalMath.FiniteOr(GridTuning.CellSizeMeters, 8f));
            if (!ReactorThermalMath.TryMapAupToCell(aup, GridTuning.GridOriginAup, cellSize, resolution, out int3 centerCell))
            {
                reactor.Flags = flags | ReactorStateDTO.FlagOutOfGrid;
                scratch.Flags = ReactorStateDTO.FlagOutOfGrid;
                scratch.ReactorHashID = reactor.ReactorHashID;
                Scratch[index] = scratch;
                return;
            }

            int centerIndex = AbyssalThermalMath.Index(centerCell.x, centerCell.y, centerCell.z, resolution);
            ThermalCellDTO ambientCell = Front[centerIndex];
            float ambientTemp = ReactorThermalMath.FiniteOr(ambientCell.TemperatureCelsius, GridTuning.AmbientTemperatureCelsius);
            float coreTemp = ReactorThermalMath.FiniteOr(reactor.CurrentCoreTempCelsius, ambientTemp);
            float3 finiteVelocity = math.select(float3.zero, velocity, math.isfinite(velocity));
            float speedSq = math.lengthsq(finiteVelocity);
            float convectiveMultiplier = ResolveConvectionMultiplier(speedSq);
            float heatCapacity = math.max(1f, ReactorThermalMath.FiniteOr(Tuning.CoreHeatCapacityJoulesPerCelsius, 1250000f));
            float invHeatCapacity = math.rcp(heatCapacity);
            float baseRate = math.max(0f, ReactorThermalMath.FiniteOr(Tuning.BaseDissipationRate, 0.085f));
            float stateRate = math.max(0f, ReactorThermalMath.FiniteOr(reactor.ThermalDissipationRate, baseRate));
            float dt = math.clamp(ReactorThermalMath.FiniteOr(DeltaTime, 1f / 60f), 0.0001f, 0.2f);
            float generatedJoules = math.max(0f, ReactorThermalMath.FiniteOr(reactor.TargetPowerOutputMW, 0f)) * 1000000f * stateRate * dt;
            float postGenerationCore = coreTemp + generatedJoules * invHeatCapacity;
            float thermalHeadroomJoules = math.max(0f, (postGenerationCore - ambientTemp) * heatCapacity);
            float gradientJoules = thermalHeadroomJoules * baseRate * convectiveMultiplier * dt;
            float coolingJoules = math.min(gradientJoules, thermalHeadroomJoules);
            float totalJoules = math.max(0f, coolingJoules);
            float coreCooling = coolingJoules * invHeatCapacity;
            float nextCore = math.max(ambientTemp, postGenerationCore - coreCooling);

            if (!math.isfinite(totalJoules + generatedJoules + nextCore + speedSq + convectiveMultiplier))
            {
                reactor.Flags = flags | ReactorStateDTO.FlagNonFinite;
                scratch.Flags = ReactorStateDTO.FlagNonFinite;
                scratch.ReactorHashID = reactor.ReactorHashID;
                Scratch[index] = scratch;
                return;
            }

            reactor.CurrentCoreTempCelsius = nextCore;
            flags = reactor.Flags;
            bool meltdown = nextCore >= math.max(Tuning.SafeCoreTempCelsius + 1f, ReactorThermalMath.FiniteOr(Tuning.MeltdownCoreTempCelsius, 1850f));
            if (meltdown)
            {
                flags |= ReactorStateDTO.FlagMeltdown;
                reactor.Flags = flags;
            }

            uint cellWrites = InjectCells(totalJoules, centerCell, resolution, cellSize, GridTuning.GlobalQualityWeight);
            uint thermalSignals = 0u;
            uint damageSignals = 0u;
            if (ShouldEmitThermalSignal(index, nextCore, totalJoules))
            {
                if (EnqueueThermalSignal(in reactor, nextCore, index))
                    thermalSignals = 1u;
                else
                    flags |= ReactorStateDTO.FlagSignalOverflow;
            }

            if (meltdown && ShouldEmitMeltdownSignal(index))
            {
                if (EnqueueMeltdownSignal(aup, entityHash, nextCore))
                    damageSignals = 1u;
                else
                    flags |= ReactorStateDTO.FlagSignalOverflow;
            }
            reactor.Flags = flags;

            scratch.JoulesInjected = totalJoules;
            scratch.CoreCoolingCelsius = coreCooling;
            scratch.CoreTempCelsius = nextCore;
            scratch.SpeedMetersPerSecond = ReactorThermalMath.FastLengthFromSq(math.max(0f, speedSq));
            scratch.ConvectiveMultiplier = convectiveMultiplier;
            scratch.CenterCellIndex = (uint)centerIndex;
            scratch.Flags = flags;
            scratch.ReactorHashID = reactor.ReactorHashID;
            scratch.CellWrites = cellWrites;
            scratch.ThermalSignalCount = thermalSignals;
            scratch.DamageSignalCount = damageSignals;
            uint hash = ReactorThermalMath.MixHash(0u, reactor.ReactorHashID);
            hash = ReactorThermalMath.MixHash(hash, math.asuint(nextCore));
            hash = ReactorThermalMath.MixHash(hash, math.asuint(totalJoules));
            hash = ReactorThermalMath.MixHash(hash, (uint)centerIndex);
            scratch.StateHash = hash;
            Scratch[index] = scratch;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResolvePose(int index, in ReactorStateDTO reactor, out double3 aup, out float3 velocity, out uint entityHash)
        {
            if (FallbackKinematics == null)
            {
                aup = GridTuning.GridOriginAup;
                velocity = float3.zero;
                entityHash = reactor.ReactorHashID;
                return;
            }

            ref readonly ReactorKinematicStateDTO fallback = ref UnsafeUtility.AsRef<ReactorKinematicStateDTO>(FallbackKinematics + index);
            aup = fallback.Aup;
            velocity = fallback.LinearVelocity;
            entityHash = fallback.EntityHashID != 0u ? fallback.EntityHashID : reactor.ReactorHashID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveConvectionMultiplier(float speedSq)
        {
            float speedSafe = math.max(0f, ReactorThermalMath.FiniteOr(speedSq, 0f));
            float forced = math.max(0f, ReactorThermalMath.FiniteOr(Tuning.ForcedConvectionMultiplier, 0.08f));
            float maxMultiplier = math.max(1f, ReactorThermalMath.FiniteOr(Tuning.MaxConvectionMultiplier, 4.5f));
            return math.clamp(1f + speedSafe * forced, 1f, maxMultiplier);
        }

        private uint InjectCells(float joules, int3 centerCell, int3 resolution, float cellSize, float quality)
        {
            if (joules <= 0f)
                return 0u;

            float radiusCells = ReactorThermalMath.ResolveContinuousKernelRadius(quality);
            int halfExtent = math.min(1, ReactorThermalMath.ResolveInjectionDiameter(quality) >> 1);
            float weightSum = 0f;
            for (int z = -halfExtent; z <= halfExtent; z++)
            {
                for (int y = -halfExtent; y <= halfExtent; y++)
                {
                    for (int x = -halfExtent; x <= halfExtent; x++)
                    {
                        int3 cell = centerCell + new int3(x, y, z);
                        if (math.any(cell < int3.zero) || math.any(cell >= resolution))
                            continue;

                        weightSum += ReactorThermalMath.CellKernelWeight(new int3(x, y, z), radiusCells, quality);
                    }
                }
            }

            if (weightSum <= 0f)
                return 0u;

            float cellVolume = cellSize * cellSize * cellSize;
            float waterCapacity = math.max(
                1f,
                ReactorThermalMath.FiniteOr(Tuning.WaterDensityKgPerCubicMeter, 1027f) *
                ReactorThermalMath.FiniteOr(Tuning.WaterHeatCapacityJoulesPerKgC, 3993f) *
                cellVolume);
            float convectionGain = math.max(0f, ReactorThermalMath.FiniteOr(Tuning.CellConvectionGain, 0.000018f));
            uint writes = 0u;
            for (int z = -halfExtent; z <= halfExtent; z++)
            {
                for (int y = -halfExtent; y <= halfExtent; y++)
                {
                    for (int x = -halfExtent; x <= halfExtent; x++)
                    {
                        int3 cell = centerCell + new int3(x, y, z);
                        if (math.any(cell < int3.zero) || math.any(cell >= resolution))
                            continue;

                        float weight = ReactorThermalMath.CellKernelWeight(new int3(x, y, z), radiusCells, quality);
                        if (weight <= 0f)
                            continue;

                        int cellIndex = AbyssalThermalMath.Index(cell.x, cell.y, cell.z, resolution);
                        ThermalCellDTO* target = Injection + cellIndex;
                        float celsiusDelta = joules * weight * math.rcp(weightSum) * math.rcp(waterCapacity);
                        celsiusDelta = math.min(celsiusDelta, math.max(1f, ReactorThermalMath.FiniteOr(Tuning.GridTemperatureClampCelsius, 2200f)));
                        ReactorThermalMath.AtomicAddFloat(&target->TemperatureCelsius, celsiusDelta);
                        ReactorThermalMath.AtomicAddFloat(&target->ConvectionVelocityY, celsiusDelta * convectionGain);
                        ReactorThermalMath.AtomicOrUInt(&target->Flags, AbyssalThermalMath.CellFlagInjected);
                        writes++;
                    }
                }
            }

            return writes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldEmitThermalSignal(int index, float coreTemp, float joules)
        {
            uint stride = Tuning.ThermalSignalStrideFrames == 0u ? 1u : Tuning.ThermalSignalStrideFrames;
            bool cadence = ((Frame + (uint)index) % stride) == 0u;
            bool hot = coreTemp >= ReactorThermalMath.FiniteOr(Tuning.SafeCoreTempCelsius, 760f);
            bool shimmer = joules >= math.max(0f, ReactorThermalMath.FiniteOr(Tuning.HeatShimmerMinJoules, 40000f));
            return cadence && (hot || shimmer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldEmitMeltdownSignal(int index)
        {
            uint stride = Tuning.MeltdownSignalStrideFrames == 0u ? 1u : Tuning.MeltdownSignalStrideFrames;
            return ((Frame + (uint)index) % stride) == 0u;
        }

        private bool EnqueueThermalSignal(in ReactorStateDTO reactor, float coreTemp, int index)
        {
            float safe = ReactorThermalMath.FiniteOr(Tuning.SafeCoreTempCelsius, 760f);
            float critical = math.max(safe + 1f, ReactorThermalMath.FiniteOr(Tuning.MeltdownCoreTempCelsius, 1850f));
            float severity01 = math.saturate((coreTemp - safe) * math.rcp(math.max(1f, critical - safe)));
            ThermalStateChangedSignal signal = default;
            signal.SourceHash = reactor.ReactorHashID != 0u ? reactor.ReactorHashID : ReactorThermalMath.SourceHash;
            signal.Frame = Frame;
            signal.Sequence = Frame + (uint)index;
            signal.Severity = (byte)math.clamp((int)math.round(severity01 * 4f), 0, 4);
            signal.PreviousSeverity = 0;
            signal.ThermalStatus = coreTemp >= critical ? (byte)3 : (byte)2;
            signal.Flags = (byte)(((reactor.Flags & ReactorStateDTO.FlagMock) != 0u ? 1 : 0) | 2);
            signal.TemperatureTenthsCelsius = (short)math.clamp((int)math.round(coreTemp * 10f), short.MinValue, short.MaxValue);
            signal.BatteryPercent = 0;
            signal.ActionMask = 1u << 7;
            return SignalBus<ThermalStateChangedSignal>.TryEnqueueBounded(ThermalWriter, ThermalWriterBudget, signal);
        }

        private bool EnqueueMeltdownSignal(double3 aup, uint entityHash, float coreTemp)
        {
            CombatDamageSignal signal = default;
            signal.ImpactAup = aup;
            signal.Direction = new float3(0f, 1f, 0f);
            signal.Magnitude = math.max(1f, coreTemp - ReactorThermalMath.FiniteOr(Tuning.SafeCoreTempCelsius, 760f));
            signal.DamageType = Tuning.DamageTypeHash != 0u ? Tuning.DamageTypeHash : ReactorThermalMath.DamageTypeReactorMeltdown;
            signal.TargetHash = entityHash;
            signal.SourceHash = Tuning.SourceHash != 0u ? Tuning.SourceHash : ReactorThermalMath.SourceHash;
            signal.Frame = Frame;
            signal.SourceId = unchecked((ushort)ReactorThermalMath.SourceHash);
            signal.TargetId = unchecked((ushort)entityHash);
            signal.Channel = 0;
            signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
            signal.IntegrityDelta = byte.MaxValue;
            return SignalBus<CombatDamageSignal>.TryEnqueueBounded(DamageWriter, DamageWriterBudget, signal);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct NuclearReactorTelemetryRecorderJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BaseReactorStateDTO* Reactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorKinematicStateDTO* Kinematics;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorPowerInjectionDTO* PowerLedger;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorThermalVisualDTO* Visuals;
        [NativeDisableUnsafePtrRestriction, NoAlias] public NuclearReactorTelemetryEntry* Ring;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* ReactorCount;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* Cursor;
        public int Capacity;
        public uint Frame;
        public float LastExecutionMicroseconds;

        public void Execute()
        {
            int count = math.clamp(*ReactorCount, 0, math.min(Capacity, ReactorThermalMath.MaxReactors));
            float totalWatts = 0f;
            float totalBoiled = 0f;
            float carnotSum = 0f;
            float coreSum = 0f;
            float maxCore = 0f;
            uint active = 0u;
            uint meltdowns = 0u;
            uint flags = 0u;
            uint stateHash = 0u;
            uint nonFinite = 0u;
            uint atomicAbort = 0u;
            uint radiationSignals = 0u;
            uint baseSignals = 0u;
            uint hotPowerNode = 0u;
            uint hotFluidRoom = 0u;
            double3 hotAup = default;

            for (int i = 0; i < count; i++)
            {
                BaseReactorStateDTO reactor = Reactors[i];
                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagActive) == 0u)
                    continue;

                ReactorPowerInjectionDTO ledger = PowerLedger != null ? PowerLedger[i] : default;
                ReactorThermalVisualDTO visual = Visuals != null ? Visuals[i] : default;
                active++;
                totalWatts += ReactorThermalMath.FiniteOr(ledger.GeneratedWatts, 0f);
                totalBoiled += ReactorThermalMath.FiniteOr(ledger.BoiledLiters, 0f);
                carnotSum += ReactorThermalMath.FiniteOr(ledger.CarnotEfficiency01, 0f);
                float core = ReactorThermalMath.FiniteOr(reactor.CoreTemperatureCelsius, 0f);
                coreSum += core;
                if (core > maxCore)
                {
                    maxCore = core;
                    hotPowerNode = reactor.PowerNodeHashID;
                    hotFluidRoom = reactor.FluidRoomHashID;
                    hotAup = Kinematics != null && math.all(math.isfinite(Kinematics[i].Aup)) ? Kinematics[i].Aup : visual.Aup;
                }

                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagMeltdown) != 0u)
                {
                    meltdowns++;
                    flags |= ReactorThermalMath.TelemetryFlagMeltdown;
                }
                if ((ledger.Flags & ReactorPowerInjectionDTO.FlagMeltdownSignalTick) != 0u)
                    radiationSignals++;
                if ((ledger.Flags & ReactorPowerInjectionDTO.FlagMeltdownEnteredThisTick) != 0u)
                    baseSignals++;
                if ((ledger.Flags & ReactorPowerInjectionDTO.FlagSignalOverflow) != 0u)
                    flags |= ReactorThermalMath.TelemetryFlagSignalOverflowRisk;

                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagMock) != 0u)
                    flags |= ReactorThermalMath.TelemetryFlagMockLoad;
                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagNoCoolant) != 0u)
                    flags |= ReactorThermalMath.TelemetryFlagNoCoolant;
                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagAtomicAbort) != 0u)
                {
                    flags |= ReactorThermalMath.TelemetryFlagAtomicAbort;
                    atomicAbort++;
                }
                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagSignalOverflow) != 0u)
                    flags |= ReactorThermalMath.TelemetryFlagSignalOverflowRisk;
                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagNonFinite) != 0u || !math.isfinite(core + ledger.GeneratedWatts + ledger.BoiledLiters))
                {
                    flags |= ReactorThermalMath.TelemetryFlagNonFinite;
                    nonFinite++;
                }

                stateHash = ReactorThermalMath.MixHash(stateHash, reactor.PowerNodeHashID);
                stateHash = ReactorThermalMath.MixHash(stateHash, reactor.FluidRoomHashID);
                stateHash = ReactorThermalMath.MixHash(stateHash, math.asuint(core));
                stateHash = ReactorThermalMath.MixHash(stateHash, math.asuint(ledger.GeneratedWatts));
                stateHash = ReactorThermalMath.MixHash(stateHash, reactor.ReactorFlags);
            }

            float timingProxy = ReactorThermalMath.FiniteOr(LastExecutionMicroseconds, 0f);
            flags |= ReactorThermalMath.TelemetryFlagTimingProxy;
            if (timingProxy > 200f)
                flags |= ReactorThermalMath.TelemetryFlagCostOverBudget;

            int ringIndex = (int)(Frame % (uint)ReactorThermalMath.TelemetryCapacity);
            NuclearReactorTelemetryEntry entry = default;
            entry.HotReactorAup = hotAup;
            entry.TotalGeneratedWatts = totalWatts;
            entry.TotalBoiledLiters = totalBoiled;
            entry.AverageCoreTempCelsius = active > 0u ? coreSum / active : 0f;
            entry.MaxCoreTempCelsius = maxCore;
            entry.LastExecutionMicroseconds = timingProxy;
            entry.AverageCarnotEfficiency01 = active > 0u ? carnotSum / active : 0f;
            entry.ActiveReactorCount = active;
            entry.MeltdownCount = meltdowns;
            entry.Flags = flags;
            entry.Frame = Frame;
            entry.StateHash = stateHash;
            entry.PowerNodeHashID = hotPowerNode;
            entry.FluidRoomHashID = hotFluidRoom;
            entry.RadiationSignalCount = radiationSignals;
            entry.BaseCompromiseSignalCount = baseSignals;
            entry.RingIndex = (uint)ringIndex;
            entry.NonFiniteCount = nonFinite;
            entry.AtomicAbortCount = atomicAbort;
            Ring[ringIndex] = entry;
            *Cursor = (int)Frame;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ReactorTelemetryRecorderJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorStateDTO* Reactors;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorKinematicStateDTO* Kinematics;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorThermalScratchDTO* Scratch;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ReactorThermalTelemetryEntry* Ring;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* ReactorCount;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* Cursor;
        public int Capacity;
        public uint Frame;
        public float LastInjectionMicroseconds;

        public void Execute()
        {
            int count = math.clamp(*ReactorCount, 0, math.min(Capacity, ReactorThermalMath.MaxReactors));
            float totalJoules = 0f;
            float coreSum = 0f;
            float maxCore = 0f;
            float maxSpeed = 0f;
            uint hotCellHash = 0u;
            uint stateHash = 0u;
            uint hotReactorHash = 0u;
            uint hotEntityHash = 0u;
            uint flags = 0u;
            uint active = 0u;
            uint meltdowns = 0u;
            uint nonFinite = 0u;
            uint writes = 0u;
            uint thermalSignals = 0u;
            uint damageSignals = 0u;
            double3 hotAup = default;

            for (int i = 0; i < count; i++)
            {
                ReactorStateDTO reactor = Reactors[i];
                ReactorThermalScratchDTO scratch = Scratch[i];
                if ((reactor.Flags & ReactorStateDTO.FlagActive) == 0u)
                    continue;

                active++;
                totalJoules += ReactorThermalMath.FiniteOr(scratch.JoulesInjected, 0f);
                float coreTemp = ReactorThermalMath.FiniteOr(scratch.CoreTempCelsius, 0f);
                coreSum += coreTemp;
                if (coreTemp > maxCore)
                {
                    maxCore = coreTemp;
                    hotReactorHash = reactor.ReactorHashID;
                    if (Kinematics != null)
                    {
                        ReactorKinematicStateDTO kinematic = Kinematics[i];
                        hotAup = math.all(math.isfinite(kinematic.Aup)) ? kinematic.Aup : default;
                        hotEntityHash = kinematic.EntityHashID;
                    }
                }

                maxSpeed = math.max(maxSpeed, ReactorThermalMath.FiniteOr(scratch.SpeedMetersPerSecond, 0f));
                hotCellHash = ReactorThermalMath.MixHash(hotCellHash, scratch.CenterCellIndex);
                stateHash = ReactorThermalMath.MixHash(stateHash, scratch.StateHash);
                writes += scratch.CellWrites;
                thermalSignals += scratch.ThermalSignalCount;
                damageSignals += scratch.DamageSignalCount;
                flags |= (scratch.Flags & ReactorStateDTO.FlagOutOfGrid) != 0u ? ReactorThermalMath.TelemetryFlagOutOfGrid : 0u;
                flags |= (scratch.Flags & ReactorStateDTO.FlagNonFinite) != 0u ? ReactorThermalMath.TelemetryFlagNonFinite : 0u;
                flags |= (scratch.Flags & ReactorStateDTO.FlagSignalOverflow) != 0u ? ReactorThermalMath.TelemetryFlagSignalOverflowRisk : 0u;
                if ((reactor.Flags & ReactorStateDTO.FlagMeltdown) != 0u)
                {
                    flags |= ReactorThermalMath.TelemetryFlagMeltdown;
                    meltdowns++;
                }

                if ((reactor.Flags & ReactorStateDTO.FlagMock) != 0u)
                    flags |= ReactorThermalMath.TelemetryFlagMockLoad;

                if (!math.isfinite(scratch.JoulesInjected + scratch.CoreTempCelsius + scratch.SpeedMetersPerSecond))
                {
                    flags |= ReactorThermalMath.TelemetryFlagNonFinite;
                    nonFinite++;
                }
            }

            float timingProxy = ReactorThermalMath.FiniteOr(LastInjectionMicroseconds, 0f);
            flags |= ReactorThermalMath.TelemetryFlagTimingProxy;
            if (timingProxy > 200f)
                flags |= ReactorThermalMath.TelemetryFlagCostOverBudget;
            if (thermalSignals + damageSignals > 8u)
                flags |= ReactorThermalMath.TelemetryFlagSignalOverflowRisk;

            int ringIndex = (int)(Frame % (uint)ReactorThermalMath.TelemetryCapacity);
            ReactorThermalTelemetryEntry entry = default;
            entry.HotReactorAup = hotAup;
            entry.TotalJoulesInjected = totalJoules;
            entry.AverageCoreTempCelsius = active > 0u ? coreSum / active : 0f;
            entry.MaxCoreTempCelsius = maxCore;
            entry.MaxSpeedMetersPerSecond = maxSpeed;
            entry.LastInjectionMicroseconds = timingProxy;
            entry.ActiveReactorCount = active;
            entry.MeltdownCount = meltdowns;
            entry.Flags = flags;
            entry.Frame = Frame;
            entry.StateHash = stateHash;
            entry.HotCellHash = hotCellHash;
            entry.InjectionCellWrites = writes;
            entry.NonFiniteCount = nonFinite;
            entry.ThermalSignalCount = thermalSignals;
            entry.DamageSignalCount = damageSignals;
            entry.RingIndex = (uint)ringIndex;
            entry.HotReactorHashID = hotReactorHash;
            entry.HotEntityHashID = hotEntityHash;
            Ring[ringIndex] = entry;
            *Cursor = ringIndex;
        }
    }
}
