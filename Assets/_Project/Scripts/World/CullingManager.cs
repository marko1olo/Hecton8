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
//   • GlobalRegistry.Culling is the authoritative runtime lookup.
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
using Hecton8.Bootstrap;
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
    public sealed class CullingManager : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Singleton instance. Null if not initialized.
        /// </summary>
        public static CullingManager Instance => GlobalRegistry.Culling;

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

        [Header("── References ───────────────────────────")]
        [SerializeField, Tooltip("Optional explicit camera reference. Falls back to player camera cold resolve paths.")]
        private Camera _cameraReference;

        // ══════════════════════════════════════════════════════════
        //  CACHED LAYER MASKS
        // ══════════════════════════════════════════════════════════

        private const int DebrisLayerIndex = 14;
        private const int MissingLayerIndex = -1;
        private static readonly int _debrisLayer = DebrisLayerIndex;
        private static readonly int _particlesLayer = MissingLayerIndex;
        private static readonly int _propsLayer = MissingLayerIndex;
        private static readonly int _floraLayer = MissingLayerIndex;
        private static readonly int _terrainLayer = MissingLayerIndex;

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
            public Renderer[] ManagedRenderers;
            public bool[] OriginalForceRenderingOffStates;
            public Bounds Bounds;
            public float CullDistanceSq;
            public float ReactivateDistanceSq;
            public bool IsActive;
            public bool ForceRenderingOffDirty;
            public bool PendingForceRenderingOff;
        }

        // COLD ALLOC: List<CullableObject>[1000] — registered cullable objects — owner: CullingManager
        private readonly List<CullableObject> _cullableObjects = new List<CullableObject>(1000);

        // COLD ALLOC: HashSet<GameObject>[1000] — O(1) duplicate check — owner: CullingManager
        private readonly HashSet<GameObject> _registeredObjects = new HashSet<GameObject>(1000);

        // COLD ALLOC: Plane[6] — frustum planes — owner: CullingManager
        private readonly Plane[] _frustumPlanes = new Plane[6];

        // COLD ALLOC: float[32] — layer cull distances — owner: CullingManager
        private readonly float[] _layerCullDistances = new float[32];
        // COLD ALLOC: List<Renderer>[32] — reusable renderer scan buffer for cold registration paths — owner: CullingManager
        private readonly List<Renderer> _rendererScratch = new List<Renderer>(32);

        private Camera _mainCamera;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerSensoryService _playerSensoryService;
        private bool _layerCullDistancesApplied;
        private bool _layerCullDistancesDirty;
        private bool _cullingEvaluationRequested;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _registeredHotSwap;

        private int _frustumCulledCount;
        private int _distanceCulledCount;
        private float _slowTickCPUTime;

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

        /// <summary>
        /// SlowTick CPU time in milliseconds (last execution).
        /// </summary>
        public float SlowTickCPUTime => _slowTickCPUTime;

        /// <summary>
        /// Count of objects with occlusion culling enabled (Editor-only).
        /// </summary>
        public int OcclusionCulledObjectCount => 0;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            CullingManager registered = GlobalRegistry.Culling;
            if (registered != null && registered != this)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Duplicate instance detected. Destroying duplicate.");
                #endif
                Destroy(gameObject);
                return;
            }

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[CullingManager] Initialized.");
            #endif
        }

        private void Start()
        {
            // Apply layer cull distances after scene initialization
            ApplyLayerCullDistances();
        }

        private void OnEnable()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            CachePlayerSensoryService(GlobalRegistry.PlayerSensory);
            TryRegisterService();
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            RestoreTrackedCullStates();
            TryUnregisterHotSwapListener();
            TryUnregisterLateFrame();
            TryUnregister();
            TryUnregisterService();
            _playerRuntimeContext = null;
            _playerSensoryService = null;
            _mainCamera = null;
            _layerCullDistancesApplied = false;
        }

        private void OnDestroy()
        {
            RestoreTrackedCullStates();
            TryUnregisterHotSwapListener();
            TryUnregisterLateFrame();
            TryUnregister();
            TryUnregisterService();

        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            CullingManager registered = GlobalRegistry.Culling;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterCullingRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Culling, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.Culling, this))
                GlobalRegistry.UnregisterCullingRuntime(this);

            _serviceRegistered = false;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = playerRuntimeContext;
        }

        private void CachePlayerSensoryService(IPlayerSensoryService playerSensoryService)
        {
            _playerSensoryService = playerSensoryService;
        }

        private void InvalidateCameraBinding()
        {
            if (_cameraReference == null)
                _mainCamera = null;

            _layerCullDistancesApplied = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    InvalidateCameraBinding();
                    break;
                case GlobalRegistryServiceSlot.PlayerSensory:
                    CachePlayerSensoryService(currentService as IPlayerSensoryService);
                    InvalidateCameraBinding();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registered = false;
                    _registeredLateFrame = false;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        TryRegisterLateFrame();
                    }
                    break;
            }
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
            _cullingEvaluationRequested = true;
        }

        private void RunCullingEvaluationVisualSync()
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            // Cache camera reference
            if (_mainCamera == null)
            {
                _mainCamera = ResolveMainCamera();
                if (_mainCamera == null) return;
            }

            if (!_layerCullDistancesApplied)
            {
                _layerCullDistancesDirty = true;
            }

            // Update frustum planes
            GeometryUtility.CalculateFrustumPlanes(_mainCamera, _frustumPlanes);

            Vector3 camPos = _mainCamera.transform.position;
            int distanceCulled = 0;
            int frustumCulled = 0;

            // Process distance culling with hysteresis
            for (int i = 0; i < _cullableObjects.Count; i++)
            {
                CullableObject obj = _cullableObjects[i];
                Transform objTransform = obj.Transform;
                if (obj.GameObject == null || objTransform == null) continue;

                // Calculate squared distance (avoid sqrt)
                Vector3 delta = objTransform.position - camPos;
                float sqrDist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;

                if (obj.IsActive)
                {
                    // Check if should deactivate
                    if (sqrDist > obj.CullDistanceSq)
                    {
                        SetCullState(ref obj, true);
                        distanceCulled++;
                    }
                    else
                    {
                        // Check frustum culling for active objects within distance
                        if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, obj.Bounds))
                        {
                            frustumCulled++;
                        }
                    }
                }
                else
                {
                    // Check if should reactivate (with hysteresis)
                    if (sqrDist < obj.ReactivateDistanceSq)
                    {
                        SetCullState(ref obj, false);
                    }
                }

                // Write back modified struct
                _cullableObjects[i] = obj;
            }

            _distanceCulledCount = distanceCulled;
            _frustumCulledCount = frustumCulled;

            long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _slowTickCPUTime = (endTicks - startTicks) / (float)System.Diagnostics.Stopwatch.Frequency * 1000f;
        }

        public void LateFrameTick()
        {
            if (_cullingEvaluationRequested)
            {
                _cullingEvaluationRequested = false;
                RunCullingEvaluationVisualSync();
            }

            if (_layerCullDistancesDirty || !_layerCullDistancesApplied)
            {
                _layerCullDistancesDirty = false;
                ApplyLayerCullDistances();
            }

            for (int i = 0; i < _cullableObjects.Count; i++)
            {
                CullableObject obj = _cullableObjects[i];
                if (obj.GameObject != null)
                    obj.Bounds = CalculateBounds(obj.GameObject, obj.ManagedRenderers);

                if (obj.ForceRenderingOffDirty)
                {
                    ApplyCullVisualState(ref obj, obj.PendingForceRenderingOff);
                    obj.ForceRenderingOffDirty = false;
                }

                _cullableObjects[i] = obj;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Register object for distance culling with automatic size-based distance assignment.
        /// </summary>
        /// <param name="obj">GameObject to cull</param>
        /// <param name="renderer">Optional Renderer component (cached by caller to avoid GetComponent)</param>
        public void RegisterCullableObject(GameObject obj, Renderer renderer = null)
        {
            if (obj == null) return;

            // Calculate bounds to determine size
            Bounds bounds = renderer != null
                ? CalculateSingleRendererBounds(obj, renderer)
                : CalculateBounds(obj, null);
            float sizeSq = bounds.size.sqrMagnitude;

            // Assign cull distance based on object size
            float cullDistance;
            if (sizeSq < 1f)
                cullDistance = _smallObjectCullDistance;
            else if (sizeSq < 25f)
                cullDistance = _mediumObjectCullDistance;
            else
                cullDistance = _largeObjectCullDistance;

            RegisterCullableObject(obj, cullDistance, renderer);
        }

        /// <summary>
        /// Register object for distance culling with explicit cull distance.
        /// </summary>
        /// <param name="obj">GameObject to cull</param>
        /// <param name="cullDistance">Distance threshold for culling</param>
        /// <param name="renderer">Optional Renderer component (cached by caller to avoid GetComponent)</param>
        public void RegisterCullableObject(GameObject obj, float cullDistance, Renderer renderer = null)
        {
            if (obj == null) return;

            // O(1) duplicate check via HashSet
            if (_registeredObjects.Contains(obj)) return;

            if (!TryCacheManagedRenderers(obj, renderer, out Renderer[] managedRenderers, out bool[] originalForceRenderingOffStates))
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Skipping registration for object without Renderer owner.", obj);
                #endif
                return;
            }

            // Validate Unity frustum culling is enabled
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = 0; i < managedRenderers.Length; i++)
            {
                Renderer managedRenderer = managedRenderers[i];
                if (managedRenderer == null)
                    continue;

                if (!managedRenderer.enabled)
                {
                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with disabled Renderer.", obj);
                    return;
                }

                if (managedRenderer.forceRenderingOff)
                {
                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with externally force-culled Renderer.", obj);
                    return;
                }
            }
            #endif

            // Calculate hysteresis distance
            float hysteresisFactor = 1f - (_hysteresisPercent / 100f);
            float reactivateDistance = cullDistance * hysteresisFactor;
            float cullDistanceSq = cullDistance * cullDistance;
            float reactivateDistanceSq = reactivateDistance * reactivateDistance;

            var cullableObj = new CullableObject
            {
                GameObject = obj,
                Transform = obj.transform,
                ManagedRenderers = managedRenderers,
                OriginalForceRenderingOffStates = originalForceRenderingOffStates,
                Bounds = CalculateBounds(obj, managedRenderers),
                CullDistanceSq = cullDistanceSq,
                ReactivateDistanceSq = reactivateDistanceSq,
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
                    CullableObject cullableObject = _cullableObjects[i];
                    RestoreCullState(ref cullableObject);
                    _cullableObjects[i] = cullableObject;

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
                _mainCamera = ResolveMainCamera();
                if (_mainCamera == null)
                {
                    _layerCullDistancesApplied = false;
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Cannot apply layer cull distances: runtime camera is unresolved.");
                    #endif
                    return;
                }
            }

            // Clear array (reuse pre-allocated)
            for (int i = 0; i < _layerCullDistances.Length; i++)
            {
                _layerCullDistances[i] = 0f;
            }

            // Set layer-specific cull distances using cached layer indices
            if (_debrisLayer >= 0) _layerCullDistances[_debrisLayer] = _debrisLayerCullDistance;
            if (_particlesLayer >= 0) _layerCullDistances[_particlesLayer] = _particlesLayerCullDistance;
            if (_propsLayer >= 0) _layerCullDistances[_propsLayer] = _propsLayerCullDistance;
            if (_floraLayer >= 0) _layerCullDistances[_floraLayer] = _floraLayerCullDistance;
            if (_terrainLayer >= 0) _layerCullDistances[_terrainLayer] = _mainCamera.farClipPlane;

            _mainCamera.layerCullDistances = _layerCullDistances;
            _layerCullDistancesApplied = true;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[CullingManager] Layer cull distances applied.");
            #endif
        }

        private Camera ResolveMainCamera()
        {
            if (_cameraReference != null)
                return _cameraReference;

            IPlayerSensoryService playerSensoryService = _playerSensoryService;
            if (playerSensoryService != null && playerSensoryService.PlayerCamera != null)
            {
                return playerSensoryService.PlayerCamera;
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && playerContext.PlayerCamera != null)
            {
                return playerContext.PlayerCamera;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerTransform.TryGetComponent(out Camera playerOwnedCamera))
                    return playerOwnedCamera;
            }

            return null;
        }

        private Bounds CalculateSingleRendererBounds(GameObject obj, Renderer renderer)
        {
            if (renderer != null)
                return renderer.bounds;

            return CalculateBounds(obj, null);
        }

        private Bounds CalculateBounds(GameObject obj, Renderer[] renderers)
        {
            if (renderers != null)
            {
                bool boundsInitialized = false;
                Bounds combinedBounds = default;

                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    if (!boundsInitialized)
                    {
                        combinedBounds = renderer.bounds;
                        boundsInitialized = true;
                        continue;
                    }

                    combinedBounds.Encapsulate(renderer.bounds);
                }

                if (boundsInitialized)
                    return combinedBounds;
            }

            // Fallback: try to get bounds from Collider (no allocation)
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
                return collider.bounds;

            // Last resort: use transform position with small bounds
            return new Bounds(obj.transform.position, Vector3.one);
        }

        private bool TryCacheManagedRenderers(GameObject obj, Renderer renderer, out Renderer[] managedRenderers, out bool[] originalForceRenderingOffStates)
        {
            managedRenderers = null;
            originalForceRenderingOffStates = null;

            if (obj == null)
                return false;

            if (renderer != null)
            {
                // COLD ALLOC: Renderer[1]/bool[1] - persistent per-object culling state, not a temp registration scratch.
                managedRenderers = new Renderer[1];
                originalForceRenderingOffStates = new bool[1];
                managedRenderers[0] = renderer;
                originalForceRenderingOffStates[0] = renderer.forceRenderingOff;
                return true;
            }

            _rendererScratch.Clear();
            obj.GetComponentsInChildren(true, _rendererScratch);

            int validRendererCount = 0;
            for (int i = 0; i < _rendererScratch.Count; i++)
            {
                if (_rendererScratch[i] != null)
                    validRendererCount++;
            }

            if (validRendererCount == 0)
                return false;

            managedRenderers = new Renderer[validRendererCount];
            originalForceRenderingOffStates = new bool[validRendererCount];

            int writeIndex = 0;
            for (int i = 0; i < _rendererScratch.Count; i++)
            {
                Renderer managedRenderer = _rendererScratch[i];
                if (managedRenderer == null)
                    continue;

                managedRenderers[writeIndex] = managedRenderer;
                originalForceRenderingOffStates[writeIndex] = managedRenderer.forceRenderingOff;
                writeIndex++;
            }

            return true;
        }

        private static void SetCullState(ref CullableObject obj, bool isCulled)
        {
            if (obj.ManagedRenderers == null)
                return;

            obj.PendingForceRenderingOff = isCulled;
            obj.ForceRenderingOffDirty = true;
            obj.IsActive = !isCulled;
        }

        private static void ApplyCullVisualState(ref CullableObject obj, bool isCulled)
        {
            if (obj.ManagedRenderers == null)
                return;

            for (int i = 0; i < obj.ManagedRenderers.Length; i++)
            {
                Renderer renderer = obj.ManagedRenderers[i];
                if (renderer == null)
                    continue;

                renderer.forceRenderingOff = isCulled;
            }
        }

        private static void RestoreCullState(ref CullableObject obj)
        {
            if (obj.ManagedRenderers == null || obj.OriginalForceRenderingOffStates == null)
                return;

            int restoreCount = Mathf.Min(obj.ManagedRenderers.Length, obj.OriginalForceRenderingOffStates.Length);
            for (int i = 0; i < restoreCount; i++)
            {
                Renderer renderer = obj.ManagedRenderers[i];
                if (renderer == null)
                    continue;

                renderer.forceRenderingOff = obj.OriginalForceRenderingOffStates[i];
            }

            obj.IsActive = obj.GameObject != null && obj.GameObject.activeSelf;
        }

        private void RestoreTrackedCullStates()
        {
            for (int i = 0; i < _cullableObjects.Count; i++)
            {
                CullableObject obj = _cullableObjects[i];
                if (obj.GameObject == null)
                    continue;

                RestoreCullState(ref obj);
                _cullableObjects[i] = obj;
            }
        }

    }
}
