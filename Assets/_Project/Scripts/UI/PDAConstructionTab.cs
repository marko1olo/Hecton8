// ============================================================================
// HECTON-8 — PDAConstructionTab.cs
// Dedicated PDA construction tab: module catalog, active buildable, cost/readiness,
// and direct selection flow for the real PlayerBuilder backend.
// ============================================================================

using System.Text;
using Hecton8.Building;
using Hecton8.Bootstrap;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Construction Tab")]
    public sealed class PDAConstructionTab : MonoBehaviour, ITickable
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
        private float _nextRefreshAt;
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
        private RectTransform[] _cardRoots;
        private Image[] _cardBgs;
        private TextMeshProUGUI[] _cardTitles;
        private TextMeshProUGUI[] _cardBodies;
        private Image[] _cardButtonBgs;
        private TextMeshProUGUI[] _cardButtonLabels;
        private readonly StringBuilder _sb = new StringBuilder(512);
        private bool _tickRegistered;

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == constructionTabIndex;

        // ══════════════════════════════════════════════════════════
        //  CACHED STRING OPERATIONS — ZERO GC
        // ══════════════════════════════════════════════════════════

        private readonly string[] _cachedUpperStrings = new string[16];

        private string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Простой hash для кэширования (не криптографический)
            int hash = input.GetHashCode() & 0xF; // Маска для индекса 0-15

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            // Создаем новую строку и кэшируем
            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }

        private void Awake()
        {
            AutoResolveTabIndex();
            AutoResolve();
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
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            RefreshAll(true);
            EvaluateTickRegistration();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnregisterTick();
        }

        private void AutoResolve()
        {
            if ((!playerBuilder || !playerInventory || !toolManager || !playerPDA) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerBuilder == null)
                    playerBuilder = playerTransform.GetComponentInChildren<PlayerBuilder>(true);

                if (playerInventory == null)
                    playerInventory = playerTransform.GetComponent<PlayerInventory>();

                if (toolManager == null)
                    toolManager = playerTransform.GetComponentInChildren<Hecton8.Gameplay.PlayerToolManager>(true);

                if (playerPDA == null)
                    playerPDA = playerTransform.GetComponentInChildren<PlayerPDA>(true);
            }

            if (constructionManager == null)
                constructionManager = ConstructionManager.Instance;

            if (playerPDA == null)
                playerPDA = GetComponentInParent<PlayerPDA>();
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        private void AutoResolveTabIndex()
        {
            if (gameObject.name.Contains("Construction", System.StringComparison.OrdinalIgnoreCase))
                constructionTabIndex = 2;
        }

        private void Subscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;

            PDAEvents.OnOpened += HandlePdaOpened;
            PDAEvents.OnClosed += HandlePdaClosed;
            PDAEvents.OnTabChanged += HandlePdaTabChanged;
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;

            PDAEvents.OnOpened -= HandlePdaOpened;
            PDAEvents.OnClosed -= HandlePdaClosed;
            PDAEvents.OnTabChanged -= HandlePdaTabChanged;
        }

        private void HandleInventoryChanged()
        {
            if (IsTabActive)
                RefreshAll(true);
        }

        private void HandlePdaOpened(int tab)
        {
            if (tab == constructionTabIndex)
            {
                RefreshAll(true);
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
                RefreshAll(true);
                EvaluateTickRegistration();
            }
            else
            {
                UnregisterTick();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!IsTabActive)
            {
                UnregisterTick();
                return;
            }

            if (Time.unscaledTime < _nextRefreshAt)
                return;

            RefreshAll(false);
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
            title.SetText("CONSTRUCTION MATRIX");

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            sub.SetText("module catalog, build cost, placement readiness, and field deployment state");

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

            _builderActionBg = EnsureImage(_builderActionRoot.gameObject);
            _builderActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            _builderActionBg.raycastTarget = true;

            _builderActionLabel = CreateText(_builderActionRoot, "BuilderActionLabel", numericFont, 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_builderActionLabel.rectTransform);
            _builderActionLabel.color = Dim;
            _builderActionLabel.SetText("ARM BUILDER");

            PDAConstructionBuilderActionButton actionButton = _builderActionRoot.gameObject.AddComponent<PDAConstructionBuilderActionButton>();
            actionButton.Init(
                this,
                _builderActionBg,
                new Color(0.08f, 0.16f, 0.18f, 0.58f),
                new Color(0.12f, 0.24f, 0.28f, 0.82f));

            _fieldActionRoot = CreateRect(left, "FieldAction");
            _fieldActionRoot.anchorMin = new Vector2(1f, 0f);
            _fieldActionRoot.anchorMax = new Vector2(1f, 0f);
            _fieldActionRoot.pivot = new Vector2(1f, 0f);
            _fieldActionRoot.anchoredPosition = new Vector2(-170f, 14f);
            _fieldActionRoot.sizeDelta = new Vector2(146f, 28f);

            _fieldActionBg = EnsureImage(_fieldActionRoot.gameObject);
            _fieldActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            _fieldActionBg.raycastTarget = true;

            _fieldActionLabel = CreateText(_fieldActionRoot, "FieldActionLabel", numericFont, 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(_fieldActionLabel.rectTransform);
            _fieldActionLabel.color = Dim;
            _fieldActionLabel.SetText("FIELD PREVIEW");

            PDAConstructionFieldActionButton fieldButton = _fieldActionRoot.gameObject.AddComponent<PDAConstructionFieldActionButton>();
            fieldButton.Init(
                this,
                _fieldActionBg,
                new Color(0.08f, 0.16f, 0.18f, 0.58f),
                new Color(0.12f, 0.24f, 0.28f, 0.82f));

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
            _deployActionLabel.SetText("DEPLOY NOW");

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
            _cardBgs = new Image[maxVisibleCards];
            _cardTitles = new TextMeshProUGUI[maxVisibleCards];
            _cardBodies = new TextMeshProUGUI[maxVisibleCards];
            _cardButtonBgs = new Image[maxVisibleCards];
            _cardButtonLabels = new TextMeshProUGUI[maxVisibleCards];

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
                actionLabel.SetText("SELECT");

                PDAConstructionSelectButton button = action.gameObject.AddComponent<PDAConstructionSelectButton>();
                button.Init(this, i, actionBg, actionLabel,
                    new Color(0.08f, 0.16f, 0.18f, 0.58f),
                    new Color(0.12f, 0.24f, 0.28f, 0.82f));

                _cardRoots[i] = card;
                _cardBgs[i] = cardBg;
                _cardTitles[i] = cardTitle;
                _cardBodies[i] = cardBody;
                _cardButtonBgs[i] = actionBg;
                _cardButtonLabels[i] = actionLabel;
            }

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 4f), new Vector2(-24f, 14f));
            _hintText.color = DimLow;
            _hintText.SetText("Select a module to arm the Builder Tool. TAB / Q-E still cycle modules in the field.");

            _built = true;
        }

        private void RefreshAll(bool immediate)
        {
            if (!_built)
                return;

            _nextRefreshAt = Time.unscaledTime + refreshInterval;
            RefreshSummary();
            RefreshCatalog();
            if (immediate && gameObject.activeSelf)
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
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

        private void RefreshSummary()
        {
            if (_summaryText == null || _statusText == null)
                return;

            ModuleCatalog catalog = constructionManager != null ? constructionManager.Catalog : null;
            BuildableData active = playerBuilder != null ? playerBuilder.ActiveBuildable : null;
            int activeIndex = playerBuilder != null ? playerBuilder.ActiveBuildableIndex : -1;
            int builtCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            bool hasResources = playerBuilder != null && playerBuilder.HasResourcesForActiveBuildable;
            bool canPlace = playerBuilder != null && playerBuilder.CanPlaceActiveBuildable;
            bool snapped = playerBuilder != null && playerBuilder.IsSnapped;
            BuildableData next = playerBuilder != null ? playerBuilder.GetRelativeBuildable(1) : null;
            int generatorCount = CountModulesByPowerRole(catalog, 1);
            int consumerCount = CountModulesByPowerRole(catalog, -1);
            int passiveCount = CountModulesByPowerRole(catalog, 0);
            int structureCount = CountModulesByFamily(catalog, BuildableFamily.Structure);
            int habitatCount = CountModulesByFamily(catalog, BuildableFamily.Habitat);
            int utilityCount = CountModulesByFamily(catalog, BuildableFamily.Utility);
            int logisticsCount = CountModulesByFamily(catalog, BuildableFamily.Logistics);
            int fabricationCount = CountModulesByFamily(catalog, BuildableFamily.Fabrication);
            int defenseCount = CountModulesByFamily(catalog, BuildableFamily.Defense);
            int builderSlot = toolManager != null ? toolManager.FindAssignedSlotForToolType<Hecton8.Gameplay.BuilderTool>() : -1;
            bool builderActive = toolManager != null && toolManager.CurrentTool is Hecton8.Gameplay.BuilderTool;
            bool builderReady = builderSlot >= 0 && toolManager != null && toolManager.IsToolAvailableInSlot(builderSlot);
            bool hasPreview = playerBuilder != null && playerBuilder.HasPlacementPreview;

            _sb.Clear();
            _sb.AppendLine("CONSTRUCTION BACKBONE");
            _sb.Append("CATALOG     ").Append(catalog != null ? catalog.Count : 0).AppendLine(" MODULES");
            _sb.Append("FAMILIES    G").Append(generatorCount).Append(" / C").Append(consumerCount).Append(" / P").Append(passiveCount).AppendLine();
            _sb.Append("DOMAINS     STR ").Append(structureCount)
                .Append(" | HAB ").Append(habitatCount)
                .Append(" | UTL ").Append(utilityCount).AppendLine();
            _sb.Append("EXTENDED    LOG ").Append(logisticsCount)
                .Append(" | FAB ").Append(fabricationCount)
                .Append(" | DEF ").Append(defenseCount).AppendLine();
            _sb.Append("BUILT       ").Append(builtCount).AppendLine(" REGISTERED");
            _sb.Append("BUILDER     ").Append(DescribeBuilderState(builderSlot, builderReady, builderActive)).AppendLine();
            _sb.Append("ACTIVE      ").Append(active != null ? CachedToUpperInvariant(active.moduleName) : "NONE").AppendLine();
            _sb.Append("FAMILY      ").Append(active != null ? active.FamilyLabel : "N/A").AppendLine();
            _sb.Append("ROLE        ").Append(active != null ? DescribePowerRole(active) : "N/A").AppendLine();
            _sb.Append("INDEX       ").Append(activeIndex >= 0 ? $"{activeIndex + 1}/{Mathf.Max(1, catalog != null ? catalog.Count : 0)}" : "N/A").AppendLine();
            _sb.Append("MODE        ").Append(snapped ? "SNAPPED" : "FREE PLACEMENT").AppendLine();
            _summaryText.SetText(_sb.ToString());

            _sb.Clear();
            if (active == null)
            {
                _statusText.color = Warn;
                _sb.Append("NO ACTIVE BUILDABLE.\nSELECT A MODULE FROM THE CATALOG TO ARM THE BUILDER.");
            }
            else
            {
                _statusText.color = !hasResources ? Warn : (canPlace ? Ready : Blocked);
                _sb.Append("READINESS // ");
                _sb.Append(!hasResources ? "MISSING COST" : (canPlace ? (snapped ? "SNAPPED READY" : "READY") : "PLACEMENT BLOCKED"));
                _sb.AppendLine();
                AppendCostDigest(_sb, active);
            }

            _statusText.SetText(_sb);
            RefreshDirective(catalog, active, next, hasResources, canPlace, snapped, builtCount);
            RefreshBuilderAction(builderSlot, builderReady, builderActive);
            RefreshFieldAction(active, hasResources, canPlace, builderSlot, builderReady, builderActive, hasPreview);
        }

        private void RefreshCatalog()
        {
            ModuleCatalog catalog = constructionManager != null ? constructionManager.Catalog : null;
            int count = catalog != null ? Mathf.Min(catalog.Count, maxVisibleCards) : 0;
            BuildableData active = playerBuilder != null ? playerBuilder.ActiveBuildable : null;

            for (int i = 0; i < maxVisibleCards; i++)
            {
                bool visible = i < count && catalog != null;
                if (_cardRoots[i] != null)
                    _cardRoots[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;

                BuildableData data = catalog.GetAt(i);
                bool isActive = ReferenceEquals(active, data);
                bool hasCost = data != null && playerInventory != null && HasCost(data);
                bool canSelect = data != null;
                bool isReadyCandidate = !isActive && hasCost;

                _cardBgs[i].color = isActive ? BoxActive : BoxBg;
                _cardTitles[i].SetText(data != null ? CachedToUpperInvariant(data.moduleName) : "UNKNOWN MODULE");
                _cardTitles[i].color = isActive ? Primary : (hasCost ? Dim : Warn);
                _cardBodies[i].SetText(BuildCardBody(data, isActive, hasCost));

                _cardButtonLabels[i].SetText(isActive ? "ARMED" : (isReadyCandidate ? "ARM" : "QUEUE"));
                _cardButtonLabels[i].color = isActive ? Primary : (hasCost ? Dim : Warn);
                _cardButtonBgs[i].color = isActive
                    ? new Color(0.14f, 0.3f, 0.28f, 0.78f)
                    : (hasCost ? new Color(0.08f, 0.16f, 0.18f, 0.58f) : new Color(0.28f, 0.2f, 0.06f, 0.72f));

                PDAConstructionSelectButton button = _cardButtonBgs[i].GetComponent<PDAConstructionSelectButton>();
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
            BuildableData data = catalog != null ? catalog.GetAt(index) : null;
            if (data == null)
                return;

            playerBuilder.SetActiveBuildable(data);
            hudNotification?.ShowInfo($"CONSTRUCTION MATRIX — {CachedToUpperInvariant(data.moduleName)} ARMED");
            RefreshAll(true);
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
                RefreshAll(true);
                return;
            }

            if (builderSlot >= 0)
            {
                if (!toolManager.IsToolAvailableInSlot(builderSlot))
                {
                    hudNotification?.ShowWarning($"CONSTRUCTION MATRIX — BUILDER NOT IN CARGO [S{builderSlot + 1}]");
                    return;
                }

                toolManager.SwitchToSlot(builderSlot);
                hudNotification?.ShowInfo($"CONSTRUCTION MATRIX — BUILDER ACTIVATED [S{builderSlot + 1}]");
                RefreshAll(true);
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
            hudNotification?.ShowInfo($"CONSTRUCTION MATRIX — BUILDER ARMED TO S{targetSlot + 1}");
            RefreshAll(true);
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
                        hudNotification?.ShowInfo($"CONSTRUCTION MATRIX - {CachedToUpperInvariant(playerBuilder.ActiveBuildable.moduleName)} DEPLOYED");
                    else
                        hudNotification?.ShowWarning("CONSTRUCTION MATRIX - DEPLOY FAILED");

                    RefreshAll(true);
                    return;
                }

                playerPDA?.Close();
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
                hudNotification?.ShowInfo($"CONSTRUCTION MATRIX - BUILDER ARMED TO S{builderSlot + 1}");
            }

            if (!builderReady)
            {
                hudNotification?.ShowWarning($"CONSTRUCTION MATRIX - BUILDER NOT IN CARGO [S{builderSlot + 1}]");
                RefreshAll(true);
                return;
            }

            toolManager.SwitchToSlot(builderSlot);
            playerPDA?.Close();
            hudNotification?.ShowInfo($"CONSTRUCTION MATRIX - FIELD PREVIEW [S{builderSlot + 1}]");
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
                hudNotification?.ShowInfo($"CONSTRUCTION MATRIX - {CachedToUpperInvariant(playerBuilder.ActiveBuildable.moduleName)} DEPLOYED");
                RefreshAll(true);
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
                if (playerInventory.CountTotal(cost.item) < cost.amount)
                    return false;
            }

            return true;
        }

        private string BuildCardBody(BuildableData data, bool isActive, bool hasCost)
        {
            if (data == null)
                return "OFFLINE";

            _sb.Clear();
            _sb.Append(isActive ? "STATUS   ARMED" : "STATUS   STANDBY").AppendLine();
            _sb.Append("ROLE     ").Append(DescribePowerRole(data)).AppendLine();
            _sb.Append("FAMILY   ").Append(data.FamilyLabel).AppendLine();
            _sb.Append("PURPOSE  ").Append(DescribePurpose(data)).AppendLine();
            _sb.Append("POWER    ");
            if (data.powerRating > 0f) _sb.Append('+').Append(data.powerRating.ToString("0")).Append("W NET");
            else if (data.powerRating < 0f) _sb.Append(data.powerRating.ToString("0")).Append("W LOAD");
            else _sb.Append("PASSIVE");
            _sb.AppendLine();
            _sb.Append("FOOTPRINT ").Append(data.TotalResourceCount).Append(" UNITS").AppendLine();
            _sb.Append("COST     ");
            AppendShortCost(_sb, data);
            _sb.AppendLine();
            _sb.Append("STATE    ").Append(hasCost ? "READY" : "MISSING COST");
            _sb.AppendLine();
            _sb.Append("TACTIC   ").Append(isActive ? "FIELD ACTIVE" : (hasCost ? "ARM NEXT" : "GATHER MATS"));
            if (!string.IsNullOrWhiteSpace(data.description))
            {
                _sb.AppendLine();
                _sb.Append("NOTES    ").Append(CachedToUpperInvariant(TrimForCard(data.description, 56)));
            }
            return _sb.ToString();
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

            _sb.Clear();
            if (catalog == null || catalog.Count == 0)
            {
                _directiveText.color = Blocked;
                _sb.Append("DIRECTIVE // AUTHOR OR ASSIGN A MODULE CATALOG.");
            }
            else if (active == null)
            {
                _directiveText.color = Warn;
                _sb.Append("DIRECTIVE // ARM A STARTER MODULE. FOUNDATION FIRST.");
            }
            else if (!hasResources)
            {
                _directiveText.color = Warn;
                _sb.Append("DIRECTIVE // ").Append(playerBuilder != null ? CachedToUpperInvariant(playerBuilder.GetActiveBuildAdvice()) : "GATHER COST BEFORE DEPLOYMENT.");
                if (next != null && !ReferenceEquals(next, active))
                    _sb.Append(" NEXT VIABLE CANDIDATE: ").Append(CachedToUpperInvariant(next.moduleName))
                        .Append(" (").Append(next.FamilyShortCode).Append(" / ").Append(DescribePowerRole(next)).Append(").");
            }
            else if (!canPlace)
            {
                _directiveText.color = Blocked;
                _sb.Append("DIRECTIVE // ").Append(playerBuilder != null ? CachedToUpperInvariant(playerBuilder.GetActiveBuildAdvice()) : "REPOSITION UNTIL BUILD VOLUME CLEARS.");
                if (builtCount <= 0)
                    _sb.Append(" OPEN WITH FOUNDATION OR PYLON FOR FIRST ANCHOR.");
            }
            else if (snapped)
            {
                _directiveText.color = Ready;
                _sb.Append("DIRECTIVE // ").Append(playerBuilder != null ? CachedToUpperInvariant(playerBuilder.GetActiveBuildAdvice()) : "SOCKET LOCK ACQUIRED. DEPLOY FOR CLEAN CHAIN EXTENSION.");
            }
            else
            {
                _directiveText.color = Ready;
                _sb.Append("DIRECTIVE // ").Append(playerBuilder != null ? CachedToUpperInvariant(playerBuilder.GetActiveBuildAdvice()) : "FIELD-READY. DEPLOY ACTIVE MODULE.");
            }

            _directiveText.SetText(_sb.ToString());

            if (_hintText == null)
                return;

            _sb.Clear();
            _sb.Append("Select to arm. ");
            if (active != null)
                _sb.Append("Active: ").Append(CachedToUpperInvariant(active.moduleName)).Append(" [")
                    .Append(active.FamilyShortCode).Append(" / ").Append(DescribePowerRole(active)).Append("]. ");
            _sb.Append("TAB / Q-E cycle in the field. INTERACT recovers a placed module. ");
            if (next != null && !ReferenceEquals(next, active))
                _sb.Append("Next: ").Append(CachedToUpperInvariant(next.moduleName)).Append(" [").Append(next.FamilyShortCode).Append("].");
            _hintText.SetText(_sb.ToString());
        }

        private static int CountModulesByFamily(ModuleCatalog catalog, BuildableFamily family)
        {
            if (catalog == null)
                return 0;

            int count = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                BuildableData data = catalog.GetAt(i);
                if (data != null && data.family == family)
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

        private static string DescribeBuilderState(int builderSlot, bool builderReady, bool builderActive)
        {
            if (builderActive)
                return "ACTIVE";
            if (builderSlot < 0)
                return "UNASSIGNED";
            if (!builderReady)
                return $"ASSIGNED S{builderSlot + 1} / MISSING";
            return $"ASSIGNED S{builderSlot + 1} / READY";
        }

        private void RefreshBuilderAction(int builderSlot, bool builderReady, bool builderActive)
        {
            if (_builderActionRoot == null || _builderActionLabel == null || _builderActionBg == null)
                return;

            if (toolManager == null)
            {
                _builderActionRoot.gameObject.SetActive(false);
                return;
            }

            _builderActionRoot.gameObject.SetActive(true);

            if (builderActive)
            {
                _builderActionLabel.SetText("HOLSTER BUILDER");
                _builderActionLabel.color = Primary;
                _builderActionBg.color = new Color(0.14f, 0.3f, 0.28f, 0.78f);
            }
            else if (builderSlot >= 0 && builderReady)
            {
                _builderActionLabel.SetText($"ACTIVATE BUILDER [S{builderSlot + 1}]");
                _builderActionLabel.color = Dim;
                _builderActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }
            else if (builderSlot >= 0)
            {
                _builderActionLabel.SetText($"BUILDER MISSING [S{builderSlot + 1}]");
                _builderActionLabel.color = Warn;
                _builderActionBg.color = new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else
            {
                _builderActionLabel.SetText("ARM BUILDER TO S4");
                _builderActionLabel.color = Dim;
                _builderActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }

            PDAConstructionBuilderActionButton button = _builderActionBg.GetComponent<PDAConstructionBuilderActionButton>();
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
                _fieldActionRoot.gameObject.SetActive(false);
                return;
            }

            _fieldActionRoot.gameObject.SetActive(true);

            if (builderActive && canPlace)
            {
                _fieldActionLabel.SetText("DEPLOY ACTIVE");
                _fieldActionLabel.color = Primary;
                _fieldActionBg.color = new Color(0.14f, 0.3f, 0.28f, 0.78f);
            }
            else if (builderActive)
            {
                _fieldActionLabel.SetText(hasPreview ? "RETURN TO FIELD" : "PREVIEW OFFLINE");
                _fieldActionLabel.color = hasPreview ? Dim : Warn;
                _fieldActionBg.color = hasPreview
                    ? new Color(0.08f, 0.16f, 0.18f, 0.58f)
                    : new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else if (builderSlot >= 0 && builderReady)
            {
                _fieldActionLabel.SetText($"FIELD PREVIEW [S{builderSlot + 1}]");
                _fieldActionLabel.color = Dim;
                _fieldActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }
            else if (builderSlot >= 0)
            {
                _fieldActionLabel.SetText($"BUILDER CARGO [S{builderSlot + 1}]");
                _fieldActionLabel.color = Warn;
                _fieldActionBg.color = new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else if (!hasResources)
            {
                _fieldActionLabel.SetText("MISSING COST");
                _fieldActionLabel.color = Warn;
                _fieldActionBg.color = new Color(0.28f, 0.2f, 0.06f, 0.72f);
            }
            else
            {
                _fieldActionLabel.SetText("ARM + PREVIEW");
                _fieldActionLabel.color = Dim;
                _fieldActionBg.color = new Color(0.08f, 0.16f, 0.18f, 0.58f);
            }

            PDAConstructionFieldActionButton button = _fieldActionBg.GetComponent<PDAConstructionFieldActionButton>();
            if (button != null)
                button.SetVisualState(_fieldActionBg.color, new Color(0.12f, 0.24f, 0.28f, 0.82f));
        }

        private static string TrimForCard(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (normalized.Length <= maxChars)
                return normalized;

            return normalized.Substring(0, maxChars).TrimEnd() + "...";
        }

        private static int CountModulesByPowerRole(ModuleCatalog catalog, int mode)
        {
            if (catalog == null || catalog.Count <= 0)
                return 0;

            int count = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                BuildableData data = catalog.GetAt(i);
                if (data == null)
                    continue;

                bool matches = mode > 0 ? data.IsGenerator : mode < 0 ? data.IsConsumer : (!data.IsGenerator && !data.IsConsumer);
                if (matches)
                    count++;
            }

            return count;
        }

        private void AppendShortCost(StringBuilder sb, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
            {
                sb.Append("NONE");
                return;
            }

            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null)
                    continue;
                if (i > 0)
                    sb.Append(" | ");
                sb.Append(CachedToUpperInvariant(cost.item.itemName)).Append(' ').Append(cost.amount);
            }
        }

        private void AppendCostDigest(StringBuilder sb, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
            {
                sb.Append("NO BUILD COST DATA.");
                return;
            }

            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null)
                    continue;

                int owned = playerInventory != null ? playerInventory.CountTotal(cost.item) : 0;
                sb.Append(CachedToUpperInvariant(cost.item.itemName))
                    .Append(' ')
                    .Append(owned)
                    .Append('/')
                    .Append(cost.amount);

                if (i < data.buildCost.Count - 1)
                    sb.Append("  |  ");
            }
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

        private static void ClearChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                    Object.Destroy(child.gameObject);
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
            return text;
        }

        private static TextMeshProUGUI CreateSectionHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI header = CreateText(parent, "Header_" + text, TMP_Settings.defaultFontAsset, 11f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -12f), new Vector2(-14f, 18f));
            header.color = DimLow;
            header.SetText(text);
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
