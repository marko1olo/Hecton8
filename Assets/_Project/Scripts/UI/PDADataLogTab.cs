// ============================================================================
// HECTON-8 — PDADataLogTab.cs
// PDA tab 2: live suit, cargo, and loadout digest for the current expedition.
// Builds its own UI at runtime and refreshes from real gameplay systems.
// ============================================================================

using System.Text;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Data Log Tab")]
    public sealed class PDADataLogTab : MonoBehaviour
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.82f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.95f);
        private static readonly Color Dim = new Color(0.76f, 0.96f, 0.93f, 0.82f);
        private static readonly Color DimLow = new Color(0.55f, 0.74f, 0.71f, 0.72f);
        private static readonly Color BoxBg = new Color(0.05f, 0.12f, 0.14f, 0.7f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private int dataLogTabIndex = 2;
        [SerializeField] private int manifestVisibleRows = 10;

        private bool _built;
        private TextMeshProUGUI _summaryText;
        private TextMeshProUGUI _cargoText;
        private TextMeshProUGUI _loadoutText;
        private TextMeshProUGUI _hintText;
        private PlayerInventory.ItemPlacement[] _placementBuffer;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == dataLogTabIndex;

        private void Awake()
        {
            AutoResolve();
            _placementBuffer = new PlayerInventory.ItemPlacement[64];
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
            if (survivalSystem == null)
                survivalSystem = FindFirstObjectByType<HectonSurvivalSystem>();
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        private void Subscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged += HandleToolSlotChanged;
                toolManager.ToolAssignmentsChanged += HandleToolAssignmentsChanged;
            }

            PDAEvents.OnOpened += HandlePdaOpened;
            PDAEvents.OnTabChanged += HandlePdaTabChanged;
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged -= HandleToolSlotChanged;
                toolManager.ToolAssignmentsChanged -= HandleToolAssignmentsChanged;
            }

            PDAEvents.OnOpened -= HandlePdaOpened;
            PDAEvents.OnTabChanged -= HandlePdaTabChanged;
        }

        private void HandleInventoryChanged() => RefreshCargo();
        private void HandleToolSlotChanged(int _) => RefreshLoadout();
        private void HandleToolAssignmentsChanged() => RefreshLoadout();

        private void HandlePdaOpened(int tab)
        {
            if (tab != dataLogTabIndex) return;
            RefreshAll();
        }

        private void HandlePdaTabChanged(int oldTab, int newTab)
        {
            if (newTab != dataLogTabIndex) return;
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
            title.SetText("EXPEDITION DATA LOG");

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            sub.SetText("live suit telemetry, cargo manifest, and loadout digest");

            CreateRule(self, -52f);

            RectTransform left = CreatePanel(self, "SuitPanel", new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                new Vector2(18f, 18f), new Vector2(-9f, -72f));
            RectTransform right = CreatePanel(self, "CargoPanel", new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                new Vector2(9f, 18f), new Vector2(-18f, -72f));

            TextMeshProUGUI leftHdr = CreateSectionHeader(left, "SUIT + ENVIRONMENT");
            _summaryText = CreateBody(left, "SummaryText", numericFont);
            Anchor(_summaryText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(14f, 16f), new Vector2(-14f, -42f));

            TextMeshProUGUI rightHdr = CreateSectionHeader(right, "CARGO + LOADOUT");
            _cargoText = CreateBody(right, "CargoText", numericFont);
            Anchor(_cargoText.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f),
                new Vector2(14f, 12f), new Vector2(-14f, -42f));

            CreateInnerRule(right, 0.34f);

            _loadoutText = CreateBody(right, "LoadoutText", numericFont);
            Anchor(_loadoutText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.32f),
                new Vector2(14f, 14f), new Vector2(-14f, -8f));

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 4f), new Vector2(-24f, 14f));
            _hintText.color = DimLow;
            _hintText.SetText("Inventory and tool state update live while the PDA is open.");

            _ = leftHdr;
            _ = rightHdr;
            _built = true;
        }

        private void RefreshAll()
        {
            EnsureBuilt();
            RefreshSummary();
            RefreshCargo();
            RefreshLoadout();
        }

        private void RefreshSummary()
        {
            if (_summaryText == null) return;

            _sb.Clear();
            _sb.AppendLine("SUIT STATUS");
            _sb.Append("OXYGEN   ").Append(FormatPercent(survivalSystem != null ? survivalSystem.OxygenNormalized : 0f)).AppendLine();
            _sb.Append("ENERGY   ").Append(FormatPercent(survivalSystem != null ? survivalSystem.EnergyNormalized : 0f)).AppendLine();
            _sb.Append("INTEGR.  ").Append(FormatPercent(survivalSystem != null ? survivalSystem.IntegrityNormalized : 0f)).AppendLine();
            _sb.AppendLine();
            _sb.AppendLine("EXPEDITION ENVIRONMENT");
            _sb.Append("DEPTH    ").AppendFormat("{0:0} m", -(survivalSystem != null ? survivalSystem.Depth : 0f)).AppendLine();
            _sb.Append("PRESS.   ").AppendFormat("{0:0.0} atm", survivalSystem != null ? survivalSystem.Pressure : 0f).AppendLine();
            _sb.Append("WEIGHT   ").AppendFormat("{0:0.0} kg", playerInventory != null ? playerInventory.TotalWeight : 0f).AppendLine();
            _sb.Append("PDA TAB  ").Append(playerPDA != null ? playerPDA.ActiveTab.ToString() : "--").AppendLine();

            _summaryText.SetText(_sb);
        }

        private void RefreshCargo()
        {
            if (_cargoText == null) return;

            _sb.Clear();

            int placementCount = 0;
            int occupiedCells = 0;
            int totalUnits = 0;
            int tools = 0;
            int consumables = 0;
            int materials = 0;

            if (playerInventory != null && playerInventory.Grid != null)
            {
                placementCount = playerInventory.GetPlacements(_placementBuffer);

                InventoryGrid grid = playerInventory.Grid;
                for (int y = 0; y < grid.Rows; y++)
                {
                    for (int x = 0; x < grid.Columns; x++)
                    {
                        if (grid.GetCell(x, y) != null)
                            occupiedCells++;
                    }
                }

                for (int i = 0; i < placementCount; i++)
                {
                    PlayerInventory.ItemPlacement p = _placementBuffer[i];
                    totalUnits += Mathf.Max(1, p.stackCount);

                    switch (p.item.category)
                    {
                        case ItemCategory.Tool:
                            tools += Mathf.Max(1, p.stackCount);
                            break;
                        case ItemCategory.Consumable:
                            consumables += Mathf.Max(1, p.stackCount);
                            break;
                        case ItemCategory.Material:
                        case ItemCategory.Component:
                            materials += Mathf.Max(1, p.stackCount);
                            break;
                    }
                }

                _sb.AppendLine("CARGO SUMMARY");
                _sb.Append("ANCHORS  ").Append(placementCount).AppendLine();
                _sb.Append("UNITS    ").Append(totalUnits).AppendLine();
                _sb.Append("CELLS    ").Append(occupiedCells).Append(" / ")
                    .Append(grid.Columns * grid.Rows).AppendLine();
                _sb.Append("TOOLS    ").Append(tools).AppendLine();
                _sb.Append("CONS.    ").Append(consumables).AppendLine();
                _sb.Append("MATL.    ").Append(materials).AppendLine();
                _sb.AppendLine();
                _sb.AppendLine("MANIFEST");

                int rowsToShow = Mathf.Min(manifestVisibleRows, placementCount);
                for (int i = 0; i < rowsToShow; i++)
                {
                    PlayerInventory.ItemPlacement p = _placementBuffer[i];
                    _sb.Append("• ").Append(p.item.itemName);
                    if (p.stackCount > 1)
                        _sb.Append(" ×").Append(p.stackCount);
                    _sb.AppendLine();
                }

                if (placementCount == 0)
                    _sb.Append("• cargo hold empty");
            }
            else
            {
                _sb.Append("CARGO DATA UNAVAILABLE");
            }

            _cargoText.SetText(_sb);
        }

        private void RefreshLoadout()
        {
            if (_loadoutText == null) return;

            _sb.Clear();
            _sb.AppendLine("ACTIVE LOADOUT");

            if (toolManager == null)
            {
                _sb.Append("tool manager unavailable");
                _loadoutText.SetText(_sb);
                return;
            }

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                bool available = toolManager.IsToolAvailableInSlot(i);
                bool active = toolManager.CurrentSlotIndex == i;

                _sb.Append(active ? "► " : "  ");
                _sb.Append(i + 1).Append(": ");
                if (prefab == null)
                {
                    _sb.Append("EMPTY");
                }
                else
                {
                    _sb.Append(prefab.name.Replace("Tool_", string.Empty).Replace("_Held", string.Empty));
                    _sb.Append(available ? "  [READY]" : "  [MISSING]");
                }
                _sb.AppendLine();
            }

            _loadoutText.SetText(_sb);
        }

        private static string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f),3}%";
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
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
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

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size,
            FontStyles style, TextAlignmentOptions alignment)
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
            text.color = Dim;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image img = EnsureImage(rect.gameObject);
            img.color = BoxBg;
            img.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI CreateSectionHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI hdr = CreateText(parent, "Header", TMP_Settings.defaultFontAsset, 10.5f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(hdr.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -10f), new Vector2(-12f, 16f));
            hdr.color = Primary;
            hdr.SetText(text);
            return hdr;
        }

        private static TextMeshProUGUI CreateBody(RectTransform parent, string name, TMP_FontAsset font)
        {
            TextMeshProUGUI body = CreateText(parent, name, font, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.enableWordWrapping = false;
            body.overflowMode = TextOverflowModes.Overflow;
            body.color = Dim;
            return body;
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

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
