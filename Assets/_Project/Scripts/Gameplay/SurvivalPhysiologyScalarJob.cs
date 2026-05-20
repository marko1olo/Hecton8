// ============================================================================
// HECTON-8 - SurvivalPhysiologyScalarJob.cs
// Burst-compatible scalar physiology step. No UnityEngine object access.
// ============================================================================

using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SurvivalPhysiologyScalarResult
    {
        [FieldOffset(0)] public float NitrogenLoad;
        [FieldOffset(4)] public float Narcosis01;
        [FieldOffset(8)] public float MovementStaminaDrain;
        [FieldOffset(12)] public uint StatusMask;
        [FieldOffset(16)] public byte BendsDamageRequested;
        [FieldOffset(17)] public byte _pad0;
        [FieldOffset(18)] public ushort _pad1;
        [FieldOffset(20)] public uint _pad2;
        [FieldOffset(24)] public ulong _pad3;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
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

        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SurvivalPhysiologyScalarResult> Result;

        public void Execute()
        {
            if (!Result.IsCreated || Result.Length < 1)
                return;

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

            SurvivalPhysiologyScalarResult result = default;
            result.NitrogenLoad = nitrogenLoad;
            result.Narcosis01 = narcosis01;
            result.MovementStaminaDrain = movementStaminaDrain;
            result.StatusMask = statusMask;
            result.BendsDamageRequested = (byte)math.select(0, 1, bends);
            Result[0] = result;
        }
    }
}
