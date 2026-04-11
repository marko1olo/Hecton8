using Hecton8.World;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Shared validation contract for organic misc families outside the main kelp/coral baked pipeline.
    /// </summary>
    internal static class WorldProceduralOrganicMiscContract
    {
        internal const string ManagedMaterialRoot = "Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc";
        internal const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        internal const string StandardShaderName = "Standard";
        internal const float LodThresholdTolerance = 0.0005f;
        internal const float RequiredLod0Threshold = 0.6f;
        internal const float RequiredLod1Threshold = 0.15f;
        internal const float RequiredLod2Threshold = 0.04f;

        internal static bool IsOrganicMiscFamily(string familyId)
        {
            return familyId == "family.egg.cluster" || familyId == "family.plant.giant";
        }

        internal static bool RequiresOrganicLod(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            return family.familyId == "family.plant.giant"
                || family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Landmark
                || family.budgetClass == WorldPrefabFamilyProfile.BudgetClass.Heavy;
        }

        internal static WorldStreamingLayer ResolveExpectedStreamingLayer(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return WorldStreamingLayer.Flora;

            switch (family.familyId)
            {
                case "family.egg.cluster":
                    return WorldStreamingLayer.Fauna;

                case "family.plant.giant":
                default:
                    return WorldStreamingLayer.Flora;
            }
        }

        internal static int ResolveRendererBudget(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 8;

            switch (family.familyId)
            {
                case "family.egg.cluster":
                    return 11;

                case "family.plant.giant":
                default:
                    return 12;
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
                failureLabel = "material-outside-managed-organic-misc-root";
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
                    $"stale-thresholds:{lods[0].screenRelativeTransitionHeight:0.###}/{lods[1].screenRelativeTransitionHeight:0.###}/{lods[2].screenRelativeTransitionHeight:0.###}";
                return true;
            }

            failureLabel = string.Empty;
            return false;
        }

        private static bool MatchesLodTransition(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= LodThresholdTolerance;
        }
    }
}
