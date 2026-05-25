using Hecton8.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ScifiOffice
{
    public class DemoFirstPersonController : MonoBehaviour, ITickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        Rigidbody rb;
        CapsuleCollider col;
        bool isCrouching;
        bool _registeredTick;
        bool _registeredLateFrame;
        bool _registeredHotSwap;
        private const string ToggleControlModeBinding = "<Keyboard>/e";
        private const string CrouchPrimaryBinding = "<Keyboard>/leftCtrl";
        private const string CrouchSecondaryBinding = "<Keyboard>/leftShift";

        public Transform playerBody;

        public enum ControlType { android, keyboard, keyboardMouse }
        public ControlType controlType;

        [Header("Movement")]
        public float speed = 3f;
        public float accelerationRate = 12f, crouchFactor = 0.5f, decelerationFactor = 1f;
        public float mouseSensitivity = 50f;

        float xRot = 0f;
        float horizontalMovement;
        float verticalMovement;

        [Header("HUD")]
        public GameObject canvas;

        private INativeInputManagerRuntime _inputManager;
        private CanvasGroup _canvasGroup;
        private bool _mobileControlsVisible;
        private InputAction _toggleControlModeAction;
        private InputAction _keyboardCrouchAction;
        private bool _demoInputActionsReady;
        private Quaternion _pendingPitchRotation;
        private float _pendingYawDeltaDegrees;
        private bool _hasPendingLookPose;
        private bool _pendingMobileControlsVisible;
        private bool _hasPendingMobileControlsVisibility;

        private void Awake()
        {
            InitializeDemoInputActions();
        }

        private void Start()
        {
            if (playerBody == null)
            {
                enabled = false;
                return;
            }

            if (!playerBody.TryGetComponent(out rb) || !playerBody.TryGetComponent(out col))
            {
                enabled = false;
                return;
            }

            _inputManager = GlobalRegistry.NativeInputRuntime;
            if (canvas != null && !canvas.TryGetComponent(out _canvasGroup))
                _canvasGroup = canvas.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] — mobile demo controls visibility without per-frame SetActive — owner: DemoFirstPersonController
            _mobileControlsVisible = controlType != ControlType.android;
            SetMobileControlsVisible(controlType == ControlType.android);

            if (controlType == ControlType.keyboardMouse)
                Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnEnable()
        {
            EnableDemoInputActions();
            TryRegisterHotSwapListener();
            RegisterTicks();
        }

        private void OnDisable()
        {
            DisableDemoInputActions();
            TryUnregisterHotSwapListener();
            UnregisterTick();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            UnregisterTick();
            DisposeDemoInputActions();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredTick = false;
                    _registeredLateFrame = false;
                    if (currentService == null)
                        return;

                    RegisterTicks();
                    break;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    _inputManager = currentService as INativeInputManagerRuntime;
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            if (playerBody == null || rb == null || col == null)
                return;

            Walk(deltaTime);
            Look(deltaTime);

            // E to switch keyboard control type between keyboardMouse and keyboard.
            if (IsToggleControlModePressed())
            {
                if (controlType == ControlType.keyboardMouse)
                {
                    controlType = ControlType.keyboard;
                    xRot = 0f;
                }
                else
                {
                    controlType = ControlType.keyboardMouse;
                }
            }
            else if (controlType == ControlType.android)
            {
                // Show mobile controls.
                QueueMobileControlsVisible(true);
            }
            else
            {
                // Do not show mobile controls when using keyboard controls.
                Crouch();
                QueueMobileControlsVisible(false);
            }
        }

        public void LateFrameTick()
        {
            FlushMobileControlsVisibility();
            FlushLookPose();
        }

        private void SetMobileControlsVisible(bool visible)
        {
            if (_mobileControlsVisible == visible)
                return;

            _mobileControlsVisible = visible;
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        public void Look(float deltaTime)
        {
            float mouseX = 0f;
            float mouseY = 0f;

            switch (controlType)
            {
                case ControlType.android:
                    mouseX = horizontalMovement * deltaTime * mouseSensitivity;
                    break;

                case ControlType.keyboard:
                    // Get changes to look left and right only. Player cannot look up and down.
                    mouseX = (_inputManager != null ? _inputManager.MoveInput.x : 0f) * mouseSensitivity * deltaTime;
                    mouseY = 0f;
                    break;

                default:
                case ControlType.keyboardMouse:
                    // Use mouse to control where to look. Can look in all directions.
                    if (_inputManager != null)
                    {
                        Vector2 look = _inputManager.LookInput;
                        mouseX = look.x * mouseSensitivity * deltaTime;
                        mouseY = look.y * mouseSensitivity * deltaTime;
                    }
                    break;
            }

            // Rotate playerBody.
            xRot -= mouseY;
            xRot = Mathf.Clamp(xRot, -90f, 90f);

            QueueLookPose(Quaternion.Euler(xRot, 0f, 0f), mouseX);
        }

        private void QueueLookPose(Quaternion pitchRotation, float yawDeltaDegrees)
        {
            _pendingPitchRotation = pitchRotation;
            _pendingYawDeltaDegrees += yawDeltaDegrees;
            _hasPendingLookPose = true;
        }

        private void FlushLookPose()
        {
            if (!_hasPendingLookPose)
                return;

            _hasPendingLookPose = false;
            transform.localRotation = _pendingPitchRotation;
            if (playerBody != null && Mathf.Abs(_pendingYawDeltaDegrees) > 0.00001f)
                playerBody.Rotate(Vector3.up * _pendingYawDeltaDegrees);
            _pendingYawDeltaDegrees = 0f;
        }

        private void QueueMobileControlsVisible(bool visible)
        {
            if (_mobileControlsVisible == visible && !_hasPendingMobileControlsVisibility)
                return;

            _pendingMobileControlsVisible = visible;
            _hasPendingMobileControlsVisibility = true;
        }

        private void FlushMobileControlsVisibility()
        {
            if (!_hasPendingMobileControlsVisibility)
                return;

            _hasPendingMobileControlsVisibility = false;
            SetMobileControlsVisible(_pendingMobileControlsVisible);
        }

        void Walk(float deltaTime)
        {
            _ = deltaTime;
        }

        void Crouch()
        {
            bool keyboardCrouch = IsKeyboardCrouchPressed();
            bool sprintCrouch = _inputManager != null && _inputManager.IsSprinting;
            bool isCrouchPressed = keyboardCrouch || sprintCrouch;

            if (isCrouchPressed)
            {
                col.height = .5f;
                isCrouching = true;
            }
            else
            {
                // Otherwise, player stop crouching.
                col.height = 2f;
                isCrouching = false;
            }
        }

        private void InitializeDemoInputActions()
        {
            if (_demoInputActionsReady)
                return;

            // COLD ALLOC: InputAction[1] — demo keyboard control-mode toggle — owner: DemoFirstPersonController
            _toggleControlModeAction = new InputAction("DemoToggleControlMode", InputActionType.Button, ToggleControlModeBinding);

            // COLD ALLOC: InputAction[1] — demo keyboard crouch compatibility bindings — owner: DemoFirstPersonController
            _keyboardCrouchAction = new InputAction("DemoKeyboardCrouch", InputActionType.Button);
            _keyboardCrouchAction.AddBinding(CrouchPrimaryBinding);
            _keyboardCrouchAction.AddBinding(CrouchSecondaryBinding);

            _demoInputActionsReady = true;
        }

        private void EnableDemoInputActions()
        {
            if (!_demoInputActionsReady)
                return;

            if (_toggleControlModeAction != null && !_toggleControlModeAction.enabled)
                _toggleControlModeAction.Enable();

            if (_keyboardCrouchAction != null && !_keyboardCrouchAction.enabled)
                _keyboardCrouchAction.Enable();
        }

        private void DisableDemoInputActions()
        {
            if (!_demoInputActionsReady)
                return;

            if (_toggleControlModeAction != null && _toggleControlModeAction.enabled)
                _toggleControlModeAction.Disable();

            if (_keyboardCrouchAction != null && _keyboardCrouchAction.enabled)
                _keyboardCrouchAction.Disable();
        }

        private void DisposeDemoInputActions()
        {
            if (_toggleControlModeAction != null)
            {
                _toggleControlModeAction.Dispose();
                _toggleControlModeAction = null;
            }

            if (_keyboardCrouchAction != null)
            {
                _keyboardCrouchAction.Dispose();
                _keyboardCrouchAction = null;
            }

            _demoInputActionsReady = false;
        }

        private bool IsToggleControlModePressed()
        {
            return _toggleControlModeAction != null && _toggleControlModeAction.WasPressedThisFrame();
        }

        private bool IsKeyboardCrouchPressed()
        {
            return _keyboardCrouchAction != null && _keyboardCrouchAction.IsPressed();
        }

        private void RegisterTicks()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void UnregisterTick()
        {
            if (_registeredTick && GlobalRegistry.Dispatcher != null)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

            _registeredTick = false;
            if (_registeredLateFrame && GlobalRegistry.Dispatcher != null)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);

            _registeredLateFrame = false;
            _hasPendingLookPose = false;
            _pendingYawDeltaDegrees = 0f;
            _hasPendingMobileControlsVisibility = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        // Crouching for android build.
        public void MobileCrouch()
        {
            // If player is currently crouching, stop crouching and vice versa.
            if (isCrouching)
            {
                col.height = 2f;
                isCrouching = false;
            }
            else
            {
                col.height = .5f;
                isCrouching = true;
            }
        }

        // Setting movement for android build.
        public void MobileWalk(int direction)
        {
            if (direction * direction == 1)
            {
                // Moving left and right.
                horizontalMovement = direction;
            }
            else if (direction == 3)
            {
                // When none of the button is pressed, stop moving.
                horizontalMovement = 0f;
                verticalMovement = 0f;
            }
            else
            {
                // Moving forward and back.
                verticalMovement = direction - 1;
            }
        }
    }
}
