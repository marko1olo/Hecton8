using UnityEngine;
using UnityEditor;
using System.IO;

public static class ExportArraySlices {
    public static void Execute() {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat == null) { Debug.LogError("Mat not found"); EditorApplication.Exit(1); return; }

        var albedoArray = mat.GetTexture("_AlbedoArray") as Texture2DArray;
        if (albedoArray == null) { Debug.LogError("No AlbedoArray"); EditorApplication.Exit(1); return; }

        string outDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4";

        for (int i = 0; i < albedoArray.depth; i++) {
            RenderTexture rt = RenderTexture.GetTemporary(albedoArray.width, albedoArray.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(albedoArray, rt, i, 0);
            
            Texture2D temp = new Texture2D(albedoArray.width, albedoArray.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            temp.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            temp.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(outDir + "/AlbedoSlice_" + i + ".png", temp.EncodeToPNG());
            Debug.Log("Exported slice " + i);
            Object.DestroyImmediate(temp);
        }

        EditorApplication.Exit(0);
    }
}
