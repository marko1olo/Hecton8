using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hecton8.Input;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton.Localization
{
    /// <summary>
    /// Game language identifiers supported by the first-party localization layer.
    /// </summary>
    public enum GameLanguage
    {
        English = 0,
        Russian = 1,
        German = 2,
        French = 3,
        Spanish = 4,
        Italian = 5,
        PortugueseBrazilian = 6,
        Polish = 7,
        Turkish = 8,
        Ukrainian = 9,
        ChineseSimplified = 10,
        ChineseTraditional = 11,
        Japanese = 12,
        Korean = 13,
        Hindi = 14,
        Indonesian = 15,
        Arabic = 16,
    }

    /// <summary>
    /// Runtime owner for string localization tables and language switching.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizationManager : MonoBehaviour
    {
        // COLD ALLOC: Regex[1] â€” flat JSON key/value extraction for localization tables â€” owner: LocalizationManager
        private static readonly Regex FlatJsonEntryRegex = new Regex(
            "\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        // COLD ALLOC: Regex[1] — button token replacement for localized TMP text — owner: LocalizationManager
        private static readonly Regex ButtonTokenRegex = new Regex(
            "<button:(?<token>[a-zA-Z0-9_\\-]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ItemTokenRegex = new Regex(
            "<item:(?<token>[a-zA-Z0-9_\\-]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex StatusTokenRegex = new Regex(
            "<status:(?<token>[a-zA-Z0-9_\\-]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly MatchEvaluator ButtonTokenEvaluator = EvaluateButtonToken;
        private static readonly MatchEvaluator ItemTokenEvaluator = EvaluateItemToken;
        private static readonly MatchEvaluator StatusTokenEvaluator = EvaluateStatusToken;
        private const string DefaultLanguageTableFolder = "Assets/_Project/Scripts";

        public static LocalizationManager Instance { get; private set; }

        public static event Action<GameLanguage> OnLanguageChanged;

        [Header("=== Config ===")]
        [SerializeField] private GameLanguage defaultLanguage = GameLanguage.English;

        [Header("=== Localization Data (JSON TextAssets) ===")]
        [Tooltip("Each TextAsset is a flat JSON object: { \"KEY\": \"Value\" }. File name must match the GameLanguage enum name.")]
        [SerializeField] private TextAsset[] languageFiles;

        // Shared user-options key owned by UserOptionsPersistence.
        private const string PrefsLanguageKey = UserOptionsPersistence.LanguageKey;

        // COLD ALLOC: Dictionary[20] â€” language tables for UI/content lookup â€” owner: LocalizationManager
        private readonly Dictionary<GameLanguage, Dictionary<string, string>> _tables =
            new Dictionary<GameLanguage, Dictionary<string, string>>(20);

        /// <summary>
        /// Active language for runtime lookups.
        /// </summary>
        public GameLanguage CurrentLanguage { get; private set; } = GameLanguage.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            OnLanguageChanged = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAllTables();
            RestoreSavedLanguage();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            SyncLanguageFilesFromDefaultFolder();
        }

        private void OnValidate()
        {
            SyncLanguageFilesFromDefaultFolder();
        }
#endif

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                OnLanguageChanged = null;
            }
        }

        /// <summary>
        /// Resolve a localized string for the current language.
        /// Missing keys return the key itself in development-friendly fashion.
        /// </summary>
        public string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            if (TryGet(CurrentLanguage, key, out string value))
                return value;

#if UNITY_EDITOR
            Debug.LogWarning($"[Localization] Missing key: \"{key}\" for {CurrentLanguage}");
#endif
            return key;
        }

        /// <summary>
        /// Resolve a formatted localized string.
        /// </summary>
        public string GetFormatted(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0)
                return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    $"[Localization] Format error for key \"{key}\", template: \"{template}\", args count: {args.Length}");
#endif
                return template;
            }
        }

        /// <summary>
        /// Resolve a localized string and expand runtime button tokens for TMP consumers.
        /// </summary>
        public string GetExpanded(string key)
        {
            return ExpandRuntimeTokens(Get(key));
        }

        /// <summary>
        /// Resolve a localized string with fallback and expand runtime button tokens.
        /// </summary>
        public string GetExpandedOrFallback(GameLanguage language, string key, string fallback)
        {
            return ExpandRuntimeTokens(GetOrFallback(language, key, fallback));
        }

        /// <summary>
        /// Resolve, format, and expand a localized string for TMP consumers.
        /// </summary>
        public string GetExpandedFormatted(string key, params object[] args)
        {
            string template = Get(key);
            return ExpandRuntimeTokens(FormatLocalized(template, key, args));
        }

        /// <summary>
        /// Expand runtime localization tokens in an arbitrary authored string.
        /// </summary>
        public string ExpandText(string text)
        {
            return ExpandRuntimeTokens(text);
        }

        /// <summary>
        /// Resolve a pluralized localized string root for the current language.
        /// Expected suffixes: _ZERO, _ONE, _TWO, _FEW, _MANY, _OTHER.
        /// </summary>
        public string GetPlural(string keyRoot, int count)
        {
            string pluralKey = ResolvePluralKey(CurrentLanguage, keyRoot, count);
            return Get(pluralKey);
        }

        /// <summary>
        /// Resolve, format, and expand a pluralized localized string root for the current language.
        /// </summary>
        public string GetPluralFormatted(string keyRoot, int count, params object[] args)
        {
            string pluralKey = ResolvePluralKey(CurrentLanguage, keyRoot, count);
            string template = Get(pluralKey);
            return ExpandRuntimeTokens(FormatLocalized(template, pluralKey, args));
        }

        /// <summary>
        /// Attempt to resolve a key for the requested language without logging warnings.
        /// Falls back to English when the target table does not contain the key.
        /// </summary>
        public bool TryGet(GameLanguage language, string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = string.Empty;
                return false;
            }

            if (TryGetFromTable(language, key, out value))
                return true;

            if (language != GameLanguage.English && TryGetFromTable(GameLanguage.English, key, out value))
                return true;

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// Resolve a key with an explicit fallback string.
        /// </summary>
        public string GetOrFallback(GameLanguage language, string key, string fallback)
        {
            return TryGet(language, key, out string value) ? value : (fallback ?? string.Empty);
        }

        /// <summary>
        /// Switch the active language and notify subscribers.
        /// </summary>
        public void SetLanguage(GameLanguage language)
        {
            if (CurrentLanguage == language)
                return;

            CurrentLanguage = language;

            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            options.SetInt(PrefsLanguageKey, (int)language);
            options.Save();

            OnLanguageChanged?.Invoke(language);

#if UNITY_EDITOR
            Debug.Log($"[Localization] Language changed to: {language}");
#endif
        }

        /// <summary>
        /// Cycle to the next language in the enum order.
        /// </summary>
        public void CycleLanguage()
        {
            int count = Enum.GetValues(typeof(GameLanguage)).Length;
            int next = ((int)CurrentLanguage + 1) % count;
            SetLanguage((GameLanguage)next);
        }

        private void LoadAllTables()
        {
            _tables.Clear();

            if (languageFiles == null || languageFiles.Length == 0)
            {
                LoadBuiltInTables();
                return;
            }

            for (int i = 0; i < languageFiles.Length; i++)
            {
                TextAsset file = languageFiles[i];
                if (file == null)
                    continue;

                if (!Enum.TryParse(file.name, true, out GameLanguage language))
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[Localization] Cannot parse language from filename: \"{file.name}\". " +
                        $"Expected one of: {string.Join(", ", Enum.GetNames(typeof(GameLanguage)))}");
#endif
                    continue;
                }

                _tables[language] = ParseJsonTable(file.text);
            }

            if (!_tables.ContainsKey(GameLanguage.English))
                LoadBuiltInTables();
        }

        private static Dictionary<string, string> ParseJsonTable(string json)
        {
            // COLD ALLOC: Dictionary[128] â€” parsed localization table entries â€” owner: LocalizationManager
            var result = new Dictionary<string, string>(128);
            if (string.IsNullOrWhiteSpace(json))
                return result;

            try
            {
                MatchCollection matches = FlatJsonEntryRegex.Matches(json);
                for (int i = 0; i < matches.Count; i++)
                {
                    Match match = matches[i];
                    if (!match.Success)
                        continue;

                    string key = Regex.Unescape(match.Groups["key"].Value);
                    string value = Regex.Unescape(match.Groups["value"].Value);

                    if (!string.IsNullOrWhiteSpace(key))
                        result[key] = value;
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR
                Debug.LogError($"[Localization] JSON parse error: {exception.Message}");
#endif
            }

            return result;
        }

        private string ExpandRuntimeTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string expanded = ButtonTokenRegex.Replace(text, ButtonTokenEvaluator);
            expanded = ItemTokenRegex.Replace(expanded, ItemTokenEvaluator);
            expanded = StatusTokenRegex.Replace(expanded, StatusTokenEvaluator);
            return expanded;
        }

        private static string EvaluateButtonToken(Match match)
        {
            if (match == null || !match.Success)
                return string.Empty;

            string token = match.Groups["token"].Value;
            InputManager input = InputManager.Instance;
            if (input != null && input.TryGetBindingMarkupForToken(token, out string markup))
                return markup;

            return match.Value;
        }

        private static string EvaluateItemToken(Match match)
        {
            if (match == null || !match.Success)
                return string.Empty;

            string token = match.Groups["token"].Value;
            return LocalizedInlineIconResolver.TryResolveItemChip(token, out string markup)
                ? markup
                : match.Value;
        }

        private static string EvaluateStatusToken(Match match)
        {
            if (match == null || !match.Success)
                return string.Empty;

            string token = match.Groups["token"].Value;
            return LocalizedInlineIconResolver.TryResolveStatusChip(token, out string markup)
                ? markup
                : match.Value;
        }

        private string ResolvePluralKey(GameLanguage language, string keyRoot, int count)
        {
            string preferred = keyRoot + GetPluralSuffix(language, count);
            if (TryGet(language, preferred, out _))
                return preferred;

            string fallbackOther = keyRoot + "_OTHER";
            if (TryGet(language, fallbackOther, out _))
                return fallbackOther;

            return keyRoot;
        }

        private static string GetPluralSuffix(GameLanguage language, int count)
        {
            int absCount = Math.Abs(count);

            switch (language)
            {
                case GameLanguage.Russian:
                case GameLanguage.Ukrainian:
                    return ResolveEastSlavicPluralSuffix(absCount);

                case GameLanguage.Polish:
                    if (absCount == 1)
                        return "_ONE";

                    int polishMod10 = absCount % 10;
                    int polishMod100 = absCount % 100;
                    if (polishMod10 >= 2 && polishMod10 <= 4 && !(polishMod100 >= 12 && polishMod100 <= 14))
                        return "_FEW";
                    return "_MANY";

                case GameLanguage.Arabic:
                    if (absCount == 0)
                        return "_ZERO";
                    if (absCount == 1)
                        return "_ONE";
                    if (absCount == 2)
                        return "_TWO";

                    int arabicMod100 = absCount % 100;
                    if (arabicMod100 >= 3 && arabicMod100 <= 10)
                        return "_FEW";
                    if (arabicMod100 >= 11 && arabicMod100 <= 99)
                        return "_MANY";
                    return "_OTHER";

                default:
                    return absCount == 1 ? "_ONE" : "_OTHER";
            }
        }

        private static string ResolveEastSlavicPluralSuffix(int count)
        {
            int mod10 = count % 10;
            int mod100 = count % 100;

            if (mod10 == 1 && mod100 != 11)
                return "_ONE";
            if (mod10 >= 2 && mod10 <= 4 && !(mod100 >= 12 && mod100 <= 14))
                return "_FEW";
            return "_MANY";
        }

        /// <summary>
        /// True when the selected language should be rendered right-to-left.
        /// </summary>
        public static bool IsRightToLeftLanguage(GameLanguage language)
        {
            return language == GameLanguage.Arabic;
        }

        private static string FormatLocalized(string template, string key, params object[] args)
        {
            if (args == null || args.Length == 0)
                return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    $"[Localization] Format error for key \"{key}\", template: \"{template}\", args count: {args.Length}");
#endif
                return template;
            }
        }

        private void LoadBuiltInTables()
        {
            if (!_tables.ContainsKey(GameLanguage.English))
            {
                _tables[GameLanguage.English] = new Dictionary<string, string>(160)
                {
                    { LocalizationKeys.MENU_NEW_GAME, "New Game" },
                    { LocalizationKeys.MENU_LOAD_GAME, "Load Game" },
                    { LocalizationKeys.MENU_SETTINGS, "Settings" },
                    { LocalizationKeys.MENU_QUIT, "Quit" },
                    { LocalizationKeys.MODAL_CONFIRM, "Confirm" },
                    { LocalizationKeys.MODAL_CANCEL, "Cancel" },
                    { LocalizationKeys.MODAL_NEW_GAME_TITLE, "New Game" },
                    { LocalizationKeys.MODAL_NEW_GAME_MESSAGE, "Start a new game?" },
                    { LocalizationKeys.MODAL_LOAD_TITLE, "Load Game" },
                    { LocalizationKeys.MODAL_LOAD_MESSAGE, "Load save \"{0}\"?" },
                    { LocalizationKeys.MODAL_QUIT_TITLE, "Quit" },
                    { LocalizationKeys.MODAL_QUIT_MESSAGE, "Quit the game?" },
                    { LocalizationKeys.SLOT_PREFIX, "SLOT" },
                    { LocalizationKeys.SLOT_NO_DATA, "NO DATA" },
                    { LocalizationKeys.SLOT_PLAYTIME, "Playtime" },
                    { LocalizationKeys.LOADING_PERCENT, "{0}%" },
                };
            }

                        if (!_tables.ContainsKey(GameLanguage.Russian))
            {
                _tables[GameLanguage.Russian] = new Dictionary<string, string>(160)
                {
                    { LocalizationKeys.MENU_NEW_GAME, "Новая игра" },
                    { LocalizationKeys.MENU_LOAD_GAME, "Загрузить" },
                    { LocalizationKeys.MENU_SETTINGS, "Настройки" },
                    { LocalizationKeys.MENU_QUIT, "Выход" },
                    { LocalizationKeys.MODAL_CONFIRM, "Подтвердить" },
                    { LocalizationKeys.MODAL_CANCEL, "Отмена" },
                    { LocalizationKeys.MODAL_NEW_GAME_TITLE, "Новая игра" },
                    { LocalizationKeys.MODAL_NEW_GAME_MESSAGE, "Начать новую игру?" },
                    { LocalizationKeys.MODAL_LOAD_TITLE, "Загрузка" },
                    { LocalizationKeys.MODAL_LOAD_MESSAGE, "Загрузить сохранение \"{0}\"?" },
                    { LocalizationKeys.MODAL_QUIT_TITLE, "Выход" },
                    { LocalizationKeys.MODAL_QUIT_MESSAGE, "Выйти из игры?" },
                    { LocalizationKeys.SLOT_PREFIX, "СЛОТ" },
                    { LocalizationKeys.SLOT_NO_DATA, "НЕТ ДАННЫХ" },
                    { LocalizationKeys.SLOT_PLAYTIME, "Время игры" },
                    { LocalizationKeys.LOADING_PERCENT, "{0}%" },
                };
            }
        }

        private void RestoreSavedLanguage()
        {
            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            if (options.HasKey(PrefsLanguageKey))
            {
                int saved = options.GetInt(PrefsLanguageKey, (int)defaultLanguage);
                if (Enum.IsDefined(typeof(GameLanguage), saved))
                {
                    CurrentLanguage = (GameLanguage)saved;
                    return;
                }

                CurrentLanguage = defaultLanguage;
                return;
            }

            CurrentLanguage = defaultLanguage;
        }

#if UNITY_EDITOR
        private void SyncLanguageFilesFromDefaultFolder()
        {
            Array languages = Enum.GetValues(typeof(GameLanguage));
            int languageCount = languages.Length;
            TextAsset[] discovered = new TextAsset[languageCount];
            int discoveredCount = 0;

            for (int i = 0; i < languageCount; i++)
            {
                GameLanguage language = (GameLanguage)languages.GetValue(i);
                string path = DefaultLanguageTableFolder + "/" + language + ".json";
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                    continue;

                discovered[discoveredCount++] = asset;
            }

            if (HasSameLanguageFiles(languageFiles, discovered, discoveredCount))
                return;

            TextAsset[] trimmed = new TextAsset[discoveredCount];
            for (int i = 0; i < discoveredCount; i++)
                trimmed[i] = discovered[i];

            languageFiles = trimmed;
            EditorUtility.SetDirty(this);
        }

        private static bool HasSameLanguageFiles(TextAsset[] existing, TextAsset[] candidate, int candidateCount)
        {
            if (existing == null)
                return candidateCount == 0;

            if (existing.Length != candidateCount)
                return false;

            for (int i = 0; i < candidateCount; i++)
            {
                if (existing[i] != candidate[i])
                    return false;
            }

            return true;
        }
#endif

        private bool TryGetFromTable(GameLanguage language, string key, out string value)
        {
            if (_tables.TryGetValue(language, out Dictionary<string, string> table) &&
                table != null &&
                table.TryGetValue(key, out value))
            {
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
