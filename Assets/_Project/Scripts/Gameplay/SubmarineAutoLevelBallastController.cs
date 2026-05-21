using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Physics;
using Hecton8.Physics.Determinism;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineCoreDirector))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Auto-Level Ballast Controller")]
    public sealed class SubmarineAutoLevelBallastController : MonoBehaviour,
        IFixedTickable,
        IPostFixedTickable,
        ISlowTickable,
        IOriginShiftListener,
        IVehicleCommandSignalListener,
        ICombatDamageEventListener,
        IDamageReceiver,
        ICombatPushbackBodySource,
        ICombatHitProfileSource,
        IGlobalRegistryHotSwapListener,
        IScalabilityChangedEventListener,
        ISubmarineState
    {
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
            [FieldOffset(117)] private byte _pad0;
            [FieldOffset(118)] private ushort _pad1;
            [FieldOffset(120)] private ulong _pad2;
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
            [ReadOnly, NoAlias] public NativeArray<WhirlpoolFlow> ActiveMaelstroms;
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

                float3 maelstromAcceleration = HectonAnalyticalFlowField.SampleWhirlpoolVelocity(
                    PositionWS,
                    ActiveMaelstroms,
                    ActiveMaelstromCount,
                    MaelstromApproximationTier,
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SubmarineMassSolverJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<float> RoomWaterLevels;
            [ReadOnly, NoAlias] public NativeArray<float> RoomVolumes;
            [ReadOnly, NoAlias] public NativeArray<float3> RoomLocalAUPs;
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
        private const string BallastPidDumpRelativePath = "Docs/AgentLogs/Dump_SUBMARINE_BALLAST_PID_V2.bin";
        private const float WaterDensityKgPerCubicMeter = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
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
        private IDataVault _dataVault;
        private HectonFluidEngine _fluid;
        private Rigidbody _hull;
        private Transform _cachedTransform;
        private SubmarineStateSnapshot _snapshot;
        private VehicleCommandSignal _pendingCommand;
        private bool _commandDirty;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredSlowTick;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _registeredScalabilityListener;
        private bool _registeredCombatListener;
        private bool _registeredCombatTarget;
        private bool _registeredState;
        private bool _pidJobPending;
        private bool _floodMassJobPending;
        private bool _floodMassSolveRequested;
        private bool _resetIntegralPending;
        private bool _dumpedTelemetry;
        private byte _pumpPowered = 1;
        private byte _authoritativeMathLod;
        private int _targetInstanceId;
        private int _fallbackInstanceId;
        private int _tickCount;
        private int _telemetryCursor;
        private float _baseMassKg = 1200f;
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
        private float _systemStress01;
        private uint _lastFloodSignalFrame;
        private float _floodSignalAgeSeconds;
        private int _dynamicFloodRoomCount;
        private byte _hasFloodSignalFrame;
        private byte _dynamicFloodSignalActive;
        private byte _criticalFloodActive;
        private int _vaultNativeStateMask;
        private uint _pendingTelemetryFlags;
        private JobHandle _pidHandle;
        private JobHandle _floodMassHandle;

        private VaultGenerationHandle<float> _ballastFill01Handle;
        private VaultGenerationHandle<float3> _tankLocalPositionsHandle;
        private VaultGenerationHandle<PidJobOutput> _pidOutputHandle;
        private VaultGenerationHandle<DynamicFloodMassOutput> _floodMassOutputHandle;
        private VaultGenerationHandle<SubmarinePidTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<float> _roomWaterLevelsHandle;
        private VaultGenerationHandle<float> _roomVolumesHandle;
        private VaultGenerationHandle<float3> _roomLocalAUPsHandle;

        public bool SuppressesKinematicPitch => isActiveAndEnabled && autoLevelEnabled;

        public int TickCount => _tickCount;

        public Rigidbody CombatPushbackBody => _hull;

        public Vector3 CombatForward => _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;

        public float CombatHeight => 2.8f;

        public NativeArray<float>.ReadOnly BallastFill01 =>
            TryResolveBallastFill(out NativeArray<float> ballastFill) ? ballastFill.AsReadOnly() : default;

        public SubmarineStateSnapshot StateSnapshot => _snapshot;

        private void Awake()
        {
            CacheReferences();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshTargetInstanceId();
            SeedAuthoritativeMathLod();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshTargetInstanceId();
            SeedAuthoritativeMathLod();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            CompleteFloodMassJob(forceComplete: true, commitOutput: false);
            CompletePidJob(forceComplete: true, commitOutput: false);
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
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
            AdvanceBallast(in command, fixedDeltaTime);
            ApplyMassDistribution();
            ApplyDynamicFloodDragTensor();
            EmitDynamicFloodFeedback(fixedDeltaTime);
            RefreshSnapshot();
            WriteTelemetry(_pendingTelemetryFlags);
            _pendingTelemetryFlags = 0u;
            SchedulePidJob(fixedDeltaTime);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            CompleteFloodMassJob(forceComplete: false, commitOutput: true);
            CompletePidJob(forceComplete: false, commitOutput: true);
        }

        public void SlowTick()
        {
            EnsureNativeState();
            RefreshTankPositions();
            RefreshRoomBufferAliases();
            _floodMassSolveRequested = true;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (shiftData.ShiftOffset.sqrMagnitude <= 0.000001f)
                return;

            _previousPidError = float3.zero;
            _lastPidDerivative = float3.zero;
            _resetIntegralPending = true;
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

        public void OnCombatDamageResolved(in CombatDamageResult result)
        {
            if (result.TargetId != _targetInstanceId)
                return;

            if ((result.DamageType & CombatDamageTypes.Impact) == 0u)
                return;

            if (result.AppliedDamage < massiveImpactDamageThreshold)
                return;

            RequestImpactIntegralReset();
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
                _audio = currentService as IAudioService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _fluid = currentService as HectonFluidEngine;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteFloodMassJob(forceComplete: true, commitOutput: false);
                CompletePidJob(forceComplete: true, commitOutput: false);
                DisposeNativeState();
                _dataVault = currentService as IDataVault;
                EnsureNativeState();
                RefreshTankPositions();
                RefreshRoomBufferAliases();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SubmarineState && currentService == null)
                TryRegisterStateReadModel();
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _authoritativeMathLod = 1;
        }

        private void RegisterRuntime()
        {
            _powerGrid = GlobalRegistry.PowerGrid;
            _audio = GlobalRegistry.Audio;
            _fluid = GlobalRegistry.Fluid;
            RefreshDynamicFloodServicesFromRegistry();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshRoomBufferAliases();

            TryRegisterStateReadModel();
            SetFluidDynamicsCenterAuthority(true);

            if (!_registeredFixed && GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player))
                _registeredFixed = true;

            if (!_registeredPostFixed && GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player))
                _registeredPostFixed = true;

            if (!_registeredSlowTick && GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player))
                _registeredSlowTick = true;

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (!_registeredHotSwap && GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwap = true;

            if (!_registeredScalabilityListener)
            {
                ScalabilityEvents.Register(this);
                _registeredScalabilityListener = true;
            }

            VehicleCommandSignalBus.Register(this);

            if (!_registeredCombatListener)
            {
                CombatDamageRuntime.Register(this);
                _registeredCombatListener = true;
            }

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

            if (_registeredCombatTarget)
            {
                CombatDamageRuntime.UnregisterTarget(_targetInstanceId, this);
                _registeredCombatTarget = false;
            }

            if (_registeredCombatListener)
            {
                CombatDamageRuntime.Unregister(this);
                _registeredCombatListener = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            if (_registeredScalabilityListener)
            {
                ScalabilityEvents.Unregister(this);
                _registeredScalabilityListener = false;
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
            _audio = null;
            _fluid = null;
            ResetDynamicFloodState(clearSignalFrame: true);
            RestoreDynamicFloodAngularDrag();
            RestoreDynamicFloodInertiaTensor();
            ResetExternalFloodDragTensor();
        }

        private void CacheReferences()
        {
            _cachedTransform = transform;
            if (_core == null)
                TryGetComponent(out _core);
            if (_hull == null)
                TryGetComponent(out _hull);

            _powerGrid = GlobalRegistry.PowerGrid;

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

        private void EnsureNativeState()
        {
            bool seedBallast = (_vaultNativeStateMask & VaultBallastFillFlag) == 0;
            if (TryResolveBallastFill(out NativeArray<float> ballastFill) && seedBallast)
            {
                for (int i = 0; i < TankCount; i++)
                    ballastFill[i] = math.saturate(neutralBallastFill01);
            }

            TryResolveTankLocalPositions(out _);
            TryResolvePidOutput(out _);
            TryResolveFloodMassOutput(out _);
            TryResolveTelemetry(out _);
        }

        private void DisposeNativeState()
        {
            ReleaseOwnedVaultHandles(_dataVault);
            _ballastFill01Handle = default;
            _tankLocalPositionsHandle = default;
            _pidOutputHandle = default;
            _floodMassOutputHandle = default;
            _telemetryHandle = default;
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

        private bool RefreshRoomBufferAliases()
        {
            return TryResolveRoomBuffers(
                out NativeArray<float> roomWaterLevels,
                out NativeArray<float> roomVolumes,
                out NativeArray<float3> roomLocalAups) &&
                roomWaterLevels.IsCreated &&
                roomVolumes.IsCreated &&
                roomLocalAups.IsCreated;
        }

        private void RefreshTankPositions()
        {
            if (!TryResolveTankLocalPositions(out NativeArray<float3> tankLocalPositions) || tankLocalPositions.Length < TankCount)
                return;

            tankLocalPositions[TankFront] = ToFloat3(frontTankLocalPosition);
            tankLocalPositions[TankAft] = ToFloat3(aftTankLocalPosition);
            tankLocalPositions[TankPort] = ToFloat3(portTankLocalPosition);
            tankLocalPositions[TankStarboard] = ToFloat3(starboardTankLocalPosition);
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
            _dynamicFloodCenterOfMassLocal = ToFloat3(baseCenterOfMassLocal);
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

        private void AdvanceBallast(in VehicleCommandSignal command, float fixedDeltaTime)
        {
            if (!TryResolveBallastFill(out NativeArray<float> ballastFill))
                return;

            float neutral = math.saturate(neutralBallastFill01);
            float pitch = math.clamp(command.Pitch, -1f, 1f);
            float totalBias = math.clamp(command.BallastDelta, -maxCommandBallastBias01, maxCommandBallastBias01);
            float pitchBias = pitch * math.max(0f, maxCommandBallastBias01);

            float targetFront = math.saturate(neutral + totalBias + pitchBias);
            float targetAft = math.saturate(neutral + totalBias - pitchBias);
            float targetPort = math.saturate(neutral + totalBias);
            float targetStarboard = math.saturate(neutral + totalBias);

            float d0 = ResolveFillDelta(ballastFill[TankFront], targetFront, fixedDeltaTime);
            float d1 = ResolveFillDelta(ballastFill[TankAft], targetAft, fixedDeltaTime);
            float d2 = ResolveFillDelta(ballastFill[TankPort], targetPort, fixedDeltaTime);
            float d3 = ResolveFillDelta(ballastFill[TankStarboard], targetStarboard, fixedDeltaTime);
            float totalDeltaMagnitude = math.abs(d0) + math.abs(d1) + math.abs(d2) + math.abs(d3);
            if (totalDeltaMagnitude <= 0.000001f)
            {
                _pumpPowered = 1;
                return;
            }

            if (!TrySpendPumpPower(totalDeltaMagnitude))
            {
                _pumpPowered = 0;
                _pendingTelemetryFlags |= PidTelemetryFlagPumpDenied;
                return;
            }

            float beforeFill = SumBallastFill();
            ballastFill[TankFront] = math.saturate(ballastFill[TankFront] + d0);
            ballastFill[TankAft] = math.saturate(ballastFill[TankAft] + d1);
            ballastFill[TankPort] = math.saturate(ballastFill[TankPort] + d2);
            ballastFill[TankStarboard] = math.saturate(ballastFill[TankStarboard] + d3);
            EmitAirReleaseIfNeeded(beforeFill, SumBallastFill());
            _pumpPowered = 1;
        }

        private float ResolveFillDelta(float current, float target, float fixedDeltaTime)
        {
            float requested = target - current;
            float rate = requested < 0f ? ballastBlowRate01PerSecond : pumpFillRate01PerSecond;
            float maxDelta = math.max(0f, rate) * math.max(0f, fixedDeltaTime);
            return math.clamp(requested, -maxDelta, maxDelta);
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
            ProceduralAudioEvents.RaiseAudioPingTriggered(
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

        private void ApplyMassDistribution()
        {
            if (!TryResolveBallastFill(out NativeArray<float> ballastFill) ||
                !TryResolveTankLocalPositions(out NativeArray<float3> tankLocalPositions))
                return;

            float tankMassFull = math.max(0.01f, ballastTankVolumeCubicMeters) * WaterDensityKgPerCubicMeter;
            float baseMass = math.max(MinimumMassForReciprocal, _baseMassKg);
            float totalBallastMass = 0f;
            float3 weightedSum = ToFloat3(baseCenterOfMassLocal) * baseMass;
            for (int i = 0; i < TankCount; i++)
            {
                float mass = math.saturate(ballastFill[i]) * tankMassFull;
                totalBallastMass += mass;
                weightedSum += tankLocalPositions[i] * mass;
            }

            _ballastWaterMassKg = totalBallastMass;
            float totalMass = math.max(MinimumMassForReciprocal, baseMass + totalBallastMass);
            _centerOfMassLocal = weightedSum * math.rcp(math.max(MinimumMassForReciprocal, totalMass));
            totalMass = ApplyDynamicFloodMassToCurrentCenter(totalMass);
            if (!math.all(math.isfinite(_centerOfMassLocal)))
                _centerOfMassLocal = ToFloat3(baseCenterOfMassLocal);

            if (_hull != null)
            {
                _hull.centerOfMass = ToVector3(_centerOfMassLocal);
                ApplyDynamicFloodAngularDrag();
                ApplyDynamicFloodInertiaTensor();
            }

            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics != null)
                fluidDynamics.SetBallastWaterMassKilograms(_ballastWaterMassKg);
            else if (_hull != null)
                _hull.mass = totalMass;
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
            float3 dryCenter = ToFloat3(baseCenterOfMassLocal);
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

            float3 baseTensor = ToFloat3(_baseInertiaTensor);
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
                    SignalBus<AcousticPingSignal>.Push(in stress);
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
                SignalBus<HapticRequest>.Push(in haptic);
            }

            if (_criticalListCooldown > 0f || !IsCriticalFloodPitchExceeded())
                return;

            _criticalListCooldown = 0.5f;
            VehicleCommandSignal criticalList = default;
            criticalList.TargetInstanceId = _targetInstanceId;
            criticalList.Flags = (byte)VehicleCommandSignalFlags.CriticalList;
            if (VehicleCommandSignalBus.Publish(in criticalList))
                _pendingTelemetryFlags |= PidTelemetryFlagCriticalList;
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

            float3 ventDirection = ToFloat3(-_cachedTransform.forward);
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
            SignalBus<BubbleSpawnSignal>.Push(in signal);
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
            SignalBus<FluidImpulseSignal>.Push(in impulse);
            _pendingTelemetryFlags |= PidTelemetryFlagFluidImpulseSignal;
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
            float thresholdSin = math.sin(math.radians(thresholdDegrees));
            return math.abs(math.clamp(forward.y, -1f, 1f)) >= thresholdSin;
        }

        private bool IsTailHeavyPitchExceeded(float thresholdDegrees)
        {
            if (_hull == null)
                return false;

            float threshold = math.clamp(thresholdDegrees, 0f, 89f);
            quaternion rotation = new quaternion(_hull.rotation.x, _hull.rotation.y, _hull.rotation.z, _hull.rotation.w);
            float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
            float thresholdSin = math.sin(math.radians(threshold));
            return math.clamp(forward.y, -1f, 1f) >= thresholdSin;
        }

        private void SchedulePidJob(float fixedDeltaTime)
        {
            if (!autoLevelEnabled ||
                _pidJobPending ||
                !TryResolvePidOutput(out NativeArray<PidJobOutput> pidOutput) ||
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

            Quaternion rotation = _hull.rotation;
            Vector3 angularVelocity = _hull.angularVelocity;
            NativeArray<WhirlpoolFlow> activeMaelstroms = default;
            int activeMaelstromCount = 0;
            if (_fluid != null &&
                _fluid.TryGetActiveWhirlpoolFlows(out NativeArray<WhirlpoolFlow> maelstroms, out int maelstromCount))
            {
                activeMaelstroms = maelstroms;
                activeMaelstromCount = maelstromCount;
            }

            _pidHandle = new SubmarineAutoLevelPidJob
            {
                CurrentRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                AngularVelocityWorld = ToFloat3(angularVelocity),
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
                PositionWS = ToFloat3(_hull.worldCenterOfMass),
                DynamicFloodCenterOfMassOffsetLocal = _dynamicFloodComOffsetLocal,
                FloodPitchBiasPerMeter = floodPidPitchBiasPerMeter,
                ResetIntegral = _resetIntegralPending ? (byte)1 : (byte)0,
                CriticalFloodActive = _criticalFloodActive,
                MaelstromApproximationTier = 0,
                ActiveMaelstromCount = activeMaelstromCount,
                ActiveMaelstroms = activeMaelstroms,
                Output = pidOutput
            }.Schedule();
            _pidJobPending = true;
            _resetIntegralPending = false;
        }

        private bool CompletePidJob(bool forceComplete, bool commitOutput)
        {
            if (!_pidJobPending)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _pidHandle, forceComplete))
                return false;

            _pidJobPending = false;
            if (!commitOutput ||
                !TryResolvePidOutput(out NativeArray<PidJobOutput> pidOutput) ||
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

            if (output.Flags != 0u)
                DumpTelemetryOnce(output.Flags);

            float3 acceptedTorque = output.TorqueWorld;
            if ((output.Flags & PidTelemetryFlagInvalidOutput) != 0u)
                _smoothedPidTorqueWorld = float3.zero;
            else
                acceptedTorque = FastNlerp(_smoothedPidTorqueWorld, acceptedTorque, pidTorqueFastNlerp01);

            if (_hull != null && output.Flags == 0u && math.lengthsq(output.TorqueWorld) > 0.0001f)
            {
                _smoothedPidTorqueWorld = acceptedTorque;
                EmitPidHullStressSignal(output.Error, _hull.worldCenterOfMass);
                PhysicsForceRouter.QueueTorque(_hull, ToVector3(acceptedTorque), ForceMode.Force);
            }

            if (_hull != null && output.Flags == 0u && math.lengthsq(output.MaelstromAcceleration) > 0.0001f)
                PhysicsForceRouter.QueueAmbientForce(_hull, ToVector3(output.MaelstromAcceleration), ForceMode.Acceleration);

            return true;
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

            IAudioService audioService = _audio;
            if (audioService != null && audioService.QueueHullStressSignal(in signal))
                return;

            ProceduralAudioEvents.RaiseHullStressSignal(in signal);
        }

        private void RefreshSnapshot()
        {
            if (_hull == null)
                return;

            Quaternion rotation = _hull.rotation;
            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            _snapshot = new SubmarineStateSnapshot
            {
                RuntimePosition = SnapMillimeter(ToFloat3(_hull.position)),
                RuntimeRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                LinearVelocity = SnapMillimeter(ToFloat3(_hull.linearVelocity)),
                AngularVelocity = ToFloat3(_hull.angularVelocity),
                CenterOfMassLocal = _centerOfMassLocal,
                BaseMassKg = _baseMassKg,
                BallastWaterMassKg = _ballastWaterMassKg,
                TotalCargoMassKg = fluidDynamics != null ? fluidDynamics.TotalCargoMassKg : _ballastWaterMassKg,
                PidIntegralWindup = _lastIntegralWindup,
                MathLod = _authoritativeMathLod,
                PumpPowered = _pumpPowered,
                AutoLevelActive = autoLevelEnabled && _criticalFloodActive == 0 ? (byte)1 : (byte)0,
                Frame = (uint)_tickCount
            };
        }

        private void SeedAuthoritativeMathLod()
        {
            _authoritativeMathLod = 1;
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

            if (!TryResolveRoomBuffers(
                    out NativeArray<float> roomWaterLevels,
                    out NativeArray<float> roomVolumes,
                    out NativeArray<float3> roomLocalAups))
            {
                return;
            }

            int bufferRoomCount = math.min(roomWaterLevels.Length, math.min(roomVolumes.Length, roomLocalAups.Length));
            int roomCount = math.min(_dynamicFloodRoomCount, bufferRoomCount);
            if (roomCount <= 0 || !TryResolveFloodMassOutput(out NativeArray<DynamicFloodMassOutput> floodMassOutput))
            {
                return;
            }

            _floodMassHandle = new SubmarineMassSolverJob
            {
                RoomWaterLevels = roomWaterLevels,
                RoomVolumes = roomVolumes,
                RoomLocalAUPs = roomLocalAups,
                Output = floodMassOutput,
                RoomCount = roomCount,
                BaseMassKg = _baseMassKg,
                BaseCenterOfMassLocal = ToFloat3(baseCenterOfMassLocal),
                GlobalPivotAnchor = ResolveGlobalPivotAnchor()
            }.Schedule();
            _floodMassJobPending = true;
        }

        private bool CompleteFloodMassJob(bool forceComplete, bool commitOutput)
        {
            if (!_floodMassJobPending)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _floodMassHandle, forceComplete))
                return false;

            _floodMassJobPending = false;
            if (!commitOutput ||
                !TryResolveFloodMassOutput(out NativeArray<DynamicFloodMassOutput> floodMassOutput) ||
                floodMassOutput.Length == 0)
            {
                return true;
            }

            CommitDynamicFloodMassOutput(floodMassOutput[0]);
            return true;
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
            if (!TryResolveBallastFill(out NativeArray<float> ballastFill))
                return 0f;

            return ballastFill[TankFront] +
                   ballastFill[TankAft] +
                   ballastFill[TankPort] +
                   ballastFill[TankStarboard];
        }

        private void WriteTelemetry(uint flags)
        {
            if (!TryResolveTelemetry(out NativeArray<SubmarinePidTelemetryEntry> telemetry) || _hull == null)
                return;

            int index = _telemetryCursor;
            if ((uint)index >= (uint)telemetry.Length)
                index = 0;

            Vector3 position = _hull.position;
            Vector3 velocity = _hull.linearVelocity;
            Vector3 angularVelocity = _hull.angularVelocity;
            uint safeFlags = flags;
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
                RuntimePosition = SnapMillimeter(ToFloat3(position)),
                LinearVelocity = SnapMillimeter(ToFloat3(velocity)),
                AngularVelocity = ToFloat3(angularVelocity),
                CenterOfMassLocal = _centerOfMassLocal,
                DynamicFloodComOffsetLocal = _dynamicFloodComOffsetLocal,
                DynamicFloodInertiaTensorMultiplier = _dynamicFloodInertiaTensorMultiplier,
                PidError = _previousPidError,
                BallastWaterMassKg = _ballastWaterMassKg,
                DynamicFloodWaterMassKg = _dynamicFloodWaterMassKg,
                DynamicFloodAngularDragMultiplier = _dynamicFloodAngularDragMultiplier,
                CriticalFloodActive = _criticalFloodActive,
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
            if ((safeFlags & PidTelemetryFlagInvalidOutput) != 0u)
                DumpTelemetryOnce(telemetry, safeFlags);
        }

        private void DumpTelemetryOnce(NativeArray<SubmarinePidTelemetryEntry> telemetry, uint reasonFlags)
        {
            if (_dumpedTelemetry || !telemetry.IsCreated)
                return;

            _dumpedTelemetry = true;
            WriteTelemetryDumpFile(BallastPidDumpRelativePath, telemetry, reasonFlags);
        }

        private void DumpTelemetryOnce(uint reasonFlags)
        {
            if (TryResolveTelemetry(out NativeArray<SubmarinePidTelemetryEntry> telemetry))
                DumpTelemetryOnce(telemetry, reasonFlags);
        }

        private void WriteTelemetryDumpFile(
            string relativePath,
            NativeArray<SubmarinePidTelemetryEntry> telemetry,
            uint reasonFlags)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(0x53504944u);
            writer.Write(reasonFlags);
            writer.Write(telemetry.Length);
            writer.Write(_telemetryCursor);
            for (int i = 0; i < telemetry.Length; i++)
            {
                SubmarinePidTelemetryEntry entry = telemetry[i];
                writer.Write(entry.Frame);
                writer.Write(entry.StateHash);
                writer.Write(entry.Flags);
                writer.Write(entry.IntegralWindup);
                writer.Write(entry.SystemStress01);
                WriteFloat3(writer, entry.RuntimePosition);
                WriteFloat3(writer, entry.LinearVelocity);
                WriteFloat3(writer, entry.AngularVelocity);
                WriteFloat3(writer, entry.CenterOfMassLocal);
                WriteFloat3(writer, entry.DynamicFloodComOffsetLocal);
                WriteFloat3(writer, entry.DynamicFloodInertiaTensorMultiplier);
                WriteFloat3(writer, entry.PidError);
                writer.Write(entry.BallastWaterMassKg);
                writer.Write(entry.DynamicFloodWaterMassKg);
                writer.Write(entry.DynamicFloodAngularDragMultiplier);
                writer.Write(entry.CriticalFloodActive);
            }
        }

        private bool TryResolveBallastFill(out NativeArray<float> buffer)
        {
            return TryResolveVaultBuffer(
                ref _ballastFill01Handle,
                BufferID.SubmarineBallastFill01,
                TankCount,
                VaultBallastFillFlag,
                out buffer);
        }

        private bool TryResolveTankLocalPositions(out NativeArray<float3> buffer)
        {
            return TryResolveVaultBuffer(
                ref _tankLocalPositionsHandle,
                BufferID.SubmarineBallastTankLocalPositions,
                TankCount,
                VaultTankLocalPositionsFlag,
                out buffer);
        }

        private bool TryResolvePidOutput(out NativeArray<PidJobOutput> buffer)
        {
            return TryResolveVaultBuffer(
                ref _pidOutputHandle,
                BufferID.SubmarineBallastPidOutput,
                1,
                VaultPidOutputFlag,
                out buffer);
        }

        private bool TryResolveFloodMassOutput(out NativeArray<DynamicFloodMassOutput> buffer)
        {
            return TryResolveVaultBuffer(
                ref _floodMassOutputHandle,
                BufferID.SubmarineDynamicFloodMassOutput,
                1,
                VaultFloodMassOutputFlag,
                out buffer);
        }

        private bool TryResolveTelemetry(out NativeArray<SubmarinePidTelemetryEntry> buffer)
        {
            return TryResolveVaultBuffer(
                ref _telemetryHandle,
                BufferID.SubmarinePidTelemetry,
                TelemetryCapacity,
                VaultTelemetryFlag,
                out buffer);
        }

        private bool TryResolveRoomBuffers(
            out NativeArray<float> roomWaterLevels,
            out NativeArray<float> roomVolumes,
            out NativeArray<float3> roomLocalAups)
        {
            bool hasWater = TryResolveExistingVaultBuffer(ref _roomWaterLevelsHandle, BufferID.RoomWaterLevels, out roomWaterLevels);
            bool hasVolumes = TryResolveExistingVaultBuffer(ref _roomVolumesHandle, BufferID.RoomVolumes, out roomVolumes);
            bool hasAups = TryResolveExistingVaultBuffer(ref _roomLocalAUPsHandle, BufferID.RoomLocalAUPs, out roomLocalAups);
            return hasWater && hasVolumes && hasAups;
        }

        private bool TryResolveVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            int vaultFlag,
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
                    handle = vault.GetGenerationHandle<T>(
                        bufferId,
                        requiredLength,
                        OwnerSystem,
                        NativeArrayOptions.ClearMemory);
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

        private bool TryResolveExistingVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                handle = default;
                _pendingTelemetryFlags |= PidTelemetryFlagDataVaultMissing;
                return false;
            }

            if (TryResolveVehiclesPhysicsVaultBuffer(vault, ref handle, bufferId, 1, out buffer))
                return true;

            if (vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
                TryResolveVehiclesPhysicsVaultBuffer(vault, ref handle, bufferId, 1, out buffer))
            {
                return true;
            }

            handle = default;
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

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _ballastFill01Handle, BufferID.SubmarineBallastFill01);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _tankLocalPositionsHandle, BufferID.SubmarineBallastTankLocalPositions);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _pidOutputHandle, BufferID.SubmarineBallastPidOutput);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _floodMassOutputHandle, BufferID.SubmarineDynamicFloodMassOutput);
            ReleaseVehiclesPhysicsVaultHandle(vault, ref _telemetryHandle, BufferID.SubmarinePidTelemetry);
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
                DeterministicPhysicsMath.SnapMillimeter(value.x),
                DeterministicPhysicsMath.SnapMillimeter(value.y),
                DeterministicPhysicsMath.SnapMillimeter(value.z));
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

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
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
