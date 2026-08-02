using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Tools;
using Hecton8.UI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core.Hardware
{
    /// <summary>
    /// FrostTick-owned hardware thermal/battery watchdog. Android/portable polling never enters frame ticks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9948)]
    public sealed class HardwareThermalService :
        MonoBehaviour,
        IHardwareThermalService,
        IFrostTickable,
        IUpdatable,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001HardwareThermalServiceSignalPushDropCount;
        private const int BlackBoxFrameCount = 300;
        private const int HardwareThermalTelemetryEntryBytes = 64;
        private const short UnknownTemperatureTenthsCelsius = short.MinValue;
        private const byte UnknownBatteryStatus = 0;
        private const byte BatteryStatusDischargingAndroid = 3;
        private const byte BatteryPercentUnknown = 100;
        private const short WarmTemperatureTenthsCelsius = 390;
        private const short ThrottlingTemperatureTenthsCelsius = 430;
        private const short CriticalTemperatureTenthsCelsius = 480;
        private const byte LowBatteryPercent = 15;
        private const byte CriticalBatteryPercent = 5;
        private const byte ThermalStatusModerate = 2;
        private const byte ThermalStatusSevere = 3;
        private const byte ThermalStatusEmergency = 5;
        private const int AndroidThermalFeatureHeadroom = 1 << 0;
        private const int AndroidThermalFeatureStatus = 1 << 1;
        private const uint Lane4VfxKillSwitchMask = 1u << 4;
        private const float HeadroomWarmPressure01 = 0.85f;
        private const float HeadroomSeverePressure01 = 1.00f;
        private const int RecoverySamplesToClear = 2;
        private const float ThermalFreezeDistanceMeters = 100f;
        private const uint SourceHash = 0x54484452u;
        private const uint ThermalContextHash = 0x54484552u;
        private const uint BatteryContextHash = 0x42415454u;
        private const string SuitThermalThrottlingMessage = "SUIT THERMAL THROTTLING";
        private const string SuitThermalCriticalMessage = "SUIT THERMAL CRITICAL";
        private const uint ActionLane4Vfx = 1u << 0;
        private const uint ActionFoveatedFreeze = 1u << 1;
        private const uint ActionRenderScale = 1u << 2;
        private const uint ActionSlowTick = 1u << 3;
        private const uint ActionHapticMute = 1u << 4;
        private const uint ActionVisorWarning = 1u << 5;
        private const int HardwareThermalDumpHeaderBytes = 16;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_HARDWARE_THROTTLING_DIRECTOR_ThermalService.bin";

        private static bool s_sceneHooked;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass s_unityPlayerClass;
        private static AndroidJavaClass s_androidVersionClass;
        private static AndroidJavaObject s_unityActivity;
        private static AndroidJavaObject s_powerManager;
        private static AndroidJavaObject s_batteryChangedFilter;
        private static int s_androidSdkInt;
        private static int s_androidThermalFeatureFlags;
        private static bool s_androidColdBridgeReady;
        private static bool s_androidColdBridgeFaulted;
#endif

        private IDataVault _dataVault;
        private VaultGenerationHandle<byte> _thermalSeverityHandle;
        private VaultGenerationHandle<HardwareThermalTelemetryEntry> _blackBoxHandle;
        private HardwareThermalSnapshot _snapshot;
        private uint _sequence;
        private int _blackBoxCursor;
        private int _recoverySampleCount;
        private byte _severity;
        private byte _previousSeverity;
        private byte _batteryPercent = BatteryPercentUnknown;
        private byte _batteryStatus = UnknownBatteryStatus;
        private byte _thermalStatus;
        private short _temperatureTenthsCelsius = UnknownTemperatureTenthsCelsius;
        private uint _lastActionMask;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _registeredFrostTick;
        private bool _registeredFrameTick;
        private bool _hotSwapRegistered;
        private bool _policyInitialized;
        private bool _throttlingPolicyApplied;
        private bool _criticalPolicyApplied;
        private bool _hapticMuteApplied;
        private bool _criticalDumped;
        private byte _lastThermalNotificationSeverity;
        private IFoveatedSimulationDirector _foveatedDirector;
        private SystemDispatcher _dispatcher;
        private ToolHapticsRuntime _haptics;
        private byte _fallbackBatteryPercentSnapshot = BatteryPercentUnknown;
        private byte _fallbackBatteryStatusSnapshot = UnknownBatteryStatus;

        public byte CurrentSeverity => _severity;
        public byte BatteryPercent => _batteryPercent;
        public uint Sequence => _sequence;
        public NativeArray<byte>.ReadOnly ThermalSeverity => TryReadThermalSeverity(out NativeArray<byte>.ReadOnly severity)
            ? severity
            : default;

        [StructLayout(LayoutKind.Explicit, Size = HardwareThermalTelemetryEntryBytes)]
        private struct HardwareThermalTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint Sequence;
            [FieldOffset(8)]
            public uint ActionMask;
            [FieldOffset(12)]
            public short TemperatureTenthsCelsius;
            [FieldOffset(14)]
            public byte Severity;
            [FieldOffset(15)]
            public byte BatteryPercent;
            [FieldOffset(16)]
            public byte BatteryStatus;
            [FieldOffset(17)]
            public byte ThermalStatus;
            [FieldOffset(18)]
            public byte Flags;
            [FieldOffset(19)]
            public byte Reserved0;
            [FieldOffset(20)]
            public byte Reserved1;
            [FieldOffset(21)]
            public byte Reserved2;
            [FieldOffset(22)]
            public byte Reserved3;
            [FieldOffset(23)]
            public byte Reserved4;
            [FieldOffset(24)]
            public ulong ReservedPadding0;
            [FieldOffset(32)]
            public ulong ReservedPadding1;
            [FieldOffset(40)]
            public ulong ReservedPadding2;
            [FieldOffset(48)]
            public ulong ReservedPadding3;
            [FieldOffset(56)]
            public ulong ReservedPadding4;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_sceneHooked = false;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
#if UNITY_ANDROID && !UNITY_EDITOR
            DisposeAndroidColdBridge();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            EnsureRuntimeInstanceCold();
            if (s_sceneHooked)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            s_sceneHooked = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRuntimeInstanceCold();
        }

        private static void EnsureRuntimeInstanceCold()
        {
            IHardwareThermalService registered = GlobalRegistry.HardwareThermal;
            if (IsHardwareThermalRuntimeUsable(registered))
                return;

            HardwareThermalService staleRuntime = registered as HardwareThermalService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterHardwareThermalService(registered);
                staleRuntime._serviceRegistered = false;
            }
            else if (!ReferenceEquals(registered, null))
            {
                return;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            GameObject serviceObject = new GameObject("[HardwareThermalService]");
            serviceObject.AddComponent<HardwareThermalService>();
        }

        public bool TryGetSnapshot(out HardwareThermalSnapshot snapshot)
        {
            snapshot = _snapshot;
            return _sequence != 0u;
        }

        public void ForceColdSample()
        {
            RefreshSystemInfoFallbackSnapshot();
            SampleAndApplyCold();
        }

        public void FrostTick()
        {
            RefreshSystemInfoFallbackSnapshot();
            SampleAndApplyCold();
        }

        public void Tick(float deltaTime)
        {
            WriteBlackBox(Hecton8.Core.SystemDispatcher.CurrentFrameId);
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            if (!TryRegisterService())
                return;

            EnsureNativeState();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!TryRegisterService())
                return;

            RebindCachedServicesCold();
            EnsureNativeState();
            TryRegisterHotSwap();
            TryRegisterFrameTick();
            TryRegisterFrostTick();
            RefreshSystemInfoFallbackSnapshot();
            SampleAndApplyCold();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            Dispose();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            Dispose();
        }

        public void Dispose()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterFrostTick();
            TryUnregisterFrameTick();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ReleaseThermalPolicies();
            DisposeNativeState();
        }

        private void SampleAndApplyCold()
        {
            byte rawBatteryPercent = BatteryPercentUnknown;
            byte rawBatteryStatus = UnknownBatteryStatus;
            byte rawThermalStatus = 0;
            short rawTemperature = UnknownTemperatureTenthsCelsius;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!TrySampleAndroidCold(out rawBatteryPercent, out rawBatteryStatus, out rawThermalStatus, out rawTemperature))
#endif
            {
                ReadSystemInfoFallbackSnapshot(out rawBatteryPercent, out rawBatteryStatus);
            }

            byte mappedSeverity = ResolveSeverity(rawBatteryPercent, rawBatteryStatus, rawThermalStatus, rawTemperature);
            byte stableSeverity = ApplyRecoveryHysteresis(mappedSeverity);
            byte previousSeverity = _severity;
            _previousSeverity = previousSeverity;
            _severity = stableSeverity;
            _batteryPercent = rawBatteryPercent;
            _batteryStatus = rawBatteryStatus;
            _thermalStatus = rawThermalStatus;
            _temperatureTenthsCelsius = rawTemperature;
            _sequence++;
            PlatformBatteryWatchdog.SampleAndApply(this);

            if (TryAcquireThermalSeverityWriteView(out NativeArray<byte> thermalSeverity, out IDataVault severityWriteVault))
            {
                try
                {
                    thermalSeverity[0] = _severity;
                }
                finally
                {
                    ReleaseThermalSeverityWriteView(severityWriteVault);
                }
            }

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            ApplyThermalPoliciesCold();
            PublishSignalsCold(frame, previousSeverity);
            PublishTelemetryCold();
            WriteSnapshot(frame);
            WriteBlackBox(frame);

            if ((_severity >= (byte)HardwareThermalSeverity.Critical || rawTemperature == short.MaxValue) && !_criticalDumped)
            {
                _criticalDumped = true;
                DumpBlackBoxCold();
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool TrySampleAndroidCold(
            out byte batteryPercent,
            out byte batteryStatus,
            out byte thermalStatus,
            out short temperatureTenthsCelsius)
        {
            batteryPercent = BatteryPercentUnknown;
            batteryStatus = UnknownBatteryStatus;
            thermalStatus = 0;
            temperatureTenthsCelsius = UnknownTemperatureTenthsCelsius;

            try
            {
                EnsureAndroidColdBridge();
                if (!s_androidColdBridgeReady || s_androidColdBridgeFaulted || s_unityActivity == null)
                    return false;

                using (AndroidJavaObject intent = s_unityActivity.Call<AndroidJavaObject>("registerReceiver", null, s_batteryChangedFilter))
                {
                    if (intent != null)
                    {
                        int level = intent.Call<int>("getIntExtra", "level", -1);
                        int scale = intent.Call<int>("getIntExtra", "scale", -1);
                        int status = intent.Call<int>("getIntExtra", "status", 0);
                        int temperature = intent.Call<int>("getIntExtra", "temperature", (int)UnknownTemperatureTenthsCelsius);
                        batteryStatus = (byte)math.clamp(status, 0, byte.MaxValue);
                        if (level >= 0 && scale > 0)
                        {
                            float percent = level * math.rcp(scale) * 100f;
                            batteryPercent = (byte)math.clamp((int)math.round(percent), 0, 100);
                        }

                        if (temperature > short.MinValue && temperature < short.MaxValue)
                            temperatureTenthsCelsius = (short)temperature;
                    }
                }

                AndroidJavaObject powerManager = s_powerManager;
                if (powerManager != null)
                {
                    byte headroomStatus = 0;
                    if ((s_androidThermalFeatureFlags & AndroidThermalFeatureHeadroom) != 0)
                    {
                        float headroom = powerManager.Call<float>("getThermalHeadroom", 30);
                        if (math.isfinite(headroom))
                            headroomStatus = MapThermalHeadroomToStatus(headroom);
                    }

                    byte currentStatus = 0;
                    if ((s_androidThermalFeatureFlags & AndroidThermalFeatureStatus) != 0)
                    {
                        int status = powerManager.Call<int>("getCurrentThermalStatus");
                        currentStatus = (byte)math.clamp(status, 0, byte.MaxValue);
                    }

                    thermalStatus = MaxByte(headroomStatus, currentStatus);
                }

                return true;
            }
            catch (Exception)
            {
                s_androidColdBridgeFaulted = true;
                batteryPercent = BatteryPercentUnknown;
                batteryStatus = UnknownBatteryStatus;
                thermalStatus = 0;
                temperatureTenthsCelsius = UnknownTemperatureTenthsCelsius;
                return false;
            }
        }

        private static void EnsureAndroidColdBridge()
        {
            if (s_androidColdBridgeReady || s_androidColdBridgeFaulted)
                return;

            try
            {
                s_unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                s_androidVersionClass = new AndroidJavaClass("android.os.Build$VERSION");
                s_androidSdkInt = s_androidVersionClass.GetStatic<int>("SDK_INT");
                s_unityActivity = s_unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
                s_batteryChangedFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
                s_powerManager = s_unityActivity != null
                    ? s_unityActivity.Call<AndroidJavaObject>("getSystemService", "power")
                    : null;
                s_androidThermalFeatureFlags = 0;
                if (s_powerManager != null && s_androidSdkInt >= 30)
                    s_androidThermalFeatureFlags |= AndroidThermalFeatureHeadroom;
                if (s_powerManager != null && s_androidSdkInt >= 29)
                    s_androidThermalFeatureFlags |= AndroidThermalFeatureStatus;
                s_androidColdBridgeReady = s_unityActivity != null && s_batteryChangedFilter != null;
            }
            catch (Exception)
            {
                DisposeAndroidColdBridge();
                s_androidColdBridgeFaulted = true;
            }
        }

        private static void DisposeAndroidColdBridge()
        {
            s_powerManager?.Dispose();
            s_batteryChangedFilter?.Dispose();
            s_unityActivity?.Dispose();
            s_androidVersionClass?.Dispose();
            s_unityPlayerClass?.Dispose();
            s_powerManager = null;
            s_batteryChangedFilter = null;
            s_unityActivity = null;
            s_androidVersionClass = null;
            s_unityPlayerClass = null;
            s_androidSdkInt = 0;
            s_androidThermalFeatureFlags = 0;
            s_androidColdBridgeReady = false;
            s_androidColdBridgeFaulted = false;
        }

        private static byte MapThermalHeadroomToStatus(float headroom)
        {
            float pressure = math.max(0f, headroom);
            if (pressure >= HeadroomSeverePressure01)
                return ThermalStatusSevere;
            if (pressure >= HeadroomWarmPressure01)
                return ThermalStatusModerate;
            return 0;
        }
#endif

        private static byte MaxByte(byte a, byte b)
        {
            return a > b ? a : b;
        }

        private void RefreshSystemInfoFallbackSnapshot()
        {
            byte batteryPercent = BatteryPercentUnknown;
            byte batteryStatus = (byte)SystemInfo.batteryStatus;
            float level = SystemInfo.batteryLevel;
            if (math.isfinite(level) && level >= 0f)
                batteryPercent = (byte)math.clamp((int)math.round(math.saturate(level) * 100f), 0, 100);

            _fallbackBatteryPercentSnapshot = batteryPercent;
            _fallbackBatteryStatusSnapshot = batteryStatus;
        }

        private void ReadSystemInfoFallbackSnapshot(out byte batteryPercent, out byte batteryStatus)
        {
            batteryPercent = _fallbackBatteryPercentSnapshot;
            batteryStatus = _fallbackBatteryStatusSnapshot;
        }

        private byte ApplyRecoveryHysteresis(byte mappedSeverity)
        {
            if (mappedSeverity >= _severity)
            {
                _recoverySampleCount = 0;
                return mappedSeverity;
            }

            if (_recoverySampleCount < RecoverySamplesToClear)
            {
                _recoverySampleCount++;
                return _severity;
            }

            _recoverySampleCount = 0;
            return mappedSeverity;
        }

        private static byte ResolveSeverity(
            byte batteryPercent,
            byte batteryStatus,
            byte thermalStatus,
            short temperatureTenthsCelsius)
        {
            byte severity = (byte)HardwareThermalSeverity.Cool;
            if (thermalStatus >= ThermalStatusEmergency ||
                temperatureTenthsCelsius >= CriticalTemperatureTenthsCelsius ||
                IsDischarging(batteryStatus) && batteryPercent > 0 && batteryPercent < CriticalBatteryPercent)
            {
                severity = (byte)HardwareThermalSeverity.Critical;
            }
            else if (thermalStatus >= ThermalStatusSevere ||
                     temperatureTenthsCelsius >= ThrottlingTemperatureTenthsCelsius ||
                     IsDischarging(batteryStatus) && batteryPercent > 0 && batteryPercent < LowBatteryPercent)
            {
                severity = (byte)HardwareThermalSeverity.Throttling;
            }
            else if (thermalStatus >= ThermalStatusModerate ||
                     temperatureTenthsCelsius >= WarmTemperatureTenthsCelsius ||
                     IsDischarging(batteryStatus) && batteryPercent > 0 && batteryPercent < 30)
            {
                severity = (byte)HardwareThermalSeverity.Warm;
            }

            return severity;
        }

        private static bool IsDischarging(byte batteryStatus)
        {
            return batteryStatus == BatteryStatusDischargingAndroid ||
                   batteryStatus == (byte)BatteryStatus.Discharging;
        }

        private void ApplyThermalPoliciesCold()
        {
            bool throttling = _severity >= (byte)HardwareThermalSeverity.Throttling;
            bool critical = _severity >= (byte)HardwareThermalSeverity.Critical;
            bool hapticMute = _batteryPercent > 0 && _batteryPercent < LowBatteryPercent;
            uint actionMask = 0u;
            if (throttling)
                actionMask |= ActionLane4Vfx | ActionFoveatedFreeze | ActionRenderScale | ActionVisorWarning;
            if (critical)
                actionMask |= ActionSlowTick;
            if (hapticMute)
                actionMask |= ActionHapticMute;

            _lastActionMask = actionMask;

            if (!_policyInitialized || throttling != _throttlingPolicyApplied)
            {
                SignalBusRegistry.SetSystemKillSwitchBits(Lane4VfxKillSwitchMask, throttling, SourceHash);

                IFoveatedSimulationDirector foveated = _foveatedDirector;
                if (foveated != null)
                    foveated.SetThermalFreezeDistanceOverride(throttling, ThermalFreezeDistanceMeters);

                _throttlingPolicyApplied = throttling;
            }

            if (!_policyInitialized || critical != _criticalPolicyApplied)
            {
                SystemDispatcher dispatcher = _dispatcher;
                if (dispatcher != null)
                    dispatcher.SetThermalCriticalSlowTick(critical);

                _criticalPolicyApplied = critical;
            }

            if (!_policyInitialized || hapticMute != _hapticMuteApplied)
            {
                ToolHapticsRuntime.SetPowerSaveMuteGlobal(hapticMute);
                ToolHapticsRuntime haptics = _haptics;
                if (haptics != null)
                    haptics.SetPowerSaveMute(hapticMute);

                _hapticMuteApplied = hapticMute;
            }

            PublishThermalNotificationIfNeeded(throttling, critical);

            _policyInitialized = true;
        }

        private void PublishThermalNotificationIfNeeded(bool throttling, bool critical)
        {
            if (!throttling)
            {
                _lastThermalNotificationSeverity = 0;
                return;
            }

            byte targetSeverity = (byte)(critical
                ? HardwareThermalSeverity.Critical
                : HardwareThermalSeverity.Throttling);
            if (_lastThermalNotificationSeverity >= targetSeverity)
                return;

            if (critical)
                NotificationEvents.TryPushCritical(SuitThermalCriticalMessage.AsSpan());
            else
                NotificationEvents.TryPushWarning(SuitThermalThrottlingMessage.AsSpan());

            _lastThermalNotificationSeverity = targetSeverity;
        }

        private void ReleaseThermalPolicies()
        {
            if (!_policyInitialized)
                return;

            SignalBusRegistry.SetSystemKillSwitchBits(Lane4VfxKillSwitchMask, false, SourceHash);
            IFoveatedSimulationDirector foveated = _foveatedDirector;
            if (foveated != null)
                foveated.SetThermalFreezeDistanceOverride(false, ThermalFreezeDistanceMeters);

            SystemDispatcher dispatcher = _dispatcher;
            if (dispatcher != null)
                dispatcher.SetThermalCriticalSlowTick(false);

            ToolHapticsRuntime haptics = _haptics;
            ToolHapticsRuntime.SetPowerSaveMuteGlobal(false);
            if (haptics != null)
                haptics.SetPowerSaveMute(false);

            _policyInitialized = false;
            _throttlingPolicyApplied = false;
            _criticalPolicyApplied = false;
            _hapticMuteApplied = false;
            _lastThermalNotificationSeverity = 0;
        }

        private void PublishSignalsCold(uint frame, byte previousSeverity)
        {
            if (previousSeverity != _severity)
            {
                ThermalStateChangedSignal thermalSignal = new ThermalStateChangedSignal
                {
                    SourceHash = SourceHash,
                    Frame = frame,
                    Sequence = _sequence,
                    Severity = _severity,
                    PreviousSeverity = previousSeverity,
                    ThermalStatus = _thermalStatus,
                    Flags = 0,
                    TemperatureTenthsCelsius = _temperatureTenthsCelsius,
                    BatteryPercent = _batteryPercent,
                    ActionMask = _lastActionMask
                };
                SignalBus<ThermalStateChangedSignal>.TryPushTracked(in thermalSignal, ref s_x001HardwareThermalServiceSignalPushDropCount);
            }

            BatteryLevelSignal batterySignal = new BatteryLevelSignal
            {
                SourceHash = SourceHash,
                Frame = frame,
                Sequence = _sequence,
                BatteryPercent = _batteryPercent,
                BatteryStatus = _batteryStatus,
                Flags = (byte)(_hapticMuteApplied ? 1 : 0),
                ActionMask = _lastActionMask
            };
            SignalBus<BatteryLevelSignal>.TryPushTracked(in batterySignal, ref s_x001HardwareThermalServiceSignalPushDropCount);
        }

        private void PublishTelemetryCold()
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                ThermalContextHash,
                BatteryContextHash,
                _batteryPercent);

            if (_lastActionMask != 0u)
                GlobalTelemetryBus.PublishSystemDegradation(ThermalContextHash, _lastActionMask, _severity);
        }

        private void WriteSnapshot(uint frame)
        {
            _snapshot = new HardwareThermalSnapshot
            {
                Severity = _severity,
                PreviousSeverity = _previousSeverity,
                BatteryPercent = _batteryPercent,
                BatteryStatus = _batteryStatus,
                ThermalStatus = _thermalStatus,
                Flags = (byte)(_hapticMuteApplied ? 1 : 0),
                TemperatureTenthsCelsius = _temperatureTenthsCelsius,
                Sequence = _sequence,
                Frame = frame,
                ActionMask = _lastActionMask
            };
        }

        private void WriteBlackBox(uint frame)
        {
            if (!TryResolveThermalBlackBoxWriteViewCurrentPhase(out NativeArray<HardwareThermalTelemetryEntry> blackBox))
                return;

            int index = _blackBoxCursor;
            blackBox[index] = new HardwareThermalTelemetryEntry
            {
                Frame = frame,
                Sequence = _sequence,
                ActionMask = _lastActionMask,
                TemperatureTenthsCelsius = _temperatureTenthsCelsius,
                Severity = _severity,
                BatteryPercent = _batteryPercent,
                BatteryStatus = _batteryStatus,
                ThermalStatus = _thermalStatus,
                Flags = (byte)(_hapticMuteApplied ? 1 : 0)
            };

            index++;
            if (index >= BlackBoxFrameCount)
                index = 0;
            _blackBoxCursor = index;
        }

        private unsafe void DumpBlackBoxCold()
        {
            if (!TryReadThermalBlackBox(out NativeArray<HardwareThermalTelemetryEntry>.ReadOnly blackBox))
                return;

            int byteCount = HardwareThermalDumpHeaderBytes + BlackBoxFrameCount * HardwareThermalTelemetryEntryBytes;
            NativeArray<byte> payload = default;
            const string dumpPayloadLabel = "hardwareThermalBlackBoxDumpPayload";
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HardwareThermalService),
                    dumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int writeCursor = 0;
                WriteUInt32LittleEndian(destination, ref writeCursor, _sequence);
                WriteInt32LittleEndian(destination, ref writeCursor, _blackBoxCursor);
                WriteInt32LittleEndian(destination, ref writeCursor, BlackBoxFrameCount);
                WriteInt32LittleEndian(destination, ref writeCursor, HardwareThermalTelemetryEntryBytes);

                for (int i = 0; i < BlackBoxFrameCount; i++)
                {
                    int index = _blackBoxCursor + i;
                    if (index >= BlackBoxFrameCount)
                        index -= BlackBoxFrameCount;

                    WriteThermalTelemetryEntry(destination, ref writeCursor, blackBox[index]);
                }

                NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, writeCursor);
            }
            catch (Exception)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(HardwareThermalService), dumpPayloadLabel);
            }
        }

        private static unsafe void WriteThermalTelemetryEntry(byte* destination, ref int cursor, HardwareThermalTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Sequence);
            WriteUInt32LittleEndian(destination, ref cursor, entry.ActionMask);
            WriteInt16LittleEndian(destination, ref cursor, entry.TemperatureTenthsCelsius);
            WriteByte(destination, ref cursor, entry.Severity);
            WriteByte(destination, ref cursor, entry.BatteryPercent);
            WriteByte(destination, ref cursor, entry.BatteryStatus);
            WriteByte(destination, ref cursor, entry.ThermalStatus);
            WriteByte(destination, ref cursor, entry.Flags);
            WriteByte(destination, ref cursor, entry.Reserved0);
            WriteByte(destination, ref cursor, entry.Reserved1);
            WriteByte(destination, ref cursor, entry.Reserved2);
            WriteByte(destination, ref cursor, entry.Reserved3);
            WriteByte(destination, ref cursor, entry.Reserved4);
            WriteUInt64LittleEndian(destination, ref cursor, entry.ReservedPadding0);
            WriteUInt64LittleEndian(destination, ref cursor, entry.ReservedPadding1);
            WriteUInt64LittleEndian(destination, ref cursor, entry.ReservedPadding2);
            WriteUInt64LittleEndian(destination, ref cursor, entry.ReservedPadding3);
            WriteUInt64LittleEndian(destination, ref cursor, entry.ReservedPadding4);
        }

        private static unsafe void WriteByte(byte* destination, ref int cursor, byte value)
        {
            destination[cursor] = value;
            cursor++;
        }

        private static unsafe void WriteInt16LittleEndian(byte* destination, ref int cursor, short value)
        {
            WriteUInt16LittleEndian(destination, ref cursor, unchecked((ushort)value));
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)value));
        }

        private static unsafe void WriteUInt16LittleEndian(byte* destination, ref int cursor, ushort value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            cursor += sizeof(ushort);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, ref int cursor, uint value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            cursor += sizeof(uint);
        }

        private static unsafe void WriteUInt64LittleEndian(byte* destination, ref int cursor, ulong value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            destination[cursor + 4] = (byte)(value >> 32);
            destination[cursor + 5] = (byte)(value >> 40);
            destination[cursor + 6] = (byte)(value >> 48);
            destination[cursor + 7] = (byte)(value >> 56);
            cursor += sizeof(ulong);
        }

        private void EnsureNativeState()
        {
            if (OpenOrAcquireThermalSeverityWriteViewForOwnerRoute(out _, out IDataVault severityWriteVault))
            {
                try
                {
                }
                finally
                {
                    ReleaseThermalSeverityWriteView(severityWriteVault);
                }
            }

            if (OpenOrAcquireThermalBlackBoxWriteViewForOwnerRoute(out _, out IDataVault blackBoxWriteVault))
            {
                try
                {
                }
                finally
                {
                    ReleaseThermalBlackBoxWriteView(blackBoxWriteVault);
                }
            }
        }

        private void DisposeNativeState()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _thermalSeverityHandle);
                ReleaseVaultHandle(vault, ref _blackBoxHandle);
            }

            _dataVault = null;
            _blackBoxCursor = 0;
        }

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            RebindCachedService(serviceSlot, currentService);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            RebindCachedService(serviceSlot, currentService);
        }

        private bool TryAcquireThermalSeverityWriteView(out NativeArray<byte> severity)
        {
            return TryAcquireThermalSeverityWriteView(out severity, out _);
        }

        private bool TryAcquireThermalSeverityWriteView(out NativeArray<byte> severity, out IDataVault writeVault)
        {
            severity = default;
            writeVault = _dataVault;
            IDataVault vault = writeVault;
            if (vault == null || _thermalSeverityHandle.BufferID == 0u)
            {
                writeVault = null;
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis, out severity))
            {
                writeVault = null;
                return false;
            }

            bool handedOff = false;
            try
            {
                if (severity.IsCreated && severity.Length >= 1)
                {
                    handedOff = true;
                    return true;
                }

                severity = default;
                return false;
            }
            finally
            {
                if (!handedOff)
                {
                    vault.ReleaseWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis);
                    writeVault = null;
                }
            }
        }

        private bool OpenOrAcquireThermalSeverityWriteViewForOwnerRoute(out NativeArray<byte> severity)
        {
            return OpenOrAcquireThermalSeverityWriteViewForOwnerRoute(out severity, out _);
        }

        private bool OpenOrAcquireThermalSeverityWriteViewForOwnerRoute(out NativeArray<byte> severity, out IDataVault writeVault)
        {
            severity = default;
            writeVault = null;
            if (!EnsureThermalSeverityHandleForOwnerRoute())
                return false;

            return TryAcquireThermalSeverityWriteView(out severity, out writeVault);
        }

        private bool EnsureThermalSeverityHandleForOwnerRoute()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (_thermalSeverityHandle.BufferID != 0u)
                return true;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _thermalSeverityHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.HardwareThermalSeverity,
                1,
                SystemID.HardwareHomeostasis,
                NativeArrayOptions.ClearMemory);

            return _thermalSeverityHandle.BufferID != 0u;
        }

        private bool ReleaseThermalSeverityWriteView(IDataVault writeVault)
        {
            return writeVault != null &&
                   _thermalSeverityHandle.BufferID != 0u &&
                   writeVault.ReleaseWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis);
        }

        private bool TryReadThermalSeverity(out NativeArray<byte>.ReadOnly severity)
        {
            severity = default;
            IDataVault vault = _dataVault;
            if (vault == null || _thermalSeverityHandle.BufferID == 0u)
                return false;

            return vault.TryReadOnlyHandle(in _thermalSeverityHandle, out severity) &&
                   severity.IsCreated &&
                   severity.Length >= 1;
        }

        private bool TryReadThermalBlackBox(out NativeArray<HardwareThermalTelemetryEntry>.ReadOnly blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            if (vault == null || _blackBoxHandle.BufferID == 0u)
                return false;

            return vault.TryReadOnlyHandle(in _blackBoxHandle, out blackBox) &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxFrameCount;
        }

        private bool TryAcquireThermalBlackBoxWriteView(out NativeArray<HardwareThermalTelemetryEntry> blackBox)
        {
            return TryAcquireThermalBlackBoxWriteView(out blackBox, out _);
        }

        private bool TryAcquireThermalBlackBoxWriteView(out NativeArray<HardwareThermalTelemetryEntry> blackBox, out IDataVault writeVault)
        {
            blackBox = default;
            writeVault = _dataVault;
            IDataVault vault = writeVault;
            if (vault == null || _blackBoxHandle.BufferID == 0u)
            {
                writeVault = null;
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis, out blackBox))
            {
                writeVault = null;
                return false;
            }

            bool handedOff = false;
            try
            {
                if (blackBox.IsCreated && blackBox.Length >= BlackBoxFrameCount)
                {
                    handedOff = true;
                    return true;
                }

                blackBox = default;
                return false;
            }
            finally
            {
                if (!handedOff)
                {
                    vault.ReleaseWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis);
                    writeVault = null;
                }
            }
        }

        private bool TryResolveThermalBlackBoxWriteViewCurrentPhase(out NativeArray<HardwareThermalTelemetryEntry> blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            if (vault == null || _blackBoxHandle.BufferID == 0u)
                return false;

            return vault.TryResolveHandle(in _blackBoxHandle, out blackBox) &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxFrameCount;
        }

        private bool OpenOrAcquireThermalBlackBoxWriteViewForOwnerRoute(out NativeArray<HardwareThermalTelemetryEntry> blackBox)
        {
            return OpenOrAcquireThermalBlackBoxWriteViewForOwnerRoute(out blackBox, out _);
        }

        private bool OpenOrAcquireThermalBlackBoxWriteViewForOwnerRoute(out NativeArray<HardwareThermalTelemetryEntry> blackBox, out IDataVault writeVault)
        {
            blackBox = default;
            writeVault = null;
            if (!EnsureThermalBlackBoxHandleForOwnerRoute())
                return false;

            return TryAcquireThermalBlackBoxWriteView(out blackBox, out writeVault);
        }

        private bool EnsureThermalBlackBoxHandleForOwnerRoute()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (_blackBoxHandle.BufferID != 0u)
                return true;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _blackBoxHandle = vault.EnsureGenerationHandle<HardwareThermalTelemetryEntry>(
                BufferID.HardwareThermalBlackBox,
                BlackBoxFrameCount,
                SystemID.HardwareHomeostasis,
                NativeArrayOptions.ClearMemory);

            return _blackBoxHandle.BufferID != 0u;
        }

        private bool ReleaseThermalBlackBoxWriteView(IDataVault writeVault)
        {
            return writeVault != null &&
                   _blackBoxHandle.BufferID != 0u &&
                   writeVault.ReleaseWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IHardwareThermalService registered = GlobalRegistry.HardwareThermal;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                HardwareThermalService staleRuntime = registered as HardwareThermalService;
                if (ReferenceEquals(staleRuntime, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                GlobalRegistry.UnregisterHardwareThermalService(registered);
                staleRuntime._serviceRegistered = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            if (GlobalRegistry.Phase == GlobalRegistry.RegistryPhase.Ready)
                GlobalRegistry.ReplaceHardwareThermalService(this);
            else
                GlobalRegistry.RegisterHardwareThermalService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.HardwareThermal, this);
            _runtimeOwnerAborted = !_serviceRegistered;
            return _serviceRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            IHardwareThermalService registered = GlobalRegistry.HardwareThermal;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsHardwareThermalRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            HardwareThermalService staleRuntime = registered as HardwareThermalService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterHardwareThermalService(registered);
                staleRuntime._serviceRegistered = false;
            }

            return false;
        }

        private static bool IsHardwareThermalRuntimeUsable(IHardwareThermalService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            HardwareThermalService runtime = service as HardwareThermalService;
            return ReferenceEquals(runtime, null) ||
                   (runtime != null &&
                    runtime._serviceRegistered &&
                    runtime.isActiveAndEnabled &&
                    !runtime._runtimeOwnerAborted);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.HardwareThermal, this))
                GlobalRegistry.UnregisterHardwareThermalService(this);

            _serviceRegistered = false;
        }

        private void RebindCachedServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _foveatedDirector = GlobalRegistry.FoveatedSimulationDirector;
            _dispatcher = GlobalRegistry.Dispatcher;
            _haptics = GlobalRegistry.ToolHaptics;
        }

        private void RebindCachedService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FoveatedSimulationDirector)
            {
                _foveatedDirector = currentService as IFoveatedSimulationDirector;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DisposeNativeState();
                _dataVault = currentService as IDataVault;
                if (_dataVault != null && isActiveAndEnabled && _serviceRegistered)
                    EnsureNativeState();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterFrameTick();
                TryUnregisterFrostTick();
                _dispatcher = currentService as SystemDispatcher;
                if (currentService != null && isActiveAndEnabled && _serviceRegistered)
                {
                    TryRegisterFrameTick();
                    TryRegisterFrostTick();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ToolHapticsRuntime)
            {
                _haptics = currentService as ToolHapticsRuntime;
                bool hapticMute = _policyInitialized && _hapticMuteApplied;
                ToolHapticsRuntime.SetPowerSaveMuteGlobal(hapticMute);
                ToolHapticsRuntime haptics = _haptics;
                if (haptics != null)
                    haptics.SetPowerSaveMute(hapticMute);
            }
        }

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered)
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

        private void TryRegisterFrostTick()
        {
            if (_registeredFrostTick || _dispatcher == null)
                return;

            _registeredFrostTick = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterFrostTick()
        {
            if (!_registeredFrostTick)
                return;

            GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Core);
            _registeredFrostTick = false;
        }

        private void TryRegisterFrameTick()
        {
            if (_registeredFrameTick || _dispatcher == null)
                return;

            _registeredFrameTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterFrameTick()
        {
            if (!_registeredFrameTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredFrameTick = false;
        }
    }
}
