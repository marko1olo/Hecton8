using System;
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
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return Resolve(manager);
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
    }
}
