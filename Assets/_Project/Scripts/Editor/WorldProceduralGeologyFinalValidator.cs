using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Validates geological procedural families, their geology profiles, and linked final prefabs.
    /// </summary>
    public static class WorldProceduralGeologyFinalValidator
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private static readonly List<Renderer> s_RendererScratch = new List<Renderer>(32);
        private static readonly List<LODGroup> s_LodGroupScratch = new List<LODGroup>(8);

        /// <summary>
        /// Validates geological family/profile/final contracts.
        /// </summary>
        [MenuItem("Hecton/Validation/Validate Procedural Geology Families", priority = 245)]
        public static void ValidateGeologyFamilies()
        {
            List<FamilyRecord> records = LoadGeologyFamilies();
            int errorCount = 0;
            int warningCount = 0;
            int realFinalFamilyCount = 0;
            int placeholderOnlyFamilyCount = 0;
            int explicitProfileCount = 0;
            int emergencyFallbackCount = 0;

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

                if (family.generativeGeologyProfile != null)
                    explicitProfileCount++;
                else if (family.UsesGenerativeGeology())
                    emergencyFallbackCount++;

                ValidateFamilyRouting(record, ref errorCount, ref warningCount);
                ValidateGeologyProfile(record, ref errorCount, ref warningCount);
                ValidateFinalVariants(record, metrics, ref errorCount, ref warningCount);
            }

            if (errorCount <= 0)
            {
                Debug.Log($"[WorldProceduralGeologyFinalValidator] PASS families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}, explicitProfiles={explicitProfileCount}, emergencyFallbacks={emergencyFallbackCount}, warnings={warningCount}");
                return;
            }

            Debug.LogWarning($"[WorldProceduralGeologyFinalValidator] COMPLETE errors={errorCount}, warnings={warningCount}, families={records.Count}, realFinalFamilies={realFinalFamilyCount}, placeholderOnlyFamilies={placeholderOnlyFamilyCount}, explicitProfiles={explicitProfileCount}, emergencyFallbacks={emergencyFallbackCount}");
        }

        private static List<FamilyRecord> LoadGeologyFamilies()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            List<FamilyRecord> records = new List<FamilyRecord>(8);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || !IsGeologicalDomain(family.proceduralDomain))
                    continue;

                records.Add(new FamilyRecord(assetPath, family));
            }

            return records;
        }

        private static void ValidateFamilyRouting(FamilyRecord record, ref int errorCount, ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldStreamingLayer streamingLayer = family.ResolveStreamingLayer();
            if (streamingLayer != WorldStreamingLayer.TerrainLod)
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geological family resolves to streaming layer '{streamingLayer}', expected '{WorldStreamingLayer.TerrainLod}'.");
                errorCount++;
            }

            if (!family.allowRuntimeScatter)
            {
                Debug.LogWarning($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geological family has runtime scatter disabled.");
                warningCount++;
            }

            if (family.placementMode == WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor)
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geological family uses SpawnAnchor placement, which is invalid for geology.");
                errorCount++;
            }
        }

        private static void ValidateGeologyProfile(FamilyRecord record, ref int errorCount, ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldGenerativeGeologyProfile profile = family.generativeGeologyProfile;

            if (profile == null)
            {
                if (family.UsesGenerativeGeology())
                {
                    Debug.LogWarning($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: family relies on geological emergency fallback without explicit WorldGenerativeGeologyProfile.");
                    warningCount++;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(profile.profileId))
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: assigned geology profile '{profile.name}' is missing profileId.");
                errorCount++;
            }

            if (!profile.IsEnabled)
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: assigned geology profile '{profile.name}' is disabled.");
                errorCount++;
            }

            if (profile.seamBlendRadius < 0.5f)
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geology profile '{profile.name}' seamBlendRadius must be >= 0.5.");
                errorCount++;
            }

            if (profile.debrisCountMax < profile.debrisCountMin)
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geology profile '{profile.name}' debrisCountMax is below debrisCountMin.");
                errorCount++;
            }

            if (profile.lodCount < 1 || profile.lodCount > 3)
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geology profile '{profile.name}' lodCount must stay within 1..3.");
                errorCount++;
            }

            if (!HasStrictDescendingLodScreenHeights(profile.lodScreenHeights))
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geology profile '{profile.name}' has invalid lodScreenHeights {profile.lodScreenHeights}.");
                errorCount++;
            }

            if (profile.placementWeight <= 0f || profile.compositionWeight <= 0f)
            {
                Debug.LogWarning($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geology profile '{profile.name}' has non-positive placement/composition weight.");
                warningCount++;
            }
        }

        private static void ValidateFinalVariants(FamilyRecord record, VariantMetrics metrics, ref int errorCount, ref int warningCount)
        {
            WorldPrefabFamilyProfile family = record.Family;
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();

            if (metrics.RealFinalCount <= 0 && metrics.PlaceholderFinalCount > 0)
            {
                Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: only placeholder geological finals are linked. Surface/shallow geology requires real final prefabs or generated production meshes.");
                errorCount++;
            }
            else if (metrics.RealFinalCount <= 0)
            {
                Debug.LogWarning($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: no geological finals are linked.");
                warningCount++;
            }

            for (int i = 0; i < variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null || !variant.finalReady || variant.proxyOnly || variant.prefab == null)
                    continue;

                if (WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                {
                    Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geological variant '{variant.variantId}' is final-ready but still points at procedural placeholder prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                if (WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh(variant.prefab))
                {
                    Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geological variant '{variant.variantId}' uses Unity built-in primitive mesh ids in prefab '{AssetDatabase.GetAssetPath(variant.prefab)}'.");
                    errorCount++;
                    continue;
                }

                GameObject prefab = variant.prefab;
                s_RendererScratch.Clear();
                prefab.GetComponentsInChildren(true, s_RendererScratch);
                if (s_RendererScratch.Count <= 0)
                {
                    Debug.LogError($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geological variant '{variant.variantId}' has no renderers in prefab '{AssetDatabase.GetAssetPath(prefab)}'.");
                    errorCount++;
                }

                s_LodGroupScratch.Clear();
                prefab.GetComponentsInChildren(true, s_LodGroupScratch);
                if (RequiresLargeFormLod(family) && s_LodGroupScratch.Count <= 0)
                {
                    Debug.LogWarning($"[WorldProceduralGeologyFinalValidator] {record.AssetPath}: geological variant '{variant.variantId}' has no LODGroup on large-form prefab '{AssetDatabase.GetAssetPath(prefab)}'.");
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

        private static bool IsGeologicalDomain(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Rock:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockCluster:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockArch:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockShelf:
                case WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance:
                case WorldPrefabFamilyProfile.ProceduralDomain.Landmark:
                    return true;

                default:
                    return false;
            }
        }

        private static bool RequiresLargeFormLod(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            switch (family.proceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.RockArch:
                case WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance:
                case WorldPrefabFamilyProfile.ProceduralDomain.Landmark:
                    return true;

                default:
                    return family.budgetClass == WorldPrefabFamilyProfile.BudgetClass.Heavy
                        || family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Landmark;
            }
        }

        private static bool HasStrictDescendingLodScreenHeights(Vector3 lodScreenHeights)
        {
            return lodScreenHeights.x > lodScreenHeights.y
                && lodScreenHeights.y > lodScreenHeights.z
                && lodScreenHeights.z > 0f
                && lodScreenHeights.x <= 1f;
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
