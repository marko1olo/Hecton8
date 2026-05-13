// ============================================================================
// HECTON-8 — PDAInventoryTab.cs
// Vkladka inventarya vnutri PDA.
// Stroit UI programmno. Chitaet PlayerInventory i PlayerToolManager.
// Veshaetsya na GameObject vkladki Tab_Inventory.
// ============================================================================

using System;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.World;
using Hecton.Localization;
using Hecton8.Core;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    internal enum InventoryViewFilter
    {
        All = 0,
        Tools = 1,
        Consumables = 2,
        Materials = 3,
        Components = 4
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Inventory Tab")]
    public sealed class PDAInventoryTab : MonoBehaviour, IUpdatable, IPDAEventListener, ILocalizationCorruptionVisualStateListener
    {
        private static readonly char[] StackCountTemplateChars = "×{0}".ToCharArray();
        private static readonly char[] DetailWeightStackTemplateChars = "MASS: {0:0.0} kg  |  STACK x{1}  |  TOTAL {2:0.0} kg".ToCharArray();
        private static readonly char[] DetailWeightTemplateChars = "MASS: {0:0.0} kg".ToCharArray();
        private static readonly char[] CargoWeightTemplateChars = "CARGO: {0:0.0} kg  |  {1}/{2} CELLS".ToCharArray();
        private static readonly char[] CargoDigestPrefixAnchorsChars = "ANCHORS ".ToCharArray();
        private static readonly char[] CargoDigestUnitsChars = "  |  UNITS ".ToCharArray();
        private static readonly char[] CargoDigestFreeChars = "  |  FREE ".ToCharArray();
        private static readonly char[] CargoDigestLineBreakChars = "\n".ToCharArray();
        private static readonly char[] CargoDigestToolsChars = "TOOLS ".ToCharArray();
        private static readonly char[] CargoDigestConsumablesChars = "  |  CONS ".ToCharArray();
        private static readonly char[] CargoDigestMaterialsChars = "  |  MATS ".ToCharArray();
        private static readonly char[] CargoDigestComponentsChars = "  |  PARTS ".ToCharArray();
        private static readonly char[] CargoDigestMiscChars = "  |  MISC ".ToCharArray();
        private static readonly char[] FilterDigestPrefixChars = "FILTER: ".ToCharArray();
        private static readonly char[] FilterDigestShowingChars = "  |  SHOWING ".ToCharArray();
        private static readonly char[] FilterDigestItemsChars = " ITEMS".ToCharArray();
        private static readonly char[] FilterLabelAllChars = "ALL".ToCharArray();
        private static readonly char[] FilterLabelToolsChars = "TOOLS".ToCharArray();
        private static readonly char[] FilterLabelConsumablesChars = "CONS".ToCharArray();
        private static readonly char[] FilterLabelMaterialsChars = "MATS".ToCharArray();
        private static readonly char[] FilterLabelComponentsChars = "PARTS".ToCharArray();
        private static readonly char[] FilterEmptyLabelToolsChars = "TOOLS".ToCharArray();
        private static readonly char[] FilterEmptyLabelConsumablesChars = "CONSUMABLES".ToCharArray();
        private static readonly char[] FilterEmptyLabelMaterialsChars = "MATERIALS".ToCharArray();
        private static readonly char[] FilterEmptyLabelComponentsChars = "COMPONENTS".ToCharArray();
        private static readonly char[] PageDigestPrefixChars = "PAGE ".ToCharArray();
        private static readonly char[] EmptyTextChars = new char[1];
        // COLD ALLOC: string[4] — cached PDA tool-slot key labels — owner: PDAInventoryTab
        private static readonly string[] ToolSlotKeyLabels = { "1", "2", "3", "4" };
        // COLD ALLOC: string[5] — cached PDA tab bar labels — owner: PDAInventoryTab
        private static readonly string[] TabLabels = { "INVENTORY", "LOADOUT", "CONSTRUCT", "BARTER", "DATA LOG" };
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private HUDNotification hudNotification;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        // ══════════════════════════════════════════════════════════
        //  LAYOUT CONSTANTS
        // ══════════════════════════════════════════════════════════

        private const int GridCols = 8;
        private const int GridRows = 6;
        private const float CellSize = 52f;
        private const float CellGap = 3f;
        private const float CellStep = CellSize + CellGap;
        private const int MaxItems = 48;
        private const int MaxVisibleBlocks = 32;
        private const int ToolSlotCount = 4;

        private const float InventoryUiParallaxStrength = 0.018f;
        private static readonly int PdaInventoryParallaxId = Shader.PropertyToID("_HectonPdaInventoryParallax");
        private const int InventoryTabIndex = 0;

        // Grid area pixel dimensions
        private static readonly Vector2 GridAreaSize = new Vector2(
            GridCols * CellStep - CellGap,
            GridRows * CellStep - CellGap);

        // ══════════════════════════════════════════════════════════
        //  COLORS
        // ══════════════════════════════════════════════════════════

        private static readonly Color BgDark = new Color(0.02f, 0.06f, 0.08f, 0.92f);
        private static readonly Color CellEmpty = new Color(0.08f, 0.14f, 0.16f, 0.6f);
        private static readonly Color CellOccupied = new Color(0.1f, 0.2f, 0.22f, 0.7f);
        private static readonly Color ItemBlock = new Color(0.12f, 0.28f, 0.32f, 0.85f);
        private static readonly Color HoverTint = new Color(0.3f, 0.9f, 0.88f, 0.18f);
        private static readonly Color SelectTint = new Color(0.46f, 0.98f, 0.94f, 0.28f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 1f);
        private static readonly Color Dim = new Color(0.78f, 0.98f, 0.95f, 1f);
        private static readonly Color DimLow = new Color(0.5f, 0.7f, 0.68f, 1f);
        private static readonly Color TabActive = new Color(0.46f, 0.98f, 0.94f, 0.9f);
        private static readonly Color TabInactive = new Color(0.4f, 0.6f, 0.58f, 0.5f);
        private static readonly Color TabBgActive = new Color(0.1f, 0.22f, 0.24f, 0.7f);
        private static readonly Color TabBgInactive = new Color(0.05f, 0.1f, 0.12f, 0.4f);
        private static readonly Color ToolSlotBg = new Color(0.06f, 0.12f, 0.14f, 0.7f);
        private static readonly Color ToolSlotActive = new Color(0.46f, 0.98f, 0.94f, 0.3f);
        private static readonly Color RuleLine = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color DetailBg = new Color(0.04f, 0.08f, 0.1f, 0.8f);

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private bool _built;

        // Grid
        private RectTransform _gridArea;
        private Image[] _cellImages;

        // Item blocks pool
        private RectTransform[] _blockRects;
        private Image[] _blockBgs;
        private Image[] _blockIcons;
        private CanvasGroup[] _blockCanvasGroups;
        private int _activeBlockCount;
        // Stack count labels
        private TextMeshProUGUI[] _blockCounts;

        // Drop button
        private RectTransform _dropButtonRoot;
        private Image _dropButtonBg;
        private CanvasGroup _dropButtonCanvasGroup;

        // Highlights
        private RectTransform _hoverRect;
        private Image _hoverImage;
        private RectTransform _selectRect;
        private Image _selectImage;

        // Pointer overlay
        private RectTransform _pointerOverlay;
        private RectTransform _dragPreviewRect;
        private Image _dragPreviewImage;

        // Details panel
        private RectTransform _detailsRoot;
        private Image _detailIconBoxBg;
        private Image _detailNameBg;
        private Image _detailStatusBg;
        private Image _detailActionBg;
        private Image _detailIcon;
        private TextMeshProUGUI _detailName;
        private TextMeshProUGUI _detailDesc;
        private TextMeshProUGUI _detailWeight;
        private TextMeshProUGUI _detailSize;
        private TextMeshProUGUI _detailEffect;
        private TextMeshProUGUI _detailStatus;
        private TextMeshProUGUI _detailAction;
        private TextMeshProUGUI _detailHint;
        private LocalizedTextMadnessFx _detailDescMadnessFx;

        // Tool strip
        private RectTransform _toolStripRoot;
        private Image[] _toolSlotBgs;
        private Image[] _toolSlotIcons;
        private TextMeshProUGUI[] _toolSlotKeys;

        // Weight
        private TextMeshProUGUI _weightLabel;
        private TextMeshProUGUI _cargoSummary;
        private TextMeshProUGUI _filterSummary;
        private TextMeshProUGUI _pageSummary;
        private TextMeshProUGUI _gridSectionLabel;
        private TextMeshProUGUI _detailsSectionLabel;
        private TextMeshProUGUI _toolStripSectionLabel;

        // Tab bar
        private RectTransform _tabBarRoot;
        private PDATabButton[] _tabButtons;
        private RectTransform _filterBarRoot;
        private PDAInventoryFilterButton[] _filterButtons;
        private PDAInventoryPageButton _previousPageButton;
        private PDAInventoryPageButton _nextPageButton;
        private RectTransform _toolSlotRowRoot;

        // USE button
        private RectTransform _useButtonRoot;
        private Image _useButtonBg;
        private TextMeshProUGUI _useButtonLabel;
        private CanvasGroup _useButtonCanvasGroup;
        private RectTransform _loadoutAssignRoot;
        private Image[] _loadoutAssignBgs;
        private TextMeshProUGUI[] _loadoutAssignLabels;
        private CanvasGroup _loadoutAssignCanvasGroup;

        // SORT button
        private RectTransform _sortButtonRoot;

        // Audio
        [Header("── Audio ─────────────────────────────────────")]
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip dropSound;
        [SerializeField] private AudioClip useSound;
        [SerializeField] private AudioClip sortSound;
        [SerializeField] [Range(0f, 1f)] private float uiVolume = 0.5f;

        // Selection state
        private int _selectedX = -1;
        private int _selectedY = -1;
        private ItemData _selectedItem;
        private int _selectedItemHashId;
        private int _hoverX = -1;
        private int _hoverY = -1;
        private bool _dragActive;
        private int _dragSourceX = -1;
        private int _dragSourceY = -1;
        private int _dragSourceHashId;
        private InventoryViewFilter _currentFilter = InventoryViewFilter.All;
        private int _visiblePlacementCount;
        private int _currentPageIndex;
        private int _filteredAnchorCount;

        // Placement buffer (pre-allocated)
        private char[] _cargoSummaryBuffer;
        private char[] _filterSummaryBuffer;
        private char[] _pageSummaryBuffer;
        private char[] _detailTextBuffer;
        private char[] _loadoutAssignTextBuffer;
        private FixedCharBuffer _notificationBuffer = new FixedCharBuffer(192); // COLD ALLOC: char[192] - inventory HUD notification staging buffer - owner: PDAInventoryTab
        private int[] _filteredAnchorIndices;
        private bool _gridDirty;
        private bool _detailsDirty;
        private bool _toolStripDirty;

        private bool _registeredToUpdateLoop;
        private Transform _dropOrigin;

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == InventoryTabIndex;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cargoSummaryBuffer = new char[160]; // COLD ALLOC: char[160] - cargo digest staging buffer - owner: PDAInventoryTab
            _filterSummaryBuffer = new char[80]; // COLD ALLOC: char[80] - filter digest staging buffer - owner: PDAInventoryTab
            _pageSummaryBuffer = new char[32]; // COLD ALLOC: char[32] - page digest staging buffer - owner: PDAInventoryTab
            _detailTextBuffer = new char[384]; // COLD ALLOC: char[384] - selected item detail text staging buffer - owner: PDAInventoryTab
            _loadoutAssignTextBuffer = new char[32]; // COLD ALLOC: char[32] - loadout assign label staging buffer - owner: PDAInventoryTab
            _filteredAnchorIndices = new int[MaxItems]; // COLD ALLOC: int[MaxItems] - filtered anchor page index buffer - owner: PDAInventoryTab
        }

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            TryRegisterTick();
            MarkAllDirty();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
            TryUnregisterTick();
            Shader.SetGlobalVector(PdaInventoryParallaxId, Vector4.zero);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            PDAEvents.AssertUnregistered(this, nameof(PDAInventoryTab));
            Shader.SetGlobalVector(PdaInventoryParallaxId, Vector4.zero);
        }

        public void Tick(float deltaTime)
        {
            if (!IsTabActive)
            {
                Shader.SetGlobalVector(PdaInventoryParallaxId, Vector4.zero);
                return;
            }

            PublishInventoryUiParallax();
        }

        private void TryRegisterTick()
        {
            if (_registeredToUpdateLoop || !Application.isPlaying)
                return;
            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToUpdateLoop = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredToUpdateLoop)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToUpdateLoop = false;
        }

        // ══════════════════════════════════════════════════════════
        //  AUTO-RESOLVE
        // ══════════════════════════════════════════════════════════

        private void AutoResolve()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (_dropOrigin == null && playerContext != null)
                _dropOrigin = playerContext.PlayerCamera != null
                    ? playerContext.PlayerCamera.transform
                    : playerContext.PlayerTransform;
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
                if (_dropOrigin == null)
                {
                    Camera playerCamera = null;
                    if (!playerTransform.TryGetComponent(out playerCamera))
                        playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
                    _dropOrigin = playerCamera != null ? playerCamera.transform : playerTransform;
                }

                if (playerInventory == null)
                    playerTransform.TryGetComponent(out playerInventory);

                if (toolManager == null)
                    playerTransform.TryGetComponent(out toolManager);

                if (playerPDA == null)
                    playerTransform.TryGetComponent(out playerPDA);
            }

            if (playerPDA == null)
                playerPDA = GetComponentInParent<PlayerPDA>();
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        private void PublishInventoryUiParallax()
        {
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            if (screenWidth <= 1f || screenHeight <= 1f)
            {
                Shader.SetGlobalVector(PdaInventoryParallaxId, Vector4.zero);
                return;
            }

            Vector3 pointerPosition = UnityEngine.Input.mousePosition;
            float halfWidth = screenWidth * 0.5f;
            float halfHeight = screenHeight * 0.5f;
            float parallaxX = Mathf.Clamp((pointerPosition.x - halfWidth) / halfWidth, -1f, 1f) * InventoryUiParallaxStrength;
            float parallaxY = Mathf.Clamp((pointerPosition.y - halfHeight) / halfHeight, -1f, 1f) * InventoryUiParallaxStrength;
            Shader.SetGlobalVector(PdaInventoryParallaxId, new Vector4(parallaxX, parallaxY, 1f, 0f));
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT SUBSCRIPTIONS
        // ══════════════════════════════════════════════════════════

        private void Subscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged += OnInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged += OnToolSlotChanged;
                toolManager.ToolAssignmentsChanged += OnToolAssignmentsChanged;
            }
            PDAEvents.Register(this);
            LocalizationEvents.RegisterCorruptionVisualStateListener(this);
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged -= OnInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged -= OnToolSlotChanged;
                toolManager.ToolAssignmentsChanged -= OnToolAssignmentsChanged;
            }
            PDAEvents.Unregister(this);
            LocalizationEvents.UnregisterCorruptionVisualStateListener(this);
        }

        private void OnInventoryChanged()
        {
            _gridDirty = true;
            _detailsDirty = true;
            if (IsTabActive)
                FlushPendingRefresh();
        }

        public void OnLocalizationCorruptionVisualStateChanged(in LocalizationEventPayload payload)

        {

            OnCorruptionVisualStateChanged();

        }


        private void OnCorruptionVisualStateChanged()
        {
            _detailsDirty = true;
            if (IsTabActive)
                FlushPendingRefresh();
        }

        private void OnToolSlotChanged(int _)
        {
            _toolStripDirty = true;
            _detailsDirty = true;
            if (IsTabActive)
                FlushPendingRefresh();
        }

        private void OnToolAssignmentsChanged()
        {
            _toolStripDirty = true;
            _detailsDirty = true;
            if (IsTabActive)
                FlushPendingRefresh();
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    OnPdaOpened(payload.CurrentTab);
                    break;
                case PDAEventType.TabChanged:
                    OnTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        private void OnPdaOpened(int tab)
        {
            if (tab == InventoryTabIndex)
                FlushPendingRefresh(forceAll: true);
        }

        private void OnTabChanged(int oldTab, int newTab)
        {
            if (_tabButtons != null)
            {
                for (int i = 0; i < _tabButtons.Length; i++)
                {
                    if (_tabButtons[i] != null)
                        _tabButtons[i].SetActive(i == newTab);
                }
            }

            if (newTab == InventoryTabIndex)
                FlushPendingRefresh(forceAll: true);
        }

        // ══════════════════════════════════════════════════════════
        //  BUILD UI HIERARCHY
        // ══════════════════════════════════════════════════════════

        private void EnsureBuilt()
        {
            if (_built) return;

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            ClearChildren(self);

            // Background
            Image bg = CreateImage("InvBg", self, BgDark);
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            // Tab bar (on parent so it persists across tab switches)
            BuildTabBar();

            // Grid area
            BuildGrid(self);

            // Details panel
            BuildDetails(self);

            // Tool strip
            BuildToolStrip(self);

            // Cargo digest
            BuildCargoDigest(self);

            // Weight label
            BuildWeightLabel(self);

            // Separator lines
            BuildChromeLines(self);

            _built = true;
        }

        // ──────────────────────────────────────────────────────────
        //  TAB BAR
        // ──────────────────────────────────────────────────────────

        private void BuildTabBar()
        {
            RectTransform parent = transform.parent as RectTransform;
            if (parent == null) return;

            string[] labels = TabLabels;
            Transform existing = parent.Find("PDA_TabBar");
            if (existing != null)
            {
                _tabBarRoot = existing as RectTransform;
                HorizontalLayoutGroup existingLayoutGroup = EnsureHorizontalLayout(_tabBarRoot, 6f, TextAnchor.MiddleCenter);
                LocalizedLayoutMirror.ConfigureRuntime(existingLayoutGroup, _tabBarRoot, true, true, false);
                if (TryCacheExistingTabButtons(labels.Length))
                    return;

                for (int i = _tabBarRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = _tabBarRoot.GetChild(i);
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
            else
            {
                _tabBarRoot = CreateRect("PDA_TabBar", parent);
                Anchor(_tabBarRoot, new Vector2(0f, 1f), new Vector2(1f, 1f),
                       new Vector2(0f, -4f), new Vector2(0f, 36f));
            }

            EnsureTabButtonCache(labels.Length);
            float tabWidth = 126f;
            HorizontalLayoutGroup layoutGroup = EnsureHorizontalLayout(_tabBarRoot, 6f, TextAnchor.MiddleCenter);
            LocalizedLayoutMirror.ConfigureRuntime(layoutGroup, _tabBarRoot, true, true, false);

            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform tabRect = CreateRect("Tab_" + i, _tabBarRoot);
                tabRect.sizeDelta = new Vector2(tabWidth, 30f);
                EnsureLayoutElement(tabRect, tabWidth, 30f);

                Image tabBg = tabRect.gameObject.AddComponent<Image>();
                tabBg.color = i == 0 ? TabBgActive : TabBgInactive;
                tabBg.raycastTarget = true;

                Button tabButton = tabRect.gameObject.AddComponent<Button>(); // COLD ALLOC: Button[1] — runtime PDA tab click component — owner: PDAInventoryTab
                tabButton.transition = Selectable.Transition.None;
                tabButton.targetGraphic = tabBg;

                TextMeshProUGUI tabLabel = CreateText("Label", tabRect, 11f,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(tabLabel.rectTransform);
                SetBufferedText(tabLabel, labels[i]);
                tabLabel.color = i == 0 ? TabActive : TabInactive;

                PDATabButton btn = tabRect.gameObject.AddComponent<PDATabButton>();
                btn.Init(i, playerPDA, tabBg, tabLabel,
                         TabBgActive, TabBgInactive, TabActive, TabInactive);
                _tabButtons[i] = btn;
            }
        }

        private bool TryCacheExistingTabButtons(int expectedCount)
        {
            if (_tabBarRoot == null || _tabBarRoot.childCount != expectedCount)
                return false;

            EnsureTabButtonCache(expectedCount);

            for (int i = 0; i < expectedCount; i++)
            {
                Transform child = _tabBarRoot.GetChild(i);
                if (child == null || !child.TryGetComponent(out PDATabButton button))
                {
                    ClearTabButtonCache();
                    return false;
                }

                _tabButtons[i] = button;
            }

            return true;
        }

        private void EnsureTabButtonCache(int expectedCount)
        {
            if (_tabButtons == null || _tabButtons.Length != expectedCount)
            {
                _tabButtons = new PDATabButton[expectedCount]; // COLD ALLOC: PDATabButton[5] — PDA tab button cache — owner: PDAInventoryTab
                return;
            }

            ClearTabButtonCache();
        }

        private void ClearTabButtonCache()
        {
            if (_tabButtons == null)
                return;

            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i] = null;
        }

        // ──────────────────────────────────────────────────────────
        //  GRID
        // ──────────────────────────────────────────────────────────

        private void BuildGrid(RectTransform parent)
        {
            _gridSectionLabel = CreateText("GridSectionLabel", parent, 10f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            _gridSectionLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _gridSectionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _gridSectionLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            _gridSectionLabel.rectTransform.anchoredPosition = new Vector2(32f, -52f);
            _gridSectionLabel.rectTransform.sizeDelta = new Vector2(220f, 18f);
            _gridSectionLabel.color = A(Primary, 0.78f);
            SetBufferedText(_gridSectionLabel, "CARGO GRID");

            _gridArea = CreateRect("GridArea", parent);
            _gridArea.pivot = new Vector2(0f, 1f);
            _gridArea.anchorMin = new Vector2(0f, 1f);
            _gridArea.anchorMax = new Vector2(0f, 1f);
            _gridArea.anchoredPosition = new Vector2(32f, -74f);
            _gridArea.sizeDelta = GridAreaSize;

            // Cell backgrounds
            _cellImages = new Image[GridCols * GridRows];
            for (int y = 0; y < GridRows; y++)
            {
                for (int x = 0; x < GridCols; x++)
                {
                    int idx = y * GridCols + x;
                    Image cell = CreateImage("Cell_" + x + "_" + y, _gridArea, CellEmpty);
                    cell.raycastTarget = false;
                    RectTransform cr = cell.rectTransform;
                    cr.pivot = new Vector2(0f, 1f);
                    cr.anchorMin = new Vector2(0f, 1f);
                    cr.anchorMax = new Vector2(0f, 1f);
                    cr.anchoredPosition = new Vector2(x * CellStep, -y * CellStep);
                    cr.sizeDelta = new Vector2(CellSize, CellSize);
                    _cellImages[idx] = cell;
                }
            }

            // Hover highlight
            _hoverRect = CreateRect("Hover", _gridArea);
            _hoverImage = _hoverRect.gameObject.AddComponent<Image>();
            _hoverImage.color = HoverTint;
            _hoverImage.raycastTarget = false;
            _hoverRect.pivot = new Vector2(0f, 1f);
            _hoverRect.anchorMin = new Vector2(0f, 1f);
            _hoverRect.anchorMax = new Vector2(0f, 1f);
            _hoverImage.enabled = false;

            // Selection highlight
            _selectRect = CreateRect("Select", _gridArea);
            _selectImage = _selectRect.gameObject.AddComponent<Image>();
            _selectImage.color = SelectTint;
            _selectImage.raycastTarget = false;
            _selectRect.pivot = new Vector2(0f, 1f);
            _selectRect.anchorMin = new Vector2(0f, 1f);
            _selectRect.anchorMax = new Vector2(0f, 1f);
            _selectImage.enabled = false;

            // Item block pool
            _blockRects = new RectTransform[MaxVisibleBlocks];
            _blockBgs = new Image[MaxVisibleBlocks];
            _blockIcons = new Image[MaxVisibleBlocks];
            _blockCanvasGroups = new CanvasGroup[MaxVisibleBlocks];
            _blockCounts = new TextMeshProUGUI[MaxVisibleBlocks];

            for (int i = 0; i < MaxVisibleBlocks; i++)
            {
                RectTransform br = CreateRect("Block_" + i, _gridArea);
                br.pivot = new Vector2(0f, 1f);
                br.anchorMin = new Vector2(0f, 1f);
                br.anchorMax = new Vector2(0f, 1f);

                Image bbg = br.gameObject.AddComponent<Image>();
                bbg.color = ItemBlock;
                bbg.raycastTarget = false;
                CanvasGroup blockCanvasGroup = EnsureCanvasGroup(br);
                SetCanvasGroupVisible(blockCanvasGroup, false);

                RectTransform iconRect = CreateRect("Icon", br);
                Stretch(iconRect, 6f, 6f, 6f, 6f);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = Color.white;
                icon.enabled = false;
                // Stack count label (bottom-right corner)
                TextMeshProUGUI countLbl = CreateText("Count", br, 11f,
                    FontStyles.Bold, TextAlignmentOptions.BottomRight);
                countLbl.rectTransform.anchorMin = Vector2.zero;
                countLbl.rectTransform.anchorMax = Vector2.one;
                countLbl.rectTransform.offsetMin = new Vector2(2f, 1f);
                countLbl.rectTransform.offsetMax = new Vector2(-4f, -2f);
                countLbl.color = Color.white;
                countLbl.enableAutoSizing = false;
                countLbl.raycastTarget = false;
                countLbl.enabled = false;

                _blockCounts[i] = countLbl;

                _blockRects[i] = br;
                _blockBgs[i] = bbg;
                _blockIcons[i] = icon;
                _blockCanvasGroups[i] = blockCanvasGroup;
            }

            // Pointer overlay (transparent, catches all clicks)
            _pointerOverlay = CreateRect("PointerOverlay", _gridArea);
            _pointerOverlay.pivot = new Vector2(0f, 1f);
            _pointerOverlay.anchorMin = new Vector2(0f, 1f);
            _pointerOverlay.anchorMax = new Vector2(0f, 1f);
            _pointerOverlay.anchoredPosition = Vector2.zero;
            _pointerOverlay.sizeDelta = GridAreaSize;

            _dragPreviewRect = CreateRect("DragPreview", _gridArea);
            _dragPreviewRect.pivot = new Vector2(0f, 1f);
            _dragPreviewRect.anchorMin = new Vector2(0f, 1f);
            _dragPreviewRect.anchorMax = new Vector2(0f, 1f);
            _dragPreviewRect.anchoredPosition = Vector2.zero;
            _dragPreviewRect.sizeDelta = Vector2.zero;
            _dragPreviewImage = _dragPreviewRect.gameObject.AddComponent<Image>();
            _dragPreviewImage.color = new Color(0.46f, 0.98f, 0.94f, 0.18f);
            _dragPreviewImage.preserveAspect = true;
            _dragPreviewImage.raycastTarget = false;
            _dragPreviewImage.enabled = false;
            _dragPreviewRect.SetAsLastSibling();

            _pointerOverlay.SetAsLastSibling();

            Image overlayImg = _pointerOverlay.gameObject.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0f);
            overlayImg.raycastTarget = true;

            GridPointerHandler handler = _pointerOverlay.gameObject.AddComponent<GridPointerHandler>();
            handler.Init(this);
        }

        // ──────────────────────────────────────────────────────────
        //  DETAILS PANEL
        // ──────────────────────────────────────────────────────────

        private void BuildDetails(RectTransform parent)
        {
            _detailsSectionLabel = CreateText("DetailsSectionLabel", parent, 10f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            _detailsSectionLabel.rectTransform.pivot = new Vector2(1f, 1f);
            _detailsSectionLabel.rectTransform.anchorMin = new Vector2(1f, 1f);
            _detailsSectionLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detailsSectionLabel.rectTransform.anchoredPosition = new Vector2(-28f, -52f);
            _detailsSectionLabel.rectTransform.sizeDelta = new Vector2(220f, 18f);
            _detailsSectionLabel.color = A(Primary, 0.78f);
            SetBufferedText(_detailsSectionLabel, "ITEM ANALYSIS");

            _detailsRoot = CreateRect("DetailsPanel", parent);
            _detailsRoot.pivot = new Vector2(1f, 1f);
            _detailsRoot.anchorMin = new Vector2(1f, 1f);
            _detailsRoot.anchorMax = new Vector2(1f, 1f);
            _detailsRoot.anchoredPosition = new Vector2(-28f, -74f);
            _detailsRoot.sizeDelta = new Vector2(260f, GridAreaSize.y);

            Image detBg = _detailsRoot.gameObject.AddComponent<Image>();
            detBg.color = DetailBg;
            detBg.raycastTarget = false;

            // Icon
            RectTransform iconBox = CreateRect("DetailIconBox", _detailsRoot);
            Anchor(iconBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                   new Vector2(0f, -20f), new Vector2(80f, 80f));
            _detailIconBoxBg = iconBox.gameObject.AddComponent<Image>();
            _detailIconBoxBg.color = new Color(0.08f, 0.18f, 0.2f, 0.74f);
            _detailIconBoxBg.raycastTarget = false;
            RectTransform iconVisual = CreateRect("DetailIconVisual", iconBox);
            Stretch(iconVisual, 8f, 8f, 8f, 8f);
            _detailIcon = iconVisual.gameObject.AddComponent<Image>();
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;
            _detailIcon.color = Color.white;

            // Name
            RectTransform nameBg = CreateRect("DetailNameBg", _detailsRoot);
            Anchor(nameBg, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f),
                   new Vector2(0f, -112f), new Vector2(0f, 24f));
            _detailNameBg = nameBg.gameObject.AddComponent<Image>();
            _detailNameBg.color = new Color(0.08f, 0.18f, 0.2f, 0.5f);
            _detailNameBg.raycastTarget = false;

            _detailName = CreateText("DetailName", _detailsRoot, 16f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(_detailName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(0f, -110f), new Vector2(-20f, 24f));
            _detailName.color = A(Primary, 0.95f);

            // Description
            _detailDesc = CreateText("DetailDesc", _detailsRoot, 11f,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(_detailDesc.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(14f, -144f), new Vector2(-28f, 80f));
            _detailDesc.color = A(Dim, 0.7f);
            _detailDesc.textWrappingMode = TextWrappingModes.Normal;
            _detailDescMadnessFx = _detailDesc.gameObject.GetComponent<LocalizedTextMadnessFx>();
            if (_detailDescMadnessFx == null)
                _detailDescMadnessFx = _detailDesc.gameObject.AddComponent<LocalizedTextMadnessFx>();

            _detailDescMadnessFx.Bind(_detailDesc);

            // Rule line
            Image detRule = CreateImage("DetailRule", _detailsRoot, A(RuleLine, 0.5f));
            Anchor(detRule.rectTransform, new Vector2(0.1f, 1f), new Vector2(0.9f, 1f),
                   new Vector2(0f, -232f), new Vector2(0f, 1f));
            detRule.raycastTarget = false;

            // Weight
            _detailWeight = CreateText("DetailWeight", _detailsRoot, 13f,
                FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(_detailWeight.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(14f, -242f), new Vector2(-28f, 20f));
            _detailWeight.color = A(DimLow, 0.8f);

            // Size
            _detailSize = CreateText("DetailSize", _detailsRoot, 13f,
                FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(_detailSize.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(14f, -264f), new Vector2(-28f, 20f));
            _detailSize.color = A(DimLow, 0.8f);

            _detailEffect = CreateText("DetailEffect", _detailsRoot, 12f,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(_detailEffect.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(14f, -294f), new Vector2(-28f, 52f));
            _detailEffect.color = A(Dim, 0.74f);
            _detailEffect.textWrappingMode = TextWrappingModes.Normal;

            _detailStatus = CreateText("DetailStatus", _detailsRoot, 11.5f,
                FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Anchor(_detailStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(14f, -352f), new Vector2(-28f, 34f));
            _detailStatus.color = A(Primary, 0.86f);
            _detailStatus.textWrappingMode = TextWrappingModes.Normal;
            RectTransform statusBg = CreateRect("DetailStatusBg", _detailsRoot);
            Anchor(statusBg, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f),
                   new Vector2(0f, -350f), new Vector2(0f, 34f));
            _detailStatusBg = statusBg.gameObject.AddComponent<Image>();
            _detailStatusBg.color = new Color(0.08f, 0.2f, 0.22f, 0.68f);
            _detailStatusBg.raycastTarget = false;
            _detailStatus.transform.SetAsLastSibling();

            _detailAction = CreateText("DetailAction", _detailsRoot, 11.5f,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            Anchor(_detailAction.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(14f, -390f), new Vector2(-28f, 56f));
            _detailAction.color = A(DimLow, 0.78f);
            _detailAction.textWrappingMode = TextWrappingModes.Normal;
            RectTransform actionBg = CreateRect("DetailActionBg", _detailsRoot);
            Anchor(actionBg, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f),
                   new Vector2(0f, -388f), new Vector2(0f, 56f));
            _detailActionBg = actionBg.gameObject.AddComponent<Image>();
            _detailActionBg.color = new Color(0.06f, 0.12f, 0.14f, 0.58f);
            _detailActionBg.raycastTarget = false;
            _detailAction.transform.SetAsLastSibling();

            // Hint (shown when nothing selected)
            _detailHint = CreateText("DetailHint", _detailsRoot, 12f,
                FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_detailHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                   Vector2.zero, Vector2.zero);
            _detailHint.color = A(DimLow, 0.4f);
            SetBufferedText(_detailHint, "SELECT AN ITEM");
            // Drop button
            _dropButtonRoot = CreateRect("DropButton", _detailsRoot);
            Anchor(_dropButtonRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(120f, 30f));

            _dropButtonBg = _dropButtonRoot.gameObject.AddComponent<Image>();
            _dropButtonBg.color = new Color(0.6f, 0.15f, 0.12f, 0.6f);
            _dropButtonBg.raycastTarget = true;

            TextMeshProUGUI dropLabel = CreateText("DropLabel", _dropButtonRoot, 12f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(dropLabel.rectTransform);
            SetBufferedText(dropLabel, "DROP");
            dropLabel.color = new Color(1f, 0.85f, 0.8f, 0.9f);

            DropItemButton dropBtn = _dropButtonRoot.gameObject.AddComponent<DropItemButton>();
            dropBtn.Init(this, _dropButtonBg,
                new Color(0.6f, 0.15f, 0.12f, 0.6f),
                new Color(0.8f, 0.2f, 0.15f, 0.8f));

            _dropButtonCanvasGroup = EnsureCanvasGroup(_dropButtonRoot);
            SetCanvasGroupVisible(_dropButtonCanvasGroup, false);

            // USE button
            _useButtonRoot = CreateRect("UseButton", _detailsRoot);
            Anchor(_useButtonRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 56f), new Vector2(120f, 30f));

            _useButtonBg = _useButtonRoot.gameObject.AddComponent<Image>();
            _useButtonBg.color = new Color(0.1f, 0.4f, 0.35f, 0.6f);
            _useButtonBg.raycastTarget = true;

            _useButtonLabel = CreateText("UseLabel", _useButtonRoot, 12f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_useButtonLabel.rectTransform);
            SetBufferedText(_useButtonLabel, "USE");
            _useButtonLabel.color = new Color(0.7f, 1f, 0.95f, 0.9f);

            UseItemButton useBtn = _useButtonRoot.gameObject.AddComponent<UseItemButton>();
            useBtn.Init(this, _useButtonBg,
                new Color(0.1f, 0.4f, 0.35f, 0.6f),
                new Color(0.15f, 0.55f, 0.48f, 0.8f));

            _useButtonCanvasGroup = EnsureCanvasGroup(_useButtonRoot);
            SetCanvasGroupVisible(_useButtonCanvasGroup, false);

            BuildLoadoutAssignButtons();
        }

        private void BuildLoadoutAssignButtons()
        {
            _loadoutAssignRoot = CreateRect("LoadoutAssignRoot", _detailsRoot);
            Anchor(_loadoutAssignRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 112f), new Vector2(212f, 64f));

            RectTransform topRow = CreateRect("LoadoutAssignRowTop", _loadoutAssignRoot);
            Anchor(topRow, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 26f));
            HorizontalLayoutGroup topRowLayout = EnsureHorizontalLayout(topRow, 8f, TextAnchor.UpperLeft);
            LocalizedLayoutMirror.ConfigureRuntime(topRowLayout, topRow, true, true, false);

            RectTransform bottomRow = CreateRect("LoadoutAssignRowBottom", _loadoutAssignRoot);
            Anchor(bottomRow, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -32f), new Vector2(0f, 26f));
            HorizontalLayoutGroup bottomRowLayout = EnsureHorizontalLayout(bottomRow, 8f, TextAnchor.UpperLeft);
            LocalizedLayoutMirror.ConfigureRuntime(bottomRowLayout, bottomRow, true, true, false);

            _loadoutAssignBgs = new Image[ToolSlotCount];
            _loadoutAssignLabels = new TextMeshProUGUI[ToolSlotCount];

            for (int i = 0; i < ToolSlotCount; i++)
            {
                int row = i / 2;
                RectTransform rowRoot = row == 0 ? topRow : bottomRow;

                RectTransform btn = CreateRect("AssignSlot_" + i, rowRoot);
                btn.sizeDelta = new Vector2(100f, 26f);
                EnsureLayoutElement(btn, 100f, 26f);

                Image bg = btn.gameObject.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
                bg.raycastTarget = true;
                _loadoutAssignBgs[i] = bg;

                TextMeshProUGUI label = CreateText("Label", btn, 10f,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(label.rectTransform);
                SetLoadoutAssignLabel(label, i, isAssigned: false, isRecommended: false);
                label.color = A(Dim, 0.78f);
                _loadoutAssignLabels[i] = label;

                LoadoutAssignButton assignButton = btn.gameObject.AddComponent<LoadoutAssignButton>();
                assignButton.Init(this, i, bg,
                    new Color(0.08f, 0.16f, 0.18f, 0.58f),
                    new Color(0.12f, 0.25f, 0.28f, 0.82f));
            }

            _loadoutAssignCanvasGroup = EnsureCanvasGroup(_loadoutAssignRoot);
            SetCanvasGroupVisible(_loadoutAssignCanvasGroup, false);
        }

        // ──────────────────────────────────────────────────────────
        //  TOOL STRIP
        // ──────────────────────────────────────────────────────────

        private void BuildToolStrip(RectTransform parent)
        {
            _toolStripSectionLabel = CreateText("ToolStripSectionLabel", parent, 10f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            _toolStripSectionLabel.rectTransform.pivot = new Vector2(0f, 0f);
            _toolStripSectionLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _toolStripSectionLabel.rectTransform.anchorMax = new Vector2(0f, 0f);
            _toolStripSectionLabel.rectTransform.anchoredPosition = new Vector2(32f, 74f);
            _toolStripSectionLabel.rectTransform.sizeDelta = new Vector2(260f, 18f);
            _toolStripSectionLabel.color = A(Primary, 0.78f);
            SetBufferedText(_toolStripSectionLabel, "QUICK ACCESS MATRIX");

            _toolStripRoot = CreateRect("ToolStrip", parent);
            _toolStripRoot.pivot = new Vector2(0f, 0f);
            _toolStripRoot.anchorMin = new Vector2(0f, 0f);
            _toolStripRoot.anchorMax = new Vector2(0f, 0f);
            _toolStripRoot.anchoredPosition = new Vector2(32f, 16f);

            float slotSize = 48f;
            float slotGap = 4f;
            float stripW = ToolSlotCount * (slotSize + slotGap) - slotGap;
            _toolStripRoot.sizeDelta = new Vector2(stripW, slotSize + 18f);

            // Header
            TextMeshProUGUI hdr = CreateText("ToolHdr", _toolStripRoot, 9f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(hdr.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(2f, 0f), new Vector2(0f, 14f));
            SetBufferedText(hdr, "FIELD LOADOUT");
            hdr.color = A(DimLow, 0.6f);

            _toolSlotRowRoot = CreateRect("ToolSlotRow", _toolStripRoot);
            Anchor(_toolSlotRowRoot, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(stripW, slotSize));
            HorizontalLayoutGroup toolLayout = EnsureHorizontalLayout(_toolSlotRowRoot, slotGap, TextAnchor.LowerLeft);
            LocalizedLayoutMirror.ConfigureRuntime(toolLayout, _toolSlotRowRoot, true, true, false);

            _toolSlotBgs = new Image[ToolSlotCount];
            _toolSlotIcons = new Image[ToolSlotCount];
            _toolSlotKeys = new TextMeshProUGUI[ToolSlotCount];

            for (int i = 0; i < ToolSlotCount; i++)
            {
                RectTransform slot = CreateRect("ToolSlot_" + i, _toolSlotRowRoot);
                slot.sizeDelta = new Vector2(slotSize, slotSize);
                EnsureLayoutElement(slot, slotSize, slotSize);

                Image bg = slot.gameObject.AddComponent<Image>();
                bg.color = ToolSlotBg;
                bg.raycastTarget = false;
                _toolSlotBgs[i] = bg;

                RectTransform iconR = CreateRect("Icon", slot);
                Stretch(iconR, 8f, 8f, 8f, 8f);
                Image icon = iconR.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = Color.white;
                icon.enabled = false;
                _toolSlotIcons[i] = icon;

                TextMeshProUGUI keyLbl = CreateText("Key", slot, 9f,
                    FontStyles.Bold, TextAlignmentOptions.TopLeft);
                Anchor(keyLbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                       new Vector2(4f, -3f), new Vector2(16f, 14f));
                SetBufferedText(keyLbl, ToolSlotKeyLabels[i]);
                keyLbl.color = A(DimLow, 0.5f);
                _toolSlotKeys[i] = keyLbl;
            }
        }

        private void BuildCargoDigest(RectTransform parent)
        {
            RectTransform root = CreateRect("CargoDigest", parent);
            root.pivot = new Vector2(0f, 0f);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(32f, 78f);
            root.sizeDelta = new Vector2(GridAreaSize.x, 58f);

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.09f, 0.11f, 0.75f);
            bg.raycastTarget = false;

            TextMeshProUGUI hdr = CreateText("CargoDigestHeader", root, 9f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            hdr.rectTransform.anchorMin = new Vector2(0f, 1f);
            hdr.rectTransform.anchorMax = new Vector2(1f, 1f);
            hdr.rectTransform.offsetMin = new Vector2(12f, -14f);
            hdr.rectTransform.offsetMax = new Vector2(-12f, 0f);
            hdr.color = A(DimLow, 0.62f);
            SetBufferedText(hdr, "CARGO DIGEST");

            _cargoSummary = CreateText("CargoSummary", root, 10.5f,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(_cargoSummary.rectTransform, 12f, 12f, 20f, 20f);
            _cargoSummary.color = A(Dim, 0.76f);

            _filterSummary = CreateText("FilterSummary", root, 9f,
                FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            _filterSummary.rectTransform.anchorMin = new Vector2(0f, 0f);
            _filterSummary.rectTransform.anchorMax = new Vector2(1f, 0f);
            _filterSummary.rectTransform.offsetMin = new Vector2(12f, 6f);
            _filterSummary.rectTransform.offsetMax = new Vector2(-12f, 20f);
            _filterSummary.color = A(Primary, 0.72f);

            _pageSummary = CreateText("PageSummary", root, 9f,
                FontStyles.Bold, TextAlignmentOptions.BottomRight);
            _pageSummary.rectTransform.anchorMin = new Vector2(0f, 0f);
            _pageSummary.rectTransform.anchorMax = new Vector2(1f, 0f);
            _pageSummary.rectTransform.offsetMin = new Vector2(12f, 6f);
            _pageSummary.rectTransform.offsetMax = new Vector2(-12f, 20f);
            _pageSummary.color = A(DimLow, 0.78f);
        }

        // ──────────────────────────────────────────────────────────
        //  WEIGHT LABEL
        // ──────────────────────────────────────────────────────────

        private void BuildWeightLabel(RectTransform parent)
        {
            _weightLabel = CreateText("WeightLabel", parent, 12f,
                FontStyles.Normal, TextAlignmentOptions.Right);
            _weightLabel.rectTransform.pivot = new Vector2(1f, 0f);
            _weightLabel.rectTransform.anchorMin = new Vector2(1f, 0f);
            _weightLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            _weightLabel.rectTransform.anchoredPosition = new Vector2(-28f, 20f);
            _weightLabel.rectTransform.sizeDelta = new Vector2(200f, 18f);
            _weightLabel.color = A(DimLow, 0.7f);
        }

        // ──────────────────────────────────────────────────────────
        //  CHROME LINES
        // ──────────────────────────────────────────────────────────

        private void BuildChromeLines(RectTransform parent)
        {
            // Horizontal rule below tab bar
            Image topRule = CreateImage("TopRule", parent, A(RuleLine, 0.4f));
            Anchor(topRule.rectTransform, new Vector2(0.02f, 1f), new Vector2(0.98f, 1f),
                   new Vector2(0f, -46f), new Vector2(0f, 1f));
            topRule.raycastTarget = false;

            Image bottomRule = CreateImage("BottomRule", parent, A(RuleLine, 0.28f));
            Anchor(bottomRule.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.98f, 0f),
                   new Vector2(0f, 56f), new Vector2(0f, 1f));
            bottomRule.raycastTarget = false;

            // Vertical separator between grid and details
            float sepX = 32f + GridAreaSize.x + 14f;
            Image vSep = CreateImage("VSep", parent, A(RuleLine, 0.3f));
            vSep.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            vSep.rectTransform.anchorMin = new Vector2(0f, 0f);
            vSep.rectTransform.anchorMax = new Vector2(0f, 1f);
            vSep.rectTransform.anchoredPosition = new Vector2(sepX, -10f);
            vSep.rectTransform.sizeDelta = new Vector2(1f, -134f);
            vSep.raycastTarget = false;

            // SORT button (top-right of grid area)
            _sortButtonRoot = CreateRect("SortButton", parent);
            _sortButtonRoot.pivot = new Vector2(0f, 1f);
            _sortButtonRoot.anchorMin = new Vector2(0f, 1f);
            _sortButtonRoot.anchorMax = new Vector2(0f, 1f);
            _sortButtonRoot.anchoredPosition = new Vector2(
                32f + GridAreaSize.x - 60f, -52f);
            _sortButtonRoot.sizeDelta = new Vector2(60f, 20f);

            Image sortBg = _sortButtonRoot.gameObject.AddComponent<Image>();
            sortBg.color = new Color(0.08f, 0.16f, 0.18f, 0.6f);
            sortBg.raycastTarget = true;

            TextMeshProUGUI sortLbl = CreateText("SortLabel", _sortButtonRoot, 10f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(sortLbl.rectTransform);
            SetBufferedText(sortLbl, "SORT");
            sortLbl.color = A(DimLow, 0.7f);

            SortButton sortBtn = _sortButtonRoot.gameObject.AddComponent<SortButton>();
            sortBtn.Init(this, sortBg,
                new Color(0.08f, 0.16f, 0.18f, 0.6f),
                new Color(0.12f, 0.24f, 0.28f, 0.8f));

            BuildFilterBar(parent);
        }

        private void BuildFilterBar(RectTransform parent)
        {
            _filterBarRoot = CreateRect("FilterBar", parent);
            _filterBarRoot.pivot = new Vector2(0f, 1f);
            _filterBarRoot.anchorMin = new Vector2(0f, 1f);
            _filterBarRoot.anchorMax = new Vector2(0f, 1f);
            _filterBarRoot.anchoredPosition = new Vector2(32f, -24f);
            _filterBarRoot.sizeDelta = new Vector2(GridAreaSize.x, 22f);
            HorizontalLayoutGroup layoutGroup = EnsureHorizontalLayout(_filterBarRoot, 6f, TextAnchor.MiddleLeft);
            LocalizedLayoutMirror.ConfigureRuntime(layoutGroup, _filterBarRoot, true, true, false);

            string[] labels = { "ALL", "TOOLS", "CONS", "MATS", "PARTS" };
            InventoryViewFilter[] filters =
            {
                InventoryViewFilter.All,
                InventoryViewFilter.Tools,
                InventoryViewFilter.Consumables,
                InventoryViewFilter.Materials,
                InventoryViewFilter.Components
            };

            _filterButtons = new PDAInventoryFilterButton[labels.Length];
            const float chipWidth = 70f;

            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform chip = CreateRect("Filter_" + labels[i], _filterBarRoot);
                chip.sizeDelta = new Vector2(chipWidth, 20f);
                EnsureLayoutElement(chip, chipWidth, 20f);

                Image chipBg = chip.gameObject.AddComponent<Image>();
                chipBg.color = filters[i] == _currentFilter ? TabBgActive : TabBgInactive;
                chipBg.raycastTarget = true;

                TextMeshProUGUI chipLabel = CreateText("Label", chip, 9f,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(chipLabel.rectTransform);
                SetBufferedText(chipLabel, labels[i]);
                chipLabel.color = filters[i] == _currentFilter ? TabActive : TabInactive;

                PDAInventoryFilterButton button = chip.gameObject.AddComponent<PDAInventoryFilterButton>();
                button.Init(this, filters[i], chipBg, chipLabel,
                    TabBgActive, TabBgInactive, TabActive, TabInactive);
                _filterButtons[i] = button;
            }

            RectTransform previousPageRect = CreateRect("PagePrev", _filterBarRoot);
            previousPageRect.sizeDelta = new Vector2(28f, 20f);
            EnsureLayoutElement(previousPageRect, 28f, 20f);
            Image previousPageBg = previousPageRect.gameObject.AddComponent<Image>();
            previousPageBg.color = TabBgInactive;
            previousPageBg.raycastTarget = true;
            TextMeshProUGUI previousPageLabel = CreateText("Label", previousPageRect, 10f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(previousPageLabel.rectTransform);
            SetBufferedText(previousPageLabel, "<");
            previousPageLabel.color = TabInactive;
            _previousPageButton = previousPageRect.gameObject.AddComponent<PDAInventoryPageButton>();
            _previousPageButton.Init(this, -1, previousPageBg, previousPageLabel,
                TabBgActive, TabBgInactive, TabActive, TabInactive);

            RectTransform nextPageRect = CreateRect("PageNext", _filterBarRoot);
            nextPageRect.sizeDelta = new Vector2(28f, 20f);
            EnsureLayoutElement(nextPageRect, 28f, 20f);
            Image nextPageBg = nextPageRect.gameObject.AddComponent<Image>();
            nextPageBg.color = TabBgInactive;
            nextPageBg.raycastTarget = true;
            TextMeshProUGUI nextPageLabel = CreateText("Label", nextPageRect, 10f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(nextPageLabel.rectTransform);
            SetBufferedText(nextPageLabel, ">");
            nextPageLabel.color = TabInactive;
            _nextPageButton = nextPageRect.gameObject.AddComponent<PDAInventoryPageButton>();
            _nextPageButton.Init(this, 1, nextPageBg, nextPageLabel,
                TabBgActive, TabBgInactive, TabActive, TabInactive);
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void RefreshAll()
        {
            RefreshGrid();
            RefreshDetails();
            RefreshToolStrip();
            RefreshWeight();
            _gridDirty = false;
            _detailsDirty = false;
            _toolStripDirty = false;
        }

        private void MarkAllDirty()
        {
            _gridDirty = true;
            _detailsDirty = true;
            _toolStripDirty = true;
        }

        private void FlushPendingRefresh(bool forceAll = false)
        {
            if (forceAll || _gridDirty)
                RefreshGrid();

            if (forceAll || _detailsDirty)
                RefreshDetails();

            if (forceAll || _toolStripDirty)
                RefreshToolStrip();

            _gridDirty = false;
            _detailsDirty = false;
            _toolStripDirty = false;
        }

        private void RefreshGrid()
        {
            if (playerInventory == null || _cellImages == null)
                return;

            InventoryGrid grid = playerInventory.Grid;
            if (grid == null)
                return;

            NativeArray<int>.ReadOnly anchorHashIds = grid.AnchorHashIds;
            NativeArray<byte>.ReadOnly anchorWidths = grid.AnchorWidths;
            NativeArray<byte>.ReadOnly anchorHeights = grid.AnchorHeights;
            NativeArray<byte>.ReadOnly anchorCategoryIds = grid.AnchorCategoryIds;
            NativeArray<ushort>.ReadOnly stackCounts = playerInventory.GetStackCountsReadOnly();
            if (!anchorHashIds.IsCreated || !anchorWidths.IsCreated || !anchorHeights.IsCreated || !anchorCategoryIds.IsCreated || !stackCounts.IsCreated)
                return;

            ValidateSelectionAgainstGrid(grid);

            for (int y = 0; y < GridRows; y++)
            {
                for (int x = 0; x < GridCols; x++)
                {
                    int idx = y * GridCols + x;
                    if (idx >= _cellImages.Length)
                        continue;

                    _cellImages[idx].color = grid.IsCellOccupied(x, y) ? CellOccupied : CellEmpty;
                }
            }

            _filteredAnchorCount = BuildFilteredAnchorIndexBuffer(anchorHashIds, anchorCategoryIds);
            _visiblePlacementCount = _filteredAnchorCount;
            SyncCurrentPageToSelection(grid);
            ClampCurrentPageIndex(_filteredAnchorCount);
            int pageStartIndex = _currentPageIndex * MaxVisibleBlocks;
            int pageItemCount = Mathf.Max(0, Mathf.Min(MaxVisibleBlocks, _filteredAnchorCount - pageStartIndex));
            bool selectionHiddenByFilter = _selectedItem != null && !MatchesFilter(_selectedItem);

            for (int i = 0; i < MaxVisibleBlocks; i++)
            {
                if (i < pageItemCount)
                {
                    int anchorIndex = _filteredAnchorIndices[pageStartIndex + i];
                    int itemHashId = anchorHashIds[anchorIndex];
                    ItemData item = playerInventory.ItemCatalog != null ? playerInventory.ItemCatalog.FindByHash(itemHashId) : null;
                    SetCanvasGroupVisible(_blockCanvasGroups[i], true);
                    _blockRects[i].anchoredPosition = new Vector2(
                        (anchorIndex % grid.Columns) * CellStep,
                        -(anchorIndex / grid.Columns) * CellStep);
                    _blockRects[i].sizeDelta = new Vector2(
                        anchorWidths[anchorIndex] * CellStep - CellGap,
                        anchorHeights[anchorIndex] * CellStep - CellGap);

                    _blockBgs[i].color = ItemBlock;

                    if (item != null && item.icon != null)
                    {
                        _blockIcons[i].sprite = item.icon;
                        SetGraphicVisible(_blockIcons[i], true);
                    }
                    else
                    {
                        SetGraphicVisible(_blockIcons[i], false);
                    }

                    if (_blockCounts != null && i < _blockCounts.Length && _blockCounts[i] != null)
                    {
                        int stackCount = Mathf.Max(1, stackCounts[anchorIndex]);
                        if (stackCount > 1)
                        {
                            SetGraphicVisible(_blockCounts[i], true);
                            SetNumericText(_blockCounts[i], StackCountTemplateChars, LocNumericArg.Int(stackCount));
                        }
                        else
                        {
                            SetGraphicVisible(_blockCounts[i], false);
                        }
                    }
                }
                else
                {
                    SetCanvasGroupVisible(_blockCanvasGroups[i], false);
                    SetGraphicVisible(_blockIcons[i], false);
                    if (_blockCounts != null && i < _blockCounts.Length && _blockCounts[i] != null)
                        SetGraphicVisible(_blockCounts[i], false);
                }
            }

            _activeBlockCount = pageItemCount;
            if (selectionHiddenByFilter)
                ClearSelectionSilently();

            RefreshPageSummary();
            RefreshPageButtons();
            RefreshWeight();
            RefreshCargoDigest();
        }

        private void ValidateSelectionAgainstGrid(InventoryGrid grid)
        {
            if (_selectedItem == null || grid == null)
                return;

            if (_selectedX < 0 || _selectedY < 0 || _selectedX >= GridCols || _selectedY >= GridRows)
            {
                ClearSelectionSilently();
                return;
            }

            if (playerInventory.GetItemHashAt(_selectedX, _selectedY) != _selectedItemHashId)
                ClearSelectionSilently();
        }

        private void RefreshDetails()
        {
            bool hasSelection = _selectedItem != null;

            SetGraphicVisible(_detailIcon, hasSelection);
            SetGraphicVisible(_detailName, hasSelection);
            SetGraphicVisible(_detailDesc, hasSelection);
            SetGraphicVisible(_detailWeight, hasSelection);
            SetGraphicVisible(_detailSize, hasSelection);
            SetGraphicVisible(_detailEffect, hasSelection);
            SetGraphicVisible(_detailStatus, hasSelection);
            SetGraphicVisible(_detailAction, hasSelection);
            SetGraphicVisible(_detailHint, !hasSelection);
            SetCanvasGroupVisible(_dropButtonCanvasGroup, hasSelection);
            SetCanvasGroupVisible(_loadoutAssignCanvasGroup, hasSelection && IsSelectedItemAssignableTool());
            SetGraphicVisible(_detailIconBoxBg, hasSelection);
            SetGraphicVisible(_detailNameBg, hasSelection);
            SetGraphicVisible(_detailStatusBg, hasSelection);
            SetGraphicVisible(_detailActionBg, hasSelection);

            if (!hasSelection)
            {
                if (_detailDescMadnessFx != null)
                    _detailDescMadnessFx.SetEffectActive(false);

                if (_detailHint != null)
                    SetNoSelectionHintText(_detailHint);
                SetCanvasGroupVisible(_useButtonCanvasGroup, false);
                if (_useButtonLabel != null)
                    ClearText(_useButtonLabel);
                SetCanvasGroupVisible(_loadoutAssignCanvasGroup, false);
                if (_detailEffect != null)
                    ClearText(_detailEffect);
                if (_detailStatus != null)
                    ClearText(_detailStatus);
                if (_detailAction != null)
                    ClearText(_detailAction);
                return;
            }

            if (_detailIcon != null)
            {
                _detailIcon.sprite = _selectedItem.icon;
                _detailIcon.color = _selectedItem.icon != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.1f);
            }

            if (_detailName != null)
                SetUpperText(_detailName, _selectedItem.itemName, ref _detailTextBuffer);

            if (_detailIconBoxBg != null)
                _detailIconBoxBg.color = GetSelectedDetailAccentColor(0.74f);

            if (_detailNameBg != null)
                _detailNameBg.color = GetSelectedDetailAccentColor(0.44f);

            if (_detailDesc != null)
                SetSelectedDescriptionText(_detailDesc);

            LocalizationManager localizationManager = Hecton8.Core.GlobalRegistry.Localization;
            if (_detailDescMadnessFx != null)
                _detailDescMadnessFx.SetEffectActive(localizationManager != null && localizationManager.IsMadnessWhisperVisualActive());

            if (_detailWeight != null)
            {
                int stk = playerInventory != null
                    ? playerInventory.GetStackCount(_selectedX, _selectedY) : 1;
                if (stk > 1)
                    SetNumericText(
                        _detailWeight,
                        DetailWeightStackTemplateChars,
                        LocNumericArg.Float(_selectedItem.weight),
                        LocNumericArg.Int(stk),
                        LocNumericArg.Float(_selectedItem.weight * stk));
                else
                    SetNumericText(_detailWeight, DetailWeightTemplateChars, LocNumericArg.Float(_selectedItem.weight));
            }

            if (_detailSize != null)
                SetSelectedSizeText(_detailSize);

            if (_detailEffect != null)
                SetSelectedItemEffectText(_detailEffect);

            if (_detailStatus != null)
                SetSelectedItemStatusText(_detailStatus);

            if (_detailStatusBg != null)
                _detailStatusBg.color = GetSelectedDetailStatusColor();

            if (_detailAction != null)
                SetSelectedItemActionText(_detailAction);

            if (_detailActionBg != null)
                _detailActionBg.color = GetSelectedDetailActionColor();

            RefreshPrimaryActionButton();

            RefreshLoadoutAssignButtons();
        }

        private void RefreshPrimaryActionButton()
        {
            if (_useButtonRoot == null)
                return;

            if (_selectedItem == null)
            {
                SetCanvasGroupVisible(_useButtonCanvasGroup, false);
                if (_useButtonLabel != null)
                    ClearText(_useButtonLabel);
                return;
            }

            EnsureCharCapacity(ref _detailTextBuffer, 32);
            int length;
            while (!TryWriteSelectedPrimaryActionLabel(_detailTextBuffer.AsSpan(), out length))
                EnsureCharCapacity(ref _detailTextBuffer, _detailTextBuffer.Length << 1);

            bool visible = length > 0;
            SetCanvasGroupVisible(_useButtonCanvasGroup, visible);

            if (_useButtonLabel != null)
                ApplyDynamicBuffer(_useButtonLabel, _detailTextBuffer, length);

            if (_useButtonBg != null)
                _useButtonBg.color = GetSelectedPrimaryActionColor();
        }

        private void RefreshToolStrip()
        {
            if (toolManager == null || _toolSlotBgs == null) return;

            int activeSlot = toolManager.CurrentSlotIndex;

            for (int i = 0; i < ToolSlotCount; i++)
            {
                bool isActive = i == activeSlot;
                _toolSlotBgs[i].color = isActive ? ToolSlotActive : ToolSlotBg;

                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                if (prefab != null && prefab.TryGetComponent<PlayerTool>(out var tool)
                    && tool.ToolData != null && tool.ToolData.icon != null)
                {
                    _toolSlotIcons[i].sprite = tool.ToolData.icon;
                    SetGraphicVisible(_toolSlotIcons[i], true);
                    _toolSlotIcons[i].color = toolManager.IsToolAvailableInSlot(i)
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.25f);
                }
                else
                {
                    SetGraphicVisible(_toolSlotIcons[i], false);
                }

                _toolSlotKeys[i].color = isActive ? A(Primary, 0.9f) : A(DimLow, 0.5f);
            }
        }

        private void RefreshWeight()
        {
            if (_weightLabel == null || playerInventory == null) return;
            InventoryGrid grid = playerInventory.Grid;
            int usedCells = CountUsedCells();
            int totalCells = grid != null ? grid.Columns * grid.Rows : GridCols * GridRows;
            SetNumericText(
                _weightLabel,
                CargoWeightTemplateChars,
                LocNumericArg.Float(playerInventory.TotalWeight),
                LocNumericArg.Int(usedCells),
                LocNumericArg.Int(totalCells));
        }

        private void RefreshCargoDigest()
        {
            if (_cargoSummary == null || _filterSummary == null || playerInventory == null)
                return;

            InventoryGrid grid = playerInventory.Grid;
            if (grid == null)
                return;

            NativeArray<int>.ReadOnly anchorHashIds = grid.AnchorHashIds;
            NativeArray<byte>.ReadOnly anchorCategoryIds = grid.AnchorCategoryIds;
            NativeArray<ushort>.ReadOnly stackCounts = playerInventory.GetStackCountsReadOnly();
            if (!anchorHashIds.IsCreated || !anchorCategoryIds.IsCreated || !stackCounts.IsCreated)
                return;

            int count = 0;
            int tools = 0;
            int consumables = 0;
            int materials = 0;
            int components = 0;
            int misc = 0;
            int totalUnits = 0;

            int anchorCount = Mathf.Min(anchorHashIds.Length, Mathf.Min(anchorCategoryIds.Length, stackCounts.Length));
            for (int i = 0; i < anchorCount; i++)
            {
                if (anchorHashIds[i] == 0)
                    continue;

                count++;
                totalUnits += Mathf.Max(1, stackCounts[i]);

                switch ((ItemCategory)anchorCategoryIds[i])
                {
                    case ItemCategory.Tool:
                    case ItemCategory.Equipment:
                        tools++;
                        break;
                    case ItemCategory.Consumable:
                        consumables++;
                        break;
                    case ItemCategory.Material:
                        materials++;
                        break;
                    case ItemCategory.Component:
                        components++;
                        break;
                    default:
                        misc++;
                        break;
                }
            }

            int totalCells = playerInventory.Grid != null
                ? playerInventory.Grid.Columns * playerInventory.Grid.Rows
                : GridCols * GridRows;
            int freeCells = Mathf.Max(0, totalCells - CountUsedCells());

            int cargoLength;
            EnsureCharCapacity(ref _cargoSummaryBuffer, 160);
            while (!TryWriteCargoDigest(
                       _cargoSummaryBuffer.AsSpan(),
                       count,
                       totalUnits,
                       freeCells,
                       totalCells,
                       tools,
                       consumables,
                       materials,
                       components,
                       misc,
                       out cargoLength))
            {
                EnsureCharCapacity(ref _cargoSummaryBuffer, _cargoSummaryBuffer.Length << 1);
            }

            ApplyDynamicBuffer(_cargoSummary, _cargoSummaryBuffer, cargoLength);

            int filterLength;
            EnsureCharCapacity(ref _filterSummaryBuffer, 80);
            while (!TryWriteFilterDigest(
                       _filterSummaryBuffer.AsSpan(),
                       _currentFilter,
                       _visiblePlacementCount,
                       count,
                       out filterLength))
            {
                EnsureCharCapacity(ref _filterSummaryBuffer, _filterSummaryBuffer.Length << 1);
            }

            ApplyDynamicBuffer(_filterSummary, _filterSummaryBuffer, filterLength);
        }

        private int BuildFilteredAnchorIndexBuffer(
            NativeArray<int>.ReadOnly anchorHashIds,
            NativeArray<byte>.ReadOnly anchorCategoryIds)
        {
            if (_filteredAnchorIndices == null)
                return 0;

            int limit = Mathf.Min(anchorHashIds.Length, anchorCategoryIds.Length);
            int writeIndex = 0;
            for (int anchorIndex = 0; anchorIndex < limit && writeIndex < _filteredAnchorIndices.Length; anchorIndex++)
            {
                if (anchorHashIds[anchorIndex] == 0 || !MatchesFilter(anchorCategoryIds[anchorIndex]))
                    continue;

                _filteredAnchorIndices[writeIndex++] = anchorIndex;
            }

            return writeIndex;
        }

        private void ClampCurrentPageIndex(int filteredAnchorCount)
        {
            int maxPageIndex = Mathf.Max(0, GetPageCount(filteredAnchorCount) - 1);
            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, maxPageIndex);
        }

        private void SyncCurrentPageToSelection(InventoryGrid grid)
        {
            if (_selectedItem == null || grid == null || _selectedX < 0 || _selectedY < 0)
                return;

            int selectedAnchorIndex = grid.GetCellAnchorIndex(_selectedX, _selectedY);
            if (selectedAnchorIndex < 0)
                return;

            for (int i = 0; i < _filteredAnchorCount; i++)
            {
                if (_filteredAnchorIndices[i] != selectedAnchorIndex)
                    continue;

                _currentPageIndex = i / MaxVisibleBlocks;
                return;
            }
        }

        private void RefreshPageSummary()
        {
            if (_pageSummary == null)
                return;

            int pageCount = GetPageCount(_filteredAnchorCount);
            int currentPage = pageCount > 0 ? Mathf.Min(_currentPageIndex + 1, pageCount) : 0;
            int length;
            EnsureCharCapacity(ref _pageSummaryBuffer, 24);
            while (!TryWritePageDigest(_pageSummaryBuffer.AsSpan(), currentPage, pageCount, out length))
                EnsureCharCapacity(ref _pageSummaryBuffer, _pageSummaryBuffer.Length << 1);

            ApplyDynamicBuffer(_pageSummary, _pageSummaryBuffer, length);
        }

        private void RefreshPageButtons()
        {
            int pageCount = GetPageCount(_filteredAnchorCount);
            bool canGoPrevious = _currentPageIndex > 0;
            bool canGoNext = _currentPageIndex + 1 < pageCount;
            _previousPageButton?.SetActive(canGoPrevious);
            _nextPageButton?.SetActive(canGoNext);
        }

        private int GetPageCount(int filteredAnchorCount)
        {
            return filteredAnchorCount <= 0
                ? 1
                : ((filteredAnchorCount - 1) / MaxVisibleBlocks) + 1;
        }

        internal void ChangePage(int direction)
        {
            if (direction == 0)
                return;

            int pageCount = GetPageCount(_filteredAnchorCount);
            if (pageCount <= 1)
                return;

            int nextPageIndex = Mathf.Clamp(_currentPageIndex + direction, 0, pageCount - 1);
            if (nextPageIndex == _currentPageIndex)
                return;

            _currentPageIndex = nextPageIndex;
            _gridDirty = true;
            if (IsTabActive)
                FlushPendingRefresh();
        }

        // ══════════════════════════════════════════════════════════
        //  POINTER INTERACTION
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// Vyzyvaetsya knopkoy DROP. Udalyaet odnu edinitsu iz steka,
        /// spavnit worldPrefab pered igrokom.
        /// </summary>
        internal void DropSelectedItem()
        {
            if (_selectedItem == null || playerInventory == null) return;

            PlayUISound(dropSound);

            Transform dropOrigin = ResolveDropOrigin();
            if (dropOrigin == null)
            {
                NotifyWarning("DROP BLOCKED - NO PLAYER VIEW ORIGIN");
                return;
            }

            Vector3 spawnPos = dropOrigin.position
                + dropOrigin.forward * 2.5f
                + Vector3.down * 0.3f;

            if (!playerInventory.TryDropOneItemToWorldSignal(
                    _selectedX,
                    _selectedY,
                    spawnPos,
                    Vector3.zero,
                    dropOrigin,
                    out _))
            {
                NotifyWarning("DROP BLOCKED - WORLD SPAWN FAILED");
                return;
            }

            // Proveryaem ostalsya li predmet na etoy pozitsii
            int remainingHashId = playerInventory.GetItemHashAt(_selectedX, _selectedY);
            if (remainingHashId != _selectedItemHashId)
            {
                ClearSelection();
            }
            else
            {
                RefreshDetails();
            }
        }
        internal void HandlePointerMove(Vector2 localPoint)
        {
            if (!TryResolveGridCoordinates(localPoint, out int gx, out int gy))
            {
                ClearHover();
                return;
            }

            if (gx == _hoverX && gy == _hoverY) return;

            _hoverX = gx;
            _hoverY = gy;

            int cellHashId = playerInventory != null
                ? playerInventory.GetItemHashAt(gx, gy)
                : 0;
            ItemData cell = ResolveInventoryItem(cellHashId);

            if (cell != null)
            {
                // Find anchor of this item
                FindAnchor(cell, gx, gy, out int ax, out int ay);
                PositionHighlight(_hoverRect, ax, ay, cell.width, cell.height);
                _hoverImage.enabled = true;
                _hoverImage.color = HoverTint;
            }
            else
            {
                PositionHighlight(_hoverRect, gx, gy, 1, 1);
                _hoverImage.enabled = true;
                _hoverImage.color = A(HoverTint, 0.1f);
            }
        }

        internal void HandlePointerClick(Vector2 localPoint)
        {
            if (!TryResolveGridCoordinates(localPoint, out int gx, out int gy))
            {
                ClearSelection();
                return;
            }

            TrySelectGridCell(gx, gy, playAudio: true);
        }

        internal void HandlePointerBeginDrag(Vector2 localPoint)
        {
            if (!TryResolveGridCoordinates(localPoint, out int gx, out int gy) || playerInventory == null)
                return;

            int cellHashId = playerInventory.GetItemHashAt(gx, gy);
            ItemData cell = ResolveInventoryItem(cellHashId);
            if (cell == null)
                return;

            FindAnchor(cell, gx, gy, out int anchorX, out int anchorY);
            TrySelectGridCell(anchorX, anchorY, playAudio: false);

            _dragActive = true;
            _dragSourceX = anchorX;
            _dragSourceY = anchorY;
            _dragSourceHashId = cellHashId;
            _dragPreviewImage.sprite = cell.icon;
            _dragPreviewImage.color = cell.icon != null
                ? new Color(1f, 1f, 1f, 0.92f)
                : new Color(0.46f, 0.98f, 0.94f, 0.18f);
            UpdateDragPreview(localPoint, cell.width, cell.height);
            if (_dragPreviewImage != null)
                _dragPreviewImage.enabled = true;
        }

        internal void HandlePointerDrag(Vector2 localPoint)
        {
            if (!_dragActive)
            {
                HandlePointerMove(localPoint);
                return;
            }

            HandlePointerMove(localPoint);

            ItemData sourceItem = ResolveInventoryItem(_dragSourceHashId);
            if (sourceItem != null)
                UpdateDragPreview(localPoint, sourceItem.width, sourceItem.height);
        }

        internal void HandlePointerEndDrag(Vector2 localPoint)
        {
            if (_dragPreviewImage != null)
            {
                _dragPreviewImage.enabled = false;
                _dragPreviewImage.sprite = null;
                _dragPreviewImage.color = new Color(0.46f, 0.98f, 0.94f, 0.18f);
            }

            if (!_dragActive)
                return;

            int sourceX = _dragSourceX;
            int sourceY = _dragSourceY;
            int sourceHashId = _dragSourceHashId;
            _dragActive = false;
            _dragSourceX = -1;
            _dragSourceY = -1;
            _dragSourceHashId = 0;

            if (playerInventory == null || !TryResolveGridCoordinates(localPoint, out int targetX, out int targetY))
                return;

            if (!playerInventory.TryMoveOrSwapAnchor(sourceX, sourceY, targetX, targetY))
                return;

            FlushPendingRefresh(forceAll: true);
            SelectDraggedAnchor(sourceHashId, targetX, targetY, sourceX, sourceY);
        }

        /// <summary>
        /// Vyzyvaetsya knopkoy USE. Potreblyaet odnu edinitsu predmeta.
        /// </summary>
        internal void UseSelectedItem()
        {
            if (_selectedItem == null || playerInventory == null) return;
            if (!_selectedItem.isConsumable) return;

            // Zvuk ispolzovaniya (prioritet: predmet → UI default)
            AudioClip clip = _selectedItem.useSound != null
                ? _selectedItem.useSound
                : useSound;
            PlayUISound(clip);

            bool consumed = playerInventory.ConsumeOneItem(_selectedX, _selectedY);
            if (!consumed) return;

            // Proveryaem ostalsya li predmet
            int remainingHashId = playerInventory.GetItemHashAt(_selectedX, _selectedY);
            if (remainingHashId != _selectedItemHashId)
                ClearSelection();
            else
                RefreshDetails();
        }

        internal void PerformPrimarySelectedAction()
        {
            if (_selectedItem == null)
                return;

            if (_selectedItem.isConsumable)
            {
                UseSelectedItem();
                return;
            }

            if (!IsSelectedItemAssignableTool() || toolManager == null)
                return;

            GameObject prefab = toolManager.GetKnownToolPrefabForItem(_selectedItem);
            if (prefab == null)
            {
                NotifyMissingHeldPrefab();
                return;
            }

            int assignedSlot = -1;
            for (int i = 0; i < ToolSlotCount; i++)
            {
                if (ReferenceEquals(toolManager.GetAssignedToolPrefab(i), prefab))
                {
                    assignedSlot = i;
                    break;
                }
            }

            if (assignedSlot >= 0)
            {
                if (toolManager.CurrentSlotIndex == assignedSlot)
                {
                    toolManager.Holster();
                    NotifyLoadoutHolstered();
                }
                else if (toolManager.IsToolAvailableInSlot(assignedSlot))
                {
                    toolManager.SwitchToSlot(assignedSlot);
                    NotifyLoadoutActivated(assignedSlot);
                }
                else
                {
                    AssignSelectedItemToSlot(GetRecommendedLoadoutSlot());
                }

                RefreshDetails();
                return;
            }

            AssignSelectedItemToSlot(GetRecommendedLoadoutSlot());
        }

        /// <summary>
        /// Vyzyvaetsya knopkoy SORT.
        /// </summary>
        internal void SortInventoryAction()
        {
            if (playerInventory == null) return;
            PlayUISound(sortSound);
            playerInventory.RequestSortInventory();
            ClearSelection();
            if (IsTabActive)
                FlushPendingRefresh(forceAll: true);
        }

        internal void HandlePointerExit()
        {
            ClearHover();
        }

        private void ClearHover()
        {
            _hoverX = -1;
            _hoverY = -1;
            if (_hoverRect != null)
                _hoverImage.enabled = false;
        }

        private void ClearSelection()
        {
            _selectedX = -1;
            _selectedY = -1;
            _selectedItem = null;
            _selectedItemHashId = 0;
            if (_selectRect != null)
                _selectImage.enabled = false;
            RefreshDetails();
        }

        private void ClearSelectionSilently()
        {
            _selectedX = -1;
            _selectedY = -1;
            _selectedItem = null;
            _selectedItemHashId = 0;
            if (_selectRect != null)
                _selectImage.enabled = false;
            RefreshDetails();
        }

        private bool TryResolveGridCoordinates(Vector2 localPoint, out int gx, out int gy)
        {
            gx = Mathf.FloorToInt(localPoint.x / CellStep);
            gy = Mathf.FloorToInt(-localPoint.y / CellStep);
            return gx >= 0 && gx < GridCols && gy >= 0 && gy < GridRows;
        }

        private void TrySelectGridCell(int gx, int gy, bool playAudio)
        {
            int cellHashId = playerInventory != null
                ? playerInventory.GetItemHashAt(gx, gy)
                : 0;
            ItemData cell = ResolveInventoryItem(cellHashId);

            if (cell == null)
            {
                ClearSelection();
                return;
            }

            FindAnchor(cell, gx, gy, out int anchorX, out int anchorY);
            if (playAudio && (cellHashId != _selectedItemHashId || anchorX != _selectedX || anchorY != _selectedY))
                PlayUISound(selectSound);

            _selectedX = anchorX;
            _selectedY = anchorY;
            _selectedItem = cell;
            _selectedItemHashId = cellHashId;
            PositionHighlight(_selectRect, anchorX, anchorY, cell.width, cell.height);
            _selectImage.enabled = true;
            RefreshDetails();
        }

        private void UpdateDragPreview(Vector2 localPoint, int width, int height)
        {
            if (_dragPreviewRect == null)
                return;

            float pixelWidth = (width * CellStep) - CellGap;
            float pixelHeight = (height * CellStep) - CellGap;
            _dragPreviewRect.sizeDelta = new Vector2(pixelWidth, pixelHeight);
            _dragPreviewRect.anchoredPosition = new Vector2(
                localPoint.x - (pixelWidth * 0.5f),
                localPoint.y + (pixelHeight * 0.5f));
            _dragPreviewRect.SetAsLastSibling();
        }

        private void SelectDraggedAnchor(int sourceHashId, int targetX, int targetY, int fallbackX, int fallbackY)
        {
            if (playerInventory == null)
                return;

            if (playerInventory.GetItemHashAt(targetX, targetY) == sourceHashId)
            {
                TrySelectGridCell(targetX, targetY, playAudio: false);
                return;
            }

            if (playerInventory.GetItemHashAt(fallbackX, fallbackY) == sourceHashId)
                TrySelectGridCell(fallbackX, fallbackY, playAudio: false);
        }

        private void PositionHighlight(RectTransform rect, int gx, int gy, int w, int h)
        {
            rect.anchoredPosition = new Vector2(gx * CellStep, -gy * CellStep);
            rect.sizeDelta = new Vector2(w * CellStep - CellGap, h * CellStep - CellGap);
        }

        private void FindAnchor(ItemData item, int gx, int gy, out int ax, out int ay)
        {
            InventoryGrid grid = playerInventory.Grid;
            int anchorIndex = grid != null ? grid.GetCellAnchorIndex(gx, gy) : -1;
            if (anchorIndex < 0)
            {
                ax = gx;
                ay = gy;
                return;
            }

            ax = anchorIndex % grid.Columns;
            ay = anchorIndex / grid.Columns;
        }

        private ItemData ResolveInventoryItem(int itemHashId)
        {
            return itemHashId != 0 && playerInventory != null && playerInventory.ItemCatalog != null
                ? playerInventory.ItemCatalog.FindByHash(itemHashId)
                : null;
        }

        // ══════════════════════════════════════════════════════════
        //  UI FACTORY HELPERS
        // ══════════════════════════════════════════════════════════
        private void PlayUISound(AudioClip clip)
        {
            if (clip == null) return;

            var audio = Hecton8.Core.GlobalRegistry.Audio;
            if (audio != null)
                audio.PlayStatic2D(clip, uiVolume);
        }
        private RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.localScale = Vector3.one;
            if (parent != null) go.layer = parent.gameObject.layer;
            return r;
        }

        private Image CreateImage(string name, RectTransform parent, Color color)
        {
            RectTransform r = CreateRect(name, parent);
            Image img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, float size,
            FontStyles style, TextAlignmentOptions align)
        {
            RectTransform r = CreateRect(name, parent);
            TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            Hecton8.UI.LocalizedTMPAutoSizer.Configure(t, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            return t;
        }

        private static void Stretch(RectTransform r, float l = 0, float r2 = 0,
            float t = 0, float b = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b);
            r.offsetMax = new Vector2(-r2, -t);
        }

        private static void Anchor(RectTransform r, Vector2 amin, Vector2 amax,
            Vector2 pos, Vector2 size)
        {
            r.anchorMin = amin;
            r.anchorMax = amax;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform rect)
        {
            if (rect == null)
                return null;

            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();

            return group;
        }

        private static HorizontalLayoutGroup EnsureHorizontalLayout(RectTransform rect, float spacing, TextAnchor alignment)
        {
            if (rect == null)
                return null;

            HorizontalLayoutGroup layout = rect.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;
            layout.reverseArrangement = false;
            return layout;
        }

        private static LayoutElement EnsureLayoutElement(RectTransform rect, float width, float height)
        {
            if (rect == null)
                return null;

            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = rect.gameObject.AddComponent<LayoutElement>();

            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0f;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0f;
            return layoutElement;
        }

        private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static void SetGraphicVisible(MaskableGraphic graphic, bool visible)
        {
            if (graphic == null || graphic.enabled == visible)
                return;

            graphic.enabled = visible;
        }

        private static void SetNumericText(TMP_Text label, char[] template, LocNumericArg value0)
        {
            if (label == null || template == null)
                return;

            LocNumericBuffer.Write(new ReadOnlySpan<char>(template), value0, out char[] buffer, out int length);
            int safeLength = Mathf.Clamp(length, 0, buffer != null ? buffer.Length : 0);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private static void SetNumericText(TMP_Text label, char[] template, LocNumericArg value0, LocNumericArg value1, LocNumericArg value2)
        {
            if (label == null || template == null)
                return;

            LocNumericBuffer.Write(new ReadOnlySpan<char>(template), value0, value1, value2, out char[] buffer, out int length);
            int safeLength = Mathf.Clamp(length, 0, buffer != null ? buffer.Length : 0);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private static void ApplyDynamicBuffer(TMP_Text label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private void SetBufferedText(TMP_Text label, string value)
        {
            if (label == null)
                return;

            if (string.IsNullOrEmpty(value))
            {
                ClearText(label);
                return;
            }

            EnsureCharCapacity(ref _detailTextBuffer, value.Length);
            value.AsSpan().CopyTo(_detailTextBuffer.AsSpan());
            ApplyDynamicBuffer(label, _detailTextBuffer, value.Length);
        }

        private static void ClearText(TMP_Text label)
        {
            if (label == null)
                return;

            label.SetCharArray(EmptyTextChars, 0, 0);
        }

        private static void SetUpperText(TMP_Text label, string value, ref char[] buffer)
        {
            if (label == null)
                return;

            if (string.IsNullOrEmpty(value))
            {
                ClearText(label);
                return;
            }

            EnsureCharCapacity(ref buffer, value.Length);
            for (int i = 0; i < value.Length; i++)
                buffer[i] = char.ToUpperInvariant(value[i]);

            label.SetCharArray(buffer, 0, value.Length);
        }

        private static void EnsureCharCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer != null && buffer.Length >= requiredLength)
                return;

            int capacity = buffer == null ? 32 : buffer.Length;
            while (capacity < requiredLength)
                capacity <<= 1;

            buffer = new char[capacity]; // COLD ALLOC: char[capacity] - expanded inventory tab text staging buffer - owner: PDAInventoryTab
        }

        private static bool TryWriteCargoDigest(
            Span<char> destination,
            int anchors,
            int totalUnits,
            int freeCells,
            int totalCells,
            int tools,
            int consumables,
            int materials,
            int components,
            int misc,
            out int length)
        {
            length = 0;
            if (!TryWriteLiteral(destination, ref length, CargoDigestPrefixAnchorsChars) ||
                !TryWriteInt(destination, ref length, anchors) ||
                !TryWriteLiteral(destination, ref length, CargoDigestUnitsChars) ||
                !TryWriteInt(destination, ref length, totalUnits) ||
                !TryWriteLiteral(destination, ref length, CargoDigestFreeChars) ||
                !TryWriteInt(destination, ref length, freeCells) ||
                !TryWriteLiteral(destination, ref length, "/".AsSpan()) ||
                !TryWriteInt(destination, ref length, totalCells) ||
                !TryWriteLiteral(destination, ref length, CargoDigestLineBreakChars) ||
                !TryWriteLiteral(destination, ref length, CargoDigestToolsChars) ||
                !TryWriteInt(destination, ref length, tools) ||
                !TryWriteLiteral(destination, ref length, CargoDigestConsumablesChars) ||
                !TryWriteInt(destination, ref length, consumables) ||
                !TryWriteLiteral(destination, ref length, CargoDigestMaterialsChars) ||
                !TryWriteInt(destination, ref length, materials) ||
                !TryWriteLiteral(destination, ref length, CargoDigestComponentsChars) ||
                !TryWriteInt(destination, ref length, components) ||
                !TryWriteLiteral(destination, ref length, CargoDigestMiscChars) ||
                !TryWriteInt(destination, ref length, misc))
            {
                length = 0;
                return false;
            }

            return true;
        }

        private static bool TryWriteFilterDigest(
            Span<char> destination,
            InventoryViewFilter filter,
            int visibleItems,
            int totalItems,
            out int length)
        {
            length = 0;
            if (!TryWriteLiteral(destination, ref length, FilterDigestPrefixChars) ||
                !TryWriteLiteral(destination, ref length, ResolveFilterLabelChars(filter)) ||
                !TryWriteLiteral(destination, ref length, FilterDigestShowingChars) ||
                !TryWriteInt(destination, ref length, visibleItems) ||
                !TryWriteLiteral(destination, ref length, "/".AsSpan()) ||
                !TryWriteInt(destination, ref length, totalItems) ||
                !TryWriteLiteral(destination, ref length, FilterDigestItemsChars))
            {
                length = 0;
                return false;
            }

            return true;
        }

        private static bool TryWritePageDigest(Span<char> destination, int currentPage, int totalPages, out int length)
        {
            length = 0;
            if (!TryWriteLiteral(destination, ref length, PageDigestPrefixChars) ||
                !TryWriteInt(destination, ref length, currentPage) ||
                !TryWriteLiteral(destination, ref length, "/".AsSpan()) ||
                !TryWriteInt(destination, ref length, totalPages))
            {
                length = 0;
                return false;
            }

            return true;
        }

        private static ReadOnlySpan<char> ResolveFilterLabelChars(InventoryViewFilter filter)
        {
            switch (filter)
            {
                case InventoryViewFilter.Tools:
                    return FilterLabelToolsChars;
                case InventoryViewFilter.Consumables:
                    return FilterLabelConsumablesChars;
                case InventoryViewFilter.Materials:
                    return FilterLabelMaterialsChars;
                case InventoryViewFilter.Components:
                    return FilterLabelComponentsChars;
                default:
                    return FilterLabelAllChars;
            }
        }

        private static bool TryWriteLiteral(Span<char> destination, ref int index, ReadOnlySpan<char> literal)
        {
            if ((uint)index > (uint)destination.Length || literal.Length > destination.Length - index)
                return false;

            literal.CopyTo(destination.Slice(index));
            index += literal.Length;
            return true;
        }

        private static bool TryWriteInt(Span<char> destination, ref int index, int value)
        {
            if ((uint)index > (uint)destination.Length)
                return false;

            if (!value.TryFormat(destination.Slice(index), out int written))
                return false;

            index += written;
            return true;
        }

        private static bool TryWriteFloat(Span<char> destination, ref int index, float value, string format)
        {
            if ((uint)index > (uint)destination.Length)
                return false;

            if (!value.TryFormat(destination.Slice(index), out int written, format))
                return false;

            index += written;
            return true;
        }

        private static bool TryWriteRestoreSegment(
            Span<char> destination,
            ref int index,
            ref bool hasEffect,
            ReadOnlySpan<char> label,
            float amount)
        {
            if (hasEffect && !TryWriteLiteral(destination, ref index, "  |".AsSpan()))
                return false;

            if (!TryWriteLiteral(destination, ref index, label) ||
                !TryWriteInt(destination, ref index, Mathf.RoundToInt(amount)))
            {
                return false;
            }

            hasEffect = true;
            return true;
        }

        private static ReadOnlySpan<char> ResolveFilterEmptyLabelChars(InventoryViewFilter filter)
        {
            switch (filter)
            {
                case InventoryViewFilter.Tools:
                    return FilterEmptyLabelToolsChars;
                case InventoryViewFilter.Consumables:
                    return FilterEmptyLabelConsumablesChars;
                case InventoryViewFilter.Materials:
                    return FilterEmptyLabelMaterialsChars;
                case InventoryViewFilter.Components:
                    return FilterEmptyLabelComponentsChars;
                default:
                    return FilterLabelAllChars;
            }
        }

        private static ReadOnlySpan<char> ResolveItemCategoryLabelChars(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Material: return "MATERIAL".AsSpan();
                case ItemCategory.Tool: return "TOOL".AsSpan();
                case ItemCategory.Equipment: return "EQUIPMENT".AsSpan();
                case ItemCategory.Consumable: return "CONSUMABLE".AsSpan();
                case ItemCategory.Component: return "COMPONENT".AsSpan();
                case ItemCategory.Organic: return "ORGANIC".AsSpan();
                default: return "MISCELLANEOUS".AsSpan();
            }
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value.AsSpan());
        }

        private static bool AppendUpperInvariant(ref FixedCharBuffer buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            Span<char> scratch = stackalloc char[1];
            for (int i = 0; i < value.Length; i++)
            {
                scratch[0] = char.ToUpperInvariant(value[i]);
                if (!buffer.Append(scratch))
                    return false;
            }

            return true;
        }

        private static Color A(Color c, float a) { c.a = a; return c; }

        internal void SetFilter(InventoryViewFilter filter)
        {
            if (_currentFilter == filter)
                return;

            _currentFilter = filter;
            _currentPageIndex = 0;

            if (_filterButtons != null)
            {
                for (int i = 0; i < _filterButtons.Length; i++)
                    _filterButtons[i]?.SetActive(_filterButtons[i].Filter == filter);
            }

            RefreshGrid();
        }

        private bool MatchesFilter(ItemData item)
        {
            if (item == null)
                return false;

            switch (_currentFilter)
            {
                case InventoryViewFilter.Tools:
                    return item.category == ItemCategory.Tool || item.category == ItemCategory.Equipment;
                case InventoryViewFilter.Consumables:
                    return item.category == ItemCategory.Consumable;
                case InventoryViewFilter.Materials:
                    return item.category == ItemCategory.Material;
                case InventoryViewFilter.Components:
                    return item.category == ItemCategory.Component;
                default:
                    return true;
            }
        }

        private bool MatchesFilter(byte categoryId)
        {
            ItemCategory category = (ItemCategory)categoryId;
            switch (_currentFilter)
            {
                case InventoryViewFilter.Tools:
                    return category == ItemCategory.Tool || category == ItemCategory.Equipment;
                case InventoryViewFilter.Consumables:
                    return category == ItemCategory.Consumable;
                case InventoryViewFilter.Materials:
                    return category == ItemCategory.Material;
                case InventoryViewFilter.Components:
                    return category == ItemCategory.Component;
                default:
                    return true;
            }
        }

        private int CountUsedCells()
        {
            if (playerInventory == null || playerInventory.Grid == null)
                return 0;

            return playerInventory.Grid.OccupiedCells;
        }

        private bool IsSelectedItemAssignableTool()
        {
            if (_selectedItem == null)
                return false;

            return _selectedItem.category == ItemCategory.Tool
                || _selectedItem.category == ItemCategory.Equipment;
        }

        private void RefreshLoadoutAssignButtons()
        {
            if (_loadoutAssignBgs == null || _loadoutAssignLabels == null)
                return;

            GameObject knownPrefab = toolManager != null
                ? toolManager.GetKnownToolPrefabForItem(_selectedItem)
                : null;
            int recommendedSlot = GetRecommendedLoadoutSlot();

            for (int i = 0; i < ToolSlotCount; i++)
            {
                if (_loadoutAssignLabels[i] == null || _loadoutAssignBgs[i] == null)
                    continue;

                GameObject assigned = toolManager != null
                    ? toolManager.GetAssignedToolPrefab(i)
                    : null;

                bool isAssigned = knownPrefab != null && ReferenceEquals(assigned, knownPrefab);
                bool available = toolManager != null && toolManager.IsToolAvailableInSlot(i);
                bool isRecommended = !isAssigned && recommendedSlot == i;
                SetLoadoutAssignLabel(_loadoutAssignLabels[i], i, isAssigned, isRecommended);
                _loadoutAssignLabels[i].color = isAssigned
                    ? (available ? A(Primary, 0.9f) : new Color(1f, 0.78f, 0.28f, 0.88f))
                    : isRecommended
                        ? new Color(0.82f, 0.98f, 1f, 0.94f)
                    : A(Dim, 0.78f);
                _loadoutAssignBgs[i].color = isAssigned
                    ? (available ? new Color(0.14f, 0.3f, 0.28f, 0.78f) : new Color(0.28f, 0.2f, 0.06f, 0.76f))
                    : isRecommended
                        ? new Color(0.1f, 0.22f, 0.34f, 0.78f)
                    : new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }
        }

        private void SetLoadoutAssignLabel(TMP_Text label, int slotIndex, bool isAssigned, bool isRecommended)
        {
            EnsureCharCapacity(ref _loadoutAssignTextBuffer, 24);
            int length;
            while (!TryWriteLoadoutAssignLabel(_loadoutAssignTextBuffer.AsSpan(), slotIndex, isAssigned, isRecommended, out length))
                EnsureCharCapacity(ref _loadoutAssignTextBuffer, _loadoutAssignTextBuffer.Length << 1);

            ApplyDynamicBuffer(label, _loadoutAssignTextBuffer, length);
        }

        private static bool TryWriteLoadoutAssignLabel(
            Span<char> destination,
            int slotIndex,
            bool isAssigned,
            bool isRecommended,
            out int length)
        {
            length = 0;
            if (isAssigned)
            {
                return TryWriteLiteral(destination, ref length, "SLOT ".AsSpan()) &&
                       TryWriteInt(destination, ref length, slotIndex + 1) &&
                       TryWriteLiteral(destination, ref length, " READY".AsSpan());
            }

            if (isRecommended)
            {
                return TryWriteLiteral(destination, ref length, "REC SLOT ".AsSpan()) &&
                       TryWriteInt(destination, ref length, slotIndex + 1);
            }

            return TryWriteLiteral(destination, ref length, "SET SLOT ".AsSpan()) &&
                   TryWriteInt(destination, ref length, slotIndex + 1);
        }

        internal void AssignSelectedItemToSlot(int slotIndex)
        {
            if (_selectedItem == null || toolManager == null)
                return;

            if (slotIndex < 0 || slotIndex >= ToolSlotCount)
                return;

            GameObject prefab = toolManager.GetKnownToolPrefabForItem(_selectedItem);
            if (prefab == null)
            {
                NotifyMissingHeldPrefab();
                return;
            }

            toolManager.SetAssignedToolPrefab(slotIndex, prefab, holsterIfCurrentInvalid: true);
            RefreshToolStrip();
            RefreshLoadoutAssignButtons();
            RefreshDetails();

            NotifyLoadoutUpdated(slotIndex);
        }

        private void NotifyInfo(string message)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            hudNotification?.ShowInfo(message);
        }

        private void NotifyInfo(in FixedCharBuffer messageBuffer)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            hudNotification?.ShowInfo(in messageBuffer);
        }

        private Transform ResolveDropOrigin()
        {
            if (_dropOrigin != null)
                return _dropOrigin;

            AutoResolve();
            return _dropOrigin;
        }

        private void NotifyWarning(string message)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            hudNotification?.ShowWarning(message);
        }

        private void NotifyWarning(in FixedCharBuffer messageBuffer)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            hudNotification?.ShowWarning(in messageBuffer);
        }

        private void NotifyMissingHeldPrefab()
        {
            _notificationBuffer.Clear();
            AppendText(ref _notificationBuffer, "NO HELD PREFAB REGISTERED FOR ");
            AppendUpperInvariant(ref _notificationBuffer, _selectedItem != null ? _selectedItem.itemName : null);
            NotifyWarning(in _notificationBuffer);
        }

        private void NotifyLoadoutHolstered()
        {
            _notificationBuffer.Clear();
            AppendText(ref _notificationBuffer, "LOADOUT HOLSTERED - ");
            AppendUpperInvariant(ref _notificationBuffer, _selectedItem != null ? _selectedItem.itemName : null);
            NotifyInfo(in _notificationBuffer);
        }

        private void NotifyLoadoutActivated(int slotIndex)
        {
            _notificationBuffer.Clear();
            AppendText(ref _notificationBuffer, "LOADOUT ACTIVATED - SLOT ");
            _notificationBuffer.AppendInt(slotIndex + 1);
            NotifyInfo(in _notificationBuffer);
        }

        private void NotifyLoadoutUpdated(int slotIndex)
        {
            _notificationBuffer.Clear();
            AppendText(ref _notificationBuffer, "LOADOUT UPDATED - SLOT ");
            _notificationBuffer.AppendInt(slotIndex + 1);
            AppendText(ref _notificationBuffer, ": ");
            AppendUpperInvariant(ref _notificationBuffer, _selectedItem != null ? _selectedItem.itemName : null);
            NotifyInfo(in _notificationBuffer);
        }

        private void SetNoSelectionHintText(TMP_Text label)
        {
            EnsureCharCapacity(ref _detailTextBuffer, 48);
            int length;
            while (!TryWriteNoSelectionHint(_detailTextBuffer.AsSpan(), out length))
                EnsureCharCapacity(ref _detailTextBuffer, _detailTextBuffer.Length << 1);

            ApplyDynamicBuffer(label, _detailTextBuffer, length);
        }

        private bool TryWriteNoSelectionHint(Span<char> destination, out int length)
        {
            length = 0;
            if (_currentFilter == InventoryViewFilter.All)
                return TryWriteLiteral(destination, ref length, "SELECT AN ITEM".AsSpan());

            return TryWriteLiteral(destination, ref length, "NO ".AsSpan()) &&
                   TryWriteLiteral(destination, ref length, ResolveFilterEmptyLabelChars(_currentFilter)) &&
                   TryWriteLiteral(destination, ref length, " ITEM SELECTED".AsSpan());
        }

        private void SetSelectedDescriptionText(TMP_Text label)
        {
            if (_selectedItem == null)
            {
                ClearText(label);
                return;
            }

            string desc = string.IsNullOrEmpty(_selectedItem.description)
                ? ResolveLocalized(LocalizationKeys.ITEM_DESCRIPTION_FALLBACK, "No description available.")
                : _selectedItem.description;
            desc = ResolveStressReactiveItemDescription(_selectedItem, desc);
            ReadOnlySpan<char> descSpan = ReadOnlySpan<char>.Empty;
            if (!string.IsNullOrEmpty(desc))
                descSpan = desc.AsSpan();

            EnsureCharCapacity(ref _detailTextBuffer, descSpan.Length + 48);
            int length;
            while (!TryWriteSelectedDescriptionText(_detailTextBuffer.AsSpan(), descSpan, out length))
                EnsureCharCapacity(ref _detailTextBuffer, _detailTextBuffer.Length << 1);

            ApplyDynamicBuffer(label, _detailTextBuffer, length);
        }

        private bool TryWriteSelectedDescriptionText(Span<char> destination, ReadOnlySpan<char> description, out int length)
        {
            length = 0;
            return TryWriteLiteral(destination, ref length, "<color=#7FBFBA>[".AsSpan()) &&
                   TryWriteLiteral(destination, ref length, ResolveItemCategoryLabelChars(_selectedItem.category)) &&
                   TryWriteLiteral(destination, ref length, "]</color>\n".AsSpan()) &&
                   TryWriteLiteral(destination, ref length, description);
        }

        private void SetSelectedSizeText(TMP_Text label)
        {
            int stackCount = playerInventory != null
                ? playerInventory.GetStackCount(_selectedX, _selectedY)
                : 1;

            EnsureCharCapacity(ref _detailTextBuffer, 96);
            int length;
            while (!TryWriteSelectedSizeText(_detailTextBuffer.AsSpan(), stackCount, out length))
                EnsureCharCapacity(ref _detailTextBuffer, _detailTextBuffer.Length << 1);

            ApplyDynamicBuffer(label, _detailTextBuffer, length);
        }

        private bool TryWriteSelectedSizeText(Span<char> destination, int stackCount, out int length)
        {
            length = 0;
            if (_selectedItem == null)
                return true;

            if (!TryWriteLiteral(destination, ref length, "SIZE: ".AsSpan()) ||
                !TryWriteInt(destination, ref length, _selectedItem.width) ||
                !TryWriteLiteral(destination, ref length, "x".AsSpan()) ||
                !TryWriteInt(destination, ref length, _selectedItem.height) ||
                !TryWriteLiteral(destination, ref length, "  |  ".AsSpan()))
            {
                length = 0;
                return false;
            }

            if (_selectedItem.stackable)
            {
                if (!TryWriteLiteral(destination, ref length, "STACK ".AsSpan()) ||
                    !TryWriteInt(destination, ref length, Mathf.Max(1, stackCount)) ||
                    !TryWriteLiteral(destination, ref length, "/".AsSpan()) ||
                    !TryWriteInt(destination, ref length, Mathf.Max(1, _selectedItem.maxStack)))
                {
                    length = 0;
                    return false;
                }
            }
            else if (!TryWriteLiteral(destination, ref length, "NON-STACK".AsSpan()))
            {
                length = 0;
                return false;
            }

            if (_selectedItem.isConsumable)
                return TryWriteLiteral(destination, ref length, "  |  USE READY".AsSpan());

            return TryWriteLiteral(destination, ref length, "  |  FIELD ITEM".AsSpan());
        }

        private void SetSelectedItemEffectText(TMP_Text label)
        {
            EnsureCharCapacity(ref _detailTextBuffer, 128);
            int length;
            while (!TryWriteSelectedItemEffectText(_detailTextBuffer.AsSpan(), out length))
                EnsureCharCapacity(ref _detailTextBuffer, _detailTextBuffer.Length << 1);

            ApplyDynamicBuffer(label, _detailTextBuffer, length);
        }

        private bool TryWriteSelectedItemEffectText(Span<char> destination, out int length)
        {
            length = 0;
            if (_selectedItem == null)
                return true;

            if (_selectedItem.isConsumable)
            {
                if (!TryWriteLiteral(destination, ref length, "EFFECT:".AsSpan()))
                    return false;

                bool hasEffect = false;
                if (_selectedItem.oxygenRestore > 0f &&
                    !TryWriteRestoreSegment(destination, ref length, ref hasEffect, " O2 +".AsSpan(), _selectedItem.oxygenRestore))
                    return false;

                if (_selectedItem.energyRestore > 0f &&
                    !TryWriteRestoreSegment(destination, ref length, ref hasEffect, " PWR +".AsSpan(), _selectedItem.energyRestore))
                    return false;

                if (_selectedItem.integrityRestore > 0f &&
                    !TryWriteRestoreSegment(destination, ref length, ref hasEffect, " HLT +".AsSpan(), _selectedItem.integrityRestore))
                    return false;

                if (hasEffect)
                    return true;

                length = 0;
                return TryWriteLiteral(destination, ref length, "EFFECT: CONSUMABLE WITH NO RESTORE PROFILE".AsSpan());
            }

            if (IsSelectedItemAssignableTool())
            {
                GameObject prefab = toolManager != null ? toolManager.GetKnownToolPrefabForItem(_selectedItem) : null;
                PlayerTool tool = prefab != null ? prefab.GetComponent<PlayerTool>() : null;
                if (tool != null && tool.Metadata != null)
                {
                    return TryWriteLiteral(destination, ref length, "TOOL PROFILE: DURABILITY ".AsSpan()) &&
                           TryWriteInt(destination, ref length, Mathf.RoundToInt(tool.Metadata.maxDurability)) &&
                           TryWriteLiteral(destination, ref length, "  |  ENERGY ".AsSpan()) &&
                           TryWriteFloat(destination, ref length, Mathf.Max(0f, tool.Metadata.energyConsumptionRate), "0.0") &&
                           TryWriteLiteral(destination, ref length, "/s".AsSpan());
                }

                return TryWriteLiteral(destination, ref length, "TOOL PROFILE: ASSIGNABLE FIELD EQUIPMENT".AsSpan());
            }

            if (_selectedItem.worldPrefab != null)
                return TryWriteLiteral(destination, ref length, "FIELD PROFILE: CAN BE DEPLOYED OR DROPPED INTO THE WORLD".AsSpan());

            return TryWriteLiteral(destination, ref length, "FIELD PROFILE: CARGO MATERIAL / COMPONENT".AsSpan());
        }

        private void SetSelectedItemStatusText(TMP_Text label)
        {
            EnsureCharCapacity(ref _detailTextBuffer, 128);
            int length;
            while (!TryWriteSelectedItemStatusText(_detailTextBuffer.AsSpan(), out length))
                EnsureCharCapacity(ref _detailTextBuffer, _detailTextBuffer.Length << 1);

            ApplyDynamicBuffer(label, _detailTextBuffer, length);
        }

        private bool TryWriteSelectedItemStatusText(Span<char> destination, out int length)
        {
            length = 0;
            if (_selectedItem == null)
                return true;

            int stackCount = playerInventory != null
                ? playerInventory.GetStackCount(_selectedX, _selectedY)
                : 1;

            if (_selectedItem.isConsumable)
            {
                return TryWriteLiteral(destination, ref length, "STATUS: READY FOR USE  |  STOCK ".AsSpan()) &&
                       TryWriteInt(destination, ref length, Mathf.Max(1, stackCount));
            }

            if (IsSelectedItemAssignableTool())
            {
                GameObject knownPrefab = toolManager != null
                    ? toolManager.GetKnownToolPrefabForItem(_selectedItem)
                    : null;

                if (knownPrefab == null)
                    return TryWriteLiteral(destination, ref length, "STATUS: TOOL ITEM  |  NO HELD-PREFAB REGISTRY".AsSpan());

                for (int i = 0; i < ToolSlotCount; i++)
                {
                    GameObject assigned = toolManager != null ? toolManager.GetAssignedToolPrefab(i) : null;
                    if (!ReferenceEquals(assigned, knownPrefab))
                        continue;

                    bool available = toolManager != null && toolManager.IsToolAvailableInSlot(i);
                    if (!TryWriteLiteral(destination, ref length, "STATUS: LOADOUT SLOT ".AsSpan()) ||
                        !TryWriteInt(destination, ref length, i + 1))
                    {
                        length = 0;
                        return false;
                    }

                    if (available)
                        return TryWriteLiteral(destination, ref length, " READY".AsSpan());

                    return TryWriteLiteral(destination, ref length, " ASSIGNED, CARGO MISSING".AsSpan());
                }

                return TryWriteLiteral(destination, ref length, "STATUS: FIELD TOOL  |  NOT ASSIGNED TO LOADOUT".AsSpan());
            }

            if (_selectedItem.stackable)
            {
                return TryWriteLiteral(destination, ref length, "STATUS: CARGO STACK  |  ".AsSpan()) &&
                       TryWriteInt(destination, ref length, Mathf.Max(1, stackCount)) &&
                       TryWriteLiteral(destination, ref length, " UNITS AVAILABLE".AsSpan());
            }

            return TryWriteLiteral(destination, ref length, "STATUS: SINGLE CARGO UNIT".AsSpan());
        }

        private void SetSelectedItemActionText(TMP_Text label)
        {
            EnsureCharCapacity(ref _detailTextBuffer, 128);
            int length;
            while (!TryWriteSelectedItemActionText(_detailTextBuffer.AsSpan(), out length))
                EnsureCharCapacity(ref _detailTextBuffer, _detailTextBuffer.Length << 1);

            ApplyDynamicBuffer(label, _detailTextBuffer, length);
        }

        private bool TryWriteSelectedItemActionText(Span<char> destination, out int length)
        {
            length = 0;
            if (_selectedItem == null)
                return true;

            if (_selectedItem.isConsumable)
                return TryWriteLiteral(destination, ref length, "NEXT ACTION: USE NOW FOR IMMEDIATE SUIT RESTORATION, OR KEEP IN CARGO AS RESERVE.".AsSpan());

            if (IsSelectedItemAssignableTool())
            {
                GameObject knownPrefab = toolManager != null
                    ? toolManager.GetKnownToolPrefabForItem(_selectedItem)
                    : null;

                if (knownPrefab == null)
                    return TryWriteLiteral(destination, ref length, "NEXT ACTION: AUTHOR A HELD PREFAB FOR THIS TOOL BEFORE ADDING IT TO QUICK SLOTS.".AsSpan());

                return TryWriteLiteral(destination, ref length, "NEXT ACTION: ASSIGN TO A LOADOUT SLOT BELOW, THEN ARM IT FROM THE LOADOUT TAB.".AsSpan());
            }

            if (_selectedItem.worldPrefab != null)
                return TryWriteLiteral(destination, ref length, "NEXT ACTION: KEEP AS FIELD RESOURCE OR DROP TO THE WORLD IF CARGO SPACE IS NEEDED.".AsSpan());

            return TryWriteLiteral(destination, ref length, "NEXT ACTION: HOLD FOR FABRICATION, RECIPES, OR FUTURE COMPONENT CHAINS.".AsSpan());
        }

        private bool TryWriteSelectedPrimaryActionLabel(Span<char> destination, out int length)
        {
            length = 0;
            if (_selectedItem == null)
                return true;

            if (_selectedItem.isConsumable)
                return TryWriteLiteral(destination, ref length, "USE".AsSpan());

            if (!IsSelectedItemAssignableTool() || toolManager == null)
                return true;

            GameObject prefab = toolManager.GetKnownToolPrefabForItem(_selectedItem);
            if (prefab == null)
                return TryWriteLiteral(destination, ref length, "NO PREFAB".AsSpan());

            for (int i = 0; i < ToolSlotCount; i++)
            {
                if (!ReferenceEquals(toolManager.GetAssignedToolPrefab(i), prefab))
                    continue;

                if (toolManager.CurrentSlotIndex == i)
                    return TryWriteLiteral(destination, ref length, "HOLSTER".AsSpan());

                bool available = toolManager.IsToolAvailableInSlot(i);
                if (available)
                {
                    if (!TryWriteLiteral(destination, ref length, "ACTIVATE S".AsSpan()))
                        return false;
                }
                else if (!TryWriteLiteral(destination, ref length, "RE-ARM S".AsSpan()))
                {
                    return false;
                }

                return TryWriteInt(destination, ref length, i + 1);
            }

            int recommendedSlot = GetRecommendedLoadoutSlot();
            if (recommendedSlot < 0)
                return TryWriteLiteral(destination, ref length, "ARM".AsSpan());

            return TryWriteLiteral(destination, ref length, "ARM S".AsSpan()) &&
                   TryWriteInt(destination, ref length, recommendedSlot + 1);
        }

        private Color GetSelectedPrimaryActionColor()
        {
            if (_selectedItem == null || _selectedItem.isConsumable)
                return new Color(0.1f, 0.4f, 0.35f, 0.6f);

            if (!IsSelectedItemAssignableTool() || toolManager == null)
                return new Color(0.1f, 0.4f, 0.35f, 0.6f);

            GameObject prefab = toolManager.GetKnownToolPrefabForItem(_selectedItem);
            if (prefab == null)
                return new Color(0.28f, 0.16f, 0.08f, 0.76f);

            for (int i = 0; i < ToolSlotCount; i++)
            {
                if (!ReferenceEquals(toolManager.GetAssignedToolPrefab(i), prefab))
                    continue;

                if (toolManager.CurrentSlotIndex == i)
                    return new Color(0.24f, 0.18f, 0.08f, 0.76f);

                return toolManager.IsToolAvailableInSlot(i)
                    ? new Color(0.08f, 0.28f, 0.34f, 0.76f)
                    : new Color(0.28f, 0.16f, 0.08f, 0.76f);
            }

            return new Color(0.08f, 0.22f, 0.34f, 0.76f);
        }

        private Color GetSelectedDetailAccentColor(float alpha)
        {
            if (_selectedItem == null)
                return new Color(0.08f, 0.18f, 0.2f, alpha);

            Color c;
            switch (_selectedItem.category)
            {
                case ItemCategory.Consumable:
                    c = new Color(0.08f, 0.24f, 0.2f, alpha);
                    break;
                case ItemCategory.Tool:
                case ItemCategory.Equipment:
                    c = new Color(0.08f, 0.2f, 0.3f, alpha);
                    break;
                case ItemCategory.Material:
                    c = new Color(0.2f, 0.18f, 0.08f, alpha);
                    break;
                case ItemCategory.Component:
                    c = new Color(0.18f, 0.12f, 0.28f, alpha);
                    break;
                default:
                    c = new Color(0.08f, 0.18f, 0.2f, alpha);
                    break;
            }

            return c;
        }

        private Color GetSelectedDetailStatusColor()
        {
            if (_selectedItem == null)
                return new Color(0.08f, 0.2f, 0.22f, 0.68f);

            if (_selectedItem.isConsumable)
                return new Color(0.08f, 0.24f, 0.2f, 0.72f);

            if (IsSelectedItemAssignableTool() && toolManager != null)
            {
                GameObject prefab = toolManager.GetKnownToolPrefabForItem(_selectedItem);
                if (prefab == null)
                    return new Color(0.3f, 0.2f, 0.06f, 0.78f);

                for (int i = 0; i < ToolSlotCount; i++)
                {
                    if (!ReferenceEquals(toolManager.GetAssignedToolPrefab(i), prefab))
                        continue;

                    return toolManager.IsToolAvailableInSlot(i)
                        ? new Color(0.08f, 0.22f, 0.3f, 0.76f)
                        : new Color(0.3f, 0.2f, 0.06f, 0.8f);
                }

                return new Color(0.08f, 0.16f, 0.26f, 0.72f);
            }

            return new Color(0.08f, 0.2f, 0.22f, 0.68f);
        }

        private Color GetSelectedDetailActionColor()
        {
            if (_selectedItem == null)
                return new Color(0.06f, 0.12f, 0.14f, 0.58f);

            if (_selectedItem.isConsumable)
                return new Color(0.08f, 0.22f, 0.18f, 0.64f);

            if (IsSelectedItemAssignableTool())
                return new Color(0.08f, 0.16f, 0.26f, 0.62f);

            if (_selectedItem.worldPrefab != null)
                return new Color(0.18f, 0.16f, 0.08f, 0.6f);

            return new Color(0.06f, 0.12f, 0.14f, 0.58f);
        }

        private int GetRecommendedLoadoutSlot()
        {
            if (!IsSelectedItemAssignableTool() || toolManager == null)
                return -1;

            GameObject knownPrefab = toolManager.GetKnownToolPrefabForItem(_selectedItem);
            if (knownPrefab == null)
                return -1;

            for (int i = 0; i < ToolSlotCount; i++)
            {
                if (ReferenceEquals(toolManager.GetAssignedToolPrefab(i), knownPrefab))
                    return i;
            }

            string itemName = _selectedItem.itemName != null
                ? _selectedItem.itemName.ToLowerInvariant()
                : string.Empty;

            int preferredSlot = -1;
            if (itemName.Contains("scanner"))
                preferredSlot = 0;
            else if (itemName.Contains("repair"))
                preferredSlot = 1;
            else if (itemName.Contains("light") || itemName.Contains("flash"))
                preferredSlot = 2;
            else if (itemName.Contains("builder") || itemName.Contains("cutter"))
                preferredSlot = 3;

            if (preferredSlot >= 0)
            {
                GameObject assigned = toolManager.GetAssignedToolPrefab(preferredSlot);
                if (assigned == null || !toolManager.IsToolAvailableInSlot(preferredSlot))
                    return preferredSlot;
            }

            for (int i = 0; i < ToolSlotCount; i++)
            {
                if (toolManager.GetAssignedToolPrefab(i) == null)
                    return i;
            }

            for (int i = 0; i < ToolSlotCount; i++)
            {
                if (!toolManager.IsToolAvailableInSlot(i))
                    return i;
            }

            return 0;
        }

        private void ClearChildren(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            return Hecton8.Core.GlobalRegistry.Localization != null
                ? Hecton8.Core.GlobalRegistry.Localization.GetOrFallback(Hecton8.Core.GlobalRegistry.Localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string ResolveStressReactiveItemDescription(Hecton8.Items.ItemData item, string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null)
                return text;

            if (item == null)
                return manager.ApplyHullStressCorruptionIfNeeded(text);

            string token = !string.IsNullOrWhiteSpace(item.DescriptionTableKey)
                ? item.DescriptionTableKey
                : item.PersistentId;
            return manager.ApplyPdaLoreCorruptionIfNeeded(token, text);
        }

    }
    // ══════════════════════════════════════════════════════════════
    //  HELPER: Drop Item Button
    // ══════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════
    //  HELPER: Use Item Button
    // ══════════════════════════════════════════════════════════════

    [DisallowMultipleComponent]
    internal sealed class UseItemButton : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAInventoryTab _tab;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDAInventoryTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _tab?.PerformPrimarySelectedAction();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPER: Sort Button
    // ══════════════════════════════════════════════════════════════

    [DisallowMultipleComponent]
    internal sealed class SortButton : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAInventoryTab _tab;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDAInventoryTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _tab?.SortInventoryAction();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PDAInventoryPageButton : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAInventoryTab _tab;
        private int _direction;
        private Image _bg;
        private TMP_Text _label;
        private Color _activeBackgroundColor;
        private Color _inactiveBackgroundColor;
        private Color _activeLabelColor;
        private Color _inactiveLabelColor;
        private bool _isActive;

        public void Init(
            PDAInventoryTab tab,
            int direction,
            Image bg,
            TMP_Text label,
            Color activeBackgroundColor,
            Color inactiveBackgroundColor,
            Color activeLabelColor,
            Color inactiveLabelColor)
        {
            _tab = tab;
            _direction = direction;
            _bg = bg;
            _label = label;
            _activeBackgroundColor = activeBackgroundColor;
            _inactiveBackgroundColor = inactiveBackgroundColor;
            _activeLabelColor = activeLabelColor;
            _inactiveLabelColor = inactiveLabelColor;
            SetActive(false);
        }

        public void SetActive(bool isActive)
        {
            _isActive = isActive;

            if (_bg != null)
                _bg.color = isActive ? _activeBackgroundColor : _inactiveBackgroundColor;

            if (_label != null)
                _label.color = isActive ? _activeLabelColor : _inactiveLabelColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isActive)
                _tab?.ChangePage(_direction);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isActive && _bg != null)
                _bg.color = _activeBackgroundColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null)
                _bg.color = _isActive ? _activeBackgroundColor : _inactiveBackgroundColor;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class DropItemButton : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAInventoryTab _tab;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDAInventoryTab tab, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _tab?.DropSelectedItem();
        }
 
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPER: Grid Pointer Handler
    // ══════════════════════════════════════════════════════════════

    [DisallowMultipleComponent]
    internal sealed class GridPointerHandler : MonoBehaviour,
        IPointerMoveHandler, IPointerClickHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private PDAInventoryTab _tab;
        private bool _suppressClick;

        public void Init(PDAInventoryTab tab)
        {
            _tab = tab;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _suppressClick = false;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_tab == null || !TryResolveLocalPoint(eventData, out Vector2 localPoint))
                return;
            _tab.HandlePointerMove(localPoint);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_suppressClick)
            {
                _suppressClick = false;
                return;
            }

            if (_tab == null || !TryResolveLocalPoint(eventData, out Vector2 localPoint))
                return;
            _tab.HandlePointerClick(localPoint);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _suppressClick = true;
            if (_tab == null || !TryResolveLocalPoint(eventData, out Vector2 localPoint))
                return;
            _tab.HandlePointerBeginDrag(localPoint);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_tab == null || !TryResolveLocalPoint(eventData, out Vector2 localPoint))
                return;
            _tab.HandlePointerDrag(localPoint);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_tab == null || !TryResolveLocalPoint(eventData, out Vector2 localPoint))
                return;
            _tab.HandlePointerEndDrag(localPoint);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _tab?.HandlePointerExit();
        }

        private bool TryResolveLocalPoint(PointerEventData eventData, out Vector2 localPoint)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPER: Loadout Assign Button
    // ══════════════════════════════════════════════════════════════

    [DisallowMultipleComponent]
    internal sealed class LoadoutAssignButton : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDAInventoryTab _tab;
        private int _slotIndex;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDAInventoryTab tab, int slotIndex, Image bg, Color normal, Color hover)
        {
            _tab = tab;
            _slotIndex = slotIndex;
            _bg = bg;
            _normalColor = normal;
            _hoverColor = hover;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _tab?.AssignSelectedItemToSlot(_slotIndex);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_bg != null) _bg.color = _normalColor;
        }
    }
}
