using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class ShinobuPhysiologyJobMath
    {
        private const float Epsilon = 0.0001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeUnit(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > Epsilon ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DepthToPressureAtm(float depthMeters)
        {
            float depth = math.max(0f, SanitizeFinite(depthMeters, 0f));
            return ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm + depth * 0.1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountFirstFourBits(uint mask)
        {
            uint four = mask & 0x0Fu;
            four = four - ((four >> 1) & 0x55555555u);
            four = (four & 0x33333333u) + ((four >> 2) & 0x33333333u);
            return (int)(four & 0x0Fu);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MixStateHash(ulong hash, uint value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveCompartmentCount(float globalQualityWeight)
        {
            float quality = math.saturate(SanitizeFinite(globalQualityWeight, 1f));
            return math.clamp((int)math.lerp(4f, 16f, quality), 4, ShinobuPhysiologyConstants.TissueCompartmentCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PhysiologyTuningDTO SanitizeTuning(PhysiologyTuningDTO tuning)
        {
            if (tuning.Version == 0u)
                tuning = BuildDefaultTuning();

            tuning.BaseO2DrainPerSecond = math.clamp(SanitizeFinite(tuning.BaseO2DrainPerSecond, 0.0012f), 0.00001f, 0.25f);
            tuning.NitrogenUptakeRate = math.clamp(SanitizeFinite(tuning.NitrogenUptakeRate, 1f), 0.05f, 16f);
            tuning.AdrenalineDecaySeconds = math.clamp(SanitizeFinite(tuning.AdrenalineDecaySeconds, 60f), 1f, 600f);
            tuning.HypothermiaCoolingRate = math.clamp(SanitizeFinite(tuning.HypothermiaCoolingRate, 0.006f), 0.0001f, 0.25f);
            tuning.MedicalPurgePerSecond = math.clamp(SanitizeFinite(tuning.MedicalPurgePerSecond, 0.1f), 0f, 4f);
            tuning.HeartRateBase = math.clamp(SanitizeFinite(tuning.HeartRateBase, 62f), 25f, 120f);
            tuning.HeartRateTraumaSpike = math.clamp(SanitizeFinite(tuning.HeartRateTraumaSpike, 14f), 0f, 80f);
            tuning.ToxemiaO2Penalty = math.clamp(SanitizeFinite(tuning.ToxemiaO2Penalty, 0.85f), 0f, 4f);
            tuning.ThermalSuitInsulation01 = math.saturate(SanitizeFinite(tuning.ThermalSuitInsulation01, 0.68f));
            tuning.NarcosisStartAtm = math.clamp(SanitizeFinite(tuning.NarcosisStartAtm, 4f), 1f, 12f);
            tuning.NarcosisFullAtm = math.max(tuning.NarcosisStartAtm + 0.25f, SanitizeFinite(tuning.NarcosisFullAtm, 7f));
            tuning.BendsRiskScale = math.clamp(SanitizeFinite(tuning.BendsRiskScale, 1f), 0.05f, 8f);
            tuning.HaldaneTimeScale = math.clamp(SanitizeFinite(tuning.HaldaneTimeScale, 1f), 0.05f, 16f);
            tuning.MinOxygen01 = math.clamp(SanitizeFinite(tuning.MinOxygen01, 0f), 0f, 0.25f);
            tuning.Version = 1u;
            return tuning;
        }

        public static PhysiologyTuningDTO BuildDefaultTuning()
        {
            PhysiologyTuningDTO tuning = default;
            tuning.BaseO2DrainPerSecond = 0.0012f;
            tuning.NitrogenUptakeRate = 1f;
            tuning.AdrenalineDecaySeconds = 60f;
            tuning.HypothermiaCoolingRate = 0.006f;
            tuning.MedicalPurgePerSecond = 0.1f;
            tuning.HeartRateBase = 62f;
            tuning.HeartRateTraumaSpike = 14f;
            tuning.ToxemiaO2Penalty = 0.85f;
            tuning.ThermalSuitInsulation01 = 0.68f;
            tuning.NarcosisStartAtm = 4f;
            tuning.NarcosisFullAtm = 7f;
            tuning.BendsRiskScale = 1f;
            tuning.HaldaneTimeScale = 1f;
            tuning.MinOxygen01 = 0f;
            tuning.Version = 1u;
            return tuning;
        }
    }

    /// <summary>
    /// Cold-start tissue initializer. It is Burst-executed once after vault allocation; runtime ticks do not rely on zero-fill.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct InitTissueCompartmentsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<TissueCompartmentDTO> TissueCompartments;
        [ReadOnly, NoAlias] public NativeArray<HaldaneTissueCoefficientDTO> TissueCoefficients;
        public int EntityCapacity;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)TissueCompartments.Length)
                return;

            int tissueIndex = index % ShinobuPhysiologyConstants.TissueCompartmentCount;
            HaldaneTissueCoefficientDTO coefficient = TissueCoefficients.IsCreated && TissueCoefficients.Length > 0
                ? TissueCoefficients[math.min(tissueIndex, TissueCoefficients.Length - 1)]
                : default;
            float halfTime = ShinobuPhysiologyJobMath.SafePositive(
                coefficient.HalfTimeSeconds,
                ResolveEmergencyHalfTimeSeconds(tissueIndex));
            float mValue = math.max(1.01f, ShinobuPhysiologyJobMath.SanitizeFinite(
                coefficient.MValueRatio,
                ResolveEmergencyMValueRatio(tissueIndex)));

            TissueCompartments[index] = new TissueCompartmentDTO
            {
                NitrogenTension = ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm,
                Halftime = halfTime,
                MValue = mValue,
                Flags = 0u
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveEmergencyHalfTimeSeconds(int index)
        {
            switch (index)
            {
                case 0: return 5f * 60f;
                case 1: return 8f * 60f;
                case 2: return 12.5f * 60f;
                case 3: return 18.5f * 60f;
                case 4: return 27f * 60f;
                case 5: return 38.3f * 60f;
                case 6: return 54.3f * 60f;
                case 7: return 77f * 60f;
                case 8: return 109f * 60f;
                case 9: return 146f * 60f;
                case 10: return 187f * 60f;
                case 11: return 239f * 60f;
                case 12: return 305f * 60f;
                case 13: return 390f * 60f;
                case 14: return 498f * 60f;
                default: return 635f * 60f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveEmergencyMValueRatio(int index)
        {
            return math.max(1.08f, 1.58f - index * 0.028f);
        }
    }

    /// <summary>
    /// Deterministic crash-dive profile generator: descent, bottom dwell, emergency ascent.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockDiveProfileJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<DiveProfileSampleDTO> Samples;
        public float SampleStepSeconds;
        public uint Frame;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Samples.Length)
                return;

            float step = ShinobuPhysiologyJobMath.SafePositive(SampleStepSeconds, 10f);
            float time = index * step;
            float depth;
            float ascentRate;
            if (time < 180f)
            {
                depth = time * (300f * math.rcp(180f));
                ascentRate = -300f * math.rcp(180f);
            }
            else if (time < 1380f)
            {
                depth = 300f;
                ascentRate = 0f;
            }
            else
            {
                float ascentTime = time - 1380f;
                depth = math.max(0f, 300f - ascentTime * 10f);
                ascentRate = 10f;
            }

            Samples[index] = new DiveProfileSampleDTO
            {
                TimeSeconds = time,
                DepthMeters = depth,
                AmbientPressureAtm = ShinobuPhysiologyJobMath.DepthToPressureAtm(depth),
                AscentRateMetersPerSecond = ascentRate,
                Frame = Frame,
                Flags = MockPressureSignal.ActiveFlag,
                ProfileHash = 0x44435331u,
                SampleIndex = unchecked((uint)index)
            };
        }
    }

    /// <summary>
    /// Vacuum fallback environment generator. It produces a deterministic 100m pressure drop without ocean dependencies.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockEnvironmentDropJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [NoAlias] public NativeArray<MockPressureSignal> PressureSignals;
        public float MockDepthMeters;
        public float SystemHealthIndex01;
        public uint Frame;
        public int Count;
        public byte UseMockDepth;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Environment.Length)
                return;

            MockEnvironmentVitalsSignal env = Environment[index];
            if (UseMockDepth != 0)
            {
                uint seed = Frame * 747796405u + (uint)index * 2891336453u + 0x9E3779B9u;
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                float jitter = ((seed >> 8) & 1023u) * (1f / 1023f) - 0.5f;
                env.DepthMeters = math.max(0f, MockDepthMeters + jitter * 2f);
                env.AmbientPressureAtm = ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters);
                env.AscentRateMetersPerSecond = 0f;
                env.AmbientTemperatureCelsius = math.lerp(10f, 2f, math.saturate(env.DepthMeters * 0.01f));
                env.Flags |= MockPressureSignal.ActiveFlag;
            }

            if (PressureSignals.IsCreated && (uint)index < (uint)PressureSignals.Length)
            {
                MockPressureSignal pressure = PressureSignals[index];
                if ((pressure.Flags & MockPressureSignal.ActiveFlag) != 0u)
                {
                    env.DepthMeters = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(pressure.DepthMeters, env.DepthMeters));
                    env.AmbientPressureAtm = pressure.AmbientPressureAtm > 0f
                        ? ShinobuPhysiologyJobMath.SanitizeFinite(pressure.AmbientPressureAtm, env.AmbientPressureAtm)
                        : ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters);
                    env.AscentRateMetersPerSecond = ShinobuPhysiologyJobMath.SanitizeFinite(pressure.AscentRateMetersPerSecond, 0f);
                    env.AmbientTemperatureCelsius = ShinobuPhysiologyJobMath.SanitizeFinite(pressure.AmbientTemperatureCelsius, env.AmbientTemperatureCelsius);
                    env.InventoryMask = pressure.InventoryMask;
                    env.Flags |= pressure.Flags;
                    pressure.Flags = 0u;
                    PressureSignals[index] = pressure;
                }
            }

            env.SystemHealthIndex01 = math.saturate(ShinobuPhysiologyJobMath.SanitizeFinite(SystemHealthIndex01, env.SystemHealthIndex01));
            env.Frame = Frame;
            Environment[index] = env;
        }
    }

    /// <summary>
    /// Drains local mock dependency packets into physiology state.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct PhysiologySignalIngestJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PhysiologyDTO> Vitals;
        [NoAlias] public NativeArray<PhysiologyScalarsDTO> Scalars;
        [NoAlias] public NativeArray<MockCombatDamageSignal> CombatSignals;
        [NoAlias] public NativeArray<MockPredatorAggroSignal> PredatorSignals;
        [NoAlias] public NativeArray<MockToxemiaSignal> ToxemiaSignals;
        [NoAlias] public NativeArray<MockMedicalItemUsedSignal> MedicalSignals;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Vitals.Length || (uint)index >= (uint)Scalars.Length)
                return;

            PhysiologyDTO vital = Vitals[index];
            PhysiologyScalarsDTO scalar = Scalars[index];

            if (CombatSignals.IsCreated && (uint)index < (uint)CombatSignals.Length)
            {
                MockCombatDamageSignal damage = CombatSignals[index];
                if ((damage.Flags & 1u) != 0u)
                {
                    int traumaType = math.clamp(damage.TraumaType, 0, 3);
                    vital.ActiveTraumaMask |= 1u << traumaType;
                    float severity = ShinobuPhysiologyJobMath.SanitizeUnit(damage.Severity01);
                    vital.HeartRate = math.max(vital.HeartRate, 70f + severity * 45f);
                    damage.Flags = 0u;
                    CombatSignals[index] = damage;
                }
            }

            if (PredatorSignals.IsCreated && (uint)index < (uint)PredatorSignals.Length)
            {
                MockPredatorAggroSignal predator = PredatorSignals[index];
                if ((predator.Flags & 1u) != 0u)
                {
                    vital.Adrenaline = math.max(vital.Adrenaline, ShinobuPhysiologyJobMath.SanitizeUnit(predator.Aggro01));
                    scalar.StatusFlags |= ShinobuPhysiologyFlags.AdrenalineSeen;
                    predator.Flags = 0u;
                    PredatorSignals[index] = predator;
                }
            }

            if (ToxemiaSignals.IsCreated && (uint)index < (uint)ToxemiaSignals.Length)
            {
                MockToxemiaSignal toxemia = ToxemiaSignals[index];
                if ((toxemia.Flags & 1u) != 0u)
                {
                    if ((toxemia.Flags & 2u) != 0u)
                        scalar.Toxemia = ShinobuPhysiologyJobMath.SanitizeUnit(toxemia.Absolute01);
                    else
                        scalar.Toxemia = math.saturate(scalar.Toxemia + ShinobuPhysiologyJobMath.SanitizeFinite(toxemia.Delta01, 0f));

                    toxemia.Flags = 0u;
                    ToxemiaSignals[index] = toxemia;
                }
            }

            if (MedicalSignals.IsCreated && (uint)index < (uint)MedicalSignals.Length)
            {
                MockMedicalItemUsedSignal medical = MedicalSignals[index];
                if ((medical.Flags & 1u) != 0u)
                {
                    scalar.MedicalPurgeSecondsRemaining = 10f;
                    scalar.MedicalPurgeStrength01 = math.max(
                        scalar.MedicalPurgeStrength01,
                        math.max(0.1f, ShinobuPhysiologyJobMath.SanitizeUnit(medical.PurgeStrength01)));
                    medical.Flags = 0u;
                    MedicalSignals[index] = medical;
                }
            }

            vital.BloodOxygen = math.clamp(
                ShinobuPhysiologyJobMath.SanitizeFinite(vital.BloodOxygen, 1f),
                0f,
                1f);
            vital.CoreTemperature = math.clamp(
                ShinobuPhysiologyJobMath.SanitizeFinite(vital.CoreTemperature, 37f),
                20f,
                43f);
            vital.HeartRate = math.clamp(
                ShinobuPhysiologyJobMath.SanitizeFinite(vital.HeartRate, 62f),
                20f,
                220f);
            vital.Adrenaline = ShinobuPhysiologyJobMath.SanitizeUnit(vital.Adrenaline);
            scalar.Toxemia = ShinobuPhysiologyJobMath.SanitizeUnit(scalar.Toxemia);

            Vitals[index] = vital;
            Scalars[index] = scalar;
        }
    }

    /// <summary>
    /// Deterministic Haldanean tissue saturation kernel. Active tissue count is driven by GlobalQualityWeight.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct TissueSaturationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PhysiologyDTO> Vitals;
        [NoAlias] public NativeArray<TissueCompartmentDTO> TissueCompartments;
        [NoAlias] public NativeArray<DecompressionStateDTO> DecompressionStates;
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [NoAlias] public NativeArray<PhysiologyScalarsDTO> Scalars;
        public NativeQueue<PhysiologyStateSignal>.ParallelWriter PhysiologyWriter;
        public PhysiologyTuningDTO Tuning;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public int Count;
        public byte EmitPhysiologySignal;

        public void Execute(int index)
        {
            int compartmentBase = index * ShinobuPhysiologyConstants.TissueCompartmentCount;
            if ((uint)index >= (uint)Count ||
                (uint)index >= (uint)Vitals.Length ||
                (uint)index >= (uint)DecompressionStates.Length ||
                (uint)index >= (uint)Scalars.Length ||
                (uint)(compartmentBase + ShinobuPhysiologyConstants.TissueCompartmentCount - 1) >= (uint)TissueCompartments.Length)
            {
                return;
            }

            PhysiologyTuningDTO tuning = ShinobuPhysiologyJobMath.SanitizeTuning(Tuning);
            MockEnvironmentVitalsSignal env = Environment.IsCreated && (uint)index < (uint)Environment.Length
                ? Environment[index]
                : default;

            float dt = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(DeltaSeconds, 0.016f), 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            float ambient = env.AmbientPressureAtm > 0f
                ? ShinobuPhysiologyJobMath.SanitizeFinite(env.AmbientPressureAtm, ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters))
                : ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters);
            ambient = math.max(0.5f, ambient);
            float ambientNitrogenPressure = ambient * ShinobuPhysiologyConstants.NitrogenFraction;
            float tissueEquilibriumPressure = ambient;
            float nitrogenScale = math.max(0.001f, tuning.NitrogenUptakeRate * tuning.HaldaneTimeScale);
            int activeCompartments = ShinobuPhysiologyJobMath.ResolveActiveCompartmentCount(GlobalQualityWeight);
            float maxTissue = 0f;
            float risk = 0f;
            uint overMask = 0u;
            uint invalidMath = 0u;

            DecompressionStateDTO state = DecompressionStates[index];
            state.AmbientPressure = ambient;
            state.AscentRate = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(env.AscentRateMetersPerSecond, 0f));

            float* stateTissues = state.TissueTensions;
            void* tissueBasePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TissueCompartments);
            int tissueStride = UnsafeUtility.SizeOf<TissueCompartmentDTO>();
            for (int tissueIndex = 0; tissueIndex < ShinobuPhysiologyConstants.TissueCompartmentCount; tissueIndex++)
            {
                bool evaluateTissue = tissueIndex < activeCompartments - 1 ||
                                      tissueIndex == ShinobuPhysiologyConstants.TissueCompartmentCount - 1;
                ref TissueCompartmentDTO tissue = ref UnsafeUtility.AsRef<TissueCompartmentDTO>(
                    (byte*)tissueBasePtr + ((compartmentBase + tissueIndex) * tissueStride));
                float halfTime = ShinobuPhysiologyJobMath.SafePositive(tissue.Halftime, 300f);
                if (!math.isfinite(tissue.NitrogenTension) || !math.isfinite(tissue.Halftime) || !math.isfinite(tissue.MValue))
                    invalidMath = ShinobuPhysiologyFlags.InvalidMath;
                float oldTension = ShinobuPhysiologyJobMath.SanitizeFinite(tissue.NitrogenTension, tissueEquilibriumPressure);
                float next = oldTension;
                if (evaluateTissue)
                {
                    float k = 0.69314718056f * math.rcp(halfTime);
                    next = tissueEquilibriumPressure + (oldTension - tissueEquilibriumPressure) * math.exp(-k * dt * nitrogenScale);
                    if (!math.isfinite(next))
                    {
                        next = tissueEquilibriumPressure;
                        invalidMath = ShinobuPhysiologyFlags.InvalidMath;
                    }
                }

                tissue.NitrogenTension = next;
                tissue.Halftime = halfTime;
                tissue.Flags = evaluateTissue ? 1u : 0u;
                stateTissues[tissueIndex] = next;

                float mValue = math.max(0.1f, ambient * math.max(1.01f, ShinobuPhysiologyJobMath.SanitizeFinite(tissue.MValue, 1.35f)));
                float excess = next - mValue;
                if (excess > 0f)
                {
                    overMask |= 1u << tissueIndex;
                    float compartmentRisk = excess * tuning.BendsRiskScale * math.rcp(math.max(0.0001f, mValue));
                    risk = math.max(risk, compartmentRisk);
                }

                maxTissue = math.max(maxTissue, next);
            }

            PhysiologyDTO vital = Vitals[index];
            PhysiologyScalarsDTO scalar = Scalars[index];
            float narcosisDenominator = math.max(0.0001f, tuning.NarcosisFullAtm - tuning.NarcosisStartAtm);
            float nitrogenNarcosis = math.saturate((ambientNitrogenPressure - tuning.NarcosisStartAtm) * math.rcp(narcosisDenominator));
            float supersaturation = math.saturate(risk);
            scalar.NarcosisSeverity = nitrogenNarcosis;
            scalar.BendsRisk = supersaturation;
            scalar.TissueOverMValueMask = overMask;
            scalar.StatusFlags &= ~(ShinobuPhysiologyFlags.Bends | ShinobuPhysiologyFlags.Narcosis | ShinobuPhysiologyFlags.FatalBends | ShinobuPhysiologyFlags.HyperbaricOverride | ShinobuPhysiologyFlags.InvalidMath);
            scalar.StatusFlags |= overMask != 0u ? ShinobuPhysiologyFlags.Bends : 0u;
            scalar.StatusFlags |= nitrogenNarcosis > 0f ? ShinobuPhysiologyFlags.Narcosis : 0u;
            scalar.StatusFlags |= supersaturation >= 0.98f ? ShinobuPhysiologyFlags.FatalBends : 0u;
            scalar.StatusFlags |= (env.Flags & MockPressureSignal.HyperbaricTreatmentFlag) != 0u ? ShinobuPhysiologyFlags.HyperbaricOverride : 0u;
            scalar.StatusFlags |= invalidMath;
            vital.TissueNitrogen = maxTissue;

            Vitals[index] = vital;
            Scalars[index] = scalar;
            DecompressionStates[index] = state;

            if (EmitPhysiologySignal != 0 && index == 0)
            {
                PhysiologyStateSignal signal = default;
                signal.PlayerStress01 = math.saturate(math.max(supersaturation, nitrogenNarcosis));
                signal.O2DrainMultiplier = math.max(1f, ambient);
                signal.Recovery01 = 1f - supersaturation;
                signal.Frame = env.Frame;
                signal.Cause = PhysiologyStateSignal.CauseDecompression;
                signal.Flags = (byte)math.select(0, 1, supersaturation > 0f);
                signal.Supersaturation01 = supersaturation;
                signal.Narcosis01 = nitrogenNarcosis;
                signal.AmbientPressureAtm = ambient;
                signal.NitrogenLoadAtm = maxTissue;
                signal.AscentRateMetersPerSecond = state.AscentRate;
                signal.TissueOverMValueMask = overMask;
                signal.SourceHash = ShinobuPhysiologyConstants.SourceHash;
                signal.EntityIndex = index;
                signal.ActiveCompartments = (byte)activeCompartments;
                signal.FatalSeverity = (byte)math.round(supersaturation * 255f);
                signal.StatusFlags = scalar.StatusFlags;
                PhysiologyWriter.Enqueue(signal);
            }
        }
    }

    /// <summary>
    /// Metabolic oxygen, temperature, toxemia, adrenaline, pulse, export, and black-box writer.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct OxygenConsumptionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PhysiologyDTO> Vitals;
        [NoAlias] public NativeArray<PhysiologyScalarsDTO> Scalars;
        [NoAlias] public NativeArray<CardiacPulseStateDTO> PulseStates;
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [NoAlias] public NativeArray<VitalsExportDTO> VitalsExport;
        [NoAlias] public NativeArray<PhysiologyTelemetryEntry> Telemetry;
        public NativeQueue<CardiacPulseSignal>.ParallelWriter CardiacPulseWriter;
        public PhysiologyTuningDTO Tuning;
        public float DeltaSeconds;
        public uint Frame;
        public int TelemetryCursor;
        public int Count;
        public byte EmitPulseSignals;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                (uint)index >= (uint)Vitals.Length ||
                (uint)index >= (uint)Scalars.Length ||
                (uint)index >= (uint)PulseStates.Length ||
                (uint)index >= (uint)VitalsExport.Length)
            {
                return;
            }

            PhysiologyTuningDTO tuning = ShinobuPhysiologyJobMath.SanitizeTuning(Tuning);
            float dt = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(DeltaSeconds, 0.016f), 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            MockEnvironmentVitalsSignal env = Environment.IsCreated && (uint)index < (uint)Environment.Length
                ? Environment[index]
                : default;

            PhysiologyDTO vital = Vitals[index];
            PhysiologyScalarsDTO scalar = Scalars[index];
            CardiacPulseStateDTO pulse = PulseStates[index];
            uint status = scalar.StatusFlags;

            int traumaCount = ShinobuPhysiologyJobMath.CountFirstFourBits(vital.ActiveTraumaMask);
            float traumaSeverity = traumaCount;
            float adrenaline = ShinobuPhysiologyJobMath.SanitizeUnit(vital.Adrenaline);
            if (adrenaline > 0f)
                status |= ShinobuPhysiologyFlags.AdrenalineSeen;

            float adrenalineDecayT = math.saturate(dt * math.rcp(math.max(0.0001f, tuning.AdrenalineDecaySeconds)));
            adrenaline = math.lerp(adrenaline, 0f, adrenalineDecayT);

            if ((status & ShinobuPhysiologyFlags.AdrenalineSeen) != 0u && adrenaline <= 0.02f)
            {
                scalar.FatigueMultiplier = 2f;
                status |= ShinobuPhysiologyFlags.AdrenalineCrash;
            }
            else
            {
                scalar.FatigueMultiplier = 1f;
                status &= ~ShinobuPhysiologyFlags.AdrenalineCrash;
            }

            float ambientTemperature = ShinobuPhysiologyJobMath.SanitizeFinite(env.AmbientTemperatureCelsius, 4f);
            bool insulated = (env.InventoryMask & ShinobuInventoryBits.ThermalSuitUpgrade) != 0u;
            float insulation = insulated ? tuning.ThermalSuitInsulation01 : 0f;
            float cooling = 1f - math.exp(-tuning.HypothermiaCoolingRate * dt);
            vital.CoreTemperature += (ambientTemperature - vital.CoreTemperature) * cooling * (1f - insulation);
            vital.CoreTemperature = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(vital.CoreTemperature, 37f), 20f, 43f);
            scalar.HypothermiaShiver = math.saturate((35f - vital.CoreTemperature) * math.rcp(3f));

            status &= ~ShinobuPhysiologyFlags.Hypothermia;
            if (scalar.HypothermiaShiver > 0f)
                status |= ShinobuPhysiologyFlags.Hypothermia;

            if (scalar.MedicalPurgeSecondsRemaining > 0f)
            {
                float purge = tuning.MedicalPurgePerSecond * math.max(0.1f, scalar.MedicalPurgeStrength01) * dt;
                scalar.Toxemia = math.max(0f, scalar.Toxemia - purge);
                scalar.MedicalPurgeSecondsRemaining = math.max(0f, scalar.MedicalPurgeSecondsRemaining - dt);
            }

            float painSuppression = 1f - adrenaline * 0.6f;
            float effectiveTrauma = traumaSeverity * math.clamp(painSuppression, 0.2f, 1f);
            float heartTarget = tuning.HeartRateBase + adrenaline * 58f + effectiveTrauma * tuning.HeartRateTraumaSpike;
            heartTarget *= math.lerp(1f, 0.58f, scalar.HypothermiaShiver);
            vital.HeartRate = math.lerp(
                math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(vital.HeartRate, tuning.HeartRateBase), 20f, 220f),
                math.clamp(heartTarget, 20f, 220f),
                math.saturate(dt * 4f));

            float heartScale = vital.HeartRate * math.rcp(60f);
            float traumaDrain = 1f + effectiveTrauma * effectiveTrauma * 0.18f;
            float ambientPressureAtm = env.AmbientPressureAtm > 0f
                ? ShinobuPhysiologyJobMath.SanitizeFinite(env.AmbientPressureAtm, ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters))
                : ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters);
            float pressureBreathScale = math.max(1f, ambientPressureAtm);
            float o2Drain = tuning.BaseO2DrainPerSecond *
                (0.65f + heartScale * 0.35f + adrenaline * 0.42f) *
                traumaDrain *
                (1f + scalar.Toxemia * tuning.ToxemiaO2Penalty) *
                (1f + scalar.HypothermiaShiver * 0.2f) *
                pressureBreathScale;

            scalar.OxygenDrainPerSecond = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(o2Drain, tuning.BaseO2DrainPerSecond));
            vital.BloodOxygen = math.max(tuning.MinOxygen01, vital.BloodOxygen - scalar.OxygenDrainPerSecond * dt);
            vital.BloodOxygen = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(vital.BloodOxygen, 1f), 0f, 1f);
            vital.Adrenaline = adrenaline;
            scalar.SwimSpeedBonus = adrenaline * 0.2f;

            status &= ~ShinobuPhysiologyFlags.OxygenCritical;
            if (vital.BloodOxygen <= 0.18f)
                status |= ShinobuPhysiologyFlags.OxygenCritical;

            pulse.Phase += math.max(0f, vital.HeartRate) * math.rcp(60f) * dt;
            int pulseCount = (int)math.floor(pulse.Phase);
            if (pulseCount > 0)
            {
                pulse.Phase -= pulseCount;
                int emitted = math.min(pulseCount, 4);
                for (int pulseIndex = 0; pulseIndex < emitted; pulseIndex++)
                {
                    pulse.PulseCount++;
                    scalar.PulseCount = pulse.PulseCount;
                    scalar.LastPulseFrame = Frame;
                    if (EmitPulseSignals != 0)
                    {
                        CardiacPulseSignal signal = default;
                        signal.HeartRate = vital.HeartRate;
                        signal.Adrenaline01 = adrenaline;
                        signal.BloodOxygen01 = vital.BloodOxygen;
                        signal.Toxemia01 = scalar.Toxemia;
                        signal.Frame = Frame;
                        signal.SourceHash = ShinobuPhysiologyConstants.SourceHash;
                        signal.PulseCount = pulse.PulseCount;
                        signal.Flags = (byte)((adrenaline > 0.25f ? CardiacPulseSignal.FlagAdrenaline : 0) |
                                              (vital.BloodOxygen <= 0.18f ? CardiacPulseSignal.FlagOxygenCritical : 0));
                        CardiacPulseWriter.Enqueue(signal);
                    }
                }
            }

            pulse.Phase = math.frac(math.max(0f, pulse.Phase));
            pulse.LastHeartRate = vital.HeartRate;
            scalar.HeartbeatPhase = pulse.Phase;

            uint fatalFlags = 0u;
            if (vital.BloodOxygen <= ShinobuPhysiologyConstants.OxygenDeathThreshold)
            {
                status |= ShinobuPhysiologyFlags.FatalOxygen;
                fatalFlags |= ShinobuPhysiologyFlags.FatalOxygen;
            }
            if ((status & ShinobuPhysiologyFlags.FatalBends) != 0u)
                fatalFlags |= ShinobuPhysiologyFlags.FatalBends;
            if ((status & ShinobuPhysiologyFlags.InvalidMath) != 0u)
                fatalFlags |= ShinobuPhysiologyFlags.InvalidMath;

            if (!math.isfinite(vital.BloodOxygen) ||
                !math.isfinite(vital.TissueNitrogen) ||
                !math.isfinite(vital.CoreTemperature) ||
                !math.isfinite(vital.HeartRate) ||
                !math.isfinite(vital.Adrenaline))
            {
                vital.BloodOxygen = 1f;
                vital.TissueNitrogen = ShinobuPhysiologyConstants.NitrogenFraction;
                vital.CoreTemperature = 37f;
                vital.HeartRate = tuning.HeartRateBase;
                vital.Adrenaline = 0f;
                fatalFlags |= ShinobuPhysiologyFlags.InvalidMath;
                status |= ShinobuPhysiologyFlags.InvalidMath;
            }

            scalar.StatusFlags = status;
            Vitals[index] = vital;
            Scalars[index] = scalar;
            PulseStates[index] = pulse;

            VitalsExport[index] = new VitalsExportDTO
            {
                BloodOxygen = vital.BloodOxygen,
                CoreTemperature = vital.CoreTemperature,
                DepthMeters = math.max(0f, env.DepthMeters),
                StatusMask = status
            };

            if (Telemetry.IsCreated && Telemetry.Length > 0 && index == 0)
            {
                int telemetryIndex = TelemetryCursor % Telemetry.Length;
                ulong hash = 1469598103934665603UL;
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, math.asuint(vital.BloodOxygen));
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, math.asuint(vital.TissueNitrogen));
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, math.asuint(vital.CoreTemperature));
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, vital.ActiveTraumaMask);
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, status);

                Telemetry[telemetryIndex] = new PhysiologyTelemetryEntry
                {
                    StateHash = hash,
                    Frame = Frame,
                    ActiveTraumaMask = vital.ActiveTraumaMask,
                    BloodOxygen = vital.BloodOxygen,
                    NitrogenLoad = vital.TissueNitrogen,
                    CoreTemperature = vital.CoreTemperature,
                    AmbientPressureAtm = math.max(0f, env.AmbientPressureAtm),
                    NarcosisSeverity = scalar.NarcosisSeverity,
                    SupersaturationScalar = scalar.BendsRisk,
                    HeartRate = vital.HeartRate,
                    Adrenaline = vital.Adrenaline,
                    FatalFlags = fatalFlags,
                    TissueOverMValueMask = scalar.TissueOverMValueMask,
                    DepthMeters = math.max(0f, env.DepthMeters),
                    ExecutionMicroseconds = 0f
                };
            }
        }
    }
}
