#if UNITY_EDITOR
using System;
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
        private const string WorldSceneName = "02_HECTON_WORLD";
        private const string Mx350ShaderStripEnvironmentVariable = "HECTON_MX350_SHADER_STRIP";
        private static readonly string[] UrpAssetRoots = { "Assets/_Project/Data" };
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

            int remainingCount = data.Count;
            int stripCount = 0;
            for (int variantIndex = data.Count - 1; variantIndex >= 0; variantIndex--)
            {
                if (remainingCount > 1 && ShouldStripVariant(data[variantIndex], policy))
                {
                    data.RemoveAt(variantIndex);
                    remainingCount--;
                    stripCount++;
                }
            }

            s_StrippedVariantCount += stripCount;
        }

        internal static string BuildCurrentPolicySummary()
        {
            VariantStripPolicy policy = GetOrBuildPolicy();
            StringBuilder builder = new StringBuilder(512);
            builder.Append("[HectonShaderVariantStripper] URPAssets=").Append(policy.AssetCount)
                .Append(", BuildMaterialAssets=").Append(policy.MaterialAssetCount)
                .Append(", BuildMaterialKeywords=").Append(policy.UsedMaterialKeywords.Count)
                .Append(", StripMainLightShadows=").Append(Bool01(policy.StripMainLightShadows))
                .Append(", StripAdditionalLights=").Append(Bool01(policy.StripAdditionalLights))
                .Append(", StripAdditionalLightShadows=").Append(Bool01(policy.StripAdditionalLightShadows))
                .Append(", StripSoftShadows=").Append(Bool01(policy.StripSoftShadows))
                .Append(", StripMixedLighting=").Append(Bool01(policy.StripMixedLighting))
                .Append(", StripPointLights=").Append(Bool01(policy.StripPointLights))
                .Append(", StripSpotLights=").Append(Bool01(policy.StripSpotLights))
                .Append(", StripMathLodHigh=").Append(Bool01(policy.StripMathLodHigh))
                .Append(", StripQuestAndroidTBDR=").Append(Bool01(policy.StripQuestAndroidTBDRVariants))
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
            HashSet<string> usedMaterialKeywords = new HashSet<string>(128, StringComparer.Ordinal);
            int materialAssetCount = CollectWorldSceneMaterialKeywords(usedMaterialKeywords, out string materialEvidence);
            bool stripMx350LightVariants = ShouldStripMx350LightVariants();
            bool stripQuestAndroidTBDRVariants = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;

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
                stripPointLights: stripMx350LightVariants,
                stripSpotLights: stripMx350LightVariants,
                stripMathLodHigh: stripMx350LightVariants,
                stripQuestAndroidTBDRVariants: stripQuestAndroidTBDRVariants,
                materialAssetCount: materialAssetCount,
                usedMaterialKeywords: usedMaterialKeywords,
                evidence: evidence.Append(" | materialScope=").Append(materialEvidence)
                    .Append(" | mx350StripEnv=").Append(global::System.Environment.GetEnvironmentVariable(Mx350ShaderStripEnvironmentVariable) ?? "<unset>")
                    .ToString());
            s_HasCachedPolicy = true;
            return s_CachedPolicy;
        }

        private static bool ShouldStripVariant(ShaderCompilerData variant, VariantStripPolicy policy)
        {
            ShaderKeyword[] keywords = variant.shaderKeywordSet.GetShaderKeywords();
            for (int i = 0; i < keywords.Length; i++)
            {
                string keywordName = keywords[i].name;
                if ((policy.StripMainLightShadows && IsMainLightShadowKeyword(keywordName))
                    || (policy.StripAdditionalLights && IsAdditionalLightKeyword(keywordName))
                    || (policy.StripAdditionalLightShadows && IsAdditionalLightShadowKeyword(keywordName))
                    || (policy.StripSoftShadows && IsSoftShadowKeyword(keywordName))
                    || (policy.StripMixedLighting && IsMixedLightingKeyword(keywordName))
                    || (policy.StripPointLights && IsPointLightKeyword(keywordName))
                    || (policy.StripSpotLights && IsSpotLightKeyword(keywordName))
                    || (policy.StripMathLodHigh && IsMathLodHighKeyword(keywordName))
                    || (policy.StripQuestAndroidTBDRVariants && IsQuestAndroidTBDRKeyword(keywordName)))
                {
                    return true;
                }

                if (policy.HasMaterialKeywordPolicy &&
                    IsMaterialOwnedKeyword(keywordName) &&
                    !policy.UsedMaterialKeywords.Contains(keywordName))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CollectWorldSceneMaterialKeywords(HashSet<string> usedKeywords, out string evidence)
        {
            evidence = string.Empty;
            if (usedKeywords == null)
                return 0;

            string worldScenePath = ResolveWorldScenePath();
            if (string.IsNullOrEmpty(worldScenePath))
            {
                evidence = "02_HECTON_WORLD scene not found; material-keyword stripping disabled";
                return 0;
            }

            string[] dependencies = AssetDatabase.GetDependencies(worldScenePath, true);
            HashSet<string> materialPaths = new HashSet<string>(dependencies.Length, StringComparer.OrdinalIgnoreCase);
            for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                string dependencyPath = dependencies[dependencyIndex];
                if (dependencyPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    materialPaths.Add(dependencyPath);
            }

            HashSet<string>.Enumerator materialPathEnumerator = materialPaths.GetEnumerator();
            while (materialPathEnumerator.MoveNext())
            {
                string materialPath = materialPathEnumerator.Current;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                    continue;

                LocalKeyword[] enabledKeywords = material.enabledKeywords;
                for (int keywordIndex = 0; keywordIndex < enabledKeywords.Length; keywordIndex++)
                {
                    string keyword = enabledKeywords[keywordIndex].name;
                    if (!string.IsNullOrEmpty(keyword))
                        usedKeywords.Add(keyword);
                }
            }

            evidence = worldScenePath + " materials=" + materialPaths.Count;
            return materialPaths.Count;
        }

        private static string ResolveWorldScenePath()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
            {
                EditorBuildSettingsScene scene = scenes[sceneIndex];
                if (scene == null || string.IsNullOrEmpty(scene.path))
                    continue;

                if (scene.path.EndsWith("/" + WorldSceneName + ".unity", StringComparison.OrdinalIgnoreCase))
                    return scene.path;
            }

            string[] guids = AssetDatabase.FindAssets(WorldSceneName + " t:Scene");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("/" + WorldSceneName + ".unity", StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return string.Empty;
        }

        private static bool IsMaterialOwnedKeyword(string keywordName)
        {
            if (string.IsNullOrEmpty(keywordName))
                return false;

            if (IsMixedLightingKeyword(keywordName) ||
                IsMainLightShadowKeyword(keywordName) ||
                IsAdditionalLightKeyword(keywordName) ||
                IsAdditionalLightShadowKeyword(keywordName) ||
                IsSoftShadowKeyword(keywordName))
            {
                return false;
            }

            if (keywordName.StartsWith("UNITY_", StringComparison.Ordinal) ||
                keywordName.StartsWith("STEREO_", StringComparison.Ordinal) ||
                keywordName.StartsWith("INSTANCING_", StringComparison.Ordinal) ||
                keywordName.StartsWith("LIGHTMAP", StringComparison.Ordinal) ||
                keywordName.StartsWith("DIRLIGHTMAP", StringComparison.Ordinal) ||
                keywordName.StartsWith("DYNAMICLIGHTMAP", StringComparison.Ordinal) ||
                keywordName.StartsWith("SHADOWS_", StringComparison.Ordinal) ||
                keywordName.StartsWith("FOG_", StringComparison.Ordinal) ||
                keywordName.StartsWith("_MAIN_LIGHT", StringComparison.Ordinal) ||
                keywordName.StartsWith("_ADDITIONAL_LIGHT", StringComparison.Ordinal) ||
                keywordName.StartsWith("_SCREEN_SPACE", StringComparison.Ordinal) ||
                keywordName.StartsWith("_DBUFFER", StringComparison.Ordinal) ||
                keywordName.StartsWith("_GBUFFER", StringComparison.Ordinal) ||
                string.Equals(keywordName, "PROCEDURAL_INSTANCING_ON", StringComparison.Ordinal) ||
                string.Equals(keywordName, "DOTS_INSTANCING_ON", StringComparison.Ordinal))
            {
                return false;
            }

            return keywordName.StartsWith("_", StringComparison.Ordinal) ||
                   keywordName.Contains("QUALITY") ||
                   keywordName.Contains("HECTON");
        }

        private static bool ShouldStripMx350LightVariants()
        {
            string value = global::System.Environment.GetEnvironmentVariable(Mx350ShaderStripEnvironmentVariable);
            return !string.Equals(value, "0", StringComparison.Ordinal);
        }

        private static bool IsMixedLightingKeyword(string keywordName)
        {
            switch (keywordName)
            {
                case "DIRLIGHTMAP_COMBINED":
                case "DYNAMICLIGHTMAP_ON":
                case "LIGHTMAP_SHADOW_MIXING":
                case "SHADOWS_SHADOWMASK":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMainLightShadowKeyword(string keywordName)
        {
            switch (keywordName)
            {
                case "_MAIN_LIGHT_SHADOWS":
                case "_MAIN_LIGHT_SHADOWS_CASCADE":
                case "_MAIN_LIGHT_SHADOWS_SCREEN":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAdditionalLightKeyword(string keywordName)
        {
            switch (keywordName)
            {
                case "_ADDITIONAL_LIGHTS":
                case "_ADDITIONAL_LIGHTS_VERTEX":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAdditionalLightShadowKeyword(string keywordName)
        {
            return string.Equals(keywordName, "_ADDITIONAL_LIGHT_SHADOWS", StringComparison.Ordinal);
        }

        private static bool IsPointLightKeyword(string keywordName)
        {
            switch (keywordName)
            {
                case "POINT":
                case "POINT_COOKIE":
                case "POINT_LIGHTS":
                case "_POINT":
                case "_POINT_LIGHTS":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSpotLightKeyword(string keywordName)
        {
            switch (keywordName)
            {
                case "SPOT":
                case "SPOT_COOKIE":
                case "SPOT_LIGHTS":
                case "_SPOT":
                case "_SPOT_LIGHTS":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMathLodHighKeyword(string keywordName)
        {
            return string.Equals(keywordName, "_MATH_LOD_HIGH", StringComparison.Ordinal);
        }

        private static bool IsSoftShadowKeyword(string keywordName)
        {
            switch (keywordName)
            {
                case "_SHADOWS_SOFT":
                case "_SHADOWS_SOFT_LOW":
                case "_SHADOWS_SOFT_MEDIUM":
                case "_SHADOWS_SOFT_HIGH":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsQuestAndroidTBDRKeyword(string keywordName)
        {
            if (IsSoftShadowKeyword(keywordName))
                return true;

            switch (keywordName)
            {
                case "DIRLIGHTMAP_COMBINED":
                case "DYNAMICLIGHTMAP_ON":
                case "LIGHTMAP_SHADOW_MIXING":
                case "SHADOWS_SHADOWMASK":
                case "_HDR":
                case "HDR":
                case "UNITY_HDR_ON":
                case "_USE_HDR":
                case "_ENABLE_HDR":
                    return true;
                default:
                    return false;
            }
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
                bool stripPointLights,
                bool stripSpotLights,
                bool stripMathLodHigh,
                bool stripQuestAndroidTBDRVariants,
                int materialAssetCount,
                HashSet<string> usedMaterialKeywords,
                string evidence)
            {
                AssetCount = assetCount;
                StripMainLightShadows = stripMainLightShadows;
                StripAdditionalLights = stripAdditionalLights;
                StripAdditionalLightShadows = stripAdditionalLightShadows;
                StripSoftShadows = stripSoftShadows;
                StripMixedLighting = stripMixedLighting;
                StripPointLights = stripPointLights;
                StripSpotLights = stripSpotLights;
                StripMathLodHigh = stripMathLodHigh;
                StripQuestAndroidTBDRVariants = stripQuestAndroidTBDRVariants;
                MaterialAssetCount = materialAssetCount;
                UsedMaterialKeywords = usedMaterialKeywords ?? new HashSet<string>(0, StringComparer.Ordinal);
                Evidence = evidence ?? string.Empty;
            }

            internal int AssetCount { get; }
            internal int MaterialAssetCount { get; }
            internal bool StripMainLightShadows { get; }
            internal bool StripAdditionalLights { get; }
            internal bool StripAdditionalLightShadows { get; }
            internal bool StripSoftShadows { get; }
            internal bool StripMixedLighting { get; }
            internal bool StripPointLights { get; }
            internal bool StripSpotLights { get; }
            internal bool StripMathLodHigh { get; }
            internal bool StripQuestAndroidTBDRVariants { get; }
            internal HashSet<string> UsedMaterialKeywords { get; }
            internal bool HasMaterialKeywordPolicy => UsedMaterialKeywords.Count > 0;
            internal string Evidence { get; }
            internal bool HasAnyStrips =>
                StripMainLightShadows
                || StripAdditionalLights
                || StripAdditionalLightShadows
                || StripSoftShadows
                || StripMixedLighting
                || StripPointLights
                || StripSpotLights
                || StripMathLodHigh
                || StripQuestAndroidTBDRVariants
                || HasMaterialKeywordPolicy;
        }
    }
}
#endif
