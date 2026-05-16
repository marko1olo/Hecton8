using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
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
                bool lowTier,
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
                LowTier = lowTier;
                VisualOverkill01 = visualOverkill01;
                QualityTier = qualityTier;
                TelemetryFlags = telemetryFlags;
            }

            public float Wetness { get; }
            public float HullStress { get; }
            public Vector3 LocalVelocity { get; }
            public float AmbientLight01 { get; }
            public float EffectIntensity { get; }
            public float RainIntensity { get; }
            public float ThermalMotionCull01 { get; }
            public float WaterDensitySignal01 { get; }
            public float HomeostasisFallback01 { get; }
            public bool LowTier { get; }
            public float VisualOverkill01 { get; }
            public HectonQualityTier QualityTier { get; }
            public uint TelemetryFlags { get; }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = BlackBoxEntrySizeBytes)]
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

            private const float MaterialFloatEpsilon = 0.0001f;

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Fluid Distortion");
            private FeatureSettings _settings;
            private Material _material;
            private RuntimeState _runtimeState;
            private Material _lastParameterMaterial;
            private Vector4 _lastLocalVelocityShader = Vector4.positiveInfinity;
            private float _lastIntensity = float.PositiveInfinity;
            private float _lastRainIntensity = float.PositiveInfinity;
            private float _lastWetness = float.PositiveInfinity;
            private float _lastHullStress = float.PositiveInfinity;
            private float _lastDistortionStrength = float.PositiveInfinity;
            private float _lastRunoffSpeed = float.PositiveInfinity;
            private float _lastDropletScale = float.PositiveInfinity;
            private float _lastLateralStreakStrength = float.PositiveInfinity;
            private float _lastForwardStretchStrength = float.PositiveInfinity;
            private float _lastEdgeStreakStrength = float.PositiveInfinity;
            private float _lastEdgeFadeExponent = float.PositiveInfinity;
            private float _lastSpeed01 = float.PositiveInfinity;
            private float _lastThermalMotionCull = float.PositiveInfinity;
            private float _lastAmbientLight = float.PositiveInfinity;
            private float _lastDustStrength = float.PositiveInfinity;
            private float _lastAmbientDustResponse = float.PositiveInfinity;
            private float _lastSnellStrength = float.PositiveInfinity;
            private float _lastDepthSoftness = float.PositiveInfinity;
            private float _lastWaterDensitySignal = float.PositiveInfinity;
            private float _lastHomeostasisFallback = float.PositiveInfinity;
            private float _lastLowTier = float.PositiveInfinity;
            private float _lastVisualOverkill = float.PositiveInfinity;
            private Vector4 _lastIorLut = Vector4.positiveInfinity;

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

                UpdateMaterialParameters(_material, _settings, _runtimeState);

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

            private void UpdateMaterialParameters(Material material, FeatureSettings settings, RuntimeState runtimeState)
            {
                if (!ReferenceEquals(_lastParameterMaterial, material))
                {
                    ResetMaterialParameterCache();
                    _lastParameterMaterial = material;
                }

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
                SetMaterialFloatIfChanged(material, ShaderConstants.IntensityId, effectIntensity, ref _lastIntensity);
                SetMaterialFloatIfChanged(material, ShaderConstants.RainIntensityId, rainIntensity, ref _lastRainIntensity);
                SetMaterialFloatIfChanged(material, ShaderConstants.WetnessId, wetness, ref _lastWetness);
                SetMaterialFloatIfChanged(material, ShaderConstants.HullStressId, hullStress, ref _lastHullStress);
                SetMaterialFloatIfChanged(material, ShaderConstants.DistortionStrengthId, SanitizeNonNegative(settings.distortionStrength), ref _lastDistortionStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.SnellStrengthId, SanitizeNonNegative(settings.snellStrength), ref _lastSnellStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.DepthSoftnessId, SanitizeAtLeast(settings.depthSoftnessMeters, 0.001f), ref _lastDepthSoftness);
                SetMaterialFloatIfChanged(material, ShaderConstants.WaterDensitySignalId, Sanitize01(runtimeState.WaterDensitySignal01), ref _lastWaterDensitySignal);
                SetMaterialFloatIfChanged(material, ShaderConstants.HomeostasisFallbackId, Sanitize01(runtimeState.HomeostasisFallback01), ref _lastHomeostasisFallback);
                SetMaterialFloatIfChanged(material, ShaderConstants.LowTierId, runtimeState.LowTier ? 1f : 0f, ref _lastLowTier);
                SetMaterialFloatIfChanged(material, ShaderConstants.VisualOverkillId, Sanitize01(runtimeState.VisualOverkill01), ref _lastVisualOverkill);
                SetMaterialVectorIfChanged(material, ShaderConstants.IorLutId, SanitizeIorLut(settings.refractionIndexLut), ref _lastIorLut);
                SetMaterialFloatIfChanged(material, ShaderConstants.RunoffSpeedId, SanitizeAtLeast(settings.runoffSpeed, 0.1f), ref _lastRunoffSpeed);
                SetMaterialFloatIfChanged(material, ShaderConstants.DropletScaleId, SanitizeAtLeast(settings.dropletScale, 2f), ref _lastDropletScale);
                SetMaterialFloatIfChanged(material, ShaderConstants.LateralStreakStrengthId, Sanitize01(settings.lateralStreakStrength), ref _lastLateralStreakStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.ForwardStretchStrengthId, Sanitize01(settings.forwardStretchStrength), ref _lastForwardStretchStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.EdgeStreakStrengthId, Sanitize01(settings.edgeStreakStrength), ref _lastEdgeStreakStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.EdgeFadeExponentId, SanitizeAtLeast(settings.edgeFadeExponent, 0.1f), ref _lastEdgeFadeExponent);
                SetMaterialFloatIfChanged(material, ShaderConstants.SpeedId, speed01, ref _lastSpeed01);
                SetMaterialVectorIfChanged(material, ShaderConstants.LocalVelocityId, localVelocityShader, ref _lastLocalVelocityShader);
                SetMaterialFloatIfChanged(material, ShaderConstants.ThermalMotionCullId, thermalMotionCull01, ref _lastThermalMotionCull);
                SetMaterialFloatIfChanged(material, ShaderConstants.AmbientLightId, ambientLight01, ref _lastAmbientLight);
                SetMaterialFloatIfChanged(material, ShaderConstants.DustStrengthId, Sanitize01(settings.dustStrength), ref _lastDustStrength);
                SetMaterialFloatIfChanged(material, ShaderConstants.AmbientDustResponseId, SanitizeNonNegative(settings.ambientDustResponse), ref _lastAmbientDustResponse);
            }

            private void ResetMaterialParameterCache()
            {
                _lastLocalVelocityShader = Vector4.positiveInfinity;
                _lastIntensity = float.PositiveInfinity;
                _lastRainIntensity = float.PositiveInfinity;
                _lastWetness = float.PositiveInfinity;
                _lastHullStress = float.PositiveInfinity;
                _lastDistortionStrength = float.PositiveInfinity;
                _lastRunoffSpeed = float.PositiveInfinity;
                _lastDropletScale = float.PositiveInfinity;
                _lastLateralStreakStrength = float.PositiveInfinity;
                _lastForwardStretchStrength = float.PositiveInfinity;
                _lastEdgeStreakStrength = float.PositiveInfinity;
                _lastEdgeFadeExponent = float.PositiveInfinity;
                _lastSpeed01 = float.PositiveInfinity;
                _lastThermalMotionCull = float.PositiveInfinity;
                _lastAmbientLight = float.PositiveInfinity;
                _lastDustStrength = float.PositiveInfinity;
                _lastAmbientDustResponse = float.PositiveInfinity;
                _lastSnellStrength = float.PositiveInfinity;
                _lastDepthSoftness = float.PositiveInfinity;
                _lastWaterDensitySignal = float.PositiveInfinity;
                _lastHomeostasisFallback = float.PositiveInfinity;
                _lastLowTier = float.PositiveInfinity;
                _lastVisualOverkill = float.PositiveInfinity;
                _lastIorLut = Vector4.positiveInfinity;
            }

            private static void SetMaterialFloatIfChanged(Material material, int shaderId, float value, ref float cachedValue)
            {
                if (math.abs(value - cachedValue) <= MaterialFloatEpsilon)
                    return;

                material.SetFloat(shaderId, value);
                cachedValue = value;
            }

            private static void SetMaterialVectorIfChanged(Material material, int shaderId, Vector4 value, ref Vector4 cachedValue)
            {
                if (math.abs(value.x - cachedValue.x) <= MaterialFloatEpsilon &&
                    math.abs(value.y - cachedValue.y) <= MaterialFloatEpsilon &&
                    math.abs(value.z - cachedValue.z) <= MaterialFloatEpsilon &&
                    math.abs(value.w - cachedValue.w) <= MaterialFloatEpsilon)
                {
                    return;
                }

                material.SetVector(shaderId, value);
                cachedValue = value;
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int IntensityId = Shader.PropertyToID("_HectonVisorFluidIntensity");
            internal static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");
            internal static readonly int WetnessId = Shader.PropertyToID("_HectonVisorFluidWetness");
            internal static readonly int HullStressId = Shader.PropertyToID("_HectonVisorFluidHullStress");
            internal static readonly int DistortionStrengthId = Shader.PropertyToID("_HectonVisorFluidDistortionStrength");
            internal static readonly int SnellStrengthId = Shader.PropertyToID("_HectonVisorFluidSnellStrength");
            internal static readonly int DepthSoftnessId = Shader.PropertyToID("_HectonVisorFluidDepthSoftness");
            internal static readonly int WaterDensitySignalId = Shader.PropertyToID("_HectonWaterDensitySignal");
            internal static readonly int HomeostasisFallbackId = Shader.PropertyToID("_HectonVisorFluidHomeostasisFallback");
            internal static readonly int LowTierId = Shader.PropertyToID("_HectonVisorFluidLowTier");
            internal static readonly int VisualOverkillId = Shader.PropertyToID("_HectonVisorFluidVisualOverkill");
            internal static readonly int IorLutId = Shader.PropertyToID("_HectonVisorFluidIorLut");
            internal static readonly int RunoffSpeedId = Shader.PropertyToID("_HectonVisorFluidRunoffSpeed");
            internal static readonly int DropletScaleId = Shader.PropertyToID("_HectonVisorFluidDropletScale");
            internal static readonly int LateralStreakStrengthId = Shader.PropertyToID("_HectonVisorFluidLateralStreakStrength");
            internal static readonly int ForwardStretchStrengthId = Shader.PropertyToID("_HectonVisorFluidForwardStretchStrength");
            internal static readonly int EdgeStreakStrengthId = Shader.PropertyToID("_HectonVisorFluidEdgeStreakStrength");
            internal static readonly int EdgeFadeExponentId = Shader.PropertyToID("_HectonVisorFluidEdgeFadeExponent");
            internal static readonly int SpeedId = Shader.PropertyToID("_HectonVisorFluidSpeed");
            internal static readonly int LocalVelocityId = Shader.PropertyToID("_HectonVisorFluidLocalVelocity");
            internal static readonly int ThermalMotionCullId = Shader.PropertyToID("_HectonThermalDistortionMotionCull");
            internal static readonly int AmbientLightId = Shader.PropertyToID("_HectonVisorFluidAmbientLight");
            internal static readonly int DustStrengthId = Shader.PropertyToID("_HectonVisorFluidDustStrength");
            internal static readonly int AmbientDustResponseId = Shader.PropertyToID("_HectonVisorFluidAmbientDustResponse");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int CameraOpaqueTextureId = Shader.PropertyToID("_CameraOpaqueTexture");
        }

        [SerializeField] private FeatureSettings settings = new FeatureSettings();

        private VisorFluidPass _pass;
        private Material _material;
        private IDataVault _dataVault;
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
            CoreUtils.Destroy(_material);
            _material = null;
            InvalidateBlackBoxLease();
        }

        private static bool TryBuildRuntimeState(
            Camera renderCamera,
            FeatureSettings settings,
            out RuntimeState runtimeState)
        {
            runtimeState = default;
            uint telemetryFlags = 0u;
            if (renderCamera == null || settings == null)
                return false;

            Camera playerCamera;
            HectonPlayerMovement playerMovement;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                playerCamera = runtimeContext.PlayerCamera;
                playerMovement = runtimeContext.PlayerMovement;
            }
            else
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext == null)
                    return false;

                playerCamera = playerContext.PlayerCamera;
                playerMovement = playerContext.PlayerMovement;
            }

            if (playerCamera == null || !ReferenceEquals(renderCamera, playerCamera))
                return false;

            Transform playerCameraTransform = playerCamera.transform;
            float rawWetness = playerMovement != null ? playerMovement.CurrentWetLensIntensity01 : 0f;
            float rawHullStress = playerMovement != null ? playerMovement.CurrentHullStress01 : 0f;
            FlagIfNonFinite(rawWetness, ref telemetryFlags);
            FlagIfNonFinite(rawHullStress, ref telemetryFlags);
            float wetness = Sanitize01(rawWetness);
            float hullStress = Sanitize01(rawHullStress);
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

            float hullContribution = math.saturate(
                math.saturate((hullStress - HullStressVisorContributionStart01) * HullStressVisorContributionInvRange) *
                Sanitize01(settings.hullStressContribution));
            float effectIntensity = math.saturate(math.max(math.max(wetness, hullContribution), dustContribution));
            float rawRainIntensity = Shader.GetGlobalFloat(ShaderConstants.RainIntensityId);
            FlagIfNonFinite(rawRainIntensity, ref telemetryFlags);
            float rainIntensity = Sanitize01(rawRainIntensity);

            Vector3 localVelocity = Vector3.zero;
            float fluidVelocityActivity = math.max(wetness, hullStress);
            if (playerMovement != null && fluidVelocityActivity > 0.001f)
            {
                Vector3 rawWorldVelocity = playerMovement.InterpolatedLinearVelocity;
                FlagIfNonFinite(rawWorldVelocity, ref telemetryFlags);
                localVelocity = ResolveCameraLocalVelocity(playerCameraTransform, rawWorldVelocity);
            }

            float localVelocitySq =
                localVelocity.x * localVelocity.x +
                localVelocity.y * localVelocity.y +
                localVelocity.z * localVelocity.z;
            float thermalMotionCull01 = localVelocitySq > ThermalDistortionCullSpeedMetersPerSecondSq ? 1f : 0f;
            HectonQualityTier qualityTier = GlobalRegistry.ScalabilityTier;
            bool lowTier = ResolveLowTier(settings, qualityTier);
            float waterDensitySignal01 = ResolveWaterDensitySignal01(ref telemetryFlags);
            float homeostasisFallback01 = lowTier || hullStress >= Sanitize01(settings.stressFallbackThreshold) ? 1f : 0f;
            float visualOverkill01 = ResolveVisualOverkill01(settings, qualityTier, lowTier);
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
                lowTier,
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

        private static float ResolveVisualOverkill01(FeatureSettings settings, HectonQualityTier qualityTier, bool lowTier)
        {
            if (lowTier)
                return 0f;

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

            return configuredStrength * tierScale;
        }

        private static float ResolveWaterDensitySignal01(ref uint telemetryFlags)
        {
            float globalSignal = Shader.GetGlobalFloat(ShaderConstants.WaterDensitySignalId);
            FlagIfNonFinite(globalSignal, ref telemetryFlags);
            if (math.isfinite(globalSignal) && globalSignal > 0.0001f)
                return math.saturate(globalSignal);

            IFluidSim fluidSimulation = GlobalRegistry.FluidSimulation;
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
            if (runtimeState.LowTier)
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
