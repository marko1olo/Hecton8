using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Captures save thumbnails from the active URP player camera without forcing manual camera renders.
    /// </summary>
    public sealed class SaveThumbnailCaptureFeature : ScriptableRendererFeature
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Thumbnail capture injection point. AfterRendering preserves the resolved game-view output.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering;
        }

        private sealed class SaveThumbnailCapturePass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal RTHandle destinationHandle;
                internal int requestSequenceId;
                internal int requestGenerationId;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Save Thumbnail Capture");
            private FeatureSettings _settings;
            private RTHandle _captureTexture;
            private int _requestSequenceId;
            private int _requestGenerationId;

            public SaveThumbnailCapturePass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public bool Setup(FeatureSettings settings, in SaveThumbnailSystem.RenderRequest request)
            {
                if (!HasCaptureTextureReady())
                    return false;

                _settings = settings;
                _requestSequenceId = request.SequenceId;
                _requestGenerationId = request.Generation;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.AfterRendering;
                ConfigureInput(ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
                return true;
            }

            public void PrepareCaptureTextureCold()
            {
                EnsureCaptureTextureCold();
            }

            public void Dispose()
            {
                _captureTexture?.Release();
                _captureTexture = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _captureTexture == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview ||
                    cameraType == CameraType.Reflection ||
                    cameraType == CameraType.SceneView)
                {
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                if (!sourceTexture.IsValid())
                    return;

                TextureHandle captureTexture = renderGraph.ImportTexture(_captureTexture);
                using (var builder = renderGraph.AddUnsafePass<PassData>("Save Thumbnail Capture", out PassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.destination = captureTexture;
                    passData.destinationHandle = _captureTexture;
                    passData.requestSequenceId = _requestSequenceId;
                    passData.requestGenerationId = _requestGenerationId;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(captureTexture, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, 0f, true);
                        if (!SaveThumbnailSystem.TryQueueGpuReadback(
                                cmd,
                                data.destinationHandle.rt,
                                data.requestSequenceId,
                                data.requestGenerationId))
                        {
                            SaveThumbnailSystem.TryFailPendingRenderRequest(data.requestSequenceId, data.requestGenerationId);
                        }
                    });
                }
            }

            private bool HasCaptureTextureReady()
            {
                return _captureTexture != null &&
                       _captureTexture.rt != null &&
                       _captureTexture.rt.width == SaveThumbnailSystem.CaptureWidth &&
                       _captureTexture.rt.height == SaveThumbnailSystem.CaptureHeight;
            }

            private void EnsureCaptureTextureCold()
            {
                if (HasCaptureTextureReady())
                {
                    return;
                }

                _captureTexture?.Release();
                _captureTexture = RTHandles.Alloc(
                    SaveThumbnailSystem.CaptureWidth,
                    SaveThumbnailSystem.CaptureHeight,
                    1,
                    DepthBits.None,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    TextureDimension.Tex2D,
                    false,
                    name: "_SaveThumbnailCaptureTexture");
            }
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private SaveThumbnailCapturePass _pass;

        public override void Create()
        {
            _pass ??= new SaveThumbnailCapturePass();
            _pass.PrepareCaptureTextureCold();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !SaveThumbnailSystem.TryAcquireRenderRequest(renderingData.cameraData.camera, out SaveThumbnailSystem.RenderRequest request))
                return;

            if (!_pass.Setup(settings, request))
            {
                SaveThumbnailSystem.TryFailPendingRenderRequest(request.SequenceId, request.Generation);
                return;
            }

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            SaveThumbnailSystem.NotifyCaptureFeatureDisposed();
        }
    }
}
