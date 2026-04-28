// â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
// â•‘  HADES HECTON-8 | HectonBiolumManager                                       â•‘
// â•‘  Central bioluminescence system (manages all zones globally)                â•‘
// â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

using UnityEngine;
using Hecton8.Caves;
using Hecton8.Core;
using System.Collections.Generic;
using Hecton8.Visor;

namespace Hecton8.Biolum
{
    #pragma warning disable CS0414 // Placeholder serialized tuning kept for future global-light budget wiring.
    /// <summary>
    /// Central manager for all bioluminescence zones in the world.
    /// Tracks active zones, manages global pools, optimizes updates.
    /// Handles:
    /// - Cave zones (CaveBiolumZone)
    /// - Ocean zones (OceanBiolumZone)
    /// - Floor zones (FloorBiolumZone)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonBiolumManager : MonoBehaviour, ITickable, IUpdatable
    {
        private static readonly int _FloraOceanBiolumColorId = Shader.PropertyToID("_HectonOceanBiolumColor");
        private static readonly int _FloraOceanBiolumStrengthId = Shader.PropertyToID("_HectonOceanBiolumStrength");
        private static readonly int _FloraFloorBiolumColorId = Shader.PropertyToID("_HectonFloorBiolumColor");
        private static readonly int _FloraFloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // SINGLETON
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public static HectonBiolumManager Instance { get; private set; }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // INSPECTOR SETTINGS
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Header("â”€â”€ Biolum Manager Settings â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Tooltip("Global intensity multiplier")]
        public float _globalIntensityScale = 1.0f;

        [SerializeField, Tooltip("Global range multiplier")]
        public float _globalRangeScale = 1.0f;

        [SerializeField, Range(0f, 1f), Tooltip("Global mood level (0=eerie, 1=vibrant)")]
        private float _globalMoodLevel = 0.5f;

        [SerializeField, Tooltip("Max total lights across all zones")]
        private int _maxTotalLights = 64;

        [SerializeField, Tooltip("Automatically find zones on start")]
        private bool _autoFindZones = true;

        [Header("â”€â”€ Sonar Communication â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(0f, 1f), Tooltip("Ð¡Ð¸Ð»Ð° ÐºÑ€Ð°Ñ‚ÐºÐ¾Ð³Ð¾ Ð±Ð¸Ð¾Ð»ÑŽÐ¼Ð¸Ð½ÐµÑÑ†ÐµÐ½Ñ‚Ð½Ð¾Ð³Ð¾ Ð¾Ñ‚Ð²ÐµÑ‚Ð° Ð½Ð° Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ sonar pulse Ð¸Ð³Ñ€Ð¾ÐºÐ°.")]
        private float _sonarCommunicationBoost = 0.42f;

        [SerializeField, Range(1f, 3f), Tooltip("ÐÐ°ÑÐºÐ¾Ð»ÑŒÐºÐ¾ sonar pulse ÑƒÑÐ¸Ð»Ð¸Ð²Ð°ÐµÑ‚ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‰ÑƒÑŽ Ð¾ÐºÐµÐ°Ð½ÑÐºÑƒÑŽ/Ð´Ð¾Ð½Ð½ÑƒÑŽ Ð±Ð¸Ð¾Ð»ÑŽÐ¼Ð¸Ð½ÐµÑÑ†ÐµÐ½Ñ†Ð¸ÑŽ.")]
        private float _sonarStrengthMultiplier = 1.65f;

        [SerializeField, Range(0f, 0.25f), Tooltip("ÐÐ°ÑÐºÐ¾Ð»ÑŒÐºÐ¾ sonar pulse Ð¿Ð¾Ð´Ð½Ð¸Ð¼Ð°ÐµÑ‚ Ñ†Ð²ÐµÑ‚ Ð±Ð¸Ð¾Ð»ÑŽÐ¼Ð° Ðº Ñ…Ð¾Ð»Ð¾Ð´Ð½Ð¾Ð¼Ñƒ Ð¾Ñ‚Ð²ÐµÑ‚Ð½Ð¾Ð¼Ñƒ ÑÐ²ÐµÑ‡ÐµÐ½Ð¸ÑŽ.")]
        private float _sonarColorLift = 0.08f;

        [SerializeField, Tooltip("Ð¡ÐºÐ¾Ñ€Ð¾ÑÑ‚ÑŒ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸Ñ sonar-Ð¾Ñ‚Ð²ÐµÑ‚Ð° Ñ„Ð»Ð¾Ñ€Ñ‹.")]
        private float _sonarDecayRate = 0.75f;

        [SerializeField, Tooltip("ÐÐ¾Ñ€Ð¼Ð°Ð»Ð¸Ð·ÑƒÑŽÑ‰Ð¸Ð¹ Ñ€Ð°Ð´Ð¸ÑƒÑ sonar pulse Ð´Ð»Ñ Ñ€Ð°ÑÑ‡ÐµÑ‚Ð° ÑÐ¸Ð»Ñ‹ Ð¾Ñ‚Ð²ÐµÑ‚Ð½Ð¾Ð¹ Ð²Ð¾Ð»Ð½Ñ‹.")]
        private float _sonarReferenceRadius = 100f;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PRIVATE STATE
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private List<HectonBiolumZone> _activeCaveZones = new List<HectonBiolumZone>();
        private List<HectonBiolumZone> _activeOceanZones = new List<HectonBiolumZone>();
        private List<HectonBiolumZone> _activeFloorZones = new List<HectonBiolumZone>();

        private int _totalActiveLights = 0;
        private bool _initialized = false;
        private Camera _cachedCamera = null;
        private Transform _cachedCameraTransform = null;
        private bool _tickRegistered = false;
        private float _floraGlobalUpdateTimer = 0f;
        private float _nextCameraResolveTime = 0f;
        private float _sonarPulseBoost = 0f;

        private const float CameraResolveCooldown = 1f;
        private static readonly Color _SonarResponseColor = new Color(0.62f, 0.94f, 1f, 1f);

        private Color _cachedOceanBiolumColor = Color.black;
        private Color _cachedFloorBiolumColor = Color.black;
        private float _cachedOceanBiolumStrength = 0f;
        private float _cachedFloorBiolumStrength = 0f;

        #if UNITY_EDITOR
        [SerializeField] private bool _debugLogUpdates = false;
        [SerializeField] private int _debugTickInvocations = 0;
        [SerializeField] private int _debugZoneTickPasses = 0;
        [SerializeField] private int _debugOceanZoneCount = 0;
        [SerializeField] private int _debugFloorZoneCount = 0;
        [SerializeField] private int _debugLastTickFrame = -1;
        [SerializeField] private float _debugLastTickDelta = 0f;
        #endif

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // LIFECYCLE
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResetFloraShaderGlobals();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            TryRegister();
            SpectrumEvents.OnSonarPulse += HandleSonarPulse;
        }

        private void OnDisable()
        {
            TryUnregister();
            SpectrumEvents.OnSonarPulse -= HandleSonarPulse;
            _sonarPulseBoost = 0f;

            ResetFloraShaderGlobals();
        }

        private void OnDestroy()
        {
            TryUnregister();
            SpectrumEvents.OnSonarPulse -= HandleSonarPulse;
            _sonarPulseBoost = 0f;

            ResetFloraShaderGlobals();

            if (Instance == this)
                Instance = null;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PUBLIC API
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Register a bioluminescence zone (called by zone OnEnable).
        /// </summary>
        public void RegisterZone(HectonBiolumZone zone)
        {
            if (zone == null) return;

            zone.EnsureTickRegistration();

            if (zone is CaveBiolumZone cave)
            {
                if (!_activeCaveZones.Contains(zone))
                    _activeCaveZones.Add(zone);
            }
            else if (zone is OceanBiolumZone ocean)
            {
                if (!_activeOceanZones.Contains(zone))
                    _activeOceanZones.Add(zone);
            }
            else if (zone is FloorBiolumZone floor)
            {
                if (!_activeFloorZones.Contains(zone))
                    _activeFloorZones.Add(zone);
            }

            #if UNITY_EDITOR
            if (_debugLogUpdates) Debug.Log($"[BiolumManager] Registered {zone.GetType().Name}: {_activeCaveZones.Count} caves, {_activeOceanZones.Count} ocean, {_activeFloorZones.Count} floor");
            #endif
        }

        /// <summary>
        /// Unregister a bioluminescence zone (called by zone OnDisable).
        /// </summary>
        public void UnregisterZone(HectonBiolumZone zone)
        {
            if (zone == null) return;

            _activeCaveZones.Remove(zone);
            _activeOceanZones.Remove(zone);
            _activeFloorZones.Remove(zone);

            #if UNITY_EDITOR
            if (_debugLogUpdates) Debug.Log($"[BiolumManager] Unregistered zone");
            #endif
        }

        /// <summary>
        /// Get total active lights across all zones.
        /// </summary>
        public int GetTotalActiveLights() => _totalActiveLights;

        /// <summary>
        /// Get zone count by type.
        /// </summary>
        public int GetCaveZoneCount() => _activeCaveZones.Count;
        public int GetOceanZoneCount() => _activeOceanZones.Count;
        public int GetFloorZoneCount() => _activeFloorZones.Count;

        internal int CopyNearbyZonesNonAlloc(Vector3 referencePosition, float maxDistance, HectonBiolumZone[] destination, float[] weights, bool includeOcean = true, bool includeFloor = true)
        {
            if (destination == null || destination.Length == 0 || weights == null || weights.Length < destination.Length)
                return 0;

            int count = 0;
            float maxDistanceSq = maxDistance > 0f ? maxDistance * maxDistance : float.PositiveInfinity;

            if (includeOcean)
                count = CollectNearbyZonesNonAlloc(_activeOceanZones, referencePosition, maxDistanceSq, destination, weights, count);

            if (includeFloor)
                count = CollectNearbyZonesNonAlloc(_activeFloorZones, referencePosition, maxDistanceSq, destination, weights, count);

            return count;
        }

        /// <summary>
        /// Get camera position for LOD calculations (cached).
        /// </summary>
        public Vector3 GetCameraPosition()
        {
            TryResolveCameraReference(false);
            return _cachedCameraTransform != null ? _cachedCameraTransform.position : Vector3.zero;
        }

        /// <summary>
        /// Set global mood level (affects all zones).
        /// </summary>
        public void SetGlobalMoodLevel(float mood)
        {
            _globalMoodLevel = Mathf.Clamp01(mood);
        }

        /// <summary>
        /// Set global intensity scale.
        /// </summary>
        public void SetGlobalIntensityScale(float scale)
        {
            _globalIntensityScale = Mathf.Max(0.1f, scale);
        }

        /// <summary>
        /// Set global range scale.
        /// </summary>
        public void SetGlobalRangeScale(float scale)
        {
            _globalRangeScale = Mathf.Max(0.1f, scale);
        }

        /// <summary>
        /// Update cheap global flora biolum shader inputs from the closest active ocean/floor zones.
        /// </summary>
        public void Tick(float deltaTime)
        {
#if UNITY_EDITOR
            _debugTickInvocations++;
            _debugLastTickFrame = Time.frameCount;
            _debugLastTickDelta = deltaTime;
            _debugOceanZoneCount = _activeOceanZones.Count;
            _debugFloorZoneCount = _activeFloorZones.Count;
#endif
            if (_sonarPulseBoost > 0f)
            {
                _sonarPulseBoost = Mathf.MoveTowards(_sonarPulseBoost, 0f, _sonarDecayRate * deltaTime);
            }

            _floraGlobalUpdateTimer += deltaTime;
            if (_floraGlobalUpdateTimer < 0.18f)
            {
                return;
            }

            _floraGlobalUpdateTimer = 0f;
            UpdateFloraShaderGlobals();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PRIVATE: Initialization & Updates
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Initialize manager: find existing zones or wait for registration.
        /// </summary>
        private void Initialize()
        {
            if (_initialized) return;

            if (_autoFindZones)
            {
                FindExistingZones();
            }

            TryResolveCameraReference(true);

            _initialized = true;
            UpdateFloraShaderGlobals();

            #if UNITY_EDITOR
            if (_debugLogUpdates) Debug.Log($"[BiolumManager] Initialized (zones: {_activeCaveZones.Count} caves, {_activeOceanZones.Count} ocean, {_activeFloorZones.Count} floor)");
            #endif
        }

        /// <summary>
        /// Find all biolum zones in scene.
        /// </summary>
        private void FindExistingZones()
        {
            HectonBiolumZone[] zones = Object.FindObjectsByType<HectonBiolumZone>();
            foreach (var zone in zones)
            {
                zone.EnsureTickRegistration();
                RegisterZone(zone);
            }
        }

        private void UpdateFloraShaderGlobals()
        {
            Vector3 cameraPosition = GetCameraPosition();

            Color oceanColor;
            float oceanStrength;
            bool hasOcean = TrySampleDominantZone(_activeOceanZones, cameraPosition, out oceanColor, out oceanStrength);

            Color floorColor;
            float floorStrength;
            bool hasFloor = TrySampleDominantZone(_activeFloorZones, cameraPosition, out floorColor, out floorStrength);

            _cachedOceanBiolumColor = hasOcean ? oceanColor : Color.black;
            _cachedFloorBiolumColor = hasFloor ? floorColor : Color.black;
            float sonarStrengthScale = Mathf.Lerp(1f, _sonarStrengthMultiplier, _sonarPulseBoost);
            float sonarColorLift = _sonarColorLift * _sonarPulseBoost;
            _cachedOceanBiolumStrength = hasOcean ? Mathf.Clamp01(oceanStrength * 0.28f * sonarStrengthScale) : 0f;
            _cachedFloorBiolumStrength = hasFloor ? Mathf.Clamp01(floorStrength * 0.24f * sonarStrengthScale) : 0f;

            if (hasOcean && sonarColorLift > 0f)
            {
                _cachedOceanBiolumColor = Color.Lerp(_cachedOceanBiolumColor, _SonarResponseColor, sonarColorLift);
            }

            if (hasFloor && sonarColorLift > 0f)
            {
                _cachedFloorBiolumColor = Color.Lerp(_cachedFloorBiolumColor, _SonarResponseColor, sonarColorLift);
            }

            Shader.SetGlobalColor(_FloraOceanBiolumColorId, _cachedOceanBiolumColor);
            Shader.SetGlobalFloat(_FloraOceanBiolumStrengthId, _cachedOceanBiolumStrength);
            Shader.SetGlobalColor(_FloraFloorBiolumColorId, _cachedFloorBiolumColor);
            Shader.SetGlobalFloat(_FloraFloorBiolumStrengthId, _cachedFloorBiolumStrength);
        }

        private bool TryResolveCameraReference(bool force)
        {
            if (_cachedCameraTransform != null)
                return true;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextCameraResolveTime)
                return false;

            _nextCameraResolveTime = currentTime + CameraResolveCooldown;

            if (BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                Camera playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
                if (playerCamera != null)
                {
                    _cachedCamera = playerCamera;
                    _cachedCameraTransform = playerCamera.transform;
                    return true;
                }

                _cachedCameraTransform = playerTransform;
                _cachedCamera = null;
                return true;
            }

            return false;
        }

        private bool TrySampleDominantZone(List<HectonBiolumZone> zones, Vector3 referencePosition, out Color sampledColor, out float sampledStrength)
        {
            sampledColor = Color.black;
            sampledStrength = 0f;

            int count = zones.Count;
            for (int i = 0; i < count; i++)
            {
                HectonBiolumZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                float zoneRange = zone.SampleZoneRange();
                if (zoneRange <= 0.01f)
                {
                    continue;
                }

                Vector3 delta = zone.GetZonePosition() - referencePosition;
                float zoneRangeSq = zoneRange * zoneRange;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq > zoneRangeSq)
                {
                    continue;
                }

                float proximity = 1f - Mathf.Clamp01(distanceSq / zoneRangeSq);
                float weightedStrength = zone.SampleZoneIntensity() * proximity;
                if (weightedStrength <= sampledStrength)
                {
                    continue;
                }

                sampledStrength = weightedStrength;
                sampledColor = zone.SampleZoneColor();
            }

            return sampledStrength > 0f;
        }

        private static int CollectNearbyZonesNonAlloc(List<HectonBiolumZone> zones, Vector3 referencePosition, float maxDistanceSq, HectonBiolumZone[] destination, float[] weights, int count)
        {
            int destinationCapacity = destination.Length;
            if (destinationCapacity == 0)
                return 0;

            int zoneCount = zones.Count;
            for (int i = 0; i < zoneCount; i++)
            {
                HectonBiolumZone zone = zones[i];
                if (zone == null)
                    continue;

                float zoneRange = zone.SampleZoneRange();
                if (zoneRange <= 0.01f)
                    continue;

                Vector3 delta = zone.GetZonePosition() - referencePosition;
                float distanceSq = delta.sqrMagnitude;
                float effectiveRangeSq = zoneRange * zoneRange;
                if (distanceSq > effectiveRangeSq || distanceSq > maxDistanceSq)
                    continue;

                float proximity = 1f - Mathf.Clamp01(distanceSq / effectiveRangeSq);
                float score = zone.SampleZoneIntensity() * proximity;
                if (score <= 0f)
                    continue;

                if (count < destinationCapacity)
                {
                    destination[count] = zone;
                    weights[count] = score;
                    count++;
                    InsertZoneDescending(destination, weights, count - 1);
                    continue;
                }

                int weakestIndex = destinationCapacity - 1;
                if (score <= weights[weakestIndex])
                    continue;

                destination[weakestIndex] = zone;
                weights[weakestIndex] = score;
                InsertZoneDescending(destination, weights, weakestIndex);
            }

            return count;
        }

        private static void InsertZoneDescending(HectonBiolumZone[] destination, float[] weights, int index)
        {
            while (index > 0 && weights[index] > weights[index - 1])
            {
                HectonBiolumZone zone = destination[index - 1];
                destination[index - 1] = destination[index];
                destination[index] = zone;

                float weight = weights[index - 1];
                weights[index - 1] = weights[index];
                weights[index] = weight;
                index--;
            }
        }

        private void ResetFloraShaderGlobals()
        {
            _cachedOceanBiolumColor = Color.black;
            _cachedFloorBiolumColor = Color.black;
            _cachedOceanBiolumStrength = 0f;
            _cachedFloorBiolumStrength = 0f;

            Shader.SetGlobalColor(_FloraOceanBiolumColorId, Color.black);
            Shader.SetGlobalFloat(_FloraOceanBiolumStrengthId, 0f);
            Shader.SetGlobalColor(_FloraFloorBiolumColorId, Color.black);
            Shader.SetGlobalFloat(_FloraFloorBiolumStrengthId, 0f);
        }

        private void HandleSonarPulse(float radius)
        {
            float normalizedRadius = Mathf.Clamp01(radius / Mathf.Max(1f, _sonarReferenceRadius));
            if (normalizedRadius <= 0f)
            {
                return;
            }

            _sonarPulseBoost = Mathf.Max(_sonarPulseBoost, _sonarCommunicationBoost * normalizedRadius);

            if (!_initialized)
            {
                return;
            }

            _floraGlobalUpdateTimer = 0f;
            UpdateFloraShaderGlobals();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // EDITOR
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void TryRegister()
        {
            if (_tickRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maxTotalLights = Mathf.Max(1, _maxTotalLights);
            _sonarDecayRate = Mathf.Max(0.01f, _sonarDecayRate);
            _sonarReferenceRadius = Mathf.Max(1f, _sonarReferenceRadius);
        }
#endif
    }
    #pragma warning restore CS0414
}
