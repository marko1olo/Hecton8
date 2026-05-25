using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Editor
{
    public static class BiomeRegistryEditor
    {
        [MenuItem("Hecton8/Maintenance/Rebuild Biome Registry from Vision Doc")]
        public static void RebuildRegistry()
        {
            string docPath = Path.Combine(Application.dataPath, "../TERRAIN_108_BIOMES_VISION.md");
            if (!File.Exists(docPath))
            {
                Debug.LogError($"[BiomeRegistryEditor] Vision doc not found at: {docPath}");
                return;
            }

            string assetPath = "Assets/_Project/Data/HectonBiomeRegistry.asset";
            HectonBiomeRegistry registry = AssetDatabase.LoadAssetAtPath<HectonBiomeRegistry>(assetPath);

            if (registry == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Data");
                }
                registry = ScriptableObject.CreateInstance<HectonBiomeRegistry>();
                AssetDatabase.CreateAsset(registry, assetPath);
            }

            string content = File.ReadAllText(docPath);
            
            // Regex to find biome entries: "1. **NORTH (Name)**: Description"
            // Pattern: (\d+)\.\s+\*\*([A-Z]+)\s+\(([^)]+)\)\*\*:\s*(.*)
            Regex entryRegex = new Regex(@"(\d+)\.\s+\*\*([A-Z]+)\s+\(([^)]+)\)\*\*:\s*(.*)", RegexOptions.Multiline);
            MatchCollection matches = entryRegex.Matches(content);

            Hecton8.Core.H8Debug.Log($"[BiomeRegistryEditor] Found {matches.Count} potential biome entries in vision doc.");

            Undo.RecordObject(registry, "Rebuild Biome Registry");

            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
            {
                Match match = matches[matchIndex];
                int id = int.Parse(match.Groups[1].Value);
                string region = match.Groups[2].Value;
                string name = match.Groups[3].Value;
                string desc = match.Groups[4].Value.Trim();

                // Tier calculation logic based on the doc structure
                // Tier 1: 1-4, Tier 2: 5-8, Tier 3: 9-12, Tier 4: 13-16, Tier 5: 17-20...
                // Tier = ceil(ID / 4)
                int tier = Mathf.CeilToInt(id / 4.0f);

                registry.BatchUpdate(id - 1, name, region, tier, desc);
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            
            Hecton8.Core.H8Debug.Log($"<color=green>[BiomeRegistryEditor] Successfully updated {matches.Count} biomes in {assetPath}</color>");
        }
    }
}
