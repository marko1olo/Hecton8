using Hecton8.Audio;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Physics projectile fired by hostile flora.
    /// Implements ITickable for centralized tick dispatch (no native Update).
    /// Implements IPoolable for ObjectPoolManager lifecycle compliance.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class FloraProjectile : MonoBehaviour, ITickable, IPoolable
    {
        private const string PlayerTag = "Player";

        [Header("Damage")]
        [Tooltip("Damage dealt to the player on hit.")]
        [SerializeField, Range(1f, 50f)] private float damage = 10f;

        [Header("Lifetime")]
        [Tooltip("Maximum lifetime in seconds before auto destroy.")]
        [SerializeField, Range(1f, 30f)] private float maxLifetime = 5f;

        [Header("VFX / Audio")]
        [Tooltip("Particle system played on non-player impact.")]
        [SerializeField] private ParticleSystem impactParticles;

        [Tooltip("Sound played on non-player impact.")]
        [SerializeField] private AudioClip impactSound;

        [Tooltip("Sound played on player hit.")]
        [SerializeField] private AudioClip hitPlayerSound;

        [Tooltip("Volume for impact sounds.")]
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.5f;

        [Header("Events")]
        [Tooltip("Invoked when the projectile hits the player. Passes damage.")]
        [SerializeField] private UnityEvent<float> OnHitPlayer;

        [Tooltip("Invoked when the projectile hits anything except the player.")]
        [SerializeField] private UnityEvent OnHitEnvironment;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Transform _cachedTransform;
        private Rigidbody _rigidbody;
        private Collider _collider;
        private float _lifetimeTimer;
        private bool _initialized;
        private Vector3 _initialVelocity;
        private bool _registered;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Damage dealt on player hit.</summary>
        public float Damage => damage;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _collider);

            if (_collider != null)
                _collider.isTrigger = false;
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — replaces native Update()
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called every frame via GameTickManager. Uses dt parameter, not Time.deltaTime.
        /// </summary>
        /// <param name="dt">Frame delta time from GameTickManager.</param>
        public void Tick(float dt)
        {
            _lifetimeTimer += dt;
            if (_lifetimeTimer >= maxLifetime)
                DespawnSelf();
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — ObjectPoolManager lifecycle
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called when spawned from pool. Resets ALL state for reuse.
        /// </summary>
        public void OnSpawn()
        {
            _lifetimeTimer = 0f;
            _initialized = false;
            _initialVelocity = Vector3.zero;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        /// <summary>
        /// Called when returned to pool. Unregisters from tick and clears state.
        /// </summary>
        public void OnDespawn()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            _lifetimeTimer = 0f;
            _initialized = false;
            _initialVelocity = Vector3.zero;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Sets the initial projectile velocity after spawn.
        /// </summary>
        public void Initialize(Vector3 velocity)
        {
            _initialVelocity = velocity;
            _initialized = true;

            if (_rigidbody != null)
                _rigidbody.linearVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 hitPoint = collision.contacts.Length > 0
                ? collision.contacts[0].point
                : _cachedTransform.position;

            if (collision.gameObject.CompareTag(PlayerTag))
            {
                HitPlayer(
                    hitPoint,
                    collision.gameObject.GetComponentInParent<PlayerActionController>());
            }
            else
            {
                HitEnvironment(hitPoint);
            }
        }

        private void HitPlayer(Vector3 hitPoint, PlayerActionController actionController)
        {
            if (hitPlayerSound != null && SpatialAudioManager.TryGetInstance(out var audio))
                audio.PlayAtPoint(hitPlayerSound, hitPoint, impactVolume);

            OnHitPlayer?.Invoke(damage);

            if (actionController != null)
                actionController.OnDamageTaken();

            DespawnSelf();
        }

        private void HitEnvironment(Vector3 hitPoint)
        {
            if (impactParticles != null)
            {
                impactParticles.transform.position = hitPoint;
                impactParticles.Play();
            }

            if (impactSound != null && SpatialAudioManager.TryGetInstance(out var audio))
                audio.PlayAtPoint(impactSound, hitPoint, impactVolume);

            OnHitEnvironment?.Invoke();
            DespawnSelf();
        }

        private void DespawnSelf()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Sets projectile damage at runtime.
        /// </summary>
        public void SetDamage(float amount)
        {
            damage = Mathf.Max(0f, amount);
        }

        /// <summary>
        /// Resets the projectile for pooling reuse.
        /// Delegates to OnSpawn for single-owner reset logic.
        /// </summary>
        public void ResetProjectile()
        {
            OnSpawn();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_initialized)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, _initialVelocity.normalized * 2f);
        }
#endif
    }
}
