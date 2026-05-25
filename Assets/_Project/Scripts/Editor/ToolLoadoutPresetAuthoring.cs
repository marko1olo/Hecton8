#if UNITY_EDITOR
using Hecton8.Tools;
using UnityEditor;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.EditorTools
{
    internal static class ToolLoadoutPresetAuthoring
    {
        private const string Root = "Assets/_Project/Data/Tools/Presets";

        [MenuItem("Hecton/Authoring/Rebuild Tool Loadout Presets")]
        private static void Rebuild()
        {
            EnsureFolder("Assets/_Project/Data", "Tools");
            EnsureFolder("Assets/_Project/Data/Tools", "Presets");

            CreateOrUpdatePreset(
                "Preset_Loadout_Exploration",
                "EXPLORATION",
                "Balanced deep-run loadout: scanner, flashlight, propulsion, and cutter.",
                new[]
                {
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab"
                });

            CreateOrUpdatePreset(
                "Preset_Loadout_Construction",
                "CONSTRUCTION",
                "Base work kit: builder, repair, scanner, and cutter.",
                new[]
                {
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab"
                });

            CreateOrUpdatePreset(
                "Preset_Loadout_FieldRecovery",
                "FIELD RECOVERY",
                "Salvage-focused kit: sampler, cutter, propulsion, and analyzer.",
                new[]
                {
                    "Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_EnvAnalyzer_Held.prefab"
                });

            CreateOrUpdatePreset(
                "Preset_Loadout_Defense",
                "DEFENSE",
                "Hostile-zone kit: stun, harpoon, knife, and flashlight.",
                new[]
                {
                    "Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab",
                    "Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab"
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            H8Debug.Log("[ToolLoadoutPresetAuthoring] Rebuilt starter tool loadout presets.");
        }

        private static void CreateOrUpdatePreset(string assetName, string presetName, string description, string[] prefabPaths)
        {
            string assetPath = $"{Root}/{assetName}.asset";
            ToolLoadoutPreset preset = AssetDatabase.LoadAssetAtPath<ToolLoadoutPreset>(assetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<ToolLoadoutPreset>();
                AssetDatabase.CreateAsset(preset, assetPath);
            }

            preset.presetName = presetName;
            preset.description = description;
            if (preset.slotPrefabs == null || preset.slotPrefabs.Length != 4)
                preset.slotPrefabs = new GameObject[4];

            for (int i = 0; i < preset.slotPrefabs.Length && i < prefabPaths.Length; i++)
                preset.slotPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);

            EditorUtility.SetDirty(preset);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string combined = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(combined))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
