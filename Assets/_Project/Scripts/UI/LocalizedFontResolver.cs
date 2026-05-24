using System;
using Hecton.Localization;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Shared cold-path font resolver for readable localized TMP text.
    /// </summary>
    public static class LocalizedFontResolver
    {
        private const string PrimaryPdaFontName = "\u0442\u0435\u043A\u0441\u0442 SDF";
        private const string TextFontToken = "\u0442\u0435\u043A\u0441\u0442";
        private const string NumericFontToken = "\u0446\u0438\u0444";
        private const string CjkScFontToken = "notosanscjksc";
        private const string CjkJpFontToken = "notosanscjkjp";
        private const string ArabicFontToken = "arabic";

        private static TMP_FontAsset _cachedReadableFont;
        private static TMP_FontAsset _cachedReadableFontCjkSc;
        private static TMP_FontAsset _cachedReadableFontCjkJp;
        private static TMP_FontAsset _cachedReadableFontArabic;
        private static TMP_FontAsset _cachedBiosFallbackFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedReadableFont = null;
            _cachedReadableFontCjkSc = null;
            _cachedReadableFontCjkJp = null;
            _cachedReadableFontArabic = null;
            _cachedBiosFallbackFont = null;
        }

        /// <summary>
        /// Resolve a readable text font, rejecting digit-only TMP assets.
        /// </summary>
        public static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred)
        {
            LocalizationManager manager = LocalizationManager.ActiveRuntimeInstance;
            return ResolveReadableFont(preferred, manager);
        }

        /// <summary>
        /// Resolve a readable text font through a cached localization owner.
        /// </summary>
        public static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred, LocalizationManager manager)
        {
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            return ResolveReadableFontForLanguage(preferred, language);
        }

        /// <summary>
        /// Resolve a readable text font for a specific language.
        /// </summary>
        public static TMP_FontAsset ResolveReadableFontForLanguage(TMP_FontAsset preferred, GameLanguage language)
        {
            if (preferred != null &&
                !IsNumericOnlyFont(preferred) &&
                FontSupportsLanguage(preferred, language))
            {
                return preferred;
            }

            TMP_FontAsset cached = GetCachedLanguageFont(language);
            if (cached != null)
                return cached;

            CacheLanguageFonts(preferred);
            TMP_FontAsset resolved = GetCachedLanguageFont(language);
            return resolved != null ? resolved : TMP_Settings.defaultFontAsset;
        }

        /// <summary>
        /// Resolve a numeric-capable font, falling back to the readable label font when needed.
        /// </summary>
        public static TMP_FontAsset ResolveNumericFont(TMP_FontAsset preferred, TMP_FontAsset readableFallback)
        {
            if (preferred != null)
                return preferred;

            TMP_FontAsset resolvedReadable = ResolveReadableFont(readableFallback);
            return resolvedReadable != null ? resolvedReadable : TMP_Settings.defaultFontAsset;
        }

        /// <summary>
        /// Resolve a numeric-capable font through a cached localization owner.
        /// </summary>
        public static TMP_FontAsset ResolveNumericFont(
            TMP_FontAsset preferred,
            TMP_FontAsset readableFallback,
            LocalizationManager manager)
        {
            if (preferred != null)
                return preferred;

            TMP_FontAsset resolvedReadable = ResolveReadableFont(readableFallback, manager);
            return resolvedReadable != null ? resolvedReadable : TMP_Settings.defaultFontAsset;
        }

        /// <summary>
        /// Resolve the low-risk BIOS fallback font used when a localized SDF atlas is not ready within two frames.
        /// </summary>
        public static TMP_FontAsset ResolveBiosFallbackFont()
        {
            if (IsFontReady(_cachedBiosFallbackFont))
                return _cachedBiosFallbackFont;

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (IsFontReady(defaultFont))
            {
                _cachedBiosFallbackFont = defaultFont;
                return _cachedBiosFallbackFont;
            }

            if (TMP_Settings.fallbackFontAssets == null)
                return defaultFont;

            for (int i = 0; i < TMP_Settings.fallbackFontAssets.Count; i++)
            {
                TMP_FontAsset candidate = TMP_Settings.fallbackFontAssets[i];
                if (!IsFontReady(candidate) || IsNumericOnlyFont(candidate))
                    continue;

                _cachedBiosFallbackFont = candidate;
                return _cachedBiosFallbackFont;
            }

            _cachedBiosFallbackFont = defaultFont;
            return _cachedBiosFallbackFont;
        }

        /// <summary>
        /// Clears dynamic TMP atlas data for the cached localization font assets before domain teardown.
        /// </summary>
        public static void ReleaseCachedRuntimeFonts()
        {
            TryClearDynamicFontData(_cachedReadableFont);
            TryClearDynamicFontData(_cachedReadableFontCjkSc);
            TryClearDynamicFontData(_cachedReadableFontCjkJp);
            TryClearDynamicFontData(_cachedReadableFontArabic);
            TryClearDynamicFontData(_cachedBiosFallbackFont);

            _cachedReadableFont = null;
            _cachedReadableFontCjkSc = null;
            _cachedReadableFontCjkJp = null;
            _cachedReadableFontArabic = null;
            _cachedBiosFallbackFont = null;
        }

        /// <summary>
        /// Best-effort dynamic font cleanup used during manager shutdown.
        /// </summary>
        public static void TryClearDynamicFontData(TMP_FontAsset font)
        {
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                return;

            if (!HasResolvableAtlas(font))
                return;

            try
            {
                font.ClearFontAssetData(false);
            }
            catch (MissingReferenceException)
            {
            }
            catch (UnassignedReferenceException)
            {
            }
        }

        /// <summary>
        /// True when the font asset exposes both a material and an atlas texture ready for staged swap.
        /// </summary>
        public static bool IsFontReady(TMP_FontAsset font)
        {
            if (font == null || font.material == null)
                return false;

            return HasResolvableAtlas(font);
        }

        /// <summary>
        /// Returns true when the supplied language uses CJK fallback coverage.
        /// </summary>
        public static bool IsCjkLanguage(GameLanguage language)
        {
            return language == GameLanguage.ChineseSimplified ||
                   language == GameLanguage.ChineseTraditional ||
                   language == GameLanguage.Japanese ||
                   language == GameLanguage.Korean;
        }

        /// <summary>
        /// Identify digit-only TMP font assets by naming convention.
        /// </summary>
        public static bool IsNumericOnlyFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string name = font.name;
            return name.IndexOf(NumericFontToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("digit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CacheLanguageFonts(TMP_FontAsset preferred)
        {
            TMP_FontAsset primary = ResolvePrimaryReadableFont(preferred);
            if (primary == null)
                primary = TMP_Settings.defaultFontAsset;

            _cachedReadableFont = primary;
            _cachedReadableFontCjkSc = ResolveLanguageFallback(primary, GameLanguage.ChineseSimplified);
            _cachedReadableFontCjkJp = ResolveLanguageFallback(primary, GameLanguage.Japanese);
            _cachedReadableFontArabic = ResolveLanguageFallback(primary, GameLanguage.Arabic);
        }

        private static TMP_FontAsset GetCachedLanguageFont(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.ChineseSimplified:
                case GameLanguage.ChineseTraditional:
                case GameLanguage.Korean:
                    return _cachedReadableFontCjkSc;

                case GameLanguage.Japanese:
                    return _cachedReadableFontCjkJp;

                case GameLanguage.Arabic:
                    return _cachedReadableFontArabic;

                default:
                    return _cachedReadableFont;
            }
        }

        private static TMP_FontAsset ResolvePrimaryReadableFont(TMP_FontAsset preferred)
        {
            if (preferred != null && !IsNumericOnlyFont(preferred))
                return preferred;

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null && !IsNumericOnlyFont(defaultFont))
                return defaultFont;

            if (TMP_Settings.fallbackFontAssets == null)
                return defaultFont;

            for (int i = 0; i < TMP_Settings.fallbackFontAssets.Count; i++)
            {
                TMP_FontAsset candidate = TMP_Settings.fallbackFontAssets[i];
                if (candidate != null && !IsNumericOnlyFont(candidate))
                    return candidate;
            }

            return defaultFont;
        }

        private static TMP_FontAsset ResolveLanguageFallback(TMP_FontAsset primary, GameLanguage language)
        {
            if (primary == null)
                return null;

            if (!IsCjkLanguage(language) && language != GameLanguage.Arabic)
                return primary;

            if (FontSupportsLanguage(primary, language))
                return primary;

            TMP_FontAsset fallback = FindLanguageFontInTable(primary.fallbackFontAssetTable, language);
            if (fallback != null)
                return fallback;

            if (TMP_Settings.fallbackFontAssets == null)
                return primary;

            fallback = FindLanguageFontInTable(TMP_Settings.fallbackFontAssets, language);
            return fallback != null ? fallback : primary;
        }

        private static TMP_FontAsset FindLanguageFontInTable(System.Collections.Generic.List<TMP_FontAsset> table, GameLanguage language)
        {
            if (table == null)
                return null;

            for (int i = 0; i < table.Count; i++)
            {
                TMP_FontAsset candidate = table[i];
                if (candidate == null || IsNumericOnlyFont(candidate))
                    continue;

                if (FontSupportsLanguage(candidate, language))
                    return candidate;

                TMP_FontAsset nested = FindLanguageFontInTable(candidate.fallbackFontAssetTable, language);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static bool FontSupportsLanguage(TMP_FontAsset font, GameLanguage language)
        {
            if (font == null)
                return false;

            string name = font.name;
            switch (language)
            {
                case GameLanguage.ChineseSimplified:
                case GameLanguage.ChineseTraditional:
                case GameLanguage.Korean:
                    return name.IndexOf(CjkScFontToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf(TextFontToken, StringComparison.OrdinalIgnoreCase) >= 0;

                case GameLanguage.Japanese:
                    return name.IndexOf(CjkJpFontToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf(CjkScFontToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf(TextFontToken, StringComparison.OrdinalIgnoreCase) >= 0;

                case GameLanguage.Arabic:
                    return name.IndexOf(ArabicFontToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf(TextFontToken, StringComparison.OrdinalIgnoreCase) >= 0;

                default:
                    return true;
            }
        }

        private static bool HasResolvableAtlas(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            try
            {
                if (font.material != null && font.material.GetTexture(ShaderUtilities.ID_MainTex) != null)
                    return true;

                Texture[] atlasTextures = font.atlasTextures;
                return atlasTextures != null &&
                       atlasTextures.Length > 0 &&
                       atlasTextures[0] != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnassignedReferenceException)
            {
                return false;
            }
        }
    }
}
