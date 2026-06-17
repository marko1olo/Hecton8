#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Bakers
{
    public sealed class VisorMaskBaker : EditorWindow
    {
        private const string MenuRoot = "Hecton8/Bakers/1726/";
        private const string DefaultComputePath = "Assets/_Project/Art/Shaders/Include/VisorMaskBaker.compute";
        private const string DefaultOutputFolder = "Assets/_Project/Art/Textures/UI";
        private const string DefaultVisorMaterialPath = "Assets/_Project/Art/Materials/Mat_Visor_Glass.mat";
        private const string DefaultAssetName = "survival_glass_wear";
        private const int MinimumResolution = 512;
        private const int MaximumResolution = 2048;
        private const int FlipbookFrames = 64;
        private const int FlipbookGrid = 8;
        private const long MaxEncodedPngBytes = 96L * 1024L * 1024L;

        private static readonly int s_outputId = Shader.PropertyToID("_VisorMaskOutput");
        private static readonly int s_paramsId = Shader.PropertyToID("_VisorBakeParams1726");
        private static readonly int s_visorMaskTexId = Shader.PropertyToID("_VisorMaskTex");
        private static readonly int s_visorMaskStrengthsId = Shader.PropertyToID("_VisorMaskStrengths");
        private static readonly int s_visorMaskUvShiftId = Shader.PropertyToID("_VisorMaskUvShift");
        private static readonly int s_visorCondensationFlipbookId = Shader.PropertyToID("_VisorCondensationFlipbook");
        private static RenderTexture s_activeRenderTexture;
        private static GraphicsBuffer s_activeParamsBuffer;

        [SerializeField] private ComputeShader _computeShader;
        [SerializeField] private string _assetName = DefaultAssetName;
        [SerializeField] private string _outputFolder = DefaultOutputFolder;
        [SerializeField, Range(0f, 1f)] private float _globalQualityWeight = 1f;
        [SerializeField] private int _seed = 1726;
        [SerializeField, Range(0f, 2f)] private float _dirtStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float _scratchStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float _saltStrength = 0.9f;
        [SerializeField, Range(0f, 2f)] private float _condensationStrength = 1f;
        [SerializeField, Range(0.5f, 6f)] private float _edgeExponent = 2.35f;
        [SerializeField, Range(0f, 1f)] private float _saltEdgeBias = 0.58f;
        [SerializeField, Range(0f, 2f)] private float _scratchDensity = 1f;
        [SerializeField] private string _lastStatus = "Idle.";

        [StructLayout(LayoutKind.Sequential)]
        private struct GpuBakeParams
        {
            public Vector4 ResolutionSeedQuality;
            public Vector4 Strengths;
            public Vector4 Shape;
        }

        private static readonly GpuBakeParams[] s_paramsPayload = new GpuBakeParams[1];

        private readonly struct BakeSettings
        {
            public readonly ComputeShader ComputeShader;
            public readonly string AssetName;
            public readonly string OutputFolder;
            public readonly float GlobalQualityWeight;
            public readonly int Seed;
            public readonly float DirtStrength;
            public readonly float ScratchStrength;
            public readonly float SaltStrength;
            public readonly float CondensationStrength;
            public readonly float EdgeExponent;
            public readonly float SaltEdgeBias;
            public readonly float ScratchDensity;

            public BakeSettings(
                ComputeShader computeShader,
                string assetName,
                string outputFolder,
                float globalQualityWeight,
                int seed,
                float dirtStrength,
                float scratchStrength,
                float saltStrength,
                float condensationStrength,
                float edgeExponent,
                float saltEdgeBias,
                float scratchDensity)
            {
                ComputeShader = computeShader;
                AssetName = assetName;
                OutputFolder = outputFolder;
                GlobalQualityWeight = globalQualityWeight;
                Seed = seed;
                DirtStrength = dirtStrength;
                ScratchStrength = scratchStrength;
                SaltStrength = saltStrength;
                CondensationStrength = condensationStrength;
                EdgeExponent = edgeExponent;
                SaltEdgeBias = saltEdgeBias;
                ScratchDensity = scratchDensity;
            }

            public static BakeSettings Default()
            {
                return new BakeSettings(
                    null,
                    DefaultAssetName,
                    DefaultOutputFolder,
                    1f,
                    1726,
                    1f,
                    1f,
                    0.9f,
                    1f,
                    2.35f,
                    0.58f,
                    1f);
            }
        }

        private readonly struct BakeDimensions
        {
            public readonly int Resolution;
            public readonly int FlipbookFrames;
            public readonly int FlipbookGrid;

            public BakeDimensions(int resolution, int flipbookFrames, int flipbookGrid)
            {
                Resolution = resolution;
                FlipbookFrames = flipbookFrames;
                FlipbookGrid = flipbookGrid;
            }

            public long PixelCount => (long)Resolution * Resolution;
            public int TileResolution => Resolution / FlipbookGrid;
        }

        private readonly struct BakeResult
        {
            public readonly string TexturePath;
            public readonly BakeDimensions Dimensions;

            public BakeResult(
                string texturePath,
                BakeDimensions dimensions)
            {
                TexturePath = texturePath;
                Dimensions = dimensions;
            }
        }

        [MenuItem(MenuRoot + "Open Visor Mask Baker", false, 1726)]
        private static void Open()
        {
            VisorMaskBaker window = GetWindow<VisorMaskBaker>();
            window.titleContent = new GUIContent("Visor Mask 1726");
            window.minSize = new Vector2(500f, 470f);
        }

        [InitializeOnLoadMethod]
        private static void RegisterReloadCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseTrackedGpuState;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseTrackedGpuState;
        }

        [MenuItem(MenuRoot + "Bake Default Visor Mask", false, 1727)]
        private static void BakeDefaultMenu()
        {
            if (TryBake(BakeSettings.Default(), out BakeResult result, out string failure))
            {
                Debug.Log("[VisorMaskBaker1726] Baked " + result.TexturePath +
                          " px=" + result.Dimensions.PixelCount.ToString(CultureInfo.InvariantCulture));
                return;
            }

            Debug.LogError("[VisorMaskBaker1726] " + failure);
        }

        [MenuItem(MenuRoot + "Dry Run Kernel", false, 1728)]
        private static void DryRunMenu()
        {
            ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputePath);
            if (TryDryRun(compute, out string failure))
            {
                Debug.Log("[VisorMaskBaker1726] Dry-run dispatch succeeded.");
                return;
            }

            Debug.LogError("[VisorMaskBaker1726] Dry-run failed: " + failure);
        }

        private void OnEnable()
        {
            if (_computeShader == null)
                _computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputePath);
        }

        private void OnDisable()
        {
            ReleaseTrackedGpuState();
        }

        private void OnDestroy()
        {
            ReleaseTrackedGpuState();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Visor Glass Dirt, Scratch, Salt, Condensation Mask", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _computeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", _computeShader, typeof(ComputeShader), false);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _seed = EditorGUILayout.IntField("Seed", Mathf.Max(1, _seed));
            _dirtStrength = EditorGUILayout.Slider("Dirt / Fingerprints", _dirtStrength, 0f, 2f);
            _scratchStrength = EditorGUILayout.Slider("Scratches", _scratchStrength, 0f, 2f);
            _saltStrength = EditorGUILayout.Slider("Salt Crust", _saltStrength, 0f, 2f);
            _condensationStrength = EditorGUILayout.Slider("Condensation", _condensationStrength, 0f, 2f);
            _edgeExponent = EditorGUILayout.Slider("Edge Exponent", _edgeExponent, 0.5f, 6f);
            _saltEdgeBias = EditorGUILayout.Slider("Salt Edge Bias", _saltEdgeBias, 0f, 1f);
            _scratchDensity = EditorGUILayout.Slider("Scratch Density", _scratchDensity, 0f, 2f);

            BakeDimensions dimensions = ResolveDimensions(_globalQualityWeight);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Packed Mask", dimensions.Resolution.ToString(CultureInfo.InvariantCulture) + " x " +
                                                      dimensions.Resolution.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Channels", "R=dirt, G=scratch, B=salt, A=64-frame condensation");
            EditorGUILayout.LabelField("Flipbook", FlipbookGrid.ToString(CultureInfo.InvariantCulture) + " x " +
                                                   FlipbookGrid.ToString(CultureInfo.InvariantCulture) + " | tile " +
                                                   dimensions.TileResolution.ToString(CultureInfo.InvariantCulture) + " px");

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Visor Mask", GUILayout.Height(32f)))
                BakeFromWindow();

            if (GUILayout.Button("Dry Run Kernel", GUILayout.Height(28f)))
                DryRunFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void BakeFromWindow()
        {
            BakeSettings settings = new BakeSettings(
                _computeShader,
                _assetName,
                _outputFolder,
                _globalQualityWeight,
                _seed,
                _dirtStrength,
                _scratchStrength,
                _saltStrength,
                _condensationStrength,
                _edgeExponent,
                _saltEdgeBias,
                _scratchDensity);

            if (TryBake(settings, out BakeResult result, out string failure))
            {
                _lastStatus = "Baked " + result.TexturePath;
                return;
            }

            _lastStatus = "Bake failed: " + failure;
            Debug.LogError("[VisorMaskBaker1726] " + failure);
        }

        private void DryRunFromWindow()
        {
            ComputeShader compute = _computeShader != null ? _computeShader : AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputePath);
            if (TryDryRun(compute, out string failure))
            {
                _lastStatus = "Dry-run dispatch passed.";
                return;
            }

            _lastStatus = "Dry-run failed: " + failure;
            Debug.LogError("[VisorMaskBaker1726] " + failure);
        }

        private static bool TryBake(BakeSettings requestedSettings, out BakeResult result, out string failure)
        {
            result = default;
            failure = string.Empty;
            BakeSettings settings = SanitizeSettings(requestedSettings);
            BakeDimensions dimensions = ResolveDimensions(settings.GlobalQualityWeight);

            if (!SystemInfo.supportsComputeShaders)
            {
                failure = "compute shaders are unavailable on this editor device";
                return false;
            }

            if (!TryValidateMaskGraphicsFormatSupport(out failure))
                return false;

            ComputeShader compute = settings.ComputeShader != null
                ? settings.ComputeShader
                : AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputePath);
            if (compute == null)
            {
                failure = "missing compute shader at " + DefaultComputePath;
                return false;
            }

            if (!TryCaptureDefaultVisorMaterialSnapshot(out VisorMaterialBindingSnapshot materialSnapshot, out string materialContractFailure))
            {
                failure = materialContractFailure;
                return false;
            }

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(settings.OutputFolder, out string outputFolder, out string folderFailure))
            {
                failure = "output folder invalid: " + folderFailure;
                return false;
            }

            string safeName = ProceduralTextureBaker.SanitizeAssetNameForPath(settings.AssetName);
            if (string.IsNullOrEmpty(safeName))
                safeName = DefaultAssetName;

            string texturePath = outputFolder + "/TX_Visor_" + safeName + "_Masks.png";
            if (!ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(texturePath, out ProceduralTextureBaker.AssetFileRollbackSnapshot[] rollback, out string rollbackFailure))
            {
                failure = "output rollback capture failed: " + rollbackFailure;
                return false;
            }

            RenderTexture maskRt = null;
            Texture2D maskTexture = null;
            GraphicsBuffer paramsBuffer = null;
            bool committed = false;
            bool materialApplied = false;
            try
            {
                if (!TryCreateParamsBuffer(settings, dimensions, out paramsBuffer, out string paramsFailure))
                {
                    failure = paramsFailure;
                    return false;
                }
                TrackParamsBuffer(paramsBuffer);

                if (!TryCreateMaskRenderTexture(dimensions.Resolution, "VisorMask1726_RT", out maskRt, out string renderTextureFailure))
                {
                    failure = renderTextureFailure;
                    return false;
                }
                TrackRenderTexture(maskRt);
                if (!DispatchBake(compute, maskRt, paramsBuffer, out _, out _, out _, out _, out string dispatchFailure))
                {
                    failure = dispatchFailure;
                    return false;
                }

                maskTexture = ReadbackTexture(maskRt, "VisorMask1726_CPU");
                if (!ValidateMask(maskTexture, dimensions, out string validationFailure))
                {
                    failure = validationFailure;
                    Debug.LogError("[VisorMaskBaker1726] Visor mask validation violation detected! " + validationFailure);
                    return false;
                }

                byte[] pngBytes = ImageConversion.EncodeToPNG(maskTexture);
                if (!ValidateEncodedBytes(pngBytes, out string bytesFailure))
                {
                    failure = bytesFailure;
                    return false;
                }

                if (!ProceduralTextureBaker.TryWriteBytesAtomic(texturePath, pngBytes, out string writeFailure))
                {
                    failure = "texture write failed: " + writeFailure;
                    return false;
                }

                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                if (!ConfigureTextureImporter(texturePath, dimensions.Resolution, out string importFailure))
                {
                    failure = importFailure;
                    return false;
                }

                if (!TryApplyMaskToDefaultVisorMaterial(in materialSnapshot, texturePath, out string materialFailure))
                {
                    failure = materialFailure;
                    return false;
                }

                materialApplied = true;
                if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("visor mask bake 1726", out string finalizeFailure))
                {
                    failure = finalizeFailure;
                    return false;
                }

                result = new BakeResult(texturePath, dimensions);
                committed = true;
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (!committed)
                {
                    if (materialApplied)
                        RestoreVisorMaterialBinding(in materialSnapshot);

                    ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(rollback);
                }

                DestroyImmediateSafe(maskTexture);
                ReleaseTrackedGpuState();
            }
        }

        private static bool TryDryRun(ComputeShader compute, out string failure)
        {
            failure = string.Empty;
            if (compute == null)
            {
                failure = "compute shader is null";
                return false;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                failure = "compute shaders are unavailable on this editor device";
                return false;
            }

            if (!TryValidateMaskGraphicsFormatSupport(out failure))
                return false;

            RenderTexture rt = null;
            GraphicsBuffer buffer = null;
            try
            {
                BakeSettings settings = BakeSettings.Default();
                BakeDimensions dimensions = new BakeDimensions(64, FlipbookFrames, FlipbookGrid);
                if (!TryCreateParamsBuffer(settings, dimensions, out buffer, out failure))
                    return false;
                TrackParamsBuffer(buffer);

                if (!TryCreateMaskRenderTexture(64, "VisorMask1726_DryRun_RT", out rt, out failure))
                    return false;
                TrackRenderTexture(rt);
                return DispatchBake(compute, rt, buffer, out _, out _, out _, out _, out failure);
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                ReleaseTrackedGpuState();
            }
        }

        private static BakeSettings SanitizeSettings(BakeSettings requested)
        {
            return new BakeSettings(
                requested.ComputeShader,
                string.IsNullOrWhiteSpace(requested.AssetName) ? DefaultAssetName : requested.AssetName,
                string.IsNullOrWhiteSpace(requested.OutputFolder) ? DefaultOutputFolder : requested.OutputFolder.Replace('\\', '/'),
                Mathf.Clamp01(FiniteOrDefault(requested.GlobalQualityWeight, 1f)),
                Mathf.Max(1, requested.Seed),
                Mathf.Clamp(FiniteOrDefault(requested.DirtStrength, 1f), 0f, 2f),
                Mathf.Clamp(FiniteOrDefault(requested.ScratchStrength, 1f), 0f, 2f),
                Mathf.Clamp(FiniteOrDefault(requested.SaltStrength, 0.9f), 0f, 2f),
                Mathf.Clamp(FiniteOrDefault(requested.CondensationStrength, 1f), 0f, 2f),
                Mathf.Clamp(FiniteOrDefault(requested.EdgeExponent, 2.35f), 0.5f, 6f),
                Mathf.Clamp01(FiniteOrDefault(requested.SaltEdgeBias, 0.58f)),
                Mathf.Clamp(FiniteOrDefault(requested.ScratchDensity, 1f), 0f, 2f));
        }

        private static BakeDimensions ResolveDimensions(float globalQualityWeight)
        {
            float q = Mathf.Clamp01(FiniteOrDefault(globalQualityWeight, 1f));
            float smooth = q * q * (3f - 2f * q);
            int resolution = Align(Mathf.RoundToInt(Mathf.Lerp(MinimumResolution, MaximumResolution, smooth)), 64);
            resolution = Mathf.Clamp(resolution, MinimumResolution, MaximumResolution);
            return new BakeDimensions(resolution, FlipbookFrames, FlipbookGrid);
        }

        private static bool TryCreateParamsBuffer(BakeSettings settings, BakeDimensions dimensions, out GraphicsBuffer buffer, out string failure)
        {
            buffer = null;
            failure = string.Empty;
            try
            {
                s_paramsPayload[0] = new GpuBakeParams
                {
                    ResolutionSeedQuality = new Vector4(dimensions.Resolution, dimensions.Resolution, settings.Seed, settings.GlobalQualityWeight),
                    Strengths = new Vector4(settings.DirtStrength, settings.ScratchStrength, settings.SaltStrength, settings.CondensationStrength),
                    Shape = new Vector4(settings.EdgeExponent, settings.SaltEdgeBias, settings.ScratchDensity, FlipbookGrid)
                };

                int stride = ResolveGpuBakeParamsStride();
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, stride);
                buffer.SetData(s_paramsPayload);
                return true;
            }
            catch (Exception ex)
            {
                if (buffer != null)
                    buffer.Release();
                buffer = null;
                failure = "params buffer failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryCreateMaskRenderTexture(int resolution, string name, out RenderTexture rt, out string failure)
        {
            rt = null;
            failure = string.Empty;
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R8G8B8A8_UNorm, 0)
            {
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false,
                msaaSamples = 1,
                depthBufferBits = 0
            };

            rt = new RenderTexture(descriptor)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (!rt.Create())
            {
                DestroyImmediateSafe(rt);
                rt = null;
                failure = "RenderTexture allocation failed for " + name + " at " + resolution.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static int ResolveGpuBakeParamsStride()
        {
            int unsafeSize = UnsafeUtility.SizeOf<GpuBakeParams>();
            int marshalSize = Marshal.SizeOf<GpuBakeParams>();
            if (unsafeSize <= 0 || marshalSize <= 0 || unsafeSize != marshalSize || (unsafeSize & 7) != 0)
                throw new InvalidOperationException("GpuBakeParams stride invalid: unsafe=" + unsafeSize.ToString(CultureInfo.InvariantCulture) + " marshal=" + marshalSize.ToString(CultureInfo.InvariantCulture));

            return unsafeSize;
        }

        private static bool TryValidateMaskGraphicsFormatSupport(out string failure)
        {
            bool supportsLoadStore = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.LoadStore);
            bool supportsReadPixels = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.ReadPixels);
            if (supportsLoadStore && supportsReadPixels)
            {
                failure = string.Empty;
                return true;
            }

            failure = "R8G8B8A8_UNorm unsupported for visor bake: loadStore=" +
                      (supportsLoadStore ? "true" : "false") +
                      " readPixels=" +
                      (supportsReadPixels ? "true" : "false");
            return false;
        }

        private static bool DispatchBake(
            ComputeShader compute,
            RenderTexture output,
            GraphicsBuffer paramsBuffer,
            out uint groupSizeX,
            out uint groupSizeY,
            out int groupsX,
            out int groupsY,
            out string failure)
        {
            groupSizeX = 0u;
            groupSizeY = 0u;
            groupsX = 0;
            groupsY = 0;
            failure = string.Empty;
            if (!compute.HasKernel("CSBakeVisorMasks1726"))
            {
                failure = "missing kernel CSBakeVisorMasks1726";
                return false;
            }

            int kernel = compute.FindKernel("CSBakeVisorMasks1726");
            compute.SetTexture(kernel, s_outputId, output);
            compute.SetBuffer(kernel, s_paramsId, paramsBuffer);
            compute.GetKernelThreadGroupSizes(kernel, out groupSizeX, out groupSizeY, out uint _);
            if (output.width != output.height)
            {
                failure = "visor mask output must be square";
                return false;
            }

            if (!ProceduralTextureBaker.TryResolveDispatchGroups(output.width, groupSizeX, groupSizeY, out groupsX, out groupsY, out failure))
                return false;

            compute.Dispatch(kernel, groupsX, groupsY, 1);
            return true;
        }

        private static Texture2D ReadbackTexture(RenderTexture source, string name)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
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

        private static bool ValidateMask(Texture2D texture, BakeDimensions dimensions, out string failure)
        {
            failure = string.Empty;
            if (texture == null)
            {
                failure = "mask texture is null";
                return false;
            }

            if (texture.width != dimensions.Resolution || texture.height != dimensions.Resolution)
            {
                failure = "mask dimensions mismatch";
                return false;
            }

            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            long expectedPixels = dimensions.PixelCount;
            if (!pixels.IsCreated || pixels.Length != expectedPixels)
            {
                failure = "mask pixel count mismatch";
                return false;
            }

            byte maxR = byte.MinValue;
            byte maxG = byte.MinValue;
            byte maxB = byte.MinValue;
            byte maxA = byte.MinValue;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.r > maxR)
                    maxR = p.r;
                if (p.g > maxG)
                    maxG = p.g;
                if (p.b > maxB)
                    maxB = p.b;
                if (p.a > maxA)
                    maxA = p.a;
            }

            const float invByte = 1f / 255f;
            float alphaLoopDelta = ResolveAlphaLoopDelta(pixels, dimensions);
            if (maxR * invByte <= 0.05f || maxG * invByte <= 0.05f || maxB * invByte <= 0.05f || maxA * invByte <= 0.05f)
            {
                failure = "one or more packed channels are empty";
                return false;
            }

            if (alphaLoopDelta > 0.05f)
            {
                failure = "condensation alpha loop discontinuity too high=" + alphaLoopDelta.ToString("0.######", CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static float ResolveAlphaLoopDelta(NativeArray<Color32> pixels, BakeDimensions dimensions)
        {
            int tile = Mathf.Max(1, dimensions.TileResolution);
            int lastFrameX = (FlipbookFrames - 1) % FlipbookGrid;
            int lastFrameY = (FlipbookFrames - 1) / FlipbookGrid;
            int sampleStep = Mathf.Max(1, tile / 16);
            double sum = 0.0;
            int count = 0;
            int resolution = dimensions.Resolution;
            for (int y = sampleStep / 2; y < tile; y += sampleStep)
            {
                for (int x = sampleStep / 2; x < tile; x += sampleStep)
                {
                    int frame0Index = y * resolution + x;
                    int frame63Index = (lastFrameY * tile + y) * resolution + lastFrameX * tile + x;
                    float a0 = pixels[frame0Index].a * (1f / 255f);
                    float a63 = pixels[frame63Index].a * (1f / 255f);
                    sum += Mathf.Abs(a0 - a63);
                    count++;
                }
            }

            return count > 0 ? (float)(sum / count) : 0f;
        }

        private static bool ValidateEncodedBytes(byte[] bytes, out string failure)
        {
            failure = string.Empty;
            if (bytes == null || bytes.Length == 0)
            {
                failure = "encoded PNG is empty";
                return false;
            }

            if (bytes.LongLength > MaxEncodedPngBytes)
            {
                failure = "encoded PNG exceeds safety limit: " + bytes.LongLength.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static bool ConfigureTextureImporter(string assetPath, int maxSize, out string failure)
        {
            failure = string.Empty;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failure = "missing TextureImporter for " + assetPath;
                return false;
            }

            int clampedMaxSize = Mathf.Clamp(maxSize, MinimumResolution, MaximumResolution);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.maxTextureSize = clampedMaxSize;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            TextureImporterPlatformSettings standalone = new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = clampedMaxSize,
                format = TextureImporterFormat.BC7,
                compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(standalone);

            TextureImporterPlatformSettings android = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = clampedMaxSize,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(android);

            TextureImporterPlatformSettings ios = new TextureImporterPlatformSettings
            {
                name = "iPhone",
                overridden = true,
                maxTextureSize = clampedMaxSize,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(ios);
            importer.SaveAndReimport();
            return AuditVisorTextureImporter(assetPath, clampedMaxSize, out failure);
        }

        private static bool AuditVisorTextureImporter(string assetPath, int expectedMaxSize, out string failure)
        {
            failure = string.Empty;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failure = "missing TextureImporter for audit " + assetPath;
                return false;
            }

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iPhone");

            bool importerCorrect =
                importer.textureType == TextureImporterType.Default &&
                !importer.sRGBTexture &&
                !importer.mipmapEnabled &&
                !importer.streamingMipmaps &&
                !importer.isReadable &&
                importer.alphaSource == TextureImporterAlphaSource.FromInput &&
                !importer.alphaIsTransparency &&
                importer.wrapMode == TextureWrapMode.Clamp &&
                importer.filterMode == FilterMode.Bilinear &&
                importer.anisoLevel == 1 &&
                importer.textureCompression == TextureImporterCompression.CompressedHQ &&
                !importer.crunchedCompression &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                importer.maxTextureSize == expectedMaxSize;
            bool standaloneCorrect = standalone.overridden &&
                                      standalone.maxTextureSize == expectedMaxSize &&
                                      standalone.format == TextureImporterFormat.BC7;
            bool androidCorrect = android.overridden &&
                                  android.maxTextureSize == expectedMaxSize &&
                                  android.format == TextureImporterFormat.ASTC_6x6;
            bool iosCorrect = ios.overridden &&
                              ios.maxTextureSize == expectedMaxSize &&
                              ios.format == TextureImporterFormat.ASTC_6x6;
            if (importerCorrect && standaloneCorrect && androidCorrect && iosCorrect)
                return true;

            failure = "importerCorrect=" + (importerCorrect ? "true" : "false") +
                      " standaloneCorrect=" + (standaloneCorrect ? "true" : "false") +
                      " androidCorrect=" + (androidCorrect ? "true" : "false") +
                      " iPhoneCorrect=" + (iosCorrect ? "true" : "false");
            return false;
        }

        private static bool TryCaptureDefaultVisorMaterialSnapshot(out VisorMaterialBindingSnapshot snapshot, out string failure)
        {
            snapshot = default;
            failure = string.Empty;
            Material visorMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultVisorMaterialPath);
            if (visorMaterial == null)
            {
                failure = "default visor material missing: " + DefaultVisorMaterialPath;
                return false;
            }

            if (!visorMaterial.HasProperty(s_visorMaskTexId) ||
                !visorMaterial.HasProperty(s_visorMaskStrengthsId) ||
                !visorMaterial.HasProperty(s_visorMaskUvShiftId) ||
                !visorMaterial.HasProperty(s_visorCondensationFlipbookId))
            {
                failure = "default visor material is not using the packed visor mask shader contract";
                return false;
            }

            snapshot = new VisorMaterialBindingSnapshot(
                visorMaterial,
                visorMaterial.GetTexture(s_visorMaskTexId),
                visorMaterial.GetVector(s_visorMaskStrengthsId),
                visorMaterial.GetVector(s_visorMaskUvShiftId),
                visorMaterial.GetVector(s_visorCondensationFlipbookId));
            return true;
        }

        private static bool TryApplyMaskToDefaultVisorMaterial(in VisorMaterialBindingSnapshot snapshot, string texturePath, out string failure)
        {
            failure = string.Empty;
            if (!snapshot.Captured || snapshot.Material == null)
            {
                failure = "default visor material snapshot missing";
                return false;
            }

            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (mask == null)
            {
                failure = "imported visor mask missing for material bind: " + texturePath;
                return false;
            }

            Undo.RecordObject(snapshot.Material, "Apply visor mask bake 1726");
            snapshot.Material.SetTexture(s_visorMaskTexId, mask);
            snapshot.Material.SetVector(s_visorMaskStrengthsId, Vector4.one);
            snapshot.Material.SetVector(s_visorMaskUvShiftId, Vector4.zero);
            snapshot.Material.SetVector(s_visorCondensationFlipbookId, new Vector4(FlipbookGrid, FlipbookGrid, -1f, 0f));
            EditorUtility.SetDirty(snapshot.Material);
            return true;
        }

        private static void RestoreVisorMaterialBinding(in VisorMaterialBindingSnapshot snapshot)
        {
            if (!snapshot.Captured || snapshot.Material == null)
                return;

            snapshot.Material.SetTexture(s_visorMaskTexId, snapshot.Mask);
            snapshot.Material.SetVector(s_visorMaskStrengthsId, snapshot.Strengths);
            snapshot.Material.SetVector(s_visorMaskUvShiftId, snapshot.UvShift);
            snapshot.Material.SetVector(s_visorCondensationFlipbookId, snapshot.CondensationFlipbook);
            EditorUtility.SetDirty(snapshot.Material);
        }

        private readonly struct VisorMaterialBindingSnapshot
        {
            public readonly bool Captured;
            public readonly Material Material;
            public readonly Texture Mask;
            public readonly Vector4 Strengths;
            public readonly Vector4 UvShift;
            public readonly Vector4 CondensationFlipbook;

            public VisorMaterialBindingSnapshot(Material material, Texture mask, Vector4 strengths, Vector4 uvShift, Vector4 condensationFlipbook)
            {
                Captured = material != null;
                Material = material;
                Mask = mask;
                Strengths = strengths;
                UvShift = uvShift;
                CondensationFlipbook = condensationFlipbook;
            }
        }

        private static int Align(int value, int alignment)
        {
            int safeAlignment = Mathf.Max(1, alignment);
            return ((Mathf.Max(1, value) + safeAlignment - 1) / safeAlignment) * safeAlignment;
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return float.IsFinite(value) ? value : fallback;
        }

        private static void TrackParamsBuffer(GraphicsBuffer buffer)
        {
            if (s_activeParamsBuffer != null && !ReferenceEquals(s_activeParamsBuffer, buffer))
                s_activeParamsBuffer.Release();

            s_activeParamsBuffer = buffer;
        }

        private static void TrackRenderTexture(RenderTexture rt)
        {
            if (s_activeRenderTexture != null && !ReferenceEquals(s_activeRenderTexture, rt))
            {
                s_activeRenderTexture.Release();
                DestroyImmediateSafe(s_activeRenderTexture);
            }

            s_activeRenderTexture = rt;
        }

        private static void ReleaseTrackedGpuState()
        {
            if (s_activeParamsBuffer != null)
            {
                s_activeParamsBuffer.Release();
                s_activeParamsBuffer = null;
            }

            if (s_activeRenderTexture != null)
            {
                s_activeRenderTexture.Release();
                DestroyImmediateSafe(s_activeRenderTexture);
                s_activeRenderTexture = null;
            }
        }

        private static void DestroyImmediateSafe(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            DestroyImmediate(obj);
        }

    }
}
#endif
