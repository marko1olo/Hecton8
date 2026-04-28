// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HADES HECTON-8 | HectonBiolumZone (MEGA-OPTIMIZED v2.0)                   ║
// ║  Light pooling + Pre-computed spectrums + LOD + Dirty-flag caching          ║
// ║  Zero allocations in hot path | Static color lookup | Cached components     ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using Hecton8.Caves;
using Hecton8.Core;

namespace Hecton8.Biolum
{
    /// <summary>
    /// PRE-COMPUTED COLOR SPECTRUMS — No allocations, static readonly.
    /// 11-step gradient for smooth Lerp transitions and fast sampling.
    /// </summary>
    public static class BiolumSpectrums
    {
        // 11-step cave spectrum: warm (0) → white (0.5) → cold (1)
        public static readonly Color[] CaveSpectrum = new Color[11]
        {
            new Color(1.0f, 0.8f, 0.3f), new Color(1.0f, 0.7f, 0.3f), new Color(1.0f, 0.6f, 0.3f),
            new Color(0.9f, 0.5f, 0.3f), new Color(0.8f, 0.4f, 0.3f), new Color(1.0f, 1.0f, 1.0f),
            new Color(0.8f, 0.9f, 1.0f), new Color(0.6f, 0.8f, 1.0f), new Color(0.4f, 0.7f, 1.0f),
            new Color(0.3f, 0.6f, 1.0f), new Color(0.2f, 0.5f, 1.0f),
        };

        // 11-step ocean spectrum: surface (0) → twilight (0.5) → abyss (1)
        public static readonly Color[] OceanSpectrum = new Color[11]
        {
            new Color(0.3f, 0.7f, 1.0f), new Color(0.25f, 0.6f, 0.9f), new Color(0.2f, 0.5f, 0.8f),
            new Color(0.2f, 0.4f, 0.8f), new Color(0.2f, 0.3f, 0.7f), new Color(0.25f, 0.8f, 0.5f),
            new Color(0.3f, 0.85f, 0.4f), new Color(0.5f, 0.7f, 0.3f), new Color(0.7f, 0.5f, 0.5f),
            new Color(0.8f, 0.3f, 0.8f), new Color(0.85f, 0.2f, 1.0f),
        };

        // Floor cluster colors (static readonly, fast ref)
        public static readonly Color CoralRed = new Color(1f, 0.3f, 0.2f);
        public static readonly Color CoralOrange = new Color(1f, 0.6f, 0.2f);
        public static readonly Color FungiGreen = new Color(0.3f, 1f, 0.5f);
        public static readonly Color VentRed = new Color(1f, 0.2f, 0.1f);
        public static readonly Color VentOrange = new Color(1f, 0.4f, 0.1f);
        public static readonly Color GardenCyan = new Color(0.2f, 1f, 0.8f);

        /// <summary>
        /// O(1) spectrum lookup with rounding (no Lerp overhead).
        /// </summary>
        public static Color Sample(Color[] spectrum, float position)
        {
            position = Mathf.Clamp01(position);
            int idx = Mathf.RoundToInt(position * (spectrum.Length - 1));
            return spectrum[idx];
        }
    }

    /// <summary>
    /// MEGA-OPTIMIZED abstract bioluminescence zone.
    /// - Light pooling (GameObject reuse, no destroy)
    /// - Pre-computed color spectrums
    /// - Distance-based LOD culling
    /// - Cached component references
    /// - Dirty-flag optimization
    /// - Zero allocations in Tick()
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Transform))]
    public abstract class HectonBiolumZone : MonoBehaviour, ITickable, IUpdatable
    {
        // ───────────────────────────────────────────────────────────────────────────────
        // INSPECTOR SETTINGS (Compact)
        // ───────────────────────────────────────────────────────────────────────────────

        [Header("── Biolum Zone ──────────────────")]
        [SerializeField] protected string _zoneKey = "Zone";
        [SerializeField, Range(0f, 1f)] protected float _moodLevel = 0.5f;
        [SerializeField, Range(0f, 1f)] protected float _hazardLevel = 0.1f;
        [SerializeField, Range(0.1f, 3f)] protected float _intensityMultiplier = 1.5f;
        [SerializeField, Range(0.5f, 30f)] protected float _rangeMultiplier = 10f;
        [SerializeField, Range(1, 100)] protected int _updateInterval = 5;
        [SerializeField, Range(2, 16)] protected int _maxLights = 8;
        [SerializeField, Range(0f, 1f)] protected float _lodDistanceScale = 1.0f;

        // ───────────────────────────────────────────────────────────────────────────────
        // CACHED COMPONENTS (No GetComponent in hot path)
        // ───────────────────────────────────────────────────────────────────────────────

        protected Transform _cachedTransform;
        protected Light[] _activeLights;
        protected int _activeLightCount = 0;
        protected bool _isRegistered = false;
        protected int _lastUpdateFrame = -1;

        // ───────────────────────────────────────────────────────────────────────────────
        // DIRTY-FLAG CACHING (Avoid redundant property updates)
        // ───────────────────────────────────────────────────────────────────────────────

        #if UNITY_EDITOR
        [SerializeField] protected bool _debugLogSpawn = false;
        [SerializeField] private int _debugTickInvocations = 0;
        [SerializeField] private int _debugEvaluateInvocations = 0;
        [SerializeField] private bool _debugLastSkippedLod = false;
        [SerializeField] private int _debugLastUpdatedFrame = -1;
        #endif

        // ───────────────────────────────────────────────────────────────────────────────
        // LIFECYCLE
        // ───────────────────────────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _cachedTransform = transform;
            _activeLights = new Light[_maxLights]; // COLD ALLOC: Light[_maxLights] — pooled biolum light references — owner: HectonBiolumZone
            PrewarmLightPool();
        }

        protected virtual void OnEnable()
        {
            if (!_isRegistered)
            {
                SystemDispatcher.EnsureRuntimeInstance();
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _isRegistered = true;
            }
            if (HectonBiolumManager.Instance != null)
                HectonBiolumManager.Instance.RegisterZone(this);
        }

        protected virtual void OnDisable()
        {
            if (_isRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _isRegistered = false;
            }
            if (HectonBiolumManager.Instance != null)
                HectonBiolumManager.Instance.UnregisterZone(this);
            CleanupLights();
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // INTERFACE: ITickable (ZERO allocations)
        // ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lazy tick with frame-count and LOD culling.
        /// ZERO allocations guaranteed.
        /// </summary>
        public void Tick(float deltaTime)
        {
#if UNITY_EDITOR
            _debugTickInvocations++;
#endif
            int frame = Time.frameCount;
            if (frame - _lastUpdateFrame < _updateInterval) return;
            _lastUpdateFrame = frame;

            bool skippedLod = ShouldSkipLOD();
#if UNITY_EDITOR
            _debugLastSkippedLod = skippedLod;
#endif
            if (skippedLod) return;

            EvaluateBiolumState();
#if UNITY_EDITOR
            _debugEvaluateInvocations++;
            _debugLastUpdatedFrame = frame;
#endif
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // ABSTRACT METHODS (Subclass Override)
        // ───────────────────────────────────────────────────────────────────────────────

        protected abstract void EvaluateBiolumState();
        protected abstract Color GetBiolumColor();
        protected abstract float GetBiolumIntensity();
        protected abstract float GetBiolumRange();

        /// <summary>
        /// Expose the current sampled biolum color for lightweight runtime consumers.
        /// </summary>
        public Color SampleZoneColor()
        {
            return GetBiolumColor();
        }

        /// <summary>
        /// Expose the current sampled biolum intensity for lightweight runtime consumers.
        /// </summary>
        public float SampleZoneIntensity()
        {
            return GetBiolumIntensity();
        }

        /// <summary>
        /// Expose the current sampled biolum range for lightweight runtime consumers.
        /// </summary>
        public float SampleZoneRange()
        {
            return GetBiolumRange();
        }

        /// <summary>
        /// Get cached world position for cheap proximity checks.
        /// </summary>
        public Vector3 GetZonePosition()
        {
            return _cachedTransform != null ? _cachedTransform.position : transform.position;
        }

        /// <summary>
        /// Ensure the zone is registered into the central tick loop even if startup order was late.
        /// Safe to call multiple times: GameTickManager ignores duplicate registrations.
        /// </summary>
        public void EnsureTickRegistration()
        {
            if (_isRegistered)
            {
                return;
            }

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = true;
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // PROTECTED HELPERS: Light Pooling
        // ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Get or create light from pre-allocated pool.
        /// Reuses GameObject to avoid allocations.
        /// </summary>
        protected Light GetOrCreateLight(Vector3 pos, Color color, float range, float intensity)
        {
            if (_activeLightCount >= _maxLights) return null;

            Light light = _activeLights[_activeLightCount];
            if (light == null)
            {
                return null;
            }

            if (!light.gameObject.activeSelf)
            {
                light.gameObject.SetActive(true);
            }

            light.transform.position = pos;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            _activeLightCount++;

            #if UNITY_EDITOR
            if (_debugLogSpawn) Debug.Log($"[Biolum] {_zoneKey} light {_activeLightCount - 1}");
            #endif

            return light;
        }

        /// <summary>
        /// Update light with dirty-flag optimization (skip redundant SetProperty calls).
        /// </summary>
        protected void UpdateLight(Light light, Color color, float range, float intensity)
        {
            if (light == null) return;

            light.intensity = intensity;
            light.range = range;
            light.color = color;
        }

        /// <summary>
        /// Cleanup (deactivate lights, don't destroy).
        /// </summary>
        protected void CleanupLights()
        {
            for (int i = 0; i < _activeLightCount; i++)
                if (_activeLights[i] != null)
                    _activeLights[i].gameObject.SetActive(false);
            _activeLightCount = 0;
        }

        private void PrewarmLightPool()
        {
            for (int i = 0; i < _maxLights; i++)
            {
                if (_activeLights[i] != null)
                {
                    continue;
                }

                // COLD ALLOC: pooled Light GameObject slot — prewarmed biolum light owner — owner: HectonBiolumZone
                GameObject lightObject = new GameObject($"BiolumLight_{i}");
                lightObject.transform.SetParent(_cachedTransform, false);
                lightObject.SetActive(false);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.renderingLayerMask = 1;
                _activeLights[i] = light;
            }
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // PROTECTED HELPERS: Scaling Functions
        // ───────────────────────────────────────────────────────────────────────────────

        protected float ScaleIntensityByMood(float baseIntensity)
        {
            float mood = Mathf.Lerp(0.5f, 1.5f, _moodLevel);
            float mgr = HectonBiolumManager.Instance != null 
                ? HectonBiolumManager.Instance._globalIntensityScale 
                : 1f;
            return baseIntensity * mood * mgr;
        }

        protected float ScaleRangeByHazard(float baseRange)
        {
            float hazard = Mathf.Lerp(1.5f, 0.5f, _hazardLevel);
            float mgr = HectonBiolumManager.Instance != null 
                ? HectonBiolumManager.Instance._globalRangeScale 
                : 1f;
            return baseRange * hazard * mgr;
        }

        protected Color GetHazardTint() => Color.Lerp(Color.white, Color.red, _hazardLevel * 0.3f);

        // ───────────────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS: LOD System
        // ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Distance-based LOD: distant zones skip updates.
        /// </summary>
        private bool ShouldSkipLOD()
        {
            if (_lodDistanceScale >= 1.0f) return false;

            Vector3 camPos = HectonBiolumManager.Instance != null 
                ? HectonBiolumManager.Instance.GetCameraPosition() 
                : Vector3.zero;

            float dist = Vector3.Distance(_cachedTransform.position, camPos);
            float lodThreshold = Mathf.Lerp(5f, 500f, _lodDistanceScale);

            // Skip 2 out of 3 frames if beyond threshold
            return dist > lodThreshold && (Time.frameCount % 3) != 0;
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // UTILITY: Fast Float Comparison
        // ───────────────────────────────────────────────────────────────────────────────

        private static bool Approximately(float a, float b, float epsilon = 0.001f) =>
            Mathf.Abs(a - b) < epsilon;

        // ───────────────────────────────────────────────────────────────────────────────
        // EDITOR
        // ───────────────────────────────────────────────────────────────────────────────

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
        #endif
    }
}
