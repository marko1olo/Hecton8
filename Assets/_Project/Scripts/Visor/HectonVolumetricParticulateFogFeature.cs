using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.VFX;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
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
    /// RenderGraph volumetric fog facade: low-tier dithered proxy through high-tier 64-step particulate raymarch.
    /// </summary>
    public sealed class HectonVolumetricParticulateFogFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener, ILateFrameTickable, ISlowTickable
    {
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog.compute";
        private const string DearLieProxyShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog_DearLie.shader";
        private const string DearLieProxyShaderName = "Hidden/Hecton8/VolumetricFogDearLie";
        private const double SetupBudgetWarningMilliseconds = 0.2d;
        private const double MaxCameraLocalAupMeters = 1000000d;
        private const int ColdStateRepairCadenceFrames = 30;
        private const uint SetupWarningHash = 0xA88120F0u;
        private const uint SetupContextHash = 0xC0120F6Au;

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
        }

        private static bool IsXrActive(XRPass xr)
        {
            return xr != null && xr.enabled;
        }

        private static bool UsesSinglePassTextureArray(XRPass xr)
        {
            return IsXrActive(xr) && xr.singlePassEnabled && xr.viewCount > 1;
        }

        private static int ResolveActiveViewCount(XRPass xr)
        {
            return UsesSinglePassTextureArray(xr) ? math.max(1, math.min(2, xr.viewCount)) : 1;
        }

        private static float ResolveFiniteSaturated(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolveFiniteClamped(float value, float minimum, float maximum, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, minimum, maximum) : fallback;
        }

        private static float ResolveFiniteNonNegative(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static float ResolveQualityCurve(float quality)
        {
            return VolumetricFogParamsAccess.ResolveQualityCurve(quality);
        }

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Compute shader that owns the reduced-resolution raymarch and depth-aware composite.")]
            public ComputeShader computeShader = null;

            [Tooltip("Raster fallback shader for Dear Lie proxy and camera-format bilateral composite.")]
            public Shader dearLieProxyShader = null;

            [Tooltip("Injection point. Runs after opaque depth and before final post.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Noir fog color. Pure black is forbidden; the abyss must still dither.")]
            public Color fogColor = new Color(0.015f, 0.045f, 0.065f, 1f);

            [Tooltip("Base participating-media density before silt injection.")]
            [Range(0f, 0.3f)] public float baseDensity = 0.045f;

            [Tooltip("Scattering coefficient for the particulate solve.")]
            [Range(0f, 4f)] public float scatteringCoefficient = 0.85f;

            [Tooltip("Extinction coefficient for the water volume.")]
            [Range(0.001f, 2f)] public float extinctionCoefficient = 0.12f;

            [Tooltip("Henyey-Greenstein anisotropy. Positive values bias toward forward flashlight shafts.")]
            [Range(-0.95f, 0.95f)] public float anisotropy = 0.42f;

            [Tooltip("Raymarch early break target opacity.")]
            [Range(0.25f, 0.995f)] public float opacityEarlyBreak = 0.97f;

            [Tooltip("Maximum ray distance in meters.")]
            [Range(4f, 140f)] public float maxRayDistanceMeters = 70f;

            [Tooltip("Quarter-res survival path.")]
            [Range(0.2f, 0.5f)] public float minimumInternalScale = 0.25f;

            [Tooltip("Upper internal resolution for expensive hardware.")]
            [Range(0.5f, 0.85f)] public float maximumInternalScale = 0.67f;

            [Tooltip("Dither strength for low-step proxy and ray start jitter.")]
            [Range(0f, 1f)] public float ditherStrength = 0.82f;

            [Tooltip("Abyssal flow vector influence on wrapped fog noise.")]
            [Range(0f, 8f)] public float flowAdvectionStrength = 2.25f;

            [Tooltip("Screen-space MarineSnow fog density gain.")]
            [Range(0f, 4f)] public float siltDensityStrength = 1.2f;

            [Tooltip("Depth rejection scale for bilateral full-res composite.")]
            [Range(0.1f, 96f)] public float bilateralDepthScale = 24f;

            [Tooltip("Raymarch heatmap blend. 0 means off, 1 means full debug overlay.")]
            [Range(0f, 1f)] public float debugHeatmapWeight = 0f;

            internal int ResolveRaySteps(float quality)
            {
                return VolumetricFogParamsAccess.ResolveRayStepsForQuality(quality);
            }

            internal float ResolveInternalScale(float quality)
            {
                float minScale = ResolveFiniteClamped(minimumInternalScale, 0.2f, 0.5f, 0.25f);
                float maxScale = ResolveFiniteClamped(maximumInternalScale, 0.5f, 0.85f, 0.67f);
                float scale = math.lerp(
                    minScale,
                    maxScale,
                    ResolveFiniteSaturated(quality));
                return math.clamp(scale, 0.2f, 0.85f);
            }

            internal float ResolveProxyBlend(float quality)
            {
                return VolumetricFogParamsAccess.ResolveProxyBlendForQuality(quality);
            }

            internal int ResolvePointLightCount(float quality)
            {
                return math.clamp(
                    1 + (int)math.floor(ResolveFiniteSaturated(quality) * (VolumetricFogConstants.MaxPointLights - 1) + 0.0001f),
                    1,
                    VolumetricFogConstants.MaxPointLights);
            }
        }

        private sealed class VolumetricFogPass : ScriptableRenderPass, IDisposable
        {
            private const int RenderTextureBucketSize = 64;
            private const int VolumeTextureBucketSize = 32;
            private const int MinVolumeGridWidth = 64;
            private const int MinVolumeGridHeight = 32;
            private const int MaxVolumeGridWidth = 384;
            private const int MaxVolumeGridHeight = 224;
            private const int ConstantBufferCount = 1;
            private const int FrameParamsStrideBytes = 224;
            private const int DumpThresholdMicroseconds = 2000;
            private const float DearLieProxyBypassThreshold = 0.999f;
            private const string DumpRelativePath = "Docs/AgentLogs/Dump_1335_VolumetricParticulateFog.bin";
            private const SystemID OwnerSystemId = SystemID.Vfx;
            private const string GridBuildKernelName = "BuildVolumetricFogGrid";
            private const string RaymarchKernelName = "RaymarchVolumetricFog";
            private const string RaymarchXrKernelName = "RaymarchVolumetricFogXR";
            private const int DearLieProxyMaterialPass = 0;
            private const int BilateralCompositeMaterialPass = 1;

            [StructLayout(LayoutKind.Explicit, Size = FrameParamsStrideBytes)]
            private struct FogFrameConstantsDTO
            {
                [FieldOffset(0)] public Vector4 FullSize;
                [FieldOffset(16)] public Vector4 HalfSize;
                [FieldOffset(32)] public Vector4 CompositeParams;
                [FieldOffset(48)] public Vector4 DebugParams;
                [FieldOffset(64)] public Vector4 MarineFogTexelSize;
                [FieldOffset(80)] public Vector4 MarineFogParams;
                [FieldOffset(96)] public Vector4 AbyssalFlowCenter;
                [FieldOffset(112)] public Vector4 AbyssalFlowSpacing;
                [FieldOffset(128)] public Vector4 AbyssalFlowTextureParams;
                [FieldOffset(144)] public Vector4 AbyssalFlowActiveAndPad;
                [FieldOffset(160)] public Vector4 InverseViewProjectionC0;
                [FieldOffset(176)] public Vector4 InverseViewProjectionC1;
                [FieldOffset(192)] public Vector4 InverseViewProjectionC2;
                [FieldOffset(208)] public Vector4 InverseViewProjectionC3;
            }

            private sealed class RaymarchPassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal int dispatchX;
                internal int dispatchY;
                internal int dispatchZ;
                internal TextureHandle depth;
                internal TextureHandle volume;
                internal TextureHandle result;
                internal GraphicsBuffer paramsBuffer;
                internal GraphicsBuffer frameParamsBuffer;
                internal BufferHandle pointLightBuffer;
                internal TextureHandle marineFogDensityTexture;
                internal TextureHandle abyssalFlowTexture;
                internal Vector4 halfSize;
            }

            private sealed class GridBuildPassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal int dispatchX;
                internal int dispatchY;
                internal int dispatchZ;
                internal TextureHandle volume;
                internal GraphicsBuffer paramsBuffer;
                internal GraphicsBuffer frameParamsBuffer;
                internal BufferHandle pointLightBuffer;
                internal TextureHandle marineFogDensityTexture;
                internal TextureHandle abyssalFlowTexture;
                internal Vector4 volumeSize;
            }

            private sealed class RasterCompositePassData
            {
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle halfInput;
                internal BufferHandle paramsBuffer;
                internal BufferHandle frameParamsBuffer;
                internal Material material;
                internal bool hasHalfInput;
                internal int passIndex;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Volumetric Particulate Fog");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private RTHandle _marineFogDensityTextureHandle;
            private RTHandle _abyssalFlowTextureHandle;
            private RTHandle _externalMarineFogDensityTextureHandle;
            private RTHandle _externalAbyssalFlowTextureHandle;
            private RTHandle _externalMarineFogDensityTextureHandleB;
            private RTHandle _externalAbyssalFlowTextureHandleB;
            private RTHandle _emptyVolumeTextureHandle;
            private Material _dearLieProxyMaterial;
            private GraphicsBuffer _paramsBufferA;
            private GraphicsBuffer _paramsBufferB;
            private GraphicsBuffer _frameParamsBufferA;
            private GraphicsBuffer _frameParamsBufferB;
            private GraphicsBuffer _pointLightBufferA;
            private GraphicsBuffer _pointLightBufferB;
            private readonly PointLightDTO[] _pointLightUploadScratch = new PointLightDTO[VolumetricFogConstants.MaxPointLights]; // COLD ALLOC: PointLightDTO[8] - GPU upload scratch after DataVault point-light lock release - owner: HectonVolumetricParticulateFogFeature
            private FogConstantsDTO _lastUploadedParams;
            private FogConstantsDTO _lastAuthoredParams;
            private FogConstantsDTO _externalOverrideParams;
            private uint _lastUploadedParamsHash;
            private uint _lastUploadedPointLightsHash;
            private Texture _marineFogDensityTextureHandleSource;
            private Texture _abyssalFlowTextureHandleSource;
            private Texture _externalMarineFogDensityTextureHandleSource;
            private Texture _externalAbyssalFlowTextureHandleSource;
            private Texture _externalMarineFogDensityTextureHandleSourceB;
            private Texture _externalAbyssalFlowTextureHandleSourceB;
            private Texture _emptyVolumeTextureHandleSource;
            private Texture _bridgeMarineFogTexture;
            private Texture _bridgeAbyssalFlowTexture;
            private Shader _dearLieProxyShader;
            private Vector4 _bridgeMarineFogTexelSize;
            private Vector4 _bridgeMarineFogParams;
            private Vector4 _bridgeAbyssalFlowCenter;
            private Vector4 _bridgeAbyssalFlowSpacing;
            private Vector4 _bridgeAbyssalFlowTextureParams;
            private Vector4 _bridgeBiomeTransitionFogColor;
            private Vector4 _bridgeBiomeTransitionAbsorption;
            private Vector4 _bridgeBiomeTransitionWeights;
            private float _bridgeAbyssalFlowTextureActive;
            private IDataVault _vault;
            private VaultGenerationHandle<FogConstantsDTO> _paramsHandle;
            private VaultGenerationHandle<PointLightDTO> _pointLightsHandle;
            private VaultGenerationHandle<VolumetricFogTelemetryEntry> _telemetryHandle;
            private VaultGenerationHandle<WaterExtinctionProfileDTO> _extinctionProfilesHandle;
            private RenderTexture _emptyFogDensityTexture;
            private Texture3D _emptyAbyssalFlowTexture;
            private int _raymarchKernel = -1;
            private int _raymarchXrKernel = -1;
            private int _gridBuildKernel = -1;
            private uint _gridBuildThreadGroupSizeX;
            private uint _gridBuildThreadGroupSizeY;
            private uint _gridBuildThreadGroupSizeZ;
            private uint _raymarchThreadGroupSizeX;
            private uint _raymarchThreadGroupSizeY;
            private uint _raymarchXrThreadGroupSizeX;
            private uint _raymarchXrThreadGroupSizeY;
            private const uint MaxKernelThreadProduct = 256u;
            private const int MaxDispatchGroupsPerDimension = 65535;
            private float _qualityWeight;
            private double3 _runtimeOriginAup;
            private int _telemetryWriteIndex;
            private int _activePointLightBufferIndex;
            private int _activeParamsBufferIndex;
            private int _activeFrameParamsBufferIndex;
            private int _lastUploadedPointLightCount;
            private int _pendingPointLightCount;
            private int _lastScheduledPointLightCount;
            private int _frameIndex;
            private uint _lastScheduledPointLightHash;
            private JobHandle _mockLightsJobHandle;
            private bool _mockLightsJobPending;
            private bool _dumpedThisSession;
            private bool _extinctionProfilesSeeded;
            private bool _hasUploadedParams;
            private bool _hasUploadedPointLights;
            private bool _hasScheduledPointLightJob;
            private bool _hasAuthoredParams;
            private bool _hasExternalOverrideParams;
            private bool _deferredDumpRequested;
            private bool _forceProxyOnly;

            public VolumetricFogPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public bool Setup(
                FeatureSettings settings,
                ComputeShader computeShader,
                float qualityWeight,
                double3 runtimeOriginAup,
                int frameIndex,
                bool forceProxyOnly)
            {
                _settings = settings;
                _qualityWeight = ResolveFiniteSaturated(qualityWeight);
                _runtimeOriginAup = math.all(math.isfinite(runtimeOriginAup)) ? runtimeOriginAup : double3.zero;
                _frameIndex = math.max(0, frameIndex);
                _forceProxyOnly = forceProxyOnly;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
                return forceProxyOnly || TryBindComputeShader(computeShader);
            }

            public bool PrepareComputeKernels(FeatureSettings settings, ComputeShader computeShader)
            {
                _settings = settings;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                return TryBindComputeShader(computeShader);
            }

            private bool TryBindComputeShader(ComputeShader computeShader)
            {
                if (!ReferenceEquals(_computeShader, computeShader))
                {
                    _computeShader = computeShader;
                    ResetComputeKernelState();
                }

                return TryInitializeComputeKernels();
            }

            private bool TryInitializeComputeKernels()
            {
                if (_computeShader == null)
                    return false;

                if (_gridBuildKernel >= 0 &&
                    _raymarchKernel >= 0 &&
                    _raymarchXrKernel >= 0)
                {
                    return true;
                }

                if (!_computeShader.HasKernel(GridBuildKernelName) ||
                    !_computeShader.HasKernel(RaymarchKernelName) ||
                    !_computeShader.HasKernel(RaymarchXrKernelName))
                {
                    ResetComputeKernelState();
                    return false;
                }

                _gridBuildKernel = _computeShader.FindKernel(GridBuildKernelName);
                _raymarchKernel = _computeShader.FindKernel(RaymarchKernelName);
                _raymarchXrKernel = _computeShader.FindKernel(RaymarchXrKernelName);
                if (!TryResolveKernelThreadGroups(_computeShader, _gridBuildKernel, false, out _gridBuildThreadGroupSizeX, out _gridBuildThreadGroupSizeY, out _gridBuildThreadGroupSizeZ) ||
                    !TryResolveKernelThreadGroups(_computeShader, _raymarchKernel, true, out _raymarchThreadGroupSizeX, out _raymarchThreadGroupSizeY, out _) ||
                    !TryResolveKernelThreadGroups(_computeShader, _raymarchXrKernel, true, out _raymarchXrThreadGroupSizeX, out _raymarchXrThreadGroupSizeY, out _))
                {
                    ResetComputeKernelState();
                    return false;
                }

                return true;
            }

            private void ResetComputeKernelState()
            {
                _gridBuildKernel = -1;
                _raymarchKernel = -1;
                _raymarchXrKernel = -1;
                _gridBuildThreadGroupSizeX = 0u;
                _gridBuildThreadGroupSizeY = 0u;
                _gridBuildThreadGroupSizeZ = 0u;
                _raymarchThreadGroupSizeX = 0u;
                _raymarchThreadGroupSizeY = 0u;
                _raymarchXrThreadGroupSizeX = 0u;
                _raymarchXrThreadGroupSizeY = 0u;
            }

            private static bool TryResolveKernelThreadGroups(
                ComputeShader computeShader,
                int kernelIndex,
                bool requireSingleZ,
                out uint groupSizeX,
                out uint groupSizeY,
                out uint groupSizeZ)
            {
                groupSizeX = 0u;
                groupSizeY = 0u;
                groupSizeZ = 0u;
                if (computeShader == null || kernelIndex < 0 || !computeShader.IsSupported(kernelIndex))
                    return false;

                computeShader.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
                ulong threadProduct = (ulong)x * y * z;
                if (x == 0u || y == 0u || z == 0u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return false;

                if (requireSingleZ && z != 1u)
                    return false;

                groupSizeX = x;
                groupSizeY = y;
                groupSizeZ = z;
                return true;
            }

            private static int ResolveDispatchGroups(int value, uint groupSize)
            {
                if (value <= 0 || groupSize == 0u)
                    return 0;

                long groups = ((long)value + groupSize - 1L) / groupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
            }

            public void Dispose()
            {
                ReleaseExternalTextureHandle(ref _marineFogDensityTextureHandle, ref _marineFogDensityTextureHandleSource);
                ReleaseExternalTextureHandle(ref _abyssalFlowTextureHandle, ref _abyssalFlowTextureHandleSource);
                ReleaseExternalTextureHandle(ref _externalMarineFogDensityTextureHandle, ref _externalMarineFogDensityTextureHandleSource);
                ReleaseExternalTextureHandle(ref _externalAbyssalFlowTextureHandle, ref _externalAbyssalFlowTextureHandleSource);
                ReleaseExternalTextureHandle(ref _externalMarineFogDensityTextureHandleB, ref _externalMarineFogDensityTextureHandleSourceB);
                ReleaseExternalTextureHandle(ref _externalAbyssalFlowTextureHandleB, ref _externalAbyssalFlowTextureHandleSourceB);
                ReleaseExternalTextureHandle(ref _emptyVolumeTextureHandle, ref _emptyVolumeTextureHandleSource);
                _paramsBufferA?.Release();
                _paramsBufferB?.Release();
                _frameParamsBufferA?.Release();
                _frameParamsBufferB?.Release();
                if (_mockLightsJobPending)
                {
                    DispatcherJobFence.TryComplete(ref _mockLightsJobHandle, forceComplete: true); // COLD SYNC JOB: render-feature teardown cannot leave a vault writer running.
                    _mockLightsJobPending = false;
                }

                _pointLightBufferA?.Release();
                _pointLightBufferB?.Release();
                _paramsBufferA = null;
                _paramsBufferB = null;
                _frameParamsBufferA = null;
                _frameParamsBufferB = null;
                _pointLightBufferA = null;
                _pointLightBufferB = null;
                ReleaseVaultHandles();
                _paramsHandle = default;
                _pointLightsHandle = default;
                _telemetryHandle = default;
                _extinctionProfilesHandle = default;
                _vault = null;
                _activePointLightBufferIndex = 0;
                _activeParamsBufferIndex = 0;
                _activeFrameParamsBufferIndex = 0;
                _lastUploadedPointLightCount = 0;
                _pendingPointLightCount = 0;
                _lastScheduledPointLightCount = 0;
                _lastScheduledPointLightHash = 0u;
                _extinctionProfilesSeeded = false;
                _hasUploadedParams = false;
                _lastUploadedParams = default;
                _lastUploadedParamsHash = 0u;
                ResetAuthoredOverrideState();
                _hasUploadedPointLights = false;
                _lastUploadedPointLightsHash = 0u;
                _hasScheduledPointLightJob = false;
                _deferredDumpRequested = false;
                ReleaseFallbackTextures();
                DestroyUnityObject(_dearLieProxyMaterial);
                _dearLieProxyMaterial = null;
                _dearLieProxyShader = null;
                _computeShader = null;
                ResetComputeKernelState();
            }

            public bool HasNativeState => _vault != null &&
                                          !_vault.IsCompactionFenceActive &&
                                          IsFogHandle(in _paramsHandle, BufferID.ShinobuVolumetricFogParams) &&
                                          IsFogHandle(in _pointLightsHandle, BufferID.ShinobuVolumetricFogPointLights) &&
                                          IsFogHandle(in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing) &&
                                          IsFogHandle(in _extinctionProfilesHandle, BufferID.ShinobuVolumetricFogExtinctionProfiles);

            public bool HasGpuState => HasGpuBuffers() &&
                                       _emptyFogDensityTexture != null &&
                                       _emptyAbyssalFlowTexture != null &&
                                       _dearLieProxyMaterial != null &&
                                       _marineFogDensityTextureHandle != null &&
                                       _abyssalFlowTextureHandle != null &&
                                       _emptyVolumeTextureHandle != null;

            public bool TryPrepareNativeState(IDataVault vault, bool allowAllocation)
            {
                if (vault == null || vault.IsCompactionFenceActive)
                    return false;

                if (!ReferenceEquals(vault, _vault))
                {
                    if (!allowAllocation)
                        return false;

                    if (_mockLightsJobPending)
                    {
                        if (!_mockLightsJobHandle.IsCompleted)
                            return false;

                        DispatcherJobFence.TryFinalizeCompleted(ref _mockLightsJobHandle);
                        _mockLightsJobPending = false;
                    }

                    ReleaseVaultHandles();
                    _vault = vault;
                    _paramsHandle = default;
                    _pointLightsHandle = default;
                    _telemetryHandle = default;
                    _extinctionProfilesHandle = default;
                    _extinctionProfilesSeeded = false;
                    ResetAuthoredOverrideState();
                    ResetPointLightScheduleState();
                    ClearPointLightBuffer(_pointLightBufferA);
                    ClearPointLightBuffer(_pointLightBufferB);
                }

                if (!allowAllocation)
                    return EnsureVaultState();

                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                    return false;

                if (!IsFogHandle(in _paramsHandle, BufferID.ShinobuVolumetricFogParams))
                    _paramsHandle = vault.EnsureGenerationHandle<FogConstantsDTO>(BufferID.ShinobuVolumetricFogParams, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
                if (!IsFogHandle(in _pointLightsHandle, BufferID.ShinobuVolumetricFogPointLights))
                    _pointLightsHandle = vault.EnsureGenerationHandle<PointLightDTO>(BufferID.ShinobuVolumetricFogPointLights, VolumetricFogConstants.MaxPointLights, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
                if (!IsFogHandle(in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing))
                    _telemetryHandle = vault.EnsureGenerationHandle<VolumetricFogTelemetryEntry>(BufferID.ShinobuVolumetricFogTelemetryRing, VolumetricFogConstants.TelemetryCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
                if (!IsFogHandle(in _extinctionProfilesHandle, BufferID.ShinobuVolumetricFogExtinctionProfiles))
                    _extinctionProfilesHandle = vault.EnsureGenerationHandle<WaterExtinctionProfileDTO>(BufferID.ShinobuVolumetricFogExtinctionProfiles, VolumetricFogConstants.ExtinctionProfileCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);

                if (!HasNativeState)
                    return false;

                SeedDefaultExtinctionProfiles();
                return true;
            }

            public bool TryPrepareGpuState(bool allowAllocation)
            {
                if (!allowAllocation && !HasGpuState)
                    return false;

                if (allowAllocation)
                    EnsureFallbackTextures();
                else if (_emptyFogDensityTexture == null || _emptyAbyssalFlowTexture == null)
                    return false;

                if (!EnsureDearLieProxyMaterial(allowAllocation))
                    return false;

                if (!EnsureGpuBuffers(allowAllocation))
                    return false;

                bool fallbackHandlesReady = ResolveExternalTextureHandle(_emptyFogDensityTexture, ref _marineFogDensityTextureHandle, ref _marineFogDensityTextureHandleSource, allowAllocation) != null &&
                                            ResolveExternalTextureHandle(_emptyAbyssalFlowTexture, ref _abyssalFlowTextureHandle, ref _abyssalFlowTextureHandleSource, allowAllocation) != null &&
                                            ResolveExternalTextureHandle(_emptyAbyssalFlowTexture, ref _emptyVolumeTextureHandle, ref _emptyVolumeTextureHandleSource, allowAllocation) != null;
                if (!fallbackHandlesReady)
                    return false;

                if (allowAllocation)
                {
                    PrepareExternalBridgeHandlesCold();
                }

                return true;
            }

            public void CachePresentationGlobalsLate()
            {
                _bridgeMarineFogTexelSize = Shader.GetGlobalVector(ShaderConstants.MarineSnowDensityTexelSizeId);
                _bridgeMarineFogParams = Shader.GetGlobalVector(ShaderConstants.MarineSnowDensityParamsId);
                _bridgeMarineFogTexture = Shader.GetGlobalTexture(ShaderConstants.MarineSnowDensityTextureId);
                _bridgeAbyssalFlowTexture = Shader.GetGlobalTexture(ShaderConstants.AbyssalFlowTextureId);
                _bridgeAbyssalFlowCenter = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowCenterId);
                _bridgeAbyssalFlowSpacing = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowSpacingId);
                _bridgeAbyssalFlowTextureParams = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowTextureParamsId);
                _bridgeAbyssalFlowTextureActive = Shader.GetGlobalFloat(ShaderConstants.AbyssalFlowTextureActiveId);
                _bridgeBiomeTransitionFogColor = Shader.GetGlobalVector(ShaderConstants.BiomeTransitionFogColorId);
                _bridgeBiomeTransitionAbsorption = Shader.GetGlobalVector(ShaderConstants.BiomeTransitionAbsorptionId);
                _bridgeBiomeTransitionWeights = Shader.GetGlobalVector(ShaderConstants.BiomeTransitionWeightsId);
            }

            public void PrepareExternalBridgeHandlesCold()
            {
                if (IsUsableMarineFogTexture(_bridgeMarineFogTexture, in _bridgeMarineFogParams) &&
                    !ReferenceEquals(_bridgeMarineFogTexture, _emptyFogDensityTexture))
                {
                    ResolveCachedExternalTextureHandle(
                        _bridgeMarineFogTexture,
                        ref _externalMarineFogDensityTextureHandle,
                        ref _externalMarineFogDensityTextureHandleSource,
                        ref _externalMarineFogDensityTextureHandleB,
                        ref _externalMarineFogDensityTextureHandleSourceB,
                        allowAllocation: true);
                }

                if (IsUsableAbyssalFlowTexture(_bridgeAbyssalFlowTexture) &&
                    !ReferenceEquals(_bridgeAbyssalFlowTexture, _emptyAbyssalFlowTexture))
                {
                    ResolveCachedExternalTextureHandle(
                        _bridgeAbyssalFlowTexture,
                        ref _externalAbyssalFlowTextureHandle,
                        ref _externalAbyssalFlowTextureHandleSource,
                        ref _externalAbyssalFlowTextureHandleB,
                        ref _externalAbyssalFlowTextureHandleSourceB,
                        allowAllocation: true);
                }
            }

            public void FlushDeferredDiagnosticDump()
            {
                if (!_deferredDumpRequested ||
                    _vault == null ||
                    !IsFogHandle(in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing))
                {
                    return;
                }

                if (!TryReadTelemetryDumpLength(out int telemetryLength) ||
                    telemetryLength <= 0)
                {
                    return;
                }

                DumpTelemetryRing(telemetryLength);
                _deferredDumpRequested = false;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || !VolumetricFogNativeLayout.Validate())
                {
                    return;
                }

                bool hasComputeKernels = _computeShader != null &&
                                         _gridBuildKernel >= 0 &&
                                         _raymarchKernel >= 0 &&
                                         _raymarchXrKernel >= 0;

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
                int fullWidth = Mathf.Max(1, sourceDesc.width);
                int fullHeight = Mathf.Max(1, sourceDesc.height);
                XRPass xrPass = cameraData.xr;
                bool xrActive = IsXrActive(xrPass);
                bool requestedTextureArray = UsesSinglePassTextureArray(xrPass);
                int requestedViewCount = ResolveActiveViewCount(xrPass);
                bool useTextureArray = requestedTextureArray &&
                    sourceDesc.dimension == TextureDimension.Tex2DArray &&
                    sourceDesc.slices >= requestedViewCount;
                if (requestedTextureArray && !useTextureArray)
                    return;

                int activeViewCount = useTextureArray ? requestedViewCount : 1;
                Camera camera = cameraData.camera;
                float proxyBlend = _settings.ResolveProxyBlend(_qualityWeight);
                float volumetricContribution = math.saturate(1f - proxyBlend);
                float volumetricCurve = ResolveQualityCurve(volumetricContribution);
                bool proxyOnly = _forceProxyOnly ||
                                 proxyBlend >= DearLieProxyBypassThreshold ||
                                 xrActive ||
                                 !hasComputeKernels;
                float effectiveProxyBlend = proxyOnly ? 1f : proxyBlend;
                float effectiveVolumetricQuality = proxyOnly
                    ? 0f
                    : math.saturate(_qualityWeight * math.lerp(0.25f, 1f, volumetricCurve));
                int raySteps = proxyOnly
                    ? VolumetricFogConstants.MinRaySteps
                    : _settings.ResolveRaySteps(effectiveVolumetricQuality);
                float visualPhaseSeconds = ResolveVisualPhaseSeconds(_qualityWeight, _frameIndex);

                float renderScale = proxyOnly
                    ? ResolveFiniteClamped(_settings.minimumInternalScale, 0.2f, 0.5f, 0.25f)
                    : math.lerp(
                        ResolveFiniteClamped(_settings.minimumInternalScale, 0.2f, 0.5f, 0.25f),
                        _settings.ResolveInternalScale(_qualityWeight),
                        volumetricCurve);
                int halfWidth = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(fullWidth * renderScale)));
                int halfHeight = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(fullHeight * renderScale)));
                int volumeWidth = ResolveVolumeGridDimension(halfWidth, _qualityWeight, MinVolumeGridWidth, MaxVolumeGridWidth);
                int volumeHeight = ResolveVolumeGridDimension(halfHeight, _qualityWeight, MinVolumeGridHeight, MaxVolumeGridHeight);
                if (!HasGpuState || !EnsureVaultState())
                    return;

                Matrix4x4 inverseViewProjection = ResolveInverseViewProjection(camera, proxyOnly);
                int requestedPointLightCount = proxyOnly ? 0 : _settings.ResolvePointLightCount(effectiveVolumetricQuality);
                float estimatedGpuMicroseconds = proxyOnly
                    ? EstimateProxyMicroseconds(halfWidth, halfHeight, fullWidth, fullHeight, renderScale)
                    : EstimateGpuMicroseconds(halfWidth, halfHeight, volumeWidth, volumeHeight, raySteps, requestedPointLightCount, renderScale);
                Vector4 marineFogTexelSize = _bridgeMarineFogTexelSize;
                Vector4 marineFogParams = _bridgeMarineFogParams;
                Texture marineFogTexture = _bridgeMarineFogTexture;
                Texture abyssalFlowTexture = _bridgeAbyssalFlowTexture;
                Vector4 abyssalFlowCenter = _bridgeAbyssalFlowCenter;
                Vector4 abyssalFlowSpacing = _bridgeAbyssalFlowSpacing;
                Vector4 abyssalFlowTextureParams = _bridgeAbyssalFlowTextureParams;
                float abyssalFlowTextureActive = _bridgeAbyssalFlowTextureActive;
                Vector4 biomeTransitionFogColor = _bridgeBiomeTransitionFogColor;
                Vector4 biomeTransitionAbsorption = _bridgeBiomeTransitionAbsorption;
                Vector4 biomeTransitionWeights = _bridgeBiomeTransitionWeights;

                if (!UpdateVaultAndGpuState(
                        camera,
                        raySteps,
                        requestedPointLightCount,
                        renderScale,
                        visualPhaseSeconds,
                        estimatedGpuMicroseconds,
                        effectiveProxyBlend,
                        in biomeTransitionFogColor,
                        in biomeTransitionAbsorption,
                        in biomeTransitionWeights,
                        IsUsableMarineFogTexture(marineFogTexture, in marineFogParams),
                        IsUsableAbyssalFlowTexture(abyssalFlowTexture) && abyssalFlowTextureActive > 0.5f,
                        !proxyOnly,
                        out int activePointLightCount,
                        out GraphicsBuffer activePointLightBuffer))
                {
                    return;
                }

                TextureDimension outputDimension = useTextureArray ? TextureDimension.Tex2DArray : TextureDimension.Tex2D;
                int outputSlices = useTextureArray ? activeViewCount : 1;
                VRTextureUsage outputVrUsage = useTextureArray ? sourceDesc.vrUsage : VRTextureUsage.None;
                TextureHandle halfTexture = default;
                TextureHandle volumeTexture = default;
                if (!proxyOnly)
                {
                    TextureDesc halfDesc = CreateGraphTextureDesc(sourceDesc, halfWidth, halfHeight, outputSlices, outputDimension, FilterMode.Bilinear, "_HectonVolumetricFogHalf", GraphicsFormat.R16G16B16A16_SFloat, true, useTextureArray, outputVrUsage);
                    halfTexture = renderGraph.CreateTexture(halfDesc);
                    TextureDesc volumeDesc = CreateGraphTextureDesc(sourceDesc, volumeWidth, volumeHeight, raySteps, TextureDimension.Tex3D, FilterMode.Point, "_HectonVolumetricFogFrustumGrid", GraphicsFormat.R16G16B16A16_SFloat, true);
                    volumeTexture = renderGraph.CreateTexture(volumeDesc);
                }

                TextureDesc compositeDesc = CreateGraphTextureDesc(sourceDesc, fullWidth, fullHeight, outputSlices, outputDimension, FilterMode.Bilinear, "_HectonVolumetricFogComposite", ResolveCompositeColorFormat(sourceDesc), false, useTextureArray, outputVrUsage);
                TextureHandle compositeTexture = renderGraph.CreateTexture(compositeDesc);
                GraphicsBuffer activeParamsBuffer = GetActiveParamsBuffer();
                if (activeParamsBuffer == null || !activeParamsBuffer.IsValid())
                    return;

                Vector4 fullSize = new Vector4(fullWidth, fullHeight, 1f / fullWidth, 1f / fullHeight);
                Vector4 halfSize = new Vector4(halfWidth, halfHeight, 1f / Mathf.Max(1, halfWidth), 1f / Mathf.Max(1, halfHeight));
                Vector4 compositeParams = new Vector4(
                    ResolveFiniteClamped(_settings.bilateralDepthScale, 0.01f, 96f, 24f),
                    ResolveFiniteClamped(_settings.siltDensityStrength, 0f, 4f, 1.2f),
                    activePointLightCount,
                    visualPhaseSeconds);
                Vector4 debugParams = new Vector4(
                    ResolveFiniteSaturated(_settings.debugHeatmapWeight),
                    ResolveFiniteSaturated(_settings.ditherStrength),
                    renderScale,
                    estimatedGpuMicroseconds);
                if (!IsUsableMarineFogTexture(marineFogTexture, in marineFogParams))
                {
                    marineFogTexture = _emptyFogDensityTexture;
                    marineFogParams = Vector4.zero;
                    marineFogTexelSize = new Vector4(1f, 1f, 1f, 1f);
                }

                if (!IsUsableAbyssalFlowTexture(abyssalFlowTexture))
                {
                    abyssalFlowTexture = _emptyAbyssalFlowTexture;
                    abyssalFlowTextureActive = 0f;
                }

                UploadFrameConstantBuffer(
                    in fullSize,
                    in halfSize,
                    in compositeParams,
                    in debugParams,
                    in marineFogTexelSize,
                    in marineFogParams,
                    in abyssalFlowCenter,
                    in abyssalFlowSpacing,
                    in abyssalFlowTextureParams,
                    abyssalFlowTextureActive,
                    in inverseViewProjection);
                GraphicsBuffer activeFrameParamsBuffer = GetActiveFrameParamsBuffer();
                if (activeFrameParamsBuffer == null || !activeFrameParamsBuffer.IsValid())
                    return;

                if (proxyOnly)
                {
                    BufferHandle proxyParamsBufferHandle = renderGraph.ImportBuffer(activeParamsBuffer);
                    BufferHandle proxyFrameParamsBufferHandle = renderGraph.ImportBuffer(activeFrameParamsBuffer);
                    if (!AddRasterFogCompositePass(
                            renderGraph,
                            "Hecton Dear Lie Fog Proxy",
                            sourceTexture,
                            depthTexture,
                            TextureHandle.nullHandle,
                            compositeTexture,
                            proxyParamsBufferHandle,
                            proxyFrameParamsBufferHandle,
                            false,
                            DearLieProxyMaterialPass))
                    {
                        return;
                    }

                    resourceData.cameraColor = compositeTexture;
                    return;
                }

                if (activePointLightBuffer == null || !activePointLightBuffer.IsValid())
                    return;

                int gridDispatchX = ResolveDispatchGroups(volumeWidth, _gridBuildThreadGroupSizeX);
                int gridDispatchY = ResolveDispatchGroups(volumeHeight, _gridBuildThreadGroupSizeY);
                int gridDispatchZ = ResolveDispatchGroups(raySteps, _gridBuildThreadGroupSizeZ);
                uint raymarchThreadGroupX = useTextureArray ? _raymarchXrThreadGroupSizeX : _raymarchThreadGroupSizeX;
                uint raymarchThreadGroupY = useTextureArray ? _raymarchXrThreadGroupSizeY : _raymarchThreadGroupSizeY;
                int raymarchDispatchX = ResolveDispatchGroups(halfWidth, raymarchThreadGroupX);
                int raymarchDispatchY = ResolveDispatchGroups(halfHeight, raymarchThreadGroupY);
                int raymarchDispatchZ = ResolveDispatchGroups(activeViewCount, 1u);
                if (gridDispatchX <= 0 || gridDispatchY <= 0 || gridDispatchZ <= 0 ||
                    raymarchDispatchX <= 0 || raymarchDispatchY <= 0 || raymarchDispatchZ <= 0)
                {
                    return;
                }

                BufferHandle paramsBufferHandle = renderGraph.ImportBuffer(activeParamsBuffer);
                BufferHandle frameParamsBufferHandle = renderGraph.ImportBuffer(activeFrameParamsBuffer);
                RTHandle marineFogTextureHandle = TryGetCachedExternalTextureHandle(
                    marineFogTexture,
                    _externalMarineFogDensityTextureHandle,
                    _externalMarineFogDensityTextureHandleSource,
                    _externalMarineFogDensityTextureHandleB,
                    _externalMarineFogDensityTextureHandleSourceB);
                if (marineFogTextureHandle == null)
                {
                    marineFogTexture = _emptyFogDensityTexture;
                    marineFogParams = Vector4.zero;
                    marineFogTexelSize = new Vector4(1f, 1f, 1f, 1f);
                    marineFogTextureHandle = _marineFogDensityTextureHandle;
                }

                RTHandle abyssalFlowTextureHandle = TryGetCachedExternalTextureHandle(
                    abyssalFlowTexture,
                    _externalAbyssalFlowTextureHandle,
                    _externalAbyssalFlowTextureHandleSource,
                    _externalAbyssalFlowTextureHandleB,
                    _externalAbyssalFlowTextureHandleSourceB);
                if (abyssalFlowTextureHandle == null)
                {
                    abyssalFlowTexture = _emptyAbyssalFlowTexture;
                    abyssalFlowTextureActive = 0f;
                    abyssalFlowTextureHandle = _abyssalFlowTextureHandle;
                }

                if (marineFogTextureHandle == null || abyssalFlowTextureHandle == null)
                    return;

                BufferHandle pointLightBufferHandle = renderGraph.ImportBuffer(activePointLightBuffer);
                TextureHandle marineFogGraphTexture = renderGraph.ImportTexture(marineFogTextureHandle);
                TextureHandle abyssalFlowGraphTexture = renderGraph.ImportTexture(abyssalFlowTextureHandle);

                using (var builder = renderGraph.AddComputePass("Hecton Particulate Fog Frustum Grid", out GridBuildPassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _gridBuildKernel;
                    passData.dispatchX = gridDispatchX;
                    passData.dispatchY = gridDispatchY;
                    passData.dispatchZ = gridDispatchZ;
                    passData.volume = volumeTexture;
                    passData.paramsBuffer = activeParamsBuffer;
                    passData.frameParamsBuffer = activeFrameParamsBuffer;
                    passData.pointLightBuffer = pointLightBufferHandle;
                    passData.marineFogDensityTexture = marineFogGraphTexture;
                    passData.abyssalFlowTexture = abyssalFlowGraphTexture;
                    passData.volumeSize = new Vector4(volumeWidth, volumeHeight, 1f / Mathf.Max(1, volumeWidth), 1f / Mathf.Max(1, volumeHeight));

                    builder.UseTexture(volumeTexture, AccessFlags.Write);
                    builder.UseTexture(marineFogGraphTexture, AccessFlags.Read);
                    builder.UseTexture(abyssalFlowGraphTexture, AccessFlags.Read);
                    builder.UseBuffer(paramsBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(frameParamsBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(pointLightBufferHandle, AccessFlags.Read);

                    builder.SetRenderFunc(static (GridBuildPassData data, ComputeGraphContext context) =>
                    {
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.VolumeWriteId, data.volume);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.MarineSnowDensityTextureId, data.marineFogDensityTexture);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.AbyssalFlowTextureId, data.abyssalFlowTexture);
                        context.cmd.SetComputeConstantBufferParam(data.computeShader, ShaderConstants.ParamsBufferId, data.paramsBuffer, 0, VolumetricFogConstants.ParamsStrideBytes);
                        context.cmd.SetComputeConstantBufferParam(data.computeShader, ShaderConstants.FrameParamsBufferId, data.frameParamsBuffer, 0, FrameParamsStrideBytes);
                        context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.PointLightsBufferId, data.pointLightBuffer);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, data.dispatchZ);
                    });
                }

                using (var builder = renderGraph.AddComputePass("Hecton Particulate Fog Raymarch", out RaymarchPassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = useTextureArray ? _raymarchXrKernel : _raymarchKernel;
                    passData.dispatchX = raymarchDispatchX;
                    passData.dispatchY = raymarchDispatchY;
                    passData.dispatchZ = raymarchDispatchZ;
                    passData.depth = depthTexture;
                    passData.volume = volumeTexture;
                    passData.result = halfTexture;
                    passData.paramsBuffer = activeParamsBuffer;
                    passData.frameParamsBuffer = activeFrameParamsBuffer;
                    passData.pointLightBuffer = pointLightBufferHandle;
                    passData.marineFogDensityTexture = marineFogGraphTexture;
                    passData.abyssalFlowTexture = abyssalFlowGraphTexture;
                    passData.halfSize = halfSize;

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(volumeTexture, AccessFlags.Read);
                    builder.UseTexture(halfTexture, AccessFlags.Write);
                    builder.UseTexture(marineFogGraphTexture, AccessFlags.Read);
                    builder.UseTexture(abyssalFlowGraphTexture, AccessFlags.Read);
                    builder.UseBuffer(paramsBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(frameParamsBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(pointLightBufferHandle, AccessFlags.Read);

                    builder.SetRenderFunc(static (RaymarchPassData data, ComputeGraphContext context) =>
                    {
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.VolumeTextureId, data.volume);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.HalfResultId, data.result);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.MarineSnowDensityTextureId, data.marineFogDensityTexture);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.AbyssalFlowTextureId, data.abyssalFlowTexture);
                        context.cmd.SetComputeConstantBufferParam(data.computeShader, ShaderConstants.ParamsBufferId, data.paramsBuffer, 0, VolumetricFogConstants.ParamsStrideBytes);
                        context.cmd.SetComputeConstantBufferParam(data.computeShader, ShaderConstants.FrameParamsBufferId, data.frameParamsBuffer, 0, FrameParamsStrideBytes);
                        context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.PointLightsBufferId, data.pointLightBuffer);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.dispatchX, data.dispatchY, data.dispatchZ);
                    });
                }

                if (!AddRasterFogCompositePass(
                        renderGraph,
                        "Hecton Particulate Fog Bilateral Composite",
                        sourceTexture,
                        depthTexture,
                        halfTexture,
                        compositeTexture,
                        paramsBufferHandle,
                        frameParamsBufferHandle,
                        true,
                        BilateralCompositeMaterialPass))
                {
                    return;
                }

                resourceData.cameraColor = compositeTexture;
            }

            private bool AddRasterFogCompositePass(
                RenderGraph renderGraph,
                string passName,
                TextureHandle sourceTexture,
                TextureHandle depthTexture,
                TextureHandle halfInputTexture,
                TextureHandle destinationTexture,
                BufferHandle paramsBufferHandle,
                BufferHandle frameParamsBufferHandle,
                bool hasHalfInput,
                int passIndex)
            {
                if (_dearLieProxyMaterial == null ||
                    !sourceTexture.IsValid() ||
                    !depthTexture.IsValid() ||
                    !destinationTexture.IsValid() ||
                    (hasHalfInput && !halfInputTexture.IsValid()))
                {
                    return false;
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<RasterCompositePassData>(
                           passName,
                           out RasterCompositePassData passData,
                           _profilingSampler))
                {
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.halfInput = halfInputTexture;
                    passData.paramsBuffer = paramsBufferHandle;
                    passData.frameParamsBuffer = frameParamsBufferHandle;
                    passData.material = _dearLieProxyMaterial;
                    passData.hasHalfInput = hasHalfInput;
                    passData.passIndex = passIndex;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    if (hasHalfInput)
                        builder.UseTexture(halfInputTexture, AccessFlags.Read);
                    builder.UseBuffer(paramsBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(frameParamsBufferHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (RasterCompositePassData data, RasterGraphContext context) =>
                    {
                        GraphicsBuffer paramsBuffer = data.paramsBuffer;
                        GraphicsBuffer frameParamsBuffer = data.frameParamsBuffer;
                        if (data.material == null ||
                            paramsBuffer == null ||
                            frameParamsBuffer == null)
                        {
                            return;
                        }

                        context.cmd.SetGlobalTexture(ShaderConstants.SourceColorId, data.source);
                        context.cmd.SetGlobalTexture(ShaderConstants.SourceDepthId, data.depth);
                        if (data.hasHalfInput)
                            context.cmd.SetGlobalTexture(ShaderConstants.HalfInputId, data.halfInput);
                        context.cmd.SetGlobalConstantBuffer(
                            paramsBuffer,
                            ShaderConstants.ParamsBufferId,
                            0,
                            VolumetricFogConstants.ParamsStrideBytes);
                        context.cmd.SetGlobalConstantBuffer(
                            frameParamsBuffer,
                            ShaderConstants.FrameParamsBufferId,
                            0,
                            FrameParamsStrideBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.passIndex);
                    });
                }

                return true;
            }

            private bool EnsureVaultState()
            {
                if (!HasNativeState)
                    return false;

                IDataVault vault = _vault;
                return vault != null &&
                       !vault.IsCompactionFenceActive &&
                       TryReadFogBuffer(vault, in _paramsHandle, BufferID.ShinobuVolumetricFogParams, 1, out NativeArray<FogConstantsDTO>.ReadOnly fogParams) &&
                       fogParams.IsCreated &&
                       fogParams.Length > 0 &&
                       TryReadFogBuffer(vault, in _pointLightsHandle, BufferID.ShinobuVolumetricFogPointLights, VolumetricFogConstants.MaxPointLights, out NativeArray<PointLightDTO>.ReadOnly pointLights) &&
                       pointLights.IsCreated &&
                       pointLights.Length >= VolumetricFogConstants.MaxPointLights &&
                       TryReadFogBuffer(vault, in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing, VolumetricFogConstants.TelemetryCapacity, out NativeArray<VolumetricFogTelemetryEntry>.ReadOnly telemetry) &&
                       telemetry.IsCreated &&
                       telemetry.Length >= VolumetricFogConstants.TelemetryCapacity &&
                       TryReadFogBuffer(vault, in _extinctionProfilesHandle, BufferID.ShinobuVolumetricFogExtinctionProfiles, VolumetricFogConstants.ExtinctionProfileCapacity, out NativeArray<WaterExtinctionProfileDTO>.ReadOnly profiles) &&
                       profiles.IsCreated &&
                       profiles.Length >= VolumetricFogConstants.ExtinctionProfileCapacity;
            }

            private void SeedDefaultExtinctionProfiles()
            {
                IDataVault vault = _vault;
                if (vault == null ||
                    vault.IsCompactionFenceActive ||
                    !TryAcquireFogWriteBuffer(vault, in _extinctionProfilesHandle, BufferID.ShinobuVolumetricFogExtinctionProfiles, VolumetricFogConstants.ExtinctionProfileCapacity, out NativeArray<WaterExtinctionProfileDTO> profiles))
                {
                    return;
                }

                try
                {
                    if (vault.IsCompactionFenceActive ||
                        _extinctionProfilesSeeded ||
                        !profiles.IsCreated ||
                        profiles.Length <= 0)
                    {
                        return;
                    }

                    profiles[0] = VolumetricFogParamsAccess.CreateDefaultExtinctionProfile();
                    for (int i = 1; i < profiles.Length; i++)
                        profiles[i] = default;
                    _extinctionProfilesSeeded = true;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _extinctionProfilesHandle, OwnerSystemId);
                }
            }

            private bool UpdateVaultAndGpuState(
                Camera camera,
                int raySteps,
                int pointLightCount,
                float renderScale,
                float visualPhaseSeconds,
                float estimatedGpuMicroseconds,
                float proxyBlend,
                in Vector4 biomeTransitionFogColor,
                in Vector4 biomeTransitionAbsorption,
                in Vector4 biomeTransitionWeights,
                bool hasMarineFogTexture,
                bool hasAbyssalFlowTexture,
                bool allowMockLightSchedule,
                out int activePointLightCount,
                out GraphicsBuffer activePointLightBuffer)
            {
                activePointLightCount = 0;
                activePointLightBuffer = GetActivePointLightBuffer();
                IDataVault vault = _vault;
                if (vault == null ||
                    vault.IsCompactionFenceActive ||
                    !IsFogHandle(in _paramsHandle, BufferID.ShinobuVolumetricFogParams) ||
                    !IsFogHandle(in _pointLightsHandle, BufferID.ShinobuVolumetricFogPointLights) ||
                    !IsFogHandle(in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing) ||
                    activePointLightBuffer == null ||
                    !activePointLightBuffer.IsValid())
                {
                    return false;
                }

                if (!TryReadFogBuffer(vault, in _paramsHandle, BufferID.ShinobuVolumetricFogParams, 1, out NativeArray<FogConstantsDTO>.ReadOnly fogParams) ||
                    !fogParams.IsCreated ||
                    fogParams.Length <= 0)
                {
                    return false;
                }

                Color linearColor = _settings.fogColor.linear;
                float3 settingsColor = new float3(
                    ResolveFiniteClamped(linearColor.r, 0.0015f, 8f, 0.015f),
                    ResolveFiniteClamped(linearColor.g, 0.0023f, 8f, 0.045f),
                    ResolveFiniteClamped(linearColor.b, 0.0031f, 8f, 0.065f));
                Color color = new Color(settingsColor.x, settingsColor.y, settingsColor.z, 1f);
                float baseDensity = ResolveFiniteClamped(_settings.baseDensity, 0f, 0.3f, 0.045f);
                float extinctionCoefficient = ResolveFiniteClamped(_settings.extinctionCoefficient, 0.0001f, 2f, 0.12f);
                float3 cameraPosition = ResolveCameraAupLocalPosition(camera, _runtimeOriginAup);
                float3 cameraForward = ResolveCameraForward(camera);
                float3 wrappedNoiseOffset = ResolveWrappedNoiseOffset(cameraPosition);
                FogConstantsDTO existing = fogParams[0];
                UpdateExternalOverrideState(in existing);
                bool useVaultOverride = _hasExternalOverrideParams;
                FogConstantsDTO overrideParams = _externalOverrideParams;
                float scatteringCoefficient = ResolveFiniteClamped(_settings.scatteringCoefficient, 0f, 4f, 0.85f);
                float anisotropy = ResolveFiniteClamped(_settings.anisotropy, -0.95f, 0.95f, 0.42f);
                float opacityEarlyBreak = ResolveFiniteClamped(_settings.opacityEarlyBreak, 0.25f, 0.995f, 0.97f);
                if (useVaultOverride)
                {
                    color = new Color(
                        ResolveFiniteClamped(overrideParams.FogColorAndDensity.x, 0.0015f, 8f, color.r),
                        ResolveFiniteClamped(overrideParams.FogColorAndDensity.y, 0.0023f, 8f, color.g),
                        ResolveFiniteClamped(overrideParams.FogColorAndDensity.z, 0.0031f, 8f, color.b),
                        color.a);
                    baseDensity = ResolveFiniteClamped(overrideParams.FogColorAndDensity.w, 0f, 0.3f, baseDensity);
                    scatteringCoefficient = ResolveFiniteClamped(overrideParams.ScatteringParams.x, 0f, 4f, scatteringCoefficient);
                    extinctionCoefficient = ResolveFiniteClamped(overrideParams.ScatteringParams.y, 0.0001f, 2f, extinctionCoefficient);
                    anisotropy = ResolveFiniteClamped(overrideParams.ScatteringParams.z, -0.95f, 0.95f, anisotropy);
                    opacityEarlyBreak = ResolveFiniteClamped(overrideParams.ScatteringParams.w, 0.25f, 0.995f, opacityEarlyBreak);
                }

                ApplyExtinctionProfileFromVault(ref color, ref baseDensity, ref extinctionCoefficient, cameraPosition);
                ApplyBiomeTransitionGlobals(ref color, ref baseDensity, ref extinctionCoefficient, in biomeTransitionFogColor, in biomeTransitionAbsorption, in biomeTransitionWeights);
                float4 fogColorAndDensity = new float4(color.r, color.g, color.b, baseDensity);
                float4 scatteringParams = new float4(scatteringCoefficient, extinctionCoefficient, anisotropy, opacityEarlyBreak);
                float flowStrength = useVaultOverride
                    ? ResolveFiniteClamped(overrideParams.FlowAdvection.w, 0f, 8f, 2.25f)
                    : ResolveFiniteClamped(_settings.flowAdvectionStrength, 0f, 8f, 2.25f);
                FogConstantsDTO dto = new FogConstantsDTO
                {
                    FogColorAndDensity = fogColorAndDensity,
                    ScatteringParams = scatteringParams,
                    FlowAdvection = new float4(wrappedNoiseOffset, flowStrength),
                    QualityAndLimits = new float4(
                        _qualityWeight,
                        raySteps,
                        ResolveFiniteClamped(_settings.maxRayDistanceMeters, 0.25f, 140f, 70f),
                        ResolveFiniteSaturated(proxyBlend))
                };

                if (!TryWriteFogParams(vault, in dto))
                    return false;

                UploadConstantBufferIfDirty(in dto);
                if (allowMockLightSchedule)
                {
                    if (!TryWriteAndUploadMockLights(
                            vault,
                            cameraPosition,
                            cameraForward,
                            pointLightCount,
                            visualPhaseSeconds,
                            out activePointLightCount))
                    {
                        return false;
                    }
                }
                else
                {
                    activePointLightCount = 0;
                }

                activePointLightBuffer = GetActivePointLightBuffer();
                return TryRecordTelemetry(
                    vault,
                    in dto,
                    cameraPosition,
                    raySteps,
                    renderScale,
                    estimatedGpuMicroseconds,
                    activePointLightCount,
                    hasMarineFogTexture,
                    hasAbyssalFlowTexture);
            }

            private bool TryWriteFogParams(IDataVault vault, in FogConstantsDTO dto)
            {
                if (!TryAcquireFogWriteBuffer(vault, in _paramsHandle, BufferID.ShinobuVolumetricFogParams, 1, out NativeArray<FogConstantsDTO> fogParams))
                    return false;

                try
                {
                    if (vault.IsCompactionFenceActive ||
                        !fogParams.IsCreated ||
                        fogParams.Length <= 0)
                    {
                        return false;
                    }

                    fogParams[0] = dto;
                    _lastAuthoredParams = dto;
                    _hasAuthoredParams = true;
                    return true;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _paramsHandle, OwnerSystemId);
                }
            }

            private bool TryWriteAndUploadMockLights(
                IDataVault vault,
                float3 cameraPosition,
                float3 cameraForward,
                int desiredPointLightCount,
                float visualPhaseSeconds,
                out int activePointLightCount)
            {
                activePointLightCount = _lastUploadedPointLightCount;
                if (_mockLightsJobPending && !_mockLightsJobHandle.IsCompleted)
                    return true;

                if (!TryAcquireFogWriteBuffer(vault, in _pointLightsHandle, BufferID.ShinobuVolumetricFogPointLights, VolumetricFogConstants.MaxPointLights, out NativeArray<PointLightDTO> pointLights))
                    return false;

                bool success = false;
                bool uploadAfterLock = false;
                int uploadPointLightCount = activePointLightCount;
                try
                {
                    if (vault.IsCompactionFenceActive ||
                        !pointLights.IsCreated ||
                        pointLights.Length < VolumetricFogConstants.MaxPointLights)
                    {
                        return false;
                    }

                    if (RefreshCompletedLightJob(pointLights, out int completedPointLightCount))
                    {
                        CopyPointLightsToUploadScratch(pointLights, completedPointLightCount);
                        uploadPointLightCount = completedPointLightCount;
                        uploadAfterLock = true;
                    }

                    if (WriteMockLightsInline(pointLights, cameraPosition, cameraForward, desiredPointLightCount, visualPhaseSeconds, out int authoredPointLightCount))
                    {
                        CopyPointLightsToUploadScratch(pointLights, authoredPointLightCount);
                        uploadPointLightCount = authoredPointLightCount;
                        uploadAfterLock = true;
                    }

                    success = true;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _pointLightsHandle, OwnerSystemId);
                }

                if (!success)
                    return false;

                if (uploadAfterLock)
                {
                    UploadPointLightsIfDirty(_pointLightUploadScratch, uploadPointLightCount);
                    activePointLightCount = _lastUploadedPointLightCount;
                }

                return true;
            }

            private bool TryRecordTelemetry(
                IDataVault vault,
                in FogConstantsDTO dto,
                float3 cameraPosition,
                int raySteps,
                float renderScale,
                float estimatedGpuMicroseconds,
                int pointLightCount,
                bool hasMarineFogTexture,
                bool hasAbyssalFlowTexture)
            {
                if (!TryAcquireFogWriteBuffer(vault, in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing, VolumetricFogConstants.TelemetryCapacity, out NativeArray<VolumetricFogTelemetryEntry> telemetry))
                    return false;

                try
                {
                    if (vault.IsCompactionFenceActive ||
                        !telemetry.IsCreated ||
                        telemetry.Length < VolumetricFogConstants.TelemetryCapacity)
                    {
                        return false;
                    }

                    RecordTelemetry(telemetry, in dto, cameraPosition, raySteps, renderScale, estimatedGpuMicroseconds, pointLightCount, hasMarineFogTexture, hasAbyssalFlowTexture);
                    return true;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
                }
            }

            private void UpdateExternalOverrideState(in FogConstantsDTO existing)
            {
                if (!IsUsableVaultOverride(in existing))
                {
                    _externalOverrideParams = default;
                    _hasExternalOverrideParams = false;
                    return;
                }

                if (_hasAuthoredParams && AreParamsEqual(in existing, in _lastAuthoredParams))
                    return;

                _externalOverrideParams = existing;
                _hasExternalOverrideParams = true;
            }

            private void ResetAuthoredOverrideState()
            {
                _lastAuthoredParams = default;
                _externalOverrideParams = default;
                _hasAuthoredParams = false;
                _hasExternalOverrideParams = false;
            }

            private static bool IsUsableVaultOverride(in FogConstantsDTO dto)
            {
                return VolumetricFogParamsAccess.IsUsableParams(in dto);
            }

            private static float3 ResolveCameraAupLocalPosition(Camera camera, double3 runtimeOriginAup)
            {
                if (camera == null)
                    return float3.zero;

                Vector3 runtimePosition = camera.transform.position;
                float3 runtime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                if (!math.all(math.isfinite(runtime)))
                    return float3.zero;

                double3 safeOriginAup = math.all(math.isfinite(runtimeOriginAup)) ? runtimeOriginAup : double3.zero;
                double3 cameraAup = safeOriginAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                double3 local = cameraAup - safeOriginAup;
                local = math.clamp(local, new double3(-MaxCameraLocalAupMeters), new double3(MaxCameraLocalAupMeters));
                float3 result = new float3((float)local.x, (float)local.y, (float)local.z);
                return math.all(math.isfinite(result)) ? result : float3.zero;
            }

            private static float3 ResolveCameraForward(Camera camera)
            {
                if (camera == null)
                    return new float3(0f, 0f, 1f);

                Vector3 forwardVector = camera.transform.forward;
                float3 forward = new float3(forwardVector.x, forwardVector.y, forwardVector.z);
                float lengthSq = math.lengthsq(forward);
                if (!math.isfinite(lengthSq) || lengthSq <= 1e-6f)
                    return new float3(0f, 0f, 1f);

                return forward * math.rsqrt(lengthSq);
            }

            private void ApplyExtinctionProfileFromVault(ref Color fogColor, ref float baseDensity, ref float extinctionCoefficient, float3 cameraPosition)
            {
                if (_vault == null ||
                    _vault.IsCompactionFenceActive ||
                    !TryReadFogBuffer(_vault, in _extinctionProfilesHandle, BufferID.ShinobuVolumetricFogExtinctionProfiles, VolumetricFogConstants.ExtinctionProfileCapacity, out NativeArray<WaterExtinctionProfileDTO>.ReadOnly profiles) ||
                    _vault.IsCompactionFenceActive)
                {
                    return;
                }

                if (!profiles.IsCreated || profiles.Length <= 0)
                    return;

                float cameraDepthMeters = ResolveFiniteNonNegative(-cameraPosition.y, 0f);
                for (int i = 0; i < profiles.Length; i++)
                {
                    WaterExtinctionProfileDTO profile = profiles[i];
                    float minDepth = ResolveFiniteClamped(profile.MinDepthMeters, 0f, 19999.999f, 0f);
                    float rawMaxDepth = ResolveFiniteClamped(profile.MaxDepthMeters, 0f, 20000f, 20000f);
                    float maxDepth = math.max(minDepth + 0.001f, rawMaxDepth);
                    if (profile.ProfileHash == 0u ||
                        cameraDepthMeters < minDepth ||
                        cameraDepthMeters > maxDepth)
                    {
                        continue;
                    }

                    float densityMultiplier = ResolveFiniteClamped(profile.DensityMultiplier, 0f, 8f, 1f);
                    float3 absorption = new float3(
                        ResolveFiniteClamped(profile.AbsorptionAndScatter.x, 0.0015f, 8f, 0.035f),
                        ResolveFiniteClamped(profile.AbsorptionAndScatter.y, 0.0023f, 8f, 0.075f),
                        ResolveFiniteClamped(profile.AbsorptionAndScatter.z, 0.0031f, 8f, 0.11f));
                    float scatter = ResolveFiniteClamped(profile.AbsorptionAndScatter.w, 0.0001f, 2f, 0.65f);
                    fogColor = new Color(
                        math.lerp(fogColor.r, absorption.x, 0.35f),
                        math.lerp(fogColor.g, absorption.y, 0.35f),
                        math.lerp(fogColor.b, absorption.z, 0.35f),
                        fogColor.a);
                    baseDensity = ResolveFiniteClamped(baseDensity * math.max(0.001f, densityMultiplier), 0f, 0.3f, baseDensity);
                    extinctionCoefficient = math.lerp(extinctionCoefficient, scatter, 0.5f);
                    return;
                }
            }

            private static void ApplyBiomeTransitionGlobals(
                ref Color fogColor,
                ref float baseDensity,
                ref float extinctionCoefficient,
                in Vector4 biomeFogColor,
                in Vector4 biomeAbsorption,
                in Vector4 biomeWeights)
            {
                float weightSum =
                    ResolveFiniteClamped(biomeWeights.x, 0f, 1f, 0f) +
                    ResolveFiniteClamped(biomeWeights.y, 0f, 1f, 0f) +
                    ResolveFiniteClamped(biomeWeights.z, 0f, 1f, 0f) +
                    ResolveFiniteClamped(biomeWeights.w, 0f, 1f, 0f);
                float blend = ResolveFiniteSaturated(weightSum);
                if (blend <= 0.0001f)
                    return;

                float biomeR = ResolveFiniteClamped(biomeFogColor.x, 0.0015f, 8f, fogColor.r);
                float biomeG = ResolveFiniteClamped(biomeFogColor.y, 0.0023f, 8f, fogColor.g);
                float biomeB = ResolveFiniteClamped(biomeFogColor.z, 0.0031f, 8f, fogColor.b);
                float absorptionR = ResolveFiniteClamped(biomeAbsorption.x, 0.0015f, 8f, extinctionCoefficient);
                float absorptionG = ResolveFiniteClamped(biomeAbsorption.y, 0.0023f, 8f, extinctionCoefficient);
                float absorptionB = ResolveFiniteClamped(biomeAbsorption.z, 0.0031f, 8f, extinctionCoefficient);
                float biomeDensity = ResolveFiniteClamped(biomeAbsorption.w * 0.04f, 0f, 0.3f, baseDensity);
                float biomeExtinction = ResolveFiniteClamped((absorptionR + absorptionG + absorptionB) * (1f / 3f), 0.0001f, 2f, extinctionCoefficient);

                fogColor = new Color(
                    math.lerp(fogColor.r, biomeR, blend),
                    math.lerp(fogColor.g, biomeG, blend),
                    math.lerp(fogColor.b, biomeB, blend),
                    fogColor.a);
                baseDensity = math.lerp(baseDensity, biomeDensity, blend);
                extinctionCoefficient = math.lerp(extinctionCoefficient, biomeExtinction, blend);
            }

            private void UploadConstantBufferIfDirty(in FogConstantsDTO dto)
            {
                uint dtoHash = HashParams(in dto);
                if (_hasUploadedParams &&
                    dtoHash == _lastUploadedParamsHash &&
                    AreParamsEqual(in dto, in _lastUploadedParams))
                {
                    return;
                }

                GraphicsBuffer target = GetInactiveParamsBuffer();
                if (target == null || !target.IsValid())
                    return;

                NativeArray<FogConstantsDTO> mapped = target.LockBufferForWrite<FogConstantsDTO>(0, ConstantBufferCount);
                try
                {
                    unsafe
                    {
                        FogConstantsDTO local = dto;
                        void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                        void* source = UnsafeUtility.AddressOf(ref local);
                        UnsafeUtility.MemCpy(destination, source, VolumetricFogConstants.ParamsStrideBytes);
                    }
                }
                finally
                {
                    target.UnlockBufferAfterWrite<FogConstantsDTO>(ConstantBufferCount);
                }

                _activeParamsBufferIndex = 1 - _activeParamsBufferIndex;
                _lastUploadedParams = dto;
                _lastUploadedParamsHash = dtoHash;
                _hasUploadedParams = true;
            }

            private unsafe void UploadFrameConstantBuffer(
                in Vector4 fullSize,
                in Vector4 halfSize,
                in Vector4 compositeParams,
                in Vector4 debugParams,
                in Vector4 marineFogTexelSize,
                in Vector4 marineFogParams,
                in Vector4 abyssalFlowCenter,
                in Vector4 abyssalFlowSpacing,
                in Vector4 abyssalFlowTextureParams,
                float abyssalFlowTextureActive,
                in Matrix4x4 inverseViewProjection)
            {
                GraphicsBuffer target = GetInactiveFrameParamsBuffer();
                if (target == null || !target.IsValid())
                    return;

                FogFrameConstantsDTO dto = new FogFrameConstantsDTO
                {
                    FullSize = fullSize,
                    HalfSize = halfSize,
                    CompositeParams = compositeParams,
                    DebugParams = debugParams,
                    MarineFogTexelSize = marineFogTexelSize,
                    MarineFogParams = marineFogParams,
                    AbyssalFlowCenter = abyssalFlowCenter,
                    AbyssalFlowSpacing = abyssalFlowSpacing,
                    AbyssalFlowTextureParams = abyssalFlowTextureParams,
                    AbyssalFlowActiveAndPad = new Vector4(ResolveFiniteSaturated(abyssalFlowTextureActive), 0f, 0f, 0f),
                    InverseViewProjectionC0 = inverseViewProjection.GetColumn(0),
                    InverseViewProjectionC1 = inverseViewProjection.GetColumn(1),
                    InverseViewProjectionC2 = inverseViewProjection.GetColumn(2),
                    InverseViewProjectionC3 = inverseViewProjection.GetColumn(3)
                };

                NativeArray<FogFrameConstantsDTO> mapped = target.LockBufferForWrite<FogFrameConstantsDTO>(0, ConstantBufferCount);
                try
                {
                    void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                    void* source = UnsafeUtility.AddressOf(ref dto);
                    UnsafeUtility.MemCpy(destination, source, FrameParamsStrideBytes);
                }
                finally
                {
                    target.UnlockBufferAfterWrite<FogFrameConstantsDTO>(ConstantBufferCount);
                }

                _activeFrameParamsBufferIndex = 1 - _activeFrameParamsBufferIndex;
            }

            private static uint HashParams(in FogConstantsDTO dto)
            {
                uint4 hashLane = new uint4(
                    math.hash(math.asuint(dto.FogColorAndDensity)),
                    math.hash(math.asuint(dto.ScatteringParams)),
                    math.hash(math.asuint(dto.FlowAdvection)),
                    math.hash(math.asuint(dto.QualityAndLimits)));
                return math.hash(hashLane);
            }

            private static bool AreParamsEqual(in FogConstantsDTO left, in FogConstantsDTO right)
            {
                return math.all(left.FogColorAndDensity == right.FogColorAndDensity) &&
                       math.all(left.ScatteringParams == right.ScatteringParams) &&
                       math.all(left.FlowAdvection == right.FlowAdvection) &&
                       math.all(left.QualityAndLimits == right.QualityAndLimits);
            }

            private bool WriteMockLightsInline(
                NativeArray<PointLightDTO> pointLights,
                float3 cameraPosition,
                float3 cameraForward,
                int desiredPointLightCount,
                float visualPhaseSeconds,
                out int authoredPointLightCount)
            {
                authoredPointLightCount = 0;
                if (!pointLights.IsCreated)
                    return false;

                int safeDesiredPointLightCount = math.clamp(desiredPointLightCount, 1, VolumetricFogConstants.MaxPointLights);
                uint scheduleHash = math.hash(new uint4(
                    math.asuint(_qualityWeight),
                    math.asuint(visualPhaseSeconds),
                    (uint)safeDesiredPointLightCount,
                    0x51A0B120u));
                if (_hasScheduledPointLightJob &&
                    safeDesiredPointLightCount == _lastScheduledPointLightCount &&
                    scheduleHash == _lastScheduledPointLightHash)
                {
                    return false;
                }

                _pendingPointLightCount = safeDesiredPointLightCount;
                BuildMockVolumetricLightsJob lightJob = new BuildMockVolumetricLightsJob
                {
                    PointLights = pointLights,
                    CameraPositionWS = cameraPosition,
                    CameraForwardWS = cameraForward,
                    FramePhaseSeconds = visualPhaseSeconds,
                    QualityWeight = _qualityWeight
                };
                lightJob.Execute();
                _lastScheduledPointLightCount = safeDesiredPointLightCount;
                _lastScheduledPointLightHash = scheduleHash;
                _hasScheduledPointLightJob = true;
                _pendingPointLightCount = 0;
                authoredPointLightCount = safeDesiredPointLightCount;
                return true;
            }

            private bool RefreshCompletedLightJob(NativeArray<PointLightDTO> pointLights, out int completedPointLightCount)
            {
                completedPointLightCount = 0;
                if (!_mockLightsJobPending || !_mockLightsJobHandle.IsCompleted)
                    return false;

                if (!DispatcherJobFence.TryFinalizeCompleted(ref _mockLightsJobHandle))
                    return false;

                _mockLightsJobPending = false;
                completedPointLightCount = math.clamp(_pendingPointLightCount, 0, VolumetricFogConstants.MaxPointLights);
                _pendingPointLightCount = 0;
                return pointLights.IsCreated && completedPointLightCount > 0;
            }

            private void CopyPointLightsToUploadScratch(NativeArray<PointLightDTO> pointLights, int pointLightCount)
            {
                int safeCount = pointLights.IsCreated
                    ? math.clamp(pointLightCount, 0, math.min(pointLights.Length, VolumetricFogConstants.MaxPointLights))
                    : 0;

                for (int i = 0; i < safeCount; i++)
                    _pointLightUploadScratch[i] = pointLights[i];

                for (int i = safeCount; i < VolumetricFogConstants.MaxPointLights; i++)
                    _pointLightUploadScratch[i] = default;
            }

            private void UploadPointLightsIfDirty(PointLightDTO[] pointLights, int completedPointLightCount)
            {
                GraphicsBuffer target = GetInactivePointLightBuffer();
                if (pointLights == null || target == null || !target.IsValid())
                    return;

                completedPointLightCount = math.clamp(completedPointLightCount, 0, VolumetricFogConstants.MaxPointLights);
                uint pointLightsHash = HashPointLights(pointLights, completedPointLightCount);
                if (_hasUploadedPointLights &&
                    completedPointLightCount == _lastUploadedPointLightCount &&
                    pointLightsHash == _lastUploadedPointLightsHash)
                {
                    return;
                }

                UploadPointLights(target, pointLights);
                _activePointLightBufferIndex = 1 - _activePointLightBufferIndex;
                _lastUploadedPointLightCount = completedPointLightCount;
                _lastUploadedPointLightsHash = pointLightsHash;
                _hasUploadedPointLights = true;
            }

            private static uint HashPointLights(PointLightDTO[] pointLights, int count)
            {
                if (pointLights == null)
                    return 0u;

                int safeCount = math.clamp(count, 0, math.min(pointLights.Length, VolumetricFogConstants.MaxPointLights));
                uint hash = math.hash(new uint4((uint)safeCount, 0x120120u, 0xC0DEF06u, 0x5EED5u));
                for (int i = 0; i < safeCount; i++)
                {
                    PointLightDTO light = pointLights[i];
                    uint4 laneHash = new uint4(
                        math.hash(math.asuint(light.PositionRadius)),
                        math.hash(math.asuint(light.ColorIntensity)),
                        hash,
                        (uint)i);
                    hash = math.hash(laneHash);
                }

                return hash;
            }

            private static void UploadPointLights(GraphicsBuffer target, PointLightDTO[] pointLights)
            {
                int count = pointLights == null ? 0 : math.min(target.count, pointLights.Length);
                if (count <= 0)
                    return;

                NativeArray<PointLightDTO> mapped = target.LockBufferForWrite<PointLightDTO>(0, count);
                try
                {
                    for (int i = 0; i < count; i++)
                        mapped[i] = pointLights[i];
                }
                finally
                {
                    target.UnlockBufferAfterWrite<PointLightDTO>(count);
                }
            }

            private GraphicsBuffer GetActivePointLightBuffer()
            {
                return _activePointLightBufferIndex == 0 ? _pointLightBufferA : _pointLightBufferB;
            }

            private GraphicsBuffer GetInactivePointLightBuffer()
            {
                return _activePointLightBufferIndex == 0 ? _pointLightBufferB : _pointLightBufferA;
            }

            private GraphicsBuffer GetActiveParamsBuffer()
            {
                return _activeParamsBufferIndex == 0 ? _paramsBufferA : _paramsBufferB;
            }

            private GraphicsBuffer GetInactiveParamsBuffer()
            {
                return _activeParamsBufferIndex == 0 ? _paramsBufferB : _paramsBufferA;
            }

            private GraphicsBuffer GetActiveFrameParamsBuffer()
            {
                return _activeFrameParamsBufferIndex == 0 ? _frameParamsBufferA : _frameParamsBufferB;
            }

            private GraphicsBuffer GetInactiveFrameParamsBuffer()
            {
                return _activeFrameParamsBufferIndex == 0 ? _frameParamsBufferB : _frameParamsBufferA;
            }

            private static float3 ResolveWrappedNoiseOffset(float3 cameraPosition)
            {
                const float wrapMeters = 256f;
                return new float3(
                    math.fmod(math.fmod(cameraPosition.x, wrapMeters) + wrapMeters, wrapMeters),
                    math.fmod(math.fmod(cameraPosition.y, wrapMeters) + wrapMeters, wrapMeters),
                    math.fmod(math.fmod(cameraPosition.z, wrapMeters) + wrapMeters, wrapMeters));
            }

            private void RecordTelemetry(
                NativeArray<VolumetricFogTelemetryEntry> telemetry,
                in FogConstantsDTO dto,
                float3 cameraPosition,
                int raySteps,
                float renderScale,
                float estimatedGpuMicroseconds,
                int pointLightCount,
                bool hasMarineFogTexture,
                bool hasAbyssalFlowTexture)
            {
                int index = _telemetryWriteIndex % VolumetricFogConstants.TelemetryCapacity;
                uint flags = 0u;
                if (dto.QualityAndLimits.w > 0.5f)
                    flags |= 1u;
                if (_settings.debugHeatmapWeight > 0.001f)
                    flags |= 2u;
                if (!hasMarineFogTexture)
                    flags |= 4u;
                if (hasAbyssalFlowTexture)
                    flags |= 8u;
                bool invalidEstimatedGpuTime = !IsFinite(estimatedGpuMicroseconds);
                if (invalidEstimatedGpuTime)
                    flags |= 16u;

                float safeEstimatedGpuMicroseconds = invalidEstimatedGpuTime
                    ? 0f
                    : math.max(0f, estimatedGpuMicroseconds);
                float safeDebugHeatmapWeight = ResolveFiniteSaturated(_settings.debugHeatmapWeight);

                telemetry[index] = new VolumetricFogTelemetryEntry
                {
                    FrameIndex = unchecked((uint)_frameIndex),
                    RaySteps = raySteps,
                    RenderScale = renderScale,
                    EstimatedGpuMicroseconds = safeEstimatedGpuMicroseconds,
                    CameraPositionLocalAndQuality = new float4(cameraPosition, _qualityWeight),
                    StateHash = math.hash(new float4(_qualityWeight, raySteps, renderScale, pointLightCount)),
                    Flags = flags,
                    AccumulatedDensity = dto.FogColorAndDensity.w,
                    MaxRayDistance = dto.QualityAndLimits.z,
                    DebugValues = new float4(dto.QualityAndLimits.w, safeDebugHeatmapWeight, pointLightCount, safeEstimatedGpuMicroseconds)
                };
                _telemetryWriteIndex = (_telemetryWriteIndex + 1) % VolumetricFogConstants.TelemetryCapacity;

                if (!_dumpedThisSession &&
                    (invalidEstimatedGpuTime || safeEstimatedGpuMicroseconds > DumpThresholdMicroseconds))
                {
                    _deferredDumpRequested = true;
                    _dumpedThisSession = true;
                }
            }

            private bool TryReadTelemetryDumpLength(out int telemetryLength)
            {
                telemetryLength = 0;
                if (_vault == null ||
                    _vault.IsCompactionFenceActive ||
                    !TryReadFogBuffer(_vault, in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing, VolumetricFogConstants.TelemetryCapacity, out NativeArray<VolumetricFogTelemetryEntry>.ReadOnly telemetry) ||
                    _vault.IsCompactionFenceActive ||
                    !telemetry.IsCreated)
                {
                    return false;
                }

                telemetryLength = math.min(telemetry.Length, VolumetricFogConstants.TelemetryCapacity);
                return telemetryLength > 0;
            }

            private bool TryReadTelemetryDumpEntry(int index, out VolumetricFogTelemetryEntry entry)
            {
                entry = default;
                if (_vault == null ||
                    _vault.IsCompactionFenceActive ||
                    index < 0 ||
                    !TryReadFogBuffer(_vault, in _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing, VolumetricFogConstants.TelemetryCapacity, out NativeArray<VolumetricFogTelemetryEntry>.ReadOnly telemetry) ||
                    _vault.IsCompactionFenceActive ||
                    !telemetry.IsCreated ||
                    index >= telemetry.Length)
                {
                    return false;
                }

                entry = telemetry[index];
                return !_vault.IsCompactionFenceActive;
            }

            private unsafe void DumpTelemetryRing(int telemetryLength)
            {
                if (telemetryLength <= 0)
                    return;

                try
                {
                    string path = Path.Combine(ResolveProjectRoot(), DumpRelativePath);
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    int stride = UnsafeUtility.SizeOf<VolumetricFogTelemetryEntry>();
                    byte* rowBytes = stackalloc byte[stride];
                    using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        for (int i = 0; i < telemetryLength; i++)
                        {
                            if (!TryReadTelemetryDumpEntry(i, out VolumetricFogTelemetryEntry entry))
                                return;

                            UnsafeUtility.MemCpy(rowBytes, &entry, stride);
                            stream.Write(new ReadOnlySpan<byte>(rowBytes, stride));
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }

            private static string ResolveProjectRoot()
            {
                string dataPath = Application.dataPath;
                DirectoryInfo directory = Directory.GetParent(dataPath);
                return directory != null ? directory.FullName : dataPath;
            }

            private static bool IsFinite(float value)
            {
                return !float.IsNaN(value) && !float.IsInfinity(value);
            }

            private static float ResolveVisualPhaseSeconds(float qualityWeight, int frameIndex)
            {
                float curved = ResolveQualityCurve(qualityWeight);
                float updateHz = math.lerp(5f, 60f, curved);
                int cadenceFrames = math.clamp((int)math.round(60f / math.max(5f, updateHz)), 1, 12);
                uint frame = unchecked((uint)math.max(0, frameIndex));
                uint cadence = (uint)cadenceFrames;
                uint quantizedFrame = frame - frame % cadence;
                return quantizedFrame * (1f / 60f);
            }

            private static float EstimateGpuMicroseconds(int halfWidth, int halfHeight, int volumeWidth, int volumeHeight, int raySteps, int pointLightCount, float renderScale)
            {
                float halfPixels = math.max(1, halfWidth) * math.max(1, halfHeight);
                float volumeVoxels = math.max(1, volumeWidth) * math.max(1, volumeHeight) * math.max(1, raySteps);
                float lightMultiplier = 1f + math.max(0, pointLightCount) * 0.075f;
                float scalePenalty = math.lerp(0.85f, 1.25f, ResolveFiniteSaturated(renderScale));
                return (halfPixels * math.max(1, raySteps) * 0.000011f + volumeVoxels * 0.000007f) * lightMultiplier * scalePenalty;
            }

            private static float EstimateProxyMicroseconds(int halfWidth, int halfHeight, int fullWidth, int fullHeight, float renderScale)
            {
                float halfPixels = math.max(1, halfWidth) * math.max(1, halfHeight);
                float fullPixels = math.max(1, fullWidth) * math.max(1, fullHeight);
                float scalePenalty = math.lerp(0.65f, 1f, ResolveFiniteSaturated(renderScale));
                return (halfPixels * 0.000009f + fullPixels * 0.000003f) * scalePenalty;
            }

            private static bool IsUsableMarineFogTexture(Texture texture, in Vector4 marineFogParams)
            {
                if (texture == null ||
                    texture.dimension != TextureDimension.Tex2D ||
                    marineFogParams.w <= 0.5f)
                {
                    return false;
                }

                if (texture is RenderTexture renderTexture)
                    return renderTexture.graphicsFormat == GraphicsFormat.R32_SInt ||
                           renderTexture.format == RenderTextureFormat.RInt;

                if (texture is Texture2D texture2D)
                    return texture2D.graphicsFormat == GraphicsFormat.R32_SInt;

                return false;
            }

            private static bool IsUsableAbyssalFlowTexture(Texture texture)
            {
                if (texture == null || texture.dimension != TextureDimension.Tex3D)
                    return false;

                if (texture is RenderTexture renderTexture)
                {
                    return renderTexture.IsCreated() &&
                           renderTexture.volumeDepth > 0 &&
                           IsSupportedFloat4VolumeFormat(renderTexture.graphicsFormat);
                }

                if (texture is Texture3D texture3D)
                {
                    return texture3D.width > 0 &&
                           texture3D.height > 0 &&
                           texture3D.depth > 0 &&
                           IsSupportedFloat4VolumeFormat(texture3D.graphicsFormat);
                }

                return false;
            }

            private static bool IsSupportedFloat4VolumeFormat(GraphicsFormat format)
            {
                return format == GraphicsFormat.R16G16B16A16_SFloat ||
                       format == GraphicsFormat.R32G32B32A32_SFloat;
            }

            private bool EnsureDearLieProxyMaterial(bool allowAllocation)
            {
                Shader shader = _settings != null ? _settings.dearLieProxyShader : null;
                if (shader == null)
                    RuntimeShaderReferenceCatalog.TryGetVolumetricFogDearLieProxyShader(out shader);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (shader == null)
                    shader = Shader.Find(DearLieProxyShaderName);
#endif

                if (shader == null)
                    return false;

                if (_settings != null && _settings.dearLieProxyShader == null)
                    _settings.dearLieProxyShader = shader;

                if (_dearLieProxyMaterial != null &&
                    ReferenceEquals(_dearLieProxyShader, shader))
                {
                    return true;
                }

                if (!allowAllocation)
                    return false;

                DestroyUnityObject(_dearLieProxyMaterial);
                _dearLieProxyMaterial = CoreUtils.CreateEngineMaterial(shader); // COLD ALLOC: material for raster Dear Lie proxy/composite only.
                _dearLieProxyShader = shader;
                return _dearLieProxyMaterial != null;
            }

            private bool EnsureGpuBuffers(bool allowAllocation)
            {
                if (!SystemInfo.supportsSetConstantBuffer ||
                    !ValidateFrameConstantsLayout())
                {
                    return false;
                }

                bool needsAllocation =
                    _paramsBufferA == null || !_paramsBufferA.IsValid() ||
                    _paramsBufferB == null || !_paramsBufferB.IsValid() ||
                    _frameParamsBufferA == null || !_frameParamsBufferA.IsValid() ||
                    _frameParamsBufferB == null || !_frameParamsBufferB.IsValid() ||
                    _pointLightBufferA == null || !_pointLightBufferA.IsValid() ||
                    _pointLightBufferB == null || !_pointLightBufferB.IsValid();
                if (needsAllocation && !allowAllocation)
                    return false;

                bool createdParamsBuffer = false;
                if (_paramsBufferA == null || !_paramsBufferA.IsValid())
                {
                    _paramsBufferA?.Release();
                    _paramsBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        ConstantBufferCount,
                        VolumetricFogConstants.ParamsStrideBytes); // COLD ALLOC: GraphicsBuffer[64B] - 13KRA volumetric fog params A.
                    createdParamsBuffer = true;
                }

                if (_paramsBufferB == null || !_paramsBufferB.IsValid())
                {
                    _paramsBufferB?.Release();
                    _paramsBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        ConstantBufferCount,
                        VolumetricFogConstants.ParamsStrideBytes); // COLD ALLOC: GraphicsBuffer[64B] - 13KRA volumetric fog params B.
                    createdParamsBuffer = true;
                }

                if (createdParamsBuffer)
                {
                    _hasUploadedParams = false;
                    _lastUploadedParams = default;
                    _lastUploadedParamsHash = 0u;
                    _activeParamsBufferIndex = 0;
                    ResetAuthoredOverrideState();
                }

                bool createdFrameParamsBuffer = false;
                if (_frameParamsBufferA == null || !_frameParamsBufferA.IsValid())
                {
                    _frameParamsBufferA?.Release();
                    _frameParamsBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        ConstantBufferCount,
                        FrameParamsStrideBytes); // COLD ALLOC: GraphicsBuffer[224B] - 13KRA frame params A.
                    createdFrameParamsBuffer = true;
                }

                if (_frameParamsBufferB == null || !_frameParamsBufferB.IsValid())
                {
                    _frameParamsBufferB?.Release();
                    _frameParamsBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        ConstantBufferCount,
                        FrameParamsStrideBytes); // COLD ALLOC: GraphicsBuffer[224B] - 13KRA frame params B.
                    createdFrameParamsBuffer = true;
                }

                if (createdFrameParamsBuffer)
                    _activeFrameParamsBufferIndex = 0;

                bool createdPointLightBuffer = false;
                if (_pointLightBufferA == null || !_pointLightBufferA.IsValid())
                {
                    _pointLightBufferA?.Release();
                    _pointLightBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        VolumetricFogConstants.MaxPointLights,
                        VolumetricFogConstants.PointLightStrideBytes); // COLD ALLOC: GraphicsBuffer[PointLightDTO x8] - 13KRA fog lights buffer A.
                    createdPointLightBuffer = true;
                }

                if (_pointLightBufferB == null || !_pointLightBufferB.IsValid())
                {
                    _pointLightBufferB?.Release();
                    _pointLightBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        VolumetricFogConstants.MaxPointLights,
                        VolumetricFogConstants.PointLightStrideBytes); // COLD ALLOC: GraphicsBuffer[PointLightDTO x8] - 13KRA fog lights buffer B.
                    createdPointLightBuffer = true;
                }

                if (createdPointLightBuffer)
                {
                    ClearPointLightBuffer(_pointLightBufferA);
                    ClearPointLightBuffer(_pointLightBufferB);
                    _activePointLightBufferIndex = 0;
                    ResetPointLightScheduleState();
                }

                return HasGpuBuffers();
            }

            private bool HasGpuBuffers()
            {
                return _paramsBufferA != null && _paramsBufferA.IsValid() &&
                       _paramsBufferB != null && _paramsBufferB.IsValid() &&
                       _frameParamsBufferA != null && _frameParamsBufferA.IsValid() &&
                       _frameParamsBufferB != null && _frameParamsBufferB.IsValid() &&
                       _pointLightBufferA != null && _pointLightBufferA.IsValid() &&
                       _pointLightBufferB != null && _pointLightBufferB.IsValid();
            }

            private static bool ValidateFrameConstantsLayout()
            {
                return UnsafeUtility.SizeOf<FogFrameConstantsDTO>() == FrameParamsStrideBytes &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.FullSize)) == 0 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.HalfSize)) == 16 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.CompositeParams)) == 32 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.DebugParams)) == 48 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.MarineFogTexelSize)) == 64 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.MarineFogParams)) == 80 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.AbyssalFlowCenter)) == 96 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.AbyssalFlowSpacing)) == 112 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.AbyssalFlowTextureParams)) == 128 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.AbyssalFlowActiveAndPad)) == 144 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.InverseViewProjectionC0)) == 160 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.InverseViewProjectionC1)) == 176 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.InverseViewProjectionC2)) == 192 &&
                       OffsetOf<FogFrameConstantsDTO>(nameof(FogFrameConstantsDTO.InverseViewProjectionC3)) == 208;
            }

            private static int OffsetOf<T>(string fieldName) where T : unmanaged
            {
                return Marshal.OffsetOf<T>(fieldName).ToInt32();
            }

            private static void ClearPointLightBuffer(GraphicsBuffer buffer)
            {
                if (buffer == null || !buffer.IsValid())
                    return;

                int count = math.min(buffer.count, VolumetricFogConstants.MaxPointLights);
                if (count <= 0)
                    return;

                NativeArray<PointLightDTO> mapped = buffer.LockBufferForWrite<PointLightDTO>(0, count);
                try
                {
                    for (int i = 0; i < count; i++)
                        mapped[i] = default;
                }
                finally
                {
                    buffer.UnlockBufferAfterWrite<PointLightDTO>(count);
                }
            }

            private void ResetPointLightScheduleState()
            {
                _lastUploadedPointLightCount = 0;
                _pendingPointLightCount = 0;
                _lastScheduledPointLightCount = 0;
                _lastUploadedPointLightsHash = 0u;
                _lastScheduledPointLightHash = 0u;
                _hasUploadedPointLights = false;
                _hasScheduledPointLightJob = false;
            }

            private static bool IsFogHandle<T>(
                in VaultGenerationHandle<T> handle,
                BufferID bufferId) where T : unmanaged
            {
                return handle.BufferID == unchecked((uint)(int)bufferId) &&
                       handle.SystemID == (uint)OwnerSystemId &&
                       handle.Generation != 0u;
            }

            private static bool TryReadFogBuffer<T>(
                IDataVault vault,
                in VaultGenerationHandle<T> handle,
                BufferID bufferId,
                int minLength,
                out NativeArray<T>.ReadOnly buffer) where T : unmanaged
            {
                buffer = default;
                return vault != null &&
                       !vault.IsCompactionFenceActive &&
                       minLength > 0 &&
                       IsFogHandle(in handle, bufferId) &&
                       vault.TryReadOnlyHandle(in handle, out buffer) &&
                       !vault.IsCompactionFenceActive &&
                       buffer.IsCreated &&
                       buffer.Length >= minLength;
            }

            private static bool TryAcquireFogWriteBuffer<T>(
                IDataVault vault,
                in VaultGenerationHandle<T> handle,
                BufferID bufferId,
                int minLength,
                out NativeArray<T> buffer) where T : unmanaged
            {
                buffer = default;
                if (vault == null ||
                    vault.IsCompactionFenceActive ||
                    minLength <= 0 ||
                    !IsFogHandle(in handle, bufferId) ||
                    !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
                {
                    return false;
                }

                bool releaseOnExit = true;
                try
                {
                    if (vault.IsCompactionFenceActive ||
                        !buffer.IsCreated ||
                        buffer.Length < minLength)
                    {
                        buffer = default;
                        return false;
                    }

                    releaseOnExit = false;
                    return true;
                }
                finally
                {
                    if (releaseOnExit)
                        vault.ReleaseWriteLock(in handle, OwnerSystemId);
                }
            }

            private void ReleaseVaultHandles()
            {
                IDataVault vault = _vault;
                ReleaseFogVaultHandle(vault, ref _paramsHandle, BufferID.ShinobuVolumetricFogParams);
                ReleaseFogVaultHandle(vault, ref _pointLightsHandle, BufferID.ShinobuVolumetricFogPointLights);
                ReleaseFogVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuVolumetricFogTelemetryRing);
                ReleaseFogVaultHandle(vault, ref _extinctionProfilesHandle, BufferID.ShinobuVolumetricFogExtinctionProfiles);
            }

            private static void ReleaseFogVaultHandle<T>(
                IDataVault vault,
                ref VaultGenerationHandle<T> handle,
                BufferID bufferId) where T : unmanaged
            {
                if (vault != null && IsFogHandle(in handle, bufferId))
                    vault.ReleaseBuffer(in handle);

                handle = default;
            }

            private static TextureDesc CreateGraphTextureDesc(
                in TextureDesc sourceDesc,
                int width,
                int height,
                int slices,
                TextureDimension dimension,
                FilterMode filterMode,
                string name,
                GraphicsFormat colorFormat,
                bool enableRandomWrite,
                bool xrReady = false,
                VRTextureUsage vrUsage = VRTextureUsage.None)
            {
                TextureDesc desc = new TextureDesc(math.max(1, width), math.max(1, height), false, xrReady);
                desc.name = name;
                desc.width = math.max(1, width);
                desc.height = math.max(1, height);
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.colorFormat = colorFormat != GraphicsFormat.None ? colorFormat : sourceDesc.colorFormat;
                desc.clearBuffer = false;
                desc.dimension = dimension;
                desc.slices = math.max(1, slices);
                desc.vrUsage = vrUsage;
                desc.useDynamicScale = false;
                desc.useDynamicScaleExplicit = false;
                desc.enableRandomWrite = enableRandomWrite;
                desc.filterMode = filterMode;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                return desc;
            }

            private static GraphicsFormat ResolveCompositeColorFormat(in TextureDesc sourceDesc)
            {
                GraphicsFormat sourceFormat = sourceDesc.colorFormat;
                if (sourceFormat == GraphicsFormat.R16G16B16A16_SFloat ||
                    sourceFormat == GraphicsFormat.R32G32B32A32_SFloat ||
                    sourceFormat == GraphicsFormat.None)
                {
                    return GraphicsFormat.B10G11R11_UFloatPack32;
                }

                return sourceFormat;
            }

            private void EnsureFallbackTextures()
            {
                if (_emptyFogDensityTexture == null)
                {
                    RenderTextureDescriptor descriptor = new RenderTextureDescriptor(1, 1)
                    {
                        graphicsFormat = GraphicsFormat.R32_SInt,
                        depthBufferBits = 0,
                        msaaSamples = 1,
                        mipCount = 1,
                        volumeDepth = 1,
                        enableRandomWrite = false,
                        dimension = TextureDimension.Tex2D
                    };
                    _emptyFogDensityTexture = new RenderTexture(descriptor)
                    {
                        name = "__HectonVolumetricFogEmptySiltDensity",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Point,
                        anisoLevel = 0
                    }; // COLD ALLOC: fallback texture for inactive MarineSnow density.
                    _emptyFogDensityTexture.Create();
                }

                if (_emptyAbyssalFlowTexture == null)
                {
                    _emptyAbyssalFlowTexture = new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false)
                    {
                        name = "__HectonVolumetricFogEmptyAbyssalFlow",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        anisoLevel = 0
                    }; // COLD ALLOC: fallback Texture3D for inactive abyssal flow.
                    _emptyAbyssalFlowTexture.SetPixel(0, 0, 0, Color.clear);
                    _emptyAbyssalFlowTexture.Apply(false, true);
                }
            }

            private void ReleaseFallbackTextures()
            {
                ReleaseExternalTextureHandle(ref _marineFogDensityTextureHandle, ref _marineFogDensityTextureHandleSource);
                ReleaseExternalTextureHandle(ref _abyssalFlowTextureHandle, ref _abyssalFlowTextureHandleSource);
                ReleaseExternalTextureHandle(ref _externalMarineFogDensityTextureHandle, ref _externalMarineFogDensityTextureHandleSource);
                ReleaseExternalTextureHandle(ref _externalAbyssalFlowTextureHandle, ref _externalAbyssalFlowTextureHandleSource);
                ReleaseExternalTextureHandle(ref _externalMarineFogDensityTextureHandleB, ref _externalMarineFogDensityTextureHandleSourceB);
                ReleaseExternalTextureHandle(ref _externalAbyssalFlowTextureHandleB, ref _externalAbyssalFlowTextureHandleSourceB);
                ReleaseExternalTextureHandle(ref _emptyVolumeTextureHandle, ref _emptyVolumeTextureHandleSource);

                if (_emptyFogDensityTexture != null)
                {
                    _emptyFogDensityTexture.Release();
                    DestroyUnityObject(_emptyFogDensityTexture);
                    _emptyFogDensityTexture = null;
                }

                if (_emptyAbyssalFlowTexture != null)
                {
                    DestroyUnityObject(_emptyAbyssalFlowTexture);
                    _emptyAbyssalFlowTexture = null;
                }
            }

            private static RTHandle ResolveExternalTextureHandle(Texture texture, ref RTHandle handle, ref Texture handleSource, bool allowAllocation)
            {
                if (texture == null)
                    return null;

                if (!ReferenceEquals(texture, handleSource))
                {
                    if (!allowAllocation)
                        return null;

                    handle?.Release();
                    handleSource = texture;
                    handle = RTHandles.Alloc(texture);
                }

                return handle;
            }

            private static RTHandle TryGetExistingExternalTextureHandle(Texture texture, RTHandle handle, Texture handleSource)
            {
                return texture != null && handle != null && ReferenceEquals(texture, handleSource) ? handle : null;
            }

            private static RTHandle TryGetCachedExternalTextureHandle(
                Texture texture,
                RTHandle handleA,
                Texture handleSourceA,
                RTHandle handleB,
                Texture handleSourceB)
            {
                if (texture == null)
                    return null;

                if (handleA != null && ReferenceEquals(texture, handleSourceA))
                    return handleA;

                return handleB != null && ReferenceEquals(texture, handleSourceB) ? handleB : null;
            }

            private static RTHandle ResolveCachedExternalTextureHandle(
                Texture texture,
                ref RTHandle handleA,
                ref Texture handleSourceA,
                ref RTHandle handleB,
                ref Texture handleSourceB,
                bool allowAllocation)
            {
                if (texture == null)
                    return null;

                RTHandle cached = TryGetCachedExternalTextureHandle(texture, handleA, handleSourceA, handleB, handleSourceB);
                if (cached != null)
                    return cached;

                if (!allowAllocation)
                    return null;

                if (handleA == null)
                {
                    handleSourceA = texture;
                    handleA = RTHandles.Alloc(texture);
                    return handleA;
                }

                if (handleB == null)
                {
                    handleSourceB = texture;
                    handleB = RTHandles.Alloc(texture);
                    return handleB;
                }

                return null;
            }

            private static void ReleaseExternalTextureHandle(ref RTHandle handle, ref Texture handleSource)
            {
                handle?.Release();
                handle = null;
                handleSource = null;
            }

            private static void DestroyUnityObject(UnityEngine.Object unityObject)
            {
                if (unityObject == null)
                    return;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(unityObject);
                else
                    UnityEngine.Object.DestroyImmediate(unityObject);
            }

            private static Matrix4x4 ResolveInverseViewProjection(Camera camera, bool proxyOnly)
            {
                if (proxyOnly || camera == null)
                    return Matrix4x4.identity;

                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
                return (projectionMatrix * camera.worldToCameraMatrix).inverse;
            }

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = Mathf.Max(1, dimension);
                return ((safeDimension + RenderTextureBucketSize - 1) / RenderTextureBucketSize) * RenderTextureBucketSize;
            }

            private static int ResolveVolumeGridDimension(int halfDimension, float qualityWeight, int minDimension, int maxDimension)
            {
                float curvedQuality = ResolveQualityCurve(qualityWeight);
                int cap = Mathf.RoundToInt(Mathf.Lerp(minDimension, maxDimension, curvedQuality));
                int safeDimension = Mathf.Clamp(Mathf.Min(halfDimension, cap), minDimension, maxDimension);
                return ((safeDimension + VolumeTextureBucketSize - 1) / VolumeTextureBucketSize) * VolumeTextureBucketSize;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int ParamsBufferId = Shader.PropertyToID("HectonVolumetricFogParams");
            internal static readonly int FrameParamsBufferId = Shader.PropertyToID("HectonVolumetricFogFrameParams");
            internal static readonly int SourceColorId = Shader.PropertyToID("_HectonVolumetricFogSourceColor");
            internal static readonly int SourceDepthId = Shader.PropertyToID("_HectonVolumetricFogSourceDepth");
            internal static readonly int HalfInputId = Shader.PropertyToID("_HectonVolumetricFogHalfInput");
            internal static readonly int HalfResultId = Shader.PropertyToID("_HectonVolumetricFogHalfResult");
            internal static readonly int VolumeWriteId = Shader.PropertyToID("_HectonVolumetricFogVolumeRW");
            internal static readonly int VolumeTextureId = Shader.PropertyToID("_HectonVolumetricFogVolume");
            internal static readonly int FullSizeId = Shader.PropertyToID("_HectonVolumetricFogFullSize");
            internal static readonly int HalfSizeId = Shader.PropertyToID("_HectonVolumetricFogHalfSize");
            internal static readonly int CompositeParamsId = Shader.PropertyToID("_HectonVolumetricFogCompositeParams");
            internal static readonly int DebugParamsId = Shader.PropertyToID("_HectonVolumetricFogDebugParams");
            internal static readonly int InverseViewProjectionId = Shader.PropertyToID("_HectonVolumetricFogInverseViewProjection");
            internal static readonly int PointLightsBufferId = Shader.PropertyToID("_HectonVolumetricFogPointLights");
            internal static readonly int MarineSnowDensityTextureId = Shader.PropertyToID("_HectonMarineSnowFogDensityTex");
            internal static readonly int MarineSnowDensityTexelSizeId = Shader.PropertyToID("_HectonMarineSnowFogDensityTexelSize");
            internal static readonly int MarineSnowDensityParamsId = Shader.PropertyToID("_HectonMarineSnowFogDensityParams");
            internal static readonly int AbyssalFlowTextureId = Shader.PropertyToID("_AbyssalFlowFieldTexture");
            internal static readonly int AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
            internal static readonly int AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
            internal static readonly int AbyssalFlowTextureParamsId = Shader.PropertyToID("_AbyssalFlowTextureParams");
            internal static readonly int AbyssalFlowTextureActiveId = Shader.PropertyToID("_AbyssalFlowTextureActive");
            internal static readonly int BiomeTransitionFogColorId = Shader.PropertyToID("_H8BiomeTransitionFogColor");
            internal static readonly int BiomeTransitionAbsorptionId = Shader.PropertyToID("_H8BiomeTransitionAbsorption");
            internal static readonly int BiomeTransitionWeightsId = Shader.PropertyToID("_H8BiomeTransitionWeights");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private VolumetricFogPass _pass;
        private int _nextPerformanceWarningFrame;
        private int _nextColdStateRepairFrame;
        private bool _supportsComputeShaders;
        private bool _hotSwapRegistered;
        private bool _lateFrameRegistered;
        private bool _slowTickRegistered;

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            _pass?.PrepareExternalBridgeHandlesCold();
        }

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
            if (settings != null && settings.dearLieProxyShader == null)
                settings.dearLieProxyShader = AssetDatabase.LoadAssetAtPath<Shader>(DearLieProxyShaderAssetPath);
#endif

            _pass ??= new VolumetricFogPass();
            _nextColdStateRepairFrame = 0;
            CacheGraphicsCapabilitiesCold();
            _pass.PrepareComputeKernels(settings, settings != null ? settings.computeShader : null);
            _pass.TryPrepareNativeState(GlobalRegistry.DataVault, allowAllocation: true);
            _pass.TryPrepareGpuState(allowAllocation: true);
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            _pass.PrepareExternalBridgeHandlesCold();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null ||
                _pass == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (IsUnsupportedCameraType(cameraType))
                return;

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            RunDiagnosticMaintenanceIfDue(currentFrame);
            bool sampleSetupCost = currentFrame >= _nextPerformanceWarningFrame;
            long setupStartTimestamp = sampleSetupCost ? Stopwatch.GetTimestamp() : 0L;
            float qualityWeight = ResolveFiniteSaturated(HomeostasisBrain.GlobalQualityWeight);
            bool allowVolumetricCompute = settings.computeShader != null &&
                                          _supportsComputeShaders;
            if (!_pass.HasNativeState || !_pass.HasGpuState)
            {
                return;
            }

            double3 runtimeOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            bool forceProxyOnly = !allowVolumetricCompute;
            if (!_pass.Setup(
                    settings,
                    allowVolumetricCompute ? settings.computeShader : null,
                    qualityWeight,
                    runtimeOriginAup,
                    currentFrame,
                    forceProxyOnly))
            {
                if (!allowVolumetricCompute ||
                    !_pass.Setup(settings, null, qualityWeight, runtimeOriginAup, currentFrame, forceProxyOnly: true))
                {
                    return;
                }
            }

            renderer.EnqueuePass(_pass);
            PublishSetupWarningIfNeeded(setupStartTimestamp, currentFrame, sampleSetupCost);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _pass?.TryPrepareNativeState(currentService as IDataVault, allowAllocation: true);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            if (currentService != null)
            {
                TryRegisterSlowTickable();
                TryRegisterLateFrameTickable();
            }
        }

        public void SlowTick()
        {
            if (_pass == null)
                return;

            _pass.TryPrepareGpuState(allowAllocation: false);
        }

        public void LateFrameTick()
        {
            _pass?.CachePresentationGlobalsLate();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShaders = SystemInfo.supportsComputeShaders;
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

        private void TryRegisterSlowTickable()
        {
            if (_slowTickRegistered)
                return;

            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_slowTickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _slowTickRegistered = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_lateFrameRegistered)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _lateFrameRegistered = false;
        }

        private void RunDiagnosticMaintenanceIfDue(int currentFrame)
        {
            if (_pass == null)
                return;

            if (currentFrame < _nextColdStateRepairFrame)
                return;

            _nextColdStateRepairFrame = currentFrame + ColdStateRepairCadenceFrames;
            _pass.FlushDeferredDiagnosticDump();
        }

        private void PublishSetupWarningIfNeeded(long setupStartTimestamp, int currentFrame, bool sampleSetupCost)
        {
            if (!sampleSetupCost)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - setupStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            _nextPerformanceWarningFrame = currentFrame + 30;
            if (elapsedMilliseconds <= SetupBudgetWarningMilliseconds)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                SetupWarningHash,
                SetupContextHash,
                (float)elapsedMilliseconds);
        }
    }
}
