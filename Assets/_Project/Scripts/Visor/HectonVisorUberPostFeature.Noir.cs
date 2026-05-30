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

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    public sealed partial class HectonVisorUberPostFeature
    {
#if UNITY_EDITOR
        private const string NoirShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_VisorGlitchACES.shader";
#endif

        private const uint NoirSourceHash = 0x53483235u; // SH25
        private const uint NoirFlagMockInput = 1u << 0;
        private const uint NoirFlagPhysiologyInput = 1u << 1;
        private const uint NoirFlagVitalsInput = 1u << 2;
        private const uint NoirFlagEditorOverride = 1u << 3;
        private const uint NoirFlagInvalidMath = 1u << 31;
        private const float NoirConstantsEpsilon = 0.0001f;
        private const int NoirTelemetryCapacity = 300;
        private const int NoirColorProfileCapacity = 32;
        private const int NoirCsvScratchBytes = 16 * 1024;
        private const string NoirDumpFileName = "Dump_1335_VisorNoir.bin";
        private const string NoirColorCsvFileName = "noir_color_grading_profiles.csv";
        private const BufferID NoirConstantsVaultId = BufferID.Shinobu235NoirConstants;
        private const BufferID NoirInputVaultId = BufferID.Shinobu235NoirInput;
        private const BufferID NoirTelemetryVaultId = BufferID.Shinobu235NoirTelemetry;
        private const BufferID NoirTuningVaultId = BufferID.Shinobu235NoirTuning;
        private const BufferID NoirColorProfilesVaultId = BufferID.Shinobu235NoirColorProfiles;
        private const BufferID NoirCsvScratchVaultId = BufferID.Shinobu235NoirCsvScratch;
        private static readonly ulong NoirColorCsvMutationGuardMask =
            UberVisorMutationGuardBit(NoirCsvScratchVaultId) |
            UberVisorMutationGuardBit(NoirColorProfilesVaultId);
        private static readonly bool s_noirLayoutValid = ComputeNoirLayoutValid();
        private static readonly bool s_noirSupportsSetConstantBufferCold = SystemInfo.supportsSetConstantBuffer;

        private sealed partial class FeatureSettings
        {
            [Tooltip("Bypasses the managed/Volume-style legacy visor path and runs one Vault-backed RenderGraph grain/glitch pre-grade pass. URP Volume owns final tonemapping.")]
            public bool deepSeaNoirUnifiedPass = true;

            [Tooltip("Cold-loads noir_color_grading_profiles.csv into Vault-backed profile rows.")]
            public bool loadNoirColorCsv = true;

            [Tooltip("Base film-grain intensity for the single-pass Noir shader.")]
            [Range(0f, 0.16f)] public float noirBaseGrain = 0.035f;

            [Tooltip("Base toxicity/stress block-glitch amplitude.")]
            [Range(0f, 1f)] public float noirBaseGlitch = 0.18f;

            [Tooltip("Single-sample channel-phase chroma fake strength.")]
            [Range(0f, 0.012f)] public float noirChromaticStrength = 0.0025f;

            [Tooltip("Noir vignette strength.")]
            [Range(0f, 1f)] public float noirVignetteStrength = 0.24f;

            [Tooltip("Pre-tonemap grade contrast.")]
            [Range(0.5f, 1.8f)] public float noirContrast = 1.08f;

            [Tooltip("Pre-tonemap grade saturation.")]
            [Range(0f, 1.4f)] public float noirSaturation = 0.72f;

            [Tooltip("Cold/warm grade bias.")]
            [Range(-1f, 1f)] public float noirTemperature = -0.12f;

            [Tooltip("Depth tint contribution.")]
            [Range(0f, 1f)] public float noirDepthTone = 0.42f;

            [Tooltip("Editor/debug split. Left half raw, right half Deep Sea Noir.")]
            public bool noirAbSplit = false;
        }

        private sealed class NoirPostProcessPass : ScriptableRenderPass
        {
            private sealed class NoirPassData
            {
                public TextureHandle Source;
                public Material Material;
                public BufferHandle ConstantsBuffer;
            }

            private static readonly ProfilingSampler s_noirProfilingSampler =
                new ProfilingSampler("Hecton Deep Sea Noir Post");
            private static readonly int s_blitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int s_noirConstantsBufferId = Shader.PropertyToID("NoirPostProcessDTO");

            private FeatureSettings _settings;
            private Material _material;
            private GraphicsBuffer _constantsBuffer;

            public NoirPostProcessPass()
            {
                profilingSampler = s_noirProfilingSampler;
                requiresIntermediateTexture = true;
            }

            public void SetupNoir(
                FeatureSettings settings,
                Material material,
                GraphicsBuffer constantsBuffer)
            {
                _settings = settings;
                _material = material;
                _constantsBuffer = constantsBuffer;
                renderPassEvent = settings != null ? settings.injectionPoint : RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null ||
                    _material == null ||
                    _constantsBuffer == null ||
                    !_constantsBuffer.IsValid())
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
                if (cameraData.renderType != CameraRenderType.Base)
                    return;

                TextureHandle sourceTexture = resourceData.activeColorTexture;
                if (!sourceTexture.IsValid())
                    return;

                TextureDesc sourceDesc = renderGraph.GetTextureDesc(sourceTexture);
                TextureDesc destinationDesc = sourceDesc;
                destinationDesc.name = "_HectonDeepSeaNoirPost";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = DepthBits.None;
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.colorFormat = sourceDesc.colorFormat;
                destinationDesc.useMipMap = false;
                destinationDesc.autoGenerateMips = false;
                TextureHandle destinationTexture = renderGraph.CreateTexture(destinationDesc);
                BufferHandle constantsBuffer = renderGraph.ImportBuffer(_constantsBuffer);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<NoirPassData>(
                           "Hecton Deep Sea Noir Post",
                           out NoirPassData passData,
                           s_noirProfilingSampler))
                {
                    passData.Source = sourceTexture;
                    passData.Material = _material;
                    passData.ConstantsBuffer = constantsBuffer;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.UseBuffer(constantsBuffer, AccessFlags.Read);
                    builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (NoirPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(s_blitTextureId, data.Source);
                        GraphicsBuffer constants = data.ConstantsBuffer;
                        if (constants == null)
                            return;

                        context.cmd.SetGlobalConstantBuffer(
                            constants,
                            s_noirConstantsBufferId,
                            0,
                            NoirPostProcessDTO.SizeBytes);
                        CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                    });
                }

                resourceData.cameraColor = destinationTexture;
            }
        }

        private NoirPostProcessPass _noirPass;
        private GraphicsBuffer _noirConstantsBufferA;
        private GraphicsBuffer _noirConstantsBufferB;
        private GraphicsBuffer _activeNoirConstantsBuffer;
        private VaultGenerationHandle<NoirPostProcessDTO> _noirConstantsHandle;
        private VaultGenerationHandle<NoirPostProcessInputDTO> _noirInputHandle;
        private VaultGenerationHandle<NoirTelemetryEntry> _noirTelemetryHandle;
        private VaultGenerationHandle<NoirPostProcessTuningDTO> _noirTuningHandle;
        private VaultGenerationHandle<NoirColorProfileDTO> _noirColorProfileHandle;
        private VaultGenerationHandle<byte> _noirCsvScratchHandle;
        private NoirPostProcessDTO _lastNoirConstants;
        private NoirColorProfileDTO _cachedNoirColorProfile;
        private readonly NoirColorProfileDTO[] _noirColorProfileCache = new NoirColorProfileDTO[NoirColorProfileCapacity]; // COLD ALLOC: NoirColorProfileDTO[32] - color CSV snapshot for LateFrame profile selection without Vault profile reads - owner: HectonVisorUberPostFeature
        private uint _cachedNoirColorProfileLookupHash;
        private int _cachedNoirColorProfileFrame = int.MinValue;
        private int _noirColorProfileCacheCount;
        private int _noirBufferIndex;
        private int _noirTelemetryCursor;
        private int _nextNoirPlayerRefreshFrame;
        private uint _noirFallbackFrameId;
        private uint _lastNoirTimeFrameId;
        private float _noirWrappedVisualTimeSeconds;
        private bool _hasNoirConstants;
        private bool _hasCachedNoirColorProfile;
        private bool _hasCachedNoirColorProfileLookup;
        private bool _noirDumpWritten;
        private bool _noirColorCsvLoaded;
        private bool _noirColorCsvLoadAttempted;
        private bool _noirPlayerSnapshotsAvailable;
        private bool _registeredLateFrameTick;
        private bool _registeredSlowTick;
        private IPlayerRuntimeContext _noirPlayerContext;
        private IResolutionScalerService _noirResolutionScaler;
        private bool _hotSwapRegistered;

#if UNITY_EDITOR
        private static NoirPostProcessDTO s_lastEditorNoirConstants;
        private static bool s_hasLastEditorNoirConstants;
        private static bool s_noirEditorOverrideActive;
        private static bool s_noirEditorMockActive;
        private static bool s_noirEditorAbSplit;
        private static float s_noirEditorBaseGrain = 0.035f;
        private static float s_noirEditorBaseGlitch = 0.18f;
        private static float s_noirEditorChroma = 0.0025f;
        private static float s_noirEditorVignette = 0.24f;
        private static float s_noirEditorContrast = 1.08f;
        private static float s_noirEditorSaturation = 0.72f;
        private static float s_noirEditorTemperature = -0.12f;
        private static float s_noirEditorDepthTone = 0.42f;
        private static float s_noirEditorMockStress = 0.65f;
        private static float s_noirEditorMockDepth = 420f;
        private static float s_noirEditorMockToxicity = 0.35f;

        private void TryAssignNoirShaderEditor()
        {
            if (settings != null && settings.deepSeaNoirUnifiedPass)
                settings.shader = AssetDatabase.LoadAssetAtPath<Shader>(NoirShaderAssetPath);
        }

        public static void SetEditorNoirOverride(
            bool active,
            float baseGrain,
            float baseGlitch,
            float chroma,
            float vignette,
            float contrast,
            float saturation,
            float temperature,
            float depthTone,
            bool abSplit,
            bool mockActive,
            float mockStress,
            float mockDepth,
            float mockToxicity)
        {
            s_noirEditorOverrideActive = active;
            s_noirEditorBaseGrain = math.clamp(SanitizeFinite(baseGrain, 0.035f), 0f, 0.35f);
            s_noirEditorBaseGlitch = math.clamp(SanitizeFinite(baseGlitch, 0.18f), 0f, 1f);
            s_noirEditorChroma = math.clamp(SanitizeFinite(chroma, 0.0025f), 0f, 0.024f);
            s_noirEditorVignette = Sanitize01(vignette);
            s_noirEditorContrast = math.clamp(SanitizeFinite(contrast, 1.08f), 0.35f, 2.4f);
            s_noirEditorSaturation = math.clamp(SanitizeFinite(saturation, 0.72f), 0f, 1.5f);
            s_noirEditorTemperature = math.clamp(SanitizeFinite(temperature, -0.12f), -1f, 1f);
            s_noirEditorDepthTone = Sanitize01(depthTone);
            s_noirEditorAbSplit = abSplit;
            s_noirEditorMockActive = mockActive;
            s_noirEditorMockStress = Sanitize01(mockStress);
            s_noirEditorMockDepth = math.max(0f, SanitizeFinite(mockDepth, 160f));
            s_noirEditorMockToxicity = Sanitize01(mockToxicity);
        }

        public static unsafe bool TryFetchEditorNoirConstants(out NoirPostProcessDTO constants)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null &&
                vault.TryGetGenerationHandle<NoirPostProcessDTO>(
                    NoirConstantsVaultId,
                    out VaultGenerationHandle<NoirPostProcessDTO> handle) &&
                !vault.IsCompactionFenceActive &&
                IsNoirVaultHandle(in handle, NoirConstantsVaultId))
            {
                if (TryReadNoirVaultBuffer(vault, in handle, NoirConstantsVaultId, 1, out NativeArray<NoirPostProcessDTO>.ReadOnly buffer) &&
                    !vault.IsCompactionFenceActive)
                {
                    constants = buffer[0];
                    return !vault.IsCompactionFenceActive;
                }
            }

            constants = s_lastEditorNoirConstants;
            return s_hasLastEditorNoirConstants;
        }

        public static unsafe bool TryWriteEditorNoirTuning(in NoirPostProcessTuningDTO tuning)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            VaultGenerationHandle<NoirPostProcessTuningDTO> handle;
            if (!vault.TryGetGenerationHandle<NoirPostProcessTuningDTO>(NoirTuningVaultId, out handle) ||
                !IsNoirVaultHandle(in handle, NoirTuningVaultId))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                    return false;

                handle = vault.EnsureGenerationHandle<NoirPostProcessTuningDTO>(
                    NoirTuningVaultId,
                    1,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (vault.IsCompactionFenceActive ||
                !IsNoirVaultHandle(in handle, NoirTuningVaultId) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.GraphicsScalability, out NativeArray<NoirPostProcessTuningDTO> tuningBuffer))
            {
                return false;
            }

            try
            {
                if (vault.IsCompactionFenceActive || !tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                tuningBuffer[0] = tuning;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.GraphicsScalability);
            }
        }

        public static bool ValidateNoirPostProcessLayoutForEditor()
        {
            return ValidateNoirLayout();
        }

        private static void ApplyEditorNoirInputOverride(
            ref NoirPostProcessInputDTO input,
            float quality01,
            float wrappedTime,
            uint frame,
            ref bool hasRuntimeInput)
        {
            if (!s_noirEditorOverrideActive || !s_noirEditorMockActive)
                return;

            input = default;
            input.Stress01 = math.saturate(s_noirEditorMockStress);
            input.DepthMeters = math.max(0f, s_noirEditorMockDepth);
            input.Toxicity01 = math.saturate(s_noirEditorMockToxicity);
            input.Narcosis01 = input.Stress01 * 0.42f;
            input.Supersaturation01 = input.Toxicity01 * 0.35f;
            input.GlobalQualityWeight01 = math.saturate(quality01);
            input.TimeSecondsWrapped = wrappedTime;
            input.FrameIndex = frame;
            input.AbSplit01 = s_noirEditorAbSplit ? 1f : 0f;
            input.Flags = NoirFlagEditorOverride | NoirFlagMockInput;
            input.SourceHash = NoirSourceHash;
            hasRuntimeInput = true;
        }

        private static void ApplyEditorNoirTuningOverride(
            ref float baseGrain,
            ref float baseGlitch,
            ref float baseChroma,
            ref float baseVignette,
            ref float contrast,
            ref float saturation,
            ref float temperature,
            ref float depthTone)
        {
            if (!s_noirEditorOverrideActive)
                return;

            baseGrain = s_noirEditorBaseGrain;
            baseGlitch = s_noirEditorBaseGlitch;
            baseChroma = s_noirEditorChroma;
            baseVignette = s_noirEditorVignette;
            contrast = s_noirEditorContrast;
            saturation = s_noirEditorSaturation;
            temperature = s_noirEditorTemperature;
            depthTone = s_noirEditorDepthTone;
        }
#endif

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                    IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : null;
                    BindUberDataVaultForLifecycle(nextVault, previousVault);
                    if (_dataVault != null)
                    {
                        if (settings != null && settings.deepSeaNoirUnifiedPass)
                        {
                            EnsureNoirVaultHandles();
#if UNITY_EDITOR
                            if (settings.loadNoirColorCsv)
                                TryLoadNoirColorCsvCold();
#endif
                        }
                        else
                        {
                            EnsureReconstructionVaultHandles();
#if UNITY_EDITOR
                            if (settings != null && settings.loadAestheticCsv)
                                TryLoadAestheticCsvCold();
#endif
                        }
                    }
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _noirPlayerContext = currentService as IPlayerRuntimeContext;
                    _nextNoirPlayerRefreshFrame = 0;
                    RefreshNoirPlayerContextCold();
                    break;
                case GlobalRegistryServiceSlot.ResolutionScalerService:
                    _noirResolutionScaler = currentService as IResolutionScalerService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterSlowTickable();
                    TryUnregisterLateFrameTickable();
                    if (currentService != null)
                    {
                        TryRegisterSlowTickable();
                        TryRegisterLateFrameTickable();
                    }
                    break;
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CachePresentationGlobalsLate();
            if (settings == null)
            {
                return;
            }

            if (settings.deepSeaNoirUnifiedPass)
            {
                if (_material == null ||
                    !NoirConstantsBuffersReady() ||
                    !NoirVaultHandlesReady())
                {
                    return;
                }

                TryUpdateNoirConstants();
                return;
            }

            TryUpdateReconstructionConstantsLate();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (settings == null)
                return;

            if (settings.deepSeaNoirUnifiedPass)
            {
                if (!NoirConstantsBuffersReady() || !NoirVaultHandlesReady())
                    ClearPendingReconstructionInput();
                return;
            }

            if (!IsReconstructionConstantsBufferReady() || !ReconstructionVaultHandlesReady())
                ClearRawColorHistoryRequest();
        }

        private void EnsureNoirPassCold()
        {
            _noirPass ??= new NoirPostProcessPass();
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick ||
                !Application.isPlaying ||
                GlobalRegistry.Dispatcher == null)
            {
                return;
            }

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick ||
                !Application.isPlaying ||
                GlobalRegistry.Dispatcher == null)
            {
                return;
            }

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        private void RefreshNoirCachedDependenciesCold()
        {
            BindUberDataVaultForLifecycle(GlobalRegistry.DataVault, _dataVault);
            _noirResolutionScaler = GlobalRegistry.ResolutionScaler;
            _noirPlayerContext = GlobalRegistry.Player;
            RefreshNoirPlayerContextCold();
        }

        private void RefreshNoirPlayerContextCold()
        {
            RefreshNoirPlayerContextCold(ResolveNoirFrameIndex());
        }

        private void RefreshNoirPlayerContextCold(int frame)
        {
            _noirPlayerSnapshotsAvailable = false;

            IPlayerRuntimeContext player = _noirPlayerContext;
            if (player == null || !player.IsInitialized)
            {
                _nextNoirPlayerRefreshFrame = frame + ResolveNoirPlayerRefreshCadenceFrames(ResolveNoirQualityWeight01());
                return;
            }

            bool hasMovement = player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                               (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
            bool hasSurvival = player.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState) &&
                               (survivalState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u;
            _noirPlayerSnapshotsAvailable = hasMovement || hasSurvival;
            _nextNoirPlayerRefreshFrame = !_noirPlayerSnapshotsAvailable
                ? frame + ResolveNoirPlayerRefreshCadenceFrames(ResolveNoirQualityWeight01())
                : 0;
        }

        private void TryRefreshLateNoirPlayerContext(float quality01, int frame)
        {
            if (_noirPlayerSnapshotsAvailable)
                return;

            if (_noirPlayerContext == null)
                return;

            if (frame < _nextNoirPlayerRefreshFrame)
                return;

            RefreshNoirPlayerContextCold(frame);
            if (!_noirPlayerSnapshotsAvailable)
                _nextNoirPlayerRefreshFrame = frame + ResolveNoirPlayerRefreshCadenceFrames(quality01);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
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

        private bool EnsureNoirConstantsBuffersCold()
        {
            if (!s_noirSupportsSetConstantBufferCold || !ValidateNoirLayout())
            {
                _noirConstantsBufferA?.Release();
                _noirConstantsBufferA = null;
                _noirConstantsBufferB?.Release();
                _noirConstantsBufferB = null;
                _activeNoirConstantsBuffer = null;
                return false;
            }

            if (_noirConstantsBufferA == null || !_noirConstantsBufferA.IsValid())
            {
                _noirConstantsBufferA?.Release();
                _noirConstantsBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    NoirPostProcessDTO.SizeBytes);
            }

            if (_noirConstantsBufferB == null || !_noirConstantsBufferB.IsValid())
            {
                _noirConstantsBufferB?.Release();
                _noirConstantsBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    NoirPostProcessDTO.SizeBytes);
            }

            return NoirConstantsBuffersReady();
        }

        private bool NoirConstantsBuffersReady()
        {
            return s_noirSupportsSetConstantBufferCold &&
                   _noirConstantsBufferA != null &&
                   _noirConstantsBufferB != null &&
                   _noirConstantsBufferA.IsValid() &&
                   _noirConstantsBufferB.IsValid();
        }

        private bool EnsureNoirVaultHandles()
        {
            if (_dataVault == null)
                return false;

            _ = EnsureNoirVaultHandle(ref _noirConstantsHandle, NoirConstantsVaultId, 1, NativeArrayOptions.UninitializedMemory);
            _ = EnsureNoirVaultHandle(ref _noirInputHandle, NoirInputVaultId, 1, NativeArrayOptions.UninitializedMemory);
            _ = EnsureNoirVaultHandle(ref _noirTelemetryHandle, NoirTelemetryVaultId, NoirTelemetryCapacity, NativeArrayOptions.ClearMemory);
            _ = EnsureNoirVaultHandle(ref _noirTuningHandle, NoirTuningVaultId, 1, NativeArrayOptions.UninitializedMemory);
            _ = EnsureNoirVaultHandle(ref _noirColorProfileHandle, NoirColorProfilesVaultId, NoirColorProfileCapacity, NativeArrayOptions.ClearMemory);
            _ = EnsureNoirVaultHandle(ref _noirCsvScratchHandle, NoirCsvScratchVaultId, NoirCsvScratchBytes, NativeArrayOptions.UninitializedMemory);

            return NoirVaultHandlesReady();
        }

        private bool NoirVaultHandlesReady()
        {
            return _dataVault != null &&
                   IsNoirVaultHandle(in _noirConstantsHandle, NoirConstantsVaultId) &&
                   IsNoirVaultHandle(in _noirInputHandle, NoirInputVaultId) &&
                   IsNoirVaultHandle(in _noirTelemetryHandle, NoirTelemetryVaultId) &&
                   IsNoirVaultHandle(in _noirTuningHandle, NoirTuningVaultId) &&
                   IsNoirVaultHandle(in _noirColorProfileHandle, NoirColorProfilesVaultId) &&
                   IsNoirVaultHandle(in _noirCsvScratchHandle, NoirCsvScratchVaultId);
        }

        private void ClearNoirVaultHandles()
        {
            _noirConstantsHandle = default;
            _noirInputHandle = default;
            _noirTelemetryHandle = default;
            _noirTuningHandle = default;
            _noirColorProfileHandle = default;
            _noirCsvScratchHandle = default;
            _hasCachedNoirColorProfile = false;
            _hasCachedNoirColorProfileLookup = false;
            _noirColorProfileCacheCount = 0;
            _noirColorCsvLoaded = false;
            _noirColorCsvLoadAttempted = false;
            ResetNoirClockState();
        }

        private void ReleaseNoirVaultHandles(IDataVault vault)
        {
            ReleaseNoirVaultHandle(vault, ref _noirConstantsHandle, NoirConstantsVaultId);
            ReleaseNoirVaultHandle(vault, ref _noirInputHandle, NoirInputVaultId);
            ReleaseNoirVaultHandle(vault, ref _noirTelemetryHandle, NoirTelemetryVaultId);
            ReleaseNoirVaultHandle(vault, ref _noirTuningHandle, NoirTuningVaultId);
            ReleaseNoirVaultHandle(vault, ref _noirColorProfileHandle, NoirColorProfilesVaultId);
            ReleaseNoirVaultHandle(vault, ref _noirCsvScratchHandle, NoirCsvScratchVaultId);

            ClearNoirVaultHandles();
        }

        private bool EnsureNoirVaultHandle<T>(
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

            if (TryReadNoirVaultBuffer(_dataVault, in handle, bufferId, requiredLength, out NativeArray<T>.ReadOnly _))
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

            return TryReadNoirVaultBuffer(_dataVault, in handle, bufferId, requiredLength, out NativeArray<T>.ReadOnly _);
        }

        private static void ReleaseNoirVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : unmanaged
        {
            if (vault != null && IsNoirVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryReadNoirVaultBuffer<T>(
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
                   IsNoirVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   (requiredLength == 0 || buffer.Length >= requiredLength);
        }

        private static bool IsNoirVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability &&
                   handle.Generation != 0u;
        }

        private bool TryUpdateNoirConstants()
        {
            IDataVault vault = _dataVault;
            if (!NoirVaultHandlesReady() || vault == null || vault.IsCompactionFenceActive)
                return false;

            float quality01 = ResolveNoirQualityWeight01();
            uint frame = ResolveNoirFrameId();
            int frameIndex = NoirFrameToIndex(frame);
            TryRefreshLateNoirPlayerContext(quality01, frameIndex);
            float wrappedTime = ResolveWrappedNoirTimeSeconds(frame);
            bool hasRuntimeInput = TryBuildNoirInputSnapshot(quality01, wrappedTime, frame, out NoirPostProcessInputDTO input);
#if UNITY_EDITOR
            ApplyEditorNoirInputOverride(ref input, quality01, wrappedTime, frame, ref hasRuntimeInput);
#endif
            if (!hasRuntimeInput)
                input = BuildMockNoirInput(wrappedTime, quality01, frame);

            NoirPostProcessTuningDTO tuning = BuildNoirTuning(settings, input);
            NoirPostProcessDTO constants = CalculateNoirParameters(in input, in tuning);
            bool validConstants = NoirConstantsFinite(in constants);
            if (!validConstants)
            {
                constants = BuildNoirFailsafeConstants(quality01, wrappedTime);
                input.Flags |= NoirFlagInvalidMath;
            }

            if (!TryWriteNoirDto(in _noirInputHandle, in input) ||
                !TryWriteNoirDto(in _noirTuningHandle, in tuning) ||
                !TryWriteNoirDto(in _noirConstantsHandle, in constants))
            {
                return false;
            }

            bool uploaded = UpdateNoirConstantsBuffer(in constants);
            RecordNoirTelemetry(in input, in constants, validConstants);
            if (!validConstants && !_noirDumpWritten)
                _noirDumpWritten = TryDumpNoirTelemetry();
            return uploaded;
        }

        private bool TryWriteNoirDto<T>(
            in VaultGenerationHandle<T> handle,
            in T value) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.GraphicsScalability, out NativeArray<T> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated || buffer.Length <= 0)
                    return false;

                T copy = value;
                buffer[0] = copy;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.GraphicsScalability);
            }
        }

        private bool TryBuildNoirInputSnapshot(
            float quality01,
            float wrappedTime,
            uint frame,
            out NoirPostProcessInputDTO input)
        {
            input = default;
            input.GlobalQualityWeight01 = math.saturate(quality01);
            input.TimeSecondsWrapped = wrappedTime;
            input.FrameIndex = frame;
            input.AbSplit01 = ResolveNoirAbSplit01(settings);
            input.SourceHash = NoirSourceHash;

            bool hasSource = false;
            IPlayerRuntimeContext playerContext = _noirPlayerContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survival))
            {
                input.Narcosis01 = Sanitize01(survival.NitrogenNarcosis01);
                input.Toxicity01 = Sanitize01(survival.Toxicity01);
                input.Supersaturation01 = Sanitize01(survival.PressureExposureSeverity01);
                input.Stress01 = math.saturate(math.max(input.Narcosis01, math.max(input.Toxicity01, input.Supersaturation01)));
                input.Flags |= NoirFlagPhysiologyInput | NoirFlagVitalsInput;
                hasSource = true;
            }

            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movement))
            {
                float movementDepth = math.max(0f, SanitizeFinite(movement.DepthMeters, input.DepthMeters));
                float movementStress01 = Sanitize01(movement.UnderwaterStressIntensity01);
                input.DepthMeters = math.max(input.DepthMeters, movementDepth);
                input.Stress01 = math.saturate(math.max(input.Stress01, movementStress01));
                input.Flags |= NoirFlagVitalsInput;
                hasSource = true;
            }

            _noirPlayerSnapshotsAvailable = hasSource;

            if (hasSource)
            {
                if (input.Stress01 <= 0.0001f)
                    input.Stress01 = math.saturate(math.max(input.Narcosis01, math.max(input.Toxicity01, input.Supersaturation01)));
            }

            return hasSource &&
                   math.isfinite(input.Stress01) &&
                   math.isfinite(input.DepthMeters) &&
                   math.isfinite(input.Toxicity01);
        }

        private NoirPostProcessTuningDTO BuildNoirTuning(FeatureSettings currentSettings, NoirPostProcessInputDTO input)
        {
            NoirPostProcessTuningDTO tuning = default;
            float baseGrain = currentSettings != null ? currentSettings.noirBaseGrain : 0.035f;
            float baseGlitch = currentSettings != null ? currentSettings.noirBaseGlitch : 0.18f;
            float baseChroma = currentSettings != null ? currentSettings.noirChromaticStrength : 0.0025f;
            float baseVignette = currentSettings != null ? currentSettings.noirVignetteStrength : 0.24f;
            float contrast = currentSettings != null ? currentSettings.noirContrast : 1.08f;
            float saturation = currentSettings != null ? currentSettings.noirSaturation : 0.72f;
            float temperature = currentSettings != null ? currentSettings.noirTemperature : -0.12f;
            float depthTone = currentSettings != null ? currentSettings.noirDepthTone : 0.42f;

            if (TrySelectNoirColorProfile(input, out NoirColorProfileDTO profile))
            {
                contrast = SanitizeFinite(profile.GradeParams.x, contrast);
                saturation = SanitizeFinite(profile.GradeParams.y, saturation);
                temperature = SanitizeFinite(profile.GradeParams.z, temperature);
                depthTone = SanitizeFinite(profile.GradeParams.w, depthTone);
                baseGrain *= math.max(0f, SanitizeFinite(profile.ResponseParams.x, 1f));
                baseGlitch *= math.max(0f, SanitizeFinite(profile.ResponseParams.y, 1f));
                baseChroma *= math.max(0f, SanitizeFinite(profile.ResponseParams.z, 1f));
                baseVignette *= math.max(0f, SanitizeFinite(profile.ResponseParams.w, 1f));
            }

#if UNITY_EDITOR
            ApplyEditorNoirTuningOverride(ref baseGrain, ref baseGlitch, ref baseChroma, ref baseVignette, ref contrast, ref saturation, ref temperature, ref depthTone);
#endif

            tuning.BaseParams = new float4(
                math.clamp(SanitizeFinite(baseGrain, 0.035f), 0f, 0.35f),
                math.clamp(SanitizeFinite(baseGlitch, 0.18f), 0f, 1f),
                math.clamp(SanitizeFinite(baseChroma, 0.0025f), 0f, 0.024f),
                Sanitize01(baseVignette));
            tuning.GradeParams = new float4(
                math.clamp(SanitizeFinite(contrast, 1.08f), 0.35f, 2.4f),
                math.clamp(SanitizeFinite(saturation, 0.72f), 0f, 1.5f),
                math.clamp(SanitizeFinite(temperature, -0.12f), -1f, 1f),
                Sanitize01(depthTone));
            tuning.StressResponse = new float4(0.72f, 0.82f, 0.94f, 0.22f);
            tuning.ProfileParams = new float4(ResolveNoirAbSplit01(currentSettings), 1f, 0f, 0f);
            return tuning;
        }

        private unsafe bool UpdateNoirConstantsBuffer(in NoirPostProcessDTO constants)
        {
            if (!NoirConstantsBuffersReady())
                return false;

            if (_hasNoirConstants && NoirConstantsEqual(in _lastNoirConstants, in constants))
                return _activeNoirConstantsBuffer != null && _activeNoirConstantsBuffer.IsValid();

            GraphicsBuffer target = (_noirBufferIndex & 1) == 0 ? _noirConstantsBufferA : _noirConstantsBufferB;
            _noirBufferIndex++;
            try
            {
                NativeArray<NoirPostProcessDTO> mapped = target.LockBufferForWrite<NoirPostProcessDTO>(0, 1);
                NoirPostProcessDTO local = constants;
                try
                {
                    UnsafeUtility.MemCpy(
                        mapped.GetUnsafePtr(),
                        UnsafeUtility.AddressOf(ref local),
                        NoirPostProcessDTO.SizeBytes);
                }
                finally
                {
                    target.UnlockBufferAfterWrite<NoirPostProcessDTO>(1);
                }
            }
            catch (ObjectDisposedException)
            {
                ClearNoirConstantsGpuPayload();
                return false;
            }
            catch (InvalidOperationException)
            {
                ClearNoirConstantsGpuPayload();
                return false;
            }
            catch (ArgumentException)
            {
                ClearNoirConstantsGpuPayload();
                return false;
            }
            catch (NotSupportedException)
            {
                ClearNoirConstantsGpuPayload();
                return false;
            }

            _lastNoirConstants = constants;
            _hasNoirConstants = true;
            _activeNoirConstantsBuffer = target;
#if UNITY_EDITOR
            s_lastEditorNoirConstants = constants;
            s_hasLastEditorNoirConstants = true;
#endif
            return true;
        }

        private void ClearNoirConstantsGpuPayload()
        {
            _activeNoirConstantsBuffer = null;
            _hasNoirConstants = false;
        }

        private void RecordNoirTelemetry(
            in NoirPostProcessInputDTO input,
            in NoirPostProcessDTO constants,
            bool validConstants)
        {
            IDataVault vault = _dataVault;
            if (!NoirVaultHandlesReady() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _noirTelemetryHandle, SystemID.GraphicsScalability, out NativeArray<NoirTelemetryEntry> telemetry))
            {
                return;
            }

            try
            {
                if (vault.IsCompactionFenceActive || !telemetry.IsCreated || telemetry.Length <= 0)
                    return;

                int index = _noirTelemetryCursor;
                _noirTelemetryCursor = (_noirTelemetryCursor + 1) % telemetry.Length;
                if ((uint)index >= (uint)telemetry.Length)
                    index = 0;

                NoirTelemetryEntry entry = default;
                entry.Frame = input.FrameIndex;
                entry.Flags = input.Flags | (validConstants ? 0u : NoirFlagInvalidMath);
                entry.Stress01 = input.Stress01;
                entry.DepthMeters = input.DepthMeters;
                entry.Toxicity01 = input.Toxicity01;
                entry.GlobalQualityWeight01 = input.GlobalQualityWeight01;
                entry.Grain01 = constants.GrainParams.x;
                entry.Glitch01 = constants.AberrationParams.y;
                entry.Vignette01 = constants.AberrationParams.w;
                entry.AbSplit01 = constants.QualityAndLimits.w;
                entry.WrappedTimeSeconds = input.TimeSecondsWrapped;
                entry.ParameterHash = HashNoirConstants(in constants);
                entry.EstimatedGpuCostMs = EstimateNoirGpuCostMs(in constants);
                entry.ActiveFeatureFlags = ResolveNoirFeatureFlags(in constants);
                telemetry[index] = entry;
            }
            finally
            {
                vault.ReleaseWriteLock(in _noirTelemetryHandle, SystemID.GraphicsScalability);
            }
        }

        private bool TryDumpNoirTelemetry()
        {
            if (_dataVault == null ||
                !TryGetNoirTelemetryEntryCount(out int entryCount) ||
                entryCount <= 0)
                return false;

            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, NoirDumpFileName);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> rowBytes = stackalloc byte[DrsContractLayout.NoirTelemetryEntryStrideBytes];
                    for (int i = 0; i < entryCount; i++)
                    {
                        if (!TryReadNoirTelemetryEntry(i, out NoirTelemetryEntry entry))
                            return false;

                        WriteNoirTelemetryEntry(rowBytes, in entry);
                        stream.Write(rowBytes);
                    }
                }

                return true;
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
        }

        private bool TryGetNoirTelemetryEntryCount(out int entryCount)
        {
            entryCount = 0;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive)
                return false;

            if (!TryReadNoirVaultBuffer(vault, in _noirTelemetryHandle, NoirTelemetryVaultId, NoirTelemetryCapacity, out NativeArray<NoirTelemetryEntry>.ReadOnly telemetry))
                return false;

            if (vault.IsCompactionFenceActive || !telemetry.IsCreated)
                return false;

            entryCount = math.min(telemetry.Length, NoirTelemetryCapacity);
            return entryCount > 0;
        }

        private bool TryReadNoirTelemetryEntry(int index, out NoirTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                index < 0)
                return false;

            if (!TryReadNoirVaultBuffer(vault, in _noirTelemetryHandle, NoirTelemetryVaultId, NoirTelemetryCapacity, out NativeArray<NoirTelemetryEntry>.ReadOnly telemetry))
                return false;

            if (vault.IsCompactionFenceActive ||
                !telemetry.IsCreated ||
                (uint)index >= (uint)math.min(telemetry.Length, NoirTelemetryCapacity))
                return false;

            entry = telemetry[index];
            return !vault.IsCompactionFenceActive;
        }

        private static void WriteNoirTelemetryEntry(Span<byte> destination, in NoirTelemetryEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Flags);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.Stress01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.DepthMeters);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.Toxicity01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.GlobalQualityWeight01);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.Grain01);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.Glitch01);
            WriteFloatLittleEndian(destination.Slice(32, 4), entry.Vignette01);
            WriteFloatLittleEndian(destination.Slice(36, 4), entry.AbSplit01);
            WriteFloatLittleEndian(destination.Slice(40, 4), entry.WrappedTimeSeconds);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(44, 4), entry.ParameterHash);
            WriteFloatLittleEndian(destination.Slice(48, 4), entry.EstimatedGpuCostMs);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(52, 4), entry.ActiveFeatureFlags);
            destination.Slice(56, 8).Clear();
        }

        private bool TrySelectNoirColorProfile(
            NoirPostProcessInputDTO input,
            out NoirColorProfileDTO profile)
        {
            uint lookupHash = HashNoirProfileLookupKey(in input);
            int frame = NoirFrameToIndex(input.FrameIndex);
            int cadence = ResolveNoirProfileCadenceFrames(input.GlobalQualityWeight01);
            if (_hasCachedNoirColorProfileLookup &&
                _cachedNoirColorProfileLookupHash == lookupHash &&
                frame - _cachedNoirColorProfileFrame < cadence)
            {
                profile = _cachedNoirColorProfile;
                return _hasCachedNoirColorProfile;
            }

            int count = math.min(_noirColorProfileCacheCount, _noirColorProfileCache.Length);
            if (!_noirColorCsvLoaded || count <= 0)
            {
                profile = default;
                return false;
            }

            float depthMeters = math.max(0f, SanitizeFinite(input.DepthMeters, 0f));
            float stress01 = Sanitize01(input.Stress01);
            for (int i = 0; i < count; i++)
            {
                NoirColorProfileDTO candidate = _noirColorProfileCache[i];
                if (candidate.ProfileHash == 0u || candidate.Flags == 0u)
                    continue;

                if (depthMeters >= candidate.DepthMinMeters &&
                    depthMeters <= candidate.DepthMaxMeters &&
                    stress01 >= candidate.StressMin01 &&
                    stress01 <= candidate.StressMax01)
                {
                    _cachedNoirColorProfile = candidate;
                    _cachedNoirColorProfileLookupHash = lookupHash;
                    _cachedNoirColorProfileFrame = frame;
                    _hasCachedNoirColorProfile = true;
                    _hasCachedNoirColorProfileLookup = true;
                    profile = candidate;
                    return true;
                }
            }

            _cachedNoirColorProfile = default;
            _cachedNoirColorProfileLookupHash = lookupHash;
            _cachedNoirColorProfileFrame = frame;
            _hasCachedNoirColorProfile = false;
            _hasCachedNoirColorProfileLookup = true;
            profile = default;
            return false;
        }

#if UNITY_EDITOR
        private bool TryLoadNoirColorCsvCold()
        {
            if (_noirColorCsvLoaded || _noirColorCsvLoadAttempted)
                return _noirColorCsvLoaded;

            IDataVault vault = _dataVault;
            if (!EnsureNoirVaultHandles() || vault == null || vault.IsCompactionFenceActive)
                return false;

            string path = ResolveNoirColorCsvPath();
            _noirColorCsvLoadAttempted = true;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                Span<byte> csvBytes = stackalloc byte[NoirCsvScratchBytes];
                int read = ReadCsvFileIntoSpan(path, csvBytes);
                if (read <= 0)
                    return false;

                Span<NoirColorProfileDTO> parsedProfiles = stackalloc NoirColorProfileDTO[NoirColorProfileCapacity];
                int parsed = ParseNoirColorCsv(csvBytes.Slice(0, read), parsedProfiles);

                if (!vault.TryAcquireMutationGuard(NoirColorCsvMutationGuardMask))
                {
                    _noirColorCsvLoadAttempted = false;
                    return false;
                }

                try
                {
                    if (!vault.TryResolveHandle(in _noirCsvScratchHandle, out NativeArray<byte> scratch) ||
                        !vault.TryResolveHandle(in _noirColorProfileHandle, out NativeArray<NoirColorProfileDTO> profiles) ||
                        !scratch.IsCreated ||
                        scratch.Length <= 0 ||
                        !profiles.IsCreated ||
                        profiles.Length <= 0)
                    {
                        return false;
                    }

                    CopyBytesToNativeArray(csvBytes.Slice(0, read), scratch);
                    CopyNoirColorProfilesToNativeArray(parsedProfiles, parsed, profiles);
                }
                finally
                {
                    vault.ReleaseMutationGuard(NoirColorCsvMutationGuardMask);
                }

                _noirColorCsvLoaded = parsed > 0;
                CacheNoirColorProfiles(parsedProfiles, parsed);
                _hasCachedNoirColorProfile = false;
                _hasCachedNoirColorProfileLookup = false;
                return _noirColorCsvLoaded;
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

        private void CacheNoirColorProfiles(
            ReadOnlySpan<NoirColorProfileDTO> profiles,
            int parsed)
        {
            int count = math.min(math.max(0, parsed), math.min(profiles.Length, _noirColorProfileCache.Length));
            for (int i = 0; i < count; i++)
                _noirColorProfileCache[i] = profiles[i];

            for (int i = count; i < _noirColorProfileCache.Length; i++)
                _noirColorProfileCache[i] = default;

            _noirColorProfileCacheCount = count;
        }

        private static int ParseNoirColorCsv(
            ReadOnlySpan<byte> bytes,
            Span<NoirColorProfileDTO> profiles)
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
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float stressMin) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float stressMax) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float contrast) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float saturation) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float temperature) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float depthTone) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float grainScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float glitchScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float chromaScale) ||
                    !TryReadCsvFloatField(bytes, limit, ref cursor, out float vignetteScale))
                {
                    SkipCsvLine(bytes, limit, ref cursor);
                    continue;
                }

                if (profileHash != 0u)
                {
                    NoirColorProfileDTO profile = default;
                    profile.ProfileHash = profileHash;
                    profile.Flags = 1u;
                    profile.DepthMinMeters = math.min(depthMin, depthMax);
                    profile.DepthMaxMeters = math.max(depthMin, depthMax);
                    profile.StressMin01 = math.saturate(math.min(stressMin, stressMax));
                    profile.StressMax01 = math.saturate(math.max(stressMin, stressMax));
                    profile.GradeParams = new float4(
                        math.clamp(contrast, 0.35f, 2.4f),
                        math.clamp(saturation, 0f, 1.5f),
                        math.clamp(temperature, -1f, 1f),
                        math.saturate(depthTone));
                    profile.ResponseParams = new float4(
                        math.max(0f, grainScale),
                        math.max(0f, glitchScale),
                        math.max(0f, chromaScale),
                        math.max(0f, vignetteScale));
                    profiles[write++] = profile;
                }

                SkipCsvLine(bytes, limit, ref cursor);
            }

            for (int i = write; i < profiles.Length; i++)
                profiles[i] = default;

            return write;
        }

        private static string ResolveNoirColorCsvPath()
        {
            string root = Directory.GetCurrentDirectory();
            string path = Path.Combine(root, "Assets", "_Project", "Data", NoirColorCsvFileName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(root, "Data", "Visuals", NoirColorCsvFileName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(root, NoirColorCsvFileName);
            return File.Exists(path) ? path : null;
        }
#endif

        private float ResolveNoirQualityWeight01()
        {
            IResolutionScalerService scaler = _noirResolutionScaler;
            return scaler != null && scaler.TryGetScaleState(out ResolutionScaleState state)
                ? Sanitize01(state.GlobalQualityWeight01)
                : 1f;
        }

        private uint ResolveNoirFrameId()
        {
            uint dispatcherFrame = TimeSliceScheduler.CurrentFrameId;
            if (dispatcherFrame != 0u)
                return dispatcherFrame;

            uint next = _noirFallbackFrameId + 1u;
            if (next == 0u)
                next = 1u;

            _noirFallbackFrameId = next;
            return next;
        }

        private int ResolveNoirFrameIndex()
        {
            return NoirFrameToIndex(ResolveNoirFrameId());
        }

        private float ResolveWrappedNoirTimeSeconds(uint frame)
        {
            if (_lastNoirTimeFrameId == frame)
                return _noirWrappedVisualTimeSeconds;

            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            if (!math.isfinite(deltaTime) || deltaTime <= 0f || deltaTime > 0.25f)
                deltaTime = 0.016666668f;

            float next = _noirWrappedVisualTimeSeconds + deltaTime;
            if (!math.isfinite(next))
                next = 0f;
            if (next >= 1000f)
                next -= math.floor(next * 0.001f) * 1000f;

            _noirWrappedVisualTimeSeconds = next;
            _lastNoirTimeFrameId = frame;
            return next;
        }

        private void ResetNoirClockState()
        {
            _noirFallbackFrameId = 0u;
            _lastNoirTimeFrameId = 0u;
            _noirWrappedVisualTimeSeconds = 0f;
        }

        private static int NoirFrameToIndex(uint frame)
        {
            return frame <= int.MaxValue ? (int)frame : int.MaxValue;
        }

        private static float ResolveNoirAbSplit01(FeatureSettings currentSettings)
        {
            bool enabled = currentSettings != null && currentSettings.noirAbSplit;
#if UNITY_EDITOR
            enabled |= s_noirEditorAbSplit;
#endif
            return enabled ? 1f : 0f;
        }

        private static int ResolveNoirProfileCadenceFrames(float quality01)
        {
            float qualityCurve = Smooth01(math.saturate((Sanitize01(quality01) - 0.18f) * 1.2195122f));
            return math.max(1, (int)math.round(math.lerp(18f, 2f, qualityCurve)));
        }

        private static int ResolveNoirPlayerRefreshCadenceFrames(float quality01)
        {
            float qualityCurve = Smooth01(math.saturate((Sanitize01(quality01) - 0.12f) * 1.1363636f));
            return math.clamp((int)math.round(math.lerp(90f, 18f, qualityCurve)), 18, 90);
        }

        private static uint HashNoirProfileLookupKey(in NoirPostProcessInputDTO input)
        {
            uint depthBucket = (uint)math.clamp((int)math.floor(math.max(0f, SanitizeFinite(input.DepthMeters, 0f)) * 0.02f), 0, 65535);
            uint stressBucket = (uint)math.clamp((int)math.floor(Sanitize01(input.Stress01) * 31f), 0, 31);
            uint toxicityBucket = (uint)math.clamp((int)math.floor(Sanitize01(input.Toxicity01) * 31f), 0, 31);
            uint hash = 2166136261u;
            hash = (hash ^ depthBucket) * 16777619u;
            hash = (hash ^ (stressBucket << 16)) * 16777619u;
            hash = (hash ^ (toxicityBucket << 24)) * 16777619u;
            return hash;
        }

        private static NoirPostProcessDTO BuildNoirFailsafeConstants(float quality01, float wrappedTime)
        {
            NoirPostProcessDTO constants = default;
            constants.GrainParams = new float4(0.01f, 96f, 0.25f, wrappedTime);
            constants.AberrationParams = new float4(0f, 0f, 0f, 0.12f);
            constants.ColorGrading = new float4(1f, 0.75f, -0.08f, 0.2f);
            constants.QualityAndLimits = new float4(Sanitize01(quality01), 0f, 0f, 0f);
            return constants;
        }

        private static bool NoirConstantsFinite(in NoirPostProcessDTO constants)
        {
            return math.all(math.isfinite(constants.GrainParams)) &&
                   math.all(math.isfinite(constants.AberrationParams)) &&
                   math.all(math.isfinite(constants.ColorGrading)) &&
                   math.all(math.isfinite(constants.QualityAndLimits));
        }

        private static bool NoirConstantsEqual(
            in NoirPostProcessDTO left,
            in NoirPostProcessDTO right)
        {
            return math.lengthsq(left.GrainParams - right.GrainParams) <= NoirConstantsEpsilon * NoirConstantsEpsilon &&
                   math.lengthsq(left.AberrationParams - right.AberrationParams) <= NoirConstantsEpsilon * NoirConstantsEpsilon &&
                   math.lengthsq(left.ColorGrading - right.ColorGrading) <= NoirConstantsEpsilon * NoirConstantsEpsilon &&
                   math.lengthsq(left.QualityAndLimits - right.QualityAndLimits) <= NoirConstantsEpsilon * NoirConstantsEpsilon;
        }

        private static uint HashNoirConstants(in NoirPostProcessDTO constants)
        {
            uint hash = math.hash(constants.GrainParams);
            hash = (hash * 16777619u) ^ math.hash(constants.AberrationParams);
            hash = (hash * 16777619u) ^ math.hash(constants.ColorGrading);
            hash = (hash * 16777619u) ^ math.hash(constants.QualityAndLimits);
            return hash;
        }

        private static uint ResolveNoirFeatureFlags(in NoirPostProcessDTO constants)
        {
            uint flags = 0u;
            if (constants.AberrationParams.x > 0.0001f)
                flags |= 1u << 1;
            if (math.max(math.abs(constants.AberrationParams.y), math.abs(constants.AberrationParams.z)) > 0.001f)
                flags |= 1u << 2;
            if (constants.QualityAndLimits.w > 0.5f)
                flags |= 1u << 3;
            return flags;
        }

        private static float EstimateNoirGpuCostMs(in NoirPostProcessDTO constants)
        {
            float quality01 = Sanitize01(constants.QualityAndLimits.x);
            float grain = Sanitize01(constants.GrainParams.x * 8f);
            float glitch = Sanitize01(math.max(math.abs(constants.AberrationParams.y), math.abs(constants.AberrationParams.z)) * 2f);
            float chroma = Sanitize01(math.abs(constants.AberrationParams.x) * 220f);
            return math.max(0f, 0.028f + quality01 * 0.018f + grain * 0.01f + glitch * 0.014f + chroma * 0.18f);
        }

        private static bool ValidateNoirLayout()
        {
            return s_noirLayoutValid;
        }

        private static bool ComputeNoirLayoutValid()
        {
            return UnsafeUtility.SizeOf<NoirPostProcessDTO>() == NoirPostProcessDTO.SizeBytes &&
                   Marshal.OffsetOf<NoirPostProcessDTO>(nameof(NoirPostProcessDTO.GrainParams)).ToInt32() == 0 &&
                   Marshal.OffsetOf<NoirPostProcessDTO>(nameof(NoirPostProcessDTO.AberrationParams)).ToInt32() == 16 &&
                   Marshal.OffsetOf<NoirPostProcessDTO>(nameof(NoirPostProcessDTO.ColorGrading)).ToInt32() == 32 &&
                   Marshal.OffsetOf<NoirPostProcessDTO>(nameof(NoirPostProcessDTO.QualityAndLimits)).ToInt32() == 48;
        }

        private static NoirPostProcessInputDTO BuildMockNoirInput(
            float wrappedTimeSeconds,
            float globalQualityWeight01,
            uint frameIndex)
        {
            float phase = wrappedTimeSeconds * 0.031f;
            float wave = Triangle01(phase);
            float pulse = Triangle01(phase * 2.71f + 0.37f);
            NoirPostProcessInputDTO input = default;
            input.Stress01 = math.saturate(0.18f + wave * 0.62f);
            input.DepthMeters = math.max(0f, 160f + pulse * 840f);
            input.Toxicity01 = math.saturate(0.08f + (1f - wave) * 0.35f);
            input.Narcosis01 = input.Stress01 * 0.35f;
            input.Supersaturation01 = input.Toxicity01 * 0.22f;
            input.GlobalQualityWeight01 = Sanitize01(globalQualityWeight01);
            input.TimeSecondsWrapped = math.max(0f, SanitizeFinite(wrappedTimeSeconds, 0f));
            input.FrameIndex = frameIndex;
            input.AbSplit01 = 0f;
            input.Flags = NoirFlagMockInput;
            input.SourceHash = NoirSourceHash;
            return input;
        }

        private static float Triangle01(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f);
        }

        private static NoirPostProcessDTO CalculateNoirParameters(
            in NoirPostProcessInputDTO input,
            in NoirPostProcessTuningDTO tuning)
        {
            float stress01 = Sanitize01(input.Stress01);
            float toxicity01 = Sanitize01(input.Toxicity01);
            float depthMeters = math.max(0f, SanitizeFinite(input.DepthMeters, 0f));
            float depth01 = math.saturate(depthMeters * 0.001f);
            float quality01 = Sanitize01(input.GlobalQualityWeight01);
            float qualityCurve = quality01 * quality01 * (3f - 2f * quality01);
            float highDetail01 = math.saturate((quality01 - 0.22f) * 1.2820513f);
            highDetail01 = highDetail01 * highDetail01 * (3f - 2f * highDetail01);

            float baseGrain = math.max(0f, SanitizeFinite(tuning.BaseParams.x, 0.035f));
            float baseGlitch = math.max(0f, SanitizeFinite(tuning.BaseParams.y, 0.18f));
            float baseChroma = math.max(0f, SanitizeFinite(tuning.BaseParams.z, 0.0025f));
            float baseVignette = Sanitize01(tuning.BaseParams.w);
            float contrastBase = math.clamp(SanitizeFinite(tuning.GradeParams.x, 1.08f), 0.1f, 2.4f);
            float saturationBase = math.clamp(SanitizeFinite(tuning.GradeParams.y, 0.72f), 0f, 1.5f);
            float temperatureBase = math.clamp(SanitizeFinite(tuning.GradeParams.z, -0.12f), -1f, 1f);
            float depthTone = Sanitize01(tuning.GradeParams.w);
            float stressGrain = math.max(0f, SanitizeFinite(tuning.StressResponse.x, 0.72f));
            float stressChroma = math.max(0f, SanitizeFinite(tuning.StressResponse.y, 0.82f));

            float grain = baseGrain * math.lerp(0.42f, 1.65f, qualityCurve) * (1f + stress01 * stressGrain);
            float grainScale = math.lerp(96f, 384f, highDetail01);
            float grainSpeed = math.lerp(0.12f, 0.76f, highDetail01);
            float chroma = baseChroma * highDetail01 * (0.35f + stress01 * stressChroma);
            float glitch = baseGlitch * highDetail01 * (toxicity01 * 0.75f + stress01 * 0.25f);
            float glitchY = glitch * math.lerp(0.32f, 0.72f, highDetail01);
            float vignette = math.saturate(baseVignette + stress01 * 0.22f + depth01 * 0.18f);
            float contrast = math.max(0.1f, contrastBase + stress01 * 0.08f);
            float saturation = math.max(0f, saturationBase - stress01 * 0.18f - toxicity01 * 0.12f);
            float temperature = math.clamp(temperatureBase - depth01 * depthTone, -1f, 1f);
            float tint = math.saturate(depth01 * depthTone + toxicity01 * 0.2f);

            float wrappedTime = math.max(0f, SanitizeFinite(input.TimeSecondsWrapped, 0f));
            float abSplit01 = Sanitize01(input.AbSplit01);

            NoirPostProcessDTO output = default;
            output.GrainParams = new float4(grain, grainScale, grainSpeed, wrappedTime);
            output.AberrationParams = new float4(chroma, glitch, glitchY, vignette);
            output.ColorGrading = new float4(contrast, saturation, temperature, tint);
            output.QualityAndLimits = new float4(quality01, stress01, toxicity01, abSplit01);
            return output;
        }
    }

}
