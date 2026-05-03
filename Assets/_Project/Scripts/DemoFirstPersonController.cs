using Hecton8.Input;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ScifiOffice
{
    public class DemoFirstPersonController : MonoBehaviour, ITickable
    {
        Rigidbody rb;
        CapsuleCollider col;
        bool isCrouching;
        bool _registeredTick;
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

        private InputManager _inputManager;
        private CanvasGroup _canvasGroup;
        private bool _mobileControlsVisible;
        private InputAction _toggleControlModeAction;
        private InputAction _keyboardCrouchAction;
        private bool _demoInputActionsReady;

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

            _inputManager = GlobalRegistry.NativeInputManager;
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

            if (_registeredTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void OnDisable()
        {
            DisableDemoInputActions();
            UnregisterTick();
        }

        private void OnDestroy()
        {
            UnregisterTick();
            DisposeDemoInputActions();
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
                SetMobileControlsVisible(true);
            }
            else
            {
                // Do not show mobile controls when using keyboard controls.
                Crouch();
                SetMobileControlsVisible(false);
            }
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

            transform.localRotation = Quaternion.Euler(xRot, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }

        void Walk(float deltaTime)
        {
            Vector3 displacement;
            float maxSpeed = speed, maxAcc = accelerationRate;

            // Lower the limits if we are crouching.
            if (isCrouching)
            {
                maxSpeed *= crouchFactor;
                maxAcc *= crouchFactor;
            }

            Vector2 moveInput = _inputManager != null ? _inputManager.MoveInput : Vector2.zero;

            // Find displacement based on controlType.
            switch (controlType)
            {
                case ControlType.android:
                    // Move forward and back only. Horizontal turns.
                    displacement = playerBody.transform.forward * verticalMovement;
                    break;

                case ControlType.keyboard:
                    // Only can move forward and back.
                    displacement = playerBody.transform.forward * moveInput.y;
                    break;

                case ControlType.keyboardMouse:
                default:
                    // Move in 4 directions, this is the default control.
                    displacement = playerBody.transform.forward * moveInput.y + playerBody.transform.right * moveInput.x;
                    break;
            }

            float len = displacement.magnitude;
            if (len > 0f)
            {
                rb.linearVelocity += displacement / len * deltaTime * maxAcc;

                // Clamp velocity to the maximum speed.
                if (rb.linearVelocity.magnitude > maxSpeed)
                    rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
            else
            {
                // If no buttons are pressed, decelerate.
                len = rb.linearVelocity.magnitude;
                float decelRate = accelerationRate * decelerationFactor * deltaTime;
                if (len < decelRate)
                    rb.linearVelocity = Vector3.zero;
                else
                    rb.linearVelocity -= rb.linearVelocity.normalized * decelRate;
            }
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

        private void UnregisterTick()
        {
            if (!_registeredTick)
                return;

            if (GlobalRegistry.Dispatcher != null)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

            _registeredTick = false;
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
