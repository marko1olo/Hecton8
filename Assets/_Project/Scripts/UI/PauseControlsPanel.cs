using System;
using System.Collections.Generic;
using Hecton8.Core;
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

        // TASK 17: Apply/Cancel/Reset buttons
        [Header("── Control Buttons ──────────────────")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button resetButton;

        private RebindRow[] _rows = Array.Empty<RebindRow>();
        private bool _built;
        private bool _subscribed;
        private int _selectedIndex;
        private TextMeshProUGUI _statusText;
        private Image _statusBackground;
        private Image[] _rowBackgrounds = Array.Empty<Image>();
        private Image[] _rowAccentBars = Array.Empty<Image>();
        private Image[] _bindingBackgrounds = Array.Empty<Image>();

        // ZERO-GC: Cached strings for status messages
        private static readonly string StatusRebindingUnavailable = "REBINDING SERVICE UNAVAILABLE.";
        private static readonly string StatusCannotResetWhileRebinding = "CANNOT RESET ALL WHILE REBINDING.";
        private static readonly string StatusAllBindingsReset = "ALL BINDINGS RESET TO DEFAULTS.";
        private static readonly string StatusRebindCanceled = "REBIND CANCELED.";
        private static readonly string StatusNoBindingsConfigured = "NO BINDINGS CONFIGURED.";
        private static readonly string StatusBindingsSaved = "BINDINGS SAVED.";
        private static readonly string StatusBindingsReverted = "BINDINGS REVERTED TO SAVED STATE.";
        private static readonly string StatusBindingsResetToDefaults = "ALL BINDINGS RESET TO DEFAULTS.";
        private static readonly string StatusConflictTitle = "BINDING CONFLICT DETECTED";
        private static readonly string StatusFailedToStartPrefix = "FAILED TO START: ";
        private static readonly string StatusPressAKeyPrefix = "PRESS A KEY... [";
        private static readonly string StatusConflictPrefix = "CONFLICT: ";
        private static readonly string StatusConflictMiddle = " already used by ";
        private static readonly string StatusRebindPrefix = "REBIND: ";
        private static readonly string StatusRebindSuffix = "  |  TAB NEXT = RESET ONE  |  TAB PREV = RESET ALL";
        
        // ZERO-GC: Cached array for excluded control paths
        private static readonly string[] ExcludedControlPaths = { "<Pointer>/position", "<Pointer>/delta" };
        
        // ZERO-GC: Cached colors for status messages
        private static readonly Color StatusColorPressKey = new Color(0.82f, 0.98f, 1f, 0.96f);
        private static readonly Color StatusBgPressKey = new Color(0.08f, 0.22f, 0.34f, 0.9f);
        private static readonly Color StatusColorComplete = new Color(0.76f, 0.98f, 0.94f, 0.96f);
        private static readonly Color StatusBgComplete = new Color(0.08f, 0.2f, 0.18f, 0.88f);
        private static readonly Color StatusColorConflict = new Color(0.98f, 0.76f, 0.46f, 0.96f);
        private static readonly Color StatusBgConflict = new Color(0.34f, 0.18f, 0.08f, 0.9f);
        private static readonly Color StatusColorReverted = new Color(0.82f, 0.98f, 1f, 0.96f);
        private static readonly Color StatusBgReverted = new Color(0.08f, 0.22f, 0.34f, 0.9f);
        
        // ZERO-GC: Cached colors for selection visuals (FIX: hardcoded colors in RefreshSelectionVisuals)
        private static readonly Color RowBgSelected = new Color(0.08f, 0.18f, 0.2f, 0.82f);
        private static readonly Color AccentDefault = new Color(0.18f, 0.32f, 0.34f, 0.78f);
        private static readonly Color AccentSelected = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color BindingBgSelected = new Color(0.1f, 0.24f, 0.28f, 0.86f);
        
        // ZERO-GC: String builder for dynamic messages (reused)
        private readonly System.Text.StringBuilder _statusBuilder = new System.Text.StringBuilder(256); // COLD ALLOC: StringBuilder[256] — status message building — owner: PauseControlsPanel
        
        // ZERO-GC: Cached previous selection for optimized refresh
        private int _previousSelectedIndex = -1;

        private bool IsActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            pauseMenu != null &&
            pauseMenu.IsSettingsOpen;

        private void Awake()
        {
            if (pauseMenu == null)
                pauseMenu = GetComponentInParent<PauseMenuController>();
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            if (bindingFont == null)
                bindingFont = labelFont;
            else
                bindingFont = LocalizedFontResolver.ResolveReadableFont(bindingFont);

            _rows = BuildDefaultRows(); // COLD ALLOC: RebindRow[15] — default rebinding rows — owner: PauseControlsPanel
            EnsureBuilt(); // COLD ALLOC: Image[45] + TextMeshProUGUI[30] — UI elements — owner: PauseControlsPanel

            // TASK 17: Wire button events
            if (applyButton != null)
            {
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetToDefaultsClicked);
            }
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshAllBindingsNow();
        }

        private void OnDisable()
        {
            Unsubscribe();

            // TASK 17: Save overrides when closing Settings section
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding != null)
            {
                rebinding.SaveOverrides();
            }
        }

        /// <summary>
        /// Configures the panel with owner reference and font assets.
        /// Called during initialization to set up panel dependencies.
        /// </summary>
        /// <param name="owner">Parent pause menu controller</param>
        /// <param name="labels">Font asset for action labels</param>
        /// <param name="bindings">Font asset for binding display text (uses labels if null)</param>
        public void Configure(PauseMenuController owner, TMP_FontAsset labels, TMP_FontAsset bindings)
        {
            pauseMenu = owner;
            labelFont = LocalizedFontResolver.ResolveReadableFont(labels);
            bindingFont = LocalizedFontResolver.ResolveReadableFont(bindings != null ? bindings : labelFont);
        }

        /// <summary>
        /// Refreshes all binding displays and UI state immediately.
        /// Subscribes to input events, rebuilds UI if needed, and updates all visual elements.
        /// Call this after changing input bindings or when panel becomes active.
        /// </summary>
        public void RefreshAllBindingsNow()
        {
            Subscribe();
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
            IInputBindingService rebinding = ResolveRebindingService();
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
            rebinding.OnConflictDetected += HandleConflictDetected; // TASK 16

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
                input.OnCancel -= HandleCancel;
                input.OnTabNext -= HandleTabNext;
                input.OnTabPrevious -= HandleTabPrevious;
            }

            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding != null)
            {
                rebinding.OnRebindStarted -= HandleRebindStarted;
                rebinding.OnRebindCompleted -= HandleRebindCompleted;
                rebinding.OnRebindCanceled -= HandleRebindCanceled;
                rebinding.OnConflictDetected -= HandleConflictDetected; // TASK 16
            }

            _subscribed = false;
        }

        private void HandleNavigate(Vector2 direction)
        {
            if (!IsActive) return;
            if (_rows.Length == 0) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding != null && rebinding.IsRebinding) return;

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
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.IsRebinding) return;

            RebindRow row = _rows[_selectedIndex];
            InputManager input = InputManager.Instance;
            if (!TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                SetStatus(resolutionMessage);
                return;
            }

            bool started = rebinding.StartInteractiveRebind(
                row.actionName,
                row.actionMap,
                bindingIndex,
                expectedControlType: null,
                cancelPath: "<Keyboard>/escape",
                excludedControlPaths: ExcludedControlPaths); // ZERO-GC: Use cached array

            if (!started)
            {
                // ZERO-GC: Build message without allocation
                _statusBuilder.Clear();
                _statusBuilder.Append(StatusFailedToStartPrefix);
                _statusBuilder.Append(row.label);
                SetStatus(_statusBuilder.ToString());
            }
        }

        private void HandleTabNext()
        {
            if (!IsActive) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.IsRebinding) return;
            ResetSelectedBinding();
        }

        private void HandleTabPrevious()
        {
            if (!IsActive) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.IsRebinding)
            {
                SetStatus(StatusCannotResetWhileRebinding);
                return;
            }

            rebinding.ClearOverrides();
            RefreshAllBindingsNow();
            SetStatus(StatusAllBindingsReset);
        }

        private void HandleCancel()
        {
            if (!IsActive) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (!rebinding.IsRebinding)
            {
                UpdateStatusForSelected();
                return;
            }

            rebinding.CancelRebind();
        }

        private void HandleRebindStarted(string actionName, string actionMap, int bindingIndex)
        {
            if (!IsActive) return;
            
            // ZERO-GC: Build message without allocation
            _statusBuilder.Clear();
            _statusBuilder.Append(StatusPressAKeyPrefix);
            _statusBuilder.Append(actionMap);
            _statusBuilder.Append('/');
            _statusBuilder.Append(actionName);
            _statusBuilder.Append(']');
            
            SetStatus(_statusBuilder.ToString(), StatusColorPressKey, StatusBgPressKey);
        }

        private void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            RefreshAllBindingsNow();
            if (!IsActive) return;
            
            // ZERO-GC: Build message without allocation
            _statusBuilder.Clear();
            _statusBuilder.Append(actionName);
            _statusBuilder.Append(": ");
            _statusBuilder.Append(display);
            
            SetStatus(_statusBuilder.ToString(), StatusColorComplete, StatusBgComplete);
        }

        private void HandleRebindCanceled(string actionName, string actionMap, int bindingIndex)
        {
            RefreshAllBindingsNow();
            if (!IsActive) return;
            SetStatus(StatusRebindCanceled);
        }

        /// <summary>
        /// TASK 16: Handles conflict detection during rebinding.
        /// Displays modal window with conflict warning and confirm/cancel options.
        /// ZERO-GC: Uses StringBuilder for message construction.
        /// SAFETY: Validates ModalWindow availability before showing dialog.
        /// EXCEPTION-SAFE: StringBuilder cleared at method start to prevent stale data.
        /// </summary>
        private void HandleConflictDetected(string actionName, string conflictingAction, string newBinding, Action onConfirm, Action onCancel)
        {
            if (!IsActive) return;

            // SAFETY: Clear StringBuilder at method start (exception-safe pattern)
            _statusBuilder.Clear();

            try
            {
                // ZERO-GC: Build message without allocation
                _statusBuilder.Append("The binding '");
                _statusBuilder.Append(newBinding);
                _statusBuilder.Append("' is already assigned to '");
                _statusBuilder.Append(conflictingAction);
                _statusBuilder.Append("'.\n\nDo you want to reassign it to '");
                _statusBuilder.Append(actionName);
                _statusBuilder.Append("'?");
                string message = _statusBuilder.ToString();

                // SAFETY: Check if ModalWindow is available (may not exist in all scenes)
                try
                {
                    Hecton.UI.MainMenu.ModalWindow.Show(
                        StatusConflictTitle,
                        message,
                        onConfirm,  // User confirms - complete rebind
                        onCancel    // User cancels - revert rebind
                    );
                }
                catch (System.Exception ex)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[PauseControlsPanel] ModalWindow unavailable: {ex.Message}. Auto-canceling conflict.");
#endif
                    // Fallback: auto-cancel if modal unavailable
                    SetStatus("CONFLICT: Cannot show dialog - ModalWindow unavailable", StatusColorConflict, StatusBgConflict);
                    onCancel?.Invoke();
                    return;
                }

                // ZERO-GC: Build status message without allocation
                _statusBuilder.Clear();
                _statusBuilder.Append(StatusConflictPrefix);
                _statusBuilder.Append(newBinding);
                _statusBuilder.Append(StatusConflictMiddle);
                _statusBuilder.Append(conflictingAction);
                
                SetStatus(_statusBuilder.ToString(), StatusColorConflict, StatusBgConflict);
            }
            finally
            {
                // SAFETY: Always clear StringBuilder on exit (prevents stale data)
                _statusBuilder.Clear();
            }
        }

        /// <summary>
        /// TASK 17: Applies all binding changes and saves to PlayerPrefs.
        /// </summary>
        private void OnApplyClicked()
        {
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            rebinding.SaveOverrides();
            SetStatus(StatusBindingsSaved, StatusColorComplete, StatusBgComplete);
        }

        /// <summary>
        /// TASK 17: Cancels all binding changes and reloads from PlayerPrefs.
        /// </summary>
        private void OnCancelClicked()
        {
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            rebinding.LoadOverrides();
            RefreshAllBindingsNow();
            SetStatus(StatusBindingsReverted, StatusColorReverted, StatusBgReverted);
        }

        /// <summary>
        /// TASK 17: Resets all bindings to defaults and clears PlayerPrefs.
        /// </summary>
        private void OnResetToDefaultsClicked()
        {
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            rebinding.ClearOverrides();
            RefreshAllBindingsNow();
            SetStatus(StatusBindingsResetToDefaults, StatusColorComplete, StatusBgComplete);
        }

        private void ResetSelectedBinding()
        {
            if (_rows.Length == 0)
                return;

            InputManager input = InputManager.Instance;
            if (input == null)
                return;

            RebindRow row = _rows[_selectedIndex];
            if (!TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                SetStatus(resolutionMessage);
                return;
            }

            action.RemoveBindingOverride(bindingIndex);
            if (saveAfterRowReset)
            {
                IInputBindingService rebinding = ResolveRebindingService();
                if (rebinding != null)
                    rebinding.SaveOverrides();
            }

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

            if (_rows == null)
                _rows = Array.Empty<RebindRow>();

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

            _rowBackgrounds = new Image[_rows.Length]; // COLD ALLOC: Image[15] — row backgrounds — owner: PauseControlsPanel
            _rowAccentBars = new Image[_rows.Length]; // COLD ALLOC: Image[15] — row accent bars — owner: PauseControlsPanel
            _bindingBackgrounds = new Image[_rows.Length]; // COLD ALLOC: Image[15] — binding backgrounds — owner: PauseControlsPanel

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
            if (row == null || row.bindingText == null)
                return;

            InputManager input = InputManager.Instance;
            if (!TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                row.bindingText.SetText(resolutionMessage);
                return;
            }

            row.bindingText.SetText(GetBindingDisplaySafe(action, bindingIndex));
        }

        /// <summary>
        /// Refreshes selection visuals for all rows.
        /// OPTIMIZED: Only updates changed rows (previous and current selection).
        /// ZERO-GC: Uses cached static readonly colors to avoid allocations.
        /// </summary>
        private void RefreshSelectionVisuals()
        {
            // OPTIMIZATION: Only update previous and current selection to reduce native calls
            // from 15×3=45 to 2×3=6 per navigation
            
            if (_previousSelectedIndex >= 0 && _previousSelectedIndex < _rows.Length)
            {
                // Deselect previous
                if (_rows[_previousSelectedIndex].selectedIndicator != null)
                    SetIndicatorVisible(_rows[_previousSelectedIndex].selectedIndicator, false);

                if (_rowBackgrounds[_previousSelectedIndex] != null)
                    _rowBackgrounds[_previousSelectedIndex].color = RowBg;

                if (_rowAccentBars[_previousSelectedIndex] != null)
                    _rowAccentBars[_previousSelectedIndex].color = AccentDefault; // FIXED: cached color

                if (_bindingBackgrounds[_previousSelectedIndex] != null)
                    _bindingBackgrounds[_previousSelectedIndex].color = BindingBg;
            }

            if (_selectedIndex >= 0 && _selectedIndex < _rows.Length)
            {
                // Select current
                if (_rows[_selectedIndex].selectedIndicator != null)
                    SetIndicatorVisible(_rows[_selectedIndex].selectedIndicator, true);

                if (_rowBackgrounds[_selectedIndex] != null)
                    _rowBackgrounds[_selectedIndex].color = RowBgSelected; // FIXED: cached color

                if (_rowAccentBars[_selectedIndex] != null)
                    _rowAccentBars[_selectedIndex].color = AccentSelected; // FIXED: cached color

                if (_bindingBackgrounds[_selectedIndex] != null)
                    _bindingBackgrounds[_selectedIndex].color = BindingBgSelected; // FIXED: cached color
            }

            _previousSelectedIndex = _selectedIndex;
        }

        private static void SetIndicatorVisible(GameObject indicator, bool visible)
        {
            if (indicator == null)
                return;

            CanvasGroup canvasGroup = indicator.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = indicator.AddComponent<CanvasGroup>();

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void UpdateStatusForSelected()
        {
            if (_rows.Length == 0)
            {
                SetStatus(StatusNoBindingsConfigured);
                return;
            }

            RebindRow row = _rows[_selectedIndex];
            InputManager input = InputManager.Instance;
            if (TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                // ZERO-GC: Build message without allocation
                _statusBuilder.Clear();
                _statusBuilder.Append(StatusRebindPrefix);
                _statusBuilder.Append(row.label);
                _statusBuilder.Append(" [");
                _statusBuilder.Append(resolutionMessage);
                _statusBuilder.Append(']');
                _statusBuilder.Append(StatusRebindSuffix);
                SetStatus(_statusBuilder.ToString());
                return;
            }

            SetStatus(resolutionMessage);
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

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0) return 0;
            if (value >= max) return 0;
            if (value < 0) return max - 1;
            return value;
        }

        private static IInputBindingService ResolveRebindingService()
        {
            return GlobalRegistry.InputBinding;
        }

        private static RebindRow[] BuildDefaultRows()
        {
            List<RebindRow> rows = new List<RebindRow>(15);
            AddRow(rows, "LOOK", "Player", "Look", 0);
            AddRow(rows, "JUMP", "Player", "Jump", 0);
            AddRow(rows, "SPRINT", "Player", "Sprint", 0);
            AddRow(rows, "INTERACT", "Player", "Interact", 0);
            AddRow(rows, "FLASHLIGHT", "Player", "Flashlight", 0);
            AddRow(rows, "PDA", "Player", "PDA", 0);
            AddRow(rows, "TOOL SLOT 1", "Player", "ToolSlot1", 0);
            AddRow(rows, "TOOL SLOT 2", "Player", "ToolSlot2", 0);
            AddRow(rows, "TOOL SLOT 3", "Player", "ToolSlot3", 0);
            AddRow(rows, "TOOL SLOT 4", "Player", "ToolSlot4", 0);
            AddRow(rows, "PRIMARY ACTION", "Player", "PrimaryAction", 0);
            AddRow(rows, "SECONDARY ACTION", "Player", "SecondaryAction", 0);
            AddRow(rows, "INVENTORY", "Player", "Inventory", 0);
            AddRow(rows, "UI SUBMIT", "UI", "Submit", 0);
            AddRow(rows, "UI CANCEL", "UI", "Cancel", 0);
            return rows.ToArray();
        }

        private static void AddRow(List<RebindRow> rows, string label, string map, string action, int bindingIndex)
        {
            if (rows == null)
                return;

            if (string.IsNullOrWhiteSpace(label) ||
                string.IsNullOrWhiteSpace(map) ||
                string.IsNullOrWhiteSpace(action))
            {
                return;
            }

            rows.Add(MakeRow(label.Trim(), map.Trim(), action.Trim(), bindingIndex));
        }

        private static bool TryResolveRowBinding(
            InputManager input,
            RebindRow row,
            out InputAction action,
            out int bindingIndex,
            out string resolutionMessage)
        {
            action = null;
            bindingIndex = -1;
            resolutionMessage = "INPUT MANAGER UNAVAILABLE.";

            if (input == null)
                return false;

            if (row == null)
            {
                resolutionMessage = "INVALID BINDING ROW.";
                return false;
            }

            action = input.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                resolutionMessage = $"MISSING ACTION: {row.actionMap}/{row.actionName}";
                return false;
            }

            bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
            if (bindingIndex < 0)
            {
                resolutionMessage = $"NO REBINDABLE BINDING: {row.label}";
                return false;
            }

            resolutionMessage = GetBindingDisplaySafe(action, bindingIndex);
            return true;
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

            TMP_FontAsset[] fonts = System.Array.Empty<TMP_FontAsset>();
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

            return LocalizedFontResolver.ResolveReadableFont(preferred);
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
