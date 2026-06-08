using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class HeldToolExternalPbrMaterialApplier
    {
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607";
        private const string GeminiMicroPanelProvider = "Gemini_Batch20260607_MicroPanel";

        private static readonly AssignmentRule[] Rules =
        {
            new("Assets/_Project/Prefabs/Tools/Held/Tool_BeaconDeployer_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_orange_safety_composite"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_clean_graphite_panel"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_EnvAnalyzer_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_white_ceramic_casing"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab", "Visual", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_dark_anodized_metal"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_fine_ribbed_trim"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_brushed_titanium"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_orange_safety_composite"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_matte_carbon_composite"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_black_gasket_rubber"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_salt_scuffed_metal"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_white_ceramic_casing"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_fine_ribbed_trim"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab", "DrillBit", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_worn_steel_inset"),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab", "VisualBody", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_black_grip_rubber"),
        };

        [MenuItem("Hecton8/Art/Apply External PBR To Held Tools")]
        public static void ExecuteMenu()
        {
            ApplyExternalPbrToHeldTools();
        }

        public static void ApplyExternalPbrToHeldTools()
        {
            ApplyExternalPbrToHeldTools(true);
        }

        public static void ApplyExternalPbrToHeldTools(bool importFirst)
        {
            if (importFirst)
                ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            ValidateRules();
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
                    RequirePrefab(rule);

                    if (!string.Equals(currentPrefab, rule.prefabPath, StringComparison.Ordinal))
                    {
                        SaveAndUnloadCurrent(ref prefabRoot, currentPrefab, currentDirty, ref prefabCount);
                        currentPrefab = rule.prefabPath;
                        prefabRoot = PrefabUtility.LoadPrefabContents(rule.prefabPath);
                        currentDirty = false;
                    }

                    Material material = RequireMaterial(rule);

                    int changed = AssignMatchingRenderers(prefabRoot, rule.rendererNameContains, material);
                    if (changed == 0)
                        throw new InvalidOperationException($"[HeldToolExternalPbrMaterialApplier] No renderer matched '{rule.rendererNameContains}' in {rule.prefabPath}");

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
                        throw new InvalidOperationException($"[HeldToolExternalPbrMaterialApplier] No renderer matched '{rule.rendererNameContains}' in {rule.prefabPath}");
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
                throw new InvalidOperationException($"[HeldToolExternalPbrMaterialApplier] Missing prefab: {rule.prefabPath}");

            return prefab;
        }

        private static Material RequireMaterial(AssignmentRule rule)
        {
            Material material = LoadMaterial(rule.providerName, rule.materialId);
            if (material == null)
                throw new InvalidOperationException($"[HeldToolExternalPbrMaterialApplier] Missing material provider={rule.providerName} id={rule.materialId}");

            return material;
        }

        private static Material LoadMaterial(string providerName, string id)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{providerName}/MAT_EXT_{providerName}_{id}.mat");
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
