using System;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Shared cold-path font resolver for readable localized TMP text.
    /// </summary>
    public static class LocalizedFontResolver
    {
        private static TMP_FontAsset _cachedReadableFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedReadableFont = null;
        }

        /// <summary>
        /// Resolve a readable text font, rejecting digit-only TMP assets.
        /// </summary>
        public static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred)
        {
            if (preferred != null && !IsNumericOnlyFont(preferred))
                return preferred;

            if (_cachedReadableFont != null)
                return _cachedReadableFont;

            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>(); // COLD ALLOC: TMP_FontAsset[] — loaded font scan for readable localized font — owner: LocalizedFontResolver
            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset candidate = fonts[i];
                if (candidate == null || IsNumericOnlyFont(candidate))
                    continue;

                string name = candidate.name;
                if (name.IndexOf("текст", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("noto", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _cachedReadableFont = candidate;
                    return candidate;
                }
            }

            _cachedReadableFont = TMP_Settings.defaultFontAsset;
            return _cachedReadableFont;
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
        /// Identify digit-only TMP font assets by naming convention.
        /// </summary>
        public static bool IsNumericOnlyFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string name = font.name;
            return name.IndexOf("циф", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("digit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
