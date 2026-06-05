using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Validates structural procedural families and linked structural finals.
    /// </summary>
    public static class WorldProceduralStructuralFinalValidator
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";

        /// <summary>
        /// Validates structural procedural family contracts.
        /// </summary>
        [MenuItem("Hecton/Validation/Validate Procedural Structural Families", priority = 247)]
        public static void ValidateStructuralFamilies()
        {
            List<FamilyRecord> records = LoadStructuralFamilies();
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
                Debug.Log($"[WorldProceduralStructuralFinalValidator] PASS families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}, warnings={warningCount}");
                return;
            }

            Debug.LogWarning($"[WorldProceduralStructuralFinalValidator] COMPLETE errors={errorCount}, warnings={warningCount}, families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}");
        }

        private static List<FamilyRecord> LoadStructuralFamilies()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyRecord> records = new List<FamilyRecord>(12);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !IsStructuralDomain(family.proceduralDomain))
                    continue;

                records.Add(new FamilyRecord(assetPath, family));
            }

            return records;
        }

        private static void ValidateRouting(FamilyRecord record, ref int errorCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldStreamingLayer expectedLayer = ResolveExpectedStreamingLayer(family.proceduralDomain);
            WorldStreamingLayer resolvedLayer = family.ResolveStreamingLayer();
            if (resolvedLayer != expectedLayer)
            {
                Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural family resolves to streaming layer '{resolvedLayer}', expected '{expectedLayer}'.");
                errorCount++;
            }

            if (!family.allowRuntimeScatter)
            {
                Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural family has runtime scatter disabled.");
                errorCount++;
            }
        }

        private static void ValidateFinalVariants(FamilyRecord record, VariantMetrics metrics, ref int errorCount, ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();

            if (metrics.RealFinalCount <= 0 && metrics.PlaceholderFinalCount > 0)
            {
                Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: only placeholder structural finals are linked. Structural scatter requires real final prefabs.");
                errorCount++;
            }
            else if (metrics.RealFinalCount <= 0)
            {
                Debug.LogWarning($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: no structural finals are linked.");
                warningCount++;
            }

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null || !variant.finalReady || variant.proxyOnly || variant.prefab == null)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                {
                    Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' is final-ready but still points at procedural placeholder prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                if (WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh(variant.prefab))
                {
                    Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' uses Unity built-in primitive mesh ids in prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                GameObject prefab = variant.prefab;
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                HashSet<Material> inspectedMaterials = new HashSet<Material>(32);
                if (renderers == null || renderers.Length <= 0)
                {
                    Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' has no renderers in prefab '{prefabPath}'.");
                    errorCount++;
                }
                else
                {
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        if (renderer == null)
                            continue;

                        Material[] sharedMaterials = renderer.sharedMaterials;
                        if (sharedMaterials == null || sharedMaterials.Length <= 0)
                        {
                            Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' renderer '{renderer.name}' has no shared materials.");
                            errorCount++;
                            continue;
                        }

                        for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                        {
                            Material material = sharedMaterials[materialIndex];
                            if (material == null)
                            {
                                Debug.LogError($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' renderer '{renderer.name}' has null material slot {materialIndex}.");
                                errorCount++;
                                continue;
                            }

                            if (!inspectedMaterials.Add(material))
                                continue;

                            if (WorldProceduralStructuralContract.TryGetMaterialContractFailure(material, out string failureLabel))
                            {
                                Debug.LogWarning($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' uses material '{material.name}' with invalid structural contract ({failureLabel}).");
                                warningCount++;
                            }
                        }
                    }
                }

                LODGroup[] lodGroups = prefab.GetComponentsInChildren<LODGroup>(true);
                if (RequiresStructuralLod(family) && (lodGroups == null || lodGroups.Length <= 0))
                {
                    Debug.LogWarning($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' has no LODGroup on prefab '{prefabPath}'.");
                    warningCount++;
                }
                else if (lodGroups != null)
                {
                    for (int lodGroupIndex = 0; lodGroupIndex < lodGroups.Length; lodGroupIndex++)
                    {
                        LODGroup lodGroup = lodGroups[lodGroupIndex];
                        if (lodGroup == null)
                            continue;

                        if (WorldProceduralStructuralContract.TryGetLodContractFailure(lodGroup, out string failureLabel))
                        {
                            Debug.LogWarning($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' LODGroup '{lodGroup.name}' fails structural LOD contract ({failureLabel}).");
                            warningCount++;
                        }
                    }
                }

                if (renderers != null && renderers.Length > ResolveRendererBudget(family))
                {
                    Debug.LogWarning($"[WorldProceduralStructuralFinalValidator] {record.AssetPath}: structural variant '{variant.variantId}' exceeds soft renderer budget ({renderers.Length}>{ResolveRendererBudget(family)}).");
                    warningCount++;
                }
            }
        }

        private static VariantMetrics MeasureVariants(WorldPrefabFamilyProfile family)
        {
            VariantMetrics metrics = new VariantMetrics();
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
            metrics.TotalVariants = variants.Length;

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null)
                    continue;

                if (variant.proxyOnly)
                    metrics.ProxyVariantCount++;

                if (!variant.finalReady || variant.proxyOnly)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    metrics.PlaceholderFinalCount++;
                else
                    metrics.RealFinalCount++;
            }

            return metrics;
        }

        private static bool IsStructuralDomain(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Debris:
                case WorldPrefabFamilyProfile.ProceduralDomain.RuinModule:
                case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                    return true;

                default:
                    return false;
            }
        }

        private static WorldStreamingLayer ResolveExpectedStreamingLayer(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Debris:
                    return WorldStreamingLayer.Debris;

                case WorldPrefabFamilyProfile.ProceduralDomain.RuinModule:
                case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                    return WorldStreamingLayer.Construction;

                default:
                    return WorldStreamingLayer.Construction;
            }
        }

        private static bool RequiresStructuralLod(WorldPrefabFamilyProfile family)
        {
            return WorldProceduralStructuralContract.RequiresStructuralLod(family);
        }

        private static int ResolveRendererBudget(WorldPrefabFamilyProfile family)
        {
            return WorldProceduralStructuralContract.ResolveRendererBudget(family);
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
            public int TotalVariants;
            public int ProxyVariantCount;
            public int RealFinalCount;
            public int PlaceholderFinalCount;
        }
    }
}
