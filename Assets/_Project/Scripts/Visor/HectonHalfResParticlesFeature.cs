using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Renders configured transparent FX layers into a half-resolution buffer and composites before post processing.
    /// </summary>
    public sealed class HectonHalfResParticlesFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_HalfResParticleComposite.shader";
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
            [Tooltip("Hidden fullscreen shader used to composite the half-resolution transparent FX target.")]
            public Shader compositeShader = null;

            [Tooltip("Injection point. Before post keeps FXAA/TAA/post operating on the composited result.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Transparent particle/FX layers rendered into the half-resolution buffer.")]
            public LayerMask particleLayerMask = HectonLayerMasks.TransparentFxLayerMask;

            [Tooltip("Internal transparent FX render scale.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Composite strength for the half-resolution transparent FX buffer.")]
            [Range(0f, 1f)] public float compositeStrength = 1f;

            [Tooltip("Depth rejection scale used by the half-resolution bilateral upsample.")]
            [Range(0f, 128f)] public float bilateralDepthScale = 24f;
        }

        private sealed class HalfResParticlesPass : ScriptableRenderPass
        {
            private sealed class DrawPassData
            {
                internal RendererListHandle RendererList;
            }

            private sealed class CompositePassData
            {
                internal TextureHandle Source;
                internal TextureHandle Particles;
                internal TextureHandle Depth;
                internal TextureHandle Destination;
                internal Material Material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Half-Res Particles");
            private readonly ShaderTagId _universalForwardTag;
            private readonly ShaderTagId _universalForwardOnlyTag;
            private readonly ShaderTagId _srpDefaultUnlitTag;
            private FeatureSettings _settings;
            private Material _compositeMaterial;
            private Material _lastUploadedCompositeMaterial;
            private bool _hasCompositeStrength;
            private bool _hasBilateralDepthScale;
            private float _lastCompositeStrength;
            private float _lastBilateralDepthScale;

            public HalfResParticlesPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
                _universalForwardTag = new ShaderTagId("UniversalForward");
                _universalForwardOnlyTag = new ShaderTagId("UniversalForwardOnly");
                _srpDefaultUnlitTag = new ShaderTagId("SRPDefaultUnlit");
            }

            public void Setup(FeatureSettings settings, Material compositeMaterial)
            {
                _settings = settings;
                _compositeMaterial = compositeMaterial;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!Application.isPlaying || _settings == null || _compositeMaterial == null || _settings.compositeStrength <= 0.0001f)
                {
                    SetGlobalActive(0f);
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    SetGlobalActive(0f);
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                {
                    SetGlobalActive(0f);
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                {
                    SetGlobalActive(0f);
                    return;
                }

                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                DrawingSettings drawingSettings = CreateDrawingSettings(
                    _universalForwardTag,
                    renderingData,
                    cameraData,
                    lightData,
                    SortingCriteria.CommonTransparent);
                drawingSettings.SetShaderPassName(1, _universalForwardOnlyTag);
                drawingSettings.SetShaderPassName(2, _srpDefaultUnlitTag);

                int sanitizedLayerMask = HectonLayerMasks.SanitizeAuthoringLayerMask(_settings.particleLayerMask.value);
                FilteringSettings filteringSettings = new FilteringSettings(
                    RenderQueueRange.all,
                    sanitizedLayerMask,
                    HectonLayerMasks.AllDefinedProjectRenderingLayerMaskValue);
                RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                RendererListHandle rendererList = renderGraph.CreateRendererList(rendererListParams);

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                float renderScale = math.clamp(_settings.renderScale, 0.25f, 1f);
                int particlesWidth = math.max(1, (int)(sourceDesc.width * renderScale + 0.5f));
                int particlesHeight = math.max(1, (int)(sourceDesc.height * renderScale + 0.5f));

                TextureDesc particlesDesc = new TextureDesc(sourceDesc);
                particlesDesc.name = "_HectonHalfResParticles";
                particlesDesc.width = particlesWidth;
                particlesDesc.height = particlesHeight;
                particlesDesc.depthBufferBits = DepthBits.None;
                particlesDesc.msaaSamples = MSAASamples.None;
                particlesDesc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                particlesDesc.clearBuffer = true;
                particlesDesc.clearColor = Color.clear;
                particlesDesc.filterMode = FilterMode.Bilinear;
                particlesDesc.useMipMap = false;
                particlesDesc.autoGenerateMips = false;

                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonHalfResParticlesComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                compositeDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;

                TextureHandle particlesTexture = renderGraph.CreateTexture(particlesDesc);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);
                UpdateCompositeMaterial(
                    _compositeMaterial,
                    math.saturate(_settings.compositeStrength),
                    math.max(0f, _settings.bilateralDepthScale));
                SetGlobalActive(1f);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<DrawPassData>(
                           "Hecton Half-Res Particles Draw",
                           out DrawPassData passData,
                           _profilingSampler))
                {
                    passData.RendererList = rendererList;

                    builder.UseRendererList(rendererList);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(particlesTexture, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(particlesTexture, ShaderConstants.ParticlesTextureId);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((DrawPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.RendererList);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<CompositePassData>(
                           "Hecton Half-Res Particles Composite",
                           out CompositePassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Particles = particlesTexture;
                    passData.Depth = depthTexture;
                    passData.Destination = compositeTexture;
                    passData.Material = _compositeMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(particlesTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);

                    builder.SetRenderFunc((CompositePassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.Source, data.Destination, LoadAction, StoreAction, data.Material, 0);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void UpdateCompositeMaterial(Material material, float compositeStrength, float bilateralDepthScale)
            {
                if (_lastUploadedCompositeMaterial != material)
                {
                    _lastUploadedCompositeMaterial = material;
                    _hasCompositeStrength = false;
                    _hasBilateralDepthScale = false;
                }

                if (!_hasCompositeStrength || math.abs(_lastCompositeStrength - compositeStrength) > 0.000001f)
                {
                    material.SetFloat(ShaderConstants.CompositeStrengthId, compositeStrength);
                    _lastCompositeStrength = compositeStrength;
                    _hasCompositeStrength = true;
                }

                if (_hasBilateralDepthScale && math.abs(_lastBilateralDepthScale - bilateralDepthScale) <= 0.000001f)
                    return;

                material.SetFloat(ShaderConstants.BilateralDepthScaleId, bilateralDepthScale);
                _lastBilateralDepthScale = bilateralDepthScale;
                _hasBilateralDepthScale = true;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int CompositeStrengthId = Shader.PropertyToID("_HectonHalfResParticlesCompositeStrength");
            internal static readonly int BilateralDepthScaleId = Shader.PropertyToID("_HectonHalfResParticlesBilateralDepthScale");
            internal static readonly int ParticlesTextureId = Shader.PropertyToID("_HectonHalfResParticlesTex");
            internal static readonly int ActiveId = Shader.PropertyToID("_HectonHalfResParticlesActive");
        }

        private static float _lastPublishedActive = -1f;

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private HalfResParticlesPass _pass;
        private Material _compositeMaterial;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.compositeShader == null)
                settings.compositeShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null && settings.compositeShader != null
                ? settings.compositeShader
                : Shader.Find("Hidden/Hecton8/HalfResParticleComposite");
            RecreateMaterial(ref _compositeMaterial, shader);
            _pass ??= new HalfResParticlesPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
            {
                SetGlobalActive(0f);
                return;
            }

            if (settings == null || _pass == null || _compositeMaterial == null || settings.compositeStrength <= 0.0001f)
            {
                SetGlobalActive(0f);
                return;
            }

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
            {
                SetGlobalActive(0f);
                return;
            }

            _pass.Setup(settings, _compositeMaterial);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeMaterial(ref _compositeMaterial);
            SetGlobalActive(0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetGlobalActive(float value)
        {
            if (_lastPublishedActive == value)
                return;

            Shader.SetGlobalFloat(ShaderConstants.ActiveId, value);
            _lastPublishedActive = value;
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
