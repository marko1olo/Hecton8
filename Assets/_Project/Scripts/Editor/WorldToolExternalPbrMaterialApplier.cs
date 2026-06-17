using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class WorldToolExternalPbrMaterialApplier
    {
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607";
        private const string GeminiMicroPanelProvider = "Gemini_Batch20260607_MicroPanel";

        private static readonly AssignmentRule[] Rules =
        {
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_BeaconDeployer_World.prefab", "Item_Tool_BeaconDeployer_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_orange_safety_composite"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Builder_World.prefab", "Item_Tool_Builder_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_clean_graphite_panel"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_EnvAnalyzer_World.prefab", "Item_Tool_EnvAnalyzer_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_white_ceramic_casing"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab", "Item_Tool_Flashlight_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_dark_anodized_metal"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_HarpoonLauncher_World.prefab", "Item_Tool_HarpoonLauncher_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_fine_ribbed_trim"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Knife_World.prefab", "Item_Tool_Knife_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_brushed_titanium"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_LaserCutter_World.prefab", "Item_Tool_LaserCutter_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_orange_safety_composite"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Propulsion_World.prefab", "Item_Tool_Propulsion_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_matte_carbon_composite"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab", "Item_Tool_Repair_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_black_gasket_rubber"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_SalvageSampler_World.prefab", "Item_Tool_SalvageSampler_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_salt_scuffed_metal"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab", "Item_Tool_Scanner_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_white_ceramic_casing"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_SeafloorDrill_World.prefab", "Item_Tool_SeafloorDrill_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_fine_ribbed_trim"),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_StunPistol_World.prefab", "Item_Tool_StunPistol_World", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_black_grip_rubber"),
        };

        [MenuItem("Hecton8/Art/Apply External PBR To World Tool Items")]
        public static void ExecuteMenu()
        {
            ApplyWorldToolMaterials();
        }

        public static void ApplyWorldToolMaterials()
        {
            ApplyWorldToolMaterials(true);
        }

        public static void ApplyWorldToolMaterials(bool importFirst)
        {
            if (importFirst)
                ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            ValidateRules();
            int assigned = 0;
            int prefabCount = 0;

            for (int i = 0; i < Rules.Length; i++)
            {
                AssignmentRule rule = Rules[i];
                RequirePrefab(rule);

                Material material = RequireMaterial(rule);

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(rule.prefabPath);
                try
                {
                    int changed = AssignMatchingRenderers(prefabRoot, rule.rendererNameContains, material);
                    if (CountMatchingRenderers(prefabRoot, rule.rendererNameContains) == 0)
                        throw new InvalidOperationException($"[WorldToolExternalPbrMaterialApplier] No renderer matched '{rule.rendererNameContains}' in {rule.prefabPath}");

                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, rule.prefabPath);
                        prefabCount++;
                        assigned += changed;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldToolExternalPbrMaterialApplier] Assigned renderer materials={assigned}, prefabsSaved={prefabCount}");
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

        private static int CountMatchingRenderers(GameObject prefabRoot, string nameContains)
        {
            if (prefabRoot == null)
                return 0;

            int count = 0;
            Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            }

            return count;
        }

        private static void ValidateRules()
        {
            for (int i = 0; i < Rules.Length; i++)
            {
                AssignmentRule rule = Rules[i];
                RequirePrefab(rule);

                RequireMaterial(rule);

                GameObject prefabRoot = null;
                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(rule.prefabPath);
                    if (CountMatchingRenderers(prefabRoot, rule.rendererNameContains) == 0)
                        throw new InvalidOperationException($"[WorldToolExternalPbrMaterialApplier] No renderer matched '{rule.rendererNameContains}' in {rule.prefabPath}");
                }
                finally
                {
                    if (prefabRoot != null)
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static GameObject RequirePrefab(AssignmentRule rule)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rule.prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"[WorldToolExternalPbrMaterialApplier] Missing prefab: {rule.prefabPath}");

            return prefab;
        }

        private static Material RequireMaterial(AssignmentRule rule)
        {
            Material material = LoadMaterial(rule.providerName, rule.materialId);
            if (material == null)
                throw new InvalidOperationException($"[WorldToolExternalPbrMaterialApplier] Missing material provider={rule.providerName} id={rule.materialId}");

            return material;
        }

        private static Material LoadMaterial(string providerName, string id)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{providerName}/MAT_EXT_{providerName}_{id}.mat");
        }

        private readonly struct AssignmentRule
        {
            public readonly string prefabPath;
            public readonly string rendererNameContains;
            public readonly string providerName;
            public readonly string materialId;

            public AssignmentRule(string prefabPath, string rendererNameContains, string providerName, string materialId)
            {
                this.prefabPath = prefabPath;
                this.rendererNameContains = rendererNameContains;
                this.providerName = providerName;
                this.materialId = materialId;
            }
        }
    }
}
