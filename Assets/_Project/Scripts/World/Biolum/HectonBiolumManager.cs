// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HADES HECTON-8 | HectonBiolumManager                                       ║
// ║  Central bioluminescence system (manages all zones globally)                ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using Hecton8.Caves;
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
    public sealed class HectonBiolumManager : MonoBehaviour
    {
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

        #if UNITY_EDITOR
        [SerializeField] private bool _debugLogUpdates = false;
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
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
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
            if (_cachedCamera == null)
                _cachedCamera = Camera.main;
            return _cachedCamera != null ? _cachedCamera.transform.position : Vector3.zero;
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

            _initialized = true;

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
                RegisterZone(zone);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // EDITOR
        // ─────────────────────────────────────────────────────────────────────────────

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
