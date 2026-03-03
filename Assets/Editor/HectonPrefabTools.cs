using UnityEngine;
using UnityEditor;
using System.IO;

public class HectonPrefabTools
{
    [MenuItem("Hecton/Convert Selection to Prefabs")]
    public static void CreatePrefabs()
    {
        string folderPath = "Assets/_Project/Prefabs";
        
        // Создаем папку, если её нет
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Ничего не выбрано в Hierarchy!");
            return;
        }

        foreach (GameObject obj in selectedObjects)
        {
            string localPath = folderPath + "/" + obj.name + ".prefab";
            
            // Если такой префаб уже есть — перезаписываем
            PrefabUtility.SaveAsPrefabAssetAndConnect(obj, localPath, InteractionMode.UserAction);
            Debug.Log($"[Hecton] Объект {obj.name} успешно превращен в префаб: {localPath}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}