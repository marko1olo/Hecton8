// ============================================================================
// HECTON-8 — PDAConstructionTab.cs
// Dedicated PDA construction tab: module catalog, active buildable, cost/readiness,
// and direct selection flow for the real PlayerBuilder backend.
// ============================================================================

using System;
using Hecton8.Building;
using Hecton8.Bootstrap;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Construction Tab")]
    public sealed class PDAConstructionTab : MonoBehaviour, ITickable, IUpdatable, IPDAEventListener, IQuestEventListener
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.84f);
        private static readonly Color BoxBg = new Color(0.05f, 0.12f, 0.14f, 0.72f);
        private static readonly Color BoxActive = new Color(0.08f, 0.2f, 0.22f, 0.86f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.55f, 0.74f, 0.71f, 0.72f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color Ready = new Color(0.34f, 0.95f, 0.74f, 0.94f);
        private static readonly Color Warn = new Color(1f, 0.75f, 0.28f, 0.94f);
        private static readonly Color Blocked = new Color(1f, 0.45f, 0.4f, 0.94f);
        private const float AutoResolveRetryInterval = 1f;

        [Header("References")]
        [SerializeField] private PlayerBuilder playerBuilder;
        [SerializeField] private ConstructionManager constructionManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Hecton8.Gameplay.PlayerToolManager toolManager;
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private HUDNotification hudNotification;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private int constructionTabIndex = 2;
        [SerializeField] private int maxVisibleCards = 6;
        [SerializeField, Range(0.05f, 0.5f)] private float refreshInterval = 0.15f;

        private bool _built;
        private float _refreshTimer;
        private float _autoResolveRetryTimer;
        private TextMeshProUGUI _summaryText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _hintText;
        private TextMeshProUGUI _directiveText;
        private RectTransform _builderActionRoot;
        private Image _builderActionBg;
        private TextMeshProUGUI _builderActionLabel;
        private RectTransform _fieldActionRoot;
        private Image _fieldActionBg;
        private TextMeshProUGUI _fieldActionLabel;
        private RectTransform _deployActionRoot;
        private Image _deployActionBg;
        private TextMeshProUGUI _deployActionLabel;
        private CanvasGroup _builderActionCanvasGroup;
        private CanvasGroup _fieldActionCanvasGroup;
        private RectTransform[] _cardRoots;
        private CanvasGroup[] _cardCanvasGroups;
        private Image[] _cardBgs;
        private TextMeshProUGUI[] _cardTitles;
        private char[][] _cardTitleBuffers;
        private TextMeshProUGUI[] _cardBodies;
        private Image[] _cardButtonBgs;
        private TextMeshProUGUI[] _cardButtonLabels;
        private PDAConstructionSelectButton[] _cardButtons;
        private bool[] _cardVisibility;
        // COLD ALLOC: char[768] - construction summary TMP staging buffer - owner: PDAConstructionTab
        private readonly char[] _summaryBuffer = new char[768];
        // COLD ALLOC: char[512] - construction readiness TMP staging buffer - owner: PDAConstructionTab
        private readonly char[] _statusBuffer = new char[512];
        // COLD ALLOC: char[768] - construction card body TMP staging buffer - owner: PDAConstructionTab
        private readonly char[] _cardBodyBuffer = new char[768];
        // COLD ALLOC: char[512] - construction directive TMP staging buffer - owner: PDAConstructionTab
        private readonly char[] _directiveBuffer = new char[512];
        // COLD ALLOC: char[512] - construction hint TMP staging buffer - owner: PDAConstructionTab
        private readonly char[] _hintBuffer = new char[512];
        // COLD ALLOC: char[96] - construction action label TMP staging buffer - owner: PDAConstructionTab
        private readonly char[] _actionLabelBuffer = new char[96];
        private bool _tickRegistered;
        private bool _pdaEventsRegistered;
        private bool _summaryDirty;
        private bool _catalogDirty;
        private bool _layoutDirty;
        private bool _builderActionVisible;
        private bool _fieldActionVisible;
        private Hecton8.Gameplay.PlayerToolManager _subscribedToolManager;
        private uint _inventorySignalHash;
        private uint _lastInventorySignalRevision;
        private ModuleCatalog _lastCatalog;
        private BuildableData _lastCatalogActiveBuildable;
        private int _lastVisibleCardCount = -1;
        private int _lastCatalogRawCount = -1;
        private int _cachedCatalogCount;
        private int _cachedGeneratorCount;
        private int _cachedConsumerCount;
        private int _cachedPassiveCount;
        private int _cachedStructureCount;
        private int _cachedHabitatCount;
        private int _cachedUtilityCount;
        private int _cachedLogisticsCount;
        private int _cachedFabricationCount;
        private int _cachedDefenseCount;
        private int _cachedLockedBlueprintCount;
        private PDAConstructionBuilderActionButton _builderActionButton;
        private PDAConstructionFieldActionButton _fieldActionButton;
        private bool _questEventsRegistered;
        private readonly char[] _directiveAdviceScratchBuffer = new char[256];
        private FixedCharBuffer _notificationBuffer = new FixedCharBuffer(192); // COLD ALLOC: char[192] - construction PDA HUD notification staging buffer - owner: PDAConstructionTab

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == constructionTabIndex;

        // ══════════════════════════════════════════════════════════
        //  CACHED STRING OPERATIONS — ZERO GC
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            AutoResolveTabIndex();
            AutoResolve(force: true);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            AutoResolveTabIndex();
        }

        private void OnEnable()
        {
            AutoResolve(force: true);
            EnsureBuilt();
            Subscribe();
            MarkAllDirty();
            Refresh(true);
            EvaluateTickRegistration();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnregisterTick();
        }

        private void AutoResolve(float deltaTime = 0f, bool force = false)
        {
            if (!force)
                _autoResolveRetryTimer = Mathf.Max(0f, _autoResolveRetryTimer - Mathf.Max(0f, deltaTime));

            bool missingRuntimeReference =
                playerBuilder == null ||
                playerInventory == null ||
                toolManager == null ||
                playerPDA == null ||
                constructionManager == null ||
                hudNotification == null;

            bool shouldResolveRuntime = missingRuntimeReference && (force || !Application.isPlaying || _autoResolveRetryTimer <= 0f);
            if (!shouldResolveRuntime)
            {
                if (!missingRuntimeReference)
                    _autoResolveRetryTimer = 0f;

                ResolveFontsIfMissing();
                RefreshSubscriptions();
                return;
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerBuilder == null && playerContext != null)
                playerBuilder = playerContext.PlayerBuilder;
            if (playerInventory == null && playerContext != null)
                playerInventory = playerContext.Inventory;
            if (toolManager == null && playerContext != null)
                toolManager = playerContext.ToolManager;
            if (playerPDA == null && playerContext != null)
                playerPDA = playerContext.PlayerPDA;

            if ((!playerBuilder || !playerInventory || !toolManager || !playerPDA) &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerBuilder == null)
                    playerTransform.TryGetComponent(out playerBuilder);

                if (playerInventory == null)
                    playerTransform.TryGetComponent(out playerInventory);

                if (toolManager == null)
                    playerTransform.TryGetComponent(out toolManager);

                if (playerPDA == null)
                    playerTransform.TryGetComponent(out playerPDA);
            }

            if (constructionManager == null)
                constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;

            if (playerPDA == null)
                playerPDA = GetComponentInParent<PlayerPDA>();
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            ResolveFontsIfMissing();
            _autoResolveRetryTimer = Application.isPlaying ? AutoResolveRetryInterval : 0f;

            RefreshSubscriptions();
        }

        private void ResolveFontsIfMissing()
        {
            if (labelFont == null)
                labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            if (numericFont == null)
                numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void AutoResolveTabIndex()
        {
            if (gameObject.name.Contains("Construction", System.StringComparison.OrdinalIgnoreCase))
                constructionTabIndex = 2;
        }

        private void Subscribe()
        {
            RefreshSubscriptions();
            TryRegisterPDAEvents();
            TryRegisterQuestEvents();
        }

        private void Unsubscribe()
        {
            if (_subscribedInventory != null)
            {
                _subscribedInventory.InventoryChanged -= HandleInventoryChanged;
                _subscribedInventory = null;
            }
            if (_subscribedToolManager != null)
            {
                _subscribedToolManager.ActiveSlotChanged -= HandleActiveSlotChanged;
                _subscribedToolManager.ToolAssignmentsChanged -= HandleToolAssignmentsChanged;
                _subscribedToolManager = null;
            }

            UnregisterPDAEvents();
            UnregisterQuestEvents();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            PDAEvents.AssertUnregistered(this, nameof(PDAConstructionTab));
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

        private void TryRegisterQuestEvents()
        {
            if (_questEventsRegistered)
                return;

            QuestEvents.Register(this);
            _questEventsRegistered = true;
        }

        private void UnregisterQuestEvents()
        {
            if (!_questEventsRegistered)
                return;

            QuestEvents.Unregister(this);
            _questEventsRegistered = false;
        }

        private void RefreshSubscriptions()
        {
            RefreshInventorySignalBinding();

            if (!ReferenceEquals(_subscribedToolManager, toolManager))
            {
                if (_subscribedToolManager != null)
                {
                    _subscribedToolManager.ActiveSlotChanged -= HandleActiveSlotChanged;
                    _subscribedToolManager.ToolAssignmentsChanged -= HandleToolAssignmentsChanged;
                }

                _subscribedToolManager = toolManager;
                if (_subscribedToolManager != null)
                {
                    _subscribedToolManager.ActiveSlotChanged += HandleActiveSlotChanged;
                    _subscribedToolManager.ToolAssignmentsChanged += HandleToolAssignmentsChanged;
                }
            }
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

        private void HandleActiveSlotChanged(int _)
        {
            if (!IsTabActive)
                return;

            MarkSummaryDirty();
            Refresh();
        }

        private void HandleToolAssignmentsChanged()
        {
            if (!IsTabActive)
                return;

            MarkSummaryDirty();
            Refresh();
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
                    HandlePdaTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        public void OnQuestEvent(in QuestEventPayload payload)
        {
            QuestEventType eventType = (QuestEventType)payload.EventType;
            if (eventType != QuestEventType.Activated &&
                eventType != QuestEventType.Completed &&
                eventType != QuestEventType.RevertRequested)
            {
                return;
            }

            MarkAllDirty();
            if (IsTabActive)
                Refresh(true);
        }

        private void HandlePdaOpened(int tab)
        {
            if (tab == constructionTabIndex)
            {
                AutoResolve(force: true);
                MarkAllDirty();
                Refresh(true);
                EvaluateTickRegistration();
            }
            else
            {
                UnregisterTick();
            }
        }

        private void HandlePdaClosed(float _)
        {
            UnregisterTick();
        }

        private void HandlePdaTabChanged(int _, int newTab)
        {
            if (newTab == constructionTabIndex)
            {
                AutoResolve(force: true);
                MarkAllDirty();
                Refresh(true);
                EvaluateTickRegistration();
            }
            else
            {
                UnregisterTick();
            }
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            AutoResolve(safeDeltaTime);

            if (!IsTabActive)
            {
                UnregisterTick();
                return;
            }

            UpdateCatalogTracking();
            if (ConsumeInventoryChangedSignals())
                MarkAllDirty();

            _refreshTimer -= safeDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _summaryDirty = true;
                _refreshTimer = Mathf.Max(0.05f, refreshInterval);
            }

            if (_summaryDirty || _catalogDirty)
                Refresh();
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform self = transform as RectTransform;
            if (self == null)
                return;

            ClearChildren(self);

            Image bg = EnsureImage(self.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            title.color = Primary;
            SetLiteralText(title, "CONSTRUCTION MATRIX".AsSpan());

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            SetLiteralText(sub, "module catalog, build cost, placement readiness, and field deployment state".AsSpan());

            CreateRule(self, -52f);

            RectTransform left = CreatePanel(self, "StatusPanel", new Vector2(0f, 0f), new Vector2(0.42f, 1f),
                new Vector2(18f, 18f), new Vector2(-9f, -72f));
            RectTransform right = CreatePanel(self, "CatalogPanel", new Vector2(0.42f, 0f), new Vector2(1f, 1f),
                new Vector2(9f, 18f), new Vector2(-18f, -72f));

            TextMeshProUGUI leftHdr = CreateSectionHeader(left, "BUILD STATUS");
            _summaryText = CreateBody(left, "SummaryText", numericFont);
            Anchor(_summaryText.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f),
                new Vector2(14f, 12f), new Vector2(-14f, -42f));

            CreateInnerRule(left, 0.34f);

            _statusText = CreateBody(left, "StatusText", numericFont);
            Anchor(_statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.34f),
                new Vector2(14f, 40f), new Vector2(-14f, -8f));
            _statusText.fontSize = 11.5f;
            _statusText.color = Primary;

            _directiveText = CreateText(left, "Directive", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            Anchor(_directiveText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(14f, 14f), new Vector2(-14f, 30f));
            _directiveText.textWrappingMode = TextWrappingModes.Normal;
            _directiveText.color = DimLow;

            _builderActionRoot = CreateRect(left, "BuilderAction");
            _builderActionRoot.anchorMin = new Vector2(1f, 0f);
            _builderActionRoot.anchorMax = new Vector2(1f, 0f);
            _builderActionRoot.pivot = new Vector2(1f, 0f);
            _builderActionRoot.anchoredPosition = new Vector2(-14f, 14f);
            _builderActionRoot.sizeDelta = new Vector2(150f, 28f);
            _builderActionCanvasGroup = EnsureCanvasGroup(_builderActionRoot.gameObject);

            _builderActionBg = EnsureImage(_builderActionRoot.gameObject);
            _builderActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            _builderActionBg.raycastTarget = true;

            _builderActionLabel = CreateText(_builderActionRoot, "BuilderActionLabel", numericFont, 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_builderActionLabel.rectTransform);
            _builderActionLabel.color = Dim;
            SetLiteralText(_builderActionLabel, "ARM BUILDER".AsSpan());

            PDAConstructionBuilderActionButton actionButton = _builderActionRoot.gameObject.AddComponent<PDAConstructionBuilderActionButton>();
            actionButton.Init(
                this,
                _builderActionBg,
                new Color(0.08f, 0.16f, 0.18f, 0.58f),
                new Color(0.12f, 0.24f, 0.28f, 0.82f));
            _builderActionButton = actionButton;

            _fieldActionRoot = CreateRect(left, "FieldAction");
            _fieldActionRoot.anchorMin = new Vector2(1f, 0f);
            _fieldActionRoot.anchorMax = new Vector2(1f, 0f);
            _fieldActionRoot.pivot = new Vector2(1f, 0f);
            _fieldActionRoot.anchoredPosition = new Vector2(-170f, 14f);
            _fieldActionRoot.sizeDelta = new Vector2(146f, 28f);
            _fieldActionCanvasGroup = EnsureCanvasGroup(_fieldActionRoot.gameObject);

            _fieldActionBg = EnsureImage(_fieldActionRoot.gameObject);
            _fieldActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            _fieldActionBg.raycastTarget = true;

            _fieldActionLabel = CreateText(_fieldActionRoot, "FieldActionLabel", numericFont, 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_fieldActionLabel.rectTransform);
            _fieldActionLabel.color = Dim;
            SetLiteralText(_fieldActionLabel, "FIELD PREVIEW".AsSpan());

            PDAConstructionFieldActionButton fieldButton = _fieldActionRoot.gameObject.AddComponent<PDAConstructionFieldActionButton>();
            fieldButton.Init(
                this,
                _fieldActionBg,
                new Color(0.08f, 0.16f, 0.18f, 0.58f),
                new Color(0.12f, 0.24f, 0.28f, 0.82f));
            _fieldActionButton = fieldButton;

            _deployActionRoot = CreateRect(left, "DeployAction");
            _deployActionRoot.anchorMin = new Vector2(1f, 0f);
            _deployActionRoot.anchorMax = new Vector2(1f, 0f);
            _deployActionRoot.pivot = new Vector2(1f, 0f);
            _deployActionRoot.anchoredPosition = new Vector2(-326f, 14f);
            _deployActionRoot.sizeDelta = new Vector2(146f, 28f);

            _deployActionBg = EnsureImage(_deployActionRoot.gameObject);
            _deployActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            _deployActionBg.raycastTarget = true;

            _deployActionLabel = CreateText(_deployActionRoot, "DeployActionLabel", numericFont, 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_deployActionLabel.rectTransform);
            _deployActionLabel.color = Dim;
            SetLiteralText(_deployActionLabel, "DEPLOY NOW".AsSpan());

            PDAConstructionDeployActionButton deployButton = _deployActionRoot.gameObject.AddComponent<PDAConstructionDeployActionButton>();
            deployButton.Init(
                this,
                _deployActionBg,
                new Color(0.08f, 0.16f, 0.18f, 0.58f),
                new Color(0.12f, 0.24f, 0.28f, 0.82f));

            TextMeshProUGUI rightHdr = CreateSectionHeader(right, "MODULE CATALOG");
            _ = leftHdr;
            _ = rightHdr;

            _cardRoots = new RectTransform[maxVisibleCards];
            // COLD ALLOC: CanvasGroup[maxVisibleCards] - catalog card visibility cache - owner: PDAConstructionTab
            _cardCanvasGroups = new CanvasGroup[maxVisibleCards];
            _cardBgs = new Image[maxVisibleCards];
            _cardTitles = new TextMeshProUGUI[maxVisibleCards];
            // COLD ALLOC: char[maxVisibleCards][] - catalog card title buffer table - owner: PDAConstructionTab
            _cardTitleBuffers = new char[maxVisibleCards][];
            _cardBodies = new TextMeshProUGUI[maxVisibleCards];
            _cardButtonBgs = new Image[maxVisibleCards];
            _cardButtonLabels = new TextMeshProUGUI[maxVisibleCards];
            _cardButtons = new PDAConstructionSelectButton[maxVisibleCards];
            _cardVisibility = new bool[maxVisibleCards];

            const float cardGap = 10f;
            const float cardHeight = 82f;

            for (int i = 0; i < maxVisibleCards; i++)
            {
                RectTransform card = CreateRect(right, "Card_" + i);
                card.anchorMin = new Vector2(0f, 1f);
                card.anchorMax = new Vector2(1f, 1f);
                card.pivot = new Vector2(0.5f, 1f);
                card.anchoredPosition = new Vector2(0f, -(36f + i * (cardHeight + cardGap)));
                card.sizeDelta = new Vector2(-2f, cardHeight);
                CanvasGroup cardCanvasGroup = EnsureCanvasGroup(card.gameObject);

                Image cardBg = EnsureImage(card.gameObject);
                cardBg.color = BoxBg;
                cardBg.raycastTarget = false;

                RectTransform accent = CreateRect(card, "Accent");
                accent.anchorMin = new Vector2(0f, 0f);
                accent.anchorMax = new Vector2(0f, 1f);
                accent.pivot = new Vector2(0f, 0.5f);
                accent.anchoredPosition = Vector2.zero;
                accent.sizeDelta = new Vector2(4f, 0f);
                Image accentImg = EnsureImage(accent.gameObject);
                accentImg.color = Rule;
                accentImg.raycastTarget = false;

                TextMeshProUGUI cardTitle = CreateText(card, "Title", labelFont, 13f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(16f, -10f), new Vector2(-120f, 18f));
                cardTitle.color = Primary;

                TextMeshProUGUI cardBody = CreateText(card, "Body", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                cardBody.textWrappingMode = TextWrappingModes.Normal;
                Anchor(cardBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                    new Vector2(16f, 10f), new Vector2(-120f, -34f));
                cardBody.color = Dim;

                RectTransform action = CreateRect(card, "Action");
                action.anchorMin = new Vector2(1f, 0.5f);
                action.anchorMax = new Vector2(1f, 0.5f);
                action.pivot = new Vector2(1f, 0.5f);
                action.anchoredPosition = new Vector2(-12f, 0f);
                action.sizeDelta = new Vector2(94f, 28f);

                Image actionBg = EnsureImage(action.gameObject);
                actionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
                actionBg.raycastTarget = true;

                TextMeshProUGUI actionLabel = CreateText(action, "ActionLabel", numericFont, 10f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(actionLabel.rectTransform);
                actionLabel.color = Dim;
                SetLiteralText(actionLabel, "SELECT".AsSpan());

                PDAConstructionSelectButton button = action.gameObject.AddComponent<PDAConstructionSelectButton>();
                button.Init(this, i, actionBg, actionLabel,
                    new Color(0.08f, 0.16f, 0.18f, 0.58f),
                    new Color(0.12f, 0.24f, 0.28f, 0.82f));

                _cardRoots[i] = card;
                _cardCanvasGroups[i] = cardCanvasGroup;
                _cardBgs[i] = cardBg;
                _cardTitles[i] = cardTitle;
                // COLD ALLOC: char[128] - catalog card title uppercase scratch - owner: PDAConstructionTab
                _cardTitleBuffers[i] = new char[128];
                _cardBodies[i] = cardBody;
                _cardButtonBgs[i] = actionBg;
                _cardButtonLabels[i] = actionLabel;
                _cardButtons[i] = button;
                _cardVisibility[i] = true;
            }

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 4f), new Vector2(-24f, 14f));
            _hintText.color = DimLow;
            SetLiteralText(_hintText, "Select a module to arm the Builder Tool. TAB / Q-E still cycle modules in the field.".AsSpan());

            _built = true;
        }

        private void MarkAllDirty()
        {
            _summaryDirty = true;
            _catalogDirty = true;
            _layoutDirty = true;
        }

        private void MarkSummaryDirty()
        {
            _summaryDirty = true;
        }

        private void UpdateCatalogTracking()
        {
            ModuleCatalog catalog = constructionManager != null ? constructionManager.Catalog : null;
            BuildableData active = playerBuilder != null ? playerBuilder.ActiveBuildable : null;
            int rawCount = catalog != null ? catalog.Count : 0;
            int visibleCount = Mathf.Min(rawCount, maxVisibleCards);

            if (!ReferenceEquals(_lastCatalog, catalog) ||
                !ReferenceEquals(_lastCatalogActiveBuildable, active) ||
                _lastCatalogRawCount != rawCount ||
                _lastVisibleCardCount != visibleCount)
            {
                _lastCatalog = catalog;
                _lastCatalogActiveBuildable = active;
                _lastCatalogRawCount = rawCount;
                _lastVisibleCardCount = visibleCount;
                _catalogDirty = true;
            }
        }

        private void Refresh(bool immediate = false)
        {
            if (!_built)
                return;

            UpdateCatalogTracking();

            ModuleCatalog catalog = constructionManager != null ? constructionManager.Catalog : null;
            BuildableData active = playerBuilder != null ? playerBuilder.ActiveBuildable : null;
            int visibleCount = _cachedCatalogCount > 0 ? Mathf.Min(_cachedCatalogCount, maxVisibleCards) : 0;

            if (_catalogDirty)
            {
                RefreshCatalogCache(catalog);
                visibleCount = _cachedCatalogCount > 0 ? Mathf.Min(_cachedCatalogCount, maxVisibleCards) : 0;
            }

            _refreshTimer = Mathf.Max(0.05f, refreshInterval);
            if (_summaryDirty)
                RefreshSummary();
            if (_catalogDirty)
                RefreshCatalog(catalog, active, visibleCount);
            if ((immediate || _layoutDirty) && gameObject.activeSelf)
                LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);

            _summaryDirty = false;
            _catalogDirty = false;
            _layoutDirty = false;
        }

        private void EvaluateTickRegistration()
        {
            if (!isActiveAndEnabled)
            {
                UnregisterTick();
                return;
            }

            if (IsTabActive)
            {
                RegisterTick();
            }
            else
            {
                UnregisterTick();
            }
        }

        private void RegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void RefreshSummary()
        {
            if (_summaryText == null || _statusText == null)
                return;

            ModuleCatalog catalog = constructionManager != null ? constructionManager.Catalog : null;
            BuildableData active = playerBuilder != null ? playerBuilder.ActiveBuildable : null;
            int builtCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            bool hasResources = playerBuilder != null && playerBuilder.HasResourcesForActiveBuildable;
            bool canPlace = playerBuilder != null && playerBuilder.CanPlaceActiveBuildable;
            bool snapped = playerBuilder != null && playerBuilder.IsSnapped;
            BuildableData next = playerBuilder != null ? playerBuilder.GetRelativeBuildable(1) : null;
            int activeViewableIndex = catalog != null ? catalog.IndexOfViewable(active) : -1;
            int builderSlot = toolManager != null ? toolManager.FindAssignedSlotForToolType<Hecton8.Gameplay.BuilderTool>() : -1;
            bool builderActive = toolManager != null && toolManager.CurrentTool is Hecton8.Gameplay.BuilderTool;
            bool builderReady = builderSlot >= 0 && toolManager != null && toolManager.IsToolAvailableInSlot(builderSlot);
            bool hasPreview = playerBuilder != null && playerBuilder.HasPlacementPreview;

            Span<char> summary = _summaryBuffer.AsSpan();
            int summaryCursor = 0;
            bool summaryWritten =
                TryAppendLine(summary, ref summaryCursor, "CONSTRUCTION BACKBONE".AsSpan()) &&
                TryAppend(summary, ref summaryCursor, "CATALOG     ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedCatalogCount) &&
                TryAppendLine(summary, ref summaryCursor, " MODULES".AsSpan()) &&
                TryAppend(summary, ref summaryCursor, "FAMILIES    G".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedGeneratorCount) &&
                TryAppend(summary, ref summaryCursor, " / C".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedConsumerCount) &&
                TryAppend(summary, ref summaryCursor, " / P".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedPassiveCount) &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "DOMAINS     STR ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedStructureCount) &&
                TryAppend(summary, ref summaryCursor, " | HAB ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedHabitatCount) &&
                TryAppend(summary, ref summaryCursor, " | UTL ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedUtilityCount) &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "EXTENDED    LOG ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedLogisticsCount) &&
                TryAppend(summary, ref summaryCursor, " | FAB ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedFabricationCount) &&
                TryAppend(summary, ref summaryCursor, " | DEF ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedDefenseCount) &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "LOCKED      ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, _cachedLockedBlueprintCount) &&
                TryAppendLine(summary, ref summaryCursor, " BLUEPRINTS".AsSpan()) &&
                TryAppend(summary, ref summaryCursor, "BUILT       ".AsSpan()) &&
                TryAppendInt(summary, ref summaryCursor, builtCount) &&
                TryAppendLine(summary, ref summaryCursor, " REGISTERED".AsSpan()) &&
                TryAppend(summary, ref summaryCursor, "BUILDER     ".AsSpan()) &&
                TryAppendBuilderState(summary, ref summaryCursor, builderSlot, builderReady, builderActive) &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "ACTIVE      ".AsSpan()) &&
                TryAppendUpperInvariant(summary, ref summaryCursor, active != null ? active.moduleName : null, "NONE".AsSpan()) &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "FAMILY      ".AsSpan()) &&
                TryAppendString(summary, ref summaryCursor, active != null ? active.FamilyLabel : null, "N/A".AsSpan()) &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "ROLE        ".AsSpan()) &&
                TryAppendString(summary, ref summaryCursor, active != null ? DescribePowerRole(active) : null, "N/A".AsSpan()) &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "INDEX       ".AsSpan());

            if (summaryWritten && activeViewableIndex >= 0)
            {
                summaryWritten =
                    TryAppendInt(summary, ref summaryCursor, activeViewableIndex + 1) &&
                    TryAppend(summary, ref summaryCursor, "/".AsSpan()) &&
                    TryAppendInt(summary, ref summaryCursor, Mathf.Max(1, _cachedCatalogCount));
            }
            else if (summaryWritten)
            {
                summaryWritten = TryAppend(summary, ref summaryCursor, "N/A".AsSpan());
            }

            summaryWritten = summaryWritten &&
                TryAppendNewLine(summary, ref summaryCursor) &&
                TryAppend(summary, ref summaryCursor, "MODE        ".AsSpan()) &&
                TryAppend(summary, ref summaryCursor, snapped ? "SNAPPED".AsSpan() : "FREE PLACEMENT".AsSpan()) &&
                TryAppendNewLine(summary, ref summaryCursor);

            ApplyBufferedText(_summaryText, _summaryBuffer, summaryWritten ? summaryCursor : 0);

            Span<char> status = _statusBuffer.AsSpan();
            int statusCursor = 0;
            bool statusWritten;
            if (active == null)
            {
                _statusText.color = Warn;
                statusWritten = TryAppend(status, ref statusCursor, "NO ACTIVE BUILDABLE.\nSELECT A MODULE FROM THE CATALOG TO ARM THE BUILDER.".AsSpan());
            }
            else
            {
                _statusText.color = !hasResources ? Warn : (canPlace ? Ready : Blocked);
                statusWritten =
                    TryAppend(status, ref statusCursor, "READINESS // ".AsSpan()) &&
                    TryAppend(status, ref statusCursor, !hasResources ? "MISSING COST".AsSpan() : (canPlace ? (snapped ? "SNAPPED READY".AsSpan() : "READY".AsSpan()) : "PLACEMENT BLOCKED".AsSpan())) &&
                    TryAppendNewLine(status, ref statusCursor) &&
                    TryAppendCostDigest(status, ref statusCursor, active);
            }

            ApplyBufferedText(_statusText, _statusBuffer, statusWritten ? statusCursor : 0);
            RefreshDirective(catalog, active, next, hasResources, canPlace, snapped, builtCount);
            RefreshBuilderAction(builderSlot, builderReady, builderActive);
            RefreshFieldAction(active, hasResources, canPlace, builderSlot, builderReady, builderActive, hasPreview);
        }

        private void RefreshCatalogCache(ModuleCatalog catalog)
        {
            _cachedCatalogCount = catalog != null ? catalog.ViewableCount : 0;
            _cachedGeneratorCount = CountModulesByPowerRole(catalog, 1);
            _cachedConsumerCount = CountModulesByPowerRole(catalog, -1);
            _cachedPassiveCount = CountModulesByPowerRole(catalog, 0);
            _cachedStructureCount = CountModulesByFamily(catalog, BuildableFamily.Structure);
            _cachedHabitatCount = CountModulesByFamily(catalog, BuildableFamily.Habitat);
            _cachedUtilityCount = CountModulesByFamily(catalog, BuildableFamily.Utility);
            _cachedLogisticsCount = CountModulesByFamily(catalog, BuildableFamily.Logistics);
            _cachedFabricationCount = CountModulesByFamily(catalog, BuildableFamily.Fabrication);
            _cachedDefenseCount = CountModulesByFamily(catalog, BuildableFamily.Defense);
            _cachedLockedBlueprintCount = CountLockedBlueprintModules(catalog);
        }

        private void RefreshCatalog(ModuleCatalog catalog, BuildableData active, int count)
        {
            for (int i = 0; i < maxVisibleCards; i++)
            {
                bool visible = i < count && catalog != null;
                SetCardVisible(i, visible);
                if (!visible)
                    continue;

                BuildableData data = catalog.GetViewableAt(i);
                bool isActive = ReferenceEquals(active, data);
                bool hasCost = data != null && playerInventory != null && HasCost(data);
                bool canSelect = data != null;
                bool isReadyCandidate = !isActive && hasCost;

                _cardBgs[i].color = isActive ? BoxActive : BoxBg;
                SetUpperInvariant(_cardTitles[i], _cardTitleBuffers[i], data != null ? data.moduleName : null, "UNKNOWN MODULE");
                _cardTitles[i].color = isActive ? Primary : (hasCost ? Dim : Warn);
                WriteCardBody(_cardBodies[i], data, isActive, hasCost);

                SetLiteralText(_cardButtonLabels[i], isActive ? "ARMED".AsSpan() : (isReadyCandidate ? "ARM".AsSpan() : "QUEUE".AsSpan()));
                _cardButtonLabels[i].color = isActive ? Primary : (hasCost ? Dim : Warn);
                _cardButtonBgs[i].color = isActive
                    ? new Color(0.14f, 0.3f, 0.28f, 0.78f)
                    : (hasCost ? new Color(0.08f, 0.16f, 0.18f, 0.58f) : new Color(0.28f, 0.2f, 0.06f, 0.72f));

                PDAConstructionSelectButton button = _cardButtons[i];
                if (button != null)
                {
                    button.SetVisualState(_cardButtonBgs[i].color, new Color(0.12f, 0.24f, 0.28f, 0.82f));
                    button.SetInteractable(canSelect && !isActive);
                }
            }
        }

        internal void SelectBuildable(int index)
        {
            if (constructionManager == null || playerBuilder == null)
                return;

            ModuleCatalog catalog = constructionManager.Catalog;
            BuildableData data = catalog != null ? catalog.GetViewableAt(index) : null;
            if (data == null)
                return;

            if (!data.IsBlueprintViewable())
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - BLUEPRINT LOCKED");
                MarkAllDirty();
                Refresh(true);
                return;
            }

            playerBuilder.SetActiveBuildable(data);
            ShowConstructionInfoWithModule("CONSTRUCTION MATRIX - ", data.moduleName, " ARMED");
            MarkAllDirty();
            Refresh(true);
        }

        internal void InvokeBuilderAction()
        {
            if (toolManager == null)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX — TOOL MANAGER OFFLINE");
                return;
            }

            int builderSlot = toolManager.FindAssignedSlotForToolType<Hecton8.Gameplay.BuilderTool>();
            bool builderActive = toolManager.CurrentTool is Hecton8.Gameplay.BuilderTool;

            if (builderActive)
            {
                toolManager.Holster();
                hudNotification?.ShowInfo("CONSTRUCTION MATRIX — BUILDER HOLSTERED");
                MarkSummaryDirty();
                Refresh();
                return;
            }

            if (builderSlot >= 0)
            {
                if (!toolManager.IsToolAvailableInSlot(builderSlot))
                {
                    ShowConstructionWarningWithSlot("CONSTRUCTION MATRIX - BUILDER NOT IN CARGO [S", builderSlot, "]");
                    return;
                }

                toolManager.SwitchToSlot(builderSlot);
                ShowConstructionInfoWithSlot("CONSTRUCTION MATRIX - BUILDER ACTIVATED [S", builderSlot, "]");
                MarkSummaryDirty();
                Refresh();
                return;
            }

            GameObject builderPrefab = toolManager.GetKnownToolPrefabForToolType<Hecton8.Gameplay.BuilderTool>();
            if (builderPrefab == null)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX — NO BUILDER PREFAB REGISTERED");
                return;
            }

            int targetSlot = Mathf.Clamp(toolManager.SlotCount - 1, 0, Mathf.Max(0, toolManager.SlotCount - 1));
            toolManager.SetAssignedToolPrefab(targetSlot, builderPrefab, holsterIfCurrentInvalid: false);
            ShowConstructionInfoWithSlot("CONSTRUCTION MATRIX - BUILDER ARMED TO S", targetSlot, string.Empty);
            MarkSummaryDirty();
            Refresh();
        }

        internal void InvokeFieldAction()
        {
            if (playerBuilder == null)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - BUILDER LOGIC OFFLINE");
                return;
            }

            if (playerBuilder.ActiveBuildable == null)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - NO ACTIVE MODULE");
                return;
            }

            if (toolManager == null)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - TOOL MANAGER OFFLINE");
                return;
            }

            int builderSlot = toolManager.FindAssignedSlotForToolType<Hecton8.Gameplay.BuilderTool>();
            bool builderActive = toolManager.CurrentTool is Hecton8.Gameplay.BuilderTool;
            bool builderReady = builderSlot >= 0 && toolManager.IsToolAvailableInSlot(builderSlot);

            if (builderActive)
            {
                if (playerBuilder.CanPlaceActiveBuildable)
                {
                    if (playerBuilder.TryDeployActiveBuildableFromPreview())
                        ShowConstructionInfoWithModule("CONSTRUCTION MATRIX - ", playerBuilder.ActiveBuildable.moduleName, " DEPLOYED");
                    else
                        hudNotification?.ShowWarning("CONSTRUCTION MATRIX - DEPLOY FAILED");

                    MarkAllDirty();
                    Refresh(true);
                    return;
                }

                QueueClosePDACommand();
                hudNotification?.ShowInfo("CONSTRUCTION MATRIX - FIELD PREVIEW ACTIVE");
                return;
            }

            if (builderSlot < 0)
            {
                GameObject builderPrefab = toolManager.GetKnownToolPrefabForToolType<Hecton8.Gameplay.BuilderTool>();
                if (builderPrefab == null)
                {
                    hudNotification?.ShowWarning("CONSTRUCTION MATRIX - NO BUILDER PREFAB REGISTERED");
                    return;
                }

                int targetSlot = Mathf.Clamp(toolManager.SlotCount - 1, 0, Mathf.Max(0, toolManager.SlotCount - 1));
                toolManager.SetAssignedToolPrefab(targetSlot, builderPrefab, holsterIfCurrentInvalid: false);
                builderSlot = targetSlot;
                builderReady = toolManager.IsToolAvailableInSlot(builderSlot);
                ShowConstructionInfoWithSlot("CONSTRUCTION MATRIX - BUILDER ARMED TO S", builderSlot, string.Empty);
            }

            if (!builderReady)
            {
                ShowConstructionWarningWithSlot("CONSTRUCTION MATRIX - BUILDER NOT IN CARGO [S", builderSlot, "]");
                MarkSummaryDirty();
                Refresh();
                return;
            }

            toolManager.SwitchToSlot(builderSlot);
            QueueClosePDACommand();
            ShowConstructionInfoWithSlot("CONSTRUCTION MATRIX - FIELD PREVIEW [S", builderSlot, "]");
        }

        private static void QueueClosePDACommand()
        {
            EntityCommand command = EntityCommand.CreateClosePDA();
            ThreadSafeCommandQueue.Enqueue(in command);
        }

        private void ShowConstructionInfoWithModule(ReadOnlySpan<char> prefix, string moduleName, ReadOnlySpan<char> suffix)
        {
            if (hudNotification == null)
                return;

            WriteModuleNotification(prefix, moduleName, suffix);
            hudNotification.ShowInfo(in _notificationBuffer);
        }

        private void ShowConstructionInfoWithSlot(ReadOnlySpan<char> prefix, int zeroBasedSlot, ReadOnlySpan<char> suffix)
        {
            if (hudNotification == null)
                return;

            WriteSlotNotification(prefix, zeroBasedSlot, suffix);
            hudNotification.ShowInfo(in _notificationBuffer);
        }

        private void ShowConstructionWarningWithSlot(ReadOnlySpan<char> prefix, int zeroBasedSlot, ReadOnlySpan<char> suffix)
        {
            if (hudNotification == null)
                return;

            WriteSlotNotification(prefix, zeroBasedSlot, suffix);
            hudNotification.ShowWarning(in _notificationBuffer);
        }

        private void WriteModuleNotification(ReadOnlySpan<char> prefix, string moduleName, ReadOnlySpan<char> suffix)
        {
            _notificationBuffer.Clear();
            _notificationBuffer.Append(prefix);
            AppendUpperInvariant(ref _notificationBuffer, moduleName);
            _notificationBuffer.Append(suffix);
        }

        private void WriteSlotNotification(ReadOnlySpan<char> prefix, int zeroBasedSlot, ReadOnlySpan<char> suffix)
        {
            _notificationBuffer.Clear();
            _notificationBuffer.Append(prefix);
            _notificationBuffer.AppendInt(zeroBasedSlot + 1);
            _notificationBuffer.Append(suffix);
        }

        internal void InvokeDeployAction()
        {
            if (toolManager == null || playerBuilder == null)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - SYSTEM OFFLINE");
                return;
            }

            if (!(toolManager.CurrentTool is Hecton8.Gameplay.BuilderTool))
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - BUILDER NOT ACTIVE");
                return;
            }

            if (playerBuilder.ActiveBuildable == null)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - NO MODULE SELECTED");
                return;
            }

            if (!playerBuilder.CanPlaceActiveBuildable)
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - CANNOT PLACE HERE");
                return;
            }

            if (playerBuilder.TryDeployActiveBuildableFromPreview())
            {
                ShowConstructionInfoWithModule("CONSTRUCTION MATRIX - ", playerBuilder.ActiveBuildable.moduleName, " DEPLOYED");
                MarkAllDirty();
                Refresh(true);
            }
            else
            {
                hudNotification?.ShowWarning("CONSTRUCTION MATRIX - DEPLOY FAILED");
            }
        }

        private bool HasCost(BuildableData data)
        {
            if (data == null || data.buildCost == null || playerInventory == null)
                return false;

            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null)
                    continue;
                if (cost.item == null ||
                    playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId)) < cost.amount)
                    return false;
            }

            return true;
        }

        private void WriteCardBody(TextMeshProUGUI target, BuildableData data, bool isActive, bool hasCost)
        {
            if (target == null)
                return;

            if (data == null)
            {
                SetLiteralText(target, "OFFLINE".AsSpan());
                return;
            }

            Span<char> buffer = _cardBodyBuffer.AsSpan();
            int cursor = 0;
            int roundedPower = Mathf.RoundToInt(data.powerRating);
            bool written =
                TryAppendLine(buffer, ref cursor, isActive ? "STATUS   ARMED".AsSpan() : "STATUS   STANDBY".AsSpan()) &&
                TryAppend(buffer, ref cursor, "ROLE     ".AsSpan()) &&
                TryAppendString(buffer, ref cursor, DescribePowerRole(data), "OFFLINE".AsSpan()) &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppend(buffer, ref cursor, "FAMILY   ".AsSpan()) &&
                TryAppendString(buffer, ref cursor, data.FamilyLabel, "N/A".AsSpan()) &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppend(buffer, ref cursor, "PURPOSE  ".AsSpan()) &&
                TryAppendString(buffer, ref cursor, DescribePurpose(data), "EXTEND FOOTPRINT".AsSpan()) &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppend(buffer, ref cursor, "POWER    ".AsSpan());

            if (written && roundedPower > 0)
            {
                written =
                    TryAppend(buffer, ref cursor, "+".AsSpan()) &&
                    TryAppendInt(buffer, ref cursor, roundedPower) &&
                    TryAppend(buffer, ref cursor, "W NET".AsSpan());
            }
            else if (written && roundedPower < 0)
            {
                written =
                    TryAppendInt(buffer, ref cursor, roundedPower) &&
                    TryAppend(buffer, ref cursor, "W LOAD".AsSpan());
            }
            else if (written)
            {
                written = TryAppend(buffer, ref cursor, "PASSIVE".AsSpan());
            }

            written = written &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppend(buffer, ref cursor, "FOOTPRINT ".AsSpan()) &&
                TryAppendInt(buffer, ref cursor, data.TotalResourceCount) &&
                TryAppendLine(buffer, ref cursor, " UNITS".AsSpan()) &&
                TryAppend(buffer, ref cursor, "COST     ".AsSpan()) &&
                TryAppendShortCost(buffer, ref cursor, data) &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppend(buffer, ref cursor, "STATE    ".AsSpan()) &&
                TryAppend(buffer, ref cursor, hasCost ? "READY".AsSpan() : "MISSING COST".AsSpan()) &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppend(buffer, ref cursor, "TACTIC   ".AsSpan()) &&
                TryAppend(buffer, ref cursor, isActive ? "FIELD ACTIVE".AsSpan() : (hasCost ? "ARM NEXT".AsSpan() : "GATHER MATS".AsSpan()));

            if (written && !string.IsNullOrWhiteSpace(data.description))
            {
                written =
                    TryAppendNewLine(buffer, ref cursor) &&
                    TryAppend(buffer, ref cursor, "NOTES    ".AsSpan()) &&
                    TryAppendTrimmedUpperForCard(buffer, ref cursor, data.description, 56);
            }

            ApplyBufferedText(target, _cardBodyBuffer, written ? cursor : 0);
        }

        private void RefreshDirective(
            ModuleCatalog catalog,
            BuildableData active,
            BuildableData next,
            bool hasResources,
            bool canPlace,
            bool snapped,
            int builtCount)
        {
            if (_directiveText == null)
                return;

            Span<char> directive = _directiveBuffer.AsSpan();
            int directiveCursor = 0;
            bool directiveWritten;
            if (catalog == null || catalog.Count == 0)
            {
                _directiveText.color = Blocked;
                directiveWritten = TryAppend(directive, ref directiveCursor, "DIRECTIVE // AUTHOR OR ASSIGN A MODULE CATALOG.".AsSpan());
            }
            else if (_cachedCatalogCount == 0)
            {
                _directiveText.color = Blocked;
                directiveWritten = TryAppend(directive, ref directiveCursor, "DIRECTIVE // BLUEPRINTS LOCKED BY QUEST STATE.".AsSpan());
            }
            else if (active == null)
            {
                _directiveText.color = Warn;
                directiveWritten = TryAppend(directive, ref directiveCursor, "DIRECTIVE // ARM A STARTER MODULE. FOUNDATION FIRST.".AsSpan());
            }
            else if (!hasResources)
            {
                _directiveText.color = Warn;
                directiveWritten =
                    TryAppend(directive, ref directiveCursor, "DIRECTIVE // ".AsSpan()) &&
                    TryAppendBuilderAdviceUpperOrFallback(directive, ref directiveCursor, "GATHER COST BEFORE DEPLOYMENT.".AsSpan());
                if (next != null && !ReferenceEquals(next, active))
                {
                    directiveWritten = directiveWritten &&
                        TryAppend(directive, ref directiveCursor, " NEXT VIABLE CANDIDATE: ".AsSpan()) &&
                        TryAppendUpperInvariant(directive, ref directiveCursor, next.moduleName, "UNKNOWN".AsSpan()) &&
                        TryAppend(directive, ref directiveCursor, " (".AsSpan()) &&
                        TryAppendString(directive, ref directiveCursor, next.FamilyShortCode, "N/A".AsSpan()) &&
                        TryAppend(directive, ref directiveCursor, " / ".AsSpan()) &&
                        TryAppendString(directive, ref directiveCursor, DescribePowerRole(next), "OFFLINE".AsSpan()) &&
                        TryAppend(directive, ref directiveCursor, ").".AsSpan());
                }
            }
            else if (!canPlace)
            {
                _directiveText.color = Blocked;
                directiveWritten =
                    TryAppend(directive, ref directiveCursor, "DIRECTIVE // ".AsSpan()) &&
                    TryAppendBuilderAdviceUpperOrFallback(directive, ref directiveCursor, "REPOSITION UNTIL BUILD VOLUME CLEARS.".AsSpan());
                if (builtCount <= 0)
                    directiveWritten = directiveWritten && TryAppend(directive, ref directiveCursor, " OPEN WITH FOUNDATION OR PYLON FOR FIRST ANCHOR.".AsSpan());
            }
            else if (snapped)
            {
                _directiveText.color = Ready;
                directiveWritten =
                    TryAppend(directive, ref directiveCursor, "DIRECTIVE // ".AsSpan()) &&
                    TryAppendBuilderAdviceUpperOrFallback(directive, ref directiveCursor, "SOCKET LOCK ACQUIRED. DEPLOY FOR CLEAN CHAIN EXTENSION.".AsSpan());
            }
            else
            {
                _directiveText.color = Ready;
                directiveWritten =
                    TryAppend(directive, ref directiveCursor, "DIRECTIVE // ".AsSpan()) &&
                    TryAppendBuilderAdviceUpperOrFallback(directive, ref directiveCursor, "FIELD-READY. DEPLOY ACTIVE MODULE.".AsSpan());
            }

            ApplyBufferedText(_directiveText, _directiveBuffer, directiveWritten ? directiveCursor : 0);

            if (_hintText == null)
                return;

            Span<char> hint = _hintBuffer.AsSpan();
            int hintCursor = 0;
            bool hintWritten = TryAppend(hint, ref hintCursor, "Select to arm. ".AsSpan());
            if (active != null)
            {
                hintWritten = hintWritten &&
                    TryAppend(hint, ref hintCursor, "Active: ".AsSpan()) &&
                    TryAppendUpperInvariant(hint, ref hintCursor, active.moduleName, "UNKNOWN".AsSpan()) &&
                    TryAppend(hint, ref hintCursor, " [".AsSpan()) &&
                    TryAppendString(hint, ref hintCursor, active.FamilyShortCode, "N/A".AsSpan()) &&
                    TryAppend(hint, ref hintCursor, " / ".AsSpan()) &&
                    TryAppendString(hint, ref hintCursor, DescribePowerRole(active), "OFFLINE".AsSpan()) &&
                    TryAppend(hint, ref hintCursor, "]. ".AsSpan());
            }
            hintWritten = hintWritten &&
                TryAppend(hint, ref hintCursor, "TAB / Q-E cycle in the field. INTERACT recovers a placed module. ".AsSpan());
            if (next != null && !ReferenceEquals(next, active))
            {
                hintWritten = hintWritten &&
                    TryAppend(hint, ref hintCursor, "Next: ".AsSpan()) &&
                    TryAppendUpperInvariant(hint, ref hintCursor, next.moduleName, "UNKNOWN".AsSpan()) &&
                    TryAppend(hint, ref hintCursor, " [".AsSpan()) &&
                    TryAppendString(hint, ref hintCursor, next.FamilyShortCode, "N/A".AsSpan()) &&
                    TryAppend(hint, ref hintCursor, "].".AsSpan());
            }
            ApplyBufferedText(_hintText, _hintBuffer, hintWritten ? hintCursor : 0);
        }

        private bool TryAppendBuilderAdviceUpperOrFallback(Span<char> buffer, ref int cursor, ReadOnlySpan<char> fallback)
        {
            if (playerBuilder == null)
                return TryAppend(buffer, ref cursor, fallback);

            FixedCharBuffer adviceBuffer = new FixedCharBuffer(_directiveAdviceScratchBuffer);
            adviceBuffer.Clear();
            playerBuilder.WriteActiveBuildAdvice(ref adviceBuffer);

            ReadOnlySpan<char> advice = adviceBuffer.AsSpan();
            if (advice.Length == 0)
                return TryAppend(buffer, ref cursor, fallback);

            for (int i = 0; i < advice.Length; i++)
            {
                if (cursor >= buffer.Length)
                    return false;

                buffer[cursor++] = ToAsciiUpperInvariant(advice[i]);
            }

            return true;
        }

        private static int CountModulesByFamily(ModuleCatalog catalog, BuildableFamily family)
        {
            if (catalog == null)
                return 0;

            int count = 0;
            int catalogCount = catalog.Count;
            for (int i = 0; i < catalogCount; i++)
            {
                BuildableData data = catalog.GetAt(i);
                if (data != null && data.IsBlueprintViewable() && data.family == family)
                    count++;
            }

            return count;
        }

        private static string DescribePowerRole(BuildableData data)
        {
            if (data == null)
                return "OFFLINE";
            if (data.IsGenerator)
                return "GENERATOR";
            if (data.IsConsumer)
                return "CONSUMER";
            return "STRUCTURAL";
        }

        private static string DescribePurpose(BuildableData data)
        {
            if (data == null)
                return "OFFLINE";

            switch (data.family)
            {
                case BuildableFamily.Structure: return "EXTEND HULL";
                case BuildableFamily.Habitat: return "EXPAND SAFE SPACE";
                case BuildableFamily.Utility: return data.IsGenerator ? "STABILIZE POWER" : "SUPPORT SYSTEMS";
                case BuildableFamily.Fabrication: return "ADD PRODUCTION";
                case BuildableFamily.Logistics: return "IMPROVE ROUTING";
                case BuildableFamily.Defense: return "HARDEN APPROACHES";
                default: return "EXTEND FOOTPRINT";
            }
        }

        private static bool TryAppendBuilderState(Span<char> buffer, ref int cursor, int builderSlot, bool builderReady, bool builderActive)
        {
            if (builderActive)
                return TryAppend(buffer, ref cursor, "ACTIVE".AsSpan());

            if (builderSlot < 0)
                return TryAppend(buffer, ref cursor, "UNASSIGNED".AsSpan());

            return TryAppend(buffer, ref cursor, "ASSIGNED S".AsSpan()) &&
                TryAppendInt(buffer, ref cursor, builderSlot + 1) &&
                TryAppend(buffer, ref cursor, builderReady ? " / READY".AsSpan() : " / MISSING".AsSpan());
        }

        private void RefreshBuilderAction(int builderSlot, bool builderReady, bool builderActive)
        {
            if (_builderActionRoot == null || _builderActionLabel == null || _builderActionBg == null)
                return;

            if (toolManager == null)
            {
                SetActionRootVisible(_builderActionCanvasGroup, ref _builderActionVisible, false);
                return;
            }

            SetActionRootVisible(_builderActionCanvasGroup, ref _builderActionVisible, true);

            if (builderActive)
            {
                SetLiteralText(_builderActionLabel, "HOLSTER BUILDER".AsSpan());
                _builderActionLabel.color = Primary;
                _builderActionBg.color = new Color(0.14f, 0.3f, 0.28f, 0.78f);
            }
            else if (builderSlot >= 0 && builderReady)
            {
                SetSlotActionLabel(_builderActionLabel, "ACTIVATE BUILDER", builderSlot);
                _builderActionLabel.color = Dim;
                _builderActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }
            else if (builderSlot >= 0)
            {
                SetSlotActionLabel(_builderActionLabel, "BUILDER MISSING", builderSlot);
                _builderActionLabel.color = Warn;
                _builderActionBg.color = new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else
            {
                SetLiteralText(_builderActionLabel, "ARM BUILDER TO S4".AsSpan());
                _builderActionLabel.color = Dim;
                _builderActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }

            PDAConstructionBuilderActionButton button = _builderActionButton;
            if (button != null)
                button.SetVisualState(_builderActionBg.color, new Color(0.12f, 0.24f, 0.28f, 0.82f));
        }

        private void RefreshFieldAction(
            BuildableData active,
            bool hasResources,
            bool canPlace,
            int builderSlot,
            bool builderReady,
            bool builderActive,
            bool hasPreview)
        {
            if (_fieldActionRoot == null || _fieldActionLabel == null || _fieldActionBg == null)
                return;

            if (active == null || playerBuilder == null)
            {
                SetActionRootVisible(_fieldActionCanvasGroup, ref _fieldActionVisible, false);
                return;
            }

            SetActionRootVisible(_fieldActionCanvasGroup, ref _fieldActionVisible, true);

            if (builderActive && canPlace)
            {
                SetLiteralText(_fieldActionLabel, "DEPLOY ACTIVE".AsSpan());
                _fieldActionLabel.color = Primary;
                _fieldActionBg.color = new Color(0.14f, 0.3f, 0.28f, 0.78f);
            }
            else if (builderActive)
            {
                SetLiteralText(_fieldActionLabel, hasPreview ? "RETURN TO FIELD".AsSpan() : "PREVIEW OFFLINE".AsSpan());
                _fieldActionLabel.color = hasPreview ? Dim : Warn;
                _fieldActionBg.color = hasPreview
                    ? new Color(0.08f, 0.16f, 0.18f, 0.58f)
                    : new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else if (builderSlot >= 0 && builderReady)
            {
                SetSlotActionLabel(_fieldActionLabel, "FIELD PREVIEW", builderSlot);
                _fieldActionLabel.color = Dim;
                _fieldActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }
            else if (builderSlot >= 0)
            {
                SetSlotActionLabel(_fieldActionLabel, "BUILDER CARGO", builderSlot);
                _fieldActionLabel.color = Warn;
                _fieldActionBg.color = new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else if (!hasResources)
            {
                SetLiteralText(_fieldActionLabel, "MISSING COST".AsSpan());
                _fieldActionLabel.color = Warn;
                _fieldActionBg.color = new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else
            {
                SetLiteralText(_fieldActionLabel, "ARM + PREVIEW".AsSpan());
                _fieldActionLabel.color = Dim;
                _fieldActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }

            PDAConstructionFieldActionButton button = _fieldActionButton;
            if (button != null)
                button.SetVisualState(_fieldActionBg.color, new Color(0.12f, 0.24f, 0.28f, 0.82f));
        }

        private void SetSlotActionLabel(TextMeshProUGUI label, string prefix, int builderSlot)
        {
            if (label == null)
                return;

            Span<char> buffer = _actionLabelBuffer.AsSpan();
            int cursor = 0;
            bool written =
                TryAppendString(buffer, ref cursor, prefix, ReadOnlySpan<char>.Empty) &&
                TryAppend(buffer, ref cursor, " [S".AsSpan()) &&
                TryAppendInt(buffer, ref cursor, builderSlot + 1) &&
                TryAppend(buffer, ref cursor, "]".AsSpan());

            ApplyBufferedText(label, _actionLabelBuffer, written ? cursor : 0);
        }

        private void SetCardVisible(int index, bool visible)
        {
            if (_cardRoots == null || _cardVisibility == null || _cardCanvasGroups == null ||
                index < 0 || index >= _cardRoots.Length || index >= _cardVisibility.Length || index >= _cardCanvasGroups.Length)
                return;

            RectTransform root = _cardRoots[index];
            CanvasGroup canvasGroup = _cardCanvasGroups[index];
            if (root == null || canvasGroup == null || _cardVisibility[index] == visible)
                return;

            SetCanvasGroupVisible(canvasGroup, visible);
            _cardVisibility[index] = visible;
        }

        private static void SetActionRootVisible(CanvasGroup canvasGroup, ref bool currentVisible, bool visible)
        {
            if (canvasGroup == null || currentVisible == visible)
                return;

            SetCanvasGroupVisible(canvasGroup, visible);
            currentVisible = visible;
        }

        private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private static void SetLiteralText(TextMeshProUGUI label, ReadOnlySpan<char> value)
        {
            if (label == null || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> buffer = lease.Buffer.AsSpan();
                int cursor = 0;
                if (TryAppend(buffer, ref cursor, value))
                    label.SetCharArray(lease.Buffer, 0, cursor);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void ApplyBufferedText(TextMeshProUGUI label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private static bool TryAppendLine(Span<char> buffer, ref int cursor, ReadOnlySpan<char> value)
        {
            return TryAppend(buffer, ref cursor, value) && TryAppendNewLine(buffer, ref cursor);
        }

        private static bool TryAppendNewLine(Span<char> buffer, ref int cursor)
        {
            if (cursor < 0 || cursor >= buffer.Length)
                return false;

            buffer[cursor++] = '\n';
            return true;
        }

        private static bool TryAppendInt(Span<char> buffer, ref int cursor, int value)
        {
            if ((uint)cursor > (uint)buffer.Length ||
                !value.TryFormat(buffer.Slice(cursor), out int written))
            {
                return false;
            }

            cursor += written;
            return true;
        }

        private static bool TryAppendString(Span<char> buffer, ref int cursor, string value, ReadOnlySpan<char> fallback)
        {
            return string.IsNullOrEmpty(value)
                ? TryAppend(buffer, ref cursor, fallback)
                : TryAppend(buffer, ref cursor, value.AsSpan());
        }

        private static bool TryAppendUpperInvariant(Span<char> buffer, ref int cursor, string value, ReadOnlySpan<char> fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TryAppend(buffer, ref cursor, fallback);

            for (int i = 0; i < value.Length; i++)
            {
                if (cursor >= buffer.Length)
                    return false;

                buffer[cursor++] = ToAsciiUpperInvariant(value[i]);
            }

            return true;
        }

        private static bool TryAppend(Span<char> buffer, ref int cursor, ReadOnlySpan<char> value)
        {
            if (cursor < 0 || cursor + value.Length > buffer.Length)
                return false;

            value.CopyTo(buffer.Slice(cursor));
            cursor += value.Length;
            return true;
        }

        private static char ToAsciiUpperInvariant(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - 32) : char.ToUpperInvariant(value);
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

        private static bool TryAppendTrimmedUpperForCard(Span<char> buffer, ref int cursor, string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text) || maxChars <= 0)
                return true;

            int appended = 0;
            int lastNonSpaceCursor = cursor;
            bool started = false;
            bool truncated = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool isWhitespace = char.IsWhiteSpace(c);
                if (!started)
                {
                    if (isWhitespace)
                        continue;

                    started = true;
                }

                char normalized = isWhitespace ? ' ' : ToAsciiUpperInvariant(c);
                if (appended >= maxChars)
                {
                    if (!isWhitespace)
                        truncated = true;
                    continue;
                }

                if (cursor >= buffer.Length)
                    return false;

                buffer[cursor++] = normalized;
                appended++;
                if (normalized != ' ')
                    lastNonSpaceCursor = cursor;
            }

            if (cursor > lastNonSpaceCursor)
                cursor = lastNonSpaceCursor;

            if (truncated)
                return TryAppend(buffer, ref cursor, "...".AsSpan());

            return true;
        }

        private static void SetUpperInvariant(TextMeshProUGUI target, char[] buffer, string value, string fallback)
        {
            if (target == null || buffer == null)
                return;

            int length = CopyUpperInvariant(buffer, string.IsNullOrEmpty(value) ? fallback : value);
            target.SetCharArray(buffer, 0, length);
        }

        private static int CopyUpperInvariant(char[] buffer, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value))
                return 0;

            int length = Mathf.Min(buffer.Length, value.Length);
            for (int i = 0; i < length; i++)
                buffer[i] = ToAsciiUpperInvariant(value[i]);

            return length;
        }

        private static int CountModulesByPowerRole(ModuleCatalog catalog, int mode)
        {
            if (catalog == null || catalog.Count <= 0)
                return 0;

            int count = 0;
            int catalogCount = catalog.Count;
            for (int i = 0; i < catalogCount; i++)
            {
                BuildableData data = catalog.GetAt(i);
                if (data == null)
                    continue;
                if (!data.IsBlueprintViewable())
                    continue;

                bool matches = mode > 0 ? data.IsGenerator : mode < 0 ? data.IsConsumer : (!data.IsGenerator && !data.IsConsumer);
                if (matches)
                    count++;
            }

            return count;
        }

        private static int CountLockedBlueprintModules(ModuleCatalog catalog)
        {
            if (catalog == null || catalog.Count <= 0)
                return 0;

            int count = 0;
            int catalogCount = catalog.Count;
            for (int i = 0; i < catalogCount; i++)
            {
                BuildableData data = catalog.GetAt(i);
                if (data != null && data.RequiresBlueprintQuestFlag && !data.IsBlueprintViewable())
                    count++;
            }

            return count;
        }

        private bool TryAppendShortCost(Span<char> buffer, ref int cursor, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return TryAppend(buffer, ref cursor, "NONE".AsSpan());

            int appendedCosts = 0;
            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null)
                    continue;
                if (appendedCosts > 0 && !TryAppend(buffer, ref cursor, " | ".AsSpan()))
                    return false;
                if (!TryAppendUpperInvariant(buffer, ref cursor, cost.item.itemName, "UNKNOWN".AsSpan()) ||
                    !TryAppend(buffer, ref cursor, " ".AsSpan()) ||
                    !TryAppendInt(buffer, ref cursor, cost.amount))
                {
                    return false;
                }

                appendedCosts++;
            }

            return appendedCosts > 0 || TryAppend(buffer, ref cursor, "NONE".AsSpan());
        }

        private bool TryAppendCostDigest(Span<char> buffer, ref int cursor, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return TryAppend(buffer, ref cursor, "NO BUILD COST DATA.".AsSpan());

            int appendedCosts = 0;
            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null)
                    continue;

                int owned = playerInventory != null && cost.item != null
                    ? playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId))
                    : 0;
                if (appendedCosts > 0 && !TryAppend(buffer, ref cursor, "  |  ".AsSpan()))
                    return false;
                if (!TryAppendUpperInvariant(buffer, ref cursor, cost.item.itemName, "UNKNOWN".AsSpan()) ||
                    !TryAppend(buffer, ref cursor, " ".AsSpan()) ||
                    !TryAppendInt(buffer, ref cursor, owned) ||
                    !TryAppend(buffer, ref cursor, "/".AsSpan()) ||
                    !TryAppendInt(buffer, ref cursor, cost.amount))
                {
                    return false;
                }

                appendedCosts++;
            }

            return appendedCosts > 0 || TryAppend(buffer, ref cursor, "NO BUILD COST DATA.".AsSpan());
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image bg = EnsureImage(rect.gameObject);
            bg.color = BoxBg;
            bg.raycastTarget = false;
            return rect;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (go == null)
                return null;

            return go.AddComponent<CanvasGroup>();
        }

        private static void ClearChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static RectTransform CreateRect(RectTransform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image EnsureImage(GameObject go)
        {
            Image image = go.GetComponent<Image>();
            if (image == null)
                image = go.AddComponent<Image>();
            return image;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(parent, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            LocalizedTMPAutoSizer.Configure(text, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            return text;
        }

        private static TextMeshProUGUI CreateSectionHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI header = CreateText(parent, "Header_" + text, TMP_Settings.defaultFontAsset, 11f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -12f), new Vector2(-14f, 18f));
            header.color = DimLow;
            SetLiteralText(header, text.AsSpan());
            return header;
        }

        private TextMeshProUGUI CreateBody(RectTransform parent, string name, TMP_FontAsset font)
        {
            TextMeshProUGUI body = CreateText(parent, name, font, 11f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.color = Dim;
            return body;
        }

        private static void CreateRule(RectTransform parent, float y)
        {
            RectTransform rule = CreateRect(parent, "Rule_" + y);
            rule.anchorMin = new Vector2(0f, 1f);
            rule.anchorMax = new Vector2(1f, 1f);
            rule.pivot = new Vector2(0.5f, 1f);
            rule.anchoredPosition = new Vector2(0f, y);
            rule.sizeDelta = new Vector2(-36f, 1f);
            Image img = EnsureImage(rule.gameObject);
            img.color = Rule;
            img.raycastTarget = false;
        }

        private static void CreateInnerRule(RectTransform parent, float anchorY)
        {
            RectTransform rule = CreateRect(parent, "InnerRule");
            rule.anchorMin = new Vector2(0.04f, anchorY);
            rule.anchorMax = new Vector2(0.96f, anchorY);
            rule.offsetMin = Vector2.zero;
            rule.offsetMax = Vector2.zero;
            rule.sizeDelta = new Vector2(0f, 1f);
            Image img = EnsureImage(rule.gameObject);
            img.color = Rule;
            img.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDAConstructionSelectButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAConstructionTab _tab;
        private int _index;
        private Image _bg;
        private TextMeshProUGUI _label;
        private Color _normal;
        private Color _hover;
        private bool _interactable = true;

        public void Init(PDAConstructionTab tab, int index, Image bg, TextMeshProUGUI label, Color normal, Color hover)
        {
            _tab = tab;
            _index = index;
            _bg = bg;
            _label = label;
            _normal = normal;
            _hover = hover;
        }

        public void SetVisualState(Color normal, Color hover)
        {
            _normal = normal;
            _hover = hover;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (_bg != null)
                _bg.color = value ? _normal : new Color(_normal.r, _normal.g, _normal.b, _normal.a * 0.7f);
            if (_label != null)
                _label.alpha = value ? 1f : 0.82f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_interactable)
                _tab?.SelectBuildable(_index);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_interactable && _bg != null)
                _bg.color = _hover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _interactable ? _normal : new Color(_normal.r, _normal.g, _normal.b, _normal.a * 0.7f);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDAConstructionBuilderActionButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAConstructionTab _tab;
        private Image _bg;
        private Color _normal;
        private Color _hover;

        public void Init(PDAConstructionTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normal = normal;
            _hover = hover;
        }

        public void SetVisualState(Color normal, Color hover)
        {
            _normal = normal;
            _hover = hover;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _tab?.InvokeBuilderAction();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _hover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _normal;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDAConstructionFieldActionButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAConstructionTab _tab;
        private Image _bg;
        private Color _normal;
        private Color _hover;

        public void Init(PDAConstructionTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normal = normal;
            _hover = hover;
        }

        public void SetVisualState(Color normal, Color hover)
        {
            _normal = normal;
            _hover = hover;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _tab?.InvokeFieldAction();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _hover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _normal;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDAConstructionDeployActionButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAConstructionTab _tab;
        private Image _bg;
        private Color _normal;
        private Color _hover;

        public void Init(PDAConstructionTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normal = normal;
            _hover = hover;
        }

        public void SetVisualState(Color normal, Color hover)
        {
            _normal = normal;
            _hover = hover;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _tab?.InvokeDeployAction();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _hover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _normal;
        }
    }
}
