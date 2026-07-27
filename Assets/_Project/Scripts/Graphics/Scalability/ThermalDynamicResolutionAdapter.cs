using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.XR;
#endif

namespace Hecton8.Graphics.Scalability
{
    /// <summary>
    /// Unity 6 STP dynamic-resolution governor. Dispatcher-owned; no Update path.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9947)]
    public sealed unsafe class ThermalDynamicResolutionAdapter :
        MonoBehaviour,
        ILateFrameTickable,
        ISlowTickable,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IResolutionScalerService
    {
        private static int s_x001ThermalDynamicResolutionAdapterSignalPushDropCount;
        private const int TelemetryCapacity = 300;
        private const int DispatcherRegistrationRepairMaxFrames = 1800;
        private const int TelemetryHeaderBytes = 20;
        private const int DrsTelemetryEntryBytes = 64;
        private const int ResolutionScaleStateBytes = 64;
        private const int DrsStateBytes = 16;
        private const int HardwareThermalSnapshotBytes = 24;
        private const int DynamicResolutionRuntimeSnapshotBytes = 24;
        private const uint TelemetryMagic = 0x53545041u; // STPA
        private const uint SourceHash = 0x53545051u; // STPQ
        private const uint ScaleContextHash = 0x5343414Cu; // SCAL
        private const uint DrsWarningHash = 0x44525357u; // DRSW
        private const uint UpscalerNativeHash = 0x4E415456u; // NATV
        private const uint UpscalerBilateralTaaHash = 0x42494C55u; // BILU
        private const uint UpscalerBilateralDrsHash = 0x42445253u; // BDRS
        private const uint DumpIoFailureHash = 0x44525349u; // DRSI
        private const uint CsvMinScaleLimitHash = 0xF3608E52u;
        private const uint CsvSmoothingFactorHash = 0x6D58F632u;
        private const uint CsvSharpeningMultiplierHash = 0x4ADC1687u;
        private const uint CsvLowMinScaleHash = 0xA114ECD3u;
        private const uint CsvMiddleMinScaleHash = 0x8D1CCECEu;
        private const uint CsvHighMinScaleHash = 0x0F348BAFu;
        private const uint CsvUltraMinScaleHash = 0x0328B0C7u;
        private const string DumpRelativeDirectory = "Docs/AgentLogs";
        private const string DumpFilePrefix = "Dump_THERMAL_DRS_";
        private const string DumpFileExtension = ".bin";
        private const string DumpPayloadLabel = "ThermalDrsBlackBoxDumpPayload";
        private const float DangerFrameTimeMs = 15.0f;
        private const float TargetFrameTimeMs = 16.66f;
        private const float PanicFrameTimeMs = 33.0f;
        private const float PanicSaturationFrameTimeMs = DynamicResolutionPanicEnvelope.DefaultSaturationFrameTimeMs;
        private const float PanicReleaseSeconds = DynamicResolutionPanicEnvelope.DefaultReleaseSeconds;
        private const float PanicAuthorityFlagEpsilon = 0.01f;
        private const float MinScale = 0.6f;
        private const float MaxScale = 1.5f;
        private const float PolicyMaxScale = 1.0f;
        private const float DefaultLowMinScale = 0.6f;
        private const float DefaultMidMinScale = 0.7f;
        private const float DefaultHighMinScale = 0.8f;
        private const float DefaultUltraMinScale = 0.85f;
        private const float DefaultSmoothingFactor = 8.0f;
        private const float DefaultSharpeningMultiplier = 0.8f;
        private const float DefaultPostCullScale = 0.6f;
        private const float StressEmergencyThreshold = 0.8f;
        private const float SurvivalPressureFadeStart01 = 0.12f;
        private const float SurvivalPressureFadeEnd01 = 0.44f;
        private const float SurvivalPressureFlagEpsilon = 0.001f;
        private const float ResolutionSignalThreshold = 0.05f;
        private const float NotificationThreshold = 0.4f;
        private const float EwmaAlpha = 0.18f;
        private const float ScaleEpsilon = 0.0001f;
        private const float PixelStableGridStep = 2f;
        private const float SharpenEpsilon = 0.001f;
        private const float VisualBudgetEpsilon = 0.01f;
        private const float VisualFeatureFeather = 0.14f;
        private const int AupShiftLockFrames = 3;
        private const int PressureHysteresisFrames = 3;
        private const int RecoveryHysteresisFrames = 15;
        private const int TelemetryReportCooldownFrames = 30;
        private const int CameraShieldCacheCapacity = 32;
        private const uint VisualFeatureVisorSalt = 1u << 0;
        private const uint VisualFeatureVolumetricSilt = 1u << 1;
        private const uint VisualFeatureProceduralHullDents = 1u << 2;
        private const uint VisualFeaturePom16Tap = 1u << 3;
        private const uint VisualFeatureSubsurfaceScatter = 1u << 4;
        private const uint VisualFeatureRaymarchedFog = 1u << 5;
        private const uint VisualFeatureRouteMask =
            VisualFeatureVisorSalt |
            VisualFeatureVolumetricSilt |
            VisualFeatureProceduralHullDents |
            VisualFeaturePom16Tap |
            VisualFeatureSubsurfaceScatter |
            VisualFeatureRaymarchedFog;
        private const byte FlagThermalOverride = 1 << 0;
        private const byte FlagFramePressure = 1 << 1;
        private const byte FlagNotification = 1 << 2;
        private const byte FlagInvalidState = 1 << 3;
        private const byte FlagSurvivalPressureEmergency = 1 << 4;
        private const byte FlagAupLocked = 1 << 5;
        private const byte FlagStpActive = 1 << 6;

        private static readonly PerformDynamicRes s_systemScaler = ResolveSystemScalePercentage;
        private static readonly PerformDynamicRes s_nativeScale = ResolveNativeScalePercentage;
        private static readonly int s_sharpenIntensityId = Shader.PropertyToID("_SharpenIntensity");
        private static readonly int s_stpRenderScaleId = Shader.PropertyToID("_H8StpRenderScale01");
        private static readonly int s_stpScaleDeficitId = Shader.PropertyToID("_H8StpScaleDeficit01");
        private static readonly int s_dearLieId = Shader.PropertyToID("_H8DearLie01");
        private static readonly int s_visualOverkillId = Shader.PropertyToID("_H8VisualOverkill01");
        private static readonly int s_visualFeatureFlagsId = Shader.PropertyToID("_H8VisualFeatureFlags");
        private static readonly int s_visualFeatureWeights0Id = Shader.PropertyToID("_H8VisualFeatureWeights0");
        private static readonly int s_visualFeatureWeights1Id = Shader.PropertyToID("_H8VisualFeatureWeights1");
        private static readonly int s_drsMipBiasId = Shader.PropertyToID("_H8DrsMipBias");
        private static readonly int s_drsTaaSharpenId = Shader.PropertyToID("_H8DrsTaaSharpen");
        private static readonly int s_drsScreenPixelsId = Shader.PropertyToID("_H8DrsScreenPixelDimensions");
        private static readonly int s_drsPostProcessWeightId = Shader.PropertyToID("_H8DrsHeavyPostProcessWeight");
        private static readonly int s_drsUpscalerTypeHashId = Shader.PropertyToID("_H8DrsUpscalerTypeHash");
        private static readonly int s_visorFluidOverkillId = Shader.PropertyToID("_HectonVisorFluidVisualOverkill");
        private static readonly int s_h8GlobalQualityWeightId = Shader.PropertyToID("_H8GlobalQualityWeight");
        private static readonly int s_globalQualityWeightId = Shader.PropertyToID("_GlobalQualityWeight");
        private static int s_uiLayer = -2;
        private static ThermalDynamicResolutionAdapter s_activeAdapter;
        private static float s_systemScalePercentage = 100f;

        private static readonly ulong DrsStateMutationGuardMask =
            DrsMutationGuardBit(BufferID.DrsState);

        private static readonly ulong ResolutionScaleStateMutationGuardMask =
            DrsMutationGuardBit(BufferID.ResolutionScaleState);

        private static readonly ulong ResolutionScaleTelemetryMutationGuardMask =
            DrsMutationGuardBit(BufferID.ResolutionScaleTelemetry);

        private static readonly ulong MockReconstructionInputMutationGuardMask =
            DrsMutationGuardBit((BufferID)UberNoirReconstructionVaultIds.MockSignal);

        private static readonly ulong ScalabilityStateMutationGuardMask =
            DrsMutationGuardBit(BufferID.ShinobuScalabilityState);

        private UniversalRenderPipelineAsset _urpAsset;
        private IDynamicResolutionRuntime _dynamicResolutionRuntime;
        private IDataVault _dataVault;
        private VaultGenerationHandle<DrsStateDTO> _drsStateHandle;
        private VaultGenerationHandle<ResolutionScaleState> _scaleStateHandle;
        private VaultGenerationHandle<DrsTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _scalabilityStateHandle;
        private VaultGenerationHandle<MockReconstructionInputSignal> _mockReconstructionInputHandle;
        private IDataVault _drsStateGuardVault;
        private IDataVault _scaleStateGuardVault;
        private IDataVault _telemetryGuardVault;
        private int _telemetryCursor;
        private uint _sequence;
        private float _defaultRenderScale = PolicyMaxScale;
        private float _currentScale = PolicyMaxScale;
        private float _targetScale = PolicyMaxScale;
        private float _latestFrameTimeEwmaMs = TargetFrameTimeMs;
        private float _latestSystemHealth01 = 1f;
        private float _latestGpuUtil01;
        private float _latestSystemStress01;
        private float _latestSystemStressEwma01;
        private float _panicAuthority01;
        private float _latestGlobalQualityWeight01 = PolicyMaxScale;
        private float _scalabilityQualityWeightSnapshot01 = PolicyMaxScale;
        private float _shaderQualityWeightSnapshot01 = PolicyMaxScale;
        private float _sharpenIntensity01;
        private float _dearLie01;
        private float _visualOverkill01;
        private float _minScaleLimit = DefaultLowMinScale;
        private float _smoothingFactor = DefaultSmoothingFactor;
        private float _sharpeningMultiplier = DefaultSharpeningMultiplier;
        private float _postCullScale = DefaultPostCullScale;
        private float _lastCommittedSharpenIntensity01 = -1f;
        private float _lastCommittedRenderScale01 = -1f;
        private float _lastCommittedScaleDeficit01 = -1f;
        private float _lastCommittedDearLie01 = -1f;
        private float _lastCommittedVisualOverkill01 = -1f;
        private float _lastCommittedMipBias = -1f;
        private float _lastCommittedPostProcessWeight = -1f;
        private Vector4 _lastCommittedScreenPixels;
        private Vector4 _visualFeatureWeights0;
        private Vector4 _visualFeatureWeights1;
        private Vector4 _lastCommittedVisualFeatureWeights0;
        private Vector4 _lastCommittedVisualFeatureWeights1;
        private float _lastPublishedScale = PolicyMaxScale;
        private uint _visualFeatureFlags;
        private uint _lastCommittedVisualFeatureFlags = uint.MaxValue;
        private uint _lastCommittedUpscalerHash = uint.MaxValue;
        private uint _upscalerTypeHash = UpscalerNativeHash;
        private byte _pressureLevel;
        private byte _thermalSeverity;
        private byte _foveatedPressureTier;
        private byte _hardwareTier;
        private int _screenWidthSnapshot = 1;
        private int _screenHeightSnapshot = 1;
        private HectonQualityTier _bootHardwareTier = HectonQualityTier.Unknown;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private int _lastObservedScaleMilli = -1;
        private uint _lastTelemetryReportFrame;
        private uint _frameCounter;
        private int _framesBelowTarget;
        private int _pressureFrameCount;
        private int _recoveryFrameCount = RecoveryHysteresisFrames;
        private int _aupShiftLockFrames;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _sceneLoadedRepairRegistered;
        private bool _dispatcherRegistrationRepairRunning;
        private int _dispatcherRegistrationRepairFramesRemaining;
        private bool _systemScalerInstalled;
        private bool _blackBoxDumped;
        private uint _blackBoxDumpHash;
        private bool _stpActive = true;
        private bool _coldBilateralDrsRouteAllowed;
        private bool _bilateralDrsRouteAllowed;
        private bool _scalabilityQualityWeightSnapshotValid;
        private bool _shaderQualityWeightSnapshotValid;
        private bool _cameraShieldRegistered;
        private bool _cameraShieldColdRefreshRequested;
        private bool _lateFrameRegistrationRequested;
        private bool _mockQualityWeightActive;
        private bool _mockReconstructionScaleActive;
        private bool _mockReconstructionQualityActive;
        private bool _visualFeatureWeightsCommitted;
        private bool _pendingRenderScaleCommitDirty;
        private bool _pendingRuntimeSnapshotDirty;
        private bool _pendingSharpenGlobalDirty;
        private bool _pendingVisualBudgetGlobalsDirty;
        private bool _drsStateGuardHeld;
        private bool _scaleStateGuardHeld;
        private bool _telemetryGuardHeld;
        private byte _pendingRenderScaleCommitFlags;
        private byte _pendingRuntimeSnapshotFlags;
        private float _mockQualityWeight01 = 1f;
        private float _mockReconstructionScale01 = PolicyMaxScale;
        private float _mockReconstructionQuality01 = PolicyMaxScale;
        private string _blackBoxDumpPath;
        private DrsScaleLimitsDTO _scaleLimits;
        private ResolutionScaleState _scaleStateMirror;
        private DrsStateDTO _drsState;
        private bool _scaleStateMirrorValid;
        private int _cameraShieldCachedCount;
        private readonly Camera[] _cameraShieldSnapshot = new Camera[CameraShieldCacheCapacity];
        private readonly Camera[] _cameraShieldCameras = new Camera[CameraShieldCacheCapacity];
        private readonly ulong[] _cameraShieldEntityIds = new ulong[CameraShieldCacheCapacity];
        private readonly byte[] _cameraShieldWorldCameraFlags = new byte[CameraShieldCacheCapacity];

#if UNITY_ANDROID && !UNITY_EDITOR
        private float _lastXrScale = -1f;
#endif

        public float CurrentRenderScale01 => _currentScale;
        public float TargetRenderScale01 => _targetScale;
        public float SystemStress01 => _latestSystemStress01;
        public float SystemStressEwma01 => _latestSystemStressEwma01;
        public float SharpenIntensity01 => _sharpenIntensity01;
        public byte HardwareTier => _hardwareTier;
        public bool StpActive => _stpActive;

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct DrsScaleLimitsDTO
        {
            [FieldOffset(0)]
            public float LowMinScale;
            [FieldOffset(4)]
            public float MiddleMinScale;
            [FieldOffset(8)]
            public float HighMinScale;
            [FieldOffset(12)]
            public float UltraMinScale;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct DrsTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public float CurrentScale01;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float TargetScale01;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float FrameTimeEwmaMs;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float SystemStress01;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float SystemStressEwma01;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float SharpenIntensity01;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public uint Flags;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public uint Sequence;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public uint UpscalerComputeTimeMsBits;
            [System.Runtime.InteropServices.FieldOffset(40)]
            public ushort HysteresisCounters;
            [System.Runtime.InteropServices.FieldOffset(42)]
            public ushort FramesBelowTarget;
            [System.Runtime.InteropServices.FieldOffset(44)]
            public byte PressureLevel;
            [System.Runtime.InteropServices.FieldOffset(45)]
            public byte ThermalSeverity;
            [System.Runtime.InteropServices.FieldOffset(46)]
            public byte StpActive;
            [System.Runtime.InteropServices.FieldOffset(47)]
            public byte AupLockFrames;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad3;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeAdapter = null;
            s_systemScalePercentage = 100f;
            DynamicResolutionHandler.SetSystemDynamicResScaler(s_nativeScale, DynamicResScalePolicyType.ReturnsPercentage);
            DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.User);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying || s_activeAdapter != null)
                return;

            GameObject host = new GameObject("[STP Dynamic Resolution Adapter]");
            DontDestroyOnLoad(host);
            host.AddComponent<ThermalDynamicResolutionAdapter>();
        }

        private static float ResolveSystemScalePercentage()
        {
            return s_systemScalePercentage;
        }

        private static float ResolveNativeScalePercentage()
        {
            return 100f;
        }

        private void Awake()
        {
            if (s_activeAdapter != null && s_activeAdapter != this)
            {
                Destroy(gameObject);
                return;
            }

            s_activeAdapter = this;
            if (!ValidateAbiLayout())
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)TelemetryMagic));
                enabled = false;
                return;
            }

            ResolveBlackBoxDumpPathCold();
            _urpAsset = UniversalRenderPipeline.asset;
            _defaultRenderScale = _urpAsset != null ? ClampRenderScale(_urpAsset.renderScale) : PolicyMaxScale;
            _currentScale = math.min(_defaultRenderScale, PolicyMaxScale);
            _targetScale = _currentScale;
            _lastPublishedScale = _currentScale;
            _lastObservedScaleMilli = ScaleToMilli(_currentScale);
            SetVector4(ref _lastCommittedScreenPixels, -1f, -1f, -1f, -1f);
            SetVector4(ref _lastCommittedVisualFeatureWeights0, -1f, -1f, -1f, -1f);
            SetVector4(ref _lastCommittedVisualFeatureWeights1, -1f, -1f, -1f, -1f);
            s_systemScalePercentage = _currentScale * 100f;
            CacheGraphicsCapabilitySnapshotCold();
            RefreshRenderSurfaceSnapshotCold();
            _bootHardwareTier = ResolveBootHardwareTier();
            RefreshQualityInputSnapshotsCold();
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            RefreshQualityTierPolicyFromContinuousWeight(_latestGlobalQualityWeight01);
            GenerateEmergencyMockLimits();
            _minScaleLimit = ResolveMinScaleLimit(_latestGlobalQualityWeight01);
            _upscalerTypeHash = ResolveUpscalerHash(_currentScale);
            _drsState = default;
            _drsState.CurrentRenderScale = _currentScale;
            _drsState.TargetRenderScale = _targetScale;
            _drsState.UpscalerTypeHash = _upscalerTypeHash;
            RebindDataVault(GlobalRegistry.DataVault);
            ApplyQualitySnapshotPolicyCold();
            TryEnsureDrsStateHandle(allowAllocation: true);
            TryEnsureTelemetryHandle(allowAllocation: true);
            TryEnsureScaleStateHandle(allowAllocation: true);
            UpdateVisualBudget(_latestGlobalQualityWeight01, _latestSystemStressEwma01, _currentScale);
            UpdateDrsState();
            UpdateScaleState(0);
            ApplySharpenGlobal();
            ApplyVisualBudgetGlobals();
            InstallSystemDynamicResolutionScaler();
            RebindDynamicResolutionRuntime(GlobalRegistry.DynamicResolutionRuntime);
        }

        private void OnEnable()
        {
            if (!TryClaimActiveAdapterAfterReloadCold())
                return;

            if (Application.isPlaying)
            {
                RebindDataVault(GlobalRegistry.DataVault);
                RebindDynamicResolutionRuntime(GlobalRegistry.DynamicResolutionRuntime);
                RefreshRenderSurfaceSnapshotCold();
                ApplyQualitySnapshotPolicyCold();
                RegisterResolutionScalerService();
                InstallSystemDynamicResolutionScaler();
                RegisterCameraShield();
                CommitRenderScale(0);
            }

            TryRegister();
            TryRegisterLateFrame();
            TryRegisterHotSwap();
            RegisterSceneLoadedRepair();
            RequestDispatcherPhaseRegistrationRepair();
        }

        private void Start()
        {
            if (!TryClaimActiveAdapterAfterReloadCold())
                return;

            RefreshCameraShieldCacheCold();
            RegisterResolutionScalerService();
            TryRegister();
            TryRegisterLateFrame();
            TryRegisterHotSwap();
            RegisterSceneLoadedRepair();
            RequestDispatcherPhaseRegistrationRepair();
        }

        private void OnDisable()
        {
            bool ownsAdapter = ReferenceEquals(s_activeAdapter, this);
            _dispatcherRegistrationRepairRunning = false;
            _dispatcherRegistrationRepairFramesRemaining = 0;
            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterHotSwap();
            UnregisterSceneLoadedRepair();
            UnregisterResolutionScalerService();
            if (ownsAdapter)
            {
                ClearSystemOverrideRenderScale();
                ReleaseSystemDynamicResolutionScaler();
                UnregisterCameraShield();
            }
        }

        private void OnDestroy()
        {
            Dispose();
            if (ReferenceEquals(s_activeAdapter, this))
                s_activeAdapter = null;
        }

        public void Dispose()
        {
            bool ownsAdapter = ReferenceEquals(s_activeAdapter, this);
            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterHotSwap();
            UnregisterSceneLoadedRepair();
            UnregisterResolutionScalerService();
            if (ownsAdapter)
            {
                ClearSystemOverrideRenderScale();
                ReleaseSystemDynamicResolutionScaler();
                UnregisterCameraShield();
            }

            ReleaseActiveVaultGuards();
            _scaleStateHandle = default;
            _telemetryHandle = default;
            _drsStateHandle = default;
            _scalabilityStateHandle = default;
            _mockReconstructionInputHandle = default;
            _dataVault = null;
            _blackBoxDumpPath = null;
        }

        private void AdvanceThermalResolutionState(float deltaTime)
        {
            if (!ReferenceEquals(s_activeAdapter, this))
                return;

            _frameCounter = unchecked(_frameCounter + 1u);
            float tickFrameMs = SanitizePositive(deltaTime * 1000f, TargetFrameTimeMs);
            _latestFrameTimeEwmaMs = math.lerp(
                SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs),
                tickFrameMs,
                EwmaAlpha);
            ConsumeSignals();
            _latestFrameTimeEwmaMs = SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs);
            _latestSystemHealth01 = Sanitize01(_latestSystemHealth01);
            _latestGpuUtil01 = Sanitize01(_latestGpuUtil01);
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            RefreshQualityTierPolicyFromContinuousWeight(_latestGlobalQualityWeight01);
            _stpActive = ResolveStpIntent(_bootHardwareTier, _cachedQualityTier);
            _latestSystemStress01 = ResolveSystemStressInput01();
            ApplySystemStressEwmaInline(_latestSystemStress01);

            if (RecoverInvalidScaleState())
                return;

            byte flags = _stpActive ? FlagStpActive : (byte)0;
            float qualityWeight01 = Sanitize01(_latestGlobalQualityWeight01);
            float stress01 = Sanitize01(_latestSystemStressEwma01);
            _minScaleLimit = ResolveMinScaleLimit(qualityWeight01);
            float requestedScale = ResolvePolicyScale(qualityWeight01, stress01, ref flags);
            bool pressureActive = (flags & (FlagFramePressure | FlagThermalOverride | FlagSurvivalPressureEmergency)) != 0;

            if (_aupShiftLockFrames > 0)
            {
                flags |= FlagAupLocked;
                _targetScale = _currentScale;
                UpdateVisualBudget(qualityWeight01, stress01, _currentScale);
                QueueRuntimeSnapshotCommit(flags);
                QueueVisualBudgetGlobals();
                _aupShiftLockFrames--;
                UpdateDrsState();
                UpdateScaleState(flags);
                WriteTelemetry(flags);
                return;
            }

            float desiredTargetScale = ResolveHysteresisTarget(requestedScale, pressureActive);
            float panicScaleLimit = ResolvePanicScaleLimit(qualityWeight01);
            AdvancePanicAuthority(deltaTime);
            if (_panicAuthority01 > PanicAuthorityFlagEpsilon)
                flags |= FlagFramePressure;

            desiredTargetScale = DynamicResolutionPanicEnvelope.ApplyCollapse(
                desiredTargetScale,
                panicScaleLimit,
                _panicAuthority01);

            float targetScale = DynamicResolutionPanicEnvelope.ApplyCollapse(
                ResolveSmoothedTargetScale(_targetScale, desiredTargetScale, deltaTime),
                desiredTargetScale,
                _panicAuthority01);
            float nextScale = DynamicResolutionPanicEnvelope.ApplyCollapse(
                ResolveSmoothedRenderScale(_currentScale, targetScale, deltaTime),
                targetScale,
                _panicAuthority01);
            if (_mockReconstructionScaleActive)
            {
                flags |= FlagFramePressure;
                targetScale = _mockReconstructionScale01;
                nextScale = _mockReconstructionScale01;
            }

            nextScale = ClampRenderScale(nextScale);
            nextScale = ResolvePixelStableRenderScale(nextScale);
            _targetScale = targetScale;
            _sharpenIntensity01 = ResolveSharpenIntensity(nextScale);
            _upscalerTypeHash = ResolveUpscalerHash(nextScale);
            UpdateVisualBudget(qualityWeight01, stress01, nextScale);
            bool notifyScale = nextScale < NotificationThreshold;
            if (notifyScale)
                flags |= FlagNotification;

            if (math.abs(nextScale - _currentScale) > ScaleEpsilon)
            {
                _currentScale = nextScale;
                QueueRenderScaleCommit(flags);
            }
            else
            {
                QueueRuntimeSnapshotCommit(flags);
                QueueSharpenGlobal();
                QueueVisualBudgetGlobals();
            }

            UpdateScaleState(flags);
            UpdateDrsState();
            WriteTelemetry(flags);
        }

        public void LateFrameTick()
        {
            AdvanceDispatcherRegistrationRepair();
            AdvanceThermalResolutionState(SystemDispatcher.CurrentFrameUnscaledDeltaTime);

            if (!ReferenceEquals(s_activeAdapter, this))
                return;

            ApplyCameraShieldCached();

            if (_pendingRenderScaleCommitDirty)
            {
                byte flags = _pendingRenderScaleCommitFlags;
                _pendingRenderScaleCommitDirty = false;
                _pendingRuntimeSnapshotDirty = false;
                _pendingSharpenGlobalDirty = false;
                _pendingVisualBudgetGlobalsDirty = false;
                CommitRenderScale(flags);
                return;
            }

            if (_pendingRuntimeSnapshotDirty)
            {
                _pendingRuntimeSnapshotDirty = false;
                CommitRuntimeSnapshot(_pendingRuntimeSnapshotFlags);
            }

            if (_pendingSharpenGlobalDirty)
            {
                _pendingSharpenGlobalDirty = false;
                ApplySharpenGlobal();
            }

            if (_pendingVisualBudgetGlobalsDirty)
            {
                _pendingVisualBudgetGlobalsDirty = false;
                ApplyVisualBudgetGlobals();
            }
        }

        public void SlowTick()
        {
            AdvanceDispatcherRegistrationRepair();

            if (_lateFrameRegistrationRequested)
            {
                _lateFrameRegistrationRequested = false;
                TryRegisterLateFrame();
            }

            if (_cameraShieldColdRefreshRequested)
            {
                RefreshCameraShieldCacheCold();
                _cameraShieldColdRefreshRequested = false;
            }

            RefreshRenderSurfaceSnapshotCold();
            ConsumeMockReconstructionInputFromVault();
            ApplyQualitySnapshotPolicyCold();
        }

        public bool TryGetScaleState(out ResolutionScaleState state)
        {
            if (!ReferenceEquals(s_activeAdapter, this) || !_scaleStateMirrorValid)
            {
                state = default;
                return false;
            }

            state = _scaleStateMirror;
            return true;
        }

        public ref readonly DrsStateDTO GetDrsStateReadOnly()
        {
            return ref _drsState;
        }

        public void GetTunerSettings(
            out float minScaleLimit,
            out float smoothingFactor,
            out float sharpeningMultiplier)
        {
            minScaleLimit = _minScaleLimit;
            smoothingFactor = _smoothingFactor;
            sharpeningMultiplier = _sharpeningMultiplier;
        }

        public void ApplyTunerSettings(
            float minScaleLimit,
            float smoothingFactor,
            float sharpeningMultiplier)
        {
            if (math.isfinite(minScaleLimit))
            {
                ApplyMinScaleLimitPreference(minScaleLimit);
                _minScaleLimit = ResolveMinScaleLimit(_latestGlobalQualityWeight01);
            }

            if (math.isfinite(smoothingFactor))
                _smoothingFactor = math.clamp(smoothingFactor, 0.1f, 32f);
            if (math.isfinite(sharpeningMultiplier))
                _sharpeningMultiplier = math.clamp(sharpeningMultiplier, 0f, 2f);

            UpdateDrsState();
        }

#if UNITY_EDITOR
        public bool TryApplyCsvProfile(ReadOnlySpan<char> csvText)
        {
            bool changed = false;
            int lineStart = 0;
            for (int i = 0; i <= csvText.Length; i++)
            {
                if (i < csvText.Length && csvText[i] != '\n')
                    continue;

                ReadOnlySpan<char> line = csvText.Slice(lineStart, i - lineStart).Trim();
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == '#')
                    continue;

                int comma = line.IndexOf(',');
                if (comma <= 0 || comma >= line.Length - 1)
                    continue;

                ReadOnlySpan<char> key = line.Slice(0, comma).Trim();
                ReadOnlySpan<char> valueSpan = line.Slice(comma + 1).Trim();
                if (!TryParseCsvFloat(valueSpan, out float value))
                {
                    continue;
                }

                uint hash = HashCsvKey(key);
                if (hash == CsvMinScaleLimitHash)
                {
                    ApplyMinScaleLimitPreference(value);
                    _minScaleLimit = ResolveMinScaleLimit(_latestGlobalQualityWeight01);
                    changed = true;
                }
                else if (hash == CsvSmoothingFactorHash)
                {
                    _smoothingFactor = math.clamp(value, 0.1f, 32f);
                    changed = true;
                }
                else if (hash == CsvSharpeningMultiplierHash)
                {
                    _sharpeningMultiplier = math.clamp(value, 0f, 2f);
                    changed = true;
                }
                else if (hash == CsvLowMinScaleHash)
                {
                    _scaleLimits.LowMinScale = math.clamp(value, MinScale, PolicyMaxScale);
                    changed = true;
                }
                else if (hash == CsvMiddleMinScaleHash)
                {
                    _scaleLimits.MiddleMinScale = math.clamp(value, MinScale, PolicyMaxScale);
                    changed = true;
                }
                else if (hash == CsvHighMinScaleHash)
                {
                    _scaleLimits.HighMinScale = math.clamp(value, MinScale, PolicyMaxScale);
                    changed = true;
                }
                else if (hash == CsvUltraMinScaleHash)
                {
                    _scaleLimits.UltraMinScale = math.clamp(value, MinScale, PolicyMaxScale);
                    changed = true;
                }
            }

            if (changed)
            {
                _minScaleLimit = ResolveMinScaleLimit(_latestGlobalQualityWeight01);
                UpdateDrsState();
            }

            return changed;
        }
#endif

        public int CopyTelemetryForEditor(
            float[] currentScale,
            float[] targetScale,
            float[] stress,
            int capacity)
        {
            if (currentScale == null || targetScale == null || stress == null || capacity <= 0)
                return 0;

            if (!TryAcquireTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
                return 0;

            try
            {
                int count = math.min(math.min(capacity, TelemetryCapacity), telemetryLength);
                for (int i = 0; i < count; i++)
                {
                    int index = _telemetryCursor + i;
                    if (index >= count)
                        index -= count;

                    DrsTelemetryEntry entry = telemetryRing[index];
                    currentScale[i] = Sanitize01(entry.CurrentScale01);
                    targetScale[i] = Sanitize01(entry.TargetScale01);
                    stress[i] = Sanitize01(entry.SystemStressEwma01);
                }

                return count;
            }
            finally
            {
                ReleaseTelemetryPointer();
            }
        }

        public void SetMockQualityWeightForTuner(float qualityWeight01, bool active)
        {
            _mockQualityWeightActive = active;
            _mockQualityWeight01 = math.isfinite(qualityWeight01) ? math.saturate(qualityWeight01) : 1f;
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
        }

        public void ConsumeMockQualityWeightSignal(in MockQualityWeightSignal signal)
        {
            if (!math.isfinite(signal.GlobalQualityWeight))
                return;

            _mockQualityWeightActive = true;
            _mockQualityWeight01 = math.saturate(signal.GlobalQualityWeight);
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            if (signal.FrameTimeMs > 0f && math.isfinite(signal.FrameTimeMs))
                _latestFrameTimeEwmaMs = math.max(_latestFrameTimeEwmaMs, signal.FrameTimeMs);
        }

        public void ConsumeMockReconstructionInputSignal(in MockReconstructionInputSignal signal)
        {
            if (!math.isfinite(signal.RenderScale01) ||
                !math.isfinite(signal.GlobalQualityWeight01))
            {
                return;
            }

            bool active = signal.Flags != 0u && signal.RenderScale01 > 0f;
            _mockReconstructionScaleActive = active;
            _mockReconstructionScale01 = active
                ? math.clamp(signal.RenderScale01, 0.3f, PolicyMaxScale)
                : PolicyMaxScale;
            _mockReconstructionQualityActive = active;
            _mockReconstructionQuality01 = active
                ? math.saturate(signal.GlobalQualityWeight01)
                : PolicyMaxScale;
            if (!active)
            {
                _latestGlobalQualityWeight01 = _mockQualityWeightActive
                    ? ResolveQualitySignalWeight()
                    : PolicyMaxScale;
                return;
            }

            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            float forcedScale = _mockReconstructionScale01;
            _currentScale = forcedScale;
            _targetScale = forcedScale;
            s_systemScalePercentage = forcedScale * 100f;
            _drsState.CurrentRenderScale = forcedScale;
            _drsState.TargetRenderScale = forcedScale;
            _drsState.UpscalerTypeHash = ResolveUpscalerHash(forcedScale);
            if (_scaleStateMirrorValid)
            {
                _scaleStateMirror.CurrentRenderScale01 = forcedScale;
                _scaleStateMirror.TargetRenderScale01 = forcedScale;
                _scaleStateMirror.GlobalQualityWeight01 = _latestGlobalQualityWeight01;
            }

            if (signal.FrameTimeMs > 0f && math.isfinite(signal.FrameTimeMs))
                _latestFrameTimeEwmaMs = math.max(_latestFrameTimeEwmaMs, signal.FrameTimeMs);
        }

        public void ForceMockQualityWeightDrop()
        {
            MockQualityWeightSignal signal = default;
            signal.GlobalQualityWeight = 0.2f;
            signal.FrameTimeMs = TargetFrameTimeMs;
            signal.Flags = 1u;
            ConsumeMockQualityWeightSignal(in signal);
            // Cold editor/tuner sync path only; never called from Tick.
            ApplyMockQualityWeightDropColdSync();
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime)
                RebindDynamicResolutionRuntime(currentService as IDynamicResolutionRuntime);
            else if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RebindDataVault(currentService as IDataVault);
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null)
            {
                TryRegister();
                RequestDispatcherPhaseRegistrationRepair();
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime)
                RebindDynamicResolutionRuntime(currentService as IDynamicResolutionRuntime);
            else if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RebindDataVault(currentService as IDataVault);
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                TryUnregisterLateFrame();
                if (currentService != null)
                {
                    TryRegister();
                    RequestDispatcherPhaseRegistrationRepair();
                }
            }
        }

        private void ConsumeSignals()
        {
            float frameTimeEwmaMs = 0f;
            bool frameTimeReceived = false;
            float systemStressSignal01 = 0f;
            bool systemHealthReceived = false;
            float gpuUtil01 = 0f;
            bool gpuUtilReceived = false;
            byte pressureLevel = 0;
            bool pressureReceived = false;
            byte foveatedPressureTier = 0;
            bool foveatedPressureReceived = false;

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                ref readonly SystemHealthSignal signal = ref healthSignals[i];
                systemStressSignal01 = math.max(systemStressSignal01, Sanitize01(signal.SystemHealthIndex01));
                systemHealthReceived = true;
                gpuUtil01 = math.max(gpuUtil01, Sanitize01(signal.GpuUtil01));
                gpuUtilReceived = true;
                pressureLevel = MaxByte(pressureLevel, signal.PressureLevel);
                pressureReceived = true;
                foveatedPressureTier = MaxByte(foveatedPressureTier, signal.FoveatedPressureTier);
                foveatedPressureReceived = true;
                if (signal.FpsEwma > 0f)
                {
                    frameTimeEwmaMs = math.max(frameTimeEwmaMs, 1000f * math.rcp(math.max(1f, signal.FpsEwma)));
                    frameTimeReceived = true;
                }
            }

            if (frameTimeReceived)
                _latestFrameTimeEwmaMs = frameTimeEwmaMs;
            if (systemHealthReceived)
                _latestSystemHealth01 = 1f - systemStressSignal01;
            if (gpuUtilReceived)
                _latestGpuUtil01 = gpuUtil01;
            if (pressureReceived)
                _pressureLevel = pressureLevel;
            if (foveatedPressureReceived)
                _foveatedPressureTier = foveatedPressureTier;

            ReadOnlySpan<ThermalStateChangedSignal> thermalSignals = SignalBus<ThermalStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < thermalSignals.Length; i++)
            {
                ref readonly ThermalStateChangedSignal signal = ref thermalSignals[i];
                _thermalSeverity = signal.Severity;
            }

            ReadOnlySpan<AupShiftSignal> shiftSignals = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            if (shiftSignals.Length > 0)
                _aupShiftLockFrames = math.max(_aupShiftLockFrames, AupShiftLockFrames);

        }

        private unsafe void ConsumeMockReconstructionInputFromVault()
        {
            if (!TryReadMockReconstructionInputFromVault(out MockReconstructionInputSignal signal))
                return;

            ConsumeMockReconstructionInputSignal(in signal);
        }

        private unsafe bool TryReadMockReconstructionInputFromVault(out MockReconstructionInputSignal signal)
        {
            signal = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            BufferID bufferId = (BufferID)UberNoirReconstructionVaultIds.MockSignal;
            if (!TryAcquireDrsGuard(vault, MockReconstructionInputMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenExistingVaultBuffer(
                        vault,
                        ref _mockReconstructionInputHandle,
                        bufferId,
                        1,
                        out NativeArray<MockReconstructionInputSignal> buffer))
                {
                    return false;
                }

                signal = buffer[0];
                if (signal.Flags == 0u &&
                    !_mockReconstructionScaleActive &&
                    !_mockReconstructionQualityActive)
                {
                    return false;
                }

                return math.isfinite(signal.RenderScale01) &&
                       math.isfinite(signal.GlobalQualityWeight01);
            }
            finally
            {
                ReleaseDrsGuard(vault, MockReconstructionInputMutationGuardMask);
            }
        }

        private float ResolvePolicyScale(float qualityWeight01, float stress01, ref byte flags)
        {
            float qualitySignal01 = Sanitize01(qualityWeight01);
            float survivalPressureWeight01 = ResolveSurvivalPressureWeight01(qualitySignal01);
            float qualityWeight = ResolveGlobalQualityWeight(stress01);
            float minScaleLimit = ResolveMinScaleLimit(qualitySignal01);
            float requestedScale = math.lerp(minScaleLimit, PolicyMaxScale, qualityWeight);

            float stressCollapse01 = SmoothRange01(stress01, ResolveStressCollapseStart(qualitySignal01), PolicyMaxScale);
            requestedScale = math.lerp(requestedScale, minScaleLimit, stressCollapse01);
            if (stressCollapse01 * survivalPressureWeight01 > SurvivalPressureFlagEpsilon)
            {
                flags |= FlagSurvivalPressureEmergency;
            }

            float frameTimeMs = SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs);
            float framePressure01 = ResolveFramePressureCollapse01(frameTimeMs);
            if (framePressure01 > 0f)
            {
                float safeFrameTimeMs = math.max(frameTimeMs, TargetFrameTimeMs);
                float frameScale = math.saturate(TargetFrameTimeMs * math.rcp(math.max(safeFrameTimeMs, 0.0001f)));
                float frameScaleLimit = math.min(requestedScale, math.max(minScaleLimit, frameScale));
                if (frameScaleLimit < requestedScale - ScaleEpsilon)
                {
                    requestedScale = math.lerp(requestedScale, frameScaleLimit, framePressure01);
                    flags |= FlagFramePressure;
                }
            }

            float pressureCollapse01 = ResolveThermalPressureCollapse01(_pressureLevel, _thermalSeverity);
            if (pressureCollapse01 > 0f)
            {
                requestedScale = math.lerp(requestedScale, minScaleLimit, pressureCollapse01);
                flags |= FlagThermalOverride;
            }

            return ClampRenderScale(requestedScale);
        }

        private float ResolveSystemStressInput01()
        {
            float frameStress = 0f;
            float frameTimeMs = SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs);
            if (frameTimeMs > TargetFrameTimeMs)
                frameStress = math.saturate((frameTimeMs - TargetFrameTimeMs) * math.rcp(TargetFrameTimeMs));

            float pressureStress = math.saturate(_pressureLevel * 0.25f);
            float healthStress = 1f - Sanitize01(_latestSystemHealth01);
            return math.max(math.max(healthStress, _latestGpuUtil01), math.max(frameStress, pressureStress));
        }

        private void CommitRenderScale(byte flags)
        {
            _currentScale = ClampRenderScale(_currentScale);
            _targetScale = ClampRenderScale(_targetScale);
            _upscalerTypeHash = ResolveUpscalerHash(_currentScale);
            UpdateDrsState();
            s_systemScalePercentage = _currentScale * 100f;
            DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.System);

            if (_dynamicResolutionRuntime != null)
            {
                CommitRuntimeSnapshot(flags);
            }
            else if (_urpAsset != null)
            {
                ApplyDirectRenderScale(_currentScale, _currentScale);
            }

            CommitQuestXrScale();
            ApplySharpenGlobal();
            ApplyVisualBudgetGlobals();
            PublishResolutionChangedSignalIfNeeded(flags);
            PublishScaleTelemetryIfChanged();
        }

        private void CommitRuntimeSnapshot(byte flags)
        {
            IDynamicResolutionRuntime runtime = _dynamicResolutionRuntime;
            if (runtime != null)
            {
                float currentScale = ClampRenderScale(_currentScale);
                float targetScale = ClampRenderScale(_targetScale);
                runtime.ApplySystemOverrideRenderScale(
                    currentScale,
                    targetScale,
                    SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs),
                    _pressureLevel,
                    flags);
            }
        }

        private bool TryEnsureScaleStateHandle(bool allowAllocation = false)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return OpenOrAcquireVaultBuffer(
                vault,
                ref _scaleStateHandle,
                BufferID.ResolutionScaleState,
                1,
                NativeArrayOptions.ClearMemory,
                allowAllocation,
                out _);
        }

        private bool TryEnsureDrsStateHandle(bool allowAllocation = false)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return OpenOrAcquireVaultBuffer(
                vault,
                ref _drsStateHandle,
                BufferID.DrsState,
                1,
                NativeArrayOptions.UninitializedMemory,
                allowAllocation,
                out _);
        }

        private bool TryAcquireDrsStatePointer(out DrsStateDTO* drsState, out int drsStateLength)
        {
            drsState = null;
            drsStateLength = 0;
            if (_drsStateGuardHeld || !TryEnsureDrsStateHandle())
                return false;

            IDataVault vault = _dataVault;
            if (!TryAcquireDrsGuard(vault, DrsStateMutationGuardMask))
                return false;

            _drsStateGuardVault = vault;
            _drsStateGuardHeld = true;
            bool handedOff = false;
            try
            {
                if (!TryOpenVaultBuffer(
                        vault,
                        ref _drsStateHandle,
                        BufferID.DrsState,
                        1,
                        out NativeArray<DrsStateDTO> buffer))
                {
                    return false;
                }

                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                if (pointer == null)
                    return false;

                drsState = (DrsStateDTO*)pointer;
                drsStateLength = buffer.Length;
                handedOff = true;
                return true;
            }
            finally
            {
                if (!handedOff)
                    ReleaseDrsStatePointer();
            }
        }

        private void ReleaseDrsStatePointer()
        {
            if (!_drsStateGuardHeld)
            {
                _drsStateGuardVault = null;
                return;
            }

            IDataVault vault = _drsStateGuardVault;
            _drsStateGuardVault = null;
            _drsStateGuardHeld = false;
            ReleaseDrsGuard(vault, DrsStateMutationGuardMask);
        }

        private bool TryAcquireScaleStatePointer(out ResolutionScaleState* scaleState, out int scaleStateLength)
        {
            scaleState = null;
            scaleStateLength = 0;
            if (_scaleStateGuardHeld || !TryEnsureScaleStateHandle())
                return false;

            IDataVault vault = _dataVault;
            if (!TryAcquireDrsGuard(vault, ResolutionScaleStateMutationGuardMask))
                return false;

            _scaleStateGuardVault = vault;
            _scaleStateGuardHeld = true;
            bool handedOff = false;
            try
            {
                if (!TryOpenVaultBuffer(
                        vault,
                        ref _scaleStateHandle,
                        BufferID.ResolutionScaleState,
                        1,
                        out NativeArray<ResolutionScaleState> buffer))
                {
                    return false;
                }

                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                if (pointer == null)
                    return false;

                scaleState = (ResolutionScaleState*)pointer;
                scaleStateLength = buffer.Length;
                handedOff = true;
                return true;
            }
            finally
            {
                if (!handedOff)
                    ReleaseScaleStatePointer();
            }
        }

        private void ReleaseScaleStatePointer()
        {
            if (!_scaleStateGuardHeld)
            {
                _scaleStateGuardVault = null;
                return;
            }

            IDataVault vault = _scaleStateGuardVault;
            _scaleStateGuardVault = null;
            _scaleStateGuardHeld = false;
            ReleaseDrsGuard(vault, ResolutionScaleStateMutationGuardMask);
        }

        private bool TryAcquireTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength)
        {
            telemetryRing = null;
            telemetryLength = 0;
            if (_telemetryGuardHeld || !TryEnsureTelemetryHandle())
                return false;

            IDataVault vault = _dataVault;
            if (!TryAcquireDrsGuard(vault, ResolutionScaleTelemetryMutationGuardMask))
                return false;

            _telemetryGuardVault = vault;
            _telemetryGuardHeld = true;
            bool handedOff = false;
            try
            {
                if (!TryOpenVaultBuffer(
                        vault,
                        ref _telemetryHandle,
                        BufferID.ResolutionScaleTelemetry,
                        TelemetryCapacity,
                        out NativeArray<DrsTelemetryEntry> buffer))
                {
                    return false;
                }

                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                if (pointer == null)
                    return false;

                telemetryRing = (DrsTelemetryEntry*)pointer;
                telemetryLength = buffer.Length;
                handedOff = true;
                return true;
            }
            finally
            {
                if (!handedOff)
                    ReleaseTelemetryPointer();
            }
        }

        private void ReleaseTelemetryPointer()
        {
            if (!_telemetryGuardHeld)
            {
                _telemetryGuardVault = null;
                return;
            }

            IDataVault vault = _telemetryGuardVault;
            _telemetryGuardVault = null;
            _telemetryGuardHeld = false;
            ReleaseDrsGuard(vault, ResolutionScaleTelemetryMutationGuardMask);
        }

        private bool TryEnsureTelemetryHandle(bool allowAllocation = false)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return OpenOrAcquireVaultBuffer(
                vault,
                ref _telemetryHandle,
                BufferID.ResolutionScaleTelemetry,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory,
                allowAllocation,
                out _);
        }

        private void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseActiveVaultGuards();
            _dataVault = vault;
            _drsStateHandle = default;
            _scaleStateHandle = default;
            _telemetryHandle = default;
            _scalabilityStateHandle = default;
            _mockReconstructionInputHandle = default;

            if (_dataVault != null && !_dataVault.IsAllocationLocked)
            {
                TryEnsureDrsStateHandle(allowAllocation: true);
                TryEnsureTelemetryHandle(allowAllocation: true);
                TryEnsureScaleStateHandle(allowAllocation: true);
            }
        }

        private void ReleaseActiveVaultGuards()
        {
            ReleaseDrsStatePointer();
            ReleaseScaleStatePointer();
            ReleaseTelemetryPointer();
        }

        private void UpdateScaleState(byte flags)
        {
            ResolutionScaleState mirror = default;
            PopulateScaleState(ref mirror, flags);
            _scaleStateMirror = mirror;
            _scaleStateMirrorValid = true;

            if (!TryAcquireScaleStatePointer(out ResolutionScaleState* scaleState, out int scaleStateLength))
                return;

            try
            {
                if (scaleStateLength <= 0)
                    return;

                UnsafeUtility.AsRef<ResolutionScaleState>(scaleState) = mirror;
            }
            finally
            {
                ReleaseScaleStatePointer();
            }
        }

        private void PopulateScaleState(ref ResolutionScaleState state, byte flags)
        {
            byte stateFlags = 0;
            if ((flags & FlagSurvivalPressureEmergency) != 0)
                stateFlags |= ResolutionScaleStateFlags.SurvivalPressureEmergency;
            if ((flags & FlagFramePressure) != 0)
                stateFlags |= ResolutionScaleStateFlags.FramePressure;
            if ((flags & FlagThermalOverride) != 0)
                stateFlags |= ResolutionScaleStateFlags.ThermalPressure;
            if ((flags & FlagAupLocked) != 0)
                stateFlags |= ResolutionScaleStateFlags.AupLocked;
            if ((flags & FlagInvalidState) != 0)
                stateFlags |= ResolutionScaleStateFlags.InvalidStateRecovered;

            state.CurrentRenderScale01 = _currentScale;
            state.TargetRenderScale01 = _targetScale;
            state.SystemStress01 = _latestSystemStress01;
            state.SystemStressEwma01 = _latestSystemStressEwma01;
            state.FrameTimeEwmaMs = _latestFrameTimeEwmaMs;
            state.SharpenIntensity01 = _sharpenIntensity01;
            state.Frame = _frameCounter;
            state.Sequence = _sequence;
            state.HardwareTier = _hardwareTier;
            state.StpActive = _stpActive ? (byte)1 : (byte)0;
            state.Flags = stateFlags;
            state.AupLockFrames = (byte)math.clamp(_aupShiftLockFrames, 0, byte.MaxValue);
            state.Reserved0 = 0;
            state.VisualOverkill01 = _visualOverkill01;
            state.DearLie01 = _dearLie01;
            state.VisualFeatureFlags = _visualFeatureFlags;
            state.GlobalQualityWeight01 = _latestGlobalQualityWeight01;
            state.Reserved5 = 0;
            state.Reserved6 = 0;
        }

        private void UpdateDrsState()
        {
            _drsState.CurrentRenderScale = ClampRenderScale(_currentScale);
            _drsState.TargetRenderScale = ClampRenderScale(_targetScale);
            _drsState.UpscalerTypeHash = _upscalerTypeHash;

            if (!TryAcquireDrsStatePointer(out DrsStateDTO* drsState, out int drsStateLength))
                return;

            try
            {
                if (drsStateLength > 0)
                {
                    ref DrsStateDTO state = ref UnsafeUtility.AsRef<DrsStateDTO>(drsState);
                    state.CurrentRenderScale = _drsState.CurrentRenderScale;
                    state.TargetRenderScale = _drsState.TargetRenderScale;
                    state.UpscalerTypeHash = _drsState.UpscalerTypeHash;
                }
            }
            finally
            {
                ReleaseDrsStatePointer();
            }
        }

        private void ApplyMockQualityWeightDropColdSync()
        {
            if (!TryAcquireDrsStatePointer(out DrsStateDTO* drsState, out int drsStateLength))
                return;

            try
            {
                if (drsStateLength <= 0)
                    return;

                ref DrsStateDTO state = ref UnsafeUtility.AsRef<DrsStateDTO>(drsState);
                state.TargetRenderScale = math.lerp(_minScaleLimit, PolicyMaxScale, 0.2f);
                state.UpscalerTypeHash = ResolveUpscalerHash(state.TargetRenderScale);
                _drsState = state;
            }
            finally
            {
                ReleaseDrsStatePointer();
            }
        }

        private void ApplySystemStressEwmaInline(float inputStress01)
        {
            float input = Sanitize01(inputStress01);
            float previous = _latestSystemStressEwma01 > 0f && math.isfinite(_latestSystemStressEwma01)
                ? Sanitize01(_latestSystemStressEwma01)
                : input;
            _latestSystemStress01 = input;
            _latestSystemStressEwma01 = math.lerp(previous, input, EwmaAlpha);
            if (_scaleStateMirrorValid)
            {
                _scaleStateMirror.SystemStress01 = _latestSystemStress01;
                _scaleStateMirror.SystemStressEwma01 = _latestSystemStressEwma01;
            }
        }

        private void InstallSystemDynamicResolutionScaler()
        {
            if (_systemScalerInstalled)
                return;

            DynamicResolutionHandler.SetSystemDynamicResScaler(s_systemScaler, DynamicResScalePolicyType.ReturnsPercentage);
            DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.System);
            _systemScalerInstalled = true;
        }

        private void ReleaseSystemDynamicResolutionScaler()
        {
            if (!_systemScalerInstalled || !ReferenceEquals(s_activeAdapter, this))
                return;

            DynamicResolutionHandler.SetSystemDynamicResScaler(s_nativeScale, DynamicResScalePolicyType.ReturnsPercentage);
            DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.User);
            s_systemScalePercentage = 100f;
            _systemScalerInstalled = false;
        }

        private void ClearSystemOverrideRenderScale()
        {
            if (_dynamicResolutionRuntime != null)
            {
                _dynamicResolutionRuntime.ClearSystemOverrideRenderScale();
            }
            else if (_urpAsset != null)
            {
                ApplyDirectRenderScale(_defaultRenderScale, PolicyMaxScale);
            }

            _currentScale = _defaultRenderScale;
            _targetScale = _defaultRenderScale;
            _lastObservedScaleMilli = ScaleToMilli(_currentScale);
            _lastPublishedScale = _currentScale;
            _sharpenIntensity01 = 0f;
            ApplySharpenGlobal();
            _dearLie01 = 0f;
            _visualOverkill01 = 0f;
            _visualFeatureFlags = 0u;
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            _upscalerTypeHash = ResolveUpscalerHash(_currentScale);
            UpdateDrsState();
            ApplyVisualBudgetGlobals();
            s_systemScalePercentage = _currentScale * 100f;
        }

        private void TryRegister()
        {
            TryRegisterLateFrame();
            TryRegisterSlowTick();
        }

        private void RequestDispatcherPhaseRegistrationRepair()
        {
            if (!Application.isPlaying || (_registeredLateFrame && _registeredSlowTick))
            {
                _dispatcherRegistrationRepairRunning = false;
                _dispatcherRegistrationRepairFramesRemaining = 0;
                return;
            }

            if (!_dispatcherRegistrationRepairRunning)
            {
                _dispatcherRegistrationRepairRunning = true;
                _dispatcherRegistrationRepairFramesRemaining = DispatcherRegistrationRepairMaxFrames;
            }

            AdvanceDispatcherRegistrationRepair();
        }

        private void AdvanceDispatcherRegistrationRepair()
        {
            if (!_dispatcherRegistrationRepairRunning)
                return;

            if (!Application.isPlaying ||
                !enabled ||
                (_registeredLateFrame && _registeredSlowTick) ||
                _dispatcherRegistrationRepairFramesRemaining <= 0)
            {
                _dispatcherRegistrationRepairRunning = false;
                _dispatcherRegistrationRepairFramesRemaining = 0;
                return;
            }

            _dispatcherRegistrationRepairFramesRemaining--;
            TryRegister();
            if (_registeredLateFrame && _registeredSlowTick)
            {
                _dispatcherRegistrationRepairRunning = false;
                _dispatcherRegistrationRepairFramesRemaining = 0;
            }
        }

        private void TryUnregister()
        {
            TryUnregisterSlowTick();
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrame = false;
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
        }

        private void RegisterResolutionScalerService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterResolutionScalerService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ResolutionScaler, this);
        }

        private void UnregisterResolutionScalerService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterResolutionScalerService(this);
            _serviceRegistered = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RegisterSceneLoadedRepair()
        {
            if (_sceneLoadedRepairRegistered || !Application.isPlaying)
                return;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedRepairCold;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedRepairCold;
            _sceneLoadedRepairRegistered = true;
        }

        private void UnregisterSceneLoadedRepair()
        {
            if (!_sceneLoadedRepairRegistered)
                return;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedRepairCold;
            _sceneLoadedRepairRegistered = false;
        }

        private void OnSceneLoadedRepairCold(
            UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!TryClaimActiveAdapterAfterReloadCold() || !Application.isPlaying)
                return;

            RebindDataVault(GlobalRegistry.DataVault);
            RebindDynamicResolutionRuntime(GlobalRegistry.DynamicResolutionRuntime);
            RefreshRenderSurfaceSnapshotCold();
            ApplyQualitySnapshotPolicyCold();
            RegisterResolutionScalerService();
            InstallSystemDynamicResolutionScaler();
            RegisterCameraShield();
            TryRegisterHotSwap();
            TryRegister();
            CommitRenderScale(0);
            RequestDispatcherPhaseRegistrationRepair();
        }

        private bool TryClaimActiveAdapterAfterReloadCold()
        {
            if (ReferenceEquals(s_activeAdapter, this))
                return true;

            if (s_activeAdapter == null)
            {
                s_activeAdapter = this;
                return true;
            }

            return false;
        }

        private void RegisterCameraShield()
        {
            if (_cameraShieldRegistered || !Application.isPlaying)
                return;

            RefreshCameraShieldCacheCold();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _cameraShieldRegistered = true;
        }

        private void UnregisterCameraShield()
        {
            if (!_cameraShieldRegistered)
                return;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _cameraShieldRegistered = false;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            _ = context;
            if (camera == null)
                return;

            ApplyCameraShieldPolicy(camera, IsWorldCameraCached(camera));
        }

        private bool IsWorldCameraCached(Camera camera)
        {
            if (!HasWorldCameraShape(camera))
                return false;

            ulong entityId = EntityId.ToULong(camera.GetEntityId());
            for (int i = 0; i < _cameraShieldCachedCount; i++)
            {
                if (_cameraShieldEntityIds[i] == entityId)
                    return _cameraShieldWorldCameraFlags[i] == 2;
            }

            bool isWorldCamera = IsWorldCameraCold(camera);
            _cameraShieldColdRefreshRequested = !TryCacheCameraShieldEntryCold(
                camera,
                entityId,
                isWorldCamera ? (byte)2 : (byte)1);
            return isWorldCamera;
        }

        private bool TryCacheCameraShieldEntryCold(Camera camera, ulong entityId, byte worldCameraFlag)
        {
            if (_cameraShieldCachedCount >= CameraShieldCacheCapacity)
                return false;

            _cameraShieldCameras[_cameraShieldCachedCount] = camera;
            _cameraShieldEntityIds[_cameraShieldCachedCount] = entityId;
            _cameraShieldWorldCameraFlags[_cameraShieldCachedCount] = worldCameraFlag;
            _cameraShieldCachedCount++;
            return true;
        }

        private void ApplyCameraShieldCached()
        {
            if (!_cameraShieldRegistered)
                return;

            for (int i = 0; i < _cameraShieldCachedCount; i++)
            {
                Camera camera = _cameraShieldCameras[i];
                if (camera == null)
                    continue;

                ApplyCameraShieldPolicy(camera, _cameraShieldWorldCameraFlags[i] == 2);
            }
        }

        private void ApplyCameraShieldPolicy(Camera camera, bool isWorldCamera)
        {
            bool shouldAllowDynamicResolution = isWorldCamera && _stpActive;
            if (camera.allowDynamicResolution != shouldAllowDynamicResolution)
                camera.allowDynamicResolution = shouldAllowDynamicResolution;
        }

        private void RefreshCameraShieldCacheCold()
        {
            _cameraShieldCachedCount = 0;
            for (int i = 0; i < CameraShieldCacheCapacity; i++)
            {
                _cameraShieldSnapshot[i] = null;
                _cameraShieldCameras[i] = null;
                _cameraShieldEntityIds[i] = 0UL;
                _cameraShieldWorldCameraFlags[i] = 0;
            }

            int cameraCount = Camera.GetAllCameras(_cameraShieldSnapshot);
            int limit = math.min(cameraCount, CameraShieldCacheCapacity);
            for (int i = 0; i < limit; i++)
            {
                Camera camera = _cameraShieldSnapshot[i];
                _cameraShieldSnapshot[i] = null;
                if (camera == null)
                    continue;

                bool isWorldCamera = IsWorldCameraCold(camera);
                _cameraShieldCameras[_cameraShieldCachedCount] = camera;
                _cameraShieldEntityIds[_cameraShieldCachedCount] = EntityId.ToULong(camera.GetEntityId());
                _cameraShieldWorldCameraFlags[_cameraShieldCachedCount] = isWorldCamera ? (byte)2 : (byte)1;
                _cameraShieldCachedCount++;
                ApplyCameraShieldPolicy(camera, isWorldCamera);
            }

            _cameraShieldColdRefreshRequested = cameraCount > CameraShieldCacheCapacity;
        }

        private static bool IsWorldCameraCold(Camera camera)
        {
            if (!HasWorldCameraShape(camera))
                return false;

            if (camera.TryGetComponent(out UniversalAdditionalCameraData urpCameraData))
                return urpCameraData.renderType == CameraRenderType.Base;

            return true;
        }

        private static bool HasWorldCameraShape(Camera camera)
        {
            return camera.cameraType == CameraType.Game &&
                   camera.targetTexture == null &&
                   !IsUiOnlyCamera(camera.cullingMask);
        }

        private static bool IsUiOnlyCamera(int cullingMask)
        {
            int uiLayer = ResolveUiLayerCold();
            if (uiLayer < 0 || uiLayer >= 31)
                return false;

            int uiMask = 1 << uiLayer;
            return (cullingMask & uiMask) != 0 && (cullingMask & ~uiMask) == 0;
        }

        private static int ResolveUiLayerCold()
        {
            int uiLayer = s_uiLayer;
            if (uiLayer != -2)
                return uiLayer;

            uiLayer = LayerMask.NameToLayer("UI");
            s_uiLayer = uiLayer;
            return uiLayer;
        }

        private void RebindDynamicResolutionRuntime(IDynamicResolutionRuntime runtime)
        {
            if (ReferenceEquals(_dynamicResolutionRuntime, runtime))
                return;

            _dynamicResolutionRuntime = runtime;
            if (_dynamicResolutionRuntime != null)
            {
                CommitRuntimeSnapshot(_stpActive ? FlagStpActive : (byte)0);
            }
            else
            {
                ApplyDirectRenderScale(_currentScale, _currentScale);
            }
        }

        private bool RecoverInvalidScaleState()
        {
            if (math.isfinite(_currentScale) &&
                math.isfinite(_targetScale) &&
                math.isfinite(_latestFrameTimeEwmaMs) &&
                math.isfinite(_latestSystemStress01) &&
                math.isfinite(_latestSystemStressEwma01) &&
                math.isfinite(_latestGlobalQualityWeight01))
            {
                return false;
            }

            bool resetByTelemetry = WriteTelemetry(FlagInvalidState);
            if (!resetByTelemetry)
                ResetInvalidScaleStateAndCommit();
            return true;
        }

        private void ResetInvalidScaleStateAndCommit()
        {
            _currentScale = PolicyMaxScale;
            _targetScale = PolicyMaxScale;
            _latestFrameTimeEwmaMs = TargetFrameTimeMs;
            _latestSystemStress01 = 1f;
            _latestSystemStressEwma01 = 1f;
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            _sharpenIntensity01 = 0f;
            _panicAuthority01 = 0f;
            _pressureFrameCount = 0;
            _recoveryFrameCount = RecoveryHysteresisFrames;
            s_systemScalePercentage = 100f;
            _upscalerTypeHash = ResolveUpscalerHash(_currentScale);
            UpdateVisualBudget(_latestGlobalQualityWeight01, _latestSystemStressEwma01, _currentScale);
            QueueRenderScaleCommit(FlagInvalidState);
            UpdateScaleState(FlagInvalidState);
        }

        private void QueueRenderScaleCommit(byte flags)
        {
            _pendingRenderScaleCommitFlags = flags;
            _pendingRenderScaleCommitDirty = true;
            RequestLateFrameRegistrationRepair();
        }

        private void QueueRuntimeSnapshotCommit(byte flags)
        {
            _pendingRuntimeSnapshotFlags = flags;
            _pendingRuntimeSnapshotDirty = true;
            RequestLateFrameRegistrationRepair();
        }

        private void QueueSharpenGlobal()
        {
            _pendingSharpenGlobalDirty = true;
            RequestLateFrameRegistrationRepair();
        }

        private void QueueVisualBudgetGlobals()
        {
            _pendingVisualBudgetGlobalsDirty = true;
            RequestLateFrameRegistrationRepair();
        }

        private void RequestLateFrameRegistrationRepair()
        {
            _lateFrameRegistrationRequested = !_registeredLateFrame;
            RequestDispatcherPhaseRegistrationRepair();
        }

        private void PublishResolutionChangedSignalIfNeeded(byte flags)
        {
            if (math.abs(_currentScale - _lastPublishedScale) <= ResolutionSignalThreshold)
                return;

            float oldScale = _lastPublishedScale;
            _lastPublishedScale = _currentScale;
            ResolutionChangedSignal signal = default;
            signal.Frame = _frameCounter;
            signal.SourceHash = SourceHash;
            signal.OldMipLimit = ScaleToMilli(oldScale);
            signal.NewMipLimit = ScaleToMilli(_currentScale);
            signal.VramUsedMb = 0f;
            signal.Reason = _currentScale < oldScale
                ? ResolutionChangedSignal.ReasonRenderScaleDropped
                : ResolutionChangedSignal.ReasonRenderScaleRaised;
            signal.Flags = (byte)(ResolutionChangedSignal.FlagRenderScale |
                (_stpActive ? ResolutionChangedSignal.FlagStpActive : 0));
            SignalBus<ResolutionChangedSignal>.TryPushTracked(in signal, ref s_x001ThermalDynamicResolutionAdapterSignalPushDropCount);
        }

        private void ApplyDirectRenderScale(float renderScale, float bufferScale)
        {
            renderScale = ClampRenderScale(renderScale);
            if (!math.isfinite(bufferScale) || bufferScale <= 0f)
                bufferScale = PolicyMaxScale;

            bufferScale = ClampRenderScale(bufferScale);
            if (_urpAsset != null && math.abs(_urpAsset.renderScale - renderScale) > ScaleEpsilon)
                _urpAsset.renderScale = renderScale;

            ScalableBufferManager.ResizeBuffers(bufferScale, bufferScale);
        }

        private void ApplySharpenGlobal()
        {
            if (math.abs(_sharpenIntensity01 - _lastCommittedSharpenIntensity01) <= SharpenEpsilon)
                return;

            _lastCommittedSharpenIntensity01 = _sharpenIntensity01;
            Shader.SetGlobalFloat(s_sharpenIntensityId, _sharpenIntensity01);
            Shader.SetGlobalFloat(s_drsTaaSharpenId, _sharpenIntensity01);
        }

        private void ApplyVisualBudgetGlobals()
        {
            float renderScale01 = SanitizePositive(_currentScale, PolicyMaxScale);
            float scaleDeficit01 = math.saturate(PolicyMaxScale - math.min(renderScale01, PolicyMaxScale));
            float safeScale = math.clamp(renderScale01, MinScale, PolicyMaxScale);
            float mipBias = math.log2(math.rcp(safeScale));
            float postCullScale = math.isfinite(_postCullScale) ? math.clamp(_postCullScale, MinScale, PolicyMaxScale) : DefaultPostCullScale;
            float postProcessWeight = math.saturate((safeScale - postCullScale) * math.rcp(math.max(0.001f, PolicyMaxScale - postCullScale)));
            float screenWidth = _screenWidthSnapshot > 0 ? _screenWidthSnapshot : 1f;
            float screenHeight = _screenHeightSnapshot > 0 ? _screenHeightSnapshot : 1f;
            Vector4 screenPixels = default;
            screenPixels.x = screenWidth;
            screenPixels.y = screenHeight;
            screenPixels.z = screenWidth * safeScale;
            screenPixels.w = screenHeight * safeScale;

            if (math.abs(renderScale01 - _lastCommittedRenderScale01) > VisualBudgetEpsilon)
            {
                _lastCommittedRenderScale01 = renderScale01;
                Shader.SetGlobalFloat(s_stpRenderScaleId, renderScale01);
            }

            if (math.abs(scaleDeficit01 - _lastCommittedScaleDeficit01) > VisualBudgetEpsilon)
            {
                _lastCommittedScaleDeficit01 = scaleDeficit01;
                Shader.SetGlobalFloat(s_stpScaleDeficitId, scaleDeficit01);
            }

            if (math.abs(_dearLie01 - _lastCommittedDearLie01) > VisualBudgetEpsilon)
            {
                _lastCommittedDearLie01 = _dearLie01;
                Shader.SetGlobalFloat(s_dearLieId, _dearLie01);
            }

            if (math.abs(_visualOverkill01 - _lastCommittedVisualOverkill01) > VisualBudgetEpsilon)
            {
                _lastCommittedVisualOverkill01 = _visualOverkill01;
                Shader.SetGlobalFloat(s_visualOverkillId, _visualOverkill01);
                Shader.SetGlobalFloat(s_visorFluidOverkillId, _visualOverkill01);
            }

            if (_visualFeatureFlags != _lastCommittedVisualFeatureFlags)
            {
                _lastCommittedVisualFeatureFlags = _visualFeatureFlags;
                Shader.SetGlobalInt(s_visualFeatureFlagsId, unchecked((int)_visualFeatureFlags));
            }

            if (!_visualFeatureWeightsCommitted ||
                (_visualFeatureWeights0 - _lastCommittedVisualFeatureWeights0).sqrMagnitude > VisualBudgetEpsilon * VisualBudgetEpsilon)
            {
                _lastCommittedVisualFeatureWeights0 = _visualFeatureWeights0;
                Shader.SetGlobalVector(s_visualFeatureWeights0Id, _visualFeatureWeights0);
            }

            if (!_visualFeatureWeightsCommitted ||
                (_visualFeatureWeights1 - _lastCommittedVisualFeatureWeights1).sqrMagnitude > VisualBudgetEpsilon * VisualBudgetEpsilon)
            {
                _lastCommittedVisualFeatureWeights1 = _visualFeatureWeights1;
                Shader.SetGlobalVector(s_visualFeatureWeights1Id, _visualFeatureWeights1);
            }
            _visualFeatureWeightsCommitted = true;

            if (math.abs(mipBias - _lastCommittedMipBias) > VisualBudgetEpsilon)
            {
                _lastCommittedMipBias = mipBias;
                Shader.SetGlobalFloat(s_drsMipBiasId, mipBias);
            }

            if (math.abs(postProcessWeight - _lastCommittedPostProcessWeight) > VisualBudgetEpsilon)
            {
                _lastCommittedPostProcessWeight = postProcessWeight;
                Shader.SetGlobalFloat(s_drsPostProcessWeightId, postProcessWeight);
            }

            if ((screenPixels - _lastCommittedScreenPixels).sqrMagnitude > 0.25f)
            {
                _lastCommittedScreenPixels = screenPixels;
                Shader.SetGlobalVector(s_drsScreenPixelsId, screenPixels);
            }

            if (_upscalerTypeHash != _lastCommittedUpscalerHash)
            {
                _lastCommittedUpscalerHash = _upscalerTypeHash;
                Shader.SetGlobalInt(s_drsUpscalerTypeHashId, unchecked((int)_upscalerTypeHash));
            }
        }

        private void PublishScaleTelemetryIfChanged()
        {
            int scaleMilli = ScaleToMilli(_currentScale);
            if (scaleMilli == _lastObservedScaleMilli)
                return;

            bool scaleDropped = _lastObservedScaleMilli < 0 || scaleMilli < _lastObservedScaleMilli;
            _lastObservedScaleMilli = scaleMilli;
            if (_currentScale >= PolicyMaxScale - ScaleEpsilon)
                return;

            uint frame = _frameCounter;
            if (!scaleDropped && frame - _lastTelemetryReportFrame < (uint)TelemetryReportCooldownFrames)
                return;

            _lastTelemetryReportFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(DrsWarningHash, ScaleContextHash, _currentScale);
        }

        private bool WriteTelemetry(byte flags)
        {
            bool shouldDumpBlackBox = false;
            bool shouldResetInvalidState = false;
            if (!TryAcquireTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
                return false;

            try
            {
                bool nonFinite =
                    !math.isfinite(_currentScale) ||
                    !math.isfinite(_targetScale) ||
                    !math.isfinite(_latestFrameTimeEwmaMs) ||
                    !math.isfinite(_latestSystemStress01) ||
                    !math.isfinite(_latestSystemStressEwma01) ||
                    !math.isfinite(_sharpenIntensity01);

                int index = _telemetryCursor;
                if ((uint)index >= (uint)TelemetryCapacity || index >= telemetryLength)
                    index = 0;

                _framesBelowTarget = _latestFrameTimeEwmaMs > TargetFrameTimeMs
                    ? math.min(_framesBelowTarget + 1, ushort.MaxValue)
                    : 0;
                float upscalerComputeTimeMs = ResolveEstimatedUpscalerComputeTimeMs(_upscalerTypeHash, _currentScale);
                ref DrsTelemetryEntry entry = ref UnsafeUtility.AsRef<DrsTelemetryEntry>(telemetryRing + index);
                entry.Frame = _frameCounter;
                entry.CurrentScale01 = _currentScale;
                entry.TargetScale01 = _targetScale;
                entry.FrameTimeEwmaMs = _latestFrameTimeEwmaMs;
                entry.SystemStress01 = _latestSystemStress01;
                entry.SystemStressEwma01 = _latestSystemStressEwma01;
                entry.SharpenIntensity01 = _sharpenIntensity01;
                entry.Flags = flags;
                entry.Sequence = _sequence++;
                entry.PressureLevel = _pressureLevel;
                entry.ThermalSeverity = _thermalSeverity;
                entry.StpActive = _stpActive ? (byte)1 : (byte)0;
                entry.AupLockFrames = (byte)math.clamp(_aupShiftLockFrames, 0, byte.MaxValue);
                entry.HysteresisCounters = PackHysteresisCounters();
                entry.FramesBelowTarget = (ushort)_framesBelowTarget;
                entry.UpscalerComputeTimeMsBits = math.asuint(upscalerComputeTimeMs);

                index++;
                _telemetryCursor = index >= TelemetryCapacity ? 0 : index;

                if (nonFinite)
                {
                    shouldDumpBlackBox = true;
                    shouldResetInvalidState = true;
                }
            }
            finally
            {
                ReleaseTelemetryPointer();
            }

            if (shouldDumpBlackBox)
                DumpBlackBoxOnce();
            if (shouldResetInvalidState)
                ResetInvalidScaleStateAndCommit();

            return shouldResetInvalidState;
        }

        private void DumpBlackBoxOnce()
        {
            if (!TryAcquireTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
                return;

            try
            {
                DumpBlackBoxOnceLocked(telemetryRing, telemetryLength);
            }
            finally
            {
                ReleaseTelemetryPointer();
            }
        }

        private void DumpBlackBoxOnceLocked(DrsTelemetryEntry* telemetryRing, int telemetryLength)
        {
            if (_blackBoxDumped || telemetryRing == null || telemetryLength < TelemetryCapacity)
                return;

            NativeArray<byte> payload = default;
            try
            {
                if (string.IsNullOrWhiteSpace(_blackBoxDumpPath))
                    ResolveBlackBoxDumpPathCold();

                int count = math.min(TelemetryCapacity, telemetryLength);
                int byteCount = TelemetryHeaderBytes + count * DrsTelemetryEntryBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ThermalDynamicResolutionAdapter),
                    DumpPayloadLabel);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                Span<byte> payloadBytes = new Span<byte>(payloadPtr, byteCount);
                BinaryPrimitives.WriteUInt32LittleEndian(payloadBytes.Slice(0, 4), TelemetryMagic);
                BinaryPrimitives.WriteInt32LittleEndian(payloadBytes.Slice(4, 4), count);
                BinaryPrimitives.WriteInt32LittleEndian(payloadBytes.Slice(8, 4), _telemetryCursor);
                BinaryPrimitives.WriteUInt32LittleEndian(payloadBytes.Slice(12, 4), _sequence);
                BinaryPrimitives.WriteInt32LittleEndian(payloadBytes.Slice(16, 4), DrsTelemetryEntryBytes);

                int writeOffset = TelemetryHeaderBytes;
                for (int i = 0; i < count; i++)
                {
                    int index = _telemetryCursor + i;
                    if (index >= count)
                        index -= count;

                    Span<byte> telemetryBytes = payloadBytes.Slice(writeOffset, DrsTelemetryEntryBytes);
                    WriteDrsTelemetryEntryLittleEndian(
                        telemetryBytes,
                        telemetryRing[index]);
                    writeOffset += DrsTelemetryEntryBytes;
                }

                uint hash = 2166136261u ^ TelemetryMagic;
                for (int byteIndex = 0; byteIndex < byteCount; byteIndex++)
                    hash = (hash ^ payloadBytes[byteIndex]) * 16777619u;

                if (NativeFaultDumpWriter.TryWriteAll(_blackBoxDumpPath, payload, byteCount))
                {
                    _blackBoxDumpHash = hash == 0u ? 2166136261u : hash;
                    _blackBoxDumped = true;
                }
                else
                {
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)DumpIoFailureHash));
                }
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)DumpIoFailureHash));
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)DumpIoFailureHash));
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)DumpIoFailureHash));
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)DumpIoFailureHash));
            }
            catch (System.Security.SecurityException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)DumpIoFailureHash));
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ThermalDynamicResolutionAdapter),
                    DumpPayloadLabel);
            }
        }

        private void ResolveBlackBoxDumpPathCold()
        {
            string fileName = string.Concat(
                DumpFilePrefix,
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
                DumpFileExtension);
#if UNITY_EDITOR
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                DirectoryInfo assetsDirectory = Directory.GetParent(dataPath);
                if (assetsDirectory != null)
                {
                    _blackBoxDumpPath = Path.Combine(assetsDirectory.FullName, DumpRelativeDirectory, fileName);
                    return;
                }
            }
#endif
            _blackBoxDumpPath = Path.Combine(DumpRelativeDirectory, fileName);
        }

        private static void WriteDrsTelemetryEntryLittleEndian(Span<byte> destination, DrsTelemetryEntry entry)
        {
            destination.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            WriteFloatLittleEndian(destination.Slice(4, 4), entry.CurrentScale01);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.TargetScale01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.FrameTimeEwmaMs);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.SystemStress01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.SystemStressEwma01);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.SharpenIntensity01);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(28, 4), entry.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(32, 4), entry.Sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), entry.UpscalerComputeTimeMsBits);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(40, 2), entry.HysteresisCounters);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(42, 2), entry.FramesBelowTarget);
            destination[44] = entry.PressureLevel;
            destination[45] = entry.ThermalSeverity;
            destination[46] = entry.StpActive;
            destination[47] = entry.AupLockFrames;
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private static bool ValidateAbiLayout()
        {
            return UnsafeUtility.SizeOf<DrsTelemetryEntry>() == DrsTelemetryEntryBytes &&
                   UnsafeUtility.SizeOf<ResolutionScaleState>() == ResolutionScaleStateBytes &&
                   UnsafeUtility.SizeOf<DrsStateDTO>() == DrsStateBytes &&
                   UnsafeUtility.SizeOf<HardwareThermalSnapshot>() == HardwareThermalSnapshotBytes &&
                   UnsafeUtility.SizeOf<DynamicResolutionRuntimeSnapshot>() == DynamicResolutionRuntimeSnapshotBytes;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ClampRenderScale(float value)
        {
            return math.isfinite(value) ? math.clamp(value, MinScale, MaxScale) : PolicyMaxScale;
        }

#if UNITY_EDITOR
        private static uint HashCsvKey(ReadOnlySpan<char> key)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c >= 'a' && c <= 'z')
                    c = (char)(c - 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool TryParseCsvFloat(ReadOnlySpan<char> valueSpan, out float value)
        {
            value = 0f;
            ReadOnlySpan<char> text = valueSpan.Trim();
            if (text.Length == 0)
                return false;

            int cursor = 0;
            double sign = 1d;
            char first = text[cursor];
            if (first == '-' || first == '+')
            {
                sign = first == '-' ? -1d : 1d;
                cursor++;
                if (cursor >= text.Length)
                    return false;
            }

            double mantissa = 0d;
            bool hasDigits = false;
            while (cursor < text.Length && IsCsvDigit(text[cursor]))
            {
                mantissa = (mantissa * 10d) + (text[cursor] - '0');
                hasDigits = true;
                cursor++;
            }

            if (cursor < text.Length && text[cursor] == '.')
            {
                cursor++;
                double place = 0.1d;
                while (cursor < text.Length && IsCsvDigit(text[cursor]))
                {
                    mantissa += (text[cursor] - '0') * place;
                    place *= 0.1d;
                    hasDigits = true;
                    cursor++;
                }
            }

            if (!hasDigits)
                return false;

            int exponent = 0;
            int exponentSign = 1;
            if (cursor < text.Length && (text[cursor] == 'e' || text[cursor] == 'E'))
            {
                cursor++;
                if (cursor < text.Length && (text[cursor] == '-' || text[cursor] == '+'))
                {
                    exponentSign = text[cursor] == '-' ? -1 : 1;
                    cursor++;
                }

                bool hasExponentDigits = false;
                while (cursor < text.Length && IsCsvDigit(text[cursor]))
                {
                    exponent = math.min((exponent * 10) + (text[cursor] - '0'), 38);
                    hasExponentDigits = true;
                    cursor++;
                }

                if (!hasExponentDigits)
                    return false;
            }

            if (cursor != text.Length)
                return false;

            double scaled = mantissa;
            for (int i = 0; i < exponent; i++)
                scaled = exponentSign > 0 ? scaled * 10d : scaled * 0.1d;

            value = (float)(scaled * sign);
            return math.isfinite(value);
        }

        private static bool IsCsvDigit(char value)
        {
            return value >= '0' && value <= '9';
        }
#endif

        private static byte MaxByte(byte a, byte b)
        {
            return a > b ? a : b;
        }

        private static int ScaleToMilli(float scale)
        {
            scale = ClampRenderScale(scale);
            return (int)math.round(scale * 1000f);
        }

        private static float ResolveTierEnvelope(float qualityWeight01, float lowValue, float middleValue, float highValue, float ultraValue)
        {
            float tierWeight = Sanitize01(qualityWeight01);
            float lowToMiddle = math.lerp(lowValue, middleValue, SmoothRange01(tierWeight, 0f, 0.42f));
            float highToUltra = math.lerp(highValue, ultraValue, SmoothRange01(tierWeight, 0.74f, 1f));
            return math.lerp(lowToMiddle, highToUltra, SmoothRange01(tierWeight, 0.42f, 0.74f));
        }

        private static float ResolveStressCollapseStart(float qualityWeight01)
        {
            return ResolveTierEnvelope(qualityWeight01, StressEmergencyThreshold, StressEmergencyThreshold, 0.90f, 0.94f);
        }

        private static float ResolveThermalPressureCollapse01(byte pressureLevel, byte thermalSeverity)
        {
            float pressure01 = math.saturate(((float)pressureLevel - 1f) * 0.5f);
            float thermal01 = math.saturate(((float)thermalSeverity - (float)HardwareThermalSeverity.Warm) * 0.5f);
            return Smooth01(math.max(pressure01, thermal01));
        }

        private static float ResolveFramePressureCollapse01(float frameTimeMs)
        {
            float safeFrameTimeMs = math.max(SanitizePositive(frameTimeMs, TargetFrameTimeMs), TargetFrameTimeMs);
            return SmoothRange01(safeFrameTimeMs, DangerFrameTimeMs, PanicFrameTimeMs);
        }

        private float ResolveGlobalQualityWeight(float stress01)
        {
            float stressWeight = math.saturate(1f - Sanitize01(stress01));
            return math.min(Sanitize01(_latestGlobalQualityWeight01), stressWeight);
        }

        private float ResolveQualitySignalWeight()
        {
            float qualityWeight = ResolvePublishedGlobalQualityWeight();
            if (_mockQualityWeightActive)
                qualityWeight = math.min(qualityWeight, Sanitize01(_mockQualityWeight01));
            if (_mockReconstructionQualityActive)
                qualityWeight = math.min(qualityWeight, Sanitize01(_mockReconstructionQuality01));

            return qualityWeight;
        }

        private float ResolvePublishedGlobalQualityWeight()
        {
            if (_scalabilityQualityWeightSnapshotValid)
                return _scalabilityQualityWeightSnapshot01;

            if (_shaderQualityWeightSnapshotValid)
                return _shaderQualityWeightSnapshot01;

            float cachedQualityWeight = _latestGlobalQualityWeight01;
            if (!math.isfinite(cachedQualityWeight))
                return PolicyMaxScale;

            if (_frameCounter == 0u &&
                cachedQualityWeight <= 0f &&
                !_mockQualityWeightActive &&
                !_mockReconstructionQualityActive)
            {
                return PolicyMaxScale;
            }

            return math.saturate(cachedQualityWeight);
        }

        private void ApplyQualitySnapshotPolicyCold()
        {
            RefreshQualityInputSnapshotsCold();
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            RefreshQualityTierPolicyFromContinuousWeight(_latestGlobalQualityWeight01);
        }

        private void RefreshQualityInputSnapshotsCold()
        {
            _scalabilityQualityWeightSnapshotValid = TryReadScalabilityStateQualityWeight(out _scalabilityQualityWeightSnapshot01);
            _shaderQualityWeightSnapshotValid = TryReadPublishedShaderQualityWeight(out _shaderQualityWeightSnapshot01);
        }

        private static bool TryReadPublishedShaderQualityWeight(out float qualityWeight)
        {
            qualityWeight = PolicyMaxScale;
            float h8QualityWeight = Shader.GetGlobalFloat(s_h8GlobalQualityWeightId);
            float legacyQualityWeight = Shader.GetGlobalFloat(s_globalQualityWeightId);
            bool hasH8QualityWeight = math.isfinite(h8QualityWeight) && h8QualityWeight > 0f;
            bool hasLegacyQualityWeight = math.isfinite(legacyQualityWeight) && legacyQualityWeight > 0f;
            if (!hasH8QualityWeight && !hasLegacyQualityWeight)
                return false;

            float value = hasH8QualityWeight && hasLegacyQualityWeight
                ? math.min(h8QualityWeight, legacyQualityWeight)
                : hasH8QualityWeight
                    ? h8QualityWeight
                    : legacyQualityWeight;
            qualityWeight = math.saturate(value);
            return true;
        }

        private bool TryReadScalabilityStateQualityWeight(out float qualityWeight)
        {
            qualityWeight = PolicyMaxScale;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryAcquireDrsGuard(vault, ScalabilityStateMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenExistingVaultBuffer(
                        vault,
                        ref _scalabilityStateHandle,
                        BufferID.ShinobuScalabilityState,
                        1,
                        out NativeArray<ScalabilityStateDTO> buffer))
                {
                    return false;
                }

                float value = buffer[0].GlobalQualityWeight;
                if (!math.isfinite(value))
                    return false;

                if (_frameCounter == 0u && value <= 0f && !_mockQualityWeightActive)
                    return false;

                qualityWeight = math.saturate(value);
                return true;
            }
            finally
            {
                ReleaseDrsGuard(vault, ScalabilityStateMutationGuardMask);
            }
        }

        private static bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            bool allowAllocation,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null)
            {
                buffer = default;
                return false;
            }

            if (!allowAllocation || vault.IsAllocationLocked)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GraphicsScalability,
                options);
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = default;
            buffer = default;
            return false;
        }

        private static bool TryOpenExistingVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null ||
                !vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsMatchingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u;
        }

        private static ulong DrsMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 63);
        }

        private static bool TryAcquireDrsGuard(IDataVault vault, ulong guardMask)
        {
            return vault != null && guardMask != 0UL && vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseDrsGuard(IDataVault vault, ulong guardMask)
        {
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private float ResolveMinScaleLimit(float qualityWeight01)
        {
            float low = math.clamp(_scaleLimits.LowMinScale > 0f ? _scaleLimits.LowMinScale : DefaultLowMinScale, MinScale, PolicyMaxScale);
            float middle = math.clamp(_scaleLimits.MiddleMinScale > 0f ? _scaleLimits.MiddleMinScale : DefaultMidMinScale, MinScale, PolicyMaxScale);
            float high = math.clamp(_scaleLimits.HighMinScale > 0f ? _scaleLimits.HighMinScale : DefaultHighMinScale, MinScale, PolicyMaxScale);
            float ultra = math.clamp(_scaleLimits.UltraMinScale > 0f ? _scaleLimits.UltraMinScale : DefaultUltraMinScale, MinScale, PolicyMaxScale);
            return math.clamp(ResolveTierEnvelope(qualityWeight01, low, middle, high, ultra), MinScale, PolicyMaxScale);
        }

        private void ApplyMinScaleLimitPreference(float value)
        {
            float clamped = math.clamp(value, MinScale, PolicyMaxScale);
            _scaleLimits.LowMinScale = clamped;
            _scaleLimits.MiddleMinScale = clamped;
            _scaleLimits.HighMinScale = clamped;
            _scaleLimits.UltraMinScale = clamped;
        }

        private float ResolvePanicScaleLimit(float qualityWeight01)
        {
            return ResolveMinScaleLimit(qualityWeight01);
        }

        private float ResolveSmoothedRenderScale(float currentScale, float targetScale, float deltaTime)
        {
            currentScale = ClampRenderScale(currentScale);
            targetScale = ClampRenderScale(targetScale);
            float safeDt = math.isfinite(deltaTime) && deltaTime > 0f ? deltaTime : (1f / 120f);
            float smoothing = math.isfinite(_smoothingFactor) ? math.clamp(_smoothingFactor, 0.1f, 32f) : DefaultSmoothingFactor;
            float alpha = math.saturate(1f - MathLodApproximation.ApproxExpNegPade33Wide40(smoothing * safeDt));
            return currentScale + (targetScale - currentScale) * alpha;
        }

        private float ResolveSmoothedTargetScale(float currentTargetScale, float desiredTargetScale, float deltaTime)
        {
            currentTargetScale = ClampRenderScale(currentTargetScale);
            desiredTargetScale = ClampRenderScale(desiredTargetScale);
            float safeDt = math.isfinite(deltaTime) && deltaTime > 0f ? deltaTime : (1f / 120f);
            float baseSmoothing = math.isfinite(_smoothingFactor)
                ? math.clamp(_smoothingFactor, 0.1f, 32f)
                : DefaultSmoothingFactor;
            float targetSmoothing = math.clamp(baseSmoothing * 2f, 0.2f, 64f);
            float alpha = math.saturate(1f - MathLodApproximation.ApproxExpNegPade33Wide40(targetSmoothing * safeDt));
            return currentTargetScale + (desiredTargetScale - currentTargetScale) * alpha;
        }

        private float ResolvePixelStableRenderScale(float renderScale)
        {
            renderScale = ClampRenderScale(renderScale);
            if (renderScale >= PolicyMaxScale - ScaleEpsilon)
                return PolicyMaxScale;

            float screenWidth = _screenWidthSnapshot > 0 ? _screenWidthSnapshot : 1f;
            float screenHeight = _screenHeightSnapshot > 0 ? _screenHeightSnapshot : 1f;
            float dominantAxisPixels = math.max(screenWidth, screenHeight);
            float scaleGrid = PixelStableGridStep * math.rcp(math.max(1f, dominantAxisPixels));
            float snappedScale = math.round(renderScale * math.rcp(scaleGrid)) * scaleGrid;
            return ClampRenderScale(snappedScale);
        }

        private uint ResolveUpscalerHash(float renderScale)
        {
            if (renderScale >= PolicyMaxScale - ScaleEpsilon)
                return UpscalerNativeHash;

            return _bilateralDrsRouteAllowed ? UpscalerBilateralDrsHash : UpscalerBilateralTaaHash;
        }

        private bool ResolveBilateralDrsRouteAllowed()
        {
            return _coldBilateralDrsRouteAllowed;
        }

        private void CacheGraphicsCapabilitySnapshotCold()
        {
            _coldBilateralDrsRouteAllowed = !Application.isMobilePlatform && SystemInfo.supportsComputeShaders;
        }

        private void RefreshRenderSurfaceSnapshotCold()
        {
            _screenWidthSnapshot = math.max(1, Screen.width);
            _screenHeightSnapshot = math.max(1, Screen.height);
        }

        private static float ResolveSurvivalPressureWeight01(float qualityWeight01)
        {
            return 1f - SmoothRange01(
                Sanitize01(qualityWeight01),
                SurvivalPressureFadeStart01,
                SurvivalPressureFadeEnd01);
        }

        private static float ResolveEstimatedUpscalerComputeTimeMs(uint upscalerHash, float renderScale)
        {
            if (upscalerHash == UpscalerNativeHash)
                return 0f;

            float deficit = math.saturate(PolicyMaxScale - math.min(PolicyMaxScale, ClampRenderScale(renderScale)));
            if (upscalerHash == UpscalerBilateralTaaHash)
                return 0.045f + deficit * 0.055f;

            if (upscalerHash == UpscalerBilateralDrsHash)
                return 0.075f + deficit * 0.075f;

            return 0.045f + deficit * 0.055f;
        }

        private void GenerateEmergencyMockLimits()
        {
            _scaleLimits = default;
            _scaleLimits.LowMinScale = DefaultLowMinScale;
            _scaleLimits.MiddleMinScale = DefaultMidMinScale;
            _scaleLimits.HighMinScale = DefaultHighMinScale;
            _scaleLimits.UltraMinScale = DefaultUltraMinScale;
        }

        private static bool ResolveStpIntent(HectonQualityTier bootHardwareTier, HectonQualityTier compatibilityQualityTier)
        {
            HectonQualityTier tier = IsValidQualityTier(bootHardwareTier)
                ? bootHardwareTier
                : compatibilityQualityTier;
            return IsValidQualityTier(tier);
        }

        private byte ResolveHardwareTierByte()
        {
            HectonQualityTier tier = IsValidQualityTier(_bootHardwareTier)
                ? _bootHardwareTier
                : _cachedQualityTier;
            return (byte)tier;
        }

        private static bool IsValidQualityTier(HectonQualityTier tier)
        {
            return tier >= HectonQualityTier.Low && tier <= HectonQualityTier.Ultra;
        }

        private void RefreshQualityTierPolicyFromContinuousWeight(float qualityWeight01)
        {
            _cachedQualityTier = ResolveQualityTierFromWeight(qualityWeight01);
            _hardwareTier = ResolveHardwareTierByte();
            _bilateralDrsRouteAllowed = ResolveBilateralDrsRouteAllowed();
            _minScaleLimit = ResolveMinScaleLimit(qualityWeight01);
        }

        private static HectonQualityTier ResolveBootHardwareTier()
        {
            HectonHardwareProfile profile = GlobalRegistry.HardwareProfile;
            return profile.QualityTier;
        }

        private static HectonQualityTier ResolveQualityTierFromWeight(float qualityWeight01)
        {
            int tierOrdinal = (int)math.round(ResolveCompatibilityQualityTierOrdinal(qualityWeight01));
            tierOrdinal = math.clamp(
                tierOrdinal,
                (int)HectonQualityTier.Low,
                (int)HectonQualityTier.Ultra);
            return (HectonQualityTier)tierOrdinal;
        }

        private static float ResolveCompatibilityQualityTierOrdinal(float qualityWeight01)
        {
            float quality = Smooth01(Sanitize01(qualityWeight01));
            return math.lerp(
                (float)HectonQualityTier.Low,
                (float)HectonQualityTier.Ultra,
                quality);
        }

        /// <summary>
        /// Advances the continuous emergency-collapse envelope. The frame-time term is zero at
        /// <see cref="PanicFrameTimeMs"/> and reaches full authority at
        /// <see cref="PanicSaturationFrameTimeMs"/>, so there is no step at the old boolean threshold, and
        /// the latch cannot fall faster than one full release over <see cref="PanicReleaseSeconds"/>.
        /// </summary>
        private void AdvancePanicAuthority(float deltaTime)
        {
            float instantAuthority01 = DynamicResolutionPanicEnvelope.ResolveInstantAuthority01(
                SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs),
                PanicFrameTimeMs,
                PanicSaturationFrameTimeMs,
                _pressureLevel);

            _panicAuthority01 = DynamicResolutionPanicEnvelope.Advance(
                _panicAuthority01,
                instantAuthority01,
                deltaTime,
                PanicReleaseSeconds);
        }

        private float ResolveHysteresisTarget(float requestedScale, bool pressureActive)
        {
            if (pressureActive)
            {
                _pressureFrameCount = math.min(PressureHysteresisFrames, _pressureFrameCount + 1);
                _recoveryFrameCount = 0;
                return _pressureFrameCount >= PressureHysteresisFrames
                    ? requestedScale
                    : _currentScale;
            }

            _pressureFrameCount = 0;
            _recoveryFrameCount = math.min(RecoveryHysteresisFrames, _recoveryFrameCount + 1);
            return _recoveryFrameCount >= RecoveryHysteresisFrames
                ? requestedScale
                : _currentScale;
        }

        private float ResolveSharpenIntensity(float nextScale)
        {
            if (nextScale >= PolicyMaxScale - ScaleEpsilon)
                return 0f;

            float safeScale = math.max(MinScale, ClampRenderScale(nextScale));
            float multiplier = math.isfinite(_sharpeningMultiplier)
                ? math.clamp(_sharpeningMultiplier, 0f, 2f)
                : DefaultSharpeningMultiplier;
            float scaleDeficit = math.saturate((PolicyMaxScale - safeScale) * math.rcp(math.max(0.0001f, PolicyMaxScale - MinScale)));
            float varianceProxy = Smooth01(scaleDeficit);
            float quality01 = Sanitize01(_latestGlobalQualityWeight01);
            float reconstructionNeed = math.saturate(math.lerp(varianceProxy, math.max(varianceProxy, 1f - quality01), 0.35f));
            float ringingGuard = math.lerp(0.58f, 0.88f, quality01);
            float scaleClamp = math.lerp(0.38f, 0.78f, varianceProxy);
            return math.clamp(reconstructionNeed * multiplier * ringingGuard, 0f, scaleClamp);
        }

        private void UpdateVisualBudget(float qualityWeight01, float stress01, float renderScale)
        {
            stress01 = Sanitize01(stress01);
            float qualityWeight = ResolveGlobalQualityWeight(stress01);
            float headroom01 = math.saturate(1f - stress01);
            float scaleDeficit01 = math.saturate(PolicyMaxScale - math.min(PolicyMaxScale, ClampRenderScale(renderScale)));
            float reconstructionNeed01 = math.saturate(math.max(scaleDeficit01, 1f - qualityWeight));
            _dearLie01 = math.saturate(ResolveDearLieCapacity(qualityWeight01) * reconstructionNeed01);
            _visualOverkill01 = math.saturate(ResolveVisualOverkillCapacity(qualityWeight01) * qualityWeight * headroom01);
            ResolveVisualFeatureWeights(_visualOverkill01, out _visualFeatureWeights0, out _visualFeatureWeights1);
            _visualFeatureFlags = ResolveVisualFeatureRouteMask();
        }

        private static float ResolveDearLieCapacity(float qualityWeight01)
        {
            return ResolveTierEnvelope(qualityWeight01, 1f, 0.72f, 0.32f, 0.18f);
        }

        private static float ResolveVisualOverkillCapacity(float qualityWeight01)
        {
            return ResolveTierEnvelope(qualityWeight01, 0.04f, 0.32f, 0.78f, 1f);
        }

        private static void ResolveVisualFeatureWeights(float visualOverkill01, out Vector4 weights0, out Vector4 weights1)
        {
            visualOverkill01 = math.saturate(visualOverkill01);
            weights0 = default;
            weights1 = default;
            weights0.x = SmoothVisualGate(visualOverkill01, 0.08f);
            weights0.y = SmoothVisualGate(visualOverkill01, 0.18f);
            weights0.z = SmoothVisualGate(visualOverkill01, 0.34f);
            weights0.w = SmoothVisualGate(visualOverkill01, 0.52f);
            weights1.x = SmoothVisualGate(visualOverkill01, 0.68f);
            weights1.y = SmoothVisualGate(visualOverkill01, 0.88f);
        }

        private static uint ResolveVisualFeatureRouteMask()
        {
            return VisualFeatureRouteMask;
        }

        private static float SmoothVisualGate(float value01, float threshold01)
        {
            float feather = math.max(0.0001f, VisualFeatureFeather);
            float t = math.saturate((value01 - threshold01) * math.rcp(feather));
            return t * t * (3f - 2f * t);
        }

        private static float SmoothRange01(float value, float edge0, float edge1)
        {
            float width = math.max(0.0001f, edge1 - edge0);
            return Smooth01((value - edge0) * math.rcp(width));
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static void SetVector4(ref Vector4 value, float x, float y, float z, float w)
        {
            value.x = x;
            value.y = y;
            value.z = z;
            value.w = w;
        }

        private ushort PackHysteresisCounters()
        {
            int pressure = math.clamp(_pressureFrameCount, 0, byte.MaxValue);
            int recovery = math.clamp(_recoveryFrameCount, 0, byte.MaxValue);
            return (ushort)(pressure | (recovery << 8));
        }

        private void CommitQuestXrScale()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!HectonXRRuntimeState.IsXRActive)
                return;

            if (math.abs(_lastXrScale - _currentScale) <= ScaleEpsilon)
                return;

            _lastXrScale = _currentScale;
            XRSettings.eyeTextureResolutionScale = _currentScale;
#endif
        }
    }
}
