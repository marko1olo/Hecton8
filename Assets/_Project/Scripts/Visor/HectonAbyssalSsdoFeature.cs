using System;
using Unity.Mathematics;
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
    /// Lightweight directional screen-space occlusion for abyssal caves and base interiors.
    /// Runs before transparents so water/visor/sonar composites stay on top.
    /// </summary>
    public sealed class HectonAbyssalSsdoFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_AbyssalSSDO.shader";
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
            [Tooltip("Hidden fullscreen shader used for occlusion, bilateral blur, and composite.")]
            public Shader shader = null;

            [Tooltip("Where the SSDO composite is injected. Before transparents keeps water and visor overlays untouched.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Internal render scale for the occlusion target.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("World-space occlusion radius in meters.")]
            [Range(0.25f, 3f)] public float radiusMeters = 1.5f;

            [Tooltip("Overall occlusion strength.")]
            [Range(0f, 2f)] public float intensity = 0.78f;

            [Tooltip("Bias applied to reduce self-occlusion acne.")]
            [Range(0f, 0.3f)] public float bias = 0.05f;

            [Tooltip("Depth rejection slope for the directional gather.")]
            [Range(1f, 96f)] public float depthSigma = 18f;

            [Tooltip("Hard depth rejection threshold in eye-space meters for bilateral cleanup.")]
            [Range(0.01f, 2f)] public float blurDepthThreshold = 0.18f;

            [Tooltip("Composite weight applied to the camera color.")]
            [Range(0f, 1f)] public float compositeStrength = 0.52f;
        }

        private sealed class AbyssalSsdoPass : ScriptableRenderPass
        {
            private const float MaterialFloatEpsilon = 0.0001f;
            private const float MaterialVectorEpsilonSq = 0.0000001f;

            private sealed class FullscreenPassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal Material material;
            }

            private struct MaterialParameterCache
            {
                internal Material Material;
                internal Vector4 InputSize;
                internal Vector4 OutputSize;
                internal float PassMode;
                internal float RadiusMeters;
                internal float Intensity;
                internal float Bias;
                internal float DepthSigma;
                internal float BlurDepthThreshold;
                internal float CompositeStrength;
                internal float ProjectionScale;
                internal bool Applied;
            }

            private sealed class CompositePassData
            {
                internal TextureHandle source;
                internal TextureHandle occlusion;
                internal TextureHandle destination;
                internal Material compositeMaterial;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Abyssal SSDO");
            private FeatureSettings _settings;
            private Material _occlusionMaterial;
            private Material _blurHorizontalMaterial;
            private Material _blurVerticalMaterial;
            private Material _compositeMaterial;
            private MaterialParameterCache _occlusionParameterCache;
            private MaterialParameterCache _blurHorizontalParameterCache;
            private MaterialParameterCache _blurVerticalParameterCache;
            private MaterialParameterCache _compositeParameterCache;

            public AbyssalSsdoPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                Material occlusionMaterial,
                Material blurHorizontalMaterial,
                Material blurVerticalMaterial,
                Material compositeMaterial)
            {
                _settings = settings;
                _occlusionMaterial = occlusionMaterial;
                _blurHorizontalMaterial = blurHorizontalMaterial;
                _blurVerticalMaterial = blurVerticalMaterial;
                _compositeMaterial = compositeMaterial;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);

                if (!Application.isPlaying)
                    return;

                if (_settings == null ||
                    _occlusionMaterial == null ||
                    _blurHorizontalMaterial == null ||
                    _blurVerticalMaterial == null ||
                    _compositeMaterial == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (IsUnsupportedCameraType(cameraType))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                float renderScale = math.clamp(_settings.renderScale, 0.25f, 1f);
                int ssdoWidth = math.max(1, (int)(sourceDesc.width * renderScale + 0.5f));
                int ssdoHeight = math.max(1, (int)(sourceDesc.height * renderScale + 0.5f));

                TextureDesc occlusionDesc = new TextureDesc(sourceDesc);
                occlusionDesc.name = "_HectonAbyssalSSDO";
                occlusionDesc.width = ssdoWidth;
                occlusionDesc.height = ssdoHeight;
                occlusionDesc.depthBufferBits = DepthBits.None;
                occlusionDesc.msaaSamples = MSAASamples.None;
                occlusionDesc.colorFormat = GraphicsFormat.R8_UNorm;
                occlusionDesc.clearBuffer = true;
                occlusionDesc.clearColor = Color.white;
                occlusionDesc.filterMode = FilterMode.Bilinear;
                occlusionDesc.useMipMap = false;
                occlusionDesc.autoGenerateMips = false;

                TextureDesc blurDesc = new TextureDesc(occlusionDesc);
                blurDesc.name = "_HectonAbyssalSSDOBLur";

                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonAbyssalSSDOComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                compositeDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;

                TextureHandle occlusionTexture = renderGraph.CreateTexture(occlusionDesc);
                TextureHandle blurTexture = renderGraph.CreateTexture(blurDesc);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                Camera camera = cameraData.camera;
                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
                float projectionScale = math.abs(projectionMatrix.m11) * 0.5f * sourceDesc.height * math.max(0.01f, _settings.radiusMeters);

                UpdateMaterialParameters(
                    _occlusionMaterial,
                    ref _occlusionParameterCache,
                    _settings,
                    0f,
                    sourceDesc,
                    ssdoWidth,
                    ssdoHeight,
                    projectionScale);
                UpdateMaterialParameters(
                    _blurHorizontalMaterial,
                    ref _blurHorizontalParameterCache,
                    _settings,
                    1f,
                    sourceDesc,
                    ssdoWidth,
                    ssdoHeight,
                    projectionScale);
                UpdateMaterialParameters(
                    _blurVerticalMaterial,
                    ref _blurVerticalParameterCache,
                    _settings,
                    2f,
                    sourceDesc,
                    ssdoWidth,
                    ssdoHeight,
                    projectionScale);
                UpdateMaterialParameters(
                    _compositeMaterial,
                    ref _compositeParameterCache,
                    _settings,
                    3f,
                    sourceDesc,
                    ssdoWidth,
                    ssdoHeight,
                    projectionScale);

                using (var builder = renderGraph.AddUnsafePass<FullscreenPassData>("Hecton Abyssal SSDO Gather", out var passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.destination = occlusionTexture;
                    passData.material = _occlusionMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(occlusionTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (FullscreenPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.material, 0);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<FullscreenPassData>("Hecton Abyssal SSDO Blur Horizontal", out var passData, _profilingSampler))
                {
                    passData.source = occlusionTexture;
                    passData.destination = blurTexture;
                    passData.material = _blurHorizontalMaterial;

                    builder.UseTexture(occlusionTexture, AccessFlags.Read);
                    builder.UseTexture(blurTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (FullscreenPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.material, 1);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<FullscreenPassData>("Hecton Abyssal SSDO Blur Vertical", out var passData, _profilingSampler))
                {
                    passData.source = blurTexture;
                    passData.destination = occlusionTexture;
                    passData.material = _blurVerticalMaterial;

                    builder.UseTexture(blurTexture, AccessFlags.Read);
                    builder.UseTexture(occlusionTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetGlobalTextureAfterPass(occlusionTexture, ShaderConstants.SsdoTextureId);

                    builder.SetRenderFunc(static (FullscreenPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.material, 2);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<CompositePassData>("Hecton Abyssal SSDO Composite", out var passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.occlusion = occlusionTexture;
                    passData.destination = compositeTexture;
                    passData.compositeMaterial = _compositeMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(occlusionTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (CompositePassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        cmd.SetGlobalFloat(ShaderConstants.ActiveId, 1f);
                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.compositeMaterial, 3);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private static void UpdateMaterialParameters(
                Material material,
                ref MaterialParameterCache cache,
                FeatureSettings settings,
                float passMode,
                TextureDesc sourceDesc,
                int outputWidth,
                int outputHeight,
                float projectionScale)
            {
                bool materialDirty = !cache.Applied || !ReferenceEquals(cache.Material, material);
                if (materialDirty)
                {
                    cache.Material = material;
                    cache.Applied = true;
                }

                Vector4 inputSize = new Vector4(
                    sourceDesc.width,
                    sourceDesc.height,
                    1f / math.max(1, sourceDesc.width),
                    1f / math.max(1, sourceDesc.height));
                Vector4 outputSize = new Vector4(
                    outputWidth,
                    outputHeight,
                    1f / math.max(1, outputWidth),
                    1f / math.max(1, outputHeight));
                float radiusMeters = math.max(0.01f, settings.radiusMeters);
                float intensity = math.max(0f, settings.intensity);
                float bias = math.max(0f, settings.bias);
                float depthSigma = math.max(0.01f, settings.depthSigma);
                float blurDepthThreshold = math.max(0.001f, settings.blurDepthThreshold);
                float compositeStrength = math.saturate(settings.compositeStrength);
                float safeProjectionScale = math.max(0.01f, projectionScale);

                SetMaterialFloatIfChanged(material, ShaderConstants.PassModeId, passMode, ref cache.PassMode, materialDirty);
                SetMaterialVectorIfChanged(material, ShaderConstants.InputSizeId, inputSize, ref cache.InputSize, materialDirty);
                SetMaterialVectorIfChanged(material, ShaderConstants.OutputSizeId, outputSize, ref cache.OutputSize, materialDirty);
                SetMaterialFloatIfChanged(material, ShaderConstants.RadiusMetersId, radiusMeters, ref cache.RadiusMeters, materialDirty);
                SetMaterialFloatIfChanged(material, ShaderConstants.IntensityId, intensity, ref cache.Intensity, materialDirty);
                SetMaterialFloatIfChanged(material, ShaderConstants.BiasId, bias, ref cache.Bias, materialDirty);
                SetMaterialFloatIfChanged(material, ShaderConstants.DepthSigmaId, depthSigma, ref cache.DepthSigma, materialDirty);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.BlurDepthThresholdId,
                    blurDepthThreshold,
                    ref cache.BlurDepthThreshold,
                    materialDirty);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.CompositeStrengthId,
                    compositeStrength,
                    ref cache.CompositeStrength,
                    materialDirty);
                SetMaterialFloatIfChanged(
                    material,
                    ShaderConstants.ProjectionScaleId,
                    safeProjectionScale,
                    ref cache.ProjectionScale,
                    materialDirty);
            }

            private static void SetMaterialFloatIfChanged(Material material, int shaderId, float value, ref float cachedValue, bool materialDirty)
            {
                if (!materialDirty && math.abs(cachedValue - value) <= MaterialFloatEpsilon)
                    return;

                material.SetFloat(shaderId, value);
                cachedValue = value;
            }

            private static void SetMaterialVectorIfChanged(Material material, int shaderId, Vector4 value, ref Vector4 cachedValue, bool materialDirty)
            {
                if (!materialDirty && Vector4DistanceSq(cachedValue, value) <= MaterialVectorEpsilonSq)
                    return;

                material.SetVector(shaderId, value);
                cachedValue = value;
            }

            private static float Vector4DistanceSq(Vector4 a, Vector4 b)
            {
                float x = a.x - b.x;
                float y = a.y - b.y;
                float z = a.z - b.z;
                float w = a.w - b.w;
                return x * x + y * y + z * z + w * w;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int PassModeId = Shader.PropertyToID("_HectonAbyssalSsdoPassMode");
            internal static readonly int InputSizeId = Shader.PropertyToID("_HectonAbyssalSsdoInputSize");
            internal static readonly int OutputSizeId = Shader.PropertyToID("_HectonAbyssalSsdoOutputSize");
            internal static readonly int RadiusMetersId = Shader.PropertyToID("_HectonAbyssalSsdoRadiusMeters");
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonAbyssalSsdoIntensity");
            internal static readonly int BiasId = Shader.PropertyToID("_HectonAbyssalSsdoBias");
            internal static readonly int DepthSigmaId = Shader.PropertyToID("_HectonAbyssalSsdoDepthSigma");
            internal static readonly int BlurDepthThresholdId = Shader.PropertyToID("_HectonAbyssalSsdoBlurDepthThreshold");
            internal static readonly int ProjectionScaleId = Shader.PropertyToID("_HectonAbyssalSsdoProjectionScale");
            internal static readonly int CompositeStrengthId = Shader.PropertyToID("_HectonAbyssalSsdoCompositeStrength");
            internal static readonly int SsdoTextureId = Shader.PropertyToID("_HectonAbyssalSSDOTex");
            internal static readonly int ActiveId = Shader.PropertyToID("_HectonAbyssalSSDOActive");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private AbyssalSsdoPass _pass;
        private Material _occlusionMaterial;
        private Material _blurHorizontalMaterial;
        private Material _blurVerticalMaterial;
        private Material _compositeMaterial;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null && settings.shader != null
                ? settings.shader
                : Shader.Find("Hidden/Hecton8/AbyssalSSDO");
            _pass ??= new AbyssalSsdoPass();
            RecreateMaterial(ref _occlusionMaterial, shader);
            RecreateMaterial(ref _blurHorizontalMaterial, shader);
            RecreateMaterial(ref _blurVerticalMaterial, shader);
            RecreateMaterial(ref _compositeMaterial, shader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

            if (settings == null ||
                _pass == null ||
                _occlusionMaterial == null ||
                _blurHorizontalMaterial == null ||
                _blurVerticalMaterial == null ||
                _compositeMaterial == null)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (IsUnsupportedCameraType(cameraType))
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

            _pass.Setup(settings, _occlusionMaterial, _blurHorizontalMaterial, _blurVerticalMaterial, _compositeMaterial);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_occlusionMaterial);
            CoreUtils.Destroy(_blurHorizontalMaterial);
            CoreUtils.Destroy(_blurVerticalMaterial);
            CoreUtils.Destroy(_compositeMaterial);
            _occlusionMaterial = null;
            _blurHorizontalMaterial = null;
            _blurVerticalMaterial = null;
            _compositeMaterial = null;
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
