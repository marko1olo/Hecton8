using UnityEngine;
using UnityEditor;
public class CheckTerrainLayers {
    public static void Execute() {
        var terrains = Object.FindObjectsByType<Terrain>();
        if (terrains.Length > 0) {
            var layers = terrains[0].terrainData.terrainLayers;
            Debug.Log($"[FAS] Terrain Layers Count: {layers.Length}");
            for(int i=0; i<layers.Length; i++) {
                Debug.Log($"[FAS] Layer {i}: {(layers[i] != null ? layers[i].name : "null")}");
            }
        }
        EditorApplication.Exit(0);
    }
}
