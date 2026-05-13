using Hecton8.Core;
using Hecton8.Physics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// RenderGraph-owned dispatch bridge for bounded GPU fluid particle advection.
    /// </summary>
    public sealed class HectonFluidAdvectionRenderFeature : ScriptableRendererFeature
    {
        private sealed class FluidAdvectionPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal HectonFluidEngine.FluidAdvectionRenderGraphPayload Payload;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Fluid Advection");

            public FluidAdvectionPass()
            {
                profilingSampler = _profilingSampler;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                    return;

                HectonFluidEngine engine = GlobalRegistry.Fluid;
                if (engine == null ||
                    !engine.TryBuildFluidAdvectionRenderGraphPayload(out HectonFluidEngine.FluidAdvectionRenderGraphPayload payload) ||
                    payload.Compute == null ||
                    payload.Kernel < 0 ||
                    payload.DispatchGroups <= 0)
                {
                    return;
                }

                BufferHandle siltRead = renderGraph.ImportBuffer(payload.SiltRead);
                BufferHandle siltWrite = renderGraph.ImportBuffer(payload.SiltWrite);
                BufferHandle bubbleRead = renderGraph.ImportBuffer(payload.BubbleRead);
                BufferHandle bubbleWrite = renderGraph.ImportBuffer(payload.BubbleWrite);
                BufferHandle debrisRead = renderGraph.ImportBuffer(payload.DebrisRead);
                BufferHandle debrisWrite = renderGraph.ImportBuffer(payload.DebrisWrite);
                BufferHandle flow = renderGraph.ImportBuffer(payload.AbyssalFlowBuffer);
                TextureHandle flowTexture = renderGraph.ImportTexture(payload.AbyssalFlowTextureHandle);
                TextureHandle sdfTexture = renderGraph.ImportTexture(payload.VoxelSdfTextureHandle);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Fluid Advection", out PassData passData, _profilingSampler))
                {
                    passData.Payload = payload;

                    builder.UseBuffer(siltRead, AccessFlags.Read);
                    builder.UseBuffer(siltWrite, AccessFlags.Write);
                    builder.UseBuffer(bubbleRead, AccessFlags.Read);
                    builder.UseBuffer(bubbleWrite, AccessFlags.Write);
                    builder.UseBuffer(debrisRead, AccessFlags.Read);
                    builder.UseBuffer(debrisWrite, AccessFlags.Write);
                    builder.UseBuffer(flow, AccessFlags.Read);
                    builder.UseTexture(flowTexture, AccessFlags.Read);
                    builder.UseTexture(sdfTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        HectonFluidEngine.BindFluidAdvectionCompute(cmd, in data.Payload);
                        cmd.DispatchCompute(
                            data.Payload.Compute,
                            data.Payload.Kernel,
                            data.Payload.DispatchGroups,
                            1,
                            1);
                        HectonFluidEngine.UnbindFluidAdvectionCompute(cmd, in data.Payload);
                    });
                }
            }
        }

        private FluidAdvectionPass _pass;

        public override void Create()
        {
            if (_pass == null)
                _pass = new FluidAdvectionPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                return;

            renderer.EnqueuePass(_pass);
        }
    }
}
