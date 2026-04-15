using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.Input
{
    /// <summary>
    /// Centralized runtime rebinding service for Input System actions.
    /// Persists binding overrides in PlayerPrefs and exposes lifecycle events for UI.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30990)] // Keep scene-owned instance ahead of regular runtime consumers.
    public sealed class RebindingManager : MonoBehaviour
    {
        private const string DefaultOverridesKey = "Hecton8.Input.BindingOverrides.v1";
        private static RebindingManager _instance;
        private static bool _isShuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _isShuttingDown = false;
        }

        [Header("Persistence")]
        [SerializeField] private bool loadOverridesOnAwake = true;
        [SerializeField] private bool saveOverridesAfterRebind = true;
        [SerializeField] private string overridesPlayerPrefsKey = DefaultOverridesKey;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private InputActionRebindingExtensions.RebindingOperation _activeRebind;

        public static RebindingManager Instance
        {
            get
            {
                if (_isShuttingDown || !Application.isPlaying)
                    return _instance;

                if (_instance != null) return _instance;

                var go = new GameObject("[RebindingManager]");
                _instance = go.AddComponent<RebindingManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        public static bool TryGetInstance(out RebindingManager instance)
        {
            instance = _instance;
            return instance != null;
        }

        public bool IsRebinding => _activeRebind != null;

        public event Action<string, string, int> OnRebindStarted;
        public event Action<string, string, int, string> OnRebindCompleted;
        public event Action<string, string, int> OnRebindCanceled;
        public event Action<string, string, string, Action, Action> OnConflictDetected; // actionName, conflictingAction, newBinding, onConfirm, onCancel
        public event Action OnOverridesLoaded;
        public event Action OnOverridesSaved;
        public event Action OnOverridesCleared;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _isShuttingDown = false;
            DontDestroyOnLoad(gameObject);

            if (!loadOverridesOnAwake) return;
            LoadOverrides();
        }

        private void OnDestroy()
        {
            _isShuttingDown = true;

            if (_instance == this)
            {
                _instance = null;
            }

            CancelRebind();
        }

        public bool StartInteractiveRebind(
            string actionName,
            string actionMap = "Player",
            int bindingIndex = 0,
            string expectedControlType = null,
            string cancelPath = "<Keyboard>/escape",
            string[] excludedControlPaths = null)
        {
            if (IsRebinding)
            {
                LogWarning("Cannot start a new rebind while another one is active.");
                return false;
            }

            InputAction action = InputManager.Instance.GetAction(actionName, actionMap);
            if (action == null)
            {
                LogWarning($"Action not found: map='{actionMap}', action='{actionName}'.");
                return false;
            }

            int bindingCount;
            try
            {
                bindingCount = action.bindings.Count;
            }
            catch (Exception ex)
            {
                LogWarning($"Unable to inspect bindings for '{actionMap}/{actionName}': {ex.Message}");
                return false;
            }

            if (bindingIndex < 0 || bindingIndex >= bindingCount)
            {
                LogWarning($"Binding index out of range: action='{actionName}', index={bindingIndex}.");
                return false;
            }

            InputBinding binding;
            try
            {
                binding = action.bindings[bindingIndex];
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to read binding for '{actionMap}/{actionName}[{bindingIndex}]': {ex.Message}");
                return false;
            }

            if (binding.isComposite)
            {
                LogWarning($"Binding index points to a composite root. Rebind composite parts instead. action='{actionName}', index={bindingIndex}.");
                return false;
            }

            bool wasEnabled = action.enabled;
            action.Disable();

            _activeRebind = action.PerformInteractiveRebinding(bindingIndex);

            if (!string.IsNullOrWhiteSpace(expectedControlType))
            {
                _activeRebind.WithExpectedControlType(expectedControlType);
            }

            if (!string.IsNullOrWhiteSpace(cancelPath))
            {
                _activeRebind.WithCancelingThrough(cancelPath);
            }

            if (excludedControlPaths != null)
            {
                for (int i = 0; i < excludedControlPaths.Length; i++)
                {
                    string path = excludedControlPaths[i];
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    _activeRebind.WithControlsExcluding(path);
                }
            }

            _activeRebind.OnCancel(operation =>
            {
                if (wasEnabled) action.Enable();
                DisposeActiveRebind();
                OnRebindCanceled?.Invoke(actionName, actionMap, bindingIndex);
                Log($"Rebind canceled: {actionMap}/{actionName}[{bindingIndex}]");
            });

            _activeRebind.OnComplete(operation =>
            {
                if (wasEnabled) action.Enable();

                string display = "--";
                if (!InputManager.TryGetBindingDisplayStringSafe(action, bindingIndex, out display) ||
                    string.IsNullOrEmpty(display))
                {
                    display = "--";
                }

                // TASK 16: Conflict detection
                string conflictingAction = DetectConflict(action, bindingIndex, actionMap);
                if (!string.IsNullOrEmpty(conflictingAction))
                {
                    // Store state for conflict resolution
                    string capturedActionName = actionName;
                    string capturedActionMap = actionMap;
                    int capturedBindingIndex = bindingIndex;
                    string capturedDisplay = display;
                    bool capturedWasEnabled = wasEnabled;

                    DisposeActiveRebind();

                    // Invoke conflict event with confirm/cancel callbacks
                    OnConflictDetected?.Invoke(
                        capturedActionName,
                        conflictingAction,
                        capturedDisplay,
                        () => CompleteRebindAfterConflictResolution(capturedActionName, capturedActionMap, capturedBindingIndex, capturedDisplay), // Confirm
                        () => CancelRebindAfterConflict(action, capturedActionName, capturedActionMap, capturedBindingIndex, capturedWasEnabled) // Cancel
                    );
                    return;
                }

                DisposeActiveRebind();

                if (saveOverridesAfterRebind)
                {
                    SaveOverrides();
                }

                OnRebindCompleted?.Invoke(actionName, actionMap, bindingIndex, display);
                Log($"Rebind complete: {actionMap}/{actionName}[{bindingIndex}] => {display}");
            });

            _activeRebind.Start();
            OnRebindStarted?.Invoke(actionName, actionMap, bindingIndex);
            Log($"Rebind started: {actionMap}/{actionName}[{bindingIndex}]");
            return true;
        }

        public bool StartInteractiveRebindById(
            string actionName,
            string bindingId,
            string actionMap = "Player",
            string expectedControlType = null,
            string cancelPath = "<Keyboard>/escape",
            string[] excludedControlPaths = null)
        {
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                LogWarning("Binding id is empty.");
                return false;
            }

            InputAction action = InputManager.Instance.GetAction(actionName, actionMap);
            if (action == null)
            {
                LogWarning($"Action not found: map='{actionMap}', action='{actionName}'.");
                return false;
            }

            int index = FindBindingIndexById(action, bindingId);
            if (index < 0)
            {
                LogWarning($"Binding id not found: action='{actionName}', bindingId='{bindingId}'.");
                return false;
            }

            return StartInteractiveRebind(
                actionName,
                actionMap,
                index,
                expectedControlType,
                cancelPath,
                excludedControlPaths);
        }

        public void CancelRebind()
        {
            if (_activeRebind == null) return;
            _activeRebind.Cancel();
            DisposeActiveRebind();
        }

        public void SaveOverrides()
        {
            string json = InputManager.Instance.SaveBindingOverridesAsJson();
            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            if (string.IsNullOrEmpty(json))
            {
                options.DeleteKey(overridesPlayerPrefsKey);
            }
            else
            {
                options.SetString(overridesPlayerPrefsKey, json);
            }

            options.Save();
            OnOverridesSaved?.Invoke();
            Log("Binding overrides saved.");
        }

        public void LoadOverrides()
        {
            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            if (!options.HasKey(overridesPlayerPrefsKey))
            {
                Log("No saved binding overrides found.");
                return;
            }

            string json = options.GetString(overridesPlayerPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                Log("Saved binding overrides key exists but payload is empty.");
                return;
            }

            InputManager.Instance.LoadBindingOverridesFromJson(json);
            OnOverridesLoaded?.Invoke();
            Log("Binding overrides loaded.");
        }

        public void ClearOverrides(bool clearPlayerPrefs = true)
        {
            CancelRebind();
            InputManager.Instance.ClearBindingOverrides();

            if (clearPlayerPrefs)
            {
                UserOptionsPersistence options = UserOptionsPersistence.Instance;
                options.DeleteKey(overridesPlayerPrefsKey);
                options.Save();
            }

            OnOverridesCleared?.Invoke();
            Log("Binding overrides cleared.");
        }

        private static int FindBindingIndexById(InputAction action, string bindingId)
        {
            if (action == null || string.IsNullOrWhiteSpace(bindingId))
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

            for (int i = 0; i < bindingCount; i++)
            {
                try
                {
                    if (action.bindings[i].id.ToString().Equals(bindingId, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
                catch
                {
                    return -1;
                }
            }

            return -1;
        }

        private void DisposeActiveRebind()
        {
            if (_activeRebind == null) return;
            _activeRebind.Dispose();
            _activeRebind = null;
        }

        private void Log(string message)
        {
            if (!verboseLogging) return;
            Debug.Log($"[RebindingManager] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[RebindingManager] {message}");
        }

        /// <summary>
        /// TASK 16: Detects if the new binding conflicts with another action.
        /// Returns the name of the conflicting action, or null if no conflict.
        /// SAFETY: Validates action map and actions collection before iteration.
        /// </summary>
        private string DetectConflict(InputAction currentAction, int currentBindingIndex, string currentActionMap)
        {
            if (currentAction == null || InputManager.Instance == null)
                return null;

            string newPath;
            try
            {
                newPath = currentAction.bindings[currentBindingIndex].effectivePath;
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrEmpty(newPath))
                return null;

            // Check all actions in the same action map for conflicts
            InputActionMap actionMap = InputManager.Instance.GetActionMap(currentActionMap);
            if (actionMap == null)
                return null;

            // SAFETY: ReadOnlyArray is a struct; only the count check is meaningful here.
            if (actionMap.actions.Count == 0)
                return null;

            foreach (InputAction action in actionMap.actions)
            {
                if (action == null || action == currentAction)
                    continue;

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    try
                    {
                        InputBinding binding = action.bindings[i];
                        
                        // Skip composite roots and parts
                        if (binding.isComposite || binding.isPartOfComposite)
                            continue;

                        string existingPath = binding.effectivePath;
                        if (string.IsNullOrEmpty(existingPath))
                            continue;

                        // Check if paths match
                        if (string.Equals(existingPath, newPath, StringComparison.OrdinalIgnoreCase))
                        {
                            return action.name;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// TASK 16: Completes rebind after user confirms conflict resolution.
        /// </summary>
        private void CompleteRebindAfterConflictResolution(string actionName, string actionMap, int bindingIndex, string display)
        {
            if (saveOverridesAfterRebind)
            {
                SaveOverrides();
            }

            OnRebindCompleted?.Invoke(actionName, actionMap, bindingIndex, display);
            Log($"Rebind complete (after conflict resolution): {actionMap}/{actionName}[{bindingIndex}] => {display}");
        }

        /// <summary>
        /// TASK 16: Cancels rebind after user rejects conflict resolution.
        /// Removes the binding override to restore previous state.
        /// </summary>
        private void CancelRebindAfterConflict(InputAction action, string actionName, string actionMap, int bindingIndex, bool wasEnabled)
        {
            if (action != null)
            {
                try
                {
                    action.RemoveBindingOverride(bindingIndex);
                    if (wasEnabled)
                        action.Enable();
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to remove binding override after conflict cancel: {ex.Message}");
                }
            }

            OnRebindCanceled?.Invoke(actionName, actionMap, bindingIndex);
            Log($"Rebind canceled (conflict rejected): {actionMap}/{actionName}[{bindingIndex}]");
        }
    }
}
