// ============================================================================
// HECTON-8 — HUDQuickBar.cs
// Компактная полоска быстрого доступа (4 tool slots) на HUD.
// Sibling к HUD_V4_CanvasRoot на Suit_HUD_Canvas.
// ============================================================================

using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Tools;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/HUD Quick Bar")]
    public sealed class HUDQuickBar : MonoBehaviour, ITickable
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
        private static readonly Color IconHidden = new Color(1f, 1f, 1f, 0f);
        private static readonly Color IconUnavailable = new Color(1f, 1f, 1f, 0.2f);
        private static readonly Color DurGood    = new Color(0.3f, 0.9f, 0.85f, 0.7f);
        private static readonly Color DurWarning = new Color(1f, 0.74f, 0.22f, 0.7f);
        private static readonly Color DurHidden = new Color(0.3f, 0.9f, 0.85f, 0f);
        private static readonly Color SummaryColor = new Color(0.9f, 0.98f, 1f, 0.94f);
        private static readonly Color DirectiveColor = new Color(0.64f, 0.83f, 0.88f, 0.92f);
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
        private TextMeshProUGUI _toolSummary;
        private TextMeshProUGUI _toolDirective;
        private float _nextStatusRefreshAt;
        private bool[] _slotIconVisible;
        private bool[] _slotIconAvailable;
        private Sprite[] _slotIconSprites;
        private bool[] _slotDurVisible;
        private float[] _slotDurWidths;
        private string _lastSummaryText;
        private string _lastDirectiveBase;
        private string _lastDirectiveAdvicePreset;
        private bool _lastDirectiveHasAdvice;
        private bool _registeredToTickManager;
        private bool _slotVisualsDirty;
        private bool _statusDirty;
        private ToolDurabilitySystem _subscribedDurabilitySystem;
        private readonly StringBuilder _statusBuilder = new StringBuilder(160);
        [SerializeField] private float fieldAdviceRange = 18f;
        [SerializeField] private LayerMask fieldAdviceMask = ~0;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            MarkAllDirty();
            Refresh(forceStatus: true);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            Unsubscribe();
        }

        public void Tick(float deltaTime)
        {
            RefreshDurabilitySubscription();

            // Dim when PDA is open
            if (_canvasGroup != null)
            {
                float target = PlayerPDA.IsOpen ? 0.15f : 1f;
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, target,
                    1f - Mathf.Exp(-8f * deltaTime));
            }

            Refresh();
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registeredToTickManager = false;
        }

        // ══════════════════════════════════════════════════════════
        //  AUTO-RESOLVE
        // ══════════════════════════════════════════════════════════

        private void AutoResolve()
        {
            if (toolManager == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    toolManager = playerTransform.GetComponentInChildren<PlayerToolManager>(true);
                }
            }
            if (font == null)
                font = TMP_Settings.defaultFontAsset;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        private void Subscribe()
        {
            RefreshDurabilitySubscription();

            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged += OnSlotChanged;
                toolManager.ToolAssignmentsChanged += OnAssignmentsChanged;
            }
        }

        private void Unsubscribe()
        {
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged -= OnSlotChanged;
                toolManager.ToolAssignmentsChanged -= OnAssignmentsChanged;
            }

            if (_subscribedDurabilitySystem != null)
            {
                _subscribedDurabilitySystem.OnDurabilityChanged -= HandleDurabilityChanged;
                _subscribedDurabilitySystem.OnToolBroken -= HandleToolBroken;
                _subscribedDurabilitySystem.OnToolRepaired -= HandleToolRepaired;
                _subscribedDurabilitySystem = null;
            }
        }

        private void RefreshDurabilitySubscription()
        {
            ToolDurabilitySystem currentSystem = ToolDurabilitySystem.Instance;
            if (ReferenceEquals(_subscribedDurabilitySystem, currentSystem))
                return;

            if (_subscribedDurabilitySystem != null)
            {
                _subscribedDurabilitySystem.OnDurabilityChanged -= HandleDurabilityChanged;
                _subscribedDurabilitySystem.OnToolBroken -= HandleToolBroken;
                _subscribedDurabilitySystem.OnToolRepaired -= HandleToolRepaired;
            }

            _subscribedDurabilitySystem = currentSystem;
            if (_subscribedDurabilitySystem != null)
            {
                _subscribedDurabilitySystem.OnDurabilityChanged += HandleDurabilityChanged;
                _subscribedDurabilitySystem.OnToolBroken += HandleToolBroken;
                _subscribedDurabilitySystem.OnToolRepaired += HandleToolRepaired;
            }
        }

        private void MarkAllDirty()
        {
            _slotVisualsDirty = true;
            _statusDirty = true;
            _nextStatusRefreshAt = 0f;
        }

        private void OnSlotChanged(int _)
        {
            _slotVisualsDirty = true;
            _statusDirty = true;
            Refresh(forceStatus: true);
        }

        private void OnAssignmentsChanged()
        {
            _slotVisualsDirty = true;
            _statusDirty = true;
            Refresh(forceStatus: true);
        }

        private void HandleDurabilityChanged(string toolId, float _, float __)
        {
            InvalidateToolSlotVisuals(toolId);
        }

        private void HandleToolBroken(string toolId)
        {
            InvalidateToolSlotVisuals(toolId);
        }

        private void HandleToolRepaired(string toolId, float _)
        {
            InvalidateToolSlotVisuals(toolId);
        }

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
            self.sizeDelta = new Vector2(totalW, slotSize + 44f);
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
            _slotIconVisible = new bool[SlotCount];
            _slotIconAvailable = new bool[SlotCount];
            _slotIconSprites = new Sprite[SlotCount];
            _slotDurVisible = new bool[SlotCount];
            _slotDurWidths = new float[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                RectTransform slot = MakeRect("Slot_" + i, self);
                slot.pivot = new Vector2(0f, 0f);
                slot.anchorMin = new Vector2(0f, 0f);
                slot.anchorMax = new Vector2(0f, 0f);
                slot.anchoredPosition = new Vector2(i * (slotSize + slotGap), 30f);
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
                icon.color = IconHidden;
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
                durImg.color = DurHidden;
                durImg.raycastTarget = false;
                durImg.rectTransform.sizeDelta = new Vector2(0f, 2f);
                _durBars[i] = durImg;
            }

            RectTransform summaryR = MakeRect("ToolSummary", self);
            summaryR.anchorMin = new Vector2(0f, 0f);
            summaryR.anchorMax = new Vector2(1f, 0f);
            summaryR.pivot = new Vector2(0.5f, 0f);
            summaryR.anchoredPosition = new Vector2(0f, 14f);
            summaryR.sizeDelta = new Vector2(0f, 16f);
            _toolSummary = summaryR.gameObject.AddComponent<TextMeshProUGUI>();
            _toolSummary.font = font;
            _toolSummary.fontSize = 11f;
            _toolSummary.fontStyle = FontStyles.Bold;
            _toolSummary.alignment = TextAlignmentOptions.Center;
            _toolSummary.textWrappingMode = TextWrappingModes.NoWrap;
            _toolSummary.color = SummaryColor;
            _toolSummary.raycastTarget = false;

            RectTransform directiveR = MakeRect("ToolDirective", self);
            directiveR.anchorMin = new Vector2(0f, 0f);
            directiveR.anchorMax = new Vector2(1f, 0f);
            directiveR.pivot = new Vector2(0.5f, 0f);
            directiveR.anchoredPosition = new Vector2(0f, 0f);
            directiveR.sizeDelta = new Vector2(0f, 14f);
            _toolDirective = directiveR.gameObject.AddComponent<TextMeshProUGUI>();
            _toolDirective.font = font;
            _toolDirective.fontSize = 10f;
            _toolDirective.fontStyle = FontStyles.Normal;
            _toolDirective.alignment = TextAlignmentOptions.Center;
            _toolDirective.textWrappingMode = TextWrappingModes.NoWrap;
            _toolDirective.color = DirectiveColor;
            _toolDirective.raycastTarget = false;

            _built = true;
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void InvalidateToolSlotVisuals(string toolId)
        {
            if (string.IsNullOrEmpty(toolId) || !IsAssignedToolIdTracked(toolId))
                return;

            _slotVisualsDirty = true;
            _statusDirty = true;
        }

        private bool IsAssignedToolIdTracked(string toolId)
        {
            if (toolManager == null || string.IsNullOrEmpty(toolId))
                return false;

            for (int i = 0; i < SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                if (prefab == null || !prefab.TryGetComponent(out PlayerTool tool) || tool.Metadata == null)
                    continue;

                if (string.Equals(tool.Metadata.toolID, toolId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void Refresh(bool forceStatus = false)
        {
            if (toolManager == null || _slotBgs == null)
                return;

            if (_slotVisualsDirty)
            {
                RefreshSlotVisuals();
                _slotVisualsDirty = false;
            }

            bool shouldPollStatus = toolManager.CurrentTool != null && Time.unscaledTime >= _nextStatusRefreshAt;
            if (!forceStatus && !_statusDirty && !shouldPollStatus)
                return;

            RefreshStatusText();
            _statusDirty = false;
            _nextStatusRefreshAt = Time.unscaledTime + 0.15f;
        }

        private void RefreshSlotVisuals()
        {
            int activeSlot = toolManager.CurrentSlotIndex;
            for (int i = 0; i < SlotCount; i++)
                RefreshSlotVisuals(i, activeSlot);
        }

        private void RefreshSlotVisuals(int slotIndex, int activeSlot)
        {
            bool isActive = slotIndex == activeSlot;
            Color desiredSlotBackground = isActive ? SlotActive : SlotBg;
            if (_slotBgs[slotIndex].color != desiredSlotBackground)
                _slotBgs[slotIndex].color = desiredSlotBackground;

            Color desiredKeyColor = isActive ? KeyActive : KeyDim;
            if (_slotKeys[slotIndex].color != desiredKeyColor)
                _slotKeys[slotIndex].color = desiredKeyColor;

            GameObject prefab = toolManager.GetAssignedToolPrefab(slotIndex);
            if (prefab != null && prefab.TryGetComponent(out PlayerTool tool) &&
                tool.ToolData != null && tool.ToolData.icon != null)
            {
                bool available = toolManager.IsToolAvailableInSlot(slotIndex);
                Sprite desiredSprite = tool.ToolData.icon;
                if (!ReferenceEquals(_slotIconSprites[slotIndex], desiredSprite))
                {
                    _slotIcons[slotIndex].sprite = desiredSprite;
                    _slotIconSprites[slotIndex] = desiredSprite;
                }

                if (!_slotIconVisible[slotIndex] || _slotIconAvailable[slotIndex] != available)
                {
                    _slotIcons[slotIndex].color = available ? Color.white : IconUnavailable;
                    _slotIconVisible[slotIndex] = true;
                    _slotIconAvailable[slotIndex] = available;
                }
            }
            else if (_slotIconVisible[slotIndex] || _slotIconSprites[slotIndex] != null)
            {
                _slotIcons[slotIndex].sprite = null;
                _slotIcons[slotIndex].color = IconHidden;
                _slotIconSprites[slotIndex] = null;
                _slotIconVisible[slotIndex] = false;
                _slotIconAvailable[slotIndex] = false;
            }

            RefreshDurabilityVisual(slotIndex, prefab);
        }

        private void RefreshDurabilityVisual(int slotIndex, GameObject prefab)
        {
            if (_durBars == null || slotIndex >= _durBars.Length || _durBars[slotIndex] == null)
                return;

            bool showDurability = false;
            float desiredWidth = 0f;
            Color desiredColor = DurHidden;

            if (prefab != null && prefab.TryGetComponent(out PlayerTool tool) && tool.Metadata != null)
            {
                ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
                if (durabilitySystem != null)
                {
                    float maxDurability = tool.Metadata.maxDurability;
                    if (maxDurability > 0f)
                    {
                        float currentDurability = durabilitySystem.GetDurability(tool.Metadata.toolID, maxDurability);
                        float normalizedDurability = Mathf.Clamp01(currentDurability / maxDurability);
                        desiredWidth = (slotSize - 6f) * normalizedDurability;
                        desiredColor = Color.Lerp(DurWarning, DurGood, normalizedDurability);
                        showDurability = true;
                    }
                }
            }

            if (_slotDurVisible[slotIndex] != showDurability || !Mathf.Approximately(_slotDurWidths[slotIndex], desiredWidth))
            {
                _durBars[slotIndex].rectTransform.sizeDelta = new Vector2(desiredWidth, 2f);
                _slotDurWidths[slotIndex] = desiredWidth;
                _slotDurVisible[slotIndex] = showDurability;
            }

            if (_slotDurVisible[slotIndex])
            {
                if (_durBars[slotIndex].color != desiredColor)
                    _durBars[slotIndex].color = desiredColor;
            }
            else if (_durBars[slotIndex].color != DurHidden)
            {
                _durBars[slotIndex].color = DurHidden;
            }
        }

        private void RefreshStatusText()
        {
            if (_toolSummary != null)
            {
                string summary = toolManager.GetCurrentToolOperationalSummary();
                if (_lastSummaryText != summary)
                {
                    _statusBuilder.Clear();
                    if (!string.IsNullOrEmpty(summary))
                        _statusBuilder.Append(summary);

                    _toolSummary.SetText(_statusBuilder);
                    _lastSummaryText = summary;
                }
            }

            if (_toolDirective == null)
                return;

            string directive = toolManager.GetCurrentToolOperationalDirective();
            Transform origin = toolManager != null ? toolManager.transform : null;
            bool hasAdvice = FieldLoadoutAdvisor.TryBuildForwardPresetName(origin, fieldAdviceRange, fieldAdviceMask, out string advicePreset);
            if (_lastDirectiveBase != directive ||
                _lastDirectiveHasAdvice != hasAdvice ||
                _lastDirectiveAdvicePreset != advicePreset)
            {
                _statusBuilder.Clear();
                if (!string.IsNullOrEmpty(directive))
                    _statusBuilder.Append(directive);

                if (hasAdvice && !string.IsNullOrEmpty(advicePreset))
                    _statusBuilder.Append("  KIT ").Append(advicePreset);

                _toolDirective.SetText(_statusBuilder);
                _lastDirectiveBase = directive;
                _lastDirectiveHasAdvice = hasAdvice;
                _lastDirectiveAdvicePreset = advicePreset;
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
