using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Hecton8.Visor
{
    /// <summary>
    /// Unified fullscreen visor post pass for damage chroma, heat haze, pressure warp, crack reveal, dirt, stress, hypoxia, and blood edge tint.
    /// </summary>
    public sealed partial class HectonVisorUberPostFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener, ILateFrameTickable, ISlowTickable
    {
        private const float ReconstructionConstantsEpsilon = 0.0001f;
        private const float DefaultHypoxiaSafeOxygen01 = 0.22f;
        private const float TemperatureActivityThreshold = 0.001f;
        private const uint BleedingStatusBit = 1u;
        private const int QuestFamilyMemoryCeilingMegabytes = 9000;
        private const int ReconstructionTelemetryCapacity = 300;
        private const int AestheticProfileCapacity = 32;
        private const int CsvScratchBytes = 16 * 1024;
        private const float InternalWaterlineFullScreenSplit = 1.08f;
        private const float InternalWaterlineSubmergeOffsetMeters = 0.03f;
        private const float InternalWaterlineSubmergeFadeMeters = 0.12f;
        private const float InternalWaterlineSplitBypassDepthMeters = 10f;
        private const float DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        private const uint ReconstructionModeNativeHash = 0x4E415456u; // NATV
        private const uint ReconstructionModeBilateralHash = 0x42494C55u; // BILU
        private const uint ReconstructionModeTemporalHash = 0x54454D50u; // TEMP
        private const uint ReconstructionModeFallbackHash = 0x46414C4Cu; // FALL
        private const uint ReconstructionFlagBilateral = 1u << 0;
        private const uint ReconstructionFlagTemporalHook = 1u << 1;
        private const uint ReconstructionFlagDearLie = 1u << 2;
        private const uint ReconstructionFlagFallback = 1u << 3;
        private const uint ReconstructionFlagAbSplit = 1u << 4;
        private const BufferID ReconstructionConstantsVaultId = (BufferID)UberNoirReconstructionVaultIds.Constants;
        private const BufferID ReconstructionTelemetryVaultId = (BufferID)UberNoirReconstructionVaultIds.Telemetry;
        private const BufferID ReconstructionProfileVaultId = (BufferID)UberNoirReconstructionVaultIds.AestheticProfiles;
        private const BufferID ReconstructionCsvScratchVaultId = (BufferID)UberNoirReconstructionVaultIds.CsvScratch;
        private const BufferID ReconstructionMockSignalVaultId = (BufferID)UberNoirReconstructionVaultIds.MockSignal;
        private const string ReconstructionDumpFileName = "Dump_13KRA.bin";
        private const string ReconstructionDumpPayloadLabel = "uberNoirReconstructionDumpPayload";
        private const string AestheticCsvFileName = "noir_aesthetic_profiles.csv";
        private static readonly ICameraHistoryReadAccess.HistoryRequestDelegate s_requestRawColorHistory =
            RequestRawColorHistory;
        private static readonly ulong AestheticCsvMutationGuardMask =
            UberVisorMutationGuardBit(ReconstructionCsvScratchVaultId) |
            UberVisorMutationGuardBit(ReconstructionProfileVaultId);

        [Serializable]
        private sealed partial class FeatureSettings
        {
            [Tooltip("Authored fullscreen material used for the legacy visor post pass when Deep Sea Noir is disabled.")]
            [FormerlySerializedAs("shader")] public Material material = null;

            [Tooltip("Authored fullscreen material used for bilateral DRS reconstruction when Deep Sea Noir is disabled.")]
            [FormerlySerializedAs("reconstructionShader")] public Material reconstructionMaterial = null;

            [Tooltip("Packed crack normal/alpha texture. RG is normal XY; alpha is reveal threshold.")]
            public Texture2D crackTexture = null;

            [Tooltip("Lens dirt texture multiplied by a blue-noise dither mask.")]
            public Texture2D lensDirtTexture = null;

            [Tooltip("Blue-noise texture used to dither lens dirt.")]
            public Texture2D blueNoiseTexture = null;

            [Tooltip("Quality-pressure circular comfort vignette mask. Red channel is peripheral darkness.")]
            public Texture2D vrComfortMaskTexture = null;

            [Tooltip("Injection point for the unified visor post effect.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Tooltip("Runs the edge-preserving reconstruction pass before visor post.")]
            public bool reconstructionEnabled = true;

            [Tooltip("Base bilateral radius in pixels. Shader scales this continuously from GlobalQualityWeight.")]
            [Range(0.25f, 3f)] public float bilateralRadiusPixels = 1.15f;

            [Tooltip("Upper clamp for reconstruction sharpening.")]
            [Range(0f, 1f)] public float sharpeningClamp = 0.68f;

            [Tooltip("Temporal hook blend ceiling. Disabled when no stable history path is available.")]
            [Range(0f, 0.96f)] public float temporalHistoryWeight = 0.62f;

            [Tooltip("Sub-pixel jitter multiplier after render-scale stabilization.")]
            [Range(0f, 2f)] public float jitterScale = 0.85f;

            [Tooltip("Procedural film grain used to hide missing DRS detail.")]
            [Range(0f, 0.16f)] public float filmGrainStrength = 0.035f;

            [Tooltip("Noir reconstruction vignette strength.")]
            [Range(0f, 1f)] public float reconstructionVignetteStrength = 0.32f;

            [Tooltip("Single-axis chromatic reconstruction offset.")]
            [Range(0f, 0.012f)] public float reconstructionChromaticStrength = 0.0025f;

            [FormerlySerializedAs("visualOverkillThreshold")]
            [Tooltip("Continuous response curve shaping visual-overkill shader budget.")]
            [Range(0f, 1f)] public float visualOverkillResponse = 0.84f;

            [Tooltip("Editor/debug split view. Left half raw, right half reconstructed.")]
            public bool reconstructionAbSplit = false;

            [Tooltip("Cold-loads noir_aesthetic_profiles.csv into vault-owned scratch memory.")]
            public bool loadAestheticCsv = true;

            [Tooltip("Single-sample chromatic damage strength.")]
            [Range(0f, 1f)] public float chromaticStrength = 0.34f;

            [Tooltip("Hypoxia desaturation strength.")]
            [Range(0f, 1f)] public float hypoxiaDesaturationStrength = 0.72f;

            [Tooltip("Pressure barrel warp strength.")]
            [Range(0f, 0.18f)] public float pressureWarpStrength = 0.035f;

            [Tooltip("Crack darken/normal strength gate.")]
            [Range(0f, 1f)] public float crackStrength = 0.82f;

            [Tooltip("Pressure delta to effect scalar. Pressure starts at 1 atm.")]
            [Min(0f)] public float pressureInvRange = 0.045f;

            [Tooltip("Temperature scalar used for heat haze activity.")]
            [Min(0f)] public float temperatureScale = 0.018f;

            [Tooltip("Crack normal UV displacement strength.")]
            [Range(0f, 0.01f)] public float crackUvStrength = 0.0024f;

            [Tooltip("Lens dirt and blood edge strength.")]
            [Range(0f, 1f)] public float lensDirtAndBloodStrength = 0.26f;

            [Tooltip("Heat haze triangle-wave frequency. Legacy values are radians-scaled in shader to preserve density.")]
            [Min(1f)] public float heatHazeFrequency = 38f;

            [Tooltip("Heat haze triangle-wave speed. Legacy values are radians-scaled in shader to preserve motion.")]
            [Min(0f)] public float heatHazeSpeed = 0.62f;

            [Tooltip("Heat haze UV displacement amplitude. Collapses continuously under quality pressure.")]
            [Range(0f, 0.006f)] public float heatHazeAmplitude = 0.0017f;

            [Tooltip("Damage/stress vignette strength.")]
            [Range(0f, 1f)] public float damageVignetteStrength = 0.24f;

            [Tooltip("Below or equal to this VRAM amount, heat haze receives continuous quality pressure.")]
            [FormerlySerializedAs("lowTierVideoMemoryMb")]
            [Min(256)] public int minimumQualityVideoMemoryMb = 2048;

            [Tooltip("Oxygen value below which hypoxia ramps when no stronger global signal is published.")]
            [Range(0.01f, 1f)] public float hypoxiaSafeOxygen01 = DefaultHypoxiaSafeOxygen01;

            [Tooltip("Runtime Y delta to screen-space waterline scale for internal flood masking.")]
            [Range(0.02f, 0.35f)] public float internalWaterlineMetersToScreen = 0.14f;

            [Tooltip("Camera pitch compensation for the internal flood screen split.")]
            [Range(0f, 1f)] public float internalWaterlinePitchScale = 0.52f;
        }

        private struct RuntimeState
        {
            public byte VisorPostActive;
            public float HealthFraction;
            public float LocalTemperature;
            public float AmbientPressure;
            public float PlayerStress01;
            public float Hypoxia01;
            public float Bleeding01;
            public float WetLens01;
            public float HullStress01;
            public uint AupShiftFrame;
            public float VrComfortVignette01;
            public Vector4 VrComfortJerkState;
            public Vector4 InternalWaterlineParams;
            public Vector4 InternalWaterlineDistortion;
            public float QualityPressure01;
            public byte DepthlessTBDR;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct NoirAestheticProfileDTO
        {
            [FieldOffset(0)]
            public uint ProfileHash;
            [FieldOffset(4)]
            public uint Flags;
            [FieldOffset(8)]
            public float DepthMinMeters;
            [FieldOffset(12)]
            public float DepthMaxMeters;
            [FieldOffset(16)]
            public float SanityMin01;
            [FieldOffset(20)]
            public float SanityMax01;
            [FieldOffset(24)]
            public float4 ReconstructionParams;
            [FieldOffset(40)]
            public float4 OverkillParams;
            [FieldOffset(56)]
            private byte _pad0;
            [FieldOffset(57)]
            private byte _pad1;
            [FieldOffset(58)]
            private byte _pad2;
            [FieldOffset(59)]
            private byte _pad3;
            [FieldOffset(60)]
            private byte _pad4;
            [FieldOffset(61)]
            private byte _pad5;
            [FieldOffset(62)]
            private byte _pad6;
            [FieldOffset(63)]
            private byte _pad7;
        }

        private sealed class VisorUberPostPass : ScriptableRenderPass
        {
            private sealed class ReconstructionPassData
            {
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal TextureHandle Motion;
                internal TextureHandle History;
                internal Material Material;
                internal BufferHandle ConstantsBuffer;
                internal float AbSplit;
                internal float VisualTimeSeconds;
                internal bool HasDepth;
                internal bool HasMotion;
                internal bool HasHistory;
            }

            private sealed class PostPassData
            {
                internal TextureHandle Source;
                internal TextureHandle Depth;
                internal TextureHandle CrackTexture;
                internal TextureHandle LensDirtTexture;
                internal TextureHandle BlueNoiseTexture;
                internal TextureHandle VrComfortMaskTexture;
                internal Material Material;
                internal Vector4 Strengths0;
                internal Vector4 Strengths1;
                internal Vector4 WaveParams;
                internal Vector4 TextureFlags;
                internal Vector4 VrComfortJerkState;
                internal Vector4 InternalWaterlineParams;
                internal Vector4 InternalWaterlineDistortion;
                internal float VisualTimeSeconds;
                internal float HealthFraction;
                internal float LocalTemperature;
                internal float AmbientPressure;
                internal float PlayerStress01;
                internal float Hypoxia01;
                internal float Bleeding01;
                internal float WetLens01;
                internal float HullStress01;
                internal float AupShiftFrame;
                internal float VrComfortVignette01;
                internal float DepthlessTBDR;
                internal float QualityPressure01;
                internal bool HasDepth;
            }

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Hecton Visor Uber Post"); // COLD ALLOC: ProfilingSampler[1] - RenderGraph marker reused for every frame - owner: VisorUberPostPass
            private readonly ProfilingSampler _reconstructionProfilingSampler = new ProfilingSampler("Hecton UberNoir Reconstruction"); // COLD ALLOC: ProfilingSampler[1] - RenderGraph marker reused for every frame - owner: VisorUberPostPass
            private FeatureSettings _settings;
            private Material _material;
            private Material _reconstructionMaterial;
            private Texture _boundCrackTexture;
            private Texture _boundLensDirtTexture;
            private Texture _boundBlueNoiseTexture;
            private Texture _boundVrComfortMaskTexture;
            private RTHandle _crackTextureHandle;
            private RTHandle _lensDirtTextureHandle;
            private RTHandle _blueNoiseTextureHandle;
            private RTHandle _vrComfortMaskTextureHandle;
            private GraphicsBuffer _reconstructionConstantsBuffer;
            private RuntimeState _runtimeState;
            private bool _requestRawColorHistory;
            private float _visualTimeSeconds;

            public VisorUberPostPass()
            {
                profilingSampler = _profilingSampler;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                FeatureSettings settings,
                Material material,
                Material reconstructionMaterial,
                GraphicsBuffer reconstructionConstantsBuffer,
                RuntimeState runtimeState,
                bool temporalHookActive,
                bool requestRawColorHistory,
                float visualTimeSeconds)
            {
                _settings = settings;
                _material = material;
                _reconstructionMaterial = reconstructionMaterial;
                _reconstructionConstantsBuffer = reconstructionConstantsBuffer;
                _runtimeState = runtimeState;
                _requestRawColorHistory = requestRawColorHistory;
                _visualTimeSeconds = SanitizeFinite(visualTimeSeconds, 0f);
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ScriptableRenderPassInput input = runtimeState.DepthlessTBDR != 0
                    ? ScriptableRenderPassInput.Color
                    : ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
                if (temporalHookActive)
                    input |= ScriptableRenderPassInput.Motion;
                ConfigureInput(input);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || (_material == null && _reconstructionMaterial == null))
                    return;

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
                if (cameraData.renderType != CameraRenderType.Base)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                TextureHandle depthTexture = resourceData.activeDepthTexture;
                TextureHandle motionTexture = resourceData.motionVectorColor;
                bool depthlessTBDR = _runtimeState.DepthlessTBDR != 0;
                if (!sourceTexture.IsValid() || (!depthlessTBDR && !depthTexture.IsValid()))
                    return;

                TextureHandle historyTexture = TextureHandle.nullHandle;
                bool hasHistory = _requestRawColorHistory &&
                                  TryImportRawColorHistory(renderGraph, cameraData, out historyTexture);
                bool hasMotion = motionTexture.IsValid();
                bool hasTemporalInputs = hasHistory && hasMotion;
                bool bindTemporalInputs = _requestRawColorHistory || hasHistory;
                TextureHandle activeMotionTexture = bindTemporalInputs
                    ? (hasTemporalInputs ? motionTexture : renderGraph.defaultResources.blackTexture)
                    : TextureHandle.nullHandle;
                TextureHandle activeHistoryTexture = bindTemporalInputs
                    ? (hasTemporalInputs ? historyTexture : sourceTexture)
                    : TextureHandle.nullHandle;
                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureHandle postSourceTexture = sourceTexture;
                bool hasDepth = !depthlessTBDR && depthTexture.IsValid();
                TextureHandle activeDepthTexture = hasDepth ? depthTexture : renderGraph.defaultResources.blackTexture;
                bool reconstructionReady =
                    _settings.reconstructionEnabled &&
                    _reconstructionMaterial != null &&
                    _reconstructionConstantsBuffer != null &&
                    _reconstructionConstantsBuffer.IsValid();

                if (reconstructionReady)
                {
                    TextureDesc reconstructionDesc = sourceDesc;
                    reconstructionDesc.name = "_HectonUberNoirReconstruction";
                    reconstructionDesc.clearBuffer = false;
                    reconstructionDesc.depthBufferBits = DepthBits.None;
                    reconstructionDesc.msaaSamples = MSAASamples.None;
                    reconstructionDesc.colorFormat = sourceDesc.colorFormat;
                    reconstructionDesc.useMipMap = false;
                    reconstructionDesc.autoGenerateMips = false;
                    TextureHandle reconstructionTexture = renderGraph.CreateTexture(reconstructionDesc);
                    BufferHandle constantsBuffer = renderGraph.ImportBuffer(_reconstructionConstantsBuffer);

                    using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<ReconstructionPassData>(
                               "Hecton UberNoir Reconstruction",
                               out ReconstructionPassData passData,
                               _reconstructionProfilingSampler))
                    {
                        passData.Source = sourceTexture;
                        passData.Depth = activeDepthTexture;
                        passData.Motion = activeMotionTexture;
                        passData.History = activeHistoryTexture;
                        passData.Material = _reconstructionMaterial;
                        passData.ConstantsBuffer = constantsBuffer;
                        passData.AbSplit = _settings != null ? ResolveAbSplit01(_settings) : 0f;
                        passData.VisualTimeSeconds = _visualTimeSeconds;
                        passData.HasDepth = activeDepthTexture.IsValid();
                        passData.HasMotion = bindTemporalInputs && activeMotionTexture.IsValid();
                        passData.HasHistory = bindTemporalInputs && activeHistoryTexture.IsValid();

                        builder.UseTexture(sourceTexture, AccessFlags.Read);
                        if (passData.HasDepth)
                            builder.UseTexture(activeDepthTexture, AccessFlags.Read);
                        if (passData.HasMotion)
                            builder.UseTexture(activeMotionTexture, AccessFlags.Read);
                        if (hasTemporalInputs)
                            builder.UseTexture(historyTexture, AccessFlags.Read);
                        builder.UseBuffer(constantsBuffer, AccessFlags.Read);
                        builder.SetRenderAttachment(reconstructionTexture, 0, AccessFlags.Write);
                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc(static (ReconstructionPassData data, RasterGraphContext context) =>
                        {
                            context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                            if (data.HasDepth)
                                context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                            if (data.HasMotion)
                                context.cmd.SetGlobalTexture(ShaderConstants.MotionVectorTextureId, data.Motion);
                            if (data.HasHistory)
                                context.cmd.SetGlobalTexture(ShaderConstants.ReconstructionHistoryTextureId, data.History);
                            GraphicsBuffer constants = data.ConstantsBuffer;
                            if (constants == null)
                                return;

                            context.cmd.SetGlobalFloat(ShaderConstants.ReconstructionAbSplitId, data.AbSplit);
                            context.cmd.SetGlobalFloat(ShaderConstants.ReconstructionVisualTimeId, data.VisualTimeSeconds);
                            context.cmd.SetGlobalConstantBuffer(
                                constants,
                                ShaderConstants.ReconstructionConstantsBufferId,
                                0,
                                UberNoirReconstructionConstantsDTO.SizeBytes);
                            CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                        });
                    }

                    postSourceTexture = reconstructionTexture;
                }

                if (_runtimeState.VisorPostActive == 0 || _material == null)
                {
                    resourceData.cameraColor = postSourceTexture;
                    return;
                }

                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonVisorUberPost";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                if (!TryImportStaticPostTextures(
                        renderGraph,
                        out TextureHandle crackTextureHandle,
                        out TextureHandle lensDirtTextureHandle,
                        out TextureHandle blueNoiseTextureHandle,
                        out TextureHandle vrComfortMaskTextureHandle))
                {
                    return;
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PostPassData>(
                           "Hecton Visor Uber Post",
                           out PostPassData passData,
                           _profilingSampler))
                {
                    passData.Source = postSourceTexture;
                    passData.Depth = depthTexture;
                    passData.CrackTexture = crackTextureHandle;
                    passData.LensDirtTexture = lensDirtTextureHandle;
                    passData.BlueNoiseTexture = blueNoiseTextureHandle;
                    passData.VrComfortMaskTexture = vrComfortMaskTextureHandle;
                    passData.Material = _material;
                    passData.HasDepth = hasDepth;
                    PopulatePostPassData(passData, _settings, _runtimeState, _visualTimeSeconds);

                    builder.UseTexture(postSourceTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    if (hasDepth)
                        builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.UseTexture(crackTextureHandle, AccessFlags.Read);
                    builder.UseTexture(lensDirtTextureHandle, AccessFlags.Read);
                    builder.UseTexture(blueNoiseTextureHandle, AccessFlags.Read);
                    builder.UseTexture(vrComfortMaskTextureHandle, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PostPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        if (data.HasDepth)
                            context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
                        context.cmd.SetGlobalTexture(ShaderConstants.CrackTextureId, data.CrackTexture);
                        context.cmd.SetGlobalTexture(ShaderConstants.LensDirtTextureId, data.LensDirtTexture);
                        context.cmd.SetGlobalTexture(ShaderConstants.BlueNoiseTextureId, data.BlueNoiseTexture);
                        context.cmd.SetGlobalTexture(ShaderConstants.VrComfortMaskTextureId, data.VrComfortMaskTexture);
                        BindPostShaderParameters(context.cmd, data);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }

            private static bool TryImportRawColorHistory(
                RenderGraph renderGraph,
                UniversalCameraData cameraData,
                out TextureHandle historyTexture)
            {
                historyTexture = TextureHandle.nullHandle;
                if (renderGraph == null || cameraData == null || cameraData.historyManager == null)
                    return false;

                RawColorHistory rawColorHistory = cameraData.historyManager.GetHistoryForRead<RawColorHistory>();
                if (rawColorHistory == null)
                    return false;

                int eyeIndex = ResolveHistoryEyeIndex(cameraData.xr);
                RTHandle previousRawColor = rawColorHistory.GetPreviousTexture(eyeIndex);
                if (previousRawColor == null)
                    return false;

                historyTexture = renderGraph.ImportTexture(previousRawColor);
                return historyTexture.IsValid();
            }

            private static int ResolveHistoryEyeIndex(XRPass xr)
            {
#if ENABLE_VR && ENABLE_XR_MODULE
                return xr != null && xr.enabled && !xr.singlePassEnabled ? xr.multipassId : 0;
#else
                return 0;
#endif
            }

            private static void PopulatePostPassData(
                PostPassData passData,
                FeatureSettings settings,
                RuntimeState runtimeState,
                float visualTimeSeconds)
            {
                float qualityPressure01 = Sanitize01(runtimeState.QualityPressure01);
                passData.VisualTimeSeconds = SanitizeFinite(visualTimeSeconds, 0f);
                passData.HealthFraction = Sanitize01(runtimeState.HealthFraction);
                passData.LocalTemperature = SanitizeFinite(runtimeState.LocalTemperature, 0f);
                passData.AmbientPressure = math.max(1f, SanitizeFinite(runtimeState.AmbientPressure, 1f));
                passData.PlayerStress01 = Sanitize01(runtimeState.PlayerStress01);
                passData.Hypoxia01 = Sanitize01(runtimeState.Hypoxia01);
                passData.Bleeding01 = Sanitize01(runtimeState.Bleeding01);
                passData.WetLens01 = Sanitize01(runtimeState.WetLens01);
                passData.HullStress01 = Sanitize01(runtimeState.HullStress01);
                passData.AupShiftFrame = runtimeState.AupShiftFrame;
                passData.VrComfortVignette01 = Sanitize01(runtimeState.VrComfortVignette01);
                passData.DepthlessTBDR = runtimeState.DepthlessTBDR != 0 ? 1f : 0f;
                passData.VrComfortJerkState = SanitizeVrComfortJerkState(runtimeState.VrComfortJerkState);
                passData.InternalWaterlineParams = SanitizeInternalWaterlineParams(runtimeState.InternalWaterlineParams);
                passData.InternalWaterlineDistortion = SanitizeInternalWaterlineDistortion(runtimeState.InternalWaterlineDistortion);
                passData.QualityPressure01 = qualityPressure01;
                Vector4 strengths0 = default;
                strengths0.x = math.saturate(settings.chromaticStrength);
                strengths0.y = math.saturate(settings.hypoxiaDesaturationStrength);
                strengths0.z = math.clamp(settings.pressureWarpStrength, 0f, 0.18f);
                strengths0.w = math.saturate(settings.crackStrength);
                passData.Strengths0 = strengths0;

                Vector4 strengths1 = default;
                strengths1.x = math.max(0f, settings.pressureInvRange);
                strengths1.y = math.max(0f, settings.temperatureScale);
                strengths1.z = math.clamp(settings.crackUvStrength, 0f, 0.01f);
                strengths1.w = math.saturate(settings.lensDirtAndBloodStrength);
                passData.Strengths1 = strengths1;

                Vector4 waveParams = default;
                waveParams.x = math.max(1f, settings.heatHazeFrequency);
                waveParams.y = math.max(0f, settings.heatHazeSpeed);
                waveParams.z = math.clamp(settings.heatHazeAmplitude, 0f, 0.006f) * (1f - qualityPressure01);
                waveParams.w = math.saturate(settings.damageVignetteStrength);
                passData.WaveParams = waveParams;

                Vector4 textureFlags = default;
                textureFlags.x = settings.crackTexture != null ? 1f : 0f;
                textureFlags.y = settings.lensDirtTexture != null ? 1f : 0f;
                textureFlags.z = settings.blueNoiseTexture != null ? 1f : 0f;
                textureFlags.w = settings.vrComfortMaskTexture != null ? 1f : 0f;
                passData.TextureFlags = textureFlags;
            }

            private bool TryImportStaticPostTextures(
                RenderGraph renderGraph,
                out TextureHandle crackTextureHandle,
                out TextureHandle lensDirtTextureHandle,
                out TextureHandle blueNoiseTextureHandle,
                out TextureHandle vrComfortMaskTextureHandle)
            {
                crackTextureHandle = TextureHandle.nullHandle;
                lensDirtTextureHandle = TextureHandle.nullHandle;
                blueNoiseTextureHandle = TextureHandle.nullHandle;
                vrComfortMaskTextureHandle = TextureHandle.nullHandle;
                if (renderGraph == null || _settings == null)
                    return false;

                Texture crackTexture = _settings.crackTexture != null ? _settings.crackTexture : Texture2D.blackTexture;
                Texture lensDirtTexture = _settings.lensDirtTexture != null ? _settings.lensDirtTexture : Texture2D.whiteTexture;
                Texture blueNoiseTexture = _settings.blueNoiseTexture != null ? _settings.blueNoiseTexture : Texture2D.grayTexture;
                Texture vrComfortMaskTexture = _settings.vrComfortMaskTexture != null ? _settings.vrComfortMaskTexture : Texture2D.grayTexture;

                RTHandle crackHandle = GetStaticPostTextureHandle(crackTexture, _boundCrackTexture, _crackTextureHandle);
                RTHandle lensDirtHandle = GetStaticPostTextureHandle(lensDirtTexture, _boundLensDirtTexture, _lensDirtTextureHandle);
                RTHandle blueNoiseHandle = GetStaticPostTextureHandle(blueNoiseTexture, _boundBlueNoiseTexture, _blueNoiseTextureHandle);
                RTHandle vrComfortMaskHandle = GetStaticPostTextureHandle(vrComfortMaskTexture, _boundVrComfortMaskTexture, _vrComfortMaskTextureHandle);
                if (crackHandle == null ||
                    lensDirtHandle == null ||
                    blueNoiseHandle == null ||
                    vrComfortMaskHandle == null)
                {
                    return false;
                }

                crackTextureHandle = renderGraph.ImportTexture(crackHandle);
                lensDirtTextureHandle = renderGraph.ImportTexture(lensDirtHandle);
                blueNoiseTextureHandle = renderGraph.ImportTexture(blueNoiseHandle);
                vrComfortMaskTextureHandle = renderGraph.ImportTexture(vrComfortMaskHandle);
                return crackTextureHandle.IsValid() &&
                       lensDirtTextureHandle.IsValid() &&
                       blueNoiseTextureHandle.IsValid() &&
                       vrComfortMaskTextureHandle.IsValid();
            }

            public void PrepareStaticPostTextureHandlesCold(FeatureSettings settings)
            {
                if (settings == null)
                    return;

                Texture crackTexture = settings.crackTexture != null ? settings.crackTexture : Texture2D.blackTexture;
                Texture lensDirtTexture = settings.lensDirtTexture != null ? settings.lensDirtTexture : Texture2D.whiteTexture;
                Texture blueNoiseTexture = settings.blueNoiseTexture != null ? settings.blueNoiseTexture : Texture2D.grayTexture;
                Texture vrComfortMaskTexture = settings.vrComfortMaskTexture != null ? settings.vrComfortMaskTexture : Texture2D.grayTexture;

                EnsureStaticPostTextureHandle(crackTexture, ref _boundCrackTexture, ref _crackTextureHandle);
                EnsureStaticPostTextureHandle(lensDirtTexture, ref _boundLensDirtTexture, ref _lensDirtTextureHandle);
                EnsureStaticPostTextureHandle(blueNoiseTexture, ref _boundBlueNoiseTexture, ref _blueNoiseTextureHandle);
                EnsureStaticPostTextureHandle(vrComfortMaskTexture, ref _boundVrComfortMaskTexture, ref _vrComfortMaskTextureHandle);
            }

            private static RTHandle GetStaticPostTextureHandle(
                Texture texture,
                Texture boundTexture,
                RTHandle handle)
            {
                if (texture == null || handle == null)
                    return null;

                return ReferenceEquals(boundTexture, texture) ? handle : null;
            }

            private static RTHandle EnsureStaticPostTextureHandle(
                Texture texture,
                ref Texture boundTexture,
                ref RTHandle handle)
            {
                if (texture == null)
                    return null;

                if (ReferenceEquals(boundTexture, texture) && handle != null)
                    return handle;

                if (handle != null)
                    RTHandles.Release(handle);

                boundTexture = texture;
                handle = RTHandles.Alloc(texture);
                return handle;
            }

            public void ReleaseStaticPostTextureHandles()
            {
                ReleaseStaticPostTextureHandle(ref _crackTextureHandle, ref _boundCrackTexture);
                ReleaseStaticPostTextureHandle(ref _lensDirtTextureHandle, ref _boundLensDirtTexture);
                ReleaseStaticPostTextureHandle(ref _blueNoiseTextureHandle, ref _boundBlueNoiseTexture);
                ReleaseStaticPostTextureHandle(ref _vrComfortMaskTextureHandle, ref _boundVrComfortMaskTexture);
            }

            private static void ReleaseStaticPostTextureHandle(ref RTHandle handle, ref Texture boundTexture)
            {
                if (handle != null)
                {
                    RTHandles.Release(handle);
                    handle = null;
                }

                boundTexture = null;
            }

            private static void BindPostShaderParameters(RasterCommandBuffer cmd, PostPassData data)
            {
                cmd.SetGlobalFloat(ShaderConstants.HealthFractionId, data.HealthFraction);
                cmd.SetGlobalFloat(ShaderConstants.LocalTemperatureId, data.LocalTemperature);
                cmd.SetGlobalFloat(ShaderConstants.AmbientPressureId, data.AmbientPressure);
                cmd.SetGlobalFloat(ShaderConstants.PlayerStressId, data.PlayerStress01);
                cmd.SetGlobalFloat(ShaderConstants.HypoxiaId, data.Hypoxia01);
                cmd.SetGlobalFloat(ShaderConstants.BleedingId, data.Bleeding01);
                cmd.SetGlobalFloat(ShaderConstants.WetLensId, data.WetLens01);
                cmd.SetGlobalFloat(ShaderConstants.HullStressId, data.HullStress01);
                cmd.SetGlobalFloat(ShaderConstants.AupShiftFrameId, data.AupShiftFrame);
                cmd.SetGlobalFloat(ShaderConstants.VrComfortVignette01Id, data.VrComfortVignette01);
                cmd.SetGlobalFloat(ShaderConstants.DepthlessTBDRId, data.DepthlessTBDR);
                cmd.SetGlobalFloat(ShaderConstants.QualityPressureId, data.QualityPressure01);
                cmd.SetGlobalFloat(ShaderConstants.UberVisualTimeId, data.VisualTimeSeconds);
                cmd.SetGlobalVector(ShaderConstants.VrComfortJerkStateId, data.VrComfortJerkState);
                cmd.SetGlobalVector(ShaderConstants.InternalWaterlineParamsId, data.InternalWaterlineParams);
                cmd.SetGlobalVector(ShaderConstants.InternalWaterlineDistortionId, data.InternalWaterlineDistortion);
                cmd.SetGlobalVector(ShaderConstants.Strengths0Id, data.Strengths0);
                cmd.SetGlobalVector(ShaderConstants.Strengths1Id, data.Strengths1);
                cmd.SetGlobalVector(ShaderConstants.WaveParamsId, data.WaveParams);
                cmd.SetGlobalVector(ShaderConstants.TextureFlagsId, data.TextureFlags);
            }
        }

        private static class ShaderConstants
        {
            internal static readonly int HealthFractionId = Shader.PropertyToID("_HectonUberHealthFraction");
            internal static readonly int LocalTemperatureId = Shader.PropertyToID("_HectonUberLocalTemperature");
            internal static readonly int AmbientPressureId = Shader.PropertyToID("_HectonUberAmbientPressure");
            internal static readonly int PlayerStressId = Shader.PropertyToID("_HectonUberPlayerStress01");
            internal static readonly int HypoxiaId = Shader.PropertyToID("_HectonUberHypoxia01");
            internal static readonly int BleedingId = Shader.PropertyToID("_HectonUberBleeding01");
            internal static readonly int WetLensId = Shader.PropertyToID("_HectonUberWetLens01");
            internal static readonly int HullStressId = Shader.PropertyToID("_HectonUberHullStress01");
            internal static readonly int AupShiftFrameId = Shader.PropertyToID("_HectonUberAupShiftFrame");
            internal static readonly int VrComfortVignette01Id = Shader.PropertyToID("_VRComfortVignette01");
            internal static readonly int SomaticComfortVignetteId = Shader.PropertyToID("_VRComfortVignette");
            internal static readonly int DepthlessTBDRId = Shader.PropertyToID("_HectonUberDepthlessTBDR");
            internal static readonly int VrComfortJerkStateId = Shader.PropertyToID("_HectonVRComfortJerkState");
            internal static readonly int InternalWaterlineYId = Shader.PropertyToID("_InternalWaterlineY");
            internal static readonly int InternalWaterlineRuntimeId = Shader.PropertyToID("_InternalWaterlineRuntime");
            internal static readonly int InternalWaterlineParamsId = Shader.PropertyToID("_InternalWaterlineParams");
            internal static readonly int InternalWaterlineDistortionId = Shader.PropertyToID("_InternalWaterlineDistortion");
            internal static readonly int QualityPressureId = Shader.PropertyToID("_HectonUberQualityPressure");
            internal static readonly int UberVisualTimeId = Shader.PropertyToID("_HectonUberVisualTime");
            internal static readonly int Strengths0Id = Shader.PropertyToID("_HectonUberStrengths0");
            internal static readonly int Strengths1Id = Shader.PropertyToID("_HectonUberStrengths1");
            internal static readonly int WaveParamsId = Shader.PropertyToID("_HectonUberWaveParams");
            internal static readonly int TextureFlagsId = Shader.PropertyToID("_HectonUberTextureFlags");
            internal static readonly int CrackTextureId = Shader.PropertyToID("_HectonVisorCrackTex");
            internal static readonly int LensDirtTextureId = Shader.PropertyToID("_HectonLensDirtTex");
            internal static readonly int BlueNoiseTextureId = Shader.PropertyToID("_HectonBlueNoiseTex");
            internal static readonly int VrComfortMaskTextureId = Shader.PropertyToID("_HectonVRComfortMaskTex");
            internal static readonly int PlayerStressGlobalId = Shader.PropertyToID("_PlayerStress01");
            internal static readonly int HypoxiaSignalGlobalId = Shader.PropertyToID("_HypoxiaSignal");
            internal static readonly int LocalTemperatureGlobalId = Shader.PropertyToID("_LocalTemperature");
            internal static readonly int AmbientPressureGlobalId = Shader.PropertyToID("_AmbientPressure");
            internal static readonly int FrequencyTuningErrorGlobalId = Shader.PropertyToID("_HectonFrequencyTuningError01");
            internal static readonly int LightShaftParamsId = Shader.PropertyToID("_HectonLightShaftParams");
            internal static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int MotionVectorTextureId = Shader.PropertyToID("_MotionVectorTexture");
            internal static readonly int ReconstructionHistoryTextureId = Shader.PropertyToID("_H8ReconstructionHistoryTex");
            internal static readonly int ReconstructionConstantsBufferId = Shader.PropertyToID("UberNoirReconstructionConstants");
            internal static readonly int ReconstructionAbSplitId = Shader.PropertyToID("_H8UberNoirABSplit");
            internal static readonly int ReconstructionVisualTimeId = Shader.PropertyToID("_H8UberNoirVisualTime");
            internal static readonly int NoirFogStratificationId = Shader.PropertyToID("_HectonNoirFogStratification");
        }

#if UNITY_EDITOR
        private static UberNoirReconstructionConstantsDTO s_lastEditorConstants;
        private static bool s_hasLastEditorConstants;
        private static bool s_editorOverrideActive;
        private static bool s_editorAbSplit;
        private static bool s_editorMockScaleActive;
        private static float s_editorBilateralRadiusPixels = 1.15f;
        private static float s_editorTemporalHistoryWeight01 = 0.62f;
        private static float s_editorSharpeningClamp01 = 0.68f;
        private static float s_editorFilmGrainStrength01 = 0.035f;
        private static float s_editorVisualOverkillResponse01 = 0.84f;
        private static float s_editorMockRenderScale01 = 0.5f;
        private static float s_editorMockQualityWeight01 = 0.35f;

        public static void SetEditorReconstructionOverride(
            bool active,
            float bilateralRadiusPixels,
            float temporalHistoryWeight01,
            float sharpeningClamp01,
            float filmGrainStrength01,
            float visualOverkillResponse01,
            bool abSplit,
            bool mockScaleActive,
            float mockRenderScale01,
            float mockQualityWeight01)
        {
            s_editorOverrideActive = active;
            s_editorBilateralRadiusPixels = math.clamp(bilateralRadiusPixels, 0.25f, 3f);
            s_editorTemporalHistoryWeight01 = math.clamp(temporalHistoryWeight01, 0f, 0.96f);
            s_editorSharpeningClamp01 = math.saturate(sharpeningClamp01);
            s_editorFilmGrainStrength01 = math.clamp(filmGrainStrength01, 0f, 0.16f);
            s_editorVisualOverkillResponse01 = math.saturate(visualOverkillResponse01);
            s_editorAbSplit = abSplit;
            s_editorMockScaleActive = mockScaleActive;
            s_editorMockRenderScale01 = math.clamp(mockRenderScale01, 0.3f, 1f);
            s_editorMockQualityWeight01 = math.saturate(mockQualityWeight01);
        }

        public static unsafe bool TryFetchEditorReconstructionConstants(out UberNoirReconstructionConstantsDTO constants)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null &&
                vault.TryGetGenerationHandle<UberNoirReconstructionConstantsDTO>(
                    ReconstructionConstantsVaultId,
                    out VaultGenerationHandle<UberNoirReconstructionConstantsDTO> handle) &&
                !vault.IsCompactionFenceActive &&
                IsReconstructionVaultHandle(in handle, ReconstructionConstantsVaultId))
            {
                if (TryReadReconstructionVaultBuffer(
                        vault,
                        in handle,
                        ReconstructionConstantsVaultId,
                        1,
                        out NativeArray<UberNoirReconstructionConstantsDTO>.ReadOnly buffer) &&
                    !vault.IsCompactionFenceActive)
                {
                    constants = buffer[0];
                    return !vault.IsCompactionFenceActive;
                }
            }

            constants = s_lastEditorConstants;
            return s_hasLastEditorConstants;
        }

        public static unsafe bool TryWriteEditorMockReconstructionSignal(in MockReconstructionInputSignal signal)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            VaultGenerationHandle<MockReconstructionInputSignal> handle;
            if (!vault.TryGetGenerationHandle<MockReconstructionInputSignal>(ReconstructionMockSignalVaultId, out handle) ||
                !IsReconstructionVaultHandle(in handle, ReconstructionMockSignalVaultId))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                    return false;

                handle = vault.EnsureGenerationHandle<MockReconstructionInputSignal>(
                    ReconstructionMockSignalVaultId,
                    1,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.ClearMemory);
            }

            if (vault.IsCompactionFenceActive ||
                !IsReconstructionVaultHandle(in handle, ReconstructionMockSignalVaultId) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.GraphicsScalability, out NativeArray<MockReconstructionInputSignal> mockBuffer))
            {
                return false;
            }

            try
            {
                if (vault.IsCompactionFenceActive || !mockBuffer.IsCreated || mockBuffer.Length <= 0)
                    return false;

                mockBuffer[0] = signal;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.GraphicsScalability);
            }
        }
#endif

        [SerializeField] private FeatureSettings settings = new FeatureSettings(); // COLD ALLOC: FeatureSettings[1] - serialized renderer feature settings - owner: HectonVisorUberPostFeature

        private VisorUberPostPass _pass;
        private Material _material;
        private Material _reconstructionMaterial;
        private GraphicsBuffer _reconstructionConstantsBufferA;
        private GraphicsBuffer _reconstructionConstantsBufferB;
        private GraphicsBuffer _activeReconstructionConstantsBuffer;
        private IDataVault _dataVault;
        private VaultGenerationHandle<UberNoirReconstructionConstantsDTO> _reconstructionConstantsHandle;
        private VaultGenerationHandle<ReconstructionTelemetryEntry> _reconstructionTelemetryHandle;
        private VaultGenerationHandle<NoirAestheticProfileDTO> _aestheticProfileHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<MockReconstructionInputSignal> _mockSignalHandle;
        private UberNoirReconstructionConstantsDTO _lastReconstructionConstants;
        private readonly NoirAestheticProfileDTO[] _aestheticProfileCache = new NoirAestheticProfileDTO[AestheticProfileCapacity]; // COLD ALLOC: NoirAestheticProfileDTO[32] - reconstruction CSV profile snapshot for render-frame lock-free selection - owner: HectonVisorUberPostFeature
        private int _aestheticProfileCacheCount;
        private int _reconstructionConstantsBufferIndex;
        private int _reconstructionTelemetryCursor;
        private bool _hasReconstructionConstants;
        private bool _reconstructionDumpWritten;
        private bool _aestheticCsvLoaded;
        private bool _aestheticCsvLoadAttempted;
        private int _cachedMinimumQualityThresholdMb = int.MinValue;
        private int _cachedGraphicsMemoryMb;
        private int _cachedDepthlessTBDRFrame = int.MinValue;
        private float _cachedMemoryQualityPressureFloor01;
        private bool _cachedDepthlessTBDR;
        private bool _depthlessTBDRPlatformClassified;
        private bool _depthlessTBDRPlatformCandidate;
        private bool _supportsReconstructionConstantBuffer;
        private float _cachedAmbientPressureAtm = 1f;
        private float _cachedLocalTemperature;
        private float _cachedPlayerStress01;
        private float _cachedFrequencyTuningError01;
        private float _cachedVrComfortVignette01;
        private Vector4 _cachedVrComfortJerkState;
        private Vector4 _cachedInternalWaterlineRuntime;
        private float _cachedInternalWaterlineY = float.NegativeInfinity;
        private Vector4 _cachedInternalWaterlineDistortion;
        private float _cachedLightShaftActiveCount;
        private float _cachedHypoxiaSignal01;
        private ICameraHistoryReadAccess _rawColorHistoryReadAccess;
        private Camera _rawColorHistoryCamera;
        private bool _rawColorHistoryRequestRegistered;
        private Camera _pendingReconstructionCamera;
        private RuntimeState _pendingReconstructionRuntimeState;
        private bool _pendingReconstructionStateValid;
        private bool _pendingReconstructionRawHistoryAvailable;

        /// <inheritdoc />
        public override void Create()
        {
            RefreshNoirCachedDependenciesCold();
            CachePlatformCapabilitiesCold(settings);
            TryRegisterHotSwapListener();

            bool runtimeAllocationAllowed = Application.isPlaying;

            // COLD ALLOC: VisorUberPostPass[1] - reused ScriptableRenderPass instance - owner: HectonVisorUberPostFeature
            _pass ??= new VisorUberPostPass();
            if (runtimeAllocationAllowed)
                _pass.PrepareStaticPostTextureHandlesCold(settings);
            else
                _pass.ReleaseStaticPostTextureHandles();
            EnsureNoirPassCold();
            _material = ResolvePostMaterial(settings);
            _reconstructionMaterial = settings != null && !settings.deepSeaNoirUnifiedPass
                ? settings.reconstructionMaterial
                : null;
            if (_material == null && _reconstructionMaterial == null)
                return;

            if (settings != null && settings.deepSeaNoirUnifiedPass)
            {
                if (!runtimeAllocationAllowed)
                {
                    ReleaseNoirRuntimeResourcesCold();
                    return;
                }

                _noirColorCsvLoadAttempted = false;
#if UNITY_EDITOR
                if (settings.loadNoirColorCsv)
                    TryLoadNoirColorCsvCold();
#endif
                TryRegisterSlowTickable();
                TryRegisterLateFrameTickable();
                return;
            }

            if (!runtimeAllocationAllowed)
            {
                ReleaseReconstructionRuntimeResourcesCold();
                return;
            }

            _aestheticCsvLoadAttempted = false;
            _aestheticProfileCacheCount = 0;
#if UNITY_EDITOR
            if (settings != null && settings.loadAestheticCsv)
                TryLoadAestheticCsvCold();
#endif
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
                return;

            if (settings == null || _pass == null || (_material == null && _reconstructionMaterial == null))
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || cameraType == CameraType.SceneView)
            {
                return;
            }
            if (renderingData.cameraData.renderType != CameraRenderType.Base)
            {
                return;
            }

            if (settings.deepSeaNoirUnifiedPass)
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
                if (!EnsureNoirConstantsBuffersCold() ||
                    !EnsureNoirVaultHandles())
                {
                    return;
                }

                if (_material == null ||
                    !NoirConstantsBuffersReady() ||
                    !NoirVaultHandlesReady() ||
                    _activeNoirConstantsBuffer == null ||
                    !_activeNoirConstantsBuffer.IsValid())
                {
                    return;
                }

                _noirPass.SetupNoir(settings, _material, _activeNoirConstantsBuffer);
                renderer.EnqueuePass(_noirPass);
                return;
            }

            _pass.PrepareStaticPostTextureHandlesCold(settings);
            Camera renderCamera = renderingData.cameraData.camera;
            float memoryQualityPressureFloor01 = ResolveMemoryQualityPressureFloor01();
            if (!TryBuildRuntimeState(renderCamera, settings, memoryQualityPressureFloor01, out RuntimeState runtimeState))
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
                return;
            }

            bool reconstructionStorageReady = EnsureReconstructionConstantsBufferCold();
            if (!EnsureReconstructionVaultHandles())
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
                return;
            }

            bool requestRawColorHistory = reconstructionStorageReady &&
                                          ShouldRequestRawColorHistory(settings, runtimeState);
            UpdateRawColorHistoryRequest(
                renderCamera,
                renderingData.cameraData.historyManager,
                requestRawColorHistory);
            bool rawColorHistoryAvailable = requestRawColorHistory &&
                                            TryHasReadableRawColorHistory(renderCamera, renderingData.cameraData.xr);
            StageReconstructionInput(renderCamera, in runtimeState, rawColorHistoryAvailable);
            bool reconstructionConstantsReady =
                settings.reconstructionEnabled &&
                _hasReconstructionConstants &&
                _activeReconstructionConstantsBuffer != null &&
                _activeReconstructionConstantsBuffer.IsValid();

            bool temporalHookActive = reconstructionConstantsReady &&
                                      _lastReconstructionConstants.TemporalParams.z > 0.001f;
            _pass.Setup(
                settings,
                _material,
                _reconstructionMaterial,
                reconstructionConstantsReady ? _activeReconstructionConstantsBuffer : null,
                runtimeState,
                temporalHookActive,
                reconstructionConstantsReady && requestRawColorHistory,
                _noirWrappedVisualTimeSeconds);
            renderer.EnqueuePass(_pass);
        }

        private void ReleaseReconstructionRuntimeResourcesCold()
        {
            ReleaseReconstructionVaultHandles(_dataVault);
            _reconstructionConstantsBufferA?.Release();
            _reconstructionConstantsBufferA = null;
            _reconstructionConstantsBufferB?.Release();
            _reconstructionConstantsBufferB = null;
            _activeReconstructionConstantsBuffer = null;
            _hasReconstructionConstants = false;
        }

        private void StageReconstructionInput(
            Camera renderCamera,
            in RuntimeState runtimeState,
            bool rawColorHistoryAvailable)
        {
            _pendingReconstructionCamera = renderCamera;
            _pendingReconstructionRuntimeState = runtimeState;
            _pendingReconstructionRawHistoryAvailable = rawColorHistoryAvailable;
            _pendingReconstructionStateValid = renderCamera != null;
        }

        private void ClearPendingReconstructionInput()
        {
            _pendingReconstructionCamera = null;
            _pendingReconstructionRuntimeState = default;
            _pendingReconstructionRawHistoryAvailable = false;
            _pendingReconstructionStateValid = false;
        }

        private void TryUpdateReconstructionConstantsLate()
        {
            if (settings == null ||
                settings.deepSeaNoirUnifiedPass ||
                !settings.reconstructionEnabled ||
                _reconstructionMaterial == null ||
                !_pendingReconstructionStateValid ||
                _pendingReconstructionCamera == null ||
                !IsReconstructionConstantsBufferReady() ||
                !ReconstructionVaultHandlesReady())
            {
                return;
            }

            uint frame = ResolveNoirFrameId();
            ResolveWrappedNoirTimeSeconds(frame);
            RuntimeState runtimeState = _pendingReconstructionRuntimeState;
            UberNoirReconstructionConstantsDTO reconstructionConstants = BuildReconstructionConstants(
                settings,
                runtimeState,
                _pendingReconstructionCamera,
                _pendingReconstructionRawHistoryAvailable);
            bool reconstructionConstantsReady = UpdateReconstructionConstants(in reconstructionConstants);
            RecordReconstructionTelemetry(in reconstructionConstants, runtimeState, reconstructionConstantsReady);
        }

        private bool ShouldRequestRawColorHistory(
            FeatureSettings currentSettings,
            RuntimeState runtimeState)
        {
            if (currentSettings == null ||
                !currentSettings.reconstructionEnabled ||
                currentSettings.temporalHistoryWeight <= 0.001f ||
                runtimeState.DepthlessTBDR != 0)
            {
                return false;
            }

            ResolutionScaleState scaleState;
            float quality01 = TryUseCachedResolutionState(out scaleState)
                ? Sanitize01(scaleState.GlobalQualityWeight01)
                : 1f;
            float temporalWarmup01 = Smooth01(math.saturate((quality01 - 0.34f) * 4.1666665f));
            return temporalWarmup01 > 0.001f;
        }

        private bool TryHasReadableRawColorHistory(Camera renderCamera, XRPass xr)
        {
            if (renderCamera == null ||
                !_rawColorHistoryRequestRegistered ||
                !ReferenceEquals(_rawColorHistoryCamera, renderCamera) ||
                _rawColorHistoryReadAccess == null)
            {
                return false;
            }

            RawColorHistory rawColorHistory = _rawColorHistoryReadAccess.GetHistoryForRead<RawColorHistory>();
            if (rawColorHistory == null)
                return false;

            RTHandle previousRawColor = rawColorHistory.GetPreviousTexture(ResolveHistoryEyeIndex(xr));
            return previousRawColor != null;
        }

        private static int ResolveHistoryEyeIndex(XRPass xr)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            return xr != null && xr.enabled && !xr.singlePassEnabled ? xr.multipassId : 0;
#else
            return 0;
#endif
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
            ClearRawColorHistoryRequest();
            ClearPendingReconstructionInput();
            _pass?.ReleaseStaticPostTextureHandles();
            ReleaseNoirVaultHandles(_dataVault);
            ReleaseReconstructionVaultHandles(_dataVault);
            _material = null;
            _reconstructionMaterial = null;
            _noirConstantsBufferA?.Release();
            _noirConstantsBufferA = null;
            _noirConstantsBufferB?.Release();
            _noirConstantsBufferB = null;
            _activeNoirConstantsBuffer = null;
            _reconstructionConstantsBufferA?.Release();
            _reconstructionConstantsBufferA = null;
            _reconstructionConstantsBufferB?.Release();
            _reconstructionConstantsBufferB = null;
            _activeReconstructionConstantsBuffer = null;
            _hasNoirConstants = false;
            _noirColorCsvLoaded = false;
            _noirColorCsvLoadAttempted = false;
            ClearNoirPlayerContext();
            _noirResolutionScaler = null;
            _nextNoirPlayerRefreshFrame = 0;
            _hasReconstructionConstants = false;
            _aestheticCsvLoaded = false;
            _aestheticCsvLoadAttempted = false;
            _aestheticProfileCacheCount = 0;
            _noirColorProfileCacheCount = 0;
        }

        private void UpdateRawColorHistoryRequest(
            Camera renderCamera,
            ICameraHistoryReadAccess historyReadAccess,
            bool requestRawColorHistory)
        {
            if (!requestRawColorHistory || renderCamera == null || historyReadAccess == null)
            {
                ClearRawColorHistoryRequest();
                return;
            }

            if (_rawColorHistoryRequestRegistered &&
                ReferenceEquals(_rawColorHistoryCamera, renderCamera) &&
                ReferenceEquals(_rawColorHistoryReadAccess, historyReadAccess))
            {
                return;
            }

            ClearRawColorHistoryRequest();
            _rawColorHistoryCamera = renderCamera;
            _rawColorHistoryReadAccess = historyReadAccess;
            _rawColorHistoryReadAccess.OnGatherHistoryRequests += s_requestRawColorHistory;
            _rawColorHistoryRequestRegistered = true;
        }

        private void ClearRawColorHistoryRequest()
        {
            if (_rawColorHistoryRequestRegistered && _rawColorHistoryReadAccess != null)
                _rawColorHistoryReadAccess.OnGatherHistoryRequests -= s_requestRawColorHistory;

            _rawColorHistoryReadAccess = null;
            _rawColorHistoryCamera = null;
            _rawColorHistoryRequestRegistered = false;
        }

        private static void RequestRawColorHistory(IPerFrameHistoryAccessTracker historyAccess)
        {
            historyAccess?.RequestAccess<RawColorHistory>();
        }

        private bool EnsureReconstructionConstantsBufferCold()
        {
            if (!_supportsReconstructionConstantBuffer)
            {
                _reconstructionConstantsBufferA?.Release();
                _reconstructionConstantsBufferA = null;
                _reconstructionConstantsBufferB?.Release();
                _reconstructionConstantsBufferB = null;
                _activeReconstructionConstantsBuffer = null;
                return false;
            }

            if (_reconstructionConstantsBufferA == null || !_reconstructionConstantsBufferA.IsValid())
            {
                _reconstructionConstantsBufferA?.Release();
                _reconstructionConstantsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    UberNoirReconstructionConstantsDTO.SizeBytes); // COLD ALLOC: GraphicsBuffer[48B] - Uber Noir reconstruction CBuffer A - owner: HectonVisorUberPostFeature
                _hasReconstructionConstants = false;
                _activeReconstructionConstantsBuffer = null;
            }

            if (_reconstructionConstantsBufferB == null || !_reconstructionConstantsBufferB.IsValid())
            {
                _reconstructionConstantsBufferB?.Release();
                _reconstructionConstantsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    UberNoirReconstructionConstantsDTO.SizeBytes); // COLD ALLOC: GraphicsBuffer[48B] - Uber Noir reconstruction CBuffer B - owner: HectonVisorUberPostFeature
                _hasReconstructionConstants = false;
                _activeReconstructionConstantsBuffer = null;
            }

            return IsReconstructionConstantsBufferReady();
        }

        private bool IsReconstructionConstantsBufferReady()
        {
            return _supportsReconstructionConstantBuffer &&
                   _reconstructionConstantsBufferA != null &&
                   _reconstructionConstantsBufferB != null &&
                   _reconstructionConstantsBufferA.IsValid() &&
                   _reconstructionConstantsBufferB.IsValid();
        }

        private bool EnsureReconstructionVaultHandles()
        {
            if (_dataVault == null)
                BindUberDataVaultForLifecycle(GlobalRegistry.DataVault, _dataVault);

            if (_dataVault == null)
                return false;

            _ = EnsureReconstructionVaultHandle(
                ref _reconstructionConstantsHandle,
                ReconstructionConstantsVaultId,
                1,
                NativeArrayOptions.ClearMemory);
            _ = EnsureReconstructionVaultHandle(
                ref _reconstructionTelemetryHandle,
                ReconstructionTelemetryVaultId,
                ReconstructionTelemetryCapacity,
                NativeArrayOptions.ClearMemory);
            _ = EnsureReconstructionVaultHandle(
                ref _aestheticProfileHandle,
                ReconstructionProfileVaultId,
                AestheticProfileCapacity,
                NativeArrayOptions.ClearMemory);
            _ = EnsureReconstructionVaultHandle(
                ref _csvScratchHandle,
                ReconstructionCsvScratchVaultId,
                CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory);
            _ = EnsureReconstructionVaultHandle(
                ref _mockSignalHandle,
                ReconstructionMockSignalVaultId,
                1,
                NativeArrayOptions.ClearMemory);

            return ReconstructionVaultHandlesReady();
        }

        private void BindUberDataVaultForLifecycle(IDataVault nextVault, IDataVault previousVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            IDataVault releaseVault = _dataVault ?? previousVault;
            ReleaseNoirVaultHandles(releaseVault);
            ReleaseReconstructionVaultHandles(releaseVault);
            _dataVault = nextVault;
        }

        private bool ReconstructionVaultHandlesReady()
        {
            return _dataVault != null &&
                   IsReconstructionVaultHandle(in _reconstructionConstantsHandle, ReconstructionConstantsVaultId) &&
                   IsReconstructionVaultHandle(in _reconstructionTelemetryHandle, ReconstructionTelemetryVaultId) &&
                   IsReconstructionVaultHandle(in _aestheticProfileHandle, ReconstructionProfileVaultId) &&
                   IsReconstructionVaultHandle(in _csvScratchHandle, ReconstructionCsvScratchVaultId) &&
                   IsReconstructionVaultHandle(in _mockSignalHandle, ReconstructionMockSignalVaultId);
        }

        private void ClearReconstructionVaultHandles()
        {
            _reconstructionConstantsHandle = default;
            _reconstructionTelemetryHandle = default;
            _aestheticProfileHandle = default;
            _csvScratchHandle = default;
            _mockSignalHandle = default;
            _aestheticCsvLoaded = false;
            _aestheticCsvLoadAttempted = false;
            _aestheticProfileCacheCount = 0;
            _reconstructionTelemetryCursor = 0;
            _hasReconstructionConstants = false;
            _activeReconstructionConstantsBuffer = null;
        }

        private void ReleaseReconstructionVaultHandles(IDataVault vault)
        {
            // Renderer features are secondary DataVault consumers. URP disposal can run while the
            // vault arena is resetting, so this lifecycle path must detach handles without freeing
            // vault-owned storage from inside RenderPipeline cleanup.
            ClearReconstructionVaultHandles();
        }

        private bool EnsureReconstructionVaultHandle<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options)
            where T : unmanaged
        {
            if (_dataVault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (TryReadReconstructionVaultBuffer(_dataVault, in handle, bufferId, requiredLength, out NativeArray<T>.ReadOnly _))
                return true;

            if (_dataVault.IsCompactionFenceActive || _dataVault.IsAllocationLocked)
            {
                handle = default;
                return false;
            }

            handle = _dataVault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GraphicsScalability,
                options);

            if (TryReadReconstructionVaultBuffer(_dataVault, in handle, bufferId, requiredLength, out NativeArray<T>.ReadOnly _))
                return true;

            ReleaseReconstructionVaultHandle(_dataVault, ref handle, bufferId);
            return false;
        }

        private static void ReleaseReconstructionVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : unmanaged
        {
            if (vault != null && IsReconstructionVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryReadReconstructionVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : unmanaged
        {
            buffer = default;
            return vault != null &&
                   requiredLength >= 0 &&
                   IsReconstructionVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   (requiredLength == 0 || buffer.Length >= requiredLength);
        }

        private static bool IsReconstructionVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability &&
                   handle.Generation != 0u;
        }

        private UberNoirReconstructionConstantsDTO BuildReconstructionConstants(
            FeatureSettings currentSettings,
            RuntimeState runtimeState,
            Camera renderCamera,
            bool rawColorHistoryAvailable)
        {
            ResolutionScaleState scaleState;
            bool hasScaleState = TryUseCachedResolutionState(out scaleState);
            float currentScale = hasScaleState ? SanitizePositive(scaleState.CurrentRenderScale01, 1f) : 1f;
            float targetScale = hasScaleState ? SanitizePositive(scaleState.TargetRenderScale01, currentScale) : currentScale;
            float quality01 = hasScaleState ? Sanitize01(scaleState.GlobalQualityWeight01) : 1f;
            float stateSharpen = hasScaleState ? Sanitize01(scaleState.SharpenIntensity01) : 0f;
            float visualOverkill01 = hasScaleState ? Sanitize01(scaleState.VisualOverkill01) : 0f;
            float dearLie01 = hasScaleState ? Sanitize01(scaleState.DearLie01) : 0f;
            float bilateralSetting = currentSettings != null ? currentSettings.bilateralRadiusPixels : 1.15f;
            float temporalSetting = currentSettings != null ? currentSettings.temporalHistoryWeight : 0.62f;
            float sharpeningSetting = currentSettings != null ? currentSettings.sharpeningClamp : 0.68f;
            float jitterSetting = currentSettings != null ? currentSettings.jitterScale : 0.85f;
            float grainSetting = currentSettings != null ? currentSettings.filmGrainStrength : 0.035f;
            float vignetteSetting = currentSettings != null ? currentSettings.reconstructionVignetteStrength : 0.32f;
            float chromaticSetting = currentSettings != null ? currentSettings.reconstructionChromaticStrength : 0.0025f;
            float overkillResponseSetting = currentSettings != null ? currentSettings.visualOverkillResponse : 0.84f;
            float mockJitterPixels = -1f;
            float mockTemporalStress01 = 0f;

            if (TryCopyMockReconstructionSignalSnapshot(out MockReconstructionInputSignal mockSignal))
            {
                currentScale = math.clamp(mockSignal.RenderScale01, 0.3f, 1f);
                targetScale = currentScale;
                quality01 = Sanitize01(mockSignal.GlobalQualityWeight01);
                mockJitterPixels = math.isfinite(mockSignal.JitterPixels) ? math.max(0f, mockSignal.JitterPixels) : mockJitterPixels;
                mockTemporalStress01 = Sanitize01(mockSignal.TemporalStress01);
            }

#if UNITY_EDITOR
            if (s_editorOverrideActive)
            {
                if (s_editorMockScaleActive)
                {
                    currentScale = math.clamp(s_editorMockRenderScale01, 0.3f, 1f);
                    targetScale = currentScale;
                    quality01 = Sanitize01(s_editorMockQualityWeight01);
                    mockJitterPixels = math.max(mockJitterPixels, math.lerp(2.0f, 0.35f, quality01));
                    mockTemporalStress01 = math.max(mockTemporalStress01, 1f - quality01);
                }

                bilateralSetting = math.clamp(s_editorBilateralRadiusPixels, 0.25f, 3f);
                temporalSetting = math.clamp(s_editorTemporalHistoryWeight01, 0f, 0.96f);
                sharpeningSetting = math.saturate(s_editorSharpeningClamp01);
                grainSetting = math.clamp(s_editorFilmGrainStrength01, 0f, 0.16f);
                overkillResponseSetting = math.saturate(s_editorVisualOverkillResponse01);
            }
#endif

            NoirAestheticProfileDTO profile;
            bool hasProfile = TrySelectAestheticProfileSnapshot(renderCamera, runtimeState, out profile);
            float scaleDeficit01 = math.saturate(1f - math.min(currentScale, 1f));
            float safeCurrentScale = math.max(0.3f, currentScale);
            float inverseScale = math.rcp(safeCurrentScale);
            float lowQuality01 = 1f - quality01;
            float reconstructionNeed01 = Smooth01(math.saturate(math.max(scaleDeficit01, lowQuality01 * 0.65f)));

            float bilateralRadius = math.max(0.25f, bilateralSetting) *
                                    math.lerp(1.85f, 0.68f, quality01) *
                                    math.lerp(1f, 1.42f, reconstructionNeed01);
            float historyWeight = temporalSetting *
                                  quality01 *
                                  math.saturate(1f - scaleDeficit01 * 0.35f);
            historyWeight *= math.lerp(1f, 0.65f, mockTemporalStress01);
            float sharpeningClamp = math.saturate(sharpeningSetting);
            float sharpness01 = math.min(sharpeningClamp, math.max(stateSharpen, reconstructionNeed01 * sharpeningClamp));
            float grain01 = grainSetting *
                            math.lerp(2.25f, 0.55f, quality01) *
                            math.lerp(1f, 1.35f, scaleDeficit01);
            float vignette01 = vignetteSetting *
                               math.lerp(1.35f, 0.45f, quality01) *
                               math.lerp(0.85f, 1.2f, dearLie01);
            float chromatic01 = chromaticSetting *
                                math.lerp(2.2f, 0.35f, quality01);
            float overkill01 = math.max(
                visualOverkill01,
                ResolveCompatibilityVisualOverkillWeight01(quality01, overkillResponseSetting));
            float temporalAvailability01 = rawColorHistoryAvailable ? 1f : 0f;
            float temporalMotionScale = temporalAvailability01 *
                                        Smooth01(math.saturate((quality01 - 0.42f) * 1.7241379f)) *
                                        math.saturate(1f - scaleDeficit01 * 0.5f);
            historyWeight *= temporalAvailability01;
            float jitterPixels = math.max(
                math.max(0f, jitterSetting) * inverseScale * math.lerp(0.45f, 1f, quality01),
                mockJitterPixels);

            if (hasProfile)
            {
                bilateralRadius *= math.max(0.1f, profile.ReconstructionParams.x);
                historyWeight *= math.saturate(profile.ReconstructionParams.y);
                sharpness01 = math.min(sharpeningClamp, math.max(sharpness01, math.saturate(profile.ReconstructionParams.z)));
                grain01 *= math.max(0f, profile.ReconstructionParams.w);
                vignette01 *= math.max(0f, profile.OverkillParams.x);
                chromatic01 *= math.max(0f, profile.OverkillParams.y);
                overkill01 = math.max(overkill01, math.saturate(profile.OverkillParams.z) * quality01);
            }

            UberNoirReconstructionConstantsDTO constants = default;
            constants.RenderScaleParams = new float4(
                safeCurrentScale,
                math.clamp(targetScale, 0.3f, 1.5f),
                inverseScale,
                math.saturate(sharpness01));
            constants.TemporalParams = new float4(
                math.saturate(historyWeight),
                jitterPixels,
                math.saturate(temporalMotionScale),
                math.clamp(bilateralRadius, 0.25f, 5f));
            constants.OverkillParams = new float4(
                math.clamp(grain01, 0f, 0.35f),
                math.saturate(vignette01),
                math.clamp(chromatic01, 0f, 0.024f),
                math.saturate(overkill01));

#if UNITY_EDITOR
            s_lastEditorConstants = constants;
            s_hasLastEditorConstants = true;
#endif
            return constants;
        }

        private unsafe bool UpdateReconstructionConstants(in UberNoirReconstructionConstantsDTO constants)
        {
            if (!IsReconstructionConstantsBufferReady())
                return false;

            if (_hasReconstructionConstants && ReconstructionConstantsEqual(in _lastReconstructionConstants, in constants))
                return _activeReconstructionConstantsBuffer != null && _activeReconstructionConstantsBuffer.IsValid();

            GraphicsBuffer target = (_reconstructionConstantsBufferIndex & 1) == 0
                ? _reconstructionConstantsBufferA
                : _reconstructionConstantsBufferB;
            if (target == null || !target.IsValid())
                return false;
            _reconstructionConstantsBufferIndex++;

            UberNoirReconstructionConstantsDTO local = constants;
            try
            {
                NativeArray<UberNoirReconstructionConstantsDTO> mapped =
                    target.LockBufferForWrite<UberNoirReconstructionConstantsDTO>(0, 1);
                try
                {
                    UnsafeUtility.MemCpy(
                        mapped.GetUnsafePtr(),
                        UnsafeUtility.AddressOf(ref local),
                        UberNoirReconstructionConstantsDTO.SizeBytes);
                }
                finally
                {
                    target.UnlockBufferAfterWrite<UberNoirReconstructionConstantsDTO>(1);
                }
            }
            catch (ObjectDisposedException)
            {
                ClearReconstructionConstantsGpuPayload();
                return false;
            }
            catch (InvalidOperationException)
            {
                ClearReconstructionConstantsGpuPayload();
                return false;
            }
            catch (ArgumentException)
            {
                ClearReconstructionConstantsGpuPayload();
                return false;
            }
            catch (NotSupportedException)
            {
                ClearReconstructionConstantsGpuPayload();
                return false;
            }

            _lastReconstructionConstants = constants;
            _hasReconstructionConstants = true;
            _activeReconstructionConstantsBuffer = target;
            WriteReconstructionConstantsToVault(in constants);
            return true;
        }

        private void ClearReconstructionConstantsGpuPayload()
        {
            _activeReconstructionConstantsBuffer = null;
            _hasReconstructionConstants = false;
        }

        private void WriteReconstructionConstantsToVault(in UberNoirReconstructionConstantsDTO constants)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsReconstructionVaultHandle(in _reconstructionConstantsHandle, ReconstructionConstantsVaultId) ||
                !vault.TryAcquireWriteLock(in _reconstructionConstantsHandle, SystemID.GraphicsScalability, out NativeArray<UberNoirReconstructionConstantsDTO> buffer))
            {
                return;
            }

            try
            {
                if (vault.IsCompactionFenceActive || !buffer.IsCreated || buffer.Length <= 0)
                    return;

                buffer[0] = constants;
            }
            finally
            {
                vault.ReleaseWriteLock(in _reconstructionConstantsHandle, SystemID.GraphicsScalability);
            }
        }

        private void RecordReconstructionTelemetry(
            in UberNoirReconstructionConstantsDTO constants,
            RuntimeState runtimeState,
            bool reconstructionBufferReady)
        {
            IDataVault vault = _dataVault;
            if (!ReconstructionVaultHandlesReady() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _reconstructionTelemetryHandle, SystemID.GraphicsScalability, out NativeArray<ReconstructionTelemetryEntry> telemetry))
            {
                return;
            }

            bool shouldDump = false;
            try
            {
                if (vault.IsCompactionFenceActive || !telemetry.IsCreated || telemetry.Length <= 0)
                    return;

                int index = _reconstructionTelemetryCursor;
                if ((uint)index >= (uint)telemetry.Length)
                    index = 0;

                ReconstructionTelemetryEntry entry = default;
                bool reconstructionActive = reconstructionBufferReady &&
                                            _reconstructionMaterial != null &&
                                            _activeReconstructionConstantsBuffer != null &&
                                            _activeReconstructionConstantsBuffer.IsValid();
                float scale = SanitizePositive(constants.RenderScaleParams.x, 1f);
                uint modeHash = !reconstructionActive
                    ? ReconstructionModeFallbackHash
                    : scale >= 0.999f
                    ? ReconstructionModeNativeHash
                    : constants.TemporalParams.z > 0.001f
                        ? ReconstructionModeTemporalHash
                        : ReconstructionModeBilateralHash;
                uint flags = !reconstructionActive
                    ? ReconstructionFlagFallback
                    : scale < 0.999f ? ReconstructionFlagBilateral : 0u;
                if (reconstructionActive && constants.TemporalParams.z > 0.001f)
                    flags |= ReconstructionFlagTemporalHook;
                if (reconstructionActive &&
                    (constants.OverkillParams.x > 0.001f ||
                     constants.OverkillParams.y > 0.001f ||
                     constants.OverkillParams.z > 0.001f))
                    flags |= ReconstructionFlagDearLie;
                if (reconstructionActive && ResolveAbSplit01(settings) > 0.5f)
                    flags |= ReconstructionFlagAbSplit;

                entry.Frame = ResolveNoirFrameId();
                entry.Flags = flags;
                entry.CurrentRenderScale01 = scale;
                entry.TargetRenderScale01 = SanitizePositive(constants.RenderScaleParams.y, scale);
                entry.SharpenIntensity01 = Sanitize01(constants.RenderScaleParams.w);
                entry.BilateralRadiusPixels = math.max(0f, constants.TemporalParams.w);
                entry.HistoryWeight01 = Sanitize01(constants.TemporalParams.x);
                ResolutionScaleState telemetryScaleState;
                entry.GlobalQualityWeight01 = TryUseCachedResolutionState(out telemetryScaleState)
                    ? Sanitize01(telemetryScaleState.GlobalQualityWeight01)
                    : 1f;
                entry.Grain01 = math.max(0f, constants.OverkillParams.x);
                entry.ChromaticAberration01 = math.max(0f, constants.OverkillParams.z);
                entry.Vignette01 = math.max(0f, constants.OverkillParams.y);
                entry.UpscalerModeHash = modeHash;
                entry.GpuComputeTimeMs = EstimateReconstructionCostMs(scale, constants.TemporalParams.w, constants.TemporalParams.z, runtimeState.DepthlessTBDR != 0);
                entry.JitterPixels = math.max(0f, constants.TemporalParams.y);
                telemetry[index] = entry;

                index++;
                _reconstructionTelemetryCursor = index >= telemetry.Length ? 0 : index;
                shouldDump = scale < 0.4f && !_reconstructionDumpWritten;
            }
            finally
            {
                vault.ReleaseWriteLock(in _reconstructionTelemetryHandle, SystemID.GraphicsScalability);
            }

            if (shouldDump)
                _reconstructionDumpWritten = TryDumpReconstructionTelemetry();
        }

        private unsafe bool TryDumpReconstructionTelemetry()
        {
            if (_dataVault == null ||
                !TryGetReconstructionTelemetryEntryCount(out int entryCount) ||
                entryCount <= 0)
                return false;

            NativeArray<byte> payload = default;
            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "AgentLogs");
                string path = Path.Combine(directory, ReconstructionDumpFileName);
                int stride = DrsContractLayout.ReconstructionTelemetryEntryStrideBytes;
                int totalBytes = entryCount * stride;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(HectonVisorUberPostFeature),
                    ReconstructionDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);

                int offset = 0;
                for (int i = 0; i < entryCount; i++)
                {
                    if (!TryReadReconstructionTelemetryEntry(i, out ReconstructionTelemetryEntry entry))
                        return false;

                    Span<byte> rowBytes = new Span<byte>(payloadPtr + offset, stride);
                    WriteReconstructionTelemetryEntry(rowBytes, in entry);
                    offset += stride;
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonVisorUberPostFeature),
                    ReconstructionDumpPayloadLabel);
            }
        }

        private bool TryGetReconstructionTelemetryEntryCount(out int entryCount)
        {
            entryCount = 0;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive)
                return false;

            if (!TryReadReconstructionVaultBuffer(
                    vault,
                    in _reconstructionTelemetryHandle,
                    ReconstructionTelemetryVaultId,
                    ReconstructionTelemetryCapacity,
                    out NativeArray<ReconstructionTelemetryEntry>.ReadOnly telemetry))
            {
                return false;
            }

            if (vault.IsCompactionFenceActive || !telemetry.IsCreated)
                return false;

            entryCount = math.min(telemetry.Length, ReconstructionTelemetryCapacity);
            return entryCount > 0;
        }

        private bool TryReadReconstructionTelemetryEntry(int index, out ReconstructionTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                index < 0)
                return false;

            if (!TryReadReconstructionVaultBuffer(
                    vault,
                    in _reconstructionTelemetryHandle,
                    ReconstructionTelemetryVaultId,
                    ReconstructionTelemetryCapacity,
                    out NativeArray<ReconstructionTelemetryEntry>.ReadOnly telemetry))
            {
                return false;
            }

            if (vault.IsCompactionFenceActive ||
                !telemetry.IsCreated ||
                (uint)index >= (uint)math.min(telemetry.Length, ReconstructionTelemetryCapacity))
                return false;

            entry = telemetry[index];
            return !vault.IsCompactionFenceActive;
        }

        private static void WriteReconstructionTelemetryEntry(Span<byte> destination, in ReconstructionTelemetryEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Flags);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.CurrentRenderScale01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.TargetRenderScale01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.SharpenIntensity01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.BilateralRadiusPixels);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.HistoryWeight01);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.GlobalQualityWeight01);
            WriteFloatLittleEndian(destination.Slice(32, 4), entry.Grain01);
            WriteFloatLittleEndian(destination.Slice(36, 4), entry.ChromaticAberration01);
            WriteFloatLittleEndian(destination.Slice(40, 4), entry.Vignette01);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(44, 4), entry.UpscalerModeHash);
            WriteFloatLittleEndian(destination.Slice(48, 4), entry.GpuComputeTimeMs);
            WriteFloatLittleEndian(destination.Slice(52, 4), entry.JitterPixels);
            destination.Slice(56, 8).Clear();
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private static ulong UberVisorMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

#if UNITY_EDITOR
        private bool TryLoadAestheticCsvCold()
        {
            if (_aestheticCsvLoaded || _aestheticCsvLoadAttempted)
                return _aestheticCsvLoaded;

            IDataVault vault = _dataVault;
            if (!EnsureReconstructionVaultHandles() || vault == null || vault.IsCompactionFenceActive)
                return false;

            string path = ResolveAestheticCsvPath();
            _aestheticCsvLoadAttempted = true;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                Span<byte> csvBytes = stackalloc byte[CsvScratchBytes];
                int read = ReadCsvFileIntoSpan(path, csvBytes);
                if (read <= 0)
                    return false;

                Span<NoirAestheticProfileDTO> parsedProfiles = stackalloc NoirAestheticProfileDTO[AestheticProfileCapacity];
                int parsed = ParseAestheticCsv(csvBytes.Slice(0, read), parsedProfiles);

                if (!vault.TryAcquireMutationGuard(AestheticCsvMutationGuardMask))
                {
                    _aestheticCsvLoadAttempted = false;
                    return false;
                }

                try
                {
                    if (!vault.TryResolveHandle(in _csvScratchHandle, out NativeArray<byte> scratch) ||
                        !vault.TryResolveHandle(in _aestheticProfileHandle, out NativeArray<NoirAestheticProfileDTO> profiles) ||
                        !scratch.IsCreated ||
                        scratch.Length <= 0 ||
                        !profiles.IsCreated ||
                        profiles.Length <= 0)
                    {
                        return false;
                    }

                    CopyBytesToNativeArray(csvBytes.Slice(0, read), scratch);
                    CopyAestheticProfilesToNativeArray(parsedProfiles, parsed, profiles);
                }
                finally
                {
                    vault.ReleaseMutationGuard(AestheticCsvMutationGuardMask);
                }

                CacheAestheticProfileSnapshot(parsedProfiles, parsed);
                _aestheticCsvLoaded = parsed > 0;
                return _aestheticCsvLoaded;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private static int ReadCsvFileIntoSpan(string path, Span<byte> destination)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            int max = (int)math.min(stream.Length, destination.Length);
            return max > 0 ? stream.Read(destination.Slice(0, max)) : 0;
        }

        private static void CopyBytesToNativeArray(ReadOnlySpan<byte> source, NativeArray<byte> destination)
        {
            int count = math.min(source.Length, destination.IsCreated ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
            for (int i = count; i < destination.Length; i++)
                destination[i] = 0;
        }

        private static void CopyAestheticProfilesToNativeArray(
            ReadOnlySpan<NoirAestheticProfileDTO> source,
            int count,
            NativeArray<NoirAestheticProfileDTO> destination)
        {
            int safeCount = math.min(math.max(0, count), math.min(source.Length, destination.IsCreated ? destination.Length : 0));
            for (int i = 0; i < safeCount; i++)
                destination[i] = source[i];
            for (int i = safeCount; i < destination.Length; i++)
                destination[i] = default;
        }

        private static void CopyNoirColorProfilesToNativeArray(
            ReadOnlySpan<NoirColorProfileDTO> source,
            int count,
            NativeArray<NoirColorProfileDTO> destination)
        {
            int safeCount = math.min(math.max(0, count), math.min(source.Length, destination.IsCreated ? destination.Length : 0));
            for (int i = 0; i < safeCount; i++)
                destination[i] = source[i];
            for (int i = safeCount; i < destination.Length; i++)
                destination[i] = default;
        }

        private static int ParseAestheticCsv(
            ReadOnlySpan<byte> bytes,
            Span<NoirAestheticProfileDTO> profiles)
        {
            int limit = bytes.Length;
            int cursor = 0;
            int write = 0;
            while (cursor < limit && write < profiles.Length)
            {
                SkipCsvWhitespace(bytes, limit, ref cursor);
                if (cursor >= limit)
                    break;

                if (bytes[cursor] == (byte)'#' || bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r')
                {
                    SkipCsvLine(bytes, limit, ref cursor);
                    continue;
                }

                uint profileHash = ReadCsvTokenHash(bytes, limit, ref cursor);
                if (!TryReadCsvFloatField(bytes, limit, ref cursor, out float depthMin) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float depthMax) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float sanityMin) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float sanityMax) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float bilateralScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float historyScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float sharpenFloor) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float grainScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float vignetteScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float chromaScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float overkillScale))
                {
                    SkipCsvLine(bytes, limit, ref cursor);
                    continue;
                }

                if (profileHash != 0u)
                {
                    NoirAestheticProfileDTO profile = default;
                    profile.ProfileHash = profileHash;
                    profile.Flags = 1u;
                    profile.DepthMinMeters = math.min(depthMin, depthMax);
                    profile.DepthMaxMeters = math.max(depthMin, depthMax);
                    profile.SanityMin01 = math.saturate(math.min(sanityMin, sanityMax));
                    profile.SanityMax01 = math.saturate(math.max(sanityMin, sanityMax));
                    profile.ReconstructionParams = new float4(
                        math.max(0.1f, bilateralScale),
                        math.saturate(historyScale),
                        math.saturate(sharpenFloor),
                        math.max(0f, grainScale));
                    profile.OverkillParams = new float4(
                        math.max(0f, vignetteScale),
                        math.max(0f, chromaScale),
                        math.saturate(overkillScale),
                        0f);
                    profiles[write++] = profile;
                }

                SkipCsvLine(bytes, limit, ref cursor);
            }

            for (int i = write; i < profiles.Length; i++)
                profiles[i] = default;

            return write;
        }

        private void CacheAestheticProfileSnapshot(ReadOnlySpan<NoirAestheticProfileDTO> profiles, int count)
        {
            int safeCount = math.min(
                math.max(0, count),
                math.min(profiles.Length, _aestheticProfileCache.Length));
            for (int i = 0; i < safeCount; i++)
                _aestheticProfileCache[i] = profiles[i];
            for (int i = safeCount; i < _aestheticProfileCacheCount; i++)
                _aestheticProfileCache[i] = default;
            _aestheticProfileCacheCount = safeCount;
        }
#endif

        // Runtime consumer half of the aesthetic-profile route. The CSV loader above is editor-only
        // authoring I/O; this selector reads the already-cached snapshot and fails closed when the
        // cache is empty, which is exactly the editor behaviour with settings.loadAestheticCsv off.
        // It must compile into a player build or the reconstruction constant path has no caller.
        private bool TrySelectAestheticProfileSnapshot(
            Camera renderCamera,
            RuntimeState runtimeState,
            out NoirAestheticProfileDTO profile)
        {
            profile = default;
            int count = _aestheticProfileCacheCount;
            if (!_aestheticCsvLoaded || count <= 0 || renderCamera == null)
                return false;

            float depthMeters = ResolveAestheticProfileDepthMeters(renderCamera);
            float sanity01 = math.saturate(1f - runtimeState.PlayerStress01);
            for (int i = 0; i < count; i++)
            {
                NoirAestheticProfileDTO candidate = _aestheticProfileCache[i];
                if (candidate.ProfileHash == 0u || (candidate.Flags & 1u) == 0u)
                    continue;

                if (depthMeters >= candidate.DepthMinMeters &&
                    depthMeters <= candidate.DepthMaxMeters &&
                    sanity01 >= candidate.SanityMin01 &&
                    sanity01 <= candidate.SanityMax01)
                {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }

        private float ResolveAestheticProfileDepthMeters(Camera renderCamera)
        {
            IPlayerRuntimeContext playerContext = ResolveNoirPlayerContext();
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            return ResolveCameraDepthFromProductionSeaLevel(renderCamera);
        }

        private static float ResolveCameraDepthFromProductionSeaLevel(Camera renderCamera)
        {
            if (renderCamera == null)
                return 0f;

            Vector3 position = renderCamera.transform.position;
            float seaLevelY = ResolveProductionSeaLevelY();
            return math.isfinite(position.y) ? math.max(0f, seaLevelY - position.y) : 0f;
        }

        private static float ResolveProductionSeaLevelY()
        {
            Vector4 fogStratification = Shader.GetGlobalVector(ShaderConstants.NoirFogStratificationId);
            float waterLevelY = fogStratification.x;
            if (IsPublishedNoirFogStratification(in fogStratification) &&
                math.isfinite(waterLevelY) &&
                math.abs(waterLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                return waterLevelY;
            }

            return DefaultSeaLevelY;
        }

        private static bool IsPublishedNoirFogStratification(in Vector4 fogStratification)
        {
            return math.isfinite(fogStratification.y) &&
                   math.isfinite(fogStratification.z) &&
                   math.isfinite(fogStratification.w) &&
                   (math.abs(fogStratification.y) > 0.000001f ||
                    math.abs(fogStratification.z) > 0.000001f ||
                    math.abs(fogStratification.w) > 0.000001f);
        }

#if UNITY_EDITOR
        private static string ResolveAestheticCsvPath()
        {
            string root = Directory.GetCurrentDirectory();
            string path = Path.Combine(root, "Data", "Visuals", AestheticCsvFileName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(root, AestheticCsvFileName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(root, "Assets", "_Project", "Data", AestheticCsvFileName);
            return File.Exists(path) ? path : null;
        }
#endif

        // Runtime read of the DRS injected-scale Vault lane (UberNoirReconstructionVaultIds.MockSignal,
        // declared in the runtime contract assembly). ThermalDynamicResolutionAdapter already reads this
        // same buffer unguarded on its runtime tick; guarding it here would let the DRS adapter honour an
        // injected render scale while the visor reconstruction constants silently did not. Fails closed:
        // with no writer the handle/flags checks reject and BuildReconstructionConstants keeps the real
        // IResolutionScalerService values.
        private bool TryCopyMockReconstructionSignalSnapshot(out MockReconstructionInputSignal signal)
        {
            signal = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsReconstructionVaultHandle(in _mockSignalHandle, ReconstructionMockSignalVaultId))
                return false;

            if (!TryReadReconstructionVaultBuffer(
                    vault,
                    in _mockSignalHandle,
                    ReconstructionMockSignalVaultId,
                    1,
                    out NativeArray<MockReconstructionInputSignal>.ReadOnly mock))
            {
                return false;
            }

            signal = mock[0];
            return !vault.IsCompactionFenceActive &&
                   signal.Flags != 0u &&
                   math.isfinite(signal.RenderScale01) &&
                   math.isfinite(signal.GlobalQualityWeight01) &&
                   signal.RenderScale01 > 0f;
        }

        private bool TryUseCachedResolutionState(out ResolutionScaleState state)
        {
            IResolutionScalerService scaler = _noirResolutionScaler;
            if (scaler != null && scaler.TryGetScaleState(out state))
                return true;

            state = default;
            return false;
        }

        private float ResolveQualityPressure01(float memoryQualityPressureFloor01)
        {
            float qualityWeight01 = ResolveCurrentQualityWeight01(memoryQualityPressureFloor01);
            float qualityPressureFromWeight01 = 1f - Smooth01(math.saturate((qualityWeight01 - 0.18f) * 1.2195122f));
            return math.max(Sanitize01(memoryQualityPressureFloor01), qualityPressureFromWeight01);
        }

        private float ResolveCurrentQualityWeight01(float memoryQualityPressureFloor01)
        {
            ResolutionScaleState state;
            return TryUseCachedResolutionState(out state)
                ? Sanitize01(state.GlobalQualityWeight01)
                : math.lerp(1f, 0.35f, Sanitize01(memoryQualityPressureFloor01));
        }

        private static bool ReconstructionConstantsEqual(
            in UberNoirReconstructionConstantsDTO left,
            in UberNoirReconstructionConstantsDTO right)
        {
            return math.lengthsq(left.RenderScaleParams - right.RenderScaleParams) <= ReconstructionConstantsEpsilon * ReconstructionConstantsEpsilon &&
                   math.lengthsq(left.TemporalParams - right.TemporalParams) <= ReconstructionConstantsEpsilon * ReconstructionConstantsEpsilon &&
                   math.lengthsq(left.OverkillParams - right.OverkillParams) <= ReconstructionConstantsEpsilon * ReconstructionConstantsEpsilon;
        }

        private static float EstimateReconstructionCostMs(float scale, float radius, float temporalScale, bool depthlessTBDR)
        {
            float deficit = math.saturate(1f - math.min(scale, 1f));
            float bilateralCost = 0.035f + deficit * 0.055f + math.saturate(radius * 0.2f) * 0.012f;
            float temporalCost = temporalScale > 0.001f ? 0.05f * math.saturate(temporalScale) : 0f;
            float depthSave = depthlessTBDR ? 0.018f : 0f;
            return math.max(0f, bilateralCost + temporalCost - depthSave);
        }

        private static float ResolveAbSplit01(FeatureSettings currentSettings)
        {
            bool enabled = currentSettings != null && currentSettings.reconstructionAbSplit;
#if UNITY_EDITOR
            enabled |= s_editorAbSplit;
#endif
            return enabled ? 1f : 0f;
        }

        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - 2f * value);
        }

        private static float ResolveCompatibilityVisualOverkillWeight01(float quality01, float response01)
        {
            float quality = Sanitize01(quality01);
            float response = math.saturate(response01);
            float curve = quality * quality * math.lerp(0.5f, 1f, quality);
            return Smooth01(math.saturate(curve * math.lerp(0.65f, 1.35f, response)));
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

#if UNITY_EDITOR
        private static void SkipCsvWhitespace(ReadOnlySpan<byte> bytes, int limit, ref int cursor)
        {
            while (cursor < limit)
            {
                byte value = bytes[cursor];
                if (value != (byte)' ' && value != (byte)'\t')
                    return;
                cursor++;
            }
        }

        private static void SkipCsvLine(ReadOnlySpan<byte> bytes, int limit, ref int cursor)
        {
            while (cursor < limit && bytes[cursor] != (byte)'\n')
                cursor++;
            if (cursor < limit)
                cursor++;
        }

        private static uint ReadCsvTokenHash(ReadOnlySpan<byte> bytes, int limit, ref int cursor)
        {
            if (cursor < limit && bytes[cursor] == (byte)',')
                cursor++;

            SkipCsvWhitespace(bytes, limit, ref cursor);
            if (cursor < limit && bytes[cursor] == (byte)'"')
                cursor++;

            uint hash = 2166136261u;
            bool any = false;
            while (cursor < limit)
            {
                byte value = bytes[cursor];
                if (value == (byte)',' || value == (byte)'\n' || value == (byte)'\r' || value == (byte)'"')
                    break;

                if (value != (byte)' ' && value != (byte)'\t')
                {
                    if (value >= (byte)'A' && value <= (byte)'Z')
                        value = (byte)(value + 32);
                    hash ^= value;
                    hash *= 16777619u;
                    any = true;
                }

                cursor++;
            }

            while (cursor < limit && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n')
                cursor++;

            return any ? hash : 0u;
        }

        private static bool TryReadCsvFloatField(ReadOnlySpan<byte> bytes, int limit, ref int cursor, out float value)
        {
            value = 0f;
            if (cursor < limit && bytes[cursor] == (byte)',')
                cursor++;

            SkipCsvWhitespace(bytes, limit, ref cursor);
            int sign = 1;
            if (cursor < limit && (bytes[cursor] == (byte)'-' || bytes[cursor] == (byte)'+'))
            {
                sign = bytes[cursor] == (byte)'-' ? -1 : 1;
                cursor++;
            }

            bool hasDigits = false;
            float integer = 0f;
            while (cursor < limit)
            {
                byte digit = bytes[cursor];
                if (digit < (byte)'0' || digit > (byte)'9')
                    break;

                integer = integer * 10f + (digit - (byte)'0');
                hasDigits = true;
                cursor++;
            }

            float fraction = 0f;
            if (cursor < limit && bytes[cursor] == (byte)'.')
            {
                cursor++;
                float place = 0.1f;
                while (cursor < limit)
                {
                    byte digit = bytes[cursor];
                    if (digit < (byte)'0' || digit > (byte)'9')
                        break;

                    fraction += (digit - (byte)'0') * place;
                    place *= 0.1f;
                    hasDigits = true;
                    cursor++;
                }
            }

            while (cursor < limit && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n')
                cursor++;

            if (!hasDigits)
                return false;

            value = (integer + fraction) * sign;
            return math.isfinite(value);
        }
#endif

        private bool TryBuildRuntimeState(Camera renderCamera, FeatureSettings settings, float memoryQualityPressureFloor01, out RuntimeState runtimeState)
        {
            runtimeState = default;
            if (renderCamera == null || settings == null)
                return false;

            if (!TryUsePlayerContextSnapshot(
                    out Camera playerCamera,
                    out float wetLens,
                    out float hullStress,
                    out uint contextStatusMask) ||
                playerCamera == null ||
                !ReferenceEquals(renderCamera, playerCamera))
            {
                return false;
            }

            float healthFraction = 1f;
            float oxygen01 = 1f;
            float ambientPressure = math.max(1f, SanitizeFinite(_cachedAmbientPressureAtm, 1f));
            if (UIStateStore.IsInitialized)
            {
                if (UIStateStore.TryReadValue(UIValueSlotId.Health01, out UIValueSlot healthSlot))
                    healthFraction = Sanitize01(healthSlot.Value);

                if (UIStateStore.TryReadValue(UIValueSlotId.Oxygen01, out UIValueSlot oxygenSlot))
                    oxygen01 = Sanitize01(oxygenSlot.Value);

                if (UIStateStore.TryReadValue(UIValueSlotId.PressureAtm, out UIValueSlot pressureSlot))
                    ambientPressure = math.max(1f, SanitizeFinite(pressureSlot.Value, ambientPressure));
            }

            float localTemperature = _cachedLocalTemperature;
            float globalStress = _cachedPlayerStress01;
            float frequencyTuningError01 = _cachedFrequencyTuningError01;
            float vrComfortVignette01 = _cachedVrComfortVignette01;
            Vector4 vrComfortJerkState = _cachedVrComfortJerkState;
            float qualityPressure01 = ResolveQualityPressure01(memoryQualityPressureFloor01);
            Vector4 internalWaterlineParams = ResolveInternalWaterlineParams(
                renderCamera,
                settings,
                _cachedInternalWaterlineRuntime,
                _cachedInternalWaterlineY);
            Vector4 internalWaterlineDistortion = ResolveInternalWaterlineDistortion(
                qualityPressure01,
                _cachedInternalWaterlineDistortion);
            bool depthlessTBDR = ResolveDepthlessTBDRPath();
            float lightShaftActiveCount = _cachedLightShaftActiveCount;

            float visualBudget01 = 1f - qualityPressure01;
            float bulletTimeVisual01 = Sanitize01(SimulationSignalRoute.BulletTimeVisualIntensity01) * visualBudget01;
            float pressureSurge01 = ResolvePressureSurgeVisual01(ambientPressure, hullStress, qualityPressure01, settings);
            float playerStress = math.saturate(math.max(frequencyTuningError01, math.max(globalStress, math.max(hullStress, 1f - healthFraction))));
            playerStress = math.max(playerStress, math.max(bulletTimeVisual01, pressureSurge01 * 0.5f));
            float hypoxia = math.max(
                _cachedHypoxiaSignal01,
                ResolveHypoxiaFromOxygen(oxygen01, settings.hypoxiaSafeOxygen01));
            uint statusMask = contextStatusMask;

            bool hasActiveSignal =
                healthFraction < 0.999f ||
                wetLens > 0.001f ||
                hullStress > 0.001f ||
                playerStress > 0.001f ||
                bulletTimeVisual01 > 0.001f ||
                hypoxia > 0.001f ||
                vrComfortVignette01 > 0.001f ||
                math.max(vrComfortJerkState.x, vrComfortJerkState.y) > 0.001f ||
                statusMask != 0u ||
                ambientPressure > 1.001f ||
                pressureSurge01 > 0.001f ||
                lightShaftActiveCount > 0.001f ||
                internalWaterlineParams.y > 0.001f ||
                internalWaterlineParams.w > 0.001f ||
                frequencyTuningError01 > 0.001f ||
                math.abs(localTemperature) > TemperatureActivityThreshold ||
                settings.lensDirtTexture != null;

            runtimeState = default;
            runtimeState.VisorPostActive = hasActiveSignal ? (byte)1 : (byte)0;
            runtimeState.HealthFraction = healthFraction;
            runtimeState.LocalTemperature = localTemperature;
            runtimeState.AmbientPressure = ambientPressure;
            runtimeState.PlayerStress01 = playerStress;
            runtimeState.Hypoxia01 = hypoxia;
            runtimeState.Bleeding01 = (statusMask & BleedingStatusBit) != 0u ? 1f : 0f;
            runtimeState.WetLens01 = wetLens;
            runtimeState.HullStress01 = hullStress;
            runtimeState.AupShiftFrame = HectonFloatingOrigin.CurrentShiftSequence;
            runtimeState.VrComfortVignette01 = vrComfortVignette01;
            runtimeState.VrComfortJerkState = vrComfortJerkState;
            runtimeState.InternalWaterlineParams = internalWaterlineParams;
            runtimeState.InternalWaterlineDistortion = internalWaterlineDistortion;
            runtimeState.QualityPressure01 = qualityPressure01;
            runtimeState.DepthlessTBDR = depthlessTBDR ? (byte)1 : (byte)0;
            return true;
        }

        private static Vector4 ResolveInternalWaterlineParams(
            Camera renderCamera,
            FeatureSettings settings,
            Vector4 runtime,
            float waterlineY)
        {
            float active01 = Sanitize01(runtime.x);
            float droplets01 = Sanitize01(runtime.z);
            if ((active01 <= 0.001f && droplets01 <= 0.001f) || renderCamera == null || settings == null)
                return Vector4.zero;

            if (active01 <= 0.001f || !math.isfinite(waterlineY))
            {
                Vector4 inactiveResult = default;
                inactiveResult.w = droplets01;
                return inactiveResult;
            }

            Transform cameraTransform = renderCamera.transform;
            float cameraY = cameraTransform.position.y;
            float depthBelowWaterline = waterlineY - cameraY;
            float submerged01 = ResolveInternalWaterlineSubmergedWeight01FromDepth(depthBelowWaterline);
            if (depthBelowWaterline >= InternalWaterlineSplitBypassDepthMeters)
                submerged01 = 1f;
            float splitLine = InternalWaterlineFullScreenSplit;
            if (submerged01 < 0.999f)
            {
                float viewportSplit = ResolveInternalWaterlineViewportSplit(renderCamera, waterlineY, settings);
                splitLine = math.lerp(viewportSplit, InternalWaterlineFullScreenSplit, submerged01);
            }

            Vector4 result = default;
            result.x = splitLine;
            result.y = active01;
            result.z = submerged01;
            result.w = droplets01;
            return result;
        }

        private static float ResolveInternalWaterlineSubmergedWeight01(float cameraY, float waterlineY)
        {
            return ResolveInternalWaterlineSubmergedWeight01FromDepth(waterlineY - cameraY);
        }

        private static float ResolveInternalWaterlineSubmergedWeight01FromDepth(float depthBelowWaterline)
        {
            float fadeStart = InternalWaterlineSubmergeOffsetMeters - InternalWaterlineSubmergeFadeMeters;
            float fadeEnd = InternalWaterlineSubmergeOffsetMeters + InternalWaterlineSubmergeFadeMeters;
            float fadeRange = math.max(0.001f, fadeEnd - fadeStart);
            return Smooth01(math.saturate((depthBelowWaterline - fadeStart) * math.rcp(fadeRange)));
        }

        private static float ResolveInternalWaterlineViewportSplit(Camera renderCamera, float waterlineY, FeatureSettings settings)
        {
            Transform cameraTransform = renderCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            float flatForwardLengthSq = forward.x * forward.x + forward.z * forward.z;
            if (flatForwardLengthSq <= 0.000001f)
            {
                Vector3 up = cameraTransform.up;
                flatForwardLengthSq = up.x * up.x + up.z * up.z;
                forward = up;
            }

            if (flatForwardLengthSq > 0.000001f)
            {
                float invLength = math.rsqrt(flatForwardLengthSq);
                float sampleDistance = math.clamp(renderCamera.nearClipPlane + 0.5f, 0.5f, math.max(0.5f, renderCamera.farClipPlane * 0.02f));
                Vector3 planeSample = default;
                planeSample.x = cameraPosition.x + forward.x * invLength * sampleDistance;
                planeSample.y = waterlineY;
                planeSample.z = cameraPosition.z + forward.z * invLength * sampleDistance;
                Vector3 viewportPoint = renderCamera.WorldToViewportPoint(planeSample);
                if (math.isfinite(viewportPoint.y) && viewportPoint.z > 0f)
                    return math.clamp(viewportPoint.y, -0.1f, 1.1f);
            }

            float pitchY = math.clamp(cameraTransform.forward.y, -1f, 1f);
            return math.saturate(
                0.5f +
                (waterlineY - cameraPosition.y) * math.max(0.02f, settings.internalWaterlineMetersToScreen) -
                pitchY * math.saturate(settings.internalWaterlinePitchScale));
        }

        private static Vector4 ResolveInternalWaterlineDistortion(float qualityPressure01, Vector4 distortion)
        {
            qualityPressure01 = Sanitize01(qualityPressure01);
            float visualBudget01 = 1f - qualityPressure01;
            Vector4 result = default;
            result.x = math.max(0f, SanitizeFinite(distortion.x, 0f)) * visualBudget01;
            result.y = Sanitize01(distortion.y);
            result.z = math.max(0.001f, SanitizeFinite(distortion.z, 0.018f));
            result.w = math.lerp(Sanitize01(distortion.w), 1f, qualityPressure01);
            return result;
        }

        private static float ResolvePressureSurgeVisual01(
            float ambientPressure,
            float hullStress01,
            float qualityPressure01,
            FeatureSettings currentSettings)
        {
            float safeRange = currentSettings != null ? math.max(0.0001f, currentSettings.pressureInvRange) : 1f;
            float pressureDrive01 = math.saturate((math.max(1f, SanitizeFinite(ambientPressure, 1f)) - 1f) * math.rcp(safeRange));
            float stressDrive01 = Sanitize01(hullStress01) * 0.35f;
            float visualBudget01 = 1f - Sanitize01(qualityPressure01);
            return Smooth01(math.max(pressureDrive01, stressDrive01)) * visualBudget01;
        }

        private static bool TryUsePlayerContextSnapshot(
            IPlayerRuntimeContext playerContext,
            out Camera playerCamera,
            out float wetLens01,
            out float hullStress01,
            out uint statusMask)
        {
            if (playerContext == null)
            {
                playerCamera = null;
                wetLens01 = 0f;
                hullStress01 = 0f;
                statusMask = 0u;
                return false;
            }

            playerCamera = playerContext.PlayerCamera;
            statusMask = 0u;
            if (playerContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState) &&
                (survivalState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u)
            {
                statusMask = survivalState.StatusMask;
            }

            hullStress01 = 0f;
            if (playerContext.TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState stressState))
                hullStress01 = Sanitize01(stressState.HullStress01);

            var movement = playerContext.PlayerMovement;
            wetLens01 = movement != null ? Sanitize01(movement.CurrentWetLensIntensity01) : 0f;
            return playerCamera != null;
        }

        private bool TryUsePlayerContextSnapshot(
            out Camera playerCamera,
            out float wetLens01,
            out float hullStress01,
            out uint statusMask)
        {
            return TryUsePlayerContextSnapshot(
                ResolveNoirPlayerContext(),
                out playerCamera,
                out wetLens01,
                out hullStress01,
                out statusMask);
        }

        private float ResolveMemoryQualityPressureFloor01()
        {
            return _cachedMemoryQualityPressureFloor01;
        }

        private bool ResolveDepthlessTBDRPath()
        {
            if (!_depthlessTBDRPlatformClassified)
                return false;

            int frame = NoirFrameToIndex(ResolveNoirFrameId());
            if (_cachedDepthlessTBDRFrame == frame)
                return _cachedDepthlessTBDR;

            _cachedDepthlessTBDRFrame = frame;
            _cachedDepthlessTBDR = _depthlessTBDRPlatformCandidate && HectonXRRuntimeState.IsXRActive;
            return _cachedDepthlessTBDR;
        }

        private void RefreshDepthlessTBDRPlatformCandidate()
        {
            _depthlessTBDRPlatformCandidate = IsQuestVulkanDepthlessCandidate();
            _depthlessTBDRPlatformClassified = true;
            _cachedDepthlessTBDRFrame = int.MinValue;
        }

        private void CachePlatformCapabilitiesCold(FeatureSettings currentSettings)
        {
            _supportsReconstructionConstantBuffer = SystemInfo.supportsSetConstantBuffer;
            _noirSupportsSetConstantBufferCold = _supportsReconstructionConstantBuffer;
            RefreshMemoryQualityPressureFloorCold(currentSettings);
            RefreshDepthlessTBDRPlatformCandidate();
        }

        private void RefreshMemoryQualityPressureFloorCold(FeatureSettings currentSettings)
        {
            int thresholdMb = currentSettings != null ? math.max(256, currentSettings.minimumQualityVideoMemoryMb) : 2048;
            _cachedGraphicsMemoryMb = SystemInfo.graphicsMemorySize;
            _cachedMinimumQualityThresholdMb = thresholdMb;
            float memoryMb = math.max(1f, _cachedGraphicsMemoryMb);
            float softCeilingMb = thresholdMb * 1.25f;
            float softRangeMb = math.max(1f, thresholdMb * 0.5f);
            float memoryShortage01 = Smooth01(math.saturate((softCeilingMb - memoryMb) * math.rcp(softRangeMb)));
            float memoryKnown01 = math.saturate((float)_cachedGraphicsMemoryMb);
            _cachedMemoryQualityPressureFloor01 = 0.25f * memoryKnown01 * memoryShortage01;
        }

        private void CachePresentationGlobalsLate()
        {
            _cachedAmbientPressureAtm = math.max(1f, SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.AmbientPressureGlobalId), 1f));
            _cachedLocalTemperature = SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.LocalTemperatureGlobalId), 0f);
            _cachedPlayerStress01 = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.PlayerStressGlobalId));
            _cachedFrequencyTuningError01 = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.FrequencyTuningErrorGlobalId));
            _cachedVrComfortVignette01 = math.max(
                Sanitize01(Shader.GetGlobalFloat(ShaderConstants.VrComfortVignette01Id)),
                Sanitize01(Shader.GetGlobalFloat(ShaderConstants.SomaticComfortVignetteId)));
            _cachedVrComfortJerkState = SanitizeVrComfortJerkState(Shader.GetGlobalVector(ShaderConstants.VrComfortJerkStateId));
            _cachedInternalWaterlineRuntime = Shader.GetGlobalVector(ShaderConstants.InternalWaterlineRuntimeId);
            _cachedInternalWaterlineY = SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.InternalWaterlineYId), float.NegativeInfinity);
            _cachedInternalWaterlineDistortion = Shader.GetGlobalVector(ShaderConstants.InternalWaterlineDistortionId);
            _cachedLightShaftActiveCount = math.max(0f, SanitizeFinite(Shader.GetGlobalVector(ShaderConstants.LightShaftParamsId).x, 0f));
            _cachedHypoxiaSignal01 = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.HypoxiaSignalGlobalId));
        }

        private static bool IsQuestVulkanDepthlessCandidate()
        {
#if UNITY_ANDROID
            if (Application.platform != RuntimePlatform.Android ||
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
            {
                return false;
            }

            int systemMemoryMb = math.max(0, SystemInfo.systemMemorySize);
            if (systemMemoryMb > 0 && systemMemoryMb < QuestFamilyMemoryCeilingMegabytes)
                return true;

            string model = SystemInfo.deviceModel;
            return !string.IsNullOrEmpty(model) &&
                   model.IndexOf("quest", StringComparison.OrdinalIgnoreCase) >= 0;
#else
            return false;
#endif
        }

        private static float ResolveHypoxiaFromOxygen(float oxygen01, float safeThreshold)
        {
            float safe = math.clamp(safeThreshold, 0.01f, 1f);
            float oxygen = Sanitize01(oxygen01);
            return oxygen < safe ? math.saturate(1f - oxygen * math.rcp(safe)) : 0f;
        }

        private static Material ResolvePostMaterial(FeatureSettings currentSettings)
        {
            if (currentSettings == null)
                return null;

            if (currentSettings.deepSeaNoirUnifiedPass)
                return currentSettings.noirMaterial != null ? currentSettings.noirMaterial : currentSettings.material;

            return currentSettings.material;
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static Vector4 SanitizeVrComfortJerkState(Vector4 value)
        {
            Vector4 result = default;
            result.x = Sanitize01(value.x);
            result.y = Sanitize01(value.y);
            result.z = math.isfinite(value.z) ? math.max(0f, value.z) : 0f;
            result.w = Sanitize01(value.w);
            return result;
        }

        private static Vector4 SanitizeInternalWaterlineParams(Vector4 value)
        {
            Vector4 result = default;
            result.x = math.isfinite(value.x) ? math.clamp(value.x, -0.1f, 1.1f) : 0f;
            result.y = Sanitize01(value.y);
            result.z = Sanitize01(value.z);
            result.w = Sanitize01(value.w);
            return result;
        }

        private static Vector4 SanitizeInternalWaterlineDistortion(Vector4 value)
        {
            Vector4 result = default;
            result.x = math.isfinite(value.x) ? math.clamp(value.x, 0f, 0.006f) : 0f;
            result.y = Sanitize01(value.y);
            result.z = math.isfinite(value.z) ? math.clamp(value.z, 0.001f, 0.1f) : 0.018f;
            result.w = Sanitize01(value.w);
            return result;
        }
    }
}
