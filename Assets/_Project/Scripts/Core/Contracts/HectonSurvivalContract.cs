using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class HectonSurvivalContract
    {
        public const float KPaPerAtmosphere = 101.325f;
        public const float StandardOxygenKPa = 21.22f;
        public const float StandardCarbonDioxideKPa = 0.04f;
        public const float StandardNitrogenKPa = 80.065f;
        public const float StandardOxygenFraction01 = 0.21f;
        public const float MaxOxygenFraction01 = 1f;
        public const float LowOxygenStressThreshold01 = 0.25f;
        public const float CriticalOxygenStressThreshold01 = 0.05f;
        public const float DefaultPlayerOxygenKPaPerSecond = 0.012f;
        public const float DefaultPlayerCarbonDioxideKPaPerSecond = 0.010f;
        public const float DefaultFireOxygenKPaPerSecond = 0.080f;
        public const float DefaultScrubberKPaPerSecond = 0.055f;
        public const float DefaultCo2ToxicityThresholdKPa = 1.0f;
        public const float DefaultCo2FatalKPa = 7.0f;
        public const float DefaultNarcosisThresholdAtm = 4.0f;
        public const float DefaultNarcosisFullAtm = 7.0f;
        public const float DefaultRoomTemperatureCelsius = 20f;
        public const float FreezingScrubberEfficiencyScale = 0.5f;
        public const float DefaultDiffusionConductancePerSecond = 0.45f;
        public const float DefaultHibernationDistanceMeters = 500f;
        public const float DefaultLowTierHibernationDistanceMeters = 150f;
        public const float DefaultHibernationHysteresisMeters = 25f;
        public const float DefaultBaseIdleDrawWatts = 45f;
        public const float DefaultBaseBatteryWattSeconds = 720000f;
        public const float DefaultHibernationLeakRatePerSecond = 0.00006f;
        public const float MaxWakeCatchUpSeconds = 86400f;
        public const float MaxDiffusionFractionPerStep = 0.45f;
        public const float StressSubstepDeltaSeconds = 0.1f;
        public const int StressSubstepsPerSlowTick = 5;
        public const float DarknessLightThreshold01 = 0.2f;
        public const float SafeLightThreshold01 = 0.8f;
        public const float DarknessStressPerSecond = 0.05f;
        public const float ApexStressPerSecond = 0.2f;
        public const float RecoveryPerSecond = 0.1f;
        public const float ApexThreatRadiusMeters = 50f;
        public const float AcousticStressImpulseScale = 0.08f;
        public const float DamageStressImpulseScale = 0.18f;
        public const float SqueezeStressImpulseScale = 1.0f;
        public const float SqueezeStressPerSecond = 0.1f;
        public const float O2StressMultiplier = 1.5f;
        public const float NeutralLightLevel01 = 0.5f;
        public const float PanicAttackThreshold01 = 1f;
        public const float HallucinationStressThreshold01 = 0.9f;
        public const float HallucinationResetThreshold01 = 0.84f;
        public const int HallucinationCooldownMinSlowTicks = 36;
        public const int HallucinationCooldownRandomSlowTicks = 48;
        public const float HallucinationForwardMeters = 36f;
        public const float HallucinationSideMeters = 18f;
        public const float HallucinationUpMeters = 1.25f;
        public const float ClimbStaminaDrainPerMeter = 0.18f;
        public const float ClimbStressOxygenDrainBonus = 0.28f;
        public const float PressureDamageSafeHullRelief01 = 0.45f;
        public const float PressureDamageReliefPerAtmosphere = 0.08f;

        private static readonly float s_StandardOxygenKPa = StandardOxygenKPa;
        private static readonly float s_DefaultPlayerOxygenKPaPerSecond = DefaultPlayerOxygenKPaPerSecond;
        private static readonly float s_DefaultCo2ToxicityThresholdKPa = DefaultCo2ToxicityThresholdKPa;
        private static readonly float s_O2StressMultiplier = O2StressMultiplier;
        private static readonly float s_OneOverKPaPerAtmosphere = math.rcp(KPaPerAtmosphere);

        static HectonSurvivalContract()
        {
            HectonContractValidator.RequirePositive(KPaPerAtmosphere, nameof(KPaPerAtmosphere));
            HectonContractValidator.RequirePositive(s_OneOverKPaPerAtmosphere, nameof(OneOverKPaPerAtmosphere));
            HectonContractValidator.RequirePositive(s_StandardOxygenKPa, nameof(StandardOxygenKPaRef));
            HectonContractValidator.RequirePositive(StandardNitrogenKPa, nameof(StandardNitrogenKPa));
            HectonContractValidator.RequireFinite(StandardCarbonDioxideKPa, nameof(StandardCarbonDioxideKPa));
            HectonContractValidator.RequireUnit(StandardOxygenFraction01, nameof(StandardOxygenFraction01));
            HectonContractValidator.RequireUnit(MaxOxygenFraction01, nameof(MaxOxygenFraction01));
            HectonContractValidator.RequirePositive(s_DefaultPlayerOxygenKPaPerSecond, nameof(DefaultPlayerOxygenKPaPerSecondRef));
            HectonContractValidator.RequirePositive(s_DefaultCo2ToxicityThresholdKPa, nameof(DefaultCo2ToxicityThresholdKPaRef));
            HectonContractValidator.RequirePositive(s_O2StressMultiplier, nameof(O2StressMultiplierRef));
        }

        public static ref readonly float StandardOxygenKPaRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_StandardOxygenKPa; }
        }

        public static ref readonly float DefaultPlayerOxygenKPaPerSecondRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_DefaultPlayerOxygenKPaPerSecond; }
        }

        public static ref readonly float DefaultCo2ToxicityThresholdKPaRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_DefaultCo2ToxicityThresholdKPa; }
        }

        public static ref readonly float O2StressMultiplierRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_O2StressMultiplier; }
        }

        public static ref readonly float OneOverKPaPerAtmosphere
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_OneOverKPaPerAtmosphere; }
        }
    }
}
