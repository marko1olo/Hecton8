using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-skript dlya bystrogo spavna oblomkov (debris) v stsene.
/// Sozdaet 50 standartnyh kubikov v sluchaynyh pozitsiyah,
/// imitiruya oblomki na dne ili v tolsche vody.
///
/// Ispolzovanie: verhnee menyu -> Tools -> Spawn Debris
/// </summary>
public class ObjectSpawner : Editor
{
    private const int DebrisCount = 50;
    private const float SpawnRadius = 500f;
    private const float MinY = -25f;
    private const float MaxY = -15f;
    private const string ContainerName = "Debris_Container";

    [MenuItem("Tools/Spawn Debris")]
    private static void SpawnDebris()
    {
        GameObject container = GameObject.Find(ContainerName);

        if (container == null)
        {
            container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Create Debris Container");
        }

        for (int i = 0; i < DebrisCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * SpawnRadius;

            Vector3 position = new Vector3(
                randomCircle.x,
                Random.Range(MinY, MaxY),
                randomCircle.y);

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Debris_{i:D3}";

            float scale = Random.Range(0.3f, 2.0f);
            cube.transform.localScale = Vector3.one * scale;
            cube.transform.rotation = Random.rotation;
            cube.transform.position = position;
            cube.transform.SetParent(container.transform);

            Undo.RegisterCreatedObjectUndo(cube, "Spawn Debris");
        }

        Debug.Log($"[ObjectSpawner] Sozdano {DebrisCount} oblomkov v radiuse {SpawnRadius}m (Y: {MinY}…{MaxY}). Roditel: \"{ContainerName}\"");
    }
}
