using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Input;
using Hecton8.UI;
using Hecton8.World;
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
        private const float HullStressCorruptionThreshold = 0.75f;
        private const float MadnessEligibilityThreshold = 0.9f;
        private const int MaxStressCorruptionBucket = 8;
        private const int MadnessVisualBucket = MaxStressCorruptionBucket + 1;
        private const int MadnessChancePercent = 15;
        private const float MadnessRollInterval = 0.5f;
        private const float MadnessBlinkDuration = 2f;
        private static readonly int[] MadnessWhisperKeyHashes =
        {
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_01),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_02),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_03),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_04),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_05),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_06),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_07),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_08),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_09),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_10),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_11),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_12),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_13),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_14),
            LocHash.Compute(LocalizationKeys.MADNESS_WHISPERS_15)
        };
        private const string DeepAbyssZoneId = "zone_deep_abyss";
        private const string CorruptionBlocks = "#%&█";
        private const string LatinCorruptionAlphabet = "AEINORSTUVWXYZ";
        private const string CyrillicCorruptionAlphabet = "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        private const string ArabicCorruptionAlphabet = "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";
        private const string CjkCorruptionAlphabet = "深海圧壳酸氧流核域警号層站影断障";
        private const string HangulCorruptionAlphabet = "심해압력산소전력균열경보파손격리영역장치";
        private const string DevanagariCorruptionAlphabet = "अआइईउऊकखगघचछजझटठडढतथदधनपफबभमयरलवशसह";

        public static LocalizationManager Instance { get; private set; }

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
        private float _cachedHullStress01;
        private float _cachedHullStressCorruptionIntensity;
        private float _externalPdaCorrosionIntensity;
        private float _externalPdaCorrosionEndTime;
        private GameLanguage _savedLanguage = GameLanguage.English;
        private bool _transientLanguageOverrideActive;
        private bool _intrusionGlyphModeActive;
        private float _madnessOverrideEndTime;
        private int _madnessActiveWindowId = -1;
        private int _madnessLastRollBucket = int.MinValue;
        private int _lastMadnessAudioWindowId = -1;
        private int _lastPublishedVisualBucket = int.MinValue;
        private int _lastMadnessResolvedWindowId = -1;
        private GameLanguage _lastMadnessResolvedLanguage = (GameLanguage)(-1);
        private string _lastMadnessResolvedSourceToken = string.Empty;
        private string _lastMadnessResolvedValue = string.Empty;
        private bool _registeredLocalizationRuntime;

        /// <summary>
        /// Active language for runtime lookups.
        /// </summary>
        public GameLanguage CurrentLanguage { get; private set; } = GameLanguage.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            GlobalRegistry.RegisterLocalizationRuntime(this);
            _registeredLocalizationRuntime = ReferenceEquals(GlobalRegistry.Localization, this);
            GameBootstrapper.PersistRuntimeService(this);

            if (GetComponent<FontStreamingManager>() == null)
                gameObject.AddComponent<FontStreamingManager>(); // COLD ALLOC: FontStreamingManager[1] — runtime staged localized font swap owner — owner: LocalizationManager

            LoadAllTables();
            RestoreSavedLanguage();
            RefreshRuntimeRegistry();
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
            if (_registeredLocalizationRuntime)
            {
                GlobalRegistry.UnregisterLocalizationRuntime(this);
                _registeredLocalizationRuntime = false;
            }

            if (Instance == this)
            {
                Instance = null;
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
                return ApplyInterfaceIntrusionIfNeeded(ExpandNarrativeTokens(value));

#if UNITY_EDITOR
            Debug.LogWarning($"[Localization] Missing key: \"{key}\" for {CurrentLanguage}");
#endif
            return key;
        }

        /// <summary>
        /// Resolve a localized raw entry as a span for zero-allocation HUD writers.
        /// </summary>
        public ReadOnlySpan<char> GetRawSpanOrFallback(int keyHash, ReadOnlySpan<char> fallback)
        {
            return LocRegistry.TryGetRawBuffer(keyHash, out char[] buffer, out int length)
                ? buffer.AsSpan(0, length)
                : fallback;
        }

        /// <summary>
        /// Resolve a localized raw entry buffer for TMP SetCharArray callers.
        /// </summary>
        public bool TryGetRawBuffer(int keyHash, out char[] buffer, out int length)
        {
            return LocRegistry.TryGetRawBuffer(keyHash, out buffer, out length);
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
            float liveIntensity = Mathf.Max(intensity, GetHullStressCorruptionIntensity());
            string expanded = GetExpanded(key);
            if (TryResolveMadnessOverride(key, out string madnessText))
                return madnessText;

            return CorruptExpandedText(expanded, liveIntensity);
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
            _cachedHullStressCorruptionIntensity = Mathf.Max(ResolveHullStressCorruptionIntensity(), ResolveExternalPdaCorrosionIntensity());
            return _cachedHullStressCorruptionIntensity;
        }

        /// <summary>
        /// Forces a temporary PDA corrosion window without mutating hull-stress state.
        /// </summary>
        public void RequestExternalPdaCorrosion(float intensity, float duration)
        {
            float clampedIntensity = Mathf.Clamp01(intensity);
            float clampedDuration = Mathf.Max(0f, duration);
            if (clampedIntensity <= 0f || clampedDuration <= 0f)
                return;

            _externalPdaCorrosionIntensity = Mathf.Max(_externalPdaCorrosionIntensity, clampedIntensity);
            _externalPdaCorrosionEndTime = Mathf.Max(_externalPdaCorrosionEndTime, Time.unscaledTime + clampedDuration);
            _cachedHullStressFrame = -1;
            LocalizationEvents.PublishCorruptionVisualStateChanged(CurrentLanguage, _lastPublishedVisualBucket);
        }

        /// <summary>
        /// Returns a coarse bucket for suit-stress driven corrosion refresh.
        /// </summary>
        public int GetHullStressCorruptionBucket()
        {
            EvaluateMadnessOverrideState();
            float intensity = GetHullStressCorruptionIntensity();
            int bucket = intensity <= 0f
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(intensity * MaxStressCorruptionBucket), 1, MaxStressCorruptionBucket);

            if (IsMadnessOverrideActive())
                bucket = MadnessVisualBucket;

            return PublishVisualBucket(bucket);
        }

        /// <summary>
        /// Applies suit-stress corrosion to an already expanded display string when hull stress is high enough.
        /// </summary>
        public string ApplyHullStressCorruptionIfNeeded(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (TryResolveMadnessOverride("hull_stress", out string madnessText))
                return madnessText;

            float intensity = GetHullStressCorruptionIntensity();
            if (intensity <= 0f)
                return text;

            return CorruptExpandedText(text, intensity);
        }

        /// <summary>
        /// Applies suit-stress corrosion into a caller-owned buffer without allocating a composed string.
        /// </summary>
        public bool TryApplyHullStressCorruptionIfNeeded(ReadOnlySpan<char> text, char[] destination, out int length)
        {
            length = 0;
            if (destination == null || destination.Length == 0)
                return false;

            if (text.Length == 0)
                return true;

            if (TryResolveMadnessOverride("hull_stress".AsSpan(), destination, out length))
                return true;

            float intensity = GetHullStressCorruptionIntensity();
            if (intensity <= 0f)
            {
                length = CopySpanToBuffer(text, destination);
                return true;
            }

            return TryCorruptVisibleText(text, intensity, CurrentLanguage, destination, out length);
        }

        /// <summary>
        /// Force visible HUD consumers to refresh against the latest hull-stress corruption state.
        /// </summary>
        internal void RefreshHullStressHudCorruptionVisuals()
        {
            EvaluateMadnessOverrideState();
            LocalizationEvents.PublishCorruptionVisualStateChanged(CurrentLanguage, _lastPublishedVisualBucket);
        }

        /// <summary>
        /// Resolve the current madness-whisper line for HUD takeover surfaces.
        /// </summary>
        internal string GetHullStressHudWhisper(string fallback)
        {
            EvaluateMadnessOverrideState();
            int cycle = _madnessActiveWindowId >= 0
                ? _madnessActiveWindowId
                : Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime / MadnessRollInterval));
            int seed = ComputeMadnessSeed("HUD", cycle, (int)CurrentLanguage);
            string whisperKey = ResolveMadnessWhisperKey(seed);
            return GetOrFallback(CurrentLanguage, whisperKey, fallback);
        }

        /// <summary>
        /// Writes the current hull-stress HUD whisper into a caller-owned buffer.
        /// </summary>
        internal bool TryGetHullStressHudWhisperBuffer(ReadOnlySpan<char> fallback, char[] destination, out int length)
        {
            length = 0;
            if (destination == null || destination.Length == 0)
                return false;

            EvaluateMadnessOverrideState();
            int cycle = _madnessActiveWindowId >= 0
                ? _madnessActiveWindowId
                : Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime / MadnessRollInterval));
            int seed = ComputeMadnessSeed("HUD".AsSpan(), cycle, (int)CurrentLanguage);
            int keyHash = ResolveMadnessWhisperKeyHash(seed);
            ReadOnlySpan<char> whisper = LocRegistry.TryGetRawBuffer(keyHash, out char[] rawBuffer, out int rawLength) && rawLength > 0
                ? rawBuffer.AsSpan(0, rawLength)
                : fallback;

            return TryApplyHullStressCorruptionIfNeeded(whisper, destination, out length);
        }

        /// <summary>
        /// Applies PDA-specific hull-stress corrosion and may replace the entire localized body with a localized madness whisper.
        /// Intended only for item descriptions, log summaries, and audio-log subtitle surfaces.
        /// </summary>
        public string ApplyPdaLoreCorruptionIfNeeded(string sourceToken, string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (TryResolveMadnessOverride(sourceToken, out string madnessText))
                return madnessText;

            float intensity = GetHullStressCorruptionIntensity();
            return intensity > 0f
                ? CorruptExpandedText(text, intensity)
                : text;
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
            return ApplyInterfaceIntrusionIfNeeded(ExpandNarrativeTokens(resolved));
        }

        /// <summary>
        /// Switch the active language and notify subscribers.
        /// </summary>
        public void SetLanguage(GameLanguage language)
        {
            bool savedChanged = _savedLanguage != language;
            _savedLanguage = language;

            if (savedChanged)
                SavePersistentLanguagePreference(language);

            if (_transientLanguageOverrideActive)
                return;

            if (CurrentLanguage == language && !_intrusionGlyphModeActive)
                return;

            CurrentLanguage = language;
            _intrusionGlyphModeActive = false;
            PublishVisualLanguageState();

#if UNITY_EDITOR
            Debug.Log($"[Localization] Language changed to: {language}");
#endif
        }

        /// <summary>
        /// Applies a temporary visual language override without touching persisted player options.
        /// Intended for runtime-only diegetic interference such as PDA intrusion states.
        /// </summary>
        /// <param name="language">Visual language override to expose to subscribers.</param>
        /// <param name="enableGlyphMode">True to additionally corrupt visible text into glitched glyph output.</param>
        public void SetTransientLanguageOverride(GameLanguage language, bool enableGlyphMode = false)
        {
            bool languageChanged = !_transientLanguageOverrideActive || CurrentLanguage != language;
            bool glyphChanged = _intrusionGlyphModeActive != enableGlyphMode;

            _transientLanguageOverrideActive = true;
            _intrusionGlyphModeActive = enableGlyphMode;
            CurrentLanguage = language;

            if (!languageChanged && !glyphChanged)
                return;

            PublishVisualLanguageState();
        }

        /// <summary>
        /// Clears the temporary visual language override and restores the persisted player language.
        /// </summary>
        public void ClearTransientLanguageOverride()
        {
            if (!_transientLanguageOverrideActive && !_intrusionGlyphModeActive && CurrentLanguage == _savedLanguage)
                return;

            _transientLanguageOverrideActive = false;
            _intrusionGlyphModeActive = false;
            CurrentLanguage = _savedLanguage;
            PublishVisualLanguageState();
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

            if (CurrentLanguage == language || language == GameLanguage.English)
                RefreshRuntimeRegistry();

            if (CurrentLanguage == language)
                LocalizationEvents.PublishLanguageChanged(language);

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
            InputManager input = GlobalRegistry.NativeInputManager;
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

            _cachedPlayerToolManager = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null) ? Hecton8.Core.GlobalRegistry.Player.ToolManager : playerTransform.GetComponent<PlayerToolManager>());
            return _cachedPlayerToolManager;
        }

        private float ResolveHullStressCorruptionIntensity()
        {
            HectonPlayerMovement playerMovement = ResolvePlayerMovement();
            _cachedHullStress01 = 0f;
            if (playerMovement == null)
                return 0f;

            float hullStress = Mathf.Clamp01(playerMovement.CurrentHullStress01);
            _cachedHullStress01 = hullStress;
            if (hullStress <= HullStressCorruptionThreshold)
                return 0f;

            return Mathf.InverseLerp(HullStressCorruptionThreshold, 1f, hullStress);
        }

        private float ResolveExternalPdaCorrosionIntensity()
        {
            if (_externalPdaCorrosionEndTime <= Time.unscaledTime)
            {
                _externalPdaCorrosionEndTime = 0f;
                _externalPdaCorrosionIntensity = 0f;
                return 0f;
            }

            return _externalPdaCorrosionIntensity;
        }

        private bool TryResolveMadnessOverride(string sourceToken, out string madnessText)
        {
            madnessText = string.Empty;
            EvaluateMadnessOverrideState();
            if (!IsMadnessOverrideActive())
                return false;

            string normalizedSourceToken = string.IsNullOrEmpty(sourceToken) ? "<null>" : sourceToken;
            if (_lastMadnessResolvedWindowId == _madnessActiveWindowId &&
                _lastMadnessResolvedLanguage == CurrentLanguage &&
                string.Equals(_lastMadnessResolvedSourceToken, normalizedSourceToken, StringComparison.Ordinal))
            {
                madnessText = _lastMadnessResolvedValue;
                if (!string.IsNullOrEmpty(madnessText))
                    TriggerMadnessWhisperAudioIfNeeded();

                return !string.IsNullOrEmpty(madnessText);
            }

            int seed = ComputeMadnessSeed(normalizedSourceToken, _madnessActiveWindowId, (int)CurrentLanguage);
            string madnessKey = ResolveMadnessWhisperKey(seed);
            if (!TryGet(CurrentLanguage, madnessKey, out madnessText) || string.IsNullOrEmpty(madnessText))
                return false;

            madnessText = ExpandRuntimeTokens(madnessText);
            _lastMadnessResolvedWindowId = _madnessActiveWindowId;
            _lastMadnessResolvedLanguage = CurrentLanguage;
            _lastMadnessResolvedSourceToken = normalizedSourceToken;
            _lastMadnessResolvedValue = madnessText;
            TriggerMadnessWhisperAudioIfNeeded();
            return !string.IsNullOrEmpty(madnessText);
        }

        private bool TryResolveMadnessOverride(ReadOnlySpan<char> sourceToken, char[] destination, out int length)
        {
            length = 0;
            if (destination == null || destination.Length == 0)
                return false;

            EvaluateMadnessOverrideState();
            if (!IsMadnessOverrideActive())
                return false;

            ReadOnlySpan<char> normalizedSourceToken = sourceToken.Length == 0 ? "<null>".AsSpan() : sourceToken;
            int seed = 17;
            for (int i = 0; i < normalizedSourceToken.Length; i++)
                seed = (seed * 31) + normalizedSourceToken[i];

            seed = (seed * 31) + _madnessActiveWindowId;
            seed = (seed * 31) + (int)CurrentLanguage;
            int keyHash = MadnessWhisperKeyHashes[(seed & int.MaxValue) % MadnessWhisperKeyHashes.Length];
            if (!LocRegistry.TryGetRawBuffer(keyHash, out char[] rawBuffer, out int rawLength) || rawLength <= 0)
                return false;

            length = CopySpanToBuffer(rawBuffer.AsSpan(0, rawLength), destination);
            if (length > 0)
                TriggerMadnessWhisperAudioIfNeeded();

            return length > 0;
        }

        /// <summary>
        /// True while the active madness whisper replacement window is live for PDA lore surfaces.
        /// </summary>
        internal bool IsMadnessWhisperVisualActive()
        {
            EvaluateMadnessOverrideState();
            return IsMadnessOverrideActive();
        }

        /// <summary>
        /// Resolves a deterministic localized madness whisper for explicit UI hallucination beats without
        /// mutating the active madness override window.
        /// </summary>
        internal bool TryResolveMadnessWhisperPreview(string sourceToken, int cycle, out string madnessText)
        {
            madnessText = string.Empty;
            string normalizedSourceToken = string.IsNullOrEmpty(sourceToken) ? "<null>" : sourceToken;
            int seed = ComputeMadnessSeed(normalizedSourceToken, cycle, (int)CurrentLanguage);
            string madnessKey = ResolveMadnessWhisperKey(seed);
            if (!TryGet(CurrentLanguage, madnessKey, out madnessText) || string.IsNullOrEmpty(madnessText))
                return false;

            madnessText = ExpandRuntimeTokens(madnessText);
            return !string.IsNullOrEmpty(madnessText);
        }

        private void EvaluateMadnessOverrideState()
        {
            float intensity = GetHullStressCorruptionIntensity();
            float now = Time.unscaledTime;
            bool isActive = now < _madnessOverrideEndTime;

            if (!IsMadnessEligible())
            {
                if (isActive)
                    ClearMadnessOverride();

                return;
            }

            if (isActive)
                return;

            int rollBucket = Mathf.FloorToInt(now / MadnessRollInterval);
            if (rollBucket == _madnessLastRollBucket)
                return;

            _madnessLastRollBucket = rollBucket;

            DepthZoneDirector depthZoneDirector = GlobalRegistry.DepthZone;
            DepthZoneProfile currentZone = depthZoneDirector != null
                ? depthZoneDirector.CurrentZone
                : null;
            string zoneToken = currentZone != null && !string.IsNullOrWhiteSpace(currentZone.zoneId)
                ? currentZone.zoneId
                : DeepAbyssZoneId;
            int seed = ComputeMadnessSeed(zoneToken, rollBucket, Mathf.RoundToInt(intensity * 100f) + (int)CurrentLanguage);
            if (((seed & int.MaxValue) % 100) >= MadnessChancePercent)
                return;

            _madnessActiveWindowId = rollBucket;
            _madnessOverrideEndTime = now + MadnessBlinkDuration;
        }

        private bool IsMadnessEligible()
        {
            if (_cachedHullStress01 >= MadnessEligibilityThreshold)
                return true;

            if (IsInDeadZoneContext())
                return true;

            DepthZoneDirector director = GlobalRegistry.DepthZone;
            DepthZoneProfile currentZone = director != null ? director.CurrentZone : null;
            return currentZone != null &&
                   string.Equals(currentZone.zoneId, DeepAbyssZoneId, StringComparison.Ordinal);
        }

        private bool IsInDeadZoneContext()
        {
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge == null)
                return false;

            Transform playerTransform = null;
            HectonPlayerMovement playerMovement = ResolvePlayerMovement();
            if (playerMovement != null)
            {
                playerTransform = playerMovement.transform;
            }
            else if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform resolvedTransform))
            {
                playerTransform = resolvedTransform;
            }

            if (playerTransform == null)
                return false;

            HectonMapMagicVegetationBridge.VegetationDensitySample densitySample = bridge.GetVegetationDensity(playerTransform.position);
            return densitySample.BiomeLayer == HectonMapMagicVegetationBridge.VegetationBiomeLayer.DeadZone;
        }

        private bool IsMadnessOverrideActive()
        {
            if (Time.unscaledTime < _madnessOverrideEndTime)
                return true;

            if (_madnessActiveWindowId >= 0)
                ClearMadnessOverride();

            return false;
        }

        private void ClearMadnessOverride()
        {
            _madnessOverrideEndTime = 0f;
            _madnessActiveWindowId = -1;
            _lastMadnessAudioWindowId = -1;
            _lastMadnessResolvedWindowId = -1;
            _lastMadnessResolvedLanguage = (GameLanguage)(-1);
            _lastMadnessResolvedSourceToken = string.Empty;
            _lastMadnessResolvedValue = string.Empty;
        }

        private void TriggerMadnessWhisperAudioIfNeeded()
        {
            if (_madnessActiveWindowId < 0 || _lastMadnessAudioWindowId == _madnessActiveWindowId)
                return;

            AcousticZoneController controller = GlobalRegistry.AcousticZone;
            if (controller == null)
                return;

            _lastMadnessAudioWindowId = _madnessActiveWindowId;
            controller.PlayMadnessWhisperCue();
        }

        private int PublishVisualBucket(int bucket)
        {
            if (_lastPublishedVisualBucket == bucket)
                return bucket;

            _lastPublishedVisualBucket = bucket;
            LocalizationEvents.PublishCorruptionVisualStateChanged(CurrentLanguage, bucket);
            return bucket;
        }

        private static int ComputeMadnessSeed(string sourceToken, int cycle, int languageIndex)
        {
            unchecked
            {
                string token = string.IsNullOrEmpty(sourceToken) ? "<null>" : sourceToken;
                int hash = 17;
                for (int i = 0; i < token.Length; i++)
                    hash = (hash * 31) + token[i];

                hash = (hash * 31) + cycle;
                hash = (hash * 31) + languageIndex;
                return hash & int.MaxValue;
            }
        }

        private static int ComputeMadnessSeed(ReadOnlySpan<char> sourceToken, int cycle, int languageIndex)
        {
            unchecked
            {
                ReadOnlySpan<char> token = sourceToken.Length == 0 ? "<null>".AsSpan() : sourceToken;
                int hash = 17;
                for (int i = 0; i < token.Length; i++)
                    hash = (hash * 31) + token[i];

                hash = (hash * 31) + cycle;
                hash = (hash * 31) + languageIndex;
                return hash & int.MaxValue;
            }
        }

        private static int ResolveMadnessWhisperKeyHash(int hash)
        {
            int index = (hash & int.MaxValue) % MadnessWhisperKeyHashes.Length;
            return MadnessWhisperKeyHashes[index];
        }

        private static string ResolveMadnessWhisperKey(int hash)
        {
            switch ((hash & int.MaxValue) % 15)
            {
                case 0:
                    return LocalizationKeys.MADNESS_WHISPERS_01;
                case 1:
                    return LocalizationKeys.MADNESS_WHISPERS_02;
                case 2:
                    return LocalizationKeys.MADNESS_WHISPERS_03;
                case 3:
                    return LocalizationKeys.MADNESS_WHISPERS_04;
                case 4:
                    return LocalizationKeys.MADNESS_WHISPERS_05;
                case 5:
                    return LocalizationKeys.MADNESS_WHISPERS_06;
                case 6:
                    return LocalizationKeys.MADNESS_WHISPERS_07;
                case 7:
                    return LocalizationKeys.MADNESS_WHISPERS_08;
                case 8:
                    return LocalizationKeys.MADNESS_WHISPERS_09;
                case 9:
                    return LocalizationKeys.MADNESS_WHISPERS_10;
                case 10:
                    return LocalizationKeys.MADNESS_WHISPERS_11;
                case 11:
                    return LocalizationKeys.MADNESS_WHISPERS_12;
                case 12:
                    return LocalizationKeys.MADNESS_WHISPERS_13;
                case 13:
                    return LocalizationKeys.MADNESS_WHISPERS_14;
                default:
                    return LocalizationKeys.MADNESS_WHISPERS_15;
            }
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

                builder.Append(ResolveCorruptionGlyph(hash, alphabet, !IsRightToLeftLanguage(language)));
                previousCorrupted = true;
            }

            string corrupted = builder.ToString();
            StringBuilderPool.Return(builder);
            return corrupted;
        }

        private static bool TryCorruptVisibleText(
            ReadOnlySpan<char> text,
            float intensity,
            GameLanguage language,
            char[] destination,
            out int length)
        {
            length = 0;
            if (destination == null || destination.Length == 0)
                return false;

            if (text.Length == 0 || intensity <= 0f)
            {
                length = CopySpanToBuffer(text, destination);
                return true;
            }

            string alphabet = GetCorruptionAlphabet(language);
            if (string.IsNullOrEmpty(alphabet))
            {
                length = CopySpanToBuffer(text, destination);
                return true;
            }

            int threshold = Mathf.RoundToInt(Mathf.Lerp(0f, 700f, Mathf.Clamp01(intensity)));
            if (threshold <= 0)
            {
                length = CopySpanToBuffer(text, destination);
                return true;
            }

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
                    AppendCharToBuffer(current, destination, ref length);
                    continue;
                }

                if (insideRichTag)
                {
                    AppendCharToBuffer(current, destination, ref length);
                    if (current == '>')
                        insideRichTag = false;
                    continue;
                }

                if (current == '[' && TryAppendBracketedMarker(text, ref i, destination, ref length))
                {
                    previousCorrupted = false;
                    continue;
                }

                if (!ShouldCorruptCharacter(current))
                {
                    previousCorrupted = false;
                    AppendCharToBuffer(current, destination, ref length);
                    continue;
                }

                int hash = seed ^ (visibleIndex * 486187739);
                visibleIndex++;
                bool shouldCorrupt = !previousCorrupted && (hash & 1023) < threshold;
                if (!shouldCorrupt)
                {
                    AppendCharToBuffer(current, destination, ref length);
                    previousCorrupted = false;
                    continue;
                }

                AppendCharToBuffer(ResolveCorruptionGlyph(hash, alphabet, !IsRightToLeftLanguage(language)), destination, ref length);
                previousCorrupted = true;
            }

            return true;
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

        private static bool TryAppendBracketedMarker(ReadOnlySpan<char> text, ref int index, char[] destination, ref int length)
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
                AppendCharToBuffer(text[markerStart], destination, ref length);
                return false;
            }

            for (int i = markerStart; i <= current; i++)
                AppendCharToBuffer(text[i], destination, ref length);

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

        private static int ComputeCorruptionSeed(ReadOnlySpan<char> text)
        {
            unchecked
            {
                int seed = 17 ^ Mathf.RoundToInt(Time.unscaledTime * 12f);
                for (int i = 0; i < text.Length; i++)
                    seed = (seed * 31) + text[i];
                return seed;
            }
        }

        private static int CopyStringToBuffer(string source, char[] destination)
        {
            return CopySpanToBuffer(string.IsNullOrEmpty(source) ? ReadOnlySpan<char>.Empty : source.AsSpan(), destination);
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> source, char[] destination)
        {
            if (destination == null || destination.Length == 0 || source.Length == 0)
                return 0;

            int length = source.Length <= destination.Length ? source.Length : destination.Length;
            for (int i = 0; i < length; i++)
                destination[i] = source[i];

            return length;
        }

        private static void AppendCharToBuffer(char value, char[] destination, ref int length)
        {
            if (destination == null || length >= destination.Length)
                return;

            destination[length++] = value;
        }

        private static char ResolveCorruptionGlyph(int hash, string alphabet, bool allowNeutralBlocks)
        {
            if (allowNeutralBlocks && (hash & 7) == 0)
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
            UserOptionsPersistence options = Hecton8.Core.GlobalRegistry.UserOptions;
            if (options != null && options.HasKey(PrefsLanguageKey))
            {
                int saved = options.GetInt(PrefsLanguageKey, (int)defaultLanguage);
                if (Enum.IsDefined(typeof(GameLanguage), saved))
                {
                    _savedLanguage = (GameLanguage)saved;
                    CurrentLanguage = _savedLanguage;
                    return;
                }

                _savedLanguage = defaultLanguage;
                CurrentLanguage = _savedLanguage;
                return;
            }

            _savedLanguage = defaultLanguage;
            CurrentLanguage = _savedLanguage;
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

        private void PublishVisualLanguageState()
        {
            _lastPublishedVisualBucket = int.MinValue;
            RefreshRuntimeRegistry();
            LocalizationEvents.PublishLanguageChanged(CurrentLanguage);
            LocalizationEvents.PublishCorruptionVisualStateChanged(CurrentLanguage, _lastPublishedVisualBucket);
        }

        private void RefreshRuntimeRegistry()
        {
            LocRegistry.Reload(_tables, CurrentLanguage);
        }

        private static void SavePersistentLanguagePreference(GameLanguage language)
        {
            UserOptionsPersistence options = Hecton8.Core.GlobalRegistry.UserOptions;
            if (options == null)
                return;

            options.SetInt(PrefsLanguageKey, (int)language);
            options.Save();
        }

        private string ApplyInterfaceIntrusionIfNeeded(string text)
        {
            if (string.IsNullOrEmpty(text) || !_intrusionGlyphModeActive)
                return text ?? string.Empty;

            return CorruptExpandedText(text, 0.98f);
        }
    }
}
