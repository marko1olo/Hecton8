using System;
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

            [Tooltip("Optional blue-noise texture used to rotate the 4-tap kernel.")]
            public Texture2D blueNoiseTexture = null;

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

            [Tooltip("Number of rotated taps used by the directional gather.")]
            [Range(4, 6)] public int sampleCount = 4;

            [Tooltip("Ambient direction used for directional darkening in world space.")]
            public Vector3 ambientDirection = new Vector3(0.18f, 0.94f, 0.26f);
        }

        private sealed class AbyssalSsdoPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle normals;
                internal TextureHandle occlusion;
                internal TextureHandle blur;
                internal TextureHandle destination;
                internal Material occlusionMaterial;
                internal Material blurHorizontalMaterial;
                internal Material blurVerticalMaterial;
                internal Material compositeMaterial;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Abyssal SSDO");
            private FeatureSettings _settings;
            private Material _occlusionMaterial;
            private Material _blurHorizontalMaterial;
            private Material _blurVerticalMaterial;
            private Material _compositeMaterial;

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
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);

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
                TextureHandle normalsTexture = resourceData.cameraNormalsTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid() || !normalsTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                int ssdoWidth = Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));
                int ssdoHeight = Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));

                TextureDesc occlusionDesc = new TextureDesc(sourceDesc);
                occlusionDesc.name = "_HectonAbyssalSSDO";
                occlusionDesc.width = ssdoWidth;
                occlusionDesc.height = ssdoHeight;
                occlusionDesc.depthBufferBits = DepthBits.None;
                occlusionDesc.msaaSamples = MSAASamples.None;
                occlusionDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
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
                float projectionScale = Mathf.Abs(projectionMatrix.m11) * 0.5f * sourceDesc.height * Mathf.Max(0.01f, _settings.radiusMeters);
                Vector3 ambientDirection = _settings.ambientDirection.sqrMagnitude > 0.0001f
                    ? _settings.ambientDirection.normalized
                    : Vector3.up;

                UpdateMaterialParameters(_occlusionMaterial, _settings, 0f, sourceDesc, ssdoWidth, ssdoHeight, projectionScale, ambientDirection);
                UpdateMaterialParameters(_blurHorizontalMaterial, _settings, 1f, sourceDesc, ssdoWidth, ssdoHeight, projectionScale, ambientDirection);
                UpdateMaterialParameters(_blurVerticalMaterial, _settings, 2f, sourceDesc, ssdoWidth, ssdoHeight, projectionScale, ambientDirection);
                UpdateMaterialParameters(_compositeMaterial, _settings, 3f, sourceDesc, ssdoWidth, ssdoHeight, projectionScale, ambientDirection);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Abyssal SSDO", out var passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.normals = normalsTexture;
                    passData.occlusion = occlusionTexture;
                    passData.blur = blurTexture;
                    passData.destination = compositeTexture;
                    passData.occlusionMaterial = _occlusionMaterial;
                    passData.blurHorizontalMaterial = _blurHorizontalMaterial;
                    passData.blurVerticalMaterial = _blurVerticalMaterial;
                    passData.compositeMaterial = _compositeMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(normalsTexture, AccessFlags.Read);
                    builder.UseTexture(occlusionTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(blurTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        cmd.SetGlobalTexture(ShaderConstants.DepthTextureId, data.depth);
                        cmd.SetGlobalTexture(ShaderConstants.NormalsTextureId, data.normals);

                        Blitter.BlitCameraTexture(cmd, data.source, data.occlusion, LoadAction, StoreAction, data.occlusionMaterial, 0);
                        Blitter.BlitCameraTexture(cmd, data.occlusion, data.blur, LoadAction, StoreAction, data.blurHorizontalMaterial, 1);
                        Blitter.BlitCameraTexture(cmd, data.blur, data.occlusion, LoadAction, StoreAction, data.blurVerticalMaterial, 2);
                        cmd.SetGlobalTexture(ShaderConstants.SsdoTextureId, data.occlusion);
                        cmd.SetGlobalFloat(ShaderConstants.ActiveId, 1f);
                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.compositeMaterial, 3);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private static void UpdateMaterialParameters(
                Material material,
                FeatureSettings settings,
                float passMode,
                TextureDesc sourceDesc,
                int outputWidth,
                int outputHeight,
                float projectionScale,
                Vector3 ambientDirection)
            {
                material.SetFloat(ShaderConstants.PassModeId, passMode);
                material.SetVector(ShaderConstants.InputSizeId, new Vector4(
                    sourceDesc.width,
                    sourceDesc.height,
                    1f / Mathf.Max(1, sourceDesc.width),
                    1f / Mathf.Max(1, sourceDesc.height)));
                material.SetVector(ShaderConstants.OutputSizeId, new Vector4(
                    outputWidth,
                    outputHeight,
                    1f / Mathf.Max(1, outputWidth),
                    1f / Mathf.Max(1, outputHeight)));
                material.SetFloat(ShaderConstants.RadiusMetersId, Mathf.Max(0.01f, settings.radiusMeters));
                material.SetFloat(ShaderConstants.IntensityId, Mathf.Max(0f, settings.intensity));
                material.SetFloat(ShaderConstants.BiasId, Mathf.Max(0f, settings.bias));
                material.SetFloat(ShaderConstants.DepthSigmaId, Mathf.Max(0.01f, settings.depthSigma));
                material.SetFloat(ShaderConstants.BlurDepthThresholdId, Mathf.Max(0.001f, settings.blurDepthThreshold));
                material.SetFloat(ShaderConstants.CompositeStrengthId, Mathf.Clamp01(settings.compositeStrength));
                material.SetFloat(ShaderConstants.ProjectionScaleId, Mathf.Max(0.01f, projectionScale));
                material.SetInt(ShaderConstants.SampleCountId, Mathf.Clamp(settings.sampleCount, 4, 6));
                material.SetVector(ShaderConstants.AmbientDirectionId, new Vector4(ambientDirection.x, ambientDirection.y, ambientDirection.z, 0f));
                material.SetFloat(ShaderConstants.HasBlueNoiseTextureId, settings.blueNoiseTexture != null ? 1f : 0f);
                material.SetTexture(ShaderConstants.BlueNoiseTextureId, settings.blueNoiseTexture);
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
            internal static readonly int SampleCountId = Shader.PropertyToID("_HectonAbyssalSsdoSampleCount");
            internal static readonly int AmbientDirectionId = Shader.PropertyToID("_HectonAbyssalSsdoAmbientDirection");
            internal static readonly int BlueNoiseTextureId = Shader.PropertyToID("_BlueNoiseTex");
            internal static readonly int HasBlueNoiseTextureId = Shader.PropertyToID("_HectonAbyssalSsdoHasBlueNoise");
            internal static readonly int DepthTextureId = Shader.PropertyToID("_HectonAbyssalSsdoDepth");
            internal static readonly int NormalsTextureId = Shader.PropertyToID("_HectonAbyssalSsdoNormals");
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
