using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class GlobalCleaner : EditorWindow
{
    [MenuItem("VibeCoder/🧹 GENERALNAYa UBORKA (Vse fayly)")]
    public static void CleanAll()
    {
        // Nastraivaem, kuda chto letit
        // Klyuch - papka, Znachenie - spisok rasshireniy
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
            { "_Project/Data", new List<string> { ".json", ".xml", ".txt", ".asset" } } // ScriptableObjects chasto .asset
        };

        int moveCount = 0;
        foreach (var file in Directory.EnumerateFiles("Assets", "*.*", SearchOption.AllDirectories))
        {
            string path = file.Replace("\\", "/");

            // Ignorim sistemnye papki, plaginy i to, chto uzhe lezhit pravilno
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
                    
                    // Sozdaem papku, esli net
                    if (!Directory.Exists(targetDir)) 
                    {
                        Directory.CreateDirectory(targetDir);
                        AssetDatabase.Refresh();
                    }

                    string newPath = Path.Combine(targetDir, fileName).Replace("\\", "/");
                    
                    // Samoe vazhnoe: dvigaem cherez AssetDatabase, chtoby ne slomat ssylki v igre!
                    string error = AssetDatabase.MoveAsset(path, newPath);
                    
                    if (string.IsNullOrEmpty(error))
                    {
                        moveCount++;
                    }
                    else
                    {
                        Debug.LogError($"Ne smog peremestit {fileName}: {error}");
                    }
                    break; // Fayl nashli, perehodim k sleduyuschemu
                }
            }
        }

        AssetDatabase.Refresh(); // Obnovlyaem Unity, chtoby ona uvidela izmeneniya
        Debug.Log($"[VibeCoder] Uborka zavershena! Peremescheno faylov: {moveCount}. Struktura sozdana v Assets/_Project");
    }
}
