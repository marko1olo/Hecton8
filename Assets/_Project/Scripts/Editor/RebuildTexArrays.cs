using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RebuildTexArrays
{
    public static void Execute()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Terrain.activeTerrains;
        Terrain t = null;
        foreach (var ter in terrains)
            if (ter.terrainData != null && ter.terrainData.terrainLayers != null && ter.terrainData.terrainLayers.Length >= 4)
            { t = ter; break; }

        if (t == null) return;
        var layers = t.terrainData.terrainLayers;

        int size = 1024;
        var albedoArray = new Texture2DArray(size, size, 4, TextureFormat.RGBA32, true, false);
        var normalArray = new Texture2DArray(size, size, 4, TextureFormat.RGBA32, true, true);
        var maskArray   = new Texture2DArray(size, size, 4, TextureFormat.RGBA32, true, true);

        for (int i = 0; i < 4; i++)
        {
            if (layers[i] == null) continue;
            var diff = layers[i].diffuseTexture;
            var norm = layers[i].normalMapTexture;
            var mask = layers[i].maskMapTexture;

            if (diff) CopyToSlice(diff, albedoArray, i, size, false);
            if (norm) CopyToSlice(norm, normalArray, i, size, true);
            if (mask) CopyToSlice(mask, maskArray, i, size, true);
        }

        albedoArray.Apply(false, true);
        normalArray.Apply(false, true);
        maskArray.Apply(false, true);

        AssetDatabase.DeleteAsset("Assets/_Project/Art/Materials/Terrain/Terrain_AlbedoArray_Fixed.asset");
        AssetDatabase.DeleteAsset("Assets/_Project/Art/Materials/Terrain/Terrain_NormalArray_Fixed.asset");
        AssetDatabase.DeleteAsset("Assets/_Project/Art/Materials/Terrain/Terrain_MaskArray_Fixed.asset");

        AssetDatabase.CreateAsset(albedoArray, "Assets/_Project/Art/Materials/Terrain/Terrain_AlbedoArray_Fixed.asset");
        AssetDatabase.CreateAsset(normalArray, "Assets/_Project/Art/Materials/Terrain/Terrain_NormalArray_Fixed.asset");
        AssetDatabase.CreateAsset(maskArray, "Assets/_Project/Art/Materials/Terrain/Terrain_MaskArray_Fixed.asset");

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        mat.SetTexture("_AlbedoArray", albedoArray);
        mat.SetTexture("_NormalArray", normalArray);
        mat.SetTexture("_MaskArray", maskArray);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        Debug.Log("[RB] Arrays rebuilt at 1024x1024 and assigned to material.");
        EditorApplication.Exit(0);
    }

    static void CopyToSlice(Texture src, Texture2DArray dest, int slice, int size, bool isLinear)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);

        var tempTex = new Texture2D(size, size, TextureFormat.RGBA32, true, isLinear);
        RenderTexture.active = rt;
        tempTex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tempTex.Apply(true); // generate mips
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        for (int m = 0; m < tempTex.mipmapCount; m++)
        {
            Color[] pixels = tempTex.GetPixels(m);
            dest.SetPixels(pixels, slice, m);
        }
        dest.Apply(false, false);
        Object.DestroyImmediate(tempTex);
    }
}
