using System;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.Bakers
{
    public static partial class ProceduralTextureBaker
    {
        public const string ComputeShaderPath = "Assets/_Project/Art/Shaders/Bakers/Hecton_ProceduralBaker.compute";
        public const string DefaultOutputRoot = "Assets/_Project/Art/Generated/TextureBaker1605";

        private const int MinimumBakeSize = 512;
        private const int MaximumBakeSize = 4096;
        private const int CompactVramMegabytes = 2048;
        private const long MaxEncodedPngBytes = 128L * 1024L * 1024L;
        private const long MaxRollbackAssetBytes = 256L * 1024L * 1024L;
        private const long MaxRollbackMetaBytes = 4L * 1024L * 1024L;
        private const string AtomicWriteTempExtension = ".tmp1605";
        private const string AtomicWriteBackupExtension = ".bak1605";

        private static readonly int s_outputId = Shader.PropertyToID("_BakerOutput");
        private static readonly int s_textureSizeId = Shader.PropertyToID("_BakerTextureSize");
        private static readonly int s_seedId = Shader.PropertyToID("_BakerSeed");
        private static readonly int s_colorAId = Shader.PropertyToID("_BakerColorA");
        private static readonly int s_colorBId = Shader.PropertyToID("_BakerColorB");
        private static readonly int s_colorCId = Shader.PropertyToID("_BakerColorC");
        private static readonly int s_noiseParamsId = Shader.PropertyToID("_BakerNoiseParams");
        private static readonly int s_surfaceParamsId = Shader.PropertyToID("_BakerSurfaceParams");
        private static readonly int s_qualityParamsId = Shader.PropertyToID("_BakerQualityParams");
        private static readonly Regex s_highCompressionRegex = new Regex(@"textureCompression:\s*[23]\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex s_linearTextureRegex = new Regex(@"sRGBTexture:\s*0\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex s_srgbTextureRegex = new Regex(@"sRGBTexture:\s*1\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex s_bc7FormatRegex = new Regex(@"textureFormat:\s*(25|50)\b|BC7", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex s_bc5FormatRegex = new Regex(@"textureFormat:\s*(27|48)\b|BC5", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex s_androidPlatformRegex = new Regex(@"buildTarget:\s*Android\b|name:\s*Android\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex s_iPhonePlatformRegex = new Regex(@"buildTarget:\s*iPhone\b|name:\s*iPhone\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public enum TextureRole
        {
            Albedo,
            Normal,
            Mask
        }

        public readonly struct GeneratedTextureSet
        {
            public readonly string AlbedoPath;
            public readonly string NormalPath;
            public readonly string MaskPath;
            public readonly int TextureSize;

            public GeneratedTextureSet(string albedoPath, string normalPath, string maskPath, int textureSize)
            {
                AlbedoPath = albedoPath;
                NormalPath = normalPath;
                MaskPath = maskPath;
                TextureSize = textureSize;
            }
        }

        internal readonly struct AssetFileRollbackSnapshot
        {
            public readonly bool Captured;
            public readonly string AssetPath;
            public readonly string AbsolutePath;
            public readonly bool HadAssetFile;
            public readonly byte[] AssetBytes;
            public readonly bool HadMetaFile;
            public readonly byte[] MetaBytes;

            public AssetFileRollbackSnapshot(
                string assetPath,
                string absolutePath,
                bool hadAssetFile,
                byte[] assetBytes,
                bool hadMetaFile,
                byte[] metaBytes)
            {
                Captured = true;
                AssetPath = assetPath;
                AbsolutePath = absolutePath;
                HadAssetFile = hadAssetFile;
                AssetBytes = assetBytes;
                HadMetaFile = hadMetaFile;
                MetaBytes = metaBytes;
            }
        }

        [MenuItem("HECTON-8/Bakers/1605/Bake Default PBR Seed Pack", false, 205)]
        public static void BakeDefaultPbrSeedPack()
        {
            BakeProfileDTO[] profiles =
            {
                BakeProfileDTO.CoralAbyssal(1605001u),
                BakeProfileDTO.BasaltMineral(1605002u),
                BakeProfileDTO.RustedIndustrial(1605003u)
            };

            for (int i = 0; i < profiles.Length; i++)
            {
                if (!TryBakeProfile(in profiles[i], DefaultOutputRoot, out GeneratedTextureSet _))
                    return;
            }

            if (!TryFinalizeAssetDatabase("default seed pack bake", out string finalizeFailure))
                Debug.LogError("[TextureBaker1605] " + finalizeFailure);
        }

        internal static bool TryFinalizeAssetDatabase(string operationName, out string failure)
        {
            failure = string.Empty;
            string safeOperationName = string.IsNullOrEmpty(operationName) ? "asset transaction" : operationName;
            try
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                failure = safeOperationName + " finalize failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static bool TryBakeProfile(in BakeProfileDTO profile, string outputFolder, out GeneratedTextureSet generated)
        {
            generated = default;

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError("[TextureBaker1605] Compute shaders unsupported. Bake aborted.");
                return false;
            }

            ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath);
            if (compute == null)
            {
                Debug.LogError("[TextureBaker1605] Missing compute shader at " + ComputeShaderPath);
                return false;
            }

            int safeSize = ResolveSafeTextureSize(profile.TextureSize, profile.GlobalQualityWeight);
            if (safeSize < MinimumBakeSize)
            {
                Debug.LogError("[TextureBaker1605] Insufficient VRAM for minimum bake size.");
                return false;
            }

            if (!TryEnsureAssetFolder(outputFolder, out string normalizedOutputFolder, out string folderFailure))
            {
                Debug.LogError("[TextureBaker1605] Invalid output folder: " + folderFailure);
                return false;
            }

            string prefix = SanitizeAssetNameForPath(profile.ProfileName);
            if (string.IsNullOrEmpty(prefix))
                prefix = BuildSeedFallbackName(profile.Seed);

            string albedoPath = normalizedOutputFolder + "/TX_" + prefix + "_Albedo.png";
            string normalPath = normalizedOutputFolder + "/TX_" + prefix + "_Normal.png";
            string maskPath = normalizedOutputFolder + "/TX_" + prefix + "_MRAO.png";
            bool albedoExisted = AssetPathExistsNoThrow(albedoPath);
            bool normalExisted = AssetPathExistsNoThrow(normalPath);
            bool maskExisted = AssetPathExistsNoThrow(maskPath);
            if (!TryCaptureAssetFileRollbackSnapshots(albedoPath, normalPath, maskPath, out AssetFileRollbackSnapshot[] outputRollback, out string rollbackFailure))
            {
                Debug.LogError("[TextureBaker1605] Output rollback capture failed: " + rollbackFailure);
                return false;
            }

            if (!TryDispatchAndWrite(compute, "GenerateAlbedo", in profile, safeSize, TextureRole.Albedo, albedoPath))
            {
                TryRestoreAssetFileRollbackSnapshots(outputRollback);
                TryDeleteCreatedBakeOutputs(albedoPath, normalPath, maskPath, albedoExisted, normalExisted, maskExisted);
                return false;
            }

            if (!TryDispatchAndWrite(compute, "GenerateNormal", in profile, safeSize, TextureRole.Normal, normalPath))
            {
                TryRestoreAssetFileRollbackSnapshots(outputRollback);
                TryDeleteCreatedBakeOutputs(albedoPath, normalPath, maskPath, albedoExisted, normalExisted, maskExisted);
                return false;
            }

            if (!TryDispatchAndWrite(compute, "GenerateMask", in profile, safeSize, TextureRole.Mask, maskPath))
            {
                TryRestoreAssetFileRollbackSnapshots(outputRollback);
                TryDeleteCreatedBakeOutputs(albedoPath, normalPath, maskPath, albedoExisted, normalExisted, maskExisted);
                return false;
            }

            generated = new GeneratedTextureSet(albedoPath, normalPath, maskPath, safeSize);
            return true;
        }

        public static int ResolveSafeTextureSize(int requestedSize)
        {
            return ResolveSafeTextureSize(requestedSize, 1f);
        }

        public static int ResolveSafeTextureSize(int requestedSize, float globalQualityWeight)
        {
            float quality = Mathf.Clamp01(globalQualityWeight);
            int maximumRequestedSize = Mathf.Clamp(NextPowerOfTwo(requestedSize), MinimumBakeSize, MaximumBakeSize);
            int minimumPower = ToPowerOfTwoExponent(MinimumBakeSize);
            int maximumPower = ToPowerOfTwoExponent(maximumRequestedSize);
            int selectedPower = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(minimumPower, maximumPower, quality) + 0.5f), minimumPower, maximumPower);
            int size = 1 << selectedPower;
            if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= CompactVramMegabytes && size > 2048)
            {
                Debug.LogWarning("[TextureBaker1605] Insufficient compact VRAM for 4K bake; reducing to 2K.");
                size = 2048;
            }

            return size;
        }

        public static int ResolveSafeVariant(HectonBakeVariant variant)
        {
            return Mathf.Clamp((int)variant, (int)HectonBakeVariant.Organic, (int)HectonBakeVariant.Industrial);
        }

        public static bool VerifyMraoChannels(Texture2D maskTexture, out string failure)
        {
            if (!TryCollectMraoStats(maskTexture, out MraoStats stats, out failure))
                return false;

            bool roughnessPresent = stats.MaxG;
            bool ambientOcclusionPresent = stats.MaxB;
            bool independentChannels = stats.AnyRgbDivergence;
            if (roughnessPresent && ambientOcclusionPresent && independentChannels)
                return true;

            failure = "roughnessPresent=" + roughnessPresent +
                      " ambientOcclusionPresent=" + ambientOcclusionPresent +
                      " independentChannels=" + independentChannels +
                      " maxR=" + stats.MaxR +
                      " maxG=" + stats.MaxG +
                      " maxB=" + stats.MaxB +
                      " maxA=" + stats.MaxA;
            return false;
        }

        public static bool VerifyMraoChannels(Texture2D maskTexture, in BakeProfileDTO profile, out string failure)
        {
            if (!TryCollectMraoStats(maskTexture, out MraoStats stats, out failure))
                return false;

            bool metallicMatchesProfile = profile.Metallic <= 0.001f || stats.MaxR;
            bool roughnessPresent = stats.MaxG;
            bool ambientOcclusionPresent = stats.MaxB;
            bool emissiveMatchesProfile = profile.Emissive <= 0.001f || stats.MaxA;
            bool independentChannels = stats.AnyRgbDivergence;
            if (metallicMatchesProfile && roughnessPresent && ambientOcclusionPresent && emissiveMatchesProfile && independentChannels)
                return true;

            failure = "metallicMatchesProfile=" + metallicMatchesProfile +
                      " roughnessPresent=" + roughnessPresent +
                      " ambientOcclusionPresent=" + ambientOcclusionPresent +
                      " emissiveMatchesProfile=" + emissiveMatchesProfile +
                      " independentChannels=" + independentChannels +
                      " maxR=" + stats.MaxR +
                      " maxG=" + stats.MaxG +
                      " maxB=" + stats.MaxB +
                      " maxA=" + stats.MaxA;
            return false;
        }

        private static bool TryCollectMraoStats(Texture2D maskTexture, out MraoStats stats, out string failure)
        {
            stats = default;
            failure = string.Empty;
            if (maskTexture == null)
            {
                failure = "mask texture is null";
                return false;
            }

            NativeArray<Color32> pixels;
            try
            {
                pixels = maskTexture.GetRawTextureData<Color32>();
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "mask texture pixel read failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            if (!pixels.IsCreated || pixels.Length == 0)
            {
                failure = "mask texture has no pixels";
                return false;
            }

            bool r = false;
            bool g = false;
            bool b = false;
            bool a = false;
            bool rgbDivergence = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                r |= p.r > 0;
                g |= p.g > 0;
                b |= p.b > 0;
                a |= p.a > 0;
                rgbDivergence |= p.r != p.g || p.g != p.b;
                if (r && g && b && a && rgbDivergence)
                    break;
            }

            stats = new MraoStats(r, g, b, a, rgbDivergence);
            return true;
        }

        private readonly struct MraoStats
        {
            public readonly bool MaxR;
            public readonly bool MaxG;
            public readonly bool MaxB;
            public readonly bool MaxA;
            public readonly bool AnyRgbDivergence;

            public MraoStats(bool maxR, bool maxG, bool maxB, bool maxA, bool anyRgbDivergence)
            {
                MaxR = maxR;
                MaxG = maxG;
                MaxB = maxB;
                MaxA = maxA;
                AnyRgbDivergence = anyRgbDivergence;
            }
        }

        public static void EnforceTextureImportSettings(string assetPath, TextureRole role, int maxTextureSize)
        {
            if (!TryEnforceTextureImportSettings(assetPath, role, maxTextureSize, out string failure))
                throw new InvalidOperationException(failure);
        }

        internal static bool TryEnforceTextureImportSettings(string assetPath, TextureRole role, int maxTextureSize, out string failure)
        {
            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    failure = "TextureImporter missing for " + assetPath;
                    return false;
                }

                importer.textureType = role == TextureRole.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = role == TextureRole.Albedo;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = role == TextureRole.Mask;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = role == TextureRole.Normal ? 2 : 1;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
                int clampedMaxTextureSize = Mathf.Clamp(maxTextureSize, MinimumBakeSize, MaximumBakeSize);
                importer.maxTextureSize = clampedMaxTextureSize;

                TextureImporterFormat standaloneFormat = role == TextureRole.Normal
                    ? TextureImporterFormat.BC5
                    : TextureImporterFormat.BC7;

                TextureImporterPlatformSettings standalone = new TextureImporterPlatformSettings
                {
                    name = "Standalone",
                    overridden = true,
                    maxTextureSize = importer.maxTextureSize,
                    format = standaloneFormat,
                    compressionQuality = 100
                };
                importer.SetPlatformTextureSettings(standalone);

                TextureImporterPlatformSettings android = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = importer.maxTextureSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    compressionQuality = 100
                };
                importer.SetPlatformTextureSettings(android);

                TextureImporterPlatformSettings iPhone = new TextureImporterPlatformSettings
                {
                    name = "iPhone",
                    overridden = true,
                    maxTextureSize = importer.maxTextureSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    compressionQuality = 100
                };
                importer.SetPlatformTextureSettings(iPhone);
                importer.SaveAndReimport();
                return AuditTextureImporterSettings(assetPath, role, clampedMaxTextureSize, out failure);
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "texture import settings failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        internal static bool TryEnforceTextureImportSettings(
            string assetPath,
            bool srgb,
            bool alphaTransparency,
            TextureWrapMode wrapMode,
            FilterMode filterMode,
            int maxTextureSize,
            TextureImporterFormat standaloneFormat,
            out string failure)
        {
            return TryEnforceTextureImportSettings(
                assetPath,
                srgb,
                alphaTransparency,
                wrapMode,
                filterMode,
                maxTextureSize,
                standaloneFormat,
                false,
                1,
                out failure);
        }

        internal static bool TryEnforceTextureImportSettings(
            string assetPath,
            bool srgb,
            bool alphaTransparency,
            TextureWrapMode wrapMode,
            FilterMode filterMode,
            int maxTextureSize,
            TextureImporterFormat standaloneFormat,
            bool streamingMipmaps,
            int anisoLevel,
            out string failure)
        {
            try
            {
                failure = string.Empty;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    failure = "TextureImporter missing for " + assetPath;
                    return false;
                }

                int clampedMaxTextureSize = Mathf.Clamp(maxTextureSize, MinimumBakeSize, MaximumBakeSize);
                int clampedAnisoLevel = Mathf.Clamp(anisoLevel, 1, 16);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = srgb;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = streamingMipmaps;
                importer.isReadable = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = alphaTransparency;
                importer.wrapMode = wrapMode;
                importer.filterMode = filterMode;
                importer.anisoLevel = clampedAnisoLevel;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = clampedMaxTextureSize;

                TextureImporterPlatformSettings standalone = new TextureImporterPlatformSettings
                {
                    name = "Standalone",
                    overridden = true,
                    maxTextureSize = clampedMaxTextureSize,
                    format = standaloneFormat,
                    compressionQuality = 100
                };
                importer.SetPlatformTextureSettings(standalone);

                TextureImporterPlatformSettings android = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = clampedMaxTextureSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    compressionQuality = 100
                };
                importer.SetPlatformTextureSettings(android);

                TextureImporterPlatformSettings iPhone = new TextureImporterPlatformSettings
                {
                    name = "iPhone",
                    overridden = true,
                    maxTextureSize = clampedMaxTextureSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    compressionQuality = 100
                };
                importer.SetPlatformTextureSettings(iPhone);

                importer.SaveAndReimport();
                return AuditTextureImporterSettings(
                    assetPath,
                    srgb,
                    alphaTransparency,
                    wrapMode,
                    filterMode,
                    clampedMaxTextureSize,
                    standaloneFormat,
                    streamingMipmaps,
                    clampedAnisoLevel,
                    out failure);
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "texture import settings failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool AuditTextureImporterSettings(
            string assetPath,
            bool srgb,
            bool alphaTransparency,
            TextureWrapMode wrapMode,
            FilterMode filterMode,
            int expectedMaxTextureSize,
            TextureImporterFormat standaloneFormat,
            bool expectedStreamingMipmaps,
            int expectedAnisoLevel,
            out string failure)
        {
            failure = string.Empty;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failure = "TextureImporter missing for audit " + assetPath;
                return false;
            }

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            TextureImporterPlatformSettings iPhone = importer.GetPlatformTextureSettings("iPhone");

            bool importerCorrect =
                importer.textureType == TextureImporterType.Default &&
                importer.sRGBTexture == srgb &&
                importer.mipmapEnabled &&
                importer.streamingMipmaps == expectedStreamingMipmaps &&
                !importer.isReadable &&
                importer.alphaSource == TextureImporterAlphaSource.FromInput &&
                importer.alphaIsTransparency == alphaTransparency &&
                importer.wrapMode == wrapMode &&
                importer.filterMode == filterMode &&
                importer.anisoLevel == expectedAnisoLevel &&
                importer.textureCompression == TextureImporterCompression.CompressedHQ &&
                !importer.crunchedCompression &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                importer.maxTextureSize == expectedMaxTextureSize;
            bool standaloneCorrect = standalone.overridden &&
                                      standalone.maxTextureSize == expectedMaxTextureSize &&
                                      standalone.format == standaloneFormat;
            bool androidCorrect = android.overridden &&
                                  android.maxTextureSize == expectedMaxTextureSize &&
                                  android.format == TextureImporterFormat.ASTC_6x6;
            bool iPhoneCorrect = iPhone.overridden &&
                                 iPhone.maxTextureSize == expectedMaxTextureSize &&
                                 iPhone.format == TextureImporterFormat.ASTC_6x6;
            if (importerCorrect && standaloneCorrect && androidCorrect && iPhoneCorrect)
                return true;

            failure = "importerCorrect=" + importerCorrect +
                      " standaloneCorrect=" + standaloneCorrect +
                      " androidCorrect=" + androidCorrect +
                      " iPhoneCorrect=" + iPhoneCorrect;
            return false;
        }

        internal static bool AuditTextureImporterSettings(string assetPath, TextureRole role, int expectedMaxTextureSize, out string failure)
        {
            try
            {
                failure = string.Empty;
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    failure = "TextureImporter missing for audit " + assetPath;
                    return false;
                }

                TextureImporterFormat standaloneFormat = role == TextureRole.Normal
                    ? TextureImporterFormat.BC5
                    : TextureImporterFormat.BC7;
                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                TextureImporterPlatformSettings iPhone = importer.GetPlatformTextureSettings("iPhone");

                bool textureTypeCorrect = importer.textureType == (role == TextureRole.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default);
                bool srgbCorrect = importer.sRGBTexture == (role == TextureRole.Albedo);
                bool readableCorrect = !importer.isReadable;
                bool alphaTransparencyCorrect = importer.alphaIsTransparency == (role == TextureRole.Mask);
                bool wrapCorrect = importer.wrapMode == TextureWrapMode.Clamp;
                bool filterCorrect = importer.filterMode == FilterMode.Bilinear;
                bool anisoCorrect = importer.anisoLevel == (role == TextureRole.Normal ? 2 : 1);
                bool compressionCorrect = importer.textureCompression == TextureImporterCompression.CompressedHQ;
                bool maxSizeCorrect = importer.maxTextureSize == expectedMaxTextureSize;
                bool standaloneCorrect = standalone.overridden && standalone.maxTextureSize == expectedMaxTextureSize && standalone.format == standaloneFormat;
                bool androidCorrect = android.overridden && android.maxTextureSize == expectedMaxTextureSize && android.format == TextureImporterFormat.ASTC_6x6;
                bool iPhoneCorrect = iPhone.overridden && iPhone.maxTextureSize == expectedMaxTextureSize && iPhone.format == TextureImporterFormat.ASTC_6x6;

                if (textureTypeCorrect && srgbCorrect && readableCorrect && alphaTransparencyCorrect && wrapCorrect && filterCorrect && anisoCorrect && compressionCorrect && maxSizeCorrect && standaloneCorrect && androidCorrect && iPhoneCorrect)
                    return true;

                failure = "textureTypeCorrect=" + textureTypeCorrect +
                          " srgbCorrect=" + srgbCorrect +
                          " readableCorrect=" + readableCorrect +
                          " alphaTransparencyCorrect=" + alphaTransparencyCorrect +
                          " wrapCorrect=" + wrapCorrect +
                          " filterCorrect=" + filterCorrect +
                          " anisoCorrect=" + anisoCorrect +
                          " compressionCorrect=" + compressionCorrect +
                          " maxSizeCorrect=" + maxSizeCorrect +
                          " standaloneCorrect=" + standaloneCorrect +
                          " androidCorrect=" + androidCorrect +
                          " iPhoneCorrect=" + iPhoneCorrect;
                return false;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "texture importer audit failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static bool AuditTextureMeta(string assetPath, TextureRole role, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                failure = "asset path is empty";
                return false;
            }

            string metaPath = assetPath + ".meta";
            string meta;
            try
            {
                if (!File.Exists(metaPath))
                {
                    failure = "missing meta " + metaPath;
                    return false;
                }

                long metaLength = new FileInfo(metaPath).Length;
                if (metaLength > MaxRollbackMetaBytes)
                {
                    failure = "meta audit byte ceiling exceeded for " + metaPath + ": " + metaLength + " > " + MaxRollbackMetaBytes;
                    return false;
                }

                meta = File.ReadAllText(metaPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "meta audit read failed for " + metaPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            bool compressionHigh = s_highCompressionRegex.IsMatch(meta);
            bool srgbCorrect = role == TextureRole.Albedo
                ? s_srgbTextureRegex.IsMatch(meta)
                : s_linearTextureRegex.IsMatch(meta);
            bool platformOverride = role == TextureRole.Normal
                ? s_bc5FormatRegex.IsMatch(meta)
                : s_bc7FormatRegex.IsMatch(meta);
            bool mobileOverrides = s_androidPlatformRegex.IsMatch(meta) && s_iPhonePlatformRegex.IsMatch(meta);

            if (compressionHigh && srgbCorrect && platformOverride && mobileOverrides)
                return true;

            failure = "compressionHigh=" + compressionHigh +
                      " srgbCorrect=" + srgbCorrect +
                      " platformOverride=" + platformOverride +
                      " mobileOverrides=" + mobileOverrides;
            return false;
        }

        private static bool TryDispatchAndWrite(
            ComputeShader compute,
            string kernelName,
            in BakeProfileDTO profile,
            int size,
            TextureRole role,
            string assetPath)
        {
            if (!TryResolveComputeKernel(compute, kernelName, out int kernel, out string kernelFailure))
            {
                Debug.LogError("[TextureBaker1605] " + kernelFailure);
                return false;
            }

            RenderTexture rt = null;
            Texture2D staging = null;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 0)
                {
                    enableRandomWrite = true,
                    mipCount = 1,
                    msaaSamples = 1,
                    sRGB = role == TextureRole.Albedo
                };

                rt = new RenderTexture(descriptor)
                {
                    name = "RT_1605_" + kernelName
                };
                if (!rt.Create())
                {
                    Debug.LogError("[TextureBaker1605] RenderTexture allocation failed for " + kernelName + " at " + size + "x" + size);
                    return false;
                }

                uint seed = profile.Seed == 0u ? 1u : profile.Seed;
                int safeVariant = ResolveSafeVariant(profile.Variant);
                compute.SetVector(s_textureSizeId, new Vector4(size, size, safeVariant, 0f));
                compute.SetInt(s_seedId, unchecked((int)seed));
                compute.SetVector(s_colorAId, (Vector4)profile.BaseColor);
                compute.SetVector(s_colorBId, (Vector4)profile.AccentColor);
                compute.SetVector(s_colorCId, (Vector4)profile.WearOrEmissionColor);
                compute.SetVector(s_noiseParamsId, new Vector4(
                    Mathf.Max(0.01f, profile.NoiseScale),
                    Mathf.Max(0.01f, profile.PoreDensity),
                    Mathf.Clamp01(profile.RustSpread),
                    Mathf.Clamp01(profile.EdgeWearIntensity)));
                compute.SetVector(s_surfaceParamsId, new Vector4(
                    Mathf.Max(0f, profile.NormalStrength),
                    Mathf.Clamp01(profile.Metallic),
                    Mathf.Clamp01(profile.Roughness),
                    Mathf.Clamp01(profile.Emissive)));
                float qualityWeight = Mathf.Clamp01(profile.GlobalQualityWeight);
                compute.SetVector(s_qualityParamsId, new Vector4(
                    qualityWeight,
                    Mathf.Lerp(0.45f, 1.0f, qualityWeight),
                    Mathf.Lerp(0.35f, 1.0f, qualityWeight),
                    0f));
                compute.SetTexture(kernel, s_outputId, rt);

                compute.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
                if (!TryResolveDispatchGroups(size, groupSizeX, groupSizeY, out int groupsX, out int groupsY, out string dispatchFailure))
                {
                    Debug.LogError("[TextureBaker1605] Invalid compute dispatch shape for " + kernelName + ": " + dispatchFailure);
                    return false;
                }

                compute.Dispatch(kernel, groupsX, groupsY, 1);

                staging = new Texture2D(size, size, TextureFormat.RGBA32, true, role != TextureRole.Albedo);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = rt;
                    staging.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                    staging.Apply(true, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                if (role == TextureRole.Mask && !VerifyMraoChannels(staging, in profile, out string failure))
                {
                    Debug.LogError("[TextureBaker1605] M.R.A.O. verification failed: " + failure);
                    return false;
                }

                if (!TryWritePng(staging, assetPath))
                    return false;

                if (!TryEnforceTextureImportSettings(assetPath, role, size, out string importFailure))
                {
                    Debug.LogError("[TextureBaker1605] " + importFailure);
                    return false;
                }

                return true;
            }
            catch (IOException ex)
            {
                Debug.LogWarning("[TextureBaker1605] File write failed: " + ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning("[TextureBaker1605] File access denied: " + ex.Message);
                return false;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                Debug.LogError("[TextureBaker1605] Bake dispatch failed for " + kernelName + ": " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                if (staging != null)
                    UnityEngine.Object.DestroyImmediate(staging);
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
        }

        private static bool TryResolveComputeKernel(ComputeShader compute, string kernelName, out int kernel, out string failure)
        {
            kernel = -1;
            failure = string.Empty;

            if (compute == null)
            {
                failure = "compute shader is null for " + kernelName;
                return false;
            }

            try
            {
                if (!compute.HasKernel(kernelName))
                {
                    failure = "missing kernel " + kernelName;
                    return false;
                }

                kernel = compute.FindKernel(kernelName);
                if (kernel < 0)
                {
                    failure = "invalid kernel index for " + kernelName;
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "compute kernel resolve failed for " + kernelName + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryWritePng(Texture2D texture, string assetPath)
        {
            byte[] png = ImageConversion.EncodeToPNG(texture);
            if (png == null || png.Length == 0)
            {
                Debug.LogError("[TextureBaker1605] PNG encoding returned no bytes for " + assetPath);
                return false;
            }

            if (png.LongLength > MaxEncodedPngBytes)
            {
                Debug.LogError("[TextureBaker1605] PNG byte ceiling exceeded for " + assetPath);
                return false;
            }

            if (TryWriteBytesAtomic(assetPath, png, out string failure))
                return true;

            Debug.LogWarning("[TextureBaker1605] File write failed: " + failure);
            return false;
        }

        internal static bool TryWriteBytesAtomic(string assetPath, byte[] bytes, out string failure)
        {
            if (bytes == null || bytes.Length == 0)
            {
                failure = "no bytes to write";
                return false;
            }

            try
            {
                string absolutePath = ResolveProjectAssetPath(assetPath);
                return TryWriteBytesAtomicAbsolute(absolutePath, bytes, false, out failure);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        internal static bool TryCaptureAssetFileRollbackSnapshots(
            string assetPath,
            out AssetFileRollbackSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            snapshots = new AssetFileRollbackSnapshot[1];
            return TryCaptureAssetFileRollbackSnapshot(assetPath, out snapshots[0], out failure);
        }

        internal static bool TryCaptureAssetFileRollbackSnapshots(
            string firstAssetPath,
            string secondAssetPath,
            out AssetFileRollbackSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            snapshots = new AssetFileRollbackSnapshot[2];
            if (!TryCaptureAssetFileRollbackSnapshot(firstAssetPath, out snapshots[0], out failure))
                return false;
            return TryCaptureAssetFileRollbackSnapshot(secondAssetPath, out snapshots[1], out failure);
        }

        internal static bool TryCaptureAssetFileRollbackSnapshots(
            string firstAssetPath,
            string secondAssetPath,
            string thirdAssetPath,
            out AssetFileRollbackSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            snapshots = new AssetFileRollbackSnapshot[3];
            if (!TryCaptureAssetFileRollbackSnapshot(firstAssetPath, out snapshots[0], out failure))
                return false;
            if (!TryCaptureAssetFileRollbackSnapshot(secondAssetPath, out snapshots[1], out failure))
                return false;
            return TryCaptureAssetFileRollbackSnapshot(thirdAssetPath, out snapshots[2], out failure);
        }

        internal static bool TryCaptureAssetFileRollbackSnapshots(
            string firstAssetPath,
            string secondAssetPath,
            string thirdAssetPath,
            string fourthAssetPath,
            out AssetFileRollbackSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            snapshots = new AssetFileRollbackSnapshot[4];
            if (!TryCaptureAssetFileRollbackSnapshot(firstAssetPath, out snapshots[0], out failure))
                return false;
            if (!TryCaptureAssetFileRollbackSnapshot(secondAssetPath, out snapshots[1], out failure))
                return false;
            if (!TryCaptureAssetFileRollbackSnapshot(thirdAssetPath, out snapshots[2], out failure))
                return false;
            return TryCaptureAssetFileRollbackSnapshot(fourthAssetPath, out snapshots[3], out failure);
        }

        internal static bool TryCaptureAssetFileRollbackSnapshots(
            string[] assetPaths,
            out AssetFileRollbackSnapshot[] snapshots,
            out string failure)
        {
            failure = string.Empty;
            if (assetPaths == null || assetPaths.Length == 0)
            {
                snapshots = Array.Empty<AssetFileRollbackSnapshot>();
                return true;
            }

            snapshots = new AssetFileRollbackSnapshot[assetPaths.Length];
            for (int i = 0; i < assetPaths.Length; i++)
            {
                if (!TryCaptureAssetFileRollbackSnapshot(assetPaths[i], out snapshots[i], out failure))
                    return false;
            }

            return true;
        }

        internal static void TryRestoreAssetFileRollbackSnapshots(AssetFileRollbackSnapshot[] snapshots)
        {
            if (snapshots == null)
                return;

            for (int i = snapshots.Length - 1; i >= 0; i--)
                TryRestoreAssetFileRollbackSnapshot(in snapshots[i]);
        }

        private static bool TryCaptureAssetFileRollbackSnapshot(string assetPath, out AssetFileRollbackSnapshot snapshot, out string failure)
        {
            snapshot = default;
            failure = string.Empty;
            try
            {
                string absolutePath = ResolveProjectAssetPath(assetPath);
                bool hadAssetFile = File.Exists(absolutePath);
                byte[] assetBytes = hadAssetFile ? ReadRollbackFileBytes(absolutePath, MaxRollbackAssetBytes, "asset") : null;
                string metaPath = absolutePath + ".meta";
                bool hadMetaFile = File.Exists(metaPath);
                byte[] metaBytes = hadMetaFile ? ReadRollbackFileBytes(metaPath, MaxRollbackMetaBytes, "meta") : null;
                snapshot = new AssetFileRollbackSnapshot(assetPath, absolutePath, hadAssetFile, assetBytes, hadMetaFile, metaBytes);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                failure = "asset file rollback capture failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static byte[] ReadRollbackFileBytes(string absolutePath, long maxBytes, string label)
        {
            long length = new FileInfo(absolutePath).Length;
            if (length > maxBytes)
                throw new IOException(label + " rollback file exceeds byte ceiling: " + length + " > " + maxBytes);

            return File.ReadAllBytes(absolutePath);
        }

        private static void TryRestoreAssetFileRollbackSnapshot(in AssetFileRollbackSnapshot snapshot)
        {
            if (!snapshot.Captured)
                return;

            try
            {
                string directory = Path.GetDirectoryName(snapshot.AbsolutePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (snapshot.HadAssetFile)
                {
                    if (snapshot.AssetBytes == null)
                        throw new InvalidOperationException("rollback asset bytes are missing");

                    if (!TryWriteBytesAtomicAbsolute(snapshot.AbsolutePath, snapshot.AssetBytes, true, out string assetWriteFailure))
                        throw new InvalidOperationException("asset rollback restore write failed: " + assetWriteFailure);
                }
                else if (File.Exists(snapshot.AbsolutePath))
                {
                    File.Delete(snapshot.AbsolutePath);
                }

                string metaPath = snapshot.AbsolutePath + ".meta";
                if (snapshot.HadMetaFile)
                {
                    if (snapshot.MetaBytes == null)
                        throw new InvalidOperationException("rollback meta bytes are missing");

                    if (!TryWriteBytesAtomicAbsolute(metaPath, snapshot.MetaBytes, true, out string metaWriteFailure))
                        throw new InvalidOperationException("meta rollback restore write failed: " + metaWriteFailure);
                }
                else if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }

                if (snapshot.HadAssetFile)
                    AssetDatabase.ImportAsset(snapshot.AssetPath, ImportAssetOptions.ForceSynchronousImport);
                else if (!snapshot.HadMetaFile)
                    AssetDatabase.DeleteAsset(snapshot.AssetPath);
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Failed to restore asset rollback snapshot " + snapshot.AssetPath + ": " + ex.Message);
            }
        }

        private static bool TryWriteBytesAtomicAbsolute(string absolutePath, byte[] bytes, bool allowEmpty, out string failure)
        {
            failure = string.Empty;
            string tempPath = string.Empty;
            if (bytes == null || (!allowEmpty && bytes.Length == 0))
            {
                failure = "no bytes to write";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(absolutePath);
                if (string.IsNullOrEmpty(directory))
                {
                    failure = "invalid output directory for " + absolutePath;
                    return false;
                }

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                tempPath = absolutePath + AtomicWriteTempExtension;
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                File.WriteAllBytes(tempPath, bytes);
                if (File.Exists(absolutePath))
                    ReplaceExistingFile(tempPath, absolutePath);
                else
                    File.Move(tempPath, absolutePath);

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                TryDeleteTempFile(tempPath);
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static void TryDeleteCreatedBakeOutputs(
            string albedoPath,
            string normalPath,
            string maskPath,
            bool albedoExisted,
            bool normalExisted,
            bool maskExisted)
        {
            TryDeleteCreatedAssetNoThrow(albedoPath, albedoExisted);
            TryDeleteCreatedAssetNoThrow(normalPath, normalExisted);
            TryDeleteCreatedAssetNoThrow(maskPath, maskExisted);
        }

        private static void TryDeleteCreatedAssetNoThrow(string assetPath, bool existedBefore)
        {
            if (existedBefore || string.IsNullOrWhiteSpace(assetPath))
                return;

            try
            {
                bool deletedByAssetDatabase = AssetDatabase.DeleteAsset(assetPath);
                if (!deletedByAssetDatabase)
                {
                    string absolutePath = ResolveProjectAssetPath(assetPath);
                    if (File.Exists(absolutePath))
                        File.Delete(absolutePath);

                    string metaPath = absolutePath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                }
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Failed to clean newly-created bake output " + assetPath + ": " + ex.Message);
            }
        }

        private static bool AssetPathExistsNoThrow(string assetPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                    return true;

                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    return true;

                string absolutePath = ResolveProjectAssetPath(assetPath);
                return File.Exists(absolutePath);
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Unknown bake output prior state for " + assetPath + ": " + ex.Message);
                return true;
            }
        }

        private static string ResolveProjectAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("asset path is empty", nameof(assetPath));

            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("asset path must stay under Assets/: " + assetPath, nameof(assetPath));

            string projectRoot = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName);
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolutePath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("asset path escapes Assets/: " + assetPath, nameof(assetPath));

            return absolutePath;
        }

        private static void ReplaceExistingFile(string tempPath, string absolutePath)
        {
            try
            {
                File.Replace(tempPath, absolutePath, null, true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (NotSupportedException)
            {
            }

            string backupPath = absolutePath + AtomicWriteBackupExtension;
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Move(absolutePath, backupPath);
            try
            {
                File.Move(tempPath, absolutePath);
                TryDeleteTempFile(backupPath);
            }
            catch
            {
                if (!File.Exists(absolutePath) && File.Exists(backupPath))
                    File.Move(backupPath, absolutePath);

                throw;
            }
        }

        private static void TryDeleteTempFile(string tempPath)
        {
            if (string.IsNullOrEmpty(tempPath))
                return;

            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        internal static bool TryEnsureAssetFolder(string assetFolder, out string failure)
        {
            return TryEnsureAssetFolder(assetFolder, out _, out failure);
        }

        internal static bool TryEnsureAssetFolder(string assetFolder, out string normalized, out string failure)
        {
            failure = string.Empty;
            if (!TryNormalizeAssetFolder(assetFolder, out normalized, out failure))
                return false;

            try
            {
                if (AssetDatabase.IsValidFolder(normalized))
                    return true;

                string[] parts = normalized.Split('/');
                string current = parts[0];
                if (!AssetDatabase.IsValidFolder(current))
                {
                    failure = "project Assets folder is missing";
                    return false;
                }

                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        string guid = AssetDatabase.CreateFolder(current, parts[i]);
                        if (string.IsNullOrEmpty(guid) && !AssetDatabase.IsValidFolder(next))
                        {
                            failure = "failed to create asset folder " + next;
                            return false;
                        }
                    }

                    current = next;
                }

                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "asset folder creation failed for " + normalized + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryNormalizeAssetFolder(string assetFolder, out string normalized, out string failure)
        {
            normalized = string.Empty;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(assetFolder))
            {
                failure = "asset folder is empty";
                return false;
            }

            normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (normalized == "Assets")
                return true;

            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                failure = "asset folder must stay under Assets/: " + assetFolder;
                return false;
            }

            string[] parts = normalized.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part) || part == "." || part == "..")
                {
                    failure = "asset folder contains invalid segment: " + assetFolder;
                    return false;
                }
            }

            return true;
        }

        internal static string SanitizeAssetNameForPath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            char[] chars = value.ToCharArray();
            bool hasValidAssetNameChar = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= 'A' && c <= 'Z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_' ||
                             c == '-';
                if (valid)
                    hasValidAssetNameChar = true;
                else
                    chars[i] = '_';
            }

            return hasValidAssetNameChar ? new string(chars) : string.Empty;
        }

        private static string BuildSeedFallbackName(uint seed)
        {
            Span<char> chars = stackalloc char[13];
            chars[0] = 'b';
            chars[1] = 'a';
            chars[2] = 'k';
            chars[3] = 'e';
            chars[4] = '_';

            for (int i = 0; i < 8; i++)
            {
                int shift = 28 - i * 4;
                int nibble = (int)((seed >> shift) & 0xFu);
                chars[5 + i] = (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
            }

            return new string(chars);
        }

        private static int CeilDivide(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;
            return (value + divisor - 1) / divisor;
        }

        internal static bool TryResolveDispatchGroups(int textureSize, uint groupSizeX, uint groupSizeY, out int groupsX, out int groupsY, out string failure)
        {
            groupsX = 0;
            groupsY = 0;
            failure = string.Empty;

            if (textureSize <= 0)
            {
                failure = "texture size must be positive";
                return false;
            }

            if (groupSizeX == 0u || groupSizeY == 0u || groupSizeX > int.MaxValue || groupSizeY > int.MaxValue)
            {
                failure = "kernel thread group size is invalid";
                return false;
            }

            groupsX = CeilDivide(textureSize, (int)groupSizeX);
            groupsY = CeilDivide(textureSize, (int)groupSizeY);
            if (groupsX <= 0 || groupsY <= 0)
            {
                failure = "dispatch group count is invalid";
                return false;
            }

            return true;
        }

        private static int NextPowerOfTwo(int value)
        {
            if (value <= MinimumBakeSize)
                return MinimumBakeSize;
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value++;
            return value;
        }

        private static int ToPowerOfTwoExponent(int value)
        {
            int clamped = Mathf.Clamp(NextPowerOfTwo(value), MinimumBakeSize, MaximumBakeSize);
            int exponent = 0;
            while ((1 << exponent) < clamped && exponent < 30)
                exponent++;
            return exponent;
        }
    }
}
