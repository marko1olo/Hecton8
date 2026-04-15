// ============================================================================
// HECTON-8 — CullingManager.cs
// Manages frustum culling, distance culling, and layer-based cull distances.
//
// RESPONSIBILITIES:
//   • Distance-based object culling (30m/80m/200m thresholds)
//   • Frustum culling integration (Unity built-in)
//   • Layer-specific cull distances (debris/particles/props/flora)
//   • Occlusion culling validation
//   • Hysteresis to prevent activation thrashing
//
// ARCHITECTURE:
//   • Singleton via CullingManager.Instance
//   • ISlowTickable — runs ~0.5s interval (not per-frame)
//   • Zero-GC — pre-allocated collections, struct-based data
//   • O(1) operations where possible
//
// PERFORMANCE:
//   • Target: < 0.5ms per SlowTick
//   • Zero GC allocations
//   • Hysteresis prevents thrashing
//
// INTEGRATION:
//   • GameTickManager — ISlowTickable registration
//   • Camera.layerCullDistances for layer-based culling
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.World
{
    /// <summary>
    /// Manages frustum culling, distance culling, and layer-based cull distances.
    /// Runs at SlowTick interval (~0.5s) to minimize CPU overhead.
    /// </summary>
    /// <remarks>
    /// ZERO-GC ARCHITECTURE:
    ///   • Pre-allocated collections with capacity
    ///   • Struct-based CullableObject data
    ///   • No LINQ, no string operations in hot paths
    ///   • Hysteresis prevents activation thrashing
    /// 
    /// PERFORMANCE TARGET:
    ///   • SlowTick processing: < 0.5ms
    ///   • Supports 1000+ cullable objects
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-140)] // Run after LODSystemManager
    public sealed class CullingManager : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static CullingManager _instance;

        /// <summary>
        /// Singleton instance. Null if not initialized.
        /// </summary>
        public static CullingManager Instance => _instance;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Distance Culling ──────────────────")]
        [SerializeField, Tooltip("Cull distance for small objects (<1m)")]
        private float _smallObjectCullDistance = 30f;

        [SerializeField, Tooltip("Cull distance for medium objects")]
        private float _mediumObjectCullDistance = 80f;

        [SerializeField, Tooltip("Cull distance for large objects")]
        private float _largeObjectCullDistance = 200f;

        [SerializeField, Tooltip("Hysteresis percentage (prevents thrashing)")]
        private float _hysteresisPercent = 10f;

        [Header("── Layer Cull Distances ──────────────────")]
        [SerializeField, Tooltip("Debris layer cull distance")]
        private float _debrisLayerCullDistance = 40f;

        [SerializeField, Tooltip("Particles layer cull distance")]
        private float _particlesLayerCullDistance = 40f;

        [SerializeField, Tooltip("Props layer cull distance")]
        private float _propsLayerCullDistance = 100f;

        [SerializeField, Tooltip("Flora layer cull distance")]
        private float _floraLayerCullDistance = 100f;

        // ══════════════════════════════════════════════════════════
        //  CACHED LAYER MASKS
        // ══════════════════════════════════════════════════════════

        private static readonly int _DebrisLayer = LayerMask.NameToLayer("Debris");
        private static readonly int _ParticlesLayer = LayerMask.NameToLayer("Particles");
        private static readonly int _PropsLayer = LayerMask.NameToLayer("Props");
        private static readonly int _FloraLayer = LayerMask.NameToLayer("Flora");
        private static readonly int _TerrainLayer = LayerMask.NameToLayer("Terrain");

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Cullable object data. Struct for zero-GC storage in List.
        /// </summary>
        private struct CullableObject
        {
            public GameObject GameObject;
            public Transform Transform;
            public Bounds Bounds;
            public float CullDistance;
            public float ReactivateDistance; // CullDistance * (1 - hysteresis)
            public bool IsActive;
        }

        // COLD ALLOC: List<CullableObject>[1000] — registered cullable objects — owner: CullingManager
        private readonly List<CullableObject> _cullableObjects = new List<CullableObject>(1000);

        // COLD ALLOC: HashSet<GameObject>[1000] — O(1) duplicate check — owner: CullingManager
        private readonly HashSet<GameObject> _registeredObjects = new HashSet<GameObject>();

        // COLD ALLOC: Plane[6] — frustum planes — owner: CullingManager
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private Camera _mainCamera;
        private bool _registered;

        private int _frustumCulledCount;
        private int _distanceCulledCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Count of frustum-culled objects this frame.
        /// </summary>
        public int FrustumCulledCount => _frustumCulledCount;

        /// <summary>
        /// Count of distance-culled objects.
        /// </summary>
        public int DistanceCulledCount => _distanceCulledCount;

        /// <summary>
        /// Count of registered cullable objects.
        /// </summary>
        public int RegisteredObjectCount => _cullableObjects.Count;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CullingManager] Duplicate instance detected. Destroying " + gameObject.name);
                #endif
                Destroy(gameObject);
                return;
            }

            _instance = this;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CullingManager] Initialized.");
            #endif
        }

        private void Start()
        {
            // Apply layer cull distances after scene initialization
            ApplyLayerCullDistances();
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

        private void OnDestroy()
        {
            // Clear singleton
            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISLOWTICABLE IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Slow tick update (~0.5s interval).
        /// Processes distance culling with hysteresis.
        /// </summary>
        public void SlowTick()
        {
            // Cache camera reference
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Update frustum planes
            GeometryUtility.CalculateFrustumPlanes(_mainCamera, _frustumPlanes);

            Vector3 camPos = _mainCamera.transform.position;
            int distanceCulled = 0;

            // Process distance culling with hysteresis
            for (int i = 0; i < _cullableObjects.Count; i++)
            {
                CullableObject obj = _cullableObjects[i];
                if (obj.GameObject == null) continue;

                // Calculate squared distance (avoid sqrt)
                Vector3 delta = obj.Transform.position - camPos;
                float sqrDist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;

                if (obj.IsActive)
                {
                    // Check if should deactivate
                    float cullDistSqr = obj.CullDistance * obj.CullDistance;
                    if (sqrDist > cullDistSqr)
                    {
                        obj.GameObject.SetActive(false);
                        obj.IsActive = false;
                        distanceCulled++;
                    }
                }
                else
                {
                    // Check if should reactivate (with hysteresis)
                    float reactivateDistSqr = obj.ReactivateDistance * obj.ReactivateDistance;
                    if (sqrDist < reactivateDistSqr)
                    {
                        obj.GameObject.SetActive(true);
                        obj.IsActive = true;
                    }
                }

                // Write back modified struct
                _cullableObjects[i] = obj;
            }

            _distanceCulledCount = distanceCulled;

            // Frustum culling is handled by Unity automatically
            // We just track the count for monitoring
            _frustumCulledCount = 0; // Unity doesn't expose this count
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Register object for distance culling.
        /// </summary>
        /// <param name="obj">GameObject to cull</param>
        /// <param name="cullDistance">Distance threshold for culling</param>
        /// <param name="renderer">Optional Renderer component (cached by caller to avoid GetComponent)</param>
        public void RegisterCullableObject(GameObject obj, float cullDistance, Renderer renderer = null)
        {
            if (obj == null) return;

            // O(1) duplicate check via HashSet
            if (_registeredObjects.Contains(obj)) return;

            // Calculate hysteresis distance
            float hysteresisFactor = 1f - (_hysteresisPercent / 100f);
            float reactivateDistance = cullDistance * hysteresisFactor;

            var cullableObj = new CullableObject
            {
                GameObject = obj,
                Transform = obj.transform,
                Bounds = CalculateBounds(obj, renderer),
                CullDistance = cullDistance,
                ReactivateDistance = reactivateDistance,
                IsActive = obj.activeSelf
            };

            _cullableObjects.Add(cullableObj);
            _registeredObjects.Add(obj);
        }

        /// <summary>
        /// Unregister object from culling.
        /// </summary>
        /// <param name="obj">GameObject to unregister</param>
        public void UnregisterCullableObject(GameObject obj)
        {
            if (obj == null) return;

            // O(1) check via HashSet
            if (!_registeredObjects.Remove(obj)) return;

            // Find and remove from list (O(n) but only if HashSet confirmed presence)
            for (int i = _cullableObjects.Count - 1; i >= 0; i--)
            {
                if (_cullableObjects[i].GameObject == obj)
                {
                    // Swap-remove pattern for O(1) removal
                    int lastIndex = _cullableObjects.Count - 1;
                    if (i != lastIndex)
                    {
                        _cullableObjects[i] = _cullableObjects[lastIndex];
                    }
                    _cullableObjects.RemoveAt(lastIndex);
                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private void ApplyLayerCullDistances()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[CullingManager] Cannot apply layer cull distances: Camera.main is null.");
                    #endif
                    return;
                }
            }

            // COLD ALLOC: float[32] — layer cull distances — owner: CullingManager
            float[] distances = new float[32];

            // Set layer-specific cull distances using cached layer indices
            if (_DebrisLayer >= 0) distances[_DebrisLayer] = _debrisLayerCullDistance;
            if (_ParticlesLayer >= 0) distances[_ParticlesLayer] = _particlesLayerCullDistance;
            if (_PropsLayer >= 0) distances[_PropsLayer] = _propsLayerCullDistance;
            if (_FloraLayer >= 0) distances[_FloraLayer] = _floraLayerCullDistance;
            if (_TerrainLayer >= 0) distances[_TerrainLayer] = _mainCamera.farClipPlane;

            _mainCamera.layerCullDistances = distances;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CullingManager] Layer cull distances applied.");
            #endif
        }

        private Bounds CalculateBounds(GameObject obj, Renderer renderer)
        {
            // Use provided renderer if available (caller cached it)
            if (renderer != null)
                return renderer.bounds;

            // Fallback: try to get bounds from Collider (no allocation)
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
                return collider.bounds;

            // Last resort: use transform position with small bounds
            return new Bounds(obj.transform.position, Vector3.one);
        }
    }
}
