using UnityEngine;
using UnityEditor;
using System.IO;

public class HectonPrefabTools
{
    [MenuItem("Hecton/Convert Selection to Prefabs")]
    public static void CreatePrefabs()
    {
        string folderPath = "Assets/_Project/Prefabs";
        
        // Sozdaem papku, esli ee net
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Nichego ne vybrano v Hierarchy!");
            return;
        }

        foreach (GameObject obj in selectedObjects)
        {
            string localPath = folderPath + "/" + obj.name + ".prefab";
            
            // Esli takoy prefab uzhe est — perezapisyvaem
            PrefabUtility.SaveAsPrefabAssetAndConnect(obj, localPath, InteractionMode.UserAction);
            Debug.Log($"[Hecton] Obekt {obj.name} uspeshno prevraschen v prefab: {localPath}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}