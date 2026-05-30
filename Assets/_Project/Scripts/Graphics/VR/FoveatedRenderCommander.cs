using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Visor;
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
    internal sealed class FoveatedRenderCommander : MonoBehaviour, ILateFrameTickable, ISlowTickable, IRenderable, IGlobalRegistryHotSwapListener, IDisposable
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
        private const float GpuTimeReliefStartMs = 8.25f;
        private const float GpuUtilReliefStart = 0.55f;
        private const float QualityReliefStart = 0.58f;
        private const float QualityReliefEnd = 0.96f;
        private const float SecondsToMilliseconds = 1000f;
        private const float LevelDowngradeHoldSeconds = 2.5f;
        private const float GazeLossHoldSeconds = 0.75f;
        private const float MaxHysteresisDeltaSeconds = 0.25f;
        private const float ApplyEpsilon = 0.0001f;
        private const uint BlackBoxMagic = 0x46565243u; // FVRC
        private const uint BlackBoxVersion = 2u;
        private const uint SourceHash = 0x46565253u; // FVRS
        private const ushort FlagXrActive = 1 << 0;
        private const ushort FlagCapsSupported = 1 << 1;
        private const ushort FlagQuest2FloorHigh = 1 << 2;
        private const ushort FlagGazeTracked = 1 << 3;
        private const ushort FlagUiSuppressed = 1 << 4;
        private const ushort FlagFlatScreenFallback = 1 << 5;
        private const ushort FlagThermalPressure = 1 << 6;
        private const ushort FlagSystemPressure = 1 << 7;
        private const ushort FlagApplied = 1 << 8;
        private const ushort FlagNonFinite = 1 << 9;
        private const ushort FlagQualityReliefActive = 1 << 10;
        private const ushort FlagHysteresisHold = 1 << 11;
        private const ushort FlagGazeGraceHold = 1 << 12;
        private const ushort FlagQuestClassificationPending = 1 << 13;
        private const ushort FlagFreshGpuTimeEscalation = 1 << 14;
        private const string RuntimeObjectName = "[FoveatedRenderCommander]";
        private const string DumpFileName = "Dump_1406.bin";

        // COLD ALLOC: List<XRDisplaySubsystem>[8] - XR display enumeration scratch reused on policy commits - owner: FoveatedRenderCommander
        private static readonly List<XRDisplaySubsystem> s_displays = new List<XRDisplaySubsystem>(8);
        private static FoveatedRenderCommander s_activeCommander;
        private static bool s_questRuntimeClassified;
        private static bool s_quest2ClassRuntime;
        private static bool s_questFamilyClassRuntime;
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

        [SerializeField, Range(0f, LevelHigh)]
        [Tooltip("Maximum continuous Quest 2-class fixed foveation floor. Runtime pressure and global quality blend this from 0 to the configured cap.")]
        private float quest2FoveationFloor01 = LevelHigh;

        [SerializeField]
        [Tooltip("Disables gaze-tracked foveation while cameras that render UI layers are drawing. Fixed Quest foveation stays stable across camera stacks.")]
        private bool failClosedForUiCameras = true;

        [SerializeField]
        [Tooltip("Layer mask treated as text/UI. Cameras rendering this mask force foveation off for that camera.")]
        private LayerMask uiLayerMask = 1 << DefaultUiLayerIndex;

        private IDataVault _dataVault;
        private IDataVault _telemetryWriteVault;
        private VaultGenerationHandle<FoveatedRenderTelemetryEntry> _telemetryHandle;
        private IHardwareThermalService _hardwareThermal;
        private InputDevice _centerEyeDeviceCold;
        private RenderTextureDescriptor _eyeDescriptorCold;
        // COLD ALLOC: fixed fault snapshot[300] - copied before diagnostic dump write - owner: FoveatedRenderCommander
        private readonly FoveatedRenderTelemetryEntry[] _blackBoxDumpSnapshot = new FoveatedRenderTelemetryEntry[TelemetryCapacity];
        private int _telemetryCursor;
        private int _blackBoxDumpSnapshotCursor;
        private int _blackBoxDumpSnapshotCount;
        private int _blackBoxDumpInFlight;
        private uint _blackBoxDumpSnapshotHash;
        private int _framesUntilSample;
        private int _lastEyeWidth;
        private int _lastEyeHeight;
        private int _lastDisplayCount;
        private uint _sequence;
        private uint _blackBoxDumpSnapshotSequence;
        private uint _telemetryVaultGeneration;
        private float _systemStress01;
        private float _gpuUtil01;
        private float _latestGpuTimeMs;
        private float _globalQualityWeight01 = 1f;
        private float _downgradeHoldSecondsRemaining;
        private float _gazeLossHoldSecondsRemaining;
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
        private FoveatedRenderingCaps _coldFoveatedCaps;
        private ushort _lastFlags;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredHotSwap;
        private bool _registeredRenderable;
        private bool _blackBoxDumped;
        private bool _uiSuppressionActive;
        private bool _displayLevelNonFinite;
        private bool _disposed;
        private bool _coldAndroidRuntime;
        private bool _coldStandaloneLikeRuntime;
        private bool _detachRequested;
        private string _blackBoxDumpPathCold;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_displays.Clear();
            s_activeCommander = null;
            s_questRuntimeClassified = false;
            s_quest2ClassRuntime = false;
            s_questFamilyClassRuntime = false;
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
            CacheRuntimeCapabilitySnapshotCold();
            _framesUntilSample = 0;
        }

        private void OnEnable()
        {
            if (!ReferenceEquals(s_activeCommander, this))
                return;

            _disposed = false;
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            CacheRuntimeCapabilitySnapshotCold();
            TryEnsureBlackBoxDumpPathCold();
            RefreshGlobalQualityWeight01();
            EnsureTelemetry();
            _hardwareThermal = GlobalRegistry.HardwareThermal;
            TryRegisterTick();
            TryRegisterSlowTick();
            TryRegisterHotSwap();
            TryRegisterRenderable();
            ApplyPolicy(force: true);
        }

        private void Start()
        {
            if (!ReferenceEquals(s_activeCommander, this))
                return;

            TryRegisterTick();
            TryRegisterSlowTick();
            TryRegisterHotSwap();
            TryRegisterRenderable();
            CacheRuntimeCapabilitySnapshotCold();
            TryEnsureBlackBoxDumpPathCold();
            ApplyPolicy(force: true);
        }

        private void OnDisable()
        {
            bool ownsRuntimeState = ReferenceEquals(s_activeCommander, this);
            TryUnregisterRenderable();
            TryUnregisterHotSwap();
            TryUnregisterTick();
            TryUnregisterSlowTick();
            if (ownsRuntimeState)
                ClearHardwareFoveation();
            _hardwareThermal = null;
            ReleaseTelemetryBuffer();
            _telemetryCursor = 0;
            _centerEyeDeviceCold = default;
            _eyeDescriptorCold = default;
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
            TryUnregisterSlowTick();
            if (ownsRuntimeState)
                ClearHardwareFoveation();
            ReleaseTelemetryBuffer();
            _telemetryVaultGeneration = 0u;
            _dataVault = null;
            _centerEyeDeviceCold = default;
            _eyeDescriptorCold = default;
        }

        public void LateFrameTick()
        {
            if (TryQueueDetachIfInactiveCommander())
                return;

            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
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

        public void SlowTick()
        {
            if (TryDetachIfInactiveCommander())
                return;

            if (!HasBlackBoxDumpPathCold() || !HasTelemetryReady())
                return;
        }

        public void Render(float deltaTime)
        {
            if (!ReferenceEquals(s_activeCommander, this) || !failClosedForUiCameras)
                return;

            if (_targetMode != FoveatedRenderMode.GazeTracked)
            {
                if (_uiSuppressionActive)
                {
                    _uiSuppressionActive = false;
                    _lastFlags = (ushort)(_lastFlags & ~FlagUiSuppressed);
                }

                return;
            }

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
                {
                    TryUnregisterTick();
                    TryUnregisterSlowTick();
                }
                else
                {
                    TryRegisterTick();
                    TryRegisterSlowTick();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Scene)
            {
                if (currentService != null)
                {
                    TryRegisterTick();
                    TryRegisterSlowTick();
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
                RebindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);
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
            RenderTextureDescriptor eyeDescriptor = _eyeDescriptorCold;
            bool xrActive = HectonXRRuntimeState.IsXRActive;
            FoveatedRenderingCaps caps = _coldFoveatedCaps;
            bool capsSupported = caps != FoveatedRenderingCaps.None;
            RefreshQuestRuntimeClass(
                xrActive,
                out bool quest2Runtime,
                out bool questFamilyRuntime,
                out bool questClassificationPending);
            float qualityWeight01 = RefreshGlobalQualityWeight01();
            bool thermalPressure =
                _thermalSeverity >= (byte)HardwareThermalSeverity.Throttling ||
                _gpuUtil01 >= GpuPressureHighThreshold ||
                IsGpuTimePressureActive(_latestGpuTimeMs);
            bool systemPressure = _systemStress01 >= StressMediumThreshold || _pressureLevel >= 2 || _foveatedPressureTier >= 2;
            float policyPressure01 = ResolvePolicyPressure01(
                _systemStress01,
                _gpuUtil01,
                _latestGpuTimeMs,
                _pressureLevel,
                _foveatedPressureTier,
                _thermalSeverity);
            float quest2Floor01 = ResolveQuest2FoveationFloor01(quest2Runtime, qualityWeight01, policyPressure01);
            bool quest2FloorActive = quest2Floor01 > ApplyEpsilon;
            bool quest2HighFloorActive = quest2Floor01 >= LevelHigh - ApplyEpsilon;
            ushort flags = 0;

            if (xrActive)
                flags |= FlagXrActive;
            if (capsSupported)
                flags |= FlagCapsSupported;
            if (quest2HighFloorActive)
                flags |= FlagQuest2FloorHigh;
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
                thermalPressure);
            byte levelCode = ApplyTargetLevelHysteresis(
                requestedLevelCode,
                thermalPressure || systemPressure || quest2FloorActive,
                out bool hysteresisHeld);
            if (hysteresisHeld)
                flags |= FlagHysteresisHold;
            float targetLevel = QuestFoveationDriver.ResolveTargetLevel01(
                qualityWeight01,
                policyPressure01,
                levelCode,
                false,
                questFamilyRuntime);
            targetLevel = math.max(targetLevel, quest2Floor01);
            float qualityRelief01 = ResolveQualityRelief01(
                qualityWeight01,
                policyPressure01,
                false);
            if (qualityRelief01 > ApplyEpsilon)
                flags |= FlagQualityReliefActive;
            bool gazeTracked = ShouldUseGazeTrackedVrs(xrActive, caps, quest2Runtime, out bool gazeGraceHeld);
            XRDisplaySubsystem.FoveatedRenderingFlags targetFlags = gazeTracked
                ? XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed
                : XRDisplaySubsystem.FoveatedRenderingFlags.None;
            FoveatedRenderMode mode = gazeTracked ? FoveatedRenderMode.GazeTracked : FoveatedRenderMode.Fixed;
            if (gazeTracked)
                flags |= FlagGazeTracked;
            if (gazeGraceHeld)
                flags |= FlagGazeGraceHold;

            if (!gazeTracked && targetLevel <= ApplyEpsilon)
            {
                levelCode = 0;
                targetLevel = 0f;
                mode = FoveatedRenderMode.Disabled;
                _downgradeHoldSecondsRemaining = 0f;
                _gazeLossHoldSecondsRemaining = 0f;
                flags = (ushort)(flags & ~FlagHysteresisHold);
                flags = (ushort)(flags & ~FlagGazeGraceHold);
            }

            _targetLevelCode = levelCode;
            _targetLevel01 = targetLevel;
            _targetMode = mode;
            _targetFlags = targetFlags;

            bool applied = ApplyDisplayState(targetLevel, targetFlags, mode, force, out float appliedLevel, out int displayCount);
            if (!thermalPressure && IsGpuTimePressureActive(_latestGpuTimeMs))
            {
                thermalPressure = true;
                flags = (ushort)(flags & ~(FlagApplied | FlagNonFinite | FlagQualityReliefActive | FlagHysteresisHold));
                flags |= FlagThermalPressure;
                flags |= FlagFreshGpuTimeEscalation;

                levelCode = ApplyTargetLevelHysteresis(
                    ResolveTargetLevelCode(
                        _systemStress01,
                        _pressureLevel,
                        _foveatedPressureTier,
                        true),
                    true,
                    out bool gpuTimeHysteresisHeld);
                if (gpuTimeHysteresisHeld)
                    flags |= FlagHysteresisHold;

                targetLevel = QuestFoveationDriver.ResolveTargetLevel01(
                    qualityWeight01,
                    1f,
                    levelCode,
                    false,
                    questFamilyRuntime);
                targetLevel = math.max(targetLevel, ResolveQuest2FoveationFloor01(quest2Runtime, qualityWeight01, 1f));
                qualityRelief01 = ResolveQualityRelief01(
                    qualityWeight01,
                    1f,
                    false);
                if (qualityRelief01 > ApplyEpsilon)
                    flags |= FlagQualityReliefActive;
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

            bool applied = QuestFoveationDriver.TryApplyUnityXrFoveation(
                s_displays,
                targetLevel,
                targetFlags,
                force,
                _appliedLevel01,
                _appliedFlags,
                _appliedMode == mode,
                ApplyEpsilon,
                out QuestFoveationDriver.ApplyResult result);

            appliedLevel = result.AppliedLevel01;
            displayCount = result.DisplayCount;
            _latestGpuTimeMs = result.GpuTimeSampled != 0 ? result.SampledGpuTimeMs : 0f;
            _displayLevelNonFinite = result.NonFiniteLevelDetected != 0;
            _appliedLevel01 = displayCount > 0 ? appliedLevel : 0f;
            _appliedLevelCode = ResolveAppliedLevelCode(_appliedLevel01);
            _appliedFlags = targetFlags;
            _appliedMode = displayCount > 0 ? mode : FoveatedRenderMode.Disabled;
            return applied;
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
            if (!IsInactiveCommander())
                return false;

            TryUnregisterRenderable();
            TryUnregisterHotSwap();
            TryUnregisterTick();
            TryUnregisterSlowTick();
            _hardwareThermal = null;
            _detachRequested = false;
            return true;
        }

        private bool TryQueueDetachIfInactiveCommander()
        {
            if (!IsInactiveCommander())
                return false;

            _detachRequested = true;
            return true;
        }

        private bool IsInactiveCommander()
        {
            return !ReferenceEquals(s_activeCommander, this) || _disposed || _detachRequested;
        }

        private void TryRegisterTick()
        {
            if (_registeredLateFrame)
                return;

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                _registeredLateFrame = false;
                return;
            }

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick)
                return;

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                _registeredSlowTick = false;
                return;
            }

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrame = false;
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
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

            if (_registeredRenderable)
                return;

            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void TryUnregisterRenderable()
        {
            if (!_registeredRenderable)
                return;

            GlobalRegistry.Renderables.TryUnregister(this);
            _registeredRenderable = false;
            _uiSuppressionActive = false;
            _lastFlags = (ushort)(_lastFlags & ~FlagUiSuppressed);
        }

        private void RebindDataVaultForLifecycle(IDataVault currentVault, IDataVault releaseVaultOverride = null)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            ReleaseTelemetryWriteBuffer();
            ReleaseVaultBuffer(_dataVault ?? releaseVaultOverride, ref _telemetryHandle);
            _dataVault = currentVault;
            _telemetryCursor = 0;
            _telemetryVaultGeneration = 0u;
            _telemetryWriteVault = null;
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
                vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<FoveatedRenderTelemetryEntry>.ReadOnly currentTelemetry) &&
                !vault.IsCompactionFenceActive &&
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
                IsOwnedVaultHandle(in existing) &&
                vault.TryReadOnlyHandle(in existing, out NativeArray<FoveatedRenderTelemetryEntry>.ReadOnly existingTelemetry) &&
                !vault.IsCompactionFenceActive &&
                existingTelemetry.IsCreated &&
                existingTelemetry.Length >= TelemetryCapacity)
            {
                _telemetryHandle = existing;
                _telemetryVaultGeneration = existing.Generation;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<FoveatedRenderTelemetryEntry> acquired = vault.EnsureGenerationHandle<FoveatedRenderTelemetryEntry>(
                BufferID.FoveatedRenderBlackBox,
                TelemetryCapacity,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryReadOnlyHandle(in acquired, out NativeArray<FoveatedRenderTelemetryEntry>.ReadOnly acquiredTelemetry) ||
                vault.IsCompactionFenceActive ||
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

        private bool HasTelemetryReady()
        {
            if (!s_telemetryLayoutChecked || !s_telemetryLayoutValid)
                return false;

            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsVaultHandleCreated(in _telemetryHandle) &&
                   _telemetryVaultGeneration == _telemetryHandle.Generation &&
                   vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<FoveatedRenderTelemetryEntry>.ReadOnly telemetry) &&
                   telemetry.IsCreated &&
                   telemetry.Length >= TelemetryCapacity;
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

            bool shouldDump = false;
            if (!TryAcquireTelemetryWriteBuffer(out NativeArray<FoveatedRenderTelemetryEntry> telemetry))
                return;

            try
            {
                FoveatedRenderTelemetryEntry entry = default;
                entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                entry.Sequence = _sequence++;
                entry.TargetLevel01 = _targetLevel01;
                entry.AppliedLevel01 = math.max(0f, _appliedLevel01);
                entry.SystemStress01 = _systemStress01;
                entry.GpuUtil01 = _gpuUtil01;
                entry.GpuTimeMs = _latestGpuTimeMs;
                entry.EyeWidth = _lastEyeWidth;
                entry.EyeHeight = _lastEyeHeight;
                entry.Flags = writeFlags;
                entry.Caps = unchecked((uint)_lastCaps);
                entry.TargetLevelCode = _targetLevelCode;
                entry.AppliedLevelCode = _appliedLevelCode;
                entry.Mode = (byte)_appliedMode;
                entry.PressureLevel = _pressureLevel;
                entry.FoveatedPressureTier = _foveatedPressureTier;
                entry.ThermalSeverity = _thermalSeverity;
                entry.DisplayCount = (ushort)math.clamp(_lastDisplayCount, 0, ushort.MaxValue);
                entry.VaultGeneration = _telemetryVaultGeneration;
                telemetry[_telemetryCursor] = entry;

                _telemetryCursor++;
                if (_telemetryCursor >= TelemetryCapacity)
                    _telemetryCursor = 0;

                shouldDump = nonFinite;
            }
            finally
            {
                ReleaseTelemetryWriteBuffer();
            }

            if (shouldDump)
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
            if (_blackBoxDumped || !HasTelemetryReady())
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _blackBoxDumpInFlight, 1, 0) != 0)
                return;

            int telemetryCursor = _telemetryCursor;
            uint sequence = _sequence;
            try
            {
                if (!TryStageBlackBoxDumpSnapshot(telemetryCursor, sequence))
                {
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SourceHash));
                    return;
                }

                if (TryWriteBlackBoxSnapshotCold())
                    _blackBoxDumped = true;
                else
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SourceHash));
            }
            finally
            {
                Interlocked.Exchange(ref _blackBoxDumpInFlight, 0);
            }
        }

        private bool TryStageBlackBoxDumpSnapshot(int telemetryCursor, uint sequence)
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || !IsVaultHandleCreated(in _telemetryHandle))
                return false;

            if (!vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<FoveatedRenderTelemetryEntry>.ReadOnly telemetry) ||
                vault.IsCompactionFenceActive ||
                !telemetry.IsCreated ||
                telemetry.Length < TelemetryCapacity)
            {
                return false;
            }

            if ((uint)telemetryCursor >= TelemetryCapacity)
                telemetryCursor = 0;

            for (int i = 0; i < TelemetryCapacity; i++)
            {
                int index = telemetryCursor + i;
                if (index >= TelemetryCapacity)
                    index -= TelemetryCapacity;

                _blackBoxDumpSnapshot[i] = telemetry[index];
            }

            _blackBoxDumpSnapshotCursor = telemetryCursor;
            _blackBoxDumpSnapshotSequence = sequence;
            _blackBoxDumpSnapshotCount = TelemetryCapacity;
            return !vault.IsCompactionFenceActive;
        }

        private unsafe bool TryWriteBlackBoxSnapshotCold()
        {
            int count = _blackBoxDumpSnapshotCount;
            if (count <= 0)
                return false;
            if (count > TelemetryCapacity)
                count = TelemetryCapacity;

            if (!HasBlackBoxDumpPathCold() &&
                !TryEnsureBlackBoxDumpPathCold())
            {
                return false;
            }

            int byteCount = 24 + (count * TelemetryRecordSizeBytes);
            NativeArray<byte> payload = default;
            try
            {
                // Fault-only native staging: one contiguous payload preserves the existing FVRC header + 64-byte row schema.
                const string dumpPayloadLabel = "FoveatedRenderCommanderBlackBoxDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(FoveatedRenderCommander),
                    dumpPayloadLabel,
                    allocator: Allocator.TempJob);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> header = new Span<byte>(payloadPtr, 24);
                WriteTelemetryDumpHeader(header, _blackBoxDumpSnapshotCursor, _blackBoxDumpSnapshotSequence);

                uint hash = SourceHash ^ (uint)count ^ (uint)_blackBoxDumpSnapshotCursor ^ _blackBoxDumpSnapshotSequence;
                for (int i = 0; i < header.Length; i++)
                    hash = (hash * 16777619u) ^ header[i];

                int offset = 24;
                for (int i = 0; i < count; i++)
                {
                    Span<byte> entryBytes = new Span<byte>(payloadPtr + offset, TelemetryRecordSizeBytes);
                    WriteTelemetryEntry(entryBytes, in _blackBoxDumpSnapshot[i]);
                    for (int byteIndex = 0; byteIndex < entryBytes.Length; byteIndex++)
                        hash = (hash * 16777619u) ^ entryBytes[byteIndex];
                    offset += TelemetryRecordSizeBytes;
                }

                _blackBoxDumpSnapshotHash = hash;
                return NativeFaultDumpWriter.TryWriteAll(_blackBoxDumpPathCold, payload, byteCount);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
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
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            finally
            {
                const string dumpPayloadLabel = "FoveatedRenderCommanderBlackBoxDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FoveatedRenderCommander),
                    dumpPayloadLabel,
                    Allocator.TempJob);
            }
        }

        private static void WriteTelemetryDumpHeader(Span<byte> destination, int telemetryCursor, uint sequence)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), BlackBoxMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), BlackBoxVersion);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(8, 4), TelemetryCapacity);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12, 4), TelemetryRecordSizeBytes);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(16, 4), telemetryCursor);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(20, 4), sequence);
        }

        private static void WriteTelemetryEntry(Span<byte> destination, in FoveatedRenderTelemetryEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Sequence);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.TargetLevel01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.AppliedLevel01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.SystemStress01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.GpuUtil01);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.GpuTimeMs);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(28, 4), entry.EyeWidth);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(32, 4), entry.EyeHeight);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), entry.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(40, 4), entry.Caps);
            destination[44] = entry.TargetLevelCode;
            destination[45] = entry.AppliedLevelCode;
            destination[46] = entry.Mode;
            destination[47] = entry.PressureLevel;
            destination[48] = entry.FoveatedPressureTier;
            destination[49] = entry.ThermalSeverity;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(50, 2), entry.DisplayCount);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(52, 4), entry.VaultGeneration);
            destination.Slice(56, 8).Clear();
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private static bool TryGetProjectDumpPath(out string path)
        {
            path = "Docs/AgentLogs/" + DumpFileName;
            return true;
        }

        private bool TryEnsureBlackBoxDumpPathCold()
        {
            if (TryGetProjectDumpPath(out string path))
            {
                _blackBoxDumpPathCold = path;
                return true;
            }

            _blackBoxDumpPathCold = null;
            return false;
        }

        private bool HasBlackBoxDumpPathCold()
        {
            return !string.IsNullOrEmpty(_blackBoxDumpPathCold);
        }

        private bool TryAcquireTelemetryWriteBuffer(out NativeArray<FoveatedRenderTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (!HasTelemetryReady())
                return false;
            if (_telemetryWriteVault != null)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultHandleCreated(in _telemetryHandle) ||
                !vault.TryAcquireWriteLock(in _telemetryHandle, SystemID.GraphicsScalability, out telemetry))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (!vault.IsCompactionFenceActive && telemetry.IsCreated && telemetry.Length >= TelemetryCapacity)
                {
                    _telemetryVaultGeneration = _telemetryHandle.Generation;
                    _telemetryWriteVault = vault;
                    releaseOnExit = false;
                    return true;
                }

                telemetry = default;
                return false;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in _telemetryHandle, SystemID.GraphicsScalability);
            }
        }

        private void ReleaseTelemetryWriteBuffer()
        {
            IDataVault vault = _telemetryWriteVault;
            _telemetryWriteVault = null;
            if (vault != null && IsVaultHandleCreated(in _telemetryHandle))
                vault.ReleaseWriteLock(in _telemetryHandle, SystemID.GraphicsScalability);
        }

        private void ClearTelemetryDescriptor()
        {
            ReleaseTelemetryWriteBuffer();
            _telemetryHandle = default;
            _telemetryVaultGeneration = 0u;
            _telemetryWriteVault = null;
        }

        private void ReleaseTelemetryBuffer()
        {
            ReleaseTelemetryWriteBuffer();
            ReleaseVaultBuffer(_dataVault, ref _telemetryHandle);
            _telemetryVaultGeneration = 0u;
            _telemetryWriteVault = null;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID == (uint)BufferID.FoveatedRenderBlackBox &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsOwnedVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static byte ResolveTargetLevelCode(
            float systemStress01,
            byte pressureLevel,
            byte foveatedPressureTier,
            bool thermalPressure)
        {
            if (thermalPressure ||
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

        private float RefreshGlobalQualityWeight01()
        {
            float value = HomeostasisBrain.GlobalQualityWeight;
            _globalQualityWeight01 = math.saturate(math.select(_globalQualityWeight01, value, math.isfinite(value)));
            return _globalQualityWeight01;
        }

        private void CacheRuntimeCapabilitySnapshotCold()
        {
            _coldFoveatedCaps = SystemInfo.foveatedRenderingCaps;
            _eyeDescriptorCold = HectonXRManager.RefreshEyeDescriptor();
            _coldAndroidRuntime = Application.platform == RuntimePlatform.Android;
            _coldStandaloneLikeRuntime = ResolveStandaloneLikeRuntimeCold(Application.platform);
            _centerEyeDeviceCold = _coldStandaloneLikeRuntime
                ? InputDevices.GetDeviceAtXRNode(XRNode.CenterEye)
                : default;
            if (_coldAndroidRuntime)
                EnsureQuestRuntimeClassification();
        }

        private float ResolveQuest2FoveationFloor01(bool quest2Runtime, float qualityWeight01, float policyPressure01)
        {
            if (!quest2Runtime)
                return 0f;

            float quality = Sanitize01(qualityWeight01);
            float pressure = Sanitize01(policyPressure01);
            float survivalNeed01 = Smooth01(math.max(pressure, 1f - quality));
            return math.saturate(quest2FoveationFloor01 * survivalNeed01);
        }

        private static float ResolvePolicyPressure01(
            float systemStress01,
            float gpuUtil01,
            float gpuTimeMs,
            byte pressureLevel,
            byte foveatedPressureTier,
            byte thermalSeverity)
        {
            float stressPressure = math.smoothstep(StressMediumThreshold, StressHighThreshold, Sanitize01(systemStress01));
            float systemPressure = math.saturate(math.max((int)pressureLevel, (int)foveatedPressureTier) * (1f / 3f));
            float gpuPressure = math.smoothstep(GpuUtilReliefStart, GpuPressureHighThreshold, Sanitize01(gpuUtil01));
            float gpuTimePressure = math.smoothstep(GpuTimeReliefStartMs, GpuTimeHighPressureMs, math.max(0f, gpuTimeMs));
            float thermalPressure = math.saturate(((float)thermalSeverity - (float)HardwareThermalSeverity.Warm) * 0.5f);
            return math.saturate(math.max(math.max(stressPressure, systemPressure), math.max(math.max(gpuPressure, gpuTimePressure), thermalPressure)));
        }

        private static float ResolveQualityRelief01(float qualityWeight01, float policyPressure01, bool lockedHighFoveation)
        {
            return QuestFoveationDriver.ResolveQualityRelief01(qualityWeight01, policyPressure01, lockedHighFoveation);
        }

        private static float Smooth01(float value)
        {
            float t = Sanitize01(value);
            return t * t * (3f - 2f * t);
        }

        private static bool HasEyeTrackedGaze(
            bool xrActive,
            FoveatedRenderingCaps caps,
            bool questRuntime,
            bool standaloneLikeRuntime,
            InputDevice centerEyeDevice)
        {
            if (!xrActive || questRuntime || !standaloneLikeRuntime)
                return false;

            if (!HasCap(caps, FoveatedRenderingCaps.FoveationImage) &&
                !HasCap(caps, FoveatedRenderingCaps.NonUniformRaster))
            {
                return false;
            }

            if (!centerEyeDevice.isValid || !centerEyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
                return false;

            return eyes.TryGetFixationPoint(out Vector3 fixationPoint) && IsFiniteVector(fixationPoint);
        }

        private bool ShouldUseGazeTrackedVrs(bool xrActive, FoveatedRenderingCaps caps, bool questRuntime, out bool graceHeld)
        {
            graceHeld = false;
            bool gazeEligible =
                xrActive &&
                _coldStandaloneLikeRuntime &&
                (HasCap(caps, FoveatedRenderingCaps.FoveationImage) ||
                 HasCap(caps, FoveatedRenderingCaps.NonUniformRaster));
            if (!allowPcVrGazeTrackedVrs || questRuntime || !gazeEligible)
            {
                _gazeLossHoldSecondsRemaining = 0f;
                return false;
            }

            if (HasEyeTrackedGaze(xrActive, caps, questRuntime, _coldStandaloneLikeRuntime, _centerEyeDeviceCold))
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

        private void RefreshQuestRuntimeClass(
            bool xrActive,
            out bool quest2Runtime,
            out bool questFamilyRuntime,
            out bool classificationPending)
        {
            classificationPending = false;
            quest2Runtime = false;
            questFamilyRuntime = false;
            if (!xrActive || !_coldAndroidRuntime)
                return;

            classificationPending = !s_questRuntimeClassified;
            quest2Runtime = s_quest2ClassRuntime;
            questFamilyRuntime = s_questFamilyClassRuntime;
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
            bool questFamilyDevice = HasQuestFamilyDeviceToken();
            s_questFamilyClassRuntime = questFamilyDevice || quest3OrPro || quest2Token;
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

            if (questFamilyDevice)
            {
                s_quest2ClassRuntime = false;
                s_questRuntimeClassified = true;
                return;
            }

            if (hasLoadedDeviceName && !questFamilyDevice)
            {
                s_questFamilyClassRuntime = false;
                s_quest2ClassRuntime = false;
                s_questRuntimeClassified = true;
            }
        }

        private static bool HasQuestFamilyDeviceToken()
        {
            return ContainsToken(SystemInfo.deviceModel, "Quest") ||
                   ContainsToken(SystemInfo.deviceName, "Quest") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Quest") ||
                   ContainsToken(SystemInfo.deviceModel, "Oculus") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Oculus") ||
                   ContainsToken(SystemInfo.deviceModel, "Meta") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Meta");
        }

        private static bool ResolveStandaloneLikeRuntimeCold(RuntimePlatform platform)
        {
            switch (platform)
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
