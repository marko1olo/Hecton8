using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only deterministic debris placement utility.
///
/// Usage: Tools -> Spawn Debris.
/// </summary>
public class ObjectSpawner : Editor
{
    private const int DebrisCount = 50;
    private const int GridColumns = 10;
    private const int GridRows = 5;
    private const float SpawnRadius = 500f;
    private const float MinY = -25f;
    private const float MaxY = -15f;
    private const float JitterMeters = 18f;
    private const float MinScale = 0.3f;
    private const float MaxScale = 2.0f;
    private const float UnitHashToFloat = 1f / 16777215f;
    private const uint SpawnSeed = 0x9E3779B9u;
    private const string ContainerName = "Debris_Container";
    private const string DebrisNamePrefix = "Debris_";

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
            uint hash = Mix(SpawnSeed + (uint)i);
            int column = i % GridColumns;
            int row = i / GridColumns;
            float x = ResolveGridAxis(column, GridColumns, hash);
            float z = ResolveGridAxis(row, GridRows, Mix(hash + 1u));
            float y = MinY + (HashUnit(Mix(hash + 2u)) * (MaxY - MinY));
            Vector3 position = new Vector3(x, y, z);

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = DebrisNamePrefix + i.ToString("D3", CultureInfo.InvariantCulture);

            float scale = MinScale + (HashUnit(Mix(hash + 3u)) * (MaxScale - MinScale));
            float yawDegrees = HashUnit(Mix(hash + 4u)) * 360f;
            cube.transform.localScale = Vector3.one * scale;
            cube.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            cube.transform.position = position;
            cube.transform.SetParent(container.transform);

            Undo.RegisterCreatedObjectUndo(cube, "Spawn Debris");
        }

        Debug.Log("[ObjectSpawner] Created deterministic debris batch.");
    }

    private static float ResolveGridAxis(int index, int count, uint hash)
    {
        float spacing = (SpawnRadius * 2f) / count;
        float basePosition = ((index + 0.5f) * spacing) - SpawnRadius;
        return basePosition + ((HashUnit(hash) - 0.5f) * JitterMeters);
    }

    private static float HashUnit(uint hash)
    {
        return (hash & 0x00FFFFFFu) * UnitHashToFloat;
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }
}
