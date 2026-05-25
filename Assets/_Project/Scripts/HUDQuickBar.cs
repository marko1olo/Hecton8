// ============================================================================
// HECTON-8 — HUDQuickBar.cs
// Kompaktnaya poloska bystrogo dostupa (4 tool slots) na HUD.
// Sibling k HUD_V4_CanvasRoot na Suit_HUD_Canvas.
// ============================================================================

using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton.Localization;
using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/HUD Quick Bar")]
    public sealed class HUDQuickBar : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private TMP_FontAsset font;

        [Header("── Layout ────────────────────────────────────")]
        [SerializeField] private float slotSize = 44f;
        [SerializeField] private float slotGap = 3f;
        [SerializeField] private Vector2 barOffset = new Vector2(0f, 96f);

        // ══════════════════════════════════════════════════════════
        //  COLORS
        // ══════════════════════════════════════════════════════════

        private static readonly Color SlotBg = new Color(0.04f, 0.1f, 0.12f, 0.55f);
        private static readonly Color SlotActive = new Color(0.46f, 0.98f, 0.94f, 0.25f);
        private static readonly Color KeyDim = new Color(0.5f, 0.7f, 0.68f, 0.45f);
        private static readonly Color KeyActive = new Color(0.46f, 0.98f, 0.94f, 0.85f);
        private static readonly Color IconHidden = new Color(1f, 1f, 1f, 0f);
        private static readonly Color IconUnavailable = new Color(1f, 1f, 1f, 0.2f);
        private static readonly Color DurGood    = new Color(0.3f, 0.9f, 0.85f, 0.7f);
        private static readonly Color DurWarning = new Color(1f, 0.74f, 0.22f, 0.7f);
        private static readonly Color DurHidden = new Color(0.3f, 0.9f, 0.85f, 0f);
        private static readonly Color SummaryColor = new Color(0.9f, 0.98f, 1f, 0.94f);
        private static readonly Color DirectiveColor = new Color(0.64f, 0.83f, 0.88f, 0.92f);
        // COLD ALLOC: string[4] — cached slot key labels — owner: HUDQuickBar
        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        private const int SlotCount = 4;
        private const float FieldAdviceRefreshInterval = 0.35f;
        private const float AutoResolveRetryInterval = 0.5f;
        private const float FadeSharpness = 8f;
        private const float FadeBlendDenominatorFloor = 0.0001f;
        private const float PadeOneTwelfth = 0.0833333333f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private bool _built;
        private RectTransform _barRoot;
        private Image[] _slotBgs;
        private Image[] _slotIcons;
        private TextMeshProUGUI[] _slotKeys;
        private Image[] _durBars;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _toolSummary;
        private TextMeshProUGUI _toolDirective;
        private float _nextStatusRefreshAt;
        private bool[] _slotIconVisible;
        private bool[] _slotIconAvailable;
        private Sprite[] _slotIconSprites;
        private bool[] _slotDurVisible;
        private float[] _slotDurWidths;
        private int _lastSummaryHash;
        private int _lastSummaryLength = -1;
        private int _lastDirectiveHash;
        private int _lastDirectiveLength = -1;
        private bool _cachedDirectiveHasAdvice;
        private string _cachedDirectiveAdvicePreset;
        private float _nextFieldAdviceRefreshAt;
        private bool _registeredToTickManager;
        private bool _registeredToLateFrame;
        private bool _slotVisualsDirty;
        private bool _statusDirty;
        private bool _presentationDirty;
        private bool _alphaDirty;
        private float _pendingCanvasAlpha = 1f;
        private IPlayerInventoryService _inventoryService;
        private PlayerInventory _playerInventory;
        private ItemCatalog _itemCatalog;
        private IToolDurabilityService _toolDurabilitySystem;
        private PlayerToolManager _subscribedToolManager;
        private uint _toolLoadoutSignalSourceId;
        private uint _lastToolLoadoutSignalSequence;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;
        private bool _hotSwapRegistered;
        private readonly int[] _slotItemHashCache = new int[SlotCount]; // COLD ALLOC: int[4] - quickbar resolved item hash cache - owner: HUDQuickBar
        private readonly bool[] _slotItemHashResolved = new bool[SlotCount]; // COLD ALLOC: bool[4] - quickbar item hash cache validity flags - owner: HUDQuickBar
        private int _lastInventoryVersion = -1;
        [SerializeField] private float fieldAdviceRange = 18f;
        [SerializeField] private LayerMask fieldAdviceMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            AutoResolve();
            _nextAutoResolveAttemptTime = Time.unscaledTime + AutoResolveRetryInterval;
            EnsureBuilt();
            Subscribe();
            MarkAllDirty();
            Refresh(forceStatus: true);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            Unsubscribe();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerInventory:
                    ApplyInventoryService(currentService as IPlayerInventoryService);
                    RefreshToolManagerSubscription();
                    MarkAllDirty();
                    break;
                case GlobalRegistryServiceSlot.ToolDurabilityRuntime:
                    _toolDurabilitySystem = currentService as IToolDurabilityService;
                    _slotVisualsDirty = true;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    RegisterToTickManager();
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            TryAutoResolveForTick();

            ConsumeToolLoadoutChangedSignals();
            ConsumeDurabilityChangedSignals();

            if (_playerInventory != null && _lastInventoryVersion != _playerInventory.InventoryVersion)
            {
                _lastInventoryVersion = _playerInventory.InventoryVersion;
                _slotVisualsDirty = true;
            }

            if (_canvasGroup != null)
            {
                float target = PlayerPDA.IsOpen ? 0.15f : 1f;
                _pendingCanvasAlpha = math.lerp(_canvasGroup.alpha, target, ResolveFadeBlend01(deltaTime));
                _alphaDirty = true;
            }

            _presentationDirty = true;
        }

        public void LateFrameTick()
        {
            if (_alphaDirty)
            {
                _alphaDirty = false;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = _pendingCanvasAlpha;
            }

            if (!_presentationDirty)
                return;

            _presentationDirty = false;
            Refresh();
        }

        private void RegisterToTickManager()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTickManager)
                _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredToLateFrame)
                _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredToLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredToLateFrame = false;
            }

            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTickManager = false;
            }

            _presentationDirty = false;
            _alphaDirty = false;
        }

        // ══════════════════════════════════════════════════════════
        //  AUTO-RESOLVE
        // ══════════════════════════════════════════════════════════

        private void AutoResolve()
        {
            ApplyInventoryService(_inventoryService);

            if (_inventoryService != null)
            {
                _playerInventory = _inventoryService.Inventory;
                _itemCatalog = _playerInventory != null ? _playerInventory.ItemCatalog : null;
                if (toolManager == null)
                    toolManager = _inventoryService.ToolManager;
            }

            if (toolManager == null)
            {
                IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
                if (playerContext != null && playerContext.ToolManager != null)
                {
                    toolManager = playerContext.ToolManager;
                }
                else if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                         playerTransform != null &&
                         playerTransform.TryGetComponent(out PlayerToolManager resolvedToolManager))
                {
                    toolManager = resolvedToolManager;
                }
            }
            if (font == null)
                font = TMP_Settings.defaultFontAsset;

            RefreshToolLoadoutSignalBinding();
        }

        private void TryAutoResolveForTick()
        {
            float now = Time.unscaledTime;
            if (now < _nextAutoResolveAttemptTime)
                return;

            if (!NeedsAutoResolve())
                return;

            _nextAutoResolveAttemptTime = now + AutoResolveRetryInterval;
            AutoResolve();
            RefreshToolManagerSubscription();
        }

        private bool NeedsAutoResolve()
        {
            if (font == null || toolManager == null || _inventoryService == null || _playerInventory == null)
                return true;

            return false;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        private void Subscribe()
        {
            RefreshToolManagerSubscription();
        }

        private void Unsubscribe()
        {
            UnsubscribeToolManager(_subscribedToolManager);
        }

        private void RefreshToolManagerSubscription()
        {
            if (ReferenceEquals(_subscribedToolManager, toolManager))
                return;

            UnsubscribeToolManager(_subscribedToolManager);
            SubscribeToolManager(toolManager);
            RefreshToolLoadoutSignalBinding();
            _slotVisualsDirty = true;
            _statusDirty = true;
        }

        private void SubscribeToolManager(PlayerToolManager manager)
        {
            if (manager == null || ReferenceEquals(_subscribedToolManager, manager))
                return;

            _subscribedToolManager = manager;
            RefreshToolLoadoutSignalBinding();
        }

        private void UnsubscribeToolManager(PlayerToolManager manager)
        {
            if (manager == null)
                return;

            if (ReferenceEquals(_subscribedToolManager, manager))
                _subscribedToolManager = null;
        }

        private void MarkAllDirty()
        {
            _slotVisualsDirty = true;
            _statusDirty = true;
            _nextStatusRefreshAt = 0f;
            _nextFieldAdviceRefreshAt = 0f;
            InvalidateSlotBindingCache();
        }

        private void MarkToolLoadoutDirty(bool invalidateAssignments)
        {
            if (invalidateAssignments)
                InvalidateSlotBindingCache();

            _slotVisualsDirty = true;
            _statusDirty = true;
            _nextStatusRefreshAt = 0f;
            _presentationDirty = true;
        }

        private bool ConsumeToolLoadoutChangedSignals()
        {
            uint sourceId = _toolLoadoutSignalSourceId;
            if (sourceId == 0u)
                return false;

            bool dirty = false;
            bool assignmentsDirty = false;
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
                assignmentsDirty |= signal.Reason == ToolLoadoutChangedSignal.ReasonAssignmentsChanged;
            }

            if (dirty)
                MarkToolLoadoutDirty(assignmentsDirty);

            return dirty;
        }

        private bool ConsumeDurabilityChangedSignals()
        {
            bool dirty = false;
            ReadOnlySpan<ItemDurabilityChangedSignal> signals = SignalBus<ItemDurabilityChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ItemDurabilityChangedSignal signal = ref signals[i];
                if (signal.InventoryHash != 0u)
                    continue;

                dirty = true;
                break;
            }

            if (dirty)
            {
                _slotVisualsDirty = true;
                _statusDirty = true;
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

        // ══════════════════════════════════════════════════════════
        //  BUILD
        // ══════════════════════════════════════════════════════════

        private void EnsureBuilt()
        {
            if (_built) return;

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            // Anchor to bottom-center
            self.anchorMin = new Vector2(0.5f, 0f);
            self.anchorMax = new Vector2(0.5f, 0f);
            self.pivot = new Vector2(0.5f, 0f);

            float totalW = SlotCount * slotSize + (SlotCount - 1) * slotGap;
            self.sizeDelta = new Vector2(totalW, slotSize + 44f);
            self.anchoredPosition = barOffset;

            // Canvas group for fade
            if (!gameObject.TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _durBars = new Image[SlotCount]; // COLD ALLOC: Image[4] - quickbar durability bar refs - owner: HUDQuickBar
            _slotBgs = new Image[SlotCount]; // COLD ALLOC: Image[4] - quickbar slot background refs - owner: HUDQuickBar
            _slotIcons = new Image[SlotCount]; // COLD ALLOC: Image[4] - quickbar slot icon refs - owner: HUDQuickBar
            _slotKeys = new TextMeshProUGUI[SlotCount]; // COLD ALLOC: TextMeshProUGUI[4] - quickbar key label refs - owner: HUDQuickBar
            _slotIconVisible = new bool[SlotCount]; // COLD ALLOC: bool[4] - quickbar icon visibility cache - owner: HUDQuickBar
            _slotIconAvailable = new bool[SlotCount]; // COLD ALLOC: bool[4] - quickbar icon availability cache - owner: HUDQuickBar
            _slotIconSprites = new Sprite[SlotCount]; // COLD ALLOC: Sprite[4] - quickbar icon sprite cache - owner: HUDQuickBar
            _slotDurVisible = new bool[SlotCount]; // COLD ALLOC: bool[4] - quickbar durability visibility cache - owner: HUDQuickBar
            _slotDurWidths = new float[SlotCount]; // COLD ALLOC: float[4] - quickbar durability width cache - owner: HUDQuickBar

            for (int i = 0; i < SlotCount; i++)
            {
                RectTransform slot = MakeRect("Slot_" + i, self);
                slot.pivot = new Vector2(0f, 0f);
                slot.anchorMin = new Vector2(0f, 0f);
                slot.anchorMax = new Vector2(0f, 0f);
                slot.anchoredPosition = new Vector2(i * (slotSize + slotGap), 30f);
                slot.sizeDelta = new Vector2(slotSize, slotSize);

                Image bg = slot.gameObject.AddComponent<Image>();
                bg.color = SlotBg;
                bg.raycastTarget = false;
                _slotBgs[i] = bg;

                // Icon
                RectTransform iconR = MakeRect("Icon", slot);
                iconR.anchorMin = Vector2.zero;
                iconR.anchorMax = Vector2.one;
                iconR.offsetMin = new Vector2(7f, 7f);
                iconR.offsetMax = new Vector2(-7f, -7f);
                Image icon = iconR.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = IconHidden;
                _slotIcons[i] = icon;

                // Key label
                RectTransform keyR = MakeRect("Key", slot);
                keyR.anchorMin = new Vector2(0f, 1f);
                keyR.anchorMax = new Vector2(0f, 1f);
                keyR.pivot = new Vector2(0f, 1f);
                keyR.anchoredPosition = new Vector2(3f, -2f);
                keyR.sizeDelta = new Vector2(14f, 12f);
                TextMeshProUGUI keyTxt = keyR.gameObject.AddComponent<TextMeshProUGUI>();
                keyTxt.font = font;
                keyTxt.fontSize = 10f;
                keyTxt.fontStyle = FontStyles.Bold;
                keyTxt.alignment = TextAlignmentOptions.TopLeft;
                keyTxt.textWrappingMode = TextWrappingModes.NoWrap;
                keyTxt.raycastTarget = false;
                SetSlotKeyText(keyTxt, i);
                keyTxt.color = KeyDim;
                _slotKeys[i] = keyTxt;
                // Durability bar
                RectTransform durR = MakeRect("Dur", slot);
                durR.pivot = new Vector2(0f, 0f);
                durR.anchorMin = new Vector2(0f, 0f);
                durR.anchorMax = new Vector2(0f, 0f);
                durR.anchoredPosition = new Vector2(3f, 2f);
                durR.sizeDelta = new Vector2(slotSize - 6f, 2f);
                Image durImg = durR.gameObject.AddComponent<Image>();
                durImg.color = DurHidden;
                durImg.raycastTarget = false;
                durImg.rectTransform.sizeDelta = new Vector2(0f, 2f);
                _durBars[i] = durImg;
            }

            RectTransform summaryR = MakeRect("ToolSummary", self);
            summaryR.anchorMin = new Vector2(0f, 0f);
            summaryR.anchorMax = new Vector2(1f, 0f);
            summaryR.pivot = new Vector2(0.5f, 0f);
            summaryR.anchoredPosition = new Vector2(0f, 14f);
            summaryR.sizeDelta = new Vector2(0f, 16f);
            _toolSummary = summaryR.gameObject.AddComponent<TextMeshProUGUI>();
            _toolSummary.font = font;
            _toolSummary.fontSize = 11f;
            _toolSummary.fontStyle = FontStyles.Bold;
            _toolSummary.alignment = TextAlignmentOptions.Center;
            _toolSummary.textWrappingMode = TextWrappingModes.NoWrap;
            _toolSummary.color = SummaryColor;
            _toolSummary.raycastTarget = false;

            RectTransform directiveR = MakeRect("ToolDirective", self);
            directiveR.anchorMin = new Vector2(0f, 0f);
            directiveR.anchorMax = new Vector2(1f, 0f);
            directiveR.pivot = new Vector2(0.5f, 0f);
            directiveR.anchoredPosition = new Vector2(0f, 0f);
            directiveR.sizeDelta = new Vector2(0f, 14f);
            _toolDirective = directiveR.gameObject.AddComponent<TextMeshProUGUI>();
            _toolDirective.font = font;
            _toolDirective.fontSize = 10f;
            _toolDirective.fontStyle = FontStyles.Normal;
            _toolDirective.alignment = TextAlignmentOptions.Center;
            _toolDirective.textWrappingMode = TextWrappingModes.NoWrap;
            _toolDirective.color = DirectiveColor;
            _toolDirective.raycastTarget = false;

            _built = true;
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void Refresh(bool forceStatus = false)
        {
            if (toolManager == null || _slotBgs == null)
                return;

            if (_slotVisualsDirty)
            {
                RefreshSlotVisuals();
                _slotVisualsDirty = false;
            }

            bool shouldPollStatus = toolManager.CurrentTool != null && Time.unscaledTime >= _nextStatusRefreshAt;
            if (!forceStatus && !_statusDirty && !shouldPollStatus)
                return;

            RefreshStatusText();
            _statusDirty = false;
            _nextStatusRefreshAt = Time.unscaledTime + 0.15f;
        }

        private void RefreshSlotVisuals()
        {
            int activeSlot = toolManager.CurrentSlotIndex;
            for (int i = 0; i < SlotCount; i++)
                RefreshSlotVisuals(i, activeSlot);
        }

        private void RefreshSlotVisuals(int slotIndex, int activeSlot)
        {
            bool isActive = slotIndex == activeSlot;
            Color desiredSlotBackground = isActive ? SlotActive : SlotBg;
            if (_slotBgs[slotIndex].color != desiredSlotBackground)
                _slotBgs[slotIndex].color = desiredSlotBackground;

            Color desiredKeyColor = isActive ? KeyActive : KeyDim;
            if (_slotKeys[slotIndex].color != desiredKeyColor)
                _slotKeys[slotIndex].color = desiredKeyColor;

            GameObject prefab = toolManager.GetAssignedToolPrefab(slotIndex);
            int itemHashId = ResolveSlotItemHash(slotIndex, prefab);
            Sprite desiredSprite = ResolveSlotIconSprite(prefab);
            bool hasRuntimeDescriptor = TryResolveRuntimeDescriptor(itemHashId, out _);
            bool available = itemHashId != 0 && IsInventoryHashAvailable(itemHashId);
            if (!available)
                available = toolManager.IsToolAvailableInSlot(slotIndex);

            if (prefab != null && desiredSprite != null && hasRuntimeDescriptor)
            {
                if (!ReferenceEquals(_slotIconSprites[slotIndex], desiredSprite))
                {
                    _slotIcons[slotIndex].sprite = desiredSprite;
                    _slotIconSprites[slotIndex] = desiredSprite;
                }

                if (!_slotIconVisible[slotIndex] || _slotIconAvailable[slotIndex] != available)
                {
                    _slotIcons[slotIndex].color = available ? Color.white : IconUnavailable;
                    _slotIconVisible[slotIndex] = true;
                    _slotIconAvailable[slotIndex] = available;
                }
            }
            else if (_slotIconVisible[slotIndex] || _slotIconSprites[slotIndex] != null)
            {
                _slotIcons[slotIndex].sprite = null;
                _slotIcons[slotIndex].color = IconHidden;
                _slotIconSprites[slotIndex] = null;
                _slotIconVisible[slotIndex] = false;
                _slotIconAvailable[slotIndex] = false;
            }

            RefreshDurabilityVisual(slotIndex, prefab);
        }

        private void RefreshDurabilityVisual(int slotIndex, GameObject prefab)
        {
            if (_durBars == null || slotIndex >= _durBars.Length || _durBars[slotIndex] == null)
                return;

            bool showDurability = false;
            float desiredWidth = 0f;
            Color desiredColor = DurHidden;

            if (prefab != null && prefab.TryGetComponent(out IPlayerToolDataReadModel tool) && tool.Metadata != null)
            {
                IToolDurabilityService durabilitySystem = _toolDurabilitySystem;
                if (durabilitySystem != null)
                {
                    float maxDurability = tool.Metadata.maxDurability;
                    if (maxDurability > 0f)
                    {
                        float currentDurability = durabilitySystem.GetDurability(tool.Metadata.toolID, maxDurability);
                        float normalizedDurability = math.saturate(currentDurability / maxDurability);
                        desiredWidth = (slotSize - 6f) * normalizedDurability;
                        desiredColor = FastLerpColor(DurWarning, DurGood, normalizedDurability);
                        showDurability = true;
                    }
                }
            }

            if (_slotDurVisible[slotIndex] != showDurability || math.abs(_slotDurWidths[slotIndex] - desiredWidth) > 0.05f)
            {
                _durBars[slotIndex].rectTransform.sizeDelta = new Vector2(desiredWidth, 2f);
                _slotDurWidths[slotIndex] = desiredWidth;
                _slotDurVisible[slotIndex] = showDurability;
            }

            if (_slotDurVisible[slotIndex])
            {
                if (_durBars[slotIndex].color != desiredColor)
                    _durBars[slotIndex].color = desiredColor;
            }
            else if (_durBars[slotIndex].color != DurHidden)
            {
                _durBars[slotIndex].color = DurHidden;
            }
        }

        private void InvalidateSlotBindingCache()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _slotItemHashCache[i] = 0;
                _slotItemHashResolved[i] = false;
            }
        }

        private int ResolveSlotItemHash(int slotIndex, GameObject prefab)
        {
            if ((uint)slotIndex >= (uint)SlotCount)
                return 0;

            if (_slotItemHashResolved[slotIndex])
                return _slotItemHashCache[slotIndex];

            int itemHashId = 0;
            if (prefab != null &&
                prefab.TryGetComponent(out IPlayerToolDataReadModel tool) &&
                tool.ToolData != null)
            {
                string persistentId = tool.ToolData.PersistentId;
                if (!string.IsNullOrWhiteSpace(persistentId))
                    itemHashId = LocHash.Compute(persistentId);
            }

            _slotItemHashCache[slotIndex] = itemHashId;
            _slotItemHashResolved[slotIndex] = true;
            return itemHashId;
        }

        private Sprite ResolveSlotIconSprite(GameObject prefab)
        {
            if (prefab == null || !prefab.TryGetComponent(out IPlayerToolDataReadModel tool) || tool.ToolData == null)
                return null;

            return tool.ToolData.icon;
        }

        private bool TryResolveRuntimeDescriptor(int itemHashId, out ItemCatalog.ItemRuntimeDescriptor runtimeDescriptor)
        {
            runtimeDescriptor = default;
            return itemHashId != 0 &&
                   _itemCatalog != null &&
                   _itemCatalog.TryGetRuntimeDescriptor(itemHashId, out runtimeDescriptor);
        }

        private bool IsInventoryHashAvailable(int itemHashId)
        {
            if (itemHashId == 0 || _playerInventory == null)
                return false;

            InventoryGrid grid = _playerInventory.Grid;
            if (grid == null)
                return false;

            NativeArray<int>.ReadOnly anchorHashIds = grid.AnchorHashIds;
            NativeArray<ushort>.ReadOnly stackCounts = _playerInventory.GetStackCountsReadOnly();
            if (!anchorHashIds.IsCreated || !stackCounts.IsCreated)
                return false;

            int anchorCount = math.min(anchorHashIds.Length, stackCounts.Length);
            for (int anchorIndex = 0; anchorIndex < anchorCount; anchorIndex++)
            {
                if (anchorHashIds[anchorIndex] == itemHashId && stackCounts[anchorIndex] > 0)
                    return true;
            }

            return false;
        }

        private void RefreshStatusText()
        {
            if (_toolSummary != null)
            {
                if (toolManager != null && CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                {
                    try
                    {
                        if (!toolManager.TryWriteCurrentToolOperationalSummary(lease.Buffer.AsSpan(), out int length))
                            length = 0;

                        int hash = ComputeCharHash(lease.Buffer, length);
                        if (_lastSummaryHash != hash || _lastSummaryLength != length)
                        {
                            _toolSummary.SetCharArray(lease.Buffer, 0, length);
                            _lastSummaryHash = hash;
                            _lastSummaryLength = length;
                        }
                    }
                    finally
                    {
                        CharBufferPool.Release(lease);
                    }
                }
            }

            if (_toolDirective == null)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease directiveLease))
                return;

            try
            {
                Span<char> destination = directiveLease.Buffer.AsSpan();
                int length = 0;
                if (toolManager == null ||
                    !toolManager.TryWriteCurrentToolOperationalDirective(destination, out length))
                {
                    Append(directiveLease.Buffer, ref length, "Arm a tool from quick slots or PDA loadout.");
                }

                if (TryGetCachedAdvicePreset(out string advicePreset))
                {
                    Append(directiveLease.Buffer, ref length, "  KIT ");
                    Append(directiveLease.Buffer, ref length, advicePreset);
                }

                int hash = ComputeCharHash(directiveLease.Buffer, length);
                if (_lastDirectiveHash != hash || _lastDirectiveLength != length)
                {
                    _toolDirective.SetCharArray(directiveLease.Buffer, 0, length);
                    _lastDirectiveHash = hash;
                    _lastDirectiveLength = length;
                }
            }
            finally
            {
                CharBufferPool.Release(directiveLease);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private RectTransform MakeRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.TryGetComponent(out RectTransform r);
            r.SetParent(parent, false);
            r.localScale = Vector3.one;
            if (parent != null) go.layer = parent.gameObject.layer;
            return r;
        }

        private static void SetSlotKeyText(TMP_Text text, int slotIndex)
        {
            if (text == null)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                lease.Buffer[0] = (char)('1' + math.clamp(slotIndex, 0, SlotCount - 1));
                text.SetCharArray(lease.Buffer, 0, 1);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void SetPooledText(TMP_Text text, string value)
        {
            if (text == null)
                return;

            if (string.IsNullOrEmpty(value))
            {
                text.SetCharArray(System.Array.Empty<char>(), 0, 0);
                return;
            }

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                int length = math.min(value.Length, lease.Buffer.Length);
                value.CopyTo(0, lease.Buffer, 0, length);
                text.SetCharArray(lease.Buffer, 0, length);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static int ComputeCharHash(char[] buffer, int length)
        {
            unchecked
            {
                int hash = (int)2166136261u;
                int safeLength = math.max(0, math.min(length, buffer != null ? buffer.Length : 0));
                for (int i = 0; i < safeLength; i++)
                    hash = (hash ^ buffer[i]) * 16777619;

                return hash ^ safeLength;
            }
        }

        private static float ResolveFadeBlend01(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return 0f;

            float scaled = deltaTime * FadeSharpness;
            float denominator = math.max(FadeBlendDenominatorFloor, 1f + scaled + (scaled * scaled * PadeOneTwelfth));
            return math.saturate(scaled / denominator);
        }

        private static Color FastLerpColor(Color from, Color to, float t)
        {
            t = math.saturate(t);
            return new Color(
                from.r + ((to.r - from.r) * t),
                from.g + ((to.g - from.g) * t),
                from.b + ((to.b - from.b) * t),
                from.a + ((to.a - from.a) * t));
        }

        private void CacheRegistryServicesCold()
        {
            ApplyInventoryService(GlobalRegistry.PlayerInventory);
            _toolDurabilitySystem = GlobalRegistry.ToolDurabilityService;
        }

        private void ApplyInventoryService(IPlayerInventoryService inventoryService)
        {
            if (ReferenceEquals(_inventoryService, inventoryService))
                return;

            _inventoryService = inventoryService;
            _playerInventory = null;
            _itemCatalog = null;
            _lastInventoryVersion = -1;
            InvalidateSlotBindingCache();
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

        private bool TryGetCachedAdvicePreset(out string advicePreset)
        {
            float now = Time.unscaledTime;
            if (now >= _nextFieldAdviceRefreshAt)
            {
                _nextFieldAdviceRefreshAt = now + FieldAdviceRefreshInterval;
                Transform origin = toolManager != null ? toolManager.transform : null;
                _cachedDirectiveHasAdvice = FieldLoadoutAdvisor.TryBuildForwardPresetName(
                    origin,
                    fieldAdviceRange,
                    fieldAdviceMask,
                    out _cachedDirectiveAdvicePreset);
                if (!_cachedDirectiveHasAdvice)
                    _cachedDirectiveAdvicePreset = null;
            }

            advicePreset = _cachedDirectiveAdvicePreset;
            return _cachedDirectiveHasAdvice && !string.IsNullOrEmpty(advicePreset);
        }

        private static void Append(char[] destination, ref int index, string value)
        {
            if (string.IsNullOrEmpty(value) || index >= destination.Length)
                return;

            int copyLength = math.min(value.Length, destination.Length - index);
            value.CopyTo(0, destination, index, copyLength);
            index += copyLength;
        }
    }
}
