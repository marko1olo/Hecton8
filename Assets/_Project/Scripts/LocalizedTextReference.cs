using System;
using Hecton8.Core;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// One localized text override for a specific language.
    /// </summary>
    [Serializable]
    public struct LocalizedTextVariant
    {
        [Tooltip("Language for this localized override.")]
        [SerializeField] private GameLanguage language;

        [Tooltip("Localized text for the selected language.")]
        [SerializeField, TextArea(1, 8)] private string text;

        /// <summary>
        /// Language bound to this override.
        /// </summary>
        public GameLanguage Language => language;

        /// <summary>
        /// Localized text content.
        /// </summary>
        public string Text => text ?? string.Empty;
    }

    /// <summary>
    /// Serializable localization reference that can resolve from a table key,
    /// inline per-language overrides, and a legacy fallback string.
    /// </summary>
    [Serializable]
    public struct LocalizedTextReference
    {
        [Tooltip("Optional global localization table key. Used when this text should come from LocalizationManager tables.")]
        [SerializeField] private string tableKey;

        [Tooltip("Fallback text when the table key or localized override is missing.")]
        [SerializeField, TextArea(1, 8)] private string fallbackText;

        [Tooltip("Optional inline per-language overrides. Use for lore/data assets that should travel with the asset.")]
        [SerializeField] private LocalizedTextVariant[] variants;

        /// <summary>
        /// True when this reference contains a table key.
        /// </summary>
        public bool HasTableKey => !string.IsNullOrWhiteSpace(tableKey);

        /// <summary>
        /// True when this reference contains any inline language overrides.
        /// </summary>
        public bool HasVariants => variants != null && variants.Length > 0;

        /// <summary>
        /// Configured fallback text.
        /// </summary>
        public string FallbackText => fallbackText ?? string.Empty;

        /// <summary>
        /// Raw localization table key configured on this reference.
        /// </summary>
        public string TableKey => tableKey ?? string.Empty;

        /// <summary>
        /// Resolve text for the currently active game language.
        /// </summary>
        public string Resolve()
        {
            return ResolveOrFallback(GlobalRegistry.LocalizationTextExpansion, fallbackText);
        }

        /// <summary>
        /// Resolve text through a cached localization owner.
        /// </summary>
        public string Resolve(LocalizationManager manager)
        {
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            return Resolve(language, manager);
        }

        /// <summary>
        /// Resolve text for a specific language.
        /// </summary>
        public string Resolve(GameLanguage language)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return Resolve(language, manager);
        }

        /// <summary>
        /// Resolve text for a specific language through a cached localization owner.
        /// </summary>
        public string Resolve(GameLanguage language, LocalizationManager manager)
        {

            if (TryResolveInline(language, out string inlineValue))
                return manager != null ? manager.ExpandText(inlineValue) : inlineValue;

            if (manager != null && HasTableKey && manager.TryGet(language, tableKey, out string tableValue))
                return manager.ExpandText(tableValue);

            if (!string.IsNullOrWhiteSpace(fallbackText))
                return manager != null ? manager.ExpandText(fallbackText) : fallbackText;

            return HasTableKey ? tableKey : string.Empty;
        }

        /// <summary>
        /// Resolve text, but keep a legacy string as the last fallback.
        /// </summary>
        public string ResolveOrFallback(string legacyFallback)
        {
            string resolved = Resolve();
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;

            return legacyFallback ?? string.Empty;
        }

        /// <summary>
        /// Resolve text through a cached localization owner, but keep a legacy string as the last fallback.
        /// </summary>
        public string ResolveOrFallback(LocalizationManager manager, string legacyFallback)
        {
            string resolved = Resolve(manager);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;

            return legacyFallback ?? string.Empty;
        }

        /// <summary>
        /// Resolve text through a cached localization read model, but keep a legacy string as the last fallback.
        /// </summary>
        public string ResolveOrFallback(ILocalizationTextReadModel manager, string legacyFallback)
        {
            string fallback = !string.IsNullOrWhiteSpace(fallbackText)
                ? fallbackText
                : (legacyFallback ?? string.Empty);

            GameLanguage language = manager != null
                ? (GameLanguage)manager.ActiveLanguageId
                : GameLanguage.English;

            if (TryResolveInline(language, out string inlineValue) && !string.IsNullOrWhiteSpace(inlineValue))
                return inlineValue;

            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return HasTableKey ? tableKey : string.Empty;
        }

        /// <summary>
        /// Resolves text as a span for caller-owned UI buffers.
        /// </summary>
        public ReadOnlySpan<char> ResolveSpanOrFallback(LocalizationManager manager, string legacyFallback)
        {
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            if (TryResolveInlineSpan(language, out ReadOnlySpan<char> inlineValue))
                return inlineValue;

            if (HasTableKey)
            {
                int tableHash = LocHash.Compute(tableKey.AsSpan());
                if (manager != null && manager.TryGetRawBuffer(tableHash, out char[] buffer, out int length))
                    return buffer.AsSpan(0, length);

                if (manager != null && manager.TryGet(language, tableKey, out string tableValue) && !string.IsNullOrWhiteSpace(tableValue))
                    return tableValue.AsSpan();
            }

            if (!string.IsNullOrWhiteSpace(fallbackText))
                return fallbackText.AsSpan();

            if (!string.IsNullOrWhiteSpace(legacyFallback))
                return legacyFallback.AsSpan();

            return HasTableKey ? tableKey.AsSpan() : ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Resolves text as a span through the contract read-model for cross-domain callers.
        /// </summary>
        public ReadOnlySpan<char> ResolveSpanOrFallback(ILocalizationTextReadModel manager, string legacyFallback)
        {
            GameLanguage language = manager != null
                ? (GameLanguage)manager.ActiveLanguageId
                : GameLanguage.English;
            if (TryResolveInlineSpan(language, out ReadOnlySpan<char> inlineValue))
                return inlineValue;

            if (HasTableKey)
            {
                int tableHash = LocHash.Compute(tableKey.AsSpan());
                if (manager != null)
                {
                    ReadOnlySpan<char> tableValue = manager.GetRawSpanOrFallback(tableHash, ReadOnlySpan<char>.Empty);
                    if (!tableValue.IsEmpty)
                        return tableValue;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackText))
                return fallbackText.AsSpan();

            if (!string.IsNullOrWhiteSpace(legacyFallback))
                return legacyFallback.AsSpan();

            return HasTableKey ? tableKey.AsSpan() : ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Resolves, expands, and copies text into caller-owned memory.
        /// </summary>
        public bool TryCopyResolvedOrFallback(LocalizationManager manager, char[] destination, out int length, string legacyFallback)
        {
            length = 0;
            if (destination == null)
                return false;

            ReadOnlySpan<char> source = ResolveSpanOrFallback(manager, legacyFallback);
            if (source.Length == 0)
                return true;

            if (manager != null && manager.TryExpandText(source, destination, out length))
                return true;

            int copyLength = Math.Min(source.Length, destination.Length);
            source.Slice(0, copyLength).CopyTo(destination.AsSpan(0, copyLength));
            length = copyLength;
            return true;
        }

        /// <summary>
        /// Resolves and copies text through the contract read-model into caller-owned memory.
        /// </summary>
        public bool TryCopyResolvedOrFallback(ILocalizationTextReadModel manager, char[] destination, out int length, string legacyFallback)
        {
            length = 0;
            if (destination == null)
                return false;

            ReadOnlySpan<char> source = ResolveSpanOrFallback(manager, legacyFallback);
            if (source.Length == 0)
                return true;

            int copyLength = Math.Min(source.Length, destination.Length);
            source.Slice(0, copyLength).CopyTo(destination.AsSpan(0, copyLength));
            length = copyLength;
            return true;
        }

        /// <summary>
        /// Checks whether the span-resolved text contains any visible non-whitespace character.
        /// </summary>
        public bool HasResolvedOrFallbackText(LocalizationManager manager, string legacyFallback)
        {
            ReadOnlySpan<char> value = ResolveSpanOrFallback(manager, legacyFallback);
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks contract-resolved text for visible non-whitespace content.
        /// </summary>
        public bool HasResolvedOrFallbackText(ILocalizationTextReadModel manager, string legacyFallback)
        {
            ReadOnlySpan<char> value = ResolveSpanOrFallback(manager, legacyFallback);
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        private bool TryResolveInline(GameLanguage language, out string value)
        {
            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i].Language == language && !string.IsNullOrWhiteSpace(variants[i].Text))
                    {
                        value = variants[i].Text;
                        return true;
                    }
                }
            }

            value = string.Empty;
            return false;
        }

        private bool TryResolveInlineSpan(GameLanguage language, out ReadOnlySpan<char> value)
        {
            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    string text = variants[i].Text;
                    if (variants[i].Language == language && !string.IsNullOrWhiteSpace(text))
                    {
                        value = text.AsSpan();
                        return true;
                    }
                }
            }

            value = ReadOnlySpan<char>.Empty;
            return false;
        }
    }
}
