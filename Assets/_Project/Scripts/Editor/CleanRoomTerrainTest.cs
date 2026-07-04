using System.IO;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Hecton8.World;

public static class CleanRoomTerrainTest
{
    private const string ArtifactDir = "C:/Users/Admin/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/";

    [MenuItem("Hecton8/Clean Room Terrain Test")]
    public static void RunTest()
    {
        Debug.Log("[CleanRoom] Starting Clean Room Terrain Test...");

        // 1. Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Debug.Log("[CleanRoom] Created empty scene.");

        // 2. Setup lighting and PBR
        // Ambient Flat Color
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.15f, 0.2f);
        RenderSettings.fog = false;

        // Directional Light
        GameObject lightGo = new GameObject("TRT_Sun");
        Light sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = Color.white;
        sun.intensity = 1.8f;
        sun.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(40f, 65f, 0f);
        Debug.Log("[CleanRoom] Configured Directional Light and Ambient.");

        // Global Volume with ACES
        GameObject volumeGo = new GameObject("TRT_GlobalVolume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100;
        
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;
        
        var tonemapping = profile.Add<Tonemapping>();
        tonemapping.mode.Override(TonemappingMode.ACES);

        var exposure = profile.Add<ColorAdjustments>();
        exposure.postExposure.Override(3.2f); // Lift brightness for dark deep-sea albedos
        exposure.contrast.Override(-10f);     // Soften contrast for readability
        Debug.Log("[CleanRoom] Configured Global Volume with ACES.");

        // Load Shader and Textures
        Shader shader = Shader.Find("Hecton8/URP/Terrain_TextureArray");
        if (shader == null)
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/HectonTerrain.shader");
        }
        if (shader == null)
        {
            Debug.LogError("[CleanRoom] Failed to find HectonTerrain shader!");
            return;
        }

        Material baseMat = new Material(shader);
        baseMat.name = "CleanRoomTerrainMaterial";

        Texture2DArray albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/DeepSea_AlbedoArray.asset");
        Texture2DArray normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/DeepSea_NormalArray.asset");
        Texture2DArray mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");

        if (albedo == null) Debug.LogWarning("[CleanRoom] DeepSea_AlbedoArray.asset not found!");
        if (normal == null) Debug.LogWarning("[CleanRoom] DeepSea_NormalArray.asset not found!");
        if (mask == null)   Debug.LogWarning("[CleanRoom] Terrain_MaskArray.asset not found!");

        baseMat.SetTexture("_AlbedoArray", albedo);
        baseMat.SetTexture("_NormalArray", normal);
        baseMat.SetTexture("_MaskArray", mask);
        baseMat.SetFloat("_HectonUVScale", 400f);
        baseMat.SetFloat("_HectonTriplanarBlend", 8f);
        baseMat.EnableKeyword("_NORMALMAP");
        baseMat.EnableKeyword("_MASKMAP");
        baseMat.EnableKeyword("_TERRAIN_BLEND_HEIGHT");

        // Load Terrain Layers
        TerrainLayer[] layers = new TerrainLayer[3];
        layers[0] = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Project/Art/TEXTURES/Terrain Textures/sand/L_Sand.terrainlayer");
        layers[1] = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Project/Art/TEXTURES/Terrain Textures/gravel/L_Gravel.terrainlayer");
        layers[2] = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Project/Art/TEXTURES/Terrain Textures/rocks/L_Rocks.terrainlayer");

        if (layers[0] == null) Debug.LogWarning("[CleanRoom] Sand terrain layer not found!");
        if (layers[2] == null) Debug.LogWarning("[CleanRoom] Rock terrain layer not found!");

        // 3. Generate 3x3 Terrains
        int chunkRes = 513;
        int alphaRes = 512;
        float chunkSize = 1000f;
        float heightRange = 4000f;

        WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(880031);
        p.WaterSurfaceY = 0f;

        Terrain[,] terrains = new Terrain[3, 3];

        for (int row = -1; row <= 1; row++)
        {
            for (int col = -1; col <= 1; col++)
            {
                float posX = col * chunkSize;
                float posZ = row * chunkSize;

                GameObject terrainGo = new GameObject($"CleanRoom_Terrain_{row + 1}_{col + 1}");
                terrainGo.transform.position = new Vector3(posX, -4000f, posZ);

                Terrain terrain = terrainGo.AddComponent<Terrain>();
                TerrainCollider collider = terrainGo.AddComponent<TerrainCollider>();

                TerrainData td = new TerrainData();
                td.heightmapResolution = chunkRes;
                td.alphamapResolution = alphaRes;
                td.size = new Vector3(chunkSize, heightRange, chunkSize);
                td.terrainLayers = layers;

                terrain.terrainData = td;
                collider.terrainData = td;
                terrain.basemapDistance = 100000.0f; // Prevent fallback to URP Lit Basemap

                terrains[row + 1, col + 1] = terrain;

                Debug.Log($"[CleanRoom] Generating terrain row={row}, col={col}...");

                // Populate Heights
                float[,] heights = new float[chunkRes, chunkRes];
                Parallel.For(0, chunkRes, z =>
                {
                    float localZ = (float)z / (chunkRes - 1) * chunkSize;
                    float worldZ = posZ + localZ;
                    for (int x = 0; x < chunkRes; x++)
                    {
                        float localX = (float)x / (chunkRes - 1) * chunkSize;
                        float worldX = posX + localX;

                        float h = WorldMacroGeologyFields.EvaluateHeightMeters(worldX, worldZ, in p);
                        float h_norm = (h + 4000f) / 4000f;
                        heights[z, x] = math.clamp(h_norm, 0f, 1f);
                    }
                });
                td.SetHeights(0, 0, heights);

                // Populate Alphamaps (Splatmaps)
                float[,,] alphamaps = new float[alphaRes, alphaRes, 3];
                Parallel.For(0, alphaRes, z =>
                {
                    float localZ = (float)z / (alphaRes - 1) * chunkSize;
                    float worldZ = posZ + localZ;
                    for (int x = 0; x < alphaRes; x++)
                    {
                        float localX = (float)x / (alphaRes - 1) * chunkSize;
                        float worldX = posX + localX;

                        float h = WorldMacroGeologyFields.EvaluateHeightMeters(worldX, worldZ, in p);
                        float hRight = WorldMacroGeologyFields.EvaluateHeightMeters(worldX + 1f, worldZ, in p);
                        float hUp = WorldMacroGeologyFields.EvaluateHeightMeters(worldX, worldZ + 1f, in p);

                        float dx = hRight - h;
                        float dz = hUp - h;
                        float slopeDegrees = math.atan(math.sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;

                        float rockWeight = math.smoothstep(15f, 25f, slopeDegrees);
                        float sandWeight = 1f - rockWeight;

                        alphamaps[z, x, 0] = sandWeight;
                        alphamaps[z, x, 1] = 0f;
                        alphamaps[z, x, 2] = rockWeight;
                    }
                });
                td.SetAlphamaps(0, 0, alphamaps);

                // Setup Material Instance
                Material chunkMat = new Material(baseMat);
                chunkMat.name = baseMat.name + "_" + terrainGo.name;

                Texture2D[] alphamapsTex = td.alphamapTextures;
                if (alphamapsTex.Length > 0 && alphamapsTex[0] != null) chunkMat.SetTexture("_Control", alphamapsTex[0]);
                if (alphamapsTex.Length > 1 && alphamapsTex[1] != null) chunkMat.SetTexture("_Control1", alphamapsTex[1]);
                if (alphamapsTex.Length > 2 && alphamapsTex[2] != null) chunkMat.SetTexture("_Control2", alphamapsTex[2]);

                chunkMat.SetFloat("_NumLayersCount", td.alphamapLayers);
                chunkMat.SetVector("_TerrainSize", new Vector4(td.size.x, td.size.y, td.size.z, 0));

                terrain.materialTemplate = chunkMat;
                terrain.Flush();
            }
        }

        // Stitch Terrains
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                Terrain left = (c > 0) ? terrains[r, c - 1] : null;
                Terrain right = (c < 2) ? terrains[r, c + 1] : null;
                Terrain bottom = (r > 0) ? terrains[r - 1, c] : null;
                Terrain top = (r < 2) ? terrains[r + 1, c] : null;
                terrains[r, c].SetNeighbors(left, top, right, bottom);
            }
        }
        Debug.Log("[CleanRoom] Terrains generated and stitched.");

        // 4. Camera and rendering
        GameObject camGo = new GameObject("TRT_Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(150f, 320f, -420f);
        cam.transform.LookAt(new Vector3(0f, -1500f, 0f));
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 10000f;

        var urpCamData = camGo.AddComponent<UniversalAdditionalCameraData>();
        urpCamData.renderPostProcessing = true;

        // Clear selection to avoid wireframe outlines
        Selection.activeGameObject = null;
        Selection.objects = new UnityEngine.Object[0];

        // Direct screenshot rendering
        if (!Directory.Exists(ArtifactDir))
        {
            Directory.CreateDirectory(ArtifactDir);
        }

        string filename = Path.Combine(ArtifactDir, "CleanRoom_Beauty.png");

        RenderTexture rt = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = rt;
        Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        
        cam.Render();
        
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        
        File.WriteAllBytes(filename, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        
        Debug.Log($"[CleanRoom] Screenshot saved to: {filename}");

        // Cleanup temporary profile
        Object.DestroyImmediate(profile);

        // 5. Refresh AssetDatabase
        AssetDatabase.Refresh();
        Debug.Log("[CleanRoom] Done.");
    }
}
