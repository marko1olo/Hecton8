using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class BakeTerrainArrays {
    public static void Execute() {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length == 0) {
            Debug.Log("[FAS] No terrains found.");
            EditorApplication.Exit(1);
            return;
        }
        var layers = terrains[0].terrainData.terrainLayers;
        int count = layers.Length;
        int size = 1024;
        
        Texture2DArray albedoArray = new Texture2DArray(size, size, count, TextureFormat.RGBA32, true, false);
        Texture2DArray normalArray = new Texture2DArray(size, size, count, TextureFormat.RGBA32, true, true);
        Texture2DArray maskArray = new Texture2DArray(size, size, count, TextureFormat.RGBA32, true, true);
        
        RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture rtLinear = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        
        Texture2D tempTex = new Texture2D(size, size, TextureFormat.RGBA32, false, false); // sRGB
        Texture2D tempTexLin = new Texture2D(size, size, TextureFormat.RGBA32, false, true); // Linear
        
        Vector4 uvScale0_3 = Vector4.zero;
        Vector4 uvScale4_7 = Vector4.zero;

        for(int i=0; i<count; i++) {
            var l = layers[i];
            
            // Extract UV Scale
            float scaleX = l.tileSize.x > 0.001f ? 1.0f / l.tileSize.x : 0.2f;
            if (i == 0) uvScale0_3.x = scaleX;
            else if (i == 1) uvScale0_3.y = scaleX;
            else if (i == 2) uvScale0_3.z = scaleX;
            else if (i == 3) uvScale0_3.w = scaleX;
            else if (i == 4) uvScale4_7.x = scaleX;
            else if (i == 5) uvScale4_7.y = scaleX;
            else if (i == 6) uvScale4_7.z = scaleX;
            else if (i == 7) uvScale4_7.w = scaleX;

            // Albedo
            if (l.diffuseTexture != null) {
                Graphics.Blit(l.diffuseTexture, rt);
                RenderTexture.active = rt;
                tempTex.ReadPixels(new Rect(0,0,size,size), 0, 0);
                tempTex.Apply();
                Color[] pixels = tempTex.GetPixels(0);
                Color min = l.diffuseRemapMin;
                Color max = l.diffuseRemapMax;
                for (int p = 0; p < pixels.Length; p++) {
                    pixels[p].r = Mathf.Lerp(min.r, max.r, pixels[p].r);
                    pixels[p].g = Mathf.Lerp(min.g, max.g, pixels[p].g);
                    pixels[p].b = Mathf.Lerp(min.b, max.b, pixels[p].b);
                    // alpha usually unchanged or remapped? We leave it for now.
                }
                albedoArray.SetPixels(pixels, i, 0);
            }
            // Normal
            if (l.normalMapTexture != null) {
                Graphics.Blit(l.normalMapTexture, rtLinear);
                RenderTexture.active = rtLinear;
                tempTexLin.ReadPixels(new Rect(0,0,size,size), 0, 0);
                tempTexLin.Apply();
                normalArray.SetPixels(tempTexLin.GetPixels(0), i, 0);
            } else {
                Color[] flat = new Color[size*size];
                for(int j=0; j<flat.Length; j++) flat[j] = new Color(0.5f, 0.5f, 1f, 1f);
                normalArray.SetPixels(flat, i, 0);
            }
            // Mask
            if (l.maskMapTexture != null) {
                Graphics.Blit(l.maskMapTexture, rtLinear);
                RenderTexture.active = rtLinear;
                tempTexLin.ReadPixels(new Rect(0,0,size,size), 0, 0);
                tempTexLin.Apply();
                maskArray.SetPixels(tempTexLin.GetPixels(0), i, 0);
            } else {
                Color defaultMask = new Color(0f, 1f, 0f, l.smoothness);
                Color[] mflat = new Color[size*size];
                for(int j=0; j<mflat.Length; j++) mflat[j] = defaultMask;
                maskArray.SetPixels(mflat, i, 0);
            }
        }
        
        albedoArray.wrapMode = TextureWrapMode.Repeat;
        albedoArray.filterMode = FilterMode.Trilinear;
        albedoArray.anisoLevel = 16;
        albedoArray.Apply();
        
        normalArray.wrapMode = TextureWrapMode.Repeat;
        normalArray.filterMode = FilterMode.Trilinear;
        normalArray.anisoLevel = 16;
        normalArray.Apply();
        
        maskArray.wrapMode = TextureWrapMode.Repeat;
        maskArray.filterMode = FilterMode.Trilinear;
        maskArray.anisoLevel = 16;
        maskArray.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        RenderTexture.ReleaseTemporary(rtLinear);
        
        string aPath = "Assets/_Project/Art/Textures/TerrainArrays/Terrain_AlbedoArray.asset";
        string nPath = "Assets/_Project/Art/Textures/TerrainArrays/Terrain_NormalArray.asset";
        string mPath = "Assets/_Project/Art/Textures/TerrainArrays/Terrain_MaskArray.asset";
        
        AssetDatabase.CreateAsset(albedoArray, aPath);
        AssetDatabase.CreateAsset(normalArray, nPath);
        AssetDatabase.CreateAsset(maskArray, mPath);
        
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat != null) {
            mat.SetTexture("_AlbedoArray", albedoArray);
            mat.SetTexture("_NormalArray", normalArray);
            mat.SetTexture("_MaskArray", maskArray);
            mat.SetVector("_TerrainUVScale0_3", uvScale0_3);
            mat.SetVector("_TerrainUVScale4_7", uvScale4_7);
            EditorUtility.SetDirty(mat);
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"[FAS] Successfully baked 8-layer arrays to {aPath}");
        EditorApplication.Exit(0);
    }
}
