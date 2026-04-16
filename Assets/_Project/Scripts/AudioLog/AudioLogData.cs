using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Audio log archive category used by PDA and HUD consumers.
    /// </summary>
    public enum AudioLogCategory
    {
        Personal = 0,
        Technical = 1,
        Emergency = 2,
        Atlas6 = 3,
        Unknown = 4,
    }

    [CreateAssetMenu(fileName = "AudioLog_", menuName = "Hecton8/Narrative/Audio Log Data", order = 10)]
    public sealed class AudioLogData : ScriptableObject
    {
        [Header("── Identity ─────────────────────────────")]
        [Tooltip("Unique log ID used by save data and narrative triggers.")]
        [SerializeField] public string logId;

        [Tooltip("Legacy title fallback kept for backward-compatible assets.")]
        [SerializeField] public string displayTitle = "UNKNOWN LOG";

        [Tooltip("Localized title source. Use this for release localization.")]
        [SerializeField] private LocalizedTextReference localizedDisplayTitle;

        [Tooltip("Legacy author fallback kept for backward-compatible assets.")]
        [SerializeField] public string author = "UNKNOWN";

        [Tooltip("Localized author/source line.")]
        [SerializeField] private LocalizedTextReference localizedAuthor;

        [Tooltip("Category used by archive filters and UI color coding.")]
        [SerializeField] public AudioLogCategory category = AudioLogCategory.Unknown;

        [Header("── Content ──────────────────────────────")]
        [Tooltip("Legacy default clip used when no localized voice clip exists.")]
        [SerializeField] public AudioClip audioClip;

        [Tooltip("Localized voice clip overrides. Optional.")]
        [SerializeField] private LocalizedAudioClipSet localizedAudioClips;

        [Tooltip("Legacy subtitle fallback kept for backward-compatible assets.")]
        [SerializeField, TextArea(3, 8)] public string subtitleText;

        [Tooltip("Localized subtitle/body text.")]
        [SerializeField] private LocalizedTextReference localizedSubtitleText;

        [Tooltip("Playback duration override in seconds. Uses resolved clip length when zero.")]
        [SerializeField] public float durationOverride;

        [Header("── Lore ─────────────────────────────────")]
        [Tooltip("Legacy PDA archive summary fallback.")]
        [SerializeField, TextArea(2, 4)] public string archiveSummary;

        [Tooltip("Localized PDA archive summary.")]
        [SerializeField] private LocalizedTextReference localizedArchiveSummary;

        [Tooltip("Legacy in-world record date fallback.")]
        [SerializeField] public string recordDate = "DATE UNKNOWN";

        [Tooltip("Localized record date.")]
        [SerializeField] private LocalizedTextReference localizedRecordDate;

        /// <summary>
        /// Playback duration using override first, then the resolved localized clip length.
        /// </summary>
        public float Duration
        {
            get
            {
                if (durationOverride > 0f)
                    return durationOverride;

                AudioClip resolvedClip = ResolvedAudioClip;
                return resolvedClip != null ? resolvedClip.length : 0f;
            }
        }

        /// <summary>
        /// Clip resolved for the current runtime language.
        /// </summary>
        public AudioClip ResolvedAudioClip
        {
            get
            {
                AudioClip localizedClip = localizedAudioClips.Resolve();
                return localizedClip != null ? localizedClip : audioClip;
            }
        }

        public bool HasAudioClip => ResolvedAudioClip != null;
        public bool HasSubtitleText => !string.IsNullOrWhiteSpace(SubtitleOrFallback);
        public bool HasPlaybackPayload => HasAudioClip || HasSubtitleText;
        public bool IsTextOnlyPlayback => !HasAudioClip && HasSubtitleText;
        public bool HasArchiveSummary => !string.IsNullOrWhiteSpace(ArchiveSummaryOrFallback);
        public bool HasVisibleContent => HasPlaybackPayload || HasArchiveSummary;
        public string SafeLogId => string.IsNullOrWhiteSpace(logId) ? "audio_log" : logId;
        public string DisplayTitleOrFallback => localizedDisplayTitle.ResolveOrFallback(FallbackOrDefault(displayTitle, SafeLogId));
        public string AuthorOrFallback => localizedAuthor.ResolveOrFallback(FallbackOrDefault(author, "UNKNOWN"));
        public string SubtitleOrFallback => localizedSubtitleText.ResolveOrFallback(subtitleText);
        public string ArchiveSummaryOrFallback => localizedArchiveSummary.ResolveOrFallback(FallbackOrDefault(archiveSummary, "Entry unavailable."));
        public string RecordDateOrFallback => localizedRecordDate.ResolveOrFallback(FallbackOrDefault(recordDate, "DATE UNKNOWN"));

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(logId))
                logId = name.ToLowerInvariant().Replace(" ", "_");

            if (string.IsNullOrWhiteSpace(displayTitle))
                displayTitle = name;

            if (string.IsNullOrWhiteSpace(author))
                author = "UNKNOWN";

            if (durationOverride < 0f)
                durationOverride = 0f;
        }
#endif
    }
}
