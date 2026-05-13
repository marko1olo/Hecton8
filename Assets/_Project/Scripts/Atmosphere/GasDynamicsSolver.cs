using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
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
    public sealed class GasDynamicsSolver : MonoBehaviour, IGasDynamicsSolver, IFixedTickable, IPostFixedTickable
    {
        private const int MaxRoomCapacity = 128;
        private const int MaxBulkheadCapacity = 256;
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
        private const float MaxDiffusionFractionPerStep = 0.45f;
        private const ushort TelemetryFlagNaN = 1 << 0;
        private const ushort TelemetryFlagBreach = 1 << 1;
        private const ushort ToxicityFlagCO2 = 1 << 0;
        private const ushort ToxicityFlagNarcosis = 1 << 1;
        private const ushort RoomFlagInternalFire = (ushort)GasDynamicsRoomFlags.InternalFire;
        private const ushort RoomFlagBreached = (ushort)GasDynamicsRoomFlags.Breached;
        private const ushort RoomFlagOccupied = (ushort)GasDynamicsRoomFlags.Occupied;
        private const string NativeMemoryOwner = nameof(GasDynamicsSolver);
        private const string DumpFileName = "Dump_GAS_DYNAMICS_SOLVER.bin";

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
        private NativeArray<float> _roomPlayerStress01;
        private NativeArray<float> _roomPlayerHeartRateBpm;
        private NativeArray<float> _roomTemperatureCelsius;
        private NativeArray<byte> _roomPlayerPresent;
        private NativeArray<byte> _roomScrubberPowered;
        private NativeArray<ushort> _roomFlags;
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
        private bool _seededStandardAtmosphere;
        private bool _blackBoxDumped;
        private int _roomCount;
        private int _bulkheadCapacityLimit;
        private int _bulkheadCount;
        private int _activePlayerRoom = -1;
        private int _telemetryWriteIndex;
        private int _tickCount;
        private float _tickAccumulator;
        private float _lastCadenceSeconds = 2.0f;
        private GasDynamicsMathLod _lastMathLod = GasDynamicsMathLod.Low;

        public bool IsInitialized => RoomO2.IsCreated && _toxicitySignals.IsCreated;
        public int RoomCount => _roomCount;
        public float LastCadenceSeconds => _lastCadenceSeconds;
        public GasDynamicsMathLod LastMathLod => _lastMathLod;

        NativeArray<float>.ReadOnly IGasDynamicsSolver.RoomO2 => RoomO2.IsCreated ? RoomO2.AsReadOnly() : default;
        NativeArray<float>.ReadOnly IGasDynamicsSolver.RoomCO2 => RoomCO2.IsCreated ? RoomCO2.AsReadOnly() : default;
        NativeArray<float>.ReadOnly IGasDynamicsSolver.RoomPressure => RoomPressure.IsCreated ? RoomPressure.AsReadOnly() : default;
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

        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            if (!TryFinalizeDeferredNativeDisposal())
                return;

            EnsureNativeState();
            SeedStandardAtmosphereIfNeeded();
            TryRegisterRegistry();
            if (!TryCompleteStep())
            {
                _tickAccumulator += math.max(0f, fixedDeltaTime);
                return;
            }

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

        public bool TryGetRoomSnapshot(int roomId, out GasRoomSnapshot snapshot)
        {
            snapshot = default;
            if (_stepRunning || !RoomO2.IsCreated || roomId < 0 || roomId >= _roomCount)
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

        public bool TryConfigureRoom(
            int roomId,
            float oxygenKPa,
            float carbonDioxideKPa,
            float nitrogenKPa,
            float ambientPressureKPa,
            ushort flags)
        {
            if (_stepRunning || !RoomO2.IsCreated || roomId < 0 || roomId >= RoomO2.Length)
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
            if (_roomPlayerPresent.IsCreated && _roomPlayerPresent[roomId] != 0)
                flags = (ushort)(flags | RoomFlagOccupied);
            _roomFlags[roomId] = flags;
            if (roomId >= _roomCount)
                _roomCount = roomId + 1;
            return true;
        }

        public bool TrySetBulkhead(int edgeIndex, int roomA, int roomB, bool sealedBulkhead)
        {
            if (_stepRunning || !_bulkheadRoomA.IsCreated || edgeIndex < 0 || edgeIndex >= _bulkheadCapacityLimit)
                return false;

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
            if (_stepRunning || !RoomO2.IsCreated)
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

        public bool TrySetRoomFlags(int roomId, ushort setMask, ushort clearMask)
        {
            if (_stepRunning || !_roomFlags.IsCreated || roomId < 0 || roomId >= _roomCount)
                return false;

            ushort flags = (ushort)((_roomFlags[roomId] | setMask) & ~clearMask);
            if (_roomPlayerPresent.IsCreated && _roomPlayerPresent[roomId] != 0)
                flags = (ushort)(flags | RoomFlagOccupied);
            _roomFlags[roomId] = flags;
            return true;
        }

        public bool TrySetAmbientPressure(int roomId, float ambientPressureKPa)
        {
            if (_stepRunning || !_roomAmbientPressure.IsCreated || roomId < 0 || roomId >= _roomCount)
                return false;

            _roomAmbientPressure[roomId] = FiniteNonNegativeOrZero(ambientPressureKPa);
            return true;
        }

        public bool TrySetScrubberPowered(int roomId, bool powerActive)
        {
            if (_stepRunning || !_roomScrubberPowered.IsCreated || roomId < 0 || roomId >= _roomCount)
                return false;

            _roomScrubberPowered[roomId] = (byte)(powerActive ? 1 : 0);
            return true;
        }

        public bool TrySetRoomTemperatureCelsius(int roomId, float temperatureCelsius)
        {
            if (_stepRunning || !_roomTemperatureCelsius.IsCreated || roomId < 0 || roomId >= _roomCount)
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
            AccumulateAudit(_roomPlayerStress01, nameof(_roomPlayerStress01), ref accumulator);
            AccumulateAudit(_roomPlayerHeartRateBpm, nameof(_roomPlayerHeartRateBpm), ref accumulator);
            AccumulateAudit(_roomTemperatureCelsius, nameof(_roomTemperatureCelsius), ref accumulator);
            AccumulateAudit(_roomPlayerPresent, nameof(_roomPlayerPresent), ref accumulator);
            AccumulateAudit(_roomScrubberPowered, nameof(_roomScrubberPowered), ref accumulator);
            AccumulateAudit(_roomFlags, nameof(_roomFlags), ref accumulator);
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
            if (!RoomPressure.IsCreated || roomId < 0 || roomId >= _roomCount)
                return depthStress01;

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

        private void TryRegisterTicks()
        {
            if (_registeredTicks)
                return;

            bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            bool postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            if (!fixedRegistered || !postFixedRegistered)
            {
                if (fixedRegistered)
                    GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                if (postFixedRegistered)
                    GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
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
            _registeredTicks = false;
        }

        private void EnsureNativeState()
        {
            if (RoomO2.IsCreated)
                return;

            int safeRoomCapacity = math.clamp(roomCapacity, 1, MaxRoomCapacity);
            int safeBulkheadCapacity = math.clamp(bulkheadCapacity, 0, MaxBulkheadCapacity);
            roomCapacity = safeRoomCapacity;
            bulkheadCapacity = safeBulkheadCapacity;
            _roomCount = safeRoomCapacity;
            _bulkheadCapacityLimit = safeBulkheadCapacity;
            _bulkheadCount = 0;

            RoomO2 = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[roomCapacity] - oxygen partial pressure kPa - owner: GasDynamicsSolver
            RoomCO2 = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[roomCapacity] - carbon dioxide partial pressure kPa - owner: GasDynamicsSolver
            RoomPressure = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[roomCapacity] - Dalton total pressure kPa - owner: GasDynamicsSolver
            _roomO2Back = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomCO2Back = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomNitrogen = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomNitrogenBack = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPressureBack = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomAmbientPressure = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPlayerStress01 = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPlayerHeartRateBpm = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomTemperatureCelsius = new NativeArray<float>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomPlayerPresent = new NativeArray<byte>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomScrubberPowered = new NativeArray<byte>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomFlags = new NativeArray<ushort>(safeRoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
            RegisterNativeArray(_roomPlayerStress01, nameof(_roomPlayerStress01));
            RegisterNativeArray(_roomPlayerHeartRateBpm, nameof(_roomPlayerHeartRateBpm));
            RegisterNativeArray(_roomTemperatureCelsius, nameof(_roomTemperatureCelsius));
            RegisterNativeArray(_roomPlayerPresent, nameof(_roomPlayerPresent));
            RegisterNativeArray(_roomScrubberPowered, nameof(_roomScrubberPowered));
            RegisterNativeArray(_roomFlags, nameof(_roomFlags));
            RegisterNativeArray(_bulkheadRoomA, nameof(_bulkheadRoomA));
            RegisterNativeArray(_bulkheadRoomB, nameof(_bulkheadRoomB));
            RegisterNativeArray(_bulkheadSealed, nameof(_bulkheadSealed));
            RegisterNativeArray(_telemetryRing, nameof(_telemetryRing));
            NativeMemorySentinel.RegisterNativeQueue(_toxicitySignals, ToxicitySignalSoftCapacity, NativeMemoryOwner, nameof(_toxicitySignals), NativeAllocationLifetime.Scene);
            PrewarmQueue(ref _toxicitySignals, ToxicitySignalSoftCapacity);
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
            if (_stepRunning || !RoomO2.IsCreated)
                return;

            TrimToxicityQueueBeforeSchedule();
            float co2Threshold = FiniteNonNegativeOrZero(co2ToxicityThresholdKPa);
            float co2Fatal = math.max(co2Threshold + 0.01f, FiniteNonNegativeOrZero(co2FatalKPa));
            float narcosisThreshold = math.max(1f, FiniteNonNegativeOrZero(narcosisThresholdAtm));
            float narcosisFull = math.max(narcosisThreshold + 0.01f, FiniteNonNegativeOrZero(narcosisFullAtm));
            int writeIndex = _telemetryWriteIndex;
            _telemetryWriteIndex = (_telemetryWriteIndex + 1) % TelemetryCapacity;
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
                RoomPlayerStress01 = _roomPlayerStress01,
                RoomPlayerHeartRateBpm = _roomPlayerHeartRateBpm,
                RoomTemperatureCelsius = _roomTemperatureCelsius,
                RoomPlayerPresent = _roomPlayerPresent,
                RoomScrubberPowered = _roomScrubberPowered,
                RoomFlags = _roomFlags,
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

            int lastIndex = (_telemetryWriteIndex + TelemetryCapacity - 1) % TelemetryCapacity;
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
                    writer.Write(TelemetryCapacity);
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
            DisposeArray(ref _roomPlayerStress01, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomPlayerHeartRateBpm, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomTemperatureCelsius, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomPlayerPresent, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomScrubberPowered, ref disposeHandle, waitForStep);
            DisposeArray(ref _roomFlags, ref disposeHandle, waitForStep);
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
            [ReadOnly] public NativeArray<float> RoomPlayerStress01;
            [ReadOnly] public NativeArray<float> RoomPlayerHeartRateBpm;
            [ReadOnly] public NativeArray<float> RoomTemperatureCelsius;
            [ReadOnly] public NativeArray<byte> RoomPlayerPresent;
            [ReadOnly] public NativeArray<byte> RoomScrubberPowered;
            [ReadOnly] public NativeArray<ushort> RoomFlags;
            [ReadOnly] public NativeArray<int> BulkheadRoomA;
            [ReadOnly] public NativeArray<int> BulkheadRoomB;
            [ReadOnly] public NativeArray<byte> BulkheadSealed;
            public NativeQueue<ToxicitySignal>.ParallelWriter ToxicitySignals;
            public NativeArray<GasDynamicsTelemetryEntry> TelemetryRing;

            public void Execute()
            {
                int roomLimit = math.min(RoomCount, RoomO2Front.Length);
                float dt = math.max(0f, DeltaTime);
                for (int room = 0; room < roomLimit; room++)
                {
                    float oxygen = FiniteNonNegativeOrZero(RoomO2Front[room]);
                    float carbonDioxide = FiniteNonNegativeOrZero(RoomCO2Front[room]);
                    float nitrogen = FiniteNonNegativeOrZero(RoomNitrogenFront[room]);
                    ushort flags = RoomFlags[room];

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
                int bulkheadLimit = math.min(BulkheadCount, BulkheadRoomA.Length);
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
                for (int room = 0; room < roomLimit; room++)
                {
                    ushort flags = RoomFlags[room];
                    float oxygen = RoomO2Back[room];
                    float carbonDioxide = RoomCO2Back[room];
                    float nitrogen = RoomNitrogenBack[room];

                    if ((flags & RoomFlagBreached) != 0)
                    {
                        oxygen = 0f;
                        carbonDioxide = 0f;
                        nitrogen = FiniteNonNegativeOrZero(RoomAmbientPressure[room]);
                        telemetryFlags |= TelemetryFlagBreach;
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
                    if ((toxicity01 > 0f || narcosis01 > 0f) && signalsWritten < ToxicitySignalSoftCapacity)
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
                    Reserved = 0
                };
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
