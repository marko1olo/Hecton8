using UnityEditor;
using UnityEngine;

public static class ReassignTextures
{
    public static void Run()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
        var mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");

        mat.SetTexture("_AlbedoArray", albedo);
        mat.SetTexture("_NormalArray", normal);
        mat.SetTexture("_MaskArray", mask);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        Debug.Log("[ReassignTextures] Done!");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
