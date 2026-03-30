// ============================================================================
// HECTON-8 — ItemData.cs
// Чистые данные предмета. Никакой логики — только описание.
// Создаётся через контекстное меню: Hecton → Item Data.
// ============================================================================

namespace Hecton8.Items
{
    using Hecton8.Physics;
    using UnityEngine;
    /// <summary>
    /// Категория предмета. Используется для фильтрации,
    /// отображения в UI и будущих крафт-правил.
    /// </summary>
    public enum ItemCategory
    {
        Miscellaneous = 0,
        Material      = 1,
        Tool          = 2,
        Equipment     = 3,
        Consumable    = 4,
        Component     = 5
    }

    public enum ResourceFamily
    {
        None              = 0,
        StructuralMetal   = 1,
        ElectronicsMetal  = 2,
        Chemical          = 3,
        Organic           = 4,
        Crystal           = 5,
        DeepMaterial      = 6,
        Component         = 7,
        Power             = 8
    }

    public enum ProgressionTier
    {
        None  = 0,
        Tier0 = 1,
        Tier1 = 2,
        Tier2 = 3,
        Tier3 = 4
    }
    /// <summary>
    /// Чистые данные предмета. Никакой логики — только описание.
    /// Создаётся через контекстное меню: Hecton → Item Data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewItem",
        menuName = "Hecton/Item Data",
        order    = 0)]
    public class ItemData : ScriptableObject
    {
        // ─────────────────────── Identity ────────────────────────
        [Header("Identity")]
        public string     itemName    = "Unnamed Item";
        public Sprite     icon;
        [TextArea(2, 5)]
        public string     description = "";

        // ─────────────────────── Properties ──────────────────────
        [Header("Properties")]
        public float      weight    = 1f;
        public bool       stackable = true;
        public int        maxStack  = 64;
        [Header("Classification")]
        [Tooltip("Категория предмета для UI фильтрации и крафт-правил")]
        public ItemCategory category = ItemCategory.Miscellaneous;
        [Tooltip("Семейство ресурса/компонента для экономики, скан-логики и будущего крафт-дерева")]
        public ResourceFamily resourceFamily = ResourceFamily.None;
        [Tooltip("Прогрессионный уровень: ранний, средний, поздний, глубоководный")]
        public ProgressionTier progressionTier = ProgressionTier.None;
        [Tooltip("Если true — это базовый добываемый ресурс мира, а не собранный компонент")]
        public bool isRawResource = false;

        // ─────────────────────── Grid ────────────────────────────
        // ◆ NEW — габариты для тетрис-инвентаря
        [Header("Grid")]
        [Tooltip("Ширина предмета в ячейках сетки инвентаря (≥ 1)")]
        public int        width  = 1;
        [Tooltip("Высота предмета в ячейках сетки инвентаря (≥ 1)")]
        public int        height = 1;

        // ─────────────────────── Consumable ──────────────────────
        [Header("Consumable")]
        [Tooltip("Можно ли использовать (потребить) этот предмет")]
        public bool isConsumable = false;

        [Tooltip("Количество кислорода при использовании")]
        public float oxygenRestore = 0f;

        [Tooltip("Количество энергии при использовании")]
        public float energyRestore = 0f;

        [Tooltip("Количество прочности костюма при использовании")]
        public float integrityRestore = 0f;

        [Tooltip("Звук при использовании")]
        public AudioClip useSound;

        // ─────────────────────── Interaction ─────────────────────
        [Header("Interaction")]
        [Tooltip("Глагол для подсказки: 'Забрать', 'Подобрать', 'Взять'")]
        public string     interactVerb = "Забрать";

        // ─────────────────────── World ───────────────────────────
        [Header("World")]
        [Tooltip("Префаб для выбрасывания в мир (опционально)")]
        public GameObject worldPrefab;
        [Tooltip("Profile applied to worldPrefab buoyancy when this item exists in the world.")]
        public BuoyancyProfile worldBuoyancyProfile;

        // ─────────────────────── Cache ───────────────────────────
        // Built once in OnEnable — zero allocation at runtime.
        private string _cachedInteractText;

        // ═════════════════════════════════════════════════════════
        // ScriptableObject Lifecycle
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Called by Unity when the asset is loaded into memory.
        /// Builds the interact-text cache exactly once per session.
        /// </summary>
        private void OnEnable()
        {
            RebuildCache();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: rebuilds the cache whenever a field is changed
        /// in the Inspector so GetInteractText() stays accurate during
        /// design time without waiting for a domain reload.
        /// Also clamps grid dimensions to sane minimums.
        /// Stripped from builds — zero overhead in production.
        /// </summary>
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            // ◆ NEW — не допускаем нулевые/отрицательные габариты
            if (width  < 1) width  = 1;
            if (height < 1) height = 1;

            RebuildCache();
        }
#endif

        // ═════════════════════════════════════════════════════════
        // Public API
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the pre-built interaction prompt.
        /// Zero allocation — safe to call every frame.
        /// Example: "Забрать Медаптечка"
        /// </summary>
        /// <returns>Cached string: "{interactVerb} {itemName}"</returns>
        public string GetInteractText()
        {
            // Defensive fallback: if somehow called before OnEnable fires
            // (e.g. direct asset instantiation in tests), rebuild on demand.
            if (string.IsNullOrEmpty(_cachedInteractText))
                RebuildCache();

            return _cachedInteractText;
        }

        /// <summary>
        /// ◆ NEW — Площадь предмета в ячейках сетки.
        /// Удобно для быстрых проверок вместимости.
        /// </summary>
        public int CellArea => width * height;

        // ═════════════════════════════════════════════════════════
        // Private Helpers
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Single source of truth for the cached string.
        /// One allocation, stored for the lifetime of the asset.
        /// </summary>
        private void RebuildCache()
        {
            _cachedInteractText = $"{interactVerb} {itemName}";
        }

        // ─────────────────────── Future Extensions ───────────────
        // Готово к интеграции с инвентарём:
        // public ItemCategory category;
        // public ItemRarity   rarity;
        // public AudioClip    pickupSound;
    }
}
