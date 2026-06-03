#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Bakers
{
    public sealed class UiScreenMeshProjector1721 : EditorWindow
    {
        private const string MenuRoot = "HECTON-8/Bakers/1721/";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Textures/UI";
        private const string ComputePath = "Assets/_Project/Art/Shaders/Include/UiScreenMeshProjector1721.compute";
        private const string DefaultScreenMeshPath = "Assets/_Project/Art/Meshes/M_Diegetic_HUD_V4_CurvedPanel.asset";
        private const int MinimumAlbedoResolution = 1024;
        private const int MaximumAlbedoResolution = 4096;
        private const int MinimumLutResolution = 512;
        private const int MaximumLutResolution = 2048;
        private const int ResolutionStep = 256;
        private const int KernelOutputLut = 0;
        private const int KernelOutputAlbedo = 1;
        private const int KernelOutputMrao = 2;
        private const int GlitchLoopFrameCount = 64;
        private const int ScreenProjectorParamStrideBytes = 72;
        private const long MaxEncodedBytes = 256L * 1024L * 1024L;

        private static readonly int ParamsId = Shader.PropertyToID("_ScreenProjectorParams1721");
        private static readonly int ProjectionLutId = Shader.PropertyToID("_ProjectionLut");
        private static readonly int AlbedoAtlasId = Shader.PropertyToID("_AlbedoAtlas");
        private static readonly int PackedMraoId = Shader.PropertyToID("_PackedMrao");
        private static readonly int OutputModeId = Shader.PropertyToID("_OutputMode");
        // COLD ALLOC: ScreenProjectorBakeParams1721[1] — single-struct compute upload scratch — owner: UiScreenMeshProjector1721
        private static readonly ScreenProjectorBakeParams1721[] ParamUploadScratch = new ScreenProjectorBakeParams1721[1];

        [SerializeField] private string assetName = "cockpit_terminal";
        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private ComputeShader projectorCompute;
        [SerializeField] private Mesh screenMeshPrototype;
        [SerializeField, Range(0f, 1f)] private float globalQualityWeight = 0.75f;
        [SerializeField, Range(-0.45f, 0.45f)] private float radialK1 = -0.18f;
        [SerializeField, Range(-0.25f, 0.25f)] private float radialK2 = 0.055f;
        [SerializeField, Range(0f, 1f)] private float distortionStrength = 1f;
        [SerializeField, Range(0f, 1f)] private float burnInStrength = 0.72f;
        [SerializeField, Range(16f, 260f)] private float scanlineDensity = 144f;
        [SerializeField, Range(0f, 1f)] private float glitchStrength = 0.68f;
        [SerializeField, Range(0f, 1f)] private float glassWear = 0.54f;
        [SerializeField, Range(0f, 2f)] private float phosphorGain = 1.15f;
        [SerializeField, Range(0f, 1f)] private float metallic = 0.24f;
        [SerializeField, Range(0f, 1f)] private float roughnessBase = 0.62f;
        [SerializeField, Range(0f, 1f)] private float aoBase = 0.84f;
        [SerializeField] private uint seed = 1721u;
        private string lastStatus = "Idle.";

        [MenuItem(MenuRoot + "Open Screen Mesh Projector", false, 1721)]
        private static void Open()
        {
            UiScreenMeshProjector1721 window = GetWindow<UiScreenMeshProjector1721>();
            window.titleContent = new GUIContent("UI Projector 1721");
            window.minSize = new Vector2(480f, 440f);
            if (window.projectorCompute == null)
                window.projectorCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            if (window.screenMeshPrototype == null)
                window.screenMeshPrototype = AssetDatabase.LoadAssetAtPath<Mesh>(DefaultScreenMeshPath);
        }

        [MenuItem(MenuRoot + "Bake Default Cockpit CRT Atlas", false, 1722)]
        private static void BakeDefaultMenu()
        {
            BakeSettings settings = BakeSettings.Default();
            if (TryBake(settings, out BakeResult result))
            {
                Debug.Log("[UiScreenMeshProjector1721] Baked CRT atlas set: " +
                          result.AlbedoPath +
                          " | " +
                          result.PackedMraoPath +
                          " | " +
                          result.ProjectionLutPath);
            }
        }

        private void OnEnable()
        {
            if (projectorCompute == null)
                projectorCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            if (screenMeshPrototype == null)
                screenMeshPrototype = AssetDatabase.LoadAssetAtPath<Mesh>(DefaultScreenMeshPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline CRT Screen Mesh Projector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            assetName = EditorGUILayout.TextField("Asset Name", assetName);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            projectorCompute = (ComputeShader)EditorGUILayout.ObjectField("Compute", projectorCompute, typeof(ComputeShader), false);
            screenMeshPrototype = (Mesh)EditorGUILayout.ObjectField("Screen Mesh", screenMeshPrototype, typeof(Mesh), false);
            globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", globalQualityWeight, 0f, 1f);
            radialK1 = EditorGUILayout.Slider("Radial K1", radialK1, -0.45f, 0.45f);
            radialK2 = EditorGUILayout.Slider("Radial K2", radialK2, -0.25f, 0.25f);
            distortionStrength = EditorGUILayout.Slider("Distortion Strength", distortionStrength, 0f, 1f);
            burnInStrength = EditorGUILayout.Slider("Burn-In Strength", burnInStrength, 0f, 1f);
            scanlineDensity = EditorGUILayout.Slider("Scanline Density", scanlineDensity, 16f, 260f);
            glitchStrength = EditorGUILayout.Slider("Glitch Strength", glitchStrength, 0f, 1f);
            glassWear = EditorGUILayout.Slider("Glass Wear", glassWear, 0f, 1f);
            phosphorGain = EditorGUILayout.Slider("Phosphor Gain", phosphorGain, 0f, 2f);
            metallic = EditorGUILayout.Slider("Metallic", metallic, 0f, 1f);
            roughnessBase = EditorGUILayout.Slider("Roughness Base", roughnessBase, 0f, 1f);
            aoBase = EditorGUILayout.Slider("AO Base", aoBase, 0f, 1f);

            ResolvedDimensions dimensions = ResolveDimensions(globalQualityWeight);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Albedo/MRAO", dimensions.AlbedoResolution.ToString(CultureInfo.InvariantCulture) + " px");
            EditorGUILayout.LabelField("Projection LUT", dimensions.LutResolution.ToString(CultureInfo.InvariantCulture) + " px");
            EditorGUILayout.LabelField("Glitch Loop", GlitchLoopFrameCount.ToString(CultureInfo.InvariantCulture) + " frame periodic alpha field");

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake CRT Projection Atlas Set", GUILayout.Height(32f)))
                BakeFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = new BakeSettings(
                assetName,
                outputFolder,
                projectorCompute,
                screenMeshPrototype,
                globalQualityWeight,
                radialK1,
                radialK2,
                distortionStrength,
                burnInStrength,
                scanlineDensity,
                glitchStrength,
                glassWear,
                phosphorGain,
                metallic,
                roughnessBase,
                aoBase,
                seed);

            if (TryBake(settings, out BakeResult result))
            {
                lastStatus = "Baked " +
                             result.AlbedoPath +
                             " | LUT=" +
                             result.LutResolution.ToString(CultureInfo.InvariantCulture) +
                             " | microseconds=" +
                             result.ElapsedMicroseconds.ToString("0.0", CultureInfo.InvariantCulture);
                return;
            }

            lastStatus = "Bake failed. Check Console.";
        }

        public static bool TryBake(in BakeSettings requestedSettings, out BakeResult result)
        {
            result = default;
            BakeSettings settings = requestedSettings.Sanitize();
            ResolvedDimensions dimensions = ResolveDimensions(settings.GlobalQualityWeight);
            ComputeShader compute = settings.ProjectorCompute != null
                ? settings.ProjectorCompute
                : AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            if (compute == null)
            {
                Debug.LogError("[UiScreenMeshProjector1721] Missing compute shader at " + ComputePath);
                return false;
            }

            if (!TryFindKernel(compute, out int kernel))
                return false;

            if (!TryValidateParameterLayout(out string layoutFailure))
            {
                Debug.LogError("[UiScreenMeshProjector1721] Parameter layout invalid: " + layoutFailure);
                return false;
            }

            if (!TryEnsureAssetFolder(settings.OutputFolder, out string safeOutputFolder, out string folderFailure))
            {
                Debug.LogError("[UiScreenMeshProjector1721] Output folder invalid: " + folderFailure);
                return false;
            }

            string safeName = SanitizeAssetNameForPath(settings.AssetName);
            if (string.IsNullOrEmpty(safeName))
                safeName = "cockpit_terminal";

            string albedoPath = safeOutputFolder + "/TX_Screen_" + safeName + "_Albedo.png";
            string mraoPath = safeOutputFolder + "/TX_Screen_" + safeName + "_MRAO.png";
            string lutPath = safeOutputFolder + "/TX_Screen_" + safeName + "_ProjectionLUT.exr";
            Stopwatch stopwatch = Stopwatch.StartNew();
            RenderTexture projectionLut = null;
            RenderTexture albedoAtlas = null;
            RenderTexture packedMrao = null;
            ComputeBuffer paramsBuffer = null;
            Texture2D projectionTexture = null;
            Texture2D albedoTexture = null;
            Texture2D mraoTexture = null;
            try
            {
                projectionLut = CreateTarget(dimensions.LutResolution, GraphicsFormat.R16G16B16A16_SFloat, "H8_1721_ProjectionLut");
                albedoAtlas = CreateTarget(dimensions.AlbedoResolution, GraphicsFormat.R8G8B8A8_UNorm, "H8_1721_AlbedoAtlas");
                packedMrao = CreateTarget(dimensions.AlbedoResolution, GraphicsFormat.R8G8B8A8_UNorm, "H8_1721_PackedMrao");

                ScreenProjectorBakeParams1721 parameters = BuildParams(in settings, dimensions);
                paramsBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<ScreenProjectorBakeParams1721>(), ComputeBufferType.Structured);
                ParamUploadScratch[0] = parameters;
                paramsBuffer.SetData(ParamUploadScratch);
                compute.SetBuffer(kernel, ParamsId, paramsBuffer);
                compute.SetTexture(kernel, ProjectionLutId, projectionLut);
                compute.SetTexture(kernel, AlbedoAtlasId, albedoAtlas);
                compute.SetTexture(kernel, PackedMraoId, packedMrao);

                DispatchOutput(compute, kernel, dimensions.LutResolution, KernelOutputLut);
                DispatchOutput(compute, kernel, dimensions.AlbedoResolution, KernelOutputAlbedo);
                DispatchOutput(compute, kernel, dimensions.AlbedoResolution, KernelOutputMrao);

                projectionTexture = ReadRenderTexture(projectionLut, TextureFormat.RGBAFloat, true);
                albedoTexture = ReadRenderTexture(albedoAtlas, TextureFormat.RGBA32, false);
                mraoTexture = ReadRenderTexture(packedMrao, TextureFormat.RGBA32, true);

                if (!ValidateProjectionLut(projectionTexture, dimensions.LutResolution, out string validationFailure))
                {
                    Debug.LogError("[UiScreenMeshProjector1721] UV LUT out of bounds violation detected! " + validationFailure);
                    return false;
                }

                string albedoFailure = string.Empty;
                string mraoFailure = string.Empty;
                if (!ValidatePixelCount(albedoTexture, dimensions.AlbedoResolution, out albedoFailure) ||
                    !ValidatePixelCount(mraoTexture, dimensions.AlbedoResolution, out mraoFailure))
                {
                    Debug.LogError("[UiScreenMeshProjector1721] Pixel count validation failed: " + albedoFailure + mraoFailure);
                    return false;
                }

                string albedoWriteFailure = string.Empty;
                string mraoWriteFailure = string.Empty;
                string lutWriteFailure = string.Empty;
                if (!TryWriteBytes(albedoPath, ImageConversion.EncodeToPNG(albedoTexture), out albedoWriteFailure) ||
                    !TryWriteBytes(mraoPath, ImageConversion.EncodeToPNG(mraoTexture), out mraoWriteFailure) ||
                    !TryWriteBytes(lutPath, ImageConversion.EncodeToEXR(projectionTexture, Texture2D.EXRFlags.OutputAsFloat), out lutWriteFailure))
                {
                    Debug.LogError("[UiScreenMeshProjector1721] Write failed: " + albedoWriteFailure + mraoWriteFailure + lutWriteFailure);
                    return false;
                }

                AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(mraoPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(lutPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                string albedoImportFailure = string.Empty;
                string mraoImportFailure = string.Empty;
                string lutImportFailure = string.Empty;
                if (!ConfigureTextureImporter(albedoPath, BakedTextureRole1721.Albedo, dimensions.AlbedoResolution, out albedoImportFailure) ||
                    !ConfigureTextureImporter(mraoPath, BakedTextureRole1721.PackedMrao, dimensions.AlbedoResolution, out mraoImportFailure) ||
                    !ConfigureTextureImporter(lutPath, BakedTextureRole1721.ProjectionLut, dimensions.LutResolution, out lutImportFailure))
                {
                    Debug.LogError("[UiScreenMeshProjector1721] Import settings failed: " + albedoImportFailure + mraoImportFailure + lutImportFailure);
                    return false;
                }

                AssetDatabase.SaveAssets();
                stopwatch.Stop();
                result = new BakeResult(
                    albedoPath,
                    mraoPath,
                    lutPath,
                    dimensions.AlbedoResolution,
                    dimensions.LutResolution,
                    GlitchLoopFrameCount,
                    stopwatch.Elapsed.TotalMilliseconds * 1000.0);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                Debug.LogError("[UiScreenMeshProjector1721] Bake exception: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                paramsBuffer?.Release();
                ReleaseTarget(projectionLut);
                ReleaseTarget(albedoAtlas);
                ReleaseTarget(packedMrao);
                DestroyImmediateSafe(projectionTexture);
                DestroyImmediateSafe(albedoTexture);
                DestroyImmediateSafe(mraoTexture);
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool TryFindKernel(ComputeShader compute, out int kernel)
        {
            kernel = -1;
            try
            {
                if (!compute.HasKernel("KScreenMeshProject1721"))
                {
                    Debug.LogError("[UiScreenMeshProjector1721] Compute shader has no KScreenMeshProject1721 kernel.");
                    return false;
                }

                kernel = compute.FindKernel("KScreenMeshProject1721");
                uint x;
                uint y;
                uint z;
                compute.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
                if (x == 0u || y == 0u || z != 1u || x * y > 256u)
                {
                    Debug.LogError("[UiScreenMeshProjector1721] Unsupported thread group shape.");
                    kernel = -1;
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException)
            {
                Debug.LogError("[UiScreenMeshProjector1721] Compute kernel validation failed: " + ex.GetType().Name + ": " + ex.Message);
                kernel = -1;
                return false;
            }
        }

        private static bool TryValidateParameterLayout(out string failure)
        {
            failure = string.Empty;
            int stride = UnsafeUtility.SizeOf<ScreenProjectorBakeParams1721>();
            if (stride != ScreenProjectorParamStrideBytes)
            {
                failure = "stride=" + stride.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if ((stride & 7) == 0)
                return true;

            failure = "stride not 8-byte aligned: " + stride.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        private static void DispatchOutput(ComputeShader compute, int kernel, int resolution, int outputMode)
        {
            uint x;
            uint y;
            uint z;
            compute.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            int groupsX = ResolveDispatchGroups(resolution, (int)x);
            int groupsY = ResolveDispatchGroups(resolution, (int)y);
            if (groupsX <= 0 || groupsY <= 0)
                throw new InvalidOperationException("Invalid dispatch group count for resolution " + resolution.ToString(CultureInfo.InvariantCulture));

            compute.SetInt(OutputModeId, outputMode);
            compute.Dispatch(kernel, groupsX, groupsY, 1);
        }

        private static RenderTexture CreateTarget(int resolution, GraphicsFormat format, string name)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, format, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            RenderTexture target = new RenderTexture(descriptor)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            return target;
        }

        private static Texture2D ReadRenderTexture(RenderTexture source, TextureFormat format, bool linear)
        {
            RenderTexture previous = RenderTexture.active;
            // Editor-only serialization readback; player builds never compile this baker.
            Texture2D texture = new Texture2D(source.width, source.height, format, false, linear)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static bool ValidateProjectionLut(Texture2D texture, int expectedResolution, out string failure)
        {
            failure = string.Empty;
            if (!ValidatePixelCount(texture, expectedResolution, out failure))
                return false;

            NativeArray<Color> pixels = texture.GetPixelData<Color>(0);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                if (float.IsNaN(p.r) || float.IsInfinity(p.r) ||
                    float.IsNaN(p.g) || float.IsInfinity(p.g) ||
                    float.IsNaN(p.b) || float.IsInfinity(p.b) ||
                    float.IsNaN(p.a) || float.IsInfinity(p.a) ||
                    p.r < 0f || p.r > 1f || p.g < 0f || p.g > 1f)
                {
                    failure = "index=" + i.ToString(CultureInfo.InvariantCulture) +
                              " uv=(" +
                              p.r.ToString("R", CultureInfo.InvariantCulture) +
                              "," +
                              p.g.ToString("R", CultureInfo.InvariantCulture) +
                              ") mask=(" +
                              p.b.ToString("R", CultureInfo.InvariantCulture) +
                              "," +
                              p.a.ToString("R", CultureInfo.InvariantCulture) +
                              ")";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidatePixelCount(Texture2D texture, int expectedResolution, out string failure)
        {
            failure = string.Empty;
            if (texture == null)
            {
                failure = "texture=null ";
                return false;
            }

            long expectedPixels = (long)expectedResolution * expectedResolution;
            long actualPixels = (long)texture.width * texture.height;
            if (actualPixels == expectedPixels)
                return true;

            failure = "expected=" + expectedPixels.ToString(CultureInfo.InvariantCulture) +
                      " actual=" +
                      actualPixels.ToString(CultureInfo.InvariantCulture) +
                      " ";
            return false;
        }

        private static bool TryWriteBytes(string assetPath, byte[] bytes, out string failure)
        {
            failure = string.Empty;
            if (bytes == null || bytes.Length == 0 || bytes.LongLength > MaxEncodedBytes)
            {
                failure = "Invalid encoded byte payload for " + assetPath + ". ";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(assetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(assetPath, bytes);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = assetPath + ": " + ex.GetType().Name + " ";
                return false;
            }
        }

        private static bool ConfigureTextureImporter(string assetPath, BakedTextureRole1721 role, int maxSize, out string failure)
        {
            failure = string.Empty;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failure = "Missing TextureImporter for " + assetPath + ". ";
                return false;
            }

            bool isProjectionLut = role == BakedTextureRole1721.ProjectionLut;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = role == BakedTextureRole1721.Albedo;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = isProjectionLut ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = maxSize;
            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = maxSize;
            standalone.format = isProjectionLut ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.BC7;
            standalone.textureCompression = isProjectionLut ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(standalone);
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = isProjectionLut ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.ASTC_6x6;
            android.textureCompression = isProjectionLut ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(android);
            TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.maxTextureSize = maxSize;
            ios.format = isProjectionLut ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.ASTC_6x6;
            ios.textureCompression = isProjectionLut ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(ios);
            importer.SaveAndReimport();
            return true;
        }

        private static bool TryEnsureAssetFolder(string requestedFolder, out string outputFolder, out string failure)
        {
            outputFolder = string.IsNullOrWhiteSpace(requestedFolder) ? DefaultOutputFolder : requestedFolder.Replace('\\', '/');
            failure = string.Empty;
            if (!outputFolder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                failure = "Folder must be under Assets/.";
                return false;
            }

            if (AssetDatabase.IsValidFolder(outputFolder))
                return true;

            string[] parts = outputFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        failure = "CreateFolder failed for " + next + ".";
                        return false;
                    }
                }
                current = next;
            }

            if (AssetDatabase.IsValidFolder(outputFolder))
                return true;

            failure = "Folder was not created: " + outputFolder + ".";
            return false;
        }

        private static int ResolveDispatchGroups(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            return (value + divisor - 1) / divisor;
        }

        private static ResolvedDimensions ResolveDimensions(float qualityWeight)
        {
            float q = Smooth01(qualityWeight);
            int albedo = RoundToStep(Mathf.RoundToInt(Mathf.Lerp(MinimumAlbedoResolution, MaximumAlbedoResolution, q)), ResolutionStep);
            int lut = RoundToStep(Mathf.RoundToInt(Mathf.Lerp(MinimumLutResolution, MaximumLutResolution, q)), ResolutionStep);
            return new ResolvedDimensions(
                Mathf.Clamp(albedo, MinimumAlbedoResolution, MaximumAlbedoResolution),
                Mathf.Clamp(lut, MinimumLutResolution, MaximumLutResolution));
        }

        private static int RoundToStep(int value, int step)
        {
            int safeStep = Mathf.Max(1, step);
            return Mathf.Max(safeStep, ((value + safeStep / 2) / safeStep) * safeStep);
        }

        private static float Smooth01(float value)
        {
            float q = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            return q * q * (3f - 2f * q);
        }

        private static void ResolveMeshProjection(Mesh screenMesh, out float screenAspect, out float meshCurvatureWeight)
        {
            screenAspect = 1f;
            meshCurvatureWeight = 0f;
            if (screenMesh == null)
                return;

            Bounds bounds = screenMesh.bounds;
            Vector3 size = bounds.size;
            float width = Mathf.Max(0.0001f, Mathf.Abs(size.x));
            float height = Mathf.Max(0.0001f, Mathf.Abs(size.y));
            float depth = Mathf.Max(0f, Mathf.Abs(size.z));
            screenAspect = Mathf.Clamp(width / height, 0.25f, 4f);
            meshCurvatureWeight = Mathf.Clamp01(depth / Mathf.Max(width, height));
        }

        private static ScreenProjectorBakeParams1721 BuildParams(in BakeSettings settings, ResolvedDimensions dimensions)
        {
            ResolveMeshProjection(settings.ScreenMeshPrototype, out float screenAspect, out float meshCurvatureWeight);
            return new ScreenProjectorBakeParams1721
            {
                AlbedoResolution = (uint)dimensions.AlbedoResolution,
                LutResolution = (uint)dimensions.LutResolution,
                OutputMode = 0u,
                Seed = settings.Seed,
                GlobalQualityWeight = settings.GlobalQualityWeight,
                K1 = settings.K1,
                K2 = settings.K2,
                DistortionStrength = settings.DistortionStrength,
                BurnInStrength = settings.BurnInStrength,
                ScanlineDensity = settings.ScanlineDensity,
                GlitchStrength = settings.GlitchStrength,
                GlassWear = settings.GlassWear,
                PhosphorGain = settings.PhosphorGain,
                Metallic = settings.Metallic,
                RoughnessBase = settings.RoughnessBase,
                AoBase = settings.AoBase,
                ScreenAspect = screenAspect,
                MeshCurvatureWeight = meshCurvatureWeight
            };
        }

        private static string SanitizeAssetNameForPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void ReleaseTarget(RenderTexture target)
        {
            if (target == null)
                return;
            target.Release();
            DestroyImmediate(target);
        }

        private static void DestroyImmediateSafe(UnityEngine.Object obj)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }

        private readonly struct ResolvedDimensions
        {
            public readonly int AlbedoResolution;
            public readonly int LutResolution;

            public ResolvedDimensions(int albedoResolution, int lutResolution)
            {
                AlbedoResolution = albedoResolution;
                LutResolution = lutResolution;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenProjectorBakeParams1721
        {
            public uint AlbedoResolution;
            public uint LutResolution;
            public uint OutputMode;
            public uint Seed;
            public float GlobalQualityWeight;
            public float K1;
            public float K2;
            public float DistortionStrength;
            public float BurnInStrength;
            public float ScanlineDensity;
            public float GlitchStrength;
            public float GlassWear;
            public float PhosphorGain;
            public float Metallic;
            public float RoughnessBase;
            public float AoBase;
            public float ScreenAspect;
            public float MeshCurvatureWeight;
        }

        private enum BakedTextureRole1721 : byte
        {
            Albedo = 0,
            PackedMrao = 1,
            ProjectionLut = 2
        }

        public readonly struct BakeSettings
        {
            public readonly string AssetName;
            public readonly string OutputFolder;
            public readonly ComputeShader ProjectorCompute;
            public readonly Mesh ScreenMeshPrototype;
            public readonly float GlobalQualityWeight;
            public readonly float K1;
            public readonly float K2;
            public readonly float DistortionStrength;
            public readonly float BurnInStrength;
            public readonly float ScanlineDensity;
            public readonly float GlitchStrength;
            public readonly float GlassWear;
            public readonly float PhosphorGain;
            public readonly float Metallic;
            public readonly float RoughnessBase;
            public readonly float AoBase;
            public readonly uint Seed;

            public BakeSettings(
                string assetName,
                string outputFolder,
                ComputeShader projectorCompute,
                Mesh screenMeshPrototype,
                float globalQualityWeight,
                float k1,
                float k2,
                float distortionStrength,
                float burnInStrength,
                float scanlineDensity,
                float glitchStrength,
                float glassWear,
                float phosphorGain,
                float metallic,
                float roughnessBase,
                float aoBase,
                uint seed)
            {
                AssetName = assetName;
                OutputFolder = outputFolder;
                ProjectorCompute = projectorCompute;
                ScreenMeshPrototype = screenMeshPrototype;
                GlobalQualityWeight = globalQualityWeight;
                K1 = k1;
                K2 = k2;
                DistortionStrength = distortionStrength;
                BurnInStrength = burnInStrength;
                ScanlineDensity = scanlineDensity;
                GlitchStrength = glitchStrength;
                GlassWear = glassWear;
                PhosphorGain = phosphorGain;
                Metallic = metallic;
                RoughnessBase = roughnessBase;
                AoBase = aoBase;
                Seed = seed;
            }

            public static BakeSettings Default()
            {
                return new BakeSettings(
                    "cockpit_terminal",
                    DefaultOutputFolder,
                    null,
                    AssetDatabase.LoadAssetAtPath<Mesh>(DefaultScreenMeshPath),
                    0.75f,
                    -0.18f,
                    0.055f,
                    1f,
                    0.72f,
                    144f,
                    0.68f,
                    0.54f,
                    1.15f,
                    0.24f,
                    0.62f,
                    0.84f,
                    1721u);
            }

            public BakeSettings Sanitize()
            {
                return new BakeSettings(
                    string.IsNullOrWhiteSpace(AssetName) ? "cockpit_terminal" : AssetName,
                    string.IsNullOrWhiteSpace(OutputFolder) ? DefaultOutputFolder : OutputFolder,
                    ProjectorCompute,
                    ScreenMeshPrototype,
                    Mathf.Clamp01(float.IsNaN(GlobalQualityWeight) || float.IsInfinity(GlobalQualityWeight) ? 0f : GlobalQualityWeight),
                    Mathf.Clamp(float.IsNaN(K1) || float.IsInfinity(K1) ? -0.18f : K1, -0.45f, 0.45f),
                    Mathf.Clamp(float.IsNaN(K2) || float.IsInfinity(K2) ? 0.055f : K2, -0.25f, 0.25f),
                    Mathf.Clamp01(float.IsNaN(DistortionStrength) || float.IsInfinity(DistortionStrength) ? 1f : DistortionStrength),
                    Mathf.Clamp01(float.IsNaN(BurnInStrength) || float.IsInfinity(BurnInStrength) ? 0.72f : BurnInStrength),
                    Mathf.Clamp(float.IsNaN(ScanlineDensity) || float.IsInfinity(ScanlineDensity) ? 144f : ScanlineDensity, 16f, 260f),
                    Mathf.Clamp01(float.IsNaN(GlitchStrength) || float.IsInfinity(GlitchStrength) ? 0.68f : GlitchStrength),
                    Mathf.Clamp01(float.IsNaN(GlassWear) || float.IsInfinity(GlassWear) ? 0.54f : GlassWear),
                    Mathf.Clamp(float.IsNaN(PhosphorGain) || float.IsInfinity(PhosphorGain) ? 1.15f : PhosphorGain, 0f, 2f),
                    Mathf.Clamp01(float.IsNaN(Metallic) || float.IsInfinity(Metallic) ? 0.24f : Metallic),
                    Mathf.Clamp01(float.IsNaN(RoughnessBase) || float.IsInfinity(RoughnessBase) ? 0.62f : RoughnessBase),
                    Mathf.Clamp01(float.IsNaN(AoBase) || float.IsInfinity(AoBase) ? 0.84f : AoBase),
                    Seed == 0u ? 1721u : Seed);
            }

        }

        public readonly struct BakeResult
        {
            public readonly string AlbedoPath;
            public readonly string PackedMraoPath;
            public readonly string ProjectionLutPath;
            public readonly int AlbedoResolution;
            public readonly int LutResolution;
            public readonly int GlitchLoopFrames;
            public readonly double ElapsedMicroseconds;

            public BakeResult(
                string albedoPath,
                string packedMraoPath,
                string projectionLutPath,
                int albedoResolution,
                int lutResolution,
                int glitchLoopFrames,
                double elapsedMicroseconds)
            {
                AlbedoPath = albedoPath;
                PackedMraoPath = packedMraoPath;
                ProjectionLutPath = projectionLutPath;
                AlbedoResolution = albedoResolution;
                LutResolution = lutResolution;
                GlitchLoopFrames = glitchLoopFrames;
                ElapsedMicroseconds = elapsedMicroseconds;
            }
        }
    }
}
#endif
