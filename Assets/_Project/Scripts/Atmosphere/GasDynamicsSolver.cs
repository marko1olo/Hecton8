using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Signals;
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
    public sealed class GasDynamicsSolver : MonoBehaviour, IGasDynamicsSolver, IUpdatable, IFixedTickable, IPostFixedTickable, IFrostTickable
    {
        private const int MaxRoomCapacity = 128;
        private const int MaxBulkheadCapacity = 256;
        private const int MaxBaseCapacity = 32;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 32;
        private const int ToxicitySignalSoftCapacity = 128;
        private const uint DumpMagic = 0x48384744u; // H8GD
        private const int DumpFormatVersion = 2;
        private const float KPaPerAtmosphere = 101.325f;
        private const float StandardOxygenKPa = 21.22f;
        private const float StandardCarbonDioxideKPa = 0.04f;
        private const float StandardNitrogenKPa = 80.065f;
        private const float DefaultCo2ToxicityThresholdKPa = 1.0f;
        private const float DefaultCo2FatalKPa = 7.0f;
        private const float DefaultNarcosisThresholdAtm = 4.0f;
        private const float DefaultNarcosisFullAtm = 7.0f;
        private const float DefaultPlayerO2KPaPerSecond = 0.012f;
        private const float DefaultPlayerCO2KPaPerSecond = 0.010f;
        private const float DefaultFireO2KPaPerSecond = 0.080f;
        private const float DefaultScrubberKPaPerSecond = 0.055f;
        private const float DefaultRoomTemperatureCelsius = 20f;
        private const float FreezingScrubberEfficiencyScale = 0.5f;
        private const float DefaultDiffusionConductancePerSecond = 0.45f;
        private const float DefaultHibernationDistanceMeters = 500f;
        private const float DefaultLowTierHibernationDistanceMeters = 150f;
        private const float DefaultHibernationHysteresisMeters = 25f;
        private const float DefaultBaseIdleDrawWatts = 45f;
        private const float DefaultBaseBatteryWattSeconds = 720000f;
        private const float DefaultHibernationLeakRatePerSecond = 0.00006f;
        private const float MaxWakeCatchUpSeconds = 86400f;
        private const float MaxDiffusionFractionPerStep = 0.45f;
        private const ushort TelemetryFlagNaN = 1 << 0;
        private const ushort TelemetryFlagBreach = 1 << 1;
        private const ushort TelemetryFlagHibernating = 1 << 2;
        private const ushort ToxicityFlagCO2 = 1 << 0;
        private const ushort ToxicityFlagNarcosis = 1 << 1;
        private const ushort RoomFlagInternalFire = (ushort)GasDynamicsRoomFlags.InternalFire;
        private const ushort RoomFlagBreached = (ushort)GasDynamicsRoomFlags.Breached;
        private const ushort RoomFlagOccupied = (ushort)GasDynamicsRoomFlags.Occupied;
        private const string NativeMemoryOwner = nameof(GasDynamicsSolver);
        private const string DumpFileName = "Dump_HABITAT_O2_SCRUBBER_LOD.bin";

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
        [Tooltip("Standard hibernation distance for Mid+ quality tiers.")]
        [SerializeField, Min(1f)] private float hibernationDistanceMeters = DefaultHibernationDistanceMeters;
        [Tooltip("Low/MX350 hibernation distance used to shut down distant base math earlier.")]
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

        // Prompt-mandated SOA lane names. These are private; callers read through IGasDynamicsSolver.
        private NativeArray<float> RoomO2;
        private NativeArray<float> RoomCO2;
        private NativeArray<float> RoomPressure;
        private NativeArray<float> _roomO2Back;
        private NativeArray<float> _roomCO2Back;
        private NativeArray<float> _roomNitrogen;
        private NativeArray<float> _roomNitrogenBack;
        private NativeArray<float> _roomPressureBack;
        private NativeArray<float> _roomAmbientPressure;
        private NativeArray<float> _roomSubmerged01;
        private NativeArray<float> _roomPlayerStress01;
        private NativeArray<float> _roomPlayerHeartRateBpm;
        private NativeArray<float> _roomTemperatureCelsius;
        private NativeArray<byte> _roomPlayerPresent;
        private NativeArray<byte> _roomScrubberPowered;
        private NativeArray<ushort> _roomFlags;
        private NativeArray<int> _roomBaseIndex;
        private NativeArray<byte> BaseAwakeState;
        private NativeArray<byte> _basePlayerInside;
        private NativeArray<int> _basePlayerInsideCount;
        private NativeArray<int> _baseRoomStart;
        private NativeArray<int> _baseRoomCount;
        private NativeArray<AbsoluteUniversePosition> _baseCenterAup;
        private NativeArray<double> _baseHibernatedUnscaledTime;
        private NativeArray<float> _baseBatteryWattSeconds;
        private NativeArray<float> _baseIdleDrawWatts;
        private NativeArray<float> _baseLeakRatePerSecond;
        private NativeArray<float> _baseAmbientOxygenKPa;
        private NativeArray<int> _bulkheadRoomA;
        private NativeArray<int> _bulkheadRoomB;
        private NativeArray<byte> _bulkheadSealed;
        private NativeArray<GasDynamicsTelemetryEntry> _telemetryRing;
        private NativeQueue<ToxicitySignal> _toxicitySignals;
        private JobHandle _stepHandle;
        private JobHandle _disposeHandle;
        private bool _stepRunning;
        private bool _registeredTicks;
        private bool _registeredRegistry;
        private bool _baseAwakeVaultOwned;
        private bool _seededStandardAtmosphere;
        private bool _blackBoxDumped;
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
        private float _lastCadenceSeconds = 2.0f;
        private GasDynamicsMathLod _lastMathLod = GasDynamicsMathLod.Low;
        private ITickDispatcher _tickDispatcher;
        private IPlayerMovementContracts _playerMovementContracts;
        private IDataVault _dataVault;

        public bool IsInitialized =>
            _toxicitySignals.IsCreated &&
            _telemetryRing.IsCreated &&
            AreRoomStateLanesReady(_roomCount) &&
            AreBulkheadLanesReady(_bulkheadCount) &&
            AreBaseStateLanesReady(_baseCount);
        public int RoomCount => _roomCount;
        public int BaseCount => _baseCount;
        public float LastCadenceSeconds => _lastCadenceSeconds;
        public GasDynamicsMathLod LastMathLod => _lastMathLod;

        NativeArray<float>.ReadOnly IGasDynamicsSolver.RoomO2 => RoomO2.IsCreated ? RoomO2.AsReadOnly() : default;
        NativeArray<float>.ReadOnly IGasDynamicsSolver.RoomCO2 => RoomCO2.IsCreated ? RoomCO2.AsReadOnly() : default;
        NativeArray<float>.ReadOnly IGasDynamicsSolver.RoomPressure => RoomPressure.IsCreated ? RoomPressure.AsReadOnly() : default;
        NativeArray<byte>.ReadOnly IGasDynamicsSolver.BaseAwakeState => BaseAwakeState.IsCreated ? BaseAwakeState.AsReadOnly() : default;
        int ISystem.TickCount => _tickCount;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!TryFinalizeDeferredNativeDisposal())
            {
                TryRegisterTicks();
                return;
            }

            CacheColdDependencies();
            EnsureNativeState();
            SeedStandardAtmosphereIfNeeded();
            TryRegisterRegistry();
            TryRegisterTicks();
        }

        private void OnDisable()
        {
            TryUnregisterTicks();
            TryUnregisterRegistry();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            TryUnregisterTicks();
            TryUnregisterRegistry();
            DisposeNativeStateDeferred();
        }

        public void Tick(float deltaTime)
        {
            if (!Application.isPlaying)
                return;

            if (!TryFinalizeDeferredNativeDisposal())
                return;

            if (!IsInitialized)
                CacheColdDependencies();
            EnsureNativeState();
            SeedStandardAtmosphereIfNeeded();
            TryRegisterRegistry();
            bool canWake = !_stepRunning;
            DrainBaseTransitionSignals(canWake);
            if (canWake)
                WakePlayerInsideSleepingBases(ResolveUnscaledTimeSeconds());
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            if (!TryFinalizeDeferredNativeDisposal())
                return;

            if (!IsInitialized)
                CacheColdDependencies();
            EnsureNativeState();
            SeedStandardAtmosphereIfNeeded();
            TryRegisterRegistry();
            if (!TryCompleteStep())
            {
                _tickAccumulator += math.max(0f, fixedDeltaTime);
                return;
            }

            double now = ResolveUnscaledTimeSeconds();
            DrainBaseTransitionSignals(allowWake: true);
            WakePlayerInsideSleepingBases(now);
            _lastMathLod = ResolveMathLod(GlobalRegistry.ScalabilityTier);
            _lastCadenceSeconds = ResolveCadenceSeconds(_lastMathLod);
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
                CacheColdDependencies();
            EnsureNativeState();
            SeedStandardAtmosphereIfNeeded();
            TryRegisterRegistry();
            DrainBaseTransitionSignals(allowWake: true);
            WakePlayerInsideSleepingBases(ResolveUnscaledTimeSeconds());
            ResolveBaseHibernationStates();
        }

        public bool TryGetRoomSnapshot(int roomId, out GasRoomSnapshot snapshot)
        {
            snapshot = default;
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

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
                !AreBaseStateLanesReady(baseId + 1))
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
                !_roomBaseIndex.IsCreated ||
                baseId < 0 ||
                baseId >= _baseCapacityLimit ||
                !AreBaseStateLanesReady(baseId + 1) ||
                !AreRoomStateLanesReady(_roomCount))
            {
                return false;
            }

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

        public bool TrySetBasePlayerInside(int baseId, bool playerInside)
        {
            if (_stepRunning || baseId < 0 || baseId >= _baseCount || !AreBaseStateLanesReady(baseId + 1))
                return false;

            _basePlayerInsideCount[baseId] = playerInside ? math.max(1, _basePlayerInsideCount[baseId]) : 0;
            _basePlayerInside[baseId] = (byte)(playerInside ? 1 : 0);
            if (playerInside && BaseAwakeState[baseId] == 0)
                WakeBase(baseId, ResolveUnscaledTimeSeconds());
            return true;
        }

        public bool TrySetBaseCenterAup(int baseId, AbsoluteUniversePosition centerAup)
        {
            if (_stepRunning || baseId < 0 || baseId >= _baseCount || !AreBaseStateLanesReady(baseId + 1))
                return false;

            _baseCenterAup[baseId] = centerAup;
            return true;
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

            _bulkheadRoomA[edgeIndex] = roomA;
            _bulkheadRoomB[edgeIndex] = roomB;
            _bulkheadSealed[edgeIndex] = (byte)(sealedBulkhead ? 1 : 0);
            if (edgeIndex >= _bulkheadCount)
                _bulkheadCount = edgeIndex + 1;
            return true;
        }

        public bool TrySetPlayerRoom(int roomId, float playerStress01, float heartRateBpm)
        {
            if (_stepRunning || !AreRoomStateLanesReady(_roomCount))
                return false;

            if (roomId >= _roomCount)
                return false;

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

        public bool TrySetRoomFlags(int roomId, ushort setMask, ushort clearMask)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            ushort flags = (ushort)((_roomFlags[roomId] | setMask) & ~clearMask);
            if (_roomPlayerPresent[roomId] != 0)
                flags = (ushort)(flags | RoomFlagOccupied);
            _roomFlags[roomId] = flags;
            return true;
        }

        public bool TrySetRoomSubmergedFraction(int roomId, float submerged01)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            _roomSubmerged01[roomId] = FiniteSaturate01(submerged01);
            return true;
        }

        public bool TrySetAmbientPressure(int roomId, float ambientPressureKPa)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            _roomAmbientPressure[roomId] = FiniteNonNegativeOrZero(ambientPressureKPa);
            return true;
        }

        public bool TrySetScrubberPowered(int roomId, bool powerActive)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            _roomScrubberPowered[roomId] = (byte)(powerActive ? 1 : 0);
            return true;
        }

        public bool TrySetRoomTemperatureCelsius(int roomId, float temperatureCelsius)
        {
            if (_stepRunning || roomId < 0 || roomId >= _roomCount || !AreRoomStateLanesReady(roomId + 1))
                return false;

            _roomTemperatureCelsius[roomId] = math.isfinite(temperatureCelsius)
                ? math.clamp(temperatureCelsius, -80f, 300f)
                : DefaultRoomTemperatureCelsius;
            return true;
        }

        public bool TryDequeueToxicitySignal(out ToxicitySignal signal)
        {
            signal = default;
            return !_stepRunning && _toxicitySignals.IsCreated && _toxicitySignals.TryDequeue(out signal);
        }

        public bool TryGetNativeMemoryAudit(out GasDynamicsNativeMemoryAudit audit)
        {
            audit = default;
            if (!RoomO2.IsCreated)
                return false;

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
            if (!_baseAwakeVaultOwned)
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
            AccumulateAudit(_telemetryRing, nameof(_telemetryRing), ref accumulator);
            if (_toxicitySignals.IsCreated)
            {
                long queueBytes = (long)UnsafeUtility.SizeOf<ToxicitySignal>() * ToxicitySignalSoftCapacity;
                AccumulateAudit(queueBytes, nameof(_toxicitySignals), ref accumulator);
            }

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
            if (!RoomPressure.IsCreated ||
                roomId < 0 ||
                roomId >= _roomCount ||
                roomId >= RoomPressure.Length)
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
            if (!IsInitialized)
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

        private void TryRegisterTicks()
        {
            if (_registeredTicks)
                return;

            bool updateRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            bool postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            bool frostRegistered = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);
            if (!updateRegistered || !fixedRegistered || !postFixedRegistered || !frostRegistered)
            {
                if (updateRegistered)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
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

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
            _registeredTicks = false;
        }

        private void EnsureNativeState()
        {
            if (RoomO2.IsCreated)
                return;

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

            RoomO2 = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[roomCapacity] - oxygen partial pressure kPa - owner: GasDynamicsSolver
            RoomCO2 = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[roomCapacity] - carbon dioxide partial pressure kPa - owner: GasDynamicsSolver
            RoomPressure = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[roomCapacity] - Dalton total pressure kPa - owner: GasDynamicsSolver
            _roomO2Back = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomCO2Back = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomNitrogen = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomNitrogenBack = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPressureBack = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomAmbientPressure = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomSubmerged01 = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPlayerStress01 = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPlayerHeartRateBpm = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomTemperatureCelsius = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPlayerPresent = new NativeArray<byte>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomScrubberPowered = new NativeArray<byte>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomFlags = new NativeArray<ushort>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomBaseIndex = new NativeArray<int>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            BaseAwakeState = ResolveBaseAwakeStateBuffer(safeBaseCapacity);
            _basePlayerInside = new NativeArray<byte>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _basePlayerInsideCount = new NativeArray<int>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseRoomStart = new NativeArray<int>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseRoomCount = new NativeArray<int>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseCenterAup = new NativeArray<AbsoluteUniversePosition>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseHibernatedUnscaledTime = new NativeArray<double>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseBatteryWattSeconds = new NativeArray<float>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseIdleDrawWatts = new NativeArray<float>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseLeakRatePerSecond = new NativeArray<float>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _baseAmbientOxygenKPa = new NativeArray<float>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bulkheadRoomA = new NativeArray<int>(math.max(1, safeBulkheadCapacity), Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bulkheadRoomB = new NativeArray<int>(math.max(1, safeBulkheadCapacity), Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bulkheadSealed = new NativeArray<byte>(math.max(1, safeBulkheadCapacity), Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _telemetryRing = new NativeArray<GasDynamicsTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _toxicitySignals = new NativeQueue<ToxicitySignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ToxicitySignal>[128] - gas-to-physiology event lane - owner: GasDynamicsSolver

            RegisterNativeArray(RoomO2, nameof(RoomO2));
            RegisterNativeArray(RoomCO2, nameof(RoomCO2));
            RegisterNativeArray(RoomPressure, nameof(RoomPressure));
            RegisterNativeArray(_roomO2Back, nameof(_roomO2Back));
            RegisterNativeArray(_roomCO2Back, nameof(_roomCO2Back));
            RegisterNativeArray(_roomNitrogen, nameof(_roomNitrogen));
            RegisterNativeArray(_roomNitrogenBack, nameof(_roomNitrogenBack));
            RegisterNativeArray(_roomPressureBack, nameof(_roomPressureBack));
            RegisterNativeArray(_roomAmbientPressure, nameof(_roomAmbientPressure));
            RegisterNativeArray(_roomSubmerged01, nameof(_roomSubmerged01));
            RegisterNativeArray(_roomPlayerStress01, nameof(_roomPlayerStress01));
            RegisterNativeArray(_roomPlayerHeartRateBpm, nameof(_roomPlayerHeartRateBpm));
            RegisterNativeArray(_roomTemperatureCelsius, nameof(_roomTemperatureCelsius));
            RegisterNativeArray(_roomPlayerPresent, nameof(_roomPlayerPresent));
            RegisterNativeArray(_roomScrubberPowered, nameof(_roomScrubberPowered));
            RegisterNativeArray(_roomFlags, nameof(_roomFlags));
            RegisterNativeArray(_roomBaseIndex, nameof(_roomBaseIndex));
            if (!_baseAwakeVaultOwned)
                RegisterNativeArray(BaseAwakeState, nameof(BaseAwakeState));
            RegisterNativeArray(_basePlayerInside, nameof(_basePlayerInside));
            RegisterNativeArray(_basePlayerInsideCount, nameof(_basePlayerInsideCount));
            RegisterNativeArray(_baseRoomStart, nameof(_baseRoomStart));
            RegisterNativeArray(_baseRoomCount, nameof(_baseRoomCount));
            RegisterNativeArray(_baseCenterAup, nameof(_baseCenterAup));
            RegisterNativeArray(_baseHibernatedUnscaledTime, nameof(_baseHibernatedUnscaledTime));
            RegisterNativeArray(_baseBatteryWattSeconds, nameof(_baseBatteryWattSeconds));
            RegisterNativeArray(_baseIdleDrawWatts, nameof(_baseIdleDrawWatts));
            RegisterNativeArray(_baseLeakRatePerSecond, nameof(_baseLeakRatePerSecond));
            RegisterNativeArray(_baseAmbientOxygenKPa, nameof(_baseAmbientOxygenKPa));
            RegisterNativeArray(_bulkheadRoomA, nameof(_bulkheadRoomA));
            RegisterNativeArray(_bulkheadRoomB, nameof(_bulkheadRoomB));
            RegisterNativeArray(_bulkheadSealed, nameof(_bulkheadSealed));
            RegisterNativeArray(_telemetryRing, nameof(_telemetryRing));
            NativeMemorySentinel.RegisterNativeQueue(_toxicitySignals, ToxicitySignalSoftCapacity, NativeMemoryOwner, nameof(_toxicitySignals), NativeAllocationLifetime.Scene);
            for (int i = 0; i < _roomCount; i++)
            {
                _roomTemperatureCelsius[i] = DefaultRoomTemperatureCelsius;
                _roomBaseIndex[i] = 0;
            }

            InitializeBaseSlots(safeBaseCapacity, safeRoomCapacity);
            PrewarmQueue(ref _toxicitySignals, ToxicitySignalSoftCapacity);
        }

        private void CacheColdDependencies()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _playerMovementContracts = GlobalRegistry.PlayerMovementContracts;
            _dataVault = GlobalRegistry.DataVault;
        }

        private NativeArray<byte> ResolveBaseAwakeStateBuffer(int safeBaseCapacity)
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                NativeArray<byte> vaultBuffer = vault.GetBuffer<byte>(
                    BufferID.HabitatBaseAwakeState,
                    safeBaseCapacity,
                    SystemID.HabitatAtmosphere,
                    NativeArrayOptions.ClearMemory);
                if (vaultBuffer.IsCreated && vaultBuffer.Length >= safeBaseCapacity)
                {
                    _baseAwakeVaultOwned = true;
                    return vaultBuffer;
                }
            }

            _baseAwakeVaultOwned = false;
            return new NativeArray<byte>(safeBaseCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[baseCapacity] - fallback base awake mask when DataVault is unavailable - owner: GasDynamicsSolver
        }

        private void InitializeBaseSlots(int safeBaseCapacity, int safeRoomCapacity)
        {
            AbsoluteUniversePosition defaultCenterAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
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
            if (!seedStandardAtmosphereOnEnable || _seededStandardAtmosphere || !RoomO2.IsCreated)
                return;

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

        private void ScheduleStep(float deltaTime)
        {
            if (_stepRunning || !IsInitialized)
                return;

            TrimToxicityQueueBeforeSchedule();
            float co2Threshold = FiniteNonNegativeOrZero(co2ToxicityThresholdKPa);
            float co2Fatal = math.max(co2Threshold + 0.01f, FiniteNonNegativeOrZero(co2FatalKPa));
            float narcosisThreshold = math.max(1f, FiniteNonNegativeOrZero(narcosisThresholdAtm));
            float narcosisFull = math.max(narcosisThreshold + 0.01f, FiniteNonNegativeOrZero(narcosisFullAtm));
            int telemetryLength = _telemetryRing.IsCreated ? _telemetryRing.Length : 0;
            int writeIndex = telemetryLength > 0 ? _telemetryWriteIndex % telemetryLength : 0;
            _telemetryWriteIndex = telemetryLength > 0 ? (writeIndex + 1) % telemetryLength : 0;
            GasDynamicsStepJob job = new GasDynamicsStepJob
            {
                DeltaTime = math.max(0f, deltaTime),
                RoomCount = _roomCount,
                BulkheadCount = _bulkheadCount,
                FrameIndex = (uint)math.max(0, Time.frameCount),
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
                ToxicitySignals = _toxicitySignals.AsParallelWriter(),
                TelemetryRing = _telemetryRing
            };

            _stepHandle = job.Schedule();
            _stepRunning = true;
        }

        private bool TryCompleteStep()
        {
            if (!_stepRunning)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _stepHandle, forceComplete: false))
                return false;

            _stepRunning = false;
            Swap(ref RoomO2, ref _roomO2Back);
            Swap(ref RoomCO2, ref _roomCO2Back);
            Swap(ref _roomNitrogen, ref _roomNitrogenBack);
            Swap(ref RoomPressure, ref _roomPressureBack);
            _tickCount++;
            PublishActiveRoomUi();
            CheckTelemetryForFault();
            return true;
        }

        private void DrainBaseTransitionSignals(bool allowWake)
        {
            if (_baseCapacityLimit <= 0)
                return;

            if (SignalBus<PlayerBaseExitSignal>.SnapshotCount <= 0 &&
                SignalBus<PlayerBaseEnterSignal>.SnapshotCount <= 0)
            {
                return;
            }

            double now = ResolveUnscaledTimeSeconds();
            while (SignalBus<PlayerBaseExitSignal>.TryReadFrame(out PlayerBaseExitSignal signal))
            {
                if (!TryEnsureBaseSlotFromSignal(signal.BaseId, signal.RoomId, in signal.BaseCenterAup))
                    continue;

                int insideCount = math.max(0, _basePlayerInsideCount[signal.BaseId] - 1);
                _basePlayerInsideCount[signal.BaseId] = insideCount;
                _basePlayerInside[signal.BaseId] = (byte)(insideCount > 0 ? 1 : 0);
                _baseCenterAup[signal.BaseId] = signal.BaseCenterAup;
            }

            // Enter wins over exit for same-frame module-to-module trigger handoffs.
            while (SignalBus<PlayerBaseEnterSignal>.TryReadFrame(out PlayerBaseEnterSignal signal))
            {
                if (!TryEnsureBaseSlotFromSignal(signal.BaseId, signal.RoomId, in signal.BaseCenterAup))
                    continue;

                int insideCount = _basePlayerInsideCount[signal.BaseId];
                _basePlayerInsideCount[signal.BaseId] = insideCount < int.MaxValue ? insideCount + 1 : int.MaxValue;
                _basePlayerInside[signal.BaseId] = 1;
                _baseCenterAup[signal.BaseId] = signal.BaseCenterAup;
                if (allowWake)
                    WakeBase(signal.BaseId, now);
            }
        }

        private void WakePlayerInsideSleepingBases(double now)
        {
            if (_stepRunning ||
                _baseCount <= 0 ||
                !AreBaseStateLanesReady(_baseCount))
            {
                return;
            }

            for (int baseId = 0; baseId < _baseCount; baseId++)
            {
                if (_basePlayerInside[baseId] != 0 && BaseAwakeState[baseId] == 0)
                    WakeBase(baseId, now);
            }
        }

        private bool TryEnsureBaseSlotFromSignal(int baseId, int roomId, in AbsoluteUniversePosition centerAup)
        {
            if (baseId < 0 ||
                baseId >= _baseCapacityLimit ||
                !_roomBaseIndex.IsCreated ||
                !AreBaseStateLanesReady(baseId + 1))
            {
                return false;
            }

            if (baseId >= _baseCount)
            {
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
                if (_baseRoomCount[baseId] <= 0)
                {
                    _baseRoomStart[baseId] = roomId;
                    _baseRoomCount[baseId] = 1;
                }

                _roomBaseIndex[roomId] = baseId;
            }

            return true;
        }

        private void ResolveBaseHibernationStates()
        {
            if (_baseCount <= 0 || !AreBaseStateLanesReady(_baseCount))
                return;

            double now = ResolveUnscaledTimeSeconds();
            bool hasPlayerAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);
            float sleepDistance = ResolveHibernationDistanceMeters(_lastMathLod);
            float wakeDistance = math.max(0f, sleepDistance - math.max(3f, hibernationHysteresisMeters));
            double sleepDistanceSq = (double)sleepDistance * sleepDistance;
            double wakeDistanceSq = (double)wakeDistance * wakeDistance;
            int sleepingCount = 0;

            for (int baseId = 0; baseId < _baseCount; baseId++)
            {
                bool awake = BaseAwakeState[baseId] != 0;
                bool playerInside = _basePlayerInside[baseId] != 0;
                bool hasRooms = _baseRoomCount[baseId] > 0;
                AbsoluteUniversePosition baseCenterAup = _baseCenterAup[baseId];
                double distanceSq = hasPlayerAup
                    ? AbsoluteUniversePosition.DistanceSq(in playerAup, in baseCenterAup)
                    : 0d;

                if (awake)
                {
                    if (hasRooms && !playerInside && hasPlayerAup && double.IsFinite(distanceSq) && distanceSq > sleepDistanceSq)
                        HibernateBase(baseId, now);
                }
                else
                {
                    bool playerNear = hasPlayerAup && double.IsFinite(distanceSq) && distanceSq <= wakeDistanceSq;
                    if (playerInside || playerNear)
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

            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            playerAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return true;
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

        private float ResolveHibernationDistanceMeters(GasDynamicsMathLod lod)
        {
            switch (lod)
            {
                case GasDynamicsMathLod.Low:
                    return math.max(1f, lowTierHibernationDistanceMeters);
                default:
                    return math.max(1f, hibernationDistanceMeters);
            }
        }

        private bool AreRoomStateLanesReady(int requiredCount)
        {
            requiredCount = math.max(0, requiredCount);
            return RoomO2.IsCreated &&
                   RoomO2.Length >= requiredCount &&
                   RoomCO2.IsCreated &&
                   RoomCO2.Length >= requiredCount &&
                   RoomPressure.IsCreated &&
                   RoomPressure.Length >= requiredCount &&
                   _roomO2Back.IsCreated &&
                   _roomO2Back.Length >= requiredCount &&
                   _roomCO2Back.IsCreated &&
                   _roomCO2Back.Length >= requiredCount &&
                   _roomNitrogen.IsCreated &&
                   _roomNitrogen.Length >= requiredCount &&
                   _roomNitrogenBack.IsCreated &&
                   _roomNitrogenBack.Length >= requiredCount &&
                   _roomPressureBack.IsCreated &&
                   _roomPressureBack.Length >= requiredCount &&
                   _roomAmbientPressure.IsCreated &&
                   _roomAmbientPressure.Length >= requiredCount &&
                   _roomSubmerged01.IsCreated &&
                   _roomSubmerged01.Length >= requiredCount &&
                   _roomPlayerStress01.IsCreated &&
                   _roomPlayerStress01.Length >= requiredCount &&
                   _roomPlayerHeartRateBpm.IsCreated &&
                   _roomPlayerHeartRateBpm.Length >= requiredCount &&
                   _roomTemperatureCelsius.IsCreated &&
                   _roomTemperatureCelsius.Length >= requiredCount &&
                   _roomPlayerPresent.IsCreated &&
                   _roomPlayerPresent.Length >= requiredCount &&
                   _roomScrubberPowered.IsCreated &&
                   _roomScrubberPowered.Length >= requiredCount &&
                   _roomFlags.IsCreated &&
                   _roomFlags.Length >= requiredCount &&
                   _roomBaseIndex.IsCreated &&
                   _roomBaseIndex.Length >= requiredCount;
        }

        private bool AreBulkheadLanesReady(int requiredCount)
        {
            requiredCount = math.max(0, requiredCount);
            return _bulkheadRoomA.IsCreated &&
                   _bulkheadRoomA.Length >= requiredCount &&
                   _bulkheadRoomB.IsCreated &&
                   _bulkheadRoomB.Length >= requiredCount &&
                   _bulkheadSealed.IsCreated &&
                   _bulkheadSealed.Length >= requiredCount;
        }

        private bool AreBaseStateLanesReady(int requiredCount)
        {
            requiredCount = math.max(0, requiredCount);
            return BaseAwakeState.IsCreated &&
                   BaseAwakeState.Length >= requiredCount &&
                   _basePlayerInside.IsCreated &&
                   _basePlayerInside.Length >= requiredCount &&
                   _basePlayerInsideCount.IsCreated &&
                   _basePlayerInsideCount.Length >= requiredCount &&
                   _baseRoomStart.IsCreated &&
                   _baseRoomStart.Length >= requiredCount &&
                   _baseRoomCount.IsCreated &&
                   _baseRoomCount.Length >= requiredCount &&
                   _baseCenterAup.IsCreated &&
                   _baseCenterAup.Length >= requiredCount &&
                   _baseHibernatedUnscaledTime.IsCreated &&
                   _baseHibernatedUnscaledTime.Length >= requiredCount &&
                   _baseBatteryWattSeconds.IsCreated &&
                   _baseBatteryWattSeconds.Length >= requiredCount &&
                   _baseIdleDrawWatts.IsCreated &&
                   _baseIdleDrawWatts.Length >= requiredCount &&
                   _baseLeakRatePerSecond.IsCreated &&
                   _baseLeakRatePerSecond.Length >= requiredCount &&
                   _baseAmbientOxygenKPa.IsCreated &&
                   _baseAmbientOxygenKPa.Length >= requiredCount;
        }

        private void HibernateBase(int baseId, double now)
        {
            if ((uint)baseId >= (uint)_baseCount ||
                !AreBaseStateLanesReady(baseId + 1) ||
                BaseAwakeState[baseId] == 0)
            {
                return;
            }

            BaseAwakeState[baseId] = 0;
            _baseHibernatedUnscaledTime[baseId] = double.IsFinite(now) && now >= 0d ? now : 0d;
        }

        private void WakeBase(int baseId, double now)
        {
            if ((uint)baseId >= (uint)_baseCount ||
                !AreBaseStateLanesReady(baseId + 1) ||
                BaseAwakeState[baseId] != 0)
            {
                return;
            }

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

            job.Run(); // COLD SYNC JOB: FrostTick wake catch-up, not a per-frame path.
        }

        private void PublishActiveRoomUi()
        {
            int roomId = _activePlayerRoom >= 0 ? _activePlayerRoom : 0;
            if (!TryGetRoomSnapshot(roomId, out GasRoomSnapshot snapshot))
                return;

            float invPressure = snapshot.PressureKPa > 0.001f ? math.rcp(snapshot.PressureKPa) : 0f;
            float oxygen01 = math.saturate(snapshot.OxygenKPa * invPressure);
            float time = Time.unscaledTime;
            UIStateStore.WriteValue(UIValueSlotId.RoomOxygen01, oxygen01, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomOxygenPartialKPa, snapshot.OxygenKPa, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomCarbonDioxidePartialKPa, snapshot.CarbonDioxideKPa, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomPressureKPa, snapshot.PressureKPa, time);
            UIStateStore.WriteValue(UIValueSlotId.RoomNarcosis01, snapshot.Narcosis01, time);
        }

        private void CheckTelemetryForFault()
        {
            if (!_telemetryRing.IsCreated)
                return;

            int telemetryLength = _telemetryRing.Length;
            if (telemetryLength <= 0)
                return;

            int lastIndex = (_telemetryWriteIndex + telemetryLength - 1) % telemetryLength;
            GasDynamicsTelemetryEntry entry = _telemetryRing[lastIndex];
            if ((entry.Flags & TelemetryFlagNaN) != 0)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !_telemetryRing.IsCreated)
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
                    writer.Write(_telemetryRing.Length);
                    writer.Write(_telemetryWriteIndex);
                    writer.Write(_tickCount);
                    for (int i = 0; i < _telemetryRing.Length; i++)
                    {
                        GasDynamicsTelemetryEntry entry = _telemetryRing[i];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.RoomCount);
                        writer.Write(entry.TotalO2KPa);
                        writer.Write(entry.TotalCO2KPa);
                        writer.Write(entry.TotalNitrogenKPa);
                        writer.Write(entry.MaxPressureKPa);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Reserved);
                    }
                }
            }
            catch (System.Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[GasDynamicsSolver] Black box dump failed: " + exception.Message);
#endif
            }
        }

        private void TrimToxicityQueueBeforeSchedule()
        {
            if (!_toxicitySignals.IsCreated || _toxicitySignals.Count == 0)
                return;

            while (_toxicitySignals.TryDequeue(out _))
            {
            }
        }

        private bool TryFinalizeDeferredNativeDisposal()
        {
            return DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle);
        }

        private void DisposeNativeStateDeferred()
        {
            if (!RoomO2.IsCreated)
                return;

            bool waitForStep = _stepRunning;
            JobHandle disposeHandle = waitForStep ? _stepHandle : default;

            DisposeArray(ref RoomO2, ref disposeHandle, waitForStep);
            DisposeArray(ref RoomCO2, ref disposeHandle, waitForStep);
            DisposeArray(ref RoomPressure, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomO2Back, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomCO2Back, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomNitrogen, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomNitrogenBack, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomPressureBack, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomAmbientPressure, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomSubmerged01, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomPlayerStress01, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomPlayerHeartRateBpm, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomTemperatureCelsius, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomPlayerPresent, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomScrubberPowered, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomFlags, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomBaseIndex, ref disposeHandle, waitForStep);
            DisposeBaseAwakeState(ref disposeHandle, waitForStep);
            DisposeArray(ref _basePlayerInside, ref disposeHandle, waitForStep);
            DisposeArray(ref _basePlayerInsideCount, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseRoomStart, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseRoomCount, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseCenterAup, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseHibernatedUnscaledTime, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseBatteryWattSeconds, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseIdleDrawWatts, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseLeakRatePerSecond, ref disposeHandle, waitForStep);
            DisposeArray(ref _baseAmbientOxygenKPa, ref disposeHandle, waitForStep);
            DisposeArray(ref _bulkheadRoomA, ref disposeHandle, waitForStep);
            DisposeArray(ref _bulkheadRoomB, ref disposeHandle, waitForStep);
            DisposeArray(ref _bulkheadSealed, ref disposeHandle, waitForStep);
            DisposeArray(ref _telemetryRing, ref disposeHandle, waitForStep);

            if (_toxicitySignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_toxicitySignals));
                if (waitForStep)
                {
                    disposeHandle = _toxicitySignals.Dispose(disposeHandle);
                }
                else
                {
                    while (_toxicitySignals.TryDequeue(out _))
                    {
                    }

                    _toxicitySignals.Dispose();
                }

                _toxicitySignals = default;
            }

            _disposeHandle = waitForStep ? disposeHandle : default;
            _stepHandle = default;
            _stepRunning = false;
            _seededStandardAtmosphere = false;
            _roomCount = 0;
            _bulkheadCapacityLimit = 0;
            _bulkheadCount = 0;
            _baseCapacityLimit = 0;
            _baseCount = 0;
            _sleepingBaseCount = 0;
            _activePlayerRoom = -1;
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Scene);
        }

        private static void DisposeArray<T>(ref NativeArray<T> array, ref JobHandle disposeHandle, bool deferred) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (deferred)
                disposeHandle = array.Dispose(disposeHandle);
            else
                array.Dispose();
            array = default;
        }

        private void DisposeBaseAwakeState(ref JobHandle disposeHandle, bool deferred)
        {
            if (!BaseAwakeState.IsCreated)
                return;

            if (_baseAwakeVaultOwned)
            {
                BaseAwakeState = default;
                _baseAwakeVaultOwned = false;
                return;
            }

            NativeMemorySentinel.UnregisterNativeArray(BaseAwakeState);
            if (deferred)
                disposeHandle = BaseAwakeState.Dispose(disposeHandle);
            else
                BaseAwakeState.Dispose();
            BaseAwakeState = default;
            _baseAwakeVaultOwned = false;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity) where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);
            while (queue.TryDequeue(out _))
            {
            }
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

        private GasDynamicsMathLod ResolveMathLod(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                    return GasDynamicsMathLod.High;
                case HectonQualityTier.Ultra:
                    return GasDynamicsMathLod.Ultra;
                case HectonQualityTier.Mid:
                    return GasDynamicsMathLod.Mid;
                default:
                    return GasDynamicsMathLod.Low;
            }
        }

        private float ResolveCadenceSeconds(GasDynamicsMathLod lod)
        {
            switch (lod)
            {
                case GasDynamicsMathLod.High:
                case GasDynamicsMathLod.Ultra:
                    return math.max(0.02f, highTierColdTickSeconds);
                case GasDynamicsMathLod.Mid:
                    return math.max(0.05f, midTierColdTickSeconds);
                default:
                    return math.max(0.1f, lowTierColdTickSeconds);
            }
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
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap(ref NativeArray<float> first, ref NativeArray<float> second)
        {
            NativeArray<float> temp = first;
            first = second;
            second = temp;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = false)]
        private struct BaseHibernationWakeCatchUpJob : IJob
        {
            public int BaseId;
            public int RoomCount;
            public float ElapsedSeconds;

            public NativeArray<float> RoomO2;
            public NativeArray<float> RoomCO2;
            public NativeArray<float> RoomNitrogen;
            public NativeArray<float> RoomPressure;
            public NativeArray<float> RoomO2Back;
            public NativeArray<float> RoomCO2Back;
            public NativeArray<float> RoomNitrogenBack;
            public NativeArray<float> RoomPressureBack;

            [ReadOnly] public NativeArray<int> BaseRoomStart;
            [ReadOnly] public NativeArray<int> BaseRoomCount;
            public NativeArray<float> BaseBatteryWattSeconds;
            [ReadOnly] public NativeArray<float> BaseIdleDrawWatts;
            [ReadOnly] public NativeArray<float> BaseLeakRatePerSecond;
            [ReadOnly] public NativeArray<float> BaseAmbientOxygenKPa;

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

                if (battery <= 0f)
                {
                    for (int room = startRoom; room < roomEnd; room++)
                    {
                        float carbonDioxide = FiniteNonNegativeOrZero(RoomCO2[room]);
                        float nitrogen = FiniteNonNegativeOrZero(RoomNitrogen[room]);
                        float pressure = ResolveDaltonPressureKPa(0f, carbonDioxide, nitrogen);
                        RoomO2[room] = 0f;
                        RoomCO2[room] = carbonDioxide;
                        RoomNitrogen[room] = nitrogen;
                        RoomO2Back[room] = 0f;
                        RoomCO2Back[room] = carbonDioxide;
                        RoomNitrogenBack[room] = nitrogen;
                        RoomPressure[room] = pressure;
                        RoomPressureBack[room] = pressure;
                    }

                    return;
                }

                float leakRate = FiniteNonNegativeOrZero(BaseLeakRatePerSecond[BaseId]);
                if (leakRate <= 0f)
                    return;

                float alpha = ResolveAnalyticalLeakAlpha(elapsed, leakRate);
                if (alpha <= 0f)
                    return;

                float ambientOxygen = FiniteNonNegativeOrZero(BaseAmbientOxygenKPa[BaseId]);
                for (int room = startRoom; room < roomEnd; room++)
                {
                    float oxygen = math.lerp(FiniteNonNegativeOrZero(RoomO2[room]), ambientOxygen, alpha);
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
                float exponent = -FiniteNonNegativeOrZero(elapsedSeconds) * FiniteNonNegativeOrZero(leakRatePerSecond);
                float alpha = 1f - math.exp(exponent);
                return math.isfinite(alpha) ? math.saturate(alpha) : 0f;
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
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = false)]
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

            [ReadOnly] public NativeArray<float> RoomO2Front;
            [ReadOnly] public NativeArray<float> RoomCO2Front;
            [ReadOnly] public NativeArray<float> RoomNitrogenFront;
            public NativeArray<float> RoomO2Back;
            public NativeArray<float> RoomCO2Back;
            public NativeArray<float> RoomNitrogenBack;
            public NativeArray<float> RoomPressureBack;
            [ReadOnly] public NativeArray<float> RoomAmbientPressure;
            [ReadOnly] public NativeArray<float> RoomSubmerged01;
            [ReadOnly] public NativeArray<float> RoomPlayerStress01;
            [ReadOnly] public NativeArray<float> RoomPlayerHeartRateBpm;
            [ReadOnly] public NativeArray<float> RoomTemperatureCelsius;
            [ReadOnly] public NativeArray<byte> RoomPlayerPresent;
            [ReadOnly] public NativeArray<byte> RoomScrubberPowered;
            [ReadOnly] public NativeArray<ushort> RoomFlags;
            [ReadOnly] public NativeArray<int> RoomBaseIndex;
            [ReadOnly] public NativeArray<byte> BaseAwakeState;
            [ReadOnly] public NativeArray<int> BulkheadRoomA;
            [ReadOnly] public NativeArray<int> BulkheadRoomB;
            [ReadOnly] public NativeArray<byte> BulkheadSealed;
            public NativeQueue<ToxicitySignal>.ParallelWriter ToxicitySignals;
            public NativeArray<GasDynamicsTelemetryEntry> TelemetryRing;

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
                float dt = math.max(0f, DeltaTime);
                for (int room = 0; room < roomLimit; room++)
                {
                    float oxygen = FiniteNonNegativeOrZero(RoomO2Front[room]);
                    float carbonDioxide = FiniteNonNegativeOrZero(RoomCO2Front[room]);
                    float nitrogen = FiniteNonNegativeOrZero(RoomNitrogenFront[room]);
                    ushort flags = RoomFlags[room];

                    if (!IsRoomAwake(room))
                    {
                        RoomO2Back[room] = oxygen;
                        RoomCO2Back[room] = carbonDioxide;
                        RoomNitrogenBack[room] = nitrogen;
                        RoomPressureBack[room] = ResolveDaltonPressureKPa(oxygen, carbonDioxide, nitrogen);
                        continue;
                    }

                    if (RoomPlayerPresent[room] != 0)
                    {
                        float stress = FiniteSaturate01(RoomPlayerStress01[room]);
                        float heartRate = FiniteNonNegativeOrZero(RoomPlayerHeartRateBpm[room]);
                        float heartRateMultiplier = heartRate > 2f
                            ? math.clamp(heartRate * math.rcp(70f), 0.5f, 3f)
                            : math.lerp(0.8f, 1.8f, math.saturate(heartRate));
                        float metabolicMultiplier = (1f + stress) * heartRateMultiplier;
                        float oxygenUsed = PlayerO2KPaPerSecond * metabolicMultiplier * dt;
                        float carbonDioxideProduced = PlayerCO2KPaPerSecond * metabolicMultiplier * dt;
                        oxygen = math.max(0f, oxygen - oxygenUsed);
                        carbonDioxide += carbonDioxideProduced;
                    }

                    if ((flags & RoomFlagInternalFire) != 0)
                    {
                        float burnedOxygen = FireO2KPaPerSecond * 5f * dt;
                        burnedOxygen = math.min(oxygen, burnedOxygen);
                        oxygen -= burnedOxygen;
                        carbonDioxide += burnedOxygen;
                    }

                    if (RoomScrubberPowered[room] != 0)
                    {
                        float roomTemperature = RoomTemperatureCelsius.IsCreated && room < RoomTemperatureCelsius.Length
                            ? RoomTemperatureCelsius[room]
                            : DefaultRoomTemperatureCelsius;
                        float scrubberScale = roomTemperature < 0f ? FreezingScrubberEfficiencyScale : 1f;
                        carbonDioxide = math.max(0f, carbonDioxide - ScrubberKPaPerSecond * scrubberScale * dt);
                    }

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
                if (diffusionFraction > 0f)
                {
                    for (int edge = 0; edge < bulkheadLimit; edge++)
                    {
                        if (BulkheadSealed[edge] != 0)
                            continue;

                        int roomA = BulkheadRoomA[edge];
                        int roomB = BulkheadRoomB[edge];
                        if ((uint)roomA >= (uint)roomLimit || (uint)roomB >= (uint)roomLimit || roomA == roomB)
                            continue;
                        if (!IsRoomAwake(roomA) || !IsRoomAwake(roomB))
                            continue;

                        float oxygenA = RoomO2Back[roomA];
                        float oxygenB = RoomO2Back[roomB];
                        float carbonDioxideA = RoomCO2Back[roomA];
                        float carbonDioxideB = RoomCO2Back[roomB];
                        float nitrogenA = RoomNitrogenBack[roomA];
                        float nitrogenB = RoomNitrogenBack[roomB];

                        DiffuseGas(ref oxygenA, ref oxygenB, diffusionFraction);
                        DiffuseGas(ref carbonDioxideA, ref carbonDioxideB, diffusionFraction);
                        DiffuseGas(ref nitrogenA, ref nitrogenB, diffusionFraction);

                        RoomO2Back[roomA] = oxygenA;
                        RoomO2Back[roomB] = oxygenB;
                        RoomCO2Back[roomA] = carbonDioxideA;
                        RoomCO2Back[roomB] = carbonDioxideB;
                        RoomNitrogenBack[roomA] = nitrogenA;
                        RoomNitrogenBack[roomB] = nitrogenB;
                        RoomPressureBack[roomA] = ResolveDaltonPressureKPa(oxygenA, carbonDioxideA, nitrogenA);
                        RoomPressureBack[roomB] = ResolveDaltonPressureKPa(oxygenB, carbonDioxideB, nitrogenB);
                    }
                }

                float totalOxygen = 0f;
                float totalCarbonDioxide = 0f;
                float totalNitrogen = 0f;
                float maxPressure = 0f;
                uint stateHash = 2166136261u;
                ushort telemetryFlags = 0;
                int signalsWritten = 0;
                int sleepingRoomCount = 0;
                for (int room = 0; room < roomLimit; room++)
                {
                    ushort flags = RoomFlags[room];
                    float oxygen = RoomO2Back[room];
                    float carbonDioxide = RoomCO2Back[room];
                    float nitrogen = RoomNitrogenBack[room];
                    bool roomAwake = IsRoomAwake(room);
                    if (!roomAwake)
                    {
                        sleepingRoomCount++;
                        telemetryFlags |= TelemetryFlagHibernating;
                    }

                    if (roomAwake && (flags & RoomFlagBreached) != 0)
                    {
                        oxygen = 0f;
                        carbonDioxide = 0f;
                        nitrogen = FiniteNonNegativeOrZero(RoomAmbientPressure[room]);
                        telemetryFlags |= TelemetryFlagBreach;
                    }

                    if (roomAwake && RoomSubmerged01.IsCreated && room < RoomSubmerged01.Length)
                    {
                        float dryFraction01 = 1f - FiniteSaturate01(RoomSubmerged01[room]);
                        oxygen = math.min(oxygen, StandardOxygenKPa * dryFraction01);
                    }

                    if (!math.isfinite(oxygen) || !math.isfinite(carbonDioxide) || !math.isfinite(nitrogen))
                        telemetryFlags |= TelemetryFlagNaN;

                    oxygen = FiniteNonNegativeOrZero(oxygen);
                    carbonDioxide = FiniteNonNegativeOrZero(carbonDioxide);
                    nitrogen = FiniteNonNegativeOrZero(nitrogen);
                    float pressure = ResolveDaltonPressureKPa(oxygen, carbonDioxide, nitrogen);

                    RoomO2Back[room] = oxygen;
                    RoomCO2Back[room] = carbonDioxide;
                    RoomNitrogenBack[room] = nitrogen;
                    RoomPressureBack[room] = pressure;

                    float pressureAtm = pressure * math.rcp(KPaPerAtmosphere);
                    float toxicity01 = ResolveToxicity01(carbonDioxide, Co2ToxicityThresholdKPa, Co2FatalKPa);
                    float narcosis01 = ResolveNarcosis01(pressureAtm, NarcosisThresholdAtm, NarcosisFullAtm);
                    if (roomAwake && (toxicity01 > 0f || narcosis01 > 0f) && signalsWritten < ToxicitySignalSoftCapacity)
                    {
                        ushort signalFlags = 0;
                        signalFlags |= (ushort)math.select(0, ToxicityFlagCO2, toxicity01 > 0f);
                        signalFlags |= (ushort)math.select(0, ToxicityFlagNarcosis, narcosis01 > 0f);
                        ToxicitySignals.Enqueue(new ToxicitySignal(
                            room,
                            carbonDioxide,
                            pressureAtm,
                            toxicity01,
                            narcosis01,
                            FrameIndex,
                            signalFlags));
                        signalsWritten++;
                    }

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

                TelemetryRing[TelemetryWriteIndex] = new GasDynamicsTelemetryEntry
                {
                    FrameIndex = FrameIndex,
                    RoomCount = roomLimit,
                    TotalO2KPa = totalOxygen,
                    TotalCO2KPa = totalCarbonDioxide,
                    TotalNitrogenKPa = totalNitrogen,
                    MaxPressureKPa = maxPressure,
                    StateHash = stateHash,
                    Flags = telemetryFlags,
                    Reserved = (ushort)math.min(ushort.MaxValue, sleepingRoomCount)
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsRoomAwake(int roomIndex)
            {
                if (!BaseAwakeState.IsCreated || !RoomBaseIndex.IsCreated || (uint)roomIndex >= (uint)RoomBaseIndex.Length)
                    return true;

                int baseIndex = RoomBaseIndex[roomIndex];
                if ((uint)baseIndex >= (uint)BaseAwakeState.Length)
                    return true;

                return BaseAwakeState[baseIndex] != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void DiffuseGas(ref float roomA, ref float roomB, float fraction)
            {
                float delta = (roomA - roomB) * fraction;
                if (delta > 0f)
                {
                    delta = math.min(delta, roomA);
                }
                else
                {
                    delta = -math.min(-delta, roomB);
                }

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
                return math.isfinite(value) ? math.saturate(value) : 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float FiniteNonNegativeOrZero(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }
        }

        private struct GasDynamicsMemoryAuditAccumulator
        {
            public int AllocationCount;
            public long RegisteredBytes;
            public long LargestAllocationBytes;
            public uint LargestAllocationLabelHash;
        }

        [StructLayout(LayoutKind.Sequential, Size = TelemetryEntrySizeBytes)]
        private struct GasDynamicsTelemetryEntry
        {
            public uint FrameIndex;
            public int RoomCount;
            public float TotalO2KPa;
            public float TotalCO2KPa;
            public float TotalNitrogenKPa;
            public float MaxPressureKPa;
            public uint StateHash;
            public ushort Flags;
            public ushort Reserved;
        }
    }
}
