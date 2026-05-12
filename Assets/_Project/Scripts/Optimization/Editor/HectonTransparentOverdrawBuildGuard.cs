#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Build gate for blended transparent material pressure in the primary world scene.
    /// </summary>
    public sealed class HectonTransparentOverdrawBuildGuard : IPreprocessBuildWithReport
    {
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string ReportPath = "Library/Hecton8/transparent_overdraw_report.csv";
        private const float MaxTransparentPixelOverlapFactor = 2.5f;
        private const int MaxReportRows = 128;

        private static readonly string[] s_largeCoverageTokens =
        {
            "visor",
            "hud",
            "overlay",
            "fog",
            "smoke",
            "plume",
            "fluid",
            "rain",
            "glass",
            "splash"
        };
        private static readonly char[] s_csvEscapeCharacters = { ',', '"', '\r', '\n' }; // COLD ALLOC: char[4] - CSV escape scanner - owner: HectonTransparentOverdrawBuildGuard

        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateOrThrow();
        }

        [MenuItem("HECTON-8/Validation/Run 02_HECTON_WORLD Transparent Overdraw Gate")]
        public static void RunFromMenu()
        {
            TransparentOverdrawResult result = ValidateOrThrow();
            Debug.Log(
                "[HectonTransparentOverdrawBuildGuard] Passed. Factor=" +
                result.TransparentPixelOverlapFactor.ToString("F2", CultureInfo.InvariantCulture) +
                " Budget=" +
                MaxTransparentPixelOverlapFactor.ToString("F2", CultureInfo.InvariantCulture) +
                " BlendedMaterials=" +
                result.BlendedMaterialCount.ToString(CultureInfo.InvariantCulture) +
                " Report=" +
                result.ReportPath);
        }

        public static TransparentOverdrawResult ValidateOrThrow()
        {
            TransparentOverdrawResult result = AnalyzeWorldScene();
            if (result.TransparentPixelOverlapFactor > MaxTransparentPixelOverlapFactor)
            {
                string message =
                    "[HectonTransparentOverdrawBuildGuard] 02_HECTON_WORLD transparent pixel overlap factor exceeded. Factor=" +
                    result.TransparentPixelOverlapFactor.ToString("F2", CultureInfo.InvariantCulture) +
                    " Budget=" +
                    MaxTransparentPixelOverlapFactor.ToString("F2", CultureInfo.InvariantCulture) +
                    " BlendedMaterials=" +
                    result.BlendedMaterialCount.ToString(CultureInfo.InvariantCulture) +
                    " CutoutMaterials=" +
                    result.CutoutMaterialCount.ToString(CultureInfo.InvariantCulture) +
                    " OpaqueMaterials=" +
                    result.OpaqueMaterialCount.ToString(CultureInfo.InvariantCulture) +
                    " Report=" +
                    result.ReportPath;
                throw new BuildFailedException(message);
            }

            return result;
        }

        private static TransparentOverdrawResult AnalyzeWorldScene()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldScenePath);
            if (sceneAsset == null)
                throw new BuildFailedException("[HectonTransparentOverdrawBuildGuard] 02_HECTON_WORLD scene not found at " + WorldScenePath);

            string[] dependencies = AssetDatabase.GetDependencies(WorldScenePath, true);
            HashSet<string> materialPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyPath = dependencies[i];
                if (string.IsNullOrEmpty(dependencyPath) || !dependencyPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    continue;

                materialPaths.Add(dependencyPath);
            }

            int opaqueMaterialCount = 0;
            int cutoutMaterialCount = 0;
            int blendedMaterialCount = 0;
            float weightedBlendedCost = 0f;
            int reportRows = 0;
            StringBuilder report = new StringBuilder(16384);
            report.AppendLine("material_path,shader_path,classification,weighted_cost,render_queue,render_type");

            foreach (string materialPath in materialPaths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.shader == null)
                    continue;

                MaterialOverdrawClassification classification = ClassifyMaterial(material);
                if (classification.Kind == MaterialOverdrawKind.Blended)
                {
                    blendedMaterialCount++;
                    weightedBlendedCost += classification.WeightedCost;
                    if (reportRows < MaxReportRows)
                    {
                        AppendReportRow(report, materialPath, classification);
                        reportRows++;
                    }
                }
                else if (classification.Kind == MaterialOverdrawKind.Cutout)
                {
                    cutoutMaterialCount++;
                }
                else
                {
                    opaqueMaterialCount++;
                }
            }

            WriteReport(ReportPath, report);

            float opaqueDenominator = Mathf.Max(8f, opaqueMaterialCount + cutoutMaterialCount * 0.35f);
            float transparentPixelOverlapFactor = 1f + weightedBlendedCost * 4f / opaqueDenominator;
            return new TransparentOverdrawResult(
                transparentPixelOverlapFactor,
                blendedMaterialCount,
                cutoutMaterialCount,
                opaqueMaterialCount,
                ReportPath);
        }

        private static MaterialOverdrawClassification ClassifyMaterial(Material material)
        {
            string renderType = material.GetTag("RenderType", false, string.Empty);
            int renderQueue = material.renderQueue;
            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            ShaderSourceFlags sourceFlags = ReadShaderSourceFlags(shaderPath);
            bool taggedCutout = ContainsIgnoreCase(renderType, "TransparentCutout") ||
                ContainsIgnoreCase(renderType, "AlphaTest") ||
                (renderQueue >= 2450 && renderQueue < 3000);
            bool taggedTransparent = ContainsIgnoreCase(renderType, "Transparent") && !taggedCutout;
            bool queuedTransparent = renderQueue >= 3000;
            bool blended = sourceFlags.HasBlend && !taggedCutout;

            if (taggedTransparent || queuedTransparent || blended)
            {
                float cost = 1f;
                if (sourceFlags.HasBlend)
                    cost += 1f;
                if (sourceFlags.HasZWriteOff)
                    cost += 0.75f;
                if (IsLikelyLargeCoverage(material.name) || IsLikelyLargeCoverage(material.shader.name) || IsLikelyLargeCoverage(shaderPath))
                    cost += 1.25f;

                return new MaterialOverdrawClassification(
                    MaterialOverdrawKind.Blended,
                    cost,
                    renderQueue,
                    renderType,
                    shaderPath);
            }

            if (taggedCutout)
            {
                return new MaterialOverdrawClassification(
                    MaterialOverdrawKind.Cutout,
                    0.25f,
                    renderQueue,
                    renderType,
                    shaderPath);
            }

            return new MaterialOverdrawClassification(
                MaterialOverdrawKind.Opaque,
                0f,
                renderQueue,
                renderType,
                shaderPath);
        }

        private static ShaderSourceFlags ReadShaderSourceFlags(string shaderPath)
        {
            if (string.IsNullOrEmpty(shaderPath) || !File.Exists(shaderPath))
                return default;

            string shaderSource = File.ReadAllText(shaderPath);
            return new ShaderSourceFlags(
                ContainsNonOffBlend(shaderSource),
                shaderSource.IndexOf("ZWrite Off", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ContainsNonOffBlend(string shaderSource)
        {
            int searchIndex = 0;
            while (searchIndex < shaderSource.Length)
            {
                int blendIndex = shaderSource.IndexOf("Blend", searchIndex, StringComparison.OrdinalIgnoreCase);
                if (blendIndex < 0)
                    return false;

                int tokenEnd = blendIndex + 5;
                bool startsToken = blendIndex == 0 || char.IsWhiteSpace(shaderSource[blendIndex - 1]);
                bool endsToken = tokenEnd >= shaderSource.Length || char.IsWhiteSpace(shaderSource[tokenEnd]);
                if (startsToken && endsToken)
                {
                    int valueIndex = tokenEnd;
                    while (valueIndex < shaderSource.Length && (shaderSource[valueIndex] == ' ' || shaderSource[valueIndex] == '\t'))
                        valueIndex++;

                    if (!StartsWithToken(shaderSource, valueIndex, "Off") && !StartsWithBlendOneZero(shaderSource, valueIndex))
                        return true;
                }

                searchIndex = tokenEnd;
            }

            return false;
        }

        private static bool StartsWithToken(string value, int startIndex, string token)
        {
            if (startIndex < 0 || startIndex + token.Length > value.Length)
                return false;

            if (string.Compare(value, startIndex, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            int tokenEnd = startIndex + token.Length;
            return tokenEnd >= value.Length || char.IsWhiteSpace(value[tokenEnd]);
        }

        private static bool StartsWithBlendOneZero(string value, int startIndex)
        {
            if (!StartsWithToken(value, startIndex, "One"))
                return false;

            int secondTokenIndex = startIndex + 3;
            while (secondTokenIndex < value.Length && (value[secondTokenIndex] == ' ' || value[secondTokenIndex] == '\t'))
                secondTokenIndex++;

            return StartsWithToken(value, secondTokenIndex, "Zero");
        }

        private static bool IsLikelyLargeCoverage(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < s_largeCoverageTokens.Length; i++)
            {
                if (value.IndexOf(s_largeCoverageTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AppendReportRow(StringBuilder report, string materialPath, MaterialOverdrawClassification classification)
        {
            report.Append(EscapeCsv(materialPath));
            report.Append(',');
            report.Append(EscapeCsv(classification.ShaderPath));
            report.Append(',');
            report.Append(classification.Kind.ToString());
            report.Append(',');
            report.Append(classification.WeightedCost.ToString("F2", CultureInfo.InvariantCulture));
            report.Append(',');
            report.Append(classification.RenderQueue.ToString(CultureInfo.InvariantCulture));
            report.Append(',');
            report.AppendLine(EscapeCsv(classification.RenderType));
        }

        private static void WriteReport(string reportPath, StringBuilder report)
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(reportPath, report.ToString());
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.IndexOfAny(s_csvEscapeCharacters) < 0)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public readonly struct TransparentOverdrawResult
        {
            public readonly float TransparentPixelOverlapFactor;
            public readonly int BlendedMaterialCount;
            public readonly int CutoutMaterialCount;
            public readonly int OpaqueMaterialCount;
            public readonly string ReportPath;

            public TransparentOverdrawResult(
                float transparentPixelOverlapFactor,
                int blendedMaterialCount,
                int cutoutMaterialCount,
                int opaqueMaterialCount,
                string reportPath)
            {
                TransparentPixelOverlapFactor = transparentPixelOverlapFactor;
                BlendedMaterialCount = blendedMaterialCount;
                CutoutMaterialCount = cutoutMaterialCount;
                OpaqueMaterialCount = opaqueMaterialCount;
                ReportPath = reportPath;
            }
        }

        private readonly struct MaterialOverdrawClassification
        {
            public readonly MaterialOverdrawKind Kind;
            public readonly float WeightedCost;
            public readonly int RenderQueue;
            public readonly string RenderType;
            public readonly string ShaderPath;

            public MaterialOverdrawClassification(
                MaterialOverdrawKind kind,
                float weightedCost,
                int renderQueue,
                string renderType,
                string shaderPath)
            {
                Kind = kind;
                WeightedCost = weightedCost;
                RenderQueue = renderQueue;
                RenderType = renderType;
                ShaderPath = shaderPath;
            }
        }

        private readonly struct ShaderSourceFlags
        {
            public readonly bool HasBlend;
            public readonly bool HasZWriteOff;

            public ShaderSourceFlags(bool hasBlend, bool hasZWriteOff)
            {
                HasBlend = hasBlend;
                HasZWriteOff = hasZWriteOff;
            }
        }

        private enum MaterialOverdrawKind
        {
            Opaque,
            Cutout,
            Blended
        }
    }
}
#endif
