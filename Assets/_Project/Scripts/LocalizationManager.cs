#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_BABEL_MMF_AVAILABLE
#endif

using System;
using System.IO;
#if HECTON8_BABEL_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Threading;
using System.Text.RegularExpressions;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Data;
using Hecton8.Gameplay;
using Hecton8.Input;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

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
    /// Runtime owner for Babel localization compatibility and language switching.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizationManager : MonoBehaviour, IBabelLocalization, IDispatcherSystem
    {
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
        private const int GameLanguageCount = (int)GameLanguage.Arabic + 1;
        private const uint BabelLocaleSwapSystemHash = 0xBABA0039u;
        private const int BabelLocaleSwapIdle = 0;
        private const int BabelLocaleSwapReading = 1;
        private const int BabelLocaleSwapReady = 2;
        private const int BabelLocaleSwapFailed = -1;
        private const int BabelLocaleReadFaultNone = 0;
        private const int BabelLocaleReadFaultMissing = 1;
        private const int BabelLocaleReadFaultShortRead = 2;
        private const int BabelLocaleReadFaultNullDestination = 3;
        private const int BabelLocaleReadFaultException = 4;
        private const int BabelLocaleReadChunkBytes = 64 * 1024;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float BabelOverrideCsvPollSeconds = 0.5f;
#endif
        private static readonly uint _missingLocalizationWarningHash = unchecked((uint)LocHash.Compute("LocalizationManager.MissingKey"));
        private static readonly uint _formatStringApiWarningHash = unchecked((uint)LocHash.Compute("LocalizationManager.FormatStringApi"));
        private static readonly uint _corruptionStringApiWarningHash = unchecked((uint)LocHash.Compute("LocalizationManager.CorruptionStringApi"));
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
        private const string CyrillicCorruptionAlphabet = "ABVGDEZhZIYKLMNOPRSTUFHTsChShSchYEYuYa";
        private const string ArabicCorruptionAlphabet = "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";
        private const string CjkCorruptionAlphabet = "深海圧壳酸氧流核域警号層站影断障";
        private const string HangulCorruptionAlphabet = "심해압력산소전력균열경보파손격리영역장치";
        private const string DevanagariCorruptionAlphabet = "अआइईउऊकखगघचछजझटठडढतथदधनपफबभमयरलवशसह";

        [Header("=== Config ===")]
        [SerializeField] private GameLanguage defaultLanguage = GameLanguage.English;

        // Shared user-options key owned by UserOptionsPersistence.
        private const string PrefsLanguageKey = UserOptionsPersistence.LanguageKey;

        private PlayerToolManager _cachedPlayerToolManager;
        private HectonPlayerMovement _cachedPlayerMovement;
        private int _cachedAnalyzerFrame = -1;
        private bool _cachedAnalyzerInstalled;
        private int _cachedHullStressFrame = -1;
        private float _cachedHullStress01;
        private float _cachedHullStressCorruptionIntensity;
        private float _externalPdaCorrosionIntensity;
        private uint _externalPdaCorrosionEndFrame;
        private GameLanguage _savedLanguage = GameLanguage.English;
        private bool _transientLanguageOverrideActive;
        private bool _intrusionGlyphModeActive;
        private uint _madnessOverrideEndFrame;
        private int _madnessActiveWindowId = -1;
        private int _madnessLastRollBucket = int.MinValue;
        private int _lastMadnessAudioWindowId = -1;
        private int _lastPublishedVisualBucket = int.MinValue;
        private int _lastMadnessResolvedWindowId = -1;
        private GameLanguage _lastMadnessResolvedLanguage = (GameLanguage)(-1);
        private string _lastMadnessResolvedSourceToken = string.Empty;
        private string _lastMadnessResolvedValue = string.Empty;
        private bool _registeredLocalizationRuntime;
        private bool _registeredBabelLocalizationRuntime;
        private BabelDictionaryStage _pendingBabelStage;
        private GameLanguage _pendingBabelLanguage;
        private GameLanguage _currentLanguage = GameLanguage.English;
        private int _pendingBabelSwapState;
        private int _pendingBabelReadFault;
        private bool _registeredBabelDispatcher;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string _overrideCsvPath;
        private long _overrideCsvLastWriteTicks;
        private float _overrideCsvPollTimer;
#endif
        /// <summary>
        /// Active localization owner published by the runtime owner after registry registration.
        /// </summary>
        public static LocalizationManager ActiveRuntimeInstance { get; private set; }


        /// <summary>
        /// Active language for runtime lookups.
        /// </summary>
        public GameLanguage CurrentLanguage => _currentLanguage;

        /// <inheritdoc />
        public ushort ActiveLanguageId => (ushort)_currentLanguage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            LocalizationManager registered = GlobalRegistry.Localization;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterLocalizationRuntime(this);
            _registeredLocalizationRuntime = ReferenceEquals(GlobalRegistry.Localization, this);
            if (_registeredLocalizationRuntime)
                ActiveRuntimeInstance = this;
            GlobalRegistry.RegisterBabelLocalizationRuntime(this);
            _registeredBabelLocalizationRuntime = ReferenceEquals(GlobalRegistry.BabelLocalization, this);
            GameBootstrapper.PersistRuntimeService(this);

            if (!TryGetComponent<FontStreamingManager>(out _))
                gameObject.AddComponent<FontStreamingManager>(); // COLD ALLOC: FontStreamingManager[1] — runtime staged localized font swap owner — owner: LocalizationManager

            LoadLegacyCompatibilityTables();
            RestoreSavedLanguage();
            RefreshRuntimeRegistry();
            TryRegisterBabelDispatcher();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (_registeredBabelLocalizationRuntime)
            {
                GlobalRegistry.UnregisterBabelLocalizationRuntime(this);
                _registeredBabelLocalizationRuntime = false;
            }

            if (_registeredLocalizationRuntime)
            {
                GlobalRegistry.UnregisterLocalizationRuntime(this);
                _registeredLocalizationRuntime = false;
            }

            if (_registeredBabelDispatcher)
            {
                GlobalRegistry.UnregisterDispatcherSystem(this);
                _registeredBabelDispatcher = false;
            }

            LocRegistry.AbortBabelDictionaryStage(in _pendingBabelStage);
            _pendingBabelStage = default;
            Volatile.Write(ref _pendingBabelSwapState, BabelLocaleSwapIdle);
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

            GlobalTelemetryBus.PublishPerformanceWarning(
                _missingLocalizationWarningHash,
                unchecked((uint)LocHash.Compute(key)),
                (float)CurrentLanguage);
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

        /// <inheritdoc />
        public bool TryGetLocalizedSpan(uint hash, out ReadOnlySpan<byte> utf8Bytes)
        {
            return LocRegistry.TryGetLocalizedSpan(hash, out utf8Bytes);
        }

        /// <inheritdoc />
        public bool TryGetLocalizedBuffer(uint hash, out char[] buffer, out int length)
        {
            return LocRegistry.TryGetVisualBufferFromUtf8(unchecked((int)hash), out buffer, out length);
        }

        /// <inheritdoc />
        public bool TryWriteLocalizedInt(uint templateHash, int value, Span<char> destination, out int length)
        {
            return LocNumericBuffer.TryWrite(
                LocRegistry.ResolveRaw(unchecked((int)templateHash)),
                destination,
                LocNumericArg.Int(value),
                out length);
        }

        /// <inheritdoc />
        public uint ResolvePluralHash(uint singularHash, uint pluralHash, int value)
        {
            return value == 1 ? singularHash : pluralHash;
        }

        /// <summary>
        /// Resolve a formatted localized string.
        /// </summary>
        public string GetFormatted(string key, params object[] args)
        {
            string template = Get(key);
            return FormatLocalized(template, key, args);
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

            uint nowFrame = ResolvePresentationAudioFrame();
            uint requestedEndFrame = AddAudioFramesWrapped(nowFrame, SecondsToAudioFrames(clampedDuration));
            _externalPdaCorrosionIntensity = Mathf.Max(_externalPdaCorrosionIntensity, clampedIntensity);
            if (!IsAudioFrameBefore(requestedEndFrame, _externalPdaCorrosionEndFrame))
                _externalPdaCorrosionEndFrame = requestedEndFrame;
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
                : ResolveMadnessRollBucket();
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
                : ResolveMadnessRollBucket();
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

            if (language == CurrentLanguage &&
                TryGetBabelLegacyString(LocHash.Compute(key.AsSpan()), out value))
            {
                return true;
            }

            if (TryGetFromTable(language, key, out value))
                return true;

            if (language != GameLanguage.English && TryGetFromTable(GameLanguage.English, key, out value))
                return true;

            value = string.Empty;
            return false;
        }

        private static bool TryGetBabelLegacyString(int keyHash, out string value)
        {
            value = string.Empty;
            if (keyHash == 0)
                return false;

            if (!LocRegistry.TryGetVisualBufferFromUtf8(keyHash, out char[] buffer, out int length) ||
                buffer == null ||
                length <= 0)
            {
                return false;
            }

            // Legacy string API only. Zero-GC UI paths use LocRegistry.TryWriteVisualSpanFromUtf8.
            value = new string(buffer, 0, length);
            return true;
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

            _currentLanguage = language;
            _intrusionGlyphModeActive = false;
            PublishVisualLanguageState();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Localization] Language changed to: {language}");
#endif
        }

        /// <summary>
        /// Reads a Babel binary off the main thread and commits the pointer swap in POST_SIMULATION.
        /// </summary>
        public async Awaitable SetLanguageAsync(GameLanguage language, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (_transientLanguageOverrideActive)
            {
                SetLanguage(language);
                return;
            }

            if (TryPrepareBabelLocaleSwap(language, out string path, out BabelDictionaryStage stage))
            {
                await RunBabelLocaleSwapAsync(language, path, stage, cancellationToken);
                return;
            }

            await AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            SetLanguage(language);
        }

        public uint GetSystemIdHash()
        {
            return BabelLocaleSwapSystemHash;
        }

        public DispatcherPhase GetDispatcherPhase()
        {
            return DispatcherPhase.PostSimulation;
        }

        public byte GetBucketId()
        {
            return byte.MaxValue;
        }

        public int GetDependencyCount()
        {
            return 0;
        }

        public uint GetDependencyHash(int dependencyIndex)
        {
            return 0u;
        }

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            return dependsOn;
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            CommitPendingBabelSwapIfReady();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PollBabelOverrideCsv(timing.FrameDelta);
#endif
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void PollBabelOverrideCsv(float frameDelta)
        {
            float safeDelta = frameDelta > 0f && frameDelta < 10f ? frameDelta : Time.unscaledDeltaTime;
            _overrideCsvPollTimer -= safeDelta;
            if (_overrideCsvPollTimer > 0f)
                return;

            _overrideCsvPollTimer = BabelOverrideCsvPollSeconds;
            if (string.IsNullOrEmpty(_overrideCsvPath))
                _overrideCsvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "loc_overrides.csv"));

            FileInfo info;
            try
            {
                info = new FileInfo(_overrideCsvPath);
            }
            catch (Exception)
            {
                return;
            }

            if (!info.Exists)
                return;

            long writeTicks = info.LastWriteTimeUtc.Ticks;
            if (writeTicks == _overrideCsvLastWriteTicks)
                return;

            if (LocRegistry.TryApplyLocOverridesCsv(_overrideCsvPath, out _, out _))
                _overrideCsvLastWriteTicks = writeTicks;
        }

        private void ResetBabelOverrideCsvMonitor()
        {
            _overrideCsvLastWriteTicks = 0L;
            _overrideCsvPollTimer = 0f;
        }
#endif

        private void TryRegisterBabelDispatcher()
        {
            if (_registeredBabelDispatcher)
                return;

            _registeredBabelDispatcher = GlobalRegistry.TryRegisterDispatcherSystem(this);
        }

        private bool TryPrepareBabelLocaleSwap(
            GameLanguage language,
            out string path,
            out BabelDictionaryStage stage)
        {
            path = null;
            stage = default;

            if (Volatile.Read(ref _pendingBabelSwapState) != BabelLocaleSwapIdle)
                return false;

            if (!TryResolveBabelLocalePath(language, out path))
                return false;

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch (Exception)
            {
                return false;
            }

            if (!info.Exists ||
                info.Length < 32L ||
                info.Length > int.MaxValue ||
                !LocRegistry.TryBeginBabelDictionaryStage((int)info.Length, language, out stage))
            {
                return false;
            }

            _pendingBabelLanguage = language;
            _pendingBabelReadFault = BabelLocaleReadFaultNone;
            Volatile.Write(ref _pendingBabelSwapState, BabelLocaleSwapReading);
            return true;
        }

        private async Awaitable RunBabelLocaleSwapAsync(
            GameLanguage language,
            string path,
            BabelDictionaryStage stage,
            CancellationToken cancellationToken)
        {
            int fault = BabelLocaleReadFaultNone;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                fault = cancellationToken.IsCancellationRequested
                    ? BabelLocaleReadFaultMissing
                    : ReadBabelDictionaryIntoStage(path, in stage);
            }
            catch (Exception)
            {
                fault = BabelLocaleReadFaultException;
            }

            await Awaitable.MainThreadAsync();
            if (cancellationToken.IsCancellationRequested || fault != BabelLocaleReadFaultNone)
            {
                _pendingBabelReadFault = fault;
                Volatile.Write(ref _pendingBabelSwapState, BabelLocaleSwapFailed);
                LocRegistry.AbortBabelDictionaryStage(in stage);
                _pendingBabelStage = default;
                Volatile.Write(ref _pendingBabelSwapState, BabelLocaleSwapIdle);
                SetLanguage(language);
                return;
            }

            _pendingBabelStage = stage;
            _pendingBabelLanguage = language;
            _pendingBabelReadFault = BabelLocaleReadFaultNone;
            Volatile.Write(ref _pendingBabelSwapState, BabelLocaleSwapReady);

            if (!_registeredBabelDispatcher)
                CommitPendingBabelSwapIfReady();
        }

        private void CommitPendingBabelSwapIfReady()
        {
            if (Volatile.Read(ref _pendingBabelSwapState) != BabelLocaleSwapReady)
                return;

            BabelDictionaryStage stage = _pendingBabelStage;
            GameLanguage language = _pendingBabelLanguage;
            bool committed = LocRegistry.TryCommitStagedBabelDictionary(in stage);
            _pendingBabelStage = default;
            Volatile.Write(ref _pendingBabelSwapState, BabelLocaleSwapIdle);

            if (!committed)
            {
                SetLanguage(language);
                return;
            }

            ApplyCommittedBabelLanguage(language);
        }

        private void ApplyCommittedBabelLanguage(GameLanguage language)
        {
            bool savedChanged = _savedLanguage != language;
            _savedLanguage = language;
            if (savedChanged)
                SavePersistentLanguagePreference(language);

            _currentLanguage = language;
            _transientLanguageOverrideActive = false;
            _intrusionGlyphModeActive = false;
            _lastPublishedVisualBucket = int.MinValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResetBabelOverrideCsvMonitor();
#endif
            LocalizationEvents.PublishLanguageChanged(CurrentLanguage);
            LocalizationEvents.PublishCorruptionVisualStateChanged(CurrentLanguage, _lastPublishedVisualBucket);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Localization] Babel binary language changed to: {language}");
#endif
        }

        private static bool TryResolveBabelLocalePath(GameLanguage language, out string path)
        {
            path = null;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string languageFileName = GetBabelLocaleFileName(language);

            if (TryResolveExistingPath(Path.Combine(projectRoot, "Data", "Localization", languageFileName), out path) ||
                TryResolveExistingPath(Path.Combine(projectRoot, "Data", "Balance", "Baked", languageFileName), out path) ||
                TryResolveExistingPath(Path.Combine(projectRoot, "Assets", "_Project", "Data", "Localization", languageFileName), out path) ||
                TryResolveExistingPath(Path.Combine(projectRoot, "Data", "Balance", "Baked", H8StaticDataFormat.BabelDictionaryFileName), out path) ||
                TryResolveExistingPath(Path.Combine(projectRoot, "Assets", "_Project", "Data", "Localization", H8StaticDataFormat.BabelDictionaryFileName), out path))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveExistingPath(string candidate, out string path)
        {
            path = null;
            if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
                return false;

            path = Path.GetFullPath(candidate);
            return true;
        }

        private static string GetBabelLocaleFileName(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Russian:
                    return "loc_strings_ru.h8bin";
                case GameLanguage.German:
                    return "loc_strings_de.h8bin";
                case GameLanguage.French:
                    return "loc_strings_fr.h8bin";
                case GameLanguage.Spanish:
                    return "loc_strings_es.h8bin";
                case GameLanguage.Italian:
                    return "loc_strings_it.h8bin";
                case GameLanguage.PortugueseBrazilian:
                    return "loc_strings_pt_br.h8bin";
                case GameLanguage.Polish:
                    return "loc_strings_pl.h8bin";
                case GameLanguage.Turkish:
                    return "loc_strings_tr.h8bin";
                case GameLanguage.Ukrainian:
                    return "loc_strings_uk.h8bin";
                case GameLanguage.ChineseSimplified:
                    return "loc_strings_zh_hans.h8bin";
                case GameLanguage.ChineseTraditional:
                    return "loc_strings_zh_hant.h8bin";
                case GameLanguage.Japanese:
                    return "loc_strings_ja.h8bin";
                case GameLanguage.Korean:
                    return "loc_strings_ko.h8bin";
                case GameLanguage.Hindi:
                    return "loc_strings_hi.h8bin";
                case GameLanguage.Indonesian:
                    return "loc_strings_id.h8bin";
                case GameLanguage.Arabic:
                    return "loc_strings_ar.h8bin";
                default:
                    return "loc_strings_en.h8bin";
            }
        }

        private static unsafe int ReadBabelDictionaryIntoStage(
            string path,
            in BabelDictionaryStage stage)
        {
            if (string.IsNullOrEmpty(path) ||
                stage.Destination == IntPtr.Zero ||
                stage.ByteLength <= 0 ||
                stage.SourceByteLength <= 0 ||
                stage.SourceByteLength > stage.ByteLength)
            {
                return BabelLocaleReadFaultNullDestination;
            }

#if HECTON8_BABEL_MMF_AVAILABLE
            int mmfFault = ReadBabelDictionaryWithMmf(path, in stage);
            if (mmfFault == BabelLocaleReadFaultNone)
                return BabelLocaleReadFaultNone;
#endif
            return ReadBabelDictionaryWithStream(path, in stage);
        }

#if HECTON8_BABEL_MMF_AVAILABLE
        private static unsafe int ReadBabelDictionaryWithMmf(
            string path,
            in BabelDictionaryStage stage)
        {
            try
            {
                using FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    BabelLocaleReadChunkBytes,
                    FileOptions.RandomAccess);
                if (stream.Length != stage.SourceByteLength)
                    return BabelLocaleReadFaultShortRead;

                using MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                    stream,
                    null,
                    stage.SourceByteLength,
                    MemoryMappedFileAccess.Read,
                    HandleInheritability.None,
                    false);
                using MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(
                    0L,
                    stage.SourceByteLength,
                    MemoryMappedFileAccess.Read);

                byte* source = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref source);
                try
                {
                    if (source == null)
                        return BabelLocaleReadFaultMissing;

                    byte* destination = (byte*)stage.Destination.ToPointer();
                    if (destination == null)
                        return BabelLocaleReadFaultNullDestination;

                    UnsafeUtility.MemCpy(destination, source + accessor.PointerOffset, stage.SourceByteLength);
                    if (stage.ByteLength > stage.SourceByteLength)
                        UnsafeUtility.MemClear(destination + stage.SourceByteLength, stage.ByteLength - stage.SourceByteLength);
                    return BabelLocaleReadFaultNone;
                }
                finally
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
            catch (Exception)
            {
                return BabelLocaleReadFaultException;
            }
        }
#endif

        private static unsafe int ReadBabelDictionaryWithStream(
            string path,
            in BabelDictionaryStage stage)
        {
            try
            {
                using FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    BabelLocaleReadChunkBytes,
                    FileOptions.SequentialScan);
                if (stream.Length != stage.SourceByteLength)
                    return BabelLocaleReadFaultShortRead;

                byte* destination = (byte*)stage.Destination.ToPointer();
                if (destination == null)
                    return BabelLocaleReadFaultNullDestination;

                int offset = 0;
                while (offset < stage.SourceByteLength)
                {
                    int chunkBytes = Math.Min(BabelLocaleReadChunkBytes, stage.SourceByteLength - offset);
                    int read = stream.Read(new Span<byte>(destination + offset, chunkBytes));
                    if (read <= 0)
                        return BabelLocaleReadFaultShortRead;

                    offset += read;
                }

                if (stage.ByteLength > stage.SourceByteLength)
                    UnsafeUtility.MemClear(destination + stage.SourceByteLength, stage.ByteLength - stage.SourceByteLength);
                return BabelLocaleReadFaultNone;
            }
            catch (FileNotFoundException)
            {
                return BabelLocaleReadFaultMissing;
            }
            catch (DirectoryNotFoundException)
            {
                return BabelLocaleReadFaultMissing;
            }
            catch (Exception)
            {
                return BabelLocaleReadFaultException;
            }
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
            _currentLanguage = language;

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
            _currentLanguage = _savedLanguage;
            PublishVisualLanguageState();
        }

        /// <summary>
        /// Cycle to the next language in the enum order.
        /// </summary>
        public void CycleLanguage()
        {
            int next = ((int)CurrentLanguage + 1) % GameLanguageCount;
            SetLanguage((GameLanguage)next);
        }

        private void LoadLegacyCompatibilityTables()
        {
            LoadBuiltInTables();
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

            LocalizationManager manager = GlobalRegistry.Localization;
            if (manager == null)
                return string.Empty;

            return manager.ResolveTechToken(match.Groups["token"].Value);
        }

        private static string EvaluateKeyToken(Match match)
        {
            if (match == null || !match.Success)
                return string.Empty;

            LocalizationManager manager = GlobalRegistry.Localization;
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

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return null;

            _cachedPlayerToolManager =
                Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null
                    ? Hecton8.Core.GlobalRegistry.Player.ToolManager
                    : ResolvePlayerToolManager(playerTransform);
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
            if (!IsAudioFrameBefore(ResolvePresentationAudioFrame(), _externalPdaCorrosionEndFrame))
            {
                _externalPdaCorrosionEndFrame = 0u;
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
            uint nowFrame = ResolvePresentationAudioFrame();
            bool isActive = IsAudioFrameBefore(nowFrame, _madnessOverrideEndFrame);

            if (!IsMadnessEligible())
            {
                if (isActive)
                    ClearMadnessOverride();

                return;
            }

            if (isActive)
                return;

            int rollBucket = ResolveMadnessRollBucket();
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
            _madnessOverrideEndFrame = AddAudioFramesWrapped(nowFrame, SecondsToAudioFrames(MadnessBlinkDuration));
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
            else if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform resolvedTransform))
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
            if (IsAudioFrameBefore(ResolvePresentationAudioFrame(), _madnessOverrideEndFrame))
                return true;

            if (_madnessActiveWindowId >= 0)
                ClearMadnessOverride();

            return false;
        }

        private static uint ResolvePresentationAudioFrame()
        {
            double frame = ResolvePresentationAudioFrameDouble();
            if (double.IsNaN(frame) || double.IsInfinity(frame) || frame <= 0d)
                return BabelSubtitleSyncRuntime.CurrentAudioFrame;

            frame %= (uint.MaxValue + 1d);
            if (frame < 0d)
                frame += uint.MaxValue + 1d;

            return (uint)frame;
        }

        private static double ResolvePresentationAudioFrameDouble()
        {
            int sampleRate = Mathf.Max(1, AudioSettings.outputSampleRate);
            return AudioSettings.dspTime * sampleRate;
        }

        private static uint SecondsToAudioFrames(float seconds)
        {
            double frames = Math.Ceiling(Mathf.Max(0f, seconds) * Mathf.Max(1, AudioSettings.outputSampleRate));
            if (frames <= 0d)
                return 0u;

            return frames >= int.MaxValue ? int.MaxValue - 1u : (uint)frames;
        }

        private static uint AddAudioFramesWrapped(uint frame, uint delta)
        {
            return frame + delta;
        }

        private static bool IsAudioFrameBefore(uint lhs, uint rhs)
        {
            return unchecked((int)(rhs - lhs)) > 0;
        }

        private static int ResolveMadnessRollBucket()
        {
            double intervalFrames = Math.Max(1d, MadnessRollInterval * Mathf.Max(1, AudioSettings.outputSampleRate));
            double bucket = ResolvePresentationAudioFrameDouble() / intervalFrames;
            if (double.IsNaN(bucket) || double.IsInfinity(bucket) || bucket <= 0d)
                return 0;

            return bucket >= int.MaxValue ? int.MaxValue : (int)bucket;
        }

        private static int ResolveCorruptionSeedBucket()
        {
            double sampleRate = Math.Max(1d, AudioSettings.outputSampleRate);
            double bucketFrames = Math.Max(1d, sampleRate / 12d);
            double bucket = ResolvePresentationAudioFrameDouble() / bucketFrames;
            if (double.IsNaN(bucket) || double.IsInfinity(bucket) || bucket <= 0d)
                return 0;

            return bucket >= int.MaxValue ? int.MaxValue : (int)bucket;
        }

        private void ClearMadnessOverride()
        {
            _madnessOverrideEndFrame = 0u;
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

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return null;

            playerTransform.TryGetComponent(out _cachedPlayerMovement);
            return _cachedPlayerMovement;
        }

        private static PlayerToolManager ResolvePlayerToolManager(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            return playerTransform.TryGetComponent(out PlayerToolManager toolManager) ? toolManager : null;
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

            int threshold = (int)(700f * intensity + 0.5f);
            if (threshold <= 0)
                return text;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return text;

            try
            {
                if (!lease.IsValid || text.Length > lease.Buffer.Length)
                    return text;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return TryCorruptVisibleText(text.AsSpan(), intensity, language, lease.Buffer, out int length) && length > 0
                    ? new string(lease.Buffer, 0, length)
                    : text;
#else
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _corruptionStringApiWarningHash,
                    unchecked((uint)language),
                    text.Length);
                return text;
#endif
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
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

            float clampedIntensity = Mathf.Clamp01(intensity);
            int threshold = (int)(700f * clampedIntensity + 0.5f);
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
                int seed = 17 ^ ResolveCorruptionSeedBucket();
                for (int i = 0; i < text.Length; i++)
                    seed = (seed * 31) + text[i];
                return seed;
            }
        }

        private static int ComputeCorruptionSeed(ReadOnlySpan<char> text)
        {
            unchecked
            {
                int seed = 17 ^ ResolveCorruptionSeedBucket();
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
            if (string.IsNullOrEmpty(template) || args == null || args.Length == 0)
                return template;

            if (TryMeasureLocalizedFormat(template, args, out int formattedLength))
                return string.Create(formattedLength, new LegacyFormatState(template, args), WriteLocalizedFormat);

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            GlobalTelemetryBus.PublishPerformanceWarning(
                _formatStringApiWarningHash,
                unchecked((uint)LocHash.Compute(key)),
                args.Length);
#else
            Debug.LogError(
                $"[Localization] Format fallback for key \"{key}\", template: \"{template}\", args count: {args.Length}");
#endif
            return template;
        }

        private static bool TryMeasureLocalizedFormat(string template, object[] args, out int formattedLength)
        {
            formattedLength = 0;
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        formattedLength++;
                        i++;
                        continue;
                    }

                    if (!TryParseFormatPlaceholder(
                            template,
                            i,
                            out int argIndex,
                            out int endIndex,
                            out int formatStart,
                            out int formatLength) ||
                        (uint)argIndex >= (uint)args.Length ||
                        !TryMeasureFormatArg(args[argIndex], template.AsSpan(formatStart, formatLength), out int argLength))
                    {
                        formattedLength = 0;
                        return false;
                    }

                    formattedLength += argLength;
                    i = endIndex;
                    continue;
                }

                if (c == '}')
                {
                    if (i + 1 < template.Length && template[i + 1] == '}')
                    {
                        formattedLength++;
                        i++;
                        continue;
                    }

                    formattedLength = 0;
                    return false;
                }

                formattedLength++;
            }

            return true;
        }

        private static void WriteLocalizedFormat(Span<char> destination, LegacyFormatState state)
        {
            int cursor = 0;
            string template = state.Template;
            object[] args = state.Args;
            for (int i = 0; i < template.Length && cursor < destination.Length; i++)
            {
                char c = template[i];
                if (c == '{')
                {
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        destination[cursor++] = '{';
                        i++;
                        continue;
                    }

                    if (TryParseFormatPlaceholder(
                            template,
                            i,
                            out int argIndex,
                            out int endIndex,
                            out int formatStart,
                            out int formatLength) &&
                        (uint)argIndex < (uint)args.Length &&
                        TryWriteFormatArg(
                            args[argIndex],
                            template.AsSpan(formatStart, formatLength),
                            destination.Slice(cursor),
                            out int written))
                    {
                        cursor += written;
                        i = endIndex;
                        continue;
                    }

                    return;
                }

                if (c == '}')
                {
                    if (i + 1 < template.Length && template[i + 1] == '}')
                    {
                        destination[cursor++] = '}';
                        i++;
                    }

                    continue;
                }

                destination[cursor++] = c;
            }
        }

        private static bool TryParseFormatPlaceholder(
            string template,
            int startIndex,
            out int argIndex,
            out int endIndex,
            out int formatStart,
            out int formatLength)
        {
            argIndex = 0;
            endIndex = startIndex;
            formatStart = 0;
            formatLength = 0;
            int cursor = startIndex + 1;
            if ((uint)cursor >= (uint)template.Length || !char.IsDigit(template[cursor]))
                return false;

            while ((uint)cursor < (uint)template.Length && char.IsDigit(template[cursor]))
            {
                argIndex = (argIndex * 10) + (template[cursor] - '0');
                cursor++;
            }

            if ((uint)cursor < (uint)template.Length && template[cursor] == ':')
            {
                cursor++;
                formatStart = cursor;
                while ((uint)cursor < (uint)template.Length && template[cursor] != '}')
                    cursor++;

                formatLength = cursor - formatStart;
            }

            if ((uint)cursor >= (uint)template.Length || template[cursor] != '}')
                return false;

            if (formatLength == 0)
                formatStart = cursor;
            endIndex = cursor;
            return true;
        }

        private static bool TryMeasureFormatArg(object arg, ReadOnlySpan<char> format, out int length)
        {
            length = 0;
            if (arg == null)
                return format.Length == 0;

            switch (arg)
            {
                case string value:
                    if (format.Length != 0)
                        return false;
                    length = value.Length;
                    return true;
                case char:
                    if (format.Length != 0)
                        return false;
                    length = 1;
                    return true;
                case bool value:
                    if (format.Length != 0)
                        return false;
                    length = value ? 4 : 5;
                    return true;
                case int value:
                    return TryMeasureInt(value, format, out length);
                case uint value:
                    return TryMeasureUInt(value, format, out length);
                case long value:
                    return TryMeasureLong(value, format, out length);
                case ulong value:
                    return TryMeasureULong(value, format, out length);
                case short value:
                    return TryMeasureInt(value, format, out length);
                case ushort value:
                    return TryMeasureUInt(value, format, out length);
                case byte value:
                    return TryMeasureUInt(value, format, out length);
                case sbyte value:
                    return TryMeasureInt(value, format, out length);
                case float value:
                    return TryMeasureFloat(value, format, out length);
                case double value:
                    return TryMeasureDouble(value, format, out length);
                case decimal value:
                    return TryMeasureDecimal(value, format, out length);
                default:
                    return false;
            }
        }

        private static bool TryWriteFormatArg(
            object arg,
            ReadOnlySpan<char> format,
            Span<char> destination,
            out int written)
        {
            written = 0;
            if (arg == null)
                return format.Length == 0;

            switch (arg)
            {
                case string value:
                    if (format.Length != 0)
                        return false;
                    if (!value.AsSpan().TryCopyTo(destination))
                        return false;
                    written = value.Length;
                    return true;
                case char value:
                    if (format.Length != 0)
                        return false;
                    if (destination.Length < 1)
                        return false;
                    destination[0] = value;
                    written = 1;
                    return true;
                case bool value:
                    if (format.Length != 0)
                        return false;
                    ReadOnlySpan<char> boolText = value ? "True".AsSpan() : "False".AsSpan();
                    if (!boolText.TryCopyTo(destination))
                        return false;
                    written = boolText.Length;
                    return true;
                case int value:
                    return value.TryFormat(destination, out written, format);
                case uint value:
                    return value.TryFormat(destination, out written, format);
                case long value:
                    return value.TryFormat(destination, out written, format);
                case ulong value:
                    return value.TryFormat(destination, out written, format);
                case short value:
                    return value.TryFormat(destination, out written, format);
                case ushort value:
                    return value.TryFormat(destination, out written, format);
                case byte value:
                    return value.TryFormat(destination, out written, format);
                case sbyte value:
                    return value.TryFormat(destination, out written, format);
                case float value:
                    return value.TryFormat(destination, out written, format);
                case double value:
                    return value.TryFormat(destination, out written, format);
                case decimal value:
                    return value.TryFormat(destination, out written, format);
                default:
                    return false;
            }
        }

        private static bool TryMeasureInt(int value, ReadOnlySpan<char> format, out int length)
        {
            Span<char> scratch = stackalloc char[16];
            return value.TryFormat(scratch, out length, format);
        }

        private static bool TryMeasureUInt(uint value, ReadOnlySpan<char> format, out int length)
        {
            Span<char> scratch = stackalloc char[16];
            return value.TryFormat(scratch, out length, format);
        }

        private static bool TryMeasureLong(long value, ReadOnlySpan<char> format, out int length)
        {
            Span<char> scratch = stackalloc char[32];
            return value.TryFormat(scratch, out length, format);
        }

        private static bool TryMeasureULong(ulong value, ReadOnlySpan<char> format, out int length)
        {
            Span<char> scratch = stackalloc char[32];
            return value.TryFormat(scratch, out length, format);
        }

        private static bool TryMeasureFloat(float value, ReadOnlySpan<char> format, out int length)
        {
            Span<char> scratch = stackalloc char[32];
            return value.TryFormat(scratch, out length, format);
        }

        private static bool TryMeasureDouble(double value, ReadOnlySpan<char> format, out int length)
        {
            Span<char> scratch = stackalloc char[32];
            return value.TryFormat(scratch, out length, format);
        }

        private static bool TryMeasureDecimal(decimal value, ReadOnlySpan<char> format, out int length)
        {
            Span<char> scratch = stackalloc char[64];
            return value.TryFormat(scratch, out length, format);
        }

        private readonly struct LegacyFormatState
        {
            public LegacyFormatState(string template, object[] args)
            {
                Template = template;
                Args = args;
            }

            public readonly string Template;
            public readonly object[] Args;
        }

        private void LoadBuiltInTables()
        {
            // Built-in compatibility strings are resolved by switch dispatch in TryGetFromTable.
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
                    _currentLanguage = _savedLanguage;
                    return;
                }

                _savedLanguage = defaultLanguage;
                _currentLanguage = _savedLanguage;
                return;
            }

            _savedLanguage = defaultLanguage;
            _currentLanguage = _savedLanguage;
        }

        private static bool TryGetFromTable(GameLanguage language, string key, out string value)
        {
            if (language == GameLanguage.Russian && TryGetBuiltInRussian(key, out value))
                return true;

            return TryGetBuiltInEnglish(key, out value);
        }

        private static bool TryGetBuiltInEnglish(string key, out string value)
        {
            switch (key)
            {
                case LocalizationKeys.MENU_NEW_GAME: value = "New Game"; return true;
                case LocalizationKeys.MENU_LOAD_GAME: value = "Load Game"; return true;
                case LocalizationKeys.MENU_SETTINGS: value = "Settings"; return true;
                case LocalizationKeys.MENU_QUIT: value = "Quit"; return true;
                case LocalizationKeys.MODAL_CONFIRM: value = "Confirm"; return true;
                case LocalizationKeys.MODAL_CANCEL: value = "Cancel"; return true;
                case LocalizationKeys.MODAL_NEW_GAME_TITLE: value = "New Game"; return true;
                case LocalizationKeys.MODAL_NEW_GAME_MESSAGE: value = "Start a new game?"; return true;
                case LocalizationKeys.MODAL_LOAD_TITLE: value = "Load Game"; return true;
                case LocalizationKeys.MODAL_LOAD_MESSAGE: value = "Load save \"{0}\"?"; return true;
                case LocalizationKeys.MODAL_QUIT_TITLE: value = "Quit"; return true;
                case LocalizationKeys.MODAL_QUIT_MESSAGE: value = "Quit the game?"; return true;
                case LocalizationKeys.SLOT_PREFIX: value = "SLOT"; return true;
                case LocalizationKeys.SLOT_NO_DATA: value = "NO DATA"; return true;
                case LocalizationKeys.SLOT_PLAYTIME: value = "Playtime"; return true;
                case LocalizationKeys.LOADING_PERCENT: value = "{0}%"; return true;
                default: value = string.Empty; return false;
            }
        }

        private static bool TryGetBuiltInRussian(string key, out string value)
        {
            switch (key)
            {
                case LocalizationKeys.MENU_NEW_GAME: value = "Novaya igra"; return true;
                case LocalizationKeys.MENU_LOAD_GAME: value = "Zagruzit"; return true;
                case LocalizationKeys.MENU_SETTINGS: value = "Nastroyki"; return true;
                case LocalizationKeys.MENU_QUIT: value = "Vyhod"; return true;
                case LocalizationKeys.MODAL_CONFIRM: value = "Podtverdit"; return true;
                case LocalizationKeys.MODAL_CANCEL: value = "Otmena"; return true;
                case LocalizationKeys.MODAL_NEW_GAME_TITLE: value = "Novaya igra"; return true;
                case LocalizationKeys.MODAL_NEW_GAME_MESSAGE: value = "Nachat novuyu igru?"; return true;
                case LocalizationKeys.MODAL_LOAD_TITLE: value = "Zagruzka"; return true;
                case LocalizationKeys.MODAL_LOAD_MESSAGE: value = "Zagruzit sohranenie \"{0}\"?"; return true;
                case LocalizationKeys.MODAL_QUIT_TITLE: value = "Vyhod"; return true;
                case LocalizationKeys.MODAL_QUIT_MESSAGE: value = "Vyyti iz igry?"; return true;
                case LocalizationKeys.SLOT_PREFIX: value = "SLOT"; return true;
                case LocalizationKeys.SLOT_NO_DATA: value = "NET DANNYH"; return true;
                case LocalizationKeys.SLOT_PLAYTIME: value = "Vremya igry"; return true;
                case LocalizationKeys.LOADING_PERCENT: value = "{0}%"; return true;
                default: value = string.Empty; return false;
            }
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
            LocRegistry.ReloadBinaryOrMock(CurrentLanguage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResetBabelOverrideCsvMonitor();
#endif
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
