using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton.Localization;
using Hecton8.Input;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Shell Chrome")]
    public sealed class PDAShellChrome : MonoBehaviour, ITickable, IPDAEventListener
    {
        private const string TitleTextValue = "HECTON-8 PERSONAL DATA ASSISTANT";
        private const string ActiveTabInventory = "ACTIVE TAB // INVENTORY";
        private const string ActiveTabLoadout = "ACTIVE TAB // LOADOUT";
        private const string ActiveTabConstruction = "ACTIVE TAB // CONSTRUCTION";
        private const string ActiveTabBarter = "ACTIVE TAB // BARTER";
        private const string ActiveTabDataLog = "ACTIVE TAB // DATA LOG";
        private const string ActiveTabSpectrum = "ACTIVE TAB // SPECTRUM";
        private const string ActiveTabDiagnostics = "ACTIVE TAB // DIAGNOSTICS";
        private const string ActiveTabUnknown = "ACTIVE TAB // UNKNOWN";
        private const string LeftFooterFormat = "CARGO {0}/{1}  |  MASS {2:0.0} kg  |  READY TOOLS {3}/{4}";
        private const string RightFooterOnlineFormat = "O2 {0:0}%  |  PWR {1:0}%  |  PDA ONLINE";
        private const string RightFooterStandbyFormat = "O2 {0:0}%  |  PWR {1:0}%  |  PDA STANDBY";
        private const string IntrusionTabOverride = "SYSTEM STATE // HACKED";
        private const string IntrusionHintFormat = "REBOOT // HOLD {0} FOR 3.0S";
        private const string IntrusionFooterFormat = "O2 {0:0}%  |  PWR {1:0}%  |  REBOOT {2}%";
        private const string MechModeTag = "[MECH-MODE ACTIVE]";
        private const string LeftFooterNumericTemplate = "CARGO {N0}/{N1}  |  MASS {N2:0.0} kg  |  READY TOOLS {N3}/{N4}";
        private const string RightFooterOnlineNumericTemplate = "O2 {N0:0}%  |  PWR {N1:0}%  |  PDA ONLINE";
        private const string RightFooterStandbyNumericTemplate = "O2 {N0:0}%  |  PWR {N1:0}%  |  PDA STANDBY";
        private const string IntrusionFooterNumericTemplate = "O2 {N0:0}%  |  PWR {N1:0}%  |  REBOOT {N2}%";
        private const int NumericTemplateWriteAttemptLimit = 8;
        private const int CharCapacityGrowthWatchdogLimit = 31;

        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.56f, 0.74f, 0.71f, 0.72f);
        private static readonly Color Stable = new Color(0.08f, 0.2f, 0.22f, 0.74f);
        private static readonly Color Warning = new Color(0.3f, 0.2f, 0.06f, 0.82f);
        private static readonly Color Critical = new Color(0.34f, 0.12f, 0.12f, 0.84f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color AlertText = new Color(1f, 0.88f, 0.72f, 0.96f);
        private static readonly Color MechModeTint = new Color(0.62f, 0.76f, 0.34f, 0.9f);
        private static readonly Color MechModeText = new Color(0.9f, 0.96f, 0.72f, 0.94f);
        private static readonly int ShaderColorId = Shader.PropertyToID("_Color");
        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly char[] s_emptyBuffer = new char[1];

        [Header("References")]
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        private bool _built;
        private RectTransform _chromeRoot;
        private CanvasGroup _chromeCanvasGroup;
        private Image _headerBg;
        private Image _footerBg;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _tabText;
        private TextMeshProUGUI _intrusionText;
        private TextMeshProUGUI _contextTagText;
        private TextMeshProUGUI _leftFooterText;
        private TextMeshProUGUI _rightFooterText;
        private Material _headerMaterial;
        private Material _footerMaterial;
        private Material _titleMaterial;
        private Material _tabMaterial;
        private Material _intrusionMaterial;
        private Material _contextTagMaterial;
        private Material _leftFooterMaterial;
        private Material _rightFooterMaterial;
        private int _lastActiveTab = int.MinValue;
        private int _lastCargoCells = -1;
        private int _lastCargoTotal = -1;
        private int _lastWeightDeci = int.MinValue;
        private int _lastReadyTools = -1;
        private int _lastAssignedTools = -1;
        private int _lastOxygenPercent = int.MinValue;
        private int _lastEnergyPercent = int.MinValue;
        private bool _lastPdaOpen;
        private PlayerInventory _subscribedInventory;
        private PlayerToolManager _subscribedToolManager;
        private HectonSurvivalSystem _subscribedSurvivalSystem;
        private string _localizedTitle = TitleTextValue;
        private string _localizedTabInventory = ActiveTabInventory;
        private string _localizedTabLoadout = ActiveTabLoadout;
        private string _localizedTabConstruction = ActiveTabConstruction;
        private string _localizedTabBarter = ActiveTabBarter;
        private string _localizedTabDataLog = ActiveTabDataLog;
        private string _localizedTabSpectrum = ActiveTabSpectrum;
        private string _localizedTabDiagnostics = ActiveTabDiagnostics;
        private string _localizedTabUnknown = ActiveTabUnknown;
        private string _localizedLeftFooterFormat = LeftFooterFormat;
        private string _localizedRightFooterOnlineFormat = RightFooterOnlineFormat;
        private string _localizedRightFooterStandbyFormat = RightFooterStandbyFormat;
        private string _localizedLeftFooterNumericTemplate = LeftFooterNumericTemplate;
        private string _localizedRightFooterOnlineNumericTemplate = RightFooterOnlineNumericTemplate;
        private string _localizedRightFooterStandbyNumericTemplate = RightFooterStandbyNumericTemplate;
        private string _localizedIntrusionFooterNumericTemplate = IntrusionFooterNumericTemplate;
        private bool _registeredToTickManager;
        private int _lastStressCorruptionBucket = int.MinValue;
        private PDAIntrusionManager _intrusionManager;
        private HectonPlayerMovement _playerMovement;
        private bool _lastIntrusionActive;
        private bool _lastMechModeActive;
        private int _lastRebootProgressPercent = -1;
        private string _cachedRebootBinding = string.Empty;
        private InputDisplayStyle _cachedRebootBindingStyle = (InputDisplayStyle)(-1);
        private string _localizedMechModeTag = MechModeTag;
        private string _localizedIntrusionHintPrefix = "REBOOT // HOLD ";
        private string _localizedIntrusionHintSuffix = " FOR 3.0S";
        // COLD ALLOC: char[160] - PDA title staging buffer - owner: PDAShellChrome
        private char[] _titleBuffer = new char[160];
        // COLD ALLOC: char[128] - PDA tab staging buffer - owner: PDAShellChrome
        private char[] _tabBuffer = new char[128];
        // COLD ALLOC: char[160] - PDA left footer staging buffer - owner: PDAShellChrome
        private char[] _leftFooterBuffer = new char[160];
        // COLD ALLOC: char[128] - PDA right footer staging buffer - owner: PDAShellChrome
        private char[] _rightFooterBuffer = new char[128];
        // COLD ALLOC: char[64] - PDA context tag staging buffer - owner: PDAShellChrome
        private char[] _contextTagBuffer = new char[64];
        // COLD ALLOC: char[96] - intrusion status hint buffer - owner: PDAShellChrome
        private readonly char[] _intrusionHintBuffer = new char[96];
        private int _appliedTitleVersion = int.MinValue;
        private int _appliedTabVersion = int.MinValue;
        private int _appliedIntrusionVersion = int.MinValue;
        private int _appliedContextTagVersion = int.MinValue;
        private int _appliedLeftFooterVersion = int.MinValue;
        private int _appliedRightFooterVersion = int.MinValue;

        private void Awake()
        {
            RefreshBindings();
        }

        private void OnEnable()
        {
            RefreshBindings();
            RefreshLocalizedTextCache();
            EnsureBuilt();
            Subscribe();
            RefreshChrome();
            EvaluateTickRegistration();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            Unsubscribe();
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnregisterFromTickManager();
            PDAEvents.AssertUnregistered(this, nameof(PDAShellChrome));
            DestroyMaterialInstance(ref _headerMaterial);
            DestroyMaterialInstance(ref _footerMaterial);
            DestroyMaterialInstance(ref _titleMaterial);
            DestroyMaterialInstance(ref _tabMaterial);
            DestroyMaterialInstance(ref _intrusionMaterial);
            DestroyMaterialInstance(ref _contextTagMaterial);
            DestroyMaterialInstance(ref _leftFooterMaterial);
            DestroyMaterialInstance(ref _rightFooterMaterial);
        }

        private void AutoResolve()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerPDA == null && playerContext != null)
                playerPDA = playerContext.PlayerPDA;
            if (playerInventory == null && playerContext != null)
                playerInventory = playerContext.Inventory;
            if (toolManager == null && playerContext != null)
                toolManager = playerContext.ToolManager;
            if (_playerMovement == null && playerContext != null)
                _playerMovement = playerContext.PlayerMovement;

            if ((!playerPDA || !playerInventory || !toolManager || !survivalSystem) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerInventory == null)
                    playerTransform.TryGetComponent(out playerInventory);

                if (toolManager == null)
                    playerTransform.TryGetComponent(out toolManager);

                if (survivalSystem == null)
                    playerTransform.TryGetComponent(out survivalSystem);

                if (playerPDA == null)
                    playerTransform.TryGetComponent(out playerPDA);

                if (_intrusionManager == null)
                    playerTransform.TryGetComponent(out _intrusionManager);

                if (_playerMovement == null)
                    playerTransform.TryGetComponent(out _playerMovement);
            }

            if (playerPDA == null)
                playerPDA = GetComponent<PlayerPDA>() ?? GetComponentInParent<PlayerPDA>();

            if (_intrusionManager == null && playerPDA != null)
                _intrusionManager = playerPDA.GetComponent<PDAIntrusionManager>();

            if (_intrusionManager == null)
                _intrusionManager = GetComponent<PDAIntrusionManager>() ?? GetComponentInParent<PDAIntrusionManager>();
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
        }

        private void RefreshBindings()
        {
            PlayerInventory previousInventory = playerInventory;
            PlayerToolManager previousToolManager = toolManager;
            HectonSurvivalSystem previousSurvivalSystem = survivalSystem;

            AutoResolve();

            if (!ReferenceEquals(previousInventory, playerInventory))
            {
                UnsubscribeInventory(previousInventory);
                SubscribeInventory(playerInventory);
            }

            if (!ReferenceEquals(previousToolManager, toolManager))
            {
                UnsubscribeToolManager(previousToolManager);
                SubscribeToolManager(toolManager);
            }

            if (!ReferenceEquals(previousSurvivalSystem, survivalSystem))
            {
                UnsubscribeSurvival(previousSurvivalSystem);
                SubscribeSurvival(survivalSystem);
            }
        }

        private void Subscribe()
        {
            PDAEvents.Register(this);
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;

            SubscribeInventory(playerInventory);
            SubscribeToolManager(toolManager);
            SubscribeSurvival(survivalSystem);
        }

        private void Unsubscribe()
        {
            PDAEvents.Unregister(this);
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;

            UnsubscribeInventory(_subscribedInventory);
            UnsubscribeToolManager(_subscribedToolManager);
            UnsubscribeSurvival(_subscribedSurvivalSystem);
        }

        private void SubscribeInventory(PlayerInventory inventory)
        {
            if (inventory == null || ReferenceEquals(_subscribedInventory, inventory))
                return;

            inventory.InventoryChanged += HandleInventoryChanged;
            _subscribedInventory = inventory;
        }

        private void UnsubscribeInventory(PlayerInventory inventory)
        {
            if (inventory == null)
                return;

            inventory.InventoryChanged -= HandleInventoryChanged;
            if (ReferenceEquals(_subscribedInventory, inventory))
                _subscribedInventory = null;
        }

        private void SubscribeToolManager(PlayerToolManager manager)
        {
            if (manager == null || ReferenceEquals(_subscribedToolManager, manager))
                return;

            manager.ActiveSlotChanged += HandleSlotChanged;
            manager.ToolAssignmentsChanged += HandleAssignmentsChanged;
            _subscribedToolManager = manager;
        }

        private void UnsubscribeToolManager(PlayerToolManager manager)
        {
            if (manager == null)
                return;

            manager.ActiveSlotChanged -= HandleSlotChanged;
            manager.ToolAssignmentsChanged -= HandleAssignmentsChanged;
            if (ReferenceEquals(_subscribedToolManager, manager))
                _subscribedToolManager = null;
        }

        private void SubscribeSurvival(HectonSurvivalSystem system)
        {
            if (system == null || ReferenceEquals(_subscribedSurvivalSystem, system))
                return;

            system.OnOxygenChanged += HandleOxygenChanged;
            system.OnEnergyChanged += HandleEnergyChanged;
            _subscribedSurvivalSystem = system;
        }

        private void UnsubscribeSurvival(HectonSurvivalSystem system)
        {
            if (system == null)
                return;

            system.OnOxygenChanged -= HandleOxygenChanged;
            system.OnEnergyChanged -= HandleEnergyChanged;
            if (ReferenceEquals(_subscribedSurvivalSystem, system))
                _subscribedSurvivalSystem = null;
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    HandlePdaOpened(payload.CurrentTab);
                    break;
                case PDAEventType.Closed:
                    HandlePdaClosed(payload.DurationSeconds);
                    break;
                case PDAEventType.TabChanged:
                    HandleTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        private void HandlePdaOpened(int _)
        {
            RefreshBindings();
            RefreshChrome();
            EvaluateTickRegistration();
        }
        private void HandlePdaClosed(float _)
        {
            RefreshChrome();
            EvaluateTickRegistration();
        }

        private void HandleTabChanged(int _, int __)
        {
            RefreshChrome();
        }

        public void Tick(float deltaTime)
        {
            if (!PlayerPDA.IsOpen)
            {
                _lastStressCorruptionBucket = int.MinValue;
                _lastIntrusionActive = false;
                _lastRebootProgressPercent = -1;
                UnregisterFromTickManager();
                return;
            }

            LocalizationManager manager = LocalizationManager.Instance;
            if (_intrusionManager == null)
                _intrusionManager = PDAIntrusionManager.ActiveRuntimeInstance;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            bool intrusionActive = _intrusionManager != null && _intrusionManager.IsHacked;
            bool mechModeActive = _playerMovement != null && _playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            int rebootProgressPercent = intrusionActive
                ? Mathf.RoundToInt(_intrusionManager.RebootProgressNormalized * 100f)
                : 0;

            if (stressBucket == _lastStressCorruptionBucket &&
                intrusionActive == _lastIntrusionActive &&
                mechModeActive == _lastMechModeActive &&
                rebootProgressPercent == _lastRebootProgressPercent)
                return;

            _lastStressCorruptionBucket = stressBucket;
            _lastIntrusionActive = intrusionActive;
            _lastMechModeActive = mechModeActive;
            _lastRebootProgressPercent = rebootProgressPercent;
            _lastActiveTab = int.MinValue;
            _lastCargoCells = -1;
            _lastCargoTotal = -1;
            _lastWeightDeci = int.MinValue;
            _lastReadyTools = -1;
            _lastAssignedTools = -1;
            _lastOxygenPercent = int.MinValue;
            _lastEnergyPercent = int.MinValue;
            RefreshChrome();
        }

        private void HandleInventoryChanged()
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome();
        }

        private void HandleSlotChanged(int _)
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome();
        }

        private void HandleAssignmentsChanged()
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome();
        }

        private void HandleOxygenChanged(float _)
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome();
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform self = transform as RectTransform;
            if (self == null)
                return;

            _chromeRoot = FindExistingChild(self, "ShellChrome") ?? CreateRect(self, "ShellChrome");
            Stretch(_chromeRoot, 0f, 0f, 0f, 0f);
            _chromeRoot.SetAsLastSibling();
            _chromeCanvasGroup = EnsureCanvasGroup(_chromeRoot);
            _chromeCanvasGroup.interactable = false;
            _chromeCanvasGroup.blocksRaycasts = false;
            _chromeCanvasGroup.alpha = 0f;

            ClearChildren(_chromeRoot);

            RectTransform header = CreateRect(_chromeRoot, "Header");
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -8f), new Vector2(-12f, 42f));
            _headerBg = EnsureImage(header.gameObject);
            _headerBg.color = Color.white;
            _headerBg.raycastTarget = false;

            RectTransform footer = CreateRect(_chromeRoot, "Footer");
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 8f), new Vector2(-12f, 38f));
            _footerBg = EnsureImage(footer.gameObject);
            _footerBg.color = Color.white;
            _footerBg.raycastTarget = false;

            CreateRule(_chromeRoot, new Vector2(0.04f, 1f), new Vector2(0.96f, 1f), -54f);
            CreateRule(_chromeRoot, new Vector2(0.04f, 0f), new Vector2(0.96f, 0f), 54f);
            CreateCornerBracket(_chromeRoot, true, true);
            CreateCornerBracket(_chromeRoot, false, true);
            CreateCornerBracket(_chromeRoot, true, false);
            CreateCornerBracket(_chromeRoot, false, false);

            _titleText = CreateText(header, "Title", labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(_titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(14f, 0f), new Vector2(-8f, 0f));
            _titleText.color = Color.white;
            ApplyDynamicBuffer(_titleText, s_emptyBuffer, 0);

            _tabText = CreateText(header, "Tab", numericFont, 11f, FontStyles.Bold, TextAlignmentOptions.Right);
            Anchor(_tabText.rectTransform, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-14f, 0f));
            _tabText.color = Color.white;

            _intrusionText = CreateText(_chromeRoot, "Intrusion", numericFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(_intrusionText.rectTransform, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), new Vector2(0f, -66f), new Vector2(0f, -38f));
            _intrusionText.color = Color.white;
            _intrusionText.alpha = 0f;
            ApplyDynamicBuffer(_intrusionText, s_emptyBuffer, 0);

            _contextTagText = CreateText(_chromeRoot, "ContextTag", numericFont, 10f, FontStyles.Bold, TextAlignmentOptions.Right);
            Anchor(_contextTagText.rectTransform, new Vector2(0.56f, 1f), new Vector2(0.96f, 1f), new Vector2(0f, -66f), new Vector2(0f, -40f));
            _contextTagText.color = Color.white;
            _contextTagText.alpha = 0f;
            ApplyDynamicBuffer(_contextTagText, s_emptyBuffer, 0);

            _leftFooterText = CreateText(footer, "FooterLeft", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(_leftFooterText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(14f, 0f), new Vector2(-8f, 0f));
            _leftFooterText.color = Color.white;

            _rightFooterText = CreateText(footer, "FooterRight", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(_rightFooterText.rectTransform, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-14f, 0f));
            _rightFooterText.color = Color.white;

            EnsureMaterialInstances();
            ApplyChromeMaterialPalette(Stable, Primary, Dim, AlertText, Dim, DimLow, MechModeText);

            _built = true;
        }

        private void HandleEnergyChanged(float _)
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshLocalizedTextCache();
            _lastActiveTab = int.MinValue;
            _lastCargoCells = -1;
            _lastCargoTotal = -1;
            _lastWeightDeci = int.MinValue;
            _lastReadyTools = -1;
            _lastAssignedTools = -1;
            _lastOxygenPercent = int.MinValue;
            _lastEnergyPercent = int.MinValue;
            _lastIntrusionActive = false;
            _lastMechModeActive = false;
            _lastRebootProgressPercent = -1;
            _cachedRebootBinding = string.Empty;
            _cachedRebootBindingStyle = (InputDisplayStyle)(-1);
            InvalidateAppliedLabelVersions();
            RefreshChrome();
            EvaluateTickRegistration();
        }

        private void RefreshChrome()
        {
            if (!_built)
                return;

            if (_intrusionManager == null)
                _intrusionManager = PDAIntrusionManager.ActiveRuntimeInstance;

            LocalizationManager manager = LocalizationManager.Instance;
            bool rtl = manager != null && LocalizationManager.IsRightToLeftLanguage(manager.CurrentLanguage);
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            bool useStressReactiveStrings = stressBucket > 0;
            float stressReactiveIntensity = useStressReactiveStrings && manager != null
                ? manager.GetHullStressCorruptionIntensity()
                : 0f;

            if (_titleText != null)
            {
                ReadOnlySpan<char> titleSpan = _localizedTitle.AsSpan();
                CopyTextToBuffer(titleSpan, ref _titleBuffer, out int titleLength);
                ApplyTextBuffer(
                    _titleText,
                    _titleBuffer,
                    titleLength,
                    rtl,
                    useStressReactiveStrings,
                    stressReactiveIntensity,
                    101,
                    ComputeTextVersion(titleSpan, 101, stressBucket),
                    ref _appliedTitleVersion);
            }

            string tabName = GetActiveTabLabel();
            int cargoCells = playerInventory != null && playerInventory.Grid != null
                ? CountUsedCells(playerInventory.Grid)
                : 0;
            int cargoTotal = playerInventory != null && playerInventory.Grid != null
                ? playerInventory.Grid.Columns * playerInventory.Grid.Rows
                : 48;
            float weight = playerInventory != null ? playerInventory.TotalWeight : 0f;
            float energy = survivalSystem != null ? survivalSystem.EnergyNormalized : 0f;
            float oxygen = survivalSystem != null ? survivalSystem.OxygenNormalized : 0f;
            int readyTools = CountReadyTools();
            int assignedTools = toolManager != null ? CountAssignedTools() : 0;
            int activeTabIndex = playerPDA != null ? playerPDA.ActiveTab : -1;
            int weightDeci = Mathf.RoundToInt(weight * 10f);
            int oxygenPercent = Mathf.RoundToInt(oxygen * 100f);
            int energyPercent = Mathf.RoundToInt(energy * 100f);
            bool pdaOpen = PlayerPDA.IsOpen;
            bool intrusionActive = _intrusionManager != null && _intrusionManager.IsHacked;
            bool mechModeActive = _playerMovement != null && _playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            int rebootProgressPercent = intrusionActive
                ? Mathf.RoundToInt(_intrusionManager.RebootProgressNormalized * 100f)
                : 0;

            if (_tabText != null &&
                (_lastActiveTab != activeTabIndex || _lastIntrusionActive != intrusionActive || _appliedTabVersion == int.MinValue))
            {
                string tabSource = intrusionActive ? IntrusionTabOverride : tabName;
                ReadOnlySpan<char> tabSpan = tabSource.AsSpan();
                CopyTextToBuffer(tabSpan, ref _tabBuffer, out int tabLength);
                ApplyTextBuffer(
                    _tabText,
                    _tabBuffer,
                    tabLength,
                    rtl,
                    useStressReactiveStrings,
                    stressReactiveIntensity,
                    113,
                    ComputeTextVersion(tabSpan, 113, stressBucket),
                    ref _appliedTabVersion);
                _lastActiveTab = activeTabIndex;
            }

            if (_leftFooterText != null &&
                (_lastCargoCells != cargoCells ||
                 _lastCargoTotal != cargoTotal ||
                 _lastWeightDeci != weightDeci ||
                 _lastReadyTools != readyTools ||
                 _lastAssignedTools != assignedTools ||
                 _appliedLeftFooterVersion == int.MinValue))
            {
                int safeAssignedTools = Mathf.Max(assignedTools, 1);
                TryWriteNumericTemplate(
                    _localizedLeftFooterNumericTemplate.AsSpan(),
                    ref _leftFooterBuffer,
                    LocNumericArg.Int(cargoCells),
                    LocNumericArg.Int(cargoTotal),
                    LocNumericArg.Float(weight),
                    LocNumericArg.Int(readyTools),
                    LocNumericArg.Int(safeAssignedTools),
                    out int leftFooterLength);

                int leftFooterVersion = unchecked((((((cargoCells * 397) ^ cargoTotal) * 397) ^ weightDeci) * 397) ^ readyTools ^ (safeAssignedTools << 8) ^ (stressBucket << 16) ^ 211);
                ApplyTextBuffer(
                    _leftFooterText,
                    _leftFooterBuffer,
                    leftFooterLength,
                    rtl,
                    useStressReactiveStrings,
                    stressReactiveIntensity,
                    211,
                    leftFooterVersion,
                    ref _appliedLeftFooterVersion);

                _lastCargoCells = cargoCells;
                _lastCargoTotal = cargoTotal;
                _lastWeightDeci = weightDeci;
                _lastReadyTools = readyTools;
                _lastAssignedTools = assignedTools;
            }

            if (_rightFooterText != null &&
                (_lastOxygenPercent != oxygenPercent ||
                 _lastEnergyPercent != energyPercent ||
                 _lastPdaOpen != pdaOpen ||
                 _lastIntrusionActive != intrusionActive ||
                 _lastRebootProgressPercent != rebootProgressPercent ||
                 _appliedRightFooterVersion == int.MinValue))
            {
                if (intrusionActive)
                {
                    TryWriteNumericTemplate(
                        _localizedIntrusionFooterNumericTemplate.AsSpan(),
                        ref _rightFooterBuffer,
                        LocNumericArg.Int(oxygenPercent),
                        LocNumericArg.Int(energyPercent),
                        LocNumericArg.Int(rebootProgressPercent),
                        out int rightFooterLength);

                    int intrusionFooterVersion = unchecked((((oxygenPercent * 397) ^ energyPercent) * 397) ^ rebootProgressPercent ^ (stressBucket << 16) ^ 223);
                    ApplyTextBuffer(
                        _rightFooterText,
                        _rightFooterBuffer,
                        rightFooterLength,
                        rtl,
                        useStressReactiveStrings,
                        stressReactiveIntensity,
                        223,
                        intrusionFooterVersion,
                        ref _appliedRightFooterVersion);
                }
                else if (pdaOpen)
                {
                    TryWriteNumericTemplate(
                        _localizedRightFooterOnlineNumericTemplate.AsSpan(),
                        ref _rightFooterBuffer,
                        LocNumericArg.Int(oxygenPercent),
                        LocNumericArg.Int(energyPercent),
                        out int rightFooterLength);

                    int onlineFooterVersion = unchecked((((oxygenPercent * 397) ^ energyPercent) * 397) ^ (stressBucket << 16) ^ 227);
                    ApplyTextBuffer(
                        _rightFooterText,
                        _rightFooterBuffer,
                        rightFooterLength,
                        rtl,
                        useStressReactiveStrings,
                        stressReactiveIntensity,
                        227,
                        onlineFooterVersion,
                        ref _appliedRightFooterVersion);
                }
                else
                {
                    TryWriteNumericTemplate(
                        _localizedRightFooterStandbyNumericTemplate.AsSpan(),
                        ref _rightFooterBuffer,
                        LocNumericArg.Int(oxygenPercent),
                        LocNumericArg.Int(energyPercent),
                        out int rightFooterLength);

                    int standbyFooterVersion = unchecked((((oxygenPercent * 397) ^ energyPercent) * 397) ^ (stressBucket << 16) ^ 229);
                    ApplyTextBuffer(
                        _rightFooterText,
                        _rightFooterBuffer,
                        rightFooterLength,
                        rtl,
                        useStressReactiveStrings,
                        stressReactiveIntensity,
                        229,
                        standbyFooterVersion,
                        ref _appliedRightFooterVersion);
                }

                _lastOxygenPercent = oxygenPercent;
                _lastEnergyPercent = energyPercent;
                _lastPdaOpen = pdaOpen;
                _lastIntrusionActive = intrusionActive;
                _lastRebootProgressPercent = rebootProgressPercent;
            }

            if (_intrusionText != null)
            {
                if (intrusionActive)
                {
                    string rebootBinding = ResolveRebootBinding();
                    SetIntrusionHintText(_intrusionText, rebootBinding, rtl, useStressReactiveStrings, stressReactiveIntensity, stressBucket);
                    _intrusionText.alpha = 1f;
                }
                else
                {
                    ClearLabel(_intrusionText, ref _appliedIntrusionVersion);
                    _intrusionText.alpha = 0f;
                }
            }

            if (_contextTagText != null)
            {
                if (mechModeActive)
                {
                    ReadOnlySpan<char> contextSpan = _localizedMechModeTag.AsSpan();
                    CopyTextToBuffer(contextSpan, ref _contextTagBuffer, out int contextLength);
                    ApplyTextBuffer(
                        _contextTagText,
                        _contextTagBuffer,
                        contextLength,
                        rtl,
                        useStressReactiveStrings,
                        stressReactiveIntensity,
                        131,
                        ComputeTextVersion(contextSpan, 131, stressBucket),
                        ref _appliedContextTagVersion);
                    _contextTagText.alpha = 1f;
                }
                else
                {
                    ClearLabel(_contextTagText, ref _appliedContextTagVersion);
                    _contextTagText.alpha = 0f;
                }
            }

            Color severity = GetShellSeverityColor(energy, oxygen, weight, readyTools, assignedTools);
            if (mechModeActive)
                severity = Color.Lerp(severity, MechModeTint, 0.42f);
            Color titleColor = mechModeActive ? MechModeText : Primary;
            Color tabColor = intrusionActive || energy < 0.25f || oxygen < 0.3f ? AlertText : (mechModeActive ? MechModeText : Dim);
            Color leftFooterColor = mechModeActive ? MechModeText : Dim;
            Color rightFooterColor = intrusionActive || energy < 0.25f || oxygen < 0.3f ? AlertText : (mechModeActive ? MechModeText : DimLow);
            ApplyChromeMaterialPalette(severity, titleColor, tabColor, AlertText, leftFooterColor, rightFooterColor, MechModeText);
            if (_chromeCanvasGroup != null)
                _chromeCanvasGroup.alpha = pdaOpen ? 1f : 0f;
        }

        private string GetActiveTabLabel()
        {
            if (playerPDA == null)
                return _localizedTabUnknown;

            switch (playerPDA.ActiveTab)
            {
                case 0: return _localizedTabInventory;
                case 1: return _localizedTabLoadout;
                case 2: return _localizedTabConstruction;
                case 3: return _localizedTabBarter;
                case 4: return _localizedTabDataLog;
                case 5: return _localizedTabSpectrum;
                case 7: return _localizedTabDiagnostics;
                default: return _localizedTabUnknown;
            }
        }

        private void RefreshLocalizedTextCache()
        {
            _localizedTitle = ResolveLocalized(LocalizationKeys.PDA_SHELL_TITLE, TitleTextValue);
            _localizedTabInventory = ResolveLocalized(LocalizationKeys.PDA_TAB_INVENTORY, ActiveTabInventory);
            _localizedTabLoadout = ResolveLocalized(LocalizationKeys.PDA_TAB_LOADOUT, ActiveTabLoadout);
            _localizedTabConstruction = ResolveLocalized(LocalizationKeys.PDA_TAB_CONSTRUCTION, ActiveTabConstruction);
            _localizedTabBarter = ResolveLocalized(LocalizationKeys.PDA_TAB_BARTER, ActiveTabBarter);
            _localizedTabDataLog = ResolveLocalized(LocalizationKeys.PDA_TAB_DATA_LOG, ActiveTabDataLog);
            _localizedTabSpectrum = ResolveLocalized(LocalizationKeys.PDA_TAB_SPECTRUM, ActiveTabSpectrum);
            _localizedTabDiagnostics = ResolveLocalized(LocalizationKeys.PDA_TAB_DIAGNOSTICS, ActiveTabDiagnostics);
            _localizedTabUnknown = ResolveLocalized(LocalizationKeys.PDA_TAB_UNKNOWN, ActiveTabUnknown);
            _localizedLeftFooterFormat = ResolveLocalized(LocalizationKeys.PDA_FOOTER_LEFT, LeftFooterFormat);
            _localizedRightFooterOnlineFormat = ResolveLocalized(LocalizationKeys.PDA_FOOTER_RIGHT_ONLINE, RightFooterOnlineFormat);
            _localizedRightFooterStandbyFormat = ResolveLocalized(LocalizationKeys.PDA_FOOTER_RIGHT_STANDBY, RightFooterStandbyFormat);
            _localizedMechModeTag = ResolveLocalized(LocalizationKeys.PDA_MECH_MODE_ACTIVE, MechModeTag);
            _localizedLeftFooterNumericTemplate = ConvertToNumericTemplate(_localizedLeftFooterFormat, LeftFooterNumericTemplate);
            _localizedRightFooterOnlineNumericTemplate = ConvertToNumericTemplate(_localizedRightFooterOnlineFormat, RightFooterOnlineNumericTemplate);
            _localizedRightFooterStandbyNumericTemplate = ConvertToNumericTemplate(_localizedRightFooterStandbyFormat, RightFooterStandbyNumericTemplate);
            _localizedIntrusionFooterNumericTemplate = IntrusionFooterNumericTemplate;
            SplitSinglePlaceholderTemplate(IntrusionHintFormat, out _localizedIntrusionHintPrefix, out _localizedIntrusionHintSuffix);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null)
                return fallback;

            return manager.GetOrFallback(manager.CurrentLanguage, key, fallback);
        }

        private static string ConvertToNumericTemplate(string template, string fallback)
        {
            string source = string.IsNullOrEmpty(template) ? fallback : template;
            if (string.IsNullOrEmpty(source))
                return fallback;

            bool foundNumericPlaceholder = false;
            for (int i = 0; i < source.Length - 1; i++)
            {
                if (source[i] == '{' && source[i + 1] >= '0' && source[i + 1] <= '9')
                {
                    foundNumericPlaceholder = true;
                    break;
                }
            }

            if (!foundNumericPlaceholder)
                return source;

            StringBuilder builder = new StringBuilder(source.Length + 8);
            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                if (current == '{' &&
                    i + 1 < source.Length &&
                    source[i + 1] >= '0' &&
                    source[i + 1] <= '9')
                {
                    builder.Append("{N");
                    builder.Append(source[i + 1]);
                    i++;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static void SplitSinglePlaceholderTemplate(string template, out string prefix, out string suffix)
        {
            string source = string.IsNullOrEmpty(template) ? IntrusionHintFormat : template;
            int placeholderIndex = source.IndexOf("{0", System.StringComparison.Ordinal);
            if (placeholderIndex < 0)
            {
                prefix = source;
                suffix = string.Empty;
                return;
            }

            int closeIndex = source.IndexOf('}', placeholderIndex);
            if (closeIndex < 0)
            {
                prefix = source;
                suffix = string.Empty;
                return;
            }

            prefix = source.Substring(0, placeholderIndex);
            suffix = closeIndex + 1 < source.Length
                ? source.Substring(closeIndex + 1)
                : string.Empty;
        }

        private void SetIntrusionHintText(
            TMP_Text label,
            string binding,
            bool rtl,
            bool useStressReactiveStrings,
            float stressReactiveIntensity,
            int stressBucket)
        {
            if (label == null)
                return;

            int index = 0;
            index = CopyLiteralToBuffer(_intrusionHintBuffer, index, _localizedIntrusionHintPrefix);
            index = CopyLiteralToBuffer(_intrusionHintBuffer, index, string.IsNullOrEmpty(binding) ? "SUBMIT" : binding);
            index = CopyLiteralToBuffer(_intrusionHintBuffer, index, _localizedIntrusionHintSuffix);

            int version = unchecked((((ComputeTextVersion(_localizedIntrusionHintPrefix.AsSpan(), 239, stressBucket) * 397) ^ ComputeTextVersion(_localizedIntrusionHintSuffix.AsSpan(), 241, stressBucket)) * 397) ^ ComputeTextVersion((string.IsNullOrEmpty(binding) ? "SUBMIT" : binding).AsSpan(), 243, stressBucket));
            ApplyTextBuffer(
                label,
                _intrusionHintBuffer,
                index,
                rtl,
                useStressReactiveStrings,
                stressReactiveIntensity,
                239,
                version,
                ref _appliedIntrusionVersion);
        }

        private static void ApplyDynamicBuffer(TMP_Text label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static int CopyLiteralToBuffer(char[] buffer, int startIndex, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value) || startIndex >= buffer.Length)
                return startIndex;

            int copyLength = Mathf.Min(value.Length, buffer.Length - startIndex);
            value.AsSpan(0, copyLength).CopyTo(buffer.AsSpan(startIndex, copyLength));
            return startIndex + copyLength;
        }

        private static void CopyTextToBuffer(ReadOnlySpan<char> source, ref char[] buffer, out int length)
        {
            EnsureCharCapacity(ref buffer, source.Length);
            source.CopyTo(buffer);
            length = source.Length;
        }

        private static void EnsureCharCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer != null && buffer.Length >= requiredLength)
                return;

            int capacity = buffer == null ? 32 : buffer.Length;
            int growthWatchdog = CharCapacityGrowthWatchdogLimit;
            while (capacity < requiredLength && growthWatchdog-- > 0)
            {
                if (capacity > (int.MaxValue >> 1))
                {
                    capacity = requiredLength;
                    break;
                }

                capacity <<= 1;
            }

            if (capacity < requiredLength)
                capacity = requiredLength;

            buffer = new char[capacity]; // COLD ALLOC: char[capacity] - expanded PDA text staging buffer - owner: PDAShellChrome
        }

        private static int ComputeTextVersion(ReadOnlySpan<char> source, int salt, int stressBucket)
        {
            unchecked
            {
                int version = salt ^ (stressBucket << 8);
                for (int i = 0; i < source.Length; i++)
                    version = (version * 397) ^ source[i];
                return version;
            }
        }

        private static void ClearLabel(TMP_Text label, ref int appliedVersion)
        {
            if (label == null)
                return;

            if (appliedVersion == 0)
                return;

            ApplyDynamicBuffer(label, s_emptyBuffer, 0);
            appliedVersion = 0;
        }

        private static void ApplyTextBuffer(
            TMP_Text label,
            char[] sourceBuffer,
            int sourceLength,
            bool rtl,
            bool useStressReactiveStrings,
            float stressReactiveIntensity,
            int corruptionSalt,
            int version,
            ref int appliedVersion)
        {
            if (label == null || sourceBuffer == null)
                return;

            if (label.isRightToLeftText != rtl)
                label.isRightToLeftText = rtl;

            if (appliedVersion == version)
                return;

            if (useStressReactiveStrings)
            {
                GlitchEncoder.ApplyDecay(
                    sourceBuffer,
                    sourceLength,
                    stressReactiveIntensity,
                    unchecked((version * 397) ^ corruptionSalt),
                    out char[] corruptedBuffer,
                    out int corruptedLength);
                ApplyDynamicBuffer(label, corruptedBuffer, corruptedLength);
            }
            else
            {
                ApplyDynamicBuffer(label, sourceBuffer, sourceLength);
            }

            appliedVersion = version;
        }

        private static void TryWriteNumericTemplate(
            ReadOnlySpan<char> template,
            ref char[] buffer,
            LocNumericArg value0,
            LocNumericArg value1,
            out int length)
        {
            EnsureCharCapacity(ref buffer, template.Length + 24);
            for (int attempt = 0; attempt < NumericTemplateWriteAttemptLimit; attempt++)
            {
                if (LocNumericBuffer.TryWrite(template, buffer.AsSpan(), value0, value1, out length))
                    return;

                if (!TryExpandNumericTemplateBuffer(ref buffer))
                    break;
            }

            CopyTextToBuffer(template, ref buffer, out length);
        }

        private static void TryWriteNumericTemplate(
            ReadOnlySpan<char> template,
            ref char[] buffer,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            out int length)
        {
            EnsureCharCapacity(ref buffer, template.Length + 24);
            for (int attempt = 0; attempt < NumericTemplateWriteAttemptLimit; attempt++)
            {
                if (LocNumericBuffer.TryWrite(template, buffer.AsSpan(), value0, value1, value2, out length))
                    return;

                if (!TryExpandNumericTemplateBuffer(ref buffer))
                    break;
            }

            CopyTextToBuffer(template, ref buffer, out length);
        }

        private static void TryWriteNumericTemplate(
            ReadOnlySpan<char> template,
            ref char[] buffer,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            LocNumericArg value4,
            out int length)
        {
            EnsureCharCapacity(ref buffer, template.Length + 32);
            for (int attempt = 0; attempt < NumericTemplateWriteAttemptLimit; attempt++)
            {
                if (LocNumericBuffer.TryWrite(template, buffer.AsSpan(), value0, value1, value2, value3, value4, out length))
                    return;

                if (!TryExpandNumericTemplateBuffer(ref buffer))
                    break;
            }

            CopyTextToBuffer(template, ref buffer, out length);
        }

        private static bool TryExpandNumericTemplateBuffer(ref char[] buffer)
        {
            int currentLength = buffer != null ? buffer.Length : 0;
            if (currentLength <= 0 || currentLength > (int.MaxValue >> 1))
                return false;

            EnsureCharCapacity(ref buffer, currentLength << 1);
            return buffer != null && buffer.Length > currentLength;
        }

        private void InvalidateAppliedLabelVersions()
        {
            _appliedTitleVersion = int.MinValue;
            _appliedTabVersion = int.MinValue;
            _appliedIntrusionVersion = int.MinValue;
            _appliedContextTagVersion = int.MinValue;
            _appliedLeftFooterVersion = int.MinValue;
            _appliedRightFooterVersion = int.MinValue;
        }

        private string ResolveRebootBinding()
        {
            InputManager inputManager = InputManager.Instance;
            InputDisplayStyle displayStyle = inputManager != null
                ? inputManager.CurrentDisplayStyle
                : InputDisplayStyle.KeyboardMouse;

            if (!string.IsNullOrEmpty(_cachedRebootBinding) && _cachedRebootBindingStyle == displayStyle)
                return _cachedRebootBinding;

            string binding = string.Empty;
            if (inputManager != null)
            {
                if (!inputManager.TryGetBindingMarkupForToken("submit", out binding) || string.IsNullOrWhiteSpace(binding))
                    binding = inputManager.GetBindingDisplayString("Submit", "UI", -1);
            }

            if (string.IsNullOrWhiteSpace(binding))
                binding = "SUBMIT";

            _cachedRebootBinding = binding;
            _cachedRebootBindingStyle = displayStyle;
            return _cachedRebootBinding;
        }

        private void EvaluateTickRegistration()
        {
            if (PlayerPDA.IsOpen)
                RegisterToTickManager();
            else
                UnregisterFromTickManager();
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

            _registeredToTickManager = false;
        }

        private int CountAssignedTools()
        {
            if (toolManager == null)
                return 0;

            int count = 0;
            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                if (toolManager.GetAssignedToolPrefab(i) != null)
                    count++;
            }

            return count;
        }

        private int CountReadyTools()
        {
            if (toolManager == null)
                return 0;

            int count = 0;
            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                if (toolManager.GetAssignedToolPrefab(i) != null && toolManager.IsToolAvailableInSlot(i))
                    count++;
            }

            return count;
        }

        private static int CountUsedCells(InventoryGrid grid)
        {
            return grid != null ? grid.OccupiedCells : 0;
        }

        private static Color GetShellSeverityColor(float energy, float oxygen, float weight, int readyTools, int assignedTools)
        {
            if (energy < 0.25f || oxygen < 0.3f)
                return Critical;

            if (weight > 22f || readyTools == 0 || (assignedTools > 0 && readyTools < assignedTools))
                return Warning;

            return Stable;
        }

        private static RectTransform FindExistingChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child as RectTransform;
            }

            return null;
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target == null)
                return null;

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            return canvasGroup != null ? canvasGroup : target.gameObject.AddComponent<CanvasGroup>();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            LocalizedTMPAutoSizer.Configure(text, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            return text;
        }

        private void EnsureMaterialInstances()
        {
            EnsureGraphicMaterialInstance(_headerBg, ref _headerMaterial);
            EnsureGraphicMaterialInstance(_footerBg, ref _footerMaterial);
            EnsureTextMaterialInstance(_titleText, ref _titleMaterial);
            EnsureTextMaterialInstance(_tabText, ref _tabMaterial);
            EnsureTextMaterialInstance(_intrusionText, ref _intrusionMaterial);
            EnsureTextMaterialInstance(_contextTagText, ref _contextTagMaterial);
            EnsureTextMaterialInstance(_leftFooterText, ref _leftFooterMaterial);
            EnsureTextMaterialInstance(_rightFooterText, ref _rightFooterMaterial);
        }

        private void ApplyChromeMaterialPalette(
            Color shellColor,
            Color titleColor,
            Color tabColor,
            Color intrusionColor,
            Color leftFooterColor,
            Color rightFooterColor,
            Color contextTagColor)
        {
            ApplyGraphicMaterialColor(_headerBg, _headerMaterial, shellColor);
            ApplyGraphicMaterialColor(_footerBg, _footerMaterial, shellColor);
            ApplyTextMaterialColor(_titleText, _titleMaterial, titleColor);
            ApplyTextMaterialColor(_tabText, _tabMaterial, tabColor);
            ApplyTextMaterialColor(_intrusionText, _intrusionMaterial, intrusionColor);
            ApplyTextMaterialColor(_contextTagText, _contextTagMaterial, contextTagColor);
            ApplyTextMaterialColor(_leftFooterText, _leftFooterMaterial, leftFooterColor);
            ApplyTextMaterialColor(_rightFooterText, _rightFooterMaterial, rightFooterColor);
        }

        private static void EnsureGraphicMaterialInstance(Graphic graphic, ref Material material)
        {
            if (graphic == null || material != null)
                return;

            Material source = graphic.materialForRendering;
            if (source == null)
                return;

            material = new Material(source); // COLD ALLOC: Material[1] — UI chrome palette instance — owner: PDAShellChrome
            material.name = source.name + "_PDAShellChrome";
            graphic.material = material;
        }

        private static void EnsureTextMaterialInstance(TextMeshProUGUI text, ref Material material)
        {
            if (text == null || material != null)
                return;

            Material source = text.fontSharedMaterial;
            if (source == null)
                return;

            material = new Material(source); // COLD ALLOC: Material[1] — TMP chrome palette instance — owner: PDAShellChrome
            material.name = source.name + "_PDAShellChrome";
            text.fontSharedMaterial = material;
        }

        private static void ApplyGraphicMaterialColor(Graphic graphic, Material material, Color color)
        {
            if (graphic == null)
                return;

            if (material != null && material.HasProperty(ShaderColorId))
            {
                material.SetColor(ShaderColorId, color);
                return;
            }

            graphic.color = color;
        }

        private static void ApplyTextMaterialColor(TextMeshProUGUI text, Material material, Color color)
        {
            if (text == null)
                return;

            if (material != null && material.HasProperty(FaceColorId))
            {
                material.SetColor(FaceColorId, color);
                return;
            }

            text.color = color;
        }

        private static void DestroyMaterialInstance(ref Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);

            material = null;
        }

        private static Image EnsureImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void CreateRule(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image image = EnsureImage(rect.gameObject);
            image.color = Rule;
            image.raycastTarget = false;
        }

        private static void CreateCornerBracket(RectTransform parent, bool left, bool top)
        {
            string cornerName = left
                ? (top ? "Corner_LT" : "Corner_LB")
                : (top ? "Corner_RT" : "Corner_RB");
            RectTransform root = CreateRect(parent, cornerName);
            root.anchorMin = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
            root.anchorMax = root.anchorMin;
            root.pivot = root.anchorMin;
            root.anchoredPosition = new Vector2(left ? 8f : -8f, top ? -8f : 8f);
            root.sizeDelta = new Vector2(28f, 28f);

            Image horiz = EnsureImage(CreateRect(root, "Horiz").gameObject);
            horiz.rectTransform.anchorMin = new Vector2(0f, top ? 1f : 0f);
            horiz.rectTransform.anchorMax = new Vector2(1f, top ? 1f : 0f);
            horiz.rectTransform.pivot = new Vector2(0.5f, top ? 1f : 0f);
            horiz.rectTransform.anchoredPosition = Vector2.zero;
            horiz.rectTransform.sizeDelta = new Vector2(0f, 2f);
            horiz.color = Rule;
            horiz.raycastTarget = false;

            Image vert = EnsureImage(CreateRect(root, "Vert").gameObject);
            vert.rectTransform.anchorMin = new Vector2(left ? 0f : 1f, 0f);
            vert.rectTransform.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            vert.rectTransform.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            vert.rectTransform.anchoredPosition = Vector2.zero;
            vert.rectTransform.sizeDelta = new Vector2(2f, 0f);
            vert.color = Rule;
            vert.raycastTarget = false;
        }
    }
}
