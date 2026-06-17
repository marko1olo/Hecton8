using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Terrain
{
    public static class AutoBuildTextureArrays
    {
        public static void Run()
        {
            Debug.Log("[AutoBuildTextureArrays] Starting headless texture packing...");

            string[] layerPaths = new string[]
            {
                "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers/L_B34_3402_ShallowSeagrassRootMat.terrainlayer", // Shell Sand
                "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers/L_B34_3401_PhoticLimestoneRubbleShelf.terrainlayer", // Limestone
                "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers/L_B34_3408_ClaySiltTurbiditySlope.terrainlayer", // Clay Silt
                "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers/L_B34_3406_SerpentiniteFaultRock.terrainlayer", // Hard Rock
                "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers/L_B34_3403_BrineCanyonSaltCrustSilt.terrainlayer", // Brine Salt
                "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers/L_B34_3404_AbyssalManganeseNodulePlain.terrainlayer", // Manganese Nodule
                "Assets/_Project/Art/TEXTURES/Terrain Textures/2rock/2Rock.terrainlayer", // Reef Rubble
                "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers/L_B34_3405_MethaneHydrateCrackVein.terrainlayer" // Seep Crust
            };

            var window = ScriptableObject.CreateInstance<HectonTerrainTextureArrayBuilder>();
            
            var type = typeof(HectonTerrainTextureArrayBuilder);
            
            List<TerrainLayer> layers = new List<TerrainLayer>();
            foreach(var path in layerPaths)
            {
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null)
                {
                    Debug.LogError($"[AutoBuildTextureArrays] Failed to load layer at path: {path}");
                }
                layers.Add(layer);
            }

            type.GetField("_layers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(window, layers);
            type.GetMethod("BuildArrays", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(window, null);

            Debug.Log("[AutoBuildTextureArrays] Finished headless texture packing.");
            
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
