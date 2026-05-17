// ============================================================================
// HECTON-8 - SurvivalPhysiologyScalarJob.cs
// Burst-compatible scalar physiology step. No UnityEngine object access.
// ============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    public struct SurvivalPhysiologyScalarResult
    {
        public float NitrogenLoad;
        public float Narcosis01;
        public float MovementStaminaDrain;
        public uint StatusMask;
        public byte BendsDamageRequested;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public struct SurvivalPhysiologyScalarJob : IJob
    {
        public float CurrentNitrogenLoad;
        public float AmbientPressure;
        public float DeltaTime;
        public float AbsorptionRate;
        public float VerticalSpeed;
        public float SafeAscentRate;
        public float BendsNitrogenLoadThreshold;
        public float CoreTemperatureCelsius;
        public float FrostTemperatureThresholdCelsius;
        public float Hunger01;
        public float Thirst01;
        public float Toxicity01;
        public float NarcosisPressureThreshold;
        public float NarcosisPressureFullRange;
        public float MovementIntentLengthSq;
        public float MovementStaminaDrainPerSecond;

        public NativeSlice<SurvivalPhysiologyScalarResult> Result;

        public void Execute()
        {
            float nitrogenLoad = SomaticSurvivalMath.ResolveNitrogenTissueLoad(
                CurrentNitrogenLoad,
                AmbientPressure,
                DeltaTime,
                AbsorptionRate);
            float narcosis01 = SomaticSurvivalMath.ResolvePressureNarcosis01(
                AmbientPressure,
                NarcosisPressureThreshold,
                NarcosisPressureFullRange);
            bool bends = SomaticSurvivalMath.ShouldApplyBendsDamage(
                VerticalSpeed,
                nitrogenLoad,
                SafeAscentRate,
                BendsNitrogenLoadThreshold);

            uint statusMask = 0u;
            statusMask |= math.select(0u, SurvivalStatusMasks.Bends, bends);
            statusMask |= math.select(0u, SurvivalStatusMasks.Freezing, CoreTemperatureCelsius < FrostTemperatureThresholdCelsius);
            statusMask |= math.select(0u, SurvivalStatusMasks.Starving, Hunger01 <= 0.0001f);
            statusMask |= math.select(0u, SurvivalStatusMasks.Dehydrated, Thirst01 <= 0.0001f);
            statusMask |= math.select(0u, SurvivalStatusMasks.Narcosis, narcosis01 > 0.0001f);
            statusMask |= math.select(0u, SurvivalStatusMasks.Toxicity, Toxicity01 > 0.0001f);

            float movementStaminaDrain =
                math.max(0f, MovementIntentLengthSq) *
                math.max(0f, MovementStaminaDrainPerSecond) *
                math.max(0f, DeltaTime);

            Result[0] = new SurvivalPhysiologyScalarResult
            {
                NitrogenLoad = nitrogenLoad,
                Narcosis01 = narcosis01,
                MovementStaminaDrain = movementStaminaDrain,
                StatusMask = statusMask,
                BendsDamageRequested = (byte)math.select(0, 1, bends)
            };
        }
    }
}
