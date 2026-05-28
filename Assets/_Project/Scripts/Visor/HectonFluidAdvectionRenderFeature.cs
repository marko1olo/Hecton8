using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace Hecton8.Visor
{
    /// <summary>
    /// RenderGraph-owned dispatch bridge for bounded GPU fluid particle advection.
    /// </summary>
    public sealed class HectonFluidAdvectionRenderFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private sealed class FluidAdvectionPass : ScriptableRenderPass
        {
            private const RenderPassEvent VisualSyncRenderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

            private sealed class PassData
            {
                internal FluidAdvectionRenderGraphPayload Payload;
                internal IFluidAdvectionRenderGraphDispatchSource DispatchSource;
                internal TextureHandle FlowTexture;
                internal TextureHandle SdfTexture;
                internal TextureHandle EmptyTexture;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Fluid Advection");
            private IFluidAdvectionRenderGraphDispatchSource _engine;

            public FluidAdvectionPass()
            {
                profilingSampler = _profilingSampler;
                renderPassEvent = VisualSyncRenderPassEvent;
            }

            public void Setup(IFluidAdvectionRenderGraphDispatchSource engine)
            {
                _engine = engine;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                    return;

                if (cameraData.renderType != CameraRenderType.Base)
                    return;

                IFluidAdvectionRenderGraphDispatchSource engine = _engine;
                if (engine == null ||
                    !engine.TryClaimFluidAdvectionRenderGraphPayload(out FluidAdvectionRenderGraphPayload payload) ||
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
                BufferHandle dynamicWake = renderGraph.ImportBuffer(payload.DynamicWakeBuffer);
                BufferHandle dynamicWakeVectors = renderGraph.ImportBuffer(payload.DynamicWakeVectorBuffer);
                TextureHandle flowTexture = renderGraph.ImportTexture(payload.AbyssalFlowTextureHandle);
                TextureHandle sdfTexture = renderGraph.ImportTexture(payload.VoxelSdfTextureHandle);
                TextureHandle emptyTexture = renderGraph.ImportTexture(payload.EmptyVoxelSdfTextureHandle);

                using (var builder = renderGraph.AddComputePass("Hecton Fluid Advection", out PassData passData, _profilingSampler))
                {
                    passData.Payload = payload;
                    passData.DispatchSource = engine;
                    passData.FlowTexture = flowTexture;
                    passData.SdfTexture = sdfTexture;
                    passData.EmptyTexture = emptyTexture;

                    builder.UseBuffer(siltRead, AccessFlags.Read);
                    builder.UseBuffer(siltWrite, AccessFlags.Write);
                    builder.UseBuffer(bubbleRead, AccessFlags.Read);
                    builder.UseBuffer(bubbleWrite, AccessFlags.Write);
                    builder.UseBuffer(debrisRead, AccessFlags.Read);
                    builder.UseBuffer(debrisWrite, AccessFlags.Write);
                    builder.UseBuffer(flow, AccessFlags.Read);
                    builder.UseBuffer(dynamicWake, AccessFlags.Read);
                    builder.UseBuffer(dynamicWakeVectors, AccessFlags.Read);
                    builder.UseTexture(flowTexture, AccessFlags.Read);
                    builder.UseTexture(sdfTexture, AccessFlags.Read);
                    builder.UseTexture(emptyTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                    {
                        data.DispatchSource.BindFluidAdvectionCompute(context.cmd, in data.Payload, data.FlowTexture, data.SdfTexture);
                        context.cmd.DispatchCompute(
                            data.Payload.Compute,
                            data.Payload.Kernel,
                            data.Payload.DispatchGroups,
                            1,
                            1);
                        data.DispatchSource.UnbindFluidAdvectionCompute(context.cmd, in data.Payload, data.EmptyTexture);
                    });
                }
            }
        }

        private FluidAdvectionPass _pass;
        private IFluidAdvectionRenderGraphDispatchSource _cachedFluidEngine;
        private bool _hotSwapRegistered;

        public override void Create()
        {
            if (_pass == null)
                _pass = new FluidAdvectionPass();

            TryRegisterHotSwapListener();
            _cachedFluidEngine = GlobalRegistry.FluidAdvectionRenderGraph;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
                return;

            IFluidAdvectionRenderGraphDispatchSource engine = _cachedFluidEngine;
            if (engine == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                return;

            if (renderingData.cameraData.renderType != CameraRenderType.Base)
                return;

            _pass.Setup(engine);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _cachedFluidEngine = null;
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
                _cachedFluidEngine = currentService as IFluidAdvectionRenderGraphDispatchSource;
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
