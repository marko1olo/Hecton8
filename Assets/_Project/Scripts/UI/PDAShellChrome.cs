using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton.Localization;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Shell Chrome")]
    public sealed class PDAShellChrome : MonoBehaviour, ILateFrameTickable, IPDAEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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
        private const string VaultPressureTag = "[VAULT FRAGMENTATION >80%]";
        private const string LeftFooterNumericTemplate = "CARGO {N0}/{N1}  |  MASS {N2:0.0} kg  |  READY TOOLS {N3}/{N4}";
        private const string RightFooterOnlineNumericTemplate = "O2 {N0:0}%  |  PWR {N1:0}%  |  PDA ONLINE";
        private const string RightFooterStandbyNumericTemplate = "O2 {N0:0}%  |  PWR {N1:0}%  |  PDA STANDBY";
        private const string IntrusionFooterNumericTemplate = "O2 {N0:0}%  |  PWR {N1:0}%  |  REBOOT {N2}%";
        private const int NumericTemplateWriteAttemptLimit = 8;
        private const int ChromeTextBufferCapacity = 512;
        private const int LegacyGlitchReadabilityPrefixChars = 5;
        private const int VaultPressureWarningStaleFrames = 300;
        private const byte DataVaultMemoryPressureFlag = 2;

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
        private static readonly int PdaShellTitleKeyHash = LocHash.Compute(LocalizationKeys.PDA_SHELL_TITLE);
        private static readonly int PdaTabInventoryKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_INVENTORY);
        private static readonly int PdaTabLoadoutKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_LOADOUT);
        private static readonly int PdaTabConstructionKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_CONSTRUCTION);
        private static readonly int PdaTabBarterKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_BARTER);
        private static readonly int PdaTabDataLogKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_DATA_LOG);
        private static readonly int PdaTabSpectrumKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_SPECTRUM);
        private static readonly int PdaTabDiagnosticsKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_DIAGNOSTICS);
        private static readonly int PdaTabUnknownKeyHash = LocHash.Compute(LocalizationKeys.PDA_TAB_UNKNOWN);
        private static readonly int PdaMechModeActiveKeyHash = LocHash.Compute(LocalizationKeys.PDA_MECH_MODE_ACTIVE);
        private static readonly int PdaFooterLeftKeyHash = LocHash.Compute(LocalizationKeys.PDA_FOOTER_LEFT);
        private static readonly int PdaFooterRightOnlineKeyHash = LocHash.Compute(LocalizationKeys.PDA_FOOTER_RIGHT_ONLINE);
        private static readonly int PdaFooterRightStandbyKeyHash = LocHash.Compute(LocalizationKeys.PDA_FOOTER_RIGHT_STANDBY);
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
        private Image _dataLinkDegradedIcon;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _tabText;
        private TextMeshProUGUI _intrusionText;
        private TextMeshProUGUI _contextTagText;
        private TextMeshProUGUI _leftFooterText;
        private TextMeshProUGUI _rightFooterText;
        private Material _headerMaterial;
        private Material _footerMaterial;
        private Material _dataLinkIconMaterial;
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
        private uint _inventorySignalHash;
        private uint _lastInventorySignalRevision;
        private uint _toolLoadoutSignalSourceId;
        private uint _lastToolLoadoutSignalSequence;
        private bool _pdaEventsRegistered;
        private readonly char[] _localizedTitleBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabInventoryBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabLoadoutBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabConstructionBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabBarterBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabDataLogBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabSpectrumBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabDiagnosticsBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedTabUnknownBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedLeftFooterNumericTemplateBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedRightFooterOnlineNumericTemplateBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedRightFooterStandbyNumericTemplateBuffer = new char[ChromeTextBufferCapacity];
        private readonly char[] _localizedIntrusionFooterNumericTemplateBuffer = new char[ChromeTextBufferCapacity];
        private int _localizedTitleLength;
        private int _localizedTabInventoryLength;
        private int _localizedTabLoadoutLength;
        private int _localizedTabConstructionLength;
        private int _localizedTabBarterLength;
        private int _localizedTabDataLogLength;
        private int _localizedTabSpectrumLength;
        private int _localizedTabDiagnosticsLength;
        private int _localizedTabUnknownLength;
        private int _localizedLeftFooterNumericTemplateLength;
        private int _localizedRightFooterOnlineNumericTemplateLength;
        private int _localizedRightFooterStandbyNumericTemplateLength;
        private int _localizedIntrusionFooterNumericTemplateLength;
        private bool _registeredToTickManager;
        private int _lastStressCorruptionBucket = int.MinValue;
        private PDAIntrusionManager _intrusionManager;
        private HectonPlayerMovement _playerMovement;
        private bool _lastIntrusionActive;
        private bool _lastMechModeActive;
        private bool _lastDataLinkDegraded;
        private int _lastStorageDebtBucket = int.MinValue;
        private int _lastVaultPressureBucket = int.MinValue;
        private int _vaultPressureWarningFrame = int.MinValue;
        private int _lastRebootProgressPercent = -1;
        private bool _vaultPressureWarningActive;
        private int _cachedRebootBindingLength;
        private byte _cachedRebootBindingStyleCode = byte.MaxValue;
        private readonly char[] _localizedMechModeTagBuffer = new char[ChromeTextBufferCapacity];
        private int _localizedMechModeTagLength;
        // COLD ALLOC: char[64] - cached PDA intrusion hint prefix - owner: PDAShellChrome
        private readonly char[] _localizedIntrusionHintPrefixBuffer = new char[64];
        // COLD ALLOC: char[48] - cached PDA intrusion hint suffix - owner: PDAShellChrome
        private readonly char[] _localizedIntrusionHintSuffixBuffer = new char[48];
        private int _localizedIntrusionHintPrefixLength;
        private int _localizedIntrusionHintSuffixLength;
        // COLD ALLOC: char[160] - PDA title staging buffer - owner: PDAShellChrome
        private char[] _titleBuffer = new char[ChromeTextBufferCapacity];
        // COLD ALLOC: char[128] - PDA tab staging buffer - owner: PDAShellChrome
        private char[] _tabBuffer = new char[ChromeTextBufferCapacity];
        // COLD ALLOC: char[160] - PDA left footer staging buffer - owner: PDAShellChrome
        private char[] _leftFooterBuffer = new char[ChromeTextBufferCapacity];
        // COLD ALLOC: char[128] - PDA right footer staging buffer - owner: PDAShellChrome
        private char[] _rightFooterBuffer = new char[ChromeTextBufferCapacity];
        // COLD ALLOC: char[160] - PDA caller-owned glitch scratch buffer - owner: PDAShellChrome
        private char[] _glitchScratchBuffer = new char[ChromeTextBufferCapacity];
        // COLD ALLOC: char[64] - PDA context tag staging buffer - owner: PDAShellChrome
        private char[] _contextTagBuffer = new char[ChromeTextBufferCapacity];
        // COLD ALLOC: char[96] - intrusion status hint buffer - owner: PDAShellChrome
        private readonly char[] _intrusionHintBuffer = new char[96];
        // COLD ALLOC: char[48] - cached PDA reboot binding label - owner: PDAShellChrome
        private readonly char[] _rebootBindingBuffer = new char[48];
        private int _appliedTitleVersion = int.MinValue;
        private int _appliedTabVersion = int.MinValue;
        private int _appliedIntrusionVersion = int.MinValue;
        private int _appliedContextTagVersion = int.MinValue;
        private int _appliedLeftFooterVersion = int.MinValue;
        private int _appliedRightFooterVersion = int.MinValue;
        private IDataVault _glitchVault;
        private ILocalizationStressPresentationReadModel _localization;
        private INativeInputManagerRuntime _nativeInputManager;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private VaultGenerationHandle<byte> _glitchTableHandle;
        private bool _glitchTableHandleReady;
        private bool _hotSwapRegistered;
        private bool _localizedChromeDirty;

        private void Awake()
        {
            RefreshBindings();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            RefreshBindings();
            RefreshLocalizedTextCache();
            EnsureBuilt();
            CacheGlitchTableVaultCold();
            TryRegisterHotSwapListener();
            Subscribe();
            RefreshChrome();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwapListener();
            Unsubscribe();
            UnregisterFromTickManager();
            ClearGlitchTableBinding();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnregisterFromTickManager();
            PDAEvents.AssertUnregistered(this, nameof(PDAShellChrome));
            DestroyMaterialInstance(ref _headerMaterial);
            DestroyMaterialInstance(ref _footerMaterial);
            DestroyMaterialInstance(ref _dataLinkIconMaterial);
            DestroyMaterialInstance(ref _titleMaterial);
            DestroyMaterialInstance(ref _tabMaterial);
            DestroyMaterialInstance(ref _intrusionMaterial);
            DestroyMaterialInstance(ref _contextTagMaterial);
            DestroyMaterialInstance(ref _leftFooterMaterial);
            DestroyMaterialInstance(ref _rightFooterMaterial);
            ClearGlitchTableBinding();
        }

        private void AutoResolve()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerPDA == null && playerContext != null)
                playerPDA = playerContext.PlayerPDA;
            if (playerInventory == null && playerContext != null)
                playerInventory = playerContext.Inventory;
            if (toolManager == null && playerContext != null)
                toolManager = playerContext.ToolManager;
            if (_playerMovement == null && playerContext != null)
                _playerMovement = playerContext.PlayerMovement;

            if ((!playerPDA || !playerInventory || !toolManager || !survivalSystem) &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
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
            {
                if (!TryGetComponent(out playerPDA))
                {
                    for (Transform current = transform.parent; current != null; current = current.parent)
                    {
                        if (current.TryGetComponent(out playerPDA))
                            break;
                    }
                }
            }

            if (_intrusionManager == null && playerPDA != null)
                playerPDA.TryGetComponent(out _intrusionManager);

            if (_intrusionManager == null)
            {
                if (!TryGetComponent(out _intrusionManager))
                {
                    for (Transform current = transform.parent; current != null; current = current.parent)
                    {
                        if (current.TryGetComponent(out _intrusionManager))
                            break;
                    }
                }
            }

            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
        }

        private void RefreshBindings()
        {
            HectonSurvivalSystem previousSurvivalSystem = survivalSystem;

            AutoResolve();
            RefreshInventorySignalBinding();
            RefreshToolLoadoutSignalBinding();

            if (!ReferenceEquals(previousSurvivalSystem, survivalSystem))
            {
                _lastOxygenPercent = int.MinValue;
                _lastEnergyPercent = int.MinValue;
                _appliedRightFooterVersion = int.MinValue;
            }
        }

        private void Subscribe()
        {
            _pdaEventsRegistered = PDAEvents.TryRegister(this);
            LocalizationEvents.RegisterLanguageListener(this);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
                BindGlitchTableVault(nextVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _localization = currentService as ILocalizationStressPresentationReadModel;
                QueueLocalizedChromeRefresh();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.Input)
            {
                if (serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                    _nativeInputManager = currentService as INativeInputManagerRuntime;

                _cachedRebootBindingLength = 0;
                _cachedRebootBindingStyleCode = byte.MaxValue;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                playerPDA = null;
                playerInventory = null;
                toolManager = null;
                _playerMovement = null;
                _intrusionManager = null;
                RefreshBindings();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterFromTickManager();
                if (currentService != null && isActiveAndEnabled)
                    RegisterToTickManager();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void CacheGlitchTableVaultCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            BindGlitchTableVault(vault);
        }

        private void BindGlitchTableVault(IDataVault vault)
        {
            if (!ReferenceEquals(_glitchVault, vault))
                ClearGlitchTableBinding();

            _glitchVault = vault;
            _glitchTableHandleReady = false;
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle(
                    (BufferID)DiegeticGlitchSurgeonRuntime.GlitchTableBufferIdRaw,
                    out VaultGenerationHandle<byte> acquired) ||
                !IsGlitchTableHandle(in acquired) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in acquired, out NativeArray<byte>.ReadOnly glitchTable) ||
                vault.IsCompactionFenceActive ||
                glitchTable.Length < DiegeticGlitchSurgeonRuntime.GlitchTableCapacity)
            {
                return;
            }

            _glitchTableHandle = acquired;
            _glitchTableHandleReady = true;

            unsafe
            {
                byte* table = (byte*)glitchTable.GetUnsafeReadOnlyPtr();
                if (table == null || !GlitchTable.IsValidGlyphTable(table, DiegeticGlitchSurgeonRuntime.GlitchTableCapacity))
                    ClearGlitchTableBinding();
            }
        }

        private unsafe bool TryResolveGlitchTablePointer(out byte* table, out int tableLength)
        {
            table = null;
            tableLength = 0;
            if (!_glitchTableHandleReady ||
                _glitchVault == null ||
                _glitchVault.IsCompactionFenceActive ||
                !IsGlitchTableHandle(in _glitchTableHandle))
                return false;

            if (!_glitchVault.TryReadOnlyHandle(in _glitchTableHandle, out NativeArray<byte>.ReadOnly glitchTable) ||
                _glitchVault.IsCompactionFenceActive ||
                glitchTable.Length < DiegeticGlitchSurgeonRuntime.GlitchTableCapacity)
            {
                return false;
            }

            table = (byte*)glitchTable.GetUnsafeReadOnlyPtr();
            if (table == null)
                return false;

            tableLength = DiegeticGlitchSurgeonRuntime.GlitchTableCapacity;
            return true;
        }

        private static bool IsGlitchTableHandle<T>(in VaultGenerationHandle<T> handle) where T : unmanaged
        {
            return handle.BufferID == DiegeticGlitchSurgeonRuntime.GlitchTableBufferIdRaw &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        private void ClearGlitchTableBinding()
        {
            _glitchVault = null;
            _glitchTableHandle = default;
            _glitchTableHandleReady = false;
        }

        private void CacheRegistryServicesCold()
        {
            _localization = GlobalRegistry.LocalizationStressPresentation;
            _nativeInputManager = GlobalRegistry.NativeInputRuntime;
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        private void Unsubscribe()
        {
            if (_pdaEventsRegistered)
            {
                PDAEvents.Unregister(this);
                _pdaEventsRegistered = false;
            }
            LocalizationEvents.UnregisterLanguageListener(this);
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
        }
        private void HandlePdaClosed(float _)
        {
            RefreshChrome();
        }

        private void HandleTabChanged(int _, int __)
        {
            RefreshChrome();
        }

        public void LateFrameTick()
        {
            if (_localizedChromeDirty)
            {
                _localizedChromeDirty = false;
                RefreshLocalizedTextCache();
                RefreshChrome();
            }

            if (!PlayerPDA.IsOpen)
            {
                _lastStressCorruptionBucket = int.MinValue;
                _lastIntrusionActive = false;
                _lastStorageDebtBucket = int.MinValue;
                _lastVaultPressureBucket = int.MinValue;
                _vaultPressureWarningActive = false;
                _lastRebootProgressPercent = -1;
                return;
            }

            ILocalizationStressPresentationReadModel manager = _localization;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            bool intrusionActive = _intrusionManager != null && _intrusionManager.IsHacked;
            bool mechModeActive = _playerMovement != null && _playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            float storageDebt01 = SystemDispatcher.StreamingStorageDebt01;
            bool dataLinkDegraded = _lastDataLinkDegraded ? storageDebt01 > 0.45f : storageDebt01 > 0.6f;
            int storageDebtBucket = (int)math.round(storageDebt01 * 20f);
            bool vaultPressureDirty = ObserveVaultMemoryPressure();
            int rebootProgressPercent = intrusionActive
                ? (int)math.round(_intrusionManager.RebootProgressNormalized * 100f)
                : 0;
            bool inventoryDirty = ConsumeInventoryChangedSignals();
            bool toolDirty = ConsumeToolLoadoutChangedSignals();
            ResolveSurvivalPercentBuckets(out int oxygenPercent, out int energyPercent);
            bool vitalsDirty = oxygenPercent != _lastOxygenPercent ||
                energyPercent != _lastEnergyPercent;

            bool reactiveDirty = stressBucket != _lastStressCorruptionBucket ||
                intrusionActive != _lastIntrusionActive ||
                mechModeActive != _lastMechModeActive ||
                dataLinkDegraded != _lastDataLinkDegraded ||
                storageDebtBucket != _lastStorageDebtBucket ||
                vaultPressureDirty ||
                rebootProgressPercent != _lastRebootProgressPercent;

            if (!inventoryDirty && !toolDirty && !vitalsDirty && !reactiveDirty)
                return;

            if (!reactiveDirty)
            {
                RefreshChrome();
                return;
            }

            _lastStressCorruptionBucket = stressBucket;
            _lastIntrusionActive = intrusionActive;
            _lastMechModeActive = mechModeActive;
            _lastDataLinkDegraded = dataLinkDegraded;
            _lastStorageDebtBucket = storageDebtBucket;
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

        private bool ObserveVaultMemoryPressure()
        {
            bool previousActive = _vaultPressureWarningActive;
            int previousBucket = _lastVaultPressureBucket;
            ReadOnlySpan<MemoryPressureSignal> signals = SignalBus<MemoryPressureSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                MemoryPressureSignal signal = signals[i];
                if ((signal.Flags & DataVaultMemoryPressureFlag) == 0 || signal.UsageRatio < 0.8f)
                    continue;

                _vaultPressureWarningActive = true;
                _vaultPressureWarningFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                _lastVaultPressureBucket = (int)math.round(math.saturate(signal.UsageRatio) * 20f);
            }

            if (_vaultPressureWarningActive &&
                Hecton8.Core.SystemDispatcher.CurrentFrameIndex - _vaultPressureWarningFrame > VaultPressureWarningStaleFrames)
            {
                _vaultPressureWarningActive = false;
                _lastVaultPressureBucket = 0;
            }

            return previousActive != _vaultPressureWarningActive ||
                previousBucket != _lastVaultPressureBucket;
        }

        private bool ConsumeInventoryChangedSignals()
        {
            uint inventoryHash = _inventorySignalHash;
            if (inventoryHash == 0u)
                return false;

            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly InventoryChangedSignal signal = ref signals[i];
                if (signal.InventoryHash != inventoryHash)
                    continue;

                if (signal.Revision == _lastInventorySignalRevision && _lastInventorySignalRevision != 0u)
                    continue;

                _lastInventorySignalRevision = signal.Revision;
                return true;
            }

            return false;
        }

        private void RefreshInventorySignalBinding()
        {
            uint resolvedHash = ResolveInventorySignalHash(playerInventory);
            if (_inventorySignalHash == resolvedHash)
                return;

            _inventorySignalHash = resolvedHash;
            _lastInventorySignalRevision = 0u;
        }

        private static uint ResolveInventorySignalHash(PlayerInventory inventory)
        {
            return inventory != null && inventory.gameObject != null
                ? unchecked((uint)EntityId.ToULong(inventory.gameObject.GetEntityId()))
                : 0u;
        }

        private bool ConsumeToolLoadoutChangedSignals()
        {
            uint sourceId = _toolLoadoutSignalSourceId;
            if (sourceId == 0u)
                return false;

            ReadOnlySpan<ToolLoadoutChangedSignal> signals = SignalBus<ToolLoadoutChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ToolLoadoutChangedSignal signal = ref signals[i];
                if (signal.SourceId != sourceId)
                    continue;

                if (signal.Sequence == _lastToolLoadoutSignalSequence && _lastToolLoadoutSignalSequence != 0u)
                    continue;

                _lastToolLoadoutSignalSequence = signal.Sequence;
                return true;
            }

            return false;
        }

        private void RefreshToolLoadoutSignalBinding()
        {
            uint resolvedSourceId = ResolveToolLoadoutSignalSourceId(toolManager);
            if (_toolLoadoutSignalSourceId == resolvedSourceId)
                return;

            _toolLoadoutSignalSourceId = resolvedSourceId;
            _lastToolLoadoutSignalSequence = 0u;
        }

        private static uint ResolveToolLoadoutSignalSourceId(PlayerToolManager manager)
        {
            return manager != null && manager.gameObject != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(manager.gameObject.GetEntityId()))
                : 0u;
        }

        private void ResolveSurvivalPercentBuckets(out int oxygenPercent, out int energyPercent)
        {
            if (survivalSystem == null)
            {
                oxygenPercent = 0;
                energyPercent = 0;
                return;
            }

            oxygenPercent = (int)math.round(survivalSystem.OxygenNormalized * 100f);
            energyPercent = (int)math.round(survivalSystem.EnergyNormalized * 100f);
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

            RectTransform dataLinkIcon = CreateRect(footer, "DataLinkDegradedIcon");
            Anchor(dataLinkIcon, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-116f, -4f), new Vector2(-108f, 4f));
            _dataLinkDegradedIcon = EnsureImage(dataLinkIcon.gameObject);
            _dataLinkDegradedIcon.color = Color.clear;
            _dataLinkDegradedIcon.raycastTarget = false;

            EnsureMaterialInstances();
            ApplyChromeMaterialPalette(Stable, Primary, Dim, AlertText, Dim, DimLow, MechModeText);

            _built = true;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            QueueLocalizedChromeRefresh();
        }

        private void QueueLocalizedChromeRefresh()
        {
            _localizedChromeDirty = true;
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
            _cachedRebootBindingLength = 0;
            _cachedRebootBindingStyleCode = byte.MaxValue;
            InvalidateAppliedLabelVersions();
        }

        private void RefreshChrome()
        {
            if (!_built)
                return;

            ILocalizationStressPresentationReadModel manager = _localization;
            bool rtl = manager != null &&
                       LocalizedMeasurementFormatter.IsRightToLeft((GameLanguage)manager.ActiveLanguageId);
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            bool useStressReactiveStrings = stressBucket > 0;
            float stressReactiveIntensity = useStressReactiveStrings && manager != null
                ? manager.GetHullStressCorruptionIntensity()
                : 0f;

            if (_titleText != null)
            {
                ReadOnlySpan<char> titleSpan = _localizedTitleBuffer.AsSpan(0, _localizedTitleLength);
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

            ReadOnlySpan<char> tabName = GetActiveTabLabelSpan();
            int cargoCells = playerInventory != null && playerInventory.Grid != null
                ? CountUsedCells(playerInventory.Grid)
                : 0;
            int cargoTotal = playerInventory != null && playerInventory.Grid != null
                ? playerInventory.Grid.Columns * playerInventory.Grid.Rows
                : 48;
            float weight = playerInventory != null ? playerInventory.TotalWeight : 0f;
            int readyTools = CountReadyTools();
            int assignedTools = toolManager != null ? CountAssignedTools() : 0;
            int activeTabIndex = playerPDA != null ? playerPDA.ActiveTab : -1;
            int weightDeci = (int)math.round(weight * 10f);
            ResolveSurvivalPercentBuckets(out int oxygenPercent, out int energyPercent);
            float oxygen = math.saturate(oxygenPercent * 0.01f);
            float energy = math.saturate(energyPercent * 0.01f);
            bool pdaOpen = PlayerPDA.IsOpen;
            bool intrusionActive = _intrusionManager != null && _intrusionManager.IsHacked;
            bool mechModeActive = _playerMovement != null && _playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            float storageDebt01 = SystemDispatcher.StreamingStorageDebt01;
            bool dataLinkDegraded = _lastDataLinkDegraded ? storageDebt01 > 0.45f : storageDebt01 > 0.6f;
            int rebootProgressPercent = intrusionActive
                ? (int)math.round(_intrusionManager.RebootProgressNormalized * 100f)
                : 0;

            if (_tabText != null &&
                (_lastActiveTab != activeTabIndex || _lastIntrusionActive != intrusionActive || _appliedTabVersion == int.MinValue))
            {
                ReadOnlySpan<char> tabSpan = intrusionActive ? IntrusionTabOverride.AsSpan() : tabName;
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
                int safeAssignedTools = math.max(assignedTools, 1);
                TryWriteNumericTemplate(
                    _localizedLeftFooterNumericTemplateBuffer.AsSpan(0, _localizedLeftFooterNumericTemplateLength),
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
                        _localizedIntrusionFooterNumericTemplateBuffer.AsSpan(0, _localizedIntrusionFooterNumericTemplateLength),
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
                        _localizedRightFooterOnlineNumericTemplateBuffer.AsSpan(0, _localizedRightFooterOnlineNumericTemplateLength),
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
                        _localizedRightFooterStandbyNumericTemplateBuffer.AsSpan(0, _localizedRightFooterStandbyNumericTemplateLength),
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
                    ReadOnlySpan<char> rebootBinding = ResolveRebootBinding();
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
                if (_vaultPressureWarningActive)
                {
                    ReadOnlySpan<char> contextSpan = VaultPressureTag.AsSpan();
                    CopyTextToBuffer(contextSpan, ref _contextTagBuffer, out int contextLength);
                    ApplyTextBuffer(
                        _contextTagText,
                        _contextTagBuffer,
                        contextLength,
                        rtl,
                        useStressReactiveStrings,
                        stressReactiveIntensity,
                        137,
                        ComputeTextVersion(contextSpan, 137, stressBucket),
                        ref _appliedContextTagVersion);
                    _contextTagText.alpha = 1f;
                }
                else if (mechModeActive)
                {
                    ReadOnlySpan<char> contextSpan = _localizedMechModeTagBuffer.AsSpan(0, _localizedMechModeTagLength);
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
                severity = LerpColor(severity, MechModeTint, 0.42f);
            Color titleColor = mechModeActive ? MechModeText : Primary;
            Color tabColor = intrusionActive || energy < 0.25f || oxygen < 0.3f ? AlertText : (mechModeActive ? MechModeText : Dim);
            Color leftFooterColor = mechModeActive ? MechModeText : Dim;
            Color rightFooterColor = intrusionActive || energy < 0.25f || oxygen < 0.3f ? AlertText : (mechModeActive ? MechModeText : DimLow);
            ApplyChromeMaterialPalette(severity, titleColor, tabColor, AlertText, leftFooterColor, rightFooterColor, MechModeText);
            ApplyDataLinkDegradedIcon(dataLinkDegraded, storageDebt01);
            if (_chromeCanvasGroup != null)
                _chromeCanvasGroup.alpha = pdaOpen ? 1f : 0f;
        }

        private ReadOnlySpan<char> GetActiveTabLabelSpan()
        {
            if (playerPDA == null)
                return _localizedTabUnknownBuffer.AsSpan(0, _localizedTabUnknownLength);

            switch (playerPDA.ActiveTab)
            {
                case 0: return _localizedTabInventoryBuffer.AsSpan(0, _localizedTabInventoryLength);
                case 1: return _localizedTabLoadoutBuffer.AsSpan(0, _localizedTabLoadoutLength);
                case 2: return _localizedTabConstructionBuffer.AsSpan(0, _localizedTabConstructionLength);
                case 3: return _localizedTabBarterBuffer.AsSpan(0, _localizedTabBarterLength);
                case 4: return _localizedTabDataLogBuffer.AsSpan(0, _localizedTabDataLogLength);
                case 5: return _localizedTabSpectrumBuffer.AsSpan(0, _localizedTabSpectrumLength);
                case 7: return _localizedTabDiagnosticsBuffer.AsSpan(0, _localizedTabDiagnosticsLength);
                default: return _localizedTabUnknownBuffer.AsSpan(0, _localizedTabUnknownLength);
            }
        }

        private void RefreshLocalizedTextCache()
        {
            _localizedTitleLength = CopyLocalizedSpan(PdaShellTitleKeyHash, TitleTextValue.AsSpan(), _localizedTitleBuffer);
            _localizedTabInventoryLength = CopyLocalizedSpan(PdaTabInventoryKeyHash, ActiveTabInventory.AsSpan(), _localizedTabInventoryBuffer);
            _localizedTabLoadoutLength = CopyLocalizedSpan(PdaTabLoadoutKeyHash, ActiveTabLoadout.AsSpan(), _localizedTabLoadoutBuffer);
            _localizedTabConstructionLength = CopyLocalizedSpan(PdaTabConstructionKeyHash, ActiveTabConstruction.AsSpan(), _localizedTabConstructionBuffer);
            _localizedTabBarterLength = CopyLocalizedSpan(PdaTabBarterKeyHash, ActiveTabBarter.AsSpan(), _localizedTabBarterBuffer);
            _localizedTabDataLogLength = CopyLocalizedSpan(PdaTabDataLogKeyHash, ActiveTabDataLog.AsSpan(), _localizedTabDataLogBuffer);
            _localizedTabSpectrumLength = CopyLocalizedSpan(PdaTabSpectrumKeyHash, ActiveTabSpectrum.AsSpan(), _localizedTabSpectrumBuffer);
            _localizedTabDiagnosticsLength = CopyLocalizedSpan(PdaTabDiagnosticsKeyHash, ActiveTabDiagnostics.AsSpan(), _localizedTabDiagnosticsBuffer);
            _localizedTabUnknownLength = CopyLocalizedSpan(PdaTabUnknownKeyHash, ActiveTabUnknown.AsSpan(), _localizedTabUnknownBuffer);
            _localizedMechModeTagLength = CopyLocalizedSpan(PdaMechModeActiveKeyHash, MechModeTag.AsSpan(), _localizedMechModeTagBuffer);
            ReadOnlySpan<char> leftFooterFormat = ResolveLocalizedSpan(PdaFooterLeftKeyHash, LeftFooterFormat.AsSpan());
            ReadOnlySpan<char> rightFooterOnlineFormat = ResolveLocalizedSpan(PdaFooterRightOnlineKeyHash, RightFooterOnlineFormat.AsSpan());
            ReadOnlySpan<char> rightFooterStandbyFormat = ResolveLocalizedSpan(PdaFooterRightStandbyKeyHash, RightFooterStandbyFormat.AsSpan());
            _localizedLeftFooterNumericTemplateLength = CopyNumericTemplate(leftFooterFormat, LeftFooterNumericTemplate.AsSpan(), _localizedLeftFooterNumericTemplateBuffer);
            _localizedRightFooterOnlineNumericTemplateLength = CopyNumericTemplate(rightFooterOnlineFormat, RightFooterOnlineNumericTemplate.AsSpan(), _localizedRightFooterOnlineNumericTemplateBuffer);
            _localizedRightFooterStandbyNumericTemplateLength = CopyNumericTemplate(rightFooterStandbyFormat, RightFooterStandbyNumericTemplate.AsSpan(), _localizedRightFooterStandbyNumericTemplateBuffer);
            _localizedIntrusionFooterNumericTemplateLength = CopySpanToFixedBuffer(IntrusionFooterNumericTemplate.AsSpan(), _localizedIntrusionFooterNumericTemplateBuffer);
            CacheSinglePlaceholderTemplate(IntrusionHintFormat.AsSpan());
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback)
        {
            ILocalizationStressPresentationReadModel manager = _localization;
            if (manager == null || keyHash == 0)
                return fallback;

            return manager.GetRawSpanOrFallback(keyHash, fallback);
        }

        private int CopyLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback, char[] destination)
        {
            return CopySpanToFixedBuffer(ResolveLocalizedSpan(keyHash, fallback), destination);
        }

        private static int CopyNumericTemplate(ReadOnlySpan<char> template, ReadOnlySpan<char> fallback, char[] destination)
        {
            ReadOnlySpan<char> source = template.IsEmpty ? fallback : template;
            if (source.IsEmpty)
                source = fallback;

            for (int i = 0; i < source.Length - 1; i++)
            {
                if (source[i] == '{' && source[i + 1] >= '0' && source[i + 1] <= '9')
                {
                    source = fallback;
                    break;
                }
            }

            return CopySpanToFixedBuffer(source, destination);
        }

        private void CacheSinglePlaceholderTemplate(ReadOnlySpan<char> template)
        {
            ReadOnlySpan<char> source = template.IsEmpty ? IntrusionHintFormat.AsSpan() : template;
            int placeholderIndex = IndexOfPlaceholderStart(source);
            if (placeholderIndex < 0)
            {
                CopySpanToFixedBuffer(source, _localizedIntrusionHintPrefixBuffer, out _localizedIntrusionHintPrefixLength);
                _localizedIntrusionHintSuffixLength = 0;
                return;
            }

            int closeIndex = IndexOfClosingBrace(source, placeholderIndex);
            if (closeIndex < 0)
            {
                CopySpanToFixedBuffer(source, _localizedIntrusionHintPrefixBuffer, out _localizedIntrusionHintPrefixLength);
                _localizedIntrusionHintSuffixLength = 0;
                return;
            }

            CopySpanToFixedBuffer(source.Slice(0, placeholderIndex), _localizedIntrusionHintPrefixBuffer, out _localizedIntrusionHintPrefixLength);
            ReadOnlySpan<char> suffix = closeIndex + 1 < source.Length
                ? source.Slice(closeIndex + 1)
                : ReadOnlySpan<char>.Empty;
            CopySpanToFixedBuffer(suffix, _localizedIntrusionHintSuffixBuffer, out _localizedIntrusionHintSuffixLength);
        }

        private static int IndexOfPlaceholderStart(ReadOnlySpan<char> source)
        {
            for (int i = 0; i < source.Length - 1; i++)
            {
                if (source[i] == '{' && source[i + 1] == '0')
                    return i;
            }

            return -1;
        }

        private static int IndexOfClosingBrace(ReadOnlySpan<char> source, int startIndex)
        {
            for (int i = math.max(0, startIndex); i < source.Length; i++)
            {
                if (source[i] == '}')
                    return i;
            }

            return -1;
        }

        private static void CopySpanToFixedBuffer(ReadOnlySpan<char> source, char[] destination, out int length)
        {
            if (destination == null || destination.Length == 0 || source.IsEmpty)
            {
                length = 0;
                return;
            }

            length = math.min(source.Length, destination.Length);
            source.Slice(0, length).CopyTo(destination.AsSpan(0, length));
        }

        private static int CopySpanToFixedBuffer(ReadOnlySpan<char> source, char[] destination)
        {
            CopySpanToFixedBuffer(source, destination, out int length);
            return length;
        }

        private void SetIntrusionHintText(
            TMP_Text label,
            ReadOnlySpan<char> binding,
            bool rtl,
            bool useStressReactiveStrings,
            float stressReactiveIntensity,
            int stressBucket)
        {
            if (label == null)
                return;

            if (_localizedIntrusionHintPrefixLength == 0 && _localizedIntrusionHintSuffixLength == 0)
                CacheSinglePlaceholderTemplate(IntrusionHintFormat.AsSpan());

            ReadOnlySpan<char> resolvedBinding = binding.IsEmpty ? "SUBMIT".AsSpan() : binding;
            ReadOnlySpan<char> prefix = _localizedIntrusionHintPrefixBuffer.AsSpan(0, _localizedIntrusionHintPrefixLength);
            ReadOnlySpan<char> suffix = _localizedIntrusionHintSuffixBuffer.AsSpan(0, _localizedIntrusionHintSuffixLength);
            int index = 0;
            index = CopySpanToBuffer(_intrusionHintBuffer, index, prefix);
            index = CopySpanToBuffer(_intrusionHintBuffer, index, resolvedBinding);
            index = CopySpanToBuffer(_intrusionHintBuffer, index, suffix);

            int version = unchecked((((ComputeTextVersion(prefix, 239, stressBucket) * 397) ^ ComputeTextVersion(suffix, 241, stressBucket)) * 397) ^ ComputeTextVersion(resolvedBinding, 243, stressBucket));
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

            int safeLength = math.clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private static int CopySpanToBuffer(char[] buffer, int startIndex, ReadOnlySpan<char> value)
        {
            if (buffer == null || value.IsEmpty || startIndex >= buffer.Length)
                return startIndex;

            int copyLength = math.min(value.Length, buffer.Length - startIndex);
            value.Slice(0, copyLength).CopyTo(buffer.AsSpan(startIndex, copyLength));
            return startIndex + copyLength;
        }

        private static void CopyTextToBuffer(ReadOnlySpan<char> source, ref char[] buffer, out int length)
        {
            EnsureCharCapacity(ref buffer, source.Length);
            if (buffer == null || buffer.Length == 0 || source.IsEmpty)
            {
                length = 0;
                return;
            }

            length = math.min(source.Length, buffer.Length);
            source.Slice(0, length).CopyTo(buffer);
        }

        private static void EnsureCharCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer != null && buffer.Length >= requiredLength)
                return;
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

        private void ApplyTextBuffer(
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
                if (sourceLength > _glitchScratchBuffer.Length)
                {
                    ApplyDynamicBuffer(label, sourceBuffer, sourceLength);
                }
                else
                {
                    unsafe
                    {
                        if (TryResolveGlitchTablePointer(out byte* table, out int tableLength))
                        {
                            GlitchEncoder.ApplyDecayToBuffer(
                                sourceBuffer,
                                sourceLength,
                                _glitchScratchBuffer,
                                stressReactiveIntensity,
                                unchecked((version * 397) ^ corruptionSalt),
                                table,
                                tableLength,
                                LegacyGlitchReadabilityPrefixChars,
                                out int corruptedLength);
                            ApplyDynamicBuffer(label, _glitchScratchBuffer, corruptedLength);
                        }
                        else
                        {
                            GlitchEncoder.ApplyDecayToBuffer(
                                sourceBuffer,
                                sourceLength,
                                _glitchScratchBuffer,
                                stressReactiveIntensity,
                                unchecked((version * 397) ^ corruptionSalt),
                                out int corruptedLength);
                            ApplyDynamicBuffer(label, _glitchScratchBuffer, corruptedLength);
                        }
                    }
                }
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
            if (buffer == null || buffer.Length == 0)
            {
                length = 0;
                return;
            }

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
            if (buffer == null || buffer.Length == 0)
            {
                length = 0;
                return;
            }

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
            if (buffer == null || buffer.Length == 0)
            {
                length = 0;
                return;
            }

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
            return false;
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

        private ReadOnlySpan<char> ResolveRebootBinding()
        {
            INativeInputManagerRuntime inputManager = _nativeInputManager;
            byte displayStyleCode = inputManager != null
                ? inputManager.CurrentDisplayStyleCode
                : NativeInputDisplayStyle.KeyboardMouse;

            if (_cachedRebootBindingLength > 0 && _cachedRebootBindingStyleCode == displayStyleCode)
                return _rebootBindingBuffer.AsSpan(0, _cachedRebootBindingLength);

            _cachedRebootBindingLength = 0;
            _cachedRebootBindingStyleCode = displayStyleCode;
            if (inputManager != null &&
                inputManager.TryWriteBindingDisplayString(
                    "Submit",
                    "UI",
                    -1,
                    _rebootBindingBuffer,
                    0,
                    out int bindingLength) &&
                bindingLength > 0)
            {
                _cachedRebootBindingLength = bindingLength;
                return _rebootBindingBuffer.AsSpan(0, _cachedRebootBindingLength);
            }

            ReadOnlySpan<char> fallback = "SUBMIT".AsSpan();
            fallback.CopyTo(_rebootBindingBuffer);
            _cachedRebootBindingLength = fallback.Length;

            return _rebootBindingBuffer.AsSpan(0, _cachedRebootBindingLength);
        }

        private void EvaluateTickRegistration()
        {
            if (PlayerPDA.IsOpen)
                RegisterToTickManager();
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            _registeredToTickManager = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);

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

        private static Color LerpColor(Color from, Color to, float t)
        {
            float clampedT = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, clampedT),
                math.lerp(from.g, to.g, clampedT),
                math.lerp(from.b, to.b, clampedT),
                math.lerp(from.a, to.a, clampedT));
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

            return target.TryGetComponent(out CanvasGroup canvasGroup)
                ? canvasGroup
                : target.gameObject.AddComponent<CanvasGroup>();
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
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out RectTransform rect);
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
            EnsureGraphicMaterialInstance(_dataLinkDegradedIcon, ref _dataLinkIconMaterial);
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

        private void ApplyDataLinkDegradedIcon(bool degraded, float debt01)
        {
            if (_dataLinkDegradedIcon == null)
                return;

            Color iconColor = degraded
                ? LerpColor(DimLow, AlertText, math.saturate((debt01 - 0.6f) * 2.5f))
                : Color.clear;
            ApplyGraphicMaterialColor(_dataLinkDegradedIcon, _dataLinkIconMaterial, iconColor);
        }

        private static void EnsureGraphicMaterialInstance(Graphic graphic, ref Material material)
        {
            if (material != null)
                DestroyMaterialInstance(ref material);
        }

        private static void EnsureTextMaterialInstance(TextMeshProUGUI text, ref Material material)
        {
            if (material != null)
                DestroyMaterialInstance(ref material);
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
            if (!target.TryGetComponent(out Image image))
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
