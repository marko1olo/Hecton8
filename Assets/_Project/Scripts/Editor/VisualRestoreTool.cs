using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class VisualRestoreTool
{
    public static void Run()
    {
        Debug.Log("[VisualRestoreTool] Starting visual restore...");
        var scenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        var scene = EditorSceneManager.OpenScene(scenePath);
        
        var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat");
        if (skyMat != null)
        {
            RenderSettings.skybox = skyMat;
            Debug.Log("[VisualRestoreTool] Skybox set to MAT_AegirSky_Master");
        }
        else
        {
            Debug.LogError("[VisualRestoreTool] Sky material not found!");
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[VisualRestoreTool] Scene saved.");
        
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }
}
