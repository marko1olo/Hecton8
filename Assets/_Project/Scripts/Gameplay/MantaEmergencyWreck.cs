namespace Hecton8.Gameplay
{
    using Hecton8.AI;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Physics;
    using UnityEngine;

    /// <summary>
    /// Pooled emergency world-body for handheld Manta bailouts.
    /// Keeps the dropped scooter physical for a short sink/drift window, then returns it to the pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MantaEmergencyWreck : MonoBehaviour, IPoolable, IFixedTickable
    {
        [Header("── Bailout Drift ──────────────────────────")]
        [Tooltip("How long the detached Manta wreck remains in the world before returning to the pool.")]
        [SerializeField, Range(2f, 60f)] private float bailoutLifetime = 18f;

        [Tooltip("Continuous downward velocity-change applied while the detached wreck sinks.")]
        [SerializeField, Range(0f, 8f)] private float sinkVelocityChangePerSecond = 1.35f;

        [Tooltip("Linear damping used while the detached wreck coasts away from the player.")]
        [SerializeField, Range(0f, 8f)] private float activeLinearDamping = 0.18f;

        [Tooltip("Angular damping used while the detached wreck tumbles into the abyss.")]
        [SerializeField, Range(0f, 8f)] private float activeAngularDamping = 0.52f;

        [Tooltip("Minimum angular spin applied on bailout.")]
        [SerializeField, Range(0f, 12f)] private float spinVelocityMin = 1.35f;

        [Tooltip("Maximum angular spin applied on bailout.")]
        [SerializeField, Range(0f, 24f)] private float spinVelocityMax = 4.6f;

        [Header("── Collision Damage ───────────────────────────")]
        [Tooltip("Minimum collision speed where the drifting wreck starts dealing catastrophic kinetic damage to fauna.")]
        [SerializeField, Range(0f, 60f)] private float collisionDamageStartSpeed = 15f;

        [Tooltip("Collision speed where the drifting wreck reaches maximum kinetic damage.")]
        [SerializeField, Range(0f, 80f)] private float collisionDamageMaxSpeed = 32f;

        [Tooltip("Maximum damage applied when the emergency wreck slams into a fauna target at full authored speed.")]
        [SerializeField, Range(0f, 500f)] private float collisionDamageAtMaxSpeed = 260f;

        [Tooltip("Cooldown preventing the same wreck body from reapplying catastrophic collision damage every single contact frame.")]
        [SerializeField, Range(0f, 1f)] private float collisionDamageCooldown = 0.18f;

        [Header("── Idle Reset ─────────────────────────────")]
        [Tooltip("Linear damping used when the wreck object returns to idle pooled pickup behavior.")]
        [SerializeField, Range(0f, 16f)] private float idleLinearDamping = 8f;

        [Tooltip("Angular damping used when the wreck object returns to idle pooled pickup behavior.")]
        [SerializeField, Range(0f, 16f)] private float idleAngularDamping = 8f;

        private Rigidbody _rigidbody;
        private PickupItem _pickupItem;
        private InteractionHighlighter _interactionHighlighter;
        private bool _registeredFixedTick;
        private bool _emergencyActive;
        private float _remainingLifetime;
        private float _collisionDamageCooldownTimer;

        /// <summary>
        /// Arms the pooled wreck body with inherited bailout inertia and enables short-lived physics drift.
        /// </summary>
        /// <param name="inheritedVelocity">Player/body velocity at the instant of bailout.</param>
        /// <param name="bailoutImpulse">Controller-resolved bailout impulse.</param>
        /// <param name="severity">Normalized crash severity.</param>
        public void ActivateEmergencyDrift(Vector3 inheritedVelocity, Vector3 bailoutImpulse, float severity)
        {
            CachePassiveReferences();
            EnsureRigidbody();
            if (_rigidbody == null)
                return;

            float clampedSeverity = Mathf.Clamp01(severity);
            _emergencyActive = true;
            _remainingLifetime = Mathf.Max(0.05f, bailoutLifetime);

            if (_pickupItem != null)
                _pickupItem.enabled = false;

            if (_interactionHighlighter != null)
                _interactionHighlighter.enabled = false;

            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            _rigidbody.linearDamping = activeLinearDamping;
            _rigidbody.angularDamping = activeAngularDamping;
            _rigidbody.WakeUp();

            Vector3 launchVelocity = inheritedVelocity * Mathf.Lerp(0.94f, 1.08f, clampedSeverity) +
                                     bailoutImpulse * Mathf.Lerp(0.12f, 0.28f, clampedSeverity);
            launchVelocity.y -= Mathf.Lerp(0.05f, 0.45f, clampedSeverity);
            _rigidbody.linearVelocity = launchVelocity;

            Vector3 spinAxis = bailoutImpulse.sqrMagnitude > 0.0001f
                ? Vector3.Cross(Vector3.up, bailoutImpulse.normalized)
                : transform.right;
            if (spinAxis.sqrMagnitude <= 0.0001f)
                spinAxis = transform.forward;

            float spinSign = Mathf.Sign(Vector3.Dot(bailoutImpulse, transform.right));
            if (spinSign == 0f)
                spinSign = 1f;

            _rigidbody.angularVelocity = spinAxis.normalized *
                                         (spinSign * Mathf.Lerp(spinVelocityMin, spinVelocityMax, clampedSeverity));

            TryRegisterFixedTick();
        }

        /// <inheritdoc />
        public void OnSpawn()
        {
            CachePassiveReferences();
            ResetToIdlePickupState();
        }

        /// <inheritdoc />
        public void OnDespawn()
        {
            ResetToIdlePickupState();
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_emergencyActive)
                return;

            if (_collisionDamageCooldownTimer > 0f)
            {
                _collisionDamageCooldownTimer -= Mathf.Max(0f, fixedDeltaTime);
                if (_collisionDamageCooldownTimer < 0f)
                    _collisionDamageCooldownTimer = 0f;
            }

            _remainingLifetime -= Mathf.Max(0f, fixedDeltaTime);
            if (_remainingLifetime <= 0f)
            {
                _remainingLifetime = 0f;
                DespawnSelf();
                return;
            }

            if (_rigidbody == null)
                return;

            PhysicsForceRouter.QueueForce(
                _rigidbody,
                Vector3.down * sinkVelocityChangePerSecond * fixedDeltaTime,
                ForceMode.VelocityChange);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_emergencyActive || _collisionDamageCooldownTimer > 0f || collision == null)
                return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed <= collisionDamageStartSpeed)
                return;

            Collider hitCollider = collision.collider;
            if (hitCollider == null)
                return;

            FaunaBrain faunaBrain = hitCollider.GetComponent<FaunaBrain>();
            if (faunaBrain == null)
                faunaBrain = hitCollider.GetComponentInParent<FaunaBrain>();

            if (faunaBrain == null)
                return;

            float maxSpeed = Mathf.Max(collisionDamageStartSpeed + 0.01f, collisionDamageMaxSpeed);
            float damageT = Mathf.InverseLerp(collisionDamageStartSpeed, maxSpeed, impactSpeed);
            float damage = Mathf.Lerp(0f, collisionDamageAtMaxSpeed, damageT);
            if (damage <= 0f)
                return;

            faunaBrain.TakeDamage(damage);
            _collisionDamageCooldownTimer = collisionDamageCooldown;
        }

        private void CachePassiveReferences()
        {
            if (_pickupItem == null)
                TryGetComponent(out _pickupItem);

            if (_interactionHighlighter == null)
                TryGetComponent(out _interactionHighlighter);

            if (_rigidbody == null)
                TryGetComponent(out _rigidbody);
        }

        private void EnsureRigidbody()
        {
            if (_rigidbody != null)
                return;

            _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void ResetToIdlePickupState()
        {
            _emergencyActive = false;
            _remainingLifetime = 0f;
            _collisionDamageCooldownTimer = 0f;
            TryUnregisterFixedTick();

            if (_pickupItem != null)
                _pickupItem.enabled = true;

            if (_interactionHighlighter != null)
                _interactionHighlighter.enabled = true;

            if (_rigidbody == null)
                return;

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.linearDamping = idleLinearDamping;
            _rigidbody.angularDamping = idleAngularDamping;
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }

        private void DespawnSelf()
        {
            ObjectPoolManager poolManager = ObjectPoolManager.Instance;
            if (poolManager != null)
            {
                poolManager.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredFixedTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((IFixedTickable)this);
            _registeredFixedTick = true;
        }

        private void TryUnregisterFixedTick()
        {
            if (!_registeredFixedTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((IFixedTickable)this);

            _registeredFixedTick = false;
        }
    }
}
