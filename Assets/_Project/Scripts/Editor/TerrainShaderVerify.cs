using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// End-to-end terrain shader verification:
/// 1. Opens 020_RENDER_SANDBOX
/// 2. Builds Texture2DArrays from terrain layer textures
/// 3. Assigns HectonTerrainMaterial with our custom shader
/// 4. Captures screenshots from the sandbox camera
/// </summary>
public static class TerrainShaderVerify
{
    static string screenshotDir;

    public static void Execute()
    {
        screenshotDir = Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
        Debug.Log("[Verify] Starting terrain shader verification...");

        // Open sandbox scene
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");

        var terrains = Terrain.activeTerrains;
        Debug.Log("[Verify] Found " + terrains.Length + " terrains");

        if (terrains.Length == 0)
        {
            Debug.LogError("[Verify] No terrains found!");
            EditorApplication.Exit(1);
            return;
        }

        // Find our shader
        var shader = Shader.Find("Hecton8/URP/Terrain_TextureArray");
        if (shader == null)
        {
            Debug.LogError("[Verify] Custom shader not found!");
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("[Verify] Shader found, isSupported=" + shader.isSupported);

        // Get layers from the first terrain with data
        Terrain refTerrain = null;
        foreach (var t in terrains)
        {
            if (t.terrainData != null && t.terrainData.terrainLayers != null && t.terrainData.terrainLayers.Length > 0)
            {
                refTerrain = t;
                break;
            }
        }

        if (refTerrain == null)
        {
            Debug.LogError("[Verify] No terrain with valid layers found!");
            EditorApplication.Exit(1);
            return;
        }

        var layers = refTerrain.terrainData.terrainLayers;
        int layerCount = Mathf.Min(layers.Length, 4); // Our shader supports 4 layers in base pass
        Debug.Log("[Verify] Using " + layerCount + " layers from " + refTerrain.name);

        // Build Texture2DArrays
        var albedoArray = BuildTextureArray(layers, layerCount, TextureType.Albedo);
        var normalArray = BuildTextureArray(layers, layerCount, TextureType.Normal);
        var maskArray = BuildTextureArray(layers, layerCount, TextureType.Mask);

        if (albedoArray == null)
        {
            Debug.LogError("[Verify] Failed to build texture arrays!");
            EditorApplication.Exit(1);
            return;
        }

        // Load or create material
        var matPath = "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            var dir = Path.GetDirectoryName(matPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log("[Verify] Created new material at " + matPath);
        }
        else
        {
            mat.shader = shader;
            Debug.Log("[Verify] Updated existing material shader");
        }

        // Assign textures
        mat.SetTexture("_AlbedoArray", albedoArray);
        mat.SetTexture("_NormalArray", normalArray);
        mat.SetTexture("_MaskArray", maskArray);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[Verify] Material configured with texture arrays");

        // Assign material to ALL terrains
        foreach (var t in terrains)
        {
            t.materialTemplate = mat;
            Debug.Log("[Verify] Assigned material to " + t.name);
        }

        // Capture screenshot
        var cam = Camera.main;
        if (cam == null)
        {
            // Find any camera
            var cams = Object.FindObjectsByType<Camera>();
            if (cams.Length > 0) cam = cams[0];
        }

        if (cam != null)
        {
            Debug.Log("[Verify] Camera: " + cam.name + " at " + cam.transform.position);
            CaptureFromCamera(cam, "TerrainVerify_0.png");

            // Move camera to look at terrain
            if (refTerrain.terrainData != null)
            {
                var terrainPos = refTerrain.transform.position;
                var terrainSize = refTerrain.terrainData.size;
                var center = terrainPos + terrainSize * 0.5f;
                center.y = terrainPos.y + terrainSize.y * 0.3f;
                cam.transform.position = center + Vector3.up * 50f + Vector3.back * 100f;
                cam.transform.LookAt(center);
                Debug.Log("[Verify] Repositioned camera to " + cam.transform.position + " looking at " + center);
                CaptureFromCamera(cam, "TerrainVerify_1.png");
            }
        }
        else
        {
            Debug.LogError("[Verify] No camera found!");
        }

        // Save scene changes
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Verify] Done! Scene saved.");
        EditorApplication.Exit(0);
    }

    enum TextureType { Albedo, Normal, Mask }

    static Texture2DArray BuildTextureArray(TerrainLayer[] layers, int count, TextureType type)
    {
        // Determine size from first valid texture
        int size = 512;
        TextureFormat fmt = TextureFormat.RGBA32;

        Texture2D[] sources = new Texture2D[count];
        for (int i = 0; i < count; i++)
        {
            Texture2D src = null;
            switch (type)
            {
                case TextureType.Albedo:
                    src = layers[i]?.diffuseTexture;
                    break;
                case TextureType.Normal:
                    src = layers[i]?.normalMapTexture;
                    break;
                case TextureType.Mask:
                    src = layers[i]?.maskMapTexture;
                    break;
            }

            if (src != null)
            {
                // Make readable
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(src)) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                sources[i] = src;
                size = src.width;
                fmt = src.format;
            }

            Debug.Log("[Verify] " + type + " layer[" + i + "]: " + (src != null ? src.name + " " + src.width + "x" + src.height : "NULL"));
        }

        // Create array
        var arr = new Texture2DArray(size, size, count, fmt, true);

        for (int i = 0; i < count; i++)
        {
            if (sources[i] != null)
            {
                // Resize if needed
                var src = sources[i];
                if (src.width != size || src.height != size)
                {
                    var rt = RenderTexture.GetTemporary(size, size);
                    Graphics.Blit(src, rt);
                    var resized = new Texture2D(size, size, fmt, true);
                    RenderTexture.active = rt;
                    resized.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    resized.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);
                    Graphics.CopyTexture(resized, 0, arr, i);
                    Object.DestroyImmediate(resized);
                }
                else
                {
                    Graphics.CopyTexture(src, 0, arr, i);
                }
            }
            else
            {
                // Fill with default color
                var fallback = new Texture2D(size, size, TextureFormat.RGBA32, true);
                Color fillColor = type == TextureType.Normal ? new Color(0.5f, 0.5f, 1f, 1f) :
                                  type == TextureType.Mask ? new Color(0f, 1f, 0f, 0.5f) :
                                  Color.gray;
                var pixels = new Color[size * size];
                for (int p = 0; p < pixels.Length; p++) pixels[p] = fillColor;
                fallback.SetPixels(pixels);
                fallback.Apply();
                Graphics.CopyTexture(fallback, 0, arr, i);
                Object.DestroyImmediate(fallback);
            }
        }

        arr.Apply();

        // Save as asset
        string arrayName = "Terrain_" + type + "Array";
        string path = "Assets/_Project/Art/Materials/Terrain/" + arrayName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(arr, path);
        Debug.Log("[Verify] Created " + type + " array: " + path);
        return arr;
    }

    static void CaptureFromCamera(Camera cam, string filename)
    {
        int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);

        var bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        // Save to artifacts dir
        string artifactDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
            + "/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4";
        string path = artifactDir + "/" + filename;
        File.WriteAllBytes(path, bytes);
        Debug.Log("[Verify] Screenshot saved: " + path + " (" + bytes.Length + " bytes)");
    }
}
