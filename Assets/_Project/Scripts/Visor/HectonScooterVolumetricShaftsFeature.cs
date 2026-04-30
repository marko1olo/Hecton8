using System;
using Hecton8.Core;
using Unity.Collections;
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
    /// Underwater noir post stack: half-res volumetric shafts, procedural lens ghosts, and GPU-side auto exposure.
    /// </summary>
    public sealed class HectonScooterVolumetricShaftsFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_ScooterVolumetricShafts.shader";
#endif
        private const string AutoExposureComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_NoirAutoExposure.compute";
        private const int HistogramBinCount = 64;
        private const float ExposureStateDefaultMultiplier = 1f;
        private static readonly Color DefaultNoirLiftFloor = new Color(0.01f, 0.012f, 0.016f, 1f);

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden multi-pass shader used for the shaft raymarch, bilateral upsample, lens ghosts, and final composite.")]
            public Shader shader = null;

            [Tooltip("GPU histogram compute shader used to resolve weighted EV and temporal exposure smoothing.")]
            public ComputeShader autoExposureComputeShader = null;

            [Tooltip("Optional blue-noise texture used to jitter raymarch steps. Leave null to fall back to procedural noise.")]
            public Texture2D blueNoiseTexture = null;

            [Tooltip("Where the volumetric shaft pass is injected into URP. Before transparents keeps Crest water and camera-space UI on top of the shaft composite.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Internal render scale for the shaft target. Lower values save MX350 fill-rate.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Raymarch step count for the underwater shaft volume. Fixed to the MX350-safe 8-step path.")]
            [Range(8, 8)] public int raymarchSteps = 8;

            [Tooltip("Maximum volumetric march distance in meters.")]
            [Range(8f, 120f)] public float maxRayDistance = 56f;

            [Tooltip("Forward-scattering anisotropy for the shaft phase function.")]
            [Range(0f, 0.95f)] public float scatteringAnisotropy = 0.68f;

            [Tooltip("Base water density used for light accumulation.")]
            [Range(0f, 4f)] public float density = 1.05f;

            [Tooltip("Amount of blue-noise jitter applied to the raymarch start position.")]
            [Range(0f, 1f)] public float blueNoiseJitter = 0.85f;

            [Tooltip("Edge-preserving bilateral depth falloff used during blur and upsample.")]
            [Range(0.1f, 128f)] public float bilateralDepthSigma = 24f;

            [Tooltip("Overall shaft brightness multiplier.")]
            [Range(0f, 6f)] public float shaftIntensity = 1.3f;

            [Tooltip("World-space scale of the abyssal biolum floor projection.")]
            [Range(0.01f, 2f)] public float biolumPatternScale = 0.14f;

            [Tooltip("How much floor biolum energy is projected back onto opaque seabed geometry.")]
            [Range(0f, 3f)] public float biolumProjectionStrength = 0.62f;

            [Tooltip("Strength of suspended silt inside the scooter headlight cone.")]
            [Range(0f, 4f)] public float siltStrength = 1.15f;

            [Tooltip("World-space scale of the silt noise field.")]
            [Range(0.02f, 1f)] public float siltNoiseScale = 0.14f;

            [Tooltip("How much denser the silt becomes near the hit surface or seabed.")]
            [Range(0f, 4f)] public float siltFloorBoost = 1.35f;

            [Tooltip("Temporal drift speed of the suspended silt field.")]
            [Range(0f, 2f)] public float siltDriftSpeed = 0.18f;

            [Tooltip("Screen-space contact shadow strength applied to headlight-lit opaque pixels.")]
            [Range(0f, 1f)] public float contactShadowStrength = 0.62f;

            [Tooltip("Number of depth raymarch steps used for screen-space contact shadows.")]
            [Range(4, 8)] public int contactShadowSteps = 6;

            [Tooltip("World-space bias used to prevent self-shadow acne in the contact shadow march.")]
            [Range(0.01f, 0.5f)] public float contactShadowBias = 0.08f;

            [Tooltip("Maximum world-space reach of the contact shadow test toward the headlight sources.")]
            [Range(1f, 24f)] public float contactShadowMaxDistance = 9f;

            [Tooltip("Soft-shadow k factor for flashlight voxel-SDF raymarching. Higher values tighten the penumbra.")]
            [Range(2f, 12f)] public float flashlightShadowSoftness = 6.5f;

            [Tooltip("Minimum world-space step size used by the flashlight voxel-SDF raymarch.")]
            [Range(0.02f, 0.5f)] public float flashlightShadowMinStep = 0.12f;

            [Tooltip("World-space bias applied when launching flashlight voxel shadow rays.")]
            [Range(0.01f, 0.4f)] public float flashlightShadowBias = 0.06f;

            [Tooltip("Darkest permitted flashlight shadow value. Pure black is forbidden.")]
            [Range(0.02f, 0.25f)] public float flashlightShadowFloor = 0.08f;

            [Tooltip("Blackout exponent applied to the underwater noir fog remap.")]
            [Range(0.5f, 6f)] public float noirPower = 1.9f;

            [Tooltip("Depth-density used by the underwater noir exponential remap.")]
            [Range(0.0005f, 0.08f)] public float noirFogDensity = 0.012f;

            [Tooltip("Lift floor used to prevent pure black in the final noir composite.")]
            public Color noirLiftColor = new Color(0.0038f, 0.0056f, 0.0078f, 1f);

            [Tooltip("Overall brightness of the procedural lens ghost sprites.")]
            [Range(0f, 2f)] public float lensGhostIntensity = 0.34f;

            [Tooltip("Base radius of each procedural ghost sprite in normalized screen space.")]
            [Range(0.01f, 0.18f)] public float lensGhostScale = 0.075f;

            [Tooltip("Edge-weighted chromatic spread applied to the procedural lens ghosts.")]
            [Range(0f, 0.03f)] public float lensChromaticAberration = 0.005f;

            [Tooltip("How strongly lens ghosts bias toward the screen edge falloff.")]
            [Range(0f, 8f)] public float lensEdgeWeight = 4.2f;

            [Tooltip("Lower EV clamp used by the luminance histogram.")]
            [Range(-16f, 4f)] public float minEv = -10f;

            [Tooltip("Upper EV clamp used by the luminance histogram.")]
            [Range(-4f, 16f)] public float maxEv = 12f;

            [Tooltip("Temporal smoothing rate for the weighted EV target.")]
            [Range(0.05f, 8f)] public float exposureAdaptationRate = 2.4f;

            [Tooltip("Per-frame EV clamp used to prevent white-out glitches.")]
            [Range(0.05f, 0.5f)] public float evMaxDeltaPerFrame = 0.5f;
        }

        private sealed class ShaftsPass : ScriptableRenderPass, IDisposable
        {
            private sealed class ExposureClearPassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal BufferHandle histogram;
            }

            private sealed class ExposureBuildPassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal uint threadGroupSizeX;
                internal uint threadGroupSizeY;
                internal TextureHandle source;
                internal BufferHandle histogram;
                internal Vector4 inputSize;
                internal float minEv;
                internal float maxEv;
            }

            private sealed class ExposureResolvePassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal BufferHandle histogram;
                internal BufferHandle exposureState;
                internal float minEv;
                internal float maxEv;
                internal float adaptationRate;
                internal float deltaTime;
                internal float maxDeltaPerFrame;
            }

            private sealed class FullscreenPassData
            {
                internal TextureHandle source;
                internal TextureHandle destination;
                internal BufferHandle exposureState;
                internal Material material;
            }

            private sealed class CompositePassData
            {
                internal TextureHandle source;
                internal TextureHandle shafts;
                internal TextureHandle destination;
                internal BufferHandle exposureState;
                internal Material compositeMaterial;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Underwater Noir Stack");
            private FeatureSettings _settings;
            private Material _raymarchMaterial;
            private Material _blurHorizontalMaterial;
            private Material _blurVerticalMaterial;
            private Material _compositeMaterial;
            private ComputeShader _autoExposureComputeShader;
            private GraphicsBuffer _histogramBuffer;
            private GraphicsBuffer _exposureStateBuffer;
            private int _clearHistogramKernel = -1;
            private int _buildHistogramKernel = -1;
            private int _resolveExposureKernel = -1;
            private uint _buildThreadGroupSizeX = 8;
            private uint _buildThreadGroupSizeY = 8;

            public ShaftsPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                Material raymarchMaterial,
                Material blurHorizontalMaterial,
                Material blurVerticalMaterial,
                Material compositeMaterial)
            {
                _settings = settings;
                _raymarchMaterial = raymarchMaterial;
                _blurHorizontalMaterial = blurHorizontalMaterial;
                _blurVerticalMaterial = blurVerticalMaterial;
                _compositeMaterial = compositeMaterial;
                _autoExposureComputeShader = settings != null ? settings.autoExposureComputeShader : null;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;

                if (_clearHistogramKernel < 0)
                {
                    TryInitializeAutoExposureKernels();
                }

                // Unity 6 RenderGraph external GraphicsBuffer imports are currently destabilizing
                // the active PC renderer path. Keep the noir stack on its fixed-exposure branch
                // until the auto-exposure buffers are re-authored around transient RG resources.
                ReleaseAutoExposureResources();
            }

            public void Dispose()
            {
                _histogramBuffer?.Release();
                _exposureStateBuffer?.Release();
                _histogramBuffer = null;
                _exposureStateBuffer = null;
                _clearHistogramKernel = -1;
                _buildHistogramKernel = -1;
                _resolveExposureKernel = -1;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!Application.isPlaying)
                    return;

                if (_settings == null ||
                    _raymarchMaterial == null ||
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
                if (IsUnsupportedCameraType(cameraData.cameraType))
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                int shaftWidth = Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));
                int shaftHeight = Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * Mathf.Clamp(_settings.renderScale, 0.25f, 1f)));

                TextureDesc shaftDesc = new TextureDesc(sourceDesc);
                shaftDesc.name = "_HectonScooterVolumetricShafts";
                shaftDesc.width = shaftWidth;
                shaftDesc.height = shaftHeight;
                shaftDesc.depthBufferBits = DepthBits.None;
                shaftDesc.msaaSamples = MSAASamples.None;
                shaftDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                shaftDesc.clearBuffer = true;
                shaftDesc.clearColor = new Color(0.0012f, 0.0018f, 0.0024f, 0f);
                shaftDesc.filterMode = FilterMode.Bilinear;
                shaftDesc.useMipMap = false;
                shaftDesc.autoGenerateMips = false;

                TextureDesc blurDesc = new TextureDesc(shaftDesc);
                blurDesc.name = "_HectonScooterVolumetricShaftsBlur";

                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonScooterVolumetricShaftsComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                compositeDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;

                TextureHandle shaftsTexture = renderGraph.CreateTexture(shaftDesc);
                TextureHandle blurTexture = renderGraph.CreateTexture(blurDesc);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                BufferHandle histogramHandle = default;
                BufferHandle exposureStateHandle = default;
                bool exposureAvailable = _autoExposureComputeShader != null &&
                    _clearHistogramKernel >= 0 &&
                    _buildHistogramKernel >= 0 &&
                    _resolveExposureKernel >= 0 &&
                    _histogramBuffer != null &&
                    _exposureStateBuffer != null;

                if (exposureAvailable)
                {
                    histogramHandle = renderGraph.ImportBuffer(_histogramBuffer);
                    exposureStateHandle = renderGraph.ImportBuffer(_exposureStateBuffer);

                    using (var builder = renderGraph.AddComputePass("Hecton Noir Exposure Clear", out ExposureClearPassData passData, _profilingSampler))
                    {
                        passData.computeShader = _autoExposureComputeShader;
                        passData.kernelIndex = _clearHistogramKernel;
                        passData.histogram = histogramHandle;

                        builder.UseBuffer(histogramHandle, AccessFlags.Write);
                        builder.SetRenderFunc(static (ExposureClearPassData data, ComputeGraphContext context) =>
                        {
                            context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.HistogramBufferId, data.histogram);
                            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, 1, 1, 1);
                        });
                    }

                    using (var builder = renderGraph.AddComputePass("Hecton Noir Exposure Histogram", out ExposureBuildPassData passData, _profilingSampler))
                    {
                        passData.computeShader = _autoExposureComputeShader;
                        passData.kernelIndex = _buildHistogramKernel;
                        passData.threadGroupSizeX = _buildThreadGroupSizeX;
                        passData.threadGroupSizeY = _buildThreadGroupSizeY;
                        passData.source = sourceTexture;
                        passData.histogram = histogramHandle;
                        passData.inputSize = new Vector4(
                            sourceDesc.width,
                            sourceDesc.height,
                            1f / Mathf.Max(1, sourceDesc.width),
                            1f / Mathf.Max(1, sourceDesc.height));
                        passData.minEv = Mathf.Min(_settings.minEv, _settings.maxEv - 0.01f);
                        passData.maxEv = Mathf.Max(_settings.maxEv, passData.minEv + 0.01f);

                        builder.UseTexture(sourceTexture, AccessFlags.Read);
                        builder.UseBuffer(histogramHandle, AccessFlags.Read | AccessFlags.Write);
                        builder.SetRenderFunc(static (ExposureBuildPassData data, ComputeGraphContext context) =>
                        {
                            int dispatchX = Mathf.CeilToInt(data.inputSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                            int dispatchY = Mathf.CeilToInt(data.inputSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                            context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceColorId, data.source);
                            context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.HistogramBufferId, data.histogram);
                            context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.InputSizeId, data.inputSize);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.MinEvId, data.minEv);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.MaxEvId, data.maxEv);
                            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                        });
                    }

                    using (var builder = renderGraph.AddComputePass("Hecton Noir Exposure Resolve", out ExposureResolvePassData passData, _profilingSampler))
                    {
                        passData.computeShader = _autoExposureComputeShader;
                        passData.kernelIndex = _resolveExposureKernel;
                        passData.histogram = histogramHandle;
                        passData.exposureState = exposureStateHandle;
                        passData.minEv = Mathf.Min(_settings.minEv, _settings.maxEv - 0.01f);
                        passData.maxEv = Mathf.Max(_settings.maxEv, passData.minEv + 0.01f);
                        passData.adaptationRate = Mathf.Max(0.01f, _settings.exposureAdaptationRate);
                        passData.deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
                        passData.maxDeltaPerFrame = Mathf.Clamp(_settings.evMaxDeltaPerFrame, 0.05f, 0.5f);

                        builder.UseBuffer(histogramHandle, AccessFlags.Read);
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read | AccessFlags.Write);
                        builder.SetRenderFunc(static (ExposureResolvePassData data, ComputeGraphContext context) =>
                        {
                            context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.HistogramBufferId, data.histogram);
                            context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.ExposureStateBufferId, data.exposureState);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.MinEvId, data.minEv);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.MaxEvId, data.maxEv);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.ExposureAdaptationRateId, data.adaptationRate);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.ExposureDeltaTimeId, data.deltaTime);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.EvMaxDeltaPerFrameId, data.maxDeltaPerFrame);
                            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, 1, 1, 1);
                        });
                    }
                }

                UpdateMaterialParameters(_raymarchMaterial, _settings, 0f, exposureAvailable);
                UpdateMaterialParameters(_blurHorizontalMaterial, _settings, 1f, exposureAvailable);
                UpdateMaterialParameters(_blurVerticalMaterial, _settings, 2f, exposureAvailable);
                UpdateMaterialParameters(_compositeMaterial, _settings, 3f, exposureAvailable);

                using (var builder = renderGraph.AddUnsafePass<FullscreenPassData>("Hecton Underwater Noir Raymarch", out var passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.destination = shaftsTexture;
                    passData.exposureState = exposureStateHandle;
                    passData.material = _raymarchMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(shaftsTexture, AccessFlags.Write);
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (FullscreenPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.material, 0);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<FullscreenPassData>("Hecton Underwater Noir Blur Horizontal", out var passData, _profilingSampler))
                {
                    passData.source = shaftsTexture;
                    passData.destination = blurTexture;
                    passData.exposureState = exposureStateHandle;
                    passData.material = _blurHorizontalMaterial;

                    builder.UseTexture(shaftsTexture, AccessFlags.Read);
                    builder.UseTexture(blurTexture, AccessFlags.Write);
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (FullscreenPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.material, 1);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<FullscreenPassData>("Hecton Underwater Noir Blur Vertical", out var passData, _profilingSampler))
                {
                    passData.source = blurTexture;
                    passData.destination = shaftsTexture;
                    passData.exposureState = exposureStateHandle;
                    passData.material = _blurVerticalMaterial;

                    builder.UseTexture(blurTexture, AccessFlags.Read);
                    builder.UseTexture(shaftsTexture, AccessFlags.Write);
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetGlobalTextureAfterPass(shaftsTexture, ShaderConstants.ShaftTextureId);
                    builder.SetGlobalTextureAfterPass(shaftsTexture, ShaderConstants.HeadlightVolumetricsTextureId);

                    builder.SetRenderFunc(static (FullscreenPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.material, 2);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<CompositePassData>("Hecton Underwater Noir Composite", out var passData, _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.shafts = shaftsTexture;
                    passData.destination = compositeTexture;
                    passData.exposureState = exposureStateHandle;
                    passData.compositeMaterial = _compositeMaterial;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(shaftsTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (CompositePassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        const RenderBufferLoadAction LoadAction = RenderBufferLoadAction.DontCare;
                        const RenderBufferStoreAction StoreAction = RenderBufferStoreAction.Store;

                        Blitter.BlitCameraTexture(cmd, data.source, data.destination, LoadAction, StoreAction, data.compositeMaterial, 3);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private void EnsureAutoExposureResources()
            {
                if (_autoExposureComputeShader == null ||
                    _clearHistogramKernel < 0 ||
                    _buildHistogramKernel < 0 ||
                    _resolveExposureKernel < 0)
                {
                    ReleaseAutoExposureResources();
                    return;
                }

                if (_histogramBuffer == null)
                {
                    // COLD ALLOC: GraphicsBuffer[64] - persistent 64-bin GPU histogram for noir auto exposure - owner: ShaftsPass
                    _histogramBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(HistogramBinCount);
                }

                if (_exposureStateBuffer == null)
                {
                    // COLD ALLOC: GraphicsBuffer[1] - persistent GPU exposure state for temporal EV clamp - owner: ShaftsPass
                    _exposureStateBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(1);
                    NativeArray<Vector4> mapped = _exposureStateBuffer.LockBufferForWrite<Vector4>(0, 1);
                    mapped[0] = new Vector4(0f, 0f, ExposureStateDefaultMultiplier, 0f);
                    _exposureStateBuffer.UnlockBufferAfterWrite<Vector4>(1);
                }

                if (_compositeMaterial != null && _exposureStateBuffer != null)
                    _compositeMaterial.SetBuffer(ShaderConstants.ExposureStateBufferId, _exposureStateBuffer);
            }

            private void TryInitializeAutoExposureKernels()
            {
                if (_autoExposureComputeShader == null || !SystemInfo.supportsComputeShaders)
                {
                    DisableAutoExposure();
                    return;
                }

                if (!_autoExposureComputeShader.HasKernel("ClearHistogram") ||
                    !_autoExposureComputeShader.HasKernel("BuildHistogram") ||
                    !_autoExposureComputeShader.HasKernel("ResolveExposure"))
                {
                    DisableAutoExposure();
                    return;
                }

                int clearHistogramKernel = _autoExposureComputeShader.FindKernel("ClearHistogram");
                int buildHistogramKernel = _autoExposureComputeShader.FindKernel("BuildHistogram");
                int resolveExposureKernel = _autoExposureComputeShader.FindKernel("ResolveExposure");
                if (!_autoExposureComputeShader.IsSupported(clearHistogramKernel) ||
                    !_autoExposureComputeShader.IsSupported(buildHistogramKernel) ||
                    !_autoExposureComputeShader.IsSupported(resolveExposureKernel))
                {
                    DisableAutoExposure();
                    return;
                }

                _clearHistogramKernel = clearHistogramKernel;
                _buildHistogramKernel = buildHistogramKernel;
                _resolveExposureKernel = resolveExposureKernel;
                _autoExposureComputeShader.GetKernelThreadGroupSizes(
                    _buildHistogramKernel,
                    out _buildThreadGroupSizeX,
                    out _buildThreadGroupSizeY,
                    out _);
            }

            private void DisableAutoExposure()
            {
                _autoExposureComputeShader = null;
                _clearHistogramKernel = -1;
                _buildHistogramKernel = -1;
                _resolveExposureKernel = -1;
                ReleaseAutoExposureResources();
            }

            private void ReleaseAutoExposureResources()
            {
                _histogramBuffer?.Release();
                _exposureStateBuffer?.Release();
                _histogramBuffer = null;
                _exposureStateBuffer = null;
            }

        private static void UpdateMaterialParameters(Material material, FeatureSettings settings, float passMode, bool exposureAvailable)
        {
            material.SetFloat(ShaderConstants.PassModeId, passMode);
            material.SetFloat(ShaderConstants.FrameCountId, Time.frameCount);
            material.SetFloat(ShaderConstants.RenderScaleId, Mathf.Clamp(settings.renderScale, 0.25f, 1f));
            material.SetFloat(ShaderConstants.RaymarchStepsId, 8f);
            material.SetFloat(ShaderConstants.MaxRayDistanceId, Mathf.Max(1f, settings.maxRayDistance));
                material.SetFloat(ShaderConstants.ScatteringAnisotropyId, Mathf.Clamp(settings.scatteringAnisotropy, 0f, 0.95f));
                material.SetFloat(ShaderConstants.DensityId, Mathf.Max(0f, settings.density));
                material.SetFloat(ShaderConstants.BlueNoiseJitterId, Mathf.Clamp01(settings.blueNoiseJitter));
                material.SetFloat(ShaderConstants.BilateralDepthSigmaId, Mathf.Max(0.01f, settings.bilateralDepthSigma));
                material.SetFloat(ShaderConstants.ShaftIntensityId, Mathf.Max(0f, settings.shaftIntensity));
                material.SetFloat(ShaderConstants.BiolumPatternScaleId, Mathf.Max(0.001f, settings.biolumPatternScale));
                material.SetFloat(ShaderConstants.BiolumProjectionStrengthId, Mathf.Max(0f, settings.biolumProjectionStrength));
                material.SetFloat(ShaderConstants.SiltStrengthId, Mathf.Max(0f, settings.siltStrength));
                material.SetFloat(ShaderConstants.SiltNoiseScaleId, Mathf.Max(0.001f, settings.siltNoiseScale));
                material.SetFloat(ShaderConstants.SiltFloorBoostId, Mathf.Max(0f, settings.siltFloorBoost));
                material.SetFloat(ShaderConstants.SiltDriftSpeedId, Mathf.Max(0f, settings.siltDriftSpeed));
                material.SetFloat(ShaderConstants.ContactShadowStrengthId, Mathf.Clamp01(settings.contactShadowStrength));
                material.SetFloat(ShaderConstants.ContactShadowStepsId, Mathf.Clamp(settings.contactShadowSteps, 4, 8));
                material.SetFloat(ShaderConstants.ContactShadowBiasId, Mathf.Max(0.001f, settings.contactShadowBias));
                material.SetFloat(ShaderConstants.ContactShadowMaxDistanceId, Mathf.Max(0.1f, settings.contactShadowMaxDistance));
                Shader.SetGlobalFloat(ShaderConstants.ContactShadowStrengthId, Mathf.Clamp01(settings.contactShadowStrength));
                Shader.SetGlobalFloat(ShaderConstants.ContactShadowStepsId, Mathf.Clamp(settings.contactShadowSteps, 4, 8));
                Shader.SetGlobalFloat(ShaderConstants.ContactShadowBiasId, Mathf.Max(0.001f, settings.contactShadowBias));
                Shader.SetGlobalFloat(ShaderConstants.ContactShadowMaxDistanceId, Mathf.Max(0.1f, settings.contactShadowMaxDistance));
                material.SetFloat(
                    ShaderConstants.FlashlightShadowStepsId,
                    SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 2048 ? 16f : 24f);
                material.SetFloat(ShaderConstants.FlashlightShadowSoftnessId, Mathf.Max(0.1f, settings.flashlightShadowSoftness));
                material.SetFloat(ShaderConstants.FlashlightShadowMinStepId, Mathf.Max(0.005f, settings.flashlightShadowMinStep));
                material.SetFloat(ShaderConstants.FlashlightShadowBiasId, Mathf.Max(0.001f, settings.flashlightShadowBias));
                material.SetFloat(ShaderConstants.FlashlightShadowFloorId, Mathf.Clamp(settings.flashlightShadowFloor, 0.02f, 0.25f));
                material.SetFloat(ShaderConstants.NoirPowerId, Mathf.Max(0.5f, settings.noirPower));
                material.SetFloat(ShaderConstants.NoirFogDensityId, Mathf.Max(0.0001f, settings.noirFogDensity));
                material.SetColor(ShaderConstants.NoirLiftColorId, ResolveNoirLiftColor(settings.noirLiftColor));
                material.SetFloat(ShaderConstants.LensGhostIntensityId, Mathf.Max(0f, settings.lensGhostIntensity));
                material.SetFloat(ShaderConstants.LensGhostScaleId, Mathf.Max(0.001f, settings.lensGhostScale));
                material.SetFloat(ShaderConstants.LensChromaticAberrationId, Mathf.Max(0f, settings.lensChromaticAberration));
                material.SetFloat(ShaderConstants.LensEdgeWeightId, Mathf.Max(0f, settings.lensEdgeWeight));
                material.SetFloat(ShaderConstants.HasExposureStateId, exposureAvailable ? 1f : 0f);
                material.SetFloat(ShaderConstants.HasBlueNoiseTextureId, settings.blueNoiseTexture != null ? 1f : 0f);
                material.SetTexture(ShaderConstants.BlueNoiseTextureId, settings.blueNoiseTexture);
            }

            private static Color ResolveNoirLiftColor(Color configured)
            {
                return new Color(
                    Mathf.Max(configured.r, DefaultNoirLiftFloor.r),
                    Mathf.Max(configured.g, DefaultNoirLiftFloor.g),
                    Mathf.Max(configured.b, DefaultNoirLiftFloor.b),
                    1f);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int SourceColorId = Shader.PropertyToID("_HectonSourceColor");
            internal static readonly int InputSizeId = Shader.PropertyToID("_HectonNoirInputSize");
            internal static readonly int MinEvId = Shader.PropertyToID("_HectonNoirMinEv");
            internal static readonly int MaxEvId = Shader.PropertyToID("_HectonNoirMaxEv");
            internal static readonly int ExposureAdaptationRateId = Shader.PropertyToID("_HectonNoirExposureAdaptationRate");
            internal static readonly int ExposureDeltaTimeId = Shader.PropertyToID("_HectonNoirExposureDeltaTime");
            internal static readonly int EvMaxDeltaPerFrameId = Shader.PropertyToID("_HectonNoirEVMaxDeltaPerFrame");
            internal static readonly int HistogramBufferId = Shader.PropertyToID("_HectonNoirHistogram");
            internal static readonly int ExposureStateBufferId = Shader.PropertyToID("_HectonNoirExposureState");
            internal static readonly int HeadlightCountId = Shader.PropertyToID("_HectonScooterHeadlightCount");
            internal static readonly int FloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");
            internal static readonly int PassModeId = Shader.PropertyToID("_HectonShaftPassMode");
            internal static readonly int RenderScaleId = Shader.PropertyToID("_HectonShaftRenderScale");
            internal static readonly int RaymarchStepsId = Shader.PropertyToID("_HectonShaftRaymarchSteps");
            internal static readonly int MaxRayDistanceId = Shader.PropertyToID("_HectonShaftMaxRayDistance");
            internal static readonly int ScatteringAnisotropyId = Shader.PropertyToID("_HectonShaftScatteringAnisotropy");
            internal static readonly int DensityId = Shader.PropertyToID("_HectonShaftDensity");
            internal static readonly int BlueNoiseJitterId = Shader.PropertyToID("_HectonShaftBlueNoiseJitter");
            internal static readonly int BilateralDepthSigmaId = Shader.PropertyToID("_HectonShaftBilateralDepthSigma");
            internal static readonly int ShaftIntensityId = Shader.PropertyToID("_HectonShaftIntensity");
            internal static readonly int BiolumPatternScaleId = Shader.PropertyToID("_HectonBiolumPatternScale");
            internal static readonly int BiolumProjectionStrengthId = Shader.PropertyToID("_HectonBiolumProjectionStrength");
            internal static readonly int SiltStrengthId = Shader.PropertyToID("_HectonSiltStrength");
            internal static readonly int SiltNoiseScaleId = Shader.PropertyToID("_HectonSiltNoiseScale");
            internal static readonly int SiltFloorBoostId = Shader.PropertyToID("_HectonSiltFloorBoost");
            internal static readonly int SiltDriftSpeedId = Shader.PropertyToID("_HectonSiltDriftSpeed");
            internal static readonly int ContactShadowStrengthId = Shader.PropertyToID("_HectonContactShadowStrength");
            internal static readonly int ContactShadowStepsId = Shader.PropertyToID("_HectonContactShadowSteps");
            internal static readonly int ContactShadowBiasId = Shader.PropertyToID("_HectonContactShadowBias");
            internal static readonly int ContactShadowMaxDistanceId = Shader.PropertyToID("_HectonContactShadowMaxDistance");
            internal static readonly int FlashlightShadowStepsId = Shader.PropertyToID("_HectonFlashlightShadowSteps");
            internal static readonly int FlashlightShadowSoftnessId = Shader.PropertyToID("_HectonFlashlightShadowSoftness");
            internal static readonly int FlashlightShadowMinStepId = Shader.PropertyToID("_HectonFlashlightShadowMinStep");
            internal static readonly int FlashlightShadowBiasId = Shader.PropertyToID("_HectonFlashlightShadowBias");
            internal static readonly int FlashlightShadowFloorId = Shader.PropertyToID("_HectonFlashlightShadowFloor");
            internal static readonly int NoirPowerId = Shader.PropertyToID("_HectonNoirPower");
            internal static readonly int NoirFogDensityId = Shader.PropertyToID("_HectonNoirFogDensity");
            internal static readonly int NoirLiftColorId = Shader.PropertyToID("_HectonNoirLiftColor");
            internal static readonly int LensGhostIntensityId = Shader.PropertyToID("_HectonLensGhostIntensity");
            internal static readonly int LensGhostScaleId = Shader.PropertyToID("_HectonLensGhostScale");
            internal static readonly int LensChromaticAberrationId = Shader.PropertyToID("_HectonLensChromaticAberration");
            internal static readonly int LensEdgeWeightId = Shader.PropertyToID("_HectonLensEdgeWeight");
            internal static readonly int FrameCountId = Shader.PropertyToID("_HectonFrameCount");
            internal static readonly int BlueNoiseTextureId = Shader.PropertyToID("_BlueNoiseTex");
            internal static readonly int HasBlueNoiseTextureId = Shader.PropertyToID("_HectonHasBlueNoiseTex");
            internal static readonly int HasExposureStateId = Shader.PropertyToID("_HectonHasExposureState");
            internal static readonly int ShaftTextureId = Shader.PropertyToID("_HectonShaftsTexture");
            internal static readonly int HeadlightVolumetricsTextureId = Shader.PropertyToID("_HectonHeadlightVolumetrics");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ShaftsPass _pass;
        private Material _raymarchMaterial;
        private Material _blurHorizontalMaterial;
        private Material _blurVerticalMaterial;
        private Material _compositeMaterial;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
            if (settings != null && settings.autoExposureComputeShader == null)
                settings.autoExposureComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(AutoExposureComputeAssetPath);
#endif

            Shader shader = settings != null && settings.shader != null
                ? settings.shader
                : Shader.Find("Hidden/Hecton8/ScooterVolumetricShafts");

            _pass ??= new ShaftsPass();
            RecreateMaterial(ref _raymarchMaterial, shader);
            RecreateMaterial(ref _blurHorizontalMaterial, shader);
            RecreateMaterial(ref _blurVerticalMaterial, shader);
            RecreateMaterial(ref _compositeMaterial, shader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
                return;

            if (settings == null ||
                _pass == null ||
                _raymarchMaterial == null ||
                _blurHorizontalMaterial == null ||
                _blurVerticalMaterial == null ||
                _compositeMaterial == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (IsUnsupportedCameraType(cameraType))
                return;

            _pass.Setup(settings, _raymarchMaterial, _blurHorizontalMaterial, _blurVerticalMaterial, _compositeMaterial);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_raymarchMaterial);
            CoreUtils.Destroy(_blurHorizontalMaterial);
            CoreUtils.Destroy(_blurVerticalMaterial);
            CoreUtils.Destroy(_compositeMaterial);
            _raymarchMaterial = null;
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
