using UnityEngine;
using UnityEditor;
using System.Linq;

public class CheckTerrainData {
    public static void Execute() {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        Terrain t = Object.FindAnyObjectByType<Terrain>();
        if (t == null) {
            Debug.Log("[CTD] No terrain found in scene.");
            return;
        }
        
        var td = t.terrainData;
        if (td == null) {
            Debug.Log("[CTD] Terrain has no TerrainData.");
            return;
        }
        
        Debug.Log($"[CTD] TerrainLayers count: {td.terrainLayers?.Length}");
        if (td.terrainLayers != null) {
            for (int i = 0; i < td.terrainLayers.Length; i++) {
                var l = td.terrainLayers[i];
                Debug.Log($"[CTD] Layer {i}: {(l != null ? l.name : "null")}");
            }
        }
        
        var alphamaps = td.alphamapTextures;
        Debug.Log($"[CTD] Alphamaps count: {alphamaps?.Length}");
        
        if (alphamaps != null && alphamaps.Length > 0) {
            var tex = alphamaps[0];
            Debug.Log($"[CTD] Alphamap 0 size: {tex.width}x{tex.height}, format: {tex.format}");
            
            // Sample a few pixels from the center
            int cx = tex.width / 2;
            int cy = tex.height / 2;
            Color[] pixels = tex.GetPixels(cx - 2, cy - 2, 5, 5);
            Debug.Log($"[CTD] Center pixel colors:");
            foreach(var p in pixels) {
                Debug.Log($"[CTD]   {p.r:F2}, {p.g:F2}, {p.b:F2}, {p.a:F2}");
            }
        }
    }
}
