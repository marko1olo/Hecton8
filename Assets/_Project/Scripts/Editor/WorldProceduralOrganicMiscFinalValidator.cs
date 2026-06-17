using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Validates organic misc procedural families and linked real-final prefabs.
    /// </summary>
    public static class WorldProceduralOrganicMiscFinalValidator
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";

        [MenuItem("Hecton8/Validation/Validate Procedural Organic Misc Families", priority = 241)]
        public static void ValidateOrganicMiscFamilies()
        {
            List<FamilyRecord> records = LoadOrganicMiscFamilies();
            int errorCount = 0;
            int warningCount = 0;
            int realFinalFamilyCount = 0;
            int placeholderOnlyFamilyCount = 0;

            for (int i = 0; i < records.Count; i++)
            {
                FamilyRecord record = records[i];
                WorldPrefabFamilyProfile family = record.Family;
                if (family == null)
                    continue;

                VariantMetrics metrics = MeasureVariants(family);
                if (metrics.RealFinalCount > 0)
                    realFinalFamilyCount++;
                else if (metrics.PlaceholderFinalCount > 0)
                    placeholderOnlyFamilyCount++;

                ValidateRouting(record, ref errorCount);
                ValidateFinalVariants(record, metrics, ref errorCount, ref warningCount);
            }

            if (errorCount <= 0)
            {
                Debug.Log($"[WorldProceduralOrganicMiscFinalValidator] PASS families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}, warnings={warningCount}");
                return;
            }

            Debug.LogWarning($"[WorldProceduralOrganicMiscFinalValidator] COMPLETE errors={errorCount}, warnings={warningCount}, families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}");
        }

        private static List<FamilyRecord> LoadOrganicMiscFamilies()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyRecord> records = new List<FamilyRecord>(4);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !WorldProceduralOrganicMiscContract.IsOrganicMiscFamily(family.familyId))
                    continue;

                records.Add(new FamilyRecord(assetPath, family));
            }

            return records;
        }

        private static void ValidateRouting(FamilyRecord record, ref int errorCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldStreamingLayer expectedLayer = WorldProceduralOrganicMiscContract.ResolveExpectedStreamingLayer(family);
            WorldStreamingLayer resolvedLayer = family.ResolveStreamingLayer();
            if (resolvedLayer != expectedLayer)
            {
                Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc family resolves to streaming layer '{resolvedLayer}', expected '{expectedLayer}'.");
                errorCount++;
            }

            if (!family.allowRuntimeScatter)
            {
                Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc family has runtime scatter disabled.");
                errorCount++;
            }
        }

        private static void ValidateFinalVariants(FamilyRecord record, VariantMetrics metrics, ref int errorCount, ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();

            if (metrics.RealFinalCount <= 0 && metrics.PlaceholderFinalCount > 0)
            {
                Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: only placeholder organic misc finals are linked. Organic scatter cannot ship with procedural placeholder finals.");
                errorCount++;
            }
            else if (metrics.RealFinalCount <= 0)
            {
                Debug.LogWarning($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: no organic misc finals are linked.");
                warningCount++;
            }

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null || !variant.finalReady || variant.proxyOnly || variant.prefab == null)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                {
                    Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' is final-ready but still points at procedural placeholder prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                if (WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh(variant.prefab))
                {
                    Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' uses Unity built-in primitive mesh ids in prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                Renderer[] renderers = variant.prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length <= 0)
                {
                    Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' has no renderers in prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                HashSet<Material> inspectedMaterials = new HashSet<Material>(16);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null)
                        continue;

                    Material[] sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials == null || sharedMaterials.Length <= 0)
                    {
                        Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' renderer '{renderer.name}' has no shared materials.");
                        errorCount++;
                        continue;
                    }

                    for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                    {
                        Material material = sharedMaterials[materialIndex];
                        if (material == null)
                        {
                            Debug.LogError($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' renderer '{renderer.name}' has null material slot {materialIndex}.");
                            errorCount++;
                            continue;
                        }

                        if (!inspectedMaterials.Add(material))
                            continue;

                        if (WorldProceduralOrganicMiscContract.TryGetMaterialContractFailure(material, out string failureLabel))
                        {
                            Debug.LogWarning($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' uses material '{material.name}' with invalid contract ({failureLabel}).");
                            warningCount++;
                        }
                    }
                }

                LODGroup[] lodGroups = variant.prefab.GetComponentsInChildren<LODGroup>(true);
                if (WorldProceduralOrganicMiscContract.RequiresOrganicLod(family) && (lodGroups == null || lodGroups.Length <= 0))
                {
                    Debug.LogWarning($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' has no LODGroup on prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    warningCount++;
                }
                else if (lodGroups != null)
                {
                    for (int lodGroupIndex = 0; lodGroupIndex < lodGroups.Length; lodGroupIndex++)
                    {
                        LODGroup lodGroup = lodGroups[lodGroupIndex];
                        if (lodGroup == null)
                            continue;

                        if (WorldProceduralOrganicMiscContract.TryGetLodContractFailure(lodGroup, out string failureLabel))
                        {
                            Debug.LogWarning($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' LODGroup '{lodGroup.name}' fails organic misc LOD contract ({failureLabel}).");
                            warningCount++;
                        }
                    }
                }

                if (renderers.Length > WorldProceduralOrganicMiscContract.ResolveRendererBudget(family))
                {
                    Debug.LogWarning($"[WorldProceduralOrganicMiscFinalValidator] {record.AssetPath}: organic misc variant '{variant.variantId}' exceeds soft renderer budget ({renderers.Length}>{WorldProceduralOrganicMiscContract.ResolveRendererBudget(family)}).");
                    warningCount++;
                }
            }
        }

        private static VariantMetrics MeasureVariants(WorldPrefabFamilyProfile family)
        {
            VariantMetrics metrics = new VariantMetrics();
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null || !variant.finalReady || variant.proxyOnly)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    metrics.PlaceholderFinalCount++;
                else
                    metrics.RealFinalCount++;
            }

            return metrics;
        }

        private struct FamilyRecord
        {
            public FamilyRecord(string assetPath, WorldPrefabFamilyProfile family)
            {
                AssetPath = assetPath ?? string.Empty;
                Family = family;
            }

            public string AssetPath;
            public WorldPrefabFamilyProfile Family;
        }

        private struct VariantMetrics
        {
            public int RealFinalCount;
            public int PlaceholderFinalCount;
        }
    }
}
