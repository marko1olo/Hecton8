using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// Restores pre-underwater color inside first-party dry interiors, then applies the underwater noir resolve after post-processing.
    /// </summary>
    public sealed class HectonDryVolumeFeature : ScriptableRendererFeature
    {
        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden stencil writer shader used to mark dry interiors after opaque depth exists.")]
            public Shader stencilWriteShader = null;

            [Tooltip("Hidden fullscreen shader that contains both the dry restore pass and the final underwater resolve pass.")]
            public Shader restoreShader = null;

            [Tooltip("Hidden fullscreen clear shader used to zero the dry stencil after restore or final resolve.")]
            public Shader clearShader = null;

            [Tooltip("Where the dry-volume restore runs. Must stay after Crest underwater and before post-processing.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Stencil reference used by the dry-volume writer and composite passes.")]
            [Range(1, 255)] public int stencilRef = 64;
        }

        private abstract class DryVolumePassBase : ScriptableRenderPass
        {
            protected static void DrawDryStencil(CommandBuffer cmd, Material material)
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

            protected static bool IsUnsupportedCamera(ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                return cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection;
            }

            protected static bool AreTargetsInvalid(ContextContainer frameData, out TextureHandle sourceTexture, out TextureHandle depthTexture)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                sourceTexture = resourceData.activeColorTexture;
                depthTexture = resourceData.activeDepthTexture;
                return resourceData.isActiveTargetBackBuffer || !sourceTexture.IsValid() || !depthTexture.IsValid();
            }
        }

        private sealed class DryRestorePass : DryVolumePassBase
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

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Dry Volume Restore");
            private FeatureSettings _settings;
            private Material _stencilWriteMaterial;
            private Material _restoreMaterial;
            private Material _clearMaterial;

            public DryRestorePass()
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
                    HectonDryVolumeStencilSource.ActiveSources.Count <= 0 ||
                    IsUnsupportedCamera(frameData) ||
                    Shader.GetGlobalTexture(ShaderConstants.CrestCameraColorTextureId) == null)
                {
                    return;
                }

                if (AreTargetsInvalid(frameData, out TextureHandle sourceTexture, out TextureHandle depthTexture))
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

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Dry Volume Restore", out PassData passData, _profilingSampler))
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

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        data.restoreMaterial.SetFloat(ShaderConstants.StencilRefId, data.stencilRef);

                        CoreUtils.SetRenderTarget(cmd, data.source, data.depth, ClearFlag.None);
                        DrawDryStencil(cmd, data.stencilWriteMaterial);

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, 0f, true);

                        CoreUtils.SetRenderTarget(cmd, data.destination, data.depth, ClearFlag.None);
                        CoreUtils.DrawFullScreen(cmd, data.restoreMaterial, null, 0);
                        CoreUtils.DrawFullScreen(cmd, data.clearMaterial);
                    });
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                resourceData.cameraColor = compositeTexture;
            }
        }

        private sealed class UnderwaterResolvePass : DryVolumePassBase
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle destination;
                internal Material stencilWriteMaterial;
                internal Material resolveMaterial;
                internal Material clearMaterial;
                internal float hasDryVolumes;
                internal int stencilRef;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Underwater Noir Resolve");
            private FeatureSettings _settings;
            private Material _stencilWriteMaterial;
            private Material _resolveMaterial;
            private Material _clearMaterial;

            public UnderwaterResolvePass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                Material stencilWriteMaterial,
                Material resolveMaterial,
                Material clearMaterial)
            {
                _settings = settings;
                _stencilWriteMaterial = stencilWriteMaterial;
                _resolveMaterial = resolveMaterial;
                _clearMaterial = clearMaterial;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _stencilWriteMaterial == null ||
                    _resolveMaterial == null ||
                    _clearMaterial == null ||
                    IsUnsupportedCamera(frameData))
                {
                    return;
                }

                if (AreTargetsInvalid(frameData, out TextureHandle sourceTexture, out TextureHandle depthTexture))
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonUnderwaterNoirResolve";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                _stencilWriteMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);
                _resolveMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Underwater Noir Resolve", out PassData passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.destination = compositeTexture;
                    passData.stencilWriteMaterial = _stencilWriteMaterial;
                    passData.resolveMaterial = _resolveMaterial;
                    passData.clearMaterial = _clearMaterial;
                    passData.hasDryVolumes = HectonDryVolumeStencilSource.ActiveSources.Count > 0 ? 1f : 0f;
                    passData.stencilRef = _settings.stencilRef;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        data.resolveMaterial.SetFloat(ShaderConstants.StencilRefId, data.stencilRef);

                        if (data.hasDryVolumes > 0.5f)
                        {
                            CoreUtils.SetRenderTarget(cmd, data.source, data.depth, ClearFlag.None);
                            DrawDryStencil(cmd, data.stencilWriteMaterial);
                        }

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, 0f, true);

                        CoreUtils.SetRenderTarget(cmd, data.destination, data.depth, ClearFlag.None);
                        CoreUtils.DrawFullScreen(cmd, data.resolveMaterial, null, 1);

                        if (data.hasDryVolumes > 0.5f)
                            CoreUtils.DrawFullScreen(cmd, data.clearMaterial);
                    });
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                resourceData.cameraColor = compositeTexture;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
            internal static readonly int CrestCameraColorTextureId = Shader.PropertyToID("_Crest_CameraColorTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DryRestorePass _restorePass;
        private UnderwaterResolvePass _resolvePass;
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

            _restorePass ??= new DryRestorePass();
            _resolvePass ??= new UnderwaterResolvePass();
            RecreateMaterial(ref _stencilWriteMaterial, stencilWriteShader);
            RecreateMaterial(ref _restoreMaterial, restoreShader);
            RecreateMaterial(ref _clearMaterial, clearShader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null ||
                _restorePass == null ||
                _resolvePass == null ||
                _stencilWriteMaterial == null ||
                _restoreMaterial == null ||
                _clearMaterial == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            _restorePass.Setup(settings, _stencilWriteMaterial, _restoreMaterial, _clearMaterial);
            _resolvePass.Setup(settings, _stencilWriteMaterial, _restoreMaterial, _clearMaterial);
            renderer.EnqueuePass(_restorePass);
            renderer.EnqueuePass(_resolvePass);
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
