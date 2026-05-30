using System;
using Hecton8.Core;
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
#if UNITY_EDITOR
        private const string CompositeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_BiolumSSGIComposite.shader";
#endif
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

            internal float ResolveRenderScale()
            {
                float authoredScale = Mathf.Clamp(renderScale, 0.25f, 1f);
                float qualityCurve = SmoothStep01(ResolveGlobalQualityWeight01());
                float lowScale = Mathf.Min(authoredScale, 0.25f);
                return Mathf.Clamp(Mathf.Lerp(lowScale, authoredScale, qualityCurve), 0.25f, 1f);
            }

            internal int ResolveSampleCount()
            {
                int authoredSamples = Mathf.Clamp(sampleCount, 1, 8);
                float qualityCurve = SmoothStep01(ResolveGlobalQualityWeight01());
                return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, authoredSamples, qualityCurve)), 1, authoredSamples);
            }

            internal float ResolveIntensity()
            {
                float authoredIntensity = Mathf.Max(0f, intensity);
                float qualityCurve = SmoothStep01(ResolveGlobalQualityWeight01());
                return authoredIntensity * Mathf.Lerp(0.55f, 1f, qualityCurve);
            }

            private static float ResolveGlobalQualityWeight01()
            {
                float quality = HomeostasisBrain.GlobalQualityWeight;
                return float.IsNaN(quality) || float.IsInfinity(quality) ? 1f : Mathf.Clamp01(quality);
            }

            private static float SmoothStep01(float value)
            {
                float t = Mathf.Clamp01(value);
                return t * t * (3f - 2f * t);
            }
        }

        private sealed class BiolumSsgiPass : ScriptableRenderPass, IDisposable
        {
            private const int RenderTextureBucketSize = 64;

            private sealed class ComputePassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal int dispatchX;
                internal int dispatchY;
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
                internal Material material;
                internal TextureHandle source;
                internal TextureHandle gi;
                internal int shaderPassIndex;
            }

            private sealed class ProxyPassData
            {
                internal Material material;
                internal TextureHandle source;
                internal TextureHandle depth;
                internal Vector4 inputSize;
                internal float threshold;
                internal float intensity;
                internal float radius;
                internal float depthSigma;
                internal int sampleCount;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Biolum SSGI");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private ComputeShader _resolvedComputeShader;
            private Material _compositeMaterial;
            private int _gatherKernelIndex = -1;
            private int _kernelIndex = -1;
            private uint _gatherThreadGroupSizeX;
            private uint _gatherThreadGroupSizeY;
            private uint _threadGroupSizeX;
            private uint _threadGroupSizeY;
            private bool _forceProxyOnly;
            private const uint MaxKernelThreadProduct = 256u;
            private const int MaxDispatchGroupsPerDimension = 65535;

            public BiolumSsgiPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, ComputeShader computeShader, Material compositeMaterial, bool forceProxyOnly)
            {
                _settings = settings;
                _computeShader = forceProxyOnly ? null : computeShader;
                _compositeMaterial = compositeMaterial;
                _forceProxyOnly = forceProxyOnly;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;

                if (!ReferenceEquals(_resolvedComputeShader, _computeShader))
                    ClearKernelState();

                if (_computeShader != null && _kernelIndex < 0)
                {
                    if (!TryResolveKernel(_computeShader, "GatherBiolumSSGI", out _gatherKernelIndex, out _gatherThreadGroupSizeX, out _gatherThreadGroupSizeY) ||
                        !TryResolveKernel(_computeShader, "ResolveBiolumSSGI", out _kernelIndex, out _threadGroupSizeX, out _threadGroupSizeY))
                    {
                        ClearKernelState();
                    }
                    else
                    {
                        _resolvedComputeShader = _computeShader;
                    }
                }
            }

            public void Dispose()
            {
                ClearKernelState();
            }

            private void ClearKernelState()
            {
                _resolvedComputeShader = null;
                _gatherKernelIndex = -1;
                _kernelIndex = -1;
                _gatherThreadGroupSizeX = 0u;
                _gatherThreadGroupSizeY = 0u;
                _threadGroupSizeX = 0u;
                _threadGroupSizeY = 0u;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _compositeMaterial == null)
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
                int sampleCount = _settings.ResolveSampleCount();
                float intensity = _settings.ResolveIntensity();
                bool computeReady = !_forceProxyOnly && _computeShader != null && _kernelIndex >= 0 && _gatherKernelIndex >= 0;
                if (!computeReady)
                {
                    RecordProxyComposite(
                        renderGraph,
                        resourceData,
                        sourceTexture,
                        depthTexture,
                        sourceDesc,
                        sampleCount,
                        intensity);
                    return;
                }

                float renderScale = _settings.ResolveRenderScale();
                int giWidth = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * renderScale)));
                int giHeight = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * renderScale)));
                int gatherDispatchX = ResolveDispatchGroups(giWidth, _gatherThreadGroupSizeX);
                int gatherDispatchY = ResolveDispatchGroups(giHeight, _gatherThreadGroupSizeY);
                int resolveDispatchX = ResolveDispatchGroups(giWidth, _threadGroupSizeX);
                int resolveDispatchY = ResolveDispatchGroups(giHeight, _threadGroupSizeY);
                if (gatherDispatchX <= 0 || gatherDispatchY <= 0 || resolveDispatchX <= 0 || resolveDispatchY <= 0)
                    return;

                TextureDesc gatherDesc = CreateGraphTextureDesc(
                    sourceDesc,
                    giWidth,
                    giHeight,
                    "_HectonBiolumSSGIGather",
                    GraphicsFormat.R16G16B16A16_SFloat,
                    true,
                    FilterMode.Bilinear);
                TextureDesc giDesc = CreateGraphTextureDesc(
                    sourceDesc,
                    giWidth,
                    giHeight,
                    "_HectonBiolumSSGITexture",
                    GraphicsFormat.R16G16B16A16_SFloat,
                    true,
                    FilterMode.Bilinear);
                TextureHandle gatherTexture = renderGraph.CreateTexture(gatherDesc);
                TextureHandle giTexture = renderGraph.CreateTexture(giDesc);

                using (var builder = renderGraph.AddComputePass("Hecton Biolum SSGI Gather", out ComputePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _gatherKernelIndex;
                    passData.dispatchX = gatherDispatchX;
                    passData.dispatchY = gatherDispatchY;
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.gather = gatherTexture;
                    passData.result = gatherTexture;
                    passData.inputSize = new Vector4(sourceDesc.width, sourceDesc.height, 1f / Mathf.Max(1, sourceDesc.width), 1f / Mathf.Max(1, sourceDesc.height));
                    passData.outputSize = new Vector4(giWidth, giHeight, 1f / Mathf.Max(1, giWidth), 1f / Mathf.Max(1, giHeight));
                    passData.threshold = Mathf.Max(0f, _settings.emissionThreshold);
                    passData.intensity = intensity;
                    passData.radius = Mathf.Max(1f, _settings.radius);
                    passData.depthSigma = Mathf.Max(0.01f, _settings.depthSigma);
                    passData.sampleCount = sampleCount;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(gatherTexture, AccessFlags.Write);

                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
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
                        cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, 1);
                    });
                }

                using (var builder = renderGraph.AddComputePass("Hecton Biolum SSGI Resolve", out ComputePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _kernelIndex;
                    passData.dispatchX = resolveDispatchX;
                    passData.dispatchY = resolveDispatchY;
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.gather = gatherTexture;
                    passData.result = giTexture;
                    passData.inputSize = new Vector4(sourceDesc.width, sourceDesc.height, 1f / Mathf.Max(1, sourceDesc.width), 1f / Mathf.Max(1, sourceDesc.height));
                    passData.outputSize = new Vector4(giWidth, giHeight, 1f / Mathf.Max(1, giWidth), 1f / Mathf.Max(1, giHeight));
                    passData.threshold = Mathf.Max(0f, _settings.emissionThreshold);
                    passData.intensity = intensity;
                    passData.radius = Mathf.Max(1f, _settings.radius);
                    passData.depthSigma = Mathf.Max(0.01f, _settings.depthSigma);
                    passData.sampleCount = sampleCount;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(gatherTexture, AccessFlags.Read);
                    builder.UseTexture(giTexture, AccessFlags.Write);

                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
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
                        cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, 1);
                    });
                }

                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonBiolumSSGIComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "Hecton Biolum SSGI Composite",
                           out CompositePassData passData,
                           _profilingSampler))
                {
                    passData.material = _compositeMaterial;
                    passData.source = sourceTexture;
                    passData.gi = giTexture;
                    passData.shaderPassIndex = 0;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(giTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(compositeTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                    {
                        if (data.material == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.source);
                        context.cmd.SetGlobalTexture(ShaderConstants.GiTextureId, data.gi);
                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.shaderPassIndex);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private void RecordProxyComposite(
                RenderGraph renderGraph,
                UniversalResourceData resourceData,
                TextureHandle sourceTexture,
                TextureHandle depthTexture,
                in TextureDesc sourceDesc,
                int sampleCount,
                float intensity)
            {
                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonBiolumSSGIProxyComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ProxyPassData>(
                           "Hecton Biolum SSGI Proxy Composite",
                           out ProxyPassData passData,
                           _profilingSampler))
                {
                    passData.material = _compositeMaterial;
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.inputSize = new Vector4(sourceDesc.width, sourceDesc.height, 1f / Mathf.Max(1, sourceDesc.width), 1f / Mathf.Max(1, sourceDesc.height));
                    passData.threshold = Mathf.Max(0f, _settings.emissionThreshold);
                    passData.intensity = intensity;
                    passData.radius = Mathf.Max(1f, _settings.radius);
                    passData.depthSigma = Mathf.Max(0.01f, _settings.depthSigma);
                    passData.sampleCount = sampleCount;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(compositeTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (ProxyPassData data, RasterGraphContext context) =>
                    {
                        if (data.material == null)
                            return;

                        var cmd = context.cmd;
                        cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.source);
                        cmd.SetGlobalTexture(ShaderConstants.SourceDepthId, data.depth);
                        cmd.SetGlobalVector(ShaderConstants.InputSizeId, data.inputSize);
                        cmd.SetGlobalFloat(ShaderConstants.ThresholdId, data.threshold);
                        cmd.SetGlobalFloat(ShaderConstants.IntensityId, data.intensity);
                        cmd.SetGlobalFloat(ShaderConstants.RadiusId, data.radius);
                        cmd.SetGlobalFloat(ShaderConstants.DepthSigmaId, data.depthSigma);
                        cmd.SetGlobalInt(ShaderConstants.SampleCountId, data.sampleCount);
                        CoreUtils.DrawFullScreen(cmd, data.material, null, 1);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private static bool TryResolveKernel(ComputeShader computeShader, string kernelName, out int kernelIndex, out uint groupSizeX, out uint groupSizeY)
            {
                kernelIndex = -1;
                groupSizeX = 0u;
                groupSizeY = 0u;
                if (computeShader == null || !computeShader.HasKernel(kernelName))
                    return false;

                int resolvedKernel = computeShader.FindKernel(kernelName);
                if (resolvedKernel < 0 || !computeShader.IsSupported(resolvedKernel))
                    return false;

                computeShader.GetKernelThreadGroupSizes(resolvedKernel, out uint x, out uint y, out uint z);
                ulong threadProduct = (ulong)x * y * z;
                if (x == 0u || y == 0u || z != 1u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return false;

                kernelIndex = resolvedKernel;
                groupSizeX = x;
                groupSizeY = y;
                return true;
            }

            private static int ResolveDispatchGroups(int value, uint groupSize)
            {
                if (value <= 0 || groupSize == 0u)
                    return 0;

                long groups = ((long)value + groupSize - 1L) / groupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
            }

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = Mathf.Max(1, dimension);
                return ((safeDimension + RenderTextureBucketSize - 1) / RenderTextureBucketSize) * RenderTextureBucketSize;
            }

            private static TextureDesc CreateGraphTextureDesc(
                in TextureDesc sourceDesc,
                int width,
                int height,
                string name,
                GraphicsFormat colorFormat,
                bool enableRandomWrite,
                FilterMode filterMode)
            {
                TextureDesc desc = new TextureDesc(Mathf.Max(1, width), Mathf.Max(1, height), false, false);
                desc.name = name;
                desc.width = Mathf.Max(1, width);
                desc.height = Mathf.Max(1, height);
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.colorFormat = colorFormat != GraphicsFormat.None ? colorFormat : sourceDesc.colorFormat;
                desc.clearBuffer = false;
                desc.dimension = TextureDimension.Tex2D;
                desc.slices = 1;
                desc.useDynamicScale = false;
                desc.useDynamicScaleExplicit = false;
                desc.enableRandomWrite = enableRandomWrite;
                desc.filterMode = filterMode;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                return desc;
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
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int GiTextureId = Shader.PropertyToID("_HectonBiolumSSGITexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private BiolumSsgiPass _pass;
        private Material _compositeMaterial;
        private bool _supportsComputeShaders;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
            if (settings != null && settings.compositeShader == null)
                settings.compositeShader = AssetDatabase.LoadAssetAtPath<Shader>(CompositeShaderAssetPath);
#endif

            Shader compositeShader = settings != null ? settings.compositeShader : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (compositeShader == null)
                compositeShader = Shader.Find("Hidden/Hecton8/BiolumSSGIComposite");
#endif

            if (_pass == null)
                _pass = new BiolumSsgiPass();

            CacheGraphicsCapabilitiesCold();
            RecreateMaterial(ref _compositeMaterial, compositeShader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null ||
                _pass == null ||
                _compositeMaterial == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            bool forceProxyOnly = settings.computeShader == null || !_supportsComputeShaders;
            _pass.Setup(settings, settings.computeShader, _compositeMaterial, forceProxyOnly);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_compositeMaterial);
            _compositeMaterial = null;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShaders = SystemInfo.supportsComputeShaders;
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
