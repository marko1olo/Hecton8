#if UNITY_EDITOR
using System;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Editor-only additive overdraw heatmap for visualizing fillrate hotspots.
    /// </summary>
    public sealed class HectonOverdrawHeatmapFeature : ScriptableRendererFeature
    {
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_OverdrawHeatmap.shader";
        private static readonly int HeatColorId = Shader.PropertyToID("_HeatColor");
        private static readonly int HeatStrengthId = Shader.PropertyToID("_HeatStrength");
        private static readonly ShaderTagId UniversalForwardShaderTag = new ShaderTagId("UniversalForward");
        private static readonly ShaderTagId UniversalForwardOnlyShaderTag = new ShaderTagId("UniversalForwardOnly");
        private static readonly ShaderTagId SrpDefaultUnlitShaderTag = new ShaderTagId("SRPDefaultUnlit");

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden additive shader used to accumulate overdraw heat.")]
            public Shader shader;

            [Tooltip("Injection point for the editor overdraw overlay.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Per-draw additive contribution color.")]
            public Color heatColor = new Color(0.08f, 0.015f, 0.0f, 1.0f);

            [Tooltip("Per-draw additive heat strength.")]
            [Range(0.01f, 1f)] public float heatStrength = 0.12f;
        }

        private sealed class OverdrawHeatmapPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal RendererListHandle RendererList;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Overdraw Heatmap");
            private readonly FilteringSettings _filteringSettings = new FilteringSettings(
                RenderQueueRange.all,
                HectonLayerMasks.RenderGraphWorldLayerMask,
                HectonLayerMasks.AllDefinedProjectRenderingLayerMaskValue);
            private FeatureSettings _settings;
            private Material _material;

            public OverdrawHeatmapPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                    return;

                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

                DrawingSettings drawingSettings = CreateDrawingSettings(UniversalForwardShaderTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                drawingSettings.SetShaderPassName(1, UniversalForwardOnlyShaderTag);
                drawingSettings.SetShaderPassName(2, SrpDefaultUnlitShaderTag);
                drawingSettings.overrideMaterial = _material;
                drawingSettings.overrideMaterialPassIndex = 0;

                RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, _filteringSettings);
                RendererListHandle rendererList = renderGraph.CreateRendererList(rendererListParams);

                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Hecton Overdraw Heatmap", out PassData passData, _profilingSampler);
                passData.RendererList = rendererList;

                builder.UseRendererList(rendererList);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }
        }

        [SerializeField]
        private FeatureSettings settings = new FeatureSettings();

        private Material _material;
        private OverdrawHeatmapPass _pass;

        public override void Create()
        {
            if (settings.shader == null)
                settings.shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);

            if (settings.shader != null)
            {
                if (_material == null || _material.shader != settings.shader)
                {
                    DisposeMaterial();
                    _material = CoreUtils.CreateEngineMaterial(settings.shader);
                }

                _material.SetColor(HeatColorId, settings.heatColor.linear);
                _material.SetFloat(HeatStrengthId, settings.heatStrength);
            }
            else
            {
                DisposeMaterial();
            }

            _pass ??= new OverdrawHeatmapPass();
            _pass.Setup(settings, _material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null)
                return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeMaterial();
        }

        private void DisposeMaterial()
        {
            if (_material == null)
                return;

            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}
#endif
