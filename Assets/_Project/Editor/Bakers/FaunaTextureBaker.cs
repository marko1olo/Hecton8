#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Bakers
{
    public sealed class FaunaTextureBaker : EditorWindow
    {
        private const string MenuRoot = "HECTON-8/Bakers/1725/";
        private const string DefaultComputeShaderPath = "Assets/_Project/Art/Shaders/Include/FaunaTextureBaker.compute";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Textures/Creatures/Fauna1725";
        private const string DefaultAssetName = "AbyssalPredator_1725";
        private const string MaskContractName = "LeviathanOrganicMaskV1";
        private const int MinimumAlbedoResolution = 1024;
        private const int MaximumAlbedoResolution = 4096;
        private const int MinimumDetailResolution = 512;
        private const int MaximumDetailResolution = 2048;
        private const int MinimumBiolumTileResolution = 64;
        private const int MaximumBiolumTileResolution = 256;
        private const int BiolumFrameCount = 64;
        private const int BiolumTilesPerAxis = 8;
        private const int MaxSkeletonPathPoints = 8;
        private const long MaxEncodedTextureBytes = 384L * 1024L * 1024L;
        private const long Bc7BytesPerPixel = 1L;
        private const int UvMetricVertexCapacity = 65536;
        private const int UvMetricIndexCapacity = 196608;
        private const float UvAreaEpsilon = 0.000001f;
        private const float UvFatalStretchRatio = 1.50f;
        private const int BiolumPulseMaxLoopSeamByteDelta = 2;
        private static readonly int s_baseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int s_normalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int s_maskMapId = Shader.PropertyToID("_MaskMap");

        // COLD ALLOC: List<Vector3>[65536] — editor UV metric scratch, 786432 bytes — owner: FaunaTextureBaker
        private static readonly List<Vector3> s_uvMetricVertices = new List<Vector3>(UvMetricVertexCapacity);
        // COLD ALLOC: List<Vector2>[65536] — editor UV metric scratch, 524288 bytes — owner: FaunaTextureBaker
        private static readonly List<Vector2> s_uvMetricUvs = new List<Vector2>(UvMetricVertexCapacity);
        // COLD ALLOC: List<int>[196608] — editor UV metric index scratch, 786432 bytes — owner: FaunaTextureBaker
        private static readonly List<int> s_uvMetricIndices = new List<int>(UvMetricIndexCapacity);

        [SerializeField] private SkinnedMeshRenderer _sourceRenderer;
        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private Material _targetSharedMaterial;
        [SerializeField] private ComputeShader _computeShader;
        [SerializeField] private string _assetName = DefaultAssetName;
        [SerializeField] private string _outputFolder = DefaultOutputFolder;
        [SerializeField, Range(0f, 1f)] private float _globalQualityWeight = 1f;
        [SerializeField] private float _seed = 1725f;
        [SerializeField, Range(0.02f, 0.45f)] private float _displacementMeters = 0.14f;
        [SerializeField, Range(0f, 2f)] private float _wrinkleGain = 1f;
        [SerializeField, Range(0f, 2f)] private float _poreGain = 1f;
        [SerializeField, Range(0f, 2f)] private float _chitinGain = 1f;
        [SerializeField, Range(0f, 2f)] private float _biolumGain = 1f;
        [SerializeField] private bool _writeBiolumPulseAtlas = true;
        [SerializeField] private string _lastStatus = "Idle.";

        [MenuItem(MenuRoot + "Open Fauna Texture Baker", false, 1725)]
        private static void Open()
        {
            FaunaTextureBaker window = GetWindow<FaunaTextureBaker>();
            window.titleContent = new GUIContent("Fauna Baker 1725");
            window.minSize = new Vector2(520f, 620f);
        }

        [MenuItem(MenuRoot + "Bake Default Fauna Package", false, 1726)]
        private static void BakeDefault()
        {
            if (TryBake(BakeSettings.Default(), out BakeResult result))
            {
                Debug.Log("[FaunaTextureBaker1725] Baked fauna package: " +
                          result.AlbedoPath + " | " + result.NormalMapPath + " | " + result.MaskPath);
            }
        }

        [MenuItem(MenuRoot + "Dry Run Compute Kernels", false, 1727)]
        private static void DryRunKernels()
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath);
            if (TryDryRun(shader, out string failure))
            {
                Debug.Log("[FaunaTextureBaker1725] Dry-run dispatch succeeded.");
                return;
            }

            Debug.LogError("[FaunaTextureBaker1725] Dry-run dispatch failed: " + failure);
        }

        private void OnEnable()
        {
            if (_computeShader == null)
                _computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Fauna Skin, Scales, And Bioluminescence Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _sourceRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Source Renderer", _sourceRenderer, typeof(SkinnedMeshRenderer), true);
            _sourceMesh = (Mesh)EditorGUILayout.ObjectField("Fallback Mesh", _sourceMesh, typeof(Mesh), false);
            _targetSharedMaterial = (Material)EditorGUILayout.ObjectField("Target Shared Material", _targetSharedMaterial, typeof(Material), false);
            _computeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", _computeShader, typeof(ComputeShader), false);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _seed = EditorGUILayout.FloatField("Seed", _seed);
            _displacementMeters = EditorGUILayout.Slider("Displacement Meters", _displacementMeters, 0.02f, 0.45f);
            _wrinkleGain = EditorGUILayout.Slider("Wrinkle Gain", _wrinkleGain, 0f, 2f);
            _poreGain = EditorGUILayout.Slider("Pore Gain", _poreGain, 0f, 2f);
            _chitinGain = EditorGUILayout.Slider("Chitin Gain", _chitinGain, 0f, 2f);
            _biolumGain = EditorGUILayout.Slider("Biolum Gain", _biolumGain, 0f, 2f);
            _writeBiolumPulseAtlas = EditorGUILayout.Toggle("Write Pulse Atlas", _writeBiolumPulseAtlas);

            BakeDimensions dimensions = ResolveDimensions(_globalQualityWeight);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Albedo", FormatResolution(dimensions.AlbedoResolution));
            EditorGUILayout.LabelField("Normal/Displacement", FormatResolution(dimensions.DetailResolution));
            EditorGUILayout.LabelField("Packed Mask", FormatResolution(dimensions.MaskResolution) + " | " + MaskContractName);
            EditorGUILayout.LabelField("Pulse Atlas", FormatResolution(dimensions.BiolumAtlasResolution) + " | " +
                                                        dimensions.BiolumFrames.ToString(CultureInfo.InvariantCulture) + " frames");

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Fauna Package", GUILayout.Height(32f)))
                BakeFromWindow();

            if (GUILayout.Button("Dry Run Kernels", GUILayout.Height(28f)))
                DryRunFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = BakeSettings.Default();
            settings.SourceRenderer = _sourceRenderer;
            settings.SourceMesh = _sourceMesh;
            settings.TargetSharedMaterial = _targetSharedMaterial;
            settings.ComputeShader = _computeShader;
            settings.AssetName = _assetName;
            settings.OutputFolder = _outputFolder;
            settings.GlobalQualityWeight = _globalQualityWeight;
            settings.Seed = _seed;
            settings.DisplacementMeters = _displacementMeters;
            settings.WrinkleGain = _wrinkleGain;
            settings.PoreGain = _poreGain;
            settings.ChitinGain = _chitinGain;
            settings.BiolumGain = _biolumGain;
            settings.WriteBiolumPulseAtlas = _writeBiolumPulseAtlas;

            if (TryBake(settings, out BakeResult result))
            {
                _lastStatus = "Baked: " + result.MaskPath;
                return;
            }

            _lastStatus = "Bake failed. Check Console.";
        }

        private void DryRunFromWindow()
        {
            ComputeShader shader = _computeShader != null ? _computeShader : AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath);
            if (TryDryRun(shader, out string failure))
            {
                _lastStatus = "Dry-run dispatch passed.";
                return;
            }

            _lastStatus = "Dry-run failed: " + failure;
            Debug.LogError("[FaunaTextureBaker1725] " + failure);
        }

        public static bool TryBake(BakeSettings requestedSettings, out BakeResult result)
        {
            result = default;
            if (!ValidateUnmanagedDtoLayout(out string layoutFailure))
            {
                Debug.LogError("[FaunaTextureBaker1725] " + layoutFailure);
                return false;
            }

            BakeSettings settings = SanitizeSettings(requestedSettings);
            BakeDimensions dimensions = ResolveDimensions(settings.GlobalQualityWeight);

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError("[FaunaTextureBaker1725] Compute shaders are unavailable on this editor device.");
                return false;
            }

            ComputeShader computeShader = settings.ComputeShader != null
                ? settings.ComputeShader
                : AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath);

            if (computeShader == null)
            {
                Debug.LogError("[FaunaTextureBaker1725] Missing compute shader at " + DefaultComputeShaderPath);
                return false;
            }

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(settings.OutputFolder, out string outputFolder, out string folderFailure))
            {
                Debug.LogError("[FaunaTextureBaker1725] Output folder invalid: " + folderFailure);
                return false;
            }

            string safeName = ProceduralTextureBaker.SanitizeAssetNameForPath(settings.AssetName);
            if (string.IsNullOrEmpty(safeName))
                safeName = DefaultAssetName;

            string albedoPath = outputFolder + "/TX_Fauna1725_Albedo_" + safeName + ".png";
            string normalMapPath = outputFolder + "/TX_Fauna1725_Normal_" + safeName + ".png";
            string maskPath = outputFolder + "/TX_Fauna1725_MaskV1_" + safeName + ".png";
            string biolumPulsePath = outputFolder + "/TX_Fauna1725_BiolumPulse64_" + safeName + ".png";

            RenderTexture albedoRt = null;
            RenderTexture normalMapRt = null;
            RenderTexture maskRt = null;
            RenderTexture biolumPulseRt = null;
            Texture2D albedoTexture = null;
            Texture2D normalMapTexture = null;
            Texture2D maskTexture = null;
            Texture2D biolumPulseTexture = null;
            GraphicsBuffer skeletonBuffer = null;
            ProceduralTextureBaker.AssetFileRollbackSnapshot[] rollbackSnapshots = null;
            MaterialTextureRollbackSnapshot materialRollbackSnapshot = default;
            bool restoreAssetSnapshots = false;
            bool restoreMaterialSnapshot = false;

            try
            {
                Mesh sourceMesh = ResolveSourceMesh(settings.SourceRenderer, settings.SourceMesh);
                MeshMetrics meshMetrics = ResolveMeshMetrics(sourceMesh);
                if (!TryResolveUvMetrics(sourceMesh, dimensions.DetailResolution, out UvMetrics uvMetrics, out string uvFailure))
                {
                    Debug.LogError("[FaunaTextureBaker1725] UV metric gate failed: " + uvFailure);
                    return false;
                }

                if (!TryCreateSkeletonPathBuffer(settings.SourceRenderer, meshMetrics, out skeletonBuffer, out int skeletonPointCount, out string skeletonSource, out string skeletonFailure))
                {
                    Debug.LogError("[FaunaTextureBaker1725] Skeleton path buffer failed: " + skeletonFailure);
                    return false;
                }

                albedoRt = CreateRenderTexture(dimensions.AlbedoResolution, dimensions.AlbedoResolution, RenderTextureFormat.ARGB32, TextureWrapMode.Clamp, "Fauna1725_Albedo_RT");
                normalMapRt = CreateRenderTexture(dimensions.DetailResolution, dimensions.DetailResolution, RenderTextureFormat.ARGB32, TextureWrapMode.Clamp, "Fauna1725_Normal_RT");
                maskRt = CreateRenderTexture(dimensions.MaskResolution, dimensions.MaskResolution, RenderTextureFormat.ARGB32, TextureWrapMode.Clamp, "Fauna1725_MaskV1_RT");
                if (settings.WriteBiolumPulseAtlas)
                    biolumPulseRt = CreateRenderTexture(dimensions.BiolumAtlasResolution, dimensions.BiolumAtlasResolution, RenderTextureFormat.ARGB32, TextureWrapMode.Repeat, "Fauna1725_BiolumPulse64_RT");

                ConfigureComputeConstants(computeShader, settings, dimensions, meshMetrics, skeletonBuffer, skeletonPointCount);
                DispatchKernel(computeShader, "CSBakeFaunaAlbedo", "_OutputAlbedo", albedoRt);
                DispatchKernel(computeShader, "CSBakeFaunaNormalMap", "_OutputNormalMap", normalMapRt);
                DispatchKernel(computeShader, "CSBakeFaunaMaskV1", "_OutputMask", maskRt);
                if (settings.WriteBiolumPulseAtlas)
                    DispatchKernel(computeShader, "CSBakeFaunaBiolumPulse64", "_OutputBiolumPulse", biolumPulseRt);

                albedoTexture = ReadbackTexture(albedoRt, TextureFormat.RGBA32, false, TextureWrapMode.Clamp, "Fauna1725_Albedo_CPU");
                normalMapTexture = ReadbackTexture(normalMapRt, TextureFormat.RGBA32, true, TextureWrapMode.Clamp, "Fauna1725_Normal_CPU");
                maskTexture = ReadbackTexture(maskRt, TextureFormat.RGBA32, true, TextureWrapMode.Clamp, "Fauna1725_MaskV1_CPU");
                if (settings.WriteBiolumPulseAtlas)
                    biolumPulseTexture = ReadbackTexture(biolumPulseRt, TextureFormat.RGBA32, true, TextureWrapMode.Repeat, "Fauna1725_BiolumPulse64_CPU");

                string albedoFailure = string.Empty;
                string normalFailure = string.Empty;
                string maskFailure = string.Empty;
                if (!ValidateTexturePixels(albedoTexture, dimensions.AlbedoResolution, dimensions.AlbedoResolution, TextureValidationRole.Albedo, out PixelMetrics albedoMetrics, out albedoFailure) ||
                    !ValidateTexturePixels(normalMapTexture, dimensions.DetailResolution, dimensions.DetailResolution, TextureValidationRole.NormalMap, out PixelMetrics normalMapMetrics, out normalFailure) ||
                    !ValidateTexturePixels(maskTexture, dimensions.MaskResolution, dimensions.MaskResolution, TextureValidationRole.PackedMask, out PixelMetrics maskMetrics, out maskFailure))
                {
                    Debug.LogError("[FaunaTextureBaker1725] Validation failed: " + albedoFailure + normalFailure + maskFailure);
                    return false;
                }

                PixelMetrics biolumPulseMetrics = default;
                if (settings.WriteBiolumPulseAtlas &&
                    !ValidateTexturePixels(biolumPulseTexture, dimensions.BiolumAtlasResolution, dimensions.BiolumAtlasResolution, TextureValidationRole.BiolumPulseAtlas, out biolumPulseMetrics, out string biolumFailure))
                {
                    Debug.LogError("[FaunaTextureBaker1725] Biolum pulse validation failed: " + biolumFailure);
                    return false;
                }

                byte[] albedoBytes = albedoTexture.EncodeToPNG();
                byte[] normalMapBytes = normalMapTexture.EncodeToPNG();
                byte[] maskBytes = maskTexture.EncodeToPNG();
                byte[] biolumPulseBytes = settings.WriteBiolumPulseAtlas ? biolumPulseTexture.EncodeToPNG() : Array.Empty<byte>();

                if (!ValidateEncodedBytes(albedoBytes, "albedo") ||
                    !ValidateEncodedBytes(normalMapBytes, "normal map") ||
                    !ValidateEncodedBytes(maskBytes, "packed mask") ||
                    (settings.WriteBiolumPulseAtlas && !ValidateEncodedBytes(biolumPulseBytes, "biolum pulse")))
                {
                    return false;
                }

                if (!TryCaptureRollbackSnapshots(
                        albedoPath,
                        normalMapPath,
                        maskPath,
                        biolumPulsePath,
                        settings.WriteBiolumPulseAtlas,
                        out rollbackSnapshots,
                        out string rollbackFailure))
                {
                    Debug.LogError("[FaunaTextureBaker1725] Rollback snapshot failed: " + rollbackFailure);
                    return false;
                }

                restoreAssetSnapshots = true;
                if (!TryWriteAssetBytes(albedoPath, albedoBytes, "albedo") ||
                    !TryWriteAssetBytes(normalMapPath, normalMapBytes, "normal map") ||
                    !TryWriteAssetBytes(maskPath, maskBytes, "packed mask") ||
                    (settings.WriteBiolumPulseAtlas && !TryWriteAssetBytes(biolumPulsePath, biolumPulseBytes, "biolum pulse")))
                {
                    return false;
                }

                string albedoImportFailure = string.Empty;
                string normalImportFailure = string.Empty;
                string maskImportFailure = string.Empty;
                string biolumImportFailure = string.Empty;
                if (!ConfigureTextureImporter(albedoPath, true, true, TextureWrapMode.Clamp, FilterMode.Trilinear, dimensions.AlbedoResolution, out albedoImportFailure) ||
                    !ConfigureNormalTextureImporter(normalMapPath, dimensions.DetailResolution, out normalImportFailure) ||
                    !ConfigureTextureImporter(maskPath, false, true, TextureWrapMode.Clamp, FilterMode.Trilinear, dimensions.MaskResolution, out maskImportFailure) ||
                    (settings.WriteBiolumPulseAtlas && !ConfigureTextureImporter(biolumPulsePath, false, true, TextureWrapMode.Repeat, FilterMode.Trilinear, dimensions.BiolumAtlasResolution, out biolumImportFailure)))
                {
                    Debug.LogError("[FaunaTextureBaker1725] Importer configuration failed: " +
                                   albedoImportFailure + normalImportFailure + maskImportFailure +
                                   (settings.WriteBiolumPulseAtlas ? biolumImportFailure : string.Empty));
                    return false;
                }

                if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("fauna texture bake 1725", out string finalizeFailure))
                {
                    Debug.LogError("[FaunaTextureBaker1725] " + finalizeFailure);
                    return false;
                }

                if (!TryApplyBakedTexturesToTargetMaterial(settings.TargetSharedMaterial, albedoPath, normalMapPath, maskPath, out materialRollbackSnapshot, out string materialFailure))
                {
                    TryRestoreTargetMaterialSnapshot(materialRollbackSnapshot);
                    Debug.LogError("[FaunaTextureBaker1725] Target material bind failed: " + materialFailure);
                    return false;
                }

                restoreMaterialSnapshot = materialRollbackSnapshot.IsValid;
                if (settings.TargetSharedMaterial != null &&
                    !ProceduralTextureBaker.TryFinalizeAssetDatabase("fauna material bind 1725", out string materialFinalizeFailure))
                {
                    Debug.LogError("[FaunaTextureBaker1725] " + materialFinalizeFailure);
                    return false;
                }

                restoreAssetSnapshots = false;
                restoreMaterialSnapshot = false;
                result = new BakeResult
                {
                    AlbedoPath = albedoPath,
                    NormalMapPath = normalMapPath,
                    MaskPath = maskPath,
                    BiolumPulseAtlasPath = settings.WriteBiolumPulseAtlas ? biolumPulsePath : string.Empty,
                    TargetMaterialPath = settings.TargetSharedMaterial != null ? AssetDatabase.GetAssetPath(settings.TargetSharedMaterial) : string.Empty,
                    Dimensions = dimensions,
                    Mesh = meshMetrics,
                    Uv = uvMetrics,
                    SkeletonPathPointCount = skeletonPointCount,
                    SkeletonSource = skeletonSource,
                    AlbedoMetrics = albedoMetrics,
                    NormalMapMetrics = normalMapMetrics,
                    MaskMetrics = maskMetrics,
                    BiolumPulseMetrics = biolumPulseMetrics
                };

                Debug.Log("[FaunaTextureBaker1725] Bake complete. Albedo px=" +
                          dimensions.AlbedoPixelCount.ToString(CultureInfo.InvariantCulture) +
                          " detail px=" +
                          dimensions.DetailPixelCount.ToString(CultureInfo.InvariantCulture) +
                          " mask px=" +
                          dimensions.MaskPixelCount.ToString(CultureInfo.InvariantCulture) +
                          " uv max stretch=" +
                          uvMetrics.MaxStretchRatio.ToString("F3", CultureInfo.InvariantCulture));
                return true;
            }
            finally
            {
                if (restoreMaterialSnapshot)
                    TryRestoreTargetMaterialSnapshot(materialRollbackSnapshot);

                if (restoreAssetSnapshots)
                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollbackSnapshots);

                if (skeletonBuffer != null)
                    skeletonBuffer.Release();

                DestroyImmediateSafe(albedoTexture);
                DestroyImmediateSafe(normalMapTexture);
                DestroyImmediateSafe(maskTexture);
                DestroyImmediateSafe(biolumPulseTexture);
                ReleaseRenderTexture(albedoRt);
                ReleaseRenderTexture(normalMapRt);
                ReleaseRenderTexture(maskRt);
                ReleaseRenderTexture(biolumPulseRt);
            }
        }

        public static bool TryDryRun(ComputeShader computeShader, out string failure)
        {
            failure = string.Empty;
            if (computeShader == null)
            {
                failure = "compute shader is null";
                return false;
            }

            if (!ValidateUnmanagedDtoLayout(out failure))
                return false;

            RenderTexture rt = null;
            GraphicsBuffer buffer = null;
            try
            {
                BakeDimensions dimensions = new BakeDimensions(64, 64, 64, 64, 8, BiolumFrameCount, BiolumTilesPerAxis);
                MeshMetrics meshMetrics = MeshMetrics.DefaultUnit;
                if (!TryCreateDefaultSkeletonPathBuffer(meshMetrics, out buffer, out int skeletonPointCount, out string bufferFailure))
                {
                    failure = bufferFailure;
                    return false;
                }

                BakeSettings settings = BakeSettings.Default();
                rt = CreateRenderTexture(64, 64, RenderTextureFormat.ARGBFloat, TextureWrapMode.Clamp, "Fauna1725_DryRun_RT");
                ConfigureComputeConstants(computeShader, settings, dimensions, meshMetrics, buffer, skeletonPointCount);
                DispatchKernel(computeShader, "CSBakeFaunaAlbedo", "_OutputAlbedo", rt);
                DispatchKernel(computeShader, "CSBakeFaunaNormalMap", "_OutputNormalMap", rt);
                DispatchKernel(computeShader, "CSBakeFaunaMaskV1", "_OutputMask", rt);
                DispatchKernel(computeShader, "CSBakeFaunaBiolumPulse64", "_OutputBiolumPulse", rt);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (buffer != null)
                    buffer.Release();

                ReleaseRenderTexture(rt);
            }
        }

        private static BakeSettings SanitizeSettings(BakeSettings requested)
        {
            BakeSettings settings = requested;
            settings.AssetName = string.IsNullOrWhiteSpace(settings.AssetName) ? DefaultAssetName : settings.AssetName;
            settings.OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder) ? DefaultOutputFolder : settings.OutputFolder.Replace('\\', '/');
            settings.GlobalQualityWeight = Mathf.Clamp01(FiniteOrDefault(settings.GlobalQualityWeight, 1f));
            settings.Seed = FiniteOrDefault(settings.Seed, 1725f);
            settings.DisplacementMeters = Mathf.Clamp(FiniteOrDefault(settings.DisplacementMeters, 0.14f), 0.02f, 0.45f);
            settings.WrinkleGain = Mathf.Clamp(FiniteOrDefault(settings.WrinkleGain, 1f), 0f, 2f);
            settings.PoreGain = Mathf.Clamp(FiniteOrDefault(settings.PoreGain, 1f), 0f, 2f);
            settings.ChitinGain = Mathf.Clamp(FiniteOrDefault(settings.ChitinGain, 1f), 0f, 2f);
            settings.BiolumGain = Mathf.Clamp(FiniteOrDefault(settings.BiolumGain, 1f), 0f, 2f);
            return settings;
        }

        private static BakeDimensions ResolveDimensions(float qualityWeight)
        {
            float quality = Mathf.Clamp01(FiniteOrDefault(qualityWeight, 1f));
            int albedoResolution = Align(Mathf.RoundToInt(Mathf.Lerp(MinimumAlbedoResolution, MaximumAlbedoResolution, quality)), 64);
            int detailResolution = Align(Mathf.RoundToInt(Mathf.Lerp(MinimumDetailResolution, MaximumDetailResolution, quality)), 64);
            int maskResolution = Align(Mathf.RoundToInt(Mathf.Lerp(MinimumDetailResolution, MaximumDetailResolution, quality)), 64);
            int tileResolution = Align(Mathf.RoundToInt(Mathf.Lerp(MinimumBiolumTileResolution, MaximumBiolumTileResolution, quality)), 16);
            albedoResolution = Mathf.Clamp(albedoResolution, MinimumAlbedoResolution, MaximumAlbedoResolution);
            detailResolution = Mathf.Clamp(detailResolution, MinimumDetailResolution, MaximumDetailResolution);
            maskResolution = Mathf.Clamp(maskResolution, MinimumDetailResolution, MaximumDetailResolution);
            tileResolution = Mathf.Clamp(tileResolution, MinimumBiolumTileResolution, MaximumBiolumTileResolution);
            int atlasResolution = tileResolution * BiolumTilesPerAxis;
            return new BakeDimensions(albedoResolution, detailResolution, maskResolution, atlasResolution, tileResolution, BiolumFrameCount, BiolumTilesPerAxis);
        }

        private static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format, TextureWrapMode wrapMode, string name)
        {
            RenderTexture rt = new RenderTexture(width, height, 0, format)
            {
                name = name,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = wrapMode,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        private static void ConfigureComputeConstants(
            ComputeShader computeShader,
            BakeSettings settings,
            BakeDimensions dimensions,
            MeshMetrics meshMetrics,
            GraphicsBuffer skeletonBuffer,
            int skeletonPointCount)
        {
            int creaseCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(12f, 56f, settings.GlobalQualityWeight)), 8, 64);
            computeShader.SetVector("_BakeParams1725", new Vector4(settings.GlobalQualityWeight, settings.Seed, creaseCount, settings.PoreGain));
            computeShader.SetVector("_SurfaceParams1725", new Vector4(settings.WrinkleGain, settings.ChitinGain, settings.BiolumGain, settings.DisplacementMeters));
            computeShader.SetVector("_AtlasParams1725", new Vector4(dimensions.BiolumFrames, dimensions.BiolumTilesPerAxis, dimensions.BiolumTileResolution, skeletonPointCount));
            computeShader.SetVector("_MeshBoundsCenter1725", meshMetrics.BoundsCenter);
            computeShader.SetVector("_MeshBoundsSize1725", SanitizeBoundsSize(meshMetrics.BoundsSize));

            computeShader.SetBuffer(computeShader.FindKernel("CSBakeFaunaAlbedo"), "_BonePathPoints1725", skeletonBuffer);
            computeShader.SetBuffer(computeShader.FindKernel("CSBakeFaunaNormalMap"), "_BonePathPoints1725", skeletonBuffer);
            computeShader.SetBuffer(computeShader.FindKernel("CSBakeFaunaMaskV1"), "_BonePathPoints1725", skeletonBuffer);
            computeShader.SetBuffer(computeShader.FindKernel("CSBakeFaunaBiolumPulse64"), "_BonePathPoints1725", skeletonBuffer);
        }

        private static void DispatchKernel(ComputeShader computeShader, string kernelName, string outputName, RenderTexture output)
        {
            int kernel = computeShader.FindKernel(kernelName);
            computeShader.SetTexture(kernel, outputName, output);
            computeShader.GetKernelThreadGroupSizes(kernel, out uint groupSizeX, out uint groupSizeY, out uint _);
            if (groupSizeX == 0u || groupSizeY == 0u || groupSizeX > int.MaxValue || groupSizeY > int.MaxValue)
                throw new InvalidOperationException("Invalid thread group size for " + kernelName);

            int groupsX = CeilDivide(output.width, (int)groupSizeX);
            int groupsY = CeilDivide(output.height, (int)groupSizeY);
            if (groupsX <= 0 || groupsY <= 0)
                throw new InvalidOperationException("Invalid dispatch group count for " + kernelName);

            computeShader.Dispatch(kernel, groupsX, groupsY, 1);
        }

        private static Texture2D ReadbackTexture(RenderTexture source, TextureFormat format, bool linear, TextureWrapMode wrapMode, string name)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = new Texture2D(source.width, source.height, format, false, linear)
            {
                name = name,
                wrapMode = wrapMode,
                filterMode = FilterMode.Bilinear
            };
            RenderTexture.active = source;
            try
            {
                texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                texture.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            return texture;
        }

        private static bool ValidateTexturePixels(
            Texture2D texture,
            int expectedWidth,
            int expectedHeight,
            TextureValidationRole role,
            out PixelMetrics metrics,
            out string failure)
        {
            metrics = default;
            failure = string.Empty;
            if (texture == null)
            {
                failure = role + " texture null. ";
                return false;
            }

            int expectedPixels = expectedWidth * expectedHeight;
            if (texture.width != expectedWidth || texture.height != expectedHeight)
            {
                failure = role + " dimensions mismatch. ";
                return false;
            }

            if (texture.format != TextureFormat.RGBA32)
            {
                failure = role + " texture format mismatch. ";
                return false;
            }

            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            if (!pixels.IsCreated || pixels.Length != expectedPixels)
            {
                failure = role + " pixel count mismatch. ";
                return false;
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            double rSum = 0.0;
            double gSum = 0.0;
            double bSum = 0.0;
            double aSum = 0.0;
            int invalidCount = 0;
            int divergentRgbCount = 0;
            int chitinLayoutWarnings = 0;
            int loopSeamMaxDelta = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                float r = pixel.r * (1f / 255f);
                float g = pixel.g * (1f / 255f);
                float b = pixel.b * (1f / 255f);
                float a = pixel.a * (1f / 255f);

                if (role == TextureValidationRole.PackedMask && pixel.r > 166 && pixel.b < 51)
                    chitinLayoutWarnings++;

                if (pixel.r != pixel.g || pixel.g != pixel.b)
                    divergentRgbCount++;

                min = Mathf.Min(Mathf.Min(min, r), Mathf.Min(g, Mathf.Min(b, a)));
                max = Mathf.Max(Mathf.Max(max, r), Mathf.Max(g, Mathf.Max(b, a)));
                rSum += r;
                gSum += g;
                bSum += b;
                aSum += a;
            }

            if (role == TextureValidationRole.BiolumPulseAtlas &&
                !TryValidateBiolumPulseLoopSeam(pixels, expectedWidth, expectedHeight, out loopSeamMaxDelta, out string loopSeamFailure))
            {
                failure = loopSeamFailure;
                return false;
            }

            metrics = new PixelMetrics
            {
                Width = expectedWidth,
                Height = expectedHeight,
                PixelCount = expectedPixels,
                InvalidPixelCount = invalidCount,
                MinChannel = min,
                MaxChannel = max,
                AverageR = expectedPixels > 0 ? (float)(rSum / expectedPixels) : 0f,
                AverageG = expectedPixels > 0 ? (float)(gSum / expectedPixels) : 0f,
                AverageB = expectedPixels > 0 ? (float)(bSum / expectedPixels) : 0f,
                AverageAlpha = expectedPixels > 0 ? (float)(aSum / expectedPixels) : 0f,
                DivergentRgbPixelCount = divergentRgbCount,
                LayoutWarningPixelCount = chitinLayoutWarnings,
                LoopSeamMaxChannelDelta = loopSeamMaxDelta,
                AlignmentPadding = 0,
                EstimatedBc7Bytes = expectedPixels * Bc7BytesPerPixel
            };

            if (invalidCount > 0)
            {
                failure = role + " invalid pixels=" + invalidCount.ToString(CultureInfo.InvariantCulture) + ". ";
                return false;
            }

            if (role == TextureValidationRole.PackedMask)
            {
                if (metrics.AverageAlpha <= 0.0001f)
                {
                    failure = "packed mask has empty emission alpha. ";
                    return false;
                }

                if (divergentRgbCount <= expectedPixels / 32)
                {
                    failure = "packed mask RGB channels are not independent enough. ";
                    return false;
                }
            }

            if (role == TextureValidationRole.BiolumPulseAtlas && metrics.AverageAlpha <= 0.0001f)
            {
                failure = "biolum pulse atlas has empty alpha. ";
                return false;
            }

            return true;
        }

        private static bool TryValidateBiolumPulseLoopSeam(
            NativeArray<Color32> pixels,
            int width,
            int height,
            out int maxChannelDelta,
            out string failure)
        {
            maxChannelDelta = 0;
            failure = string.Empty;
            if (width <= 0 || height <= 0 || width % BiolumTilesPerAxis != 0 || height % BiolumTilesPerAxis != 0)
            {
                failure = "biolum pulse atlas dimensions are not divisible by tile grid. ";
                return false;
            }

            int tileWidth = width / BiolumTilesPerAxis;
            int tileHeight = height / BiolumTilesPerAxis;
            int lastFrame = Mathf.Min(BiolumFrameCount, BiolumTilesPerAxis * BiolumTilesPerAxis) - 1;
            if (lastFrame <= 0 || tileWidth <= 0 || tileHeight <= 0)
            {
                failure = "biolum pulse atlas frame layout invalid. ";
                return false;
            }

            int lastTileX = lastFrame % BiolumTilesPerAxis;
            int lastTileY = lastFrame / BiolumTilesPerAxis;
            for (int y = 0; y < tileHeight; y++)
            {
                int firstRow = y * width;
                int lastRow = (lastTileY * tileHeight + y) * width;
                for (int x = 0; x < tileWidth; x++)
                {
                    Color32 first = pixels[firstRow + x];
                    Color32 last = pixels[lastRow + lastTileX * tileWidth + x];
                    maxChannelDelta = Mathf.Max(maxChannelDelta, AbsByteDelta(first.r, last.r));
                    maxChannelDelta = Mathf.Max(maxChannelDelta, AbsByteDelta(first.g, last.g));
                    maxChannelDelta = Mathf.Max(maxChannelDelta, AbsByteDelta(first.b, last.b));
                    maxChannelDelta = Mathf.Max(maxChannelDelta, AbsByteDelta(first.a, last.a));
                }
            }

            if (maxChannelDelta > BiolumPulseMaxLoopSeamByteDelta)
            {
                failure = "biolum pulse loop seam exceeds byte delta gate. ";
                return false;
            }

            return true;
        }

        private static int AbsByteDelta(byte a, byte b)
        {
            return a >= b ? a - b : b - a;
        }

        private static bool ValidateEncodedBytes(byte[] bytes, string label)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError("[FaunaTextureBaker1725] Encoded " + label + " texture is empty.");
                return false;
            }

            if (bytes.LongLength > MaxEncodedTextureBytes)
            {
                Debug.LogError("[FaunaTextureBaker1725] Encoded " + label + " texture exceeds safety limit: " + bytes.LongLength.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            return true;
        }

        private static bool TryWriteAssetBytes(string assetPath, byte[] bytes, string label)
        {
            if (ProceduralTextureBaker.TryWriteBytesAtomic(assetPath, bytes, out string failure))
                return true;

            Debug.LogError("[FaunaTextureBaker1725] Failed to write " + label + ": " + failure);
            return false;
        }

        private static bool TryCaptureRollbackSnapshots(
            string albedoPath,
            string normalMapPath,
            string maskPath,
            string biolumPulsePath,
            bool includeBiolumPulse,
            out ProceduralTextureBaker.AssetFileRollbackSnapshot[] snapshots,
            out string failure)
        {
            return includeBiolumPulse
                ? ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(albedoPath, normalMapPath, maskPath, biolumPulsePath, out snapshots, out failure)
                : ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(albedoPath, normalMapPath, maskPath, out snapshots, out failure);
        }

        private static bool ConfigureTextureImporter(string assetPath, bool srgb, bool mipmaps, TextureWrapMode wrapMode, FilterMode filterMode, int maxSize, out string failure)
        {
            if (!mipmaps)
            {
                failure = "fauna baked textures require mipmaps";
                return false;
            }

            return ProceduralTextureBaker.TryEnforceTextureImportSettings(
                assetPath,
                srgb,
                false,
                wrapMode,
                filterMode,
                maxSize,
                TextureImporterFormat.BC7,
                out failure);
        }

        private static bool ConfigureNormalTextureImporter(string assetPath, int maxSize, out string failure)
        {
            return ProceduralTextureBaker.TryEnforceTextureImportSettings(
                assetPath,
                ProceduralTextureBaker.TextureRole.Normal,
                maxSize,
                out failure);
        }

        private static bool TryApplyBakedTexturesToTargetMaterial(
            Material targetMaterial,
            string albedoPath,
            string normalMapPath,
            string maskPath,
            out MaterialTextureRollbackSnapshot rollbackSnapshot,
            out string failure)
        {
            rollbackSnapshot = default;
            failure = string.Empty;
            if (targetMaterial == null)
                return true;

            if (!AssetDatabase.Contains(targetMaterial))
            {
                failure = "target material must be a project asset, not a scene/runtime material";
                return false;
            }

            if (!TryResolveTargetMaterialProjectPath(targetMaterial, out _, out failure))
                return false;

            if (!targetMaterial.HasProperty(s_baseMapId) ||
                !targetMaterial.HasProperty(s_normalMapId) ||
                !targetMaterial.HasProperty(s_maskMapId))
            {
                failure = "target material misses _BaseMap, _NormalMap, or _MaskMap";
                return false;
            }

            rollbackSnapshot = new MaterialTextureRollbackSnapshot
            {
                Material = targetMaterial,
                BaseMap = targetMaterial.GetTexture(s_baseMapId),
                NormalMap = targetMaterial.GetTexture(s_normalMapId),
                MaskMap = targetMaterial.GetTexture(s_maskMapId),
                IsValid = true
            };

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (albedo == null || normalMap == null || mask == null)
            {
                failure = "one or more baked textures were not importable before material bind";
                return false;
            }

            try
            {
                targetMaterial.SetTexture(s_baseMapId, albedo);
                targetMaterial.SetTexture(s_normalMapId, normalMap);
                targetMaterial.SetTexture(s_maskMapId, mask);
                EditorUtility.SetDirty(targetMaterial);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryResolveTargetMaterialProjectPath(Material targetMaterial, out string materialPath, out string failure)
        {
            materialPath = string.Empty;
            failure = string.Empty;
            if (targetMaterial == null)
                return true;

            materialPath = AssetDatabase.GetAssetPath(targetMaterial);
            if (string.IsNullOrEmpty(materialPath))
            {
                failure = "target material asset path is empty";
                return false;
            }

            if (!materialPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                failure = "target material must live under Assets/, path=" + materialPath;
                return false;
            }

            if (!materialPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                failure = "target material must be a .mat asset, path=" + materialPath;
                return false;
            }

            return true;
        }

        private static void TryRestoreTargetMaterialSnapshot(MaterialTextureRollbackSnapshot snapshot)
        {
            if (!snapshot.IsValid || snapshot.Material == null)
                return;

            try
            {
                snapshot.Material.SetTexture(s_baseMapId, snapshot.BaseMap);
                snapshot.Material.SetTexture(s_normalMapId, snapshot.NormalMap);
                snapshot.Material.SetTexture(s_maskMapId, snapshot.MaskMap);
                EditorUtility.SetDirty(snapshot.Material);
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException)
            {
                Debug.LogWarning("[FaunaTextureBaker1725] Failed to restore target material texture snapshot: " + ex.Message);
            }
        }

        private static Mesh ResolveSourceMesh(SkinnedMeshRenderer sourceRenderer, Mesh sourceMesh)
        {
            return sourceMesh != null ? sourceMesh : sourceRenderer != null ? sourceRenderer.sharedMesh : null;
        }

        private static MeshMetrics ResolveMeshMetrics(Mesh mesh)
        {
            if (mesh == null)
                return MeshMetrics.DefaultUnit;

            Bounds bounds = mesh.bounds;
            long triangleCount = 0L;
            int subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
                triangleCount += (long)(mesh.GetIndexCount(i) / 3u);

            return new MeshMetrics
            {
                MeshName = mesh.name,
                VertexCount = mesh.vertexCount,
                TriangleCount = triangleCount > int.MaxValue ? int.MaxValue : (int)triangleCount,
                BoundsSize = SanitizeBoundsSize(bounds.size),
                BoundsCenter = bounds.center
            };
        }

        private static bool TryResolveUvMetrics(Mesh mesh, int detailResolution, out UvMetrics metrics, out string failure)
        {
            metrics = default;
            failure = string.Empty;
            if (mesh == null)
            {
                metrics = UvMetrics.DefaultUnit;
                return true;
            }

            int vertexCount = mesh.vertexCount;
            if (vertexCount <= 0)
            {
                failure = "source mesh has no vertices";
                return false;
            }

            if (vertexCount > UvMetricVertexCapacity)
            {
                failure = "source mesh vertex count exceeds UV metric scratch capacity";
                return false;
            }

            long estimatedIndexCount = 0L;
            long estimatedTriangleCount = 0L;
            int subMeshCount = mesh.subMeshCount;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                uint subMeshIndexCount = mesh.GetIndexCount(subMeshIndex);
                estimatedIndexCount += subMeshIndexCount;
                estimatedTriangleCount += subMeshIndexCount / 3u;
                if (estimatedIndexCount > UvMetricIndexCapacity)
                {
                    failure = "source mesh index count exceeds UV metric scratch capacity";
                    return false;
                }
            }

            s_uvMetricVertices.Clear();
            s_uvMetricUvs.Clear();
            s_uvMetricIndices.Clear();
            mesh.GetVertices(s_uvMetricVertices);
            mesh.GetUVs(0, s_uvMetricUvs);

            if (s_uvMetricVertices.Count != vertexCount || s_uvMetricUvs.Count < vertexCount)
            {
                failure = "source mesh UV0 is missing or incomplete";
                return false;
            }

            float uvMinX = float.MaxValue;
            float uvMinY = float.MaxValue;
            float uvMaxX = float.MinValue;
            float uvMaxY = float.MinValue;
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                Vector3 vertex = s_uvMetricVertices[vertexIndex];
                if (!IsFinite(vertex.x) || !IsFinite(vertex.y) || !IsFinite(vertex.z))
                {
                    failure = "source mesh contains non-finite vertex positions";
                    return false;
                }

                Vector2 uv = s_uvMetricUvs[vertexIndex];
                if (!IsFinite(uv.x) || !IsFinite(uv.y))
                {
                    failure = "source mesh UV0 contains non-finite coordinates";
                    return false;
                }

                uvMinX = Mathf.Min(uvMinX, uv.x);
                uvMinY = Mathf.Min(uvMinY, uv.y);
                uvMaxX = Mathf.Max(uvMaxX, uv.x);
                uvMaxY = Mathf.Max(uvMaxY, uv.y);
            }

            int measuredTriangles = 0;
            int skippedTriangles = 0;
            float minStretch = float.MaxValue;
            float maxStretch = 0f;
            double stretchSum = 0.0;
            double texelDensitySum = 0.0;
            float detailPixels = Mathf.Max(1f, detailResolution * (float)detailResolution);

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                uint subMeshIndexCount = mesh.GetIndexCount(subMeshIndex);
                if (mesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
                {
                    skippedTriangles += (int)(subMeshIndexCount / 3u);
                    continue;
                }

                s_uvMetricIndices.Clear();
                mesh.GetTriangles(s_uvMetricIndices, subMeshIndex, true);
                int indexLimit = s_uvMetricIndices.Count - s_uvMetricIndices.Count % 3;
                for (int index = 0; index < indexLimit; index += 3)
                {
                    int i0 = s_uvMetricIndices[index];
                    int i1 = s_uvMetricIndices[index + 1];
                    int i2 = s_uvMetricIndices[index + 2];
                    if ((uint)i0 >= vertexCount || (uint)i1 >= vertexCount || (uint)i2 >= vertexCount)
                    {
                        skippedTriangles++;
                        continue;
                    }

                    Vector3 p0 = s_uvMetricVertices[i0];
                    Vector3 p1 = s_uvMetricVertices[i1];
                    Vector3 p2 = s_uvMetricVertices[i2];
                    Vector2 uv0 = s_uvMetricUvs[i0];
                    Vector2 uv1 = s_uvMetricUvs[i1];
                    Vector2 uv2 = s_uvMetricUvs[i2];

                    float surfaceArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                    float uvArea = Mathf.Abs(CrossUv(uv1 - uv0, uv2 - uv0)) * 0.5f;
                    if (surfaceArea <= UvAreaEpsilon || uvArea <= UvAreaEpsilon)
                    {
                        skippedTriangles++;
                        continue;
                    }

                    if (!TryMeasureTriangleStretch(p0, p1, p2, uv0, uv1, uv2, out float stretchRatio))
                    {
                        skippedTriangles++;
                        continue;
                    }

                    float texelDensity = Mathf.Sqrt(uvArea * detailPixels) / Mathf.Sqrt(surfaceArea);
                    if (!IsFinite(stretchRatio) || !IsFinite(texelDensity))
                    {
                        skippedTriangles++;
                        continue;
                    }

                    minStretch = Mathf.Min(minStretch, stretchRatio);
                    maxStretch = Mathf.Max(maxStretch, stretchRatio);
                    stretchSum += stretchRatio;
                    texelDensitySum += texelDensity;
                    measuredTriangles++;
                }
            }

            if (measuredTriangles <= 0)
            {
                failure = "source mesh has no measurable UV0 triangles";
                return false;
            }

            metrics = new UvMetrics
            {
                VertexCount = vertexCount,
                TriangleCount = estimatedTriangleCount > int.MaxValue ? int.MaxValue : (int)estimatedTriangleCount,
                MeasuredTriangleCount = measuredTriangles,
                SkippedTriangleCount = skippedTriangles,
                MinStretchRatio = minStretch == float.MaxValue ? 0f : minStretch,
                MaxStretchRatio = maxStretch,
                AverageStretchRatio = (float)(stretchSum / measuredTriangles),
                AverageTexelDensity = (float)(texelDensitySum / measuredTriangles),
                UvMinX = uvMinX,
                UvMinY = uvMinY,
                UvMaxX = uvMaxX,
                UvMaxY = uvMaxY,
                EstimatedIndexCount = estimatedIndexCount
            };

            if (metrics.MaxStretchRatio > UvFatalStretchRatio)
            {
                failure = "source mesh UV0 stretch exceeds 1.50 fatal gate";
                return false;
            }

            return true;
        }

        private static bool TryMeasureTriangleStretch(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2,
            out float stretchRatio)
        {
            stretchRatio = 0f;
            float minEdgeScale = float.MaxValue;
            float maxEdgeScale = 0f;
            int validEdges = 0;
            AccumulateEdgeStretch(p0, p1, uv0, uv1, ref minEdgeScale, ref maxEdgeScale, ref validEdges);
            AccumulateEdgeStretch(p1, p2, uv1, uv2, ref minEdgeScale, ref maxEdgeScale, ref validEdges);
            AccumulateEdgeStretch(p2, p0, uv2, uv0, ref minEdgeScale, ref maxEdgeScale, ref validEdges);
            if (validEdges < 2 || minEdgeScale <= UvAreaEpsilon || maxEdgeScale <= UvAreaEpsilon)
                return false;

            stretchRatio = maxEdgeScale / minEdgeScale;
            return IsFinite(stretchRatio);
        }

        private static void AccumulateEdgeStretch(
            Vector3 p0,
            Vector3 p1,
            Vector2 uv0,
            Vector2 uv1,
            ref float minEdgeScale,
            ref float maxEdgeScale,
            ref int validEdges)
        {
            float worldLength = (p1 - p0).magnitude;
            float uvLength = (uv1 - uv0).magnitude;
            if (worldLength <= UvAreaEpsilon || uvLength <= UvAreaEpsilon)
                return;

            float edgeScale = uvLength / worldLength;
            if (!IsFinite(edgeScale) || edgeScale <= UvAreaEpsilon)
                return;

            minEdgeScale = Mathf.Min(minEdgeScale, edgeScale);
            maxEdgeScale = Mathf.Max(maxEdgeScale, edgeScale);
            validEdges++;
        }

        private static float CrossUv(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool TryCreateSkeletonPathBuffer(
            SkinnedMeshRenderer sourceRenderer,
            MeshMetrics meshMetrics,
            out GraphicsBuffer buffer,
            out int pointCount,
            out string skeletonSource,
            out string failure)
        {
            Transform[] bones = sourceRenderer != null ? sourceRenderer.bones : null;
            if (bones != null && bones.Length >= 2)
            {
                if (TryCreateBonePathBufferFromRenderer(sourceRenderer, bones, meshMetrics, out buffer, out pointCount, out failure))
                {
                    skeletonSource = "SkinnedMeshRenderer.bones";
                    return true;
                }
            }

            if (TryCreateDefaultSkeletonPathBuffer(meshMetrics, out buffer, out pointCount, out failure))
            {
                skeletonSource = "mesh-bounds-default";
                return true;
            }

            skeletonSource = string.Empty;
            return false;
        }

        private static unsafe bool TryCreateBonePathBufferFromRenderer(
            SkinnedMeshRenderer sourceRenderer,
            Transform[] bones,
            MeshMetrics meshMetrics,
            out GraphicsBuffer buffer,
            out int pointCount,
            out string failure)
        {
            buffer = null;
            pointCount = 0;
            failure = string.Empty;
            if (bones == null || bones.Length < 2)
            {
                failure = "renderer bones unavailable";
                return false;
            }

            Vector4* points = stackalloc Vector4[MaxSkeletonPathPoints];
            int stride = Mathf.Max(1, Mathf.FloorToInt((bones.Length - 1f) / (MaxSkeletonPathPoints - 1f)));
            Transform rendererTransform = sourceRenderer.transform;
            for (int i = 0, boneIndex = 0; i < MaxSkeletonPathPoints && boneIndex < bones.Length; i++, boneIndex += stride)
            {
                Transform bone = bones[Mathf.Min(boneIndex, bones.Length - 1)];
                Vector3 local = bone != null && rendererTransform != null
                    ? rendererTransform.InverseTransformPoint(bone.position)
                    : ResolveDefaultPathPoint(meshMetrics, i, MaxSkeletonPathPoints);
                points[i] = new Vector4(local.x, local.y, local.z, 1f);
                pointCount++;
            }

            if (pointCount < 2)
            {
                failure = "not enough bone points";
                return false;
            }

            Vector4 lastPoint = points[pointCount - 1];
            for (int i = pointCount; i < MaxSkeletonPathPoints; i++)
                points[i] = lastPoint;

            if (TryCreateSkeletonBufferFromPoints(points, out buffer, out failure))
                return true;

            pointCount = 0;
            return false;
        }

        private static unsafe bool TryCreateDefaultSkeletonPathBuffer(MeshMetrics meshMetrics, out GraphicsBuffer buffer, out int pointCount, out string failure)
        {
            buffer = null;
            pointCount = MaxSkeletonPathPoints;
            failure = string.Empty;
            Vector4* points = stackalloc Vector4[MaxSkeletonPathPoints];
            for (int i = 0; i < MaxSkeletonPathPoints; i++)
            {
                Vector3 local = ResolveDefaultPathPoint(meshMetrics, i, MaxSkeletonPathPoints);
                points[i] = new Vector4(local.x, local.y, local.z, 1f);
            }

            if (TryCreateSkeletonBufferFromPoints(points, out buffer, out failure))
                return true;

            pointCount = 0;
            return false;
        }

        private static unsafe bool TryCreateSkeletonBufferFromPoints(Vector4* points, out GraphicsBuffer buffer, out string failure)
        {
            buffer = null;
            failure = string.Empty;
            try
            {
                int strideBytes = UnsafeUtility.SizeOf<Vector4>();
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxSkeletonPathPoints, strideBytes);
                NativeArray<Vector4> mapped = buffer.LockBufferForWrite<Vector4>(0, MaxSkeletonPathPoints);
                try
                {
                    UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(mapped), points, strideBytes * MaxSkeletonPathPoints);
                }
                finally
                {
                    buffer.UnlockBufferAfterWrite<Vector4>(MaxSkeletonPathPoints);
                }

                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is UnityException)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                if (buffer != null)
                {
                    buffer.Release();
                    buffer = null;
                }

                return false;
            }
        }

        private static Vector3 ResolveDefaultPathPoint(MeshMetrics meshMetrics, int index, int count)
        {
            Vector3 size = SanitizeBoundsSize(meshMetrics.BoundsSize);
            Vector3 center = meshMetrics.BoundsCenter;
            float t = count > 1 ? index / (float)(count - 1) : 0.5f;
            float z = Mathf.Lerp(-0.48f, 0.48f, t) * size.z;
            float lateralWave = Mathf.Sin(t * Mathf.PI * 2f) * size.x * 0.08f;
            float y = Mathf.Lerp(0.14f, -0.10f, t) * size.y;
            return center + new Vector3(lateralWave, y, z);
        }

        private static void ReleaseRenderTexture(RenderTexture rt)
        {
            if (rt == null)
                return;

            rt.Release();
            DestroyImmediateSafe(rt);
        }

        private static void DestroyImmediateSafe(UnityEngine.Object obj)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }

        private static Vector3 SanitizeBoundsSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(0.01f, FiniteOrDefault(size.x, 1f)),
                Mathf.Max(0.01f, FiniteOrDefault(size.y, 1f)),
                Mathf.Max(0.01f, FiniteOrDefault(size.z, 1f)));
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int Align(int value, int alignment)
        {
            return Mathf.CeilToInt(value / (float)alignment) * alignment;
        }

        private static int CeilDivide(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            return (value + divisor - 1) / divisor;
        }

        private static bool ValidateUnmanagedDtoLayout(out string failure)
        {
            failure = string.Empty;
            int dimensionsBytes = UnsafeUtility.SizeOf<BakeDimensions>();
            int pixelMetricsBytes = UnsafeUtility.SizeOf<PixelMetrics>();
            int uvMetricsBytes = UnsafeUtility.SizeOf<UvMetrics>();
            if ((dimensionsBytes & 7) == 0 && (pixelMetricsBytes & 7) == 0 && (uvMetricsBytes & 7) == 0)
                return true;

            failure = "unmanaged DTO alignment violation: BakeDimensions=" +
                      dimensionsBytes.ToString(CultureInfo.InvariantCulture) +
                      " PixelMetrics=" +
                      pixelMetricsBytes.ToString(CultureInfo.InvariantCulture) +
                      " UvMetrics=" +
                      uvMetricsBytes.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        private static string FormatResolution(int resolution)
        {
            return resolution.ToString(CultureInfo.InvariantCulture) + " x " + resolution.ToString(CultureInfo.InvariantCulture);
        }

        private enum TextureValidationRole
        {
            Albedo,
            NormalMap,
            PackedMask,
            BiolumPulseAtlas
        }

        public struct BakeSettings
        {
            public SkinnedMeshRenderer SourceRenderer;
            public Mesh SourceMesh;
            public Material TargetSharedMaterial;
            public ComputeShader ComputeShader;
            public string AssetName;
            public string OutputFolder;
            public float GlobalQualityWeight;
            public float Seed;
            public float DisplacementMeters;
            public float WrinkleGain;
            public float PoreGain;
            public float ChitinGain;
            public float BiolumGain;
            public bool WriteBiolumPulseAtlas;

            public static BakeSettings Default()
            {
                return new BakeSettings
                {
                    SourceRenderer = null,
                    SourceMesh = null,
                    TargetSharedMaterial = null,
                    ComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath),
                    AssetName = DefaultAssetName,
                    OutputFolder = DefaultOutputFolder,
                    GlobalQualityWeight = 1f,
                    Seed = 1725f,
                    DisplacementMeters = 0.14f,
                    WrinkleGain = 1f,
                    PoreGain = 1f,
                    ChitinGain = 1f,
                    BiolumGain = 1f,
                    WriteBiolumPulseAtlas = true
                };
            }
        }

        public struct BakeResult
        {
            public string AlbedoPath;
            public string NormalMapPath;
            public string MaskPath;
            public string BiolumPulseAtlasPath;
            public string TargetMaterialPath;
            public BakeDimensions Dimensions;
            public MeshMetrics Mesh;
            public UvMetrics Uv;
            public int SkeletonPathPointCount;
            public string SkeletonSource;
            public PixelMetrics AlbedoMetrics;
            public PixelMetrics NormalMapMetrics;
            public PixelMetrics MaskMetrics;
            public PixelMetrics BiolumPulseMetrics;
        }

        private struct MaterialTextureRollbackSnapshot
        {
            public Material Material;
            public Texture BaseMap;
            public Texture NormalMap;
            public Texture MaskMap;
            public bool IsValid;
        }

        public readonly struct BakeDimensions
        {
            public readonly int AlbedoResolution;
            public readonly int DetailResolution;
            public readonly int MaskResolution;
            public readonly int BiolumAtlasResolution;
            public readonly int BiolumTileResolution;
            public readonly int BiolumFrames;
            public readonly int BiolumTilesPerAxis;
            private readonly int _padding;

            public BakeDimensions(
                int albedoResolution,
                int detailResolution,
                int maskResolution,
                int biolumAtlasResolution,
                int biolumTileResolution,
                int biolumFrames,
                int biolumTilesPerAxis)
            {
                AlbedoResolution = albedoResolution;
                DetailResolution = detailResolution;
                MaskResolution = maskResolution;
                BiolumAtlasResolution = biolumAtlasResolution;
                BiolumTileResolution = biolumTileResolution;
                BiolumFrames = biolumFrames;
                BiolumTilesPerAxis = biolumTilesPerAxis;
                _padding = 0;
            }

            public int AlbedoPixelCount => AlbedoResolution * AlbedoResolution;
            public int DetailPixelCount => DetailResolution * DetailResolution;
            public int MaskPixelCount => MaskResolution * MaskResolution;
            public int BiolumPixelCount => BiolumAtlasResolution * BiolumAtlasResolution;
            public int AlignmentPadding => _padding;
        }

        public struct MeshMetrics
        {
            public string MeshName;
            public int VertexCount;
            public int TriangleCount;
            public Vector3 BoundsSize;
            public Vector3 BoundsCenter;

            public static MeshMetrics DefaultUnit => new MeshMetrics
            {
                MeshName = "default-unit-fauna",
                VertexCount = 0,
                TriangleCount = 0,
                BoundsSize = Vector3.one,
                BoundsCenter = Vector3.zero
            };
        }

        public struct UvMetrics
        {
            public int VertexCount;
            public int TriangleCount;
            public int MeasuredTriangleCount;
            public int SkippedTriangleCount;
            public float MinStretchRatio;
            public float MaxStretchRatio;
            public float AverageStretchRatio;
            public float AverageTexelDensity;
            public float UvMinX;
            public float UvMinY;
            public float UvMaxX;
            public float UvMaxY;
            public long EstimatedIndexCount;

            public static UvMetrics DefaultUnit => new UvMetrics
            {
                VertexCount = 0,
                TriangleCount = 0,
                MeasuredTriangleCount = 0,
                SkippedTriangleCount = 0,
                MinStretchRatio = 1f,
                MaxStretchRatio = 1f,
                AverageStretchRatio = 1f,
                AverageTexelDensity = 1f,
                UvMinX = 0f,
                UvMinY = 0f,
                UvMaxX = 1f,
                UvMaxY = 1f,
                EstimatedIndexCount = 0L
            };
        }

        public struct PixelMetrics
        {
            public int Width;
            public int Height;
            public int PixelCount;
            public int InvalidPixelCount;
            public int DivergentRgbPixelCount;
            public int LayoutWarningPixelCount;
            public int LoopSeamMaxChannelDelta;
            public int AlignmentPadding;
            public float MinChannel;
            public float MaxChannel;
            public float AverageR;
            public float AverageG;
            public float AverageB;
            public float AverageAlpha;
            public long EstimatedBc7Bytes;
        }
    }
}
#endif
