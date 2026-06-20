using UnityEngine;
using UnityEditor;
using System.IO;

public class TestExtractSlice
{
    public static void Run()
    {
        Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
        if (array == null) { Debug.LogError("Array is null!"); return; }

        int res = array.width;
        RenderTexture rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false, false);

        // Slice 0
        Graphics.Blit(array, rt, 0, 0);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes("C:\\hades\\Hecton8\\Logs\\ExtractedSlice0.png", bytes);

        // Slice 1
        Graphics.Blit(array, rt, 0, 1);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();
        bytes = tex.EncodeToPNG();
        File.WriteAllBytes("C:\\hades\\Hecton8\\Logs\\ExtractedSlice1.png", bytes);

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        Debug.Log("Slices extracted successfully.");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
