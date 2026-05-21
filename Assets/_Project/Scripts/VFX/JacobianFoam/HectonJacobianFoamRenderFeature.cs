using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.VFX
{
    public sealed class HectonJacobianFoamRenderFeature : ScriptableRendererFeature
    {
        private sealed class JacobianFoamPass : ScriptableRenderPass
        {
            private const RenderPassEvent VisualSyncRenderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

            private sealed class GeneratePassData
            {
                internal JacobianFoamGpuRuntime.FoamRenderGraphPayload Payload;
                internal TextureHandle DepthTexture;
                internal Vector4 DepthTexelSize;
                internal TextureHandle GenerationTexture;
                internal TextureHandle HistoryReadTexture;
                internal BufferHandle ParamsBuffer;
                internal BufferHandle WakeBuffer;
            }

            private sealed class AdvectPassData
            {
                internal JacobianFoamGpuRuntime.FoamRenderGraphPayload Payload;
                internal TextureHandle GenerationTexture;
                internal TextureHandle HistoryReadTexture;
                internal TextureHandle HistoryWriteTexture;
                internal BufferHandle ParamsBuffer;
            }

            private sealed class FallbackPassData
            {
                internal TextureHandle FallbackTexture;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Jacobian Foam");

            public JacobianFoamPass()
            {
                profilingSampler = _profilingSampler;
                renderPassEvent = VisualSyncRenderPassEvent;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview ||
                    cameraType == CameraType.Reflection ||
                    cameraType == CameraType.SceneView ||
                    cameraData.renderType == CameraRenderType.Overlay)
                    return;

                if (!JacobianFoamGpuRuntime.TryReadPublishedRenderGraphPayload(out JacobianFoamGpuRuntime.FoamRenderGraphPayload payload) ||
                    payload.Compute == null ||
                    payload.DispatchGroupsX <= 0 ||
                    payload.DispatchGroupsY <= 0 ||
                    payload.ParamsBuffer == null ||
                    payload.WakeBuffer == null)
                {
                    RecordFallbackTexturePass(renderGraph);
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                bool depthShorelineEnabled = !UsesSinglePassTextureArray(cameraData.xr);
                TextureHandle cameraDepthTexture = resourceData.cameraDepthTexture;
                if (depthShorelineEnabled && !cameraDepthTexture.IsValid())
                {
                    RecordFallbackTexturePass(renderGraph);
                    return;
                }
                TextureHandle depthTexture = depthShorelineEnabled ? cameraDepthTexture : renderGraph.defaultResources.blackTexture;
                if (!depthTexture.IsValid())
                {
                    RecordFallbackTexturePass(renderGraph);
                    return;
                }

                Vector4 depthTexelSize = ResolveDepthTexelSize(renderGraph, depthTexture, depthShorelineEnabled);
                if (!depthShorelineEnabled)
                    payload.WakeParams.z = 0f;

                BufferHandle paramsBuffer = renderGraph.ImportBuffer(payload.ParamsBuffer);
                BufferHandle wakeBuffer = renderGraph.ImportBuffer(payload.WakeBuffer);
                TextureHandle generationTexture = CreateGenerationTexture(renderGraph, in payload);
                TextureHandle historyReadTexture = renderGraph.ImportTexture(payload.HistoryReadTexture);
                TextureHandle historyWriteTexture = renderGraph.ImportTexture(payload.HistoryWriteTexture);

                using (var builder = renderGraph.AddComputePass("Hecton Jacobian Foam Generate", out GeneratePassData passData, _profilingSampler))
                {
                    passData.Payload = payload;
                    passData.DepthTexture = depthTexture;
                    passData.DepthTexelSize = depthTexelSize;
                    passData.GenerationTexture = generationTexture;
                    passData.HistoryReadTexture = historyReadTexture;
                    passData.ParamsBuffer = paramsBuffer;
                    passData.WakeBuffer = wakeBuffer;

                    builder.UseBuffer(paramsBuffer, AccessFlags.Read);
                    builder.UseBuffer(wakeBuffer, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(generationTexture, AccessFlags.Write);
                    if (payload.ClearHistory != 0)
                        builder.UseTexture(historyReadTexture, AccessFlags.Write);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (GeneratePassData data, ComputeGraphContext context) =>
                    {
                        JacobianFoamGpuRuntime.FoamRenderGraphPayload payloadData = data.Payload;
                        if (payloadData.ClearHistory != 0)
                        {
                            BindClear(context.cmd, in payloadData, data.GenerationTexture, data.HistoryReadTexture);
                            context.cmd.DispatchCompute(payloadData.Compute, payloadData.ClearKernel, payloadData.DispatchGroupsX, payloadData.DispatchGroupsY, 1);
                        }

                        BindCalculate(context.cmd, in payloadData, data.ParamsBuffer, data.DepthTexture, data.DepthTexelSize, data.GenerationTexture, data.WakeBuffer);
                        context.cmd.DispatchCompute(payloadData.Compute, payloadData.CalculateKernel, payloadData.DispatchGroupsX, payloadData.DispatchGroupsY, 1);
                    });
                }

                using (var builder = renderGraph.AddComputePass("Hecton Jacobian Foam Advect", out AdvectPassData passData, _profilingSampler))
                {
                    passData.Payload = payload;
                    passData.GenerationTexture = generationTexture;
                    passData.HistoryReadTexture = historyReadTexture;
                    passData.HistoryWriteTexture = historyWriteTexture;
                    passData.ParamsBuffer = paramsBuffer;

                    builder.UseBuffer(paramsBuffer, AccessFlags.Read);
                    builder.UseTexture(generationTexture, AccessFlags.Read);
                    builder.UseTexture(historyReadTexture, AccessFlags.Read);
                    builder.UseTexture(historyWriteTexture, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(historyWriteTexture, ShaderConstants.JacobianFoamTextureId);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (AdvectPassData data, ComputeGraphContext context) =>
                    {
                        JacobianFoamGpuRuntime.FoamRenderGraphPayload payloadData = data.Payload;
                        BindAdvect(context.cmd, in payloadData, data.ParamsBuffer, data.GenerationTexture, data.HistoryReadTexture, data.HistoryWriteTexture);
                        context.cmd.DispatchCompute(payloadData.Compute, payloadData.AdvectKernel, payloadData.DispatchGroupsX, payloadData.DispatchGroupsY, 1);
                        JacobianFoamGpuRuntime.AcknowledgePublishedRenderGraphPayload(payloadData.OwnerId, payloadData.Sequence, payloadData.HistoryWriteIndex, payloadData.HistoryWriteTexture.rt);
                    });
                }
            }

            private void RecordFallbackTexturePass(RenderGraph renderGraph)
            {
                TextureHandle fallbackTexture = renderGraph.defaultResources.blackTexture;
                if (!fallbackTexture.IsValid())
                    return;

                using (var builder = renderGraph.AddComputePass("Hecton Jacobian Foam Fallback", out FallbackPassData passData, _profilingSampler))
                {
                    passData.FallbackTexture = fallbackTexture;
                    builder.UseTexture(fallbackTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(fallbackTexture, ShaderConstants.JacobianFoamTextureId);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (FallbackPassData data, ComputeGraphContext context)
                    {
                        JacobianFoamGpuRuntime.AcknowledgeFallbackFoamTexture();
                    });
                }
            }

            private static TextureHandle CreateGenerationTexture(RenderGraph renderGraph, in JacobianFoamGpuRuntime.FoamRenderGraphPayload payload)
            {
                int resolution = Mathf.Max(1, payload.Resolution);
                TextureDesc desc = new TextureDesc(resolution, resolution, dynamicResolution: false, xrReady: false);
                desc.name = "_HectonJacobianFoamGeneration";
                desc.clearBuffer = false;
                desc.depthBufferBits = DepthBits.None;
                desc.colorFormat = payload.FoamTextureFormat;
                desc.msaaSamples = MSAASamples.None;
                desc.dimension = TextureDimension.Tex2D;
                desc.slices = 1;
                desc.enableRandomWrite = true;
                desc.filterMode = FilterMode.Bilinear;
                desc.wrapMode = TextureWrapMode.Repeat;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                desc.useDynamicScale = false;
                desc.useDynamicScaleExplicit = false;
                return renderGraph.CreateTexture(desc);
            }

            private static bool UsesSinglePassTextureArray(XRPass xr)
            {
                return xr != null && xr.enabled && xr.singlePassEnabled && xr.viewCount > 1;
            }

            private static Vector4 ResolveDepthTexelSize(RenderGraph renderGraph, TextureHandle depthTexture, bool depthShorelineEnabled)
            {
                if (!depthShorelineEnabled)
                    return new Vector4(1f, 1f, 1f, 1f);

                RenderTargetInfo depthInfo = renderGraph.GetRenderTargetInfo(depthTexture);
                int width = Mathf.Max(1, depthInfo.width);
                int height = Mathf.Max(1, depthInfo.height);
                return new Vector4(1f / width, 1f / height, width, height);
            }

            private static void BindCalculate(
                IComputeCommandBuffer cmd,
                in JacobianFoamGpuRuntime.FoamRenderGraphPayload payload,
                BufferHandle paramsBuffer,
                TextureHandle depthTexture,
                Vector4 depthTexelSize,
                TextureHandle generationTexture,
                BufferHandle wakeBuffer)
            {
                cmd.SetComputeConstantBufferParam(payload.Compute, ShaderConstants.ParamsBufferId, paramsBuffer, 0, JacobianFoamContracts.ParamsStrideBytes);
                if (wakeBuffer.IsValid())
                    cmd.SetComputeBufferParam(payload.Compute, payload.CalculateKernel, ShaderConstants.WakeImpactsId, wakeBuffer);
                cmd.SetComputeTextureParam(payload.Compute, payload.CalculateKernel, ShaderConstants.GenerationTextureId, generationTexture);
                cmd.SetComputeTextureParam(payload.Compute, payload.CalculateKernel, ShaderConstants.SourceDepthTextureId, depthTexture);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.SourceDepthTextureTexelSizeId, depthTexelSize);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.GridParamsId, payload.GridParams);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.WorldParamsId, payload.WorldParams);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.WakeParamsId, payload.WakeParams);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.Wave0Id, payload.Wave0);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.Wave1Id, payload.Wave1);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.Wave2Id, payload.Wave2);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.Wave3Id, payload.Wave3);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.WaveSpeedId, payload.WaveSpeed);
            }

            private static void BindAdvect(
                IComputeCommandBuffer cmd,
                in JacobianFoamGpuRuntime.FoamRenderGraphPayload payload,
                BufferHandle paramsBuffer,
                TextureHandle generationTexture,
                TextureHandle historyReadTexture,
                TextureHandle historyWriteTexture)
            {
                cmd.SetComputeConstantBufferParam(payload.Compute, ShaderConstants.ParamsBufferId, paramsBuffer, 0, JacobianFoamContracts.ParamsStrideBytes);
                cmd.SetComputeTextureParam(payload.Compute, payload.AdvectKernel, ShaderConstants.GenerationTextureId, generationTexture);
                cmd.SetComputeTextureParam(payload.Compute, payload.AdvectKernel, ShaderConstants.HistoryTextureId, historyReadTexture);
                cmd.SetComputeTextureParam(payload.Compute, payload.AdvectKernel, ShaderConstants.OutputTextureId, historyWriteTexture);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.GridParamsId, payload.GridParams);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.WorldParamsId, payload.WorldParams);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.WakeParamsId, payload.WakeParams);
            }

            private static void BindClear(
                IComputeCommandBuffer cmd,
                in JacobianFoamGpuRuntime.FoamRenderGraphPayload payload,
                TextureHandle generationTexture,
                TextureHandle historyWriteTexture)
            {
                cmd.SetComputeTextureParam(payload.Compute, payload.ClearKernel, ShaderConstants.GenerationTextureId, generationTexture);
                cmd.SetComputeTextureParam(payload.Compute, payload.ClearKernel, ShaderConstants.OutputTextureId, historyWriteTexture);
                cmd.SetComputeVectorParam(payload.Compute, ShaderConstants.GridParamsId, payload.GridParams);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int ParamsBufferId = Shader.PropertyToID("FoamComputeParamsDTO");
            internal static readonly int GenerationTextureId = Shader.PropertyToID("_FoamGenerationTexture");
            internal static readonly int HistoryTextureId = Shader.PropertyToID("_FoamHistoryTexture");
            internal static readonly int OutputTextureId = Shader.PropertyToID("_FoamOutputTexture");
            internal static readonly int SourceDepthTextureId = Shader.PropertyToID("_FoamSourceDepthTexture");
            internal static readonly int SourceDepthTextureTexelSizeId = Shader.PropertyToID("_FoamSourceDepthTexture_TexelSize");
            internal static readonly int WakeImpactsId = Shader.PropertyToID("_FoamWakeImpacts");
            internal static readonly int GridParamsId = Shader.PropertyToID("_FoamGridParams");
            internal static readonly int WorldParamsId = Shader.PropertyToID("_FoamWorldParams");
            internal static readonly int WakeParamsId = Shader.PropertyToID("_FoamWakeParams");
            internal static readonly int Wave0Id = Shader.PropertyToID("_FoamWave0");
            internal static readonly int Wave1Id = Shader.PropertyToID("_FoamWave1");
            internal static readonly int Wave2Id = Shader.PropertyToID("_FoamWave2");
            internal static readonly int Wave3Id = Shader.PropertyToID("_FoamWave3");
            internal static readonly int WaveSpeedId = Shader.PropertyToID("_FoamWaveSpeed");
            internal static readonly int JacobianFoamTextureId = Shader.PropertyToID("_H8JacobianFoamTexture");
        }

        private JacobianFoamPass _pass;

        public override void Create()
        {
            if (_pass == null)
                _pass = new JacobianFoamPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview ||
                cameraType == CameraType.Reflection ||
                cameraType == CameraType.SceneView ||
                renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            renderer.EnqueuePass(_pass);
        }
    }
}
