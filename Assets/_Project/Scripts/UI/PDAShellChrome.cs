using Hecton8.Gameplay;
using Hecton8.Bootstrap;
using Hecton8.Inventory;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Shell Chrome")]
    public sealed class PDAShellChrome : MonoBehaviour, ITickable
    {
        private const string TitleTextValue = "HECTON-8 PERSONAL DATA ASSISTANT";
        private const string ActiveTabInventory = "ACTIVE TAB // INVENTORY";
        private const string ActiveTabLoadout = "ACTIVE TAB // LOADOUT";
        private const string ActiveTabConstruction = "ACTIVE TAB // CONSTRUCTION";
        private const string ActiveTabBarter = "ACTIVE TAB // BARTER";
        private const string ActiveTabDataLog = "ACTIVE TAB // DATA LOG";
        private const string ActiveTabSpectrum = "ACTIVE TAB // SPECTRUM";
        private const string ActiveTabUnknown = "ACTIVE TAB // UNKNOWN";
        private const string RightFooterOnlineFormat = "O2 {0:0}%  |  PWR {1:0}%  |  PDA ONLINE";
        private const string RightFooterStandbyFormat = "O2 {0:0}%  |  PWR {1:0}%  |  PDA STANDBY";

        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.56f, 0.74f, 0.71f, 0.72f);
        private static readonly Color Stable = new Color(0.08f, 0.2f, 0.22f, 0.74f);
        private static readonly Color Warning = new Color(0.3f, 0.2f, 0.06f, 0.82f);
        private static readonly Color Critical = new Color(0.34f, 0.12f, 0.12f, 0.84f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color AlertText = new Color(1f, 0.88f, 0.72f, 0.96f);

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
        private CanvasGroup _chromeCanvasGroup;
        private Image _headerBg;
        private Image _footerBg;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _tabText;
        private TextMeshProUGUI _leftFooterText;
        private TextMeshProUGUI _rightFooterText;
        private bool _tickRegistered;
        private int _lastActiveTab = int.MinValue;
        private int _lastCargoCells = -1;
        private int _lastCargoTotal = -1;
        private int _lastWeightDeci = int.MinValue;
        private int _lastReadyTools = -1;
        private int _lastAssignedTools = -1;
        private int _lastOxygenPercent = int.MinValue;
        private int _lastEnergyPercent = int.MinValue;
        private bool _lastPdaOpen;

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
            EvaluateTickRegistration();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnregisterTick();
        }

        private void AutoResolve()
        {
            if ((!playerPDA || !playerInventory || !toolManager || !survivalSystem) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerInventory == null)
                    playerInventory = playerTransform.GetComponent<PlayerInventory>();

                if (toolManager == null)
                    toolManager = playerTransform.GetComponentInChildren<PlayerToolManager>(true);

                if (survivalSystem == null)
                    survivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();

                if (playerPDA == null)
                    playerPDA = playerTransform.GetComponentInChildren<PlayerPDA>(true);
            }

            if (playerPDA == null)
                playerPDA = GetComponent<PlayerPDA>() ?? GetComponentInParent<PlayerPDA>();
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

        private void HandlePdaOpened(int _)
        {
            RefreshChrome(true);
            EvaluateTickRegistration();
        }
        private void HandlePdaClosed(float _)
        {
            RefreshChrome(true);
            UnregisterTick();
        }

        private void HandleTabChanged(int _, int __)
        {
            RefreshChrome(true);
            EvaluateTickRegistration();
        }

        private void HandleInventoryChanged()
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome(true);
        }

        private void HandleSlotChanged(int _)
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome(true);
        }

        private void HandleAssignmentsChanged()
        {
            if (PlayerPDA.IsOpen)
                RefreshChrome(true);
        }

        public void Tick(float deltaTime)
        {
            if (!PlayerPDA.IsOpen)
            {
                UnregisterTick();
                return;
            }

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            RefreshChrome(false);
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
            _chromeCanvasGroup = _chromeRoot.GetComponent<CanvasGroup>();
            if (_chromeCanvasGroup == null)
                _chromeCanvasGroup = _chromeRoot.gameObject.AddComponent<CanvasGroup>();
            _chromeCanvasGroup.interactable = false;
            _chromeCanvasGroup.blocksRaycasts = false;
            _chromeCanvasGroup.alpha = 0f;

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
            _titleText.SetText(TitleTextValue);

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
            int activeTabIndex = playerPDA != null ? playerPDA.ActiveTab : -1;
            int weightDeci = Mathf.RoundToInt(weight * 10f);
            int oxygenPercent = Mathf.RoundToInt(oxygen * 100f);
            int energyPercent = Mathf.RoundToInt(energy * 100f);
            bool pdaOpen = PlayerPDA.IsOpen;

            if (_tabText != null && _lastActiveTab != activeTabIndex)
            {
                _tabText.SetText(tabName);
                _lastActiveTab = activeTabIndex;
            }

            if (_leftFooterText != null &&
                (_lastCargoCells != cargoCells ||
                 _lastCargoTotal != cargoTotal ||
                 _lastWeightDeci != weightDeci ||
                 _lastReadyTools != readyTools ||
                 _lastAssignedTools != assignedTools))
            {
                _leftFooterText.SetText(
                    "CARGO {0}/{1}  |  MASS {2:0.0} kg  |  READY TOOLS {3}/{4}",
                    cargoCells, cargoTotal, weight, readyTools, Mathf.Max(assignedTools, 1));
                _lastCargoCells = cargoCells;
                _lastCargoTotal = cargoTotal;
                _lastWeightDeci = weightDeci;
                _lastReadyTools = readyTools;
                _lastAssignedTools = assignedTools;
            }

            if (_rightFooterText != null &&
                (_lastOxygenPercent != oxygenPercent ||
                 _lastEnergyPercent != energyPercent ||
                 _lastPdaOpen != pdaOpen))
            {
                _rightFooterText.SetText(
                    pdaOpen ? RightFooterOnlineFormat : RightFooterStandbyFormat,
                    oxygenPercent, energyPercent);
                _lastOxygenPercent = oxygenPercent;
                _lastEnergyPercent = energyPercent;
                _lastPdaOpen = pdaOpen;
            }

            Color severity = GetShellSeverityColor(energy, oxygen, weight, readyTools, assignedTools);
            if (_headerBg != null) _headerBg.color = severity;
            if (_footerBg != null) _footerBg.color = severity;
            if (_tabText != null) _tabText.color = energy < 0.25f || oxygen < 0.3f ? AlertText : Dim;
            if (_rightFooterText != null) _rightFooterText.color = energy < 0.25f || oxygen < 0.3f ? AlertText : DimLow;
            if (_chromeCanvasGroup != null)
                _chromeCanvasGroup.alpha = pdaOpen || immediate ? 1f : 0f;
        }

        private void EvaluateTickRegistration()
        {
            if (!isActiveAndEnabled)
            {
                UnregisterTick();
                return;
            }

            if (PlayerPDA.IsOpen)
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
            if (_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _tickRegistered = true;
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _tickRegistered = false;
        }

        private string GetActiveTabLabel()
        {
            if (playerPDA == null)
                return ActiveTabUnknown;

            switch (playerPDA.ActiveTab)
            {
                case 0: return ActiveTabInventory;
                case 1: return ActiveTabLoadout;
                case 2: return ActiveTabConstruction;
                case 3: return ActiveTabBarter;
                case 4: return ActiveTabDataLog;
                case 5: return ActiveTabSpectrum;
                default: return ActiveTabUnknown;
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
