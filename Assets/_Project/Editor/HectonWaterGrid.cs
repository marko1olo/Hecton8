using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public class HectonWaterGrid : EditorWindow
{
    [MenuItem("Hecton/Generate Water Mesh")]
    static void Init()
    {
        // Настройки сетки
        int size = 200; // Количество клеток (200x200 = 40k вершин)
        float cellSize = 10f; // Размер одной клетки в метрах

        Mesh mesh = new Mesh();
        mesh.name = "HighPolyWater";

        Vector3[] vertices = new Vector3[(size + 1) * (size + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[size * size * 6];

        for (int i = 0, z = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++, i++)
            {
                vertices[i] = new Vector3(x * cellSize - (size * cellSize * 0.5f), 0, z * cellSize - (size * cellSize * 0.5f));
                uv[i] = new Vector2((float)x / size, (float)z / size);
            }
        }

        int ti = 0;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = x + z * (size + 1);
                triangles[ti] = i; triangles[ti + 1] = i + size + 1; triangles[ti + 2] = i + 1;
                triangles[ti + 3] = i + 1; triangles[ti + 4] = i + size + 1; triangles[ti + 5] = i + size + 2;
                ti += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Сохраняем как ассет
        string path = "Assets/HectonWaterMesh.asset";
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        
        // Создаем объект на сцене
        GameObject go = new GameObject("Hecton Ocean");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();
        Selection.activeGameObject = go;
        
        Debug.Log("Вода готова: " + path);
    }
}
#endif