using UnityEngine;
using UnityEditor;
using System.IO;

public class TerrainDeepAudit
{
    public static void RunAudit()
    {
        Debug.Log("=== DEEP AUDIT STARTED ===");
        string path = "Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        if (tex != null)
        {
            Debug.Log($"Albedo Format: {tex.format}, Mips: {tex.mipmapCount}, IsLinear: {!tex.isDataSRGB}");
            Debug.Log($"Albedo Aniso: {tex.anisoLevel}, Wrap: {tex.wrapMode}, Filter: {tex.filterMode}");
        }
        else Debug.LogError("Albedo not found!");

        path = "Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset";
        tex = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        if (tex != null)
        {
            Debug.Log($"Normal Format: {tex.format}, Mips: {tex.mipmapCount}, IsLinear: {!tex.isDataSRGB}");
        }

        path = "Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset";
        tex = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        if (tex != null)
        {
            Debug.Log($"Mask Format: {tex.format}, Mips: {tex.mipmapCount}, IsLinear: {!tex.isDataSRGB}");
        }
        Debug.Log("=== DEEP AUDIT FINISHED ===");
    }
}

