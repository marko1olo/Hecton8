// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HADES HECTON-8 | HectonBiolumManager                                       ║
// ║  Central bioluminescence system (manages all zones globally)                ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using Hecton8.Caves;
using Hecton8.Core;
using System.Collections.Generic;

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
    public sealed class HectonBiolumManager : MonoBehaviour, ITickable
    {
        private static readonly int _FloraOceanBiolumColorId = Shader.PropertyToID("_HectonOceanBiolumColor");
        private static readonly int _FloraOceanBiolumStrengthId = Shader.PropertyToID("_HectonOceanBiolumStrength");
        private static readonly int _FloraFloorBiolumColorId = Shader.PropertyToID("_HectonFloorBiolumColor");
        private static readonly int _FloraFloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");

        // ─────────────────────────────────────────────────────────────────────────────
        // SINGLETON
        // ─────────────────────────────────────────────────────────────────────────────

        public static HectonBiolumManager Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────────────
        // INSPECTOR SETTINGS
        // ─────────────────────────────────────────────────────────────────────────────

        [Header("── Biolum Manager Settings ────────")]
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

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────────────

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

        private const float CameraResolveCooldown = 1f;

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

        // ─────────────────────────────────────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResetFloraShaderGlobals();
        }

        private void Start()
        {
            Initialize();

            if (GameTickManager.Instance != null && !_tickRegistered)
            {
                GameTickManager.Instance.Register(this);
                _tickRegistered = true;
            }
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_tickRegistered)
            {
                GameTickManager.Instance.Register(this);
                _tickRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _tickRegistered)
            {
                GameTickManager.Instance.Unregister(this);
                _tickRegistered = false;
            }

            ResetFloraShaderGlobals();
        }

        private void OnDestroy()
        {
            if (GameTickManager.Instance != null && _tickRegistered)
            {
                GameTickManager.Instance.Unregister(this);
                _tickRegistered = false;
            }

            ResetFloraShaderGlobals();

            if (Instance == this)
                Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────────────────────

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
            TickZones(deltaTime);

            _floraGlobalUpdateTimer += deltaTime;
            if (_floraGlobalUpdateTimer < 0.18f)
            {
                return;
            }

            _floraGlobalUpdateTimer = 0f;
            UpdateFloraShaderGlobals();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE: Initialization & Updates
        // ─────────────────────────────────────────────────────────────────────────────

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
            _cachedOceanBiolumStrength = hasOcean ? Mathf.Clamp01(oceanStrength * 0.28f) : 0f;
            _cachedFloorBiolumStrength = hasFloor ? Mathf.Clamp01(floorStrength * 0.24f) : 0f;

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
                Camera playerCamera = playerTransform.GetComponentInChildren<Camera>();
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

        // ─────────────────────────────────────────────────────────────────────────────
        // EDITOR
        // ─────────────────────────────────────────────────────────────────────────────

        private void TickZones(float deltaTime)
        {
#if UNITY_EDITOR
            _debugZoneTickPasses++;
#endif
            TickZoneList(_activeCaveZones, deltaTime);
            TickZoneList(_activeOceanZones, deltaTime);
            TickZoneList(_activeFloorZones, deltaTime);
        }

        private static void TickZoneList(List<HectonBiolumZone> zones, float deltaTime)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                HectonBiolumZone zone = zones[i];
                if (zone == null || zone is UnityEngine.Object zoneObject && zoneObject == null)
                {
                    zones.RemoveAt(i);
                    continue;
                }

                zone.Tick(deltaTime);
            }
        }

        #if UNITY_EDITOR
        private void OnGUI()
        {
            if (!_debugLogUpdates) return;

            GUI.Label(new Rect(10, 10, 400, 120),
                $"[BIOLUM MANAGER]\n" +
                $"Caves: {_activeCaveZones.Count}\n" +
                $"Ocean: {_activeOceanZones.Count}\n" +
                $"Floor: {_activeFloorZones.Count}\n" +
                $"Total: {_activeCaveZones.Count + _activeOceanZones.Count + _activeFloorZones.Count} zones\n" +
                $"Mood: {_globalMoodLevel:F2}\n" +
                $"Intensity Scale: {_globalIntensityScale:F2}");
        }
        #endif
    }
    #pragma warning restore CS0414
}
