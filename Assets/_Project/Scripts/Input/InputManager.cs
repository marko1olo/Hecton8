// ═══════════════════════════════════════════════════════════════════════════════════════════════════
// Hecton-8 Enterprise Input Manager v1.0
// ═══════════════════════════════════════════════════════════════════════════════════════════════════
// ZERO GC ALLOCATION GUARANTEE
// - All events cached at initialization
// - No lambda allocations in hot paths
// - Pre-allocated action references
// - Static delegate pattern for callbacks
// ═══════════════════════════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;

namespace Hecton8.Input
{
    /// <summary>
    /// UI-facing input display styles used by localization token expansion.
    /// </summary>
    public enum InputDisplayStyle
    {
        KeyboardMouse = 0,
        Gamepad = 1,
        SteamDeck = 2,
        XRTouch = 3
    }

    /// <summary>
    /// Enterprise-grade Input Manager with zero GC allocations.
    /// Registry-owned input service initialized by the bootstrapper.
    /// Supports keyboard, mouse, and gamepad with full rebinding support.
    /// </summary>
    [DefaultExecutionOrder(-31000)] // Must initialize before bootstrap input consumers.
    public class InputManager : MonoBehaviour, INativeInputManagerRuntime
    {
        private const string GeneratedInputActionsTypeName = "HectonInputActions, Hecton8.Input.Generated";

        private enum InputRecoveryState : byte
        {
            Stable = 0,
            AwaitingDeviceReconnect = 1,
            RebuildPending = 2,
            Rebuilding = 3
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // REGISTRY OWNERSHIP
        // ═══════════════════════════════════════════════════════════════════════════════════════════

        private bool _serviceRegistered;
        private bool _serviceShuttingDown;
        private bool _serviceShutdownComplete;
        internal static InputManager ActiveRuntimeInstance { get; private set; }
        // COLD ALLOC: string[36] — cached single-character binding labels — owner: InputManager
        private static readonly string[] SingleCharacterBindingLabels =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J",
            "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T",
            "U", "V", "W", "X", "Y", "Z"
        };
        private const string KeyboardBindingChipFallback = "<b><color=#AEE8FF>KBD</color> KEY</b>";
        private const string GamepadBindingChipFallback = "<b><color=#AEE8FF>PAD</color> KEY</b>";
        private const string XrBindingChipFallback = "<b><color=#AEE8FF>XR</color> KEY</b>";

        private IInputActionCollection2 _generatedInputActions;
        private InputActionAsset _runtimeInputActionAsset;
        private bool _inputMapsInitialized;
        private bool _initialActivationComplete;
        private bool _restorePlayerInputOnEnable;
        private bool _restoreUiInputOnEnable;
        private bool _deviceChangeSubscribed;
        private bool _processingInputRecovery;
        private int _lastDisplayDeviceId;
        private int _connectedGamepadCount;
        private int _connectedXRControllerCount;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private float _verticalMovementInput;
        private bool _isJumping;
        private bool _isSprinting;
        private bool _isPrimaryActionHeld;
        private bool _isSecondaryActionHeld;
        private InputRecoveryState _inputRecoveryState;

        // COLD ALLOC: Dictionary<int, InputDisplayStyle>[8] — cached device-display-style lookup for input callbacks — owner: InputManager
        private readonly Dictionary<int, InputDisplayStyle> _displayStyleByDeviceId = new Dictionary<int, InputDisplayStyle>(8);

        public static bool TryValidateRuntimeConfiguration(out string message)
        {
#if !ENABLE_INPUT_SYSTEM
            message =
                "BIOS ERROR 0xINPUT\nEXPECTED: Input System Package (New)\nDETECTED: ENABLE_INPUT_SYSTEM missing\nACTION: Enable the Input System package before boot.";
            return false;
#else
#if ENABLE_LEGACY_INPUT_MANAGER
            message =
                "BIOS ERROR 0xINPUT\nEXPECTED: Active Input Handling = Input System Package (New)\nDETECTED: Legacy Input Manager compile path is still active\nACTION: Set Active Input Handling to Input System Package (New).";
            return false;
#else
            if (InputSystem.settings == null)
            {
                message =
                    "BIOS ERROR 0xINPUT\nEXPECTED: InputSystem settings asset\nDETECTED: null runtime settings\nACTION: Restore the Input System package configuration before boot.";
                return false;
            }

            message = string.Empty;
            return true;
#endif
#endif
        }
        
        public ServiceHeartbeatState HeartbeatState =>
            _serviceShuttingDown
                ? ServiceHeartbeatState.Shutdown
                : _serviceRegistered && _inputMapsInitialized
                    ? ServiceHeartbeatState.Ready
                    : ServiceHeartbeatState.Booting;

        public bool IsServiceReady => _serviceRegistered && !_serviceShuttingDown && _inputMapsInitialized;

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
        private InputAction _pauseAction;
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
        private InputAction _uiModuleSubmitAction;
        private InputAction _uiModuleCancelAction;
        private InputAction _uiPointAction;
        private InputAction _uiClickAction;
        private InputAction _uiMiddleClickAction;
        private InputAction _uiRightClickAction;
        private InputAction _uiScrollWheelAction;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private InputAction _debugToggleBlackBoxDashboardAction;
        private InputAction _debugToggleEngineHealthOverlayAction;
#endif

        private InputActionReference _uiModuleMoveReference;
        private InputActionReference _uiModuleSubmitReference;
        private InputActionReference _uiModuleCancelReference;
        private InputActionReference _uiPointReference;
        private InputActionReference _uiClickReference;
        private InputActionReference _uiMiddleClickReference;
        private InputActionReference _uiRightClickReference;
        private InputActionReference _uiScrollWheelReference;

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
        public event Action OnPause;
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
        public event Action<byte> OnInputDisplayStyleCodeChanged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public event Action OnDebugToggleBlackBoxDashboard;
        public event Action OnDebugToggleEngineHealthOverlay;
#endif

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        public bool IsPlayerInputEnabled => TryGetActionMapEnabled(_playerActionMap);
        public bool IsUIInputEnabled => TryGetActionMapEnabled(_uiActionMap);
        public bool CanSwitchActionMaps => _serviceRegistered && !_serviceShuttingDown && _inputMapsInitialized && _runtimeInputActionAsset != null;
        public InputDisplayStyle CurrentDisplayStyle { get; private set; } = InputDisplayStyle.KeyboardMouse;
        public byte CurrentDisplayStyleCode => (byte)CurrentDisplayStyle;
        
        public Vector2 MoveInput => _moveInput;
        public Vector2 LookInput => _lookInput;
        public bool IsJumping => _isJumping;
        public bool IsSprinting => _isSprinting;
        public bool IsPrimaryActionHeld => _isPrimaryActionHeld;
        public bool IsSecondaryActionHeld => _isSecondaryActionHeld;
        public float VerticalMovementInput => _verticalMovementInput;
        public InputActionAsset InputActionsAsset => _runtimeInputActionAsset;
        internal InputAction UiSubmitAction => _submitAction;

        public bool TryReadUiScrollWheel(out Vector2 scrollDelta)
        {
            scrollDelta = Vector2.zero;
            if (_uiScrollWheelAction == null || !TryGetActionMapEnabled(_uiActionMap))
                return false;

            scrollDelta = _uiScrollWheelAction.ReadValue<Vector2>();
            return scrollDelta.sqrMagnitude > 0.000001f;
        }

        public bool TryReadUiPoint(out Vector2 point)
        {
            point = Vector2.zero;
            if (_uiPointAction == null || !TryGetActionMapEnabled(_uiActionMap))
                return false;

            point = _uiPointAction.ReadValue<Vector2>();
            return point.x >= 0f && point.y >= 0f;
        }

        public bool TryValidateRuntimeActions(out string message)
        {
            if (!EnsureInputActionsInitialized())
            {
                message =
                    "BIOS ERROR 0xINPUT\nEXPECTED: Runtime action asset initialized\nDETECTED: InputManager action initialization failed\nACTION: Repair the HECTON-8 input action maps before boot.";
                return false;
            }

            if (_playerActionMap == null)
            {
                message =
                    "BIOS ERROR 0xINPUT\nEXPECTED: Player action map\nDETECTED: missing Player map\nACTION: Restore the player action map before boot.";
                return false;
            }

            if (_uiActionMap == null)
            {
                message =
                    "BIOS ERROR 0xINPUT\nEXPECTED: UI action map\nDETECTED: missing UI map\nACTION: Restore the UI action map before boot.";
                return false;
            }

            if (_moveAction == null ||
                _lookAction == null ||
                _interactAction == null ||
                _pdaAction == null ||
                _pauseAction == null ||
                _inventoryAction == null)
            {
                message =
                    "BIOS ERROR 0xINPUT\nEXPECTED: Player actions Movement/Look/Interact/PDA/Pause/Inventory\nDETECTED: one or more player actions missing\nACTION: Rebuild the player action schema before boot.";
                return false;
            }

            if (_navigateAction == null ||
                _submitAction == null ||
                _cancelAction == null ||
                _tabNextAction == null ||
                _tabPreviousAction == null)
            {
                message =
                    "BIOS ERROR 0xINPUT\nEXPECTED: UI actions Navigate/Submit/Cancel/TabNext/TabPrevious\nDETECTED: one or more UI actions missing\nACTION: Rebuild the UI action schema before boot.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Configures a runtime UI input module to use the project-owned action asset instead of Unity's default UI actions.
        /// </summary>
        /// <param name="inputModule">Target runtime UI input module.</param>
        /// <returns>True when the module was bound to the runtime HECTON-8 UI actions.</returns>
        public bool TryConfigureUiInputModule(InputSystemUIInputModule inputModule)
        {
            if (inputModule == null || !EnsureInputActionsInitialized())
                return false;

            if (_navigateAction == null ||
                _uiModuleSubmitAction == null ||
                _uiModuleCancelAction == null ||
                _uiPointAction == null ||
                _uiClickAction == null ||
                _uiMiddleClickAction == null ||
                _uiRightClickAction == null ||
                _uiScrollWheelAction == null)
            {
                return false;
            }

            EnsureUiModuleActionReferences();

            inputModule.actionsAsset = _runtimeInputActionAsset;
            inputModule.move = _uiModuleMoveReference;
            inputModule.submit = _uiModuleSubmitReference;
            inputModule.cancel = _uiModuleCancelReference;
            inputModule.point = _uiPointReference;
            inputModule.leftClick = _uiClickReference;
            inputModule.middleClick = _uiMiddleClickReference;
            inputModule.rightClick = _uiRightClickReference;
            inputModule.scrollWheel = _uiScrollWheelReference;
            inputModule.trackedDeviceOrientation = null;
            inputModule.trackedDevicePosition = null;

            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private void Awake()
        {
            BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, out INativeInputManagerRuntime registered);
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            _serviceShuttingDown = false;
            _serviceShutdownComplete = false;
            RegisterService();
            InitializeInputActions();
        }

        private void OnEnable()
        {
            if (_serviceShuttingDown)
                return;

            RegisterService();
            SubscribeToDeviceChanges();
            EnsureInputActionsInitialized();

            if (!_initialActivationComplete)
                return;

            if (_restorePlayerInputOnEnable || _restoreUiInputOnEnable)
                RebuildRuntimeInputActionsForActivation();

            if (_restorePlayerInputOnEnable)
                SafeEnableActionMap(_playerActionMap);

            if (_restoreUiInputOnEnable && _uiActionMap != null)
                SafeEnableActionMap(_uiActionMap);
        }

        private void OnDisable()
        {
            if (!_serviceRegistered)
                return;

            _restorePlayerInputOnEnable = IsActionMapEnabledForStateCapture(_playerActionMap);
            _restoreUiInputOnEnable = IsActionMapEnabledForStateCapture(_uiActionMap);
            SafeDisableActionMapForTeardown(_playerActionMap);
            SafeDisableActionMapForTeardown(_uiActionMap);
            UnsubscribeFromDeviceChanges();
            if (!_serviceShuttingDown)
                UnregisterService();
        }

        private void Start()
        {
            _initialActivationComplete = true;
            EnablePlayerInput();
        }
        
        private void InitializeInputActions()
        {
            if (_inputMapsInitialized && TryValidateActionMap(_playerActionMap, scheduleRecoveryOnFailure: true))
                return;

            ResetInputActionCaches(disposeRuntimeAsset: true);
            _inputMapsInitialized = false;

            InputActionAsset templateAsset = _inputActionAsset;
            if (templateAsset == null)
                templateAsset = TryResolveGeneratedInputActionAsset(ref _generatedInputActions);

            if (templateAsset == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InputManager] No InputActionAsset template available.");
#endif
                return;
            }

            _runtimeInputActionAsset = CreateRuntimeInputActionAsset(templateAsset);
            if (_runtimeInputActionAsset == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InputManager] Failed to create runtime InputActionAsset clone.");
#endif
                return;
            }

            _runtimeInputActionAsset.name = templateAsset.name;
            EnsureRequiredRuntimeActions(_runtimeInputActionAsset);
            
            // Get action maps
            _playerActionMap = _runtimeInputActionAsset.FindActionMap("Player");
            _uiActionMap = _runtimeInputActionAsset.FindActionMap("UI");
            
            if (_playerActionMap == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InputManager] Player action map not found in InputActionAsset!");
#endif
                return;
            }

            if (_uiActionMap == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InputManager] UI action map not found in InputActionAsset!");
#endif
                return;
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

        private void RebuildRuntimeInputActionsForActivation()
        {
            ResetInputActionCaches(disposeRuntimeAsset: true);
            InitializeInputActions();
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
            _pauseAction = _playerActionMap.FindAction("Pause");
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
            _uiModuleSubmitAction = _uiActionMap.FindAction("UiModuleSubmit");
            _uiModuleCancelAction = _uiActionMap.FindAction("UiModuleCancel");
            _uiPointAction = _uiActionMap.FindAction("Point");
            _uiClickAction = _uiActionMap.FindAction("Click");
            _uiMiddleClickAction = _uiActionMap.FindAction("MiddleClick");
            _uiRightClickAction = _uiActionMap.FindAction("RightClick");
            _uiScrollWheelAction = _uiActionMap.FindAction("ScrollWheel");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugToggleBlackBoxDashboardAction = _uiActionMap.FindAction("DebugToggleBlackBoxDashboard");
            _debugToggleEngineHealthOverlayAction = _uiActionMap.FindAction("DebugToggleEngineHealthOverlay");
#endif
        }

        private void EnsureUiModuleActionReferences()
        {
            _uiModuleMoveReference = CreateUiModuleActionReference(_uiModuleMoveReference, _navigateAction);
            _uiModuleSubmitReference = CreateUiModuleActionReference(_uiModuleSubmitReference, _uiModuleSubmitAction);
            _uiModuleCancelReference = CreateUiModuleActionReference(_uiModuleCancelReference, _uiModuleCancelAction);
            _uiPointReference = CreateUiModuleActionReference(_uiPointReference, _uiPointAction);
            _uiClickReference = CreateUiModuleActionReference(_uiClickReference, _uiClickAction);
            _uiMiddleClickReference = CreateUiModuleActionReference(_uiMiddleClickReference, _uiMiddleClickAction);
            _uiRightClickReference = CreateUiModuleActionReference(_uiRightClickReference, _uiRightClickAction);
            _uiScrollWheelReference = CreateUiModuleActionReference(_uiScrollWheelReference, _uiScrollWheelAction);
        }

        private static InputActionReference CreateUiModuleActionReference(InputActionReference existingReference, InputAction action)
        {
            if (action == null)
                return null;

            if (existingReference != null && existingReference.action == action)
                return existingReference;

            return InputActionReference.Create(action); // COLD ALLOC: InputActionReference[1] - runtime UI module action reference - owner: InputManager
        }

        private void ReleaseUiModuleActionReferences()
        {
            DestroyUiModuleActionReference(ref _uiModuleMoveReference);
            DestroyUiModuleActionReference(ref _uiModuleSubmitReference);
            DestroyUiModuleActionReference(ref _uiModuleCancelReference);
            DestroyUiModuleActionReference(ref _uiPointReference);
            DestroyUiModuleActionReference(ref _uiClickReference);
            DestroyUiModuleActionReference(ref _uiMiddleClickReference);
            DestroyUiModuleActionReference(ref _uiRightClickReference);
            DestroyUiModuleActionReference(ref _uiScrollWheelReference);
        }

        private void DestroyUiModuleActionReference(ref InputActionReference actionReference)
        {
            if (actionReference == null)
                return;

            if (Application.isPlaying)
                Destroy(actionReference);
            else
                DestroyImmediate(actionReference);

            actionReference = null;
        }

        private InputActionAsset CreateRuntimeInputActionAsset(InputActionAsset templateAsset)
        {
            if (templateAsset == null)
                return null;

            try
            {
                return Instantiate(templateAsset); // COLD ALLOC: InputActionAsset[1] — detached runtime input asset clone — owner: InputManager
            }
            catch (Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InputManager] Runtime InputActionAsset clone failed.");
#endif
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // EVENT SUBSCRIPTION (ZERO GC)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private static InputActionAsset TryResolveGeneratedInputActionAsset(ref IInputActionCollection2 generatedInputActions)
        {
            if (generatedInputActions != null)
                return TryExtractGeneratedInputActionAsset(generatedInputActions);

            Type generatedType = Type.GetType(GeneratedInputActionsTypeName, throwOnError: false);
            if (generatedType == null)
                return null;

            object instance = null;
            try
            {
                instance = Activator.CreateInstance(generatedType);
                if (instance is IInputActionCollection2 actions)
                {
                    generatedInputActions = actions;
                    return TryExtractGeneratedInputActionAsset(actions);
                }
            }
            catch (Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[InputManager] Generated InputAction fallback unavailable.");
#endif
            }

            (instance as IDisposable)?.Dispose();
            return null;
        }

        private static InputActionAsset TryExtractGeneratedInputActionAsset(IInputActionCollection2 actions)
        {
            if (actions == null)
                return null;

            System.Reflection.PropertyInfo assetProperty = actions.GetType().GetProperty(
                "asset",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return assetProperty != null ? assetProperty.GetValue(actions) as InputActionAsset : null;
        }

        private void SubscribeToPlayerActions()
        {
        }
        
        private void SubscribeToUIActions()
        {
        }

        private void UnsubscribeFromPlayerActions()
        {
        }

        private void UnsubscribeFromUIActions()
        {
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // INPUT CALLBACKS (ZERO GC)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        // Movement Callbacks
        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            _moveInput = context.ReadValue<Vector2>();
            OnMove?.Invoke(_moveInput);
        }
        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            _moveInput = Vector2.zero;
            OnMove?.Invoke(Vector2.zero);
        }
        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            _lookInput = context.ReadValue<Vector2>();
            OnLook?.Invoke(_lookInput);
        }
        private void OnLookCanceled(InputAction.CallbackContext context)
        {
            _lookInput = Vector2.zero;
        }
        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            _isJumping = true;
            OnJump?.Invoke();
        }
        private void OnJumpCanceledPerformed(InputAction.CallbackContext context)
        {
            _isJumping = false;
            OnJumpCanceled?.Invoke();
        }
        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            _isSprinting = true;
            OnSprint?.Invoke();
        }
        private void OnSprintCanceledPerformed(InputAction.CallbackContext context)
        {
            _isSprinting = false;
            OnSprintCanceled?.Invoke();
        }
        private void OnVerticalMovementPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            _verticalMovementInput = context.ReadValue<float>();
            OnVerticalMove?.Invoke(_verticalMovementInput);
        }
        private void OnVerticalMovementCanceled(InputAction.CallbackContext context)
        {
            _verticalMovementInput = 0f;
            OnVerticalMove?.Invoke(0f);
        }
        
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
        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnPause?.Invoke();
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
            _isPrimaryActionHeld = true;
            OnPrimaryAction?.Invoke();
        }
        private void OnPrimaryActionCanceledPerformed(InputAction.CallbackContext context)
        {
            _isPrimaryActionHeld = false;
            OnPrimaryActionCanceled?.Invoke();
        }
        private void OnSecondaryActionPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            _isSecondaryActionHeld = true;
            OnSecondaryAction?.Invoke();
        }
        private void OnSecondaryActionCanceledPerformed(InputAction.CallbackContext context)
        {
            _isSecondaryActionHeld = false;
            OnSecondaryActionCanceled?.Invoke();
        }
        
        // UI Callbacks
        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnNavigate?.Invoke(context.ReadValue<Vector2>());
        }
        private void OnNavigateCanceled(InputAction.CallbackContext context) => OnNavigate?.Invoke(Vector2.zero);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnDebugToggleBlackBoxDashboardPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnDebugToggleBlackBoxDashboard?.Invoke();
        }

        private void OnDebugToggleEngineHealthOverlayPerformed(InputAction.CallbackContext context)
        {
            CaptureInputDisplayStyle(context);
            OnDebugToggleEngineHealthOverlay?.Invoke();
        }
#endif

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

        /// <summary>
        /// Writes the preferred binding display label into a caller-owned character buffer without creating a display string.
        /// </summary>
        /// <param name="actionName">Input action name.</param>
        /// <param name="actionMap">Input action map name.</param>
        /// <param name="bindingIndex">Binding index, or -1 to resolve the current display-style preferred binding.</param>
        /// <param name="buffer">Caller-owned destination buffer.</param>
        /// <param name="bufferOffset">Destination start offset.</param>
        /// <param name="charsWritten">Number of characters written into <paramref name="buffer"/>.</param>
        /// <returns>True when a display label was written.</returns>
        public bool TryWriteBindingDisplayString(
            string actionName,
            string actionMap,
            int bindingIndex,
            char[] buffer,
            int bufferOffset,
            out int charsWritten)
        {
            charsWritten = 0;
            if (buffer == null || bufferOffset < 0 || bufferOffset >= buffer.Length)
                return false;

            InputAction action = GetAction(actionName, actionMap);
            if (action == null)
                return false;

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                bindingIndex = GetPreferredBindingIndex(action, CurrentDisplayStyle);

            return TryWriteBindingDisplayStringSafe(action, bindingIndex, buffer, bufferOffset, out charsWritten);
        }

        public bool TryGetBindingDisplayString(InputAction action, int bindingIndex, out string display)
        {
            return TryGetBindingDisplayStringSafe(action, bindingIndex, out display);
        }

        public bool TryWriteBindingDisplayString(
            InputAction action,
            int bindingIndex,
            char[] buffer,
            int bufferOffset,
            out int charsWritten)
        {
            return TryWriteBindingDisplayStringSafe(action, bindingIndex, buffer, bufferOffset, out charsWritten);
        }

        public int GetPreferredBindingIndex(string actionName, string actionMap = "Player")
        {
            InputAction action = GetAction(actionName, actionMap);
            return action != null
                ? GetPreferredBindingIndex(action, CurrentDisplayStyle)
                : -1;
        }

        public bool TryGetBindingMarkupForToken(string token, out string markup)
        {
            markup = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            if (!TryResolveTokenBinding(token, out string actionName, out string actionMap))
                return false;

            InputAction action = GetAction(actionName, actionMap);
            if (action == null)
                return false;

            int bindingIndex = GetPreferredBindingIndex(action, CurrentDisplayStyle);
            if (TryGetBindingGlyphMarkup(action, bindingIndex, out markup))
                return true;

            string display = GetBindingDisplayString(actionName, actionMap, bindingIndex);
            if (string.IsNullOrWhiteSpace(display))
                return false;

            markup = FormatBindingChip(display, CurrentDisplayStyle);
            return !string.IsNullOrWhiteSpace(markup);
        }

        public bool TryGetBindingGlyphMarkup(string actionName, string actionMap, int bindingIndex, out string markup)
        {
            markup = string.Empty;
            InputAction action = GetAction(actionName, actionMap);
            if (action == null)
                return false;

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                bindingIndex = GetPreferredBindingIndex(action, CurrentDisplayStyle);

            return TryGetBindingGlyphMarkup(action, bindingIndex, out markup);
        }

        public bool TryGetPreferredBindingPath(string actionName, string actionMap, out string bindingPath)
        {
            bindingPath = string.Empty;
            InputAction action = GetAction(actionName, actionMap);
            if (action == null)
                return false;

            int bindingIndex = GetPreferredBindingIndex(action, CurrentDisplayStyle);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return false;

            InputBinding binding;
            try
            {
                binding = action.bindings[bindingIndex];
            }
            catch (Exception)
            {
                return false;
            }

            string path = binding.effectivePath;
            if (string.IsNullOrWhiteSpace(path))
                path = !string.IsNullOrWhiteSpace(binding.overridePath) ? binding.overridePath : binding.path;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            bindingPath = path;
            return true;
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

            return TryBuildBindingDisplayStringFromPath(path, out display);
        }

        /// <summary>
        /// Writes a binding display label from an input action into a caller-owned character buffer.
        /// </summary>
        /// <param name="action">Input action containing the binding.</param>
        /// <param name="bindingIndex">Binding index to format.</param>
        /// <param name="buffer">Caller-owned destination buffer.</param>
        /// <param name="bufferOffset">Destination start offset.</param>
        /// <param name="charsWritten">Number of characters written into <paramref name="buffer"/>.</param>
        /// <returns>True when a display label was written.</returns>
        public static bool TryWriteBindingDisplayStringSafe(
            InputAction action,
            int bindingIndex,
            char[] buffer,
            int bufferOffset,
            out int charsWritten)
        {
            charsWritten = 0;
            if (action == null || bindingIndex < 0 || buffer == null || bufferOffset < 0 || bufferOffset >= buffer.Length)
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

            return TryWriteBindingDisplayStringFromPath(path, buffer, bufferOffset, out charsWritten);
        }

        private static bool TryBuildBindingDisplayStringFromPath(string path, out string display)
        {
            display = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            ReadOnlySpan<char> pathSpan = path.AsSpan();
            ReadOnlySpan<char> controlName = ExtractBindingControlName(pathSpan);
            if (controlName.IsEmpty)
            {
                display = path;
                return true;
            }

            bool isKeyboard = PathContainsDeviceToken(pathSpan, "Keyboard");
            bool isMouse = PathContainsDeviceToken(pathSpan, "Mouse");
            bool isGamepad = PathContainsDeviceToken(pathSpan, "Gamepad");
            if (TryResolveBindingAlias(controlName, isKeyboard, isMouse, isGamepad, out display))
                return true;

            display = path;
            return true;
        }

        private static bool TryWriteBindingDisplayStringFromPath(
            string path,
            char[] buffer,
            int bufferOffset,
            out int charsWritten)
        {
            charsWritten = 0;
            if (string.IsNullOrWhiteSpace(path) || buffer == null || bufferOffset < 0 || bufferOffset >= buffer.Length)
                return false;

            ReadOnlySpan<char> pathSpan = path.AsSpan();
            ReadOnlySpan<char> controlName = ExtractBindingControlName(pathSpan);
            if (controlName.IsEmpty)
                return TryWriteUpperTrimmed(pathSpan, buffer, bufferOffset, out charsWritten);

            bool isKeyboard = PathContainsDeviceToken(pathSpan, "Keyboard");
            bool isMouse = PathContainsDeviceToken(pathSpan, "Mouse");
            bool isGamepad = PathContainsDeviceToken(pathSpan, "Gamepad");
            if (TryWriteBindingAlias(controlName, isKeyboard, isMouse, isGamepad, buffer, bufferOffset, out charsWritten))
                return true;

            return TryWriteUpperTrimmed(controlName, buffer, bufferOffset, out charsWritten);
        }

        private static ReadOnlySpan<char> ExtractBindingControlName(ReadOnlySpan<char> bindingPath)
        {
            int slashIndex = bindingPath.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex >= bindingPath.Length - 1)
                return ReadOnlySpan<char>.Empty;

            return bindingPath.Slice(slashIndex + 1);
        }

        private static bool PathContainsDeviceToken(ReadOnlySpan<char> bindingPath, string token)
        {
            return bindingPath.IndexOf(token.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryResolveBindingAlias(
            ReadOnlySpan<char> controlName,
            bool isKeyboard,
            bool isMouse,
            bool isGamepad,
            out string display)
        {
            if (TryResolveSingleCharacterBindingLabel(controlName, out display))
                return true;

            if (isKeyboard)
            {
                if (ControlNameEquals(controlName, "space"))
                    return TryResolveLiteral("SPACE", out display);
                if (ControlNameEquals(controlName, "escape"))
                    return TryResolveLiteral("ESC", out display);
                if (ControlNameEquals(controlName, "enter") || ControlNameEquals(controlName, "numenter"))
                    return TryResolveLiteral("ENTER", out display);
                if (ControlNameEquals(controlName, "tab"))
                    return TryResolveLiteral("TAB", out display);
                if (ControlNameEquals(controlName, "backspace"))
                    return TryResolveLiteral("BACK", out display);
                if (ControlNameEquals(controlName, "uparrow"))
                    return TryResolveLiteral("UP", out display);
                if (ControlNameEquals(controlName, "downarrow"))
                    return TryResolveLiteral("DOWN", out display);
                if (ControlNameEquals(controlName, "leftarrow"))
                    return TryResolveLiteral("LEFT", out display);
                if (ControlNameEquals(controlName, "rightarrow"))
                    return TryResolveLiteral("RIGHT", out display);
                if (ControlNameEquals(controlName, "leftshift") || ControlNameEquals(controlName, "rightshift"))
                    return TryResolveLiteral("SHIFT", out display);
                if (ControlNameEquals(controlName, "leftctrl") || ControlNameEquals(controlName, "rightctrl") ||
                    ControlNameEquals(controlName, "leftcontrol") || ControlNameEquals(controlName, "rightcontrol"))
                {
                    return TryResolveLiteral("CTRL", out display);
                }

                if (ControlNameEquals(controlName, "leftalt") || ControlNameEquals(controlName, "rightalt"))
                    return TryResolveLiteral("ALT", out display);
                if (TryResolveDigitAlias(controlName, out char digit))
                    return TryResolveSingleCharacterLabel(digit, out display);
            }

            if (isMouse)
            {
                if (ControlNameEquals(controlName, "leftbutton"))
                    return TryResolveLiteral("LMB", out display);
                if (ControlNameEquals(controlName, "rightbutton"))
                    return TryResolveLiteral("RMB", out display);
                if (ControlNameEquals(controlName, "middlebutton"))
                    return TryResolveLiteral("MMB", out display);
                if (ControlNameEquals(controlName, "scroll") || ControlNameEquals(controlName, "scrolly"))
                    return TryResolveLiteral("SCROLL", out display);
            }

            if (isGamepad)
            {
                if (ControlNameEquals(controlName, "buttonsouth"))
                    return TryResolveLiteral("A", out display);
                if (ControlNameEquals(controlName, "buttoneast"))
                    return TryResolveLiteral("B", out display);
                if (ControlNameEquals(controlName, "buttonwest"))
                    return TryResolveLiteral("X", out display);
                if (ControlNameEquals(controlName, "buttonnorth"))
                    return TryResolveLiteral("Y", out display);
                if (ControlNameEquals(controlName, "leftshoulder"))
                    return TryResolveLiteral("LB", out display);
                if (ControlNameEquals(controlName, "rightshoulder"))
                    return TryResolveLiteral("RB", out display);
                if (ControlNameEquals(controlName, "lefttrigger"))
                    return TryResolveLiteral("LT", out display);
                if (ControlNameEquals(controlName, "righttrigger"))
                    return TryResolveLiteral("RT", out display);
                if (ControlNameEquals(controlName, "start"))
                    return TryResolveLiteral("START", out display);
                if (ControlNameEquals(controlName, "select"))
                    return TryResolveLiteral("SELECT", out display);
                if (ControlNameEquals(controlName, "dpadup"))
                    return TryResolveLiteral("DPAD UP", out display);
                if (ControlNameEquals(controlName, "dpaddown"))
                    return TryResolveLiteral("DPAD DOWN", out display);
                if (ControlNameEquals(controlName, "dpadleft"))
                    return TryResolveLiteral("DPAD LEFT", out display);
                if (ControlNameEquals(controlName, "dpadright"))
                    return TryResolveLiteral("DPAD RIGHT", out display);
            }

            display = string.Empty;
            return false;
        }

        private static bool TryResolveLiteral(string value, out string display)
        {
            display = value;
            return !string.IsNullOrEmpty(display);
        }

        private static bool TryResolveSingleCharacterBindingLabel(ReadOnlySpan<char> controlName, out string display)
        {
            display = string.Empty;
            return TryResolveSingleNormalizedChar(controlName, out char single) &&
                   TryResolveSingleCharacterLabel(single, out display);
        }

        private static bool TryResolveSingleCharacterLabel(char value, out string display)
        {
            display = string.Empty;
            char upper = ToUpperAscii(value);
            if (upper >= '0' && upper <= '9')
            {
                display = SingleCharacterBindingLabels[upper - '0'];
                return true;
            }

            if (upper >= 'A' && upper <= 'Z')
            {
                display = SingleCharacterBindingLabels[10 + upper - 'A'];
                return true;
            }

            return false;
        }

        private static bool TryWriteBindingAlias(
            ReadOnlySpan<char> controlName,
            bool isKeyboard,
            bool isMouse,
            bool isGamepad,
            char[] buffer,
            int bufferOffset,
            out int charsWritten)
        {
            charsWritten = 0;
            if (TryResolveSingleNormalizedChar(controlName, out char single) && char.IsLetterOrDigit(single))
                return TryWriteChar(ToUpperAscii(single), buffer, bufferOffset, out charsWritten);

            if (isKeyboard)
            {
                if (ControlNameEquals(controlName, "space"))
                    return TryWriteLiteral("SPACE".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "escape"))
                    return TryWriteLiteral("ESC".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "enter") || ControlNameEquals(controlName, "numenter"))
                    return TryWriteLiteral("ENTER".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "tab"))
                    return TryWriteLiteral("TAB".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "backspace"))
                    return TryWriteLiteral("BACK".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "uparrow"))
                    return TryWriteLiteral("UP".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "downarrow"))
                    return TryWriteLiteral("DOWN".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "leftarrow"))
                    return TryWriteLiteral("LEFT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "rightarrow"))
                    return TryWriteLiteral("RIGHT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "leftshift") || ControlNameEquals(controlName, "rightshift"))
                    return TryWriteLiteral("SHIFT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "leftctrl") || ControlNameEquals(controlName, "rightctrl") ||
                    ControlNameEquals(controlName, "leftcontrol") || ControlNameEquals(controlName, "rightcontrol"))
                {
                    return TryWriteLiteral("CTRL".AsSpan(), buffer, bufferOffset, out charsWritten);
                }

                if (ControlNameEquals(controlName, "leftalt") || ControlNameEquals(controlName, "rightalt"))
                    return TryWriteLiteral("ALT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (TryResolveDigitAlias(controlName, out char digit))
                    return TryWriteChar(digit, buffer, bufferOffset, out charsWritten);
            }

            if (isMouse)
            {
                if (ControlNameEquals(controlName, "leftbutton"))
                    return TryWriteLiteral("LMB".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "rightbutton"))
                    return TryWriteLiteral("RMB".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "middlebutton"))
                    return TryWriteLiteral("MMB".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "scroll") || ControlNameEquals(controlName, "scrolly"))
                    return TryWriteLiteral("SCROLL".AsSpan(), buffer, bufferOffset, out charsWritten);
            }

            if (isGamepad)
            {
                if (ControlNameEquals(controlName, "buttonsouth"))
                    return TryWriteLiteral("A".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "buttoneast"))
                    return TryWriteLiteral("B".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "buttonwest"))
                    return TryWriteLiteral("X".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "buttonnorth"))
                    return TryWriteLiteral("Y".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "leftshoulder"))
                    return TryWriteLiteral("LB".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "rightshoulder"))
                    return TryWriteLiteral("RB".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "lefttrigger"))
                    return TryWriteLiteral("LT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "righttrigger"))
                    return TryWriteLiteral("RT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "start"))
                    return TryWriteLiteral("START".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "select"))
                    return TryWriteLiteral("SELECT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "dpadup"))
                    return TryWriteLiteral("DPAD UP".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "dpaddown"))
                    return TryWriteLiteral("DPAD DOWN".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "dpadleft"))
                    return TryWriteLiteral("DPAD LEFT".AsSpan(), buffer, bufferOffset, out charsWritten);
                if (ControlNameEquals(controlName, "dpadright"))
                    return TryWriteLiteral("DPAD RIGHT".AsSpan(), buffer, bufferOffset, out charsWritten);
            }

            return false;
        }

        private static bool TryResolveSingleNormalizedChar(ReadOnlySpan<char> controlName, out char result)
        {
            result = '\0';
            int count = 0;
            for (int i = 0; i < controlName.Length; i++)
            {
                char c = controlName[i];
                if (c == '-' || char.IsWhiteSpace(c))
                    continue;

                result = c;
                count++;
                if (count > 1)
                    return false;
            }

            return count == 1;
        }

        private static bool TryResolveDigitAlias(ReadOnlySpan<char> controlName, out char digit)
        {
            digit = '\0';
            for (char candidate = '0'; candidate <= '9'; candidate++)
            {
                if (ControlNameEqualsDigit(controlName, "digit", candidate) ||
                    ControlNameEqualsDigit(controlName, "numpad", candidate))
                {
                    digit = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool ControlNameEqualsDigit(ReadOnlySpan<char> controlName, string prefix, char digit)
        {
            int prefixLength = prefix.Length;
            int tokenIndex = 0;
            for (int i = 0; i < controlName.Length; i++)
            {
                char c = controlName[i];
                if (c == '-' || char.IsWhiteSpace(c))
                    continue;

                if (tokenIndex < prefixLength)
                {
                    if (ToLowerAscii(c) != prefix[tokenIndex])
                        return false;
                }
                else if (tokenIndex == prefixLength)
                {
                    if (c != digit)
                        return false;
                }
                else
                {
                    return false;
                }

                tokenIndex++;
            }

            return tokenIndex == prefixLength + 1;
        }

        private static bool ControlNameEquals(ReadOnlySpan<char> controlName, string token)
        {
            int tokenIndex = 0;
            for (int i = 0; i < controlName.Length; i++)
            {
                char c = controlName[i];
                if (c == '-' || char.IsWhiteSpace(c))
                    continue;

                if (tokenIndex >= token.Length || ToLowerAscii(c) != token[tokenIndex])
                    return false;

                tokenIndex++;
            }

            return tokenIndex == token.Length;
        }

        private static bool TryWriteChar(char value, char[] buffer, int bufferOffset, out int charsWritten)
        {
            charsWritten = 0;
            if (buffer == null || bufferOffset < 0 || bufferOffset >= buffer.Length)
                return false;

            buffer[bufferOffset] = value;
            charsWritten = 1;
            return true;
        }

        private static bool TryWriteLiteral(ReadOnlySpan<char> value, char[] buffer, int bufferOffset, out int charsWritten)
        {
            charsWritten = 0;
            if (value.IsEmpty || buffer == null || bufferOffset < 0 || bufferOffset > buffer.Length - value.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
                buffer[bufferOffset + i] = value[i];

            charsWritten = value.Length;
            return true;
        }

        private static bool TryWriteUpperTrimmed(
            ReadOnlySpan<char> value,
            char[] buffer,
            int bufferOffset,
            out int charsWritten)
        {
            charsWritten = 0;
            if (value.IsEmpty || buffer == null || bufferOffset < 0 || bufferOffset >= buffer.Length)
                return false;

            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            if (start > end)
                return false;

            int length = end - start + 1;
            if (bufferOffset > buffer.Length - length)
                return false;

            for (int i = 0; i < length; i++)
                buffer[bufferOffset + i] = ToUpperAscii(value[start + i]);

            charsWritten = length;
            return true;
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

            if (IsGamepadDisplayStyle(displayStyle))
                return path.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;

            if (IsXRDisplayStyle(displayStyle))
                return path.IndexOf("XRController", StringComparison.OrdinalIgnoreCase) >= 0;

            return path.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatBindingChip(string display, InputDisplayStyle displayStyle)
        {
            return IsXRDisplayStyle(displayStyle)
                ? XrBindingChipFallback
                : IsGamepadDisplayStyle(displayStyle)
                    ? GamepadBindingChipFallback
                    : KeyboardBindingChipFallback;
        }

        private static bool TryGetBindingGlyphMarkup(InputAction action, int bindingIndex, out string markup)
        {
            markup = string.Empty;
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

            return GlyphProvider.TryGetBindingMarkup(path, out markup);
        }

        private static bool TryResolveTokenBinding(string token, out string actionName, out string actionMap)
        {
            actionName = string.Empty;
            actionMap = "Player";

            if (TokenEquals(token, "interact"))
                return TryResolveTokenAction("Interact", "Player", out actionName, out actionMap);
            if (TokenEquals(token, "inventory"))
                return TryResolveTokenAction("Inventory", "Player", out actionName, out actionMap);
            if (TokenEquals(token, "pda"))
                return TryResolveTokenAction("PDA", "Player", out actionName, out actionMap);
            if (TokenEquals(token, "flashlight"))
                return TryResolveTokenAction("Flashlight", "Player", out actionName, out actionMap);
            if (TokenEquals(token, "primary"))
                return TryResolveTokenAction("PrimaryAction", "Player", out actionName, out actionMap);
            if (TokenEquals(token, "secondary"))
                return TryResolveTokenAction("SecondaryAction", "Player", out actionName, out actionMap);
            if (TokenEquals(token, "navigate"))
                return TryResolveTokenAction("Navigate", "UI", out actionName, out actionMap);
            if (TokenEquals(token, "submit"))
                return TryResolveTokenAction("Submit", "UI", out actionName, out actionMap);
            if (TokenEquals(token, "cancel"))
                return TryResolveTokenAction("Cancel", "UI", out actionName, out actionMap);

            return false;
        }

        private static bool TryResolveTokenAction(
            string resolvedActionName,
            string resolvedActionMap,
            out string actionName,
            out string actionMap)
        {
            actionName = resolvedActionName;
            actionMap = resolvedActionMap;
            return true;
        }

        private static bool TokenEquals(string token, string expected)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            ReadOnlySpan<char> tokenSpan = token.AsSpan();
            int start = 0;
            int end = tokenSpan.Length - 1;
            while (start <= end && char.IsWhiteSpace(tokenSpan[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(tokenSpan[end]))
                end--;

            int length = end - start + 1;
            if (length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (ToLowerAscii(tokenSpan[start + i]) != expected[i])
                    return false;
            }

            return true;
        }

        private static char ToUpperAscii(char value)
        {
            return value >= 'a' && value <= 'z'
                ? (char)(value - 32)
                : value;
        }

        private static char ToLowerAscii(char value)
        {
            return value >= 'A' && value <= 'Z'
                ? (char)(value + 32)
                : value;
        }

        private void CaptureInputDisplayStyle(InputAction.CallbackContext context)
        {
            InputDevice device = context.control?.device;
            if (device == null)
                return;

            _lastDisplayDeviceId = device.deviceId;
            SetCurrentDisplayStyle(ResolveCachedDisplayStyle(device.deviceId));
        }

        private void SubscribeToDeviceChanges()
        {
            if (_deviceChangeSubscribed)
                return;

            InputSystem.onDeviceChange += HandleInputDeviceChange;
            _deviceChangeSubscribed = true;
            RefreshTrackedDevices();
        }

        private void UnsubscribeFromDeviceChanges()
        {
            if (!_deviceChangeSubscribed)
                return;

            InputSystem.onDeviceChange -= HandleInputDeviceChange;
            _deviceChangeSubscribed = false;
        }

        private void HandleInputDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.ConfigurationChanged:
                case InputDeviceChange.UsageChanged:
                    TrackDevice(device);
                    if (_inputRecoveryState == InputRecoveryState.AwaitingDeviceReconnect)
                    {
                        _inputRecoveryState = InputRecoveryState.RebuildPending;
                        ProcessInputRecoveryStateMachine();
                    }
                    else
                    {
                        RefreshCurrentDisplayStyleFromTrackedDevices();
                    }
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    UntrackDevice(device);
                    if (_inputMapsInitialized && !_serviceShuttingDown)
                        _inputRecoveryState = InputRecoveryState.AwaitingDeviceReconnect;

                    RefreshCurrentDisplayStyleFromTrackedDevices();
                    break;
            }
        }

        private void RefreshTrackedDevices()
        {
            _displayStyleByDeviceId.Clear();
            _connectedGamepadCount = 0;
            _connectedXRControllerCount = 0;

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
                TrackDevice(devices[i]);

            RefreshCurrentDisplayStyleFromTrackedDevices();
        }

        private void TrackDevice(InputDevice device)
        {
            if (device == null)
                return;

            int deviceId = device.deviceId;
            if (deviceId == 0)
                return;

            InputDisplayStyle style = ResolveDisplayStyle(device);
            if (_displayStyleByDeviceId.TryGetValue(deviceId, out InputDisplayStyle existingStyle))
            {
                if (existingStyle == style)
                    return;

                if (IsGamepadDisplayStyle(existingStyle))
                    _connectedGamepadCount = Mathf.Max(0, _connectedGamepadCount - 1);
                if (IsXRDisplayStyle(existingStyle))
                    _connectedXRControllerCount = Mathf.Max(0, _connectedXRControllerCount - 1);
            }

            _displayStyleByDeviceId[deviceId] = style;
            if (IsGamepadDisplayStyle(style))
                _connectedGamepadCount++;
            if (IsXRDisplayStyle(style))
                _connectedXRControllerCount++;
        }

        private void UntrackDevice(InputDevice device)
        {
            if (device == null)
                return;

            int deviceId = device.deviceId;
            if (!_displayStyleByDeviceId.TryGetValue(deviceId, out InputDisplayStyle style))
                return;

            if (IsGamepadDisplayStyle(style))
                _connectedGamepadCount = Mathf.Max(0, _connectedGamepadCount - 1);
            if (IsXRDisplayStyle(style))
                _connectedXRControllerCount = Mathf.Max(0, _connectedXRControllerCount - 1);

            _displayStyleByDeviceId.Remove(deviceId);
            if (_lastDisplayDeviceId == deviceId)
                _lastDisplayDeviceId = 0;
        }

        private void RefreshCurrentDisplayStyleFromTrackedDevices()
        {
            if (_lastDisplayDeviceId != 0 && _displayStyleByDeviceId.TryGetValue(_lastDisplayDeviceId, out InputDisplayStyle lastStyle))
            {
                SetCurrentDisplayStyle(lastStyle);
                return;
            }

            if (IsGamepadDisplayStyle(CurrentDisplayStyle) && _connectedGamepadCount == 0)
                SetCurrentDisplayStyle(InputDisplayStyle.KeyboardMouse);
            if (IsXRDisplayStyle(CurrentDisplayStyle) && _connectedXRControllerCount == 0)
                SetCurrentDisplayStyle(InputDisplayStyle.KeyboardMouse);
        }

        private InputDisplayStyle ResolveCachedDisplayStyle(int deviceId)
        {
            return _displayStyleByDeviceId.TryGetValue(deviceId, out InputDisplayStyle style)
                ? style
                : InputDisplayStyle.KeyboardMouse;
        }

        private void SetCurrentDisplayStyle(InputDisplayStyle nextStyle)
        {
            if (CurrentDisplayStyle == nextStyle)
                return;

            CurrentDisplayStyle = nextStyle;
            OnInputDisplayStyleChanged?.Invoke(nextStyle);
            OnInputDisplayStyleCodeChanged?.Invoke((byte)nextStyle);
        }

        private static InputDisplayStyle ResolveDisplayStyle(InputDevice device)
        {
            if (device is XRController)
                return InputDisplayStyle.XRTouch;

            if (!(device is Gamepad))
                return InputDisplayStyle.KeyboardMouse;

            return LooksLikeSteamDeck(device) ? InputDisplayStyle.SteamDeck : InputDisplayStyle.Gamepad;
        }

        private static bool IsGamepadDisplayStyle(InputDisplayStyle style)
        {
            return style == InputDisplayStyle.Gamepad || style == InputDisplayStyle.SteamDeck;
        }

        private static bool IsXRDisplayStyle(InputDisplayStyle style)
        {
            return style == InputDisplayStyle.XRTouch;
        }

        private static bool LooksLikeSteamDeck(InputDevice device)
        {
            if (device == null)
                return false;

            var description = device.description;
            return ContainsSteamDeckToken(description.product) ||
                   ContainsSteamDeckToken(description.manufacturer) ||
                   ContainsSteamDeckToken(description.interfaceName) ||
                   ContainsSteamDeckToken(device.displayName) ||
                   ContainsSteamDeckToken(device.name);
        }

        private static bool ContainsSteamDeckToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Steam Deck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("SteamDeck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (value.IndexOf("Valve", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    value.IndexOf("Deck", StringComparison.OrdinalIgnoreCase) >= 0);
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
            if (_serviceShuttingDown)
                return;

            RequestActionMapRecovery();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        private void OnApplicationQuit()
        {
            OnServiceShutdown();
            _inputRecoveryState = InputRecoveryState.Stable;
        }

        public void OnServiceShutdown()
        {
            if (_serviceShutdownComplete)
                return;

            _serviceShuttingDown = true;
            UnsubscribeFromDeviceChanges();
            ResetInputActionCaches(disposeRuntimeAsset: true);
            (_generatedInputActions as IDisposable)?.Dispose();
            _generatedInputActions = null;
            UnregisterService();

            _serviceShutdownComplete = true;
        }

        private void RegisterService()
        {
            if (_serviceShuttingDown || !Application.isPlaying)
                return;

            BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, out INativeInputManagerRuntime registered);
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            if (!ReferenceEquals(registered, this))
                BootstrapRegistryBridge.Register(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, this);

            _serviceRegistered =
                BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, out registered) &&
                ReferenceEquals(registered, this);

            if (_serviceRegistered)
                ActiveRuntimeInstance = this;
        }

        private void UnregisterService()
        {
            if (!_serviceRegistered)
                return;

            BootstrapRegistryBridge.Unregister(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, this);
            _serviceRegistered = false;

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void SafeEnableActionMap(InputActionMap actionMap)
        {
            TrySetActionMapEnabled(actionMap, enable: true, scheduleRecoveryOnFailure: true);
        }

        private void SafeDisableActionMap(InputActionMap actionMap)
        {
            TrySetActionMapEnabled(actionMap, enable: false, scheduleRecoveryOnFailure: true);
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

        private bool TrySetActionMapEnabled(InputActionMap requestedActionMap, bool enable, bool scheduleRecoveryOnFailure)
        {
            if (!TryResolveRuntimeActionMap(requestedActionMap, out InputActionMap resolvedActionMap))
            {
                if (scheduleRecoveryOnFailure)
                    RequestActionMapRecovery();

                return false;
            }

            if (!TryValidateActionMap(resolvedActionMap, scheduleRecoveryOnFailure))
                return false;

            try
            {
                if (enable)
                {
                    if (!resolvedActionMap.enabled)
                        resolvedActionMap.Enable();
                }
                else
                {
                    if (resolvedActionMap.enabled)
                        resolvedActionMap.Disable();

                    ResetCachedActionState(resolvedActionMap);
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                if (scheduleRecoveryOnFailure)
                    RequestActionMapRecovery();
            }
            catch (ArgumentOutOfRangeException)
            {
                if (scheduleRecoveryOnFailure)
                    RequestActionMapRecovery();
            }
            catch (Exception)
            {
                if (scheduleRecoveryOnFailure)
                    RequestActionMapRecovery();
            }

            return false;
        }

        private bool TryResolveRuntimeActionMap(InputActionMap actionMap, out InputActionMap resolvedActionMap)
        {
            resolvedActionMap = actionMap;
            if (_runtimeInputActionAsset == null)
                return false;

            if (resolvedActionMap == null)
                return false;

            if (IsActionMapOwnedByRuntimeAsset(resolvedActionMap))
                return true;

            string actionMapName;
            try
            {
                actionMapName = resolvedActionMap.name;
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(actionMapName))
                return false;

            try
            {
                resolvedActionMap = _runtimeInputActionAsset.FindActionMap(actionMapName);
            }
            catch (Exception)
            {
                resolvedActionMap = null;
            }

            if (resolvedActionMap == null)
                return false;

            BindResolvedActionMapReference(actionMapName, resolvedActionMap);
            return true;
        }

        private void RequestActionMapRecovery()
        {
            if (_serviceShuttingDown ||
                _inputRecoveryState == InputRecoveryState.Rebuilding ||
                _inputRecoveryState == InputRecoveryState.RebuildPending ||
                _inputRecoveryState == InputRecoveryState.AwaitingDeviceReconnect)
                return;

            _inputRecoveryState = InputRecoveryState.RebuildPending;
            ProcessInputRecoveryStateMachine();
        }

        private void ProcessInputRecoveryStateMachine()
        {
            if (_serviceShuttingDown ||
                _processingInputRecovery ||
                _inputRecoveryState != InputRecoveryState.RebuildPending)
                return;

            _processingInputRecovery = true;
            try
            {
                _inputRecoveryState = InputRecoveryState.Rebuilding;
                ResetInputActionCaches(disposeRuntimeAsset: true);

                if (_serviceShuttingDown)
                {
                    _inputRecoveryState = InputRecoveryState.Stable;
                    return;
                }

                InitializeInputActions();
                if (!_inputMapsInitialized)
                {
                    _inputRecoveryState = InputRecoveryState.AwaitingDeviceReconnect;
                    return;
                }

                bool playerRestored = !_restorePlayerInputOnEnable || TrySetActionMapEnabled(_playerActionMap, enable: true, scheduleRecoveryOnFailure: false);
                bool uiRestored = !_restoreUiInputOnEnable || _uiActionMap == null || TrySetActionMapEnabled(_uiActionMap, enable: true, scheduleRecoveryOnFailure: false);
                if (!playerRestored || !uiRestored)
                {
                    _inputRecoveryState = InputRecoveryState.AwaitingDeviceReconnect;
                    return;
                }

                _inputRecoveryState = InputRecoveryState.Stable;
                RefreshCurrentDisplayStyleFromTrackedDevices();
            }
            finally
            {
                _processingInputRecovery = false;
            }
        }

        private void BindResolvedActionMapReference(string actionMapName, InputActionMap actionMap)
        {
            if (actionMap == null || string.IsNullOrWhiteSpace(actionMapName))
                return;

            if (string.Equals(actionMapName, "Player", StringComparison.Ordinal))
            {
                _playerActionMap = actionMap;
                return;
            }

            if (string.Equals(actionMapName, "UI", StringComparison.Ordinal))
                _uiActionMap = actionMap;
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

        private bool TryValidateActionMap(InputActionMap actionMap, bool scheduleRecoveryOnFailure)
        {
            if (!_inputMapsInitialized || actionMap == null || _serviceShuttingDown || _runtimeInputActionAsset == null)
                return false;

            try
            {
                if (!ReferenceEquals(actionMap.asset, _runtimeInputActionAsset))
                {
                    if (scheduleRecoveryOnFailure)
                        RequestActionMapRecovery();

                    return false;
                }
            }
            catch (Exception)
            {
                if (scheduleRecoveryOnFailure)
                    RequestActionMapRecovery();

                return false;
            }

            return true;
        }

        private static void EnsureRequiredRuntimeActions(InputActionAsset runtimeAsset)
        {
            if (runtimeAsset == null)
                return;

            InputActionMap playerActionMap = runtimeAsset.FindActionMap("Player");
            if (playerActionMap != null)
                EnsurePauseAction(playerActionMap);

            EnsureUiActionMap(runtimeAsset);
        }

        private static void EnsurePauseAction(InputActionMap playerActionMap)
        {
            if (playerActionMap == null || playerActionMap.FindAction("Pause") != null)
                return;

            InputAction pauseAction = playerActionMap.AddAction("Pause", type: InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
        }

        private static InputActionMap EnsureUiActionMap(InputActionAsset runtimeAsset)
        {
            if (runtimeAsset == null)
                return null;

            InputActionMap uiActionMap = runtimeAsset.FindActionMap("UI");
            if (uiActionMap == null)
            {
                uiActionMap = new InputActionMap("UI");
                runtimeAsset.AddActionMap(uiActionMap);
            }

            InputAction navigateAction = EnsureUiAction(uiActionMap, "Navigate", InputActionType.Value, "Vector2");
            if (!HasBindingPath(navigateAction, "2DVector"))
            {
                navigateAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
            }

            if (!HasBindingPath(navigateAction, "<Keyboard>/upArrow"))
            {
                navigateAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
            }

            AddBindingIfMissing(navigateAction, "<Gamepad>/dpad");
            AddBindingIfMissing(navigateAction, "<Gamepad>/leftStick");

            InputAction submitAction = EnsureUiAction(uiActionMap, "Submit", InputActionType.Button, "Button");
            AddBindingIfMissing(submitAction, "<Keyboard>/enter");
            AddBindingIfMissing(submitAction, "<Mouse>/leftButton");
            AddBindingIfMissing(submitAction, "<Gamepad>/buttonSouth");

            InputAction cancelAction = EnsureUiAction(uiActionMap, "Cancel", InputActionType.Button, "Button");
            AddBindingIfMissing(cancelAction, "<Keyboard>/escape");
            AddBindingIfMissing(cancelAction, "<Keyboard>/tab");
            AddBindingIfMissing(cancelAction, "<Mouse>/rightButton");
            AddBindingIfMissing(cancelAction, "<Gamepad>/buttonEast");
            AddBindingIfMissing(cancelAction, "<Gamepad>/start");

            InputAction tabNextAction = EnsureUiAction(uiActionMap, "TabNext", InputActionType.Button, "Button");
            AddBindingIfMissing(tabNextAction, "<Keyboard>/e");
            AddBindingIfMissing(tabNextAction, "<Gamepad>/rightShoulder");

            InputAction tabPreviousAction = EnsureUiAction(uiActionMap, "TabPrevious", InputActionType.Button, "Button");
            AddBindingIfMissing(tabPreviousAction, "<Keyboard>/q");
            AddBindingIfMissing(tabPreviousAction, "<Gamepad>/leftShoulder");

            InputAction uiModuleSubmitAction = EnsureUiAction(uiActionMap, "UiModuleSubmit", InputActionType.Button, "Button");
            AddBindingIfMissing(uiModuleSubmitAction, "<Keyboard>/enter");
            AddBindingIfMissing(uiModuleSubmitAction, "<Gamepad>/buttonSouth");

            InputAction uiModuleCancelAction = EnsureUiAction(uiActionMap, "UiModuleCancel", InputActionType.Button, "Button");
            AddBindingIfMissing(uiModuleCancelAction, "<Keyboard>/escape");
            AddBindingIfMissing(uiModuleCancelAction, "<Gamepad>/buttonEast");
            AddBindingIfMissing(uiModuleCancelAction, "<Gamepad>/start");

            InputAction pointAction = EnsureUiAction(uiActionMap, "Point", InputActionType.PassThrough, "Vector2");
            AddBindingIfMissing(pointAction, "<Mouse>/position");

            InputAction clickAction = EnsureUiAction(uiActionMap, "Click", InputActionType.PassThrough, "Button");
            AddBindingIfMissing(clickAction, "<Mouse>/leftButton");

            InputAction middleClickAction = EnsureUiAction(uiActionMap, "MiddleClick", InputActionType.PassThrough, "Button");
            AddBindingIfMissing(middleClickAction, "<Mouse>/middleButton");

            InputAction rightClickAction = EnsureUiAction(uiActionMap, "RightClick", InputActionType.PassThrough, "Button");
            AddBindingIfMissing(rightClickAction, "<Mouse>/rightButton");

            InputAction scrollWheelAction = EnsureUiAction(uiActionMap, "ScrollWheel", InputActionType.PassThrough, "Vector2");
            AddBindingIfMissing(scrollWheelAction, "<Mouse>/scroll");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            InputAction debugBlackBoxDashboardAction =
                EnsureUiAction(uiActionMap, "DebugToggleBlackBoxDashboard", InputActionType.Button, "Button");
            AddBindingIfMissing(debugBlackBoxDashboardAction, "<Keyboard>/f3");
            AddButtonWithOneModifierBindingIfMissing(
                debugBlackBoxDashboardAction,
                "<Gamepad>/select",
                "<Gamepad>/rightShoulder");
            AddButtonWithOneModifierBindingIfMissing(
                debugBlackBoxDashboardAction,
                "<XRController>{LeftHand}/menuButton",
                "<XRController>{RightHand}/primaryButton");

            InputAction debugEngineHealthOverlayAction =
                EnsureUiAction(uiActionMap, "DebugToggleEngineHealthOverlay", InputActionType.Button, "Button");
            AddButtonWithOneModifierBindingIfMissing(
                debugEngineHealthOverlayAction,
                "<Keyboard>/leftCtrl",
                "<Keyboard>/f10");
            AddButtonWithOneModifierBindingIfMissing(
                debugEngineHealthOverlayAction,
                "<Keyboard>/rightCtrl",
                "<Keyboard>/f10");
            AddButtonWithOneModifierBindingIfMissing(
                debugEngineHealthOverlayAction,
                "<Gamepad>/select",
                "<Gamepad>/leftShoulder");
            AddButtonWithOneModifierBindingIfMissing(
                debugEngineHealthOverlayAction,
                "<XRController>{LeftHand}/menuButton",
                "<XRController>{RightHand}/secondaryButton");
#endif

            return uiActionMap;
        }

        private static InputAction EnsureUiAction(
            InputActionMap actionMap,
            string actionName,
            InputActionType actionType,
            string expectedControlType)
        {
            InputAction action = actionMap.FindAction(actionName);
            if (action != null)
                return action;

            return actionMap.AddAction(actionName, type: actionType, expectedControlLayout: expectedControlType);
        }

        private static bool HasBindingPath(InputAction action, string bindingPath)
        {
            if (action == null || string.IsNullOrEmpty(bindingPath))
                return false;

            var bindings = action.bindings;
            int bindingCount = bindings.Count;
            for (int i = 0; i < bindingCount; i++)
            {
                if (string.Equals(bindings[i].path, bindingPath, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AddBindingIfMissing(InputAction action, string bindingPath)
        {
            if (action == null || HasBindingPath(action, bindingPath))
                return;

            action.AddBinding(bindingPath);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void AddButtonWithOneModifierBindingIfMissing(
            InputAction action,
            string modifierPath,
            string buttonPath)
        {
            if (action == null ||
                string.IsNullOrEmpty(modifierPath) ||
                string.IsNullOrEmpty(buttonPath) ||
                HasButtonWithOneModifierBinding(action, modifierPath, buttonPath))
            {
                return;
            }

            action.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", modifierPath)
                .With("Button", buttonPath);
        }

        private static bool HasButtonWithOneModifierBinding(
            InputAction action,
            string modifierPath,
            string buttonPath)
        {
            if (action == null)
                return false;

            var bindings = action.bindings;
            int bindingCount = bindings.Count;
            for (int i = 0; i < bindingCount; i++)
            {
                if (!bindings[i].isComposite ||
                    !string.Equals(bindings[i].path, "ButtonWithOneModifier", StringComparison.Ordinal))
                {
                    continue;
                }

                bool hasModifier = false;
                bool hasButton = false;
                int partIndex = i + 1;
                while (partIndex < bindingCount && bindings[partIndex].isPartOfComposite)
                {
                    if (string.Equals(bindings[partIndex].name, "Modifier", StringComparison.Ordinal) &&
                        string.Equals(bindings[partIndex].path, modifierPath, StringComparison.Ordinal))
                    {
                        hasModifier = true;
                    }
                    else if (string.Equals(bindings[partIndex].name, "Button", StringComparison.Ordinal) &&
                             string.Equals(bindings[partIndex].path, buttonPath, StringComparison.Ordinal))
                    {
                        hasButton = true;
                    }

                    partIndex++;
                }

                if (hasModifier && hasButton)
                    return true;
            }

            return false;
        }
#endif

        private bool EnsureInputActionsInitialized()
        {
            if (_serviceShuttingDown)
                return false;

            if (_inputMapsInitialized && TryValidateActionMap(_playerActionMap, scheduleRecoveryOnFailure: true))
                return true;

            InitializeInputActions();
            return _inputMapsInitialized && TryValidateActionMap(_playerActionMap, scheduleRecoveryOnFailure: true);
        }

        private bool TryGetActionMapEnabled(InputActionMap actionMap)
        {
            if (actionMap == null || _serviceShuttingDown)
                return false;

            if (!TryValidateActionMap(actionMap, scheduleRecoveryOnFailure: true))
                return false;

            try
            {
                return actionMap.enabled;
            }
            catch (InvalidOperationException)
            {
                RequestActionMapRecovery();
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                RequestActionMapRecovery();
                return false;
            }
            catch (Exception)
            {
                RequestActionMapRecovery();
                return false;
            }
        }

        private void ResetInputActionCaches(bool disposeRuntimeAsset)
        {
            UnsubscribeFromPlayerActions();
            UnsubscribeFromUIActions();

            SafeDisableActionMapForTeardown(_playerActionMap);
            SafeDisableActionMapForTeardown(_uiActionMap);
            ResetCachedInputState();

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
            _pauseAction = null;
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
            _uiModuleSubmitAction = null;
            _uiModuleCancelAction = null;
            _uiPointAction = null;
            _uiClickAction = null;
            _uiMiddleClickAction = null;
            _uiRightClickAction = null;
            _uiScrollWheelAction = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugToggleBlackBoxDashboardAction = null;
            _debugToggleEngineHealthOverlayAction = null;
#endif

            ReleaseUiModuleActionReferences();

            if (!disposeRuntimeAsset)
                return;

            DisposeRuntimeInputActionAsset();
        }

        private void ResetCachedInputState()
        {
            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _verticalMovementInput = 0f;
            _isJumping = false;
            _isSprinting = false;
            _isPrimaryActionHeld = false;
            _isSecondaryActionHeld = false;
        }

        private void ResetCachedActionState(InputActionMap actionMap)
        {
            if (actionMap == null)
                return;

            string actionMapName;
            try
            {
                actionMapName = actionMap.name;
            }
            catch (Exception)
            {
                return;
            }

            if (string.Equals(actionMapName, "Player", StringComparison.Ordinal))
            {
                ResetCachedInputState();
                return;
            }

            if (string.Equals(actionMapName, "UI", StringComparison.Ordinal))
                _lastDisplayDeviceId = 0;
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
        /// <summary>
        /// Resolves first-party TMP sprite glyph markup for input bindings.
        /// Falls back to text chips when the assigned TMP sprite asset does not contain the requested glyph.
        /// Nested here deliberately so InputManager has no external compile dependency on a second type.
        /// </summary>
        public static class GlyphProvider
        {
            private enum GlyphId : byte
            {
                None = 0,
                KeyboardE = 1,
                KeyboardF = 2,
                KeyboardM = 3,
                KeyboardQ = 4,
                KeyboardR = 5,
                KeyboardTab = 6,
                KeyboardSpace = 7,
                KeyboardEnter = 8,
                KeyboardEscape = 9,
                MouseLeft = 10,
                MouseRight = 11,
                GamepadSouth = 12,
                GamepadEast = 13,
                GamepadWest = 14,
                GamepadNorth = 15,
                GamepadLeftShoulder = 16,
                GamepadRightShoulder = 17,
                GamepadLeftTrigger = 18,
                GamepadRightTrigger = 19,
                GamepadStart = 20,
                GamepadSelect = 21,
                GamepadDpadUp = 22,
                GamepadDpadDown = 23,
                GamepadDpadLeft = 24,
                GamepadDpadRight = 25
            }

            private const string KeyboardDeviceToken = "Keyboard";
            private const string MouseDeviceToken = "Mouse";
            private const string GamepadDeviceToken = "Gamepad";

            // COLD ALLOC: string[26] — cached TMP sprite markups indexed by GlyphId — owner: InputManager.GlyphProvider
            private static readonly string[] SpriteMarkups =
            {
                string.Empty,
                "<voffset=-0.08em><sprite name=\"kbd_e\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_f\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_m\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_q\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_r\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_tab\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_space\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_enter\"></voffset>",
                "<voffset=-0.08em><sprite name=\"kbd_escape\"></voffset>",
                "<voffset=-0.08em><sprite name=\"mouse_left\"></voffset>",
                "<voffset=-0.08em><sprite name=\"mouse_right\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_south\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_east\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_west\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_north\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_lb\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_rb\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_lt\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_rt\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_start\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_select\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_up\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_down\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_left\"></voffset>",
                "<voffset=-0.08em><sprite name=\"pad_right\"></voffset>"
            };

            // COLD ALLOC: string[26] — cached TMP sprite names indexed by GlyphId — owner: InputManager.GlyphProvider
            private static readonly string[] SpriteNames =
            {
                string.Empty,
                "kbd_e",
                "kbd_f",
                "kbd_m",
                "kbd_q",
                "kbd_r",
                "kbd_tab",
                "kbd_space",
                "kbd_enter",
                "kbd_escape",
                "mouse_left",
                "mouse_right",
                "pad_south",
                "pad_east",
                "pad_west",
                "pad_north",
                "pad_lb",
                "pad_rb",
                "pad_lt",
                "pad_rt",
                "pad_start",
                "pad_select",
                "pad_up",
                "pad_down",
                "pad_left",
                "pad_right"
            };

            /// <summary>
            /// Attempts to resolve TMP sprite markup for a binding path.
            /// </summary>
            public static bool TryGetBindingMarkup(string bindingPath, out string markup)
            {
                markup = string.Empty;
                if (string.IsNullOrWhiteSpace(bindingPath))
                    return false;

                GlyphId glyphId = ResolveGlyphId(bindingPath);
                if (glyphId == GlyphId.None)
                    return false;

                markup = SpriteMarkups[(int)glyphId];
                return !string.IsNullOrEmpty(markup);
            }

            public static bool TryGetBindingSpriteName(string bindingPath, out string spriteName)
            {
                spriteName = string.Empty;
                if (string.IsNullOrWhiteSpace(bindingPath))
                    return false;

                GlyphId glyphId = ResolveGlyphId(bindingPath);
                if (glyphId == GlyphId.None)
                    return false;

                spriteName = SpriteNames[(int)glyphId];
                return !string.IsNullOrEmpty(spriteName);
            }

            private static GlyphId ResolveGlyphId(string bindingPath)
            {
                if (string.IsNullOrWhiteSpace(bindingPath))
                    return GlyphId.None;

                ReadOnlySpan<char> path = bindingPath.AsSpan();
                ReadOnlySpan<char> controlName = ExtractControlName(path);
                if (controlName.IsEmpty)
                    return GlyphId.None;

                if (PathContainsToken(path, KeyboardDeviceToken))
                {
                    if (ControlMatches(controlName, "e")) return GlyphId.KeyboardE;
                    if (ControlMatches(controlName, "f")) return GlyphId.KeyboardF;
                    if (ControlMatches(controlName, "m")) return GlyphId.KeyboardM;
                    if (ControlMatches(controlName, "q")) return GlyphId.KeyboardQ;
                    if (ControlMatches(controlName, "r")) return GlyphId.KeyboardR;
                    if (ControlMatches(controlName, "tab")) return GlyphId.KeyboardTab;
                    if (ControlMatches(controlName, "space")) return GlyphId.KeyboardSpace;
                    if (ControlMatches(controlName, "enter")) return GlyphId.KeyboardEnter;
                    if (ControlMatches(controlName, "escape")) return GlyphId.KeyboardEscape;
                }

                if (PathContainsToken(path, MouseDeviceToken))
                {
                    if (ControlMatches(controlName, "leftbutton")) return GlyphId.MouseLeft;
                    if (ControlMatches(controlName, "rightbutton")) return GlyphId.MouseRight;
                }

                if (PathContainsToken(path, GamepadDeviceToken))
                {
                    if (ControlMatches(controlName, "buttonsouth")) return GlyphId.GamepadSouth;
                    if (ControlMatches(controlName, "buttoneast")) return GlyphId.GamepadEast;
                    if (ControlMatches(controlName, "buttonwest")) return GlyphId.GamepadWest;
                    if (ControlMatches(controlName, "buttonnorth")) return GlyphId.GamepadNorth;
                    if (ControlMatches(controlName, "leftshoulder")) return GlyphId.GamepadLeftShoulder;
                    if (ControlMatches(controlName, "rightshoulder")) return GlyphId.GamepadRightShoulder;
                    if (ControlMatches(controlName, "lefttrigger")) return GlyphId.GamepadLeftTrigger;
                    if (ControlMatches(controlName, "righttrigger")) return GlyphId.GamepadRightTrigger;
                    if (ControlMatches(controlName, "start")) return GlyphId.GamepadStart;
                    if (ControlMatches(controlName, "select")) return GlyphId.GamepadSelect;
                    if (ControlMatches(controlName, "dpadup")) return GlyphId.GamepadDpadUp;
                    if (ControlMatches(controlName, "dpaddown")) return GlyphId.GamepadDpadDown;
                    if (ControlMatches(controlName, "dpadleft")) return GlyphId.GamepadDpadLeft;
                    if (ControlMatches(controlName, "dpadright")) return GlyphId.GamepadDpadRight;
                }

                return GlyphId.None;
            }

            private static ReadOnlySpan<char> ExtractControlName(ReadOnlySpan<char> bindingPath)
            {
                int slashIndex = bindingPath.LastIndexOf('/');
                if (slashIndex < 0 || slashIndex >= bindingPath.Length - 1)
                    return ReadOnlySpan<char>.Empty;

                return bindingPath.Slice(slashIndex + 1);
            }

            private static bool PathContainsToken(ReadOnlySpan<char> bindingPath, string token)
            {
                return bindingPath.IndexOf(token.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static bool ControlMatches(ReadOnlySpan<char> controlName, string expectedNormalized)
            {
                ReadOnlySpan<char> expected = expectedNormalized.AsSpan();
                int controlIndex = 0;
                int expectedIndex = 0;

                while (controlIndex < controlName.Length)
                {
                    char current = controlName[controlIndex++];
                    if (current == '-')
                        continue;

                    if (expectedIndex >= expected.Length || ToLowerAscii(current) != expected[expectedIndex])
                        return false;

                    expectedIndex++;
                }

                return expectedIndex == expected.Length;
            }
        }
    }
}
