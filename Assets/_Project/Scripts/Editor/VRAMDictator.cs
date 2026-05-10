#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Build-time texture gate for the MX350 art budget.
    /// </summary>
    internal sealed class VRAMDictator : IPreprocessBuildWithReport
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Run VRAM Dictator";
        private const int MaxReportRows = 96;

        public int callbackOrder => -2048;

        public void OnPreprocessBuild(BuildReport report)
        {
            DictatorResult result = Scan();
            if (result.BlockingViolationCount <= 0)
                return;

            throw new BuildFailedException(result.BuildFailureMessage);
        }

        [MenuItem(MenuPath, priority = 197)]
        private static void RunFromMenu()
        {
            DictatorResult result = Scan();
            if (result.BlockingViolationCount > 0)
                throw new BuildFailedException(result.BuildFailureMessage);

            Debug.Log(result.BuildFailureMessage);
        }

        internal static DictatorResult Scan()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture", new[] { ArtRoot });
            StringBuilder blockingRows = new StringBuilder(8192); // COLD ALLOC: StringBuilder[8192] - editor/build-only VRAM failure report - owner: VRAMDictator
            StringBuilder auditRows = new StringBuilder(8192); // COLD ALLOC: StringBuilder[8192] - editor/build-only BC format audit report - owner: VRAMDictator
            int scanned = 0;
            int blockingCount = 0;
            int nonBc7Count = 0;
            int normalNotBc5Count = 0;
            int runtimeFormatViolationCount = 0;
            int uncompressedCount = 0;
            int oversizedNonAtlasCount = 0;
            int blockingRowsWritten = 0;
            int auditRowsWritten = 0;

            for (int i = 0; i < textureGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                scanned++;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                string formatLabel = ResolveFormatLabel(importer, standalone);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                string runtimeFormatLabel = texture != null ? texture.format.ToString() : "unloaded";
                bool normalMap = IsNormalMap(assetPath, importer);
                bool atlas = IsAtlasTexturePath(assetPath);
                int expectedMaxSize = HectonTextureImportDictator.ResolveMaxTextureSize(assetPath);
                bool oversizedNonAtlas = (width > expectedMaxSize || height > expectedMaxSize) && !atlas;
                bool uncompressedRgba = IsUncompressedRgba(formatLabel, importer);
                bool nonBc7 = !normalMap && !formatLabel.Contains("BC7");
                bool normalNotBc5 = normalMap && !formatLabel.Contains("BC5");
                bool runtimeFormatViolation = texture != null && !IsExpectedRuntimeFormat(texture.format, normalMap);

                if (oversizedNonAtlas)
                    oversizedNonAtlasCount++;
                if (uncompressedRgba)
                    uncompressedCount++;
                if (nonBc7)
                    nonBc7Count++;
                if (normalNotBc5)
                    normalNotBc5Count++;
                if (runtimeFormatViolation)
                    runtimeFormatViolationCount++;

                if (oversizedNonAtlas || uncompressedRgba || nonBc7 || normalNotBc5 || runtimeFormatViolation)
                {
                    blockingCount++;
                    if (blockingRowsWritten < MaxReportRows)
                    {
                        AppendTextureRow(blockingRows, assetPath, width, height, formatLabel, runtimeFormatLabel, normalMap, atlas);
                        if (oversizedNonAtlas)
                            blockingRows.Append(" | oversizedNonAtlas");
                        if (uncompressedRgba)
                            blockingRows.Append(" | uncompressedRGBA");
                        if (nonBc7)
                            blockingRows.Append(" | nonBC7");
                        if (normalNotBc5)
                            blockingRows.Append(" | normalNotBC5");
                        if (runtimeFormatViolation)
                            blockingRows.Append(" | runtimeFormatNotBC7BC5");
                        blockingRows.AppendLine();
                        blockingRowsWritten++;
                    }
                }

                if ((nonBc7 || normalNotBc5) && auditRowsWritten < MaxReportRows)
                {
                    AppendTextureRow(auditRows, assetPath, width, height, formatLabel, runtimeFormatLabel, normalMap, atlas);
                    if (nonBc7)
                        auditRows.Append(" | nonBC7");
                    if (normalNotBc5)
                        auditRows.Append(" | normalNotBC5");
                    auditRows.AppendLine();
                    auditRowsWritten++;
                }
            }

            string message = BuildMessage(
                scanned,
                blockingCount,
                oversizedNonAtlasCount,
                uncompressedCount,
                nonBc7Count,
                normalNotBc5Count,
                runtimeFormatViolationCount,
                blockingRows.ToString(),
                auditRows.ToString());
            return new DictatorResult(scanned, blockingCount, message);
        }

        private static string ResolveFormatLabel(TextureImporter importer, TextureImporterPlatformSettings platformSettings)
        {
            if (platformSettings != null && platformSettings.overridden)
                return platformSettings.format.ToString();

            return importer.textureCompression.ToString();
        }

        private static bool IsNormalMap(string assetPath, TextureImporter importer)
        {
            if (importer.textureType == TextureImporterType.NormalMap)
                return true;

            string lowerPath = assetPath.ToLowerInvariant();
            return lowerPath.Contains("normal") || lowerPath.Contains("_n.") || lowerPath.Contains("_n_") || lowerPath.Contains("nrm");
        }

        private static bool IsAtlasTexturePath(string assetPath)
        {
            string lowerPath = assetPath.ToLowerInvariant();
            return lowerPath.Contains("atlas") || lowerPath.Contains("sheet") || lowerPath.Contains("flipbook");
        }

        private static bool IsUncompressedRgba(string formatLabel, TextureImporter importer)
        {
            if (formatLabel == "R8" || formatLabel == "Alpha8")
                return false;

            if (importer.textureCompression == TextureImporterCompression.Uncompressed && formatLabel == "Uncompressed")
                return true;

            return formatLabel.Contains("RGBA32") ||
                   formatLabel.Contains("ARGB32") ||
                   formatLabel.Contains("RGB24") ||
                   formatLabel.Contains("RGBAHalf") ||
                   formatLabel.Contains("RGBAFloat") ||
                   formatLabel.Contains("R8G8B8A8") ||
                   formatLabel.Contains("R16G16B16A16");
        }

        private static bool IsExpectedRuntimeFormat(TextureFormat format, bool normalMap)
        {
            return normalMap ? format == TextureFormat.BC5 : format == TextureFormat.BC7;
        }

        private static void AppendTextureRow(
            StringBuilder builder,
            string assetPath,
            int width,
            int height,
            string formatLabel,
            string runtimeFormatLabel,
            bool normalMap,
            bool atlas)
        {
            builder.Append(assetPath)
                .Append(" | ")
                .Append(width)
                .Append('x')
                .Append(height)
                .Append(" | ")
                .Append(formatLabel)
                .Append(" | texture.format=")
                .Append(runtimeFormatLabel)
                .Append(normalMap ? " | normal" : " | color-mask")
                .Append(atlas ? " | atlas" : " | non-atlas");
        }

        private static string BuildMessage(
            int scanned,
            int blockingCount,
            int oversizedNonAtlasCount,
            int uncompressedCount,
            int nonBc7Count,
            int normalNotBc5Count,
            int runtimeFormatViolationCount,
            string blockingRows,
            string auditRows)
        {
            StringBuilder message = new StringBuilder(12288); // COLD ALLOC: StringBuilder[12288] - editor/build-only VRAM gate message - owner: VRAMDictator
            message.Append("[VRAMDictator] Assets/_Project/Art texture scan: scanned=")
                .Append(scanned)
                .Append(" blocking=")
                .Append(blockingCount)
                .Append(" oversizedNonAtlas=")
                .Append(oversizedNonAtlasCount)
                .Append(" uncompressedRGBA=")
                .Append(uncompressedCount)
                .Append(" nonBC7(non-normal audit)=")
                .Append(nonBc7Count)
                .Append(" normalNotBC5(audit)=")
                .Append(normalNotBc5Count)
                .Append(" runtimeFormatNotBC7BC5=")
                .Append(runtimeFormatViolationCount)
                .AppendLine(".");

            if (blockingCount > 0)
            {
                message.Append("BUILD BLOCKED. Non-atlas textures must obey Hero<=2048 and Scatter<=512 import caps; RGB/RGBA imports must not be uncompressed; albedo/mask runtime format must be BC7 and normal runtime format must be BC5.")
                    .AppendLine()
                    .Append(blockingRows);
            }
            else
            {
                message.Append("No blocking VRAM violations found. BC format audit follows.").AppendLine();
            }

            if (!string.IsNullOrEmpty(auditRows))
            {
                message.Append("BC audit rows: non-normal assets should be BC7; normal maps should be BC5.")
                    .AppendLine()
                    .Append(auditRows);
            }

            return message.ToString();
        }

        internal readonly struct DictatorResult
        {
            public int ScannedTextureCount { get; }
            public int BlockingViolationCount { get; }
            public string BuildFailureMessage { get; }

            public DictatorResult(int scannedTextureCount, int blockingViolationCount, string buildFailureMessage)
            {
                ScannedTextureCount = scannedTextureCount;
                BlockingViolationCount = blockingViolationCount;
                BuildFailureMessage = buildFailureMessage;
            }
        }
    }
}
#endif
