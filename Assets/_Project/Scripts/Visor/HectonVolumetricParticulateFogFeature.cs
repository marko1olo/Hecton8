using System;
using System.Diagnostics;
using System.IO;
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
    public sealed class HectonVolumetricParticulateFogFeature : ScriptableRendererFeature
    {
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VolumetricFog.compute";
        private const double SetupBudgetWarningMilliseconds = 0.2d;
        private const uint SetupWarningHash = 0xA88120F0u;
        private const uint SetupContextHash = 0xC0120F6Au;

        private static bool IsUnsupportedCameraType(CameraType cameraType)
        {
            return cameraType == CameraType.Preview ||
                   cameraType == CameraType.Reflection ||
                   cameraType == CameraType.SceneView;
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
            private const int ConstantBufferCount = 1;
            private const int DumpThresholdMicroseconds = 2000;
            private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_120.bin";

            private sealed class RaymarchPassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal uint threadGroupSizeX;
                internal uint threadGroupSizeY;
                internal TextureHandle depth;
                internal TextureHandle result;
                internal GraphicsBuffer paramsBuffer;
                internal BufferHandle pointLightBuffer;
                internal TextureHandle marineFogDensityTexture;
                internal TextureHandle abyssalFlowTexture;
                internal Vector4 fullSize;
                internal Vector4 halfSize;
                internal Vector4 compositeParams;
                internal Vector4 debugParams;
                internal Vector4 marineFogTexelSize;
                internal Vector4 marineFogParams;
                internal Vector4 abyssalFlowCenter;
                internal Vector4 abyssalFlowSpacing;
                internal Vector4 abyssalFlowTextureParams;
                internal float abyssalFlowTextureActive;
                internal Matrix4x4 inverseViewProjection;
            }

            private sealed class CompositePassData
            {
                internal ComputeShader computeShader;
                internal int kernelIndex;
                internal uint threadGroupSizeX;
                internal uint threadGroupSizeY;
                internal TextureHandle source;
                internal TextureHandle depth;
                internal TextureHandle halfInput;
                internal TextureHandle destination;
                internal GraphicsBuffer paramsBuffer;
                internal Vector4 fullSize;
                internal Vector4 halfSize;
                internal Vector4 compositeParams;
                internal Vector4 debugParams;
                internal Vector4 marineFogTexelSize;
                internal Vector4 marineFogParams;
                internal Vector4 abyssalFlowCenter;
                internal Vector4 abyssalFlowSpacing;
                internal Vector4 abyssalFlowTextureParams;
                internal float abyssalFlowTextureActive;
                internal Matrix4x4 inverseViewProjection;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Volumetric Particulate Fog");
            private FeatureSettings _settings;
            private ComputeShader _computeShader;
            private RTHandle _halfTexture;
            private RTHandle _compositeTexture;
            private RTHandle _marineFogDensityTextureHandle;
            private RTHandle _abyssalFlowTextureHandle;
            private GraphicsBuffer _paramsBuffer;
            private GraphicsBuffer _pointLightBufferA;
            private GraphicsBuffer _pointLightBufferB;
            private VolumetricFogParamsDTO _lastUploadedParams;
            private VolumetricFogParamsDTO _lastAuthoredParams;
            private VolumetricFogParamsDTO _externalOverrideParams;
            private uint _lastUploadedParamsHash;
            private uint _lastUploadedPointLightsHash;
            private Texture _marineFogDensityTextureHandleSource;
            private Texture _abyssalFlowTextureHandleSource;
            private IDataVault _vault;
            private VaultBufferHandle<VolumetricFogParamsDTO> _paramsHandle;
            private VaultBufferHandle<PointLightDTO> _pointLightsHandle;
            private VaultBufferHandle<VolumetricFogTelemetryEntry> _telemetryHandle;
            private VaultBufferHandle<WaterExtinctionProfileDTO> _extinctionProfilesHandle;
            private RenderTexture _emptyFogDensityTexture;
            private Texture3D _emptyAbyssalFlowTexture;
            private int _raymarchKernel = -1;
            private int _compositeKernel = -1;
            private uint _raymarchThreadGroupSizeX = 8;
            private uint _raymarchThreadGroupSizeY = 8;
            private uint _compositeThreadGroupSizeX = 8;
            private uint _compositeThreadGroupSizeY = 8;
            private float _qualityWeight;
            private int _telemetryWriteIndex;
            private int _activePointLightBufferIndex;
            private int _lastUploadedPointLightCount;
            private int _pendingPointLightCount;
            private int _lastScheduledPointLightCount;
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

            public VolumetricFogPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, ComputeShader computeShader, float qualityWeight)
            {
                _settings = settings;
                _computeShader = computeShader;
                _qualityWeight = ResolveFiniteSaturated(qualityWeight);
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;

                if (_computeShader != null && (_raymarchKernel < 0 || _compositeKernel < 0))
                {
                    _raymarchKernel = _computeShader.FindKernel("RaymarchVolumetricFog");
                    _compositeKernel = _computeShader.FindKernel("CompositeVolumetricFog");
                    _computeShader.GetKernelThreadGroupSizes(_raymarchKernel, out _raymarchThreadGroupSizeX, out _raymarchThreadGroupSizeY, out _);
                    _computeShader.GetKernelThreadGroupSizes(_compositeKernel, out _compositeThreadGroupSizeX, out _compositeThreadGroupSizeY, out _);
                }
            }

            public void Dispose()
            {
                _halfTexture?.Release();
                _compositeTexture?.Release();
                ReleaseExternalTextureHandle(ref _marineFogDensityTextureHandle, ref _marineFogDensityTextureHandleSource);
                ReleaseExternalTextureHandle(ref _abyssalFlowTextureHandle, ref _abyssalFlowTextureHandleSource);
                _paramsBuffer?.Release();
                if (_mockLightsJobPending)
                {
                    DispatcherJobFence.TryComplete(ref _mockLightsJobHandle, forceComplete: true); // COLD SYNC JOB: render-feature teardown cannot leave a vault writer running.
                    _mockLightsJobPending = false;
                }

                _pointLightBufferA?.Release();
                _pointLightBufferB?.Release();
                _halfTexture = null;
                _compositeTexture = null;
                _paramsBuffer = null;
                _pointLightBufferA = null;
                _pointLightBufferB = null;
                _paramsHandle = default;
                _pointLightsHandle = default;
                _telemetryHandle = default;
                _extinctionProfilesHandle = default;
                _vault = null;
                _activePointLightBufferIndex = 0;
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
                ReleaseFallbackTextures();
                _raymarchKernel = -1;
                _compositeKernel = -1;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _computeShader == null ||
                    _raymarchKernel < 0 ||
                    _compositeKernel < 0 ||
                    !VolumetricFogNativeLayout.Validate())
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
                int fullWidth = Mathf.Max(1, sourceDesc.width);
                int fullHeight = Mathf.Max(1, sourceDesc.height);
                float renderScale = _settings.ResolveInternalScale(_qualityWeight);
                int halfWidth = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.width * renderScale)));
                int halfHeight = QuantizeDimension(Mathf.Max(1, Mathf.RoundToInt(sourceDesc.height * renderScale)));
                EnsureRenderTargets(halfWidth, halfHeight, fullWidth, fullHeight);
                if (!EnsureGpuBuffers() || !EnsureVaultState())
                    return;

                Camera camera = cameraData.camera;
                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
                Matrix4x4 viewProjection = projectionMatrix * camera.worldToCameraMatrix;
                Matrix4x4 inverseViewProjection = viewProjection.inverse;
                int raySteps = _settings.ResolveRaySteps(_qualityWeight);
                int requestedPointLightCount = _settings.ResolvePointLightCount(_qualityWeight);
                float visualPhaseSeconds = ResolveVisualPhaseSeconds(_qualityWeight);
                float estimatedGpuMicroseconds = EstimateGpuMicroseconds(halfWidth, halfHeight, raySteps, requestedPointLightCount, renderScale);
                Vector4 marineFogTexelSize = Shader.GetGlobalVector(ShaderConstants.MarineSnowDensityTexelSizeId);
                Vector4 marineFogParams = Shader.GetGlobalVector(ShaderConstants.MarineSnowDensityParamsId);
                Texture marineFogTexture = Shader.GetGlobalTexture(ShaderConstants.MarineSnowDensityTextureId);
                Texture abyssalFlowTexture = Shader.GetGlobalTexture(ShaderConstants.AbyssalFlowTextureId);
                Vector4 abyssalFlowCenter = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowCenterId);
                Vector4 abyssalFlowSpacing = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowSpacingId);
                Vector4 abyssalFlowTextureParams = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowTextureParamsId);
                float abyssalFlowTextureActive = Shader.GetGlobalFloat(ShaderConstants.AbyssalFlowTextureActiveId);
                Vector4 biomeTransitionFogColor = Shader.GetGlobalVector(ShaderConstants.BiomeTransitionFogColorId);
                Vector4 biomeTransitionAbsorption = Shader.GetGlobalVector(ShaderConstants.BiomeTransitionAbsorptionId);
                Vector4 biomeTransitionWeights = Shader.GetGlobalVector(ShaderConstants.BiomeTransitionWeightsId);

                if (!UpdateVaultAndGpuState(
                        camera,
                        raySteps,
                        requestedPointLightCount,
                        renderScale,
                        visualPhaseSeconds,
                        estimatedGpuMicroseconds,
                        in biomeTransitionFogColor,
                        in biomeTransitionAbsorption,
                        in biomeTransitionWeights,
                        marineFogTexture != null,
                        abyssalFlowTexture != null && abyssalFlowTextureActive > 0.5f,
                        out int activePointLightCount,
                        out GraphicsBuffer activePointLightBuffer))
                {
                    return;
                }

                TextureHandle halfTexture = renderGraph.ImportTexture(_halfTexture);
                TextureHandle compositeTexture = renderGraph.ImportTexture(_compositeTexture);
                BufferHandle paramsBufferHandle = renderGraph.ImportBuffer(_paramsBuffer);
                BufferHandle pointLightBufferHandle = renderGraph.ImportBuffer(activePointLightBuffer);
                Vector4 fullSize = new Vector4(sourceDesc.width, sourceDesc.height, 1f / Mathf.Max(1, sourceDesc.width), 1f / Mathf.Max(1, sourceDesc.height));
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
                if (marineFogTexture == null || marineFogParams.w <= 0.5f)
                {
                    EnsureFallbackTextures();
                    marineFogTexture = _emptyFogDensityTexture;
                    marineFogParams = Vector4.zero;
                    marineFogTexelSize = new Vector4(1f, 1f, 1f, 1f);
                }

                if (abyssalFlowTexture == null)
                {
                    EnsureFallbackTextures();
                    abyssalFlowTexture = _emptyAbyssalFlowTexture;
                }

                RTHandle marineFogTextureHandle = ResolveExternalTextureHandle(marineFogTexture, ref _marineFogDensityTextureHandle, ref _marineFogDensityTextureHandleSource);
                RTHandle abyssalFlowTextureHandle = ResolveExternalTextureHandle(abyssalFlowTexture, ref _abyssalFlowTextureHandle, ref _abyssalFlowTextureHandleSource);
                if (marineFogTextureHandle == null || abyssalFlowTextureHandle == null)
                    return;

                TextureHandle marineFogGraphTexture = renderGraph.ImportTexture(marineFogTextureHandle);
                TextureHandle abyssalFlowGraphTexture = renderGraph.ImportTexture(abyssalFlowTextureHandle);

                using (var builder = renderGraph.AddComputePass("Hecton Particulate Fog Raymarch", out RaymarchPassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _raymarchKernel;
                    passData.threadGroupSizeX = _raymarchThreadGroupSizeX;
                    passData.threadGroupSizeY = _raymarchThreadGroupSizeY;
                    passData.depth = depthTexture;
                    passData.result = halfTexture;
                    passData.paramsBuffer = _paramsBuffer;
                    passData.pointLightBuffer = pointLightBufferHandle;
                    passData.marineFogDensityTexture = marineFogGraphTexture;
                    passData.abyssalFlowTexture = abyssalFlowGraphTexture;
                    passData.fullSize = fullSize;
                    passData.halfSize = halfSize;
                    passData.compositeParams = compositeParams;
                    passData.debugParams = debugParams;
                    passData.marineFogTexelSize = marineFogTexelSize;
                    passData.marineFogParams = marineFogParams;
                    passData.abyssalFlowCenter = abyssalFlowCenter;
                    passData.abyssalFlowSpacing = abyssalFlowSpacing;
                    passData.abyssalFlowTextureParams = abyssalFlowTextureParams;
                    passData.abyssalFlowTextureActive = abyssalFlowTextureActive;
                    passData.inverseViewProjection = inverseViewProjection;

                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(halfTexture, AccessFlags.Write);
                    builder.UseTexture(marineFogGraphTexture, AccessFlags.Read);
                    builder.UseTexture(abyssalFlowGraphTexture, AccessFlags.Read);
                    builder.UseBuffer(paramsBufferHandle, AccessFlags.Read);
                    builder.UseBuffer(pointLightBufferHandle, AccessFlags.Read);

                    builder.SetRenderFunc((RaymarchPassData data, ComputeGraphContext context) =>
                    {
                        int dispatchX = Mathf.CeilToInt(data.halfSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                        int dispatchY = Mathf.CeilToInt(data.halfSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.HalfResultId, data.result);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.MarineSnowDensityTextureId, data.marineFogDensityTexture);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.AbyssalFlowTextureId, data.abyssalFlowTexture);
                        context.cmd.SetComputeConstantBufferParam(data.computeShader, ShaderConstants.ParamsBufferId, data.paramsBuffer, 0, VolumetricFogConstants.ParamsStrideBytes);
                        context.cmd.SetComputeBufferParam(data.computeShader, data.kernelIndex, ShaderConstants.PointLightsBufferId, data.pointLightBuffer);
                        SetFrameParams(data.computeShader, data.kernelIndex, context.cmd, in data.fullSize, in data.halfSize, in data.compositeParams, in data.debugParams, in data.marineFogTexelSize, in data.marineFogParams, in data.abyssalFlowCenter, in data.abyssalFlowSpacing, in data.abyssalFlowTextureParams, data.abyssalFlowTextureActive, in data.inverseViewProjection);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                    });
                }

                using (var builder = renderGraph.AddComputePass("Hecton Particulate Fog Composite", out CompositePassData passData, _profilingSampler))
                {
                    passData.computeShader = _computeShader;
                    passData.kernelIndex = _compositeKernel;
                    passData.threadGroupSizeX = _compositeThreadGroupSizeX;
                    passData.threadGroupSizeY = _compositeThreadGroupSizeY;
                    passData.source = sourceTexture;
                    passData.depth = depthTexture;
                    passData.halfInput = halfTexture;
                    passData.destination = compositeTexture;
                    passData.paramsBuffer = _paramsBuffer;
                    passData.fullSize = fullSize;
                    passData.halfSize = halfSize;
                    passData.compositeParams = compositeParams;
                    passData.debugParams = debugParams;
                    passData.marineFogTexelSize = marineFogTexelSize;
                    passData.marineFogParams = marineFogParams;
                    passData.abyssalFlowCenter = abyssalFlowCenter;
                    passData.abyssalFlowSpacing = abyssalFlowSpacing;
                    passData.abyssalFlowTextureParams = abyssalFlowTextureParams;
                    passData.abyssalFlowTextureActive = abyssalFlowTextureActive;
                    passData.inverseViewProjection = inverseViewProjection;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(halfTexture, AccessFlags.Read);
                    builder.UseTexture(compositeTexture, AccessFlags.Write);
                    builder.UseBuffer(paramsBufferHandle, AccessFlags.Read);

                    builder.SetRenderFunc((CompositePassData data, ComputeGraphContext context) =>
                    {
                        int dispatchX = Mathf.CeilToInt(data.fullSize.x / Mathf.Max(1u, data.threadGroupSizeX));
                        int dispatchY = Mathf.CeilToInt(data.fullSize.y / Mathf.Max(1u, data.threadGroupSizeY));
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceColorId, data.source);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.SourceDepthId, data.depth);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.HalfInputId, data.halfInput);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, ShaderConstants.CompositeResultId, data.destination);
                        context.cmd.SetComputeConstantBufferParam(data.computeShader, ShaderConstants.ParamsBufferId, data.paramsBuffer, 0, VolumetricFogConstants.ParamsStrideBytes);
                        SetFrameParams(data.computeShader, data.kernelIndex, context.cmd, in data.fullSize, in data.halfSize, in data.compositeParams, in data.debugParams, in data.marineFogTexelSize, in data.marineFogParams, in data.abyssalFlowCenter, in data.abyssalFlowSpacing, in data.abyssalFlowTextureParams, data.abyssalFlowTextureActive, in data.inverseViewProjection);
                        context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, dispatchX, dispatchY, 1);
                    });
                }

                resourceData.cameraColor = compositeTexture;
            }

            private static void SetFrameParams(
                ComputeShader computeShader,
                int kernelIndex,
                CommandBuffer commandBuffer,
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
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.FullSizeId, fullSize);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.HalfSizeId, halfSize);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.CompositeParamsId, compositeParams);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.DebugParamsId, debugParams);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.MarineSnowDensityTexelSizeId, marineFogTexelSize);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.MarineSnowDensityParamsId, marineFogParams);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.AbyssalFlowCenterId, abyssalFlowCenter);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.AbyssalFlowSpacingId, abyssalFlowSpacing);
                commandBuffer.SetComputeVectorParam(computeShader, ShaderConstants.AbyssalFlowTextureParamsId, abyssalFlowTextureParams);
                commandBuffer.SetComputeFloatParam(computeShader, ShaderConstants.AbyssalFlowTextureActiveId, abyssalFlowTextureActive);
                commandBuffer.SetComputeMatrixParam(computeShader, ShaderConstants.InverseViewProjectionId, inverseViewProjection);
            }

            private bool EnsureVaultState()
            {
                IDataVault vault = _vault;
                if (vault == null || vault.IsCompactionFenceActive)
                {
                    vault = GlobalRegistry.DataVault;
                    if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                        vault = latestVault;
                }

                if (vault == null || vault.IsCompactionFenceActive)
                    return false;

                if (!ReferenceEquals(vault, _vault))
                {
                    if (_mockLightsJobPending)
                    {
                        if (!_mockLightsJobHandle.IsCompleted)
                            return false;

                        DispatcherJobFence.TryFinalizeCompleted(ref _mockLightsJobHandle);
                        _mockLightsJobPending = false;
                    }

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

                if (!_paramsHandle.IsCreated)
                    _paramsHandle = vault.GetBufferHandle<VolumetricFogParamsDTO>(BufferID.ShinobuVolumetricFogParams, 1, SystemID.Vfx, NativeArrayOptions.UninitializedMemory);
                if (!_pointLightsHandle.IsCreated)
                    _pointLightsHandle = vault.GetBufferHandle<PointLightDTO>(BufferID.ShinobuVolumetricFogPointLights, VolumetricFogConstants.MaxPointLights, SystemID.Vfx, NativeArrayOptions.UninitializedMemory);
                if (!_telemetryHandle.IsCreated)
                    _telemetryHandle = vault.GetBufferHandle<VolumetricFogTelemetryEntry>(BufferID.ShinobuVolumetricFogTelemetryRing, VolumetricFogConstants.TelemetryCapacity, SystemID.Vfx, NativeArrayOptions.UninitializedMemory);
                if (!_extinctionProfilesHandle.IsCreated)
                    _extinctionProfilesHandle = vault.GetBufferHandle<WaterExtinctionProfileDTO>(BufferID.ShinobuVolumetricFogExtinctionProfiles, VolumetricFogConstants.ExtinctionProfileCapacity, SystemID.Vfx, NativeArrayOptions.UninitializedMemory);

                if (!_paramsHandle.IsCreated || !_pointLightsHandle.IsCreated || !_telemetryHandle.IsCreated || !_extinctionProfilesHandle.IsCreated)
                    return false;

                SeedDefaultExtinctionProfiles();
                return true;
            }

            private void SeedDefaultExtinctionProfiles()
            {
                NativeArray<WaterExtinctionProfileDTO> profiles = _extinctionProfilesHandle.Resolve(_vault);
                if (_extinctionProfilesSeeded || !profiles.IsCreated || profiles.Length <= 0)
                    return;

                profiles[0] = VolumetricFogParamsAccess.CreateDefaultExtinctionProfile();
                for (int i = 1; i < profiles.Length; i++)
                    profiles[i] = default;
                _extinctionProfilesSeeded = true;
            }

            private bool UpdateVaultAndGpuState(
                Camera camera,
                int raySteps,
                int pointLightCount,
                float renderScale,
                float visualPhaseSeconds,
                float estimatedGpuMicroseconds,
                in Vector4 biomeTransitionFogColor,
                in Vector4 biomeTransitionAbsorption,
                in Vector4 biomeTransitionWeights,
                bool hasMarineFogTexture,
                bool hasAbyssalFlowTexture,
                out int activePointLightCount,
                out GraphicsBuffer activePointLightBuffer)
            {
                activePointLightCount = 0;
                activePointLightBuffer = GetActivePointLightBuffer();
                NativeArray<VolumetricFogParamsDTO> fogParams = _paramsHandle.Resolve(_vault);
                NativeArray<PointLightDTO> pointLights = _pointLightsHandle.Resolve(_vault);
                NativeArray<VolumetricFogTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                if (!fogParams.IsCreated ||
                    fogParams.Length <= 0 ||
                    !pointLights.IsCreated ||
                    pointLights.Length < VolumetricFogConstants.MaxPointLights ||
                    activePointLightBuffer == null ||
                    !activePointLightBuffer.IsValid())
                {
                    return false;
                }

                RefreshCompletedLightJobAndUpload(pointLights);

                Color linearColor = _settings.fogColor.linear;
                float3 settingsColor = new float3(
                    ResolveFiniteClamped(linearColor.r, 0.0015f, 8f, 0.015f),
                    ResolveFiniteClamped(linearColor.g, 0.0023f, 8f, 0.045f),
                    ResolveFiniteClamped(linearColor.b, 0.0031f, 8f, 0.065f));
                Color color = new Color(settingsColor.x, settingsColor.y, settingsColor.z, 1f);
                float baseDensity = ResolveFiniteClamped(_settings.baseDensity, 0f, 0.3f, 0.045f);
                float extinctionCoefficient = ResolveFiniteClamped(_settings.extinctionCoefficient, 0.0001f, 2f, 0.12f);
                float3 cameraPosition = ResolveCameraAupLocalPosition(camera);
                float3 cameraForward = ResolveCameraForward(camera);
                float3 wrappedNoiseOffset = ResolveWrappedNoiseOffset(cameraPosition);
                ref VolumetricFogParamsDTO fogState = ref VolumetricFogParamsAccess.ElementAt(fogParams, 0);
                VolumetricFogParamsDTO existing = fogState;
                UpdateExternalOverrideState(in existing);
                bool useVaultOverride = _hasExternalOverrideParams;
                VolumetricFogParamsDTO overrideParams = _externalOverrideParams;
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
                VolumetricFogParamsDTO dto = new VolumetricFogParamsDTO
                {
                    FogColorAndDensity = fogColorAndDensity,
                    ScatteringParams = scatteringParams,
                    FlowAdvection = new float4(wrappedNoiseOffset, flowStrength),
                    QualityAndLimits = new float4(
                        _qualityWeight,
                        raySteps,
                        ResolveFiniteClamped(_settings.maxRayDistanceMeters, 0.25f, 140f, 70f),
                        _settings.ResolveProxyBlend(_qualityWeight))
                };
                fogState = dto;
                _lastAuthoredParams = dto;
                _hasAuthoredParams = true;

                UploadConstantBufferIfDirty(in dto);
                ScheduleMockLightsIfIdle(pointLights, cameraPosition, cameraForward, pointLightCount, visualPhaseSeconds);
                activePointLightBuffer = GetActivePointLightBuffer();
                activePointLightCount = _lastUploadedPointLightCount;
                if (telemetry.IsCreated && telemetry.Length >= VolumetricFogConstants.TelemetryCapacity)
                    RecordTelemetry(telemetry, in dto, cameraPosition, raySteps, renderScale, estimatedGpuMicroseconds, activePointLightCount, hasMarineFogTexture, hasAbyssalFlowTexture);
                return true;
            }

            private void UpdateExternalOverrideState(in VolumetricFogParamsDTO existing)
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

            private static bool IsUsableVaultOverride(in VolumetricFogParamsDTO dto)
            {
                return VolumetricFogParamsAccess.IsUsableParams(in dto);
            }

            private static float3 ResolveCameraAupLocalPosition(Camera camera)
            {
                if (camera == null)
                    return float3.zero;

                Vector3 runtimePosition = camera.transform.position;
                float3 runtime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                if (!math.all(math.isfinite(runtime)))
                    return float3.zero;

                double3 committedOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                double3 cameraAup = new double3(runtime.x, runtime.y, runtime.z) + committedOffset;
                double3 local = cameraAup - committedOffset;
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
                NativeArray<WaterExtinctionProfileDTO> profiles = _extinctionProfilesHandle.Resolve(_vault);
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

            private void UploadConstantBufferIfDirty(in VolumetricFogParamsDTO dto)
            {
                uint dtoHash = HashParams(in dto);
                if (_hasUploadedParams &&
                    dtoHash == _lastUploadedParamsHash &&
                    AreParamsEqual(in dto, in _lastUploadedParams))
                {
                    return;
                }

                NativeArray<VolumetricFogParamsDTO> mapped = _paramsBuffer.LockBufferForWrite<VolumetricFogParamsDTO>(0, ConstantBufferCount);
                try
                {
                    mapped[0] = dto;
                }
                finally
                {
                    _paramsBuffer.UnlockBufferAfterWrite<VolumetricFogParamsDTO>(ConstantBufferCount);
                }

                _lastUploadedParams = dto;
                _lastUploadedParamsHash = dtoHash;
                _hasUploadedParams = true;
            }

            private static uint HashParams(in VolumetricFogParamsDTO dto)
            {
                uint4 hashLane = new uint4(
                    math.hash(math.asuint(dto.FogColorAndDensity)),
                    math.hash(math.asuint(dto.ScatteringParams)),
                    math.hash(math.asuint(dto.FlowAdvection)),
                    math.hash(math.asuint(dto.QualityAndLimits)));
                return math.hash(hashLane);
            }

            private static bool AreParamsEqual(in VolumetricFogParamsDTO left, in VolumetricFogParamsDTO right)
            {
                return math.all(left.FogColorAndDensity == right.FogColorAndDensity) &&
                       math.all(left.ScatteringParams == right.ScatteringParams) &&
                       math.all(left.FlowAdvection == right.FlowAdvection) &&
                       math.all(left.QualityAndLimits == right.QualityAndLimits);
            }

            private void ScheduleMockLightsIfIdle(
                NativeArray<PointLightDTO> pointLights,
                float3 cameraPosition,
                float3 cameraForward,
                int desiredPointLightCount,
                float visualPhaseSeconds)
            {
                if (_mockLightsJobPending || !pointLights.IsCreated)
                    return;

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
                    return;
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
                _mockLightsJobHandle = lightJob.Schedule();
                _mockLightsJobPending = true;
                _lastScheduledPointLightCount = safeDesiredPointLightCount;
                _lastScheduledPointLightHash = scheduleHash;
                _hasScheduledPointLightJob = true;
            }

            private void RefreshCompletedLightJobAndUpload(NativeArray<PointLightDTO> pointLights)
            {
                if (!_mockLightsJobPending || !_mockLightsJobHandle.IsCompleted)
                    return;

                if (!DispatcherJobFence.TryFinalizeCompleted(ref _mockLightsJobHandle))
                    return;

                _mockLightsJobPending = false;

                GraphicsBuffer target = GetInactivePointLightBuffer();
                if (target == null || !target.IsValid())
                    return;

                int completedPointLightCount = math.clamp(_pendingPointLightCount, 0, VolumetricFogConstants.MaxPointLights);
                uint pointLightsHash = HashPointLights(pointLights, completedPointLightCount);
                if (_hasUploadedPointLights &&
                    completedPointLightCount == _lastUploadedPointLightCount &&
                    pointLightsHash == _lastUploadedPointLightsHash)
                {
                    _pendingPointLightCount = 0;
                    return;
                }

                UploadPointLights(target, pointLights);
                _activePointLightBufferIndex = 1 - _activePointLightBufferIndex;
                _lastUploadedPointLightCount = completedPointLightCount;
                _lastUploadedPointLightsHash = pointLightsHash;
                _hasUploadedPointLights = true;
                _pendingPointLightCount = 0;
            }

            private static uint HashPointLights(NativeArray<PointLightDTO> pointLights, int count)
            {
                if (!pointLights.IsCreated)
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

            private unsafe void UploadPointLights(GraphicsBuffer target, NativeArray<PointLightDTO> pointLights)
            {
                int count = math.min(target.count, pointLights.Length);
                if (count <= 0)
                    return;

                NativeArray<PointLightDTO> mapped = target.LockBufferForWrite<PointLightDTO>(0, count);
                try
                {
                    void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(pointLights);
                    UnsafeUtility.MemCpy(destination, source, count * UnsafeUtility.SizeOf<PointLightDTO>());
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
                in VolumetricFogParamsDTO dto,
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
                    FrameIndex = unchecked((uint)Time.frameCount),
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
                    DumpTelemetryRing(telemetry);
                    _dumpedThisSession = true;
                }
            }

            private unsafe void DumpTelemetryRing(NativeArray<VolumetricFogTelemetryEntry> telemetry)
            {
                if (!telemetry.IsCreated || telemetry.Length <= 0)
                    return;

                try
                {
                    string path = Path.Combine(ResolveProjectRoot(), DumpRelativePath);
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    int byteLength = telemetry.Length * UnsafeUtility.SizeOf<VolumetricFogTelemetryEntry>();
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    ReadOnlySpan<byte> dumpBytes = new ReadOnlySpan<byte>(source, byteLength);
                    using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                        stream.Write(dumpBytes);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
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

            private static float ResolveVisualPhaseSeconds(float qualityWeight)
            {
                float curved = ResolveQualityCurve(qualityWeight);
                float updateHz = math.lerp(5f, 60f, curved);
                int cadenceFrames = math.clamp((int)math.round(60f / math.max(5f, updateHz)), 1, 12);
                uint frame = unchecked((uint)Time.frameCount);
                uint cadence = (uint)cadenceFrames;
                uint quantizedFrame = frame - frame % cadence;
                return quantizedFrame * (1f / 60f);
            }

            private static float EstimateGpuMicroseconds(int width, int height, int raySteps, int pointLightCount, float renderScale)
            {
                float pixels = math.max(1, width) * math.max(1, height);
                float lightMultiplier = 1f + math.max(0, pointLightCount) * 0.075f;
                float scalePenalty = math.lerp(0.85f, 1.25f, ResolveFiniteSaturated(renderScale));
                return pixels * math.max(1, raySteps) * lightMultiplier * scalePenalty * 0.000018f;
            }

            private bool EnsureGpuBuffers()
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                    return false;

                if (_paramsBuffer == null || !_paramsBuffer.IsValid())
                {
                    _paramsBuffer?.Release();
                    _paramsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        ConstantBufferCount,
                        VolumetricFogConstants.ParamsStrideBytes); // COLD ALLOC: GraphicsBuffer[64B] - SHINOBU_120 volumetric fog params.
                    _hasUploadedParams = false;
                    _lastUploadedParams = default;
                    _lastUploadedParamsHash = 0u;
                    ResetAuthoredOverrideState();
                }

                bool createdPointLightBuffer = false;
                if (_pointLightBufferA == null || !_pointLightBufferA.IsValid())
                {
                    _pointLightBufferA?.Release();
                    _pointLightBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        VolumetricFogConstants.MaxPointLights,
                        VolumetricFogConstants.PointLightStrideBytes); // COLD ALLOC: GraphicsBuffer[PointLightDTO x8] - SHINOBU_120 fog lights buffer A.
                    createdPointLightBuffer = true;
                }

                if (_pointLightBufferB == null || !_pointLightBufferB.IsValid())
                {
                    _pointLightBufferB?.Release();
                    _pointLightBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        VolumetricFogConstants.MaxPointLights,
                        VolumetricFogConstants.PointLightStrideBytes); // COLD ALLOC: GraphicsBuffer[PointLightDTO x8] - SHINOBU_120 fog lights buffer B.
                    createdPointLightBuffer = true;
                }

                if (createdPointLightBuffer)
                {
                    ClearPointLightBuffer(_pointLightBufferA);
                    ClearPointLightBuffer(_pointLightBufferB);
                    _activePointLightBufferIndex = 0;
                    ResetPointLightScheduleState();
                }

                return _paramsBuffer != null && _paramsBuffer.IsValid() &&
                       _pointLightBufferA != null && _pointLightBufferA.IsValid() &&
                       _pointLightBufferB != null && _pointLightBufferB.IsValid();
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

            private void EnsureRenderTargets(int halfWidth, int halfHeight, int fullWidth, int fullHeight)
            {
                if ((_halfTexture == null || _halfTexture.rt == null || _halfTexture.rt.width != halfWidth || _halfTexture.rt.height != halfHeight) ||
                    (_compositeTexture == null || _compositeTexture.rt == null || _compositeTexture.rt.width != fullWidth || _compositeTexture.rt.height != fullHeight))
                {
                    _halfTexture?.Release();
                    _compositeTexture?.Release();

                    _halfTexture = RTHandles.Alloc(
                        halfWidth,
                        halfHeight,
                        1,
                        DepthBits.None,
                        GraphicsFormat.R16G16B16A16_SFloat,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        TextureDimension.Tex2D,
                        true,
                        name: "_HectonVolumetricFogHalf"); // COLD ALLOC: persistent reduced-resolution volumetric fog target.

                    _compositeTexture = RTHandles.Alloc(
                        fullWidth,
                        fullHeight,
                        1,
                        DepthBits.None,
                        GraphicsFormat.R16G16B16A16_SFloat,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        TextureDimension.Tex2D,
                        true,
                        name: "_HectonVolumetricFogComposite"); // COLD ALLOC: persistent full-resolution fog composite target.
                }
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

            private static RTHandle ResolveExternalTextureHandle(Texture texture, ref RTHandle handle, ref Texture handleSource)
            {
                if (texture == null)
                    return null;

                if (!ReferenceEquals(texture, handleSource))
                {
                    handle?.Release();
                    handleSource = texture;
                    handle = RTHandles.Alloc(texture);
                }

                return handle;
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

            private static int QuantizeDimension(int dimension)
            {
                int safeDimension = Mathf.Max(1, dimension);
                return ((safeDimension + RenderTextureBucketSize - 1) / RenderTextureBucketSize) * RenderTextureBucketSize;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int ParamsBufferId = Shader.PropertyToID("HectonVolumetricFogParams");
            internal static readonly int SourceColorId = Shader.PropertyToID("_HectonVolumetricFogSourceColor");
            internal static readonly int SourceDepthId = Shader.PropertyToID("_HectonVolumetricFogSourceDepth");
            internal static readonly int HalfInputId = Shader.PropertyToID("_HectonVolumetricFogHalfInput");
            internal static readonly int HalfResultId = Shader.PropertyToID("_HectonVolumetricFogHalfResult");
            internal static readonly int CompositeResultId = Shader.PropertyToID("_HectonVolumetricFogCompositeResult");
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

        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.computeShader == null)
                settings.computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
#endif

            _pass ??= new VolumetricFogPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.computeShader == null || _pass == null || !SystemInfo.supportsComputeShaders)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (IsUnsupportedCameraType(cameraType))
                return;

            int currentFrame = Time.frameCount;
            bool sampleSetupCost = currentFrame >= _nextPerformanceWarningFrame;
            long setupStartTimestamp = sampleSetupCost ? Stopwatch.GetTimestamp() : 0L;
            float qualityWeight = ResolveFiniteSaturated(HomeostasisBrain.GlobalQualityWeight);
            _pass.Setup(settings, settings.computeShader, qualityWeight);
            renderer.EnqueuePass(_pass);
            PublishSetupWarningIfNeeded(setupStartTimestamp, currentFrame, sampleSetupCost);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
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
