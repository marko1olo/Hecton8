using UnityEngine;
using UnityEditor;

public static class PrintTerrains {
    public static void Execute() {
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (var t in terrains) {
            Debug.Log("TERRAIN: " + t.name);
            var m = t.materialTemplate;
            if (m != null) {
                Debug.Log("Mat: " + m.name);
                Debug.Log("_AlbedoArray: " + (m.GetTexture("_AlbedoArray") != null ? m.GetTexture("_AlbedoArray").name : "null"));
                Debug.Log("_Control1: " + (m.GetTexture("_Control1") != null ? m.GetTexture("_Control1").name : "null"));
            }
        }
        EditorApplication.Exit(0);
    }
}
