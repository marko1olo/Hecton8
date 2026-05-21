using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Opt-in reflection sheen cheat. It writes a half-res R8 mask, then composites one cheap color offset.
    /// </summary>
    public sealed class HectonStochasticSsrFeature : ScriptableRendererFeature
    {
        private const int StochasticSsrGlobalsStrideBytes = 48;

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
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Reflection Sheen");
            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _stochasticSsrGlobalsBuffer;
            private StochasticSsrGlobalsDTO _lastStochasticSsrGlobals;
            private bool _hasStochasticSsrGlobals;

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
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
                EnsureStochasticSsrGlobalsBuffer();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
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
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                if (!UpdateStochasticSsrGlobals(_settings, sourceDesc.width, sourceDesc.height))
                    return;

                TextureDesc maskDesc = new TextureDesc(sourceDesc);
                maskDesc.name = "_HectonStochasticSsrMask";
                int maskShift = FrameTimeWatchdog.CurrentMathLodMode == MathLodMode.Low ? 2 : 1;
                maskDesc.width = math.max(1, sourceDesc.width >> maskShift);
                maskDesc.height = math.max(1, sourceDesc.height >> maskShift);
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.black;
                maskDesc.depthBufferBits = DepthBits.None;
                maskDesc.msaaSamples = MSAASamples.None;
                maskDesc.colorFormat = GraphicsFormat.R8_UNorm;
                maskDesc.filterMode = FilterMode.Bilinear;
                maskDesc.useMipMap = false;
                maskDesc.autoGenerateMips = false;

                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonReflectionSheenComposite";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;

                TextureHandle maskTexture = renderGraph.CreateTexture(maskDesc);
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, maskTexture, _material, 0),
                           passName: "Hecton Reflection Sheen Mask R8 Half",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(maskTexture, ShaderConstants.MaskTextureId);
                }

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, destinationTexture, _material, 1),
                           passName: "Hecton Reflection Sheen Composite",
                           returnBuilder: true))
                {
                    builder.UseTexture(maskTexture, AccessFlags.Read);
                }

                resourceData.cameraColor = destinationTexture;
            }

            public void Dispose()
            {
                _stochasticSsrGlobalsBuffer?.Release();
                _stochasticSsrGlobalsBuffer = null;
                _hasStochasticSsrGlobals = false;
            }

            private bool EnsureStochasticSsrGlobalsBuffer()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (_stochasticSsrGlobalsBuffer != null && _stochasticSsrGlobalsBuffer.IsValid())
                    return true;

                _stochasticSsrGlobalsBuffer?.Release();
                _stochasticSsrGlobalsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    StochasticSsrGlobalsStrideBytes);
                _hasStochasticSsrGlobals = false;
                return _stochasticSsrGlobalsBuffer.IsValid();
            }

            private bool UpdateStochasticSsrGlobals(FeatureSettings settings, int inputWidth, int inputHeight)
            {
                if (!EnsureStochasticSsrGlobalsBuffer())
                    return false;

                Vector4 inputSize = new Vector4(inputWidth, inputHeight, 1f / math.max(1, inputWidth), 1f / math.max(1, inputHeight));
                Vector4 paramsA = new Vector4(
                    math.max(settings.maxPixelOffset, 0.25f),
                    math.max(settings.depthFadeMeters, 1f),
                    math.saturate(settings.intensity),
                    math.max(settings.edgeFade, 1f));
                Vector4 paramsB = new Vector4(math.saturate(settings.noiseModulation), 0f, 0f, 0f);

                StochasticSsrGlobalsDTO globals = new StochasticSsrGlobalsDTO(inputSize, paramsA, paramsB);
                if (_hasStochasticSsrGlobals && StochasticSsrGlobalsEqual(in _lastStochasticSsrGlobals, in globals))
                {
                    Shader.SetGlobalConstantBuffer(ShaderConstants.StochasticSsrGlobalsBufferId, _stochasticSsrGlobalsBuffer, 0, StochasticSsrGlobalsStrideBytes);
                    return true;
                }

                NativeArray<StochasticSsrGlobalsDTO> mapped = _stochasticSsrGlobalsBuffer.LockBufferForWrite<StochasticSsrGlobalsDTO>(0, 1);
                mapped[0] = globals;
                _stochasticSsrGlobalsBuffer.UnlockBufferAfterWrite<StochasticSsrGlobalsDTO>(1);
                _lastStochasticSsrGlobals = globals;
                _hasStochasticSsrGlobals = true;
                Shader.SetGlobalConstantBuffer(ShaderConstants.StochasticSsrGlobalsBufferId, _stochasticSsrGlobalsBuffer, 0, StochasticSsrGlobalsStrideBytes);
                return true;
            }

            private static bool StochasticSsrGlobalsEqual(in StochasticSsrGlobalsDTO left, in StochasticSsrGlobalsDTO right)
            {
                return left.InputSize == right.InputSize &&
                       left.ParamsA == right.ParamsA &&
                       left.ParamsB == right.ParamsB;
            }

            [StructLayout(LayoutKind.Explicit, Size = StochasticSsrGlobalsStrideBytes)]
            private struct StochasticSsrGlobalsDTO
            {
                [FieldOffset(0)]
                public Vector4 InputSize;

                [FieldOffset(16)]
                public Vector4 ParamsA;

                [FieldOffset(32)]
                public Vector4 ParamsB;

                public StochasticSsrGlobalsDTO(Vector4 inputSize, Vector4 paramsA, Vector4 paramsB)
                {
                    InputSize = inputSize;
                    ParamsA = paramsA;
                    ParamsB = paramsB;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int StochasticSsrGlobalsBufferId = Shader.PropertyToID("HectonStochasticSsrGlobals");
            internal static readonly int MaskTextureId = Shader.PropertyToID("_HectonSsrMaskTex");
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
                return;

            if (settings == null || _pass == null || _material == null || settings.intensity <= 0.0001f)
                return;

            if (IsUnsupportedCameraType(renderingData.cameraData.cameraType))
                return;

            _pass.Setup(settings, _material);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
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
