using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Validates world-support procedural families and linked final prefabs.
    /// </summary>
    public static class WorldProceduralSupportFinalValidator
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";

        [MenuItem("Hecton/Validation/Validate Procedural World Support Families", priority = 249)]
        public static void ValidateSupportFamilies()
        {
            List<FamilyRecord> records = LoadSupportFamilies();
            int errorCount = 0;
            int warningCount = 0;
            int realFinalFamilyCount = 0;
            int placeholderOnlyFamilyCount = 0;
            int largeThreatZoneCount = 0;

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

                if (family.contributesLargeThreatZone)
                    largeThreatZoneCount++;

                ValidateRouting(record, ref errorCount, ref warningCount);
                ValidateFinalVariants(record, metrics, ref errorCount, ref warningCount);
            }

            if (errorCount <= 0)
            {
                Debug.Log($"[WorldProceduralSupportFinalValidator] PASS families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}, largeThreatZones={largeThreatZoneCount}, warnings={warningCount}");
                return;
            }

            Debug.LogWarning($"[WorldProceduralSupportFinalValidator] COMPLETE errors={errorCount}, warnings={warningCount}, families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}, largeThreatZones={largeThreatZoneCount}");
        }

        private static List<FamilyRecord> LoadSupportFamilies()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyRecord> records = new List<FamilyRecord>(12);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !WorldProceduralSupportContract.IsSupportDomain(family.proceduralDomain))
                    continue;

                records.Add(new FamilyRecord(assetPath, family));
            }

            return records;
        }

        private static void ValidateRouting(FamilyRecord record, ref int errorCount, ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldStreamingLayer expectedLayer = WorldProceduralSupportContract.ResolveExpectedStreamingLayer(family);
            WorldStreamingLayer resolvedLayer = family.ResolveStreamingLayer();
            if (resolvedLayer != expectedLayer)
            {
                Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support family resolves to streaming layer '{resolvedLayer}', expected '{expectedLayer}'.");
                errorCount++;
            }

            if (!family.allowRuntimeScatter)
            {
                Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support family has runtime scatter disabled.");
                errorCount++;
            }

            if (family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn && family.placementMode != WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor)
            {
                Debug.LogWarning($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: creature-support family is not using SpawnAnchor placement.");
                warningCount++;
            }

            if (family.proceduralDomain != WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn && family.placementMode == WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor)
            {
                Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: pocket family uses SpawnAnchor placement.");
                errorCount++;
            }
        }

        private static void ValidateFinalVariants(FamilyRecord record, VariantMetrics metrics, ref int errorCount, ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();

            if (metrics.RealFinalCount <= 0 && metrics.PlaceholderFinalCount > 0)
            {
                Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: only placeholder support finals are linked. World-support scatter cannot ship with procedural placeholder finals.");
                errorCount++;
            }
            else if (metrics.RealFinalCount <= 0)
            {
                Debug.LogWarning($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: no support finals are linked.");
                warningCount++;
            }

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null || !variant.finalReady || variant.proxyOnly || variant.prefab == null)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                {
                    Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' is final-ready but still points at procedural placeholder prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                if (WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh(variant.prefab))
                {
                    Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' uses Unity built-in primitive mesh ids in prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                Renderer[] renderers = variant.prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length <= 0)
                {
                    Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' has no renderers in prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                HashSet<Material> inspectedMaterials = new HashSet<Material>(renderers.Length * 2);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null)
                        continue;

                    Material[] sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials == null || sharedMaterials.Length <= 0)
                    {
                        Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' renderer '{renderer.name}' has no shared materials.");
                        errorCount++;
                        continue;
                    }

                    for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                    {
                        Material material = sharedMaterials[materialIndex];
                        if (material == null)
                        {
                            Debug.LogError($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' renderer '{renderer.name}' has null material slot {materialIndex}.");
                            errorCount++;
                            continue;
                        }

                        if (!inspectedMaterials.Add(material))
                            continue;

                        if (WorldProceduralSupportContract.TryGetMaterialContractFailure(material, out string failureLabel))
                        {
                            Debug.LogWarning($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' uses material '{material.name}' with invalid support contract ({failureLabel}).");
                            warningCount++;
                        }
                    }
                }

                LODGroup[] lodGroups = variant.prefab.GetComponentsInChildren<LODGroup>(true);
                if (WorldProceduralSupportContract.RequiresSupportLod(family) && (lodGroups == null || lodGroups.Length <= 0))
                {
                    Debug.LogWarning($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' has no LODGroup on prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    warningCount++;
                }
                else if (lodGroups != null)
                {
                    for (int lodGroupIndex = 0; lodGroupIndex < lodGroups.Length; lodGroupIndex++)
                    {
                        LODGroup lodGroup = lodGroups[lodGroupIndex];
                        if (lodGroup == null)
                            continue;

                        if (WorldProceduralSupportContract.TryGetLodContractFailure(lodGroup, out string failureLabel))
                        {
                            Debug.LogWarning($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' LODGroup '{lodGroup.name}' fails support LOD contract ({failureLabel}).");
                            warningCount++;
                        }
                    }
                }

                if (renderers.Length > WorldProceduralSupportContract.ResolveRendererBudget(family))
                {
                    Debug.LogWarning($"[WorldProceduralSupportFinalValidator] {record.AssetPath}: support variant '{variant.variantId}' exceeds soft renderer budget ({renderers.Length}>{WorldProceduralSupportContract.ResolveRendererBudget(family)}).");
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
                if (variant == null)
                    continue;

                if (!variant.finalReady || variant.proxyOnly)
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
