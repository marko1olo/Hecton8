using UnityEngine;
using UnityEditor;
using System.IO;

public class TestExtractSlice3
{
    public static void Run()
    {
        Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
        int res = array.width;
        RenderTexture rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false, false);

        Graphics.Blit(array, rt, 3, 0);
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, res, res), 0, 0); tex.Apply();
        File.WriteAllBytes("C:\\hades\\Hecton8\\Logs\\ExtractedSlice3_Fixed.png", tex.EncodeToPNG());

        RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
