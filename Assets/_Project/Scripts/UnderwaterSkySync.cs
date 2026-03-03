using UnityEngine;

[RequireComponent(typeof(Camera))]
public class UnderwaterSkySync : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        // Проверяем, опустилась ли камера под воду (Y < 0)
        if (transform.position.y < 0f)
        {
            // Под водой: выключаем скайбокс, рисуем сплошной цвет тумана
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
        }
        else
        {
            // Над водой: показываем небо и космос
            cam.clearFlags = CameraClearFlags.Skybox;
        }
    }
}