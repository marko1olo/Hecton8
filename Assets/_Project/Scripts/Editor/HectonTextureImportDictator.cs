#if UNITY_EDITOR
using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

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
                !IsManagedArtTexture(assetPath) ||
                !IsNormalMap(assetPath, importer) ||
                !ShouldFlipNormalGreenChannel(assetPath, texture))
            {
                return;
            }

            if (!TryGetRawColor32(texture, out NativeArray<Color32> pixels))
                return;

            FlipGreenChannel(pixels);

            texture.Apply(false, false);
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
            return lowerPath.Contains("normal") ||
                   lowerPath.Contains("_n.") ||
                   lowerPath.Contains("_n_") ||
                   lowerPath.Contains("nrm");
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

        private static bool IsMaskMap(string path)
        {
            string lowerPath = Normalize(path);
            return lowerPath.Contains("mask") ||
                   lowerPath.Contains("detail") ||
                   lowerPath.Contains("metal") ||
                   lowerPath.Contains("rough") ||
                   lowerPath.Contains("smooth") ||
                   lowerPath.Contains("occlusion") ||
                   lowerPath.Contains("_ao") ||
                   lowerPath.Contains("ambient") ||
                   lowerPath.Contains("height") ||
                   lowerPath.Contains("emissive");
        }

        private static bool IsUiTexture(string path, TextureImporter importer)
        {
            if (importer != null && importer.textureType == TextureImporterType.Sprite)
                return true;

            string lowerPath = Normalize(path);
            return lowerPath.Contains("/sprites/") ||
                   lowerPath.Contains("/ui/") ||
                   lowerPath.Contains("/icons/");
        }

        private static bool IsScatterTexture(string path)
        {
            string lowerPath = Normalize(path);
            return lowerPath.Contains("scatter") ||
                   lowerPath.Contains("flora") ||
                   lowerPath.Contains("coral") ||
                   lowerPath.Contains("kelp") ||
                   lowerPath.Contains("rock") ||
                   lowerPath.Contains("rocks") ||
                   lowerPath.Contains("gravel") ||
                   lowerPath.Contains("sand") ||
                   lowerPath.Contains("moss") ||
                   lowerPath.Contains("mud") ||
                   lowerPath.Contains("basalt") ||
                   lowerPath.Contains("debris") ||
                   lowerPath.Contains("terrain textures") ||
                   lowerPath.Contains("worldproceduralflora");
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
            if (lowerPath.Contains("normalgl") ||
                lowerPath.Contains("opengl") ||
                lowerPath.Contains("_gl.") ||
                lowerPath.Contains("_gl_"))
            {
                return false;
            }

            if (lowerPath.Contains("normaldx") ||
                lowerPath.Contains("directx") ||
                lowerPath.Contains("_dx.") ||
                lowerPath.Contains("_dx_") ||
                lowerPath.Contains("-dx") ||
                lowerPath.Contains("y-") ||
                lowerPath.Contains("green_inverted") ||
                lowerPath.Contains("green-inverted"))
            {
                return true;
            }

            return HasInvertedGreenHistogram(texture);
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
