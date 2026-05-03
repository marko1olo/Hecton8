// ============================================================================
// HECTON-8 â€” PDADataLogTab.cs
// Ð’ÐºÐ»Ð°Ð´ÐºÐ° PDA: Ð°Ñ€Ñ…Ð¸Ð² Ð°ÑƒÐ´Ð¸Ð¾Ð´Ð½ÐµÐ²Ð½Ð¸ÐºÐ¾Ð² ÐºÐ¾Ð»Ð¾Ð½Ð¸Ð¸.
//
// Ð ÐžÐ›Ð¬:
//   â€¢ ÐžÑ‚Ð¾Ð±Ñ€Ð°Ð¶Ð°ÐµÑ‚ ÑÐ¿Ð¸ÑÐ¾Ðº Ð¾Ð±Ð½Ð°Ñ€ÑƒÐ¶ÐµÐ½Ð½Ñ‹Ñ… AudioLogData.
//   â€¢ ÐŸÐ¾Ð·Ð²Ð¾Ð»ÑÐµÑ‚ Ð¿ÐµÑ€ÐµÑÐ»ÑƒÑˆÐ°Ñ‚ÑŒ Ð»ÑŽÐ±ÑƒÑŽ Ð·Ð°Ð¿Ð¸ÑÑŒ.
//   â€¢ ÐŸÐ¾ÐºÐ°Ð·Ñ‹Ð²Ð°ÐµÑ‚ ÑÑƒÐ±Ñ‚Ð¸Ñ‚Ñ€Ñ‹ Ñ‚ÐµÐºÑƒÑ‰ÐµÐ¹ Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²Ð¾Ð´Ð¸Ð¼Ð¾Ð¹ Ð·Ð°Ð¿Ð¸ÑÐ¸.
//   â€¢ ÐžÐ±Ð½Ð¾Ð²Ð»ÑÐµÑ‚ÑÑ Ð¿Ñ€Ð¸ Ð¾Ñ‚ÐºÑ€Ñ‹Ñ‚Ð¸Ð¸ PDA (Ð½Ðµ Ð² Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð¼ Ð²Ñ€ÐµÐ¼ÐµÐ½Ð¸ â€” zero GC).
//
// ÐÐ Ð¥Ð˜Ð¢Ð•ÐšÐ¢Ð£Ð Ð:
//   â€¢ ÐŸÑ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ñ‹Ð¹ UI (Ð±ÐµÐ· UXML/USS) â€” Ð² ÑÑ‚Ð¸Ð»Ðµ PDAInventoryTab.
//   â€¢ ITickable â€” Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð´Ð»Ñ Ð¾Ð±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ñ Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð° Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÐ´ÐµÐ½Ð¸Ñ.
//   â€¢ Ð¡Ð»ÑƒÑˆÐ°ÐµÑ‚ AudioLogEvents Ð´Ð»Ñ Ð¾Ð±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ñ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ñ.
//   â€¢ ÐšÐ°Ñ‚Ð°Ð»Ð¾Ð³ Ð»Ð¾Ð³Ð¾Ð²: Ð½Ð°Ð·Ð½Ð°Ñ‡Ð°ÐµÑ‚ÑÑ Ð² Ð¸Ð½ÑÐ¿ÐµÐºÑ‚Ð¾Ñ€Ðµ (AudioLogData[]).
//
// ZERO GC:
//   â€¢ Pre-allocated ÑÐ¿Ð¸ÑÐ¾Ðº ÑÑ‚Ñ€Ð¾Ðº Ð´Ð»Ñ Ð¾Ñ‚Ð¾Ð±Ñ€Ð°Ð¶ÐµÐ½Ð¸Ñ.
//   â€¢ Dirty-flag Ð¾Ð±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ðµ Ñ‚ÐµÐºÑÑ‚Ð°.
//   â€¢ ÐÐ¸ÐºÐ°ÐºÐ¸Ñ… new/LINQ Ð² Tick.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Narrative;
using Hecton8.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Data Log Tab")]
    public sealed class PDADataLogTab : MonoBehaviour, ITickable, IUpdatable, IAudioLogEventListener, IPDAEventListener, ILocalizationLanguageChangedListener
    {
        private const string PlaybackTimerTemplate = "{0:00}:{1:00}";
        private static readonly char[] PlaybackTimerTemplateChars = PlaybackTimerTemplate.ToCharArray();
        private const int MaxRegisteredCatalogTabs = 4;

        // COLD ALLOC: RegistryBucket<PDADataLogTab>[4] - active PDA catalog sources for procedural lore lookup - owner: PDADataLogTab
        private static readonly RegistryBucket<PDADataLogTab> _registeredCatalogTabs = new RegistryBucket<PDADataLogTab>(MaxRegisteredCatalogTabs);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Catalog â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð’ÑÐµ AudioLogData Ð² Ð¸Ð³Ñ€Ðµ. ÐÐ°Ð·Ð½Ð°Ñ‡Ð¸Ñ‚ÑŒ Ð² Ð¸Ð½ÑÐ¿ÐµÐºÑ‚Ð¾Ñ€Ðµ.")]
        [SerializeField] private AudioLogData[] allLogs = new AudioLogData[0];

        [Header("â”€â”€ Font â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð¨Ñ€Ð¸Ñ„Ñ‚ Ñ ÐºÐ¸Ñ€Ð¸Ð»Ð»Ð¸Ñ†ÐµÐ¹. Ð•ÑÐ»Ð¸ null â€” Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ TMP default.")]
        [SerializeField] private TMPro.TMP_FontAsset _labelFont;

        [Header("â”€â”€ Colors â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private Color colorBackground  = new Color(0.04f, 0.06f, 0.10f, 0.95f);
        [SerializeField] private Color colorAccent      = new Color(0.20f, 0.80f, 0.60f, 1f);
        [SerializeField] private Color colorText        = new Color(0.85f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color colorDim         = new Color(0.45f, 0.50f, 0.45f, 1f);
        [SerializeField] private Color colorSelected    = new Color(0.10f, 0.25f, 0.18f, 1f);
        [SerializeField] private Color colorPlaying     = new Color(0.05f, 0.35f, 0.20f, 1f);

        [Header("── Hologram ──────────────────────────────")]
        [SerializeField] private Mesh[] hologramProxyMeshes = System.Array.Empty<Mesh>();
        [SerializeField] private Shader hologramShader;
        [SerializeField] private float hologramHeight = 0.14f;
        [SerializeField] private float hologramForwardOffset = 0.06f;
        [SerializeField] private float hologramScale = 0.045f;
        [SerializeField] private float hologramSpinDegreesPerSecond = 42f;
        [SerializeField] private float hologramBobAmplitude = 0.008f;
        [SerializeField] private float hologramBobFrequency = 1.7f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ðµ ÑÑ‚Ñ€Ð¾ÐºÐ¸ Ð´Ð»Ñ AudioLogCategory enum (Ð¸Ð·Ð±ÐµÐ³Ð°ÐµÑ‚ Enum.ToString() Ð´Ð°Ð¶Ðµ Ð² COLD path)</summary>

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

        // List rows â€” pre-allocated
        private readonly List<LogRow> _rows = new List<LogRow>(32);
        private readonly string[] _localizedCategoryLabels = new string[5]; // COLD ALLOC: string[5] — localized category labels — owner: PDADataLogTab
        // COLD ALLOC: uint[allLogs.Length] — precomputed lore hashes for direct packed-word archive reads — owner: PDADataLogTab
        private uint[] _catalogLoreHashes = Array.Empty<uint>();
        // COLD ALLOC: int[allLogs.Length] — precomputed lore record indices for direct packed-word archive reads — owner: PDADataLogTab
        private int[] _catalogLoreRecordIndices = Array.Empty<int>();
        // COLD ALLOC: char[128] — uppercase title staging buffer for allocation-free TMP updates — owner: PDADataLogTab
        private char[] _detailTitleBuffer = new char[128];
        // COLD ALLOC: char[256] — general PDA archive text staging buffer — owner: PDADataLogTab
        private char[] _dynamicTextBuffer = new char[256];
        // COLD ALLOC: char[2048] — PDA archive long-form summary staging buffer — owner: PDADataLogTab
        private char[] _summaryTextBuffer = new char[2048];
        // COLD ALLOC: Matrix4x4[1] — PDA data-log hologram draw buffer — owner: PDADataLogTab
        private readonly Matrix4x4[] _hologramMatrices = new Matrix4x4[1];

        // State
        private int _selectedIndex = -1;
        private bool _built;
        private bool _registered;
        private bool _pdaEventsRegistered;
        private bool _catalogTabRegistered;
        private bool _dirty;
        private bool _detailVisible = true;

        // Playback timer display
        private float _playbackRemaining;
        private int _prevTimerSeconds = -1;
        private string _prevSubtitleText;
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
        private string _resolvedSummaryBaseText = string.Empty;
        // COLD ALLOC: char[4096] — PDA archive hex-decrypt overlay staging buffer — owner: PDADataLogTab
        private char[] _resolvedSummaryHexBuffer = new char[4096];
        private int _resolvedSummaryHexLength;
        private float _hologramAnimationTime;
        private Material _runtimeHologramMaterial;
        private uint _latestSimulationLogHash;
        private float _latestSimulationLogTimestamp;

        private const float TICK_DT = 1f / 60f;
        private const float HiddenRecordDelaySeconds = 5f;
        private const float HiddenRecordBlinkSeconds = 0.18f;
        private const float SummaryDecryptDuration = 3f;
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

        private string _localizedArchiveTitle = "DATA ARCHIVE - HECTON-8 COLONY";
        private string _localizedCountFormat = "{0}/{1} LOGS";
        private string _localizedCategoryUnknown = "UNKNOWN";
        private string _localizedEncryptedLabel = "??? ENCRYPTED ???";
        private string _localizedEncryptedSummary = "Entry encrypted. Discovery required before archive access.";
        private string _localizedUnknownAuthor = "UNKNOWN";
        private string _localizedUnknownDate = "DATE UNKNOWN";
        private string _localizedAuthorPrefix = "AUTHOR: ";
        private string _localizedDatePrefix = "DATE: ";
        private string _localizedPlayAudioLabel = PlayAudioLabel;
        private string _localizedOpenTextLabel = OpenTextLogLabel;
        private string _localizedStopAudioLabel = StopAudioLabel;
        private string _localizedCloseTextLabel = CloseTextLogLabel;
        private string _localizedLockedLabel = LockedLogLabel;
        private string _localizedNoPayloadLabel = NoPayloadLabel;
        private string _localizedTextOnlySummaryPrefix = TextOnlySummaryPrefix;
        private string _localizedArchiveOnlySummaryPrefix = ArchiveOnlySummaryPrefix;
        private string _localizedEmptyStateText = "ARCHIVE EMPTY\nAssign AudioLogData assets in allLogs.";

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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  NESTED TYPE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null)
                _root = gameObject.AddComponent<RectTransform>();

            EnsureHologramMaterial();
            RebuildLoreBindingCache();
        }

        private void OnEnable()
        {
            TryRegisterCatalogTab();
            RebuildLocalizationCache();
            if (!_built) EnsureBuilt();
            ApplyLocalizedStaticText();

            TryRegister();

            AudioLogEvents.Register(this);
            TryRegisterPDAEvents();
            LocalizationEvents.RegisterLanguageListener(this);

            RebuildLoreBindingCache();
            _dirty = true;
        }

        private void OnDisable()
        {
            TryUnregisterCatalogTab();
            TryUnregister();

            AudioLogEvents.Unregister(this);
            UnregisterPDAEvents();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            TryUnregisterCatalogTab();
            TryUnregister();
            AudioLogEvents.Unregister(this);
            UnregisterPDAEvents();
            LocalizationEvents.UnregisterLanguageListener(this);
            PDAEvents.AssertUnregistered(this, nameof(PDADataLogTab));
            if (_runtimeHologramMaterial != null)
            {
                Destroy(_runtimeHologramMaterial);
                _runtimeHologramMaterial = null;
            }
        }

        private void TryRegisterPDAEvents()
        {
            if (_pdaEventsRegistered)
                return;

            PDAEvents.Register(this);
            _pdaEventsRegistered = true;
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            if (_dirty)
            {
                RefreshList();
                _dirty = false;
            }

            // ÐžÐ±Ð½Ð¾Ð²Ð»ÑÐµÐ¼ Ñ‚Ð°Ð¹Ð¼ÐµÑ€ Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÐ´ÐµÐ½Ð¸Ñ
            if (_playbackRemaining > 0f)
            {
                _playbackRemaining -= deltaTime;
                if (_playbackRemaining < 0f) _playbackRemaining = 0f;
                UpdatePlaybackTimer();
            }

            TickDetailNarrativeFx(deltaTime);
            RefreshStressReactiveDetailIfNeeded();
            RenderSelectedLoreHologram(deltaTime);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Ð’Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ Ð·Ð°Ð¿Ð¸ÑÑŒ Ð¿Ð¾ Ð¸Ð½Ð´ÐµÐºÑÑƒ Ð² allLogs.</summary>
        public void SelectLog(int logIndex)
        {
            if (logIndex < 0 || logIndex >= CatalogCount) return;

            _selectedIndex = logIndex;
            RefreshDetail();
            RefreshRowHighlights();
        }

        /// <summary>Ð’Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÑÑ‚Ð¸ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½ÑƒÑŽ Ð·Ð°Ð¿Ð¸ÑÑŒ.</summary>
        public void PlaySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= CatalogCount) return;

            AudioLogSystem system = Hecton8.Core.GlobalRegistry.AudioLogs;
            if (system == null) return;

            AudioLogData log = GetLog(_selectedIndex);
            if (log == null) return;

            if (!IsCatalogLogUnlocked(_selectedIndex)) return;
            if (!log.HasPlaybackPayload) return;

            system.PlayLog(log);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EVENT HANDLERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
            AudioLogData data = Hecton8.Core.GlobalRegistry.AudioLogs != null ? Hecton8.Core.GlobalRegistry.AudioLogs.CurrentLog : null;
            _playbackRemaining = durationSeconds > 0f ? durationSeconds : (data != null ? data.Duration : 0f);
            if (_subtitleLabel != null && data != null)
            {
                string visibleSubtitle = data.VisibleSubtitleOrFallback;
                ApplyDynamicText(_subtitleLabel, ResolveLogStressReactiveText(data, "subtitle", visibleSubtitle), ref _summaryTextBuffer);
                _prevSubtitleText = visibleSubtitle;
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
                ApplyDynamicText(_subtitleLabel, string.Empty, ref _summaryTextBuffer);
                _prevSubtitleText = string.Empty;
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
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BUILD UI
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _labelFont = LocalizedFontResolver.ResolveReadableFont(_labelFont);

            // Background
            Image bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
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
            ApplyDynamicText(_headerTitleLabel, _localizedArchiveTitle, ref _dynamicTextBuffer);
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

            // Scroll view would be ideal but for minimal impl â€“ static rows
            // COLD ALLOC: up to 32 rows
            BuildLogRows();

            if (_rows.Count == 0)
                BuildEmptyState();
        }

        private void BuildLogRows()
        {
            _rows.Clear();
            float rowH = 44f;
            float y = 0f;
            int logCount = CatalogCount;

            for (int i = 0; i < logCount && i < 32; i++)
            {
                AudioLogData log = GetLog(i);
                if (log == null) continue;

                RectTransform rowRoot = CreateRect($"Row_{i}", _listPanel);
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
                ApplyDynamicText(titleLabel, log.DisplayTitleOrFallback, ref _dynamicTextBuffer);

                TextMeshProUGUI catLabel = CreateText("Cat", rowRoot, 8f, colorDim, TextAlignmentOptions.MidlineRight);
                Anchor(catLabel.rectTransform, new Vector2(0.75f, 0), new Vector2(1, 1),
                    new Vector2(0, 0), new Vector2(-6, 0));
                ApplyDynamicText(catLabel, GetCachedCategoryLabel(log.category), ref _dynamicTextBuffer);

                // Button component
                LogRowButton btn = rowRoot.gameObject.AddComponent<LogRowButton>();
                int capturedIndex = i;
                btn.Init(this, capturedIndex, rowBg, colorDim, colorSelected);

                _rows.Add(new LogRow
                {
                    Root = rowRoot,
                    Background = rowBg,
                    IndexLabel = idxLabel,
                    TitleLabel = titleLabel,
                    CategoryLabel = catLabel,
                    Button = btn,
                    LogIndex = i
                });

                y += rowH;
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
            ApplyDynamicText(_emptyStateLabel, _localizedEmptyStateText, ref _summaryTextBuffer);
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
            _summaryMadnessFx = _summaryLabel.gameObject.GetComponent<LocalizedTextMadnessFx>();
            if (_summaryMadnessFx == null)
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
            _subtitleMadnessFx = _subtitleLabel.gameObject.GetComponent<LocalizedTextMadnessFx>();
            if (_subtitleMadnessFx == null)
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
            ApplyDynamicText(_playButtonLabel, _localizedPlayAudioLabel, ref _dynamicTextBuffer);
            Stretch(_playButtonLabel.rectTransform);

            PlayButtonHandler pbh = playBtn.gameObject.AddComponent<PlayButtonHandler>();
            pbh.Init(this, _playButtonBg, colorAccent, new Color(colorAccent.r * 0.7f, colorAccent.g * 0.7f, colorAccent.b * 0.7f));

            // Initial state
            SetDetailVisible(false);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  REFRESH
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void RefreshList()
        {
            LoreDatabaseManager database = Hecton8.Core.GlobalRegistry.LoreDatabase;
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
                    ApplyDynamicText(_emptyStateLabel, ResolveStressReactiveText(_localizedEmptyStateText), ref _summaryTextBuffer);
            }

            if (logCount == 0)
            {
                SetDetailVisible(false);
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                LogRow row = _rows[i];
                AudioLogData log = GetLog(row.LogIndex);
                bool isDiscovered = log != null && IsCatalogLogUnlocked(row.LogIndex);

                // Dim undiscovered entries
                Color textColor = isDiscovered ? colorText : colorDim;
                if (row.TitleLabel != null) row.TitleLabel.color = textColor;
                if (row.IndexLabel != null) row.IndexLabel.color = colorDim;
                if (row.CategoryLabel != null)
                    ApplyDynamicText(
                        row.CategoryLabel,
                        log != null
                            ? ResolveStressReactiveText(GetCachedCategoryLabel(log.category))
                            : ResolveStressReactiveText(_localizedCategoryUnknown),
                        ref _dynamicTextBuffer);
                if (row.CategoryLabel != null) row.CategoryLabel.color = isDiscovered ? colorDim : new Color(colorDim.r, colorDim.g, colorDim.b, 0.3f);

                // Replace title with ??? for undiscovered
                if (row.TitleLabel != null)
                    ApplyDynamicText(
                        row.TitleLabel,
                        isDiscovered
                            ? ResolveLogStressReactiveText(log, "row.title", log.DisplayTitleOrFallback)
                            : ResolveStressReactiveText(_localizedEncryptedLabel),
                        ref _summaryTextBuffer);
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
                string titleText = isDiscovered
                    ? ResolveLogStressReactiveText(log, "detail.title", log.DisplayTitleOrFallback)
                    : ResolveStressReactiveText(_localizedEncryptedLabel);
                SetUppercaseLabelText(_titleLabel, titleText, ref _detailTitleBuffer);
            }

            if (_authorLabel != null)
                ApplyDynamicText(
                    _authorLabel,
                    isDiscovered
                        ? ResolveLogStressReactiveText(log, "detail.author", string.Concat(_localizedAuthorPrefix, log.AuthorOrFallback))
                        : ResolveStressReactiveText(string.Concat(_localizedAuthorPrefix, _localizedUnknownAuthor)),
                    ref _dynamicTextBuffer);

            if (_dateLabel != null)
                ApplyDynamicText(
                    _dateLabel,
                    isDiscovered
                        ? ResolveLogStressReactiveText(log, "detail.date", log.RecordDateOrFallback)
                        : ResolveStressReactiveText(string.Concat(_localizedDatePrefix, _localizedUnknownDate)),
                    ref _dynamicTextBuffer);

            if (_summaryLabel != null)
            {
                string summaryText = isDiscovered
                    ? ResolveLogStressReactiveText(log, "detail.summary", GetCachedSummaryText(log))
                    : ResolveStressReactiveText(_localizedEncryptedSummary);
                ApplySummaryNarrativePresentation(log, isDiscovered, summaryText);
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
            AudioLogSystem system = Hecton8.Core.GlobalRegistry.AudioLogs;
            string playingId = system != null && system.IsPlaying && system.CurrentLog != null
                ? system.CurrentLog.logId
                : null;

            for (int i = 0; i < _rows.Count; i++)
            {
                LogRow row = _rows[i];
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
            AudioLogSystem system = Hecton8.Core.GlobalRegistry.AudioLogs;
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

            LoreDatabaseManager database = Hecton8.Core.GlobalRegistry.LoreDatabase;
            for (int i = 0; i < logCount; i++)
            {
                AudioLogData log = GetLog(i);
                if (log == null || string.IsNullOrWhiteSpace(log.SafeLogId))
                {
                    _catalogLoreHashes[i] = 0u;
                    _catalogLoreRecordIndices[i] = -1;
                    continue;
                }

                uint loreHash = LoreDatabaseManager.ComputeLoreHash(log.SafeLogId);
                _catalogLoreHashes[i] = loreHash;
                _catalogLoreRecordIndices[i] = database != null && database.TryGetRecordIndex(loreHash, out int recordIndex)
                    ? recordIndex
                    : -1;
            }

            _catalogLoreBindingsDirty = false;
        }

        private void EnsureLoreBindingCache()
        {
            if (_catalogLoreBindingsDirty || _catalogLoreHashes.Length != CatalogCount || _catalogLoreRecordIndices.Length != CatalogCount)
                RebuildLoreBindingCache();
        }

        private uint ResolveCatalogLoreHash(int logIndex)
        {
            EnsureLoreBindingCache();
            return (uint)logIndex < (uint)_catalogLoreHashes.Length
                ? _catalogLoreHashes[logIndex]
                : 0u;
        }

        private bool IsCatalogLogUnlocked(int logIndex)
        {
            if (logIndex < 0 || logIndex >= CatalogCount)
                return false;

            EnsureLoreBindingCache();
            LoreDatabaseManager database = Hecton8.Core.GlobalRegistry.LoreDatabase;
            if (database == null || !database.TryGetPackedUnlockWords(out Unity.Collections.NativeArray<uint> words))
                return false;

            int recordIndex = _catalogLoreRecordIndices[logIndex];
            if (recordIndex < 0)
            {
                uint loreHash = _catalogLoreHashes[logIndex];
                if (loreHash != 0u && database.TryGetRecordIndex(loreHash, out int resolvedIndex))
                {
                    _catalogLoreRecordIndices[logIndex] = resolvedIndex;
                    recordIndex = resolvedIndex;
                }
            }

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

        private static string GetSummaryText(AudioLogData log)
        {
            if (log == null)
                return string.Empty;

            if (log.IsTextOnlyPlayback)
                return TextOnlySummaryPrefix + log.ArchiveSummaryOrFallback;

            if (!log.HasPlaybackPayload && log.HasArchiveSummary)
                return ArchiveOnlySummaryPrefix + log.ArchiveSummaryOrFallback;

            return log.ArchiveSummaryOrFallback;
        }

        private static string ResolvePlayButtonLabel(AudioLogSystem system, AudioLogData selectedLog, bool isDiscovered)
        {
            if (system != null && system.IsPlaying)
            {
                AudioLogData playingLog = system.CurrentLog;
                return playingLog != null && playingLog.IsTextOnlyPlayback
                    ? CloseTextLogLabel
                    : StopAudioLabel;
            }

            if (!isDiscovered)
                return LockedLogLabel;

            if (selectedLog == null || !selectedLog.HasPlaybackPayload)
                return NoPayloadLabel;

            return selectedLog.HasAudioClip ? PlayAudioLabel : OpenTextLogLabel;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  UI HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            _lastStressCorruptionBucket = int.MinValue;
            ResetDetailNarrativeState(clearPendingDecryption: false);
            RebuildLocalizationCache();
            ApplyLocalizedStaticText();
            _dirty = true;
            RefreshList();
            RefreshDetail();
            RefreshPlayButton();
        }

        private void RebuildLocalizationCache()
        {
            _localizedArchiveTitle = ResolveLocalized(LocalizationKeys.AUDIOLOG_ARCHIVE_TITLE, "DATA ARCHIVE - HECTON-8 COLONY");
            _localizedCountFormat = ResolveLocalized(LocalizationKeys.AUDIOLOG_COUNT, "{0}/{1} LOGS");
            _localizedCategoryLabels[(int)AudioLogCategory.Personal] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_PERSONAL, "PERSONAL");
            _localizedCategoryLabels[(int)AudioLogCategory.Technical] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_TECHNICAL, "TECHNICAL");
            _localizedCategoryLabels[(int)AudioLogCategory.Emergency] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_EMERGENCY, "EMERGENCY");
            _localizedCategoryLabels[(int)AudioLogCategory.Atlas6] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_ATLAS6, "ATLAS6");
            _localizedCategoryLabels[(int)AudioLogCategory.Unknown] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_UNKNOWN, "UNKNOWN");
            _localizedCategoryUnknown = _localizedCategoryLabels[(int)AudioLogCategory.Unknown];
            _localizedEncryptedLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_ENCRYPTED, "??? ENCRYPTED ???");
            _localizedEncryptedSummary = ResolveLocalized(LocalizationKeys.AUDIOLOG_ENCRYPTED_SUMMARY, "Entry encrypted. Discovery required before archive access.");
            _localizedUnknownAuthor = ResolveLocalized(LocalizationKeys.AUDIOLOG_UNKNOWN_AUTHOR, "UNKNOWN");
            _localizedUnknownDate = ResolveLocalized(LocalizationKeys.AUDIOLOG_UNKNOWN_DATE, "DATE UNKNOWN");
            _localizedAuthorPrefix = string.Concat(ResolveLocalized(LocalizationKeys.INTERACT_AUTHOR, "AUTHOR"), ": ");
            _localizedDatePrefix = string.Concat(ResolveLocalized(LocalizationKeys.INTERACT_DATE, "DATE"), ": ");
            _localizedPlayAudioLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_PLAY, PlayAudioLabel);
            _localizedOpenTextLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_OPEN_TEXT, OpenTextLogLabel);
            _localizedStopAudioLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_STOP, StopAudioLabel);
            _localizedCloseTextLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_CLOSE_TEXT, CloseTextLogLabel);
            _localizedLockedLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_LOCKED, LockedLogLabel);
            _localizedNoPayloadLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_NO_PAYLOAD, NoPayloadLabel);
            _localizedTextOnlySummaryPrefix = ResolveLocalized(LocalizationKeys.AUDIOLOG_TEXT_ONLY_PREFIX, TextOnlySummaryPrefix);
            _localizedArchiveOnlySummaryPrefix = ResolveLocalized(LocalizationKeys.AUDIOLOG_ARCHIVE_ONLY_PREFIX, ArchiveOnlySummaryPrefix);
            _localizedEmptyStateText = string.Concat(
                ResolveLocalized(LocalizationKeys.AUDIOLOG_EMPTY_ARCHIVE, "ARCHIVE EMPTY"),
                "\n",
                ResolveLocalized(LocalizationKeys.AUDIOLOG_EMPTY_ARCHIVE_HINT, "Assign AudioLogData assets in allLogs."));
        }

        private void ApplyLocalizedStaticText()
        {
            if (_headerTitleLabel != null)
                ApplyDynamicText(_headerTitleLabel, ResolveStressReactiveText(_localizedArchiveTitle), ref _dynamicTextBuffer);

            if (_emptyStateLabel != null)
                ApplyDynamicText(_emptyStateLabel, ResolveStressReactiveText(_localizedEmptyStateText), ref _summaryTextBuffer);
        }

        private string GetCachedSummaryText(AudioLogData log)
        {
            if (log == null)
                return string.Empty;

            if (log.IsTextOnlyPlayback)
                return string.Concat(_localizedTextOnlySummaryPrefix, log.ArchiveSummaryOrFallback);

            if (!log.HasPlaybackPayload && log.HasArchiveSummary)
                return string.Concat(_localizedArchiveOnlySummaryPrefix, log.ArchiveSummaryOrFallback);

            return log.ArchiveSummaryOrFallback;
        }

        private string GetCachedPlayButtonLabel(AudioLogSystem system, AudioLogData selectedLog, bool isDiscovered)
        {
            if (system != null && system.IsPlaying)
            {
                AudioLogData playingLog = system.CurrentLog;
                return playingLog != null && playingLog.IsTextOnlyPlayback
                    ? _localizedCloseTextLabel
                    : _localizedStopAudioLabel;
            }

            if (!isDiscovered)
                return _localizedLockedLabel;

            if (selectedLog == null || !selectedLog.HasPlaybackPayload)
                return _localizedNoPayloadLabel;

            return selectedLog.HasAudioClip
                ? _localizedPlayAudioLabel
                : _localizedOpenTextLabel;
        }

        private string GetCachedCategoryLabel(AudioLogCategory category)
        {
            int categoryIndex = (int)category;
            if ((uint)categoryIndex < (uint)_localizedCategoryLabels.Length)
                return _localizedCategoryLabels[categoryIndex];

            return _localizedCategoryUnknown;
        }

        private void RefreshStressReactiveDetailIfNeeded()
        {
            if (!_detailVisible)
            {
                _lastStressCorruptionBucket = int.MinValue;
                return;
            }

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            RefreshList();
            RefreshDetail();

            if (_subtitleLabel != null)
            {
                AudioLogSystem system = Hecton8.Core.GlobalRegistry.AudioLogs;
                AudioLogData subtitleLog = system != null && system.IsPlaying ? system.CurrentLog : GetSelectedLog();
                string displaySubtitle = ResolveLogStressReactiveText(subtitleLog, "subtitle", _prevSubtitleText);
                ApplyDynamicText(_subtitleLabel, displaySubtitle, ref _summaryTextBuffer);

                UpdateMadnessFxState(subtitleLog, _subtitleMadnessFx);
            }
        }

        private static void UpdateMadnessFxState(AudioLogData log, LocalizedTextMadnessFx effect)
        {
            if (effect == null)
                return;

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            effect.SetEffectActive(
                manager != null &&
                log != null &&
                !string.IsNullOrWhiteSpace(log.logId) &&
                manager.IsMadnessWhisperVisualActive());
        }

        private static string ResolveStressReactiveText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.ApplyHullStressCorruptionIfNeeded(text)
                : text;
        }

        private static string ResolveLogStressReactiveText(AudioLogData log, string surfaceId, string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null)
                return text;

            if (log == null || string.IsNullOrWhiteSpace(log.logId))
                return manager.ApplyHullStressCorruptionIfNeeded(text);

            return manager.ApplyPdaLoreCorruptionIfNeeded(string.Concat(log.logId, ".", surfaceId), text);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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

        private void ApplySummaryNarrativePresentation(AudioLogData log, bool isDiscovered, string summaryText)
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

            _resolvedSummaryBaseText = summaryText ?? string.Empty;

            if (_summaryDecryptActive)
            {
                ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseText, ref _summaryTextBuffer);
                _summaryLabel.ForceMeshUpdate();
                _summaryVisibleCharacterTarget = _summaryLabel.textInfo.characterCount;
                UpdateSummaryDecryptPresentation();
                return;
            }

            if (_hiddenRecordFlashActive)
                return;

            _summaryLabel.maxVisibleCharacters = int.MaxValue;
            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseText, ref _summaryTextBuffer);

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
            BuildHexCipherText(_resolvedSummaryBaseText, ref _resolvedSummaryHexBuffer, out _resolvedSummaryHexLength);

            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseText, ref _summaryTextBuffer);
            _summaryLabel.ForceMeshUpdate();
            _summaryVisibleCharacterTarget = _summaryLabel.textInfo.characterCount;

            ApplyDynamicText(_summaryDecryptOverlayLabel, _resolvedSummaryHexBuffer, _resolvedSummaryHexLength);
            _summaryDecryptOverlayLabel.maxVisibleCharacters = int.MaxValue;
            SetElementVisible(_summaryDecryptOverlayLabel, true);
            _summaryDecryptOverlayLabel.ForceMeshUpdate();
            _summaryHexVisibleCharacterTarget = _summaryDecryptOverlayLabel.textInfo.characterCount;

            UpdateSummaryDecryptPresentation();
            if (_summaryMadnessFx != null)
                _summaryMadnessFx.SetEffectActive(false);
        }

        private void UpdateSummaryDecryptPresentation()
        {
            if (!_summaryDecryptActive || _summaryLabel == null || _summaryDecryptOverlayLabel == null)
                return;

            float t = Mathf.Clamp01(_summaryDecryptTimer / SummaryDecryptDuration);
            int summaryVisible = Mathf.Clamp(Mathf.CeilToInt(_summaryVisibleCharacterTarget * t), 0, _summaryVisibleCharacterTarget);
            int hexVisible = Mathf.Clamp(
                Mathf.CeilToInt(_summaryHexVisibleCharacterTarget * (1f - t)),
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

            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseText, ref _summaryTextBuffer);

            _detailReadTimer = 0f;
            if (_summaryMadnessFx != null)
                UpdateMadnessFxState(log, _summaryMadnessFx);
        }

        private void TriggerHiddenRecordFlash(AudioLogData log)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null || _summaryLabel == null || log == null)
                return;

            int cycle = Mathf.Max(1, Mathf.FloorToInt(Time.unscaledTime));
            if (!manager.TryResolveMadnessWhisperPreview(string.Concat(log.logId, ".", SummaryHiddenSurfaceId), cycle, out string hiddenText) ||
                string.IsNullOrEmpty(hiddenText))
            {
                _hiddenRecordFlashConsumed = true;
                return;
            }

            _hiddenRecordFlashActive = true;
            _hiddenRecordFlashConsumed = true;
            _hiddenRecordFlashTimer = HiddenRecordBlinkSeconds;
            _summaryLabel.maxVisibleCharacters = int.MaxValue;
            ApplyDynamicText(_summaryLabel, hiddenText, ref _summaryTextBuffer);
            if (_summaryMadnessFx != null)
                _summaryMadnessFx.SetEffectActive(true);
        }

        private void CompleteHiddenRecordFlash(AudioLogData log)
        {
            _hiddenRecordFlashActive = false;
            _hiddenRecordFlashTimer = 0f;
            _summaryLabel.maxVisibleCharacters = int.MaxValue;
            ApplyDynamicText(_summaryLabel, _resolvedSummaryBaseText, ref _summaryTextBuffer);
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
            _resolvedSummaryBaseText = string.Empty;
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
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge == null || !bridge.TryGetActiveArtificialInteriorState(out ArtificialInteriorState state))
                return false;

            return state.Type == StructureType.MegaWreck;
        }

        private static void BuildHexCipherText(string sourceText, ref char[] buffer, out int length)
        {
            if (string.IsNullOrEmpty(sourceText))
            {
                EnsureCharCapacity(ref buffer, 1);
                length = 0;
                return;
            }

            EnsureCharCapacity(ref buffer, sourceText.Length * 3);
            int cursor = 0;
            for (int i = 0; i < sourceText.Length; i++)
            {
                char current = sourceText[i];
                if (current == '\n' || current == '\r')
                {
                    buffer[cursor++] = current;
                    continue;
                }

                if (char.IsWhiteSpace(current))
                {
                    buffer[cursor++] = ' ';
                    continue;
                }

                int value = current & 0xFF;
                buffer[cursor++] = HexDigits[(value >> 4) & 0x0F];
                buffer[cursor++] = HexDigits[value & 0x0F];

                if (i + 1 < sourceText.Length && !char.IsWhiteSpace(sourceText[i + 1]))
                    buffer[cursor++] = ' ';
            }
            length = cursor;
        }

        private RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
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

            LocNumericBuffer.Write(new System.ReadOnlySpan<char>(PlaybackTimerTemplateChars), LocNumericArg.Int(minutes), LocNumericArg.Int(seconds), out char[] buffer, out int length);
            int safeLength = Mathf.Clamp(length, 0, buffer != null ? buffer.Length : 0);
            _playbackTimerLabel.SetCharArray(buffer, 0, safeLength);
            _playbackTimerLabel.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static void SetUppercaseLabelText(TMP_Text label, string source, ref char[] buffer)
        {
            if (label == null)
                return;

            WriteUppercaseToBuffer(source, ref buffer, out int length);
            label.SetCharArray(buffer, 0, length);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static void WriteUppercaseToBuffer(string source, ref char[] buffer, out int length)
        {
            if (string.IsNullOrEmpty(source))
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

            int capacity = buffer == null ? 32 : buffer.Length;
            while (capacity < requiredLength)
                capacity <<= 1;

            buffer = new char[capacity]; // COLD ALLOC: char[capacity] — expanded PDA text staging buffer — owner: PDADataLogTab
        }

        private static void ApplyDynamicText(TMP_Text label, string value, ref char[] buffer)
        {
            if (label == null)
                return;

            if (string.IsNullOrEmpty(value))
            {
                label.SetCharArray(System.Array.Empty<char>(), 0, 0);
                label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
                return;
            }

            EnsureCharCapacity(ref buffer, value.Length);
            value.AsSpan().CopyTo(buffer.AsSpan());
            label.SetCharArray(buffer, 0, value.Length);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static void ApplyDynamicText(TMP_Text label, char[] valueBuffer, int valueLength)
        {
            if (label == null)
                return;

            if (valueBuffer == null || valueLength <= 0)
            {
                label.SetCharArray(System.Array.Empty<char>(), 0, 0);
                label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
                return;
            }

            int safeLength = Mathf.Clamp(valueLength, 0, valueBuffer.Length);
            label.SetCharArray(valueBuffer, 0, safeLength);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
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
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
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
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static void SetElementVisible(Component component, bool visible)
        {
            if (component == null)
                return;

            if (!component.TryGetComponent(out CanvasGroup canvasGroup))
                canvasGroup = component.gameObject.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] — PDA data-log element visibility gate — owner: PDADataLogTab

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void EnsureHologramMaterial()
        {
            if (_runtimeHologramMaterial != null || hologramShader == null)
                return;

            _runtimeHologramMaterial = new Material(hologramShader)
            {
                name = "Runtime_PDADataLogHologram"
            }; // COLD ALLOC: Material[1] — PDA data-log hologram material — owner: PDADataLogTab
        }

        private void RenderSelectedLoreHologram(float deltaTime)
        {
            if (!_detailVisible || _selectedIndex < 0)
                return;

            EnsureHologramMaterial();
            if (_runtimeHologramMaterial == null || hologramProxyMeshes == null || hologramProxyMeshes.Length == 0)
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

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null)
                return;

            _hologramAnimationTime += deltaTime;

            Transform anchor = transform;
            Vector3 worldPosition =
                anchor.position +
                anchor.up * (hologramHeight + Mathf.Sin(_hologramAnimationTime * hologramBobFrequency) * hologramBobAmplitude) +
                anchor.forward * hologramForwardOffset;

            Vector3 facing = worldPosition - playerCamera.transform.position;
            Quaternion rotation = facing.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(facing.normalized, anchor.up) * Quaternion.Euler(0f, _hologramAnimationTime * hologramSpinDegreesPerSecond, 0f)
                : Quaternion.identity;

            _hologramMatrices[0] = Matrix4x4.TRS(worldPosition, rotation, Vector3.one * hologramScale);
            Graphics.DrawMeshInstanced(mesh, 0, _runtimeHologramMaterial, _hologramMatrices, 1, null, UnityEngine.Rendering.ShadowCastingMode.Off, false, gameObject.layer);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  NESTED BUTTON HANDLERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
                AudioLogSystem sys = Hecton8.Core.GlobalRegistry.AudioLogs;
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

