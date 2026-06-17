using UnityEngine;
using UnityEditor;
using System.IO;

public static class CheckSceneMat {
    public static void Execute() {
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        string log = "";
        foreach (var t in terrains) {
            log += "Terrain " + t.name + ":\n";
            var mat = t.materialTemplate;
            if (mat != null) {
                log += "  Material: " + mat.name + "\n";
                log += "  Shader: " + mat.shader.name + "\n";
                var a = mat.GetTexture("_TerrainBaseMapArray");
                var n = mat.GetTexture("_TerrainNormalMapArray");
                var m = mat.GetTexture("_TerrainMaskMapArray");
                log += "  _TerrainBaseMapArray: " + (a != null ? a.name : "null") + "\n";
                log += "  _TerrainNormalMapArray: " + (n != null ? n.name : "null") + "\n";
                log += "  _TerrainMaskMapArray: " + (m != null ? m.name : "null") + "\n";
            } else {
                log += "  Material: null\n";
            }
        }
        File.WriteAllText("C:/hades/Hecton8/scene_mats.txt", log);
        EditorApplication.Exit(0);
    }
}
