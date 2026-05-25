using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct InitSensoryImpairmentJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<SensoryImpairmentDTO> Impairments;
        [WriteOnly, NoAlias] public NativeArray<SensoryImpairmentTelemetryEntry> Telemetry;
        [WriteOnly, NoAlias] public NativeArray<SensoryInputDriftDebugDTO> DriftDebug;
        public int Count;

        public void Execute(int index)
        {
            if (Impairments.IsCreated && (uint)index < (uint)Count && (uint)index < (uint)Impairments.Length)
                Impairments[index] = default;

            if (Telemetry.IsCreated && (uint)index < (uint)Telemetry.Length)
                Telemetry[index] = default;

            if (DriftDebug.IsCreated && (uint)index < (uint)DriftDebug.Length)
                DriftDebug[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockToxicityDataJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<GasPhysiologyStateDTO> GasStates;
        [NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        public SensoryImpairmentTuningDTO Tuning;
        public float TimeSeconds;
        public uint Frame;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)GasStates.Length)
                return;

            SensoryImpairmentTuningDTO tuning = ShinobuSensoryImpairmentJobMath.SanitizeTuning(Tuning);
            float phase = math.frac(ShinobuSensoryImpairmentJobMath.SanitizeFinite(TimeSeconds, 0f) * math.rcp(tuning.MockCycleSeconds));
            float triangle = 1f - math.abs(phase * 2f - 1f);
            float stress = ShinobuSensoryImpairmentJobMath.Smooth01(triangle);
            float depthMeters = tuning.MockMaxDepthMeters * stress;
            float ambientAtm = ShinobuPhysiologyJobMath.DepthToPressureAtm(depthMeters);
            float hypoxicO2 = math.lerp(ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm, tuning.AnoxiaPartialPressureAtm * 0.72f, stress);
            float narcoticN2 = ambientAtm * ShinobuPhysiologyConstants.NitrogenFraction * math.lerp(1f, 1.2f, stress);
            float co2 = math.lerp(ShinobuPhysiologyConstants.CarbonDioxideFraction, ShinobuPhysiologyConstants.CarbonDioxideToxicityFullAtm * 0.45f, stress * stress);

            GasPhysiologyStateDTO gas = GasStates[index];
            gas.OxygenPartialPressure = math.max(0f, hypoxicO2);
            gas.NitrogenPartialPressure = math.max(0f, narcoticN2);
            gas.CarbonDioxidePartialPressure = math.max(0f, co2);
            gas.CnsToxicity01 = math.saturate(gas.CnsToxicity01);
            gas.NarcosisLevel01 = ShinobuPhysiologyJobMath.ResolveNitrogenNarcosis01(gas.NitrogenPartialPressure, BuildGasTuning(tuning));
            gas.StaminaDrainRate = math.max(1f, 1f + gas.NarcosisLevel01 * 0.35f);
            gas.Flags |= ShinobuPhysiologyFlags.EmergencyMockCoefficients | ShinobuPhysiologyFlags.Narcosis;
            if (gas.OxygenPartialPressure < tuning.HypoxiaPartialPressureAtm)
                gas.Flags |= ShinobuPhysiologyFlags.Hypoxia;
            GasStates[index] = gas;

            if (Environment.IsCreated && (uint)index < (uint)Environment.Length)
            {
                Environment[index] = new MockEnvironmentVitalsSignal
                {
                    DepthMeters = depthMeters,
                    AmbientPressureAtm = ambientAtm,
                    AmbientTemperatureCelsius = 2f,
                    SystemHealthIndex01 = 1f,
                    Frame = Frame,
                    Flags = MockPressureSignal.ActiveFlag,
                    AscentRateMetersPerSecond = 0f
                };
            }
        }

        private static GasPhysiologyTuningDTO BuildGasTuning(SensoryImpairmentTuningDTO tuning)
        {
            GasPhysiologyTuningDTO gasTuning = ShinobuPhysiologyJobMath.BuildDefaultGasTuning();
            gasTuning.HypoxiaPartialPressureAtm = tuning.HypoxiaPartialPressureAtm;
            gasTuning.AnoxiaPartialPressureAtm = tuning.AnoxiaPartialPressureAtm;
            gasTuning.NarcosisStartAtm = tuning.NarcosisStartAtm;
            gasTuning.NarcosisFullAtm = tuning.NarcosisFullAtm;
            return ShinobuPhysiologyJobMath.SanitizeGasTuning(gasTuning);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateSensoryImpairmentJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<GasPhysiologyStateDTO> GasStates;
        [ReadOnly, NoAlias] public NativeArray<SensoryImpairmentTuningDTO> TuningArray;
        [NoAlias] public NativeArray<SensoryImpairmentDTO> Impairments;
        public float GlobalQualityWeight;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                (uint)index >= (uint)GasStates.Length ||
                (uint)index >= (uint)Impairments.Length)
            {
                return;
            }

            SensoryImpairmentTuningDTO tuning = TuningArray.IsCreated && TuningArray.Length > 0
                ? TuningArray[0]
                : ShinobuSensoryImpairmentJobMath.BuildDefaultTuning();
            tuning = ShinobuSensoryImpairmentJobMath.SanitizeTuning(tuning);
            float quality = math.saturate(ShinobuSensoryImpairmentJobMath.SanitizeFinite(GlobalQualityWeight, tuning.GlobalQualityWeight));
            GasPhysiologyStateDTO gas = GasStates[index];

            float hypoxia01 = ShinobuSensoryImpairmentJobMath.ResolveHypoxia01(gas.OxygenPartialPressure, tuning);
            float narcosisLinear = math.saturate((math.max(0f, gas.NitrogenPartialPressure) - tuning.NarcosisStartAtm) *
                                                 math.rcp(math.max(0.0001f, tuning.NarcosisFullAtm - tuning.NarcosisStartAtm)));
            float narcosis01 = ShinobuSensoryImpairmentJobMath.Smooth01(math.max(narcosisLinear, gas.NarcosisLevel01));
            float latencyStress = ShinobuSensoryImpairmentJobMath.Smooth01(math.max(hypoxia01, narcosis01 * 0.72f));
            float latencyMs = tuning.MaxInputLatencyMilliseconds * latencyStress;
            uint flags = SensoryImpairmentFlags.None;

            if (hypoxia01 > 0.0001f)
                flags |= SensoryImpairmentFlags.HypoxiaActive;
            if (narcosis01 > 0.0001f)
                flags |= SensoryImpairmentFlags.NarcosisActive;
            if (latencyMs > 0.0001f)
                flags |= SensoryImpairmentFlags.LatencyActive;

            float complexWeight = ResolveComplexWeight(quality);
            if (complexWeight > 0f)
                flags |= SensoryImpairmentFlags.ComplexNoiseAdmitted;
            if ((gas.Flags & ShinobuPhysiologyFlags.EmergencyMockCoefficients) != 0u)
                flags |= SensoryImpairmentFlags.MockToxicity;

            if (!math.isfinite(hypoxia01) || !math.isfinite(narcosis01) || !math.isfinite(latencyMs))
            {
                hypoxia01 = 0f;
                narcosis01 = 0f;
                latencyMs = 0f;
                flags |= SensoryImpairmentFlags.NonFiniteSanitized;
            }

            Impairments[index] = new SensoryImpairmentDTO
            {
                HypoxiaVignette01 = math.saturate(hypoxia01),
                NarcosisDrift01 = math.saturate(narcosis01),
                InputLatencyMilliseconds = math.max(0f, latencyMs),
                ImpairmentFlags = flags
            };
        }

        private static float ResolveComplexWeight(float quality)
        {
            return ShinobuSensoryImpairmentJobMath.Smooth01(math.saturate((quality - 0.25f) * math.rcp(0.75f)));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CorruptPlayerInputJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<InputStateDTO> CurrentInput;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<PredictedInputDTO> PredictedInputs;
        [ReadOnly, NoAlias] public NativeArray<PredictedInputAupTargetDTO> AupTargets;
        [ReadOnly, NoAlias] public NativeArray<SensoryImpairmentDTO> Impairments;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SensoryImpairmentTelemetryEntry> Telemetry;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SensoryInputDriftDebugDTO> DriftDebug;
        public SensoryImpairmentTuningDTO Tuning;
        public double3 AupOrigin;
        public uint TickNumber;
        public uint Frame;
        public int TelemetryCursor;
        public float DeltaSeconds;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (index != 0 ||
                !CurrentInput.IsCreated ||
                CurrentInput.Length <= 0 ||
                !Impairments.IsCreated ||
                Impairments.Length <= 0)
            {
                return;
            }

            SensoryImpairmentTuningDTO tuning = ShinobuSensoryImpairmentJobMath.SanitizeTuning(Tuning);
            SensoryImpairmentDTO impairment = Impairments[0];
            float quality = math.saturate(ShinobuSensoryImpairmentJobMath.SanitizeFinite(GlobalQualityWeight, tuning.GlobalQualityWeight));
            float dt = math.clamp(ShinobuSensoryImpairmentJobMath.SanitizeFinite(DeltaSeconds, 0.016f), 0.0001f, 0.1f);

            InputStateDTO current = CurrentInput[0];
            float2 rawMove = current.MoveAxis;
            float2 rawLook = current.LookDelta;
            InputStateDTO delayed = current;
            int delayedIndex = -1;
            int currentIndex = -1;
            float latencyWeight = ResolveLatencyWeight(impairment.InputLatencyMilliseconds, tuning.MaxInputLatencyMilliseconds);
            if (PredictedInputs.IsCreated && PredictedInputs.Length > 0)
            {
                int ringLength = PredictedInputs.Length;
                int latencyFrames = math.clamp((int)math.round(impairment.InputLatencyMilliseconds * tuning.LatencyFrameRate * 0.001f), 0, ringLength - 1);
                uint delayedTick = TickNumber > (uint)latencyFrames ? TickNumber - (uint)latencyFrames : 0u;
                delayedIndex = (int)(delayedTick % (uint)ringLength);
                currentIndex = (int)(TickNumber % (uint)ringLength);
                PredictedInputDTO historical = PredictedInputs[delayedIndex];
                if ((historical._pad0 & PredictedInputFlags.Valid) != 0u)
                {
                    delayed.MoveAxis = new float2(historical.LocalMoveVector.x, historical.LocalMoveVector.z);
                    delayed.LookDelta = historical.LookDelta;
                    delayed.ButtonMask = historical.ActionButtonsMask;
                }
            }

            float3 localSeed = ResolveAupLocalSeed(delayedIndex);
            float2 drift = ResolveDriftVector(localSeed, TickNumber, quality, tuning);
            float narcosis = math.saturate(impairment.NarcosisDrift01);
            float hypoxia = math.saturate(impairment.HypoxiaVignette01);
            float driftStrength = math.saturate(math.max(narcosis, hypoxia * 0.35f));

            float2 move = math.lerp(current.MoveAxis, delayed.MoveAxis, latencyWeight);
            move += drift * (tuning.MaxNarcosisDriftScalar * driftStrength);
            move = ClampUnit(move);

            float2 look = math.lerp(current.LookDelta, delayed.LookDelta, latencyWeight);
            float2 lookDrift = new float2(drift.y, -drift.x) * (tuning.MaxLookDriftDegrees * driftStrength * dt);
            look += lookDrift;

            uint flags = impairment.ImpairmentFlags | SensoryImpairmentFlags.InputCorrupted;
            if (!math.all(math.isfinite(move)) || !math.all(math.isfinite(look)))
            {
                move = float2.zero;
                look = float2.zero;
                flags |= SensoryImpairmentFlags.NonFiniteSanitized;
            }

            current.MoveAxis = move;
            current.LookDelta = look;
            current.ButtonMask = delayed.ButtonMask;
            CurrentInput[0] = current;

            if (PredictedInputs.IsCreated && currentIndex >= 0 && currentIndex < PredictedInputs.Length)
            {
                PredictedInputDTO predicted = PredictedInputs[currentIndex];
                predicted.TickNumber = TickNumber;
                predicted.LocalMoveVector = new float3(move.x, 0f, move.y);
                predicted.LookDelta = look;
                predicted.ActionButtonsMask = current.ButtonMask;
                predicted._pad0 = (predicted._pad0 | PredictedInputFlags.Predicted | PredictedInputFlags.Valid | PredictedInputFlags.ExtrapolatedDearLie) &
                                  ~PredictedInputFlags.HasTargetAup;
                if ((flags & SensoryImpairmentFlags.NonFiniteSanitized) != 0u)
                    predicted._pad0 |= PredictedInputFlags.NonFiniteSanitized;
                PredictedInputs[currentIndex] = predicted;
            }

            ulong stateHash = ResolveStateHash(flags, impairment, localSeed, move);
            if (DriftDebug.IsCreated && DriftDebug.Length > 0)
                WriteDriftDebug(flags, impairment, rawMove, move, rawLook, look, stateHash);

            if (Telemetry.IsCreated && Telemetry.Length > 0)
                WriteTelemetry(flags, impairment, move, look, stateHash);
        }

        private float3 ResolveAupLocalSeed(int delayedIndex)
        {
            if (!AupTargets.IsCreated || AupTargets.Length <= 0 || delayedIndex < 0)
                return float3.zero;

            int index = delayedIndex % AupTargets.Length;
            if (index < 0)
                index += AupTargets.Length;

            PredictedInputAupTargetDTO target = AupTargets[index];
            if ((target.Flags & (PredictedInputFlags.HasTargetAup | PredictedInputFlags.Valid)) == 0u ||
                !math.all(math.isfinite(target.TargetAupAbsolute)))
            {
                return float3.zero;
            }

            double3 local = target.TargetAupAbsolute - AupOrigin;
            if (!math.all(math.isfinite(local)))
                return float3.zero;

            return new float3((float)local.x, (float)local.y, (float)local.z);
        }

        private static float2 ResolveDriftVector(float3 localSeed, uint tick, float quality, SensoryImpairmentTuningDTO tuning)
        {
            float cheapPhase = tick * tuning.CheapDriftFrequency +
                               math.dot(localSeed, new float3(0.00021f, 0.00013f, 0.00017f));
            float2 cheap = new float2(
                MathLodApproximation.ApproxSinBhaskara(cheapPhase * 6.28318530718f),
                MathLodApproximation.ApproxSinBhaskara((cheapPhase * 1.6180339f + 0.25f) * 6.28318530718f));
            float complexWeight = ShinobuSensoryImpairmentJobMath.Smooth01(math.saturate((quality - 0.25f) * math.rcp(0.75f)));
            float2 complex = ResolveValueNoise2(localSeed, tick) * tuning.ComplexDriftScale;
            float2 drift = math.lerp(cheap, complex, complexWeight);
            return math.normalizesafe(drift, float2.zero);
        }

        private static float2 ResolveValueNoise2(float3 localSeed, uint tick)
        {
            float2 p = new float2(localSeed.x, localSeed.z) * 0.0137f + tick * 0.0025f;
            int2 cell = (int2)math.floor(p);
            float2 f = math.frac(p);
            float2 u = f * f * (3f - 2f * f);
            float a = HashSigned(cell, tick);
            float b = HashSigned(cell + new int2(1, 0), tick);
            float c = HashSigned(cell + new int2(0, 1), tick);
            float d = HashSigned(cell + new int2(1, 1), tick);
            float x0 = math.lerp(a, b, u.x);
            float x1 = math.lerp(c, d, u.x);
            float y = math.lerp(x0, x1, u.y);
            float y2 = HashSigned(cell + new int2(3, 5), tick ^ 0x9E3779B9u);
            return new float2(y, y2);
        }

        private static float HashSigned(int2 cell, uint tick)
        {
            uint h = math.hash(new uint3((uint)cell.x, (uint)cell.y, tick));
            return ((h & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
        }

        private static float ResolveLatencyWeight(float latencyMilliseconds, float maxLatencyMilliseconds)
        {
            return ShinobuSensoryImpairmentJobMath.Smooth01(math.saturate(latencyMilliseconds * math.rcp(math.max(1f, maxLatencyMilliseconds))));
        }

        private static float2 ClampUnit(float2 value)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 1f)
                return value;
            return value * math.rsqrt(math.max(0.0001f, lengthSq));
        }

        private static ulong ResolveStateHash(uint flags, SensoryImpairmentDTO impairment, float3 localSeed, float2 move)
        {
            ulong hash = 1469598103934665603UL;
            hash = ShinobuSensoryImpairmentJobMath.MixStateHash(hash, math.asuint(impairment.HypoxiaVignette01));
            hash = ShinobuSensoryImpairmentJobMath.MixStateHash(hash, math.asuint(impairment.NarcosisDrift01));
            hash = ShinobuSensoryImpairmentJobMath.MixStateHash(hash, math.asuint(impairment.InputLatencyMilliseconds));
            hash = ShinobuSensoryImpairmentJobMath.MixStateHash(hash, math.asuint(move.x));
            hash = ShinobuSensoryImpairmentJobMath.MixStateHash(hash, math.asuint(move.y));
            hash = ShinobuSensoryImpairmentJobMath.MixStateHash(hash, math.asuint(localSeed.x));
            return ShinobuSensoryImpairmentJobMath.MixStateHash(hash, flags);
        }

        private void WriteDriftDebug(
            uint flags,
            SensoryImpairmentDTO impairment,
            float2 rawMove,
            float2 corruptedMove,
            float2 rawLook,
            float2 corruptedLook,
            ulong stateHash)
        {
            DriftDebug[0] = new SensoryInputDriftDebugDTO
            {
                RawMoveAxis = rawMove,
                CorruptedMoveAxis = corruptedMove,
                RawLookDelta = rawLook,
                CorruptedLookDelta = corruptedLook,
                HypoxiaVignette01 = impairment.HypoxiaVignette01,
                NarcosisDrift01 = impairment.NarcosisDrift01,
                Frame = Frame,
                Flags = flags,
                StateHash = stateHash
            };
        }

        private void WriteTelemetry(uint flags, SensoryImpairmentDTO impairment, float2 move, float2 look, ulong stateHash)
        {
            int telemetryIndex = TelemetryCursor % Telemetry.Length;
            if (telemetryIndex < 0)
                telemetryIndex += Telemetry.Length;

            Telemetry[telemetryIndex] = new SensoryImpairmentTelemetryEntry
            {
                StateHash = stateHash,
                Frame = Frame,
                Flags = flags,
                HypoxiaVignette01 = impairment.HypoxiaVignette01,
                NarcosisDrift01 = impairment.NarcosisDrift01,
                InputLatencyMilliseconds = impairment.InputLatencyMilliseconds,
                MoveDriftMagnitude = math.length(move),
                LookDriftMagnitude = math.length(look),
                GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                RingCursor = (uint)telemetryIndex
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct PatchSensoryTelemetryGasJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<GasPhysiologyStateDTO> GasStates;
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [NoAlias] public NativeArray<SensoryImpairmentTelemetryEntry> Telemetry;
        public int TelemetryCursor;
        public float ExecutionMicroseconds;

        public void Execute()
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return;

            int telemetryIndex = TelemetryCursor % Telemetry.Length;
            if (telemetryIndex < 0)
                telemetryIndex += Telemetry.Length;

            GasPhysiologyStateDTO gas = GasStates.IsCreated && GasStates.Length > 0 ? GasStates[0] : default;
            MockEnvironmentVitalsSignal environment = Environment.IsCreated && Environment.Length > 0 ? Environment[0] : default;
            SensoryImpairmentTelemetryEntry entry = Telemetry[telemetryIndex];
            entry.OxygenPartialPressureAtm = math.max(0f, ShinobuSensoryImpairmentJobMath.SanitizeFinite(gas.OxygenPartialPressure, 0f));
            entry.NitrogenPartialPressureAtm = math.max(0f, ShinobuSensoryImpairmentJobMath.SanitizeFinite(gas.NitrogenPartialPressure, 0f));
            entry.CarbonDioxidePartialPressureAtm = math.max(0f, ShinobuSensoryImpairmentJobMath.SanitizeFinite(gas.CarbonDioxidePartialPressure, 0f));
            entry.DepthMeters = math.max(0f, ShinobuSensoryImpairmentJobMath.SanitizeFinite(environment.DepthMeters, 0f));
            float executionMicroseconds = ShinobuSensoryImpairmentJobMath.SanitizeFinite(ExecutionMicroseconds, -1f);
            if (executionMicroseconds >= 0f)
            {
                entry.ExecutionMicroseconds = math.max(0f, executionMicroseconds);
                if (entry.ExecutionMicroseconds > 100f)
                    entry.Flags |= SensoryImpairmentFlags.OverBudget;
            }
            Telemetry[telemetryIndex] = entry;
        }
    }
}
