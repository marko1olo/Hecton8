using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public class CheckLayerSizes {
    public static void Execute() {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length > 0) {
            var layers = terrains[0].terrainData.terrainLayers;
            for(int i=0; i<layers.Length; i++) {
                if (layers[i] != null) {
                    var t = layers[i].diffuseTexture;
                    Debug.Log($"[FAS] Layer {i}: {t.width}x{t.height} - {t.format} - {layers[i].name}");
                }
            }
        }
        EditorApplication.Exit(0);
    }
}
