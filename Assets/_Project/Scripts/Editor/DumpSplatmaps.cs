using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;
public class DumpSplatmaps {
    public static void Execute() {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length > 0) {
            var t = terrains[0];
            Debug.Log($"[FAS] Terrain {t.name} alphamap resolution: {t.terrainData.alphamapResolution}");
            for (int i = 0; i < t.terrainData.alphamapTextures.Length; i++) {
                var tex = t.terrainData.alphamapTextures[i];
                var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(tex, rt);
                var temp = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
                RenderTexture.active = rt;
                temp.ReadPixels(new Rect(0,0,tex.width,tex.height), 0, 0);
                temp.Apply();
                byte[] bytes = temp.EncodeToPNG();
                string path = $"C:\\Users\\danat\\.gemini\\antigravity\\brain\\389e4a53-b1e6-440c-b190-0f5c509fa8c4\\Splatmap_{i}.png";
                File.WriteAllBytes(path, bytes);
                Debug.Log($"[FAS] Saved splatmap {i} to {path} ({bytes.Length} bytes)");
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
        EditorApplication.Exit(0);
    }
}
