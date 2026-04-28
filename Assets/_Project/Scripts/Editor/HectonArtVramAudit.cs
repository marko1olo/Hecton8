#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Estimates compressed VRAM usage for first-party art textures under Assets/_Project/Art.
    /// Uses BC7/BC5 production assumptions from the MX350 budget audit and warns when the folder exceeds 1.5 GB.
    /// </summary>
    internal static class HectonArtVramAudit
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Audit Art VRAM Budget";
        private const string ArtRoot = "Assets/_Project/Art";
        private const long WarningThresholdBytes = 1536L * 1024L * 1024L;
        private const int MaxLoggedTextures = 16;
        private const float MipChainMultiplier = 4f / 3f;

        internal struct TextureVramEntry
        {
            public string AssetPath;
            public long EstimatedBytes;
            public int Width;
            public int Height;
            public string CompressionLabel;
            public bool HasMipMaps;
        }

        [MenuItem(MenuPath, priority = 196)]
        private static void RunFromMenu()
        {
            AuditResult result = RunAudit();
            EmitResult(result);
        }

        internal static AuditResult RunAudit()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture", new[] { ArtRoot });
            List<TextureVramEntry> entries = new List<TextureVramEntry>(textureGuids.Length); // COLD ALLOC: List<TextureVramEntry>[textureGuids.Length] - editor-only VRAM report rows - owner: HectonArtVramAudit
            long totalEstimatedBytes = 0L;

            for (int i = 0; i < textureGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                if (importer == null || texture == null)
                    continue;

                TextureVramEntry entry = BuildEntry(assetPath, importer, texture);
                totalEstimatedBytes += entry.EstimatedBytes;
                entries.Add(entry);
            }

            entries.Sort(static (left, right) => right.EstimatedBytes.CompareTo(left.EstimatedBytes));

            return new AuditResult(totalEstimatedBytes, entries.Count, entries);
        }

        private static TextureVramEntry BuildEntry(string assetPath, TextureImporter importer, Texture texture)
        {
            int effectiveWidth = Mathf.Max(1, ResolveEffectiveDimension(texture.width, importer.maxTextureSize));
            int effectiveHeight = Mathf.Max(1, ResolveEffectiveDimension(texture.height, importer.maxTextureSize));
            bool hasMipMaps = importer.mipmapEnabled;
            bool isCube = importer.textureShape == TextureImporterShape.TextureCube;
            bool isNormalMap = importer.textureType == TextureImporterType.NormalMap;
            string compressionLabel = isNormalMap ? "BC5" : "BC7";

            double estimatedBytes = (double)effectiveWidth * effectiveHeight;
            if (isCube)
                estimatedBytes *= 6.0d;

            estimatedBytes *= hasMipMaps ? MipChainMultiplier : 1.0d;

            return new TextureVramEntry
            {
                AssetPath = assetPath,
                EstimatedBytes = (long)Math.Ceiling(estimatedBytes),
                Width = effectiveWidth,
                Height = effectiveHeight,
                CompressionLabel = compressionLabel,
                HasMipMaps = hasMipMaps
            };
        }

        private static int ResolveEffectiveDimension(int importedDimension, int importerMaxTextureSize)
        {
            int clampedImportedDimension = Mathf.Max(1, importedDimension);
            int maxSize = importerMaxTextureSize > 0 ? importerMaxTextureSize : clampedImportedDimension;
            return Mathf.Min(clampedImportedDimension, maxSize);
        }

        private static void EmitResult(AuditResult result)
        {
            float totalMegabytes = result.TotalEstimatedBytes / (1024f * 1024f);
            string header =
                $"[HectonArtVramAudit] Assets/_Project/Art compressed VRAM estimate: {totalMegabytes:F2} MB across {result.TextureCount} textures. " +
                $"Threshold: {WarningThresholdBytes / (1024f * 1024f):F0} MB.";

            if (result.TotalEstimatedBytes > WarningThresholdBytes)
                Debug.LogWarning(header);
            else
                Debug.Log(header);

            int loggedCount = Mathf.Min(result.Entries.Count, MaxLoggedTextures);
            for (int i = 0; i < loggedCount; i++)
            {
                TextureVramEntry entry = result.Entries[i];
                float entryMegabytes = entry.EstimatedBytes / (1024f * 1024f);
                Debug.Log(
                    $"[HectonArtVramAudit] Top VRAM #{i + 1}: {entry.AssetPath} | {entry.Width}x{entry.Height} | {entry.CompressionLabel} | " +
                    $"MipMaps={(entry.HasMipMaps ? "On" : "Off")} | ~{entryMegabytes:F2} MB");
            }
        }

        internal readonly struct AuditResult
        {
            public long TotalEstimatedBytes { get; }
            public int TextureCount { get; }
            public List<TextureVramEntry> Entries { get; }

            public AuditResult(long totalEstimatedBytes, int textureCount, List<TextureVramEntry> entries)
            {
                TotalEstimatedBytes = totalEstimatedBytes;
                TextureCount = textureCount;
                Entries = entries;
            }
        }
    }
}
#endif
