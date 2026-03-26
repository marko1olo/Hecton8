// ============================================================================
// HECTON-8 — HUDQuickBar.cs
// Компактная полоска быстрого доступа (4 tool slots) на HUD.
// Sibling к HUD_V4_CanvasRoot на Suit_HUD_Canvas.
// ============================================================================

using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/HUD Quick Bar")]
    public sealed class HUDQuickBar : MonoBehaviour
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
        private static readonly Color DurGood    = new Color(0.3f, 0.9f, 0.85f, 0.7f);
        private static readonly Color DurWarning = new Color(1f, 0.74f, 0.22f, 0.7f);
        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        private const int SlotCount = 4;

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            // Dim when PDA is open
            if (_canvasGroup != null)
            {
                float target = PlayerPDA.IsOpen ? 0.15f : 1f;
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, target,
                    1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

        // ══════════════════════════════════════════════════════════
        //  AUTO-RESOLVE
        // ══════════════════════════════════════════════════════════

        private void AutoResolve()
        {
            if (toolManager == null)
                toolManager = FindFirstObjectByType<PlayerToolManager>();
            if (font == null)
                font = TMP_Settings.defaultFontAsset;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        private void Subscribe()
        {
            if (toolManager != null)
                toolManager.ActiveSlotChanged += OnSlotChanged;
        }

        private void Unsubscribe()
        {
            if (toolManager != null)
                toolManager.ActiveSlotChanged -= OnSlotChanged;
        }

        private void OnSlotChanged(int _) => Refresh();

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
            self.sizeDelta = new Vector2(totalW, slotSize);
            self.anchoredPosition = barOffset;

            // Canvas group for fade
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _durBars = new Image[SlotCount];
            _slotBgs = new Image[SlotCount];
            _slotIcons = new Image[SlotCount];
            _slotKeys = new TextMeshProUGUI[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                RectTransform slot = MakeRect("Slot_" + i, self);
                slot.pivot = new Vector2(0f, 0f);
                slot.anchorMin = new Vector2(0f, 0f);
                slot.anchorMax = new Vector2(0f, 0f);
                slot.anchoredPosition = new Vector2(i * (slotSize + slotGap), 0f);
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
                icon.color = Color.white;
                icon.gameObject.SetActive(false);
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
                keyTxt.text = (i + 1).ToString();
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
                durImg.color = DurGood;
                durImg.raycastTarget = false;
                durImg.gameObject.SetActive(false);
                _durBars[i] = durImg;
            }

            _built = true;
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void Refresh()
        {
            if (toolManager == null || _slotBgs == null) return;

            int active = toolManager.CurrentSlotIndex;

            for (int i = 0; i < SlotCount; i++)
            {
                bool isActive = i == active;
                _slotBgs[i].color = isActive ? SlotActive : SlotBg;
                _slotKeys[i].color = isActive ? KeyActive : KeyDim;

                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                if (prefab != null && prefab.TryGetComponent<PlayerTool>(out var tool)
                    && tool.ToolData != null && tool.ToolData.icon != null)
                {
                    _slotIcons[i].sprite = tool.ToolData.icon;
                    _slotIcons[i].gameObject.SetActive(true);

                    bool available = toolManager.IsToolAvailableInSlot(i);
                    _slotIcons[i].color = available
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.2f);
                }
                else
                {
                    _slotIcons[i].gameObject.SetActive(false);
                }
                // Durability bar
                if (_durBars != null && i < _durBars.Length && _durBars[i] != null)
                {
                    bool showDur = false;

                    if (prefab != null
                        && prefab.TryGetComponent<PlayerTool>(out var ptool)
                        && ptool.Metadata != null)
                    {
                        var durSys = ToolDurabilitySystem.Instance;
                        if (durSys != null)
                        {
                            float maxD = ptool.Metadata.maxDurability;
                            if (maxD > 0f)
                            {
                                float curD = durSys.GetDurability(
                                    ptool.Metadata.toolID, maxD);
                                float norm = Mathf.Clamp01(curD / maxD);

                                float fullW = slotSize - 6f;
                                _durBars[i].rectTransform.sizeDelta =
                                    new Vector2(fullW * norm, 2f);
                                _durBars[i].color =
                                    Color.Lerp(DurWarning, DurGood, norm);
                                showDur = true;
                            }
                        }
                    }

                    _durBars[i].gameObject.SetActive(showDur);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private RectTransform MakeRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.localScale = Vector3.one;
            if (parent != null) go.layer = parent.gameObject.layer;
            return r;
        }
    }
}