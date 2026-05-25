using System.Globalization;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Shared support-family validation contract for world support materials, routing, and LOD rules.
    /// </summary>
    internal static class WorldProceduralSupportContract
    {
        internal const string ManagedMaterialRoot = "Assets/_Project/Art/Materials/WorldSupport";
        internal const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        internal const string StandardShaderName = "Standard";
        internal const float LodThresholdTolerance = 0.0005f;
        internal const float RequiredLod0Threshold = 0.6f;
        internal const float RequiredLod1Threshold = 0.15f;
        internal const float RequiredLod2Threshold = 0.04f;

        internal static bool IsSupportDomain(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket:
                case WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket:
                case WorldPrefabFamilyProfile.ProceduralDomain.SafePocket:
                case WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn:
                    return true;

                default:
                    return false;
            }
        }

        internal static WorldStreamingLayer ResolveExpectedStreamingLayer(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return WorldStreamingLayer.Resources;

            if (family.contributesLargeThreatZone)
                return WorldStreamingLayer.LargeThreats;

            switch (family.proceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket:
                    return WorldStreamingLayer.Resources;

                case WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket:
                case WorldPrefabFamilyProfile.ProceduralDomain.SafePocket:
                    return WorldStreamingLayer.Construction;

                case WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn:
                    return WorldStreamingLayer.Fauna;

                default:
                    return WorldStreamingLayer.Resources;
            }
        }

        internal static bool RequiresSupportLod(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            return family.contributesLargeThreatZone
                || family.budgetClass == WorldPrefabFamilyProfile.BudgetClass.Heavy;
        }

        internal static int ResolveRendererBudget(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 6;

            if (family.contributesLargeThreatZone)
                return 12;

            switch (family.proceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn:
                    return 9;

                case WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket:
                    return 9;

                case WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket:
                    return 9;

                case WorldPrefabFamilyProfile.ProceduralDomain.SafePocket:
                default:
                    return 8;
            }
        }

        internal static bool IsManagedMaterial(Material material)
        {
            if (material == null)
                return false;

            string materialPath = UnityEditor.AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrWhiteSpace(materialPath))
                return false;

            string normalizedPath = materialPath.Replace('\\', '/');
            return normalizedPath.StartsWith(ManagedMaterialRoot, System.StringComparison.Ordinal);
        }

        internal static bool TryGetMaterialContractFailure(Material material, out string failureLabel)
        {
            if (material == null)
            {
                failureLabel = "missing-material";
                return true;
            }

            if (!IsManagedMaterial(material))
            {
                failureLabel = "material-outside-managed-support-root";
                return true;
            }

            if (material.shader == null)
            {
                failureLabel = "missing-shader";
                return true;
            }

            if (material.shader.name != UrpLitShaderName && material.shader.name != StandardShaderName)
            {
                failureLabel = $"unexpected-shader:{material.shader.name}";
                return true;
            }

            if (!material.enableInstancing)
            {
                failureLabel = "instancing-disabled";
                return true;
            }

            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.001f)
            {
                failureLabel = "transparent-surface";
                return true;
            }

            if (material.HasProperty("_ZWrite") && material.GetFloat("_ZWrite") < 0.999f)
            {
                failureLabel = "zwrite-disabled";
                return true;
            }

            failureLabel = string.Empty;
            return false;
        }

        internal static bool TryGetLodContractFailure(LODGroup lodGroup, out string failureLabel)
        {
            if (lodGroup == null)
            {
                failureLabel = "missing-lodgroup";
                return true;
            }

            if (lodGroup.fadeMode != LODFadeMode.CrossFade)
            {
                failureLabel = "fade-mode-not-crossfade";
                return true;
            }

            if (!lodGroup.animateCrossFading)
            {
                failureLabel = "animate-crossfading-disabled";
                return true;
            }

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length != 3)
            {
                failureLabel = $"lod-count:{(lods != null ? lods.Length : 0)}";
                return true;
            }

            if (!MatchesLodTransition(lods[0].screenRelativeTransitionHeight, RequiredLod0Threshold)
                || !MatchesLodTransition(lods[1].screenRelativeTransitionHeight, RequiredLod1Threshold)
                || !MatchesLodTransition(lods[2].screenRelativeTransitionHeight, RequiredLod2Threshold))
            {
                failureLabel =
                    "stale-thresholds:" +
                    FormatLodThreshold(lods[0].screenRelativeTransitionHeight) + "/" +
                    FormatLodThreshold(lods[1].screenRelativeTransitionHeight) + "/" +
                    FormatLodThreshold(lods[2].screenRelativeTransitionHeight);
                return true;
            }

            failureLabel = string.Empty;
            return false;
        }

        private static bool MatchesLodTransition(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= LodThresholdTolerance;
        }

        private static string FormatLodThreshold(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
