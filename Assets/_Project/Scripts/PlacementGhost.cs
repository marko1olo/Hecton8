// ============================================================================
// HECTON-8 — PlacementGhost.cs
// Скрипт призрака постройки (Building Ghost).
//
// Висит на ghostPrefab. Отвечает за:
//   1. Проверку коллизий (можно ли строить в данной позиции)
//   2. Визуальную индикацию (зелёный / красный материал)
//
// АРХИТЕКТУРА:
//   • IFixedTickable — проверка коллизий в физическом шаге.
//   • IPoolable — корректная работа с ObjectPoolManager.
//   • Physics.OverlapBoxNonAlloc — zero GC, с фильтрацией
//     собственных коллайдеров.
//   • Материал меняется ТОЛЬКО при смене состояния (не каждый кадр).
//
// НАСТРОЙКА ПРЕФАБА:
//   1. Поставь призрак на слой "BuildGhost" (или любой, исключённый
//      из blockingMask).
//   2. Все коллайдеры на призраке — isTrigger = true (не мешают физике).
//   3. Назначь validMaterial (зелёный) и invalidMaterial (красный).
//   4. Настрой checkHalfExtents под размер модуля.
// ============================================================================

using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    public sealed class PlacementGhost : MonoBehaviour, IFixedTickable, IPoolable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Materials ─────────────────────────────────")]
        [Tooltip("Полупрозрачный зелёный — строить можно")]
        [SerializeField] private Material validMaterial;

        [Tooltip("Полупрозрачный красный — строить нельзя")]
        [SerializeField] private Material invalidMaterial;

        [Header("── Collision Check ───────────────────────────")]
        [Tooltip("Полуразмеры проверочного бокса (half extents). " +
                 "Настрой под габариты модуля.")]
        [SerializeField] private Vector3 checkHalfExtents = new Vector3(1f, 0.5f, 1f);

        [Tooltip("Смещение центра проверочного бокса от pivot'а объекта")]
        [SerializeField] private Vector3 checkCenterOffset = Vector3.zero;

        [Tooltip("Слои, которые БЛОКИРУЮТ строительство. " +
                 "Исключи слой призрака!")]
        [SerializeField] private LayerMask blockingMask = ~0;

        [Tooltip("Немного уменьшить бокс, чтобы модули могли " +
                 "соприкасаться стенками (snap)")]
        [SerializeField] private float checkShrink = 0.02f;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Кэш рендереров для смены материала. Заполняется один раз в Awake.</summary>
        private Renderer[] _renderers;

        /// <summary>Кэш собственных коллайдеров для фильтрации OverlapBox.</summary>
        private Collider[] _ownColliders;

        /// <summary>Текущее состояние: можно ли строить.</summary>
        private bool _canBuild = true;

        /// <summary>Последнее применённое визуальное состояние. Предотвращает повторную смену материала.</summary>
        private bool _lastVisualState = true;

        /// <summary>
        /// Статический буфер для OverlapBoxNonAlloc.
        /// 32 коллайдера — более чем достаточно.
        /// Shared across all instances (one ghost active at a time).
        /// </summary>
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true = пространство свободно, можно ставить модуль.
        /// Читается PlayerBuilder при нажатии ЛКМ.
        /// </summary>
        public bool CanBuild => _canBuild;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Кэш рендереров (один раз за жизнь объекта) ──
            _renderers = GetComponentsInChildren<Renderer>(true);

            // ── Кэш собственных коллайдеров для фильтрации ──
            _ownColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((IFixedTickable)this);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((IFixedTickable)this);
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — жизненный цикл пула
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается при извлечении из пула.
        /// Сбрасывает визуальное состояние в "можно строить".
        /// </summary>
        public void OnSpawn()
        {
            _canBuild       = true;
            _lastVisualState = true;
            ApplyMaterial(validMaterial);
        }

        /// <summary>
        /// Вызывается при возврате в пул.
        /// Сбрасывает флаги.
        /// </summary>
        public void OnDespawn()
        {
            _canBuild        = false;
            _lastVisualState = false;
        }

        // ══════════════════════════════════════════════════════════
        //  IFixedTickable — проверка коллизий (физический шаг)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет, свободно ли пространство для размещения модуля.
        ///
        /// Использует OverlapBoxNonAlloc (zero GC) с фильтрацией
        /// собственных коллайдеров.
        ///
        /// Материал обновляется ТОЛЬКО при смене состояния can → cannot
        /// или наоборот (не каждый FixedTick).
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            // ── Параметры проверочного бокса ──
            Vector3    center     = transform.TransformPoint(checkCenterOffset);
            Vector3    halfExt    = checkHalfExtents - Vector3.one * checkShrink;
            Quaternion rotation   = transform.rotation;

            // ── OverlapBoxNonAlloc: заполняет статический буфер ──
            int overlapCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                center,
                halfExt,
                OverlapBuffer,
                rotation,
                blockingMask,
                QueryTriggerInteraction.Ignore);

            // ── Фильтрация: исключаем собственные коллайдеры ──
            bool blocked = false;
            for (int i = 0; i < overlapCount; i++)
            {
                if (!IsOwnCollider(OverlapBuffer[i]))
                {
                    blocked = true;
                    break; // достаточно одного чужого коллайдера
                }
            }

            _canBuild = !blocked;

            // ── Обновление визуала (только при смене состояния) ──
            if (_canBuild != _lastVisualState)
            {
                _lastVisualState = _canBuild;
                ApplyMaterial(_canBuild ? validMaterial : invalidMaterial);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет, принадлежит ли коллайдер этому призраку.
        /// Линейный поиск по кэшированному массиву.
        /// Для 1-5 коллайдеров это быстрее HashSet (cache locality).
        /// Zero GC: ReferenceEquals, без boxing.
        /// </summary>
        private bool IsOwnCollider(Collider col)
        {
            for (int i = 0, len = _ownColliders.Length; i < len; i++)
            {
                if (ReferenceEquals(_ownColliders[i], col))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Применяет материал ко всем рендерерам призрака.
        /// sharedMaterial — не создаёт инстансов, zero GC.
        /// Безопасно для пулированных объектов: меняет ссылку, не ассет.
        /// </summary>
        private void ApplyMaterial(Material mat)
        {
            if (mat == null) return;

            for (int i = 0, len = _renderers.Length; i < len; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].sharedMaterial = mat;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR — GIZMOS
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center  = transform.TransformPoint(checkCenterOffset);
            Vector3 halfExt = checkHalfExtents - Vector3.one * checkShrink;

            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.color  = _canBuild
                ? new Color(0f, 1f, 0f, 0.25f)
                : new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, halfExt * 2f);
            Gizmos.DrawWireCube(Vector3.zero, halfExt * 2f);
        }
#endif
    }
}