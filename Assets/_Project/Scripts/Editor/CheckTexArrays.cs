using UnityEngine;
using UnityEditor;
using System.IO;

public static class CheckTexArrays
{
    public static void Execute()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat == null) { Debug.LogError("No mat"); return; }

        var albedoArray = mat.GetTexture("_AlbedoArray") as Texture2DArray;
        if (albedoArray == null) { Debug.LogError("No array"); return; }

        Debug.Log($"Array: {albedoArray.width}x{albedoArray.height} depth={albedoArray.depth}");

        // Save first mip of each slice to PNG to verify contents
        for (int i = 0; i < albedoArray.depth; i++)
        {
            var px = albedoArray.GetPixels(i, 0);
            var tex = new Texture2D(albedoArray.width, albedoArray.height, albedoArray.format, false);
            tex.SetPixels(px);
            tex.Apply();

            string path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) +
                          $"/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4/Slice_{i}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[CHECK] Saved Slice {i}");
        }

        EditorApplication.Exit(0);
    }
}
