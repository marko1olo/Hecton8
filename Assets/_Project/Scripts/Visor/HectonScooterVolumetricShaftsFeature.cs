using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

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
        private const float ThermalHazeMotionCullSpeedMetersPerSecondSq = 225f;
        private const float SurfaceNoirSuppressionDepth = 0.08f;
        private const float UnderwaterNoirFullDepth = 0.24f;
        private static readonly Color DefaultNoirLiftFloor = new Color(0.01f, 0.012f, 0.016f, 1f);
        private static readonly Color ShaftClearColor = new Color(0.0012f, 0.0018f, 0.0024f, 0f);

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden multi-pass shader used for screen-space bright-pass shafts, bilateral upsample, lens ghosts, and final composite.")]
            public Shader shader = null;

            [Tooltip("GPU histogram compute shader used to resolve weighted EV and temporal exposure smoothing.")]
            public ComputeShader autoExposureComputeShader = null;

            [Tooltip("Where the volumetric shaft pass is injected into URP. Before transparents keeps Crest water and camera-space UI on top of the shaft composite.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("Internal render scale for the shaft target. Lower values save MX350 fill-rate.")]
            [Range(0.25f, 1f)] public float renderScale = 0.5f;

            [Tooltip("Compatibility field. Shaft generation performs zero world raymarch steps; shader uses fixed 2D radial taps.")]
            [Range(0, 0)] public int raymarchSteps = 0;

            [Tooltip("Legacy compatibility clamp. Screen-space shaft generation does not march through world volume.")]
            [Range(8f, 120f)] public float maxRayDistance = 56f;

            [Tooltip("Legacy compatibility value retained for material layout.")]
            [Range(0f, 0.95f)] public float scatteringAnisotropy = 0.68f;

            [Tooltip("Base water density used for light accumulation.")]
            [Range(0f, 4f)] public float density = 1.05f;

            [Tooltip("Amount of deterministic IGN jitter applied to the screen-space radial taps.")]
            [FormerlySerializedAs("blueNoiseJitter")]
            [Range(0f, 1f)] public float ignJitter = 0.85f;

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

            [Tooltip("Procedural lens dirt visibility when bright light hits the visor glass.")]
            [Range(0f, 1f)] public float lensDirtIntensity = 0.24f;

            [Tooltip("Procedural water-drop condensation visibility when bright light hits the visor glass.")]
            [Range(0f, 1f)] public float condensationIntensity = 0.18f;

            [Tooltip("Screen-space thermal haze displacement driven by recent heat stamps.")]
            [Range(0f, 0.006f)] public float thermalHazeIntensity = 0.0016f;

            [Tooltip("Quarter-resolution cell scale for procedural thermal haze displacement.")]
            [Range(0.25f, 8f)] public float thermalHazeScale = 2.25f;

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
            private const int HalfResContactDepthPassIndex = 4;
            private const float ExposureFixedDeltaSeconds = 1f / 60f;

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
                internal int dispatchX;
                internal int dispatchY;
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

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Underwater Noir Stack");
            private FeatureSettings _settings;
            private Material _raymarchMaterial;
            private Material _blurHorizontalMaterial;
            private Material _blurVerticalMaterial;
            private Material _compositeMaterial;
            private ComputeShader _autoExposureComputeShader;
            private GraphicsBuffer _histogramBuffer;
            private GraphicsBuffer _exposureStateBuffer;
            private GraphicsBuffer _lastExposureStateBuffer;
            private Material _lastExposureStateMaterial;
            private MaterialUploadCache _raymarchMaterialCache;
            private MaterialUploadCache _blurHorizontalMaterialCache;
            private MaterialUploadCache _blurVerticalMaterialCache;
            private MaterialUploadCache _compositeMaterialCache;
            private Vector4 _lastContactShadowGlobals = Vector4.positiveInfinity;
            private bool _hasContactShadowGlobals;
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

                EnsureAutoExposureResources();
            }

            public void Dispose()
            {
                _histogramBuffer?.Release();
                _exposureStateBuffer?.Release();
                _histogramBuffer = null;
                _exposureStateBuffer = null;
                _lastExposureStateBuffer = null;
                _lastExposureStateMaterial = null;
                _raymarchMaterialCache = default;
                _blurHorizontalMaterialCache = default;
                _blurVerticalMaterialCache = default;
                _compositeMaterialCache = default;
                _hasContactShadowGlobals = false;
                _lastContactShadowGlobals = Vector4.positiveInfinity;
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

                float underwaterNoirBlend = ResolveUnderwaterNoirBlend();
                if (underwaterNoirBlend <= 0.0001f)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                float resolvedRenderScale = math.clamp(_settings.renderScale, 0.25f, 1f);
                int sourceWidth = math.max(1, sourceDesc.width);
                int sourceHeight = math.max(1, sourceDesc.height);
                int shaftWidth = math.max(1, (int)math.round(sourceWidth * resolvedRenderScale));
                int shaftHeight = math.max(1, (int)math.round(sourceHeight * resolvedRenderScale));

                TextureDesc shaftDesc = new TextureDesc(sourceDesc);
                shaftDesc.name = "_HectonScooterVolumetricShafts";
                shaftDesc.width = shaftWidth;
                shaftDesc.height = shaftHeight;
                shaftDesc.depthBufferBits = DepthBits.None;
                shaftDesc.msaaSamples = MSAASamples.None;
                shaftDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                shaftDesc.clearBuffer = true;
                shaftDesc.clearColor = ShaftClearColor;
                shaftDesc.filterMode = FilterMode.Bilinear;
                shaftDesc.useMipMap = false;
                shaftDesc.autoGenerateMips = false;

                TextureDesc blurDesc = new TextureDesc(shaftDesc);
                blurDesc.name = "_HectonScooterVolumetricShaftsBlur";

                TextureDesc halfResDepthDesc = new TextureDesc(shaftDesc);
                halfResDepthDesc.name = "_HectonHalfResContactDepth";
                halfResDepthDesc.colorFormat = GraphicsFormat.R32_SFloat;
                halfResDepthDesc.clearColor = Color.white;
                halfResDepthDesc.filterMode = FilterMode.Point;

                TextureDesc compositeDesc = new TextureDesc(sourceDesc);
                compositeDesc.name = "_HectonScooterVolumetricShaftsComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                compositeDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;

                TextureHandle shaftsTexture = renderGraph.CreateTexture(shaftDesc);
                TextureHandle blurTexture = renderGraph.CreateTexture(blurDesc);
                TextureHandle halfResDepthTexture = renderGraph.CreateTexture(halfResDepthDesc);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);

                BufferHandle histogramHandle = default;
                BufferHandle exposureStateHandle = default;
                bool exposureAvailable = _autoExposureComputeShader != null &&
                    _clearHistogramKernel >= 0 &&
                    _buildHistogramKernel >= 0 &&
                    _resolveExposureKernel >= 0 &&
                    _histogramBuffer != null &&
                    _exposureStateBuffer != null;
                float resolvedMinEv = math.min(_settings.minEv, _settings.maxEv - 0.01f);
                float resolvedMaxEv = math.max(_settings.maxEv, resolvedMinEv + 0.01f);
                int exposureThreadGroupSizeX = math.max(1, (int)_buildThreadGroupSizeX);
                int exposureThreadGroupSizeY = math.max(1, (int)_buildThreadGroupSizeY);
                int exposureDispatchX = (sourceWidth + exposureThreadGroupSizeX - 1) / exposureThreadGroupSizeX;
                int exposureDispatchY = (sourceHeight + exposureThreadGroupSizeY - 1) / exposureThreadGroupSizeY;

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
                        builder.SetRenderFunc((ExposureClearPassData data, ComputeGraphContext context) =>
                        {
                            context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.HistogramBufferId, data.histogram);
                            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, 1, 1, 1);
                        });
                    }

                    using (var builder = renderGraph.AddComputePass("Hecton Noir Exposure Histogram", out ExposureBuildPassData passData, _profilingSampler))
                    {
                        passData.computeShader = _autoExposureComputeShader;
                        passData.kernelIndex = _buildHistogramKernel;
                        passData.dispatchX = exposureDispatchX;
                        passData.dispatchY = exposureDispatchY;
                        passData.source = sourceTexture;
                        passData.histogram = histogramHandle;
                        passData.inputSize = ResolveInputSize(sourceWidth, sourceHeight);
                        passData.minEv = resolvedMinEv;
                        passData.maxEv = resolvedMaxEv;

                        builder.UseTexture(sourceTexture, AccessFlags.Read);
                        builder.UseBuffer(histogramHandle, AccessFlags.Read | AccessFlags.Write);
                        builder.SetRenderFunc((ExposureBuildPassData data, ComputeGraphContext context) =>
                        {
                            context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceColorId, data.source);
                            context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.HistogramBufferId, data.histogram);
                            context.cmd.SetComputeVectorParam(data.computeShader, ShaderConstants.InputSizeId, data.inputSize);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.MinEvId, data.minEv);
                            context.cmd.SetComputeFloatParam(data.computeShader, ShaderConstants.MaxEvId, data.maxEv);
                            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, 1);
                        });
                    }

                    using (var builder = renderGraph.AddComputePass("Hecton Noir Exposure Resolve", out ExposureResolvePassData passData, _profilingSampler))
                    {
                        passData.computeShader = _autoExposureComputeShader;
                        passData.kernelIndex = _resolveExposureKernel;
                        passData.histogram = histogramHandle;
                        passData.exposureState = exposureStateHandle;
                        passData.minEv = resolvedMinEv;
                        passData.maxEv = resolvedMaxEv;
                        passData.adaptationRate = math.max(0.01f, _settings.exposureAdaptationRate);
                        passData.deltaTime = ExposureFixedDeltaSeconds;
                        passData.maxDeltaPerFrame = math.clamp(_settings.evMaxDeltaPerFrame, 0.05f, 0.5f);

                        builder.UseBuffer(histogramHandle, AccessFlags.Read);
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read | AccessFlags.Write);
                        builder.SetRenderFunc((ExposureResolvePassData data, ComputeGraphContext context) =>
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

                MaterialParameterState materialParameters = MaterialParameterState.Resolve(
                    _settings,
                    exposureAvailable,
                    underwaterNoirBlend);
                ApplyContactShadowGlobalsIfChanged(in materialParameters);
                UpdateMaterialParameters(_raymarchMaterial, ref _raymarchMaterialCache, in materialParameters, 0f);
                UpdateMaterialParameters(_blurHorizontalMaterial, ref _blurHorizontalMaterialCache, in materialParameters, 1f);
                UpdateMaterialParameters(_blurVerticalMaterial, ref _blurVerticalMaterialCache, in materialParameters, 2f);
                UpdateMaterialParameters(_compositeMaterial, ref _compositeMaterialCache, in materialParameters, 3f);

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, halfResDepthTexture, _raymarchMaterial, HalfResContactDepthPassIndex),
                           passName: "Hecton Underwater Noir Half-Res Contact Depth",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(halfResDepthTexture, ShaderConstants.HalfResDepthTextureId);
                }

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, shaftsTexture, _raymarchMaterial, 0),
                           passName: "Hecton Underwater Noir Radial Shafts",
                           returnBuilder: true))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
                }

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(shaftsTexture, blurTexture, _blurHorizontalMaterial, 1),
                           passName: "Hecton Underwater Noir Blur Horizontal",
                           returnBuilder: true))
                {
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
                }

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(blurTexture, shaftsTexture, _blurVerticalMaterial, 2),
                           passName: "Hecton Underwater Noir Blur Vertical",
                           returnBuilder: true))
                {
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(shaftsTexture, ShaderConstants.ShaftTextureId);
                    builder.SetGlobalTextureAfterPass(shaftsTexture, ShaderConstants.HeadlightVolumetricsTextureId);
                }

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           new RenderGraphUtils.BlitMaterialParameters(sourceTexture, compositeTexture, _compositeMaterial, 3),
                           passName: "Hecton Underwater Noir Composite",
                           returnBuilder: true))
                {
                    builder.UseTexture(shaftsTexture, AccessFlags.Read);
                    builder.UseTexture(halfResDepthTexture, AccessFlags.Read);
                    if (exposureAvailable)
                        builder.UseBuffer(exposureStateHandle, AccessFlags.Read);
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
                    Vector4 exposureState;
                    exposureState.x = 0f;
                    exposureState.y = 0f;
                    exposureState.z = ExposureStateDefaultMultiplier;
                    exposureState.w = 0f;
                    mapped[0] = exposureState;
                    _exposureStateBuffer.UnlockBufferAfterWrite<Vector4>(1);
                }

                if (_compositeMaterial != null &&
                    _exposureStateBuffer != null &&
                    (!ReferenceEquals(_lastExposureStateMaterial, _compositeMaterial) ||
                     !ReferenceEquals(_lastExposureStateBuffer, _exposureStateBuffer)))
                {
                    _compositeMaterial.SetBuffer(ShaderConstants.ExposureStateBufferId, _exposureStateBuffer);
                    _lastExposureStateMaterial = _compositeMaterial;
                    _lastExposureStateBuffer = _exposureStateBuffer;
                }
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
                _lastExposureStateBuffer = null;
                _lastExposureStateMaterial = null;
            }

            private static Vector4 ResolveInputSize(int width, int height)
            {
                Vector4 inputSize;
                inputSize.x = width;
                inputSize.y = height;
                inputSize.z = 1f / width;
                inputSize.w = 1f / height;
                return inputSize;
            }

            private static void UpdateMaterialParameters(
                Material material,
                ref MaterialUploadCache cache,
                in MaterialParameterState parameters,
                float passMode)
            {
                if (cache.HasState &&
                    ReferenceEquals(cache.Material, material) &&
                    cache.PassMode == passMode &&
                    MaterialParametersEqual(in cache.Parameters, in parameters))
                {
                    return;
                }

                material.SetFloat(ShaderConstants.PassModeId, passMode);
                material.SetFloat(ShaderConstants.RenderScaleId, parameters.RenderScale);
                material.SetFloat(ShaderConstants.RaymarchStepsId, 0f);
                material.SetFloat(ShaderConstants.MaxRayDistanceId, parameters.MaxRayDistance);
                material.SetFloat(ShaderConstants.ScatteringAnisotropyId, parameters.ScatteringAnisotropy);
                material.SetFloat(ShaderConstants.DensityId, parameters.Density);
                material.SetFloat(ShaderConstants.IgnJitterId, parameters.IgnJitter);
                material.SetFloat(ShaderConstants.BilateralDepthSigmaId, parameters.BilateralDepthSigma);
                material.SetFloat(ShaderConstants.ShaftIntensityId, parameters.ShaftIntensity);
                material.SetFloat(ShaderConstants.BiolumPatternScaleId, parameters.BiolumPatternScale);
                material.SetFloat(ShaderConstants.BiolumProjectionStrengthId, parameters.BiolumProjectionStrength);
                material.SetFloat(ShaderConstants.SiltStrengthId, parameters.SiltStrength);
                material.SetFloat(ShaderConstants.SiltNoiseScaleId, parameters.SiltNoiseScale);
                material.SetFloat(ShaderConstants.SiltFloorBoostId, parameters.SiltFloorBoost);
                material.SetFloat(ShaderConstants.SiltDriftSpeedId, parameters.SiltDriftSpeed);
                material.SetFloat(ShaderConstants.ContactShadowStrengthId, parameters.ContactShadowStrength);
                material.SetFloat(ShaderConstants.ContactShadowStepsId, parameters.ContactShadowSteps);
                material.SetFloat(ShaderConstants.ContactShadowBiasId, parameters.ContactShadowBias);
                material.SetFloat(ShaderConstants.ContactShadowMaxDistanceId, parameters.ContactShadowMaxDistance);
                material.SetFloat(ShaderConstants.FlashlightShadowStepsId, parameters.FlashlightShadowSteps);
                material.SetFloat(ShaderConstants.FlashlightShadowSoftnessId, parameters.FlashlightShadowSoftness);
                material.SetFloat(ShaderConstants.FlashlightShadowMinStepId, parameters.FlashlightShadowMinStep);
                material.SetFloat(ShaderConstants.FlashlightShadowBiasId, parameters.FlashlightShadowBias);
                material.SetFloat(ShaderConstants.FlashlightShadowFloorId, parameters.FlashlightShadowFloor);
                material.SetFloat(ShaderConstants.NoirPowerId, parameters.NoirPower);
                material.SetFloat(ShaderConstants.NoirFogDensityId, parameters.NoirFogDensity);
                material.SetColor(ShaderConstants.NoirLiftColorId, parameters.NoirLiftColor);
                material.SetFloat(ShaderConstants.LensGhostIntensityId, parameters.LensGhostIntensity);
                material.SetFloat(ShaderConstants.LensGhostScaleId, parameters.LensGhostScale);
                material.SetFloat(ShaderConstants.LensChromaticAberrationId, parameters.LensChromaticAberration);
                material.SetFloat(ShaderConstants.LensEdgeWeightId, parameters.LensEdgeWeight);
                material.SetFloat(ShaderConstants.LensDirtIntensityId, parameters.LensDirtIntensity);
                material.SetFloat(ShaderConstants.CondensationIntensityId, parameters.CondensationIntensity);
                material.SetFloat(ShaderConstants.ThermalHazeIntensityId, parameters.ThermalHazeIntensity);
                material.SetFloat(ShaderConstants.ThermalHazeScaleId, parameters.ThermalHazeScale);
                material.SetFloat(ShaderConstants.HasExposureStateId, parameters.HasExposureState);

                cache.Material = material;
                cache.PassMode = passMode;
                cache.Parameters = parameters;
                cache.HasState = true;
            }

            private void ApplyContactShadowGlobalsIfChanged(in MaterialParameterState parameters)
            {
                Vector4 globals = new Vector4(
                    parameters.ContactShadowStrength,
                    parameters.ContactShadowSteps,
                    parameters.ContactShadowBias,
                    parameters.ContactShadowMaxDistance);

                if (_hasContactShadowGlobals && _lastContactShadowGlobals == globals)
                    return;

                Shader.SetGlobalFloat(ShaderConstants.ContactShadowStrengthId, parameters.ContactShadowStrength);
                Shader.SetGlobalFloat(ShaderConstants.ContactShadowStepsId, parameters.ContactShadowSteps);
                Shader.SetGlobalFloat(ShaderConstants.ContactShadowBiasId, parameters.ContactShadowBias);
                Shader.SetGlobalFloat(ShaderConstants.ContactShadowMaxDistanceId, parameters.ContactShadowMaxDistance);
                _lastContactShadowGlobals = globals;
                _hasContactShadowGlobals = true;
            }

            private static bool MaterialParametersEqual(
                in MaterialParameterState left,
                in MaterialParameterState right)
            {
                return left.RenderScale == right.RenderScale &&
                       left.MaxRayDistance == right.MaxRayDistance &&
                       left.ScatteringAnisotropy == right.ScatteringAnisotropy &&
                       left.Density == right.Density &&
                       left.IgnJitter == right.IgnJitter &&
                       left.BilateralDepthSigma == right.BilateralDepthSigma &&
                       left.ShaftIntensity == right.ShaftIntensity &&
                       left.BiolumPatternScale == right.BiolumPatternScale &&
                       left.BiolumProjectionStrength == right.BiolumProjectionStrength &&
                       left.SiltStrength == right.SiltStrength &&
                       left.SiltNoiseScale == right.SiltNoiseScale &&
                       left.SiltFloorBoost == right.SiltFloorBoost &&
                       left.SiltDriftSpeed == right.SiltDriftSpeed &&
                       left.ContactShadowStrength == right.ContactShadowStrength &&
                       left.ContactShadowSteps == right.ContactShadowSteps &&
                       left.ContactShadowBias == right.ContactShadowBias &&
                       left.ContactShadowMaxDistance == right.ContactShadowMaxDistance &&
                       left.FlashlightShadowSteps == right.FlashlightShadowSteps &&
                       left.FlashlightShadowSoftness == right.FlashlightShadowSoftness &&
                       left.FlashlightShadowMinStep == right.FlashlightShadowMinStep &&
                       left.FlashlightShadowBias == right.FlashlightShadowBias &&
                       left.FlashlightShadowFloor == right.FlashlightShadowFloor &&
                       left.NoirPower == right.NoirPower &&
                       left.NoirFogDensity == right.NoirFogDensity &&
                       left.NoirLiftColor == right.NoirLiftColor &&
                       left.LensGhostIntensity == right.LensGhostIntensity &&
                       left.LensGhostScale == right.LensGhostScale &&
                       left.LensChromaticAberration == right.LensChromaticAberration &&
                       left.LensEdgeWeight == right.LensEdgeWeight &&
                       left.LensDirtIntensity == right.LensDirtIntensity &&
                       left.CondensationIntensity == right.CondensationIntensity &&
                       left.ThermalHazeIntensity == right.ThermalHazeIntensity &&
                       left.ThermalHazeScale == right.ThermalHazeScale &&
                       left.HasExposureState == right.HasExposureState;
            }

            private static float ResolveThermalHazeIntensity(float configuredIntensity)
            {
                float intensity = math.max(0f, configuredIntensity);
                if (intensity <= 0f)
                    return 0f;

                float3 velocity = ResolvePlayerVelocity();
                return math.lengthsq(velocity) > ThermalHazeMotionCullSpeedMetersPerSecondSq ? 0f : intensity;
            }

            private static float ResolveUnderwaterNoirBlend()
            {
                var underwaterVisuals = GlobalRegistry.UnderwaterVisuals;
                if (underwaterVisuals != null && underwaterVisuals.IsUnderwater)
                    return 1f;

                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
                if (playerMovement == null)
                    return 0f;

                if (playerMovement.IsPlayerSubmerged)
                    return 1f;

                return math.saturate(
                    (playerMovement.CurrentDepth - SurfaceNoirSuppressionDepth) /
                    math.max(0.0001f, UnderwaterNoirFullDepth - SurfaceNoirSuppressionDepth));
            }

            private static float3 ResolvePlayerVelocity()
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext == null)
                    return default;

                HectonPlayerMovement playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                    return ToFloat3(playerMovement.InterpolatedLinearVelocity);

                return playerContext.PlayerRigidbody != null
                    ? ToFloat3(playerContext.PlayerRigidbody.linearVelocity)
                    : default;
            }

            private static float3 ToFloat3(Vector3 value)
            {
                float3 result;
                result.x = value.x;
                result.y = value.y;
                result.z = value.z;
                return result;
            }

            private static Color ResolveNoirLiftColor(Color configured)
            {
                configured.r = math.max(configured.r, DefaultNoirLiftFloor.r);
                configured.g = math.max(configured.g, DefaultNoirLiftFloor.g);
                configured.b = math.max(configured.b, DefaultNoirLiftFloor.b);
                configured.a = 1f;
                return configured;
            }

            private struct MaterialUploadCache
            {
                internal Material Material;
                internal MaterialParameterState Parameters;
                internal float PassMode;
                internal bool HasState;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct MaterialParameterState
            {
                internal float RenderScale;
                internal float MaxRayDistance;
                internal float ScatteringAnisotropy;
                internal float Density;
                internal float IgnJitter;
                internal float BilateralDepthSigma;
                internal float ShaftIntensity;
                internal float BiolumPatternScale;
                internal float BiolumProjectionStrength;
                internal float SiltStrength;
                internal float SiltNoiseScale;
                internal float SiltFloorBoost;
                internal float SiltDriftSpeed;
                internal float ContactShadowStrength;
                internal float ContactShadowSteps;
                internal float ContactShadowBias;
                internal float ContactShadowMaxDistance;
                internal float FlashlightShadowSteps;
                internal float FlashlightShadowSoftness;
                internal float FlashlightShadowMinStep;
                internal float FlashlightShadowBias;
                internal float FlashlightShadowFloor;
                internal float NoirPower;
                internal float NoirFogDensity;
                internal Color NoirLiftColor;
                internal float LensGhostIntensity;
                internal float LensGhostScale;
                internal float LensChromaticAberration;
                internal float LensEdgeWeight;
                internal float LensDirtIntensity;
                internal float CondensationIntensity;
                internal float ThermalHazeIntensity;
                internal float ThermalHazeScale;
                internal float HasExposureState;

                internal static MaterialParameterState Resolve(
                    FeatureSettings settings,
                    bool exposureAvailable,
                    float underwaterNoirBlend)
                {
                    MaterialParameterState state;
                    float underwaterBlend = math.saturate(underwaterNoirBlend);
                    state.RenderScale = math.clamp(settings.renderScale, 0.25f, 1f);
                    state.MaxRayDistance = math.max(1f, settings.maxRayDistance);
                    state.ScatteringAnisotropy = math.clamp(settings.scatteringAnisotropy, 0f, 0.95f);
                    state.Density = math.max(0f, settings.density) * underwaterBlend;
                    state.IgnJitter = math.saturate(settings.ignJitter);
                    state.BilateralDepthSigma = math.max(0.01f, settings.bilateralDepthSigma);
                    state.ShaftIntensity = math.max(0f, settings.shaftIntensity) * underwaterBlend;
                    state.BiolumPatternScale = math.max(0.001f, settings.biolumPatternScale);
                    state.BiolumProjectionStrength = math.max(0f, settings.biolumProjectionStrength) * underwaterBlend;
                    state.SiltStrength = math.max(0f, settings.siltStrength) * underwaterBlend;
                    state.SiltNoiseScale = math.max(0.001f, settings.siltNoiseScale);
                    state.SiltFloorBoost = math.max(0f, settings.siltFloorBoost);
                    state.SiltDriftSpeed = math.max(0f, settings.siltDriftSpeed);
                    state.ContactShadowStrength = math.saturate(settings.contactShadowStrength) * underwaterBlend;
                    state.ContactShadowSteps = math.clamp(settings.contactShadowSteps, 4, 8);
                    state.ContactShadowBias = math.max(0.001f, settings.contactShadowBias);
                    state.ContactShadowMaxDistance = math.max(0.1f, settings.contactShadowMaxDistance);
                    state.FlashlightShadowSteps = SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 2048 ? 16f : 24f;
                    state.FlashlightShadowSoftness = math.max(0.1f, settings.flashlightShadowSoftness);
                    state.FlashlightShadowMinStep = math.max(0.005f, settings.flashlightShadowMinStep);
                    state.FlashlightShadowBias = math.max(0.001f, settings.flashlightShadowBias);
                    state.FlashlightShadowFloor = math.clamp(settings.flashlightShadowFloor, 0.02f, 0.25f);
                    state.NoirPower = math.max(0.5f, settings.noirPower);
                    state.NoirFogDensity = math.max(0.0001f, settings.noirFogDensity) * underwaterBlend;
                    state.NoirLiftColor = ResolveNoirLiftColor(settings.noirLiftColor);
                    state.LensGhostIntensity = math.max(0f, settings.lensGhostIntensity) * underwaterBlend;
                    state.LensGhostScale = math.max(0.001f, settings.lensGhostScale);
                    state.LensChromaticAberration = math.max(0f, settings.lensChromaticAberration);
                    state.LensEdgeWeight = math.max(0f, settings.lensEdgeWeight);
                    state.LensDirtIntensity = math.saturate(settings.lensDirtIntensity);
                    state.CondensationIntensity = math.saturate(settings.condensationIntensity) * underwaterBlend;
                    state.ThermalHazeIntensity = ResolveThermalHazeIntensity(settings.thermalHazeIntensity) * underwaterBlend;
                    state.ThermalHazeScale = math.max(0.001f, settings.thermalHazeScale);
                    state.HasExposureState = exposureAvailable ? 1f : 0f;
                    return state;
                }
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
            internal static readonly int IgnJitterId = Shader.PropertyToID("_HectonShaftIgnJitter");
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
            internal static readonly int LensDirtIntensityId = Shader.PropertyToID("_HectonLensDirtIntensity");
            internal static readonly int CondensationIntensityId = Shader.PropertyToID("_HectonCondensationIntensity");
            internal static readonly int ThermalHazeIntensityId = Shader.PropertyToID("_HectonThermalHazeIntensity");
            internal static readonly int ThermalHazeScaleId = Shader.PropertyToID("_HectonThermalHazeScale");
            internal static readonly int HasExposureStateId = Shader.PropertyToID("_HectonHasExposureState");
            internal static readonly int ShaftTextureId = Shader.PropertyToID("_HectonShaftsTexture");
            internal static readonly int HalfResDepthTextureId = Shader.PropertyToID("_HectonHalfResDepthTexture");
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

            Shader shader = settings != null ? settings.shader : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shader == null)
                shader = Shader.Find("Hidden/Hecton8/ScooterVolumetricShafts");
#endif

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
