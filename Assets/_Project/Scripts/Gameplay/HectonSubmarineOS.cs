using Hecton8.Audio;
using Hecton8.Atmosphere;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Crafting;
using Hecton8.Gameplay.Atlas6Liability;
using Hecton8.Power;
using Hecton8.UI;
using Hecton8.Visor;
using Hecton8.World;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [System.Flags]
    public enum SubsystemStatus : byte
    {
        None = 0,
        Engines = 1 << 0,
        LifeSupport = 1 << 1,
        Lights = 1 << 2,
        Sonar = 1 << 3
    }

    public enum SubmarineEmergencyLevel : byte
    {
        Nominal = 0,
        Caution = 1,
        Danger = 2,
        Evacuate = 3
    }

    [System.Flags]
    public enum SubmarineVwsFlags : ushort
    {
        None = 0,
        PowerLow = 1 << 0,
        OxygenLow = 1 << 1,
        OxygenCritical = 1 << 2,
        HullBreach = 1 << 3,
        PressureHigh = 1 << 4,
        FatalPressure = 1 << 5,
        ThermalStress = 1 << 6,
        MultiSystemFailure = 1 << 7
    }

    public enum HectonSubmarineOsLogCode : byte
    {
        ReactorStable = 0,
        LowPowerModeEngaged = 1,
        LowPowerModeCleared = 2,
        LifeSupportCritical = 3,
        LifeSupportStabilized = 4,
        HullPressureHigh = 5,
        HullPressureStabilized = 6,
        MultiSystemFailure = 7,
        FatalImplosion = 8,
        EmergencyLevelNominal = 9,
        EmergencyLevelCaution = 10,
        EmergencyLevelDanger = 11,
        EmergencyLevelEvacuate = 12,
        StationKeepingArmed = 13,
        StationKeepingReleased = 14,
        HostileDroneDetected = 15,
        EngineTelemetryMasked = 16,
        EngineTelemetryRestored = 17
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public readonly struct HectonSubmarineOsSnapshot
    {
        private const uint LowPowerModeFlag = 1u << 8;
        private const uint LifeSupportCriticalFlag = 1u << 9;
        private const uint StationKeepingFlag = 1u << 10;
        private const uint SubOsPoweredFlag = 1u << 11;

        public HectonSubmarineOsSnapshot(
            SubsystemStatus subsystemStatus,
            SubmarineEmergencyLevel emergencyLevel,
            float powerNormalized,
            float oxygenNormalized,
            float carbonDioxideNormalized,
            float maxPressureKPa,
            float speedKnots,
            float engineHeat01,
            float engineHeatTrue01,
            float engineHeatMaskDelta01,
            uint atlasTelemetryFlags,
            int sonarContactCount,
            int nearestSonarContactMeters,
            SubmarineVwsFlags vocalWarningFlags,
            bool lowPowerModeActive,
            bool lifeSupportCriticalActive,
            bool stationKeepingActive,
            bool subOsPowered)
        {
            this = default;
            PowerNormalized = powerNormalized;
            OxygenNormalized = oxygenNormalized;
            CarbonDioxideNormalized = carbonDioxideNormalized;
            MaxPressureKPa = maxPressureKPa;
            SpeedKnots = speedKnots;
            EngineHeat01 = engineHeat01;
            EngineHeatTrue01 = engineHeatTrue01;
            EngineHeatMaskDelta01 = engineHeatMaskDelta01;
            AtlasTelemetryFlags = atlasTelemetryFlags;
            SonarContactCount = sonarContactCount;
            NearestSonarContactMeters = nearestSonarContactMeters;
            VocalWarningFlags = vocalWarningFlags;
            SubsystemStatus = subsystemStatus;
            EmergencyLevel = emergencyLevel;
            StatusFlags = BuildStatusFlags(subsystemStatus, lowPowerModeActive, lifeSupportCriticalActive, stationKeepingActive, subOsPowered);
        }

        [FieldOffset(0)] public readonly float PowerNormalized;
        [FieldOffset(4)] public readonly float OxygenNormalized;
        [FieldOffset(8)] public readonly float CarbonDioxideNormalized;
        [FieldOffset(12)] public readonly float MaxPressureKPa;
        [FieldOffset(16)] public readonly float SpeedKnots;
        [FieldOffset(20)] public readonly float EngineHeat01;
        [FieldOffset(24)] public readonly int SonarContactCount;
        [FieldOffset(28)] public readonly int NearestSonarContactMeters;
        [FieldOffset(32)] public readonly uint StatusFlags;
        [FieldOffset(36)] public readonly SubmarineVwsFlags VocalWarningFlags;
        [FieldOffset(38)] public readonly SubsystemStatus SubsystemStatus;
        [FieldOffset(39)] public readonly SubmarineEmergencyLevel EmergencyLevel;
        [FieldOffset(40)] public readonly float EngineHeatTrue01;
        [FieldOffset(44)] public readonly float EngineHeatMaskDelta01;
        [FieldOffset(48)] public readonly uint AtlasTelemetryFlags;
        [FieldOffset(52)] private readonly uint _pad0;
        [FieldOffset(56)] private readonly ulong _pad2;

        public readonly bool IsEngineTelemetryMasked =>
            (AtlasTelemetryFlags & Hecton8.Gameplay.Atlas6Liability.ThermalSheerManager.TelemetryFlagMasked) != 0u;

        public static bool HasLowPowerMode(uint statusFlags)
        {
            return (statusFlags & LowPowerModeFlag) != 0u;
        }

        public static bool HasLifeSupportCritical(uint statusFlags)
        {
            return (statusFlags & LifeSupportCriticalFlag) != 0u;
        }

        public static bool HasStationKeeping(uint statusFlags)
        {
            return (statusFlags & StationKeepingFlag) != 0u;
        }

        public static bool HasSubOsPowered(uint statusFlags)
        {
            return (statusFlags & SubOsPoweredFlag) != 0u;
        }

        private static uint BuildStatusFlags(
            SubsystemStatus subsystemStatus,
            bool lowPowerModeActive,
            bool lifeSupportCriticalActive,
            bool stationKeepingActive,
            bool subOsPowered)
        {
            uint flags = (uint)subsystemStatus;
            if (lowPowerModeActive)
                flags |= LowPowerModeFlag;
            if (lifeSupportCriticalActive)
                flags |= LifeSupportCriticalFlag;
            if (stationKeepingActive)
                flags |= StationKeepingFlag;
            if (subOsPowered)
                flags |= SubOsPoweredFlag;

            return flags;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct HectonSubmarineOsLogRequest
    {
        public HectonSubmarineOsLogRequest(HectonSubmarineOsLogCode code, byte priority)
        {
            this = default;
            Code = code;
            Priority = priority;
        }

        [FieldOffset(0)] public readonly HectonSubmarineOsLogCode Code;
        [FieldOffset(1)] public readonly byte Priority;
        [FieldOffset(2)] private readonly ushort _pad0;
        [FieldOffset(4)] private readonly uint _pad1;
        [FieldOffset(8)] private readonly ulong _pad2;
    }

    /// <summary>
    /// Event discriminator for <see cref="SubmarineOsEventPayload"/>.
    /// </summary>
    public enum SubmarineOsEventType : byte
    {
        SnapshotUpdated = 0,
        LogRequested = 1
    }

    /// <summary>
    /// Unmanaged submarine OS event payload drained by <see cref="SystemDispatcher"/> in LateUpdate.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarineOsEventPayload
    {
        [FieldOffset(0)] public float PowerNormalized;
        [FieldOffset(4)] public float OxygenNormalized;
        [FieldOffset(8)] public float CarbonDioxideNormalized;
        [FieldOffset(12)] public float MaxPressureKPa;
        [FieldOffset(16)] public float SpeedKnots;
        [FieldOffset(20)] public float EngineHeat01;
        [FieldOffset(24)] public int SonarContactCount;
        [FieldOffset(28)] public int NearestSonarContactMeters;
        [FieldOffset(32)] public uint ModuleId;
        [FieldOffset(36)] public uint StatusBits;
        [FieldOffset(40)] public ushort EmergencyLevel;
        [FieldOffset(42)] public ushort EventType;
        [FieldOffset(44)] public ushort LogCode;
        [FieldOffset(46)] public ushort Priority;
        [FieldOffset(48)] public ushort VocalWarningFlags;
        [FieldOffset(50)] private ushort _pad0;
        [FieldOffset(52)] public float EngineHeatTrue01;
        [FieldOffset(56)] public float EngineHeatMaskDelta01;
        [FieldOffset(60)] public uint AtlasTelemetryFlags;
    }

    /// <summary>
    /// Listener contract for deferred submarine OS events.
    /// </summary>
    public interface ISubmarineOsEventListener
    {
        void OnSubmarineOsEvent(in SubmarineOsEventPayload payload);
    }

    /// <summary>
    /// NativeQueue-backed submarine OS telemetry and log request bus.
    /// </summary>
    public static class HectonSubmarineOsEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 16;
        private const uint GlobalSubmarineOsModuleId = 0x48534F53u; // "HSOS"
        private const uint LowPowerModeStatusBit = 1u << 8;
        private const uint LifeSupportCriticalStatusBit = 1u << 9;
        private const uint StationKeepingStatusBit = 1u << 10;
        private const uint SubOsPoweredStatusBit = 1u << 11;
        private const float MaximumDecodedPressureKPa = 999999f;
        private const float MaximumDecodedSpeedKnots = 9999.9f;
        private const uint KnownSubsystemStatusBits = (uint)(SubsystemStatus.Engines | SubsystemStatus.LifeSupport | SubsystemStatus.Lights | SubsystemStatus.Sonar);
        private const uint KnownAtlasTelemetryFlags =
            ThermalSheerManager.TelemetryFlagMasked |
            ThermalSheerManager.TelemetryFlagCriticalDowngraded;
        private const ushort KnownVocalWarningFlags = (ushort)(
            SubmarineVwsFlags.PowerLow |
            SubmarineVwsFlags.OxygenLow |
            SubmarineVwsFlags.OxygenCritical |
            SubmarineVwsFlags.HullBreach |
            SubmarineVwsFlags.PressureHigh |
            SubmarineVwsFlags.FatalPressure |
            SubmarineVwsFlags.ThermalStress |
            SubmarineVwsFlags.MultiSystemFailure);
        private const uint SubOsDuplicateListenerWarningHash = 0x48445344u; // HDSD
        private const uint SubOsListenerRejectedWarningHash = 0x4853524Au; // HSRJ
        private const uint SubOsListenerExceptionWarningHash = 0x48534558u; // HSEX
        private const uint SubOsListenerContextHash = 0x48534C53u; // HSLS
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public ISubmarineOsEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct SubmarineOsListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public SubmarineOsListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ISubmarineOsEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ISubmarineOsEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(ISubmarineOsEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return true;
                }

                return false;
            }

            public ISubmarineOsEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[16] - submarine OS deferred listeners - owner: HectonSubmarineOsEvents
        private static SubmarineOsListenerRegistry _listeners = new SubmarineOsListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[16] - listener additions deferred while Sub OS events are dispatching - owner: HectonSubmarineOsEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[16] - listener removals deferred while Sub OS events are dispatching - owner: HectonSubmarineOsEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<SubmarineOsEventPayload> _pendingEvents;
        private static NativeQueue<SubmarineOsEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedSnapshotEventCount;
        private static int _droppedLogEventCount;
        private static int _duplicateListenerRegistrationCount;
        private static int _listenerRejectCount;
        private static int _listenerExceptionCount;
        private static int _lastDuplicateListenerTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DroppedSnapshotEventCount => _droppedSnapshotEventCount;
        public static int DroppedLogEventCount => _droppedLogEventCount;
        public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;
        public static int ListenerRejectCount => _listenerRejectCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;
        public static uint ModuleId => GlobalSubmarineOsModuleId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _listeners.Clear();
            System.Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            System.Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedSnapshotEventCount = 0;
            _droppedLogEventCount = 0;
            _duplicateListenerRegistrationCount = 0;
            _listenerRejectCount = 0;
            _listenerExceptionCount = 0;
            _lastDuplicateListenerTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a deferred submarine OS event listener.
        /// </summary>
        public static void Register(ISubmarineOsEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a deferred submarine OS event listener.
        /// </summary>
        public static void Unregister(ISubmarineOsEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            _listeners.TryUnregister(listener);
        }

        private static void RegisterImmediate(ISubmarineOsEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                ReportDuplicateListenerRegistration();
                return;
            }

            if (!_listeners.TryRegister(listener))
                ReportListenerRejected();
        }

        /// <summary>
        /// Flushes queued submarine OS events to listeners. Called by <see cref="SystemDispatcher"/>.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out SubmarineOsEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                DispatchRegisteredListeners(in payload);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static bool TryRaiseSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            if (!IsKnownEmergencyLevel((ushort)snapshot.EmergencyLevel))
                return false;

            uint statusBits = (uint)snapshot.SubsystemStatus & KnownSubsystemStatusBits;
            if (HectonSubmarineOsSnapshot.HasLowPowerMode(snapshot.StatusFlags))
                statusBits |= LowPowerModeStatusBit;
            if (HectonSubmarineOsSnapshot.HasLifeSupportCritical(snapshot.StatusFlags))
                statusBits |= LifeSupportCriticalStatusBit;
            if (HectonSubmarineOsSnapshot.HasStationKeeping(snapshot.StatusFlags))
                statusBits |= StationKeepingStatusBit;
            if (HectonSubmarineOsSnapshot.HasSubOsPowered(snapshot.StatusFlags))
                statusBits |= SubOsPoweredStatusBit;

            return Enqueue(new SubmarineOsEventPayload
            {
                PowerNormalized = SanitizeNormalized(snapshot.PowerNormalized),
                OxygenNormalized = SanitizeNormalized(snapshot.OxygenNormalized),
                CarbonDioxideNormalized = SanitizeNormalized(snapshot.CarbonDioxideNormalized),
                MaxPressureKPa = SanitizeNonNegativeFinite(snapshot.MaxPressureKPa, MaximumDecodedPressureKPa),
                SpeedKnots = SanitizeNonNegativeFinite(snapshot.SpeedKnots, MaximumDecodedSpeedKnots),
                EngineHeat01 = SanitizeNormalized(snapshot.EngineHeat01),
                EngineHeatTrue01 = SanitizeNormalized(snapshot.EngineHeatTrue01),
                EngineHeatMaskDelta01 = SanitizeNormalized(snapshot.EngineHeatMaskDelta01),
                AtlasTelemetryFlags = snapshot.AtlasTelemetryFlags & KnownAtlasTelemetryFlags,
                SonarContactCount = math.max(0, snapshot.SonarContactCount),
                NearestSonarContactMeters = math.max(0, snapshot.NearestSonarContactMeters),
                ModuleId = GlobalSubmarineOsModuleId,
                StatusBits = statusBits,
                EmergencyLevel = (ushort)snapshot.EmergencyLevel,
                EventType = (ushort)SubmarineOsEventType.SnapshotUpdated,
                LogCode = 0,
                Priority = 0,
                VocalWarningFlags = (ushort)((ushort)snapshot.VocalWarningFlags & KnownVocalWarningFlags)
            });
        }

        [System.Obsolete("Use TryRaiseSnapshotUpdated so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot) => TryRaiseSnapshotUpdated(in snapshot);

        public static bool TryRaiseLogRequested(in HectonSubmarineOsLogRequest request)
        {
            if (!IsKnownLogCode((ushort)request.Code) || request.Priority == 0)
                return false;

            return Enqueue(new SubmarineOsEventPayload
            {
                PowerNormalized = 0f,
                OxygenNormalized = 0f,
                CarbonDioxideNormalized = 0f,
                MaxPressureKPa = 0f,
                SpeedKnots = 0f,
                EngineHeat01 = 0f,
                SonarContactCount = 0,
                NearestSonarContactMeters = 0,
                ModuleId = GlobalSubmarineOsModuleId,
                StatusBits = 0u,
                EmergencyLevel = 0,
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = (ushort)request.Code,
                Priority = request.Priority,
                VocalWarningFlags = 0
            });
        }

        [System.Obsolete("Use TryRaiseLogRequested so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseLogRequested(in HectonSubmarineOsLogRequest request) => TryRaiseLogRequested(in request);

        public static bool TryBuildSnapshot(in SubmarineOsEventPayload payload, out HectonSubmarineOsSnapshot snapshot)
        {
            snapshot = default;
            if (payload.ModuleId != GlobalSubmarineOsModuleId)
                return false;

            if ((SubmarineOsEventType)payload.EventType != SubmarineOsEventType.SnapshotUpdated)
                return false;

            if (!IsKnownEmergencyLevel(payload.EmergencyLevel))
                return false;

            snapshot = new HectonSubmarineOsSnapshot(
                (SubsystemStatus)(payload.StatusBits & KnownSubsystemStatusBits),
                (SubmarineEmergencyLevel)payload.EmergencyLevel,
                SanitizeNormalized(payload.PowerNormalized),
                SanitizeNormalized(payload.OxygenNormalized),
                SanitizeNormalized(payload.CarbonDioxideNormalized),
                SanitizeNonNegativeFinite(payload.MaxPressureKPa, MaximumDecodedPressureKPa),
                SanitizeNonNegativeFinite(payload.SpeedKnots, MaximumDecodedSpeedKnots),
                SanitizeNormalized(payload.EngineHeat01),
                SanitizeNormalized(payload.EngineHeatTrue01),
                SanitizeNormalized(payload.EngineHeatMaskDelta01),
                payload.AtlasTelemetryFlags & KnownAtlasTelemetryFlags,
                math.max(0, payload.SonarContactCount),
                math.max(0, payload.NearestSonarContactMeters),
                (SubmarineVwsFlags)(payload.VocalWarningFlags & KnownVocalWarningFlags),
                (payload.StatusBits & LowPowerModeStatusBit) != 0u,
                (payload.StatusBits & LifeSupportCriticalStatusBit) != 0u,
                (payload.StatusBits & StationKeepingStatusBit) != 0u,
                (payload.StatusBits & SubOsPoweredStatusBit) != 0u);
            return true;
        }

        public static bool TryBuildLogRequest(in SubmarineOsEventPayload payload, out HectonSubmarineOsLogRequest request)
        {
            request = default;
            if (payload.ModuleId != GlobalSubmarineOsModuleId)
                return false;

            if ((SubmarineOsEventType)payload.EventType != SubmarineOsEventType.LogRequested)
                return false;

            if (!IsKnownLogCode(payload.LogCode) || payload.Priority == 0 || payload.Priority > byte.MaxValue)
                return false;

            request = new HectonSubmarineOsLogRequest(
                (HectonSubmarineOsLogCode)payload.LogCode,
                (byte)payload.Priority);
            return true;
        }

        private static bool IsKnownLogCode(ushort logCode)
        {
            switch ((HectonSubmarineOsLogCode)logCode)
            {
                case HectonSubmarineOsLogCode.ReactorStable:
                case HectonSubmarineOsLogCode.LowPowerModeEngaged:
                case HectonSubmarineOsLogCode.LowPowerModeCleared:
                case HectonSubmarineOsLogCode.LifeSupportCritical:
                case HectonSubmarineOsLogCode.LifeSupportStabilized:
                case HectonSubmarineOsLogCode.HullPressureHigh:
                case HectonSubmarineOsLogCode.HullPressureStabilized:
                case HectonSubmarineOsLogCode.MultiSystemFailure:
                case HectonSubmarineOsLogCode.FatalImplosion:
                case HectonSubmarineOsLogCode.EmergencyLevelNominal:
                case HectonSubmarineOsLogCode.EmergencyLevelCaution:
                case HectonSubmarineOsLogCode.EmergencyLevelDanger:
                case HectonSubmarineOsLogCode.EmergencyLevelEvacuate:
                case HectonSubmarineOsLogCode.StationKeepingArmed:
                case HectonSubmarineOsLogCode.StationKeepingReleased:
                case HectonSubmarineOsLogCode.HostileDroneDetected:
                case HectonSubmarineOsLogCode.EngineTelemetryMasked:
                case HectonSubmarineOsLogCode.EngineTelemetryRestored:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsKnownEmergencyLevel(ushort emergencyLevel)
        {
            switch ((SubmarineEmergencyLevel)emergencyLevel)
            {
                case SubmarineEmergencyLevel.Nominal:
                case SubmarineEmergencyLevel.Caution:
                case SubmarineEmergencyLevel.Danger:
                case SubmarineEmergencyLevel.Evacuate:
                    return true;
                default:
                    return false;
            }
        }

        private static float SanitizeNormalized(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeNonNegativeFinite(float value, float maxValue)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, math.max(0f, maxValue)) : 0f;
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<SubmarineOsEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SubmarineOsEventPayload>[16] - deferred submarine OS event lane - owner: HectonSubmarineOsEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<SubmarineOsEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SubmarineOsEventPayload>[16] - next-frame submarine OS event lane prevents same-frame reentrant dispatch - owner: HectonSubmarineOsEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(HectonSubmarineOsEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static bool Enqueue(in SubmarineOsEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                RecordDroppedEvent(payload.EventType);
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static void RecordDroppedEvent(ushort eventType)
        {
            if (_droppedEventCount < int.MaxValue)
                _droppedEventCount++;

            if (eventType == (ushort)SubmarineOsEventType.SnapshotUpdated)
            {
                if (_droppedSnapshotEventCount < int.MaxValue)
                    _droppedSnapshotEventCount++;
                return;
            }

            if (eventType == (ushort)SubmarineOsEventType.LogRequested &&
                _droppedLogEventCount < int.MaxValue)
            {
                _droppedLogEventCount++;
            }
        }

        private static void ReportDuplicateListenerRegistration()
        {
            _duplicateListenerRegistrationCount = SaturatingIncrement(_duplicateListenerRegistrationCount);
            PublishListenerWarning(
                SubOsDuplicateListenerWarningHash,
                _duplicateListenerRegistrationCount,
                ref _lastDuplicateListenerTelemetryFrame);
        }

        private static void ReportListenerRejected()
        {
            _listenerRejectCount = SaturatingIncrement(_listenerRejectCount);
            PublishListenerWarning(
                SubOsListenerRejectedWarningHash,
                _listenerRejectCount,
                ref _lastListenerRejectedTelemetryFrame);
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount = SaturatingIncrement(_listenerExceptionCount);
            PublishListenerWarning(
                SubOsListenerExceptionWarningHash,
                _listenerExceptionCount,
                ref _lastListenerExceptionTelemetryFrame);
        }

        private static void PublishListenerWarning(uint warningHash, int count, ref int lastTelemetryFrame)
        {
            if (!TryReserveTelemetryWarningFrame(ref lastTelemetryFrame, 1))
                return;

            PublishPerformanceWarningBestEffort(
                warningHash,
                SubOsListenerContextHash,
                count);
        }

        private static bool TryReserveTelemetryWarningFrame(ref int lastTelemetryFrame, int cooldownFrames)
        {
            int frame = ResolveCurrentFrameIndexSafe();
            if (frame < 0)
            {
                if (lastTelemetryFrame == int.MinValue)
                    return false;

                lastTelemetryFrame = int.MinValue;
                return true;
            }

            if (lastTelemetryFrame >= 0 && frame - lastTelemetryFrame < cooldownFrames)
                return false;

            lastTelemetryFrame = frame;
            return true;
        }

        private static int ResolveCurrentFrameIndexSafe()
        {
            try
            {
                return SystemDispatcher.CurrentFrameIndex;
            }
            catch
            {
                return -1;
            }
        }

        private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (System.Exception telemetryException)
            {
                LogTelemetryWarningException(telemetryException);
            }
        }

        private static int SaturatingIncrement(int value)
        {
            return value < int.MaxValue ? value + 1 : int.MaxValue;
        }

        private static void DispatchRegisteredListeners(in SubmarineOsEventPayload payload)
        {
            int count = _listeners.Count;
            if (count <= 0)
                return;

            _isDispatching = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    ISubmarineOsEventListener listener = _listeners.GetAt(i);
                    if (listener != null)
                        DispatchToListener(listener, in payload);
                }
            }
            finally
            {
                _isDispatching = false;
                ApplyDeferredListenerMutations();
            }
        }

        private static void DispatchToListener(ISubmarineOsEventListener listener, in SubmarineOsEventPayload payload)
        {
            try
            {
                listener.OnSubmarineOsEvent(in payload);
            }
            catch (System.Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                H8Debug.LogException(exception);
            }
            catch
            {
            }
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTelemetryWarningException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                H8Debug.LogException(exception);
            }
            catch
            {
            }
#endif
        }

        private static void QueueDeferredRegister(ISubmarineOsEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                if (!CancelDeferredUnregister(listener))
                    ReportDuplicateListenerRegistration();
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(ISubmarineOsEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(ISubmarineOsEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static bool CancelDeferredUnregister(ISubmarineOsEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return true;
            }

            return false;
        }

        private static bool IsDeferredRegisterPending(ISubmarineOsEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(ISubmarineOsEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                ISubmarineOsEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                ISubmarineOsEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<SubmarineOsEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                {
                    pendingCount = 0;
                    break;
                }

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<SubmarineOsEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Central submarine diagnostic owner that monitors power, atmosphere, and emergency state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineCoreDirector))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Hecton Submarine OS")]
    public sealed class HectonSubmarineOS : MonoBehaviour, IUpdatable, ISlowTickable, IRenderable, IPowerGridTelemetryListener, IHighPressureEventListener, IFatalPressureImplosionEventListener, IDroneFleetSnapshotEventListener, ISonarPingEventListener, ISonarSnapshotEventListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HectonSubmarineOSSignalPushDropCount;
        private const float DefaultReferencePressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float LowPowerThreshold01 = 0.20f;
        private const float LowPowerReleaseThreshold01 = 0.24f;
        private const float CascadingBrownoutThreshold01 = 0.40f;
        private const float DangerPowerThreshold01 = 0.10f;
        private const float VitalWarningHealthThreshold01 = 0.20f;
        private const float VitalWarningHealthReleaseThreshold01 = 0.28f;
        private const float LifeSupportCriticalThreshold01 = 0.10f;
        private const float LifeSupportReleaseThreshold01 = 0.12f;
        private const float EvacuateOxygenThreshold01 = 0.05f;
        private const float PressureHighThresholdKPa = 150f;
        private const float PressureDangerThresholdKPa = 220f;
        private const float PressureReleaseThresholdKPa = 140f;
        private const float OxygenLowVwsThreshold01 = 0.20f;
        private const float ThermalStressVwsThreshold01 = 0.65f;
        private const float HullBreachAreaThresholdSquareMeters = 0.0001f;
        private const float SubOsUnpoweredThreshold01 = 0.001f;
        private const float SurvivalSonarRefreshIntervalSeconds = 0.1f;
        private const float StandardSonarRefreshIntervalSeconds = 0.06666667f;
        private const float VisualOverkillSonarRefreshIntervalSeconds = 0.03333334f;
        private const float DiagnosticsRefreshIntervalSeconds = 0.5f;
        private const float SonarMonitorRadiusMeters = 200f;
        private const float KnotsPerMeterPerSecond = 1.94384449f;
        private const float EngineHeatSpeedReferenceInv = 0.071428571f;
        private const float EngineHeatAccelerationReferenceInv = 0.25f;
        private const float EngineHeatCruiseLoadScale = 0.71875f;
        private const float EngineHeatQuantizeScale = 31f;
        private const float EngineHeatQuantizeInv = 0.0322580645f;
        private const float SonarSweepDecayPerSecond = 1.75f;
        private const float VwsRepeatCooldownSeconds = 8f;
        private const float VwsCaptionDurationSeconds = 2.5f;
        private const float BrownoutBlinkFrequency = 8f;
        private const int SubOsEventDropTelemetryCooldownFrames = 120;
        private const uint SubOsSnapshotDropWarningHash = 0x534E4452u; // SNDR
        private const uint SubOsLogDropWarningHash = 0x4C4F4452u; // LODR
        private const uint SubOsEventDropContextHash = 0x48534F53u; // HSOS
        private const byte LogPriorityNormal = 1;
        private const byte LogPriorityWarning = 2;
        private const byte LogPriorityCritical = 3;
        private static readonly uint LowPowerCaptionHash = AudioCaptionEvents.LowPowerCaptionHash;
        private static readonly uint MultiFailureCaptionHash = AudioCaptionEvents.MultiFailureCaptionHash;
        private static readonly uint EmergencyDangerCaptionHash = AudioCaptionEvents.EmergencyDangerCaptionHash;
        private static readonly uint AbandonShipCaptionHash = AudioCaptionEvents.AbandonShipCaptionHash;
        private static readonly uint HostileDroneCaptionHash = AudioCaptionEvents.HostileDroneCaptionHash;
        private static readonly uint OxygenLowCaptionHash = AudioCaptionEvents.OxygenLowCaptionHash;
        private static readonly uint OxygenCriticalCaptionHash = AudioCaptionEvents.OxygenCriticalCaptionHash;
        private static readonly uint HullBreachCaptionHash = AudioCaptionEvents.HullBreachCaptionHash;
        private static readonly uint PressureHighCaptionHash = AudioCaptionEvents.PressureHighCaptionHash;
        private static readonly uint ThermalStressCaptionHash = AudioCaptionEvents.ThermalStressCaptionHash;
        private static readonly int _HectonBrownoutPulseId = Shader.PropertyToID("_HectonBrownoutPulse");
        private static readonly int _HectonSubOsLightingStateId = Shader.PropertyToID("_HectonSubOsLightingState");
        private static readonly int _SubInteriorLightingStateId = Shader.PropertyToID("_SubInteriorLightingState");
        private static readonly int _HectonSubOsSonarSweepId = Shader.PropertyToID("_HectonSubOsSonarSweep");
        private static readonly int _HectonSubOsSonarLodId = Shader.PropertyToID("_HectonSubOsSonarLod");
        private static readonly int _HectonSubOsNavigationId = Shader.PropertyToID("_HectonSubOsNavigation");
        private static readonly int _HectonSubOsEngineDiagnosticsId = Shader.PropertyToID("_HectonSubOsEngineDiagnostics");
        [Header("Audio")]
        [Tooltip("Optional helmet warning for low-power transition events.")]
        [SerializeField] private AudioClip lowPowerWarningClip;

        [Tooltip("Optional helmet warning loop/one-shot for life-support critical state.")]
        [SerializeField] private AudioClip lifeSupportCriticalClip;

        [Tooltip("Optional helmet warning for simultaneous multi-system failures.")]
        [SerializeField] private AudioClip multiSystemFailureClip;

        [Tooltip("Optional abandon-ship alarm routed directly through GlobalRegistry.Audio.")]
        [SerializeField] private AudioClip abandonShipAlarmClip;

        [Tooltip("Optional VWS clip for oxygen low. Falls back to life-support warning when unset.")]
        [SerializeField] private AudioClip oxygenLowWarningClip;

        [Header("Queued Audio Event IDs")]
        [Tooltip("One-based SpatialAudioManager event table ID for low-power VWS. Zero disables queued audio.")]
        [SerializeField] private uint lowPowerWarningEventId;

        [Tooltip("One-based SpatialAudioManager event table ID for life-support critical VWS. Zero disables queued audio.")]
        [SerializeField] private uint lifeSupportCriticalEventId;

        [Tooltip("One-based SpatialAudioManager event table ID for multi-system failure VWS. Zero disables queued audio.")]
        [SerializeField] private uint multiSystemFailureEventId;

        [Tooltip("One-based SpatialAudioManager event table ID for abandon-ship VWS. Zero disables queued audio.")]
        [SerializeField] private uint abandonShipAlarmEventId;

        [Tooltip("One-based SpatialAudioManager event table ID for oxygen-low VWS. Zero falls back to life-support ID.")]
        [SerializeField] private uint oxygenLowWarningEventId;

        [Tooltip("One-based SpatialAudioManager event table ID for hull-breach VWS. Zero falls back to multi-system ID.")]
        [SerializeField] private uint hullBreachWarningEventId;

        [Tooltip("One-based SpatialAudioManager event table ID for hull pressure or thermal-stress VWS. Zero falls back to multi-system ID.")]
        [SerializeField] private uint hullStressWarningEventId;

        [Tooltip("UI mixer volume for diegetic submarine OS warnings.")]
        [SerializeField, Range(0f, 1f)] private float warningVolume = 0.55f;

        private SubmarineCoreDirector _submarineCore;
        private ISubmarineAtmosphereRoomReadModel _atmosphereSystem;
        private SubmarineStationKeepingController _stationKeepingController;
        private Hecton8.Gameplay.Atlas6Liability.Atlas6CorporateLiabilityManager _atlas6Manager;
        private IPowerGridService _powerGridService;
        private SpectrumSystem _spectrumRuntime;
        private IPlayerRuntimeContext _playerRuntime;
        private HectonSubmarineOsSnapshot _lastPublishedSnapshot;
        private SubsystemStatus _subsystemStatus;
        private SubmarineEmergencyLevel _emergencyLevel;
        private float _powerNormalized = 1f;
        private float _powerSupplyRatio = 1f;
        private float _oxygenNormalized = 1f;
        private float _carbonDioxideNormalized;
        private float _maxPressureKPa = DefaultReferencePressureKPa;
        private float _speedKnots;
        private float _engineHeat01;
        private float _engineHeatTrue01;
        private float _engineHeatMaskDelta01;
        private uint _atlasTelemetryFlags;
        private float _lastHullSpeedMetersPerSecond;
        private float _navigationRefreshAccumulator;
        private float _diagnosticsRefreshAccumulator;
        private float _sonarSweepPhase;
        private float _sonarPingIntensity;
        private float _lightingPulsePhase;
        private int _sonarContactCount;
        private int _nearestSonarContactMeters;
        private SpatialSonarSnapshot _lastSonarSnapshot;
        private LogisticsBrownoutTier _highestBrownoutTier;
        private bool _lowPowerModeActive;
        private bool _cascadingBrownoutActive;
        private bool _lifeSupportCriticalActive;
        private bool _pressureHighActive;
        private bool _vitalWarningActive;
        private bool _fatalImplosionLatched;
        private bool _multiSystemFailureLatched;
        private bool _engineTelemetryMaskActive;
        private bool _subOsPowered = true;
        private bool _registeredUpdatable;
        private bool _registeredRenderable;
        private bool _registeredSlowTick;
        private bool _registeredHotSwapListener;
        private bool _registeredAtlas6ActiveRuntimeListener;
        private bool _runtimeDispatcherReady;
        private bool _runtimeLifecycleStarted;
        private bool _stationKeepingStateCached;
        private float _cachedSonarQualityWeight01 = 1f;
        private float _lastAppliedSonarQualityWeight01 = -1f;
        private float _brownoutPulsePhase;
        private int _hostileDroneAlarmCount;
        private SubmarineVwsFlags _vwsActiveFlags;
        private double _nextPowerLowVwsTime;
        private double _nextOxygenLowVwsTime;
        private double _nextOxygenCriticalVwsTime;
        private double _nextHullBreachVwsTime;
        private double _nextPressureHighVwsTime;
        private double _nextFatalPressureVwsTime;
        private double _nextThermalStressVwsTime;
        private double _nextMultiFailureVwsTime;
        private HectonDroneFleetSnapshot _fleetSnapshot;
        private bool _subOsShaderResetDirty;
        private bool _navigationShaderGlobalDirty;
        private bool _engineDiagnosticsShaderGlobalDirty;
        private bool _brownoutPulseShaderGlobalDirty;
        private Vector4 _pendingNavigationShaderGlobal;
        private Vector4 _pendingEngineDiagnosticsShaderGlobal;
        private float _pendingBrownoutPulseShaderGlobal;
        private int _droppedSubOsSnapshotPublishCount;
        private int _droppedSubOsLogPublishCount;
        private int _lastSubOsEventDropTelemetryFrame = -SubOsEventDropTelemetryCooldownFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstalled()
        {
            int submarineRootCount = SubmarineCoreDirector.RegisteredRootCount;
            for (int i = 0; i < submarineRootCount; i++)
            {
                SubmarineCoreDirector submarineRoot = SubmarineCoreDirector.GetRegisteredRootAt(i);
                if (submarineRoot == null)
                    continue;

                if (!submarineRoot.TryGetComponent(out HectonSubmarineOS _))
                {
                    // Player-build construction path: no authored/bootstrap instance reachable.
                    // Must construct in player builds when bootstrap reorders or skips registration.
                    submarineRoot.gameObject.AddComponent<HectonSubmarineOS>(); // COLD ALLOC: HectonSubmarineOS[1] - submarine-wide diagnostic owner - owner: HectonSubmarineOS
                }

                if (!submarineRoot.TryGetComponent(out SubmarineStationKeepingController _))
                {
                    // Player-build construction path: no authored/bootstrap instance reachable.
                    // Must construct in player builds when bootstrap reorders or skips registration.
                    submarineRoot.gameObject.AddComponent<SubmarineStationKeepingController>(); // COLD ALLOC: SubmarineStationKeepingController[1] - cinematic station-keeping owner - owner: HectonSubmarineOS
                }
            }
        }

        /// <summary>Current authored emergency level resolved by the submarine OS.</summary>
        public SubmarineEmergencyLevel EmergencyLevel => _emergencyLevel;

        /// <summary>Current subsystem status bitmask.</summary>
        public SubsystemStatus CurrentSubsystemStatus => _subsystemStatus;

        /// <summary>Current normalized power health used for low-power decisions.</summary>
        public float PowerNormalized => _powerNormalized;

        /// <summary>Current minimum normalized oxygen fraction across all rooms.</summary>
        public float OxygenNormalized => _oxygenNormalized;

        /// <summary>Current maximum room pressure in kilopascals.</summary>
        public float MaxPressureKPa => _maxPressureKPa;

        /// <summary>Latest fleet telemetry published by the repair-drone dispatcher.</summary>
        public HectonDroneFleetSnapshot FleetSnapshot => _fleetSnapshot;

        /// <summary>Arms the fleet-wide last-resort sacrifice weld command.</summary>
        public void RequestFleetSacrifice()
        {
            DroneFleetManager.RequestFleetSacrifice();
        }

        private void Awake()
        {
            CacheReferencesCold();
            RefreshColdRegistryReferences();
        }

        private void OnEnable()
        {
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            TryRegisterAtlas6ActiveRuntimeListener();
            RefreshCachedSonarQualityWeight();
            ApplySonarLodShaderGlobal(true);
            TryStartRuntimeLifecycle();
        }

        private void Start()
        {
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            TryRegisterAtlas6ActiveRuntimeListener();
            TryStartRuntimeLifecycle();
        }

        private void OnDisable()
        {
            TryUnregisterAtlas6ActiveRuntimeListener();
            TryUnregisterHotSwapListener();

            if (!_runtimeLifecycleStarted && !_registeredUpdatable && !_registeredSlowTick && !_registeredRenderable)
                return;

            _runtimeLifecycleStarted = false;
            PublishShutdownSnapshot();
            Unsubscribe();
            TryUnregister();
            SetLowPowerMode(false);
            SetCascadingBrownout(false);
            RestoreBrownoutVisualsImmediate();
        }

        private void OnDestroy()
        {
            _runtimeLifecycleStarted = false;
            Unsubscribe();
            TryUnregisterAtlas6ActiveRuntimeListener();
            TryUnregisterHotSwapListener();
            TryUnregister();
            RestoreBrownoutVisualsImmediate();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!CanUseRuntimeDispatcher() || !_subOsPowered)
                return;

            RefreshCachedComponentReferencesHot();
            float safeDeltaTime = math.max(0f, deltaTime);
            _navigationRefreshAccumulator += safeDeltaTime;
            _diagnosticsRefreshAccumulator += safeDeltaTime;

            bool publishSnapshot = false;
            float sonarQualityWeight01 = RefreshCachedSonarQualityWeight();
            if (_navigationRefreshAccumulator >= ResolveSonarRefreshIntervalSeconds(sonarQualityWeight01))
            {
                _navigationRefreshAccumulator = 0f;
                RefreshNavigationTelemetry();
                publishSnapshot = true;
            }

            if (_diagnosticsRefreshAccumulator >= DiagnosticsRefreshIntervalSeconds)
            {
                float elapsed = _diagnosticsRefreshAccumulator;
                _diagnosticsRefreshAccumulator = 0f;
                RefreshEngineDiagnosticsTelemetry(elapsed);
                publishSnapshot = true;
            }

            if (!publishSnapshot)
                return;

            PublishCurrentSnapshotIfChanged();
            HectonSubmarineOsDisplay.EnsureRuntimeInstance();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!CanUseRuntimeDispatcher())
                return;

            RefreshCachedComponentReferencesHot();
            RefreshTelemetryFromServices();
            bool wasPowered = _subOsPowered;
            SetSubOsPowered(ResolveSubOsPowered());
            if (!_subOsPowered)
                return;

            if (wasPowered)
                EvaluateStateMachine(false);
        }

        /// <inheritdoc />
        public void Render(float deltaTime)
        {
            if (!CanUseRuntimeDispatcher())
                return;

            FlushQueuedSubOsShaderGlobals();

            if (!_subOsPowered)
                return;

            float safeDeltaTime = math.max(0f, deltaTime);
            RefreshSonarSweepGlobal(safeDeltaTime);
            ApplySonarLodShaderGlobal();
            ApplyLightingStateGlobal(safeDeltaTime);

            if (!_cascadingBrownoutActive || _lowPowerModeActive)
            {
                Shader.SetGlobalFloat(_HectonBrownoutPulseId, 0f);
                return;
            }

            _brownoutPulsePhase = math.frac(_brownoutPulsePhase + math.max(0f, deltaTime) * BrownoutBlinkFrequency);
            float pulse = 1f - math.abs((_brownoutPulsePhase * 2f) - 1f);
            Shader.SetGlobalFloat(_HectonBrownoutPulseId, pulse);
        }

        private void TryStartRuntimeLifecycle()
        {
            if (_runtimeLifecycleStarted || !CanUseRuntimeDispatcher())
                return;

            CacheReferencesCold();
            Subscribe();
            _fleetSnapshot = DroneFleetManager.CurrentSnapshot;
            TryRegister();
            PublishLog(HectonSubmarineOsLogCode.ReactorStable, LogPriorityNormal);
            RefreshTelemetryFromServices();
            SetSubOsPowered(ResolveSubOsPowered());
            if (!_subOsPowered)
            {
                _runtimeLifecycleStarted = true;
                return;
            }

            RefreshNavigationTelemetry();
            RefreshEngineDiagnosticsTelemetry(DiagnosticsRefreshIntervalSeconds);
            EvaluateStateMachine(true);
            if (_subOsPowered)
                HectonSubmarineOsDisplay.EnsureRuntimeInstance();
            _runtimeLifecycleStarted = true;
        }

        private void CacheReferencesCold()
        {
            if (_submarineCore == null)
                TryGetComponent(out _submarineCore);

            if (_submarineCore != null)
            {
                if (_atmosphereSystem == null)
                    _atmosphereSystem = _submarineCore.AtmosphereSystem;

                if (_stationKeepingController == null)
                    _submarineCore.TryGetComponent(out _stationKeepingController);
            }

            RefreshAtlas6ManagerReference(publishIfChanged: false);
        }

        private void RefreshCachedComponentReferencesHot()
        {
            SubmarineCoreDirector submarineCore = _submarineCore;
            if (submarineCore != null && _atmosphereSystem == null)
                _atmosphereSystem = submarineCore.AtmosphereSystem;

            RefreshAtlas6ManagerReference(publishIfChanged: true);
        }

        private void RefreshAtlas6ManagerReference(bool publishIfChanged)
        {
            Hecton8.Gameplay.Atlas6Liability.Atlas6CorporateLiabilityManager activeRuntime =
                Hecton8.Gameplay.Atlas6Liability.Atlas6CorporateLiabilityManager.ActiveRuntimeInstance;
            if (ReferenceEquals(_atlas6Manager, activeRuntime))
                return;

            _atlas6Manager = activeRuntime;
            if (!publishIfChanged || !_runtimeLifecycleStarted || !_subOsPowered || !CanUseRuntimeDispatcher())
                return;

            RefreshEngineDiagnosticsTelemetry(DiagnosticsRefreshIntervalSeconds);
            PublishCurrentSnapshotIfChanged();
        }

        private void TryRegisterAtlas6ActiveRuntimeListener()
        {
            if (!Application.isPlaying)
                return;

            if (_registeredAtlas6ActiveRuntimeListener)
                Hecton8.Gameplay.Atlas6Liability.Atlas6CorporateLiabilityManager.ActiveRuntimeInstanceChanged -= HandleAtlas6ActiveRuntimeInstanceChanged;

            Hecton8.Gameplay.Atlas6Liability.Atlas6CorporateLiabilityManager.ActiveRuntimeInstanceChanged += HandleAtlas6ActiveRuntimeInstanceChanged;
            _registeredAtlas6ActiveRuntimeListener = true;
            RefreshAtlas6ManagerReference(publishIfChanged: false);
        }

        private void TryUnregisterAtlas6ActiveRuntimeListener()
        {
            if (!_registeredAtlas6ActiveRuntimeListener)
                return;

            Hecton8.Gameplay.Atlas6Liability.Atlas6CorporateLiabilityManager.ActiveRuntimeInstanceChanged -= HandleAtlas6ActiveRuntimeInstanceChanged;
            _registeredAtlas6ActiveRuntimeListener = false;
        }

        private void HandleAtlas6ActiveRuntimeInstanceChanged(
            Hecton8.Gameplay.Atlas6Liability.Atlas6CorporateLiabilityManager activeRuntime)
        {
            if (ReferenceEquals(_atlas6Manager, activeRuntime))
                return;

            _atlas6Manager = activeRuntime;
            if (!_runtimeLifecycleStarted || !_subOsPowered || !CanUseRuntimeDispatcher())
                return;

            RefreshEngineDiagnosticsTelemetry(DiagnosticsRefreshIntervalSeconds);
            PublishCurrentSnapshotIfChanged();
        }

        private void RefreshColdRegistryReferences()
        {
            _runtimeDispatcherReady = GlobalRegistry.Dispatcher != null;
            _powerGridService = GlobalRegistry.PowerGrid;
            _spectrumRuntime = GlobalRegistry.Spectrum;
            _playerRuntime = GlobalRegistry.Player;
        }

        private void Subscribe()
        {
            PowerGridTelemetryEvents.Unregister(this);
            PowerGridTelemetryEvents.Register(this);
            HighPressureEvents.Unregister(this);
            HighPressureEvents.Register(this);
            FatalPressureImplosionEvents.Unregister(this);
            FatalPressureImplosionEvents.Register(this);
            HectonDroneFleetEvents.Unregister(this);
            HectonDroneFleetEvents.Register(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
            SpectrumEvents.RegisterSonarSnapshotListener(this);
        }

        private void Unsubscribe()
        {
            PowerGridTelemetryEvents.Unregister(this);
            HighPressureEvents.Unregister(this);
            FatalPressureImplosionEvents.Unregister(this);
            HectonDroneFleetEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
        }

        /// <inheritdoc />
        public void OnDroneFleetSnapshotUpdated(in HectonDroneFleetSnapshot snapshot)
        {
            HandleFleetSnapshotUpdated(in snapshot);
        }

        /// <inheritdoc />
        public void OnSonarPingSent(float intensity)
        {
            _sonarPingIntensity = math.max(_sonarPingIntensity, math.saturate(intensity));
            _sonarSweepPhase = 0f;
            RefreshSonarSweepGlobal(0f);
        }

        /// <inheritdoc />
        public void OnSonarSnapshotUpdated(in SpatialSonarSnapshot snapshot)
        {
            _lastSonarSnapshot = snapshot;
            RefreshSonarDerivedTelemetry();
            ApplyNavigationShaderGlobal();
        }

        private void HandleFleetSnapshotUpdated(in HectonDroneFleetSnapshot snapshot)
        {
            _fleetSnapshot = snapshot;
            int alarmSequence = math.max(snapshot.LogicLeechHijackCount, snapshot.HostileDroneCount > 0 ? 1 : 0);
            if (alarmSequence <= _hostileDroneAlarmCount)
                return;

            _hostileDroneAlarmCount = alarmSequence;
            PublishLog(HectonSubmarineOsLogCode.HostileDroneDetected, LogPriorityCritical);
            QueueVoiceAlarm(
                multiSystemFailureEventId,
                HostileDroneCaptionHash,
                1f,
                (byte)VocalWarningId.HullBreach,
                VocalWarningSignalFlags.HabitatIntegrityCompromised);
        }

        private void TryRegister()
        {
            if (!CanUseRuntimeDispatcher())
                return;

            if (!_registeredUpdatable)
            {
                _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredRenderable)
            {
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
            }
        }

        private bool CanUseRuntimeDispatcher()
        {
            if (!Application.isPlaying || !_runtimeDispatcherReady)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
                return false;
#endif

            return true;
        }

        private void TryUnregister()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _runtimeDispatcherReady = currentService != null;
                    TryUnregisterDispatcherTicks();
                    if (_runtimeDispatcherReady && isActiveAndEnabled)
                    {
                        if (_runtimeLifecycleStarted)
                            TryRegister();
                        else
                            TryStartRuntimeLifecycle();
                    }
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGridService = currentService as IPowerGridService;
                    RefreshTelemetryFromServices();
                    PublishCurrentSnapshotIfRuntimeReady();
                    break;
                case GlobalRegistryServiceSlot.SpectrumRuntime:
                    _spectrumRuntime = currentService as SpectrumSystem;
                    RefreshSubsystemStatus();
                    PublishCurrentSnapshotIfRuntimeReady();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntime = currentService as IPlayerRuntimeContext;
                    RefreshPlayerDrivenStateAfterServiceReplacement();
                    break;
            }
        }

        private bool ResolveSubOsPowered()
        {
            return _powerNormalized > SubOsUnpoweredThreshold01 || _powerSupplyRatio > SubOsUnpoweredThreshold01;
        }

        private void SetSubOsPowered(bool powered)
        {
            if (_subOsPowered == powered)
                return;

            _subOsPowered = powered;
            if (!powered)
            {
                _navigationRefreshAccumulator = 0f;
                _diagnosticsRefreshAccumulator = 0f;
                _sonarPingIntensity = 0f;
                _sonarSweepPhase = 0f;
                _lightingPulsePhase = 0f;
                _vwsActiveFlags = SubmarineVwsFlags.None;
                ResetSubOsShaderGlobals();
                PublishShutdownSnapshot();
                return;
            }

            PublishLog(HectonSubmarineOsLogCode.ReactorStable, LogPriorityNormal);
            RefreshNavigationTelemetry();
            RefreshEngineDiagnosticsTelemetry(DiagnosticsRefreshIntervalSeconds);
            EvaluateStateMachine(true);
            HectonSubmarineOsDisplay.EnsureRuntimeInstance();
        }

        private void PublishCurrentSnapshotIfRuntimeReady()
        {
            if (!_runtimeLifecycleStarted || !CanUseRuntimeDispatcher())
                return;

            SetSubOsPowered(ResolveSubOsPowered());
            if (_subOsPowered)
                PublishCurrentSnapshotIfChanged();
        }

        private void RefreshPlayerDrivenStateAfterServiceReplacement()
        {
            if (!_runtimeLifecycleStarted || !_subOsPowered || !CanUseRuntimeDispatcher())
                return;

            EvaluateStateMachine(false);
            PublishCurrentSnapshotIfChanged();
        }

        private void ResetSubOsShaderGlobals()
        {
            _subOsShaderResetDirty = true;
            _pendingBrownoutPulseShaderGlobal = 0f;
            _brownoutPulseShaderGlobalDirty = true;
            _pendingNavigationShaderGlobal = Vector4.zero;
            _pendingEngineDiagnosticsShaderGlobal = Vector4.zero;
            _navigationShaderGlobalDirty = false;
            _engineDiagnosticsShaderGlobalDirty = false;
        }

        private void RefreshTelemetryFromServices()
        {
            IPowerGridService powerGridService = _powerGridService;
            if (powerGridService == null)
            {
                ResetPowerTelemetryFallback();
            }
            else
            {
                BatteryRuntimeSnapshot batterySnapshot = powerGridService.BatterySnapshot;
                _powerSupplyRatio = ResolveSupplyRatio(powerGridService.TotalGeneration, powerGridService.TotalConsumption);
                _powerNormalized = batterySnapshot.TotalCapacityWattSeconds > 0.0001f
                    ? SaturateFinite(batterySnapshot.ChargeNormalized, _powerSupplyRatio)
                    : _powerSupplyRatio;
            }

            RefreshAtmosphereTelemetry();
            RefreshSubsystemStatus();
        }

        private void ResetPowerTelemetryFallback()
        {
            _powerSupplyRatio = 1f;
            _powerNormalized = 1f;
            _highestBrownoutTier = LogisticsBrownoutTier.None;
            _cascadingBrownoutActive = false;
        }

        private void RefreshAtmosphereTelemetry()
        {
            ISubmarineAtmosphereRoomReadModel atmosphereSystem = _atmosphereSystem;
            if (atmosphereSystem == null || !atmosphereSystem.IsAtmosphereRuntimeActive)
            {
                _oxygenNormalized = 1f;
                _carbonDioxideNormalized = 0f;
                _maxPressureKPa = DefaultReferencePressureKPa;
                return;
            }

            int roomCount = atmosphereSystem.RoomCount;
            if (roomCount <= 0)
            {
                _oxygenNormalized = 1f;
                _carbonDioxideNormalized = 0f;
                _maxPressureKPa = DefaultReferencePressureKPa;
                return;
            }

            float minOxygenFraction = 1f;
            float maxCarbonDioxideFraction = 0f;
            float maxPressureKPa = 0f;
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                float oxygenFraction = atmosphereSystem.GetRoomOxygenFraction(roomIndex);
                if (math.isfinite(oxygenFraction))
                    minOxygenFraction = math.min(minOxygenFraction, oxygenFraction);

                float carbonDioxideFraction = atmosphereSystem.GetRoomCarbonDioxidePressureFraction(roomIndex);
                if (math.isfinite(carbonDioxideFraction))
                    maxCarbonDioxideFraction = math.max(maxCarbonDioxideFraction, carbonDioxideFraction);

                float pressureKPa = atmosphereSystem.GetRoomPressureKPa(roomIndex);
                if (math.isfinite(pressureKPa))
                    maxPressureKPa = math.max(maxPressureKPa, math.max(0f, pressureKPa));
            }

            _oxygenNormalized = SaturateFinite(minOxygenFraction, 1f);
            _carbonDioxideNormalized = SaturateFinite(maxCarbonDioxideFraction, 0f);
            _maxPressureKPa = math.max(DefaultReferencePressureKPa, NonNegativeFinite(maxPressureKPa, DefaultReferencePressureKPa));
        }

        private void RefreshSubsystemStatus()
        {
            SubsystemStatus subsystemStatus = SubsystemStatus.None;
            if (_submarineCore != null && _submarineCore.HullRigidbody != null && _submarineCore.IsTransportPlatformActive)
                subsystemStatus |= SubsystemStatus.Engines;

            if (_atmosphereSystem != null && _atmosphereSystem.IsAtmosphereRuntimeActive && !_lifeSupportCriticalActive)
                subsystemStatus |= SubsystemStatus.LifeSupport;

            if (!_lowPowerModeActive)
                subsystemStatus |= SubsystemStatus.Lights;

            SpectrumSystem spectrumSystem = _spectrumRuntime;
            if (spectrumSystem != null && spectrumSystem.isActiveAndEnabled)
                subsystemStatus |= SubsystemStatus.Sonar;

            _subsystemStatus = subsystemStatus;
        }

        private void RefreshNavigationTelemetry()
        {
            Rigidbody hullRigidbody = _submarineCore != null ? _submarineCore.HullRigidbody : null;
            Vector3 origin = hullRigidbody != null ? hullRigidbody.worldCenterOfMass : transform.position;
            if (hullRigidbody != null && IsFinite(origin))
                WorldSpatialHashGrid.BuildSonarSnapshot(origin, SonarMonitorRadiusMeters, out _lastSonarSnapshot);
            else
                _lastSonarSnapshot = default;

            _speedKnots = ResolveHullSpeedMetersPerSecond() * KnotsPerMeterPerSecond;
            RefreshSonarDerivedTelemetry();
            ApplyNavigationShaderGlobal();
        }

        private void RefreshEngineDiagnosticsTelemetry(float elapsedSeconds)
        {
            float safeElapsed = math.max(0.0001f, elapsedSeconds);
            float hullSpeedMetersPerSecond = ResolveHullSpeedMetersPerSecond();
            float speedLoad01 = math.saturate(hullSpeedMetersPerSecond * EngineHeatSpeedReferenceInv);
            float elapsedInv = math.rcp(safeElapsed);
            float accelerationLoad01 = math.saturate(
                math.abs(hullSpeedMetersPerSecond - _lastHullSpeedMetersPerSecond) *
                elapsedInv *
                EngineHeatAccelerationReferenceInv);
            float targetHeat01 = math.saturate(math.max(speedLoad01 * EngineHeatCruiseLoadScale, accelerationLoad01));
            float trueHeat01 = targetHeat01;
            float maskDelta01 = 0f;
            uint atlasTelemetryFlags = 0u;

            if (_atlas6Manager != null)
            {
                var readout = _atlas6Manager.GetSubmarineOSReadout(targetHeat01);
                targetHeat01 = readout.ReportedSheer;
                trueHeat01 = readout.TrueSheer;
                maskDelta01 = readout.MaskDelta01;
                atlasTelemetryFlags = readout.Flags;
            }

            _engineHeat01 = QuantizeHeat01(targetHeat01);
            _engineHeatTrue01 = QuantizeHeat01(trueHeat01);
            _engineHeatMaskDelta01 = QuantizeHeat01(maskDelta01);
            _atlasTelemetryFlags = atlasTelemetryFlags;
            bool nextEngineTelemetryMaskActive =
                (atlasTelemetryFlags & Hecton8.Gameplay.Atlas6Liability.ThermalSheerManager.TelemetryFlagMasked) != 0u;
            if (nextEngineTelemetryMaskActive != _engineTelemetryMaskActive)
            {
                _engineTelemetryMaskActive = nextEngineTelemetryMaskActive;
                PublishLog(
                    nextEngineTelemetryMaskActive
                        ? HectonSubmarineOsLogCode.EngineTelemetryMasked
                        : HectonSubmarineOsLogCode.EngineTelemetryRestored,
                    nextEngineTelemetryMaskActive ? LogPriorityWarning : LogPriorityNormal);
            }

            _lastHullSpeedMetersPerSecond = hullSpeedMetersPerSecond;
            ApplyEngineDiagnosticsShaderGlobal();
        }

        private float ResolveHullSpeedMetersPerSecond()
        {
            Rigidbody hullRigidbody = _submarineCore != null ? _submarineCore.HullRigidbody : null;
            if (hullRigidbody == null)
                return 0f;

            Vector3 velocity = hullRigidbody.linearVelocity;
            if (!IsFinite(velocity))
                return 0f;

            float3 absVelocity = math.abs((float3)velocity);
            float major = math.cmax(absVelocity);
            float minor = math.cmin(absVelocity);
            float middle = absVelocity.x + absVelocity.y + absVelocity.z - major - minor;
            return major + (middle * 0.375f) + (minor * 0.125f);
        }

        private static float QuantizeHeat01(float value)
        {
            return math.floor(SaturateFinite(value, 0f) * EngineHeatQuantizeScale + 0.5f) * EngineHeatQuantizeInv;
        }

        private void RefreshSonarDerivedTelemetry()
        {
            SpatialSonarSnapshot snapshot = _lastSonarSnapshot;
            _sonarContactCount = math.max(0, snapshot.ResourceCount) +
                                 math.max(0, snapshot.BioformCount) +
                                 math.max(0, snapshot.SignalCount);

            int nearest = int.MaxValue;
            if (SpatialSonarSnapshot.HasNearestResource(in snapshot))
                nearest = math.min(nearest, math.max(0, snapshot.NearestResourceDistanceMeters));
            if (SpatialSonarSnapshot.HasNearestBioform(in snapshot))
                nearest = math.min(nearest, math.max(0, snapshot.NearestBioformDistanceMeters));
            if (SpatialSonarSnapshot.HasNearestSignal(in snapshot))
                nearest = math.min(nearest, math.max(0, snapshot.NearestSignalDistanceMeters));

            _nearestSonarContactMeters = nearest == int.MaxValue ? 0 : nearest;
        }

        private void RefreshSonarSweepGlobal(float deltaTime)
        {
            if (_sonarPingIntensity > 0f)
            {
                _sonarSweepPhase = math.saturate(_sonarSweepPhase + deltaTime * SonarSweepDecayPerSecond);
                _sonarPingIntensity = math.max(0f, _sonarPingIntensity - deltaTime * SonarSweepDecayPerSecond);
            }
            else
            {
                _sonarSweepPhase = 0f;
            }

            Shader.SetGlobalVector(
                _HectonSubOsSonarSweepId,
                new Vector4(_sonarSweepPhase, _sonarPingIntensity, _sonarContactCount, SonarMonitorRadiusMeters));
        }

        private float RefreshCachedSonarQualityWeight()
        {
            float qualityWeight01 = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(qualityWeight01))
                _cachedSonarQualityWeight01 = math.saturate(qualityWeight01);

            return _cachedSonarQualityWeight01;
        }

        private static float SmoothQuality01(float value)
        {
            float t = math.saturate(math.select(1f, value, math.isfinite(value)));
            return t * t * (3f - (2f * t));
        }

        private static float ResolveSonarRefreshIntervalSeconds(float qualityWeight01)
        {
            float quality = SmoothQuality01(qualityWeight01);
            float survivalToStandard = math.lerp(
                SurvivalSonarRefreshIntervalSeconds,
                StandardSonarRefreshIntervalSeconds,
                math.saturate(quality * 2f));
            float standardToOverkill = math.lerp(
                StandardSonarRefreshIntervalSeconds,
                VisualOverkillSonarRefreshIntervalSeconds,
                math.saturate((quality - 0.5f) * 2f));
            return math.lerp(survivalToStandard, standardToOverkill, quality);
        }

        private static float ResolveSonarInterpolationWeight(float qualityWeight01)
        {
            return SmoothQuality01((qualityWeight01 - 0.5f) * 2f);
        }

        private void ApplySonarLodShaderGlobal(bool force = false)
        {
            float qualityWeight01 = RefreshCachedSonarQualityWeight();
            if (!force && math.abs(qualityWeight01 - _lastAppliedSonarQualityWeight01) < 0.001f)
                return;

            float refreshInterval = ResolveSonarRefreshIntervalSeconds(qualityWeight01);
            float interpolationWeight = ResolveSonarInterpolationWeight(qualityWeight01);
            Shader.SetGlobalVector(
                _HectonSubOsSonarLodId,
                new Vector4(refreshInterval, math.rcp(math.max(0.0001f, refreshInterval)), interpolationWeight, qualityWeight01));
            _lastAppliedSonarQualityWeight01 = qualityWeight01;
        }

        private void ApplyNavigationShaderGlobal()
        {
            _pendingNavigationShaderGlobal = new Vector4(_speedKnots, _sonarContactCount, _nearestSonarContactMeters, _subOsPowered ? 1f : 0f);
            _navigationShaderGlobalDirty = true;
        }

        private void ApplyEngineDiagnosticsShaderGlobal()
        {
            _pendingEngineDiagnosticsShaderGlobal = new Vector4(_engineHeat01, _powerSupplyRatio, _powerNormalized, _subOsPowered ? 1f : 0f);
            _engineDiagnosticsShaderGlobalDirty = true;
        }

        private void ApplyLightingStateGlobal(float deltaTime)
        {
            float lightingMode = ResolveLightingMode();
            if (lightingMode >= 2f)
                _lightingPulsePhase = math.frac(_lightingPulsePhase + deltaTime * BrownoutBlinkFrequency);
            else
                _lightingPulsePhase = 0f;

            float emergencyPulse = lightingMode >= 2f
                ? 1f - math.abs((_lightingPulsePhase * 2f) - 1f)
                : 0f;
            Vector4 lightingState = new Vector4(lightingMode, emergencyPulse, _powerNormalized, (float)_emergencyLevel);
            Shader.SetGlobalVector(_HectonSubOsLightingStateId, lightingState);
            Shader.SetGlobalVector(_SubInteriorLightingStateId, lightingState);
        }

        private float ResolveLightingMode()
        {
            if (_emergencyLevel >= SubmarineEmergencyLevel.Danger || _fatalImplosionLatched || _pressureHighActive || _vitalWarningActive)
                return 2f;

            if (_lowPowerModeActive || _cascadingBrownoutActive)
                return 1f;

            return 0f;
        }

        private void EvaluateStateMachine(bool forceLog)
        {
            bool nextLowPowerActive = _lowPowerModeActive
                ? _powerNormalized < LowPowerReleaseThreshold01
                : _powerNormalized < LowPowerThreshold01;
            bool nextLifeSupportCritical = _lifeSupportCriticalActive
                ? _oxygenNormalized < LifeSupportReleaseThreshold01
                : _oxygenNormalized < LifeSupportCriticalThreshold01;
            bool nextPressureHighActive = _pressureHighActive
                ? _maxPressureKPa > PressureReleaseThresholdKPa
                : _maxPressureKPa > PressureHighThresholdKPa;
            bool nextVitalWarningActive = ResolvePlayerVitalWarningActive();

            if (nextLowPowerActive != _lowPowerModeActive)
            {
                SetLowPowerMode(nextLowPowerActive);
                PublishLog(
                    nextLowPowerActive ? HectonSubmarineOsLogCode.LowPowerModeEngaged : HectonSubmarineOsLogCode.LowPowerModeCleared,
                    nextLowPowerActive ? LogPriorityWarning : LogPriorityNormal);
            }

            if (nextLifeSupportCritical != _lifeSupportCriticalActive)
            {
                _lifeSupportCriticalActive = nextLifeSupportCritical;
                PublishLog(
                    nextLifeSupportCritical ? HectonSubmarineOsLogCode.LifeSupportCritical : HectonSubmarineOsLogCode.LifeSupportStabilized,
                    nextLifeSupportCritical ? LogPriorityCritical : LogPriorityNormal);
            }

            if (nextPressureHighActive != _pressureHighActive)
            {
                _pressureHighActive = nextPressureHighActive;
                PublishLog(
                    nextPressureHighActive ? HectonSubmarineOsLogCode.HullPressureHigh : HectonSubmarineOsLogCode.HullPressureStabilized,
                    nextPressureHighActive ? LogPriorityWarning : LogPriorityNormal);
            }

            _vitalWarningActive = nextVitalWarningActive;

            SetCascadingBrownout(ResolveCascadingBrownoutActive());

            RefreshSubsystemStatus();

            bool nextStationKeepingActive = _stationKeepingController != null && _stationKeepingController.IsStationKeepingEnabled;
            if (nextStationKeepingActive != _stationKeepingStateCached)
            {
                _stationKeepingStateCached = nextStationKeepingActive;
                PublishLog(
                    nextStationKeepingActive ? HectonSubmarineOsLogCode.StationKeepingArmed : HectonSubmarineOsLogCode.StationKeepingReleased,
                    LogPriorityNormal);
            }

            int failureCount = 0;
            if (_lowPowerModeActive)
                failureCount++;
            if (_lifeSupportCriticalActive)
                failureCount++;
            if (_pressureHighActive)
                failureCount++;
            if (_vitalWarningActive)
                failureCount++;
            if (_fatalImplosionLatched)
                failureCount++;

            bool multiSystemFailure = failureCount >= 2;
            if (multiSystemFailure && !_multiSystemFailureLatched)
            {
                _multiSystemFailureLatched = true;
                PublishLog(HectonSubmarineOsLogCode.MultiSystemFailure, LogPriorityCritical);
            }
            else if (!multiSystemFailure)
            {
                _multiSystemFailureLatched = false;
            }

            SubmarineEmergencyLevel nextEmergencyLevel = ResolveEmergencyLevel();
            if (forceLog || nextEmergencyLevel != _emergencyLevel)
            {
                _emergencyLevel = nextEmergencyLevel;
                PublishLog(ResolveEmergencyLevelLogCode(_emergencyLevel), _emergencyLevel >= SubmarineEmergencyLevel.Danger ? LogPriorityCritical : LogPriorityNormal);
                if (_emergencyLevel >= SubmarineEmergencyLevel.Danger)
                    PlayEmergencyLevelAlarm(_emergencyLevel);
            }

            ProcessVwsFlags();
            PublishCurrentSnapshotIfChanged();
        }

        private void SetLowPowerMode(bool active)
        {
            if (_lowPowerModeActive == active)
                return;

            _lowPowerModeActive = active;
            Fabricator.SetEmergencyPowerLockAll(active);
            ApplyAmbientLightPolicy(active);
        }

        private void ApplyAmbientLightPolicy(bool forceBrownout)
        {
            int moduleCount = BaseModule.ActiveModuleCount;
            if (moduleCount <= 0)
                return;

            for (int i = 0; i < moduleCount; i++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(i);
                if (module == null)
                    continue;

                bool shouldBrownOut = forceBrownout || ResolveModuleGridBrownout(module);
                module.SetAmbientLightsBrownout(shouldBrownOut);
            }
        }

        private static bool ResolveModuleGridBrownout(BaseModule module)
        {
            PowerGrid grid = module != null ? module.CachedPowerGrid : null;
            if (grid == null)
                return false;

            return grid.BrownoutTier != LogisticsBrownoutTier.None || grid.IsBatteryEmergencyReserveActive;
        }

        /// <summary>
        /// Receives deferred aggregate power telemetry snapshots.
        /// </summary>
        /// <param name="snapshot">Aggregate power telemetry snapshot.</param>
        public void OnPowerGridTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot)
        {
            _powerNormalized = SaturateFinite(snapshot.AvailablePowerNormalized, _powerNormalized);
            _powerSupplyRatio = SaturateFinite(snapshot.SupplyRatio, _powerSupplyRatio);
            _highestBrownoutTier = PowerGridTelemetrySnapshot.GetHighestBrownoutTier(in snapshot);
            SetSubOsPowered(ResolveSubOsPowered());
            if (!_subOsPowered)
                return;

            SetCascadingBrownout(ResolveCascadingBrownoutActive());
        }

        /// <summary>
        /// Receives deferred high-pressure warnings from the submarine atmosphere event lane.
        /// </summary>
        public void OnHighPressure(in HighPressureEvent pressureEvent)
        {
            HandleHighPressure(in pressureEvent);
        }

        /// <summary>
        /// Receives deferred fatal pressure implosion notifications from the submarine atmosphere event lane.
        /// </summary>
        public void OnFatalPressureImplosion(in FatalPressureImplosionEvent implosionEvent)
        {
            HandleFatalPressureImplosion(in implosionEvent);
        }

        private void HandleHighPressure(in HighPressureEvent pressureEvent)
        {
            float pressureA = NonNegativeFinite(pressureEvent.PressureAKPa, _maxPressureKPa);
            float pressureB = NonNegativeFinite(pressureEvent.PressureBKPa, _maxPressureKPa);
            _maxPressureKPa = math.max(_maxPressureKPa, math.max(pressureA, pressureB));
        }

        private void HandleFatalPressureImplosion(in FatalPressureImplosionEvent implosionEvent)
        {
            if (_fatalImplosionLatched)
                return;

            _fatalImplosionLatched = true;
            PublishLog(HectonSubmarineOsLogCode.FatalImplosion, LogPriorityCritical);
            ProcessVwsFlags();
        }

        private void ProcessVwsFlags()
        {
            SubmarineVwsFlags nextFlags = ResolveVwsFlags();
            SubmarineVwsFlags risingFlags = nextFlags & ~_vwsActiveFlags;
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            uint activeMask = (uint)(ushort)nextFlags;
            while (activeMask != 0u)
            {
                int bitIndex = math.tzcnt(activeMask);
                uint flagBit = 1u << bitIndex;
                activeMask &= activeMask - 1u;
                TryPlayVwsFlagByBit((SubmarineVwsFlags)flagBit, risingFlags, now);
            }

            _vwsActiveFlags = nextFlags;
        }

        private SubmarineVwsFlags ResolveVwsFlags()
        {
            SubmarineVwsFlags flags = SubmarineVwsFlags.None;
            if (_lowPowerModeActive || _powerNormalized <= LowPowerThreshold01)
                flags |= SubmarineVwsFlags.PowerLow;

            float oxygen01 = ResolveVwsOxygenNormalized();
            if (oxygen01 <= EvacuateOxygenThreshold01 || _lifeSupportCriticalActive)
                flags |= SubmarineVwsFlags.OxygenCritical;
            else if (oxygen01 <= OxygenLowVwsThreshold01)
                flags |= SubmarineVwsFlags.OxygenLow;

            if (ResolveHullBreachActive())
                flags |= SubmarineVwsFlags.HullBreach;

            if (_pressureHighActive || _maxPressureKPa >= PressureHighThresholdKPa)
                flags |= SubmarineVwsFlags.PressureHigh;

            if (_fatalImplosionLatched)
                flags |= SubmarineVwsFlags.FatalPressure;

            if (_multiSystemFailureLatched)
                flags |= SubmarineVwsFlags.MultiSystemFailure;

            HectonSurvivalSystem survivalSystem = ResolvePlayerSurvivalSystem();
            if (survivalSystem != null &&
                SaturateFinite(survivalSystem.ThermalStressSeverity01, 0f) >= ThermalStressVwsThreshold01)
            {
                flags |= SubmarineVwsFlags.ThermalStress;
            }

            return flags;
        }

        private float ResolveVwsOxygenNormalized()
        {
            float oxygen01 = _oxygenNormalized;
            HectonSurvivalSystem survivalSystem = ResolvePlayerSurvivalSystem();
            if (survivalSystem != null)
                oxygen01 = math.min(oxygen01, SaturateFinite(survivalSystem.OxygenNormalized, oxygen01));

            return SaturateFinite(oxygen01, 1f);
        }

        private HectonSurvivalSystem ResolvePlayerSurvivalSystem()
        {
            IPlayerRuntimeContext playerContext = _playerRuntime;
            return playerContext != null ? playerContext.SurvivalSystem : null;
        }

        private bool ResolvePlayerVitalWarningActive()
        {
            IPlayerRuntimeContext playerContext = _playerRuntime;
            HectonPlayerHealth playerHealth = playerContext != null ? playerContext.PlayerHealth : null;
            if (playerHealth == null)
                return false;

            float health01 = SaturateFinite(playerHealth.HealthPercent, 1f);
            float threshold01 = _vitalWarningActive
                ? VitalWarningHealthReleaseThreshold01
                : VitalWarningHealthThreshold01;
            return health01 <= threshold01;
        }

        private bool ResolveHullBreachActive()
        {
            if (_submarineCore == null)
                return false;

            var structuralGrid = _submarineCore.StructuralGrid;
            var fluidDynamics = _submarineCore.FluidDynamics;
            if (structuralGrid == null || fluidDynamics == null || !structuralGrid.IsReady)
                return false;

            int compartmentCount = math.clamp(fluidDynamics.CompartmentCount, 0, 32);
            for (int compartmentIndex = 0; compartmentIndex < compartmentCount; compartmentIndex++)
            {
                if (structuralGrid.GetCompartmentBreachAreaSquareMeters(compartmentIndex) > HullBreachAreaThresholdSquareMeters)
                    return true;
            }

            return false;
        }

        private void TryPlayVwsFlagByBit(SubmarineVwsFlags flag, SubmarineVwsFlags risingFlags, double now)
        {
            switch (flag)
            {
                case SubmarineVwsFlags.PowerLow:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        lowPowerWarningEventId,
                        LowPowerCaptionHash,
                        0.8f,
                        (byte)VocalWarningId.PowerLow,
                        0,
                        ref _nextPowerLowVwsTime,
                        now);
                    break;
                case SubmarineVwsFlags.OxygenLow:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        oxygenLowWarningEventId != 0u ? oxygenLowWarningEventId : lifeSupportCriticalEventId,
                        OxygenLowCaptionHash,
                        0.85f,
                        (byte)VocalWarningId.OxygenLow,
                        0,
                        ref _nextOxygenLowVwsTime,
                        now);
                    break;
                case SubmarineVwsFlags.OxygenCritical:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        lifeSupportCriticalEventId != 0u ? lifeSupportCriticalEventId : oxygenLowWarningEventId,
                        OxygenCriticalCaptionHash,
                        1f,
                        (byte)VocalWarningId.OxygenLow,
                        0,
                        ref _nextOxygenCriticalVwsTime,
                        now);
                    break;
                case SubmarineVwsFlags.HullBreach:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        hullBreachWarningEventId != 0u ? hullBreachWarningEventId : multiSystemFailureEventId,
                        HullBreachCaptionHash,
                        1f,
                        (byte)VocalWarningId.HullBreach,
                        VocalWarningSignalFlags.HabitatIntegrityCompromised,
                        ref _nextHullBreachVwsTime,
                        now);
                    break;
                case SubmarineVwsFlags.PressureHigh:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        hullStressWarningEventId != 0u ? hullStressWarningEventId : multiSystemFailureEventId,
                        PressureHighCaptionHash,
                        0.85f,
                        (byte)VocalWarningId.CrushDepth,
                        VocalWarningSignalFlags.HabitatIntegrityCompromised,
                        ref _nextPressureHighVwsTime,
                        now);
                    break;
                case SubmarineVwsFlags.FatalPressure:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        abandonShipAlarmEventId != 0u ? abandonShipAlarmEventId : multiSystemFailureEventId,
                        AbandonShipCaptionHash,
                        1f,
                        (byte)VocalWarningId.CrushDepth,
                        VocalWarningSignalFlags.HabitatIntegrityCompromised,
                        ref _nextFatalPressureVwsTime,
                        now);
                    break;
                case SubmarineVwsFlags.ThermalStress:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        hullStressWarningEventId != 0u ? hullStressWarningEventId : multiSystemFailureEventId,
                        ThermalStressCaptionHash,
                        0.75f,
                        (byte)VocalWarningId.Radiation,
                        0,
                        ref _nextThermalStressVwsTime,
                        now);
                    break;
                case SubmarineVwsFlags.MultiSystemFailure:
                    TryPlayVwsFlag(
                        risingFlags,
                        flag,
                        multiSystemFailureEventId,
                        MultiFailureCaptionHash,
                        1f,
                        (byte)VocalWarningId.HullBreach,
                        VocalWarningSignalFlags.HabitatIntegrityCompromised,
                        ref _nextMultiFailureVwsTime,
                        now);
                    break;
            }
        }

        private void TryPlayVwsFlag(
            SubmarineVwsFlags risingFlags,
            SubmarineVwsFlags flag,
            uint eventId,
            uint captionHashId,
            float intensity,
            byte warningId,
            byte warningFlags,
            ref double nextAllowedTime,
            double now)
        {
            bool rising = (risingFlags & flag) != 0;
            if (!rising && now < nextAllowedTime)
                return;

            QueueVoiceAlarm(eventId, captionHashId, intensity, warningId, warningFlags);
            nextAllowedTime = now + VwsRepeatCooldownSeconds;
        }

        private void PlayEmergencyLevelAlarm(SubmarineEmergencyLevel emergencyLevel)
        {
            switch (emergencyLevel)
            {
                case SubmarineEmergencyLevel.Evacuate:
                    QueueVoiceAlarm(
                        abandonShipAlarmEventId != 0u
                            ? abandonShipAlarmEventId
                            : (lifeSupportCriticalEventId != 0u ? lifeSupportCriticalEventId : multiSystemFailureEventId),
                        AbandonShipCaptionHash,
                        1f,
                        (byte)VocalWarningId.CrushDepth,
                        VocalWarningSignalFlags.HabitatIntegrityCompromised);
                    break;

                case SubmarineEmergencyLevel.Danger:
                    QueueVoiceAlarm(
                        multiSystemFailureEventId != 0u ? multiSystemFailureEventId : lifeSupportCriticalEventId,
                        EmergencyDangerCaptionHash,
                        1f,
                        (byte)VocalWarningId.HullBreach,
                        VocalWarningSignalFlags.HabitatIntegrityCompromised);
                    break;
            }
        }

        private void SetCascadingBrownout(bool active)
        {
            if (_cascadingBrownoutActive == active)
                return;

            _cascadingBrownoutActive = active;
            if (!active)
            {
                _brownoutPulsePhase = 0f;
                _pendingBrownoutPulseShaderGlobal = 0f;
                _brownoutPulseShaderGlobalDirty = true;
            }
        }

        private bool ResolveCascadingBrownoutActive()
        {
            if (_powerSupplyRatio >= CascadingBrownoutThreshold01)
                return false;

            return _highestBrownoutTier >= LogisticsBrownoutTier.EssentialOnly || _powerNormalized < CascadingBrownoutThreshold01;
        }

        private void RestoreBrownoutVisualsImmediate()
        {
            _brownoutPulsePhase = 0f;
            Shader.SetGlobalFloat(_HectonBrownoutPulseId, 0f);
        }

        private void FlushQueuedSubOsShaderGlobals()
        {
            if (_subOsShaderResetDirty)
            {
                _subOsShaderResetDirty = false;
                Shader.SetGlobalFloat(_HectonBrownoutPulseId, 0f);
                Shader.SetGlobalVector(_HectonSubOsLightingStateId, Vector4.zero);
                Shader.SetGlobalVector(_SubInteriorLightingStateId, Vector4.zero);
                Shader.SetGlobalVector(_HectonSubOsSonarSweepId, Vector4.zero);
                Shader.SetGlobalVector(_HectonSubOsNavigationId, Vector4.zero);
                Shader.SetGlobalVector(_HectonSubOsEngineDiagnosticsId, Vector4.zero);
                _brownoutPulseShaderGlobalDirty = false;
                _navigationShaderGlobalDirty = false;
                _engineDiagnosticsShaderGlobalDirty = false;
                return;
            }

            if (_navigationShaderGlobalDirty)
            {
                _navigationShaderGlobalDirty = false;
                Shader.SetGlobalVector(_HectonSubOsNavigationId, _pendingNavigationShaderGlobal);
            }

            if (_engineDiagnosticsShaderGlobalDirty)
            {
                _engineDiagnosticsShaderGlobalDirty = false;
                Shader.SetGlobalVector(_HectonSubOsEngineDiagnosticsId, _pendingEngineDiagnosticsShaderGlobal);
            }

            if (_brownoutPulseShaderGlobalDirty)
            {
                _brownoutPulseShaderGlobalDirty = false;
                Shader.SetGlobalFloat(_HectonBrownoutPulseId, _pendingBrownoutPulseShaderGlobal);
            }
        }

        private void QueueVoiceAlarm(uint eventId, uint captionHashId, float intensity, byte warningId, byte warningFlags)
        {
            byte normalizedWarningId = warningId >= (byte)VocalWarningId.CrushDepth && warningId <= (byte)VocalWarningId.Toxicity
                ? warningId
                : (byte)0;
            if (normalizedWarningId == 0)
                return;

            VocalWarningSignal signal = new VocalWarningSignal
            {
                WarningHash = VocalWarningHashes.FromWarningId(normalizedWarningId),
                SourceId = eventId,
                Severity01 = math.saturate(intensity * warningVolume),
                CooldownSeconds = VwsRepeatCooldownSeconds,
                Priority = normalizedWarningId,
                Flags = warningFlags
            };
            SignalBus<VocalWarningSignal>.TryPushTracked(in signal, ref s_x001HectonSubmarineOSSignalPushDropCount);
            AudioCaptionEvents.TryRaiseHash(captionHashId, transform.position, VwsCaptionDurationSeconds, intensity);
        }

        private void PublishCurrentSnapshotIfChanged()
        {
            HectonSubmarineOsSnapshot nextSnapshot = new HectonSubmarineOsSnapshot(
                _subsystemStatus,
                _emergencyLevel,
                _powerNormalized,
                _oxygenNormalized,
                _carbonDioxideNormalized,
                _maxPressureKPa,
                _speedKnots,
                _engineHeat01,
                _engineHeatTrue01,
                _engineHeatMaskDelta01,
                _atlasTelemetryFlags,
                _sonarContactCount,
                _nearestSonarContactMeters,
                _vwsActiveFlags,
                _lowPowerModeActive,
                _lifeSupportCriticalActive,
                _stationKeepingStateCached,
                _subOsPowered);

            if (AreSnapshotsEqual(in _lastPublishedSnapshot, in nextSnapshot))
                return;

            if (!HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in nextSnapshot))
            {
                RecordSubOsEventPublishDrop(snapshotDrop: true);
                return;
            }

            _lastPublishedSnapshot = nextSnapshot;
        }

        private void PublishShutdownSnapshot()
        {
            HectonSubmarineOsSnapshot shutdownSnapshot = new HectonSubmarineOsSnapshot(
                _subsystemStatus,
                SubmarineEmergencyLevel.Nominal,
                _powerNormalized,
                _oxygenNormalized,
                _carbonDioxideNormalized,
                _maxPressureKPa,
                0f,
                0f,
                0f,
                0f,
                0u,
                0,
                0,
                SubmarineVwsFlags.None,
                false,
                false,
                false,
                false);
            if (!HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in shutdownSnapshot))
            {
                RecordSubOsEventPublishDrop(snapshotDrop: true);
                return;
            }

            _lastPublishedSnapshot = shutdownSnapshot;
        }

        private void PublishLog(HectonSubmarineOsLogCode code, byte priority)
        {
            HectonSubmarineOsLogRequest request = new HectonSubmarineOsLogRequest(code, priority);
            if (!HectonSubmarineOsEvents.TryRaiseLogRequested(in request))
                RecordSubOsEventPublishDrop(snapshotDrop: false);
        }

        private void RecordSubOsEventPublishDrop(bool snapshotDrop)
        {
            if (snapshotDrop)
                _droppedSubOsSnapshotPublishCount = SaturatingIncrement(_droppedSubOsSnapshotPublishCount);
            else
                _droppedSubOsLogPublishCount = SaturatingIncrement(_droppedSubOsLogPublishCount);

            if (!TryReserveTelemetryWarningFrame(
                    ref _lastSubOsEventDropTelemetryFrame,
                    SubOsEventDropTelemetryCooldownFrames))
                return;

            PublishPerformanceWarningBestEffort(
                snapshotDrop ? SubOsSnapshotDropWarningHash : SubOsLogDropWarningHash,
                SubOsEventDropContextHash,
                snapshotDrop ? _droppedSubOsSnapshotPublishCount : _droppedSubOsLogPublishCount);
        }

        private static bool TryReserveTelemetryWarningFrame(ref int lastTelemetryFrame, int cooldownFrames)
        {
            int frame = ResolveCurrentFrameIndexSafe();
            if (frame < 0)
            {
                if (lastTelemetryFrame == int.MinValue)
                    return false;

                lastTelemetryFrame = int.MinValue;
                return true;
            }

            if (lastTelemetryFrame >= 0 && frame - lastTelemetryFrame < cooldownFrames)
                return false;

            lastTelemetryFrame = frame;
            return true;
        }

        private static int ResolveCurrentFrameIndexSafe()
        {
            try
            {
                return SystemDispatcher.CurrentFrameIndex;
            }
            catch
            {
                return -1;
            }
        }

        private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (System.Exception telemetryException)
            {
                LogTelemetryWarningException(telemetryException);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTelemetryWarningException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                H8Debug.LogException(exception);
            }
            catch
            {
            }
#endif
        }

        private static int SaturatingIncrement(int value)
        {
            return value < int.MaxValue ? value + 1 : int.MaxValue;
        }

        private static float ResolveSupplyRatio(float totalGeneration, float totalConsumption)
        {
            return math.isfinite(totalConsumption) && totalConsumption > 0.0001f
                ? SaturateFinite(totalGeneration / totalConsumption, 1f)
                : 1f;
        }

        private static float SaturateFinite(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : fallback;
        }

        private static float NonNegativeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private SubmarineEmergencyLevel ResolveEmergencyLevel()
        {
            if (_fatalImplosionLatched || _oxygenNormalized <= EvacuateOxygenThreshold01)
                return SubmarineEmergencyLevel.Evacuate;

            if (_lifeSupportCriticalActive || _vitalWarningActive || _powerNormalized <= DangerPowerThreshold01 || _maxPressureKPa >= PressureDangerThresholdKPa)
                return SubmarineEmergencyLevel.Danger;

            if (_lowPowerModeActive || _pressureHighActive)
                return SubmarineEmergencyLevel.Caution;

            return SubmarineEmergencyLevel.Nominal;
        }

        private static HectonSubmarineOsLogCode ResolveEmergencyLevelLogCode(SubmarineEmergencyLevel emergencyLevel)
        {
            switch (emergencyLevel)
            {
                case SubmarineEmergencyLevel.Caution:
                    return HectonSubmarineOsLogCode.EmergencyLevelCaution;
                case SubmarineEmergencyLevel.Danger:
                    return HectonSubmarineOsLogCode.EmergencyLevelDanger;
                case SubmarineEmergencyLevel.Evacuate:
                    return HectonSubmarineOsLogCode.EmergencyLevelEvacuate;
                default:
                    return HectonSubmarineOsLogCode.EmergencyLevelNominal;
            }
        }

        private static bool AreSnapshotsEqual(in HectonSubmarineOsSnapshot a, in HectonSubmarineOsSnapshot b)
        {
            return a.SubsystemStatus == b.SubsystemStatus &&
                   a.EmergencyLevel == b.EmergencyLevel &&
                   math.abs(a.PowerNormalized - b.PowerNormalized) <= 0.0005f &&
                   math.abs(a.OxygenNormalized - b.OxygenNormalized) <= 0.0005f &&
                   math.abs(a.CarbonDioxideNormalized - b.CarbonDioxideNormalized) <= 0.0005f &&
                   math.abs(a.MaxPressureKPa - b.MaxPressureKPa) <= 0.5f &&
                   math.abs(a.SpeedKnots - b.SpeedKnots) <= 0.05f &&
                   math.abs(a.EngineHeat01 - b.EngineHeat01) <= 0.005f &&
                   math.abs(a.EngineHeatTrue01 - b.EngineHeatTrue01) <= 0.005f &&
                   math.abs(a.EngineHeatMaskDelta01 - b.EngineHeatMaskDelta01) <= 0.005f &&
                   a.AtlasTelemetryFlags == b.AtlasTelemetryFlags &&
                   a.SonarContactCount == b.SonarContactCount &&
                   a.NearestSonarContactMeters == b.NearestSonarContactMeters &&
                   a.VocalWarningFlags == b.VocalWarningFlags &&
                   a.StatusFlags == b.StatusFlags;
        }
    }
}
