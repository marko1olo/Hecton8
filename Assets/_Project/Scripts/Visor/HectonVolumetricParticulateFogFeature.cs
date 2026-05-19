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
                float curved = Mathf.Clamp01(quality);
                curved = curved * curved * (3f - 2f * curved);
                return Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Lerp(VolumetricFogConstants.MinRaySteps, VolumetricFogConstants.MaxRaySteps, curved)),
                    VolumetricFogConstants.MinRaySteps,
                    VolumetricFogConstants.MaxRaySteps);
            }

            internal float ResolveInternalScale(float quality)
            {
                float clampedQuality = Mathf.Clamp01(quality);
                float scale = Mathf.Lerp(
                    Mathf.Clamp(minimumInternalScale, 0.2f, 0.5f),
                    Mathf.Clamp(maximumInternalScale, 0.5f, 0.85f),
                    clampedQuality);
                return Mathf.Clamp(scale, 0.2f, 0.85f);
            }

            internal float ResolveProxyBlend(float quality)
            {
                float q = Mathf.Clamp01(quality);
                float t = Mathf.Clamp01((q - 0.12f) * (1f / 0.3f));
                float fade = t * t * (3f - 2f * t);
                return 1f - fade;
            }

            internal int ResolvePointLightCount(float quality)
            {
                return Mathf.Clamp(
                    1 + Mathf.FloorToInt(Mathf.Clamp01(quality) * (VolumetricFogConstants.MaxPointLights - 1) + 0.0001f),
                    1,
                    VolumetricFogConstants.MaxPointLights);
            }
        }

        private sealed class VolumetricFogPass : ScriptableRenderPass, IDisposable
        {
            private const int RenderTextureBucketSize = 64;
            private const int ConstantBufferCount = 1;
            private const int DumpThresholdMicroseconds = 2000;
            private const string DumpRelativePath = "Docs/AgentLogs/Dump_VOLUMETRIC_SURGEON.bin";

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
            private JobHandle _mockLightsJobHandle;
            private bool _mockLightsJobPending;
            private bool _dumpedThisSession;
            private bool _extinctionProfilesSeeded;

            public VolumetricFogPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, ComputeShader computeShader, float qualityWeight)
            {
                _settings = settings;
                _computeShader = computeShader;
                _qualityWeight = Mathf.Clamp01(qualityWeight);
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
                    _mockLightsJobHandle.Complete(); // COLD SYNC JOB: render-feature teardown cannot leave a vault writer running.
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
                _extinctionProfilesSeeded = false;
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
                int fullWidth = QuantizeDimension(Mathf.Max(1, sourceDesc.width));
                int fullHeight = QuantizeDimension(Mathf.Max(1, sourceDesc.height));
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

                if (!UpdateVaultAndGpuState(
                        camera,
                        raySteps,
                        requestedPointLightCount,
                        renderScale,
                        visualPhaseSeconds,
                        estimatedGpuMicroseconds,
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
                    Mathf.Max(0.01f, _settings.bilateralDepthScale),
                    Mathf.Max(0f, _settings.siltDensityStrength),
                    activePointLightCount,
                    visualPhaseSeconds);
                Vector4 debugParams = new Vector4(
                    Mathf.Clamp01(_settings.debugHeatmapWeight),
                    Mathf.Clamp01(_settings.ditherStrength),
                    renderScale,
                    estimatedGpuMicroseconds);
                Vector4 marineFogTexelSize = Shader.GetGlobalVector(ShaderConstants.MarineSnowDensityTexelSizeId);
                Vector4 marineFogParams = Shader.GetGlobalVector(ShaderConstants.MarineSnowDensityParamsId);
                Texture marineFogTexture = Shader.GetGlobalTexture(ShaderConstants.MarineSnowDensityTextureId);
                if (marineFogTexture == null || marineFogParams.w <= 0.5f)
                {
                    EnsureFallbackTextures();
                    marineFogTexture = _emptyFogDensityTexture;
                    marineFogParams = Vector4.zero;
                    marineFogTexelSize = new Vector4(1f, 1f, 1f, 1f);
                }

                Texture abyssalFlowTexture = Shader.GetGlobalTexture(ShaderConstants.AbyssalFlowTextureId);
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

                Vector4 abyssalFlowCenter = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowCenterId);
                Vector4 abyssalFlowSpacing = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowSpacingId);
                Vector4 abyssalFlowTextureParams = Shader.GetGlobalVector(ShaderConstants.AbyssalFlowTextureParamsId);
                float abyssalFlowTextureActive = Shader.GetGlobalFloat(ShaderConstants.AbyssalFlowTextureActiveId);

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
                    _vault = vault;
                    _paramsHandle = default;
                    _pointLightsHandle = default;
                    _telemetryHandle = default;
                    _extinctionProfilesHandle = default;
                    _extinctionProfilesSeeded = false;
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

                Color color = _settings.fogColor.linear;
                float baseDensity = Mathf.Max(0f, _settings.baseDensity);
                float extinctionCoefficient = Mathf.Max(0.0001f, _settings.extinctionCoefficient);
                float3 cameraPosition = camera != null ? (float3)camera.transform.position : float3.zero;
                float3 cameraForward = camera != null ? (float3)camera.transform.forward : new float3(0f, 0f, 1f);
                ApplyExtinctionProfileFromVault(ref color, ref baseDensity, ref extinctionCoefficient, cameraPosition);
                float3 wrappedNoiseOffset = ResolveWrappedNoiseOffset(cameraPosition);
                ref VolumetricFogParamsDTO fogState = ref VolumetricFogParamsAccess.ElementAt(fogParams, 0);
                VolumetricFogParamsDTO existing = fogState;
                bool useVaultOverride = IsUsableVaultOverride(in existing);
                float4 fogColorAndDensity = useVaultOverride
                    ? new float4(
                        math.max(existing.FogColorAndDensity.x, 0f),
                        math.max(existing.FogColorAndDensity.y, 0f),
                        math.max(existing.FogColorAndDensity.z, 0f),
                        math.clamp(existing.FogColorAndDensity.w, 0f, 0.3f))
                    : new float4(color.r, color.g, color.b, baseDensity);
                float4 scatteringParams = useVaultOverride
                    ? new float4(
                        math.clamp(existing.ScatteringParams.x, 0f, 4f),
                        math.clamp(existing.ScatteringParams.y, 0.0001f, 2f),
                        math.clamp(existing.ScatteringParams.z, -0.95f, 0.95f),
                        math.clamp(existing.ScatteringParams.w, 0.25f, 0.995f))
                    : new float4(
                        Mathf.Max(0f, _settings.scatteringCoefficient),
                        extinctionCoefficient,
                        Mathf.Clamp(_settings.anisotropy, -0.95f, 0.95f),
                        Mathf.Clamp(_settings.opacityEarlyBreak, 0.25f, 0.995f));
                float flowStrength = useVaultOverride
                    ? math.clamp(existing.FlowAdvection.w, 0f, 8f)
                    : Mathf.Max(0f, _settings.flowAdvectionStrength);
                VolumetricFogParamsDTO dto = new VolumetricFogParamsDTO
                {
                    FogColorAndDensity = fogColorAndDensity,
                    ScatteringParams = scatteringParams,
                    FlowAdvection = new float4(wrappedNoiseOffset, flowStrength),
                    QualityAndLimits = new float4(
                        _qualityWeight,
                        raySteps,
                        Mathf.Max(0.25f, _settings.maxRayDistanceMeters),
                        _settings.ResolveProxyBlend(_qualityWeight))
                };
                fogState = dto;

                UploadConstantBuffer(in dto);
                ScheduleMockLightsIfIdle(pointLights, cameraPosition, cameraForward, pointLightCount, visualPhaseSeconds);
                activePointLightBuffer = GetActivePointLightBuffer();
                activePointLightCount = _lastUploadedPointLightCount;
                if (telemetry.IsCreated && telemetry.Length >= VolumetricFogConstants.TelemetryCapacity)
                    RecordTelemetry(telemetry, in dto, cameraPosition, raySteps, renderScale, estimatedGpuMicroseconds, activePointLightCount);
                return true;
            }

            private static bool IsUsableVaultOverride(in VolumetricFogParamsDTO dto)
            {
                return IsFinite(dto.FogColorAndDensity.w) &&
                       IsFinite(dto.ScatteringParams.x) &&
                       IsFinite(dto.ScatteringParams.y) &&
                       IsFinite(dto.ScatteringParams.z) &&
                       IsFinite(dto.ScatteringParams.w) &&
                       IsFinite(dto.FlowAdvection.w) &&
                       dto.QualityAndLimits.y >= VolumetricFogConstants.MinRaySteps &&
                       dto.QualityAndLimits.y <= VolumetricFogConstants.MaxRaySteps &&
                       dto.ScatteringParams.y > 0f;
            }

            private void ApplyExtinctionProfileFromVault(ref Color fogColor, ref float baseDensity, ref float extinctionCoefficient, float3 cameraPosition)
            {
                NativeArray<WaterExtinctionProfileDTO> profiles = _extinctionProfilesHandle.Resolve(_vault);
                if (!profiles.IsCreated || profiles.Length <= 0)
                    return;

                float cameraDepthMeters = Mathf.Max(0f, -cameraPosition.y);
                for (int i = 0; i < profiles.Length; i++)
                {
                    WaterExtinctionProfileDTO profile = profiles[i];
                    if (profile.ProfileHash == 0u ||
                        cameraDepthMeters < profile.MinDepthMeters ||
                        cameraDepthMeters > profile.MaxDepthMeters)
                    {
                        continue;
                    }

                    float densityMultiplier = Mathf.Clamp(profile.DensityMultiplier, 0f, 8f);
                    float3 absorption = math.max(profile.AbsorptionAndScatter.xyz, float3.zero);
                    float scatter = math.max(0f, profile.AbsorptionAndScatter.w);
                    fogColor = new Color(
                        Mathf.Lerp(fogColor.r, absorption.x, 0.35f),
                        Mathf.Lerp(fogColor.g, absorption.y, 0.35f),
                        Mathf.Lerp(fogColor.b, absorption.z, 0.35f),
                        fogColor.a);
                    baseDensity = Mathf.Max(0f, baseDensity * Mathf.Max(0.001f, densityMultiplier));
                    extinctionCoefficient = Mathf.Lerp(extinctionCoefficient, Mathf.Max(0.0001f, scatter), 0.5f);
                    return;
                }
            }

            private void UploadConstantBuffer(in VolumetricFogParamsDTO dto)
            {
                NativeArray<VolumetricFogParamsDTO> mapped = _paramsBuffer.LockBufferForWrite<VolumetricFogParamsDTO>(0, ConstantBufferCount);
                try
                {
                    mapped[0] = dto;
                }
                finally
                {
                    _paramsBuffer.UnlockBufferAfterWrite<VolumetricFogParamsDTO>(ConstantBufferCount);
                }
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

                _pendingPointLightCount = Mathf.Clamp(desiredPointLightCount, 1, VolumetricFogConstants.MaxPointLights);
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
            }

            private void RefreshCompletedLightJobAndUpload(NativeArray<PointLightDTO> pointLights)
            {
                if (!_mockLightsJobPending || !_mockLightsJobHandle.IsCompleted)
                    return;

                _mockLightsJobHandle.Complete();
                _mockLightsJobPending = false;

                GraphicsBuffer target = GetInactivePointLightBuffer();
                if (target == null || !target.IsValid())
                    return;

                UploadPointLights(target, pointLights);
                _activePointLightBufferIndex = 1 - _activePointLightBufferIndex;
                _lastUploadedPointLightCount = Mathf.Clamp(_pendingPointLightCount, 0, VolumetricFogConstants.MaxPointLights);
                _pendingPointLightCount = 0;
            }

            private unsafe void UploadPointLights(GraphicsBuffer target, NativeArray<PointLightDTO> pointLights)
            {
                int count = Mathf.Min(target.count, pointLights.Length);
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
                int pointLightCount)
            {
                int index = _telemetryWriteIndex % VolumetricFogConstants.TelemetryCapacity;
                uint flags = 0u;
                if (dto.QualityAndLimits.w > 0.5f)
                    flags |= 1u;
                if (_settings.debugHeatmapWeight > 0.001f)
                    flags |= 2u;
                if (Shader.GetGlobalTexture(ShaderConstants.MarineSnowDensityTextureId) == null)
                    flags |= 4u;
                if (Shader.GetGlobalFloat(ShaderConstants.AbyssalFlowTextureActiveId) > 0.5f)
                    flags |= 8u;

                telemetry[index] = new VolumetricFogTelemetryEntry
                {
                    FrameIndex = unchecked((uint)Time.frameCount),
                    RaySteps = raySteps,
                    RenderScale = renderScale,
                    EstimatedGpuMicroseconds = estimatedGpuMicroseconds,
                    CameraPositionLocalAndQuality = new float4(cameraPosition, _qualityWeight),
                    StateHash = math.hash(new float4(_qualityWeight, raySteps, renderScale, pointLightCount)),
                    Flags = flags,
                    AccumulatedDensity = dto.FogColorAndDensity.w,
                    MaxRayDistance = dto.QualityAndLimits.z,
                    DebugValues = new float4(dto.QualityAndLimits.w, _settings.debugHeatmapWeight, pointLightCount, estimatedGpuMicroseconds)
                };
                _telemetryWriteIndex = (_telemetryWriteIndex + 1) % VolumetricFogConstants.TelemetryCapacity;

                if (!_dumpedThisSession &&
                    (!IsFinite(estimatedGpuMicroseconds) || estimatedGpuMicroseconds > DumpThresholdMicroseconds))
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
                float quality = Mathf.Clamp01(qualityWeight);
                float curved = quality * quality * (3f - 2f * quality);
                float updateHz = Mathf.Lerp(5f, 60f, curved);
                int cadenceFrames = Mathf.Clamp(Mathf.RoundToInt(60f / Mathf.Max(5f, updateHz)), 1, 12);
                uint frame = unchecked((uint)Time.frameCount);
                uint cadence = (uint)cadenceFrames;
                uint quantizedFrame = frame - frame % cadence;
                return quantizedFrame * (1f / 60f);
            }

            private static float EstimateGpuMicroseconds(int width, int height, int raySteps, int pointLightCount, float renderScale)
            {
                float pixels = Mathf.Max(1, width) * Mathf.Max(1, height);
                float lightMultiplier = 1f + Mathf.Max(0, pointLightCount) * 0.075f;
                float scalePenalty = Mathf.Lerp(0.85f, 1.25f, Mathf.Clamp01(renderScale));
                return pixels * Mathf.Max(1, raySteps) * lightMultiplier * scalePenalty * 0.000018f;
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
                    _lastUploadedPointLightCount = 0;
                }

                return _paramsBuffer != null && _paramsBuffer.IsValid() &&
                       _pointLightBufferA != null && _pointLightBufferA.IsValid() &&
                       _pointLightBufferB != null && _pointLightBufferB.IsValid();
            }

            private static void ClearPointLightBuffer(GraphicsBuffer buffer)
            {
                if (buffer == null || !buffer.IsValid())
                    return;

                int count = Mathf.Min(buffer.count, VolumetricFogConstants.MaxPointLights);
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

            long setupStartTimestamp = Stopwatch.GetTimestamp();
            float qualityWeight = Mathf.Clamp01(HomeostasisBrain.GlobalQualityWeight);
            _pass.Setup(settings, settings.computeShader, qualityWeight);
            renderer.EnqueuePass(_pass);
            PublishSetupWarningIfNeeded(setupStartTimestamp);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
        }

        private void PublishSetupWarningIfNeeded(long setupStartTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - setupStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= SetupBudgetWarningMilliseconds || Time.frameCount < _nextPerformanceWarningFrame)
                return;

            _nextPerformanceWarningFrame = Time.frameCount + 30;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SetupWarningHash,
                SetupContextHash,
                (float)elapsedMilliseconds);
        }
    }
}
