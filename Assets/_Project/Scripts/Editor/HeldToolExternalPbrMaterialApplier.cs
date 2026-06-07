using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class HeldToolExternalPbrMaterialApplier
    {
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607/PolyHaven";

        private static readonly AssignmentRule[] Rules =
        {
            new("Assets/_Project/Prefabs/Tools/Held/Tool_BeaconDeployer_Held.prefab", "VisualBody", "rubber_tiles"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab", "VisualBody", "blue_metal_plate"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_EnvAnalyzer_Held.prefab", "VisualBody", "green_metal_rust"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab", "Visual", "painted_metal_shutter"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab", "VisualBody", "box_profile_metal_sheet"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab", "VisualBody", "metal_plate"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab", "VisualBody", "painted_metal_shutter"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab", "VisualBody", "box_profile_metal_sheet"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab", "VisualBody", "factory_wall"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab", "VisualBody", "metal_plate_02"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab", "VisualBody", "blue_metal_plate"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab", "VisualBody", "corrugated_iron_03"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab", "DrillBit", "metal_plate"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab", "VisualBody", "blue_metal_plate"),
        };

        [MenuItem("Hecton8/Art/Apply External PBR To Held Tools")]
        public static void ExecuteMenu()
        {
            ApplyExternalPbrToHeldTools();
        }

        public static void ApplyExternalPbrToHeldTools()
        {
            ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            int assigned = 0;
            int prefabCount = 0;
            string currentPrefab = string.Empty;
            GameObject prefabRoot = null;
            bool currentDirty = false;

            try
            {
                for (int i = 0; i < Rules.Length; i++)
                {
                    AssignmentRule rule = Rules[i];
                    if (!File.Exists(rule.prefabPath))
                    {
                        Debug.LogWarning($"[HeldToolExternalPbrMaterialApplier] Missing prefab: {rule.prefabPath}");
                        continue;
                    }

                    if (!string.Equals(currentPrefab, rule.prefabPath, StringComparison.Ordinal))
                    {
                        SaveAndUnloadCurrent(ref prefabRoot, currentPrefab, currentDirty, ref prefabCount);
                        currentPrefab = rule.prefabPath;
                        prefabRoot = PrefabUtility.LoadPrefabContents(rule.prefabPath);
                        currentDirty = false;
                    }

                    Material material = LoadMaterial(rule.materialId);
                    if (material == null)
                    {
                        Debug.LogWarning($"[HeldToolExternalPbrMaterialApplier] Missing material for id={rule.materialId}");
                        continue;
                    }

                    int changed = AssignMatchingRenderers(prefabRoot, rule.rendererNameContains, material);
                    if (changed == 0)
                        Debug.LogWarning($"[HeldToolExternalPbrMaterialApplier] No renderer matched '{rule.rendererNameContains}' in {rule.prefabPath}");
                    else
                        currentDirty = true;

                    assigned += changed;
                }

                SaveAndUnloadCurrent(ref prefabRoot, currentPrefab, currentDirty, ref prefabCount);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HeldToolExternalPbrMaterialApplier] Assigned renderer materials={assigned}, prefabsSaved={prefabCount}");
        }

        private static int AssignMatchingRenderers(GameObject prefabRoot, string nameContains, Material material)
        {
            if (prefabRoot == null || material == null)
                return 0;

            int assigned = 0;
            Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    materials = new Material[1];

                bool changed = false;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == material)
                        continue;

                    materials[m] = material;
                    changed = true;
                }

                if (!changed)
                    continue;

                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                assigned++;
            }

            return assigned;
        }

        private static Material LoadMaterial(string id)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/MAT_EXT_PolyHaven_{id}.mat");
        }

        private static void SaveAndUnloadCurrent(
            ref GameObject prefabRoot,
            string prefabPath,
            bool dirty,
            ref int prefabCount)
        {
            if (prefabRoot == null)
                return;

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                prefabCount++;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
            prefabRoot = null;
        }

        private readonly struct AssignmentRule
        {
            public readonly string prefabPath;
            public readonly string rendererNameContains;
            public readonly string materialId;

            public AssignmentRule(string prefabPath, string rendererNameContains, string materialId)
            {
                this.prefabPath = prefabPath;
                this.rendererNameContains = rendererNameContains;
                this.materialId = materialId;
            }
        }
    }
}
