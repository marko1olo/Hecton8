using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace Hecton8.Editor {
    public static class TextureBakerRun {
        public static void Execute() {
            var w = ScriptableObject.CreateInstance<Hecton8.Editor.Terrain.HectonTerrainTextureArrayBuilder>();
            
            List<TerrainLayer> layers = new List<TerrainLayer>();
            string root = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers";
            string[] canonicalPaths = new string[] {
                root + "/L_B34_3408_ClaySiltTurbiditySlope.terrainlayer",
                root + "/L_B34_3401_PhoticLimestoneRubbleShelf.terrainlayer",
                root + "/L_B34_3402_ShallowSeagrassRootMat.terrainlayer",
                root + "/L_B34_3406_SerpentiniteFaultRock.terrainlayer",
                root + "/L_B34_3403_BrineCanyonSaltCrustSilt.terrainlayer",
                root + "/L_B34_3404_AbyssalManganeseNodulePlain.terrainlayer",
                root + "/L_B34_3405_MethaneHydrateCrackVein.terrainlayer",
                root + "/L_B34_3409_LimestoneCaveCeilingMineralDrip.terrainlayer"
            };
            foreach(var path in canonicalPaths) {
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (tl != null) { layers.Add(tl); Debug.Log("Loaded " + path); }
                else { Debug.LogError("Missing " + path); }
            }
            
            typeof(Hecton8.Editor.Terrain.HectonTerrainTextureArrayBuilder).GetField("_layers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(w, layers);
            typeof(Hecton8.Editor.Terrain.HectonTerrainTextureArrayBuilder).GetField("_resolution", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(w, 1024);
            typeof(Hecton8.Editor.Terrain.HectonTerrainTextureArrayBuilder).GetField("_exportPath", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(w, "Assets/_Project/Art/Textures/TerrainArrays");
            
            var buildMethod = typeof(Hecton8.Editor.Terrain.HectonTerrainTextureArrayBuilder).GetMethod("BuildArrays", BindingFlags.NonPublic | BindingFlags.Instance);
            buildMethod.Invoke(w, null);
            
            Debug.Log("Finished baking arrays!");
            
            // NOW ASSIGN THEM TO THE MATERIAL
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
            if (mat != null) {
                var a = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_Project/Art/Textures/TerrainArrays/TerrainAlbedoArray.asset");
                var n = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_Project/Art/Textures/TerrainArrays/TerrainNormalArray.asset");
                var m = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_Project/Art/Textures/TerrainArrays/TerrainMaskArray.asset");
                if (a != null) { mat.SetTexture("_TerrainBaseMapArray", a); Debug.Log("Set _TerrainBaseMapArray"); }
                if (n != null) { mat.SetTexture("_TerrainNormalMapArray", n); Debug.Log("Set _TerrainNormalMapArray"); }
                if (m != null) { mat.SetTexture("_TerrainMaskMapArray", m); Debug.Log("Set _TerrainMaskMapArray"); }
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
            }
            else {
                 Debug.LogError("Material not found at Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
            }
        }
    }
}
