using System;
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
    /// Cheap emission-threshold screen-space bounce used to bleed bioluminescent color onto nearby opaque surfaces.
    /// </summary>
    public sealed class HectonBiolumSSGIFeature : ScriptableRendererFeature
    {
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_BiolumSSGI.compute";

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Compute shader used to resolve cheap emission-threshold SSGI.")]
            public ComputeShader computeShader = null;

            [Tooltip("Fullscreen composite shader used to add the resolved SSGI back into camera color.")]
            public Shader compositeShader = null;

            [Tooltip("Where the biolum SSGI pass is injected into URP.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Internal resolution scale for the SSGI solve.")]
            [Range(0.25f, 1f)] public float renderScale = 0.25f;

            [Tooltip("Brightness threshold above which screen pixels are treated as emissive GI sources.")]
            [Range(0f, 4f)] public float emissionThreshold = 1.05f;

            [Tooltip("Final bounce intensity added back onto nearby surfaces.")]
            [Range(0f, 2f)] public float intensity = 0.22f;

            [Tooltip("Radius in source pixels used for the neighbor gather.")]
            [Range(1f, 12f)] public float radius = 4f;

            [Tooltip("How aggressively depth differences reject bounce bleeding across hard edges.")]
            [Range(1f, 256f)] public float depthSigma = 54f;

            [Tooltip("Neighbor count used during the low-res gather.")]
            [Range(1, 8)] public int sampleCount = 6;
        }

        private sealed class BiolumSsgiPass : ScriptableRenderPass, IDisposable
        {
            private const int RenderTextureBucketSize = 64;

            private sealed class ComputePassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal uint threadGroupSizeX;
                internal uint threadGroupSizeY;
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle gather;
                internal TextureHandle result;
                internal Vector4 inputSize;
                internal Vector4 outputSize;
                internal float threshold;
                internal float intensity;
                internal float radius;
                internal float depthSigma;
                internal int sampleCount;
            }

            private sealed class CompositePassData
            {
                internal TextureHandle source;
                internal TextureHandle giTexture;
                internal TextureHandle destination;
                internal Material compositeMaterial;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Biolum SSGI");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private Material _compositeMaterial;
            private RTHandle _gatherTexture;
            private RTHandle _giTexture;
            private int _gatherKernelIndex = -1;
            private int _kernelIndex = -1;
            private uint _gatherThreadGroupSizeX = 8;
            private uint _gatherThreadGroupSizeY = 8;
            private uint _threadGroupSizeX = 8;
            private uint _threadGroupSizeY = 8;

            public BiolumSsgiPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, ComputeShader computeShader, Material compositeMaterial)
            {
                _settings = settings;
                _computeShader = computeShader;
                _compositeMaterial = compositeMaterial;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;

                if (_computeShader != null && _kernelIndex < 0)
                {
                    _gatherKernelIndex = _computeShader.FindKernel("GatherBiolumSSGI");
                    _kernelIndex = _computeShader.FindKernel("ResolveBiolumSSGI");
                    _computeShader.GetKernelThreadGroupSizes(_gatherKernelIndex, out _gatherThreadGroupSizeX, out _gatherThreadGroupSizeY, out _);
                    _computeShader.GetKernelThreadGroupSizes(_kernelIndex, out _threadGroupSizeX, out _threadGroupSizeY, out _);
                }
            }

            public void Dispose()
            {
                _gatherTexture?.Release();
                _giTexture?.Release();
                _gatherTexture = null;
                _giTexture = null;
                _gatherKernelIndex = -1;
                _kernelIndex = -1;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _computeShader == null || _compositeMaterial == null || _kernelIndex < 0 || _gatherKernelIndex < 0)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                int giWidth = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * Mathf.Clamp(_settings.renderScale, 0.25f, 1f))));
                int giHeight = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * Mathf.Clamp(_settings.renderScale, 0.25f, 1f))));
                EnsureGiTexture(giWidth, giHeight);

                TextureHandle gatherTexture = renderGraph.ImportTexture(_gatherTexture);
                TextureHandle giTexture = renderGraph.ImportTexture(_giTexture);

                using (var builder = renderGraph.AddComputePass("Hecton Biolum SSGI Gather", out ComputePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _gatherKernelIndex;
                    passData.threadGroupSizeX = _gatherThreadGroupSizeX;
                    passData.threadGroupSizeY = _gatherThreadGroupSizeY;
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.gather = gatherTexture;
                    passData.result = gatherTexture;
                    passData.inputSize = new Vector4(sourceDesc.width, sourceDesc.height, 1f / Mathf.Max(1, sourceDesc.width), 1f / Mathf.Max(1, sourceDesc.height));
                    passData.outputSize = new Vector4(giWidth, giHeight, 1f / Mathf.Max(1, giWidth), 1f / Mathf.Max(1, giHeight));
                    passData.threshold = Mathf.Max(0f, _settings.emissionThreshold);
                    passData.intensity = Mathf.Max(0f, _settings.intensity);
                    passData.radius = Mathf.Max(1f, _settings.radius);
                    passData.depthSigma = Mathf.Max(0.01f, _settings.depthSigma);
                    passData.sampleCount = Mathf.Clamp(_settings.sampleCount, 1, 8);

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(gatherTexture, AccessFlags.Write);

                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
                        int dispatchX = Mathf.CeilToInt(data.outputSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                        int dispatchY = Mathf.CeilToInt(data.outputSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceColorId, data.source);
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.GatherId, data.gather);
                        cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.InputSizeId, data.inputSize);
                        cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.OutputSizeId, data.outputSize);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.ThresholdId, data.threshold);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.IntensityId, data.intensity);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.RadiusId, data.radius);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.DepthSigmaId, data.depthSigma);
                        cmd.SetComputeIntParam(data.computeShader, ShaderConstants.SampleCountId, data.sampleCount);
                        cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                    });
                }

                using (var builder = renderGraph.AddComputePass("Hecton Biolum SSGI Resolve", out ComputePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _kernelIndex;
                    passData.threadGroupSizeX = _threadGroupSizeX;
                    passData.threadGroupSizeY = _threadGroupSizeY;
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.gather = gatherTexture;
                    passData.result = giTexture;
                    passData.inputSize = new Vector4(sourceDesc.width, sourceDesc.height, 1f / Mathf.Max(1, sourceDesc.width), 1f / Mathf.Max(1, sourceDesc.height));
                    passData.outputSize = new Vector4(giWidth, giHeight, 1f / Mathf.Max(1, giWidth), 1f / Mathf.Max(1, giHeight));
                    passData.threshold = Mathf.Max(0f, _settings.emissionThreshold);
                    passData.intensity = Mathf.Max(0f, _settings.intensity);
                    passData.radius = Mathf.Max(1f, _settings.radius);
                    passData.depthSigma = Mathf.Max(0.01f, _settings.depthSigma);
                    passData.sampleCount = Mathf.Clamp(_settings.sampleCount, 1, 8);

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(gatherTexture, AccessFlags.Read);
                    builder.UseTexture(giTexture, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(giTexture, ShaderConstants.GlobalGiTextureId);

                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
                        int dispatchX = Mathf.CeilToInt(data.outputSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                        int dispatchY = Mathf.CeilToInt(data.outputSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceColorId, data.source);
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.GatherInputId, data.gather);
                        cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.ResultId, data.result);
                        cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.InputSizeId, data.inputSize);
                        cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.OutputSizeId, data.outputSize);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.ThresholdId, data.threshold);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.IntensityId, data.intensity);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.RadiusId, data.radius);
                        cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.DepthSigmaId, data.depthSigma);
                        cmd.SetComputeIntParam(data.computeShader, ShaderConstants.SampleCountId, data.sampleCount);
                        cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                    });
                }

                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonBiolumSSGIComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                using (var builder = renderGraph.AddUnsafePass<CompositePassData>("Hecton Biolum SSGI Composite", out CompositePassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.giTexture = giTexture;
                    passData.destination = compositeTexture;
                    passData.compositeMaterial = _compositeMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(giTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);

                    builder.SetRenderFunc(static (CompositePassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;
                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.compositeMaterial, 0);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private void EnsureGiTexture(int width, int height)
            {
                if (_gatherTexture != null &&
                    _gatherTexture.rt != null &&
                    _gatherTexture.rt.width == width &&
                    _gatherTexture.rt.height == height &&
                    _giTexture != null &&
                    _giTexture.rt != null &&
                    _giTexture.rt.width == width &&
                    _giTexture.rt.height == height)
                {
                    return;
                }

                _gatherTexture?.Release();
                _giTexture?.Release();
                _gatherTexture = RTHandles.Alloc(
                    width,
                    height,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    name: "_HectonBiolumSSGIGather");
                _giTexture = RTHandles.Alloc(
                    width,
                    height,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R16G16B16A16_SFloat,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    true,
                    name: "_HectonBiolumSSGITexture");
            }

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = Mathf.Max(1, dimension);
                return ((safeDimension + RenderTextureBucketSize - 1) / RenderTextureBucketSize) * RenderTextureBucketSize;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int SourceColorId = Shader.PropertyToID("_HectonSourceColor");
            internal static readonly int SourceDepthId = Shader.PropertyToID("_HectonSourceDepth");
            internal static readonly int GatherId = Shader.PropertyToID("_HectonBiolumSSGIGather");
            internal static readonly int GatherInputId = Shader.PropertyToID("_HectonBiolumSSGIGatherInput");
            internal static readonly int ResultId = Shader.PropertyToID("_HectonBiolumSSGIResult");
            internal static readonly int InputSizeId = Shader.PropertyToID("_HectonSSGIInputSize");
            internal static readonly int OutputSizeId = Shader.PropertyToID("_HectonSSGIOutputSize");
            internal static readonly int ThresholdId = Shader.PropertyToID("_HectonSSGIThreshold");
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonSSGIIntensity");
            internal static readonly int RadiusId = Shader.PropertyToID("_HectonSSGIRadius");
            internal static readonly int DepthSigmaId = Shader.PropertyToID("_HectonSSGIDepthSigma");
            internal static readonly int SampleCountId = Shader.PropertyToID("_HectonSSGISampleCount");
            internal static readonly int GlobalGiTextureId = Shader.PropertyToID("_HectonBiolumSSGITexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private BiolumSsgiPass _pass;
        private Material _compositeMaterial;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
#endif

            Shader compositeShader = settings != null && settings.compositeShader != null
                ? settings.compositeShader
                : Shader.Find("Hidden/Hecton8/BiolumSSGIComposite");

            if (_pass == null)
                _pass = new BiolumSsgiPass();

            RecreateMaterial(ref _compositeMaterial, compositeShader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.computeShader == null || _pass == null || _compositeMaterial == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            _pass.Setup(settings, settings.computeShader, _compositeMaterial);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_compositeMaterial);
            _compositeMaterial = null;
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                CoreUtils.Destroy(material);
                material = null;
                return;
            }

            if (material != null && material.shader == shader)
                return;

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }
    }
}
