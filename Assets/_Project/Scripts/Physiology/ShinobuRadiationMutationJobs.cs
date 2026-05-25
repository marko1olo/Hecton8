using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts.Physiology;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class RadiationMutationKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GenerateMockDose(float timeSeconds, in RadiationMutationTuningDTO tuningInput)
        {
            RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuningInput);
            float phase = math.frac(ShinobuRadiationMutationJobMath.SanitizeFinite(timeSeconds, 0f) * math.rcp(tuning.MockRampSeconds));
            float triangle = 1f - math.abs(phase * 2f - 1f);
            float shaped = ShinobuRadiationMutationJobMath.Smooth01(triangle);
            return math.max(0f, tuning.MockPeakDoseRad * shaped);
        }

        public static void EvaluateRow(
            int index,
            in RadiationStateDTO radiation,
            byte hasRadiation,
            float mockDoseRad,
            byte useMockDose,
            in RadiationMutationTuningDTO tuningInput,
            float previousDoseRad,
            float deltaSeconds,
            float globalQualityWeight,
            uint frame,
            int telemetryIndex,
            ref MutationStateDTO mutationState,
            out RadiationMutationTelemetryEntry telemetry)
        {
            RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuningInput);
            float dt = math.clamp(ShinobuRadiationMutationJobMath.SanitizeFinite(deltaSeconds, 0.1f), 0.0001f, 1f);
            float quality = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(globalQualityWeight, tuning.GlobalQualityWeight));
            float sourceDose = hasRadiation != 0 ? radiation.CumulativeDoseRad : 0f;
            float exposureRate = hasRadiation != 0 ? radiation.CurrentExposureRate : 0f;
            float shielding01 = hasRadiation != 0 ? radiation.ShieldingFactor01 : 0f;
            uint flags = RadiationMutationFlags.None;

            if (useMockDose != 0)
            {
                sourceDose = math.max(sourceDose, ShinobuRadiationMutationJobMath.SanitizeFinite(mockDoseRad, 0f));
                flags |= RadiationMutationFlags.MockDose;
            }

            sourceDose = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(sourceDose, 0f));
            exposureRate = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(exposureRate, 0f));
            shielding01 = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(shielding01, 0f));
            float attenuatedRate = exposureRate * (1f - shielding01);
            float attenuatedDose = math.max(sourceDose, sourceDose + attenuatedRate * dt);
            float doseDenominator = math.max(0.0001f, tuning.FatalDoseRad - tuning.SafeDoseRad);
            float doseT = math.saturate((attenuatedDose - tuning.SafeDoseRad) * math.rcp(doseDenominator));
            float targetSeverity = ShinobuRadiationMutationJobMath.Smooth01(doseT);

            float previousSeverity = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(mutationState.MutationSeverity01, 0f));
            float previousDose = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(previousDoseRad, sourceDose));
            bool healing = attenuatedDose + 0.001f < previousDose;
            float rate = targetSeverity > previousSeverity ? tuning.SeverityRisePerSecond : tuning.SeverityFallPerSecond;
            if (healing)
            {
                float decay = ShinobuRadiationMutationJobMath.ApproximateExpNegPositive(tuning.HealingDecayPerSecond * dt);
                targetSeverity = math.min(targetSeverity, previousSeverity * decay);
                flags |= RadiationMutationFlags.Healing;
            }

            float blend = math.saturate(rate * dt);
            float severity = math.saturate(math.lerp(previousSeverity, targetSeverity, blend));
            float staminaPenalty = math.saturate(severity * tuning.MaxStaminaPenaltyPercent);
            float healingSuppression = math.saturate(severity * (healing ? 0.55f : 1f));
            float complexWeight = ShinobuRadiationMutationJobMath.Smooth01((quality - 0.25f) * math.rcp(0.75f));
            if (complexWeight > 0f)
                flags |= RadiationMutationFlags.ComplexNoiseAdmitted;
            if (severity > 0.0001f)
                flags |= RadiationMutationFlags.Active;
            if (severity >= 0.95f)
                flags |= RadiationMutationFlags.Critical;
            if (severity >= tuning.ToxicBloodThreshold01)
                flags |= RadiationMutationFlags.ToxicBloodVfxRequested;

            bool nonFinite = !math.isfinite(severity) ||
                             !math.isfinite(staminaPenalty) ||
                             !math.isfinite(healingSuppression) ||
                             !math.isfinite(attenuatedDose);
            if (nonFinite)
            {
                severity = 0f;
                staminaPenalty = 0f;
                healingSuppression = 0f;
                attenuatedDose = 0f;
                flags |= RadiationMutationFlags.NonFiniteSanitized;
            }

            mutationState = new MutationStateDTO
            {
                MutationSeverity01 = severity,
                MaxStaminaPenalty = staminaPenalty,
                HealingSuppression01 = healingSuppression,
                MutationFlags = flags
            };

            telemetry = BuildTelemetry(index, telemetryIndex, frame, flags, sourceDose, exposureRate, attenuatedDose, severity, staminaPenalty, healingSuppression, quality, tuning);
        }

        public static void ApplyMetabolicBridge(
            ref MutationStateDTO mutation,
            ref MetabolicStateDTO metabolic,
            in RadiationMutationTuningDTO tuningInput)
        {
            RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuningInput);
            float severity = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(mutation.MutationSeverity01, 0f));
            if (severity <= 0.0001f)
                return;

            float toxicity = math.clamp(ShinobuRadiationMutationJobMath.SanitizeFinite(metabolic.Toxicity, 0f), 0f, 8f);
            toxicity = math.max(toxicity, severity * tuning.MetabolicToxicityScale);
            metabolic.Toxicity = toxicity;
            metabolic.Flags |= ShinobuMetabolismVaultContract.FlagFatigue;
            if (toxicity > 0.25f)
                metabolic.Flags |= ShinobuMetabolismVaultContract.FlagToxic;
            mutation.MutationFlags |= RadiationMutationFlags.MetabolicBridgeApplied;
        }

        public static void PatchTelemetry(ref RadiationMutationTelemetryEntry entry, float executionMicroseconds)
        {
            entry.ExecutionMicroseconds = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(executionMicroseconds, 0f));
            if (entry.ExecutionMicroseconds > 100f)
                entry.Flags |= RadiationMutationFlags.OverBudget;
        }

        private static RadiationMutationTelemetryEntry BuildTelemetry(
            int index,
            int telemetryIndex,
            uint frame,
            uint flags,
            float cumulativeDose,
            float exposureRate,
            float attenuatedDose,
            float severity,
            float staminaPenalty,
            float healingSuppression,
            float quality,
            in RadiationMutationTuningDTO tuning)
        {
            ulong hash = 1469598103934665603UL;
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(cumulativeDose));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(attenuatedDose));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(severity));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(staminaPenalty));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, (uint)index);
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, flags);

            return new RadiationMutationTelemetryEntry
            {
                StateHash = hash,
                Frame = frame,
                Flags = flags,
                CumulativeDoseRad = cumulativeDose,
                CurrentExposureRate = exposureRate,
                AttenuatedDoseRad = attenuatedDose,
                MutationSeverity01 = severity,
                MaxStaminaPenalty = staminaPenalty,
                HealingSuppression01 = healingSuppression,
                GlobalQualityWeight = quality,
                MetabolicToxicity = math.saturate(severity * tuning.MetabolicToxicityScale),
                VfxIntensity01 = math.saturate((severity - tuning.ToxicBloodThreshold01) * math.rcp(math.max(0.0001f, 1f - tuning.ToxicBloodThreshold01))),
                RingCursor = (uint)telemetryIndex,
                SourceHash = ShinobuRadiationMutationConstants.SourceHash
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitRadiationMutationJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public MutationStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationMutationTelemetryEntry* Telemetry;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* MockDoseRad;
        public int StateCount;
        public int TelemetryCount;
        public int MockDoseCount;

        public void Execute(int index)
        {
            if (States != null && (uint)index < (uint)StateCount)
                States[index] = default;

            if (Telemetry != null && (uint)index < (uint)TelemetryCount)
                Telemetry[index] = default;

            if (MockDoseRad != null && (uint)index < (uint)MockDoseCount)
                MockDoseRad[index] = 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockRadiationDoseJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* MockDoseRad;
        public RadiationMutationTuningDTO Tuning;
        public float TimeSeconds;
        public int Count;

        public void Execute(int index)
        {
            if (MockDoseRad == null || (uint)index >= (uint)Count)
                return;

            RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(Tuning);
            float phase = math.frac(ShinobuRadiationMutationJobMath.SanitizeFinite(TimeSeconds, 0f) * math.rcp(tuning.MockRampSeconds));
            float triangle = 1f - math.abs(phase * 2f - 1f);
            float shaped = ShinobuRadiationMutationJobMath.Smooth01(triangle);
            MockDoseRad[index] = math.max(0f, tuning.MockPeakDoseRad * shaped);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateRadiationMutationJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationStateDTO* RadiationStates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationMutationTuningDTO* TuningRows;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* MockDoseRad;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MutationStateDTO* MutationStates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationMutationTelemetryEntry* Telemetry;
        public float PreviousDoseRad;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public uint Frame;
        public int TelemetryCursor;
        public int Count;
        public int RadiationStateCount;
        public int TuningCount;
        public int MockDoseCount;
        public int MutationStateCount;
        public int TelemetryCount;
        public byte UseMockDose;

        public void Execute(int index)
        {
            if (MutationStates == null ||
                (uint)index >= (uint)Count ||
                (uint)index >= (uint)MutationStateCount)
            {
                return;
            }

            RadiationMutationTuningDTO tuning = TuningRows != null && TuningCount > 0
                ? TuningRows[0]
                : ShinobuRadiationMutationJobMath.BuildDefaultTuning();
            tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuning);

            float dt = math.clamp(ShinobuRadiationMutationJobMath.SanitizeFinite(DeltaSeconds, 0.1f), 0.0001f, 1f);
            float quality = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(GlobalQualityWeight, tuning.GlobalQualityWeight));
            RadiationStateDTO radiation = default;
            bool hasRadiation = RadiationStates != null && (uint)index < (uint)RadiationStateCount;
            if (hasRadiation)
                radiation = RadiationStates[index];

            float sourceDose = hasRadiation ? radiation.CumulativeDoseRad : 0f;
            float exposureRate = hasRadiation ? radiation.CurrentExposureRate : 0f;
            float shielding01 = hasRadiation ? radiation.ShieldingFactor01 : 0f;
            uint flags = RadiationMutationFlags.None;

            if (UseMockDose != 0 && MockDoseRad != null && (uint)index < (uint)MockDoseCount)
            {
                sourceDose = math.max(sourceDose, MockDoseRad[index]);
                flags |= RadiationMutationFlags.MockDose;
            }

            sourceDose = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(sourceDose, 0f));
            exposureRate = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(exposureRate, 0f));
            shielding01 = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(shielding01, 0f));
            float attenuatedRate = exposureRate * (1f - shielding01);
            float attenuatedDose = math.max(sourceDose, sourceDose + attenuatedRate * dt);
            float doseT = math.saturate((attenuatedDose - tuning.SafeDoseRad) * math.rcp(math.max(0.0001f, tuning.FatalDoseRad - tuning.SafeDoseRad)));
            float targetSeverity = ShinobuRadiationMutationJobMath.Smooth01(doseT);

            MutationStateDTO previous = MutationStates[index];
            float previousSeverity = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(previous.MutationSeverity01, 0f));
            float previousDose = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(PreviousDoseRad, sourceDose));
            bool healing = attenuatedDose + 0.001f < previousDose;
            float rate = targetSeverity > previousSeverity ? tuning.SeverityRisePerSecond : tuning.SeverityFallPerSecond;
            if (healing)
            {
                float decay = ShinobuRadiationMutationJobMath.ApproximateExpNegPositive(tuning.HealingDecayPerSecond * dt);
                targetSeverity = math.min(targetSeverity, previousSeverity * decay);
                flags |= RadiationMutationFlags.Healing;
            }

            float blend = math.saturate(rate * dt);
            float severity = math.saturate(math.lerp(previousSeverity, targetSeverity, blend));
            float staminaPenalty = math.saturate(severity * tuning.MaxStaminaPenaltyPercent);
            float healingSuppression = math.saturate(severity * (healing ? 0.55f : 1f));
            float complexWeight = ShinobuRadiationMutationJobMath.Smooth01((quality - 0.25f) * math.rcp(0.75f));
            if (complexWeight > 0f)
                flags |= RadiationMutationFlags.ComplexNoiseAdmitted;
            if (severity > 0.0001f)
                flags |= RadiationMutationFlags.Active;
            if (severity >= 0.95f)
                flags |= RadiationMutationFlags.Critical;
            if (severity >= tuning.ToxicBloodThreshold01)
                flags |= RadiationMutationFlags.ToxicBloodVfxRequested;

            bool nonFinite = !math.isfinite(severity) ||
                             !math.isfinite(staminaPenalty) ||
                             !math.isfinite(healingSuppression) ||
                             !math.isfinite(attenuatedDose);
            if (nonFinite)
            {
                severity = 0f;
                staminaPenalty = 0f;
                healingSuppression = 0f;
                attenuatedDose = 0f;
                flags |= RadiationMutationFlags.NonFiniteSanitized;
            }

            MutationStates[index] = new MutationStateDTO
            {
                MutationSeverity01 = severity,
                MaxStaminaPenalty = staminaPenalty,
                HealingSuppression01 = healingSuppression,
                MutationFlags = flags
            };

            WriteTelemetry(index, flags, sourceDose, exposureRate, attenuatedDose, severity, staminaPenalty, healingSuppression, quality, tuning);
        }

        private void WriteTelemetry(
            int index,
            uint flags,
            float cumulativeDose,
            float exposureRate,
            float attenuatedDose,
            float severity,
            float staminaPenalty,
            float healingSuppression,
            float quality,
            RadiationMutationTuningDTO tuning)
        {
            if (Telemetry == null || TelemetryCount <= 0)
                return;

            int telemetryIndex = TelemetryCursor % TelemetryCount;
            if (telemetryIndex < 0)
                telemetryIndex += TelemetryCount;

            ulong hash = 1469598103934665603UL;
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(cumulativeDose));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(attenuatedDose));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(severity));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, math.asuint(staminaPenalty));
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, (uint)index);
            hash = ShinobuRadiationMutationJobMath.MixStateHash(hash, flags);

            Telemetry[telemetryIndex] = new RadiationMutationTelemetryEntry
            {
                StateHash = hash,
                Frame = Frame,
                Flags = flags,
                CumulativeDoseRad = cumulativeDose,
                CurrentExposureRate = exposureRate,
                AttenuatedDoseRad = attenuatedDose,
                MutationSeverity01 = severity,
                MaxStaminaPenalty = staminaPenalty,
                HealingSuppression01 = healingSuppression,
                GlobalQualityWeight = quality,
                MetabolicToxicity = math.saturate(severity * tuning.MetabolicToxicityScale),
                VfxIntensity01 = math.saturate((severity - tuning.ToxicBloodThreshold01) * math.rcp(math.max(0.0001f, 1f - tuning.ToxicBloodThreshold01))),
                RingCursor = (uint)telemetryIndex,
                SourceHash = ShinobuRadiationMutationConstants.SourceHash
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyRadiationMutationMetabolicBridgeJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public MutationStateDTO* MutationStates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicStateDTO* MetabolicStates;
        public RadiationMutationTuningDTO Tuning;
        public int Count;
        public int MutationStateCount;
        public int MetabolicStateCount;

        public void Execute(int index)
        {
            if (MutationStates == null ||
                MetabolicStates == null ||
                (uint)index >= (uint)Count ||
                (uint)index >= (uint)MutationStateCount ||
                (uint)index >= (uint)MetabolicStateCount)
            {
                return;
            }

            RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(Tuning);
            MutationStateDTO mutation = MutationStates[index];
            float severity = math.saturate(ShinobuRadiationMutationJobMath.SanitizeFinite(mutation.MutationSeverity01, 0f));
            if (severity <= 0.0001f)
                return;

            MetabolicStateDTO metabolic = MetabolicStates[index];
            float toxicity = math.clamp(ShinobuRadiationMutationJobMath.SanitizeFinite(metabolic.Toxicity, 0f), 0f, 8f);
            toxicity = math.max(toxicity, severity * tuning.MetabolicToxicityScale);
            metabolic.Toxicity = toxicity;
            metabolic.Flags |= ShinobuMetabolismVaultContract.FlagFatigue;
            if (toxicity > 0.25f)
                metabolic.Flags |= ShinobuMetabolismVaultContract.FlagToxic;
            MetabolicStates[index] = metabolic;

            mutation.MutationFlags |= RadiationMutationFlags.MetabolicBridgeApplied;
            MutationStates[index] = mutation;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct PatchRadiationMutationTelemetryJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationMutationTelemetryEntry* Telemetry;
        public int TelemetryCursor;
        public int TelemetryCount;
        public float ExecutionMicroseconds;

        public void Execute()
        {
            if (Telemetry == null || TelemetryCount <= 0)
                return;

            int telemetryIndex = TelemetryCursor % TelemetryCount;
            if (telemetryIndex < 0)
                telemetryIndex += TelemetryCount;

            RadiationMutationTelemetryEntry entry = Telemetry[telemetryIndex];
            entry.ExecutionMicroseconds = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(ExecutionMicroseconds, 0f));
            if (entry.ExecutionMicroseconds > 100f)
                entry.Flags |= RadiationMutationFlags.OverBudget;
            Telemetry[telemetryIndex] = entry;
        }
    }
}
