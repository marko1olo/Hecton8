using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    public static class BaseAtmosphereFlags
    {
        public const ushort Sealed = 1 << 0;
        public const ushort Unsealed = 1 << 1;
        public const ushort PlayerInside = 1 << 2;
        public const ushort ScrubberPowered = 1 << 3;
        public const ushort RenderFogRequested = 1 << 4;
        public const ushort TraumaGlitchRequested = 1 << 5;
        public const ushort BubbleVfxRequested = 1 << 6;
        public const ushort HelioxMix = 1 << 7;
        public const ushort Hypercapnia = 1 << 8;
        public const ushort BendsDamageRequested = 1 << 9;
        public const ushort VisualBlurRequested = 1 << 10;
        public const ushort SmokeParticlesRequested = 1 << 11;
    }

    public enum BaseAtmosphereSolveMode : byte
    {
        High5Hz = 0,
        ActiveCompartment1Hz = 1
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct CompartmentState
    {
        public float OxygenKPa;
        public float CarbonDioxideKPa;
        public float NitrogenKPa;
        public float TotalPressureKPa;
        public float InvMaxPressureKPa;
        public float OxygenBaseConsumptionKPaPerSecond;
        public float CarbonDioxideGenerationKPaPerSecond;
        public ushort Flags;
        public byte Toxicity;
        public byte HumidityPercent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AtmospherePhysiologyHazard
    {
        public float HealthDamage;
        public float VisualBlur01;
        public float NitrogenTissueLoading;
        public float StaminaRecoveryMultiplier;
        public float NarcosisInputOffset;
        public ushort Flags;
    }

    public static class BaseAtmosphereMath
    {
        public const int DefaultCompartmentCapacity = 50;
        public const float HighTickSeconds = 0.2f;
        public const float LowColdTickSeconds = 1f;
        public const float AirlockEqualizationSeconds = 5f;
        public const float HypercapniaCarbonDioxideFraction = 0.05f;
        public const float BendsDepthMeters = 100f;
        public const float BendsAscentMetersPerSecond = 10f;
        public const float BendsHealthDamage = 8f;
        public const float NarcosisDepthMeters = 150f;

        private const float DefaultMaxPressureKPa = 101.325f;
        private const float DefaultOxygenFraction = 0.2095f;
        private const float DefaultCarbonDioxideFraction = 0.0004f;
        private const float NarcosisFullRangeInv = 0.006666667f;
        private const float ByteScale = 255f;
        private const uint LcgMultiplier = 1664525u;
        private const uint LcgIncrement = 1013904223u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighScalability(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveColdTickIntervalSeconds(HectonQualityTier tier)
        {
            return IsHighScalability(tier) ? HighTickSeconds : LowColdTickSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BaseAtmosphereSolveMode ResolveSolveMode(HectonQualityTier tier)
        {
            return IsHighScalability(tier)
                ? BaseAtmosphereSolveMode.High5Hz
                : BaseAtmosphereSolveMode.ActiveCompartment1Hz;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompartmentState CreateDefaultCompartment(float maxPressureKPa, ushort flags)
        {
            float safeMaxPressure = FinitePositiveOrDefault(maxPressureKPa, DefaultMaxPressureKPa);
            float oxygen = safeMaxPressure * DefaultOxygenFraction;
            float carbonDioxide = safeMaxPressure * DefaultCarbonDioxideFraction;
            float nitrogen = math.max(0f, safeMaxPressure - oxygen - carbonDioxide);
            return new CompartmentState
            {
                OxygenKPa = oxygen,
                CarbonDioxideKPa = carbonDioxide,
                NitrogenKPa = nitrogen,
                TotalPressureKPa = ResolveDaltonPressureFake(oxygen, carbonDioxide, nitrogen),
                InvMaxPressureKPa = math.rcp(safeMaxPressure),
                Flags = (ushort)(flags | BaseAtmosphereFlags.Sealed),
                HumidityPercent = 45
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDaltonPressureFake(float oxygenKPa, float carbonDioxideKPa, float nitrogenKPa)
        {
            return math.max(0f, FiniteNonNegative(oxygenKPa)) +
                   math.max(0f, FiniteNonNegative(carbonDioxideKPa)) +
                   math.max(0f, FiniteNonNegative(nitrogenKPa));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolvePlayerOxygenConsumption(float baseRateKPaPerSecond, float stressMultiplier)
        {
            return FiniteNonNegative(baseRateKPaPerSecond) * math.max(1f, FinitePositiveOrDefault(stressMultiplier, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFlag(ushort flags, ushort flag)
        {
            return (flags & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSealed(ushort flags)
        {
            return (flags & BaseAtmosphereFlags.Sealed) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnsealed(ushort flags)
        {
            return (flags & BaseAtmosphereFlags.Unsealed) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BypassesNitrogenNarcosis(ushort flags)
        {
            return (flags & BaseAtmosphereFlags.HelioxMix) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ClearFlags(ushort flags, ushort flagsToClear)
        {
            return (ushort)(flags & (ushort)(flagsToClear ^ 0xFFFF));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompartmentState StepCompartment(
            CompartmentState state,
            float deltaTime,
            float stressMultiplier,
            float logisticsPowerWatts,
            float scrubberKPaPerSecond,
            float suitRuptureDamage,
            float suitRuptureThreshold,
            float suitRuptureDrainPerSecond,
            bool highScalability)
        {
            float dt = FiniteNonNegative(deltaTime);
            float oxygenConsumption = ResolvePlayerOxygenConsumption(
                state.OxygenBaseConsumptionKPaPerSecond,
                stressMultiplier);
            state.OxygenKPa = math.max(0f, FiniteNonNegative(state.OxygenKPa) - oxygenConsumption * dt);
            state.CarbonDioxideKPa = math.max(
                0f,
                FiniteNonNegative(state.CarbonDioxideKPa) +
                FiniteNonNegative(state.CarbonDioxideGenerationKPaPerSecond) * dt);

            if (logisticsPowerWatts > 0f && HasFlag(state.Flags, BaseAtmosphereFlags.ScrubberPowered))
            {
                state.CarbonDioxideKPa = math.max(
                    0f,
                    state.CarbonDioxideKPa - FiniteNonNegative(scrubberKPaPerSecond) * dt);
            }

            if (suitRuptureDamage > suitRuptureThreshold)
            {
                float ruptureFactor = ResolveSuitRuptureOxygenFactor(
                    suitRuptureDamage,
                    suitRuptureThreshold,
                    suitRuptureDrainPerSecond,
                    dt);
                state.OxygenKPa *= ruptureFactor;
                state.Flags = (ushort)(state.Flags | BaseAtmosphereFlags.BubbleVfxRequested);
            }

            state.TotalPressureKPa = ResolveDaltonPressureFake(
                state.OxygenKPa,
                state.CarbonDioxideKPa,
                state.NitrogenKPa);

            float carbonDioxideFraction = ResolveGasFraction(state.CarbonDioxideKPa, state.TotalPressureKPa);
            if (carbonDioxideFraction > HypercapniaCarbonDioxideFraction)
            {
                state.Flags = (ushort)(state.Flags | BaseAtmosphereFlags.Hypercapnia | BaseAtmosphereFlags.TraumaGlitchRequested);
            }
            else
            {
                state.Flags = ClearFlags(
                    state.Flags,
                    (ushort)(BaseAtmosphereFlags.Hypercapnia | BaseAtmosphereFlags.TraumaGlitchRequested));
            }

            if (highScalability && state.HumidityPercent > 90)
                state.Flags = (ushort)(state.Flags | BaseAtmosphereFlags.RenderFogRequested);
            else
                state.Flags = ClearFlags(state.Flags, BaseAtmosphereFlags.RenderFogRequested);

            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte EncodeCarbonDioxideByte(float carbonDioxideKPa, float totalPressureKPa)
        {
            int encoded = (int)math.round(math.saturate(ResolveGasFraction(carbonDioxideKPa, totalPressureKPa)) * ByteScale);
            return (byte)math.clamp(encoded, 0, 255);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReduceCarbonDioxideByte(byte value, byte reduction)
        {
            int next = value - reduction;
            return (byte)math.max(0, next);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte SaturatingAddByte(byte value, byte increment)
        {
            int next = value + increment;
            return (byte)math.min(255, next);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveGasFraction(float gasKPa, float totalPressureKPa)
        {
            float safeTotal = FiniteNonNegative(totalPressureKPa);
            return safeTotal > 0.0001f ? math.saturate(FiniteNonNegative(gasKPa) * math.rcp(safeTotal)) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolvePressureGauge01(CompartmentState state)
        {
            return math.saturate(FiniteNonNegative(state.TotalPressureKPa) * FiniteNonNegative(state.InvMaxPressureKPa));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveOxygenWholePercent(CompartmentState state)
        {
            float oxygenPercent = FiniteNonNegative(state.OxygenKPa) * FiniteNonNegative(state.InvMaxPressureKPa) * 100f;
            return math.clamp((int)math.round(oxygenPercent), 0, 999);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveStaminaRecoveryMultiplierForCarbonDioxide(float carbonDioxideFraction)
        {
            return carbonDioxideFraction > HypercapniaCarbonDioxideFraction ? 0.5f : 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldApplyImmediateBends(float originDepthMeters, float ascentMetersPerSecond)
        {
            return math.isfinite(originDepthMeters) &&
                   math.isfinite(ascentMetersPerSecond) &&
                   originDepthMeters > BendsDepthMeters &&
                   ascentMetersPerSecond > BendsAscentMetersPerSecond;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveNitrogenTissueLoading(
            float currentLoading,
            float depthMeters,
            float deltaTime,
            ushort breathingFlags)
        {
            float current = FiniteNonNegative(currentLoading);
            if (BypassesNitrogenNarcosis(breathingFlags))
                return math.max(0f, current - FiniteNonNegative(deltaTime) * 0.25f);

            float target = math.max(0f, FiniteNonNegative(depthMeters) - 10f) * 0.01f;
            float alpha = math.saturate(FiniteNonNegative(deltaTime) * 0.05f);
            return current + (target - current) * alpha;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtmospherePhysiologyHazard ResolvePhysiologyHazard(
            float nitrogenTissueLoading,
            float originDepthMeters,
            float ascentMetersPerSecond,
            float currentDepthMeters,
            float timeSeconds,
            uint playerSeed,
            bool isInsideSubmarine,
            ushort breathingFlags,
            float carbonDioxideFraction,
            float deltaTime)
        {
            AtmospherePhysiologyHazard hazard;
            hazard.HealthDamage = 0f;
            hazard.VisualBlur01 = 0f;
            hazard.NitrogenTissueLoading = ResolveNitrogenTissueLoading(
                nitrogenTissueLoading,
                currentDepthMeters,
                deltaTime,
                breathingFlags);
            hazard.StaminaRecoveryMultiplier = ResolveStaminaRecoveryMultiplierForCarbonDioxide(carbonDioxideFraction);
            hazard.NarcosisInputOffset = ResolveNarcosisTriangleOffset(
                currentDepthMeters,
                timeSeconds,
                playerSeed,
                isInsideSubmarine,
                BypassesNitrogenNarcosis(breathingFlags));
            hazard.Flags = 0;

            if (ShouldApplyImmediateBends(originDepthMeters, ascentMetersPerSecond))
            {
                hazard.HealthDamage = BendsHealthDamage;
                hazard.VisualBlur01 = 1f;
                hazard.Flags = (ushort)(hazard.Flags | BaseAtmosphereFlags.BendsDamageRequested | BaseAtmosphereFlags.VisualBlurRequested);
            }

            if (carbonDioxideFraction > HypercapniaCarbonDioxideFraction)
                hazard.Flags = (ushort)(hazard.Flags | BaseAtmosphereFlags.TraumaGlitchRequested | BaseAtmosphereFlags.Hypercapnia);

            return hazard;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveNarcosisTriangleOffset(
            float depthMeters,
            float timeSeconds,
            uint playerSeed,
            bool isInsideSubmarine,
            bool helioxMix)
        {
            if (isInsideSubmarine || helioxMix || !math.isfinite(depthMeters) || depthMeters <= NarcosisDepthMeters)
                return 0f;

            float severity = math.saturate((depthMeters - NarcosisDepthMeters) * NarcosisFullRangeInv);
            uint timeTick = (uint)math.max(0, (int)math.min(2147483647f, FiniteNonNegative(timeSeconds) * 60f));
            uint hash = AdvanceLcg(playerSeed ^ timeTick);
            float phase = FiniteNonNegative(timeSeconds) * 1.37f + ((hash & 0xFFFFu) * 0.000015259022f);
            return SignedTriangle01(phase) * 0.22f * severity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCrushDepthDamage(float overDepthMeters)
        {
            float overDepth = FiniteNonNegative(overDepthMeters);
            return overDepth > 0f ? overDepth * overDepth * math.rsqrt(overDepth) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSuitRuptureOxygenFactor(
            float damage,
            float threshold,
            float drainPerSecond,
            float deltaTime)
        {
            float overDamage = math.max(0f, FiniteNonNegative(damage) - FiniteNonNegative(threshold));
            float x = overDamage * FiniteNonNegative(drainPerSecond) * FiniteNonNegative(deltaTime);
            float denominator = 1f + x + 0.5f * x * x;
            return math.saturate(math.rcp(math.max(0.0001f, denominator)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint AdvanceLcg(uint state)
        {
            return state * LcgMultiplier + LcgIncrement;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SignedTriangle01(float phase)
        {
            float wrapped = math.frac(phase);
            return 1f - 4f * math.abs(wrapped - 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FinitePositiveOrDefault(float value, float fallback)
        {
            return math.isfinite(value) && value > 0.0001f ? value : fallback;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct BaseAtmosphereColdTickJob : IJob
    {
        [ReadOnly] public NativeArray<CompartmentState> Input;
        [WriteOnly] public NativeArray<CompartmentState> Output;
        public NativeArray<byte> CarbonDioxideByteLane;
        public int CompartmentCount;
        public int ActiveCompartmentIndex;
        public float DeltaTime;
        public float PlayerStressMultiplier;
        public float LogisticsPowerWatts;
        public float ScrubberKPaPerSecond;
        public float SuitRuptureDamage;
        public float SuitRuptureThreshold;
        public float SuitRuptureDrainPerSecond;
        public byte SolveMode;
        public byte ScrubberBytePerColdTick;
        public byte ScalabilityHigh;

        public void Execute()
        {
            int count = math.min(CompartmentCount, math.min(Input.Length, Output.Length));
            int active = math.clamp(ActiveCompartmentIndex, 0, math.max(0, count - 1));
            bool updateAll = SolveMode == (byte)BaseAtmosphereSolveMode.High5Hz;
            bool highScalability = ScalabilityHigh != 0;

            for (int i = 0; i < count; i++)
            {
                CompartmentState state = Input[i];
                if (!updateAll && i != active)
                {
                    Output[i] = state;
                    continue;
                }

                state = BaseAtmosphereMath.StepCompartment(
                    state,
                    DeltaTime,
                    PlayerStressMultiplier,
                    LogisticsPowerWatts,
                    ScrubberKPaPerSecond,
                    SuitRuptureDamage,
                    SuitRuptureThreshold,
                    SuitRuptureDrainPerSecond,
                    highScalability);

                if (i < CarbonDioxideByteLane.Length)
                {
                    byte carbonDioxideByte = BaseAtmosphereMath.EncodeCarbonDioxideByte(
                        state.CarbonDioxideKPa,
                        state.TotalPressureKPa);
                    if (LogisticsPowerWatts > 0f && BaseAtmosphereMath.HasFlag(state.Flags, BaseAtmosphereFlags.ScrubberPowered))
                        carbonDioxideByte = BaseAtmosphereMath.ReduceCarbonDioxideByte(carbonDioxideByte, ScrubberBytePerColdTick);
                    CarbonDioxideByteLane[i] = carbonDioxideByte;
                }

                Output[i] = state;
            }
        }
    }
}
