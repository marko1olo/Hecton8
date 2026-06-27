using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class HectonBinaryToYaml
{
    public static void Convert()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;
        AssetDatabase.SaveAssets();

        string[] paths = new string[]
        {
            "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity"
        };
        
        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
        Debug.Log("[YamlConverter2] ForceReserializeAssets completed.");
        EditorApplication.Exit(0);
    }
}
