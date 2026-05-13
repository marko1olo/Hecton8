using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Audio;
using Hecton8.Core;
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
    [RequireComponent(typeof(Rigidbody))]
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
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PidJobOutput
        {
            public float3 TorqueWorld;
            public float3 MaelstromAcceleration;
            public float3 Integral;
            public float3 Error;
            public float3 Derivative;
            public float IntegralWindup;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct SubmarinePidTelemetryEntry
        {
            public int Frame;
            public uint StateHash;
            public uint Flags;
            public float IntegralWindup;
            public float3 RuntimePosition;
            public float3 LinearVelocity;
            public float3 AngularVelocity;
            public float3 CenterOfMassLocal;
            public float BallastWaterMassKg;
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
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
            public float3 PositionWS;
            public byte ResetIntegral;
            public byte LowMaelstromTier;
            public int ActiveMaelstromCount;
            [ReadOnly] public NativeArray<WhirlpoolFlow> ActiveMaelstroms;
            public NativeArray<PidJobOutput> Output;

            public void Execute()
            {
                const float Epsilon = 0.000001f;
                float safeDeltaTime = math.max(DeltaTime, 0.0001f);
                float3 targetUp = new float3(0f, 1f, 0f);
                float3 currentUp = math.mul(CurrentRotation, targetUp);
                float currentUpLengthSq = math.lengthsq(currentUp);
                currentUp = currentUpLengthSq > Epsilon
                    ? currentUp * math.rsqrt(currentUpLengthSq)
                    : targetUp;
                float dot = math.clamp(math.dot(currentUp, targetUp), -1f, 1f);
                float3 errorAxis = math.cross(currentUp, targetUp);
                if (math.lengthsq(errorAxis) <= Epsilon && dot < 0f)
                {
                    float3 fallbackAxis = math.mul(CurrentRotation, new float3(1f, 0f, 0f));
                    float fallbackAxisLengthSq = math.lengthsq(fallbackAxis);
                    errorAxis = fallbackAxisLengthSq > Epsilon
                        ? fallbackAxis * math.rsqrt(fallbackAxisLengthSq)
                        : new float3(1f, 0f, 0f);
                }

                float3 error = errorAxis * (1f + math.saturate(1f - dot));
                float3 integral = ResetIntegral != 0
                    ? float3.zero
                    : PreviousIntegral + (error * safeDeltaTime);
                float clamp = math.max(0f, IntegralClamp);
                integral = math.clamp(integral, new float3(-clamp), new float3(clamp));

                float3 derivative = ResetIntegral != 0
                    ? float3.zero
                    : (error - PreviousError) * math.rcp(safeDeltaTime);
                float3 dampedDerivative = derivative - AngularVelocityWorld;
                float3 torque = (error * math.max(0f, Kp)) +
                                (integral * math.max(0f, Ki)) +
                                (dampedDerivative * math.max(0f, Kd));

                float maxTorque = math.max(0f, MaxTorque);
                float torqueLengthSq = math.lengthsq(torque);
                if (torqueLengthSq > maxTorque * maxTorque && torqueLengthSq > Epsilon)
                    torque *= maxTorque * math.rsqrt(torqueLengthSq);

                float3 maelstromAcceleration = HectonAnalyticalFlowField.SampleWhirlpoolVelocity(
                    PositionWS,
                    ActiveMaelstroms,
                    ActiveMaelstromCount,
                    LowMaelstromTier,
                    MaelstromAccelerationClamp);

                uint flags = 0u;
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
                    ? integralLengthSq * math.rsqrt(integralLengthSq)
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
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SUBMARINE_AUTOPILOT.bin";
        private const float WaterDensityKgPerCubicMeter = 1025f;
        private const float MaelstromAccelerationClamp = 12f;

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

        [Header("Scalability")]
        [SerializeField, Min(0.1f)] private float mathLodSwitchHoldSeconds = 2.5f;

        [Header("Mass Layout")]
        [SerializeField] private Vector3 baseCenterOfMassLocal = Vector3.zero;
        [SerializeField] private Vector3 frontTankLocalPosition = new Vector3(0f, -0.35f, 2.4f);
        [SerializeField] private Vector3 aftTankLocalPosition = new Vector3(0f, -0.35f, -2.4f);
        [SerializeField] private Vector3 portTankLocalPosition = new Vector3(-1.1f, -0.35f, 0f);
        [SerializeField] private Vector3 starboardTankLocalPosition = new Vector3(1.1f, -0.35f, 0f);

        [Header("Combat Recovery")]
        [SerializeField, Min(0f)] private float combatTargetHealth = 250f;
        [SerializeField, Min(0f)] private float massiveImpactDamageThreshold = 35f;
        [SerializeField, Min(0f)] private float combatArmorValue = 8f;

        private SubmarineCoreDirector _core;
        private IPowerGridService _powerGrid;
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
        private bool _resetIntegralPending;
        private bool _dumpedTelemetry;
        private byte _pumpPowered = 1;
        private byte _mathLod;
        private int _targetInstanceId;
        private int _fallbackInstanceId;
        private int _tickCount;
        private int _telemetryCursor;
        private float _baseMassKg = 1200f;
        private float _ballastWaterMassKg;
        private float _mathLodSwitchTimer;
        private float _lastIntegralWindup;
        private float _airReleaseCooldownSeconds;
        private bool _lowMathLodActive;
        private float3 _pidIntegral;
        private float3 _previousPidError;
        private float3 _lastPidDerivative;
        private float3 _centerOfMassLocal;
        private uint _pendingTelemetryFlags;
        private HectonQualityTier _cachedScalabilityTier = HectonQualityTier.Unknown;
        private MathPrecisionLevel _cachedMathPrecision = MathPrecisionLevel.Low;
        private bool _desiredLowMathLod = true;
        private JobHandle _pidHandle;

        private NativeArray<float> _ballastFill01;
        private NativeArray<float3> _tankLocalPositions;
        private NativeArray<PidJobOutput> _pidOutput;
        private NativeArray<SubmarinePidTelemetryEntry> _telemetry;

        public bool SuppressesKinematicPitch => isActiveAndEnabled && autoLevelEnabled;

        public int TickCount => _tickCount;

        public Rigidbody CombatPushbackBody => _hull;

        public Vector3 CombatForward => _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;

        public float CombatHeight => 2.8f;

        public NativeArray<float>.ReadOnly BallastFill01 => _ballastFill01.IsCreated ? _ballastFill01.AsReadOnly() : default;

        public SubmarineStateSnapshot StateSnapshot => _snapshot;

        private void Awake()
        {
            CacheReferences();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshTargetInstanceId();
            SeedMathLod();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureNativeState();
            RefreshTankPositions();
            RefreshTargetInstanceId();
            SeedMathLod();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            CompletePidJob(forceComplete: true, commitOutput: false);
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            CompletePidJob(forceComplete: true, commitOutput: false);
            DisposeNativeState();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            _tickCount++;
            if (_hull == null || fixedDeltaTime <= 0f)
                return;

            VehicleCommandSignalBus.FlushPending();
            VehicleCommandSignal command = ConsumeCommand();
            AdvanceAirReleaseCooldown(fixedDeltaTime);
            AdvanceMathLod(fixedDeltaTime);
            bool lowMathLod = _lowMathLodActive;
            _mathLod = lowMathLod ? (byte)0 : (byte)1;
            AdvanceBallast(in command, fixedDeltaTime, lowMathLod);
            ApplyMassDistribution();
            RefreshSnapshot();
            WriteTelemetry(_pendingTelemetryFlags);
            _pendingTelemetryFlags = 0u;
            SchedulePidJob(fixedDeltaTime, lowMathLod);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            CompletePidJob(forceComplete: false, commitOutput: true);
        }

        public void SlowTick()
        {
            if (_fluid == null)
                _fluid = GlobalRegistry.Fluid;

            RefreshMathLodPolicyFromRegistrySlow();
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

            if (serviceSlot == GlobalRegistryServiceSlot.SubmarineState && currentService == null)
                TryRegisterStateReadModel();
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            MathPrecisionLevel precision = payload.CurrentTier == ScalabilityTierProfiles.LowMx350
                ? MathPrecisionLevel.Low
                : MathPrecisionLevel.High;
            _cachedScalabilityTier = payload.CurrentQualityTier;
            _cachedMathPrecision = precision;
            _desiredLowMathLod = ResolveLowMathLod(payload.CurrentQualityTier, precision);
        }

        private void RegisterRuntime()
        {
            _powerGrid = GlobalRegistry.PowerGrid;
            _fluid = GlobalRegistry.Fluid;

            TryRegisterStateReadModel();

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
            _powerGrid = null;
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
        }

        private void EnsureNativeState()
        {
            if (!_ballastFill01.IsCreated)
            {
                _ballastFill01 = AllocateArray<float>(TankCount, nameof(_ballastFill01));
                for (int i = 0; i < TankCount; i++)
                    _ballastFill01[i] = math.saturate(neutralBallastFill01);
            }

            if (!_tankLocalPositions.IsCreated)
                _tankLocalPositions = AllocateArray<float3>(TankCount, nameof(_tankLocalPositions));

            if (!_pidOutput.IsCreated)
                _pidOutput = AllocateArray<PidJobOutput>(1, nameof(_pidOutput));

            if (!_telemetry.IsCreated)
                _telemetry = AllocateArray<SubmarinePidTelemetryEntry>(TelemetryCapacity, nameof(_telemetry));
        }

        private void DisposeNativeState()
        {
            DisposeArray(ref _ballastFill01);
            DisposeArray(ref _tankLocalPositions);
            DisposeArray(ref _pidOutput);
            DisposeArray(ref _telemetry);
        }

        private void ClearBallastMassCoupling()
        {
            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics != null)
                fluidDynamics.SetBallastWaterMassKilograms(0f);

            _ballastWaterMassKg = 0f;
        }

        private void RefreshTankPositions()
        {
            if (!_tankLocalPositions.IsCreated || _tankLocalPositions.Length < TankCount)
                return;

            _tankLocalPositions[TankFront] = ToFloat3(frontTankLocalPosition);
            _tankLocalPositions[TankAft] = ToFloat3(aftTankLocalPosition);
            _tankLocalPositions[TankPort] = ToFloat3(portTankLocalPosition);
            _tankLocalPositions[TankStarboard] = ToFloat3(starboardTankLocalPosition);
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

        private void AdvanceBallast(in VehicleCommandSignal command, float fixedDeltaTime, bool lowMathLod)
        {
            if (!_ballastFill01.IsCreated)
                return;

            float neutral = math.saturate(neutralBallastFill01);
            float pitch = math.clamp(command.Pitch, -1f, 1f);
            float totalBias = math.clamp(command.BallastDelta, -maxCommandBallastBias01, maxCommandBallastBias01);
            float pitchBias = pitch * math.max(0f, maxCommandBallastBias01);

            if (lowMathLod)
            {
                float target = math.saturate(neutral + totalBias);
                float current = AverageBallastFill();
                float delta = ResolveFillDelta(current, target, fixedDeltaTime);
                if (math.abs(delta) <= 0.000001f)
                {
                    _pumpPowered = 1;
                    return;
                }

                if (!TrySpendPumpPower(math.abs(delta)))
                {
                    _pumpPowered = 0;
                    _pendingTelemetryFlags |= PidTelemetryFlagPumpDenied;
                    return;
                }

                float before = current * TankCount;
                float next = math.saturate(current + delta);
                for (int i = 0; i < TankCount; i++)
                    _ballastFill01[i] = next;
                EmitAirReleaseIfNeeded(before, next * TankCount);
                _pumpPowered = 1;
                return;
            }

            float targetFront = math.saturate(neutral + totalBias + pitchBias);
            float targetAft = math.saturate(neutral + totalBias - pitchBias);
            float targetPort = math.saturate(neutral + totalBias);
            float targetStarboard = math.saturate(neutral + totalBias);

            float d0 = ResolveFillDelta(_ballastFill01[TankFront], targetFront, fixedDeltaTime);
            float d1 = ResolveFillDelta(_ballastFill01[TankAft], targetAft, fixedDeltaTime);
            float d2 = ResolveFillDelta(_ballastFill01[TankPort], targetPort, fixedDeltaTime);
            float d3 = ResolveFillDelta(_ballastFill01[TankStarboard], targetStarboard, fixedDeltaTime);
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
            _ballastFill01[TankFront] = math.saturate(_ballastFill01[TankFront] + d0);
            _ballastFill01[TankAft] = math.saturate(_ballastFill01[TankAft] + d1);
            _ballastFill01[TankPort] = math.saturate(_ballastFill01[TankPort] + d2);
            _ballastFill01[TankStarboard] = math.saturate(_ballastFill01[TankStarboard] + d3);
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
            if (!_ballastFill01.IsCreated || !_tankLocalPositions.IsCreated)
                return;

            float tankMassFull = math.max(0.01f, ballastTankVolumeCubicMeters) * WaterDensityKgPerCubicMeter;
            float totalBallastMass = 0f;
            float3 weightedSum = ToFloat3(baseCenterOfMassLocal) * math.max(1f, _baseMassKg);
            for (int i = 0; i < TankCount; i++)
            {
                float mass = math.saturate(_ballastFill01[i]) * tankMassFull;
                totalBallastMass += mass;
                weightedSum += _tankLocalPositions[i] * mass;
            }

            _ballastWaterMassKg = totalBallastMass;
            float totalMass = math.max(1f, _baseMassKg + totalBallastMass);
            _centerOfMassLocal = weightedSum * math.rcp(totalMass);
            if (!math.all(math.isfinite(_centerOfMassLocal)))
                _centerOfMassLocal = ToFloat3(baseCenterOfMassLocal);

            if (_hull != null)
                _hull.centerOfMass = ToVector3(_centerOfMassLocal);

            SubmarineFluidDynamics fluidDynamics = _core != null ? _core.FluidDynamics : null;
            if (fluidDynamics != null)
                fluidDynamics.SetBallastWaterMassKilograms(_ballastWaterMassKg);
            else if (_hull != null)
                _hull.mass = totalMass;
        }

        private void SchedulePidJob(float fixedDeltaTime, bool lowMathLod)
        {
            if (!autoLevelEnabled || _pidJobPending || !_pidOutput.IsCreated || _hull == null)
                return;

            Quaternion rotation = _hull.rotation;
            Vector3 angularVelocity = _hull.angularVelocity;
            float torqueScale = lowMathLod ? 0.65f : 1f;
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
                Kp = proportionalGain * torqueScale,
                Ki = integralGain * torqueScale,
                Kd = derivativeGain * torqueScale,
                IntegralClamp = integralClamp,
                MaxTorque = maxTorqueNewtons * torqueScale,
                MaelstromAccelerationClamp = MaelstromAccelerationClamp,
                PositionWS = ToFloat3(_hull.worldCenterOfMass),
                ResetIntegral = _resetIntegralPending ? (byte)1 : (byte)0,
                LowMaelstromTier = lowMathLod ? (byte)1 : (byte)0,
                ActiveMaelstromCount = activeMaelstromCount,
                ActiveMaelstroms = activeMaelstroms,
                Output = _pidOutput
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
            if (!commitOutput || !_pidOutput.IsCreated || _pidOutput.Length == 0)
                return true;

            PidJobOutput output = _pidOutput[0];
            _pidIntegral = output.Integral;
            _previousPidError = output.Error;
            _lastPidDerivative = output.Derivative;
            _lastIntegralWindup = output.IntegralWindup;
            _pendingTelemetryFlags |= output.Flags;

            if (output.Flags != 0u)
                DumpTelemetryOnce(output.Flags);

            if (_hull != null && output.Flags == 0u && math.lengthsq(output.TorqueWorld) > 0.0001f)
                PhysicsForceRouter.QueueTorque(_hull, ToVector3(output.TorqueWorld), ForceMode.Force);

            if (_hull != null && output.Flags == 0u && math.lengthsq(output.MaelstromAcceleration) > 0.0001f)
                PhysicsForceRouter.QueueAmbientForce(_hull, ToVector3(output.MaelstromAcceleration), ForceMode.Acceleration);

            return true;
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
                MathLod = _mathLod,
                PumpPowered = _pumpPowered,
                AutoLevelActive = autoLevelEnabled ? (byte)1 : (byte)0,
                Frame = (uint)_tickCount
            };
        }

        private bool ResolveLowMathLodFromRegistryCold()
        {
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
            _cachedMathPrecision = GlobalRegistry.MathPrecision;
            return ResolveLowMathLod(_cachedScalabilityTier, _cachedMathPrecision);
        }

        private static bool ResolveLowMathLod(HectonQualityTier tier, MathPrecisionLevel precision)
        {
            return precision == MathPrecisionLevel.Low ||
                   tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        private void SeedMathLod()
        {
            _desiredLowMathLod = ResolveLowMathLodFromRegistryCold();
            _lowMathLodActive = _desiredLowMathLod;
            _mathLodSwitchTimer = 0f;
            _mathLod = _lowMathLodActive ? (byte)0 : (byte)1;
        }

        private void AdvanceMathLod(float fixedDeltaTime)
        {
            bool desiredLow = _desiredLowMathLod;
            if (desiredLow == _lowMathLodActive)
            {
                _mathLodSwitchTimer = 0f;
                return;
            }

            _mathLodSwitchTimer += math.max(0f, fixedDeltaTime);
            if (_mathLodSwitchTimer < math.max(0.1f, mathLodSwitchHoldSeconds))
                return;

            _lowMathLodActive = desiredLow;
            _mathLodSwitchTimer = 0f;
        }

        private void RefreshMathLodPolicyFromRegistrySlow()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            MathPrecisionLevel precision = GlobalRegistry.MathPrecision;
            if (tier == _cachedScalabilityTier && precision == _cachedMathPrecision)
                return;

            _cachedScalabilityTier = tier;
            _cachedMathPrecision = precision;
            _desiredLowMathLod = ResolveLowMathLod(tier, precision);
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
            if (!_ballastFill01.IsCreated)
                return 0f;

            return _ballastFill01[TankFront] +
                   _ballastFill01[TankAft] +
                   _ballastFill01[TankPort] +
                   _ballastFill01[TankStarboard];
        }

        private void WriteTelemetry(uint flags)
        {
            if (!_telemetry.IsCreated || _hull == null)
                return;

            int index = _telemetryCursor;
            if ((uint)index >= (uint)_telemetry.Length)
                index = 0;

            Vector3 position = _hull.position;
            Vector3 velocity = _hull.linearVelocity;
            Vector3 angularVelocity = _hull.angularVelocity;
            uint safeFlags = flags;
            if (!IsFinite(position) || !IsFinite(velocity) || !IsFinite(angularVelocity) || !math.isfinite(_lastIntegralWindup))
                safeFlags |= PidTelemetryFlagInvalidOutput;

            _telemetry[index] = new SubmarinePidTelemetryEntry
            {
                Frame = _tickCount,
                RuntimePosition = SnapMillimeter(ToFloat3(position)),
                LinearVelocity = SnapMillimeter(ToFloat3(velocity)),
                AngularVelocity = ToFloat3(angularVelocity),
                CenterOfMassLocal = _centerOfMassLocal,
                BallastWaterMassKg = _ballastWaterMassKg,
                IntegralWindup = _lastIntegralWindup,
                Flags = safeFlags,
                StateHash = BuildTelemetryHash(position, velocity, angularVelocity, _lastIntegralWindup, safeFlags)
            };

            _telemetryCursor = (index + 1) % TelemetryCapacity;
            if ((safeFlags & PidTelemetryFlagInvalidOutput) != 0u)
                DumpTelemetryOnce(safeFlags);
        }

        private void DumpTelemetryOnce(uint reasonFlags)
        {
            if (_dumpedTelemetry || !_telemetry.IsCreated)
                return;

            _dumpedTelemetry = true;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(0x53504944u);
            writer.Write(reasonFlags);
            writer.Write(_telemetry.Length);
            writer.Write(_telemetryCursor);
            for (int i = 0; i < _telemetry.Length; i++)
            {
                SubmarinePidTelemetryEntry entry = _telemetry[i];
                writer.Write(entry.Frame);
                writer.Write(entry.StateHash);
                writer.Write(entry.Flags);
                writer.Write(entry.IntegralWindup);
                WriteFloat3(writer, entry.RuntimePosition);
                WriteFloat3(writer, entry.LinearVelocity);
                WriteFloat3(writer, entry.AngularVelocity);
                WriteFloat3(writer, entry.CenterOfMassLocal);
                writer.Write(entry.BallastWaterMassKg);
            }
        }

        private static NativeArray<T> AllocateArray<T>(int length, string label)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                array,
                nameof(SubmarineAutoLevelBallastController),
                label,
                NativeAllocationLifetime.Scene);
            return array;
        }

        private static void DisposeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static uint BuildTelemetryHash(Vector3 position, Vector3 velocity, Vector3 angularVelocity, float integralWindup, uint flags)
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

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
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
            mathLodSwitchHoldSeconds = Mathf.Max(0.1f, mathLodSwitchHoldSeconds);
            combatTargetHealth = Mathf.Max(0f, combatTargetHealth);
            massiveImpactDamageThreshold = Mathf.Max(0f, massiveImpactDamageThreshold);
            combatArmorValue = Mathf.Max(0f, combatArmorValue);
            integralClamp = Mathf.Max(0f, integralClamp);
            maxTorqueNewtons = Mathf.Max(0f, maxTorqueNewtons);
        }
    }
}
