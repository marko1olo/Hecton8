using Hecton8.Input;
using UnityEngine;

public class PlayerControllerDeprecated : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float verticalSpeed = 6f;

    [Header("Look")]
    [SerializeField] private float sensitivity = 0.5f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("References")]
    [SerializeField] private Transform cam;

    private CharacterController controller;
    private float rotX;
    private float rotY;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (controller == null || cam == null)
        {
            return;
        }

        InputManager inputManager = InputManager.Instance;
        Vector2 lookInput = inputManager != null ? inputManager.LookInput : Vector2.zero;
        Vector2 moveInput = inputManager != null ? inputManager.MoveInput : Vector2.zero;
        float verticalInput = inputManager != null ? inputManager.VerticalMovementInput : 0f;
        bool sprinting = inputManager != null && inputManager.IsSprinting;

        UpdateLook(lookInput);
        UpdateMovement(moveInput, verticalInput, sprinting);
    }

    private void UpdateLook(Vector2 lookInput)
    {
        rotY += lookInput.x * sensitivity;
        rotX -= lookInput.y * sensitivity;
        rotX = Mathf.Clamp(rotX, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        cam.localRotation = Quaternion.Euler(rotX, 0f, 0f);
    }

    private void UpdateMovement(Vector2 moveInput, float verticalInput, bool sprinting)
    {
        float speedMultiplier = sprinting ? sprintMultiplier : 1f;
        Vector3 planarMovement = cam.forward * moveInput.y + cam.right * moveInput.x;
        Vector3 verticalMovement = Vector3.up * verticalInput * verticalSpeed;
        Vector3 motion = planarMovement * moveSpeed * speedMultiplier + verticalMovement;

        controller.Move(motion * Time.deltaTime);
    }
}
