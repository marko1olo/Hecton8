using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Rendering.WaterOptics
{
    public sealed class HectonWaterOpticsTelemetryFeature : ScriptableRendererFeature
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Marker injection point. AfterRenderingOpaques tracks the UberNoir opaque extinction lane without adding a draw.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;

            [Tooltip("Enable a RenderGraph/CommandBuffer marker for water optics opaque-pass telemetry.")]
            public bool enableCommandBufferMarker = true;
        }

        private sealed class WaterOpticsTelemetryPass : ScriptableRenderPass
        {
            private const string PassName = "Hecton Water Optics Opaque Extinction";
            private const string CommandBufferMarkerName = "H8 Water Optics Opaque Extinction";

            private sealed class PassData
            {
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler(PassName);
            private FeatureSettings _settings;

            public WaterOpticsTelemetryPass()
            {
                profilingSampler = _profilingSampler;
            }

            public void Setup(FeatureSettings settings)
            {
                _settings = settings;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.AfterRenderingOpaques;
                ConfigureInput(ScriptableRenderPassInput.None);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!Application.isPlaying ||
                    _settings == null ||
                    !_settings.enableCommandBufferMarker)
                {
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview ||
                    cameraType == CameraType.Reflection ||
                    cameraType == CameraType.SceneView)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle activeColor = resourceData.activeColorTexture;
                if (!activeColor.IsValid())
                    return;

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           PassName,
                           out _,
                           _profilingSampler))
                {
                    builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.BeginSample(CommandBufferMarkerName);
                        context.cmd.EndSample(CommandBufferMarkerName);
                    });
                }
            }
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();
        private WaterOpticsTelemetryPass _pass;

        public override void Create()
        {
            if (_pass == null)
                _pass = new WaterOpticsTelemetryPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying || _pass == null || settings == null || !settings.enableCommandBufferMarker)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview ||
                cameraType == CameraType.Reflection ||
                cameraType == CameraType.SceneView)
            {
                return;
            }

            _pass.Setup(settings);
            renderer.EnqueuePass(_pass);
        }
    }
}
