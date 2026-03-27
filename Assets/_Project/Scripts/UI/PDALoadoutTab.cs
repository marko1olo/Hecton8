// ============================================================================
// HECTON-8 — PDALoadoutTab.cs
// Dedicated PDA loadout tab with 4 large slot cards, readiness state,
// durability, energy profile, and cargo linkage to the real tool backend.
// ============================================================================

using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Tools;
using TMPro;
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
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private int loadoutTabIndex = 1;

        private bool _built;
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
        private Image[] _slotActionBgs;
        private Image[] _slotClearBgs;
        private TextMeshProUGUI[] _slotActionLabels;
        private TextMeshProUGUI[] _slotClearLabels;
        private TextMeshProUGUI _summaryText;
        private TextMeshProUGUI _hintText;

        private void Awake()
        {
            AutoResolveTabIndex();
            AutoResolve();
        }

        private void OnValidate()
        {
            AutoResolveTabIndex();
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
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        private void AutoResolveTabIndex()
        {
            if (gameObject.name.Contains("Loadout", System.StringComparison.OrdinalIgnoreCase))
                loadoutTabIndex = 1;
        }

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
        }

        private void HandleInventoryChanged() => RefreshAll();
        private void HandleActiveSlotChanged(int _) => RefreshSlots();
        private void HandleToolAssignmentsChanged() => RefreshAll();

        private void HandlePdaOpened(int tab)
        {
            if (tab != loadoutTabIndex) return;
            RefreshAll();
        }

        private void HandlePdaTabChanged(int _, int newTab)
        {
            if (newTab != loadoutTabIndex) return;
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
            _slotActionBgs = new Image[4];
            _slotClearBgs = new Image[4];
            _slotActionLabels = new TextMeshProUGUI[4];
            _slotClearLabels = new TextMeshProUGUI[4];

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
                _slotClearBgs[i] = clearBg;
                _slotClearLabels[i] = clearLabel;
                PDALoadoutSlotActionButton clearButton = clearRoot.gameObject.AddComponent<PDALoadoutSlotActionButton>();
                clearButton.Init(this, i, true, clearBg,
                    new Color(0.22f, 0.08f, 0.08f, 0.74f),
                    new Color(0.34f, 0.12f, 0.12f, 0.92f));
            }

            _summaryText = CreateText(self, "Summary", numericFont, 11.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(_summaryText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 18f), new Vector2(-20f, 46f));
            _summaryText.color = Dim;

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Right);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 18f), new Vector2(-20f, 46f));
            _hintText.color = DimLow;
            _hintText.SetText("Assign tools from Inventory details. Hotbar mirrors this matrix live.");

            _built = true;
        }

        private void RefreshAll()
        {
            EnsureBuilt();
            RefreshSlots();
            RefreshSummary();
        }

        private void RefreshSlots()
        {
            if (_slotRoots == null || toolManager == null) return;

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;

            for (int i = 0; i < 4; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                PlayerTool tool = prefab != null ? prefab.GetComponent<PlayerTool>() : null;
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
                    if (_slotActionRoots[i] != null) _slotActionRoots[i].gameObject.SetActive(false);
                    if (_slotClearRoots[i] != null) _slotClearRoots[i].gameObject.SetActive(false);
                    continue;
                }

                if (_slotActionRoots[i] != null) _slotActionRoots[i].gameObject.SetActive(true);
                if (_slotClearRoots[i] != null) _slotClearRoots[i].gameObject.SetActive(true);

                _slotTitles[i].SetText(item != null ? item.itemName.ToUpperInvariant() : prefab.name.ToUpperInvariant());

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

                string category = item != null ? item.category.ToString().ToUpperInvariant() : "TOOL";
                float weight = item != null ? item.weight : 0f;
                float currentDurability = meta != null && durabilitySystem != null
                    ? durabilitySystem.GetDurability(meta.toolID, meta.maxDurability)
                    : (meta != null ? meta.maxDurability : 0f);
                float normalized = meta != null
                    ? Mathf.Clamp01(currentDurability / Mathf.Max(1f, meta.maxDurability))
                    : 1f;

                _slotBodies[i].text =
                    $"CLASS    {category}\n" +
                    $"IN CARGO  {(item != null && playerInventory != null ? playerInventory.CountTotal(item) : 0)}\n" +
                    $"MASS     {weight:0.0} kg\n" +
                    $"DURAB.   {normalized:0%}\n" +
                    $"ENERGY   {(meta != null ? Mathf.Max(0f, meta.energyConsumptionRate) : 0f):0.0}/s";

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

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                PlayerTool tool = prefab != null ? prefab.GetComponent<PlayerTool>() : null;
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

            _summaryText.text =
                $"LOADOUT: {assigned}/4 assigned | READY {ready} | MISSING {missing} | " +
                $"BROKEN {broken} | ACTIVE SLOT {(toolManager.CurrentSlotIndex >= 0 ? (toolManager.CurrentSlotIndex + 1).ToString() : "--")} | " +
                $"KIT MASS {totalWeight:0.0} kg";

            if (_hintText != null)
                _hintText.SetText(GetLoadoutDirective(assigned, ready, missing, broken));
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

        internal void InvokeSlotAction(int slotIndex, bool clearAssignment)
        {
            if (toolManager == null || slotIndex < 0 || slotIndex >= toolManager.SlotCount)
                return;

            GameObject prefab = toolManager.GetAssignedToolPrefab(slotIndex);
            PlayerTool tool = prefab != null ? prefab.GetComponent<PlayerTool>() : null;
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
                NotifyWarning($"{(item != null ? item.itemName.ToUpperInvariant() : "TOOL")} IS BROKEN");
                return;
            }

            if (!toolManager.IsToolAvailableInSlot(slotIndex))
            {
                NotifyWarning($"{(item != null ? item.itemName.ToUpperInvariant() : "TOOL")} IS NOT IN CARGO");
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
            NotifyInfo($"LOADOUT ACTIVE — SLOT {slotIndex + 1}: {(item != null ? item.itemName.ToUpperInvariant() : prefab.name.ToUpperInvariant())}");
        }

        private void NotifyInfo(string message)
        {
            HUDNotification notification = FindFirstObjectByType<HUDNotification>();
            if (notification != null)
                notification.ShowInfo(message);
        }

        private void NotifyWarning(string message)
        {
            HUDNotification notification = FindFirstObjectByType<HUDNotification>();
            if (notification != null)
                notification.ShowWarning(message);
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
            text.raycastTarget = false;
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
}
