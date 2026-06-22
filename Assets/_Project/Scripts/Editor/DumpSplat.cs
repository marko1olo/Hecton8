using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class DumpSplat
{
    public static void Execute()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Terrain.activeTerrains;
        if (terrains.Length == 0) return;
        
        var t = terrains[0];
        if (t.terrainData == null) return;

        var maps = t.terrainData.alphamapTextures;
        for (int i=0; i<maps.Length; i++) {
            var tex = maps[i];
            var rt = new RenderTexture(tex.width, tex.height, 0);
            Graphics.Blit(tex, rt);
            var t2d = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            t2d.ReadPixels(new Rect(0,0,tex.width,tex.height), 0, 0);
            t2d.Apply();
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            
            string path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4/Splat_" + i + ".png";
            File.WriteAllBytes(path, t2d.EncodeToPNG());
            Object.DestroyImmediate(t2d);
            Debug.Log("[DUMPSPLAT] Dumped " + path);
        }
        EditorApplication.Exit(0);
    }
}
