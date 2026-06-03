using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Hecton8.Rendering
{
    public sealed class HectonDeferredCausticsFeature : ScriptableRendererFeature
    {
        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Authored fullscreen material used for screen-space deferred caustics.")]
            [FormerlySerializedAs("shader")]
            public Material material = null;

            [Tooltip("Injection point after opaque lighting and before transparent water/visor overlays.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Optional 1719-baked RGB caustic flipbook atlas. Null keeps the legacy procedural fallback.")]
            public Texture2D causticFlipbookAtlas = null;

            [Tooltip("Optional 1719-baked waterline mask. Null disables static waterline clipping.")]
            public Texture2D waterlineMask = null;

            [Range(0f, 1f)]
            [Tooltip("Continuous blend from procedural caustics to the baked flipbook atlas.")]
            public float bakedAtlasWeight = 1f;

            [Min(1)]
            [Tooltip("Flipbook columns in the baked caustic atlas.")]
            public int flipbookColumns = 8;

            [Min(1)]
            [Tooltip("Flipbook rows in the baked caustic atlas.")]
            public int flipbookRows = 8;

            [Min(1)]
            [Tooltip("Active frame count in the baked caustic atlas.")]
            public int flipbookFrames = 64;

            [Range(0f, 1f)]
            [Tooltip("Continuous weight for baked waterline clipping.")]
            public float waterlineMaskWeight = 1f;

            [Tooltip("World-space Y mapped to the bottom of the waterline mask.")]
            public float waterlineWorldMinY = -2f;

            [Tooltip("World-space Y mapped to the top of the waterline mask.")]
            public float waterlineWorldMaxY = 2f;
        }

        private sealed class DeferredCausticsPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Deferred Caustics");
            private FeatureSettings _settings;
            private Material _material;
            private MaterialPropertyBlock _propertyBlock;

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
                EnsurePropertyBlockCold();
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
                    BuildPropertyPayload(
                        _settings,
                        out passData.CausticFlipbookAtlas,
                        out passData.WaterlineMask,
                        out passData.BakedAtlasParams,
                        out passData.BakedAtlasTexelParams,
                        out passData.BakedWaterlineParams);
                    passData.PropertyBlock = _propertyBlock;

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
                        MaterialPropertyBlock properties = data.PropertyBlock;
                        if (properties == null)
                            return;

                        properties.Clear();
                        if (data.CausticFlipbookAtlas != null)
                            properties.SetTexture(AbyssalCausticsShaderIds.BakedAtlasTextureId, data.CausticFlipbookAtlas);
                        if (data.WaterlineMask != null)
                            properties.SetTexture(AbyssalCausticsShaderIds.BakedWaterlineMaskId, data.WaterlineMask);
                        properties.SetVector(AbyssalCausticsShaderIds.BakedAtlasParamsId, data.BakedAtlasParams);
                        properties.SetVector(AbyssalCausticsShaderIds.BakedAtlasTexelParamsId, data.BakedAtlasTexelParams);
                        properties.SetVector(AbyssalCausticsShaderIds.BakedWaterlineParamsId, data.BakedWaterlineParams);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, properties, 0);
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
                internal Texture CausticFlipbookAtlas;
                internal Texture WaterlineMask;
                internal Vector4 BakedAtlasParams;
                internal Vector4 BakedAtlasTexelParams;
                internal Vector4 BakedWaterlineParams;
                internal MaterialPropertyBlock PropertyBlock;
            }

            private void EnsurePropertyBlockCold()
            {
                _propertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - deferred caustics render payload - owner: HectonDeferredCausticsFeature
            }
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private DeferredCausticsPass _pass;
        private Material _material;

        public override void Create()
        {
            _material = settings != null ? settings.material : null;
            _pass ??= new DeferredCausticsPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null)
                return;

            _material = settings.material;
            if (_material == null)
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
            _material = null;
        }

        private static void BuildPropertyPayload(
            FeatureSettings settings,
            out Texture causticFlipbookAtlas,
            out Texture waterlineMask,
            out Vector4 bakedAtlasParams,
            out Vector4 bakedAtlasTexelParams,
            out Vector4 bakedWaterlineParams)
        {
            causticFlipbookAtlas = null;
            waterlineMask = null;
            bakedAtlasParams = Vector4.zero;
            bakedAtlasTexelParams = Vector4.zero;
            bakedWaterlineParams = Vector4.zero;
            if (settings == null)
                return;

            int columns = Mathf.Max(1, settings.flipbookColumns);
            int rows = Mathf.Max(1, settings.flipbookRows);
            int frames = Mathf.Clamp(settings.flipbookFrames, 1, columns * rows);
            int atlasWidth = settings.causticFlipbookAtlas != null ? Mathf.Max(1, settings.causticFlipbookAtlas.width) : columns;
            int atlasHeight = settings.causticFlipbookAtlas != null ? Mathf.Max(1, settings.causticFlipbookAtlas.height) : rows;
            float cellTexelX = columns / (float)atlasWidth;
            float cellTexelY = rows / (float)atlasHeight;
            float atlasWeight = settings.causticFlipbookAtlas != null ? Mathf.Clamp01(settings.bakedAtlasWeight) : 0f;
            causticFlipbookAtlas = settings.causticFlipbookAtlas;
            bakedAtlasParams = new Vector4(atlasWeight, columns, rows, frames);
            bakedAtlasTexelParams = new Vector4(cellTexelX, cellTexelY, 0f, 0f);

            float minY = Mathf.Min(settings.waterlineWorldMinY, settings.waterlineWorldMaxY);
            float maxY = Mathf.Max(settings.waterlineWorldMinY, settings.waterlineWorldMaxY);
            float invRange = 1f / Mathf.Max(0.001f, maxY - minY);
            float maskWeight = settings.waterlineMask != null ? Mathf.Clamp01(settings.waterlineMaskWeight) : 0f;
            waterlineMask = settings.waterlineMask;
            bakedWaterlineParams = new Vector4(maskWeight, minY, invRange, 0f);
        }
    }
}
