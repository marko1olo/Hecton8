using System;
using System.IO;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.Input
{
    /// <summary>
    /// Centralized runtime rebinding service for Input System actions.
    /// Persists binding overrides to controls.json and exposes lifecycle events for UI.
    /// Legacy PlayerPrefs payload is still supported as a migration fallback.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30990)] // Keep scene-owned instance ahead of regular runtime consumers.
    public sealed class RebindingManager : MonoBehaviour, IInputRebindService
    {
        private const string DefaultOverridesKey = "Hecton8.Input.BindingOverrides.v1";
        private const string DefaultOverridesFileName = "controls.json";
        private const string DefaultOverridesTempFileName = "controls.json.tmp";
        private const string DefaultKeyboardCancelPath = "<Keyboard>/escape";
        [Header("Persistence")]
        [SerializeField] private bool loadOverridesOnAwake = true;
        [SerializeField] private bool saveOverridesAfterRebind = true;
        [SerializeField] private string overridesFileName = DefaultOverridesFileName;
        [SerializeField] private string overridesTempFileName = DefaultOverridesTempFileName;
        [Tooltip("Legacy migration fallback. Existing PlayerPrefs payload is consumed only when controls.json is absent.")]
        [SerializeField] private string overridesPlayerPrefsKey = DefaultOverridesKey;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private InputActionRebindingExtensions.RebindingOperation _activeRebind;
        private bool _registeredService;

        internal static RebindingManager ActiveRuntimeInstance { get; private set; }

        public bool IsRebinding => _activeRebind != null;

        public event Action<string, string, int> OnRebindStarted;
        public event Action<string, string, int, string> OnRebindCompleted;
        public event Action<string, string, int> OnRebindCanceled;
        public event Action<string, string, string, Action, Action> OnConflictDetected; // actionName, conflictingAction, newBinding, onConfirm, onCancel
        public event Action OnOverridesLoaded;
        public event Action OnOverridesSaved;
        public event Action OnOverridesCleared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            IInputBindingService registered = GlobalRegistry.InputBinding;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            ActiveRuntimeInstance = this;
            TryRegisterService();

            if (!loadOverridesOnAwake) return;
            LoadOverrides();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            TryRegisterService();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterService();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            CancelRebind();
            TryUnregisterService();
        }

        public bool StartInteractiveRebind(
            string actionName,
            string actionMap = "Player",
            int bindingIndex = 0,
            string expectedControlType = null,
            string cancelPath = DefaultKeyboardCancelPath,
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
            string cancelPath = DefaultKeyboardCancelPath,
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
            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                LogWarning("Cannot save binding overrides because InputManager.Instance is null.");
                return;
            }

            string json = inputManager.SaveBindingOverridesAsJson();
            if (string.IsNullOrEmpty(json))
            {
                DeleteOverridesFileIfExists();
                DeleteLegacyOverridesKey();
            }
            else
            {
                if (!TryWriteOverridesFile(json))
                    return;

                DeleteLegacyOverridesKey();
            }

            OnOverridesSaved?.Invoke();
            Log("Binding overrides saved.");
        }

        public void LoadOverrides()
        {
            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                LogWarning("Cannot load binding overrides because InputManager.Instance is null.");
                return;
            }

            if (TryLoadOverridesFromFile(inputManager))
                return;

            if (TryLoadOverridesFromLegacyStorage(inputManager))
                return;

            Log("No saved binding overrides found.");
        }

        public void ClearOverrides(bool clearPlayerPrefs = true)
        {
            CancelRebind();
            InputManager inputManager = InputManager.Instance;
            if (inputManager != null)
                inputManager.ClearBindingOverrides();

            DeleteOverridesFileIfExists();

            if (clearPlayerPrefs)
                DeleteLegacyOverridesKey();

            OnOverridesCleared?.Invoke();
            Log("Binding overrides cleared.");
        }

        private bool TryLoadOverridesFromFile(InputManager inputManager)
        {
            string path = GetOverridesFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    LogWarning($"Binding overrides file is empty: {path}");
                    return false;
                }

                inputManager.LoadBindingOverridesFromJson(json);
                OnOverridesLoaded?.Invoke();
                Log($"Binding overrides loaded from file: {path}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to load binding overrides file '{path}': {ex.Message}");
                return false;
            }
        }

        private bool TryLoadOverridesFromLegacyStorage(InputManager inputManager)
        {
            if (string.IsNullOrWhiteSpace(overridesPlayerPrefsKey))
                return false;

            UserOptionsPersistence options = Hecton8.Core.GlobalRegistry.UserOptions;
            if (options == null || !options.HasKey(overridesPlayerPrefsKey))
                return false;

            string json = options.GetString(overridesPlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                Log("Legacy binding overrides key exists but payload is empty.");
                return false;
            }

            try
            {
                inputManager.LoadBindingOverridesFromJson(json);
                if (TryWriteOverridesFile(json))
                    DeleteLegacyOverridesKey();

                OnOverridesLoaded?.Invoke();
                Log("Binding overrides loaded from legacy PlayerPrefs payload and migrated to file storage.");
                return true;
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to load legacy binding overrides payload: {ex.Message}");
                return false;
            }
        }

        private bool TryWriteOverridesFile(string json)
        {
            string path = GetOverridesFilePath();
            string tempPath = GetOverridesTempFilePath();
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(tempPath))
            {
                LogWarning("Cannot save binding overrides because resolved controls.json path is empty.");
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);
                return true;
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to save binding overrides file '{path}': {ex.Message}");

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort cleanup only.
                }

                return false;
            }
        }

        private void DeleteOverridesFileIfExists()
        {
            string path = GetOverridesFilePath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to delete binding overrides file '{path}': {ex.Message}");
                }
            }

            string tempPath = GetOverridesTempFilePath();
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to delete temp binding overrides file '{tempPath}': {ex.Message}");
                }
            }
        }

        private void DeleteLegacyOverridesKey()
        {
            if (string.IsNullOrWhiteSpace(overridesPlayerPrefsKey))
                return;

            UserOptionsPersistence options = Hecton8.Core.GlobalRegistry.UserOptions;
            if (options == null)
                return;

            options.DeleteKey(overridesPlayerPrefsKey);
            options.Save();
        }

        private string GetOverridesFilePath()
        {
            string fileName = string.IsNullOrWhiteSpace(overridesFileName)
                ? DefaultOverridesFileName
                : overridesFileName;
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private string GetOverridesTempFilePath()
        {
            string fileName = string.IsNullOrWhiteSpace(overridesTempFileName)
                ? DefaultOverridesTempFileName
                : overridesTempFileName;
            return Path.Combine(Application.persistentDataPath, fileName);
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

        private void TryRegisterService()
        {
            if (_registeredService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterInputBindingService(this);
            _registeredService = true;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            if (ReferenceEquals(GlobalRegistry.InputBinding, this))
                GlobalRegistry.UnregisterInputBindingService(this);

            _registeredService = false;
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
