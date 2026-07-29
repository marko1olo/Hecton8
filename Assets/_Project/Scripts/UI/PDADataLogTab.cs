// ============================================================================
// HECTON-8 - PDADataLogTab.cs
// PDA audio-log archive tab for discovered colony records.
//
// ROLE:
//   - Displays discovered AudioLogData entries.
//   - Allows replaying unlocked records.
//   - Shows subtitles for the current playback.
//   - Refreshes when the PDA opens, not every frame - zero GC.
//
// ARCHITECTURE:
//   - Procedural UI without UXML/USS, matching PDAInventoryTab style.
//   - Late-frame playback timer only; catalog rebuilds are event-driven.
//   - Listens to AudioLogEvents for state refresh.
//   - AudioLogData[] catalog is inspector-authored.
//
// ZERO GC:
//   - Preallocated row and text buffers.
//   - Dirty-flag text updates.
//   - No new/LINQ in LateFrameTick.
// ============================================================================

using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Narrative;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Data Log Tab")]
    public sealed class PDADataLogTab : MonoBehaviour, ILateFrameTickable, IAudioLogEventListener, IPDAEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private const string PlaybackTimerTemplate = "{0:00}:{1:00}";
        private const int MaxRegisteredCatalogTabs = 4;
        private const float InvTwoPi = 0.15915494f;
        private const float Inv360 = 0.0027777778f;
        private const int HologramYawLutSize = 8;
        private const int HologramYawLutMask = HologramYawLutSize - 1;

        // COLD ALLOC: RegistryBucket<PDADataLogTab>[4] - active PDA catalog sources for procedural lore lookup - owner: PDADataLogTab
        private static readonly RegistryBucket<PDADataLogTab> _registeredCatalogTabs = new RegistryBucket<PDADataLogTab>(MaxRegisteredCatalogTabs);
        // COLD ALLOC: Quaternion[8] - eight-step hologram yaw spin table, replaces Tick-path rotation construction - owner: PDADataLogTab
        private static readonly Quaternion[] s_hologramYawLut =
        {
            Quaternion.identity,
            new Quaternion(0f, 0.38268343f, 0f, 0.9238795f),
            new Quaternion(0f, 0.70710677f, 0f, 0.70710677f),
            new Quaternion(0f, 0.9238795f, 0f, 0.38268343f),
            new Quaternion(0f, 1f, 0f, 0f),
            new Quaternion(0f, 0.9238795f, 0f, -0.38268343f),
            new Quaternion(0f, 0.70710677f, 0f, -0.70710677f),
            new Quaternion(0f, 0.38268343f, 0f, -0.9238795f)
        };
        private static ILocalizationMadnessPresentationReadModel s_cachedLocalization;
        private static ILoreDatabaseReadModel s_cachedLoreDatabase;
        private static AudioLogSystem s_cachedAudioLogs;
        private static IPlayerRuntimeContext s_cachedPlayerContext;
        private static readonly int AudioLogArchiveTitleKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_ARCHIVE_TITLE);
        private static readonly int AudioLogCategoryPersonalKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_CATEGORY_PERSONAL);
        private static readonly int AudioLogCategoryTechnicalKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_CATEGORY_TECHNICAL);
        private static readonly int AudioLogCategoryEmergencyKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_CATEGORY_EMERGENCY);
        private static readonly int AudioLogCategoryAtlas6KeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_CATEGORY_ATLAS6);
        private static readonly int AudioLogCategoryUnknownKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_CATEGORY_UNKNOWN);
        private static readonly int AudioLogEncryptedKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_ENCRYPTED);
        private static readonly int AudioLogEncryptedSummaryKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_ENCRYPTED_SUMMARY);
        private static readonly int AudioLogUnknownAuthorKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_UNKNOWN_AUTHOR);
        private static readonly int AudioLogUnknownDateKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_UNKNOWN_DATE);
        private static readonly int AudioLogPlayKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_PLAY);
        private static readonly int AudioLogOpenTextKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_OPEN_TEXT);
        private static readonly int AudioLogStopKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_STOP);
        private static readonly int AudioLogCloseTextKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_CLOSE_TEXT);
        private static readonly int AudioLogLockedKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_LOCKED);
        private static readonly int AudioLogNoPayloadKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_NO_PAYLOAD);
        private static readonly int AudioLogEmptyArchiveKeyHash = LocHash.Compute(LocalizationKeys.AUDIOLOG_EMPTY_ARCHIVE);

        // --------------------------------------------------------------------------
        //  INSPECTOR
        // --------------------------------------------------------------------------

        [Header("-- Catalog ----------------------------------")]
        [Tooltip("Все AudioLogData в игре. Назначить в инспекторе.")]
        [SerializeField] private AudioLogData[] allLogs = new AudioLogData[0];

        [Header("-- Font -------------------------------------")]
        [Tooltip("Шрифт с кириллицей. Если null — используется TMP default.")]
        [SerializeField] private TMPro.TMP_FontAsset _labelFont;

        [Header("-- Colors -----------------------------------")]
        [SerializeField] private Color colorBackground  = new Color(0.04f, 0.06f, 0.10f, 0.95f);
        [SerializeField] private Color colorAccent      = new Color(0.20f, 0.80f, 0.60f, 1f);
        [SerializeField] private Color colorText        = new Color(0.85f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color colorDim         = new Color(0.45f, 0.50f, 0.45f, 1f);
        [SerializeField] private Color colorSelected    = new Color(0.10f, 0.25f, 0.18f, 1f);
        [SerializeField] private Color colorPlaying     = new Color(0.05f, 0.35f, 0.20f, 1f);

        [Header("-- Hologram ------------------------------")]
        [SerializeField] private Mesh[] hologramProxyMeshes = System.Array.Empty<Mesh>();
        [SerializeField, Tooltip("Required authored hologram material. Runtime material generation is forbidden.")]
        private Material hologramMaterial;
        [SerializeField] private float hologramHeight = 0.14f;
        [SerializeField] private float hologramForwardOffset = 0.06f;
        [SerializeField] private float hologramScale = 0.045f;
        [SerializeField] private float hologramSpinDegreesPerSecond = 42f;
        [SerializeField] private float hologramBobAmplitude = 0.008f;
        [SerializeField] private float hologramBobFrequency = 1.7f;

        // --------------------------------------------------------------------------
        //  PRIVATE STATE
        // --------------------------------------------------------------------------

 /// <summary> - AudioLogCategory enum ( - enum string conversion - COLD path)</summary>

        // UI roots
        private RectTransform _root;
        private RectTransform _listPanel;
        private RectTransform _detailPanel;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _authorLabel;
        private TextMeshProUGUI _dateLabel;
        private TextMeshProUGUI _summaryLabel;
        private TextMeshProUGUI _summaryDecryptOverlayLabel;
        private TextMeshProUGUI _subtitleLabel;
        private TextMeshProUGUI _playbackTimerLabel;
        private Image _playButtonBg;
        private TextMeshProUGUI _playButtonLabel;
        private TextMeshProUGUI _countLabel;
        private TextMeshProUGUI _emptyStateLabel;
        private TextMeshProUGUI _headerTitleLabel;
        private LocalizedTextMadnessFx _summaryMadnessFx;
        private LocalizedTextMadnessFx _subtitleMadnessFx;

        // List rows: fixed fields avoid a managed row-cache array allocation.
        private LogRow _row0;
        private LogRow _row1;
        private LogRow _row2;
        private LogRow _row3;
        private LogRow _row4;
        private LogRow _row5;
        private LogRow _row6;
        private LogRow _row7;
        private LogRow _row8;
        private LogRow _row9;
        private LogRow _row10;
        private LogRow _row11;
        private LogRow _row12;
        private LogRow _row13;
        private LogRow _row14;
        private LogRow _row15;
        private LogRow _row16;
        private LogRow _row17;
        private LogRow _row18;
        private LogRow _row19;
        private LogRow _row20;
        private LogRow _row21;
        private LogRow _row22;
        private LogRow _row23;
        private LogRow _row24;
        private LogRow _row25;
        private LogRow _row26;
        private LogRow _row27;
        private LogRow _row28;
        private LogRow _row29;
        private LogRow _row30;
        private LogRow _row31;
        private int _rowCount;
        private const int MaxDynamicTextBufferChars = 4096;
        private const int CategoryLabelCapacity = 32;
        private static readonly char[] SharedOversizedTextBuffer = new char[MaxDynamicTextBufferChars]; // COLD ALLOC: char[4096] - no-GC fallback for unusually long PDA data-log strings - owner: PDADataLogTab
        // COLD ALLOC: uint[allLogs.Length] - precomputed lore hashes for direct packed-word archive reads - owner: PDADataLogTab
        private uint[] _catalogLoreHashes = Array.Empty<uint>();
        // COLD ALLOC: int[allLogs.Length] - precomputed lore record indices for direct packed-word archive reads - owner: PDADataLogTab
        private int[] _catalogLoreRecordIndices = Array.Empty<int>();
        private int[] _catalogLoreSurfaceHashes = Array.Empty<int>();
        // COLD ALLOC: char[128] - uppercase title staging buffer for allocation-free TMP updates - owner: PDADataLogTab
        private char[] _detailTitleBuffer = new char[128];
        // COLD ALLOC: char[256] - general PDA archive text staging buffer - owner: PDADataLogTab
        private char[] _dynamicTextBuffer = new char[256];
        // COLD ALLOC: char[2048] - PDA archive long-form summary staging buffer - owner: PDADataLogTab
        private char[] _summaryTextBuffer = new char[2048];

        // State
        private int _selectedIndex = -1;
        private bool _built;
        private bool _registeredLateFrame;
        private bool _pdaEventsRegistered;
        private bool _catalogTabRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _dirty;
        private bool _localizedPresentationDirty;
        private bool _detailVisible = true;

        // Playback timer display
        private float _playbackRemaining;
        private float _pendingVisualDeltaTime;
        private bool _playbackTimerDirty;
        private bool _visualLateFrameDirty;
        private int _prevTimerSeconds = -1;
        private char[] _prevSubtitleBuffer = new char[2048];
        private int _prevSubtitleLength;
        private int _lastStressCorruptionBucket = int.MinValue;
        private float _detailReadTimer;
        private float _hiddenRecordFlashTimer;
        private float _summaryDecryptTimer;
        private int _summaryVisibleCharacterTarget = int.MaxValue;
        private int _summaryHexVisibleCharacterTarget = int.MaxValue;
        private bool _hiddenRecordFlashActive;
        private bool _hiddenRecordFlashConsumed;
        private bool _summaryDecryptActive;
        private bool _catalogLoreBindingsDirty = true;
        private string _activeDetailLogId = string.Empty;
        private string _pendingSummaryDecryptLogId = string.Empty;
        private char[] _resolvedSummaryBaseBuffer = new char[2048];
        private int _resolvedSummaryBaseLength;
        // COLD ALLOC: char[4096] - PDA archive hex-decrypt overlay staging buffer - owner: PDADataLogTab
        private char[] _resolvedSummaryHexBuffer = new char[4096];
        private int _resolvedSummaryHexLength;
        private float _hologramAnimationTime;
        private Material _resolvedHologramMaterial;
        private bool _missingHologramMaterialAnnounced;
        private uint _latestSimulationLogHash;
        private float _latestSimulationLogTimestamp;
        private uint _observedPdaLogVersion;
        private uint _observedPdaLogCount;
        private uint _observedPdaLatestLogHash;

        private const float TICK_DT = 1f / 60f;
        private const float HiddenRecordDelaySeconds = 5f;
        private const float HiddenRecordBlinkSeconds = 0.18f;
        private const float SummaryDecryptDuration = 3f;
        private const int LoreSurfaceSubtitle = 0;
        private const int LoreSurfaceRowTitle = 1;
        private const int LoreSurfaceDetailTitle = 2;
        private const int LoreSurfaceDetailAuthor = 3;
        private const int LoreSurfaceDetailDate = 4;
        private const int LoreSurfaceDetailSummary = 5;
        private const int LoreSurfaceSummaryHidden = 6;
        private const int LoreSurfaceCount = 7;
        private const string PlayAudioLabel = "PLAY AUDIO";
        private const string OpenTextLogLabel = "OPEN LOG";
        private const string StopAudioLabel = "STOP";
        private const string CloseTextLogLabel = "CLOSE LOG";
        private const string LockedLogLabel = "DISCOVERY REQUIRED";
        private const string NoPayloadLabel = "NO PLAYBACK";
        private const string TextOnlySummaryPrefix = "TEXT LOG\n";
        private const string ArchiveOnlySummaryPrefix = "ARCHIVE FRAGMENT\n";
        private const string SummaryHiddenSurfaceId = "detail.summary.hidden";
        private const string HexDigits = "0123456789ABCDEF";

        private readonly char[] _localizedArchiveTitleBuffer = new char[256];
        private readonly char[] _localizedEncryptedLabelBuffer = new char[128];
        private readonly char[] _localizedEncryptedSummaryBuffer = new char[256];
        private readonly char[] _localizedUnknownAuthorLineBuffer = new char[96];
        private readonly char[] _localizedUnknownDateLineBuffer = new char[96];
        private readonly char[] _localizedPlayAudioLabelBuffer = new char[96];
        private readonly char[] _localizedOpenTextLabelBuffer = new char[96];
        private readonly char[] _localizedStopAudioLabelBuffer = new char[96];
        private readonly char[] _localizedCloseTextLabelBuffer = new char[96];
        private readonly char[] _localizedLockedLabelBuffer = new char[128];
        private readonly char[] _localizedNoPayloadLabelBuffer = new char[96];
        private readonly char[] _localizedEmptyStateTextBuffer = new char[256];
        private int _localizedArchiveTitleLength;
        private int _localizedEncryptedLabelLength;
        private int _localizedEncryptedSummaryLength;
        private int _localizedUnknownAuthorLineLength;
        private int _localizedUnknownDateLineLength;
        private int _localizedPlayAudioLabelLength;
        private int _localizedOpenTextLabelLength;
        private int _localizedStopAudioLabelLength;
        private int _localizedCloseTextLabelLength;
        private int _localizedLockedLabelLength;
        private int _localizedNoPayloadLabelLength;
        private int _localizedEmptyStateTextLength;
        private readonly char[] _categoryPersonalBuffer = new char[CategoryLabelCapacity];
        private readonly char[] _categoryTechnicalBuffer = new char[CategoryLabelCapacity];
        private readonly char[] _categoryEmergencyBuffer = new char[CategoryLabelCapacity];
        private readonly char[] _categoryAtlas6Buffer = new char[CategoryLabelCapacity];
        private readonly char[] _categoryUnknownBuffer = new char[CategoryLabelCapacity];
        private int _categoryPersonalLength;
        private int _categoryTechnicalLength;
        private int _categoryEmergencyLength;
        private int _categoryAtlas6Length;
        private int _categoryUnknownLength;

        private int CatalogCount => allLogs != null ? allLogs.Length : 0;

        /// <summary>
        /// Copies the authored audio-log catalog into a caller-owned buffer.
        /// </summary>
        /// <param name="buffer">Destination buffer owned by the caller.</param>
        /// <returns>Number of copied catalog entries.</returns>
        public int CopyCatalog(AudioLogData[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || allLogs == null || allLogs.Length == 0)
                return 0;

            int copyCount = Mathf.Min(buffer.Length, allLogs.Length);
            for (int i = 0; i < copyCount; i++)
                buffer[i] = allLogs[i];

            return copyCount;
        }

        /// <summary>
        /// Copies the first active PDA catalog into a caller-owned buffer without scene search.
        /// </summary>
        /// <param name="buffer">Destination buffer owned by the caller.</param>
        /// <param name="count">Copied entry count.</param>
        /// <returns>True when a live catalog source produced entries.</returns>
        internal static bool TryCopyRegisteredCatalog(AudioLogData[] buffer, out int count)
        {
            count = 0;
            if (buffer == null || buffer.Length == 0)
                return false;

            PDADataLogTab[] rawArray = _registeredCatalogTabs.RawArray;
            int registeredCount = _registeredCatalogTabs.Count;
            for (int i = 0; i < registeredCount; i++)
            {
                PDADataLogTab tab = rawArray[i];
                if (tab == null || !tab.isActiveAndEnabled)
                    continue;

                count = tab.CopyCatalog(buffer);
                if (count > 0)
                    return true;
            }

            count = 0;
            return false;
        }

        // --------------------------------------------------------------------------
        //  NESTED TYPE
        // --------------------------------------------------------------------------

        private struct LogRow
        {
            public RectTransform Root;
            public Image Background;
            public TextMeshProUGUI IndexLabel;
            public TextMeshProUGUI TitleLabel;
            public TextMeshProUGUI CategoryLabel;
            public LogRowButton Button;
            public int LogIndex; // index into allLogs
        }

        // --------------------------------------------------------------------------
        //  LIFECYCLE
        // --------------------------------------------------------------------------

        private void Awake()
        {
            if (!TryGetComponent(out _root))
                _root = gameObject.AddComponent<RectTransform>();

            EnsureHologramMaterial();
            RebuildLoreBindingCache();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterCatalogTab();
            RebuildLocalizationCache();
            if (!_built) EnsureBuilt();
            ApplyLocalizedStaticText();

            TryRegister();

            AudioLogEvents.Register(this);
            TryRegisterPDAEvents();
            LocalizationEvents.RegisterLanguageListener(this);

            RebuildLoreBindingCache();
            ResetObservedPdaLogState();
            RefreshEventSourcedLogStateFromUIStore();
            _dirty = true;
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterCatalogTab();
            TryUnregister();

            AudioLogEvents.Unregister(this);
            UnregisterPDAEvents();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterCatalogTab();
            TryUnregister();
            AudioLogEvents.Unregister(this);
            UnregisterPDAEvents();
            LocalizationEvents.UnregisterLanguageListener(this);
            PDAEvents.AssertUnregistered(this, nameof(PDADataLogTab));
            _resolvedHologramMaterial = null;
        }

        private void TryRegisterPDAEvents()
        {
            if (_pdaEventsRegistered)
                return;

            _pdaEventsRegistered = PDAEvents.TryRegister(this);
        }

        private void UnregisterPDAEvents()
        {
            if (!_pdaEventsRegistered)
                return;

            PDAEvents.Unregister(this);
            _pdaEventsRegistered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCatalogRegistry()
        {
            _registeredCatalogTabs.Clear();
            ClearCachedRuntimeServices();
        }

        private void TryRegisterCatalogTab()
        {
            if (_catalogTabRegistered)
                return;

            _catalogTabRegistered = _registeredCatalogTabs.TryRegister(this);
        }

        private void TryUnregisterCatalogTab()
        {
            if (!_catalogTabRegistered)
                return;

            _registeredCatalogTabs.Unregister(this);
            _catalogTabRegistered = false;
        }

        // --------------------------------------------------------------------------
        //  ITickable
        // --------------------------------------------------------------------------

        private void AdvanceVisualPlaybackState(float deltaTime)
        {
            if (_dirty)
                _visualLateFrameDirty = true;

            // --------------------------------------------------------------------------
            if (_playbackRemaining > 0f)
            {
                _playbackRemaining -= deltaTime;
                if (_playbackRemaining < 0f) _playbackRemaining = 0f;
                _playbackTimerDirty = true;
                _visualLateFrameDirty = true;
            }

            _pendingVisualDeltaTime += math.max(0f, deltaTime);
            _visualLateFrameDirty = true;
        }

        public void LateFrameTick()
        {
            AdvanceVisualPlaybackState(SystemDispatcher.CurrentFrameDeltaTime);
            RefreshEventSourcedLogStateFromUIStore();

            if (!_visualLateFrameDirty && !_dirty)
                return;

            float deltaTime = _pendingVisualDeltaTime;
            _pendingVisualDeltaTime = 0f;
            _visualLateFrameDirty = false;

            if (_localizedPresentationDirty)
            {
                _localizedPresentationDirty = false;
                ResetDetailNarrativeState(clearPendingDecryption: false);
                RebuildLocalizationCache();
                ApplyLocalizedStaticText();
                RefreshList();
                RefreshDetail();
                RefreshPlayButton();
                _dirty = false;
            }
            else if (_dirty)
            {
                RefreshList();
                RefreshDetail();
                RefreshPlayButton();
                _dirty = false;
            }

            if (_playbackTimerDirty)
            {
                _playbackTimerDirty = false;
                UpdatePlaybackTimer();
            }

            TickDetailNarrativeFx(deltaTime);
            RefreshStressReactiveDetailIfNeeded();
            RenderSelectedLoreHologram(deltaTime);
        }

        // --------------------------------------------------------------------------
        //  PUBLIC API
        // --------------------------------------------------------------------------

 /// <summary> - - allLogs.</summary>
        public void SelectLog(int logIndex)
        {
            if (logIndex < 0 || logIndex >= CatalogCount) return;

            _selectedIndex = logIndex;
            RefreshDetail();
            RefreshRowHighlights();
        }

 /// <summary> - .</summary>
        public void PlaySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= CatalogCount) return;

            AudioLogSystem system = ResolveAudioLogSystem();
            if (system == null) return;

            AudioLogData log = GetLog(_selectedIndex);
            if (log == null) return;

            if (!IsCatalogLogUnlocked(_selectedIndex)) return;
            if (!log.HasPlaybackPayload) return;

            system.PlayLog(log);
        }

        // --------------------------------------------------------------------------
        //  EVENT HANDLERS
        // --------------------------------------------------------------------------

        public void OnAudioLogEvent(in AudioLogEventPayload payload)
        {
            switch (payload.Type)
            {
                case AudioLogEventType.Discovered:
                    HandleLogDiscovered(payload.LogHash);
                    return;

                case AudioLogEventType.PlaybackStarted:
                    HandlePlaybackStarted(payload.DurationSeconds);
                    return;

                case AudioLogEventType.PlaybackStopped:
                    HandlePlaybackStopped();
                    return;

                case AudioLogEventType.PlaybackCompleted:
                    HandlePlaybackCompleted();
                    return;
            }
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    HandlePDAOpened(payload.CurrentTab);
                    return;

                case PDAEventType.LogbookChanged:
                    HandleEventSourcedLogbookChanged(payload.LogEventHashID);
                    return;
            }
        }

        private void HandlePDAOpened(int tab) => _dirty = true;

        private void HandleEventSourcedLogbookChanged(uint eventHash)
        {
            if (eventHash != 0u)
            {
                _latestSimulationLogHash = eventHash;
                _latestSimulationLogTimestamp = 0f;
            }

            if (UIStateStore.TryGetPDALogEvent(0, out uint latestHash, out float timestampSeconds))
            {
                _latestSimulationLogHash = latestHash;
                _latestSimulationLogTimestamp = timestampSeconds;
            }

            _dirty = true;
        }

        private void RefreshEventSourcedLogStateFromUIStore()
        {
            if (!UIStateStore.IsInitialized)
                return;

            UIStateData pdaState = UIStateStore.GetPDAState();
            if (_observedPdaLogVersion == pdaState.Version &&
                _observedPdaLogCount == pdaState.LogEntryCount &&
                _observedPdaLatestLogHash == pdaState.LatestLogEventHash)
            {
                return;
            }

            _observedPdaLogVersion = pdaState.Version;
            _observedPdaLogCount = pdaState.LogEntryCount;
            _observedPdaLatestLogHash = pdaState.LatestLogEventHash;

            if (pdaState.LogEntryCount == 0u || pdaState.LatestLogEventHash == 0u)
            {
                _latestSimulationLogHash = 0u;
                _latestSimulationLogTimestamp = 0f;
            }
            else if (UIStateStore.TryGetPDALogEvent(0, out uint latestHash, out float timestampSeconds))
            {
                _latestSimulationLogHash = latestHash;
                _latestSimulationLogTimestamp = timestampSeconds;
            }
            else
            {
                _latestSimulationLogHash = pdaState.LatestLogEventHash;
                _latestSimulationLogTimestamp = 0f;
            }

            _dirty = true;
            _visualLateFrameDirty = true;
        }

        private void ResetObservedPdaLogState()
        {
            _observedPdaLogVersion = 0u;
            _observedPdaLogCount = 0u;
            _observedPdaLatestLogHash = 0u;
            _latestSimulationLogHash = 0u;
            _latestSimulationLogTimestamp = 0f;
        }

        private void HandleLogDiscovered(uint logHash)
        {
            if (ShouldArmSummaryDecryption())
                _pendingSummaryDecryptLogId = ResolveLogId(logHash);

            _dirty = true;
            AudioLogData selectedLog = GetSelectedLog();
            if (selectedLog != null &&
                logHash != 0u &&
                ResolveCatalogLoreHash(_selectedIndex) == logHash)
            {
                RefreshDetail();
                RefreshPlayButton();
            }
        }

        private void HandlePlaybackStarted(float durationSeconds)
        {
            AudioLogSystem system = ResolveAudioLogSystem();
            AudioLogData data = system != null ? system.CurrentLog : null;
            _playbackRemaining = durationSeconds > 0f ? durationSeconds : (data != null ? data.Duration : 0f);
            if (_subtitleLabel != null && data != null)
            {
                if (data.TryWriteVisibleSubtitleOrFallback(_prevSubtitleBuffer, out _prevSubtitleLength))
                    ApplyLogStressReactiveText(
                        _subtitleLabel,
                        data,
                        "subtitle",
                        _prevSubtitleBuffer.AsSpan(0, _prevSubtitleLength),
                        ref _summaryTextBuffer);
                else
                    ApplyDynamicText(_subtitleLabel, Array.Empty<char>(), 0);
            }

            UpdateMadnessFxState(data, _subtitleMadnessFx);

            RefreshPlayButton();
        }

        private void HandlePlaybackStopped()
        {
            _playbackRemaining = 0f;
            ResetPlaybackTimerDisplay();
            RefreshPlayButton();
            if (_subtitleLabel != null)
            {
                ApplyDynamicText(_subtitleLabel, Array.Empty<char>(), 0);
                _prevSubtitleLength = 0;
            }

            if (_subtitleMadnessFx != null)
                _subtitleMadnessFx.SetEffectActive(false);
        }

        private void HandlePlaybackCompleted()
        {
            _playbackRemaining = 0f;
            ResetPlaybackTimerDisplay();
            RefreshPlayButton();
            if (_subtitleMadnessFx != null)
                _subtitleMadnessFx.SetEffectActive(false);
        }

        private string ResolveLogId(uint logHash)
        {
            if (logHash == 0u || allLogs == null)
                return string.Empty;

            for (int i = 0; i < allLogs.Length; i++)
            {
                AudioLogData candidate = allLogs[i];
                if (candidate == null)
                    continue;

                if (ResolveCatalogLoreHash(i) == logHash)
                    return candidate.SafeLogId;
            }

            return string.Empty;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (isActiveAndEnabled)
                {
                    if (currentService != null)
                        TryRegister();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                s_cachedLocalization = currentService as ILocalizationMadnessPresentationReadModel;
                HandleLanguageChanged(s_cachedLocalization != null
                    ? (GameLanguage)s_cachedLocalization.ActiveLanguageId
                    : GameLanguage.English);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.AudioLogRuntime)
            {
                CacheAudioLogSystem(currentService as AudioLogSystem);
                _dirty = true;
                RefreshPlayButton();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LoreDatabaseRuntime)
            {
                s_cachedLoreDatabase = currentService as ILoreDatabaseReadModel;
                RebuildLoreBindingCache();
                _dirty = true;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                s_cachedPlayerContext = currentService as IPlayerRuntimeContext;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private static void CacheRegistryServicesCold()
        {
            s_cachedLocalization = GlobalRegistry.LocalizationMadnessPresentation;
            s_cachedLoreDatabase = GlobalRegistry.LoreDatabaseReadModel;
            CacheAudioLogSystem(GlobalRegistry.AudioLogs);
            s_cachedPlayerContext = GlobalRegistry.Player;
        }

        private static void ClearCachedRuntimeServices()
        {
            s_cachedLocalization = null;
            s_cachedLoreDatabase = null;
            s_cachedAudioLogs = null;
            s_cachedPlayerContext = null;
        }

        private static void CacheAudioLogSystem(AudioLogSystem audioLogSystem)
        {
            s_cachedAudioLogs = IsAudioLogSystemUsable(audioLogSystem) ? audioLogSystem : null;
        }

        private static AudioLogSystem ResolveAudioLogSystem()
        {
            AudioLogSystem audioLogSystem = s_cachedAudioLogs;
            if (IsAudioLogSystemUsable(audioLogSystem))
                return audioLogSystem;

            s_cachedAudioLogs = null;
            return null;
        }

        private static bool IsAudioLogSystemUsable(AudioLogSystem audioLogSystem)
        {
            return audioLogSystem != null && audioLogSystem.IsAudioLogRuntimeReady;
        }

        // --------------------------------------------------------------------------
        //  BUILD UI
        // --------------------------------------------------------------------------

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _labelFont = LocalizedFontResolver.ResolveReadableFont(_labelFont);

            // Background
            if (!TryGetComponent(out Image bg)) bg = gameObject.AddComponent<Image>();
            bg.color = colorBackground;

            // Header
            BuildHeader();

            // Split: list left, detail right
            BuildListPanel();
            BuildDetailPanel();
        }

        private void BuildHeader()
        {
            RectTransform header = CreateRect("Header", _root);
            Anchor(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -48), new Vector2(0, 0));

            Image hBg = header.gameObject.AddComponent<Image>();
            hBg.color = new Color(0.06f, 0.10f, 0.08f, 1f);

            _countLabel = CreateText("CountLabel", header, 11f, colorDim, TextAlignmentOptions.MidlineRight);
            Anchor(_countLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(-8, 0), new Vector2(-12, 0));

            _headerTitleLabel = CreateText("Title", header, 13f, colorAccent, TextAlignmentOptions.MidlineLeft);
            ApplyStressReactiveText(
                _headerTitleLabel,
                _localizedArchiveTitleBuffer.AsSpan(0, _localizedArchiveTitleLength),
                ref _dynamicTextBuffer);
            _headerTitleLabel.fontStyle = FontStyles.Bold;
            Anchor(_headerTitleLabel.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(12, 0), new Vector2(0, 0));
        }

        private void BuildListPanel()
        {
            _listPanel = CreateRect("ListPanel", _root);
            Anchor(_listPanel, new Vector2(0, 0), new Vector2(0.42f, 1),
                new Vector2(0, 0), new Vector2(0, -48));

            Image lBg = _listPanel.gameObject.AddComponent<Image>();
            lBg.color = new Color(0.03f, 0.05f, 0.04f, 1f);

            // Scroll view would be ideal but for minimal impl - static rows
            // COLD ALLOC: up to 32 rows
            BuildLogRows();

            if (_rowCount == 0)
                BuildEmptyState();
        }

        private void BuildLogRows()
        {
            ClearRows();
            float rowH = 44f;
            float y = 0f;
            int logCount = CatalogCount;

            for (int i = 0; i < logCount && i < 32; i++)
            {
                AudioLogData log = GetLog(i);
                if (log == null) continue;

                RectTransform rowRoot = CreateRect("Row", _listPanel);
                Anchor(rowRoot, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -y - rowH), new Vector2(0, -y));

                Image rowBg = rowRoot.gameObject.AddComponent<Image>();
                rowBg.color = new Color(0.04f, 0.07f, 0.05f, 0f);

                TextMeshProUGUI idxLabel = CreateText("Idx", rowRoot, 9f, colorDim, TextAlignmentOptions.MidlineLeft);
                Anchor(idxLabel.rectTransform, new Vector2(0, 0), new Vector2(0.08f, 1),
                    new Vector2(6, 0), new Vector2(0, 0));
                ApplyTwoDigitText(idxLabel, i + 1, ref _dynamicTextBuffer);

                TextMeshProUGUI titleLabel = CreateText("Title", rowRoot, 10f, colorText, TextAlignmentOptions.MidlineLeft);
                Anchor(titleLabel.rectTransform, new Vector2(0.08f, 0), new Vector2(0.75f, 1),
                    new Vector2(4, 0), new Vector2(0, 0));
                if (log.TryWriteDisplayTitleOrFallback(_dynamicTextBuffer, out int rowTitleLength))
                    ApplyDynamicText(titleLabel, _dynamicTextBuffer, rowTitleLength);
                else
                    ApplyDynamicText(titleLabel, Array.Empty<char>(), 0);

                TextMeshProUGUI catLabel = CreateText("Cat", rowRoot, 8f, colorDim, TextAlignmentOptions.MidlineRight);
                Anchor(catLabel.rectTransform, new Vector2(0.75f, 0), new Vector2(1, 1),
                    new Vector2(0, 0), new Vector2(-6, 0));
                ApplyDynamicText(catLabel, GetCachedCategoryLabel(log.category), ref _dynamicTextBuffer);

                // Button component
                LogRowButton btn = rowRoot.gameObject.AddComponent<LogRowButton>();
                int capturedIndex = i;
                btn.Init(this, capturedIndex, rowBg, colorDim, colorSelected);

                LogRow row = default;
                row.Root = rowRoot;
                row.Background = rowBg;
                row.IndexLabel = idxLabel;
                row.TitleLabel = titleLabel;
                row.CategoryLabel = catLabel;
                row.Button = btn;
                row.LogIndex = i;
                SetRow(_rowCount, row);
                _rowCount++;

                y += rowH;
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rowCount; i++)
                SetRow(i, default);

            _rowCount = 0;
        }

        private LogRow GetRow(int index)
        {
            switch (index)
            {
                case 0: return _row0;
                case 1: return _row1;
                case 2: return _row2;
                case 3: return _row3;
                case 4: return _row4;
                case 5: return _row5;
                case 6: return _row6;
                case 7: return _row7;
                case 8: return _row8;
                case 9: return _row9;
                case 10: return _row10;
                case 11: return _row11;
                case 12: return _row12;
                case 13: return _row13;
                case 14: return _row14;
                case 15: return _row15;
                case 16: return _row16;
                case 17: return _row17;
                case 18: return _row18;
                case 19: return _row19;
                case 20: return _row20;
                case 21: return _row21;
                case 22: return _row22;
                case 23: return _row23;
                case 24: return _row24;
                case 25: return _row25;
                case 26: return _row26;
                case 27: return _row27;
                case 28: return _row28;
                case 29: return _row29;
                case 30: return _row30;
                case 31: return _row31;
                default: return default;
            }
        }

        private void SetRow(int index, LogRow row)
        {
            switch (index)
            {
                case 0: _row0 = row; break;
                case 1: _row1 = row; break;
                case 2: _row2 = row; break;
                case 3: _row3 = row; break;
                case 4: _row4 = row; break;
                case 5: _row5 = row; break;
                case 6: _row6 = row; break;
                case 7: _row7 = row; break;
                case 8: _row8 = row; break;
                case 9: _row9 = row; break;
                case 10: _row10 = row; break;
                case 11: _row11 = row; break;
                case 12: _row12 = row; break;
                case 13: _row13 = row; break;
                case 14: _row14 = row; break;
                case 15: _row15 = row; break;
                case 16: _row16 = row; break;
                case 17: _row17 = row; break;
                case 18: _row18 = row; break;
                case 19: _row19 = row; break;
                case 20: _row20 = row; break;
                case 21: _row21 = row; break;
                case 22: _row22 = row; break;
                case 23: _row23 = row; break;
                case 24: _row24 = row; break;
                case 25: _row25 = row; break;
                case 26: _row26 = row; break;
                case 27: _row27 = row; break;
                case 28: _row28 = row; break;
                case 29: _row29 = row; break;
                case 30: _row30 = row; break;
                case 31: _row31 = row; break;
            }
        }

        private void BuildEmptyState()
        {
            RectTransform emptyState = CreateRect("EmptyState", _listPanel);
            Anchor(emptyState, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(16, 16), new Vector2(-16, -16));

            Image emptyBg = emptyState.gameObject.AddComponent<Image>();
            emptyBg.color = new Color(0.05f, 0.07f, 0.06f, 0.75f);

            _emptyStateLabel = CreateText("EmptyStateLabel", emptyState, 10f, colorDim, TextAlignmentOptions.Center);
            _emptyStateLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            ApplyStressReactiveText(
                _emptyStateLabel,
                _localizedEmptyStateTextBuffer.AsSpan(0, _localizedEmptyStateTextLength),
                ref _summaryTextBuffer);
            Stretch(_emptyStateLabel.rectTransform, 16, 16, 16, 16);
        }

        private void BuildDetailPanel()
        {
            _detailPanel = CreateRect("DetailPanel", _root);
            Anchor(_detailPanel, new Vector2(0.42f, 0), new Vector2(1, 1),
                new Vector2(1, 0), new Vector2(0, -48));

            Image dBg = _detailPanel.gameObject.AddComponent<Image>();
            dBg.color = new Color(0.03f, 0.06f, 0.05f, 1f);

            // Title
            _titleLabel = CreateText("Title", _detailPanel, 14f, colorAccent, TextAlignmentOptions.TopLeft);
            _titleLabel.fontStyle = FontStyles.Bold;
            Anchor(_titleLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -52), new Vector2(-12, -12));

            // Author
            _authorLabel = CreateText("Author", _detailPanel, 10f, colorDim, TextAlignmentOptions.TopLeft);
            Anchor(_authorLabel.rectTransform, new Vector2(0, 1), new Vector2(0.6f, 1),
                new Vector2(12, -90), new Vector2(0, -56));

            // Date
            _dateLabel = CreateText("Date", _detailPanel, 10f, colorDim, TextAlignmentOptions.TopRight);
            Anchor(_dateLabel.rectTransform, new Vector2(0.6f, 1), new Vector2(1, 1),
                new Vector2(0, -90), new Vector2(-12, -56));

            // Summary
            _summaryLabel = CreateText("Summary", _detailPanel, 10f, colorText, TextAlignmentOptions.TopLeft);
            _summaryLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Anchor(_summaryLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -200), new Vector2(-12, -96));
            if (!_summaryLabel.TryGetComponent(out _summaryMadnessFx))
                _summaryMadnessFx = _summaryLabel.gameObject.AddComponent<LocalizedTextMadnessFx>();

            _summaryMadnessFx.Bind(_summaryLabel);

            _summaryDecryptOverlayLabel = CreateText("SummaryDecryptOverlay", _detailPanel, 10f, colorAccent, TextAlignmentOptions.TopLeft);
            _summaryDecryptOverlayLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _summaryDecryptOverlayLabel.color = new Color(colorAccent.r, colorAccent.g, colorAccent.b, 0.86f);
            Anchor(_summaryDecryptOverlayLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -200), new Vector2(-12, -96));
            SetElementVisible(_summaryDecryptOverlayLabel, false);

            // Subtitle (playback)
            _subtitleLabel = CreateText("Subtitle", _detailPanel, 9f, colorAccent, TextAlignmentOptions.TopLeft);
            _subtitleLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Anchor(_subtitleLabel.rectTransform, new Vector2(0, 0.3f), new Vector2(1, 0.7f),
                new Vector2(12, 0), new Vector2(-12, 0));
            if (!_subtitleLabel.TryGetComponent(out _subtitleMadnessFx))
                _subtitleMadnessFx = _subtitleLabel.gameObject.AddComponent<LocalizedTextMadnessFx>();

            _subtitleMadnessFx.Bind(_subtitleLabel);

            // Playback timer
            _playbackTimerLabel = CreateText("Timer", _detailPanel, 9f, colorDim, TextAlignmentOptions.BottomRight);
            Anchor(_playbackTimerLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(0, 8), new Vector2(-12, 32));

            // Play button
            RectTransform playBtn = CreateRect("PlayButton", _detailPanel);
            Anchor(playBtn, new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(12, 8), new Vector2(-6, 44));

            _playButtonBg = playBtn.gameObject.AddComponent<Image>();
            _playButtonBg.color = colorAccent;

            _playButtonLabel = CreateText("PlayLabel", playBtn, 11f, colorBackground, TextAlignmentOptions.Midline);
            _playButtonLabel.fontStyle = FontStyles.Bold;
            ApplyDynamicText(_playButtonLabel, _localizedPlayAudioLabelBuffer, _localizedPlayAudioLabelLength);
            Stretch(_playButtonLabel.rectTransform);

            PlayButtonHandler pbh = playBtn.gameObject.AddComponent<PlayButtonHandler>();
            pbh.Init(this, _playButtonBg, colorAccent, new Color(colorAccent.r * 0.7f, colorAccent.g * 0.7f, colorAccent.b * 0.7f));

            // Initial state
            SetDetailVisible(false);
        }

        // --------------------------------------------------------------------------
        //  REFRESH
        // --------------------------------------------------------------------------

        private void RefreshList()
        {
            ILoreDatabaseReadModel database = s_cachedLoreDatabase;
            int discovered = database != null ? database.UnlockedCount : 0;
            int logCount = CatalogCount;

            if (_countLabel != null)
                ApplyCountLabelText(_countLabel, discovered, logCount, ref _dynamicTextBuffer);

            if (_emptyStateLabel != null)
            {
                bool shouldShowEmptyState = logCount == 0;
                SetElementVisible(_emptyStateLabel, shouldShowEmptyState);
                if (shouldShowEmptyState && TryResolveEventSourcedLogText(out char[] eventBuffer, out int eventLength))
                    ApplyDynamicText(_emptyStateLabel, eventBuffer, eventLength);
                else
                    ApplyStressReactiveText(
                        _emptyStateLabel,
                        _localizedEmptyStateTextBuffer.AsSpan(0, _localizedEmptyStateTextLength),
                        ref _summaryTextBuffer);
            }

            if (logCount == 0)
            {
                SetDetailVisible(false);
                return;
            }

            for (int i = 0; i < _rowCount; i++)
            {
                LogRow row = GetRow(i);
                AudioLogData log = GetLog(row.LogIndex);
                bool isDiscovered = log != null && IsCatalogLogUnlocked(row.LogIndex);

                // Dim undiscovered entries
                Color textColor = isDiscovered ? colorText : colorDim;
                if (row.TitleLabel != null) row.TitleLabel.color = textColor;
                if (row.IndexLabel != null) row.IndexLabel.color = colorDim;
                if (row.CategoryLabel != null)
                    ApplyCategoryLabelText(row.CategoryLabel, log, ref _dynamicTextBuffer);
                if (row.CategoryLabel != null) row.CategoryLabel.color = isDiscovered ? colorDim : new Color(colorDim.r, colorDim.g, colorDim.b, 0.3f);

                // Replace title with encrypted label for undiscovered rows.
                if (row.TitleLabel != null)
                {
                    if (isDiscovered)
                    {
                        if (log != null && log.TryWriteDisplayTitleOrFallback(_dynamicTextBuffer, out int rowTitleLength))
                        {
                            ApplyLogStressReactiveText(
                                row.TitleLabel,
                                log,
                                "row.title",
                                _dynamicTextBuffer.AsSpan(0, rowTitleLength),
                                ref _summaryTextBuffer);
                        }
                        else
                        {
                            ApplyDynamicText(row.TitleLabel, Array.Empty<char>(), 0);
                        }
                    }
                    else
                    {
                        ApplyDynamicText(
                            row.TitleLabel,
                            _localizedEncryptedLabelBuffer.AsSpan(0, _localizedEncryptedLabelLength),
                            ref _summaryTextBuffer);
                    }
                }
            }

            RefreshRowHighlights();
        }

        private void RefreshDetail()
        {
            if (_selectedIndex < 0 || _selectedIndex >= CatalogCount)
            {
                SetDetailVisible(false);
                return;
            }

            AudioLogData log = GetLog(_selectedIndex);
            if (log == null) { SetDetailVisible(false); return; }

            bool isDiscovered = IsCatalogLogUnlocked(_selectedIndex);

            SetDetailVisible(true);

            if (_titleLabel != null)
            {
                if (isDiscovered)
                {
                    int rawTitleLength = log.TryWriteDisplayTitleOrFallback(_dynamicTextBuffer, out int writtenTitleLength)
                        ? writtenTitleLength
                        : 0;
                    int titleLength = ResolveLogStressReactiveTextToBuffer(
                        log,
                        "detail.title",
                        _dynamicTextBuffer.AsSpan(0, rawTitleLength),
                        ref _summaryTextBuffer);
                    SetUppercaseLabelText(
                        _titleLabel,
                        _summaryTextBuffer.AsSpan(0, titleLength),
                        ref _detailTitleBuffer);
                }
                else
                {
                    SetUppercaseLabelText(
                        _titleLabel,
                        _localizedEncryptedLabelBuffer.AsSpan(0, _localizedEncryptedLabelLength),
                        ref _detailTitleBuffer);
                }
            }

            if (_authorLabel != null)
            {
                if (isDiscovered)
                {
                    int authorLength = log.TryWriteAuthorOrFallback(_dynamicTextBuffer, out int writtenAuthorLength)
                        ? writtenAuthorLength
                        : 0;
                    ApplyLogStressReactiveText(
                        _authorLabel,
                        log,
                        "detail.author",
                        _dynamicTextBuffer.AsSpan(0, authorLength),
                        ref _summaryTextBuffer);
                }
                else
                    ApplyStressReactiveText(
                        _authorLabel,
                        _localizedUnknownAuthorLineBuffer.AsSpan(0, _localizedUnknownAuthorLineLength),
                        ref _dynamicTextBuffer);
            }

            if (_dateLabel != null)
            {
                if (isDiscovered)
                {
                    int dateLength = log.TryWriteRecordDateOrFallback(_dynamicTextBuffer, out int writtenDateLength)
                        ? writtenDateLength
                        : 0;
                    ApplyLogStressReactiveText(
                        _dateLabel,
                        log,
                        "detail.date",
                        _dynamicTextBuffer.AsSpan(0, dateLength),
                        ref _summaryTextBuffer);
                }
                else
                    ApplyStressReactiveText(
                        _dateLabel,
                        _localizedUnknownDateLineBuffer.AsSpan(0, _localizedUnknownDateLineLength),
                        ref _dynamicTextBuffer);
            }

            if (_summaryLabel != null)
            {
                if (isDiscovered)
                {
                    int summaryLength = log.TryWriteArchiveSummaryOrFallback(_summaryTextBuffer, out int writtenSummaryLength)
                        ? writtenSummaryLength
                        : 0;
                    ApplySummaryNarrativePresentation(
                        log,
                        true,
                        _summaryTextBuffer.AsSpan(0, summaryLength));
                }
                else
                {
                    ApplySummaryNarrativePresentation(
                        log,
                        false,
                        _localizedEncryptedSummaryBuffer.AsSpan(0, _localizedEncryptedSummaryLength));
                }
            }

            if (_summaryMadnessFx != null)
            {
                if (_summaryDecryptActive)
                    _summaryMadnessFx.SetEffectActive(false);
                else if (_hiddenRecordFlashActive)
                    _summaryMadnessFx.SetEffectActive(true);
                else
                    UpdateMadnessFxState(isDiscovered ? log : null, _summaryMadnessFx);
            }

            RefreshPlayButton();
        }

        private void RefreshRowHighlights()
        {
            AudioLogSystem system = ResolveAudioLogSystem();
            string playingId = system != null && system.IsPlaying && system.CurrentLog != null
                ? system.CurrentLog.logId
                : null;

            for (int i = 0; i < _rowCount; i++)
            {
                LogRow row = GetRow(i);
                AudioLogData log = GetLog(row.LogIndex);
                bool isSelected = row.LogIndex == _selectedIndex;
                bool isPlaying = log != null && log.logId == playingId;

                if (row.Background != null)
                {
                    if (isPlaying)
                        row.Background.color = colorPlaying;
                    else if (isSelected)
                        row.Background.color = colorSelected;
                    else
                        row.Background.color = new Color(0, 0, 0, 0);
                }
            }
        }

        private void RefreshPlayButton()
        {
            AudioLogSystem system = ResolveAudioLogSystem();
            AudioLogData selectedLog = GetSelectedLog();
            bool isDiscovered = selectedLog != null && IsCatalogLogUnlocked(_selectedIndex);
            bool isPlaying = system != null && system.IsPlaying;
            bool canStartPlayback = isDiscovered && selectedLog != null && selectedLog.HasPlaybackPayload;
            bool buttonEnabled = isPlaying || canStartPlayback;

            if (_playButtonBg != null)
            {
                _playButtonBg.color = buttonEnabled ? colorAccent : colorDim;
                _playButtonBg.raycastTarget = buttonEnabled;
            }

            if (_playButtonLabel != null)
                ApplyDynamicText(_playButtonLabel, GetCachedPlayButtonLabel(system, selectedLog, isDiscovered), ref _dynamicTextBuffer);

            RefreshRowHighlights();
        }

        private void UpdatePlaybackTimer()
        {
            if (_playbackTimerLabel == null) return;

            int secs = Mathf.CeilToInt(_playbackRemaining);
            if (secs == _prevTimerSeconds)
                return;

            _prevTimerSeconds = secs;
            int m = secs / 60;
            int s = secs % 60;
            SetPlaybackTimerText(m, s);
        }

        private void ResetPlaybackTimerDisplay()
        {
            _prevTimerSeconds = -1;
            if (_playbackTimerLabel != null)
                SetPlaybackTimerText(0, 0);
        }

        private void SetDetailVisible(bool visible)
        {
            if (_detailVisible == visible)
                return;

            _detailVisible = visible;
            SetElementVisible(_titleLabel, visible);
            SetElementVisible(_authorLabel, visible);
            SetElementVisible(_dateLabel, visible);
            SetElementVisible(_summaryLabel, visible);
            SetElementVisible(_subtitleLabel, visible);
            SetElementVisible(_playbackTimerLabel, visible);
            SetElementVisible(_playButtonBg, visible);
            SetElementVisible(_playButtonLabel, visible);
            SetElementVisible(_summaryDecryptOverlayLabel, visible && _summaryDecryptActive);

            if (!visible)
            {
                ResetDetailNarrativeState(clearPendingDecryption: false);
                if (_summaryMadnessFx != null)
                    _summaryMadnessFx.SetEffectActive(false);

                if (_subtitleMadnessFx != null)
                    _subtitleMadnessFx.SetEffectActive(false);
            }
        }

        private AudioLogData GetLog(int index)
        {
            if (allLogs == null || index < 0 || index >= allLogs.Length)
                return null;

            return allLogs[index];
        }

        private bool TryResolveEventSourcedLogText(out char[] buffer, out int length)
        {
            buffer = null;
            length = 0;
            if (_latestSimulationLogHash == 0u)
                return false;

            return LocRegistry.TryGetRawBuffer(unchecked((int)_latestSimulationLogHash), out buffer, out length);
        }

        private AudioLogData GetSelectedLog()
        {
            return GetLog(_selectedIndex);
        }

        private void RebuildLoreBindingCache()
        {
            int logCount = CatalogCount;
            if (_catalogLoreHashes.Length != logCount)
                _catalogLoreHashes = new uint[logCount]; // COLD ALLOC: uint[allLogs.Length] — lore hash cache aligned to PDA archive catalog — owner: PDADataLogTab

            if (_catalogLoreRecordIndices.Length != logCount)
                _catalogLoreRecordIndices = new int[logCount]; // COLD ALLOC: int[allLogs.Length] — lore record index cache aligned to PDA archive catalog — owner: PDADataLogTab

            int surfaceKeyCount = logCount * LoreSurfaceCount;
            if (_catalogLoreSurfaceHashes.Length != surfaceKeyCount)
                _catalogLoreSurfaceHashes = new int[surfaceKeyCount]; // COLD ALLOC: int[allLogs.Length * lore surfaces] - PDA corruption surface token hashes - owner: PDADataLogTab

            ILoreDatabaseReadModel database = s_cachedLoreDatabase;
            for (int i = 0; i < logCount; i++)
            {
                AudioLogData log = GetLog(i);
                if (log == null || string.IsNullOrWhiteSpace(log.SafeLogId))
                {
                    _catalogLoreHashes[i] = 0u;
                    _catalogLoreRecordIndices[i] = -1;
                    WriteLoreSurfaceHashes(i, string.Empty);
                    continue;
                }

                uint loreHash = LocHash.ComputeAscii(log.SafeLogId);
                _catalogLoreHashes[i] = loreHash;
                _catalogLoreRecordIndices[i] = database != null && database.TryGetRecordIndex(loreHash, out int recordIndex)
                    ? recordIndex
                    : -1;
                WriteLoreSurfaceHashes(i, log.SafeLogId);
            }

            _catalogLoreBindingsDirty = false;
        }

        private void WriteLoreSurfaceHashes(int logIndex, string logId)
        {
            int baseIndex = logIndex * LoreSurfaceCount;
            if ((uint)(baseIndex + LoreSurfaceSummaryHidden) >= (uint)_catalogLoreSurfaceHashes.Length)
                return;

            if (string.IsNullOrEmpty(logId))
            {
                for (int i = 0; i < LoreSurfaceCount; i++)
                    _catalogLoreSurfaceHashes[baseIndex + i] = 0;
                return;
            }

            ReadOnlySpan<char> id = logId.AsSpan();
            _catalogLoreSurfaceHashes[baseIndex + LoreSurfaceSubtitle] = BuildLoreSurfaceHash(id, "subtitle".AsSpan());
            _catalogLoreSurfaceHashes[baseIndex + LoreSurfaceRowTitle] = BuildLoreSurfaceHash(id, "row.title".AsSpan());
            _catalogLoreSurfaceHashes[baseIndex + LoreSurfaceDetailTitle] = BuildLoreSurfaceHash(id, "detail.title".AsSpan());
            _catalogLoreSurfaceHashes[baseIndex + LoreSurfaceDetailAuthor] = BuildLoreSurfaceHash(id, "detail.author".AsSpan());
            _catalogLoreSurfaceHashes[baseIndex + LoreSurfaceDetailDate] = BuildLoreSurfaceHash(id, "detail.date".AsSpan());
            _catalogLoreSurfaceHashes[baseIndex + LoreSurfaceDetailSummary] = BuildLoreSurfaceHash(id, "detail.summary".AsSpan());
            _catalogLoreSurfaceHashes[baseIndex + LoreSurfaceSummaryHidden] = BuildLoreSurfaceHash(id, SummaryHiddenSurfaceId.AsSpan());
        }

        private bool IsLoreBindingCacheReady()
        {
            int logCount = CatalogCount;
            return !_catalogLoreBindingsDirty &&
                _catalogLoreHashes.Length == logCount &&
                _catalogLoreRecordIndices.Length == logCount &&
                _catalogLoreSurfaceHashes.Length == logCount * LoreSurfaceCount;
        }

        private uint ResolveCatalogLoreHash(int logIndex)
        {
            if (!IsLoreBindingCacheReady())
                return 0u;

            return (uint)logIndex < (uint)_catalogLoreHashes.Length
                ? _catalogLoreHashes[logIndex]
                : 0u;
        }

        private bool IsCatalogLogUnlocked(int logIndex)
        {
            if (logIndex < 0 || logIndex >= CatalogCount)
                return false;

            if (!IsLoreBindingCacheReady())
                return false;

            ILoreDatabaseReadModel database = s_cachedLoreDatabase;
            if (database == null || !database.TryGetPackedUnlockWords(out Unity.Collections.NativeArray<uint>.ReadOnly words))
                return false;

            int recordIndex = _catalogLoreRecordIndices[logIndex];
            if (recordIndex < 0)
                return false;

            int wordIndex = recordIndex >> 5;
            if ((uint)wordIndex >= (uint)words.Length)
                return false;

            uint bitMask = 1u << (recordIndex & 31);
            return (words[wordIndex] & bitMask) != 0u;
        }

        internal bool TryGetLogById(string logId, out AudioLogData log)
        {
            if (allLogs != null && !string.IsNullOrEmpty(logId))
            {
                for (int i = 0; i < allLogs.Length; i++)
                {
                    AudioLogData candidate = allLogs[i];
                    if (candidate != null &&
                        string.Equals(candidate.logId, logId, System.StringComparison.Ordinal))
                    {
                        log = candidate;
                        return true;
                    }
                }
            }

            log = null;
            return false;
        }

        // --------------------------------------------------------------------------
        //  UI HELPERS
        // --------------------------------------------------------------------------

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            _lastStressCorruptionBucket = int.MinValue;
            _localizedPresentationDirty = true;
            _dirty = true;
            _visualLateFrameDirty = true;
        }

        private void RebuildLocalizationCache()
        {
            _localizedArchiveTitleLength = CopyLocalizedSpan(AudioLogArchiveTitleKeyHash, "DATA ARCHIVE - HECTON-8 COLONY".AsSpan(), _localizedArchiveTitleBuffer);
            _categoryPersonalLength = CopyLocalizedSpan(AudioLogCategoryPersonalKeyHash, "PERSONAL".AsSpan(), _categoryPersonalBuffer);
            _categoryTechnicalLength = CopyLocalizedSpan(AudioLogCategoryTechnicalKeyHash, "TECHNICAL".AsSpan(), _categoryTechnicalBuffer);
            _categoryEmergencyLength = CopyLocalizedSpan(AudioLogCategoryEmergencyKeyHash, "EMERGENCY".AsSpan(), _categoryEmergencyBuffer);
            _categoryAtlas6Length = CopyLocalizedSpan(AudioLogCategoryAtlas6KeyHash, "ATLAS6".AsSpan(), _categoryAtlas6Buffer);
            _categoryUnknownLength = CopyLocalizedSpan(AudioLogCategoryUnknownKeyHash, "UNKNOWN".AsSpan(), _categoryUnknownBuffer);
            _localizedEncryptedLabelLength = CopyLocalizedSpan(AudioLogEncryptedKeyHash, "[ENCRYPTED]".AsSpan(), _localizedEncryptedLabelBuffer);
            _localizedEncryptedSummaryLength = CopyLocalizedSpan(AudioLogEncryptedSummaryKeyHash, "Entry encrypted. Discovery required before archive access.".AsSpan(), _localizedEncryptedSummaryBuffer);
            _localizedUnknownAuthorLineLength = CopyLocalizedSpan(AudioLogUnknownAuthorKeyHash, "UNKNOWN".AsSpan(), _localizedUnknownAuthorLineBuffer);
            _localizedUnknownDateLineLength = CopyLocalizedSpan(AudioLogUnknownDateKeyHash, "DATE UNKNOWN".AsSpan(), _localizedUnknownDateLineBuffer);
            _localizedPlayAudioLabelLength = CopyLocalizedSpan(AudioLogPlayKeyHash, PlayAudioLabel.AsSpan(), _localizedPlayAudioLabelBuffer);
            _localizedOpenTextLabelLength = CopyLocalizedSpan(AudioLogOpenTextKeyHash, OpenTextLogLabel.AsSpan(), _localizedOpenTextLabelBuffer);
            _localizedStopAudioLabelLength = CopyLocalizedSpan(AudioLogStopKeyHash, StopAudioLabel.AsSpan(), _localizedStopAudioLabelBuffer);
            _localizedCloseTextLabelLength = CopyLocalizedSpan(AudioLogCloseTextKeyHash, CloseTextLogLabel.AsSpan(), _localizedCloseTextLabelBuffer);
            _localizedLockedLabelLength = CopyLocalizedSpan(AudioLogLockedKeyHash, LockedLogLabel.AsSpan(), _localizedLockedLabelBuffer);
            _localizedNoPayloadLabelLength = CopyLocalizedSpan(AudioLogNoPayloadKeyHash, NoPayloadLabel.AsSpan(), _localizedNoPayloadLabelBuffer);
            _localizedEmptyStateTextLength = CopyLocalizedSpan(AudioLogEmptyArchiveKeyHash, "ARCHIVE EMPTY".AsSpan(), _localizedEmptyStateTextBuffer);
        }

        private void ApplyLocalizedStaticText()
        {
            if (_headerTitleLabel != null)
                ApplyStressReactiveText(
                    _headerTitleLabel,
                    _localizedArchiveTitleBuffer.AsSpan(0, _localizedArchiveTitleLength),
                    ref _dynamicTextBuffer);

            if (_emptyStateLabel != null)
                ApplyStressReactiveText(
                    _emptyStateLabel,
                    _localizedEmptyStateTextBuffer.AsSpan(0, _localizedEmptyStateTextLength),
                    ref _summaryTextBuffer);
        }

        private int ResolveCachedLoreSurfaceHash(AudioLogData log, string surfaceId)
        {
            if (IsLoreBindingCacheReady())
            {
                int logIndex = ResolveCatalogIndex(log);
                int surfaceIndex = ResolveLoreSurfaceIndex(surfaceId);
                if (logIndex >= 0 && surfaceIndex >= 0)
                {
                    int keyIndex = logIndex * LoreSurfaceCount + surfaceIndex;
                    if ((uint)keyIndex < (uint)_catalogLoreSurfaceHashes.Length)
                        return _catalogLoreSurfaceHashes[keyIndex];
                }
            }

            return BuildLoreSurfaceHash(ReadOnlySpan<char>.Empty, string.IsNullOrEmpty(surfaceId) ? ReadOnlySpan<char>.Empty : surfaceId.AsSpan());
        }

        private static int BuildLoreSurfaceHash(ReadOnlySpan<char> logId, ReadOnlySpan<char> surfaceId)
        {
            return logId.Length > 0
                ? LocalizationMadnessHash.ComputeSourceTokenHash(logId, ".".AsSpan(), surfaceId)
                : LocalizationMadnessHash.ComputeSourceTokenHash(surfaceId);
        }

        private int ResolveCatalogIndex(AudioLogData log)
        {
            if (log == null || allLogs == null)
                return -1;

            for (int i = 0; i < allLogs.Length; i++)
            {
                if (ReferenceEquals(allLogs[i], log))
                    return i;
            }

            return -1;
        }

        private static int ResolveLoreSurfaceIndex(string surfaceId)
        {
            switch (surfaceId)
            {
                case "subtitle":
                    return LoreSurfaceSubtitle;
                case "row.title":
                    return LoreSurfaceRowTitle;
                case "detail.title":
                    return LoreSurfaceDetailTitle;
                case "detail.author":
                    return LoreSurfaceDetailAuthor;
                case "detail.date":
                    return LoreSurfaceDetailDate;
                case "detail.summary":
                    return LoreSurfaceDetailSummary;
                case SummaryHiddenSurfaceId:
                    return LoreSurfaceSummaryHidden;
                default:
                    return -1;
            }
        }

        private ReadOnlySpan<char> GetCachedPlayButtonLabel(AudioLogSystem system, AudioLogData selectedLog, bool isDiscovered)
        {
            if (system != null && system.IsPlaying)
            {
                AudioLogData playingLog = system.CurrentLog;
                return playingLog != null && playingLog.IsTextOnlyPlayback
                    ? _localizedCloseTextLabelBuffer.AsSpan(0, _localizedCloseTextLabelLength)
                    : _localizedStopAudioLabelBuffer.AsSpan(0, _localizedStopAudioLabelLength);
            }

            if (!isDiscovered)
                return _localizedLockedLabelBuffer.AsSpan(0, _localizedLockedLabelLength);

            if (selectedLog == null || !selectedLog.HasPlaybackPayload)
                return _localizedNoPayloadLabelBuffer.AsSpan(0, _localizedNoPayloadLabelLength);

            return selectedLog.HasAudioClip
                ? _localizedPlayAudioLabelBuffer.AsSpan(0, _localizedPlayAudioLabelLength)
                : _localizedOpenTextLabelBuffer.AsSpan(0, _localizedOpenTextLabelLength);
        }

        private ReadOnlySpan<char> GetCachedCategoryLabel(AudioLogCategory category)
        {
            switch (category)
            {
                case AudioLogCategory.Personal:
                    return _categoryPersonalBuffer.AsSpan(0, _categoryPersonalLength);
                case AudioLogCategory.Technical:
                    return _categoryTechnicalBuffer.AsSpan(0, _categoryTechnicalLength);
                case AudioLogCategory.Emergency:
                    return _categoryEmergencyBuffer.AsSpan(0, _categoryEmergencyLength);
                case AudioLogCategory.Atlas6:
                    return _categoryAtlas6Buffer.AsSpan(0, _categoryAtlas6Length);
                default:
                    return _categoryUnknownBuffer.AsSpan(0, _categoryUnknownLength);
            }
        }

        private void ApplyCategoryLabelText(TMP_Text label, AudioLogData log, ref char[] buffer)
        {
            ReadOnlySpan<char> categoryText = log != null
                ? GetCachedCategoryLabel(log.category)
                : GetCachedCategoryLabel(AudioLogCategory.Unknown);
            int length = ResolveStressReactiveSpanToBuffer(categoryText, ref buffer);
            ApplyDynamicText(label, buffer, length);
        }

        private int ResolveStressReactiveSpanToBuffer(ReadOnlySpan<char> text, ref char[] buffer)
        {
            EnsureCharCapacity(ref buffer, math.max(1, text.Length));
            ILocalizationMadnessPresentationReadModel manager = s_cachedLocalization;
            if (manager != null && manager.TryApplyHullStressCorruptionIfNeeded(text, buffer, out int length))
                return length;

            return CopySpanToBuffer(text, buffer);
        }

        private void RefreshStressReactiveDetailIfNeeded()
        {
            if (!_detailVisible)
            {
                _lastStressCorruptionBucket = int.MinValue;
                return;
            }

            ILocalizationMadnessPresentationReadModel manager = s_cachedLocalization;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            RefreshList();
            RefreshDetail();

            if (_subtitleLabel != null)
            {
                AudioLogSystem system = ResolveAudioLogSystem();
                AudioLogData subtitleLog = system != null && system.IsPlaying ? system.CurrentLog : GetSelectedLog();
                if (_prevSubtitleLength > 0)
                {
                    ApplyLogStressReactiveText(
                        _subtitleLabel,
                        subtitleLog,
                        "subtitle",
                        _prevSubtitleBuffer.AsSpan(0, _prevSubtitleLength),
                        ref _summaryTextBuffer);
                }
                else
                {
                    ApplyDynamicText(_subtitleLabel, Array.Empty<char>(), 0);
                }

                UpdateMadnessFxState(subtitleLog, _subtitleMadnessFx);
            }
        }

        private static void UpdateMadnessFxState(AudioLogData log, LocalizedTextMadnessFx effect)
        {
            if (effect == null)
                return;

            ILocalizationMadnessPresentationReadModel manager = s_cachedLocalization;
            effect.SetEffectActive(
                manager != null &&
                log != null &&
                !string.IsNullOrWhiteSpace(log.logId) &&
                manager.IsMadnessWhisperVisualActive());
        }

        private void ApplyStressReactiveText(TMP_Text label, ReadOnlySpan<char> text, ref char[] buffer)
        {
            if (label == null)
                return;

            int length = ResolveStressReactiveSpanToBuffer(text, ref buffer);
            ApplyDynamicText(label, buffer, length);
        }

        private void ApplyLogStressReactiveText(
            TMP_Text label,
            AudioLogData log,
            string surfaceId,
            ReadOnlySpan<char> text,
            ref char[] buffer)
        {
            if (label == null)
                return;

            int length = ResolveLogStressReactiveTextToBuffer(log, surfaceId, text, ref buffer);
            ApplyDynamicText(label, buffer, length);
        }

        private int ResolveLogStressReactiveTextToBuffer(
            AudioLogData log,
            string surfaceId,
            ReadOnlySpan<char> text,
            ref char[] buffer)
        {
            EnsureCharCapacity(ref buffer, Mathf.Max(256, text.Length));
            if (!TryResolveLogStressReactiveText(log, surfaceId, text, buffer, out int length))
                length = CopySpanToBuffer(text, buffer);

            return length;
        }

        private bool TryResolveLogStressReactiveText(
            AudioLogData log,
            string surfaceId,
            ReadOnlySpan<char> text,
            char[] destination,
            out int length)
        {
            length = 0;
            if (destination == null || destination.Length == 0)
                return false;

            if (text.Length == 0)
                return true;

            ILocalizationMadnessPresentationReadModel manager = s_cachedLocalization;
            if (manager == null)
            {
                length = CopySpanToBuffer(text, destination);
                return true;
            }

            if (log == null || string.IsNullOrWhiteSpace(log.logId))
                return manager.TryApplyHullStressCorruptionIfNeeded(text, destination, out length);

            return manager.TryApplyPdaLoreCorruptionIfNeeded(
                ResolveCachedLoreSurfaceHash(log, surfaceId),
                text,
                destination,
                out length);
        }

        private static int CopyLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback, char[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            ILocalizationMadnessPresentationReadModel manager = s_cachedLocalization;
            ReadOnlySpan<char> source = manager != null && keyHash != 0
                ? manager.GetRawSpanOrFallback(keyHash, fallback)
                : fallback;

            return CopySpanToBuffer(source, destination);
        }

        private void TickDetailNarrativeFx(float deltaTime)
        {
            if (!_detailVisible || _summaryLabel == null)
                return;

            AudioLogData log = GetSelectedLog();
            bool isDiscovered = log != null && IsCatalogLogUnlocked(_selectedIndex);
            if (!isDiscovered)
                return;

            if (_summaryDecryptActive)
            {
                _summaryDecryptTimer += deltaTime;
                UpdateSummaryDecryptPresentation();
                if (_summaryDecryptTimer >= SummaryDecryptDuration)
                    CompleteSummaryDecryption(log);
                return;
            }

            if (_hiddenRecordFlashActive)
            {
                _hiddenRecordFlashTimer -= deltaTime;
                if (_hiddenRecordFlashTimer <= 0f)
                    CompleteHiddenRecordFlash(log);
                return;
            }

            _detailReadTimer += deltaTime;
            if (!_hiddenRecordFlashConsumed && _detailReadTimer >= HiddenRecordDelaySeconds)
                TriggerHiddenRecordFlash(log);
        }

        private void ApplySummaryNarrativePresentation(AudioLogData log, bool isDiscovered, ReadOnlySpan<char> summaryText)
        {
            if (_summaryLabel == null)
                return;

            if (!isDiscovered || log == null || string.IsNullOrWhiteSpace(log.logId))
            {
                ResetDetailNarrativeState(clearPendingDecryption: false);
                _summaryLabel.maxVisibleCharacters = int.MaxValue;
                ApplyDynamicText(_summaryLabel, summaryText, ref _summaryTextBuffer);
                if (_summaryDecryptOverlayLabel != null)
                    SetElementVisible(_summaryDecryptOverlayLabel, false);
                return;
            }

            bool logChanged = !string.Equals(_activeDetailLogId, log.logId, System.StringComparison.Ordinal);
            if (logChanged)
            {
                ResetDetailNarrativeState(clearPendingDecryption: false);
                _activeDetailLogId = log.logId;
            }

            _resolvedSummaryBaseLength = CopySpanToBuffer(summaryText, ref _resolvedSummaryBaseBuffer);

            if (_summaryDecryptActive)
            {
                ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseBuffer, _resolvedSummaryBaseLength);
                _summaryVisibleCharacterTarget = _resolvedSummaryBaseLength;
                UpdateSummaryDecryptPresentation();
                return;
            }

            if (_hiddenRecordFlashActive)
                return;

            _summaryLabel.maxVisibleCharacters = int.MaxValue;
            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseBuffer, _resolvedSummaryBaseLength);

            if (_summaryDecryptOverlayLabel != null)
                SetElementVisible(_summaryDecryptOverlayLabel, false);

            if (TryConsumePendingSummaryDecryption(log.logId))
                BeginSummaryDecryption(log);
        }

        private void BeginSummaryDecryption(AudioLogData log)
        {
            if (_summaryLabel == null || _summaryDecryptOverlayLabel == null || log == null)
                return;

            _summaryDecryptActive = true;
            _summaryDecryptTimer = 0f;
            _hiddenRecordFlashActive = false;
            _hiddenRecordFlashConsumed = true;
            BuildHexCipherText(
                _resolvedSummaryBaseBuffer.AsSpan(0, _resolvedSummaryBaseLength),
                ref _resolvedSummaryHexBuffer,
                out _resolvedSummaryHexLength);

            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseBuffer, _resolvedSummaryBaseLength);
            _summaryVisibleCharacterTarget = _resolvedSummaryBaseLength;

            ApplyDynamicText(_summaryDecryptOverlayLabel, _resolvedSummaryHexBuffer, _resolvedSummaryHexLength);
            _summaryDecryptOverlayLabel.maxVisibleCharacters = int.MaxValue;
            SetElementVisible(_summaryDecryptOverlayLabel, true);
            _summaryHexVisibleCharacterTarget = _resolvedSummaryHexLength;

            UpdateSummaryDecryptPresentation();
            if (_summaryMadnessFx != null)
                _summaryMadnessFx.SetEffectActive(false);
        }

        private void UpdateSummaryDecryptPresentation()
        {
            if (!_summaryDecryptActive || _summaryLabel == null || _summaryDecryptOverlayLabel == null)
                return;

            float t = math.saturate(_summaryDecryptTimer / SummaryDecryptDuration);
            int summaryVisible = math.clamp((int)math.ceil(_summaryVisibleCharacterTarget * t), 0, _summaryVisibleCharacterTarget);
            int hexVisible = math.clamp(
                (int)math.ceil(_summaryHexVisibleCharacterTarget * (1f - t)),
                0,
                _summaryHexVisibleCharacterTarget);

            _summaryLabel.maxVisibleCharacters = summaryVisible;
            _summaryDecryptOverlayLabel.maxVisibleCharacters = hexVisible;
            SetElementVisible(_summaryDecryptOverlayLabel, true);
        }

        private void CompleteSummaryDecryption(AudioLogData log)
        {
            _summaryDecryptActive = false;
            _summaryDecryptTimer = 0f;
            _summaryVisibleCharacterTarget = int.MaxValue;
            _summaryHexVisibleCharacterTarget = int.MaxValue;
            _summaryLabel.maxVisibleCharacters = int.MaxValue;

            if (_summaryDecryptOverlayLabel != null)
            {
                _summaryDecryptOverlayLabel.maxVisibleCharacters = int.MaxValue;
                ApplyDynamicText(_summaryDecryptOverlayLabel, string.Empty, ref _summaryTextBuffer);
                SetElementVisible(_summaryDecryptOverlayLabel, false);
            }

            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseBuffer, _resolvedSummaryBaseLength);

            _detailReadTimer = 0f;
            if (_summaryMadnessFx != null)
                UpdateMadnessFxState(log, _summaryMadnessFx);
        }

        private void TriggerHiddenRecordFlash(AudioLogData log)
        {
            ILocalizationMadnessPresentationReadModel manager = s_cachedLocalization;
            if (manager == null || _summaryLabel == null || log == null)
                return;

            int cycle = Mathf.Max(1, Mathf.FloorToInt((float)SystemDispatcher.CurrentUnscaledTimeSeconds));
            EnsureCharCapacity(ref _summaryTextBuffer, 256);
            if (!manager.TryResolveMadnessWhisperPreview(
                    ResolveCachedLoreSurfaceHash(log, SummaryHiddenSurfaceId),
                    cycle,
                    _summaryTextBuffer,
                    out int hiddenLength) ||
                hiddenLength <= 0)
            {
                _hiddenRecordFlashConsumed = true;
                return;
            }

            _hiddenRecordFlashActive = true;
            _hiddenRecordFlashConsumed = true;
            _hiddenRecordFlashTimer = HiddenRecordBlinkSeconds;
            _summaryLabel.maxVisibleCharacters = int.MaxValue;
            ApplyDynamicText(_summaryLabel, _summaryTextBuffer, hiddenLength);
            if (_summaryMadnessFx != null)
                _summaryMadnessFx.SetEffectActive(true);
        }

        private void CompleteHiddenRecordFlash(AudioLogData log)
        {
            _hiddenRecordFlashActive = false;
            _hiddenRecordFlashTimer = 0f;
            _summaryLabel.maxVisibleCharacters = int.MaxValue;
            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseBuffer, _resolvedSummaryBaseLength);
            if (_summaryMadnessFx != null)
                UpdateMadnessFxState(log, _summaryMadnessFx);
        }

        private void ResetDetailNarrativeState(bool clearPendingDecryption)
        {
            _detailReadTimer = 0f;
            _hiddenRecordFlashTimer = 0f;
            _summaryDecryptTimer = 0f;
            _hiddenRecordFlashActive = false;
            _hiddenRecordFlashConsumed = false;
            _summaryDecryptActive = false;
            _summaryVisibleCharacterTarget = int.MaxValue;
            _summaryHexVisibleCharacterTarget = int.MaxValue;
            _activeDetailLogId = string.Empty;
            _resolvedSummaryBaseLength = 0;
            _resolvedSummaryHexLength = 0;

            if (clearPendingDecryption)
                _pendingSummaryDecryptLogId = string.Empty;

            if (_summaryLabel != null)
                _summaryLabel.maxVisibleCharacters = int.MaxValue;

            if (_summaryDecryptOverlayLabel != null)
            {
                _summaryDecryptOverlayLabel.maxVisibleCharacters = int.MaxValue;
                ApplyDynamicText(_summaryDecryptOverlayLabel, string.Empty, ref _summaryTextBuffer);
                SetElementVisible(_summaryDecryptOverlayLabel, false);
            }
        }

        private bool TryConsumePendingSummaryDecryption(string logId)
        {
            if (string.IsNullOrEmpty(logId) ||
                !string.Equals(_pendingSummaryDecryptLogId, logId, System.StringComparison.Ordinal))
            {
                return false;
            }

            _pendingSummaryDecryptLogId = string.Empty;
            return true;
        }

        private static bool ShouldArmSummaryDecryption()
        {
            HectonMapMagicVegetationBridge bridge = null;
            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref bridge) ||
                !bridge.TryGetActiveArtificialInteriorState(out ArtificialInteriorState state))
                return false;

            return state.Type == StructureType.MegaWreck;
        }

        private static void BuildHexCipherText(ReadOnlySpan<char> sourceText, ref char[] buffer, out int length)
        {
            if (sourceText.Length == 0)
            {
                EnsureCharCapacity(ref buffer, 1);
                length = 0;
                return;
            }

            EnsureCharCapacity(ref buffer, sourceText.Length * 3);
            int maxCursor = buffer != null ? buffer.Length : 0;
            int cursor = 0;
            for (int i = 0; i < sourceText.Length && cursor < maxCursor; i++)
            {
                char current = sourceText[i];
                if (current == '\n' || current == '\r')
                {
                    if (cursor >= maxCursor)
                        break;

                    buffer[cursor++] = current;
                    continue;
                }

                if (char.IsWhiteSpace(current))
                {
                    if (cursor >= maxCursor)
                        break;

                    buffer[cursor++] = ' ';
                    continue;
                }

                if (cursor + 2 > maxCursor)
                    break;

                int value = current & 0xFF;
                buffer[cursor++] = HexDigits[(value >> 4) & 0x0F];
                buffer[cursor++] = HexDigits[value & 0x0F];

                if (i + 1 < sourceText.Length && !char.IsWhiteSpace(sourceText[i + 1]) && cursor < maxCursor)
                    buffer[cursor++] = ' ';
            }
            length = cursor;
        }

        private RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out RectTransform rt);
            rt.SetParent(parent, false);
            return rt;
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, float size,
            Color color, TextAlignmentOptions alignment)
        {
            RectTransform rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_labelFont != null) tmp.font = _labelFont;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.overflowMode = TextOverflowModes.Truncate;
            LocalizedTMPAutoSizer.Configure(tmp, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            return tmp;
        }

        private static void Stretch(RectTransform r, float l = 0, float r2 = 0, float b = 0, float t = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b);
            r.offsetMax = new Vector2(-r2, -t);
        }

        private static void Anchor(RectTransform r, Vector2 amin, Vector2 amax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = amin;
            r.anchorMax = amax;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
        }

        private void SetPlaybackTimerText(int minutes, int seconds)
        {
            if (_playbackTimerLabel == null)
                return;

            LocNumericBuffer.Write(PlaybackTimerTemplate.AsSpan(), LocNumericArg.Int(minutes), LocNumericArg.Int(seconds), out char[] buffer, out int length);
            int safeLength = Mathf.Clamp(length, 0, buffer != null ? buffer.Length : 0);
            _playbackTimerLabel.SetCharArray(buffer, 0, safeLength);
        }

        private static void SetUppercaseLabelText(TMP_Text label, string source, ref char[] buffer)
        {
            if (label == null)
                return;

            WriteUppercaseToBuffer(string.IsNullOrEmpty(source) ? ReadOnlySpan<char>.Empty : source.AsSpan(), ref buffer, out int length);
            label.SetCharArray(buffer, 0, length);
        }

        private static void SetUppercaseLabelText(TMP_Text label, ReadOnlySpan<char> source, ref char[] buffer)
        {
            if (label == null)
                return;

            WriteUppercaseToBuffer(source, ref buffer, out int length);
            label.SetCharArray(buffer, 0, length);
        }

        private static void WriteUppercaseToBuffer(string source, ref char[] buffer, out int length)
        {
            WriteUppercaseToBuffer(string.IsNullOrEmpty(source) ? ReadOnlySpan<char>.Empty : source.AsSpan(), ref buffer, out length);
        }

        private static void WriteUppercaseToBuffer(ReadOnlySpan<char> source, ref char[] buffer, out int length)
        {
            if (source.Length == 0)
            {
                EnsureCharCapacity(ref buffer, 1);
                length = 0;
                return;
            }

            EnsureCharCapacity(ref buffer, source.Length);
            for (int i = 0; i < source.Length; i++)
                buffer[i] = ConvertUppercaseInvariantChar(source[i]);

            length = source.Length;
        }

        private static char ConvertUppercaseInvariantChar(char value)
        {
            if ((uint)(value - 'a') <= 25u)
                return (char)(value - 32);

            if ((uint)(value - '\u0430') <= 31u)
                return (char)(value - 32);

            return value == '\u0451' ? '\u0401' : value;
        }

        private static void EnsureCharCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer != null && buffer.Length >= requiredLength)
                return;

            buffer = SharedOversizedTextBuffer;
        }

        private static void ApplyDynamicText(TMP_Text label, string value, ref char[] buffer)
        {
            if (label == null)
                return;

            if (string.IsNullOrEmpty(value))
            {
                label.SetCharArray(System.Array.Empty<char>(), 0, 0);
                return;
            }

            EnsureCharCapacity(ref buffer, value.Length);
            int length = math.min(value.Length, buffer != null ? buffer.Length : 0);
            if (length <= 0)
            {
                label.SetCharArray(System.Array.Empty<char>(), 0, 0);
                return;
            }

            value.AsSpan(0, length).CopyTo(buffer.AsSpan());
            label.SetCharArray(buffer, 0, length);
        }

        private static void ApplyDynamicText(TMP_Text label, ReadOnlySpan<char> value, ref char[] buffer)
        {
            if (label == null)
                return;

            if (value.Length == 0)
            {
                label.SetCharArray(System.Array.Empty<char>(), 0, 0);
                return;
            }

            int length = CopySpanToBuffer(value, ref buffer);
            label.SetCharArray(buffer, 0, length);
        }

        private static void ApplyDynamicText(TMP_Text label, char[] valueBuffer, int valueLength)
        {
            if (label == null)
                return;

            if (valueBuffer == null || valueLength <= 0)
            {
                label.SetCharArray(System.Array.Empty<char>(), 0, 0);
                return;
            }

            int safeLength = Mathf.Clamp(valueLength, 0, valueBuffer.Length);
            label.SetCharArray(valueBuffer, 0, safeLength);
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

        private static int CopySpanToBuffer(ReadOnlySpan<char> source, ref char[] destination)
        {
            if (source.Length == 0)
            {
                EnsureCharCapacity(ref destination, 1);
                return 0;
            }

            EnsureCharCapacity(ref destination, source.Length);
            return CopySpanToBuffer(source, destination);
        }

        private static void ApplyTwoDigitText(TMP_Text label, int value, ref char[] buffer)
        {
            if (label == null)
                return;

            EnsureCharCapacity(ref buffer, 2);
            int clamped = Mathf.Clamp(value, 0, 99);
            buffer[0] = (char)('0' + (clamped / 10));
            buffer[1] = (char)('0' + (clamped % 10));
            label.SetCharArray(buffer, 0, 2);
        }

        private static void ApplyCountLabelText(TMP_Text label, int discovered, int total, ref char[] buffer)
        {
            if (label == null)
                return;

            EnsureCharCapacity(ref buffer, 32);
            int cursor = 0;
            if (!discovered.TryFormat(buffer.AsSpan(cursor), out int discoveredLength))
                discoveredLength = 0;
            cursor += discoveredLength;
            if (cursor < buffer.Length)
                buffer[cursor++] = '/';
            if (!total.TryFormat(buffer.AsSpan(cursor), out int totalLength))
                totalLength = 0;
            cursor += totalLength;
            if (cursor < buffer.Length)
                buffer[cursor++] = ' ';
            ReadOnlySpan<char> logsLiteral = "LOGS".AsSpan();
            logsLiteral.CopyTo(buffer.AsSpan(cursor));
            cursor += logsLiteral.Length;
            label.SetCharArray(buffer, 0, cursor);
        }

        private static void SetElementVisible(Component component, bool visible)
        {
            if (component == null)
                return;

            if (component is Graphic graphic)
            {
                Color color = graphic.color;
                color.a = visible ? 1f : 0f;
                graphic.color = color;
                graphic.raycastTarget = false;
                return;
            }

            if (component is CanvasGroup canvasGroup)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// Resolves the authored lore hologram material, or reports the gap once without throwing.
        /// </summary>
        /// <remarks>
        /// The <c>UnityEngine.Assertions.Assert.IsNotNull</c> removed from here THREW - nothing under Assets sets
        /// <c>Assert.raiseExceptions = false</c> - and the only caller is <see cref="Awake"/> (:361), which reaches
        /// it before <see cref="RebuildLoreBindingCache"/> (:362). One unassigned inspector slot therefore threw
        /// out of Awake and left the lore binding cache unbuilt.
        ///
        /// The assert guarded nothing: a null <c>_resolvedHologramMaterial</c> is the designed idle state and
        /// <see cref="RenderSelectedLoreHologram"/> already returns on it (:2425), alongside its null/empty checks
        /// on <c>hologramProxyMeshes</c>. The data log list, playback and localization are all independent of this
        /// material, so only the 3D lore hologram preview is lost.
        /// </remarks>
        private void EnsureHologramMaterial()
        {
            if (_resolvedHologramMaterial != null)
                return;

            _resolvedHologramMaterial = hologramMaterial;
            if (_resolvedHologramMaterial != null || _missingHologramMaterialAnnounced)
                return;

            // Report LAST. Awake continues to RebuildLoreBindingCache after this returns, so a future
            // re-introduced throw here can no longer leave the lore binding cache unbuilt.
            _missingHologramMaterialAnnounced = true;
            LogMissingLoreHologramMaterial();
        }

        /// <summary>
        /// One-shot report of the unassigned authored lore hologram material. The latch guarantees single emission
        /// and the method takes no arguments, so no string work or allocation reaches a tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingLoreHologramMaterial()
        {
            Hecton8.Core.H8Debug.LogError("PDADataLogTab: serialized field 'hologramMaterial' is unassigned, so the 3D lore hologram preview never renders this session - RenderSelectedLoreHologram returns on the null material. The data log list, audio log playback, PDA events and localization are unaffected. Runtime material generation is forbidden: assign the authored PDA hologram material in the inspector.");
        }

        private void RenderSelectedLoreHologram(float deltaTime)
        {
            if (!_detailVisible || _selectedIndex < 0)
                return;

            if (_resolvedHologramMaterial == null || hologramProxyMeshes == null || hologramProxyMeshes.Length == 0)
                return;

            AudioLogData log = GetSelectedLog();
            if (log == null)
                return;

            int meshIndex = log.ProxyMeshIndex;
            if ((uint)meshIndex >= (uint)hologramProxyMeshes.Length)
                return;

            Mesh mesh = hologramProxyMeshes[meshIndex];
            if (mesh == null)
                return;

            IPlayerRuntimeContext playerContext = s_cachedPlayerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null)
                return;

            _hologramAnimationTime += deltaTime;

            Transform anchor = transform;
            float bobWave = EvaluateCheapWaveSigned(_hologramAnimationTime * hologramBobFrequency);
            Vector3 worldPosition =
                anchor.position +
                anchor.up * (hologramHeight + bobWave * hologramBobAmplitude) +
                anchor.forward * hologramForwardOffset;

            float spinTurns = math.frac(_hologramAnimationTime * hologramSpinDegreesPerSecond * Inv360);
            int yawIndex = ((int)math.floor(spinTurns * HologramYawLutSize)) & HologramYawLutMask;
            Quaternion rotation = playerCamera.transform.rotation * s_hologramYawLut[yawIndex];

            Matrix4x4 matrix = Matrix4x4.TRS(worldPosition, rotation, Vector3.one * hologramScale);
            UnityEngine.Graphics.DrawMesh(
                mesh,
                matrix,
                _resolvedHologramMaterial,
                gameObject.layer,
                null,
                0,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                null,
                UnityEngine.Rendering.LightProbeUsage.Off);
        }

        private static float EvaluateCheapWaveSigned(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return (triangle * 2f) - 1f;
        }

        // --------------------------------------------------------------------------
        //  NESTED BUTTON HANDLERS
        // --------------------------------------------------------------------------

        private sealed class LogRowButton : MonoBehaviour,
            UnityEngine.EventSystems.IPointerClickHandler,
            UnityEngine.EventSystems.IPointerEnterHandler,
            UnityEngine.EventSystems.IPointerExitHandler
        {
            private PDADataLogTab _tab;
            private int _index;
            private Image _bg;
            private Color _normal;
            private Color _hover;

            public void Init(PDADataLogTab tab, int index, Image bg, Color normal, Color hover)
            {
                _tab = tab; _index = index; _bg = bg; _normal = normal; _hover = hover;
            }

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
                => _tab?.SelectLog(_index);

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _hover; }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _normal; }
        }

        private sealed class PlayButtonHandler : MonoBehaviour,
            UnityEngine.EventSystems.IPointerClickHandler,
            UnityEngine.EventSystems.IPointerEnterHandler,
            UnityEngine.EventSystems.IPointerExitHandler
        {
            private PDADataLogTab _tab;
            private Image _bg;
            private Color _normal;
            private Color _hover;

            public void Init(PDADataLogTab tab, Image bg, Color normal, Color hover)
            {
                _tab = tab; _bg = bg; _normal = normal; _hover = hover;
            }

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
            {
                AudioLogSystem sys = PDADataLogTab.ResolveAudioLogSystem();
                if (sys != null && sys.IsPlaying)
                    sys.StopPlayback();
                else
                    _tab?.PlaySelected();
            }

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _hover; }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _normal; }
        }
    }

}
