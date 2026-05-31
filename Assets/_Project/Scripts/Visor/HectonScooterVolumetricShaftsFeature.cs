using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
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
    public sealed class HectonScooterVolumetricShaftsFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_ScooterVolumetricShafts.shader";
#endif
        private const string AutoExposureComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_NoirAutoExposure.compute";
        private const int HistogramBinCount = 64;
        private const int ShaftGlobalsStrideBytes = 176;
        private const int MaterialParameterStateSizeBytes = 152;
        private const float ExposureStateDefaultMultiplier = 1f;
        private const uint MaxKernelThreadProduct = 256u;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const float ThermalHazeMotionCullSpeedMetersPerSecondSq = 225f;
        private const uint KccVelocityShaftMaxAgeFrames = 12u;
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

            [Tooltip("Where the volumetric shaft pass is injected into URP. Before transparents keeps the ocean surface and camera-space UI on top of the shaft composite.")]
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

            [Tooltip("Maximum depth samples used for screen-space contact shadows. Runtime scales this continuously with GlobalQualityWeight.")]
            [Range(1, 3)] public int contactShadowSteps = 3;

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
                internal int dispatchX;
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
                internal int dispatchX;
                internal BufferHandle histogram;
                internal BufferHandle exposureState;
                internal float minEv;
                internal float maxEv;
                internal float adaptationRate;
                internal float deltaTime;
                internal float maxDeltaPerFrame;
            }

            private sealed class ShaftFullscreenPassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle shafts;
                internal TextureHandle halfResDepth;
                internal BufferHandle shaftGlobals;
                internal BufferHandle exposureState;
                internal Material material;
                internal int shaderPassIndex;
                internal bool bindDepth;
                internal bool bindShafts;
                internal bool bindHalfResDepth;
                internal bool bindExposureState;
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
            private GraphicsBuffer _shaftGlobalsBuffer;
            private GraphicsBuffer _shaftGlobalsBufferA;
            private GraphicsBuffer _shaftGlobalsBufferB;
            private MaterialParameterState _shaftGlobalsCache;
            private int _shaftGlobalsWriteIndex;
            private bool _hasShaftGlobalsCache;
            private int _clearHistogramKernel = -1;
            private int _buildHistogramKernel = -1;
            private int _resolveExposureKernel = -1;
            private uint _clearHistogramThreadGroupSizeX;
            private uint _buildThreadGroupSizeX;
            private uint _buildThreadGroupSizeY;
            private uint _resolveExposureThreadGroupSizeX;
            private HectonUnderwaterVisuals _underwaterVisuals;
            private IPlayerRuntimeContext _playerContext;
            private float _cachedLowVramPressure01;
            private bool _supportsSetConstantBuffer;
            private bool _supportsComputeShaders;

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
                Material compositeMaterial,
                HectonUnderwaterVisuals underwaterVisuals,
                IPlayerRuntimeContext playerContext)
            {
                _settings = settings;
                _raymarchMaterial = raymarchMaterial;
                _blurHorizontalMaterial = blurHorizontalMaterial;
                _blurVerticalMaterial = blurVerticalMaterial;
                _compositeMaterial = compositeMaterial;
                _underwaterVisuals = underwaterVisuals;
                _playerContext = playerContext;
                SetAutoExposureComputeShader(settings != null ? settings.autoExposureComputeShader : null);
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;

                if (_clearHistogramKernel < 0)
                {
                    TryInitializeAutoExposureKernels();
                }
            }

            public bool PrepareResources(FeatureSettings settings)
            {
                SetAutoExposureComputeShader(settings != null ? settings.autoExposureComputeShader : null);
                if (_clearHistogramKernel < 0)
                    TryInitializeAutoExposureKernels();
                EnsureAutoExposureResources();
                return EnsureShaftGlobalsBuffer();
            }

            public void SetGraphicsCapabilitiesCold(
                bool supportsSetConstantBuffer,
                bool supportsComputeShaders,
                float lowVramPressure01)
            {
                _supportsSetConstantBuffer = supportsSetConstantBuffer;
                _supportsComputeShaders = supportsComputeShaders;
                _cachedLowVramPressure01 = math.saturate(lowVramPressure01);
                if (!_supportsSetConstantBuffer)
                    Dispose();
                if (!_supportsComputeShaders)
                    DisableAutoExposure();
            }

            public void Dispose()
            {
                _histogramBuffer?.Release();
                _exposureStateBuffer?.Release();
                _shaftGlobalsBufferA?.Release();
                _shaftGlobalsBufferB?.Release();
                _histogramBuffer = null;
                _exposureStateBuffer = null;
                _shaftGlobalsBufferA = null;
                _shaftGlobalsBufferB = null;
                _shaftGlobalsBuffer = null;
                _shaftGlobalsCache = default;
                _shaftGlobalsWriteIndex = 0;
                _hasShaftGlobalsCache = false;
                _underwaterVisuals = null;
                _playerContext = null;
                _clearHistogramKernel = -1;
                _buildHistogramKernel = -1;
                _resolveExposureKernel = -1;
                _clearHistogramThreadGroupSizeX = 0u;
                _buildThreadGroupSizeX = 0u;
                _buildThreadGroupSizeY = 0u;
                _resolveExposureThreadGroupSizeX = 0u;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner())
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
                float globalQualityWeight = ResolveGlobalQualityWeight01();
                float lowVramPressure01 = ResolveLowVramPressure01();
                float visualBudgetPressure01 = CombineVisualBudgetPressure(
                    lowVramPressure01,
                    HectonDrsRenderFeatureGate.ResolveSurvivalPressure01());
                float resolvedRenderScale = ResolveQualityScaledRenderScale(
                    _settings.renderScale,
                    globalQualityWeight,
                    visualBudgetPressure01);
                int sourceWidth = math.max(1, sourceDesc.width);
                int sourceHeight = math.max(1, sourceDesc.height);
                int shaftWidth = math.max(1, (int)math.round(sourceWidth * resolvedRenderScale));
                int shaftHeight = math.max(1, (int)math.round(sourceHeight * resolvedRenderScale));

                TextureDesc shaftDesc = sourceDesc;
                shaftDesc.name = "_HectonScooterVolumetricShafts";
                shaftDesc.width = shaftWidth;
                shaftDesc.height = shaftHeight;
                shaftDesc.depthBufferBits = DepthBits.None;
                shaftDesc.msaaSamples = MSAASamples.None;
                shaftDesc.colorFormat = sourceDesc.colorFormat;
                shaftDesc.clearBuffer = true;
                shaftDesc.clearColor = ShaftClearColor;
                shaftDesc.filterMode = FilterMode.Bilinear;
                shaftDesc.useMipMap = false;
                shaftDesc.autoGenerateMips = false;

                TextureDesc blurDesc = shaftDesc;
                blurDesc.name = "_HectonScooterVolumetricShaftsBlur";

                TextureDesc halfResDepthDesc = shaftDesc;
                halfResDepthDesc.name = "_HectonHalfResContactDepth";
                halfResDepthDesc.colorFormat = GraphicsFormat.R32_SFloat;
                halfResDepthDesc.clearColor = Color.white;
                halfResDepthDesc.filterMode = FilterMode.Point;

                TextureDesc compositeDesc = sourceDesc;
                compositeDesc.name = "_HectonScooterVolumetricShaftsComposite";
                compositeDesc.clearBuffer = false;
                compositeDesc.depthBufferBits = DepthBits.None;
                compositeDesc.msaaSamples = MSAASamples.None;
                compositeDesc.colorFormat = sourceDesc.colorFormat;

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
                int exposureClearDispatchX = ResolveDispatchGroups(1, _clearHistogramThreadGroupSizeX);
                int exposureDispatchX = ResolveDispatchGroups(sourceWidth, _buildThreadGroupSizeX);
                int exposureDispatchY = ResolveDispatchGroups(sourceHeight, _buildThreadGroupSizeY);
                int exposureResolveDispatchX = ResolveDispatchGroups(1, _resolveExposureThreadGroupSizeX);
                if (exposureAvailable && (exposureClearDispatchX <= 0 || exposureDispatchX <= 0 || exposureDispatchY <= 0 || exposureResolveDispatchX <= 0))
                    exposureAvailable = false;

                if (exposureAvailable)
                {
                    histogramHandle = renderGraph.ImportBuffer(_histogramBuffer);
                    exposureStateHandle = renderGraph.ImportBuffer(_exposureStateBuffer);

                    using (var builder = renderGraph.AddComputePass("Hecton Noir Exposure Clear", out ExposureClearPassData passData, _profilingSampler))
                    {
                        passData.computeShader = _autoExposureComputeShader;
                        passData.kernelIndex = _clearHistogramKernel;
                        passData.dispatchX = exposureClearDispatchX;
                        passData.histogram = histogramHandle;

                        builder.UseBuffer(histogramHandle, AccessFlags.Write);
                        builder.SetRenderFunc(static (ExposureClearPassData data, ComputeGraphContext context) =>
                        {
                            context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.HistogramBufferId, data.histogram);
                            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, 1, 1);
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
                        builder.SetRenderFunc(static (ExposureBuildPassData data, ComputeGraphContext context) =>
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
                        passData.dispatchX = exposureResolveDispatchX;
                        passData.histogram = histogramHandle;
                        passData.exposureState = exposureStateHandle;
                        passData.minEv = resolvedMinEv;
                        passData.maxEv = resolvedMaxEv;
                        passData.adaptationRate = math.max(0.01f, _settings.exposureAdaptationRate);
                        passData.deltaTime = ExposureFixedDeltaSeconds;
                        passData.maxDeltaPerFrame = math.clamp(_settings.evMaxDeltaPerFrame, 0.05f, 0.5f);

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
                            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, 1, 1);
                        });
                    }
                }

                MaterialParameterState materialParameters = MaterialParameterState.Resolve(
                    _settings,
                    exposureAvailable,
                    underwaterNoirBlend,
                    ResolveThermalHazeIntensity(_settings.thermalHazeIntensity),
                    resolvedRenderScale,
                    globalQualityWeight,
                    visualBudgetPressure01);
                if (!UpdateShaftGlobals(in materialParameters))
                    return;
                BufferHandle shaftGlobalsHandle = renderGraph.ImportBuffer(_shaftGlobalsBuffer);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Underwater Noir Half-Res Contact Depth",
                    sourceTexture,
                    depthTexture,
                    default,
                    default,
                    halfResDepthTexture,
                    shaftGlobalsHandle,
                    default,
                    _raymarchMaterial,
                    HalfResContactDepthPassIndex,
                    true,
                    false,
                    false,
                    false);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Underwater Noir Radial Shafts",
                    sourceTexture,
                    depthTexture,
                    default,
                    default,
                    shaftsTexture,
                    shaftGlobalsHandle,
                    exposureStateHandle,
                    _raymarchMaterial,
                    0,
                    true,
                    false,
                    false,
                    exposureAvailable);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Underwater Noir Blur Horizontal",
                    shaftsTexture,
                    depthTexture,
                    default,
                    default,
                    blurTexture,
                    shaftGlobalsHandle,
                    exposureStateHandle,
                    _blurHorizontalMaterial,
                    1,
                    true,
                    false,
                    false,
                    exposureAvailable);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Underwater Noir Blur Vertical",
                    blurTexture,
                    depthTexture,
                    default,
                    default,
                    shaftsTexture,
                    shaftGlobalsHandle,
                    exposureStateHandle,
                    _blurVerticalMaterial,
                    2,
                    true,
                    false,
                    false,
                    exposureAvailable);

                RecordFullscreenPass(
                    renderGraph,
                    "Hecton Underwater Noir Composite",
                    sourceTexture,
                    depthTexture,
                    shaftsTexture,
                    halfResDepthTexture,
                    compositeTexture,
                    shaftGlobalsHandle,
                    exposureStateHandle,
                    _compositeMaterial,
                    3,
                    true,
                    true,
                    true,
                    exposureAvailable);

                resourceData.cameraColor = compositeTexture;
            }

            private void RecordFullscreenPass(
                RenderGraph renderGraph,
                string passName,
                TextureHandle source,
                TextureHandle depth,
                TextureHandle shafts,
                TextureHandle halfResDepth,
                TextureHandle destination,
                BufferHandle shaftGlobals,
                BufferHandle exposureState,
                Material material,
                int shaderPassIndex,
                bool bindDepth,
                bool bindShafts,
                bool bindHalfResDepth,
                bool bindExposureState)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ShaftFullscreenPassData>(
                           passName,
                           out ShaftFullscreenPassData passData,
                           _profilingSampler))
                {
                    passData.source = source;
                    passData.depth = depth;
                    passData.shafts = shafts;
                    passData.halfResDepth = halfResDepth;
                    passData.shaftGlobals = shaftGlobals;
                    passData.exposureState = exposureState;
                    passData.material = material;
                    passData.shaderPassIndex = shaderPassIndex;
                    passData.bindDepth = bindDepth;
                    passData.bindShafts = bindShafts;
                    passData.bindHalfResDepth = bindHalfResDepth;
                    passData.bindExposureState = bindExposureState;

                    builder.UseTexture(source, AccessFlags.Read);
                    if (bindDepth)
                        builder.UseTexture(depth, AccessFlags.Read);
                    if (bindShafts)
                        builder.UseTexture(shafts, AccessFlags.Read);
                    if (bindHalfResDepth)
                        builder.UseTexture(halfResDepth, AccessFlags.Read);
                    builder.UseBuffer(shaftGlobals, AccessFlags.Read);
                    if (bindExposureState)
                        builder.UseBuffer(exposureState, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (ShaftFullscreenPassData data, RasterGraphContext context) =>
                    {
                        if (data.material == null)
                            return;

                        GraphicsBuffer constants = data.shaftGlobals;
                        if (constants == null || !constants.IsValid())
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.source);
                        if (data.bindDepth)
                            context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.depth);
                        if (data.bindShafts)
                            context.cmd.SetGlobalTexture(ShaderConstants.ShaftTextureId, data.shafts);
                        if (data.bindHalfResDepth)
                            context.cmd.SetGlobalTexture(ShaderConstants.HalfResDepthTextureId, data.halfResDepth);
                        if (data.bindExposureState)
                        {
                            GraphicsBuffer exposure = data.exposureState;
                            if (exposure != null && exposure.IsValid())
                                context.cmd.SetGlobalBuffer(ShaderConstants.ExposureStateBufferId, exposure);
                        }

                        context.cmd.SetGlobalConstantBuffer(
                            constants,
                            ShaderConstants.ShaftGlobalsBufferId,
                            0,
                            ShaftGlobalsStrideBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.shaderPassIndex);
                    });
                }
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

                try
                {
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
                        try
                        {
                            mapped[0] = exposureState;
                        }
                        finally
                        {
                            _exposureStateBuffer.UnlockBufferAfterWrite<Vector4>(1);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    ReleaseAutoExposureResources();
                    return;
                }
                catch (InvalidOperationException)
                {
                    ReleaseAutoExposureResources();
                    return;
                }
                catch (ArgumentException)
                {
                    ReleaseAutoExposureResources();
                    return;
                }
                catch (NotSupportedException)
                {
                    ReleaseAutoExposureResources();
                    return;
                }
                catch (OutOfMemoryException)
                {
                    ReleaseAutoExposureResources();
                    return;
                }

                EnsureShaftGlobalsBuffer();
            }

            private void TryInitializeAutoExposureKernels()
            {
                if (_autoExposureComputeShader == null || !_supportsComputeShaders)
                {
                    DisableAutoExposure();
                    return;
                }

                if (!TryFindKernel(_autoExposureComputeShader, "ClearHistogram", out int clearHistogramKernel) ||
                    !TryFindKernel(_autoExposureComputeShader, "BuildHistogram", out int buildHistogramKernel) ||
                    !TryFindKernel(_autoExposureComputeShader, "ResolveExposure", out int resolveExposureKernel))
                {
                    DisableAutoExposure();
                    return;
                }

                _clearHistogramKernel = clearHistogramKernel;
                _buildHistogramKernel = buildHistogramKernel;
                _resolveExposureKernel = resolveExposureKernel;
                if (!TryResolveKernelThreadGroupSizeX(_autoExposureComputeShader, _clearHistogramKernel, out _clearHistogramThreadGroupSizeX) ||
                    !TryResolveBuildHistogramThreadGroups(_autoExposureComputeShader, _buildHistogramKernel, out _buildThreadGroupSizeX, out _buildThreadGroupSizeY) ||
                    !TryResolveKernelThreadGroupSizeX(_autoExposureComputeShader, _resolveExposureKernel, out _resolveExposureThreadGroupSizeX))
                {
                    DisableAutoExposure();
                }
            }

            private void SetAutoExposureComputeShader(ComputeShader computeShader)
            {
                if (ReferenceEquals(_autoExposureComputeShader, computeShader))
                    return;

                _autoExposureComputeShader = computeShader;
                _clearHistogramKernel = -1;
                _buildHistogramKernel = -1;
                _resolveExposureKernel = -1;
                _clearHistogramThreadGroupSizeX = 0u;
                _buildThreadGroupSizeX = 0u;
                _buildThreadGroupSizeY = 0u;
                _resolveExposureThreadGroupSizeX = 0u;
                ReleaseAutoExposureResources();
            }

            private void DisableAutoExposure()
            {
                _autoExposureComputeShader = null;
                _clearHistogramKernel = -1;
                _buildHistogramKernel = -1;
                _resolveExposureKernel = -1;
                _clearHistogramThreadGroupSizeX = 0u;
                _buildThreadGroupSizeX = 0u;
                _buildThreadGroupSizeY = 0u;
                _resolveExposureThreadGroupSizeX = 0u;
                ReleaseAutoExposureResources();
            }

            private static bool TryFindKernel(ComputeShader computeShader, string kernelName, out int kernel)
            {
                kernel = -1;
                if (computeShader == null)
                    return false;

                try
                {
                    if (!computeShader.HasKernel(kernelName))
                        return false;

                    kernel = computeShader.FindKernel(kernelName);
                    return kernel >= 0;
                }
                catch (ObjectDisposedException)
                {
                    kernel = -1;
                    return false;
                }
                catch (InvalidOperationException)
                {
                    kernel = -1;
                    return false;
                }
                catch (ArgumentException)
                {
                    kernel = -1;
                    return false;
                }
                catch (MissingReferenceException)
                {
                    kernel = -1;
                    return false;
                }
                catch (UnityException)
                {
                    kernel = -1;
                    return false;
                }
            }

            private void ReleaseAutoExposureResources()
            {
                _histogramBuffer?.Release();
                _exposureStateBuffer?.Release();
                _histogramBuffer = null;
                _exposureStateBuffer = null;
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

            private static bool TryResolveKernelThreadGroupSizeX(ComputeShader computeShader, int kernelIndex, out uint groupSizeX)
            {
                groupSizeX = 0u;
                if (!TryValidateKernelThreadGroups(computeShader, kernelIndex, out uint x, out uint y, out uint z))
                    return false;
                if (y != 1u || z != 1u)
                    return false;

                groupSizeX = x;
                return true;
            }

            private static bool TryResolveBuildHistogramThreadGroups(ComputeShader computeShader, int kernelIndex, out uint groupSizeX, out uint groupSizeY)
            {
                groupSizeX = 0u;
                groupSizeY = 0u;
                if (!TryValidateKernelThreadGroups(computeShader, kernelIndex, out uint x, out uint y, out _))
                    return false;

                groupSizeX = x;
                groupSizeY = y;
                return true;
            }

            private static bool TryValidateKernelThreadGroups(ComputeShader computeShader, int kernelIndex)
            {
                return TryValidateKernelThreadGroups(computeShader, kernelIndex, out _, out _, out _);
            }

            private static bool TryValidateKernelThreadGroups(ComputeShader computeShader, int kernelIndex, out uint x, out uint y, out uint z)
            {
                x = 0u;
                y = 0u;
                z = 0u;
                if (computeShader == null || kernelIndex < 0)
                    return false;

                try
                {
                    if (!computeShader.IsSupported(kernelIndex))
                        return false;

                    computeShader.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
                }
                catch (ObjectDisposedException)
                {
                    x = 0u;
                    y = 0u;
                    z = 0u;
                    return false;
                }
                catch (InvalidOperationException)
                {
                    x = 0u;
                    y = 0u;
                    z = 0u;
                    return false;
                }
                catch (ArgumentException)
                {
                    x = 0u;
                    y = 0u;
                    z = 0u;
                    return false;
                }
                catch (MissingReferenceException)
                {
                    x = 0u;
                    y = 0u;
                    z = 0u;
                    return false;
                }
                catch (UnityException)
                {
                    x = 0u;
                    y = 0u;
                    z = 0u;
                    return false;
                }

                ulong threadProduct = (ulong)x * y * z;
                if (x == 0u || y == 0u || z == 0u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return false;

                return true;
            }

            private static int ResolveDispatchGroups(int value, uint groupSize)
            {
                if (value <= 0 || groupSize == 0u)
                    return 0;

                long groups = ((long)value + groupSize - 1L) / groupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
            }

            private bool EnsureShaftGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                    return false;

                if (_shaftGlobalsBufferA != null && _shaftGlobalsBufferA.IsValid() &&
                    _shaftGlobalsBufferB != null && _shaftGlobalsBufferB.IsValid())
                {
                    if (_shaftGlobalsBuffer == null)
                        _shaftGlobalsBuffer = _shaftGlobalsBufferA;
                    return true;
                }

                _shaftGlobalsBufferA?.Release();
                _shaftGlobalsBufferB?.Release();
                try
                {
                    _shaftGlobalsBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        ShaftGlobalsStrideBytes); // COLD ALLOC: GraphicsBuffer[176B] - URP noir shaft global CBuffer A - owner: ShaftsPass
                    _shaftGlobalsBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        ShaftGlobalsStrideBytes); // COLD ALLOC: GraphicsBuffer[176B] - URP noir shaft global CBuffer B - owner: ShaftsPass
                }
                catch (ArgumentException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }
                catch (NotSupportedException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }
                catch (OutOfMemoryException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }
                _shaftGlobalsBuffer = _shaftGlobalsBufferA;
                _shaftGlobalsWriteIndex = 1;
                _hasShaftGlobalsCache = false;
                return _shaftGlobalsBufferA.IsValid() && _shaftGlobalsBufferB.IsValid();
            }

            private bool UpdateShaftGlobals(in MaterialParameterState parameters)
            {
                if (!HasShaftGlobalsBuffer())
                    return false;

                if (_hasShaftGlobalsCache && MaterialParametersEqual(in _shaftGlobalsCache, in parameters))
                {
                    return _shaftGlobalsBuffer != null && _shaftGlobalsBuffer.IsValid();
                }

                GraphicsBuffer writeBuffer = (_shaftGlobalsWriteIndex & 1) == 0 ? _shaftGlobalsBufferA : _shaftGlobalsBufferB;
                if (writeBuffer == null || !writeBuffer.IsValid())
                    return false;

                try
                {
                    NativeArray<ShaftGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<ShaftGlobalsDTO>(0, 1);
                    try
                    {
                        mapped[0] = ShaftGlobalsDTO.FromParameters(in parameters);
                    }
                    finally
                    {
                        writeBuffer.UnlockBufferAfterWrite<ShaftGlobalsDTO>(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }
                catch (ArgumentException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }
                catch (NotSupportedException)
                {
                    MarkShaftGlobalsUnavailable();
                    return false;
                }

                _shaftGlobalsBuffer = writeBuffer;
                _shaftGlobalsWriteIndex ^= 1;
                _shaftGlobalsCache = parameters;
                _hasShaftGlobalsCache = true;
                return _shaftGlobalsBuffer != null && _shaftGlobalsBuffer.IsValid();
            }

            private bool HasShaftGlobalsBuffer()
            {
                if (!_supportsSetConstantBuffer)
                    return false;

                if (_shaftGlobalsBufferA == null || !_shaftGlobalsBufferA.IsValid() ||
                    _shaftGlobalsBufferB == null || !_shaftGlobalsBufferB.IsValid())
                {
                    return false;
                }

                if (_shaftGlobalsBuffer == null || !_shaftGlobalsBuffer.IsValid())
                    _shaftGlobalsBuffer = _shaftGlobalsBufferA;
                return true;
            }

            private void MarkShaftGlobalsUnavailable()
            {
                _shaftGlobalsBuffer = null;
                _hasShaftGlobalsCache = false;
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

            private float ResolveThermalHazeIntensity(float configuredIntensity)
            {
                float intensity = math.max(0f, configuredIntensity);
                if (intensity <= 0f)
                    return 0f;

                float3 velocity = ResolvePlayerVelocity();
                return math.lengthsq(velocity) > ThermalHazeMotionCullSpeedMetersPerSecondSq ? 0f : intensity;
            }

            private float ResolveUnderwaterNoirBlend()
            {
                HectonUnderwaterVisuals underwaterVisuals = _underwaterVisuals;
                if (underwaterVisuals != null && underwaterVisuals.IsUnderwater)
                    return 1f;

                IPlayerRuntimeContext playerContext = _playerContext;
                var playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
                if (playerMovement == null)
                    return 0f;

                if (playerMovement.IsPlayerSubmerged)
                    return 1f;

                return math.saturate(
                    (playerMovement.CurrentDepth - SurfaceNoirSuppressionDepth) /
                    math.max(0.0001f, UnderwaterNoirFullDepth - SurfaceNoirSuppressionDepth));
            }

            private static float ResolveGlobalQualityWeight01()
            {
                float quality = HomeostasisBrain.GlobalQualityWeight;
                return math.saturate(math.isfinite(quality) ? quality : 1f);
            }

            private float ResolveLowVramPressure01()
            {
                return _cachedLowVramPressure01;
            }

            private static float CombineVisualBudgetPressure(float lowVramPressure01, float drsSurvivalPressure01)
            {
                float low = math.saturate(lowVramPressure01);
                float drs = math.saturate(drsSurvivalPressure01);
                return 1f - ((1f - low) * (1f - drs));
            }

            private static float ResolveQualityScaledRenderScale(
                float authoredRenderScale,
                float globalQualityWeight,
                float visualBudgetPressure01)
            {
                float authored = math.clamp(authoredRenderScale, 0.25f, 1f);
                float qualityCurve = Smooth01(globalQualityWeight);
                float survivalMultiplier = math.lerp(0.72f, 0.54f, math.saturate(visualBudgetPressure01));
                float survivalScale = math.max(0.25f, authored * survivalMultiplier);
                float budgetPressure = math.saturate(visualBudgetPressure01);
                float overkillTarget = math.lerp(math.max(authored, 0.75f), authored, budgetPressure * 0.45f);
                float overkillScale = math.lerp(
                    authored,
                    overkillTarget,
                    math.saturate((qualityCurve - 0.72f) * 3.5714285f));
                return math.clamp(math.lerp(survivalScale, overkillScale, qualityCurve), 0.25f, 1f);
            }

            private static float ResolveContactShadowStepBudget(
                int authoredContactShadowSteps,
                float globalQualityWeight,
                float visualBudgetPressure01)
            {
                float authored = math.clamp(authoredContactShadowSteps, 1, 3);
                float qualityCurve = Smooth01(globalQualityWeight) * math.lerp(1f, 0.68f, math.saturate(visualBudgetPressure01));
                return math.clamp(math.lerp(1f, authored, qualityCurve), 1f, 3f);
            }

            private static float ResolveFlashlightShadowStepBudget(float globalQualityWeight, float visualBudgetPressure01)
            {
                float qualityCurve = Smooth01(globalQualityWeight) * math.lerp(1f, 0.62f, math.saturate(visualBudgetPressure01));
                return math.clamp(math.lerp(1f, 5f, qualityCurve), 1f, 5f);
            }

            private static float Smooth01(float value)
            {
                float t = math.saturate(value);
                return t * t * (3f - 2f * t);
            }

            private float3 ResolvePlayerVelocity()
            {
                IPlayerRuntimeContext playerContext = _playerContext;
                if (playerContext == null)
                    return default;

                var playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                    return ToFloat3(playerMovement.InterpolatedLinearVelocity);

                return CoreDeterminismSignals.TryGetLatestKccVelocityFloat3(KccVelocityShaftMaxAgeFrames, out float3 velocity)
                    ? velocity
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

            [StructLayout(LayoutKind.Explicit, Size = ShaftGlobalsStrideBytes)]
            private struct ShaftGlobalsDTO
            {
                [FieldOffset(0)]
                internal Vector4 PassRenderRayDistance;

                [FieldOffset(16)]
                internal Vector4 ScatteringDensityIgnBilateral;

                [FieldOffset(32)]
                internal Vector4 ShaftBiolumSilt;

                [FieldOffset(48)]
                internal Vector4 SiltContact;

                [FieldOffset(64)]
                internal Vector4 ContactFlashlight;

                [FieldOffset(80)]
                internal Vector4 FlashlightParams;

                [FieldOffset(96)]
                internal Vector4 NoirPowerDensityPad;

                [FieldOffset(112)]
                internal Vector4 NoirLiftColor;

                [FieldOffset(128)]
                internal Vector4 LensGhostChromatic;

                [FieldOffset(144)]
                internal Vector4 LensThermal;

                [FieldOffset(160)]
                internal Vector4 ExposurePad;

                internal static ShaftGlobalsDTO FromParameters(in MaterialParameterState parameters)
                {
                    ShaftGlobalsDTO dto;
                    dto.PassRenderRayDistance = new Vector4(0f, parameters.RenderScale, 0f, parameters.MaxRayDistance);
                    dto.ScatteringDensityIgnBilateral = new Vector4(parameters.ScatteringAnisotropy, parameters.Density, parameters.IgnJitter, parameters.BilateralDepthSigma);
                    dto.ShaftBiolumSilt = new Vector4(parameters.ShaftIntensity, parameters.BiolumPatternScale, parameters.BiolumProjectionStrength, parameters.SiltStrength);
                    dto.SiltContact = new Vector4(parameters.SiltNoiseScale, parameters.SiltFloorBoost, parameters.SiltDriftSpeed, parameters.ContactShadowStrength);
                    dto.ContactFlashlight = new Vector4(parameters.ContactShadowSteps, parameters.ContactShadowBias, parameters.ContactShadowMaxDistance, parameters.FlashlightShadowSteps);
                    dto.FlashlightParams = new Vector4(parameters.FlashlightShadowSoftness, parameters.FlashlightShadowMinStep, parameters.FlashlightShadowBias, parameters.FlashlightShadowFloor);
                    dto.NoirPowerDensityPad = new Vector4(parameters.NoirPower, parameters.NoirFogDensity, 0f, 0f);
                    dto.NoirLiftColor = parameters.NoirLiftColor;
                    dto.LensGhostChromatic = new Vector4(parameters.LensGhostIntensity, parameters.LensGhostScale, parameters.LensChromaticAberration, parameters.LensEdgeWeight);
                    dto.LensThermal = new Vector4(parameters.LensDirtIntensity, parameters.CondensationIntensity, parameters.ThermalHazeIntensity, parameters.ThermalHazeScale);
                    dto.ExposurePad = new Vector4(parameters.HasExposureState, 0f, 0f, 0f);
                    return dto;
                }
            }

            [StructLayout(LayoutKind.Explicit, Size = MaterialParameterStateSizeBytes)]
            private struct MaterialParameterState
            {
                [FieldOffset(0)]
                internal float RenderScale;
                [FieldOffset(4)]
                internal float MaxRayDistance;
                [FieldOffset(8)]
                internal float ScatteringAnisotropy;
                [FieldOffset(12)]
                internal float Density;
                [FieldOffset(16)]
                internal float IgnJitter;
                [FieldOffset(20)]
                internal float BilateralDepthSigma;
                [FieldOffset(24)]
                internal float ShaftIntensity;
                [FieldOffset(28)]
                internal float BiolumPatternScale;
                [FieldOffset(32)]
                internal float BiolumProjectionStrength;
                [FieldOffset(36)]
                internal float SiltStrength;
                [FieldOffset(40)]
                internal float SiltNoiseScale;
                [FieldOffset(44)]
                internal float SiltFloorBoost;
                [FieldOffset(48)]
                internal float SiltDriftSpeed;
                [FieldOffset(52)]
                internal float ContactShadowStrength;
                [FieldOffset(56)]
                internal float ContactShadowSteps;
                [FieldOffset(60)]
                internal float ContactShadowBias;
                [FieldOffset(64)]
                internal float ContactShadowMaxDistance;
                [FieldOffset(68)]
                internal float FlashlightShadowSteps;
                [FieldOffset(72)]
                internal float FlashlightShadowSoftness;
                [FieldOffset(76)]
                internal float FlashlightShadowMinStep;
                [FieldOffset(80)]
                internal float FlashlightShadowBias;
                [FieldOffset(84)]
                internal float FlashlightShadowFloor;
                [FieldOffset(88)]
                internal float NoirPower;
                [FieldOffset(92)]
                internal float NoirFogDensity;
                [FieldOffset(96)]
                internal Color NoirLiftColor;
                [FieldOffset(112)]
                internal float LensGhostIntensity;
                [FieldOffset(116)]
                internal float LensGhostScale;
                [FieldOffset(120)]
                internal float LensChromaticAberration;
                [FieldOffset(124)]
                internal float LensEdgeWeight;
                [FieldOffset(128)]
                internal float LensDirtIntensity;
                [FieldOffset(132)]
                internal float CondensationIntensity;
                [FieldOffset(136)]
                internal float ThermalHazeIntensity;
                [FieldOffset(140)]
                internal float ThermalHazeScale;
                [FieldOffset(144)]
                internal float HasExposureState;
                [FieldOffset(148)]
                private float _pad0;

                internal static MaterialParameterState Resolve(
                    FeatureSettings settings,
                    bool exposureAvailable,
                    float underwaterNoirBlend,
                    float thermalHazeIntensity,
                    float resolvedRenderScale,
                    float globalQualityWeight,
                    float visualBudgetPressure01)
                {
                    MaterialParameterState state = default;
                    float underwaterBlend = math.saturate(underwaterNoirBlend);
                    state.RenderScale = math.clamp(resolvedRenderScale, 0.25f, 1f);
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
                    state.ContactShadowSteps = ResolveContactShadowStepBudget(settings.contactShadowSteps, globalQualityWeight, visualBudgetPressure01);
                    state.ContactShadowBias = math.max(0.001f, settings.contactShadowBias);
                    state.ContactShadowMaxDistance = math.max(0.1f, settings.contactShadowMaxDistance);
                    state.FlashlightShadowSteps = ResolveFlashlightShadowStepBudget(globalQualityWeight, visualBudgetPressure01);
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
                    state.ThermalHazeIntensity = math.max(0f, thermalHazeIntensity) * underwaterBlend;
                    state.ThermalHazeScale = math.max(0.001f, settings.thermalHazeScale);
                    state.HasExposureState = exposureAvailable ? 1f : 0f;
                    return state;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int ShaftGlobalsBufferId = Shader.PropertyToID("HectonScooterVolumetricShaftsGlobals");
            internal static readonly int SourceColorId = Shader.PropertyToID("_HectonSourceColor");
            internal static readonly int InputSizeId = Shader.PropertyToID("_HectonNoirInputSize");
            internal static readonly int MinEvId = Shader.PropertyToID("_HectonNoirMinEv");
            internal static readonly int MaxEvId = Shader.PropertyToID("_HectonNoirMaxEv");
            internal static readonly int ExposureAdaptationRateId = Shader.PropertyToID("_HectonNoirExposureAdaptationRate");
            internal static readonly int ExposureDeltaTimeId = Shader.PropertyToID("_HectonNoirExposureDeltaTime");
            internal static readonly int EvMaxDeltaPerFrameId = Shader.PropertyToID("_HectonNoirEVMaxDeltaPerFrame");
            internal static readonly int HistogramBufferId = Shader.PropertyToID("_HectonNoirHistogram");
            internal static readonly int ExposureStateBufferId = Shader.PropertyToID("_HectonNoirExposureState");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int ShaftTextureId = Shader.PropertyToID("_HectonShaftsTexture");
            internal static readonly int HalfResDepthTextureId = Shader.PropertyToID("_HectonHalfResDepthTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private ShaftsPass _pass;
        private Material _raymarchMaterial;
        private Material _blurHorizontalMaterial;
        private Material _blurVerticalMaterial;
        private Material _compositeMaterial;
        private HectonUnderwaterVisuals _cachedUnderwaterVisuals;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapRegistered;
        private bool _supportsSetConstantBuffer;
        private bool _supportsComputeShaders;
        private float _cachedLowVramPressure01;

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
            if (shader == null)
                RuntimeShaderReferenceCatalog.TryGetScooterVolumetricShaftsShader(out shader);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shader == null)
                shader = Shader.Find("Hidden/Hecton8/ScooterVolumetricShafts");
#endif

            _pass ??= new ShaftsPass();
            CacheGraphicsCapabilitiesCold();
            RecreateMaterial(ref _raymarchMaterial, shader);
            RecreateMaterial(ref _blurHorizontalMaterial, shader);
            RecreateMaterial(ref _blurVerticalMaterial, shader);
            RecreateMaterial(ref _compositeMaterial, shader);
            _pass.PrepareResources(settings);
            TryRegisterHotSwapListener();
            _cachedUnderwaterVisuals = GlobalRegistry.UnderwaterVisuals;
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!HectonDrsRenderFeatureGate.HasRuntimeRenderOwner())
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

            _pass.Setup(
                settings,
                _raymarchMaterial,
                _blurHorizontalMaterial,
                _blurVerticalMaterial,
                _compositeMaterial,
                _cachedUnderwaterVisuals,
                _cachedPlayerContext);
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
            _cachedUnderwaterVisuals = null;
            _cachedPlayerContext = null;
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.UnderwaterVisualsRuntime:
                    _cachedUnderwaterVisuals = currentService as HectonUnderwaterVisuals;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    break;
            }
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
            _supportsComputeShaders = SystemInfo.supportsComputeShaders;
            _cachedLowVramPressure01 = ResolveLowVramPressure01(SystemInfo.graphicsMemorySize);
            _pass?.SetGraphicsCapabilitiesCold(
                _supportsSetConstantBuffer,
                _supportsComputeShaders,
                _cachedLowVramPressure01);
        }

        private static float ResolveLowVramPressure01(int graphicsMemoryMb)
        {
            if (graphicsMemoryMb <= 0)
                return 0.35f;

            float t = math.saturate((2048f - graphicsMemoryMb) * (1f / 1536f));
            return t * t * (3f - 2f * t);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
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
