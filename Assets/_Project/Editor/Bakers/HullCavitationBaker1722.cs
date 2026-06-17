#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Bakers
{
    public sealed class HullCavitationBaker1722 : EditorWindow
    {
        private const string MenuRoot = "Hecton8/Bakers/1722/";
        private const string DefaultComputeShaderPath = "Assets/_Project/Art/Shaders/Include/HullCavitationBaker1722.compute";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Textures/Vehicles/Hull1722";
        private const string DefaultAssetName = "SubmarineHull_1722";
        private const int MinimumHullResolution = 1024;
        private const int MaximumHullResolution = 4096;
        private const int MinimumCavitationTile = 64;
        private const int MaximumCavitationTile = 256;
        private const int CavitationFrameCount = 64;
        private const int CavitationTilesPerAxis = 8;
        private const int ThreadGroupSize = 8;
        private const int MaxMeshVertexCapacity = 1048576;
        private const long MaxEncodedTextureBytes = 384L * 1024L * 1024L;
        private static readonly Vector3[] FallbackMeshVertex = { Vector3.zero };
        // COLD ALLOC: List<Vector3>(1048576) - Editor-only mesh transfer scratch for offline bake.
        private static readonly List<Vector3> MeshVertexScratch = new List<Vector3>(MaxMeshVertexCapacity);

        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private ComputeShader _computeShader;
        [SerializeField] private string _assetName = DefaultAssetName;
        [SerializeField] private string _outputFolder = DefaultOutputFolder;
        [SerializeField, Range(0f, 1f)] private float _globalQualityWeight = 1f;
        [SerializeField] private float _seed = 1722f;
        [SerializeField, Range(0.05f, 0.65f)] private float _displacementMeters = 0.18f;
        [SerializeField, Range(0f, 2f)] private float _scarGain = 1f;
        [SerializeField, Range(0f, 2f)] private float _cavitationSwirlGain = 1f;
        [SerializeField, Range(0f, 2f)] private float _cavitationBubbleGain = 1f;
        [SerializeField] private string _lastStatus = "Idle.";

        [MenuItem(MenuRoot + "Open Hull Cavitation Baker", false, 1722)]
        private static void Open()
        {
            HullCavitationBaker1722 window = GetWindow<HullCavitationBaker1722>();
            window.titleContent = new GUIContent("Hull Cavitation 1722");
            window.minSize = new Vector2(480f, 520f);
        }

        [MenuItem(MenuRoot + "Bake Default Hull Package", false, 1723)]
        private static void BakeDefault()
        {
            if (TryBake(BakeSettings.Default(), out BakeResult result))
            {
                Debug.Log("[HullCavitationBaker1722] Baked hull package: " + result.DisplacementPath + " | " + result.CavitationFlipbookPath);
            }
        }

        [MenuItem(MenuRoot + "Dry Run Compute Kernels", false, 1724)]
        private static void DryRunKernels()
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath);
            if (TryDryRun(shader, out string failure))
            {
                Debug.Log("[HullCavitationBaker1722] Dry-run dispatch succeeded.");
                return;
            }

            Debug.LogError("[HullCavitationBaker1722] Dry-run dispatch failed: " + failure);
        }

        private void OnEnable()
        {
            if (_computeShader == null)
                _computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Submarine Hull Displacement And Cavitation Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _sourceMesh = (Mesh)EditorGUILayout.ObjectField("Source Hull Mesh", _sourceMesh, typeof(Mesh), false);
            _computeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", _computeShader, typeof(ComputeShader), false);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _seed = EditorGUILayout.FloatField("Seed", _seed);
            _displacementMeters = EditorGUILayout.Slider("Displacement Meters", _displacementMeters, 0.05f, 0.65f);
            _scarGain = EditorGUILayout.Slider("Scar Gain", _scarGain, 0f, 2f);
            _cavitationSwirlGain = EditorGUILayout.Slider("Cavitation Swirl", _cavitationSwirlGain, 0f, 2f);
            _cavitationBubbleGain = EditorGUILayout.Slider("Cavitation Bubble Gain", _cavitationBubbleGain, 0f, 2f);

            BakeDimensions dimensions = ResolveDimensions(_globalQualityWeight);
            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Hull Map Resolution", dimensions.HullResolution);
                EditorGUILayout.IntField("Cavitation Atlas Resolution", dimensions.CavitationAtlasResolution);
                EditorGUILayout.IntField("Cavitation Frame Count", CavitationFrameCount);
                EditorGUILayout.IntField("Cavitation Tile Resolution", dimensions.CavitationTileResolution);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Hull Package", GUILayout.Height(32f)))
                BakeFromWindow();

            if (GUILayout.Button("Dry Run Kernels", GUILayout.Height(28f)))
                DryRunFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = BakeSettings.Default();
            settings.SourceMesh = _sourceMesh;
            settings.ComputeShader = _computeShader;
            settings.AssetName = _assetName;
            settings.OutputFolder = _outputFolder;
            settings.GlobalQualityWeight = _globalQualityWeight;
            settings.Seed = _seed;
            settings.DisplacementMeters = _displacementMeters;
            settings.ScarGain = _scarGain;
            settings.CavitationSwirlGain = _cavitationSwirlGain;
            settings.CavitationBubbleGain = _cavitationBubbleGain;

            if (TryBake(settings, out BakeResult result))
            {
                _lastStatus = "Baked: " + result.DisplacementPath + " | " + result.CavitationFlipbookPath;
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
            Debug.LogError("[HullCavitationBaker1722] " + failure);
        }

        public static bool TryBake(BakeSettings requestedSettings, out BakeResult result)
        {
            result = default;
            BakeSettings settings = SanitizeSettings(requestedSettings);
            BakeDimensions dimensions = ResolveDimensions(settings.GlobalQualityWeight);

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError("[HullCavitationBaker1722] Compute shaders are unavailable on this editor device.");
                return false;
            }

            ComputeShader computeShader = settings.ComputeShader != null
                ? settings.ComputeShader
                : AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath);

            if (computeShader == null)
            {
                Debug.LogError("[HullCavitationBaker1722] Missing compute shader at " + DefaultComputeShaderPath);
                return false;
            }

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(settings.OutputFolder, out string outputFolder, out string folderFailure))
            {
                Debug.LogError("[HullCavitationBaker1722] Output folder invalid: " + folderFailure);
                return false;
            }

            string safeName = ProceduralTextureBaker.SanitizeAssetNameForPath(settings.AssetName);
            if (string.IsNullOrEmpty(safeName))
                safeName = DefaultAssetName;

            string albedoPath = outputFolder + "/TX_Hull1722_Albedo_" + safeName + ".png";
            string mraoPath = outputFolder + "/TX_Hull1722_MRAO_" + safeName + ".png";
            string displacementPath = outputFolder + "/TX_Hull1722_Displacement_" + safeName + ".exr";
            string cavitationPath = outputFolder + "/TX_Hull1722_Cavitation64_" + safeName + ".png";

            RenderTexture albedoRt = null;
            RenderTexture mraoRt = null;
            RenderTexture displacementRt = null;
            RenderTexture cavitationRt = null;
            GraphicsBuffer meshVertexBuffer = null;
            Texture2D albedoTexture = null;
            Texture2D mraoTexture = null;
            Texture2D displacementTexture = null;
            Texture2D cavitationTexture = null;

            try
            {
                albedoRt = CreateRenderTexture(dimensions.HullResolution, dimensions.HullResolution, RenderTextureFormat.ARGB32, "Hull1722_Albedo_RT");
                mraoRt = CreateRenderTexture(dimensions.HullResolution, dimensions.HullResolution, RenderTextureFormat.ARGB32, "Hull1722_MRAO_RT");
                displacementRt = CreateRenderTexture(dimensions.HullResolution, dimensions.HullResolution, RenderTextureFormat.ARGBFloat, "Hull1722_Displacement_RT");
                cavitationRt = CreateRenderTexture(dimensions.CavitationAtlasResolution, dimensions.CavitationAtlasResolution, RenderTextureFormat.ARGB32, "Hull1722_Cavitation_RT");

                ConfigureComputeConstants(computeShader, settings, dimensions);
                meshVertexBuffer = CreateMeshVertexBuffer(settings.SourceMesh, out int sourceVertexCount, out string meshBufferFailure);
                if (meshVertexBuffer == null)
                {
                    Debug.LogError("[HullCavitationBaker1722] Mesh buffer creation failed: " + meshBufferFailure);
                    return false;
                }

                DispatchKernel(computeShader, "CSBakeHullAlbedo", "_OutputAlbedo", albedoRt, meshVertexBuffer, sourceVertexCount);
                DispatchKernel(computeShader, "CSBakeHullMrao", "_OutputMrao", mraoRt, meshVertexBuffer, sourceVertexCount);
                DispatchKernel(computeShader, "CSBakeHullDisplacement", "_OutputDisplacement", displacementRt, meshVertexBuffer, sourceVertexCount);
                DispatchKernel(computeShader, "CSBakeCavitationFlipbook", "_OutputCavitation", cavitationRt, meshVertexBuffer, sourceVertexCount);

                albedoTexture = ReadbackTexture(albedoRt, TextureFormat.RGBA32, false, "Hull1722_Albedo_CPU");
                mraoTexture = ReadbackTexture(mraoRt, TextureFormat.RGBA32, true, "Hull1722_MRAO_CPU");
                displacementTexture = ReadbackTexture(displacementRt, TextureFormat.RGBAFloat, true, "Hull1722_Displacement_CPU");
                cavitationTexture = ReadbackTexture(cavitationRt, TextureFormat.RGBA32, true, "Hull1722_Cavitation_CPU");

                string albedoFailure = string.Empty;
                string mraoFailure = string.Empty;
                string displacementFailure = string.Empty;
                string cavitationFailure = string.Empty;
                if (!ValidateTexturePixels(albedoTexture, dimensions.HullResolution, dimensions.HullResolution, false, out PixelMetrics albedoMetrics, out albedoFailure) ||
                    !ValidateTexturePixels(mraoTexture, dimensions.HullResolution, dimensions.HullResolution, false, out PixelMetrics mraoMetrics, out mraoFailure) ||
                    !ValidateTexturePixels(displacementTexture, dimensions.HullResolution, dimensions.HullResolution, true, out PixelMetrics displacementMetrics, out displacementFailure) ||
                    !ValidateTexturePixels(cavitationTexture, dimensions.CavitationAtlasResolution, dimensions.CavitationAtlasResolution, false, out PixelMetrics cavitationMetrics, out cavitationFailure))
                {
                    Debug.LogError("[HullCavitationBaker1722] Validation failed: " + albedoFailure + mraoFailure + displacementFailure + cavitationFailure);
                    return false;
                }

                byte[] albedoBytes = albedoTexture.EncodeToPNG();
                byte[] mraoBytes = mraoTexture.EncodeToPNG();
                byte[] displacementBytes = displacementTexture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP);
                byte[] cavitationBytes = cavitationTexture.EncodeToPNG();

                if (!ValidateEncodedBytes(albedoBytes, "albedo") ||
                    !ValidateEncodedBytes(mraoBytes, "mrao") ||
                    !ValidateEncodedBytes(displacementBytes, "displacement") ||
                    !ValidateEncodedBytes(cavitationBytes, "cavitation"))
                {
                    return false;
                }

                WriteBytesToAssetPath(albedoPath, albedoBytes);
                WriteBytesToAssetPath(mraoPath, mraoBytes);
                WriteBytesToAssetPath(displacementPath, displacementBytes);
                WriteBytesToAssetPath(cavitationPath, cavitationBytes);

                AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(mraoPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(displacementPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(cavitationPath, ImportAssetOptions.ForceSynchronousImport);

                string albedoImportFailure = string.Empty;
                string mraoImportFailure = string.Empty;
                string displacementImportFailure = string.Empty;
                string cavitationImportFailure = string.Empty;
                if (!ConfigureTextureImporter(albedoPath, true, true, TextureWrapMode.Clamp, FilterMode.Trilinear, dimensions.HullResolution, out albedoImportFailure) ||
                    !ConfigureTextureImporter(mraoPath, false, true, TextureWrapMode.Clamp, FilterMode.Trilinear, dimensions.HullResolution, out mraoImportFailure) ||
                    !ConfigureTextureImporter(displacementPath, false, true, TextureWrapMode.Clamp, FilterMode.Trilinear, dimensions.HullResolution, out displacementImportFailure) ||
                    !ConfigureTextureImporter(cavitationPath, false, true, TextureWrapMode.Repeat, FilterMode.Trilinear, dimensions.CavitationAtlasResolution, out cavitationImportFailure))
                {
                    Debug.LogError("[HullCavitationBaker1722] Importer configuration failed: " +
                                   albedoImportFailure + mraoImportFailure + displacementImportFailure + cavitationImportFailure);
                    return false;
                }

                MeshMetrics meshMetrics = ResolveMeshMetrics(settings.SourceMesh);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                result = new BakeResult
                {
                    AlbedoPath = albedoPath,
                    MraoPath = mraoPath,
                    DisplacementPath = displacementPath,
                    CavitationFlipbookPath = cavitationPath,
                    Dimensions = dimensions,
                    Mesh = meshMetrics,
                    AlbedoMetrics = albedoMetrics,
                    MraoMetrics = mraoMetrics,
                    DisplacementMetrics = displacementMetrics,
                    CavitationMetrics = cavitationMetrics
                };

                Debug.Log("[HullCavitationBaker1722] Bake complete. Assets imported and validated.");
                return true;
            }
            finally
            {
                DestroyImmediateSafe(albedoTexture);
                DestroyImmediateSafe(mraoTexture);
                DestroyImmediateSafe(displacementTexture);
                DestroyImmediateSafe(cavitationTexture);
                ReleaseGraphicsBuffer(meshVertexBuffer);
                ReleaseRenderTexture(albedoRt);
                ReleaseRenderTexture(mraoRt);
                ReleaseRenderTexture(displacementRt);
                ReleaseRenderTexture(cavitationRt);
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

            RenderTexture rt = null;
            try
            {
                rt = CreateRenderTexture(64, 64, RenderTextureFormat.ARGBFloat, "Hull1722_DryRun_RT");
                BakeSettings settings = BakeSettings.Default();
                BakeDimensions dimensions = new BakeDimensions(64, 64, 8, CavitationFrameCount, CavitationTilesPerAxis);
                ConfigureComputeConstants(computeShader, settings, dimensions);
                GraphicsBuffer meshBuffer = null;
                try
                {
                    meshBuffer = CreateFallbackMeshVertexBuffer();
                    DispatchKernel(computeShader, "CSBakeHullAlbedo", "_OutputAlbedo", rt, meshBuffer, 0);
                    DispatchKernel(computeShader, "CSBakeHullMrao", "_OutputMrao", rt, meshBuffer, 0);
                    DispatchKernel(computeShader, "CSBakeHullDisplacement", "_OutputDisplacement", rt, meshBuffer, 0);
                    DispatchKernel(computeShader, "CSBakeCavitationFlipbook", "_OutputCavitation", rt, meshBuffer, 0);
                }
                finally
                {
                    ReleaseGraphicsBuffer(meshBuffer);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
            finally
            {
                ReleaseRenderTexture(rt);
            }
        }

        private static BakeSettings SanitizeSettings(BakeSettings requested)
        {
            BakeSettings settings = requested;
            settings.AssetName = string.IsNullOrWhiteSpace(settings.AssetName) ? DefaultAssetName : settings.AssetName;
            settings.OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder) ? DefaultOutputFolder : settings.OutputFolder.Replace('\\', '/');
            settings.GlobalQualityWeight = Mathf.Clamp01(FiniteOrDefault(settings.GlobalQualityWeight, 1f));
            settings.Seed = FiniteOrDefault(settings.Seed, 1722f);
            settings.DisplacementMeters = Mathf.Clamp(FiniteOrDefault(settings.DisplacementMeters, 0.18f), 0.05f, 0.65f);
            settings.ScarGain = Mathf.Clamp(FiniteOrDefault(settings.ScarGain, 1f), 0f, 2f);
            settings.CavitationSwirlGain = Mathf.Clamp(FiniteOrDefault(settings.CavitationSwirlGain, 1f), 0f, 2f);
            settings.CavitationBubbleGain = Mathf.Clamp(FiniteOrDefault(settings.CavitationBubbleGain, 1f), 0f, 2f);
            return settings;
        }

        private static BakeDimensions ResolveDimensions(float qualityWeight)
        {
            float quality = Mathf.Clamp01(FiniteOrDefault(qualityWeight, 1f));
            int hullResolution = Align(Mathf.RoundToInt(Mathf.Lerp(MinimumHullResolution, MaximumHullResolution, quality)), 64);
            int tileResolution = Align(Mathf.RoundToInt(Mathf.Lerp(MinimumCavitationTile, MaximumCavitationTile, quality)), 16);
            hullResolution = Mathf.Clamp(hullResolution, MinimumHullResolution, MaximumHullResolution);
            tileResolution = Mathf.Clamp(tileResolution, MinimumCavitationTile, MaximumCavitationTile);
            int atlasResolution = tileResolution * CavitationTilesPerAxis;
            return new BakeDimensions(hullResolution, atlasResolution, tileResolution, CavitationFrameCount, CavitationTilesPerAxis);
        }

        private static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format, string name)
        {
            RenderTexture rt = new RenderTexture(width, height, 0, format)
            {
                name = name,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        private static void ConfigureComputeConstants(ComputeShader computeShader, BakeSettings settings, BakeDimensions dimensions)
        {
            int dentCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(8f, 32f, settings.GlobalQualityWeight)), 6, 32);
            computeShader.SetVector("_BakeParams", new Vector4(settings.GlobalQualityWeight, settings.Seed, dentCount, settings.ScarGain));
            computeShader.SetVector("_CavitationParams", new Vector4(dimensions.CavitationFrames, dimensions.CavitationTilesPerAxis, settings.CavitationSwirlGain, settings.CavitationBubbleGain));
        }

        private static GraphicsBuffer CreateMeshVertexBuffer(Mesh mesh, out int sourceVertexCount, out string failure)
        {
            sourceVertexCount = 0;
            failure = string.Empty;
            if (mesh == null || mesh.vertexCount <= 0)
                return CreateFallbackMeshVertexBuffer();

            if (mesh.vertexCount > MeshVertexScratch.Capacity)
            {
                failure = "source mesh exceeds prewarmed vertex scratch capacity. ";
                return null;
            }

            MeshVertexScratch.Clear();
            mesh.GetVertices(MeshVertexScratch);
            if (MeshVertexScratch.Count == 0)
                return CreateFallbackMeshVertexBuffer();

            sourceVertexCount = MeshVertexScratch.Count;
            GraphicsBuffer buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MeshVertexScratch.Count, sizeof(float) * 3);
            buffer.SetData(MeshVertexScratch);
            return buffer;
        }

        private static GraphicsBuffer CreateFallbackMeshVertexBuffer()
        {
            GraphicsBuffer buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(float) * 3);
            buffer.SetData(FallbackMeshVertex);
            return buffer;
        }

        private static void DispatchKernel(ComputeShader computeShader, string kernelName, string outputName, RenderTexture output, GraphicsBuffer meshVertexBuffer, int sourceVertexCount)
        {
            int kernel = computeShader.FindKernel(kernelName);
            computeShader.SetTexture(kernel, outputName, output);
            computeShader.SetInt("_SourceMeshVertexCount", Mathf.Max(0, sourceVertexCount));
            computeShader.SetBuffer(kernel, "_SourceMeshVertices", meshVertexBuffer);
            int groupsX = Mathf.CeilToInt(output.width / (float)ThreadGroupSize);
            int groupsY = Mathf.CeilToInt(output.height / (float)ThreadGroupSize);
            computeShader.Dispatch(kernel, groupsX, groupsY, 1);
        }

        private static Texture2D ReadbackTexture(RenderTexture source, TextureFormat format, bool linear, string name)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = new Texture2D(source.width, source.height, format, false, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
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

        private static bool ValidateTexturePixels(Texture2D texture, int expectedWidth, int expectedHeight, bool displacementMap, out PixelMetrics metrics, out string failure)
        {
            metrics = default;
            failure = string.Empty;
            if (texture == null)
            {
                failure = "texture null. ";
                return false;
            }

            int expectedPixels = expectedWidth * expectedHeight;
            if (texture.width != expectedWidth || texture.height != expectedHeight)
            {
                failure = "texture dimensions mismatch. ";
                return false;
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            double alphaSum = 0.0;
            int invalidCount = 0;

            if (displacementMap)
            {
                NativeArray<Color> pixels = texture.GetPixelData<Color>(0);
                if (!pixels.IsCreated || pixels.Length != expectedPixels)
                {
                    failure = "pixel count mismatch. ";
                    return false;
                }

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color pixel = pixels[i];
                    if (!IsFinite(pixel.r) || !IsFinite(pixel.g) || !IsFinite(pixel.b) || !IsFinite(pixel.a))
                    {
                        invalidCount++;
                        continue;
                    }

                    if (pixel.r < -0.001f || pixel.r > 1.001f)
                        invalidCount++;

                    min = Mathf.Min(Mathf.Min(min, pixel.r), Mathf.Min(pixel.g, Mathf.Min(pixel.b, pixel.a)));
                    max = Mathf.Max(Mathf.Max(max, pixel.r), Mathf.Max(pixel.g, Mathf.Max(pixel.b, pixel.a)));
                    alphaSum += pixel.a;
                }
            }
            else
            {
                NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
                if (!pixels.IsCreated || pixels.Length != expectedPixels)
                {
                    failure = "pixel count mismatch. ";
                    return false;
                }

                const float inv255 = 1f / 255f;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    float r = pixel.r * inv255;
                    float g = pixel.g * inv255;
                    float b = pixel.b * inv255;
                    float a = pixel.a * inv255;
                    min = Mathf.Min(Mathf.Min(min, r), Mathf.Min(g, Mathf.Min(b, a)));
                    max = Mathf.Max(Mathf.Max(max, r), Mathf.Max(g, Mathf.Max(b, a)));
                    alphaSum += a;
                }
            }

            metrics = new PixelMetrics
            {
                Width = expectedWidth,
                Height = expectedHeight,
                PixelCount = expectedPixels,
                InvalidPixelCount = invalidCount,
                MinChannel = min,
                MaxChannel = max,
                AverageAlpha = expectedPixels > 0 ? (float)(alphaSum / expectedPixels) : 0f
            };

            if (invalidCount > 0)
            {
                failure = "invalid pixels. ";
                return false;
            }

            return true;
        }

        private static bool ValidateEncodedBytes(byte[] bytes, string label)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError("[HullCavitationBaker1722] Encoded " + label + " texture is empty.");
                return false;
            }

            if (bytes.LongLength > MaxEncodedTextureBytes)
            {
                Debug.LogError("[HullCavitationBaker1722] Encoded texture exceeds safety limit.");
                return false;
            }

            return true;
        }

        private static bool ConfigureTextureImporter(string assetPath, bool srgb, bool mipmaps, TextureWrapMode wrapMode, FilterMode filterMode, int maxSize, out string failure)
        {
            failure = string.Empty;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failure = "missing TextureImporter for " + assetPath + ". ";
                return false;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = wrapMode;
            importer.filterMode = filterMode;
            importer.maxTextureSize = maxSize;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.None;

            TextureImporterPlatformSettings standalone = new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = maxSize,
                format = TextureImporterFormat.BC7,
                compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(standalone);

            TextureImporterPlatformSettings android = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = maxSize,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(android);

            TextureImporterPlatformSettings ios = new TextureImporterPlatformSettings
            {
                name = "iPhone",
                overridden = true,
                maxTextureSize = maxSize,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(ios);

            importer.SaveAndReimport();
            return true;
        }

        private static MeshMetrics ResolveMeshMetrics(Mesh mesh)
        {
            if (mesh == null)
                return MeshMetrics.Empty;

            Bounds bounds = mesh.bounds;
            return new MeshMetrics
            {
                MeshName = mesh.name,
                VertexCount = mesh.vertexCount,
                TriangleCount = ResolveTriangleCount(mesh),
                BoundsSize = bounds.size,
                BoundsCenter = bounds.center
            };
        }

        private static int ResolveTriangleCount(Mesh mesh)
        {
            long indexCount = 0L;
            int subMeshCount = Mathf.Max(0, mesh.subMeshCount);
            for (int i = 0; i < subMeshCount; i++)
            {
                indexCount += (long)mesh.GetIndexCount(i);
            }

            long triangleCount = indexCount / 3L;
            return triangleCount > int.MaxValue ? int.MaxValue : (int)triangleCount;
        }

        private static void WriteBytesToAssetPath(string assetPath, byte[] bytes)
        {
            string fullPath = ProjectRelativeToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, bytes);
        }

        private static string ProjectRelativeToFullPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            string normalized = path.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", normalized));
        }

        private static void ReleaseRenderTexture(RenderTexture rt)
        {
            if (rt == null)
                return;

            rt.Release();
            DestroyImmediateSafe(rt);
        }

        private static void ReleaseGraphicsBuffer(GraphicsBuffer buffer)
        {
            if (buffer != null)
                buffer.Release();
        }

        private static void DestroyImmediateSafe(UnityEngine.Object obj)
        {
            if (obj != null)
                DestroyImmediate(obj);
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

        public struct BakeSettings
        {
            public Mesh SourceMesh;
            public ComputeShader ComputeShader;
            public string AssetName;
            public string OutputFolder;
            public float GlobalQualityWeight;
            public float Seed;
            public float DisplacementMeters;
            public float ScarGain;
            public float CavitationSwirlGain;
            public float CavitationBubbleGain;

            public static BakeSettings Default()
            {
                return new BakeSettings
                {
                    SourceMesh = null,
                    ComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderPath),
                    AssetName = DefaultAssetName,
                    OutputFolder = DefaultOutputFolder,
                    GlobalQualityWeight = 1f,
                    Seed = 1722f,
                    DisplacementMeters = 0.18f,
                    ScarGain = 1f,
                    CavitationSwirlGain = 1f,
                    CavitationBubbleGain = 1f
                };
            }
        }

        public struct BakeResult
        {
            public string AlbedoPath;
            public string MraoPath;
            public string DisplacementPath;
            public string CavitationFlipbookPath;
            public BakeDimensions Dimensions;
            public MeshMetrics Mesh;
            public PixelMetrics AlbedoMetrics;
            public PixelMetrics MraoMetrics;
            public PixelMetrics DisplacementMetrics;
            public PixelMetrics CavitationMetrics;
        }

        public readonly struct BakeDimensions
        {
            public readonly int HullResolution;
            public readonly int CavitationAtlasResolution;
            public readonly int CavitationTileResolution;
            public readonly int CavitationFrames;
            public readonly int CavitationTilesPerAxis;

            public int HullPixelCount => HullResolution * HullResolution;
            public int CavitationPixelCount => CavitationAtlasResolution * CavitationAtlasResolution;

            public BakeDimensions(int hullResolution, int cavitationAtlasResolution, int cavitationTileResolution, int cavitationFrames, int cavitationTilesPerAxis)
            {
                HullResolution = hullResolution;
                CavitationAtlasResolution = cavitationAtlasResolution;
                CavitationTileResolution = cavitationTileResolution;
                CavitationFrames = cavitationFrames;
                CavitationTilesPerAxis = cavitationTilesPerAxis;
            }
        }

        public struct MeshMetrics
        {
            public static readonly MeshMetrics Empty = new MeshMetrics
            {
                MeshName = "none",
                VertexCount = 0,
                TriangleCount = 0,
                BoundsSize = Vector3.zero,
                BoundsCenter = Vector3.zero
            };

            public string MeshName;
            public int VertexCount;
            public int TriangleCount;
            public Vector3 BoundsSize;
            public Vector3 BoundsCenter;
        }

        public struct PixelMetrics
        {
            public int Width;
            public int Height;
            public int PixelCount;
            public int InvalidPixelCount;
            public float MinChannel;
            public float MaxChannel;
            public float AverageAlpha;
        }
    }
}
#endif
