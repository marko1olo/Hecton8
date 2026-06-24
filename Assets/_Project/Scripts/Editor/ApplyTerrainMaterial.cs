using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ApplyTerrainMaterial
{
    public static void Execute()
    {
        string[] scenes = {
            "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity",
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity"
        };

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat == null)
        {
            Debug.LogError("Material not found!");
            EditorApplication.Exit(1);
        }

        foreach (var s in scenes)
        {
            var scene = EditorSceneManager.OpenScene(s);
            var terrains = Terrain.activeTerrains;
            int count = 0;
            foreach (var t in terrains)
            {
                t.materialTemplate = mat;
                EditorUtility.SetDirty(t);
                count++;
            }
            Debug.Log($"[ApplyMat] Applied material to {count} terrains in {scene.name}.");
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("[ApplyMat] Done.");
        EditorApplication.Exit(0);
    }
}
