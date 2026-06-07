namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Tools;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Identifies the physical LifePod strap side used by latch and IK consumers.
    /// </summary>
    public enum LifePodSeatStrapSide : byte
    {
        /// <summary>Left seat strap.</summary>
        Left = 0,

        /// <summary>Right seat strap.</summary>
        Right = 1
    }

    /// <summary>
    /// Owns the two-strap panic latch state and pins the player motor to the LifePod seat anchor.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/LifePod Seat Strap Coordinator")]
    public sealed class LifePodSeatStrapCoordinator : MonoBehaviour, IFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;
        private const byte LeftMotorMask = 0x01;
        private const byte RightMotorMask = 0x02;
        private const float MinimumFixedDeltaTime = 0.0001f;
        private const float MaximumSeatLockFixedDeltaTime = 0.05f;
        private const float MaximumCorrectionMetersPerSecond = 20f;
        private const float MaximumHardSnapDistanceMeters = 0.5f;
        private const float MaximumHapticDurationSeconds = 0.2f;
        private const float MaximumHapticFrequencyHz = 60f;

        [Header("Seat Lock")]
        [SerializeField, Tooltip("Runtime seat anchor. Both straps must latch before the player motor is pinned to this transform.")]
        private Transform seatAnchor;

        [SerializeField, Min(0.01f), Tooltip("Maximum seat correction speed in meters per second. Keeps panic lock bounded during violent crash motion.")]
        private float maximumCorrectionMetersPerSecond = 6.0f;

        [SerializeField, Min(0.0001f), Tooltip("When player body is this close to the seat anchor, the motor snaps to the exact anchor position.")]
        private float hardSnapDistanceMeters = 0.025f;

        [SerializeField, Tooltip("Zeroes player linear velocity while the panic latch is active.")]
        private bool zeroLinearVelocityWhileLocked = true;

        [SerializeField, Tooltip("If true, both latched straps immediately engage the seat lock.")]
        private bool lockWhenBothStrapsLatched = true;

        [Header("Haptics")]
        [SerializeField, Tooltip("Emit bounded haptic pulses when straps latch and when the seat lock engages.")]
        private bool hapticsEnabled = true;

        [SerializeField, Range(0f, 1f), Tooltip("Low-frequency latch confirmation pulse.")]
        private float latchLowFrequency = 0.18f;

        [SerializeField, Range(0f, 1f), Tooltip("High-frequency latch confirmation pulse.")]
        private float latchHighFrequency = 0.35f;

        [SerializeField, Min(0.01f), Tooltip("Latch confirmation haptic duration.")]
        private float latchHapticDurationSeconds = 0.055f;

        [SerializeField, Min(1f), Tooltip("Latch confirmation haptic frequency.")]
        private float latchHapticFrequencyHz = 96f;

        [SerializeField, Range(0f, 1f), Tooltip("Low-frequency full-lock pulse.")]
        private float lockLowFrequency = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip("High-frequency full-lock pulse.")]
        private float lockHighFrequency = 0.55f;

        [SerializeField, Min(0.01f), Tooltip("Full-lock haptic duration.")]
        private float lockHapticDurationSeconds = 0.09f;

        [SerializeField, Min(1f), Tooltip("Full-lock haptic frequency.")]
        private float lockHapticFrequencyHz = 118f;

        private bool _leftLatched;
        private bool _rightLatched;
        private bool _seatLockActive;
        private bool _registeredFixedTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _fixedTickDormant;
        private Transform _leftIkAnchor;
        private Transform _rightIkAnchor;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerSeatLockMotorSink _playerMotor;
        private AbsoluteUniversePosition _seatLockAup;
        private float _resolvedMaximumCorrectionMetersPerSecond;
        private float _resolvedHardSnapDistanceSq;
        private float _resolvedLatchLowFrequency;
        private float _resolvedLatchHighFrequency;
        private float _resolvedLatchHapticDurationSeconds;
        private float _resolvedLatchHapticFrequencyHz;
        private float _resolvedLockLowFrequency;
        private float _resolvedLockHighFrequency;
        private float _resolvedLockHapticDurationSeconds;
        private float _resolvedLockHapticFrequencyHz;
        private bool _seatLockPoseCached;
        private bool _pendingLatchHaptic;
        private bool _pendingLockHaptic;
        private byte _pendingLatchMotorMask;
        private float _pendingLatchLowFrequency;
        private float _pendingLatchHighFrequency;
        private float _pendingLatchDurationSeconds;
        private float _pendingLatchFrequencyHz;
        private float _pendingLockLowFrequency;
        private float _pendingLockHighFrequency;
        private float _pendingLockDurationSeconds;
        private float _pendingLockFrequencyHz;

        /// <summary>
        /// True after the left strap has completed its latch hold.
        /// </summary>
        public bool LeftLatched => _leftLatched;

        /// <summary>
        /// True after the right strap has completed its latch hold.
        /// </summary>
        public bool RightLatched => _rightLatched;

        /// <summary>
        /// True when both physical straps are latched.
        /// </summary>
        public bool IsFullyLatched => _leftLatched && _rightLatched;

        /// <summary>
        /// True while the player motor is being pinned to the seat anchor.
        /// </summary>
        public bool IsSeatLockActive => _seatLockActive;

        /// <summary>
        /// Latest AUP seat anchor used by the panic latch.
        /// </summary>
        public AbsoluteUniversePosition SeatLockAup => _seatLockAup;

        private void Awake()
        {
            if (seatAnchor == null)
                seatAnchor = transform;

            CacheScalarConfig();
            RefreshColdRegistryReferences();
            TryCacheSeatLockPose();
        }

        private void OnEnable()
        {
            CacheScalarConfig();
            RefreshColdRegistryReferences();
            bool hotSwapReady = TryRegisterHotSwapListener();
            if (!_seatLockActive)
                return;

            if (!hotSwapReady ||
                !TryCacheSeatLockPose() ||
                !TryEnsurePlayerMotor() ||
                !TryRegisterFixedTick())
            {
                ReleaseSeatLockForLostRuntimeRoute();
            }
        }

        private void OnDisable()
        {
            ReleaseSeatLock();
            InvalidatePlayerCache();
            TryUnregisterFixedTick();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            ReleaseSeatLock();
            InvalidatePlayerCache();
            TryUnregisterFixedTick();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
        }

        /// <summary>
        /// Records a completed strap latch and engages the seat lock once both sides are secured.
        /// </summary>
        public bool TryLatch(
            LifePodSeatStrapSide side,
            Transform handIkAnchor,
            Vector3 handPosition,
            PhysicalHandSide handSide)
        {
            if (!IsFinite(handPosition))
                return false;

            CacheScalarConfig();
            bool stateChanged = false;
            if (side == LifePodSeatStrapSide.Left)
            {
                if (!_leftLatched)
                {
                    _leftLatched = true;
                    _leftIkAnchor = handIkAnchor;
                    stateChanged = true;
                }
            }
            else if (!_rightLatched)
            {
                _rightLatched = true;
                _rightIkAnchor = handIkAnchor;
                stateChanged = true;
            }

            if (!stateChanged)
                return true;

            QueueLatchHaptic(handSide, side);

            if (lockWhenBothStrapsLatched && IsFullyLatched)
                EngageSeatLock();

            return true;
        }

        /// <summary>
        /// Releases the player motor from the LifePod seat anchor without resetting strap state.
        /// </summary>
        public void ReleaseSeatLock()
        {
            _seatLockActive = false;
            TryUnregisterFixedTick();
            ClearPendingHaptics();
            TryUnregisterLateFrame();
        }

        /// <summary>
        /// Clears both latch bits and releases the seat lock.
        /// </summary>
        public void ResetLatchState()
        {
            ReleaseSeatLock();
            _leftLatched = false;
            _rightLatched = false;
            _leftIkAnchor = null;
            _rightIkAnchor = null;
            InvalidatePlayerCache();
        }

        /// <summary>
        /// Returns the hand IK anchor for a latched strap side.
        /// </summary>
        public bool TryGetLatchedHandAnchor(LifePodSeatStrapSide side, out Transform anchor)
        {
            if (side == LifePodSeatStrapSide.Left)
            {
                anchor = _leftLatched ? _leftIkAnchor : null;
                return anchor != null;
            }

            anchor = _rightLatched ? _rightIkAnchor : null;
            return anchor != null;
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (_fixedTickDormant)
            {
                TryUnregisterFixedTick();
                return;
            }

            if (!_seatLockActive)
            {
                TryUnregisterFixedTick();
                return;
            }

            if (!TryEnsurePlayerMotor())
                return;

            if (!TryResolveSeatLockRuntimePosition(out Vector3 targetPosition))
                return;

            float safeFixedDeltaTime = SanitizeFixedDeltaSeconds(fixedDeltaTime);
            if (!TryResolveCurrentPlayerAup(out AbsoluteUniversePosition currentAup, out Vector3 currentPosition))
                return;

            float3 deltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in _seatLockAup, in currentAup);
            Vector3 delta = new Vector3(deltaAup.x, deltaAup.y, deltaAup.z);
            if (!IsFinite(delta))
                return;

            float distanceSq = delta.sqrMagnitude;
            if (distanceSq <= _resolvedHardSnapDistanceSq)
            {
                _playerMotor.MoveSeatLockPosition(targetPosition);
                if (zeroLinearVelocityWhileLocked)
                    _playerMotor.SetSeatLockLinearVelocity(Vector3.zero);
                return;
            }

            float maxStep = _resolvedMaximumCorrectionMetersPerSecond * safeFixedDeltaTime;
            float maxStepSq = maxStep * maxStep;
            if (distanceSq <= maxStepSq)
            {
                _playerMotor.MoveSeatLockPosition(targetPosition);
                if (zeroLinearVelocityWhileLocked)
                    _playerMotor.SetSeatLockLinearVelocity(Vector3.zero);
                return;
            }

            float invDistance = math.rcp(math.max(ApproximateMagnitudeNoSqrt(delta), 0.000001f));
            Vector3 nextPosition = currentPosition + delta * (maxStep * invDistance);

            if (!IsFinite(nextPosition))
                return;

            _playerMotor.MoveSeatLockPosition(nextPosition);
            if (zeroLinearVelocityWhileLocked)
                _playerMotor.SetSeatLockLinearVelocity(Vector3.zero);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_pendingLatchHaptic && !_pendingLockHaptic)
            {
                TryUnregisterLateFrame();
                return;
            }

            DispatchPendingHaptics();

            if (!_pendingLatchHaptic && !_pendingLockHaptic)
                TryUnregisterLateFrame();
        }

        private void EngageSeatLock()
        {
            if (!TryCacheSeatLockPose())
                return;

            if (!TryEnsurePlayerMotor())
                return;

            if (!TryRegisterHotSwapListener())
                return;

            _seatLockActive = true;
            if (!TryRegisterFixedTick())
            {
                ReleaseSeatLockForLostRuntimeRoute();
                return;
            }

            QueueLockHaptic();
        }

        private bool TryEnsurePlayerMotor()
        {
            return _playerMotor != null &&
                   _playerMotor.HasControllableBody &&
                   _playerRuntimeContext != null &&
                   _playerRuntimeContext.IsInitialized;
        }

        private void InvalidatePlayerCache()
        {
            _playerMotor = null;
        }

        private bool TryResolveCurrentPlayerAup(out AbsoluteUniversePosition currentAup, out Vector3 runtimePosition)
        {
            currentAup = default;
            runtimePosition = Vector3.zero;
            if (_playerRuntimeContext == null ||
                !_playerRuntimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                return false;

            currentAup = snapshot.Aup;
            float3 runtime = snapshot.RuntimePosition;
            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return IsFinite(runtimePosition);
        }

        private bool TryResolveSeatLockRuntimePosition(out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            if (!_seatLockPoseCached)
                return false;

            float3 runtime = _seatLockAup.ToRuntimeFloat3();
            targetPosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return IsFinite(targetPosition);
        }

        private bool TryCacheSeatLockPose()
        {
            Transform anchor = seatAnchor != null ? seatAnchor : transform;
            Vector3 targetPosition = anchor.position;
            if (!IsFinite(targetPosition))
                return false;

            if (!TryResolveRuntimeAup(targetPosition, out _seatLockAup))
                return false;

            _seatLockPoseCached = true;
            return true;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private void QueueLatchHaptic(PhysicalHandSide handSide, LifePodSeatStrapSide strapSide)
        {
            if (!hapticsEnabled)
                return;

            _pendingLatchLowFrequency = _resolvedLatchLowFrequency;
            _pendingLatchHighFrequency = _resolvedLatchHighFrequency;
            _pendingLatchDurationSeconds = _resolvedLatchHapticDurationSeconds;
            _pendingLatchFrequencyHz = _resolvedLatchHapticFrequencyHz;
            _pendingLatchMotorMask = ResolveMotorMask(handSide, strapSide);
            _pendingLatchHaptic = true;

            if (!TryRegisterLateFrame())
                ClearPendingHaptics();
        }

        private void QueueLockHaptic()
        {
            if (!hapticsEnabled)
                return;

            _pendingLockLowFrequency = _resolvedLockLowFrequency;
            _pendingLockHighFrequency = _resolvedLockHighFrequency;
            _pendingLockDurationSeconds = _resolvedLockHapticDurationSeconds;
            _pendingLockFrequencyHz = _resolvedLockHapticFrequencyHz;
            _pendingLockHaptic = true;

            if (!TryRegisterLateFrame())
                ClearPendingHaptics();
        }

        private void DispatchPendingHaptics()
        {
            if (_pendingLatchHaptic)
            {
                float lowFrequency = _pendingLatchLowFrequency;
                float highFrequency = _pendingLatchHighFrequency;
                float durationSeconds = _pendingLatchDurationSeconds;
                float frequencyHz = _pendingLatchFrequencyHz;
                byte motorMask = _pendingLatchMotorMask;
                _pendingLatchHaptic = false;

                ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                    lowFrequency,
                    highFrequency,
                    durationSeconds,
                    frequencyHz,
                    HapticPriorityCritical,
                    motorMask);
            }

            if (_pendingLockHaptic)
            {
                float lowFrequency = _pendingLockLowFrequency;
                float highFrequency = _pendingLockHighFrequency;
                float durationSeconds = _pendingLockDurationSeconds;
                float frequencyHz = _pendingLockFrequencyHz;
                _pendingLockHaptic = false;

                ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                    lowFrequency,
                    highFrequency,
                    durationSeconds,
                    frequencyHz,
                    HapticPriorityCritical,
                    LeftMotorMask | RightMotorMask);
            }
        }

        private void ClearPendingHaptics()
        {
            _pendingLatchHaptic = false;
            _pendingLatchMotorMask = 0;
            _pendingLatchLowFrequency = 0f;
            _pendingLatchHighFrequency = 0f;
            _pendingLatchDurationSeconds = 0f;
            _pendingLatchFrequencyHz = 0f;
            ClearPendingLockHaptic();
        }

        private void ClearPendingLockHaptic()
        {
            _pendingLockHaptic = false;
            _pendingLockLowFrequency = 0f;
            _pendingLockHighFrequency = 0f;
            _pendingLockDurationSeconds = 0f;
            _pendingLockFrequencyHz = 0f;
        }

        private bool TryRegisterFixedTick()
        {
            if (_registeredFixedTick)
                return true;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            if (_registeredFixedTick)
            {
                _fixedTickDormant = false;
                return true;
            }

            return false;
        }

        private void TryUnregisterFixedTick()
        {
            if (!_registeredFixedTick)
                return;

            SystemDispatcher.Unregister(this as IFixedTickable, PriorityLayer.Player);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixedTick = false;
            _fixedTickDormant = false;
        }

        private bool TryRegisterLateFrame()
        {
            if (_registeredLateFrame)
                return true;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            return _registeredLateFrame;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.Player);
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void RefreshColdRegistryReferences()
        {
            _playerMotor = GlobalRegistry.PlayerSeatLockMotor;
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void ReleaseSeatLockForLostRuntimeRoute()
        {
            _seatLockActive = false;
            ClearPendingLockHaptic();
            TryUnregisterFixedTick();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerMotor:
                    _playerMotor = currentService as IPlayerSeatLockMotorSink;
                    if (_seatLockActive && !TryEnsurePlayerMotor())
                        ReleaseSeatLockForLostRuntimeRoute();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    if (_seatLockActive && !TryEnsurePlayerMotor())
                        ReleaseSeatLockForLostRuntimeRoute();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    bool shouldRestoreFixedTick = _seatLockActive;
                    bool shouldRestoreLateFrame = _pendingLatchHaptic || _pendingLockHaptic;
                    TryUnregisterFixedTick();
                    TryUnregisterLateFrame();
                    if (shouldRestoreFixedTick &&
                        (currentService == null || !isActiveAndEnabled || !TryRegisterFixedTick()))
                    {
                        ReleaseSeatLockForLostRuntimeRoute();
                    }

                    if (shouldRestoreLateFrame &&
                        (currentService == null || !isActiveAndEnabled || !TryRegisterLateFrame()))
                    {
                        ClearPendingHaptics();
                    }
                    break;
            }
        }

        private bool TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
                return true;
            if (!Application.isPlaying)
                return false;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
            return _registeredHotSwapListener;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private static byte ResolveMotorMask(PhysicalHandSide handSide, LifePodSeatStrapSide strapSide)
        {
            if (handSide == PhysicalHandSide.Left)
                return LeftMotorMask;

            if (handSide == PhysicalHandSide.Right)
                return RightMotorMask;

            return strapSide == LifePodSeatStrapSide.Left ? LeftMotorMask : RightMotorMask;
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static float ApproximateMagnitudeNoSqrt(Vector3 value)
        {
            float3 absValue = math.abs(new float3(value.x, value.y, value.z));
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private static float SanitizeFixedDeltaSeconds(float value)
        {
            return math.isfinite(value)
                ? math.clamp(value, MinimumFixedDeltaTime, MaximumSeatLockFixedDeltaTime)
                : MinimumFixedDeltaTime;
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private void CacheScalarConfig()
        {
            _resolvedMaximumCorrectionMetersPerSecond = ResolveSafeMaximumCorrectionMetersPerSecond();
            float hardSnapDistance = ResolveSafeHardSnapDistanceMeters();
            _resolvedHardSnapDistanceSq = hardSnapDistance * hardSnapDistance;
            _resolvedLatchLowFrequency = Sanitize01(latchLowFrequency);
            _resolvedLatchHighFrequency = Sanitize01(latchHighFrequency);
            _resolvedLatchHapticDurationSeconds = ResolveSafeHapticDuration(latchHapticDurationSeconds);
            _resolvedLatchHapticFrequencyHz = ResolveSafeHapticFrequency(latchHapticFrequencyHz);
            _resolvedLockLowFrequency = Sanitize01(lockLowFrequency);
            _resolvedLockHighFrequency = Sanitize01(lockHighFrequency);
            _resolvedLockHapticDurationSeconds = ResolveSafeHapticDuration(lockHapticDurationSeconds);
            _resolvedLockHapticFrequencyHz = ResolveSafeHapticFrequency(lockHapticFrequencyHz);
        }

        private float ResolveSafeMaximumCorrectionMetersPerSecond()
        {
            return math.isfinite(maximumCorrectionMetersPerSecond)
                ? math.clamp(maximumCorrectionMetersPerSecond, 0.01f, MaximumCorrectionMetersPerSecond)
                : 6f;
        }

        private float ResolveSafeHardSnapDistanceMeters()
        {
            return math.isfinite(hardSnapDistanceMeters)
                ? math.clamp(hardSnapDistanceMeters, 0.0001f, MaximumHardSnapDistanceMeters)
                : 0.025f;
        }

        private static float ResolveSafeHapticDuration(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0.01f, MaximumHapticDurationSeconds) : 0.01f;
        }

        private static float ResolveSafeHapticFrequency(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 1f, MaximumHapticFrequencyHz) : MaximumHapticFrequencyHz;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(maximumCorrectionMetersPerSecond))
                maximumCorrectionMetersPerSecond = 6f;
            if (!math.isfinite(hardSnapDistanceMeters))
                hardSnapDistanceMeters = 0.025f;
            maximumCorrectionMetersPerSecond = math.clamp(maximumCorrectionMetersPerSecond, 0.01f, MaximumCorrectionMetersPerSecond);
            hardSnapDistanceMeters = math.clamp(hardSnapDistanceMeters, 0.0001f, MaximumHardSnapDistanceMeters);
            if (!math.isfinite(latchLowFrequency))
                latchLowFrequency = 0.18f;
            if (!math.isfinite(latchHighFrequency))
                latchHighFrequency = 0.35f;
            if (!math.isfinite(latchHapticDurationSeconds))
                latchHapticDurationSeconds = 0.055f;
            if (!math.isfinite(latchHapticFrequencyHz))
                latchHapticFrequencyHz = MaximumHapticFrequencyHz;
            if (!math.isfinite(lockLowFrequency))
                lockLowFrequency = 0.35f;
            if (!math.isfinite(lockHighFrequency))
                lockHighFrequency = 0.55f;
            if (!math.isfinite(lockHapticDurationSeconds))
                lockHapticDurationSeconds = 0.09f;
            if (!math.isfinite(lockHapticFrequencyHz))
                lockHapticFrequencyHz = MaximumHapticFrequencyHz;
            latchLowFrequency = Sanitize01(latchLowFrequency);
            latchHighFrequency = Sanitize01(latchHighFrequency);
            latchHapticDurationSeconds = ResolveSafeHapticDuration(latchHapticDurationSeconds);
            latchHapticFrequencyHz = ResolveSafeHapticFrequency(latchHapticFrequencyHz);
            lockLowFrequency = Sanitize01(lockLowFrequency);
            lockHighFrequency = Sanitize01(lockHighFrequency);
            lockHapticDurationSeconds = ResolveSafeHapticDuration(lockHapticDurationSeconds);
            lockHapticFrequencyHz = ResolveSafeHapticFrequency(lockHapticFrequencyHz);
            CacheScalarConfig();
        }
#endif
    }
}
