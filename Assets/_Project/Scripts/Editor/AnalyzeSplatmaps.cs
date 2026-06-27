using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public class AnalyzeSplatmaps {
    public static void Execute() {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Object.FindObjectsByType<Terrain>();
        foreach(var t in terrains) {
            if (t.transform.position.sqrMagnitude < 1000) { // closest to origin
                var alphamaps = t.terrainData.GetAlphamaps(0, 0, t.terrainData.alphamapWidth, t.terrainData.alphamapHeight);
                float[] totals = new float[8];
                int count = t.terrainData.alphamapWidth * t.terrainData.alphamapHeight;
                for (int y=0; y<t.terrainData.alphamapHeight; y++) {
                    for (int x=0; x<t.terrainData.alphamapWidth; x++) {
                        for (int l=0; l<t.terrainData.alphamapLayers; l++) {
                            totals[l] += alphamaps[y,x,l];
                        }
                    }
                }
                string s = $"[FAS] Terrain {t.name} averages: ";
                for (int l=0; l<t.terrainData.alphamapLayers; l++) {
                    s += $"L{l}={(totals[l]/count):F3} ";
                }
                Debug.Log(s);
            }
        }
        EditorApplication.Exit(0);
    }
}
