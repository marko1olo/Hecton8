using System;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct SystemHealthSignal : ISignal
    {
        public uint Frame;
        public float SystemHealthIndex01;
        public float FpsEwma;
        public float JitterSigmaMs;
        public float CpuTempC;
        public float GpuUtil01;
        public float BatteryLife01;
        public ulong KillSwitchMask;
        public byte PressureLevel;
        public byte FoveatedPressureTier;
        public ushort Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct FrameTimeSignal : ISignal
    {
        public uint Frame;
        public float CurrentFrameTimeMs;
        public float FrameTimeEwmaMs;
        public float TargetFrameTimeMs;
        public float JitterSigmaMs;
        public byte PressureLevel;
        public byte Flags;
        public ushort Reserved;
        public uint Sequence;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct KillSwitchSignal : ISignal
    {
        public uint Frame;
        public ulong PreviousMask;
        public ulong CurrentMask;
        public float SystemHealthIndex01;
        public byte PreviousLevel;
        public byte CurrentLevel;
        public ushort Flags;
    }
}

namespace Hecton8.Core
{
    public enum HardwareMetricSlot : int
    {
        FpsEwma = 0,
        JitterSigma = 1,
        CpuTempC = 2,
        GpuUtil01 = 3,
        BatteryLife01 = 4,
        Count = 5
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
        TimeDilation08 = TimeDilation09
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

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct HomeostasisBlackBoxEntry
    {
        public uint Frame;
        public float SystemHealthIndex01;
        public ulong KillSwitchMask;
        public float FpsEwma;
        public float JitterSigmaMs;
        public float CpuTempC;
        public float GpuUtil01;
        public float BatteryLife01;
        public byte PressureLevel;
        public byte FoveatedPressureTier;
        public ushort Flags;
        public float TimeDilationScalar;
        public float PeakSystemHealthIndex01;
        public uint LastThermalAction;
        public uint Reserved1;
    }

    /// <summary>
    /// Pre-simulation hardware homeostasis controller. It writes numeric masks and signals only;
    /// render/gameplay systems decide how to consume each bit.
    /// </summary>
    public static class HomeostasisBrain
    {
        private const int FrameTimeWindow = 120;
        private const int BlackBoxCapacity = 300;
        private const int TelemetryCadenceFrames = 60;
        private const int RecoveryArmFrames = 3000;
        private const int RecoveryStepFrames = 60;
        private const float FrostPollSeconds = 5f;
        private const float FpsEwmaAlpha = 0.1f;
        private const float ShiEwmaAlpha = 0.12f;
        private const float JitterUnstableSigmaMs = 2.0f;
        private const float Level1ActivateShi = 0.60f;
        private const float Level1RestoreShi = 0.50f;
        private const float Level2ActivateShi = 0.80f;
        private const float Level2RestoreShi = 0.70f;
        private const float Level3ActivateShi = 0.95f;
        private const float Level3RestoreShi = 0.90f;
        private const float SequentialRecoveryShi = 0.30f;
        private const float EmergencyTimeDilationScalar = 0.9f;
        private const long PersistentNativeBudgetBytes = 8192L;
        private const string OwnerName = nameof(HomeostasisBrain);
        private const string BlackBoxDumpFileName = "Dump_AGENT_HOMEOSTASIS_BRAIN.bin";
        private const uint ReasonHash = 0x484F4D45u; // HOME

        private const ulong Level1Mask =
            (ulong)(SystemBit.SecondaryCaustics |
                    SystemBit.MicroDebrisAdvection);

        private const ulong Level2Mask =
            Level1Mask |
            (ulong)(SystemBit.DistantFaunaSteering |
                    SystemBit.ProceduralSway |
                    SystemBit.HighQualityIK |
                    SystemBit.SSR |
                    SystemBit.VolumetricFogHighRes |
                    SystemBit.FoveatedSimulationTier3);

        private const ulong Level3Mask =
            Level2Mask |
            (ulong)(SystemBit.BoidBrain |
                    SystemBit.NonCriticalVfx |
                    SystemBit.AiOneHz |
                    SystemBit.TimeDilation09);

        private static IDataVault _dataVault;
        private static VaultBufferHandle<float> _globalHardwareMetricsHandle;
        private static VaultBufferHandle<float> _frameTimeMsHandle;
        private static VaultBufferHandle<HomeostasisBlackBoxEntry> _blackBoxHandle;
        private static FunctionPointer<ComputeSystemHealthIndexDelegate> _computeShi;

        private static bool _initialized;
        private static bool _blackBoxDumped;
        private static bool _shiEwmaSeeded;
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
        private static ulong _currentKillSwitchMask;
        private static byte _currentPressureLevel;
        private static uint _lastThermalAction;
        private static uint _frameTimeSignalSequence;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass _unityPlayerClass;
        private static AndroidJavaClass _androidVersionClass;
        private static AndroidJavaObject _unityActivity;
        private static AndroidJavaObject _powerManager;
        private static int _androidSdkInt;
        private static int _androidThermalFeatureFlags;
        private static int _androidThermalPollCountdown;
        private static bool _androidBridgeReady;
        private static bool _androidBridgeFaulted;
        private static float _androidCpuTempC = 45f;
#endif

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

            _globalHardwareMetrics = ResolveHardwareMetricsBuffer();
            _frameTimeMs = H8Memory.Allocate<float>(
                FrameTimeWindow,
                SystemID.HardwareHomeostasis,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            _blackBox = H8Memory.Allocate<HomeostasisBlackBoxEntry>(
                BlackBoxCapacity,
                SystemID.HardwareHomeostasis,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            if (!_globalHardwareMetrics.IsCreated || !_frameTimeMs.IsCreated || !_blackBox.IsCreated)
            {
                ShutdownRuntime();
                return;
            }

            if (!_globalHardwareMetricsVaultOwned)
            {
                NativeMemorySentinel.RegisterNativeArray(
                    _globalHardwareMetrics,
                    OwnerName,
                    nameof(_globalHardwareMetrics),
                    NativeAllocationLifetime.Session);
            }

            NativeMemorySentinel.RegisterNativeArray(
                _frameTimeMs,
                OwnerName,
                nameof(_frameTimeMs),
                NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(
                _blackBox,
                OwnerName,
                nameof(_blackBox),
                NativeAllocationLifetime.Session);
            MemoryBudgetTracker.Register(OwnerName, ResolvePersistentBytes(), PersistentNativeBudgetBytes);

            GlobalSignals.InitializeAllQueues();

            _computeShi = BurstCompiler.CompileFunctionPointer<ComputeSystemHealthIndexDelegate>(ComputeSystemHealthIndexBurst);
            _fpsEwma = ResolveTargetFrameRate();
            _globalHardwareMetrics[(int)HardwareMetricSlot.FpsEwma] = _fpsEwma;
            _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = 45f;
            _globalHardwareMetrics[(int)HardwareMetricSlot.BatteryLife01] = 1f;
            _fallbackHardwareBias = ResolveFallbackHardwareBias();
            _cachedBatteryLife01 = 1f;
            _batteryPollCountdown = 0;
            _currentKillSwitchMask = 0UL;
            _currentPressureLevel = 0;
            _frameTimeSignalSequence = 0u;
            _blackBoxDumped = false;

#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroidThermalBridge();
#endif

            _initialized = true;
        }

        public static void ShutdownRuntime()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            DisposeAndroidThermalBridge();
#endif
            if (_blackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_blackBox);
                H8Memory.Release(ref _blackBox, SystemID.HardwareHomeostasis);
            }

            if (_frameTimeMs.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_frameTimeMs);
                H8Memory.Release(ref _frameTimeMs, SystemID.HardwareHomeostasis);
            }

            if (_globalHardwareMetrics.IsCreated)
            {
                if (!_globalHardwareMetricsVaultOwned)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_globalHardwareMetrics);
                    H8Memory.Release(ref _globalHardwareMetrics, SystemID.HardwareHomeostasis);
                }
            }

            MemoryBudgetTracker.Unregister(OwnerName);
            _computeShi = default;
            _globalHardwareMetrics = default;
            _globalHardwareMetricsVaultOwned = false;
            _initialized = false;
            _blackBoxDumped = false;
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
            _fallbackHardwareBias = 0f;
            _cachedBatteryLife01 = 1f;
            _usingHardwareSnapshot = false;
            _currentKillSwitchMask = 0UL;
            _currentPressureLevel = 0;
            _frameTimeSignalSequence = 0u;
        }

        internal static void PreSimulationTick(float unscaledDeltaTime)
        {
            InitializeRuntime();
            if (!_globalHardwareMetrics.IsCreated || !_frameTimeMs.IsCreated)
                return;

            int frame = Time.frameCount;
            float targetFps = ResolveTargetFrameRate();
            float frameMs = SampleFrameMetrics(unscaledDeltaTime, targetFps);
            SamplePlatformMetrics(targetFps);

            bool lowTier = IsLowTier(GlobalRegistry.ScalabilityTier);
            _systemHealthIndex01 = _computeShi.IsCreated
                ? _computeShi.Invoke(
                    _globalHardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                    _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC],
                    _globalHardwareMetrics[(int)HardwareMetricSlot.BatteryLife01],
                    lowTier ? 1 : 0)
                : ComputeSystemHealthIndexManaged(
                    _globalHardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                    _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC],
                    _globalHardwareMetrics[(int)HardwareMetricSlot.BatteryLife01],
                    lowTier);

            if (!math.isfinite(_systemHealthIndex01))
            {
                DumpBlackBoxOnce();
                _systemHealthIndex01 = 1f;
            }

            ushort flags = ApplyPressurePolicy(frame, frameMs, BuildFlags(lowTier));
            PublishFrameTimeSignal(frame, frameMs, targetFps, flags);
            WriteBlackBox(frame, flags);
        }

        private static float SampleFrameMetrics(float unscaledDeltaTime, float targetFps)
        {
            float safeDeltaTime = math.isfinite(unscaledDeltaTime) && unscaledDeltaTime > 0f
                ? unscaledDeltaTime
                : 1f / math.max(1f, targetFps);
            float currentFps = math.clamp(1f / safeDeltaTime, 1f, 1000f);
            _fpsEwma = _fpsEwma <= 0f
                ? currentFps
                : math.lerp(_fpsEwma, currentFps, FpsEwmaAlpha);
            _globalHardwareMetrics[(int)HardwareMetricSlot.FpsEwma] = _fpsEwma;

            float frameMs = safeDeltaTime * 1000f;
            _frameTimeMs[_frameTimeCursor] = frameMs;
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
                float sample = _frameTimeMs[i];
                sum += sample;
                sumSq += sample * sample;
            }

            float inverseCount = count > 0 ? 1f / count : 1f;
            float mean = sum * inverseCount;
            float variance = math.max(0f, sumSq * inverseCount - mean * mean);
            float sigma = math.sqrt(variance);
            _globalHardwareMetrics[(int)HardwareMetricSlot.JitterSigma] = sigma;
            return frameMs;
        }

        private static void SamplePlatformMetrics(float targetFps)
        {
            _usingHardwareSnapshot = false;
            if (TrySampleHardwareThermalSnapshot(targetFps))
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!TrySampleAndroidThermals())
                SampleFallbackHardwareMetrics(targetFps);
#else
            SampleFallbackHardwareMetrics(targetFps);
#endif
            _globalHardwareMetrics[(int)HardwareMetricSlot.BatteryLife01] = ResolveBatteryLife01();
        }

        private static bool TrySampleHardwareThermalSnapshot(float targetFps)
        {
            IHardwareThermalService hardwareThermal = GlobalRegistry.HardwareThermal;
            if (hardwareThermal == null || !hardwareThermal.TryGetSnapshot(out HardwareThermalSnapshot snapshot))
                return false;

            float severity01 = math.saturate(snapshot.Severity / (float)HardwareThermalSeverity.Critical);
            float framePressure01 = ResolveFramePressure01(targetFps);
            float pressure01 = math.saturate(math.max(framePressure01 + _fallbackHardwareBias, severity01));
            short rawTemperature = snapshot.TemperatureTenthsCelsius;
            float temperatureC = rawTemperature != short.MinValue
                ? rawTemperature * 0.1f
                : 48f + pressure01 * 34f;
            _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = temperatureC;
            _globalHardwareMetrics[(int)HardwareMetricSlot.GpuUtil01] = pressure01;

            if (snapshot.BatteryPercent <= 100)
                _cachedBatteryLife01 = math.saturate(snapshot.BatteryPercent * 0.01f);
            _globalHardwareMetrics[(int)HardwareMetricSlot.BatteryLife01] = _cachedBatteryLife01;
            _usingHardwareSnapshot = true;
            return true;
        }

        private static void SampleFallbackHardwareMetrics(float targetFps)
        {
            float pressure = math.saturate(ResolveFramePressure01(targetFps) + _fallbackHardwareBias);
            _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = 48f + pressure * 34f;
            _globalHardwareMetrics[(int)HardwareMetricSlot.GpuUtil01] = pressure;
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

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool TrySampleAndroidThermals()
        {
            EnsureAndroidThermalBridge();
            if (!_androidBridgeReady || _androidBridgeFaulted || _powerManager == null)
                return false;

            if (_androidThermalPollCountdown > 0)
            {
                _androidThermalPollCountdown--;
                _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = _androidCpuTempC;
                return true;
            }

            _androidThermalPollCountdown = AndroidThermalPollFrames;
            try
            {
                if ((_androidThermalFeatureFlags & 1) != 0)
                {
                    float headroom = _powerManager.Call<float>("getThermalHeadroom", 0);
                    if (math.isfinite(headroom))
                    {
                        float clamped = math.saturate(headroom);
                        _androidCpuTempC = 45f + (1f - clamped) * 45f;
                        _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = _androidCpuTempC;
                        _globalHardwareMetrics[(int)HardwareMetricSlot.GpuUtil01] = math.saturate(1f - clamped);
                        return true;
                    }
                }

                if ((_androidThermalFeatureFlags & 2) != 0)
                {
                    int status = _powerManager.Call<int>("getCurrentThermalStatus");
                    float status01 = math.saturate(status / 6f);
                    _androidCpuTempC = 45f + status01 * 45f;
                    _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC] = _androidCpuTempC;
                    _globalHardwareMetrics[(int)HardwareMetricSlot.GpuUtil01] = status01;
                    return true;
                }
            }
            catch (Exception)
            {
                _androidBridgeFaulted = true;
            }

            return false;
        }

        private static void EnsureAndroidThermalBridge()
        {
            if (_androidBridgeReady || _androidBridgeFaulted)
                return;

            try
            {
                _unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                _androidVersionClass = new AndroidJavaClass("android.os.Build$VERSION");
                _androidSdkInt = _androidVersionClass.GetStatic<int>("SDK_INT");
                _unityActivity = _unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
                _powerManager = _unityActivity != null
                    ? _unityActivity.Call<AndroidJavaObject>("getSystemService", "power")
                    : null;
                _androidThermalFeatureFlags = 0;
                if (_androidSdkInt >= 30)
                    _androidThermalFeatureFlags |= 1;
                if (_androidSdkInt >= 29)
                    _androidThermalFeatureFlags |= 2;

                _androidBridgeReady = _powerManager != null && _androidThermalFeatureFlags != 0;
            }
            catch (Exception)
            {
                _androidBridgeFaulted = true;
                DisposeAndroidThermalBridge();
            }
        }

        private static void DisposeAndroidThermalBridge()
        {
            _powerManager?.Dispose();
            _unityActivity?.Dispose();
            _androidVersionClass?.Dispose();
            _unityPlayerClass?.Dispose();
            _powerManager = null;
            _unityActivity = null;
            _androidVersionClass = null;
            _unityPlayerClass = null;
            _androidSdkInt = 0;
            _androidThermalFeatureFlags = 0;
            _androidThermalPollCountdown = 0;
            _androidBridgeReady = false;
            _androidBridgeFaulted = false;
            _androidCpuTempC = 45f;
        }
#endif

        private static float ResolveBatteryLife01()
        {
            if (_batteryPollCountdown > 0)
            {
                _batteryPollCountdown--;
                return _cachedBatteryLife01;
            }

            _batteryPollCountdown = BatteryPollFrames;
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

        private static ushort BuildFlags(bool lowTier)
        {
            ushort flags = 0;
            if (_globalHardwareMetrics[(int)HardwareMetricSlot.JitterSigma] > JitterUnstableSigmaMs)
                flags |= (ushort)HomeostasisSignalFlags.UnstableJitter;
            if (lowTier)
                flags |= (ushort)HomeostasisSignalFlags.LowTierBatteryWeight;
            if (_usingHardwareSnapshot)
            {
                flags |= (ushort)HomeostasisSignalFlags.HardwareThermalSnapshot;
                return flags;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_androidBridgeReady && !_androidBridgeFaulted)
                flags |= (ushort)HomeostasisSignalFlags.AndroidThermalBridge;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            flags |= (ushort)HomeostasisSignalFlags.WindowsFallback;
#endif
            return flags;
        }

        private static ushort ApplyPressurePolicy(int frame, float frameMs, ushort flags)
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

            _currentPressureLevel = targetLevel;
            _currentKillSwitchMask = targetMask;
            if (_stableRecoveryFrames >= RecoveryArmFrames && _currentKillSwitchMask != 0UL)
                flags |= (ushort)HomeostasisSignalFlags.SequentialRestoration;
            byte foveatedPressureTier = targetLevel >= 2 ? (byte)3 : (byte)0;
            bool emergency = targetLevel >= 3;
            SystemDispatcher.ApplyHomeostasisKillSwitch(
                _currentKillSwitchMask,
                _currentPressureLevel,
                foveatedPressureTier,
                (_currentKillSwitchMask & (ulong)SystemBit.SlowTick2Hz) != 0UL,
                emergency,
                ReasonHash);

            bool changed = previousMask != _currentKillSwitchMask || previousLevel != _currentPressureLevel;
            if (changed)
                PublishKillSwitchSignal(frame, previousMask, previousLevel, flags);

            if (changed || frame - _lastTelemetryFrame >= TelemetryCadenceFrames)
            {
                _lastTelemetryFrame = frame;
                PublishSystemHealthSignal(frame, foveatedPressureTier, flags);
                PublishLegacySystemHealthIndexSignal(frame);
                GlobalTelemetryBus.PublishSystemDegradation(
                    ReasonHash,
                    FoldMaskToUInt(_currentKillSwitchMask),
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
            ulong bit = ResolveRestorationBit(_restorationIndex);
            _restorationIndex++;
            if (bit == 0UL)
                return 0UL;

            targetMask = _currentKillSwitchMask & ~bit;
            return targetMask;
        }

        private static ulong ResolveRestorationBit(int index)
        {
            switch (index)
            {
                case 0: return (ulong)SystemBit.TimeDilation08;
                case 1: return (ulong)SystemBit.SlowTick2Hz;
                case 2: return (ulong)SystemBit.NonCriticalVfx;
                case 3: return (ulong)SystemBit.BoidBrain;
                case 4: return (ulong)SystemBit.IKBracing;
                case 5: return (ulong)SystemBit.ProceduralSway;
                case 6: return (ulong)SystemBit.DistantFaunaSteering;
                case 7: return (ulong)SystemBit.SSR;
                case 8: return (ulong)SystemBit.FoveatedSimulationTier3;
                case 9: return (ulong)SystemBit.VolumetricFogHighRes;
                case 10: return (ulong)SystemBit.ParticleAdvection;
                case 11: return (ulong)SystemBit.SecondaryCaustics;
                default: return 0UL;
            }
        }

        private static void PublishSystemHealthSignal(int frame, byte foveatedPressureTier, ushort flags)
        {
            SystemHealthSignal signal = new SystemHealthSignal
            {
                Frame = unchecked((uint)frame),
                SystemHealthIndex01 = _systemHealthIndex01,
                FpsEwma = _globalHardwareMetrics[(int)HardwareMetricSlot.FpsEwma],
                JitterSigmaMs = _globalHardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                CpuTempC = _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC],
                GpuUtil01 = _globalHardwareMetrics[(int)HardwareMetricSlot.GpuUtil01],
                BatteryLife01 = _globalHardwareMetrics[(int)HardwareMetricSlot.BatteryLife01],
                KillSwitchMask = _currentKillSwitchMask,
                PressureLevel = _currentPressureLevel,
                FoveatedPressureTier = foveatedPressureTier,
                Flags = flags
            };
            SignalBus<SystemHealthSignal>.Push(in signal);
        }

        private static void PublishFrameTimeSignal(int frame, float frameMs, float targetFps, ushort flags)
        {
            float fpsEwma = _globalHardwareMetrics[(int)HardwareMetricSlot.FpsEwma];
            float frameTimeEwmaMs = fpsEwma > 0f
                ? 1000f * math.rcp(math.max(1f, fpsEwma))
                : frameMs;
            if (!math.isfinite(frameTimeEwmaMs))
                frameTimeEwmaMs = frameMs;

            FrameTimeSignal signal = new FrameTimeSignal
            {
                Frame = unchecked((uint)frame),
                CurrentFrameTimeMs = frameMs,
                FrameTimeEwmaMs = frameTimeEwmaMs,
                TargetFrameTimeMs = 1000f * math.rcp(math.max(1f, targetFps)),
                JitterSigmaMs = _globalHardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                PressureLevel = _currentPressureLevel,
                Flags = unchecked((byte)(flags & 0xFF)),
                Reserved = 0,
                Sequence = _frameTimeSignalSequence++
            };
            SignalBus<FrameTimeSignal>.Push(in signal);
        }

        private static void PublishKillSwitchSignal(int frame, ulong previousMask, byte previousLevel, ushort flags)
        {
            KillSwitchSignal signal = new KillSwitchSignal
            {
                Frame = unchecked((uint)frame),
                PreviousMask = previousMask,
                CurrentMask = _currentKillSwitchMask,
                SystemHealthIndex01 = _systemHealthIndex01,
                PreviousLevel = previousLevel,
                CurrentLevel = _currentPressureLevel,
                Flags = flags
            };
            SignalBus<KillSwitchSignal>.Push(in signal);
        }

        private static void PublishLegacySystemHealthIndexSignal(int frame)
        {
            SystemHealthIndexSignal signal = new SystemHealthIndexSignal
            {
                Health01 = 1f - _systemHealthIndex01,
                Pressure01 = _systemHealthIndex01,
                Frame = unchecked((uint)frame),
                SourceHash = ReasonHash,
                State = _currentPressureLevel >= 3
                    ? SystemHealthIndexSignal.StateCritical
                    : (_currentPressureLevel > 0 ? SystemHealthIndexSignal.StateWarning : SystemHealthIndexSignal.StateStable),
                Flags = _currentPressureLevel >= 3 ? SystemHealthIndexSignal.FlagAdrenaline : (byte)0
            };
            GlobalSignals.Publish(in signal);
        }

        private static void WriteBlackBox(int frame, ushort flags)
        {
            if (!_blackBox.IsCreated)
                return;

            float timeDilation = SystemDispatcher.ActiveRuntimeInstance != null
                ? SystemDispatcher.ActiveRuntimeInstance.TimeDilationScalar
                : 1f;
            _blackBox[_blackBoxCursor] = new HomeostasisBlackBoxEntry
            {
                Frame = unchecked((uint)frame),
                SystemHealthIndex01 = _systemHealthIndex01,
                KillSwitchMask = _currentKillSwitchMask,
                FpsEwma = _globalHardwareMetrics[(int)HardwareMetricSlot.FpsEwma],
                JitterSigmaMs = _globalHardwareMetrics[(int)HardwareMetricSlot.JitterSigma],
                CpuTempC = _globalHardwareMetrics[(int)HardwareMetricSlot.CpuTempC],
                GpuUtil01 = _globalHardwareMetrics[(int)HardwareMetricSlot.GpuUtil01],
                BatteryLife01 = _globalHardwareMetrics[(int)HardwareMetricSlot.BatteryLife01],
                PressureLevel = _currentPressureLevel,
                FoveatedPressureTier = _currentPressureLevel >= 2 ? (byte)3 : (byte)0,
                Flags = flags,
                TimeDilationScalar = timeDilation
            };
            _blackBoxCursor++;
            if (_blackBoxCursor >= BlackBoxCapacity)
                _blackBoxCursor = 0;
        }

        private static void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !_blackBox.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, BlackBoxDumpFileName);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x484F4D42u);
                    writer.Write(1);
                    writer.Write(BlackBoxCapacity);
                    writer.Write(_blackBoxCursor);
                    for (int i = 0; i < BlackBoxCapacity; i++)
                    {
                        HomeostasisBlackBoxEntry entry = _blackBox[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.SystemHealthIndex01);
                        writer.Write(entry.KillSwitchMask);
                        writer.Write(entry.FpsEwma);
                        writer.Write(entry.JitterSigmaMs);
                        writer.Write(entry.CpuTempC);
                        writer.Write(entry.GpuUtil01);
                        writer.Write(entry.BatteryLife01);
                        writer.Write(entry.PressureLevel);
                        writer.Write(entry.FoveatedPressureTier);
                        writer.Write(entry.Flags);
                        writer.Write(entry.TimeDilationScalar);
                        writer.Write(entry.Reserved0);
                        writer.Write(entry.Reserved1);
                    }
                }
            }
            catch (Exception)
            {
                // Fault-path only: black-box dumping must never crash the runtime while already degraded.
            }
        }

        private static long ResolvePersistentBytes()
        {
            long hardwareMetricsBytes = _globalHardwareMetricsVaultOwned
                ? 0L
                : (long)HardwareMetricSlot.Count * sizeof(float);
            return hardwareMetricsBytes +
                   ((long)FrameTimeWindow * sizeof(float)) +
                   ((long)BlackBoxCapacity * Marshal.SizeOf<HomeostasisBlackBoxEntry>());
        }

        private static NativeArray<float> ResolveHardwareMetricsBuffer()
        {
            _globalHardwareMetricsVaultOwned = false;
            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault != null)
            {
                NativeArray<float> vaultMetrics = dataVault.GetBuffer<float>(
                    BufferID.HardwareMetrics,
                    (int)HardwareMetricSlot.Count,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.ClearMemory);
                if (vaultMetrics.IsCreated)
                {
                    _globalHardwareMetricsVaultOwned = true;
                    return vaultMetrics;
                }
            }

            return H8Memory.Allocate<float>(
                (int)HardwareMetricSlot.Count,
                SystemID.HardwareHomeostasis,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
        }

        private static uint FoldMaskToUInt(ulong mask)
        {
            return unchecked((uint)mask ^ (uint)(mask >> 32));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            float batteryWeight = lowTier != 0 ? 0.4f : 0.2f;
            return math.saturate(jitter01 * 0.4f + temp01 * 0.4f + batteryPressure01 * batteryWeight);
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
            float batteryWeight = lowTier ? 0.4f : 0.2f;
            return math.saturate(jitter01 * 0.4f + temp01 * 0.4f + batteryPressure01 * batteryWeight);
        }
    }
}
