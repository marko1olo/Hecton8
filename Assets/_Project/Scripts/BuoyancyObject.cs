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
// СУХИЕ ЗОНЫ:
//   IsInAir — устанавливается BaseModule, когда объект находится внутри
//   незатопленного модуля. При IsInAir == true HectonFluidEngine
//   обнуляет все силы плавучести/сопротивления для этого объекта.
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

        // ══════════════════════════════════════════════════════════
        //  CACHED
        // ══════════════════════════════════════════════════════════

        private Rigidbody _rb;

        // ══════════════════════════════════════════════════════════
        //  DRY ZONE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Счётчик вложенности «сухих зон».
        /// Объект может находиться в перекрывающихся модулях одновременно.
        /// IsInAir == true, когда _dryZoneRefCount > 0.
        ///
        /// Инкремент: BaseModule при входе в незатопленный триггер.
        /// Декремент: BaseModule при выходе или затоплении.
        ///
        /// Использование ref-count вместо bool предотвращает баг,
        /// когда выход из одного модуля сбрасывает флаг,
        /// хотя объект всё ещё внутри другого.
        /// </summary>
        private int _dryZoneRefCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Плотность объекта (кг/м³).</summary>
        public float Density => density;

        /// <summary>Объём (м³).</summary>
        public float Volume => volume;

        /// <summary>Высота (м).</summary>
        public float Height => height;

        /// <summary>Кэшированный Rigidbody. Гарантированно не-null (RequireComponent).</summary>
        public Rigidbody Body => _rb;

        /// <summary>
        /// Объект находится «в воздухе» — внутри незатопленного модуля базы.
        /// Когда true, HectonFluidEngine обнуляет все водные силы.
        /// Устанавливается через EnterDryZone / ExitDryZone.
        /// </summary>
        public bool IsInAir => _dryZoneRefCount > 0;

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

            HectonFluidEngine engine = HectonFluidEngine.Instance;
            if (engine != null)
                engine.Unregister(this);
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
        }

        private void OnDrawGizmosSelected()
        {
            float waterY = HectonFluidEngine.Instance != null
                ? HectonFluidEngine.Instance.WaterLevel
                : 5000f;

            bool submerged = transform.position.y < waterY;

            // Зелёный = в сухой зоне, синий = под водой, жёлтый = над водой
            if (IsInAir)
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            else if (submerged)
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            else
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);

            Gizmos.DrawWireSphere(transform.position, Mathf.Pow(volume, 1f / 3f));
        }
#endif
    }
}