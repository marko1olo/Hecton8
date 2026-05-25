using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
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
    /// Fullscreen visor droplet and leak distortion driven by the active player wet-lens and hull-stress signals.
    /// </summary>
    public sealed class HectonVisorFluidDistortionFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener
    {
        private const float ThermalDistortionCullStartSpeedMetersPerSecond = 12f;
        private const float ThermalDistortionCullEndSpeedMetersPerSecond = 15f;
        private const float ThermalDistortionCullStartSpeedMetersPerSecondSq = ThermalDistortionCullStartSpeedMetersPerSecond * ThermalDistortionCullStartSpeedMetersPerSecond;
        private const float ThermalDistortionCullEndSpeedMetersPerSecondSq = ThermalDistortionCullEndSpeedMetersPerSecond * ThermalDistortionCullEndSpeedMetersPerSecond;
        private const float ThermalDistortionCullInvSpeedRangeSq = 1f / (ThermalDistortionCullEndSpeedMetersPerSecondSq - ThermalDistortionCullStartSpeedMetersPerSecondSq);
        private const float HullStressVisorContributionStart01 = 0.65f;
        private const float HullStressVisorContributionInvRange = 1f / (1f - HullStressVisorContributionStart01);
        private const float VisorSpeedSquaredToShader01 = 0.0016f;
        private const float QuaternionMinimumLengthSq = 0.000001f;
        private const float QuaternionUnitLengthSqEpsilon = 0.015625f;
        private const int BlackBoxFrameCount = 300;
        private const int BlackBoxEntrySizeBytes = 48;
        private const int VisorFluidGlobalsStrideBytes = 128;
        private const int LensComputeGlobalsStrideBytes = 80;
        private const uint BlackBoxMagic = 0x56535246u;
        private const uint BlackBoxVersion = 1u;
        private const uint BlackBoxFlagPlayerCamera = 1u << 0;
        private const uint BlackBoxFlagVisualActive = 1u << 1;
        private const uint BlackBoxFlagQualityPressure = 1u << 2;
        private const uint BlackBoxFlagHomeostasisFallback = 1u << 3;
        private const uint BlackBoxFlagNonFiniteInput = 1u << 4;
        private const uint BlackBoxFlagThermalMotionCull = 1u << 5;
        private const uint BlackBoxFlagVisualOverkill = 1u << 6;
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SCREEN_SPACE_REFRACTION.bin";

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader";
        private const string ComputeShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_DiegeticVisorLens.compute";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for procedural visor droplets and hull-stress leaks.")]
            public Shader shader = null;

            [Tooltip("Compute shader that resolves compact diegetic lens masks from CPU visor scalars.")]
            public ComputeShader lensComputeShader = null;

            [Tooltip("Injection point for the visor distortion. Before post-processing keeps the effect inside the validated noir stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Maximum UV refraction applied by the procedural fluid mask.")]
            [Range(0f, 0.04f)] public float distortionStrength = 0.012f;

            [Tooltip("Snell approximation strength. Minimum quality pressure fades toward chromatic-only sampling.")]
            [Range(0f, 0.04f)] public float snellStrength = 0.014f;

            [Tooltip("Air, seawater, dense water, and visor glass IOR values consumed as a compact LUT.")]
            public Vector4 refractionIndexLut = new Vector4(1.0003f, 1.333f, 1.38f, 1.46f);

            [Tooltip("Depth softness in metres used to fade refraction near foreground occluders.")]
            [Range(0.005f, 0.5f)] public float depthSoftnessMeters = 0.08f;

            [Tooltip("Hull stress above this value degrades to the cheap chromatic fallback.")]
            [Range(0f, 1f)] public float stressFallbackThreshold = 0.82f;

            [Tooltip("Graphics memory at or below this value raises continuous visor quality pressure.")]
            [FormerlySerializedAs("lowTierVideoMemoryMb")]
            [Min(256)] public int minimumQualityVideoMemoryMb = 2048;

            [Tooltip("Base vertical runoff speed for droplets sliding down the visor.")]
            [Range(0.1f, 4f)] public float runoffSpeed = 1.2f;

            [Tooltip("Base droplet tiling density across the visor.")]
            [Range(2f, 24f)] public float dropletScale = 8f;

            [Tooltip("How strongly camera-relative sideways velocity shears the runoff field.")]
            [Range(0f, 1f)] public float lateralStreakStrength = 0.42f;

            [Tooltip("How strongly forward speed elongates and densifies the streak field.")]
            [Range(0f, 1f)] public float forwardStretchStrength = 0.28f;

            [Tooltip("How strongly high speed pushes droplets radially toward the visor edges.")]
            [Range(0f, 1f)] public float edgeStreakStrength = 0.46f;

            [Tooltip("How much hull stress contributes even when the player is otherwise dry.")]
            [Range(0f, 1f)] public float hullStressContribution = 0.8f;

            [Tooltip("Viewport edge fade used to keep the center readable while droplets accumulate on the visor rim.")]
            [Range(0.1f, 4f)] public float edgeFadeExponent = 1.35f;

            [Tooltip("Dust visibility added by ambient light on the visor layer.")]
            [Range(0f, 1f)] public float dustStrength = 0.28f;

            [Tooltip("How aggressively ambient light exposes visor dust.")]
            [Range(0f, 4f)] public float ambientDustResponse = 1.45f;

            [Tooltip("Visual-overkill salt crystal growth and fine caustic glint. Collapses under quality pressure.")]
            [Range(0f, 1f)] public float visualOverkillStrength = 1f;

            [Tooltip("Upper render scale for the compute-resolved lens mask. GlobalQualityWeight still collapses it downward.")]
            [Range(0.125f, 0.5f)] public float lensMaskRenderScale = 0.25f;
        }

        private readonly struct RuntimeState
        {
            public RuntimeState(
                float wetness,
                float hullStress,
                Vector3 localVelocity,
                float ambientLight01,
                float effectIntensity,
                float rainIntensity,
                float thermalMotionCull01,
                float waterDensitySignal01,
                float homeostasisFallback01,
                float qualityPressure01,
                float visualOverkill01,
                float qualityWeight01,
                Vector4 diegeticLensState,
                Vector4 diegeticLensParams0,
                Vector4 diegeticLensParams1,
                Vector4 diegeticLensParams2,
                uint telemetryFlags)
            {
                Wetness = wetness;
                HullStress = hullStress;
                LocalVelocity = localVelocity;
                AmbientLight01 = ambientLight01;
                EffectIntensity = effectIntensity;
                RainIntensity = rainIntensity;
                ThermalMotionCull01 = thermalMotionCull01;
                WaterDensitySignal01 = waterDensitySignal01;
                HomeostasisFallback01 = homeostasisFallback01;
                QualityPressure01 = qualityPressure01;
                VisualOverkill01 = visualOverkill01;
                QualityWeight01 = qualityWeight01;
                DiegeticLensState = diegeticLensState;
                DiegeticLensParams0 = diegeticLensParams0;
                DiegeticLensParams1 = diegeticLensParams1;
                DiegeticLensParams2 = diegeticLensParams2;
                TelemetryFlags = telemetryFlags;
            }

            public readonly float Wetness;
            public readonly float HullStress;
            public readonly Vector3 LocalVelocity;
            public readonly float AmbientLight01;
            public readonly float EffectIntensity;
            public readonly float RainIntensity;
            public readonly float ThermalMotionCull01;
            public readonly float WaterDensitySignal01;
            public readonly float HomeostasisFallback01;
            public readonly float QualityPressure01;
            public readonly float VisualOverkill01;
            public readonly float QualityWeight01;
            public readonly Vector4 DiegeticLensState;
            public readonly Vector4 DiegeticLensParams0;
            public readonly Vector4 DiegeticLensParams1;
            public readonly Vector4 DiegeticLensParams2;
            public readonly uint TelemetryFlags;
        }

        [StructLayout(LayoutKind.Explicit, Size = BlackBoxEntrySizeBytes)]
        private struct VisorRefractionTelemetryEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint Flags;
            [FieldOffset(8)] public float EffectIntensity01;
            [FieldOffset(12)] public float Wetness01;
            [FieldOffset(16)] public float HullStress01;
            [FieldOffset(20)] public float WaterDensitySignal01;
            [FieldOffset(24)] public float HomeostasisFallback01;
            [FieldOffset(28)] public float LocalVelocitySq;
            [FieldOffset(32)] public uint StateHash;
            [FieldOffset(36)] public ushort CameraPixelWidth;
            [FieldOffset(38)] public ushort CameraPixelHeight;
            [FieldOffset(40)] public uint VaultGeneration;
            [FieldOffset(44)] public uint QualityWeightQ16;
        }

        private sealed class VisorFluidPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal TextureHandle Opaque;
                internal TextureHandle LensMask;
                internal Material Material;
                internal bool LensMaskActive;
            }

            private sealed class LensComputePassData
            {
                internal ComputeShader ComputeShader;
                internal int KernelIndex;
                internal TextureHandle LensMask;
                internal GraphicsBuffer LensComputeGlobalsBuffer;
                internal int DispatchX;
                internal int DispatchY;
            }

            private const float GlobalsFloatEpsilon = 0.0001f;
            private const int LensMaskTextureBucketSize = 64;

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Fluid Distortion");
            private FeatureSettings _settings;
            private Material _material;
            private ComputeShader _lensComputeShader;
            private ComputeShader _resolvedLensComputeShader;
            private RuntimeState _runtimeState;
            private GraphicsBuffer _visorFluidGlobalsBufferA;
            private GraphicsBuffer _visorFluidGlobalsBufferB;
            private GraphicsBuffer _activeVisorFluidGlobalsBuffer;
            private GraphicsBuffer _lensComputeGlobalsBufferA;
            private GraphicsBuffer _lensComputeGlobalsBufferB;
            private GraphicsBuffer _activeLensComputeGlobalsBuffer;
            private VisorFluidGlobalsDTO _lastVisorFluidGlobals;
            private int _lensKernelIndex = -1;
            private int _visorFluidGlobalsWriteIndex;
            private int _lensComputeGlobalsWriteIndex;
            private uint _lensThreadGroupSizeX = 8;
            private uint _lensThreadGroupSizeY = 8;
            private float _lensThreadGroupInvX = 0.125f;
            private float _lensThreadGroupInvY = 0.125f;
            private bool _hasVisorFluidGlobals;

            public VisorFluidPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(FeatureSettings settings, Material material, RuntimeState runtimeState)
            {
                _settings = settings;
                _material = material;
                _lensComputeShader = settings != null ? settings.lensComputeShader : null;
                _runtimeState = runtimeState;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
                ResolveLensComputeKernel();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _material == null ||
                    (_runtimeState.EffectIntensity <= 0.001f && _runtimeState.RainIntensity <= 0.001f))
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                CameraType cameraType = cameraData.cameraType;
                if (cameraType == CameraType.Preview ||
                    cameraType == CameraType.Reflection ||
                    cameraType == CameraType.SceneView)
                {
                    return;
                }

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.cameraDepthTexture;
                TextureHandle opaqueTexture = resourceData.cameraOpaqueTexture;
                if (!sourceTexture.IsValid() || !depthTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonVisorFluidDistortion";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                bool lensMaskActive = TryAddDiegeticLensMaskPass(
                    renderGraph,
                    in sourceDesc,
                    out TextureHandle lensMaskTexture,
                    out float lensMaskBlend);

                if (!UpdateVisorFluidGlobals(_settings, _runtimeState, lensMaskActive, lensMaskBlend))
                    return;

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton Visor Fluid Distortion",
                           out PassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.Opaque = opaqueTexture.IsValid() ? opaqueTexture : sourceTexture;
                    passData.LensMask = lensMaskTexture;
                    passData.LensMaskActive = lensMaskActive;
                    passData.Material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    if (opaqueTexture.IsValid())
                        builder.UseTexture(opaqueTexture, AccessFlags.Read);
                    if (lensMaskActive)
                        builder.UseTexture(lensMaskTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraOpaqueTextureId, data.Opaque);
                        if (data.LensMaskActive)
                            context.cmd.SetGlobalTexture(ShaderConstants.DiegeticLensMaskTextureId, data.LensMask);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            public void Dispose()
            {
                _visorFluidGlobalsBufferA?.Release();
                _visorFluidGlobalsBufferB?.Release();
                _lensComputeGlobalsBufferA?.Release();
                _lensComputeGlobalsBufferB?.Release();
                _visorFluidGlobalsBufferA = null;
                _visorFluidGlobalsBufferB = null;
                _activeVisorFluidGlobalsBuffer = null;
                _lensComputeGlobalsBufferA = null;
                _lensComputeGlobalsBufferB = null;
                _activeLensComputeGlobalsBuffer = null;
                _resolvedLensComputeShader = null;
                _lensKernelIndex = -1;
                _hasVisorFluidGlobals = false;
            }

            public bool PrewarmVisorFluidGlobalsBuffer()
            {
                bool fluidReady = EnsureVisorFluidGlobalsBuffer(allowAllocation: true);
                bool computeReady = !SystemInfo.supportsComputeShaders || EnsureLensComputeGlobalsBuffer(allowAllocation: true);
                return fluidReady && computeReady;
            }

            private bool EnsureVisorFluidGlobalsBuffer(bool allowAllocation)
            {
                if (!SystemInfo.supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (_visorFluidGlobalsBufferA != null && _visorFluidGlobalsBufferA.IsValid() &&
                    _visorFluidGlobalsBufferB != null && _visorFluidGlobalsBufferB.IsValid())
                    return true;

                if (!allowAllocation)
                    return false;

                Dispose();
                // COLD ALLOC: GraphicsBuffer[2] - ping-pong visor fluid RenderGraph CBuffers - owner: HectonVisorFluidDistortionFeature
                _visorFluidGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, VisorFluidGlobalsStrideBytes);
                _visorFluidGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, VisorFluidGlobalsStrideBytes);
                _hasVisorFluidGlobals = false;
                return _visorFluidGlobalsBufferA.IsValid() && _visorFluidGlobalsBufferB.IsValid();
            }

            private bool UpdateVisorFluidGlobals(FeatureSettings settings, RuntimeState runtimeState, bool lensMaskActive, float lensMaskBlend)
            {
                if (!EnsureVisorFluidGlobalsBuffer(allowAllocation: false))
                    return false;

                float effectIntensity = Sanitize01(runtimeState.EffectIntensity);
                float rainIntensity = Sanitize01(runtimeState.RainIntensity);
                float wetness = Sanitize01(runtimeState.Wetness);
                float hullStress = Sanitize01(runtimeState.HullStress);
                float ambientLight01 = Sanitize01(runtimeState.AmbientLight01);
                float thermalMotionCull01 = Sanitize01(runtimeState.ThermalMotionCull01);
                Vector3 localVelocity = SanitizeVector(runtimeState.LocalVelocity);
                float lateralVelocity = math.clamp(localVelocity.x * 0.08f, -1f, 1f);
                float forwardVelocity = math.clamp(localVelocity.z * 0.05f, -1f, 1f);
                float verticalVelocity = math.clamp(localVelocity.y * 0.08f, -1f, 1f);
                float speed01 = math.saturate(
                    (localVelocity.x * localVelocity.x +
                     localVelocity.y * localVelocity.y +
                    localVelocity.z * localVelocity.z) * VisorSpeedSquaredToShader01);
                Vector4 localVelocityShader = new Vector4(lateralVelocity, verticalVelocity, forwardVelocity, 0f);

                VisorFluidGlobalsDTO globals = new VisorFluidGlobalsDTO(
                    new Vector4(effectIntensity, rainIntensity, wetness, hullStress),
                    new Vector4(
                        SanitizeNonNegative(settings.distortionStrength),
                        SanitizeNonNegative(settings.snellStrength),
                        SanitizeAtLeast(settings.depthSoftnessMeters, 0.001f),
                        Sanitize01(runtimeState.WaterDensitySignal01)),
                    new Vector4(
                        Sanitize01(runtimeState.HomeostasisFallback01),
                        Sanitize01(runtimeState.QualityPressure01),
                        Sanitize01(runtimeState.VisualOverkill01),
                        SanitizeAtLeast(settings.runoffSpeed, 0.1f)),
                    SanitizeIorLut(settings.refractionIndexLut),
                    new Vector4(
                        SanitizeAtLeast(settings.dropletScale, 2f),
                        Sanitize01(settings.lateralStreakStrength),
                        Sanitize01(settings.forwardStretchStrength),
                        Sanitize01(settings.edgeStreakStrength)),
                    new Vector4(
                        SanitizeAtLeast(settings.edgeFadeExponent, 0.1f),
                        speed01,
                        thermalMotionCull01,
                        ambientLight01),
                    localVelocityShader,
                    new Vector4(
                        Sanitize01(settings.dustStrength),
                        SanitizeNonNegative(settings.ambientDustResponse),
                        lensMaskActive ? 1f : 0f,
                        Sanitize01(lensMaskBlend)));
                if (_hasVisorFluidGlobals && VisorFluidGlobalsEqual(in _lastVisorFluidGlobals, in globals))
                {
                    if (_activeVisorFluidGlobalsBuffer == null || !_activeVisorFluidGlobalsBuffer.IsValid())
                        return false;

                    Shader.SetGlobalConstantBuffer(ShaderConstants.VisorFluidGlobalsBufferId, _activeVisorFluidGlobalsBuffer, 0, VisorFluidGlobalsStrideBytes);
                    return true;
                }

                GraphicsBuffer writeBuffer = ResolveNextVisorFluidGlobalsBuffer();
                NativeArray<VisorFluidGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<VisorFluidGlobalsDTO>(0, 1);
                mapped[0] = globals;
                writeBuffer.UnlockBufferAfterWrite<VisorFluidGlobalsDTO>(1);
                _activeVisorFluidGlobalsBuffer = writeBuffer;
                _lastVisorFluidGlobals = globals;
                _hasVisorFluidGlobals = true;
                Shader.SetGlobalConstantBuffer(ShaderConstants.VisorFluidGlobalsBufferId, _activeVisorFluidGlobalsBuffer, 0, VisorFluidGlobalsStrideBytes);
                return true;
            }

            private GraphicsBuffer ResolveNextVisorFluidGlobalsBuffer()
            {
                _visorFluidGlobalsWriteIndex ^= 1;
                return _visorFluidGlobalsWriteIndex == 0 ? _visorFluidGlobalsBufferA : _visorFluidGlobalsBufferB;
            }

            private bool EnsureLensComputeGlobalsBuffer(bool allowAllocation)
            {
                if (!SystemInfo.supportsSetConstantBuffer || !SystemInfo.supportsComputeShaders)
                {
                    ReleaseLensComputeGlobalsBuffer();
                    return false;
                }

                if (_lensComputeGlobalsBufferA != null && _lensComputeGlobalsBufferA.IsValid() &&
                    _lensComputeGlobalsBufferB != null && _lensComputeGlobalsBufferB.IsValid())
                    return true;

                if (!allowAllocation)
                    return false;

                ReleaseLensComputeGlobalsBuffer();
                // COLD ALLOC: GraphicsBuffer[2] - ping-pong diegetic visor compute CBuffers - owner: HectonVisorFluidDistortionFeature
                _lensComputeGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, LensComputeGlobalsStrideBytes);
                _lensComputeGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, LensComputeGlobalsStrideBytes);
                return _lensComputeGlobalsBufferA.IsValid() && _lensComputeGlobalsBufferB.IsValid();
            }

            private void ReleaseLensComputeGlobalsBuffer()
            {
                _lensComputeGlobalsBufferA?.Release();
                _lensComputeGlobalsBufferB?.Release();
                _lensComputeGlobalsBufferA = null;
                _lensComputeGlobalsBufferB = null;
                _activeLensComputeGlobalsBuffer = null;
            }

            private GraphicsBuffer ResolveNextLensComputeGlobalsBuffer()
            {
                _lensComputeGlobalsWriteIndex ^= 1;
                return _lensComputeGlobalsWriteIndex == 0 ? _lensComputeGlobalsBufferA : _lensComputeGlobalsBufferB;
            }

            private bool UpdateLensComputeGlobals(in RuntimeState runtimeState, float lensMaskBlend, out GraphicsBuffer globalsBuffer)
            {
                globalsBuffer = null;
                if (!EnsureLensComputeGlobalsBuffer(allowAllocation: false))
                    return false;

                LensComputeGlobalsDTO globals = new LensComputeGlobalsDTO(
                    runtimeState.DiegeticLensState,
                    runtimeState.DiegeticLensParams0,
                    runtimeState.DiegeticLensParams1,
                    runtimeState.DiegeticLensParams2,
                    new Vector4(
                        Time.timeSinceLevelLoad,
                        Sanitize01(lensMaskBlend),
                        Sanitize01(runtimeState.QualityPressure01),
                        Sanitize01(runtimeState.VisualOverkill01)));

                GraphicsBuffer writeBuffer = ResolveNextLensComputeGlobalsBuffer();
                NativeArray<LensComputeGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<LensComputeGlobalsDTO>(0, 1);
                mapped[0] = globals;
                writeBuffer.UnlockBufferAfterWrite<LensComputeGlobalsDTO>(1);
                _activeLensComputeGlobalsBuffer = writeBuffer;
                globalsBuffer = writeBuffer;
                return true;
            }

            private void ResolveLensComputeKernel()
            {
                if (ReferenceEquals(_resolvedLensComputeShader, _lensComputeShader))
                    return;

                _resolvedLensComputeShader = _lensComputeShader;
                _lensKernelIndex = -1;
                _lensThreadGroupSizeX = 8;
                _lensThreadGroupSizeY = 8;
                _lensThreadGroupInvX = 0.125f;
                _lensThreadGroupInvY = 0.125f;
                if (_lensComputeShader == null)
                    return;

                try
                {
                    _lensKernelIndex = _lensComputeShader.FindKernel("ResolveDiegeticVisorLensMask");
                    _lensComputeShader.GetKernelThreadGroupSizes(_lensKernelIndex, out _lensThreadGroupSizeX, out _lensThreadGroupSizeY, out _);
                    _lensThreadGroupInvX = math.rcp(math.max(1f, (float)_lensThreadGroupSizeX));
                    _lensThreadGroupInvY = math.rcp(math.max(1f, (float)_lensThreadGroupSizeY));
                }
                catch (Exception)
                {
                    _lensKernelIndex = -1;
                }
            }

            private bool TryAddDiegeticLensMaskPass(RenderGraph renderGraph, in TextureDesc sourceDesc, out TextureHandle lensMaskTexture, out float lensMaskBlend)
            {
                lensMaskTexture = default;
                lensMaskBlend = ResolveLensMaskBlend(in _runtimeState);
                if (!SystemInfo.supportsComputeShaders ||
                    _lensComputeShader == null ||
                    _lensKernelIndex < 0 ||
                    lensMaskBlend <= 0.001f)
                {
                    return false;
                }

                Vector4 lensState = _runtimeState.DiegeticLensState;
                float lensActivity = math.max(
                    math.max(Sanitize01(lensState.x), Sanitize01(lensState.y)),
                    math.max(Sanitize01(lensState.z), Sanitize01(lensState.w)));
                if (lensActivity <= 0.001f)
                    return false;

                float renderScale = ResolveLensMaskRenderScale(_settings, in _runtimeState);
                int maskWidth = QuantizeLensMaskDimension(math.max(1, (int)math.round(sourceDesc.width * renderScale)));
                int maskHeight = QuantizeLensMaskDimension(math.max(1, (int)math.round(sourceDesc.height * renderScale)));
                if (!UpdateLensComputeGlobals(in _runtimeState, lensMaskBlend, out GraphicsBuffer lensComputeGlobalsBuffer))
                    return false;
                BufferHandle lensComputeGlobalsHandle = renderGraph.ImportBuffer(lensComputeGlobalsBuffer);

                TextureDesc maskDesc = new TextureDesc(maskWidth, maskHeight, dynamicResolution: false, xrReady: false);
                maskDesc.name = "_HectonDiegeticVisorLensMask";
                maskDesc.clearBuffer = false;
                maskDesc.depthBufferBits = DepthBits.None;
                maskDesc.msaaSamples = MSAASamples.None;
                maskDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                maskDesc.dimension = TextureDimension.Tex2D;
                maskDesc.slices = 1;
                maskDesc.vrUsage = VRTextureUsage.None;
                maskDesc.useDynamicScale = false;
                maskDesc.useDynamicScaleExplicit = false;
                maskDesc.enableRandomWrite = true;
                maskDesc.filterMode = FilterMode.Bilinear;
                maskDesc.wrapMode = TextureWrapMode.Clamp;
                maskDesc.useMipMap = false;
                maskDesc.autoGenerateMips = false;
                lensMaskTexture = renderGraph.CreateTexture(maskDesc);

                using (var builder = renderGraph.AddComputePass("Hecton Diegetic Visor Lens Mask", out LensComputePassData passData, _profilingSampler))
                {
                    passData.ComputeShader = _lensComputeShader;
                    passData.KernelIndex = _lensKernelIndex;
                    passData.LensMask = lensMaskTexture;
                    passData.LensComputeGlobalsBuffer = lensComputeGlobalsBuffer;
                    passData.DispatchX = CeilByThreadGroup(maskWidth, _lensThreadGroupInvX);
                    passData.DispatchY = CeilByThreadGroup(maskHeight, _lensThreadGroupInvY);

                    builder.UseTexture(lensMaskTexture, AccessFlags.Write);
                    builder.UseBuffer(lensComputeGlobalsHandle, AccessFlags.Read);
                    builder.SetRenderFunc(static (LensComputePassData data, ComputeGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, ShaderConstants.DiegeticLensMaskWriteId, data.LensMask);
                        cmd.SetComputeConstantBufferParam(data.ComputeShader, ShaderConstants.DiegeticLensComputeGlobalsBufferId, data.LensComputeGlobalsBuffer, 0, LensComputeGlobalsStrideBytes);
                        cmd.DispatchCompute(data.ComputeShader, data.KernelIndex, data.DispatchX, data.DispatchY, 1);
                    });
                }

                return true;
            }

            private static float ResolveLensMaskBlend(in RuntimeState runtimeState)
            {
                float quality = Sanitize01(runtimeState.DiegeticLensParams1.x);
                float qualityBlend = Smooth01((quality - 0.22f) * (1f / 0.5f));
                float pressureAttenuation = 1f - Sanitize01(runtimeState.QualityPressure01) * 0.82f;
                return math.saturate(qualityBlend * pressureAttenuation + Sanitize01(runtimeState.VisualOverkill01) * 0.18f);
            }

            private static float ResolveLensMaskRenderScale(FeatureSettings settings, in RuntimeState runtimeState)
            {
                float configuredScale = settings != null ? math.clamp(settings.lensMaskRenderScale, 0.125f, 0.5f) : 0.25f;
                float quality = Sanitize01(runtimeState.DiegeticLensParams1.x);
                float qualityScale = Smooth01((quality - 0.1f) * (1f / 0.9f));
                float baseScale = math.lerp(0.125f, configuredScale, qualityScale);
                return math.clamp(baseScale + Sanitize01(runtimeState.VisualOverkill01) * 0.125f, 0.125f, 0.5f);
            }

            private static int CeilByThreadGroup(int dimension, float invThreadGroupSize)
            {
                return math.max(1, (int)math.ceil(math.max(1, dimension) * invThreadGroupSize));
            }

            private static int QuantizeLensMaskDimension(int dimension)
            {
                int safeDimension = math.max(1, dimension);
                return (safeDimension + LensMaskTextureBucketSize - 1) & ~(LensMaskTextureBucketSize - 1);
            }

            private static bool VisorFluidGlobalsEqual(in VisorFluidGlobalsDTO left, in VisorFluidGlobalsDTO right)
            {
                return Vector4Approximately(left.Params0, right.Params0) &&
                       Vector4Approximately(left.Params1, right.Params1) &&
                       Vector4Approximately(left.Params2, right.Params2) &&
                       Vector4Approximately(left.IorLut, right.IorLut) &&
                       Vector4Approximately(left.Params3, right.Params3) &&
                       Vector4Approximately(left.Params4, right.Params4) &&
                       Vector4Approximately(left.LocalVelocity, right.LocalVelocity) &&
                       Vector4Approximately(left.Params5, right.Params5);
            }

            private static bool Vector4Approximately(Vector4 left, Vector4 right)
            {
                return math.abs(left.x - right.x) <= GlobalsFloatEpsilon &&
                       math.abs(left.y - right.y) <= GlobalsFloatEpsilon &&
                       math.abs(left.z - right.z) <= GlobalsFloatEpsilon &&
                       math.abs(left.w - right.w) <= GlobalsFloatEpsilon;
            }

            [StructLayout(LayoutKind.Explicit, Size = VisorFluidGlobalsStrideBytes)]
            private struct VisorFluidGlobalsDTO
            {
                [FieldOffset(0)]
                public Vector4 Params0;

                [FieldOffset(16)]
                public Vector4 Params1;

                [FieldOffset(32)]
                public Vector4 Params2;

                [FieldOffset(48)]
                public Vector4 IorLut;

                [FieldOffset(64)]
                public Vector4 Params3;

                [FieldOffset(80)]
                public Vector4 Params4;

                [FieldOffset(96)]
                public Vector4 LocalVelocity;

                [FieldOffset(112)]
                public Vector4 Params5;

                public VisorFluidGlobalsDTO(
                    Vector4 params0,
                    Vector4 params1,
                    Vector4 params2,
                    Vector4 iorLut,
                    Vector4 params3,
                    Vector4 params4,
                    Vector4 localVelocity,
                    Vector4 params5)
                {
                    Params0 = params0;
                    Params1 = params1;
                    Params2 = params2;
                    IorLut = iorLut;
                    Params3 = params3;
                    Params4 = params4;
                    LocalVelocity = localVelocity;
                    Params5 = params5;
                }
            }

            [StructLayout(LayoutKind.Explicit, Size = LensComputeGlobalsStrideBytes)]
            private struct LensComputeGlobalsDTO
            {
                [FieldOffset(0)]
                public Vector4 LensState;

                [FieldOffset(16)]
                public Vector4 LensParams0;

                [FieldOffset(32)]
                public Vector4 LensParams1;

                [FieldOffset(48)]
                public Vector4 LensParams2;

                [FieldOffset(64)]
                public Vector4 ComputeParams;

                public LensComputeGlobalsDTO(
                    Vector4 lensState,
                    Vector4 lensParams0,
                    Vector4 lensParams1,
                    Vector4 lensParams2,
                    Vector4 computeParams)
                {
                    LensState = lensState;
                    LensParams0 = lensParams0;
                    LensParams1 = lensParams1;
                    LensParams2 = lensParams2;
                    ComputeParams = computeParams;
                }
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int VisorFluidGlobalsBufferId = Shader.PropertyToID("HectonVisorFluidDistortionGlobals");
            internal static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");
            internal static readonly int WaterDensitySignalId = Shader.PropertyToID("_HectonWaterDensitySignal");
            internal static readonly int DiegeticLensStateId = Shader.PropertyToID("_HectonDiegeticVisorLensState");
            internal static readonly int DiegeticLensParams0Id = Shader.PropertyToID("_HectonDiegeticVisorLensParams0");
            internal static readonly int DiegeticLensParams1Id = Shader.PropertyToID("_HectonDiegeticVisorLensParams1");
            internal static readonly int DiegeticLensParams2Id = Shader.PropertyToID("_HectonDiegeticVisorLensParams2");
            internal static readonly int DiegeticLensComputeGlobalsBufferId = Shader.PropertyToID("HectonDiegeticVisorLensComputeGlobals");
            internal static readonly int DiegeticLensMaskWriteId = Shader.PropertyToID("_HectonDiegeticVisorLensMask");
            internal static readonly int DiegeticLensMaskTextureId = Shader.PropertyToID("_HectonDiegeticVisorLensMaskTex");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int CameraOpaqueTextureId = Shader.PropertyToID("_CameraOpaqueTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private VisorFluidPass _pass;
        private Material _material;
        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private IFluidSim _fluidSimulation;
        private VaultGenerationHandle<VisorRefractionTelemetryEntry> _blackBoxHandle;
        private uint _blackBoxVaultGeneration;
        private bool _blackBoxHandleOwned;
        private bool _blackBoxDumped;
        private bool _blackBoxHotSwapRegistered;

        private void OnEnable()
        {
            TryRegisterBlackBoxHotSwapListener();
            CacheBlackBoxVaultCold(GlobalRegistry.DataVault);
        }

        private void OnDisable()
        {
            ReleaseBlackBoxLease();
            TryUnregisterBlackBoxHotSwapListener();
        }

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
            if (settings != null && settings.lensComputeShader == null)
                settings.lensComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderAssetPath);
#endif

            _pass ??= new VisorFluidPass();
            _pass.PrewarmVisorFluidGlobalsBuffer();
            TryRegisterBlackBoxHotSwapListener();
            CacheBlackBoxVaultCold(GlobalRegistry.DataVault);
            Shader shader = settings != null ? settings.shader : null;
            if (shader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                return;
            }

            RecreateMaterial(ref _material, shader);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || _material == null)
                return;

            Camera renderCamera = renderingData.cameraData.camera;
            if (!TryBuildRuntimeState(renderCamera, settings, out RuntimeState runtimeState))
                return;

            WriteBlackBoxFrame(renderCamera, in runtimeState);
            if (runtimeState.EffectIntensity <= 0.001f && runtimeState.RainIntensity <= 0.001f)
                return;

            _pass.Setup(settings, _material, runtimeState);
            renderer.EnqueuePass(_pass);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
            _material = null;
            _playerContext = null;
            _fluidSimulation = null;
            ReleaseBlackBoxLease();
            TryUnregisterBlackBoxHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ReleaseBlackBoxLease();
                CacheBlackBoxVaultCold(currentService as IDataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidSimulation)
                _fluidSimulation = currentService as IFluidSim;
        }

        private bool TryBuildRuntimeState(
            Camera renderCamera,
            FeatureSettings settings,
            out RuntimeState runtimeState)
        {
            runtimeState = default;
            uint telemetryFlags = 0u;
            if (renderCamera == null || settings == null)
                return false;

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (playerContext == null)
                return false;

            Camera playerCamera = playerContext.PlayerCamera;
            var playerMovement = playerContext.PlayerMovement;

            if (playerCamera == null || !ReferenceEquals(renderCamera, playerCamera))
                return false;

            Transform playerCameraTransform = playerCamera.transform;
            float rawWetness = playerMovement != null ? playerMovement.CurrentWetLensIntensity01 : 0f;
            float rawHullStress = playerMovement != null ? playerMovement.CurrentHullStress01 : 0f;
            FlagIfNonFinite(rawWetness, ref telemetryFlags);
            FlagIfNonFinite(rawHullStress, ref telemetryFlags);
            float wetness = Sanitize01(rawWetness);
            float hullStress = Sanitize01(rawHullStress);
            Vector4 rawLensState = Shader.GetGlobalVector(ShaderConstants.DiegeticLensStateId);
            Vector4 rawLensParams0 = Shader.GetGlobalVector(ShaderConstants.DiegeticLensParams0Id);
            Vector4 rawLensParams1 = Shader.GetGlobalVector(ShaderConstants.DiegeticLensParams1Id);
            Vector4 rawLensParams2 = Shader.GetGlobalVector(ShaderConstants.DiegeticLensParams2Id);
            FlagIfNonFinite(rawLensState, ref telemetryFlags);
            FlagIfNonFinite(rawLensParams0, ref telemetryFlags);
            FlagIfNonFinite(rawLensParams1, ref telemetryFlags);
            FlagIfNonFinite(rawLensParams2, ref telemetryFlags);
            float lensCondensation = Sanitize01(rawLensState.x);
            float lensDroplets = Sanitize01(rawLensState.y);
            float lensCrack = Sanitize01(rawLensState.z);
            float lensDirt = Sanitize01(rawLensState.w);
            float lensRefractionScale = Sanitize01(rawLensParams0.w);
            float lensSurfaceWash = Sanitize01(rawLensParams1.z);
            Vector4 sanitizedLensState = new Vector4(lensCondensation, lensDroplets, lensCrack, lensDirt);
            Vector4 sanitizedLensParams0 = new Vector4(
                SanitizeSigned(rawLensParams0.x, -1f, 1f),
                SanitizeSigned(rawLensParams0.y, -1f, 1f),
                Sanitize01(rawLensParams0.z),
                lensRefractionScale);
            Vector4 sanitizedLensParams1 = new Vector4(
                Sanitize01(rawLensParams1.x),
                Sanitize01(rawLensParams1.y),
                lensSurfaceWash,
                Sanitize01(rawLensParams1.w));
            Vector4 sanitizedLensParams2 = new Vector4(
                Sanitize01(rawLensParams2.x),
                Sanitize01(rawLensParams2.y),
                SanitizeNonNegative(rawLensParams2.z),
                0f);
            float lensContribution = math.max(
                math.max(lensCondensation, lensDroplets),
                math.max(lensCrack, lensDirt));
            wetness = math.saturate(math.max(wetness, math.max(lensDroplets, lensCondensation * 0.35f)));
            hullStress = math.saturate(math.max(hullStress, lensCrack));
            float dustStrength = Sanitize01(settings.dustStrength);
            float ambientDustResponse = SanitizeNonNegative(settings.ambientDustResponse);
            float ambientLight01 = 0f;
            float dustContribution = 0f;
            if (dustStrength > 0.001f && ambientDustResponse > 0.001f)
            {
                float rawAmbientLight = ResolveAmbientLight01();
                FlagIfNonFinite(rawAmbientLight, ref telemetryFlags);
                ambientLight01 = Sanitize01(rawAmbientLight);
                dustContribution = math.saturate(ambientLight01 * dustStrength * ambientDustResponse);
            }
            dustContribution = math.saturate(math.max(dustContribution, lensDirt * (0.18f + ambientLight01 * 0.82f)));

            float hullContribution = math.saturate(
                math.saturate((hullStress - HullStressVisorContributionStart01) * HullStressVisorContributionInvRange) *
                Sanitize01(settings.hullStressContribution));
            float effectIntensity = math.saturate(math.max(math.max(math.max(wetness, hullContribution), dustContribution), lensContribution));
            float rawRainIntensity = Shader.GetGlobalFloat(ShaderConstants.RainIntensityId);
            FlagIfNonFinite(rawRainIntensity, ref telemetryFlags);
            float rainIntensity = math.saturate(math.max(Sanitize01(rawRainIntensity), lensSurfaceWash * 0.35f));

            Vector3 localVelocity = Vector3.zero;
            float fluidVelocityActivity = math.max(wetness, hullStress);
            if (playerMovement != null && fluidVelocityActivity > 0.001f)
            {
                Vector3 rawWorldVelocity = playerMovement.InterpolatedLinearVelocity;
                FlagIfNonFinite(rawWorldVelocity, ref telemetryFlags);
                localVelocity = ResolveCameraLocalVelocity(playerCameraTransform, rawWorldVelocity);
            }
            if (lensContribution > 0.001f)
            {
                localVelocity.x += sanitizedLensParams0.x * 6f;
                localVelocity.y += sanitizedLensParams0.y * 6f;
            }

            float localVelocitySq =
                localVelocity.x * localVelocity.x +
                localVelocity.y * localVelocity.y +
                localVelocity.z * localVelocity.z;
            float thermalMotionCull01 = Smooth01((localVelocitySq - ThermalDistortionCullStartSpeedMetersPerSecondSq) * ThermalDistortionCullInvSpeedRangeSq);
            float waterDensitySignal01 = ResolveWaterDensitySignal01(ref telemetryFlags);
            float globalQualityWeight = ResolveGlobalQualityWeight();
            float qualityPressureFromWeight01 = 1f - Smooth01((globalQualityWeight - 0.18f) * (1f / 0.12f));
            float hardwareQualityPressure01 = ResolveHardwareQualityPressure01(settings);
            float lensQualityPressure01 = lensContribution > 0.001f ? 1f - lensRefractionScale : 0f;
            float stressFallback01 = Smooth01((hullStress - Sanitize01(settings.stressFallbackThreshold)) * 5f);
            float qualityPressure01 = math.saturate(math.max(math.max(qualityPressureFromWeight01, hardwareQualityPressure01), lensQualityPressure01));
            float homeostasisFallback01 = math.saturate(math.max(qualityPressure01, stressFallback01));
            float visualOverkill01 = ResolveVisualOverkill01(settings, qualityPressure01, globalQualityWeight);
            runtimeState = new RuntimeState(
                wetness,
                hullStress,
                localVelocity,
                ambientLight01,
                effectIntensity,
                rainIntensity,
                thermalMotionCull01,
                waterDensitySignal01,
                homeostasisFallback01,
                qualityPressure01,
                visualOverkill01,
                globalQualityWeight,
                sanitizedLensState,
                sanitizedLensParams0,
                sanitizedLensParams1,
                sanitizedLensParams2,
                telemetryFlags);
            return true;
        }

        private static Vector3 ResolveCameraLocalVelocity(Transform cameraTransform, Vector3 worldVelocity)
        {
            if (cameraTransform == null)
                return Vector3.zero;

            worldVelocity = SanitizeVector(worldVelocity);
            Quaternion cameraRotation = cameraTransform.rotation;
            if (!TrySanitizeQuaternion(cameraRotation, out cameraRotation))
                return Vector3.zero;

            Vector3 cameraRight = cameraRotation * Vector3.right;
            Vector3 cameraUp = cameraRotation * Vector3.up;
            Vector3 cameraForward = cameraRotation * Vector3.forward;
            return new Vector3(
                Vector3.Dot(worldVelocity, cameraRight),
                Vector3.Dot(worldVelocity, cameraUp),
                Vector3.Dot(worldVelocity, cameraForward));
        }

        private static bool TrySanitizeQuaternion(Quaternion value, out Quaternion sanitized)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) || !math.isfinite(lengthSq) || lengthSq <= QuaternionMinimumLengthSq)
            {
                sanitized = Quaternion.identity;
                return false;
            }

            if (math.abs(lengthSq - 1f) > QuaternionUnitLengthSqEpsilon)
                q *= math.rcp(math.max(ApproximateMagnitude(q), 0.000001f));

            sanitized = new Quaternion(q.x, q.y, q.z, q.w);
            return true;
        }

        private static float ApproximateMagnitude(float4 value)
        {
            float4 absValue = math.abs(value);
            float maxA = math.max(absValue.x, absValue.y);
            float maxB = math.max(absValue.z, absValue.w);
            float maxAxis = math.max(maxA, maxB);
            float minA = math.min(absValue.x, absValue.y);
            float minB = math.min(absValue.z, absValue.w);
            float minAxis = math.min(minA, minB);
            float midSum = absValue.x + absValue.y + absValue.z + absValue.w - maxAxis - minAxis;
            return maxAxis + (midSum * 0.25f) + (minAxis * 0.125f);
        }

        private static float ResolveAmbientLight01()
        {
            Color ambientColor = RenderSettings.ambientLight.linear;
            float colorIntensity = math.max(ambientColor.r, math.max(ambientColor.g, ambientColor.b));
            return math.saturate(math.max(RenderSettings.ambientIntensity, colorIntensity));
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizeAtLeast(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }

        private static float SanitizeSigned(float value, float minimum, float maximum)
        {
            return math.isfinite(value) ? math.clamp(value, minimum, maximum) : 0f;
        }

        private static Vector4 SanitizeIorLut(Vector4 value)
        {
            float air = math.max(1.0001f, SanitizeAtLeast(value.x, 1.0003f));
            float water = math.max(air, SanitizeAtLeast(value.y, 1.333f));
            float denseWater = math.max(water, SanitizeAtLeast(value.z, 1.38f));
            float glass = math.max(water, SanitizeAtLeast(value.w, 1.46f));
            return new Vector4(air, water, denseWater, glass);
        }

        private static float ResolveHardwareQualityPressure01(FeatureSettings settings)
        {
            int thresholdMb = settings != null ? math.max(256, settings.minimumQualityVideoMemoryMb) : 2048;
            float graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            if (!math.isfinite(graphicsMemoryMb) || graphicsMemoryMb <= 0f)
                return 0f;

            float normalizedHeadroom = (graphicsMemoryMb - thresholdMb) * math.rcp(math.max(1f, (float)thresholdMb));
            return 1f - Smooth01(normalizedHeadroom);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(weight) ? math.saturate(weight) : 0.5f;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static float ResolveVisualOverkill01(FeatureSettings settings, float qualityPressure01, float globalQualityWeight)
        {
            float configuredStrength = settings != null ? Sanitize01(settings.visualOverkillStrength) : 0f;
            float qualityOverkill = Smooth01((Sanitize01(globalQualityWeight) - 0.56f) * (1f / 0.44f));
            float thermalHeadroom = 1f - Sanitize01(qualityPressure01);
            return configuredStrength * thermalHeadroom * qualityOverkill;
        }

        private IPlayerRuntimeContext ResolvePlayerContext()
        {
            IPlayerRuntimeContext context = _playerContext;
            if (context != null && context.PlayerCamera != null)
                return context;

            context = GlobalRegistry.Player;
            _playerContext = context;
            return context;
        }

        private IFluidSim ResolveFluidSimulation()
        {
            IFluidSim fluidSimulation = _fluidSimulation;
            if (fluidSimulation != null && fluidSimulation.IsReady)
                return fluidSimulation;

            fluidSimulation = GlobalRegistry.FluidSimulation;
            _fluidSimulation = fluidSimulation;
            return fluidSimulation;
        }

        private float ResolveWaterDensitySignal01(ref uint telemetryFlags)
        {
            float globalSignal = Shader.GetGlobalFloat(ShaderConstants.WaterDensitySignalId);
            FlagIfNonFinite(globalSignal, ref telemetryFlags);
            if (math.isfinite(globalSignal) && globalSignal > 0.0001f)
                return math.saturate(globalSignal);

            IFluidSim fluidSimulation = ResolveFluidSimulation();
            if (fluidSimulation == null || !fluidSimulation.IsReady)
                return 0f;

            float density = fluidSimulation.WaterDensityKilogramsPerCubicMeter;
            FlagIfNonFinite(density, ref telemetryFlags);
            return math.isfinite(density)
                ? math.saturate((density - HectonPhysicsContract.WaterDensityKgPerCubicMeterConst) * (1f / 256f))
                : 0f;
        }

        private static Vector3 SanitizeVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z)
                ? value
                : Vector3.zero;
        }

        private static void FlagIfNonFinite(float value, ref uint flags)
        {
            if (!math.isfinite(value))
                flags |= BlackBoxFlagNonFiniteInput;
        }

        private static void FlagIfNonFinite(Vector3 value, ref uint flags)
        {
            if (!math.isfinite(value.x) || !math.isfinite(value.y) || !math.isfinite(value.z))
                flags |= BlackBoxFlagNonFiniteInput;
        }

        private static void FlagIfNonFinite(Vector4 value, ref uint flags)
        {
            if (!math.isfinite(value.x) || !math.isfinite(value.y) || !math.isfinite(value.z) || !math.isfinite(value.w))
                flags |= BlackBoxFlagNonFiniteInput;
        }

        private void WriteBlackBoxFrame(Camera renderCamera, in RuntimeState runtimeState)
        {
            int frame = Time.frameCount;
            if (!TryResolveBlackBoxRing(out NativeArray<VisorRefractionTelemetryEntry> blackBox, out int blackBoxLength))
                return;

            Vector3 localVelocity = SanitizeVector(runtimeState.LocalVelocity);
            float localVelocitySq =
                localVelocity.x * localVelocity.x +
                localVelocity.y * localVelocity.y +
                localVelocity.z * localVelocity.z;
            uint flags = BlackBoxFlagPlayerCamera | runtimeState.TelemetryFlags;
            if (runtimeState.EffectIntensity > 0.001f || runtimeState.RainIntensity > 0.001f)
                flags |= BlackBoxFlagVisualActive;
            if (runtimeState.QualityPressure01 > 0.001f)
                flags |= BlackBoxFlagQualityPressure;
            if (runtimeState.HomeostasisFallback01 > 0.5f)
                flags |= BlackBoxFlagHomeostasisFallback;
            if (runtimeState.ThermalMotionCull01 > 0.5f)
                flags |= BlackBoxFlagThermalMotionCull;
            if (runtimeState.VisualOverkill01 > 0.001f)
                flags |= BlackBoxFlagVisualOverkill;

            int blackBoxIndex = ResolveBlackBoxIndex(frame, blackBoxLength);
            blackBox[blackBoxIndex] = new VisorRefractionTelemetryEntry
            {
                FrameIndex = frame >= 0 ? (uint)frame : 0u,
                Flags = flags,
                EffectIntensity01 = Sanitize01(runtimeState.EffectIntensity),
                Wetness01 = Sanitize01(runtimeState.Wetness),
                HullStress01 = Sanitize01(runtimeState.HullStress),
                WaterDensitySignal01 = Sanitize01(runtimeState.WaterDensitySignal01),
                HomeostasisFallback01 = Sanitize01(runtimeState.HomeostasisFallback01),
                LocalVelocitySq = SanitizeNonNegative(localVelocitySq),
                StateHash = BuildBlackBoxHash(in runtimeState, flags),
                CameraPixelWidth = ClampUShort(renderCamera != null ? renderCamera.pixelWidth : 0),
                CameraPixelHeight = ClampUShort(renderCamera != null ? renderCamera.pixelHeight : 0),
                VaultGeneration = _blackBoxVaultGeneration,
                QualityWeightQ16 = EncodeQualityQ16(runtimeState.QualityWeight01)
            };

            if ((flags & BlackBoxFlagNonFiniteInput) != 0u)
                DumpBlackBoxOnce(flags, blackBox, blackBoxLength, ResolveBlackBoxIndex(frame + 1, blackBoxLength));
        }

        private bool TryEnsureBlackBoxLease()
        {
            if (TryResolveCurrentBlackBoxRing(out _, out _))
                return true;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                ClearBlackBoxDescriptor();
                return false;
            }

            if (vault.TryGetGenerationHandle(
                    BufferID.VisorRefractionBlackBox,
                    out VaultGenerationHandle<VisorRefractionTelemetryEntry> existingHandle) &&
                vault.TryResolveHandle(in existingHandle, out NativeArray<VisorRefractionTelemetryEntry> existingBlackBox) &&
                existingBlackBox.IsCreated &&
                existingBlackBox.Length >= BlackBoxFrameCount)
            {
                _blackBoxHandle = existingHandle;
                _blackBoxVaultGeneration = existingHandle.Generation;
                _blackBoxHandleOwned = false;
                return true;
            }

            VaultGenerationHandle<VisorRefractionTelemetryEntry> blackBoxHandle = vault.EnsureGenerationHandle<VisorRefractionTelemetryEntry>(
                BufferID.VisorRefractionBlackBox,
                BlackBoxFrameCount,
                SystemID.Vfx);
            if (!IsVaultHandleCreated(in blackBoxHandle) ||
                !vault.TryResolveHandle(in blackBoxHandle, out NativeArray<VisorRefractionTelemetryEntry> blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length < BlackBoxFrameCount)
            {
                ClearBlackBoxDescriptor();
                return false;
            }

            _blackBoxHandle = blackBoxHandle;
            _blackBoxVaultGeneration = blackBoxHandle.Generation;
            _blackBoxHandleOwned = true;
            return true;
        }

        private bool TryResolveBlackBoxRing(out NativeArray<VisorRefractionTelemetryEntry> blackBox, out int blackBoxLength)
        {
            if (TryResolveCurrentBlackBoxRing(out blackBox, out blackBoxLength))
                return true;

            if (!TryEnsureBlackBoxLease())
                return false;

            return TryResolveCurrentBlackBoxRing(out blackBox, out blackBoxLength);
        }

        private bool TryResolveCurrentBlackBoxRing(out NativeArray<VisorRefractionTelemetryEntry> blackBox, out int blackBoxLength)
        {
            blackBox = default;
            blackBoxLength = 0;
            if (_dataVault == null ||
                !IsVaultHandleCreated(in _blackBoxHandle) ||
                _dataVault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!_dataVault.TryResolveHandle(in _blackBoxHandle, out blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length < BlackBoxFrameCount)
            {
                ClearBlackBoxDescriptor();
                return false;
            }

            _blackBoxVaultGeneration = _blackBoxHandle.Generation;
            blackBoxLength = blackBox.Length;
            return true;
        }

        private void CacheBlackBoxVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseBlackBoxLease();
            _dataVault = vault;
        }

        private void ReleaseBlackBoxLease()
        {
            IDataVault vault = _dataVault;
            if (vault != null &&
                _blackBoxHandleOwned &&
                IsVaultHandleCreated(in _blackBoxHandle) &&
                !vault.IsCompactionFenceActive &&
                vault.TryGetGenerationHandle(
                    BufferID.VisorRefractionBlackBox,
                    out VaultGenerationHandle<VisorRefractionTelemetryEntry> currentHandle) &&
                currentHandle.Generation == _blackBoxHandle.Generation)
            {
                vault.ReleaseBuffer(in _blackBoxHandle);
            }

            ClearBlackBoxDescriptor();
            _dataVault = null;
        }

        private void ClearBlackBoxDescriptor()
        {
            _blackBoxHandle = default;
            _blackBoxVaultGeneration = 0u;
            _blackBoxHandleOwned = false;
        }

        private void TryRegisterBlackBoxHotSwapListener()
        {
            if (_blackBoxHotSwapRegistered)
                return;

            _blackBoxHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterBlackBoxHotSwapListener()
        {
            if (!_blackBoxHotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _blackBoxHotSwapRegistered = false;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static uint BuildBlackBoxHash(in RuntimeState runtimeState, uint flags)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, flags);
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.EffectIntensity)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.Wetness)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.HullStress)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.WaterDensitySignal01)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.QualityPressure01)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.VisualOverkill01)));
            hash = MixHash(hash, EncodeQualityQ16(runtimeState.QualityWeight01));
            Vector3 velocity = SanitizeVector(runtimeState.LocalVelocity);
            hash = MixHash(hash, math.asuint(velocity.x));
            hash = MixHash(hash, math.asuint(velocity.y));
            hash = MixHash(hash, math.asuint(velocity.z));
            return hash;
        }

        private static uint EncodeQualityQ16(float qualityWeight01)
        {
            return (uint)math.round(Sanitize01(qualityWeight01) * 65535f);
        }

        private static uint MixHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private static ushort ClampUShort(int value)
        {
            if (value <= 0)
                return 0;
            if (value >= ushort.MaxValue)
                return ushort.MaxValue;
            return (ushort)value;
        }

        private static int ResolveBlackBoxIndex(int frame, int blackBoxLength)
        {
            if (blackBoxLength <= 1)
                return 0;

            int index = frame % blackBoxLength;
            return index >= 0 ? index : index + blackBoxLength;
        }

        private void DumpBlackBoxOnce(uint reasonFlags, NativeArray<VisorRefractionTelemetryEntry> blackBox, int blackBoxLength, int startIndex)
        {
            if (_blackBoxDumped || !blackBox.IsCreated || blackBoxLength <= 0)
                return;

            blackBoxLength = math.min(blackBoxLength, blackBox.Length);
            _blackBoxDumped = true;
            string path = Path.Combine(Application.dataPath, "..", BlackBoxDumpRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(BlackBoxMagic);
                    writer.Write(BlackBoxVersion);
                    writer.Write(reasonFlags);
                    writer.Write(BlackBoxEntrySizeBytes);
                    writer.Write(blackBoxLength);
                    int index = ResolveBlackBoxIndex(startIndex, blackBoxLength);
                    for (int i = 0; i < blackBoxLength; i++)
                    {
                        if (index >= blackBoxLength)
                            index = 0;

                        WriteTelemetryEntry(writer, blackBox[index]);
                        index++;
                    }
                }
            }
            catch (Exception)
            {
                _blackBoxDumped = true;
            }
        }

        private static void WriteTelemetryEntry(BinaryWriter writer, VisorRefractionTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.Flags);
            writer.Write(entry.EffectIntensity01);
            writer.Write(entry.Wetness01);
            writer.Write(entry.HullStress01);
            writer.Write(entry.WaterDensitySignal01);
            writer.Write(entry.HomeostasisFallback01);
            writer.Write(entry.LocalVelocitySq);
            writer.Write(entry.StateHash);
            writer.Write(entry.CameraPixelWidth);
            writer.Write(entry.CameraPixelHeight);
            writer.Write(entry.VaultGeneration);
            writer.Write(entry.QualityWeightQ16);
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
