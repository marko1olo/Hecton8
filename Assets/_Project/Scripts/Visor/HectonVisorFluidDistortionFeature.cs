using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    public sealed class HectonVisorFluidDistortionFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener, ILateFrameTickable
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
        private const int BlackBoxEntrySizeBytes = 64;
        private const int VisorFluidGlobalsStrideBytes = 128;
        private const int LensComputeGlobalsStrideBytes = 80;
        private const SystemID BlackBoxOwnerSystemId = SystemID.Vfx;
        private const uint BlackBoxMagic = 0x56535246u;
        private const uint BlackBoxVersion = 1u;
        private const uint BlackBoxFlagPlayerCamera = 1u << 0;
        private const uint BlackBoxFlagVisualActive = 1u << 1;
        private const uint BlackBoxFlagNonFiniteInput = 1u << 4;
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1335_VisorFluidRefraction.bin";

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

        private struct RuntimeState
        {
            public float Wetness;
            public float HullStress;
            public Vector3 LocalVelocity;
            public float AmbientLight01;
            public float EffectIntensity;
            public float RainIntensity;
            public float ThermalMotionCull01;
            public float WaterDensitySignal01;
            public float HomeostasisFallback01;
            public float QualityPressure01;
            public float VisualOverkill01;
            public float QualityWeight01;
            public Vector4 DiegeticLensState;
            public Vector4 DiegeticLensParams0;
            public Vector4 DiegeticLensParams1;
            public Vector4 DiegeticLensParams2;
            public uint TelemetryFlags;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct VisorRefractionTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint FrameIndex;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint Flags;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float EffectIntensity01;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float Wetness01;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float HullStress01;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float WaterDensitySignal01;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float HomeostasisFallback01;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public float LocalVelocitySq;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public uint StateHash;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public uint VaultGeneration;
            [System.Runtime.InteropServices.FieldOffset(40)]
            public uint QualityWeightQ16;
            [System.Runtime.InteropServices.FieldOffset(44)]
            public ushort CameraPixelWidth;
            [System.Runtime.InteropServices.FieldOffset(46)]
            public ushort CameraPixelHeight;
            [System.Runtime.InteropServices.FieldOffset(48)]
            public byte QualityPressureQ8;
            [System.Runtime.InteropServices.FieldOffset(49)]
            public byte HomeostasisFallbackQ8;
            [System.Runtime.InteropServices.FieldOffset(50)]
            public byte ThermalMotionCullQ8;
            [System.Runtime.InteropServices.FieldOffset(51)]
            public byte VisualOverkillQ8;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad15;
        }

        private sealed class VisorFluidPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal TextureHandle Opaque;
                internal TextureHandle LensMask;
                internal BufferHandle ConstantsBuffer;
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
            private uint _lensThreadGroupSizeX;
            private uint _lensThreadGroupSizeY;
            private bool _hasVisorFluidGlobals;
            private bool _supportsSetConstantBuffer;
            private bool _supportsComputeShaders;
            private const uint MaxKernelThreadProduct = 256u;
            private const int MaxDispatchGroupsPerDimension = 65535;

            public VisorFluidPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void SetGraphicsCapabilitiesCold(bool supportsSetConstantBuffer, bool supportsComputeShaders)
            {
                _supportsSetConstantBuffer = supportsSetConstantBuffer;
                _supportsComputeShaders = supportsComputeShaders;
                if (!_supportsSetConstantBuffer)
                {
                    Dispose();
                    return;
                }

                if (!_supportsComputeShaders)
                    ReleaseLensComputeGlobalsBuffer();
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
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonVisorFluidDistortion";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                bool lensMaskActive = TryAddDiegeticLensMaskPass(
                    renderGraph,
                    in sourceDesc,
                    out TextureHandle lensMaskTexture,
                    out float lensMaskBlend);

                if (!UpdateVisorFluidGlobals(_settings, _runtimeState, lensMaskActive, lensMaskBlend))
                    return;
                if (_activeVisorFluidGlobalsBuffer == null || !_activeVisorFluidGlobalsBuffer.IsValid())
                    return;

                BufferHandle globalsBuffer = renderGraph.ImportBuffer(_activeVisorFluidGlobalsBuffer);

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
                    passData.ConstantsBuffer = globalsBuffer;
                    passData.Material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    if (opaqueTexture.IsValid())
                        builder.UseTexture(opaqueTexture, AccessFlags.Read);
                    if (lensMaskActive)
                        builder.UseTexture(lensMaskTexture, AccessFlags.Read);
                    builder.UseBuffer(globalsBuffer, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        GraphicsBuffer constants = data.ConstantsBuffer;
                        if (constants == null || !constants.IsValid())
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraOpaqueTextureId, data.Opaque);
                        if (data.LensMaskActive)
                            context.cmd.SetGlobalTexture(ShaderConstants.DiegeticLensMaskTextureId, data.LensMask);
                        context.cmd.SetGlobalConstantBuffer(
                            constants,
                            ShaderConstants.VisorFluidGlobalsBufferId,
                            0,
                            VisorFluidGlobalsStrideBytes);
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
                bool computeReady = !_supportsComputeShaders || EnsureLensComputeGlobalsBuffer(allowAllocation: true);
                return fluidReady && computeReady;
            }

            private bool EnsureVisorFluidGlobalsBuffer(bool allowAllocation)
            {
                if (!_supportsSetConstantBuffer)
                {
                    Dispose();
                    return false;
                }

                if (HasVisorFluidGlobalsBuffer())
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

            private bool HasVisorFluidGlobalsBuffer()
            {
                return _supportsSetConstantBuffer &&
                    _visorFluidGlobalsBufferA != null && _visorFluidGlobalsBufferA.IsValid() &&
                    _visorFluidGlobalsBufferB != null && _visorFluidGlobalsBufferB.IsValid();
            }

            private bool UpdateVisorFluidGlobals(FeatureSettings settings, RuntimeState runtimeState, bool lensMaskActive, float lensMaskBlend)
            {
                if (!HasVisorFluidGlobalsBuffer())
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
                    return _activeVisorFluidGlobalsBuffer != null && _activeVisorFluidGlobalsBuffer.IsValid();
                }

                GraphicsBuffer writeBuffer = ResolveNextVisorFluidGlobalsBuffer();
                try
                {
                    NativeArray<VisorFluidGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<VisorFluidGlobalsDTO>(0, 1);
                    try
                    {
                        mapped[0] = globals;
                    }
                    finally
                    {
                        writeBuffer.UnlockBufferAfterWrite<VisorFluidGlobalsDTO>(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    MarkVisorFluidGlobalsUnavailable();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    MarkVisorFluidGlobalsUnavailable();
                    return false;
                }
                catch (ArgumentException)
                {
                    MarkVisorFluidGlobalsUnavailable();
                    return false;
                }
                catch (NotSupportedException)
                {
                    MarkVisorFluidGlobalsUnavailable();
                    return false;
                }
                _activeVisorFluidGlobalsBuffer = writeBuffer;
                _lastVisorFluidGlobals = globals;
                _hasVisorFluidGlobals = true;
                return _activeVisorFluidGlobalsBuffer != null && _activeVisorFluidGlobalsBuffer.IsValid();
            }

            private GraphicsBuffer ResolveNextVisorFluidGlobalsBuffer()
            {
                _visorFluidGlobalsWriteIndex ^= 1;
                return _visorFluidGlobalsWriteIndex == 0 ? _visorFluidGlobalsBufferA : _visorFluidGlobalsBufferB;
            }

            private void MarkVisorFluidGlobalsUnavailable()
            {
                _activeVisorFluidGlobalsBuffer = null;
                _hasVisorFluidGlobals = false;
            }

            private bool EnsureLensComputeGlobalsBuffer(bool allowAllocation)
            {
                if (!_supportsSetConstantBuffer || !_supportsComputeShaders)
                {
                    ReleaseLensComputeGlobalsBuffer();
                    return false;
                }

                if (HasLensComputeGlobalsBuffer())
                    return true;

                if (!allowAllocation)
                    return false;

                ReleaseLensComputeGlobalsBuffer();
                // COLD ALLOC: GraphicsBuffer[2] - ping-pong diegetic visor compute CBuffers - owner: HectonVisorFluidDistortionFeature
                _lensComputeGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, LensComputeGlobalsStrideBytes);
                _lensComputeGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, LensComputeGlobalsStrideBytes);
                return _lensComputeGlobalsBufferA.IsValid() && _lensComputeGlobalsBufferB.IsValid();
            }

            private bool HasLensComputeGlobalsBuffer()
            {
                return _supportsSetConstantBuffer &&
                    _supportsComputeShaders &&
                    _lensComputeGlobalsBufferA != null && _lensComputeGlobalsBufferA.IsValid() &&
                    _lensComputeGlobalsBufferB != null && _lensComputeGlobalsBufferB.IsValid();
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
                if (!HasLensComputeGlobalsBuffer())
                    return false;

                LensComputeGlobalsDTO globals = new LensComputeGlobalsDTO(
                    runtimeState.DiegeticLensState,
                    runtimeState.DiegeticLensParams0,
                    runtimeState.DiegeticLensParams1,
                    runtimeState.DiegeticLensParams2,
                    new Vector4(
                        (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
                        Sanitize01(lensMaskBlend),
                        Sanitize01(runtimeState.QualityPressure01),
                        Sanitize01(runtimeState.VisualOverkill01)));

                GraphicsBuffer writeBuffer = ResolveNextLensComputeGlobalsBuffer();
                try
                {
                    NativeArray<LensComputeGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<LensComputeGlobalsDTO>(0, 1);
                    try
                    {
                        mapped[0] = globals;
                    }
                    finally
                    {
                        writeBuffer.UnlockBufferAfterWrite<LensComputeGlobalsDTO>(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    ReleaseLensComputeGlobalsBuffer();
                    return false;
                }
                catch (InvalidOperationException)
                {
                    ReleaseLensComputeGlobalsBuffer();
                    return false;
                }
                catch (ArgumentException)
                {
                    ReleaseLensComputeGlobalsBuffer();
                    return false;
                }
                catch (NotSupportedException)
                {
                    ReleaseLensComputeGlobalsBuffer();
                    return false;
                }
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
                _lensThreadGroupSizeX = 0u;
                _lensThreadGroupSizeY = 0u;
                int resolvedKernel = -1;
                uint x = 0u;
                uint y = 0u;
                uint z = 0u;
                try
                {
                    if (_lensComputeShader == null ||
                        !_lensComputeShader.HasKernel("ResolveDiegeticVisorLensMask"))
                        return;

                    resolvedKernel = _lensComputeShader.FindKernel("ResolveDiegeticVisorLensMask");
                    if (resolvedKernel < 0)
                        return;

                    if (!_lensComputeShader.IsSupported(resolvedKernel))
                        return;

                    _lensComputeShader.GetKernelThreadGroupSizes(resolvedKernel, out x, out y, out z);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                catch (ArgumentException)
                {
                    return;
                }
                catch (MissingReferenceException)
                {
                    return;
                }
                catch (UnityException)
                {
                    return;
                }

                ulong threadProduct = (ulong)x * y * z;
                if (x == 0u || y == 0u || z != 1u || threadProduct == 0UL || threadProduct > MaxKernelThreadProduct)
                    return;

                _lensKernelIndex = resolvedKernel;
                _lensThreadGroupSizeX = x;
                _lensThreadGroupSizeY = y;
            }

            private bool TryAddDiegeticLensMaskPass(RenderGraph renderGraph, in TextureDesc sourceDesc, out TextureHandle lensMaskTexture, out float lensMaskBlend)
            {
                lensMaskTexture = default;
                lensMaskBlend = ResolveLensMaskBlend(in _runtimeState);
                if (!_supportsComputeShaders ||
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
                int dispatchX = ResolveDispatchGroups(maskWidth, _lensThreadGroupSizeX);
                int dispatchY = ResolveDispatchGroups(maskHeight, _lensThreadGroupSizeY);
                if (dispatchX <= 0 || dispatchY <= 0)
                    return false;

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
                    passData.DispatchX = dispatchX;
                    passData.DispatchY = dispatchY;

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

            private static int ResolveDispatchGroups(int dimension, uint threadGroupSize)
            {
                if (dimension <= 0 || threadGroupSize == 0u)
                    return 0;

                long groups = ((long)dimension + threadGroupSize - 1L) / threadGroupSize;
                return groups > 0L && groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
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
        private Vector4 _cachedDiegeticLensState;
        private Vector4 _cachedDiegeticLensParams0;
        private Vector4 _cachedDiegeticLensParams1;
        private Vector4 _cachedDiegeticLensParams2;
        private float _cachedRainIntensity;
        private float _cachedWaterDensitySignal;
        private float _cachedAmbientLight01;
        private int _cachedGraphicsMemoryMb;
        private uint _blackBoxVaultGeneration;
        private uint _cachedPresentationTelemetryFlags;
        private bool _blackBoxDumped;
        private bool _blackBoxHotSwapRegistered;
        private bool _supportsSetConstantBuffer;
        private bool _supportsComputeShaders;
        private bool _lateFrameRegistered;

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            TryRegisterLateFrameTickable();
            TryRegisterBlackBoxHotSwapListener();
            BindBlackBoxVaultForLifecycle(GlobalRegistry.DataVault);
            EnsureBlackBoxLeaseCold();
            CacheRenderDependenciesCold();
            CachePresentationGlobalsLate();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrameTickable();
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
            CacheGraphicsCapabilitiesCold();
            _pass.PrewarmVisorFluidGlobalsBuffer();
            TryRegisterLateFrameTickable();
            TryRegisterBlackBoxHotSwapListener();
            BindBlackBoxVaultForLifecycle(GlobalRegistry.DataVault);
            EnsureBlackBoxLeaseCold();
            CacheRenderDependenciesCold();
            CachePresentationGlobalsLate();
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
            TryUnregisterLateFrameTickable();
            TryUnregisterBlackBoxHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault nextVault = currentService is IDataVault vault ? vault : null;
                BindBlackBoxVaultForLifecycle(nextVault);
                EnsureBlackBoxLeaseCold();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidSimulation)
            {
                _fluidSimulation = currentService as IFluidSim;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterLateFrameTickable();
                if (currentService != null)
                    TryRegisterLateFrameTickable();
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CachePresentationGlobalsLate();
        }

        private bool TryBuildRuntimeState(
            Camera renderCamera,
            FeatureSettings settings,
            out RuntimeState runtimeState)
        {
            runtimeState = default;
            uint telemetryFlags = _cachedPresentationTelemetryFlags;
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
            Vector4 rawLensState = _cachedDiegeticLensState;
            Vector4 rawLensParams0 = _cachedDiegeticLensParams0;
            Vector4 rawLensParams1 = _cachedDiegeticLensParams1;
            Vector4 rawLensParams2 = _cachedDiegeticLensParams2;
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
                float rawAmbientLight = _cachedAmbientLight01;
                FlagIfNonFinite(rawAmbientLight, ref telemetryFlags);
                ambientLight01 = Sanitize01(rawAmbientLight);
                dustContribution = math.saturate(ambientLight01 * dustStrength * ambientDustResponse);
            }
            dustContribution = math.saturate(math.max(dustContribution, lensDirt * (0.18f + ambientLight01 * 0.82f)));

            float hullContribution = math.saturate(
                math.saturate((hullStress - HullStressVisorContributionStart01) * HullStressVisorContributionInvRange) *
                Sanitize01(settings.hullStressContribution));
            float effectIntensity = math.saturate(math.max(math.max(math.max(wetness, hullContribution), dustContribution), lensContribution));
            float rawRainIntensity = _cachedRainIntensity;
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
            runtimeState = default;
            runtimeState.Wetness = wetness;
            runtimeState.HullStress = hullStress;
            runtimeState.LocalVelocity = localVelocity;
            runtimeState.AmbientLight01 = ambientLight01;
            runtimeState.EffectIntensity = effectIntensity;
            runtimeState.RainIntensity = rainIntensity;
            runtimeState.ThermalMotionCull01 = thermalMotionCull01;
            runtimeState.WaterDensitySignal01 = waterDensitySignal01;
            runtimeState.HomeostasisFallback01 = homeostasisFallback01;
            runtimeState.QualityPressure01 = qualityPressure01;
            runtimeState.VisualOverkill01 = visualOverkill01;
            runtimeState.QualityWeight01 = globalQualityWeight;
            runtimeState.DiegeticLensState = sanitizedLensState;
            runtimeState.DiegeticLensParams0 = sanitizedLensParams0;
            runtimeState.DiegeticLensParams1 = sanitizedLensParams1;
            runtimeState.DiegeticLensParams2 = sanitizedLensParams2;
            runtimeState.TelemetryFlags = telemetryFlags;
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

        private float ResolveHardwareQualityPressure01(FeatureSettings settings)
        {
            int thresholdMb = settings != null ? math.max(256, settings.minimumQualityVideoMemoryMb) : 2048;
            float graphicsMemoryMb = _cachedGraphicsMemoryMb;
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
            return context != null && context.PlayerCamera != null ? context : null;
        }

        private IFluidSim ResolveFluidSimulation()
        {
            IFluidSim fluidSimulation = _fluidSimulation;
            return fluidSimulation != null && fluidSimulation.IsReady ? fluidSimulation : null;
        }

        private void CacheRenderDependenciesCold()
        {
            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;

            if (_fluidSimulation == null)
                _fluidSimulation = GlobalRegistry.FluidSimulation;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
            _supportsComputeShaders = SystemInfo.supportsComputeShaders;
            _cachedGraphicsMemoryMb = SystemInfo.graphicsMemorySize;
            _pass?.SetGraphicsCapabilitiesCold(_supportsSetConstantBuffer, _supportsComputeShaders);
        }

        private void CachePresentationGlobalsLate()
        {
            uint telemetryFlags = 0u;
            _cachedDiegeticLensState = Shader.GetGlobalVector(ShaderConstants.DiegeticLensStateId);
            _cachedDiegeticLensParams0 = Shader.GetGlobalVector(ShaderConstants.DiegeticLensParams0Id);
            _cachedDiegeticLensParams1 = Shader.GetGlobalVector(ShaderConstants.DiegeticLensParams1Id);
            _cachedDiegeticLensParams2 = Shader.GetGlobalVector(ShaderConstants.DiegeticLensParams2Id);
            _cachedRainIntensity = Shader.GetGlobalFloat(ShaderConstants.RainIntensityId);
            _cachedWaterDensitySignal = Shader.GetGlobalFloat(ShaderConstants.WaterDensitySignalId);
            _cachedAmbientLight01 = ResolveAmbientLight01();

            FlagIfNonFinite(_cachedDiegeticLensState, ref telemetryFlags);
            FlagIfNonFinite(_cachedDiegeticLensParams0, ref telemetryFlags);
            FlagIfNonFinite(_cachedDiegeticLensParams1, ref telemetryFlags);
            FlagIfNonFinite(_cachedDiegeticLensParams2, ref telemetryFlags);
            FlagIfNonFinite(_cachedRainIntensity, ref telemetryFlags);
            FlagIfNonFinite(_cachedWaterDensitySignal, ref telemetryFlags);
            FlagIfNonFinite(_cachedAmbientLight01, ref telemetryFlags);
            _cachedPresentationTelemetryFlags = telemetryFlags;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_lateFrameRegistered ||
                !Application.isPlaying ||
                GlobalRegistry.Dispatcher == null)
            {
                return;
            }

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _lateFrameRegistered = false;
        }

        private float ResolveWaterDensitySignal01(ref uint telemetryFlags)
        {
            float globalSignal = _cachedWaterDensitySignal;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (!TryEnsureBlackBoxLease())
                return;

            Vector3 localVelocity = SanitizeVector(runtimeState.LocalVelocity);
            float localVelocitySq =
                localVelocity.x * localVelocity.x +
                localVelocity.y * localVelocity.y +
                localVelocity.z * localVelocity.z;
            uint flags = BlackBoxFlagPlayerCamera | runtimeState.TelemetryFlags;
            if (runtimeState.EffectIntensity > 0.001f || runtimeState.RainIntensity > 0.001f)
                flags |= BlackBoxFlagVisualActive;

            VisorRefractionTelemetryEntry entry = default;
            entry.FrameIndex = frame >= 0 ? (uint)frame : 0u;
            entry.Flags = flags;
            entry.EffectIntensity01 = Sanitize01(runtimeState.EffectIntensity);
            entry.Wetness01 = Sanitize01(runtimeState.Wetness);
            entry.HullStress01 = Sanitize01(runtimeState.HullStress);
            entry.WaterDensitySignal01 = Sanitize01(runtimeState.WaterDensitySignal01);
            entry.HomeostasisFallback01 = Sanitize01(runtimeState.HomeostasisFallback01);
            entry.LocalVelocitySq = SanitizeNonNegative(localVelocitySq);
            entry.StateHash = BuildBlackBoxHash(in runtimeState, flags);
            entry.CameraPixelWidth = ClampUShort(renderCamera != null ? renderCamera.pixelWidth : 0);
            entry.CameraPixelHeight = ClampUShort(renderCamera != null ? renderCamera.pixelHeight : 0);
            entry.VaultGeneration = _blackBoxVaultGeneration;
            entry.QualityWeightQ16 = EncodeQualityQ16(runtimeState.QualityWeight01);
            entry.QualityPressureQ8 = EncodeUnitQ8(runtimeState.QualityPressure01);
            entry.HomeostasisFallbackQ8 = EncodeUnitQ8(runtimeState.HomeostasisFallback01);
            entry.ThermalMotionCullQ8 = EncodeUnitQ8(runtimeState.ThermalMotionCull01);
            entry.VisualOverkillQ8 = EncodeUnitQ8(runtimeState.VisualOverkill01);

            if (!TryWriteBlackBoxEntry(frame, in entry, out int blackBoxLength))
                return;

            if ((flags & BlackBoxFlagNonFiniteInput) != 0u)
                DumpBlackBoxOnce(flags, blackBoxLength, ResolveBlackBoxIndex(frame + 1, blackBoxLength));
        }

        private bool TryWriteBlackBoxEntry(int frame, in VisorRefractionTelemetryEntry sourceEntry, out int blackBoxLength)
        {
            blackBoxLength = 0;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || !IsBlackBoxHandle(in _blackBoxHandle))
                return false;

            bool blackBoxLocked = false;
            bool clearDescriptor = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _blackBoxHandle, BlackBoxOwnerSystemId, out NativeArray<VisorRefractionTelemetryEntry> blackBox))
                    return false;

                blackBoxLocked = true;
                if (vault.IsCompactionFenceActive || !blackBox.IsCreated || blackBox.Length < BlackBoxFrameCount)
                {
                    clearDescriptor = true;
                    return false;
                }

                blackBoxLength = blackBox.Length;
                VisorRefractionTelemetryEntry entry = sourceEntry;
                entry.VaultGeneration = _blackBoxHandle.Generation;
                _blackBoxVaultGeneration = _blackBoxHandle.Generation;
                blackBox[ResolveBlackBoxIndex(frame, blackBoxLength)] = entry;
                return true;
            }
            finally
            {
                if (blackBoxLocked)
                    vault.ReleaseWriteLock(in _blackBoxHandle, BlackBoxOwnerSystemId);
                if (clearDescriptor)
                    ClearBlackBoxDescriptor();
            }
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
                IsBlackBoxHandle(in existingHandle) &&
                !vault.IsCompactionFenceActive &&
                vault.TryReadOnlyHandle(in existingHandle, out NativeArray<VisorRefractionTelemetryEntry>.ReadOnly existingBlackBox) &&
                !vault.IsCompactionFenceActive &&
                existingBlackBox.IsCreated &&
                existingBlackBox.Length >= BlackBoxFrameCount)
            {
                _blackBoxHandle = existingHandle;
                _blackBoxVaultGeneration = existingHandle.Generation;
                return true;
            }

            ClearBlackBoxDescriptor();
            return false;
        }

        private bool EnsureBlackBoxLeaseCold()
        {
            if (TryEnsureBlackBoxLease())
                return true;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
            {
                ClearBlackBoxDescriptor();
                return false;
            }

            VaultGenerationHandle<VisorRefractionTelemetryEntry> blackBoxHandle = vault.EnsureGenerationHandle<VisorRefractionTelemetryEntry>(
                BufferID.VisorRefractionBlackBox,
                BlackBoxFrameCount,
                SystemID.Vfx);
            if (!IsBlackBoxHandle(in blackBoxHandle) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in blackBoxHandle, out NativeArray<VisorRefractionTelemetryEntry>.ReadOnly blackBox) ||
                vault.IsCompactionFenceActive ||
                !blackBox.IsCreated ||
                blackBox.Length < BlackBoxFrameCount)
            {
                ClearBlackBoxDescriptor();
                return false;
            }

            _blackBoxHandle = blackBoxHandle;
            _blackBoxVaultGeneration = blackBoxHandle.Generation;
            return true;
        }

        private bool TryResolveCurrentBlackBoxRing(out NativeArray<VisorRefractionTelemetryEntry>.ReadOnly blackBox, out int blackBoxLength)
        {
            blackBox = default;
            blackBoxLength = 0;
            if (_dataVault == null ||
                !IsBlackBoxHandle(in _blackBoxHandle) ||
                _dataVault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!_dataVault.TryReadOnlyHandle(in _blackBoxHandle, out blackBox) ||
                _dataVault.IsCompactionFenceActive ||
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

        private void BindBlackBoxVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseBlackBoxLease();
            _dataVault = vault;
            ResetBlackBoxNativeEpochState();
        }

        private void ReleaseBlackBoxLease()
        {
            // Renderer features are secondary DataVault consumers. URP disposal can run while the
            // vault arena is resetting, so this lifecycle path detaches handles without freeing
            // vault-owned storage from inside RenderPipeline cleanup.
            ClearBlackBoxDescriptor();
            _dataVault = null;
        }

        private void ClearBlackBoxDescriptor()
        {
            _blackBoxHandle = default;
            _blackBoxVaultGeneration = 0u;
        }

        private void ResetBlackBoxNativeEpochState()
        {
            ClearBlackBoxDescriptor();
            _blackBoxDumped = false;
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

        private static bool IsBlackBoxHandle(in VaultGenerationHandle<VisorRefractionTelemetryEntry> handle)
        {
            return handle.BufferID == (uint)BufferID.VisorRefractionBlackBox &&
                   handle.SystemID == (uint)BlackBoxOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static uint BuildBlackBoxHash(in RuntimeState runtimeState, uint flags)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, flags);
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.EffectIntensity)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.Wetness)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.HullStress)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.WaterDensitySignal01)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.HomeostasisFallback01)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.QualityPressure01)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.ThermalMotionCull01)));
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

        private static byte EncodeUnitQ8(float value01)
        {
            return (byte)math.round(Sanitize01(value01) * 255f);
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

        private bool TryReadBlackBoxEntry(int index, out VisorRefractionTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || !IsBlackBoxHandle(in _blackBoxHandle) || index < 0)
                return false;

            if (!vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<VisorRefractionTelemetryEntry>.ReadOnly blackBox) ||
                vault.IsCompactionFenceActive ||
                !blackBox.IsCreated ||
                blackBox.Length < BlackBoxFrameCount ||
                index >= blackBox.Length)
            {
                return false;
            }

            entry = blackBox[index];
            return !vault.IsCompactionFenceActive;
        }

        private unsafe void DumpBlackBoxOnce(uint reasonFlags, int blackBoxLength, int startIndex)
        {
            if (_blackBoxDumped || blackBoxLength <= 0)
                return;

            blackBoxLength = math.min(blackBoxLength, BlackBoxFrameCount);

            NativeArray<byte> payload = default;
            try
            {
                string path = Path.Combine(Application.dataPath, "..", BlackBoxDumpRelativePath);
                int totalBytes = 20 + blackBoxLength * BlackBoxEntrySizeBytes;
                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);

                Span<byte> header = new Span<byte>(payloadPtr, 20);
                WriteBlackBoxHeader(header, reasonFlags, blackBoxLength);

                int offset = 20;
                int index = ResolveBlackBoxIndex(startIndex, blackBoxLength);
                for (int i = 0; i < blackBoxLength; i++)
                {
                    if (index >= blackBoxLength)
                        index = 0;

                    if (!TryReadBlackBoxEntry(index, out VisorRefractionTelemetryEntry entry))
                        return;

                    Span<byte> entryBytes = new Span<byte>(payloadPtr + offset, BlackBoxEntrySizeBytes);
                    WriteTelemetryEntry(entryBytes, in entry);
                    offset += BlackBoxEntrySizeBytes;
                    index++;
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }
            finally
            {
                if (payload.IsCreated)
                    payload.Dispose();
            }
        }

        private static void WriteBlackBoxHeader(Span<byte> destination, uint reasonFlags, int entryCount)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), BlackBoxMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), BlackBoxVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), reasonFlags);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12, 4), BlackBoxEntrySizeBytes);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(16, 4), entryCount);
        }

        private static void WriteTelemetryEntry(Span<byte> destination, in VisorRefractionTelemetryEntry entry)
        {
            destination.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.FrameIndex);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Flags);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.EffectIntensity01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.Wetness01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.HullStress01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.WaterDensitySignal01);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.HomeostasisFallback01);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.LocalVelocitySq);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(32, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), entry.VaultGeneration);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(40, 4), entry.QualityWeightQ16);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(44, 2), entry.CameraPixelWidth);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(46, 2), entry.CameraPixelHeight);
            destination[48] = entry.QualityPressureQ8;
            destination[49] = entry.HomeostasisFallbackQ8;
            destination[50] = entry.ThermalMotionCullQ8;
            destination[51] = entry.VisualOverkillQ8;
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
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
