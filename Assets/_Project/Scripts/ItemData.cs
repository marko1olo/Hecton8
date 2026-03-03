// ============================================================================
// HECTON-8 — ItemData.cs
// Чистые данные предмета. Никакой логики — только описание.
// Создаётся через контекстное меню: Hecton → Item Data.
// ============================================================================

namespace Hecton8.Items
{
    using UnityEngine;

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

        // ─────────────────────── Interaction ─────────────────────
        [Header("Interaction")]
        [Tooltip("Глагол для подсказки: 'Забрать', 'Подобрать', 'Взять'")]
        public string     interactVerb = "Забрать";

        // ─────────────────────── World ───────────────────────────
        [Header("World")]
        [Tooltip("Префаб для выбрасывания в мир (опционально)")]
        public GameObject worldPrefab;

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
        /// Stripped from builds — zero overhead in production.
        /// </summary>
        private void OnValidate()
        {
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