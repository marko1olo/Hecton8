using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
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
        private const int HalfResParticlesGlobalsStrideBytes = 16;

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

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Half-Res Particles");
            private FeatureSettings _settings;
            private Material _compositeMaterial;
            private GraphicsBuffer _halfResParticlesGlobalsBuffer;
            private GraphicsBuffer _halfResParticlesGlobalsBufferA;
            private GraphicsBuffer _halfResParticlesGlobalsBufferB;
            private HalfResParticlesGlobalsDTO _lastHalfResParticlesGlobals;
            private int _halfResParticlesGlobalsWriteIndex;
            private bool _hasHalfResParticlesGlobals;

            public HalfResParticlesPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material compositeMaterial)
            {
                _settings = settings;
                _compositeMaterial = compositeMaterial;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
                EnsureHalfResParticlesGlobalsBuffer();
            }

            public void Dispose()
            {
                _halfResParticlesGlobalsBufferA?.Release();
                _halfResParticlesGlobalsBufferA = null;
                _halfResParticlesGlobalsBufferB?.Release();
                _halfResParticlesGlobalsBufferB = null;
                _halfResParticlesGlobalsBuffer = null;
                _lastHalfResParticlesGlobals = default;
                _halfResParticlesGlobalsWriteIndex = 0;
                _hasHalfResParticlesGlobals = false;
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
                    HectonVisorShaderTagIds.UniversalForward,
                    renderingData,
                    cameraData,
                    lightData,
                    SortingCriteria.CommonTransparent);
                drawingSettings.SetShaderPassName(1, HectonVisorShaderTagIds.UniversalForwardOnly);
                drawingSettings.SetShaderPassName(2, HectonVisorShaderTagIds.SrpDefaultUnlit);

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
                if (!UpdateCompositeGlobals(
                        math.saturate(_settings.compositeStrength),
                        math.max(0f, _settings.bilateralDepthScale)))
                {
                    SetGlobalActive(0f);
                    return;
                }

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

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, compositeTexture, _compositeMaterial, 0),
                           passName: "Hecton Half-Res Particles Composite",
                           returnBuilder: true))
                {
                    builder.UseTexture(particlesTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                }

                resourceData.cameraColor = compositeTexture;
            }

            private bool EnsureHalfResParticlesGlobalsBuffer()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                    return false;

                if (_halfResParticlesGlobalsBufferA == null || !_halfResParticlesGlobalsBufferA.IsValid() ||
                    _halfResParticlesGlobalsBufferB == null || !_halfResParticlesGlobalsBufferB.IsValid())
                {
                    _halfResParticlesGlobalsBufferA?.Release();
                    _halfResParticlesGlobalsBufferB?.Release();
                    _halfResParticlesGlobalsBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        HalfResParticlesGlobalsStrideBytes); // COLD ALLOC: GraphicsBuffer[16B] - half-res particle composite globals A - owner: HalfResParticlesPass
                    _halfResParticlesGlobalsBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        HalfResParticlesGlobalsStrideBytes); // COLD ALLOC: GraphicsBuffer[16B] - half-res particle composite globals B - owner: HalfResParticlesPass
                    _halfResParticlesGlobalsBuffer = _halfResParticlesGlobalsBufferA;
                    _halfResParticlesGlobalsWriteIndex = 1;
                    _hasHalfResParticlesGlobals = false;
                }

                return _halfResParticlesGlobalsBufferA != null && _halfResParticlesGlobalsBufferA.IsValid() &&
                       _halfResParticlesGlobalsBufferB != null && _halfResParticlesGlobalsBufferB.IsValid();
            }

            private bool UpdateCompositeGlobals(float compositeStrength, float bilateralDepthScale)
            {
                if (!EnsureHalfResParticlesGlobalsBuffer())
                    return false;

                HalfResParticlesGlobalsDTO globals = HalfResParticlesGlobalsDTO.FromValues(compositeStrength, bilateralDepthScale);
                if (_hasHalfResParticlesGlobals && _lastHalfResParticlesGlobals.Params == globals.Params)
                {
                    Shader.SetGlobalConstantBuffer(ShaderConstants.HalfResParticlesGlobalsBufferId, _halfResParticlesGlobalsBuffer, 0, HalfResParticlesGlobalsStrideBytes);
                    return true;
                }

                GraphicsBuffer writeBuffer = _halfResParticlesGlobalsWriteIndex == 0 ? _halfResParticlesGlobalsBufferA : _halfResParticlesGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                NativeArray<HalfResParticlesGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<HalfResParticlesGlobalsDTO>(0, 1);
                try
                {
                    mapped[0] = globals;
                }
                finally
                {
                    writeBuffer.UnlockBufferAfterWrite<HalfResParticlesGlobalsDTO>(1);
                }

                _halfResParticlesGlobalsBuffer = writeBuffer;
                _halfResParticlesGlobalsWriteIndex ^= 1;
                Shader.SetGlobalConstantBuffer(ShaderConstants.HalfResParticlesGlobalsBufferId, _halfResParticlesGlobalsBuffer, 0, HalfResParticlesGlobalsStrideBytes);
                _lastHalfResParticlesGlobals = globals;
                _hasHalfResParticlesGlobals = true;
                return true;
            }

            [StructLayout(LayoutKind.Explicit, Size = HalfResParticlesGlobalsStrideBytes)]
            private struct HalfResParticlesGlobalsDTO
            {
                [FieldOffset(0)]
                internal Vector4 Params;

                internal static HalfResParticlesGlobalsDTO FromValues(float compositeStrength, float bilateralDepthScale)
                {
                    HalfResParticlesGlobalsDTO dto;
                    dto.Params = new Vector4(compositeStrength, bilateralDepthScale, 1f, 0f);
                    return dto;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int HalfResParticlesGlobalsBufferId = Shader.PropertyToID("HectonHalfResParticlesGlobals");
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

            if (HectonDrsRenderFeatureGate.ShouldCullForSurvivalScale())
            {
                SetGlobalActive(0f);
                return;
            }

            _pass.Setup(settings, _compositeMaterial);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
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
