using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// Restores pre-underwater color inside first-party dry interiors after Crest underwater fog has already rendered.
    /// </summary>
    public sealed class HectonDryVolumeFeature : ScriptableRendererFeature
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden stencil writer shader used to mark dry interiors after opaque depth exists.")]
            public Shader stencilWriteShader;

            [Tooltip("Hidden fullscreen restore shader. Samples Crest pre-underwater color and restores it only where dry stencil is set.")]
            public Shader restoreShader;

            [Tooltip("Hidden fullscreen clear shader used to zero the dry stencil after restore.")]
            public Shader clearShader;

            [Tooltip("Where the dry-volume restore runs. Must stay after Crest underwater and before post-processing.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Stencil reference used by the dry-volume writer and restore passes.")]
            [Range(1, 255)] public int stencilRef = 64;
        }

        private sealed class DryVolumePass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle destination;
                internal Material stencilWriteMaterial;
                internal Material restoreMaterial;
                internal Material clearMaterial;
                internal int stencilRef;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Dry Volume");
            private FeatureSettings _settings;
            private Material _stencilWriteMaterial;
            private Material _restoreMaterial;
            private Material _clearMaterial;

            public DryVolumePass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                Material stencilWriteMaterial,
                Material restoreMaterial,
                Material clearMaterial)
            {
                _settings = settings;
                _stencilWriteMaterial = stencilWriteMaterial;
                _restoreMaterial = restoreMaterial;
                _clearMaterial = clearMaterial;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _stencilWriteMaterial == null ||
                    _restoreMaterial == null ||
                    _clearMaterial == null ||
                    HectonDryVolumeStencilSource.ActiveSources.Count <= 0)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                    return;

                if (Shader.GetGlobalTexture(ShaderConstants.CrestCameraColorTextureId) == null)
                {
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonDryVolumeComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                _stencilWriteMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);
                _restoreMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Dry Volume", out PassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.destination = compositeTexture;
                    passData.stencilWriteMaterial = _stencilWriteMaterial;
                    passData.restoreMaterial = _restoreMaterial;
                    passData.clearMaterial = _clearMaterial;
                    passData.stencilRef = _settings.stencilRef;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        data.restoreMaterial.SetFloat(ShaderConstants.StencilRefId, data.stencilRef);

                        CoreUtils.SetRenderTarget(cmd, data.source, data.depth, ClearFlag.None);
                        DrawDryStencil(cmd, data.stencilWriteMaterial);

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, 0f, true);

                        CoreUtils.SetRenderTarget(cmd, data.destination, data.depth, ClearFlag.None);
                        CoreUtils.DrawFullScreen(cmd, data.restoreMaterial);
                        CoreUtils.DrawFullScreen(cmd, data.clearMaterial);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private static void DrawDryStencil(CommandBuffer cmd, Material material)
            {
                int sourceCount = HectonDryVolumeStencilSource.ActiveSources.Count;
                for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
                {
                    HectonDryVolumeStencilSource source = HectonDryVolumeStencilSource.ActiveSources[sourceIndex];
                    if (source == null || !source.isActiveAndEnabled)
                        continue;

                    int entryCount = source.EntryCount;
                    for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
                    {
                        if (!source.TryGetEntry(entryIndex, out Renderer renderer, out int subMeshCount) ||
                            renderer == null ||
                            !renderer.enabled ||
                            !renderer.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                            cmd.DrawRenderer(renderer, material, subMeshIndex, 0);
                    }
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
            internal static readonly int CrestCameraColorTextureId = Shader.PropertyToID("_Crest_CameraColorTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DryVolumePass _pass;
        private Material _stencilWriteMaterial;
        private Material _restoreMaterial;
        private Material _clearMaterial;

        /// <inheritdoc />
        public override void Create()
        {
            Shader stencilWriteShader = settings != null && settings.stencilWriteShader != null
                ? settings.stencilWriteShader
                : Shader.Find("Hidden/Hecton8/DryVolumeStencil");
            Shader restoreShader = settings != null && settings.restoreShader != null
                ? settings.restoreShader
                : Shader.Find("Hidden/Hecton8/DryVolumeRestore");
            Shader clearShader = settings != null && settings.clearShader != null
                ? settings.clearShader
                : Shader.Find("Hidden/Hecton8/DryVolumeStencilClear");

            _pass ??= new DryVolumePass();
            RecreateMaterial(ref _stencilWriteMaterial, stencilWriteShader);
            RecreateMaterial(ref _restoreMaterial, restoreShader);
            RecreateMaterial(ref _clearMaterial, clearShader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null ||
                _pass == null ||
                _stencilWriteMaterial == null ||
                _restoreMaterial == null ||
                _clearMaterial == null ||
                HectonDryVolumeStencilSource.ActiveSources.Count <= 0)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            _pass.Setup(settings, _stencilWriteMaterial, _restoreMaterial, _clearMaterial);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_stencilWriteMaterial);
            CoreUtils.Destroy(_restoreMaterial);
            CoreUtils.Destroy(_clearMaterial);
            _stencilWriteMaterial = null;
            _restoreMaterial = null;
            _clearMaterial = null;
        }

        private static void RecreateMaterial(ref Material material, Shader shader)
        {
            if (shader == null)
            {
                CoreUtils.Destroy(material);
                material = null;
                return;
            }

            if (material != null && material.shader == shader)
                return;

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }
    }
}
