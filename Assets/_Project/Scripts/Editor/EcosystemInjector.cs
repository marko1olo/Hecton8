using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Hecton8.World;
using GPUInstancer;

public static class EcosystemInjector
{
    public static void Inject()
    {
        Debug.Log("[EcosystemInjector] Starting Phase 1 & 2 Setup...");

        // 1. Enable GPU Instancing on all GEN_* flora materials
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art/Generated/Flora" });
        int matCount = 0;
        foreach (var guid in matGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !mat.enableInstancing)
            {
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                matCount++;
            }
        }
        Debug.Log($"[EcosystemInjector] Enabled GPU Instancing on {matCount} materials.");

        // 2. Add GPUInstancerPrefab to generated prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Nature/Flora/BioForge" });
        int prefabCount = 0;
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<GPUInstancerPrefab>() == null)
            {
                using (var editingScope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var contents = editingScope.prefabContentsRoot;
                    if (contents.GetComponent<GPUInstancerPrefab>() == null)
                    {
                        contents.AddComponent<GPUInstancerPrefab>();
                        prefabCount++;
                    }
                }
            }
        }
        Debug.Log($"[EcosystemInjector] Added GPUInstancerPrefab to {prefabCount} prefabs.");

        // 3. Inject into Families
        // Shallows -> Kelp
        InjectFamily("ProceduralFamily_family_kelp_patch_dense", "GEN_Shallows_Kelp");
        InjectFamily("ProceduralFamily_family_kelp_tall", "GEN_Shallows_Kelp");
        InjectFamily("ProceduralFamily_family_kelp_canopy", "GEN_Shallows_Kelp");

        // Cliffs -> Corals
        InjectFamily("ProceduralFamily_family_coral_branching", "GEN_Shallows_TubeCoral");
        InjectFamily("ProceduralFamily_family_coral_massive", "GEN_Shallows_TubeCoral");
        InjectFamily("ProceduralFamily_family_coral_low", "GEN_Shallows_TubeCoral");

        // Abyss -> Vents / Nodules
        InjectFamily("ProceduralFamily_family_pocket_hazard", "PFB_Ore_MagmaVent");
        InjectFamily("ProceduralFamily_family_pocket_resource", "PFB_Ore_MagmaVent");

        // Save
        AssetDatabase.SaveAssets();
        Debug.Log("[EcosystemInjector] Injection Complete!");
    }

    private static void InjectFamily(string familyName, string searchFilter)
    {
        string[] familyGuids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile " + familyName);
        if (familyGuids.Length == 0)
        {
            Debug.LogWarning($"[EcosystemInjector] Could not find {familyName}");
            return;
        }
        var family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(AssetDatabase.GUIDToAssetPath(familyGuids[0]));

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab " + searchFilter);
        List<WorldPrefabFamilyProfile.VariantEntry> variants = new List<WorldPrefabFamilyProfile.VariantEntry>();

        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                variants.Add(new WorldPrefabFamilyProfile.VariantEntry
                {
                    variantId = prefab.name,
                    prefab = prefab,
                    weight = 1,
                    proxyOnly = false,
                    finalReady = true,
                    uniformScaleRange = new Vector2(0.8f, 1.2f)
                });
            }
        }
        if (variants.Count > 0)
        {
            family.variants = variants.ToArray();
            EditorUtility.SetDirty(family);
            Debug.Log($"[EcosystemInjector] Injected {variants.Count} {searchFilter} prefabs into {familyName}.");
        }
    }
}
