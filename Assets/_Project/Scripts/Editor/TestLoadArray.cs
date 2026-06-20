using UnityEngine;
using UnityEditor;

public class TestLoadArray
{
    public static void Run()
    {
        var albedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
        Debug.Log("[TestLoadArray] albedoArray loaded: " + (albedoArray != null));
        if (albedoArray == null) {
            var obj = AssetDatabase.LoadAssetAtPath<Object>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
            Debug.Log("[TestLoadArray] As generic Object: " + (obj != null));
            if (obj != null) {
                Debug.Log("[TestLoadArray] Object type: " + obj.GetType().Name);
            }
        }
        EditorApplication.Exit(0);
    }
}
