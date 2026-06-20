using UnityEngine;
using UnityEditor;

public class TestMaterialBind
{
    public static void Run()
    {
        var albedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
        Shader shader = Shader.Find("Hecton8/URP/Terrain_TextureArray");
        Material mat = new Material(shader);
        
        Debug.Log("[TestBind] albedoArray loaded: " + (albedoArray != null));
        
        if (albedoArray != null) {
            mat.SetTexture("_AlbedoArray", albedoArray);
            Debug.Log("[TestBind] After SetTexture, _AlbedoArray bound: " + (mat.GetTexture("_AlbedoArray") != null));
            Debug.Log("[TestBind] HasProperty: " + mat.HasProperty("_AlbedoArray"));
        }
        EditorApplication.Exit(0);
    }
}
