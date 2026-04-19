// ═══════════════════════════════════════════════════════════════════════════════════════════════════
// Hecton-8 Enterprise Input Manager v1.0
// ═══════════════════════════════════════════════════════════════════════════════════════════════════
// ZERO GC ALLOCATION GUARANTEE
// - All events cached at initialization
// - No lambda allocations in hot paths
// - Pre-allocated action references
// - Static delegate pattern for callbacks
// ═══════════════════════════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace Hecton8.Input
{
    /// <summary>
    /// UI-facing input display styles used by localization token expansion.
    /// </summary>
    public enum InputDisplayStyle
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    /// <summary>
    /// Enterprise-grade Input Manager with zero GC allocations.
    /// Singleton pattern with thread-safe initialization.
    /// Supports keyboard, mouse, and gamepad with full rebinding support.
    /// </summary>
    [DefaultExecutionOrder(-31000)] // Must initialize before BootstrapController singleton access.
    public class InputManager : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // SINGLETON PATTERN
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private static InputManager _instance;
        private static readonly object _lock = new object();
        private static bool _isShuttingDown;
        private HectonInputActions _generatedInputActions;
        private InputActionAsset _runtimeInputActionAsset;
        private bool _inputMapsInitialized;
        private bool _playerActionsSubscribed;
        private bool _uiActionsSubscribed;
        private bool _initialActivationComplete;
        private bool _restorePlayerInputOnEnable;
        private bool _restoreUiInputOnEnable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isShuttingDown = false;
        }
        
        public static InputManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (_isShuttingDown || !Application.isPlaying)
                        return null;

                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            if (_isShuttingDown || !Application.isPlaying)
                                return null;

                            GameObject go = new GameObject("[InputManager]");
                            _instance = go.AddComponent<InputManager>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // INPUT ACTIONS (CACHED)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        [SerializeField] private InputActionAsset _inputActionAsset;
        private InputActionMap _playerActionMap;
        private InputActionMap _uiActionMap;
        
        // Player Actions (cached references - zero GC)
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _interactAction;
        private InputAction _flashlightAction;
        private InputAction _pdaAction;
        private InputAction _toolSlot1Action;
        private InputAction _toolSlot2Action;
        private InputAction _toolSlot3Action;
        private InputAction _toolSlot4Action;
        private InputAction _primaryActionAction;
        private InputAction _secondaryActionAction;
        private InputAction _verticalMovementAction;
        private InputAction _inventoryAction;
        
        // UI Actions (cached references - zero GC)
        private InputAction _navigateAction;
        private InputAction _submitAction;
        private InputAction _cancelAction;
        private InputAction _tabNextAction;
        private InputAction _tabPreviousAction;

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // EVENTS (ZERO GC)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        // Movement Events
        public event Action<Vector2> OnMove;
        public event Action<Vector2> OnLook;
        public event Action OnJump;
        public event Action OnJumpCanceled;
        public event Action OnSprint;
        public event Action OnSprintCanceled;
        public event Action<float> OnVerticalMove;
        
        // Interaction Events
        public event Action OnInteract;
        public event Action OnFlashlight;
        public event Action OnPDA;
        public event Action OnInventory;
        
        // Tool Events
        public event Action OnToolSlot1;
        public event Action OnToolSlot2;
        public event Action OnToolSlot3;
        public event Action OnToolSlot4;
        
        // Action Events
        public event Action OnPrimaryAction;
        public event Action OnPrimaryActionCanceled;
        public event Action OnSecondaryAction;
        public event Action OnSecondaryActionCanceled;
        
        // UI Events
        public event Action<Vector2> OnNavigate;
        public event Action OnSubmit;
        public event Action OnCancel;
        public event Action OnTabNext;
        public event Action OnTabPrevious;
        public event Action<InputDisplayStyle> OnInputDisplayStyleChanged;

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        public bool IsPlayerInputEnabled => TryGetActionMapEnabled(_playerActionMap);
        public bool IsUIInputEnabled => TryGetActionMapEnabled(_uiActionMap);
        public bool CanSwitchActionMaps => !_isShuttingDown && _inputMapsInitialized && _runtimeInputActionAsset != null;
        public InputDisplayStyle CurrentDisplayStyle { get; private set; } = InputDisplayStyle.KeyboardMouse;
        
        public Vector2 MoveInput => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 LookInput => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool IsJumping => _jumpAction?.IsPressed() ?? false;
        public bool IsSprinting => _sprintAction?.IsPressed() ?? false;
        public bool IsPrimaryActionHeld => _primaryActionAction?.IsPressed() ?? false;
        public bool IsSecondaryActionHeld => _secondaryActionAction?.IsPressed() ?? false;
        public float VerticalMovementInput => _verticalMovementAction?.ReadValue<float>() ?? 0f;
        public InputActionAsset InputActionsAsset => _runtimeInputActionAsset;

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            _isShuttingDown = false;
            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
            
            InitializeInputActions();
        }

        private void OnEnable()
        {
            if (_isShuttingDown || _instance != this)
                return;

            EnsureInputActionsInitialized();

            if (!_initialActivationComplete)
                return;

            if (_restorePlayerInputOnEnable)
                SafeEnableActionMap(_playerActionMap);

            if (_restoreUiInputOnEnable && _uiActionMap != null)
                SafeEnableActionMap(_uiActionMap);
        }

        private void OnDisable()
        {
            if (_instance != this)
                return;

            _restorePlayerInputOnEnable = IsActionMapEnabledForStateCapture(_playerActionMap);
            _restoreUiInputOnEnable = IsActionMapEnabledForStateCapture(_uiActionMap);
        }

        private void Start()
        {
            _initialActivationComplete = true;
            EnablePlayerInput();
        }
        
        private void InitializeInputActions()
        {
            if (_inputMapsInitialized && IsActionMapUsable(_playerActionMap))
                return;

            ResetInputActionCaches(disposeRuntimeAsset: true);
            _inputMapsInitialized = false;

            InputActionAsset templateAsset = _inputActionAsset;
            if (templateAsset == null)
            {
                _generatedInputActions ??= new HectonInputActions(); // COLD ALLOC: HectonInputActions[1] — runtime fallback input asset wrapper — owner: InputManager
                templateAsset = _generatedInputActions.asset;
            }

            if (templateAsset == null)
            {
                Debug.LogError("[InputManager] No InputActionAsset template available.");
                return;
            }

            _runtimeInputActionAsset = CreateRuntimeInputActionAsset(templateAsset);
            if (_runtimeInputActionAsset == null)
            {
                Debug.LogError("[InputManager] Failed to create runtime InputActionAsset clone.");
                return;
            }

            _runtimeInputActionAsset.name = templateAsset.name;
            
            // Get action maps
            _playerActionMap = _runtimeInputActionAsset.FindActionMap("Player");
            _uiActionMap = _runtimeInputActionAsset.FindActionMap("UI");
            
            if (_playerActionMap == null)
            {
                Debug.LogError("[InputManager] Player action map not found in InputActionAsset!");
                return;
            }

            if (_uiActionMap == null)
            {
                // Generated fallback currently exposes only the Player map. This is a
                // supported runtime mode while the UI actions asset is being migrated.
            }
            
            // Cache all action references (zero GC)
            CachePlayerActions();
            if (_uiActionMap != null)
                CacheUIActions();
            
            // Subscribe to all actions (zero GC - static delegates)
            SubscribeToPlayerActions();
            if (_uiActionMap != null)
                SubscribeToUIActions();

            _inputMapsInitialized = true;
        }
        
        private void CachePlayerActions()
        {
            _moveAction = _playerActionMap.FindAction("Movement");
            _lookAction = _playerActionMap.FindAction("Look");
            _jumpAction = _playerActionMap.FindAction("Jump");
            _sprintAction = _playerActionMap.FindAction("Sprint");
            _interactAction = _playerActionMap.FindAction("Interact");
            _flashlightAction = _playerActionMap.FindAction("Flashlight");
            _pdaAction = _playerActionMap.FindAction("PDA");
            _toolSlot1Action = _playerActionMap.FindAction("ToolSlot1");
            _toolSlot2Action = _playerActionMap.FindAction("ToolSlot2");
            _toolSlot3Action = _playerActionMap.FindAction("ToolSlot3");
            _toolSlot4Action = _playerActionMap.FindAction("ToolSlot4");
            _primaryActionAction = _playerActionMap.FindAction("PrimaryAction");
            _secondaryActionAction = _playerActionMap.FindAction("SecondaryAction");
            _verticalMovementAction = _playerActionMap.FindAction("VerticalMovement");
            _inventoryAction = _playerActionMap.FindAction("Inventory");
        }
        
        private void CacheUIActions()
        {
            _navigateAction = _uiActionMap.FindAction("Navigate");
            _submitAction = _uiActionMap.FindAction("Submit");
            _cancelAction = _uiActionMap.FindAction("Cancel");
            _tabNextAction = _uiActionMap.FindAction("TabNext");
            _tabPreviousAction = _uiActionMap.FindAction("TabPrevious");
        }

        private InputActionAsset CreateRuntimeInputActionAsset(InputActionAsset templateAsset)
        {
            if (templateAsset == null)
                return null;

            try
            {
                return Instantiate(templateAsset); // COLD ALLOC: InputActionAsset[1] — detached runtime input asset clone — owner: InputManager
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputManager] Runtime InputActionAsset clone failed: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // EVENT SUBSCRIPTION (ZERO GC)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private void SubscribeToPlayerActions()
        {
            if (_playerActionsSubscribed)
                return;

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            
            _lookAction.performed += OnLookPerformed;
            
            _jumpAction.performed += OnJumpPerformed;
            _jumpAction.canceled += OnJumpCanceledPerformed;
            
            _sprintAction.performed += OnSprintPerformed;
            _sprintAction.canceled += OnSprintCanceledPerformed;
            
            _interactAction.performed += OnInteractPerformed;
            _flashlightAction.performed += OnFlashlightPerformed;
            _pdaAction.performed += OnPDAPerformed;
            _inventoryAction.performed += OnInventoryPerformed;
            
            _toolSlot1Action.performed += OnToolSlot1Performed;
            _toolSlot2Action.performed += OnToolSlot2Performed;
            _toolSlot3Action.performed += OnToolSlot3Performed;
            _toolSlot4Action.performed += OnToolSlot4Performed;
            
            _primaryActionAction.performed += OnPrimaryActionPerformed;
            _primaryActionAction.canceled += OnPrimaryActionCanceledPerformed;
            
            _secondaryActionAction.performed += OnSecondaryActionPerformed;
            _secondaryActionAction.canceled += OnSecondaryActionCanceledPerformed;
            
            _verticalMovementAction.performed += OnVerticalMovementPerformed;
            _verticalMovementAction.canceled += OnVerticalMovementCanceled;

            _playerActionsSubscribed = true;
        }
        
        private void SubscribeToUIActions()
        {
            if (_uiActionsSubscribed)
                return;

            _navigateAction.performed += OnNavigatePerformed;
            _submitAction.performed += OnSubmitPerformed;
            _cancelAction.performed += OnCancelPerformed;
            _tabNextAction.performed += OnTabNextPerformed;
            _tabPreviousAction.performed += OnTabPreviousPerformed;

            _uiActionsSubscribed = true;
        }

        private void UnsubscribeFromPlayerActions()
        {
            if (!_playerActionsSubscribed)
                return;

            if (_moveAction != null)
            {
                _moveAction.performed -= OnMovePerformed;
                _moveAction.canceled -= OnMoveCanceled;
            }

            if (_lookAction != null)
                _lookAction.performed -= OnLookPerformed;

            if (_jumpAction != null)
            {
                _jumpAction.performed -= OnJumpPerformed;
                _jumpAction.canceled -= OnJumpCanceledPerformed;
            }

            if (_sprintAction != null)
            {
                _sprintAction.performed -= OnSprintPerformed;
                _sprintAction.canceled -= OnSprintCanceledPerformed;
            }

            if (_interactAction != null)
                _interactAction.performed -= OnInteractPerformed;

            if (_flashlightAction != null)
                _flashlightAction.performed -= OnFlashlightPerformed;

            if (_pdaAction != null)
                _pdaAction.performed -= OnPDAPerformed;

            if (_inventoryAction != null)
                _inventoryAction.performed -= OnInventoryPerformed;

            if (_toolSlot1Action != null)
                _toolSlot1Action.performed -= OnToolSlot1Performed;

            if (_toolSlot2Action != null)
                _toolSlot2Action.performed -= OnToolSlot2Performed;

            if (_toolSlot3Action != null)
                _toolSlot3Action.performed -= OnToolSlot3Performed;

            if (_toolSlot4Action != null)
                _toolSlot4Action.performed -= OnToolSlot4Performed;

            if (_primaryActionAction != null)
            {
                _primaryActionAction.performed -= OnPrimaryActionPerformed;
                _primaryActionAction.canceled -= OnPrimaryActionCanceledPerformed;
            }

            if (_secondaryActionAction != null)
            {
                _secondaryActionAction.performed -= OnSecondaryActionPerformed;
                _secondaryActionAction.canceled -= OnSecondaryActionCanceledPerformed;
            }

            if (_verticalMovementAction != null)
            {
                _verticalMovementAction.performed -= OnVerticalMovementPerformed;
                _verticalMovementAction.canceled -= OnVerticalMovementCanceled;
            }

            _playerActionsSubscribed = false;
        }

        private void UnsubscribeFromUIActions()
        {
            if (!_uiActionsSubscribed)
                return;

            if (_navigateAction != null)
                _navigateAction.performed -= OnNavigatePerformed;

            if (_submitAction != null)
                _submitAction.performed -= OnSubmitPerformed;

            if (_cancelAction != null)
                _cancelAction.performed -= OnCancelPerformed;

            if (_tabNextAction != null)
                _tabNextAction.performed -= OnTabNextPerformed;

            if (_tabPreviousAction != null)
                _tabPreviousAction.performed -= OnTabPreviousPerformed;

            _uiActionsSubscribed = false;
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // INPUT CALLBACKS (ZERO GC)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        // Movement Callbacks
        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnMove?.Invoke(context.ReadValue<Vector2>());
        }
        private void OnMoveCanceled(InputAction.CallbackContext context) => OnMove?.Invoke(Vector2.zero);
        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnLook?.Invoke(context.ReadValue<Vector2>());
        }
        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnJump?.Invoke();
        }
        private void OnJumpCanceledPerformed(InputAction.CallbackContext context) => OnJumpCanceled?.Invoke();
        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnSprint?.Invoke();
        }
        private void OnSprintCanceledPerformed(InputAction.CallbackContext context) => OnSprintCanceled?.Invoke();
        private void OnVerticalMovementPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnVerticalMove?.Invoke(context.ReadValue<float>());
        }
        private void OnVerticalMovementCanceled(InputAction.CallbackContext context) => OnVerticalMove?.Invoke(0f);
        
        // Interaction Callbacks
        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnInteract?.Invoke();
        }
        private void OnFlashlightPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnFlashlight?.Invoke();
        }
        private void OnPDAPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnPDA?.Invoke();
        }
        private void OnInventoryPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnInventory?.Invoke();
        }
        
        // Tool Callbacks
        private void OnToolSlot1Performed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnToolSlot1?.Invoke();
        }
        private void OnToolSlot2Performed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnToolSlot2?.Invoke();
        }
        private void OnToolSlot3Performed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnToolSlot3?.Invoke();
        }
        private void OnToolSlot4Performed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnToolSlot4?.Invoke();
        }
        
        // Action Callbacks
        private void OnPrimaryActionPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnPrimaryAction?.Invoke();
        }
        private void OnPrimaryActionCanceledPerformed(InputAction.CallbackContext context) => OnPrimaryActionCanceled?.Invoke();
        private void OnSecondaryActionPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnSecondaryAction?.Invoke();
        }
        private void OnSecondaryActionCanceledPerformed(InputAction.CallbackContext context) => OnSecondaryActionCanceled?.Invoke();
        
        // UI Callbacks
        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnNavigate?.Invoke(context.ReadValue<Vector2>());
        }
        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnSubmit?.Invoke();
        }
        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnCancel?.Invoke();
        }
        private void OnTabNextPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnTabNext?.Invoke();
        }
        private void OnTabPreviousPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnTabPrevious?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        public void EnablePlayerInput()
        {
            if (!EnsureInputActionsInitialized())
                return;

            _restorePlayerInputOnEnable = true;
            SafeEnableActionMap(_playerActionMap);
        }
        
        public void DisablePlayerInput()
        {
            _restorePlayerInputOnEnable = false;
            SafeDisableActionMap(_playerActionMap);
        }
        
        public void EnableUIInput()
        {
            if (!EnsureInputActionsInitialized() || _uiActionMap == null)
                return;

            _restoreUiInputOnEnable = true;
            SafeEnableActionMap(_uiActionMap);
        }
        
        public void DisableUIInput()
        {
            if (_uiActionMap == null)
                return;

            _restoreUiInputOnEnable = false;
            SafeDisableActionMap(_uiActionMap);
        }
        
        public void SwitchToPlayerInput()
        {
            if (!EnsureInputActionsInitialized())
                return;

            DisableUIInput();
            EnablePlayerInput();
        }
        
        public void SwitchToUIInput()
        {
            if (!EnsureInputActionsInitialized())
                return;

            if (_uiActionMap == null)
            {
                // Never strand gameplay input just because the UI map is absent.
                EnablePlayerInput();
                return;
            }

            DisablePlayerInput();
            EnableUIInput();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // REBINDING SUPPORT
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        public InputAction GetAction(string actionName, string actionMap = "Player")
        {
            if (string.IsNullOrWhiteSpace(actionName) || !EnsureInputActionsInitialized())
                return null;

            try
            {
                if (actionMap == "Player")
                    return _playerActionMap?.FindAction(actionName);
                else if (actionMap == "UI")
                    return _uiActionMap?.FindAction(actionName);
            }
            catch (Exception)
            {
                return null;
            }
            
            return null;
        }

        public InputActionMap GetActionMap(string actionMap = "Player")
        {
            if (!EnsureInputActionsInitialized())
                return null;

            try
            {
                if (actionMap == "Player")
                    return _playerActionMap;
                if (actionMap == "UI")
                    return _uiActionMap;
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }
        
        public string GetBindingDisplayString(string actionName, string actionMap = "Player", int bindingIndex = 0)
        {
            InputAction action = GetAction(actionName, actionMap);
            if (action == null)
                return string.Empty;

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                bindingIndex = GetPreferredBindingIndex(action, CurrentDisplayStyle);

            if (!TryGetBindingDisplayStringSafe(action, bindingIndex, out string display))
                return string.Empty;

            return display;
        }

        public bool TryGetBindingMarkupForToken(string token, out string markup)
        {
            markup = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            if (!TryResolveTokenBinding(token, out string actionName, out string actionMap))
                return false;

            string display = GetBindingDisplayString(actionName, actionMap, -1);
            if (string.IsNullOrWhiteSpace(display))
                return false;

            markup = FormatBindingChip(display, CurrentDisplayStyle);
            return !string.IsNullOrWhiteSpace(markup);
        }

        public static bool TryGetBindingDisplayStringSafe(InputAction action, int bindingIndex, out string display)
        {
            display = string.Empty;
            if (action == null || bindingIndex < 0)
                return false;

            InputBinding binding;
            try
            {
                if (bindingIndex >= action.bindings.Count)
                    return false;

                binding = action.bindings[bindingIndex];
            }
            catch
            {
                return false;
            }

            string path = binding.effectivePath;
            if (string.IsNullOrWhiteSpace(path))
                path = !string.IsNullOrWhiteSpace(binding.overridePath) ? binding.overridePath : binding.path;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                display = action.GetBindingDisplayString(bindingIndex);
            }
            catch
            {
                display = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(display))
            {
                try
                {
                    display = InputControlPath.ToHumanReadableString(
                        path,
                        InputControlPath.HumanReadableStringOptions.OmitDevice);
                }
                catch
                {
                    display = string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(display))
                display = path;

            return !string.IsNullOrWhiteSpace(display);
        }

        private static int GetFirstDisplayableBindingIndex(InputAction action)
        {
            if (action == null)
                return -1;

            try
            {
                int count = action.bindings.Count;
                for (int i = 0; i < count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (!binding.isComposite && !binding.isPartOfComposite)
                        return i;
                }

                return count > 0 ? 0 : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static int GetPreferredBindingIndex(InputAction action, InputDisplayStyle displayStyle)
        {
            if (action == null)
                return -1;

            try
            {
                int count = action.bindings.Count;
                for (int i = 0; i < count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (!IsBindingSuitableForDisplay(binding, displayStyle))
                        continue;

                    return i;
                }
            }
            catch
            {
                return GetFirstDisplayableBindingIndex(action);
            }

            return GetFirstDisplayableBindingIndex(action);
        }

        private static bool IsBindingSuitableForDisplay(InputBinding binding, InputDisplayStyle displayStyle)
        {
            string path = binding.effectivePath;
            if (string.IsNullOrWhiteSpace(path))
                path = !string.IsNullOrWhiteSpace(binding.overridePath) ? binding.overridePath : binding.path;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (displayStyle == InputDisplayStyle.Gamepad)
                return path.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;

            return path.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatBindingChip(string display, InputDisplayStyle displayStyle)
        {
            string sanitized = string.IsNullOrWhiteSpace(display) ? "?" : display.Trim().ToUpperInvariant();
            string prefix = displayStyle == InputDisplayStyle.Gamepad ? "\u25C6" : "\u2328";
            return $"<b><color=#AEE8FF>{prefix}</color> {sanitized}</b>";
        }

        private static bool TryResolveTokenBinding(string token, out string actionName, out string actionMap)
        {
            string normalized = token.Trim().ToLowerInvariant();
            actionName = string.Empty;
            actionMap = "Player";

            switch (normalized)
            {
                case "interact":
                    actionName = "Interact";
                    return true;
                case "inventory":
                    actionName = "Inventory";
                    return true;
                case "pda":
                    actionName = "PDA";
                    return true;
                case "flashlight":
                    actionName = "Flashlight";
                    return true;
                case "primary":
                    actionName = "PrimaryAction";
                    return true;
                case "secondary":
                    actionName = "SecondaryAction";
                    return true;
                case "navigate":
                    actionName = "Navigate";
                    actionMap = "UI";
                    return true;
                case "submit":
                    actionName = "Submit";
                    actionMap = "UI";
                    return true;
                case "cancel":
                    actionName = "Cancel";
                    actionMap = "UI";
                    return true;
                default:
                    return false;
            }
        }

        private void CaptureInputDisplayStyle(InputAction.CallbackContext context)
        {
            InputDisplayStyle nextStyle = ResolveDisplayStyle(context.control);
            if (CurrentDisplayStyle == nextStyle)
                return;

            CurrentDisplayStyle = nextStyle;
            OnInputDisplayStyleChanged?.Invoke(nextStyle);
        }

        private static InputDisplayStyle ResolveDisplayStyle(InputControl control)
        {
            InputDevice device = control?.device;
            if (device is Gamepad)
                return InputDisplayStyle.Gamepad;

            return InputDisplayStyle.KeyboardMouse;
        }

        public string SaveBindingOverridesAsJson()
        {
            if (_runtimeInputActionAsset == null) return string.Empty;
            return _runtimeInputActionAsset.SaveBindingOverridesAsJson();
        }

        public void LoadBindingOverridesFromJson(string json)
        {
            if (_runtimeInputActionAsset == null || string.IsNullOrEmpty(json)) return;
            _runtimeInputActionAsset.LoadBindingOverridesFromJson(json);
        }

        public void ClearBindingOverrides()
        {
            if (_runtimeInputActionAsset == null) return;
            _runtimeInputActionAsset.RemoveAllBindingOverrides();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private void ReinitializeInputActions()
        {
            if (_isShuttingDown)
                return;

            ResetInputActionCaches(disposeRuntimeAsset: true);
            InitializeInputActions();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _isShuttingDown = true;

            ResetInputActionCaches(disposeRuntimeAsset: true);
            _generatedInputActions?.Dispose();
            _generatedInputActions = null;

            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        private void SafeEnableActionMap(InputActionMap actionMap)
        {
            if (!IsActionMapUsable(actionMap))
                return;

            try
            {
                if (actionMap.enabled)
                    return;

                actionMap.Enable();
            }
            catch (InvalidOperationException)
            {
                HandleStaleActionMap(actionMap);
            }
            catch (ArgumentOutOfRangeException)
            {
                HandleStaleActionMap(actionMap);
            }
            catch (Exception)
            {
                HandleStaleActionMap(actionMap);
            }
        }

        private void SafeDisableActionMap(InputActionMap actionMap)
        {
            if (!IsActionMapUsable(actionMap))
                return;

            try
            {
                if (!actionMap.enabled)
                    return;

                actionMap.Disable();
            }
            catch (InvalidOperationException)
            {
                HandleStaleActionMap(actionMap);
            }
            catch (ArgumentOutOfRangeException)
            {
                HandleStaleActionMap(actionMap);
            }
            catch (Exception)
            {
                HandleStaleActionMap(actionMap);
            }
        }

        private void SafeDisableActionMapForTeardown(InputActionMap actionMap)
        {
            if (!IsActionMapOwnedByRuntimeAsset(actionMap))
                return;

            try
            {
                actionMap.Disable();
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentOutOfRangeException)
            {
            }
            catch (Exception)
            {
            }
        }

        private void HandleStaleActionMap(InputActionMap actionMap)
        {
            ResetInputActionCaches(disposeRuntimeAsset: true);

            if (_isShuttingDown)
                return;

            InitializeInputActions();
            if (!_inputMapsInitialized || !_initialActivationComplete)
                return;

            if (_restorePlayerInputOnEnable)
                SafeEnableActionMap(_playerActionMap);

            if (_restoreUiInputOnEnable && _uiActionMap != null)
                SafeEnableActionMap(_uiActionMap);
        }

        private static bool IsActionMapEnabledForStateCapture(InputActionMap actionMap)
        {
            if (actionMap == null)
                return false;

            try
            {
                return actionMap.enabled;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsActionMapUsable(InputActionMap actionMap)
        {
            if (!_inputMapsInitialized || actionMap == null || _isShuttingDown || _runtimeInputActionAsset == null)
                return false;

            try
            {
                if (!ReferenceEquals(actionMap.asset, _runtimeInputActionAsset))
                {
                    HandleStaleActionMap(actionMap);
                    return false;
                }
            }
            catch (Exception)
            {
                HandleStaleActionMap(actionMap);
                return false;
            }

            return true;
        }

        private bool EnsureInputActionsInitialized()
        {
            if (_isShuttingDown)
                return false;

            if (_inputMapsInitialized && IsActionMapUsable(_playerActionMap))
                return true;

            InitializeInputActions();
            return _inputMapsInitialized && IsActionMapUsable(_playerActionMap);
        }

        private bool TryGetActionMapEnabled(InputActionMap actionMap)
        {
            if (actionMap == null || _isShuttingDown)
                return false;

            try
            {
                if (_runtimeInputActionAsset != null && !ReferenceEquals(actionMap.asset, _runtimeInputActionAsset))
                {
                    HandleStaleActionMap(actionMap);
                    return false;
                }

                return actionMap.enabled;
            }
            catch (InvalidOperationException)
            {
                HandleStaleActionMap(actionMap);
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                HandleStaleActionMap(actionMap);
                return false;
            }
            catch (Exception)
            {
                HandleStaleActionMap(actionMap);
                return false;
            }
        }

        private void ResetInputActionCaches(bool disposeRuntimeAsset)
        {
            UnsubscribeFromPlayerActions();
            UnsubscribeFromUIActions();

            SafeDisableActionMapForTeardown(_playerActionMap);
            SafeDisableActionMapForTeardown(_uiActionMap);

            _inputMapsInitialized = false;
            _playerActionMap = null;
            _uiActionMap = null;
            _moveAction = null;
            _lookAction = null;
            _jumpAction = null;
            _sprintAction = null;
            _interactAction = null;
            _flashlightAction = null;
            _pdaAction = null;
            _toolSlot1Action = null;
            _toolSlot2Action = null;
            _toolSlot3Action = null;
            _toolSlot4Action = null;
            _primaryActionAction = null;
            _secondaryActionAction = null;
            _verticalMovementAction = null;
            _inventoryAction = null;
            _navigateAction = null;
            _submitAction = null;
            _cancelAction = null;
            _tabNextAction = null;
            _tabPreviousAction = null;

            if (!disposeRuntimeAsset)
                return;

            DisposeRuntimeInputActionAsset();
        }

        private void DisposeRuntimeInputActionAsset()
        {
            if (_runtimeInputActionAsset == null)
                return;

            if (Application.isPlaying)
                Destroy(_runtimeInputActionAsset);
            else
                DestroyImmediate(_runtimeInputActionAsset);

            _runtimeInputActionAsset = null;
        }

        private bool IsActionMapOwnedByRuntimeAsset(InputActionMap actionMap)
        {
            if (actionMap == null || _runtimeInputActionAsset == null)
                return false;

            try
            {
                return ReferenceEquals(actionMap.asset, _runtimeInputActionAsset);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
