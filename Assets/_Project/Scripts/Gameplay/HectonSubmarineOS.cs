using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Power;
using Hecton8.UI;
using Hecton8.Visor;
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
        StationKeepingReleased = 14
    }

    public readonly struct HectonSubmarineOsSnapshot
    {
        public HectonSubmarineOsSnapshot(
            SubsystemStatus subsystemStatus,
            SubmarineEmergencyLevel emergencyLevel,
            float powerNormalized,
            float oxygenNormalized,
            float maxPressureKPa,
            bool lowPowerModeActive,
            bool lifeSupportCriticalActive,
            bool stationKeepingActive)
        {
            SubsystemStatus = subsystemStatus;
            EmergencyLevel = emergencyLevel;
            PowerNormalized = powerNormalized;
            OxygenNormalized = oxygenNormalized;
            MaxPressureKPa = maxPressureKPa;
            LowPowerModeActive = lowPowerModeActive;
            LifeSupportCriticalActive = lifeSupportCriticalActive;
            StationKeepingActive = stationKeepingActive;
        }

        public SubsystemStatus SubsystemStatus { get; }
        public SubmarineEmergencyLevel EmergencyLevel { get; }
        public float PowerNormalized { get; }
        public float OxygenNormalized { get; }
        public float MaxPressureKPa { get; }
        public bool LowPowerModeActive { get; }
        public bool LifeSupportCriticalActive { get; }
        public bool StationKeepingActive { get; }
    }

    public readonly struct HectonSubmarineOsLogRequest
    {
        public HectonSubmarineOsLogRequest(HectonSubmarineOsLogCode code, byte priority)
        {
            Code = code;
            Priority = priority;
        }

        public HectonSubmarineOsLogCode Code { get; }
        public byte Priority { get; }
    }

    /// <summary>
    /// Static event bus for submarine OS telemetry and log requests.
    /// </summary>
    public static class HectonSubmarineOsEvents
    {
        public delegate void SnapshotUpdatedHandler(in HectonSubmarineOsSnapshot snapshot);
        public delegate void LogRequestedHandler(in HectonSubmarineOsLogRequest request);

        public static event SnapshotUpdatedHandler OnSnapshotUpdated;
        public static event LogRequestedHandler OnLogRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnSnapshotUpdated = null;
            OnLogRequested = null;
        }

        public static void RaiseSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            OnSnapshotUpdated?.Invoke(snapshot);
        }

        public static void RaiseLogRequested(in HectonSubmarineOsLogRequest request)
        {
            OnLogRequested?.Invoke(request);
        }
    }

    /// <summary>
    /// Central submarine diagnostic owner that monitors power, atmosphere, and emergency state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineCoreDirector))]
    [AddComponentMenu("Hecton8/Gameplay/Submarine/Hecton Submarine OS")]
    public sealed class HectonSubmarineOS : MonoBehaviour, IUpdatable, ISlowTickable
    {
        private const float DefaultReferencePressureKPa = 101.325f;
        private const float LowPowerThreshold01 = 0.20f;
        private const float LowPowerReleaseThreshold01 = 0.24f;
        private const float DangerPowerThreshold01 = 0.10f;
        private const float LifeSupportCriticalThreshold01 = 0.10f;
        private const float LifeSupportReleaseThreshold01 = 0.12f;
        private const float EvacuateOxygenThreshold01 = 0.05f;
        private const float PressureHighThresholdKPa = 150f;
        private const float PressureDangerThresholdKPa = 220f;
        private const float PressureReleaseThresholdKPa = 140f;
        private const byte LogPriorityNormal = 1;
        private const byte LogPriorityWarning = 2;
        private const byte LogPriorityCritical = 3;
        private static readonly char[] s_lowPowerCaption = "SUBMARINE LOW POWER".ToCharArray();
        private static readonly char[] s_lifeSupportCaption = "LIFE SUPPORT CRITICAL".ToCharArray();
        private static readonly char[] s_multiFailureCaption = "MULTIPLE SYSTEM FAILURES".ToCharArray();
        private static readonly char[] s_emergencyDangerCaption = "EMERGENCY LEVEL DANGER".ToCharArray();
        private static readonly char[] s_emergencyEvacuateCaption = "EMERGENCY LEVEL EVACUATE".ToCharArray();

        [Header("Audio")]
        [Tooltip("Optional helmet warning for low-power transition events.")]
        [SerializeField] private AudioClip lowPowerWarningClip;

        [Tooltip("Optional helmet warning loop/one-shot for life-support critical state.")]
        [SerializeField] private AudioClip lifeSupportCriticalClip;

        [Tooltip("Optional helmet warning for simultaneous multi-system failures.")]
        [SerializeField] private AudioClip multiSystemFailureClip;

        [Tooltip("UI mixer volume for diegetic submarine OS warnings.")]
        [SerializeField, Range(0f, 1f)] private float warningVolume = 0.55f;

        private SubmarineCoreDirector _submarineCore;
        private SubmarineAtmosphereSystem _atmosphereSystem;
        private SubmarineStationKeepingController _stationKeepingController;
        private HectonSubmarineOsSnapshot _lastPublishedSnapshot;
        private SubsystemStatus _subsystemStatus;
        private SubmarineEmergencyLevel _emergencyLevel;
        private float _powerNormalized = 1f;
        private float _oxygenNormalized = 1f;
        private float _maxPressureKPa = DefaultReferencePressureKPa;
        private bool _lowPowerModeActive;
        private bool _lifeSupportCriticalActive;
        private bool _pressureHighActive;
        private bool _fatalImplosionLatched;
        private bool _multiSystemFailureLatched;
        private bool _registeredUpdatable;
        private bool _registeredSlowTick;
        private bool _stationKeepingStateCached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstalled()
        {
            SubmarineCoreDirector[] submarineRoots = Object.FindObjectsByType<SubmarineCoreDirector>(
                FindObjectsInactive.Exclude);
            if (submarineRoots == null)
                return;

            for (int i = 0; i < submarineRoots.Length; i++)
            {
                SubmarineCoreDirector submarineRoot = submarineRoots[i];
                if (submarineRoot == null)
                    continue;

                if (!submarineRoot.TryGetComponent(out HectonSubmarineOS _))
                    submarineRoot.gameObject.AddComponent<HectonSubmarineOS>(); // COLD ALLOC: HectonSubmarineOS[1] — submarine-wide diagnostic owner — owner: HectonSubmarineOS

                if (!submarineRoot.TryGetComponent(out SubmarineStationKeepingController _))
                    submarineRoot.gameObject.AddComponent<SubmarineStationKeepingController>(); // COLD ALLOC: SubmarineStationKeepingController[1] — station-keeping PID owner — owner: HectonSubmarineOS
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

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            Subscribe();
            TryRegister();
            PublishLog(HectonSubmarineOsLogCode.ReactorStable, LogPriorityNormal);
            RefreshTelemetryFromServices();
            EvaluateStateMachine(true);
            HectonSubmarineOsDisplay.EnsureRuntimeInstance();
        }

        private void OnDisable()
        {
            Unsubscribe();
            TryUnregister();
            SetLowPowerMode(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            TryUnregister();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            CacheReferences();
            HectonSubmarineOsDisplay.EnsureRuntimeInstance();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            CacheReferences();
            RefreshTelemetryFromServices();
            EvaluateStateMachine(false);
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
            PowerGridTelemetryEvents.OnTelemetryUpdated -= HandlePowerTelemetryUpdated;
            PowerGridTelemetryEvents.OnTelemetryUpdated += HandlePowerTelemetryUpdated;
            HighPressureEvents.OnHighPressure -= HandleHighPressure;
            HighPressureEvents.OnHighPressure += HandleHighPressure;
            FatalPressureImplosionEvents.OnFatalPressureImplosion -= HandleFatalPressureImplosion;
            FatalPressureImplosionEvents.OnFatalPressureImplosion += HandleFatalPressureImplosion;
        }

        private void Unsubscribe()
        {
            PowerGridTelemetryEvents.OnTelemetryUpdated -= HandlePowerTelemetryUpdated;
            HighPressureEvents.OnHighPressure -= HandleHighPressure;
            FatalPressureImplosionEvents.OnFatalPressureImplosion -= HandleFatalPressureImplosion;
        }

        private void TryRegister()
        {
            if (!_registeredUpdatable)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = true;
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = true;
            }
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
        }

        private void RefreshTelemetryFromServices()
        {
            IPowerGridService powerGridService = GlobalRegistry.PowerGrid;
            if (powerGridService != null)
            {
                BatteryRuntimeSnapshot batterySnapshot = powerGridService.BatterySnapshot;
                _powerNormalized = batterySnapshot.TotalCapacityWattSeconds > 0.0001f
                    ? math.saturate(batterySnapshot.ChargeNormalized)
                    : ResolveSupplyRatio(powerGridService.TotalGeneration, powerGridService.TotalConsumption);
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
            float maxPressureKPa = 0f;
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                minOxygenFraction = math.min(minOxygenFraction, atmosphereSystem.GetRoomOxygenFraction(roomIndex));
                maxPressureKPa = math.max(maxPressureKPa, atmosphereSystem.GetRoomPressureKPa(roomIndex));
            }

            _oxygenNormalized = math.saturate(minOxygenFraction);
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

            SpectrumSystem spectrumSystem = SpectrumSystem.Instance;
            if (spectrumSystem != null && spectrumSystem.isActiveAndEnabled)
                subsystemStatus |= SubsystemStatus.Sonar;

            _subsystemStatus = subsystemStatus;
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
                if (nextLifeSupportCritical)
                    PlayVoiceAlarm(lifeSupportCriticalClip, s_lifeSupportCaption, 1f);
            }

            if (nextPressureHighActive != _pressureHighActive)
            {
                _pressureHighActive = nextPressureHighActive;
                PublishLog(
                    nextPressureHighActive ? HectonSubmarineOsLogCode.HullPressureHigh : HectonSubmarineOsLogCode.HullPressureStabilized,
                    nextPressureHighActive ? LogPriorityWarning : LogPriorityNormal);
            }

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
            if (_fatalImplosionLatched)
                failureCount++;

            bool multiSystemFailure = failureCount >= 2;
            if (multiSystemFailure && !_multiSystemFailureLatched)
            {
                _multiSystemFailureLatched = true;
                PublishLog(HectonSubmarineOsLogCode.MultiSystemFailure, LogPriorityCritical);
                PlayVoiceAlarm(multiSystemFailureClip, s_multiFailureCaption, 1f);
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

            HectonSubmarineOsSnapshot nextSnapshot = new HectonSubmarineOsSnapshot(
                _subsystemStatus,
                _emergencyLevel,
                _powerNormalized,
                _oxygenNormalized,
                _maxPressureKPa,
                _lowPowerModeActive,
                _lifeSupportCriticalActive,
                _stationKeepingStateCached);

            if (!AreSnapshotsEqual(in _lastPublishedSnapshot, in nextSnapshot))
            {
                _lastPublishedSnapshot = nextSnapshot;
                HectonSubmarineOsEvents.RaiseSnapshotUpdated(in nextSnapshot);
            }
        }

        private void SetLowPowerMode(bool active)
        {
            if (_lowPowerModeActive == active)
                return;

            _lowPowerModeActive = active;
            Fabricator.SetEmergencyPowerLockAll(active);
            ApplyAmbientLightPolicy(active);
            if (active)
                PlayVoiceAlarm(lowPowerWarningClip, s_lowPowerCaption, 0.8f);
        }

        private void ApplyAmbientLightPolicy(bool forceBrownout)
        {
            BaseModule[] modules = Object.FindObjectsByType<BaseModule>(FindObjectsInactive.Exclude);
            if (modules == null)
                return;

            for (int i = 0; i < modules.Length; i++)
            {
                BaseModule module = modules[i];
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

        private void HandlePowerTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot)
        {
            _powerNormalized = math.saturate(snapshot.AvailablePowerNormalized);
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
            PlayVoiceAlarm(multiSystemFailureClip, s_multiFailureCaption, 1f);
        }

        private void PlayEmergencyLevelAlarm(SubmarineEmergencyLevel emergencyLevel)
        {
            switch (emergencyLevel)
            {
                case SubmarineEmergencyLevel.Evacuate:
                    PlayVoiceAlarm(
                        lifeSupportCriticalClip != null ? lifeSupportCriticalClip : multiSystemFailureClip,
                        s_emergencyEvacuateCaption,
                        1f);
                    break;

                case SubmarineEmergencyLevel.Danger:
                    PlayVoiceAlarm(
                        multiSystemFailureClip != null ? multiSystemFailureClip : lifeSupportCriticalClip,
                        s_emergencyDangerCaption,
                        1f);
                    break;
            }
        }

        private void PlayVoiceAlarm(AudioClip clip, char[] captionChars, float intensity)
        {
            IAudioService audioService = GlobalRegistry.Audio;
            if (clip != null)
            {
                if (audioService != null && audioService.IsInitialized)
                {
                    audioService.PlayStatic2D(clip, warningVolume);
                }
            }

            if (captionChars == null || captionChars.Length <= 0)
                return;

            string captionText = new string(captionChars); // COLD ALLOC: string[1] — spatial caption payload authored on state-transition boundaries only — owner: HectonSubmarineOS
            AudioCaptionEvents.Raise(new AudioCaptionRequest(captionText, transform.position, 2.4f, math.saturate(intensity)));
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

        private SubmarineEmergencyLevel ResolveEmergencyLevel()
        {
            if (_fatalImplosionLatched || _oxygenNormalized <= EvacuateOxygenThreshold01)
                return SubmarineEmergencyLevel.Evacuate;

            if (_lifeSupportCriticalActive || _powerNormalized <= DangerPowerThreshold01 || _maxPressureKPa >= PressureDangerThresholdKPa)
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
                   math.abs(a.MaxPressureKPa - b.MaxPressureKPa) <= 0.5f &&
                   a.LowPowerModeActive == b.LowPowerModeActive &&
                   a.LifeSupportCriticalActive == b.LifeSupportCriticalActive &&
                   a.StationKeepingActive == b.StationKeepingActive;
        }
    }
}
