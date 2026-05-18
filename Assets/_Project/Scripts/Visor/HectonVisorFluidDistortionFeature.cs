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

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Fullscreen visor droplet and leak distortion driven by the active player wet-lens and hull-stress signals.
    /// </summary>
    public sealed class HectonVisorFluidDistortionFeature : ScriptableRendererFeature
    {
        private const float ThermalDistortionCullSpeedMetersPerSecond = 15f;
        private const float ThermalDistortionCullSpeedMetersPerSecondSq = ThermalDistortionCullSpeedMetersPerSecond * ThermalDistortionCullSpeedMetersPerSecond;
        private const float HullStressVisorContributionStart01 = 0.65f;
        private const float HullStressVisorContributionInvRange = 1f / (1f - HullStressVisorContributionStart01);
        private const float VisorSpeedSquaredToShader01 = 0.0016f;
        private const float QuaternionMinimumLengthSq = 0.000001f;
        private const float QuaternionUnitLengthSqEpsilon = 0.015625f;
        private const int BlackBoxFrameCount = 300;
        private const int BlackBoxEntrySizeBytes = 48;
        private const int VisorFluidGlobalsStrideBytes = 128;
        private const uint BlackBoxMagic = 0x56535246u;
        private const uint BlackBoxVersion = 1u;
        private const uint BlackBoxFlagPlayerCamera = 1u << 0;
        private const uint BlackBoxFlagVisualActive = 1u << 1;
        private const uint BlackBoxFlagLowTier = 1u << 2;
        private const uint BlackBoxFlagHomeostasisFallback = 1u << 3;
        private const uint BlackBoxFlagNonFiniteInput = 1u << 4;
        private const uint BlackBoxFlagThermalMotionCull = 1u << 5;
        private const uint BlackBoxFlagVisualOverkill = 1u << 6;
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SCREEN_SPACE_REFRACTION.bin";

#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader";
#endif

        [Serializable]
        private sealed class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for procedural visor droplets and hull-stress leaks.")]
            public Shader shader = null;

            [Tooltip("Injection point for the visor distortion. Before post-processing keeps the effect inside the validated noir stack.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Maximum UV refraction applied by the procedural fluid mask.")]
            [Range(0f, 0.04f)] public float distortionStrength = 0.012f;

            [Tooltip("Snell approximation strength. Low tier drops to chromatic-only sampling.")]
            [Range(0f, 0.04f)] public float snellStrength = 0.014f;

            [Tooltip("Air, seawater, dense water, and visor glass IOR values consumed as a compact LUT.")]
            public Vector4 refractionIndexLut = new Vector4(1.0003f, 1.333f, 1.38f, 1.46f);

            [Tooltip("Depth softness in metres used to fade refraction near foreground occluders.")]
            [Range(0.005f, 0.5f)] public float depthSoftnessMeters = 0.08f;

            [Tooltip("Hull stress above this value degrades to the cheap chromatic fallback.")]
            [Range(0f, 1f)] public float stressFallbackThreshold = 0.82f;

            [Tooltip("Graphics memory at or below this value uses the MX350 chromatic-only path.")]
            [Min(256)] public int lowTierVideoMemoryMb = 2048;

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

            [Tooltip("High/Ultra-only salt crystal growth and fine caustic glint. Forced off on low-tier hardware.")]
            [Range(0f, 1f)] public float visualOverkillStrength = 1f;
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
                float lowTierWeight01,
                float visualOverkill01,
                HectonQualityTier qualityTier,
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
                LowTierWeight01 = lowTierWeight01;
                VisualOverkill01 = visualOverkill01;
                QualityTier = qualityTier;
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
            public readonly float LowTierWeight01;
            public readonly float VisualOverkill01;
            public readonly HectonQualityTier QualityTier;
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
            [FieldOffset(44)] public uint QualityTier;
        }

        private sealed class VisorFluidPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal TextureHandle Opaque;
                internal Material Material;
            }

            private const float GlobalsFloatEpsilon = 0.0001f;

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Fluid Distortion");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private GraphicsBuffer _visorFluidGlobalsBufferA;
            private GraphicsBuffer _visorFluidGlobalsBufferB;
            private GraphicsBuffer _activeVisorFluidGlobalsBuffer;
            private VisorFluidGlobalsDTO _lastVisorFluidGlobals;
            private int _visorFluidGlobalsWriteIndex;
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
                _runtimeState = runtimeState;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
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

                if (!UpdateVisorFluidGlobals(_settings, _runtimeState))
                    return;

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Hecton Visor Fluid Distortion",
                           out PassData passData,
                           _profilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Depth = depthTexture;
                    passData.Opaque = opaqueTexture.IsValid() ? opaqueTexture : sourceTexture;
                    passData.Material = _material;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    if (opaqueTexture.IsValid())
                        builder.UseTexture(opaqueTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        context.cmd.SetGlobalTexture(ShaderConstants.CameraOpaqueTextureId, data.Opaque);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            public void Dispose()
            {
                _visorFluidGlobalsBufferA?.Release();
                _visorFluidGlobalsBufferB?.Release();
                _visorFluidGlobalsBufferA = null;
                _visorFluidGlobalsBufferB = null;
                _activeVisorFluidGlobalsBuffer = null;
                _hasVisorFluidGlobals = false;
            }

            public bool PrewarmVisorFluidGlobalsBuffer()
            {
                return EnsureVisorFluidGlobalsBuffer(allowAllocation: true);
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

            private bool UpdateVisorFluidGlobals(FeatureSettings settings, RuntimeState runtimeState)
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
                        Sanitize01(runtimeState.LowTierWeight01),
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
                        0f,
                        0f));
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

            [StructLayout(LayoutKind.Sequential, Size = VisorFluidGlobalsStrideBytes)]
            private struct VisorFluidGlobalsDTO
            {
                public Vector4 Params0;
                public Vector4 Params1;
                public Vector4 Params2;
                public Vector4 IorLut;
                public Vector4 Params3;
                public Vector4 Params4;
                public Vector4 LocalVelocity;
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
        private VaultBufferHandle<VisorRefractionTelemetryEntry> _blackBoxHandle;
        private uint _blackBoxVaultGeneration;
        private bool _blackBoxDumped;

        /// <inheritdoc />
        public override void Create()
        {
#if UNITY_EDITOR
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            _pass ??= new VisorFluidPass();
            _pass.PrewarmVisorFluidGlobalsBuffer();
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
            InvalidateBlackBoxLease();
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
                localVelocity.x += SanitizeSigned(rawLensParams0.x, -1f, 1f) * 6f;
                localVelocity.y += SanitizeSigned(rawLensParams0.y, -1f, 1f) * 6f;
            }

            float localVelocitySq =
                localVelocity.x * localVelocity.x +
                localVelocity.y * localVelocity.y +
                localVelocity.z * localVelocity.z;
            float thermalMotionCull01 = localVelocitySq > ThermalDistortionCullSpeedMetersPerSecondSq ? 1f : 0f;
            HectonQualityTier qualityTier = GlobalRegistry.ScalabilityTier;
            bool lowTier = ResolveLowTier(settings, qualityTier);
            float waterDensitySignal01 = ResolveWaterDensitySignal01(ref telemetryFlags);
            float globalQualityWeight = ResolveGlobalQualityWeight();
            float qualityLowPressure01 = 1f - Smooth01((globalQualityWeight - 0.18f) * (1f / 0.12f));
            float hardwareLowPressure01 = lowTier ? 1f : 0f;
            float lensLowPressure01 = lensContribution > 0.001f ? 1f - lensRefractionScale : 0f;
            float stressFallback01 = Smooth01((hullStress - Sanitize01(settings.stressFallbackThreshold)) * 5f);
            float lowTierWeight01 = math.saturate(math.max(math.max(qualityLowPressure01, hardwareLowPressure01), lensLowPressure01));
            float homeostasisFallback01 = math.saturate(math.max(lowTierWeight01, stressFallback01));
            float visualOverkill01 = ResolveVisualOverkill01(settings, qualityTier, lowTierWeight01);
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
                lowTierWeight01,
                visualOverkill01,
                qualityTier,
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

        private static bool ResolveLowTier(FeatureSettings settings, HectonQualityTier qualityTier)
        {
            if (qualityTier == HectonQualityTier.Low || qualityTier == HectonQualityTier.Mx350)
                return true;

            int thresholdMb = settings != null ? math.max(256, settings.lowTierVideoMemoryMb) : 2048;
            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            return graphicsMemoryMb > 0 && graphicsMemoryMb <= thresholdMb;
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

        private static float ResolveVisualOverkill01(FeatureSettings settings, HectonQualityTier qualityTier, float lowTierWeight01)
        {
            float configuredStrength = settings != null ? Sanitize01(settings.visualOverkillStrength) : 0f;
            float tierScale;
            switch (qualityTier)
            {
                case HectonQualityTier.Ultra:
                    tierScale = 1f;
                    break;
                case HectonQualityTier.High:
                    tierScale = 0.72f;
                    break;
                case HectonQualityTier.Mid:
                    tierScale = 0.24f;
                    break;
                default:
                    tierScale = 0f;
                    break;
            }

            return configuredStrength * tierScale * (1f - Sanitize01(lowTierWeight01));
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

        private unsafe void WriteBlackBoxFrame(Camera renderCamera, in RuntimeState runtimeState)
        {
            int frame = Time.frameCount;
            if (!TryResolveBlackBoxPointer(out VisorRefractionTelemetryEntry* blackBox, out int blackBoxLength))
                return;

            Vector3 localVelocity = SanitizeVector(runtimeState.LocalVelocity);
            float localVelocitySq =
                localVelocity.x * localVelocity.x +
                localVelocity.y * localVelocity.y +
                localVelocity.z * localVelocity.z;
            uint flags = BlackBoxFlagPlayerCamera | runtimeState.TelemetryFlags;
            if (runtimeState.EffectIntensity > 0.001f || runtimeState.RainIntensity > 0.001f)
                flags |= BlackBoxFlagVisualActive;
            if (runtimeState.LowTierWeight01 >= 0.5f)
                flags |= BlackBoxFlagLowTier;
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
                QualityTier = (uint)runtimeState.QualityTier
            };

            if ((flags & BlackBoxFlagNonFiniteInput) != 0u)
                DumpBlackBoxOnce(flags, blackBox, blackBoxLength, ResolveBlackBoxIndex(frame + 1, blackBoxLength));
        }

        private bool TryEnsureBlackBoxLease()
        {
            if (IsBlackBoxLeaseValid())
                return true;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                InvalidateBlackBoxLease();
                return false;
            }

            VaultBufferHandle<VisorRefractionTelemetryEntry> blackBoxHandle = vault.GetBufferHandle<VisorRefractionTelemetryEntry>(
                BufferID.VisorRefractionBlackBox,
                BlackBoxFrameCount,
                SystemID.Vfx);
            if (!blackBoxHandle.IsCreated ||
                blackBoxHandle.Length < BlackBoxFrameCount ||
                !vault.TryGetBufferGeneration(BufferID.VisorRefractionBlackBox, out uint generation) ||
                generation != blackBoxHandle.GenerationID)
            {
                InvalidateBlackBoxLease();
                return false;
            }

            _dataVault = vault;
            _blackBoxHandle = blackBoxHandle;
            _blackBoxVaultGeneration = generation;
            return true;
        }

        private unsafe bool TryResolveBlackBoxPointer(out VisorRefractionTelemetryEntry* blackBox, out int blackBoxLength)
        {
            blackBox = null;
            blackBoxLength = 0;
            if (!TryEnsureBlackBoxLease())
                return false;

            void* ptr = _blackBoxHandle.ResolvePointer(_dataVault);
            if (ptr == null || !_blackBoxHandle.IsCreated || _blackBoxHandle.Length < BlackBoxFrameCount)
            {
                InvalidateBlackBoxLease();
                return false;
            }

            blackBox = (VisorRefractionTelemetryEntry*)ptr;
            blackBoxLength = _blackBoxHandle.Length;
            return true;
        }

        private bool IsBlackBoxLeaseValid()
        {
            if (_dataVault == null ||
                !_blackBoxHandle.IsCreated ||
                _blackBoxHandle.Length < BlackBoxFrameCount ||
                _dataVault.IsCompactionFenceActive)
            {
                return false;
            }

            return _dataVault.TryGetBufferGeneration(BufferID.VisorRefractionBlackBox, out uint generation) &&
                   generation == _blackBoxVaultGeneration &&
                   ReferenceEquals(_dataVault, GlobalRegistry.DataVault);
        }

        private void InvalidateBlackBoxLease()
        {
            _dataVault = null;
            _blackBoxHandle = default;
            _blackBoxVaultGeneration = 0u;
        }

        private static uint BuildBlackBoxHash(in RuntimeState runtimeState, uint flags)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, flags);
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.EffectIntensity)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.Wetness)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.HullStress)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.WaterDensitySignal01)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.LowTierWeight01)));
            hash = MixHash(hash, math.asuint(Sanitize01(runtimeState.VisualOverkill01)));
            Vector3 velocity = SanitizeVector(runtimeState.LocalVelocity);
            hash = MixHash(hash, math.asuint(velocity.x));
            hash = MixHash(hash, math.asuint(velocity.y));
            hash = MixHash(hash, math.asuint(velocity.z));
            hash = MixHash(hash, (uint)runtimeState.QualityTier);
            return hash;
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

        private unsafe void DumpBlackBoxOnce(uint reasonFlags, VisorRefractionTelemetryEntry* blackBox, int blackBoxLength, int startIndex)
        {
            if (_blackBoxDumped || blackBox == null || blackBoxLength <= 0)
                return;

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
            writer.Write(entry.QualityTier);
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
