using Hecton8.UI;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI.Rendering
{
    public sealed class WristPdaScreenProjectorFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_PdaScreen.shader";
#endif

        [System.Serializable]
        private sealed class FeatureSettings
        {
            public Shader shader;
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        private sealed class PdaProjectorPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal Material Material;
                internal Texture AtlasTexture;
                internal BufferHandle StateBuffer;
                internal BufferHandle GlobalsBuffer;
            }

            private readonly ProfilingSampler _sampler = new ProfilingSampler("Hecton PDA Screen Projector");
            private FeatureSettings _settings;
            private Material _material;

            public PdaProjectorPass()
            {
                profilingSampler = _sampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
                    return;

                if (!WristHologramHudRuntime.TryGetActivePdaProjectionResources(
                        out GraphicsBuffer stateBuffer,
                        out GraphicsBuffer globalsBuffer,
                        out Texture atlasTexture) ||
                    stateBuffer == null ||
                    globalsBuffer == null ||
                    atlasTexture == null ||
                    !stateBuffer.IsValid() ||
                    !globalsBuffer.IsValid())
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc)
                {
                    name = "_HectonPdaScreenProjector",
                    clearBuffer = false,
                    depthBufferBits = DepthBits.None,
                    msaaSamples = MSAASamples.None,
                    colorFormat = sourceDesc.colorFormat
                };
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle stateBufferHandle = renderGraph.ImportBuffer(stateBuffer);
                BufferHandle globalsBufferHandle = renderGraph.ImportBuffer(globalsBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton PDA Screen Projector",
                           out PassData passData,
                           _sampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.Material = _material;
                    passData.AtlasTexture = atlasTexture;
                    passData.StateBuffer = stateBufferHandle;
                    passData.GlobalsBuffer = globalsBufferHandle;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseBuffer(stateBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(globalsBufferHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        GraphicsBuffer stateGraphicsBuffer = data.StateBuffer;
                        GraphicsBuffer globalsGraphicsBuffer = data.GlobalsBuffer;
                        if (stateGraphicsBuffer == null || globalsGraphicsBuffer == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        data.Material.SetTexture(ShaderConstants.AtlasTextureId, data.AtlasTexture);
                        context.cmd.SetGlobalBuffer(ShaderConstants.PdaStateBufferId, stateGraphicsBuffer);
                        context.cmd.SetGlobalConstantBuffer(
                            globalsGraphicsBuffer,
                            ShaderConstants.PdaGlobalsBufferId,
                            0,
                            64);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int AtlasTextureId = Shader.PropertyToID("_HectonPdaInterfaceAtlas");
            internal static readonly int PdaStateBufferId = Shader.PropertyToID("_HectonPdaStateBuffer");
            internal static readonly int PdaGlobalsBufferId = Shader.PropertyToID("HectonPdaProjectionGlobals");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private PdaProjectorPass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif
            _pass ??= new PdaProjectorPass();
            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                return;
            }

            if (_material == null || _material.shader != shader)
            {
                CoreUtils.Destroy(_material);
                _material = CoreUtils.CreateEngineMaterial(shader);
                _material.hideFlags = HideFlags.DontSave;
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
                return;

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}
