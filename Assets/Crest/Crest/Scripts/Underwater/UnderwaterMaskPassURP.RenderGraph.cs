// Crest Ocean System

// Copyright 2024 Wave Harmonic Ltd

#if CREST_URP
#if UNITY_2023_3_OR_NEWER

namespace Crest
{
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    partial class UnderwaterMaskPassURP : ScriptableRenderPass
    {
        class PassData
        {
            public UniversalCameraData cameraData;
            public TextureHandle maskTexture;
            public TextureHandle depthTexture;
            public TextureHandle volumeFrontFaceTexture;
            public TextureHandle volumeBackFaceTexture;

            public void Init(UnderwaterMaskPassURP owner, RenderGraph graph, ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
            {
                cameraData = frameData.Get<UniversalCameraData>();
                owner.EnsureRenderGraphTargets(cameraData);

                if (graph != null && owner._maskRT != null)
                    maskTexture = graph.ImportTexture(owner._maskRT);
                if (graph != null && owner._depthRT != null)
                    depthTexture = graph.ImportTexture(owner._depthRT);
                if (graph != null && owner._volumeFrontFaceRT != null)
                    volumeFrontFaceTexture = graph.ImportTexture(owner._volumeFrontFaceRT);
                if (graph != null && owner._volumeBackFaceRT != null)
                    volumeBackFaceTexture = graph.ImportTexture(owner._volumeBackFaceRT);

                if (builder == null)
                    return;

                if (owner._maskRT != null)
                    builder.UseTexture(maskTexture, AccessFlags.Write);
                if (owner._depthRT != null)
                    builder.UseTexture(depthTexture, AccessFlags.Write);
                if (owner._volumeFrontFaceRT != null)
                    builder.UseTexture(volumeFrontFaceTexture, AccessFlags.Write);
                if (owner._volumeBackFaceRT != null)
                    builder.UseTexture(volumeBackFaceTexture, AccessFlags.Write);

                if (owner._maskRT != null)
                    builder.SetGlobalTextureAfterPass(maskTexture, UnderwaterRenderer.ShaderIDs.s_CrestOceanMaskTexture);
                if (owner._depthRT != null)
                    builder.SetGlobalTextureAfterPass(depthTexture, UnderwaterRenderer.ShaderIDs.s_CrestOceanMaskDepthTexture);
                if (owner._volumeFrontFaceRT != null)
                    builder.SetGlobalTextureAfterPass(volumeFrontFaceTexture, UnderwaterRenderer.ShaderIDs.s_CrestWaterVolumeFrontFaceTexture);
                if (owner._volumeBackFaceRT != null)
                    builder.SetGlobalTextureAfterPass(volumeBackFaceTexture, UnderwaterRenderer.ShaderIDs.s_CrestWaterVolumeBackFaceTexture);
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

        // Called before Configure.
        [System.Obsolete]
        public void OnCameraSetup(CommandBuffer buffer, ref RenderingData renderingData)
        {
            passData.Init(this, default, renderingData.GetFrameData());
        }

        [System.Obsolete]
        public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            passData.Init(this, default, renderingData.GetFrameData());
            var cmd = CommandBufferPool.Get(PassName);
            OnSetup(cmd, passData);
            ExecutePass(context, cmd, passData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

#endif // UNITY_2023_3_OR_NEWER
#endif // CREST_URP
