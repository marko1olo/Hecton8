using System;
using Hecton.Localization;
using Hecton8.Core;
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

        [Header("── Hologram ──────────────────────────────")]
        [Tooltip("Proxy-mesh index used by diegetic PDA hologram previews.")]
        [SerializeField] private ushort proxyMeshIndex;

        [Header("Encrypted Fragment Recovery")]
        [Tooltip("Hash for encrypted fragment bit 0. Zero means this bit is not authored.")]
        [SerializeField] private uint encryptedFragmentBit0Hash;

        [Tooltip("Hash for encrypted fragment bit 1. Zero means this bit is not authored.")]
        [SerializeField] private uint encryptedFragmentBit1Hash;

        [Tooltip("Hash for encrypted fragment bit 2. Zero means this bit is not authored.")]
        [SerializeField] private uint encryptedFragmentBit2Hash;

        [Tooltip("Hash for encrypted fragment bit 3. Zero means this bit is not authored.")]
        [SerializeField] private uint encryptedFragmentBit3Hash;

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
        public bool HasSubtitleText => localizedSubtitleText.HasResolvedOrFallbackText(GlobalRegistry.LocalizationText, subtitleText);
        public bool HasPlaybackPayload => HasAudioClip || HasSubtitleText;
        public bool IsTextOnlyPlayback => !HasAudioClip && HasSubtitleText;
        public bool HasArchiveSummary => localizedArchiveSummary.HasResolvedOrFallbackText(
            GlobalRegistry.LocalizationText,
            FallbackOrDefault(archiveSummary, "Entry unavailable."));
        public bool HasVisibleContent => HasPlaybackPayload || HasArchiveSummary;
        public string SafeLogId => string.IsNullOrWhiteSpace(logId) ? "audio_log" : logId;
        public ushort ProxyMeshIndex => proxyMeshIndex;
        public bool IsFragmentedEncrypted =>
            encryptedFragmentBit0Hash != 0u ||
            encryptedFragmentBit1Hash != 0u ||
            encryptedFragmentBit2Hash != 0u ||
            encryptedFragmentBit3Hash != 0u;
        public string DisplayTitleOrFallback => localizedDisplayTitle.ResolveOrFallback(FallbackOrDefault(displayTitle, SafeLogId));
        public string AuthorOrFallback => localizedAuthor.ResolveOrFallback(FallbackOrDefault(author, "UNKNOWN"));
        public string SubtitleOrFallback => localizedSubtitleText.ResolveOrFallback(subtitleText);
        public string VisibleSubtitleOrFallback => StripTimecodedSubtitleMarkup(SubtitleOrFallback);
        public string ArchiveSummaryOrFallback => localizedArchiveSummary.ResolveOrFallback(FallbackOrDefault(archiveSummary, "Entry unavailable."));
        public string RecordDateOrFallback => localizedRecordDate.ResolveOrFallback(FallbackOrDefault(recordDate, "DATE UNKNOWN"));

        public bool TryWriteDisplayTitleOrFallback(char[] destination, out int length)
        {
            return localizedDisplayTitle.TryCopyResolvedOrFallback(
                GlobalRegistry.LocalizationText,
                destination,
                out length,
                FallbackOrDefault(displayTitle, SafeLogId));
        }

        public bool TryWriteAuthorOrFallback(char[] destination, out int length)
        {
            return localizedAuthor.TryCopyResolvedOrFallback(
                GlobalRegistry.LocalizationText,
                destination,
                out length,
                FallbackOrDefault(author, "UNKNOWN"));
        }

        public bool TryWriteArchiveSummaryOrFallback(char[] destination, out int length)
        {
            return localizedArchiveSummary.TryCopyResolvedOrFallback(
                GlobalRegistry.LocalizationText,
                destination,
                out length,
                FallbackOrDefault(archiveSummary, "Entry unavailable."));
        }

        public bool TryWriteRecordDateOrFallback(char[] destination, out int length)
        {
            return localizedRecordDate.TryCopyResolvedOrFallback(
                GlobalRegistry.LocalizationText,
                destination,
                out length,
                FallbackOrDefault(recordDate, "DATE UNKNOWN"));
        }

        public bool TryResolveEncryptedFragmentMask(uint fragmentHash, out uint fragmentBitMask)
        {
            fragmentBitMask = 0u;
            if (fragmentHash == 0u)
                return false;

            if (fragmentHash == encryptedFragmentBit0Hash)
                fragmentBitMask = 1u << 0;
            else if (fragmentHash == encryptedFragmentBit1Hash)
                fragmentBitMask = 1u << 1;
            else if (fragmentHash == encryptedFragmentBit2Hash)
                fragmentBitMask = 1u << 2;
            else if (fragmentHash == encryptedFragmentBit3Hash)
                fragmentBitMask = 1u << 3;

            return fragmentBitMask != 0u;
        }

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public bool TryWriteVisibleSubtitleOrFallback(char[] destination, out int length)
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            ReadOnlySpan<char> subtitle = localizedSubtitleText.ResolveSpanOrFallback(localization, subtitleText);
            if (localization != null && localization.TryExpandText(subtitle, destination, out int expandedLength))
                return TryStripTimecodedSubtitleMarkup(destination.AsSpan(0, expandedLength), destination, out length);

            return TryStripTimecodedSubtitleMarkup(
                subtitle,
                destination,
                out length);
        }

        public static string StripTimecodedSubtitleMarkup(string subtitle)
        {
            if (string.IsNullOrEmpty(subtitle) || subtitle.IndexOf('[', System.StringComparison.Ordinal) < 0)
                return subtitle ?? string.Empty;

            return subtitle;
        }

        public static bool TryStripTimecodedSubtitleMarkup(
            string subtitle,
            char[] destination,
            out int length)
        {
            return TryStripTimecodedSubtitleMarkup(
                string.IsNullOrEmpty(subtitle) ? ReadOnlySpan<char>.Empty : subtitle.AsSpan(),
                destination,
                out length);
        }

        public static bool TryStripTimecodedSubtitleMarkup(
            ReadOnlySpan<char> subtitle,
            char[] destination,
            out int length)
        {
            length = 0;
            if (destination == null || destination.Length == 0)
                return false;

            if (subtitle.Length == 0)
                return true;

            bool removedAny = false;
            for (int i = 0; i < subtitle.Length && length < destination.Length; i++)
            {
                char current = subtitle[i];
                if (current == '[' && TrySkipTimeMarker(subtitle, ref i))
                {
                    removedAny = true;
                    continue;
                }

                destination[length++] = current;
            }

            if (!removedAny)
                length = System.Math.Min(subtitle.Length, destination.Length);

            while (length > 0 && char.IsWhiteSpace(destination[length - 1]))
                length--;

            int leadingWhitespace = 0;
            while (leadingWhitespace < length && char.IsWhiteSpace(destination[leadingWhitespace]))
                leadingWhitespace++;

            if (leadingWhitespace > 0)
            {
                int trimmedLength = length - leadingWhitespace;
                for (int i = 0; i < trimmedLength; i++)
                    destination[i] = destination[i + leadingWhitespace];
                length = trimmedLength;
            }

            return true;
        }

        private static bool TrySkipTimeMarker(ReadOnlySpan<char> text, ref int index)
        {
            int markerStart = index;
            int current = markerStart + 1;
            bool sawDigit = false;
            bool sawDot = false;

            while (current < text.Length)
            {
                char markerChar = text[current];
                if (markerChar >= '0' && markerChar <= '9')
                {
                    sawDigit = true;
                    current++;
                    continue;
                }

                if (markerChar == '.' && !sawDot)
                {
                    sawDot = true;
                    current++;
                    continue;
                }

                break;
            }

            if (!sawDigit || current >= text.Length || text[current] != ']')
                return false;

            index = current;
            return true;
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
