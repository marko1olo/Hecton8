using UnityEngine;
using UnityEditor;
using System.IO;

public static class CheckMat {
    public static void Execute() {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat == null) return;
        string log = "Shader: " + mat.shader.name + "\n";
        log += "_TerrainBaseMapArray: " + (mat.GetTexture("_TerrainBaseMapArray") != null ? mat.GetTexture("_TerrainBaseMapArray").name : "null") + "\n";
        log += "_TerrainNormalMapArray: " + (mat.GetTexture("_TerrainNormalMapArray") != null ? mat.GetTexture("_TerrainNormalMapArray").name : "null") + "\n";
        log += "_TerrainMaskMapArray: " + (mat.GetTexture("_TerrainMaskMapArray") != null ? mat.GetTexture("_TerrainMaskMapArray").name : "null") + "\n";
        log += "_Control1: " + (mat.GetTexture("_Control1") != null ? mat.GetTexture("_Control1").name : "null") + "\n";
        log += "_Control2: " + (mat.GetTexture("_Control2") != null ? mat.GetTexture("_Control2").name : "null") + "\n";
        log += "Keywords: " + string.Join(", ", mat.shaderKeywords) + "\n";
        File.WriteAllText("C:/hades/Hecton8/terrain_mat_dump2.txt", log);
        EditorApplication.Exit(0);
    }
}
