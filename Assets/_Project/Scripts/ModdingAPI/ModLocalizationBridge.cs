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
        // COLD ALLOC: HashSet<string>[64] — injected table guards by mod/language/path — owner: ModLocalizationBridge
        private static readonly HashSet<string> _injectedTableKeys = new HashSet<string>();

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

                string injectionKey = modId + "|" + language + "|" + filePath;
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
            LocalizationManager localization = LocalizationManager.Instance;
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

            string token = fileNameWithoutExtension;
            if (token.StartsWith("lang_", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(5);

            token = token.Replace('-', '_').ToLowerInvariant();

            switch (token)
            {
                case "en":
                case "english":
                    language = GameLanguage.English;
                    return true;
                case "ru":
                case "russian":
                    language = GameLanguage.Russian;
                    return true;
                case "de":
                case "german":
                    language = GameLanguage.German;
                    return true;
                case "fr":
                case "french":
                    language = GameLanguage.French;
                    return true;
                case "es":
                case "spanish":
                    language = GameLanguage.Spanish;
                    return true;
                case "it":
                case "italian":
                    language = GameLanguage.Italian;
                    return true;
                case "pt_br":
                case "portuguesebrazilian":
                    language = GameLanguage.PortugueseBrazilian;
                    return true;
                case "pl":
                case "polish":
                    language = GameLanguage.Polish;
                    return true;
                case "tr":
                case "turkish":
                    language = GameLanguage.Turkish;
                    return true;
                case "uk":
                case "ukrainian":
                    language = GameLanguage.Ukrainian;
                    return true;
                case "zh_cn":
                case "zh_hans":
                case "chinesesimplified":
                    language = GameLanguage.ChineseSimplified;
                    return true;
                case "zh_tw":
                case "zh_hant":
                case "chinesetraditional":
                    language = GameLanguage.ChineseTraditional;
                    return true;
                case "ja":
                case "japanese":
                    language = GameLanguage.Japanese;
                    return true;
                case "ko":
                case "korean":
                    language = GameLanguage.Korean;
                    return true;
                case "hi":
                case "hindi":
                    language = GameLanguage.Hindi;
                    return true;
                case "id":
                case "indonesian":
                    language = GameLanguage.Indonesian;
                    return true;
                case "ar":
                case "arabic":
                    language = GameLanguage.Arabic;
                    return true;
            }

            return Enum.TryParse(fileNameWithoutExtension, true, out language);
        }

        private struct PendingLanguageTable
        {
            public string ModId;
            public GameLanguage Language;
            public string FilePath;
            public string InjectionKey;
        }
    }
}
