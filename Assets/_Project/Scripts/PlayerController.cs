using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float sensitivity = 0.5f; // Поставил маленькую, чтобы не колбасило
    public Transform cam;

    private float rotX = 0f;
    private float rotY = 0f;
    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        if (cam == null) cam = Camera.main.transform;
    }

    void Update()
    {
        // ПОВОРОТ
        rotY += Input.GetAxisRaw("Mouse X") * sensitivity;
        rotX -= Input.GetAxisRaw("Mouse Y") * sensitivity;
        rotX = Mathf.Clamp(rotX, -80f, 80f);

        transform.rotation = Quaternion.Euler(0, rotY, 0);
        cam.localRotation = Quaternion.Euler(rotX, 0, 0);

        // ДВИЖЕНИЕ
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        float up = 0;
        if (Input.GetKey(KeyCode.E)) up = 1;
        if (Input.GetKey(KeyCode.Q)) up = -1;

        // Летим СТРОГО туда, куда смотрим
        Vector3 move = cam.forward * v + cam.right * h + Vector3.up * up;
        controller.Move(move * moveSpeed * Time.deltaTime);
    }
}