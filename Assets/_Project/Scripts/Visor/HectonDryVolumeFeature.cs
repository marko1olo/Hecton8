using System;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Restores pre-underwater color inside first-party dry interiors, then applies the underwater noir resolve after post-processing.
    /// </summary>
    public sealed class HectonDryVolumeFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string StencilWriteShaderPath = "Assets/_Project/Art/Shaders/Hecton_DryVolumeStencil.shader";
        private const string RestoreShaderPath = "Assets/_Project/Art/Shaders/Hecton_DryVolumeRestore.shader";
        private const string ClearShaderPath = "Assets/_Project/Art/Shaders/Hecton_DryVolumeStencilClear.shader";
#endif
        private const string StencilWriteShaderName = "Hidden/Hecton8/DryVolumeStencil";
        private const string RestoreShaderName = "Hidden/Hecton8/DryVolumeRestore";
        private const string ClearShaderName = "Hidden/Hecton8/DryVolumeStencilClear";

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden stencil writer shader used to mark dry interiors after opaque depth exists.")]
            public Shader stencilWriteShader = null;

            [Tooltip("Hidden fullscreen shader that contains both the dry restore pass and the final underwater resolve pass.")]
            public Shader restoreShader = null;

            [Tooltip("Hidden fullscreen clear shader used to zero the dry stencil after restore or final resolve.")]
            public Shader clearShader = null;

            [Tooltip("Where the dry-volume restore runs. Must stay after the ocean underwater pass and before post-processing.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Stencil reference used by the dry-volume writer and composite passes.")]
            [Range(1, 255)] public int stencilRef = 64;
        }

        private abstract class DryVolumePassBase : ScriptableRenderPass
        {
            private sealed class ColorCopyPassData
            {
                internal TextureHandle source;
                internal Material copyMaterial;
            }

            protected static void DrawDryStencil(IRasterCommandBuffer cmd, Material material)
            {
                int sourceCount = HectonDryVolumeStencilSource.ActiveSourceCount;
                for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
                {
                    HectonDryVolumeStencilSource source = HectonDryVolumeStencilSource.GetActiveSource(sourceIndex);
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

            protected static void AddColorCopyPass(
                RenderGraph renderGraph,
                TextureHandle sourceTexture,
                TextureHandle destinationTexture,
                Material copyMaterial,
                ProfilingSampler profilingSampler,
                string passName)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ColorCopyPassData>(
                           passName,
                           out ColorCopyPassData passData,
                           profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.copyMaterial = copyMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((ColorCopyPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.source);
                        CoreUtils.DrawFullScreen(context.cmd, data.copyMaterial, null, 2);
                    });
                }
            }
        }

        private sealed class DryRestorePass : DryVolumePassBase
        {
            private sealed class StencilPassData
            {
                internal Material stencilWriteMaterial;
            }

            private sealed class RestorePassData
            {
                internal Material restoreMaterial;
                internal Material clearMaterial;
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
                    HectonDryVolumeStencilSource.ActiveSourceCount <= 0 ||
                    IsUnsupportedCamera(frameData) ||
                    !TryReadOceanCameraColorTexture(out Texture oceanCameraColorTexture))
                {
                    return;
                }

                if (AreTargetsInvalid(frameData, out TextureHandle sourceTexture, out TextureHandle depthTexture))
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonDryVolumeComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                _stencilWriteMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);
                _restoreMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);
                _restoreMaterial.SetTexture(ShaderConstants.OceanCameraColorTextureId, oceanCameraColorTexture);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<StencilPassData>(
                           "Hecton Dry Volume Stencil",
                           out StencilPassData passData,
                           _profilingSampler))
                {
                    passData.stencilWriteMaterial = _stencilWriteMaterial;

                    builder.SetRenderAttachment(sourceTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((StencilPassData data, RasterGraphContext context) =>
                    {
                        DrawDryStencil(context.cmd, data.stencilWriteMaterial);
                    });
                }

                AddColorCopyPass(
                    renderGraph,
                    sourceTexture,
                    compositeTexture,
                    _restoreMaterial,
                    _profilingSampler,
                    "Hecton Dry Volume Color Copy");

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<RestorePassData>(
                           "Hecton Dry Volume Restore",
                           out RestorePassData passData,
                           _profilingSampler))
                {
                    passData.restoreMaterial = _restoreMaterial;
                    passData.clearMaterial = _clearMaterial;

                    builder.SetRenderAttachment(compositeTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((RestorePassData data, RasterGraphContext context) =>
                    {
                        CoreUtils.DrawFullScreen(context.cmd, data.restoreMaterial, null, 0);
                        CoreUtils.DrawFullScreen(context.cmd, data.clearMaterial);
                    });
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                resourceData.cameraColor = compositeTexture;
            }
        }

        private sealed class UnderwaterResolvePass : DryVolumePassBase
        {
            private sealed class StencilPassData
            {
                internal Material stencilWriteMaterial;
            }

            private sealed class ResolvePassData
            {
                internal TextureHandle source;
                internal Material resolveMaterial;
                internal Material clearMaterial;
                internal float hasDryVolumes;
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
                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonUnderwaterNoirResolve";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                _stencilWriteMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);
                _resolveMaterial.SetFloat(ShaderConstants.StencilRefId, _settings.stencilRef);
                bool hasDryVolumes = HectonDryVolumeStencilSource.ActiveSourceCount > 0;

                if (hasDryVolumes)
                {
                    using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<StencilPassData>(
                               "Hecton Underwater Dry Stencil",
                               out StencilPassData passData,
                               _profilingSampler))
                    {
                        passData.stencilWriteMaterial = _stencilWriteMaterial;

                        builder.SetRenderAttachment(sourceTexture, 0, AccessFlags.ReadWrite);
                        builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.ReadWrite);

                        builder.SetRenderFunc((StencilPassData data, RasterGraphContext context) =>
                        {
                            DrawDryStencil(context.cmd, data.stencilWriteMaterial);
                        });
                    }
                }

                AddColorCopyPass(
                    renderGraph,
                    sourceTexture,
                    compositeTexture,
                    _resolveMaterial,
                    _profilingSampler,
                    "Hecton Underwater Noir Color Copy");

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ResolvePassData>(
                           "Hecton Underwater Noir Resolve",
                           out ResolvePassData passData,
                           _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.resolveMaterial = _resolveMaterial;
                    passData.clearMaterial = _clearMaterial;
                    passData.hasDryVolumes = hasDryVolumes ? 1f : 0f;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(compositeTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((ResolvePassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.source);
                        CoreUtils.DrawFullScreen(context.cmd, data.resolveMaterial, null, 1);

                        if (data.hasDryVolumes > 0.5f)
                            CoreUtils.DrawFullScreen(context.cmd, data.clearMaterial);
                    });
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                resourceData.cameraColor = compositeTexture;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int OceanCameraColorTextureId = Shader.PropertyToID("_OceanCameraColorTexture");
        }

        private static bool TryReadOceanCameraColorTexture(out Texture texture)
        {
            IOceanVisualBridge bridge = OceanVisualBridgeRegistry.Active;
            texture = bridge != null ? Shader.GetGlobalTexture(bridge.CameraColorTextureId) : null;
            return texture != null;
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
            EnsureEditorShaderReferences();

            Shader stencilWriteShader = settings != null ? settings.stencilWriteShader : null;
            Shader restoreShader = settings != null ? settings.restoreShader : null;
            Shader clearShader = settings != null ? settings.clearShader : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (stencilWriteShader == null)
                stencilWriteShader = Shader.Find(StencilWriteShaderName);
            if (restoreShader == null)
                restoreShader = Shader.Find(RestoreShaderName);
            if (clearShader == null)
                clearShader = Shader.Find(ClearShaderName);
#endif

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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void EnsureEditorShaderReferences()
        {
#if UNITY_EDITOR
            if (settings == null)
                return;

            if (settings.stencilWriteShader == null)
                settings.stencilWriteShader = AssetDatabase.LoadAssetAtPath<Shader>(StencilWriteShaderPath);
            if (settings.restoreShader == null)
                settings.restoreShader = AssetDatabase.LoadAssetAtPath<Shader>(RestoreShaderPath);
            if (settings.clearShader == null)
                settings.clearShader = AssetDatabase.LoadAssetAtPath<Shader>(ClearShaderPath);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureEditorShaderReferences();
        }
#endif
    }
}
