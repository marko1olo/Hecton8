using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-скрипт для быстрого спавна обломков (debris) в сцене.
/// Создаёт 50 стандартных кубиков в случайных позициях,
/// имитируя обломки на дне или в толще воды.
///
/// Использование: верхнее меню → Tools → Spawn Debris
/// </summary>
public class ObjectSpawner : Editor
{
    private const int    DebrisCount    = 50;
    private const float  SpawnRadius    = 500f;
    private const float  MinY           = -25f;
    private const float  MaxY           = -15f;
    private const string ContainerName  = "Debris_Container";

    [MenuItem("Tools/Spawn Debris")]
    private static void SpawnDebris()
    {
        // ── Находим или создаём контейнер ─────────────────────────
        GameObject container = GameObject.Find(ContainerName);

        if (container == null)
        {
            container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Create Debris Container");
        }

        // ── Спавним кубики ─────────────────────────────
        for (int i = 0; i < DebrisCount; i++)
        {
            // Случайная позиция в круге радиусом SpawnRadius
            Vector2 randomCircle = Random.insideUnitCircle * SpawnRadius;

            Vector3 position = new Vector3(
                randomCircle.x,                        // X: −500 … +500
                Random.Range(MinY, MaxY),              // Y: −25  … −15
                randomCircle.y                         // Z: −500 … +500
            );

            // Создаём стандартный куб
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Debris_{i:D3}";

            // Случайный масштаб для разнообразия
            float scale = Random.Range(0.3f, 2.0f);
            cube.transform.localScale = Vector3.one * scale;

            // Случайный поворот
            cube.transform.rotation = Random.rotation;

            // Позиция и родитель
            cube.transform.position = position;
            cube.transform.SetParent(container.transform);

            // Регистрируем для Undo (Ctrl+Z)
            Undo.RegisterCreatedObjectUndo(cube, "Spawn Debris");
        }

        Debug.Log($"[ObjectSpawner] Создано {DebrisCount} обломков " +
                  $"в радиусе {SpawnRadius}м (Y: {MinY}…{MaxY}). " +
                  $"Родитель: \"{ContainerName}\"");
    }
}