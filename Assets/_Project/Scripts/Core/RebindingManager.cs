using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.Input
{
    /// <summary>
    /// Centralized runtime rebinding service for Input System actions.
    /// Persists binding overrides to controls.json and exposes lifecycle events for UI.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30990)] // Keep scene-owned instance ahead of regular runtime consumers.
    public sealed class RebindingManager : MonoBehaviour, IInputRebindService
    {
        private const string DefaultOverridesFileName = "controls.json";
        private const string DefaultOverridesTempFileName = "controls.json.tmp";
        private const string DefaultKeyboardCancelPath = "<Keyboard>/escape";
        private const string AgentTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_1332.bin";
        [Header("Persistence")]
        [SerializeField] private bool loadOverridesOnAwake = true;
        [SerializeField] private bool saveOverridesAfterRebind = true;
        [SerializeField] private string overridesFileName = DefaultOverridesFileName;
        [SerializeField] private string overridesTempFileName = DefaultOverridesTempFileName;
        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private InputActionRebindingExtensions.RebindingOperation _activeRebind;
        private INativeInputManagerRuntime _nativeInputManager;
        private Action<InputActionRebindingExtensions.RebindingOperation> _cachedRebindCancelAction;
        private Action<InputActionRebindingExtensions.RebindingOperation> _cachedRebindCompleteAction;
        private Action _cachedConflictConfirmAction;
        private Action _cachedConflictCancelAction;
        private readonly RuntimeOverrideRollbackRecord[] _clearRollbackRecords = new RuntimeOverrideRollbackRecord[ControlRemapper.MaxBindingRecords]; // COLD ALLOC: RuntimeOverrideRollbackRecord[128] - clear command rollback snapshot - owner: RebindingManager
        private InputAction _activeAction;
        private INativeInputManagerRuntime _activeInputManager;
        private string _activeActionName;
        private string _activeActionMap;
        private string _activePreviousOverridePath;
        private int _activeBindingIndex;
        private bool _activeActionWasEnabled;
        private InputAction _pendingConflictAction;
        private InputAction _pendingConflictVictimAction;
        private string _pendingConflictActionName;
        private string _pendingConflictActionMap;
        private string _pendingConflictDisplay;
        private string _pendingConflictPreviousOverridePath;
        private string _pendingConflictVictimPreviousOverridePath;
        private int _pendingConflictBindingIndex;
        private int _pendingConflictVictimBindingIndex;
        private bool _pendingConflictWasEnabled;
        private IDataVault _dataVault;
        private VaultGenerationHandle<InputBindingTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private bool _registeredService;
        private bool _initialOverridesLoadAttempted;
        private bool _telemetryBootstrapped;

        private struct RuntimeOverrideRollbackRecord
        {
            public InputAction Action;
            public string OverridePath;
            public int BindingIndex;
        }

        internal static RebindingManager ActiveRuntimeInstance { get; private set; }

        public bool IsRebinding => _activeRebind != null || _pendingConflictAction != null;

        public event Action<string, string, int> OnRebindStarted;
        public event Action<string, string, int, string> OnRebindCompleted;
        public event Action<string, string, int> OnRebindCanceled;
        public event Action<string, string, string, Action, Action> OnConflictDetected; // actionName, conflictingAction, newBinding, onConfirm, onCancel
        public event Action OnOverridesLoaded;
        public event Action OnOverridesSaved;
        public event Action<string, string, int> OnRebindSaveFailed;
        public event Action OnOverridesSaveFailed;
        public event Action OnOverridesCleared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            EnsureCachedDelegates();
            if (!Application.isPlaying)
                return;

            if (TryDestroyDuplicateService())
                return;

            TryColdBootstrapTelemetry();
            TryRegisterService();
            if (_registeredService)
                ActiveRuntimeInstance = this;

            if (_registeredService)
                TryLoadInitialOverrides();
        }

        private void OnEnable()
        {
            EnsureCachedDelegates();
            if (!Application.isPlaying)
                return;

            if (TryDestroyDuplicateService())
                return;

            TryColdBootstrapTelemetry();
            TryRegisterService();
            if (_registeredService)
                ActiveRuntimeInstance = this;
        }

        private void OnDisable()
        {
            CancelRebindOrPendingConflict();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterService();
        }

        private void OnDestroy()
        {
            CancelRebindOrPendingConflict();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterService();
        }

        private bool TryDestroyDuplicateService()
        {
            if (!Application.isPlaying)
                return false;

            IInputBindingService registered = GlobalRegistry.InputBinding;
            if (registered == null || ReferenceEquals(registered, this))
                return false;

            Destroy(gameObject);
            return true;
        }

        /// <summary>
        /// Binds the bootstrap-owned native input action owner used for rebind operations.
        /// </summary>
        /// <param name="inputManager">Bootstrap-owned native input manager.</param>
        internal void BindNativeInputManager(INativeInputManagerRuntime inputManager)
        {
            if (ReferenceEquals(_nativeInputManager, inputManager))
                return;

            _nativeInputManager = inputManager;
            TryLoadInitialOverrides();
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

            INativeInputManagerRuntime inputManager = ResolveNativeInputManager();
            if (inputManager == null)
            {
                LogWarning("Cannot start rebind because the native input manager is not bound.");
                return false;
            }

            InputAction action = inputManager.GetAction(actionName, actionMap);
            if (action == null)
            {
                LogWarning("Action not found.");
                return false;
            }

            int bindingCount = action.bindings.Count;

            if (bindingIndex < 0 || bindingIndex >= bindingCount)
            {
                LogWarning("Binding index out of range.");
                return false;
            }

            InputBinding binding = action.bindings[bindingIndex];

            if (binding.isComposite)
            {
                LogWarning("Binding index points to a composite root.");
                return false;
            }

            if (!TryReadActionEnabled(action, out bool wasEnabled) ||
                !TryDisableActionForRebind(action))
            {
                LogWarning("Cannot start rebind because the action state cannot be prepared.");
                return false;
            }

            _activeAction = action;
            _activeInputManager = inputManager;
            _activeActionName = actionName;
            _activeActionMap = actionMap;
            _activePreviousOverridePath = binding.overridePath;
            _activeBindingIndex = bindingIndex;
            _activeActionWasEnabled = wasEnabled;

            try
            {
                _activeRebind = action.PerformInteractiveRebinding(bindingIndex);
                if (_activeRebind == null)
                {
                    FailStartInteractiveRebind(action, wasEnabled);
                    return false;
                }

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

                _activeRebind.OnCancel(_cachedRebindCancelAction);
                _activeRebind.OnComplete(_cachedRebindCompleteAction);

                _activeRebind.Start();
            }
            catch (InvalidOperationException)
            {
                FailStartInteractiveRebind(action, wasEnabled);
                return false;
            }
            catch (ArgumentException)
            {
                FailStartInteractiveRebind(action, wasEnabled);
                return false;
            }
            catch (NotSupportedException)
            {
                FailStartInteractiveRebind(action, wasEnabled);
                return false;
            }

            OnRebindStarted?.Invoke(actionName, actionMap, bindingIndex);
            Log("Rebind started.");
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

            INativeInputManagerRuntime inputManager = ResolveNativeInputManager();
            if (inputManager == null)
            {
                LogWarning("Cannot start rebind by id because the native input manager is not bound.");
                return false;
            }

            InputAction action = inputManager.GetAction(actionName, actionMap);
            if (action == null)
            {
                LogWarning("Action not found.");
                return false;
            }

            int index = FindBindingIndexById(action, bindingId);
            if (index < 0)
            {
                LogWarning("Binding id not found.");
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
            if (_activeRebind != null)
            {
                InputActionRebindingExtensions.RebindingOperation operation = _activeRebind;
                operation.Cancel();
                if (ReferenceEquals(_activeRebind, operation))
                    HandleRebindCanceledOperation(operation);
                return;
            }

            if (_pendingConflictAction != null)
                CancelPendingConflict();
        }

        private void HandleRebindCanceledOperation(InputActionRebindingExtensions.RebindingOperation operation)
        {
            InputAction action = _activeAction;
            string actionName = _activeActionName;
            string actionMap = _activeActionMap;
            int bindingIndex = _activeBindingIndex;
            bool wasEnabled = _activeActionWasEnabled;

            if (!TryRestoreActionEnabled(action, wasEnabled))
                LogWarning("Rebind cancel could not restore action enabled state.");

            DisposeActiveRebind();
            ClearActiveRebindContext();
            OnRebindCanceled?.Invoke(actionName, actionMap, bindingIndex);
            Log("Rebind canceled.");
        }

        private void HandleRebindCompletedOperation(InputActionRebindingExtensions.RebindingOperation operation)
        {
            InputAction action = _activeAction;
            INativeInputManagerRuntime inputManager = _activeInputManager;
            string actionName = _activeActionName;
            string actionMap = _activeActionMap;
            int bindingIndex = _activeBindingIndex;
            bool wasEnabled = _activeActionWasEnabled;
            string previousOverridePath = _activePreviousOverridePath;

            if (!TryRestoreActionEnabled(action, wasEnabled))
            {
                DisposeActiveRebind();
                ClearActiveRebindContext();
                OnRebindCanceled?.Invoke(actionName, actionMap, bindingIndex);
                LogWarning("Rebind complete could not restore action enabled state.");
                return;
            }

            string display = "--";
            if (inputManager == null ||
                action == null ||
                !inputManager.TryGetBindingDisplayString(action, bindingIndex, out display) ||
                string.IsNullOrEmpty(display))
            {
                display = "--";
            }

            if (TryDetectConflict(
                    action,
                    bindingIndex,
                    actionMap,
                    out string conflictingAction,
                    out InputAction conflictingInputAction,
                    out int conflictingBindingIndex))
            {
                _pendingConflictAction = action;
                _pendingConflictVictimAction = conflictingInputAction;
                _pendingConflictActionName = actionName;
                _pendingConflictActionMap = actionMap;
                _pendingConflictBindingIndex = bindingIndex;
                _pendingConflictVictimBindingIndex = conflictingBindingIndex;
                _pendingConflictDisplay = display;
                _pendingConflictPreviousOverridePath = previousOverridePath;
                _pendingConflictVictimPreviousOverridePath = TryGetBindingOverridePath(conflictingInputAction, conflictingBindingIndex);
                _pendingConflictWasEnabled = wasEnabled;

                DisposeActiveRebind();
                ClearActiveRebindContext();
                OnConflictDetected?.Invoke(
                    _pendingConflictActionName,
                    conflictingAction,
                    _pendingConflictDisplay,
                    _cachedConflictConfirmAction,
                    _cachedConflictCancelAction);
                return;
            }

            DisposeActiveRebind();
            ClearActiveRebindContext();

            if (saveOverridesAfterRebind)
            {
                if (!SaveOverrides())
                {
                    TryRestoreBindingOverride(action, bindingIndex, previousOverridePath);
                    OnRebindSaveFailed?.Invoke(actionName, actionMap, bindingIndex);
                    OnRebindCanceled?.Invoke(actionName, actionMap, bindingIndex);
                    OnOverridesSaveFailed?.Invoke();
                    LogWarning("Rebind auto-save failed; restored previous binding override.");
                    return;
                }
            }

            OnRebindCompleted?.Invoke(actionName, actionMap, bindingIndex, display);
            Log("Rebind complete.");
        }

        private void ConfirmPendingConflict()
        {
            if (_pendingConflictAction == null)
                return;

            InputAction action = _pendingConflictAction;
            InputAction victimAction = _pendingConflictVictimAction;
            string actionName = _pendingConflictActionName;
            string actionMap = _pendingConflictActionMap;
            string display = _pendingConflictDisplay;
            string previousOverridePath = _pendingConflictPreviousOverridePath;
            string victimPreviousOverridePath = _pendingConflictVictimPreviousOverridePath;
            int bindingIndex = _pendingConflictBindingIndex;
            int victimBindingIndex = _pendingConflictVictimBindingIndex;
            bool wasEnabled = _pendingConflictWasEnabled;

            ClearPendingConflictContext();

            if (!TryDisableConflictingBinding(victimAction, victimBindingIndex))
            {
                TryRestoreBindingOverride(victimAction, victimBindingIndex, victimPreviousOverridePath);
                CancelRebindAfterConflict(action, actionName, actionMap, bindingIndex, previousOverridePath, wasEnabled);
                return;
            }

            CompleteRebindAfterConflictResolution(
                action,
                victimAction,
                actionName,
                actionMap,
                bindingIndex,
                victimBindingIndex,
                display,
                previousOverridePath,
                victimPreviousOverridePath);
        }

        private void CancelPendingConflict()
        {
            if (_pendingConflictAction == null)
                return;

            InputAction action = _pendingConflictAction;
            string actionName = _pendingConflictActionName;
            string actionMap = _pendingConflictActionMap;
            string previousOverridePath = _pendingConflictPreviousOverridePath;
            int bindingIndex = _pendingConflictBindingIndex;
            bool wasEnabled = _pendingConflictWasEnabled;
            ClearPendingConflictContext();
            CancelRebindAfterConflict(action, actionName, actionMap, bindingIndex, previousOverridePath, wasEnabled);
        }

        public bool SaveOverrides()
        {
            if (IsRebinding)
            {
                LogWarning("Cannot save binding overrides while rebinding.");
                return false;
            }

            INativeInputManagerRuntime inputManager = ResolveNativeInputManager();
            if (inputManager == null)
            {
                LogWarning("Cannot save binding overrides because the native input manager is not bound.");
                return false;
            }

            string path = GetOverridesFilePath();
            string tempPath = GetOverridesTempFilePath();
            if (!ControlRemapper.TrySaveOverrides(inputManager, path, tempPath, out ControlRemapIoResult saveResult))
            {
                RecordControlRemapTelemetry(in saveResult.Telemetry);
                DumpControlRemapTelemetryOnFault(in saveResult.Telemetry);
                LogWarning("Failed to save binding overrides file.");
                return false;
            }

            RecordControlRemapTelemetry(in saveResult.Telemetry);
            if (saveResult.RecordCount == 0)
            {
                if (!DeleteOverridesFileIfExists())
                    return false;
            }

            OnOverridesSaved?.Invoke();
            Log("Binding overrides saved.");
            return true;
        }

        public bool LoadOverrides()
        {
            CancelRebindOrPendingConflict();
            INativeInputManagerRuntime inputManager = ResolveNativeInputManager();
            if (inputManager == null)
            {
                LogWarning("Cannot load binding overrides because the native input manager is not bound.");
                return false;
            }

            return TryLoadOverridesFromFile(inputManager);
        }

        public bool ClearOverrides(bool clearSavedOverrides = true)
        {
            CancelRebindOrPendingConflict();
            INativeInputManagerRuntime inputManager = ResolveNativeInputManager();
            if (inputManager == null)
            {
                LogWarning("Cannot clear binding overrides because the native input manager is not bound.");
                return false;
            }

            if (!TryCaptureRuntimeOverrideRollback(inputManager, out int rollbackCount))
            {
                LogWarning("Cannot clear binding overrides because the current runtime override state could not be captured.");
                return false;
            }

            try
            {
                if (!TryClearRuntimeBindingOverrides(inputManager))
                {
                    TryRestoreRuntimeOverrideRollback(rollbackCount);
                    LogWarning("Cannot clear binding overrides because the native input manager rejected the clear request.");
                    return false;
                }

                if (clearSavedOverrides && !DeleteOverridesFileIfExists())
                {
                    if (!TryRestoreRuntimeOverrideRollback(rollbackCount))
                        LogWarning("Runtime binding override rollback failed after saved override deletion failed.");
                    return false;
                }

                OnOverridesCleared?.Invoke();
                Log("Binding overrides cleared.");
                return true;
            }
            finally
            {
                ClearRuntimeOverrideRollback(rollbackCount);
            }
        }

        private bool TryLoadOverridesFromFile(INativeInputManagerRuntime inputManager)
        {
            string path = GetOverridesFilePath();
            if (ControlRemapper.TryLoadOverrides(inputManager, path, out ControlRemapIoResult loadResult))
            {
                RecordControlRemapTelemetry(in loadResult.Telemetry);
                OnOverridesLoaded?.Invoke();
                Log("Binding overrides loaded from file.");
                return true;
            }

            RecordControlRemapTelemetry(in loadResult.Telemetry);
            if (loadResult.ResultCode == InputBindingTelemetryResult.FileMissing)
            {
                if (!TryClearRuntimeBindingOverrides(inputManager))
                {
                    LogWarning("No saved binding overrides found, but runtime defaults could not be applied.");
                    return false;
                }

                OnOverridesLoaded?.Invoke();
                Log("No saved binding overrides found; defaults applied.");
                return true;
            }

            DumpControlRemapTelemetryOnFault(in loadResult.Telemetry);
            if ((loadResult.FaultFlags & InputBindingFaultFlags.InvalidSchema) != 0u)
            {
                LogWarning("Binding overrides file schema rejected.");
                return false;
            }

            LogWarning("Failed to load binding overrides file.");
            return false;
        }

        private bool DeleteOverridesFileIfExists()
        {
            string tempPath = GetOverridesTempFilePath();
            if (!TryDeleteOverridesFile(tempPath, "Failed to delete temp binding overrides file."))
                return false;

            string path = GetOverridesFilePath();
            return TryDeleteOverridesFile(path, "Failed to delete binding overrides file.");
        }

        private bool TryDeleteOverridesFile(string path, string warning)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return true;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                LogWarning(warning);
                return false;
            }
            catch (IOException)
            {
                LogWarning(warning);
                return false;
            }
            catch (ArgumentException)
            {
                LogWarning(warning);
                return false;
            }
            catch (NotSupportedException)
            {
                LogWarning(warning);
                return false;
            }
        }

        private string GetOverridesFilePath()
        {
            string fileName = string.IsNullOrWhiteSpace(overridesFileName)
                ? DefaultOverridesFileName
                : overridesFileName;
            return HectonPersistentPathPolicy.CombineFile(fileName);
        }

        private string GetOverridesTempFilePath()
        {
            string fileName = string.IsNullOrWhiteSpace(overridesTempFileName)
                ? DefaultOverridesTempFileName
                : overridesTempFileName;
            return HectonPersistentPathPolicy.CombineFile(fileName);
        }

        private static int FindBindingIndexById(InputAction action, string bindingId)
        {
            if (action == null || string.IsNullOrWhiteSpace(bindingId))
                return -1;

            if (!Guid.TryParse(bindingId, out Guid targetBindingId))
                return -1;

            int bindingCount = action.bindings.Count;

            for (int i = 0; i < bindingCount; i++)
            {
                if (action.bindings[i].id == targetBindingId)
                    return i;
            }

            return -1;
        }

        private void EnsureCachedDelegates()
        {
            _cachedRebindCancelAction ??= HandleRebindCanceledOperation; // COLD ALLOC: Action<RebindingOperation>[1] - cached cancel callback - owner: RebindingManager
            _cachedRebindCompleteAction ??= HandleRebindCompletedOperation; // COLD ALLOC: Action<RebindingOperation>[1] - cached complete callback - owner: RebindingManager
            _cachedConflictConfirmAction ??= ConfirmPendingConflict; // COLD ALLOC: Action[1] - cached conflict confirm callback - owner: RebindingManager
            _cachedConflictCancelAction ??= CancelPendingConflict; // COLD ALLOC: Action[1] - cached conflict cancel callback - owner: RebindingManager
        }

        private void TryColdBootstrapTelemetry()
        {
            if (_telemetryBootstrapped)
                return;

            _dataVault = GlobalRegistry.DataVault;
            _telemetryBootstrapped = ControlRemapper.TryBootstrapTelemetry(
                _dataVault,
                out _telemetryRingHandle,
                out _telemetryCursorHandle);
        }

        private void RecordControlRemapTelemetry(in InputBindingTelemetryEntry entry)
        {
            if (!_telemetryBootstrapped)
                TryColdBootstrapTelemetry();

            ControlRemapper.RecordTelemetry(_dataVault, in _telemetryRingHandle, in _telemetryCursorHandle, in entry);
        }

        private void DumpControlRemapTelemetryOnFault(in InputBindingTelemetryEntry entry)
        {
            if (entry.Result == InputBindingTelemetryResult.Success ||
                entry.Result == InputBindingTelemetryResult.NoOverrides ||
                entry.Result == InputBindingTelemetryResult.FileMissing)
            {
                return;
            }

            if (!_telemetryBootstrapped)
                TryColdBootstrapTelemetry();

            string path = Path.Combine(Directory.GetCurrentDirectory(), AgentTelemetryDumpRelativePath);
            ControlRemapper.TryDumpTelemetry(_dataVault, in _telemetryRingHandle, path);
        }

        private void ClearActiveRebindContext()
        {
            _activeAction = null;
            _activeInputManager = null;
            _activeActionName = null;
            _activeActionMap = null;
            _activePreviousOverridePath = null;
            _activeBindingIndex = 0;
            _activeActionWasEnabled = false;
        }

        private void ClearPendingConflictContext()
        {
            _pendingConflictAction = null;
            _pendingConflictVictimAction = null;
            _pendingConflictActionName = null;
            _pendingConflictActionMap = null;
            _pendingConflictDisplay = null;
            _pendingConflictPreviousOverridePath = null;
            _pendingConflictVictimPreviousOverridePath = null;
            _pendingConflictBindingIndex = 0;
            _pendingConflictVictimBindingIndex = 0;
            _pendingConflictWasEnabled = false;
        }

        private void CancelRebindOrPendingConflict()
        {
            CancelRebind();
        }

        private void TryRegisterService()
        {
            if (_registeredService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterInputBindingService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.InputBinding, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            if (ReferenceEquals(GlobalRegistry.InputBinding, this))
                GlobalRegistry.UnregisterInputBindingService(this);

            _registeredService = false;
        }

        private INativeInputManagerRuntime ResolveNativeInputManager()
        {
            return _nativeInputManager;
        }

        private static bool TryClearRuntimeBindingOverrides(INativeInputManagerRuntime inputManager)
        {
            if (inputManager == null)
                return false;

            try
            {
                return inputManager.TryClearBindingOverrides();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private bool TryCaptureRuntimeOverrideRollback(INativeInputManagerRuntime inputManager, out int rollbackCount)
        {
            rollbackCount = 0;
            if (inputManager == null)
                return false;

            InputActionMap playerMap;
            InputActionMap uiMap;
            try
            {
                playerMap = inputManager.GetActionMap("Player");
                uiMap = inputManager.GetActionMap("UI");
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            bool captured = TryCaptureRuntimeOverrideRollbackFromMap(playerMap, ref rollbackCount) &&
                            TryCaptureRuntimeOverrideRollbackFromMap(uiMap, ref rollbackCount);
            if (!captured)
                ClearRuntimeOverrideRollback(rollbackCount);
            return captured;
        }

        private bool TryCaptureRuntimeOverrideRollbackFromMap(InputActionMap map, ref int rollbackCount)
        {
            if (map == null)
                return true;

            int actionCount = map.actions.Count;
            for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                InputAction action = map.actions[actionIndex];
                if (action == null)
                    continue;

                int bindingCount = action.bindings.Count;
                for (int bindingIndex = 0; bindingIndex < bindingCount; bindingIndex++)
                {
                    InputBinding binding = action.bindings[bindingIndex];
                    if (binding.overridePath == null)
                        continue;

                    if (rollbackCount >= _clearRollbackRecords.Length)
                        return false;

                    _clearRollbackRecords[rollbackCount++] = new RuntimeOverrideRollbackRecord
                    {
                        Action = action,
                        OverridePath = binding.overridePath,
                        BindingIndex = bindingIndex
                    };
                }
            }

            return true;
        }

        private bool TryRestoreRuntimeOverrideRollback(int rollbackCount)
        {
            bool restored = true;
            int safeCount = Math.Min(rollbackCount, _clearRollbackRecords.Length);
            for (int i = 0; i < safeCount; i++)
            {
                RuntimeOverrideRollbackRecord record = _clearRollbackRecords[i];
                restored &= TryRestoreBindingOverride(record.Action, record.BindingIndex, record.OverridePath);
            }

            return restored;
        }

        private void ClearRuntimeOverrideRollback(int rollbackCount)
        {
            int safeCount = Math.Min(rollbackCount, _clearRollbackRecords.Length);
            for (int i = 0; i < safeCount; i++)
                _clearRollbackRecords[i] = default;
        }

        private void TryLoadInitialOverrides()
        {
            if (!loadOverridesOnAwake || _initialOverridesLoadAttempted || _nativeInputManager == null)
                return;

            _initialOverridesLoadAttempted = true;
            LoadOverrides();
        }

        private void DisposeActiveRebind()
        {
            if (_activeRebind == null) return;
            _activeRebind.Dispose();
            _activeRebind = null;
        }

        private void FailStartInteractiveRebind(InputAction action, bool wasEnabled)
        {
            DisposeActiveRebind();
            TryRestoreActionEnabled(action, wasEnabled);
            ClearActiveRebindContext();
            LogWarning("Rebind start failed; restored action state.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Log(string message)
        {
            if (!verboseLogging) return;
            Hecton8.Core.H8Debug.Log(message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogWarning(string message)
        {
            Hecton8.Core.H8Debug.LogWarning(message);
        }

        /// <summary>
        /// TASK 16: Detects if the new binding conflicts with another action.
        /// Returns the name of the conflicting action, or null if no conflict.
        /// SAFETY: Validates action map and actions collection before iteration.
        /// </summary>
        private bool TryDetectConflict(
            InputAction currentAction,
            int currentBindingIndex,
            string currentActionMap,
            out string conflictingActionName,
            out InputAction conflictingAction,
            out int conflictingBindingIndex)
        {
            conflictingActionName = null;
            conflictingAction = null;
            conflictingBindingIndex = -1;

            INativeInputManagerRuntime inputManager = ResolveNativeInputManager();
            if (currentAction == null || inputManager == null)
                return false;

            int currentBindingCount = currentAction.bindings.Count;
            if (currentBindingIndex < 0 || currentBindingIndex >= currentBindingCount)
                return false;

            string newPath = currentAction.bindings[currentBindingIndex].effectivePath;

            if (string.IsNullOrEmpty(newPath))
                return false;

            // Check all actions in the same action map for conflicts
            InputActionMap actionMap = inputManager.GetActionMap(currentActionMap);
            if (actionMap == null)
                return false;

            // SAFETY: ReadOnlyArray is a struct; only the count check is meaningful here.
            if (actionMap.actions.Count == 0)
                return false;

            int actionCount = actionMap.actions.Count;
            for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                InputAction action = actionMap.actions[actionIndex];
                if (action == null || action == currentAction)
                    continue;

                int bindingCount = action.bindings.Count;
                for (int i = 0; i < bindingCount; i++)
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
                        conflictingActionName = action.name;
                        conflictingAction = action;
                        conflictingBindingIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryDisableConflictingBinding(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0)
                return false;

            try
            {
                if (bindingIndex >= action.bindings.Count)
                    return false;

                action.ApplyBindingOverride(bindingIndex, string.Empty);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool TryReadActionEnabled(InputAction action, out bool enabled)
        {
            enabled = false;
            if (action == null)
                return false;

            try
            {
                enabled = action.enabled;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool TryDisableActionForRebind(InputAction action)
        {
            if (action == null)
                return false;

            try
            {
                action.Disable();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool TryRestoreActionEnabled(InputAction action, bool wasEnabled)
        {
            if (!wasEnabled || action == null)
                return true;

            try
            {
                action.Enable();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string TryGetBindingOverridePath(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return null;

            return action.bindings[bindingIndex].overridePath;
        }

        private static bool TryRestoreBindingOverride(InputAction action, int bindingIndex, string previousOverridePath)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return false;

            try
            {
                if (previousOverridePath == null)
                    action.RemoveBindingOverride(bindingIndex);
                else
                    action.ApplyBindingOverride(bindingIndex, previousOverridePath);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// TASK 16: Completes rebind after user confirms conflict resolution.
        /// </summary>
        private void CompleteRebindAfterConflictResolution(
            InputAction action,
            InputAction victimAction,
            string actionName,
            string actionMap,
            int bindingIndex,
            int victimBindingIndex,
            string display,
            string previousOverridePath,
            string victimPreviousOverridePath)
        {
            if (saveOverridesAfterRebind)
            {
                if (!SaveOverrides())
                {
                    TryRestoreBindingOverride(victimAction, victimBindingIndex, victimPreviousOverridePath);
                    TryRestoreBindingOverride(action, bindingIndex, previousOverridePath);
                    OnRebindSaveFailed?.Invoke(actionName, actionMap, bindingIndex);
                    OnRebindCanceled?.Invoke(actionName, actionMap, bindingIndex);
                    OnOverridesSaveFailed?.Invoke();
                    LogWarning("Conflict rebind auto-save failed; restored previous binding overrides.");
                    return;
                }
            }

            OnRebindCompleted?.Invoke(actionName, actionMap, bindingIndex, display);
            Log("Rebind complete after conflict resolution.");
        }

        /// <summary>
        /// TASK 16: Cancels rebind after user rejects conflict resolution.
        /// Removes the binding override to restore previous state.
        /// </summary>
        private void CancelRebindAfterConflict(
            InputAction action,
            string actionName,
            string actionMap,
            int bindingIndex,
            string previousOverridePath,
            bool wasEnabled)
        {
            if (action != null)
            {
                TryRestoreBindingOverride(action, bindingIndex, previousOverridePath);
                TryRestoreActionEnabled(action, wasEnabled);
            }

            OnRebindCanceled?.Invoke(actionName, actionMap, bindingIndex);
            Log("Rebind canceled after conflict rejection.");
        }
    }
}
