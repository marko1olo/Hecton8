using UnityEngine;
using UnityEditor;
using System.IO;

public class TestSplat
{
    public static void Run()
    {
        var terrains = UnityEngine.Terrain.activeTerrains;
        if (terrains.Length > 0)
        {
            var data = terrains[0].terrainData;
            if (data != null && data.alphamapTextures.Length > 0)
            {
                for(int i=0; i<data.alphamapTextures.Length; i++)
                {
                    var tex = data.alphamapTextures[i];
                    if (tex != null)
                    {
                        var t2d = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                        RenderTexture currentRT = RenderTexture.active;
                        RenderTexture renderTexture = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                        Graphics.Blit(tex, renderTexture);
                        RenderTexture.active = renderTexture;
                        t2d.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                        t2d.Apply();
                        RenderTexture.active = currentRT;
                        RenderTexture.ReleaseTemporary(renderTexture);
                        
                        byte[] bytes = t2d.EncodeToPNG();
                        File.WriteAllBytes("C:/hades/Hecton8/Logs/Splatmap_" + i + ".png", bytes);
                    }
                }
            }
        }
    }
}
