#if UNITY_EDITOR
using System;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Enforces MX350 texture import policy on first-party art assets at import time.
    /// </summary>
    internal sealed class HectonTextureImportDictator : AssetPostprocessor
    {
        internal const string ArtRoot = "Assets/_Project/Art";
        internal const int HeroMaxTextureSize = 2048;
        internal const int ScatterMaxTextureSize = 512;
        internal const string TierHighLabel = "Tier_High";
        internal const string TierLowLabel = "Tier_Low";
        private const string TieredTextureGroupName = "Hecton_TextureStreaming_Auto";
        private const string SyncTierLabelsMenuPath = "Hecton/Art Optimization/Sync Texture Addressables Tier Labels";

        private void OnPreprocessTexture()
        {
            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null || !IsManagedArtTexture(assetPath))
                return;

            ApplyImportPolicy(importer, assetPath);
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            TextureImporter importer = assetImporter as TextureImporter;
            if (texture == null ||
                importer == null ||
                !IsManagedArtTexture(assetPath))
            {
                return;
            }

            if (!IsNormalMap(assetPath, importer) ||
                !ShouldFlipNormalGreenChannel(assetPath, texture))
            {
                return;
            }

            if (!TryGetRawColor32(texture, out NativeArray<Color32> pixels))
                return;

            FlipGreenChannel(pixels);

            texture.Apply(false, false);
        }

        [MenuItem(SyncTierLabelsMenuPath, priority = 211)]
        private static void SyncTextureTierLabels()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            int labeled = 0;

            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (texture == null || importer == null || !IsManagedArtTexture(path))
                    continue;

                if (ApplyAddressablesTierLabel(texture, path, importer))
                    labeled++;
            }

            Debug.Log("[HectonTextureImportDictator] Synced Addressables tier labels for textures=" + labeled + ".");
        }

        internal static bool ApplyImportPolicy(TextureImporter importer, string path)
        {
            if (importer == null)
                return false;

            bool changed = false;
            bool normalMap = IsNormalMap(path, importer);
            bool maskMap = IsMaskMap(path);
            bool uiTexture = IsUiTexture(path, importer);
            int maxTextureSize = ResolveMaxTextureSize(path);
            TextureImporterFormat format = normalMap ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;

            if (normalMap && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            else if (!normalMap && importer.textureType != TextureImporterType.Default && !uiTexture)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            bool desiredSrgb = !normalMap && !maskMap;
            if (importer.sRGBTexture != desiredSrgb)
            {
                importer.sRGBTexture = desiredSrgb;
                changed = true;
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Compressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                changed = true;
            }

            if (importer.crunchedCompression)
            {
                importer.crunchedCompression = false;
                changed = true;
            }

            bool mipMaps = !uiTexture;
            if (importer.mipmapEnabled != mipMaps)
            {
                importer.mipmapEnabled = mipMaps;
                changed = true;
            }

            if (importer.maxTextureSize != maxTextureSize)
            {
                importer.maxTextureSize = maxTextureSize;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.ToNearest)
            {
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                changed = true;
            }

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            if (!standalone.overridden ||
                standalone.format != format ||
                standalone.maxTextureSize != maxTextureSize ||
                standalone.textureCompression != TextureImporterCompression.Compressed)
            {
                standalone.overridden = true;
                standalone.format = format;
                standalone.maxTextureSize = maxTextureSize;
                standalone.textureCompression = TextureImporterCompression.Compressed;
                standalone.crunchedCompression = false;
                importer.SetPlatformTextureSettings(standalone);
                changed = true;
            }

            return changed;
        }

        internal static int ResolveMaxTextureSize(string path)
        {
            return IsScatterTexture(path) ? ScatterMaxTextureSize : HeroMaxTextureSize;
        }

        internal static bool IsNormalMap(string path, TextureImporter importer)
        {
            if (importer != null && importer.textureType == TextureImporterType.NormalMap)
                return true;

            string lowerPath = Normalize(path);
            return ContainsOrdinal(lowerPath, "normal") ||
                   ContainsOrdinal(lowerPath, "_n.") ||
                   ContainsOrdinal(lowerPath, "_n_") ||
                   ContainsOrdinal(lowerPath, "nrm");
        }

        private static bool IsManagedArtTexture(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith(ArtRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            string lowerPath = normalizedPath.ToLowerInvariant();
            return lowerPath.EndsWith(".png", StringComparison.Ordinal) ||
                   lowerPath.EndsWith(".jpg", StringComparison.Ordinal) ||
                   lowerPath.EndsWith(".jpeg", StringComparison.Ordinal) ||
                   lowerPath.EndsWith(".tga", StringComparison.Ordinal) ||
                   lowerPath.EndsWith(".tif", StringComparison.Ordinal) ||
                   lowerPath.EndsWith(".tiff", StringComparison.Ordinal) ||
                   lowerPath.EndsWith(".psd", StringComparison.Ordinal) ||
                   lowerPath.EndsWith(".exr", StringComparison.Ordinal);
        }

        private static bool ApplyAddressablesTierLabel(Texture2D texture, string path, TextureImporter importer)
        {
            if (texture == null || importer == null || string.IsNullOrEmpty(path))
                return false;

            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            if (!TryResolveTierLabel(path, sourceWidth, sourceHeight, out string label))
                return ClearAddressablesTierLabels(path);

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
                return false;

            AddressableAssetGroup group = ResolveTieredTextureGroup(settings);
            if (group == null)
                return false;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return false;

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
                return false;

            bool changed = false;
            if (!string.Equals(entry.address, path, StringComparison.Ordinal))
            {
                entry.address = path;
                changed = true;
            }

            string otherLabel = string.Equals(label, TierHighLabel, StringComparison.Ordinal)
                ? TierLowLabel
                : TierHighLabel;
            changed |= entry.SetLabel(label, true, true, false);
            changed |= entry.SetLabel(otherLabel, false, true, false);

            if (changed)
            {
                EditorUtility.SetDirty(settings);
            }

            return changed;
        }

        private static bool ClearAddressablesTierLabels(string path)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return false;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return false;

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null)
                return false;

            bool changed = entry.SetLabel(TierHighLabel, false, false, false);
            changed |= entry.SetLabel(TierLowLabel, false, false, false);
            if (changed)
            {
                EditorUtility.SetDirty(settings);
            }

            return changed;
        }

        private static bool TryResolveTierLabel(string path, int width, int height, out string label)
        {
            label = null;
            int maxDimension = Mathf.Max(width, height);
            if (maxDimension >= HeroMaxTextureSize)
            {
                label = TierHighLabel;
                return true;
            }

            if (maxDimension <= ScatterMaxTextureSize && IsAtlasTexture(path))
            {
                label = TierLowLabel;
                return true;
            }

            return false;
        }

        private static AddressableAssetGroup ResolveTieredTextureGroup(AddressableAssetSettings settings)
        {
            if (settings == null)
                return null;

            AddressableAssetGroup group = settings.FindGroup(TieredTextureGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    TieredTextureGroupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(BundledAssetGroupSchema));
            }

            ConfigureBundledLoadMode(group);
            return group;
        }

        private static void ConfigureBundledLoadMode(AddressableAssetGroup group)
        {
            BundledAssetGroupSchema schema = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
            if (schema == null)
                return;

            if (schema.AssetLoadMode != AssetLoadMode.RequestedAssetAndDependencies)
            {
                schema.AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;
                EditorUtility.SetDirty(group);
            }
        }

        private static bool IsMaskMap(string path)
        {
            string lowerPath = Normalize(path);
            return ContainsOrdinal(lowerPath, "mask") ||
                   ContainsOrdinal(lowerPath, "detail") ||
                   ContainsOrdinal(lowerPath, "metal") ||
                   ContainsOrdinal(lowerPath, "rough") ||
                   ContainsOrdinal(lowerPath, "smooth") ||
                   ContainsOrdinal(lowerPath, "occlusion") ||
                   ContainsOrdinal(lowerPath, "_ao") ||
                   ContainsOrdinal(lowerPath, "ambient") ||
                   ContainsOrdinal(lowerPath, "height") ||
                   ContainsOrdinal(lowerPath, "emissive");
        }

        private static bool IsUiTexture(string path, TextureImporter importer)
        {
            if (importer != null && importer.textureType == TextureImporterType.Sprite)
                return true;

            string lowerPath = Normalize(path);
            return ContainsOrdinal(lowerPath, "/sprites/") ||
                   ContainsOrdinal(lowerPath, "/ui/") ||
                   ContainsOrdinal(lowerPath, "/icons/");
        }

        private static bool IsScatterTexture(string path)
        {
            string lowerPath = Normalize(path);
            return ContainsOrdinal(lowerPath, "scatter") ||
                   ContainsOrdinal(lowerPath, "flora") ||
                   ContainsOrdinal(lowerPath, "coral") ||
                   ContainsOrdinal(lowerPath, "kelp") ||
                   ContainsOrdinal(lowerPath, "rock") ||
                   ContainsOrdinal(lowerPath, "rocks") ||
                   ContainsOrdinal(lowerPath, "gravel") ||
                   ContainsOrdinal(lowerPath, "sand") ||
                   ContainsOrdinal(lowerPath, "moss") ||
                   ContainsOrdinal(lowerPath, "mud") ||
                   ContainsOrdinal(lowerPath, "basalt") ||
                   ContainsOrdinal(lowerPath, "debris") ||
                   ContainsOrdinal(lowerPath, "terrain textures") ||
                   ContainsOrdinal(lowerPath, "worldproceduralflora");
        }

        private static bool IsAtlasTexture(string path)
        {
            string lowerPath = Normalize(path);
            return ContainsOrdinal(lowerPath, "atlas") ||
                   ContainsOrdinal(lowerPath, "sheet") ||
                   ContainsOrdinal(lowerPath, "flipbook");
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').ToLowerInvariant();
        }

        private static bool ShouldFlipNormalGreenChannel(string path, Texture2D texture)
        {
            string lowerPath = Normalize(path);
            if (ContainsOrdinal(lowerPath, "normalgl") ||
                ContainsOrdinal(lowerPath, "opengl") ||
                ContainsOrdinal(lowerPath, "_gl.") ||
                ContainsOrdinal(lowerPath, "_gl_"))
            {
                return false;
            }

            if (ContainsOrdinal(lowerPath, "normaldx") ||
                ContainsOrdinal(lowerPath, "directx") ||
                ContainsOrdinal(lowerPath, "_dx.") ||
                ContainsOrdinal(lowerPath, "_dx_") ||
                ContainsOrdinal(lowerPath, "-dx") ||
                ContainsOrdinal(lowerPath, "y-") ||
                ContainsOrdinal(lowerPath, "green_inverted") ||
                ContainsOrdinal(lowerPath, "green-inverted"))
            {
                return true;
            }

            return HasInvertedGreenHistogram(texture);
        }

        private static bool ContainsOrdinal(string source, string token)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        private static bool HasInvertedGreenHistogram(Texture2D texture)
        {
            if (!TryGetRawColor32(texture, out NativeArray<Color32> pixels) || pixels.Length <= 0)
                return false;

            int stride = Mathf.Max(1, pixels.Length / 32768);
            int sampleCount = 0;
            int lowerTail = 0;
            int upperTail = 0;
            long greenSum = 0L;

            for (int i = 0; i < pixels.Length; i += stride)
            {
                byte green = pixels[i].g;
                greenSum += green;
                sampleCount++;
                if (green < 64)
                    lowerTail++;
                else if (green > 191)
                    upperTail++;
            }

            if (sampleCount <= 0)
                return false;

            float averageGreen = greenSum / (float)sampleCount;
            return averageGreen < 126f && lowerTail > upperTail * 1.2f;
        }

        private static void FlipGreenChannel(NativeArray<Color32> pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                pixel.g = (byte)(255 - pixel.g);
                pixels[i] = pixel;
            }
        }

        private static bool TryGetRawColor32(Texture2D texture, out NativeArray<Color32> pixels)
        {
            pixels = default;
            if (texture == null)
                return false;

            TextureFormat format = texture.format;
            if (format != TextureFormat.RGBA32 &&
                format != TextureFormat.ARGB32 &&
                format != TextureFormat.BGRA32)
            {
                return false;
            }

            pixels = texture.GetRawTextureData<Color32>();
            return pixels.Length == texture.width * texture.height;
        }
    }
}
#endif
