using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
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
        IUpdatable,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IScalabilityChangedEventListener,
        IResolutionScalerService
    {
        private const int TelemetryCapacity = 300;
        private const int TelemetryHeaderBytes = 20;
        private const int DrsTelemetryEntryBytes = 48;
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
        private const uint UpscalerFsrTaaHash = 0x46535254u; // FSRT
        private const uint CsvMinScaleLimitHash = 0xF3608E52u;
        private const uint CsvSmoothingFactorHash = 0x6D58F632u;
        private const uint CsvSharpeningMultiplierHash = 0x4ADC1687u;
        private const uint CsvLowMinScaleHash = 0xA114ECD3u;
        private const uint CsvMiddleMinScaleHash = 0x8D1CCECEu;
        private const uint CsvHighMinScaleHash = 0x0F348BAFu;
        private const uint CsvUltraMinScaleHash = 0x0328B0C7u;
        private const string DumpFileName = "Dump_DRS_SURGEON.bin";
        private const float DangerFrameTimeMs = 15.0f;
        private const float TargetFrameTimeMs = 16.66f;
        private const float PanicFrameTimeMs = 33.0f;
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
        private const float ResolutionSignalThreshold = 0.05f;
        private const float NotificationThreshold = 0.4f;
        private const float EwmaAlpha = 0.18f;
        private const float ScaleEpsilon = 0.0001f;
        private const float PixelStableGridStep = 2f;
        private const float SharpenEpsilon = 0.001f;
        private const float VisualBudgetEpsilon = 0.01f;
        private const float VisualFeatureFeather = 0.14f;
        private const float VisualFeatureFlagEpsilon = 0.001f;
        private const float FsrEligibilityEpsilon = 0.001f;
        private const int AupShiftLockFrames = 3;
        private const int PressureHysteresisFrames = 3;
        private const int RecoveryHysteresisFrames = 15;
        private const int TelemetryReportCooldownFrames = 30;
        private const uint VisualFeatureVisorSalt = 1u << 0;
        private const uint VisualFeatureVolumetricSilt = 1u << 1;
        private const uint VisualFeatureProceduralHullDents = 1u << 2;
        private const uint VisualFeaturePom16Tap = 1u << 3;
        private const uint VisualFeatureSubsurfaceScatter = 1u << 4;
        private const uint VisualFeatureRaymarchedFog = 1u << 5;
        private const byte FlagThermalOverride = 1 << 0;
        private const byte FlagFramePressure = 1 << 1;
        private const byte FlagNotification = 1 << 2;
        private const byte FlagInvalidState = 1 << 3;
        private const byte FlagLowTierEmergency = 1 << 4;
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
        private static readonly int s_uiLayer = LayerMask.NameToLayer("UI");
        private static ThermalDynamicResolutionAdapter s_activeAdapter;
        private static float s_systemScalePercentage = 100f;

        private UniversalRenderPipelineAsset _urpAsset;
        private IDynamicResolutionRuntime _dynamicResolutionRuntime;
        private IDataVault _dataVault;
        private VaultBufferHandle<DrsStateDTO> _drsStateHandle;
        private VaultBufferHandle<ResolutionScaleState> _scaleStateHandle;
        private VaultBufferHandle<DrsTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<ScalabilityStateDTO> _scalabilityStateHandle;
        private VaultBufferHandle<MockReconstructionInputSignal> _mockReconstructionInputHandle;
        private JobHandle _stressEwmaHandle;
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
        private float _latestGlobalQualityWeight01 = PolicyMaxScale;
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
        private HectonQualityTier _bootHardwareTier = HectonQualityTier.Unknown;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private int _lastObservedScaleMilli = -1;
        private uint _lastTelemetryReportFrame;
        private uint _frameCounter;
        private int _framesBelowTarget;
        private int _pressureFrameCount;
        private int _recoveryFrameCount = RecoveryHysteresisFrames;
        private int _aupShiftLockFrames;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _scalabilityListenerRegistered;
        private bool _systemScalerInstalled;
        private bool _blackBoxDumped;
        private bool _stressEwmaScheduled;
        private bool _stressEwmaBufferLocked;
        private bool _stpActive = true;
        private bool _fsrUpscalerAllowed;
        private bool _cameraShieldRegistered;
        private bool _mockQualityWeightActive;
        private bool _mockReconstructionScaleActive;
        private bool _mockReconstructionQualityActive;
        private bool _visualFeatureWeightsCommitted;
        private float _mockQualityWeight01 = 1f;
        private float _mockReconstructionScale01 = PolicyMaxScale;
        private float _mockReconstructionQuality01 = PolicyMaxScale;
        private string _blackBoxDumpPath;
        private DrsScaleLimitsDTO _scaleLimits;
        private ResolutionScaleState _scaleStateMirror;
        private DrsStateDTO _drsState;
        private bool _scaleStateMirrorValid;

#if UNITY_ANDROID && !UNITY_EDITOR
        // COLD ALLOC: List<XRDisplaySubsystem>[4] - Quest display bridge scratch; reused only on scale changes.
        private readonly List<XRDisplaySubsystem> _xrDisplays = new List<XRDisplaySubsystem>(4);
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

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct DrsTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public float CurrentScale01;
            [FieldOffset(8)]
            public float TargetScale01;
            [FieldOffset(12)]
            public float FrameTimeEwmaMs;
            [FieldOffset(16)]
            public float SystemStress01;
            [FieldOffset(20)]
            public float SystemStressEwma01;
            [FieldOffset(24)]
            public float SharpenIntensity01;
            [FieldOffset(28)]
            public uint Flags;
            [FieldOffset(32)]
            public uint Sequence;
            [FieldOffset(36)]
            public byte PressureLevel;
            [FieldOffset(37)]
            public byte ThermalSeverity;
            [FieldOffset(38)]
            public byte StpActive;
            [FieldOffset(39)]
            public byte AupLockFrames;
            [FieldOffset(40)]
            public ushort HysteresisCounters;
            [FieldOffset(42)]
            public ushort FramesBelowTarget;
            [FieldOffset(44)]
            public uint UpscalerComputeTimeMsBits;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct SystemStressEwmaJob : IJob
        {
            [NoAlias]
            [NativeDisableUnsafePtrRestriction]
            public ResolutionScaleState* State;
            public int StateLength;
            public float InputStress01;
            public float Alpha;

            public void Execute()
            {
                if (State != null && StateLength > 0)
                {
                    float input = math.isfinite(InputStress01) ? math.saturate(InputStress01) : 1f;
                    float alpha = math.isfinite(Alpha) ? math.saturate(Alpha) : EwmaAlpha;
                    ref ResolutionScaleState state = ref UnsafeUtility.AsRef<ResolutionScaleState>(State);
                    float previous = math.isfinite(state.SystemStressEwma01)
                        ? math.saturate(state.SystemStressEwma01)
                        : input;
                    state.SystemStress01 = input;
                    state.SystemStressEwma01 = math.lerp(previous, input, alpha);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct MockQualityWeightDropJob : IJob
        {
            [NoAlias]
            [NativeDisableUnsafePtrRestriction]
            public DrsStateDTO* State;
            public float MinScaleLimit;

            public void Execute()
            {
                if (State == null)
                    return;

                ref DrsStateDTO state = ref UnsafeUtility.AsRef<DrsStateDTO>(State);
                state.TargetRenderScale = math.lerp(MinScaleLimit, PolicyMaxScale, 0.2f);
                state.UpscalerTypeHash = UpscalerBilateralTaaHash;
            }
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
            _bootHardwareTier = ResolveBootHardwareTier();
            _cachedQualityTier = ResolveInitialScalabilityTier(_bootHardwareTier);
            _hardwareTier = (byte)_cachedQualityTier;
            _fsrUpscalerAllowed = ResolveFsrUpscalerAllowed(_cachedQualityTier);
            GenerateEmergencyMockLimits();
            _minScaleLimit = ResolveMinScaleLimit(_cachedQualityTier);
            _upscalerTypeHash = ResolveUpscalerHash(_cachedQualityTier, _currentScale);
            _drsState = default;
            _drsState.CurrentRenderScale = _currentScale;
            _drsState.TargetRenderScale = _targetScale;
            _drsState.UpscalerTypeHash = _upscalerTypeHash;
            _drsState._pad0 = 0u;
            RebindDataVault(GlobalRegistry.DataVault);
            TryEnsureDrsStateHandle();
            TryEnsureTelemetryHandle();
            TryEnsureScaleStateHandle();
            UpdateVisualBudget((HectonQualityTier)_hardwareTier, _latestSystemStressEwma01, _currentScale);
            UpdateDrsState();
            UpdateScaleState(0);
            ApplySharpenGlobal();
            ApplyVisualBudgetGlobals();
            InstallSystemDynamicResolutionScaler();
            RebindDynamicResolutionRuntime(GlobalRegistry.DynamicResolutionRuntime);
        }

        private void OnEnable()
        {
            if (!ReferenceEquals(s_activeAdapter, this))
                return;

            if (Application.isPlaying)
            {
                RebindDataVault(GlobalRegistry.DataVault);
                RebindDynamicResolutionRuntime(GlobalRegistry.DynamicResolutionRuntime);
                RegisterResolutionScalerService();
                InstallSystemDynamicResolutionScaler();
                RegisterCameraShield();
                CommitRenderScale(0);
            }

            TryRegister();
            TryRegisterHotSwap();
            TryRegisterScalabilityListener();
        }

        private void Start()
        {
            if (!ReferenceEquals(s_activeAdapter, this))
                return;

            RegisterResolutionScalerService();
            TryRegister();
            TryRegisterHotSwap();
            TryRegisterScalabilityListener();
        }

        private void OnDisable()
        {
            bool ownsAdapter = ReferenceEquals(s_activeAdapter, this);
            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterScalabilityListener();
            UnregisterResolutionScalerService();
            if (!ownsAdapter)
                return;

            CompletePendingStressJobForTeardown();
            ClearSystemOverrideRenderScale();
            ReleaseSystemDynamicResolutionScaler();
            UnregisterCameraShield();
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
            TryUnregisterHotSwap();
            TryUnregisterScalabilityListener();
            UnregisterResolutionScalerService();
            CompletePendingStressJobForTeardown();
            if (ownsAdapter)
            {
                ClearSystemOverrideRenderScale();
                ReleaseSystemDynamicResolutionScaler();
                UnregisterCameraShield();
            }

            _scaleStateHandle = default;
            _telemetryHandle = default;
            _drsStateHandle = default;
            _scalabilityStateHandle = default;
            _dataVault = null;
            _blackBoxDumpPath = null;
        }

        public void Tick(float deltaTime)
        {
            TryFinalizePendingStressJobNoWait();
            if (!ReferenceEquals(s_activeAdapter, this))
                return;

            _frameCounter = unchecked(_frameCounter + 1u);
            float tickFrameMs = SanitizePositive(deltaTime * 1000f, TargetFrameTimeMs);
            _latestFrameTimeEwmaMs = math.lerp(
                SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs),
                tickFrameMs,
                EwmaAlpha);
            ConsumeSignals();
            ConsumeMockReconstructionInputFromVault();
            _latestFrameTimeEwmaMs = SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs);
            _latestSystemHealth01 = Sanitize01(_latestSystemHealth01);
            _latestGpuUtil01 = Sanitize01(_latestGpuUtil01);
            _latestGlobalQualityWeight01 = ResolveQualitySignalWeight();
            _hardwareTier = ResolveHardwareTierByte();
            _stpActive = ResolveStpIntent((HectonQualityTier)_hardwareTier);
            _latestSystemStress01 = ResolveSystemStressInput01();
            if (_latestSystemStressEwma01 <= 0f)
                _latestSystemStressEwma01 = _latestSystemStress01;

            if (RecoverInvalidScaleState())
            {
                ScheduleStressEwmaJob(_latestSystemStress01);
                return;
            }

            byte flags = _stpActive ? FlagStpActive : (byte)0;
            HectonQualityTier tier = (HectonQualityTier)_hardwareTier;
            float stress01 = Sanitize01(_latestSystemStressEwma01);
            _minScaleLimit = ResolveMinScaleLimit(tier);
            float requestedScale = ResolvePolicyScale(tier, stress01, ref flags);
            bool pressureActive = (flags & (FlagFramePressure | FlagThermalOverride | FlagLowTierEmergency)) != 0;

            if (_aupShiftLockFrames > 0)
            {
                flags |= FlagAupLocked;
                _targetScale = _currentScale;
                UpdateVisualBudget(tier, stress01, _currentScale);
                CommitRuntimeSnapshot(flags);
                ApplyVisualBudgetGlobals();
                _aupShiftLockFrames--;
                UpdateDrsState();
                UpdateScaleState(flags);
                WriteTelemetry(flags);
                ScheduleStressEwmaJob(_latestSystemStress01);
                return;
            }

            float desiredTargetScale = ResolveHysteresisTarget(requestedScale, pressureActive);
            bool panicDrop = _latestFrameTimeEwmaMs >= PanicFrameTimeMs || _pressureLevel >= 3;
            if (panicDrop)
            {
                flags |= FlagFramePressure;
                desiredTargetScale = ResolvePanicScaleLimit(tier);
            }

            float targetScale = panicDrop
                ? desiredTargetScale
                : ResolveSmoothedTargetScale(_targetScale, desiredTargetScale, deltaTime);
            float nextScale = panicDrop
                ? targetScale
                : ResolveSmoothedRenderScale(_currentScale, targetScale, deltaTime);
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
            _upscalerTypeHash = ResolveUpscalerHash(tier, nextScale);
            UpdateVisualBudget(tier, stress01, nextScale);
            bool notifyScale = nextScale < NotificationThreshold;
            if (notifyScale)
                flags |= FlagNotification;

            if (math.abs(nextScale - _currentScale) > ScaleEpsilon)
            {
                _currentScale = nextScale;
                CommitRenderScale(flags);
            }
            else
            {
                CommitRuntimeSnapshot(flags);
                ApplySharpenGlobal();
                ApplyVisualBudgetGlobals();
            }

            UpdateScaleState(flags);
            UpdateDrsState();
            WriteTelemetry(flags);
            ScheduleStressEwmaJob(_latestSystemStress01);
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
                ApplyMinScaleLimitForTier((HectonQualityTier)_hardwareTier, minScaleLimit);
                _minScaleLimit = ResolveMinScaleLimit((HectonQualityTier)_hardwareTier);
            }

            if (math.isfinite(smoothingFactor))
                _smoothingFactor = math.clamp(smoothingFactor, 0.1f, 32f);
            if (math.isfinite(sharpeningMultiplier))
                _sharpeningMultiplier = math.clamp(sharpeningMultiplier, 0f, 2f);

            UpdateDrsState();
        }

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
                    ApplyMinScaleLimitForTier((HectonQualityTier)_hardwareTier, value);
                    _minScaleLimit = ResolveMinScaleLimit((HectonQualityTier)_hardwareTier);
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
                _minScaleLimit = ResolveMinScaleLimit((HectonQualityTier)_hardwareTier);
                UpdateDrsState();
            }

            return changed;
        }

        public int CopyTelemetryForEditor(
            float[] currentScale,
            float[] targetScale,
            float[] stress,
            int capacity)
        {
            if (currentScale == null || targetScale == null || stress == null || capacity <= 0)
                return 0;

            if (!TryLockTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
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
                if (_dataVault != null)
                    _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleTelemetry, SystemID.GraphicsScalability);
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
            _drsState.UpscalerTypeHash = forcedScale < PolicyMaxScale - ScaleEpsilon
                ? UpscalerBilateralTaaHash
                : UpscalerNativeHash;
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
            signal._pad0 = 0u;
            ConsumeMockQualityWeightSignal(in signal);
            // COLD SYNC JOB: editor/tuner proof path only; never called from Tick.
            RunMockQualityWeightDropJob();
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime)
                RebindDynamicResolutionRuntime(currentService as IDynamicResolutionRuntime);
            else if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RebindDataVault(currentService as IDataVault);
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
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _cachedQualityTier = ResolveCachedQualityTier(payload.CurrentTier);
            _hardwareTier = (byte)_cachedQualityTier;
            _fsrUpscalerAllowed = ResolveFsrUpscalerAllowed(_cachedQualityTier);
            _minScaleLimit = ResolveMinScaleLimit(_cachedQualityTier);
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
            if (!_mockReconstructionInputHandle.IsCreated ||
                _mockReconstructionInputHandle.BufferId != bufferId)
            {
                if (!vault.TryGetBufferHandle<MockReconstructionInputSignal>(bufferId, out _mockReconstructionInputHandle) ||
                    !_mockReconstructionInputHandle.IsCreated)
                {
                    return false;
                }
            }

            if (!vault.TryLockBuffer(bufferId, SystemID.GraphicsScalability))
                return false;

            try
            {
                void* pointer = _mockReconstructionInputHandle.ResolvePointer(vault);
                if (pointer == null || _mockReconstructionInputHandle.Length <= 0)
                    return false;

                signal = UnsafeUtility.AsRef<MockReconstructionInputSignal>(pointer);
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
                vault.TryUnlockBuffer(bufferId, SystemID.GraphicsScalability);
            }
        }

        private float ResolvePolicyScale(HectonQualityTier tier, float stress01, ref byte flags)
        {
            float lowTierWeight01 = ResolveLowTierWeight01(tier);
            float qualityWeight = ResolveGlobalQualityWeight(stress01);
            float minScaleLimit = ResolveMinScaleLimit(tier);
            float requestedScale = math.lerp(minScaleLimit, PolicyMaxScale, qualityWeight);

            float stressCollapse01 = SmoothRange01(stress01, ResolveStressCollapseStart(tier), PolicyMaxScale);
            requestedScale = math.lerp(requestedScale, minScaleLimit, stressCollapse01);
            if (stressCollapse01 * lowTierWeight01 > 0.001f)
            {
                flags |= FlagLowTierEmergency;
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
            _upscalerTypeHash = ResolveUpscalerHash((HectonQualityTier)_hardwareTier, _currentScale);
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

        private bool TryEnsureScaleStateHandle()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.ResolutionScaleState, out _scaleStateHandle) ||
                !_scaleStateHandle.IsCreated)
            {
                _scaleStateHandle = vault.GetBufferHandle<ResolutionScaleState>(
                    BufferID.ResolutionScaleState,
                    1,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.ClearMemory);
            }

            return _scaleStateHandle.IsCreated;
        }

        private bool TryEnsureDrsStateHandle()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.DrsState, out _drsStateHandle) ||
                !_drsStateHandle.IsCreated)
            {
                _drsStateHandle = vault.GetBufferHandle<DrsStateDTO>(
                    BufferID.DrsState,
                    1,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.UninitializedMemory);
            }

            return _drsStateHandle.IsCreated;
        }

        private bool TryLockDrsStatePointer(out DrsStateDTO* drsState, out int drsStateLength)
        {
            drsState = null;
            drsStateLength = 0;
            if (!TryEnsureDrsStateHandle())
                return false;

            if (!_dataVault.TryLockBuffer(BufferID.DrsState, SystemID.GraphicsScalability))
                return false;

            void* pointer = _drsStateHandle.ResolvePointer(_dataVault);
            if (pointer == null || _drsStateHandle.Length < 1)
            {
                _dataVault.TryUnlockBuffer(BufferID.DrsState, SystemID.GraphicsScalability);
                return false;
            }

            drsState = (DrsStateDTO*)pointer;
            drsStateLength = _drsStateHandle.Length;
            return true;
        }

        private void UnlockDrsStatePointer()
        {
            if (_dataVault != null)
                _dataVault.TryUnlockBuffer(BufferID.DrsState, SystemID.GraphicsScalability);
        }

        private bool TryLockScaleStatePointer(out ResolutionScaleState* scaleState, out int scaleStateLength)
        {
            scaleState = null;
            scaleStateLength = 0;
            if (!TryEnsureScaleStateHandle())
                return false;

            if (!_dataVault.TryLockBuffer(BufferID.ResolutionScaleState, SystemID.GraphicsScalability))
                return false;

            void* pointer = _scaleStateHandle.ResolvePointer(_dataVault);
            if (pointer == null || _scaleStateHandle.Length < 1)
            {
                _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleState, SystemID.GraphicsScalability);
                return false;
            }

            scaleState = (ResolutionScaleState*)pointer;
            scaleStateLength = _scaleStateHandle.Length;
            return true;
        }

        private void UnlockScaleStatePointer()
        {
            if (_dataVault != null)
                _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleState, SystemID.GraphicsScalability);
        }

        private bool TryLockTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength)
        {
            telemetryRing = null;
            telemetryLength = 0;
            if (!TryEnsureTelemetryHandle())
                return false;

            if (!_dataVault.TryLockBuffer(BufferID.ResolutionScaleTelemetry, SystemID.GraphicsScalability))
                return false;

            void* pointer = _telemetryHandle.ResolvePointer(_dataVault);
            if (pointer == null || _telemetryHandle.Length < TelemetryCapacity)
            {
                _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleTelemetry, SystemID.GraphicsScalability);
                return false;
            }

            telemetryRing = (DrsTelemetryEntry*)pointer;
            telemetryLength = _telemetryHandle.Length;
            return true;
        }

        private bool TryEnsureTelemetryHandle()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.ResolutionScaleTelemetry, out _telemetryHandle) ||
                !_telemetryHandle.IsCreated)
            {
                _telemetryHandle = vault.GetBufferHandle<DrsTelemetryEntry>(
                    BufferID.ResolutionScaleTelemetry,
                    TelemetryCapacity,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.ClearMemory);
            }

            return _telemetryHandle.IsCreated;
        }

        private void RebindDataVault(IDataVault vault)
        {
            CompletePendingStressJobForTeardown();

            if (ReferenceEquals(_dataVault, vault))
                return;

            _dataVault = vault;
            _drsStateHandle = default;
            _scaleStateHandle = default;
            _telemetryHandle = default;
            _scalabilityStateHandle = default;
            _mockReconstructionInputHandle = default;
        }

        private void UpdateScaleState(byte flags)
        {
            ResolutionScaleState mirror = default;
            PopulateScaleState(ref mirror, flags);
            _scaleStateMirror = mirror;
            _scaleStateMirrorValid = true;

            if (_stressEwmaScheduled)
                return;

            if (!TryLockScaleStatePointer(out ResolutionScaleState* scaleState, out int scaleStateLength))
                return;

            try
            {
                if (scaleStateLength <= 0)
                    return;

                UnsafeUtility.AsRef<ResolutionScaleState>(scaleState) = mirror;
            }
            finally
            {
                UnlockScaleStatePointer();
            }
        }

        private void PopulateScaleState(ref ResolutionScaleState state, byte flags)
        {
            byte stateFlags = 0;
            if ((flags & FlagLowTierEmergency) != 0)
                stateFlags |= ResolutionScaleStateFlags.LowTierEmergency;
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
            _drsState._pad0 = 0u;

            if (!TryLockDrsStatePointer(out DrsStateDTO* drsState, out int drsStateLength))
                return;

            try
            {
                if (drsStateLength > 0)
                {
                    ref DrsStateDTO state = ref UnsafeUtility.AsRef<DrsStateDTO>(drsState);
                    state.CurrentRenderScale = _drsState.CurrentRenderScale;
                    state.TargetRenderScale = _drsState.TargetRenderScale;
                    state.UpscalerTypeHash = _drsState.UpscalerTypeHash;
                    state._pad0 = 0u;
                }
            }
            finally
            {
                UnlockDrsStatePointer();
            }
        }

        private void RunMockQualityWeightDropJob()
        {
            if (!TryLockDrsStatePointer(out DrsStateDTO* drsState, out int drsStateLength))
                return;

            try
            {
                if (drsStateLength <= 0)
                    return;

                MockQualityWeightDropJob job = default;
                job.State = drsState;
                job.MinScaleLimit = _minScaleLimit;
                job.Execute();
                _drsState = drsState[0];
            }
            finally
            {
                UnlockDrsStatePointer();
            }
        }

        private void ScheduleStressEwmaJob(float inputStress01)
        {
            if (_stressEwmaScheduled || !TryEnsureScaleStateHandle())
                return;

            if (_dataVault == null || !_dataVault.TryLockBuffer(BufferID.ResolutionScaleState, SystemID.GraphicsScalability))
                return;

            void* pointer = _scaleStateHandle.ResolvePointer(_dataVault);
            if (pointer == null || _scaleStateHandle.Length <= 0)
            {
                _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleState, SystemID.GraphicsScalability);
                return;
            }

            SystemStressEwmaJob job = default;
            job.State = (ResolutionScaleState*)pointer;
            job.StateLength = _scaleStateHandle.Length;
            job.InputStress01 = inputStress01;
            job.Alpha = EwmaAlpha;
            _stressEwmaHandle = job.Schedule();
            _stressEwmaScheduled = true;
            _stressEwmaBufferLocked = true;
        }

        private void TryFinalizePendingStressJobNoWait()
        {
            if (!_stressEwmaScheduled)
            {
                UnlockStressEwmaBufferIfNeeded();
                return;
            }

            if (!_stressEwmaHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _stressEwmaHandle))
                return;

            FinishPendingStressJob();
        }

        private void CompletePendingStressJobForTeardown()
        {
            if (!_stressEwmaScheduled)
            {
                UnlockStressEwmaBufferIfNeeded();
                return;
            }

            if (!DispatcherJobFence.TryComplete(ref _stressEwmaHandle, forceComplete: true))
                return;

            FinishPendingStressJob();
        }

        private void FinishPendingStressJob()
        {
            _stressEwmaScheduled = false;
            bool hasState = false;
            ResolutionScaleState state = default;
            if (_dataVault != null && _scaleStateHandle.IsCreated)
            {
                void* pointer = _scaleStateHandle.ResolvePointer(_dataVault);
                if (pointer != null && _scaleStateHandle.Length > 0)
                {
                    state = ((ResolutionScaleState*)pointer)[0];
                    hasState = true;
                }
            }

            UnlockStressEwmaBufferIfNeeded();

            if (hasState)
            {
                _latestSystemStress01 = Sanitize01(state.SystemStress01);
                _latestSystemStressEwma01 = Sanitize01(state.SystemStressEwma01);
                if (_scaleStateMirrorValid)
                {
                    _scaleStateMirror.SystemStress01 = _latestSystemStress01;
                    _scaleStateMirror.SystemStressEwma01 = _latestSystemStressEwma01;
                }
                else
                {
                    _scaleStateMirror = state;
                    _scaleStateMirrorValid = true;
                }
            }
        }

        private void UnlockStressEwmaBufferIfNeeded()
        {
            if (!_stressEwmaBufferLocked)
                return;

            if (_dataVault != null)
                _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleState, SystemID.GraphicsScalability);
            _stressEwmaBufferLocked = false;
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
            _upscalerTypeHash = ResolveUpscalerHash((HectonQualityTier)_hardwareTier, _currentScale);
            UpdateDrsState();
            ApplyVisualBudgetGlobals();
            s_systemScalePercentage = _currentScale * 100f;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registered = false;
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

        private void TryRegisterScalabilityListener()
        {
            if (_scalabilityListenerRegistered || !Application.isPlaying)
                return;

            ScalabilityEvents.Register(this);
            _scalabilityListenerRegistered = true;
        }

        private void TryUnregisterScalabilityListener()
        {
            if (!_scalabilityListenerRegistered)
                return;

            ScalabilityEvents.Unregister(this);
            _scalabilityListenerRegistered = false;
        }

        private void RegisterCameraShield()
        {
            if (_cameraShieldRegistered || !Application.isPlaying)
                return;

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

            bool shouldAllowDynamicResolution = IsWorldCamera(camera) && _stpActive;
            if (camera.allowDynamicResolution != shouldAllowDynamicResolution)
                camera.allowDynamicResolution = shouldAllowDynamicResolution;
        }

        private static bool IsWorldCamera(Camera camera)
        {
            if (camera.cameraType != CameraType.Game)
                return false;

            if (camera.targetTexture != null)
                return false;

            if (IsUiOnlyCamera(camera.cullingMask))
                return false;

            if (camera.TryGetComponent(out UniversalAdditionalCameraData urpCameraData))
                return urpCameraData.renderType == CameraRenderType.Base;

            return true;
        }

        private static bool IsUiOnlyCamera(int cullingMask)
        {
            if (s_uiLayer < 0 || s_uiLayer >= 31)
                return false;

            int uiMask = 1 << s_uiLayer;
            return (cullingMask & uiMask) != 0 && (cullingMask & ~uiMask) == 0;
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

            WriteTelemetry(FlagInvalidState);
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
            _pressureFrameCount = 0;
            _recoveryFrameCount = RecoveryHysteresisFrames;
            s_systemScalePercentage = 100f;
            _upscalerTypeHash = ResolveUpscalerHash((HectonQualityTier)_hardwareTier, _currentScale);
            UpdateVisualBudget((HectonQualityTier)_hardwareTier, _latestSystemStressEwma01, _currentScale);
            CommitRenderScale(FlagInvalidState);
            UpdateScaleState(FlagInvalidState);
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
            SignalBus<ResolutionChangedSignal>.Push(in signal);
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
            float screenWidth = Screen.width > 0 ? Screen.width : 1f;
            float screenHeight = Screen.height > 0 ? Screen.height : 1f;
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

        private void WriteTelemetry(byte flags)
        {
            if (!TryLockTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
                return;

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
                    DumpBlackBoxOnceLocked(telemetryRing, telemetryLength);
                    ResetInvalidScaleStateAndCommit();
                }
            }
            finally
            {
                if (_dataVault != null)
                    _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleTelemetry, SystemID.GraphicsScalability);
            }
        }

        private void DumpBlackBoxOnce()
        {
            if (!TryLockTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
                return;

            try
            {
                DumpBlackBoxOnceLocked(telemetryRing, telemetryLength);
            }
            finally
            {
                if (_dataVault != null)
                    _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleTelemetry, SystemID.GraphicsScalability);
            }
        }

        private void DumpBlackBoxOnceLocked(DrsTelemetryEntry* telemetryRing, int telemetryLength)
        {
            if (_blackBoxDumped || telemetryRing == null || telemetryLength < TelemetryCapacity)
                return;

            try
            {
                string dumpPath = _blackBoxDumpPath;
                if (string.IsNullOrEmpty(dumpPath))
                    return;

                using FileStream stream = File.Open(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                int count = math.min(TelemetryCapacity, telemetryLength);
                Span<byte> header = stackalloc byte[TelemetryHeaderBytes];
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), TelemetryMagic);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), count);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), _telemetryCursor);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12, 4), _sequence);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), DrsTelemetryEntryBytes);
                stream.Write(header);

                Span<byte> telemetryBytes = stackalloc byte[DrsTelemetryEntryBytes];
                for (int i = 0; i < count; i++)
                {
                    int index = _telemetryCursor + i;
                    if (index >= count)
                        index -= count;

                    WriteDrsTelemetryEntryLittleEndian(
                        telemetryBytes,
                        telemetryRing[index]);
                    stream.Write(telemetryBytes);
                }
                _blackBoxDumped = true;
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)TelemetryMagic));
            }
        }

        private void ResolveBlackBoxDumpPathCold()
        {
            _blackBoxDumpPath = null;
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return;

            DirectoryInfo projectRoot = Directory.GetParent(dataPath);
            if (projectRoot == null)
                return;

            string logDirectory = Path.Combine(projectRoot.FullName, "Docs", "AgentLogs");
            Directory.CreateDirectory(logDirectory);
            _blackBoxDumpPath = Path.Combine(logDirectory, DumpFileName);
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
            destination[36] = entry.PressureLevel;
            destination[37] = entry.ThermalSeverity;
            destination[38] = entry.StpActive;
            destination[39] = entry.AupLockFrames;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(40, 2), entry.HysteresisCounters);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(42, 2), entry.FramesBelowTarget);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(44, 4), entry.UpscalerComputeTimeMsBits);
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

        private static byte MaxByte(byte a, byte b)
        {
            return a > b ? a : b;
        }

        private static int ScaleToMilli(float scale)
        {
            scale = ClampRenderScale(scale);
            return (int)math.round(scale * 1000f);
        }

        private static float ResolveHardwareTierWeight01(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Mx350:
                    return 0.15f;
                case HectonQualityTier.Mid:
                    return 0.42f;
                case HectonQualityTier.High:
                    return 0.74f;
                case HectonQualityTier.Ultra:
                    return 1f;
                case HectonQualityTier.Low:
                case HectonQualityTier.Unknown:
                default:
                    return 0f;
            }
        }

        private static float ResolveLowTierWeight01(HectonQualityTier tier)
        {
            return 1f - SmoothRange01(ResolveHardwareTierWeight01(tier), 0.12f, 0.44f);
        }

        private static float ResolveTierEnvelope(HectonQualityTier tier, float lowValue, float middleValue, float highValue, float ultraValue)
        {
            float tierWeight = ResolveHardwareTierWeight01(tier);
            float lowToMiddle = math.lerp(lowValue, middleValue, SmoothRange01(tierWeight, 0f, 0.42f));
            float highToUltra = math.lerp(highValue, ultraValue, SmoothRange01(tierWeight, 0.74f, 1f));
            return math.lerp(lowToMiddle, highToUltra, SmoothRange01(tierWeight, 0.42f, 0.74f));
        }

        private static float ResolveStressCollapseStart(HectonQualityTier tier)
        {
            return ResolveTierEnvelope(tier, StressEmergencyThreshold, StressEmergencyThreshold, 0.90f, 0.94f);
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
            if (TryReadScalabilityStateQualityWeight(out float vaultQualityWeight))
                return vaultQualityWeight;

            if (TryReadPublishedShaderQualityWeight(out float shaderQualityWeight))
                return shaderQualityWeight;

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
            if (vault == null || !TryRefreshScalabilityStateHandle(vault))
                return false;

            if (_scalabilityStateHandle.Length <= 0)
                return false;

            if (!vault.TryLockBuffer(BufferID.ShinobuScalabilityState, SystemID.GraphicsScalability))
                return false;

            try
            {
                void* pointer = _scalabilityStateHandle.ResolvePointer(vault);
                if (pointer == null)
                    return false;

                float value = *(float*)pointer;
                if (!math.isfinite(value))
                    return false;

                if (_frameCounter == 0u && value <= 0f && !_mockQualityWeightActive)
                    return false;

                qualityWeight = math.saturate(value);
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShinobuScalabilityState, SystemID.GraphicsScalability);
            }
        }

        private bool TryRefreshScalabilityStateHandle(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (_scalabilityStateHandle.IsCreated && vault.ResolveBuffer(ref _scalabilityStateHandle))
                return _scalabilityStateHandle.Length > 0;

            if (!vault.TryGetBufferHandle(BufferID.ShinobuScalabilityState, out _scalabilityStateHandle))
            {
                _scalabilityStateHandle = default;
                return false;
            }

            return _scalabilityStateHandle.IsCreated && _scalabilityStateHandle.Length > 0;
        }

        private float ResolveMinScaleLimit(HectonQualityTier tier)
        {
            float low = math.clamp(_scaleLimits.LowMinScale > 0f ? _scaleLimits.LowMinScale : DefaultLowMinScale, MinScale, PolicyMaxScale);
            float middle = math.clamp(_scaleLimits.MiddleMinScale > 0f ? _scaleLimits.MiddleMinScale : DefaultMidMinScale, MinScale, PolicyMaxScale);
            float high = math.clamp(_scaleLimits.HighMinScale > 0f ? _scaleLimits.HighMinScale : DefaultHighMinScale, MinScale, PolicyMaxScale);
            float ultra = math.clamp(_scaleLimits.UltraMinScale > 0f ? _scaleLimits.UltraMinScale : DefaultUltraMinScale, MinScale, PolicyMaxScale);
            return math.clamp(ResolveTierEnvelope(tier, low, middle, high, ultra), MinScale, PolicyMaxScale);
        }

        private void ApplyMinScaleLimitForTier(HectonQualityTier tier, float value)
        {
            float clamped = math.clamp(value, MinScale, PolicyMaxScale);
            if (tier == HectonQualityTier.Unknown ||
                tier == HectonQualityTier.Low ||
                tier == HectonQualityTier.Mx350)
            {
                _scaleLimits.LowMinScale = clamped;
                return;
            }

            if (tier == HectonQualityTier.Mid)
            {
                _scaleLimits.MiddleMinScale = clamped;
                return;
            }

            if (tier == HectonQualityTier.Ultra)
            {
                _scaleLimits.UltraMinScale = clamped;
                return;
            }

            _scaleLimits.HighMinScale = clamped;
        }

        private float ResolvePanicScaleLimit(HectonQualityTier tier)
        {
            return ResolveMinScaleLimit(tier);
        }

        private float ResolveSmoothedRenderScale(float currentScale, float targetScale, float deltaTime)
        {
            currentScale = ClampRenderScale(currentScale);
            targetScale = ClampRenderScale(targetScale);
            float safeDt = math.isfinite(deltaTime) && deltaTime > 0f ? deltaTime : (1f / 120f);
            float smoothing = math.isfinite(_smoothingFactor) ? math.clamp(_smoothingFactor, 0.1f, 32f) : DefaultSmoothingFactor;
            float alpha = math.saturate(1f - math.exp(-smoothing * safeDt));
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
            float alpha = math.saturate(1f - math.exp(-targetSmoothing * safeDt));
            return currentTargetScale + (desiredTargetScale - currentTargetScale) * alpha;
        }

        private static float ResolvePixelStableRenderScale(float renderScale)
        {
            renderScale = ClampRenderScale(renderScale);
            if (renderScale >= PolicyMaxScale - ScaleEpsilon)
                return PolicyMaxScale;

            float screenWidth = Screen.width > 0 ? Screen.width : 1f;
            float screenHeight = Screen.height > 0 ? Screen.height : 1f;
            float dominantAxisPixels = math.max(screenWidth, screenHeight);
            float scaleGrid = PixelStableGridStep * math.rcp(math.max(1f, dominantAxisPixels));
            float snappedScale = math.round(renderScale * math.rcp(scaleGrid)) * scaleGrid;
            return ClampRenderScale(snappedScale);
        }

        private uint ResolveUpscalerHash(HectonQualityTier tier, float renderScale)
        {
            if (renderScale >= PolicyMaxScale - ScaleEpsilon)
                return UpscalerNativeHash;

            if (ResolveFsrUpscalerEligibility01(tier) <= FsrEligibilityEpsilon || !_fsrUpscalerAllowed)
                return UpscalerBilateralTaaHash;

            return UpscalerFsrTaaHash;
        }

        private static bool ResolveFsrUpscalerAllowed(HectonQualityTier tier)
        {
            if (Application.isMobilePlatform || !SystemInfo.supportsComputeShaders)
                return false;

            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            return (graphicsMemoryMb <= 0 || graphicsMemoryMb >= 3000 || tier == HectonQualityTier.Ultra) &&
                   ResolveFsrUpscalerEligibility01(tier) > FsrEligibilityEpsilon;
        }

        private static float ResolveFsrUpscalerEligibility01(HectonQualityTier tier)
        {
            return SmoothRange01(ResolveHardwareTierWeight01(tier), 0.42f, 0.74f);
        }

        private static float ResolveEstimatedUpscalerComputeTimeMs(uint upscalerHash, float renderScale)
        {
            if (upscalerHash == UpscalerNativeHash)
                return 0f;

            float deficit = math.saturate(PolicyMaxScale - math.min(PolicyMaxScale, ClampRenderScale(renderScale)));
            if (upscalerHash == UpscalerBilateralTaaHash)
                return 0.045f + deficit * 0.055f;

            return 0.12f + deficit * 0.10f;
        }

        private void GenerateEmergencyMockLimits()
        {
            _scaleLimits = default;
            _scaleLimits.LowMinScale = DefaultLowMinScale;
            _scaleLimits.MiddleMinScale = DefaultMidMinScale;
            _scaleLimits.HighMinScale = DefaultHighMinScale;
            _scaleLimits.UltraMinScale = DefaultUltraMinScale;
        }

        private static bool ResolveStpIntent(HectonQualityTier tier)
        {
            return (byte)tier <= (byte)HectonQualityTier.Ultra;
        }

        private byte ResolveHardwareTierByte()
        {
            return (byte)_cachedQualityTier;
        }

        private static HectonQualityTier ResolveInitialScalabilityTier(HectonQualityTier bootTier)
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.High &&
                (bootTier == HectonQualityTier.High || bootTier == HectonQualityTier.Ultra))
            {
                return bootTier;
            }

            if (tier != HectonQualityTier.Unknown)
                return tier;

            return bootTier;
        }

        private static HectonQualityTier ResolveBootHardwareTier()
        {
            HectonHardwareProfile profile = GlobalRegistry.HardwareProfile;
            return profile.QualityTier;
        }

        private HectonQualityTier ResolveCachedQualityTier(byte profileTier)
        {
            byte normalizedTier = ScalabilityTierProfiles.Normalize(profileTier);
            if (normalizedTier == ScalabilityTierProfiles.LowMx350)
                return HectonQualityTier.Mx350;

            if (_bootHardwareTier == HectonQualityTier.Ultra ||
                _bootHardwareTier == HectonQualityTier.High ||
                _bootHardwareTier == HectonQualityTier.Mid)
            {
                return _bootHardwareTier;
            }

            return HectonQualityTier.High;
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

        private void UpdateVisualBudget(HectonQualityTier tier, float stress01, float renderScale)
        {
            stress01 = Sanitize01(stress01);
            float qualityWeight = ResolveGlobalQualityWeight(stress01);
            float headroom01 = math.saturate(1f - stress01);
            float scaleDeficit01 = math.saturate(PolicyMaxScale - math.min(PolicyMaxScale, ClampRenderScale(renderScale)));
            float reconstructionNeed01 = math.saturate(math.max(scaleDeficit01, 1f - qualityWeight));
            _dearLie01 = math.saturate(ResolveDearLieCapacity(tier) * reconstructionNeed01);
            _visualOverkill01 = math.saturate(ResolveVisualOverkillCapacity(tier) * qualityWeight * headroom01);
            ResolveVisualFeatureWeights(_visualOverkill01, out _visualFeatureWeights0, out _visualFeatureWeights1);
            _visualFeatureFlags = ResolveVisualFeatureFlags(_visualFeatureWeights0, _visualFeatureWeights1);
        }

        private static float ResolveDearLieCapacity(HectonQualityTier tier)
        {
            return ResolveTierEnvelope(tier, 1f, 0.72f, 0.32f, 0.18f);
        }

        private static float ResolveVisualOverkillCapacity(HectonQualityTier tier)
        {
            return ResolveTierEnvelope(tier, 0.04f, 0.32f, 0.78f, 1f);
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

        private static uint ResolveVisualFeatureFlags(Vector4 weights0, Vector4 weights1)
        {
            uint flags = 0u;
            flags |= (weights0.x > VisualFeatureFlagEpsilon ? 1u : 0u) * VisualFeatureVisorSalt;
            flags |= (weights0.y > VisualFeatureFlagEpsilon ? 1u : 0u) * VisualFeatureVolumetricSilt;
            flags |= (weights0.z > VisualFeatureFlagEpsilon ? 1u : 0u) * VisualFeatureProceduralHullDents;
            flags |= (weights0.w > VisualFeatureFlagEpsilon ? 1u : 0u) * VisualFeaturePom16Tap;
            flags |= (weights1.x > VisualFeatureFlagEpsilon ? 1u : 0u) * VisualFeatureSubsurfaceScatter;
            flags |= (weights1.y > VisualFeatureFlagEpsilon ? 1u : 0u) * VisualFeatureRaymarchedFog;
            return flags;
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
            _xrDisplays.Clear();
            SubsystemManager.GetSubsystems(_xrDisplays);
            bool xrRunning = false;
            for (int i = 0; i < _xrDisplays.Count; i++)
            {
                XRDisplaySubsystem display = _xrDisplays[i];
                if (display != null && display.running)
                {
                    xrRunning = true;
                    break;
                }
            }

            if (!xrRunning)
                return;

            if (math.abs(_lastXrScale - _currentScale) <= ScaleEpsilon)
                return;

            _lastXrScale = _currentScale;
            XRSettings.eyeTextureResolutionScale = _currentScale;
#endif
        }
    }
}
