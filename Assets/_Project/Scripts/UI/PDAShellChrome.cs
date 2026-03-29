using Hecton8.Gameplay;
using Hecton8.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Shell Chrome")]
    public sealed class PDAShellChrome : MonoBehaviour
    {
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.56f, 0.74f, 0.71f, 0.72f);
        private static readonly Color Stable = new Color(0.08f, 0.2f, 0.22f, 0.74f);
        private static readonly Color Warning = new Color(0.3f, 0.2f, 0.06f, 0.82f);
        private static readonly Color Critical = new Color(0.34f, 0.12f, 0.12f, 0.84f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);

        [Header("References")]
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField, Range(0.05f, 1f)] private float refreshInterval = 0.2f;

        private bool _built;
        private float _nextRefreshTime;
        private RectTransform _chromeRoot;
        private Image _headerBg;
        private Image _footerBg;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _tabText;
        private TextMeshProUGUI _leftFooterText;
        private TextMeshProUGUI _rightFooterText;

        private void Awake()
        {
            AutoResolve();
        }

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            RefreshChrome(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!PlayerPDA.IsOpen)
                return;

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            RefreshChrome(false);
        }

        private void AutoResolve()
        {
            if (playerPDA == null)
                playerPDA = GetComponent<PlayerPDA>() ?? GetComponentInParent<PlayerPDA>() ?? FindFirstObjectByType<PlayerPDA>();
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (toolManager == null)
                toolManager = FindFirstObjectByType<PlayerToolManager>();
            if (survivalSystem == null)
                survivalSystem = FindFirstObjectByType<HectonSurvivalSystem>();
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        private void Subscribe()
        {
            PDAEvents.OnOpened += HandlePdaOpened;
            PDAEvents.OnClosed += HandlePdaClosed;
            PDAEvents.OnTabChanged += HandleTabChanged;

            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged += HandleSlotChanged;
                toolManager.ToolAssignmentsChanged += HandleAssignmentsChanged;
            }
        }

        private void Unsubscribe()
        {
            PDAEvents.OnOpened -= HandlePdaOpened;
            PDAEvents.OnClosed -= HandlePdaClosed;
            PDAEvents.OnTabChanged -= HandleTabChanged;

            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged -= HandleSlotChanged;
                toolManager.ToolAssignmentsChanged -= HandleAssignmentsChanged;
            }
        }

        private void HandlePdaOpened(int _) => RefreshChrome(true);
        private void HandlePdaClosed(float _) => RefreshChrome(true);
        private void HandleTabChanged(int _, int __) => RefreshChrome(true);
        private void HandleInventoryChanged() => RefreshChrome(true);
        private void HandleSlotChanged(int _) => RefreshChrome(true);
        private void HandleAssignmentsChanged() => RefreshChrome(true);

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

            ClearChildren(_chromeRoot);

            RectTransform header = CreateRect(_chromeRoot, "Header");
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -8f), new Vector2(-12f, 42f));
            _headerBg = EnsureImage(header.gameObject);
            _headerBg.color = Stable;
            _headerBg.raycastTarget = false;

            RectTransform footer = CreateRect(_chromeRoot, "Footer");
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 8f), new Vector2(-12f, 38f));
            _footerBg = EnsureImage(footer.gameObject);
            _footerBg.color = Stable;
            _footerBg.raycastTarget = false;

            CreateRule(_chromeRoot, new Vector2(0.04f, 1f), new Vector2(0.96f, 1f), -54f);
            CreateRule(_chromeRoot, new Vector2(0.04f, 0f), new Vector2(0.96f, 0f), 54f);
            CreateCornerBracket(_chromeRoot, true, true);
            CreateCornerBracket(_chromeRoot, false, true);
            CreateCornerBracket(_chromeRoot, true, false);
            CreateCornerBracket(_chromeRoot, false, false);

            _titleText = CreateText(header, "Title", labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(_titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(14f, 0f), new Vector2(-8f, 0f));
            _titleText.color = Primary;

            _tabText = CreateText(header, "Tab", numericFont, 11f, FontStyles.Bold, TextAlignmentOptions.Right);
            Anchor(_tabText.rectTransform, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-14f, 0f));
            _tabText.color = Dim;

            _leftFooterText = CreateText(footer, "FooterLeft", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(_leftFooterText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(14f, 0f), new Vector2(-8f, 0f));
            _leftFooterText.color = Dim;

            _rightFooterText = CreateText(footer, "FooterRight", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(_rightFooterText.rectTransform, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-14f, 0f));
            _rightFooterText.color = DimLow;

            _built = true;
        }

        private void RefreshChrome(bool immediate)
        {
            if (!_built)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshInterval;

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

            if (_titleText != null)
                _titleText.SetText("HECTON-8 PERSONAL DATA ASSISTANT");

            if (_tabText != null)
                _tabText.SetText($"ACTIVE TAB // {tabName}");

            if (_leftFooterText != null)
                _leftFooterText.SetText(
                    "CARGO {0}/{1}  |  MASS {2:0.0} kg  |  READY TOOLS {3}/{4}",
                    cargoCells, cargoTotal, weight, readyTools, Mathf.Max(assignedTools, 1));

            if (_rightFooterText != null)
                _rightFooterText.text =
                    $"O2 {oxygen * 100f:0}%  |  PWR {energy * 100f:0}%  |  PDA {(PlayerPDA.IsOpen ? "ONLINE" : "STANDBY")}";

            Color severity = GetShellSeverityColor(energy, oxygen, weight, readyTools, assignedTools);
            if (_headerBg != null) _headerBg.color = severity;
            if (_footerBg != null) _footerBg.color = severity;
            if (_tabText != null) _tabText.color = energy < 0.25f || oxygen < 0.3f ? new Color(1f, 0.88f, 0.72f, 0.96f) : Dim;
            if (_rightFooterText != null) _rightFooterText.color = energy < 0.25f || oxygen < 0.3f ? new Color(1f, 0.88f, 0.72f, 0.96f) : DimLow;

            _chromeRoot.gameObject.SetActive(PlayerPDA.IsOpen || immediate);
        }

        private string GetActiveTabLabel()
        {
            if (playerPDA == null)
                return "UNKNOWN";

            switch (playerPDA.ActiveTab)
            {
                case 0: return "INVENTORY";
                case 1: return "LOADOUT";
                case 2: return "CONSTRUCTION";
                case 3: return "BARTER";
                case 4: return "DATA LOG";
                default: return "UNKNOWN";
            }
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
            int used = 0;
            for (int y = 0; y < grid.Rows; y++)
            {
                for (int x = 0; x < grid.Columns; x++)
                {
                    if (grid.GetCell(x, y) != null)
                        used++;
                }
            }

            return used;
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

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
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
            return text;
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
            RectTransform root = CreateRect(parent, $"Corner_{(left ? "L" : "R")}{(top ? "T" : "B")}");
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
