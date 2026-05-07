using Hecton8.Core;
using Hecton8.World;
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
    public sealed class SubmarineStationKeepingController : MonoBehaviour, IFixedTickable
    {
        [Header("Station Keeping")]
        [Tooltip("When enabled at runtime, the controller holds the current hull pose until released or retargeted.")]
        [SerializeField] private bool armOnEnable;

        [Tooltip("Maximum cinematic position lock speed in meters per second.")]
        [SerializeField, Min(0.01f)] private float positionLockSpeedMetersPerSecond = 18f;

        [Tooltip("Maximum cinematic attitude lock speed in degrees per second.")]
        [SerializeField, Min(1f)] private float rotationLockDegreesPerSecond = 110f;

        private SubmarineCoreDirector _submarineCore;
        private Rigidbody _hullRigidbody;
        private bool _registeredFixedTick;
        private bool _stationKeepingEnabled;
        private Quaternion _targetRotation = Quaternion.identity;
        private double3 _targetAbsolutePosition;

        /// <summary>True while the controller is actively holding a target pose.</summary>
        public bool IsStationKeepingEnabled => _stationKeepingEnabled;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            TryRegister();
            if (armOnEnable)
                ArmAtCurrentPose();
        }

        private void OnDisable()
        {
            TryUnregister();
            _stationKeepingEnabled = false;
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_stationKeepingEnabled || _hullRigidbody == null || fixedDeltaTime <= 0f)
                return;

            double3 currentAbsolutePosition = AbsoluteUniversePosition.FromRuntimePosition(_hullRigidbody.worldCenterOfMass).ToAbsoluteDouble3();
            float3 offsetToTarget = (float3)(_targetAbsolutePosition - currentAbsolutePosition);
            if (!math.all(math.isfinite(offsetToTarget)))
                return;

            Vector3 hullPosition = _hullRigidbody.position;
            Vector3 nextRuntimePosition = ResolveStationKeepingStepNoSqrt(hullPosition, offsetToTarget, ResolvePositionStepMeters(fixedDeltaTime));
            if (!IsFinite(nextRuntimePosition))
                return;

            _hullRigidbody.linearVelocity = Vector3.zero;
            _hullRigidbody.angularVelocity = Vector3.zero;

            float rotationStep = Mathf.Max(1f, rotationLockDegreesPerSecond) * fixedDeltaTime;
            _hullRigidbody.MovePosition(nextRuntimePosition);
            _hullRigidbody.MoveRotation(Quaternion.RotateTowards(_hullRigidbody.rotation, _targetRotation, rotationStep));
        }

        /// <summary>
        /// Arms station keeping using the current hull pose as the target.
        /// </summary>
        public void ArmAtCurrentPose()
        {
            CacheReferences();
            if (_hullRigidbody == null)
                return;

            _targetAbsolutePosition = AbsoluteUniversePosition.FromRuntimePosition(_hullRigidbody.worldCenterOfMass).ToAbsoluteDouble3();
            _targetRotation = _hullRigidbody.rotation;
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

            _targetAbsolutePosition = absoluteUniversePosition;
            _targetRotation = _hullRigidbody.rotation;
            _stationKeepingEnabled = true;
        }

        /// <summary>
        /// Releases the station-keeping controller.
        /// </summary>
        public void Release()
        {
            _stationKeepingEnabled = false;
        }

        private void CacheReferences()
        {
            if (_submarineCore == null)
                TryGetComponent(out _submarineCore);

            if (_submarineCore != null)
                _hullRigidbody = _submarineCore.HullRigidbody;
        }

        private float ResolvePositionStepMeters(float fixedDeltaTime)
        {
            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            float authoredStep = math.max(0.01f, positionLockSpeedMetersPerSecond) * safeDeltaTime;
            float hullMass = _hullRigidbody != null ? math.max(1f, _hullRigidbody.mass) : 1f;
            float maxThrusterForce = _submarineCore != null
                ? math.max(1f, _submarineCore.MaxThrust)
                : hullMass * math.max(0.01f, positionLockSpeedMetersPerSecond);
            float thrusterAcceleration = maxThrusterForce / hullMass;
            float thrusterLimitedStep = 0.5f * thrusterAcceleration * safeDeltaTime * safeDeltaTime;
            return math.max(0.001f, math.min(authoredStep, thrusterLimitedStep));
        }

        private static Vector3 ResolveStationKeepingStepNoSqrt(Vector3 currentPosition, float3 offsetToTarget, float maxStepMeters)
        {
            float safeStep = math.max(0f, maxStepMeters);
            float stepSq = safeStep * safeStep;
            float offsetSq = math.lengthsq(offsetToTarget);
            float3 current3 = new float3(currentPosition.x, currentPosition.y, currentPosition.z);
            if (offsetSq <= stepSq)
            {
                float3 snapped3 = current3 + offsetToTarget;
                return new Vector3(snapped3.x, snapped3.y, snapped3.z);
            }

            float3 absOffset = math.abs(offsetToTarget);
            float l1Distance = math.max(0.0001f, absOffset.x + absOffset.y + absOffset.z);
            float stepT = math.saturate(safeStep / l1Distance);
            float3 next3 = current3 + (offsetToTarget * stepT);
            return new Vector3(next3.x, next3.y, next3.z);
        }

        private void TryRegister()
        {
            if (_registeredFixedTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = GlobalRegistry.FixedTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
