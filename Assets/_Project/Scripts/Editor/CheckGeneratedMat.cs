using UnityEngine;
using UnityEditor;
using System.IO;
using MapMagic.Core;
using System.Collections;
using System.Linq;

public static class CheckGeneratedMat {
    static float startTime = 0f;
    static MapMagicObject mm;

    public static void Execute() {
        mm = Object.FindAnyObjectByType<MapMagicObject>();
        if (mm == null) {
            File.WriteAllText("C:/hades/Hecton8/generated_mat.txt", "No MapMagic");
            EditorApplication.Exit(1);
        }
        mm.StartGenerate();
        startTime = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update += OnUpdate;
    }
    
    static void OnUpdate() {
        if (EditorApplication.timeSinceStartup - startTime < 10f) {
            return;
        }
        EditorApplication.update -= OnUpdate;
        
        var terrains = Object.FindObjectsByType<Terrain>();
        string log = "";
        foreach (var t in terrains) {
            log += "Terrain " + t.name + ":\n";
            var mat = t.materialTemplate;
            if (mat != null) {
                log += "  Material: " + mat.name + " (Instance ID: " + mat.GetHashCode() + ")\n";
                log += "  Shader: " + mat.shader.name + "\n";
                log += "  _AlbedoArray: " + (mat.GetTexture("_AlbedoArray") != null ? mat.GetTexture("_AlbedoArray").name : "null") + "\n";
                log += "  _NormalArray: " + (mat.GetTexture("_NormalArray") != null ? mat.GetTexture("_NormalArray").name : "null") + "\n";
                log += "  _MaskArray: " + (mat.GetTexture("_MaskArray") != null ? mat.GetTexture("_MaskArray").name : "null") + "\n";
                log += "  _Control: " + (mat.GetTexture("_Control") != null ? mat.GetTexture("_Control").name : "null") + "\n";
                log += "  _Control1: " + (mat.GetTexture("_Control1") != null ? mat.GetTexture("_Control1").name : "null") + "\n";
                log += "  _TerrainBaseMapArray: " + (mat.GetTexture("_TerrainBaseMapArray") != null ? mat.GetTexture("_TerrainBaseMapArray").name : "null") + "\n";
            }
        }
        File.WriteAllText("C:/hades/Hecton8/generated_mat.txt", log);
        EditorApplication.Exit(0);
    }
}

