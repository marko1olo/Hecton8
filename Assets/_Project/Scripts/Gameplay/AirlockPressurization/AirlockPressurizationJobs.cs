// ============================================================================
// HECTON-8 - AirlockPressurizationJobs.cs
// SHINOBU_338 Burst kernels for airlock pressure, Torricelli water exchange, gas mixing, and telemetry.
// ============================================================================

using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Atmosphere;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using FluidCompartmentDTO = global::Hecton8.Core.Contracts.Physics.FluidCompartmentDTO;
using FluidCompartmentFlags = global::Hecton8.Core.Contracts.Physics.FluidCompartmentFlags;
using HabitatFluidIncursionConstants = global::Hecton8.Core.Contracts.Physics.HabitatFluidIncursionConstants;

namespace Hecton8.Gameplay.AirlockPressurization
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockAirlockCycleJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AirlockStateDTO> Airlocks;
        [NoAlias] public NativeArray<AirlockTuningDTO> Tunings;
        [NoAlias] public NativeArray<AirlockDoorPoseDTO> DoorPoses;
        [NoAlias] public NativeArray<AirlockExchangeIndexDTO> ExchangeIndices;
        public double3 OriginAup;
        public uint Frame;

        public void Execute(int index)
        {
            uint phase = (Frame + (uint)(index * 17)) & 127u;
            float wave = phase <= 63u ? phase * (1f / 63f) : (127u - phase) * (1f / 63f);
            float depth = 40f + (index % 10) * 55f;
            float maxWater = math.lerp(1800f, 9000f, wave);
            float pressure = AirlockPressurizationMath.ResolveExternalPressureAtm(depth);
            uint flags = AirlockCycleFlags.MockGenerated;
            flags |= phase < 32u ? AirlockCycleFlags.Pumping : 0u;
            flags |= phase >= 32u && phase < 72u ? AirlockCycleFlags.Equalizing : 0u;
            flags |= phase >= 72u ? AirlockCycleFlags.OuterOpen : 0u;
            flags |= phase > 112u ? AirlockCycleFlags.ForceInnerOpen : 0u;

            Airlocks[index] = new AirlockStateDTO
            {
                InnerRoomHashID = 0xA1100000u + (uint)index,
                OuterRoomHashID = 0x0CE00000u + (uint)index,
                CurrentWaterVolumeLiters = math.saturate(1f - wave) * maxWater,
                CurrentPressureATM = math.lerp(1f, pressure, wave),
                CycleStateFlags = flags,
                CycleTimer = wave * 10f
            };

            if (index < Tunings.Length)
            {
                Tunings[index] = new AirlockTuningDTO
                {
                    PumpEvacuationSpeedLps = AirlockPressurizationConstants.DefaultPumpEvacuationSpeedLps + index * 3f,
                    MaxWaterVolumeLiters = maxWater,
                    ChamberVolumeLiters = math.max(maxWater + 400f, 2400f),
                    EqualizationCurveExponent = AirlockPressurizationConstants.DefaultEqualizationCurveExponent,
                    PowerDrawWatts = AirlockPressurizationConstants.DefaultPowerDrawWatts,
                    AvailablePower01 = math.lerp(0.35f, 1f, wave),
                    ExternalDepthMeters = depth,
                    BreachAreaM2 = AirlockPressurizationConstants.DefaultBreachAreaM2,
                    DischargeCoefficient = AirlockPressurizationConstants.DefaultDischargeCoefficient,
                    GlobalQualityWeight = math.saturate(wave),
                    PressureEqualizedAtm = AirlockPressurizationConstants.PressureEqualizedAtm,
                    WaterEqualizedLiters = AirlockPressurizationConstants.WaterEqualizedLiters,
                    ExternalPressureAtm = pressure,
                    RoomPressureAtm = AirlockPressurizationConstants.SurfacePressureAtm,
                    Flags = 0u,
                    Frame = Frame
                };
            }

            if (index < DoorPoses.Length)
            {
                double3 doorAup = OriginAup + new double3(index * 3.25d, 0d, (index % 5) * 2.5d);
                DoorPoses[index] = new AirlockDoorPoseDTO
                {
                    DoorAup = AbsoluteUniversePosition.FromAbsolutePosition(doorAup),
                    DoorNormal = new float3(0f, 0f, 1f),
                    WidthMeters = 2.6f,
                    HeightMeters = 3.2f,
                    DoorHashID = 0xD0000000u + (uint)index,
                    EdgeHashID = 0xE0000000u + (uint)index,
                    Flags = AirlockDoorPoseFlags.Valid | AirlockDoorPoseFlags.OuterFaceSubmerged,
                    ExternalDepthMeters = depth,
                    HeadMeters = math.max(0f, depth - 2f),
                    Frame = Frame
                };
            }

            if (index < ExchangeIndices.Length)
            {
                ExchangeIndices[index] = new AirlockExchangeIndexDTO
                {
                    FluidCompartmentIndex = index,
                    AtmosphereCellIndex = index,
                    OwnerIndex = index,
                    Flags = 0u
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateAirlockCyclesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AirlockStateDTO> Airlocks;
        [ReadOnly, NoAlias] public NativeArray<AirlockTuningDTO> Tunings;
        [ReadOnly, NoAlias] public NativeArray<AirlockDoorPoseDTO> DoorPoses;
        [NoAlias] public NativeArray<AirlockEvaluationResultDTO> Results;
        [NoAlias] public NativeArray<BulkheadContainmentIntentDTO> BulkheadIntents;
        [NoAlias] public NativeArray<BubbleSpawnSignal> VfxSignals;
        [NoAlias] public NativeArray<MovementAcousticSignal> AcousticSignals;
        [NoAlias] public NativeArray<AirlockDebugGizmoDTO> DebugGizmos;
        public float DeltaTime;
        public float GlobalQualityWeight;
        public uint Frame;

        public void Execute(int index)
        {
            if (index >= Airlocks.Length)
                return;

            AirlockStateDTO airlock = Airlocks[index];
            AirlockTuningDTO tuning = ResolveTuning(index);
            AirlockDoorPoseDTO door = ResolveDoor(index);
            AirlockEvaluationResultDTO result = default;
            BubbleSpawnSignal vfx = default;
            MovementAcousticSignal acoustic = default;

            float dt = math.max(0f, AirlockPressurizationMath.FiniteOr(DeltaTime, 0f));
            float visualQuality = math.saturate(AirlockPressurizationMath.FiniteOr(
                GlobalQualityWeight,
                AirlockPressurizationMath.FiniteOr(tuning.GlobalQualityWeight, 0.5f)));
            const float authorityQuality = AirlockPressurizationConstants.AuthoritativeQualityWeight;
            float maxWater = math.max(1f, AirlockPressurizationMath.FiniteOr(tuning.MaxWaterVolumeLiters, AirlockPressurizationConstants.LitersPerCubicMeter));
            float chamberLiters = math.max(maxWater + 1f, AirlockPressurizationMath.FiniteOr(tuning.ChamberVolumeLiters, maxWater + 400f));
            float water = math.clamp(AirlockPressurizationMath.FiniteOr(airlock.CurrentWaterVolumeLiters, 0f), 0f, maxWater);
            float pressure = math.max(AirlockPressurizationConstants.MinimumPressureAtm, AirlockPressurizationMath.FiniteOr(airlock.CurrentPressureATM, 1f));
            float roomPressure = math.max(AirlockPressurizationConstants.MinimumPressureAtm, AirlockPressurizationMath.FiniteOr(tuning.RoomPressureAtm, 1f));
            float externalPressure = math.max(
                AirlockPressurizationMath.ResolveExternalPressureAtm(door.ExternalDepthMeters),
                AirlockPressurizationMath.FiniteOr(tuning.ExternalPressureAtm, 1f));

            uint flags = airlock.CycleStateFlags;
            bool pumping = (flags & AirlockCycleFlags.Pumping) != 0u;
            bool equalizing = (flags & AirlockCycleFlags.Equalizing) != 0u;
            bool outerOpen = (flags & (AirlockCycleFlags.OuterOpen | AirlockCycleFlags.ForceOuterOpen)) != 0u;
            bool innerOpen = (flags & (AirlockCycleFlags.InnerOpen | AirlockCycleFlags.ForceInnerOpen)) != 0u;
            float originalWater = water;
            float originalPressure = pressure;

            if (pumping)
            {
                float pumpRate = math.max(0f, AirlockPressurizationMath.FiniteOr(tuning.PumpEvacuationSpeedLps, AirlockPressurizationConstants.DefaultPumpEvacuationSpeedLps));
                float power01 = math.saturate(AirlockPressurizationMath.FiniteOr(tuning.AvailablePower01, 1f));
                float moved = math.min(water, pumpRate * power01 * dt);
                water -= moved;
                pressure = AirlockPressurizationMath.ApplyNonLinearEqualization(
                    pressure,
                    roomPressure,
                    tuning.EqualizationCurveExponent,
                    dt * math.lerp(0.7f, 1.15f, authorityQuality),
                    chamberLiters);
                result.PumpMovedLiters = moved;
                flags |= moved > 0.001f ? AirlockCycleFlags.AcousticPump : 0u;
            }

            if (equalizing)
            {
                float target = outerOpen ? externalPressure : roomPressure;
                pressure = AirlockPressurizationMath.ApplyNonLinearEqualization(
                    pressure,
                    target,
                    tuning.EqualizationCurveExponent,
                    dt,
                    chamberLiters);
            }

            if (outerOpen)
            {
                float head = math.max(0f, AirlockPressurizationMath.FiniteOr(door.HeadMeters, tuning.ExternalDepthMeters));
                float flowLps = AirlockPressurizationMath.ResolveTorricelliLitersPerSecond(
                    tuning.BreachAreaM2,
                    head,
                    tuning.DischargeCoefficient);
                float moved = math.min(maxWater - water, flowLps * dt);
                water += moved;
                pressure = AirlockPressurizationMath.ApplyNonLinearEqualization(
                    pressure,
                    externalPressure,
                    tuning.EqualizationCurveExponent + 0.5f,
                    dt,
                    chamberLiters);
                result.TorricelliMovedLiters = moved;
            }

            float targetPressure = outerOpen ? externalPressure : roomPressure;
            float pressureDelta = math.abs(targetPressure - pressure);
            float waterDelta = math.abs(water - (outerOpen ? maxWater : 0f));
            float pressureTolerance = math.max(0.001f, AirlockPressurizationMath.FiniteOr(tuning.PressureEqualizedAtm, AirlockPressurizationConstants.PressureEqualizedAtm));
            float waterTolerance = math.max(0.001f, AirlockPressurizationMath.FiniteOr(tuning.WaterEqualizedLiters, AirlockPressurizationConstants.WaterEqualizedLiters));
            bool blocked = pumping || equalizing || pressureDelta > pressureTolerance || waterDelta > waterTolerance;
            if (blocked)
                flags |= AirlockCycleFlags.CollisionBlocked;
            else
                flags &= ~AirlockCycleFlags.CollisionBlocked;

            bool catastrophic = (flags & AirlockCycleFlags.ForceInnerOpen) != 0u &&
                                outerOpen &&
                                water >= maxWater * AirlockPressurizationConstants.CatastrophicWaterRatio;
            if (catastrophic)
            {
                flags |= AirlockCycleFlags.CatastrophicFlood | AirlockCycleFlags.CollisionBlocked;
                result.StressSpike01 = math.saturate(math.max(0f, externalPressure - roomPressure) *
                                                     math.rcp(AirlockPressurizationConstants.CatastrophicStressScaleAtm));
            }

            bool finite = math.isfinite(water) && math.isfinite(pressure) && math.isfinite(pressureDelta);
            if (!finite)
            {
                water = 0f;
                pressure = roomPressure;
                flags |= AirlockCycleFlags.NonFinite;
                result.Flags |= AirlockCycleFlags.NonFinite;
            }

            airlock.CurrentWaterVolumeLiters = math.clamp(water, 0f, maxWater);
            airlock.CurrentPressureATM = math.max(AirlockPressurizationConstants.MinimumPressureAtm, pressure);
            airlock.CycleStateFlags = flags;
            airlock.CycleTimer = math.max(0f, AirlockPressurizationMath.FiniteOr(airlock.CycleTimer, 0f) - dt);
            Airlocks[index] = airlock;

            result.WaterDeltaLiters = math.abs(airlock.CurrentWaterVolumeLiters - originalWater);
            result.PressureDeltaAtm = math.abs(airlock.CurrentPressureATM - originalPressure);
            result.TargetPressureAtm = targetPressure;
            float safePressureDelta = math.max(0f, result.PressureDeltaAtm);
            result.VfxIntensity01 = math.saturate(safePressureDelta * math.rcp(AirlockPressurizationConstants.ViolentPressureDeltaAtm));
            result.Flags |= flags;
            result.Frame = Frame;
            result.EdgeHashID = door.EdgeHashID;
            result.InnerRoomHashID = airlock.InnerRoomHashID;
            result.OuterRoomHashID = airlock.OuterRoomHashID;
            result.GasExchange01 = math.saturate(math.max(0f, chamberLiters - water) * math.rcp(chamberLiters));
            result.EffectHash = result.VfxIntensity01 > 0.65f
                ? AirlockPressurizationConstants.ViolentBubblesHash
                : AirlockPressurizationConstants.CondensationFogHash;
            result.StateHash = HashState(in airlock, in result);
            if (index < BulkheadIntents.Length)
            {
                BulkheadContainmentIntentDTO intent = default;
                float normalLengthSq = math.lengthsq(door.DoorNormal);
                bool validDoor =
                    door.EdgeHashID != 0u &&
                    (door.Flags & AirlockDoorPoseFlags.Valid) != 0u &&
                    door.DoorAup.IsFinite() &&
                    math.all(math.isfinite(door.DoorNormal)) &&
                    math.isfinite(normalLengthSq) &&
                    normalLengthSq > 0.0001f;

                double3 centerAup = validDoor ? door.DoorAup.ToAbsoluteDouble3() : default;
                validDoor &= math.all(math.isfinite(centerAup));

                if (validDoor)
                {
                    intent = new BulkheadContainmentIntentDTO
                    {
                        CenterAup = centerAup,
                        Normal = door.DoorNormal * math.rsqrt(math.max(normalLengthSq, 0.0001f)),
                        WidthMeters = math.max(0.25f, AirlockPressurizationMath.FiniteOr(door.WidthMeters, 2.6f)),
                        HeightMeters = math.max(0.25f, AirlockPressurizationMath.FiniteOr(door.HeightMeters, 3.2f)),
                        ParentIntegrity01 = math.saturate(1f - result.StressSpike01),
                        EdgeHashID = door.EdgeHashID,
                        SiblingNodeHash = airlock.OuterRoomHashID != 0u ? airlock.OuterRoomHashID : airlock.InnerRoomHashID,
                        Flags = BulkheadContainmentIntentFlags.Valid |
                                (blocked ? BulkheadContainmentIntentFlags.Locked : BulkheadContainmentIntentFlags.None),
                        Frame = Frame
                    };
                }

                BulkheadIntents[index] = intent;
            }

            int vfxPeriod = math.max(1, (int)math.round(math.lerp(10f, 2f, visualQuality)));
            if (result.VfxIntensity01 > 0.2f && ((Frame + (uint)index) % (uint)vfxPeriod) == 0u)
            {
                vfx = new BubbleSpawnSignal
                {
                    PositionAup = door.DoorAup,
                    Direction = door.DoorNormal,
                    Intensity01 = result.VfxIntensity01,
                    RadiusMeters = math.lerp(0.4f, 2.5f, result.VfxIntensity01),
                    Frame = Frame,
                    SourceHash = result.EffectHash,
                    Flags = result.EffectHash == AirlockPressurizationConstants.ViolentBubblesHash
                        ? BubbleSpawnSignal.FlagEngineVent | BubbleSpawnSignal.FlagTailHeavy
                        : BubbleSpawnSignal.FlagEngineVent
                };
                flags |= result.EffectHash == AirlockPressurizationConstants.ViolentBubblesHash
                    ? AirlockCycleFlags.ViolentBubbles
                    : AirlockCycleFlags.CondensationFog;
                airlock.CycleStateFlags = flags;
                Airlocks[index] = airlock;
                result.Flags |= flags;
            }

            int audioPeriod = math.max(1, (int)math.round(math.lerp(18f, 5f, visualQuality)));
            if (pumping && ((Frame + (uint)(index * 3)) % (uint)audioPeriod) == 0u)
            {
                acoustic = new MovementAcousticSignal
                {
                    PositionAup = door.DoorAup,
                    Volume = math.saturate(result.PumpMovedLiters * math.rcp(math.max(1f, maxWater * 0.03f))),
                    VelocitySq = math.max(0f, result.PumpMovedLiters * result.PumpMovedLiters),
                    SourceId = AirlockPressurizationConstants.HeavyPumpHash ^ door.DoorHashID,
                    LocomotionMode = 0,
                    SurfaceMode = 0,
                    Flags = 1
                };
            }

            if (index < VfxSignals.Length)
                VfxSignals[index] = vfx;
            if (index < AcousticSignals.Length)
                AcousticSignals[index] = acoustic;
            if (index < Results.Length)
                Results[index] = result;
            if (index < DebugGizmos.Length)
            {
                DebugGizmos[index] = new AirlockDebugGizmoDTO
                {
                    DoorAup = door.DoorAup,
                    CurrentWaterVolumeLiters = airlock.CurrentWaterVolumeLiters,
                    MaxWaterVolumeLiters = maxWater,
                    CurrentPressureAtm = airlock.CurrentPressureATM,
                    Flags = flags
                };
            }
        }

        private AirlockTuningDTO ResolveTuning(int index)
        {
            if (Tunings.IsCreated && Tunings.Length > 0)
                return Tunings[math.min(index, Tunings.Length - 1)];

            return new AirlockTuningDTO
            {
                PumpEvacuationSpeedLps = AirlockPressurizationConstants.DefaultPumpEvacuationSpeedLps,
                MaxWaterVolumeLiters = 2400f,
                ChamberVolumeLiters = 3000f,
                EqualizationCurveExponent = AirlockPressurizationConstants.DefaultEqualizationCurveExponent,
                AvailablePower01 = 1f,
                ExternalDepthMeters = 100f,
                BreachAreaM2 = AirlockPressurizationConstants.DefaultBreachAreaM2,
                DischargeCoefficient = AirlockPressurizationConstants.DefaultDischargeCoefficient,
                GlobalQualityWeight = 0.5f,
                PressureEqualizedAtm = AirlockPressurizationConstants.PressureEqualizedAtm,
                WaterEqualizedLiters = AirlockPressurizationConstants.WaterEqualizedLiters,
                ExternalPressureAtm = AirlockPressurizationMath.ResolveExternalPressureAtm(100f),
                RoomPressureAtm = 1f
            };
        }

        private AirlockDoorPoseDTO ResolveDoor(int index)
        {
            if (DoorPoses.IsCreated && DoorPoses.Length > 0)
                return DoorPoses[math.min(index, DoorPoses.Length - 1)];

            return new AirlockDoorPoseDTO
            {
                DoorAup = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero),
                DoorNormal = new float3(0f, 0f, 1f),
                WidthMeters = 2.6f,
                HeightMeters = 3.2f,
                DoorHashID = 0xD0000000u,
                EdgeHashID = 0xE0000000u,
                Flags = AirlockDoorPoseFlags.Valid,
                ExternalDepthMeters = 100f,
                HeadMeters = 100f,
                Frame = Frame
            };
        }

        private static uint HashState(in AirlockStateDTO state, in AirlockEvaluationResultDTO result)
        {
            uint hash = 2166136261u;
            hash = AirlockPressurizationMath.Hash(hash, state.InnerRoomHashID);
            hash = AirlockPressurizationMath.Hash(hash, state.OuterRoomHashID);
            hash = AirlockPressurizationMath.HashFloat(hash, state.CurrentWaterVolumeLiters);
            hash = AirlockPressurizationMath.HashFloat(hash, state.CurrentPressureATM);
            hash = AirlockPressurizationMath.Hash(hash, state.CycleStateFlags);
            hash = AirlockPressurizationMath.HashFloat(hash, state.CycleTimer);
            hash = AirlockPressurizationMath.HashFloat(hash, result.StressSpike01);
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct IntegrateAirlockExchangeJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public AirlockStateDTO* Airlocks;
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AirlockExchangeIndexDTO* ExchangeIndices;
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public AirlockTuningDTO* Tunings;
        [NoAlias, NativeDisableUnsafePtrRestriction] public FluidCompartmentDTO* FluidCompartments;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AtmosphereCellDTO* AtmosphereCells;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AirlockEvaluationResultDTO* Results;
        public int AirlockCount;
        public int TuningCount;
        public int FluidCompartmentCount;
        public int AtmosphereCellCount;
        public int ResultCount;
        public int ExchangeCount;
        public float DeltaTime;

        public void Execute()
        {
            int count = math.min(math.max(0, AirlockCount), math.max(0, ExchangeCount));
            for (int index = 0; index < count; index++)
                ExecuteExchange(index);
        }

        private void ExecuteExchange(int index)
        {
            if (index < 0 || index >= AirlockCount)
                return;

            ref AirlockStateDTO airlock = ref UnsafeUtility.AsRef<AirlockStateDTO>(Airlocks + index);
            AirlockExchangeIndexDTO route = UnsafeUtility.AsRef<AirlockExchangeIndexDTO>(ExchangeIndices + index);
            bool innerOpen = (airlock.CycleStateFlags & (AirlockCycleFlags.InnerOpen | AirlockCycleFlags.ForceInnerOpen)) != 0u;
            if (!innerOpen)
                return;

            uint flags = 0u;
            float dt = math.max(0f, AirlockPressurizationMath.FiniteOr(DeltaTime, 0f));
            float requestedLiters = math.max(0f, AirlockPressurizationMath.FiniteOr(airlock.CurrentWaterVolumeLiters, 0f));
            bool catastrophic = (airlock.CycleStateFlags & AirlockCycleFlags.CatastrophicFlood) != 0u;
            float transferLiters = catastrophic ? requestedLiters : math.min(requestedLiters, math.max(1f, requestedLiters * math.saturate(dt * 2.5f)));

            if (transferLiters > 0.001f &&
                route.FluidCompartmentIndex >= 0 &&
                route.FluidCompartmentIndex < FluidCompartmentCount)
            {
                    float removedLiters;
                    if (!TryAtomicAddFloat(
                        FloatPtr(ref airlock.CurrentWaterVolumeLiters),
                        -transferLiters,
                        0f,
                        requestedLiters,
                        out removedLiters))
                {
                    flags |= AirlockCycleFlags.AtomicAbort;
                }
                else
                {
                    float removedPositive = math.max(0f, -removedLiters);
                    ref FluidCompartmentDTO compartment = ref UnsafeUtility.AsRef<FluidCompartmentDTO>(FluidCompartments + route.FluidCompartmentIndex);
                    float requestedM3 = removedPositive * AirlockPressurizationConstants.CubicMetersPerLiter;
                    float appliedM3;
                    bool added = TryAtomicAddFloat(
                        FloatPtr(ref compartment.CurrentWaterVolume),
                        requestedM3,
                        0f,
                        math.max(AirlockPressurizationMath.FiniteOr(compartment.MaxWaterVolume, requestedM3), requestedM3),
                        out appliedM3);
                    if (added)
                    {
                        float maxVolume = math.max(
                            HabitatFluidIncursionConstants.WaterEpsilonM3,
                            AirlockPressurizationMath.FiniteOr(compartment.MaxWaterVolume, requestedM3));
                        float currentWater = AirlockPressurizationMath.FiniteOr(AtomicReadFloat(FloatPtr(ref compartment.CurrentWaterVolume)), 0f);
                        float waterLevel = math.saturate(currentWater * math.rcp(maxVolume));
                        if (!TryAtomicMaxFloat(FloatPtr(ref compartment.WaterLevelHeight01), waterLevel))
                            flags |= AirlockCycleFlags.AtomicAbort;
                        if (currentWater >= maxVolume - HabitatFluidIncursionConstants.WaterEpsilonM3 &&
                            !TryAtomicOrUInt(UIntPtr(ref compartment.Flags), FluidCompartmentFlags.Flooded))
                        {
                            flags |= AirlockCycleFlags.AtomicAbort;
                        }

                        float unappliedLiters = math.max(0f, removedPositive - appliedM3 * AirlockPressurizationConstants.LitersPerCubicMeter);
                        if (unappliedLiters > 0.001f)
                        {
                            float ignored;
                            TryAtomicAddFloat(FloatPtr(ref airlock.CurrentWaterVolumeLiters), unappliedLiters, 0f, requestedLiters, out ignored);
                        }
                    }
                    else
                    {
                        float ignored;
                        TryAtomicAddFloat(FloatPtr(ref airlock.CurrentWaterVolumeLiters), removedPositive, 0f, requestedLiters, out ignored);
                        flags |= AirlockCycleFlags.AtomicAbort;
                    }
                }
            }

            if (route.AtmosphereCellIndex >= 0 && route.AtmosphereCellIndex < AtmosphereCellCount)
            {
                ref AtmosphereCellDTO cell = ref UnsafeUtility.AsRef<AtmosphereCellDTO>(AtmosphereCells + route.AtmosphereCellIndex);
                float fallbackChamberLiters = 2400f;
                float chamberLiters = fallbackChamberLiters;
                if (Tunings != null && index < TuningCount)
                {
                    ref readonly AirlockTuningDTO tuning = ref UnsafeUtility.AsRef<AirlockTuningDTO>(Tunings + index);
                    chamberLiters = AirlockPressurizationMath.FiniteOr(tuning.ChamberVolumeLiters, fallbackChamberLiters);
                }

                chamberLiters = math.max(1f, chamberLiters);
                float airVolume01 = math.saturate(1f - math.saturate(airlock.CurrentWaterVolumeLiters * math.rcp(chamberLiters)));
                float mixFactor = math.saturate(airVolume01 * dt * 4f);
                bool outerOpen = (airlock.CycleStateFlags & (AirlockCycleFlags.OuterOpen | AirlockCycleFlags.ForceOuterOpen)) != 0u;
                float targetOxygen = outerOpen ? 0.02f : AtmosphereLogisticsConstants.DefaultOxygen01;
                float targetCarbonDioxide = outerOpen ? 0.02f : AtmosphereLogisticsConstants.DefaultCarbonDioxide01;
                float ignored;
                if (!TryAtomicBlendFloat(FloatPtr(ref cell.Oxygen01), targetOxygen, mixFactor, 0f, 1f, out ignored))
                    flags |= AirlockCycleFlags.AtomicAbort;
                if (!TryAtomicBlendFloat(FloatPtr(ref cell.CarbonDioxide01), targetCarbonDioxide, mixFactor, 0f, 1f, out ignored))
                    flags |= AirlockCycleFlags.AtomicAbort;
                float oxygen = AirlockPressurizationMath.FiniteOr(AtomicReadFloat(FloatPtr(ref cell.Oxygen01)), AtmosphereLogisticsConstants.DefaultOxygen01);
                float carbonDioxide = AirlockPressurizationMath.FiniteOr(AtomicReadFloat(FloatPtr(ref cell.CarbonDioxide01)), AtmosphereLogisticsConstants.DefaultCarbonDioxide01);
                float toxin = AirlockPressurizationMath.FiniteOr(AtomicReadFloat(FloatPtr(ref cell.Toxin01)), 0f);
                float nitrogen = math.saturate(1f - oxygen - carbonDioxide - math.max(0f, toxin));
                if (!TryAtomicBlendFloat(FloatPtr(ref cell.Nitrogen01), nitrogen, 1f, 0f, 1f, out ignored))
                    flags |= AirlockCycleFlags.AtomicAbort;
                if ((flags & AirlockCycleFlags.AtomicAbort) != 0u &&
                    !TryAtomicOrUInt(UIntPtr(ref cell.Flags), AtmosphereFaultFlags.NonFiniteGas))
                {
                    flags |= AirlockCycleFlags.AtomicAbort;
                }
            }

            if (flags != 0u)
            {
                airlock.CycleStateFlags |= flags;
                if (Results != null && index < ResultCount)
                {
                    ref AirlockEvaluationResultDTO result = ref UnsafeUtility.AsRef<AirlockEvaluationResultDTO>(Results + index);
                    result.Flags |= flags;
                }
            }
        }

        private static float* FloatPtr(ref float value)
        {
            return (float*)UnsafeUtility.AddressOf(ref value);
        }

        private static uint* UIntPtr(ref uint value)
        {
            return (uint*)UnsafeUtility.AddressOf(ref value);
        }

        private static bool TryAtomicBlendFloat(float* target, float blendTarget, float factor, float min, float max, out float applied)
        {
            applied = 0f;
            if (!math.isfinite(blendTarget) || !math.isfinite(min) || !math.isfinite(max))
                return false;

            factor = math.saturate(factor);
            max = math.max(min, max);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int* bits = (int*)target;
                int observedBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
                float observed = math.asfloat(observedBits);
                if (!math.isfinite(observed))
                    return false;

                float next = math.clamp(observed + (blendTarget - observed) * factor, min, max);
                int nextBits = math.asint(next);
                if (Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, observedBits) == observedBits)
                {
                    applied = next - observed;
                    return true;
                }
            }

            return false;
        }

        private static bool TryAtomicAddFloat(float* target, float delta, float min, float max, out float applied)
        {
            applied = 0f;
            if (!math.isfinite(delta) || !math.isfinite(min) || !math.isfinite(max))
                return false;

            max = math.max(min, max);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int* bits = (int*)target;
                int observedBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
                float observed = math.asfloat(observedBits);
                if (!math.isfinite(observed))
                    return false;
                if (delta > 0f && observed >= max)
                {
                    applied = 0f;
                    return true;
                }
                if (delta < 0f && observed <= min)
                {
                    applied = 0f;
                    return true;
                }

                float next = math.clamp(observed + delta, min, max);
                int nextBits = math.asint(next);
                if (Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, observedBits) == observedBits)
                {
                    applied = next - observed;
                    return true;
                }
            }

            return false;
        }

        private static float AtomicReadFloat(float* target)
        {
            int* bits = (int*)target;
            int observedBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
            return math.asfloat(observedBits);
        }

        private static bool TryAtomicMaxFloat(float* target, float candidate)
        {
            if (!math.isfinite(candidate))
                return false;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                int* bits = (int*)target;
                int observedBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
                float observed = math.asfloat(observedBits);
                if (!math.isfinite(observed))
                    return false;
                if (candidate <= observed)
                    return true;

                int nextBits = math.asint(candidate);
                if (Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, observedBits) == observedBits)
                    return true;
            }

            return false;
        }

        private static bool TryAtomicOrUInt(uint* target, uint mask)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int* bits = (int*)target;
                int observedBits = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), 0, 0);
                int nextBits = observedBits | unchecked((int)mask);
                if (Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(bits), nextBits, observedBits) == observedBits)
                    return true;
            }

            return false;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordAirlockTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AirlockStateDTO> Airlocks;
        [ReadOnly, NoAlias] public NativeArray<AirlockEvaluationResultDTO> Results;
        [NoAlias] public NativeArray<AirlockTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [NoAlias] public NativeArray<int> DumpRequested;
        public uint Frame;
        public uint SolverWallMicroseconds;
        public float TickIntervalSeconds;

        public void Execute()
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            AirlockTelemetryEntry entry = default;
            entry.Frame = Frame;
            entry.SolverWallMicroseconds = SolverWallMicroseconds;
            entry.TickIntervalSeconds = TickIntervalSeconds;
            entry.MinPressureAtm = float.MaxValue;
            uint hash = 2166136261u;

            int count = math.min(Airlocks.IsCreated ? Airlocks.Length : 0, Results.IsCreated ? Results.Length : 0);
            for (int i = 0; i < count; i++)
            {
                AirlockStateDTO state = Airlocks[i];
                AirlockEvaluationResultDTO result = Results[i];
                bool active = (state.CycleStateFlags & (AirlockCycleFlags.Pumping | AirlockCycleFlags.Equalizing | AirlockCycleFlags.InnerOpen | AirlockCycleFlags.OuterOpen)) != 0u;
                entry.ActiveCycles += active ? 1u : 0u;
                entry.TotalWaterDisplacedLiters += result.PumpMovedLiters + result.TorricelliMovedLiters + result.WaterDeltaLiters;
                entry.MaxPressureDeltaAtm = math.max(entry.MaxPressureDeltaAtm, result.PressureDeltaAtm);
                entry.MaxWaterVolumeLiters = math.max(entry.MaxWaterVolumeLiters, state.CurrentWaterVolumeLiters);
                entry.MinPressureAtm = math.min(entry.MinPressureAtm, state.CurrentPressureATM);
                entry.CollisionBlockedCount += (state.CycleStateFlags & AirlockCycleFlags.CollisionBlocked) != 0u ? 1u : 0u;
                entry.ForcedFloodEvents += (state.CycleStateFlags & AirlockCycleFlags.CatastrophicFlood) != 0u ? 1u : 0u;
                entry.VfxSignals += result.VfxIntensity01 > 0.2f ? 1u : 0u;
                entry.AcousticSignals += (state.CycleStateFlags & AirlockCycleFlags.AcousticPump) != 0u ? 1u : 0u;
                entry.NonFiniteCount += (state.CycleStateFlags & AirlockCycleFlags.NonFinite) != 0u ? 1u : 0u;
                hash = AirlockPressurizationMath.Hash(hash, result.StateHash);
            }

            if (entry.MinPressureAtm == float.MaxValue)
                entry.MinPressureAtm = 0f;

            entry.LastStateHash = hash;
            if (entry.NonFiniteCount != 0u)
                entry.Flags |= AirlockCycleFlags.NonFinite;
            if (SolverWallMicroseconds > 200u)
                entry.Flags |= 1u << 30;

            int capacity = math.min(AirlockPressurizationConstants.TelemetryFrameCount, Telemetry.Length);
            int cursor = TelemetryCursor[0];
            int index = capacity > 0 ? cursor % capacity : 0;
            Telemetry[index] = entry;
            TelemetryCursor[0] = cursor + 1;

            if (DumpRequested.IsCreated && DumpRequested.Length > 0 && (entry.Flags & (AirlockCycleFlags.NonFinite | (1u << 30))) != 0u)
                DumpRequested[0] = 1;
        }
    }
}
