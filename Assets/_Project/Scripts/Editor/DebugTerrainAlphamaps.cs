using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public class DebugTerrainAlphamaps {
    public static void Execute() {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length > 0) {
            var t = terrains[0];
            Debug.Log($"[FAS] Terrain has {t.terrainData.alphamapTextures.Length} alphamaps.");
            for (int i = 0; i < t.terrainData.alphamapTextures.Length; i++) {
                var tex = t.terrainData.alphamapTextures[i];
                Debug.Log($"[FAS] Alphamap {i}: {tex.width}x{tex.height} format={tex.format}");
            }

            Material mat = t.materialTemplate;
            if (mat != null) {
                Debug.Log($"[FAS] Material _Control: {(mat.GetTexture("_Control") != null ? mat.GetTexture("_Control").name : "null")}");
                Debug.Log($"[FAS] Material _Control1: {(mat.HasProperty("_Control1") ? (mat.GetTexture("_Control1") != null ? mat.GetTexture("_Control1").name : "null") : "no property")}");

                // Force assign to materialTemplate for our single-pass shader!
                if (t.terrainData.alphamapTextures.Length > 0) mat.SetTexture("_Control", t.terrainData.alphamapTextures[0]);
                if (t.terrainData.alphamapTextures.Length > 1) mat.SetTexture("_Control1", t.terrainData.alphamapTextures[1]);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                Debug.Log("[FAS] Forced assigned alphamaps to _Control and _Control1 in materialTemplate.");
            } else {
                Debug.Log("[FAS] No material template on terrain!");
            }
        }
        EditorApplication.Exit(0);
    }
}
