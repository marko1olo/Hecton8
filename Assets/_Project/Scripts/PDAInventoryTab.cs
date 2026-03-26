// ============================================================================
// HECTON-8 — PDAInventoryTab.cs
// Вкладка инвентаря внутри PDA.
// Строит UI программно. Читает PlayerInventory и PlayerToolManager.
// Вешается на GameObject вкладки Tab_Inventory.
// ============================================================================

using System;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Inventory Tab")]
    public sealed class PDAInventoryTab : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerPDA playerPDA;
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
        private const int ToolSlotCount = 4;

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
        private int _activeBlockCount;
        // Stack count labels
        private TextMeshProUGUI[] _blockCounts;

        // Drop button
        private RectTransform _dropButtonRoot;
        private Image _dropButtonBg;

        // Highlights
        private RectTransform _hoverRect;
        private Image _hoverImage;
        private RectTransform _selectRect;
        private Image _selectImage;

        // Pointer overlay
        private RectTransform _pointerOverlay;

        // Details panel
        private RectTransform _detailsRoot;
        private Image _detailIcon;
        private TextMeshProUGUI _detailName;
        private TextMeshProUGUI _detailDesc;
        private TextMeshProUGUI _detailWeight;
        private TextMeshProUGUI _detailSize;
        private TextMeshProUGUI _detailHint;

        // Tool strip
        private RectTransform _toolStripRoot;
        private Image[] _toolSlotBgs;
        private Image[] _toolSlotIcons;
        private TextMeshProUGUI[] _toolSlotKeys;

        // Weight
        private TextMeshProUGUI _weightLabel;

        // Tab bar
        private RectTransform _tabBarRoot;
        private PDATabButton[] _tabButtons;

        // USE button
        private RectTransform _useButtonRoot;
        private Image _useButtonBg;

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
        private int _hoverX = -1;
        private int _hoverY = -1;

        // Placement buffer (pre-allocated)
        private PlayerInventory.ItemPlacement[] _placementBuffer;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _placementBuffer = new PlayerInventory.ItemPlacement[MaxItems];
        }

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        // ══════════════════════════════════════════════════════════
        //  AUTO-RESOLVE
        // ══════════════════════════════════════════════════════════

        private void AutoResolve()
        {
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (toolManager == null)
                toolManager = FindFirstObjectByType<PlayerToolManager>();
            if (playerPDA == null)
                playerPDA = GetComponentInParent<PlayerPDA>();
            if (playerPDA == null)
                playerPDA = FindFirstObjectByType<PlayerPDA>();
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT SUBSCRIPTIONS
        // ══════════════════════════════════════════════════════════

        private void Subscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged += OnInventoryChanged;
            if (toolManager != null)
                toolManager.ActiveSlotChanged += OnToolSlotChanged;
            PDAEvents.OnTabChanged += OnTabChanged;
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged -= OnInventoryChanged;
            if (toolManager != null)
                toolManager.ActiveSlotChanged -= OnToolSlotChanged;
            PDAEvents.OnTabChanged -= OnTabChanged;
        }

        private void OnInventoryChanged() => RefreshGrid();
        private void OnToolSlotChanged(int _) => RefreshToolStrip();

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

            // Check if tab bar already exists
            Transform existing = parent.Find("PDA_TabBar");
            if (existing != null)
            {
                _tabBarRoot = existing as RectTransform;
                // Collect existing buttons
                _tabButtons = _tabBarRoot.GetComponentsInChildren<PDATabButton>(true);
                return;
            }

            _tabBarRoot = CreateRect("PDA_TabBar", parent);
            Anchor(_tabBarRoot, new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(0f, -4f), new Vector2(0f, 36f));

            string[] labels = { "INVENTORY", "CONTROLS", "DATA LOG" };
            _tabButtons = new PDATabButton[labels.Length];
            float tabWidth = 140f;
            float totalWidth = labels.Length * tabWidth + (labels.Length - 1) * 6f;
            float startX = -totalWidth * 0.5f + tabWidth * 0.5f;

            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform tabRect = CreateRect("Tab_" + i, _tabBarRoot);
                Anchor(tabRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                       new Vector2(startX + i * (tabWidth + 6f), 0f),
                       new Vector2(tabWidth, 30f));

                Image tabBg = tabRect.gameObject.AddComponent<Image>();
                tabBg.color = i == 0 ? TabBgActive : TabBgInactive;
                tabBg.raycastTarget = true;

                TextMeshProUGUI tabLabel = CreateText("Label", tabRect, 11f,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(tabLabel.rectTransform);
                tabLabel.text = labels[i];
                tabLabel.color = i == 0 ? TabActive : TabInactive;

                PDATabButton btn = tabRect.gameObject.AddComponent<PDATabButton>();
                btn.Init(i, playerPDA, tabBg, tabLabel,
                         TabBgActive, TabBgInactive, TabActive, TabInactive);
                _tabButtons[i] = btn;
            }
        }

        // ──────────────────────────────────────────────────────────
        //  GRID
        // ──────────────────────────────────────────────────────────

        private void BuildGrid(RectTransform parent)
        {
            _gridArea = CreateRect("GridArea", parent);
            _gridArea.pivot = new Vector2(0f, 1f);
            _gridArea.anchorMin = new Vector2(0f, 1f);
            _gridArea.anchorMax = new Vector2(0f, 1f);
            _gridArea.anchoredPosition = new Vector2(32f, -52f);
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
            _hoverRect.gameObject.SetActive(false);

            // Selection highlight
            _selectRect = CreateRect("Select", _gridArea);
            _selectImage = _selectRect.gameObject.AddComponent<Image>();
            _selectImage.color = SelectTint;
            _selectImage.raycastTarget = false;
            _selectRect.pivot = new Vector2(0f, 1f);
            _selectRect.anchorMin = new Vector2(0f, 1f);
            _selectRect.anchorMax = new Vector2(0f, 1f);
            _selectRect.gameObject.SetActive(false);

            // Item block pool
            _blockRects = new RectTransform[MaxItems];
            _blockBgs = new Image[MaxItems];
            _blockIcons = new Image[MaxItems];
            _blockCounts = new TextMeshProUGUI[MaxItems];

            for (int i = 0; i < MaxItems; i++)
            {
                RectTransform br = CreateRect("Block_" + i, _gridArea);
                br.pivot = new Vector2(0f, 1f);
                br.anchorMin = new Vector2(0f, 1f);
                br.anchorMax = new Vector2(0f, 1f);

                Image bbg = br.gameObject.AddComponent<Image>();
                bbg.color = ItemBlock;
                bbg.raycastTarget = false;

                RectTransform iconRect = CreateRect("Icon", br);
                Stretch(iconRect, 6f, 6f, 6f, 6f);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = Color.white;
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
                countLbl.gameObject.SetActive(false);
                br.gameObject.SetActive(false);

                _blockCounts[i] = countLbl;

                _blockRects[i] = br;
                _blockBgs[i] = bbg;
                _blockIcons[i] = icon;
            }

            // Pointer overlay (transparent, catches all clicks)
            _pointerOverlay = CreateRect("PointerOverlay", _gridArea);
            _pointerOverlay.pivot = new Vector2(0f, 1f);
            _pointerOverlay.anchorMin = new Vector2(0f, 1f);
            _pointerOverlay.anchorMax = new Vector2(0f, 1f);
            _pointerOverlay.anchoredPosition = Vector2.zero;
            _pointerOverlay.sizeDelta = GridAreaSize;
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
            _detailsRoot = CreateRect("DetailsPanel", parent);
            _detailsRoot.pivot = new Vector2(1f, 1f);
            _detailsRoot.anchorMin = new Vector2(1f, 1f);
            _detailsRoot.anchorMax = new Vector2(1f, 1f);
            _detailsRoot.anchoredPosition = new Vector2(-28f, -52f);
            _detailsRoot.sizeDelta = new Vector2(260f, GridAreaSize.y);

            Image detBg = _detailsRoot.gameObject.AddComponent<Image>();
            detBg.color = DetailBg;
            detBg.raycastTarget = false;

            // Icon
            RectTransform iconBox = CreateRect("DetailIconBox", _detailsRoot);
            Anchor(iconBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                   new Vector2(0f, -20f), new Vector2(80f, 80f));
            _detailIcon = iconBox.gameObject.AddComponent<Image>();
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;
            _detailIcon.color = Color.white;

            // Name
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

            // Hint (shown when nothing selected)
            _detailHint = CreateText("DetailHint", _detailsRoot, 12f,
                FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_detailHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                   Vector2.zero, Vector2.zero);
            _detailHint.color = A(DimLow, 0.4f);
            _detailHint.text = "SELECT AN ITEM";
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
            dropLabel.text = "DROP";
            dropLabel.color = new Color(1f, 0.85f, 0.8f, 0.9f);

            DropItemButton dropBtn = _dropButtonRoot.gameObject.AddComponent<DropItemButton>();
            dropBtn.Init(this, _dropButtonBg,
                new Color(0.6f, 0.15f, 0.12f, 0.6f),
                new Color(0.8f, 0.2f, 0.15f, 0.8f));

            _dropButtonRoot.gameObject.SetActive(false);

            // USE button
            _useButtonRoot = CreateRect("UseButton", _detailsRoot);
            Anchor(_useButtonRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 56f), new Vector2(120f, 30f));

            _useButtonBg = _useButtonRoot.gameObject.AddComponent<Image>();
            _useButtonBg.color = new Color(0.1f, 0.4f, 0.35f, 0.6f);
            _useButtonBg.raycastTarget = true;

            TextMeshProUGUI useLabel = CreateText("UseLabel", _useButtonRoot, 12f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(useLabel.rectTransform);
            useLabel.text = "USE";
            useLabel.color = new Color(0.7f, 1f, 0.95f, 0.9f);

            UseItemButton useBtn = _useButtonRoot.gameObject.AddComponent<UseItemButton>();
            useBtn.Init(this, _useButtonBg,
                new Color(0.1f, 0.4f, 0.35f, 0.6f),
                new Color(0.15f, 0.55f, 0.48f, 0.8f));

            _useButtonRoot.gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────
        //  TOOL STRIP
        // ──────────────────────────────────────────────────────────

        private void BuildToolStrip(RectTransform parent)
        {
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
            hdr.text = "EQUIPMENT";
            hdr.color = A(DimLow, 0.6f);

            _toolSlotBgs = new Image[ToolSlotCount];
            _toolSlotIcons = new Image[ToolSlotCount];
            _toolSlotKeys = new TextMeshProUGUI[ToolSlotCount];

            for (int i = 0; i < ToolSlotCount; i++)
            {
                RectTransform slot = CreateRect("ToolSlot_" + i, _toolStripRoot);
                slot.pivot = new Vector2(0f, 0f);
                slot.anchorMin = new Vector2(0f, 0f);
                slot.anchorMax = new Vector2(0f, 0f);
                slot.anchoredPosition = new Vector2(i * (slotSize + slotGap), 0f);
                slot.sizeDelta = new Vector2(slotSize, slotSize);

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
                icon.gameObject.SetActive(false);
                _toolSlotIcons[i] = icon;

                TextMeshProUGUI keyLbl = CreateText("Key", slot, 9f,
                    FontStyles.Bold, TextAlignmentOptions.TopLeft);
                Anchor(keyLbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                       new Vector2(4f, -3f), new Vector2(16f, 14f));
                keyLbl.text = (i + 1).ToString();
                keyLbl.color = A(DimLow, 0.5f);
                _toolSlotKeys[i] = keyLbl;
            }
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
                   new Vector2(0f, -42f), new Vector2(0f, 1f));
            topRule.raycastTarget = false;

            // Vertical separator between grid and details
            float sepX = 32f + GridAreaSize.x + 14f;
            Image vSep = CreateImage("VSep", parent, A(RuleLine, 0.3f));
            vSep.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            vSep.rectTransform.anchorMin = new Vector2(0f, 0f);
            vSep.rectTransform.anchorMax = new Vector2(0f, 1f);
            vSep.rectTransform.anchoredPosition = new Vector2(sepX, 0f);
            vSep.rectTransform.sizeDelta = new Vector2(1f, -100f);
            vSep.raycastTarget = false;

            // SORT button (top-right of grid area)
            _sortButtonRoot = CreateRect("SortButton", parent);
            _sortButtonRoot.pivot = new Vector2(0f, 1f);
            _sortButtonRoot.anchorMin = new Vector2(0f, 1f);
            _sortButtonRoot.anchorMax = new Vector2(0f, 1f);
            _sortButtonRoot.anchoredPosition = new Vector2(
                32f + GridAreaSize.x - 60f, -52f + GridAreaSize.y + 22f);
            _sortButtonRoot.sizeDelta = new Vector2(60f, 20f);

            Image sortBg = _sortButtonRoot.gameObject.AddComponent<Image>();
            sortBg.color = new Color(0.08f, 0.16f, 0.18f, 0.6f);
            sortBg.raycastTarget = true;

            TextMeshProUGUI sortLbl = CreateText("SortLabel", _sortButtonRoot, 10f,
                FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(sortLbl.rectTransform);
            sortLbl.text = "SORT";
            sortLbl.color = A(DimLow, 0.7f);

            SortButton sortBtn = _sortButtonRoot.gameObject.AddComponent<SortButton>();
            sortBtn.Init(this, sortBg,
                new Color(0.08f, 0.16f, 0.18f, 0.6f),
                new Color(0.12f, 0.24f, 0.28f, 0.8f));
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
        }

        private void RefreshGrid()
        {
            if (playerInventory == null || _cellImages == null) return;

            InventoryGrid grid = playerInventory.Grid;
            if (grid == null) return;

            // Update cell tints
            for (int y = 0; y < GridRows; y++)
            {
                for (int x = 0; x < GridCols; x++)
                {
                    int idx = y * GridCols + x;
                    if (idx >= _cellImages.Length) continue;
                    ItemData cell = grid.GetCell(x, y);
                    _cellImages[idx].color = cell != null ? CellOccupied : CellEmpty;
                }
            }

            // Get placements
            int count = playerInventory.GetPlacements(_placementBuffer);

            // Activate/position blocks
            for (int i = 0; i < MaxItems; i++)
            {
                if (i < count)
                {
                    var p = _placementBuffer[i];
                    _blockRects[i].gameObject.SetActive(true);
                    _blockRects[i].anchoredPosition = new Vector2(
                        p.x * CellStep,
                        -p.y * CellStep);
                    _blockRects[i].sizeDelta = new Vector2(
                        p.item.width * CellStep - CellGap,
                        p.item.height * CellStep - CellGap);

                    _blockBgs[i].color = ItemBlock;

                    if (p.item.icon != null)
                    {
                        _blockIcons[i].sprite = p.item.icon;
                        _blockIcons[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        _blockIcons[i].gameObject.SetActive(false);
                    }

                    // Stack count badge
                    if (_blockCounts != null && i < _blockCounts.Length && _blockCounts[i] != null)
                    {
                        if (p.stackCount > 1)
                        {
                            _blockCounts[i].gameObject.SetActive(true);
                            _blockCounts[i].SetText("×{0}", p.stackCount);
                        }
                        else
                        {
                            _blockCounts[i].gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    _blockRects[i].gameObject.SetActive(false);
                    if (_blockCounts != null && i < _blockCounts.Length && _blockCounts[i] != null)
                        _blockCounts[i].gameObject.SetActive(false);
                }
            }

            _activeBlockCount = count;
            RefreshWeight();
        }

        private void RefreshDetails()
        {
            bool hasSelection = _selectedItem != null;

            if (_detailIcon != null) _detailIcon.gameObject.SetActive(hasSelection);
            if (_detailName != null) _detailName.gameObject.SetActive(hasSelection);
            if (_detailDesc != null) _detailDesc.gameObject.SetActive(hasSelection);
            if (_detailWeight != null) _detailWeight.gameObject.SetActive(hasSelection);
            if (_detailSize != null) _detailSize.gameObject.SetActive(hasSelection);
            if (_detailHint != null) _detailHint.gameObject.SetActive(!hasSelection);
            if (_dropButtonRoot != null) _dropButtonRoot.gameObject.SetActive(hasSelection);

            if (!hasSelection) return;

            if (_detailIcon != null)
            {
                _detailIcon.sprite = _selectedItem.icon;
                _detailIcon.color = _selectedItem.icon != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.1f);
            }

            if (_detailName != null)
                _detailName.text = _selectedItem.itemName.ToUpperInvariant();

            if (_detailDesc != null)
            {
                string desc = string.IsNullOrEmpty(_selectedItem.description)
                    ? "No description available."
                    : _selectedItem.description;
                string cat = _selectedItem.category.ToString().ToUpperInvariant();
                _detailDesc.text = $"<color=#7FBFBA>[{cat}]</color>\n{desc}";
            }

            if (_detailWeight != null)
            {
                int stk = playerInventory != null
                    ? playerInventory.GetStackCount(_selectedX, _selectedY) : 1;
                if (stk > 1)
                    _detailWeight.SetText("MASS: {0:0.0} kg (×{1} = {2:0.0})",
                        _selectedItem.weight, stk, _selectedItem.weight * stk);
                else
                    _detailWeight.SetText("MASS: {0:0.0} kg", _selectedItem.weight);
            }

            if (_detailSize != null)
                _detailSize.SetText("SIZE: {0}×{1}", _selectedItem.width, _selectedItem.height);

            // Show/hide USE button based on consumable flag
            if (_useButtonRoot != null)
                _useButtonRoot.gameObject.SetActive(_selectedItem.isConsumable);
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
                    _toolSlotIcons[i].gameObject.SetActive(true);
                    _toolSlotIcons[i].color = toolManager.IsToolAvailableInSlot(i)
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.25f);
                }
                else
                {
                    _toolSlotIcons[i].gameObject.SetActive(false);
                }

                _toolSlotKeys[i].color = isActive ? A(Primary, 0.9f) : A(DimLow, 0.5f);
            }
        }

        private void RefreshWeight()
        {
            if (_weightLabel == null || playerInventory == null) return;
            _weightLabel.SetText("CARGO: {0:0.0} kg", playerInventory.TotalWeight);
        }

        // ══════════════════════════════════════════════════════════
        //  POINTER INTERACTION
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// Вызывается кнопкой DROP. Удаляет одну единицу из стека,
        /// спавнит worldPrefab перед игроком.
        /// </summary>
        internal void DropSelectedItem()
        {
            if (_selectedItem == null || playerInventory == null) return;

            PlayUISound(dropSound);

            ItemData dropped = playerInventory.RemoveOneItem(_selectedX, _selectedY);
            if (dropped == null) return;

            // Спавн в мир
            if (dropped.worldPrefab != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 spawnPos = cam.transform.position
                        + cam.transform.forward * 2.5f
                        + Vector3.down * 0.3f;

                    ObjectPoolManager pool = ObjectPoolManager.Instance;
                    if (pool != null)
                        pool.Spawn(dropped.worldPrefab, spawnPos, Quaternion.identity);
                    else
                        Instantiate(dropped.worldPrefab, spawnPos, Quaternion.identity);
                }
            }

            // Проверяем остался ли предмет на этой позиции
            ItemData remaining = playerInventory.Grid.GetCell(_selectedX, _selectedY);
            if (!ReferenceEquals(remaining, _selectedItem))
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
            int gx = Mathf.FloorToInt(localPoint.x / CellStep);
            int gy = Mathf.FloorToInt(-localPoint.y / CellStep);

            if (gx < 0 || gx >= GridCols || gy < 0 || gy >= GridRows)
            {
                ClearHover();
                return;
            }

            if (gx == _hoverX && gy == _hoverY) return;

            _hoverX = gx;
            _hoverY = gy;

            ItemData cell = playerInventory != null
                ? playerInventory.Grid.GetCell(gx, gy)
                : null;

            if (cell != null)
            {
                // Find anchor of this item
                FindAnchor(cell, gx, gy, out int ax, out int ay);
                PositionHighlight(_hoverRect, ax, ay, cell.width, cell.height);
                _hoverRect.gameObject.SetActive(true);
                _hoverImage.color = HoverTint;
            }
            else
            {
                PositionHighlight(_hoverRect, gx, gy, 1, 1);
                _hoverRect.gameObject.SetActive(true);
                _hoverImage.color = A(HoverTint, 0.1f);
            }
        }

        internal void HandlePointerClick(Vector2 localPoint)
        {
            int gx = Mathf.FloorToInt(localPoint.x / CellStep);
            int gy = Mathf.FloorToInt(-localPoint.y / CellStep);

            if (gx < 0 || gx >= GridCols || gy < 0 || gy >= GridRows)
            {
                ClearSelection();
                return;
            }

            ItemData cell = playerInventory != null
                ? playerInventory.Grid.GetCell(gx, gy)
                : null;

            if (cell != null)
            {
                FindAnchor(cell, gx, gy, out int ax, out int ay);

                // Звук только при смене выбора
                if (!ReferenceEquals(cell, _selectedItem) || ax != _selectedX || ay != _selectedY)
                    PlayUISound(selectSound);

                _selectedX = ax;
                _selectedY = ay;
                _selectedItem = cell;
                PositionHighlight(_selectRect, ax, ay, cell.width, cell.height);
                _selectRect.gameObject.SetActive(true);
            }
            else
            {
                ClearSelection();
            }

            RefreshDetails();
        }

        /// <summary>
        /// Вызывается кнопкой USE. Потребляет одну единицу предмета.
        /// </summary>
        internal void UseSelectedItem()
        {
            if (_selectedItem == null || playerInventory == null) return;
            if (!_selectedItem.isConsumable) return;

            // Звук использования (приоритет: предмет → UI default)
            AudioClip clip = _selectedItem.useSound != null
                ? _selectedItem.useSound
                : useSound;
            PlayUISound(clip);

            bool consumed = playerInventory.ConsumeOneItem(_selectedX, _selectedY);
            if (!consumed) return;

            // Проверяем остался ли предмет
            ItemData remaining = playerInventory.Grid.GetCell(_selectedX, _selectedY);
            if (!ReferenceEquals(remaining, _selectedItem))
                ClearSelection();
            else
                RefreshDetails();
        }

        /// <summary>
        /// Вызывается кнопкой SORT.
        /// </summary>
        internal void SortInventoryAction()
        {
            if (playerInventory == null) return;
            PlayUISound(sortSound);
            playerInventory.SortInventory();
            ClearSelection();
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
                _hoverRect.gameObject.SetActive(false);
        }

        private void ClearSelection()
        {
            _selectedX = -1;
            _selectedY = -1;
            _selectedItem = null;
            if (_selectRect != null)
                _selectRect.gameObject.SetActive(false);
            RefreshDetails();
        }

        private void PositionHighlight(RectTransform rect, int gx, int gy, int w, int h)
        {
            rect.anchoredPosition = new Vector2(gx * CellStep, -gy * CellStep);
            rect.sizeDelta = new Vector2(w * CellStep - CellGap, h * CellStep - CellGap);
        }

        private void FindAnchor(ItemData item, int gx, int gy, out int ax, out int ay)
        {
            ax = gx;
            ay = gy;
            InventoryGrid grid = playerInventory.Grid;

            // Walk left
            while (ax > 0 && ReferenceEquals(grid.GetCell(ax - 1, gy), item))
                ax--;

            // Walk up
            while (ay > 0 && ReferenceEquals(grid.GetCell(ax, ay - 1), item))
                ay--;
        }

        // ══════════════════════════════════════════════════════════
        //  UI FACTORY HELPERS
        // ══════════════════════════════════════════════════════════
        private void PlayUISound(AudioClip clip)
        {
            if (clip == null) return;

            var audio = Hecton8.Audio.SpatialAudioManager.Instance;
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

        private static Color A(Color c, float a) { c.a = a; return c; }

        private void ClearChildren(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
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
            _tab?.UseSelectedItem();
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
        IPointerMoveHandler, IPointerClickHandler, IPointerExitHandler
    {
        private PDAInventoryTab _tab;

        public void Init(PDAInventoryTab tab) => _tab = tab;

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_tab == null) return;
            RectTransform rt = transform as RectTransform;
            if (rt == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out Vector2 local);
            _tab.HandlePointerMove(local);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_tab == null) return;
            RectTransform rt = transform as RectTransform;
            if (rt == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out Vector2 local);
            _tab.HandlePointerClick(local);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _tab?.HandlePointerExit();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPER: PDA Tab Button
    // ══════════════════════════════════════════════════════════════

    [DisallowMultipleComponent]
    internal sealed class PDATabButton : MonoBehaviour, IPointerClickHandler
    {
        private int _tabIndex;
        private PlayerPDA _pda;
        private Image _bg;
        private TextMeshProUGUI _label;
        private Color _bgActive, _bgInactive, _txtActive, _txtInactive;
        private bool _isActive;

        public void Init(int index, PlayerPDA pda, Image bg, TextMeshProUGUI label,
            Color bgActive, Color bgInactive, Color txtActive, Color txtInactive)
        {
            _tabIndex = index;
            _pda = pda;
            _bg = bg;
            _label = label;
            _bgActive = bgActive;
            _bgInactive = bgInactive;
            _txtActive = txtActive;
            _txtInactive = txtInactive;
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (_bg != null) _bg.color = active ? _bgActive : _bgInactive;
            if (_label != null) _label.color = active ? _txtActive : _txtInactive;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_pda != null)
                _pda.SetActiveTab(_tabIndex);
        }
    }
}