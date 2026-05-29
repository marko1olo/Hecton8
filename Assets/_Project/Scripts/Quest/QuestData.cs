using System;
using Hecton.Localization;
using Hecton8.Core;
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
        OnCraftCompleted = 7,
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
        OnCraftCompleted = 6,
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

        [Header("── Graph ────────────────────────────────")]
        [Tooltip("Quest IDs that must already be completed before this quest may activate.")]
        [SerializeField] public string[] prerequisiteQuestIds = Array.Empty<string>();

        [Tooltip("Critical item persistent ID that reverts this quest if lost after completion. Leave empty to infer from completionId when completionType is OnItemCollected.")]
        [SerializeField] public string criticalItemId;

        [Tooltip("Stable respawn event ID raised when a critical item must be re-spawned after destruction or discard.")]
        [SerializeField] public string respawnEventId;

        [Tooltip("Optional phase gate that must already be unlocked before this quest may activate.")]
        [SerializeField] public QuestPhaseGateType phaseGate = QuestPhaseGateType.None;

        [Header("── Marker ───────────────────────────")]
        [Tooltip("Stable world marker target ID resolved through the quest marker runtime. Leave empty to use markerWorldPosition.")]
        [SerializeField] public string markerTargetId;

        [Tooltip("Fallback world-space marker position used when markerTargetId is empty or unresolved.")]
        [SerializeField] public Vector3 markerWorldPosition;

        [Tooltip("Vertical marker lift above the resolved world-space position.")]
        [SerializeField, Min(0f)] public float markerHeightOffset = 6f;

        [Header("── Flags ────────────────────────────────")]
        [SerializeField] public bool autoActivateOnStart;
        [SerializeField] public bool oneTimeOnly = true;

        [NonSerialized] private int _invalidPrerequisiteCount;
        [NonSerialized] private int _duplicatePrerequisiteCount;
        [NonSerialized] private int _firstInvalidPrerequisiteIndex = -1;
        [NonSerialized] private int _firstDuplicatePrerequisiteIndex = -1;

        /// <summary>
        /// Localized display title for the active language.
        /// </summary>
        public string DisplayTitleOrFallback => localizedDisplayTitle.ResolveOrFallback(FallbackOrDefault(displayTitle, "UNKNOWN OBJECTIVE"));

        /// <summary>
        /// Localized description for the active language.
        /// </summary>
        public string DescriptionOrFallback => localizedDescription.ResolveOrFallback(description);

        /// <summary>
        /// Finite, non-negative trigger threshold used by runtime quest graph compilation.
        /// </summary>
        public float RuntimeTriggerValue => SanitizeFiniteNonNegative(triggerValue, 0f);

        /// <summary>
        /// Finite, non-negative completion threshold used by runtime quest graph compilation.
        /// </summary>
        public float RuntimeCompletionValue => SanitizeFiniteNonNegative(completionValue, 0f);

        /// <summary>
        /// Finite marker fallback position used when no marker target resolves.
        /// </summary>
        public Vector3 RuntimeMarkerWorldPosition => new Vector3(
            SanitizeFinite(markerWorldPosition.x, 0f),
            SanitizeFinite(markerWorldPosition.y, 0f),
            SanitizeFinite(markerWorldPosition.z, 0f));

        /// <summary>
        /// Finite, non-negative marker lift used by runtime marker presentation.
        /// </summary>
        public float RuntimeMarkerHeightOffset => SanitizeFiniteNonNegative(markerHeightOffset, 6f);

        /// <summary>
        /// Authored prerequisite slot count, including invalid rows preserved for designer repair.
        /// </summary>
        public int PrerequisiteSlotCount => prerequisiteQuestIds != null ? prerequisiteQuestIds.Length : 0;

        /// <summary>
        /// Number of blank prerequisite slots detected during cold validation.
        /// </summary>
        public int InvalidPrerequisiteCount => _invalidPrerequisiteCount;

        /// <summary>
        /// Number of duplicate prerequisite slots detected during cold validation.
        /// </summary>
        public int DuplicatePrerequisiteCount => _duplicatePrerequisiteCount;

        /// <summary>
        /// First blank prerequisite slot index, or -1 when none is present.
        /// </summary>
        public int FirstInvalidPrerequisiteIndex => _firstInvalidPrerequisiteIndex;

        /// <summary>
        /// First duplicate prerequisite slot index, or -1 when none is present.
        /// </summary>
        public int FirstDuplicatePrerequisiteIndex => _firstDuplicatePrerequisiteIndex;

        /// <summary>
        /// True when prerequisite authoring contains blank or duplicate rows.
        /// </summary>
        public bool HasPrerequisiteValidationErrors => _invalidPrerequisiteCount > 0 || _duplicatePrerequisiteCount > 0;

        public bool TryWriteDisplayTitleOrFallback(ILocalizationTextReadModel manager, char[] destination, out int length)
        {
            return localizedDisplayTitle.TryCopyResolvedOrFallback(
                manager,
                destination,
                out length,
                FallbackOrDefault(displayTitle, "UNKNOWN OBJECTIVE"));
        }

        public bool TryWriteDescriptionOrFallback(ILocalizationTextReadModel manager, char[] destination, out int length)
        {
            return localizedDescription.TryCopyResolvedOrFallback(
                manager,
                destination,
                out length,
                description);
        }

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private void OnEnable()
        {
            RebuildValidationCache();
        }

        private void RebuildValidationCache()
        {
            _invalidPrerequisiteCount = 0;
            _duplicatePrerequisiteCount = 0;
            _firstInvalidPrerequisiteIndex = -1;
            _firstDuplicatePrerequisiteIndex = -1;

            if (prerequisiteQuestIds == null)
                return;

            for (int i = 0; i < prerequisiteQuestIds.Length; i++)
            {
                string prerequisiteQuestId = prerequisiteQuestIds[i];
                if (string.IsNullOrWhiteSpace(prerequisiteQuestId))
                {
                    _invalidPrerequisiteCount++;
                    if (_firstInvalidPrerequisiteIndex < 0)
                        _firstInvalidPrerequisiteIndex = i;

                    continue;
                }

                for (int previousIndex = 0; previousIndex < i; previousIndex++)
                {
                    string previousQuestId = prerequisiteQuestIds[previousIndex];
                    if (string.IsNullOrWhiteSpace(previousQuestId))
                        continue;

                    if (!string.Equals(previousQuestId, prerequisiteQuestId, StringComparison.Ordinal))
                        continue;

                    _duplicatePrerequisiteCount++;
                    if (_firstDuplicatePrerequisiteIndex < 0)
                        _firstDuplicatePrerequisiteIndex = i;

                    break;
                }
            }
        }

        private static float SanitizeFiniteNonNegative(float value, float fallback)
        {
            value = SanitizeFinite(value, fallback);
            return value < 0f ? 0f : value;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(questId))
                questId = name.ToLowerInvariant().Replace(" ", "_");

            questId = NormalizeToken(questId);
            triggerId = NormalizeToken(triggerId);
            completionId = NormalizeToken(completionId);
            criticalItemId = NormalizeToken(criticalItemId);
            respawnEventId = NormalizeToken(respawnEventId);
            markerTargetId = NormalizeToken(markerTargetId);
            triggerValue = RuntimeTriggerValue;
            completionValue = RuntimeCompletionValue;
            markerWorldPosition = RuntimeMarkerWorldPosition;
            markerHeightOffset = RuntimeMarkerHeightOffset;

            if (prerequisiteQuestIds == null)
                prerequisiteQuestIds = Array.Empty<string>();

            for (int i = 0; i < prerequisiteQuestIds.Length; i++)
                prerequisiteQuestIds[i] = NormalizeToken(prerequisiteQuestIds[i]);

            RebuildValidationCache();
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
#endif
    }
}
