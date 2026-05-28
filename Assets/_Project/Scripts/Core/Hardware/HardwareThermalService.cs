using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Tools;
using Unity.Collections;
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
        private const float HeadroomWarmPressure01 = 0.85f;
        private const float HeadroomSeverePressure01 = 1.00f;
        private const int RecoverySamplesToClear = 2;
        private const float ThermalFreezeDistanceMeters = 100f;
        private const uint SourceHash = 0x54484452u;
        private const uint ThermalContextHash = 0x54484552u;
        private const uint BatteryContextHash = 0x42415454u;
        private const uint SuitThermalCriticalHash = 0x53544352u;
        private const uint ActionLane4Vfx = 1u << 0;
        private const uint ActionFoveatedFreeze = 1u << 1;
        private const uint ActionRenderScale = 1u << 2;
        private const uint ActionSlowTick = 1u << 3;
        private const uint ActionHapticMute = 1u << 4;
        private const uint ActionVisorWarning = 1u << 5;
        private const string DumpFileName = "Dump_HARDWARE_THROTTLING_DIRECTOR_ThermalService.bin";

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
        private bool _registeredFrostTick;
        private bool _registeredFrameTick;
        private bool _hotSwapRegistered;
        private bool _policyInitialized;
        private bool _throttlingPolicyApplied;
        private bool _criticalPolicyApplied;
        private bool _hapticMuteApplied;
        private bool _transientLowTierOverrideApplied;
        private bool _criticalDumped;
        private IFoveatedSimulationDirector _foveatedDirector;
        private SystemDispatcher _dispatcher;
        private ToolHapticsRuntime _haptics;

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
            if (GlobalRegistry.HardwareThermal != null)
                return;

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
            SampleAndApplyCold();
        }

        public void FrostTick()
        {
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

            IHardwareThermalService registered = GlobalRegistry.HardwareThermal;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            EnsureNativeState();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            RebindCachedServicesCold();
            EnsureNativeState();
            TryRegisterService();
            if (!_serviceRegistered)
                return;

            TryRegisterHotSwap();
            TryRegisterFrameTick();
            TryRegisterFrostTick();
            SampleAndApplyCold();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                Dispose();
                return;
            }

            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
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
                SampleSystemInfoFallbackCold(out rawBatteryPercent, out rawBatteryStatus);
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

            if (OpenOrAcquireThermalSeverityWriteView(out NativeArray<byte> thermalSeverity))
            {
                try
                {
                    thermalSeverity[0] = _severity;
                }
                finally
                {
                    ReleaseThermalSeverityWriteView();
                }
            }

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            ApplyThermalPoliciesCold(frame);
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

        private static void SampleSystemInfoFallbackCold(out byte batteryPercent, out byte batteryStatus)
        {
            batteryPercent = BatteryPercentUnknown;
            batteryStatus = (byte)SystemInfo.batteryStatus;
            float level = SystemInfo.batteryLevel;
            if (!math.isfinite(level) || level < 0f)
                return;

            batteryPercent = (byte)math.clamp((int)math.round(math.saturate(level) * 100f), 0, 100);
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

        private void ApplyThermalPoliciesCold(uint frame)
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
                GlobalRegistry.SetSystemKillSwitchBits(GlobalRegistry.SystemKillSwitchLane4VfxMask, throttling);

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
                ToolHapticsRuntime haptics = _haptics;
                if (haptics != null)
                    haptics.SetPowerSaveMute(hapticMute);

                _hapticMuteApplied = hapticMute;
            }

            if (throttling)
            {
                HUDNotificationSignal warning = new HUDNotificationSignal
                {
                    MessageHash = SuitThermalCriticalHash,
                    ContextHash = ThermalContextHash,
                    SourceId = SourceHash,
                    Frame = frame,
                    Severity = _severity,
                    Flags = (byte)(critical ? 1 : 0)
                };
                SignalBus<HUDNotificationSignal>.TryPushTracked(in warning, ref s_x001HardwareThermalServiceSignalPushDropCount);
            }

            bool transientLowTierOverride = throttling || hapticMute;
            if (!_policyInitialized || transientLowTierOverride != _transientLowTierOverrideApplied)
            {
                GlobalRegistry.SetTransientLowScalabilityOverride(
                    GlobalRegistry.TransientScalabilityThermalPressureMask,
                    transientLowTierOverride);
                _transientLowTierOverrideApplied = transientLowTierOverride;
            }

            _policyInitialized = true;
        }

        private void ReleaseThermalPolicies()
        {
            if (!_policyInitialized)
                return;

            GlobalRegistry.SetSystemKillSwitchBits(GlobalRegistry.SystemKillSwitchLane4VfxMask, false);
            IFoveatedSimulationDirector foveated = _foveatedDirector;
            if (foveated != null)
                foveated.SetThermalFreezeDistanceOverride(false, ThermalFreezeDistanceMeters);

            SystemDispatcher dispatcher = _dispatcher;
            if (dispatcher != null)
                dispatcher.SetThermalCriticalSlowTick(false);

            ToolHapticsRuntime haptics = _haptics;
            if (haptics != null)
                haptics.SetPowerSaveMute(false);

            if (_transientLowTierOverrideApplied)
            {
                GlobalRegistry.SetTransientLowScalabilityOverride(
                    GlobalRegistry.TransientScalabilityThermalPressureMask,
                    false);
            }

            _policyInitialized = false;
            _throttlingPolicyApplied = false;
            _criticalPolicyApplied = false;
            _hapticMuteApplied = false;
            _transientLowTierOverrideApplied = false;
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
            if (!OpenOrAcquireThermalBlackBoxWriteView(out NativeArray<HardwareThermalTelemetryEntry> blackBox))
                return;

            try
            {
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
            finally
            {
                ReleaseThermalBlackBoxWriteView();
            }
        }

        private void DumpBlackBoxCold()
        {
            if (!TryReadThermalBlackBox(out NativeArray<HardwareThermalTelemetryEntry>.ReadOnly blackBox))
                return;

            try
            {
                string projectRoot = Application.dataPath;
                DirectoryInfo parent = Directory.GetParent(projectRoot);
                if (parent != null)
                    projectRoot = parent.FullName;

                string folder = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, DumpFileName);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[16];
                    BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), _sequence);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), _blackBoxCursor);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), BlackBoxFrameCount);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), HardwareThermalTelemetryEntryBytes);
                    stream.Write(header);

                    Span<byte> entryBytes = stackalloc byte[HardwareThermalTelemetryEntryBytes];
                    for (int i = 0; i < BlackBoxFrameCount; i++)
                    {
                        int index = _blackBoxCursor + i;
                        if (index >= BlackBoxFrameCount)
                            index -= BlackBoxFrameCount;

                        HardwareThermalTelemetryEntry entry = blackBox[index];
                        entryBytes.Clear();
                        BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(0, 4), entry.Frame);
                        BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(4, 4), entry.Sequence);
                        BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(8, 4), entry.ActionMask);
                        BinaryPrimitives.WriteInt16LittleEndian(entryBytes.Slice(12, 2), entry.TemperatureTenthsCelsius);
                        entryBytes[14] = entry.Severity;
                        entryBytes[15] = entry.BatteryPercent;
                        entryBytes[16] = entry.BatteryStatus;
                        entryBytes[17] = entry.ThermalStatus;
                        entryBytes[18] = entry.Flags;
                        entryBytes[19] = entry.Reserved0;
                        entryBytes[20] = entry.Reserved1;
                        entryBytes[21] = entry.Reserved2;
                        entryBytes[22] = entry.Reserved3;
                        entryBytes[23] = entry.Reserved4;
                        stream.Write(entryBytes);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void EnsureNativeState()
        {
            if (OpenOrAcquireThermalSeverityWriteView(out _))
                ReleaseThermalSeverityWriteView();

            if (OpenOrAcquireThermalBlackBoxWriteView(out _))
                ReleaseThermalBlackBoxWriteView();
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

        private bool OpenOrAcquireThermalSeverityWriteView(out NativeArray<byte> severity)
        {
            severity = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (_thermalSeverityHandle.BufferID != 0u &&
                vault.TryAcquireWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis, out severity))
            {
                if (severity.IsCreated && severity.Length >= 1)
                    return true;

                vault.ReleaseWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis);
                severity = default;
            }

            if (vault.IsAllocationLocked)
            {
                severity = default;
                return false;
            }

            _thermalSeverityHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.HardwareThermalSeverity,
                1,
                SystemID.HardwareHomeostasis,
                NativeArrayOptions.ClearMemory);

            if (!vault.TryAcquireWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis, out severity))
            {
                severity = default;
                return false;
            }

            if (severity.IsCreated && severity.Length >= 1)
                return true;

            vault.ReleaseWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis);
            severity = default;
            return false;
        }

        private bool ReleaseThermalSeverityWriteView()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   _thermalSeverityHandle.BufferID != 0u &&
                   vault.ReleaseWriteLock(in _thermalSeverityHandle, SystemID.HardwareHomeostasis);
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

        private bool OpenOrAcquireThermalBlackBoxWriteView(out NativeArray<HardwareThermalTelemetryEntry> blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (_blackBoxHandle.BufferID != 0u &&
                vault.TryAcquireWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis, out blackBox))
            {
                if (blackBox.IsCreated && blackBox.Length >= BlackBoxFrameCount)
                    return true;

                vault.ReleaseWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis);
                blackBox = default;
            }

            if (vault.IsAllocationLocked)
            {
                blackBox = default;
                return false;
            }

            _blackBoxHandle = vault.EnsureGenerationHandle<HardwareThermalTelemetryEntry>(
                BufferID.HardwareThermalBlackBox,
                BlackBoxFrameCount,
                SystemID.HardwareHomeostasis,
                NativeArrayOptions.ClearMemory);

            if (!vault.TryAcquireWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis, out blackBox))
            {
                blackBox = default;
                return false;
            }

            if (blackBox.IsCreated && blackBox.Length >= BlackBoxFrameCount)
                return true;

            vault.ReleaseWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis);
            blackBox = default;
            return false;
        }

        private bool ReleaseThermalBlackBoxWriteView()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   _blackBoxHandle.BufferID != 0u &&
                   vault.ReleaseWriteLock(in _blackBoxHandle, SystemID.HardwareHomeostasis);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
                return;

            IHardwareThermalService registered = GlobalRegistry.HardwareThermal;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterHardwareThermalService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.HardwareThermal, this);
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
                _dispatcher = currentService as SystemDispatcher;
                _registeredFrameTick = false;
                _registeredFrostTick = false;
                if (currentService != null && isActiveAndEnabled && _serviceRegistered)
                {
                    TryRegisterFrameTick();
                    TryRegisterFrostTick();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ToolHapticsRuntime)
                _haptics = currentService as ToolHapticsRuntime;
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
            if (_registeredFrostTick)
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
            if (_registeredFrameTick)
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
