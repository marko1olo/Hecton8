// ============================================================================
// HECTON-8 — PDALoadoutTab.cs
// Dedicated PDA loadout tab with 4 large slot cards, readiness state,
// durability, energy profile, and cargo linkage to the real tool backend.
// ============================================================================

using System;
using Hecton8.Gameplay;
using Hecton8.Bootstrap;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Tools;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Loadout Tab")]
    public sealed class PDALoadoutTab : MonoBehaviour, ILateFrameTickable, IPDAEventListener, IPlayerExpressionEventListener, IGlobalRegistryHotSwapListener
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.84f);
        private static readonly Color BoxBg = new Color(0.05f, 0.12f, 0.14f, 0.72f);
        private static readonly Color BoxActive = new Color(0.08f, 0.2f, 0.22f, 0.86f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.76f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.55f, 0.74f, 0.71f, 0.72f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color Ready = new Color(0.46f, 0.98f, 0.94f, 0.92f);
        private static readonly Color Missing = new Color(1f, 0.74f, 0.22f, 0.92f);
        private static readonly Color Broken = new Color(1f, 0.48f, 0.38f, 0.92f);
        private const int CachedLoadoutSlots = 4;
        private const float InvTwoPi = 0.15915494309f;

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private PlayerExpressionManager playerExpressionManager;
        [SerializeField] private HUDNotification hudNotification;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private int loadoutTabIndex = 1;
        [SerializeField] private bool holsterBeforeApplyingPreset = true;
        [SerializeField] private ToolLoadoutPreset[] loadoutPresets = new ToolLoadoutPreset[4];
        [SerializeField] private float fieldAdviceRange = 18f;
        [SerializeField] private LayerMask fieldAdviceMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        // ════════════════════════════════════════════════════════════
        //  CACHED FIELDS FOR ZERO-GC OPTIMIZATION
        // ════════════════════════════════════════════════════════════

        // COLD ALLOC: char[1024] — PDA loadout summary composition buffer — owner: PDALoadoutTab
        private readonly char[] _summaryCharBuffer = new char[1024];
        private readonly System.Collections.Generic.Dictionary<ulong, IPlayerToolDataReadModel> _prefabToolCache = new System.Collections.Generic.Dictionary<ulong, IPlayerToolDataReadModel>(32); // COLD ALLOC: Dictionary<ulong, IPlayerToolDataReadModel>(32) — caches prefab tool metadata routes for repeated loadout refreshes — owner: PDALoadoutTab

        private readonly uint[] _slotItemHashCache = new uint[CachedLoadoutSlots]; // COLD ALLOC: uint[4] - PDA slot item hash cache - owner: PDALoadoutTab
        private readonly uint[] _slotMetadataHashCache = new uint[CachedLoadoutSlots]; // COLD ALLOC: uint[4] - PDA slot metadata hash cache - owner: PDALoadoutTab
        private readonly ulong[] _slotHashPrefabCache = new ulong[CachedLoadoutSlots]; // COLD ALLOC: ulong[4] - PDA slot hash prefab identity cache - owner: PDALoadoutTab
        private readonly bool[] _slotHashResolved = new bool[CachedLoadoutSlots]; // COLD ALLOC: bool[4] - PDA slot hash cache validity flags - owner: PDALoadoutTab

        private bool _built;
        private bool _refreshDirty;
        private RectTransform[] _slotRoots;
        private Image[] _slotBgs;
        private Image[] _slotAccents;
        private Image[] _slotIcons;
        private Image[] _slotStatusBgs;
        private TextMeshProUGUI[] _slotTitles;
        private TextMeshProUGUI[] _slotStatuses;
        private TextMeshProUGUI[] _slotBodies;
        private RectTransform[] _slotActionRoots;
        private RectTransform[] _slotClearRoots;
        private CanvasGroup[] _slotActionCanvasGroups;
        private CanvasGroup[] _slotClearCanvasGroups;
        private Image[] _slotActionBgs;
        private Image[] _slotClearBgs;
        private TextMeshProUGUI[] _slotActionLabels;
        private TextMeshProUGUI[] _slotClearLabels;
        private RectTransform[] _presetRoots;
        private CanvasGroup[] _presetCanvasGroups;
        private Image[] _presetBgs;
        private TextMeshProUGUI[] _presetTitles;
        private TextMeshProUGUI[] _presetBodies;
        private TextMeshProUGUI _summaryText;
        private TextMeshProUGUI _hintText;
        private RectTransform _identityActionRoot;
        private CanvasGroup _identityActionCanvasGroup;
        private Image _identityActionBg;
        private TextMeshProUGUI _identityActionLabel;
        private RectTransform _recommendedActionRoot;
        private CanvasGroup _recommendedActionCanvasGroup;
        private Image _recommendedActionBg;
        private TextMeshProUGUI _recommendedActionLabel;
        private IPlayerInventoryService _inventoryService;
        private IPlayerExpressionReadModel _playerExpressionReadModel;
        private IToolDurabilityService _toolDurabilityService;
        private bool _registeredToLateFrameDispatcher;
        private bool _registeredHotSwap;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private uint _inventorySignalHash;
        private uint _lastInventorySignalRevision;
        private uint _toolLoadoutSignalSourceId;
        private uint _lastToolLoadoutSignalSequence;
        private bool _inventoryMassOverCapacity;
        private int _summaryMassCharStart = -1;
        private int _summaryMassCharLength;
        private float _massPulsePhase;
        private float _pendingSummaryMassPulseDelta;
        private bool _summaryMassPulseDirty;
        private bool _summaryMassVertexRefreshDeferred;
        private Color32 _appliedSummaryMassColor = new Color32(0, 0, 0, 0);
        private FixedCharBuffer _notificationBuffer = new FixedCharBuffer(192); // COLD ALLOC: char[192] - loadout HUD notification staging buffer - owner: PDALoadoutTab

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == loadoutTabIndex;

        private void Awake()
        {
            AutoResolveTabIndex();
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            CachePlayerInventoryService(GlobalRegistry.PlayerInventory);
            CachePlayerExpressionReadModel(GlobalRegistry.PlayerExpressionReadModel);
            CacheToolDurabilityService(GlobalRegistry.ToolDurabilityService);
            AutoResolve();
        }

        private void OnValidate()
        {
            #if UNITY_EDITOR
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            #endif

            AutoResolveTabIndex();
#if UNITY_EDITOR
            AutoResolvePresets();
#endif
        }

        private void OnEnable()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            CachePlayerInventoryService(GlobalRegistry.PlayerInventory);
            CachePlayerExpressionReadModel(GlobalRegistry.PlayerExpressionReadModel);
            CacheToolDurabilityService(GlobalRegistry.ToolDurabilityService);
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            TryRegisterHotSwapListener();
            RegisterToLateFrameManager();
            _refreshDirty = true;
            RefreshAll();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            Unsubscribe();
            UnregisterFromLateFrameManager();
            _playerRuntimeContext = null;
            _toolDurabilityService = null;
        }

        /// <inheritdoc />
        private void AdvanceLoadoutPresentationState(float deltaTime)
        {
            if (IsTabActive)
            {
                bool signalDirty = ConsumeInventoryChangedSignals();
                signalDirty |= ConsumeToolLoadoutChangedSignals();
                signalDirty |= ConsumeDurabilityChangedSignals();
                if (signalDirty)
                    _refreshDirty = true;
            }

            _pendingSummaryMassPulseDelta += math.max(0f, deltaTime);
            _summaryMassPulseDirty = true;
        }

        public void LateFrameTick()
        {
            AdvanceLoadoutPresentationState(SystemDispatcher.CurrentFrameDeltaTime);

            if (_refreshDirty && IsTabActive)
                RefreshAll();

            if (!_summaryMassPulseDirty)
                return;

            float deltaTime = _pendingSummaryMassPulseDelta;
            _pendingSummaryMassPulseDelta = 0f;
            _summaryMassPulseDirty = false;
            UpdateSummaryMassPulse(deltaTime);
        }

        // ════════════════════════════════════════════════════════════
        //  ZERO-GC OPTIMIZATION HELPERS
        // ════════════════════════════════════════════════════════════

        private void AutoResolve()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerInventory == null && playerContext != null)
                playerInventory = playerContext.Inventory;
            if (toolManager == null && playerContext != null)
                toolManager = playerContext.ToolManager;
            if (playerPDA == null && playerContext != null)
                playerPDA = playerContext.PlayerPDA;

            if ((!playerInventory || !toolManager || !playerPDA) &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerInventory == null)
                    playerTransform.TryGetComponent(out playerInventory);

                if (toolManager == null)
                    playerTransform.TryGetComponent(out toolManager);

                if (playerPDA == null)
                    playerTransform.TryGetComponent(out playerPDA);
            }

            if (playerPDA == null)
            {
                for (Transform current = transform; current != null; current = current.parent)
                {
                    if (current.TryGetComponent(out playerPDA))
                        break;
                }
            }

            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
            RefreshInventorySignalBinding();
            RefreshToolLoadoutSignalBinding();
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
        }

        private void CachePlayerInventoryService(object inventoryService)
        {
            _inventoryService = inventoryService as IPlayerInventoryService;
        }

        private void CachePlayerExpressionReadModel(IPlayerExpressionReadModel expressionReadModel)
        {
            _playerExpressionReadModel = expressionReadModel ?? playerExpressionManager;
        }

        private void CacheToolDurabilityService(IToolDurabilityService durabilityService)
        {
            _toolDurabilityService = durabilityService;
        }

        private bool TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionReadModel)
        {
            expressionReadModel = _playerExpressionReadModel ?? playerExpressionManager;
            return expressionReadModel != null;
        }

        private void ClearPlayerOwnedReferences(IPlayerRuntimeContext previousContext)
        {
            if (previousContext == null)
                return;

            if (ReferenceEquals(playerInventory, previousContext.Inventory))
                playerInventory = null;
            if (ReferenceEquals(toolManager, previousContext.ToolManager))
                toolManager = null;
            if (ReferenceEquals(playerPDA, previousContext.PlayerPDA))
                playerPDA = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    ClearPlayerOwnedReferences(previousService as IPlayerRuntimeContext);
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    AutoResolve();
                    _refreshDirty = true;
                    if (IsTabActive)
                        RefreshAll();
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    CachePlayerInventoryService(currentService);
                    AutoResolve();
                    _refreshDirty = true;
                    if (IsTabActive)
                        RefreshAll();
                    break;
                case GlobalRegistryServiceSlot.PlayerExpressionRuntime:
                    CachePlayerExpressionReadModel(currentService as IPlayerExpressionReadModel);
                    _refreshDirty = true;
                    if (IsTabActive)
                        RefreshAll();
                    break;
                case GlobalRegistryServiceSlot.ToolDurabilityRuntime:
                    CacheToolDurabilityService(currentService as IToolDurabilityService);
                    _refreshDirty = true;
                    if (IsTabActive)
                        RefreshAll();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService != null)
                    {
                        UnregisterFromLateFrameManager();
                        RegisterToLateFrameManager();
                    }
                    break;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void AutoResolveTabIndex()
        {
            if (gameObject.name.Contains("Loadout", System.StringComparison.OrdinalIgnoreCase))
                loadoutTabIndex = 1;
        }

#if UNITY_EDITOR
        private void AutoResolvePresets()
        {
            if (loadoutPresets == null || loadoutPresets.Length < 4)
                loadoutPresets = new ToolLoadoutPreset[4];

            TryAssignPreset(ref loadoutPresets[0], "Assets/_Project/Data/Tools/Presets/Preset_Exploration.asset");
            TryAssignPreset(ref loadoutPresets[1], "Assets/_Project/Data/Tools/Presets/Preset_Construction.asset");
            TryAssignPreset(ref loadoutPresets[2], "Assets/_Project/Data/Tools/Presets/Preset_FieldRecovery.asset");
            TryAssignPreset(ref loadoutPresets[3], "Assets/_Project/Data/Tools/Presets/Preset_Defense.asset");
        }

        private static void TryAssignPreset(ref ToolLoadoutPreset target, string path)
        {
            if (target != null)
                return;

            target = AssetDatabase.LoadAssetAtPath<ToolLoadoutPreset>(path);
        }
#endif

        private void Subscribe()
        {
            PDAEvents.Register(this);
            PlayerExpressionEvents.Register(this);
        }

        private void Unsubscribe()
        {
            PDAEvents.Unregister(this);
            PlayerExpressionEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            PDAEvents.AssertUnregistered(this, nameof(PDALoadoutTab));
        }

        private bool ConsumeInventoryChangedSignals()
        {
            uint inventoryHash = _inventorySignalHash;
            if (inventoryHash == 0u)
                return false;

            bool dirty = false;
            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly InventoryChangedSignal signal = ref signals[i];
                if (signal.InventoryHash != inventoryHash)
                    continue;

                if (signal.Revision == _lastInventorySignalRevision && _lastInventorySignalRevision != 0u)
                    continue;

                _lastInventorySignalRevision = signal.Revision;
                dirty = true;
            }

            return dirty;
        }

        private bool ConsumeDurabilityChangedSignals()
        {
            ReadOnlySpan<ItemDurabilityChangedSignal> signals = SignalBus<ItemDurabilityChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ItemDurabilityChangedSignal signal = ref signals[i];
                if (signal.InventoryHash == 0u)
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

            bool dirty = false;
            ReadOnlySpan<ToolLoadoutChangedSignal> signals = SignalBus<ToolLoadoutChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ToolLoadoutChangedSignal signal = ref signals[i];
                if (signal.SourceId != sourceId)
                    continue;

                if (signal.Sequence == _lastToolLoadoutSignalSequence && _lastToolLoadoutSignalSequence != 0u)
                    continue;

                _lastToolLoadoutSignalSequence = signal.Sequence;
                dirty = true;
            }

            return dirty;
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

        public void OnPlayerExpressionEvent(in PlayerExpressionEventPayload payload)
        {
            if ((PlayerExpressionEventType)payload.EventType != PlayerExpressionEventType.ProfileChanged)
                return;

            HandlePlayerExpressionChanged();
        }

        private void HandlePlayerExpressionChanged()
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    HandlePdaOpened(payload.CurrentTab);
                    break;
                case PDAEventType.TabChanged:
                    HandlePdaTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        private void HandlePdaOpened(int tab)
        {
            if (tab != loadoutTabIndex) return;
            AutoResolve();
            _refreshDirty = true;
            RefreshAll();
        }

        private void HandlePdaTabChanged(int _, int newTab)
        {
            if (newTab != loadoutTabIndex) return;
            AutoResolve();
            _refreshDirty = true;
            RefreshAll();
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            ClearChildren(self);

            Image bg = EnsureImage(self.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            title.color = Primary;
            SetLiteralText(title, "LOADOUT MATRIX".AsSpan());

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            SetLiteralText(sub, "quick-slot readiness, durability state, and expedition utility profile".AsSpan());

            CreateRule(self, -52f);

            _slotRoots = new RectTransform[4];
            _slotBgs = new Image[4];
            _slotAccents = new Image[4];
            _slotIcons = new Image[4];
            _slotStatusBgs = new Image[4];
            _slotTitles = new TextMeshProUGUI[4];
            _slotStatuses = new TextMeshProUGUI[4];
            _slotBodies = new TextMeshProUGUI[4];
            _slotActionRoots = new RectTransform[4];
            _slotClearRoots = new RectTransform[4];
            _slotActionCanvasGroups = new CanvasGroup[4];
            _slotClearCanvasGroups = new CanvasGroup[4];
            _slotActionBgs = new Image[4];
            _slotClearBgs = new Image[4];
            _slotActionLabels = new TextMeshProUGUI[4];
            _slotClearLabels = new TextMeshProUGUI[4];
            _presetRoots = new RectTransform[loadoutPresets != null ? loadoutPresets.Length : 0];
            _presetCanvasGroups = new CanvasGroup[_presetRoots.Length];
            _presetBgs = new Image[_presetRoots.Length];
            _presetTitles = new TextMeshProUGUI[_presetRoots.Length];
            _presetBodies = new TextMeshProUGUI[_presetRoots.Length];

            const float left = 18f;
            const float right = 18f;
            const float top = 72f;
            const float bottom = 88f;
            const float gap = 14f;
            const float totalWidth = 1320f;
            const float totalHeight = 760f;

            float cardWidth = (totalWidth - left - right - gap) * 0.5f;
            float cardHeight = (totalHeight - top - bottom - gap) * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                int col = i % 2;
                int row = i / 2;

                RectTransform card = CreateRect(self, "LoadoutSlot");
                card.anchorMin = new Vector2(0f, 1f);
                card.anchorMax = new Vector2(0f, 1f);
                card.pivot = new Vector2(0f, 1f);
                card.anchoredPosition = new Vector2(
                    left + col * (cardWidth + gap),
                    -(top + row * (cardHeight + gap)));
                card.sizeDelta = new Vector2(cardWidth, cardHeight);

                Image cardBg = EnsureImage(card.gameObject);
                cardBg.color = BoxBg;
                cardBg.raycastTarget = false;
                _slotRoots[i] = card;
                _slotBgs[i] = cardBg;

                Image accent = CreateImage("Accent", card, new Color(0.16f, 0.34f, 0.36f, 0.82f));
                RectTransform accentRect = accent.rectTransform;
                accentRect.anchorMin = new Vector2(0f, 0f);
                accentRect.anchorMax = new Vector2(0f, 1f);
                accentRect.pivot = new Vector2(0f, 0.5f);
                accentRect.anchoredPosition = Vector2.zero;
                accentRect.sizeDelta = new Vector2(4f, 0f);
                accent.raycastTarget = false;
                _slotAccents[i] = accent;

                TextMeshProUGUI slotHdr = CreateText(card, "SlotHeader", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(slotHdr.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(14f, -10f), new Vector2(-14f, 16f));
                slotHdr.color = DimLow;
                SetSlotHeaderText(slotHdr, i + 1);

                TextMeshProUGUI titleText = CreateText(card, "ToolName", labelFont, 16f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(64f, -34f), new Vector2(-14f, 22f));
                titleText.color = Primary;
                _slotTitles[i] = titleText;

                Image icon = CreateImage("Icon", card, new Color(1f, 1f, 1f, 0.95f));
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 1f);
                iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.anchoredPosition = new Vector2(14f, -38f);
                iconRect.sizeDelta = new Vector2(38f, 38f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                _slotIcons[i] = icon;

                TextMeshProUGUI statusText = CreateText(card, "Status", numericFont, 11.5f, FontStyles.Bold, TextAlignmentOptions.Right);
                Anchor(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(196f, -32f), new Vector2(-18f, 24f));
                _slotStatuses[i] = statusText;

                Image statusBg = CreateImage("StatusBg", card, new Color(0.1f, 0.24f, 0.26f, 0.66f));
                RectTransform statusBgRect = statusBg.rectTransform;
                statusBgRect.anchorMin = new Vector2(1f, 1f);
                statusBgRect.anchorMax = new Vector2(1f, 1f);
                statusBgRect.pivot = new Vector2(1f, 1f);
                statusBgRect.anchoredPosition = new Vector2(-14f, -30f);
                statusBgRect.sizeDelta = new Vector2(156f, 24f);
                statusBg.raycastTarget = false;
                statusBg.transform.SetAsFirstSibling();
                _slotStatusBgs[i] = statusBg;

                CreateInnerRule(card, 0.62f);

                TextMeshProUGUI bodyText = CreateText(card, "Body", numericFont, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                bodyText.textWrappingMode = TextWrappingModes.NoWrap;
                bodyText.overflowMode = TextOverflowModes.Overflow;
                bodyText.color = Dim;
                Anchor(bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.62f),
                    new Vector2(14f, 14f), new Vector2(-14f, -74f));
                _slotBodies[i] = bodyText;

                RectTransform actionRoot = CreateRect(card, "ActionButton");
                actionRoot.anchorMin = new Vector2(0f, 0f);
                actionRoot.anchorMax = new Vector2(0f, 0f);
                actionRoot.pivot = new Vector2(0f, 0f);
                actionRoot.anchoredPosition = new Vector2(14f, 12f);
                actionRoot.sizeDelta = new Vector2(124f, 28f);
                Image actionBg = EnsureImage(actionRoot.gameObject);
                actionBg.color = new Color(0.08f, 0.18f, 0.2f, 0.74f);
                actionBg.raycastTarget = true;
                TextMeshProUGUI actionLabel = CreateText(actionRoot, "ActionLabel", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(actionLabel.rectTransform, 0f, 0f, 0f, 0f);
                actionLabel.color = Primary;
                _slotActionRoots[i] = actionRoot;
                _slotActionCanvasGroups[i] = EnsureCanvasGroup(actionRoot);
                _slotActionBgs[i] = actionBg;
                _slotActionLabels[i] = actionLabel;
                PDALoadoutSlotActionButton actionButton = actionRoot.gameObject.AddComponent<PDALoadoutSlotActionButton>();
                actionButton.Init(this, i, false, actionBg,
                    new Color(0.08f, 0.18f, 0.2f, 0.74f),
                    new Color(0.14f, 0.28f, 0.3f, 0.92f));

                RectTransform clearRoot = CreateRect(card, "ClearButton");
                clearRoot.anchorMin = new Vector2(0f, 0f);
                clearRoot.anchorMax = new Vector2(0f, 0f);
                clearRoot.pivot = new Vector2(0f, 0f);
                clearRoot.anchoredPosition = new Vector2(146f, 12f);
                clearRoot.sizeDelta = new Vector2(92f, 28f);
                Image clearBg = EnsureImage(clearRoot.gameObject);
                clearBg.color = new Color(0.22f, 0.08f, 0.08f, 0.74f);
                clearBg.raycastTarget = true;
                TextMeshProUGUI clearLabel = CreateText(clearRoot, "ClearLabel", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(clearLabel.rectTransform, 0f, 0f, 0f, 0f);
                clearLabel.color = new Color(1f, 0.82f, 0.78f, 0.94f);
                SetLiteralText(clearLabel, "CLEAR".AsSpan());
                _slotClearRoots[i] = clearRoot;
                _slotClearCanvasGroups[i] = EnsureCanvasGroup(clearRoot);
                _slotClearBgs[i] = clearBg;
                _slotClearLabels[i] = clearLabel;
                PDALoadoutSlotActionButton clearButton = clearRoot.gameObject.AddComponent<PDALoadoutSlotActionButton>();
                clearButton.Init(this, i, true, clearBg,
                    new Color(0.22f, 0.08f, 0.08f, 0.74f),
                    new Color(0.34f, 0.12f, 0.12f, 0.92f));
            }

            BuildPresetStrip(self);

            _summaryText = CreateText(self, "Summary", numericFont, 11.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(_summaryText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 18f), new Vector2(-20f, 46f));
            _summaryText.color = Dim;

            _identityActionRoot = CreateRect(self, "IdentityActionButton");
            _identityActionRoot.anchorMin = new Vector2(1f, 0f);
            _identityActionRoot.anchorMax = new Vector2(1f, 0f);
            _identityActionRoot.pivot = new Vector2(1f, 0f);
            _identityActionRoot.anchoredPosition = new Vector2(-20f, 82f);
            _identityActionRoot.sizeDelta = new Vector2(184f, 26f);
            _identityActionBg = EnsureImage(_identityActionRoot.gameObject);
            _identityActionBg.color = new Color(0.08f, 0.18f, 0.2f, 0.82f);
            _identityActionBg.raycastTarget = true;
            _identityActionLabel = CreateText(_identityActionRoot, "IdentityActionLabel", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_identityActionLabel.rectTransform, 0f, 0f, 0f, 0f);
            _identityActionLabel.color = Primary;
            SetLiteralText(_identityActionLabel, "CYCLE SUIT IDENTITY".AsSpan());
            _identityActionCanvasGroup = EnsureCanvasGroup(_identityActionRoot);
            PDALoadoutIdentityButton identityButton = _identityActionRoot.gameObject.AddComponent<PDALoadoutIdentityButton>();
            identityButton.Init(
                this,
                _identityActionBg,
                new Color(0.08f, 0.18f, 0.2f, 0.82f),
                new Color(0.14f, 0.28f, 0.3f, 0.94f));
            SetCanvasGroupVisible(_identityActionCanvasGroup, false);

            _recommendedActionRoot = CreateRect(self, "RecommendedActionButton");
            _recommendedActionRoot.anchorMin = new Vector2(1f, 0f);
            _recommendedActionRoot.anchorMax = new Vector2(1f, 0f);
            _recommendedActionRoot.pivot = new Vector2(1f, 0f);
            _recommendedActionRoot.anchoredPosition = new Vector2(-20f, 50f);
            _recommendedActionRoot.sizeDelta = new Vector2(184f, 26f);
            _recommendedActionBg = EnsureImage(_recommendedActionRoot.gameObject);
            _recommendedActionBg.color = new Color(0.08f, 0.18f, 0.2f, 0.82f);
            _recommendedActionBg.raycastTarget = true;
            _recommendedActionLabel = CreateText(_recommendedActionRoot, "RecommendedActionLabel", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_recommendedActionLabel.rectTransform, 0f, 0f, 0f, 0f);
            _recommendedActionLabel.color = Primary;
            SetLiteralText(_recommendedActionLabel, "APPLY SUGGESTED".AsSpan());
            _recommendedActionCanvasGroup = EnsureCanvasGroup(_recommendedActionRoot);
            PDALoadoutRecommendedButton recommendedButton = _recommendedActionRoot.gameObject.AddComponent<PDALoadoutRecommendedButton>();
            recommendedButton.Init(
                this,
                _recommendedActionBg,
                new Color(0.08f, 0.18f, 0.2f, 0.82f),
                new Color(0.14f, 0.28f, 0.3f, 0.94f));
            SetCanvasGroupVisible(_recommendedActionCanvasGroup, false);

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Right);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 18f), new Vector2(-214f, 46f));
            _hintText.color = DimLow;
            SetLiteralText(_hintText, "Assign tools from Inventory details. Hotbar mirrors this matrix live.".AsSpan());

            _built = true;
        }

        private void RefreshAll()
        {
            if (!IsTabActive && !_refreshDirty)
                return;

            EnsureBuilt();
            RefreshSlots();
            RefreshPresets();
            RefreshSummary();
            RefreshIdentityAction();
            _refreshDirty = false;
        }

        private void RefreshSlots()
        {
            if (_slotRoots == null || toolManager == null) return;

            IToolDurabilityService durabilitySystem = _toolDurabilityService;

            for (int i = 0; i < 4; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                IPlayerToolDataReadModel tool = ResolvePrefabTool(prefab);
                ItemData item = tool != null ? tool.ToolData : null;
                ToolMetadata meta = tool != null ? tool.Metadata : null;
                ResolveSlotDurabilityHashes(i, prefab, tool, meta, out uint itemHash, out uint metadataHash);

                bool active = toolManager.CurrentSlotIndex == i;
                bool assigned = prefab != null && tool != null;
                bool available = toolManager.IsToolAvailableInSlot(i);
                bool broken = TryReadBrokenByHashes(durabilitySystem, itemHash, metadataHash, out bool resolvedBroken) && resolvedBroken;

                _slotBgs[i].color = active ? BoxActive : BoxBg;
                _slotIcons[i].sprite = item != null ? item.icon : null;
                _slotIcons[i].enabled = item != null && item.icon != null;

                if (!assigned)
                {
                    if (_slotAccents[i] != null)
                        _slotAccents[i].color = new Color(0.42f, 0.36f, 0.14f, 0.86f);
                    if (_slotStatusBgs[i] != null)
                        _slotStatusBgs[i].color = new Color(0.28f, 0.2f, 0.06f, 0.76f);
                    SetLiteralText(_slotTitles[i], "UNASSIGNED".AsSpan());
                    _slotStatuses[i].color = Missing;
                    SetLiteralText(_slotStatuses[i], "EMPTY".AsSpan());
                    SetLiteralText(_slotBodies[i], "No held-tool prefab is mapped to this quick slot.\nAssign a tool from Inventory to arm the slot.".AsSpan());
                    if (_slotActionLabels[i] != null) SetLiteralText(_slotActionLabels[i], "UNASSIGNED".AsSpan());
                    SetCanvasGroupVisible(_slotActionCanvasGroups[i], false);
                    SetCanvasGroupVisible(_slotClearCanvasGroups[i], false);
                    continue;
                }

                SetCanvasGroupVisible(_slotActionCanvasGroups[i], true);
                SetCanvasGroupVisible(_slotClearCanvasGroups[i], true);

                SetUpperText(_slotTitles[i], item != null ? item.itemName : prefab.name, false);

                if (broken)
                {
                    if (_slotAccents[i] != null)
                        _slotAccents[i].color = new Color(0.88f, 0.34f, 0.28f, 0.92f);
                    if (_slotStatusBgs[i] != null)
                        _slotStatusBgs[i].color = new Color(0.34f, 0.12f, 0.12f, 0.84f);
                    _slotStatuses[i].color = Broken;
                    SetLiteralText(_slotStatuses[i], active ? "BROKEN / ACTIVE".AsSpan() : "BROKEN".AsSpan());
                }
                else if (!available)
                {
                    if (_slotAccents[i] != null)
                        _slotAccents[i].color = new Color(0.94f, 0.68f, 0.22f, 0.92f);
                    if (_slotStatusBgs[i] != null)
                        _slotStatusBgs[i].color = new Color(0.3f, 0.2f, 0.06f, 0.82f);
                    _slotStatuses[i].color = Missing;
                    SetLiteralText(_slotStatuses[i], active ? "MISSING / ACTIVE".AsSpan() : "MISSING".AsSpan());
                }
                else
                {
                    if (_slotAccents[i] != null)
                        _slotAccents[i].color = active
                            ? new Color(0.46f, 0.98f, 0.94f, 0.98f)
                            : new Color(0.2f, 0.7f, 0.78f, 0.88f);
                    if (_slotStatusBgs[i] != null)
                        _slotStatusBgs[i].color = active
                            ? new Color(0.1f, 0.3f, 0.3f, 0.86f)
                            : new Color(0.08f, 0.2f, 0.22f, 0.76f);
                    _slotStatuses[i].color = Ready;
                    SetLiteralText(_slotStatuses[i], active ? "READY / ACTIVE".AsSpan() : "READY".AsSpan());
                }

                float weight = item != null ? item.weight : 0f;
                float currentDurability = meta != null &&
                                          TryReadDurabilityByHashes(durabilitySystem, itemHash, metadataHash, meta.maxDurability, out float resolvedDurability)
                    ? resolvedDurability
                    : (meta != null ? meta.maxDurability : 0f);
                float normalized = meta != null
                    ? Mathf.Clamp01(currentDurability / Mathf.Max(1f, meta.maxDurability))
                    : 1f;

                SetSlotBodyText(
                    _slotBodies[i],
                    ResolveCategoryLabel(item),
                    itemHash != 0u && playerInventory != null
                        ? playerInventory.CountTotal(unchecked((int)itemHash))
                        : 0,
                    weight,
                    normalized,
                    meta != null ? Mathf.Max(0f, meta.energyConsumptionRate) : 0f);

                if (_slotActionLabels[i] != null)
                {
                    if (active)
                        SetLiteralText(_slotActionLabels[i], "HOLSTER".AsSpan());
                    else if (broken)
                        SetLiteralText(_slotActionLabels[i], "BROKEN".AsSpan());
                    else if (!available)
                        SetLiteralText(_slotActionLabels[i], "MISSING".AsSpan());
                    else
                        SetLiteralText(_slotActionLabels[i], "ACTIVATE".AsSpan());
                }
            }
        }

        private void RefreshSummary()
        {
            if (_summaryText == null || toolManager == null)
                return;

            int assigned = 0;
            int ready = 0;
            int missing = 0;
            int broken = 0;
            float totalWeight = 0f;
            float inventoryMass = playerInventory != null ? Mathf.Max(0f, playerInventory.TotalWeight) : 0f;
            float carryCapacity = _inventoryService != null ? _inventoryService.CarryCapacityKilograms : 200f;
            string recommendedPresetName = "GENERAL";
            string recommendedPresetDirective = "No authored target in front of the diver. General-purpose expedition loadout remains valid.";

            IToolDurabilityService durabilitySystem = _toolDurabilityService;

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                IPlayerToolDataReadModel tool = ResolvePrefabTool(prefab);
                if (tool == null)
                    continue;

                assigned++;
                if (tool.ToolData != null)
                    totalWeight += tool.ToolData.weight;

                ResolveSlotDurabilityHashes(i, prefab, tool, tool.Metadata, out uint itemHash, out uint metadataHash);
                if (TryReadBrokenByHashes(durabilitySystem, itemHash, metadataHash, out bool resolvedBroken) && resolvedBroken)
                    broken++;
                else if (toolManager.IsToolAvailableInSlot(i))
                    ready++;
                else
                    missing++;
            }

            Transform origin = toolManager.transform;
            if (FieldLoadoutAdvisor.TryBuildForwardAdvice(origin, fieldAdviceRange, fieldAdviceMask, out FieldLoadoutAdvisor.LoadoutAdvice advice))
            {
                recommendedPresetName = advice.PresetName;
                recommendedPresetDirective = advice.Summary;
            }

            Span<char> summaryDestination = _summaryCharBuffer.AsSpan();
            int summaryIndex = 0;
            _summaryMassCharStart = summaryIndex;
            Append(summaryDestination, ref summaryIndex, "MASS: ".AsSpan());
            AppendFloat(summaryDestination, ref summaryIndex, inventoryMass, "0.0".AsSpan());
            Append(summaryDestination, ref summaryIndex, "/".AsSpan());
            AppendFloat(summaryDestination, ref summaryIndex, carryCapacity, "0.0".AsSpan());
            Append(summaryDestination, ref summaryIndex, " KG | LOADOUT: ".AsSpan());
            _summaryMassCharLength = summaryIndex - _summaryMassCharStart - " | LOADOUT: ".Length;
            _inventoryMassOverCapacity = inventoryMass > carryCapacity + 0.001f;
            AppendInt(summaryDestination, ref summaryIndex, assigned);
            Append(summaryDestination, ref summaryIndex, "/4 assigned | READY ".AsSpan());
            AppendInt(summaryDestination, ref summaryIndex, ready);
            Append(summaryDestination, ref summaryIndex, " | MISSING ".AsSpan());
            AppendInt(summaryDestination, ref summaryIndex, missing);
            Append(summaryDestination, ref summaryIndex, " | BROKEN ".AsSpan());
            AppendInt(summaryDestination, ref summaryIndex, broken);
            Append(summaryDestination, ref summaryIndex, " | ACTIVE SLOT ".AsSpan());
            if (toolManager.CurrentSlotIndex >= 0)
                AppendInt(summaryDestination, ref summaryIndex, toolManager.CurrentSlotIndex + 1);
            else
                Append(summaryDestination, ref summaryIndex, "--".AsSpan());
            Append(summaryDestination, ref summaryIndex, " | KIT MASS ".AsSpan());
            AppendFloat(summaryDestination, ref summaryIndex, totalWeight, "0.0".AsSpan());
            Append(summaryDestination, ref summaryIndex, " kg | PRESET ".AsSpan());
            AppendMatchedPresetName(summaryDestination, ref summaryIndex);
            Append(summaryDestination, ref summaryIndex, " | IDENTITY ".AsSpan());
            AppendActiveExpressionName(summaryDestination, ref summaryIndex);

            string liveSuitName = GetLiveExpressionSuitName();
            if (!string.IsNullOrWhiteSpace(liveSuitName))
            {
                Append(summaryDestination, ref summaryIndex, " | SHELL ".AsSpan());
                AppendUpper(summaryDestination, ref summaryIndex, liveSuitName);
            }

            Append(summaryDestination, ref summaryIndex, " | SUGGESTED ".AsSpan());
            Append(summaryDestination, ref summaryIndex, recommendedPresetName);
            Append(summaryDestination, ref summaryIndex, "\nACTIVE TOOL: ".AsSpan());
            if (!toolManager.TryWriteCurrentToolOperationalSummary(summaryDestination.Slice(summaryIndex), out int toolSummaryLength))
                Append(summaryDestination, ref summaryIndex, "NO TOOL ARMED".AsSpan());
            else
                summaryIndex += toolSummaryLength;

            _summaryText.SetCharArray(_summaryCharBuffer, 0, summaryIndex);
            _summaryMassVertexRefreshDeferred = true;
            _appliedSummaryMassColor = default;

            SetHintText(assigned, ready, missing, broken, recommendedPresetDirective);

            RefreshRecommendedAction();
        }

        private void RefreshIdentityAction()
        {
            if (_identityActionRoot == null || _identityActionLabel == null || _identityActionBg == null)
                return;

            if (!TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager) ||
                expressionManager.ProfileCount <= 1 ||
                !expressionManager.TryGetNextProfileDisplayName(out string nextProfileDisplayName))
            {
                SetCanvasGroupVisible(_identityActionCanvasGroup, false);
                return;
            }

            SetCanvasGroupVisible(_identityActionCanvasGroup, true);
            SetPrefixedUpperText(_identityActionLabel, "NEXT IDENTITY - ".AsSpan(), nextProfileDisplayName);
            _identityActionBg.color = new Color(0.08f, 0.18f, 0.2f, 0.82f);
        }

        private string GetLoadoutDirective(int assigned, int ready, int missing, int broken)
        {
            if (assigned == 0)
                return "Directive: no expedition kit armed. Assign core tools from Inventory before departure.";

            if (broken > 0)
                return "Directive: one or more assigned tools are broken. Repair or replace them before committing to deep-water tasks.";

            if (missing > 0)
                return "Directive: loadout references tools not currently present in cargo. Re-arm or clear those slots to avoid dead hotbar space.";

            if (ready < 2)
                return "Directive: expedition utility is too thin. Carry at least two ready tools before leaving the habitat perimeter.";

            if (toolManager.CurrentSlotIndex < 0)
                return "Directive: kit is ready. Activate a slot to deploy a field tool.";

            return "Directive: loadout matrix is mission-ready. Use Activate/Holster here for controlled tool management.";
        }

        private string GetRecommendedPresetName()
        {
            if (toolManager == null)
                return "UNKNOWN";

            Transform origin = toolManager.transform;
            return FieldLoadoutAdvisor.TryBuildForwardPresetName(origin, fieldAdviceRange, fieldAdviceMask, out string presetName)
                ? presetName
                : "GENERAL";
        }

        private string GetRecommendedPresetDirective()
        {
            if (toolManager == null)
                return "No field loadout advice available.";

            Transform origin = toolManager.transform;
            return FieldLoadoutAdvisor.TryBuildForwardAdvice(origin, fieldAdviceRange, fieldAdviceMask, out FieldLoadoutAdvisor.LoadoutAdvice advice)
                ? advice.Summary
                : "No authored target in front of the diver. General-purpose expedition loadout remains valid.";
        }

        private void RefreshPresets()
        {
            if (_presetRoots == null || _presetBgs == null)
                return;

            for (int i = 0; i < _presetRoots.Length; i++)
            {
                ToolLoadoutPreset preset = loadoutPresets != null && i < loadoutPresets.Length
                    ? loadoutPresets[i]
                    : null;

                bool hasPreset = preset != null;
                bool matched = hasPreset && MatchesPreset(preset);

                SetCanvasGroupVisible(_presetCanvasGroups[i], hasPreset);

                if (!hasPreset)
                    continue;

                if (_presetBgs[i] != null)
                {
                    _presetBgs[i].color = matched
                        ? new Color(0.1f, 0.28f, 0.3f, 0.9f)
                        : new Color(0.07f, 0.15f, 0.17f, 0.76f);
                }

                if (_presetTitles[i] != null)
                    SetUpperText(_presetTitles[i], preset.presetName, false);

                if (_presetBodies[i] != null)
                {
                    int ready = CountReadyToolsInPreset(preset);

                    SetPresetBodyText(_presetBodies[i], preset, ready, matched);
                }
            }
        }

        internal void InvokeSlotAction(int slotIndex, bool clearAssignment)
        {
            if (toolManager == null || slotIndex < 0 || slotIndex >= toolManager.SlotCount)
                return;

            GameObject prefab = toolManager.GetAssignedToolPrefab(slotIndex);
            IPlayerToolDataReadModel tool = ResolvePrefabTool(prefab);
            ItemData item = tool != null ? tool.ToolData : null;

            if (clearAssignment)
            {
                if (prefab == null)
                {
                    NotifySlotMessage(true, "LOADOUT SLOT ".AsSpan(), slotIndex, " IS ALREADY EMPTY".AsSpan());
                    return;
                }

                toolManager.SetAssignedToolPrefab(slotIndex, null, holsterIfCurrentInvalid: true);
                RefreshAll();
                NotifySlotMessage(false, "LOADOUT CLEARED - SLOT ".AsSpan(), slotIndex, ReadOnlySpan<char>.Empty);
                return;
            }

            if (prefab == null || tool == null)
            {
                NotifySlotMessage(true, "NO TOOL ASSIGNED TO SLOT ".AsSpan(), slotIndex, ReadOnlySpan<char>.Empty);
                return;
            }

            IToolDurabilityService durabilitySystem = _toolDurabilityService;
            ResolveSlotDurabilityHashes(slotIndex, prefab, tool, tool.Metadata, out uint itemHash, out uint metadataHash);
            if (TryReadBrokenByHashes(durabilitySystem, itemHash, metadataHash, out bool resolvedBroken) && resolvedBroken)
            {
                NotifyUpperSuffix(true, item != null ? item.itemName : "TOOL", " IS BROKEN".AsSpan());
                return;
            }

            if (!toolManager.IsToolAvailableInSlot(slotIndex))
            {
                NotifyUpperSuffix(true, item != null ? item.itemName : "TOOL", " IS NOT IN CARGO".AsSpan());
                return;
            }

            if (toolManager.CurrentSlotIndex == slotIndex)
            {
                toolManager.Holster();
                RefreshAll();
                NotifySlotMessage(false, "LOADOUT HOLSTERED - SLOT ".AsSpan(), slotIndex, ReadOnlySpan<char>.Empty);
                return;
            }

            toolManager.SwitchToSlot(slotIndex);
            RefreshAll();
            NotifyActiveSlot(slotIndex, item != null ? item.itemName : prefab.name);
        }

        internal void InvokePresetAction(int presetIndex)
        {
            if (toolManager == null || loadoutPresets == null || presetIndex < 0 || presetIndex >= loadoutPresets.Length)
                return;

            ToolLoadoutPreset preset = loadoutPresets[presetIndex];
            if (preset == null)
            {
                NotifyWarning("LOADOUT PRESET IS NOT CONFIGURED".AsSpan());
                return;
            }

            if (!toolManager.ApplyLoadoutPreset(preset, holsterBeforeApplyingPreset))
            {
                NotifyPrefixUpper(true, "FAILED TO APPLY ".AsSpan(), preset.presetName);
                return;
            }

            RefreshAll();
            NotifyPrefixUpper(false, "LOADOUT PRESET APPLIED - ".AsSpan(), preset.presetName);
        }

        internal void InvokeRecommendedPresetAction()
        {
            int presetIndex = GetRecommendedPresetIndex();
            if (presetIndex < 0)
            {
                NotifyWarning("NO SUGGESTED PRESET FOR CURRENT FIELD TARGET".AsSpan());
                return;
            }

            ToolLoadoutPreset preset = loadoutPresets != null && presetIndex < loadoutPresets.Length
                ? loadoutPresets[presetIndex]
                : null;
            if (preset == null)
            {
                NotifyWarning("SUGGESTED PRESET IS NOT CONFIGURED".AsSpan());
                return;
            }

            if (MatchesPreset(preset))
            {
                NotifyPrefixUpper(false, "SUGGESTED KIT ALREADY ACTIVE - ".AsSpan(), preset.presetName);
                return;
            }

            InvokePresetAction(presetIndex);
        }

        internal void InvokeIdentityCycleAction()
        {
            if (!TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager))
            {
                NotifyWarning("SUIT IDENTITY CATALOG IS NOT AVAILABLE".AsSpan());
                return;
            }

            if (!expressionManager.CycleNextProfile(false))
            {
                NotifyWarning("FAILED TO SWITCH SUIT IDENTITY".AsSpan());
                return;
            }

            RefreshAll();
        }

        private void NotifyInfo(ReadOnlySpan<char> message)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            if (hudNotification == null || message.Length <= 0)
                return;

            _notificationBuffer.Clear();
            _notificationBuffer.Append(message);
            hudNotification.ShowInfo(in _notificationBuffer);
        }

        private void NotifyInfo(in FixedCharBuffer messageBuffer)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            if (hudNotification != null)
                hudNotification.ShowInfo(in messageBuffer);
        }

        private void NotifyWarning(ReadOnlySpan<char> message)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            if (hudNotification == null || message.Length <= 0)
                return;

            _notificationBuffer.Clear();
            _notificationBuffer.Append(message);
            hudNotification.ShowWarning(in _notificationBuffer);
        }

        private void NotifyWarning(in FixedCharBuffer messageBuffer)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            if (hudNotification != null)
                hudNotification.ShowWarning(in messageBuffer);
        }

        private void NotifySlotMessage(bool warning, ReadOnlySpan<char> prefix, int slotIndex, ReadOnlySpan<char> suffix)
        {
            _notificationBuffer.Clear();
            _notificationBuffer.Append(prefix);
            _notificationBuffer.AppendInt(slotIndex + 1);
            _notificationBuffer.Append(suffix);
            if (warning)
                NotifyWarning(in _notificationBuffer);
            else
                NotifyInfo(in _notificationBuffer);
        }

        private void NotifyUpperSuffix(bool warning, string value, ReadOnlySpan<char> suffix)
        {
            _notificationBuffer.Clear();
            AppendUpperInvariant(ref _notificationBuffer, value);
            _notificationBuffer.Append(suffix);
            if (warning)
                NotifyWarning(in _notificationBuffer);
            else
                NotifyInfo(in _notificationBuffer);
        }

        private void NotifyPrefixUpper(bool warning, ReadOnlySpan<char> prefix, string value)
        {
            _notificationBuffer.Clear();
            _notificationBuffer.Append(prefix);
            AppendUpperInvariant(ref _notificationBuffer, value);
            if (warning)
                NotifyWarning(in _notificationBuffer);
            else
                NotifyInfo(in _notificationBuffer);
        }

        private void NotifyActiveSlot(int slotIndex, string itemName)
        {
            _notificationBuffer.Clear();
            _notificationBuffer.Append("LOADOUT ACTIVE - SLOT ".AsSpan());
            _notificationBuffer.AppendInt(slotIndex + 1);
            _notificationBuffer.Append(": ".AsSpan());
            AppendUpperInvariant(ref _notificationBuffer, itemName);
            NotifyInfo(in _notificationBuffer);
        }

        private void BuildPresetStrip(RectTransform self)
        {
            if (_presetRoots == null || _presetRoots.Length == 0)
                return;

            TextMeshProUGUI hdr = CreateText(self, "PresetHeader", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(hdr.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 58f), new Vector2(-20f, 80f));
            hdr.color = DimLow;
            SetLiteralText(hdr, "MISSION PRESETS".AsSpan());

            float gap = 10f;
            float width = (1320f - 40f - gap * (_presetRoots.Length - 1)) / Mathf.Max(1, _presetRoots.Length);

            for (int i = 0; i < _presetRoots.Length; i++)
            {
                RectTransform root = CreateRect(self, "Preset");
                root.anchorMin = new Vector2(0f, 0f);
                root.anchorMax = new Vector2(0f, 0f);
                root.pivot = new Vector2(0f, 0f);
                root.anchoredPosition = new Vector2(20f + i * (width + gap), 48f);
                root.sizeDelta = new Vector2(width, 64f);

                Image bg = EnsureImage(root.gameObject);
                bg.color = new Color(0.07f, 0.15f, 0.17f, 0.76f);
                bg.raycastTarget = true;

                TextMeshProUGUI title = CreateText(root, "Title", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(10f, -8f), new Vector2(-10f, 18f));
                title.color = Primary;

                TextMeshProUGUI body = CreateText(root, "Body", numericFont, 9.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                Anchor(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                    new Vector2(10f, 6f), new Vector2(-10f, -24f));
                body.color = Dim;

                PDALoadoutPresetButton button = root.gameObject.AddComponent<PDALoadoutPresetButton>();
                button.Init(this, i, bg,
                    new Color(0.07f, 0.15f, 0.17f, 0.76f),
                    new Color(0.12f, 0.22f, 0.24f, 0.92f));

                _presetRoots[i] = root;
                _presetCanvasGroups[i] = EnsureCanvasGroup(root);
                _presetBgs[i] = bg;
                _presetTitles[i] = title;
                _presetBodies[i] = body;
            }
        }

        private int GetRecommendedPresetIndex()
        {
            string recommended = GetRecommendedPresetName();
            if (string.IsNullOrWhiteSpace(recommended) || string.Equals(recommended, "GENERAL", System.StringComparison.OrdinalIgnoreCase))
                return -1;

            if (loadoutPresets == null)
                return -1;

            for (int i = 0; i < loadoutPresets.Length; i++)
            {
                ToolLoadoutPreset preset = loadoutPresets[i];
                if (preset == null || string.IsNullOrWhiteSpace(preset.presetName))
                    continue;

                if (string.Equals(preset.presetName, recommended, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void RefreshRecommendedAction()
        {
            if (_recommendedActionRoot == null || _recommendedActionLabel == null || _recommendedActionBg == null)
                return;

            int presetIndex = GetRecommendedPresetIndex();
            if (presetIndex < 0)
            {
                SetCanvasGroupVisible(_recommendedActionCanvasGroup, false);
                return;
            }

            SetCanvasGroupVisible(_recommendedActionCanvasGroup, true);

            ToolLoadoutPreset preset = loadoutPresets != null && presetIndex < loadoutPresets.Length
                ? loadoutPresets[presetIndex]
                : null;
            bool matched = preset != null && MatchesPreset(preset);
            SetPrefixedUpperText(
                _recommendedActionLabel,
                matched ? "SUGGESTED ACTIVE - ".AsSpan() : "APPLY SUGGESTED - ".AsSpan(),
                preset != null ? preset.presetName : string.Empty);
            _recommendedActionBg.color = matched
                ? new Color(0.1f, 0.3f, 0.3f, 0.9f)
                : new Color(0.08f, 0.18f, 0.2f, 0.82f);
        }

        private void AppendMatchedPresetName(Span<char> destination, ref int index)
        {
            if (loadoutPresets == null)
            {
                Append(destination, ref index, "--".AsSpan());
                return;
            }

            for (int i = 0; i < loadoutPresets.Length; i++)
            {
                ToolLoadoutPreset preset = loadoutPresets[i];
                if (preset != null && MatchesPreset(preset))
                {
                    AppendUpper(destination, ref index, preset.presetName);
                    return;
                }
            }

            Append(destination, ref index, "CUSTOM".AsSpan());
        }

        private bool MatchesPreset(ToolLoadoutPreset preset)
        {
            if (preset == null || toolManager == null)
                return false;

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject assigned = toolManager.GetAssignedToolPrefab(i);
                GameObject expected = preset.slotPrefabs != null && i < preset.slotPrefabs.Length
                    ? preset.slotPrefabs[i]
                    : null;

                if (!ReferenceEquals(assigned, expected))
                    return false;
            }

            return true;
        }

        private int CountReadyToolsInPreset(ToolLoadoutPreset preset)
        {
            if (preset == null || preset.slotPrefabs == null || playerInventory == null)
                return 0;

            int count = 0;
            for (int i = 0; i < preset.slotPrefabs.Length; i++)
            {
                GameObject prefab = preset.slotPrefabs[i];
                IPlayerToolDataReadModel tool = ResolvePrefabTool(prefab);
                uint itemHash = ResolveToolItemHash(tool);
                if (itemHash != 0u && playerInventory.ContainsItem(unchecked((int)itemHash)))
                    count++;
            }

            return count;
        }

        private void ResolveSlotDurabilityHashes(
            int slotIndex,
            GameObject prefab,
            IPlayerToolDataReadModel tool,
            ToolMetadata metadata,
            out uint itemHash,
            out uint metadataHash)
        {
            itemHash = 0u;
            metadataHash = 0u;

            if ((uint)slotIndex >= CachedLoadoutSlots)
            {
                itemHash = ResolveToolItemHash(tool);
                metadataHash = ResolveToolMetadataHash(metadata);
                return;
            }

            ulong prefabId = prefab != null ? EntityId.ToULong(prefab.GetEntityId()) : 0ul;
            if (_slotHashResolved[slotIndex] && _slotHashPrefabCache[slotIndex] == prefabId)
            {
                itemHash = _slotItemHashCache[slotIndex];
                metadataHash = _slotMetadataHashCache[slotIndex];
                return;
            }

            itemHash = ResolveToolItemHash(tool);
            metadataHash = ResolveToolMetadataHash(metadata);
            _slotItemHashCache[slotIndex] = itemHash;
            _slotMetadataHashCache[slotIndex] = metadataHash;
            _slotHashPrefabCache[slotIndex] = prefabId;
            _slotHashResolved[slotIndex] = true;
        }

        private static uint ResolveToolItemHash(IPlayerToolDataReadModel tool)
        {
            if (tool?.ToolData == null)
                return 0u;

            string persistentId = tool.ToolData.PersistentId;
            return !string.IsNullOrEmpty(persistentId)
                ? unchecked((uint)Hecton.Localization.LocHash.Compute(persistentId))
                : 0u;
        }

        private static uint ResolveToolMetadataHash(ToolMetadata metadata)
        {
            return metadata != null && !string.IsNullOrEmpty(metadata.toolID)
                ? unchecked((uint)Animator.StringToHash(metadata.toolID))
                : 0u;
        }

        private static bool TryReadBrokenByHashes(
            IToolDurabilityService durabilitySystem,
            uint itemHash,
            uint metadataHash,
            out bool broken)
        {
            broken = false;
            if (durabilitySystem == null)
                return false;

            if (itemHash != 0u && durabilitySystem.TryReadBroken(itemHash, out broken))
                return true;

            return metadataHash != 0u &&
                   metadataHash != itemHash &&
                   durabilitySystem.TryReadBroken(metadataHash, out broken);
        }

        private static bool TryReadDurabilityByHashes(
            IToolDurabilityService durabilitySystem,
            uint itemHash,
            uint metadataHash,
            float maxDurability,
            out float durability)
        {
            durability = maxDurability;
            if (durabilitySystem == null)
                return false;

            if (itemHash != 0u && durabilitySystem.TryReadDurability(itemHash, maxDurability, out durability))
                return true;

            return metadataHash != 0u &&
                   metadataHash != itemHash &&
                   durabilitySystem.TryReadDurability(metadataHash, maxDurability, out durability);
        }

        private IPlayerToolDataReadModel ResolvePrefabTool(GameObject prefab)
        {
            if (prefab == null)
                return null;

            ulong prefabId = EntityId.ToULong(prefab.GetEntityId());
            if (_prefabToolCache.TryGetValue(prefabId, out IPlayerToolDataReadModel cachedTool) &&
                cachedTool != null)
            {
                return cachedTool;
            }

            if (!prefab.TryGetComponent(out IPlayerToolDataReadModel resolvedTool))
                return null;

            _prefabToolCache[prefabId] = resolvedTool;
            return resolvedTool;
        }

        private void AppendActiveExpressionName(Span<char> destination, ref int index)
        {
            if (TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager))
            {
                AppendUpper(destination, ref index, expressionManager.GetActiveProfileName());
                return;
            }

            Append(destination, ref index, "STANDARD".AsSpan());
        }

        private string GetActiveExpressionSummary()
        {
            return TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager)
                ? expressionManager.GetActiveProfileSummary()
                : "Suit identity matrix is offline.";
        }

        private string GetActiveExpressionLoadoutName()
        {
            return TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager)
                ? expressionManager.GetActiveRecommendedLoadoutName()
                : string.Empty;
        }

        private string GetActiveExpressionSuitName()
        {
            return TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager)
                ? expressionManager.GetActiveRecommendedSuitName()
                : string.Empty;
        }

        private string GetLiveExpressionSuitName()
        {
            return TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager)
                ? expressionManager.GetLiveSuitName()
                : string.Empty;
        }

        private bool IsExpressionSuitApplied()
        {
            return TryGetPlayerExpressionReadModel(out IPlayerExpressionReadModel expressionManager) &&
                expressionManager.IsActiveRecommendedSuitApplied();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private static Image EnsureImage(GameObject target)
        {
            if (!target.TryGetComponent(out Image image))
                image = target.AddComponent<Image>();
            return image;
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target == null)
                return null;

            if (!target.TryGetComponent(out CanvasGroup group))
                group = target.gameObject.AddComponent<CanvasGroup>();

            return group;
        }

        private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private void RegisterToLateFrameManager()
        {
            if (_registeredToLateFrameDispatcher || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToLateFrameDispatcher = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromLateFrameManager()
        {
            if (!_registeredToLateFrameDispatcher)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredToLateFrameDispatcher = false;
        }

        private void UpdateSummaryMassPulse(float deltaTime)
        {
            if (_summaryText == null || _summaryMassCharStart < 0 || _summaryMassCharLength <= 0 || !IsTabActive)
                return;

            if (_summaryMassVertexRefreshDeferred)
            {
                _summaryMassVertexRefreshDeferred = false;
                return;
            }

            if (_inventoryMassOverCapacity)
            {
                _massPulsePhase += deltaTime * 4.2f;
                ApplySummaryMassVertexColor(ResolvePulsedMassColor(_massPulsePhase));
                return;
            }

            _massPulsePhase = 0f;
            ApplySummaryMassVertexColor((Color32)Dim);
        }

        private Color32 ResolvePulsedMassColor(float phase)
        {
            float phase01 = math.frac((phase * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            float pulse = triangle * triangle;
            Color blended = LerpColorClamped(Broken, Missing, pulse * 0.35f);
            blended.a = 0.98f;
            return blended;
        }

        private void ApplySummaryMassVertexColor(Color32 color)
        {
            if (_summaryText == null || _summaryMassCharStart < 0 || _summaryMassCharLength <= 0)
                return;

            if (_appliedSummaryMassColor.Equals(color))
                return;

            TMP_TextInfo textInfo = _summaryText.textInfo;
            if (textInfo == null || textInfo.characterCount <= 0 || _summaryMassCharStart >= textInfo.characterCount)
                return;

            int end = math.min(textInfo.characterCount, _summaryMassCharStart + _summaryMassCharLength);
            for (int i = _summaryMassCharStart; i < end; i++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
                if (!characterInfo.isVisible)
                    continue;

                int meshIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                Color32[] colors = textInfo.meshInfo[meshIndex].colors32;
                colors[vertexIndex] = color;
                colors[vertexIndex + 1] = color;
                colors[vertexIndex + 2] = color;
                colors[vertexIndex + 3] = color;
            }

            _summaryText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            _appliedSummaryMassColor = color;
        }

        private static Color LerpColorClamped(Color from, Color to, float t)
        {
            float u = math.saturate(t);
            return new Color(
                from.r + (to.r - from.r) * u,
                from.g + (to.g - from.g) * u,
                from.b + (to.b - from.b) * u,
                from.a + (to.a - from.a) * u);
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

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size,
            FontStyles style, TextAlignmentOptions alignment)
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
            LocalizedTMPAutoSizer.Configure(text, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void CreateRule(RectTransform parent, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = new Vector2(0.08f, 1f);
            rect.anchorMax = new Vector2(0.92f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image image = EnsureImage(rect.gameObject);
            image.color = Rule;
            image.raycastTarget = false;
        }

        private static void CreateInnerRule(RectTransform parent, float yAnchor)
        {
            RectTransform rect = CreateRect(parent, "InnerRule");
            rect.anchorMin = new Vector2(0.06f, yAnchor);
            rect.anchorMax = new Vector2(0.94f, yAnchor);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 1f);
            Image image = EnsureImage(rect.gameObject);
            image.color = Rule;
            image.raycastTarget = false;
        }

        private static void SetLiteralText(TMP_Text text, ReadOnlySpan<char> value)
        {
            if (text == null)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                if (value.Length > lease.Buffer.Length)
                    return;

                value.CopyTo(lease.Buffer.AsSpan());
                text.SetCharArray(lease.Buffer, 0, value.Length);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void SetUpperText(TMP_Text text, string value, bool replaceUnderscores)
        {
            if (text == null || string.IsNullOrEmpty(value))
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                ReadOnlySpan<char> source = value.AsSpan();
                if (source.Length > lease.Buffer.Length)
                    return;

                Span<char> destination = lease.Buffer.AsSpan(0, source.Length);
                for (int i = 0; i < source.Length; i++)
                {
                    char character = source[i];
                    destination[i] = replaceUnderscores && character == '_' ? ' ' : ToAsciiUpperInvariant(character);
                }

                text.SetCharArray(lease.Buffer, 0, source.Length);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void SetPrefixedUpperText(TMP_Text text, ReadOnlySpan<char> prefix, string value)
        {
            if (text == null)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                ReadOnlySpan<char> source = string.IsNullOrEmpty(value) ? ReadOnlySpan<char>.Empty : value.AsSpan();
                int totalLength = prefix.Length + source.Length;
                if (totalLength > lease.Buffer.Length)
                    return;

                Span<char> destination = lease.Buffer.AsSpan();
                prefix.CopyTo(destination);
                for (int i = 0; i < source.Length; i++)
                    destination[prefix.Length + i] = ToAsciiUpperInvariant(source[i]);

                text.SetCharArray(lease.Buffer, 0, totalLength);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private void SetHintText(int assigned, int ready, int missing, int broken, string recommendedPresetDirective)
        {
            if (_hintText == null || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> destination = lease.Buffer.AsSpan();
                int index = 0;
                Append(destination, ref index, GetLoadoutDirective(assigned, ready, missing, broken));
                Append(destination, ref index, "  LIVE: ".AsSpan());
                if (toolManager != null &&
                    toolManager.TryWriteCurrentToolOperationalDirective(destination.Slice(index), out int directiveLength))
                    index += directiveLength;
                Append(destination, ref index, "  IDENTITY: ".AsSpan());
                Append(destination, ref index, GetActiveExpressionSummary());

                string identityLoadout = GetActiveExpressionLoadoutName();
                if (!string.IsNullOrWhiteSpace(identityLoadout))
                {
                    Append(destination, ref index, "  IDENTITY KIT: ".AsSpan());
                    AppendUpper(destination, ref index, identityLoadout);
                }

                string identitySuit = GetActiveExpressionSuitName();
                if (!string.IsNullOrWhiteSpace(identitySuit))
                {
                    Append(destination, ref index, "  IDENTITY SHELL: ".AsSpan());
                    AppendUpper(destination, ref index, identitySuit);

                    if (!IsExpressionSuitApplied())
                        Append(destination, ref index, " (PENDING SYNC)".AsSpan());
                }

                Append(destination, ref index, "  FIELD: ".AsSpan());
                Append(destination, ref index, recommendedPresetDirective);
                _hintText.SetCharArray(lease.Buffer, 0, index);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void SetPresetBodyText(TMP_Text text, ToolLoadoutPreset preset, int readyCount, bool matched)
        {
            if (text == null || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> destination = lease.Buffer.AsSpan();
                int index = 0;
                AppendPresetBrief(destination, ref index, preset);
                Append(destination, ref index, "\nREADY NOW ".AsSpan());
                AppendInt(destination, ref index, readyCount);
                Append(destination, ref index, "/4".AsSpan());
                if (matched)
                    Append(destination, ref index, " | ACTIVE".AsSpan());

                text.SetCharArray(lease.Buffer, 0, index);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void AppendPresetBrief(Span<char> destination, ref int index, ToolLoadoutPreset preset)
        {
            if (preset == null)
            {
                Append(destination, ref index, "NO DATA".AsSpan());
                return;
            }

            ReadOnlySpan<char> description = string.IsNullOrEmpty(preset.description)
                ? ReadOnlySpan<char>.Empty
                : preset.description.AsSpan();
            if (!HasNonWhiteSpace(description))
            {
                Append(destination, ref index, "Standard expedition slot map.".AsSpan());
                return;
            }

            int end = description.Length;
            int newline = IndexOfLineBreak(description);
            if (newline >= 0)
                end = newline;

            bool truncated = end > 44;
            if (truncated)
                end = 44;

            while (end > 0 && char.IsWhiteSpace(description[end - 1]))
                end--;

            Append(destination, ref index, description.Slice(0, end));
            if (truncated)
                Append(destination, ref index, "...".AsSpan());
        }

        private static int IndexOfLineBreak(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                    return i;
            }

            return -1;
        }

        private static bool HasNonWhiteSpace(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        private static void SetSlotHeaderText(TMP_Text text, int slotNumber)
        {
            if (text == null)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> destination = lease.Buffer.AsSpan();
                "SLOT ".AsSpan().CopyTo(destination);
                if (!slotNumber.TryFormat(destination.Slice(5), out int charsWritten))
                    return;

                text.SetCharArray(lease.Buffer, 0, 5 + charsWritten);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static ReadOnlySpan<char> ResolveCategoryLabel(ItemData item)
        {
            if (item == null)
                return "TOOL".AsSpan();

            switch (item.category)
            {
                case ItemCategory.Miscellaneous:
                    return "MISCELLANEOUS".AsSpan();
                case ItemCategory.Material:
                    return "MATERIAL".AsSpan();
                case ItemCategory.Tool:
                    return "TOOL".AsSpan();
                case ItemCategory.Equipment:
                    return "EQUIPMENT".AsSpan();
                case ItemCategory.Consumable:
                    return "CONSUMABLE".AsSpan();
                case ItemCategory.Component:
                    return "COMPONENT".AsSpan();
                default:
                    return "TOOL".AsSpan();
            }
        }

        private static void SetSlotBodyText(TMP_Text text, ReadOnlySpan<char> category, int cargoCount, float weight, float normalizedDurability, float energyPerSecond)
        {
            if (text == null || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> destination = lease.Buffer.AsSpan();
                int index = 0;
                Append(destination, ref index, "CLASS    ".AsSpan());
                Append(destination, ref index, category.IsEmpty ? "TOOL".AsSpan() : category);
                Append(destination, ref index, "\nIN CARGO  ".AsSpan());
                AppendInt(destination, ref index, cargoCount);
                Append(destination, ref index, "\nMASS     ".AsSpan());
                AppendFloat(destination, ref index, weight, "0.0".AsSpan());
                Append(destination, ref index, " kg\nDURAB.   ".AsSpan());
                AppendInt(destination, ref index, Mathf.RoundToInt(Mathf.Clamp01(normalizedDurability) * 100f));
                Append(destination, ref index, "%\nENERGY   ".AsSpan());
                AppendFloat(destination, ref index, energyPerSecond, "0.0".AsSpan());
                Append(destination, ref index, "/s".AsSpan());
                text.SetCharArray(lease.Buffer, 0, index);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void Append(Span<char> destination, ref int index, ReadOnlySpan<char> value)
        {
            if (index >= destination.Length)
                return;

            int copyLength = Mathf.Min(value.Length, destination.Length - index);
            value.Slice(0, copyLength).CopyTo(destination.Slice(index));
            index += copyLength;
        }

        private static void Append(Span<char> destination, ref int index, string value)
        {
            if (string.IsNullOrEmpty(value) || index >= destination.Length)
                return;

            ReadOnlySpan<char> source = value.AsSpan();
            int copyLength = Mathf.Min(source.Length, destination.Length - index);
            source.Slice(0, copyLength).CopyTo(destination.Slice(index));
            index += copyLength;
        }

        private static void AppendUpper(Span<char> destination, ref int index, string value)
        {
            if (string.IsNullOrEmpty(value) || index >= destination.Length)
                return;

            ReadOnlySpan<char> source = value.AsSpan();
            int copyLength = Mathf.Min(source.Length, destination.Length - index);
            Span<char> target = destination.Slice(index, copyLength);
            for (int i = 0; i < copyLength; i++)
                target[i] = ToAsciiUpperInvariant(source[i]);

            index += copyLength;
        }

        private static void AppendUpperInvariant(ref FixedCharBuffer buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            Span<char> scratch = stackalloc char[1];
            for (int i = 0; i < value.Length; i++)
            {
                scratch[0] = ToAsciiUpperInvariant(value[i]);
                if (!buffer.Append(scratch))
                    return;
            }
        }

        private static char ToAsciiUpperInvariant(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - 32) : value;
        }

        private static void AppendInt(Span<char> destination, ref int index, int value)
        {
            if (index < destination.Length && value.TryFormat(destination.Slice(index), out int charsWritten))
                index += charsWritten;
        }

        private static void AppendFloat(Span<char> destination, ref int index, float value, ReadOnlySpan<char> format)
        {
            if (index < destination.Length && value.TryFormat(destination.Slice(index), out int charsWritten, format))
                index += charsWritten;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDALoadoutSlotActionButton : MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private PDALoadoutTab _tab;
        private int _slotIndex;
        private bool _clearAssignment;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDALoadoutTab tab, int slotIndex, bool clearAssignment, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _slotIndex = slotIndex;
            _clearAssignment = clearAssignment;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _tab?.InvokeSlotAction(_slotIndex, _clearAssignment);
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDALoadoutPresetButton : MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private PDALoadoutTab _tab;
        private int _presetIndex;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDALoadoutTab tab, int presetIndex, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _presetIndex = presetIndex;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _tab?.InvokePresetAction(_presetIndex);
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDALoadoutRecommendedButton : MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private PDALoadoutTab _tab;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDALoadoutTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _tab?.InvokeRecommendedPresetAction();
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDALoadoutIdentityButton : MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private PDALoadoutTab _tab;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDALoadoutTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _tab?.InvokeIdentityCycleAction();
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }
}
