using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Signals;
using Hecton8.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.XR;
#endif

namespace Hecton8.Graphics.DRS
{
    /// <summary>
    /// Signal-driven render-scale governor. No Update; dispatcher-owned pre-simulation signal consumer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9947)]
    public sealed class ThermalDynamicResolutionAdapter :
        MonoBehaviour,
        IUpdatable,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IDisposable
    {
        private const int TelemetryCapacity = 300;
        private const uint TelemetryMagic = 0x44525341u; // DRSA
        private const uint SourceHash = 0x52445253u; // RDRS
        private const uint ScaleContextHash = 0x5343414Cu; // SCAL
        private const uint DrsWarningHash = 0x44525357u; // DRSW
        private const string NotificationMessage = "SYS: RESOLUTION SCALED";
        private const string OwnerName = nameof(ThermalDynamicResolutionAdapter);
        private const string DumpFileName = "Dump_REND_DYNAMIC_RESOLUTION_ADAPTER.bin";
        private const float DangerFrameTimeMs = 15.0f;
        private const float TargetFrameTimeMs = 16.66f;
        private const float MinScale = 0.5f;
        private const float MaxScale = 1.0f;
        private const float ThermalMaxScale = 0.7f;
        private const float NotificationThreshold = 0.6f;
        private const float NotificationResetThreshold = 0.65f;
        private const float RecoveryStepPerTick = 0.01f;
        private const float ScaleEpsilon = 0.0001f;
        private const byte FlagThermalOverride = 1 << 0;
        private const byte FlagFramePressure = 1 << 1;
        private const byte FlagNotification = 1 << 2;

        private static readonly PerformDynamicRes s_systemScaler = ResolveSystemScalePercentage;
        private static readonly PerformDynamicRes s_nativeScale = ResolveNativeScalePercentage;
        private static ThermalDynamicResolutionAdapter s_activeAdapter;
        private static float s_systemScalePercentage = 100f;

        private UniversalRenderPipelineAsset _urpAsset;
        private IDynamicResolutionRuntime _dynamicResolutionRuntime;
        private NativeArray<DrsTelemetryEntry> _telemetryRing;
        private int _telemetryCursor;
        private uint _sequence;
        private uint _notificationMessageHash;
        private float _currentScale = 1f;
        private float _targetScale = 1f;
        private float _latestFrameTimeEwmaMs = TargetFrameTimeMs;
        private float _latestSystemHealth01;
        private byte _pressureLevel;
        private byte _thermalSeverity;
        private byte _foveatedPressureTier;
        private int _lastTelemetryScaleMilli = -1;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _notificationArmed = true;
        private bool _blackBoxDumped;

#if UNITY_ANDROID && !UNITY_EDITOR
        // COLD ALLOC: List<XRDisplaySubsystem>[4] - Quest display bridge scratch; reused only on scale changes.
        private readonly List<XRDisplaySubsystem> _xrDisplays = new List<XRDisplaySubsystem>(4);
        private float _lastXrScale = -1f;
#endif

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
        private struct DrsTelemetryEntry
        {
            public uint Frame;
            public float CurrentScale01;
            public float TargetScale01;
            public float FrameTimeEwmaMs;
            public float SystemHealth01;
            public uint Flags;
            public byte PressureLevel;
            public byte ThermalSeverity;
            public ushort Reserved;
            public uint Sequence;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeAdapter = null;
            s_systemScalePercentage = 100f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying || s_activeAdapter != null)
                return;

            GameObject host = new GameObject("[ThermalDynamicResolutionAdapter]");
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
            _currentScale = _urpAsset != null ? math.clamp(_urpAsset.renderScale, MinScale, MaxScale) : MaxScale;
            _targetScale = _currentScale;
            s_systemScalePercentage = _currentScale * 100f;
            _notificationMessageHash = NotificationEvents.RegisterMessage(NotificationMessage);
            EnsureTelemetry();
            EnsureUpscalingFilter();
            InstallSystemDynamicResolutionScaler();
            RebindDynamicResolutionRuntime(GlobalRegistry.DynamicResolutionRuntime);
        }

        private void OnEnable()
        {
            TryRegister();
            TryRegisterHotSwap();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwap();
            if (_dynamicResolutionRuntime != null)
                _dynamicResolutionRuntime.ClearSystemOverrideRenderScale();
        }

        private void OnDestroy()
        {
            Dispose();
            if (ReferenceEquals(s_activeAdapter, this))
            {
                s_activeAdapter = null;
                DynamicResolutionHandler.SetSystemDynamicResScaler(s_nativeScale, DynamicResScalePolicyType.ReturnsPercentage);
                DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.User);
                s_systemScalePercentage = 100f;
            }
        }

        public void Dispose()
        {
            TryUnregister();
            TryUnregisterHotSwap();
            if (_dynamicResolutionRuntime != null)
                _dynamicResolutionRuntime.ClearSystemOverrideRenderScale();
            if (_telemetryRing.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_telemetryRing);
                _telemetryRing.Dispose();
                _telemetryRing = default;
            }
        }

        public void Tick(float deltaTime)
        {
            ConsumeSignals();

            byte flags = 0;
            float frameTimeMs = SanitizePositive(_latestFrameTimeEwmaMs, TargetFrameTimeMs);
            float targetScale = frameTimeMs > DangerFrameTimeMs
                ? TargetFrameTimeMs * math.rcp(frameTimeMs)
                : MaxScale;
            if (frameTimeMs > DangerFrameTimeMs)
                flags |= FlagFramePressure;

            targetScale = math.clamp(targetScale, MinScale, MaxScale);
            bool thermalOverride = _pressureLevel >= 2 || _thermalSeverity >= (byte)HardwareThermalSeverity.Throttling;
            if (thermalOverride)
            {
                targetScale = math.min(targetScale, ThermalMaxScale);
                flags |= FlagThermalOverride;
            }

            float nextScale = targetScale < _currentScale
                ? targetScale
                : math.min(targetScale, _currentScale + RecoveryStepPerTick);
            nextScale = math.clamp(nextScale, MinScale, MaxScale);
            _targetScale = targetScale;

            if (math.abs(nextScale - _currentScale) > ScaleEpsilon)
            {
                _currentScale = nextScale;
                CommitRenderScale(flags);
            }
            else
            {
                CommitRuntimeSnapshot(flags);
            }

            if (_currentScale < NotificationThreshold)
            {
                flags |= FlagNotification;
                PublishScaleNotificationOnce();
            }
            else if (_currentScale > NotificationResetThreshold)
            {
                _notificationArmed = true;
            }

            WriteTelemetry(flags);
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime)
                RebindDynamicResolutionRuntime(currentService as IDynamicResolutionRuntime);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime)
                RebindDynamicResolutionRuntime(currentService as IDynamicResolutionRuntime);
        }

        private void ConsumeSignals()
        {
            ReadOnlySpan<FrameTimeSignal> frameTimeSignals = SignalBus<FrameTimeSignal>.GetFrameSnapshot();
            for (int i = 0; i < frameTimeSignals.Length; i++)
            {
                FrameTimeSignal signal = frameTimeSignals[i];
                _latestFrameTimeEwmaMs = SanitizePositive(signal.FrameTimeEwmaMs, _latestFrameTimeEwmaMs);
                _pressureLevel = signal.PressureLevel;
            }

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthSignal signal = healthSignals[i];
                _latestSystemHealth01 = math.saturate(signal.SystemHealthIndex01);
                _pressureLevel = signal.PressureLevel;
                _foveatedPressureTier = signal.FoveatedPressureTier;
                if (signal.FpsEwma > 0f)
                    _latestFrameTimeEwmaMs = 1000f * math.rcp(math.max(1f, signal.FpsEwma));
            }

            ReadOnlySpan<ThermalStateChangedSignal> thermalSignals = SignalBus<ThermalStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < thermalSignals.Length; i++)
                _thermalSeverity = thermalSignals[i].Severity;
        }

        private void CommitRenderScale(byte flags)
        {
            s_systemScalePercentage = _currentScale * 100f;
            DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.System);

            if (_urpAsset != null)
            {
                _urpAsset.renderScale = _currentScale;
                ScalableBufferManager.ResizeBuffers(_currentScale, _currentScale);
            }

            CommitRuntimeSnapshot(flags);
            CommitQuestXrScale();
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

        private void EnsureTelemetry()
        {
            if (_telemetryRing.IsCreated)
                return;

            _telemetryRing = new NativeArray<DrsTelemetryEntry>(
                TelemetryCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<DrsTelemetryEntry>[300] - DRS blackbox telemetry - owner: ThermalDynamicResolutionAdapter
            NativeMemorySentinel.RegisterNativeArray(
                _telemetryRing,
                OwnerName,
                nameof(_telemetryRing),
                NativeAllocationLifetime.Session);
        }

        private void EnsureUpscalingFilter()
        {
            if (_urpAsset == null)
                return;

            UpscalingFilterSelection filter = _urpAsset.upscalingFilter;
            if (filter == UpscalingFilterSelection.STP || filter == UpscalingFilterSelection.FSR)
                return;

            _urpAsset.upscalingFilter = SystemInfo.supportsComputeShaders
                ? UpscalingFilterSelection.STP
                : UpscalingFilterSelection.FSR;
        }

        private void InstallSystemDynamicResolutionScaler()
        {
            DynamicResolutionHandler.SetSystemDynamicResScaler(s_systemScaler, DynamicResScalePolicyType.ReturnsPercentage);
            DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.System);
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
            _dynamicResolutionRuntime = runtime;
            if (_dynamicResolutionRuntime != null)
            {
                _dynamicResolutionRuntime.ApplySystemOverrideRenderScale(
                    _currentScale,
                    _targetScale,
                    _latestFrameTimeEwmaMs,
                    _pressureLevel,
                    0);
            }
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
                Severity = (byte)NotificationEventSeverity.Warning,
                Flags = _foveatedPressureTier
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishScaleTelemetryIfChanged()
        {
            int scaleMilli = (int)math.round(_currentScale * 1000f);
            if (scaleMilli == _lastTelemetryScaleMilli)
                return;

            _lastTelemetryScaleMilli = scaleMilli;
            GlobalTelemetryBus.PublishPerformanceWarning(DrsWarningHash, ScaleContextHash, _currentScale);
        }

        private void WriteTelemetry(byte flags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            bool nonFinite =
                !math.isfinite(_currentScale) ||
                !math.isfinite(_targetScale) ||
                !math.isfinite(_latestFrameTimeEwmaMs);
            if (nonFinite)
            {
                DumpBlackBoxOnce();
                _currentScale = MaxScale;
                _targetScale = MaxScale;
                s_systemScalePercentage = 100f;
                flags = 0;
            }

            int index = _telemetryCursor;
            _telemetryRing[index] = new DrsTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                CurrentScale01 = _currentScale,
                TargetScale01 = _targetScale,
                FrameTimeEwmaMs = _latestFrameTimeEwmaMs,
                SystemHealth01 = _latestSystemHealth01,
                Flags = flags,
                PressureLevel = _pressureLevel,
                ThermalSeverity = _thermalSeverity,
                Reserved = 0,
                Sequence = _sequence++
            };

            index++;
            _telemetryCursor = index >= TelemetryCapacity ? 0 : index;
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !_telemetryRing.IsCreated)
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
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    DrsTelemetryEntry entry = _telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.CurrentScale01);
                    writer.Write(entry.TargetScale01);
                    writer.Write(entry.FrameTimeEwmaMs);
                    writer.Write(entry.SystemHealth01);
                    writer.Write(entry.Flags);
                    writer.Write(entry.PressureLevel);
                    writer.Write(entry.ThermalSeverity);
                    writer.Write(entry.Reserved);
                    writer.Write(entry.Sequence);
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
