using System;
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
    /// Unified fullscreen visor post pass for damage chroma, heat haze, pressure warp, crack reveal, dirt, stress, hypoxia, and blood edge tint.
    /// </summary>
    public sealed partial class HectonVisorUberPostFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener, ILateFrameTickable
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/HectonVisorUberPost.shader";
        private const string ReconstructionShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_BilateralUpsample.shader";
#endif

        private const float ReconstructionConstantsEpsilon = 0.0001f;
        private const float DefaultHypoxiaSafeOxygen01 = 0.22f;
        private const float TemperatureActivityThreshold = 0.001f;
        private const uint BleedingStatusBit = 1u;
        private const int QuestFamilyMemoryCeilingMegabytes = 9000;
        private const int ReconstructionTelemetryCapacity = 300;
        private const int AestheticProfileCapacity = 32;
        private const int CsvScratchBytes = 16 * 1024;
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
        private const string ReconstructionDumpFileName = "Dump_UBER_NOIR.bin";
        private const string AestheticCsvFileName = "noir_aesthetic_profiles.csv";
        private static readonly ICameraHistoryReadAccess.HistoryRequestDelegate s_requestRawColorHistory =
            RequestRawColorHistory;

        [Serializable]
        private sealed partial class FeatureSettings
        {
            [Tooltip("Hidden fullscreen shader used for the unified visor post pass.")]
            public Shader shader = null;

            [Tooltip("Hidden fullscreen shader used for bilateral DRS reconstruction.")]
            public Shader reconstructionShader = null;

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

            [Tooltip("GlobalQualityWeight threshold where visual overkill reaches full shader budget.")]
            [Range(0f, 1f)] public float visualOverkillThreshold = 0.84f;

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

            [Tooltip("Heat haze sine frequency.")]
            [Min(1f)] public float heatHazeFrequency = 38f;

            [Tooltip("Heat haze sine speed.")]
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

        private readonly struct RuntimeState
        {
            public RuntimeState(
                bool visorPostActive,
                float healthFraction,
                float localTemperature,
                float ambientPressure,
                float playerStress01,
                float hypoxia01,
                uint statusMask,
                float wetLens01,
                float hullStress01,
                uint aupShiftFrame,
                float vrComfortVignette01,
                Vector4 vrComfortJerkState,
                Vector4 internalWaterlineParams,
                Vector4 internalWaterlineDistortion,
                float qualityPressure01,
                bool depthlessTBDR)
            {
                VisorPostActive = visorPostActive ? (byte)1 : (byte)0;
                HealthFraction = healthFraction;
                LocalTemperature = localTemperature;
                AmbientPressure = ambientPressure;
                PlayerStress01 = playerStress01;
                Hypoxia01 = hypoxia01;
                Bleeding01 = (statusMask & BleedingStatusBit) != 0u ? 1f : 0f;
                WetLens01 = wetLens01;
                HullStress01 = hullStress01;
                AupShiftFrame = aupShiftFrame;
                VrComfortVignette01 = vrComfortVignette01;
                VrComfortJerkState = vrComfortJerkState;
                InternalWaterlineParams = internalWaterlineParams;
                InternalWaterlineDistortion = internalWaterlineDistortion;
                QualityPressure01 = qualityPressure01;
                DepthlessTBDR = depthlessTBDR ? (byte)1 : (byte)0;
            }

            public readonly byte VisorPostActive;
            public readonly float HealthFraction;
            public readonly float LocalTemperature;
            public readonly float AmbientPressure;
            public readonly float PlayerStress01;
            public readonly float Hypoxia01;
            public readonly float Bleeding01;
            public readonly float WetLens01;
            public readonly float HullStress01;
            public readonly uint AupShiftFrame;
            public readonly float VrComfortVignette01;
            public readonly Vector4 VrComfortJerkState;
            public readonly Vector4 InternalWaterlineParams;
            public readonly Vector4 InternalWaterlineDistortion;
            public readonly float QualityPressure01;
            public readonly byte DepthlessTBDR;
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
            public uint _pad0;
            [FieldOffset(60)]
            public uint _pad1;
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
                internal Material Material;
                internal Texture CrackTexture;
                internal Texture LensDirtTexture;
                internal Texture BlueNoiseTexture;
                internal Texture VrComfortMaskTexture;
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
                    TextureDesc reconstructionDesc = new TextureDesc(sourceDesc);
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

                TextureDesc destinationDesc = new TextureDesc(sourceDesc);
                destinationDesc.name = "_HectonVisorUberPost";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PostPassData>(
                           "Hecton Visor Uber Post",
                           out PostPassData passData,
                           _profilingSampler))
                {
                    passData.Source = postSourceTexture;
                    passData.Depth = depthTexture;
                    passData.Material = _material;
                    passData.HasDepth = hasDepth;
                    PopulatePostPassData(passData, _settings, _runtimeState, _visualTimeSeconds);

                    builder.UseTexture(postSourceTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    if (hasDepth)
                        builder.UseTexture(depthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PostPassData data, RasterGraphContext context) =>
                    {
                        if (data.Material == null)
                            return;

                        context.cmd.SetGlobalTexture(ShaderConstants.BlitTextureId, data.Source);
                        if (data.HasDepth)
                            context.cmd.SetGlobalTexture(ShaderConstants.CameraDepthTextureId, data.Depth);
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
                passData.Strengths0 = new Vector4(
                    math.saturate(settings.chromaticStrength),
                    math.saturate(settings.hypoxiaDesaturationStrength),
                    math.clamp(settings.pressureWarpStrength, 0f, 0.18f),
                    math.saturate(settings.crackStrength));
                passData.Strengths1 = new Vector4(
                    math.max(0f, settings.pressureInvRange),
                    math.max(0f, settings.temperatureScale),
                    math.clamp(settings.crackUvStrength, 0f, 0.01f),
                    math.saturate(settings.lensDirtAndBloodStrength));
                passData.WaveParams = new Vector4(
                    math.max(1f, settings.heatHazeFrequency),
                    math.max(0f, settings.heatHazeSpeed),
                    math.clamp(settings.heatHazeAmplitude, 0f, 0.006f) * (1f - qualityPressure01),
                    math.saturate(settings.damageVignetteStrength));
                passData.TextureFlags = new Vector4(
                    settings.crackTexture != null ? 1f : 0f,
                    settings.lensDirtTexture != null ? 1f : 0f,
                    settings.blueNoiseTexture != null ? 1f : 0f,
                    settings.vrComfortMaskTexture != null ? 1f : 0f);
                passData.CrackTexture = settings.crackTexture != null ? settings.crackTexture : Texture2D.blackTexture;
                passData.LensDirtTexture = settings.lensDirtTexture != null ? settings.lensDirtTexture : Texture2D.whiteTexture;
                passData.BlueNoiseTexture = settings.blueNoiseTexture != null ? settings.blueNoiseTexture : Texture2D.grayTexture;
                passData.VrComfortMaskTexture = settings.vrComfortMaskTexture != null ? settings.vrComfortMaskTexture : Texture2D.grayTexture;
            }

            private static void BindPostShaderParameters(RasterCommandBuffer cmd, PostPassData data)
            {
                cmd.SetGlobalTexture(ShaderConstants.CrackTextureId, data.CrackTexture);
                cmd.SetGlobalTexture(ShaderConstants.LensDirtTextureId, data.LensDirtTexture);
                cmd.SetGlobalTexture(ShaderConstants.BlueNoiseTextureId, data.BlueNoiseTexture);
                cmd.SetGlobalTexture(ShaderConstants.VrComfortMaskTextureId, data.VrComfortMaskTexture);

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
        private static float s_editorVisualOverkillThreshold01 = 0.84f;
        private static float s_editorMockRenderScale01 = 0.5f;
        private static float s_editorMockQualityWeight01 = 0.35f;

        public static void SetEditorReconstructionOverride(
            bool active,
            float bilateralRadiusPixels,
            float temporalHistoryWeight01,
            float sharpeningClamp01,
            float filmGrainStrength01,
            float visualOverkillThreshold01,
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
            s_editorVisualOverkillThreshold01 = math.saturate(visualOverkillThreshold01);
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
                IsReconstructionVaultHandle(in handle, ReconstructionConstantsVaultId) &&
                vault.TryLockBuffer(ReconstructionConstantsVaultId, SystemID.GraphicsScalability))
            {
                try
                {
                    if (TryReadReconstructionVaultBuffer(
                            vault,
                            in handle,
                            ReconstructionConstantsVaultId,
                            1,
                            out NativeArray<UberNoirReconstructionConstantsDTO> buffer))
                    {
                        constants = buffer[0];
                        return true;
                    }
                }
                finally
                {
                    vault.TryUnlockBuffer(ReconstructionConstantsVaultId, SystemID.GraphicsScalability);
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
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.EnsureGenerationHandle<MockReconstructionInputSignal>(
                    ReconstructionMockSignalVaultId,
                    1,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsReconstructionVaultHandle(in handle, ReconstructionMockSignalVaultId) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.GraphicsScalability, out NativeArray<MockReconstructionInputSignal> mockBuffer))
            {
                return false;
            }

            try
            {
                if (!mockBuffer.IsCreated || mockBuffer.Length <= 0)
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
#if UNITY_EDITOR
            TryAssignNoirShaderEditor();
            if (settings != null && settings.shader == null)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
            if (settings != null && settings.reconstructionShader == null)
                settings.reconstructionShader = AssetDatabase.LoadAssetAtPath<Shader>(ReconstructionShaderAssetPath);
#endif

            RefreshNoirCachedDependenciesCold();
            TryRegisterHotSwapListener();

            // COLD ALLOC: VisorUberPostPass[1] - reused ScriptableRenderPass instance - owner: HectonVisorUberPostFeature
            _pass ??= new VisorUberPostPass();
            EnsureNoirPassCold();
            Shader shader = settings != null ? settings.shader : null;
            Shader reconstructionShader = settings != null && !settings.deepSeaNoirUnifiedPass ? settings.reconstructionShader : null;
            if (shader == null && reconstructionShader == null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
                CoreUtils.Destroy(_reconstructionMaterial);
                _reconstructionMaterial = null;
                return;
            }

            RecreateMaterial(ref _material, shader);
            RecreateMaterial(ref _reconstructionMaterial, reconstructionShader);
            if (settings != null && settings.deepSeaNoirUnifiedPass)
            {
                EnsureNoirConstantsBuffersCold();
                EnsureNoirVaultHandles();
                _noirColorCsvLoadAttempted = false;
#if UNITY_EDITOR
                if (settings.loadNoirColorCsv)
                    TryLoadNoirColorCsvCold();
#endif
                TryRegisterLateFrameTickable();
                return;
            }

            EnsureReconstructionConstantsBufferCold();
            EnsureReconstructionVaultHandles();
            _aestheticCsvLoadAttempted = false;
            _aestheticProfileCacheCount = 0;
#if UNITY_EDITOR
            if (settings != null && settings.loadAestheticCsv)
                TryLoadAestheticCsvCold();
#endif
            TryRegisterLateFrameTickable();
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || _pass == null || (_material == null && _reconstructionMaterial == null))
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
                return;
            }

            if (settings.deepSeaNoirUnifiedPass)
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
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

            Camera renderCamera = renderingData.cameraData.camera;
            float memoryQualityPressureFloor01 = ResolveMemoryQualityPressureFloor01(settings);
            if (!TryBuildRuntimeState(renderCamera, settings, memoryQualityPressureFloor01, out RuntimeState runtimeState))
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
                return;
            }

            bool reconstructionStorageReady = IsReconstructionConstantsBufferReady();
            if (!ReconstructionVaultHandlesReady())
            {
                ClearRawColorHistoryRequest();
                ClearPendingReconstructionInput();
                return;
            }

            bool requestRawColorHistory = reconstructionStorageReady &&
                                          ShouldRequestRawColorHistory(settings, runtimeState);
            UpdateRawColorHistoryRequest(renderCamera, requestRawColorHistory);
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
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
            ClearRawColorHistoryRequest();
            ClearPendingReconstructionInput();
            ReleaseNoirVaultHandles(_dataVault);
            ReleaseReconstructionVaultHandles(_dataVault);
            CoreUtils.Destroy(_material);
            _material = null;
            CoreUtils.Destroy(_reconstructionMaterial);
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
            _noirPlayerContext = null;
            _noirResolutionScaler = null;
            _nextNoirPlayerRefreshFrame = 0;
            _hasReconstructionConstants = false;
            _aestheticCsvLoaded = false;
            _aestheticCsvLoadAttempted = false;
            _aestheticProfileCacheCount = 0;
            _noirColorProfileCacheCount = 0;
        }

        private void UpdateRawColorHistoryRequest(Camera renderCamera, bool requestRawColorHistory)
        {
            if (!requestRawColorHistory)
            {
                ClearRawColorHistoryRequest();
                return;
            }

            if (_rawColorHistoryRequestRegistered &&
                ReferenceEquals(_rawColorHistoryCamera, renderCamera) &&
                _rawColorHistoryReadAccess != null)
            {
                return;
            }

            if (!TryResolveHistoryReadAccess(renderCamera, out ICameraHistoryReadAccess historyReadAccess))
            {
                ClearRawColorHistoryRequest();
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

        private static bool TryResolveHistoryReadAccess(Camera renderCamera, out ICameraHistoryReadAccess historyReadAccess)
        {
            if (renderCamera != null &&
                renderCamera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
            {
                historyReadAccess = additionalCameraData.history;
                return historyReadAccess != null;
            }

            historyReadAccess = null;
            return false;
        }

        private static void RequestRawColorHistory(IPerFrameHistoryAccessTracker historyAccess)
        {
            historyAccess?.RequestAccess<RawColorHistory>();
        }

        private bool EnsureReconstructionConstantsBufferCold()
        {
            if (!SystemInfo.supportsSetConstantBuffer)
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
            return SystemInfo.supportsSetConstantBuffer &&
                   _reconstructionConstantsBufferA != null &&
                   _reconstructionConstantsBufferB != null &&
                   _reconstructionConstantsBufferA.IsValid() &&
                   _reconstructionConstantsBufferB.IsValid();
        }

        private bool EnsureReconstructionVaultHandles()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

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
            ReleaseReconstructionVaultHandle(vault, ref _reconstructionConstantsHandle, ReconstructionConstantsVaultId);
            ReleaseReconstructionVaultHandle(vault, ref _reconstructionTelemetryHandle, ReconstructionTelemetryVaultId);
            ReleaseReconstructionVaultHandle(vault, ref _aestheticProfileHandle, ReconstructionProfileVaultId);
            ReleaseReconstructionVaultHandle(vault, ref _csvScratchHandle, ReconstructionCsvScratchVaultId);
            ReleaseReconstructionVaultHandle(vault, ref _mockSignalHandle, ReconstructionMockSignalVaultId);

            ClearReconstructionVaultHandles();
        }

        private bool EnsureReconstructionVaultHandle<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options)
            where T : struct
        {
            if (_dataVault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (TryReadReconstructionVaultBuffer(_dataVault, in handle, bufferId, requiredLength, out NativeArray<T> _))
                return true;

            handle = _dataVault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GraphicsScalability,
                options);

            if (TryReadReconstructionVaultBuffer(_dataVault, in handle, bufferId, requiredLength, out NativeArray<T> _))
                return true;

            ReleaseReconstructionVaultHandle(_dataVault, ref handle, bufferId);
            return false;
        }

        private static void ReleaseReconstructionVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            if (vault != null && IsReconstructionVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryResolveReconstructionVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            return TryOpenReconstructionVaultBuffer(vault, in handle, bufferId, requiredLength, readOnly: false, out buffer);
        }

        private static bool TryReadReconstructionVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            return TryOpenReconstructionVaultBuffer(vault, in handle, bufferId, requiredLength, readOnly: true, out buffer);
        }

        private static bool TryOpenReconstructionVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            bool readOnly,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength < 0 ||
                !IsReconstructionVaultHandle(in handle, bufferId))
            {
                return false;
            }

            bool opened = readOnly
                ? vault.TryReadHandle(in handle, out buffer)
                : vault.TryResolveHandle(in handle, out buffer);

            return opened &&
                   buffer.IsCreated &&
                   (requiredLength == 0 || buffer.Length >= requiredLength);
        }

        private static bool IsReconstructionVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
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
            float overkillThresholdSetting = currentSettings != null ? currentSettings.visualOverkillThreshold : 0.84f;
            float mockJitterPixels = -1f;
            float mockTemporalStress01 = 0f;

            if (TryLockAndCopyMockReconstructionSignal(out MockReconstructionInputSignal mockSignal))
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
                overkillThresholdSetting = math.saturate(s_editorVisualOverkillThreshold01);
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
            float threshold = math.clamp(overkillThresholdSetting, 0.001f, 0.999f);
            float overkill01 = math.max(visualOverkill01, Smooth01(math.saturate((quality01 - threshold) * math.rcp(1f - threshold))));
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

            _lastReconstructionConstants = constants;
            _hasReconstructionConstants = true;
            _activeReconstructionConstantsBuffer = target;
            WriteReconstructionConstantsToVault(in constants);
            return true;
        }

        private unsafe void WriteReconstructionConstantsToVault(in UberNoirReconstructionConstantsDTO constants)
        {
            if (_dataVault == null ||
                !IsReconstructionVaultHandle(in _reconstructionConstantsHandle, ReconstructionConstantsVaultId) ||
                !_dataVault.TryLockBuffer(ReconstructionConstantsVaultId, SystemID.GraphicsScalability))
            {
                return;
            }

            try
            {
                if (TryResolveReconstructionVaultBuffer(
                        _dataVault,
                        in _reconstructionConstantsHandle,
                        ReconstructionConstantsVaultId,
                        1,
                        out NativeArray<UberNoirReconstructionConstantsDTO> buffer))
                {
                    buffer[0] = constants;
                }
            }
            finally
            {
                _dataVault.TryUnlockBuffer(ReconstructionConstantsVaultId, SystemID.GraphicsScalability);
            }
        }

        private unsafe void RecordReconstructionTelemetry(
            in UberNoirReconstructionConstantsDTO constants,
            RuntimeState runtimeState,
            bool reconstructionBufferReady)
        {
            if (!ReconstructionVaultHandlesReady() ||
                _dataVault == null ||
                !_dataVault.TryLockBuffer(ReconstructionTelemetryVaultId, SystemID.GraphicsScalability))
            {
                return;
            }

            try
            {
                if (!TryResolveReconstructionVaultBuffer(
                        _dataVault,
                        in _reconstructionTelemetryHandle,
                        ReconstructionTelemetryVaultId,
                        ReconstructionTelemetryCapacity,
                        out NativeArray<ReconstructionTelemetryEntry> telemetry))
                {
                    return;
                }

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
                entry._pad0 = 0u;
                entry._pad1 = 0u;
                telemetry[index] = entry;

                index++;
                _reconstructionTelemetryCursor = index >= telemetry.Length ? 0 : index;
                if (scale < 0.4f && !_reconstructionDumpWritten)
                    _reconstructionDumpWritten = TryDumpReconstructionTelemetry();
            }
            finally
            {
                _dataVault.TryUnlockBuffer(ReconstructionTelemetryVaultId, SystemID.GraphicsScalability);
            }
        }

        private unsafe bool TryDumpReconstructionTelemetry()
        {
            if (_dataVault == null ||
                !TryReadReconstructionVaultBuffer(
                    _dataVault,
                    in _reconstructionTelemetryHandle,
                    ReconstructionTelemetryVaultId,
                    ReconstructionTelemetryCapacity,
                    out NativeArray<ReconstructionTelemetryEntry> telemetry))
            {
                return false;
            }

            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, ReconstructionDumpFileName);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
                int totalBytes = telemetry.Length * UnsafeUtility.SizeOf<ReconstructionTelemetryEntry>();
                byte* chunk = stackalloc byte[1024];
                int offset = 0;
                while (offset < totalBytes)
                {
                    int count = math.min(1024, totalBytes - offset);
                    UnsafeUtility.MemCpy(chunk, source + offset, count);
                    stream.Write(new ReadOnlySpan<byte>(chunk, count));
                    offset += count;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        private unsafe bool TryLoadAestheticCsvCold()
        {
            if (_aestheticCsvLoaded || _aestheticCsvLoadAttempted)
                return _aestheticCsvLoaded;

            if (!EnsureReconstructionVaultHandles() || _dataVault == null)
                return false;

            string path = ResolveAestheticCsvPath();
            _aestheticCsvLoadAttempted = true;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!_dataVault.TryLockBuffer(ReconstructionCsvScratchVaultId, SystemID.GraphicsScalability))
            {
                _aestheticCsvLoadAttempted = false;
                return false;
            }

            bool profileLocked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(ReconstructionProfileVaultId, SystemID.GraphicsScalability))
                {
                    _aestheticCsvLoadAttempted = false;
                    return false;
                }
                profileLocked = true;

                if (!TryResolveReconstructionVaultBuffer(
                        _dataVault,
                        in _csvScratchHandle,
                        ReconstructionCsvScratchVaultId,
                        CsvScratchBytes,
                        out NativeArray<byte> scratch) ||
                    !TryResolveReconstructionVaultBuffer(
                        _dataVault,
                        in _aestheticProfileHandle,
                        ReconstructionProfileVaultId,
                        AestheticProfileCapacity,
                        out NativeArray<NoirAestheticProfileDTO> profiles))
                {
                    return false;
                }

                int read;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    int max = math.min(scratch.Length, CsvScratchBytes);
                    void* scratchPtr = scratch.GetUnsafePtr();
                    read = stream.Read(new Span<byte>(scratchPtr, max));
                }

                if (read <= 0)
                    return false;

                int parsed = ParseAestheticCsv(scratch, read, profiles);
                CacheAestheticProfileSnapshot(profiles, parsed);
                _aestheticCsvLoaded = parsed > 0;
                return _aestheticCsvLoaded;
            }
            finally
            {
                if (profileLocked)
                    _dataVault.TryUnlockBuffer(ReconstructionProfileVaultId, SystemID.GraphicsScalability);
                _dataVault.TryUnlockBuffer(ReconstructionCsvScratchVaultId, SystemID.GraphicsScalability);
            }
        }

        private static int ParseAestheticCsv(
            NativeArray<byte> bytes,
            int length,
            NativeArray<NoirAestheticProfileDTO> profiles)
        {
            int limit = math.min(length, bytes.Length);
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

        private void CacheAestheticProfileSnapshot(NativeArray<NoirAestheticProfileDTO> profiles, int count)
        {
            int safeCount = math.min(
                math.max(0, count),
                math.min(profiles.IsCreated ? profiles.Length : 0, _aestheticProfileCache.Length));
            for (int i = 0; i < safeCount; i++)
                _aestheticProfileCache[i] = profiles[i];
            for (int i = safeCount; i < _aestheticProfileCacheCount; i++)
                _aestheticProfileCache[i] = default;
            _aestheticProfileCacheCount = safeCount;
        }

        private bool TrySelectAestheticProfileSnapshot(
            Camera renderCamera,
            RuntimeState runtimeState,
            out NoirAestheticProfileDTO profile)
        {
            profile = default;
            int count = _aestheticProfileCacheCount;
            if (!_aestheticCsvLoaded || count <= 0 || renderCamera == null)
                return false;

            float depthMeters = math.max(0f, -renderCamera.transform.position.y);
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

        private bool TryLockAndCopyMockReconstructionSignal(out MockReconstructionInputSignal signal)
        {
            signal = default;
            if (_dataVault == null ||
                !IsReconstructionVaultHandle(in _mockSignalHandle, ReconstructionMockSignalVaultId))
                return false;

            if (!_dataVault.TryLockBuffer(ReconstructionMockSignalVaultId, SystemID.GraphicsScalability))
                return false;

            try
            {
                if (!TryReadReconstructionVaultBuffer(
                        _dataVault,
                        in _mockSignalHandle,
                        ReconstructionMockSignalVaultId,
                        1,
                        out NativeArray<MockReconstructionInputSignal> mock))
                {
                    return false;
                }

                signal = mock[0];
                return signal.Flags != 0u &&
                       math.isfinite(signal.RenderScale01) &&
                       math.isfinite(signal.GlobalQualityWeight01) &&
                       signal.RenderScale01 > 0f;
            }
            finally
            {
                _dataVault.TryUnlockBuffer(ReconstructionMockSignalVaultId, SystemID.GraphicsScalability);
            }
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

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static void SkipCsvWhitespace(NativeArray<byte> bytes, int limit, ref int cursor)
        {
            while (cursor < limit)
            {
                byte value = bytes[cursor];
                if (value != (byte)' ' && value != (byte)'\t')
                    return;
                cursor++;
            }
        }

        private static void SkipCsvLine(NativeArray<byte> bytes, int limit, ref int cursor)
        {
            while (cursor < limit && bytes[cursor] != (byte)'\n')
                cursor++;
            if (cursor < limit)
                cursor++;
        }

        private static uint ReadCsvTokenHash(NativeArray<byte> bytes, int limit, ref int cursor)
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

        private static bool TryReadCsvFloatField(NativeArray<byte> bytes, int limit, ref int cursor, out float value)
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
            float ambientPressure = math.max(1f, SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.AmbientPressureGlobalId), 1f));
            if (UIStateStore.IsInitialized)
            {
                if (UIStateStore.TryReadValue(UIValueSlotId.Health01, out UIValueSlot healthSlot))
                    healthFraction = Sanitize01(healthSlot.Value);

                if (UIStateStore.TryReadValue(UIValueSlotId.Oxygen01, out UIValueSlot oxygenSlot))
                    oxygen01 = Sanitize01(oxygenSlot.Value);

                if (UIStateStore.TryReadValue(UIValueSlotId.PressureAtm, out UIValueSlot pressureSlot))
                    ambientPressure = math.max(1f, SanitizeFinite(pressureSlot.Value, ambientPressure));
            }

            float localTemperature = SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.LocalTemperatureGlobalId), 0f);
            float globalStress = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.PlayerStressGlobalId));
            float frequencyTuningError01 = Sanitize01(Shader.GetGlobalFloat(ShaderConstants.FrequencyTuningErrorGlobalId));
            float vrComfortVignette01 = math.max(
                Sanitize01(Shader.GetGlobalFloat(ShaderConstants.VrComfortVignette01Id)),
                Sanitize01(Shader.GetGlobalFloat(ShaderConstants.SomaticComfortVignetteId)));
            Vector4 vrComfortJerkState = SanitizeVrComfortJerkState(Shader.GetGlobalVector(ShaderConstants.VrComfortJerkStateId));
            float qualityPressure01 = ResolveQualityPressure01(memoryQualityPressureFloor01);
            Vector4 internalWaterlineParams = ResolveInternalWaterlineParams(renderCamera, settings);
            Vector4 internalWaterlineDistortion = ResolveInternalWaterlineDistortion(qualityPressure01);
            bool depthlessTBDR = ResolveDepthlessTBDRPath();
            float lightShaftActiveCount = math.max(0f, SanitizeFinite(Shader.GetGlobalVector(ShaderConstants.LightShaftParamsId).x, 0f));

            float visualBudget01 = 1f - qualityPressure01;
            float bulletTimeVisual01 = Sanitize01(SimulationSignalRoute.BulletTimeVisualIntensity01) * visualBudget01;
            float pressureSurge01 = ResolvePressureSurgeVisual01(ambientPressure, hullStress, qualityPressure01, settings);
            float playerStress = math.saturate(math.max(frequencyTuningError01, math.max(globalStress, math.max(hullStress, 1f - healthFraction))));
            playerStress = math.max(playerStress, math.max(bulletTimeVisual01, pressureSurge01 * 0.5f));
            float hypoxia = math.max(
                Sanitize01(Shader.GetGlobalFloat(ShaderConstants.HypoxiaSignalGlobalId)),
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

            runtimeState = new RuntimeState(
                hasActiveSignal,
                healthFraction,
                localTemperature,
                ambientPressure,
                playerStress,
                hypoxia,
                statusMask,
                wetLens,
                hullStress,
                HectonFloatingOrigin.CurrentShiftSequence,
                vrComfortVignette01,
                vrComfortJerkState,
                internalWaterlineParams,
                internalWaterlineDistortion,
                qualityPressure01,
                depthlessTBDR);
            return true;
        }

        private static Vector4 ResolveInternalWaterlineParams(Camera renderCamera, FeatureSettings settings)
        {
            Vector4 runtime = Shader.GetGlobalVector(ShaderConstants.InternalWaterlineRuntimeId);
            float active01 = Sanitize01(runtime.x);
            float droplets01 = Sanitize01(runtime.z);
            if ((active01 <= 0.001f && droplets01 <= 0.001f) || renderCamera == null || settings == null)
                return Vector4.zero;

            float waterlineY = SanitizeFinite(Shader.GetGlobalFloat(ShaderConstants.InternalWaterlineYId), float.NegativeInfinity);
            if (active01 <= 0.001f || !math.isfinite(waterlineY))
                return new Vector4(0f, 0f, 0f, droplets01);

            Transform cameraTransform = renderCamera.transform;
            float cameraY = cameraTransform.position.y;
            float splitLine = cameraY < waterlineY - 0.03f
                ? 1.08f
                : ResolveInternalWaterlineViewportSplit(renderCamera, waterlineY, settings);
            float submerged01 = cameraY < waterlineY - 0.03f ? 1f : 0f;
            return new Vector4(splitLine, active01, submerged01, droplets01);
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
                Vector3 planeSample = new Vector3(
                    cameraPosition.x + forward.x * invLength * sampleDistance,
                    waterlineY,
                    cameraPosition.z + forward.z * invLength * sampleDistance);
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

        private static Vector4 ResolveInternalWaterlineDistortion(float qualityPressure01)
        {
            Vector4 distortion = Shader.GetGlobalVector(ShaderConstants.InternalWaterlineDistortionId);
            qualityPressure01 = Sanitize01(qualityPressure01);
            float visualBudget01 = 1f - qualityPressure01;
            return new Vector4(
                math.max(0f, SanitizeFinite(distortion.x, 0f)) * visualBudget01,
                Sanitize01(distortion.y),
                math.max(0.001f, SanitizeFinite(distortion.z, 0.018f)),
                math.lerp(Sanitize01(distortion.w), 1f, qualityPressure01));
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
            return math.smoothstep(0f, 1f, math.max(pressureDrive01, stressDrive01)) * visualBudget01;
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
                _noirPlayerContext,
                out playerCamera,
                out wetLens01,
                out hullStress01,
                out statusMask);
        }

        private float ResolveMemoryQualityPressureFloor01(FeatureSettings currentSettings)
        {
            int thresholdMb = currentSettings != null ? math.max(256, currentSettings.minimumQualityVideoMemoryMb) : 2048;
            if (_cachedMinimumQualityThresholdMb == thresholdMb)
                return _cachedMemoryQualityPressureFloor01;

            _cachedGraphicsMemoryMb = SystemInfo.graphicsMemorySize;
            _cachedMinimumQualityThresholdMb = thresholdMb;
            float memoryMb = math.max(1f, _cachedGraphicsMemoryMb);
            float softCeilingMb = thresholdMb * 1.25f;
            float softRangeMb = math.max(1f, thresholdMb * 0.5f);
            float memoryShortage01 = Smooth01(math.saturate((softCeilingMb - memoryMb) * math.rcp(softRangeMb)));
            float memoryKnown01 = math.saturate((float)_cachedGraphicsMemoryMb);
            _cachedMemoryQualityPressureFloor01 = 0.25f * memoryKnown01 * memoryShortage01;
            return _cachedMemoryQualityPressureFloor01;
        }

        private bool ResolveDepthlessTBDRPath()
        {
            int frame = NoirFrameToIndex(ResolveNoirFrameId());
            if (_cachedDepthlessTBDRFrame == frame)
                return _cachedDepthlessTBDR;

            _cachedDepthlessTBDRFrame = frame;
            _cachedDepthlessTBDR = IsQuestVulkanDepthlessCandidate() && HectonXRRuntimeState.IsXRActive;
            return _cachedDepthlessTBDR;
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
            // COLD ALLOC: Material[1] - engine-owned fullscreen post material recreated only when shader changes - owner: HectonVisorUberPostFeature
            material = CoreUtils.CreateEngineMaterial(shader);
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
            return new Vector4(
                Sanitize01(value.x),
                Sanitize01(value.y),
                math.isfinite(value.z) ? math.max(0f, value.z) : 0f,
                Sanitize01(value.w));
        }

        private static Vector4 SanitizeInternalWaterlineParams(Vector4 value)
        {
            return new Vector4(
                math.isfinite(value.x) ? math.clamp(value.x, -0.1f, 1.1f) : 0f,
                Sanitize01(value.y),
                Sanitize01(value.z),
                Sanitize01(value.w));
        }

        private static Vector4 SanitizeInternalWaterlineDistortion(Vector4 value)
        {
            return new Vector4(
                math.isfinite(value.x) ? math.clamp(value.x, 0f, 0.006f) : 0f,
                Sanitize01(value.y),
                math.isfinite(value.z) ? math.clamp(value.z, 0.001f, 0.1f) : 0.018f,
                Sanitize01(value.w));
        }
    }
}
