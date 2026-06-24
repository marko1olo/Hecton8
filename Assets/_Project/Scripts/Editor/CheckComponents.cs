using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CheckComponents
{
    public static void Execute()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Terrain.activeTerrains;
        if (terrains.Length > 0)
        {
            var t = terrains[0];
            Debug.Log($"[COMP] Terrain GO: {t.name}");
            foreach (var comp in t.GetComponents<Component>())
            {
                Debug.Log($"[COMP] - {comp.GetType().Name}");
            }
            Debug.Log($"[COMP] Material Template: {(t.materialTemplate ? t.materialTemplate.name : "null")}");
        }
        EditorApplication.Exit(0);
    }
}
