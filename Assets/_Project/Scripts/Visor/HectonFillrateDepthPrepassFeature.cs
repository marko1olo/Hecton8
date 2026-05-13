using System;
using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;
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
    /// Writes water, voxel cave, and terrain depth before transparent silt and refractive passes shade pixels.
    /// </summary>
    public sealed class HectonFillrateDepthPrepassFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_FillrateDepthOnly.shader";
#endif

        // COLD ALLOC: List<ShaderTagId>[4] - renderer-list tags for fillrate depth prepass - owner: HectonFillrateDepthPrepassFeature
        private static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>(4)
        {
            new ShaderTagId("DepthOnly"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden depth-only override shader used by the fill-rate prepass.")]
            public Shader depthOnlyShader = null;

            [Tooltip("Injection point. Must execute before transparent water/silt shading.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Layers forced through depth-only prepass: water plus broad opaque occluders.")]
            public LayerMask depthPrepassLayerMask =
                HectonLayerMasks.WaterLayerMask |
                HectonLayerMasks.TerrainLayerMask |
                HectonLayerMasks.VoxelCaveLayerMask;
        }

        private sealed class FillrateDepthPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal RendererListHandle RendererList;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Fillrate Depth Prepass");
            private FeatureSettings _settings;
            private Material _depthOnlyMaterial;

            public FillrateDepthPass()
            {
                profilingSampler = _profilingSampler;
            }

            public void Setup(FeatureSettings settings, Material depthOnlyMaterial)
            {
                _settings = settings;
                _depthOnlyMaterial = depthOnlyMaterial;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _depthOnlyMaterial == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                    return;

                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!depthTexture.IsValid())
                    return;

                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                DrawingSettings drawingSettings = CreateDrawingSettings(
                    ShaderTagIds,
                    renderingData,
                    cameraData,
                    lightData,
                    SortingCriteria.CommonOpaque);
                drawingSettings.overrideMaterial = _depthOnlyMaterial;
                drawingSettings.overrideMaterialPassIndex = 0;

                int sanitizedLayerMask = HectonLayerMasks.SanitizeAuthoringLayerMask(_settings.depthPrepassLayerMask.value);
                FilteringSettings filteringSettings = new FilteringSettings(
                    RenderQueueRange.all,
                    sanitizedLayerMask,
                    HectonLayerMasks.AllDefinedProjectRenderingLayerMaskValue);
                RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                RendererListHandle rendererList = renderGraph.CreateRendererList(rendererListParams);

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                    "Hecton Fillrate Depth Prepass",
                    out PassData passData,
                    _profilingSampler);
                passData.RendererList = rendererList;

                builder.UseRendererList(rendererList);
                builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private FillrateDepthPass _pass;
        private Material _depthOnlyMaterial;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.depthOnlyShader == null)
                settings.depthOnlyShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null ? settings.depthOnlyShader : null;
            RecreateMaterial(ref _depthOnlyMaterial, shader);
            _pass ??= new FillrateDepthPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _depthOnlyMaterial == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            _pass.Setup(settings, _depthOnlyMaterial);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_depthOnlyMaterial);
            _depthOnlyMaterial = null;
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (material != null && material.shader == shader)
                return;

            CoreUtils.Destroy(material);
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        }
    }
}
