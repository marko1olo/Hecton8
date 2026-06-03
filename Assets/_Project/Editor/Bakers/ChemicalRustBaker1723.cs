#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Bakers
{
    public sealed class ChemicalRustBaker1723 : EditorWindow
    {
        private const string MenuRoot = "HECTON-8/Bakers/1723/";
        private const string ComputeShaderPath = "Assets/_Project/Art/Shaders/Include/ChemicalRustBaker1723.compute";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Generated/ChemicalRust1723";
        private const int MinimumAlbedoSize = 1024;
        private const int MaximumAlbedoSize = 4096;
        private const int MinimumMraoSize = 512;
        private const int MaximumMraoSize = 2048;
        private const long MaxEncodedPngBytes = 128L * 1024L * 1024L;

        private static readonly int s_outputId = Shader.PropertyToID("_ChemicalOutput");
        private static readonly int s_curvatureMapId = Shader.PropertyToID("_CurvatureMap");
        private static readonly int s_textureSizeId = Shader.PropertyToID("_ChemicalTextureSize");
        private static readonly int s_seedId = Shader.PropertyToID("_ChemicalSeed");
        private static readonly int s_qualityId = Shader.PropertyToID("_ChemicalQuality");
        private static readonly int s_layerParamsId = Shader.PropertyToID("_ChemicalLayerParams");
        private static readonly int s_flowParamsId = Shader.PropertyToID("_ChemicalFlowParams");

        private string _assetName = "industrial_wreck_oxidation";
        private string _outputFolder = DefaultOutputFolder;
        private ComputeShader _computeOverride;
        private Texture2D _curvatureMap;
        private int _seed = 1723001;
        private float _globalQualityWeight = 0.72f;
        private float _rustStrength = 0.82f;
        private float _verdigrisStrength = 0.38f;
        private float _oilStrength = 0.44f;
        private float _paintSurvival = 0.54f;
        private float _streakLength = 0.68f;
        private string _lastStatus = "Idle.";

        private enum TextureRole
        {
            Albedo,
            Mrao
        }

        private struct BakeSettings
        {
            public string AssetName;
            public string OutputFolder;
            public ComputeShader ComputeOverride;
            public Texture2D CurvatureMap;
            public uint Seed;
            public float GlobalQualityWeight;
            public float RustStrength;
            public float VerdigrisStrength;
            public float OilStrength;
            public float PaintSurvival;
            public float StreakLength;

            public static BakeSettings Default()
            {
                return new BakeSettings
                {
                    AssetName = "industrial_wreck_oxidation",
                    OutputFolder = DefaultOutputFolder,
                    ComputeOverride = null,
                    CurvatureMap = null,
                    Seed = 1723001u,
                    GlobalQualityWeight = 0.72f,
                    RustStrength = 0.82f,
                    VerdigrisStrength = 0.38f,
                    OilStrength = 0.44f,
                    PaintSurvival = 0.54f,
                    StreakLength = 0.68f
                };
            }
        }

        private readonly struct BakeResult
        {
            public readonly string AlbedoPath;
            public readonly string MraoPath;
            public readonly int AlbedoSize;
            public readonly int MraoSize;
            public readonly double DurationMs;
            public readonly BakeValidation MraoValidation;

            public BakeResult(string albedoPath, string mraoPath, int albedoSize, int mraoSize, double durationMs, BakeValidation mraoValidation)
            {
                AlbedoPath = albedoPath;
                MraoPath = mraoPath;
                AlbedoSize = albedoSize;
                MraoSize = mraoSize;
                DurationMs = durationMs;
                MraoValidation = mraoValidation;
            }
        }

        private readonly struct BakeValidation
        {
            public readonly long ExpectedPixelCount;
            public readonly long ActualPixelCount;
            public readonly byte MetallicMax;
            public readonly byte RoughnessMin;
            public readonly byte RoughnessMax;
            public readonly byte AoMin;
            public readonly byte AoMax;
            public readonly byte EmissionMax;

            public BakeValidation(
                long expectedPixelCount,
                long actualPixelCount,
                byte metallicMax,
                byte roughnessMin,
                byte roughnessMax,
                byte aoMin,
                byte aoMax,
                byte emissionMax)
            {
                ExpectedPixelCount = expectedPixelCount;
                ActualPixelCount = actualPixelCount;
                MetallicMax = metallicMax;
                RoughnessMin = roughnessMin;
                RoughnessMax = roughnessMax;
                AoMin = aoMin;
                AoMax = aoMax;
                EmissionMax = emissionMax;
            }
        }

        [MenuItem(MenuRoot + "Open Chemical Rust Baker", false, 1723)]
        private static void Open()
        {
            ChemicalRustBaker1723 window = GetWindow<ChemicalRustBaker1723>();
            window.titleContent = new GUIContent("Rust Baker 1723");
            window.minSize = new Vector2(460f, 390f);
        }

        [MenuItem(MenuRoot + "Bake Default Chemical Rust MRAO", false, 1724)]
        private static void BakeDefaultMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryBake(settings, out BakeResult result, out string failure))
            {
                Debug.Log("[ChemicalRustBaker1723] Baked albedo=" + result.AlbedoPath +
                          " mrao=" + result.MraoPath +
                          " durationMs=" + result.DurationMs.ToString("0.00", CultureInfo.InvariantCulture));
            }
            else
            {
                Debug.LogError("[ChemicalRustBaker1723] " + failure);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Industrial Rust And Chemical Oxidation", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _computeOverride = (ComputeShader)EditorGUILayout.ObjectField("Compute Override", _computeOverride, typeof(ComputeShader), false);
            _curvatureMap = (Texture2D)EditorGUILayout.ObjectField("Curvature Map", _curvatureMap, typeof(Texture2D), false);
            _seed = EditorGUILayout.IntField("Seed", Mathf.Max(1, _seed));
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _rustStrength = EditorGUILayout.Slider("Layered Rust", _rustStrength, 0f, 1f);
            _verdigrisStrength = EditorGUILayout.Slider("Copper Verdigris", _verdigrisStrength, 0f, 1f);
            _oilStrength = EditorGUILayout.Slider("Oil Streaks", _oilStrength, 0f, 1f);
            _paintSurvival = EditorGUILayout.Slider("Paint Survival", _paintSurvival, 0f, 1f);
            _streakLength = EditorGUILayout.Slider("Streak Length", _streakLength, 0f, 1f);

            EditorGUILayout.Space(6f);
            int albedoSize = ResolveTextureSize(MinimumAlbedoSize, MaximumAlbedoSize, _globalQualityWeight);
            int mraoSize = ResolveTextureSize(MinimumMraoSize, MaximumMraoSize, _globalQualityWeight);
            EditorGUILayout.LabelField("Albedo", albedoSize + " x " + albedoSize);
            EditorGUILayout.LabelField("MRAO", mraoSize + " x " + mraoSize);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Chemical Rust Textures", GUILayout.Height(32f)))
                BakeFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = BakeSettings.Default();
            settings.AssetName = _assetName;
            settings.OutputFolder = _outputFolder;
            settings.ComputeOverride = _computeOverride;
            settings.CurvatureMap = _curvatureMap;
            settings.Seed = unchecked((uint)Mathf.Max(1, _seed));
            settings.GlobalQualityWeight = _globalQualityWeight;
            settings.RustStrength = _rustStrength;
            settings.VerdigrisStrength = _verdigrisStrength;
            settings.OilStrength = _oilStrength;
            settings.PaintSurvival = _paintSurvival;
            settings.StreakLength = _streakLength;

            if (TryBake(settings, out BakeResult result, out string failure))
            {
                _lastStatus = "Baked " + result.AlbedoPath +
                              " | " + result.MraoPath +
                              " | ms=" + result.DurationMs.ToString("0.00", CultureInfo.InvariantCulture);
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

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(settings.OutputFolder, out string normalizedFolder, out failure))
                return false;

            string safeName = ProceduralTextureBaker.SanitizeAssetNameForPath(settings.AssetName);
            if (string.IsNullOrEmpty(safeName))
                safeName = "chemical_rust_" + settings.Seed.ToString("X8", CultureInfo.InvariantCulture);

            int albedoSize = ResolveTextureSize(MinimumAlbedoSize, MaximumAlbedoSize, settings.GlobalQualityWeight);
            int mraoSize = ResolveTextureSize(MinimumMraoSize, MaximumMraoSize, settings.GlobalQualityWeight);
            string albedoPath = normalizedFolder + "/TX_" + safeName + "_Albedo.png";
            string mraoPath = normalizedFolder + "/TX_" + safeName + "_MRAO.png";

            if (!ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(albedoPath, mraoPath, out ProceduralTextureBaker.AssetFileRollbackSnapshot[] rollback, out failure))
                return false;

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (!TryDispatchWriteImport(compute, "GenerateChemicalAlbedo", settings, albedoSize, TextureRole.Albedo, albedoPath, out _, out failure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                if (!TryDispatchWriteImport(compute, "GenerateChemicalMrao", settings, mraoSize, TextureRole.Mrao, mraoPath, out BakeValidation mraoValidation, out failure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("chemical rust bake 1723", out failure))
                {
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                    return false;
                }

                stopwatch.Stop();
                result = new BakeResult(albedoPath, mraoPath, albedoSize, mraoSize, stopwatch.Elapsed.TotalMilliseconds, mraoValidation);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                stopwatch.Stop();
                failure = ex.GetType().Name + ": " + ex.Message;
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                return false;
            }
        }

        private static bool TryDispatchWriteImport(
            ComputeShader compute,
            string kernelName,
            BakeSettings settings,
            int textureSize,
            TextureRole role,
            string assetPath,
            out BakeValidation validation,
            out string failure)
        {
            validation = default;
            failure = string.Empty;

            if (!TryResolveKernel(compute, kernelName, out int kernel, out failure))
                return false;

            compute.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out _);
            if (!TryResolveDispatch(textureSize, groupSizeX, groupSizeY, out int groupsX, out int groupsY, out failure))
                return false;

            RenderTexture rt = null;
            Texture2D staging = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGB32, 0)
                {
                    enableRandomWrite = true,
                    mipCount = 1,
                    msaaSamples = 1,
                    sRGB = false
                };

                rt = new RenderTexture(descriptor)
                {
                    name = "RT_ChemicalRust1723_" + kernelName,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };

                if (!rt.Create())
                {
                    failure = "RenderTexture allocation failed for " + kernelName + " at " + textureSize.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                Texture2D curvature = settings.CurvatureMap != null ? settings.CurvatureMap : Texture2D.blackTexture;
                compute.SetTexture(kernel, s_outputId, rt);
                compute.SetTexture(kernel, s_curvatureMapId, curvature);
                compute.SetVector(s_textureSizeId, new Vector4(textureSize, textureSize, settings.CurvatureMap != null ? 1f : 0f, role == TextureRole.Mrao ? 1f : 0f));
                compute.SetInt(s_seedId, unchecked((int)(settings.Seed == 0u ? 1u : settings.Seed)));
                compute.SetFloat(s_qualityId, Mathf.Clamp01(settings.GlobalQualityWeight));
                compute.SetVector(s_layerParamsId, new Vector4(
                    Mathf.Clamp01(settings.RustStrength),
                    Mathf.Clamp01(settings.VerdigrisStrength),
                    Mathf.Clamp01(settings.OilStrength),
                    Mathf.Clamp01(settings.PaintSurvival)));
                compute.SetVector(s_flowParamsId, new Vector4(
                    Mathf.Clamp01(settings.StreakLength),
                    Mathf.Lerp(0.12f, 0.38f, Mathf.Clamp01(settings.GlobalQualityWeight)),
                    Mathf.Lerp(32f, 128f, Mathf.Clamp01(settings.GlobalQualityWeight)),
                    0f));

                compute.Dispatch(kernel, groupsX, groupsY, 1);
                RenderTexture.active = rt;
                staging = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, role == TextureRole.Mrao);
                staging.ReadPixels(new Rect(0f, 0f, textureSize, textureSize), 0, 0, false);
                staging.Apply(false, false);

                Color32[] pixels = staging.GetPixels32();
                if (!ValidatePixels(pixels, textureSize, role, out validation, out failure))
                    return false;

                byte[] png = ImageConversion.EncodeToPNG(staging);
                if (png == null || png.Length == 0)
                {
                    failure = "EncodeToPNG returned no bytes for " + assetPath;
                    return false;
                }

                if (png.LongLength > MaxEncodedPngBytes)
                {
                    failure = "encoded PNG byte ceiling exceeded for " + assetPath;
                    return false;
                }

                if (!ProceduralTextureBaker.TryWriteBytesAtomic(assetPath, png, out failure))
                    return false;

                if (!ProceduralTextureBaker.TryEnforceTextureImportSettings(
                        assetPath,
                        role == TextureRole.Albedo,
                        false,
                        TextureWrapMode.Repeat,
                        FilterMode.Trilinear,
                        textureSize,
                        TextureImporterFormat.BC7,
                        true,
                        role == TextureRole.Albedo ? 4 : 2,
                        out failure))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }

                if (staging != null)
                    Object.DestroyImmediate(staging);
            }
        }

        private static bool ValidatePixels(Color32[] pixels, int textureSize, TextureRole role, out BakeValidation validation, out string failure)
        {
            validation = default;
            failure = string.Empty;

            long expected = (long)textureSize * textureSize;
            long actual = pixels == null ? 0L : pixels.LongLength;
            if (actual != expected)
            {
                failure = "pixel count mismatch expected=" + expected.ToString(CultureInfo.InvariantCulture) +
                          " actual=" + actual.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            byte metallicMax = 0;
            byte roughnessMin = byte.MaxValue;
            byte roughnessMax = 0;
            byte aoMin = byte.MaxValue;
            byte aoMax = 0;
            byte emissionMax = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.r > metallicMax)
                    metallicMax = pixel.r;
                if (pixel.g < roughnessMin)
                    roughnessMin = pixel.g;
                if (pixel.g > roughnessMax)
                    roughnessMax = pixel.g;
                if (pixel.b < aoMin)
                    aoMin = pixel.b;
                if (pixel.b > aoMax)
                    aoMax = pixel.b;
                if (pixel.a > emissionMax)
                    emissionMax = pixel.a;
            }

            validation = new BakeValidation(expected, actual, metallicMax, roughnessMin, roughnessMax, aoMin, aoMax, emissionMax);
            if (role == TextureRole.Albedo)
                return true;

            if (metallicMax < 96)
            {
                failure = "MRAO metallic channel never exposes believable bare steel; max=" + metallicMax.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (roughnessMax - roughnessMin < 24)
            {
                failure = "MRAO roughness channel is flat; min=" + roughnessMin.ToString(CultureInfo.InvariantCulture) +
                          " max=" + roughnessMax.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (aoMax - aoMin < 12)
            {
                failure = "MRAO AO channel is flat; min=" + aoMin.ToString(CultureInfo.InvariantCulture) +
                          " max=" + aoMax.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static bool TryResolveKernel(ComputeShader compute, string kernelName, out int kernel, out string failure)
        {
            kernel = -1;
            failure = string.Empty;
            try
            {
                if (!compute.HasKernel(kernelName))
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

        private static int ResolveTextureSize(int minimum, int maximum, float globalQualityWeight)
        {
            float q = Mathf.Clamp01(globalQualityWeight);
            float continuous = Mathf.Lerp(minimum, maximum, q * q);
            return Mathf.Clamp(RoundUpPowerOfTwo(Mathf.CeilToInt(continuous)), minimum, maximum);
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

        private static int CeilDivide(int value, int divisor)
        {
            return divisor <= 0 ? 0 : (value + divisor - 1) / divisor;
        }

        private static bool ValidateUnmanagedLayouts(out string failure)
        {
            failure = string.Empty;
            int validationSize = UnsafeUtility.SizeOf<BakeValidation>();
            if ((validationSize & 7) == 0)
                return true;

            failure = "BakeValidation size must be 8-byte aligned; size=" + validationSize.ToString(CultureInfo.InvariantCulture);
            return false;
        }
    }
}
#endif
