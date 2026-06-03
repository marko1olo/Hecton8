using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Opt-in reflection sheen cheat. It writes a half-res R8 mask, then composites one cheap color offset.
    /// </summary>
    public sealed class HectonStochasticSsrFeature : ScriptableRendererFeature
    {
        private const int StochasticSsrGlobalsStrideBytes = 48;

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_StochasticSSR.shader";
#endif

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for deterministic reflection sheen.")]
            public Shader shader = null;

            [Tooltip("Injection point. Runs after opaques and before fogged transparents.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Composite intensity. Default is zero: this pass is opt-in because real SSR is over budget on MX350.")]
            [Range(0f, 1f)] public float intensity = 0f;

            [Tooltip("Maximum screen-space color offset in pixels.")]
            [Range(0.25f, 4f)] public float maxPixelOffset = 1.25f;

            [Tooltip("Eye-space depth where the reflection sheen fades out.")]
            [Range(5f, 120f)] public float depthFadeMeters = 55f;

            [Tooltip("Screen-edge fade multiplier.")]
            [Range(1f, 96f)] public float edgeFade = 36f;

            [Tooltip("Static IGN modulation range. Zero is flat; one is visibly dithered.")]
            [Range(0f, 1f)] public float noiseModulation = 0.35f;
        }

        private sealed class ReflectionSheenPass : ScriptableRenderPass
        {
            private sealed class ReflectionSheenPassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
                public TextureHandle Mask;
                public BufferHandle ConstantsBuffer;
                public Material Material;
                public int ShaderPassIndex;
                public bool BindDepth;
                public bool BindMask;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Reflection Sheen");
            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _stochasticSsrGlobalsBuffer;
            private GraphicsBuffer _stochasticSsrGlobalsBufferA;
            private GraphicsBuffer _stochasticSsrGlobalsBufferB;
            private StochasticSsrGlobalsDTO _lastStochasticSsrGlobals;
            private int _stochasticSsrGlobalsWriteIndex;
            private bool _hasStochasticSsrGlobals;
            private bool _supportsSetConstantBuffer;

            public ReflectionSheenPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public bool PrepareResources()
            {
                return EnsureStochasticSsrGlobalsBuffer();
            }

            public void SetGraphicsCapabilitiesCold(bool supportsSetConstantBuffer)
            {
                _supportsSetConstantBuffer = supportsSetConstantBuffer;
                if (!_supportsSetConstantBuffer)
                    Dispose();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner() || _settings == null || _material == null || _settings.intensity <= 0.0001f)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                if (!UpdateStochasticSsrGlobals(_settings, sourceDesc.width, sourceDesc.height))
                    return;

                TextureDesc maskDesc = sourceDesc;
                maskDesc.name = "_HectonStochasticSsrMask";
                float maskScale = ResolveMaskScale01(FrameTimeWatchdog.CurrentVisualQualityWeight01);
                maskDesc.width = math.max(1, (int)math.round(sourceDesc.width * maskScale));
                maskDesc.height = math.max(1, (int)math.round(sourceDesc.height * maskScale));
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.black;
                maskDesc.depthBufferBits = DepthBits.None;
                maskDesc.msaaSamples = MSAASamples.None;
                maskDesc.colorFormat = GraphicsFormat.R8_UNorm;
                maskDesc.filterMode = FilterMode.Bilinear;
                maskDesc.useMipMap = false;
                maskDesc.autoGenerateMips = false;

                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonReflectionSheenComposite";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;

                TextureHandle maskTexture = renderGraph.CreateTexture(maskDesc);
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle globalsBuffer = renderGraph.ImportBuffer(_stochasticSsrGlobalsBuffer);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Reflection Sheen Mask R8 Half",
                    sourceTexture,
                    depthTexture,
                    default,
                    maskTexture,
                    globalsBuffer,
                    _material,
                    0,
                    true,
                    false);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Reflection Sheen Composite",
                    sourceTexture,
                    default,
                    maskTexture,
                    destinationTexture,
                    globalsBuffer,
                    _material,
                    1,
                    false,
                    true);

                resourceData.cameraColor = destinationTexture;
            }

            private void RecordFullscreenPass(
                RenderGraph renderGraph,
                string passName,
                TextureHandle source,
                TextureHandle depth,
                TextureHandle mask,
                TextureHandle destination,
                BufferHandle globalsBuffer,
                Material material,
                int shaderPassIndex,
                bool bindDepth,
                bool bindMask)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ReflectionSheenPassData>(
                           passName,
                           out ReflectionSheenPassData passData,
                           _profilingSampler))
                {
                    passData.Source = source;
                    passData.Depth = depth;
                    passData.Mask = mask;
                    passData.ConstantsBuffer = globalsBuffer;
                    passData.Material = material;
                    passData.ShaderPassIndex = shaderPassIndex;
                    passData.BindDepth = bindDepth;
                    passData.BindMask = bindMask;

                    builder.UseTexture(source, AccessFlags.Read);
                    if (bindDepth)
                        builder.UseTexture(depth, AccessFlags.Read);
                    if (bindMask)
                        builder.UseTexture(mask, AccessFlags.Read);
                    builder.UseBuffer(globalsBuffer, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (ReflectionSheenPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null)
                            return;

                        GraphicsBuffer constants = data.ConstantsBuffer;
                        if (constants == null || !constants.IsValid())
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        if (data.BindDepth)
                            context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        if (data.BindMask)
                            context.cmd.SetGlobalTexture(ShaderConstants.MaskTextureId, data.Mask);
                        context.cmd.SetGlobalConstantBuffer(
                            constants,
                            ShaderConstants.StochasticSsrGlobalsBufferId,
                            0,
                            StochasticSsrGlobalsStrideBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, data.ShaderPassIndex);
                    });
                }
            }

            private static float ResolveMaskScale01(float visualQualityWeight01)
            {
                float safeQuality01 = math.saturate(math.select(1f, visualQualityWeight01, math.isfinite(visualQualityWeight01)));
                return math.lerp(0.25f, 0.5f, SmoothStep01(safeQuality01));
            }

            private static float SmoothStep01(float value)
            {
                float t = math.saturate(value);
                return t * t * (3f - 2f * t);
            }

            public void Dispose()
            {
                _stochasticSsrGlobalsBufferA?.Release();
                _stochasticSsrGlobalsBufferB?.Release();
                _stochasticSsrGlobalsBufferA = null;
                _stochasticSsrGlobalsBufferB = null;
                _stochasticSsrGlobalsBuffer = null;
                _stochasticSsrGlobalsWriteIndex = 0;
                _hasStochasticSsrGlobals = false;
            }

            private bool EnsureStochasticSsrGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (_stochasticSsrGlobalsBufferA != null && _stochasticSsrGlobalsBufferA.IsValid() &&
                    _stochasticSsrGlobalsBufferB != null && _stochasticSsrGlobalsBufferB.IsValid())
                {
                    if (_stochasticSsrGlobalsBuffer == null)
                        _stochasticSsrGlobalsBuffer = _stochasticSsrGlobalsBufferA;
                    return true;
                }

                _stochasticSsrGlobalsBufferA?.Release();
                _stochasticSsrGlobalsBufferB?.Release();
                try
                {
                    _stochasticSsrGlobalsBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        StochasticSsrGlobalsStrideBytes);
                    _stochasticSsrGlobalsBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        StochasticSsrGlobalsStrideBytes);
                }
                catch (ArgumentException)
                {
                    Dispose();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    Dispose();
                    return false;
                }
                catch (NotSupportedException)
                {
                    Dispose();
                    return false;
                }
                catch (OutOfMemoryException)
                {
                    Dispose();
                    return false;
                }
                _stochasticSsrGlobalsBuffer = _stochasticSsrGlobalsBufferA;
                _stochasticSsrGlobalsWriteIndex = 1;
                _hasStochasticSsrGlobals = false;
                return _stochasticSsrGlobalsBufferA.IsValid() && _stochasticSsrGlobalsBufferB.IsValid();
            }

            private bool UpdateStochasticSsrGlobals(FeatureSettings settings, int inputWidth, int inputHeight)
            {
                if (!HasStochasticSsrGlobalsBuffer())
                    return false;

                Vector4 inputSize = new Vector4(inputWidth, inputHeight, 1f / math.max(1, inputWidth), 1f / math.max(1, inputHeight));
                Vector4 paramsA = new Vector4(
                    math.max(settings.maxPixelOffset, 0.25f),
                    math.max(settings.depthFadeMeters, 1f),
                    math.saturate(settings.intensity),
                    math.max(settings.edgeFade, 1f));
                Vector4 paramsB = new Vector4(math.saturate(settings.noiseModulation), 0f, 0f, 0f);

                StochasticSsrGlobalsDTO globals = new StochasticSsrGlobalsDTO(inputSize, paramsA, paramsB);
                if (_hasStochasticSsrGlobals && StochasticSsrGlobalsEqual(in _lastStochasticSsrGlobals, in globals))
                {
                    return _stochasticSsrGlobalsBuffer != null && _stochasticSsrGlobalsBuffer.IsValid();
                }

                GraphicsBuffer writeBuffer = (_stochasticSsrGlobalsWriteIndex & 1) == 0 ? _stochasticSsrGlobalsBufferA : _stochasticSsrGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                try
                {
                    NativeArray<StochasticSsrGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<StochasticSsrGlobalsDTO>(0, 1);
                    try
                    {
                        mapped[0] = globals;
                    }
                    finally
                    {
                        writeBuffer.UnlockBufferAfterWrite<StochasticSsrGlobalsDTO>(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    MarkStochasticSsrGlobalsUnavailable();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    MarkStochasticSsrGlobalsUnavailable();
                    return false;
                }
                catch (ArgumentException)
                {
                    MarkStochasticSsrGlobalsUnavailable();
                    return false;
                }
                catch (NotSupportedException)
                {
                    MarkStochasticSsrGlobalsUnavailable();
                    return false;
                }
                _stochasticSsrGlobalsBuffer = writeBuffer;
                _stochasticSsrGlobalsWriteIndex ^= 1;
                _lastStochasticSsrGlobals = globals;
                _hasStochasticSsrGlobals = true;
                return _stochasticSsrGlobalsBuffer != null && _stochasticSsrGlobalsBuffer.IsValid();
            }

            private bool HasStochasticSsrGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                    return false;

                if (_stochasticSsrGlobalsBufferA == null || !_stochasticSsrGlobalsBufferA.IsValid() ||
                    _stochasticSsrGlobalsBufferB == null || !_stochasticSsrGlobalsBufferB.IsValid())
                {
                    return false;
                }

                if (_stochasticSsrGlobalsBuffer == null || !_stochasticSsrGlobalsBuffer.IsValid())
                    _stochasticSsrGlobalsBuffer = _stochasticSsrGlobalsBufferA;
                return true;
            }

            private void MarkStochasticSsrGlobalsUnavailable()
            {
                _stochasticSsrGlobalsBuffer = null;
                _hasStochasticSsrGlobals = false;
            }

            private static bool StochasticSsrGlobalsEqual(in StochasticSsrGlobalsDTO left, in StochasticSsrGlobalsDTO right)
            {
                return left.InputSize == right.InputSize &&
                       left.ParamsA == right.ParamsA &&
                       left.ParamsB == right.ParamsB;
            }

            [StructLayout(LayoutKind.Explicit, Size = StochasticSsrGlobalsStrideBytes)]
            private struct StochasticSsrGlobalsDTO
            {
                [FieldOffset(0)]
                public Vector4 InputSize;

                [FieldOffset(16)]
                public Vector4 ParamsA;

                [FieldOffset(32)]
                public Vector4 ParamsB;

                public StochasticSsrGlobalsDTO(Vector4 inputSize, Vector4 paramsA, Vector4 paramsB)
                {
                    InputSize = inputSize;
                    ParamsA = paramsA;
                    ParamsB = paramsB;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int StochasticSsrGlobalsBufferId = Shader.PropertyToID("HectonStochasticSsrGlobals");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int MaskTextureId = Shader.PropertyToID("_HectonSsrMaskTex");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ReflectionSheenPass _pass;
        private Material _material;
        private bool _supportsSetConstantBuffer;

        public override void Create()
        {
            HectonDrsRenderFeatureGate.PrimeCold();

#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
                RuntimeShaderReferenceCatalog.TryGetStochasticSsrShader(out shader);
            RecreateMaterial(ref _material, shader);
            _pass ??= new ReflectionSheenPass();
            CacheGraphicsCapabilitiesCold();
            if (!Application.isPlaying)
                _pass.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner())
                return;

            if (settings == null || _pass == null || _material == null || settings.intensity <= 0.0001f)
                return;

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
                return;

            if (!_pass.PrepareResources())
                return;

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            DisposeMaterial(ref _material);
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
            _pass?.SetGraphicsCapabilitiesCold(_supportsSetConstantBuffer);
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                DisposeMaterial(ref material);
                return;
            }

            if (material != null && material.shader == shader)
                return;

            DisposeMaterial(ref material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        private static void DisposeMaterial(ref Material material)
        {
            if (material == null)
                return;

            CoreUtils.Destroy(material);
            material = null;
        }
    }
}
