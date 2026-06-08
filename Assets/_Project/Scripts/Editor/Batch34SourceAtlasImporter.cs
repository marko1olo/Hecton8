using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Imports Batch34 source-only atlases with atlas-safe texture settings.
    /// Does not create Lit materials; source atlases require split/alpha/decal/UV binding first.
    /// </summary>
    public static class Batch34SourceAtlasImporter
    {
        private const string SourceAtlasManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json";
        private const string AlphaCandidateManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json";
        private const string PaddedAtlasManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34PaddedAtlasSources_20260608/GeminiBatch34PaddedAtlasSources_Manifest.json";
        private const string SplitAtlasManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/GeminiBatch34SplitAtlasCandidates_Manifest.json";

        [MenuItem("Hecton8/Art/Import Batch34 Source Atlases")]
        public static void ExecuteMenu()
        {
            ImportBatch34SourceAtlases();
        }

        public static void ImportBatch34SourceAtlases()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            SourceAtlasManifest manifest = LoadRequiredManifest<SourceAtlasManifest>(SourceAtlasManifestPath, "source atlas");
            if (manifest.entries == null || manifest.entries.Length == 0)
                throw new InvalidOperationException("[Batch34SourceAtlasImporter] Missing or empty source atlas manifest entries: " + SourceAtlasManifestPath);

            int imported = 0;
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                SourceAtlasEntry entry = manifest.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.source))
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Source atlas entry missing source path at index " + i);
                if (string.IsNullOrWhiteSpace(entry.id))
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Source atlas entry missing id at index " + i);

                ImportTexture(entry.id, entry.source, false);
                imported++;
            }

            ImportAlphaCandidates(ref imported);
            ImportPaddedAtlases(ref imported);
            ImportSplitAtlasCandidates(ref imported);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Batch34SourceAtlasImporter] Imported={imported}");
        }

        private static T LoadRequiredManifest<T>(string manifestPath, string label)
            where T : class
        {
            string normalizedManifestPath = NormalizeAssetPath(manifestPath);
            string projectFilePath = ResolveProjectFilePath(normalizedManifestPath);
            if (!File.Exists(projectFilePath))
                throw new InvalidOperationException("[Batch34SourceAtlasImporter] Missing " + label + " manifest: " + manifestPath);

            T manifest = JsonUtility.FromJson<T>(File.ReadAllText(projectFilePath));
            if (manifest == null)
                throw new InvalidOperationException("[Batch34SourceAtlasImporter] Could not parse " + label + " manifest: " + manifestPath);

            return manifest;
        }

        private static void ImportAlphaCandidates(ref int imported)
        {
            AlphaCandidateManifest manifest = LoadRequiredManifest<AlphaCandidateManifest>(AlphaCandidateManifestPath, "alpha candidate");
            if (manifest.entries == null || manifest.entries.Length == 0)
                throw new InvalidOperationException("[Batch34SourceAtlasImporter] Missing or empty alpha candidate manifest entries: " + AlphaCandidateManifestPath);

            for (int i = 0; i < manifest.entries.Length; i++)
            {
                AlphaCandidateEntry entry = manifest.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.alphaCandidate))
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Alpha candidate entry missing path at index " + i);
                if (string.IsNullOrWhiteSpace(entry.id))
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Alpha candidate entry missing id at index " + i);

                ImportTexture(entry.id, entry.alphaCandidate, true);
                imported++;
            }
        }

        private static void ImportPaddedAtlases(ref int imported)
        {
            PaddedAtlasManifest manifest = LoadRequiredManifest<PaddedAtlasManifest>(PaddedAtlasManifestPath, "padded atlas");
            if (manifest.entries == null || manifest.entries.Length == 0)
                throw new InvalidOperationException("[Batch34SourceAtlasImporter] Missing or empty padded atlas manifest entries: " + PaddedAtlasManifestPath);

            for (int i = 0; i < manifest.entries.Length; i++)
            {
                PaddedAtlasEntry entry = manifest.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.paddedAtlas))
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Padded atlas entry missing path at index " + i);
                if (string.IsNullOrWhiteSpace(entry.id))
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Padded atlas entry missing id at index " + i);

                ImportTexture(entry.id, entry.paddedAtlas, true);
                imported++;
            }
        }

        private static void ImportSplitAtlasCandidates(ref int imported)
        {
            SplitAtlasManifest manifest = LoadRequiredManifest<SplitAtlasManifest>(SplitAtlasManifestPath, "split atlas candidate");
            if (manifest.entries == null || manifest.entries.Length == 0)
                throw new InvalidOperationException("[Batch34SourceAtlasImporter] Missing or empty split atlas candidate manifest entries: " + SplitAtlasManifestPath);

            for (int i = 0; i < manifest.entries.Length; i++)
            {
                SplitAtlasEntry entry = manifest.entries[i];
                if (entry == null || entry.islands == null || entry.islands.Length == 0)
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Split atlas entry missing islands at index " + i);
                if (string.IsNullOrWhiteSpace(entry.id))
                    throw new InvalidOperationException("[Batch34SourceAtlasImporter] Split atlas entry missing id at index " + i);
                if (entry.islandCount != 0 && entry.islandCount != entry.islands.Length)
                    throw new InvalidOperationException($"[Batch34SourceAtlasImporter] Split atlas island count mismatch for {entry.id}: declared={entry.islandCount} actual={entry.islands.Length}");

                for (int islandIndex = 0; islandIndex < entry.islands.Length; islandIndex++)
                {
                    SplitAtlasIsland island = entry.islands[islandIndex];
                    if (island == null || string.IsNullOrWhiteSpace(island.path))
                        throw new InvalidOperationException($"[Batch34SourceAtlasImporter] Split atlas island entry missing path for {entry.id} at index {islandIndex}");
                    if (island.index != islandIndex)
                        throw new InvalidOperationException($"[Batch34SourceAtlasImporter] Split atlas island index drift for {entry.id}: expected={islandIndex} actual={island.index}");

                    ImportTexture(entry.id + "_island_" + islandIndex.ToString("D2"), island.path, true);
                    imported++;
                }
            }
        }

        private static void ImportTexture(string id, string source, bool alphaIsTransparency)
        {
            string sourcePath = NormalizeAssetPath(source);
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                !IsProjectAssetPath(sourcePath) ||
                !File.Exists(ResolveProjectFilePath(sourcePath)))
            {
                throw new InvalidOperationException($"[Batch34SourceAtlasImporter] Missing source atlas texture for {id}: {source}");
            }

            TextureImporter importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"[Batch34SourceAtlasImporter] Missing TextureImporter for {id}: {sourcePath}");

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = alphaIsTransparency;
            SetPlatformSettings(importer, "Standalone", 2048, TextureImporterFormat.BC7);
            SetPlatformSettings(importer, "Android", 2048, TextureImporterFormat.ASTC_6x6);
            SetPlatformSettings(importer, "iPhone", 2048, TextureImporterFormat.ASTC_6x6);
            importer.SaveAndReimport();
        }

        private static void SetPlatformSettings(
            TextureImporter importer,
            string platform,
            int maxTextureSize,
            TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = maxTextureSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
        }

        private static bool IsProjectAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) || path == "Assets";
        }

        private static string ResolveProjectFilePath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
                return assetPath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        [Serializable]
        private sealed class SourceAtlasManifest
        {
            public SourceAtlasEntry[] entries;
        }

        [Serializable]
        private sealed class AlphaCandidateManifest
        {
            public AlphaCandidateEntry[] entries;
        }

        [Serializable]
        private sealed class PaddedAtlasManifest
        {
            public PaddedAtlasEntry[] entries;
        }

        [Serializable]
        private sealed class SplitAtlasManifest
        {
            public SplitAtlasEntry[] entries;
        }

        [Serializable]
        private sealed class SourceAtlasEntry
        {
            public string id;
            public string source;
        }

        [Serializable]
        private sealed class AlphaCandidateEntry
        {
            public string id;
            public string alphaCandidate;
        }

        [Serializable]
        private sealed class PaddedAtlasEntry
        {
            public string id;
            public string paddedAtlas;
        }

        [Serializable]
        private sealed class SplitAtlasEntry
        {
            public string id;
            public int islandCount;
            public SplitAtlasIsland[] islands;
        }

        [Serializable]
        private sealed class SplitAtlasIsland
        {
            public int index;
            public string path;
        }
    }
}
