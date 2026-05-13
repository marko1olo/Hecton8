using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Crafting;
using Hecton8.Power;
using Hecton8.UI;
using Hecton8.Visor;
using Hecton8.World;
using System.Collections.Generic;
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
        HostileDroneDetected = 15
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public readonly struct HectonSubmarineOsSnapshot
    {
        private const byte LowPowerModeFlag = 1 << 0;
        private const byte LifeSupportCriticalFlag = 1 << 1;
        private const byte StationKeepingFlag = 1 << 2;
        private const byte SubOsPoweredFlag = 1 << 3;

        public HectonSubmarineOsSnapshot(
            SubsystemStatus subsystemStatus,
            SubmarineEmergencyLevel emergencyLevel,
            float powerNormalized,
            float oxygenNormalized,
            float carbonDioxideNormalized,
            float maxPressureKPa,
            float speedKnots,
            float engineHeat01,
            int sonarContactCount,
            int nearestSonarContactMeters,
            SubmarineVwsFlags vocalWarningFlags,
            bool lowPowerModeActive,
            bool lifeSupportCriticalActive,
            bool stationKeepingActive,
            bool subOsPowered)
        {
            PowerNormalized = powerNormalized;
            OxygenNormalized = oxygenNormalized;
            CarbonDioxideNormalized = carbonDioxideNormalized;
            MaxPressureKPa = maxPressureKPa;
            SpeedKnots = speedKnots;
            EngineHeat01 = engineHeat01;
            SonarContactCount = sonarContactCount;
            NearestSonarContactMeters = nearestSonarContactMeters;
            VocalWarningFlags = vocalWarningFlags;
            SubsystemStatus = subsystemStatus;
            EmergencyLevel = emergencyLevel;
            _stateFlags = BuildStateFlags(lowPowerModeActive, lifeSupportCriticalActive, stationKeepingActive, subOsPowered);
        }

        public readonly float PowerNormalized;
        public readonly float OxygenNormalized;
        public readonly float CarbonDioxideNormalized;
        public readonly float MaxPressureKPa;
        public readonly float SpeedKnots;
        public readonly float EngineHeat01;
        public readonly int SonarContactCount;
        public readonly int NearestSonarContactMeters;
        public readonly SubmarineVwsFlags VocalWarningFlags;
        public readonly SubsystemStatus SubsystemStatus;
        public readonly SubmarineEmergencyLevel EmergencyLevel;
        private readonly byte _stateFlags;

        public bool LowPowerModeActive => (_stateFlags & LowPowerModeFlag) != 0;
        public bool LifeSupportCriticalActive => (_stateFlags & LifeSupportCriticalFlag) != 0;
        public bool StationKeepingActive => (_stateFlags & StationKeepingFlag) != 0;
        public bool SubOsPowered => (_stateFlags & SubOsPoweredFlag) != 0;

        private static byte BuildStateFlags(
            bool lowPowerModeActive,
            bool lifeSupportCriticalActive,
            bool stationKeepingActive,
            bool subOsPowered)
        {
            byte flags = 0;
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

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HectonSubmarineOsLogRequest
    {
        public HectonSubmarineOsLogRequest(HectonSubmarineOsLogCode code, byte priority)
        {
            Code = code;
            Priority = priority;
        }

        public readonly HectonSubmarineOsLogCode Code;
        public readonly byte Priority;
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
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct SubmarineOsEventPayload
    {
        public float PowerNormalized;
        public float OxygenNormalized;
        public float CarbonDioxideNormalized;
        public float MaxPressureKPa;
        public float SpeedKnots;
        public float EngineHeat01;
        public int SonarContactCount;
        public int NearestSonarContactMeters;
        public uint ModuleId;
        public uint StatusBits;
        public ushort EmergencyLevel;
        public ushort EventType;
        public ushort LogCode;
        public ushort Priority;
        public ushort VocalWarningFlags;
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

        // COLD ALLOC: RegistryBucket<ISubmarineOsEventListener>[16] - submarine OS deferred listeners - owner: HectonSubmarineOsEvents
        private static readonly RegistryBucket<ISubmarineOsEventListener> _listeners = new RegistryBucket<ISubmarineOsEventListener>(ListenerCapacity);
        private static NativeQueue<SubmarineOsEventPayload> _pendingEvents;
        private static NativeQueue<SubmarineOsEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(HectonSubmarineOsEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(HectonSubmarineOsEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
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
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a deferred submarine OS event listener.
        /// </summary>
        public static void Unregister(ISubmarineOsEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
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
                    break;

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

        public static void RaiseSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            uint statusBits = (uint)snapshot.SubsystemStatus;
            if (snapshot.LowPowerModeActive)
                statusBits |= LowPowerModeStatusBit;
            if (snapshot.LifeSupportCriticalActive)
                statusBits |= LifeSupportCriticalStatusBit;
            if (snapshot.StationKeepingActive)
                statusBits |= StationKeepingStatusBit;
            if (snapshot.SubOsPowered)
                statusBits |= SubOsPoweredStatusBit;

            Enqueue(new SubmarineOsEventPayload
            {
                PowerNormalized = snapshot.PowerNormalized,
                OxygenNormalized = snapshot.OxygenNormalized,
                CarbonDioxideNormalized = snapshot.CarbonDioxideNormalized,
                MaxPressureKPa = snapshot.MaxPressureKPa,
                SpeedKnots = snapshot.SpeedKnots,
                EngineHeat01 = snapshot.EngineHeat01,
                SonarContactCount = snapshot.SonarContactCount,
                NearestSonarContactMeters = snapshot.NearestSonarContactMeters,
                ModuleId = GlobalSubmarineOsModuleId,
                StatusBits = statusBits,
                EmergencyLevel = (ushort)snapshot.EmergencyLevel,
                EventType = (ushort)SubmarineOsEventType.SnapshotUpdated,
                LogCode = 0,
                Priority = 0,
                VocalWarningFlags = (ushort)snapshot.VocalWarningFlags
            });
        }

        public static void RaiseLogRequested(in HectonSubmarineOsLogRequest request)
        {
            Enqueue(new SubmarineOsEventPayload
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

        public static bool TryBuildSnapshot(in SubmarineOsEventPayload payload, out HectonSubmarineOsSnapshot snapshot)
        {
            snapshot = default;
            if ((SubmarineOsEventType)payload.EventType != SubmarineOsEventType.SnapshotUpdated)
                return false;

            snapshot = new HectonSubmarineOsSnapshot(
                (SubsystemStatus)(payload.StatusBits & 0xFFu),
                (SubmarineEmergencyLevel)payload.EmergencyLevel,
                payload.PowerNormalized,
                payload.OxygenNormalized,
                payload.CarbonDioxideNormalized,
                payload.MaxPressureKPa,
                payload.SpeedKnots,
                payload.EngineHeat01,
                payload.SonarContactCount,
                payload.NearestSonarContactMeters,
                (SubmarineVwsFlags)payload.VocalWarningFlags,
                (payload.StatusBits & LowPowerModeStatusBit) != 0u,
                (payload.StatusBits & LifeSupportCriticalStatusBit) != 0u,
                (payload.StatusBits & StationKeepingStatusBit) != 0u,
                (payload.StatusBits & SubOsPoweredStatusBit) != 0u);
            return true;
        }

        public static bool TryBuildLogRequest(in SubmarineOsEventPayload payload, out HectonSubmarineOsLogRequest request)
        {
            request = default;
            if ((SubmarineOsEventType)payload.EventType != SubmarineOsEventType.LogRequested)
                return false;

            request = new HectonSubmarineOsLogRequest(
                (HectonSubmarineOsLogCode)payload.LogCode,
                (byte)payload.Priority);
            return true;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<SubmarineOsEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SubmarineOsEventPayload>[16] — deferred submarine OS event lane — owner: HectonSubmarineOsEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(HectonSubmarineOsEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<SubmarineOsEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SubmarineOsEventPayload>[16] — next-frame submarine OS event lane prevents same-frame reentrant dispatch — owner: HectonSubmarineOsEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(HectonSubmarineOsEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
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

        private static void Enqueue(in SubmarineOsEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void DispatchRegisteredListeners(in SubmarineOsEventPayload payload)
        {
            int count = _listeners.Count;
            if (count <= 0)
                return;

            ISubmarineOsEventListener[] rawArray = _listeners.RawArray;
            _isDispatching = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    ISubmarineOsEventListener listener = rawArray[i];
                    if (listener != null)
                        listener.OnSubmarineOsEvent(in payload);
                }
            }
            finally
            {
                _isDispatching = false;
            }
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
                    break;

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
    public sealed class HectonSubmarineOS : MonoBehaviour, IUpdatable, ISlowTickable, IRenderable, IPowerGridTelemetryListener, IHighPressureEventListener, IFatalPressureImplosionEventListener, IDroneFleetSnapshotEventListener, ISonarPingEventListener, ISonarSnapshotEventListener
    {
        private const float DefaultReferencePressureKPa = 101.325f;
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
        private const float LowTierSonarRefreshIntervalSeconds = 0.1f;
        private const float MidTierSonarRefreshIntervalSeconds = 0.06666667f;
        private const float HighTierSonarRefreshIntervalSeconds = 0.03333334f;
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
        private const float BrownoutLightIntensityScale = 0.15f;
        private const float BrownoutBlinkFrequency = 8f;
        private const int BrownoutLightBindingCapacity = 256;
        private const int BrownoutMaterialBindingCapacity = 384;
        private const int BrownoutLightResolveCapacity = 32;
        private const int BrownoutRendererResolveCapacity = 48;
        private const int BrownoutSharedMaterialResolveCapacity = 8;
        private const int BrownoutVisualMutationBudgetPerRender = 64;
        private const byte LogPriorityNormal = 1;
        private const byte LogPriorityWarning = 2;
        private const byte LogPriorityCritical = 3;
        private const string LowPowerCaptionText = "SUBMARINE LOW POWER";
        private const string LifeSupportCaptionText = "LIFE SUPPORT CRITICAL";
        private const string MultiFailureCaptionText = "MULTIPLE SYSTEM FAILURES";
        private const string EmergencyDangerCaptionText = "EMERGENCY LEVEL DANGER";
        private const string AbandonShipCaptionText = "ABANDON SHIP";
        private const string HostileDroneCaptionText = "HOSTILE DRONE DETECTED";
        private const string OxygenLowCaptionText = "OXYGEN LOW";
        private const string OxygenCriticalCaptionText = "OXYGEN CRITICAL";
        private const string HullBreachCaptionText = "HULL BREACH";
        private const string PressureHighCaptionText = "HULL PRESSURE HIGH";
        private const string ThermalStressCaptionText = "THERMAL STRESS";
        private static readonly int _EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int _HectonBrownoutPulseId = Shader.PropertyToID("_HectonBrownoutPulse");
        private static readonly int _HectonSubOsLightingStateId = Shader.PropertyToID("_HectonSubOsLightingState");
        private static readonly int _SubInteriorLightingStateId = Shader.PropertyToID("_SubInteriorLightingState");
        private static readonly int _HectonSubOsSonarSweepId = Shader.PropertyToID("_HectonSubOsSonarSweep");
        private static readonly int _HectonSubOsSonarLodId = Shader.PropertyToID("_HectonSubOsSonarLod");
        private static readonly int _HectonSubOsNavigationId = Shader.PropertyToID("_HectonSubOsNavigation");
        private static readonly int _HectonSubOsEngineDiagnosticsId = Shader.PropertyToID("_HectonSubOsEngineDiagnostics");
        private static readonly Color BrownoutEmissiveColor = new Color(1f, 0.12f, 0.08f, 1f);

        private struct BrownoutLightBinding
        {
            public Light Light;
            public float BaseIntensity;
            public Color BaseColor;
        }

        private struct BrownoutMaterialBinding
        {
            public Material Material;
            public Color BaseEmissionColor;
        }

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

        [Tooltip("Optional VWS clip for hull breach. Falls back to multi-system warning when unset.")]
        [SerializeField] private AudioClip hullBreachWarningClip;

        [Tooltip("Optional VWS clip for hull pressure/thermal stress. Falls back to multi-system warning when unset.")]
        [SerializeField] private AudioClip hullStressWarningClip;

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
        private SubmarineAtmosphereSystem _atmosphereSystem;
        private SubmarineStationKeepingController _stationKeepingController;
        // COLD ALLOC: BrownoutLightBinding[256] - fixed brownout point-light cache, no runtime ToArray - owner: HectonSubmarineOS
        private readonly BrownoutLightBinding[] _brownoutLights = new BrownoutLightBinding[BrownoutLightBindingCapacity];
        // COLD ALLOC: BrownoutMaterialBinding[384] - fixed brownout emissive-material cache, no runtime ToArray - owner: HectonSubmarineOS
        private readonly BrownoutMaterialBinding[] _brownoutMaterials = new BrownoutMaterialBinding[BrownoutMaterialBindingCapacity];
        // COLD ALLOC: List<Light>[32] - reusable module light resolve buffer for brownout cache rebuild - owner: HectonSubmarineOS
        private readonly List<Light> _brownoutLightResolveBuffer = new List<Light>(BrownoutLightResolveCapacity);
        // COLD ALLOC: List<Renderer>[48] - reusable module renderer resolve buffer for brownout cache rebuild - owner: HectonSubmarineOS
        private readonly List<Renderer> _brownoutRendererResolveBuffer = new List<Renderer>(BrownoutRendererResolveCapacity);
        // COLD ALLOC: List<Material>[8] - reusable renderer shared-material resolve buffer for brownout cache rebuild - owner: HectonSubmarineOS
        private readonly List<Material> _brownoutSharedMaterialResolveBuffer = new List<Material>(BrownoutSharedMaterialResolveCapacity);
        private int _brownoutLightCount;
        private int _brownoutMaterialCount;
        private int _brownoutLightApplyCursor;
        private int _brownoutMaterialApplyCursor;
        private int _brownoutLightRestoreCursor;
        private int _brownoutMaterialRestoreCursor;
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
        private bool _subOsPowered = true;
        private bool _brownoutCachesBuilt;
        private bool _brownoutVisualStateApplied;
        private bool _brownoutRestorePending;
        private bool _registeredUpdatable;
        private bool _registeredRenderable;
        private bool _registeredSlowTick;
        private bool _runtimeLifecycleStarted;
        private bool _stationKeepingStateCached;
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
                    submarineRoot.gameObject.AddComponent<HectonSubmarineOS>(); // COLD ALLOC: HectonSubmarineOS[1] — submarine-wide diagnostic owner — owner: HectonSubmarineOS

                if (!submarineRoot.TryGetComponent(out SubmarineStationKeepingController _))
                    submarineRoot.gameObject.AddComponent<SubmarineStationKeepingController>(); // COLD ALLOC: SubmarineStationKeepingController[1] - cinematic station-keeping owner - owner: HectonSubmarineOS
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
            CacheReferences();
        }

        private void OnEnable()
        {
            TryStartRuntimeLifecycle();
        }

        private void Start()
        {
            TryStartRuntimeLifecycle();
        }

        private void OnDisable()
        {
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
            TryUnregister();
            RestoreBrownoutVisualsImmediate();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!CanUseRuntimeDispatcher() || !_subOsPowered)
                return;

            CacheReferences();
            float safeDeltaTime = math.max(0f, deltaTime);
            _navigationRefreshAccumulator += safeDeltaTime;
            _diagnosticsRefreshAccumulator += safeDeltaTime;

            bool publishSnapshot = false;
            if (_navigationRefreshAccumulator >= ResolveSonarRefreshIntervalSeconds(GlobalRegistry.ScalabilityTier))
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

            CacheReferences();
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
            if (!CanUseRuntimeDispatcher() || !_subOsPowered)
                return;

            float safeDeltaTime = math.max(0f, deltaTime);
            RefreshSonarSweepGlobal(safeDeltaTime);
            ApplySonarLodShaderGlobal();
            ApplyLightingStateGlobal(safeDeltaTime);

            if (!_cascadingBrownoutActive || _lowPowerModeActive)
            {
                Shader.SetGlobalFloat(_HectonBrownoutPulseId, 0f);
                if (_brownoutVisualStateApplied || _brownoutRestorePending)
                    RestoreBrownoutVisualsBudgeted();

                return;
            }

            if (!_brownoutCachesBuilt)
                return;

            _brownoutPulsePhase = math.frac(_brownoutPulsePhase + math.max(0f, deltaTime) * BrownoutBlinkFrequency);
            float pulse = 1f - math.abs((_brownoutPulsePhase * 2f) - 1f);
            Shader.SetGlobalFloat(_HectonBrownoutPulseId, pulse);
            ApplyBrownoutVisualsBudgeted();
        }

        private void TryStartRuntimeLifecycle()
        {
            if (_runtimeLifecycleStarted || !CanUseRuntimeDispatcher())
                return;

            CacheReferences();
            RebuildBrownoutCaches();
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

        private void CacheReferences()
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
        }

        private void Subscribe()
        {
            PowerGridTelemetryEvents.Register(this);
            HighPressureEvents.Register(this);
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
                HostileDroneCaptionText,
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
                _registeredUpdatable = _subOsPowered && GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredRenderable)
            {
                _registeredRenderable = _subOsPowered && GlobalRegistry.Renderables.TryRegister(this);
            }
        }

        private void TryRegisterActiveLoops()
        {
            if (!CanUseRuntimeDispatcher() || !_subOsPowered)
                return;

            if (!_registeredUpdatable)
                _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredRenderable)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void TryUnregisterActiveLoops()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }
        }

        private static bool CanUseRuntimeDispatcher()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
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
                TryUnregisterActiveLoops();
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

            TryRegisterActiveLoops();
            PublishLog(HectonSubmarineOsLogCode.ReactorStable, LogPriorityNormal);
            RefreshNavigationTelemetry();
            RefreshEngineDiagnosticsTelemetry(DiagnosticsRefreshIntervalSeconds);
            EvaluateStateMachine(true);
            HectonSubmarineOsDisplay.EnsureRuntimeInstance();
        }

        private void ResetSubOsShaderGlobals()
        {
            Shader.SetGlobalFloat(_HectonBrownoutPulseId, 0f);
            Shader.SetGlobalVector(_HectonSubOsLightingStateId, Vector4.zero);
            Shader.SetGlobalVector(_SubInteriorLightingStateId, Vector4.zero);
            Shader.SetGlobalVector(_HectonSubOsSonarSweepId, Vector4.zero);
            Shader.SetGlobalVector(_HectonSubOsNavigationId, Vector4.zero);
            Shader.SetGlobalVector(_HectonSubOsEngineDiagnosticsId, Vector4.zero);
        }

        private void RefreshTelemetryFromServices()
        {
            IPowerGridService powerGridService = GlobalRegistry.PowerGrid;
            if (powerGridService != null)
            {
                BatteryRuntimeSnapshot batterySnapshot = powerGridService.BatterySnapshot;
                _powerSupplyRatio = ResolveSupplyRatio(powerGridService.TotalGeneration, powerGridService.TotalConsumption);
                _powerNormalized = batterySnapshot.TotalCapacityWattSeconds > 0.0001f
                    ? math.saturate(batterySnapshot.ChargeNormalized)
                    : _powerSupplyRatio;
            }

            RefreshAtmosphereTelemetry();
            RefreshSubsystemStatus();
        }

        private void RefreshAtmosphereTelemetry()
        {
            SubmarineAtmosphereSystem atmosphereSystem = _atmosphereSystem;
            if (atmosphereSystem == null)
            {
                _oxygenNormalized = 1f;
                _maxPressureKPa = DefaultReferencePressureKPa;
                return;
            }

            int roomCount = atmosphereSystem.RoomCount;
            if (roomCount <= 0)
            {
                _oxygenNormalized = 1f;
                _maxPressureKPa = DefaultReferencePressureKPa;
                return;
            }

            float minOxygenFraction = 1f;
            float maxCarbonDioxideFraction = 0f;
            float maxPressureKPa = 0f;
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                minOxygenFraction = math.min(minOxygenFraction, atmosphereSystem.GetRoomOxygenFraction(roomIndex));
                maxCarbonDioxideFraction = math.max(maxCarbonDioxideFraction, atmosphereSystem.GetRoomCarbonDioxidePressureFraction(roomIndex));
                maxPressureKPa = math.max(maxPressureKPa, atmosphereSystem.GetRoomPressureKPa(roomIndex));
            }

            _oxygenNormalized = math.saturate(minOxygenFraction);
            _carbonDioxideNormalized = math.saturate(maxCarbonDioxideFraction);
            _maxPressureKPa = math.max(DefaultReferencePressureKPa, maxPressureKPa);
        }

        private void RefreshSubsystemStatus()
        {
            SubsystemStatus subsystemStatus = SubsystemStatus.None;
            if (_submarineCore != null && _submarineCore.HullRigidbody != null && _submarineCore.IsTransportPlatformActive)
                subsystemStatus |= SubsystemStatus.Engines;

            if (_atmosphereSystem != null && !_lifeSupportCriticalActive)
                subsystemStatus |= SubsystemStatus.LifeSupport;

            if (!_lowPowerModeActive)
                subsystemStatus |= SubsystemStatus.Lights;

            SpectrumSystem spectrumSystem = GlobalRegistry.Spectrum;
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
            _engineHeat01 = QuantizeHeat01(targetHeat01);
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
            return math.floor(math.saturate(value) * EngineHeatQuantizeScale + 0.5f) * EngineHeatQuantizeInv;
        }

        private void RefreshSonarDerivedTelemetry()
        {
            SpatialSonarSnapshot snapshot = _lastSonarSnapshot;
            _sonarContactCount = math.max(0, snapshot.ResourceCount) +
                                 math.max(0, snapshot.BioformCount) +
                                 math.max(0, snapshot.SignalCount);

            int nearest = int.MaxValue;
            if (snapshot.HasNearestResource)
                nearest = math.min(nearest, math.max(0, snapshot.NearestResourceDistanceMeters));
            if (snapshot.HasNearestBioform)
                nearest = math.min(nearest, math.max(0, snapshot.NearestBioformDistanceMeters));
            if (snapshot.HasNearestSignal)
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

        private static float ResolveSonarRefreshIntervalSeconds(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return HighTierSonarRefreshIntervalSeconds;
                case HectonQualityTier.Mid:
                    return MidTierSonarRefreshIntervalSeconds;
                default:
                    return LowTierSonarRefreshIntervalSeconds;
            }
        }

        private static bool ResolveSonarInterpolationEnabled(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        private static void ApplySonarLodShaderGlobal()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            float refreshInterval = ResolveSonarRefreshIntervalSeconds(tier);
            float interpolationEnabled = ResolveSonarInterpolationEnabled(tier) ? 1f : 0f;
            Shader.SetGlobalVector(
                _HectonSubOsSonarLodId,
                new Vector4(refreshInterval, math.rcp(math.max(0.0001f, refreshInterval)), interpolationEnabled, (float)tier));
        }

        private void ApplyNavigationShaderGlobal()
        {
            Shader.SetGlobalVector(
                _HectonSubOsNavigationId,
                new Vector4(_speedKnots, _sonarContactCount, _nearestSonarContactMeters, _subOsPowered ? 1f : 0f));
        }

        private void ApplyEngineDiagnosticsShaderGlobal()
        {
            Shader.SetGlobalVector(
                _HectonSubOsEngineDiagnosticsId,
                new Vector4(_engineHeat01, _powerSupplyRatio, _powerNormalized, _subOsPowered ? 1f : 0f));
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
            if (module == null || !module.TryGetComponent(out PowerNode powerNode) || powerNode.Grid == null)
                return false;

            PowerGrid grid = powerNode.Grid;
            return grid.BrownoutTier != LogisticsBrownoutTier.None || grid.IsBatteryEmergencyReserveActive;
        }

        /// <summary>
        /// Receives deferred aggregate power telemetry snapshots.
        /// </summary>
        /// <param name="snapshot">Aggregate power telemetry snapshot.</param>
        public void OnPowerGridTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot)
        {
            _powerNormalized = math.saturate(snapshot.AvailablePowerNormalized);
            _powerSupplyRatio = math.saturate(snapshot.SupplyRatio);
            _highestBrownoutTier = snapshot.HighestBrownoutTier;
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
            _maxPressureKPa = math.max(_maxPressureKPa, math.max(pressureEvent.PressureAKPa, pressureEvent.PressureBKPa));
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
            double now = Time.unscaledTimeAsDouble;
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
            if (survivalSystem != null && survivalSystem.ThermalStressSeverity01 >= ThermalStressVwsThreshold01)
                flags |= SubmarineVwsFlags.ThermalStress;

            return flags;
        }

        private float ResolveVwsOxygenNormalized()
        {
            float oxygen01 = _oxygenNormalized;
            HectonSurvivalSystem survivalSystem = ResolvePlayerSurvivalSystem();
            if (survivalSystem != null)
                oxygen01 = math.min(oxygen01, math.saturate(survivalSystem.OxygenNormalized));

            return math.saturate(oxygen01);
        }

        private HectonSurvivalSystem ResolvePlayerSurvivalSystem()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            return playerContext != null ? playerContext.SurvivalSystem : null;
        }

        private bool ResolvePlayerVitalWarningActive()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerHealth playerHealth = playerContext != null ? playerContext.PlayerHealth : null;
            if (playerHealth == null)
                return false;

            float health01 = math.saturate(playerHealth.HealthPercent);
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
                        LowPowerCaptionText,
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
                        OxygenLowCaptionText,
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
                        OxygenCriticalCaptionText,
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
                        HullBreachCaptionText,
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
                        PressureHighCaptionText,
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
                        AbandonShipCaptionText,
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
                        ThermalStressCaptionText,
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
                        MultiFailureCaptionText,
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
            string captionText,
            float intensity,
            byte warningId,
            byte warningFlags,
            ref double nextAllowedTime,
            double now)
        {
            bool rising = (risingFlags & flag) != 0;
            if (!rising && now < nextAllowedTime)
                return;

            QueueVoiceAlarm(eventId, captionText, intensity, warningId, warningFlags);
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
                        AbandonShipCaptionText,
                        1f,
                        (byte)VocalWarningId.CrushDepth,
                        VocalWarningSignalFlags.HabitatIntegrityCompromised);
                    break;

                case SubmarineEmergencyLevel.Danger:
                    QueueVoiceAlarm(
                        multiSystemFailureEventId != 0u ? multiSystemFailureEventId : lifeSupportCriticalEventId,
                        EmergencyDangerCaptionText,
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
                BeginBrownoutRestore();
            else
                ResetBrownoutVisualMutationCursors();
        }

        private bool ResolveCascadingBrownoutActive()
        {
            if (_powerSupplyRatio >= CascadingBrownoutThreshold01)
                return false;

            return _highestBrownoutTier >= LogisticsBrownoutTier.EssentialOnly || _powerNormalized < CascadingBrownoutThreshold01;
        }

        private void RebuildBrownoutCaches()
        {
            System.Array.Clear(_brownoutLights, 0, _brownoutLights.Length);
            System.Array.Clear(_brownoutMaterials, 0, _brownoutMaterials.Length);
            _brownoutLightCount = 0;
            _brownoutMaterialCount = 0;
            ResetBrownoutVisualMutationCursors();
            _brownoutLightResolveBuffer.Clear();
            _brownoutRendererResolveBuffer.Clear();
            _brownoutSharedMaterialResolveBuffer.Clear();

            int moduleCount = BaseModule.ActiveModuleCount;
            if (moduleCount <= 0)
            {
                _brownoutCachesBuilt = true;
                return;
            }

            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(moduleIndex);
                if (module == null)
                    continue;

                _brownoutLightResolveBuffer.Clear();
                module.GetComponentsInChildren(true, _brownoutLightResolveBuffer);
                for (int lightIndex = 0; lightIndex < _brownoutLightResolveBuffer.Count; lightIndex++)
                {
                    Light light = _brownoutLightResolveBuffer[lightIndex];
                    if (light == null || light.type != LightType.Point)
                        continue;

                    AddBrownoutLightBinding(new BrownoutLightBinding
                    {
                        Light = light,
                        BaseIntensity = light.intensity,
                        BaseColor = light.color
                    });
                }

                _brownoutRendererResolveBuffer.Clear();
                module.GetComponentsInChildren(true, _brownoutRendererResolveBuffer);
                for (int rendererIndex = 0; rendererIndex < _brownoutRendererResolveBuffer.Count; rendererIndex++)
                {
                    Renderer renderer = _brownoutRendererResolveBuffer[rendererIndex];
                    if (renderer == null)
                        continue;

                    _brownoutSharedMaterialResolveBuffer.Clear();
                    renderer.GetSharedMaterials(_brownoutSharedMaterialResolveBuffer);
                    for (int materialIndex = 0; materialIndex < _brownoutSharedMaterialResolveBuffer.Count; materialIndex++)
                    {
                        Material material = _brownoutSharedMaterialResolveBuffer[materialIndex];
                        if (material == null || !material.HasProperty(_EmissionColorId) || ContainsMaterial(material))
                            continue;

                        AddBrownoutMaterialBinding(new BrownoutMaterialBinding
                        {
                            Material = material,
                            BaseEmissionColor = material.GetColor(_EmissionColorId)
                        });
                    }
                }
            }

            _brownoutLightResolveBuffer.Clear();
            _brownoutRendererResolveBuffer.Clear();
            _brownoutSharedMaterialResolveBuffer.Clear();
            _brownoutCachesBuilt = true;
        }

        private void AddBrownoutLightBinding(BrownoutLightBinding binding)
        {
            if (_brownoutLightCount >= _brownoutLights.Length)
                return;

            _brownoutLights[_brownoutLightCount++] = binding;
        }

        private void AddBrownoutMaterialBinding(BrownoutMaterialBinding binding)
        {
            if (_brownoutMaterialCount >= _brownoutMaterials.Length)
                return;

            _brownoutMaterials[_brownoutMaterialCount++] = binding;
        }

        private bool ContainsMaterial(Material material)
        {
            for (int i = 0; i < _brownoutMaterialCount; i++)
            {
                if (ReferenceEquals(_brownoutMaterials[i].Material, material))
                    return true;
            }

            return false;
        }

        private void ResetBrownoutVisualMutationCursors()
        {
            _brownoutLightApplyCursor = 0;
            _brownoutMaterialApplyCursor = 0;
            _brownoutLightRestoreCursor = 0;
            _brownoutMaterialRestoreCursor = 0;
            _brownoutRestorePending = false;
        }

        private void BeginBrownoutRestore()
        {
            _brownoutLightRestoreCursor = 0;
            _brownoutMaterialRestoreCursor = 0;
            _brownoutRestorePending =
                _brownoutVisualStateApplied ||
                _brownoutLightApplyCursor > 0 ||
                _brownoutMaterialApplyCursor > 0;
        }

        private void ApplyBrownoutVisualsBudgeted()
        {
            int budget = BrownoutVisualMutationBudgetPerRender;
            while (budget > 0 && _brownoutLightApplyCursor < _brownoutLightCount)
            {
                BrownoutLightBinding binding = _brownoutLights[_brownoutLightApplyCursor++];
                budget--;
                if (binding.Light == null)
                    continue;

                binding.Light.intensity = binding.BaseIntensity * BrownoutLightIntensityScale;
                binding.Light.color = binding.BaseColor;
            }

            while (budget > 0 && _brownoutMaterialApplyCursor < _brownoutMaterialCount)
            {
                BrownoutMaterialBinding binding = _brownoutMaterials[_brownoutMaterialApplyCursor++];
                budget--;
                if (binding.Material == null)
                    continue;

                binding.Material.SetColor(_EmissionColorId, BrownoutEmissiveColor);
            }

            _brownoutVisualStateApplied =
                _brownoutLightApplyCursor >= _brownoutLightCount &&
                _brownoutMaterialApplyCursor >= _brownoutMaterialCount;
        }

        private void RestoreBrownoutVisualsBudgeted()
        {
            if (!_brownoutRestorePending && !_brownoutVisualStateApplied)
                return;

            int budget = BrownoutVisualMutationBudgetPerRender;
            while (budget > 0 && _brownoutLightRestoreCursor < _brownoutLightCount)
            {
                BrownoutLightBinding binding = _brownoutLights[_brownoutLightRestoreCursor++];
                budget--;
                if (binding.Light == null)
                    continue;

                binding.Light.intensity = binding.BaseIntensity;
                binding.Light.color = binding.BaseColor;
            }

            while (budget > 0 && _brownoutMaterialRestoreCursor < _brownoutMaterialCount)
            {
                BrownoutMaterialBinding binding = _brownoutMaterials[_brownoutMaterialRestoreCursor++];
                budget--;
                if (binding.Material == null)
                    continue;

                binding.Material.SetColor(_EmissionColorId, binding.BaseEmissionColor);
            }

            if (_brownoutLightRestoreCursor < _brownoutLightCount ||
                _brownoutMaterialRestoreCursor < _brownoutMaterialCount)
            {
                return;
            }

            _brownoutVisualStateApplied = false;
            _brownoutRestorePending = false;
            _brownoutLightApplyCursor = 0;
            _brownoutMaterialApplyCursor = 0;
        }

        private void RestoreBrownoutVisualsImmediate()
        {
            Shader.SetGlobalFloat(_HectonBrownoutPulseId, 0f);

            for (int i = 0; i < _brownoutLightCount; i++)
            {
                BrownoutLightBinding binding = _brownoutLights[i];
                if (binding.Light == null)
                    continue;

                binding.Light.intensity = binding.BaseIntensity;
                binding.Light.color = binding.BaseColor;
            }

            for (int i = 0; i < _brownoutMaterialCount; i++)
            {
                BrownoutMaterialBinding binding = _brownoutMaterials[i];
                if (binding.Material == null)
                    continue;

                binding.Material.SetColor(_EmissionColorId, binding.BaseEmissionColor);
            }

            _brownoutVisualStateApplied = false;
            ResetBrownoutVisualMutationCursors();
        }

        private void QueueVoiceAlarm(uint eventId, string captionText, float intensity, byte warningId, byte warningFlags)
        {
            byte normalizedWarningId = warningId >= (byte)VocalWarningId.CrushDepth && warningId <= (byte)VocalWarningId.PowerLow
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
            GlobalSignals.Publish(in signal);
            _ = captionText;
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
                _sonarContactCount,
                _nearestSonarContactMeters,
                _vwsActiveFlags,
                _lowPowerModeActive,
                _lifeSupportCriticalActive,
                _stationKeepingStateCached,
                _subOsPowered);

            if (AreSnapshotsEqual(in _lastPublishedSnapshot, in nextSnapshot))
                return;

            _lastPublishedSnapshot = nextSnapshot;
            HectonSubmarineOsEvents.RaiseSnapshotUpdated(in nextSnapshot);
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
                0,
                0,
                SubmarineVwsFlags.None,
                false,
                false,
                false,
                false);
            _lastPublishedSnapshot = shutdownSnapshot;
            HectonSubmarineOsEvents.RaiseSnapshotUpdated(in shutdownSnapshot);
        }

        private void PublishLog(HectonSubmarineOsLogCode code, byte priority)
        {
            HectonSubmarineOsLogRequest request = new HectonSubmarineOsLogRequest(code, priority);
            HectonSubmarineOsEvents.RaiseLogRequested(in request);
        }

        private static float ResolveSupplyRatio(float totalGeneration, float totalConsumption)
        {
            return totalConsumption > 0.0001f
                ? math.saturate(totalGeneration / totalConsumption)
                : 1f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
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
                   a.SonarContactCount == b.SonarContactCount &&
                   a.NearestSonarContactMeters == b.NearestSonarContactMeters &&
                   a.VocalWarningFlags == b.VocalWarningFlags &&
                   a.LowPowerModeActive == b.LowPowerModeActive &&
                   a.LifeSupportCriticalActive == b.LifeSupportCriticalActive &&
                   a.StationKeepingActive == b.StationKeepingActive &&
                   a.SubOsPowered == b.SubOsPowered;
        }
    }
}
