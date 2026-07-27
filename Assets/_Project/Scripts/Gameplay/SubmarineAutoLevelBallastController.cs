using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Interaction;
using Hecton8.Physics.Vehicles;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using SubmarineFluidDynamics = global::Hecton8.Physics.SubmarineFluidDynamics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineCoreDirector))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Auto-Level Ballast Controller")]
    public sealed class SubmarineAutoLevelBallastController : MonoBehaviour,
        IFixedTickable,
        IPostFixedTickable,
        ISlowTickable,
        ILateFrameTickable,
        IOriginShiftListener,
        IVehicleCommandSignalListener,
        IDamageReceiver,
        ICombatPushbackBodySource,
        ICombatHitProfileSource,
        IGlobalRegistryHotSwapListener,
        ISubmarineState
    {
        private static int s_x001SubmarineAutoLevelBallastControllerSignalPushDropCount;
        private const float DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct PidJobOutput
        {
            [FieldOffset(0)] public float3 TorqueWorld;
            [FieldOffset(12)] public float3 MaelstromAcceleration;
            [FieldOffset(24)] public float3 Integral;
            [FieldOffset(36)] public float3 Error;
            [FieldOffset(48)] public float3 Derivative;
            [FieldOffset(60)] public float IntegralWindup;
            [FieldOffset(64)] public uint Flags;
            [FieldOffset(68)] private uint _pad0;
            [FieldOffset(72)] private ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct SubmarinePidTelemetryEntry
        {
            [FieldOffset(0)] public int Frame;
            [FieldOffset(4)] public uint StateHash;
            [FieldOffset(8)] public uint Flags;
            [FieldOffset(12)] public float IntegralWindup;
            [FieldOffset(16)] public float SystemStress01;
            [FieldOffset(20)] public float3 RuntimePosition;
            [FieldOffset(32)] public float3 LinearVelocity;
            [FieldOffset(44)] public float3 AngularVelocity;
            [FieldOffset(56)] public float3 CenterOfMassLocal;
            [FieldOffset(68)] public float3 DynamicFloodComOffsetLocal;
            [FieldOffset(80)] public float3 DynamicFloodInertiaTensorMultiplier;
            [FieldOffset(92)] public float3 PidError;
            [FieldOffset(104)] public float BallastWaterMassKg;
            [FieldOffset(108)] public float DynamicFloodWaterMassKg;
            [FieldOffset(112)] public float DynamicFloodAngularDragMultiplier;
            [FieldOffset(116)] public byte CriticalFloodActive;
            [FieldOffset(117)] public byte LastVaultFaultCode;
            [FieldOffset(118)] private ushort _pad0;
            [FieldOffset(120)] public uint LastVaultFaultBufferId;
            [FieldOffset(124)] public uint LastVaultFaultFrame;
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct DynamicFloodMassOutput
        {
            [FieldOffset(0)] public float3 DynamicCenterOfMassLocal;
            [FieldOffset(12)] public float3 DynamicCenterOfMassOffsetLocal;
            [FieldOffset(24)] public float3 InertiaTensorMultiplier;
            [FieldOffset(40)] public double3 GlobalPivotAnchor;
            [FieldOffset(64)] public float TotalWaterMassKg;
            [FieldOffset(68)] public float AngularDragMultiplier;
            [FieldOffset(72)] public uint Flags;
            [FieldOffset(76)] private uint _pad0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct SubmarineAutoLevelPidJob : IJob
        {
            public quaternion CurrentRotation;
            public float3 AngularVelocityWorld;
            public float3 PreviousError;
            public float3 PreviousIntegral;
            public float DeltaTime;
            public float Kp;
            public float Ki;
            public float Kd;
            public float IntegralClamp;
            public float MaxTorque;
            public float MaelstromAccelerationClamp;
            public float SystemStress01;
            public float3 PositionWS;
            public float3 DynamicFloodCenterOfMassOffsetLocal;
            public float FloodPitchBiasPerMeter;
            public byte ResetIntegral;
            public byte CriticalFloodActive;
            public byte MaelstromApproximationTier;
            public int ActiveMaelstromCount;
            public WhirlpoolFlow ActiveMaelstrom0;
            public WhirlpoolFlow ActiveMaelstrom1;
            [NoAlias] public NativeArray<PidJobOutput> Output;

            public void Execute()
            {
                const float Epsilon = 0.000001f;
                float safeDeltaTime = math.max(DeltaTime, 0.0001f);
                float3 targetUp = new float3(0f, 1f, 0f);
                float3 currentUp = math.mul(CurrentRotation, targetUp);
                float currentUpLengthSq = math.lengthsq(currentUp);
                currentUp = currentUpLengthSq > Epsilon
                    ? currentUp * math.rsqrt(math.max(currentUpLengthSq, Epsilon))
                    : targetUp;
                float dot = math.clamp(math.dot(currentUp, targetUp), -1f, 1f);
                float3 errorAxis = math.cross(currentUp, targetUp);
                if (math.lengthsq(errorAxis) <= Epsilon && dot < 0f)
                {
                    float3 fallbackAxis = math.mul(CurrentRotation, new float3(1f, 0f, 0f));
                    float fallbackAxisLengthSq = math.lengthsq(fallbackAxis);
                    errorAxis = fallbackAxisLengthSq > Epsilon
                        ? fallbackAxis * math.rsqrt(math.max(fallbackAxisLengthSq, Epsilon))
                        : new float3(1f, 0f, 0f);
                }

                float3 error = errorAxis * (1f + math.saturate(1f - dot));
                if (CriticalFloodActive != 0)
                {
                    Output[0] = new PidJobOutput
                    {
                        TorqueWorld = float3.zero,
                        MaelstromAcceleration = float3.zero,
                        Integral = float3.zero,
                        Error = float3.zero,
                        Derivative = float3.zero,
                        IntegralWindup = 0f,
                        Flags = PidTelemetryFlagCriticalFlood
                    };
                    return;
                }

                float floodPitchBias = math.clamp(
                    -DynamicFloodCenterOfMassOffsetLocal.z * math.max(0f, FloodPitchBiasPerMeter),
                    -1f,
                    1f);
                if (math.abs(floodPitchBias) > 0.0001f)
                {
                    float3 pitchAxisWorld = math.mul(CurrentRotation, new float3(1f, 0f, 0f));
                    float pitchAxisLengthSq = math.lengthsq(pitchAxisWorld);
                    pitchAxisWorld = pitchAxisLengthSq > Epsilon
                        ? pitchAxisWorld * math.rsqrt(math.max(pitchAxisLengthSq, Epsilon))
                        : new float3(1f, 0f, 0f);
                    error += pitchAxisWorld * floodPitchBias;
                }

                float3 integral = ResetIntegral != 0
                    ? float3.zero
                    : PreviousIntegral + (error * safeDeltaTime);
                float clamp = math.max(0f, IntegralClamp);
                integral = math.clamp(integral, new float3(-clamp), new float3(clamp));

                float3 derivative = ResetIntegral != 0
                    ? float3.zero
                    : (error - PreviousError) * math.rcp(safeDeltaTime);
                float3 dampedDerivative = derivative - AngularVelocityWorld;
                bool disableDerivative = math.saturate(SystemStress01) > SystemStressDerivativeCutoff;
                float effectiveKd = disableDerivative ? 0f : math.max(0f, Kd);
                float3 torque = (error * math.max(0f, Kp)) +
                                (integral * math.max(0f, Ki)) +
                                (dampedDerivative * effectiveKd);

                float maxTorque = math.max(0f, MaxTorque);
                float torqueLengthSq = math.lengthsq(torque);
                if (torqueLengthSq > maxTorque * maxTorque && torqueLengthSq > Epsilon)
                    torque *= maxTorque * math.rsqrt(math.max(torqueLengthSq, Epsilon));

                float3 maelstromAcceleration = float3.zero;
                if (ActiveMaelstromCount > 0)
                {
                    maelstromAcceleration += FluidAnalyticalContractMath.SampleWhirlpoolVelocity(
                        PositionWS,
                        ActiveMaelstrom0,
                        MaelstromApproximationTier,
                        MaelstromAccelerationClamp);
                }

                if (ActiveMaelstromCount > 1)
                {
                    maelstromAcceleration += FluidAnalyticalContractMath.SampleWhirlpoolVelocity(
                        PositionWS,
                        ActiveMaelstrom1,
                        MaelstromApproximationTier,
                        MaelstromAccelerationClamp);
                }

                maelstromAcceleration = FluidAnalyticalContractMath.ClampFiniteFloat3Magnitude(
                    maelstromAcceleration,
                    MaelstromAccelerationClamp);

                uint flags = 0u;
                if (disableDerivative)
                    flags |= PidTelemetryFlagDerivativeDisabled;

                if (!math.all(math.isfinite(error)) ||
                    !math.all(math.isfinite(integral)) ||
                    !math.all(math.isfinite(derivative)) ||
                    !math.all(math.isfinite(torque)) ||
                    !math.all(math.isfinite(maelstromAcceleration)))
                {
                    flags |= PidTelemetryFlagInvalidOutput;
                    torque = float3.zero;
                    maelstromAcceleration = float3.zero;
                    integral = float3.zero;
                    error = float3.zero;
                    derivative = float3.zero;
                }

                float integralLengthSq = math.lengthsq(integral);
                float integralWindup = integralLengthSq > Epsilon
                    ? integralLengthSq * math.rsqrt(math.max(integralLengthSq, Epsilon))
                    : 0f;
                Output[0] = new PidJobOutput
                {
                    TorqueWorld = torque,
                    MaelstromAcceleration = maelstromAcceleration,
                    Integral = integral,
                    Error = error,
                    Derivative = derivative,
                    IntegralWindup = integralWindup,
                    Flags = flags
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct SubmarineMassSolverJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<float>.ReadOnly RoomWaterLevels;
            [ReadOnly, NoAlias] public NativeArray<float>.ReadOnly RoomVolumes;
            [ReadOnly, NoAlias] public NativeArray<float3>.ReadOnly RoomLocalAUPs;
            [NoAlias] public NativeArray<DynamicFloodMassOutput> Output;
            public int RoomCount;
            public float BaseMassKg;
            public float3 BaseCenterOfMassLocal;
            public double3 GlobalPivotAnchor;

            public void Execute()
            {
                const float Epsilon = 0.000001f;
                float baseMass = math.max(MinimumMassForReciprocal, BaseMassKg);
                double3 pivotAnchor = GlobalPivotAnchor;
                bool invalidPivot = !math.all(math.isfinite(pivotAnchor));
                if (invalidPivot)
                    pivotAnchor = double3.zero;

                double3 weightedWaterSum = double3.zero;
                float totalWaterMass = 0f;
                uint flags = invalidPivot ? PidTelemetryFlagFloodInvalid : 0u;
                int count = math.min(RoomCount, math.min(RoomWaterLevels.Length, math.min(RoomVolumes.Length, RoomLocalAUPs.Length)));

                for (int i = 0; i < count; i++)
                {
                    float waterLevel01 = math.saturate(RoomWaterLevels[i]);
                    float roomVolumeM3 = math.max(0f, RoomVolumes[i]);
                    float waterMass = waterLevel01 * roomVolumeM3 * WaterDensityKgPerCubicMeter;
                    float3 roomLocal = RoomLocalAUPs[i];
                    if (!math.isfinite(waterMass) || !math.all(math.isfinite(roomLocal)))
                    {
                        flags |= PidTelemetryFlagFloodInvalid;
                        continue;
                    }

                    totalWaterMass += waterMass;
                    double3 roomAbsolute = pivotAnchor + new double3(roomLocal.x, roomLocal.y, roomLocal.z);
                    if (!math.all(math.isfinite(roomAbsolute)))
                    {
                        flags |= PidTelemetryFlagFloodInvalid;
                        continue;
                    }

                    weightedWaterSum += roomAbsolute * waterMass;
                }

                float3 floodCenter = BaseCenterOfMassLocal;
                if (totalWaterMass > Epsilon)
                {
                    double inverseWaterMass = math.rcp(math.max((double)MinimumMassForReciprocal, (double)totalWaterMass));
                    double3 floodCenterAbsolute = weightedWaterSum * inverseWaterMass;
                    double3 floodCenterLocal = floodCenterAbsolute - pivotAnchor;
                    floodCenter = new float3((float)floodCenterLocal.x, (float)floodCenterLocal.y, (float)floodCenterLocal.z);
                }

                float combinedMass = baseMass + totalWaterMass;
                float3 dynamicCenter = ((BaseCenterOfMassLocal * baseMass) + (floodCenter * totalWaterMass)) *
                                       math.rcp(math.max(MinimumMassForReciprocal, combinedMass));
                float3 offset = dynamicCenter - BaseCenterOfMassLocal;
                float angularDragMultiplier = 1f + (totalWaterMass * math.rcp(math.max(MinimumMassForReciprocal, baseMass)));
                float floodMassRatio = totalWaterMass * math.rcp(math.max(MinimumMassForReciprocal, baseMass));
                float3 absOffset = math.abs(offset);
                float3 inertiaTensorMultiplier = new float3(
                    1f + floodMassRatio * (0.75f + absOffset.z),
                    1f + floodMassRatio * (0.50f + absOffset.x),
                    1f + floodMassRatio * (0.75f + absOffset.y));
                if (totalWaterMass > Epsilon)
                    flags |= PidTelemetryFlagFloodSignal;
                if (totalWaterMass > baseMass * CriticalFloodMassBaseRatio)
                    flags |= PidTelemetryFlagCriticalFlood;
                if (!math.all(math.isfinite(dynamicCenter)) ||
                    !math.all(math.isfinite(offset)) ||
                    !math.all(math.isfinite(inertiaTensorMultiplier)) ||
                    !math.isfinite(totalWaterMass) ||
                    !math.isfinite(angularDragMultiplier))
                {
                    flags |= PidTelemetryFlagFloodInvalid;
                    dynamicCenter = BaseCenterOfMassLocal;
                    offset = float3.zero;
                    inertiaTensorMultiplier = new float3(1f);
                    totalWaterMass = 0f;
                    angularDragMultiplier = 1f;
                }

                Output[0] = new DynamicFloodMassOutput
                {
                    DynamicCenterOfMassLocal = dynamicCenter,
                    DynamicCenterOfMassOffsetLocal = offset,
                    InertiaTensorMultiplier = math.max(new float3(1f), inertiaTensorMultiplier),
                    GlobalPivotAnchor = pivotAnchor,
                    TotalWaterMassKg = math.max(0f, totalWaterMass),
                    AngularDragMultiplier = math.max(1f, angularDragMultiplier),
                    Flags = flags
                };
            }
        }

        private const int TankCount = 4;
        private const int TankFront = 0;
        private const int TankAft = 1;
        private const int TankPort = 2;
        private const int TankStarboard = 3;
        private const int TelemetryCapacity = 300;
        private const uint PidTelemetryFlagInvalidOutput = 1u << 0;
        private const uint PidTelemetryFlagImpactReset = 1u << 1;
        private const uint PidTelemetryFlagOriginShiftReset = 1u << 2;
        private const uint PidTelemetryFlagPumpDenied = 1u << 3;
        private const uint PidTelemetryFlagFloodSignal = 1u << 4;
        private const uint PidTelemetryFlagCriticalFlood = 1u << 5;
        private const uint PidTelemetryFlagFloodInvalid = 1u << 6;
        private const uint PidTelemetryFlagCriticalList = 1u << 7;
        private const uint PidTelemetryFlagDerivativeDisabled = 1u << 8;
        private const uint PidTelemetryFlagBubbleSignal = 1u << 9;
        private const uint PidTelemetryFlagDataVaultMissing = 1u << 10;
        private const uint PidTelemetryFlagFluidImpulseSignal = 1u << 11;
        private const uint PidTelemetryFlagBallastPressureBlocked = 1u << 12;
        private const uint PidTelemetryFlagBallastInvalid = 1u << 13;
        private const uint PidTelemetryFlagVaultWriteContention = 1u << 14;
        private const uint PidTelemetryFlagVaultViewInvalid = 1u << 15;
        private const uint PidTelemetryDumpFaultMask =
            PidTelemetryFlagInvalidOutput |
            PidTelemetryFlagFloodInvalid |
            PidTelemetryFlagDataVaultMissing |
            PidTelemetryFlagBallastInvalid |
            PidTelemetryFlagVaultWriteContention |
            PidTelemetryFlagVaultViewInvalid;
        private const uint PidTelemetryPidOutputForceBlockMask =
            PidTelemetryFlagInvalidOutput |
            PidTelemetryFlagCriticalFlood;
        private const byte VaultFaultCodeMissing = 1;
        private const byte VaultFaultCodeContention = 2;
        private const byte VaultFaultCodeInvalidView = 3;
        private const string BallastPidDumpRelativePath = "Docs/AgentLogs/Dump_1420_SubmarineNavigation.bin";
        private const string BallastBuoyancyDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_333.bin";
        private const float WaterDensityKgPerCubicMeter = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        // Densest authored brine layer sits near 1.3x sea water; the ratio bounds a hot-swapped or
        // malformed density provider instead of trusting it to stay in range.
        private const float MaximumAmbientDensityRatio = 1.35f;
        private const float DefaultBallastHullHeightMeters = 4f;
        private const float DefaultBallastAirPressureATM = 24f;
        private const float DefaultBallastHullVolumeMassScalar = 1.12f;
        private const float MaelstromAccelerationClamp = 12f;
        private const float CriticalFloodMassBaseRatio = 0.4f;
        private const float FloodSolveCadenceSeconds = 0.5f;
        private const float FloodSignalTimeoutSeconds = 3f;
        private const float MinimumMassForReciprocal = 0.01f;
        private const float SystemStressDerivativeCutoff = 0.8f;
        private const int VaultBallastFillFlag = 1 << 0;
        private const int VaultTankLocalPositionsFlag = 1 << 1;
        private const int VaultPidOutputFlag = 1 << 2;
        private const int VaultFloodMassOutputFlag = 1 << 3;
        private const int VaultTelemetryFlag = 1 << 4;
        private const int VaultBallastTanksFlag = 1 << 5;
        private const int VaultBallastCommandsFlag = 1 << 6;
        private const int VaultBallastFluidSamplesFlag = 1 << 7;
        private const int VaultBallastForcePacketsFlag = 1 << 8;
        private const int VaultBallastTelemetryFlag = 1 << 9;
        private const int VaultBallastTuningFlag = 1 << 10;
        private const int VaultVesselTelemetryFlag = 1 << 11;
        private static readonly ulong BallastSolverMutationGuardMask =
            BallastMutationGuardBit(SubmarineBallastBufferIds.Tanks) |
            BallastMutationGuardBit(SubmarineBallastBufferIds.Commands) |
            BallastMutationGuardBit(SubmarineBallastBufferIds.FluidSamples) |
            BallastMutationGuardBit(SubmarineBallastBufferIds.ForcePackets) |
            BallastMutationGuardBit(SubmarineBallastBufferIds.TelemetryRing) |
            BallastMutationGuardBit(SubmarineBallastBufferIds.VesselTelemetry);
        private static readonly ulong FloodRoomInputMutationGuardMask =
            BallastMutationGuardBit(BufferID.RoomWaterLevels) |
            BallastMutationGuardBit(BufferID.RoomVolumes) |
            BallastMutationGuardBit(BufferID.RoomLocalAUPs);
#if UNITY_EDITOR
        private const long MaxBallastProfileCsvBytes = SubmarineBallastConstants.CsvImportByteCapacity;
#endif
        private const SystemID OwnerSystem = SystemID.VehiclesPhysics;
        private const uint FloodFeedbackSourceHash = 0x56434d53u;
        private const uint EngineVentBubbleSourceHash = 0x42414c32u;
        private const uint EngineVentFluidImpulseFlag = 1u << 0;
        private const uint TailHeavyFluidImpulseFlag = 1u << 1;

        [Header("Auto-Level")]
        [SerializeField] private bool autoLevelEnabled = true;
        [SerializeField, Min(0f)] private float proportionalGain = 42000f;
        [SerializeField, Min(0f)] private float integralGain = 2400f;
        [SerializeField, Min(0f)] private float derivativeGain = 15000f;
        [SerializeField, Min(0f)] private float integralClamp = 0.35f;
        [SerializeField, Min(0f)] private float maxTorqueNewtons = 90000f;

        [Header("Ballast")]
        [SerializeField, Range(0f, 1f)] private float neutralBallastFill01 = 0.38f;
        [SerializeField, Min(0.01f)] private float ballastTankVolumeCubicMeters = 0.85f;
        [SerializeField, Min(0f)] private float pumpFillRate01PerSecond = 0.18f;
        [SerializeField, Min(0f)] private float ballastBlowRate01PerSecond = 0.45f;
        [SerializeField, Min(0f)] private float pumpEnergyWattSecondsPerFill01 = 320f;
        [SerializeField, Range(0f, 0.45f)] private float maxCommandBallastBias01 = 0.22f;
        [SerializeField, Min(0f)] private float airReleaseAudioFillDeltaThreshold = 0.035f;
        [SerializeField, Min(0.1f)] private float ballastHullVolumeCubicMeters = 18f;
        [SerializeField, Min(0.1f)] private float ballastHullHeightMeters = DefaultBallastHullHeightMeters;
        [SerializeField, Min(1f)] private float airBankPressureATM = DefaultBallastAirPressureATM;
        [SerializeField] private bool useEmergencyMockWaveSampler;

        [Header("Mass Layout")]
        [SerializeField] private Vector3 baseCenterOfMassLocal = Vector3.zero;
        [SerializeField] private Vector3 frontTankLocalPosition = new Vector3(0f, -0.35f, 2.4f);
        [SerializeField] private Vector3 aftTankLocalPosition = new Vector3(0f, -0.35f, -2.4f);
        [SerializeField] private Vector3 portTankLocalPosition = new Vector3(-1.1f, -0.35f, 0f);
        [SerializeField] private Vector3 starboardTankLocalPosition = new Vector3(1.1f, -0.35f, 0f);

        [Header("Dynamic Flooding")]
        [SerializeField, Min(0f)] private float floodPidPitchBiasPerMeter = 0.45f;
        [SerializeField, Min(0f)] private float floodComStressAudioThresholdMeters = 0.18f;
        [SerializeField, Min(0f)] private float floodAngularDampingFloor = 0.05f;
        [SerializeField, Min(0.05f)] private float floodStressAudioCooldownSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float pidHullStressAudioCooldownSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float criticalFloodHapticCooldownSeconds = 0.25f;
        [SerializeField, Range(0f, 89f)] private float criticalFloodPitchDegrees = 30f;
        [SerializeField, Range(0f, 89f)] private float tailHeavyBubblePitchDegrees = 20f;
        [SerializeField, Min(0.05f)] private float tailHeavyBubbleCooldownSeconds = 0.25f;
        [SerializeField] private Vector3 engineVentLocalPosition = new Vector3(0f, -0.35f, -2.8f);
        [SerializeField, Range(0.05f, 1f)] private float pidTorqueFastNlerp01 = 0.45f;

        [Header("Combat Recovery")]
        [SerializeField, Min(0f)] private float combatTargetHealth = 250f;
        [SerializeField, Min(0f)] private float massiveImpactDamageThreshold = 35f;
        [SerializeField, Min(0f)] private float combatArmorValue = 8f;

        private SubmarineCoreDirector _core;
        private IPowerGridService _powerGrid;
        private IAudioService _audio;
        private IPhysicsService _physicsService;
        private IDataVault _dataVault;
        private IAnalyticalFlowReadModel _analyticalFlowReadModel;
        private IBrineFluidDensityReadModel _brineDensityReadModel;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private Rigidbody _hull;
        private Transform _cachedTransform;
        private SubmarineStateSnapshot _snapshot;
        private VehicleCommandSignal _pendingCommand;
        private bool _commandDirty;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _registeredCombatTarget;
        private bool _interactionTargetRegistered;
        private bool _registeredState;
        private bool _pidJobPending;
        private bool _floodMassJobPending;
        private bool _ballastSolverJobPending;
        private bool _pidOutputVaultLockHeld;
        private bool _floodMassOutputVaultLockHeld;
        private bool _ballastSolverVaultLocksHeld;
        private bool _floodMassSolveRequested;
        private bool _resetIntegralPending;
        private bool _dumpedTelemetry;
        private bool _dumpedBallastTelemetry;
        private bool _pendingFloodStressAcousticDirty;
        private AcousticPingSignal _pendingFloodStressAcoustic;
        private bool _pendingCriticalFloodHapticDirty;
        private HapticRequest _pendingCriticalFloodHaptic;
        private bool _pendingTailHeavyBubbleDirty;
        private BubbleSpawnSignal _pendingTailHeavyBubble;
        private bool _pendingTailHeavyFluidImpulseDirty;
        private FluidImpulseSignal _pendingTailHeavyFluidImpulse;
        private bool _pendingAirReleaseAudioDirty;
        private ProceduralAudioPingRequest _pendingAirReleaseAudio;
        private bool _pendingPidHullStressSignalDirty;
        private HullStressSignal _pendingPidHullStressSignal;
        private IDataVault _ballastSolverGuardVault;
        private IDataVault _floodRoomInputGuardVault;
        private byte _pumpPowered = 1;
        private byte _authoritativeMathLod;
        private int _targetInstanceId;
        private int _fallbackInstanceId;
        private int _tickCount;
        private int _telemetryCursor;
        private int _ballastProfileRows;
        private int _ballastActiveSampleBudget;
        private float _baseMassKg = 1200f;

        private struct ProceduralAudioPingRequest
        {
            public Vector3 Position;
            public float Intensity01;
            public float DurationSeconds;
            public float Transmission01;
            public float PitchCarrierHz;
            public ProceduralAudioPingKind Kind;
        }
        private float _baseAngularDamping;
        private float _ballastWaterMassKg;
        private float _floodSolveAccumulator;
        private float _floodStressAudioCooldown;
        private float _pidHullStressAudioCooldown;
        private float _criticalFloodHapticCooldown;
        private float _tailHeavyBubbleCooldown;
        private float _criticalListCooldown;
        private float _lastIntegralWindup;
        private float _airReleaseCooldownSeconds;
        private float _lastBallastComputeMicros;
        private float _cachedGlobalQualityWeight = 1f;
        private long _ballastScheduleTimestamp;
        private long _ballastProfilesCsvLastWriteTicks;
        private string _ballastProfilesCsvPath;
        private bool _baseAngularDampingCached;
        private bool _baseInertiaTensorCached;
        private float3 _pidIntegral;
        private float3 _previousPidError;
        private float3 _lastPidDerivative;
        private float3 _smoothedPidTorqueWorld;
        private float3 _centerOfMassLocal;
        private float3 _dynamicFloodCenterOfMassLocal;
        private float3 _dynamicFloodComOffsetLocal;
        private float3 _dynamicFloodInertiaTensorMultiplier = new float3(1f);
        private float3 _lastAppliedInertiaTensorMultiplier = new float3(1f);
        private Vector3 _baseInertiaTensor;
        private float _dynamicFloodWaterMassKg;
        private float _dynamicFloodAngularDragMultiplier = 1f;
        private double3 _dynamicFloodGlobalPivotAnchor;
        private AbsoluteUniversePosition _cachedRuntimeOriginAup;
        private float _systemStress01;
        private uint _lastFloodSignalFrame;
        private float _floodSignalAgeSeconds;
        private int _dynamicFloodRoomCount;
        private byte _hasFloodSignalFrame;
        private byte _dynamicFloodSignalActive;
        private byte _criticalFloodActive;
        private byte _shinobu332GyroRouteActive;
        private byte _runtimeOriginAupCached;
        private byte _ballastProfilesCsvLoaded;
        private byte _dataMonolithStaticPayloadPresent;
        private int _vaultNativeStateMask;
        private uint _pendingTelemetryFlags;
        private uint _lastVaultFaultBufferId;
        private uint _lastVaultFaultFrame;
        private byte _lastVaultFaultCode;
        private JobHandle _pidHandle;
        private JobHandle _floodMassHandle;
        private JobHandle _ballastSolverHandle;

        private VaultGenerationHandle<float> _ballastFill01Handle;
        private VaultGenerationHandle<float3> _tankLocalPositionsHandle;
        private VaultGenerationHandle<PidJobOutput> _pidOutputHandle;
        private VaultGenerationHandle<DynamicFloodMassOutput> _floodMassOutputHandle;
        private VaultGenerationHandle<SubmarinePidTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<BallastTankDTO> _ballastTanksHandle;
        private VaultGenerationHandle<BallastTankCommandDTO> _ballastCommandsHandle;
        private VaultGenerationHandle<SubmarineBallastFluidSampleDTO> _ballastFluidSamplesHandle;
        private VaultGenerationHandle<SubmarineBallastForcePacketDTO> _ballastForcePacketsHandle;
        private VaultGenerationHandle<SubmarineBallastTelemetryEntry> _ballastTelemetryHandle;
        private VaultGenerationHandle<SubmarineBallastTuningDTO> _ballastTuningHandle;
        private VaultGenerationHandle<SubmarineBallastProfileDTO> _ballastProfilesHandle;
        private VaultGenerationHandle<VesselTelemetryEntry> _vesselTelemetryHandle;
        private VaultGenerationHandle<SubmarineGyroCounterDTO> _shinobu332GyroCounterHandle;
        private VaultGenerationHandle<float> _roomWaterLevelsHandle;
        private VaultGenerationHandle<float> _roomVolumesHandle;
        private VaultGenerationHandle<float3> _roomLocalAUPsHandle;

        public bool SuppressesKinematicPitch => isActiveAndEnabled && autoLevelEnabled && _shinobu332GyroRouteActive == 0;

        public int TickCount => _tickCount;

        public Rigidbody CombatPushbackBody => _hull;

        public Vector3 CombatForward => _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;

        public float CombatHeight => 2.8f;

        public NativeArray<float>.ReadOnly BallastFill01 =>
            TryReadBallastFillReadOnly(out NativeArray<float>.ReadOnly ballastFill) ? ballastFill : default;

        public SubmarineStateSnapshot StateSnapshot => _snapshot;

        private void Awake()
        {
            CacheReferences();
            CacheColdBallastProfilePaths();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshTargetInstanceId();
            RefreshOwnerPhaseSnapshotsCold();
            SeedAuthoritativeMathLod();
        }

        private void OnEnable()
        {
            CacheReferences();
            CacheColdBallastProfilePaths();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshTargetInstanceId();
            RefreshOwnerPhaseSnapshotsCold();
            SeedAuthoritativeMathLod();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            CompleteBallastSolverJob(forceComplete: true, applyForces: false);
            CompleteFloodMassJob(forceComplete: true, commitOutput: false);
            CompletePidJob(forceComplete: true, commitOutput: false);
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            CompleteBallastSolverJob(forceComplete: true, applyForces: false);
            CompleteFloodMassJob(forceComplete: true, commitOutput: false);
            CompletePidJob(forceComplete: true, commitOutput: false);
            DisposeNativeState();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            _tickCount++;
            if (_hull == null || fixedDeltaTime <= 0f)
                return;

            VehicleCommandSignalBus.FlushPending();
            ConsumeFloodStateSignals();
            ConsumeSystemHealthSignals();
            ExpireStaleDynamicFloodState(fixedDeltaTime);
            VehicleCommandSignal command = ConsumeCommand();
            AdvanceAirReleaseCooldown(fixedDeltaTime);
            _authoritativeMathLod = 1;
            AdvanceDynamicFloodSolver(fixedDeltaTime);
            PrepareBallastCommands(in command, fixedDeltaTime);
            ApplyMassDistribution();
            ScheduleBallastSolver(fixedDeltaTime);
            ApplyDynamicFloodDragTensor();
            EmitDynamicFloodFeedback(fixedDeltaTime);
            RefreshSnapshot();
            if (WriteTelemetry(_pendingTelemetryFlags))
                _pendingTelemetryFlags = 0u;
            SchedulePidJobAfterFloodWriteLocks(fixedDeltaTime);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            CompleteBallastSolverJob(forceComplete: false, applyForces: true);
            CompleteFloodMassJob(forceComplete: false, commitOutput: true);
            CompletePidJob(forceComplete: false, commitOutput: true);
        }

        public void SlowTick()
        {
            EnsureNativeState();
            RefreshTankPositions();
            RefreshRoomBufferAliases();
            RefreshOwnerPhaseSnapshotsCold();
            _floodMassSolveRequested = true;
        }

        public void LateFrameTick()
        {
            FlushDynamicFloodFeedback();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFinite(shiftOffset) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            _previousPidError = float3.zero;
            _lastPidDerivative = float3.zero;
            _resetIntegralPending = true;
            RefreshRuntimeOriginAupSnapshotCold();
            _pendingTelemetryFlags |= PidTelemetryFlagOriginShiftReset;
        }

        public void OnVehicleCommandSignal(in VehicleCommandSignal signal)
        {
            if (signal.TargetInstanceId == 0)
                return;

            if (signal.TargetInstanceId != _targetInstanceId &&
                signal.TargetInstanceId != _fallbackInstanceId)
            {
                return;
            }

            _pendingCommand = signal;
            _commandDirty = true;
        }

        public void ReceiveDamage(in DamagePacket packet)
        {
            if ((packet.DamageType & CombatDamageTypes.Impact) == 0u)
                return;

            if (packet.Magnitude < massiveImpactDamageThreshold)
                return;

            RequestImpactIntegralReset();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.PowerGrid)
            {
                _powerGrid = currentService as IPowerGridService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                CacheAudioService(currentService as IAudioService);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _analyticalFlowReadModel = currentService as IAnalyticalFlowReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
            {
                _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ResourceDistributionRuntime)
            {
                _brineDensityReadModel = currentService as IBrineFluidDensityReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteBallastSolverJob(forceComplete: true, applyForces: false);
                CompleteFloodMassJob(forceComplete: true, commitOutput: false);
                CompletePidJob(forceComplete: true, commitOutput: false);
                DisposeNativeState();
                _dataVault = currentService as IDataVault;
                EnsureNativeState();
                RefreshTankPositions();
                RefreshRoomBufferAliases();
                RefreshOwnerPhaseSnapshotsCold();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SubmarineState && currentService == null)
                TryRegisterStateReadModel();
        }

        private void RegisterRuntime()
        {
            _powerGrid = GlobalRegistry.PowerGrid;
            CacheAudioService(GlobalRegistry.Audio);
            _analyticalFlowReadModel = GlobalRegistry.AnalyticalFlow;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            RefreshDynamicFloodServicesFromRegistry();
            RefreshOwnerPhaseSnapshotsCold();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshRoomBufferAliases();
            SignalBus<MovementAcousticSignal>.EnsureInitialized();

            TryRegisterStateReadModel();
            SetFluidDynamicsCenterAuthority(true);

            if (!_registeredFixed && GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player))
                _registeredFixed = true;

            if (!_registeredPostFixed && GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player))
                _registeredPostFixed = true;

            if (!_registeredSlowTick && GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player))
                _registeredSlowTick = true;

            TryRegisterLateFrameTickable();

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (!_registeredHotSwap && GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwap = true;

            VehicleCommandSignalBus.Register(this);

            if (!_registeredCombatTarget && _targetInstanceId != 0)
            {
                _registeredCombatTarget = CombatDamageRuntime.RegisterTarget(
                    _targetInstanceId,
                    this,
                    combatTargetHealth,
                    combatTargetHealth,
                    CombatEntityKind.Submarine,
                    CombatArmorClass.Structure,
                    combatArmorValue,
                    0f);
            }

            TryRegisterInteractionTargetTree();
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryRegisterStateReadModel()
        {
            if (_registeredState)
                return;

            ISubmarineState registeredState = GlobalRegistry.SubmarineState;
            if (registeredState != null && !ReferenceEquals(registeredState, this))
                return;

            GlobalRegistry.RegisterSubmarineState(this);
            _registeredState = ReferenceEquals(GlobalRegistry.SubmarineState, this);
        }

        private void UnregisterRuntime()
        {
            VehicleCommandSignalBus.Unregister(this);
            TryUnregisterInteractionTargetTree();

            if (_registeredCombatTarget)
            {
                CombatDamageRuntime.UnregisterTarget(_targetInstanceId, this);
                _registeredCombatTarget = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
                _registeredPostFixed = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTick = false;
            }

            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixed = false;
            }

            if (_registeredState)
            {
                if (ReferenceEquals(GlobalRegistry.SubmarineState, this))
                    GlobalRegistry.UnregisterSubmarineState(this);

                _registeredState = false;
            }

            ClearBallastMassCoupling();
            SetFluidDynamicsCenterAuthority(false);
            _powerGrid = null;
            ClearCachedAudioService();
            _analyticalFlowReadModel = null;
            _oceanKinematicsService = null;
            ResetDynamicFloodState(clearSignalFrame: true);
            ClearPendingDynamicFloodFeedback();
            RestoreDynamicFloodAngularDrag();
            RestoreDynamicFloodInertiaTensor();
            ResetExternalFloodDragTensor();
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audio = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private void ClearCachedAudioService()
        {
            _audio = null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void ClearPendingDynamicFloodFeedback()
        {
            _pendingFloodStressAcousticDirty = false;
            _pendingFloodStressAcoustic = default;
            _pendingCriticalFloodHapticDirty = false;
            _pendingCriticalFloodHaptic = default;
            _pendingTailHeavyBubbleDirty = false;
            _pendingTailHeavyBubble = default;
            _pendingTailHeavyFluidImpulseDirty = false;
            _pendingTailHeavyFluidImpulse = default;
            _pendingAirReleaseAudioDirty = false;
            _pendingAirReleaseAudio = default;
            _pendingPidHullStressSignalDirty = false;
            _pendingPidHullStressSignal = default;
        }

        private void TryRegisterInteractionTargetTree()
        {
            if (_interactionTargetRegistered || !Application.isPlaying)
                return;

            InteractableRegistry.RegisterTree(this);
            _interactionTargetRegistered = true;
        }

        private void TryUnregisterInteractionTargetTree()
        {
            if (!_interactionTargetRegistered)
                return;

            InteractableRegistry.InvalidateTree(this);
            _interactionTargetRegistered = false;
        }

        private void CacheReferences()
        {
            _cachedTransform = transform;
            if (_core == null)
                TryGetComponent(out _core);
            if (_hull == null)
                TryGetComponent(out _hull);

            _powerGrid = GlobalRegistry.PowerGrid;
            _physicsService = GlobalRegistry.Physics;

            if (_core != null)
                _baseMassKg = math.max(1f, _core.BaseMass);
            else if (_hull != null && math.isfinite(_hull.mass))
                _baseMassKg = math.max(1f, _hull.mass);

            if (_hull != null && !_baseAngularDampingCached)
            {
                _baseAngularDamping = math.max(0f, _hull.angularDamping);
                _baseAngularDampingCached = true;
            }

            if (_hull != null && !_baseInertiaTensorCached)
            {
                Vector3 inertiaTensor = _hull.inertiaTensor;
                _baseInertiaTensor = IsFinite(inertiaTensor)
                    ? new Vector3(
                        Mathf.Max(0.001f, inertiaTensor.x),
                        Mathf.Max(0.001f, inertiaTensor.y),
                        Mathf.Max(0.001f, inertiaTensor.z))
                    : Vector3.one;
                _baseInertiaTensorCached = true;
            }
        }

        private void CacheColdBallastProfilePaths()
        {
            if (!string.IsNullOrEmpty(_ballastProfilesCsvPath))
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _ballastProfilesCsvPath = Path.Combine(projectRoot, "Data", "Physics", "vehicle_ballast_profiles.csv");
            string monolithPath = Path.Combine(projectRoot, "Assets", "StreamingAssets", "Hecton8", "DataMonolith", "static_data.h8bin");
            _dataMonolithStaticPayloadPresent = File.Exists(monolithPath) ? (byte)1 : (byte)0;
        }

        private void EnsureNativeState()
        {
            bool seedBallast = (_vaultNativeStateMask & VaultBallastFillFlag) == 0;
            if (EnsureBallastFillCold(out _) &&
                seedBallast &&
                TryAcquireVaultWrite(
                    in _ballastFill01Handle,
                    BufferID.SubmarineBallastFill01,
                    TankCount,
                    out NativeArray<float> ballastFill))
            {
                try
                {
                    for (int i = 0; i < TankCount; i++)
                        ballastFill[i] = math.saturate(neutralBallastFill01);
                }
                finally
                {
                    ReleaseVaultWrite(in _ballastFill01Handle);
                }
            }

            EnsureTankLocalPositionsCold(out _);
            EnsurePidOutputCold(out _);
            EnsureFloodMassOutputCold(out _);
            EnsureTelemetryCold(out _);
            bool seedTanks = (_vaultNativeStateMask & VaultBallastTanksFlag) == 0;
            if (EnsureBallastTanksCold(out _) &&
                seedTanks &&
                TryAcquireVaultWrite(
                    in _ballastTanksHandle,
                    SubmarineBallastBufferIds.Tanks,
                    TankCount,
                    out NativeArray<BallastTankDTO> tanks))
            {
                try
                {
                    SeedBallastTankState(tanks);
                }
                finally
                {
                    ReleaseVaultWrite(in _ballastTanksHandle);
                }
            }

            EnsureBallastCommandsCold(out _);
            EnsureBallastFluidSamplesCold(out _);
            EnsureBallastForcePacketsCold(out _);
            EnsureBallastTelemetryCold(out _);
            bool seedVesselTelemetry = (_vaultNativeStateMask & VaultVesselTelemetryFlag) == 0;
            if (EnsureVesselTelemetryCold(out _) &&
                seedVesselTelemetry &&
                TryAcquireVaultWrite(
                    in _vesselTelemetryHandle,
                    SubmarineBallastBufferIds.VesselTelemetry,
                    1,
                    out NativeArray<VesselTelemetryEntry> vesselTelemetry))
            {
                try
                {
                    vesselTelemetry[0] = new VesselTelemetryEntry
                    {
                        CurrentBallastRatio = 1f - math.saturate(neutralBallastFill01)
                    };
                }
                finally
                {
                    ReleaseVaultWrite(in _vesselTelemetryHandle);
                }
            }

            bool seedTuning = (_vaultNativeStateMask & VaultBallastTuningFlag) == 0;
            if (EnsureBallastTuningCold(out NativeArray<SubmarineBallastTuningDTO> tuning) && seedTuning && tuning.Length > 0)
                WriteBallastTuning(ResolveTankVolumeLiters());

            EnsureBallastProfilesCold(out _);
#if UNITY_EDITOR
            TryApplyBallastProfilesCsv();
#endif
        }

        private void DisposeNativeState()
        {
            ReleaseBallastSolverVaultLocks();
            ReleaseFloodMassOutputVaultLock();
            ReleaseFloodRoomInputVaultLocks();
            ReleasePidOutputVaultLock();
            ReleaseOwnedVaultHandles(_dataVault);
            _ballastFill01Handle = default;
            _tankLocalPositionsHandle = default;
            _pidOutputHandle = default;
            _floodMassOutputHandle = default;
            _telemetryHandle = default;
            _ballastTanksHandle = default;
            _ballastCommandsHandle = default;
            _ballastFluidSamplesHandle = default;
            _ballastForcePacketsHandle = default;
            _ballastTelemetryHandle = default;
            _ballastTuningHandle = default;
            _ballastProfilesHandle = default;
            _vesselTelemetryHandle = default;
            _shinobu332GyroCounterHandle = default;
            _roomWaterLevelsHandle = default;
            _roomVolumesHandle = default;
            _roomLocalAUPsHandle = default;
            _dataVault = null;
            _vaultNativeStateMask = 0;
        }

        private void ClearBallastMassCoupling()
        {
            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics != null)
                fluidDynamics.SetBallastWaterMassKilograms(0f);

            _ballastWaterMassKg = 0f;
        }

        private void SetFluidDynamicsCenterAuthority(bool enabled)
        {
            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics != null)
                fluidDynamics.SetExternalCenterOfMassAuthority(enabled);
        }

        private void RefreshDynamicFloodServicesFromRegistry()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, vault))
            {
                DisposeNativeState();
                _dataVault = vault;
            }
        }

        private void RefreshOwnerPhaseSnapshotsCold()
        {
            RefreshGlobalQualityWeightSnapshotCold();
            RefreshRuntimeOriginAupSnapshotCold();
            RefreshShinobu332GyroRouteHandleCold();
            RefreshShinobu332GyroRouteStateFromCachedVault();
        }

        private bool RefreshRoomBufferAliases()
        {
            return TryRefreshExistingReadOnlyVaultBuffer(
                    ref _roomWaterLevelsHandle,
                    BufferID.RoomWaterLevels,
                    out NativeArray<float>.ReadOnly roomWaterLevels) &&
                TryRefreshExistingReadOnlyVaultBuffer(
                    ref _roomVolumesHandle,
                    BufferID.RoomVolumes,
                    out NativeArray<float>.ReadOnly roomVolumes) &&
                TryRefreshExistingReadOnlyVaultBuffer(
                    ref _roomLocalAUPsHandle,
                    BufferID.RoomLocalAUPs,
                    out NativeArray<float3>.ReadOnly roomLocalAups) &&
                roomWaterLevels.IsCreated &&
                roomVolumes.IsCreated &&
                roomLocalAups.IsCreated;
        }

        private void RefreshTankPositions()
        {
            if (!TryAcquireVaultWrite(
                    in _tankLocalPositionsHandle,
                    BufferID.SubmarineBallastTankLocalPositions,
                    TankCount,
                    out NativeArray<float3> tankLocalPositions))
            {
                return;
            }

            try
            {
                tankLocalPositions[TankFront] = (float3)(frontTankLocalPosition);
                tankLocalPositions[TankAft] = (float3)(aftTankLocalPosition);
                tankLocalPositions[TankPort] = (float3)(portTankLocalPosition);
                tankLocalPositions[TankStarboard] = (float3)(starboardTankLocalPosition);
            }
            finally
            {
                ReleaseVaultWrite(in _tankLocalPositionsHandle);
            }
        }

        private void SeedBallastTankState(NativeArray<BallastTankDTO> tanks)
        {
            float tankVolumeLiters = ResolveTankVolumeLiters();
            float neutralLiters = math.saturate(neutralBallastFill01) * tankVolumeLiters;
            float pumpLitersPerSecond = math.max(0f, pumpFillRate01PerSecond) * tankVolumeLiters;
            int count = math.min(TankCount, tanks.Length);
            for (int i = 0; i < count; i++)
            {
                tanks[i] = new BallastTankDTO
                {
                    TankVolumeLiters = tankVolumeLiters,
                    CurrentWaterLiters = neutralLiters,
                    CompressedAirPressureATM = math.max(1f, airBankPressureATM),
                    InputStateFlags = SubmarineBallastConstants.TankFlagInitialized,
                    PumpRateLitersPerSecond = pumpLitersPerSecond
                };
            }
        }

        private void WriteBallastTuning(float tankVolumeLiters)
        {
            if (!TryAcquireVaultWrite(
                    in _ballastTuningHandle,
                    SubmarineBallastBufferIds.Tuning,
                    1,
                    out NativeArray<SubmarineBallastTuningDTO> tuning))
            {
                return;
            }

            try
            {
                tuning[0] = new SubmarineBallastTuningDTO
                {
                    HullVolumeCubicMeters = ResolveBallastHullVolume(),
                    HullHeightMeters = math.max(0.1f, ballastHullHeightMeters),
                    MaxTankLiters = math.max(0.01f, tankVolumeLiters),
                    PumpRateLitersPerSecond = math.max(0f, pumpFillRate01PerSecond) * math.max(0.01f, tankVolumeLiters),
                    BlowRateLitersPerSecond = math.max(0f, ballastBlowRate01PerSecond) * math.max(0.01f, tankVolumeLiters),
                    AirBankPressureATM = math.max(1f, airBankPressureATM),
                    FluidDensityKgPerM3 = ResolveAmbientFluidDensityKgPerM3(),
                    GlobalQualityWeight = ReadCachedGlobalQualityWeight(),
                    SourceHash = SubmarineBallastConstants.SourceHash,
                    Frame = unchecked((uint)_tickCount),
                    Flags = 0u,
                    LastNetForceY = 0f,
                    LastWaterLiters = _ballastWaterMassKg * math.rcp(math.max(1f, WaterDensityKgPerCubicMeter)) * SubmarineBallastConstants.LitersPerCubicMeter,
                    LastAmbientPressureATM = SubmarineBallastConstants.AtmosphericPressureAtm
                };
            }
            finally
            {
                ReleaseVaultWrite(in _ballastTuningHandle);
            }
        }

        private void WriteBallastTuning(float tankVolumeLiters, in SubmarineBallastForcePacketDTO packet)
        {
            if (!TryAcquireVaultWrite(
                    in _ballastTuningHandle,
                    SubmarineBallastBufferIds.Tuning,
                    1,
                    out NativeArray<SubmarineBallastTuningDTO> tuning))
            {
                return;
            }

            try
            {
                SubmarineBallastTuningDTO dto = tuning[0];
                dto.HullVolumeCubicMeters = ResolveBallastHullVolume();
                dto.HullHeightMeters = math.max(0.1f, ballastHullHeightMeters);
                dto.MaxTankLiters = math.max(0.01f, tankVolumeLiters);
                dto.PumpRateLitersPerSecond = math.max(0f, pumpFillRate01PerSecond) * math.max(0.01f, tankVolumeLiters);
                dto.BlowRateLitersPerSecond = math.max(0f, ballastBlowRate01PerSecond) * math.max(0.01f, tankVolumeLiters);
                dto.AirBankPressureATM = math.max(1f, airBankPressureATM);
                dto.FluidDensityKgPerM3 = ResolveAmbientFluidDensityKgPerM3();
                dto.GlobalQualityWeight = ReadCachedGlobalQualityWeight();
                dto.SourceHash = SubmarineBallastConstants.SourceHash;
                dto.Frame = packet.Frame;
                dto.Flags = packet.Flags;
                dto.LastNetForceY = packet.NetForce.y;
                dto.LastWaterLiters = packet.TotalWaterLiters;
                dto.LastAmbientPressureATM = packet.AmbientPressureATM;
                tuning[0] = dto;
            }
            finally
            {
                ReleaseVaultWrite(in _ballastTuningHandle);
            }
        }

#if UNITY_EDITOR
        private unsafe bool TryApplyBallastProfilesCsv()
        {
            if (_dataVault == null || string.IsNullOrEmpty(_ballastProfilesCsvPath) || !File.Exists(_ballastProfilesCsvPath))
                return false;

            try
            {
                FileInfo info = new FileInfo(_ballastProfilesCsvPath);
                long stamp = info.LastWriteTimeUtc.Ticks;
                if (_ballastProfilesCsvLoaded != 0 && stamp == _ballastProfilesCsvLastWriteTicks)
                    return false;

                if (info.Length <= 0L || info.Length > MaxBallastProfileCsvBytes)
                    return false;

                int expectedBytes = (int)info.Length;
                Span<byte> csvScratch = stackalloc byte[SubmarineBallastConstants.CsvImportByteCapacity];
                int read;
                using (FileStream stream = new FileStream(_ballastProfilesCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan))
                {
                    read = stream.Read(csvScratch.Slice(0, expectedBytes));
                }

                if (read != expectedBytes)
                    return false;

                Span<SubmarineBallastProfileDTO> profileScratch = stackalloc SubmarineBallastProfileDTO[SubmarineBallastConstants.ProfileCapacity];
                int parsed = SubmarineBallastCsvParser.ParseProfiles(csvScratch.Slice(0, read), profileScratch);
                if (parsed <= 0)
                    return false;

                if (!CommitBallastProfilesCsv(profileScratch.Slice(0, parsed), out SubmarineBallastProfileDTO primaryProfile))
                    return false;

                _ballastProfileRows = parsed;
                _ballastProfilesCsvLoaded = 1;
                _ballastProfilesCsvLastWriteTicks = stamp;
                ApplyPrimaryBallastProfile(in primaryProfile);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private bool CommitBallastProfilesCsv(
            ReadOnlySpan<SubmarineBallastProfileDTO> sourceProfiles,
            out SubmarineBallastProfileDTO primaryProfile)
        {
            primaryProfile = default;
            if (sourceProfiles.Length <= 0)
                return false;

            if (!TryAcquireVaultWrite(
                    in _ballastProfilesHandle,
                    SubmarineBallastBufferIds.Profiles,
                    SubmarineBallastConstants.ProfileCapacity,
                    out NativeArray<SubmarineBallastProfileDTO> profiles))
                return false;

            try
            {
                int count = sourceProfiles.Length < profiles.Length ? sourceProfiles.Length : profiles.Length;
                if (count <= 0)
                    return false;

                for (int i = 0; i < count; i++)
                    profiles[i] = sourceProfiles[i];

                for (int i = count; i < profiles.Length; i++)
                    profiles[i] = default;

                primaryProfile = sourceProfiles[0];
                return true;
            }
            finally
            {
                ReleaseVaultWrite(in _ballastProfilesHandle);
            }
        }
#endif

        private void ApplyPrimaryBallastProfile(in SubmarineBallastProfileDTO profile)
        {
            if ((profile.Flags & 1u) == 0u || profile.VehicleHash == 0u)
                return;

            ballastHullVolumeCubicMeters = math.max(0.1f, profile.HullVolumeCubicMeters);
            ballastHullHeightMeters = math.max(0.1f, profile.HullHeightMeters);
            ballastTankVolumeCubicMeters = math.max(0.01f, profile.TankVolumeLiters * SubmarineBallastConstants.CubicMetersPerLiter);
            pumpFillRate01PerSecond = math.max(0f, profile.PumpRateLitersPerSecond) * math.rcp(math.max(1f, profile.TankVolumeLiters));
            ballastBlowRate01PerSecond = math.max(0f, profile.BlowRateLitersPerSecond) * math.rcp(math.max(1f, profile.TankVolumeLiters));
            airBankPressureATM = math.max(1f, profile.AirBankPressureATM);
            WriteBallastTuning(math.max(1f, profile.TankVolumeLiters));
        }

        private void RefreshTargetInstanceId()
        {
            _fallbackInstanceId = unchecked((int)EntityId.ToULong(gameObject.GetEntityId()));
            _targetInstanceId = 0;

            if (_hull != null)
                _targetInstanceId = unchecked((int)EntityId.ToULong(_hull.GetEntityId()));

            if (_targetInstanceId == 0)
                _targetInstanceId = _fallbackInstanceId;
        }

        private VehicleCommandSignal ConsumeCommand()
        {
            if (!_commandDirty)
                return default;

            _commandDirty = false;
            return _pendingCommand;
        }

        private void ConsumeFloodStateSignals()
        {
            ReadOnlySpan<SubmarineFloodStateSignal> snapshot = SignalBus<SubmarineFloodStateSignal>.GetFrameSnapshot();
            if (snapshot.Length <= 0)
                return;

            uint targetBodyId = unchecked((uint)_targetInstanceId);
            uint fallbackBodyId = unchecked((uint)_fallbackInstanceId);
            for (int i = 0; i < snapshot.Length; i++)
            {
                SubmarineFloodStateSignal signal = snapshot[i];
                if (signal.SourceBodyId != 0u &&
                    signal.SourceBodyId != targetBodyId &&
                    signal.SourceBodyId != fallbackBodyId)
                {
                    continue;
                }

                if (_hasFloodSignalFrame != 0)
                {
                    if (signal.Frame == _lastFloodSignalFrame)
                        continue;

                    if (signal.Frame < _lastFloodSignalFrame && _lastFloodSignalFrame - signal.Frame < int.MaxValue)
                        continue;
                }

                CommitFloodStateSignal(in signal);
            }
        }

        private void ConsumeSystemHealthSignals()
        {
            ReadOnlySpan<SystemHealthIndexSignal> snapshot = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            if (snapshot.Length <= 0)
            {
                _systemStress01 = math.saturate(_systemStress01 * 0.95f);
                return;
            }

            float stress01 = _systemStress01 * 0.95f;
            for (int i = 0; i < snapshot.Length; i++)
            {
                SystemHealthIndexSignal signal = snapshot[i];
                float pressure = math.isfinite(signal.Pressure01) ? math.saturate(signal.Pressure01) : 0f;
                if (signal.State >= SystemHealthIndexSignal.StateCritical)
                    pressure = math.max(pressure, 1f);
                else if (signal.State >= SystemHealthIndexSignal.StateWarning)
                    pressure = math.max(pressure, 0.5f);

                if ((signal.Flags & SystemHealthIndexSignal.FlagAdrenaline) != 0)
                    pressure = math.max(pressure, 0.85f);

                stress01 = math.max(stress01, pressure);
            }

            _systemStress01 = math.saturate(stress01);
        }

        private void CommitFloodStateSignal(in SubmarineFloodStateSignal signal)
        {
            _lastFloodSignalFrame = signal.Frame;
            _hasFloodSignalFrame = 1;
            _floodSignalAgeSeconds = 0f;
            uint flags = PidTelemetryFlagFloodSignal;
            if ((signal.Flags & SubmarineFloodStateSignal.FlagInvalid) != 0)
                flags |= PidTelemetryFlagFloodInvalid;

            if (!math.all(math.isfinite(signal.DynamicCenterOfMassLocal)) ||
                !math.all(math.isfinite(signal.DynamicCenterOfMassOffsetLocal)) ||
                !math.isfinite(signal.TotalWaterMassKg) ||
                !math.isfinite(signal.AngularDragMultiplier))
            {
                flags |= PidTelemetryFlagFloodInvalid;
                _pendingTelemetryFlags |= flags;
                ResetDynamicFloodState(clearSignalFrame: false);
                DumpTelemetryOnce(flags);
                return;
            }

            _dynamicFloodCenterOfMassLocal = signal.DynamicCenterOfMassLocal;
            _dynamicFloodComOffsetLocal = signal.DynamicCenterOfMassOffsetLocal;
            _dynamicFloodWaterMassKg = math.max(0f, signal.TotalWaterMassKg);
            _dynamicFloodInertiaTensorMultiplier = ResolveInertiaTensorMultiplier(_dynamicFloodWaterMassKg, _dynamicFloodComOffsetLocal);
            _dynamicFloodGlobalPivotAnchor = ResolveGlobalPivotAnchor();
            _dynamicFloodAngularDragMultiplier = math.max(1f, signal.AngularDragMultiplier);
            _dynamicFloodRoomCount = signal.RoomCount;
            _dynamicFloodSignalActive = 1;
            float safeBaseMass = math.max(MinimumMassForReciprocal, _baseMassKg);
            bool critical = _dynamicFloodWaterMassKg > safeBaseMass * CriticalFloodMassBaseRatio ||
                            (signal.Flags & SubmarineFloodStateSignal.FlagCriticalFlood) != 0;
            _criticalFloodActive = critical ? (byte)1 : (byte)0;
            if (critical)
                flags |= PidTelemetryFlagCriticalFlood;

            _pendingTelemetryFlags |= flags;
        }

        private void ExpireStaleDynamicFloodState(float fixedDeltaTime)
        {
            if (_dynamicFloodSignalActive == 0)
                return;

            _floodSignalAgeSeconds += math.max(0f, fixedDeltaTime);
            if (_floodSignalAgeSeconds < FloodSignalTimeoutSeconds)
                return;

            _pendingTelemetryFlags |= PidTelemetryFlagFloodSignal;
            ResetDynamicFloodState(clearSignalFrame: false);
        }

        private void ResetDynamicFloodState(bool clearSignalFrame)
        {
            _dynamicFloodCenterOfMassLocal = (float3)(baseCenterOfMassLocal);
            _dynamicFloodComOffsetLocal = float3.zero;
            _dynamicFloodInertiaTensorMultiplier = new float3(1f);
            _dynamicFloodGlobalPivotAnchor = double3.zero;
            _dynamicFloodWaterMassKg = 0f;
            _dynamicFloodAngularDragMultiplier = 1f;
            _dynamicFloodRoomCount = 0;
            _dynamicFloodSignalActive = 0;
            _criticalFloodActive = 0;
            _floodSignalAgeSeconds = 0f;
            _floodMassSolveRequested = false;
            _floodSolveAccumulator = 0f;

            if (!clearSignalFrame)
                return;

            _lastFloodSignalFrame = 0u;
            _hasFloodSignalFrame = 0;
        }

        private void PrepareBallastCommands(in VehicleCommandSignal command, float fixedDeltaTime)
        {
            float vesselBallastRatio = ReadVesselBallastRatioOrNeutral();
            if (_ballastSolverJobPending ||
                !TryAcquireVaultWrite(
                    in _ballastTanksHandle,
                    SubmarineBallastBufferIds.Tanks,
                    TankCount,
                    out NativeArray<BallastTankDTO> tanks))
            {
                return;
            }

            bool wroteCommands = false;
            float writtenTankVolumeLiters = 0f;
            float currentFrontFill01 = math.saturate(neutralBallastFill01);
            float currentAftFill01 = currentFrontFill01;
            float currentPortFill01 = currentFrontFill01;
            float currentStarboardFill01 = currentFrontFill01;
            float targetFront = 0f;
            float targetAft = 0f;
            float targetPort = 0f;
            float targetStarboard = 0f;
            float tankVolumeLiters = 0f;
            float fillDelta01 = 0f;
            bool pumpPowered = false;
            bool emergencyBlow = false;
            try
            {
                float neutral = math.saturate(neutralBallastFill01);
                float pitch = math.clamp(command.Pitch, -1f, 1f);
                float vesselTargetFill01 = 1f - vesselBallastRatio;
                float vesselBias = math.clamp(vesselTargetFill01 - neutral, -maxCommandBallastBias01, maxCommandBallastBias01);
                float totalBias = math.clamp(command.BallastDelta + vesselBias, -maxCommandBallastBias01, maxCommandBallastBias01);
                float pitchBias = pitch * math.max(0f, maxCommandBallastBias01);
                emergencyBlow = (((VehicleCommandSignalFlags)command.Flags) & VehicleCommandSignalFlags.BallastBlow) != 0 ||
                                vesselBallastRatio >= 0.985f;

                targetFront = emergencyBlow ? 0f : math.saturate(neutral + totalBias + pitchBias);
                targetAft = emergencyBlow ? 0f : math.saturate(neutral + totalBias - pitchBias);
                targetPort = emergencyBlow ? 0f : math.saturate(neutral + totalBias);
                targetStarboard = emergencyBlow ? 0f : math.saturate(neutral + totalBias);
                tankVolumeLiters = ResolveTankVolumeLiters();
                currentFrontFill01 = PrepareTankForCommand(tanks, TankFront, tankVolumeLiters);
                currentAftFill01 = PrepareTankForCommand(tanks, TankAft, tankVolumeLiters);
                currentPortFill01 = PrepareTankForCommand(tanks, TankPort, tankVolumeLiters);
                currentStarboardFill01 = PrepareTankForCommand(tanks, TankStarboard, tankVolumeLiters);
                fillDelta01 = EstimateRequestedFillDelta01(
                    currentFrontFill01,
                    currentAftFill01,
                    currentPortFill01,
                    currentStarboardFill01,
                    targetFront,
                    targetAft,
                    targetPort,
                    targetStarboard,
                    fixedDeltaTime);
            }
            finally
            {
                ReleaseVaultWrite(in _ballastTanksHandle);
            }

            pumpPowered = fillDelta01 <= 0.000001f || TrySpendPumpPower(fillDelta01);
            if (!pumpPowered)
            {
                _pumpPowered = 0;
                _pendingTelemetryFlags |= PidTelemetryFlagPumpDenied;
            }
            else
            {
                _pumpPowered = 1;
            }

            if (!TryAcquireVaultWrite(
                    in _ballastCommandsHandle,
                    SubmarineBallastBufferIds.Commands,
                    TankCount,
                    out NativeArray<BallastTankCommandDTO> commands))
            {
                return;
            }

            try
            {
                WriteBallastCommand(commands, currentFrontFill01, TankFront, targetFront, tankVolumeLiters, fixedDeltaTime, pumpPowered, emergencyBlow);
                WriteBallastCommand(commands, currentAftFill01, TankAft, targetAft, tankVolumeLiters, fixedDeltaTime, pumpPowered, emergencyBlow);
                WriteBallastCommand(commands, currentPortFill01, TankPort, targetPort, tankVolumeLiters, fixedDeltaTime, pumpPowered, emergencyBlow);
                WriteBallastCommand(commands, currentStarboardFill01, TankStarboard, targetStarboard, tankVolumeLiters, fixedDeltaTime, pumpPowered, emergencyBlow);
                writtenTankVolumeLiters = tankVolumeLiters;
                wroteCommands = true;
            }
            finally
            {
                ReleaseVaultWrite(in _ballastCommandsHandle);
            }

            if (wroteCommands)
                WriteBallastTuning(writtenTankVolumeLiters);
        }

        private float PrepareTankForCommand(NativeArray<BallastTankDTO> tanks, int index, float tankVolumeLiters)
        {
            if ((uint)index >= (uint)tanks.Length)
                return math.saturate(neutralBallastFill01);

            BallastTankDTO tank = tanks[index];
            if ((tank.InputStateFlags & SubmarineBallastConstants.TankFlagInitialized) == 0u ||
                tank.TankVolumeLiters <= 0.0001f)
            {
                tank.TankVolumeLiters = tankVolumeLiters;
                tank.CurrentWaterLiters = math.saturate(neutralBallastFill01) * tankVolumeLiters;
                tank.CompressedAirPressureATM = math.max(1f, airBankPressureATM);
                tank.InputStateFlags = SubmarineBallastConstants.TankFlagInitialized;
                tank.PumpRateLitersPerSecond = math.max(0f, pumpFillRate01PerSecond) * tankVolumeLiters;
                tanks[index] = tank;
            }

            return ResolveTankFill01(tank);
        }

        private float EstimateRequestedFillDelta01(
            float currentFrontFill01,
            float currentAftFill01,
            float currentPortFill01,
            float currentStarboardFill01,
            float targetFront,
            float targetAft,
            float targetPort,
            float targetStarboard,
            float fixedDeltaTime)
        {
            float dt = math.max(0f, fixedDeltaTime);
            return EstimateTankDelta01(currentFrontFill01, targetFront, dt) +
                   EstimateTankDelta01(currentAftFill01, targetAft, dt) +
                   EstimateTankDelta01(currentPortFill01, targetPort, dt) +
                   EstimateTankDelta01(currentStarboardFill01, targetStarboard, dt);
        }

        private float EstimateTankDelta01(float currentFill01, float targetFill01, float fixedDeltaTime)
        {
            float currentFill = math.saturate(currentFill01);
            float requested = math.abs(math.saturate(targetFill01) - currentFill);
            float rate = targetFill01 < currentFill ? ballastBlowRate01PerSecond : pumpFillRate01PerSecond;
            return math.min(requested, math.max(0f, rate) * fixedDeltaTime);
        }

        private void WriteBallastCommand(
            NativeArray<BallastTankCommandDTO> commands,
            float currentFill01,
            int index,
            float targetFill01,
            float tankVolumeLiters,
            float fixedDeltaTime,
            bool pumpPowered,
            bool emergencyBlow)
        {
            if ((uint)index >= (uint)commands.Length)
                return;

            float safeTankVolumeLiters = math.max(0.0001f, tankVolumeLiters);
            float currentLiters = math.saturate(currentFill01) * safeTankVolumeLiters;
            float targetLiters = math.saturate(targetFill01) * safeTankVolumeLiters;
            uint flags = 0u;
            if (!pumpPowered)
                flags |= SubmarineBallastConstants.CommandFlagPumpDenied;
            else if (targetLiters > currentLiters + 0.001f)
                flags |= SubmarineBallastConstants.CommandFlagFlood;
            else if (targetLiters < currentLiters - 0.001f || emergencyBlow)
                flags |= SubmarineBallastConstants.CommandFlagBlow;

            commands[index] = new BallastTankCommandDTO
            {
                TargetWaterLiters = targetLiters,
                FloodRateLitersPerSecond = math.max(0f, pumpFillRate01PerSecond) * safeTankVolumeLiters,
                BlowRateLitersPerSecond = math.max(0f, ballastBlowRate01PerSecond) * safeTankVolumeLiters,
                CompressedAirPressureATM = math.max(1f, airBankPressureATM),
                CommandFlags = flags,
                TargetEntityHash = unchecked((uint)_targetInstanceId),
                Frame = unchecked((uint)_tickCount),
                TankIndex = index
            };
        }

        private static float ResolveTankFill01(in BallastTankDTO tank)
        {
            float volume = math.max(0.0001f, tank.TankVolumeLiters);
            return math.saturate(tank.CurrentWaterLiters * math.rcp(volume));
        }

        private bool TrySpendPumpPower(float fillMagnitude)
        {
            if (fillMagnitude <= 0.000001f)
                return true;

            IPowerGridService powerGrid = _powerGrid;
            if (powerGrid == null)
                return false;

            float requestedEnergy = fillMagnitude * math.max(0f, pumpEnergyWattSecondsPerFill01);
            if (requestedEnergy <= 0.000001f)
                return true;

            return powerGrid.TryQueueWirelessToolDrain(requestedEnergy, out float grantedEnergy) &&
                   grantedEnergy + 0.001f >= requestedEnergy;
        }

        private void EmitAirReleaseIfNeeded(float beforeFillSum, float afterFillSum)
        {
            float releasedFill = math.max(0f, beforeFillSum - afterFillSum);
            if (releasedFill < airReleaseAudioFillDeltaThreshold)
                return;

            if (_airReleaseCooldownSeconds > 0f)
                return;

            _airReleaseCooldownSeconds = 0.2f;
            Vector3 source = _hull != null ? _hull.worldCenterOfMass : (_cachedTransform != null ? _cachedTransform.position : Vector3.zero);
            QueueAirReleaseAudio(
                source,
                math.saturate(releasedFill),
                0.16f,
                1f,
                1800f,
                ProceduralAudioPingKind.AirRelease);
        }

        private void AdvanceAirReleaseCooldown(float fixedDeltaTime)
        {
            if (_airReleaseCooldownSeconds <= 0f)
                return;

            _airReleaseCooldownSeconds = math.max(0f, _airReleaseCooldownSeconds - math.max(0f, fixedDeltaTime));
        }

        private void ScheduleBallastSolver(float fixedDeltaTime)
        {
            if (_ballastSolverJobPending ||
                !PrepareBallastFluidSample(fixedDeltaTime) ||
                !TryAcquireBallastSolverJobBuffers(
                    out NativeArray<BallastTankDTO> tanks,
                    out NativeArray<BallastTankCommandDTO> commands,
                    out NativeArray<SubmarineBallastFluidSampleDTO> samples,
                    out NativeArray<SubmarineBallastForcePacketDTO> forcePackets,
                    out NativeArray<SubmarineBallastTelemetryEntry> telemetry,
                    out NativeArray<VesselTelemetryEntry> vesselTelemetry))
            {
                return;
            }

            try
            {
                JobHandle dependency = default;
                if (useEmergencyMockWaveSampler)
                {
                    dependency = new GenerateMockFluidDisplacementJob
                    {
                        FluidSamples = samples,
                        Frame = unchecked((uint)_tickCount)
                    }.Schedule(1, 1, dependency);
                }

                JobHandle tankHandle = new EvaluateBallastTanksJob
                {
                    Tanks = tanks,
                    Commands = commands,
                    FluidSamples = samples,
                    AcousticWriter = SignalBus<MovementAcousticSignal>.ParallelWriter,
                    AcousticWriterBudget = SignalBus<MovementAcousticSignal>.ParallelWriterBudget,
                    DeltaTime = fixedDeltaTime,
                    Frame = unchecked((uint)_tickCount),
                    EmitAcousticSignals = 1
                }.Schedule(TankCount, 1, dependency);

                _ballastSolverHandle = new CalculateBuoyancyForceJob
                {
                    Tanks = tanks,
                    FluidSamples = samples,
                    VesselTelemetry = vesselTelemetry,
                    ForcePackets = forcePackets,
                    TelemetryRing = telemetry,
                    TankCount = TankCount,
                    Frame = unchecked((uint)_tickCount)
                }.Schedule(1, 1, tankHandle);
                _ballastSolverJobPending = true;
                _ballastScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                H8Memory.RegisterActiveJob(OwnerSystem, _ballastSolverHandle);
            }
            finally
            {
                if (!_ballastSolverJobPending)
                    ReleaseBallastSolverVaultLocks();
            }
        }

        private bool CompleteBallastSolverJob(bool forceComplete, bool applyForces)
        {
            if (!_ballastSolverJobPending)
            {
                ReleaseBallastSolverVaultLocks();
                return true;
            }

            if (!DispatcherJobSwap.TryComplete(ref _ballastSolverHandle, forceComplete))
                return false;

            _ballastSolverJobPending = false;
            SubmarineBallastForcePacketDTO packet = default;
            bool hasPacket = false;
            try
            {
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _ballastScheduleTimestamp;
                _lastBallastComputeMicros = elapsedTicks > 0
                    ? (float)(elapsedTicks * 1000000d / System.Diagnostics.Stopwatch.Frequency)
                    : 0f;
                MirrorBallastFillFromTanks();

                if (applyForces &&
                    TryResolveBallastForcePacketsLocked(out NativeArray<SubmarineBallastForcePacketDTO> forcePackets) &&
                    forcePackets.Length > 0)
                {
                    packet = forcePackets[0];
                    packet.ComputeMicros = _lastBallastComputeMicros;
                    packet.Flags |= SubmarineBallastConstants.ForceFlagTimingProxy;
                    forcePackets[0] = packet;
                    hasPacket = true;
                }
            }
            finally
            {
                ReleaseBallastSolverVaultLocks();
            }

            if (!applyForces || !hasPacket)
                return true;

            PatchBallastTelemetryComputeMicros(in packet);
            WriteBallastTuning(ResolveTankVolumeLiters(), in packet);

            bool invalid = (packet.Flags & SubmarineBallastConstants.ForceFlagNonFinite) != 0u ||
                           !math.all(math.isfinite(packet.NetForce)) ||
                           _lastBallastComputeMicros > SubmarineBallastConstants.FaultMicros;
            if ((packet.Flags & SubmarineBallastConstants.ForceFlagPressureBlocked) != 0u)
                _pendingTelemetryFlags |= PidTelemetryFlagBallastPressureBlocked;
            if (invalid)
                _pendingTelemetryFlags |= PidTelemetryFlagBallastInvalid;

            if (invalid)
            {
                DumpBallastTelemetryOnce(packet.Flags);
                return true;
            }

            if (_hull != null &&
                (packet.Flags & SubmarineBallastConstants.ForceFlagValid) != 0u &&
                math.lengthsq(packet.NetForce) > 0.000001f)
            {
                _physicsService?.QueueAmbientForce(_hull, ToVector3(packet.NetForce), ForceMode.Force);
            }

            return true;
        }

        private bool PrepareBallastFluidSample(float fixedDeltaTime)
        {
            if (_hull == null ||
                !TryAcquireVaultWrite(
                    in _ballastFluidSamplesHandle,
                    SubmarineBallastBufferIds.FluidSamples,
                    1,
                    out NativeArray<SubmarineBallastFluidSampleDTO> samples))
            {
                return false;
            }

            try
            {
                Vector3 worldCenter = _hull.worldCenterOfMass;
                if (!TryResolveAupFromRuntimeOrigin(worldCenter, out AbsoluteUniversePosition hullAup))
                    return false;

                double3 hullAbsolute = hullAup.ToAbsoluteDouble3();
                SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
                float depthMeters = fluidDynamics != null
                    ? math.max(0f, fluidDynamics.ExternalDepthMeters)
                    : ResolveFallbackExternalDepthMeters(worldCenter);
                double3 surfaceAbsolute = hullAbsolute + new double3(0d, depthMeters, 0d);
                float hullVolume = ResolveBallastHullVolume();
                float hullHeight = math.max(0.1f, ballastHullHeightMeters);
                float quality = ReadCachedGlobalQualityWeight();
                int activeSampleBudget = ResolveBallastActiveSampleBudget(quality);

                samples[0] = new SubmarineBallastFluidSampleDTO
                {
                    HullPositionAup = hullAup,
                    HullAup = hullAbsolute,
                    OceanSurfaceAup = surfaceAbsolute,
                    HullVelocity = (float3)(_hull.linearVelocity),
                    HullHeightMeters = hullHeight,
                    HullVolumeCubicMeters = hullVolume,
                    FluidDensityKgPerM3 = ResolveAmbientFluidDensityKgPerM3(),
                    AmbientPressureATM = SubmarineBallastConstants.AtmosphericPressureAtm +
                                         (depthMeters * SubmarineBallastConstants.SeaWaterAtmPerMeter),
                    GlobalQualityWeight = quality,
                    SimulationDeltaTime = math.max(0.0001f, fixedDeltaTime),
                    TargetEntityHash = unchecked((uint)_targetInstanceId),
                    Frame = unchecked((uint)_tickCount),
                    Flags = 0u,
                    SurfaceSwellMeters = 0f,
                    ActiveSampleBudget = activeSampleBudget
                };
                return true;
            }
            finally
            {
                ReleaseVaultWrite(in _ballastFluidSamplesHandle);
            }
        }

        private float ResolveFallbackExternalDepthMeters(Vector3 worldCenter)
        {
            float seaLevelY = ResolveFallbackSeaLevelY();
            return math.isfinite(worldCenter.y) ? math.max(0f, seaLevelY - worldCenter.y) : 0f;
        }

        private float ResolveFallbackSeaLevelY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveBallastSeaLevelY(oceanKinematics.SeaLevel, out float oceanSeaLevelY))
            {
                return oceanSeaLevelY;
            }

            return DefaultSeaLevelY;
        }

        private static bool TryResolveBallastSeaLevelY(float candidateSeaLevelY, out float seaLevelY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = candidateSeaLevelY;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private void PatchBallastTelemetryComputeMicros(in SubmarineBallastForcePacketDTO packet)
        {
            if (!TryAcquireVaultWrite(
                    in _ballastTelemetryHandle,
                    SubmarineBallastBufferIds.TelemetryRing,
                    SubmarineBallastConstants.TelemetryCapacity,
                    out NativeArray<SubmarineBallastTelemetryEntry> telemetry))
            {
                return;
            }

            try
            {
                int index = (int)(packet.Frame % (uint)telemetry.Length);
                SubmarineBallastTelemetryEntry entry = telemetry[index];
                if (entry.Frame != packet.Frame)
                    return;

                entry.ComputeMicros = _lastBallastComputeMicros;
                entry.Flags = packet.Flags;
                telemetry[index] = entry;
            }
            finally
            {
                ReleaseVaultWrite(in _ballastTelemetryHandle);
            }
        }

        private void MirrorBallastFillFromTanks()
        {
            if (!TryResolveBallastTanksLocked(out NativeArray<BallastTankDTO> tanks) ||
                !TryAcquireVaultWrite(
                    in _ballastFill01Handle,
                    BufferID.SubmarineBallastFill01,
                    TankCount,
                    out NativeArray<float> ballastFill))
            {
                return;
            }

            try
            {
                int count = math.min(TankCount, math.min(tanks.Length, ballastFill.Length));
                for (int i = 0; i < count; i++)
                    ballastFill[i] = ResolveTankFill01(tanks[i]);
            }
            finally
            {
                ReleaseVaultWrite(in _ballastFill01Handle);
            }
        }

        private float ResolveTankVolumeLiters()
        {
            return math.max(0.01f, ballastTankVolumeCubicMeters) * SubmarineBallastConstants.LitersPerCubicMeter;
        }

        private float ResolveBallastHullVolume()
        {
            float authored = math.max(0f, ballastHullVolumeCubicMeters);
            if (authored > 0.001f)
                return authored;

            float neutralMass = math.max(1f, _baseMassKg) * DefaultBallastHullVolumeMassScalar;
            return neutralMass * math.rcp(math.max(1f, WaterDensityKgPerCubicMeter));
        }

        /// <summary>
        /// Ambient fluid density at the hull. The buoyancy solver and the ballast DTO both carry a
        /// per-instance FluidDensityKgPerM3, and SubmarineBallastBuoyancyContracts already divides by
        /// it - but every call site fed the sea-water compile-time constant, so the submarine
        /// displaced the same mass of fluid inside a heavy brine pool as in open ocean. The brine
        /// read model is the same service ToolDurabilitySystem consumes, injected cold through the
        /// registry hot-swap slot, so this stays a pure read with no scene search.
        /// </summary>
        private float ResolveAmbientFluidDensityKgPerM3()
        {
            IBrineFluidDensityReadModel readModel = _brineDensityReadModel;
            if (readModel == null || _hull == null)
                return WaterDensityKgPerCubicMeter;

            if (!readModel.TrySampleBrineFluidDensity(_hull.position, out float densityKgPerCubicMeter) ||
                !math.isfinite(densityKgPerCubicMeter))
            {
                return WaterDensityKgPerCubicMeter;
            }

            // Brine is denser than sea water, never lighter; clamping the low side keeps a bad
            // sample from making the hull weightless, and the high side bounds the solver's
            // buoyant force so a hot-swapped provider cannot launch the submarine.
            return math.clamp(
                densityKgPerCubicMeter,
                WaterDensityKgPerCubicMeter,
                WaterDensityKgPerCubicMeter * MaximumAmbientDensityRatio);
        }

        private void RefreshGlobalQualityWeightSnapshotCold()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            _cachedGlobalQualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private float ReadCachedGlobalQualityWeight()
        {
            float quality = _cachedGlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static int ResolveBallastActiveSampleBudget(float quality)
        {
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            return math.clamp(1 + (int)math.ceil(quality * 3f), 1, 4);
        }

        private void ApplyMassDistribution()
        {
            if (!TryReadTankLocalPositionsReadOnly(out NativeArray<float3>.ReadOnly tankLocalPositions))
                return;

            float baseMass = math.max(MinimumMassForReciprocal, _baseMassKg);
            float totalBallastMass = 0f;
            float3 weightedSum = (float3)(baseCenterOfMassLocal) * baseMass;

            if (!_ballastSolverJobPending &&
                !_ballastSolverVaultLocksHeld &&
                TryReadBallastTanksReadOnly(out NativeArray<BallastTankDTO>.ReadOnly tanks))
            {
                int count = math.min(TankCount, math.min(tanks.Length, tankLocalPositions.Length));
                for (int i = 0; i < count; i++)
                {
                    float liters = math.clamp(tanks[i].CurrentWaterLiters, 0f, math.max(0.0001f, tanks[i].TankVolumeLiters));
                    float mass = liters * SubmarineBallastConstants.CubicMetersPerLiter * WaterDensityKgPerCubicMeter;
                    totalBallastMass += mass;
                    weightedSum += tankLocalPositions[i] * mass;
                }
            }
            else if (TryReadBallastFillReadOnly(out NativeArray<float>.ReadOnly ballastFill))
            {
                float tankMassFull = ResolveTankVolumeLiters() * SubmarineBallastConstants.CubicMetersPerLiter * WaterDensityKgPerCubicMeter;
                int count = math.min(TankCount, math.min(ballastFill.Length, tankLocalPositions.Length));
                for (int i = 0; i < count; i++)
                {
                    float mass = math.saturate(ballastFill[i]) * tankMassFull;
                    totalBallastMass += mass;
                    weightedSum += tankLocalPositions[i] * mass;
                }
            }

            _ballastWaterMassKg = totalBallastMass;
            float totalMass = math.max(MinimumMassForReciprocal, baseMass + totalBallastMass);
            _centerOfMassLocal = weightedSum * math.rcp(math.max(MinimumMassForReciprocal, totalMass));
            ApplyDynamicFloodMassToCurrentCenter(totalMass);
            if (!math.all(math.isfinite(_centerOfMassLocal)))
                _centerOfMassLocal = (float3)(baseCenterOfMassLocal);

            if (_hull != null)
            {
                _hull.centerOfMass = ToVector3(_centerOfMassLocal);
                ApplyDynamicFloodAngularDrag();
                ApplyDynamicFloodInertiaTensor();
            }

            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics != null)
                fluidDynamics.SetBallastWaterMassKilograms(_ballastWaterMassKg);
        }

        private float ApplyDynamicFloodMassToCurrentCenter(float totalMass)
        {
            if (_dynamicFloodWaterMassKg <= 0.001f)
                return totalMass;

            if (!math.all(math.isfinite(_dynamicFloodCenterOfMassLocal)) ||
                !math.all(math.isfinite(_dynamicFloodComOffsetLocal)) ||
                !math.isfinite(_dynamicFloodWaterMassKg) ||
                !math.isfinite(totalMass))
            {
                _pendingTelemetryFlags |= PidTelemetryFlagFloodInvalid;
                return totalMass;
            }

            float dryMass = math.max(MinimumMassForReciprocal, _baseMassKg);
            float floodMass = math.max(0f, _dynamicFloodWaterMassKg);
            float3 dryCenter = (float3)(baseCenterOfMassLocal);
            float3 floodOnlyWeightedCenter =
                (_dynamicFloodCenterOfMassLocal * (dryMass + floodMass)) -
                (dryCenter * dryMass);
            float combinedMass = math.max(MinimumMassForReciprocal, totalMass + floodMass);
            float3 combinedCenter =
                ((_centerOfMassLocal * math.max(MinimumMassForReciprocal, totalMass)) + floodOnlyWeightedCenter) *
                math.rcp(math.max(MinimumMassForReciprocal, combinedMass));

            if (math.all(math.isfinite(combinedCenter)))
                _centerOfMassLocal = combinedCenter;
            else
                _pendingTelemetryFlags |= PidTelemetryFlagFloodInvalid;

            return combinedMass;
        }

        private void ApplyDynamicFloodAngularDrag()
        {
            if (_hull == null)
                return;

            float multiplier = math.max(1f, _dynamicFloodAngularDragMultiplier);
            if (!math.isfinite(multiplier))
            {
                _pendingTelemetryFlags |= PidTelemetryFlagFloodInvalid;
                multiplier = 1f;
            }

            float baseDamping = _baseAngularDampingCached
                ? math.max(0f, _baseAngularDamping)
                : math.max(0f, _hull.angularDamping);
            if (multiplier > 1.0001f)
                baseDamping = math.max(baseDamping, math.max(0f, floodAngularDampingFloor));

            _hull.angularDamping = baseDamping * multiplier;
        }

        private void RestoreDynamicFloodAngularDrag()
        {
            if (_hull == null)
                return;

            float baseDamping = _baseAngularDampingCached
                ? math.max(0f, _baseAngularDamping)
                : math.max(0f, _hull.angularDamping);
            _hull.angularDamping = math.isfinite(baseDamping) ? baseDamping : 0f;
        }

        private void ApplyDynamicFloodInertiaTensor()
        {
            if (_hull == null || !_baseInertiaTensorCached)
                return;

            float3 multiplier = _dynamicFloodInertiaTensorMultiplier;
            if (!math.all(math.isfinite(multiplier)))
            {
                _pendingTelemetryFlags |= PidTelemetryFlagFloodInvalid;
                multiplier = new float3(1f);
            }

            multiplier = math.max(new float3(1f), multiplier);
            float3 delta = multiplier - _lastAppliedInertiaTensorMultiplier;
            if (math.lengthsq(delta) <= 0.00000025f)
                return;

            float3 baseTensor = (float3)(_baseInertiaTensor);
            float3 nextTensor = math.max(new float3(0.001f), baseTensor * multiplier);
            if (!math.all(math.isfinite(nextTensor)))
            {
                _pendingTelemetryFlags |= PidTelemetryFlagFloodInvalid;
                return;
            }

            _hull.inertiaTensor = ToVector3(nextTensor);
            _lastAppliedInertiaTensorMultiplier = multiplier;
        }

        private void RestoreDynamicFloodInertiaTensor()
        {
            if (_hull == null || !_baseInertiaTensorCached)
                return;

            _hull.inertiaTensor = _baseInertiaTensor;
            _lastAppliedInertiaTensorMultiplier = new float3(1f);
        }

        private void ApplyDynamicFloodDragTensor()
        {
            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics == null)
                return;

            if (_dynamicFloodWaterMassKg <= 0.001f)
            {
                fluidDynamics.SetExternalFloodDragTensor(new float3(1f), new float3(1f));
                return;
            }

            float mass01 = math.saturate(_dynamicFloodWaterMassKg * math.rcp(math.max(MinimumMassForReciprocal, _baseMassKg)));
            float3 offset = math.abs(_dynamicFloodComOffsetLocal);
            float3 linear = new float3(
                1f + mass01 * (0.90f + offset.x),
                1f + mass01 * (0.65f + offset.y),
                1f + mass01 * (0.45f + offset.z));
            float3 angular = new float3(
                1f + mass01 * (0.95f + offset.z),
                1f + mass01 * (0.70f + offset.x),
                1f + mass01 * (0.95f + offset.y));

            if (!math.all(math.isfinite(linear)) || !math.all(math.isfinite(angular)))
            {
                _pendingTelemetryFlags |= PidTelemetryFlagFloodInvalid;
                fluidDynamics.SetExternalFloodDragTensor(new float3(1f), new float3(1f));
                return;
            }

            fluidDynamics.SetExternalFloodDragTensor(linear, angular);
        }

        private void ResetExternalFloodDragTensor()
        {
            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics != null)
                fluidDynamics.SetExternalFloodDragTensor(new float3(1f), new float3(1f));
        }

        private float3 ResolveInertiaTensorMultiplier(float waterMassKg, float3 centerOfMassOffsetLocal)
        {
            if (!math.isfinite(waterMassKg) || !math.all(math.isfinite(centerOfMassOffsetLocal)))
                return new float3(1f);

            float massRatio = math.max(0f, waterMassKg) * math.rcp(math.max(MinimumMassForReciprocal, _baseMassKg));
            float3 absOffset = math.abs(centerOfMassOffsetLocal);
            float3 multiplier = new float3(
                1f + massRatio * (0.75f + absOffset.z),
                1f + massRatio * (0.50f + absOffset.x),
                1f + massRatio * (0.75f + absOffset.y));

            return math.all(math.isfinite(multiplier))
                ? math.clamp(multiplier, new float3(1f), new float3(4f))
                : new float3(1f);
        }

        private double3 ResolveGlobalPivotAnchor()
        {
            Vector3 position = _hull != null
                ? _hull.position
                : (_cachedTransform != null ? _cachedTransform.position : Vector3.zero);
            if (!IsFinite(position))
                return math.all(math.isfinite(_dynamicFloodGlobalPivotAnchor)) ? _dynamicFloodGlobalPivotAnchor : double3.zero;

            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition aup))
                return math.all(math.isfinite(_dynamicFloodGlobalPivotAnchor)) ? _dynamicFloodGlobalPivotAnchor : double3.zero;

            double3 anchor = aup.ToAbsoluteDouble3();
            return math.all(math.isfinite(anchor)) ? anchor : double3.zero;
        }

        private void EmitDynamicFloodFeedback(float fixedDeltaTime)
        {
            _floodStressAudioCooldown = math.max(0f, _floodStressAudioCooldown - math.max(0f, fixedDeltaTime));
            _pidHullStressAudioCooldown = math.max(0f, _pidHullStressAudioCooldown - math.max(0f, fixedDeltaTime));
            _criticalFloodHapticCooldown = math.max(0f, _criticalFloodHapticCooldown - math.max(0f, fixedDeltaTime));
            _tailHeavyBubbleCooldown = math.max(0f, _tailHeavyBubbleCooldown - math.max(0f, fixedDeltaTime));
            _criticalListCooldown = math.max(0f, _criticalListCooldown - math.max(0f, fixedDeltaTime));

            if (_hull == null)
                return;

            EmitTailHeavyBubbleSignal();

            float stressThreshold = math.max(0f, floodComStressAudioThresholdMeters);
            float offsetMagnitudeSq = math.lengthsq(_dynamicFloodComOffsetLocal);
            if (offsetMagnitudeSq >= stressThreshold * stressThreshold &&
                _floodStressAudioCooldown <= 0f)
            {
                float offsetMagnitude = ApproximateMagnitudeNoSqrt(_dynamicFloodComOffsetLocal);
                if (TryResolveAupFromRuntimeOrigin(_hull.worldCenterOfMass, out AbsoluteUniversePosition stressAup))
                {
                    _floodStressAudioCooldown = math.max(0.05f, floodStressAudioCooldownSeconds);
                    AcousticPingSignal stress = default;
                    stress.PositionAup = stressAup;
                    stress.RadiusMeters = math.lerp(18f, 42f, math.saturate(offsetMagnitude));
                    stress.Intensity01 = math.saturate(offsetMagnitude * 1.8f);
                    stress.SourceId = unchecked((uint)_targetInstanceId);
                    stress.Channel = AcousticPingSignal.ChannelMetalStress;
                    QueueFloodStressAcoustic(in stress);
                }
            }

            if (_criticalFloodActive == 0)
                return;

            if (_criticalFloodHapticCooldown <= 0f)
            {
                _criticalFloodHapticCooldown = math.max(0.05f, criticalFloodHapticCooldownSeconds);
                HapticRequest haptic = default;
                haptic.Intensity01 = math.saturate(_dynamicFloodWaterMassKg * math.rcp(math.max(MinimumMassForReciprocal, _baseMassKg)));
                haptic.DurationSeconds = 0.35f;
                haptic.Frequency01 = 0.18f;
                haptic.SourceHash = FloodFeedbackSourceHash;
                haptic.Frame = unchecked((uint)_tickCount);
                haptic.Channel = HapticRequest.ChannelVehicleCritical;
                QueueCriticalFloodHaptic(in haptic);
            }

            if (_criticalListCooldown > 0f || !IsCriticalFloodPitchExceeded())
                return;

            _criticalListCooldown = 0.5f;
            VehicleCommandSignal criticalList = default;
            criticalList.TargetInstanceId = _targetInstanceId;
            criticalList.Flags = (byte)VehicleCommandSignalFlags.CriticalList;
            if (VehicleCommandSignalBus.TryPublish(in criticalList))
                _pendingTelemetryFlags |= PidTelemetryFlagCriticalList;
        }

        private void QueueFloodStressAcoustic(in AcousticPingSignal signal)
        {
            _pendingFloodStressAcoustic = signal;
            _pendingFloodStressAcousticDirty = true;
        }

        private void QueueCriticalFloodHaptic(in HapticRequest signal)
        {
            _pendingCriticalFloodHaptic = signal;
            _pendingCriticalFloodHapticDirty = true;
        }

        private void QueueAirReleaseAudio(
            Vector3 position,
            float intensity01,
            float durationSeconds,
            float transmission01,
            float pitchCarrierHz,
            ProceduralAudioPingKind kind)
        {
            _pendingAirReleaseAudio.Position = position;
            _pendingAirReleaseAudio.Intensity01 = intensity01;
            _pendingAirReleaseAudio.DurationSeconds = durationSeconds;
            _pendingAirReleaseAudio.Transmission01 = transmission01;
            _pendingAirReleaseAudio.PitchCarrierHz = pitchCarrierHz;
            _pendingAirReleaseAudio.Kind = kind;
            _pendingAirReleaseAudioDirty = true;
        }

        private void FlushDynamicFloodFeedback()
        {
            if (_pendingAirReleaseAudioDirty)
            {
                _pendingAirReleaseAudioDirty = false;
                ProceduralAudioPingRequest request = _pendingAirReleaseAudio;
                _pendingAirReleaseAudio = default;
                ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                    request.Position,
                    request.Intensity01,
                    request.DurationSeconds,
                    request.Transmission01,
                    request.PitchCarrierHz,
                    request.Kind);
            }

            if (_pendingFloodStressAcousticDirty)
            {
                _pendingFloodStressAcousticDirty = false;
                SignalBus<AcousticPingSignal>.TryPushTracked(in _pendingFloodStressAcoustic, ref s_x001SubmarineAutoLevelBallastControllerSignalPushDropCount);
                _pendingFloodStressAcoustic = default;
            }

            if (_pendingCriticalFloodHapticDirty)
            {
                _pendingCriticalFloodHapticDirty = false;
                SignalBus<HapticRequest>.TryPushTracked(in _pendingCriticalFloodHaptic, ref s_x001SubmarineAutoLevelBallastControllerSignalPushDropCount);
                _pendingCriticalFloodHaptic = default;
            }

            if (_pendingTailHeavyBubbleDirty)
            {
                _pendingTailHeavyBubbleDirty = false;
                SignalBus<BubbleSpawnSignal>.TryPushTracked(in _pendingTailHeavyBubble, ref s_x001SubmarineAutoLevelBallastControllerSignalPushDropCount);
                _pendingTailHeavyBubble = default;
            }

            if (_pendingTailHeavyFluidImpulseDirty)
            {
                _pendingTailHeavyFluidImpulseDirty = false;
                SignalBus<FluidImpulseSignal>.TryPushTracked(in _pendingTailHeavyFluidImpulse, ref s_x001SubmarineAutoLevelBallastControllerSignalPushDropCount);
                _pendingTailHeavyFluidImpulse = default;
            }

            if (_pendingPidHullStressSignalDirty)
            {
                _pendingPidHullStressSignalDirty = false;
                HullStressSignal request = _pendingPidHullStressSignal;
                _pendingPidHullStressSignal = default;
                ProceduralAudioEvents.TryRaiseHullStressSignal(in request);
            }
        }

        private static float ApproximateMagnitudeNoSqrt(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxAxis = math.max(absValue.x, math.max(absValue.y, absValue.z));
            float minAxis = math.min(absValue.x, math.min(absValue.y, absValue.z));
            float midAxis = absValue.x + absValue.y + absValue.z - maxAxis - minAxis;
            float magnitude = maxAxis + (0.375f * midAxis) + (0.125f * minAxis);
            return math.select(magnitude, 0.0f, !math.isfinite(magnitude));
        }

        private void EmitTailHeavyBubbleSignal()
        {
            if (_tailHeavyBubbleCooldown > 0f ||
                _dynamicFloodSignalActive == 0 ||
                !IsTailHeavyPitchExceeded(tailHeavyBubblePitchDegrees) ||
                _cachedTransform == null)
            {
                return;
            }

            Vector3 ventPosition = _cachedTransform.TransformPoint(engineVentLocalPosition);
            if (!IsFinite(ventPosition))
                return;

            float3 ventDirection = (float3)(-_cachedTransform.forward);
            if (!math.all(math.isfinite(ventDirection)))
                return;

            float directionLengthSq = math.lengthsq(ventDirection);
            if (directionLengthSq <= 1e-6f)
                return;

            ventDirection *= math.rsqrt(math.max(directionLengthSq, 1e-6f));
            float intensity01 = math.saturate(_dynamicFloodWaterMassKg * math.rcp(math.max(MinimumMassForReciprocal, _baseMassKg)));
            if (!TryResolveAupFromRuntimeOrigin(ventPosition, out AbsoluteUniversePosition ventAup))
                return;

            _tailHeavyBubbleCooldown = math.max(0.05f, tailHeavyBubbleCooldownSeconds);
            BubbleSpawnSignal signal = default;
            signal.PositionAup = ventAup;
            signal.Direction = ventDirection;
            signal.Intensity01 = intensity01;
            signal.RadiusMeters = 1.35f;
            signal.Frame = unchecked((uint)_tickCount);
            signal.SourceHash = EngineVentBubbleSourceHash;
            signal.Flags = BubbleSpawnSignal.FlagEngineVent | BubbleSpawnSignal.FlagTailHeavy;
            QueueTailHeavyBubbleSignal(in signal);
            _pendingTelemetryFlags |= PidTelemetryFlagBubbleSignal;
            EmitTailHeavyFluidImpulse(in signal.PositionAup, ventDirection, intensity01);
        }

        private void EmitTailHeavyFluidImpulse(in AbsoluteUniversePosition positionAup, float3 direction, float intensity01)
        {
            if (intensity01 <= 0.001f || !math.all(math.isfinite(direction)))
                return;

            float directionLengthSq = math.lengthsq(direction);
            if (directionLengthSq <= 1e-6f)
                return;

            float strength01 = math.saturate(intensity01);
            float3 normalizedDirection = direction * math.rsqrt(math.max(directionLengthSq, 1e-6f));
            FluidImpulseSignal impulse = default;
            impulse.PositionAup = positionAup;
            impulse.Vector = normalizedDirection * math.lerp(0.75f, 3.5f, strength01);
            impulse.Radius = math.lerp(1.5f, 4.75f, strength01);
            impulse.Lifetime = math.lerp(0.35f, 1.1f, strength01);
            impulse.Frame = unchecked((uint)_tickCount);
            impulse.SourceHash = EngineVentBubbleSourceHash;
            impulse.Flags = EngineVentFluidImpulseFlag | TailHeavyFluidImpulseFlag;
            QueueTailHeavyFluidImpulse(in impulse);
            _pendingTelemetryFlags |= PidTelemetryFlagFluidImpulseSignal;
        }

        private void QueueTailHeavyBubbleSignal(in BubbleSpawnSignal signal)
        {
            _pendingTailHeavyBubble = signal;
            _pendingTailHeavyBubbleDirty = true;
        }

        private void QueueTailHeavyFluidImpulse(in FluidImpulseSignal impulse)
        {
            _pendingTailHeavyFluidImpulse = impulse;
            _pendingTailHeavyFluidImpulseDirty = true;
        }

        private bool IsCriticalFloodPitchExceeded()
        {
            if (_hull == null)
                return false;

            float thresholdDegrees = math.clamp(criticalFloodPitchDegrees, 0f, 89f);
            if (thresholdDegrees <= 0.001f)
                return true;

            quaternion rotation = new quaternion(_hull.rotation.x, _hull.rotation.y, _hull.rotation.z, _hull.rotation.w);
            float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
            float thresholdSin = ResolveConservativeThresholdSin(thresholdDegrees);
            return math.abs(math.clamp(forward.y, -1f, 1f)) >= thresholdSin;
        }

        private bool IsTailHeavyPitchExceeded(float thresholdDegrees)
        {
            if (_hull == null)
                return false;

            float threshold = math.clamp(thresholdDegrees, 0f, 89f);
            quaternion rotation = new quaternion(_hull.rotation.x, _hull.rotation.y, _hull.rotation.z, _hull.rotation.w);
            float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
            float thresholdSin = ResolveConservativeThresholdSin(threshold);
            return math.clamp(forward.y, -1f, 1f) >= thresholdSin;
        }

        private static float ResolveConservativeThresholdSin(float degrees)
        {
            const float BhaskaraSinMaxAbsError = 0.0017f;
            float radians = math.radians(math.clamp(degrees, 0f, 89f));
            return math.saturate(MathLodApproximation.ApproxSinBhaskara(radians) + BhaskaraSinMaxAbsError);
        }

        private void SchedulePidJob(float fixedDeltaTime)
        {
            RefreshShinobu332GyroRouteStateFromCachedVault();
            if (IsShinobu332GyroRouteActive())
            {
                ResetLegacyAutoLevelStateForGyroRoute();
                return;
            }

            if (!autoLevelEnabled ||
                _pidJobPending ||
                _hull == null)
            {
                return;
            }

            if (_criticalFloodActive != 0)
            {
                _pidIntegral = float3.zero;
                _previousPidError = float3.zero;
                _lastPidDerivative = float3.zero;
                _lastIntegralWindup = 0f;
                _resetIntegralPending = true;
                _pendingTelemetryFlags |= PidTelemetryFlagCriticalFlood;
                return;
            }

            if (!TryAcquireVaultWrite(
                    in _pidOutputHandle,
                    BufferID.SubmarineBallastPidOutput,
                    1,
                    out NativeArray<PidJobOutput> pidOutput))
            {
                return;
            }

            Quaternion rotation = _hull.rotation;
            Vector3 angularVelocity = _hull.angularVelocity;
            WhirlpoolFlow activeMaelstrom0 = default;
            WhirlpoolFlow activeMaelstrom1 = default;
            int activeMaelstromCount = 0;
            IAnalyticalFlowReadModel analyticalFlow = _analyticalFlowReadModel;
            if (analyticalFlow != null &&
                analyticalFlow.TryGetActiveWhirlpoolFlows(out NativeArray<WhirlpoolFlow>.ReadOnly maelstroms, out int maelstromCount) &&
                maelstroms.IsCreated)
            {
                int maelstromLimit = math.min(
                    FluidAnalyticalContractConstants.MaxActiveMaelstromCount,
                    math.min(math.max(0, maelstromCount), maelstroms.Length));
                if (maelstromLimit > 0)
                    activeMaelstrom0 = maelstroms[0];
                if (maelstromLimit > 1)
                    activeMaelstrom1 = maelstroms[1];
                activeMaelstromCount = maelstromLimit;
            }

            try
            {
                _pidHandle = new SubmarineAutoLevelPidJob
                {
                    CurrentRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                    AngularVelocityWorld = (float3)(angularVelocity),
                    PreviousError = _previousPidError,
                    PreviousIntegral = _pidIntegral,
                    DeltaTime = fixedDeltaTime,
                    Kp = proportionalGain,
                    Ki = integralGain,
                    Kd = derivativeGain,
                    IntegralClamp = integralClamp,
                    MaxTorque = maxTorqueNewtons,
                    MaelstromAccelerationClamp = MaelstromAccelerationClamp,
                    SystemStress01 = _systemStress01,
                    PositionWS = (float3)(_hull.worldCenterOfMass),
                    DynamicFloodCenterOfMassOffsetLocal = _dynamicFloodComOffsetLocal,
                    FloodPitchBiasPerMeter = floodPidPitchBiasPerMeter,
                    ResetIntegral = _resetIntegralPending ? (byte)1 : (byte)0,
                    CriticalFloodActive = _criticalFloodActive,
                    MaelstromApproximationTier = 0,
                    ActiveMaelstromCount = activeMaelstromCount,
                    ActiveMaelstrom0 = activeMaelstrom0,
                    ActiveMaelstrom1 = activeMaelstrom1,
                    Output = pidOutput
                }.Schedule();
                _pidJobPending = true;
                _pidOutputVaultLockHeld = true;
                _resetIntegralPending = false;
            }
            finally
            {
                if (!_pidJobPending)
                    ReleasePidOutputVaultLock();
            }
        }

        private void SchedulePidJobAfterFloodWriteLocks(float fixedDeltaTime)
        {
            if (_floodMassJobPending ||
                _floodMassOutputVaultLockHeld ||
                _floodRoomInputGuardVault != null)
            {
                _pendingTelemetryFlags |= PidTelemetryFlagVaultWriteContention;
                return;
            }

            SchedulePidJob(fixedDeltaTime);
        }

        private bool IsShinobu332GyroRouteActive()
        {
            return _shinobu332GyroRouteActive != 0;
        }

        private void RefreshShinobu332GyroRouteHandleCold()
        {
            NativeArray<SubmarineGyroCounterDTO>.ReadOnly counters;
            TryRefreshExistingReadOnlyVaultBuffer(
                ref _shinobu332GyroCounterHandle,
                BufferID.Shinobu332GyroCounters,
                out counters);
        }

        private void RefreshShinobu332GyroRouteStateFromCachedVault()
        {
            if (!TryReadShinobu332GyroCountersCached(out NativeArray<SubmarineGyroCounterDTO>.ReadOnly counters) ||
                counters.Length == 0)
            {
                _shinobu332GyroRouteActive = 0;
                return;
            }

            SubmarineGyroCounterDTO counter = counters[0];
            uint lastTargetHash = counter.LastTargetEntityHash;
            uint targetHash = unchecked((uint)_targetInstanceId);
            uint fallbackHash = unchecked((uint)_fallbackInstanceId);
            bool active = counter.ActiveControllers > 0 &&
                          lastTargetHash != 0u &&
                          (lastTargetHash == targetHash ||
                           (fallbackHash != 0u && fallbackHash != targetHash && lastTargetHash == fallbackHash));
            _shinobu332GyroRouteActive = active ? (byte)1 : (byte)0;
        }

        private void ResetLegacyAutoLevelStateForGyroRoute()
        {
            _pidIntegral = float3.zero;
            _previousPidError = float3.zero;
            _lastPidDerivative = float3.zero;
            _lastIntegralWindup = 0f;
            _smoothedPidTorqueWorld = float3.zero;
            _resetIntegralPending = true;
        }

        private bool CompletePidJob(bool forceComplete, bool commitOutput)
        {
            if (!_pidJobPending)
            {
                ReleasePidOutputVaultLock();
                return true;
            }

            if (!DispatcherJobSwap.TryComplete(ref _pidHandle, forceComplete))
                return false;

            _pidJobPending = false;
            try
            {
                if (!commitOutput ||
                    !TryResolvePidOutputLocked(out NativeArray<PidJobOutput> pidOutput) ||
                    pidOutput.Length == 0)
                {
                    return true;
                }

                PidJobOutput output = pidOutput[0];
                _pidIntegral = output.Integral;
                _previousPidError = output.Error;
                _lastPidDerivative = output.Derivative;
                _lastIntegralWindup = output.IntegralWindup;
                _pendingTelemetryFlags |= output.Flags;

                if ((output.Flags & PidTelemetryDumpFaultMask) != 0u)
                    DumpTelemetryOnce(output.Flags);

                bool forceOutputBlocked = (output.Flags & PidTelemetryPidOutputForceBlockMask) != 0u;
                float3 acceptedTorque = output.TorqueWorld;
                if (forceOutputBlocked)
                    _smoothedPidTorqueWorld = float3.zero;
                else
                    acceptedTorque = FastNlerp(_smoothedPidTorqueWorld, acceptedTorque, pidTorqueFastNlerp01);

                RefreshShinobu332GyroRouteStateFromCachedVault();
                bool shinobuGyroActive = IsShinobu332GyroRouteActive();
                if (!shinobuGyroActive && _hull != null && !forceOutputBlocked && math.lengthsq(output.TorqueWorld) > 0.0001f)
                {
                    _smoothedPidTorqueWorld = acceptedTorque;
                    EmitPidHullStressSignal(output.Error, _hull.worldCenterOfMass);
                    _physicsService?.QueueTorque(_hull, ToVector3(acceptedTorque), ForceMode.Force);
                }

                if (_hull != null && !forceOutputBlocked && math.lengthsq(output.MaelstromAcceleration) > 0.0001f)
                    _physicsService?.QueueAmbientForce(_hull, ToVector3(output.MaelstromAcceleration), ForceMode.Acceleration);

                return true;
            }
            finally
            {
                ReleasePidOutputVaultLock();
            }
        }

        private void EmitPidHullStressSignal(float3 pidError, Vector3 worldPosition)
        {
            if (_pidHullStressAudioCooldown > 0f || !math.all(math.isfinite(pidError)) || !IsFinite(worldPosition))
                return;

            float pidErrorMagnitude = ApproximateMagnitudeNoSqrt(pidError);
            float stress01 = math.saturate(pidErrorMagnitude * 0.35f);
            if (stress01 <= 0.001f)
                return;

            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            float depthMeters = fluidDynamics != null ? math.max(0f, fluidDynamics.ExternalDepthMeters) : 0f;
            float pitchScale = 0.85f + (stress01 * 0.6f);
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition stressAup))
                return;

            _pidHullStressAudioCooldown = math.max(0.05f, pidHullStressAudioCooldownSeconds);
            HullStressSignal signal = new HullStressSignal(
                stressAup,
                worldPosition,
                stress01,
                pidErrorMagnitude,
                depthMeters,
                pitchScale);

            QueuePidHullStressSignal(in signal);
        }

        private void QueuePidHullStressSignal(in HullStressSignal signal)
        {
            _pendingPidHullStressSignal = signal;
            _pendingPidHullStressSignalDirty = true;
        }

        private void RefreshSnapshot()
        {
            if (_hull == null)
                return;

            Quaternion rotation = _hull.rotation;
            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            _snapshot = new SubmarineStateSnapshot
            {
                RuntimePosition = SnapMillimeter((float3)(_hull.position)),
                RuntimeRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                LinearVelocity = SnapMillimeter((float3)(_hull.linearVelocity)),
                AngularVelocity = (float3)(_hull.angularVelocity),
                CenterOfMassLocal = _centerOfMassLocal,
                BaseMassKg = _baseMassKg,
                BallastWaterMassKg = _ballastWaterMassKg,
                TotalCargoMassKg = fluidDynamics != null ? fluidDynamics.TotalCargoMassKg : _ballastWaterMassKg,
                PidIntegralWindup = _lastIntegralWindup,
                MathLod = _authoritativeMathLod,
                PumpPowered = _pumpPowered,
                AutoLevelActive = autoLevelEnabled && _criticalFloodActive == 0 && !IsShinobu332GyroRouteActive() ? (byte)1 : (byte)0,
                Frame = (uint)_tickCount
            };
        }

        private void SeedAuthoritativeMathLod()
        {
            _authoritativeMathLod = 1;
            _ballastActiveSampleBudget = ResolveBallastActiveSampleBudget(ReadCachedGlobalQualityWeight());
        }

        private void AdvanceDynamicFloodSolver(float fixedDeltaTime)
        {
            if (_floodMassJobPending)
                return;

            _floodSolveAccumulator += math.max(0f, fixedDeltaTime);
            float cadence = FloodSolveCadenceSeconds;
            if (!_floodMassSolveRequested && _floodSolveAccumulator < cadence)
                return;

            if (_floodSolveAccumulator < cadence)
                return;

            _floodSolveAccumulator = 0f;
            _floodMassSolveRequested = false;
            if (_dynamicFloodSignalActive == 0 || _dynamicFloodRoomCount <= 0)
                return;

            if (!TryAcquireFloodRoomInputAliases(
                    out NativeArray<float>.ReadOnly roomWaterLevels,
                    out NativeArray<float>.ReadOnly roomVolumes,
                    out NativeArray<float3>.ReadOnly roomLocalAups,
                    out int roomCount))
            {
                return;
            }

            if (!TryAcquireVaultWrite(
                    in _floodMassOutputHandle,
                    BufferID.SubmarineDynamicFloodMassOutput,
                    1,
                    out NativeArray<DynamicFloodMassOutput> floodMassOutput))
            {
                ReleaseFloodRoomInputVaultLocks();
                return;
            }

            try
            {
                _floodMassHandle = new SubmarineMassSolverJob
                {
                    RoomWaterLevels = roomWaterLevels,
                    RoomVolumes = roomVolumes,
                    RoomLocalAUPs = roomLocalAups,
                    Output = floodMassOutput,
                    RoomCount = roomCount,
                    BaseMassKg = _baseMassKg,
                    BaseCenterOfMassLocal = (float3)(baseCenterOfMassLocal),
                    GlobalPivotAnchor = ResolveGlobalPivotAnchor()
                }.Schedule();
                _floodMassJobPending = true;
                _floodMassOutputVaultLockHeld = true;
            }
            finally
            {
                if (!_floodMassJobPending)
                {
                    ReleaseFloodMassOutputVaultLock();
                    ReleaseFloodRoomInputVaultLocks();
                }
            }
        }

        private bool CompleteFloodMassJob(bool forceComplete, bool commitOutput)
        {
            if (!_floodMassJobPending)
            {
                ReleaseFloodMassOutputVaultLock();
                ReleaseFloodRoomInputVaultLocks();
                return true;
            }

            if (!DispatcherJobSwap.TryComplete(ref _floodMassHandle, forceComplete))
                return false;

            _floodMassJobPending = false;
            try
            {
                if (!commitOutput ||
                    !TryResolveFloodMassOutputLocked(out NativeArray<DynamicFloodMassOutput> floodMassOutput) ||
                    floodMassOutput.Length == 0)
                {
                    return true;
                }

                CommitDynamicFloodMassOutput(floodMassOutput[0]);
                return true;
            }
            finally
            {
                ReleaseFloodMassOutputVaultLock();
                ReleaseFloodRoomInputVaultLocks();
            }
        }

        private void CommitDynamicFloodMassOutput(in DynamicFloodMassOutput output)
        {
            if (_dynamicFloodSignalActive == 0)
                return;

            uint flags = output.Flags;
            if ((flags & PidTelemetryFlagFloodInvalid) != 0u ||
                !math.all(math.isfinite(output.DynamicCenterOfMassLocal)) ||
                !math.all(math.isfinite(output.DynamicCenterOfMassOffsetLocal)) ||
                !math.all(math.isfinite(output.InertiaTensorMultiplier)) ||
                !math.all(math.isfinite(output.GlobalPivotAnchor)) ||
                !math.isfinite(output.TotalWaterMassKg) ||
                !math.isfinite(output.AngularDragMultiplier))
            {
                flags |= PidTelemetryFlagFloodInvalid;
                ResetDynamicFloodState(clearSignalFrame: false);
                _pendingTelemetryFlags |= flags;
                DumpTelemetryOnce(flags);
                return;
            }

            _dynamicFloodCenterOfMassLocal = output.DynamicCenterOfMassLocal;
            _dynamicFloodComOffsetLocal = output.DynamicCenterOfMassOffsetLocal;
            _dynamicFloodInertiaTensorMultiplier = math.max(new float3(1f), output.InertiaTensorMultiplier);
            _dynamicFloodGlobalPivotAnchor = output.GlobalPivotAnchor;
            _dynamicFloodWaterMassKg = math.max(0f, output.TotalWaterMassKg);
            _dynamicFloodAngularDragMultiplier = math.max(1f, output.AngularDragMultiplier);
            float safeBaseMass = math.max(MinimumMassForReciprocal, _baseMassKg);
            _criticalFloodActive = _dynamicFloodWaterMassKg > safeBaseMass * CriticalFloodMassBaseRatio ? (byte)1 : (byte)0;
            if (_criticalFloodActive != 0)
                flags |= PidTelemetryFlagCriticalFlood;

            _pendingTelemetryFlags |= flags;
        }

        private void RequestImpactIntegralReset()
        {
            _pidIntegral = float3.zero;
            _previousPidError = float3.zero;
            _lastPidDerivative = float3.zero;
            _resetIntegralPending = true;
            _pendingTelemetryFlags |= PidTelemetryFlagImpactReset;
        }

        private float AverageBallastFill()
        {
            return SumBallastFill() * 0.25f;
        }

        private float SumBallastFill()
        {
            if (!_ballastSolverJobPending &&
                !_ballastSolverVaultLocksHeld &&
                TryReadBallastTanksReadOnly(out NativeArray<BallastTankDTO>.ReadOnly tanks))
            {
                int tankCount = math.min(TankCount, tanks.Length);
                float sum = 0f;
                for (int i = 0; i < tankCount; i++)
                    sum += ResolveTankFill01(tanks[i]);

                return sum;
            }

            if (!TryReadBallastFillReadOnly(out NativeArray<float>.ReadOnly ballastFill))
                return 0f;

            return ballastFill[TankFront] +
                   ballastFill[TankAft] +
                   ballastFill[TankPort] +
                   ballastFill[TankStarboard];
        }

        private bool WriteTelemetry(uint flags)
        {
            if (_hull == null ||
                !TryAcquireVaultWrite(
                    in _telemetryHandle,
                    BufferID.SubmarinePidTelemetry,
                    TelemetryCapacity,
                    out NativeArray<SubmarinePidTelemetryEntry> telemetry))
            {
                return false;
            }

            uint safeFlags = flags;
            bool dumpAfterWrite = false;
            try
            {
                int index = _telemetryCursor;
                if ((uint)index >= (uint)telemetry.Length)
                    index = 0;

                Vector3 position = _hull.position;
                Vector3 velocity = _hull.linearVelocity;
                Vector3 angularVelocity = _hull.angularVelocity;
                if (!IsFinite(position) ||
                    !IsFinite(velocity) ||
                    !IsFinite(angularVelocity) ||
                    !math.isfinite(_lastIntegralWindup) ||
                    !math.all(math.isfinite(_dynamicFloodComOffsetLocal)) ||
                    !math.all(math.isfinite(_dynamicFloodInertiaTensorMultiplier)) ||
                    !math.all(math.isfinite(_previousPidError)) ||
                    !math.isfinite(_systemStress01) ||
                    !math.isfinite(_dynamicFloodWaterMassKg) ||
                    !math.isfinite(_dynamicFloodAngularDragMultiplier))
                {
                    safeFlags |= PidTelemetryFlagInvalidOutput;
                }

                telemetry[index] = new SubmarinePidTelemetryEntry
                {
                    Frame = _tickCount,
                    RuntimePosition = SnapMillimeter((float3)(position)),
                    LinearVelocity = SnapMillimeter((float3)(velocity)),
                    AngularVelocity = (float3)(angularVelocity),
                    CenterOfMassLocal = _centerOfMassLocal,
                    DynamicFloodComOffsetLocal = _dynamicFloodComOffsetLocal,
                    DynamicFloodInertiaTensorMultiplier = _dynamicFloodInertiaTensorMultiplier,
                    PidError = _previousPidError,
                    BallastWaterMassKg = _ballastWaterMassKg,
                    DynamicFloodWaterMassKg = _dynamicFloodWaterMassKg,
                    DynamicFloodAngularDragMultiplier = _dynamicFloodAngularDragMultiplier,
                    CriticalFloodActive = _criticalFloodActive,
                    LastVaultFaultCode = _lastVaultFaultCode,
                    LastVaultFaultBufferId = _lastVaultFaultBufferId,
                    LastVaultFaultFrame = _lastVaultFaultFrame,
                    IntegralWindup = _lastIntegralWindup,
                    SystemStress01 = _systemStress01,
                    Flags = safeFlags,
                    StateHash = BuildTelemetryHash(
                        position,
                        velocity,
                        angularVelocity,
                        _lastIntegralWindup,
                        _dynamicFloodWaterMassKg,
                        _dynamicFloodComOffsetLocal,
                        _dynamicFloodInertiaTensorMultiplier,
                        _previousPidError,
                        _systemStress01,
                        safeFlags)
                };

                _telemetryCursor = (index + 1) % TelemetryCapacity;
                dumpAfterWrite = (safeFlags & PidTelemetryFlagInvalidOutput) != 0u;
            }
            finally
            {
                ReleaseVaultWrite(in _telemetryHandle);
            }

            if (dumpAfterWrite)
                DumpTelemetryOnce(safeFlags);

            return true;
        }

        private void DumpTelemetryOnce(NativeArray<SubmarinePidTelemetryEntry>.ReadOnly telemetry, uint reasonFlags)
        {
            if (_dumpedTelemetry || !telemetry.IsCreated)
                return;

            _dumpedTelemetry = WriteTelemetryDumpFile(BallastPidDumpRelativePath, telemetry, reasonFlags);
        }

        private void DumpTelemetryOnce(uint reasonFlags)
        {
            if (TryReadTelemetryReadOnly(out NativeArray<SubmarinePidTelemetryEntry>.ReadOnly telemetry))
                DumpTelemetryOnce(telemetry, reasonFlags);
        }

        private void DumpBallastTelemetryOnce(uint reasonFlags)
        {
            if (_dumpedBallastTelemetry ||
                !TryReadBallastTelemetryReadOnly(out NativeArray<SubmarineBallastTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated)
            {
                return;
            }

            _dumpedBallastTelemetry = WriteBallastTelemetryDumpFile(BallastBuoyancyDumpRelativePath, telemetry, reasonFlags);
        }

        private unsafe bool WriteTelemetryDumpFile(
            string relativePath,
            NativeArray<SubmarinePidTelemetryEntry>.ReadOnly telemetry,
            uint reasonFlags)
        {
            const int HeaderBytes = 16;
            const int RowBytes = 126;
            const string dumpPayloadLabel = "SubmarineAutoLevelBallastController.PidTelemetryDumpPayload";
            NativeArray<byte> payload = default;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
                int totalBytes = HeaderBytes + telemetry.Length * RowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(SubmarineAutoLevelBallastController),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);

                Span<byte> header = new Span<byte>(payloadPtr, HeaderBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), 0x53504944u);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), reasonFlags);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), telemetry.Length);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), _telemetryCursor);

                int offset = HeaderBytes;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    Span<byte> row = new Span<byte>(payloadPtr + offset, RowBytes);
                    WritePidTelemetryEntry(row, telemetry[i]);
                    offset += RowBytes;
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SubmarineAutoLevelBallastController),
                    dumpPayloadLabel);
            }
        }

        private unsafe bool WriteBallastTelemetryDumpFile(
            string relativePath,
            NativeArray<SubmarineBallastTelemetryEntry>.ReadOnly telemetry,
            uint reasonFlags)
        {
            const int HeaderBytes = 12;
            const int RowBytes = 64;
            const string dumpPayloadLabel = "SubmarineAutoLevelBallastController.BallastTelemetryDumpPayload";
            NativeArray<byte> payload = default;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
                int totalBytes = HeaderBytes + telemetry.Length * RowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(SubmarineAutoLevelBallastController),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);

                Span<byte> header = new Span<byte>(payloadPtr, HeaderBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), 0x53333333u);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), reasonFlags);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), telemetry.Length);

                int offset = HeaderBytes;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    Span<byte> row = new Span<byte>(payloadPtr + offset, RowBytes);
                    WriteBallastTelemetryEntry(row, telemetry[i]);
                    offset += RowBytes;
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SubmarineAutoLevelBallastController),
                    dumpPayloadLabel);
            }
        }

        private bool EnsureBallastFillCold(out NativeArray<float> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastFill01Handle,
                BufferID.SubmarineBallastFill01,
                TankCount,
                VaultBallastFillFlag,
                out buffer);
        }

        private bool EnsureTankLocalPositionsCold(out NativeArray<float3> buffer)
        {
            return EnsureVaultBufferCold(
                ref _tankLocalPositionsHandle,
                BufferID.SubmarineBallastTankLocalPositions,
                TankCount,
                VaultTankLocalPositionsFlag,
                out buffer);
        }

        private bool EnsurePidOutputCold(out NativeArray<PidJobOutput> buffer)
        {
            return EnsureVaultBufferCold(
                ref _pidOutputHandle,
                BufferID.SubmarineBallastPidOutput,
                1,
                VaultPidOutputFlag,
                out buffer);
        }

        private bool EnsureFloodMassOutputCold(out NativeArray<DynamicFloodMassOutput> buffer)
        {
            return EnsureVaultBufferCold(
                ref _floodMassOutputHandle,
                BufferID.SubmarineDynamicFloodMassOutput,
                1,
                VaultFloodMassOutputFlag,
                out buffer);
        }

        private bool EnsureTelemetryCold(out NativeArray<SubmarinePidTelemetryEntry> buffer)
        {
            return EnsureVaultBufferCold(
                ref _telemetryHandle,
                BufferID.SubmarinePidTelemetry,
                TelemetryCapacity,
                VaultTelemetryFlag,
                out buffer);
        }

        private bool EnsureBallastTanksCold(out NativeArray<BallastTankDTO> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastTanksHandle,
                SubmarineBallastBufferIds.Tanks,
                TankCount,
                VaultBallastTanksFlag,
                NativeArrayOptions.UninitializedMemory,
                out buffer);
        }

        private bool EnsureBallastCommandsCold(out NativeArray<BallastTankCommandDTO> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastCommandsHandle,
                SubmarineBallastBufferIds.Commands,
                TankCount,
                VaultBallastCommandsFlag,
                NativeArrayOptions.UninitializedMemory,
                out buffer);
        }

        private bool EnsureBallastFluidSamplesCold(out NativeArray<SubmarineBallastFluidSampleDTO> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastFluidSamplesHandle,
                SubmarineBallastBufferIds.FluidSamples,
                1,
                VaultBallastFluidSamplesFlag,
                NativeArrayOptions.UninitializedMemory,
                out buffer);
        }

        private bool EnsureBallastForcePacketsCold(out NativeArray<SubmarineBallastForcePacketDTO> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastForcePacketsHandle,
                SubmarineBallastBufferIds.ForcePackets,
                1,
                VaultBallastForcePacketsFlag,
                NativeArrayOptions.UninitializedMemory,
                out buffer);
        }

        private bool EnsureBallastTelemetryCold(out NativeArray<SubmarineBallastTelemetryEntry> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastTelemetryHandle,
                SubmarineBallastBufferIds.TelemetryRing,
                SubmarineBallastConstants.TelemetryCapacity,
                VaultBallastTelemetryFlag,
                NativeArrayOptions.ClearMemory,
                out buffer);
        }

        private bool EnsureBallastTuningCold(out NativeArray<SubmarineBallastTuningDTO> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastTuningHandle,
                SubmarineBallastBufferIds.Tuning,
                1,
                VaultBallastTuningFlag,
                NativeArrayOptions.ClearMemory,
                out buffer);
        }

        private bool EnsureVesselTelemetryCold(out NativeArray<VesselTelemetryEntry> buffer)
        {
            return EnsureVaultBufferCold(
                ref _vesselTelemetryHandle,
                SubmarineBallastBufferIds.VesselTelemetry,
                1,
                VaultVesselTelemetryFlag,
                NativeArrayOptions.ClearMemory,
                out buffer);
        }

        private bool EnsureBallastProfilesCold(out NativeArray<SubmarineBallastProfileDTO> buffer)
        {
            return EnsureVaultBufferCold(
                ref _ballastProfilesHandle,
                SubmarineBallastBufferIds.Profiles,
                SubmarineBallastConstants.ProfileCapacity,
                0,
                NativeArrayOptions.ClearMemory,
                out buffer);
        }

        private bool TryReadBallastFillReadOnly(out NativeArray<float>.ReadOnly buffer)
        {
            return TryReadOnlyVaultBuffer(
                in _ballastFill01Handle,
                BufferID.SubmarineBallastFill01,
                TankCount,
                out buffer);
        }

        public bool TrySubmitSomaticBallastLever(float leverAngleDegrees, uint sourceHash)
        {
            float safeAngle = math.isfinite(leverAngleDegrees) ? math.clamp(leverAngleDegrees, 0f, 90f) : 0f;
            float ballastRatio = math.saturate(safeAngle * (1f / 90f));
            return TryWriteVesselBallastRatio(ballastRatio, sourceHash);
        }

        public bool TryRecordVesselMaintenanceAction(uint panelBitIndex, uint sourceHash)
        {
            if (panelBitIndex >= 64u)
                return false;

            ulong panelMask = 1UL << (int)panelBitIndex;
            return TryWriteVesselMaintenanceAction(panelMask, sourceHash);
        }

        private bool TryWriteVesselBallastRatio(float ballastRatio, uint sourceHash)
        {
            float safeRatio = math.saturate(math.select(1f - math.saturate(neutralBallastFill01), ballastRatio, math.isfinite(ballastRatio)));
            if (!TryAcquireVaultWrite(
                    in _vesselTelemetryHandle,
                    SubmarineBallastBufferIds.VesselTelemetry,
                    1,
                    out NativeArray<VesselTelemetryEntry> vesselTelemetry))
            {
                return false;
            }

            try
            {
                VesselTelemetryEntry entry = vesselTelemetry[0];
                entry.CurrentBallastRatio = safeRatio;
                entry.LastBallastSourceHash = sourceHash;
                vesselTelemetry[0] = entry;
                return true;
            }
            finally
            {
                ReleaseVaultWrite(in _vesselTelemetryHandle);
            }
        }

        private bool TryWriteVesselMaintenanceAction(ulong panelMask, uint sourceHash)
        {
            if (!TryAcquireVaultWrite(
                    in _vesselTelemetryHandle,
                    SubmarineBallastBufferIds.VesselTelemetry,
                    1,
                    out NativeArray<VesselTelemetryEntry> vesselTelemetry))
            {
                return false;
            }

            try
            {
                VesselTelemetryEntry entry = vesselTelemetry[0];
                bool newPanel = (entry.HullCleanlinessMask & panelMask) == 0UL;
                if (newPanel && entry.TotalCareActionsCount < uint.MaxValue)
                    entry.TotalCareActionsCount++;
                entry.HullCleanlinessMask |= panelMask;
                entry.LastCareSourceHash = sourceHash;
                vesselTelemetry[0] = entry;
                return true;
            }
            finally
            {
                ReleaseVaultWrite(in _vesselTelemetryHandle);
            }
        }

        private float ReadVesselBallastRatioOrNeutral()
        {
            if (!TryReadOnlyVaultBuffer(
                    in _vesselTelemetryHandle,
                    SubmarineBallastBufferIds.VesselTelemetry,
                    1,
                    out NativeArray<VesselTelemetryEntry>.ReadOnly vesselTelemetry))
            {
                return 1f - math.saturate(neutralBallastFill01);
            }

            VesselTelemetryEntry entry = vesselTelemetry[0];
            return math.saturate(math.select(1f - math.saturate(neutralBallastFill01), entry.CurrentBallastRatio, math.isfinite(entry.CurrentBallastRatio)));
        }

        private bool TryResolvePidOutputLocked(out NativeArray<PidJobOutput> buffer)
        {
            if (!_pidOutputVaultLockHeld)
            {
                buffer = default;
                return false;
            }

            return TryResolveMutableVaultBuffer(
                ref _pidOutputHandle,
                BufferID.SubmarineBallastPidOutput,
                1,
                out buffer);
        }

        private bool TryResolveFloodMassOutputLocked(out NativeArray<DynamicFloodMassOutput> buffer)
        {
            if (!_floodMassOutputVaultLockHeld)
            {
                buffer = default;
                return false;
            }

            return TryResolveMutableVaultBuffer(
                ref _floodMassOutputHandle,
                BufferID.SubmarineDynamicFloodMassOutput,
                1,
                out buffer);
        }

        private bool TryReadTelemetryReadOnly(out NativeArray<SubmarinePidTelemetryEntry>.ReadOnly buffer)
        {
            return TryReadOnlyVaultBuffer(
                in _telemetryHandle,
                BufferID.SubmarinePidTelemetry,
                TelemetryCapacity,
                out buffer);
        }

        private bool TryReadTankLocalPositionsReadOnly(out NativeArray<float3>.ReadOnly buffer)
        {
            return TryReadOnlyVaultBuffer(
                in _tankLocalPositionsHandle,
                BufferID.SubmarineBallastTankLocalPositions,
                TankCount,
                out buffer);
        }

        private bool TryReadBallastTanksReadOnly(out NativeArray<BallastTankDTO>.ReadOnly buffer)
        {
            return TryReadOnlyVaultBuffer(
                in _ballastTanksHandle,
                SubmarineBallastBufferIds.Tanks,
                TankCount,
                out buffer);
        }

        private bool TryResolveBallastTanksLocked(out NativeArray<BallastTankDTO> buffer)
        {
            if (!_ballastSolverVaultLocksHeld)
            {
                buffer = default;
                return false;
            }

            return TryResolveBallastSolverLockedBuffer(
                ref _ballastTanksHandle,
                SubmarineBallastBufferIds.Tanks,
                TankCount,
                out buffer);
        }

        private bool TryResolveBallastForcePacketsLocked(out NativeArray<SubmarineBallastForcePacketDTO> buffer)
        {
            if (!_ballastSolverVaultLocksHeld)
            {
                buffer = default;
                return false;
            }

            return TryResolveBallastSolverLockedBuffer(
                ref _ballastForcePacketsHandle,
                SubmarineBallastBufferIds.ForcePackets,
                1,
                out buffer);
        }

        private bool TryReadBallastTelemetryReadOnly(out NativeArray<SubmarineBallastTelemetryEntry>.ReadOnly buffer)
        {
            return TryReadOnlyVaultBuffer(
                in _ballastTelemetryHandle,
                SubmarineBallastBufferIds.TelemetryRing,
                SubmarineBallastConstants.TelemetryCapacity,
                out buffer);
        }

        private bool TryAcquireBallastSolverJobBuffers(
            out NativeArray<BallastTankDTO> tanks,
            out NativeArray<BallastTankCommandDTO> commands,
            out NativeArray<SubmarineBallastFluidSampleDTO> samples,
            out NativeArray<SubmarineBallastForcePacketDTO> forcePackets,
            out NativeArray<SubmarineBallastTelemetryEntry> telemetry,
            out NativeArray<VesselTelemetryEntry> vesselTelemetry)
        {
            tanks = default;
            commands = default;
            samples = default;
            forcePackets = default;
            telemetry = default;
            vesselTelemetry = default;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                RecordVaultFault(SubmarineBallastBufferIds.Tanks, VaultFaultCodeMissing, PidTelemetryFlagDataVaultMissing);
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                RecordVaultFault(SubmarineBallastBufferIds.Tanks, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            if (!vault.TryAcquireMutationGuard(BallastSolverMutationGuardMask))
            {
                RecordVaultFault(SubmarineBallastBufferIds.Tanks, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            bool success = false;
            try
            {
                if (!TryResolveBallastSolverGuardedBuffer(vault, ref _ballastTanksHandle, SubmarineBallastBufferIds.Tanks, TankCount, out tanks))
                    return false;

                if (!TryResolveBallastSolverGuardedBuffer(vault, ref _ballastCommandsHandle, SubmarineBallastBufferIds.Commands, TankCount, out commands))
                    return false;

                if (!TryResolveBallastSolverGuardedBuffer(vault, ref _ballastFluidSamplesHandle, SubmarineBallastBufferIds.FluidSamples, 1, out samples))
                    return false;

                if (!TryResolveBallastSolverGuardedBuffer(vault, ref _ballastForcePacketsHandle, SubmarineBallastBufferIds.ForcePackets, 1, out forcePackets))
                    return false;

                if (!TryResolveBallastSolverGuardedBuffer(
                        vault,
                        ref _ballastTelemetryHandle,
                        SubmarineBallastBufferIds.TelemetryRing,
                        SubmarineBallastConstants.TelemetryCapacity,
                        out telemetry))
                    return false;

                if (!TryResolveVehiclesPhysicsVaultBuffer(
                        vault,
                        ref _vesselTelemetryHandle,
                        SubmarineBallastBufferIds.VesselTelemetry,
                        1,
                        out vesselTelemetry))
                {
                    RecordVaultFault(SubmarineBallastBufferIds.VesselTelemetry, VaultFaultCodeInvalidView, PidTelemetryFlagVaultViewInvalid);
                    return false;
                }

                _ballastSolverGuardVault = vault;
                _ballastSolverVaultLocksHeld = true;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    vault.ReleaseMutationGuard(BallastSolverMutationGuardMask);
                    _ballastSolverGuardVault = null;
                    _ballastSolverVaultLocksHeld = false;
                    tanks = default;
                    commands = default;
                    samples = default;
                    forcePackets = default;
                    telemetry = default;
                    vesselTelemetry = default;
                }
            }
        }

        private bool TryResolveBallastSolverLockedBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (!_ballastSolverVaultLocksHeld || _ballastSolverGuardVault == null)
                return false;

            return TryResolveBallastSolverGuardedBuffer(
                _ballastSolverGuardVault,
                ref handle,
                bufferId,
                requiredLength,
                out buffer);
        }

        private bool TryResolveBallastSolverGuardedBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
            {
                RecordVaultFault(bufferId, VaultFaultCodeMissing, PidTelemetryFlagDataVaultMissing);
                return false;
            }

            if (TryResolveVehiclesPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (!vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> refreshed) ||
                !TryResolveVehiclesPhysicsVaultBuffer(vault, ref refreshed, bufferId, requiredLength, out buffer))
            {
                handle = default;
                buffer = default;
                RecordVaultFault(bufferId, VaultFaultCodeInvalidView, PidTelemetryFlagVaultViewInvalid);
                return false;
            }

            handle = refreshed;
            return true;
        }

        private void ReleaseBallastSolverVaultLocks()
        {
            if (!_ballastSolverVaultLocksHeld)
                return;

            IDataVault vault = _ballastSolverGuardVault ?? ResolveDataVault();
            _ballastSolverGuardVault = null;
            _ballastSolverVaultLocksHeld = false;
            if (vault != null)
                vault.ReleaseMutationGuard(BallastSolverMutationGuardMask);
        }

        private void ReleasePidOutputVaultLock()
        {
            if (!_pidOutputVaultLockHeld)
                return;

            ReleaseVaultWrite(in _pidOutputHandle);
            _pidOutputVaultLockHeld = false;
        }

        private void ReleaseFloodMassOutputVaultLock()
        {
            if (!_floodMassOutputVaultLockHeld)
                return;

            ReleaseVaultWrite(in _floodMassOutputHandle);
            _floodMassOutputVaultLockHeld = false;
        }

        private void ReleaseFloodRoomInputVaultLocks()
        {
            IDataVault vault = _floodRoomInputGuardVault;
            if (vault == null)
                return;

            _floodRoomInputGuardVault = null;
            vault.ReleaseMutationGuard(FloodRoomInputMutationGuardMask);
        }

        private bool TryAcquireVaultWrite<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || requiredLength <= 0 || !IsVehiclesPhysicsVaultHandle(in handle, bufferId))
            {
                RecordVaultFault(bufferId, VaultFaultCodeMissing, PidTelemetryFlagDataVaultMissing);
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                RecordVaultFault(bufferId, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            bool lockAcquired = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer))
                {
                    buffer = default;
                    RecordVaultFault(bufferId, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                    return false;
                }

                lockAcquired = true;
                bool fencedAfterAcquire = vault.IsCompactionFenceActive;
                if (!buffer.IsCreated || buffer.Length < requiredLength || fencedAfterAcquire)
                {
                    RecordVaultFault(
                        bufferId,
                        fencedAfterAcquire ? VaultFaultCodeContention : VaultFaultCodeInvalidView,
                        fencedAfterAcquire ? PidTelemetryFlagVaultWriteContention : PidTelemetryFlagVaultViewInvalid);
                    return false;
                }

                lockAcquired = false;
                return true;
            }
            finally
            {
                if (lockAcquired)
                {
                    vault.ReleaseWriteLock(in handle, OwnerSystem);
                    buffer = default;
                }
            }
        }

        private void ReleaseVaultWrite<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.Generation == 0u)
                return;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return;

            vault.ReleaseWriteLock(in handle, OwnerSystem);
        }

        private void RecordVaultFault(BufferID bufferId, byte faultCode, uint telemetryFlag)
        {
            _lastVaultFaultCode = faultCode;
            _lastVaultFaultBufferId = unchecked((uint)(int)bufferId);
            _lastVaultFaultFrame = unchecked((uint)_tickCount);
            _pendingTelemetryFlags |= telemetryFlag;
        }

        private bool TryReadShinobu332GyroCountersCached(out NativeArray<SubmarineGyroCounterDTO>.ReadOnly buffer)
        {
            return TryReadExternalReadOnlyVaultBuffer(
                in _shinobu332GyroCounterHandle,
                BufferID.Shinobu332GyroCounters,
                1,
                out buffer);
        }

        private bool TryAcquireFloodRoomInputAliases(
            out NativeArray<float>.ReadOnly roomWaterLevels,
            out NativeArray<float>.ReadOnly roomVolumes,
            out NativeArray<float3>.ReadOnly roomLocalAups,
            out int roomCount)
        {
            roomWaterLevels = default;
            roomVolumes = default;
            roomLocalAups = default;
            roomCount = 0;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                RecordVaultFault(BufferID.RoomWaterLevels, VaultFaultCodeMissing, PidTelemetryFlagDataVaultMissing);
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                RecordVaultFault(BufferID.RoomWaterLevels, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            if (!vault.TryAcquireMutationGuard(FloodRoomInputMutationGuardMask))
            {
                RecordVaultFault(BufferID.RoomWaterLevels, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            bool success = false;
            try
            {
                if (!TryResolveFloodRoomInputReadOnly(
                        vault,
                        ref _roomWaterLevelsHandle,
                        BufferID.RoomWaterLevels,
                        1,
                        out roomWaterLevels))
                {
                    return false;
                }

                if (!TryResolveFloodRoomInputReadOnly(
                        vault,
                        ref _roomVolumesHandle,
                        BufferID.RoomVolumes,
                        1,
                        out roomVolumes))
                {
                    return false;
                }

                if (!TryResolveFloodRoomInputReadOnly(
                        vault,
                        ref _roomLocalAUPsHandle,
                        BufferID.RoomLocalAUPs,
                        1,
                        out roomLocalAups))
                {
                    return false;
                }

                int bufferRoomCount = math.min(roomWaterLevels.Length, math.min(roomVolumes.Length, roomLocalAups.Length));
                roomCount = math.min(_dynamicFloodRoomCount, bufferRoomCount);
                if (roomCount <= 0)
                {
                    RecordVaultFault(BufferID.RoomWaterLevels, VaultFaultCodeInvalidView, PidTelemetryFlagVaultViewInvalid);
                    return false;
                }

                _floodRoomInputGuardVault = vault;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    vault.ReleaseMutationGuard(FloodRoomInputMutationGuardMask);
                    roomWaterLevels = default;
                    roomVolumes = default;
                    roomLocalAups = default;
                    roomCount = 0;
                    _floodRoomInputGuardVault = null;
                }
            }
        }

        private bool TryResolveFloodRoomInputReadOnly<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
            {
                RecordVaultFault(bufferId, VaultFaultCodeMissing, PidTelemetryFlagDataVaultMissing);
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                RecordVaultFault(bufferId, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            if (!IsVehiclesPhysicsVaultHandle(in handle, bufferId))
            {
                if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> refreshed) ||
                    !IsVehiclesPhysicsVaultHandle(in refreshed, bufferId))
                {
                    handle = default;
                    RecordVaultFault(bufferId, VaultFaultCodeInvalidView, PidTelemetryFlagVaultViewInvalid);
                    return false;
                }

                handle = refreshed;
            }

            if (!vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> refreshed) ||
                    !IsVehiclesPhysicsVaultHandle(in refreshed, bufferId) ||
                    !vault.TryReadOnlyHandle(in refreshed, out buffer) ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    handle = default;
                    buffer = default;
                    RecordVaultFault(bufferId, VaultFaultCodeInvalidView, PidTelemetryFlagVaultViewInvalid);
                    return false;
                }

                handle = refreshed;
            }

            if (vault.IsCompactionFenceActive)
            {
                buffer = default;
                RecordVaultFault(bufferId, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            return true;
        }

        private bool EnsureVaultBufferCold<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            int vaultFlag,
            out NativeArray<T> buffer)
            where T : struct
        {
            return EnsureVaultBufferCold(
                ref handle,
                bufferId,
                requiredLength,
                vaultFlag,
                NativeArrayOptions.ClearMemory,
                out buffer);
        }

        private bool EnsureVaultBufferCold<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            int vaultFlag,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                _pendingTelemetryFlags |= PidTelemetryFlagDataVaultMissing;
                handle = default;
                _vaultNativeStateMask &= ~vaultFlag;
                return false;
            }

            if (!TryResolveVehiclesPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
            {
                if (vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                        !TryResolveVehiclesPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                    {
                        handle = default;
                        _pendingTelemetryFlags |= PidTelemetryFlagDataVaultMissing;
                        _vaultNativeStateMask &= ~vaultFlag;
                        return false;
                    }
                }
                else
                {
                    handle = vault.EnsureGenerationHandle<T>(
                        bufferId,
                        requiredLength,
                        OwnerSystem,
                        options);
                    if (!TryResolveVehiclesPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                    {
                        handle = default;
                        _pendingTelemetryFlags |= PidTelemetryFlagDataVaultMissing;
                        _vaultNativeStateMask &= ~vaultFlag;
                        return false;
                    }
                }
            }

            _vaultNativeStateMask |= vaultFlag;
            return true;
        }

        private bool TryResolveMutableVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveVehiclesPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            buffer = default;
            return false;
        }

        private bool TryReadOnlyVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsVehiclesPhysicsVaultHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryReadExternalReadOnlyVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsVaultHandleForBuffer(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryRefreshExistingReadOnlyVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                handle = default;
                RecordVaultFault(bufferId, VaultFaultCodeMissing, PidTelemetryFlagDataVaultMissing);
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                RecordVaultFault(bufferId, VaultFaultCodeContention, PidTelemetryFlagVaultWriteContention);
                return false;
            }

            if (IsVaultHandleForBuffer(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length > 0)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> refreshed) &&
                IsVaultHandleForBuffer(in refreshed, bufferId) &&
                vault.TryReadOnlyHandle(in refreshed, out buffer) &&
                buffer.IsCreated &&
                buffer.Length > 0)
            {
                handle = refreshed;
                return true;
            }

            buffer = default;
            handle = default;
            RecordVaultFault(bufferId, VaultFaultCodeInvalidView, PidTelemetryFlagVaultViewInvalid);
            return false;
        }

        private static bool TryResolveVehiclesPhysicsVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsVehiclesPhysicsVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static ulong BallastMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 63);
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastFill01Handle, BufferID.SubmarineBallastFill01);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _tankLocalPositionsHandle, BufferID.SubmarineBallastTankLocalPositions);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _pidOutputHandle, BufferID.SubmarineBallastPidOutput);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _floodMassOutputHandle, BufferID.SubmarineDynamicFloodMassOutput);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _telemetryHandle, BufferID.SubmarinePidTelemetry);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastTanksHandle, SubmarineBallastBufferIds.Tanks);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastCommandsHandle, SubmarineBallastBufferIds.Commands);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastFluidSamplesHandle, SubmarineBallastBufferIds.FluidSamples);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastForcePacketsHandle, SubmarineBallastBufferIds.ForcePackets);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastTelemetryHandle, SubmarineBallastBufferIds.TelemetryRing);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastTuningHandle, SubmarineBallastBufferIds.Tuning);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastProfilesHandle, SubmarineBallastBufferIds.Profiles);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _vesselTelemetryHandle, SubmarineBallastBufferIds.VesselTelemetry);
        }

        private static void ReleaseVehiclesPhysicsVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            if (vault != null && IsVehiclesPhysicsVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsVehiclesPhysicsVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static bool IsVaultHandleForBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u;
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private static uint BuildTelemetryHash(
            Vector3 position,
            Vector3 velocity,
            Vector3 angularVelocity,
            float integralWindup,
            float dynamicFloodWaterMassKg,
            float3 dynamicFloodOffsetLocal,
            float3 dynamicFloodInertiaTensorMultiplier,
            float3 pidError,
            float systemStress01,
            uint flags)
        {
            uint hash = 2166136261u;
            hash = Hash(hash, Quantize(position.x));
            hash = Hash(hash, Quantize(position.y));
            hash = Hash(hash, Quantize(position.z));
            hash = Hash(hash, Quantize(velocity.x));
            hash = Hash(hash, Quantize(velocity.y));
            hash = Hash(hash, Quantize(velocity.z));
            hash = Hash(hash, Quantize(angularVelocity.x));
            hash = Hash(hash, Quantize(angularVelocity.y));
            hash = Hash(hash, Quantize(angularVelocity.z));
            hash = Hash(hash, Quantize(integralWindup));
            hash = Hash(hash, Quantize(dynamicFloodWaterMassKg));
            hash = Hash(hash, Quantize(dynamicFloodOffsetLocal.x));
            hash = Hash(hash, Quantize(dynamicFloodOffsetLocal.y));
            hash = Hash(hash, Quantize(dynamicFloodOffsetLocal.z));
            hash = Hash(hash, Quantize(dynamicFloodInertiaTensorMultiplier.x));
            hash = Hash(hash, Quantize(dynamicFloodInertiaTensorMultiplier.y));
            hash = Hash(hash, Quantize(dynamicFloodInertiaTensorMultiplier.z));
            hash = Hash(hash, Quantize(pidError.x));
            hash = Hash(hash, Quantize(pidError.y));
            hash = Hash(hash, Quantize(pidError.z));
            hash = Hash(hash, Quantize(systemStress01));
            return Hash(hash, flags);
        }

        private static uint Hash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private static uint Quantize(float value)
        {
            if (!math.isfinite(value))
                return 0xffffffffu;

            int quantized = (int)math.round(value * 1000f);
            return unchecked((uint)quantized);
        }

        private static float3 SnapMillimeter(float3 value)
        {
            return new float3(
                DeterministicContractMath.SnapMillimeter(value.x),
                DeterministicContractMath.SnapMillimeter(value.y),
                DeterministicContractMath.SnapMillimeter(value.z));
        }

        private static float3 FastNlerp(float3 from, float3 to, float blend01)
        {
            if (!math.all(math.isfinite(from)) || !math.all(math.isfinite(to)))
                return math.all(math.isfinite(to)) ? to : float3.zero;

            float t = math.saturate(blend01);
            float3 value = from + ((to - from) * t);
            float maxMagnitude = math.max(ApproximateMagnitudeNoSqrt(from), ApproximateMagnitudeNoSqrt(to));
            if (maxMagnitude <= 0.000001f)
                return value;

            float valueLengthSq = math.lengthsq(value);
            if (valueLengthSq <= 0.000001f)
                return float3.zero;

            return value * (maxMagnitude * math.rsqrt(math.max(valueLengthSq, 0.000001f)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private bool RefreshRuntimeOriginAupSnapshotCold()
        {
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
            {
                _runtimeOriginAupCached = 0;
                return false;
            }

            _cachedRuntimeOriginAup = originAup;
            _runtimeOriginAupCached = 1;
            return true;
        }

        private bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            if (_runtimeOriginAupCached == 0)
                return false;

            AbsoluteUniversePosition originAup = _cachedRuntimeOriginAup;
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void WritePidTelemetryEntry(Span<byte> destination, in SubmarinePidTelemetryEntry entry)
        {
            destination.Clear();
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), entry.Flags);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.IntegralWindup);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.SystemStress01);
            WriteFloat3LittleEndian(destination.Slice(20, 12), entry.RuntimePosition);
            WriteFloat3LittleEndian(destination.Slice(32, 12), entry.LinearVelocity);
            WriteFloat3LittleEndian(destination.Slice(44, 12), entry.AngularVelocity);
            WriteFloat3LittleEndian(destination.Slice(56, 12), entry.CenterOfMassLocal);
            WriteFloat3LittleEndian(destination.Slice(68, 12), entry.DynamicFloodComOffsetLocal);
            WriteFloat3LittleEndian(destination.Slice(80, 12), entry.DynamicFloodInertiaTensorMultiplier);
            WriteFloat3LittleEndian(destination.Slice(92, 12), entry.PidError);
            WriteFloatLittleEndian(destination.Slice(104, 4), entry.BallastWaterMassKg);
            WriteFloatLittleEndian(destination.Slice(108, 4), entry.DynamicFloodWaterMassKg);
            WriteFloatLittleEndian(destination.Slice(112, 4), entry.DynamicFloodAngularDragMultiplier);
            destination[116] = entry.CriticalFloodActive;
            destination[117] = entry.LastVaultFaultCode;
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(118, 4), entry.LastVaultFaultBufferId);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(122, 4), entry.LastVaultFaultFrame);
        }

        private static void WriteBallastTelemetryEntry(Span<byte> destination, in SubmarineBallastTelemetryEntry entry)
        {
            destination.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), entry.StateHash);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.NetForceY);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.BuoyantForceY);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.BallastGravityForceY);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.WaterLiters);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.CompressedAirMassKg);
            WriteFloatLittleEndian(destination.Slice(32, 4), entry.AmbientPressureATM);
            WriteFloatLittleEndian(destination.Slice(36, 4), entry.DisplacedVolumeCubicMeters);
            WriteFloatLittleEndian(destination.Slice(40, 4), entry.SubmergedRatio);
            WriteFloatLittleEndian(destination.Slice(44, 4), entry.ComputeMicros);
            WriteFloatLittleEndian(destination.Slice(48, 4), entry.GlobalQualityWeight);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(52, 4), entry.ActiveSamples);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(56, 4), entry.TargetEntityHash);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(60, 4), entry.RingCursor);
        }

        private static void WriteFloat3LittleEndian(Span<byte> destination, float3 value)
        {
            WriteFloatLittleEndian(destination.Slice(0, 4), value.x);
            WriteFloatLittleEndian(destination.Slice(4, 4), value.y);
            WriteFloatLittleEndian(destination.Slice(8, 4), value.z);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private void OnValidate()
        {
            proportionalGain = Mathf.Max(0f, proportionalGain);
            integralGain = Mathf.Max(0f, integralGain);
            derivativeGain = Mathf.Max(0f, derivativeGain);
            neutralBallastFill01 = Mathf.Clamp01(neutralBallastFill01);
            ballastTankVolumeCubicMeters = Mathf.Max(0.01f, ballastTankVolumeCubicMeters);
            pumpFillRate01PerSecond = Mathf.Max(0f, pumpFillRate01PerSecond);
            ballastBlowRate01PerSecond = Mathf.Max(0f, ballastBlowRate01PerSecond);
            pumpEnergyWattSecondsPerFill01 = Mathf.Max(0f, pumpEnergyWattSecondsPerFill01);
            maxCommandBallastBias01 = Mathf.Clamp(maxCommandBallastBias01, 0f, 0.45f);
            airReleaseAudioFillDeltaThreshold = Mathf.Max(0f, airReleaseAudioFillDeltaThreshold);
            ballastHullVolumeCubicMeters = Mathf.Max(0.1f, ballastHullVolumeCubicMeters);
            ballastHullHeightMeters = Mathf.Max(0.1f, ballastHullHeightMeters);
            airBankPressureATM = Mathf.Max(1f, airBankPressureATM);
            floodPidPitchBiasPerMeter = Mathf.Max(0f, floodPidPitchBiasPerMeter);
            floodComStressAudioThresholdMeters = Mathf.Max(0f, floodComStressAudioThresholdMeters);
            floodAngularDampingFloor = Mathf.Max(0f, floodAngularDampingFloor);
            floodStressAudioCooldownSeconds = Mathf.Max(0.05f, floodStressAudioCooldownSeconds);
            pidHullStressAudioCooldownSeconds = Mathf.Max(0.05f, pidHullStressAudioCooldownSeconds);
            criticalFloodHapticCooldownSeconds = Mathf.Max(0.05f, criticalFloodHapticCooldownSeconds);
            criticalFloodPitchDegrees = Mathf.Clamp(criticalFloodPitchDegrees, 0f, 89f);
            tailHeavyBubblePitchDegrees = Mathf.Clamp(tailHeavyBubblePitchDegrees, 0f, 89f);
            tailHeavyBubbleCooldownSeconds = Mathf.Max(0.05f, tailHeavyBubbleCooldownSeconds);
            pidTorqueFastNlerp01 = Mathf.Clamp(pidTorqueFastNlerp01, 0.05f, 1f);
            combatTargetHealth = Mathf.Max(0f, combatTargetHealth);
            massiveImpactDamageThreshold = Mathf.Max(0f, massiveImpactDamageThreshold);
            combatArmorValue = Mathf.Max(0f, combatArmorValue);
            integralClamp = Mathf.Max(0f, integralClamp);
            maxTorqueNewtons = Mathf.Max(0f, maxTorqueNewtons);
        }
    }
}
