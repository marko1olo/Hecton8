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

using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton/Physics/Buoyancy Object")]
    public sealed class BuoyancyObject : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Physical Properties ───────────────────────")]
        [Tooltip("Плотность объекта (кг/м³). " +
                 "Вода ≈ 1000, Дерево ≈ 600, Железо ≈ 7800, Титан ≈ 4500")]
        [SerializeField] private float density = 500f;

        [Tooltip("Объём объекта (м³). Определяет выталкивающую силу. " +
                 "Куб 10см = 0.001 м³")]
        [SerializeField] private float volume = 0.01f;

        [Tooltip("Высота объекта (м). Для расчёта частичного погружения. " +
                 "0 = считать полностью погружённым")]
        [SerializeField] private float height = 0.3f;

        [Tooltip("Насколько сильно объект реагирует на течение. " +
                 "1 = стандартно, 0 = игнорирует поток, >1 = лёгкий/парусный объект.")]
        [SerializeField] private float currentResponse = 1f;

        [Tooltip("Стабилизирующий момент у поверхности. " +
                 "Помогает объекту красиво выравниваться и не болтаться как мусорный баг.")]
        [SerializeField] private float surfaceStability = 0.75f;

        [Tooltip("Насколько важен объект для high-fidelity симуляции на расстоянии. " +
                 "1 = стандарт, >1 = дольше остаётся в high LOD, <1 = раньше упрощается.")]
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
        [SerializeField] private float groundCheckDistance = 1.0f;

        [Tooltip("Layers considered as ground (Terrain, Default, etc). " +
                 "MUST exclude Water layer to avoid false positives.")]
        [SerializeField] private LayerMask groundLayers = ~0; // default: everything

        // ══════════════════════════════════════════════════════════
        //  CACHED
        // ══════════════════════════════════════════════════════════

        private Rigidbody _rb;
        private Collider _collider;
        private Transform _cachedTransform;

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

        /// <summary>
        /// Fixed-frame counter for staggered ground checks.
        /// Incremented in FixedUpdate (lightweight — just a counter).
        /// </summary>
        private int _frameCounter;

        /// <summary>
        /// Frame offset unique to this instance. Distributes ground checks
        /// across frames so not all BuoyancyObjects check on the same frame.
        /// Computed from GetInstanceID() in Awake.
        /// </summary>
        private int _frameOffset;

        /// <summary>
        /// Cached raycast hit. Avoids stack allocation in hot path.
        /// </summary>
        private RaycastHit _groundHit;

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
        /// Объект находится «в воздухе» — either inside an unflooded base module
        /// OR standing on solid ground (island/terrain).
        ///
        /// When true, HectonFluidEngine обнуляет все водные силы.
        ///
        /// Priority: dryZone OR grounded → IsInAir = true.
        /// This prevents buoyancy from pushing objects up through islands.
        /// </summary>
        public bool IsInAir => _dryZoneRefCount > 0 || _isGrounded;

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            TryGetComponent(out _rb);
            TryGetComponent(out _collider);
            _cachedTransform = transform;

            // Compute frame offset from instance ID for staggered checks.
            // Abs because GetInstanceID can be negative.
            int id = GetInstanceID();
            _frameOffset = (id < 0 ? -id : id) % groundCheckInterval;
        }

        private void OnEnable()
        {
            HectonFluidEngine engine = HectonFluidEngine.Instance;
            if (engine != null)
                engine.Register(this);
        }

        private void OnDisable()
        {
            // Сбрасываем ref-count — объект больше не в зоне
            _dryZoneRefCount = 0;
            _isGrounded = false;

            HectonFluidEngine engine = HectonFluidEngine.Instance;
            if (engine != null)
                engine.Unregister(this);
        }

        // ══════════════════════════════════════════════════════════
        //  FIXED UPDATE — Ground Check Only (staggered)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Lightweight FixedUpdate: only increments a counter and performs
        /// a raycast every N frames. No other logic.
        ///
        /// We use FixedUpdate instead of IFixedTickable to keep this
        /// component self-contained and independent of GameTickManager.
        /// The cost is one integer increment per object per fixed frame,
        /// plus one raycast every groundCheckInterval frames (amortized).
        ///
        /// Zero-GC: no allocations. Cached RaycastHit on the instance.
        /// </summary>
        private void FixedUpdate()
        {
            _frameCounter++;

            // Staggered check: this instance checks on its designated frame
            if ((_frameCounter + _frameOffset) % groundCheckInterval != 0)
                return;

            PerformGroundCheck();
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

            _isGrounded = UnityEngine.Physics.Raycast(
                origin,
                Vector3.down,
                out _groundHit,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (density < 0.01f) density = 0.01f;
            if (volume  < 0.0001f) volume = 0.0001f;
            if (height  < 0f) height = 0f;
            if (currentResponse < 0f) currentResponse = 0f;
            if (surfaceStability < 0f) surfaceStability = 0f;
            if (lodBias < 0.1f) lodBias = 0.1f;
            if (groundCheckDistance < 0.01f) groundCheckDistance = 0.01f;

            // Ensure Water layer is excluded from groundLayers
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer >= 0 && (groundLayers & (1 << waterLayer)) != 0)
            {
                groundLayers &= ~(1 << waterLayer);
                Debug.LogWarning(
                    $"[BuoyancyObject] Removed 'Water' layer from groundLayers on '{gameObject.name}'. " +
                    "Water must be excluded to prevent false ground detection.",
                    this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            float waterY = HectonFluidEngine.Instance != null
                ? HectonFluidEngine.Instance.WaterLevel
                : 5000f;

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
