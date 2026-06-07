using Hecton8.Core;
using Hecton8.World;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Fixed-step cinematic lock that keeps the submarine hull near a target AUP pose.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineCoreDirector))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Station Keeping Controller")]
    public sealed class SubmarineStationKeepingController : MonoBehaviour, IFixedTickable, IGlobalRegistryHotSwapListener
    {
        private const float PositionHoldEpsilonMetersSq = 0.000001f;
        private const float RotationHoldDotThreshold = 0.9999995f;
        private const float AutoLevelPlanarForwardEpsilonSq = 0.0001f;

        [Header("Station Keeping")]
        [Tooltip("When enabled at runtime, the controller holds the current hull pose until released or retargeted.")]
        [SerializeField] private bool armOnEnable;

        [Tooltip("Maximum cinematic position lock speed in meters per second.")]
        [SerializeField, Min(0.01f)] private float positionLockSpeedMetersPerSecond = 18f;

        [Tooltip("Maximum cinematic attitude lock speed in degrees per second.")]
        [SerializeField, Min(1f)] private float rotationLockDegreesPerSecond = 110f;

        private SubmarineCoreDirector _submarineCore;
        private Rigidbody _hullRigidbody;
        private IPhysicsService _physicsService;
        private bool _registeredFixedTick;
        private bool _hotSwapRegistered;
        private bool _stationKeepingEnabled;
        private Quaternion _targetRotation = Quaternion.identity;
        private double3 _targetAbsolutePosition;
        private float _stationKeepingSpeedMetersPerSecond;

        /// <summary>True while the controller is actively holding a target pose.</summary>
        public bool IsStationKeepingEnabled => _stationKeepingEnabled;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            TryRegisterHotSwapListener();
            TryRegister();
            if (armOnEnable)
                ArmAtCurrentPose();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            _stationKeepingEnabled = false;
            _stationKeepingSpeedMetersPerSecond = 0f;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_stationKeepingEnabled || _hullRigidbody == null || fixedDeltaTime <= 0f)
                return;

            if (!TryResolveAbsolutePositionFromRuntimeOrigin(_hullRigidbody.worldCenterOfMass, out double3 currentAbsolutePosition))
                return;

            double3 offsetToTarget = _targetAbsolutePosition - currentAbsolutePosition;
            double offsetToTargetSq = math.lengthsq(offsetToTarget);
            if (!math.all(math.isfinite(offsetToTarget)) || !math.isfinite(offsetToTargetSq))
                return;

            Vector3 hullPosition = ResolveHullRuntimePosition();
            Quaternion currentRotation = _hullRigidbody.rotation;
            if (offsetToTargetSq <= PositionHoldEpsilonMetersSq &&
                IsRotationClose(currentRotation, _targetRotation))
            {
                IPhysicsService holdPhysicsService = _physicsService;
                if (!_hullRigidbody.isKinematic && holdPhysicsService != null)
                {
                    holdPhysicsService.QueueLinearVelocitySet(_hullRigidbody, Vector3.zero, wake: false);
                    holdPhysicsService.QueueAngularVelocitySet(_hullRigidbody, Vector3.zero, wake: false);
                }

                _stationKeepingSpeedMetersPerSecond = 0f;
                return;
            }

            double stepMeters = ResolvePositionStepMeters(offsetToTarget, fixedDeltaTime);
            double3 stepDelta = offsetToTarget;
            double safeStepMeters = math.max(0.0d, stepMeters);
            double safeStepSq = safeStepMeters * safeStepMeters;
            if (safeStepSq > 0.0d && safeStepSq < offsetToTargetSq)
            {
                double inverseDistance = math.rsqrt(math.max(offsetToTargetSq, 0.0001d));
                stepDelta = offsetToTarget * (safeStepMeters * inverseDistance);
            }

            Vector3 nextRuntimePosition = hullPosition + ToVector3(stepDelta);
            if (!IsFinite(nextRuntimePosition))
                return;

            float safeDeltaTime = math.max(fixedDeltaTime, 0.0001f);
            float rotationStep = math.max(1f, rotationLockDegreesPerSecond) * fixedDeltaTime;
            Quaternion nextRotation = Quaternion.RotateTowards(currentRotation, _targetRotation, rotationStep);
            Vector3 impliedLinearVelocity = (nextRuntimePosition - hullPosition) / safeDeltaTime;
            Vector3 impliedAngularVelocity = ResolveAngularVelocityRadians(currentRotation, nextRotation, safeDeltaTime);
            if (!IsFinite(impliedLinearVelocity) || !IsFinite(impliedAngularVelocity))
                return;

            impliedLinearVelocity = ClampMagnitude(impliedLinearVelocity, math.max(0.01f, positionLockSpeedMetersPerSecond));
            impliedAngularVelocity = ClampMagnitude(impliedAngularVelocity, math.radians(math.max(1f, rotationLockDegreesPerSecond)));
            nextRuntimePosition = hullPosition + impliedLinearVelocity * safeDeltaTime;
            if (!IsFinite(nextRuntimePosition))
                return;

            IPhysicsService physicsService = _physicsService;
            if (!_hullRigidbody.isKinematic && physicsService != null)
            {
                physicsService.QueueLinearVelocitySet(_hullRigidbody, impliedLinearVelocity);
                physicsService.QueueAngularVelocitySet(_hullRigidbody, impliedAngularVelocity);
            }

            _hullRigidbody.MovePosition(nextRuntimePosition);
            _hullRigidbody.MoveRotation(nextRotation);
        }

        /// <summary>
        /// Arms station keeping using the current hull pose as the target.
        /// </summary>
        public void ArmAtCurrentPose()
        {
            CacheReferences();
            if (_hullRigidbody == null)
                return;

            if (!TryResolveAbsolutePositionFromRuntimeOrigin(_hullRigidbody.worldCenterOfMass, out _targetAbsolutePosition))
            {
                _stationKeepingEnabled = false;
                _stationKeepingSpeedMetersPerSecond = 0f;
                return;
            }

            _targetRotation = _hullRigidbody.rotation;
            _stationKeepingSpeedMetersPerSecond = 0f;
            _stationKeepingEnabled = true;
        }

        /// <summary>
        /// Arms station keeping on a supplied target AUP position while keeping the current hull attitude.
        /// </summary>
        public void ArmAtTarget(double3 absoluteUniversePosition)
        {
            CacheReferences();
            if (_hullRigidbody == null)
                return;

            if (!math.all(math.isfinite(absoluteUniversePosition)))
            {
                _stationKeepingEnabled = false;
                _stationKeepingSpeedMetersPerSecond = 0f;
                return;
            }

            _targetAbsolutePosition = absoluteUniversePosition;
            _targetRotation = _hullRigidbody.rotation;
            _stationKeepingSpeedMetersPerSecond = 0f;
            _stationKeepingEnabled = true;
        }

        /// <summary>
        /// Releases the station-keeping controller.
        /// </summary>
        public void Release()
        {
            _stationKeepingEnabled = false;
            _stationKeepingSpeedMetersPerSecond = 0f;
        }

        /// <summary>
        /// Arms a pitch/roll correction while retaining current yaw and position. Call after player controls are released.
        /// </summary>
        public async Awaitable AutoLevelWhenControlsReleasedAsync(CancellationToken cancellationToken = default)
        {
            ArmAutoLevelAtCurrentPosition();
            while (!cancellationToken.IsCancellationRequested &&
                   isActiveAndEnabled &&
                   _stationKeepingEnabled &&
                   _hullRigidbody != null &&
                   !IsRotationClose(_hullRigidbody.rotation, _targetRotation))
            {
                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Arms the no-allocation fixed-step auto-level path for control owners that cannot await completion.
        /// </summary>
        public void ArmAutoLevelAtCurrentPosition()
        {
            CacheReferences();
            if (_hullRigidbody == null)
                return;

            if (!TryResolveAbsolutePositionFromRuntimeOrigin(_hullRigidbody.worldCenterOfMass, out _targetAbsolutePosition))
            {
                _stationKeepingEnabled = false;
                _stationKeepingSpeedMetersPerSecond = 0f;
                return;
            }

            _targetRotation = ResolveAutoLevelRotation(_hullRigidbody.rotation);
            _stationKeepingSpeedMetersPerSecond = 0f;
            _stationKeepingEnabled = true;
        }

        private void CacheReferences()
        {
            if (_submarineCore == null)
                TryGetComponent(out _submarineCore);

            if (_submarineCore != null)
                _hullRigidbody = _submarineCore.HullRigidbody;

            if (_physicsService == null)
                _physicsService = GlobalRegistry.Physics;
        }

        private double ResolvePositionStepMeters(double3 offsetToTarget, float fixedDeltaTime)
        {
            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            double offsetSq = math.lengthsq(offsetToTarget);
            if (offsetSq <= 0.000001f)
            {
                _stationKeepingSpeedMetersPerSecond = 0f;
                return 0.0d;
            }

            float authoredSpeed = math.max(0.01f, positionLockSpeedMetersPerSecond);
            float hullMass = _hullRigidbody != null ? math.max(1f, _hullRigidbody.mass) : 1f;
            float maxThrusterForce = _submarineCore != null
                ? math.max(1f, _submarineCore.MaxThrust)
                : hullMass * authoredSpeed;
            float thrusterAcceleration = maxThrusterForce * math.rcp(math.max(hullMass, 1f));
            _stationKeepingSpeedMetersPerSecond = math.min(
                authoredSpeed,
                _stationKeepingSpeedMetersPerSecond + thrusterAcceleration * safeDeltaTime);
            return math.max(0.001f, _stationKeepingSpeedMetersPerSecond * safeDeltaTime);
        }

        private void TryRegister()
        {
            if (_registeredFixedTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private Vector3 ResolveHullRuntimePosition()
        {
            return _hullRigidbody != null ? _hullRigidbody.position : Vector3.zero;
        }

        private static bool TryResolveAbsolutePositionFromRuntimeOrigin(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!AbsoluteUniversePosition.IsFinite(in positionAup))
                return false;

            absolutePosition = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePosition));
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static Vector3 ClampMagnitude(Vector3 value, float maxMagnitude)
        {
            float sqrMagnitude = value.sqrMagnitude;
            if (!float.IsFinite(sqrMagnitude) || sqrMagnitude <= 0.000001f)
                return Vector3.zero;

            float safeMax = math.max(0f, maxMagnitude);
            float maxSq = safeMax * safeMax;
            return sqrMagnitude <= maxSq
                ? value
                : value * (safeMax * math.rsqrt(math.max(sqrMagnitude, 0.000001f)));
        }

        private static bool IsRotationClose(Quaternion currentRotation, Quaternion targetRotation)
        {
            float dot =
                currentRotation.x * targetRotation.x +
                currentRotation.y * targetRotation.y +
                currentRotation.z * targetRotation.z +
                currentRotation.w * targetRotation.w;
            return math.abs(dot) >= RotationHoldDotThreshold;
        }

        private static Vector3 ResolveAngularVelocityRadians(Quaternion currentRotation, Quaternion nextRotation, float deltaTime)
        {
            Quaternion inverseCurrent = new Quaternion(
                -currentRotation.x,
                -currentRotation.y,
                -currentRotation.z,
                currentRotation.w);
            Quaternion deltaRotation = nextRotation * inverseCurrent;
            float4 q = new float4(deltaRotation.x, deltaRotation.y, deltaRotation.z, deltaRotation.w);
            if (q.w < 0f)
                q = -q;

            q = NormalizeQuaternionNoSqrt(q);
            float3 angularDelta = new float3(q.x, q.y, q.z) * 2f;
            if (!math.all(math.isfinite(angularDelta)) || math.lengthsq(angularDelta) <= 0.00000001f)
                return Vector3.zero;

            float inverseDeltaTime = math.rcp(math.max(deltaTime, 0.0001f));
            Vector3 angularVelocity = new Vector3(
                angularDelta.x * inverseDeltaTime,
                angularDelta.y * inverseDeltaTime,
                angularDelta.z * inverseDeltaTime);
            return IsFinite(angularVelocity) ? angularVelocity : Vector3.zero;
        }

        private static Quaternion ResolveAutoLevelRotation(Quaternion currentRotation)
        {
            Vector3 forward = currentRotation * Vector3.forward;
            Vector3 planarForward = new Vector3(forward.x, 0f, forward.z);
            float planarForwardSq = planarForward.sqrMagnitude;
            if (!IsFinite(planarForward) || planarForwardSq <= AutoLevelPlanarForwardEpsilonSq)
                planarForward = Vector3.forward;
            else
                planarForward *= math.rsqrt(math.max(planarForwardSq, AutoLevelPlanarForwardEpsilonSq));

            return Quaternion.LookRotation(planarForward, Vector3.up);
        }

        private static float4 NormalizeQuaternionNoSqrt(float4 value)
        {
            float lengthSq = math.max(math.dot(value, value), 0.000001f);
            return value * math.rsqrt(math.max(lengthSq, 0.000001f));
        }
    }
}
