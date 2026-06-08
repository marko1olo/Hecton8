using UnityEngine;
using UnityEngine.Serialization;
using Unity.Mathematics;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// UI particle feedback cue backed only by ObjectPoolManager.
    /// No runtime GameObject creation, component creation, or material cloning.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Particle Effect")]
    public sealed class UIParticleEffect : MonoBehaviour, IPoolable, IGlobalRegistryHotSwapListener
    {
        private const float CompactParticleDensity = 0.35f;
        private const int PrefabControllerScanStackCapacity = 64;
        // COLD ALLOC: ParticleSystem.Burst[0] - clears authored emission bursts for manual pooled UI emission - owner: UIParticleEffect
        private static readonly ParticleSystem.Burst[] s_noAuthoredBursts = System.Array.Empty<ParticleSystem.Burst>();
        // COLD ALLOC: Transform[64] - fixed prefab-subtree validation stack for recursive effect graph rejection - owner: UIParticleEffect
        private static readonly Transform[] s_prefabControllerScanStack = new Transform[PrefabControllerScanStackCapacity];

        // ----------------------------------------------------------
        // INSPECTOR
        // ----------------------------------------------------------

        [Header("=== PARTICLE PREFAB ===")]
        [SerializeField] private ObjectPoolManager _uiEffectPool;
        [FormerlySerializedAs("particlePrefab")]
        [SerializeField] private GameObject _authoredParticlePrefab;

        [Header("=== CANVAS ATLAS ===")]
        [SerializeField] private Material _sharedCanvasAtlasMaterial;

        [Header("=== SETTINGS ===")]
        [SerializeField] private int particleCount = 10;
        [SerializeField] private float particleLifetime = 0.5f;
        [SerializeField] private float particleSpeed = 100f;
        [SerializeField] private Color particleColor = Color.white;

        // ----------------------------------------------------------
        // FIELDS
        // ----------------------------------------------------------

        private ParticleSystem _particleSystem;
        private ParticleSystemRenderer _particleRenderer;
        private GameObject _particleInstance;
        private Transform _particleInstanceTransform;
        private IObjectPoolService _cachedObjectPool;
        private IObjectPoolService _particlePoolOwner;
        private int _resolvedParticleCount;
        private bool _hotSwapListenerRegistered;

        // ----------------------------------------------------------
        // LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            SpawnEffectInstance();
        }

        private void OnDisable()
        {
            DespawnEffectInstance();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            DespawnEffectInstance();
            TryUnregisterHotSwapListener();
        }

        // ----------------------------------------------------------
        // IPOOLABLE
        // ----------------------------------------------------------

        public void OnSpawn()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            SpawnEffectInstance();
        }

        public void OnDespawn()
        {
            DespawnEffectInstance();
            TryUnregisterHotSwapListener();
        }

        // ----------------------------------------------------------
        // PUBLIC API
        // ----------------------------------------------------------

        /// <summary>
        /// Play particle effect at position.
        /// </summary>
        public void Play(Vector3 worldPosition)
        {
            if (_particleSystem == null || _particleInstance == null || _particleInstanceTransform == null)
                return;

            int emitCount = ResolveQualityScaledParticleCount();
            if (emitCount <= 0)
                return;

            if (emitCount != _resolvedParticleCount)
            {
                _resolvedParticleCount = emitCount;
                ParticleSystem.MainModule main = _particleSystem.main;
                main.maxParticles = emitCount;
            }

            _particleInstanceTransform.position = worldPosition;
            _particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particleSystem.Play(false);
            _particleSystem.Emit(emitCount);
        }

        /// <summary>
        /// Play particle effect at UI element position.
        /// </summary>
        public void PlayAtUIElement(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            Play(rectTransform.position);
        }

        // ----------------------------------------------------------
        // PRIVATE
        // ----------------------------------------------------------

        private bool SpawnEffectInstance()
        {
            if (_particleInstance != null && _particleSystem != null)
                return true;

            if (_particleInstance != null)
                DespawnEffectInstance();

            if (_authoredParticlePrefab == null)
                return false;

            if (AuthoredPrefabContainsEffectController())
                return false;

            IObjectPoolService pool = ResolvePool();
            if (pool == null)
                return false;

            if (!pool.HasPool(_authoredParticlePrefab) || pool.GetAvailableCount(_authoredParticlePrefab) <= 0)
                return false;

            GameObject instance = pool.Spawn(_authoredParticlePrefab, transform.position, transform.rotation, false);
            if (instance == null)
                return false;

            if (!CanDespawnWithPool(pool, instance))
            {
                pool.Despawn(instance);
                return false;
            }

            _particleInstance = instance;
            _particleInstanceTransform = instance.transform;
            _particlePoolOwner = pool;
            CacheParticleComponents(instance, pool);

            if (PrefabContainsNestedEffectController(instance, pool))
            {
                DespawnEffectInstance();
                return false;
            }

            if (_particleSystem == null)
            {
                DespawnEffectInstance();
                return false;
            }

            ApplyParticleSettings();
            if (!EnforceSharedCanvasAtlas())
            {
                DespawnEffectInstance();
                return false;
            }

            return true;
        }

        private void DespawnEffectInstance()
        {
            GameObject instance = _particleInstance;
            IObjectPoolService pool = _particlePoolOwner;

            if (_particleSystem != null)
                _particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            _particleInstance = null;
            _particleInstanceTransform = null;
            _particlePoolOwner = null;
            _particleSystem = null;
            _particleRenderer = null;

            if (instance == null)
                return;

            if (ObjectPoolManager.TryResolvePoolForInstance(instance, pool, out IObjectPoolService ownerPool))
            {
                ownerPool.Despawn(instance);
                return;
            }

            Destroy(instance);
        }

        private void CacheParticleComponents(GameObject instance, IObjectPoolService pool)
        {
            _particleSystem = null;
            _particleRenderer = null;

            pool.TryGetPooledComponent(instance, out _particleSystem);
            pool.TryGetPooledComponent(instance, out _particleRenderer);
        }

        private bool PrefabContainsNestedEffectController(GameObject instance, IObjectPoolService pool)
        {
            if (pool.TryGetPooledComponent(instance, out UIParticleEffect nestedEffect))
                return nestedEffect != null && !ReferenceEquals(nestedEffect, this);

            return false;
        }

        private bool AuthoredPrefabContainsEffectController()
        {
            if (_authoredParticlePrefab == null)
                return false;

            Transform root = _authoredParticlePrefab.transform;
            int stackCount = 1;
            s_prefabControllerScanStack[0] = root;

            while (stackCount > 0)
            {
                Transform current = s_prefabControllerScanStack[--stackCount];
                if (current == null)
                    continue;

                if (current.TryGetComponent(out UIParticleEffect _))
                {
                    ClearPrefabControllerScanStack();
                    return true;
                }

                int childCount = current.childCount;
                if (stackCount + childCount > PrefabControllerScanStackCapacity)
                {
                    ClearPrefabControllerScanStack();
                    return true;
                }

                for (int i = 0; i < childCount; i++)
                    s_prefabControllerScanStack[stackCount++] = current.GetChild(i);
            }

            ClearPrefabControllerScanStack();
            return false;
        }

        private static void ClearPrefabControllerScanStack()
        {
            for (int i = 0; i < PrefabControllerScanStackCapacity; i++)
                s_prefabControllerScanStack[i] = null;
        }

        private void ApplyParticleSettings()
        {
            _resolvedParticleCount = ResolveQualityScaledParticleCount();
            ParticleSystem.MainModule mainModule = _particleSystem.main;
            mainModule.startLifetime = particleLifetime;
            mainModule.startSpeed = particleSpeed;
            mainModule.startColor = particleColor;
            mainModule.maxParticles = _resolvedParticleCount;
            mainModule.loop = false;
            mainModule.playOnAwake = false;

            ParticleSystem.EmissionModule emissionModule = _particleSystem.emission;
            emissionModule.rateOverTime = 0f;
            emissionModule.SetBursts(s_noAuthoredBursts, 0);
        }

        private static float SmoothQuality01(float quality)
        {
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return q * q * (3f - 2f * q);
        }

        private int ResolveQualityScaledParticleCount()
        {
            int authoredCount = Mathf.Max(0, particleCount);
            if (authoredCount == 0)
                return 0;

            float quality01 = SmoothQuality01(HomeostasisBrain.GlobalQualityWeight);
            float scaledCount = authoredCount * math.lerp(CompactParticleDensity, 1f, quality01);
            return math.clamp((int)math.round(scaledCount), 1, authoredCount);
        }

        private bool EnforceSharedCanvasAtlas()
        {
            if (_sharedCanvasAtlasMaterial == null)
                return false;

            if (_particleRenderer == null)
                return false;

            _particleRenderer.sharedMaterial = _sharedCanvasAtlasMaterial;
            return _particleRenderer.sharedMaterial == _sharedCanvasAtlasMaterial;
        }

        private IObjectPoolService ResolvePool()
        {
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_uiEffectPool))
                return _uiEffectPool;

            return TryResolveCachedObjectPool(out IObjectPoolService pool) ? pool : null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.ObjectPool)
                return;

            if (ReferenceEquals(previousService, _particlePoolOwner))
                DespawnEffectInstance();

            CacheObjectPoolService(currentService as ObjectPoolManager);
            if (isActiveAndEnabled)
                SpawnEffectInstance();
        }

        private void CacheRegistryServicesCold()
        {
            CacheObjectPoolService(null);
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _cachedObjectPool = pool;
                return;
            }

            _cachedObjectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _cachedObjectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _cachedObjectPool = resolved;
                pool = resolved;
                return true;
            }

            _cachedObjectPool = null;
            pool = null;
            return false;
        }

        private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)
        {
            return ObjectPoolManager.CanDespawnWithPool(pool, instance);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }
    }
}
