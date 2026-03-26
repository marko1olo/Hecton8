using System;
using Hecton8.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private int _selectedIndex;
        private bool _subscribed;

        private bool IsControlsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPda != null &&
            playerPda.ActiveTab == controlsTabIndex;

        private void Awake()
        {
            if (playerPda == null)
            {
                playerPda = GetComponentInParent<PlayerPDA>();
            }

            if (rows == null) rows = Array.Empty<RebindRow>();
            EnsureRowsConfigured();
            if (autoResolveRowReferences)
            {
                ResolveRowReferencesByName();
            }

            if (_selectedIndex >= rows.Length) _selectedIndex = 0;
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
            if (input == null || RebindingManager.Instance == null)
                return;

            input.OnNavigate += HandleNavigate;
            input.OnSubmit += HandleSubmit;
            input.OnCancel += HandleCancel;
            input.OnTabNext += HandleTabNext;
            input.OnTabPrevious += HandleTabPrevious;

            RebindingManager.Instance.OnRebindStarted += HandleRebindStarted;
            RebindingManager.Instance.OnRebindCompleted += HandleRebindCompleted;
            RebindingManager.Instance.OnRebindCanceled += HandleRebindCanceled;

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

            if (RebindingManager.Instance != null)
            {
                RebindingManager.Instance.OnRebindStarted -= HandleRebindStarted;
                RebindingManager.Instance.OnRebindCompleted -= HandleRebindCompleted;
                RebindingManager.Instance.OnRebindCanceled -= HandleRebindCanceled;
            }

            PDAEvents.OnTabChanged -= HandlePdaTabChanged;
            PDAEvents.OnOpened -= HandlePdaOpened;

            _subscribed = false;
        }

        private void HandleNavigate(Vector2 direction)
        {
            if (!IsControlsTabActive) return;
            if (rows.Length == 0) return;
            if (RebindingManager.Instance.IsRebinding) return;

            int delta = 0;
            if (direction.y > 0.35f) delta = -1;
            else if (direction.y < -0.35f) delta = 1;

            if (delta == 0) return;
            _selectedIndex = WrapIndex(_selectedIndex + delta, rows.Length);
            RefreshSelectionVisuals();
            UpdateStatusForSelected();
        }

        private void HandleSubmit()
        {
            if (!IsControlsTabActive) return;
            if (rows.Length == 0) return;
            if (RebindingManager.Instance.IsRebinding) return;

            RebindRow row = rows[_selectedIndex];
            bool started = RebindingManager.Instance.StartInteractiveRebind(
                row.actionName,
                row.actionMap,
                row.bindingIndex,
                expectedControlType: null,
                cancelPath: "<Keyboard>/escape",
                excludedControlPaths: new[] { "<Pointer>/position", "<Pointer>/delta" });

            if (!started)
            {
                SetStatus($"Failed to start: {row.label}");
            }
        }

        private void HandleCancel()
        {
            if (!PlayerPDA.IsOpen) return;
            if (!RebindingManager.Instance.IsRebinding) return;
            RebindingManager.Instance.CancelRebind();
        }

        private void HandleTabNext()
        {
            if (!IsControlsTabActive) return;
            if (rows.Length == 0) return;
            if (RebindingManager.Instance.IsRebinding) return;

            ResetSelectedBinding();
        }

        private void HandleTabPrevious()
        {
            if (!IsControlsTabActive) return;
            if (RebindingManager.Instance.IsRebinding) return;

            RebindingManager.Instance.ClearOverrides();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void HandleRebindStarted(string actionName, string actionMap, int bindingIndex)
        {
            if (!IsControlsTabActive) return;
            SetStatus($"{rebindingPrefix}  [{actionMap}/{actionName}]");
        }

        private void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            RefreshAllBindings();
            if (!IsControlsTabActive) return;
            SetStatus($"{actionName}: {display}");
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

        private void ResetSelectedBinding()
        {
            RebindRow row = rows[_selectedIndex];
            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                SetStatus($"Action not found: {row.actionMap}/{row.actionName}");
                return;
            }

            if (row.bindingIndex < 0 || row.bindingIndex >= action.bindings.Count)
            {
                SetStatus($"Invalid binding index: {row.actionName}[{row.bindingIndex}]");
                return;
            }

            action.RemoveBindingOverride(row.bindingIndex);
            if (saveAfterRowReset)
            {
                RebindingManager.Instance.SaveOverrides();
            }

            RefreshRowBinding(row);
            UpdateStatusForSelected();
        }

        private void RefreshAll()
        {
            RefreshLabels();
            RefreshSelectionVisuals();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void EnsureRowsConfigured()
        {
            if (rows != null && rows.Length > 0) return;
            if (!autoGenerateRowsIfEmpty) return;
            rows = BuildDefaultRows();
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
            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (row.labelText != null)
                {
                    row.labelText.text = row.label;
                }
            }
        }

        private void RefreshAllBindings()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                RefreshRowBinding(rows[i]);
            }
        }

        private void RefreshRowBinding(RebindRow row)
        {
            if (row.bindingText == null) return;

            InputAction action = InputManager.Instance.GetAction(row.actionName, row.actionMap);
            if (action == null || row.bindingIndex < 0 || row.bindingIndex >= action.bindings.Count)
            {
                row.bindingText.text = "--";
                return;
            }

            string binding = action.GetBindingDisplayString(row.bindingIndex);
            row.bindingText.text = string.IsNullOrEmpty(binding) ? "--" : binding;
        }

        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                GameObject indicator = rows[i].selectedIndicator;
                if (indicator != null)
                {
                    indicator.SetActive(i == _selectedIndex);
                }
            }
        }

        private void UpdateStatusForSelected()
        {
            if (rows.Length == 0)
            {
                SetStatus("No bindings configured.");
                return;
            }

            RebindRow row = rows[_selectedIndex];
            string binding = InputManager.Instance.GetBindingDisplayString(
                row.actionName, row.actionMap, row.bindingIndex);
            if (string.IsNullOrEmpty(binding)) binding = "--";

            SetStatus($"{readyPrefix}: {row.label} [{binding}]  |  {resetHint}");
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
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
    }
}
