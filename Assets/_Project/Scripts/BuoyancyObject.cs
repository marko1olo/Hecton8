// ============================================================================
// HECTON-8 - BuoyancyObject.cs
// Buoyancy marker. Attach to any GameObject with a Rigidbody.
//
// OnEnable registers with HectonFluidEngine.
// OnDisable unregisters from HectonFluidEngine.
//
// Rigidbody is cached in Awake: zero GetComponent in runtime flow.
// No Update: HectonFluidEngine applies forces through the job path.
//
// PHYSICAL PARAMETERS:
//   density - object density (kg/m3).
//             Water = 1000. If density < waterDensity, the object floats.
//   volume  - object volume (m3). Controls buoyant force.
//   height  - object height (m). Used for partial-submersion estimates.
//
// DRY ZONES + GROUND CHECK:
//   IsInAir returns true when EITHER:
//     1. _dryZoneRefCount > 0 (inside unflooded base module), OR
//     2. _isGrounded == true (standing on terrain/island)
//
//   When IsInAir == true, HectonFluidEngine zeroes all buoyancy/drag forces.
//   This prevents objects from being "pushed out of water" when standing
//   on an island that sits below the water surface level.
//
// GROUND CHECK IMPLEMENTATION:
//   Performs Physics.Raycast downward every N fixed frames (configurable).
//   Uses a non-water LayerMask to detect terrain, island colliders, etc.
//   Staggered execution: not every frame, for O(n) performance with many objects.
//   Frame offset based on instance ID prevents all objects checking same frame.
// ============================================================================

using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton/Physics/Buoyancy Object")]
    public sealed class BuoyancyObject : MonoBehaviour, IFixedTickable
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

        [Tooltip("Distance to raycast downward for ground detection (meters). " +
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
        private float _runtimeLocalFluidDensity = 0f;
        private float _runtimeAngularDragMultiplier = 1f;
        private bool _runtimeLocalFluidDensityOverrideActive;

        // ------------------------------------------------------------
        //  DRY ZONE STATE
        // ------------------------------------------------------------

        /// <summary>
        /// Nested dry-zone reference count.
        /// The object can be inside overlapping modules at the same time.
        ///
        /// Increment: BaseModule entry into an unflooded trigger.
        /// Decrement: BaseModule exit or flooding.
        /// </summary>
        private int _dryZoneRefCount;

        // ------------------------------------------------------------
        //  GROUND STATE
        // ------------------------------------------------------------

        /// <summary>
        /// True when raycast detects solid ground below the object.
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
        /// Cached raycast hit. Avoids stack allocation in hot path.
        /// </summary>
        private RaycastHit _groundHit;
        private bool _registeredToFixedTick;
        private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[1]; // COLD ALLOC: single-hit ground probe buffer.

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
        /// True only when this object is inside one or more unflooded dry zones.
        /// Does not include terrain grounding.
        /// </summary>
        public bool IsInDryZone => _dryZoneRefCount > 0;

        /// <summary>
        /// Object is out of water: either inside an unflooded base module
        /// OR standing on solid ground (island/terrain).
        ///
        /// When true, HectonFluidEngine zeros all water forces.
        ///
        /// Priority: dryZone OR grounded -> IsInAir = true.
        /// This prevents buoyancy from pushing objects up through islands.
        /// </summary>
        public bool IsInAir => _dryZoneRefCount > 0 || _isGrounded;
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

            if (_dryZoneRefCount > 0)
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

        /// <summary>
        /// Called by BaseModule when the object enters a dry zone.
        /// Increments the ref-count. Thread safety is not required: main thread only.
        /// </summary>
        public void EnterDryZone()
        {
            _dryZoneRefCount++;
        }

        /// <summary>
        /// Called by BaseModule when the object exits a dry zone
        /// or when the module floods.
        /// Decrements the ref-count and clamps to 0 against bad calls.
        /// </summary>
        public void ExitDryZone()
        {
            _dryZoneRefCount--;
            if (_dryZoneRefCount < 0)
                _dryZoneRefCount = 0;
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
        }

        private void OnEnable()
        {
            HectonFluidEngine engine = GlobalRegistry.Fluid;
            if (engine != null)
                engine.Register(this);

            TryRegisterToFixedTick();
        }

        private void Start()
        {
            TryRegisterToFixedTick();
        }

        private void OnDisable()
        {
            // Reset dry-zone state when the object leaves runtime tracking.
            _dryZoneRefCount = 0;
            _isGrounded = false;

            HectonFluidEngine engine = GlobalRegistry.Fluid;
            if (engine != null)
                engine.Unregister(this);

            TryUnregisterFromFixedTick();
        }

        private void OnDestroy()
        {
            TryUnregisterFromFixedTick();
        }

        // ------------------------------------------------------------
        //  FIXED TICK - Ground Check Only (staggered)
        // ------------------------------------------------------------

        /// <summary>
        /// Lightweight fixed tick: only increments a counter and performs
        /// a raycast every N frames. No other logic.
        ///
        /// Driven by GameTickManager via IFixedTickable so the component
        /// stays inside the centralized physics cadence contract.
        /// Cost: one countdown branch per fixed step plus one raycast
        /// every groundCheckInterval frames (amortized).
        ///
        /// Zero-GC: no allocations. Uses cached hit state and a preallocated
        /// RaycastNonAlloc buffer on the instance.
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

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredToFixedTick = GlobalRegistry.FixedTickables.Contains(this);
        }

        private void TryUnregisterFromFixedTick()
        {
            if (!_registeredToFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredToFixedTick = false;
        }

        /// <summary>
        /// Raycasts downward from the object's position to detect solid ground.
        ///
        /// Uses the bottom of the collider bounds if available, otherwise
        /// uses transform.position as the origin.
        ///
        /// Result stored in _isGrounded. When true, IsInAir returns true,
        /// which causes HectonFluidEngine to zero buoyancy forces.
        /// </summary>
        private void PerformGroundCheck()
        {
            // Determine raycast origin: bottom of collider bounds, or transform position
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

            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHitBuffer,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore
            );

            _isGrounded = hitCount > 0;
            _groundHit = _isGrounded ? _groundHitBuffer[0] : default;
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

            HectonFluidEngine engine = GlobalRegistry.Fluid;
            float waterY = engine != null ? engine.WaterLevel : 5000f;

            bool submerged = transform.position.y < waterY;

            // Green = dry zone/grounded, blue = underwater, yellow = above water.
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
