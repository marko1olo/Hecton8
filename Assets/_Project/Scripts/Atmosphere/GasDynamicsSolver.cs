using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere/Gas Dynamics Solver")]
    public sealed class GasDynamicsSolver : MonoBehaviour, IGasDynamicsSolver, IFixedTickable, IPostFixedTickable, IFrostTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxRoomCapacity = 128;
        private const int MaxBulkheadCapacity = 256;
        private const int MaxBaseCapacity = 32;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 64;
        private const int PendingBaseTransitionCapacity = 128;
        private const double TransitionOverflowAwakeSeconds = 2.0d;
        private const uint DumpMagic = 0x48384744u; // H8GD
        private const int DumpFormatVersion = 2;
        private const float KPaPerAtmosphere = HectonSurvivalContract.KPaPerAtmosphere;
        private const float StandardOxygenKPa = HectonSurvivalContract.StandardOxygenKPa;
        private const float StandardCarbonDioxideKPa = HectonSurvivalContract.StandardCarbonDioxideKPa;
        private const float StandardNitrogenKPa = HectonSurvivalContract.StandardNitrogenKPa;
        private const float DefaultCo2ToxicityThresholdKPa = HectonSurvivalContract.DefaultCo2ToxicityThresholdKPa;
        private const float DefaultCo2FatalKPa = HectonSurvivalContract.DefaultCo2FatalKPa;
        private const float DefaultNarcosisThresholdAtm = HectonSurvivalContract.DefaultNarcosisThresholdAtm;
        private const float DefaultNarcosisFullAtm = HectonSurvivalContract.DefaultNarcosisFullAtm;
        private const float DefaultPlayerO2KPaPerSecond = HectonSurvivalContract.DefaultPlayerOxygenKPaPerSecond;
        private const float DefaultPlayerCO2KPaPerSecond = HectonSurvivalContract.DefaultPlayerCarbonDioxideKPaPerSecond;
        private const float DefaultFireO2KPaPerSecond = HectonSurvivalContract.DefaultFireOxygenKPaPerSecond;
        private const float DefaultScrubberKPaPerSecond = HectonSurvivalContract.DefaultScrubberKPaPerSecond;
        private const float DefaultRoomTemperatureCelsius = HectonSurvivalContract.DefaultRoomTemperatureCelsius;
        private const float FreezingScrubberEfficiencyScale = HectonSurvivalContract.FreezingScrubberEfficiencyScale;
        private const float DefaultDiffusionConductancePerSecond = HectonSurvivalContract.DefaultDiffusionConductancePerSecond;
        private const float DefaultHibernationDistanceMeters = HectonSurvivalContract.DefaultHibernationDistanceMeters;
        private const float DefaultLowTierHibernationDistanceMeters = HectonSurvivalContract.DefaultLowTierHibernationDistanceMeters;
        private const float DefaultHibernationHysteresisMeters = HectonSurvivalContract.DefaultHibernationHysteresisMeters;
        private const float DefaultBaseIdleDrawWatts = HectonSurvivalContract.DefaultBaseIdleDrawWatts;
        private const float DefaultBaseBatteryWattSeconds = HectonSurvivalContract.DefaultBaseBatteryWattSeconds;
        private const float DefaultHibernationLeakRatePerSecond = HectonSurvivalContract.DefaultHibernationLeakRatePerSecond;
        private const float MaxWakeCatchUpSeconds = HectonSurvivalContract.MaxWakeCatchUpSeconds;
        private const float MaxDiffusionFractionPerStep = HectonSurvivalContract.MaxDiffusionFractionPerStep;
        private const ushort TelemetryFlagNaN = 1 << 0;
        private const ushort TelemetryFlagBreach = 1 << 1;
        private const ushort TelemetryFlagHibernating = 1 << 2;
        private const ushort TelemetryFlagFailure = 1 << 15;
        private const ushort TelemetryFailureStateWriteLock = (ushort)(TelemetryFlagFailure | 3);
        private const byte ConsecutiveStateWriteLockFailureDumpThreshold = 4;
        private const ushort ToxicityFlagCO2 = 1 << 0;
        private const ushort ToxicityFlagNarcosis = 1 << 1;
        private const uint PlayerTargetHash = 0x504C5952u; // PLYR
        private const uint GasCarbonDioxideChemicalHash = 0x434F3247u; // CO2G
        private const float ToxicitySignalEpsilon = 0.0001f;
        private const float ToxicityExposureDeltaScalePerSecond = 0.08f;
        private const ushort RoomFlagInternalFire = (ushort)GasDynamicsRoomFlags.InternalFire;
        private const ushort RoomFlagBreached = (ushort)GasDynamicsRoomFlags.Breached;
        private const ushort RoomFlagOccupied = (ushort)GasDynamicsRoomFlags.Occupied;
        private const float AuthoritativeQualityWeight = 1f;
        private const string DumpFileName = "Dump_1324_SubmarineAtmosphere.bin";
        private const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;
        private const BufferID RoomO2BufferId = (BufferID)74420;
        private const BufferID RoomCO2BufferId = (BufferID)74421;
        private const BufferID RoomPressureBufferId = (BufferID)74422;
        private const BufferID RoomO2BackBufferId = (BufferID)74423;
        private const BufferID RoomCO2BackBufferId = (BufferID)74424;
        private const BufferID RoomNitrogenBufferId = (BufferID)74425;
        private const BufferID RoomNitrogenBackBufferId = (BufferID)74426;
        private const BufferID RoomPressureBackBufferId = (BufferID)74427;
        private const BufferID RoomAmbientPressureBufferId = (BufferID)74428;
        private const BufferID RoomSubmerged01BufferId = (BufferID)74429;
        private const BufferID RoomPlayerStress01BufferId = (BufferID)74430;
        private const BufferID RoomPlayerHeartRateBpmBufferId = (BufferID)74431;
        private const BufferID RoomTemperatureCelsiusBufferId = (BufferID)74432;
        private const BufferID RoomPlayerPresentBufferId = (BufferID)74433;
        private const BufferID RoomScrubberPoweredBufferId = (BufferID)74434;
        private const BufferID RoomFlagsBufferId = (BufferID)74435;
        private const BufferID RoomBaseIndexBufferId = (BufferID)74436;
        private const BufferID BasePlayerInsideBufferId = (BufferID)74437;
        private const BufferID BasePlayerInsideCountBufferId = (BufferID)74438;
        private const BufferID BaseRoomStartBufferId = (BufferID)74439;
        private const BufferID BaseRoomCountBufferId = (BufferID)74440;
        private const BufferID BaseCenterAupBufferId = (BufferID)74441;
        private const BufferID BaseHibernatedUnscaledTimeBufferId = (BufferID)74442;
        private const BufferID BaseBatteryWattSecondsBufferId = (BufferID)74443;
        private const BufferID BaseIdleDrawWattsBufferId = (BufferID)74444;
        private const BufferID BaseLeakRatePerSecondBufferId = (BufferID)74445;
        private const BufferID BaseAmbientOxygenKPaBufferId = (BufferID)74446;
        private const BufferID BulkheadRoomABufferId = (BufferID)74447;
        private const BufferID BulkheadRoomBBufferId = (BufferID)74448;
        private const BufferID BulkheadSealedBufferId = (BufferID)74449;
        private const uint LockRoomO2 = 1u << 0;
        private const uint LockRoomCO2 = 1u << 1;
        private const uint LockRoomPressure = 1u << 2;
        private const uint LockRoomO2Back = 1u << 3;
        private const uint LockRoomCO2Back = 1u << 4;
        private const uint LockRoomNitrogen = 1u << 5;
        private const uint LockRoomNitrogenBack = 1u << 6;
        private const uint LockRoomPressureBack = 1u << 7;
        private const uint LockRoomAmbientPressure = 1u << 8;
        private const uint LockRoomSubmerged01 = 1u << 9;
        private const uint LockRoomPlayerStress01 = 1u << 10;
        private const uint LockRoomPlayerHeartRateBpm = 1u << 11;
        private const uint LockRoomTemperatureCelsius = 1u << 12;
        private const uint LockRoomPlayerPresent = 1u << 13;
        private const uint LockRoomScrubberPowered = 1u << 14;
        private const uint LockRoomFlags = 1u << 15;
        private const uint LockRoomBaseIndex = 1u << 16;
        private const uint LockBaseAwakeState = 1u << 17;
        private const uint LockBasePlayerInside = 1u << 18;
        private const uint LockBasePlayerInsideCount = 1u << 19;
        private const uint LockBaseRoomStart = 1u << 20;
        private const uint LockBaseRoomCount = 1u << 21;
        private const uint LockBaseCenterAup = 1u << 22;
        private const uint LockBaseHibernatedUnscaledTime = 1u << 23;
        private const uint LockBaseBatteryWattSeconds = 1u << 24;
        private const uint LockBaseIdleDrawWatts = 1u << 25;
        private const uint LockBaseLeakRatePerSecond = 1u << 26;
        private const uint LockBaseAmbientOxygenKPa = 1u << 27;
        private const uint LockBulkheadRoomA = 1u << 28;
        private const uint LockBulkheadRoomB = 1u << 29;
        private const uint LockBulkheadSealed = 1u << 30;

        [SerializeField, Range(1, MaxRoomCapacity)] private int roomCapacity = 64;
        [SerializeField, Range(0, MaxBulkheadCapacity)] private int bulkheadCapacity = 128;
        [SerializeField, Min(0.1f)] private float lowTierColdTickSeconds = 2.0f;
        [SerializeField, Min(0.05f)] private float midTierColdTickSeconds = 0.5f;
        [SerializeField, Min(0.02f)] private float highTierColdTickSeconds = 0.1f;
        [SerializeField, Min(0f)] private float playerOxygenKPaPerSecond = DefaultPlayerO2KPaPerSecond;
        [SerializeField, Min(0f)] private float playerCarbonDioxideKPaPerSecond = DefaultPlayerCO2KPaPerSecond;
        [SerializeField, Min(0f)] private float fireOxygenDrainKPaPerSecond = DefaultFireO2KPaPerSecond;
        [SerializeField, Min(0f)] private float scrubberKPaPerSecond = DefaultScrubberKPaPerSecond;
        [SerializeField, Range(0f, 2f)] private float diffusionConductancePerSecond = DefaultDiffusionConductancePerSecond;
        [SerializeField, Min(0f)] private float co2ToxicityThresholdKPa = DefaultCo2ToxicityThresholdKPa;
        [SerializeField, Min(0.1f)] private float co2FatalKPa = DefaultCo2FatalKPa;
        [SerializeField, Min(1f)] private float narcosisThresholdAtm = DefaultNarcosisThresholdAtm;
        [SerializeField, Min(1f)] private float narcosisFullAtm = DefaultNarcosisFullAtm;
        [SerializeField, Range(0f, 0.95f)] private float maxHullStressRelief01 = 0.45f;
        [SerializeField, Range(0f, 0.5f)] private float hullStressReliefPerAtm = 0.08f;
        [SerializeField] private bool seedStandardAtmosphereOnEnable = true;
        [Header("Base Hibernation")]
        [Tooltip("Maximum atmosphere islands tracked by the hibernation mask.")]
        [SerializeField, Range(1, MaxBaseCapacity)] private int baseCapacity = 8;
        [Tooltip("Authoritative hibernation distance. Hardware quality must not alter gas truth.")]
        [SerializeField, Min(1f)] private float hibernationDistanceMeters = DefaultHibernationDistanceMeters;
        [Tooltip("Legacy serialized fallback. Gas authority treats this only as a conservative distance floor.")]
        [SerializeField, Min(1f)] private float lowTierHibernationDistanceMeters = DefaultLowTierHibernationDistanceMeters;
        [Tooltip("Distance band preventing awake/sleep flicker around the hibernation threshold.")]
        [SerializeField, Min(3f)] private float hibernationHysteresisMeters = DefaultHibernationHysteresisMeters;
        [Tooltip("Fallback battery capacity for unconfigured base atmosphere islands.")]
        [SerializeField, Min(0f)] private float defaultBaseBatteryWattSeconds = DefaultBaseBatteryWattSeconds;
        [Tooltip("Fallback idle life-support draw applied while a base is hibernating.")]
        [SerializeField, Min(0f)] private float defaultBaseIdleDrawWatts = DefaultBaseIdleDrawWatts;
        [Tooltip("Analytical oxygen leak rate used during hibernation catch-up.")]
        [SerializeField, Min(0f)] private float hibernationLeakRatePerSecond = DefaultHibernationLeakRatePerSecond;
        [Tooltip("Ambient oxygen partial pressure target used by the analytical leak fake.")]
        [SerializeField, Min(0f)] private float hibernationAmbientOxygenKPa = StandardOxygenKPa;

        private VaultGenerationHandle<float> _roomO2Handle;
        private VaultGenerationHandle<float> _roomCO2Handle;
        private VaultGenerationHandle<float> _roomPressureHandle;
        private VaultGenerationHandle<float> _roomO2BackHandle;
        private VaultGenerationHandle<float> _roomCO2BackHandle;
        private VaultGenerationHandle<float> _roomNitrogenHandle;
        private VaultGenerationHandle<float> _roomNitrogenBackHandle;
        private VaultGenerationHandle<float> _roomPressureBackHandle;
        private VaultGenerationHandle<float> _roomAmbientPressureHandle;
        private VaultGenerationHandle<float> _roomSubmerged01Handle;
        private VaultGenerationHandle<float> _roomPlayerStress01Handle;
        private VaultGenerationHandle<float> _roomPlayerHeartRateBpmHandle;
        private VaultGenerationHandle<float> _roomTemperatureCelsiusHandle;
        private VaultGenerationHandle<byte> _roomPlayerPresentHandle;
        private VaultGenerationHandle<byte> _roomScrubberPoweredHandle;
        private VaultGenerationHandle<ushort> _roomFlagsHandle;
        private VaultGenerationHandle<int> _roomBaseIndexHandle;
        private VaultGenerationHandle<byte> _baseAwakeStateHandle;
        private VaultGenerationHandle<byte> _basePlayerInsideHandle;
        private VaultGenerationHandle<int> _basePlayerInsideCountHandle;
        private VaultGenerationHandle<int> _baseRoomStartHandle;
        private VaultGenerationHandle<int> _baseRoomCountHandle;
        private VaultGenerationHandle<AbsoluteUniversePosition> _baseCenterAupHandle;
        private VaultGenerationHandle<double> _baseHibernatedUnscaledTimeHandle;
        private VaultGenerationHandle<float> _baseBatteryWattSecondsHandle;
        private VaultGenerationHandle<float> _baseIdleDrawWattsHandle;
        private VaultGenerationHandle<float> _baseLeakRatePerSecondHandle;
        private VaultGenerationHandle<float> _baseAmbientOxygenKPaHandle;
        private VaultGenerationHandle<int> _bulkheadRoomAHandle;
        private VaultGenerationHandle<int> _bulkheadRoomBHandle;
        private VaultGenerationHandle<byte> _bulkheadSealedHandle;
        private VaultGenerationHandle<AtmosphereTelemetryEntry> _telemetryRingHandle;
        // COLD ALLOC: PendingBaseTransitionSignal[128] - fixed managed staging for same-phase gas mutation - owner: GasDynamicsSolver
        private readonly PendingBaseTransitionSignal[] _deferredBaseTransitions = new PendingBaseTransitionSignal[PendingBaseTransitionCapacity];
        private int _deferredBaseTransitionCount;
        private bool _stepRunning;
        private bool _registeredTicks;
        private bool _registeredRegistry;
        private bool _registeredHotSwap;
        private bool _telemetryRingLocked;
        private uint _stateWriteLockMask;
        private int _stateWriteLockDepth;
        private int _stateWriteRequiredRoomCount;
        private int _stateWriteRequiredBaseCount;
        private int _stateWriteRequiredBulkheadCount;
        private byte _consecutiveStateWriteLockFailures;
        private bool _seededStandardAtmosphere;
        private bool _blackBoxDumped;
        private bool _deferredBaseTransitionOverflow;
        private double _transitionOverflowAwakeUntil;
        private ToxicitySignal _latestToxicitySignal;
        private int _latestToxicitySignalSequence;
        private int _toxicitySignalReadSequence;
        private int _toxicityExposureSignalDropCount;
        // Fixed 128-room bitmask stages repair seals while the gas job owns room lanes.
        private ulong _pendingHullRepairRoomsLo;
        private ulong _pendingHullRepairRoomsHi;
        private int _roomCount;
        private int _bulkheadCapacityLimit;
        private int _bulkheadCount;
        private int _baseCapacityLimit;
        private int _baseCount;
        private int _sleepingBaseCount;
        private int _activePlayerRoom = -1;
        private int _telemetryWriteIndex;
        private int _tickCount;
        private float _tickAccumulator;
        private float _lastCadenceSeconds = 0.1f;
        private ITickDispatcher _tickDispatcher;
        private IPlayerMovementContracts _playerMovementContracts;
        private IDataVault _dataVault;

        private NativeArray<float> ResolveRoomO2() => ResolveLane(in _roomO2Handle);
        private NativeArray<float> ResolveRoomCO2() => ResolveLane(in _roomCO2Handle);
        private NativeArray<float> ResolveRoomPressure() => ResolveLane(in _roomPressureHandle);
        private NativeArray<float> ResolveRoomO2Back() => ResolveLane(in _roomO2BackHandle);
        private NativeArray<float> ResolveRoomCO2Back() => ResolveLane(in _roomCO2BackHandle);
        private NativeArray<float> ResolveRoomNitrogen() => ResolveLane(in _roomNitrogenHandle);
        private NativeArray<float> ResolveRoomNitrogenBack() => ResolveLane(in _roomNitrogenBackHandle);
        private NativeArray<float> ResolveRoomPressureBack() => ResolveLane(in _roomPressureBackHandle);
        private NativeArray<float> ResolveRoomAmbientPressure() => ResolveLane(in _roomAmbientPressureHandle);
        private NativeArray<float> ResolveRoomSubmerged01() => ResolveLane(in _roomSubmerged01Handle);
        private NativeArray<float> ResolveRoomPlayerStress01() => ResolveLane(in _roomPlayerStress01Handle);
        private NativeArray<float> ResolveRoomPlayerHeartRateBpm() => ResolveLane(in _roomPlayerHeartRateBpmHandle);
        private NativeArray<float> ResolveRoomTemperatureCelsius() => ResolveLane(in _roomTemperatureCelsiusHandle);
        private NativeArray<byte> ResolveRoomPlayerPresent() => ResolveLane(in _roomPlayerPresentHandle);
        private NativeArray<byte> ResolveRoomScrubberPowered() => ResolveLane(in _roomScrubberPoweredHandle);
        private NativeArray<ushort> ResolveRoomFlags() => ResolveLane(in _roomFlagsHandle);
        private NativeArray<int> ResolveRoomBaseIndex() => ResolveLane(in _roomBaseIndexHandle);
        private NativeArray<byte> ResolveBaseAwakeState() => ResolveLane(in _baseAwakeStateHandle);
        private NativeArray<byte> ResolveBasePlayerInside() => ResolveLane(in _basePlayerInsideHandle);
        private NativeArray<int> ResolveBasePlayerInsideCount() => ResolveLane(in _basePlayerInsideCountHandle);
        private NativeArray<int> ResolveBaseRoomStart() => ResolveLane(in _baseRoomStartHandle);
        private NativeArray<int> ResolveBaseRoomCount() => ResolveLane(in _baseRoomCountHandle);
        private NativeArray<AbsoluteUniversePosition> ResolveBaseCenterAup() => ResolveLane(in _baseCenterAupHandle);
        private NativeArray<double> ResolveBaseHibernatedUnscaledTime() => ResolveLane(in _baseHibernatedUnscaledTimeHandle);
        private NativeArray<float> ResolveBaseBatteryWattSeconds() => ResolveLane(in _baseBatteryWattSecondsHandle);
        private NativeArray<float> ResolveBaseIdleDrawWatts() => ResolveLane(in _baseIdleDrawWattsHandle);
        private NativeArray<float> ResolveBaseLeakRatePerSecond() => ResolveLane(in _baseLeakRatePerSecondHandle);
        private NativeArray<float> ResolveBaseAmbientOxygenKPa() => ResolveLane(in _baseAmbientOxygenKPaHandle);
        private NativeArray<int> ResolveBulkheadRoomA() => ResolveLane(in _bulkheadRoomAHandle);
        private NativeArray<int> ResolveBulkheadRoomB() => ResolveLane(in _bulkheadRoomBHandle);
        private NativeArray<byte> ResolveBulkheadSealed() => ResolveLane(in _bulkheadSealedHandle);

        public bool IsInitialized =>
            _deferredBaseTransitions.Length >= PendingBaseTransitionCapacity &&
            IsTelemetryRingReady() &&
            AreRoomStateLanesReady(_roomCount) &&
            AreBulkheadLanesReady(_bulkheadCount) &&
            AreBaseStateLanesReady(_baseCount);
        public int RoomCount => _roomCount;
        public int BaseCount => _baseCount;
        public float LastCadenceSeconds => _lastCadenceSeconds;
        int ISystem.TickCount => _tickCount;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheColdDependencies();
            TryRegisterHotSwapListener();
            TryRegisterRegistry();
            ConfigureColdSignalLanes();
            if (!TryFinalizeDeferredNativeDisposal())
            {
                TryRegisterTicks();
                return;
            }

            EnsureNativeState();
            SeedStandardAtmosphereIfNeeded();
            TryRegisterTicks();
        }

        private void OnDisable()
        {
            TryUnregisterTicks();
            TryUnregisterRegistry();
            TryUnregisterHotSwapListener();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            TryUnregisterTicks();
            TryUnregisterRegistry();
            TryUnregisterHotSwapListener();
            DisposeNativeStateDeferred();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            if (!TryFinalizeDeferredNativeDisposal())
                return;

            if (!IsInitialized)
                return;

            SeedStandardAtmosphereIfNeeded();
            if (!TryCompleteStep())
            {
                DrainBaseTransitionSignals(allowWake: false);
                DrainHullRepairedSignals();
                _tickAccumulator += math.max(0f, fixedDeltaTime);
                return;
            }

            double now = ResolveUnscaledTimeSeconds();
            if (!TryAcquireStateWriteLocks())
            {
                RecordStateWriteLockFailure(0u);
                return;
            }

            try
            {
                DrainBaseTransitionSignals(allowWake: true);
                DrainHullRepairedSignals();
                WakePlayerInsideSleepingBases(now);
            }
            finally
            {
                ReleaseStateWriteLocks();
            }

            float qualityWeight01 = ResolveGlobalQualityWeight();
            _lastCadenceSeconds = ResolveCadenceSeconds(qualityWeight01);
            _tickAccumulator += math.max(0f, fixedDeltaTime);
            if (_tickAccumulator + 0.0001f < _lastCadenceSeconds)
                return;

            float deltaTime = _tickAccumulator;
            _tickAccumulator = 0f;
            ScheduleStep(deltaTime);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            TryCompleteStep();
        }

        public void FrostTick()
        {
            if (!Application.isPlaying)
                return;

            if (!TryFinalizeDeferredNativeDisposal() || !TryCompleteStep())
                return;

            if (!IsInitialized)
                return;

            SeedStandardAtmosphereIfNeeded();
            if (!TryAcquireStateWriteLocks())
            {
                RecordStateWriteLockFailure(0u);
                return;
            }

            try
            {
                DrainBaseTransitionSignals(allowWake: true);
                DrainHullRepairedSignals();
                WakePlayerInsideSleepingBases(ResolveUnscaledTimeSeconds());
                ResolveBaseHibernationStates();
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TryGetRoomSnapshot(int roomId, out GasRoomSnapshot snapshot)
        {
            snapshot = default;
            if (_stepRunning ||
                roomId < 0 ||
                roomId >= _roomCount ||
                !TryReadLane(in _roomO2Handle, roomId + 1, out NativeArray<float>.ReadOnly RoomO2) ||
                !TryReadLane(in _roomCO2Handle, roomId + 1, out NativeArray<float>.ReadOnly RoomCO2) ||
                !TryReadLane(in _roomNitrogenHandle, roomId + 1, out NativeArray<float>.ReadOnly _roomNitrogen) ||
                !TryReadLane(in _roomAmbientPressureHandle, roomId + 1, out NativeArray<float>.ReadOnly _roomAmbientPressure) ||
                !TryReadLane(in _roomFlagsHandle, roomId + 1, out NativeArray<ushort>.ReadOnly _roomFlags))
            {
                return false;
            }

            float oxygen = FiniteNonNegativeOrZero(RoomO2[roomId]);
            float carbonDioxide = FiniteNonNegativeOrZero(RoomCO2[roomId]);
            float nitrogen = FiniteNonNegativeOrZero(_roomNitrogen[roomId]);
            float pressure = ResolveDaltonPressureKPa(oxygen, carbonDioxide, nitrogen);
            float pressureAtm = pressure * math.rcp(KPaPerAtmosphere);
            snapshot = new GasRoomSnapshot(
                roomId,
                oxygen,
                carbonDioxide,
                nitrogen,
                pressure,
                FiniteNonNegativeOrZero(_roomAmbientPressure[roomId]),
                ResolveToxicity01(carbonDioxide, co2ToxicityThresholdKPa, co2FatalKPa),
                ResolveNarcosis01(pressureAtm, narcosisThresholdAtm, narcosisFullAtm),
                _roomFlags[roomId]);
            return true;
        }

        public bool TryGetBaseHibernationSnapshot(int baseId, out GasBaseHibernationSnapshot snapshot)
        {
            snapshot = default;
            if (_stepRunning ||
                baseId < 0 ||
                baseId >= _baseCount ||
                !TryReadLane(in _baseRoomStartHandle, baseId + 1, out NativeArray<int>.ReadOnly _baseRoomStart) ||
                !TryReadLane(in _baseRoomCountHandle, baseId + 1, out NativeArray<int>.ReadOnly _baseRoomCount) ||
                !TryReadLane(in _baseCenterAupHandle, baseId + 1, out NativeArray<AbsoluteUniversePosition>.ReadOnly _baseCenterAup) ||
                !TryReadLane(in _baseAwakeStateHandle, baseId + 1, out NativeArray<byte>.ReadOnly BaseAwakeState) ||
                !TryReadLane(in _basePlayerInsideHandle, baseId + 1, out NativeArray<byte>.ReadOnly _basePlayerInside) ||
                !TryReadLane(in _baseBatteryWattSecondsHandle, baseId + 1, out NativeArray<float>.ReadOnly _baseBatteryWattSeconds) ||
                !TryReadLane(in _baseIdleDrawWattsHandle, baseId + 1, out NativeArray<float>.ReadOnly _baseIdleDrawWatts) ||
                !TryReadLane(in _baseLeakRatePerSecondHandle, baseId + 1, out NativeArray<float>.ReadOnly _baseLeakRatePerSecond) ||
                !TryReadLane(in _baseHibernatedUnscaledTimeHandle, baseId + 1, out NativeArray<double>.ReadOnly _baseHibernatedUnscaledTime))
            {
                return false;
            }

            snapshot = new GasBaseHibernationSnapshot(
                baseId,
                _baseRoomStart[baseId],
                _baseRoomCount[baseId],
                _baseCenterAup[baseId],
                BaseAwakeState[baseId] != 0,
                _basePlayerInside[baseId] != 0,
                FiniteNonNegativeOrZero(_baseBatteryWattSeconds[baseId]),
                FiniteNonNegativeOrZero(_baseIdleDrawWatts[baseId]),
                FiniteNonNegativeOrZero(_baseLeakRatePerSecond[baseId]),
                _baseHibernatedUnscaledTime[baseId]);
            return true;
        }

        public bool TryConfigureRoom(
            int roomId,
            float oxygenKPa,
            float carbonDioxideKPa,
            float nitrogenKPa,
            float ambientPressureKPa,
            ushort flags)
        {
            if (_stepRunning || roomId < 0 || !AreRoomStateLanesReady(roomId + 1))
                return false;

            if (!TryAcquireStateWriteLocks(roomId + 1, _baseCount, math.max(1, _bulkheadCapacityLimit)))
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<float> RoomO2 = ResolveRoomO2();
                NativeArray<float> RoomCO2 = ResolveRoomCO2();
                NativeArray<float> RoomPressure = ResolveRoomPressure();
                NativeArray<float> _roomO2Back = ResolveRoomO2Back();
                NativeArray<float> _roomCO2Back = ResolveRoomCO2Back();
                NativeArray<float> _roomNitrogen = ResolveRoomNitrogen();
                NativeArray<float> _roomNitrogenBack = ResolveRoomNitrogenBack();
                NativeArray<float> _roomPressureBack = ResolveRoomPressureBack();
                NativeArray<float> _roomAmbientPressure = ResolveRoomAmbientPressure();
                NativeArray<byte> _roomPlayerPresent = ResolveRoomPlayerPresent();
                NativeArray<ushort> _roomFlags = ResolveRoomFlags();
                oxygenKPa = FiniteNonNegativeOrZero(oxygenKPa);
                carbonDioxideKPa = FiniteNonNegativeOrZero(carbonDioxideKPa);
                nitrogenKPa = FiniteNonNegativeOrZero(nitrogenKPa);
                ambientPressureKPa = FiniteNonNegativeOrZero(ambientPressureKPa);
                float pressureKPa = ResolveDaltonPressureKPa(oxygenKPa, carbonDioxideKPa, nitrogenKPa);
                RoomO2[roomId] = oxygenKPa;
                RoomCO2[roomId] = carbonDioxideKPa;
                _roomNitrogen[roomId] = nitrogenKPa;
                RoomPressure[roomId] = pressureKPa;
                _roomO2Back[roomId] = oxygenKPa;
                _roomCO2Back[roomId] = carbonDioxideKPa;
                _roomNitrogenBack[roomId] = nitrogenKPa;
                _roomPressureBack[roomId] = pressureKPa;
                _roomAmbientPressure[roomId] = ambientPressureKPa;
                if (_roomPlayerPresent[roomId] != 0)
                    flags = (ushort)(flags | RoomFlagOccupied);
                _roomFlags[roomId] = flags;
                if (roomId >= _roomCount)
                    _roomCount = roomId + 1;
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TryConfigureBase(
            int baseId,
            int roomStart,
            int roomCount,
            AbsoluteUniversePosition centerAup,
            float batteryWattSeconds,
            float idleDrawWatts,
            float leakRatePerSecond)
        {
            if (_stepRunning ||
                baseId < 0 ||
                baseId >= _baseCapacityLimit ||
                !IsFiniteAup(in centerAup) ||
                !AreBaseStateLanesReady(baseId + 1) ||
                !AreRoomStateLanesReady(_roomCount))
            {
                return false;
            }

            if (!TryAcquireStateWriteLocks(_roomCount, baseId + 1, math.max(1, _bulkheadCapacityLimit)))
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<int> _roomBaseIndex = ResolveRoomBaseIndex();
                NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
                NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
                NativeArray<int> _basePlayerInsideCount = ResolveBasePlayerInsideCount();
                NativeArray<int> _baseRoomStart = ResolveBaseRoomStart();
                NativeArray<int> _baseRoomCount = ResolveBaseRoomCount();
                NativeArray<double> _baseHibernatedUnscaledTime = ResolveBaseHibernatedUnscaledTime();
                int mappedRoomCount = math.min(_roomCount, _roomBaseIndex.Length);
                int previousStart = 0;
                int previousEnd = 0;
                bool knownBase = baseId < _baseCount;
                if (knownBase)
                {
                    previousStart = math.clamp(_baseRoomStart[baseId], 0, math.max(0, mappedRoomCount));
                    previousEnd = math.min(mappedRoomCount, previousStart + math.max(0, _baseRoomCount[baseId]));
                }

                int safeRoomStart = math.clamp(roomStart, 0, math.max(0, mappedRoomCount - 1));
                int safeRoomCount = math.clamp(roomCount, 0, math.max(0, mappedRoomCount - safeRoomStart));
                for (int room = previousStart; room < previousEnd; room++)
                {
                    if (_roomBaseIndex[room] == baseId)
                        _roomBaseIndex[room] = 0;
                }

                ConfigureBaseSlot(
                    baseId,
                    safeRoomStart,
                    safeRoomCount,
                    in centerAup,
                    FiniteNonNegativeOrZero(batteryWattSeconds),
                    FiniteNonNegativeOrZero(idleDrawWatts),
                    FiniteNonNegativeOrZero(leakRatePerSecond),
                    hibernationAmbientOxygenKPa);

                for (int room = safeRoomStart; room < safeRoomStart + safeRoomCount; room++)
                    _roomBaseIndex[room] = baseId;

                if (!knownBase)
                {
                    BaseAwakeState[baseId] = 1;
                    _basePlayerInside[baseId] = 0;
                    _basePlayerInsideCount[baseId] = 0;
                    _baseHibernatedUnscaledTime[baseId] = ResolveUnscaledTimeSeconds();
                }

                _baseCount = math.max(_baseCount, baseId + 1);
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetBasePlayerInside(int baseId, bool playerInside)
        {
            if (_stepRunning || baseId < 0 || baseId >= _baseCount || !AreBaseStateLanesReady(baseId + 1))
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
                NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
                NativeArray<int> _basePlayerInsideCount = ResolveBasePlayerInsideCount();
                _basePlayerInsideCount[baseId] = playerInside ? math.max(1, _basePlayerInsideCount[baseId]) : 0;
                _basePlayerInside[baseId] = (byte)(playerInside ? 1 : 0);
                if (playerInside && BaseAwakeState[baseId] == 0)
                    WakeBase(baseId, ResolveUnscaledTimeSeconds());
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetBaseCenterAup(int baseId, AbsoluteUniversePosition centerAup)
        {
            if (_stepRunning ||
                baseId < 0 ||
                baseId >= _baseCount ||
                !IsFiniteAup(in centerAup) ||
                !AreBaseStateLanesReady(baseId + 1))
            {
                return false;
            }

            if (!TryAcquireStateWriteLocks(_roomCount, baseId + 1, math.max(1, _bulkheadCapacityLimit)))
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<AbsoluteUniversePosition> _baseCenterAup = ResolveBaseCenterAup();
                _baseCenterAup[baseId] = centerAup;
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetBulkhead(int edgeIndex, int roomA, int roomB, bool sealedBulkhead)
        {
            if (_stepRunning ||
                edgeIndex < 0 ||
                edgeIndex >= _bulkheadCapacityLimit ||
                !AreBulkheadLanesReady(edgeIndex + 1) ||
                !AreRoomStateLanesReady(_roomCount))
            {
                return false;
            }

            if (roomA < 0 || roomB < 0 || roomA >= _roomCount || roomB >= _roomCount || roomA == roomB)
                return false;

            if (!TryAcquireStateWriteLocks(_roomCount, _baseCount, edgeIndex + 1))
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<int> _bulkheadRoomA = ResolveBulkheadRoomA();
                NativeArray<int> _bulkheadRoomB = ResolveBulkheadRoomB();
                NativeArray<byte> _bulkheadSealed = ResolveBulkheadSealed();
                _bulkheadRoomA[edgeIndex] = roomA;
                _bulkheadRoomB[edgeIndex] = roomB;
                _bulkheadSealed[edgeIndex] = (byte)(sealedBulkhead ? 1 : 0);
                if (edgeIndex >= _bulkheadCount)
                    _bulkheadCount = edgeIndex + 1;
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetPlayerRoom(int roomId, float playerStress01, float heartRateBpm)
        {
            if (_stepRunning || !AreRoomStateLanesReady(_roomCount))
                return false;

            if (roomId >= _roomCount)
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<byte> _roomPlayerPresent = ResolveRoomPlayerPresent();
                NativeArray<float> _roomPlayerStress01 = ResolveRoomPlayerStress01();
                NativeArray<float> _roomPlayerHeartRateBpm = ResolveRoomPlayerHeartRateBpm();
                NativeArray<ushort> _roomFlags = ResolveRoomFlags();
                if (_activePlayerRoom >= 0 && _activePlayerRoom < _roomCount)
                {
                    _roomPlayerPresent[_activePlayerRoom] = 0;
                    _roomPlayerStress01[_activePlayerRoom] = 0f;
                    _roomPlayerHeartRateBpm[_activePlayerRoom] = 0f;
                    _roomFlags[_activePlayerRoom] = (ushort)(_roomFlags[_activePlayerRoom] & ~RoomFlagOccupied);
                }

                _activePlayerRoom = roomId;
                if (roomId < 0)
                    return true;

                _roomPlayerPresent[roomId] = 1;
                _roomPlayerStress01[roomId] = FiniteSaturate01(playerStress01);
                _roomPlayerHeartRateBpm[roomId] = FiniteNonNegativeOrZero(heartRateBpm);
                _roomFlags[roomId] = (ushort)(_roomFlags[roomId] | RoomFlagOccupied);
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TryApplyPlayerRoomCarbonDioxideEquivalentPressure(float carbonDioxideKPa)
        {
            if (_stepRunning ||
                _roomCount <= 0)
            {
                return false;
            }

            int roomId = _activePlayerRoom >= 0 ? _activePlayerRoom : 0;
            if ((uint)roomId >= (uint)_roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<float> RoomO2 = ResolveRoomO2();
                NativeArray<float> RoomCO2 = ResolveRoomCO2();
                NativeArray<float> RoomPressure = ResolveRoomPressure();
                NativeArray<float> _roomCO2Back = ResolveRoomCO2Back();
                NativeArray<float> _roomNitrogen = ResolveRoomNitrogen();
                NativeArray<float> _roomPressureBack = ResolveRoomPressureBack();
                float targetCarbonDioxide = StandardCarbonDioxideKPa + FiniteNonNegativeOrZero(carbonDioxideKPa);
                float frontCarbonDioxide = math.max(FiniteNonNegativeOrZero(RoomCO2[roomId]), targetCarbonDioxide);
                float backCarbonDioxide = math.max(FiniteNonNegativeOrZero(_roomCO2Back[roomId]), targetCarbonDioxide);
                RoomCO2[roomId] = frontCarbonDioxide;
                _roomCO2Back[roomId] = backCarbonDioxide;

                float oxygen = FiniteNonNegativeOrZero(RoomO2[roomId]);
                float nitrogen = FiniteNonNegativeOrZero(_roomNitrogen[roomId]);
                RoomPressure[roomId] = ResolveDaltonPressureKPa(oxygen, frontCarbonDioxide, nitrogen);
                _roomPressureBack[roomId] = ResolveDaltonPressureKPa(oxygen, backCarbonDioxide, nitrogen);
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetRoomFlags(int roomId, ushort setMask, ushort clearMask)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<byte> _roomPlayerPresent = ResolveRoomPlayerPresent();
                NativeArray<ushort> _roomFlags = ResolveRoomFlags();
                ushort flags = (ushort)((_roomFlags[roomId] | setMask) & ~clearMask);
                if (_roomPlayerPresent[roomId] != 0)
                    flags = (ushort)(flags | RoomFlagOccupied);
                _roomFlags[roomId] = flags;
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetRoomSubmergedFraction(int roomId, float submerged01)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<float> _roomSubmerged01 = ResolveRoomSubmerged01();
                _roomSubmerged01[roomId] = FiniteSaturate01(submerged01);
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetAmbientPressure(int roomId, float ambientPressureKPa)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<float> _roomAmbientPressure = ResolveRoomAmbientPressure();
                _roomAmbientPressure[roomId] = FiniteNonNegativeOrZero(ambientPressureKPa);
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetScrubberPowered(int roomId, bool powerActive)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<byte> _roomScrubberPowered = ResolveRoomScrubberPowered();
                _roomScrubberPowered[roomId] = (byte)(powerActive ? 1 : 0);
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TrySetRoomTemperatureCelsius(int roomId, float temperatureCelsius)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            if (!TryAcquireStateWriteLocks())
                return FailStateWriteLock(0u);

            try
            {
                NativeArray<float> _roomTemperatureCelsius = ResolveRoomTemperatureCelsius();
                _roomTemperatureCelsius[roomId] = math.isfinite(temperatureCelsius)
                    ? math.clamp(temperatureCelsius, -80f, 300f)
                    : DefaultRoomTemperatureCelsius;
                return true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        public bool TryDequeueToxicitySignal(out ToxicitySignal signal)
        {
            if (!IsInitialized)
            {
                signal = default;
                return false;
            }

            int sequence = _latestToxicitySignalSequence;
            if (sequence == 0 || sequence == _toxicitySignalReadSequence)
            {
                signal = default;
                return false;
            }

            signal = _latestToxicitySignal;
            _toxicitySignalReadSequence = sequence;
            return true;
        }

        public bool TryGetNativeMemoryAudit(out GasDynamicsNativeMemoryAudit audit)
        {
            audit = default;
            if (!TryReadLane(in _roomO2Handle, _roomCount, out NativeArray<float>.ReadOnly RoomO2) ||
                !TryReadLane(in _roomCO2Handle, _roomCount, out NativeArray<float>.ReadOnly RoomCO2) ||
                !TryReadLane(in _roomPressureHandle, _roomCount, out NativeArray<float>.ReadOnly RoomPressure) ||
                !TryReadLane(in _roomO2BackHandle, _roomCount, out NativeArray<float>.ReadOnly _roomO2Back) ||
                !TryReadLane(in _roomCO2BackHandle, _roomCount, out NativeArray<float>.ReadOnly _roomCO2Back) ||
                !TryReadLane(in _roomNitrogenHandle, _roomCount, out NativeArray<float>.ReadOnly _roomNitrogen) ||
                !TryReadLane(in _roomNitrogenBackHandle, _roomCount, out NativeArray<float>.ReadOnly _roomNitrogenBack) ||
                !TryReadLane(in _roomPressureBackHandle, _roomCount, out NativeArray<float>.ReadOnly _roomPressureBack) ||
                !TryReadLane(in _roomAmbientPressureHandle, _roomCount, out NativeArray<float>.ReadOnly _roomAmbientPressure) ||
                !TryReadLane(in _roomSubmerged01Handle, _roomCount, out NativeArray<float>.ReadOnly _roomSubmerged01) ||
                !TryReadLane(in _roomPlayerStress01Handle, _roomCount, out NativeArray<float>.ReadOnly _roomPlayerStress01) ||
                !TryReadLane(in _roomPlayerHeartRateBpmHandle, _roomCount, out NativeArray<float>.ReadOnly _roomPlayerHeartRateBpm) ||
                !TryReadLane(in _roomTemperatureCelsiusHandle, _roomCount, out NativeArray<float>.ReadOnly _roomTemperatureCelsius) ||
                !TryReadLane(in _roomPlayerPresentHandle, _roomCount, out NativeArray<byte>.ReadOnly _roomPlayerPresent) ||
                !TryReadLane(in _roomScrubberPoweredHandle, _roomCount, out NativeArray<byte>.ReadOnly _roomScrubberPowered) ||
                !TryReadLane(in _roomFlagsHandle, _roomCount, out NativeArray<ushort>.ReadOnly _roomFlags) ||
                !TryReadLane(in _roomBaseIndexHandle, _roomCount, out NativeArray<int>.ReadOnly _roomBaseIndex) ||
                !TryReadLane(in _baseAwakeStateHandle, _baseCount, out NativeArray<byte>.ReadOnly BaseAwakeState) ||
                !TryReadLane(in _basePlayerInsideHandle, _baseCount, out NativeArray<byte>.ReadOnly _basePlayerInside) ||
                !TryReadLane(in _basePlayerInsideCountHandle, _baseCount, out NativeArray<int>.ReadOnly _basePlayerInsideCount) ||
                !TryReadLane(in _baseRoomStartHandle, _baseCount, out NativeArray<int>.ReadOnly _baseRoomStart) ||
                !TryReadLane(in _baseRoomCountHandle, _baseCount, out NativeArray<int>.ReadOnly _baseRoomCount) ||
                !TryReadLane(in _baseCenterAupHandle, _baseCount, out NativeArray<AbsoluteUniversePosition>.ReadOnly _baseCenterAup) ||
                !TryReadLane(in _baseHibernatedUnscaledTimeHandle, _baseCount, out NativeArray<double>.ReadOnly _baseHibernatedUnscaledTime) ||
                !TryReadLane(in _baseBatteryWattSecondsHandle, _baseCount, out NativeArray<float>.ReadOnly _baseBatteryWattSeconds) ||
                !TryReadLane(in _baseIdleDrawWattsHandle, _baseCount, out NativeArray<float>.ReadOnly _baseIdleDrawWatts) ||
                !TryReadLane(in _baseLeakRatePerSecondHandle, _baseCount, out NativeArray<float>.ReadOnly _baseLeakRatePerSecond) ||
                !TryReadLane(in _baseAmbientOxygenKPaHandle, _baseCount, out NativeArray<float>.ReadOnly _baseAmbientOxygenKPa) ||
                !TryReadLane(in _bulkheadRoomAHandle, _bulkheadCount, out NativeArray<int>.ReadOnly _bulkheadRoomA) ||
                !TryReadLane(in _bulkheadRoomBHandle, _bulkheadCount, out NativeArray<int>.ReadOnly _bulkheadRoomB) ||
                !TryReadLane(in _bulkheadSealedHandle, _bulkheadCount, out NativeArray<byte>.ReadOnly _bulkheadSealed) ||
                !TryReadTelemetryRing(out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetryRing))
            {
                return false;
            }

            GasDynamicsMemoryAuditAccumulator accumulator = default;
            AccumulateAudit(RoomO2, nameof(RoomO2), ref accumulator);
            AccumulateAudit(RoomCO2, nameof(RoomCO2), ref accumulator);
            AccumulateAudit(RoomPressure, nameof(RoomPressure), ref accumulator);
            AccumulateAudit(_roomO2Back, nameof(_roomO2Back), ref accumulator);
            AccumulateAudit(_roomCO2Back, nameof(_roomCO2Back), ref accumulator);
            AccumulateAudit(_roomNitrogen, nameof(_roomNitrogen), ref accumulator);
            AccumulateAudit(_roomNitrogenBack, nameof(_roomNitrogenBack), ref accumulator);
            AccumulateAudit(_roomPressureBack, nameof(_roomPressureBack), ref accumulator);
            AccumulateAudit(_roomAmbientPressure, nameof(_roomAmbientPressure), ref accumulator);
            AccumulateAudit(_roomSubmerged01, nameof(_roomSubmerged01), ref accumulator);
            AccumulateAudit(_roomPlayerStress01, nameof(_roomPlayerStress01), ref accumulator);
            AccumulateAudit(_roomPlayerHeartRateBpm, nameof(_roomPlayerHeartRateBpm), ref accumulator);
            AccumulateAudit(_roomTemperatureCelsius, nameof(_roomTemperatureCelsius), ref accumulator);
            AccumulateAudit(_roomPlayerPresent, nameof(_roomPlayerPresent), ref accumulator);
            AccumulateAudit(_roomScrubberPowered, nameof(_roomScrubberPowered), ref accumulator);
            AccumulateAudit(_roomFlags, nameof(_roomFlags), ref accumulator);
            AccumulateAudit(_roomBaseIndex, nameof(_roomBaseIndex), ref accumulator);
            AccumulateAudit(BaseAwakeState, nameof(BaseAwakeState), ref accumulator);
            AccumulateAudit(_basePlayerInside, nameof(_basePlayerInside), ref accumulator);
            AccumulateAudit(_basePlayerInsideCount, nameof(_basePlayerInsideCount), ref accumulator);
            AccumulateAudit(_baseRoomStart, nameof(_baseRoomStart), ref accumulator);
            AccumulateAudit(_baseRoomCount, nameof(_baseRoomCount), ref accumulator);
            AccumulateAudit(_baseCenterAup, nameof(_baseCenterAup), ref accumulator);
            AccumulateAudit(_baseHibernatedUnscaledTime, nameof(_baseHibernatedUnscaledTime), ref accumulator);
            AccumulateAudit(_baseBatteryWattSeconds, nameof(_baseBatteryWattSeconds), ref accumulator);
            AccumulateAudit(_baseIdleDrawWatts, nameof(_baseIdleDrawWatts), ref accumulator);
            AccumulateAudit(_baseLeakRatePerSecond, nameof(_baseLeakRatePerSecond), ref accumulator);
            AccumulateAudit(_baseAmbientOxygenKPa, nameof(_baseAmbientOxygenKPa), ref accumulator);
            AccumulateAudit(_bulkheadRoomA, nameof(_bulkheadRoomA), ref accumulator);
            AccumulateAudit(_bulkheadRoomB, nameof(_bulkheadRoomB), ref accumulator);
            AccumulateAudit(_bulkheadSealed, nameof(_bulkheadSealed), ref accumulator);
            AccumulateAudit(
                (long)UnsafeUtility.SizeOf<AtmosphereTelemetryEntry>() * telemetryRing.Length,
                nameof(_telemetryRingHandle),
                ref accumulator);

            audit = new GasDynamicsNativeMemoryAudit(
                RoomO2.Length,
                _bulkheadCapacityLimit,
                accumulator.AllocationCount,
                accumulator.RegisteredBytes,
                accumulator.LargestAllocationBytes,
                accumulator.LargestAllocationLabelHash,
                NativeMemorySentinel.ActiveAllocationCount,
                NativeMemorySentinel.TrackedBytes);
            return true;
        }

        public float ResolveEffectiveDepthStress01(int roomId, float depthStress01)
        {
            depthStress01 = FiniteSaturate01(depthStress01);
            if (roomId < 0 ||
                roomId >= _roomCount ||
                !TryReadLane(in _roomPressureHandle, roomId + 1, out NativeArray<float>.ReadOnly RoomPressure))
            {
                return depthStress01;
            }

            float pressureAtm = FiniteNonNegativeOrZero(RoomPressure[roomId]) * math.rcp(KPaPerAtmosphere);
            float relief01 = math.saturate((pressureAtm - 1f) * hullStressReliefPerAtm);
            return math.saturate(depthStress01 * (1f - relief01 * maxHullStressRelief01));
        }

        private void TryRegisterRegistry()
        {
            if (_registeredRegistry)
                return;

            GlobalRegistry.RegisterGasDynamicsSolver(this);
            _registeredRegistry = ReferenceEquals(GlobalRegistry.GasDynamics, this);
        }

        private void TryUnregisterRegistry()
        {
            if (!_registeredRegistry)
                return;

            if (ReferenceEquals(GlobalRegistry.GasDynamics, this))
                GlobalRegistry.UnregisterGasDynamicsSolver(this);
            _registeredRegistry = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                case GlobalRegistryServiceSlot.TickManager:
                    if (currentService == null || currentService is ITickDispatcher)
                        _tickDispatcher = currentService as ITickDispatcher;
                    _registeredTicks = false;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterTicks();
                    break;
                case GlobalRegistryServiceSlot.PlayerMovementContracts:
                    _playerMovementContracts = currentService as IPlayerMovementContracts;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault nextVault = currentService as IDataVault;
                    if (!ReferenceEquals(_dataVault, nextVault))
                    {
                        DisposeNativeStateDeferred();
                        _dataVault = nextVault;
                    }

                    if (nextVault != null && isActiveAndEnabled)
                    {
                        EnsureNativeState();
                        SeedStandardAtmosphereIfNeeded();
                    }
                    break;
            }
        }

        private void TryRegisterTicks()
        {
            if (_registeredTicks)
                return;

            bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            bool postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            bool frostRegistered = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);
            if (!fixedRegistered || !postFixedRegistered || !frostRegistered)
            {
                if (fixedRegistered)
                    GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                if (postFixedRegistered)
                    GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                if (frostRegistered)
                    GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
                return;
            }

            _registeredTicks = true;
        }

        private void TryUnregisterTicks()
        {
            if (!_registeredTicks)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
            _registeredTicks = false;
        }

        private void EnsureNativeState()
        {
            if (IsInitialized)
                return;

            if (!AreDtoLayoutsValid())
                return;

            if (_roomO2Handle.BufferID != 0u)
                DisposeNativeStateDeferred();

            int safeRoomCapacity = math.clamp(roomCapacity, 1, MaxRoomCapacity);
            int safeBulkheadCapacity = math.clamp(bulkheadCapacity, 0, MaxBulkheadCapacity);
            int safeBaseCapacity = math.clamp(baseCapacity, 1, MaxBaseCapacity);
            roomCapacity = safeRoomCapacity;
            bulkheadCapacity = safeBulkheadCapacity;
            baseCapacity = safeBaseCapacity;
            _roomCount = safeRoomCapacity;
            _bulkheadCapacityLimit = safeBulkheadCapacity;
            _bulkheadCount = 0;
            _baseCapacityLimit = safeBaseCapacity;
            _baseCount = 1;
            _sleepingBaseCount = 0;

            if (!TryEnsureGasStateHandles(safeRoomCapacity, safeBaseCapacity, safeBulkheadCapacity) ||
                !TryEnsureTelemetryRing() ||
                !TryAcquireStateWriteLocks(safeRoomCapacity, safeBaseCapacity, math.max(1, safeBulkheadCapacity)))
            {
                DisposeNativeStateDeferred();
                _roomCount = 0;
                _baseCount = 0;
                _sleepingBaseCount = 0;
                _bulkheadCount = 0;
                return;
            }

            try
            {
                _deferredBaseTransitionCount = 0;
                InitializeRoomSlots(safeRoomCapacity);
                InitializeBulkheadSlots(math.max(1, safeBulkheadCapacity));
                InitializeBaseSlots(safeBaseCapacity, safeRoomCapacity);
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        private void CacheColdDependencies()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _playerMovementContracts = GlobalRegistry.PlayerMovementContracts;
            _dataVault = GlobalRegistry.DataVault;
        }

        private static void ConfigureColdSignalLanes()
        {
            SignalBus<ToxicityExposureSignal>.Configure(
                ToxicityExposureSignal.ExpectedCapacity,
                ToxicityExposureSignal.MaxFrameSignals,
                ToxicityExposureSignal.LowTierFrameSignals,
                ToxicityExposureSignal.LaneHash);
            SignalBus<ToxicityExposureSignal>.EnsureInitialized();
        }

        private static bool AreDtoLayoutsValid()
        {
            return UnsafeUtility.SizeOf<PendingBaseTransitionSignal>() == 64 &&
                   UnsafeUtility.SizeOf<AtmosphereTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<GasDynamicsNativeMemoryAudit>() == 48;
        }

        private bool TryEnsureGasStateHandles(int safeRoomCapacity, int safeBaseCapacity, int safeBulkheadCapacity)
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            const NativeArrayOptions coldStateOptions = NativeArrayOptions.UninitializedMemory;
            int bulkheadStorageCapacity = math.max(1, safeBulkheadCapacity);
            bool ok =
                TryEnsureLane(vault, RoomO2BufferId, safeRoomCapacity, coldStateOptions, out _roomO2Handle) &&
                TryEnsureLane(vault, RoomCO2BufferId, safeRoomCapacity, coldStateOptions, out _roomCO2Handle) &&
                TryEnsureLane(vault, RoomPressureBufferId, safeRoomCapacity, coldStateOptions, out _roomPressureHandle) &&
                TryEnsureLane(vault, RoomO2BackBufferId, safeRoomCapacity, coldStateOptions, out _roomO2BackHandle) &&
                TryEnsureLane(vault, RoomCO2BackBufferId, safeRoomCapacity, coldStateOptions, out _roomCO2BackHandle) &&
                TryEnsureLane(vault, RoomNitrogenBufferId, safeRoomCapacity, coldStateOptions, out _roomNitrogenHandle) &&
                TryEnsureLane(vault, RoomNitrogenBackBufferId, safeRoomCapacity, coldStateOptions, out _roomNitrogenBackHandle) &&
                TryEnsureLane(vault, RoomPressureBackBufferId, safeRoomCapacity, coldStateOptions, out _roomPressureBackHandle) &&
                TryEnsureLane(vault, RoomAmbientPressureBufferId, safeRoomCapacity, coldStateOptions, out _roomAmbientPressureHandle) &&
                TryEnsureLane(vault, RoomSubmerged01BufferId, safeRoomCapacity, coldStateOptions, out _roomSubmerged01Handle) &&
                TryEnsureLane(vault, RoomPlayerStress01BufferId, safeRoomCapacity, coldStateOptions, out _roomPlayerStress01Handle) &&
                TryEnsureLane(vault, RoomPlayerHeartRateBpmBufferId, safeRoomCapacity, coldStateOptions, out _roomPlayerHeartRateBpmHandle) &&
                TryEnsureLane(vault, RoomTemperatureCelsiusBufferId, safeRoomCapacity, coldStateOptions, out _roomTemperatureCelsiusHandle) &&
                TryEnsureLane(vault, RoomPlayerPresentBufferId, safeRoomCapacity, coldStateOptions, out _roomPlayerPresentHandle) &&
                TryEnsureLane(vault, RoomScrubberPoweredBufferId, safeRoomCapacity, coldStateOptions, out _roomScrubberPoweredHandle) &&
                TryEnsureLane(vault, RoomFlagsBufferId, safeRoomCapacity, coldStateOptions, out _roomFlagsHandle) &&
                TryEnsureLane(vault, RoomBaseIndexBufferId, safeRoomCapacity, coldStateOptions, out _roomBaseIndexHandle) &&
                TryEnsureLane(vault, BufferID.HabitatBaseAwakeState, safeBaseCapacity, coldStateOptions, out _baseAwakeStateHandle) &&
                TryEnsureLane(vault, BasePlayerInsideBufferId, safeBaseCapacity, coldStateOptions, out _basePlayerInsideHandle) &&
                TryEnsureLane(vault, BasePlayerInsideCountBufferId, safeBaseCapacity, coldStateOptions, out _basePlayerInsideCountHandle) &&
                TryEnsureLane(vault, BaseRoomStartBufferId, safeBaseCapacity, coldStateOptions, out _baseRoomStartHandle) &&
                TryEnsureLane(vault, BaseRoomCountBufferId, safeBaseCapacity, coldStateOptions, out _baseRoomCountHandle) &&
                TryEnsureLane(vault, BaseCenterAupBufferId, safeBaseCapacity, coldStateOptions, out _baseCenterAupHandle) &&
                TryEnsureLane(vault, BaseHibernatedUnscaledTimeBufferId, safeBaseCapacity, coldStateOptions, out _baseHibernatedUnscaledTimeHandle) &&
                TryEnsureLane(vault, BaseBatteryWattSecondsBufferId, safeBaseCapacity, coldStateOptions, out _baseBatteryWattSecondsHandle) &&
                TryEnsureLane(vault, BaseIdleDrawWattsBufferId, safeBaseCapacity, coldStateOptions, out _baseIdleDrawWattsHandle) &&
                TryEnsureLane(vault, BaseLeakRatePerSecondBufferId, safeBaseCapacity, coldStateOptions, out _baseLeakRatePerSecondHandle) &&
                TryEnsureLane(vault, BaseAmbientOxygenKPaBufferId, safeBaseCapacity, coldStateOptions, out _baseAmbientOxygenKPaHandle) &&
                TryEnsureLane(vault, BulkheadRoomABufferId, bulkheadStorageCapacity, coldStateOptions, out _bulkheadRoomAHandle) &&
                TryEnsureLane(vault, BulkheadRoomBBufferId, bulkheadStorageCapacity, coldStateOptions, out _bulkheadRoomBHandle) &&
                TryEnsureLane(vault, BulkheadSealedBufferId, bulkheadStorageCapacity, coldStateOptions, out _bulkheadSealedHandle);

            return ok;
        }

        private static bool TryEnsureLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            int safeLength = math.max(1, requiredLength);
            handle = vault.EnsureGenerationHandle<T>(bufferId, safeLength, OwnerSystemId, options);
            if (vault.IsCompactionFenceActive)
            {
                handle = default;
                return false;
            }

            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= safeLength;
        }

        private NativeArray<T> ResolveLane<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _stateWriteLockMask == 0u ||
                handle.BufferID == 0u ||
                !vault.TryReadHandle(in handle, out NativeArray<T> buffer) ||
                vault.IsCompactionFenceActive ||
                !buffer.IsCreated)
            {
                return default;
            }

            return buffer;
        }

        private bool TryReadLane<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            requiredLength = math.max(0, requiredLength);
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                handle.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryAcquireStateWriteLocks()
        {
            return TryAcquireStateWriteLocks(
                _roomCount,
                _baseCount,
                math.max(1, _bulkheadCapacityLimit));
        }

        private bool TryAcquireStateWriteLocks(int requiredRoomCount, int requiredBaseCount, int requiredBulkheadCount)
        {
            requiredRoomCount = math.max(0, requiredRoomCount);
            requiredBaseCount = math.max(0, requiredBaseCount);
            requiredBulkheadCount = math.max(1, requiredBulkheadCount);
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (_stateWriteLockMask != 0u)
            {
                if (requiredRoomCount > _stateWriteRequiredRoomCount ||
                    requiredBaseCount > _stateWriteRequiredBaseCount ||
                    requiredBulkheadCount > _stateWriteRequiredBulkheadCount)
                {
                    return false;
                }

                _stateWriteLockDepth++;
                _consecutiveStateWriteLockFailures = 0;
                return true;
            }

            _stateWriteRequiredRoomCount = requiredRoomCount;
            _stateWriteRequiredBaseCount = requiredBaseCount;
            _stateWriteRequiredBulkheadCount = requiredBulkheadCount;
            if (!TryAcquireLaneWriteLock(vault, in _roomO2Handle, LockRoomO2) ||
                !TryAcquireLaneWriteLock(vault, in _roomCO2Handle, LockRoomCO2) ||
                !TryAcquireLaneWriteLock(vault, in _roomPressureHandle, LockRoomPressure) ||
                !TryAcquireLaneWriteLock(vault, in _roomO2BackHandle, LockRoomO2Back) ||
                !TryAcquireLaneWriteLock(vault, in _roomCO2BackHandle, LockRoomCO2Back) ||
                !TryAcquireLaneWriteLock(vault, in _roomNitrogenHandle, LockRoomNitrogen) ||
                !TryAcquireLaneWriteLock(vault, in _roomNitrogenBackHandle, LockRoomNitrogenBack) ||
                !TryAcquireLaneWriteLock(vault, in _roomPressureBackHandle, LockRoomPressureBack) ||
                !TryAcquireLaneWriteLock(vault, in _roomAmbientPressureHandle, LockRoomAmbientPressure) ||
                !TryAcquireLaneWriteLock(vault, in _roomSubmerged01Handle, LockRoomSubmerged01) ||
                !TryAcquireLaneWriteLock(vault, in _roomPlayerStress01Handle, LockRoomPlayerStress01) ||
                !TryAcquireLaneWriteLock(vault, in _roomPlayerHeartRateBpmHandle, LockRoomPlayerHeartRateBpm) ||
                !TryAcquireLaneWriteLock(vault, in _roomTemperatureCelsiusHandle, LockRoomTemperatureCelsius) ||
                !TryAcquireLaneWriteLock(vault, in _roomPlayerPresentHandle, LockRoomPlayerPresent) ||
                !TryAcquireLaneWriteLock(vault, in _roomScrubberPoweredHandle, LockRoomScrubberPowered) ||
                !TryAcquireLaneWriteLock(vault, in _roomFlagsHandle, LockRoomFlags) ||
                !TryAcquireLaneWriteLock(vault, in _roomBaseIndexHandle, LockRoomBaseIndex) ||
                !TryAcquireLaneWriteLock(vault, in _baseAwakeStateHandle, LockBaseAwakeState) ||
                !TryAcquireLaneWriteLock(vault, in _basePlayerInsideHandle, LockBasePlayerInside) ||
                !TryAcquireLaneWriteLock(vault, in _basePlayerInsideCountHandle, LockBasePlayerInsideCount) ||
                !TryAcquireLaneWriteLock(vault, in _baseRoomStartHandle, LockBaseRoomStart) ||
                !TryAcquireLaneWriteLock(vault, in _baseRoomCountHandle, LockBaseRoomCount) ||
                !TryAcquireLaneWriteLock(vault, in _baseCenterAupHandle, LockBaseCenterAup) ||
                !TryAcquireLaneWriteLock(vault, in _baseHibernatedUnscaledTimeHandle, LockBaseHibernatedUnscaledTime) ||
                !TryAcquireLaneWriteLock(vault, in _baseBatteryWattSecondsHandle, LockBaseBatteryWattSeconds) ||
                !TryAcquireLaneWriteLock(vault, in _baseIdleDrawWattsHandle, LockBaseIdleDrawWatts) ||
                !TryAcquireLaneWriteLock(vault, in _baseLeakRatePerSecondHandle, LockBaseLeakRatePerSecond) ||
                !TryAcquireLaneWriteLock(vault, in _baseAmbientOxygenKPaHandle, LockBaseAmbientOxygenKPa) ||
                !TryAcquireLaneWriteLock(vault, in _bulkheadRoomAHandle, LockBulkheadRoomA) ||
                !TryAcquireLaneWriteLock(vault, in _bulkheadRoomBHandle, LockBulkheadRoomB) ||
                !TryAcquireLaneWriteLock(vault, in _bulkheadSealedHandle, LockBulkheadSealed))
            {
                ReleaseStateWriteLocks();
                return false;
            }

            _stateWriteLockDepth = 1;
            _consecutiveStateWriteLockFailures = 0;
            return true;
        }

        private bool TryAcquireLaneWriteLock<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            uint mask) where T : struct
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                handle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out NativeArray<T> buffer))
            {
                return false;
            }

            int requiredLength = ResolveRequiredLengthForStateLock(mask);
            if (vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                vault.ReleaseWriteLock(in handle, OwnerSystemId);
                return false;
            }

            _stateWriteLockMask |= mask;
            return true;
        }

        private int ResolveRequiredLengthForStateLock(uint mask)
        {
            if (mask <= LockRoomBaseIndex)
                return _stateWriteRequiredRoomCount;

            if (mask <= LockBaseAmbientOxygenKPa)
                return _stateWriteRequiredBaseCount;

            return _stateWriteRequiredBulkheadCount;
        }

        private void ReleaseStateWriteLocks()
        {
            uint mask = _stateWriteLockMask;
            if (mask == 0u)
            {
                _stateWriteLockDepth = 0;
                _stateWriteRequiredRoomCount = 0;
                _stateWriteRequiredBaseCount = 0;
                _stateWriteRequiredBulkheadCount = 0;
                return;
            }

            if (_stateWriteLockDepth > 1)
            {
                _stateWriteLockDepth--;
                return;
            }

            _stateWriteLockDepth = 0;
            _stateWriteLockMask = 0u;
            _stateWriteRequiredRoomCount = 0;
            _stateWriteRequiredBaseCount = 0;
            _stateWriteRequiredBulkheadCount = 0;
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if ((mask & LockRoomO2) != 0u) vault.ReleaseWriteLock(in _roomO2Handle, OwnerSystemId);
            if ((mask & LockRoomCO2) != 0u) vault.ReleaseWriteLock(in _roomCO2Handle, OwnerSystemId);
            if ((mask & LockRoomPressure) != 0u) vault.ReleaseWriteLock(in _roomPressureHandle, OwnerSystemId);
            if ((mask & LockRoomO2Back) != 0u) vault.ReleaseWriteLock(in _roomO2BackHandle, OwnerSystemId);
            if ((mask & LockRoomCO2Back) != 0u) vault.ReleaseWriteLock(in _roomCO2BackHandle, OwnerSystemId);
            if ((mask & LockRoomNitrogen) != 0u) vault.ReleaseWriteLock(in _roomNitrogenHandle, OwnerSystemId);
            if ((mask & LockRoomNitrogenBack) != 0u) vault.ReleaseWriteLock(in _roomNitrogenBackHandle, OwnerSystemId);
            if ((mask & LockRoomPressureBack) != 0u) vault.ReleaseWriteLock(in _roomPressureBackHandle, OwnerSystemId);
            if ((mask & LockRoomAmbientPressure) != 0u) vault.ReleaseWriteLock(in _roomAmbientPressureHandle, OwnerSystemId);
            if ((mask & LockRoomSubmerged01) != 0u) vault.ReleaseWriteLock(in _roomSubmerged01Handle, OwnerSystemId);
            if ((mask & LockRoomPlayerStress01) != 0u) vault.ReleaseWriteLock(in _roomPlayerStress01Handle, OwnerSystemId);
            if ((mask & LockRoomPlayerHeartRateBpm) != 0u) vault.ReleaseWriteLock(in _roomPlayerHeartRateBpmHandle, OwnerSystemId);
            if ((mask & LockRoomTemperatureCelsius) != 0u) vault.ReleaseWriteLock(in _roomTemperatureCelsiusHandle, OwnerSystemId);
            if ((mask & LockRoomPlayerPresent) != 0u) vault.ReleaseWriteLock(in _roomPlayerPresentHandle, OwnerSystemId);
            if ((mask & LockRoomScrubberPowered) != 0u) vault.ReleaseWriteLock(in _roomScrubberPoweredHandle, OwnerSystemId);
            if ((mask & LockRoomFlags) != 0u) vault.ReleaseWriteLock(in _roomFlagsHandle, OwnerSystemId);
            if ((mask & LockRoomBaseIndex) != 0u) vault.ReleaseWriteLock(in _roomBaseIndexHandle, OwnerSystemId);
            if ((mask & LockBaseAwakeState) != 0u) vault.ReleaseWriteLock(in _baseAwakeStateHandle, OwnerSystemId);
            if ((mask & LockBasePlayerInside) != 0u) vault.ReleaseWriteLock(in _basePlayerInsideHandle, OwnerSystemId);
            if ((mask & LockBasePlayerInsideCount) != 0u) vault.ReleaseWriteLock(in _basePlayerInsideCountHandle, OwnerSystemId);
            if ((mask & LockBaseRoomStart) != 0u) vault.ReleaseWriteLock(in _baseRoomStartHandle, OwnerSystemId);
            if ((mask & LockBaseRoomCount) != 0u) vault.ReleaseWriteLock(in _baseRoomCountHandle, OwnerSystemId);
            if ((mask & LockBaseCenterAup) != 0u) vault.ReleaseWriteLock(in _baseCenterAupHandle, OwnerSystemId);
            if ((mask & LockBaseHibernatedUnscaledTime) != 0u) vault.ReleaseWriteLock(in _baseHibernatedUnscaledTimeHandle, OwnerSystemId);
            if ((mask & LockBaseBatteryWattSeconds) != 0u) vault.ReleaseWriteLock(in _baseBatteryWattSecondsHandle, OwnerSystemId);
            if ((mask & LockBaseIdleDrawWatts) != 0u) vault.ReleaseWriteLock(in _baseIdleDrawWattsHandle, OwnerSystemId);
            if ((mask & LockBaseLeakRatePerSecond) != 0u) vault.ReleaseWriteLock(in _baseLeakRatePerSecondHandle, OwnerSystemId);
            if ((mask & LockBaseAmbientOxygenKPa) != 0u) vault.ReleaseWriteLock(in _baseAmbientOxygenKPaHandle, OwnerSystemId);
            if ((mask & LockBulkheadRoomA) != 0u) vault.ReleaseWriteLock(in _bulkheadRoomAHandle, OwnerSystemId);
            if ((mask & LockBulkheadRoomB) != 0u) vault.ReleaseWriteLock(in _bulkheadRoomBHandle, OwnerSystemId);
            if ((mask & LockBulkheadSealed) != 0u) vault.ReleaseWriteLock(in _bulkheadSealedHandle, OwnerSystemId);
        }

        private bool TryEnsureTelemetryRing()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _telemetryRingHandle = default;
                return false;
            }

            VaultGenerationHandle<AtmosphereTelemetryEntry> handle = vault.EnsureGenerationHandle<AtmosphereTelemetryEntry>(
                BufferID.GasDynamicsTelemetryRing,
                TelemetryCapacity,
                SystemID.HabitatAtmosphere,
                NativeArrayOptions.ClearMemory);
            if (vault.IsCompactionFenceActive)
            {
                _telemetryRingHandle = default;
                return false;
            }

            if (handle.BufferID != unchecked((uint)(int)BufferID.GasDynamicsTelemetryRing) ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetryRing) ||
                vault.IsCompactionFenceActive ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length < TelemetryCapacity)
            {
                _telemetryRingHandle = default;
                return false;
            }

            _telemetryRingHandle = handle;
            return true;
        }

        private bool IsTelemetryRingReady()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   _telemetryRingHandle.BufferID == unchecked((uint)(int)BufferID.GasDynamicsTelemetryRing) &&
                   vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetryRing) &&
                   !vault.IsCompactionFenceActive &&
                   telemetryRing.IsCreated &&
                   telemetryRing.Length >= TelemetryCapacity;
        }

        private bool TryReadTelemetryRing(out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetryRing)
        {
            telemetryRing = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _telemetryRingHandle.BufferID != unchecked((uint)(int)BufferID.GasDynamicsTelemetryRing) ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out telemetryRing) ||
                vault.IsCompactionFenceActive ||
                !telemetryRing.IsCreated)
            {
                telemetryRing = default;
                return false;
            }

            return true;
        }

        private bool TryAcquireTelemetryRingForStep(out NativeArray<AtmosphereTelemetryEntry> telemetryRing)
        {
            telemetryRing = default;
            if (_telemetryRingLocked)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _telemetryRingHandle.BufferID != unchecked((uint)(int)BufferID.GasDynamicsTelemetryRing) ||
                !vault.TryAcquireWriteLock(in _telemetryRingHandle, SystemID.HabitatAtmosphere, out telemetryRing))
            {
                return false;
            }

            if (vault.IsCompactionFenceActive ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length < TelemetryCapacity)
            {
                vault.ReleaseWriteLock(in _telemetryRingHandle, SystemID.HabitatAtmosphere);
                telemetryRing = default;
                return false;
            }

            _telemetryRingLocked = true;
            return true;
        }

        private bool TryWriteFailureTelemetry(uint failedBufferId, ushort failureCode)
        {
            if (!TryAcquireTelemetryRingForStep(out NativeArray<AtmosphereTelemetryEntry> telemetryRing))
                return false;

            try
            {
                int telemetryLength = telemetryRing.Length;
                if (telemetryLength <= 0)
                    return false;

                int writeIndex = _telemetryWriteIndex % telemetryLength;
                _telemetryWriteIndex = (writeIndex + 1) % telemetryLength;
                telemetryRing[writeIndex] = new AtmosphereTelemetryEntry
                {
                    PackedOwner = ((ulong)_telemetryRingHandle.BufferID << 32) | _telemetryRingHandle.SystemID,
                    FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    RoomCount = _roomCount,
                    TotalO2KPa = 0f,
                    TotalCO2KPa = 0f,
                    TotalNitrogenKPa = 0f,
                    MaxPressureKPa = 0f,
                    StateHash = 0u,
                    BufferId = failedBufferId,
                    SystemId = _telemetryRingHandle.SystemID,
                    Generation = _telemetryRingHandle.Generation,
                    DroppedUpdates = 1,
                    CpuMicroseconds = 0f,
                    _pad0 = 0u,
                    Flags = failureCode,
                    Reserved = 0
                };
                return true;
            }
            finally
            {
                ReleaseTelemetryRingStepLock();
            }
        }

        private bool FailStateWriteLock(uint failedBufferId)
        {
            RecordStateWriteLockFailure(failedBufferId);
            return false;
        }

        private void RecordStateWriteLockFailure(uint failedBufferId)
        {
            if (_consecutiveStateWriteLockFailures < byte.MaxValue)
                _consecutiveStateWriteLockFailures++;

            TryWriteFailureTelemetry(failedBufferId, TelemetryFailureStateWriteLock);

            if (_consecutiveStateWriteLockFailures >= ConsecutiveStateWriteLockFailureDumpThreshold)
                DumpBlackBoxOnce();
        }

        private void ReleaseTelemetryRingStepLock()
        {
            if (!_telemetryRingLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null &&
                _telemetryRingHandle.BufferID == unchecked((uint)(int)BufferID.GasDynamicsTelemetryRing))
            {
                vault.ReleaseWriteLock(in _telemetryRingHandle, SystemID.HabitatAtmosphere);
            }

            _telemetryRingLocked = false;
        }

        private void InitializeRoomSlots(int safeRoomCapacity)
        {
            NativeArray<float> RoomO2 = ResolveRoomO2();
            NativeArray<float> RoomCO2 = ResolveRoomCO2();
            NativeArray<float> RoomPressure = ResolveRoomPressure();
            NativeArray<float> _roomO2Back = ResolveRoomO2Back();
            NativeArray<float> _roomCO2Back = ResolveRoomCO2Back();
            NativeArray<float> _roomNitrogen = ResolveRoomNitrogen();
            NativeArray<float> _roomNitrogenBack = ResolveRoomNitrogenBack();
            NativeArray<float> _roomPressureBack = ResolveRoomPressureBack();
            NativeArray<float> _roomAmbientPressure = ResolveRoomAmbientPressure();
            NativeArray<float> _roomSubmerged01 = ResolveRoomSubmerged01();
            NativeArray<float> _roomPlayerStress01 = ResolveRoomPlayerStress01();
            NativeArray<float> _roomPlayerHeartRateBpm = ResolveRoomPlayerHeartRateBpm();
            NativeArray<float> _roomTemperatureCelsius = ResolveRoomTemperatureCelsius();
            NativeArray<byte> _roomPlayerPresent = ResolveRoomPlayerPresent();
            NativeArray<byte> _roomScrubberPowered = ResolveRoomScrubberPowered();
            NativeArray<ushort> _roomFlags = ResolveRoomFlags();
            NativeArray<int> _roomBaseIndex = ResolveRoomBaseIndex();
            int roomLimit = math.min(math.max(0, safeRoomCapacity), RoomO2.Length);
            roomLimit = math.min(roomLimit, RoomCO2.Length);
            roomLimit = math.min(roomLimit, RoomPressure.Length);
            roomLimit = math.min(roomLimit, _roomO2Back.Length);
            roomLimit = math.min(roomLimit, _roomCO2Back.Length);
            roomLimit = math.min(roomLimit, _roomNitrogen.Length);
            roomLimit = math.min(roomLimit, _roomNitrogenBack.Length);
            roomLimit = math.min(roomLimit, _roomPressureBack.Length);
            roomLimit = math.min(roomLimit, _roomAmbientPressure.Length);
            roomLimit = math.min(roomLimit, _roomSubmerged01.Length);
            roomLimit = math.min(roomLimit, _roomPlayerStress01.Length);
            roomLimit = math.min(roomLimit, _roomPlayerHeartRateBpm.Length);
            roomLimit = math.min(roomLimit, _roomTemperatureCelsius.Length);
            roomLimit = math.min(roomLimit, _roomPlayerPresent.Length);
            roomLimit = math.min(roomLimit, _roomScrubberPowered.Length);
            roomLimit = math.min(roomLimit, _roomFlags.Length);
            roomLimit = math.min(roomLimit, _roomBaseIndex.Length);

            for (int room = 0; room < roomLimit; room++)
            {
                RoomO2[room] = 0f;
                RoomCO2[room] = 0f;
                RoomPressure[room] = 0f;
                _roomO2Back[room] = 0f;
                _roomCO2Back[room] = 0f;
                _roomNitrogen[room] = 0f;
                _roomNitrogenBack[room] = 0f;
                _roomPressureBack[room] = 0f;
                _roomAmbientPressure[room] = 0f;
                _roomSubmerged01[room] = 0f;
                _roomPlayerStress01[room] = 0f;
                _roomPlayerHeartRateBpm[room] = 0f;
                _roomTemperatureCelsius[room] = DefaultRoomTemperatureCelsius;
                _roomPlayerPresent[room] = 0;
                _roomScrubberPowered[room] = 0;
                _roomFlags[room] = 0;
                _roomBaseIndex[room] = 0;
            }
        }

        private void InitializeBulkheadSlots(int safeBulkheadCapacity)
        {
            NativeArray<int> _bulkheadRoomA = ResolveBulkheadRoomA();
            NativeArray<int> _bulkheadRoomB = ResolveBulkheadRoomB();
            NativeArray<byte> _bulkheadSealed = ResolveBulkheadSealed();
            int bulkheadLimit = math.min(
                math.max(0, safeBulkheadCapacity),
                math.min(_bulkheadRoomA.Length, math.min(_bulkheadRoomB.Length, _bulkheadSealed.Length)));

            for (int edge = 0; edge < bulkheadLimit; edge++)
            {
                _bulkheadRoomA[edge] = 0;
                _bulkheadRoomB[edge] = 0;
                _bulkheadSealed[edge] = 1;
            }
        }

        private void InitializeBaseSlots(int safeBaseCapacity, int safeRoomCapacity)
        {
            NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
            NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
            NativeArray<int> _basePlayerInsideCount = ResolveBasePlayerInsideCount();
            NativeArray<double> _baseHibernatedUnscaledTime = ResolveBaseHibernatedUnscaledTime();
            AbsoluteUniversePosition defaultCenterAup = ResolveDefaultBaseCenterAup();
            float safeDefaultBattery = FiniteNonNegativeOrZero(defaultBaseBatteryWattSeconds);
            float safeDefaultIdleDraw = FiniteNonNegativeOrZero(defaultBaseIdleDrawWatts);
            float safeDefaultLeakRate = FiniteNonNegativeOrZero(hibernationLeakRatePerSecond);
            float safeAmbientOxygen = FiniteNonNegativeOrZero(hibernationAmbientOxygenKPa);

            for (int baseId = 0; baseId < safeBaseCapacity; baseId++)
            {
                int roomCountForBase = baseId == 0 ? safeRoomCapacity : 0;
                ConfigureBaseSlot(
                    baseId,
                    0,
                    roomCountForBase,
                    in defaultCenterAup,
                    safeDefaultBattery,
                    safeDefaultIdleDraw,
                    safeDefaultLeakRate,
                    safeAmbientOxygen);
                BaseAwakeState[baseId] = (byte)(baseId == 0 ? 1 : 0);
                _basePlayerInside[baseId] = 0;
                _basePlayerInsideCount[baseId] = 0;
                _baseHibernatedUnscaledTime[baseId] = 0d;
            }
        }

        private void ConfigureBaseSlot(
            int baseId,
            int roomStart,
            int roomCount,
            in AbsoluteUniversePosition centerAup,
            float batteryWattSeconds,
            float idleDrawWatts,
            float leakRatePerSecond,
            float ambientOxygenKPa)
        {
            NativeArray<int> _baseRoomStart = ResolveBaseRoomStart();
            NativeArray<int> _baseRoomCount = ResolveBaseRoomCount();
            NativeArray<AbsoluteUniversePosition> _baseCenterAup = ResolveBaseCenterAup();
            NativeArray<float> _baseBatteryWattSeconds = ResolveBaseBatteryWattSeconds();
            NativeArray<float> _baseIdleDrawWatts = ResolveBaseIdleDrawWatts();
            NativeArray<float> _baseLeakRatePerSecond = ResolveBaseLeakRatePerSecond();
            NativeArray<float> _baseAmbientOxygenKPa = ResolveBaseAmbientOxygenKPa();
            _baseRoomStart[baseId] = roomStart;
            _baseRoomCount[baseId] = roomCount;
            _baseCenterAup[baseId] = centerAup;
            _baseBatteryWattSeconds[baseId] = FiniteNonNegativeOrZero(batteryWattSeconds);
            _baseIdleDrawWatts[baseId] = FiniteNonNegativeOrZero(idleDrawWatts);
            _baseLeakRatePerSecond[baseId] = FiniteNonNegativeOrZero(leakRatePerSecond);
            _baseAmbientOxygenKPa[baseId] = FiniteNonNegativeOrZero(ambientOxygenKPa);
        }

        private void SeedStandardAtmosphereIfNeeded()
        {
            if (!seedStandardAtmosphereOnEnable ||
                _seededStandardAtmosphere ||
                !TryReadLane(in _roomO2Handle, _roomCount, out NativeArray<float>.ReadOnly _))
            {
                return;
            }

            if (!TryAcquireStateWriteLocks())
            {
                RecordStateWriteLockFailure(0u);
                return;
            }

            try
            {
                NativeArray<float> RoomO2 = ResolveRoomO2();
                NativeArray<float> RoomCO2 = ResolveRoomCO2();
                NativeArray<float> RoomPressure = ResolveRoomPressure();
                NativeArray<float> _roomO2Back = ResolveRoomO2Back();
                NativeArray<float> _roomCO2Back = ResolveRoomCO2Back();
                NativeArray<float> _roomNitrogen = ResolveRoomNitrogen();
                NativeArray<float> _roomNitrogenBack = ResolveRoomNitrogenBack();
                NativeArray<float> _roomPressureBack = ResolveRoomPressureBack();
                NativeArray<float> _roomAmbientPressure = ResolveRoomAmbientPressure();
                NativeArray<float> _roomSubmerged01 = ResolveRoomSubmerged01();
                NativeArray<float> _roomPlayerStress01 = ResolveRoomPlayerStress01();
                NativeArray<float> _roomPlayerHeartRateBpm = ResolveRoomPlayerHeartRateBpm();
                NativeArray<float> _roomTemperatureCelsius = ResolveRoomTemperatureCelsius();
                NativeArray<byte> _roomPlayerPresent = ResolveRoomPlayerPresent();
                NativeArray<byte> _roomScrubberPowered = ResolveRoomScrubberPowered();
                NativeArray<ushort> _roomFlags = ResolveRoomFlags();
                for (int i = 0; i < _roomCount; i++)
                {
                    RoomO2[i] = StandardOxygenKPa;
                    RoomCO2[i] = StandardCarbonDioxideKPa;
                    _roomNitrogen[i] = StandardNitrogenKPa;
                    RoomPressure[i] = ResolveDaltonPressureKPa(StandardOxygenKPa, StandardCarbonDioxideKPa, StandardNitrogenKPa);
                    _roomO2Back[i] = RoomO2[i];
                    _roomCO2Back[i] = RoomCO2[i];
                    _roomNitrogenBack[i] = _roomNitrogen[i];
                    _roomPressureBack[i] = RoomPressure[i];
                    _roomAmbientPressure[i] = RoomPressure[i];
                    _roomSubmerged01[i] = 0f;
                    _roomFlags[i] = 0;
                    _roomScrubberPowered[i] = 0;
                    _roomTemperatureCelsius[i] = DefaultRoomTemperatureCelsius;
                    _roomPlayerPresent[i] = 0;
                    _roomPlayerStress01[i] = 0f;
                    _roomPlayerHeartRateBpm[i] = 0f;
                }

                _seededStandardAtmosphere = true;
            }
            finally
            {
                ReleaseStateWriteLocks();
            }
        }

        private void ScheduleStep(float deltaTime)
        {
            if (_stepRunning || !IsInitialized)
                return;

            float co2Threshold = FiniteNonNegativeOrZero(co2ToxicityThresholdKPa);
            float co2Fatal = math.max(co2Threshold + 0.01f, FiniteNonNegativeOrZero(co2FatalKPa));
            float narcosisThreshold = math.max(1f, FiniteNonNegativeOrZero(narcosisThresholdAtm));
            float narcosisFull = math.max(narcosisThreshold + 0.01f, FiniteNonNegativeOrZero(narcosisFullAtm));
            if (!TryAcquireStateWriteLocks())
            {
                RecordStateWriteLockFailure(0u);
                return;
            }

            bool completed = false;
            try
            {
                if (!TryAcquireTelemetryRingForStep(out NativeArray<AtmosphereTelemetryEntry> telemetryRing))
                    return;

                try
                {
                    int telemetryLength = telemetryRing.Length;
                    int writeIndex = telemetryLength > 0 ? _telemetryWriteIndex % telemetryLength : 0;
                    _telemetryWriteIndex = telemetryLength > 0 ? (writeIndex + 1) % telemetryLength : 0;
                    NativeArray<float> RoomO2 = ResolveRoomO2();
                    NativeArray<float> RoomCO2 = ResolveRoomCO2();
                    NativeArray<float> _roomNitrogen = ResolveRoomNitrogen();
                    NativeArray<float> _roomO2Back = ResolveRoomO2Back();
                    NativeArray<float> _roomCO2Back = ResolveRoomCO2Back();
                    NativeArray<float> _roomNitrogenBack = ResolveRoomNitrogenBack();
                    NativeArray<float> _roomPressureBack = ResolveRoomPressureBack();
                    NativeArray<float> _roomAmbientPressure = ResolveRoomAmbientPressure();
                    NativeArray<float> _roomSubmerged01 = ResolveRoomSubmerged01();
                    NativeArray<float> _roomPlayerStress01 = ResolveRoomPlayerStress01();
                    NativeArray<float> _roomPlayerHeartRateBpm = ResolveRoomPlayerHeartRateBpm();
                    NativeArray<float> _roomTemperatureCelsius = ResolveRoomTemperatureCelsius();
                    NativeArray<byte> _roomPlayerPresent = ResolveRoomPlayerPresent();
                    NativeArray<byte> _roomScrubberPowered = ResolveRoomScrubberPowered();
                    NativeArray<ushort> _roomFlags = ResolveRoomFlags();
                    NativeArray<int> _roomBaseIndex = ResolveRoomBaseIndex();
                    NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
                    NativeArray<int> _bulkheadRoomA = ResolveBulkheadRoomA();
                    NativeArray<int> _bulkheadRoomB = ResolveBulkheadRoomB();
                    NativeArray<byte> _bulkheadSealed = ResolveBulkheadSealed();
                    GasDynamicsStepJob job = new GasDynamicsStepJob
                    {
                        DeltaTime = math.max(0f, deltaTime),
                        RoomCount = _roomCount,
                        BulkheadCount = _bulkheadCount,
                        FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                        PlayerO2KPaPerSecond = FiniteNonNegativeOrZero(playerOxygenKPaPerSecond),
                        PlayerCO2KPaPerSecond = FiniteNonNegativeOrZero(playerCarbonDioxideKPaPerSecond),
                        FireO2KPaPerSecond = FiniteNonNegativeOrZero(fireOxygenDrainKPaPerSecond),
                        ScrubberKPaPerSecond = FiniteNonNegativeOrZero(scrubberKPaPerSecond),
                        DiffusionConductancePerSecond = FiniteNonNegativeOrZero(diffusionConductancePerSecond),
                        Co2ToxicityThresholdKPa = co2Threshold,
                        Co2FatalKPa = co2Fatal,
                        NarcosisThresholdAtm = narcosisThreshold,
                        NarcosisFullAtm = narcosisFull,
                        TelemetryWriteIndex = writeIndex,
                        TelemetryBufferId = _telemetryRingHandle.BufferID,
                        TelemetrySystemId = _telemetryRingHandle.SystemID,
                        TelemetryGeneration = _telemetryRingHandle.Generation,
                        RoomO2Front = RoomO2,
                        RoomCO2Front = RoomCO2,
                        RoomNitrogenFront = _roomNitrogen,
                        RoomO2Back = _roomO2Back,
                        RoomCO2Back = _roomCO2Back,
                        RoomNitrogenBack = _roomNitrogenBack,
                        RoomPressureBack = _roomPressureBack,
                        RoomAmbientPressure = _roomAmbientPressure,
                        RoomSubmerged01 = _roomSubmerged01,
                        RoomPlayerStress01 = _roomPlayerStress01,
                        RoomPlayerHeartRateBpm = _roomPlayerHeartRateBpm,
                        RoomTemperatureCelsius = _roomTemperatureCelsius,
                        RoomPlayerPresent = _roomPlayerPresent,
                        RoomScrubberPowered = _roomScrubberPowered,
                        RoomFlags = _roomFlags,
                        RoomBaseIndex = _roomBaseIndex,
                        BaseAwakeState = BaseAwakeState,
                        BulkheadRoomA = _bulkheadRoomA,
                        BulkheadRoomB = _bulkheadRoomB,
                        BulkheadSealed = _bulkheadSealed,
                        TelemetryRing = telemetryRing
                    };

                    _stepRunning = true;
                    job.Run();
                    completed = true;
                }
                finally
                {
                    _stepRunning = false;
                    ReleaseTelemetryRingStepLock();
                }
            }
            finally
            {
                ReleaseStateWriteLocks();
            }

            if (!completed)
                return;

            Swap(ref _roomO2Handle, ref _roomO2BackHandle);
            Swap(ref _roomCO2Handle, ref _roomCO2BackHandle);
            Swap(ref _roomNitrogenHandle, ref _roomNitrogenBackHandle);
            Swap(ref _roomPressureHandle, ref _roomPressureBackHandle);
            _tickCount++;
            PublishActiveRoomUi();
            PublishActiveRoomToxicitySignal(deltaTime);
            CheckTelemetryForFault();
        }

        private bool TryCompleteStep()
        {
            return !_stepRunning;
        }

        private void DrainBaseTransitionSignals(bool allowWake)
        {
            if (_baseCapacityLimit <= 0)
                return;

            if (_stepRunning)
            {
                CaptureBaseTransitionSignalsForLater();
                return;
            }

            bool hasDeferred = _deferredBaseTransitionOverflow ||
                               _deferredBaseTransitionCount > 0;
            if (!hasDeferred &&
                SignalBus<PlayerBaseExitSignal>.SnapshotCount <= 0 &&
                SignalBus<PlayerBaseEnterSignal>.SnapshotCount <= 0)
            {
                return;
            }

            double now = ResolveUnscaledTimeSeconds();
            ApplyDeferredBaseTransitions(now, allowWake);
            while (SignalBus<PlayerBaseExitSignal>.TryConsumeFrame(out PlayerBaseExitSignal signal))
                ApplyBaseExitSignal(in signal);

            // Enter wins over exit for same-frame module-to-module trigger handoffs.
            while (SignalBus<PlayerBaseEnterSignal>.TryConsumeFrame(out PlayerBaseEnterSignal signal))
                ApplyBaseEnterSignal(in signal, now, allowWake);
        }

        private void DrainHullRepairedSignals()
        {
            if (_stepRunning)
            {
                CaptureHullRepairedSignalsForLater();
                return;
            }

            ApplyPendingHullRepairSignals();
            if (_roomCount <= 0 || !AreRoomStateLanesReady(_roomCount))
            {
                CaptureHullRepairedSignalsForLater();
                return;
            }

            while (SignalBus<HullRepairedSignal>.TryConsumeFrame(out HullRepairedSignal signal))
            {
                ApplyOrDeferHullRepairedSignal(in signal);
            }
        }

        private void CaptureHullRepairedSignalsForLater()
        {
            while (SignalBus<HullRepairedSignal>.TryConsumeFrame(out HullRepairedSignal signal))
                QueueHullRepairSignal(in signal);
        }

        private void ApplyOrDeferHullRepairedSignal(in HullRepairedSignal signal)
        {
            if ((signal.Flags & HullRepairedSignal.CompletedFlag) == 0)
                return;

            int roomId = signal.RoomId;
            if ((uint)roomId >= MaxRoomCapacity)
                return;

            if (roomId < _roomCount &&
                AreRoomStateLanesReady(roomId + 1) &&
                TrySetRoomFlags(roomId, 0, RoomFlagBreached))
            {
                return;
            }

            SetPendingHullRepairRoom(roomId);
        }

        private void QueueHullRepairSignal(in HullRepairedSignal signal)
        {
            if ((signal.Flags & HullRepairedSignal.CompletedFlag) == 0)
                return;

            int roomId = signal.RoomId;
            if ((uint)roomId < MaxRoomCapacity)
                SetPendingHullRepairRoom(roomId);
        }

        private void SetPendingHullRepairRoom(int roomId)
        {
            if (roomId < 64)
                _pendingHullRepairRoomsLo |= 1UL << roomId;
            else
                _pendingHullRepairRoomsHi |= 1UL << (roomId - 64);
        }

        private void ApplyPendingHullRepairSignals()
        {
            if ((_pendingHullRepairRoomsLo | _pendingHullRepairRoomsHi) == 0UL ||
                _roomCount <= 0 ||
                !AreRoomStateLanesReady(_roomCount))
            {
                return;
            }

            ulong lo = _pendingHullRepairRoomsLo;
            int lowLimit = math.min(_roomCount, 64);
            for (int roomId = 0; roomId < lowLimit; roomId++)
            {
                ulong bit = 1UL << roomId;
                if ((lo & bit) == 0UL)
                    continue;

                if (TrySetRoomFlags(roomId, 0, RoomFlagBreached))
                    lo &= ~bit;
            }

            ulong hi = _pendingHullRepairRoomsHi;
            int highLimit = math.min(_roomCount, MaxRoomCapacity) - 64;
            for (int offset = 0; offset < highLimit; offset++)
            {
                ulong bit = 1UL << offset;
                if ((hi & bit) == 0UL)
                    continue;

                if (TrySetRoomFlags(offset + 64, 0, RoomFlagBreached))
                    hi &= ~bit;
            }

            _pendingHullRepairRoomsLo = lo;
            _pendingHullRepairRoomsHi = hi;
        }

        private void CaptureBaseTransitionSignalsForLater()
        {
            while (SignalBus<PlayerBaseExitSignal>.TryConsumeFrame(out PlayerBaseExitSignal signal))
                EnqueueDeferredBaseTransition(in signal, isEnter: false);

            // Keep the existing same-frame rule: exit packets are staged before enter packets.
            while (SignalBus<PlayerBaseEnterSignal>.TryConsumeFrame(out PlayerBaseEnterSignal signal))
                EnqueueDeferredBaseTransition(in signal, isEnter: true);
        }

        private void EnqueueDeferredBaseTransition(in PlayerBaseExitSignal signal, bool isEnter)
        {
            if (_deferredBaseTransitionCount >= _deferredBaseTransitions.Length)
            {
                _deferredBaseTransitionOverflow = true;
                return;
            }

            _deferredBaseTransitions[_deferredBaseTransitionCount++] = new PendingBaseTransitionSignal
            {
                BaseCenterAup = signal.BaseCenterAup,
                BaseId = signal.BaseId,
                RoomId = signal.RoomId,
                Flags = signal.Flags,
                IsEnter = (byte)(isEnter ? 1 : 0)
            };
        }

        private void EnqueueDeferredBaseTransition(in PlayerBaseEnterSignal signal, bool isEnter)
        {
            if (_deferredBaseTransitionCount >= _deferredBaseTransitions.Length)
            {
                _deferredBaseTransitionOverflow = true;
                return;
            }

            _deferredBaseTransitions[_deferredBaseTransitionCount++] = new PendingBaseTransitionSignal
            {
                BaseCenterAup = signal.BaseCenterAup,
                BaseId = signal.BaseId,
                RoomId = signal.RoomId,
                Flags = signal.Flags,
                IsEnter = (byte)(isEnter ? 1 : 0)
            };
        }

        private void ApplyDeferredBaseTransitions(double now, bool allowWake)
        {
            if (_deferredBaseTransitions.Length <= 0)
            {
                _deferredBaseTransitionOverflow = false;
                return;
            }

            int deferredCount = math.min(_deferredBaseTransitionCount, _deferredBaseTransitions.Length);
            for (int i = 0; i < deferredCount; i++)
            {
                PendingBaseTransitionSignal signal = _deferredBaseTransitions[i];
                if (signal.IsEnter != 0)
                    ApplyBaseEnterSignal(in signal, now, allowWake);
                else
                    ApplyBaseExitSignal(in signal);
            }

            _deferredBaseTransitionCount = 0;
            if (!_deferredBaseTransitionOverflow)
                return;

            _deferredBaseTransitionOverflow = false;
            ApplyTransitionOverflowFailOpen(now, allowWake);
        }

        private void ApplyTransitionOverflowFailOpen(double now, bool allowWake)
        {
            if (!allowWake ||
                _baseCount <= 0 ||
                !AreBaseStateLanesReady(_baseCount))
            {
                return;
            }

            double safeNow = double.IsFinite(now) && now >= 0d ? now : 0d;
            NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
            if (!BaseAwakeState.IsCreated)
                return;

            _transitionOverflowAwakeUntil = Math.Max(_transitionOverflowAwakeUntil, safeNow + TransitionOverflowAwakeSeconds);
            for (int baseId = 0; baseId < _baseCount; baseId++)
            {
                if (BaseAwakeState[baseId] == 0)
                    WakeBase(baseId, safeNow);
            }
        }

        private void ApplyBaseExitSignal(in PendingBaseTransitionSignal signal)
        {
            AbsoluteUniversePosition centerAup = signal.BaseCenterAup;
            if (!TryEnsureBaseSlotFromSignal(in signal))
                return;

            NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
            NativeArray<int> _basePlayerInsideCount = ResolveBasePlayerInsideCount();
            NativeArray<AbsoluteUniversePosition> _baseCenterAup = ResolveBaseCenterAup();
            int insideCount = math.max(0, _basePlayerInsideCount[signal.BaseId] - 1);
            _basePlayerInsideCount[signal.BaseId] = insideCount;
            _basePlayerInside[signal.BaseId] = (byte)(insideCount > 0 ? 1 : 0);
            _baseCenterAup[signal.BaseId] = centerAup;
        }

        private void ApplyBaseExitSignal(in PlayerBaseExitSignal signal)
        {
            PendingBaseTransitionSignal deferredSignal = new PendingBaseTransitionSignal
            {
                BaseCenterAup = signal.BaseCenterAup,
                BaseId = signal.BaseId,
                RoomId = signal.RoomId,
                Flags = signal.Flags,
                IsEnter = 0
            };
            ApplyBaseExitSignal(in deferredSignal);
        }

        private void ApplyBaseEnterSignal(in PendingBaseTransitionSignal signal, double now, bool allowWake)
        {
            AbsoluteUniversePosition centerAup = signal.BaseCenterAup;
            if (!TryEnsureBaseSlotFromSignal(in signal))
                return;

            NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
            NativeArray<int> _basePlayerInsideCount = ResolveBasePlayerInsideCount();
            NativeArray<AbsoluteUniversePosition> _baseCenterAup = ResolveBaseCenterAup();
            int insideCount = _basePlayerInsideCount[signal.BaseId];
            _basePlayerInsideCount[signal.BaseId] = insideCount < int.MaxValue ? insideCount + 1 : int.MaxValue;
            _basePlayerInside[signal.BaseId] = 1;
            _baseCenterAup[signal.BaseId] = centerAup;
            if (allowWake)
                WakeBase(signal.BaseId, now);
        }

        private void ApplyBaseEnterSignal(in PlayerBaseEnterSignal signal, double now, bool allowWake)
        {
            PendingBaseTransitionSignal deferredSignal = new PendingBaseTransitionSignal
            {
                BaseCenterAup = signal.BaseCenterAup,
                BaseId = signal.BaseId,
                RoomId = signal.RoomId,
                Flags = signal.Flags,
                IsEnter = 1
            };
            ApplyBaseEnterSignal(in deferredSignal, now, allowWake);
        }

        private void WakePlayerInsideSleepingBases(double now)
        {
            if (_stepRunning ||
                _baseCount <= 0 ||
                !AreBaseStateLanesReady(_baseCount))
            {
                return;
            }

            NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
            NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
            if (!BaseAwakeState.IsCreated || !_basePlayerInside.IsCreated)
                return;

            for (int baseId = 0; baseId < _baseCount; baseId++)
            {
                if (_basePlayerInside[baseId] != 0 && BaseAwakeState[baseId] == 0)
                    WakeBase(baseId, now);
            }
        }

        private bool TryEnsureBaseSlotFromSignal(int baseId, int roomId, in AbsoluteUniversePosition centerAup)
        {
            NativeArray<int> _roomBaseIndex = ResolveRoomBaseIndex();
            if (baseId < 0 ||
                baseId >= _baseCapacityLimit ||
                !IsFiniteAup(in centerAup) ||
                !_roomBaseIndex.IsCreated ||
                !AreBaseStateLanesReady(baseId + 1))
            {
                return false;
            }

            if (baseId >= _baseCount)
            {
                NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
                NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
                NativeArray<int> _basePlayerInsideCount = ResolveBasePlayerInsideCount();
                for (int i = _baseCount; i <= baseId; i++)
                {
                    ConfigureBaseSlot(
                        i,
                        0,
                        0,
                        in centerAup,
                        defaultBaseBatteryWattSeconds,
                        defaultBaseIdleDrawWatts,
                        hibernationLeakRatePerSecond,
                        hibernationAmbientOxygenKPa);
                    BaseAwakeState[i] = 1;
                    _basePlayerInside[i] = 0;
                    _basePlayerInsideCount[i] = 0;
                }

                _baseCount = baseId + 1;
            }

            if ((uint)roomId < (uint)_roomCount &&
                (uint)roomId < (uint)_roomBaseIndex.Length)
            {
                NativeArray<int> _baseRoomStart = ResolveBaseRoomStart();
                NativeArray<int> _baseRoomCount = ResolveBaseRoomCount();
                if (_baseRoomCount[baseId] <= 0)
                {
                    _baseRoomStart[baseId] = roomId;
                    _baseRoomCount[baseId] = 1;
                }

                _roomBaseIndex[roomId] = baseId;
            }

            return true;
        }

        private bool TryEnsureBaseSlotFromSignal(in PendingBaseTransitionSignal signal)
        {
            if ((signal.Flags & PlayerBaseEnterSignal.SanitizedBaseCenterFlag) != 0)
                return false;

            AbsoluteUniversePosition centerAup = signal.BaseCenterAup;
            return TryEnsureBaseSlotFromSignal(signal.BaseId, signal.RoomId, in centerAup);
        }

        private void ResolveBaseHibernationStates()
        {
            if (_baseCount <= 0 || !AreBaseStateLanesReady(_baseCount))
                return;

            NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
            NativeArray<byte> _basePlayerInside = ResolveBasePlayerInside();
            NativeArray<int> _baseRoomCount = ResolveBaseRoomCount();
            NativeArray<AbsoluteUniversePosition> _baseCenterAup = ResolveBaseCenterAup();
            if (!BaseAwakeState.IsCreated ||
                !_basePlayerInside.IsCreated ||
                !_baseRoomCount.IsCreated ||
                !_baseCenterAup.IsCreated)
            {
                return;
            }

            double now = ResolveUnscaledTimeSeconds();
            bool hasPlayerAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);
            float sleepDistance = ResolveHibernationDistanceMeters();
            float wakeDistance = math.max(0f, sleepDistance - math.max(3f, hibernationHysteresisMeters));
            double sleepDistanceSq = (double)sleepDistance * sleepDistance;
            double wakeDistanceSq = (double)wakeDistance * wakeDistance;
            bool transitionOverflowAwakeGuard = _transitionOverflowAwakeUntil > 0d &&
                                                double.IsFinite(_transitionOverflowAwakeUntil) &&
                                                now <= _transitionOverflowAwakeUntil;
            int sleepingCount = 0;

            for (int baseId = 0; baseId < _baseCount; baseId++)
            {
                bool awake = BaseAwakeState[baseId] != 0;
                bool playerInside = _basePlayerInside[baseId] != 0;
                bool hasRooms = _baseRoomCount[baseId] > 0;
                AbsoluteUniversePosition baseCenterAup = _baseCenterAup[baseId];
                bool baseCenterFinite = IsFiniteAup(in baseCenterAup);
                double distanceSq = hasPlayerAup && baseCenterFinite
                    ? AbsoluteUniversePosition.DistanceSq(in playerAup, in baseCenterAup)
                    : 0d;

                if (!baseCenterFinite)
                {
                    if (!awake && hasRooms)
                        WakeBase(baseId, now);

                    if (BaseAwakeState[baseId] == 0 && hasRooms)
                        sleepingCount++;
                    continue;
                }

                if (awake)
                {
                    if (!transitionOverflowAwakeGuard &&
                        hasRooms &&
                        !playerInside &&
                        hasPlayerAup &&
                        double.IsFinite(distanceSq) &&
                        distanceSq > sleepDistanceSq)
                    {
                        HibernateBase(baseId, now);
                    }
                }
                else
                {
                    bool playerNear = hasPlayerAup && double.IsFinite(distanceSq) && distanceSq <= wakeDistanceSq;
                    if (transitionOverflowAwakeGuard || playerInside || playerNear)
                        WakeBase(baseId, now);
                }

                if (BaseAwakeState[baseId] == 0 && hasRooms)
                    sleepingCount++;
            }

            _sleepingBaseCount = sleepingCount;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerMovementContracts movement = _playerMovementContracts;
            if (movement == null || !movement.TryGetRuntimePosition(out Vector3 runtimePosition))
                return false;

            return TryResolveAupFromRuntimeOrigin(runtimePosition, out playerAup);
        }

        private double ResolveUnscaledTimeSeconds()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null)
            {
                double unscaled = dispatcher.TimeSnapshot.UnscaledTime;
                if (double.IsFinite(unscaled) && unscaled >= 0d)
                    return unscaled;
            }

            double fallback = SystemDispatcher.CurrentUnscaledTimeSeconds;
            return double.IsFinite(fallback) && fallback >= 0d ? fallback : 0d;
        }

        private AbsoluteUniversePosition ResolveDefaultBaseCenterAup()
        {
            Vector3 position = transform.position;
            return TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition centerAup)
                ? centerAup
                : default;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector3(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private float ResolveHibernationDistanceMeters()
        {
            float normalDistance = math.max(1f, hibernationDistanceMeters);
            float legacyDistanceFloor = math.max(1f, lowTierHibernationDistanceMeters);
            return math.max(normalDistance, legacyDistanceFloor);
        }

        private bool AreRoomStateLanesReady(int requiredCount)
        {
            requiredCount = math.max(0, requiredCount);
            return TryReadLane(in _roomO2Handle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomCO2Handle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomPressureHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomO2BackHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomCO2BackHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomNitrogenHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomNitrogenBackHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomPressureBackHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomAmbientPressureHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomSubmerged01Handle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomPlayerStress01Handle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomPlayerHeartRateBpmHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomTemperatureCelsiusHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _roomPlayerPresentHandle, requiredCount, out NativeArray<byte>.ReadOnly _) &&
                   TryReadLane(in _roomScrubberPoweredHandle, requiredCount, out NativeArray<byte>.ReadOnly _) &&
                   TryReadLane(in _roomFlagsHandle, requiredCount, out NativeArray<ushort>.ReadOnly _) &&
                   TryReadLane(in _roomBaseIndexHandle, requiredCount, out NativeArray<int>.ReadOnly _);
        }

        private bool AreBulkheadLanesReady(int requiredCount)
        {
            requiredCount = math.max(0, requiredCount);
            return TryReadLane(in _bulkheadRoomAHandle, requiredCount, out NativeArray<int>.ReadOnly _) &&
                   TryReadLane(in _bulkheadRoomBHandle, requiredCount, out NativeArray<int>.ReadOnly _) &&
                   TryReadLane(in _bulkheadSealedHandle, requiredCount, out NativeArray<byte>.ReadOnly _);
        }

        private bool AreBaseStateLanesReady(int requiredCount)
        {
            requiredCount = math.max(0, requiredCount);
            return TryReadLane(in _baseAwakeStateHandle, requiredCount, out NativeArray<byte>.ReadOnly _) &&
                   TryReadLane(in _basePlayerInsideHandle, requiredCount, out NativeArray<byte>.ReadOnly _) &&
                   TryReadLane(in _basePlayerInsideCountHandle, requiredCount, out NativeArray<int>.ReadOnly _) &&
                   TryReadLane(in _baseRoomStartHandle, requiredCount, out NativeArray<int>.ReadOnly _) &&
                   TryReadLane(in _baseRoomCountHandle, requiredCount, out NativeArray<int>.ReadOnly _) &&
                   TryReadLane(in _baseCenterAupHandle, requiredCount, out NativeArray<AbsoluteUniversePosition>.ReadOnly _) &&
                   TryReadLane(in _baseHibernatedUnscaledTimeHandle, requiredCount, out NativeArray<double>.ReadOnly _) &&
                   TryReadLane(in _baseBatteryWattSecondsHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _baseIdleDrawWattsHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _baseLeakRatePerSecondHandle, requiredCount, out NativeArray<float>.ReadOnly _) &&
                   TryReadLane(in _baseAmbientOxygenKPaHandle, requiredCount, out NativeArray<float>.ReadOnly _);
        }

        private void HibernateBase(int baseId, double now)
        {
            NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
            if ((uint)baseId >= (uint)_baseCount ||
                !AreBaseStateLanesReady(baseId + 1) ||
                BaseAwakeState[baseId] == 0)
            {
                return;
            }

            NativeArray<double> _baseHibernatedUnscaledTime = ResolveBaseHibernatedUnscaledTime();
            BaseAwakeState[baseId] = 0;
            _baseHibernatedUnscaledTime[baseId] = double.IsFinite(now) && now >= 0d ? now : 0d;
        }

        private void WakeBase(int baseId, double now)
        {
            NativeArray<byte> BaseAwakeState = ResolveBaseAwakeState();
            if ((uint)baseId >= (uint)_baseCount ||
                !AreBaseStateLanesReady(baseId + 1) ||
                BaseAwakeState[baseId] != 0)
            {
                return;
            }

            NativeArray<double> _baseHibernatedUnscaledTime = ResolveBaseHibernatedUnscaledTime();
            double start = _baseHibernatedUnscaledTime[baseId];
            double elapsedDouble = double.IsFinite(now) && double.IsFinite(start) && now > start
                ? now - start
                : 0d;
            float elapsedSeconds = (float)math.min(MaxWakeCatchUpSeconds, math.max(0d, elapsedDouble));
            ApplyBaseWakeCatchUp(baseId, elapsedSeconds);
            BaseAwakeState[baseId] = 1;
            _baseHibernatedUnscaledTime[baseId] = double.IsFinite(now) && now >= 0d ? now : 0d;
        }

        private void ApplyBaseWakeCatchUp(int baseId, float elapsedSeconds)
        {
            NativeArray<float> RoomO2 = ResolveRoomO2();
            NativeArray<float> RoomCO2 = ResolveRoomCO2();
            NativeArray<float> _roomNitrogen = ResolveRoomNitrogen();
            NativeArray<float> RoomPressure = ResolveRoomPressure();
            NativeArray<float> _roomO2Back = ResolveRoomO2Back();
            NativeArray<float> _roomCO2Back = ResolveRoomCO2Back();
            NativeArray<float> _roomNitrogenBack = ResolveRoomNitrogenBack();
            NativeArray<float> _roomPressureBack = ResolveRoomPressureBack();
            NativeArray<int> _baseRoomStart = ResolveBaseRoomStart();
            NativeArray<int> _baseRoomCount = ResolveBaseRoomCount();
            NativeArray<float> _baseBatteryWattSeconds = ResolveBaseBatteryWattSeconds();
            NativeArray<float> _baseIdleDrawWatts = ResolveBaseIdleDrawWatts();
            NativeArray<float> _baseLeakRatePerSecond = ResolveBaseLeakRatePerSecond();
            NativeArray<float> _baseAmbientOxygenKPa = ResolveBaseAmbientOxygenKPa();
            if (elapsedSeconds <= 0f ||
                !RoomO2.IsCreated ||
                !RoomCO2.IsCreated ||
                !_roomNitrogen.IsCreated ||
                !RoomPressure.IsCreated ||
                !_roomO2Back.IsCreated ||
                !_roomCO2Back.IsCreated ||
                !_roomNitrogenBack.IsCreated ||
                !_roomPressureBack.IsCreated ||
                !_baseRoomStart.IsCreated ||
                !_baseRoomCount.IsCreated ||
                !_baseBatteryWattSeconds.IsCreated ||
                !_baseIdleDrawWatts.IsCreated ||
                !_baseLeakRatePerSecond.IsCreated ||
                !_baseAmbientOxygenKPa.IsCreated)
            {
                return;
            }

            BaseHibernationWakeCatchUpJob job = new BaseHibernationWakeCatchUpJob
            {
                BaseId = baseId,
                RoomCount = _roomCount,
                ElapsedSeconds = elapsedSeconds,
                RoomO2 = RoomO2,
                RoomCO2 = RoomCO2,
                RoomNitrogen = _roomNitrogen,
                RoomPressure = RoomPressure,
                RoomO2Back = _roomO2Back,
                RoomCO2Back = _roomCO2Back,
                RoomNitrogenBack = _roomNitrogenBack,
                RoomPressureBack = _roomPressureBack,
                BaseRoomStart = _baseRoomStart,
                BaseRoomCount = _baseRoomCount,
                BaseBatteryWattSeconds = _baseBatteryWattSeconds,
                BaseIdleDrawWatts = _baseIdleDrawWatts,
                BaseLeakRatePerSecond = _baseLeakRatePerSecond,
                BaseAmbientOxygenKPa = _baseAmbientOxygenKPa
            };

            job.Execute(); // COLD SYNC JOB: FrostTick wake catch-up, not a per-frame path.
        }

        private void PublishActiveRoomUi()
        {
            int roomId = _activePlayerRoom >= 0 ? _activePlayerRoom : 0;
            if (!TryGetRoomSnapshot(roomId, out GasRoomSnapshot snapshot))
                return;

            float invPressure = snapshot.PressureKPa > 0.001f ? math.rcp(snapshot.PressureKPa) : 0f;
            float oxygen01 = math.saturate(snapshot.OxygenKPa * invPressure);
            float time = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            UIStateStore.WriteValue(UIValueSlotId.RoomOxygen01, oxygen01, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomOxygenPartialKPa, snapshot.OxygenKPa, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomCarbonDioxidePartialKPa, snapshot.CarbonDioxideKPa, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomPressureKPa, snapshot.PressureKPa, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomNarcosis01, snapshot.Narcosis01, time);
        }

        private void PublishActiveRoomToxicitySignal(float deltaTime)
        {
            int roomId = _activePlayerRoom;
            if (roomId < 0 ||
                !TryGetRoomSnapshot(roomId, out GasRoomSnapshot snapshot))
            {
                return;
            }

            float toxicity01 = math.saturate(FiniteNonNegativeOrZero(snapshot.Toxicity01));
            float narcosis01 = math.saturate(FiniteNonNegativeOrZero(snapshot.Narcosis01));
            if (toxicity01 <= ToxicitySignalEpsilon && narcosis01 <= ToxicitySignalEpsilon)
                return;

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            ushort flags = (ushort)(
                math.select(0, ToxicityFlagCO2, toxicity01 > ToxicitySignalEpsilon) |
                math.select(0, ToxicityFlagNarcosis, narcosis01 > ToxicitySignalEpsilon));
            _latestToxicitySignal = new ToxicitySignal(
                roomId,
                snapshot.CarbonDioxideKPa,
                snapshot.PressureAtm,
                toxicity01,
                narcosis01,
                frame,
                flags);
            AdvanceToxicitySignalSequence();

            if (toxicity01 <= ToxicitySignalEpsilon ||
                !SignalBus<ToxicityExposureSignal>.HasNativeStorage ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                return;
            }

            ToxicityExposureSignal exposure = default;
            exposure.AUP = playerAup.ToAbsoluteDouble3();
            exposure.Exposure01 = toxicity01;
            exposure.ToxemiaDelta = math.saturate(toxicity01 * math.max(0f, deltaTime) * ToxicityExposureDeltaScalePerSecond);
            exposure.EntityId = PlayerTargetHash;
            exposure.ChemicalHash = GasCarbonDioxideChemicalHash;
            exposure.Frame = frame;
            exposure.Flags = 1;
            SignalBus<ToxicityExposureSignal>.TryPushTracked(in exposure, ref _toxicityExposureSignalDropCount);
        }

        private void AdvanceToxicitySignalSequence()
        {
            int next = _latestToxicitySignalSequence + 1;
            _latestToxicitySignalSequence = next != 0 ? next : 1;
        }

        private void CheckTelemetryForFault()
        {
            if (!TryReadTelemetryRing(out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetryRing))
                return;

            int telemetryLength = telemetryRing.Length;
            if (telemetryLength <= 0)
                return;

            int lastIndex = (_telemetryWriteIndex + telemetryLength - 1) % telemetryLength;
            AtmosphereTelemetryEntry entry = telemetryRing[lastIndex];
            if ((entry.Flags & TelemetryFlagNaN) != 0)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped ||
                !TryReadTelemetryRing(out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetryRing))
                return;

            _blackBoxDumped = true;
            try
            {
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", DumpFileName));
                using (System.IO.FileStream stream = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read))
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(DumpMagic);
                    writer.Write(DumpFormatVersion);
                    writer.Write(TelemetryEntrySizeBytes);
                    writer.Write(telemetryRing.Length);
                    writer.Write(_telemetryWriteIndex);
                    writer.Write(_tickCount);
                    for (int i = 0; i < telemetryRing.Length; i++)
                    {
                        AtmosphereTelemetryEntry entry = telemetryRing[i];
                        writer.Write(entry.PackedOwner);
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.RoomCount);
                        writer.Write(entry.TotalO2KPa);
                        writer.Write(entry.TotalCO2KPa);
                        writer.Write(entry.TotalNitrogenKPa);
                        writer.Write(entry.MaxPressureKPa);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.BufferId);
                        writer.Write(entry.SystemId);
                        writer.Write(entry.Generation);
                        writer.Write(entry.DroppedUpdates);
                        writer.Write(entry.CpuMicroseconds);
                        writer.Write(entry._pad0);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Reserved);
                    }
                }
            }
            catch (System.IO.IOException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(DumpMagic, 0u, 1u);
            }
            catch (System.UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(DumpMagic, 0u, 1u);
            }
            catch (System.ArgumentException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(DumpMagic, 0u, 1u);
            }
        }

        private bool TryFinalizeDeferredNativeDisposal()
        {
            return true;
        }

        private void DisposeNativeStateDeferred()
        {
            if (_roomO2Handle.BufferID == 0u &&
                _telemetryRingHandle.BufferID == 0u)
                return;

            _stepRunning = false;
            ReleaseTelemetryRingStepLock();
            ReleaseStateWriteLocks();
            ReleaseGasStateBuffers();
            ReleaseTelemetryRingBuffer();
            _deferredBaseTransitionCount = 0;

            _stepRunning = false;
            _seededStandardAtmosphere = false;
            _deferredBaseTransitionOverflow = false;
            _transitionOverflowAwakeUntil = 0d;
            _roomCount = 0;
            _bulkheadCapacityLimit = 0;
            _bulkheadCount = 0;
            _baseCapacityLimit = 0;
            _baseCount = 0;
            _sleepingBaseCount = 0;
            _activePlayerRoom = -1;
            _latestToxicitySignal = default;
            _latestToxicitySignalSequence = 0;
            _toxicitySignalReadSequence = 0;
            _toxicityExposureSignalDropCount = 0;
            _consecutiveStateWriteLockFailures = 0;
        }

        private void ReleaseGasStateBuffers()
        {
            ReleaseStateWriteLocks();
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseGasLane(vault, in _roomO2Handle);
                ReleaseGasLane(vault, in _roomCO2Handle);
                ReleaseGasLane(vault, in _roomPressureHandle);
                ReleaseGasLane(vault, in _roomO2BackHandle);
                ReleaseGasLane(vault, in _roomCO2BackHandle);
                ReleaseGasLane(vault, in _roomNitrogenHandle);
                ReleaseGasLane(vault, in _roomNitrogenBackHandle);
                ReleaseGasLane(vault, in _roomPressureBackHandle);
                ReleaseGasLane(vault, in _roomAmbientPressureHandle);
                ReleaseGasLane(vault, in _roomSubmerged01Handle);
                ReleaseGasLane(vault, in _roomPlayerStress01Handle);
                ReleaseGasLane(vault, in _roomPlayerHeartRateBpmHandle);
                ReleaseGasLane(vault, in _roomTemperatureCelsiusHandle);
                ReleaseGasLane(vault, in _roomPlayerPresentHandle);
                ReleaseGasLane(vault, in _roomScrubberPoweredHandle);
                ReleaseGasLane(vault, in _roomFlagsHandle);
                ReleaseGasLane(vault, in _roomBaseIndexHandle);
                ReleaseGasLane(vault, in _baseAwakeStateHandle);
                ReleaseGasLane(vault, in _basePlayerInsideHandle);
                ReleaseGasLane(vault, in _basePlayerInsideCountHandle);
                ReleaseGasLane(vault, in _baseRoomStartHandle);
                ReleaseGasLane(vault, in _baseRoomCountHandle);
                ReleaseGasLane(vault, in _baseCenterAupHandle);
                ReleaseGasLane(vault, in _baseHibernatedUnscaledTimeHandle);
                ReleaseGasLane(vault, in _baseBatteryWattSecondsHandle);
                ReleaseGasLane(vault, in _baseIdleDrawWattsHandle);
                ReleaseGasLane(vault, in _baseLeakRatePerSecondHandle);
                ReleaseGasLane(vault, in _baseAmbientOxygenKPaHandle);
                ReleaseGasLane(vault, in _bulkheadRoomAHandle);
                ReleaseGasLane(vault, in _bulkheadRoomBHandle);
                ReleaseGasLane(vault, in _bulkheadSealedHandle);
            }

            _roomO2Handle = default;
            _roomCO2Handle = default;
            _roomPressureHandle = default;
            _roomO2BackHandle = default;
            _roomCO2BackHandle = default;
            _roomNitrogenHandle = default;
            _roomNitrogenBackHandle = default;
            _roomPressureBackHandle = default;
            _roomAmbientPressureHandle = default;
            _roomSubmerged01Handle = default;
            _roomPlayerStress01Handle = default;
            _roomPlayerHeartRateBpmHandle = default;
            _roomTemperatureCelsiusHandle = default;
            _roomPlayerPresentHandle = default;
            _roomScrubberPoweredHandle = default;
            _roomFlagsHandle = default;
            _roomBaseIndexHandle = default;
            _baseAwakeStateHandle = default;
            _basePlayerInsideHandle = default;
            _basePlayerInsideCountHandle = default;
            _baseRoomStartHandle = default;
            _baseRoomCountHandle = default;
            _baseCenterAupHandle = default;
            _baseHibernatedUnscaledTimeHandle = default;
            _baseBatteryWattSecondsHandle = default;
            _baseIdleDrawWattsHandle = default;
            _baseLeakRatePerSecondHandle = default;
            _baseAmbientOxygenKPaHandle = default;
            _bulkheadRoomAHandle = default;
            _bulkheadRoomBHandle = default;
            _bulkheadSealedHandle = default;
        }

        private static void ReleaseGasLane<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);
        }

        private void ReleaseTelemetryRingBuffer()
        {
            ReleaseTelemetryRingStepLock();
            IDataVault vault = _dataVault;
            if (vault != null &&
                _telemetryRingHandle.BufferID == unchecked((uint)(int)BufferID.GasDynamicsTelemetryRing))
            {
                vault.ReleaseBuffer(in _telemetryRingHandle);
            }

            _telemetryRingHandle = default;
        }

        private static void AccumulateAudit<T>(
            NativeArray<T> array,
            string label,
            ref GasDynamicsMemoryAuditAccumulator accumulator) where T : struct
        {
            if (!array.IsCreated)
                return;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * array.Length;
            AccumulateAudit(bytes, label, ref accumulator);
        }

        private static void AccumulateAudit<T>(
            NativeArray<T>.ReadOnly array,
            string label,
            ref GasDynamicsMemoryAuditAccumulator accumulator) where T : struct
        {
            if (!array.IsCreated)
                return;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * array.Length;
            AccumulateAudit(bytes, label, ref accumulator);
        }

        private static void AccumulateAudit(
            long bytes,
            string label,
            ref GasDynamicsMemoryAuditAccumulator accumulator)
        {
            bytes = bytes > 0L ? bytes : 0L;
            accumulator.AllocationCount++;
            accumulator.RegisteredBytes += bytes;
            if (bytes <= accumulator.LargestAllocationBytes)
                return;

            accumulator.LargestAllocationBytes = bytes;
            accumulator.LargestAllocationLabelHash = NativeMemorySentinel.ComputeSnapshotHash(label);
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return MathLodApproximation.SaturateFinite(quality, AuthoritativeQualityWeight);
        }

        private float ResolveCadenceSeconds(float globalQualityWeight01)
        {
            float lowCadence = math.max(0.1f, lowTierColdTickSeconds);
            float midCadence = math.max(0.05f, midTierColdTickSeconds);
            float highCadence = math.max(0.02f, highTierColdTickSeconds);
            float q = Smooth01(math.saturate(math.isfinite(globalQualityWeight01) ? globalQualityWeight01 : AuthoritativeQualityWeight));
            float lowToMid = math.lerp(lowCadence, midCadence, q);
            return math.max(0.02f, math.lerp(lowToMid, highCadence, q));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDaltonPressureKPa(float oxygenKPa, float carbonDioxideKPa, float nitrogenKPa)
        {
            return FiniteNonNegativeOrZero(oxygenKPa) +
                   FiniteNonNegativeOrZero(carbonDioxideKPa) +
                   FiniteNonNegativeOrZero(nitrogenKPa);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveToxicity01(float carbonDioxideKPa, float thresholdKPa, float fatalKPa)
        {
            float range = math.max(0.01f, fatalKPa - thresholdKPa);
            return math.saturate((FiniteNonNegativeOrZero(carbonDioxideKPa) - thresholdKPa) * math.rcp(range));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveNarcosis01(float pressureAtm, float thresholdAtm, float fullAtm)
        {
            float range = math.max(0.01f, fullAtm - thresholdAtm);
            return math.saturate((FiniteNonNegativeOrZero(pressureAtm) - thresholdAtm) * math.rcp(range));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteSaturate01(float value)
        {
            return math.select(0f, math.saturate(value), math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.select(0f, math.max(0f, value), math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap(ref VaultGenerationHandle<float> first, ref VaultGenerationHandle<float> second)
        {
            VaultGenerationHandle<float> temp = first;
            first = second;
            second = temp;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct PendingBaseTransitionSignal
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition BaseCenterAup;
            [FieldOffset(48)]
            public int BaseId;
            [FieldOffset(52)]
            public int RoomId;
            [FieldOffset(56)]
            private uint _pad0;
            [FieldOffset(60)]
            public ushort Flags;
            [FieldOffset(62)]
            public byte IsEnter;
            [FieldOffset(63)]
            private byte _pad1;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct BaseHibernationWakeCatchUpJob : IJob
        {
            public int BaseId;
            public int RoomCount;
            public float ElapsedSeconds;

            [NoAlias] public NativeArray<float> RoomO2;
            [NoAlias] public NativeArray<float> RoomCO2;
            [NoAlias] public NativeArray<float> RoomNitrogen;
            [NoAlias] public NativeArray<float> RoomPressure;
            [NoAlias] public NativeArray<float> RoomO2Back;
            [NoAlias] public NativeArray<float> RoomCO2Back;
            [NoAlias] public NativeArray<float> RoomNitrogenBack;
            [NoAlias] public NativeArray<float> RoomPressureBack;

            [ReadOnly, NoAlias] public NativeArray<int> BaseRoomStart;
            [ReadOnly, NoAlias] public NativeArray<int> BaseRoomCount;
            [NoAlias] public NativeArray<float> BaseBatteryWattSeconds;
            [ReadOnly, NoAlias] public NativeArray<float> BaseIdleDrawWatts;
            [ReadOnly, NoAlias] public NativeArray<float> BaseLeakRatePerSecond;
            [ReadOnly, NoAlias] public NativeArray<float> BaseAmbientOxygenKPa;

            public void Execute()
            {
                int baseLimit = math.min(
                    BaseRoomStart.Length,
                    math.min(
                        BaseRoomCount.Length,
                        math.min(
                            BaseBatteryWattSeconds.Length,
                            math.min(BaseIdleDrawWatts.Length, math.min(BaseLeakRatePerSecond.Length, BaseAmbientOxygenKPa.Length)))));

                if ((uint)BaseId >= (uint)baseLimit || ElapsedSeconds <= 0f)
                    return;

                float elapsed = FiniteNonNegativeOrZero(ElapsedSeconds);
                float battery = FiniteNonNegativeOrZero(BaseBatteryWattSeconds[BaseId]);
                float idleDraw = FiniteNonNegativeOrZero(BaseIdleDrawWatts[BaseId]);
                battery = math.max(0f, battery - idleDraw * elapsed);
                BaseBatteryWattSeconds[BaseId] = battery;

                int roomLimit = math.min(
                    RoomCount,
                    math.min(
                        RoomO2.Length,
                        math.min(
                            RoomCO2.Length,
                            math.min(
                                RoomNitrogen.Length,
                                math.min(
                                    RoomPressure.Length,
                                    math.min(RoomO2Back.Length, math.min(RoomCO2Back.Length, math.min(RoomNitrogenBack.Length, RoomPressureBack.Length))))))));
                int startRoom = math.clamp(BaseRoomStart[BaseId], 0, math.max(0, roomLimit));
                int roomEnd = math.min(roomLimit, startRoom + math.max(0, BaseRoomCount[BaseId]));
                if (roomEnd <= startRoom)
                    return;

                float leakRate = FiniteNonNegativeOrZero(BaseLeakRatePerSecond[BaseId]);
                float alpha = ResolveAnalyticalLeakAlpha(elapsed, leakRate);
                float ambientOxygen = FiniteNonNegativeOrZero(BaseAmbientOxygenKPa[BaseId]);
                float batteryDead01 = math.select(0f, 1f, battery <= 0f);
                float leakActive01 = math.select(0f, 1f, leakRate > 0f & alpha > 0f) * (1f - batteryDead01);
                for (int room = startRoom; room < roomEnd; room++)
                {
                    float currentOxygen = FiniteNonNegativeOrZero(RoomO2[room]);
                    float leakedOxygen = math.lerp(currentOxygen, ambientOxygen, alpha);
                    float oxygen = math.select(currentOxygen, leakedOxygen, leakActive01 > 0f);
                    oxygen = math.select(oxygen, 0f, batteryDead01 > 0f);
                    float carbonDioxide = FiniteNonNegativeOrZero(RoomCO2[room]);
                    float nitrogen = FiniteNonNegativeOrZero(RoomNitrogen[room]);
                    float pressure = ResolveDaltonPressureKPa(oxygen, carbonDioxide, nitrogen);
                    RoomO2[room] = oxygen;
                    RoomCO2[room] = carbonDioxide;
                    RoomNitrogen[room] = nitrogen;
                    RoomO2Back[room] = oxygen;
                    RoomCO2Back[room] = carbonDioxide;
                    RoomNitrogenBack[room] = nitrogen;
                    RoomPressure[room] = pressure;
                    RoomPressureBack[room] = pressure;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveAnalyticalLeakAlpha(float elapsedSeconds, float leakRatePerSecond)
            {
                float x = FiniteNonNegativeOrZero(elapsedSeconds) * FiniteNonNegativeOrZero(leakRatePerSecond);
                float alpha = 1f - MathLodApproximation.ApproxExpNegPade33Wide40(x);
                return math.select(0f, math.saturate(alpha), math.isfinite(alpha));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveDaltonPressureKPa(float oxygenKPa, float carbonDioxideKPa, float nitrogenKPa)
            {
                return FiniteNonNegativeOrZero(oxygenKPa) +
                       FiniteNonNegativeOrZero(carbonDioxideKPa) +
                       FiniteNonNegativeOrZero(nitrogenKPa);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float FiniteNonNegativeOrZero(float value)
            {
                return math.select(0f, math.max(0f, value), math.isfinite(value));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct GasDynamicsStepJob : IJob
        {
            public float DeltaTime;
            public int RoomCount;
            public int BulkheadCount;
            public uint FrameIndex;
            public float PlayerO2KPaPerSecond;
            public float PlayerCO2KPaPerSecond;
            public float FireO2KPaPerSecond;
            public float ScrubberKPaPerSecond;
            public float DiffusionConductancePerSecond;
            public float Co2ToxicityThresholdKPa;
            public float Co2FatalKPa;
            public float NarcosisThresholdAtm;
            public float NarcosisFullAtm;
            public int TelemetryWriteIndex;
            public uint TelemetryBufferId;
            public uint TelemetrySystemId;
            public uint TelemetryGeneration;

            [ReadOnly, NoAlias] public NativeArray<float> RoomO2Front;
            [ReadOnly, NoAlias] public NativeArray<float> RoomCO2Front;
            [ReadOnly, NoAlias] public NativeArray<float> RoomNitrogenFront;
            [NoAlias] public NativeArray<float> RoomO2Back;
            [NoAlias] public NativeArray<float> RoomCO2Back;
            [NoAlias] public NativeArray<float> RoomNitrogenBack;
            [NoAlias] public NativeArray<float> RoomPressureBack;
            [ReadOnly, NoAlias] public NativeArray<float> RoomAmbientPressure;
            [ReadOnly, NoAlias] public NativeArray<float> RoomSubmerged01;
            [ReadOnly, NoAlias] public NativeArray<float> RoomPlayerStress01;
            [ReadOnly, NoAlias] public NativeArray<float> RoomPlayerHeartRateBpm;
            [ReadOnly, NoAlias] public NativeArray<float> RoomTemperatureCelsius;
            [ReadOnly, NoAlias] public NativeArray<byte> RoomPlayerPresent;
            [ReadOnly, NoAlias] public NativeArray<byte> RoomScrubberPowered;
            [ReadOnly, NoAlias] public NativeArray<ushort> RoomFlags;
            [ReadOnly, NoAlias] public NativeArray<int> RoomBaseIndex;
            [ReadOnly, NoAlias] public NativeArray<byte> BaseAwakeState;
            [ReadOnly, NoAlias] public NativeArray<int> BulkheadRoomA;
            [ReadOnly, NoAlias] public NativeArray<int> BulkheadRoomB;
            [ReadOnly, NoAlias] public NativeArray<byte> BulkheadSealed;
            [WriteOnly, NoAlias] public NativeArray<AtmosphereTelemetryEntry> TelemetryRing;

            public void Execute()
            {
                int roomLimit = math.max(0, RoomCount);
                roomLimit = math.min(roomLimit, RoomO2Front.Length);
                roomLimit = math.min(roomLimit, RoomCO2Front.Length);
                roomLimit = math.min(roomLimit, RoomNitrogenFront.Length);
                roomLimit = math.min(roomLimit, RoomO2Back.Length);
                roomLimit = math.min(roomLimit, RoomCO2Back.Length);
                roomLimit = math.min(roomLimit, RoomNitrogenBack.Length);
                roomLimit = math.min(roomLimit, RoomPressureBack.Length);
                roomLimit = math.min(roomLimit, RoomAmbientPressure.Length);
                roomLimit = math.min(roomLimit, RoomPlayerStress01.Length);
                roomLimit = math.min(roomLimit, RoomPlayerHeartRateBpm.Length);
                roomLimit = math.min(roomLimit, RoomPlayerPresent.Length);
                roomLimit = math.min(roomLimit, RoomScrubberPowered.Length);
                roomLimit = math.min(roomLimit, RoomFlags.Length);
                roomLimit = math.min(roomLimit, RoomTemperatureCelsius.Length);
                roomLimit = math.min(roomLimit, RoomSubmerged01.Length);
                roomLimit = math.min(roomLimit, RoomBaseIndex.Length);
                float dt = math.max(0f, DeltaTime);
                for (int room = 0; room < roomLimit; room++)
                {
                    float oxygen = FiniteNonNegativeOrZero(RoomO2Front[room]);
                    float carbonDioxide = FiniteNonNegativeOrZero(RoomCO2Front[room]);
                    float nitrogen = FiniteNonNegativeOrZero(RoomNitrogenFront[room]);
                    ushort flags = RoomFlags[room];
                    float awake01 = ReadRoomAwake01(room);
                    float present01 = math.select(0f, 1f, RoomPlayerPresent[room] != 0);
                    float stress = FiniteSaturate01(RoomPlayerStress01[room]);
                    float heartRate = FiniteNonNegativeOrZero(RoomPlayerHeartRateBpm[room]);
                    float heartRateMultiplier = math.select(
                        math.lerp(0.8f, 1.8f, math.saturate(heartRate)),
                        math.clamp(heartRate * math.rcp(70f), 0.5f, 3f),
                        heartRate > 2f);
                    float metabolicMultiplier = awake01 * present01 * (1f + stress) * heartRateMultiplier;
                    oxygen = math.max(0f, oxygen - PlayerO2KPaPerSecond * metabolicMultiplier * dt);
                    carbonDioxide += PlayerCO2KPaPerSecond * metabolicMultiplier * dt;

                    float fire01 = math.select(0f, 1f, (flags & RoomFlagInternalFire) != 0) * awake01;
                    float burnedOxygen = math.min(oxygen, FireO2KPaPerSecond * 5f * dt * fire01);
                    oxygen -= burnedOxygen;
                    carbonDioxide += burnedOxygen;

                    float roomTemperature = RoomTemperatureCelsius[room];
                    float scrubber01 = math.select(0f, 1f, RoomScrubberPowered[room] != 0) * awake01;
                    float scrubberScale = math.select(1f, FreezingScrubberEfficiencyScale, roomTemperature < 0f);
                    carbonDioxide = math.max(0f, carbonDioxide - ScrubberKPaPerSecond * scrubberScale * dt * scrubber01);

                    RoomO2Back[room] = oxygen;
                    RoomCO2Back[room] = carbonDioxide;
                    RoomNitrogenBack[room] = nitrogen;
                    RoomPressureBack[room] = ResolveDaltonPressureKPa(oxygen, carbonDioxide, nitrogen);
                }

                float diffusionFraction = math.min(
                    MaxDiffusionFractionPerStep,
                    math.saturate(DiffusionConductancePerSecond * dt));
                int bulkheadLimit = math.max(0, BulkheadCount);
                bulkheadLimit = math.min(bulkheadLimit, BulkheadRoomA.Length);
                bulkheadLimit = math.min(bulkheadLimit, BulkheadRoomB.Length);
                bulkheadLimit = math.min(bulkheadLimit, BulkheadSealed.Length);
                bulkheadLimit = math.select(0, bulkheadLimit, roomLimit > 0);
                float activeDiffusionBase = math.select(0f, diffusionFraction, diffusionFraction > 0f);
                for (int edge = 0; edge < bulkheadLimit; edge++)
                {
                    int roomA = BulkheadRoomA[edge];
                    int roomB = BulkheadRoomB[edge];
                    int clampedRoomA = math.clamp(roomA, 0, roomLimit - 1);
                    int clampedRoomB = math.clamp(roomB, 0, roomLimit - 1);
                    bool activeEdge =
                        (BulkheadSealed[edge] == 0) &
                        ((uint)roomA < (uint)roomLimit) &
                        ((uint)roomB < (uint)roomLimit) &
                        (roomA != roomB) &
                        (ReadRoomAwake01(clampedRoomA) > 0f) &
                        (ReadRoomAwake01(clampedRoomB) > 0f);
                    float activeDiffusionFraction = activeDiffusionBase * math.select(0f, 1f, activeEdge);

                    float oxygenA = RoomO2Back[clampedRoomA];
                    float oxygenB = RoomO2Back[clampedRoomB];
                    float carbonDioxideA = RoomCO2Back[clampedRoomA];
                    float carbonDioxideB = RoomCO2Back[clampedRoomB];
                    float nitrogenA = RoomNitrogenBack[clampedRoomA];
                    float nitrogenB = RoomNitrogenBack[clampedRoomB];

                    DiffuseGas(ref oxygenA, ref oxygenB, activeDiffusionFraction);
                    DiffuseGas(ref carbonDioxideA, ref carbonDioxideB, activeDiffusionFraction);
                    DiffuseGas(ref nitrogenA, ref nitrogenB, activeDiffusionFraction);

                    RoomO2Back[clampedRoomA] = oxygenA;
                    RoomO2Back[clampedRoomB] = oxygenB;
                    RoomCO2Back[clampedRoomA] = carbonDioxideA;
                    RoomCO2Back[clampedRoomB] = carbonDioxideB;
                    RoomNitrogenBack[clampedRoomA] = nitrogenA;
                    RoomNitrogenBack[clampedRoomB] = nitrogenB;
                    RoomPressureBack[clampedRoomA] = ResolveDaltonPressureKPa(oxygenA, carbonDioxideA, nitrogenA);
                    RoomPressureBack[clampedRoomB] = ResolveDaltonPressureKPa(oxygenB, carbonDioxideB, nitrogenB);
                }

                float totalOxygen = 0f;
                float totalCarbonDioxide = 0f;
                float totalNitrogen = 0f;
                float maxPressure = 0f;
                uint stateHash = 2166136261u;
                ushort telemetryFlags = 0;
                int sleepingRoomCount = 0;
                for (int room = 0; room < roomLimit; room++)
                {
                    ushort flags = RoomFlags[room];
                    float oxygen = RoomO2Back[room];
                    float carbonDioxide = RoomCO2Back[room];
                    float nitrogen = RoomNitrogenBack[room];
                    float awake01 = ReadRoomAwake01(room);
                    bool roomAwake = awake01 > 0f;
                    bool roomSleeping = !roomAwake;
                    sleepingRoomCount += math.select(0, 1, roomSleeping);
                    telemetryFlags |= (ushort)math.select(0, TelemetryFlagHibernating, roomSleeping);

                    bool breached = roomAwake & ((flags & RoomFlagBreached) != 0);
                    oxygen = math.select(oxygen, 0f, breached);
                    carbonDioxide = math.select(carbonDioxide, 0f, breached);
                    nitrogen = math.select(nitrogen, FiniteNonNegativeOrZero(RoomAmbientPressure[room]), breached);
                    telemetryFlags |= (ushort)math.select(0, TelemetryFlagBreach, breached);

                    float dryFraction01 = 1f - FiniteSaturate01(RoomSubmerged01[room]);
                    oxygen = math.select(oxygen, math.min(oxygen, StandardOxygenKPa * dryFraction01), roomAwake);

                    bool nanDetected = !math.isfinite(oxygen) | !math.isfinite(carbonDioxide) | !math.isfinite(nitrogen);
                    telemetryFlags |= (ushort)math.select(0, TelemetryFlagNaN, nanDetected);

                    oxygen = FiniteNonNegativeOrZero(oxygen);
                    carbonDioxide = FiniteNonNegativeOrZero(carbonDioxide);
                    nitrogen = FiniteNonNegativeOrZero(nitrogen);
                    float pressure = ResolveDaltonPressureKPa(oxygen, carbonDioxide, nitrogen);

                    RoomO2Back[room] = oxygen;
                    RoomCO2Back[room] = carbonDioxide;
                    RoomNitrogenBack[room] = nitrogen;
                    RoomPressureBack[room] = pressure;

                    totalOxygen += oxygen;
                    totalCarbonDioxide += carbonDioxide;
                    totalNitrogen += nitrogen;
                    maxPressure = math.max(maxPressure, pressure);
                    stateHash = HashFloat(stateHash, oxygen);
                    stateHash = HashFloat(stateHash, carbonDioxide);
                    stateHash = HashFloat(stateHash, nitrogen);
                    stateHash = HashFloat(stateHash, pressure);
                }

                if (!TelemetryRing.IsCreated || (uint)TelemetryWriteIndex >= (uint)TelemetryRing.Length)
                    return;

                TelemetryRing[TelemetryWriteIndex] = new AtmosphereTelemetryEntry
                {
                    PackedOwner = ((ulong)TelemetryBufferId << 32) | TelemetrySystemId,
                    FrameIndex = FrameIndex,
                    RoomCount = roomLimit,
                    TotalO2KPa = totalOxygen,
                    TotalCO2KPa = totalCarbonDioxide,
                    TotalNitrogenKPa = totalNitrogen,
                    MaxPressureKPa = maxPressure,
                    StateHash = stateHash,
                    BufferId = TelemetryBufferId,
                    SystemId = TelemetrySystemId,
                    Generation = TelemetryGeneration,
                    Flags = telemetryFlags,
                    Reserved = (ushort)math.min(ushort.MaxValue, sleepingRoomCount),
                    DroppedUpdates = 0,
                    CpuMicroseconds = 0f,
                    _pad0 = 0u
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float ReadRoomAwake01(int roomIndex)
            {
                int baseIndex = RoomBaseIndex[roomIndex];
                int clampedBaseIndex = math.clamp(baseIndex, 0, BaseAwakeState.Length - 1);
                float validBase01 = math.select(0f, 1f, (uint)baseIndex < (uint)BaseAwakeState.Length);
                float awake01 = math.select(0f, 1f, BaseAwakeState[clampedBaseIndex] != 0);
                return math.select(1f, awake01, validBase01 > 0f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void DiffuseGas(ref float roomA, ref float roomB, float fraction)
            {
                float delta = (roomA - roomB) * fraction;
                float positiveDelta = math.min(delta, roomA);
                float negativeDelta = -math.min(-delta, roomB);
                delta = math.select(negativeDelta, positiveDelta, delta > 0f);

                roomA -= delta;
                roomB += delta;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint HashFloat(uint hash, float value)
            {
                return (hash ^ math.asuint(value)) * 16777619u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveDaltonPressureKPa(float oxygenKPa, float carbonDioxideKPa, float nitrogenKPa)
            {
                return FiniteNonNegativeOrZero(oxygenKPa) +
                       FiniteNonNegativeOrZero(carbonDioxideKPa) +
                       FiniteNonNegativeOrZero(nitrogenKPa);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveToxicity01(float carbonDioxideKPa, float thresholdKPa, float fatalKPa)
            {
                float range = math.max(0.01f, fatalKPa - thresholdKPa);
                return math.saturate((FiniteNonNegativeOrZero(carbonDioxideKPa) - thresholdKPa) * math.rcp(range));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveNarcosis01(float pressureAtm, float thresholdAtm, float fullAtm)
            {
                float range = math.max(0.01f, fullAtm - thresholdAtm);
                return math.saturate((FiniteNonNegativeOrZero(pressureAtm) - thresholdAtm) * math.rcp(range));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float FiniteSaturate01(float value)
            {
                return math.select(0f, math.saturate(value), math.isfinite(value));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float FiniteNonNegativeOrZero(float value)
            {
                return math.select(0f, math.max(0f, value), math.isfinite(value));
            }
        }

        private struct GasDynamicsMemoryAuditAccumulator
        {
            public int AllocationCount;
            public long RegisteredBytes;
            public long LargestAllocationBytes;
            public uint LargestAllocationLabelHash;
        }

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        internal struct AtmosphereTelemetryEntry
        {
            [FieldOffset(0)]
            public ulong PackedOwner;
            [FieldOffset(8)]
            public uint FrameIndex;
            [FieldOffset(12)]
            public int RoomCount;
            [FieldOffset(16)]
            public float TotalO2KPa;
            [FieldOffset(20)]
            public float TotalCO2KPa;
            [FieldOffset(24)]
            public float TotalNitrogenKPa;
            [FieldOffset(28)]
            public float MaxPressureKPa;
            [FieldOffset(32)]
            public uint StateHash;
            [FieldOffset(36)]
            public uint BufferId;
            [FieldOffset(40)]
            public uint SystemId;
            [FieldOffset(44)]
            public uint Generation;
            [FieldOffset(48)]
            public int DroppedUpdates;
            [FieldOffset(52)]
            public float CpuMicroseconds;
            [FieldOffset(56)]
            private uint _pad0;
            [FieldOffset(60)]
            public ushort Flags;
            [FieldOffset(62)]
            public ushort Reserved;
        }
    }
}
