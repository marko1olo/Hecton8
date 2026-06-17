using System.Globalization;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Shared structural family validation contract for managed materials and LOD rules.
    /// </summary>
    internal static class WorldProceduralStructuralContract
    {
        internal const string ManagedMaterialRoot = "Assets/_Project/Art/Materials/Construction";
        internal const string ManagedTextureRoot = "Assets/_Project/Art/Textures/Construction";
        internal const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        internal const string StandardShaderName = "Standard";
        internal const float LodThresholdTolerance = 0.0005f;
        internal const float RequiredLod0Threshold = 0.6f;
        internal const float RequiredLod1Threshold = 0.15f;
        internal const float RequiredLod2Threshold = 0.04f;

        internal static bool RequiresStructuralLod(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            return family.budgetClass == WorldPrefabFamilyProfile.BudgetClass.Heavy
                || family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Landmark
                || family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.RuinModule;
        }

        internal static int ResolveRendererBudget(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 8;

            switch (family.proceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Debris:
                    return 6;

                case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                    return 8;

                case WorldPrefabFamilyProfile.ProceduralDomain.RuinModule:
                    return family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Landmark ? 16 : 12;

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
                failureLabel = "material-outside-managed-construction-root";
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

        internal static bool TryGetTextureContractFailure(Texture texture, string propertyName, out string failureLabel)
        {
            failureLabel = string.Empty;
            if (texture == null)
                return false;

            string texturePath = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(texturePath))
            {
                failureLabel = propertyName + ":non-asset-texture";
                return true;
            }

            string normalizedPath = texturePath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(ManagedTextureRoot + "/", System.StringComparison.Ordinal))
            {
                failureLabel = propertyName + ":outside-managed-construction-texture-root";
                return true;
            }

            TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                failureLabel = propertyName + ":missing-texture-importer";
                return true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                failureLabel = propertyName + ":wrap-not-repeat";
                return true;
            }

            if (!importer.mipmapEnabled)
            {
                failureLabel = propertyName + ":mipmaps-off";
                return true;
            }

            if (importer.isReadable)
            {
                failureLabel = propertyName + ":readwrite-on";
                return true;
            }

            if (importer.maxTextureSize > 2048)
            {
                failureLabel = propertyName + ":maxsize-too-high";
                return true;
            }

            if ((propertyName == "_BumpMap" || propertyName == "_DetailNormalMap") && importer.textureType != TextureImporterType.NormalMap)
            {
                failureLabel = propertyName + ":not-normalmap";
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
