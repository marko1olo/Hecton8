namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
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
    public sealed class LifePodSeatStrapCoordinator : MonoBehaviour, IFixedTickable
    {
        private const byte HapticPriorityCritical = 3;
        private const byte LeftMotorMask = 0x01;
        private const byte RightMotorMask = 0x02;
        private const float MinimumFixedDeltaTime = 0.0001f;

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
        private Transform _leftIkAnchor;
        private Transform _rightIkAnchor;
        private HectonPlayerMotor _playerMotor;
        private Rigidbody _playerBody;
        private Transform _playerTransform;
        private AbsoluteUniversePosition _seatLockAup;

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

            TryUpdateSeatAup();
        }

        private void OnEnable()
        {
            if (_seatLockActive)
                TryRegisterFixedTick();
        }

        private void OnDisable()
        {
            ReleaseSeatLock();
            TryUnregisterFixedTick();
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
            if (!_seatLockActive)
            {
                TryUnregisterFixedTick();
                return;
            }

            if (!TryResolvePlayerMotor())
                return;

            if (!TryResolveSeatPosition(out Vector3 targetPosition))
                return;

            float safeFixedDeltaTime = math.max(fixedDeltaTime, MinimumFixedDeltaTime);
            Vector3 currentPosition = _playerBody != null ? _playerBody.position : _playerTransform.position;
            Vector3 delta = targetPosition - currentPosition;
            if (!IsFinite(delta))
                return;

            float distanceSq = delta.sqrMagnitude;
            float hardSnapDistanceSq = hardSnapDistanceMeters * hardSnapDistanceMeters;
            if (distanceSq <= hardSnapDistanceSq)
            {
                _playerMotor.MovePosition(targetPosition);
                if (zeroLinearVelocityWhileLocked)
                    _playerMotor.SetLinearVelocity(Vector3.zero);
                return;
            }

            float maxStep = maximumCorrectionMetersPerSecond * safeFixedDeltaTime;
            float maxStepSq = maxStep * maxStep;
            if (distanceSq <= maxStepSq)
            {
                _playerMotor.MovePosition(targetPosition);
                if (zeroLinearVelocityWhileLocked)
                    _playerMotor.SetLinearVelocity(Vector3.zero);
                return;
            }

            float invDistance = math.rsqrt(distanceSq);
            Vector3 nextPosition = currentPosition + delta * (maxStep * invDistance);

            if (!IsFinite(nextPosition))
                return;

            _playerMotor.MovePosition(nextPosition);
            if (zeroLinearVelocityWhileLocked)
                _playerMotor.SetLinearVelocity(Vector3.zero);
        }

        private void EngageSeatLock()
        {
            if (!TryUpdateSeatAup())
                return;

            _seatLockActive = true;
            TryResolvePlayerMotor();
            TryRegisterFixedTick();

            if (hapticsEnabled)
            {
                ToolHapticsRuntime.EnqueueSinusoidalCommand(
                    lockLowFrequency,
                    lockHighFrequency,
                    lockHapticDurationSeconds,
                    lockHapticFrequencyHz,
                    HapticPriorityCritical,
                    LeftMotorMask | RightMotorMask);
            }
        }

        private bool TryResolvePlayerMotor()
        {
            HectonPlayerMotor motor = GlobalRegistry.PlayerMotor;
            if (motor == null || motor.Body == null)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _playerMotor = motor;
            _playerBody = motor.Body;
            _playerTransform = playerContext != null && playerContext.IsInitialized && playerContext.PlayerTransform != null
                ? playerContext.PlayerTransform
                : motor.transform;
            return _playerTransform != null;
        }

        private bool TryResolveSeatPosition(out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            if (seatAnchor == null)
                return false;

            targetPosition = seatAnchor.position;
            if (!IsFinite(targetPosition))
                return false;

            _seatLockAup = AbsoluteUniversePosition.FromRuntimePosition(targetPosition);
            return true;
        }

        private bool TryUpdateSeatAup()
        {
            return TryResolveSeatPosition(out _);
        }

        private void QueueLatchHaptic(PhysicalHandSide handSide, LifePodSeatStrapSide strapSide)
        {
            if (!hapticsEnabled)
                return;

            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                latchLowFrequency,
                latchHighFrequency,
                latchHapticDurationSeconds,
                latchHapticFrequencyHz,
                HapticPriorityCritical,
                ResolveMotorMask(handSide, strapSide));
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredFixedTick || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixedTick = true;
        }

        private void TryUnregisterFixedTick()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixedTick = false;
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
    }
}
