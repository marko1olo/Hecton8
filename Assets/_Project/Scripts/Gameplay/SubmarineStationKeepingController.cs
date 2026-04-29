using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Fixed-step PID controller that keeps the submarine hull near a target AUP pose without direct Rigidbody writes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineCoreDirector))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Submarine Station Keeping Controller")]
    public sealed class SubmarineStationKeepingController : MonoBehaviour, IFixedTickable
    {
        private const float DefaultMinSpatialLengthMeters = 1f;

        [Header("Station Keeping")]
        [Tooltip("When enabled at runtime, the controller holds the current hull pose until released or retargeted.")]
        [SerializeField] private bool armOnEnable;

        [Tooltip("Maximum translational acceleration authored by the PID in meters per second squared.")]
        [SerializeField, Range(0.1f, 30f)] private float maxLinearAcceleration = 7.5f;

        [Tooltip("Maximum angular acceleration authored by the attitude hold in radians per second squared.")]
        [SerializeField, Range(0.1f, 30f)] private float maxAngularAcceleration = 4.5f;

        [Tooltip("Position error gain for the translational PID.")]
        [SerializeField, Range(0f, 8f)] private float proportionalGain = 1.85f;

        [Tooltip("Integral error gain for the translational PID.")]
        [SerializeField, Range(0f, 4f)] private float integralGain = 0.14f;

        [Tooltip("Velocity damping gain for the translational PID.")]
        [SerializeField, Range(0f, 8f)] private float derivativeGain = 2.6f;

        [Tooltip("How strongly sampled ocean current velocity is counter-fed into the linear controller.")]
        [SerializeField, Range(0f, 4f)] private float currentCompensationGain = 0.65f;

        [Tooltip("Orientation restoring gain for station-keeping attitude hold.")]
        [SerializeField, Range(0f, 12f)] private float angularProportionalGain = 3.4f;

        [Tooltip("Angular-velocity damping for station-keeping attitude hold.")]
        [SerializeField, Range(0f, 12f)] private float angularDerivativeGain = 2.8f;

        [Tooltip("Integral clamp in meters to prevent runaway windup.")]
        [SerializeField, Range(0.1f, 50f)] private float integralClampMeters = 12f;

        private SubmarineCoreDirector _submarineCore;
        private Rigidbody _hullRigidbody;
        private bool _registeredFixedTick;
        private bool _stationKeepingEnabled;
        private Quaternion _targetRotation = Quaternion.identity;
        private double3 _targetAbsolutePosition;
        private float3 _integralError;

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
            _integralError = float3.zero;
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
            float3 positionError = (float3)(_targetAbsolutePosition - currentAbsolutePosition);
            _integralError += positionError * fixedDeltaTime;
            _integralError = math.clamp(_integralError, new float3(-integralClampMeters), new float3(integralClampMeters));

            float3 linearVelocity = _hullRigidbody.linearVelocity;
            float3 proportionalTerm = positionError * proportionalGain;
            float3 integralTerm = _integralError * integralGain;
            float3 derivativeTerm = (-linearVelocity) * derivativeGain;
            float3 feedForwardTerm = float3.zero;

            IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null ? oceanKinematicsService.ActiveProvider : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                oceanKinematics.TrySampleWaterVelocity(_hullRigidbody.worldCenterOfMass, DefaultMinSpatialLengthMeters, out float3 waterVelocity))
            {
                feedForwardTerm = (-waterVelocity) * currentCompensationGain;
            }

            float3 commandedAcceleration = proportionalTerm + integralTerm + derivativeTerm + feedForwardTerm;

            float accelerationMagnitude = math.length(commandedAcceleration);
            if (accelerationMagnitude > maxLinearAcceleration && accelerationMagnitude > 0.0001f)
                commandedAcceleration = commandedAcceleration * (maxLinearAcceleration / accelerationMagnitude);

            Quaternion currentRotation = _hullRigidbody.rotation;
            Quaternion deltaRotation = _targetRotation * Quaternion.Inverse(currentRotation);
            deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            Vector3 safeAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            float3 angularVelocity = _hullRigidbody.angularVelocity;
            float3 commandedAngularAcceleration = (float3)(safeAxis * (angleRadians * angularProportionalGain)) +
                                                  ((-angularVelocity) * angularDerivativeGain);
            float angularAccelerationMagnitude = math.length(commandedAngularAcceleration);
            if (angularAccelerationMagnitude > maxAngularAcceleration && angularAccelerationMagnitude > 0.0001f)
            {
                commandedAngularAcceleration = commandedAngularAcceleration * (maxAngularAcceleration / angularAccelerationMagnitude);
            }

            PhysicsForceRouter.QueueForce(_hullRigidbody, commandedAcceleration, ForceMode.Acceleration);
            PhysicsForceRouter.QueueTorque(_hullRigidbody, commandedAngularAcceleration, ForceMode.Acceleration);
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
            _integralError = float3.zero;
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
            _integralError = float3.zero;
            _stationKeepingEnabled = true;
        }

        /// <summary>
        /// Releases the station-keeping controller and clears PID state.
        /// </summary>
        public void Release()
        {
            _stationKeepingEnabled = false;
            _integralError = float3.zero;
        }

        private void CacheReferences()
        {
            if (_submarineCore == null)
                TryGetComponent(out _submarineCore);

            if (_submarineCore != null)
                _hullRigidbody = _submarineCore.HullRigidbody;
        }

        private void TryRegister()
        {
            if (_registeredFixedTick)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }
    }
}
