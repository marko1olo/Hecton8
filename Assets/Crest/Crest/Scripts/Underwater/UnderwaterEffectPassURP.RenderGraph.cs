// Crest Ocean System

// Copyright 2024 Wave Harmonic Ltd

#if CREST_URP
#if UNITY_2023_3_OR_NEWER

namespace Crest
{
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    partial class UnderwaterEffectPassURP : ScriptableRenderPass
    {
        class PassData
        {
            public UniversalCameraData cameraData;
            public RenderGraphHelper.Handle colorTargetHandle;
            public RenderGraphHelper.Handle depthTargetHandle;
            public RenderGraphHelper.Handle temporaryColorHandle;
            public RenderGraphHelper.Handle depthStencilHandle;

            public void Init(UnderwaterEffectPassURP owner, RenderGraph graph, ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
            {
                var resources = frameData.Get<UniversalResourceData>();
                cameraData = frameData.Get<UniversalCameraData>();

                colorTargetHandle = resources.activeColorTexture;
                depthTargetHandle = resources.activeDepthTexture;
                owner.EnsureRenderGraphTemporaryTargets(cameraData, colorTargetHandle, depthTargetHandle);

                if (graph != null && owner._temporaryColorHandle != null)
                    temporaryColorHandle = graph.ImportTexture(owner._temporaryColorHandle);

                if (graph != null && owner._underwaterRenderer.UseStencilBufferOnEffect && owner._depthStencilHandle != null)
                    depthStencilHandle = graph.ImportTexture(owner._depthStencilHandle);

                if (builder != null)
                {
                    builder.UseTexture(colorTargetHandle, AccessFlags.ReadWrite);
                    builder.UseTexture(depthTargetHandle, AccessFlags.ReadWrite);
                    if (owner._temporaryColorHandle != null)
                        builder.UseTexture(temporaryColorHandle, AccessFlags.ReadWrite);
                    if (owner._underwaterRenderer.UseStencilBufferOnEffect && owner._depthStencilHandle != null)
                        builder.UseTexture(depthStencilHandle, AccessFlags.ReadWrite);
                }
            }
        }

        readonly PassData passData = new();

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            using (var builder = graph.AddUnsafePass<PassData>(PassName, out var data))
            {
                data.Init(this, graph, frame, builder);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc<PassData>((data, context) =>
                {
                    var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    OnSetup(buffer, data);
                    ExecutePass(context.GetRenderContext(), buffer, data);
                });
            }
        }

        [System.Obsolete]
        public void OnCameraSetup(CommandBuffer buffer, ref RenderingData renderingData)
        {
            passData.Init(this, default, renderingData.GetFrameData());
        }

        [System.Obsolete]
        public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            passData.Init(this, default, renderingData.GetFrameData());
            var buffer = CommandBufferPool.Get(PassName);
            OnSetup(buffer, passData);
            ExecutePass(context, buffer, passData);
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
        }

        partial class RenderObjectsWithoutFogPass
        {
            class PassData
            {
                public UniversalCameraData cameraData;
                public UniversalLightData lightData;
                public UniversalRenderingData renderingData;
                public CullingResults cullResults;

                public void Init(ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
                {
                    cameraData = frameData.Get<UniversalCameraData>();
                    lightData = frameData.Get<UniversalLightData>();
                    renderingData = frameData.Get<UniversalRenderingData>();
                    cullResults = renderingData.cullResults;
                }
            }

            readonly PassData passData = new();

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                using (var builder = graph.AddUnsafePass<PassData>(PassName, out var data))
                {
                    data.Init(frame, builder);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc<PassData>((data, context) =>
                    {
                        var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        ExecutePass(context.GetRenderContext(), buffer, data);
                    });
                }
            }

            [System.Obsolete]
            public void OnCameraSetup(CommandBuffer buffer, ref RenderingData renderingData)
            {
                passData.Init(renderingData.GetFrameData());
            }

            [System.Obsolete]
            public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                passData.Init(renderingData.GetFrameData());
                var buffer = CommandBufferPool.Get(PassName);
                ExecutePass(context, buffer, passData);
                context.ExecuteCommandBuffer(buffer);
                CommandBufferPool.Release(buffer);
            }
        }
    }
}

#endif // UNITY_2023_3_OR_NEWER
#endif // CREST_URP
