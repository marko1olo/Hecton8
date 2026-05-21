using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Hecton8.Graphics.VR
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9946)]
    [AddComponentMenu("Hecton8/Graphics/VR/Foveated Render Commander")]
    internal sealed class FoveatedRenderCommander : MonoBehaviour, IUpdatable, IRenderable, IGlobalRegistryHotSwapListener, IScalabilityChangedEventListener, IDisposable
    {
        private const int TelemetryCapacity = 300;
        private const int TelemetryRecordSizeBytes = 64;
        private const int DefaultSampleIntervalFrames = 30;
        private const int MinSampleIntervalFrames = 1;
        private const int MaxSampleIntervalFrames = 240;
        private const int DefaultUiLayerIndex = 5;
        private const float LevelLow = 0.35f;
        private const float LevelMedium = 0.62f;
        private const float LevelHigh = 0.85f;
        private const float StressMediumThreshold = 0.35f;
        private const float StressHighThreshold = 0.70f;
        private const float GpuPressureHighThreshold = 0.78f;
        private const float GpuTimeHighPressureMs = 10.75f;
        private const float SecondsToMilliseconds = 1000f;
        private const float LevelDowngradeHoldSeconds = 2.5f;
        private const float GazeLossHoldSeconds = 0.75f;
        private const float MaxHysteresisDeltaSeconds = 0.25f;
        private const float ApplyEpsilon = 0.0001f;
        private const uint BlackBoxMagic = 0x46565243u; // FVRC
        private const uint BlackBoxVersion = 2u;
        private const uint SourceHash = 0x46565253u; // FVRS
        private const ulong TelemetrySerializedPadding = 0UL;
        private const ushort FlagXrActive = 1 << 0;
        private const ushort FlagCapsSupported = 1 << 1;
        private const ushort FlagQuest2LockedHigh = 1 << 2;
        private const ushort FlagGazeTracked = 1 << 3;
        private const ushort FlagUiSuppressed = 1 << 4;
        private const ushort FlagFlatScreenFallback = 1 << 5;
        private const ushort FlagThermalPressure = 1 << 6;
        private const ushort FlagSystemPressure = 1 << 7;
        private const ushort FlagApplied = 1 << 8;
        private const ushort FlagNonFinite = 1 << 9;
        private const ushort FlagHighEndFixedDisabled = 1 << 10;
        private const ushort FlagHysteresisHold = 1 << 11;
        private const ushort FlagGazeGraceHold = 1 << 12;
        private const ushort FlagQuestClassificationPending = 1 << 13;
        private const ushort FlagFreshGpuTimeEscalation = 1 << 14;
        private const string RuntimeObjectName = "[FoveatedRenderCommander]";
        private const string DumpFileName = "Dump_FOVEATED_RENDER_COMMANDER.bin";

        // COLD ALLOC: List<XRDisplaySubsystem>[8] - XR display enumeration scratch reused on policy commits - owner: FoveatedRenderCommander
        private static readonly List<XRDisplaySubsystem> s_displays = new List<XRDisplaySubsystem>(8);
        private static FoveatedRenderCommander s_activeCommander;
        private static bool s_questRuntimeClassified;
        private static bool s_quest2ClassRuntime;
        private static bool s_telemetryLayoutChecked;
        private static bool s_telemetryLayoutValid;

        [Header("Policy")]
        [SerializeField, Range(MinSampleIntervalFrames, MaxSampleIntervalFrames)]
        [Tooltip("Frames between hardware foveation policy commits. Signal consumption remains per dispatcher tick.")]
        private int sampleIntervalFrames = DefaultSampleIntervalFrames;

        [SerializeField]
        [Tooltip("Allows hardware foveation on non-XR cameras. Off by default; flat-screen PC must fail closed.")]
        private bool allowFlatScreenFoveation;

        [SerializeField]
        [Tooltip("Enables Unity XR gaze-allowed foveation on standalone PC VR only when eye data is present.")]
        private bool allowPcVrGazeTrackedVrs = true;

        [SerializeField]
        [Tooltip("Locks Quest 2-class runtimes to high fixed foveation regardless of transient stress.")]
        private bool lockQuest2HighFoveation = true;

        [SerializeField]
        [Tooltip("Disables foveation while cameras that render UI layers are drawing.")]
        private bool failClosedForUiCameras = true;

        [SerializeField]
        [Tooltip("Layer mask treated as text/UI. Cameras rendering this mask force foveation off for that camera.")]
        private LayerMask uiLayerMask = 1 << DefaultUiLayerIndex;

        private IDataVault _dataVault;
        private VaultGenerationHandle<FoveatedRenderTelemetryEntry> _telemetryHandle;
        private IHardwareThermalService _hardwareThermal;
        private int _telemetryCursor;
        private int _framesUntilSample;
        private int _lastEyeWidth;
        private int _lastEyeHeight;
        private int _lastDisplayCount;
        private uint _sequence;
        private uint _telemetryVaultGeneration;
        private float _systemStress01;
        private float _gpuUtil01;
        private float _latestGpuTimeMs;
        private float _downgradeHoldSecondsRemaining;
        private float _gazeLossHoldSecondsRemaining;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;
        private float _targetLevel01;
        private float _appliedLevel01 = -1f;
        private byte _pressureLevel;
        private byte _foveatedPressureTier;
        private byte _thermalSeverity;
        private byte _targetLevelCode;
        private byte _appliedLevelCode;
        private FoveatedRenderMode _targetMode;
        private FoveatedRenderMode _appliedMode = FoveatedRenderMode.Disabled;
        private XRDisplaySubsystem.FoveatedRenderingFlags _targetFlags;
        private XRDisplaySubsystem.FoveatedRenderingFlags _appliedFlags;
        private FoveatedRenderingCaps _lastCaps;
        private ushort _lastFlags;
        private bool _registeredTick;
        private bool _registeredHotSwap;
        private bool _registeredRenderable;
        private bool _blackBoxDumped;
        private bool _uiSuppressionActive;
        private bool _displayLevelNonFinite;
        private bool _disposed;

        private enum FoveatedRenderMode : byte
        {
            Disabled = 0,
            Fixed = 1,
            GazeTracked = 2,
            UiExempted = 3
        }

        [StructLayout(LayoutKind.Explicit, Size = TelemetryRecordSizeBytes)]
        private struct FoveatedRenderTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint Sequence;
            [FieldOffset(8)]
            public float TargetLevel01;
            [FieldOffset(12)]
            public float AppliedLevel01;
            [FieldOffset(16)]
            public float SystemStress01;
            [FieldOffset(20)]
            public float GpuUtil01;
            [FieldOffset(24)]
            public float GpuTimeMs;
            [FieldOffset(28)]
            public int EyeWidth;
            [FieldOffset(32)]
            public int EyeHeight;
            [FieldOffset(36)]
            public uint Flags;
            [FieldOffset(40)]
            public uint Caps;
            [FieldOffset(44)]
            public byte TargetLevelCode;
            [FieldOffset(45)]
            public byte AppliedLevelCode;
            [FieldOffset(46)]
            public byte Mode;
            [FieldOffset(47)]
            public byte PressureLevel;
            [FieldOffset(48)]
            public byte FoveatedPressureTier;
            [FieldOffset(49)]
            public byte ThermalSeverity;
            [FieldOffset(50)]
            public ushort DisplayCount;
            [FieldOffset(52)]
            public uint VaultGeneration;
            [FieldOffset(56)]
            private ulong _pad0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_displays.Clear();
            s_activeCommander = null;
            s_questRuntimeClassified = false;
            s_quest2ClassRuntime = false;
            s_telemetryLayoutChecked = false;
            s_telemetryLayoutValid = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying || s_activeCommander != null)
                return;

            GameObject host = new GameObject(RuntimeObjectName); // COLD ALLOC: GameObject[1] - runtime foveated rendering commander host - owner: FoveatedRenderCommander
            DontDestroyOnLoad(host);
            host.AddComponent<FoveatedRenderCommander>(); // COLD ALLOC: FoveatedRenderCommander[1] - runtime foveated rendering policy owner - owner: FoveatedRenderCommander
        }

        private void Awake()
        {
            if (s_activeCommander != null && !ReferenceEquals(s_activeCommander, this))
            {
                Destroy(this);
                return;
            }

            s_activeCommander = this;
            EnsureTelemetry();
            _framesUntilSample = 0;
        }

        private void OnEnable()
        {
            if (!ReferenceEquals(s_activeCommander, this))
                return;

            _disposed = false;
            _dataVault = GlobalRegistry.DataVault;
            _qualityTier = GlobalRegistry.ScalabilityTier;
            ScalabilityEvents.Register(this);
            EnsureTelemetry();
            _hardwareThermal = GlobalRegistry.HardwareThermal;
            TryRegisterTick();
            TryRegisterHotSwap();
            TryRegisterRenderable();
            ApplyPolicy(force: true);
        }

        private void Start()
        {
            if (!ReferenceEquals(s_activeCommander, this))
                return;

            TryRegisterTick();
            TryRegisterHotSwap();
            TryRegisterRenderable();
            ApplyPolicy(force: true);
        }

        private void OnDisable()
        {
            bool ownsRuntimeState = ReferenceEquals(s_activeCommander, this);
            TryUnregisterRenderable();
            TryUnregisterHotSwap();
            TryUnregisterTick();
            ScalabilityEvents.Unregister(this);
            if (ownsRuntimeState)
                ClearHardwareFoveation();
            _hardwareThermal = null;
            ReleaseTelemetryBuffer();
            _telemetryCursor = 0;
        }

        private void OnDestroy()
        {
            Dispose();
            if (ReferenceEquals(s_activeCommander, this))
                s_activeCommander = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            bool ownsRuntimeState = ReferenceEquals(s_activeCommander, this);
            TryUnregisterRenderable();
            TryUnregisterHotSwap();
            TryUnregisterTick();
            ScalabilityEvents.Unregister(this);
            if (ownsRuntimeState)
                ClearHardwareFoveation();
            ReleaseTelemetryBuffer();
            _telemetryVaultGeneration = 0u;
            _dataVault = null;
        }

        public void Tick(float deltaTime)
        {
            if (TryDetachIfInactiveCommander())
                return;

            DecayFoveationHysteresis(deltaTime);
            ConsumeSignals();

            _framesUntilSample--;
            if (_framesUntilSample <= 0)
            {
                _framesUntilSample = ClampSampleIntervalFrames(sampleIntervalFrames);
                ApplyPolicy(force: false);
            }

            WriteTelemetry(_lastFlags);
        }

        public void Render(float deltaTime)
        {
            if (!ReferenceEquals(s_activeCommander, this) || !failClosedForUiCameras)
                return;

            Camera renderCamera = GlobalRenderContext.CurrentCamera;
            if (renderCamera == null)
                return;

            bool uiCamera = _targetLevel01 > ApplyEpsilon && (renderCamera.cullingMask & uiLayerMask.value) != 0;
            if (uiCamera)
            {
                _uiSuppressionActive = true;
                _lastFlags |= FlagUiSuppressed;
                if (_appliedMode != FoveatedRenderMode.UiExempted ||
                    _appliedLevel01 > ApplyEpsilon ||
                    _appliedFlags != XRDisplaySubsystem.FoveatedRenderingFlags.None)
                {
                    ApplyDisplayState(
                        0f,
                        XRDisplaySubsystem.FoveatedRenderingFlags.None,
                        FoveatedRenderMode.UiExempted,
                        true,
                        out _,
                        out _);
                }

                return;
            }

            if (!_uiSuppressionActive)
                return;

            _uiSuppressionActive = false;
            _lastFlags = (ushort)(_lastFlags & ~FlagUiSuppressed);
            ApplyDisplayState(_targetLevel01, _targetFlags, _targetMode, true, out _, out _);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (TryDetachIfInactiveCommander())
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null)
                    TryUnregisterTick();
                else
                    TryRegisterTick();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Scene)
            {
                if (currentService != null)
                {
                    TryRegisterTick();
                    TryRegisterRenderable();
                    EnsureTelemetry();
                    ApplyPolicy(force: true);
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.HardwareThermalService)
            {
                _hardwareThermal = currentService as IHardwareThermalService;
                if (_hardwareThermal == null)
                    _thermalSeverity = 0;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ReleaseVaultBuffer(previousService as IDataVault ?? _dataVault, ref _telemetryHandle);
                _dataVault = currentService as IDataVault;
                _telemetryCursor = 0;
                _telemetryVaultGeneration = 0u;
                EnsureTelemetry();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.RenderDispatcher)
            {
                if (currentService != null)
                    TryRegisterRenderable();
                else
                    TryUnregisterRenderable();
            }
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            if (TryDetachIfInactiveCommander())
                return;

            _qualityTier = payload.CurrentQualityTier;
        }

        internal void RequestBlackBoxDump()
        {
            if (!ReferenceEquals(s_activeCommander, this) || _disposed)
                return;

            DumpBlackBoxOnce();
        }

        private void ConsumeSignals()
        {
            float stress01 = 0f;
            float gpuUtil01 = 0f;
            byte pressureLevel = 0;
            byte foveatedPressureTier = 0;
            bool hasHealth = false;

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                ref readonly SystemHealthSignal signal = ref healthSignals[i];
                stress01 = math.max(stress01, Sanitize01(signal.SystemHealthIndex01));
                gpuUtil01 = math.max(gpuUtil01, Sanitize01(signal.GpuUtil01));
                pressureLevel = MaxByte(pressureLevel, signal.PressureLevel);
                foveatedPressureTier = MaxByte(foveatedPressureTier, signal.FoveatedPressureTier);
                hasHealth = true;
            }

            if (hasHealth)
            {
                _systemStress01 = stress01;
                _gpuUtil01 = gpuUtil01;
                _pressureLevel = pressureLevel;
                _foveatedPressureTier = foveatedPressureTier;
            }

            byte thermalSeverity = 0;
            bool hasThermal = false;

            ReadOnlySpan<ThermalStateChangedSignal> thermalSignals = SignalBus<ThermalStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < thermalSignals.Length; i++)
            {
                ref readonly ThermalStateChangedSignal signal = ref thermalSignals[i];
                thermalSeverity = MaxByte(thermalSeverity, signal.Severity);
                hasThermal = true;
            }

            IHardwareThermalService thermal = _hardwareThermal;
            if (thermal != null && thermal.TryGetSnapshot(out HardwareThermalSnapshot snapshot))
            {
                thermalSeverity = MaxByte(thermalSeverity, snapshot.Severity);
                hasThermal = true;
            }

            if (hasThermal)
                _thermalSeverity = thermalSeverity;
            else if (thermal == null)
                _thermalSeverity = 0;
        }

        private void ApplyPolicy(bool force)
        {
            RenderTextureDescriptor eyeDescriptor = HectonXRManager.RefreshEyeDescriptor();
            bool xrActive = XRSettings.enabled && XRSettings.isDeviceActive;
            FoveatedRenderingCaps caps = SystemInfo.foveatedRenderingCaps;
            bool capsSupported = caps != FoveatedRenderingCaps.None;
            bool quest2Runtime = IsQuest2Runtime(out bool questClassificationPending);
            HectonQualityTier qualityTier = _qualityTier;
            bool thermalPressure =
                _thermalSeverity >= (byte)HardwareThermalSeverity.Throttling ||
                _gpuUtil01 >= GpuPressureHighThreshold ||
                IsGpuTimePressureActive(_latestGpuTimeMs);
            bool systemPressure = _systemStress01 >= StressMediumThreshold || _pressureLevel >= 2 || _foveatedPressureTier >= 2;
            ushort flags = 0;

            if (xrActive)
                flags |= FlagXrActive;
            if (capsSupported)
                flags |= FlagCapsSupported;
            if (quest2Runtime && lockQuest2HighFoveation)
                flags |= FlagQuest2LockedHigh;
            if (questClassificationPending)
                flags |= FlagQuestClassificationPending;
            if (thermalPressure)
                flags |= FlagThermalPressure;
            if (systemPressure)
                flags |= FlagSystemPressure;

            if (!xrActive && !allowFlatScreenFoveation)
            {
                flags |= FlagFlatScreenFallback;
                ResolveDisabledTarget();
                ApplyDisplayState(0f, XRDisplaySubsystem.FoveatedRenderingFlags.None, FoveatedRenderMode.Disabled, force, out _, out int flatDisplayCount);
                _lastDisplayCount = flatDisplayCount;
                ReportHardwareState(false, 0f, eyeDescriptor);
                _lastFlags = flags;
                _lastCaps = caps;
                _lastEyeWidth = eyeDescriptor.width;
                _lastEyeHeight = eyeDescriptor.height;
                return;
            }

            if (!capsSupported)
            {
                ResolveDisabledTarget();
                ApplyDisplayState(0f, XRDisplaySubsystem.FoveatedRenderingFlags.None, FoveatedRenderMode.Disabled, force, out _, out int capsDisplayCount);
                _lastDisplayCount = capsDisplayCount;
                ReportHardwareState(false, 0f, eyeDescriptor);
                _lastFlags = flags;
                _lastCaps = caps;
                _lastEyeWidth = eyeDescriptor.width;
                _lastEyeHeight = eyeDescriptor.height;
                return;
            }

            byte requestedLevelCode = ResolveTargetLevelCode(
                _systemStress01,
                _pressureLevel,
                _foveatedPressureTier,
                quest2Runtime,
                thermalPressure);
            byte levelCode = ApplyTargetLevelHysteresis(
                requestedLevelCode,
                thermalPressure || systemPressure || (quest2Runtime && lockQuest2HighFoveation),
                out bool hysteresisHeld);
            if (hysteresisHeld)
                flags |= FlagHysteresisHold;
            float targetLevel = ResolveLevel01(levelCode);
            bool gazeTracked = ShouldUseGazeTrackedVrs(xrActive, caps, quest2Runtime, out bool gazeGraceHeld);
            XRDisplaySubsystem.FoveatedRenderingFlags targetFlags = gazeTracked
                ? XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed
                : XRDisplaySubsystem.FoveatedRenderingFlags.None;
            FoveatedRenderMode mode = gazeTracked ? FoveatedRenderMode.GazeTracked : FoveatedRenderMode.Fixed;
            if (gazeTracked)
                flags |= FlagGazeTracked;
            if (gazeGraceHeld)
                flags |= FlagGazeGraceHold;

            if (!quest2Runtime &&
                !gazeTracked &&
                IsHighEndTier(qualityTier) &&
                !thermalPressure &&
                !systemPressure)
            {
                levelCode = 0;
                targetLevel = 0f;
                mode = FoveatedRenderMode.Disabled;
                _downgradeHoldSecondsRemaining = 0f;
                _gazeLossHoldSecondsRemaining = 0f;
                flags = (ushort)(flags & ~FlagHysteresisHold);
                flags = (ushort)(flags & ~FlagGazeGraceHold);
                flags |= FlagHighEndFixedDisabled;
            }

            _targetLevelCode = levelCode;
            _targetLevel01 = targetLevel;
            _targetMode = mode;
            _targetFlags = targetFlags;

            bool applied = ApplyDisplayState(targetLevel, targetFlags, mode, force, out float appliedLevel, out int displayCount);
            if (!thermalPressure && IsGpuTimePressureActive(_latestGpuTimeMs))
            {
                thermalPressure = true;
                flags = (ushort)(flags & ~(FlagApplied | FlagNonFinite | FlagHighEndFixedDisabled | FlagHysteresisHold));
                flags |= FlagThermalPressure;
                flags |= FlagFreshGpuTimeEscalation;

                levelCode = ApplyTargetLevelHysteresis(
                    ResolveTargetLevelCode(
                        _systemStress01,
                        _pressureLevel,
                        _foveatedPressureTier,
                        quest2Runtime,
                        true),
                    true,
                    out bool gpuTimeHysteresisHeld);
                if (gpuTimeHysteresisHeld)
                    flags |= FlagHysteresisHold;

                targetLevel = ResolveLevel01(levelCode);
                mode = gazeTracked ? FoveatedRenderMode.GazeTracked : FoveatedRenderMode.Fixed;
                targetFlags = gazeTracked
                    ? XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed
                    : XRDisplaySubsystem.FoveatedRenderingFlags.None;

                _targetLevelCode = levelCode;
                _targetLevel01 = targetLevel;
                _targetMode = mode;
                _targetFlags = targetFlags;

                applied = ApplyDisplayState(targetLevel, targetFlags, mode, true, out appliedLevel, out displayCount);
            }

            if (applied)
                flags |= FlagApplied;
            bool invalidState = !math.isfinite(appliedLevel) ||
                _displayLevelNonFinite ||
                eyeDescriptor.width <= 0 ||
                eyeDescriptor.height <= 0;
            if (invalidState)
                flags |= FlagNonFinite;

            _lastDisplayCount = displayCount;
            _lastEyeWidth = eyeDescriptor.width;
            _lastEyeHeight = eyeDescriptor.height;
            _lastFlags = flags;
            _lastCaps = caps;
            bool reportApplied = applied && !invalidState;
            ReportHardwareState(reportApplied, reportApplied ? appliedLevel : 0f, eyeDescriptor);
            if ((flags & FlagNonFinite) != 0)
            {
                WriteTelemetry(flags);
                DumpBlackBoxOnce();
                ClearHardwareFoveation();
            }
        }

        private bool ApplyDisplayState(
            float targetLevel,
            XRDisplaySubsystem.FoveatedRenderingFlags targetFlags,
            FoveatedRenderMode mode,
            bool force,
            out float appliedLevel,
            out int displayCount)
        {
            targetLevel = Sanitize01(targetLevel);
            appliedLevel = 0f;
            displayCount = 0;

            bool stateUnchanged = !force &&
                math.abs(_appliedLevel01 - targetLevel) <= ApplyEpsilon &&
                _appliedFlags == targetFlags &&
                _appliedMode == mode;

            s_displays.Clear();
            SubsystemManager.GetSubsystems(s_displays);
            float sampledGpuTimeMs = 0f;
            bool gpuTimeSampled = false;
            _displayLevelNonFinite = false;
            for (int i = 0; i < s_displays.Count; i++)
            {
                XRDisplaySubsystem display = s_displays[i];
                if (display == null || !display.running)
                    continue;

                float displayLevel = display.foveatedRenderingLevel;
                bool displayLevelFinite = math.isfinite(displayLevel);
                if (!displayLevelFinite)
                    _displayLevelNonFinite = true;
                bool displayDrifted = display.foveatedRenderingFlags != targetFlags ||
                    !displayLevelFinite ||
                    math.abs(displayLevel - targetLevel) > ApplyEpsilon;

                if (!stateUnchanged || displayDrifted)
                {
                    display.foveatedRenderingFlags = targetFlags;
                    display.foveatedRenderingLevel = targetLevel;
                    displayLevel = display.foveatedRenderingLevel;
                    displayLevelFinite = math.isfinite(displayLevel);
                    if (!displayLevelFinite)
                        _displayLevelNonFinite = true;
                }

                if (!displayLevelFinite)
                    displayLevel = 0f;

                appliedLevel = math.max(appliedLevel, displayLevel);
                if (display.TryGetAppGPUTimeLastFrame(out float gpuSeconds) && math.isfinite(gpuSeconds) && gpuSeconds >= 0f)
                {
                    float gpuMs = gpuSeconds * SecondsToMilliseconds;
                    if (math.isfinite(gpuMs))
                    {
                        sampledGpuTimeMs = math.max(sampledGpuTimeMs, gpuMs);
                        gpuTimeSampled = true;
                    }
                }

                displayCount++;
            }

            _latestGpuTimeMs = gpuTimeSampled ? sampledGpuTimeMs : 0f;
            _appliedLevel01 = displayCount > 0 ? appliedLevel : 0f;
            _appliedLevelCode = ResolveAppliedLevelCode(_appliedLevel01);
            _appliedFlags = targetFlags;
            _appliedMode = displayCount > 0 ? mode : FoveatedRenderMode.Disabled;
            return displayCount > 0 && appliedLevel > ApplyEpsilon;
        }

        private void ClearHardwareFoveation()
        {
            _uiSuppressionActive = false;
            _lastFlags = (ushort)(_lastFlags & ~FlagUiSuppressed);
            ResolveDisabledTarget();
            ApplyDisplayState(0f, XRDisplaySubsystem.FoveatedRenderingFlags.None, FoveatedRenderMode.Disabled, true, out _, out _);
            HectonXRRuntimeState.ReportHardwareFoveationState(false, 0f, 0, 0);
        }

        private void ResolveDisabledTarget()
        {
            _targetLevelCode = 0;
            _targetLevel01 = 0f;
            _targetMode = FoveatedRenderMode.Disabled;
            _targetFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
            _downgradeHoldSecondsRemaining = 0f;
            _gazeLossHoldSecondsRemaining = 0f;
        }

        private void DecayFoveationHysteresis(float deltaTime)
        {
            if (_downgradeHoldSecondsRemaining <= 0f && _gazeLossHoldSecondsRemaining <= 0f)
                return;

            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            float cappedDelta = math.min(deltaTime, MaxHysteresisDeltaSeconds);
            if (_downgradeHoldSecondsRemaining > 0f)
                _downgradeHoldSecondsRemaining = math.max(0f, _downgradeHoldSecondsRemaining - cappedDelta);
            if (_gazeLossHoldSecondsRemaining > 0f)
                _gazeLossHoldSecondsRemaining = math.max(0f, _gazeLossHoldSecondsRemaining - cappedDelta);
        }

        private byte ApplyTargetLevelHysteresis(byte requestedLevelCode, bool pressureActive, out bool held)
        {
            held = false;
            byte previousLevelCode = _targetLevelCode;
            if (requestedLevelCode > previousLevelCode)
            {
                _downgradeHoldSecondsRemaining = LevelDowngradeHoldSeconds;
                return requestedLevelCode;
            }

            if (requestedLevelCode == previousLevelCode)
            {
                if (pressureActive && requestedLevelCode > 0)
                    _downgradeHoldSecondsRemaining = LevelDowngradeHoldSeconds;
                return requestedLevelCode;
            }

            if (previousLevelCode > requestedLevelCode && _downgradeHoldSecondsRemaining > 0f)
            {
                held = true;
                return previousLevelCode;
            }

            return requestedLevelCode;
        }

        private void ReportHardwareState(bool applied, float appliedLevel, RenderTextureDescriptor eyeDescriptor)
        {
            HectonXRRuntimeState.ReportHardwareFoveationState(
                applied,
                applied ? appliedLevel : 0f,
                eyeDescriptor.width,
                eyeDescriptor.height);
        }

        private bool TryDetachIfInactiveCommander()
        {
            if (ReferenceEquals(s_activeCommander, this) && !_disposed)
                return false;

            TryUnregisterRenderable();
            TryUnregisterHotSwap();
            TryUnregisterTick();
            ScalabilityEvents.Unregister(this);
            _hardwareThermal = null;
            return true;
        }

        private void TryRegisterTick()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                _registeredTick = false;
                return;
            }

            RegistryBucket<IUpdatable> updatables = GlobalRegistry.Updatables;
            RegistryBucket<IUpdatable> dispatcherLane = SystemDispatcher.GetLane(PriorityLayer.Core);
            bool inGlobalBucket = updatables.Contains(this);
            bool inDispatcherLane = dispatcherLane.Contains(this);
            if (inGlobalBucket && inDispatcherLane)
            {
                _registeredTick = true;
                return;
            }

            if (inGlobalBucket || inDispatcherLane)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterTick()
        {
            RegistryBucket<IUpdatable> updatables = GlobalRegistry.Updatables;
            RegistryBucket<IUpdatable> dispatcherLane = SystemDispatcher.GetLane(PriorityLayer.Core);
            if (!_registeredTick && !updatables.Contains(this) && !dispatcherLane.Contains(this))
            {
                _registeredTick = false;
                return;
            }

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private void TryRegisterHotSwap()
        {
            if (!Application.isPlaying)
            {
                _registeredHotSwap = false;
                return;
            }

            if (GlobalRegistry.IsHotSwapListenerRegistered(this))
            {
                _registeredHotSwap = true;
                return;
            }

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap && !GlobalRegistry.IsHotSwapListenerRegistered(this))
            {
                _registeredHotSwap = false;
                return;
            }

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void TryRegisterRenderable()
        {
            if (!Application.isPlaying)
                return;

            RegistryBucket<IRenderable> renderables = GlobalRegistry.Renderables;
            if (renderables.Contains(this))
            {
                _registeredRenderable = true;
                return;
            }

            _registeredRenderable = renderables.TryRegister(this);
        }

        private void TryUnregisterRenderable()
        {
            RegistryBucket<IRenderable> renderables = GlobalRegistry.Renderables;
            if (!_registeredRenderable && !renderables.Contains(this))
            {
                _registeredRenderable = false;
                return;
            }

            renderables.TryUnregister(this);
            _registeredRenderable = false;
            _uiSuppressionActive = false;
            _lastFlags = (ushort)(_lastFlags & ~FlagUiSuppressed);
        }

        private bool EnsureTelemetry()
        {
            if (!VerifyTelemetryLayout())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearTelemetryDescriptor();
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                ClearTelemetryDescriptor();
                return false;
            }

            if (IsVaultHandleCreated(in _telemetryHandle) &&
                vault.TryResolveHandle(in _telemetryHandle, out NativeArray<FoveatedRenderTelemetryEntry> currentTelemetry) &&
                currentTelemetry.IsCreated &&
                currentTelemetry.Length >= TelemetryCapacity)
            {
                _telemetryVaultGeneration = _telemetryHandle.Generation;
                return true;
            }

            ClearTelemetryDescriptor();
            if (vault.TryGetGenerationHandle(
                    BufferID.FoveatedRenderBlackBox,
                    out VaultGenerationHandle<FoveatedRenderTelemetryEntry> existing) &&
                vault.TryResolveHandle(in existing, out NativeArray<FoveatedRenderTelemetryEntry> existingTelemetry) &&
                existingTelemetry.IsCreated &&
                existingTelemetry.Length >= TelemetryCapacity)
            {
                _telemetryHandle = existing;
                _telemetryVaultGeneration = existing.Generation;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<FoveatedRenderTelemetryEntry> acquired = vault.GetGenerationHandle<FoveatedRenderTelemetryEntry>(
                BufferID.FoveatedRenderBlackBox,
                TelemetryCapacity,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out NativeArray<FoveatedRenderTelemetryEntry> acquiredTelemetry) ||
                !acquiredTelemetry.IsCreated ||
                acquiredTelemetry.Length < TelemetryCapacity)
            {
                ReleaseVaultBuffer(vault, ref acquired);
                ClearTelemetryDescriptor();
                return false;
            }

            _telemetryHandle = acquired;
            _telemetryVaultGeneration = acquired.Generation;
            return true;
        }

        private static bool VerifyTelemetryLayout()
        {
            if (s_telemetryLayoutChecked)
                return s_telemetryLayoutValid;

            s_telemetryLayoutValid = UnsafeUtility.SizeOf<FoveatedRenderTelemetryEntry>() == TelemetryRecordSizeBytes;
            s_telemetryLayoutChecked = true;
            if (!s_telemetryLayoutValid)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SourceHash));

            return s_telemetryLayoutValid;
        }

        private void WriteTelemetry(ushort flags)
        {
            if (!TryResolveTelemetryRing(out NativeArray<FoveatedRenderTelemetryEntry> telemetry, allowEnsure: true))
                return;

            bool nonFinite =
                !math.isfinite(_targetLevel01) ||
                !math.isfinite(_appliedLevel01) ||
                !math.isfinite(_systemStress01) ||
                !math.isfinite(_gpuUtil01) ||
                !math.isfinite(_latestGpuTimeMs) ||
                _displayLevelNonFinite;

            ushort writeFlags = nonFinite ? (ushort)(flags | FlagNonFinite) : flags;
            if (_uiSuppressionActive)
                writeFlags |= FlagUiSuppressed;
            else
                writeFlags = (ushort)(writeFlags & ~FlagUiSuppressed);

            telemetry[_telemetryCursor] = new FoveatedRenderTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                Sequence = _sequence++,
                TargetLevel01 = _targetLevel01,
                AppliedLevel01 = math.max(0f, _appliedLevel01),
                SystemStress01 = _systemStress01,
                GpuUtil01 = _gpuUtil01,
                GpuTimeMs = _latestGpuTimeMs,
                EyeWidth = _lastEyeWidth,
                EyeHeight = _lastEyeHeight,
                Flags = writeFlags,
                Caps = unchecked((uint)_lastCaps),
                TargetLevelCode = _targetLevelCode,
                AppliedLevelCode = _appliedLevelCode,
                Mode = (byte)_appliedMode,
                PressureLevel = _pressureLevel,
                FoveatedPressureTier = _foveatedPressureTier,
                ThermalSeverity = _thermalSeverity,
                DisplayCount = (ushort)math.clamp(_lastDisplayCount, 0, ushort.MaxValue),
                VaultGeneration = _telemetryVaultGeneration
            };

            _telemetryCursor++;
            if (_telemetryCursor >= TelemetryCapacity)
                _telemetryCursor = 0;

            if (nonFinite)
            {
                DumpBlackBoxOnce();
                _systemStress01 = 0f;
                _gpuUtil01 = 0f;
                _latestGpuTimeMs = 0f;
                ClearHardwareFoveation();
            }
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped ||
                !TryResolveTelemetryRing(out NativeArray<FoveatedRenderTelemetryEntry> telemetry, allowEnsure: true))
            {
                return;
            }

            _blackBoxDumped = true;
            try
            {
                if (!TryOpenDumpStream(out FileStream stream))
                {
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SourceHash));
                    return;
                }

                using (stream)
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(BlackBoxMagic);
                    writer.Write(BlackBoxVersion);
                    writer.Write(TelemetryCapacity);
                    writer.Write(TelemetryRecordSizeBytes);
                    writer.Write(_telemetryCursor);
                    writer.Write(_sequence);
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        int index = _telemetryCursor + i;
                        if (index >= TelemetryCapacity)
                            index -= TelemetryCapacity;

                        FoveatedRenderTelemetryEntry entry = telemetry[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.TargetLevel01);
                        writer.Write(entry.AppliedLevel01);
                        writer.Write(entry.SystemStress01);
                        writer.Write(entry.GpuUtil01);
                        writer.Write(entry.GpuTimeMs);
                        writer.Write(entry.EyeWidth);
                        writer.Write(entry.EyeHeight);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Caps);
                        writer.Write(entry.TargetLevelCode);
                        writer.Write(entry.AppliedLevelCode);
                        writer.Write(entry.Mode);
                        writer.Write(entry.PressureLevel);
                        writer.Write(entry.FoveatedPressureTier);
                        writer.Write(entry.ThermalSeverity);
                        writer.Write(entry.DisplayCount);
                        writer.Write(entry.VaultGeneration);
                        writer.Write(TelemetrySerializedPadding);
                    }
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SourceHash));
            }
        }

        private static bool TryOpenDumpStream(out FileStream stream)
        {
            stream = null;
            if (TryGetProjectDumpPath(out string projectPath) &&
                TryOpenDumpPath(projectPath, out stream))
            {
                return true;
            }

            string persistentRoot = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentRoot))
                return false;

            string persistentPath = Path.Combine(persistentRoot, "AgentLogs", DumpFileName);
            return TryOpenDumpPath(persistentPath, out stream);
        }

        private static bool TryGetProjectDumpPath(out string path)
        {
            path = null;
            try
            {
                string dataPath = Application.dataPath;
                if (string.IsNullOrEmpty(dataPath))
                    return false;

                string projectRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
                path = Path.Combine(projectRoot, "Docs", "AgentLogs", DumpFileName);
                return true;
            }
            catch (Exception)
            {
                path = null;
                return false;
            }
        }

        private static bool TryOpenDumpPath(string path, out FileStream stream)
        {
            stream = null;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                return true;
            }
            catch (Exception)
            {
                stream?.Dispose();
                stream = null;
                return false;
            }
        }

        private bool TryResolveTelemetryRing(out NativeArray<FoveatedRenderTelemetryEntry> telemetry, bool allowEnsure)
        {
            telemetry = default;
            if (allowEnsure && !EnsureTelemetry())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultHandleCreated(in _telemetryHandle))
                return false;

            if (!vault.TryResolveHandle(in _telemetryHandle, out telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length < TelemetryCapacity)
            {
                if (allowEnsure)
                    ClearTelemetryDescriptor();

                return false;
            }

            _telemetryVaultGeneration = _telemetryHandle.Generation;
            return true;
        }

        private void ClearTelemetryDescriptor()
        {
            _telemetryHandle = default;
            _telemetryVaultGeneration = 0u;
        }

        private void ReleaseTelemetryBuffer()
        {
            ReleaseVaultBuffer(_dataVault, ref _telemetryHandle);
            _telemetryVaultGeneration = 0u;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static byte ResolveTargetLevelCode(
            float systemStress01,
            byte pressureLevel,
            byte foveatedPressureTier,
            bool quest2Runtime,
            bool thermalPressure)
        {
            if (quest2Runtime ||
                thermalPressure ||
                pressureLevel >= 3 ||
                foveatedPressureTier >= 3)
            {
                return 3;
            }

            float stress = Sanitize01(systemStress01);

            if (stress >= StressHighThreshold)
                return 3;

            if (stress >= StressMediumThreshold ||
                pressureLevel >= 2 ||
                foveatedPressureTier >= 2)
            {
                return 2;
            }

            return 1;
        }

        private byte ResolveAppliedLevelCode(float level01)
        {
            if (!math.isfinite(level01) || level01 <= ApplyEpsilon)
                return 0;
            if (level01 >= LevelHigh - ApplyEpsilon)
                return 3;
            if (level01 >= LevelMedium - ApplyEpsilon)
                return 2;
            return 1;
        }

        private static float ResolveLevel01(byte levelCode)
        {
            switch (levelCode)
            {
                case 3:
                    return LevelHigh;
                case 2:
                    return LevelMedium;
                case 1:
                    return LevelLow;
                default:
                    return 0f;
            }
        }

        private static bool HasEyeTrackedGaze(bool xrActive, FoveatedRenderingCaps caps, bool questRuntime)
        {
            if (!xrActive || questRuntime || !IsStandaloneLikeRuntime())
                return false;

            if (!HasCap(caps, FoveatedRenderingCaps.FoveationImage) &&
                !HasCap(caps, FoveatedRenderingCaps.NonUniformRaster))
            {
                return false;
            }

            InputDevice centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!centerEye.isValid || !centerEye.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
                return false;

            return eyes.TryGetFixationPoint(out Vector3 fixationPoint) && IsFiniteVector(fixationPoint);
        }

        private bool ShouldUseGazeTrackedVrs(bool xrActive, FoveatedRenderingCaps caps, bool questRuntime, out bool graceHeld)
        {
            graceHeld = false;
            bool gazeEligible =
                xrActive &&
                IsStandaloneLikeRuntime() &&
                (HasCap(caps, FoveatedRenderingCaps.FoveationImage) ||
                 HasCap(caps, FoveatedRenderingCaps.NonUniformRaster));
            if (!allowPcVrGazeTrackedVrs || questRuntime || !gazeEligible)
            {
                _gazeLossHoldSecondsRemaining = 0f;
                return false;
            }

            if (HasEyeTrackedGaze(xrActive, caps, questRuntime))
            {
                _gazeLossHoldSecondsRemaining = GazeLossHoldSeconds;
                return true;
            }

            if (_targetMode == FoveatedRenderMode.GazeTracked && _gazeLossHoldSecondsRemaining > 0f)
            {
                graceHeld = true;
                return true;
            }

            return false;
        }

        private static bool IsQuest2Runtime(out bool classificationPending)
        {
            classificationPending = false;
            bool xrActive = HectonXRRuntimeState.IsXRActive || XRSettings.enabled || XRSettings.isDeviceActive;
            if (!xrActive || Application.platform != RuntimePlatform.Android)
                return false;

            EnsureQuestRuntimeClassification();
            classificationPending = !s_questRuntimeClassified;
            return s_quest2ClassRuntime;
        }

        private static void EnsureQuestRuntimeClassification()
        {
            if (s_questRuntimeClassified)
                return;

            QuestVulkanRuntimePolicy.EnsureInitialized();

            bool quest3OrPro =
                HardwareTierDetector.IsQuest3Like ||
                ContainsToken(XRSettings.loadedDeviceName, "Quest 3") ||
                ContainsToken(XRSettings.loadedDeviceName, "Quest3") ||
                ContainsToken(SystemInfo.deviceModel, "Quest Pro") ||
                ContainsToken(SystemInfo.deviceName, "Quest Pro") ||
                ContainsToken(XRSettings.loadedDeviceName, "Quest Pro");
            bool quest2Token =
                ContainsToken(SystemInfo.deviceModel, "Quest 2") ||
                ContainsToken(SystemInfo.deviceName, "Quest 2") ||
                ContainsToken(XRSettings.loadedDeviceName, "Quest 2") ||
                ContainsToken(SystemInfo.deviceModel, "Oculus Quest") ||
                ContainsToken(XRSettings.loadedDeviceName, "Oculus Quest");
            bool questFamilyDevice = IsQuestFamilyDevice();
            bool questMemoryGate =
                QuestVulkanRuntimePolicy.SystemMemoryMegabytes > 0 &&
                QuestVulkanRuntimePolicy.SystemMemoryMegabytes < QuestVulkanRuntimePolicy.QuestMemoryGateMegabytes &&
                questFamilyDevice;
            bool hasLoadedDeviceName = !string.IsNullOrEmpty(XRSettings.loadedDeviceName);

            if (quest3OrPro)
            {
                s_quest2ClassRuntime = false;
                s_questRuntimeClassified = true;
                return;
            }

            if (quest2Token || questMemoryGate)
            {
                s_quest2ClassRuntime = true;
                s_questRuntimeClassified = true;
                return;
            }

            if (hasLoadedDeviceName && !questFamilyDevice)
            {
                s_quest2ClassRuntime = false;
                s_questRuntimeClassified = true;
            }
        }

        private static bool IsQuestFamilyDevice()
        {
            return ContainsToken(SystemInfo.deviceModel, "Quest") ||
                   ContainsToken(SystemInfo.deviceName, "Quest") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Quest") ||
                   ContainsToken(SystemInfo.deviceModel, "Oculus") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Oculus") ||
                   ContainsToken(SystemInfo.deviceModel, "Meta") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Meta");
        }

        private static bool IsStandaloneLikeRuntime()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasCap(FoveatedRenderingCaps caps, FoveatedRenderingCaps flag)
        {
            return (caps & flag) != 0;
        }

        private static bool IsHighEndTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static bool IsGpuTimePressureActive(float gpuTimeMs)
        {
            return math.isfinite(gpuTimeMs) && gpuTimeMs >= GpuTimeHighPressureMs;
        }

        private static byte MaxByte(byte a, byte b)
        {
            return a > b ? a : b;
        }

        private static int ClampSampleIntervalFrames(int value)
        {
            if (value < MinSampleIntervalFrames)
                return DefaultSampleIntervalFrames;
            if (value > MaxSampleIntervalFrames)
                return MaxSampleIntervalFrames;
            return value;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
