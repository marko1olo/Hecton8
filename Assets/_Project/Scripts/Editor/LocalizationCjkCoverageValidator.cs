using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Validates that the primary PDA readable font can resolve shipped CJK glyphs through its fallback chain.
    /// </summary>
    public static class LocalizationCjkCoverageValidator
    {
        private const string PrimaryTextAssetPath = "Assets/_Project/Art/Materials/Fonts/tekst_SDF.asset";
        private const string ChineseLocalizationPath = "Assets/_Project/Scripts/ChineseSimplified.json";
        private const string JapaneseLocalizationPath = "Assets/_Project/Scripts/Japanese.json";
        private const int MissingPreviewLimit = 24;

        [MenuItem("Hecton8/Localization/Validate CJK Fallback Coverage")]
        public static void Validate()
        {
            TMP_FontAsset primaryFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryTextAssetPath);
            if (primaryFont == null)
                throw new System.InvalidOperationException("Primary PDA text font asset is missing.");

            ValidateLanguage(primaryFont, "ChineseSimplified", ChineseLocalizationPath);
            ValidateLanguage(primaryFont, "Japanese", JapaneseLocalizationPath);
        }

        private static void ValidateLanguage(TMP_FontAsset primaryFont, string languageLabel, string tableAssetPath)
        {
            string seed = BuildLocalizedCharacterSeed(tableAssetPath);
            if (string.IsNullOrEmpty(seed))
            {
                Debug.LogWarning($"[Localization] {languageLabel} coverage validation skipped: no visible glyph seed extracted.");
                return;
            }

            if (primaryFont.HasCharacters(seed, out uint[] missingCharacters, searchFallbacks: true, tryAddCharacter: false))
            {
                Debug.Log($"[Localization] {languageLabel} fallback coverage PASS. UniqueGlyphs={seed.Length} Font='{primaryFont.name}'.");
                return;
            }

            string missingPreview = BuildMissingPreview(missingCharacters);
            Debug.LogError(
                $"[Localization] {languageLabel} fallback coverage FAIL. MissingGlyphCount={missingCharacters.Length} Font='{primaryFont.name}' MissingPreview='{missingPreview}'.");
        }

        private static string BuildLocalizedCharacterSeed(string tableAssetPath)
        {
            string fullPath = ResolveProjectPath(tableAssetPath);
            if (!File.Exists(fullPath))
                return string.Empty;

            string json = File.ReadAllText(fullPath);
            Dictionary<string, string> table = LocalizationManager.ParseFlatJsonTable(json);
            // COLD ALLOC: HashSet<char>[estimated localization glyph set] — unique shipped glyph seed — owner: LocalizationCjkCoverageValidator
            var characters = new HashSet<char>();
            Dictionary<string, string>.Enumerator enumerator = table.GetEnumerator();
            while (enumerator.MoveNext())
                AppendVisibleCharacters(enumerator.Current.Value, characters);

            // COLD ALLOC: StringBuilder[unique glyph count] — coverage seed string — owner: LocalizationCjkCoverageValidator
            var builder = new StringBuilder(characters.Count);
            HashSet<char>.Enumerator characterEnumerator = characters.GetEnumerator();
            while (characterEnumerator.MoveNext())
                builder.Append(characterEnumerator.Current);

            return builder.ToString();
        }

        private static void AppendVisibleCharacters(string source, HashSet<char> characters)
        {
            if (string.IsNullOrEmpty(source) || characters == null)
                return;

            bool insideTag = false;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if (character == '<')
                {
                    insideTag = true;
                    continue;
                }

                if (character == '>')
                {
                    insideTag = false;
                    continue;
                }

                if (insideTag || char.IsControl(character) || char.IsWhiteSpace(character))
                    continue;

                characters.Add(character);
            }
        }

        private static string BuildMissingPreview(uint[] missingCharacters)
        {
            if (missingCharacters == null || missingCharacters.Length == 0)
                return string.Empty;

            int previewCount = missingCharacters.Length < MissingPreviewLimit ? missingCharacters.Length : MissingPreviewLimit;
            // COLD ALLOC: StringBuilder[preview glyphs] — missing glyph preview string — owner: LocalizationCjkCoverageValidator
            var builder = new StringBuilder(previewCount * 2);
            for (int i = 0; i < previewCount; i++)
                builder.Append(char.ConvertFromUtf32((int)missingCharacters[i]));

            return builder.ToString();
        }

        private static string ResolveProjectPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
