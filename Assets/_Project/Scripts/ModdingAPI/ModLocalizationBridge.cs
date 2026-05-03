using System;
using System.Collections.Generic;
using System.IO;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Cold-path bridge that discovers mod localization files and injects them into the first-party localization owner.
    /// </summary>
    internal static class ModLocalizationBridge
    {
        // COLD ALLOC: List<PendingLanguageTable>[32] — pending mod language injections — owner: ModLocalizationBridge
        private static readonly List<PendingLanguageTable> _pendingTables = new List<PendingLanguageTable>(32);
        // COLD ALLOC: HashSet<uint>[64] — injected table guards by mod/language/path FNV hash — owner: ModLocalizationBridge
        private static readonly HashSet<uint> _injectedTableKeys = new HashSet<uint>(64);
        private static readonly uint _languageEnHash = ComputeLanguageAliasHash("en");
        private static readonly uint _languageEnglishHash = ComputeLanguageAliasHash("english");
        private static readonly uint _languageRuHash = ComputeLanguageAliasHash("ru");
        private static readonly uint _languageRussianHash = ComputeLanguageAliasHash("russian");
        private static readonly uint _languageDeHash = ComputeLanguageAliasHash("de");
        private static readonly uint _languageGermanHash = ComputeLanguageAliasHash("german");
        private static readonly uint _languageFrHash = ComputeLanguageAliasHash("fr");
        private static readonly uint _languageFrenchHash = ComputeLanguageAliasHash("french");
        private static readonly uint _languageEsHash = ComputeLanguageAliasHash("es");
        private static readonly uint _languageSpanishHash = ComputeLanguageAliasHash("spanish");
        private static readonly uint _languageItHash = ComputeLanguageAliasHash("it");
        private static readonly uint _languageItalianHash = ComputeLanguageAliasHash("italian");
        private static readonly uint _languagePtBrHash = ComputeLanguageAliasHash("pt_br");
        private static readonly uint _languagePortugueseBrazilianHash = ComputeLanguageAliasHash("portuguesebrazilian");
        private static readonly uint _languagePlHash = ComputeLanguageAliasHash("pl");
        private static readonly uint _languagePolishHash = ComputeLanguageAliasHash("polish");
        private static readonly uint _languageTrHash = ComputeLanguageAliasHash("tr");
        private static readonly uint _languageTurkishHash = ComputeLanguageAliasHash("turkish");
        private static readonly uint _languageUkHash = ComputeLanguageAliasHash("uk");
        private static readonly uint _languageUkrainianHash = ComputeLanguageAliasHash("ukrainian");
        private static readonly uint _languageZhCnHash = ComputeLanguageAliasHash("zh_cn");
        private static readonly uint _languageZhHansHash = ComputeLanguageAliasHash("zh_hans");
        private static readonly uint _languageChineseSimplifiedHash = ComputeLanguageAliasHash("chinesesimplified");
        private static readonly uint _languageZhTwHash = ComputeLanguageAliasHash("zh_tw");
        private static readonly uint _languageZhHantHash = ComputeLanguageAliasHash("zh_hant");
        private static readonly uint _languageChineseTraditionalHash = ComputeLanguageAliasHash("chinesetraditional");
        private static readonly uint _languageJaHash = ComputeLanguageAliasHash("ja");
        private static readonly uint _languageJapaneseHash = ComputeLanguageAliasHash("japanese");
        private static readonly uint _languageKoHash = ComputeLanguageAliasHash("ko");
        private static readonly uint _languageKoreanHash = ComputeLanguageAliasHash("korean");
        private static readonly uint _languageHiHash = ComputeLanguageAliasHash("hi");
        private static readonly uint _languageHindiHash = ComputeLanguageAliasHash("hindi");
        private static readonly uint _languageIdHash = ComputeLanguageAliasHash("id");
        private static readonly uint _languageIndonesianHash = ComputeLanguageAliasHash("indonesian");
        private static readonly uint _languageArHash = ComputeLanguageAliasHash("ar");
        private static readonly uint _languageArabicHash = ComputeLanguageAliasHash("arabic");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingTables.Clear();
            _injectedTableKeys.Clear();
        }

        /// <summary>
        /// Registers localization files discovered for a mod directory.
        /// </summary>
        internal static void RegisterLocalizationFiles(string modId, string[] filePaths)
        {
            if (string.IsNullOrWhiteSpace(modId) || filePaths == null || filePaths.Length == 0)
                return;

            for (int i = 0; i < filePaths.Length; i++)
            {
                string filePath = filePaths[i];
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    continue;

                if (!TryResolveLanguageFromFileName(Path.GetFileNameWithoutExtension(filePath), out GameLanguage language))
                    continue;

                uint injectionKey = ComputeInjectionHash(modId, language, filePath);
                if (_injectedTableKeys.Contains(injectionKey))
                    continue;

                _pendingTables.Add(new PendingLanguageTable
                {
                    ModId = modId,
                    Language = language,
                    FilePath = filePath,
                    InjectionKey = injectionKey
                });
            }
        }

        /// <summary>
        /// Attempts to inject every pending mod language table into the live localization owner.
        /// Safe to call repeatedly; already injected files are ignored.
        /// </summary>
        internal static void FlushPendingInjections()
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            if (localization == null || _pendingTables.Count == 0)
                return;

            for (int i = _pendingTables.Count - 1; i >= 0; i--)
            {
                PendingLanguageTable pending = _pendingTables[i];

                try
                {
                    string json = File.ReadAllText(pending.FilePath);
                    Dictionary<string, string> entries = LocalizationManager.ParseFlatJsonTable(json);
                    if (entries.Count == 0)
                    {
                        _injectedTableKeys.Add(pending.InjectionKey);
                        _pendingTables.RemoveAt(i);
                        continue;
                    }

                    localization.InjectEntries(
                        pending.Language,
                        entries,
                        pending.ModId + ":" + Path.GetFileName(pending.FilePath),
                        true);

                    _injectedTableKeys.Add(pending.InjectionKey);
                    _pendingTables.RemoveAt(i);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[ModLocalizationBridge] Failed to inject '{pending.FilePath}' for mod '{pending.ModId}': {exception.Message}");
                    _injectedTableKeys.Add(pending.InjectionKey);
                    _pendingTables.RemoveAt(i);
                }
            }
        }

        private static bool TryResolveLanguageFromFileName(string fileNameWithoutExtension, out GameLanguage language)
        {
            language = GameLanguage.English;
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
                return false;

            uint tokenHash = ComputeLanguageTokenHash(fileNameWithoutExtension);
            return TryResolveLanguageFromHash(tokenHash, out language);
        }

        private static bool TryResolveLanguageFromHash(uint tokenHash, out GameLanguage language)
        {
            if (TryMatchLanguage(tokenHash, _languageEnHash, _languageEnglishHash, GameLanguage.English, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageRuHash, _languageRussianHash, GameLanguage.Russian, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageDeHash, _languageGermanHash, GameLanguage.German, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageFrHash, _languageFrenchHash, GameLanguage.French, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageEsHash, _languageSpanishHash, GameLanguage.Spanish, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageItHash, _languageItalianHash, GameLanguage.Italian, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languagePtBrHash, _languagePortugueseBrazilianHash, GameLanguage.PortugueseBrazilian, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languagePlHash, _languagePolishHash, GameLanguage.Polish, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageTrHash, _languageTurkishHash, GameLanguage.Turkish, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageUkHash, _languageUkrainianHash, GameLanguage.Ukrainian, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageZhCnHash, _languageZhHansHash, _languageChineseSimplifiedHash, GameLanguage.ChineseSimplified, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageZhTwHash, _languageZhHantHash, _languageChineseTraditionalHash, GameLanguage.ChineseTraditional, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageJaHash, _languageJapaneseHash, GameLanguage.Japanese, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageKoHash, _languageKoreanHash, GameLanguage.Korean, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageHiHash, _languageHindiHash, GameLanguage.Hindi, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageIdHash, _languageIndonesianHash, GameLanguage.Indonesian, out language))
                return true;

            if (TryMatchLanguage(tokenHash, _languageArHash, _languageArabicHash, GameLanguage.Arabic, out language))
                return true;

            language = GameLanguage.English;
            return false;
        }

        private static bool TryMatchLanguage(
            uint tokenHash,
            uint firstAliasHash,
            uint secondAliasHash,
            GameLanguage candidate,
            out GameLanguage language)
        {
            if (tokenHash == firstAliasHash || tokenHash == secondAliasHash)
            {
                language = candidate;
                return true;
            }

            language = GameLanguage.English;
            return false;
        }

        private static bool TryMatchLanguage(
            uint tokenHash,
            uint firstAliasHash,
            uint secondAliasHash,
            uint thirdAliasHash,
            GameLanguage candidate,
            out GameLanguage language)
        {
            if (tokenHash == firstAliasHash || tokenHash == secondAliasHash || tokenHash == thirdAliasHash)
            {
                language = candidate;
                return true;
            }

            language = GameLanguage.English;
            return false;
        }

        private static uint ComputeLanguageAliasHash(string token)
        {
            return string.IsNullOrEmpty(token) ? 0u : ComputeNormalizedTokenHash(token, 0);
        }

        private static uint ComputeLanguageTokenHash(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
                return 0u;

            int start = StartsWithLangPrefix(fileNameWithoutExtension) ? 5 : 0;
            return ComputeNormalizedTokenHash(fileNameWithoutExtension, start);
        }

        private static uint ComputeNormalizedTokenHash(string token, int start)
        {
            if (start < 0 || start >= token.Length)
                return 0u;

            unchecked
            {
                uint hash = LocHash.FnvOffsetBasis;
                for (int i = start; i < token.Length; i++)
                {
                    char value = NormalizeLanguageTokenChar(token[i]);
                    hash ^= (byte)value;
                    hash *= LocHash.FnvPrime;
                    hash ^= (byte)(value >> 8);
                    hash *= LocHash.FnvPrime;
                }

                return hash;
            }
        }

        private static bool StartsWithLangPrefix(string value)
        {
            return value.Length >= 5 &&
                   ToAsciiLower(value[0]) == 'l' &&
                   ToAsciiLower(value[1]) == 'a' &&
                   ToAsciiLower(value[2]) == 'n' &&
                   ToAsciiLower(value[3]) == 'g' &&
                   value[4] == '_';
        }

        private static char NormalizeLanguageTokenChar(char value)
        {
            return value == '-' ? '_' : ToAsciiLower(value);
        }

        private static char ToAsciiLower(char value)
        {
            return value >= 'A' && value <= 'Z' ? (char)(value + 32) : value;
        }

        private struct PendingLanguageTable
        {
            public string ModId;
            public GameLanguage Language;
            public string FilePath;
            public uint InjectionKey;
        }

        private static uint ComputeInjectionHash(string modId, GameLanguage language, string filePath)
        {
            unchecked
            {
                uint hash = ModCommandDispatcher.ComputeModHash(modId);
                hash ^= ((uint)language + 0x9E3779B9u + (hash << 6) + (hash >> 2));
                hash ^= ModCommandDispatcher.ComputeModHash(filePath) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                return hash;
            }
        }
    }
}
