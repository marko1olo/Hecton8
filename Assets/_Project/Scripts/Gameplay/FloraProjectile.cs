using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Legacy pooled flora projectile facade. Damage is queued into BallisticsRuntime; no physics body or collision callback is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloraProjectile : MonoBehaviour, IPoolable
    {
        private const float MaxProjectileSpeedMetersPerSecond = 64f;
        private const float MaxProjectileSpeedSq = MaxProjectileSpeedMetersPerSecond * MaxProjectileSpeedMetersPerSecond;

        [Header("Damage")]
        [Tooltip("Damage scalar preserved for legacy prefabs; mathematical shots use it as mass bias.")]
        [SerializeField, Range(1f, 50f)] private float damage = 10f;

        [Header("Lifetime")]
        [Tooltip("Legacy lifetime field retained for prefab compatibility. Mathematical shots despawn immediately.")]
        [SerializeField, Range(1f, 30f)] private float maxLifetime = 5f;

        [Header("VFX and Audio")]
        [Tooltip("Legacy impact particle reference retained for serialized prefab compatibility.")]
        [SerializeField] private ParticleSystem impactParticles;

        [Tooltip("Legacy impact sound reference retained for serialized prefab compatibility.")]
        [SerializeField] private AudioClip impactSound;

        [Tooltip("Legacy player-hit sound reference retained for serialized prefab compatibility.")]
        [SerializeField] private AudioClip hitPlayerSound;

        [Tooltip("Legacy impact volume retained for serialized prefab compatibility.")]
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.5f;

        private Transform _cachedTransform;
        private Vector3 _initialVelocity;
        private bool _initialized;

        /// <summary>Legacy damage scalar retained for old prefab inspectors.</summary>
        public float Damage => damage;
        public float MaxLifetime => maxLifetime;
        public float ImpactVolume => impactVolume;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        /// <summary>Called by ObjectPoolManager when this legacy facade is reused.</summary>
        public void OnSpawn()
        {
            _initialized = false;
            _initialVelocity = Vector3.zero;
        }

        /// <summary>Called by ObjectPoolManager when returned to the pool.</summary>
        public void OnDespawn()
        {
            _initialized = false;
            _initialVelocity = Vector3.zero;
        }

        /// <summary>Queues a mathematical ballistic trajectory and immediately returns the visual shell.</summary>
        public void Initialize(Vector3 velocity)
        {
            Vector3 safeVelocity = ResolveSafeVelocity(velocity);
            _initialVelocity = safeVelocity;
            _initialized = true;

            if (safeVelocity.sqrMagnitude > 0.000001f)
            {
                float mass = math.max(0.001f, damage * 0.0018f);
                uint source = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(gameObject.GetEntityId()));
                BallisticsRuntime.QueueTrajectoryFromVelocity(
                    _cachedTransform != null ? _cachedTransform.position : transform.position,
                    safeVelocity,
                    mass,
                    BallisticWeaponHashes.FloraSpike,
                    source,
                    BallisticTrajectoryFlags.LegacyFacade | BallisticTrajectoryFlags.HostileFlora);
            }

            DespawnSelf();
        }

        /// <summary>Sets projectile damage at runtime for legacy callers.</summary>
        public void SetDamage(float amount)
        {
            damage = float.IsFinite(amount) ? math.max(0f, amount) : 0f;
        }

        /// <summary>Resets the projectile facade for pooling reuse.</summary>
        public void ResetProjectile()
        {
            OnSpawn();
        }

        private void DespawnSelf()
        {
            IObjectPoolService pool = GlobalRegistry.ObjectPoolService;
            if (pool != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        private static Vector3 ResolveSafeVelocity(Vector3 velocity)
        {
            if (!IsFinite(velocity))
                return Vector3.zero;

            float speedSq = velocity.sqrMagnitude;
            if (!float.IsFinite(speedSq) || speedSq <= 0.000001f)
                return Vector3.zero;

            return speedSq <= MaxProjectileSpeedSq
                ? velocity
                : velocity * (MaxProjectileSpeedMetersPerSecond * math.rsqrt(math.max(speedSq, 0.000001f)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_initialized)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, _initialVelocity.normalized * 2f);
        }
#endif
    }
}
