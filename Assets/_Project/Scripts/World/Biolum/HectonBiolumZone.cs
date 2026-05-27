// â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
// â•‘  HADES HECTON-8 | HectonBiolumZone (MEGA-OPTIMIZED v2.0)                   â•‘
// â•‘  Light pooling + Pre-computed spectrums + LOD + Dirty-flag caching          â•‘
// â•‘  Zero allocations in hot path | Static color lookup | Cached components     â•‘
// â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

using UnityEngine;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Biolum
{
    /// <summary>
    /// PRE-COMPUTED COLOR SPECTRUMS â€” No allocations, static readonly.
    /// 11-step gradient for smooth Lerp transitions and fast sampling.
    /// </summary>
    public static class BiolumSpectrums
    {
        // 11-step cave spectrum: warm (0) â†’ white (0.5) â†’ cold (1)
        public static readonly Color[] CaveSpectrum = new Color[11]
        {
            new Color(1.0f, 0.8f, 0.3f), new Color(1.0f, 0.7f, 0.3f), new Color(1.0f, 0.6f, 0.3f),
            new Color(0.9f, 0.5f, 0.3f), new Color(0.8f, 0.4f, 0.3f), new Color(1.0f, 1.0f, 1.0f),
            new Color(0.8f, 0.9f, 1.0f), new Color(0.6f, 0.8f, 1.0f), new Color(0.4f, 0.7f, 1.0f),
            new Color(0.3f, 0.6f, 1.0f), new Color(0.2f, 0.5f, 1.0f),
        };

        // 11-step ocean spectrum: surface (0) â†’ twilight (0.5) â†’ abyss (1)
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
            if (spectrum == null || spectrum.Length == 0)
                return Color.black;

            position = math.isfinite(position) ? math.saturate(position) : 0.5f;
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
    public abstract class HectonBiolumZone : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxTrackedActiveZones = 512;
        private const float AupRefreshDistanceSqr = 0.0004f;
        private const double BiolumTickTimeModulo = 65536d;
        private const float MaxLegacyLightIntensity = 10f;
        private const float MaxLegacyLightRange = 160f;
        private const int BiolumZoneInvalidInputHash = unchecked((int)0x42494F5Au); // BIOZ
        // COLD ALLOC: fixed active-zone registry replacing scene-wide reflection search fallback - owner: HectonBiolumZone
        private static readonly HectonBiolumZone[] s_ActiveZones = new HectonBiolumZone[MaxTrackedActiveZones];
        private static int s_ActiveZoneCount;
        private static int s_ActiveZoneVersion;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // INSPECTOR SETTINGS (Compact)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Header("â”€â”€ Biolum Zone â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] protected string _zoneKey = "Zone";
        [SerializeField, Range(0f, 1f)] protected float _moodLevel = 0.5f;
        [SerializeField, Range(0f, 1f)] protected float _hazardLevel = 0.1f;
        [SerializeField, Range(0.1f, 3f)] protected float _intensityMultiplier = 1.5f;
        [SerializeField, Range(0.5f, 30f)] protected float _rangeMultiplier = 10f;
        [SerializeField, Range(1, 100)] protected int _updateInterval = 5;
        [SerializeField, Range(2, 16)] protected int _maxLights = 8;
        [SerializeField, Range(0f, 1f)] protected float _lodDistanceScale = 1.0f;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // CACHED COMPONENTS (No GetComponent in hot path)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        protected Transform _cachedTransform;
        protected Light[] _activeLights;
        protected int _activeLightCount = 0;
        protected bool _isRegistered = false;
        private bool _lateFrameRegistered;
        private bool _biolumVisualDirty;
        private bool _hotSwapListenerRegistered;
        protected int _lastUpdateFrame = -1;
        private AbsoluteUniversePosition _cachedZoneAup;
        private Vector3 _cachedZoneRuntimePosition;
        private bool _cachedZoneAupValid;
        private double _biolumFallbackTimeSeconds;
        private float _biolumTickTime;
        private int _lastInvalidZoneInputFrame = -1;
        private HectonBiolumManager _cachedBiolumManager;
        private ITickDispatcher _cachedTickDispatcher;
        private ISimulationBucketer _cachedSimulationBucketer;

        internal static int ActiveZoneCount => math.clamp(s_ActiveZoneCount, 0, MaxTrackedActiveZones);
        internal static int ActiveZoneVersion => s_ActiveZoneVersion;
        protected float BiolumTickTime => _biolumTickTime;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // DIRTY-FLAG CACHING (Avoid redundant property updates)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] protected bool _debugLogSpawn = false;
        [SerializeField] private int _debugTickInvocations = 0;
        [SerializeField] private int _debugEvaluateInvocations = 0;
        [SerializeField] private bool _debugLastSkippedLod = false;
        [SerializeField] private int _debugLastUpdatedFrame = -1;
        #endif

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // LIFECYCLE
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        protected virtual void Awake()
        {
            _maxLights = Mathf.Clamp(_maxLights, 1, 16);
            _updateInterval = Mathf.Max(1, _updateInterval);
            _cachedTransform = transform;
            RefreshCachedAup();
            _activeLights = new Light[_maxLights]; // COLD ALLOC: Light[_maxLights] â€” pooled biolum light references â€” owner: HectonBiolumZone
            PrewarmLightPool();
        }

        protected virtual void OnEnable()
        {
            RegisterActiveZone(this);
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            EnsureTickRegistration();
            HectonBiolumManager manager = _cachedBiolumManager;
            if (manager != null)
                manager.RegisterZone(this);
        }

        protected virtual void OnDisable()
        {
            if (_isRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _isRegistered = false;
            }
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }
            HectonBiolumManager manager = _cachedBiolumManager;
            if (manager != null)
                manager.UnregisterZone(this);
            TryUnregisterHotSwapListener();
            _cachedBiolumManager = null;
            _cachedTickDispatcher = null;
            _cachedSimulationBucketer = null;
            UnregisterActiveZone(this);
            CleanupLights();
        }

        private static void RegisterActiveZone(HectonBiolumZone zone)
        {
            if (zone == null)
                return;

            int count = math.clamp(s_ActiveZoneCount, 0, MaxTrackedActiveZones);
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(s_ActiveZones[i], zone))
                {
                    s_ActiveZoneCount = count;
                    return;
                }
            }

            if (count < MaxTrackedActiveZones)
            {
                s_ActiveZones[count] = zone;
                s_ActiveZoneCount = count + 1;
                s_ActiveZoneVersion++;
                return;
            }

            s_ActiveZoneCount = count;
        }

        private static void UnregisterActiveZone(HectonBiolumZone zone)
        {
            if (zone == null)
                return;

            int count = math.clamp(s_ActiveZoneCount, 0, MaxTrackedActiveZones);
            for (int i = count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_ActiveZones[i], zone))
                {
                    int last = count - 1;
                    for (int move = i; move < last; move++)
                    {
                        s_ActiveZones[move] = s_ActiveZones[move + 1];
                    }

                    s_ActiveZones[last] = null;
                    count = last;
                    s_ActiveZoneVersion++;
                }
            }

            s_ActiveZoneCount = count;
        }

        internal static HectonBiolumZone GetActiveZoneAt(int index)
        {
            return (uint)index < (uint)ActiveZoneCount ? s_ActiveZones[index] : null;
        }

        internal static int CopyActiveZonesTo(HectonBiolumZone[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int count = math.min(math.clamp(s_ActiveZoneCount, 0, MaxTrackedActiveZones), destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = s_ActiveZones[i];
            }

            return count;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // INTERFACE: ITickable (ZERO allocations)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Lazy tick with frame-count and LOD culling.
        /// ZERO allocations guaranteed.
        /// </summary>
        public void Tick(float deltaTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugTickInvocations++;
#endif
            _biolumTickTime = ResolveBiolumTickTime(deltaTime);
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (frame - _lastUpdateFrame < _updateInterval) return;
            _lastUpdateFrame = frame;

            bool skippedLod = ShouldSkipLOD();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugLastSkippedLod = skippedLod;
#endif
            if (skippedLod) return;

            _biolumVisualDirty = true;
        }

        public void LateFrameTick()
        {
            if (!_biolumVisualDirty)
                return;

            _biolumVisualDirty = false;
            EvaluateBiolumState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugEvaluateInvocations++;
            _debugLastUpdatedFrame = _lastUpdateFrame;
#endif
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // ABSTRACT METHODS (Subclass Override)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            Vector3 position = _cachedTransform != null ? _cachedTransform.position : transform.position;
            if (MathGuard.IsFinite(position))
                return position;

            ReportInvalidZoneInput();
            return Vector3.zero;
        }

        /// <summary>
        /// Returns cached AUP for long-range biolum queries. Static zones pay conversion only after movement.
        /// </summary>
        public AbsoluteUniversePosition GetZoneAup()
        {
            Vector3 runtimePosition = GetZonePosition();
            if (!_cachedZoneAupValid ||
                (runtimePosition - _cachedZoneRuntimePosition).sqrMagnitude > AupRefreshDistanceSqr)
            {
                _cachedZoneRuntimePosition = runtimePosition;
                if (TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition zoneAup))
                {
                    _cachedZoneAup = zoneAup;
                    _cachedZoneAupValid = true;
                }
                else
                {
                    _cachedZoneAupValid = false;
                    ReportInvalidZoneInput();
                }
            }

            return _cachedZoneAup;
        }

        /// <summary>
        /// Ensure the zone is registered into the central tick loop even if startup order was late.
        /// Safe to call multiple times: GameTickManager ignores duplicate registrations.
        /// </summary>
        public void EnsureTickRegistration()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (GlobalRegistry.Dispatcher == null)
            {
                return;
            }

            if (!_isRegistered)
                _isRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PROTECTED HELPERS: Light Pooling
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Get or create light from pre-allocated pool.
        /// Reuses GameObject to avoid allocations.
        /// </summary>
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _cachedTickDispatcher = currentService as ITickDispatcher;
                    _isRegistered = false;
                    _lateFrameRegistered = false;
                    if (currentService != null && isActiveAndEnabled)
                        EnsureTickRegistration();
                    break;
                case GlobalRegistryServiceSlot.BiolumManagerRuntime:
                    if (isActiveAndEnabled && previousService is HectonBiolumManager previousManager)
                        previousManager.UnregisterZone(this);
                    _cachedBiolumManager = currentService as HectonBiolumManager;
                    if (isActiveAndEnabled && _cachedBiolumManager != null)
                        _cachedBiolumManager.RegisterZone(this);
                    break;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _cachedSimulationBucketer = currentService as ISimulationBucketer;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedBiolumManager = GlobalRegistry.BiolumManager;
            _cachedTickDispatcher = GlobalRegistry.TickDispatcher;
            _cachedSimulationBucketer = GlobalRegistry.SimulationBucketer;
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

            light.transform.position = SanitizeLightPosition(pos);
            light.color = SanitizeLightColor(color);
            light.range = SanitizeLightRange(range);
            light.intensity = SanitizeLightIntensity(intensity);
            _activeLightCount++;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogSpawn) H8Debug.Log("[Biolum] light spawned");
            #endif

            return light;
        }

        /// <summary>
        /// Update light with dirty-flag optimization (skip redundant SetProperty calls).
        /// </summary>
        protected void UpdateLight(Light light, Color color, float range, float intensity)
        {
            if (light == null) return;

            light.intensity = SanitizeLightIntensity(intensity);
            light.range = SanitizeLightRange(range);
            light.color = SanitizeLightColor(color);
        }

        protected void UpdateLightPosition(Light light, Vector3 position)
        {
            if (light == null) return;

            light.transform.position = SanitizeLightPosition(position);
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

                // COLD ALLOC: pooled Light GameObject slot â€” prewarmed biolum light owner â€” owner: HectonBiolumZone
                GameObject lightObject = new GameObject("BiolumLight");
                lightObject.transform.SetParent(_cachedTransform, false);
                lightObject.SetActive(false);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.renderingLayerMask = 1;
                _activeLights[i] = light;
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PROTECTED HELPERS: Scaling Functions
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        protected float ScaleIntensityByMood(float baseIntensity)
        {
            float mood = 0.5f + Sanitize01(_moodLevel, 0.5f);
            HectonBiolumManager manager = _cachedBiolumManager;
            float mgr = manager != null
                ? SanitizeNonNegative(manager._globalIntensityScale, 1f)
                : 1f;
            return math.min(SanitizeNonNegative(baseIntensity, 0f) * mood * mgr, MaxLegacyLightIntensity);
        }

        protected float ScaleRangeByHazard(float baseRange)
        {
            float hazard = 1.5f - Sanitize01(_hazardLevel, 0.1f);
            HectonBiolumManager manager = _cachedBiolumManager;
            float mgr = manager != null
                ? SanitizeNonNegative(manager._globalRangeScale, 1f)
                : 1f;
            return math.min(SanitizeNonNegative(baseRange, 0f) * hazard * mgr, MaxLegacyLightRange);
        }

        protected Color GetHazardTint() => Color.Lerp(Color.white, Color.red, Sanitize01(_hazardLevel, 0.1f) * 0.3f);

        protected float Sanitize01(float value, float fallback)
        {
            if (math.isfinite(value))
                return math.saturate(value);

            ReportInvalidZoneInput();
            return math.saturate(fallback);
        }

        protected float SanitizeNonNegative(float value, float fallback)
        {
            if (math.isfinite(value))
                return math.max(0f, value);

            ReportInvalidZoneInput();
            return math.max(0f, fallback);
        }

        protected Color SanitizeBiolumColor(Color color)
        {
            bool valid =
                math.isfinite(color.r) &&
                math.isfinite(color.g) &&
                math.isfinite(color.b) &&
                math.isfinite(color.a);
            if (valid)
            {
                return new Color(
                    math.clamp(color.r, 0f, MaxLegacyLightIntensity),
                    math.clamp(color.g, 0f, MaxLegacyLightIntensity),
                    math.clamp(color.b, 0f, MaxLegacyLightIntensity),
                    math.saturate(color.a));
            }

            ReportInvalidZoneInput();
            return Color.black;
        }

        private Vector3 SanitizeLightPosition(Vector3 position)
        {
            if (MathGuard.IsFinite(position))
                return position;

            ReportInvalidZoneInput();
            return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
        }

        private Color SanitizeLightColor(Color color)
        {
            return SanitizeBiolumColor(color);
        }

        private float SanitizeLightRange(float range)
        {
            return math.clamp(SanitizeNonNegative(range, 0f), 0f, MaxLegacyLightRange);
        }

        private float SanitizeLightIntensity(float intensity)
        {
            return math.clamp(SanitizeNonNegative(intensity, 0f), 0f, MaxLegacyLightIntensity);
        }

        private void ReportInvalidZoneInput()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastInvalidZoneInputFrame == frame)
                return;

            _lastInvalidZoneInputFrame = frame;
            GlobalTelemetryBus.PublishMathGuardInvalidNumber(BiolumZoneInvalidInputHash);
        }

        private float ResolveBiolumTickTime(float deltaTime)
        {
            ITickDispatcher dispatcher = _cachedTickDispatcher;
            if (dispatcher != null)
            {
                H8TimeSnapshot snapshot = dispatcher.TimeSnapshot;
                if (snapshot.Time >= 0d && !double.IsNaN(snapshot.Time) && !double.IsInfinity(snapshot.Time))
                {
                    _biolumFallbackTimeSeconds = snapshot.Time;
                    return (float)(_biolumFallbackTimeSeconds % BiolumTickTimeModulo);
                }
            }

            float safeDeltaTime = (!float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime) && deltaTime > 0f)
                ? Mathf.Min(deltaTime, 0.25f)
                : 0f;
            _biolumFallbackTimeSeconds += safeDeltaTime;
            if (_biolumFallbackTimeSeconds < 0d ||
                double.IsNaN(_biolumFallbackTimeSeconds) ||
                double.IsInfinity(_biolumFallbackTimeSeconds))
            {
                _biolumFallbackTimeSeconds = 0d;
            }

            return (float)(_biolumFallbackTimeSeconds % BiolumTickTimeModulo);
        }

        private void RefreshCachedAup()
        {
            _cachedZoneRuntimePosition = GetZonePosition();
            _cachedZoneAupValid = TryResolveAupFromRuntimeOrigin(_cachedZoneRuntimePosition, out _cachedZoneAup);
            if (!_cachedZoneAupValid)
                ReportInvalidZoneInput();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PRIVATE HELPERS: LOD System
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Distance-based LOD: distant zones skip updates.
        /// </summary>
        private bool ShouldSkipLOD()
        {
            if (_lodDistanceScale >= 1.0f) return false;

            HectonBiolumManager manager = _cachedBiolumManager;
            AbsoluteUniversePosition cameraAup = manager != null
                ? manager.GetCameraAup()
                : RuntimeOriginRoute.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition zoneAup = GetZoneAup();
            if (!AbsoluteUniversePosition.IsFinite(in cameraAup) || !AbsoluteUniversePosition.IsFinite(in zoneAup))
                return false;

            float lodThreshold = 5f + (500f - 5f) * Mathf.Clamp01(_lodDistanceScale);
            double lodThresholdSq = (double)lodThreshold * lodThreshold;

            ISimulationBucketer bucketer = _cachedSimulationBucketer;
            int activeFastBucket = bucketer != null && bucketer.IsInitialized
                ? bucketer.ActiveFastBucket
                : SystemDispatcher.CurrentFrameIndex & SimulationBucketConstants.FastBucketMask;
            int zoneFastBucket = SimulationBucketMath.ResolveBucket(unchecked((uint)EntityId.ToULong(GetEntityId())), SimulationBucketConstants.FastBucketMask);
            return AbsoluteUniversePosition.DistanceSq(in zoneAup, in cameraAup) > lodThresholdSq && activeFastBucket != zoneFastBucket;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // UTILITY: Fast Float Comparison
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static bool Approximately(float a, float b, float epsilon = 0.001f) =>
            Mathf.Abs(a - b) < epsilon;

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!MathGuard.IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // EDITOR
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
        #endif
    }
}
