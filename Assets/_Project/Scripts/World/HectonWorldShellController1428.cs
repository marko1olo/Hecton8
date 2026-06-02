using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class HectonWorldShellController1428 : MonoBehaviour
    {
        [SerializeField] private Transform cameraRig;
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float verticalSpeed = 4.0f;
        [SerializeField] private float lookSpeed = 0.11f;
        [SerializeField] private float idleDriftMeters = 0.18f;

        private Transform _transform;
        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            _transform = transform;

            if (cameraRig == null && Camera.main != null)
                cameraRig = Camera.main.transform;

            Vector3 euler = cameraRig != null ? cameraRig.rotation.eulerAngles : _transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizePitch(euler.x);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            ReadLookInput(out float lookX, out float lookY);
            _yaw += lookX * lookSpeed;
            _pitch = Mathf.Clamp(_pitch - lookY * lookSpeed, -38f, 38f);

            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            _transform.rotation = yawRotation;

            ReadMoveInput(out float strafe, out float forward, out float vertical);
            Vector3 move =
                yawRotation * new Vector3(strafe, 0f, forward);

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            _transform.position += move * (moveSpeed * deltaTime);
            if (vertical != 0f)
                _transform.position += Vector3.up * (vertical * verticalSpeed * deltaTime);

            UpdateCameraRig(deltaTime);
        }

        private void UpdateCameraRig(float deltaTime)
        {
            if (cameraRig == null)
                return;

            float drift = Mathf.Sin(Time.time * 0.42f) * idleDriftMeters;
            Vector3 localOffset = new Vector3(0f, 1.32f + drift, -5.35f);
            Quaternion cameraRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            cameraRig.SetPositionAndRotation(
                _transform.position + cameraRotation * localOffset,
                cameraRotation);
        }

        private static float NormalizePitch(float pitch)
        {
            return pitch > 180f ? pitch - 360f : pitch;
        }

        private void ReadMoveInput(out float strafe, out float forward, out float vertical)
        {
            strafe = 0f;
            forward = 0f;
            vertical = 0f;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                strafe -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                strafe += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                forward += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                forward -= 1f;
            if (keyboard.spaceKey.isPressed || keyboard.eKey.isPressed)
                vertical += 1f;
            if (keyboard.leftCtrlKey.isPressed || keyboard.qKey.isPressed)
                vertical -= 1f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                strafe -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                strafe += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                forward += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                forward -= 1f;
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E))
                vertical += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.Q))
                vertical -= 1f;
#endif
        }

        private static void ReadLookInput(out float lookX, out float lookY)
        {
            lookX = 0f;
            lookY = 0f;

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            lookX = delta.x;
            lookY = delta.y;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButton(1))
                return;

            lookX = Input.GetAxisRaw("Mouse X");
            lookY = Input.GetAxisRaw("Mouse Y");
#endif
        }
    }
}
