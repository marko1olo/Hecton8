using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Scannable Target")]
    public sealed class ScannableTarget : MonoBehaviour
    {
        [SerializeField] private string entryId = "scannable.unknown";
        [SerializeField] private string entryTitle = "UNIDENTIFIED CONTACT";
        [SerializeField] private string entryCategory = "Unknown";
        [TextArea(2, 5)]
        [SerializeField] private string entrySummary =
            "Passive scan profile has been captured. Manual classification pending.";

        public string EntryId => string.IsNullOrWhiteSpace(entryId) ? gameObject.name : entryId;
        public string EntryTitle => string.IsNullOrWhiteSpace(entryTitle) ? CachedToUpperInvariant(gameObject.name) : entryTitle;
        public string EntryCategory => string.IsNullOrWhiteSpace(entryCategory) ? "Unknown" : entryCategory;
        public string EntrySummary => string.IsNullOrWhiteSpace(entrySummary)
            ? "Passive scan profile has been captured."
            : entrySummary;

        public void Configure(string id, string title, string category, string summary)
        {
            entryId = string.IsNullOrWhiteSpace(id) ? gameObject.name : id.Trim();
            entryTitle = string.IsNullOrWhiteSpace(title) ? CachedToUpperInvariant(gameObject.name) : title.Trim();
            entryCategory = string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim();
            entrySummary = string.IsNullOrWhiteSpace(summary)
                ? "Passive scan profile has been captured."
                : summary.Trim();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (string.IsNullOrWhiteSpace(entryId))
                entryId = gameObject.name.Trim().ToLowerInvariant().Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(entryTitle))
                entryTitle = CachedToUpperInvariant(gameObject.name);

            if (string.IsNullOrWhiteSpace(entryCategory))
                entryCategory = "Unknown";
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static readonly string[] _upperCache = new string[16];
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            int hash = input.GetHashCode() & 0xF;
            string cached = _upperCache[hash];
            if (cached != null && cached == input)
                return cached.ToUpperInvariant();

            _upperCache[hash] = input;
            return input.ToUpperInvariant();
        }
    }
}
