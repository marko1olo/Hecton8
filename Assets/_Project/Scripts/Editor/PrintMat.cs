using UnityEngine;
using UnityEditor;

public static class PrintMat {
    public static void Execute() {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat != null) {
            Debug.Log("Mat: " + mat.name);
            Debug.Log("_Control: " + (mat.GetTexture("_Control") != null ? mat.GetTexture("_Control").name : "null"));
            Debug.Log("_Control1: " + (mat.GetTexture("_Control1") != null ? mat.GetTexture("_Control1").name : "null"));
            Debug.Log("_AlbedoArray: " + (mat.GetTexture("_AlbedoArray") != null ? mat.GetTexture("_AlbedoArray").name : "null"));
        }
        EditorApplication.Exit(0);
    }
}
