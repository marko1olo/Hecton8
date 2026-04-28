// ============================================================================
// HECTON-8 — PDALoadoutTab.cs
// Dedicated PDA loadout tab with 4 large slot cards, readiness state,
// durability, energy profile, and cargo linkage to the real tool backend.
// ============================================================================

using Hecton8.Gameplay;
using Hecton8.Bootstrap;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Tools;
using Hecton8.Core;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Loadout Tab")]
    public sealed class PDALoadoutTab : MonoBehaviour
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
        [SerializeField] private LayerMask fieldAdviceMask = ~0;

        // ════════════════════════════════════════════════════════════
        //  CACHED FIELDS FOR ZERO-GC OPTIMIZATION
        // ════════════════════════════════════════════════════════════

        /// <summary>Кэшированный StringBuilder для сборки текста слотов (избегает аллокаций в RefreshSlots)</summary>
        private readonly System.Text.StringBuilder _slotBodyBuilder = new System.Text.StringBuilder(256);

        /// <summary>Кэшированный StringBuilder для сборки summary текста (избегает аллокаций в RefreshSummary)</summary>
        private readonly System.Text.StringBuilder _summaryBuilder = new System.Text.StringBuilder(512);

        /// <summary>Кэшированный StringBuilder для сборки preset текста (избегает аллокаций в RefreshPresets)</summary>
        private readonly System.Text.StringBuilder _presetBuilder = new System.Text.StringBuilder(128);
        private readonly System.Collections.Generic.Dictionary<ulong, PlayerTool> _prefabToolCache = new System.Collections.Generic.Dictionary<ulong, PlayerTool>(32); // COLD ALLOC: Dictionary<ulong, PlayerTool>(32) — caches prefab PlayerTool owners for repeated loadout refreshes — owner: PDALoadoutTab

        /// <summary>Кэшированные строки для ToUpperInvariant (избегает повторных аллокаций)</summary>
        private static readonly string[] _cachedUpperStrings = new string[16];

        /// <summary>Кэшированные строки для ItemCategory enum (избегает Enum.ToString() в hot path)</summary>
        private static readonly string[] _cachedCategoryStrings = new string[]
        {
            "MISCELLANEOUS", // ItemCategory.Miscellaneous = 0
            "MATERIAL",      // ItemCategory.Material = 1
            "TOOL",          // ItemCategory.Tool = 2
            "EQUIPMENT",     // ItemCategory.Equipment = 3
            "CONSUMABLE",    // ItemCategory.Consumable = 4
            "COMPONENT"      // ItemCategory.Component = 5
        };

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
        private ToolDurabilitySystem _subscribedDurabilitySystem;

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == loadoutTabIndex;

        private void Awake()
        {
            AutoResolveTabIndex();
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
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            RefreshDurabilityBindings();
            _refreshDirty = true;
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        // ════════════════════════════════════════════════════════════
        //  ZERO-GC OPTIMIZATION HELPERS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Кэшированный ToUpperInvariant для избежания повторных аллокаций строк.
        /// Хранит до 16 последних преобразований для повторного использования.
        /// </summary>
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

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

        private void AutoResolve()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerInventory == null && playerContext != null)
                playerInventory = playerContext.Inventory;
            if (toolManager == null && playerContext != null)
                toolManager = playerContext.ToolManager;
            if (playerPDA == null && playerContext != null)
                playerPDA = playerContext.PlayerPDA;

            if ((!playerInventory || !toolManager || !playerPDA) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
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
                playerPDA = GetComponentInParent<PlayerPDA>();
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
        }

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
            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged += HandleActiveSlotChanged;
                toolManager.ToolAssignmentsChanged += HandleToolAssignmentsChanged;
            }

            PDAEvents.OnOpened += HandlePdaOpened;
            PDAEvents.OnTabChanged += HandlePdaTabChanged;
            PlayerExpressionEvents.OnProfileChanged += HandlePlayerExpressionChanged;
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged -= HandleActiveSlotChanged;
                toolManager.ToolAssignmentsChanged -= HandleToolAssignmentsChanged;
            }

            PDAEvents.OnOpened -= HandlePdaOpened;
            PDAEvents.OnTabChanged -= HandlePdaTabChanged;
            PlayerExpressionEvents.OnProfileChanged -= HandlePlayerExpressionChanged;
            UnsubscribeDurabilitySystem();
        }

        private void RefreshDurabilityBindings()
        {
            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
            if (_subscribedDurabilitySystem == durabilitySystem)
                return;

            UnsubscribeDurabilitySystem();
            if (durabilitySystem == null)
                return;

            durabilitySystem.OnDurabilityChanged += HandleDurabilityChanged;
            durabilitySystem.OnToolBroken += HandleToolBroken;
            durabilitySystem.OnToolRepaired += HandleToolRepaired;
            _subscribedDurabilitySystem = durabilitySystem;
        }

        private void UnsubscribeDurabilitySystem()
        {
            if (_subscribedDurabilitySystem == null)
                return;

            _subscribedDurabilitySystem.OnDurabilityChanged -= HandleDurabilityChanged;
            _subscribedDurabilitySystem.OnToolBroken -= HandleToolBroken;
            _subscribedDurabilitySystem.OnToolRepaired -= HandleToolRepaired;
            _subscribedDurabilitySystem = null;
        }

        private void HandleInventoryChanged()
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        private void HandleActiveSlotChanged(int _)
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        private void HandleToolAssignmentsChanged()
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        private void HandleDurabilityChanged(string _, float __, float ___)
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        private void HandleToolBroken(string _)
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        private void HandleToolRepaired(string _, float __)
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        private void HandlePlayerExpressionChanged(PlayerExpressionProfile _)
        {
            _refreshDirty = true;
            if (IsTabActive)
                RefreshAll();
        }

        private void HandlePdaOpened(int tab)
        {
            if (tab != loadoutTabIndex) return;
            RefreshDurabilityBindings();
            _refreshDirty = true;
            RefreshAll();
        }

        private void HandlePdaTabChanged(int _, int newTab)
        {
            if (newTab != loadoutTabIndex) return;
            RefreshDurabilityBindings();
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
            title.SetText("LOADOUT MATRIX");

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            sub.SetText("quick-slot readiness, durability state, and expedition utility profile");

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

                RectTransform card = CreateRect(self, "LoadoutSlot_" + (i + 1));
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
                slotHdr.SetText($"SLOT {i + 1}");

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
                clearLabel.SetText("CLEAR");
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
            _identityActionLabel.SetText("CYCLE SUIT IDENTITY");
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
            _recommendedActionLabel.SetText("APPLY SUGGESTED");
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
            _hintText.SetText("Assign tools from Inventory details. Hotbar mirrors this matrix live.");

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

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;

            for (int i = 0; i < 4; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                PlayerTool tool = ResolvePrefabTool(prefab);
                ItemData item = tool != null ? tool.ToolData : null;
                ToolMetadata meta = tool != null ? tool.Metadata : null;

                bool active = toolManager.CurrentSlotIndex == i;
                bool assigned = prefab != null && tool != null;
                bool available = toolManager.IsToolAvailableInSlot(i);
                bool broken = meta != null && durabilitySystem != null && durabilitySystem.IsBroken(meta.toolID);

                _slotBgs[i].color = active ? BoxActive : BoxBg;
                _slotIcons[i].sprite = item != null ? item.icon : null;
                _slotIcons[i].enabled = item != null && item.icon != null;

                if (!assigned)
                {
                    if (_slotAccents[i] != null)
                        _slotAccents[i].color = new Color(0.42f, 0.36f, 0.14f, 0.86f);
                    if (_slotStatusBgs[i] != null)
                        _slotStatusBgs[i].color = new Color(0.28f, 0.2f, 0.06f, 0.76f);
                    _slotTitles[i].SetText("UNASSIGNED");
                    _slotStatuses[i].color = Missing;
                    _slotStatuses[i].SetText("EMPTY");
                    _slotBodies[i].SetText("No held-tool prefab is mapped to this quick slot.\nAssign a tool from Inventory to arm the slot.");
                    if (_slotActionLabels[i] != null) _slotActionLabels[i].SetText("UNASSIGNED");
                    SetCanvasGroupVisible(_slotActionCanvasGroups[i], false);
                    SetCanvasGroupVisible(_slotClearCanvasGroups[i], false);
                    continue;
                }

                SetCanvasGroupVisible(_slotActionCanvasGroups[i], true);
                SetCanvasGroupVisible(_slotClearCanvasGroups[i], true);

                _slotTitles[i].SetText(item != null ? CachedToUpperInvariant(item.itemName) : CachedToUpperInvariant(prefab.name));

                if (broken)
                {
                    if (_slotAccents[i] != null)
                        _slotAccents[i].color = new Color(0.88f, 0.34f, 0.28f, 0.92f);
                    if (_slotStatusBgs[i] != null)
                        _slotStatusBgs[i].color = new Color(0.34f, 0.12f, 0.12f, 0.84f);
                    _slotStatuses[i].color = Broken;
                    _slotStatuses[i].SetText(active ? "BROKEN / ACTIVE" : "BROKEN");
                }
                else if (!available)
                {
                    if (_slotAccents[i] != null)
                        _slotAccents[i].color = new Color(0.94f, 0.68f, 0.22f, 0.92f);
                    if (_slotStatusBgs[i] != null)
                        _slotStatusBgs[i].color = new Color(0.3f, 0.2f, 0.06f, 0.82f);
                    _slotStatuses[i].color = Missing;
                    _slotStatuses[i].SetText(active ? "MISSING / ACTIVE" : "MISSING");
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
                    _slotStatuses[i].SetText(active ? "READY / ACTIVE" : "READY");
                }

                string category = item != null && (int)item.category >= 0 && (int)item.category < _cachedCategoryStrings.Length
                    ? _cachedCategoryStrings[(int)item.category]
                    : "TOOL";
                float weight = item != null ? item.weight : 0f;
                float currentDurability = meta != null && durabilitySystem != null
                    ? durabilitySystem.GetDurability(meta.toolID, meta.maxDurability)
                    : (meta != null ? meta.maxDurability : 0f);
                float normalized = meta != null
                    ? Mathf.Clamp01(currentDurability / Mathf.Max(1f, meta.maxDurability))
                    : 1f;

                // Используем кэшированный StringBuilder для сборки текста слота (zero-GC)
                _slotBodyBuilder.Clear();
                _slotBodyBuilder.Append("CLASS    ").Append(category).Append('\n');
                _slotBodyBuilder.Append("IN CARGO  ").Append(item != null && playerInventory != null ? playerInventory.CountTotal(item) : 0).Append('\n');
                _slotBodyBuilder.Append("MASS     ").Append(weight.ToString("0.0")).Append(" kg\n");
                _slotBodyBuilder.Append("DURAB.   ").Append(normalized.ToString("0%")).Append('\n');
                _slotBodyBuilder.Append("ENERGY   ").Append((meta != null ? Mathf.Max(0f, meta.energyConsumptionRate) : 0f).ToString("0.0")).Append("/s");

                _slotBodies[i].SetText(_slotBodyBuilder);

                if (_slotActionLabels[i] != null)
                {
                    if (active)
                        _slotActionLabels[i].SetText("HOLSTER");
                    else if (broken)
                        _slotActionLabels[i].SetText("BROKEN");
                    else if (!available)
                        _slotActionLabels[i].SetText("MISSING");
                    else
                        _slotActionLabels[i].SetText("ACTIVATE");
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
            string recommendedPresetName = "GENERAL";
            string recommendedPresetDirective = "No authored target in front of the diver. General-purpose expedition loadout remains valid.";

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                PlayerTool tool = ResolvePrefabTool(prefab);
                if (tool == null)
                    continue;

                assigned++;
                if (tool.ToolData != null)
                    totalWeight += tool.ToolData.weight;

                if (tool.Metadata != null && durabilitySystem != null && durabilitySystem.IsBroken(tool.Metadata.toolID))
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

            // Используем кэшированный StringBuilder для сборки summary текста (zero-GC)
            _summaryBuilder.Clear();
            _summaryBuilder.Append("LOADOUT: ").Append(assigned).Append("/4 assigned | READY ").Append(ready);
            _summaryBuilder.Append(" | MISSING ").Append(missing).Append(" | BROKEN ").Append(broken);
            _summaryBuilder.Append(" | ACTIVE SLOT ").Append(toolManager.CurrentSlotIndex >= 0 ? (toolManager.CurrentSlotIndex + 1).ToString() : "--");
            _summaryBuilder.Append(" | KIT MASS ").Append(totalWeight.ToString("0.0")).Append(" kg");
            _summaryBuilder.Append(" | PRESET ").Append(GetMatchedPresetName());
            _summaryBuilder.Append(" | IDENTITY ").Append(GetActiveExpressionName());
            string liveSuitName = GetLiveExpressionSuitName();
            if (!string.IsNullOrWhiteSpace(liveSuitName))
                _summaryBuilder.Append(" | SHELL ").Append(CachedToUpperInvariant(liveSuitName));
            _summaryBuilder.Append(" | SUGGESTED ").Append(recommendedPresetName);
            _summaryBuilder.Append('\n');
            _summaryBuilder.Append("ACTIVE TOOL: ").Append(toolManager.GetCurrentToolOperationalSummary());

            _summaryText.SetText(_summaryBuilder);

            // Оптимизируем hint текст через StringBuilder
            using (var scope = StringBuilderScope.Get())
            {
                var sb = scope.Value;
                sb.Append(GetLoadoutDirective(assigned, ready, missing, broken));
                sb.Append("  LIVE: ").Append(toolManager.GetCurrentToolOperationalDirective());
                sb.Append("  IDENTITY: ").Append(GetActiveExpressionSummary());

                string identityLoadout = GetActiveExpressionLoadoutName();
                if (!string.IsNullOrWhiteSpace(identityLoadout))
                    sb.Append("  IDENTITY KIT: ").Append(CachedToUpperInvariant(identityLoadout));

                string identitySuit = GetActiveExpressionSuitName();
                if (!string.IsNullOrWhiteSpace(identitySuit))
                {
                    sb.Append("  IDENTITY SHELL: ").Append(CachedToUpperInvariant(identitySuit));

                    if (!IsExpressionSuitApplied())
                        sb.Append(" (PENDING SYNC)");
                }

                sb.Append("  FIELD: ").Append(recommendedPresetDirective);

                if (_hintText != null)
                    _hintText.SetText(sb);
            }

            RefreshRecommendedAction();
        }

        private void RefreshIdentityAction()
        {
            if (_identityActionRoot == null || _identityActionLabel == null || _identityActionBg == null)
                return;

            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            if (playerExpressionManager == null || playerExpressionManager.ProfileCount <= 1)
            {
                SetCanvasGroupVisible(_identityActionCanvasGroup, false);
                return;
            }

            int nextProfileIndex = playerExpressionManager.GetNextProfileIndex();
            PlayerExpressionProfile nextProfile = playerExpressionManager.GetProfile(nextProfileIndex);
            if (nextProfile == null)
            {
                SetCanvasGroupVisible(_identityActionCanvasGroup, false);
                return;
            }

            SetCanvasGroupVisible(_identityActionCanvasGroup, true);
            _identityActionLabel.SetText($"NEXT IDENTITY - {CachedToUpperInvariant(nextProfile.DisplayName)}");
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
                    _presetTitles[i].SetText(CachedToUpperInvariant(preset.presetName));

                if (_presetBodies[i] != null)
                {
                    int ready = CountReadyToolsInPreset(preset);

                    // Используем кэшированный StringBuilder для сборки preset текста (zero-GC)
                    _presetBuilder.Clear();
                    _presetBuilder.Append(GetPresetBrief(preset)).Append('\n');
                    _presetBuilder.Append("READY NOW ").Append(ready).Append("/4");
                    if (matched)
                        _presetBuilder.Append(" | ACTIVE");

                    _presetBodies[i].SetText(_presetBuilder);
                }
            }
        }

        internal void InvokeSlotAction(int slotIndex, bool clearAssignment)
        {
            if (toolManager == null || slotIndex < 0 || slotIndex >= toolManager.SlotCount)
                return;

            GameObject prefab = toolManager.GetAssignedToolPrefab(slotIndex);
            PlayerTool tool = ResolvePrefabTool(prefab);
            ItemData item = tool != null ? tool.ToolData : null;

            if (clearAssignment)
            {
                if (prefab == null)
                {
                    NotifyWarning($"LOADOUT SLOT {slotIndex + 1} IS ALREADY EMPTY");
                    return;
                }

                toolManager.SetAssignedToolPrefab(slotIndex, null, holsterIfCurrentInvalid: true);
                RefreshAll();
                NotifyInfo($"LOADOUT CLEARED — SLOT {slotIndex + 1}");
                return;
            }

            if (prefab == null || tool == null)
            {
                NotifyWarning($"NO TOOL ASSIGNED TO SLOT {slotIndex + 1}");
                return;
            }

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
            if (tool.Metadata != null && durabilitySystem != null && durabilitySystem.IsBroken(tool.Metadata.toolID))
            {
                NotifyWarning($"{(item != null ? CachedToUpperInvariant(item.itemName) : "TOOL")} IS BROKEN");
                return;
            }

            if (!toolManager.IsToolAvailableInSlot(slotIndex))
            {
                NotifyWarning($"{(item != null ? CachedToUpperInvariant(item.itemName) : "TOOL")} IS NOT IN CARGO");
                return;
            }

            if (toolManager.CurrentSlotIndex == slotIndex)
            {
                toolManager.Holster();
                RefreshAll();
                NotifyInfo($"LOADOUT HOLSTERED — SLOT {slotIndex + 1}");
                return;
            }

            toolManager.SwitchToSlot(slotIndex);
            RefreshAll();
            NotifyInfo($"LOADOUT ACTIVE — SLOT {slotIndex + 1}: {(item != null ? CachedToUpperInvariant(item.itemName) : CachedToUpperInvariant(prefab.name))}");
        }

        internal void InvokePresetAction(int presetIndex)
        {
            if (toolManager == null || loadoutPresets == null || presetIndex < 0 || presetIndex >= loadoutPresets.Length)
                return;

            ToolLoadoutPreset preset = loadoutPresets[presetIndex];
            if (preset == null)
            {
                NotifyWarning("LOADOUT PRESET IS NOT CONFIGURED");
                return;
            }

            if (!toolManager.ApplyLoadoutPreset(preset, holsterBeforeApplyingPreset))
            {
                NotifyWarning($"FAILED TO APPLY {CachedToUpperInvariant(preset.presetName)}");
                return;
            }

            RefreshAll();
            NotifyInfo($"LOADOUT PRESET APPLIED - {CachedToUpperInvariant(preset.presetName)}");
        }

        internal void InvokeRecommendedPresetAction()
        {
            int presetIndex = GetRecommendedPresetIndex();
            if (presetIndex < 0)
            {
                NotifyWarning("NO SUGGESTED PRESET FOR CURRENT FIELD TARGET");
                return;
            }

            ToolLoadoutPreset preset = loadoutPresets != null && presetIndex < loadoutPresets.Length
                ? loadoutPresets[presetIndex]
                : null;
            if (preset == null)
            {
                NotifyWarning("SUGGESTED PRESET IS NOT CONFIGURED");
                return;
            }

            if (MatchesPreset(preset))
            {
                NotifyInfo($"SUGGESTED KIT ALREADY ACTIVE - {CachedToUpperInvariant(preset.presetName)}");
                return;
            }

            InvokePresetAction(presetIndex);
        }

        internal void InvokeIdentityCycleAction()
        {
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            if (playerExpressionManager == null)
            {
                NotifyWarning("SUIT IDENTITY CATALOG IS NOT AVAILABLE");
                return;
            }

            if (!playerExpressionManager.CycleNextProfile(false))
            {
                NotifyWarning("FAILED TO SWITCH SUIT IDENTITY");
                return;
            }

            RefreshAll();
        }

        private void NotifyInfo(string message)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            if (hudNotification != null)
                hudNotification.ShowInfo(message);
        }

        private void NotifyWarning(string message)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            if (hudNotification != null)
                hudNotification.ShowWarning(message);
        }

        private void BuildPresetStrip(RectTransform self)
        {
            if (_presetRoots == null || _presetRoots.Length == 0)
                return;

            TextMeshProUGUI hdr = CreateText(self, "PresetHeader", labelFont, 10.5f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(hdr.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 58f), new Vector2(-20f, 80f));
            hdr.color = DimLow;
            hdr.SetText("MISSION PRESETS");

            float gap = 10f;
            float width = (1320f - 40f - gap * (_presetRoots.Length - 1)) / Mathf.Max(1, _presetRoots.Length);

            for (int i = 0; i < _presetRoots.Length; i++)
            {
                RectTransform root = CreateRect(self, $"Preset_{i + 1}");
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
            _recommendedActionLabel.SetText(matched
                ? $"SUGGESTED ACTIVE - {CachedToUpperInvariant(preset.presetName)}"
                : $"APPLY SUGGESTED - {CachedToUpperInvariant(preset.presetName)}");
            _recommendedActionBg.color = matched
                ? new Color(0.1f, 0.3f, 0.3f, 0.9f)
                : new Color(0.08f, 0.18f, 0.2f, 0.82f);
        }

        private string GetMatchedPresetName()
        {
            if (loadoutPresets == null)
                return "--";

            for (int i = 0; i < loadoutPresets.Length; i++)
            {
                ToolLoadoutPreset preset = loadoutPresets[i];
                if (preset != null && MatchesPreset(preset))
                    return CachedToUpperInvariant(preset.presetName);
            }

            return "CUSTOM";
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
                PlayerTool tool = ResolvePrefabTool(prefab);
                if (tool?.ToolData != null && playerInventory.ContainsItem(tool.ToolData))
                    count++;
            }

            return count;
        }

        private static string GetPresetBrief(ToolLoadoutPreset preset)
        {
            if (preset == null)
                return "NO DATA";

            string description = preset.description;
            if (string.IsNullOrWhiteSpace(description))
                return "Standard expedition slot map.";

            int newline = description.IndexOf('\n');
            if (newline >= 0)
                description = description.Substring(0, newline);

            return description.Length > 44
                ? description.Substring(0, 44).TrimEnd() + "..."
                : description;
        }

        private PlayerTool ResolvePrefabTool(GameObject prefab)
        {
            if (prefab == null)
                return null;

            ulong prefabId = EntityId.ToULong(prefab.GetEntityId());
            if (_prefabToolCache.TryGetValue(prefabId, out PlayerTool cachedTool) &&
                cachedTool != null)
            {
                return cachedTool;
            }

            if (!prefab.TryGetComponent(out PlayerTool resolvedTool))
                return null;

            _prefabToolCache[prefabId] = resolvedTool;
            return resolvedTool;
        }

        private string GetActiveExpressionName()
        {
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            return playerExpressionManager != null
                ? CachedToUpperInvariant(playerExpressionManager.GetActiveProfileName())
                : "STANDARD";
        }

        private string GetActiveExpressionSummary()
        {
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            return playerExpressionManager != null
                ? playerExpressionManager.GetActiveProfileSummary()
                : "Suit identity matrix is offline.";
        }

        private string GetActiveExpressionLoadoutName()
        {
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            return playerExpressionManager != null
                ? playerExpressionManager.GetActiveRecommendedLoadoutName()
                : string.Empty;
        }

        private string GetActiveExpressionSuitName()
        {
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            return playerExpressionManager != null
                ? playerExpressionManager.GetActiveRecommendedSuitName()
                : string.Empty;
        }

        private string GetLiveExpressionSuitName()
        {
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            return playerExpressionManager != null
                ? playerExpressionManager.GetLiveSuitName()
                : string.Empty;
        }

        private bool IsExpressionSuitApplied()
        {
            if (playerExpressionManager == null)
                playerExpressionManager = PlayerExpressionManager.Instance;

            return playerExpressionManager != null && playerExpressionManager.IsActiveRecommendedSuitApplied();
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

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target == null)
                return null;

            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null)
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
