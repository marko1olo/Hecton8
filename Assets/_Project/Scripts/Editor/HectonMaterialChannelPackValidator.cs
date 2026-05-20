#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Audits and enforces packed-mask authoring for first-party materials.
    /// </summary>
    internal static class HectonMaterialChannelPackValidator
    {
        private const string AuditMenuPath = "Hecton/Validation/Asset Pipeline/Audit Channel Packing";
        private const string EnforceMenuPath = "Hecton/Validation/Asset Pipeline/Enforce Channel Packing";
        private const string RenderingScanMenuPath = "Hecton8/Rendering/Texture Channel Packer/Scan Unoptimized PBR Materials";
        private const string RenderingReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const int PackedMaskPreviewResolution = 64;
        private const int MaxConsoleEntries = 32;
        private const byte ChannelDifferenceThreshold = 3;
        private static readonly string[] MaterialRoots = { "Assets/_Project/Art/Materials", "Assets/_Project/Materials", "Assets/_Project/Prefabs" };
        private static readonly string[] PackedMaskPropertyNames = { "_MaskMap", "_Mask_Map" };
        private static readonly HashSet<string> TargetShaders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Hecton8/Rendering/UberNoir",
            "Universal Render Pipeline/Lit",
            "Standard",
            "Hecton8/Environment/Hecton_DryZoneLit",
            "Hecton8/Environment/Hecton_AbyssalVoxelRock",
            "Hecton8/World/WreckIndirectLit",
            "Hecton8/World/ScatterIndirectLit",
            "Hecton8/Fauna/LeviathanOrganic"
        };

        internal sealed class AuditResult
        {
            internal int ScannedMaterialCount;
            internal int TargetMaterialCount;
            internal int AnalysedMaskCount;
            internal int FixedImporterCount;
            internal readonly List<string> Violations = new List<string>(128);
            internal readonly List<string> VramViolations = new List<string>(64);
            internal readonly List<string> PackedMaskViolations = new List<string>(64);
            internal readonly List<string> CompliantMaterials = new List<string>(64);
            internal readonly List<string> FixedImporters = new List<string>(32);
            internal readonly List<string> QuarantineCandidatePaths = new List<string>(16);
        }

        [MenuItem(AuditMenuPath, priority = 191)]
        private static void RunFromMenu()
        {
            AuditResult result = RunAudit();
            WriteRenderingOptimizationReport(result, applyImporterFixes: false);
            Debug.Log(
                $"[HectonMaterialChannelPackValidator] Scanned={result.ScannedMaterialCount}, " +
                $"Targeted={result.TargetMaterialCount}, AnalysedMasks={result.AnalysedMaskCount}, " +
                $"VRAMViolations={result.VramViolations.Count}, PackedMaskViolations={result.PackedMaskViolations.Count}, " +
                $"QuarantineCandidates={result.QuarantineCandidatePaths.Count}, Compliant={result.CompliantMaterials.Count}.");
            LogEntries("VRAM violations", result.VramViolations);
            LogEntries("Packed-mask violations", result.PackedMaskViolations);
        }

        [MenuItem(EnforceMenuPath, priority = 192)]
        private static void RunEnforcementFromMenu()
        {
            AuditResult result = RunEnforcement();
            WriteRenderingOptimizationReport(result, applyImporterFixes: true);
            Debug.Log(
                $"[HectonMaterialChannelPackValidator] Enforcement complete. Scanned={result.ScannedMaterialCount}, " +
                $"Targeted={result.TargetMaterialCount}, AnalysedMasks={result.AnalysedMaskCount}, " +
                $"FixedImporters={result.FixedImporterCount}, VRAMViolations={result.VramViolations.Count}, " +
                $"PackedMaskViolations={result.PackedMaskViolations.Count}, QuarantineCandidates={result.QuarantineCandidatePaths.Count}, " +
                $"Compliant={result.CompliantMaterials.Count}.");
            LogEntries("VRAM violations", result.VramViolations);
            LogEntries("Packed-mask violations", result.PackedMaskViolations);
        }

        [MenuItem(RenderingScanMenuPath, priority = 203)]
        private static void RunRenderingOptimizationScanFromMenu()
        {
            AuditResult result = RunAudit();
            WriteRenderingOptimizationReport(result, applyImporterFixes: false);
            Debug.Log(
                $"[HectonMaterialChannelPackValidator] Rendering optimization report wrote {RenderingReportPath}. " +
                $"Scanned={result.ScannedMaterialCount}, LooseStacks={result.VramViolations.Count}, PackedMaskViolations={result.PackedMaskViolations.Count}.");
        }

        internal static AuditResult RunAudit()
        {
            return RunInternal(applyImporterFixes: false);
        }

        internal static AuditResult RunEnforcement()
        {
            return RunInternal(applyImporterFixes: true);
        }

        private static AuditResult RunInternal(bool applyImporterFixes)
        {
            AuditResult result = new AuditResult();
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", MaterialRoots);
            bool anyImporterChanged = false;
            List<string> issueBuffer = new List<string>(8);

            for (int i = 0; i < materialGuids.Length; i++)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                    continue;

                result.ScannedMaterialCount++;
                if (!ShouldAudit(material))
                    continue;

                result.TargetMaterialCount++;
                issueBuffer.Clear();
                if (applyImporterFixes && ShouldValidatePackedMask(material.shader.name))
                {
                    anyImporterChanged |= TryFixMaskImporter(material, "_MaskMap", result);
                    anyImporterChanged |= TryFixMaskImporter(material, "_Mask_Map", result);
                }

                InspectMaterial(materialPath, material, issueBuffer, result);
                if (issueBuffer.Count <= 0)
                    result.CompliantMaterials.Add(materialPath);
            }

            ScanPrefabMaterials(result, applyImporterFixes, issueBuffer, ref anyImporterChanged);

            if (anyImporterChanged)
                AssetDatabase.SaveAssets();

            return result;
        }

        private static void ScanPrefabMaterials(
            AuditResult result,
            bool applyImporterFixes,
            List<string> issueBuffer,
            ref bool anyImporterChanged)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" });
            for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    continue;

                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null)
                        continue;

                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        if (material == null || !ShouldAudit(material))
                            continue;

                        string materialPath = ResolvePrefabMaterialPath(prefabPath, renderer, material, materialIndex);
                        result.ScannedMaterialCount++;
                        result.TargetMaterialCount++;
                        issueBuffer.Clear();
                        if (applyImporterFixes && ShouldValidatePackedMask(material.shader.name))
                        {
                            anyImporterChanged |= TryFixMaskImporter(material, "_MaskMap", result);
                            anyImporterChanged |= TryFixMaskImporter(material, "_Mask_Map", result);
                        }

                        InspectMaterial(materialPath, material, issueBuffer, result);
                        if (issueBuffer.Count <= 0)
                            result.CompliantMaterials.Add(materialPath);
                    }
                }
            }
        }

        private static string ResolvePrefabMaterialPath(string prefabPath, Renderer renderer, Material material, int materialIndex)
        {
            string materialAssetPath = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(materialAssetPath))
                return materialAssetPath;

            string rendererName = renderer != null ? renderer.name : "Renderer";
            string materialName = material != null ? material.name : "Material";
            return prefabPath + "::" + rendererName + "[" + materialIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]::" + materialName;
        }

        private static bool ShouldAudit(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            if (TargetShaders.Contains(material.shader.name))
                return true;

            return HasTexture(material, "_MetallicGlossMap")
                || HasTexture(material, "_OcclusionMap")
                || HasTexture(material, "_SpecGlossMap")
                || HasTexture(material, "_RoughnessMap")
                || HasTexture(material, "_RoughnessDirt")
                || HasTexture(material, "_EmissionMap");
        }

        private static void InspectMaterial(
            string materialPath,
            Material material,
            List<string> issueBuffer,
            AuditResult result)
        {
            string shaderName = material.shader != null ? material.shader.name : "<null>";
            string packedMaskPropertyName = GetPackedMaskPropertyName(material);
            bool hasMaskMap = !string.IsNullOrEmpty(packedMaskPropertyName);
            bool hasLooseMetallic = HasTexture(material, "_MetallicGlossMap");
            bool hasLooseOcclusion = HasTexture(material, "_OcclusionMap");
            bool hasLooseSpecGloss = HasTexture(material, "_SpecGlossMap");
            bool hasLooseRoughness = HasTexture(material, "_RoughnessMap") || HasTexture(material, "_RoughnessDirt");
            bool hasLooseEmission = HasTexture(material, "_EmissionMap");

            if (string.Equals(shaderName, "Hecton8/Environment/Hecton_AbyssalVoxelRock", StringComparison.Ordinal) &&
                HasTexture(material, "_MaskMap") &&
                !HasTexture(material, "_Mask_Map"))
            {
                AddPackedMaskViolation(result, materialPath, "Hecton_AbyssalVoxelRock samples _Mask_Map; assign the packed RGBA mask there, not only _MaskMap.", issueBuffer);
            }

            if (hasLooseMetallic || hasLooseOcclusion || hasLooseSpecGloss || hasLooseRoughness || hasLooseEmission)
            {
                AddVramViolation(
                    result,
                    materialPath,
                    $"uses loose texture stack: metallic={Bool01(hasLooseMetallic)}, ao={Bool01(hasLooseOcclusion)}, roughness={Bool01(hasLooseRoughness)}, specGloss={Bool01(hasLooseSpecGloss)}, emission={Bool01(hasLooseEmission)}.",
                    issueBuffer);
            }

            if (RequiresPackedMask(shaderName))
            {
                if (!hasMaskMap)
                {
                    AddPackedMaskViolation(result, materialPath, "missing required packed _MaskMap/_Mask_Map texture.", issueBuffer);
                    return;
                }
            }

            if (hasMaskMap && ShouldValidatePackedMask(shaderName))
                ValidatePackedMask(materialPath, material, packedMaskPropertyName, issueBuffer, result);
        }

        private static void ValidatePackedMask(
            string materialPath,
            Material material,
            string propertyName,
            List<string> issueBuffer,
            AuditResult result)
        {
            Texture texture = material.GetTexture(propertyName);
            if (texture == null && string.Equals(propertyName, "_MaskMap", StringComparison.Ordinal))
                texture = material.GetTexture("_Mask_Map");

            if (texture == null)
                return;

            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                if (!ValidatePackedTextureAsset(materialPath, texturePath, texture, issueBuffer, result))
                {
                    AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' has no TextureImporter at '{texturePath}'.", issueBuffer);
                    RegisterQuarantineCandidate(result, texturePath);
                }

                return;
            }

            if (importer.sRGBTexture)
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' is imported as sRGB. Mask textures must stay linear.", issueBuffer);

            if (importer.textureType != TextureImporterType.Default)
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' has TextureImporterType={importer.textureType}. Expected Default.", issueBuffer);

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (!IsPowerOfTwo(width) || !IsPowerOfTwo(height))
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' is {width}x{height}. Expected power-of-two dimensions.", issueBuffer);

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            if (!IsExpectedStandaloneFormat(standalone, TextureImporterFormat.BC7))
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' Standalone format is {ResolveFormatLabel(importer, standalone)}. Expected Standalone:BC7.", issueBuffer);

            result.AnalysedMaskCount++;
            if (!TryAnalysePackedMaskTexture(texture, importer, out PackedMaskAnalysis analysis, out string failureReason))
            {
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' analysis failed: {failureReason}", issueBuffer);
                RegisterQuarantineCandidate(result, texturePath);
                return;
            }

            if (!analysis.HasSourceAlpha)
            {
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' has no source alpha channel; RGBA pack is incomplete.", issueBuffer);
            }

            if (analysis.RgbChannelsCollapseToGreyscale)
            {
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' collapses to grayscale across RGB; packed AO/Roughness/Metallic data is absent.", issueBuffer);
            }
        }

        private static bool TryFixMaskImporter(Material material, string propertyName, AuditResult result)
        {
            if (material == null || !material.HasProperty(propertyName))
                return false;

            Texture texture = material.GetTexture(propertyName);
            if (texture == null)
                return false;

            string texturePath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                return false;

            bool changed = false;
            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                changed = true;
            }

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            if (!IsExpectedStandaloneFormat(standalone, TextureImporterFormat.BC7))
            {
                standalone.overridden = true;
                standalone.format = TextureImporterFormat.BC7;
                importer.SetPlatformTextureSettings(standalone);
                changed = true;
            }

            if (!changed)
                return false;

            importer.SaveAndReimport();
            result.FixedImporterCount++;
            result.FixedImporters.Add(texturePath);
            return true;
        }

        private static bool ValidatePackedTextureAsset(
            string materialPath,
            string texturePath,
            Texture texture,
            List<string> issueBuffer,
            AuditResult result)
        {
            Texture2D texture2D = texture as Texture2D;
            if (texture2D == null || !texturePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return false;

            if (texture2D.format != TextureFormat.BC7)
                AddPackedMaskViolation(result, materialPath, $"packed mask asset '{texture.name}' format is {texture2D.format}. Expected BC7.", issueBuffer);

            if (!IsPowerOfTwo(texture2D.width) || !IsPowerOfTwo(texture2D.height))
                AddPackedMaskViolation(result, materialPath, $"packed mask asset '{texture.name}' is {texture2D.width}x{texture2D.height}. Expected power-of-two dimensions.", issueBuffer);

            if (texture2D.mipmapCount <= 1)
                AddPackedMaskViolation(result, materialPath, $"packed mask asset '{texture.name}' has no mip chain.", issueBuffer);

            result.AnalysedMaskCount++;
            if (!TryAnalysePackedMaskTexture(texture, null, out PackedMaskAnalysis analysis, out string failureReason))
            {
                AddPackedMaskViolation(result, materialPath, $"packed mask asset '{texture.name}' analysis failed: {failureReason}", issueBuffer);
                RegisterQuarantineCandidate(result, texturePath);
                return true;
            }

            if (analysis.RgbChannelsCollapseToGreyscale)
                AddPackedMaskViolation(result, materialPath, $"packed mask asset '{texture.name}' collapses to grayscale across RGB; packed AO/Roughness/Metallic data is absent.", issueBuffer);

            return true;
        }

        private static bool TryAnalysePackedMaskTexture(
            Texture texture,
            TextureImporter importer,
            out PackedMaskAnalysis analysis,
            out string failureReason)
        {
            analysis = default;
            failureReason = string.Empty;

            if (texture == null)
            {
                failureReason = "texture reference is null.";
                return false;
            }

            Texture2D snapshot = null;
            try
            {
                snapshot = CaptureTextureSnapshot(texture);
                if (snapshot == null)
                {
                    failureReason = "GPU snapshot capture returned null.";
                    return false;
                }

                NativeArray<Color32> pixels = snapshot.GetRawTextureData<Color32>();
                if (pixels.Length <= 0)
                {
                    failureReason = "snapshot contains no pixels.";
                    return false;
                }

                bool hasRgbDifference = false;
                int sampleStride = Mathf.Max(1, pixels.Length / 1024);
                for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex += sampleStride)
                {
                    Color32 pixel = pixels[pixelIndex];
                    if (Mathf.Abs(pixel.r - pixel.g) > ChannelDifferenceThreshold
                        || Mathf.Abs(pixel.r - pixel.b) > ChannelDifferenceThreshold
                        || Mathf.Abs(pixel.g - pixel.b) > ChannelDifferenceThreshold)
                    {
                        hasRgbDifference = true;
                        break;
                    }
                }

                bool hasAlpha = importer == null || importer.DoesSourceTextureHaveAlpha();
                analysis = new PackedMaskAnalysis(hasAlpha, !hasRgbDifference);
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
            finally
            {
                if (snapshot != null)
                    UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        private static Texture2D CaptureTextureSnapshot(Texture texture)
        {
            int width = Mathf.Max(1, Mathf.Min(PackedMaskPreviewResolution, texture.width));
            int height = Mathf.Max(1, Mathf.Min(PackedMaskPreviewResolution, texture.height));
            RenderTexture tempRt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D snapshot = new Texture2D(width, height, TextureFormat.RGBA32, false, true);

            try
            {
                UnityEngine.Graphics.Blit(texture, tempRt);
                RenderTexture.active = tempRt;
                snapshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                snapshot.Apply(false, false);
                return snapshot;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tempRt);
            }
        }

        private static void AddVramViolation(AuditResult result, string assetPath, string message, List<string> issueBuffer)
        {
            string entry = $"{assetPath}: {message}";
            result.VramViolations.Add(entry);
            result.Violations.Add(entry);
            issueBuffer.Add(entry);
        }

        private static void AddPackedMaskViolation(AuditResult result, string assetPath, string message, List<string> issueBuffer)
        {
            string entry = $"{assetPath}: {message}";
            result.PackedMaskViolations.Add(entry);
            result.Violations.Add(entry);
            issueBuffer.Add(entry);
        }

        private static void RegisterQuarantineCandidate(AuditResult result, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            for (int i = 0; i < result.QuarantineCandidatePaths.Count; i++)
            {
                if (string.Equals(result.QuarantineCandidatePaths[i], assetPath, StringComparison.Ordinal))
                    return;
            }

            result.QuarantineCandidatePaths.Add(assetPath);
        }

        private static bool IsExpectedStandaloneFormat(TextureImporterPlatformSettings platformSettings, TextureImporterFormat expectedFormat)
        {
            return platformSettings != null &&
                   platformSettings.overridden &&
                   platformSettings.format == expectedFormat;
        }

        private static string ResolveFormatLabel(TextureImporter importer, TextureImporterPlatformSettings platformSettings)
        {
            if (platformSettings != null && platformSettings.overridden)
                return $"Standalone:{platformSettings.format}";

            return $"Default:{importer.textureCompression}";
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static string GetPackedMaskPropertyName(Material material)
        {
            for (int i = 0; i < PackedMaskPropertyNames.Length; i++)
            {
                string propertyName = PackedMaskPropertyNames[i];
                if (HasTexture(material, propertyName))
                    return propertyName;
            }

            return string.Empty;
        }

        private static bool RequiresPackedMask(string shaderName)
        {
            return string.Equals(shaderName, "Hecton8/Environment/Hecton_AbyssalVoxelRock", StringComparison.Ordinal) ||
                   string.Equals(shaderName, "Hecton8/Environment/Hecton_DryZoneLit", StringComparison.Ordinal) ||
                   string.Equals(shaderName, "Hecton8/Rendering/UberNoir", StringComparison.Ordinal) ||
                   string.Equals(shaderName, "Hecton8/World/WreckIndirectLit", StringComparison.Ordinal) ||
                   string.Equals(shaderName, "Hecton8/World/ScatterIndirectLit", StringComparison.Ordinal) ||
                   string.Equals(shaderName, "Hecton8/Fauna/LeviathanOrganic", StringComparison.Ordinal);
        }

        private static void WriteRenderingOptimizationReport(AuditResult result, bool applyImporterFixes)
        {
            string directory = Path.GetDirectoryName(RenderingReportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(RenderingReportPath, BuildRenderingOptimizationReportJson(result, applyImporterFixes), Encoding.UTF8);
        }

        private static string BuildRenderingOptimizationReportJson(AuditResult result, bool applyImporterFixes)
        {
            StringBuilder builder = new StringBuilder(4096); // COLD ALLOC: StringBuilder[4096] - editor scanner report - owner: HectonMaterialChannelPackValidator
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.rendering_optimization_report.v1", true);
            AppendJson(builder, "scanner", "HectonMaterialChannelPackValidator", true);
            AppendJson(builder, "applyImporterFixes", applyImporterFixes, true);
            AppendJson(builder, "rollbackNetcodeExcluded", true, true);
            AppendJson(builder, "merkleStateExcluded", true, true);
            AppendJson(builder, "stateRingBufferExcluded", true, true);
            AppendJson(builder, "scannedMaterials", result.ScannedMaterialCount, true);
            AppendJson(builder, "targetMaterials", result.TargetMaterialCount, true);
            AppendJson(builder, "analysedMasks", result.AnalysedMaskCount, true);
            AppendJson(builder, "fixedImporters", result.FixedImporterCount, true);
            AppendJson(builder, "looseSamplerStackCount", result.VramViolations.Count, true);
            AppendJson(builder, "packedMaskViolationCount", result.PackedMaskViolations.Count, true);
            AppendJson(builder, "quarantineCandidateCount", result.QuarantineCandidatePaths.Count, true);
            AppendArray(builder, "looseSamplerStacks", result.VramViolations, true);
            AppendArray(builder, "packedMaskViolations", result.PackedMaskViolations, true);
            AppendArray(builder, "fixedImportersList", result.FixedImporters, true);
            AppendArray(builder, "quarantineCandidates", result.QuarantineCandidatePaths, false);
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool trailingComma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": \"");
            AppendEscaped(builder, value);
            builder.Append('"');
            if (trailingComma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool trailingComma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value);
            if (trailingComma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool trailingComma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
            if (trailingComma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendArray(StringBuilder builder, string name, List<string> values, bool trailingComma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": [\n");
            int count = values != null ? values.Count : 0;
            for (int i = 0; i < count; i++)
            {
                builder.Append("    \"");
                AppendEscaped(builder, values[i]);
                builder.Append('"');
                if (i + 1 < count)
                    builder.Append(',');
                builder.Append('\n');
            }

            builder.Append("  ]");
            if (trailingComma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                if (c == '\n')
                {
                    builder.Append("\\n");
                    continue;
                }

                if (c == '\r')
                    continue;

                builder.Append(c);
            }
        }

        private static bool ShouldValidatePackedMask(string shaderName)
        {
            return TargetShaders.Contains(shaderName);
        }

        private static void LogEntries(string label, List<string> entries)
        {
            if (entries == null || entries.Count <= 0)
                return;

            int maxCount = Mathf.Min(MaxConsoleEntries, entries.Count);
            for (int i = 0; i < maxCount; i++)
                Debug.LogWarning($"[HectonMaterialChannelPackValidator] {label}: {entries[i]}");

            if (entries.Count > maxCount)
                Debug.LogWarning($"[HectonMaterialChannelPackValidator] {label}: truncated {entries.Count - maxCount} additional entries.");
        }

        private static bool HasTexture(Material material, string propertyName)
        {
            return material != null
                && material.HasProperty(propertyName)
                && material.GetTexture(propertyName) != null;
        }

        private static int Bool01(bool value)
        {
            return value ? 1 : 0;
        }

        private readonly struct PackedMaskAnalysis
        {
            internal PackedMaskAnalysis(bool hasSourceAlpha, bool rgbChannelsCollapseToGreyscale)
            {
                HasSourceAlpha = hasSourceAlpha;
                RgbChannelsCollapseToGreyscale = rgbChannelsCollapseToGreyscale;
            }

            internal bool HasSourceAlpha { get; }
            internal bool RgbChannelsCollapseToGreyscale { get; }
        }
    }
}
#endif
