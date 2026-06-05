using System;
using Hecton.Localization;
using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Shared cold-path font resolver for readable localized TMP text.
    /// </summary>
    public static class LocalizedFontResolver
    {
        private const string NumericFontToken = "\u0446\u0438\u0444";
        private const string CjkScFontToken = "notosanscjksc";
        private const string CjkJpFontToken = "notosanscjkjp";
        private const string GeneratedCjkScFontToken = "cjk_sc";
        private const string GeneratedCjkJpFontToken = "cjk_jp";
        private const string ArabicFontToken = "arabic";
        private const uint CjkUnifiedOneGlyph = 0x4E00u;
        private const uint CjkUnifiedMiddleGlyph = 0x4E2Du;
        private const uint HiraganaA = 0x3042u;
        private const uint KatakanaA = 0x30A2u;
        private const uint HangulHan = 0xD55Cu;
        private const uint HangulGeul = 0xAE00u;
        private const uint ArabicAlef = 0x0627u;
        private const uint ArabicBeh = 0x0628u;
        private const uint ArabicMeem = 0x0645u;
        private const uint HebrewAlef = 0x05D0u;
        private const uint HebrewBet = 0x05D1u;
        private const uint HebrewMem = 0x05DEu;

        private static TMP_FontAsset _cachedReadableFont;
        private static TMP_FontAsset _cachedReadableFontCjkSc;
        private static TMP_FontAsset _cachedReadableFontCjkJp;
        private static TMP_FontAsset _cachedReadableFontArabic;
        private static TMP_FontAsset _cachedReadableFontHebrew;
        private static TMP_FontAsset _cachedBiosFallbackFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedReadableFont = null;
            _cachedReadableFontCjkSc = null;
            _cachedReadableFontCjkJp = null;
            _cachedReadableFontArabic = null;
            _cachedReadableFontHebrew = null;
            _cachedBiosFallbackFont = null;
        }

        /// <summary>
        /// Resolve a readable text font, rejecting digit-only TMP assets.
        /// </summary>
        public static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred)
        {
            return ResolveReadableFontForLanguage(preferred, GameLanguage.English);
        }

        /// <summary>
        /// Resolve a readable text font through a cached localization read model.
        /// </summary>
        public static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred, ILocalizationTextReadModel manager)
        {
            GameLanguage language = manager != null ? (GameLanguage)manager.ActiveLanguageId : GameLanguage.English;
            return ResolveReadableFontForLanguage(preferred, language);
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
                IsFontReady(preferred) &&
                !IsNumericOnlyFont(preferred) &&
                FontSupportsLanguage(preferred, language))
            {
                return preferred;
            }

            TMP_FontAsset cached = GetCachedLanguageFont(language);
            if (IsFontReady(cached))
                return cached;

            CacheLanguageFonts(preferred);
            TMP_FontAsset resolved = GetCachedLanguageFont(language);
            if (IsFontReady(resolved))
                return resolved;

            TMP_FontAsset biosFallback = ResolveBiosFallbackFont();
            if (UsesDedicatedScriptFallback(language) &&
                !FontSupportsLanguage(biosFallback, language))
            {
                return null;
            }

            return biosFallback;
        }

        /// <summary>
        /// Resolve a numeric-capable font, falling back to the readable label font when needed.
        /// </summary>
        public static TMP_FontAsset ResolveNumericFont(TMP_FontAsset preferred, TMP_FontAsset readableFallback)
        {
            if (IsFontReady(preferred))
                return preferred;

            TMP_FontAsset resolvedReadable = ResolveReadableFont(readableFallback);
            return IsFontReady(resolvedReadable) ? resolvedReadable : null;
        }

        /// <summary>
        /// Resolve a numeric-capable font through a cached localization owner.
        /// </summary>
        public static TMP_FontAsset ResolveNumericFont(
            TMP_FontAsset preferred,
            TMP_FontAsset readableFallback,
            LocalizationManager manager)
        {
            if (IsFontReady(preferred))
                return preferred;

            TMP_FontAsset resolvedReadable = ResolveReadableFont(readableFallback, manager);
            return IsFontReady(resolvedReadable) ? resolvedReadable : null;
        }

        /// <summary>
        /// Resolve the low-risk BIOS fallback font used when a localized SDF atlas is not ready within two frames.
        /// </summary>
        public static TMP_FontAsset ResolveBiosFallbackFont()
        {
            if (IsFontReady(_cachedBiosFallbackFont))
                return _cachedBiosFallbackFont;

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (IsFontReady(defaultFont) && !IsNumericOnlyFont(defaultFont))
            {
                _cachedBiosFallbackFont = defaultFont;
                return _cachedBiosFallbackFont;
            }

            if (TMP_Settings.fallbackFontAssets == null)
                return null;

            for (int i = 0; i < TMP_Settings.fallbackFontAssets.Count; i++)
            {
                TMP_FontAsset candidate = TMP_Settings.fallbackFontAssets[i];
                if (!IsFontReady(candidate) || IsNumericOnlyFont(candidate))
                    continue;

                _cachedBiosFallbackFont = candidate;
                return _cachedBiosFallbackFont;
            }

            _cachedBiosFallbackFont = null;
            return null;
        }

        /// <summary>
        /// Releases cached localization font references. Glyph atlas ownership is static and editor-baked.
        /// </summary>
        public static void ReleaseCachedRuntimeFonts()
        {
            _cachedReadableFont = null;
            _cachedReadableFontCjkSc = null;
            _cachedReadableFontCjkJp = null;
            _cachedReadableFontArabic = null;
            _cachedReadableFontHebrew = null;
            _cachedBiosFallbackFont = null;
        }

        /// <summary>
        /// True when the font asset exposes both a material and an atlas texture ready for staged swap.
        /// </summary>
        public static bool IsFontReady(TMP_FontAsset font)
        {
            if (font == null || font.material == null)
                return false;

            if (font.atlasPopulationMode != AtlasPopulationMode.Static || font.isMultiAtlasTexturesEnabled)
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
            _cachedReadableFont = primary;
            _cachedReadableFontCjkSc = ResolveLanguageFallback(primary, GameLanguage.ChineseSimplified);
            _cachedReadableFontCjkJp = ResolveLanguageFallback(primary, GameLanguage.Japanese);
            _cachedReadableFontArabic = ResolveLanguageFallback(primary, GameLanguage.Arabic);
            _cachedReadableFontHebrew = ResolveLanguageFallback(primary, GameLanguage.Hebrew);
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

                case GameLanguage.Hebrew:
                    return _cachedReadableFontHebrew;

                default:
                    return _cachedReadableFont;
            }
        }

        private static TMP_FontAsset ResolvePrimaryReadableFont(TMP_FontAsset preferred)
        {
            if (preferred != null && IsFontReady(preferred) && !IsNumericOnlyFont(preferred))
                return preferred;

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (IsFontReady(defaultFont) && !IsNumericOnlyFont(defaultFont))
                return defaultFont;

            if (TMP_Settings.fallbackFontAssets == null)
                return null;

            for (int i = 0; i < TMP_Settings.fallbackFontAssets.Count; i++)
            {
                TMP_FontAsset candidate = TMP_Settings.fallbackFontAssets[i];
                if (IsFontReady(candidate) && !IsNumericOnlyFont(candidate))
                    return candidate;
            }

            return null;
        }

        private static TMP_FontAsset ResolveLanguageFallback(TMP_FontAsset primary, GameLanguage language)
        {
            if (primary == null)
                return null;

            if (!UsesDedicatedScriptFallback(language))
                return primary;

            if (FontSupportsLanguage(primary, language))
                return primary;

            TMP_FontAsset fallback = FindLanguageFontInTable(primary.fallbackFontAssetTable, language);
            if (fallback != null)
                return fallback;

            if (TMP_Settings.fallbackFontAssets == null)
                return null;

            fallback = FindLanguageFontInTable(TMP_Settings.fallbackFontAssets, language);
            return fallback;
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

                if (IsFontReady(candidate) && FontSupportsLanguage(candidate, language))
                    return candidate;
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
                    return HasCjkScName(name) &&
                           HasGlyph(font, CjkUnifiedOneGlyph) &&
                           HasGlyph(font, CjkUnifiedMiddleGlyph);

                case GameLanguage.Korean:
                    return HasCjkScName(name) &&
                           HasGlyph(font, HangulHan) &&
                           HasGlyph(font, HangulGeul);

                case GameLanguage.Japanese:
                    return (HasCjkJpName(name) || HasCjkScName(name)) &&
                           HasGlyph(font, HiraganaA) &&
                           HasGlyph(font, KatakanaA) &&
                           HasGlyph(font, CjkUnifiedOneGlyph);

                case GameLanguage.Arabic:
                    return name.IndexOf(ArabicFontToken, StringComparison.OrdinalIgnoreCase) >= 0 &&
                           HasGlyph(font, ArabicAlef) &&
                           HasGlyph(font, ArabicBeh) &&
                           HasGlyph(font, ArabicMeem);

                case GameLanguage.Hebrew:
                    return HasGlyph(font, HebrewAlef) &&
                           HasGlyph(font, HebrewBet) &&
                           HasGlyph(font, HebrewMem);

                default:
                    return true;
            }
        }

        private static bool UsesDedicatedScriptFallback(GameLanguage language)
        {
            return IsCjkLanguage(language) ||
                   language == GameLanguage.Arabic ||
                   language == GameLanguage.Hebrew;
        }

        private static bool HasCjkScName(string name)
        {
            return name.IndexOf(CjkScFontToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf(GeneratedCjkScFontToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasCjkJpName(string name)
        {
            return name.IndexOf(CjkJpFontToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf(GeneratedCjkJpFontToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasGlyph(TMP_FontAsset font, uint unicode)
        {
            if (font == null)
                return false;

            var characterTable = font.characterTable;
            if (characterTable == null)
                return false;

            for (int i = 0; i < characterTable.Count; i++)
            {
                TMP_Character character = characterTable[i];
                if (character != null && character.unicode == unicode)
                    return true;
            }

            return false;
        }

        private static bool HasResolvableAtlas(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            try
            {
                Material material = font.material;
                if (material == null)
                    return false;

                Texture[] atlasTextures = font.atlasTextures;
                if (atlasTextures == null ||
                    atlasTextures.Length != 1 ||
                    atlasTextures[0] == null)
                {
                    return false;
                }

                return ReferenceEquals(material.GetTexture(ShaderUtilities.ID_MainTex), atlasTextures[0]);
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
