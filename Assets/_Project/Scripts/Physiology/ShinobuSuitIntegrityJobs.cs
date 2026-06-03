using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class ShinobuSuitIntegrityJobMath
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
        public static float DepthToPressureAtm(float depthMeters)
        {
            float depth = math.max(0f, SanitizeFinite(depthMeters, 0f));
            return ShinobuSuitIntegrityConstants.SurfacePressureAtm +
                   depth * ShinobuSuitIntegrityConstants.AtmPerMeter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition aup)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                aup.GridX * cell + aup.LocalX,
                aup.GridY * cell + aup.LocalY,
                aup.GridZ * cell + aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDepthMetersFromAup(double3 playerAup, double3 seaLevelAup)
        {
            if (!math.all(math.isfinite(playerAup)) || !math.all(math.isfinite(seaLevelAup)))
                return 0f;

            double3 delta = seaLevelAup - playerAup;
            double depth = math.max(0d, delta.y);
            return math.isfinite(depth) ? (float)math.clamp(depth, 0d, 12000d) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTickInterval(float globalQualityWeight)
        {
            float quality = math.saturate(SanitizeFinite(globalQualityWeight, 1f));
            return math.lerp(0.1f, 1.0f, 1.0f - quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveContinuousYieldScale(in SuitPressureProfileDTO profile, float quality)
        {
            float q = math.saturate(SanitizeFinite(quality, 1f));
            float low = SafePositive(profile.LowTierYieldScale, 0.65f);
            float mid = SafePositive(profile.MiddleTierYieldScale, 0.85f);
            float high = SafePositive(profile.HighTierYieldScale, 1.0f);
            float ultra = SafePositive(profile.UltraTierYieldScale, 1.2f);
            float lowToMid = math.smoothstep(0f, 0.33333334f, q);
            float midToHigh = math.smoothstep(0.33333334f, 0.6666667f, q);
            float highToUltra = math.smoothstep(0.6666667f, 1f, q);
            float value = math.lerp(low, mid, lowToMid);
            value = math.lerp(value, high, midToHigh);
            return math.lerp(value, ultra, highToUltra);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > Epsilon ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SuitPressureProfileDTO SanitizeProfile(SuitPressureProfileDTO profile, uint fallbackHash)
        {
            profile.SuitHash = profile.SuitHash != 0u ? profile.SuitHash : fallbackHash;
            profile.MaxSafePressureATM = math.max(
                ShinobuSuitIntegrityConstants.MinimumSafePressureAtm,
                SanitizeFinite(profile.MaxSafePressureATM, 61f));
            profile.YieldConstant = math.max(0f, SanitizeFinite(profile.YieldConstant, 0.004f));
            profile.CriticalFractureThreshold = math.max(0.001f, SanitizeFinite(profile.CriticalFractureThreshold, 1f));
            profile.FractureIntegrityDamageRate = math.max(0f, SanitizeFinite(profile.FractureIntegrityDamageRate, 0.08f));
            profile.VisualBucklingGain = math.max(0f, SanitizeFinite(profile.VisualBucklingGain, 0.2f));
            profile.GroanOverpressureThreshold = math.max(0f, SanitizeFinite(profile.GroanOverpressureThreshold, 0.05f));
            profile.LowTierYieldScale = SafePositive(profile.LowTierYieldScale, 0.65f);
            profile.MiddleTierYieldScale = SafePositive(profile.MiddleTierYieldScale, 0.85f);
            profile.HighTierYieldScale = SafePositive(profile.HighTierYieldScale, 1.0f);
            profile.UltraTierYieldScale = SafePositive(profile.UltraTierYieldScale, 1.2f);
            return profile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SuitIntegrityTuningDTO SanitizeTuning(SuitIntegrityTuningDTO tuning)
        {
            tuning.GlobalQualityWeight = math.saturate(SanitizeFinite(tuning.GlobalQualityWeight, 1f));
            tuning.TickBudgetMicroseconds = math.max(1f, SanitizeFinite(tuning.TickBudgetMicroseconds, ShinobuSuitIntegrityConstants.DefaultTickBudgetMicroseconds));
            tuning.WarningOverpressure = math.max(0f, SanitizeFinite(tuning.WarningOverpressure, 0.05f));
            tuning.BuckleOverpressure = math.max(tuning.WarningOverpressure, SanitizeFinite(tuning.BuckleOverpressure, 0.2f));
            tuning.CatastrophicIntegrity01 = math.saturate(SanitizeFinite(tuning.CatastrophicIntegrity01, 0.001f));
            tuning.AcousticIntervalMinSeconds = math.max(0.1f, SanitizeFinite(tuning.AcousticIntervalMinSeconds, 0.25f));
            tuning.AcousticIntervalMaxSeconds = math.max(tuning.AcousticIntervalMinSeconds, SanitizeFinite(tuning.AcousticIntervalMaxSeconds, 1.5f));
            tuning.VisualDeformationGain = math.max(0f, SanitizeFinite(tuning.VisualDeformationGain, 1f));
            tuning.MockMaxDepthMeters = math.max(1f, SanitizeFinite(tuning.MockMaxDepthMeters, 8000f));
            tuning.MockDurationSeconds = math.max(0.1f, SanitizeFinite(tuning.MockDurationSeconds, 10f));
            tuning.DefaultSuitHash = tuning.DefaultSuitHash != 0u ? tuning.DefaultSuitHash : ShinobuSuitIntegrityConstants.StandardSuitHash;
            tuning.Version = tuning.Version != 0u ? tuning.Version : 1u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MixHash(ulong hash, uint value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockHydrostaticPressureJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SuitHydrostaticMockAupDTO> Samples;
        public double3 SeaLevelAup;
        public float MaxDepthMeters;
        public float DurationSeconds;
        public uint FrameBase;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Samples.Length)
                return;

            float denom = math.max(1f, Count - 1);
            float t = math.saturate(index * math.rcp(denom));
            float depth = math.max(0f, MaxDepthMeters) * t;
            double3 playerAup = SeaLevelAup;
            playerAup.y -= depth;
            Samples[index] = new SuitHydrostaticMockAupDTO
            {
                PlayerAup = playerAup,
                SeaLevelAup = SeaLevelAup,
                TimeSeconds = math.max(0f, DurationSeconds) * t,
                DepthMeters = depth,
                Frame = FrameBase + (uint)index,
                Flags = SuitIntegrityFlags.MockProfile
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct InitializeSuitIntegrityJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<SuitIntegrityDTO> Integrity;
        public uint DefaultSuitHash;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Integrity.Length)
                return;

            Integrity[index] = new SuitIntegrityDTO
            {
                CurrentIntegrity01 = 1f,
                AppliedPressureATM = ShinobuSuitIntegrityConstants.SurfacePressureAtm,
                MicroFractureAccumulation = 0f,
                EquippedSuitHash = DefaultSuitHash != 0u ? DefaultSuitHash : ShinobuSuitIntegrityConstants.StandardSuitHash,
                IntegrityFlags = SuitIntegrityFlags.Initialized
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateHydrostaticPressureJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SuitIntegrityDTO> Integrity;
        [ReadOnly, NoAlias] public NativeArray<SuitHydrostaticMockAupDTO> MockAups;
        public AbsoluteUniversePosition PlayerAup;
        public double3 PlayerAupOverride;
        public double3 SeaLevelAup;
        public SuitIntegrityTuningDTO Tuning;
        public uint Frame;
        public int Count;
        public byte UseMockAup;
        public byte UsePlayerAupOverride;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Integrity.Length)
                return;

            double3 playerAup = UsePlayerAupOverride != 0
                ? PlayerAupOverride
                : ShinobuSuitIntegrityJobMath.ToAbsoluteDouble3(in PlayerAup);
            double3 seaLevelAup = SeaLevelAup;
            uint flags = 0u;
            if (UseMockAup != 0 && MockAups.IsCreated && MockAups.Length > 0)
            {
                int sampleIndex = (int)(Frame % (uint)MockAups.Length);
                SuitHydrostaticMockAupDTO sample = MockAups[sampleIndex];
                playerAup = sample.PlayerAup;
                seaLevelAup = sample.SeaLevelAup;
                flags |= SuitIntegrityFlags.MockProfile;
            }

            bool finiteAupInput = math.all(math.isfinite(playerAup)) && math.all(math.isfinite(seaLevelAup));
            float depthMeters = ShinobuSuitIntegrityJobMath.ResolveDepthMetersFromAup(playerAup, seaLevelAup);
            float pressureAtm = finiteAupInput
                ? ShinobuSuitIntegrityJobMath.DepthToPressureAtm(depthMeters)
                : ShinobuSuitIntegrityConstants.SurfacePressureAtm;
            flags |= finiteAupInput ? 0u : SuitIntegrityFlags.NonFinitePressure;
            void* integrityPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Integrity);
            ref SuitIntegrityDTO state = ref UnsafeUtility.AsRef<SuitIntegrityDTO>(
                (byte*)integrityPtr + index * UnsafeUtility.SizeOf<SuitIntegrityDTO>());
            if ((state.IntegrityFlags & SuitIntegrityFlags.Initialized) == 0u)
            {
                state.CurrentIntegrity01 = 1f;
                state.MicroFractureAccumulation = 0f;
                state.EquippedSuitHash = Tuning.DefaultSuitHash != 0u ? Tuning.DefaultSuitHash : ShinobuSuitIntegrityConstants.StandardSuitHash;
                state.IntegrityFlags = SuitIntegrityFlags.Initialized;
            }

            if (!math.isfinite(pressureAtm))
            {
                pressureAtm = ShinobuSuitIntegrityConstants.SurfacePressureAtm;
                flags |= SuitIntegrityFlags.NonFinitePressure;
            }

            state.AppliedPressureATM = pressureAtm;
            state.IntegrityFlags = (state.IntegrityFlags & ~(SuitIntegrityFlags.MockProfile | SuitIntegrityFlags.NonFinitePressure)) | flags;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateStructuralYieldJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SuitIntegrityDTO> Integrity;
        [NoAlias] public NativeArray<SuitIntegrityVisualDTO> Visuals;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Telemetry is intentionally not lane-local. The black-box contract is a
        // 300-frame circular history, so the write index is the owner-maintained TelemetryCursor rather than
        // Execute(index). Only Execute(0) reaches WriteTelemetry; every other lane returns without touching Telemetry.
        // This preserves a single authoritative row per completed solver tick and avoids parallel writers racing on
        // adjacent cache lines.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: The runtime locks the telemetry Vault buffer before scheduling the pressure
        // and yield job chain, then unlocks it only after LateFrameTick observes completion or teardown explicitly
        // completes the chain. No other owner route writes Vault 72513 during that window, and public TryGet accessors
        // return false while _jobScheduled is true, so the bypass is scoped to the known non-indexed ring write.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: Splitting the telemetry write into a second tiny job was rejected because it
        // adds another scheduler node and same-frame dependency for one 64-byte row. The single-chain write keeps the
        // work data-local, bounded, and dispatcher-fenced. The annotation is therefore limited to Telemetry; Integrity
        // and Visuals remain normal lane-local NativeArray writes.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SuitIntegrityTelemetryEntry> Telemetry;
        [ReadOnly, NoAlias] public NativeArray<SuitPressureProfileDTO> Profiles;
        [WriteOnly, NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<CombatDamageSignal>.ParallelWriter DamageWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> DamageWriterBudget;
        [WriteOnly, NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<MovementAcousticSignal>.ParallelWriter AcousticWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> AcousticWriterBudget;
        public AbsoluteUniversePosition PlayerAup;
        public double3 PlayerImpactAup;
        public SuitIntegrityTuningDTO Tuning;
        public uint PlayerTargetHash;
        public uint Frame;
        public int Count;
        public int ProfileCount;
        public int TelemetryCursor;
        public float DeltaSeconds;
        public float TickIntervalSeconds;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Integrity.Length)
                return;

            SuitIntegrityTuningDTO tuning = ShinobuSuitIntegrityJobMath.SanitizeTuning(Tuning);
            void* integrityPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Integrity);
            ref SuitIntegrityDTO state = ref UnsafeUtility.AsRef<SuitIntegrityDTO>(
                (byte*)integrityPtr + index * UnsafeUtility.SizeOf<SuitIntegrityDTO>());
            SuitPressureProfileDTO profile = ResolveProfile(state.EquippedSuitHash, tuning.DefaultSuitHash);
            float dt = math.clamp(ShinobuSuitIntegrityJobMath.SanitizeFinite(DeltaSeconds, 0.1f), 0.0001f, 1.25f);
            float safePressure = math.max(ShinobuSuitIntegrityConstants.MinimumSafePressureAtm, profile.MaxSafePressureATM);
            float pressure = math.max(ShinobuSuitIntegrityConstants.SurfacePressureAtm, ShinobuSuitIntegrityJobMath.SanitizeFinite(state.AppliedPressureATM, 1f));
            float overpressure = math.max(0f, (pressure - safePressure) * math.rcp(safePressure));
            float previousIntegrity = math.saturate(ShinobuSuitIntegrityJobMath.SanitizeFinite(state.CurrentIntegrity01, 1f));
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float presentationYieldScale = ShinobuSuitIntegrityJobMath.ResolveContinuousYieldScale(in profile, quality);
            float fractureDelta = overpressure * overpressure * profile.YieldConstant * dt;
            float fracture = math.min(1024f, math.max(0f, ShinobuSuitIntegrityJobMath.SanitizeFinite(state.MicroFractureAccumulation, 0f)) + fractureDelta);
            float excessFracture = math.max(0f, fracture - profile.CriticalFractureThreshold);
            float damage = excessFracture * profile.FractureIntegrityDamageRate * dt;
            float integrity = math.saturate(previousIntegrity - damage);
            float visualBuckle = math.saturate((overpressure - tuning.BuckleOverpressure) * profile.VisualBucklingGain * tuning.VisualDeformationGain * presentationYieldScale);
            bool currentPressureFault = (state.IntegrityFlags & SuitIntegrityFlags.NonFinitePressure) != 0u;
            uint flags = state.IntegrityFlags & ~(SuitIntegrityFlags.Warning | SuitIntegrityFlags.Buckling | SuitIntegrityFlags.AcousticGroan | SuitIntegrityFlags.NonFinitePressure);
            flags |= overpressure > tuning.WarningOverpressure ? SuitIntegrityFlags.Warning : 0u;
            flags |= visualBuckle > 0f ? SuitIntegrityFlags.Buckling : 0u;
            flags |= currentPressureFault ? SuitIntegrityFlags.NonFinitePressure : 0u;
            flags |= !math.isfinite(pressure + overpressure + fracture + integrity) ? SuitIntegrityFlags.NonFinitePressure : 0u;

            bool implodedNow = integrity <= tuning.CatastrophicIntegrity01 &&
                               previousIntegrity > tuning.CatastrophicIntegrity01 &&
                               (flags & SuitIntegrityFlags.Imploded) == 0u;
            if (implodedNow)
            {
                flags |= SuitIntegrityFlags.Imploded;
                EnqueueImplosionDamage();
            }

            if (ShouldEmitAcousticGroan(overpressure, in profile, in tuning, quality))
            {
                flags |= SuitIntegrityFlags.AcousticGroan;
                EnqueueAcousticGroan(overpressure, visualBuckle);
            }

            state.CurrentIntegrity01 = integrity;
            state.MicroFractureAccumulation = fracture;
            state.IntegrityFlags = flags;

            SuitIntegrityVisualDTO visual = new SuitIntegrityVisualDTO
            {
                AppliedPressureATM = pressure,
                OverpressureScalar = overpressure,
                Buckling01 = visualBuckle,
                CurrentIntegrity01 = integrity,
                MicroFractureAccumulation = fracture,
                GlobalQualityWeight = quality,
                Flags = flags,
                Frame = Frame
            };
            if (Visuals.IsCreated && (uint)index < (uint)Visuals.Length)
                Visuals[index] = visual;

            if (index == 0 && Telemetry.IsCreated && Telemetry.Length > 0)
                WriteTelemetry(in visual, in state, in profile);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SuitPressureProfileDTO ResolveProfile(uint suitHash, uint fallbackHash)
        {
            uint desiredHash = suitHash != 0u ? suitHash : fallbackHash;
            int limit = math.min(ProfileCount, Profiles.IsCreated ? Profiles.Length : 0);
            for (int i = 0; i < limit; i++)
            {
                SuitPressureProfileDTO profile = Profiles[i];
                if (profile.SuitHash == desiredHash && profile.SuitHash != 0u)
                    return ShinobuSuitIntegrityJobMath.SanitizeProfile(profile, desiredHash);
            }

            return ShinobuSuitIntegrityJobMath.SanitizeProfile(default, desiredHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldEmitAcousticGroan(float overpressure, in SuitPressureProfileDTO profile, in SuitIntegrityTuningDTO tuning, float quality)
        {
            if (overpressure <= profile.GroanOverpressureThreshold)
                return false;

            float intervalSeconds = math.lerp(tuning.AcousticIntervalMaxSeconds, tuning.AcousticIntervalMinSeconds, quality);
            uint intervalFrames = (uint)math.max(1, (int)math.round(intervalSeconds * 10f));
            return intervalFrames == 0u || Frame % intervalFrames == 0u;
        }

        private void EnqueueImplosionDamage()
        {
            uint target = PlayerTargetHash != 0u ? PlayerTargetHash : ShinobuSuitIntegrityConstants.PlayerTargetHash;
            if (target == 0u)
                return;

            CombatDamageSignal damage = default;
            damage.ImpactAup = PlayerImpactAup;
            damage.Direction = new float3(0f, 1f, 0f);
            damage.Magnitude = 9999f;
            damage.DamageType = ShinobuSuitIntegrityConstants.CombatDamageTypeBarotraumaImplosion;
            damage.TargetHash = target;
            damage.SourceHash = ShinobuSuitIntegrityConstants.SourceHash;
            damage.Frame = Frame;
            damage.SourceId = unchecked((ushort)ShinobuSuitIntegrityConstants.SourceHash);
            damage.TargetId = 0;
            damage.Channel = 0;
            damage.Flags = CombatDamageSignal.DirectRuntimeFlag;
            damage.IntegrityDelta = byte.MaxValue;
            SignalBus<CombatDamageSignal>.TryEnqueueBounded(DamageWriter, DamageWriterBudget, damage);
        }

        private void EnqueueAcousticGroan(float overpressure, float visualBuckle)
        {
            MovementAcousticSignal signal = default;
            signal.PositionAup = PlayerAup;
            signal.Volume = math.saturate(0.15f + overpressure * 0.2f + visualBuckle * 0.65f);
            signal.VelocitySq = overpressure * overpressure;
            signal.SourceId = ShinobuSuitIntegrityConstants.AcousticSourceMetalGroan;
            signal.LocomotionMode = 0;
            signal.SurfaceMode = 0;
            signal.Flags = 1;
            SignalBus<MovementAcousticSignal>.TryEnqueueBounded(AcousticWriter, AcousticWriterBudget, signal);
        }

        private void WriteTelemetry(in SuitIntegrityVisualDTO visual, in SuitIntegrityDTO state, in SuitPressureProfileDTO profile)
        {
            int index = TelemetryCursor;
            if (Telemetry.Length > 0)
                index %= Telemetry.Length;
            ulong hash = 1469598103934665603UL;
            hash = ShinobuSuitIntegrityJobMath.MixHash(hash, Frame);
            hash = ShinobuSuitIntegrityJobMath.MixHash(hash, math.asuint(visual.AppliedPressureATM));
            hash = ShinobuSuitIntegrityJobMath.MixHash(hash, math.asuint(visual.OverpressureScalar));
            hash = ShinobuSuitIntegrityJobMath.MixHash(hash, math.asuint(visual.MicroFractureAccumulation));
            hash = ShinobuSuitIntegrityJobMath.MixHash(hash, math.asuint(visual.CurrentIntegrity01));
            hash = ShinobuSuitIntegrityJobMath.MixHash(hash, state.IntegrityFlags);
            Telemetry[index] = new SuitIntegrityTelemetryEntry
            {
                StateHash = hash,
                Frame = Frame,
                EntityHash = PlayerTargetHash,
                DepthMeters = (visual.AppliedPressureATM - ShinobuSuitIntegrityConstants.SurfacePressureAtm) * math.rcp(ShinobuSuitIntegrityConstants.AtmPerMeter),
                AppliedPressureATM = visual.AppliedPressureATM,
                OverpressureScalar = visual.OverpressureScalar,
                MicroFractureAccumulation = visual.MicroFractureAccumulation,
                CurrentIntegrity01 = visual.CurrentIntegrity01,
                VisualBuckling01 = visual.Buckling01,
                ExecutionMicroseconds = 0f,
                Flags = state.IntegrityFlags,
                EquippedSuitHash = profile.SuitHash,
                TickIntervalSeconds = TickIntervalSeconds,
                SignalFlags = (state.IntegrityFlags & (SuitIntegrityFlags.Imploded | SuitIntegrityFlags.AcousticGroan))
            };
        }
    }
}
