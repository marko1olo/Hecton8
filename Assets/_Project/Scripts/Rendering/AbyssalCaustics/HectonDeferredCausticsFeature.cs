using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Rendering
{
    public sealed class HectonDeferredCausticsFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_DeferredCaustics.shader";
#endif

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for screen-space deferred caustics.")]
            public Shader shader = null;

            [Tooltip("Injection point after opaque lighting and before transparent water/visor overlays.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;
        }

        private sealed class DeferredCausticsPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Deferred Caustics");
            private FeatureSettings _settings;
            private Material _material;

            public DeferredCausticsPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                    return;
                if (cameraData.renderType != CameraRenderType.Base)
                    return;

                if (!AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out GraphicsBuffer constantBuffer, out _))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonDeferredCausticsComposite";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle constantBufferHandle = renderGraph.ImportBuffer(constantBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton Deferred Caustics",
                           out PassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.Material = _material;
                    passData.ConstantBuffer = constantBufferHandle;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseBuffer(constantBufferHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        GraphicsBuffer constantBuffer = data.ConstantBuffer;
                        if (constantBuffer == null)
                            return;

                        context.cmd.SetGlobalTexture(AbyssalCausticsShaderIds.SourceTextureId, data.Source);
                        context.cmd.SetGlobalTexture(AbyssalCausticsShaderIds.DepthTextureId, data.Depth);
                        context.cmd.SetGlobalConstantBuffer(
                            constantBuffer,
                            AbyssalCausticsShaderIds.ConstantBufferId,
                            0,
                            AbyssalCausticsConstants.CBufferBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private sealed class PassData
            {
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal Material Material;
                internal BufferHandle ConstantBuffer;
            }
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DeferredCausticsPass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null ? settings.shader : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shader == null)
                shader = Shader.Find("Hidden/Hecton8/DeferredCaustics");
#endif
            RecreateMaterial(ref _material, shader);
            _pass ??= new DeferredCausticsPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
                return;
            if (renderingData.cameraData.renderType != CameraRenderType.Base)
                return;

            if (!AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out _, out _))
                return;

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeMaterial(ref _material);
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                DisposeMaterial(ref material);
                return;
            }

            if (material != null && material.shader == shader)
                return;

            DisposeMaterial(ref material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        private static void DisposeMaterial(ref Material material)
        {
            if (material == null)
                return;

            CoreUtils.Destroy(material);
            material = null;
        }
    }
}
