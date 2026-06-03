using System;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Hecton8.Visor
{
    /// <summary>
    /// Lightweight directional screen-space occlusion for abyssal caves and base interiors.
    /// Runs before transparents so water/visor/sonar composites stay on top.
    /// </summary>
    public sealed class HectonAbyssalSsdoFeature : ScriptableRendererFeature
    {
        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Authored fullscreen material used for occlusion, bilateral blur, and composite.")]
            [FormerlySerializedAs("shader")]
            public Material material = null;

            [Tooltip("Where the SSDO composite is injected. Before transparents keeps water and visor overlays untouched.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Internal render scale for the occlusion target.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Depth-only occlusion radius in eye-space meters.")]
            [Range(0.25f, 3f)] public float radiusMeters = 1.5f;

            [Tooltip("Overall occlusion strength.")]
            [Range(0f, 2f)] public float intensity = 0.78f;

            [Tooltip("Bias applied to reduce self-occlusion acne.")]
            [Range(0f, 0.3f)] public float bias = 0.05f;

            [Tooltip("Depth rejection slope for the directional gather.")]
            [Range(1f, 96f)] public float depthSigma = 18f;

            [Tooltip("Hard depth rejection threshold in eye-space meters for bilateral cleanup.")]
            [Range(0.01f, 2f)] public float blurDepthThreshold = 0.18f;

            [Tooltip("Composite weight applied to the camera color.")]
            [Range(0f, 1f)] public float compositeStrength = 0.52f;

            internal float ResolveRenderScale(float survivalVisualWeight01)
            {
                float authored = math.clamp(renderScale, 0.25f, 1f);
                float qualityCurve = Smooth01(ResolveGlobalQualityWeight01()) * Smooth01(survivalVisualWeight01);
                float survivalScale = math.max(0.25f, math.min(authored, authored * 0.58f));
                return math.clamp(math.lerp(survivalScale, authored, qualityCurve), 0.25f, 1f);
            }

            internal float ResolveRadiusMeters(float survivalVisualWeight01)
            {
                float authored = math.max(0.01f, radiusMeters);
                float qualityCurve = Smooth01(ResolveGlobalQualityWeight01()) * Smooth01(survivalVisualWeight01);
                float survivalRadius = math.min(authored, 0.55f);
                return math.max(0.01f, math.lerp(survivalRadius, authored, qualityCurve));
            }

            internal float ResolveIntensity(float survivalVisualWeight01)
            {
                float qualityCurve = math.lerp(0.42f, 1f, Smooth01(ResolveGlobalQualityWeight01()));
                return math.max(0f, intensity) * math.saturate(survivalVisualWeight01) * qualityCurve;
            }

            internal float ResolveCompositeStrength(float survivalVisualWeight01)
            {
                return math.saturate(compositeStrength) * math.saturate(survivalVisualWeight01);
            }

            private static float ResolveGlobalQualityWeight01()
            {
                float quality = HomeostasisBrain.GlobalQualityWeight;
                return math.saturate(math.isfinite(quality) ? quality : 1f);
            }

            private static float Smooth01(float value)
            {
                float t = math.saturate(value);
                return t * t * (3f - 2f * t);
            }
        }

        private sealed class AbyssalSsdoPass : ScriptableRenderPass
        {
            private sealed class SsdoFullscreenPassData
            {
                public TextureHandle Source;
                public TextureHandle Depth;
                public TextureHandle Ssdo;
                public Material Material;
                public MaterialPropertyBlock PropertyBlock;
                public int ShaderPassIndex;
                public bool BindDepth;
                public bool BindSsdo;
                public Vector4 InputSize;
                public Vector4 OutputSize;
                public float PassMode;
                public float RadiusMeters;
                public float Intensity;
                public float Bias;
                public float DepthSigma;
                public float BlurDepthThreshold;
                public float ProjectionScale;
                public float CompositeStrength;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Abyssal SSDO");
            private readonly MaterialPropertyBlock _occlusionProperties = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock _blurHorizontalProperties = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock _blurVerticalProperties = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock _compositeProperties = new MaterialPropertyBlock();
            private FeatureSettings _settings;
            private Material _material;

            public AbyssalSsdoPass()
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

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner())
                    return;

                if (_settings == null ||
                    _material == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (IsUnsupportedCameraType(cameraType))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                float survivalVisualWeight01 = HectonDrsRenderFeatureGate.ResolveSurvivalVisualWeight01();
                if (survivalVisualWeight01 <= 0.0001f)
                    return;

                float renderScale = _settings.ResolveRenderScale(survivalVisualWeight01);
                int ssdoWidth = math.max(1, (int)(sourceDesc.width * renderScale + 0.5f));
                int ssdoHeight = math.max(1, (int)(sourceDesc.height * renderScale + 0.5f));

                TextureDesc occlusionDesc = sourceDesc;
                occlusionDesc.name = "_HectonAbyssalSSDO";
                occlusionDesc.width = ssdoWidth;
                occlusionDesc.height = ssdoHeight;
                occlusionDesc.depthBufferBits = DepthBits.None;
                occlusionDesc.msaaSamples = MSAASamples.None;
                occlusionDesc.colorFormat = GraphicsFormat.R8_UNorm;
                occlusionDesc.clearBuffer = true;
                occlusionDesc.clearColor = Color.white;
                occlusionDesc.filterMode = FilterMode.Bilinear;
                occlusionDesc.useMipMap = false;
                occlusionDesc.autoGenerateMips = false;

                TextureDesc blurDesc = occlusionDesc;
                blurDesc.name = "_HectonAbyssalSSDOBLur";

                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonAbyssalSSDOComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                compositeDesc.colorFormat = sourceDesc.colorFormat;

                TextureHandle occlusionTexture = renderGraph.CreateTexture(occlusionDesc);
                TextureHandle blurTexture = renderGraph.CreateTexture(blurDesc);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                float projectionRadiusMeters = _settings.ResolveRadiusMeters(survivalVisualWeight01);
                float projectionScale = math.abs(cameraData.camera.projectionMatrix.m11) * 0.5f * sourceDesc.height * projectionRadiusMeters;
                Vector4 inputSize = new Vector4(
                    sourceDesc.width,
                    sourceDesc.height,
                    1f / math.max(1, sourceDesc.width),
                    1f / math.max(1, sourceDesc.height));
                Vector4 outputSize = new Vector4(
                    ssdoWidth,
                    ssdoHeight,
                    1f / math.max(1, ssdoWidth),
                    1f / math.max(1, ssdoHeight));
                float radiusMeters = _settings.ResolveRadiusMeters(survivalVisualWeight01);
                float intensity = _settings.ResolveIntensity(survivalVisualWeight01);
                float bias = math.max(0f, _settings.bias);
                float depthSigma = math.max(0.01f, _settings.depthSigma);
                float blurDepthThreshold = math.max(0.001f, _settings.blurDepthThreshold);
                float compositeStrength = _settings.ResolveCompositeStrength(survivalVisualWeight01);
                float safeProjectionScale = math.max(0.01f, projectionScale);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Abyssal SSDO Gather",
                    sourceTexture,
                    depthTexture,
                    default,
                    occlusionTexture,
                    _material,
                    _occlusionProperties,
                    0,
                    true,
                    false,
                    inputSize,
                    outputSize,
                    0f,
                    radiusMeters,
                    intensity,
                    bias,
                    depthSigma,
                    blurDepthThreshold,
                    safeProjectionScale,
                    compositeStrength);
                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Abyssal SSDO Blur Horizontal",
                    occlusionTexture,
                    depthTexture,
                    default,
                    blurTexture,
                    _material,
                    _blurHorizontalProperties,
                    1,
                    true,
                    false,
                    inputSize,
                    outputSize,
                    1f,
                    radiusMeters,
                    intensity,
                    bias,
                    depthSigma,
                    blurDepthThreshold,
                    safeProjectionScale,
                    compositeStrength);
                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Abyssal SSDO Blur Vertical",
                    blurTexture,
                    depthTexture,
                    default,
                    occlusionTexture,
                    _material,
                    _blurVerticalProperties,
                    2,
                    true,
                    false,
                    inputSize,
                    outputSize,
                    2f,
                    radiusMeters,
                    intensity,
                    bias,
                    depthSigma,
                    blurDepthThreshold,
                    safeProjectionScale,
                    compositeStrength);
                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Abyssal SSDO Composite",
                    sourceTexture,
                    depthTexture,
                    occlusionTexture,
                    compositeTexture,
                    _material,
                    _compositeProperties,
                    3,
                    true,
                    true,
                    inputSize,
                    outputSize,
                    3f,
                    radiusMeters,
                    intensity,
                    bias,
                    depthSigma,
                    blurDepthThreshold,
                    safeProjectionScale,
                    compositeStrength);

                resourceData.cameraColor = compositeTexture;
            }

            private void RecordFullscreenPass(
                RenderGraph renderGraph,
                string passName,
                TextureHandle source,
                TextureHandle depth,
                TextureHandle ssdo,
                TextureHandle destination,
                Material material,
                MaterialPropertyBlock propertyBlock,
                int shaderPassIndex,
                bool bindDepth,
                bool bindSsdo,
                Vector4 inputSize,
                Vector4 outputSize,
                float passMode,
                float radiusMeters,
                float intensity,
                float bias,
                float depthSigma,
                float blurDepthThreshold,
                float projectionScale,
                float compositeStrength)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<SsdoFullscreenPassData>(
                           passName,
                           out SsdoFullscreenPassData passData,
                           _profilingSampler))
                {
                    passData.Source = source;
                    passData.Depth = depth;
                    passData.Ssdo = ssdo;
                    passData.Material = material;
                    passData.PropertyBlock = propertyBlock;
                    passData.ShaderPassIndex = shaderPassIndex;
                    passData.BindDepth = bindDepth;
                    passData.BindSsdo = bindSsdo;
                    passData.InputSize = inputSize;
                    passData.OutputSize = outputSize;
                    passData.PassMode = passMode;
                    passData.RadiusMeters = radiusMeters;
                    passData.Intensity = intensity;
                    passData.Bias = bias;
                    passData.DepthSigma = depthSigma;
                    passData.BlurDepthThreshold = blurDepthThreshold;
                    passData.ProjectionScale = projectionScale;
                    passData.CompositeStrength = compositeStrength;

                    builder.UseTexture(source, AccessFlags.Read);
                    if (bindDepth)
                        builder.UseTexture(depth, AccessFlags.Read);
                    if (bindSsdo)
                        builder.UseTexture(ssdo, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (SsdoFullscreenPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null)
                            return;

                        MaterialPropertyBlock properties = data.PropertyBlock;
                        properties.Clear();
                        properties.SetFloat(ShaderConstants.PassModeId, data.PassMode);
                        properties.SetVector(ShaderConstants.InputSizeId, data.InputSize);
                        properties.SetVector(ShaderConstants.OutputSizeId, data.OutputSize);
                        properties.SetFloat(ShaderConstants.RadiusMetersId, data.RadiusMeters);
                        properties.SetFloat(ShaderConstants.IntensityId, data.Intensity);
                        properties.SetFloat(ShaderConstants.BiasId, data.Bias);
                        properties.SetFloat(ShaderConstants.DepthSigmaId, data.DepthSigma);
                        properties.SetFloat(ShaderConstants.BlurDepthThresholdId, data.BlurDepthThreshold);
                        properties.SetFloat(ShaderConstants.ProjectionScaleId, data.ProjectionScale);
                        properties.SetFloat(ShaderConstants.CompositeStrengthId, data.CompositeStrength);
                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        if (data.BindDepth)
                            context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        if (data.BindSsdo)
                            context.cmd.SetGlobalTexture(ShaderConstants.SsdoTextureId, data.Ssdo);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, properties, data.ShaderPassIndex);
                    });
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int PassModeId = Shader.PropertyToID("_HectonAbyssalSsdoPassMode");
            internal static readonly int InputSizeId = Shader.PropertyToID("_HectonAbyssalSsdoInputSize");
            internal static readonly int OutputSizeId = Shader.PropertyToID("_HectonAbyssalSsdoOutputSize");
            internal static readonly int RadiusMetersId = Shader.PropertyToID("_HectonAbyssalSsdoRadiusMeters");
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonAbyssalSsdoIntensity");
            internal static readonly int BiasId = Shader.PropertyToID("_HectonAbyssalSsdoBias");
            internal static readonly int DepthSigmaId = Shader.PropertyToID("_HectonAbyssalSsdoDepthSigma");
            internal static readonly int BlurDepthThresholdId = Shader.PropertyToID("_HectonAbyssalSsdoBlurDepthThreshold");
            internal static readonly int ProjectionScaleId = Shader.PropertyToID("_HectonAbyssalSsdoProjectionScale");
            internal static readonly int CompositeStrengthId = Shader.PropertyToID("_HectonAbyssalSsdoCompositeStrength");
            internal static readonly int SsdoTextureId = Shader.PropertyToID("_HectonAbyssalSSDOTex");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private AbyssalSsdoPass _pass;

        /// <inheritdoc />
        public override void Create()
        {
            HectonDrsRenderFeatureGate.PrimeCold();
            _pass ??= new AbyssalSsdoPass();
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner())
                return;

            if (settings == null ||
                _pass == null ||
                settings.material == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (IsUnsupportedCameraType(cameraType))
                return;

            _pass.Setup(settings, settings.material);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass = null;
        }
    }
}
