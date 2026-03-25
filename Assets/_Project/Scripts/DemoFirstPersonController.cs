using Hecton8.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ScifiOffice
{
    public class DemoFirstPersonController : MonoBehaviour
    {
        Rigidbody rb;
        CapsuleCollider col;
        bool isCrouching;

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

        private void Start()
        {
            rb = playerBody.GetComponent<Rigidbody>();
            col = playerBody.GetComponent<CapsuleCollider>();
            _inputManager = InputManager.Instance;

            if (controlType == ControlType.keyboardMouse)
                Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            Walk();
            Look();

            // E to switch keyboard control type between keyboardMouse and keyboard.
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
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
                canvas.SetActive(true);
            }
            else
            {
                // Do not show mobile controls when using keyboard controls.
                Crouch();
                canvas.SetActive(false);
            }
        }

        public void Look()
        {
            float mouseX = 0f;
            float mouseY = 0f;

            switch (controlType)
            {
                case ControlType.android:
                    mouseX = horizontalMovement * Time.deltaTime * mouseSensitivity;
                    break;

                case ControlType.keyboard:
                    // Get changes to look left and right only. Player cannot look up and down.
                    mouseX = (_inputManager != null ? _inputManager.MoveInput.x : 0f) * mouseSensitivity * Time.deltaTime;
                    mouseY = 0f;
                    break;

                default:
                case ControlType.keyboardMouse:
                    // Use mouse to control where to look. Can look in all directions.
                    if (_inputManager != null)
                    {
                        Vector2 look = _inputManager.LookInput;
                        mouseX = look.x * mouseSensitivity * Time.deltaTime;
                        mouseY = look.y * mouseSensitivity * Time.deltaTime;
                    }
                    break;
            }

            // Rotate playerBody.
            xRot -= mouseY;
            xRot = Mathf.Clamp(xRot, -90f, 90f);

            transform.localRotation = Quaternion.Euler(xRot, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }

        void Walk()
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
                rb.linearVelocity += displacement / len * Time.deltaTime * maxAcc;

                // Clamp velocity to the maximum speed.
                if (rb.linearVelocity.magnitude > maxSpeed)
                    rb.linearVelocity = rb.linearVelocity.normalized * speed;
            }
            else
            {
                // If no buttons are pressed, decelerate.
                len = rb.linearVelocity.magnitude;
                float decelRate = accelerationRate * decelerationFactor * Time.deltaTime;
                if (len < decelRate)
                    rb.linearVelocity = Vector3.zero;
                else
                    rb.linearVelocity -= rb.linearVelocity.normalized * decelRate;
            }
        }

        void Crouch()
        {
            bool keyboardCrouch = Keyboard.current != null &&
                                  (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.leftShiftKey.isPressed);
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
