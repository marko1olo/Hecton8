using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CheckComponents
{
    private static readonly List<Component> s_ComponentsCache = new List<Component>();

    public static void Execute()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Terrain.activeTerrains;
        if (terrains.Length > 0)
        {
            var t = terrains[0];
            Debug.Log($"[COMP] Terrain GO: {t.name}");
            t.GetComponents(s_ComponentsCache);
            foreach (var comp in s_ComponentsCache)
            {
                Debug.Log($"[COMP] - {comp.GetType().Name}");
            }
            Debug.Log($"[COMP] Material Template: {(t.materialTemplate ? t.materialTemplate.name : "null")}");
        }
        EditorApplication.Exit(0);
    }
}
