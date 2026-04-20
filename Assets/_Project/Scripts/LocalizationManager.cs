using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
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
        private static readonly Regex TechTokenRegex = new Regex(
            "<tech:(?<token>[a-zA-Z0-9_\\-]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex KeyTokenRegex = new Regex(
            "<key:(?<token>[a-zA-Z0-9_\\-]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly MatchEvaluator ButtonTokenEvaluator = EvaluateButtonToken;
        private static readonly MatchEvaluator ItemTokenEvaluator = EvaluateItemToken;
        private static readonly MatchEvaluator StatusTokenEvaluator = EvaluateStatusToken;
        private static readonly MatchEvaluator TechTokenEvaluator = EvaluateTechToken;
        private static readonly MatchEvaluator KeyTokenEvaluator = EvaluateKeyToken;
        private const string DefaultLanguageTableFolder = "Assets/_Project/Scripts";
        private const int MaxExpansionPasses = 3;
        private const string AnalyzerTechKeyPrefix = "TECH_";
        private const string AnalyzerPrefabToken = "EnvAnalyzer";
        private const string EnvironmentalAnalyzerToolTypeName = "EnvironmentalAnalyzerTool";
        private const float HullStressCorruptionThreshold = 0.7f;
        private const int MaxStressCorruptionBucket = 8;
        private const string CorruptionBlocks = "#%&█";
        private const string LatinCorruptionAlphabet = "AEINORSTUVWXYZ";
        private const string CyrillicCorruptionAlphabet = "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        private const string ArabicCorruptionAlphabet = "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";
        private const string CjkCorruptionAlphabet = "深海圧壳酸氧流核域警号層站影断障";
        private const string HangulCorruptionAlphabet = "심해압력산소전력균열경보파손격리영역장치";
        private const string DevanagariCorruptionAlphabet = "अआइईउऊकखगघचछजझटठडढतथदधनपफबभमयरलवशसह";

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
        private PlayerToolManager _cachedPlayerToolManager;
        private HectonPlayerMovement _cachedPlayerMovement;
        private int _cachedAnalyzerFrame = -1;
        private bool _cachedAnalyzerInstalled;
        private int _cachedHullStressFrame = -1;
        private float _cachedHullStressCorruptionIntensity;

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
                return ExpandNarrativeTokens(value);

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
        /// Resolve, expand, and apply atmospheric corruption to a localized string.
        /// </summary>
        public string GetCorruptedText(string key, float intensity)
        {
            string expanded = GetExpanded(key);
            return CorruptExpandedText(expanded, intensity);
        }

        /// <summary>
        /// Apply atmospheric corruption to an already expanded localized string.
        /// </summary>
        public string CorruptExpandedText(string text, float intensity)
        {
            return CorruptVisibleText(text, Mathf.Clamp01(intensity), CurrentLanguage);
        }

        /// <summary>
        /// Returns the current suit-stress corruption intensity derived from player hull stress.
        /// </summary>
        public float GetHullStressCorruptionIntensity()
        {
            int frame = Time.frameCount;
            if (_cachedHullStressFrame == frame)
                return _cachedHullStressCorruptionIntensity;

            _cachedHullStressFrame = frame;
            _cachedHullStressCorruptionIntensity = ResolveHullStressCorruptionIntensity();
            return _cachedHullStressCorruptionIntensity;
        }

        /// <summary>
        /// Returns a coarse bucket for suit-stress driven corrosion refresh.
        /// </summary>
        public int GetHullStressCorruptionBucket()
        {
            float intensity = GetHullStressCorruptionIntensity();
            if (intensity <= 0f)
                return 0;

            return Mathf.Clamp(Mathf.CeilToInt(intensity * MaxStressCorruptionBucket), 1, MaxStressCorruptionBucket);
        }

        /// <summary>
        /// Applies suit-stress corrosion to an already expanded display string when hull stress is high enough.
        /// </summary>
        public string ApplyHullStressCorruptionIfNeeded(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            float intensity = GetHullStressCorruptionIntensity();
            if (intensity <= 0f)
                return text;

            return CorruptExpandedText(text, intensity);
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
            string resolved = TryGet(language, key, out string value) ? value : (fallback ?? string.Empty);
            return ExpandNarrativeTokens(resolved);
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

        /// <summary>
        /// Injects additional localization entries into the live table for a language.
        /// Intended for mod content and other cold-path runtime table extensions after first-party tables are loaded.
        /// </summary>
        /// <param name="language">Target language table to extend.</param>
        /// <param name="entries">Flat key/value map to merge into the live table.</param>
        /// <param name="sourceId">Diagnostic source label used in warnings and editor logs.</param>
        /// <param name="overwriteExisting">
        /// True to replace existing keys with injected values.
        /// False to preserve first-writer ownership and only add missing keys.
        /// </param>
        public void InjectEntries(
            GameLanguage language,
            Dictionary<string, string> entries,
            string sourceId,
            bool overwriteExisting = true)
        {
            if (entries == null || entries.Count == 0)
                return;

            if (!_tables.TryGetValue(language, out Dictionary<string, string> table) || table == null)
            {
                table = new Dictionary<string, string>(Mathf.Max(32, entries.Count));
                _tables[language] = table;
            }

            Dictionary<string, string>.Enumerator enumerator = entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!overwriteExisting && table.ContainsKey(key))
                    continue;

                table[key] = enumerator.Current.Value ?? string.Empty;
            }

            if (CurrentLanguage == language)
                OnLanguageChanged?.Invoke(language);

#if UNITY_EDITOR
            Debug.Log($"[Localization] Injected {entries.Count} entries into {language} from '{sourceId}'.");
#endif
        }

        /// <summary>
        /// Parses a flat JSON localization object into the dictionary format consumed by the runtime localization owner.
        /// Expected schema: <c>{ "KEY": "Value" }</c>.
        /// </summary>
        /// <param name="json">Raw JSON text to parse.</param>
        /// <returns>
        /// Parsed key/value pairs.
        /// Invalid or empty input returns an empty dictionary instead of throwing.
        /// </returns>
        public static Dictionary<string, string> ParseFlatJsonTable(string json)
        {
            return ParseJsonTable(json);
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

            string expanded = ExpandNarrativeTokens(text);
            for (int pass = 0; pass < MaxExpansionPasses; pass++)
            {
                string next = ButtonTokenRegex.Replace(expanded, ButtonTokenEvaluator);
                next = ItemTokenRegex.Replace(next, ItemTokenEvaluator);
                next = StatusTokenRegex.Replace(next, StatusTokenEvaluator);
                next = NormalizeExpandedText(next);
                if (string.Equals(next, expanded, StringComparison.Ordinal))
                    break;

                expanded = next;
            }

            return expanded;
        }

        private string ExpandNarrativeTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string expanded = text;
            for (int pass = 0; pass < MaxExpansionPasses; pass++)
            {
                string next = KeyTokenRegex.Replace(expanded, KeyTokenEvaluator);
                next = TechTokenRegex.Replace(next, TechTokenEvaluator);
                next = NormalizeExpandedText(next);
                if (string.Equals(next, expanded, StringComparison.Ordinal))
                    break;

                expanded = next;
            }

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

        private static string EvaluateTechToken(Match match)
        {
            if (match == null || !match.Success)
                return string.Empty;

            LocalizationManager manager = Instance;
            if (manager == null)
                return string.Empty;

            return manager.ResolveTechToken(match.Groups["token"].Value);
        }

        private static string EvaluateKeyToken(Match match)
        {
            if (match == null || !match.Success)
                return string.Empty;

            LocalizationManager manager = Instance;
            if (manager == null)
                return match.Value;

            string token = match.Groups["token"].Value;
            return manager.TryGet(manager.CurrentLanguage, token, out string value) ? value : token;
        }

        private string ResolveTechToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !HasAnalyzerContext())
                return string.Empty;

            string techKey = token.StartsWith(AnalyzerTechKeyPrefix, StringComparison.OrdinalIgnoreCase)
                ? token.ToUpperInvariant()
                : AnalyzerTechKeyPrefix + token.ToUpperInvariant();

            return TryGet(CurrentLanguage, techKey, out string value) ? value : string.Empty;
        }

        private bool HasAnalyzerContext()
        {
            int frame = Time.frameCount;
            if (_cachedAnalyzerFrame == frame)
                return _cachedAnalyzerInstalled;

            _cachedAnalyzerFrame = frame;
            _cachedAnalyzerInstalled = ResolveAnalyzerContext();
            return _cachedAnalyzerInstalled;
        }

        private bool ResolveAnalyzerContext()
        {
            PlayerToolManager toolManager = ResolvePlayerToolManager();
            if (toolManager == null)
                return false;

            PlayerTool currentTool = toolManager.CurrentTool;
            if (currentTool != null && currentTool.GetType().Name.IndexOf(EnvironmentalAnalyzerToolTypeName, StringComparison.Ordinal) >= 0)
                return true;

            int slotCount = toolManager.SlotCount;
            for (int i = 0; i < slotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                if (prefab == null)
                    continue;

                string prefabName = prefab.name;
                if (!string.IsNullOrEmpty(prefabName) &&
                    prefabName.IndexOf(AnalyzerPrefabToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private PlayerToolManager ResolvePlayerToolManager()
        {
            if (_cachedPlayerToolManager != null)
                return _cachedPlayerToolManager;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return null;

            _cachedPlayerToolManager = playerTransform.GetComponentInChildren<PlayerToolManager>(true);
            return _cachedPlayerToolManager;
        }

        private float ResolveHullStressCorruptionIntensity()
        {
            HectonPlayerMovement playerMovement = ResolvePlayerMovement();
            if (playerMovement == null)
                return 0f;

            float hullStress = Mathf.Clamp01(playerMovement.CurrentHullStress01);
            if (hullStress <= HullStressCorruptionThreshold)
                return 0f;

            return Mathf.InverseLerp(HullStressCorruptionThreshold, 1f, hullStress);
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_cachedPlayerMovement != null)
                return _cachedPlayerMovement;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return null;

            _cachedPlayerMovement = playerTransform.GetComponent<HectonPlayerMovement>();
            return _cachedPlayerMovement;
        }

        private static string NormalizeExpandedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string normalized = text.Replace("  ", " ");
            normalized = normalized.Replace(" \n", "\n");
            normalized = normalized.Replace("\n ", "\n");
            return normalized.Trim();
        }

        private static string CorruptVisibleText(string text, float intensity, GameLanguage language)
        {
            if (string.IsNullOrEmpty(text) || intensity <= 0f)
                return text ?? string.Empty;

            string alphabet = GetCorruptionAlphabet(language);
            if (string.IsNullOrEmpty(alphabet))
                return text;

            int threshold = Mathf.RoundToInt(Mathf.Lerp(0f, 700f, intensity));
            if (threshold <= 0)
                return text;

            System.Text.StringBuilder builder = StringBuilderPool.Get();
            bool insideRichTag = false;
            bool previousCorrupted = false;
            int visibleIndex = 0;
            int seed = ComputeCorruptionSeed(text);

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];

                if (current == '<')
                {
                    insideRichTag = true;
                    previousCorrupted = false;
                    builder.Append(current);
                    continue;
                }

                if (insideRichTag)
                {
                    builder.Append(current);
                    if (current == '>')
                        insideRichTag = false;
                    continue;
                }

                if (current == '[' && TryAppendBracketedMarker(text, ref i, builder))
                {
                    previousCorrupted = false;
                    continue;
                }

                if (!ShouldCorruptCharacter(current))
                {
                    previousCorrupted = false;
                    builder.Append(current);
                    continue;
                }

                int hash = seed ^ (visibleIndex * 486187739);
                visibleIndex++;
                bool shouldCorrupt = !previousCorrupted && (hash & 1023) < threshold;
                if (!shouldCorrupt)
                {
                    builder.Append(current);
                    previousCorrupted = false;
                    continue;
                }

                builder.Append(ResolveCorruptionGlyph(hash, alphabet));
                previousCorrupted = true;
            }

            string corrupted = builder.ToString();
            StringBuilderPool.Return(builder);
            return corrupted;
        }

        private static bool TryAppendBracketedMarker(string text, ref int index, System.Text.StringBuilder builder)
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
            {
                builder.Append(text[markerStart]);
                return false;
            }

            for (int i = markerStart; i <= current; i++)
                builder.Append(text[i]);

            index = current;
            return true;
        }

        private static bool ShouldCorruptCharacter(char value)
        {
            return char.IsLetter(value);
        }

        private static int ComputeCorruptionSeed(string text)
        {
            unchecked
            {
                int seed = 17 ^ Mathf.RoundToInt(Time.unscaledTime * 12f);
                for (int i = 0; i < text.Length; i++)
                    seed = (seed * 31) + text[i];
                return seed;
            }
        }

        private static char ResolveCorruptionGlyph(int hash, string alphabet)
        {
            if ((hash & 7) == 0)
                return CorruptionBlocks[(hash >> 3) & (CorruptionBlocks.Length - 1)];

            int alphabetIndex = (hash & int.MaxValue) % alphabet.Length;
            return alphabet[alphabetIndex];
        }

        private static string GetCorruptionAlphabet(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Russian:
                case GameLanguage.Ukrainian:
                    return CyrillicCorruptionAlphabet;

                case GameLanguage.Arabic:
                    return ArabicCorruptionAlphabet;

                case GameLanguage.ChineseSimplified:
                case GameLanguage.ChineseTraditional:
                case GameLanguage.Japanese:
                    return CjkCorruptionAlphabet;

                case GameLanguage.Korean:
                    return HangulCorruptionAlphabet;

                case GameLanguage.Hindi:
                    return DevanagariCorruptionAlphabet;

                default:
                    return LatinCorruptionAlphabet;
            }
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
