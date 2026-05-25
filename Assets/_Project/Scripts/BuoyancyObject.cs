// ============================================================================
// HECTON-8 - BuoyancyObject.cs
// Buoyancy marker. Attach to any GameObject with a Rigidbody.
//
// OnEnable registers with the fluid runtime.
// OnDisable unregisters from the fluid runtime.
//
// Rigidbody is cached in Awake: zero GetComponent in runtime flow.
// No Update: the fluid runtime applies forces through the job path.
//
// PHYSICAL PARAMETERS:
//   density - object density (kg/m3).
//             Water = 1000. If density < waterDensity, the object floats.
//   volume  - object volume (m3). Controls buoyant force.
//   height  - object height (m). Used for partial-submersion estimates.
//
// GROUND CHECK + EXTERNAL SUPPRESSION:
//   Interior dry-zone suppression was removed from this component.
//   Base/player interior state travels through PlayerBaseEnter/Exit signals.
//   Ground contact still suppresses fluid when the object is effectively above
//   the waterline, preventing island contact from fighting buoyancy.
//
// GROUND CHECK IMPLEMENTATION:
//   Samples cached terrain/SDF authority every N fixed frames (configurable).
//   Unsupported collider-only layers intentionally resolve as no-ground instead
//   of pulling PhysX back into player-adjacent water state.
//   Staggered execution: not every frame, for O(n) performance with many objects.
//   Frame offset based on instance ID prevents all objects checking same frame.
// ============================================================================

using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace Hecton8.Physics
{
    /// <summary>
    /// Narrow buoyancy registration route consumed by buoyancy bodies without binding them to the fluid runtime owner.
    /// </summary>
    public interface IBuoyancyObjectRegistry : ISystem
    {
        /// <summary>Registers one buoyancy body with the active fluid solve.</summary>
        void Register(BuoyancyObject obj);

        /// <summary>Unregisters one buoyancy body from the active fluid solve.</summary>
        void Unregister(BuoyancyObject obj);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton/Physics/Buoyancy Object")]
    public sealed class BuoyancyObject : MonoBehaviour, IFixedTickable, IGlobalRegistryHotSwapListener, IBuoyancyAirStateReadModel
    {
        private static int _WaterLayer = -1;
        private static bool _layerCacheInitialized;

        private static void EnsureLayerCache()
        {
            if (_layerCacheInitialized)
                return;

            _WaterLayer = Hecton8.Core.HectonLayerMasks.Water;
            _layerCacheInitialized = true;
        }

        [Header("Profile")]
#if UNITY_EDITOR
        [Required("BuoyancyObject requires a BuoyancyProfile reference.")]
#endif
        [SerializeField] private BuoyancyProfile profile;
        [SerializeField] private bool autoApplyProfile = true;
        // ------------------------------------------------------------
        //  INSPECTOR
        // ------------------------------------------------------------

        [Header("-- Physical Properties --")]
        [Tooltip("Object density (kg/m3). " +
                 "Water ~ 1000, wood ~ 600, iron ~ 7800, titanium ~ 4500")]
#if UNITY_EDITOR
        [MinValue(0.01d)]
        [ValidateInput(nameof(IsFinitePositive), "Density must be finite and greater than zero.")]
#endif
        [SerializeField] private float density = 500f;

        [Tooltip("Object volume (m3). Controls buoyant force. " +
                 "A 10 cm cube is 0.001 m3.")]
#if UNITY_EDITOR
        [MinValue(0.0001d)]
        [ValidateInput(nameof(IsFinitePositive), "Volume must be finite and greater than zero.")]
#endif
        [SerializeField] private float volume = 0.01f;

        [Tooltip("Object height (m). Used for partial-submersion estimates. " +
                 "0 means treat as fully submerged.")]
#if UNITY_EDITOR
        [MinValue(0.01d)]
        [ValidateInput(nameof(IsFinitePositive), "Height must be finite and greater than zero.")]
#endif
        [SerializeField] private float height = 0.3f;

        [Tooltip("How strongly the object reacts to current. " +
                 "1 = standard, 0 = ignores flow, >1 = light or sail-like object.")]
#if UNITY_EDITOR
        [MinValue(0d)]
        [ValidateInput(nameof(IsFiniteNonNegative), "Current Response must be finite and non-negative.")]
#endif
        [SerializeField] private float currentResponse = 1f;

        [Tooltip("Stabilizing torque near the surface. " +
                 "Helps the object settle instead of tumbling.")]
#if UNITY_EDITOR
        [MinValue(0d)]
        [ValidateInput(nameof(IsFiniteNonNegative), "Surface Stability must be finite and non-negative.")]
#endif
        [SerializeField] private float surfaceStability = 0.75f;

        [Tooltip("Distance LOD importance for high-fidelity simulation. " +
                 "1 = standard, >1 stays high LOD longer, <1 simplifies sooner.")]
#if UNITY_EDITOR
        [MinValue(0.1d)]
        [ValidateInput(nameof(IsFinitePositive), "LOD Bias must be finite and greater than zero.")]
#endif
        [SerializeField] private float lodBias = 1f;

        [Tooltip("When disabled, the object always runs full quality with no distance LOD.")]
        [SerializeField] private bool allowDistanceLod = true;

        [Header("-- Ground Detection --")]
        [Tooltip("How often to perform ground check (in fixed frames). " +
                 "1 = every frame, 3 = every 3rd frame. Higher = better perf, slower response.")]
        [SerializeField, Range(1, 10)]
        private int groundCheckInterval = 3;

        [Tooltip("Distance to probe downward for ground detection (meters). " +
                 "Should be slightly more than half the object height.")]
#if UNITY_EDITOR
        [MinValue(0.01d)]
        [ValidateInput(nameof(IsFinitePositive), "Ground Check Distance must be finite and greater than zero.")]
#endif
        [SerializeField] private float groundCheckDistance = 1.0f;

        [Tooltip("Layers considered as ground (Terrain, Default, etc). " +
                 "MUST exclude Water layer to avoid false positives.")]
        [SerializeField] private LayerMask groundLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        // ------------------------------------------------------------
        //  CACHED
        // ------------------------------------------------------------

        private Rigidbody _rb;
        private Collider _collider;
        private Transform _cachedTransform;
        private IBuoyancyObjectRegistry _cachedFluidRuntime;
        private IBuoyancyObjectRegistry _registeredFluidRuntime;
        private float _runtimeLocalFluidDensity = 0f;
        private float _runtimeAngularDragMultiplier = 1f;
        private bool _runtimeLocalFluidDensityOverrideActive;
        private bool _registeredHotSwapListener;
        private ITerrainProvider _terrainProvider;
        private IVoxelSonarSdfReadModel _voxelSdfReadModel;

        // ------------------------------------------------------------
        //  GROUND STATE
        // ------------------------------------------------------------

        /// <summary>
        /// True when cached terrain/SDF detects solid ground below the object.
        /// Updated every groundCheckInterval fixed frames.
        /// Causes IsInAir to return true, disabling buoyancy on islands.
        /// </summary>
        private bool _isGrounded;
        private bool _externallySuppressed;

        /// <summary>
        /// Fixed-tick countdown for staggered ground checks.
        /// Avoids a per-body modulo in FixedTick.
        /// </summary>
        private int _groundCheckCountdown;

        /// <summary>
        /// Frame offset unique to this instance. Distributes ground checks
        /// across frames so not all BuoyancyObjects check on the same frame.
        /// Computed from GetEntityId() in Awake.
        /// </summary>
        private int _frameOffset;

        /// <summary>
        /// Cached ground hit for editor gizmos. Written from terrain/SDF providers, not PhysX.
        /// </summary>
        private KinematicSurfaceHit _groundHit;
        private bool _registeredToFixedTick;

        // ------------------------------------------------------------
        //  PUBLIC API
        // ------------------------------------------------------------

        /// <summary>Object density (kg/m3).</summary>
        public float Density => density;

        /// <summary>Volume (m3).</summary>
        public float Volume => volume;

        /// <summary>Height (m).</summary>
        public float Height => height;

        /// <summary>Current response multiplier.</summary>
        public float CurrentResponse => currentResponse;

        /// <summary>Stabilizing torque near the surface.</summary>
        public float SurfaceStability => surfaceStability;

        /// <summary>LOD priority offset.</summary>
        public float LodBias => lodBias;

        /// <summary>Whether distance-based LOD is allowed for this object.</summary>
        public bool AllowDistanceLod => allowDistanceLod;

        /// <summary>Cached Rigidbody. Guaranteed non-null by RequireComponent.</summary>
        public Rigidbody Body => _rb;

        /// <summary>
        /// Whether this object is grounded on terrain (not in water).
        /// Exposed for external systems that need to know ground state.
        /// </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>
        /// Legacy compatibility bridge. Interior state is no longer owned by buoyancy.
        /// </summary>
        public bool IsInDryZone => false;

        /// <summary>
        /// Object is out of water by solid ground contact.
        ///
        /// When true, the fluid runtime zeros all water forces.
        /// </summary>
        public bool IsInAir => _isGrounded;
        public BuoyancyProfile Profile => profile;
        public bool UseLocalFluidDensityOverride => _runtimeLocalFluidDensityOverrideActive;
        public float LocalFluidDensityOverride => _runtimeLocalFluidDensity;
        public float RuntimeAngularDragMultiplier => _runtimeAngularDragMultiplier;

        /// <summary>
        /// True while another system explicitly suppresses buoyancy forces for this body.
        /// </summary>
        public bool IsExternallySuppressed => _externallySuppressed;

        /// <summary>
        /// Resolves the world-space voxel sampling bounds used by the fluid job.
        /// Collider bounds are authoritative when present; otherwise the method
        /// derives a stable fallback prism from the authored volume and height.
        /// </summary>
        internal void GetBuoyancySampleBounds(out Vector3 center, out Vector3 extents)
        {
            if (_collider != null)
            {
                Bounds bounds = _collider.bounds;
                if (bounds.extents.sqrMagnitude > 0.000001f)
                {
                    center = bounds.center;
                    extents = new Vector3(
                        Mathf.Max(0.05f, bounds.extents.x),
                        Mathf.Max(0.05f, bounds.extents.y),
                        Mathf.Max(0.05f, bounds.extents.z));
                    return;
                }
            }

            float resolvedHeight = Mathf.Max(0.1f, height);
            float footprintArea = Mathf.Max(0.01f, volume * math.rcp(resolvedHeight));
            float halfWidth = Mathf.Max(0.05f, ApproximateSqrtPositive(footprintArea) * 0.5f);
            center = _cachedTransform != null ? _cachedTransform.position : transform.position;
            extents = new Vector3(halfWidth, resolvedHeight * 0.5f, halfWidth);
        }

        private static float ApproximateSqrtPositive(float value)
        {
            float safeValue = math.max(0f, value);
            float invSqrt = math.rsqrt(math.max(0.0001f, safeValue));
            return math.select(0f, safeValue * invSqrt, safeValue > 0f);
        }

        private static float ApproximateCubeRootPositive(float value)
        {
            float safeValue = math.max(0f, value);
            if (safeValue <= 0f)
                return 0f;

            return math.asfloat((math.asint(safeValue) / 3) + 709921077);
        }

        /// <summary>
        /// True when fluid simulation should be fully suppressed for this object.
        /// Dry interiors always suppress fluid. Ground contact suppresses fluid only
        /// when the object is effectively above the waterline, so underwater bottom
        /// contact can still receive buoyancy / drag / current.
        /// </summary>
        public bool ShouldSuppressFluid(float waterLevel)
        {
            if (_externallySuppressed)
                return true;

            if (!_isGrounded)
                return false;

            float bottomY;
            if (_collider != null)
                bottomY = _collider.bounds.min.y;
            else
                bottomY = _cachedTransform.position.y - Mathf.Max(0.05f, height * 0.5f);

            return bottomY >= waterLevel - 0.02f;
        }

        /// <summary>
        /// Enables or suppresses external fluid influence without unregistering this component.
        /// Used by heavy locomotion modes that must own the vertical force budget outright.
        /// </summary>
        public void SetExternalSuppression(bool suppressed)
        {
            _externallySuppressed = suppressed;
        }

        public void ApplyProfile()
        {
            if (profile == null)
                return;

            density = profile.density;
            volume = profile.volume;
            height = profile.height;
            currentResponse = profile.currentResponse;
            surfaceStability = profile.surfaceStability;
            lodBias = profile.lodBias;
            allowDistanceLod = profile.allowDistanceLod;
        }

        public void SetProfile(BuoyancyProfile newProfile, bool applyImmediately = true)
        {
            profile = newProfile;

            if (applyImmediately)
                ApplyProfileIfNeeded();
        }

        internal void ConfigureRuntimeFluidState(
            float massKg,
            float volumeM3,
            float heightMeters,
            float localFluidDensityKgPerM3,
            float angularDragMultiplier)
        {
            float safeVolumeM3 = float.IsFinite(volumeM3)
                ? Mathf.Max(0.0001f, volumeM3)
                : 0.0001f;
            float safeMassKg = float.IsFinite(massKg)
                ? Mathf.Max(0.01f, massKg)
                : 0.01f;

            volume = safeVolumeM3;
            density = safeMassKg * math.rcp(safeVolumeM3);
            height = float.IsFinite(heightMeters) ? Mathf.Max(0.05f, heightMeters) : 0.05f;
            _runtimeLocalFluidDensityOverrideActive = float.IsFinite(localFluidDensityKgPerM3) && localFluidDensityKgPerM3 > 0.01f;
            _runtimeLocalFluidDensity = _runtimeLocalFluidDensityOverrideActive
                ? Mathf.Max(0.01f, localFluidDensityKgPerM3)
                : 0f;
            _runtimeAngularDragMultiplier = Mathf.Max(0.1f, angularDragMultiplier);
        }

        private void ApplyProfileIfNeeded()
        {
            if (autoApplyProfile && profile != null)
                ApplyProfile();
        }

        // ------------------------------------------------------------
        //  LIFECYCLE
        // ------------------------------------------------------------

        private void Awake()
        {
            EnsureLayerCache();
            ApplyProfileIfNeeded();
            TryGetComponent(out _rb);
            TryGetComponent(out _collider);
            _cachedTransform = transform;

            // Compute frame offset from entity ID for staggered checks.
            // Abs because the truncated int can be negative.
            int id = unchecked((int)EntityId.ToULong(GetEntityId()));
            int safeGroundCheckInterval = math.max(1, groundCheckInterval);
            _frameOffset = (id < 0 ? -id : id) % safeGroundCheckInterval;
            _groundCheckCountdown = ResolveInitialGroundCheckCountdown(_frameOffset, safeGroundCheckInterval);
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            RebindFluidRuntime(_cachedFluidRuntime);
            TryRegisterToFixedTick();
        }

        private void Start()
        {
            TryRegisterToFixedTick();
        }

        private void OnDisable()
        {
            _isGrounded = false;

            UnregisterFromFluidRuntime();
            TryUnregisterHotSwapListener();
            TryUnregisterFromFixedTick();
        }

        private void OnDestroy()
        {
            UnregisterFromFluidRuntime();
            TryUnregisterHotSwapListener();
            TryUnregisterFromFixedTick();
        }

        // ------------------------------------------------------------
        //  FIXED TICK - Ground Check Only (staggered)
        // ------------------------------------------------------------

        /// <summary>
        /// Lightweight fixed tick: only increments a counter and performs
        /// a cached terrain/SDF ground probe every N frames. No other logic.
        ///
        /// Driven by GameTickManager via IFixedTickable so the component
        /// stays inside the centralized physics cadence contract.
        /// Cost: one countdown branch per fixed step plus one provider probe
        /// every groundCheckInterval frames (amortized).
        ///
        /// Zero-GC: no allocations. Uses cached hit state and owner read models.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            if (_groundCheckCountdown > 0)
            {
                _groundCheckCountdown--;
                return;
            }

            PerformGroundCheck();
            _groundCheckCountdown = math.max(1, groundCheckInterval) - 1;
        }

        private static int ResolveInitialGroundCheckCountdown(int frameOffset, int interval)
        {
            int safeInterval = math.max(1, interval);
            int firstProbeTick = safeInterval - math.clamp(frameOffset, 0, safeInterval - 1);
            return firstProbeTick - 1;
        }

        private void TryRegisterToFixedTick()
        {
            if (_registeredToFixedTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromFixedTick()
        {
            if (!_registeredToFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredToFixedTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.FluidRuntime:
                    if (ReferenceEquals(_registeredFluidRuntime, previousService))
                        UnregisterFromFluidRuntime();

                    _cachedFluidRuntime = currentService as IBuoyancyObjectRegistry;
                    RebindFluidRuntime(_cachedFluidRuntime);
                    break;

                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    _terrainProvider = currentService as ITerrainProvider;
                    break;

                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelSdfReadModel = currentService as IVoxelSonarSdfReadModel;
                    break;

                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        _registeredToFixedTick = false;
                        break;
                    }

                    if (isActiveAndEnabled)
                    {
                        TryUnregisterFromFixedTick();
                        TryRegisterToFixedTick();
                    }
                    break;
            }
        }

        private void RebindFluidRuntime(IBuoyancyObjectRegistry engine)
        {
            if (ReferenceEquals(_registeredFluidRuntime, engine))
                return;

            UnregisterFromFluidRuntime();
            if (!isActiveAndEnabled || engine == null)
                return;

            engine.Register(this);
            _registeredFluidRuntime = engine;
        }

        private void UnregisterFromFluidRuntime()
        {
            IBuoyancyObjectRegistry engine = _registeredFluidRuntime;
            if (engine != null)
                engine.Unregister(this);

            _registeredFluidRuntime = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        /// <summary>
        /// Probes downward from the object's position to detect terrain/SDF ground.
        ///
        /// Uses the bottom of the collider bounds if available, otherwise
        /// uses transform.position as the origin.
        ///
        /// Result stored in _isGrounded. When true, IsInAir returns true,
        /// which causes the fluid runtime to zero buoyancy forces.
        /// </summary>
        private void PerformGroundCheck()
        {
            // Determine probe origin: bottom of collider bounds, or transform position.
            Vector3 origin;

            if (_collider != null)
            {
                Bounds bounds = _collider.bounds;
                origin.x = bounds.center.x;
                origin.y = bounds.min.y + 0.05f; // Slight offset above bottom to avoid self-intersection
                origin.z = bounds.center.z;
            }
            else
            {
                origin = _cachedTransform.position;
            }

            int layerMask = ResolveGroundProbeMask();
            _isGrounded = TryResolveCachedGroundHit(origin, groundCheckDistance, layerMask, out _groundHit);
        }

        private void CacheRegistryServicesCold()
        {
            _cachedFluidRuntime = GlobalRegistry.BuoyancyObjectRegistry;
            _terrainProvider = GlobalRegistry.Terrain;
            _voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
        }

        private int ResolveGroundProbeMask()
        {
            int mask = groundLayers.value;
            if (mask == 0)
                return HectonLayerMasks.TerrainLayerMask | HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask;

            if (mask == HectonLayerMasks.StrictInteractionLayerMask)
                return mask | HectonLayerMasks.TerrainLayerMask | HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask;

            return mask;
        }

        private bool TryResolveCachedGroundHit(Vector3 origin, float range, int layerMask, out KinematicSurfaceHit hit)
        {
            hit = default;
            if (!IsFinite(origin) || !math.isfinite(range) || range <= 0f)
                return false;

            return TryResolveTerrainGroundHit(origin, range, layerMask, out hit) ||
                   TryResolveVoxelGroundHit(origin, range, layerMask, out hit);
        }

        private bool TryResolveTerrainGroundHit(Vector3 origin, float range, int layerMask, out KinematicSurfaceHit hit)
        {
            hit = default;
            if (!IncludesAnyLayer(layerMask, HectonLayerMasks.TerrainLayerMask))
                return false;

            ITerrainProvider terrainProvider = _terrainProvider;
            if (terrainProvider == null ||
                !terrainProvider.IsAvailable ||
                !terrainProvider.TryGetHeight(origin.x, origin.z, out float terrainHeight) ||
                !math.isfinite(terrainHeight))
            {
                return false;
            }

            float distance = origin.y - terrainHeight;
            if (!math.isfinite(distance) || distance < 0f || distance > range)
                return false;

            Vector3 point = new Vector3(origin.x, terrainHeight, origin.z);
            Vector3 normal = Vector3.up;
            if (terrainProvider.TryGetNormal(point.x, point.z, 1f, out Vector3 sampledNormal) && IsFinite(sampledNormal))
                normal = sampledNormal.normalized;

            hit.point = point;
            hit.normal = normal;
            hit.distance = distance;
            return true;
        }

        private bool TryResolveVoxelGroundHit(Vector3 origin, float range, int layerMask, out KinematicSurfaceHit hit)
        {
            hit = default;
            if (!IncludesAnyLayer(layerMask, HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask))
                return false;

            IVoxelSonarSdfReadModel readModel = _voxelSdfReadModel;
            if (readModel == null)
                return false;

            if (!VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    readModel,
                    new float3(origin.x, origin.y, origin.z),
                    new float3(0f, -1f, 0f),
                    range,
                    ResolveGroundSdfStepMeters(range),
                    out VoxelSonarSdfRaycastHit sdfHit) ||
                (sdfHit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u ||
                !math.all(math.isfinite(sdfHit.Point)) ||
                !math.all(math.isfinite(sdfHit.Normal)) ||
                !math.isfinite(sdfHit.Distance) ||
                sdfHit.Distance < 0f ||
                sdfHit.Distance > range)
            {
                return false;
            }

            float3 normal = math.normalizesafe(sdfHit.Normal, new float3(0f, 1f, 0f));
            hit.point = new Vector3(sdfHit.Point.x, sdfHit.Point.y, sdfHit.Point.z);
            hit.normal = new Vector3(normal.x, normal.y, normal.z);
            hit.distance = sdfHit.Distance;
            return true;
        }

        private static float ResolveGroundSdfStepMeters(float range)
        {
            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            float coarse = math.max(0.12f, range * 0.35f);
            float fine = math.max(0.04f, range * 0.1f);
            return math.lerp(coarse, fine, quality);
        }

        private static bool IncludesAnyLayer(int queryMask, int requiredMask)
        {
            return queryMask == -1 || (queryMask & requiredMask) != 0;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        // ------------------------------------------------------------
        //  EDITOR
        // ------------------------------------------------------------

#if UNITY_EDITOR
        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
#endif

            ApplyProfileIfNeeded();
            if (density < 0.01f) density = 0.01f;
            if (volume  < 0.0001f) volume = 0.0001f;
            if (height  < 0f) height = 0f;
            if (currentResponse < 0f) currentResponse = 0f;
            if (surfaceStability < 0f) surfaceStability = 0f;
            if (lodBias < 0.1f) lodBias = 0.1f;
            if (groundCheckDistance < 0.01f) groundCheckDistance = 0.01f;

            // Ensure Water layer is excluded from groundLayers
            if (_WaterLayer >= 0 && (groundLayers & (1 << _WaterLayer)) != 0)
            {
                groundLayers &= ~(1 << _WaterLayer);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            IFluidSurfaceCurrentReadModel fluidSurface = GlobalRegistry.FluidSurfaceCurrent;
            float waterY = fluidSurface != null ? fluidSurface.WaterLevel : 5000f;

            bool submerged = transform.position.y < waterY;

            // Green = grounded/suppressed, blue = underwater, yellow = above water.
            if (IsInAir)
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            else if (submerged)
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            else
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);

            Gizmos.DrawWireSphere(transform.position, ApproximateCubeRootPositive(volume));

            // Draw ground check ray
            Vector3 rayOrigin = transform.position;
            if (Application.isPlaying && _collider != null)
            {
                Bounds bounds = _collider.bounds;
                rayOrigin = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
            }

            Gizmos.color = _isGrounded
                ? new Color(0f, 1f, 0f, 0.8f)
                : new Color(1f, 0f, 0f, 0.4f);

            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * groundCheckDistance);

            if (_isGrounded && Application.isPlaying)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
                Gizmos.DrawWireSphere(_groundHit.point, 0.05f);
            }
        }
#endif
    }
}
