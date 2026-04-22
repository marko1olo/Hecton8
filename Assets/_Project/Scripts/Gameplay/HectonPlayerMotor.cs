using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Motor-side force buffer for locomotion-owned rigidbody application.
    /// </summary>
    [DefaultExecutionOrder(20)] // Explicit helper registration ordering: owner -> environment -> motor.
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player Motor")]
    public sealed class HectonPlayerMotor : MonoBehaviour, IMotorForces, IFixedTickable
    {
        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private Vector3 _bufferedExternalAcceleration;
        private Vector3 _bufferedExternalVelocityChange;
        private bool _isGrounded;
        private bool _registered;

        /// <inheritdoc />
        public Rigidbody Body => _body;

        /// <inheritdoc />
        public CapsuleCollider Capsule => _capsule;

        /// <inheritdoc />
        public bool IsGrounded => _isGrounded;

        /// <summary>Binds authoritative body references owned by the locomotion controller.</summary>
        public void Bind(Rigidbody body, CapsuleCollider capsule)
        {
            _body = body;
            _capsule = capsule;
        }

        /// <summary>Updates grounded state mirror for external systems.</summary>
        public void SetGroundedState(bool isGrounded)
        {
            _isGrounded = isGrounded;
        }

        /// <inheritdoc />
        public void AddExternalAcceleration(Vector3 acceleration)
        {
            float3 acceleration3 = new float3(acceleration.x, acceleration.y, acceleration.z);
            if (!math.all(math.isfinite(acceleration3)))
                return;

            if (math.lengthsq(acceleration3) <= 0.000001f)
                return;

            _bufferedExternalAcceleration += acceleration;
        }

        /// <inheritdoc />
        public void AddExternalVelocityChange(Vector3 velocityChange)
        {
            float3 velocityChange3 = new float3(velocityChange.x, velocityChange.y, velocityChange.z);
            if (!math.all(math.isfinite(velocityChange3)))
                return;

            if (math.lengthsq(velocityChange3) <= 0.000001f)
                return;

            _bufferedExternalVelocityChange += velocityChange;
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ResetRuntimeState();
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (_body == null)
            {
                _bufferedExternalAcceleration = Vector3.zero;
                _bufferedExternalVelocityChange = Vector3.zero;
                return;
            }

            if (_bufferedExternalAcceleration.sqrMagnitude > 0.000001f)
                _body.AddForce(_bufferedExternalAcceleration, ForceMode.Acceleration);

            if (_bufferedExternalVelocityChange.sqrMagnitude > 0.000001f)
                _body.AddForce(_bufferedExternalVelocityChange, ForceMode.VelocityChange);

            _bufferedExternalAcceleration = Vector3.zero;
            _bufferedExternalVelocityChange = Vector3.zero;
        }

        /// <summary>Clears transient runtime state.</summary>
        public void ResetRuntimeState()
        {
            _bufferedExternalAcceleration = Vector3.zero;
            _bufferedExternalVelocityChange = Vector3.zero;
            _isGrounded = false;
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
