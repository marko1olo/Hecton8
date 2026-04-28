#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        private const string FirstPartyMaterialRoot = "Assets/_Project/Art/Materials";
        private const int PackedMaskPreviewResolution = 64;
        private const int MaxConsoleEntries = 32;
        private const byte ChannelDifferenceThreshold = 3;
        private static readonly string[] MaterialRoots = { FirstPartyMaterialRoot };
        private static readonly string[] PackedMaskPropertyNames = { "_MaskMap", "_Mask_Map" };
        private static readonly HashSet<string> TargetShaders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Universal Render Pipeline/Lit",
            "Standard",
            "Hecton8/Environment/Hecton_DryZoneLit",
            "Hecton8/Environment/Hecton_AbyssalVoxelRock"
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
            Debug.Log(
                $"[HectonMaterialChannelPackValidator] Enforcement complete. Scanned={result.ScannedMaterialCount}, " +
                $"Targeted={result.TargetMaterialCount}, AnalysedMasks={result.AnalysedMaskCount}, " +
                $"FixedImporters={result.FixedImporterCount}, VRAMViolations={result.VramViolations.Count}, " +
                $"PackedMaskViolations={result.PackedMaskViolations.Count}, QuarantineCandidates={result.QuarantineCandidatePaths.Count}, " +
                $"Compliant={result.CompliantMaterials.Count}.");
            LogEntries("VRAM violations", result.VramViolations);
            LogEntries("Packed-mask violations", result.PackedMaskViolations);
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
                if (applyImporterFixes)
                {
                    anyImporterChanged |= TryFixMaskImporter(material, "_MaskMap", result);
                    anyImporterChanged |= TryFixMaskImporter(material, "_Mask_Map", result);
                }

                InspectMaterial(materialPath, material, issueBuffer, result);
                if (issueBuffer.Count <= 0)
                    result.CompliantMaterials.Add(materialPath);
            }

            if (anyImporterChanged)
                AssetDatabase.SaveAssets();

            return result;
        }

        private static bool ShouldAudit(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            if (TargetShaders.Contains(material.shader.name))
                return true;

            for (int i = 0; i < PackedMaskPropertyNames.Length; i++)
            {
                if (material.HasProperty(PackedMaskPropertyNames[i]))
                    return true;
            }

            return HasTexture(material, "_MetallicGlossMap")
                || HasTexture(material, "_OcclusionMap")
                || HasTexture(material, "_SpecGlossMap")
                || HasTexture(material, "_EmissionMap");
        }

        private static void InspectMaterial(
            string materialPath,
            Material material,
            List<string> issueBuffer,
            AuditResult result)
        {
            string shaderName = material.shader != null ? material.shader.name : "<null>";
            if (string.Equals(shaderName, "Hecton8/Environment/Hecton_DryZoneLit", StringComparison.Ordinal))
            {
                AddVramViolation(
                    result,
                    materialPath,
                    "legacy Hecton_DryZoneLit shader still depends on split _MetallicGlossMap/_OcclusionMap/_EmissionMap instead of a single packed RGBA mask.",
                    issueBuffer);
            }

            string packedMaskPropertyName = GetPackedMaskPropertyName(material);
            bool hasMaskMap = !string.IsNullOrEmpty(packedMaskPropertyName);
            bool hasLooseMetallic = HasTexture(material, "_MetallicGlossMap");
            bool hasLooseOcclusion = HasTexture(material, "_OcclusionMap");
            bool hasLooseSpecGloss = HasTexture(material, "_SpecGlossMap");
            bool hasLooseEmission = HasTexture(material, "_EmissionMap");

            if (hasLooseMetallic || hasLooseOcclusion || hasLooseSpecGloss || hasLooseEmission)
            {
                AddVramViolation(
                    result,
                    materialPath,
                    $"uses loose texture stack: metallic={Bool01(hasLooseMetallic)}, ao={Bool01(hasLooseOcclusion)}, specGloss={Bool01(hasLooseSpecGloss)}, emission={Bool01(hasLooseEmission)}.",
                    issueBuffer);
            }

            if (string.Equals(shaderName, "Hecton8/Environment/Hecton_AbyssalVoxelRock", StringComparison.Ordinal))
            {
                if (!hasMaskMap)
                {
                    AddPackedMaskViolation(result, materialPath, "missing required packed _MaskMap texture.", issueBuffer);
                    return;
                }
            }

            if (hasMaskMap)
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
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' has no TextureImporter at '{texturePath}'.", issueBuffer);
                RegisterQuarantineCandidate(result, texturePath);
                return;
            }

            if (importer.sRGBTexture)
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' is imported as sRGB. Mask textures must stay linear.", issueBuffer);

            if (importer.textureType != TextureImporterType.Default)
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' has TextureImporterType={importer.textureType}. Expected Default.", issueBuffer);

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
                AddPackedMaskViolation(result, materialPath, $"packed mask '{texture.name}' collapses to grayscale across RGB; packed metallic/AO/smoothness data is absent.", issueBuffer);
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

            if (!changed)
                return false;

            importer.SaveAndReimport();
            result.FixedImporterCount++;
            result.FixedImporters.Add(texturePath);
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

                Color32[] pixels = snapshot.GetPixels32();
                if (pixels == null || pixels.Length <= 0)
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

                analysis = new PackedMaskAnalysis(importer.DoesSourceTextureHaveAlpha(), !hasRgbDifference);
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
                Graphics.Blit(texture, tempRt);
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
