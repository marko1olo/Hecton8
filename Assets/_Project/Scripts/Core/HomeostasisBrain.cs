using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public enum HardwareMetricSlot : int
    {
        FpsEwma = 0,
        JitterSigma = 1,
        CpuTempC = 2,
        GpuUtil01 = 3,
        BatteryLife01 = 4,
        VramPressure01 = 5,
        Count = 6
    }

    [Flags]
    public enum SystemBit : ulong
    {
        None = 0UL,
        SecondaryCaustics = 1UL << 4,
        MicroDebrisAdvection = 1UL << 5,
        ParticleAdvection = MicroDebrisAdvection,
        VolumetricFogHighRes = 1UL << 6,
        DistantFaunaSteering = 1UL << 7,
        ProceduralSway = 1UL << 8,
        HighQualityIK = 1UL << 9,
        IKBracing = HighQualityIK,
        SSR = 1UL << 10,
        BoidBrain = 1UL << 12,
        NonCriticalVfx = 1UL << 20,
        FoveatedSimulationTier3 = 1UL << 21,
        AiOneHz = 1UL << 22,
        SlowTick2Hz = AiOneHz,
        TimeDilation09 = 1UL << 23,
        TimeDilation08 = TimeDilation09,
        LowTierEmergency = 1UL << 24,
        VisualOverkill = 1UL << 25,
        VramShedding = 1UL << 26,
        CullingDistanceSqueeze = 1UL << 27,
        MathLodLow = 1UL << 28,
        GcFreeze = 1UL << 29,
        MockHeavyLoad = 1UL << 30
    }

    [Flags]
    public enum HomeostasisSignalFlags : ushort
    {
        None = 0,
        UnstableJitter = 1 << 0,
        HudWarning = 1 << 1,
        SequentialRestoration = 1 << 2,
        AndroidThermalBridge = 1 << 3,
        WindowsFallback = 1 << 4,
        LowTierBatteryWeight = 1 << 5,
        Emergency = 1 << 6,
        HardwareThermalSnapshot = 1 << 7,
        VisualOverkillBudgetOpen = 1 << 8,
        MacThermalBridge = 1 << 9,
        PlatformFallback = 1 << 10,
        XrRefreshRateShed = 1 << 11
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HomeostasisBlackBoxEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public float SystemHealthIndex01;
        [FieldOffset(8)]
        public ulong KillSwitchMask;
        [FieldOffset(16)]
        public float FpsEwma;
        [FieldOffset(20)]
        public float JitterSigmaMs;
        [FieldOffset(24)]
        public float CpuTempC;
        [FieldOffset(28)]
        public float GpuUtil01;
        [FieldOffset(32)]
        public float BatteryLife01;
        [FieldOffset(36)]
        public byte PressureLevel;
        [FieldOffset(37)]
        public byte FoveatedPressureTier;
        [FieldOffset(38)]
        public ushort Flags;
        [FieldOffset(40)]
        public float TimeDilationScalar;
        [FieldOffset(44)]
        public float PeakSystemHealthIndex01;
        [FieldOffset(48)]
        public uint LastThermalAction;
        [FieldOffset(52)]
        public uint Reserved0;
        [FieldOffset(56)]
        public uint Reserved1;
        [FieldOffset(60)]
        public uint Reserved2;
    }

    /// <summary>
    /// Pre-simulation hardware homeostasis controller. It writes numeric masks and signals only;
    /// render/gameplay systems decide how to consume each bit.
    /// </summary>
    public static partial class HomeostasisBrain
    {
        private const int FrameTimeWindow = ScalabilityContract.HomeostasisFrameTimeWindow;
        private const int BlackBoxCapacity = ScalabilityContract.HomeostasisBlackBoxCapacity;
        private const int TelemetryCadenceFrames = ScalabilityContract.HomeostasisTelemetryCadenceFrames;
        private const int RecoveryArmFrames = ScalabilityContract.HomeostasisRecoveryArmFrames;
        private const int RecoveryStepFrames = ScalabilityContract.HomeostasisRecoveryStepFrames;
        private const float FrostPollSeconds = ScalabilityContract.HomeostasisFrostPollSeconds;
        private const float FpsEwmaAlpha = ScalabilityContract.HomeostasisFpsEwmaAlpha;
        private const float ShiEwmaAlpha = ScalabilityContract.HomeostasisShiEwmaAlpha;
        private const float JitterUnstableSigmaMs = ScalabilityContract.HomeostasisJitterUnstableSigmaMs;
        private const float Level1ActivateShi = ScalabilityContract.HomeostasisLevel1ActivateShi;
        private const float Level1RestoreShi = ScalabilityContract.HomeostasisLevel1RestoreShi;
        private const float Level2ActivateShi = ScalabilityContract.HomeostasisLevel2ActivateShi;
        private const float Level2RestoreShi = ScalabilityContract.HomeostasisLevel2RestoreShi;
        private const float Level3ActivateShi = ScalabilityContract.HomeostasisLevel3ActivateShi;
        private const float Level3RestoreShi = ScalabilityContract.HomeostasisLevel3RestoreShi;
        private const float SequentialRecoveryShi = ScalabilityContract.HomeostasisSequentialRecoveryShi;
        private const long PersistentNativeBudgetBytes = ScalabilityContract.HomeostasisPersistentNativeBudgetBytes;
        private const string OwnerName = nameof(HomeostasisBrain);
        private const string BlackBoxDumpFileName = "Dump_HARDWARE_THROTTLING_DIRECTOR.bin";
        private const uint ReasonHash = 0x484F4D45u; // HOME

        private const ulong Level1Mask =
            (ulong)(SystemBit.SecondaryCaustics |
                    SystemBit.MicroDebrisAdvection);

        private const ulong Level2Mask =
            Level1Mask |
            (ulong)(SystemBit.ProceduralSway |
                    SystemBit.HighQualityIK);

        private const ulong Level3Mask =
            Level2Mask |
            (ulong)(SystemBit.DistantFaunaSteering |
                    SystemBit.SSR |
                    SystemBit.VolumetricFogHighRes |
                    SystemBit.FoveatedSimulationTier3 |
                    SystemBit.BoidBrain |
                    SystemBit.NonCriticalVfx |
                    SystemBit.AiOneHz |
                    SystemBit.TimeDilation09);

        private static IDataVault _dataVault;
        private static VaultBufferHandle<float> _globalHardwareMetricsHandle;
        private static VaultBufferHandle<float> _frameTimeMsHandle;
        private static VaultBufferHandle<HomeostasisBlackBoxEntry> _blackBoxHandle;
        private static FunctionPointer<ComputeSystemHealthIndexDelegate> _computeShi;
        private static FunctionPointer<ComputeFrameEwmaDelegate> _computeFrameEwma;
        // COLD ALLOC: ScalabilityListener[1] - cached scalability-tier bridge for PreSimulationTick - owner: HomeostasisBrain
        private static readonly ScalabilityListener s_scalabilityListener = new ScalabilityListener();
        // COLD ALLOC: DependencyHotSwapBridge[1] - cached registry dependency bridge - owner: HomeostasisBrain
        private static readonly DependencyHotSwapBridge s_dependencyHotSwapBridge = new DependencyHotSwapBridge();

        private static bool _initialized;
        private static bool _blackBoxDumped;
        private static bool _shiEwmaSeeded;
        private static bool _scalabilityListenerRegistered;
        private static bool _hotSwapRegistered;
        private static int _frameTimeCursor;
        private static int _frameTimeSampleCount;
        private static int _blackBoxCursor;
        private static int _stableRecoveryFrames;
        private static int _recoveryStepFrameCounter;
        private static int _restorationIndex;
        private static int _batteryPollCountdown;
        private static int _lastTelemetryFrame = -TelemetryCadenceFrames;
        private static float _fpsEwma;
        private static float _systemHealthIndex01;
        private static float _peakSystemHealthIndex01;
        private static float _fallbackHardwareBias;
        private static float _cachedBatteryLife01 = 1f;
        private static bool _usingHardwareSnapshot;
        private static HectonQualityTier _cachedScalabilityTier = HectonQualityTier.Unknown;
        private static IHardwareThermalService _hardwareThermalService;
        private static ulong _currentKillSwitchMask;
        private static byte _currentPressureLevel;
        private static uint _lastThermalAction;
        private static uint _frameTimeSignalSequence;

#if UNITY_OSX && !UNITY_EDITOR
        private static IntPtr _macProcessInfoClass;
        private static IntPtr _macProcessInfoSelector;
        private static IntPtr _macThermalStateSelector;
        private static IntPtr _macProcessInfo;
        private static bool _macBridgeReady;
        private static bool _macBridgeFaulted;
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate float ComputeSystemHealthIndexDelegate(
            float jitterSigmaMs,
            float cpuTempC,
            float batteryLife01,
            int lowTier);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate float ComputeFrameEwmaDelegate(
            float previousValue,
            float currentValue,
            float alpha,
            int seeded);

        public static NativeArray<float> GlobalHardwareMetrics
        {
            get
            {
                return TryResolveHardwareMetrics(out NativeArray<float> metrics) ? metrics : default;
            }
        }

        public static float SystemHealthIndex01 => _systemHealthIndex01;

        public static byte PressureLevel => _currentPressureLevel;

        public static ulong CurrentKillSwitchMask => _currentKillSwitchMask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownRuntime();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorLifecycleHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownRuntime;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownRuntime;
            UnityEditor.EditorApplication.quitting -= ShutdownRuntime;
            UnityEditor.EditorApplication.quitting += ShutdownRuntime;
        }
#endif

        public static void InitializeRuntime()
        {
            if (_initialized)
                return;

            _dataVault = GlobalRegistry.DataVault;
            if (!TryResolveRuntimeBuffers(
                    out NativeArray<float> hardwareMetrics,
                    out NativeArray<float> frameTimes,
                    out NativeArray<HomeostasisBlackBoxEntry> blackBox))
            {
                ShutdownRuntime();
                return;
            }

            MemoryBudgetTracker.Register(OwnerName, ResolveRequestedVaultBytes(), PersistentNativeBudgetBytes);

            GlobalSignals.InitializeAllQueues();

            _computeShi = BurstCompiler.CompileFunctionPointer<ComputeSystemHealthIndexDelegate>(ComputeSystemHealthIndexBurst);
            _computeFrameEwma = BurstCompiler.CompileFunctionPointer<ComputeFrameEwmaDelegate>(ComputeFrameEwmaBurst);
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
            _hardwareThermalService = GlobalRegistry.HardwareThermal;
            RegisterDependencyListeners();
            _fpsEwma = ResolveTargetFrameRate();
            hardwareMetrics[(int)HardwareMetricSlot.FpsEwma] = _fpsEwma;
            hardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = 45f;
            hardwareMetrics[(int)HardwareMetricSlot.BatteryLife01] = 1f;
            frameTimes[0] = 0f;
            blackBox[0] = default;
            _fallbackHardwareBias = ResolveFallbackHardwareBias();
            _cachedBatteryLife01 = 1f;
            _batteryPollCountdown = 0;
            _currentKillSwitchMask = 0UL;
            _currentPressureLevel = 0;
            _lastThermalAction = 0u;
            _frameTimeSignalSequence = 0u;
            _blackBoxDumped = false;
            _shiEwmaSeeded = false;
            _peakSystemHealthIndex01 = 0f;
            InitializeScalabilityDictator(hardwareMetrics, frameTimes, blackBox);

#if UNITY_OSX && !UNITY_EDITOR
            EnsureMacThermalBridge();
#endif

            _initialized = true;
        }

        public static void ShutdownRuntime()
        {
#if UNITY_OSX && !UNITY_EDITOR
            DisposeMacThermalBridge();
#endif
            UnregisterDependencyListeners();
            MemoryBudgetTracker.Unregister(OwnerName);
            _computeShi = default;
            _dataVault = null;
            _globalHardwareMetricsHandle = default;
            _frameTimeMsHandle = default;
            _blackBoxHandle = default;
            _computeFrameEwma = default;
            ShutdownScalabilityDictator();
            _initialized = false;
            _blackBoxDumped = false;
            _shiEwmaSeeded = false;
            _frameTimeCursor = 0;
            _frameTimeSampleCount = 0;
            _blackBoxCursor = 0;
            _stableRecoveryFrames = 0;
            _recoveryStepFrameCounter = 0;
            _restorationIndex = 0;
            _batteryPollCountdown = 0;
            _lastTelemetryFrame = -TelemetryCadenceFrames;
            _fpsEwma = 0f;
            _systemHealthIndex01 = 0f;
            _peakSystemHealthIndex01 = 0f;
            _fallbackHardwareBias = 0f;
            _cachedBatteryLife01 = 1f;
            _usingHardwareSnapshot = false;
            _cachedScalabilityTier = HectonQualityTier.Unknown;
            _hardwareThermalService = null;
            _currentKillSwitchMask = 0UL;
            _currentPressureLevel = 0;
            _lastThermalAction = 0u;
            _frameTimeSignalSequence = 0u;
        }

        internal static void PreSimulationTick(float unscaledDeltaTime)
        {
            InitializeRuntime();
            if (!TryResolveRuntimeBuffers(
                    out NativeArray<float> hardwareMetrics,
                    out NativeArray<float> frameTimes,
                    out NativeArray<HomeostasisBlackBoxEntry> blackBox))
                return;

            int frame = Time.frameCount;
            float targetFps = ResolveTargetFrameRate();
            float frameMs = SampleFrameMetrics(unscaledDeltaTime, targetFps, hardwareMetrics, frameTimes);
            SamplePlatformMetrics(targetFps, hardwareMetrics);
            float targetFrameMs = ResolveTargetFrameMs(targetFps);
            float vramPressure01 = SampleVramPressure01(hardwareMetrics);

            HectonQualityTier tier = _cachedScalabilityTier;
            bool lowTier = IsLowTier(tier);
            float rawShi = _computeShi.IsCreated
                ? _computeShi.Invoke(
                    hardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                    hardwareMetrics[(int)HardwareMetricSlot.CpuTempC],
                    hardwareMetrics[(int)HardwareMetricSlot.BatteryLife01],
                    lowTier ? 1 : 0)
                : ComputeSystemHealthIndexManaged(
                    hardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                    hardwareMetrics[(int)HardwareMetricSlot.CpuTempC],
                    hardwareMetrics[(int)HardwareMetricSlot.BatteryLife01],
                    lowTier);
            rawShi = ComputeDictatorRawShi(
                frame,
                rawShi,
                frameMs,
                targetFrameMs,
                vramPressure01,
                hardwareMetrics[(int)HardwareMetricSlot.CpuTempC],
                hardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                lowTier,
                hardwareMetrics);

            if (!math.isfinite(rawShi))
            {
                DumpBlackBoxOnce(blackBox);
                rawShi = 1f;
            }

            rawShi = math.saturate(rawShi);
            _systemHealthIndex01 = _shiEwmaSeeded
                ? math.lerp(_systemHealthIndex01, rawShi, ShiEwmaAlpha)
                : rawShi;
            _shiEwmaSeeded = true;
            if (!math.isfinite(_systemHealthIndex01))
            {
                DumpBlackBoxOnce(blackBox);
                _systemHealthIndex01 = 1f;
            }

            _systemHealthIndex01 = math.saturate(_systemHealthIndex01);
            _systemHealthIndex01 = ApplyHardwareShiFloor(_systemHealthIndex01);
            if (_systemHealthIndex01 > _peakSystemHealthIndex01)
                _peakSystemHealthIndex01 = _systemHealthIndex01;

            ushort flags = ApplyPressurePolicy(frame, frameMs, BuildFlags(lowTier, tier, hardwareMetrics), hardwareMetrics);
            PublishFrameTimeSignal(frame, frameMs, targetFps, flags, hardwareMetrics);
            WriteBlackBox(frame, frameMs, flags, hardwareMetrics, blackBox);
        }

        private static float SampleFrameMetrics(
            float unscaledDeltaTime,
            float targetFps,
            NativeArray<float> hardwareMetrics,
            NativeArray<float> frameTimes)
        {
            float safeDeltaTime = math.isfinite(unscaledDeltaTime) && unscaledDeltaTime > 0f
                ? unscaledDeltaTime
                : 1f / math.max(1f, targetFps);
            float frameMs = SampleStopwatchFrameMilliseconds(safeDeltaTime, targetFps);
            frameMs = ApplyMockFrameSpikeToFrameMs(frameMs);
            float currentFps = math.clamp(1000f * math.rcp(math.max(0.001f, frameMs)), 1f, 1000f);
            _fpsEwma = ComputeFrameEwma(_fpsEwma, currentFps, FpsEwmaAlpha, _fpsEwma > 0f);
            hardwareMetrics[(int)HardwareMetricSlot.FpsEwma] = math.isfinite(_fpsEwma) ? _fpsEwma : math.max(1f, targetFps);

            frameMs = math.isfinite(frameMs) ? math.max(0f, frameMs) : 1000f * math.rcp(math.max(1f, targetFps));
            frameTimes[_frameTimeCursor] = frameMs;
            _frameTimeCursor++;
            if (_frameTimeCursor >= FrameTimeWindow)
                _frameTimeCursor = 0;
            if (_frameTimeSampleCount < FrameTimeWindow)
                _frameTimeSampleCount++;

            float sum = 0f;
            float sumSq = 0f;
            int count = _frameTimeSampleCount;
            for (int i = 0; i < count; i++)
            {
                float sample = frameTimes[i];
                if (!math.isfinite(sample))
                    sample = frameMs;
                sum += sample;
                sumSq += sample * sample;
            }

            float inverseCount = count > 0 ? 1f / count : 1f;
            float mean = sum * inverseCount;
            float variance = math.max(0f, sumSq * inverseCount - mean * mean);
            float sigma = math.sqrt(variance);
            hardwareMetrics[(int)HardwareMetricSlot.JitterSigma] = math.isfinite(sigma) ? sigma : 0f;
            return frameMs;
        }

        private static void SamplePlatformMetrics(float targetFps, NativeArray<float> hardwareMetrics)
        {
            _usingHardwareSnapshot = false;
            if (TrySampleHardwareThermalSnapshot(targetFps, hardwareMetrics))
                return;

#if UNITY_OSX && !UNITY_EDITOR
            if (!TrySampleMacThermals(targetFps, hardwareMetrics))
                SampleFallbackHardwareMetrics(targetFps, hardwareMetrics);
#else
            SampleFallbackHardwareMetrics(targetFps, hardwareMetrics);
#endif
            hardwareMetrics[(int)HardwareMetricSlot.BatteryLife01] = ResolveBatteryLife01(targetFps);
        }

        private static bool TrySampleHardwareThermalSnapshot(float targetFps, NativeArray<float> hardwareMetrics)
        {
            IHardwareThermalService hardwareThermal = _hardwareThermalService;
            if (hardwareThermal == null || !hardwareThermal.TryGetSnapshot(out HardwareThermalSnapshot snapshot))
                return false;

            float severity01 = math.saturate(snapshot.Severity / (float)HardwareThermalSeverity.Critical);
            float framePressure01 = ResolveFramePressure01(targetFps);
            float pressure01 = math.saturate(math.max(framePressure01 + _fallbackHardwareBias, severity01));
            short rawTemperature = snapshot.TemperatureTenthsCelsius;
            float syntheticTemperatureC = 48f + pressure01 * 34f;
            float temperatureC = rawTemperature != short.MinValue
                ? rawTemperature * 0.1f
                : syntheticTemperatureC;
            temperatureC = math.max(temperatureC, syntheticTemperatureC);
            hardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = math.isfinite(temperatureC) ? temperatureC : 82f;
            hardwareMetrics[(int)HardwareMetricSlot.GpuUtil01] = math.isfinite(pressure01) ? pressure01 : 1f;

            if (snapshot.BatteryPercent <= 100)
                _cachedBatteryLife01 = math.saturate(snapshot.BatteryPercent * 0.01f);
            hardwareMetrics[(int)HardwareMetricSlot.BatteryLife01] = _cachedBatteryLife01;
            _usingHardwareSnapshot = true;
            return true;
        }

        private static void SampleFallbackHardwareMetrics(float targetFps, NativeArray<float> hardwareMetrics)
        {
            float pressure = math.saturate(ResolveFramePressure01(targetFps) + _fallbackHardwareBias + ResolveProcessorFallbackPressure01());
            hardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = 48f + pressure * 34f;
            hardwareMetrics[(int)HardwareMetricSlot.GpuUtil01] = pressure;
        }

        private static float ResolveFramePressure01(float targetFps)
        {
            return targetFps > 0f
                ? math.saturate((targetFps - _fpsEwma) / targetFps)
                : 0f;
        }

        private static float ResolveFallbackHardwareBias()
        {
            int systemMemoryMb = SystemInfo.systemMemorySize;
            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            float bias = systemMemoryMb > 0 && systemMemoryMb <= 8192 ? 0.06f : 0f;
            bias += graphicsMemoryMb > 0 && graphicsMemoryMb <= 2048 ? 0.08f : 0f;
            return math.saturate(bias);
        }

#if UNITY_OSX && !UNITY_EDITOR
        private static bool TrySampleMacThermals(float targetFps, NativeArray<float> hardwareMetrics)
        {
            EnsureMacThermalBridge();
            if (!_macBridgeReady || _macBridgeFaulted || _macProcessInfo == IntPtr.Zero)
                return false;

            try
            {
                long state = objc_msgSend_Int64(_macProcessInfo, _macThermalStateSelector);
                float pressure = math.saturate(state / 3f);
                pressure = math.max(pressure, ResolveFramePressure01(targetFps));
                hardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = 45f + pressure * 42f;
                hardwareMetrics[(int)HardwareMetricSlot.GpuUtil01] = pressure;
                return true;
            }
            catch (Exception)
            {
                _macBridgeFaulted = true;
                return false;
            }
        }

        private static void EnsureMacThermalBridge()
        {
            if (_macBridgeReady || _macBridgeFaulted)
                return;

            try
            {
                _macProcessInfoClass = objc_getClass("NSProcessInfo");
                _macProcessInfoSelector = sel_registerName("processInfo");
                _macThermalStateSelector = sel_registerName("thermalState");
                _macProcessInfo = _macProcessInfoClass != IntPtr.Zero && _macProcessInfoSelector != IntPtr.Zero
                    ? objc_msgSend_IntPtr(_macProcessInfoClass, _macProcessInfoSelector)
                    : IntPtr.Zero;
                _macBridgeReady = _macProcessInfo != IntPtr.Zero && _macThermalStateSelector != IntPtr.Zero;
            }
            catch (Exception)
            {
                DisposeMacThermalBridge();
                _macBridgeFaulted = true;
            }
        }

        private static void DisposeMacThermalBridge()
        {
            _macProcessInfoClass = IntPtr.Zero;
            _macProcessInfoSelector = IntPtr.Zero;
            _macThermalStateSelector = IntPtr.Zero;
            _macProcessInfo = IntPtr.Zero;
            _macBridgeReady = false;
            _macBridgeFaulted = false;
        }

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
        private static extern IntPtr objc_getClass(string className);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
        private static extern IntPtr sel_registerName(string selectorName);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern long objc_msgSend_Int64(IntPtr receiver, IntPtr selector);
#endif

        private static float ResolveBatteryLife01(float targetFps)
        {
            if (_batteryPollCountdown > 0)
            {
                _batteryPollCountdown--;
                return _cachedBatteryLife01;
            }

            _batteryPollCountdown = ResolveFrostPollFrames(targetFps);
            float level = SystemInfo.batteryLevel;
            if (!math.isfinite(level) || level < 0f)
                return _cachedBatteryLife01;

            BatteryStatus status = SystemInfo.batteryStatus;
            if (status == BatteryStatus.Charging || status == BatteryStatus.Full)
            {
                _cachedBatteryLife01 = 1f;
                return _cachedBatteryLife01;
            }

            _cachedBatteryLife01 = math.saturate(level);
            return _cachedBatteryLife01;
        }

        private static int ResolveFrostPollFrames(float targetFps)
        {
            float safeFps = math.isfinite(targetFps) && targetFps > 0f ? targetFps : 60f;
            return math.max(1, (int)math.ceil(safeFps * FrostPollSeconds));
        }

        private static int ResolveTargetFrameRate()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            int target = UnityEngine.Device.Application.targetFrameRate;
#else
            int target = Application.targetFrameRate;
#endif
            if (target <= 0)
                target = 60;
            return target;
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350;
        }

        private static ushort BuildFlags(bool lowTier, HectonQualityTier tier, NativeArray<float> hardwareMetrics)
        {
            ushort flags = 0;
            if (hardwareMetrics[(int)HardwareMetricSlot.JitterSigma] > JitterUnstableSigmaMs)
                flags |= (ushort)HomeostasisSignalFlags.UnstableJitter;
            if (lowTier)
                flags |= (ushort)HomeostasisSignalFlags.LowTierBatteryWeight;
            else if ((tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra) &&
                     _systemHealthIndex01 < SequentialRecoveryShi)
                flags |= (ushort)HomeostasisSignalFlags.VisualOverkillBudgetOpen;
            if (_usingHardwareSnapshot)
            {
                flags |= (ushort)HomeostasisSignalFlags.HardwareThermalSnapshot;
#if UNITY_ANDROID && !UNITY_EDITOR
                flags |= (ushort)HomeostasisSignalFlags.AndroidThermalBridge;
#endif
                return flags;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            flags |= (ushort)HomeostasisSignalFlags.PlatformFallback;
#elif UNITY_OSX && !UNITY_EDITOR
            if (_macBridgeReady && !_macBridgeFaulted)
                flags |= (ushort)HomeostasisSignalFlags.MacThermalBridge;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            flags |= (ushort)HomeostasisSignalFlags.WindowsFallback;
#else
            flags |= (ushort)HomeostasisSignalFlags.PlatformFallback;
#endif
            return flags;
        }

        private static ushort ApplyPressurePolicy(
            int frame,
            float frameMs,
            ushort flags,
            NativeArray<float> hardwareMetrics)
        {
            byte previousLevel = _currentPressureLevel;
            ulong previousMask = _currentKillSwitchMask;
            byte targetLevel = ResolvePressureLevel(_systemHealthIndex01);
            if (targetLevel > 1)
                flags |= (ushort)HomeostasisSignalFlags.HudWarning;
            if (targetLevel >= 3)
                flags |= (ushort)HomeostasisSignalFlags.Emergency;
            ulong targetMask = ResolveTargetMask(targetLevel);
            if (targetLevel == 0)
                targetMask = ResolveSequentialRestorationMask(targetMask);
            else
            {
                _stableRecoveryFrames = 0;
                _recoveryStepFrameCounter = 0;
                _restorationIndex = 0;
            }
            targetMask = ApplyDictatorPressurePolicy(
                frame,
                frameMs,
                targetMask,
                ref targetLevel,
                ref flags,
                hardwareMetrics);

            _currentPressureLevel = targetLevel;
            _currentKillSwitchMask = targetMask;
            if (_stableRecoveryFrames >= RecoveryArmFrames && _currentKillSwitchMask != 0UL)
                flags |= (ushort)HomeostasisSignalFlags.SequentialRestoration;
            byte foveatedPressureTier = targetLevel >= 2 ? (byte)3 : (byte)0;
            bool emergency = targetLevel >= 3;
            if (TryApplyXrRefreshRatePolicy(targetLevel))
                flags |= (ushort)HomeostasisSignalFlags.XrRefreshRateShed;
            SystemDispatcher.ApplyHomeostasisKillSwitch(
                _currentKillSwitchMask,
                _currentPressureLevel,
                foveatedPressureTier,
                (_currentKillSwitchMask & (ulong)SystemBit.AiOneHz) != 0UL,
                emergency,
                ReasonHash);

            bool changed = previousMask != _currentKillSwitchMask || previousLevel != _currentPressureLevel;
            _lastThermalAction = FoldMaskToUInt(_currentKillSwitchMask);
            if (changed)
                PublishKillSwitchSignal(frame, previousMask, previousLevel, flags);

            if (changed || frame - _lastTelemetryFrame >= TelemetryCadenceFrames)
            {
                _lastTelemetryFrame = frame;
                PublishSystemHealthSignal(frame, foveatedPressureTier, flags, hardwareMetrics);
                PublishLegacySystemHealthIndexSignal(frame);
                GlobalTelemetryBus.PublishSystemDegradation(
                    ReasonHash,
                    _lastThermalAction,
                    frameMs);
            }

            return flags;
        }

        private static byte ResolvePressureLevel(float shi)
        {
            if (shi > Level3ActivateShi)
                return 3;
            if (_currentPressureLevel >= 3 && shi > Level3RestoreShi)
                return 3;
            if (shi > Level2ActivateShi)
                return 2;
            if (_currentPressureLevel >= 2 && shi > Level2RestoreShi)
                return 2;
            if (shi > Level1ActivateShi)
                return 1;
            if (_currentPressureLevel >= 1 && shi > Level1RestoreShi)
                return 1;
            return 0;
        }

        private static ulong ResolveTargetMask(byte pressureLevel)
        {
            switch (pressureLevel)
            {
                case 1:
                    return Level1Mask;
                case 2:
                    return Level2Mask;
                case 3:
                    return Level3Mask;
                default:
                    return _currentKillSwitchMask;
            }
        }

        private static ulong ResolveSequentialRestorationMask(ulong targetMask)
        {
            if (_currentKillSwitchMask == 0UL)
            {
                _stableRecoveryFrames = 0;
                _recoveryStepFrameCounter = 0;
                _restorationIndex = 0;
                return 0UL;
            }

            if (_systemHealthIndex01 < SequentialRecoveryShi)
            {
                if (_stableRecoveryFrames < int.MaxValue)
                    _stableRecoveryFrames++;
            }
            else if (_systemHealthIndex01 > Level1RestoreShi)
            {
                _stableRecoveryFrames = 0;
                _recoveryStepFrameCounter = 0;
                _restorationIndex = 0;
            }

            if (_stableRecoveryFrames < RecoveryArmFrames)
                return _currentKillSwitchMask;

            _recoveryStepFrameCounter++;
            if (_recoveryStepFrameCounter < RecoveryStepFrames)
                return _currentKillSwitchMask;

            _recoveryStepFrameCounter = 0;
            while (true)
            {
                ulong bit = ResolveRestorationBit(_restorationIndex);
                _restorationIndex++;
                if (bit == 0UL)
                    return 0UL;
                if ((_currentKillSwitchMask & bit) == 0UL)
                    continue;

                targetMask = _currentKillSwitchMask & ~bit;
                return targetMask;
            }
        }

        private static ulong ResolveRestorationBit(int index)
        {
            switch (index)
            {
                case 0: return (ulong)SystemBit.TimeDilation09;
                case 1: return (ulong)SystemBit.AiOneHz;
                case 2: return (ulong)SystemBit.NonCriticalVfx;
                case 3: return (ulong)SystemBit.BoidBrain;
                case 4: return (ulong)SystemBit.HighQualityIK;
                case 5: return (ulong)SystemBit.ProceduralSway;
                case 6: return (ulong)SystemBit.DistantFaunaSteering;
                case 7: return (ulong)SystemBit.SSR;
                case 8: return (ulong)SystemBit.FoveatedSimulationTier3;
                case 9: return (ulong)SystemBit.VolumetricFogHighRes;
                case 10: return (ulong)SystemBit.MicroDebrisAdvection;
                case 11: return (ulong)SystemBit.SecondaryCaustics;
                default: return 0UL;
            }
        }

        private static void PublishSystemHealthSignal(
            int frame,
            byte foveatedPressureTier,
            ushort flags,
            NativeArray<float> hardwareMetrics)
        {
            SystemHealthSignal signal = default;
            signal.Frame = unchecked((uint)frame);
            signal.SystemHealthIndex01 = _systemHealthIndex01;
            signal.FpsEwma = hardwareMetrics[(int)HardwareMetricSlot.FpsEwma];
            signal.JitterSigmaMs = hardwareMetrics[(int)HardwareMetricSlot.JitterSigma];
            signal.CpuTempC = hardwareMetrics[(int)HardwareMetricSlot.CpuTempC];
            signal.GpuUtil01 = hardwareMetrics[(int)HardwareMetricSlot.GpuUtil01];
            signal.BatteryLife01 = hardwareMetrics[(int)HardwareMetricSlot.BatteryLife01];
            signal.KillSwitchMask = _currentKillSwitchMask;
            signal.PressureLevel = _currentPressureLevel;
            signal.FoveatedPressureTier = foveatedPressureTier;
            signal.Flags = flags;
            SignalBus<SystemHealthSignal>.Push(in signal);
        }

        private static void PublishFrameTimeSignal(
            int frame,
            float frameMs,
            float targetFps,
            ushort flags,
            NativeArray<float> hardwareMetrics)
        {
            float fpsEwma = hardwareMetrics[(int)HardwareMetricSlot.FpsEwma];
            float frameTimeEwmaMs = fpsEwma > 0f
                ? 1000f * math.rcp(math.max(1f, fpsEwma))
                : frameMs;
            if (!math.isfinite(frameTimeEwmaMs))
                frameTimeEwmaMs = frameMs;

            FrameTimeSignal signal = default;
            signal.Frame = unchecked((uint)frame);
            signal.CurrentFrameTimeMs = frameMs;
            signal.FrameTimeEwmaMs = frameTimeEwmaMs;
            signal.TargetFrameTimeMs = 1000f * math.rcp(math.max(1f, targetFps));
            signal.JitterSigmaMs = hardwareMetrics[(int)HardwareMetricSlot.JitterSigma];
            signal.PressureLevel = _currentPressureLevel;
            signal.Flags = unchecked((byte)(flags & 0xFF));
            signal.Reserved = 0;
            signal.Sequence = _frameTimeSignalSequence++;
            SignalBus<FrameTimeSignal>.Push(in signal);
        }

        private static void PublishKillSwitchSignal(int frame, ulong previousMask, byte previousLevel, ushort flags)
        {
            KillSwitchSignal signal = default;
            signal.Frame = unchecked((uint)frame);
            signal.PreviousMask = previousMask;
            signal.CurrentMask = _currentKillSwitchMask;
            signal.SystemHealthIndex01 = _systemHealthIndex01;
            signal.PreviousLevel = previousLevel;
            signal.CurrentLevel = _currentPressureLevel;
            signal.Flags = flags;
            SignalBus<KillSwitchSignal>.Push(in signal);
        }

        private static void PublishLegacySystemHealthIndexSignal(int frame)
        {
            SystemHealthIndexSignal signal = default;
            signal.Health01 = 1f - _systemHealthIndex01;
            signal.Pressure01 = _systemHealthIndex01;
            signal.Frame = unchecked((uint)frame);
            signal.SourceHash = ReasonHash;
            signal.State = _currentPressureLevel >= 3
                ? SystemHealthIndexSignal.StateCritical
                : (_currentPressureLevel > 0 ? SystemHealthIndexSignal.StateWarning : SystemHealthIndexSignal.StateStable);
            signal.Flags = _currentPressureLevel >= 3 ? SystemHealthIndexSignal.FlagAdrenaline : (byte)0;
            SignalBus<SystemHealthIndexSignal>.Push(in signal);
        }

        private static void WriteBlackBox(
            int frame,
            float frameMs,
            ushort flags,
            NativeArray<float> hardwareMetrics,
            NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            if (!blackBox.IsCreated)
                return;

            float timeDilation = SystemDispatcher.ActiveRuntimeInstance != null
                ? SystemDispatcher.ActiveRuntimeInstance.TimeDilationScalar
                : 1f;
            float vramPressure01 = hardwareMetrics[(int)HardwareMetricSlot.VramPressure01];
            vramPressure01 = math.isfinite(vramPressure01) ? math.saturate(vramPressure01) : 1f;
            HomeostasisBlackBoxEntry entry = default;
            entry.Frame = unchecked((uint)frame);
            entry.SystemHealthIndex01 = _systemHealthIndex01;
            entry.KillSwitchMask = _currentKillSwitchMask;
            entry.FpsEwma = hardwareMetrics[(int)HardwareMetricSlot.FpsEwma];
            entry.JitterSigmaMs = hardwareMetrics[(int)HardwareMetricSlot.JitterSigma];
            entry.CpuTempC = hardwareMetrics[(int)HardwareMetricSlot.CpuTempC];
            entry.GpuUtil01 = hardwareMetrics[(int)HardwareMetricSlot.GpuUtil01];
            entry.BatteryLife01 = hardwareMetrics[(int)HardwareMetricSlot.BatteryLife01];
            entry.PressureLevel = _currentPressureLevel;
            entry.FoveatedPressureTier = _currentPressureLevel >= 2 ? (byte)3 : (byte)0;
            entry.Flags = flags;
            entry.TimeDilationScalar = math.isfinite(timeDilation) ? timeDilation : 1f;
            entry.PeakSystemHealthIndex01 = _peakSystemHealthIndex01;
            entry.LastThermalAction = _lastThermalAction;
            entry.Reserved0 = math.asuint(math.isfinite(frameMs) ? frameMs : 0f);
            entry.Reserved1 = math.asuint(vramPressure01);
            entry.Reserved2 = math.asuint(GlobalQualityWeight);
            blackBox[_blackBoxCursor] = entry;
            _blackBoxCursor++;
            if (_blackBoxCursor >= BlackBoxCapacity)
                _blackBoxCursor = 0;
        }

        private static void DumpBlackBoxOnce(NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            if (_blackBoxDumped || !blackBox.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, BlackBoxDumpFileName);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[20];
                    BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), 0x484F4D42u);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), 1);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), BlackBoxCapacity);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), _blackBoxCursor);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), 64);
                    stream.Write(header);

                    Span<byte> entryBytes = stackalloc byte[64];
                    for (int i = 0; i < BlackBoxCapacity; i++)
                    {
                        int index = _blackBoxCursor + i;
                        if (index >= BlackBoxCapacity)
                            index -= BlackBoxCapacity;

                        HomeostasisBlackBoxEntry entry = blackBox[index];
                        entryBytes.Clear();
                        BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(0, 4), entry.Frame);
                        WriteFloatLittleEndian(entryBytes.Slice(4, 4), entry.SystemHealthIndex01);
                        BinaryPrimitives.WriteUInt64LittleEndian(entryBytes.Slice(8, 8), entry.KillSwitchMask);
                        WriteFloatLittleEndian(entryBytes.Slice(16, 4), entry.FpsEwma);
                        WriteFloatLittleEndian(entryBytes.Slice(20, 4), entry.JitterSigmaMs);
                        WriteFloatLittleEndian(entryBytes.Slice(24, 4), entry.CpuTempC);
                        WriteFloatLittleEndian(entryBytes.Slice(28, 4), entry.GpuUtil01);
                        WriteFloatLittleEndian(entryBytes.Slice(32, 4), entry.BatteryLife01);
                        entryBytes[36] = entry.PressureLevel;
                        entryBytes[37] = entry.FoveatedPressureTier;
                        BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.Slice(38, 2), entry.Flags);
                        WriteFloatLittleEndian(entryBytes.Slice(40, 4), entry.TimeDilationScalar);
                        WriteFloatLittleEndian(entryBytes.Slice(44, 4), entry.PeakSystemHealthIndex01);
                        BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(48, 4), entry.LastThermalAction);
                        WriteFloatLittleEndian(entryBytes.Slice(52, 4), math.asfloat(entry.Reserved0));
                        WriteFloatLittleEndian(entryBytes.Slice(56, 4), math.asfloat(entry.Reserved1));
                        BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(60, 4), entry.Reserved2);
                        stream.Write(entryBytes);
                    }
                }
            }
            catch (Exception)
            {
                // Fault-path only: black-box dumping must never crash the runtime while already degraded.
            }
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private static long ResolveRequestedVaultBytes()
        {
            return ((long)HardwareMetricSlot.Count * sizeof(float)) +
                   ((long)FrameTimeWindow * sizeof(float)) +
                   ((long)BlackBoxCapacity * Marshal.SizeOf<HomeostasisBlackBoxEntry>()) +
                   ResolveScalabilityDictatorRequestedVaultBytes();
        }

        private static bool TryResolveRuntimeBuffers(
            out NativeArray<float> hardwareMetrics,
            out NativeArray<float> frameTimes,
            out NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            hardwareMetrics = default;
            frameTimes = default;
            blackBox = default;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            bool metricsCreated = false;
            bool frameTimesCreated = false;
            bool blackBoxCreated = false;
            if (!_globalHardwareMetricsHandle.IsCreated || !vault.ResolveBuffer(ref _globalHardwareMetricsHandle))
            {
                _globalHardwareMetricsHandle = vault.GetBufferHandle<float>(
                    BufferID.HardwareMetrics,
                    (int)HardwareMetricSlot.Count,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                metricsCreated = true;
            }

            if (!_frameTimeMsHandle.IsCreated || !vault.ResolveBuffer(ref _frameTimeMsHandle))
            {
                _frameTimeMsHandle = vault.GetBufferHandle<float>(
                    BufferID.HardwareFrameTimes,
                    FrameTimeWindow,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                frameTimesCreated = true;
            }

            if (!_blackBoxHandle.IsCreated || !vault.ResolveBuffer(ref _blackBoxHandle))
            {
                _blackBoxHandle = vault.GetBufferHandle<HomeostasisBlackBoxEntry>(
                    BufferID.HomeostasisBlackBox,
                    BlackBoxCapacity,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                blackBoxCreated = true;
            }

            hardwareMetrics = _globalHardwareMetricsHandle.Resolve(vault);
            frameTimes = _frameTimeMsHandle.Resolve(vault);
            blackBox = _blackBoxHandle.Resolve(vault);
            if (metricsCreated)
                MemClearIfCreated(hardwareMetrics);
            if (frameTimesCreated)
                MemClearIfCreated(frameTimes);
            if (blackBoxCreated)
                MemClearIfCreated(blackBox);
            return hardwareMetrics.IsCreated &&
                   hardwareMetrics.Length >= (int)HardwareMetricSlot.Count &&
                   frameTimes.IsCreated &&
                   frameTimes.Length >= FrameTimeWindow &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxCapacity;
        }

        private static bool TryResolveHardwareMetrics(out NativeArray<float> metrics)
        {
            metrics = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            bool metricsCreated = false;
            if (!_globalHardwareMetricsHandle.IsCreated || !vault.ResolveBuffer(ref _globalHardwareMetricsHandle))
            {
                _globalHardwareMetricsHandle = vault.GetBufferHandle<float>(
                    BufferID.HardwareMetrics,
                    (int)HardwareMetricSlot.Count,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                metricsCreated = true;
            }

            metrics = _globalHardwareMetricsHandle.Resolve(vault);
            if (metricsCreated)
                MemClearIfCreated(metrics);
            return metrics.IsCreated && metrics.Length >= (int)HardwareMetricSlot.Count;
        }

        private static void RegisterDependencyListeners()
        {
            if (!_scalabilityListenerRegistered)
            {
                ScalabilityEvents.Register(s_scalabilityListener);
                _scalabilityListenerRegistered = true;
            }

            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(s_dependencyHotSwapBridge);
        }

        private static void UnregisterDependencyListeners()
        {
            if (_scalabilityListenerRegistered)
            {
                ScalabilityEvents.Unregister(s_scalabilityListener);
                _scalabilityListenerRegistered = false;
            }

            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(s_dependencyHotSwapBridge);
                _hotSwapRegistered = false;
            }
        }

        private static void RebindRegistryDependency(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.HardwareThermalService)
            {
                _hardwareThermalService = currentService as IHardwareThermalService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime)
            {
                _dynamicResolutionRuntime = currentService as IDynamicResolutionRuntime;
                _lastAppliedRenderScale01 = ForcedQualityWeightDisabled;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            _dataVault = currentService as IDataVault;
            _globalHardwareMetricsHandle = default;
            _frameTimeMsHandle = default;
            _blackBoxHandle = default;
            ResetScalabilityDictatorVaultHandles();
        }

        private sealed class ScalabilityListener : IScalabilityChangedEventListener
        {
            public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
            {
                _cachedScalabilityTier = payload.CurrentQualityTier;
            }
        }

        private sealed class DependencyHotSwapBridge : IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
        {
            public void OnGlobalRegistryServiceRebound(
                GlobalRegistryServiceSlot serviceSlot,
                ref object currentService)
            {
                RebindRegistryDependency(serviceSlot, currentService);
            }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                RebindRegistryDependency(serviceSlot, currentService);
            }
        }

        private static uint FoldMaskToUInt(ulong mask)
        {
            return unchecked((uint)mask ^ (uint)(mask >> 32));
        }

        private static float ResolveProcessorFallbackPressure01()
        {
            int processorFrequency = SystemInfo.processorFrequency;
            if (processorFrequency <= 0)
                return 0f;

            return processorFrequency < 1800
                ? 0.18f
                : (processorFrequency < 2400 ? 0.08f : 0f);
        }

        private static bool TryApplyXrRefreshRatePolicy(byte targetLevel)
        {
            if (targetLevel < 2 || !HectonXRRuntimeState.IsXRActive)
                return false;

            return HectonXRRuntimeState.TryRequestDisplayRefreshRateHz(72f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MonoPInvokeCallback(typeof(ComputeSystemHealthIndexDelegate))]
        private static float ComputeSystemHealthIndexBurst(
            float jitterSigmaMs,
            float cpuTempC,
            float batteryLife01,
            int lowTier)
        {
            float jitter01 = math.saturate(jitterSigmaMs * 0.5f);
            float temp01 = math.saturate((cpuTempC - 55f) / 30f);
            float batteryPressure01 = math.saturate(1f - batteryLife01);
            return math.saturate(temp01 * 0.5f + batteryPressure01 * 0.3f + jitter01 * 0.2f);
        }

        private static float ComputeSystemHealthIndexManaged(
            float jitterSigmaMs,
            float cpuTempC,
            float batteryLife01,
            bool lowTier)
        {
            float jitter01 = math.saturate(jitterSigmaMs * 0.5f);
            float temp01 = math.saturate((cpuTempC - 55f) / 30f);
            float batteryPressure01 = math.saturate(1f - batteryLife01);
            return math.saturate(temp01 * 0.5f + batteryPressure01 * 0.3f + jitter01 * 0.2f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MonoPInvokeCallback(typeof(ComputeFrameEwmaDelegate))]
        private static float ComputeFrameEwmaBurst(
            float previousValue,
            float currentValue,
            float alpha,
            int seeded)
        {
            float safeCurrent = math.isfinite(currentValue) ? currentValue : 0f;
            if (seeded == 0 || !math.isfinite(previousValue) || previousValue <= 0f)
                return safeCurrent;

            float safeAlpha = math.clamp(alpha, 0f, 1f);
            return math.lerp(previousValue, safeCurrent, safeAlpha);
        }

        private static float ComputeFrameEwma(
            float previousValue,
            float currentValue,
            float alpha,
            bool seeded)
        {
            if (_computeFrameEwma.IsCreated)
                return _computeFrameEwma.Invoke(previousValue, currentValue, alpha, seeded ? 1 : 0);

            return ComputeFrameEwmaBurst(previousValue, currentValue, alpha, seeded ? 1 : 0);
        }
    }
}
