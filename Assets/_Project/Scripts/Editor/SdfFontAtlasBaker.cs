#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Editor;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TextCore.LowLevel;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Localization
{
    /// <summary>
    /// Editor-only static SDF/MSDF localization font atlas baker.
    /// </summary>
    public sealed class SdfFontAtlasBaker : EditorWindow
    {
        private const string MenuRoot = "HECTON-8/Bakers/1729/";
        private const string ComputeShaderPath = "Assets/_Project/Art/Shaders/Include/SdfFontAtlasBaker1729.compute";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Generated/SdfFontAtlas1729";
        private const string NotoSansPath = "Assets/_Project/Art/Fonts/NotoSans-Regular.ttf";
        private const string NotoSansArabicPath = "Assets/_Project/Art/Fonts/NotoSansArabic-Regular.ttf";
        private const string NotoSansCjkScPath = "Assets/_Project/Art/Fonts/NotoSansCJKsc-Regular.otf";
        private const string NotoSansCjkJpPath = "Assets/_Project/Art/Fonts/NotoSansCJKjp-Regular.otf";
        private const string EnglishLocalizationPath = "Assets/_Project/Scripts/English.json";
        private const string RussianLocalizationPath = "Assets/_Project/Scripts/Russian.json";
        private const string ChineseLocalizationPath = "Assets/_Project/Scripts/ChineseSimplified.json";
        private const string JapaneseLocalizationPath = "Assets/_Project/Scripts/Japanese.json";
        private const string KoreanLocalizationPath = "Assets/_Project/Scripts/Korean.json";
        private const string ArabicLocalizationPath = "Assets/_Project/Scripts/Arabic.json";
        private const string KoreanSentinelGlyphs = "\uD55C\uAE00";
        private const int MinimumAtlasSize = 1024;
        private const int MaximumAtlasSize = 4096;
        private const int MinimumSamplingPointSize = 72;
        private const int MaximumSamplingPointSize = 118;
        private const int MinimumPadding = 7;
        private const int MaximumPadding = 14;
        private const long MaxEncodedPngBytes = 192L * 1024L * 1024L;
        private const long Bc4BytesPerBlock = 8L;
        private const long Bc7BytesPerBlock = 16L;

        private static readonly int s_sdfSourceId = Shader.PropertyToID("_SdfSource");
        private static readonly int s_sdfOutputId = Shader.PropertyToID("_SdfOutput");
        private static readonly int s_sdfAtlasParamsId = Shader.PropertyToID("_SdfAtlasParams");
        private static readonly int s_sdfEdgeCenterId = Shader.PropertyToID("_SdfEdgeCenter");

        [SerializeField] private string _assetNamePrefix = "h8_babel_static";
        [SerializeField] private string _outputFolder = DefaultOutputFolder;
        [SerializeField] private ComputeShader _computeOverride;
        [SerializeField] private Font _latinCyrillicFont;
        [SerializeField] private Font _arabicFont;
        [SerializeField] private Font _cjkScFont;
        [SerializeField] private Font _cjkJpFont;
        [SerializeField] private float _globalQualityWeight = 0.55f;
        [SerializeField] private bool _includeMsdfSupportTexture = true;
        [SerializeField] private bool _includeLocalizationTables = true;
        private string _lastStatus = "Idle.";

        private enum FontCoverage
        {
            LatinCyrillic,
            Arabic,
            CjkSc,
            CjkJp
        }

        private enum TextureExportRole
        {
            SdfSingleChannel,
            SdfRgba,
            MsdfRgba
        }

        private readonly struct FontBakeSpec
        {
            public readonly string Name;
            public readonly Font SourceFont;
            public readonly FontCoverage Coverage;

            public FontBakeSpec(string name, Font sourceFont, FontCoverage coverage)
            {
                Name = name;
                SourceFont = sourceFont;
                Coverage = coverage;
            }
        }

        private readonly struct FontBakeOutput
        {
            public readonly string FontAssetPath;
            public readonly string SdfBc4Path;
            public readonly string SdfRgbaPath;
            public readonly string MsdfPath;
            public readonly int RequestedGlyphs;
            public readonly int MissingGlyphs;
            public readonly long EstimatedBc4Bytes;
            public readonly long EstimatedBc7Bytes;

            public FontBakeOutput(
                string fontAssetPath,
                string sdfBc4Path,
                string sdfRgbaPath,
                string msdfPath,
                int requestedGlyphs,
                int missingGlyphs,
                long estimatedBc4Bytes,
                long estimatedBc7Bytes)
            {
                FontAssetPath = fontAssetPath;
                SdfBc4Path = sdfBc4Path;
                SdfRgbaPath = sdfRgbaPath;
                MsdfPath = msdfPath;
                RequestedGlyphs = requestedGlyphs;
                MissingGlyphs = missingGlyphs;
                EstimatedBc4Bytes = estimatedBc4Bytes;
                EstimatedBc7Bytes = estimatedBc7Bytes;
            }
        }

        private readonly struct TextureValidation
        {
            public readonly long ExpectedPixelCount;
            public readonly long ActualPixelCount;
            public readonly byte MinSignal;
            public readonly byte MaxSignal;
            public readonly byte AlphaMin;
            public readonly byte AlphaMax;

            public TextureValidation(long expectedPixelCount, long actualPixelCount, byte minSignal, byte maxSignal, byte alphaMin, byte alphaMax)
            {
                ExpectedPixelCount = expectedPixelCount;
                ActualPixelCount = actualPixelCount;
                MinSignal = minSignal;
                MaxSignal = maxSignal;
                AlphaMin = alphaMin;
                AlphaMax = alphaMax;
            }
        }

        private readonly struct BakeResult
        {
            public readonly string OutputFolder;
            public readonly int AtlasSize;
            public readonly int SamplingPointSize;
            public readonly int Padding;
            public readonly double DurationMs;
            public readonly GlyphCatalogStats GlyphStats;
            public readonly FontBakeOutput[] Outputs;

            public BakeResult(
                string outputFolder,
                int atlasSize,
                int samplingPointSize,
                int padding,
                double durationMs,
                GlyphCatalogStats glyphStats,
                FontBakeOutput[] outputs)
            {
                OutputFolder = outputFolder;
                AtlasSize = atlasSize;
                SamplingPointSize = samplingPointSize;
                Padding = padding;
                DurationMs = durationMs;
                GlyphStats = glyphStats;
                Outputs = outputs;
            }
        }

        private struct BakeSettings
        {
            public string AssetNamePrefix;
            public string OutputFolder;
            public ComputeShader ComputeOverride;
            public Font LatinCyrillicFont;
            public Font ArabicFont;
            public Font CjkScFont;
            public Font CjkJpFont;
            public float GlobalQualityWeight;
            public bool IncludeMsdfSupportTexture;
            public bool IncludeLocalizationTables;

            public static BakeSettings Default()
            {
                return new BakeSettings
                {
                    AssetNamePrefix = "h8_babel_static",
                    OutputFolder = DefaultOutputFolder,
                    ComputeOverride = null,
                    LatinCyrillicFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansPath),
                    ArabicFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansArabicPath),
                    CjkScFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansCjkScPath),
                    CjkJpFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansCjkJpPath),
                    GlobalQualityWeight = 0.55f,
                    IncludeMsdfSupportTexture = true,
                    IncludeLocalizationTables = true
                };
            }
        }

        private readonly struct GlyphCatalog
        {
            public readonly string LatinCyrillic;
            public readonly string Arabic;
            public readonly string CjkSc;
            public readonly string CjkJp;
            public readonly GlyphCatalogStats Stats;

            public GlyphCatalog(string latinCyrillic, string arabic, string cjkSc, string cjkJp, GlyphCatalogStats stats)
            {
                LatinCyrillic = latinCyrillic;
                Arabic = arabic;
                CjkSc = cjkSc;
                CjkJp = cjkJp;
                Stats = stats;
            }
        }

        private readonly struct GlyphCatalogStats
        {
            public readonly int LatinCyrillicCount;
            public readonly int ArabicCount;
            public readonly int CjkScCount;
            public readonly int CjkJpCount;
            public readonly int LocalizationFilesRead;
            public readonly int Reserved;

            public GlyphCatalogStats(int latinCyrillicCount, int arabicCount, int cjkScCount, int cjkJpCount, int localizationFilesRead)
            {
                LatinCyrillicCount = latinCyrillicCount;
                ArabicCount = arabicCount;
                CjkScCount = cjkScCount;
                CjkJpCount = cjkJpCount;
                LocalizationFilesRead = localizationFilesRead;
                Reserved = 0;
            }
        }

        [MenuItem(MenuRoot + "Open SDF Font Atlas Baker", false, 1729)]
        private static void Open()
        {
            SdfFontAtlasBaker window = GetWindow<SdfFontAtlasBaker>();
            window.titleContent = new GUIContent("SDF Baker 1729");
            window.minSize = new Vector2(520f, 470f);
            window.EnsureDefaultReferences();
        }

        [MenuItem(MenuRoot + "Bake Default Static Babel Atlases", false, 1730)]
        private static void BakeDefaultMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryBake(settings, out BakeResult result, out string failure))
            {
                Debug.Log("[SdfFontAtlasBaker1729] Baked static localization atlases. atlas=" +
                          result.AtlasSize.ToString(CultureInfo.InvariantCulture) +
                          " outputs=" + result.Outputs.Length.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                Debug.LogError("[SdfFontAtlasBaker1729] " + failure);
            }
        }

        private void OnEnable()
        {
            EnsureDefaultReferences();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Static Babel SDF / MSDF Atlas Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _assetNamePrefix = EditorGUILayout.TextField("Asset Prefix", _assetNamePrefix);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _computeOverride = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", _computeOverride, typeof(ComputeShader), false);
            _latinCyrillicFont = (Font)EditorGUILayout.ObjectField("Latin/Cyrillic Font", _latinCyrillicFont, typeof(Font), false);
            _arabicFont = (Font)EditorGUILayout.ObjectField("Arabic Font", _arabicFont, typeof(Font), false);
            _cjkScFont = (Font)EditorGUILayout.ObjectField("CJK SC Font", _cjkScFont, typeof(Font), false);
            _cjkJpFont = (Font)EditorGUILayout.ObjectField("CJK JP Font", _cjkJpFont, typeof(Font), false);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _includeMsdfSupportTexture = EditorGUILayout.Toggle("Export MSDF Support", _includeMsdfSupportTexture);
            _includeLocalizationTables = EditorGUILayout.Toggle("Read Localization JSON", _includeLocalizationTables);

            int atlasSize = ResolveAtlasSize(_globalQualityWeight);
            int pointSize = ResolveSamplingPointSize(_globalQualityWeight);
            int padding = ResolvePadding(_globalQualityWeight);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Atlas", atlasSize + " x " + atlasSize);
            EditorGUILayout.LabelField("Point Size", pointSize.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Padding", padding.ToString(CultureInfo.InvariantCulture));

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Static Localization Atlases", GUILayout.Height(32f)))
                BakeFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void EnsureDefaultReferences()
        {
            if (_computeOverride == null)
                _computeOverride = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath);
            if (_latinCyrillicFont == null)
                _latinCyrillicFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansPath);
            if (_arabicFont == null)
                _arabicFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansArabicPath);
            if (_cjkScFont == null)
                _cjkScFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansCjkScPath);
            if (_cjkJpFont == null)
                _cjkJpFont = AssetDatabase.LoadAssetAtPath<Font>(NotoSansCjkJpPath);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = BakeSettings.Default();
            settings.AssetNamePrefix = _assetNamePrefix;
            settings.OutputFolder = _outputFolder;
            settings.ComputeOverride = _computeOverride;
            settings.LatinCyrillicFont = _latinCyrillicFont;
            settings.ArabicFont = _arabicFont;
            settings.CjkScFont = _cjkScFont;
            settings.CjkJpFont = _cjkJpFont;
            settings.GlobalQualityWeight = _globalQualityWeight;
            settings.IncludeMsdfSupportTexture = _includeMsdfSupportTexture;
            settings.IncludeLocalizationTables = _includeLocalizationTables;

            if (TryBake(settings, out BakeResult result, out string failure))
            {
                _lastStatus = "Baked " + result.Outputs.Length.ToString(CultureInfo.InvariantCulture) +
                              " static atlases | " + result.AtlasSize.ToString(CultureInfo.InvariantCulture) +
                              "px | " + result.DurationMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms";
            }
            else
            {
                _lastStatus = "Bake failed: " + failure;
            }
        }

        private static bool TryBake(BakeSettings settings, out BakeResult result, out string failure)
        {
            result = default;
            failure = string.Empty;

            if (!ValidateUnmanagedLayouts(out failure))
                return false;

            if (!SystemInfo.supportsComputeShaders)
            {
                failure = "compute shaders are unsupported";
                return false;
            }

            ComputeShader compute = settings.ComputeOverride != null
                ? settings.ComputeOverride
                : AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath);
            if (compute == null)
            {
                failure = "missing compute shader at " + ComputeShaderPath;
                return false;
            }

            if (!ValidateSettings(settings, out failure))
                return false;

            if (!TryEnsureAssetFolder(settings.OutputFolder, out string normalizedFolder, out failure))
                return false;

            string safePrefix = SanitizeAssetNameForPath(settings.AssetNamePrefix);
            if (string.IsNullOrEmpty(safePrefix))
                safePrefix = "h8_babel_static";

            int atlasSize = ResolveAtlasSize(settings.GlobalQualityWeight);
            int pointSize = ResolveSamplingPointSize(settings.GlobalQualityWeight);
            int padding = ResolvePadding(settings.GlobalQualityWeight);
            GlyphCatalog catalog = BuildGlyphCatalog(settings.GlobalQualityWeight, settings.IncludeLocalizationTables);
            FontBakeSpec[] specs =
            {
                new FontBakeSpec("latin_cyrillic", settings.LatinCyrillicFont, FontCoverage.LatinCyrillic),
                new FontBakeSpec("arabic_rtl", settings.ArabicFont, FontCoverage.Arabic),
                new FontBakeSpec("cjk_sc", settings.CjkScFont, FontCoverage.CjkSc),
                new FontBakeSpec("cjk_jp", settings.CjkJpFont, FontCoverage.CjkJp)
            };
            FontBakeOutput[] outputs = new FontBakeOutput[specs.Length];
            TMP_FontAsset[] bakedFonts = new TMP_FontAsset[specs.Length];

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool transactionSucceeded = false;
            try
            {
                for (int i = 0; i < specs.Length; i++)
                {
                    if (!TryBakeFontSpec(
                            specs[i],
                            safePrefix,
                            normalizedFolder,
                            compute,
                            settings,
                            catalog,
                            atlasSize,
                            pointSize,
                            padding,
                            out TMP_FontAsset bakedFont,
                            out FontBakeOutput output,
                            out failure))
                    {
                        return false;
                    }

                    bakedFonts[i] = bakedFont;
                    outputs[i] = output;
                }

                WireStaticFallbacks(bakedFonts);
                if (!TryFinalizeAssetDatabase("sdf font atlas bake 1729", out failure))
                    return false;

                stopwatch.Stop();
                transactionSucceeded = true;
                result = new BakeResult(
                    normalizedFolder,
                    atlasSize,
                    pointSize,
                    padding,
                    stopwatch.Elapsed.TotalMilliseconds,
                    catalog.Stats,
                    outputs);

                return true;
            }
            catch (Exception ex) when (ex is UnityException ||
                                       ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is InvalidOperationException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                stopwatch.Stop();
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (!transactionSucceeded)
                    CleanupGeneratedBakeOutputs(outputs, bakedFonts);
            }
        }

        private static bool ValidateSettings(BakeSettings settings, out string failure)
        {
            failure = string.Empty;
            if (settings.LatinCyrillicFont == null)
            {
                failure = "missing Latin/Cyrillic font";
                return false;
            }

            if (settings.ArabicFont == null)
            {
                failure = "missing Arabic font";
                return false;
            }

            if (settings.CjkScFont == null)
            {
                failure = "missing Simplified Chinese CJK font";
                return false;
            }

            if (settings.CjkJpFont == null)
            {
                failure = "missing Japanese CJK font";
                return false;
            }

            return true;
        }

        private static bool TryBakeFontSpec(
            FontBakeSpec spec,
            string safePrefix,
            string outputFolder,
            ComputeShader compute,
            BakeSettings settings,
            GlyphCatalog catalog,
            int atlasSize,
            int pointSize,
            int padding,
            out TMP_FontAsset bakedFont,
            out FontBakeOutput output,
            out string failure)
        {
            bakedFont = null;
            output = default;
            failure = string.Empty;

            string glyphs = ResolveGlyphsForCoverage(catalog, spec.Coverage);
            if (string.IsNullOrEmpty(glyphs))
            {
                failure = "glyph set is empty for " + spec.Name;
                return false;
            }

            string assetBase = safePrefix + "_" + spec.Name;
            string fontAssetPath = outputFolder + "/FA_" + assetBase + "_SDF_Static.asset";
            string sdfBc4Path = outputFolder + "/TX_" + assetBase + "_SDF_BC4.png";
            string sdfRgbaPath = outputFolder + "/TX_" + assetBase + "_SDF_RGBA_BC7.png";
            string msdfPath = outputFolder + "/TX_" + assetBase + "_MSDF_BC7.png";

            if (!DeleteExistingGeneratedAsset(fontAssetPath, out failure))
                return false;
            if (!DeleteExistingGeneratedAsset(sdfBc4Path, out failure))
                return false;
            if (!DeleteExistingGeneratedAsset(sdfRgbaPath, out failure))
                return false;
            if (!DeleteExistingGeneratedAsset(msdfPath, out failure))
                return false;

            bool bakeSucceeded = false;
            try
            {
                bakedFont = TMP_FontAsset.CreateFontAsset(
                    spec.SourceFont,
                    pointSize,
                    padding,
                    GlyphRenderMode.SDFAA,
                    atlasSize,
                    atlasSize,
                    AtlasPopulationMode.Dynamic,
                    false);

                if (bakedFont == null)
                {
                    failure = "TMP failed to create font asset for " + spec.Name;
                    return false;
                }

                bakedFont.name = "FA_" + assetBase + "_SDF_Static";
                bakedFont.isMultiAtlasTexturesEnabled = false;
                AssetDatabase.CreateAsset(bakedFont, fontAssetPath);

                string missingCharacters;
                bool glyphsAdded = bakedFont.TryAddCharacters(glyphs, out missingCharacters);
                int missingCount = CountUniqueCharacters(missingCharacters);
                if (!glyphsAdded || missingCount != 0)
                {
                    failure = "missing glyphs for " + spec.Name +
                              " count=" + missingCount.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                ApplyStaticRuntimePolicy(bakedFont);
                EnsureFontMaterial(bakedFont);

                if (!TryValidateSingleAtlas(bakedFont, spec.Name, out failure))
                    return false;

                Texture atlasTexture = ResolveAtlasTexture(bakedFont);
                if (atlasTexture == null)
                {
                    failure = "font atlas missing after glyph bake for " + spec.Name;
                    return false;
                }

                if (!TryDispatchTextureExport(
                        compute,
                        "NormalizeSdfAlpha",
                        atlasTexture,
                        sdfBc4Path,
                        TextureExportRole.SdfSingleChannel,
                        atlasSize,
                        padding,
                        settings.GlobalQualityWeight,
                        out TextureValidation sdfBc4Validation,
                        out failure))
                {
                    return false;
                }

                if (!TryDispatchTextureExport(
                        compute,
                        "NormalizeSdfAlpha",
                        atlasTexture,
                        sdfRgbaPath,
                        TextureExportRole.SdfRgba,
                        atlasSize,
                        padding,
                        settings.GlobalQualityWeight,
                        out TextureValidation sdfRgbaValidation,
                        out failure))
                {
                    return false;
                }

                if (settings.IncludeMsdfSupportTexture)
                {
                    if (!TryDispatchTextureExport(
                            compute,
                            "DeriveMsdfFromSdf",
                            atlasTexture,
                            msdfPath,
                            TextureExportRole.MsdfRgba,
                            atlasSize,
                            padding,
                            settings.GlobalQualityWeight,
                            out TextureValidation msdfValidation,
                            out failure))
                    {
                        return false;
                    }

                    if (msdfValidation.AlphaMax <= msdfValidation.AlphaMin)
                    {
                        failure = "MSDF alpha validation failed for " + spec.Name;
                        return false;
                    }
                }
                else
                {
                    msdfPath = string.Empty;
                }

                if (sdfBc4Validation.MaxSignal <= sdfBc4Validation.MinSignal ||
                    sdfRgbaValidation.MaxSignal <= sdfRgbaValidation.MinSignal)
                {
                    failure = "SDF texture range collapsed for " + spec.Name;
                    return false;
                }

                if (!TryBindRuntimeAtlasTexture(bakedFont, sdfRgbaPath, atlasSize, out failure))
                    return false;

                EnsureFontMaterial(bakedFont);
                if (!TryValidateRuntimeAtlasBinding(bakedFont, sdfRgbaPath, out failure))
                    return false;

                if (!TryValidateSingleAtlas(bakedFont, spec.Name, out failure))
                    return false;

                output = new FontBakeOutput(
                    fontAssetPath,
                    sdfBc4Path,
                    sdfRgbaPath,
                    msdfPath,
                    glyphs.Length,
                    missingCount,
                    EstimateBlockCompressedBytes(atlasSize, Bc4BytesPerBlock),
                    EstimateBlockCompressedBytes(atlasSize, Bc7BytesPerBlock) * (settings.IncludeMsdfSupportTexture ? 2L : 1L));

                EditorUtility.SetDirty(bakedFont);
                if (bakedFont.material != null)
                    EditorUtility.SetDirty(bakedFont.material);

                bakeSucceeded = true;
                return true;
            }
            finally
            {
                if (!bakeSucceeded)
                    CleanupGeneratedBakeOutputs(fontAssetPath, sdfBc4Path, sdfRgbaPath, msdfPath, bakedFont);
            }
        }

        private static bool TryDispatchTextureExport(
            ComputeShader compute,
            string kernelName,
            Texture source,
            string assetPath,
            TextureExportRole role,
            int atlasSize,
            int padding,
            float globalQualityWeight,
            out TextureValidation validation,
            out string failure)
        {
            validation = default;
            failure = string.Empty;
            if (!TryResolveKernel(compute, kernelName, out int kernel, out failure))
                return false;

            compute.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
            if (!TryResolveDispatch(atlasSize, groupSizeX, groupSizeY, out int groupsX, out int groupsY, out failure))
                return false;

            RenderTexture output = null;
            Texture2D staging = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(atlasSize, atlasSize, GraphicsFormat.R8G8B8A8_UNorm, 0)
                {
                    enableRandomWrite = true,
                    mipCount = 1,
                    msaaSamples = 1,
                    sRGB = false
                };

                output = new RenderTexture(descriptor)
                {
                    name = "RT_SdfFontAtlasBaker1729_" + kernelName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                if (!output.Create())
                {
                    failure = "RenderTexture allocation failed for " + kernelName;
                    return false;
                }

                compute.SetTexture(kernel, s_sdfSourceId, source);
                compute.SetTexture(kernel, s_sdfOutputId, output);
                compute.SetVector(s_sdfAtlasParamsId, new Vector4(atlasSize, atlasSize, padding, Mathf.Clamp01(globalQualityWeight)));
                compute.SetFloat(s_sdfEdgeCenterId, 0.5f);
                compute.Dispatch(kernel, groupsX, groupsY, 1);

                RenderTexture.active = output;
                staging = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false, true);
                staging.ReadPixels(new Rect(0f, 0f, atlasSize, atlasSize), 0, 0, false);
                staging.Apply(false, false);

                Color32[] pixels = staging.GetPixels32();
                if (!ValidateTexturePixels(pixels, atlasSize, role, out validation, out failure))
                    return false;

                byte[] png = ImageConversion.EncodeToPNG(staging);
                if (png == null || png.LongLength == 0L)
                {
                    failure = "PNG encoder returned no bytes for " + assetPath;
                    return false;
                }

                if (png.LongLength > MaxEncodedPngBytes)
                {
                    failure = "encoded PNG byte ceiling exceeded for " + assetPath;
                    return false;
                }

                if (!TryWriteBytesAtomicAsset(assetPath, png, out failure))
                    return false;

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                return TryConfigureTextureImporter(assetPath, role, atlasSize, out failure);
            }
            finally
            {
                RenderTexture.active = previous;
                if (output != null)
                    Object.DestroyImmediate(output);
                if (staging != null)
                    Object.DestroyImmediate(staging);
            }
        }

        private static bool ValidateTexturePixels(Color32[] pixels, int atlasSize, TextureExportRole role, out TextureValidation validation, out string failure)
        {
            validation = default;
            failure = string.Empty;
            long expected = (long)atlasSize * atlasSize;
            long actual = pixels == null ? 0L : pixels.LongLength;
            if (actual != expected)
            {
                failure = "pixel count mismatch expected=" + expected.ToString(CultureInfo.InvariantCulture) +
                          " actual=" + actual.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            byte minSignal = byte.MaxValue;
            byte maxSignal = 0;
            byte alphaMin = byte.MaxValue;
            byte alphaMax = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                byte signal = role == TextureExportRole.SdfSingleChannel ? pixel.r : Max3(pixel.r, pixel.g, pixel.b);
                if (signal < minSignal)
                    minSignal = signal;
                if (signal > maxSignal)
                    maxSignal = signal;
                if (pixel.a < alphaMin)
                    alphaMin = pixel.a;
                if (pixel.a > alphaMax)
                    alphaMax = pixel.a;
            }

            validation = new TextureValidation(expected, actual, minSignal, maxSignal, alphaMin, alphaMax);
            if (maxSignal - minSignal < 8)
            {
                failure = "texture signal range is flat min=" + minSignal.ToString(CultureInfo.InvariantCulture) +
                          " max=" + maxSignal.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static bool TryConfigureTextureImporter(string assetPath, TextureExportRole role, int maxTextureSize, out string failure)
        {
            failure = string.Empty;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failure = "TextureImporter missing for " + assetPath;
                return false;
            }

            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = role == TextureExportRole.SdfSingleChannel
                ? TextureImporterType.SingleChannel
                : TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = role == TextureExportRole.SdfSingleChannel
                ? TextureImporterAlphaSource.None
                : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 2;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = maxTextureSize;

            TextureImporterFormat standaloneFormat = role == TextureExportRole.SdfSingleChannel
                ? TextureImporterFormat.BC4
                : TextureImporterFormat.BC7;
            SetPlatformSettings(importer, "Standalone", maxTextureSize, standaloneFormat);
            SetPlatformSettings(importer, "Android", maxTextureSize, TextureImporterFormat.ASTC_6x6);
            SetPlatformSettings(importer, "iPhone", maxTextureSize, TextureImporterFormat.ASTC_6x6);
            importer.SaveAndReimport();

            return AuditImporter(assetPath, role, maxTextureSize, standaloneFormat, out failure);
        }

        private static void SetPlatformSettings(TextureImporter importer, string platform, int maxTextureSize, TextureImporterFormat format)
        {
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = platform,
                overridden = true,
                maxTextureSize = maxTextureSize,
                format = format,
                compressionQuality = 100
            });
        }

        private static bool AuditImporter(
            string assetPath,
            TextureExportRole role,
            int maxTextureSize,
            TextureImporterFormat standaloneFormat,
            out string failure)
        {
            failure = string.Empty;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failure = "TextureImporter missing during audit for " + assetPath;
                return false;
            }

            bool typeCorrect = importer.textureType == (role == TextureExportRole.SdfSingleChannel
                ? TextureImporterType.SingleChannel
                : TextureImporterType.Default);
            bool srgbCorrect = !importer.sRGBTexture;
            bool readableCorrect = !importer.isReadable;
            bool mipCorrect = !importer.mipmapEnabled && !importer.streamingMipmaps;
            bool wrapCorrect = importer.wrapMode == TextureWrapMode.Clamp;
            bool filterCorrect = importer.filterMode == FilterMode.Bilinear;
            bool compressionCorrect = importer.textureCompression == TextureImporterCompression.CompressedHQ;
            bool sizeCorrect = importer.maxTextureSize == maxTextureSize;
            bool standaloneCorrect = IsPlatformFormat(importer, "Standalone", maxTextureSize, standaloneFormat);
            bool androidCorrect = IsPlatformFormat(importer, "Android", maxTextureSize, TextureImporterFormat.ASTC_6x6);
            bool iPhoneCorrect = IsPlatformFormat(importer, "iPhone", maxTextureSize, TextureImporterFormat.ASTC_6x6);

            if (typeCorrect && srgbCorrect && readableCorrect && mipCorrect && wrapCorrect && filterCorrect && compressionCorrect &&
                sizeCorrect && standaloneCorrect && androidCorrect && iPhoneCorrect)
            {
                return true;
            }

            failure = "typeCorrect=" + typeCorrect +
                      " srgbCorrect=" + srgbCorrect +
                      " readableCorrect=" + readableCorrect +
                      " mipCorrect=" + mipCorrect +
                      " wrapCorrect=" + wrapCorrect +
                      " filterCorrect=" + filterCorrect +
                      " compressionCorrect=" + compressionCorrect +
                      " sizeCorrect=" + sizeCorrect +
                      " standaloneCorrect=" + standaloneCorrect +
                      " androidCorrect=" + androidCorrect +
                      " iPhoneCorrect=" + iPhoneCorrect;
            return false;
        }

        private static bool IsPlatformFormat(TextureImporter importer, string platform, int maxTextureSize, TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            return settings.overridden && settings.maxTextureSize == maxTextureSize && settings.format == format;
        }

        private static void ApplyStaticRuntimePolicy(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.isMultiAtlasTexturesEnabled = false;
            LocalizationCjkFontBootstrap.SetClearDynamicDataOnBuild(fontAsset, false);
            EditorUtility.SetDirty(fontAsset);
        }

        private static bool TryBindRuntimeAtlasTexture(TMP_FontAsset fontAsset, string runtimeAtlasPath, int atlasSize, out string failure)
        {
            failure = string.Empty;
            if (fontAsset == null)
            {
                failure = "font asset is null during runtime atlas binding";
                return false;
            }

            Texture2D runtimeAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(runtimeAtlasPath);
            if (runtimeAtlas == null)
            {
                failure = "imported runtime atlas missing at " + runtimeAtlasPath;
                return false;
            }

            if (runtimeAtlas.width != atlasSize || runtimeAtlas.height != atlasSize)
            {
                failure = "runtime atlas dimensions mismatch path=" + runtimeAtlasPath +
                          " expected=" + atlasSize.ToString(CultureInfo.InvariantCulture) +
                          " actual=" + runtimeAtlas.width.ToString(CultureInfo.InvariantCulture) +
                          "x" + runtimeAtlas.height.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            fontAsset.atlasTextures = new[] { runtimeAtlas };

            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty atlasTextureProperty = serializedFontAsset.FindProperty("m_AtlasTexture");
            if (atlasTextureProperty != null)
                atlasTextureProperty.objectReferenceValue = runtimeAtlas;

            SerializedProperty atlasTexturesProperty = serializedFontAsset.FindProperty("m_AtlasTextures");
            if (atlasTexturesProperty != null)
            {
                atlasTexturesProperty.arraySize = 1;
                atlasTexturesProperty.GetArrayElementAtIndex(0).objectReferenceValue = runtimeAtlas;
            }

            SerializedProperty atlasTextureIndexProperty = serializedFontAsset.FindProperty("m_AtlasTextureIndex");
            if (atlasTextureIndexProperty != null)
                atlasTextureIndexProperty.intValue = 0;

            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fontAsset);
            return true;
        }

        private static bool TryValidateRuntimeAtlasBinding(TMP_FontAsset fontAsset, string runtimeAtlasPath, out string failure)
        {
            failure = string.Empty;
            if (fontAsset == null)
            {
                failure = "font asset is null during runtime atlas validation";
                return false;
            }

            Texture2D runtimeAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(runtimeAtlasPath);
            if (runtimeAtlas == null)
            {
                failure = "runtime atlas missing during validation at " + runtimeAtlasPath;
                return false;
            }

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            if (fontAsset.atlasTextureCount != 1 ||
                atlasTextures == null ||
                atlasTextures.Length != 1 ||
                !ReferenceEquals(atlasTextures[0], runtimeAtlas))
            {
                failure = "font asset atlasTextures[0] is not bound to imported runtime atlas " + runtimeAtlasPath;
                return false;
            }

            if (!ReferenceEquals(fontAsset.atlasTexture, runtimeAtlas))
            {
                failure = "font asset atlasTexture cache is not bound to imported runtime atlas " + runtimeAtlasPath;
                return false;
            }

            Material material = fontAsset.material;
            if (material == null || !ReferenceEquals(material.GetTexture(ShaderUtilities.ID_MainTex), runtimeAtlas))
            {
                failure = "font material is not bound to imported runtime atlas " + runtimeAtlasPath;
                return false;
            }

            return true;
        }

        private static void EnsureFontMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            Material material = fontAsset.material;
            if (material == null)
            {
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fontAsset));
                for (int i = 0; i < subAssets.Length; i++)
                {
                    material = subAssets[i] as Material;
                    if (material != null)
                        break;
                }
            }

            if (material == null)
            {
                Shader shader = Shader.Find("TextMeshPro/Distance Field");
                if (shader == null)
                    throw new InvalidOperationException("TextMeshPro/Distance Field shader not found.");

                material = new Material(shader)
                {
                    name = fontAsset.name.Replace(" SDF", " Atlas Material")
                };
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            Texture atlasTexture = ResolveAtlasTexture(fontAsset);
            if (atlasTexture != null)
            {
                material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
                material.SetFloat(ShaderUtilities.ID_TextureWidth, atlasTexture.width);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, atlasTexture.height);
                material.SetFloat(ShaderUtilities.ID_GradientScale, 10f);
            }

            fontAsset.material = material;
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(fontAsset);
        }

        private static Texture ResolveAtlasTexture(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return null;

            try
            {
                Texture[] atlasTextures = fontAsset.atlasTextures;
                if (atlasTextures == null || atlasTextures.Length == 0)
                    return fontAsset.material != null
                        ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
                        : null;

                return atlasTextures[0];
            }
            catch (MissingReferenceException)
            {
                return fontAsset.material != null
                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
                    : null;
            }
            catch (UnassignedReferenceException)
            {
                return fontAsset.material != null
                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
                    : null;
            }
        }

        private static bool TryValidateSingleAtlas(TMP_FontAsset fontAsset, string specName, out string failure)
        {
            failure = string.Empty;
            if (fontAsset == null)
            {
                failure = "font asset is null for " + specName;
                return false;
            }

            try
            {
                Texture[] atlasTextures = fontAsset.atlasTextures;
                int atlasCount = atlasTextures == null ? 0 : atlasTextures.Length;
                if (atlasCount == 1 && atlasTextures[0] != null)
                    return true;

                failure = "static localization bake generated " + atlasCount.ToString(CultureInfo.InvariantCulture) +
                          " atlases for " + specName +
                          "; increase atlas size/quality or reduce glyph seed before runtime";
                return false;
            }
            catch (MissingReferenceException)
            {
                failure = "missing atlas reference for " + specName;
                return false;
            }
            catch (UnassignedReferenceException)
            {
                failure = "unassigned atlas reference for " + specName;
                return false;
            }
        }

        private static void WireStaticFallbacks(TMP_FontAsset[] bakedFonts)
        {
            if (bakedFonts == null)
                return;

            for (int i = 0; i < bakedFonts.Length; i++)
            {
                TMP_FontAsset owner = bakedFonts[i];
                if (owner == null || owner.fallbackFontAssetTable == null)
                    continue;

                owner.fallbackFontAssetTable.Clear();
                EditorUtility.SetDirty(owner);
            }

            if (bakedFonts.Length == 0 || bakedFonts[0] == null || bakedFonts[0].fallbackFontAssetTable == null)
                return;

            TMP_FontAsset latinRoot = bakedFonts[0];
            for (int i = 1; i < bakedFonts.Length; i++)
            {
                TMP_FontAsset fallback = bakedFonts[i];
                if (fallback != null)
                    latinRoot.fallbackFontAssetTable.Add(fallback);
            }

            EditorUtility.SetDirty(latinRoot);
        }

        private static bool DeleteExistingGeneratedAsset(string assetPath, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrEmpty(assetPath))
            {
                failure = "asset path is empty";
                return false;
            }

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (existing != null)
            {
                if (AssetDatabase.DeleteAsset(assetPath))
                    return true;

                failure = "failed to delete previous generated asset " + assetPath;
                return false;
            }

            try
            {
                string absolutePath = ResolveProjectPath(assetPath);
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);

                string metaPath = absolutePath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);

                return true;
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                failure = "failed to delete previous generated asset file pair " + assetPath + ": " + ex.GetType().Name;
                return false;
            }
        }

        private static void CleanupGeneratedBakeOutputs(
            string fontAssetPath,
            string sdfBc4Path,
            string sdfRgbaPath,
            string msdfPath,
            TMP_FontAsset transientFont)
        {
            DeleteGeneratedAssetIfPresent(fontAssetPath);
            DeleteGeneratedAssetIfPresent(sdfBc4Path);
            DeleteGeneratedAssetIfPresent(sdfRgbaPath);
            DeleteGeneratedAssetIfPresent(msdfPath);

            if (transientFont != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(transientFont)))
                Object.DestroyImmediate(transientFont);
        }

        private static void CleanupGeneratedBakeOutputs(FontBakeOutput[] outputs, TMP_FontAsset[] transientFonts)
        {
            int outputCount = outputs == null ? 0 : outputs.Length;
            for (int i = 0; i < outputCount; i++)
            {
                TMP_FontAsset transientFont = transientFonts != null && i < transientFonts.Length ? transientFonts[i] : null;
                CleanupGeneratedBakeOutputs(
                    outputs[i].FontAssetPath,
                    outputs[i].SdfBc4Path,
                    outputs[i].SdfRgbaPath,
                    outputs[i].MsdfPath,
                    transientFont);
            }
        }

        private static void DeleteGeneratedAssetIfPresent(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
                return;
            }

            try
            {
                string absolutePath = ResolveProjectPath(assetPath);
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);

                string metaPath = absolutePath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                Debug.LogWarning("[SdfFontAtlasBaker1729] Generated cleanup failed for " + assetPath + ": " + ex.GetType().Name);
            }
        }

        private static GlyphCatalog BuildGlyphCatalog(float globalQualityWeight, bool includeLocalizationTables)
        {
            var latin = new HashSet<char>(2048);
            var arabic = new HashSet<char>(2048);
            var cjkSc = new HashSet<char>(8192);
            var cjkJp = new HashSet<char>(8192);
            int filesRead = 0;

            AppendAsciiAndPunctuation(latin);
            AppendRange(latin, 0x00A0, 0x00FF);
            AppendRange(latin, 0x0100, 0x017F);
            AppendRange(latin, 0x0400, 0x052F);
            AppendAsciiAndPunctuation(arabic);
            AppendRange(arabic, 0x0600, 0x06FF);
            AppendRange(arabic, 0x0750, 0x077F);
            AppendRange(arabic, 0x08A0, 0x08FF);
            AppendRange(arabic, 0xFB50, 0xFDFF);
            AppendRange(arabic, 0xFE70, 0xFEFF);
            AppendAsciiAndPunctuation(cjkSc);
            AppendAsciiAndPunctuation(cjkJp);
            LocalizationCjkFontBootstrap.AppendSeedCharacters(KoreanSentinelGlyphs, cjkSc);
            AppendRange(cjkJp, 0x3040, 0x309F);
            AppendRange(cjkJp, 0x30A0, 0x30FF);
            AppendRange(cjkJp, 0xFF65, 0xFF9F);

            int cjkSeedCount = ResolveCjkSeedCount(globalQualityWeight);
            AppendRangeCount(cjkSc, 0x4E00, cjkSeedCount);
            AppendRangeCount(cjkJp, 0x4E00, cjkSeedCount);

            if (includeLocalizationTables)
            {
                filesRead += AppendLocalizationFile(EnglishLocalizationPath, latin);
                filesRead += AppendLocalizationFile(RussianLocalizationPath, latin);
                filesRead += AppendLocalizationFile(ChineseLocalizationPath, cjkSc);
                filesRead += AppendLocalizationFile(JapaneseLocalizationPath, cjkJp);
                filesRead += AppendLocalizationFile(KoreanLocalizationPath, cjkSc);
                filesRead += AppendLocalizationFile(ArabicLocalizationPath, arabic);
            }

            string latinText = ToSortedString(latin);
            string arabicText = ToSortedString(arabic);
            string cjkScText = ToSortedString(cjkSc);
            string cjkJpText = ToSortedString(cjkJp);
            var stats = new GlyphCatalogStats(latinText.Length, arabicText.Length, cjkScText.Length, cjkJpText.Length, filesRead);
            return new GlyphCatalog(latinText, arabicText, cjkScText, cjkJpText, stats);
        }

        private static string ResolveGlyphsForCoverage(GlyphCatalog catalog, FontCoverage coverage)
        {
            switch (coverage)
            {
                case FontCoverage.Arabic:
                    return catalog.Arabic;
                case FontCoverage.CjkSc:
                    return catalog.CjkSc;
                case FontCoverage.CjkJp:
                    return catalog.CjkJp;
                default:
                    return catalog.LatinCyrillic;
            }
        }

        private static int AppendLocalizationFile(string assetPath, HashSet<char> target)
        {
            if (target == null)
                return 0;

            try
            {
                string absolutePath = ResolveProjectPath(assetPath);
                if (!File.Exists(absolutePath))
                    return 0;

                string text = File.ReadAllText(absolutePath, Encoding.UTF8);
                Dictionary<string, string> table = LocalizationEditorJsonTableParser.ParseFlatJsonTable(text);
                Dictionary<string, string>.Enumerator enumerator = table.GetEnumerator();
                while (enumerator.MoveNext())
                    LocalizationCjkFontBootstrap.AppendSeedCharacters(enumerator.Current.Value, target);

                return 1;
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                Debug.LogWarning("[SdfFontAtlasBaker1729] Skipped localization seed file " + assetPath + ": " + ex.GetType().Name);
                return 0;
            }
        }

        private static void AppendAsciiAndPunctuation(HashSet<char> target)
        {
            AppendRange(target, 0x0020, 0x007E);
            AppendRange(target, 0x2000, 0x206F);
            AppendRange(target, 0x20A0, 0x20CF);
            AppendRange(target, 0x2100, 0x214F);
            AppendRange(target, 0x2190, 0x21FF);
        }

        private static void AppendRange(HashSet<char> target, int firstInclusive, int lastInclusive)
        {
            if (target == null)
                return;

            for (int code = firstInclusive; code <= lastInclusive && code <= char.MaxValue; code++)
                target.Add((char)code);
        }

        private static void AppendRangeCount(HashSet<char> target, int firstInclusive, int count)
        {
            if (target == null || count <= 0)
                return;

            int lastExclusive = Mathf.Min(char.MaxValue + 1, firstInclusive + count);
            for (int code = firstInclusive; code < lastExclusive; code++)
                target.Add((char)code);
        }

        private static string ToSortedString(HashSet<char> target)
        {
            if (target == null || target.Count == 0)
                return string.Empty;

            char[] chars = new char[target.Count];
            target.CopyTo(chars);
            Array.Sort(chars);
            return new string(chars);
        }

        private static int CountUniqueCharacters(string source)
        {
            if (string.IsNullOrEmpty(source))
                return 0;

            var set = new HashSet<char>(source.Length);
            for (int i = 0; i < source.Length; i++)
                set.Add(source[i]);

            return set.Count;
        }

        private static bool TryEnsureAssetFolder(string assetFolder, out string normalized, out string failure)
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
            catch (Exception ex) when (ex is UnityException ||
                                       ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is InvalidOperationException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
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

        private static string SanitizeAssetNameForPath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            char[] chars = value.ToCharArray();
            bool hasValid = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= 'A' && c <= 'Z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_' ||
                             c == '-';
                if (valid)
                    hasValid = true;
                else
                    chars[i] = '_';
            }

            return hasValid ? new string(chars).Trim('_') : string.Empty;
        }

        private static bool TryWriteBytesAtomicAsset(string assetPath, byte[] bytes, out string failure)
        {
            failure = string.Empty;
            string tempPath = string.Empty;
            if (bytes == null || bytes.Length == 0)
            {
                failure = "no bytes to write";
                return false;
            }

            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
            {
                failure = "asset path must stay under Assets/: " + assetPath;
                return false;
            }

            try
            {
                string absolutePath = ResolveProjectPath(assetPath);
                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                tempPath = absolutePath + ".tmp." + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
                File.WriteAllBytes(tempPath, bytes);
                if (File.Exists(absolutePath))
                    File.Replace(tempPath, absolutePath, null, true);
                else
                    File.Move(tempPath, absolutePath);

                return true;
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    File.Delete(tempPath);

                failure = "asset write failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryFinalizeAssetDatabase(string operationName, out string failure)
        {
            failure = string.Empty;
            try
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException)
            {
                failure = operationName + " finalization failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool ValidateUnmanagedLayouts(out string failure)
        {
            failure = string.Empty;

            int textureValidationSize = UnsafeUtility.SizeOf<TextureValidation>();
            if ((textureValidationSize & 7) != 0)
            {
                failure = "TextureValidation size is not 8-byte aligned: " + textureValidationSize.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            int glyphCatalogStatsSize = UnsafeUtility.SizeOf<GlyphCatalogStats>();
            if ((glyphCatalogStatsSize & 7) != 0)
            {
                failure = "GlyphCatalogStats size is not 8-byte aligned: " + glyphCatalogStatsSize.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static int ResolveAtlasSize(float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            float continuous = Mathf.Lerp(MinimumAtlasSize, MaximumAtlasSize, q * q);
            int resolved = Mathf.Clamp(RoundUpPowerOfTwo(Mathf.CeilToInt(continuous)), MinimumAtlasSize, MaximumAtlasSize);
            if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 2048 && resolved > 2048)
                resolved = 2048;

            return resolved;
        }

        private static int ResolveSamplingPointSize(float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(MinimumSamplingPointSize, MaximumSamplingPointSize, q)), MinimumSamplingPointSize, MaximumSamplingPointSize);
        }

        private static int ResolvePadding(float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(MinimumPadding, MaximumPadding, q)), MinimumPadding, MaximumPadding);
        }

        private static int ResolveCjkSeedCount(float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            float continuous = Mathf.Lerp(384f, 4096f, q * q);
            return Mathf.Clamp(RoundUpPowerOfTwo(Mathf.CeilToInt(continuous)), 384, 4096);
        }

        private static int RoundUpPowerOfTwo(int value)
        {
            int v = Mathf.Max(1, value);
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        private static bool TryResolveKernel(ComputeShader compute, string kernelName, out int kernel, out string failure)
        {
            kernel = -1;
            failure = string.Empty;
            try
            {
                if (compute == null || !compute.HasKernel(kernelName))
                {
                    failure = "missing compute kernel " + kernelName;
                    return false;
                }

                kernel = compute.FindKernel(kernelName);
                return kernel >= 0;
            }
            catch (Exception ex) when (ex is UnityException || ex is ArgumentException || ex is InvalidOperationException)
            {
                failure = "compute kernel resolve failed for " + kernelName + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryResolveDispatch(int textureSize, uint groupSizeX, uint groupSizeY, out int groupsX, out int groupsY, out string failure)
        {
            groupsX = 0;
            groupsY = 0;
            failure = string.Empty;
            if (textureSize <= 0 || groupSizeX == 0u || groupSizeY == 0u || groupSizeX > int.MaxValue || groupSizeY > int.MaxValue)
            {
                failure = "invalid dispatch shape size=" + textureSize.ToString(CultureInfo.InvariantCulture) +
                          " groupX=" + groupSizeX.ToString(CultureInfo.InvariantCulture) +
                          " groupY=" + groupSizeY.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            groupsX = CeilDivide(textureSize, (int)groupSizeX);
            groupsY = CeilDivide(textureSize, (int)groupSizeY);
            if (groupsX <= 0 || groupsY <= 0)
            {
                failure = "resolved dispatch group count is invalid";
                return false;
            }

            return true;
        }

        private static int CeilDivide(int value, int divisor)
        {
            return divisor <= 0 ? 0 : (value + divisor - 1) / divisor;
        }

        private static long EstimateBlockCompressedBytes(int textureSize, long bytesPerBlock)
        {
            long blocks = ((long)textureSize + 3L) / 4L;
            return blocks * blocks * bytesPerBlock;
        }

        private static byte Max3(byte a, byte b, byte c)
        {
            return a > b ? (a > c ? a : c) : (b > c ? b : c);
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath))
                return string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

    }
}
#endif
