#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Strips shader variants for URP features that are disabled across the authored HECTON-8 pipeline assets.
    /// </summary>
    internal sealed class HectonShaderVariantStripper : IPreprocessShaders, IPreprocessBuildWithReport
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Log Shader Variant Strip Policy";
        private static readonly string[] UrpAssetRoots = { "Assets/_Project/Data" };
        private static readonly HashSet<string> MixedLightingKeywords = new HashSet<string>
        {
            "DIRLIGHTMAP_COMBINED",
            "DYNAMICLIGHTMAP_ON",
            "LIGHTMAP_SHADOW_MIXING",
            "SHADOWS_SHADOWMASK"
        };
        private static readonly HashSet<string> MainLightShadowKeywords = new HashSet<string>
        {
            "_MAIN_LIGHT_SHADOWS",
            "_MAIN_LIGHT_SHADOWS_CASCADE",
            "_MAIN_LIGHT_SHADOWS_SCREEN"
        };
        private static readonly HashSet<string> AdditionalLightKeywords = new HashSet<string>
        {
            "_ADDITIONAL_LIGHTS",
            "_ADDITIONAL_LIGHTS_VERTEX"
        };
        private static readonly HashSet<string> AdditionalLightShadowKeywords = new HashSet<string>
        {
            "_ADDITIONAL_LIGHT_SHADOWS"
        };
        private static readonly HashSet<string> SoftShadowKeywords = new HashSet<string>
        {
            "_SHADOWS_SOFT",
            "_SHADOWS_SOFT_LOW",
            "_SHADOWS_SOFT_MEDIUM",
            "_SHADOWS_SOFT_HIGH"
        };

        private static VariantStripPolicy s_CachedPolicy;
        private static bool s_HasCachedPolicy;
        private static int s_StrippedVariantCount;

        public int callbackOrder => 0;

        [MenuItem(MenuPath, priority = 196)]
        private static void LogPolicyFromMenu()
        {
            Debug.Log(BuildCurrentPolicySummary());
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            s_HasCachedPolicy = false;
            s_StrippedVariantCount = 0;
            Debug.Log(BuildCurrentPolicySummary());
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || data == null || data.Count <= 0)
                return;

            VariantStripPolicy policy = GetOrBuildPolicy();
            if (!policy.HasAnyStrips)
                return;

            List<int> indicesToRemove = new List<int>(data.Count);
            for (int variantIndex = 0; variantIndex < data.Count; variantIndex++)
            {
                if (ShouldStripVariant(data[variantIndex], policy))
                    indicesToRemove.Add(variantIndex);
            }

            if (indicesToRemove.Count <= 0 || indicesToRemove.Count >= data.Count)
                return;

            for (int removeIndex = indicesToRemove.Count - 1; removeIndex >= 0; removeIndex--)
                data.RemoveAt(indicesToRemove[removeIndex]);

            s_StrippedVariantCount += indicesToRemove.Count;
        }

        internal static string BuildCurrentPolicySummary()
        {
            VariantStripPolicy policy = GetOrBuildPolicy();
            StringBuilder builder = new StringBuilder(512);
            builder.Append("[HectonShaderVariantStripper] URPAssets=").Append(policy.AssetCount)
                .Append(", StripMainLightShadows=").Append(Bool01(policy.StripMainLightShadows))
                .Append(", StripAdditionalLights=").Append(Bool01(policy.StripAdditionalLights))
                .Append(", StripAdditionalLightShadows=").Append(Bool01(policy.StripAdditionalLightShadows))
                .Append(", StripSoftShadows=").Append(Bool01(policy.StripSoftShadows))
                .Append(", StripMixedLighting=").Append(Bool01(policy.StripMixedLighting))
                .Append(", StrippedSoFar=").Append(s_StrippedVariantCount)
                .Append(", Evidence=").Append(policy.Evidence);
            return builder.ToString();
        }

        private static VariantStripPolicy GetOrBuildPolicy()
        {
            if (s_HasCachedPolicy)
                return s_CachedPolicy;

            bool supportsMainLightShadows = false;
            bool supportsAdditionalLights = false;
            bool supportsAdditionalLightShadows = false;
            bool supportsSoftShadows = false;
            bool supportsMixedLighting = false;

            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", UrpAssetRoots);
            List<string> assetPaths = new List<string>(guids.Length);
            for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                RenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
                if (asset == null)
                    continue;

                assetPaths.Add(assetPath);
                supportsMainLightShadows |= GetBoolPropertyValue(asset, "supportsMainLightShadows");
                supportsAdditionalLights |= !string.Equals(
                    GetEnumPropertyName(asset, "additionalLightsRenderingMode"),
                    "Disabled",
                    System.StringComparison.Ordinal);
                supportsAdditionalLightShadows |= GetBoolPropertyValue(asset, "supportsAdditionalLightShadows");
                supportsSoftShadows |= GetBoolPropertyValue(asset, "supportsSoftShadows");
                supportsMixedLighting |= GetBoolPropertyValue(asset, "supportsMixedLighting");
            }

            StringBuilder evidence = new StringBuilder(256);
            if (assetPaths.Count <= 0)
            {
                evidence.Append("no URP assets discovered under Assets/_Project/Data");
            }
            else
            {
                evidence.Append("scanned ");
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    if (i > 0)
                        evidence.Append(" | ");

                    evidence.Append(assetPaths[i]);
                }
            }

            s_CachedPolicy = new VariantStripPolicy(
                assetPaths.Count,
                stripMainLightShadows: !supportsMainLightShadows,
                stripAdditionalLights: !supportsAdditionalLights,
                stripAdditionalLightShadows: !supportsAdditionalLightShadows,
                stripSoftShadows: !supportsSoftShadows,
                stripMixedLighting: !supportsMixedLighting,
                evidence.ToString());
            s_HasCachedPolicy = true;
            return s_CachedPolicy;
        }

        private static bool ShouldStripVariant(ShaderCompilerData variant, VariantStripPolicy policy)
        {
            ShaderKeyword[] keywords = variant.shaderKeywordSet.GetShaderKeywords();
            for (int i = 0; i < keywords.Length; i++)
            {
                string keywordName = keywords[i].name;
                if ((policy.StripMainLightShadows && MainLightShadowKeywords.Contains(keywordName))
                    || (policy.StripAdditionalLights && AdditionalLightKeywords.Contains(keywordName))
                    || (policy.StripAdditionalLightShadows && AdditionalLightShadowKeywords.Contains(keywordName))
                    || (policy.StripSoftShadows && SoftShadowKeywords.Contains(keywordName))
                    || (policy.StripMixedLighting && MixedLightingKeywords.Contains(keywordName)))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Bool01(bool value)
        {
            return value ? 1 : 0;
        }

        private static bool GetBoolPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
                return false;

            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || property.PropertyType != typeof(bool))
                return false;

            object value = property.GetValue(target);
            return value is bool boolValue && boolValue;
        }

        private static string GetEnumPropertyName(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
                return string.Empty;

            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                return string.Empty;

            object value = property.GetValue(target);
            return value != null ? value.ToString() : string.Empty;
        }

        private readonly struct VariantStripPolicy
        {
            internal VariantStripPolicy(
                int assetCount,
                bool stripMainLightShadows,
                bool stripAdditionalLights,
                bool stripAdditionalLightShadows,
                bool stripSoftShadows,
                bool stripMixedLighting,
                string evidence)
            {
                AssetCount = assetCount;
                StripMainLightShadows = stripMainLightShadows;
                StripAdditionalLights = stripAdditionalLights;
                StripAdditionalLightShadows = stripAdditionalLightShadows;
                StripSoftShadows = stripSoftShadows;
                StripMixedLighting = stripMixedLighting;
                Evidence = evidence ?? string.Empty;
            }

            internal int AssetCount { get; }
            internal bool StripMainLightShadows { get; }
            internal bool StripAdditionalLights { get; }
            internal bool StripAdditionalLightShadows { get; }
            internal bool StripSoftShadows { get; }
            internal bool StripMixedLighting { get; }
            internal string Evidence { get; }
            internal bool HasAnyStrips =>
                StripMainLightShadows
                || StripAdditionalLights
                || StripAdditionalLightShadows
                || StripSoftShadows
                || StripMixedLighting;
        }
    }
}
#endif
