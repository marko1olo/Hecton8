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
    /// Opt-in reflection sheen cheat. It uses one color tap plus depth/normal masks; no ray marching.
    /// </summary>
    public sealed class HectonStochasticSsrFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_StochasticSSR.shader";
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
            [Tooltip("Hidden fullscreen shader used for deterministic reflection sheen.")]
            public Shader shader = null;

            [Tooltip("Injection point. Runs after opaques and before fogged transparents.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Composite intensity. Default is zero: this pass is opt-in because real SSR is over budget on MX350.")]
            [Range(0f, 1f)] public float intensity = 0f;

            [Tooltip("Maximum screen-space color offset in pixels.")]
            [Range(0.25f, 4f)] public float maxPixelOffset = 1.25f;

            [Tooltip("Eye-space depth where the reflection sheen fades out.")]
            [Range(5f, 120f)] public float depthFadeMeters = 55f;

            [Tooltip("Screen-edge fade multiplier.")]
            [Range(1f, 96f)] public float edgeFade = 36f;

            [Tooltip("Static IGN modulation range. Zero is flat; one is visibly dithered.")]
            [Range(0f, 1f)] public float noiseModulation = 0.35f;
        }

        private sealed class ReflectionSheenPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle Source;
                internal TextureHandle Destination;
                internal Material Material;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Reflection Sheen");
            private FeatureSettings _settings;
            private Material _material;
            private Material _lastUploadedMaterial;
            private bool _hasMaterialState;
            private Vector4 _lastInputSize;
            private Vector4 _lastParamsA;
            private Vector4 _lastParamsB;

            public ReflectionSheenPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material)
            {
                _settings = settings;
                _material = material;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);

                if (!Application.isPlaying || _settings == null || _material == null || _settings.intensity <= 0.0001f)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (IsUnsupportedCameraType(cameraData.cameraType))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                TextureHandle normalsTexture = resourceData.cameraNormalsTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid() || !normalsTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonReflectionSheenComposite";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;

                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                UpdateMaterialParameters(_material, _settings, sourceDesc.width, sourceDesc.height);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Hecton Reflection Sheen", out PassData passData, _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Destination = destinationTexture;
                    passData.Material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(normalsTexture, AccessFlags.Read);
                    builder.UseTexture(destinationTexture, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        cmd.SetGlobalFloat(ShaderConstants.ActiveId, 1f);
                        Blitter.BlitCameraTexture(cmd, data.Source, data.Destination, LoadAction, StoreAction, data.Material, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private void UpdateMaterialParameters(Material material, FeatureSettings settings, int inputWidth, int inputHeight)
            {
                Vector4 inputSize = new Vector4(inputWidth, inputHeight, 1f / math.max(1, inputWidth), 1f / math.max(1, inputHeight));
                Vector4 paramsA = new Vector4(
                    math.max(settings.maxPixelOffset, 0.25f),
                    math.max(settings.depthFadeMeters, 1f),
                    math.saturate(settings.intensity),
                    math.max(settings.edgeFade, 1f));
                Vector4 paramsB = new Vector4(math.saturate(settings.noiseModulation), 0f, 0f, 0f);

                if (_lastUploadedMaterial != material)
                {
                    _lastUploadedMaterial = material;
                    _hasMaterialState = false;
                }

                if (!_hasMaterialState || _lastInputSize != inputSize)
                {
                    material.SetVector(ShaderConstants.InputSizeId, inputSize);
                    _lastInputSize = inputSize;
                }

                if (!_hasMaterialState || _lastParamsA != paramsA)
                {
                    material.SetVector(ShaderConstants.ParamsAId, paramsA);
                    _lastParamsA = paramsA;
                }

                if (!_hasMaterialState || _lastParamsB != paramsB)
                {
                    material.SetVector(ShaderConstants.ParamsBId, paramsB);
                    _lastParamsB = paramsB;
                }

                _hasMaterialState = true;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int InputSizeId = Shader.PropertyToID("_HectonSsrInputSize");
            internal static readonly int ParamsAId = Shader.PropertyToID("_HectonSsrParamsA");
            internal static readonly int ParamsBId = Shader.PropertyToID("_HectonSsrParamsB");
            internal static readonly int ActiveId = Shader.PropertyToID("_HectonStochasticSSRActive");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ReflectionSheenPass _pass;
        private Material _material;

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            Shader shader = settings != null && settings.shader != null
                ? settings.shader
                : Shader.Find("Hidden/Hecton8/StochasticSSR");
            RecreateMaterial(ref _material, shader);
            _pass ??= new ReflectionSheenPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

            if (settings == null || _pass == null || _material == null || settings.intensity <= 0.0001f)
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
            {
                Shader.SetGlobalFloat(ShaderConstants.ActiveId, 0f);
                return;
            }

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
