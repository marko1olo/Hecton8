using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Environment-side buffer for currents, thermal updrafts, and hull-stress requests.
    /// </summary>
    [DefaultExecutionOrder(10)] // Explicit helper registration ordering: owner -> environment -> motor.
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player Environment Handler")]
    public sealed class HectonPlayerEnvironmentHandler : MonoBehaviour, IHectonPlayerEnvironmentHandler, IFixedTickable
    {
        private HectonPlayerMovement _owner;
        private IMotorForces _motorForces;
        private Vector3 _bufferedExternalAcceleration;
        private Vector3 _bufferedVelocityChange;
        private float _bufferedHullStress;
        private bool _registered;

        /// <summary>Binds authoritative owner references.</summary>
        public void Bind(HectonPlayerMovement owner, IMotorForces motorForces)
        {
            _owner = owner;
            _motorForces = motorForces;
        }

        private void Awake()
        {
            if (_owner == null)
                TryGetComponent(out _owner);
        }

        private void OnEnable()
        {
            if (_owner == null)
                TryGetComponent(out _owner);

            if (_motorForces == null && TryGetComponent(out HectonPlayerMotor motor))
                _motorForces = motor;

            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ResetRuntimeState();
        }

        /// <inheritdoc />
        public void ResetStepBuffers()
        {
            _bufferedExternalAcceleration = Vector3.zero;
            _bufferedVelocityChange = Vector3.zero;
            _bufferedHullStress = 0f;
        }

        /// <inheritdoc />
        public void ResetRuntimeState()
        {
            ResetStepBuffers();
        }

        /// <inheritdoc />
        public void QueueExternalAcceleration(Vector3 acceleration)
        {
            float3 acceleration3 = new float3(acceleration.x, acceleration.y, acceleration.z);
            if (!math.all(math.isfinite(acceleration3)))
                return;

            if (math.lengthsq(acceleration3) <= 0.000001f)
                return;

            _bufferedExternalAcceleration += acceleration;
        }

        /// <inheritdoc />
        public void QueueVelocityChange(Vector3 velocityChange)
        {
            float3 velocityChange3 = new float3(velocityChange.x, velocityChange.y, velocityChange.z);
            if (!math.all(math.isfinite(velocityChange3)))
                return;

            if (math.lengthsq(velocityChange3) <= 0.000001f)
                return;

            _bufferedVelocityChange += velocityChange;
        }

        /// <inheritdoc />
        public void QueueHullStress(float normalizedStress)
        {
            float clamped = math.saturate(normalizedStress);
            if (clamped > _bufferedHullStress)
                _bufferedHullStress = clamped;
        }

        /// <inheritdoc />
        public Vector3 ConsumeExternalAcceleration()
        {
            Vector3 value = _bufferedExternalAcceleration;
            _bufferedExternalAcceleration = Vector3.zero;
            return value;
        }

        /// <inheritdoc />
        public Vector3 ConsumeVelocityChange()
        {
            Vector3 value = _bufferedVelocityChange;
            _bufferedVelocityChange = Vector3.zero;
            return value;
        }

        /// <inheritdoc />
        public float ConsumeHullStress()
        {
            float value = _bufferedHullStress;
            _bufferedHullStress = 0f;
            return value;
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (_owner == null || _motorForces == null)
                return;

            PlayerTransportPreset activeTransportPreset = _owner.ResolveActiveTransportPresetForSubsystems();
            _owner.ExecuteEnvironmentForcePhase(fixedDeltaTime, activeTransportPreset);

            Vector3 bufferedAcceleration = ConsumeExternalAcceleration();
            if (bufferedAcceleration.sqrMagnitude > 0.000001f)
                _motorForces.AddExternalAcceleration(bufferedAcceleration);

            Vector3 bufferedVelocityChange = ConsumeVelocityChange();
            if (bufferedVelocityChange.sqrMagnitude > 0.000001f)
                _motorForces.AddExternalVelocityChange(bufferedVelocityChange);

            float bufferedHullStress = ConsumeHullStress();
            if (bufferedHullStress > 0.0001f)
                _owner.AccumulateBufferedEnvironmentalHullStress(bufferedHullStress);

            _owner.ExecuteEnvironmentStressPhase(fixedDeltaTime, activeTransportPreset);
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register((IFixedTickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister((IFixedTickable)this);
            _registered = false;
        }
    }
}
