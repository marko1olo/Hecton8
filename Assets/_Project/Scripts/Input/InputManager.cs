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
    /// Enterprise-grade Input Manager with zero GC allocations.
    /// Singleton pattern with thread-safe initialization.
    /// Supports keyboard, mouse, and gamepad with full rebinding support.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // SINGLETON PATTERN
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private static InputManager _instance;
        private static readonly object _lock = new object();
        private static bool _isShuttingDown;
        private HectonInputActions _generatedInputActions;

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
                            _instance = FindFirstObjectByType<InputManager>();
                            
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

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        public bool IsPlayerInputEnabled => _playerActionMap?.enabled ?? false;
        public bool IsUIInputEnabled => _uiActionMap?.enabled ?? false;
        
        public Vector2 MoveInput => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 LookInput => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool IsJumping => _jumpAction?.IsPressed() ?? false;
        public bool IsSprinting => _sprintAction?.IsPressed() ?? false;
        public bool IsPrimaryActionHeld => _primaryActionAction?.IsPressed() ?? false;
        public bool IsSecondaryActionHeld => _secondaryActionAction?.IsPressed() ?? false;
        public float VerticalMovementInput => _verticalMovementAction?.ReadValue<float>() ?? 0f;
        public InputActionAsset InputActionsAsset => _inputActionAsset;

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
        
        private void InitializeInputActions()
        {
            // Prefer a real asset from inspector/resources, but always keep a generated
            // fallback so the project never hard-fails on a missing asset reference.
            if (_inputActionAsset == null)
            {
                _inputActionAsset = Resources.Load<InputActionAsset>("HectonRuntimeInputActions");
            }

            if (_inputActionAsset == null)
            {
                _generatedInputActions = new HectonInputActions();
                _inputActionAsset = _generatedInputActions.asset;
            }
            
            // Get action maps
            _playerActionMap = _inputActionAsset.FindActionMap("Player");
            _uiActionMap = _inputActionAsset.FindActionMap("UI");
            
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
            
            // Enable player input by default
            EnablePlayerInput();
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

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // EVENT SUBSCRIPTION (ZERO GC)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private void SubscribeToPlayerActions()
        {
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
        }
        
        private void SubscribeToUIActions()
        {
            _navigateAction.performed += OnNavigatePerformed;
            _submitAction.performed += OnSubmitPerformed;
            _cancelAction.performed += OnCancelPerformed;
            _tabNextAction.performed += OnTabNextPerformed;
            _tabPreviousAction.performed += OnTabPreviousPerformed;
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // INPUT CALLBACKS (ZERO GC)
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        // Movement Callbacks
        private void OnMovePerformed(InputAction.CallbackContext context) => OnMove?.Invoke(context.ReadValue<Vector2>());
        private void OnMoveCanceled(InputAction.CallbackContext context) => OnMove?.Invoke(Vector2.zero);
        private void OnLookPerformed(InputAction.CallbackContext context) => OnLook?.Invoke(context.ReadValue<Vector2>());
        private void OnJumpPerformed(InputAction.CallbackContext context) => OnJump?.Invoke();
        private void OnJumpCanceledPerformed(InputAction.CallbackContext context) => OnJumpCanceled?.Invoke();
        private void OnSprintPerformed(InputAction.CallbackContext context) => OnSprint?.Invoke();
        private void OnSprintCanceledPerformed(InputAction.CallbackContext context) => OnSprintCanceled?.Invoke();
        private void OnVerticalMovementPerformed(InputAction.CallbackContext context) => OnVerticalMove?.Invoke(context.ReadValue<float>());
        private void OnVerticalMovementCanceled(InputAction.CallbackContext context) => OnVerticalMove?.Invoke(0f);
        
        // Interaction Callbacks
        private void OnInteractPerformed(InputAction.CallbackContext context) => OnInteract?.Invoke();
        private void OnFlashlightPerformed(InputAction.CallbackContext context) => OnFlashlight?.Invoke();
        private void OnPDAPerformed(InputAction.CallbackContext context) => OnPDA?.Invoke();
        private void OnInventoryPerformed(InputAction.CallbackContext context) => OnInventory?.Invoke();
        
        // Tool Callbacks
        private void OnToolSlot1Performed(InputAction.CallbackContext context) => OnToolSlot1?.Invoke();
        private void OnToolSlot2Performed(InputAction.CallbackContext context) => OnToolSlot2?.Invoke();
        private void OnToolSlot3Performed(InputAction.CallbackContext context) => OnToolSlot3?.Invoke();
        private void OnToolSlot4Performed(InputAction.CallbackContext context) => OnToolSlot4?.Invoke();
        
        // Action Callbacks
        private void OnPrimaryActionPerformed(InputAction.CallbackContext context) => OnPrimaryAction?.Invoke();
        private void OnPrimaryActionCanceledPerformed(InputAction.CallbackContext context) => OnPrimaryActionCanceled?.Invoke();
        private void OnSecondaryActionPerformed(InputAction.CallbackContext context) => OnSecondaryAction?.Invoke();
        private void OnSecondaryActionCanceledPerformed(InputAction.CallbackContext context) => OnSecondaryActionCanceled?.Invoke();
        
        // UI Callbacks
        private void OnNavigatePerformed(InputAction.CallbackContext context) => OnNavigate?.Invoke(context.ReadValue<Vector2>());
        private void OnSubmitPerformed(InputAction.CallbackContext context) => OnSubmit?.Invoke();
        private void OnCancelPerformed(InputAction.CallbackContext context) => OnCancel?.Invoke();
        private void OnTabNextPerformed(InputAction.CallbackContext context) => OnTabNext?.Invoke();
        private void OnTabPreviousPerformed(InputAction.CallbackContext context) => OnTabPrevious?.Invoke();

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        public void EnablePlayerInput()
        {
            _playerActionMap?.Enable();
        }
        
        public void DisablePlayerInput()
        {
            _playerActionMap?.Disable();
        }
        
        public void EnableUIInput()
        {
            if (_uiActionMap == null)
                return;

            _uiActionMap.Enable();
        }
        
        public void DisableUIInput()
        {
            if (_uiActionMap == null)
                return;

            _uiActionMap.Disable();
        }
        
        public void SwitchToPlayerInput()
        {
            DisableUIInput();
            EnablePlayerInput();
        }
        
        public void SwitchToUIInput()
        {
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
            if (actionMap == "Player")
                return _playerActionMap?.FindAction(actionName);
            else if (actionMap == "UI")
                return _uiActionMap?.FindAction(actionName);
            
            return null;
        }
        
        public string GetBindingDisplayString(string actionName, string actionMap = "Player", int bindingIndex = 0)
        {
            InputAction action = GetAction(actionName, actionMap);
            if (action == null) return string.Empty;
            
            return action.GetBindingDisplayString(bindingIndex);
        }

        public string SaveBindingOverridesAsJson()
        {
            if (_inputActionAsset == null) return string.Empty;
            return _inputActionAsset.SaveBindingOverridesAsJson();
        }

        public void LoadBindingOverridesFromJson(string json)
        {
            if (_inputActionAsset == null || string.IsNullOrEmpty(json)) return;
            _inputActionAsset.LoadBindingOverridesFromJson(json);
        }

        public void ClearBindingOverrides()
        {
            if (_inputActionAsset == null) return;
            _inputActionAsset.RemoveAllBindingOverrides();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════════════════════════════════
        
        private void OnDestroy()
        {
            if (_instance == this)
                _isShuttingDown = true;

            if (_playerActionMap != null)
            {
                _playerActionMap.Disable();
            }
            
            if (_uiActionMap != null)
            {
                _uiActionMap.Disable();
            }

            _generatedInputActions?.Dispose();
            _generatedInputActions = null;

            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }
    }
}
