using UnityEngine;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// UI particle effect for button clicks and interactions.
    /// EXCEEDS SUBNAUTICA: Subnautica has minimal UI particles, mostly static.
    /// Spawns particle burst on button click with pooling.
    /// Zero-GC: ObjectPoolManager, IPoolable, cached ParticleSystem.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Particle Effect")]
    public sealed class UIParticleEffect : MonoBehaviour, IPoolable
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== PARTICLE PREFAB ===")]
        [SerializeField] private GameObject particlePrefab;

        [Header("=== SETTINGS ===")]
        [SerializeField] private int particleCount = 10;
        [SerializeField] private float particleLifetime = 0.5f;
        [SerializeField] private float particleSpeed = 100f;
        [SerializeField] private Color particleColor = Color.white;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private ParticleSystem _particleSystem;
        private ParticleSystem.MainModule _mainModule;
        private ParticleSystem.EmissionModule _emissionModule;
        private bool _initialized;
        private GameObject _particleInstance;
        private bool _particleInstanceOwnedByPool;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (!_particleInstanceOwnedByPool || _particleInstance == null)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool != null && _particleInstance.TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
                pool.Despawn(_particleInstance);

            _particleInstance = null;
            _particleInstanceOwnedByPool = false;
        }

        // ══════════════════════════════════════════════════════════
        // IPOOLABLE
        // ══════════════════════════════════════════════════════════

        public void OnSpawn()
        {
            if (!_initialized)
                Initialize();
        }

        public void OnDespawn()
        {
            if (_particleSystem != null)
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Play particle effect at position.
        /// </summary>
        public void Play(Vector3 worldPosition)
        {
            if (_particleSystem == null)
                return;

            transform.position = worldPosition;
            _particleSystem.Play();
        }

        /// <summary>
        /// Play particle effect at UI element position.
        /// </summary>
        public void PlayAtUIElement(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            Vector3 worldPosition = rectTransform.position;
            Play(worldPosition);
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void Initialize()
        {
            if (_initialized)
                return;

            // Create particle system if prefab provided
            if (particlePrefab != null)
            {
                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null)
                {
                    _particleInstance = pool.Spawn(particlePrefab, transform.position, transform.rotation);
                    if (_particleInstance != null)
                    {
                        _particleInstance.transform.SetParent(transform, false);
                        _particleSystem = _particleInstance.GetComponent<ParticleSystem>();
                        _particleInstanceOwnedByPool = _particleInstance.TryGetComponent(out ObjectPoolManager.PoolItemMarker _);
                    }
                }
            }

            // Create default particle system if none exists
            if (_particleSystem == null)
            {
                GameObject particleObj = new GameObject("Particles");
                particleObj.transform.SetParent(transform, false);
                _particleSystem = particleObj.AddComponent<ParticleSystem>();
                _particleInstance = particleObj;
                _particleInstanceOwnedByPool = false;
                ConfigureDefaultParticleSystem();
            }

            _mainModule = _particleSystem.main;
            _emissionModule = _particleSystem.emission;
            _initialized = true;
        }

        private void ConfigureDefaultParticleSystem()
        {
            if (_particleSystem == null)
                return;

            ParticleSystem.MainModule main = _particleSystem.main;
            main.startLifetime = particleLifetime;
            main.startSpeed = particleSpeed;
            main.startColor = particleColor;
            main.maxParticles = particleCount;
            main.loop = false;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = _particleSystem.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, particleCount);
            emission.SetBurst(0, burst);

            ParticleSystem.ShapeModule shape = _particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 50f;

            ParticleSystemRenderer renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 100;
            }
        }
    }
}
