using System;
using Hecton8.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Pause Controls Panel")]
    public sealed class PauseControlsPanel : MonoBehaviour
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.8f);
        private static readonly Color RuleColor = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color RowBg = new Color(0.05f, 0.12f, 0.14f, 0.62f);
        private static readonly Color BindingBg = new Color(0.08f, 0.18f, 0.2f, 0.75f);
        private static readonly Color LabelColor = new Color(0.8f, 0.95f, 0.92f, 0.92f);
        private static readonly Color BindingColor = new Color(0.46f, 0.98f, 0.94f, 0.95f);
        private static readonly Color HintColor = new Color(0.58f, 0.78f, 0.74f, 0.72f);
        private static readonly Color SelectionColor = new Color(0.46f, 0.98f, 0.94f, 0.9f);

        [Serializable]
        private sealed class RebindRow
        {
            public string label = "Action";
            public string actionMap = "Player";
            public string actionName = "Interact";
            public int bindingIndex;
            public TextMeshProUGUI labelText;
            public TextMeshProUGUI bindingText;
            public GameObject selectedIndicator;
        }

        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset bindingFont;
        [SerializeField] private bool saveAfterRowReset = true;

        private RebindRow[] _rows = Array.Empty<RebindRow>();
        private bool _built;
        private bool _subscribed;
        private int _selectedIndex;
        private TextMeshProUGUI _statusText;
        private Image _statusBackground;
        private Image[] _rowBackgrounds = Array.Empty<Image>();
        private Image[] _rowAccentBars = Array.Empty<Image>();
        private Image[] _bindingBackgrounds = Array.Empty<Image>();

        private bool IsActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            pauseMenu != null &&
            pauseMenu.IsSettingsOpen;

        private void Awake()
        {
            if (pauseMenu == null)
                pauseMenu = GetComponentInParent<PauseMenuController>();
            labelFont = ResolveReadableFont(labelFont);
            if (bindingFont == null)
                bindingFont = labelFont;
            else
                bindingFont = ResolveReadableFont(bindingFont);

            _rows = BuildDefaultRows();
            EnsureBuilt();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshAllBindingsNow();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(PauseMenuController owner, TMP_FontAsset labels, TMP_FontAsset bindings)
        {
            pauseMenu = owner;
            labelFont = ResolveReadableFont(labels);
            bindingFont = ResolveReadableFont(bindings != null ? bindings : labelFont);
        }

        public void RefreshAllBindingsNow()
        {
            EnsureBuilt();
            RefreshLabels();
            RefreshSelectionVisuals();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            InputManager input = InputManager.Instance;
            RebindingManager rebinding = RebindingManager.Instance;
            if (input == null || rebinding == null)
                return;

            input.OnNavigate += HandleNavigate;
            input.OnSubmit += HandleSubmit;
            input.OnTabNext += HandleTabNext;
            input.OnTabPrevious += HandleTabPrevious;

            rebinding.OnRebindStarted += HandleRebindStarted;
            rebinding.OnRebindCompleted += HandleRebindCompleted;
            rebinding.OnRebindCanceled += HandleRebindCanceled;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            InputManager input = InputManager.Instance;
            if (input != null)
            {
                input.OnNavigate -= HandleNavigate;
                input.OnSubmit -= HandleSubmit;
                input.OnTabNext -= HandleTabNext;
                input.OnTabPrevious -= HandleTabPrevious;
            }

            RebindingManager rebinding = RebindingManager.Instance;
            if (rebinding != null)
            {
                rebinding.OnRebindStarted -= HandleRebindStarted;
                rebinding.OnRebindCompleted -= HandleRebindCompleted;
                rebinding.OnRebindCanceled -= HandleRebindCanceled;
            }

            _subscribed = false;
        }

        private void HandleNavigate(Vector2 direction)
        {
            if (!IsActive) return;
            if (_rows.Length == 0) return;
            if (RebindingManager.Instance != null && RebindingManager.Instance.IsRebinding) return;

            int delta = 0;
            if (direction.y > 0.35f) delta = -1;
            else if (direction.y < -0.35f) delta = 1;

            if (delta == 0) return;
            _selectedIndex = WrapIndex(_selectedIndex + delta, _rows.Length);
            RefreshSelectionVisuals();
            UpdateStatusForSelected();
        }

        private void HandleSubmit()
        {
            if (!IsActive) return;
            if (_rows.Length == 0) return;
            if (RebindingManager.Instance == null || RebindingManager.Instance.IsRebinding) return;

            RebindRow row = _rows[_selectedIndex];
            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                SetStatus($"ACTION NOT FOUND: {row.actionMap}/{row.actionName}");
                return;
            }

            int bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
            if (bindingIndex < 0)
            {
                SetStatus($"NO REBINDABLE BINDING: {row.label}");
                return;
            }

            bool started = RebindingManager.Instance.StartInteractiveRebind(
                row.actionName,
                row.actionMap,
                bindingIndex,
                expectedControlType: null,
                cancelPath: "<Keyboard>/escape",
                excludedControlPaths: new[] { "<Pointer>/position", "<Pointer>/delta" });

            if (!started)
                SetStatus($"FAILED TO START: {row.label}");
        }

        private void HandleTabNext()
        {
            if (!IsActive) return;
            if (RebindingManager.Instance != null && RebindingManager.Instance.IsRebinding) return;
            ResetSelectedBinding();
        }

        private void HandleTabPrevious()
        {
            if (!IsActive) return;
            if (RebindingManager.Instance == null || RebindingManager.Instance.IsRebinding) return;

            RebindingManager.Instance.ClearOverrides();
            RefreshAllBindingsNow();
            SetStatus("ALL BINDINGS RESET TO DEFAULTS.");
        }

        private void HandleRebindStarted(string actionName, string actionMap, int bindingIndex)
        {
            if (!IsActive) return;
            SetStatus($"PRESS A KEY... [{actionMap}/{actionName}]",
                new Color(0.82f, 0.98f, 1f, 0.96f),
                new Color(0.08f, 0.22f, 0.34f, 0.9f));
        }

        private void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            RefreshAllBindingsNow();
            if (!IsActive) return;
            SetStatus($"{actionName}: {display}",
                new Color(0.76f, 0.98f, 0.94f, 0.96f),
                new Color(0.08f, 0.2f, 0.18f, 0.88f));
        }

        private void HandleRebindCanceled(string actionName, string actionMap, int bindingIndex)
        {
            RefreshAllBindingsNow();
            if (!IsActive) return;
            UpdateStatusForSelected();
        }

        private void ResetSelectedBinding()
        {
            if (_rows.Length == 0 || InputManager.Instance == null)
                return;

            RebindRow row = _rows[_selectedIndex];
            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                SetStatus($"ACTION NOT FOUND: {row.actionMap}/{row.actionName}");
                return;
            }

            int bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
            if (bindingIndex < 0)
            {
                SetStatus($"NO REBINDABLE BINDING: {row.label}");
                return;
            }

            action.RemoveBindingOverride(bindingIndex);
            if (saveAfterRowReset && RebindingManager.Instance != null)
                RebindingManager.Instance.SaveOverrides();

            RefreshRowBinding(row);
            UpdateStatusForSelected();
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

            CreateRule(self, -48f);

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 16f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -16f), new Vector2(-18f, 24f));
            title.color = BindingColor;
            title.SetText("CONTROL MATRIX");

            TextMeshProUGUI hint = CreateText(self, "Hint", labelFont, 10f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -16f), new Vector2(-18f, 24f));
            hint.color = HintColor;
            hint.SetText("SUBMIT = rebind  |  TAB NEXT = reset one  |  TAB PREV = reset all");

            RectTransform listRoot = CreateRect(self, "Rows");
            Stretch(listRoot, 18f, 18f, 58f, 66f);

            _rowBackgrounds = new Image[_rows.Length];
            _rowAccentBars = new Image[_rows.Length];
            _bindingBackgrounds = new Image[_rows.Length];

            const float rowHeight = 28f;
            const float rowGap = 5f;
            for (int i = 0; i < _rows.Length; i++)
            {
                RebindRow row = _rows[i];

                RectTransform rowRoot = CreateRect(listRoot, $"Row_{row.actionName}");
                rowRoot.anchorMin = new Vector2(0f, 1f);
                rowRoot.anchorMax = new Vector2(1f, 1f);
                rowRoot.pivot = new Vector2(0.5f, 1f);
                rowRoot.anchoredPosition = new Vector2(0f, -i * (rowHeight + rowGap));
                rowRoot.sizeDelta = new Vector2(0f, rowHeight);

                Image rowBg = EnsureImage(rowRoot.gameObject);
                rowBg.color = RowBg;
                rowBg.raycastTarget = false;
                _rowBackgrounds[i] = rowBg;

                RectTransform accent = CreateRect(rowRoot, $"Accent_{row.actionName}");
                Anchor(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(4f, 0f));
                Image accentImg = EnsureImage(accent.gameObject);
                accentImg.color = new Color(0.18f, 0.32f, 0.34f, 0.78f);
                accentImg.raycastTarget = false;
                _rowAccentBars[i] = accentImg;

                RectTransform selected = CreateRect(rowRoot, $"Selected_{row.actionName}");
                Anchor(selected, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(3f, 0f));
                Image selImg = EnsureImage(selected.gameObject);
                selImg.color = SelectionColor;
                selImg.raycastTarget = false;

                TextMeshProUGUI label = CreateText(rowRoot, $"Label_{row.actionName}", labelFont, 11.5f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.56f, 1f), new Vector2(14f, 0f), new Vector2(-12f, 0f));
                label.color = LabelColor;

                RectTransform bindingBox = CreateRect(rowRoot, $"BindingBox_{row.actionName}");
                Anchor(bindingBox, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(176f, 22f));
                Image bindingBg = EnsureImage(bindingBox.gameObject);
                bindingBg.color = BindingBg;
                bindingBg.raycastTarget = false;
                _bindingBackgrounds[i] = bindingBg;

                TextMeshProUGUI binding = CreateText(bindingBox, $"Binding_{row.actionName}", bindingFont, 11f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(binding.rectTransform, 0f, 0f, 0f, 0f);
                binding.color = BindingColor;

                row.labelText = label;
                row.bindingText = binding;
                row.selectedIndicator = selected.gameObject;
            }

            RectTransform statusRoot = CreateRect(self, "Status");
            Anchor(statusRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 18f), new Vector2(-18f, 32f));
            _statusBackground = EnsureImage(statusRoot.gameObject);
            _statusBackground.color = new Color(0.05f, 0.1f, 0.12f, 0.82f);
            _statusBackground.raycastTarget = false;

            _statusText = CreateText(statusRoot, "StatusText", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(_statusText.rectTransform, 12f, 12f, 0f, 0f);
            _statusText.color = HintColor;

            _built = true;
        }

        private void RefreshLabels()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                RebindRow row = _rows[i];
                if (row.labelText != null)
                    row.labelText.SetText(row.label);
            }
        }

        private void RefreshAllBindings()
        {
            for (int i = 0; i < _rows.Length; i++)
                RefreshRowBinding(_rows[i]);
        }

        private void RefreshRowBinding(RebindRow row)
        {
            if (row.bindingText == null || InputManager.Instance == null)
                return;

            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            int bindingIndex = action != null ? ResolveBindingIndex(action, row.bindingIndex) : -1;
            if (action == null || bindingIndex < 0)
            {
                row.bindingText.SetText("--");
                return;
            }

            string binding = action.GetBindingDisplayString(bindingIndex);
            row.bindingText.SetText(string.IsNullOrEmpty(binding) ? "--" : binding);
        }

        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].selectedIndicator != null)
                    _rows[i].selectedIndicator.SetActive(i == _selectedIndex);

                if (_rowBackgrounds[i] != null)
                    _rowBackgrounds[i].color = i == _selectedIndex ? new Color(0.08f, 0.18f, 0.2f, 0.82f) : RowBg;

                if (_rowAccentBars[i] != null)
                    _rowAccentBars[i].color = i == _selectedIndex ? new Color(0.46f, 0.98f, 0.94f, 0.96f) : new Color(0.18f, 0.32f, 0.34f, 0.78f);

                if (_bindingBackgrounds[i] != null)
                    _bindingBackgrounds[i].color = i == _selectedIndex ? new Color(0.1f, 0.24f, 0.28f, 0.86f) : BindingBg;
            }
        }

        private void UpdateStatusForSelected()
        {
            if (_rows.Length == 0)
            {
                SetStatus("NO BINDINGS CONFIGURED.");
                return;
            }

            RebindRow row = _rows[_selectedIndex];
            string binding = "--";
            if (InputManager.Instance != null)
            {
                InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
                if (action != null)
                {
                    int bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
                    if (bindingIndex >= 0)
                    {
                        binding = action.GetBindingDisplayString(bindingIndex);
                        if (string.IsNullOrEmpty(binding))
                            binding = "--";
                    }
                }
            }

            SetStatus($"REBIND: {row.label} [{binding}]  |  TAB NEXT = RESET ONE  |  TAB PREV = RESET ALL");
        }

        private void SetStatus(string value)
        {
            SetStatus(value, HintColor, new Color(0.05f, 0.1f, 0.12f, 0.82f));
        }

        private void SetStatus(string value, Color textColor, Color backgroundColor)
        {
            if (_statusText != null)
            {
                _statusText.SetText(value);
                _statusText.color = textColor;
            }

            if (_statusBackground != null)
                _statusBackground.color = backgroundColor;
        }

        private static int ResolveBindingIndex(InputAction action, int preferredIndex)
        {
            if (action == null || action.bindings.Count == 0)
                return -1;

            if (preferredIndex >= 0 &&
                preferredIndex < action.bindings.Count &&
                !action.bindings[preferredIndex].isComposite &&
                !action.bindings[preferredIndex].isPartOfComposite)
            {
                return preferredIndex;
            }

            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
                    return i;
            }

            return -1;
        }

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0) return 0;
            if (value >= max) return 0;
            if (value < 0) return max - 1;
            return value;
        }

        private static RebindRow[] BuildDefaultRows()
        {
            return new[]
            {
                MakeRow("LOOK", "Player", "Look", 0),
                MakeRow("JUMP", "Player", "Jump", 0),
                MakeRow("SPRINT", "Player", "Sprint", 0),
                MakeRow("INTERACT", "Player", "Interact", 0),
                MakeRow("FLASHLIGHT", "Player", "Flashlight", 0),
                MakeRow("PDA", "Player", "PDA", 0),
                MakeRow("TOOL SLOT 1", "Player", "ToolSlot1", 0),
                MakeRow("TOOL SLOT 2", "Player", "ToolSlot2", 0),
                MakeRow("TOOL SLOT 3", "Player", "ToolSlot3", 0),
                MakeRow("TOOL SLOT 4", "Player", "ToolSlot4", 0),
                MakeRow("PRIMARY ACTION", "Player", "PrimaryAction", 0),
                MakeRow("SECONDARY ACTION", "Player", "SecondaryAction", 0),
                MakeRow("INVENTORY", "Player", "Inventory", 0),
                MakeRow("UI SUBMIT", "UI", "Submit", 0),
                MakeRow("UI CANCEL", "UI", "Cancel", 0)
            };
        }

        private static RebindRow MakeRow(string label, string map, string action, int bindingIndex)
        {
            return new RebindRow
            {
                label = label,
                actionMap = map,
                actionName = action,
                bindingIndex = bindingIndex
            };
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

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image EnsureImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
            float size, FontStyles style, TextAlignmentOptions alignment)
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
            text.color = LabelColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
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
            Image img = EnsureImage(rect.gameObject);
            img.color = RuleColor;
            img.raycastTarget = false;
        }

        private static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred)
        {
            if (preferred != null && !IsNumericOnlyFont(preferred))
                return preferred;

            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset candidate = fonts[i];
                if (candidate == null)
                    continue;

                string name = candidate.name;
                if (name.IndexOf("текст", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static bool IsNumericOnlyFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string name = font.name;
            return name.IndexOf("циф", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("digit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
