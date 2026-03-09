using UnityEngine;

public class PlanetRotation : MonoBehaviour
{
    [Tooltip("Скорость вращения в градусах в секунду")]
    public float rotationSpeed = 2f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }
}