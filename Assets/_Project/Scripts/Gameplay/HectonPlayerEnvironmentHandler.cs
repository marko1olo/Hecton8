using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Environment-side buffer and execution bridge for currents, updrafts, and hull stress.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player Environment Handler")]
    public sealed class HectonPlayerEnvironmentHandler : MonoBehaviour, IHectonPlayerEnvironmentHandler, IEnvironmentHandler
    {
        private HectonPlayerMovement _owner;
        private IMotorForces _motorForces;
        private Vector3 _bufferedExternalAcceleration;
        private Vector3 _bufferedVelocityChange;
        private float _bufferedHullStress;
        private readonly RaycastHit[] _environmentHitBuffer = new RaycastHit[32]; // COLD ALLOC: RaycastHit[32] — reserved environment query buffer for external-force subsystem ownership isolation — owner: HectonPlayerEnvironmentHandler

        /// <summary>
        /// Dedicated environment query buffer. Environment-owned only.
        /// </summary>
        public RaycastHit[] EnvironmentHitBuffer => _environmentHitBuffer;

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

            if (_motorForces == null && TryGetComponent(out HectonPlayerMotor motor))
                _motorForces = motor;
        }

        private void OnDisable()
        {
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

        /// <summary>
        /// Executes the authoritative environment pass for the current fixed step.
        /// </summary>
        public void ExecuteStep(float fixedDeltaTime)
        {
            ExecuteStep(fixedDeltaTime, true);
        }

        /// <summary>
        /// Executes the authoritative environment pass and optionally suppresses direct motor writes.
        /// </summary>
        public void ExecuteStep(float fixedDeltaTime, bool applyToMotor)
        {
            if (_owner == null || _motorForces == null)
                return;

            ResetStepBuffers();

            PlayerTransportPreset activeTransportPreset = _owner.ResolveActiveTransportPresetForSubsystems();
            _owner.ExecuteEnvironmentForcePhase(fixedDeltaTime, activeTransportPreset);

            Vector3 bufferedAcceleration = ConsumeExternalAcceleration();
            if (applyToMotor && bufferedAcceleration.sqrMagnitude > 0.000001f)
                _motorForces.AddExternalAcceleration(bufferedAcceleration);

            Vector3 bufferedVelocityChange = ConsumeVelocityChange();
            if (applyToMotor && bufferedVelocityChange.sqrMagnitude > 0.000001f)
                _motorForces.AddExternalVelocityChange(bufferedVelocityChange);

            float bufferedHullStress = ConsumeHullStress();
            if (bufferedHullStress > 0.0001f)
                _owner.AccumulateBufferedEnvironmentalHullStress(bufferedHullStress);

            _owner.ExecuteEnvironmentStressPhase(fixedDeltaTime, activeTransportPreset);
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
    }
}
