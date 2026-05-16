using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.UI;
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
        IResolutionScalerService
    {
        private const int TelemetryCapacity = 300;
        private const uint TelemetryMagic = 0x53545041u; // STPA
        private const uint SourceHash = 0x53545051u; // STPQ
        private const uint ScaleContextHash = 0x5343414Cu; // SCAL
        private const uint DrsWarningHash = 0x44525357u; // DRSW
        private const string NotificationMessage = "OPTICS COMPENSATING";
        private const string DumpFileName = "Dump_STP_QUALITY_ADAPTER.bin";
        private const float DangerFrameTimeMs = 15.0f;
        private const float TargetFrameTimeMs = 16.66f;
        private const float MinScale = 0.25f;
        private const float MaxScale = 1.5f;
        private const float PolicyMaxScale = 1.0f;
        private const float LowTierBaseScale = 0.5f;
        private const float LowTierEmergencyScale = 0.35f;
        private const float MidTierBaseScale = 0.82f;
        private const float StressEmergencyThreshold = 0.8f;
        private const float ResolutionSignalThreshold = 0.05f;
        private const float NotificationThreshold = 0.4f;
        private const float NotificationResetThreshold = 0.45f;
        private const float RecoveryStepPerTick = 0.01f;
        private const float EwmaAlpha = 0.18f;
        private const float ScaleEpsilon = 0.0001f;
        private const float SharpenEpsilon = 0.001f;
        private const float VisualBudgetEpsilon = 0.01f;
        private const float HighTierThermalMaxScale = 0.9f;
        private const float UltraTierThermalMaxScale = 1.0f;
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
        private static readonly int s_visorFluidOverkillId = Shader.PropertyToID("_HectonVisorFluidVisualOverkill");
        private static ThermalDynamicResolutionAdapter s_activeAdapter;
        private static float s_systemScalePercentage = 100f;

        private UniversalRenderPipelineAsset _urpAsset;
        private IDynamicResolutionRuntime _dynamicResolutionRuntime;
        private IDataVault _dataVault;
        private VaultBufferHandle<ResolutionScaleState> _scaleStateHandle;
        private VaultBufferHandle<DrsTelemetryEntry> _telemetryHandle;
        private JobHandle _stressEwmaHandle;
        private int _telemetryCursor;
        private uint _sequence;
        private uint _notificationMessageHash;
        private float _defaultRenderScale = PolicyMaxScale;
        private float _currentScale = PolicyMaxScale;
        private float _targetScale = PolicyMaxScale;
        private float _latestFrameTimeEwmaMs = TargetFrameTimeMs;
        private float _latestSystemHealth01 = 1f;
        private float _latestGpuUtil01;
        private float _latestSystemStress01;
        private float _latestSystemStressEwma01;
        private float _sharpenIntensity01;
        private float _dearLie01;
        private float _visualOverkill01;
        private float _lastCommittedSharpenIntensity01 = -1f;
        private float _lastCommittedRenderScale01 = -1f;
        private float _lastCommittedScaleDeficit01 = -1f;
        private float _lastCommittedDearLie01 = -1f;
        private float _lastCommittedVisualOverkill01 = -1f;
        private float _lastPublishedScale = PolicyMaxScale;
        private uint _visualFeatureFlags;
        private uint _lastCommittedVisualFeatureFlags = uint.MaxValue;
        private byte _pressureLevel;
        private byte _thermalSeverity;
        private byte _foveatedPressureTier;
        private byte _hardwareTier;
        private int _lastObservedScaleMilli = -1;
        private int _lastTelemetryReportFrame = -TelemetryReportCooldownFrames;
        private int _pressureFrameCount;
        private int _recoveryFrameCount = RecoveryHysteresisFrames;
        private int _aupShiftLockFrames;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _systemScalerInstalled;
        private bool _notificationArmed = true;
        private bool _blackBoxDumped;
        private bool _stressEwmaScheduled;
        private bool _stressEwmaBufferLocked;
        private bool _stpActive = true;

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

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]
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
            public ushort Reserved;
            [FieldOffset(44)]
            public uint Reserved0;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private unsafe struct SystemStressEwmaJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public ResolutionScaleState* State;
            public int StateLength;
            public float InputStress01;
            public float Alpha;

            public void Execute()
            {
                if (State == null || StateLength <= 0)
                    return;

                float input = math.isfinite(InputStress01) ? math.saturate(InputStress01) : 1f;
                float alpha = math.isfinite(Alpha) ? math.saturate(Alpha) : EwmaAlpha;
                ResolutionScaleState state = State[0];
                float previous = math.isfinite(state.SystemStressEwma01)
                    ? math.saturate(state.SystemStressEwma01)
                    : input;
                state.SystemStress01 = input;
                state.SystemStressEwma01 = math.lerp(previous, input, alpha);
                State[0] = state;
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
            _urpAsset = UniversalRenderPipeline.asset;
            _defaultRenderScale = _urpAsset != null ? ClampRenderScale(_urpAsset.renderScale) : PolicyMaxScale;
            _currentScale = math.min(_defaultRenderScale, PolicyMaxScale);
            _targetScale = _currentScale;
            _lastPublishedScale = _currentScale;
            _lastObservedScaleMilli = ScaleToMilli(_currentScale);
            s_systemScalePercentage = _currentScale * 100f;
            _notificationMessageHash = NotificationEvents.RegisterMessage(NotificationMessage);
            TryResolveTelemetryPointer(out _, out _);
            TryResolveScaleStatePointer(out ResolutionScaleState* scaleState, out int scaleStateLength);
            UpdateVisualBudget((HectonQualityTier)_hardwareTier, _latestSystemStressEwma01);
            UpdateScaleState(0, scaleState, scaleStateLength);
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
                RegisterResolutionScalerService();
                InstallSystemDynamicResolutionScaler();
                CommitRenderScale(0);
            }

            TryRegister();
            TryRegisterHotSwap();
        }

        private void Start()
        {
            if (!ReferenceEquals(s_activeAdapter, this))
                return;

            RegisterResolutionScalerService();
            TryRegister();
            TryRegisterHotSwap();
        }

        private void OnDisable()
        {
            bool ownsAdapter = ReferenceEquals(s_activeAdapter, this);
            TryUnregister();
            TryUnregisterHotSwap();
            UnregisterResolutionScalerService();
            if (!ownsAdapter)
                return;

            CompletePendingStressJob(true);
            ClearSystemOverrideRenderScale();
            ReleaseSystemDynamicResolutionScaler();
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
            UnregisterResolutionScalerService();
            CompletePendingStressJob(true);
            if (ownsAdapter)
            {
                ClearSystemOverrideRenderScale();
                ReleaseSystemDynamicResolutionScaler();
            }

            _scaleStateHandle = default;
            _telemetryHandle = default;
            _dataVault = null;
        }

        public void Tick(float deltaTime)
        {
            CompletePendingStressJob();
            if (!ReferenceEquals(s_activeAdapter, this))
                return;

            TryResolveScaleStatePointer(out ResolutionScaleState* scaleState, out int scaleStateLength);
            ConsumeSignals();
            _latestFrameTimeEwmaMs = SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs);
            _latestSystemHealth01 = Sanitize01(_latestSystemHealth01);
            _latestGpuUtil01 = Sanitize01(_latestGpuUtil01);
            _hardwareTier = ResolveHardwareTierByte();
            _stpActive = ResolveStpIntent((HectonQualityTier)_hardwareTier);
            _latestSystemStress01 = ResolveSystemStressInput01();
            if (_latestSystemStressEwma01 <= 0f)
                _latestSystemStressEwma01 = _latestSystemStress01;

            if (RecoverInvalidScaleState(scaleState, scaleStateLength))
            {
                ScheduleStressEwmaJob(_latestSystemStress01, scaleState, scaleStateLength);
                return;
            }

            byte flags = _stpActive ? FlagStpActive : (byte)0;
            HectonQualityTier tier = (HectonQualityTier)_hardwareTier;
            float stress01 = Sanitize01(_latestSystemStressEwma01);
            float requestedScale = ResolvePolicyScale(tier, stress01, ref flags);
            bool pressureActive = (flags & (FlagFramePressure | FlagThermalOverride | FlagLowTierEmergency)) != 0;
            UpdateVisualBudget(tier, stress01);

            if (_aupShiftLockFrames > 0)
            {
                flags |= FlagAupLocked;
                _targetScale = _currentScale;
                CommitRuntimeSnapshot(flags);
                ApplyVisualBudgetGlobals();
                _aupShiftLockFrames--;
                UpdateScaleState(flags, scaleState, scaleStateLength);
                WriteTelemetry(flags);
                ScheduleStressEwmaJob(_latestSystemStress01, scaleState, scaleStateLength);
                return;
            }

            float targetScale = ResolveHysteresisTarget(requestedScale, pressureActive);
            float nextScale = targetScale < _currentScale
                ? targetScale
                : math.min(targetScale, _currentScale + RecoveryStepPerTick);
            nextScale = ClampRenderScale(nextScale);
            _targetScale = targetScale;
            _sharpenIntensity01 = ResolveSharpenIntensity(nextScale);
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

            if (notifyScale)
            {
                PublishScaleNotificationOnce();
            }
            else if (_currentScale > NotificationResetThreshold)
            {
                _notificationArmed = true;
            }

            UpdateScaleState(flags, scaleState, scaleStateLength);
            WriteTelemetry(flags);
            ScheduleStressEwmaJob(_latestSystemStress01, scaleState, scaleStateLength);
        }

        public bool TryGetScaleState(out ResolutionScaleState state)
        {
            CompletePendingStressJob();
            if (TryResolveScaleStatePointer(out ResolutionScaleState* scaleState, out _))
            {
                state = scaleState[0];
                return true;
            }

            state = default;
            return false;
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

        private void ConsumeSignals()
        {
            float frameTimeEwmaMs = 0f;
            bool frameTimeReceived = false;
            float systemHealth01 = 1f;
            bool systemHealthReceived = false;
            float gpuUtil01 = 0f;
            bool gpuUtilReceived = false;
            byte pressureLevel = 0;
            bool pressureReceived = false;
            byte foveatedPressureTier = 0;
            bool foveatedPressureReceived = false;

            ReadOnlySpan<FrameTimeSignal> frameTimeSignals = SignalBus<FrameTimeSignal>.GetFrameSnapshot();
            for (int i = 0; i < frameTimeSignals.Length; i++)
            {
                FrameTimeSignal signal = frameTimeSignals[i];
                float candidateFrameTimeMs = SanitizePositive(signal.FrameTimeEwmaMs, 0f);
                if (candidateFrameTimeMs > 0f)
                {
                    frameTimeEwmaMs = math.max(frameTimeEwmaMs, candidateFrameTimeMs);
                    frameTimeReceived = true;
                }

                pressureLevel = MaxByte(pressureLevel, signal.PressureLevel);
                pressureReceived = true;
            }

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthSignal signal = healthSignals[i];
                systemHealth01 = math.min(systemHealth01, Sanitize01(signal.SystemHealthIndex01));
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
                _latestSystemHealth01 = systemHealth01;
            if (gpuUtilReceived)
                _latestGpuUtil01 = gpuUtil01;
            if (pressureReceived)
                _pressureLevel = pressureLevel;
            if (foveatedPressureReceived)
                _foveatedPressureTier = foveatedPressureTier;

            ReadOnlySpan<ThermalStateChangedSignal> thermalSignals = SignalBus<ThermalStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < thermalSignals.Length; i++)
                _thermalSeverity = thermalSignals[i].Severity;

            ReadOnlySpan<AupShiftSignal> shiftSignals = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            if (shiftSignals.Length > 0)
                _aupShiftLockFrames = math.max(_aupShiftLockFrames, AupShiftLockFrames);
        }

        private float ResolvePolicyScale(HectonQualityTier tier, float stress01, ref byte flags)
        {
            bool lowTier = IsLowTier(tier);
            float requestedScale = ResolveBaseScale(tier);
            if (lowTier && stress01 > StressEmergencyThreshold)
            {
                requestedScale = LowTierEmergencyScale;
                flags |= FlagLowTierEmergency;
            }
            else if (tier == HectonQualityTier.Mid && stress01 > StressEmergencyThreshold)
            {
                requestedScale = math.min(requestedScale, 0.65f);
            }
            else if ((tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra) && stress01 > 0.95f)
            {
                requestedScale = math.min(requestedScale, 0.85f);
            }

            float frameTimeMs = SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs);
            bool framePressure = frameTimeMs > DangerFrameTimeMs;
            if (framePressure)
            {
                float frameScale = TargetFrameTimeMs * math.rcp(frameTimeMs);
                requestedScale = math.min(requestedScale, frameScale);
                flags |= FlagFramePressure;
            }

            bool thermalOverride = _pressureLevel >= 2 || _thermalSeverity >= (byte)HardwareThermalSeverity.Throttling;
            if (thermalOverride)
            {
                requestedScale = math.min(requestedScale, ResolveThermalMaxScale(tier));
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
                runtime.ApplySystemOverrideRenderScale(
                    _currentScale,
                    _targetScale,
                    _latestFrameTimeEwmaMs,
                    _pressureLevel,
                    flags);
            }
        }

        private bool TryResolveScaleStatePointer(out ResolutionScaleState* scaleState, out int scaleStateLength)
        {
            scaleState = null;
            scaleStateLength = 0;
            IDataVault vault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, vault))
                RebindDataVault(vault);

            if (_dataVault == null)
                return false;

            if (!_dataVault.TryGetBufferHandle(BufferID.ResolutionScaleState, out _scaleStateHandle) ||
                !_scaleStateHandle.IsCreated)
            {
                _scaleStateHandle = _dataVault.GetBufferHandle<ResolutionScaleState>(
                    BufferID.ResolutionScaleState,
                    1,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.ClearMemory);
            }

            void* pointer = _scaleStateHandle.ResolvePointer(_dataVault);
            if (pointer == null || _scaleStateHandle.Length < 1)
                return false;

            scaleState = (ResolutionScaleState*)pointer;
            scaleStateLength = _scaleStateHandle.Length;
            return true;
        }

        private bool TryResolveTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength)
        {
            telemetryRing = null;
            telemetryLength = 0;
            IDataVault vault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, vault))
                RebindDataVault(vault);

            if (_dataVault == null)
                return false;

            if (!_dataVault.TryGetBufferHandle(BufferID.ResolutionScaleTelemetry, out _telemetryHandle) ||
                !_telemetryHandle.IsCreated)
            {
                _telemetryHandle = _dataVault.GetBufferHandle<DrsTelemetryEntry>(
                    BufferID.ResolutionScaleTelemetry,
                    TelemetryCapacity,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.ClearMemory);
            }

            void* pointer = _telemetryHandle.ResolvePointer(_dataVault);
            if (pointer == null || _telemetryHandle.Length < TelemetryCapacity)
                return false;

            telemetryRing = (DrsTelemetryEntry*)pointer;
            telemetryLength = _telemetryHandle.Length;
            return true;
        }

        private void RebindDataVault(IDataVault vault)
        {
            CompletePendingStressJob(true);

            if (ReferenceEquals(_dataVault, vault))
                return;

            _dataVault = vault;
            _scaleStateHandle = default;
            _telemetryHandle = default;
        }

        private void UpdateScaleState(byte flags, ResolutionScaleState* scaleState, int scaleStateLength)
        {
            if (scaleState == null || scaleStateLength <= 0)
                return;

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

            scaleState[0] = new ResolutionScaleState
            {
                CurrentRenderScale01 = _currentScale,
                TargetRenderScale01 = _targetScale,
                SystemStress01 = _latestSystemStress01,
                SystemStressEwma01 = _latestSystemStressEwma01,
                FrameTimeEwmaMs = _latestFrameTimeEwmaMs,
                SharpenIntensity01 = _sharpenIntensity01,
                Frame = unchecked((uint)Time.frameCount),
                Sequence = _sequence,
                HardwareTier = _hardwareTier,
                StpActive = _stpActive ? (byte)1 : (byte)0,
                Flags = stateFlags,
                AupLockFrames = (byte)math.clamp(_aupShiftLockFrames, 0, byte.MaxValue),
                VisualOverkill01 = _visualOverkill01,
                DearLie01 = _dearLie01,
                VisualFeatureFlags = _visualFeatureFlags
            };
        }

        private void ScheduleStressEwmaJob(float inputStress01, ResolutionScaleState* scaleState, int scaleStateLength)
        {
            if (_stressEwmaScheduled || scaleState == null || scaleStateLength <= 0)
                return;

            if (_dataVault == null || !_dataVault.TryLockBuffer(BufferID.ResolutionScaleState))
                return;

            void* pointer = _scaleStateHandle.ResolvePointer(_dataVault);
            if (pointer == null || _scaleStateHandle.Length <= 0)
            {
                _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleState);
                return;
            }

            scaleState = (ResolutionScaleState*)pointer;
            scaleStateLength = _scaleStateHandle.Length;
            SystemStressEwmaJob job = new SystemStressEwmaJob
            {
                State = scaleState,
                StateLength = scaleStateLength,
                InputStress01 = inputStress01,
                Alpha = EwmaAlpha
            };
            _stressEwmaHandle = job.Schedule();
            _stressEwmaScheduled = true;
            _stressEwmaBufferLocked = true;
        }

        private void CompletePendingStressJob(bool force = false)
        {
            if (!_stressEwmaScheduled)
            {
                if (_stressEwmaBufferLocked)
                {
                    if (_dataVault != null)
                        _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleState);
                    _stressEwmaBufferLocked = false;
                }

                return;
            }

            if (!force && !_stressEwmaHandle.IsCompleted)
                return;

            _stressEwmaHandle.Complete();
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

            if (_stressEwmaBufferLocked)
            {
                if (_dataVault != null)
                    _dataVault.TryUnlockBuffer(BufferID.ResolutionScaleState);
                _stressEwmaBufferLocked = false;
            }

            if (hasState)
            {
                _latestSystemStress01 = Sanitize01(state.SystemStress01);
                _latestSystemStressEwma01 = Sanitize01(state.SystemStressEwma01);
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

        private void RebindDynamicResolutionRuntime(IDynamicResolutionRuntime runtime)
        {
            if (ReferenceEquals(_dynamicResolutionRuntime, runtime))
                return;

            _dynamicResolutionRuntime = runtime;
            if (_dynamicResolutionRuntime != null)
            {
                _dynamicResolutionRuntime.ApplySystemOverrideRenderScale(
                    _currentScale,
                    _targetScale,
                    _latestFrameTimeEwmaMs,
                    _pressureLevel,
                    _stpActive ? FlagStpActive : (byte)0);
            }
            else
            {
                ApplyDirectRenderScale(_currentScale, _currentScale);
            }
        }

        private bool RecoverInvalidScaleState(ResolutionScaleState* scaleState, int scaleStateLength)
        {
            if (math.isfinite(_currentScale) &&
                math.isfinite(_targetScale) &&
                math.isfinite(_latestFrameTimeEwmaMs) &&
                math.isfinite(_latestSystemStress01) &&
                math.isfinite(_latestSystemStressEwma01))
            {
                return false;
            }

            WriteTelemetry(FlagInvalidState);
            _currentScale = PolicyMaxScale;
            _targetScale = PolicyMaxScale;
            _latestFrameTimeEwmaMs = TargetFrameTimeMs;
            _latestSystemStress01 = 1f;
            _latestSystemStressEwma01 = 1f;
            _sharpenIntensity01 = 0f;
            _pressureFrameCount = 0;
            _recoveryFrameCount = RecoveryHysteresisFrames;
            s_systemScalePercentage = 100f;
            CommitRenderScale(FlagInvalidState);
            UpdateScaleState(FlagInvalidState, scaleState, scaleStateLength);
            return true;
        }

        private void PublishScaleNotificationOnce()
        {
            if (!_notificationArmed || _notificationMessageHash == 0u)
                return;

            _notificationArmed = false;
            HUDNotificationSignal signal = new HUDNotificationSignal
            {
                MessageHash = _notificationMessageHash,
                ContextHash = ScaleContextHash,
                SourceId = SourceHash,
                Frame = unchecked((uint)Time.frameCount),
                Severity = (byte)NotificationEventSeverity.Info,
                Flags = _foveatedPressureTier
            };
            SignalBus<HUDNotificationSignal>.Push(in signal);
        }

        private void PublishResolutionChangedSignalIfNeeded(byte flags)
        {
            if (math.abs(_currentScale - _lastPublishedScale) <= ResolutionSignalThreshold)
                return;

            float oldScale = _lastPublishedScale;
            _lastPublishedScale = _currentScale;
            ResolutionChangedSignal signal = new ResolutionChangedSignal
            {
                Frame = unchecked((uint)Time.frameCount),
                SourceHash = SourceHash,
                OldMipLimit = ScaleToMilli(oldScale),
                NewMipLimit = ScaleToMilli(_currentScale),
                VramUsedMb = 0f,
                Reason = _currentScale < oldScale
                    ? ResolutionChangedSignal.ReasonRenderScaleDropped
                    : ResolutionChangedSignal.ReasonRenderScaleRaised,
                Flags = (byte)(ResolutionChangedSignal.FlagRenderScale |
                    (_stpActive ? ResolutionChangedSignal.FlagStpActive : 0))
            };
            SignalBus<ResolutionChangedSignal>.Push(in signal);
        }

        private void ApplyDirectRenderScale(float renderScale, float bufferScale)
        {
            if (!math.isfinite(bufferScale) || bufferScale <= 0f)
                bufferScale = PolicyMaxScale;

            bufferScale = ClampRenderScale(bufferScale);
            ScalableBufferManager.ResizeBuffers(bufferScale, bufferScale);
        }

        private void ApplySharpenGlobal()
        {
            if (math.abs(_sharpenIntensity01 - _lastCommittedSharpenIntensity01) <= SharpenEpsilon)
                return;

            _lastCommittedSharpenIntensity01 = _sharpenIntensity01;
            Shader.SetGlobalFloat(s_sharpenIntensityId, _sharpenIntensity01);
        }

        private void ApplyVisualBudgetGlobals()
        {
            float renderScale01 = SanitizePositive(_currentScale, PolicyMaxScale);
            float scaleDeficit01 = math.saturate(PolicyMaxScale - math.min(renderScale01, PolicyMaxScale));

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

            int frame = Time.frameCount;
            if (!scaleDropped && frame - _lastTelemetryReportFrame < TelemetryReportCooldownFrames)
                return;

            _lastTelemetryReportFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(DrsWarningHash, ScaleContextHash, _currentScale);
        }

        private void WriteTelemetry(byte flags)
        {
            if (!TryResolveTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
                return;

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

            telemetryRing[index] = new DrsTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                CurrentScale01 = _currentScale,
                TargetScale01 = _targetScale,
                FrameTimeEwmaMs = _latestFrameTimeEwmaMs,
                SystemStress01 = _latestSystemStress01,
                SystemStressEwma01 = _latestSystemStressEwma01,
                SharpenIntensity01 = _sharpenIntensity01,
                Flags = flags,
                Sequence = _sequence++,
                PressureLevel = _pressureLevel,
                ThermalSeverity = _thermalSeverity,
                StpActive = _stpActive ? (byte)1 : (byte)0,
                AupLockFrames = (byte)math.clamp(_aupShiftLockFrames, 0, byte.MaxValue),
                HysteresisCounters = PackHysteresisCounters(),
                Reserved = (ushort)math.clamp(ScaleToMilli(_visualOverkill01), 0, ushort.MaxValue),
                Reserved0 = _visualFeatureFlags
            };

            index++;
            _telemetryCursor = index >= TelemetryCapacity ? 0 : index;

            if (nonFinite)
            {
                DumpBlackBoxOnce();
                _currentScale = PolicyMaxScale;
                _targetScale = PolicyMaxScale;
                _latestFrameTimeEwmaMs = TargetFrameTimeMs;
                _latestSystemStress01 = 1f;
                _latestSystemStressEwma01 = 1f;
                _sharpenIntensity01 = 0f;
                _pressureFrameCount = 0;
                _recoveryFrameCount = RecoveryHysteresisFrames;
                s_systemScalePercentage = 100f;
            }
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !TryResolveTelemetryPointer(out DrsTelemetryEntry* telemetryRing, out int telemetryLength))
                return;

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                using FileStream stream = File.Open(Path.Combine(logDirectory, DumpFileName), FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(TelemetryMagic);
                writer.Write(TelemetryCapacity);
                writer.Write(_telemetryCursor);
                writer.Write(_sequence);
                int count = math.min(TelemetryCapacity, telemetryLength);
                for (int i = 0; i < count; i++)
                {
                    DrsTelemetryEntry entry = telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.CurrentScale01);
                    writer.Write(entry.TargetScale01);
                    writer.Write(entry.FrameTimeEwmaMs);
                    writer.Write(entry.SystemStress01);
                    writer.Write(entry.SystemStressEwma01);
                    writer.Write(entry.SharpenIntensity01);
                    writer.Write(entry.Flags);
                    writer.Write(entry.Sequence);
                    writer.Write(entry.PressureLevel);
                    writer.Write(entry.ThermalSeverity);
                    writer.Write(entry.StpActive);
                    writer.Write(entry.AupLockFrames);
                    writer.Write(entry.HysteresisCounters);
                    writer.Write(entry.Reserved);
                    writer.Write(entry.Reserved0);
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)TelemetryMagic));
            }
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

        private static byte MaxByte(byte a, byte b)
        {
            return a > b ? a : b;
        }

        private static int ScaleToMilli(float scale)
        {
            return (int)math.round(scale * 1000f);
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        private static float ResolveBaseScale(HectonQualityTier tier)
        {
            if (IsLowTier(tier))
                return LowTierBaseScale;
            return tier == HectonQualityTier.Mid ? MidTierBaseScale : PolicyMaxScale;
        }

        private static float ResolveThermalMaxScale(HectonQualityTier tier)
        {
            if (IsLowTier(tier))
                return LowTierEmergencyScale;
            if (tier == HectonQualityTier.Mid)
                return 0.65f;
            return tier == HectonQualityTier.Ultra ? UltraTierThermalMaxScale : HighTierThermalMaxScale;
        }

        private static bool ResolveStpIntent(HectonQualityTier tier)
        {
            return IsLowTier(tier) ||
                   tier == HectonQualityTier.Mid ||
                   tier == HectonQualityTier.High ||
                   tier == HectonQualityTier.Ultra;
        }

        private byte ResolveHardwareTierByte()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier != HectonQualityTier.Unknown)
                return (byte)tier;

            HectonHardwareProfile profile = GlobalRegistry.HardwareProfile;
            return (byte)profile.QualityTier;
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

            float deficit = math.saturate((PolicyMaxScale - nextScale) * math.rcp(PolicyMaxScale - LowTierEmergencyScale));
            return math.clamp(0.08f + deficit * 0.62f, 0f, 0.75f);
        }

        private void UpdateVisualBudget(HectonQualityTier tier, float stress01)
        {
            stress01 = Sanitize01(stress01);
            if (IsLowTier(tier))
            {
                _dearLie01 = 1f;
                _visualOverkill01 = 0f;
                _visualFeatureFlags = 0u;
                return;
            }

            if (tier == HectonQualityTier.Mid)
            {
                _dearLie01 = 0.35f;
                _visualOverkill01 = math.saturate(0.25f * (1f - stress01));
                _visualFeatureFlags = _visualOverkill01 > 0.1f ? VisualFeatureVolumetricSilt : 0u;
                return;
            }

            float headroom01 = math.saturate(1f - stress01);
            if (tier == HectonQualityTier.Ultra)
            {
                _dearLie01 = 0f;
                _visualOverkill01 = math.saturate(0.78f + headroom01 * 0.22f);
                _visualFeatureFlags =
                    VisualFeatureVisorSalt |
                    VisualFeatureVolumetricSilt |
                    VisualFeatureProceduralHullDents |
                    VisualFeaturePom16Tap |
                    VisualFeatureSubsurfaceScatter |
                    VisualFeatureRaymarchedFog;
                return;
            }

            _dearLie01 = 0f;
            _visualOverkill01 = math.saturate(0.55f + headroom01 * 0.25f);
            _visualFeatureFlags =
                VisualFeatureVisorSalt |
                VisualFeatureVolumetricSilt |
                VisualFeatureProceduralHullDents |
                VisualFeaturePom16Tap |
                VisualFeatureSubsurfaceScatter;
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
