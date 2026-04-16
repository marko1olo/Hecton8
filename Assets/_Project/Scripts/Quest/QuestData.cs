using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Quest
{
    /// <summary>
    /// Activation trigger for a quest asset.
    /// </summary>
    public enum QuestTriggerType
    {
        OnItemCollected = 0,
        OnDepthReached = 1,
        OnBiomeEntered = 2,
        OnDiscoveryMade = 3,
        OnAudioLogFound = 4,
        OnEclipseStart = 5,
        OnSignalDetected = 6,
        Manual = 99,
    }

    /// <summary>
    /// Completion condition for a quest asset.
    /// </summary>
    public enum QuestCompletionType
    {
        OnItemCollected = 0,
        OnDepthReached = 1,
        OnBiomeEntered = 2,
        OnDiscoveryMade = 3,
        OnAudioLogFound = 4,
        OnSignalDecoded = 5,
        Manual = 99,
    }

    [CreateAssetMenu(fileName = "Quest_", menuName = "Hecton8/Quest/Quest Data", order = 20)]
    public sealed class QuestData : ScriptableObject
    {
        [Header("── Identity ─────────────────────────────")]
        [Tooltip("Unique quest ID.")]
        [SerializeField] public string questId;

        [Tooltip("Legacy display title fallback.")]
        [SerializeField] public string displayTitle = "UNKNOWN OBJECTIVE";

        [Tooltip("Localized quest title.")]
        [SerializeField] private LocalizedTextReference localizedDisplayTitle;

        [Tooltip("Legacy PDA description fallback.")]
        [SerializeField, TextArea(2, 5)] public string description;

        [Tooltip("Localized PDA description.")]
        [SerializeField] private LocalizedTextReference localizedDescription;

        [Header("── Activation ───────────────────────────")]
        [SerializeField] public QuestTriggerType triggerType = QuestTriggerType.Manual;
        [SerializeField] public string triggerId;
        [SerializeField] public float triggerValue;

        [Header("── Completion ───────────────────────────")]
        [SerializeField] public QuestCompletionType completionType = QuestCompletionType.Manual;
        [SerializeField] public string completionId;
        [SerializeField] public float completionValue;

        [Header("── Flags ────────────────────────────────")]
        [SerializeField] public bool autoActivateOnStart;
        [SerializeField] public bool oneTimeOnly = true;

        /// <summary>
        /// Localized display title for the active language.
        /// </summary>
        public string DisplayTitleOrFallback => localizedDisplayTitle.ResolveOrFallback(FallbackOrDefault(displayTitle, "UNKNOWN OBJECTIVE"));

        /// <summary>
        /// Localized description for the active language.
        /// </summary>
        public string DescriptionOrFallback => localizedDescription.ResolveOrFallback(description);

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(questId))
                questId = name.ToLower().Replace(" ", "_");
        }
#endif
    }
}
