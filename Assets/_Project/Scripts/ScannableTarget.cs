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
        public string EntryTitle => string.IsNullOrWhiteSpace(entryTitle) ? gameObject.name.ToUpperInvariant() : entryTitle;
        public string EntryCategory => string.IsNullOrWhiteSpace(entryCategory) ? "Unknown" : entryCategory;
        public string EntrySummary => string.IsNullOrWhiteSpace(entrySummary)
            ? "Passive scan profile has been captured."
            : entrySummary;

        public void Configure(string id, string title, string category, string summary)
        {
            entryId = string.IsNullOrWhiteSpace(id) ? gameObject.name : id.Trim();
            entryTitle = string.IsNullOrWhiteSpace(title) ? gameObject.name.ToUpperInvariant() : title.Trim();
            entryCategory = string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim();
            entrySummary = string.IsNullOrWhiteSpace(summary)
                ? "Passive scan profile has been captured."
                : summary.Trim();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(entryId))
                entryId = gameObject.name.Trim().ToLowerInvariant().Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(entryTitle))
                entryTitle = gameObject.name.ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(entryCategory))
                entryCategory = "Unknown";
        }
#endif
    }
}
