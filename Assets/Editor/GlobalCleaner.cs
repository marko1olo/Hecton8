using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class GlobalCleaner : EditorWindow
{
    [MenuItem("VibeCoder/🧹 ГЕНЕРАЛЬНАЯ УБОРКА (Все файлы)")]
    public static void CleanAll()
    {
        // Настраиваем, куда что летит
        // Ключ - папка, Значение - список расширений
        var rules = new Dictionary<string, List<string>>
        {
            { "_Project/Art/Sprites", new List<string> { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".bmp" } },
            { "_Project/Art/Materials", new List<string> { ".mat", ".physicsMaterial2D", ".physicMaterial" } },
            { "_Project/Art/Models", new List<string> { ".fbx", ".obj", ".blend", ".dae" } },
            { "_Project/Art/Animations", new List<string> { ".anim", ".controller" } },
            { "_Project/Audio", new List<string> { ".mp3", ".wav", ".ogg", ".aiff" } },
            { "_Project/Prefabs", new List<string> { ".prefab" } },
            { "_Project/Scripts", new List<string> { ".cs", ".shader", ".cginc" } },
            { "_Project/Scenes", new List<string> { ".unity" } },
            { "_Project/Data", new List<string> { ".json", ".xml", ".txt", ".asset" } } // ScriptableObjects часто .asset
        };

        int moveCount = 0;
        string[] allFiles = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            string path = file.Replace("\\", "/");

            // Игнорим системные папки, плагины и то, что уже лежит правильно
            if (path.Contains("/Editor/") || 
                path.Contains("/Plugins/") || 
                path.Contains("/Packages/") || 
                path.Contains("/_Project/") || 
                path.EndsWith(".meta")) 
                continue;

            string ext = Path.GetExtension(path).ToLower();
            string fileName = Path.GetFileName(path);

            foreach (var rule in rules)
            {
                if (rule.Value.Contains(ext))
                {
                    string targetDir = Path.Combine("Assets", rule.Key);
                    
                    // Создаем папку, если нет
                    if (!Directory.Exists(targetDir)) 
                    {
                        Directory.CreateDirectory(targetDir);
                        AssetDatabase.Refresh();
                    }

                    string newPath = Path.Combine(targetDir, fileName).Replace("\\", "/");
                    
                    // Самое важное: двигаем через AssetDatabase, чтобы не сломать ссылки в игре!
                    string error = AssetDatabase.MoveAsset(path, newPath);
                    
                    if (string.IsNullOrEmpty(error))
                    {
                        moveCount++;
                    }
                    else
                    {
                        Debug.LogError($"Не смог переместить {fileName}: {error}");
                    }
                    break; // Файл нашли, переходим к следующему
                }
            }
        }

        AssetDatabase.Refresh(); // Обновляем Unity, чтобы она увидела изменения
        Debug.Log($"[VibeCoder] Уборка завершена! Перемещено файлов: {moveCount}. Структура создана в Assets/_Project");
    }
}