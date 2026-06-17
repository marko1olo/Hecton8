using UnityEngine;
using UnityEditor;
using Hecton8.Editor.Terrain;

public class RebuildTextureArrays
{
    public static void Run()
    {
        var builder = ScriptableObject.CreateInstance<HectonTerrainTextureArrayBuilder>();
        
        string[] guids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers" });
        var layers = typeof(HectonTerrainTextureArrayBuilder).GetField("_layers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var layerList = new System.Collections.Generic.List<TerrainLayer>();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            layerList.Add(AssetDatabase.LoadAssetAtPath<TerrainLayer>(path));
        }
        
        layers.SetValue(builder, layerList);
        
        var buildMethod = typeof(HectonTerrainTextureArrayBuilder).GetMethod("BuildArrays", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        buildMethod.Invoke(builder, null);
        
        Debug.Log("Arrays built from CLI.");
        EditorApplication.Exit(0);
    }
}

