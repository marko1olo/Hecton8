// ============================================================================
// HECTON-8 — BuoyancyObject.cs
// Маркер плавучести. Вешается на любой GameObject с Rigidbody.
//
// При OnEnable регистрируется в HectonFluidEngine.
// При OnDisable — отписывается.
//
// Rigidbody кэшируется в Awake — zero GetComponent в рантайме.
// Никакого Update — все силы применяет HectonFluidEngine через Job.
//
// ФИЗИЧЕСКИЕ ПАРАМЕТРЫ:
//   density — плотность объекта (кг/м³).
//             Вода = 1000. Если density < waterDensity → объект всплывает.
//   volume  — объём объекта (м³). Определяет силу Архимеда.
//   height  — высота объекта (м). Для расчёта частичного погружения.
//
// СУХИЕ ЗОНЫ + GROUND CHECK:
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
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Physical Properties ───────────────────────")]
        [Tooltip("Плотность объекта (кг/м³). " +
                 "Вода ≈ 1000, Дерево ≈ 600, Железо ≈ 7800, Титан ≈ 4500")]
#if UNITY_EDITOR
        [MinValue(0.01d)]
        [ValidateInput(nameof(IsFinitePositive), "Density must be finite and greater than zero.")]
#endif
        [SerializeField] private float density = 500f;

        [Tooltip("Объём объекта (м³). Определяет выталкивающую силу. " +
                 "Куб 10см = 0.001 м³")]
#if UNITY_EDITOR
        [MinValue(0.0001d)]
        [ValidateInput(nameof(IsFinitePositive), "Volume must be finite and greater than zero.")]
#endif
        [SerializeField] private float volume = 0.01f;

        [Tooltip("Высота объекта (м). Для расчёта частичного погружения. " +
                 "0 = считать полностью погружённым")]
#if UNITY_EDITOR
        [MinValue(0.01d)]
        [ValidateInput(nameof(IsFinitePositive), "Height must be finite and greater than zero.")]
#endif
        [SerializeField] private float height = 0.3f;

        [Tooltip("Насколько сильно объект реагирует на течение. " +
                 "1 = стандартно, 0 = игнорирует поток, >1 = лёгкий/парусный объект.")]
#if UNITY_EDITOR
        [MinValue(0d)]
        [ValidateInput(nameof(IsFiniteNonNegative), "Current Response must be finite and non-negative.")]
#endif
        [SerializeField] private float currentResponse = 1f;

        [Tooltip("Стабилизирующий момент у поверхности. " +
                 "Помогает объекту красиво выравниваться и не болтаться как мусорный баг.")]
#if UNITY_EDITOR
        [MinValue(0d)]
        [ValidateInput(nameof(IsFiniteNonNegative), "Surface Stability must be finite and non-negative.")]
#endif
        [SerializeField] private float surfaceStability = 0.75f;

        [Tooltip("Насколько важен объект для high-fidelity симуляции на расстоянии. " +
                 "1 = стандарт, >1 = дольше остаётся в high LOD, <1 = раньше упрощается.")]
#if UNITY_EDITOR
        [MinValue(0.1d)]
        [ValidateInput(nameof(IsFinitePositive), "LOD Bias must be finite and greater than zero.")]
#endif
        [SerializeField] private float lodBias = 1f;

        [Tooltip("Если выключено — объект всегда считается в полном качестве, без distance LOD.")]
        [SerializeField] private bool allowDistanceLod = true;

        [Header("── Ground Detection ─────────────────────────")]
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

        // ══════════════════════════════════════════════════════════
        //  CACHED
        // ══════════════════════════════════════════════════════════

        private Rigidbody _rb;
        private Collider _collider;
        private Transform _cachedTransform;
        private float _runtimeLocalFluidDensity = 0f;
        private float _runtimeAngularDragMultiplier = 1f;
        private bool _runtimeLocalFluidDensityOverrideActive;

        // ══════════════════════════════════════════════════════════
        //  DRY ZONE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Счётчик вложенности «сухих зон».
        /// Объект может находиться в перекрывающихся модулях одновременно.
        ///
        /// Инкремент: BaseModule при входе в незатопленный триггер.
        /// Декремент: BaseModule при выходе или затоплении.
        /// </summary>
        private int _dryZoneRefCount;

        // ══════════════════════════════════════════════════════════
        //  GROUND STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// True when raycast detects solid ground below the object.
        /// Updated every groundCheckInterval fixed frames.
        /// Causes IsInAir to return true → disabling buoyancy on islands.
        /// </summary>
        private bool _isGrounded;
        private bool _externallySuppressed;

        /// <summary>
        /// Fixed-frame counter for staggered ground checks.
        /// Incremented in FixedUpdate (lightweight — just a counter).
        /// </summary>
        private int _frameCounter;

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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Плотность объекта (кг/м³).</summary>
        public float Density => density;

        /// <summary>Объём (м³).</summary>
        public float Volume => volume;

        /// <summary>Высота (м).</summary>
        public float Height => height;

        /// <summary>Множитель реакции на течение.</summary>
        public float CurrentResponse => currentResponse;

        /// <summary>Стабилизирующий момент у поверхности.</summary>
        public float SurfaceStability => surfaceStability;

        /// <summary>Смещение приоритета LOD.</summary>
        public float LodBias => lodBias;

        /// <summary>Разрешён ли distance-based LOD для этого объекта.</summary>
        public bool AllowDistanceLod => allowDistanceLod;

        /// <summary>Кэшированный Rigidbody. Гарантированно не-null (RequireComponent).</summary>
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
        /// Объект находится «в воздухе» — either inside an unflooded base module
        /// OR standing on solid ground (island/terrain).
        ///
        /// When true, HectonFluidEngine обнуляет все водные силы.
        ///
        /// Priority: dryZone OR grounded → IsInAir = true.
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
            float footprintArea = Mathf.Max(0.01f, volume / resolvedHeight);
            float halfWidth = Mathf.Max(0.05f, Mathf.Sqrt(footprintArea) * 0.5f);
            center = _cachedTransform != null ? _cachedTransform.position : transform.position;
            extents = new Vector3(halfWidth, resolvedHeight * 0.5f, halfWidth);
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
        /// Вызывается BaseModule при входе объекта в сухую зону.
        /// Увеличивает ref-count. Thread-safe не требуется (main thread only).
        /// </summary>
        public void EnterDryZone()
        {
            _dryZoneRefCount++;
        }

        /// <summary>
        /// Вызывается BaseModule при выходе объекта из сухой зоны
        /// или при затоплении модуля.
        /// Уменьшает ref-count. Clamp к 0 для защиты от некорректных вызовов.
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
            density = safeMassKg / safeVolumeM3;
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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

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
            _frameOffset = (id < 0 ? -id : id) % groundCheckInterval;
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
            // Сбрасываем ref-count — объект больше не в зоне
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

        // ══════════════════════════════════════════════════════════
        //  FIXED TICK — Ground Check Only (staggered)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Lightweight fixed tick: only increments a counter and performs
        /// a raycast every N frames. No other logic.
        ///
        /// Driven by GameTickManager via IFixedTickable so the component
        /// stays inside the centralized physics cadence contract.
        /// Cost: one integer increment per fixed step plus one raycast
        /// every groundCheckInterval frames (amortized).
        ///
        /// Zero-GC: no allocations. Uses cached hit state and a preallocated
        /// RaycastNonAlloc buffer on the instance.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            _frameCounter++;

            // Staggered check: this instance checks on its designated frame
            if ((_frameCounter + _frameOffset) % groundCheckInterval != 0)
                return;

            PerformGroundCheck();
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

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

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

            // Зелёный = в сухой зоне/grounded, синий = под водой, жёлтый = над водой
            if (IsInAir)
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            else if (submerged)
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            else
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);

            Gizmos.DrawWireSphere(transform.position, Mathf.Pow(volume, 1f / 3f));

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
