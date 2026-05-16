using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Hecton8.Graphics.VR
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9946)]
    [AddComponentMenu("Hecton8/Graphics/VR/Foveated Render Commander")]
    internal sealed unsafe class FoveatedRenderCommander : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int TelemetryCapacity = 300;
        private const int DefaultSampleIntervalFrames = 30;
        private const int DefaultUiLayerIndex = 5;
        private const float LevelLow = 0.35f;
        private const float LevelMedium = 0.62f;
        private const float LevelHigh = 0.85f;
        private const float StressMediumThreshold = 0.35f;
        private const float StressHighThreshold = 0.70f;
        private const float GpuPressureHighThreshold = 0.78f;
        private const float GpuTimeHighPressureMs = 10.75f;
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
        private const string RuntimeObjectName = "[FoveatedRenderCommander]";
        private const string DumpFileName = "Dump_FOVEATED_RENDER_COMMANDER.bin";

        // COLD ALLOC: List<XRDisplaySubsystem>[4] — XR display enumeration scratch reused on policy commits — owner: FoveatedRenderCommander
        private static readonly List<XRDisplaySubsystem> s_displays = new List<XRDisplaySubsystem>(4);
        private static FoveatedRenderCommander s_activeCommander;

        [Header("Policy")]
        [SerializeField, Range(1, 240)]
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
        private VaultBufferHandle<FoveatedRenderTelemetryEntry> _telemetryHandle;
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
        private bool _registeredRenderCallbacks;
        private bool _blackBoxDumped;
        private int _uiSuppressionDepth;
        private bool _displayLevelNonFinite;
        private bool _disposed;

        private enum FoveatedRenderMode : byte
        {
            Disabled = 0,
            Fixed = 1,
            GazeTracked = 2,
            UiExempted = 3
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
        private struct FoveatedRenderTelemetryEntry
        {
            public uint Frame;
            public uint Sequence;
            public float TargetLevel01;
            public float AppliedLevel01;
            public float SystemStress01;
            public float GpuUtil01;
            public float GpuTimeMs;
            public int EyeWidth;
            public int EyeHeight;
            public uint Flags;
            public uint Caps;
            public byte TargetLevelCode;
            public byte AppliedLevelCode;
            public byte Mode;
            public byte PressureLevel;
            public byte FoveatedPressureTier;
            public byte ThermalSeverity;
            public ushort DisplayCount;
            public uint VaultGeneration;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_displays.Clear();
            s_activeCommander = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying || s_activeCommander != null)
                return;

            GameObject host = new GameObject(RuntimeObjectName); // COLD ALLOC: GameObject[1] — runtime foveated rendering commander host — owner: FoveatedRenderCommander
            DontDestroyOnLoad(host);
            host.AddComponent<FoveatedRenderCommander>(); // COLD ALLOC: FoveatedRenderCommander[1] — runtime foveated rendering policy owner — owner: FoveatedRenderCommander
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
            EnsureTelemetry();
            _hardwareThermal = GlobalRegistry.HardwareThermal;
            TryRegisterTick();
            TryRegisterHotSwap();
            TryRegisterRenderCallbacks();
            ApplyPolicy(force: true);
        }

        private void Start()
        {
            if (!ReferenceEquals(s_activeCommander, this))
                return;

            TryRegisterTick();
            TryRegisterHotSwap();
            TryRegisterRenderCallbacks();
            ApplyPolicy(force: true);
        }

        private void OnDisable()
        {
            TryUnregisterRenderCallbacks();
            TryUnregisterHotSwap();
            TryUnregisterTick();
            ClearHardwareFoveation();
            _hardwareThermal = null;
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
            TryUnregisterRenderCallbacks();
            TryUnregisterHotSwap();
            TryUnregisterTick();
            ClearHardwareFoveation();
            _telemetryHandle = default;
            _telemetryVaultGeneration = 0u;
            _dataVault = null;
        }

        public void Tick(float deltaTime)
        {
            ConsumeSignals();

            _framesUntilSample--;
            if (_framesUntilSample <= 0)
            {
                int interval = sampleIntervalFrames > 0 ? sampleIntervalFrames : DefaultSampleIntervalFrames;
                _framesUntilSample = interval;
                ApplyPolicy(force: false);
            }

            WriteTelemetry(_lastFlags);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null)
                    _registeredTick = false;
                else
                    TryRegisterTick();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.HardwareThermalService)
            {
                _hardwareThermal = currentService as IHardwareThermalService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = currentService as IDataVault;
                _telemetryHandle = default;
                _telemetryVaultGeneration = 0u;
                EnsureTelemetry();
            }
        }

        internal void RequestBlackBoxDump()
        {
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
                SystemHealthSignal signal = healthSignals[i];
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
                thermalSeverity = MaxByte(thermalSeverity, thermalSignals[i].Severity);
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
        }

        private void ApplyPolicy(bool force)
        {
            RenderTextureDescriptor eyeDescriptor = HectonXRManager.RefreshEyeDescriptor();
            bool xrActive = XRSettings.enabled && XRSettings.isDeviceActive;
            FoveatedRenderingCaps caps = SystemInfo.foveatedRenderingCaps;
            bool capsSupported = caps != FoveatedRenderingCaps.None;
            bool quest2Runtime = IsQuest2Runtime();
            bool thermalPressure =
                _thermalSeverity >= (byte)HardwareThermalSeverity.Throttling ||
                _gpuUtil01 >= GpuPressureHighThreshold ||
                _latestGpuTimeMs >= GpuTimeHighPressureMs;
            bool systemPressure = _systemStress01 >= StressMediumThreshold || _pressureLevel >= 2 || _foveatedPressureTier >= 2;
            ushort flags = 0;

            if (xrActive)
                flags |= FlagXrActive;
            if (capsSupported)
                flags |= FlagCapsSupported;
            if (quest2Runtime && lockQuest2HighFoveation)
                flags |= FlagQuest2LockedHigh;
            if (thermalPressure)
                flags |= FlagThermalPressure;
            if (systemPressure)
                flags |= FlagSystemPressure;

            if (!xrActive && !allowFlatScreenFoveation)
            {
                flags |= FlagFlatScreenFallback;
                ResolveDisabledTarget();
                ApplyDisplayState(0f, XRDisplaySubsystem.FoveatedRenderingFlags.None, FoveatedRenderMode.Disabled, force, out _, out int displayCount);
                _lastDisplayCount = displayCount;
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
                ApplyDisplayState(0f, XRDisplaySubsystem.FoveatedRenderingFlags.None, FoveatedRenderMode.Disabled, force, out _, out int displayCount);
                _lastDisplayCount = displayCount;
                ReportHardwareState(false, 0f, eyeDescriptor);
                _lastFlags = flags;
                _lastCaps = caps;
                _lastEyeWidth = eyeDescriptor.width;
                _lastEyeHeight = eyeDescriptor.height;
                return;
            }

            byte levelCode = ResolveTargetLevelCode(_systemStress01, quest2Runtime, thermalPressure);
            float targetLevel = ResolveLevel01(levelCode);
            bool gazeTracked = ShouldUseGazeTrackedVrs(xrActive, caps, quest2Runtime);
            XRDisplaySubsystem.FoveatedRenderingFlags targetFlags = gazeTracked
                ? XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed
                : XRDisplaySubsystem.FoveatedRenderingFlags.None;
            FoveatedRenderMode mode = gazeTracked ? FoveatedRenderMode.GazeTracked : FoveatedRenderMode.Fixed;
            if (gazeTracked)
                flags |= FlagGazeTracked;

            _targetLevelCode = levelCode;
            _targetLevel01 = targetLevel;
            _targetMode = mode;
            _targetFlags = targetFlags;

            bool applied = ApplyDisplayState(targetLevel, targetFlags, mode, force, out float appliedLevel, out int displayCount);
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
                if (display.TryGetAppGPUTimeLastFrame(out float gpuMs) && math.isfinite(gpuMs))
                {
                    sampledGpuTimeMs = math.max(sampledGpuTimeMs, gpuMs);
                    gpuTimeSampled = true;
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
        }

        private void ReportHardwareState(bool applied, float appliedLevel, RenderTextureDescriptor eyeDescriptor)
        {
            HectonXRRuntimeState.ReportHardwareFoveationState(
                applied,
                applied ? appliedLevel : 0f,
                eyeDescriptor.width,
                eyeDescriptor.height);
        }

        private void TryRegisterRenderCallbacks()
        {
            if (_registeredRenderCallbacks)
                return;

            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
            _registeredRenderCallbacks = true;
        }

        private void TryUnregisterRenderCallbacks()
        {
            if (!_registeredRenderCallbacks)
                return;

            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            _registeredRenderCallbacks = false;
            _uiSuppressionDepth = 0;
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderCamera)
        {
            if (!failClosedForUiCameras || renderCamera == null || _targetLevel01 <= ApplyEpsilon)
                return;

            if ((renderCamera.cullingMask & uiLayerMask.value) == 0)
                return;

            _uiSuppressionDepth++;
            if (_uiSuppressionDepth > 1)
                return;

            ushort flags = (ushort)(_lastFlags | FlagUiSuppressed);
            _lastFlags = flags;
            ApplyDisplayState(0f, XRDisplaySubsystem.FoveatedRenderingFlags.None, FoveatedRenderMode.UiExempted, true, out _, out _);
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera renderCamera)
        {
            if (!failClosedForUiCameras || renderCamera == null || (renderCamera.cullingMask & uiLayerMask.value) == 0)
                return;

            if (_uiSuppressionDepth <= 0)
                return;

            _uiSuppressionDepth--;
            if (_uiSuppressionDepth > 0)
                return;

            if (_targetLevel01 <= ApplyEpsilon)
                return;

            ApplyDisplayState(_targetLevel01, _targetFlags, _targetMode, true, out _, out _);
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private bool EnsureTelemetry()
        {
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
            {
                _telemetryHandle = default;
                _telemetryVaultGeneration = 0u;
                return false;
            }

            _dataVault = vault;
            if (_telemetryHandle.IsCreated && vault.ResolveBuffer(ref _telemetryHandle))
            {
                vault.TryGetBufferGeneration(BufferID.FoveatedRenderBlackBox, out _telemetryVaultGeneration);
                return _telemetryHandle.Length >= TelemetryCapacity;
            }

            _telemetryHandle = vault.GetBufferHandle<FoveatedRenderTelemetryEntry>(
                BufferID.FoveatedRenderBlackBox,
                TelemetryCapacity,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!_telemetryHandle.IsCreated)
            {
                _telemetryVaultGeneration = 0u;
                return false;
            }

            vault.TryGetBufferGeneration(BufferID.FoveatedRenderBlackBox, out _telemetryVaultGeneration);
            return _telemetryHandle.Length >= TelemetryCapacity;
        }

        private void WriteTelemetry(ushort flags)
        {
            if (!TryResolveTelemetryPointer(out FoveatedRenderTelemetryEntry* telemetry))
                return;

            bool nonFinite =
                !math.isfinite(_targetLevel01) ||
                !math.isfinite(_appliedLevel01) ||
                !math.isfinite(_systemStress01) ||
                !math.isfinite(_gpuUtil01) ||
                !math.isfinite(_latestGpuTimeMs) ||
                _displayLevelNonFinite;

            ushort writeFlags = nonFinite ? (ushort)(flags | FlagNonFinite) : flags;
            if (_uiSuppressionDepth > 0)
                writeFlags |= FlagUiSuppressed;

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
            if (_blackBoxDumped || !TryResolveTelemetryPointer(out FoveatedRenderTelemetryEntry* telemetry))
                return;

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                string path = Path.Combine(logDirectory, DumpFileName);

                using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(BlackBoxMagic);
                writer.Write(BlackBoxVersion);
                writer.Write(TelemetryCapacity);
                writer.Write(Marshal.SizeOf<FoveatedRenderTelemetryEntry>());
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
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SourceHash));
            }
        }

        private bool TryResolveTelemetryPointer(out FoveatedRenderTelemetryEntry* telemetry)
        {
            telemetry = null;
            if (!EnsureTelemetry())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            void* pointer = _telemetryHandle.ResolvePointer(vault);
            if (pointer == null || _telemetryHandle.Length < TelemetryCapacity)
                return false;

            vault.TryGetBufferGeneration(BufferID.FoveatedRenderBlackBox, out _telemetryVaultGeneration);
            telemetry = (FoveatedRenderTelemetryEntry*)pointer;
            return true;
        }

        private static byte ResolveTargetLevelCode(float systemStress01, bool quest2Runtime, bool thermalPressure)
        {
            if (quest2Runtime)
                return 3;

            if (thermalPressure)
                return 3;

            float stress = Sanitize01(systemStress01);

            if (stress >= StressHighThreshold)
                return 3;

            if (stress >= StressMediumThreshold)
                return 2;

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

        private bool ShouldUseGazeTrackedVrs(bool xrActive, FoveatedRenderingCaps caps, bool questRuntime)
        {
            return allowPcVrGazeTrackedVrs && HasEyeTrackedGaze(xrActive, caps, questRuntime);
        }

        private static bool IsQuest2Runtime()
        {
            bool xrActive = HectonXRRuntimeState.IsXRActive || XRSettings.enabled || XRSettings.isDeviceActive;
            if (!xrActive || Application.platform != RuntimePlatform.Android)
                return false;

            if (QuestVulkanRuntimePolicy.SystemMemoryMegabytes > 0 &&
                QuestVulkanRuntimePolicy.SystemMemoryMegabytes < QuestVulkanRuntimePolicy.QuestMemoryGateMegabytes &&
                IsQuestFamilyDevice())
            {
                return true;
            }

            return ContainsToken(SystemInfo.deviceModel, "Quest 2") ||
                   ContainsToken(SystemInfo.deviceName, "Quest 2") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Quest 2") ||
                   ContainsToken(SystemInfo.deviceModel, "Oculus Quest") ||
                   ContainsToken(XRSettings.loadedDeviceName, "Oculus Quest");
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

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static byte MaxByte(byte a, byte b)
        {
            return a > b ? a : b;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
