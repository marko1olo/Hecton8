using System.Runtime.CompilerServices;
using Hecton8.Core;
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
        private const float BleedingHoldSeconds = 6f;
        private const float StunHoldSeconds = 0.75f;
        private const uint RadiationHoldSlowTicks = 40u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeUnit(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafePositive(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value) & (value > Epsilon));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DepthToPressureAtm(float depthMeters)
        {
            float depth = math.max(0f, SanitizeFinite(depthMeters, 0f));
            return ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm + depth * 0.1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountFirstEightBits(uint mask)
        {
            uint eight = mask & 0xFFu;
            eight = eight - ((eight >> 1) & 0x55u);
            eight = (eight & 0x33u) + ((eight >> 2) & 0x33u);
            return (int)((eight + (eight >> 4)) & 0x0Fu);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MixStateHash(ulong hash, uint value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveTraumaMaskFromCombatStatus(uint combatStatusMask)
        {
            uint traumaMask = 0u;
            traumaMask |= (combatStatusMask & ShinobuCombatStatusBridgeBits.Bleeding) != 0u ? ShinobuTraumaBits.Laceration : 0u;
            traumaMask |= (combatStatusMask & ShinobuCombatStatusBridgeBits.Poisoned) != 0u ? ShinobuTraumaBits.Poison : 0u;
            traumaMask |= (combatStatusMask & ShinobuCombatStatusBridgeBits.Stunned) != 0u ? ShinobuTraumaBits.Stun : 0u;
            traumaMask |= (combatStatusMask & ShinobuCombatStatusBridgeBits.Irradiated) != 0u ? ShinobuTraumaBits.Radiation : 0u;
            traumaMask |= (combatStatusMask & ShinobuCombatStatusBridgeBits.Hypoxia) != 0u ? ShinobuTraumaBits.Suffocation : 0u;
            return traumaMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BuildStatusEffectMask(uint flags, uint traumaMask, uint combatStatusMask)
        {
            ulong mask = 0UL;
            uint mergedTraumaMask = traumaMask | ResolveTraumaMaskFromCombatStatus(combatStatusMask);
            mask |= (flags & ShinobuPhysiologyFlags.Bends) != 0u ? ShinobuStatusEffectBits.Bends : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.Narcosis) != 0u ? ShinobuStatusEffectBits.Narcosis : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.Hypothermia) != 0u ? ShinobuStatusEffectBits.Hypothermia : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.OxygenCritical) != 0u ? ShinobuStatusEffectBits.OxygenCritical : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.FatalOxygen) != 0u ? ShinobuStatusEffectBits.FatalOxygen : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.InvalidMath) != 0u ? ShinobuStatusEffectBits.InvalidMath : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.HyperbaricOverride) != 0u ? ShinobuStatusEffectBits.HyperbaricOverride : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.FatalBends) != 0u ? ShinobuStatusEffectBits.FatalBends : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.Hypoxia) != 0u ? ShinobuStatusEffectBits.Hypoxia : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.Hyperoxia) != 0u ? ShinobuStatusEffectBits.Hyperoxia : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.CarbonDioxideToxicity) != 0u ? ShinobuStatusEffectBits.CarbonDioxideToxicity : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.CnsOxygenToxicity) != 0u ? ShinobuStatusEffectBits.CnsOxygenToxicity : 0UL;
            mask |= (flags & ShinobuPhysiologyFlags.FatalGasToxicity) != 0u ? ShinobuStatusEffectBits.FatalGasToxicity : 0UL;
            mask |= (mergedTraumaMask & ShinobuTraumaBits.Laceration) != 0u ? ShinobuStatusEffectBits.Bleeding : 0UL;
            mask |= (mergedTraumaMask & ShinobuTraumaBits.Poison) != 0u ? ShinobuStatusEffectBits.Poison : 0UL;
            mask |= (mergedTraumaMask & ShinobuTraumaBits.Stun) != 0u ? ShinobuStatusEffectBits.Stun : 0UL;
            mask |= (mergedTraumaMask & ShinobuTraumaBits.Radiation) != 0u ? ShinobuStatusEffectBits.Radiation : 0UL;
            mask |= (mergedTraumaMask & ShinobuTraumaBits.Suffocation) != 0u ? ShinobuStatusEffectBits.Hypoxia : 0UL;
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ClearExpiredTransientTraumaMask(
            uint traumaMask,
            uint refreshMask,
            StatusEffectStateDTO previous,
            uint lastTraumaRefreshFrame,
            uint frame)
        {
            ulong previousStatusMask = previous.StatusEffectMask;
            uint mask = traumaMask;
            uint radiationRefreshFrame = lastTraumaRefreshFrame != 0u
                ? lastTraumaRefreshFrame
                : previous.LastTransitionFrame;
            bool bleedingExpired =
                (refreshMask & ShinobuTraumaBits.Laceration) == 0u &&
                (previousStatusMask & ShinobuStatusEffectBits.Bleeding) != 0UL &&
                SanitizeFinite(previous.BleedingSeconds, 0f) >= BleedingHoldSeconds;
            bool stunExpired =
                (refreshMask & ShinobuTraumaBits.Stun) == 0u &&
                (previousStatusMask & ShinobuStatusEffectBits.Stun) != 0UL &&
                SanitizeFinite(previous.StunSeconds, 0f) >= StunHoldSeconds;
            bool radiationExpired =
                (refreshMask & ShinobuTraumaBits.Radiation) == 0u &&
                (previousStatusMask & ShinobuStatusEffectBits.Radiation) != 0UL &&
                HasElapsedSlowTicks(radiationRefreshFrame, frame, RadiationHoldSlowTicks);

            mask &= math.select(0xFFFFFFFFu, ~ShinobuTraumaBits.Laceration, bleedingExpired);
            mask &= math.select(0xFFFFFFFFu, ~ShinobuTraumaBits.Stun, stunExpired);
            mask &= math.select(0xFFFFFFFFu, ~ShinobuTraumaBits.Radiation, radiationExpired);
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasElapsedSlowTicks(uint startFrame, uint currentFrame, uint threshold)
        {
            if (startFrame == 0u)
                return false;

            uint elapsed = currentFrame >= startFrame
                ? currentFrame - startFrame
                : uint.MaxValue - startFrame + currentFrame + 1u;
            return elapsed >= threshold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UpdateStatusSeconds(float previousSeconds, float deltaSeconds, ulong mask, ulong bit)
        {
            float previous = math.max(0f, SanitizeFinite(previousSeconds, 0f));
            float next = math.min(previous + math.max(0f, deltaSeconds), 65535f);
            return math.select(0f, next, (mask & bit) != 0UL);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StatusEffectStateDTO BuildStatusEffectState(
            StatusEffectStateDTO previous,
            ulong statusMask,
            uint frame,
            float deltaSeconds,
            uint traumaMask,
            float toxemia01,
            float narcosis01,
            float fatigueMultiplier,
            float bloodOxygen01)
        {
            uint faultMask = 0u;
            faultMask |= math.select(1u, 0u, math.isfinite(previous.BleedingSeconds));
            faultMask |= math.select(2u, 0u, math.isfinite(previous.PoisonSeconds));
            faultMask |= math.select(4u, 0u, math.isfinite(previous.StunSeconds));
            faultMask |= math.select(8u, 0u, math.isfinite(previous.RadiationDose01));

            StatusEffectStateDTO next = default;
            next.StatusEffectMask = statusMask;
            next.BleedingSeconds = UpdateStatusSeconds(previous.BleedingSeconds, deltaSeconds, statusMask, ShinobuStatusEffectBits.Bleeding);
            next.PoisonSeconds = UpdateStatusSeconds(previous.PoisonSeconds, deltaSeconds, statusMask, ShinobuStatusEffectBits.Poison);
            next.StunSeconds = UpdateStatusSeconds(previous.StunSeconds, deltaSeconds, statusMask, ShinobuStatusEffectBits.Stun);
            next.BleedingSeverity01 = math.select(0f, math.saturate(0.25f + CountFirstEightBits(traumaMask & ShinobuTraumaBits.Laceration) * 0.25f), (statusMask & ShinobuStatusEffectBits.Bleeding) != 0UL);
            next.PoisonSeverity01 = math.select(0f, math.saturate(SanitizeUnit(toxemia01)), (statusMask & ShinobuStatusEffectBits.Poison) != 0UL);
            next.SuffocationSeverity01 = math.select(0f, math.saturate(1f - SanitizeUnit(bloodOxygen01)), (statusMask & ShinobuStatusEffectBits.Hypoxia) != 0UL);
            next.NarcosisSeverity01 = math.select(0f, SanitizeUnit(narcosis01), (statusMask & ShinobuStatusEffectBits.Narcosis) != 0UL);
            next.Fatigue01 = math.saturate((SanitizeFinite(fatigueMultiplier, 1f) - 1f) * 0.5f);
            next.OxygenDebt01 = math.saturate(1f - SanitizeUnit(bloodOxygen01));
            float radiationDose = math.max(0f, SanitizeFinite(previous.RadiationDose01, 0f));
            float radiationActive = math.select(0f, 1f, (statusMask & ShinobuStatusEffectBits.Radiation) != 0UL);
            next.RadiationDose01 = math.saturate(math.select(math.max(0f, radiationDose - deltaSeconds * 0.05f), math.max(radiationDose, radiationActive), radiationActive > 0f));
            next.LastTransitionFrame = previous.StatusEffectMask == statusMask ? previous.LastTransitionFrame : frame;
            next.SanitizedFaultMask = previous.SanitizedFaultMask | faultMask;

            ulong hash = 1469598103934665603UL;
            hash = MixStateHash(hash, (uint)statusMask);
            hash = MixStateHash(hash, (uint)(statusMask >> 32));
            hash = MixStateHash(hash, math.asuint(next.BleedingSeconds));
            hash = MixStateHash(hash, math.asuint(next.PoisonSeconds));
            hash = MixStateHash(hash, math.asuint(next.StunSeconds));
            hash = MixStateHash(hash, math.asuint(next.RadiationDose01));
            hash = MixStateHash(hash, frame);
            next.StateHash = hash;
            return next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveCompartmentCount(float globalQualityWeight)
        {
            return ShinobuPhysiologyConstants.TissueCompartmentCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 ApproxExpNegPade33Reduced(float4 value)
        {
            return MathLodApproximation.ApproxExpNegPade33Reduced(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpNegPade33Reduced(float value)
        {
            return MathLodApproximation.ApproxExpNegPade33Reduced(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpPositivePade33Reduced(float value)
        {
            return MathLodApproximation.ApproxExpPositivePade33Reduced(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float OneMinusApproxExpNegPade33Reduced(float value)
        {
            return MathLodApproximation.OneMinusApproxExpNegPade33Reduced(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveEmergencyHalfTimeSeconds(int index)
        {
            switch (index)
            {
                case 0: return 5f * 60f;
                case 1: return 38.3f * 60f;
                default: return 187f * 60f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveEmergencyBuhlmannA(int index)
        {
            switch (index)
            {
                case 0: return 1.2599f;
                case 1: return 0.5933f;
                default: return 0.3497f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveEmergencyBuhlmannB(int index)
        {
            switch (index)
            {
                case 0: return 0.5050f;
                case 1: return 0.8434f;
                default: return 0.9319f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveEmergencyMValueRatio(int index)
        {
            return math.max(1.08f, 1.58f - index * 0.028f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveBuhlmannAllowedAmbientPressure(float tensionAtm, float a, float b)
        {
            float safeTension = math.max(0f, SanitizeFinite(tensionAtm, ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm));
            float safeA = math.max(0f, SanitizeFinite(a, 0.5f));
            float safeB = math.clamp(SanitizeFinite(b, 0.8f), 0.1f, 2f);
            return math.max(0.1f, (safeTension - safeA) * safeB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveHypoxiaTunnel01(float oxygenPartialPressureAtm)
        {
            return ResolveHypoxiaTunnel01(oxygenPartialPressureAtm, BuildDefaultGasTuning());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveHypoxiaTunnel01(float oxygenPartialPressureAtm, GasPhysiologyTuningDTO gasTuning)
        {
            float ppO2 = math.max(0f, SanitizeFinite(oxygenPartialPressureAtm, ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm));
            GasPhysiologyTuningDTO tuning = SanitizeGasTuning(gasTuning);
            return math.saturate((tuning.HypoxiaPartialPressureAtm - ppO2) *
                                 math.rcp(math.max(0.0001f, tuning.HypoxiaPartialPressureAtm - tuning.AnoxiaPartialPressureAtm)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOxygenAvailability01(float oxygenPartialPressureAtm)
        {
            return ResolveOxygenAvailability01(oxygenPartialPressureAtm, BuildDefaultGasTuning());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOxygenAvailability01(float oxygenPartialPressureAtm, GasPhysiologyTuningDTO gasTuning)
        {
            float ppO2 = math.max(0f, SanitizeFinite(oxygenPartialPressureAtm, ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm));
            GasPhysiologyTuningDTO tuning = SanitizeGasTuning(gasTuning);
            return math.saturate((ppO2 - tuning.AnoxiaPartialPressureAtm) *
                                 math.rcp(math.max(0.0001f, ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm - tuning.AnoxiaPartialPressureAtm)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCarbonDioxideToxicity01(float carbonDioxidePartialPressureAtm)
        {
            return ResolveCarbonDioxideToxicity01(carbonDioxidePartialPressureAtm, BuildDefaultGasTuning());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCarbonDioxideToxicity01(float carbonDioxidePartialPressureAtm, GasPhysiologyTuningDTO gasTuning)
        {
            float ppCO2 = math.max(0f, SanitizeFinite(carbonDioxidePartialPressureAtm, ShinobuPhysiologyConstants.CarbonDioxideFraction));
            GasPhysiologyTuningDTO tuning = SanitizeGasTuning(gasTuning);
            return math.saturate((ppCO2 - tuning.CarbonDioxideToxicityStartAtm) *
                                 math.rcp(math.max(0.0001f, tuning.CarbonDioxideToxicityFullAtm - tuning.CarbonDioxideToxicityStartAtm)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveNitrogenNarcosis01(float nitrogenPartialPressureAtm, PhysiologyTuningDTO tuning)
        {
            float start = math.max(0.5f, SanitizeFinite(tuning.NarcosisStartAtm, 4f));
            float full = math.max(start + 0.25f, SanitizeFinite(tuning.NarcosisFullAtm, 7f));
            float ppN2 = math.max(0f, SanitizeFinite(nitrogenPartialPressureAtm, ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm));
            return math.saturate((ppN2 - start) * math.rcp(math.max(0.0001f, full - start)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveNitrogenNarcosis01(float nitrogenPartialPressureAtm, GasPhysiologyTuningDTO gasTuning)
        {
            GasPhysiologyTuningDTO tuning = SanitizeGasTuning(gasTuning);
            float ppN2 = math.max(0f, SanitizeFinite(nitrogenPartialPressureAtm, ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm));
            return math.saturate((ppN2 - tuning.NarcosisStartAtm) * math.rcp(math.max(0.0001f, tuning.NarcosisFullAtm - tuning.NarcosisStartAtm)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BreathingGasFractionsDTO SanitizeBreathingGas(BreathingGasFractionsDTO gas)
        {
            float oxygen = math.clamp(SanitizeFinite(gas.OxygenFraction, ShinobuPhysiologyConstants.OxygenFraction), 0f, 1f);
            float nitrogen = math.clamp(SanitizeFinite(gas.NitrogenFraction, ShinobuPhysiologyConstants.NitrogenFraction), 0f, 1f);
            float carbonDioxide = math.clamp(SanitizeFinite(gas.CarbonDioxideFraction, ShinobuPhysiologyConstants.CarbonDioxideFraction), 0f, 0.2f);
            float total = oxygen + nitrogen + carbonDioxide;
            if (total > 1f)
            {
                float inverseTotal = math.rcp(total);
                oxygen *= inverseTotal;
                nitrogen *= inverseTotal;
                carbonDioxide *= inverseTotal;
                total = 1f;
            }

            gas.OxygenFraction = oxygen;
            gas.NitrogenFraction = nitrogen;
            gas.CarbonDioxideFraction = carbonDioxide;
            gas.InertReserveFraction = math.max(0f, 1f - total);
            gas.GasHash = gas.GasHash != 0u ? gas.GasHash : 0x41495231u; // AIR1
            return gas;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GasPhysiologyTuningDTO SanitizeGasTuning(GasPhysiologyTuningDTO tuning)
        {
            if (tuning.Version == 0u)
                tuning = BuildDefaultGasTuning();

            tuning.AnoxiaPartialPressureAtm = math.clamp(SanitizeFinite(tuning.AnoxiaPartialPressureAtm, ShinobuPhysiologyConstants.AnoxiaPartialPressureAtm), 0.02f, 0.14f);
            tuning.HypoxiaPartialPressureAtm = math.clamp(SanitizeFinite(tuning.HypoxiaPartialPressureAtm, ShinobuPhysiologyConstants.HypoxiaPartialPressureAtm), tuning.AnoxiaPartialPressureAtm + 0.01f, 0.35f);
            tuning.CnsToxicityStartAtm = math.clamp(SanitizeFinite(tuning.CnsToxicityStartAtm, ShinobuPhysiologyConstants.CnsToxicityStartAtm), 0.6f, 2.4f);
            tuning.CnsToxicityExtremeAtm = math.max(tuning.CnsToxicityStartAtm + 0.05f, SanitizeFinite(tuning.CnsToxicityExtremeAtm, ShinobuPhysiologyConstants.CnsToxicityExtremeAtm));
            tuning.CnsAccumulationRate = math.clamp(SanitizeFinite(tuning.CnsAccumulationRate, 0.035f), 0.001f, 0.25f);
            tuning.CnsExtremeRate = math.clamp(SanitizeFinite(tuning.CnsExtremeRate, 0.08f), 0.001f, 0.6f);
            tuning.CnsRecoveryPerSecond = math.clamp(SanitizeFinite(tuning.CnsRecoveryPerSecond, 0.012f), 0.0001f, 0.12f);
            tuning.CnsRecoveryPressureScale = math.clamp(SanitizeFinite(tuning.CnsRecoveryPressureScale, 0.004f), 0f, 0.08f);
            tuning.NarcosisStartAtm = math.clamp(SanitizeFinite(tuning.NarcosisStartAtm, 4f), 1f, 12f);
            tuning.NarcosisFullAtm = math.max(tuning.NarcosisStartAtm + 0.25f, SanitizeFinite(tuning.NarcosisFullAtm, 7f));
            tuning.CarbonDioxideToxicityStartAtm = math.clamp(SanitizeFinite(tuning.CarbonDioxideToxicityStartAtm, ShinobuPhysiologyConstants.CarbonDioxideToxicityStartAtm), 0.005f, 0.2f);
            tuning.CarbonDioxideToxicityFullAtm = math.max(tuning.CarbonDioxideToxicityStartAtm + 0.005f, SanitizeFinite(tuning.CarbonDioxideToxicityFullAtm, ShinobuPhysiologyConstants.CarbonDioxideToxicityFullAtm));
            tuning.ToxicDamageStart01 = math.clamp(SanitizeFinite(tuning.ToxicDamageStart01, 0.85f), 0.2f, 1f);
            tuning.ToxicDamagePerSecond = math.clamp(SanitizeFinite(tuning.ToxicDamagePerSecond, 6f), 0.1f, 40f);
            tuning.StaminaStressScale = math.clamp(SanitizeFinite(tuning.StaminaStressScale, 1f), 0f, 4f);
            tuning.Version = 1u;
            return tuning;
        }

        public static GasPhysiologyTuningDTO BuildDefaultGasTuning()
        {
            GasPhysiologyTuningDTO tuning = default;
            tuning.HypoxiaPartialPressureAtm = ShinobuPhysiologyConstants.HypoxiaPartialPressureAtm;
            tuning.AnoxiaPartialPressureAtm = ShinobuPhysiologyConstants.AnoxiaPartialPressureAtm;
            tuning.CnsToxicityStartAtm = ShinobuPhysiologyConstants.CnsToxicityStartAtm;
            tuning.CnsToxicityExtremeAtm = ShinobuPhysiologyConstants.CnsToxicityExtremeAtm;
            tuning.CnsAccumulationRate = 0.035f;
            tuning.CnsExtremeRate = 0.08f;
            tuning.CnsRecoveryPerSecond = 0.012f;
            tuning.CnsRecoveryPressureScale = 0.004f;
            tuning.NarcosisStartAtm = 4f;
            tuning.NarcosisFullAtm = 7f;
            tuning.CarbonDioxideToxicityStartAtm = ShinobuPhysiologyConstants.CarbonDioxideToxicityStartAtm;
            tuning.CarbonDioxideToxicityFullAtm = ShinobuPhysiologyConstants.CarbonDioxideToxicityFullAtm;
            tuning.ToxicDamageStart01 = 0.85f;
            tuning.ToxicDamagePerSecond = 6f;
            tuning.StaminaStressScale = 1f;
            tuning.Version = 1u;
            return tuning;
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
                ShinobuPhysiologyJobMath.ResolveEmergencyHalfTimeSeconds(tissueIndex));
            float buhlmannB = math.clamp(
                ShinobuPhysiologyJobMath.SanitizeFinite(coefficient.BuhlmannB, ShinobuPhysiologyJobMath.ResolveEmergencyBuhlmannB(tissueIndex)),
                0.1f,
                2f);
            float mValue = math.max(1.01f, ShinobuPhysiologyJobMath.SanitizeFinite(
                coefficient.MValueRatio,
                ShinobuPhysiologyJobMath.ResolveEmergencyMValueRatio(tissueIndex)));

            TissueCompartments[index] = new TissueCompartmentDTO
            {
                NitrogenTension = ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm,
                Halftime = halfTime,
                MValue = buhlmannB > 0f ? buhlmannB : mValue,
                Flags = 0u
            };
        }
    }

    /// <summary>
    /// Deterministic crash-dive profile generator: descent, bottom dwell, emergency ascent.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockDiveProfileJob : IJobParallelFor
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
    /// Deterministic breathing-gas source for isolated tests and bootstraps. Depth blends air toward heliox continuously.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockBreathingGasJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [NoAlias] public NativeArray<BreathingGasFractionsDTO> BreathingGas;
        public BreathingGasFractionsDTO OverrideGas;
        public int Count;
        public byte UseOverrideGas;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)BreathingGas.Length)
                return;

            if (UseOverrideGas != 0)
            {
                BreathingGas[index] = ShinobuPhysiologyJobMath.SanitizeBreathingGas(OverrideGas);
                return;
            }

            MockEnvironmentVitalsSignal env = Environment.IsCreated && (uint)index < (uint)Environment.Length
                ? Environment[index]
                : default;
            float depthMeters = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(env.DepthMeters, 0f));
            float heliox01 = math.saturate((depthMeters - ShinobuPhysiologyConstants.HelioxTransitionStartMeters) *
                                           math.rcp(math.max(0.0001f, ShinobuPhysiologyConstants.HelioxTransitionSpanMeters)));

            BreathingGasFractionsDTO gas = default;
            gas.OxygenFraction = math.lerp(ShinobuPhysiologyConstants.OxygenFraction, ShinobuPhysiologyConstants.HelioxOxygenFraction, heliox01);
            gas.NitrogenFraction = math.lerp(ShinobuPhysiologyConstants.NitrogenFraction, ShinobuPhysiologyConstants.HelioxNitrogenFraction, heliox01);
            gas.CarbonDioxideFraction = ShinobuPhysiologyConstants.CarbonDioxideFraction;
            gas.InertReserveFraction = math.max(0f, 1f - gas.OxygenFraction - gas.NitrogenFraction - gas.CarbonDioxideFraction);
            bool helioxIdentity = heliox01 >= 0.5f;
            gas.GasHash = helioxIdentity ? 0x484C5831u : 0x41495231u; // HLX1 / AIR1
            gas.Flags = ShinobuPhysiologyFlags.EmergencyMockCoefficients |
                        (helioxIdentity ? ShinobuPhysiologyFlags.BreathingGasHeliox : 0u);
            BreathingGas[index] = ShinobuPhysiologyJobMath.SanitizeBreathingGas(gas);
        }
    }

    /// <summary>
    /// Dalton's law kernel: partial pressure = gas fraction * ambient absolute pressure.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CalculatePartialPressuresJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [ReadOnly, NoAlias] public NativeArray<BreathingGasFractionsDTO> BreathingGas;
        [NoAlias] public NativeArray<GasPhysiologyStateDTO> GasStates;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)GasStates.Length)
                return;

            MockEnvironmentVitalsSignal env = Environment.IsCreated && (uint)index < (uint)Environment.Length
                ? Environment[index]
                : default;
            float ambient = env.AmbientPressureAtm > 0f
                ? ShinobuPhysiologyJobMath.SanitizeFinite(env.AmbientPressureAtm, ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters))
                : ShinobuPhysiologyJobMath.DepthToPressureAtm(env.DepthMeters);
            ambient = math.max(0f, ambient);

            BreathingGasFractionsDTO gas = BreathingGas.IsCreated && (uint)index < (uint)BreathingGas.Length
                ? ShinobuPhysiologyJobMath.SanitizeBreathingGas(BreathingGas[index])
                : default;
            if (!BreathingGas.IsCreated || (uint)index >= (uint)BreathingGas.Length)
            {
                gas.OxygenFraction = ShinobuPhysiologyConstants.OxygenFraction;
                gas.NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction;
                gas.CarbonDioxideFraction = ShinobuPhysiologyConstants.CarbonDioxideFraction;
                gas.GasHash = 0x41495231u;
            }

            GasPhysiologyStateDTO state = GasStates[index];
            state.OxygenPartialPressure = ambient * gas.OxygenFraction;
            state.NitrogenPartialPressure = ambient * gas.NitrogenFraction;
            state.CarbonDioxidePartialPressure = ambient * gas.CarbonDioxideFraction;
            state.StaminaDrainRate = math.max(1f, ShinobuPhysiologyJobMath.SanitizeFinite(state.StaminaDrainRate, 1f));
            state.Flags = gas.Flags;
            GasStates[index] = state;
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
                    int traumaType = math.clamp(damage.TraumaType, 0, 7);
                    uint appliedTraumaMask = 1u << traumaType;
                    appliedTraumaMask |= ShinobuPhysiologyJobMath.ResolveTraumaMaskFromCombatStatus(damage.CombatStatusMask);
                    vital.ActiveTraumaMask |= appliedTraumaMask;
                    vital.ActiveTraumaRefreshMask |= appliedTraumaMask;
                    vital.LastTraumaRefreshFrame = damage.Frame != 0u ? damage.Frame : vital.LastTraumaRefreshFrame;
                    float severity = ShinobuPhysiologyJobMath.SanitizeUnit(damage.Severity01);
                    if ((appliedTraumaMask & ShinobuTraumaBits.Poison) != 0u)
                        scalar.Toxemia = math.max(scalar.Toxemia, severity);
                    vital.HeartRate = math.max(vital.HeartRate, 70f + severity * 45f);
                    scalar.StatusEffectMask = ShinobuPhysiologyJobMath.BuildStatusEffectMask(scalar.StatusFlags, vital.ActiveTraumaMask, damage.CombatStatusMask);
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
                    scalar.StatusEffectMask = ShinobuPhysiologyJobMath.BuildStatusEffectMask(scalar.StatusFlags, vital.ActiveTraumaMask, 0u);
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

                    if (scalar.Toxemia > 0.0001f)
                        vital.ActiveTraumaMask |= ShinobuTraumaBits.Poison;
                    else
                        vital.ActiveTraumaMask &= ~ShinobuTraumaBits.Poison;

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
            scalar.StatusEffectMask = ShinobuPhysiologyJobMath.BuildStatusEffectMask(scalar.StatusFlags, vital.ActiveTraumaMask, 0u);

            Vitals[index] = vital;
            Scalars[index] = scalar;
        }
    }

    /// <summary>
    /// Pragmatic three-lane blood-gas tension integrator.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct IntegrateBloodGasTensionsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PhysiologyDTO> Vitals;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Execute(index) owns exactly one entity tissue slice:
        // [index * TissueCompartmentCount, index * TissueCompartmentCount + TissueCompartmentCount).
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: Rejected direct indexed NativeArray writes because CS1612 copies
        // block in-place mutation of the 3-row slice; unsafe refs keep the Burst loop contiguous.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: No worker writes outside its derived slice, and the bounds guard
        // proves the entire slice is inside the Vault array before unsafe ref mutation begins.
        // SAFETY_JUSTIFICATION_PARAGRAPH_4: ShinobuPhysiologyRuntime locks this Vault lane before scheduling
        // the job and releases it only after the dispatcher-owned completion fence has run.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<TissueCompartmentDTO> TissueCompartments;
        [NoAlias] public NativeArray<DecompressionStateDTO> DecompressionStates;
        [ReadOnly, NoAlias] public NativeArray<HaldaneTissueCoefficientDTO> TissueCoefficients;
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [ReadOnly, NoAlias] public NativeArray<GasPhysiologyStateDTO> GasStates;
        [NoAlias] public NativeArray<PhysiologyScalarsDTO> Scalars;
        [WriteOnly, NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<PhysiologyStateSignal>.ParallelWriter PhysiologyWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> PhysiologyWriterBudget;
        [WriteOnly, NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<CombatDamageSignal>.ParallelWriter DamageWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> DamageWriterBudget;
        public PhysiologyTuningDTO Tuning;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public uint Frame;
        public uint PlayerTargetHash;
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
            GasPhysiologyStateDTO gas = GasStates.IsCreated && (uint)index < (uint)GasStates.Length
                ? GasStates[index]
                : default;
            float ambientNitrogenPressure = gas.NitrogenPartialPressure > 0f
                ? ShinobuPhysiologyJobMath.SanitizeFinite(gas.NitrogenPartialPressure, ambient * ShinobuPhysiologyConstants.NitrogenFraction)
                : ambient * ShinobuPhysiologyConstants.NitrogenFraction;
            ambientNitrogenPressure = math.max(0f, ambientNitrogenPressure);
            float tissueEquilibriumPressure = ambientNitrogenPressure;
            float nitrogenScale = math.max(0.001f, tuning.NitrogenUptakeRate * tuning.HaldaneTimeScale);
            int activeCompartments = ShinobuPhysiologyConstants.TissueCompartmentCount;
            float maxTissue = 0f;
            float risk = 0f;
            float minGradient = 128f;
            uint overMask = 0u;
            uint invalidMath = 0u;

            DecompressionStateDTO state = DecompressionStates[index];
            uint previousWarningStatus = Scalars[index].StatusFlags &
                                         ShinobuPhysiologyConstants.DecompressionWarningStatusMask;
            uint previousWarningFrame = state.LastWarningFrame;
            float previousAmbient = state.CurrentAmbientPressure > 0f
                ? ShinobuPhysiologyJobMath.SanitizeFinite(state.CurrentAmbientPressure, ambient)
                : ambient;
            float ascentRate = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(env.AscentRateMetersPerSecond, 0f));
            state.CurrentAmbientPressure = ambient;

            float gasNitrogenFraction = math.saturate(ambientNitrogenPressure * math.rcp(math.max(0.0001f, ambient)));
            float inspiredStart = math.max(0f, previousAmbient * gasNitrogenFraction);
            float inspiredEnd = ambientNitrogenPressure;
            float schreinerRate = (inspiredEnd - inspiredStart) * math.rcp(dt);

            // The full 3-row entity slice is bounds-checked above; the runtime locks this Vault lane before scheduling.
            void* tissueBasePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TissueCompartments);
            int tissueStride = UnsafeUtility.SizeOf<TissueCompartmentDTO>();
            for (int tissueIndex = 0; tissueIndex < ShinobuPhysiologyConstants.TissueCompartmentCount; tissueIndex++)
            {
                ref TissueCompartmentDTO tissue = ref UnsafeUtility.AsRef<TissueCompartmentDTO>((byte*)tissueBasePtr + ((compartmentBase + tissueIndex) * tissueStride));
                HaldaneTissueCoefficientDTO coefficient = ResolveCoefficient(tissueIndex);
                float halfTime = ResolveHalfTime(tissue, coefficient, tissueIndex);
                float k = ResolveK(coefficient, halfTime);
                if (!math.isfinite(tissue.NitrogenTension) || !math.isfinite(halfTime))
                    invalidMath = ShinobuPhysiologyFlags.InvalidMath;

                float oldTension = ShinobuPhysiologyJobMath.SanitizeFinite(tissue.NitrogenTension, tissueEquilibriumPressure);
                float effectiveK = math.max(k * nitrogenScale, 0.0001f);
                float inverseK = math.rcp(effectiveK);
                float decay = ShinobuPhysiologyJobMath.ApproxExpNegPade33Reduced(effectiveK * dt);
                float next = inspiredStart + schreinerRate * (dt - inverseK) - (inspiredStart - oldTension - schreinerRate * inverseK) * decay;
                if (!math.isfinite(next))
                {
                    invalidMath = ShinobuPhysiologyFlags.InvalidMath;
                    next = tissueEquilibriumPressure;
                }

                ApplyLaneResult(tissueIndex, next, ambient, tuning.BendsRiskScale, coefficient, ref tissue, ref state, true, ref maxTissue, ref risk, ref minGradient, ref overMask);
            }

            state.GradientAdvantage = minGradient;
            state.BubbleFlags = overMask;
            float correctedRisk = math.saturate(risk * ShinobuPhysiologyConstants.ThreeTissueRiskCorrection);
            state.Supersaturation01 = correctedRisk;
            state.AscentRateMetersPerSecond = ascentRate;
            state.Frame = Frame;
            PhysiologyDTO vital = Vitals[index];
            PhysiologyScalarsDTO scalar = Scalars[index];
            float nitrogenNarcosis = GasStates.IsCreated && (uint)index < (uint)GasStates.Length && gas.NarcosisLevel01 > 0f
                ? ShinobuPhysiologyJobMath.SanitizeUnit(gas.NarcosisLevel01)
                : ShinobuPhysiologyJobMath.ResolveNitrogenNarcosis01(ambientNitrogenPressure, tuning);
            float supersaturation = correctedRisk;
            scalar.NarcosisSeverity = nitrogenNarcosis;
            scalar.BendsRisk = supersaturation;
            scalar.TissueOverMValueMask = overMask;
            scalar.StatusFlags &= ~(ShinobuPhysiologyFlags.Bends | ShinobuPhysiologyFlags.Narcosis | ShinobuPhysiologyFlags.FatalBends | ShinobuPhysiologyFlags.HyperbaricOverride | ShinobuPhysiologyFlags.InvalidMath);
            scalar.StatusFlags |= overMask != 0u ? ShinobuPhysiologyFlags.Bends : 0u;
            scalar.StatusFlags |= nitrogenNarcosis > 0f ? ShinobuPhysiologyFlags.Narcosis : 0u;
            scalar.StatusFlags |= supersaturation >= 0.98f ? ShinobuPhysiologyFlags.FatalBends : 0u;
            scalar.StatusFlags |= (env.Flags & MockPressureSignal.HyperbaricTreatmentFlag) != 0u ? ShinobuPhysiologyFlags.HyperbaricOverride : 0u;
            scalar.StatusFlags |= invalidMath;
            scalar.StatusEffectMask = ShinobuPhysiologyJobMath.BuildStatusEffectMask(scalar.StatusFlags, vital.ActiveTraumaMask, 0u);
            vital.TissueNitrogen = maxTissue;

            uint currentWarningStatus = scalar.StatusFlags &
                                        ShinobuPhysiologyConstants.DecompressionWarningStatusMask;
            uint signalFrame = Frame != 0u ? Frame : env.Frame;
            uint warningFrameDelta = signalFrame >= previousWarningFrame
                ? signalFrame - previousWarningFrame
                : ShinobuPhysiologyConstants.DecompressionWarningCadenceFrames;
            bool warningStateChanged = currentWarningStatus != previousWarningStatus;
            bool warningCadenceElapsed = previousWarningFrame == 0u ||
                                         warningFrameDelta >= ShinobuPhysiologyConstants.DecompressionWarningCadenceFrames;
            bool shouldEmitWarningSignal = warningStateChanged ||
                                           (currentWarningStatus != 0u && warningCadenceElapsed);

            Vitals[index] = vital;

            if (EmitPhysiologySignal != 0 && index == 0 && shouldEmitWarningSignal)
            {
                state.LastWarningFrame = signalFrame;
                state.WarningPulseCount++;
                float decompressionStress01 = math.saturate(math.max(
                    supersaturation,
                    (currentWarningStatus & (ShinobuPhysiologyFlags.FatalBends | ShinobuPhysiologyFlags.InvalidMath)) != 0u ? 1f : 0f));
                PhysiologyStateSignal signal = default;
                signal.PlayerStress01 = decompressionStress01;
                signal.O2DrainMultiplier = math.max(1f, ambient);
                signal.Recovery01 = 1f - decompressionStress01;
                signal.Frame = signalFrame;
                signal.Cause = PhysiologyStateSignal.CauseDecompression;
                signal.Flags = (byte)math.select(0, 1, decompressionStress01 > 0f);
                signal.Supersaturation01 = supersaturation;
                signal.Narcosis01 = nitrogenNarcosis;
                signal.AmbientPressureAtm = ambient;
                signal.NitrogenLoadAtm = maxTissue;
                signal.AscentRateMetersPerSecond = ascentRate;
                signal.TissueOverMValueMask = overMask;
                signal.SourceHash = ShinobuPhysiologyConstants.SourceHash;
                signal.EntityIndex = index;
                signal.ActiveCompartments = (byte)activeCompartments;
                signal.FatalSeverity = (byte)math.round(decompressionStress01 * 255f);
                signal.StatusFlags = scalar.StatusFlags;
                SignalBus<PhysiologyStateSignal>.TryEnqueueBounded(PhysiologyWriter, PhysiologyWriterBudget, signal);
            }

            uint targetHash = PlayerTargetHash != 0u ? PlayerTargetHash : ShinobuPhysiologyConstants.PlayerTargetHash;
            if (EmitPhysiologySignal != 0 && index == 0 && supersaturation > 0f && targetHash != 0u)
            {
                CombatDamageSignal damage = default;
                damage.ImpactAup = double3.zero;
                damage.Direction = new float3(0f, 1f, 0f);
                damage.Magnitude = supersaturation * dt * math.max(0.1f, tuning.BendsRiskScale) * 12f;
                damage.DamageType = ShinobuPhysiologyConstants.CombatDamageTypeBarotrauma;
                damage.TargetHash = targetHash;
                damage.SourceHash = ShinobuPhysiologyConstants.SourceHash;
                damage.Frame = signalFrame;
                damage.SourceId = unchecked((ushort)ShinobuPhysiologyConstants.SourceHash);
                damage.TargetId = 0;
                damage.Channel = 0;
                damage.Flags = CombatDamageSignal.DirectRuntimeFlag;
                SignalBus<CombatDamageSignal>.TryEnqueueBounded(DamageWriter, DamageWriterBudget, damage);
            }

            Scalars[index] = scalar;
            DecompressionStates[index] = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HaldaneTissueCoefficientDTO ResolveCoefficient(int tissueIndex)
        {
            return TissueCoefficients.IsCreated && (uint)tissueIndex < (uint)TissueCoefficients.Length
                ? TissueCoefficients[tissueIndex]
                : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveHalfTime(TissueCompartmentDTO tissue, HaldaneTissueCoefficientDTO coefficient, int tissueIndex)
        {
            float fallback = ShinobuPhysiologyJobMath.SafePositive(tissue.Halftime, ShinobuPhysiologyJobMath.ResolveEmergencyHalfTimeSeconds(tissueIndex));
            return ShinobuPhysiologyJobMath.SafePositive(coefficient.HalfTimeSeconds, fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveK(HaldaneTissueCoefficientDTO coefficient, float halfTime)
        {
            float fallbackK = 0.69314718056f * math.rcp(math.max(1f, halfTime));
            return ShinobuPhysiologyJobMath.SafePositive(coefficient.K, fallbackK);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveBuhlmannA(HaldaneTissueCoefficientDTO coefficient, int tissueIndex)
        {
            return math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(coefficient.BuhlmannA, ShinobuPhysiologyJobMath.ResolveEmergencyBuhlmannA(tissueIndex)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveBuhlmannB(HaldaneTissueCoefficientDTO coefficient, int tissueIndex)
        {
            return math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(coefficient.BuhlmannB, ShinobuPhysiologyJobMath.ResolveEmergencyBuhlmannB(tissueIndex)), 0.1f, 2f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyLaneResult(
            int tissueIndex,
            float next,
            float ambient,
            float bendsRiskScale,
            HaldaneTissueCoefficientDTO coefficient,
            ref TissueCompartmentDTO tissue,
            ref DecompressionStateDTO state,
            bool evaluated,
            ref float maxTissue,
            ref float risk,
            ref float minGradient,
            ref uint overMask)
        {
            float halfTime = ResolveHalfTime(tissue, coefficient, tissueIndex);
            float a = ResolveBuhlmannA(coefficient, tissueIndex);
            float b = ResolveBuhlmannB(coefficient, tissueIndex);
            tissue.NitrogenTension = next;
            tissue.Halftime = halfTime;
            tissue.MValue = b;
            tissue.Flags = (tissue.Flags & ShinobuPhysiologyFlags.CsvOverride) | (evaluated ? 1u : 0u);

            float allowedPressure = ShinobuPhysiologyJobMath.ResolveBuhlmannAllowedAmbientPressure(next, a, b);
            state.SetTissueTensionN2(tissueIndex, next);
            state.SetAllowedAmbientPressure(tissueIndex, allowedPressure);
            float gradient = ambient - allowedPressure;
            minGradient = math.min(minGradient, gradient);
            if (gradient < 0f)
            {
                overMask |= 1u << tissueIndex;
                float compartmentRisk = -gradient * bendsRiskScale * math.rcp(math.max(0.0001f, ambient));
                risk = math.max(risk, compartmentRisk);
            }

            maxTissue = math.max(maxTissue, next);
        }

    }

    /// <summary>
    /// Integrates oxygen CNS toxicity, CO2 toxicity, hypoxia, narcosis, and unmanaged damage signals.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CalculateCnsToxicityJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<GasPhysiologyStateDTO> GasStates;
        [NoAlias] public NativeArray<PhysiologyScalarsDTO> Scalars;
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [WriteOnly, NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<PhysiologyStateSignal>.ParallelWriter PhysiologyWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> PhysiologyWriterBudget;
        public GasPhysiologyTuningDTO GasTuning;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public uint Frame;
        public int Count;
        public byte EmitSignals;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                (uint)index >= (uint)GasStates.Length ||
                (uint)index >= (uint)Scalars.Length)
            {
                return;
            }

            GasPhysiologyTuningDTO gasTuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(GasTuning);
            float dt = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(DeltaSeconds, 0.016f), 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            GasPhysiologyStateDTO gas = GasStates[index];
            PhysiologyScalarsDTO scalar = Scalars[index];
            MockEnvironmentVitalsSignal env = Environment.IsCreated && Environment.Length > 0 ? Environment[0] : default;
            uint signalFrame = Frame != 0u ? Frame : env.Frame;
            uint previousGasStatus = gas.Flags & ShinobuPhysiologyConstants.GasStatusWarningMask;
            uint previousWarningFrame = gas.LastWarningFrame;

            float ppO2 = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(gas.OxygenPartialPressure, ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm));
            float ppN2 = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(gas.NitrogenPartialPressure, ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm));
            float ppCO2 = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(gas.CarbonDioxidePartialPressure, ShinobuPhysiologyConstants.CarbonDioxideFraction));

            float oxygenOverSafe = math.max(0f, ppO2 - gasTuning.CnsToxicityStartAtm);
            float oxygenExtreme = math.max(0f, ppO2 - gasTuning.CnsToxicityExtremeAtm);
            float cnsAccumulation = oxygenOverSafe * gasTuning.CnsAccumulationRate +
                (ShinobuPhysiologyJobMath.ApproxExpPositivePade33Reduced(oxygenExtreme) - 1f) * gasTuning.CnsExtremeRate;
            float cnsRecovery = ppO2 < gasTuning.CnsToxicityStartAtm
                ? (gasTuning.CnsRecoveryPerSecond + (gasTuning.CnsToxicityStartAtm - ppO2) * gasTuning.CnsRecoveryPressureScale)
                : 0f;
            gas.CnsToxicity01 = math.saturate(ShinobuPhysiologyJobMath.SanitizeUnit(gas.CnsToxicity01) + (cnsAccumulation - cnsRecovery) * dt);

            float hypoxia01 = ShinobuPhysiologyJobMath.ResolveHypoxiaTunnel01(ppO2, gasTuning);
            float anoxia01 = math.saturate((gasTuning.AnoxiaPartialPressureAtm - ppO2) *
                                           math.rcp(math.max(0.0001f, gasTuning.AnoxiaPartialPressureAtm)));
            float co2Toxicity01 = ShinobuPhysiologyJobMath.ResolveCarbonDioxideToxicity01(ppCO2, gasTuning);
            float narcosis01 = ShinobuPhysiologyJobMath.ResolveNitrogenNarcosis01(ppN2, gasTuning);
            gas.NarcosisLevel01 = narcosis01;
            gas.StaminaDrainRate = math.max(1f, 1f + (hypoxia01 * 1.5f + co2Toxicity01 * 2f + gas.CnsToxicity01 + narcosis01 * 0.6f) * gasTuning.StaminaStressScale);

            uint status = scalar.StatusFlags &
                          ~(ShinobuPhysiologyFlags.Hypoxia |
                            ShinobuPhysiologyFlags.Hyperoxia |
                            ShinobuPhysiologyFlags.CarbonDioxideToxicity |
                            ShinobuPhysiologyFlags.CnsOxygenToxicity |
                            ShinobuPhysiologyFlags.FatalGasToxicity |
                            ShinobuPhysiologyFlags.Narcosis);
            uint gasFlags = gas.Flags &
                            (ShinobuPhysiologyFlags.EmergencyMockCoefficients |
                             ShinobuPhysiologyFlags.CsvOverride |
                             ShinobuPhysiologyFlags.BreathingGasHeliox);
            gasFlags |= hypoxia01 > 0f ? ShinobuPhysiologyFlags.Hypoxia : 0u;
            gasFlags |= ppO2 > gasTuning.CnsToxicityStartAtm ? ShinobuPhysiologyFlags.Hyperoxia : 0u;
            gasFlags |= gas.CnsToxicity01 > 0f ? ShinobuPhysiologyFlags.CnsOxygenToxicity : 0u;
            gasFlags |= co2Toxicity01 > 0f ? ShinobuPhysiologyFlags.CarbonDioxideToxicity : 0u;
            gasFlags |= narcosis01 > 0f ? ShinobuPhysiologyFlags.Narcosis : 0u;
            gasFlags |= (gas.CnsToxicity01 >= 0.98f || co2Toxicity01 >= 0.98f || anoxia01 >= 0.98f)
                ? ShinobuPhysiologyFlags.FatalGasToxicity
                : 0u;
            gas.Flags = gasFlags;

            float gasStress01 = math.saturate(math.max(math.max(hypoxia01, co2Toxicity01), math.max(gas.CnsToxicity01, narcosis01)));
            scalar.NarcosisSeverity = math.max(ShinobuPhysiologyJobMath.SanitizeUnit(scalar.NarcosisSeverity), narcosis01);
            scalar.Toxemia = math.max(ShinobuPhysiologyJobMath.SanitizeUnit(scalar.Toxemia), math.max(co2Toxicity01, gas.CnsToxicity01));
            scalar.FatigueMultiplier = math.max(ShinobuPhysiologyJobMath.SanitizeFinite(scalar.FatigueMultiplier, 1f), gas.StaminaDrainRate);
            scalar.StatusFlags = status | gasFlags;
            scalar.StatusEffectMask = ShinobuPhysiologyJobMath.BuildStatusEffectMask(scalar.StatusFlags, 0u, 0u);

            uint currentGasStatus = gasFlags & ShinobuPhysiologyConstants.GasStatusWarningMask;
            uint warningFrameDelta = signalFrame >= previousWarningFrame
                ? signalFrame - previousWarningFrame
                : ShinobuPhysiologyConstants.GasStatusWarningCadenceFrames;
            bool warningStateChanged = currentGasStatus != previousGasStatus;
            bool warningCadenceElapsed = previousWarningFrame == 0u ||
                                         warningFrameDelta >= ShinobuPhysiologyConstants.GasStatusWarningCadenceFrames;
            bool shouldEmitGasSignal = warningStateChanged ||
                                       (currentGasStatus != 0u && warningCadenceElapsed);

            if (EmitSignals != 0 && index == 0 && shouldEmitGasSignal)
            {
                gas.LastWarningFrame = signalFrame;
            }

            GasStates[index] = gas;
            Scalars[index] = scalar;

            if (EmitSignals != 0 && index == 0 && shouldEmitGasSignal)
            {
                PhysiologyStateSignal signal = default;
                signal.PlayerStress01 = gasStress01;
                signal.O2DrainMultiplier = gas.StaminaDrainRate;
                signal.Recovery01 = 1f - gasStress01;
                signal.Frame = signalFrame;
                signal.Cause = ShinobuPhysiologyConstants.GasToxicitySignalCause;
                signal.Flags = (byte)math.select(0, 1, gasStress01 > 0f);
                signal.GasCnsSeverity = (byte)math.round(math.saturate(gas.CnsToxicity01) * 255f);
                signal.GasCarbonDioxideSeverity = (byte)math.round(math.saturate(co2Toxicity01) * 255f);
                signal.Supersaturation01 = hypoxia01;
                signal.Narcosis01 = narcosis01;
                signal.AmbientPressureAtm = math.max(0f, env.AmbientPressureAtm);
                signal.NitrogenLoadAtm = ppN2;
                signal.AscentRateMetersPerSecond = math.max(0f, env.AscentRateMetersPerSecond);
                signal.TissueOverMValueMask = scalar.TissueOverMValueMask;
                signal.SourceHash = ShinobuPhysiologyConstants.SourceHash;
                signal.EntityIndex = index;
                signal.ActiveCompartments = (byte)ShinobuPhysiologyJobMath.ResolveActiveCompartmentCount(GlobalQualityWeight);
                signal.FatalSeverity = (byte)math.round(gasStress01 * 255f);
                signal.StatusFlags = scalar.StatusFlags;
                SignalBus<PhysiologyStateSignal>.TryEnqueueBounded(PhysiologyWriter, PhysiologyWriterBudget, signal);
            }

            // Gas toxicity injury is carried by scalar.Toxemia/status flags into StatusEffectStateDTO.
            // Direct toxic CombatDamageSignal is intentionally not emitted from this job.
        }
    }

    /// <summary>
    /// Metabolic oxygen, temperature, toxemia, adrenaline, pulse, export, and black-box writer.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct OxygenConsumptionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PhysiologyDTO> Vitals;
        [NoAlias] public NativeArray<PhysiologyScalarsDTO> Scalars;
        [NoAlias] public NativeArray<StatusEffectStateDTO> StatusEffects;
        [NoAlias] public NativeArray<CardiacPulseStateDTO> PulseStates;
        [ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;
        [ReadOnly, NoAlias] public NativeArray<GasPhysiologyStateDTO> GasStates;
        [NoAlias] public NativeArray<VitalsExportDTO> VitalsExport;
        [NoAlias] public NativeArray<PhysiologyTelemetryEntry> Telemetry;
        [ReadOnly, NoAlias] public NativeArray<DecompressionStateDTO> DecompressionStates;
        [NoAlias] public NativeArray<DecompressionTelemetryEntry> DecompressionTelemetry;
        [WriteOnly, NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<CardiacPulseSignal>.ParallelWriter CardiacPulseWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> CardiacPulseWriterBudget;
        public PhysiologyTuningDTO Tuning;
        public GasPhysiologyTuningDTO GasTuning;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public uint Frame;
        public int TelemetryCursor;
        public int DecompressionTelemetryCursor;
        public int Count;
        public byte EmitPulseSignals;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                (uint)index >= (uint)Vitals.Length ||
                (uint)index >= (uint)Scalars.Length ||
                (uint)index >= (uint)StatusEffects.Length ||
                (uint)index >= (uint)PulseStates.Length ||
                (uint)index >= (uint)VitalsExport.Length)
            {
                return;
            }

            PhysiologyTuningDTO tuning = ShinobuPhysiologyJobMath.SanitizeTuning(Tuning);
            GasPhysiologyTuningDTO gasTuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(GasTuning);
            float dt = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(DeltaSeconds, 0.016f), 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            MockEnvironmentVitalsSignal env = Environment.IsCreated && (uint)index < (uint)Environment.Length
                ? Environment[index]
                : default;

            PhysiologyDTO vital = Vitals[index];
            PhysiologyScalarsDTO scalar = Scalars[index];
            CardiacPulseStateDTO pulse = PulseStates[index];
            GasPhysiologyStateDTO gas = GasStates.IsCreated && (uint)index < (uint)GasStates.Length
                ? GasStates[index]
                : default;
            uint status = scalar.StatusFlags;

            int traumaCount = ShinobuPhysiologyJobMath.CountFirstEightBits(vital.ActiveTraumaMask);
            float traumaSeverity = traumaCount;
            float adrenaline = ShinobuPhysiologyJobMath.SanitizeUnit(vital.Adrenaline);
            if (adrenaline > 0f)
                status |= ShinobuPhysiologyFlags.AdrenalineSeen;

            float adrenalineDecayT = math.saturate(dt * math.rcp(math.max(0.0001f, tuning.AdrenalineDecaySeconds)));
            adrenaline = math.lerp(adrenaline, 0f, adrenalineDecayT);

            if ((status & ShinobuPhysiologyFlags.AdrenalineSeen) != 0u && adrenaline <= 0.02f)
            {
                scalar.FatigueMultiplier = math.max(2f, math.max(1f, ShinobuPhysiologyJobMath.SanitizeFinite(gas.StaminaDrainRate, 1f)));
                status |= ShinobuPhysiologyFlags.AdrenalineCrash;
            }
            else
            {
                scalar.FatigueMultiplier = math.max(1f, ShinobuPhysiologyJobMath.SanitizeFinite(gas.StaminaDrainRate, 1f));
                status &= ~ShinobuPhysiologyFlags.AdrenalineCrash;
            }

            float ambientTemperature = ShinobuPhysiologyJobMath.SanitizeFinite(env.AmbientTemperatureCelsius, 4f);
            bool insulated = (env.InventoryMask & ShinobuInventoryBits.ThermalSuitUpgrade) != 0u;
            float insulation = insulated ? tuning.ThermalSuitInsulation01 : 0f;
            float cooling = ShinobuPhysiologyJobMath.OneMinusApproxExpNegPade33Reduced(tuning.HypothermiaCoolingRate * dt);
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
            float ppO2 = gas.OxygenPartialPressure > 0f
                ? ShinobuPhysiologyJobMath.SanitizeFinite(gas.OxygenPartialPressure, ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm)
                : ambientPressureAtm * ShinobuPhysiologyConstants.OxygenFraction;
            float hypoxia01 = ShinobuPhysiologyJobMath.ResolveHypoxiaTunnel01(ppO2, gasTuning);
            float oxygenAvailability01 = ShinobuPhysiologyJobMath.ResolveOxygenAvailability01(ppO2, gasTuning);
            float carbonDioxideToxicity01 = ShinobuPhysiologyJobMath.ResolveCarbonDioxideToxicity01(gas.CarbonDioxidePartialPressure, gasTuning);
            float o2Drain = tuning.BaseO2DrainPerSecond *
                (0.65f + heartScale * 0.35f + adrenaline * 0.42f) *
                traumaDrain *
                (1f + scalar.Toxemia * tuning.ToxemiaO2Penalty) *
                (1f + scalar.HypothermiaShiver * 0.2f) *
                pressureBreathScale *
                math.max(1f, ShinobuPhysiologyJobMath.SanitizeFinite(gas.StaminaDrainRate, 1f));

            scalar.OxygenDrainPerSecond = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(o2Drain, tuning.BaseO2DrainPerSecond));
            float saturationBlend = ShinobuPhysiologyJobMath.OneMinusApproxExpNegPade33Reduced(dt * 0.6f);
            vital.BloodOxygen = math.lerp(vital.BloodOxygen, oxygenAvailability01, saturationBlend);
            float hypoxicDrainScale = 0.2f + hypoxia01 + carbonDioxideToxicity01 * 0.5f;
            vital.BloodOxygen = math.max(tuning.MinOxygen01, vital.BloodOxygen - scalar.OxygenDrainPerSecond * hypoxicDrainScale * dt);
            vital.BloodOxygen = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(vital.BloodOxygen, 1f), 0f, 1f);
            vital.Adrenaline = adrenaline;
            scalar.SwimSpeedBonus = adrenaline * 0.2f;

            status &= ~ShinobuPhysiologyFlags.OxygenCritical;
            if (vital.BloodOxygen <= 0.18f || hypoxia01 > 0f)
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
                        SignalBus<CardiacPulseSignal>.TryEnqueueBounded(CardiacPulseWriter, CardiacPulseWriterBudget, signal);
                    }
                }
            }

            pulse.Phase = math.frac(math.max(0f, pulse.Phase));
            pulse.LastHeartRate = vital.HeartRate;
            scalar.HeartbeatPhase = pulse.Phase;

            uint fatalFlags = 0u;
            if (vital.BloodOxygen <= ShinobuPhysiologyConstants.OxygenDeathThreshold ||
                (gas.Flags & ShinobuPhysiologyFlags.FatalGasToxicity) != 0u)
            {
                status |= ShinobuPhysiologyFlags.FatalOxygen;
                fatalFlags |= ShinobuPhysiologyFlags.FatalOxygen;
            }
            if ((gas.Flags & ShinobuPhysiologyFlags.FatalGasToxicity) != 0u)
                fatalFlags |= ShinobuPhysiologyFlags.FatalGasToxicity;
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

            StatusEffectStateDTO previousStatusEffectState = StatusEffects[index];
            uint activeTraumaMask = ShinobuPhysiologyJobMath.ClearExpiredTransientTraumaMask(
                vital.ActiveTraumaMask,
                vital.ActiveTraumaRefreshMask,
                previousStatusEffectState,
                vital.LastTraumaRefreshFrame,
                Frame);
            activeTraumaMask = scalar.Toxemia > 0.0001f
                ? activeTraumaMask | ShinobuTraumaBits.Poison
                : activeTraumaMask & ~ShinobuTraumaBits.Poison;
            activeTraumaMask = previousStatusEffectState.StunSeconds >= 2f
                ? activeTraumaMask & ~ShinobuTraumaBits.Stun
                : activeTraumaMask;
            activeTraumaMask = (status & ShinobuPhysiologyFlags.Hypoxia) != 0u
                ? activeTraumaMask
                : activeTraumaMask & ~ShinobuTraumaBits.Suffocation;
            vital.ActiveTraumaMask = activeTraumaMask;
            vital.ActiveTraumaRefreshMask = 0u;

            scalar.StatusFlags = status;
            scalar.StatusEffectMask = ShinobuPhysiologyJobMath.BuildStatusEffectMask(status, activeTraumaMask, 0u);
            StatusEffectStateDTO statusEffectState = ShinobuPhysiologyJobMath.BuildStatusEffectState(
                previousStatusEffectState,
                scalar.StatusEffectMask,
                Frame,
                dt,
                activeTraumaMask,
                scalar.Toxemia,
                scalar.NarcosisSeverity,
                scalar.FatigueMultiplier,
                vital.BloodOxygen);
            StatusEffects[index] = statusEffectState;
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
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, math.asuint(gas.OxygenPartialPressure));
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, math.asuint(gas.NitrogenPartialPressure));
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, math.asuint(gas.CarbonDioxidePartialPressure));
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, (uint)statusEffectState.StatusEffectMask);
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, (uint)(statusEffectState.StatusEffectMask >> 32));
                hash = ShinobuPhysiologyJobMath.MixStateHash(hash, status);

                Telemetry[telemetryIndex] = new PhysiologyTelemetryEntry
                {
                    StateHash = hash,
                    StatusEffectMask = statusEffectState.StatusEffectMask,
                    Frame = Frame,
                    FatalFlags = fatalFlags,
                    BloodOxygen = vital.BloodOxygen,
                    NitrogenLoad = vital.TissueNitrogen,
                    CoreTemperature = vital.CoreTemperature,
                    AmbientPressureAtm = math.max(0f, env.AmbientPressureAtm),
                    NarcosisSeverity = scalar.NarcosisSeverity,
                    SupersaturationScalar = scalar.BendsRisk,
                    HeartRate = vital.HeartRate,
                    Adrenaline = vital.Adrenaline,
                    TissueOverMValueMask = scalar.TissueOverMValueMask,
                    ExecutionMicroseconds = 0f
                };

                if (DecompressionTelemetry.IsCreated && DecompressionTelemetry.Length > 0 &&
                    DecompressionStates.IsCreated && DecompressionStates.Length > 0)
                {
                    int decompressionIndex = DecompressionTelemetryCursor % DecompressionTelemetry.Length;
                    DecompressionStateDTO decompression = DecompressionStates[0];
                    float leadingTissue = 0f;
                    for (int tissueIndex = 0; tissueIndex < ShinobuPhysiologyConstants.TissueCompartmentCount; tissueIndex++)
                        leadingTissue = math.max(leadingTissue, math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(decompression.GetTissueTensionN2(tissueIndex), 0f)));

                    float ambient = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(decompression.CurrentAmbientPressure, env.AmbientPressureAtm));
                    float gradient = ShinobuPhysiologyJobMath.SanitizeFinite(decompression.GradientAdvantage, 0f);
                    float allowedAmbient = math.max(0f, ambient - gradient);
                    uint dcsFatalFlags = fatalFlags;
                    if (!math.isfinite(leadingTissue) ||
                        !math.isfinite(ambient) ||
                        !math.isfinite(gradient) ||
                        !math.isfinite(allowedAmbient))
                    {
                        dcsFatalFlags |= ShinobuPhysiologyFlags.InvalidMath;
                    }

                    ulong decompressionHash = hash;
                    decompressionHash = ShinobuPhysiologyJobMath.MixStateHash(decompressionHash, math.asuint(leadingTissue));
                    decompressionHash = ShinobuPhysiologyJobMath.MixStateHash(decompressionHash, math.asuint(gradient));
                    decompressionHash = ShinobuPhysiologyJobMath.MixStateHash(decompressionHash, decompression.BubbleFlags);
                    DecompressionTelemetry[decompressionIndex] = new DecompressionTelemetryEntry
                    {
                        StateHash = decompressionHash,
                        Frame = Frame,
                        BubbleFlags = decompression.BubbleFlags,
                        DepthMeters = math.max(0f, env.DepthMeters),
                        AmbientPressureAtm = ambient,
                        LeadingTissueTensionAtm = leadingTissue,
                        AllowedAmbientPressureAtm = allowedAmbient,
                        MValueGradientAtm = gradient,
                        SupersaturationScalar = scalar.BendsRisk,
                        ExecutionMicroseconds = 0f,
                        GlobalQualityWeight = math.saturate(ShinobuPhysiologyJobMath.SanitizeFinite(GlobalQualityWeight, 1f)),
                        TissueOverMValueMask = scalar.TissueOverMValueMask,
                        ActiveCompartments = (uint)ShinobuPhysiologyJobMath.ResolveActiveCompartmentCount(GlobalQualityWeight),
                        FatalFlags = dcsFatalFlags
                    };
                }
            }
        }
    }
}
