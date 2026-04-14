using System;
using Hecton8.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Runtime rebinding controller for the PDA "Controls" tab.
    /// Event-driven, no Update polling, and resilient to missing references.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Controls Rebind UI")]
    public sealed class PDAControlsRebindUI : MonoBehaviour
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.8f);
        private static readonly Color RuleColor = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color RowBg = new Color(0.05f, 0.12f, 0.14f, 0.62f);
        private static readonly Color BindingBg = new Color(0.08f, 0.18f, 0.2f, 0.75f);
        private static readonly Color LabelColor = new Color(0.8f, 0.95f, 0.92f, 0.92f);
        private static readonly Color BindingColor = new Color(0.46f, 0.98f, 0.94f, 0.95f);
        private static readonly Color HintColor = new Color(0.58f, 0.78f, 0.74f, 0.72f);
        private static readonly Color SelectionColor = new Color(0.46f, 0.98f, 0.94f, 0.9f);
        private static readonly string[] ExcludedControlPaths =
        {
            "<Pointer>/position",
            "<Pointer>/delta"
        };

        [Serializable]
        public sealed class RebindRow
        {
            [Tooltip("Display label shown in UI for this action.")]
            public string label = "Action";

            [Tooltip("Input action map name (Player/UI).")]
            public string actionMap = "Player";

            [Tooltip("Input action name inside map.")]
            public string actionName = "Interact";

            [Tooltip("Binding index for the action.")]
            public int bindingIndex;

            [Tooltip("Optional text label for action name.")]
            public TextMeshProUGUI labelText;

            [Tooltip("Binding text output (e.g. E, Left Shift, Mouse 0).")]
            public TextMeshProUGUI bindingText;

            [Tooltip("Optional visual indicator for currently selected row.")]
            public GameObject selectedIndicator;
        }

        [Header("References")]
        [SerializeField] private PlayerPDA playerPda;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset bindingFont;

        [Header("Settings")]
        [Tooltip("PDA tab index where controls panel is shown.")]
        [SerializeField] private int controlsTabIndex = 2;

        [Tooltip("Rows shown in controls rebinding panel.")]
        [SerializeField] private RebindRow[] rows = Array.Empty<RebindRow>();

        [Tooltip("Auto-generate a default controls list when rows are empty.")]
        [SerializeField] private bool autoGenerateRowsIfEmpty = true;

        [Tooltip("Auto-resolve row text references by child object naming convention.")]
        [SerializeField] private bool autoResolveRowReferences = true;

        [Tooltip("If true, SaveOverrides is called after per-row reset.")]
        [SerializeField] private bool saveAfterRowReset = true;

        [Header("Status Text")]
        [SerializeField] private string readyPrefix = "Rebind";
        [SerializeField] private string rebindingPrefix = "Press a key...";
        [SerializeField] private string resetHint = "TabNext = reset selected, TabPrevious = reset all";

        private bool _built;
        private int _selectedIndex;
        private bool _subscribed;
        private Image[] _rowBackgrounds = Array.Empty<Image>();
        private Image[] _rowAccentBars = Array.Empty<Image>();
        private Image[] _bindingBackgrounds = Array.Empty<Image>();
        private Image _statusBackground;

        private bool IsControlsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPda != null &&
            playerPda.ActiveTab == controlsTabIndex;

        private void Awake()
        {
            AutoResolveTabIndex();
            if (playerPda == null)
            {
                playerPda = GetComponentInParent<PlayerPDA>();
            }

            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (bindingFont == null)
                bindingFont = labelFont;

            if (rows == null) rows = Array.Empty<RebindRow>();
            EnsureRowsConfigured();
            EnsureBuilt();
            if (autoResolveRowReferences)
            {
                ResolveRowReferencesByName();
            }

            if (_selectedIndex >= rows.Length) _selectedIndex = 0;
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

        private void AutoResolveTabIndex()
        {
            if (gameObject.name.Contains("Controls", StringComparison.OrdinalIgnoreCase))
                controlsTabIndex = 2;
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            var input = InputManager.Instance;
            RebindingManager.TryGetInstance(out RebindingManager rebinding);
            if (input == null || rebinding == null)
                return;

            input.OnNavigate += HandleNavigate;
            input.OnSubmit += HandleSubmit;
            input.OnCancel += HandleCancel;
            input.OnTabNext += HandleTabNext;
            input.OnTabPrevious += HandleTabPrevious;

            rebinding.OnRebindStarted += HandleRebindStarted;
            rebinding.OnRebindCompleted += HandleRebindCompleted;
            rebinding.OnRebindCanceled += HandleRebindCanceled;
            rebinding.OnOverridesLoaded += HandleBindingOverridesChanged;
            rebinding.OnOverridesSaved += HandleBindingOverridesChanged;
            rebinding.OnOverridesCleared += HandleBindingOverridesChanged;

            PDAEvents.OnTabChanged += HandlePdaTabChanged;
            PDAEvents.OnOpened += HandlePdaOpened;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            var input = InputManager.Instance;
            if (input != null)
            {
                input.OnNavigate -= HandleNavigate;
                input.OnSubmit -= HandleSubmit;
                input.OnCancel -= HandleCancel;
                input.OnTabNext -= HandleTabNext;
                input.OnTabPrevious -= HandleTabPrevious;
            }

            RebindingManager.TryGetInstance(out RebindingManager rebinding);
            if (rebinding != null)
            {
                rebinding.OnRebindStarted -= HandleRebindStarted;
                rebinding.OnRebindCompleted -= HandleRebindCompleted;
                rebinding.OnRebindCanceled -= HandleRebindCanceled;
                rebinding.OnOverridesLoaded -= HandleBindingOverridesChanged;
                rebinding.OnOverridesSaved -= HandleBindingOverridesChanged;
                rebinding.OnOverridesCleared -= HandleBindingOverridesChanged;
            }

            PDAEvents.OnTabChanged -= HandlePdaTabChanged;
            PDAEvents.OnOpened -= HandlePdaOpened;

            _subscribed = false;
        }

        private void HandleNavigate(Vector2 direction)
        {
            if (!IsControlsTabActive) return;
            if (rows == null || rows.Length == 0) return;
            if (!RebindingManager.TryGetInstance(out RebindingManager rebinding) || rebinding.IsRebinding) return;

            int delta = 0;
            if (direction.y > 0.35f) delta = -1;
            else if (direction.y < -0.35f) delta = 1;

            if (delta == 0) return;
            MoveSelection(delta);
        }

        private void HandleSubmit()
        {
            if (!IsControlsTabActive) return;
            if (rows == null || rows.Length == 0) return;
            if (!RebindingManager.TryGetInstance(out RebindingManager rebinding) || rebinding.IsRebinding) return;

            if (!TryGetSelectedRow(out RebindRow row, out _))
            {
                SetStatus("No bindings configured.");
                return;
            }

            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                SetStatus($"Action not found: {row.actionMap}/{row.actionName}");
                return;
            }

            int bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
            if (bindingIndex < 0)
            {
                SetStatus($"No rebindable binding: {row.label}");
                return;
            }

            bool started = rebinding.StartInteractiveRebind(
                row.actionName,
                row.actionMap,
                bindingIndex,
                expectedControlType: null,
                cancelPath: "<Keyboard>/escape",
                excludedControlPaths: ExcludedControlPaths);

            if (!started)
            {
                SetStatus($"Failed to start: {row.label}");
            }
        }

        private void HandleCancel()
        {
            if (!PlayerPDA.IsOpen) return;
            if (!RebindingManager.TryGetInstance(out RebindingManager rebinding) || !rebinding.IsRebinding) return;
            rebinding.CancelRebind();
        }

        private void HandleTabNext()
        {
            if (!IsControlsTabActive) return;
            if (rows == null || rows.Length == 0) return;
            if (!RebindingManager.TryGetInstance(out RebindingManager rebinding) || rebinding.IsRebinding) return;

            ResetSelectedBinding();
        }

        private void HandleTabPrevious()
        {
            if (!IsControlsTabActive) return;
            if (!RebindingManager.TryGetInstance(out RebindingManager rebinding) || rebinding.IsRebinding) return;

            rebinding.ClearOverrides();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void HandleRebindStarted(string actionName, string actionMap, int bindingIndex)
        {
            if (!IsControlsTabActive) return;
            SetStatus(
                $"{rebindingPrefix}  [{actionMap}/{actionName}]",
                new Color(0.82f, 0.98f, 1f, 0.96f),
                new Color(0.08f, 0.22f, 0.34f, 0.9f));
        }

        private void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            RefreshAllBindings();
            if (!IsControlsTabActive) return;
            SetStatus(
                $"{actionName}: {display}",
                new Color(0.76f, 0.98f, 0.94f, 0.96f),
                new Color(0.08f, 0.2f, 0.18f, 0.88f));
        }

        private void HandleRebindCanceled(string actionName, string actionMap, int bindingIndex)
        {
            RefreshAllBindings();
            if (!IsControlsTabActive) return;
            UpdateStatusForSelected();
        }

        private void HandlePdaTabChanged(int oldTab, int newTab)
        {
            if (newTab != controlsTabIndex) return;
            RefreshAll();
        }

        private void HandlePdaOpened(int startTab)
        {
            if (startTab != controlsTabIndex) return;
            RefreshAll();
        }

        private void HandleBindingOverridesChanged()
        {
            if (!IsControlsTabActive) return;
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void ResetSelectedBinding()
        {
            if (!TryGetSelectedRow(out RebindRow row, out _))
            {
                SetStatus("No bindings configured.");
                return;
            }

            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                SetStatus($"Action not found: {row.actionMap}/{row.actionName}");
                return;
            }

            int bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
            if (bindingIndex < 0)
            {
                SetStatus($"No rebindable binding: {row.actionName}");
                return;
            }

            action.RemoveBindingOverride(bindingIndex);
            if (saveAfterRowReset)
            {
                if (RebindingManager.TryGetInstance(out RebindingManager rebinding))
                    rebinding.SaveOverrides();
            }

            RefreshRowBinding(row);
            UpdateStatusForSelected();
        }

        private void RefreshAll()
        {
            EnsureBuilt();
            if (autoResolveRowReferences)
            {
                ResolveRowReferencesByName();
            }

            NormalizeSelectedIndex();
            RefreshLabels();
            RefreshSelectionVisuals();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            if (rows == null || rows.Length == 0)
                return;

            bool alreadyHasRowRefs = true;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null ||
                    rows[i].labelText == null ||
                    rows[i].bindingText == null ||
                    rows[i].selectedIndicator == null)
                {
                    alreadyHasRowRefs = false;
                    break;
                }
            }

            if (alreadyHasRowRefs && statusText != null)
            {
                _built = true;
                return;
            }

            ClearChildren(self);

            Image bg = EnsureImage(self.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;

            CreateRule(self, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), -52f);

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            title.color = BindingColor;
            title.SetText("CONTROL MATRIX");

            TextMeshProUGUI hint = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            hint.color = HintColor;
            hint.SetText("SUBMIT = rebind  |  TAB NEXT = reset one  |  TAB PREV = reset all");

            RectTransform listRoot = CreateRect(self, "Rows");
            Anchor(listRoot, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(18f, 72f), new Vector2(-18f, -72f));

            _rowBackgrounds = new Image[rows.Length];
            _rowAccentBars = new Image[rows.Length];
            _bindingBackgrounds = new Image[rows.Length];

            const float rowHeight = 30f;
            const float rowGap = 6f;
            float totalHeight = rows.Length * rowHeight + Mathf.Max(0, rows.Length - 1) * rowGap;
            float startY = -Mathf.Max(0f, (listRoot.rect.height - totalHeight) * 0.5f);

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (row == null)
                    continue;

                RectTransform rowRoot = CreateRect(listRoot, $"Row_{row.actionName}");
                Anchor(rowRoot, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, startY - i * (rowHeight + rowGap)),
                    new Vector2(0f, rowHeight));

                Image rowBg = EnsureImage(rowRoot.gameObject);
                rowBg.color = RowBg;
                rowBg.raycastTarget = false;
                _rowBackgrounds[i] = rowBg;

                RectTransform accent = CreateRect(rowRoot, $"Accent_{row.actionName}");
                Anchor(accent, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0f), new Vector2(4f, 0f));
                Image accentImg = EnsureImage(accent.gameObject);
                accentImg.color = new Color(0.18f, 0.32f, 0.34f, 0.78f);
                accentImg.raycastTarget = false;
                _rowAccentBars[i] = accentImg;

                RectTransform selected = CreateRect(rowRoot, $"Selected_{row.actionName}");
                Anchor(selected, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0f), new Vector2(3f, 0f));
                Image selImg = EnsureImage(selected.gameObject);
                selImg.color = SelectionColor;
                selImg.raycastTarget = false;

                TextMeshProUGUI label = CreateText(rowRoot, $"Label_{row.actionName}",
                    labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f),
                    new Vector2(14f, 0f), new Vector2(-12f, 0f));
                label.color = LabelColor;

                RectTransform bindingBox = CreateRect(rowRoot, $"BindingBox_{row.actionName}");
                Anchor(bindingBox, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-12f, 0f), new Vector2(164f, 22f));
                Image bindingBg = EnsureImage(bindingBox.gameObject);
                bindingBg.color = BindingBg;
                bindingBg.raycastTarget = false;
                _bindingBackgrounds[i] = bindingBg;

                TextMeshProUGUI binding = CreateText(bindingBox, $"Binding_{row.actionName}",
                    bindingFont, 11.5f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(binding.rectTransform, 0f, 0f, 0f, 0f);
                binding.color = BindingColor;

                row.labelText = label;
                row.bindingText = binding;
                row.selectedIndicator = selected.gameObject;
            }

            RectTransform statusRoot = CreateRect(self, "Status");
            Anchor(statusRoot, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(18f, 18f), new Vector2(-18f, 36f));
            Image statusBg = EnsureImage(statusRoot.gameObject);
            statusBg.color = new Color(0.05f, 0.1f, 0.12f, 0.82f);
            statusBg.raycastTarget = false;
            _statusBackground = statusBg;

            statusText = CreateText(statusRoot, "StatusText", labelFont, 11f, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(statusText.rectTransform, 12f, 0f, 12f, 0f);
            statusText.color = HintColor;

            _built = true;
        }

        private void EnsureRowsConfigured()
        {
            if (rows != null && rows.Length > 0) return;
            if (!autoGenerateRowsIfEmpty) return;
            rows = BuildDefaultRows();
        }

        private void NormalizeSelectedIndex()
        {
            if (rows == null || rows.Length == 0)
            {
                _selectedIndex = 0;
                return;
            }

            if (_selectedIndex >= 0 && _selectedIndex < rows.Length && IsBindableRow(rows[_selectedIndex]))
                return;

            _selectedIndex = GetFirstValidRowIndex();
            if (_selectedIndex < 0)
                _selectedIndex = 0;
        }

        private void ResolveRowReferencesByName()
        {
            if (rows == null || rows.Length == 0) return;

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (row == null) continue;

                if (string.IsNullOrWhiteSpace(row.label))
                {
                    row.label = row.actionName;
                }

                string key = row.actionName;
                if (row.labelText == null)
                {
                    Transform t = FindDeepChild(transform, $"Label_{key}");
                    if (t != null) row.labelText = t.GetComponent<TextMeshProUGUI>();
                }

                if (row.bindingText == null)
                {
                    Transform t = FindDeepChild(transform, $"Binding_{key}");
                    if (t != null) row.bindingText = t.GetComponent<TextMeshProUGUI>();
                }

                if (row.selectedIndicator == null)
                {
                    Transform t = FindDeepChild(transform, $"Selected_{key}");
                    if (t != null) row.selectedIndicator = t.gameObject;
                }
            }
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null || string.IsNullOrEmpty(targetName)) return null;
            if (parent.name == targetName) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform result = FindDeepChild(child, targetName);
                if (result != null) return result;
            }

            return null;
        }

        private static RebindRow[] BuildDefaultRows()
        {
            return new[]
            {
                MakeRow("Look", "Player", "Look", 5),
                MakeRow("Jump", "Player", "Jump", 6),
                MakeRow("Sprint", "Player", "Sprint", 9),
                MakeRow("Interact", "Player", "Interact", 11),
                MakeRow("Flashlight", "Player", "Flashlight", 13),
                MakeRow("PDA", "Player", "PDA", 15),
                MakeRow("Tool Slot 1", "Player", "ToolSlot1", 17),
                MakeRow("Tool Slot 2", "Player", "ToolSlot2", 18),
                MakeRow("Tool Slot 3", "Player", "ToolSlot3", 19),
                MakeRow("Tool Slot 4", "Player", "ToolSlot4", 20),
                MakeRow("Primary Action", "Player", "PrimaryAction", 21),
                MakeRow("Secondary Action", "Player", "SecondaryAction", 23),
                MakeRow("Inventory", "Player", "Inventory", 28),
                MakeRow("UI Navigate", "UI", "Navigate", 5),
                MakeRow("UI Submit", "UI", "Submit", 7),
                MakeRow("UI Cancel", "UI", "Cancel", 10)
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

        private void RefreshLabels()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (!IsBindableRow(row))
                    continue;

                if (row.labelText != null)
                {
                    row.labelText.text = row.label;
                }
            }
        }

        private void RefreshAllBindings()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (!IsBindableRow(row))
                    continue;

                RefreshRowBinding(row);
            }
        }

        private void RefreshRowBinding(RebindRow row)
        {
            if (!IsBindableRow(row))
                return;

            if (row.bindingText == null) return;

            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            int bindingIndex = action != null ? ResolveBindingIndex(action, row.bindingIndex) : -1;
            if (action == null || bindingIndex < 0)
            {
                row.bindingText.text = "--";
                return;
            }

            row.bindingText.text = GetBindingDisplaySafe(action, bindingIndex);
        }

        private void RefreshSelectionVisuals()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (!IsBindableRow(row))
                    continue;

                GameObject indicator = row.selectedIndicator;
                if (indicator != null)
                {
                    indicator.SetActive(i == _selectedIndex);
                }

                if (_rowBackgrounds != null && i < _rowBackgrounds.Length && _rowBackgrounds[i] != null)
                {
                    _rowBackgrounds[i].color = i == _selectedIndex
                        ? new Color(0.08f, 0.18f, 0.2f, 0.82f)
                        : RowBg;
                }

                if (_rowAccentBars != null && i < _rowAccentBars.Length && _rowAccentBars[i] != null)
                {
                    _rowAccentBars[i].color = i == _selectedIndex
                        ? new Color(0.46f, 0.98f, 0.94f, 0.96f)
                        : new Color(0.18f, 0.32f, 0.34f, 0.78f);
                }

                if (_bindingBackgrounds != null && i < _bindingBackgrounds.Length && _bindingBackgrounds[i] != null)
                {
                    _bindingBackgrounds[i].color = i == _selectedIndex
                        ? new Color(0.1f, 0.24f, 0.28f, 0.86f)
                        : BindingBg;
                }
            }
        }

        private void UpdateStatusForSelected()
        {
            if (!TryGetSelectedRow(out RebindRow row, out _))
            {
                SetStatus("No bindings configured.");
                return;
            }

            string binding = "--";
            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            if (action != null)
            {
                int bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
                if (bindingIndex >= 0)
                {
                    binding = GetBindingDisplaySafe(action, bindingIndex);
                }
            }

            SetStatus($"{readyPrefix}: {row.label} [{binding}]  |  {resetHint}");
        }

        private bool TryGetSelectedRow(out RebindRow row, out int rowIndex)
        {
            row = null;
            rowIndex = -1;

            if (rows == null || rows.Length == 0)
                return false;

            NormalizeSelectedIndex();

            if (_selectedIndex >= 0 && _selectedIndex < rows.Length)
            {
                row = rows[_selectedIndex];
                if (IsBindableRow(row))
                {
                    rowIndex = _selectedIndex;
                    return true;
                }
            }

            rowIndex = GetFirstValidRowIndex();
            if (rowIndex < 0)
                return false;

            _selectedIndex = rowIndex;
            row = rows[rowIndex];
            return IsBindableRow(row);
        }

        private static bool IsBindableRow(RebindRow row)
        {
            return row != null &&
                   !string.IsNullOrWhiteSpace(row.actionMap) &&
                   !string.IsNullOrWhiteSpace(row.actionName);
        }

        private int GetFirstValidRowIndex()
        {
            if (rows == null || rows.Length == 0)
                return -1;

            for (int i = 0; i < rows.Length; i++)
            {
                if (IsBindableRow(rows[i]))
                    return i;
            }

            return -1;
        }

        private void MoveSelection(int delta)
        {
            if (rows == null || rows.Length == 0)
                return;

            NormalizeSelectedIndex();
            int current = _selectedIndex;

            for (int i = 0; i < rows.Length; i++)
            {
                current = WrapIndex(current + delta, rows.Length);
                if (IsBindableRow(rows[current]))
                {
                    _selectedIndex = current;
                    RefreshSelectionVisuals();
                    UpdateStatusForSelected();
                    return;
                }
            }
        }

        private static int ResolveBindingIndex(InputAction action, int preferredIndex)
        {
            if (action == null)
                return -1;

            int bindingCount;
            try
            {
                bindingCount = action.bindings.Count;
            }
            catch
            {
                return -1;
            }

            if (bindingCount == 0)
                return -1;

            try
            {
                if (preferredIndex >= 0 &&
                    preferredIndex < bindingCount &&
                    !action.bindings[preferredIndex].isComposite &&
                    !action.bindings[preferredIndex].isPartOfComposite)
                {
                    return preferredIndex;
                }
            }
            catch
            {
                return -1;
            }

            for (int i = 0; i < bindingCount; i++)
            {
                try
                {
                    if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
                        return i;
                }
                catch
                {
                    return -1;
                }
            }

            return -1;
        }

        private static string GetBindingDisplaySafe(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0)
                return "--";

            return InputManager.TryGetBindingDisplayStringSafe(action, bindingIndex, out string binding) &&
                   !string.IsNullOrEmpty(binding)
                ? binding
                : "--";
        }

        private void SetStatus(string value)
        {
            SetStatus(value, HintColor, new Color(0.05f, 0.1f, 0.12f, 0.82f));
        }

        private void SetStatus(string value, Color textColor, Color backgroundColor)
        {
            if (statusText != null)
            {
                statusText.text = value;
                statusText.color = textColor;
            }

            if (_statusBackground != null)
            {
                _statusBackground.color = backgroundColor;
            }
        }

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0) return 0;
            if (value >= max) return 0;
            if (value < 0) return max - 1;
            return value;
        }

        public void Configure(PlayerPDA pda, TextMeshProUGUI statusOutput, int tabIndex)
        {
            playerPda = pda;
            statusText = statusOutput;
            controlsTabIndex = tabIndex;
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

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
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

        private static void CreateRule(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image img = EnsureImage(rect.gameObject);
            img.color = RuleColor;
            img.raycastTarget = false;
        }
    }
}
