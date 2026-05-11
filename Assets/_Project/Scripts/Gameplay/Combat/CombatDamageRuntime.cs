using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum CombatEntityKind : byte
    {
        Generic = 0,
        Player = 1,
        Habitat = 2,
        Fauna = 3,
        Flora = 4,
        Submarine = 5
    }

    public enum CombatArmorClass : byte
    {
        None = 0,
        Suit = 1,
        Shell = 2,
        Structure = 3,
        OrganicHeavy = 4,
        Brittle = 5,
        Shielded = 6,
        Reserved = 7
    }

    public enum CombatMathLod : byte
    {
        Low = 0,
        High = 1
    }

    public enum CombatWeakspotTier : byte
    {
        None = 0,
        Weakspot = 1
    }

    public static class CombatStatusBits
    {
        public const uint Bleeding = 1u << 0;
        public const uint Poisoned = 1u << 1;
        public const uint Burning = 1u << 2;
        public const uint Stunned = 1u << 3;
        public const uint Brittle = 1u << 4;
    }

    public static class CombatDamageTypes
    {
        public const uint Pressure = 1u << 0;
        public const uint Thermal = 1u << 1;
        public const uint Impact = 1u << 2;
        public const uint Parasite = 1u << 3;
        public const uint Radioactive = 1u << 4;
        public const uint Toxic = 1u << 5;
        public const uint Emp = 1u << 6;
        public const uint MicroFracture = 1u << 7;
    }

    public static class CombatDamageResultFlags
    {
        public const byte None = 0;
        public const byte WoundTrigger = 1 << 0;
        public const byte ShieldAbsorbed = 1 << 1;
        public const byte TargetKilled = 1 << 2;
        public const byte StatusChanged = 1 << 3;
        public const byte HitHud = 1 << 4;
        public const byte BloodScent = 1 << 5;
        public const byte CriticalFailure = 1 << 6;
        public const byte HighFidelityWound = 1 << 7;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct CombatDamageSignal
    {
        public int TargetId;
        public int SourceId;
        public float Amount;
        public float ImpulseMagnitude;
        public float3 Direction;
        public uint PackedMeta;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct CombatDamageSignalDetail
    {
        public float3 LocalPoint;
        public float3 ArmorNormal;
        public float LocalTemperatureCelsius;
        public float StatusDurationSeconds;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    public struct CombatDamageResult
    {
        public int TargetId;
        public int SourceId;
        public uint DamageType;
        public uint StatusBits;
        public float PreviousHealth;
        public float NextHealth;
        public float AppliedDamage;
        public float MaxHealth;
        public float3 Direction;
        public byte TraumaLevel;
        public byte Flags;
        public byte Channel;
        public byte DirectionOctant;
        public float3 LocalPoint;
        public float Depth;
    }

    public interface ICombatDamageEventListener
    {
        void OnCombatDamageResolved(in CombatDamageResult result);
    }

    public static class CombatDamageRuntime
    {
        private const int MaxTargets = 2048;
        private const int MaxQueuedSignals = 1024;
        private const int MaxResults = 1024;
        private const int ListenerCapacity = 16;
        private const int DamageClassCount = 8;
        private const int ArmorClassCount = 8;
        private const int DamageArmorLutLength = DamageClassCount * ArmorClassCount;
        private const int StatusBatchSize = 64;
        private const float ThermalBurnThresholdCelsius = 100f;
        private const float ThermalBrittleThresholdCelsius = 0f;
        private const float DefaultThermalStatusDurationSeconds = 4f;
        private const float DefaultPoisonStatusDurationSeconds = 5f;
        private const float DefaultBleedStatusDurationSeconds = 6f;
        private const float DefaultStunStatusDurationSeconds = 0.75f;
        private const float ShieldAbsorbFraction = 0.8f;
        private const float BrittleImpactMultiplier = 1.25f;
        private const float PoisonDamagePerSlowTick = 1f;
        private const float BurningDamagePerSlowTick = 1.5f;
        private const float WoundThresholdFraction = 0.2f;
        private const float CriticalFailureHealthFraction = 0.1f;
        private const uint TargetFlagArmorMask = 0xFu;
        private const int TargetFlagKindShift = 4;
        private const int CounterResultCount = 0;
        private const int CounterDroppedResults = 1;
        private const int CounterMissingTargets = 2;
        private const int CounterProcessedSignals = 3;
        private const int CounterLength = 4;
        private const int MetaDamageTypeShift = 0;
        private const int MetaStatusBitsShift = 8;
        private const int MetaWeakspotTierShift = 13;
        private const int MetaDetailIndexShift = 15;
        private const uint MetaDamageTypeMask = 0xFFu;
        private const uint MetaStatusBitsMask = 0x1Fu;
        private const uint MetaWeakspotTierMask = 0x3u;
        private const uint MetaDetailIndexMask = 0x3FFu;
        private const uint MetaDetailIndexClearMask = ~(MetaDetailIndexMask << MetaDetailIndexShift);

        private static readonly ProfilerMarker _scheduleMarker = new ProfilerMarker("CombatDamageRuntime.Schedule");
        private static readonly ProfilerMarker _lateFrameMarker = new ProfilerMarker("CombatDamageRuntime.LateFrame");
        private static readonly ProfilerMarker _slowTickMarker = new ProfilerMarker("CombatDamageRuntime.SlowTick");
        private static readonly RegistryBucket<ICombatDamageEventListener> _listeners =
            new RegistryBucket<ICombatDamageEventListener>(ListenerCapacity);

        private static NativeQueue<CombatDamageSignal> _damageSignals;
        private static NativeArray<CombatDamageSignalDetail> _signalDetails;
        private static NativeParallelHashMap<int, int> _slotByTargetId;
        private static NativeArray<int> _instanceIds;
        private static NativeArray<float> _health;
        private static NativeArray<float> _maxHealth;
        private static NativeArray<float> _invMaxHealth;
        private static NativeArray<float> _armorValues;
        private static NativeArray<float> _shieldValues;
        private static NativeArray<float> _minorDamageAccumulators;
        private static NativeArray<uint> _targetFlags;
        private static NativeArray<uint> _statusMasks;
        private static NativeArray<float4> _statusDurations0123;
        private static NativeArray<float> _brittleDurations;
        private static NativeArray<float> _damageArmorLut;
        private static NativeArray<CombatDamageResult> _results;
        private static NativeArray<CombatDamageResult> _statusResults;
        private static NativeArray<byte> _statusResultActive;
        private static NativeArray<int> _counters;
        private static IDamageReceiver[] _receivers;
        private static int _targetCount;
        private static int _queuedSignalCount;
        private static JobHandle _damageJobHandle;
        private static JobHandle _statusJobHandle;
        private static bool _damageJobScheduled;
        private static bool _statusJobScheduled;
        private static byte _mathLod = (byte)CombatMathLod.Low;

        public static bool IsInitialized => _damageSignals.IsCreated;
        public static int PendingSignalCount => _queuedSignalCount;

        public static void SetCombatMathLod(CombatMathLod lod)
        {
            _mathLod = (byte)lod;
        }

        public static uint PackSignalMeta(
            uint damageType,
            uint statusBits,
            CombatWeakspotTier weakspotTier)
        {
            return ((damageType & MetaDamageTypeMask) << MetaDamageTypeShift) |
                   ((statusBits & MetaStatusBitsMask) << MetaStatusBitsShift) |
                   (((uint)weakspotTier & MetaWeakspotTierMask) << MetaWeakspotTierShift);
        }

        public static int ResolveTargetId(GameObject owner)
        {
            if (owner == null)
                return 0;

            return unchecked((int)EntityId.ToULong(owner.GetEntityId()));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        public static void Register(ICombatDamageEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(ICombatDamageEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.TryUnregister(listener);
        }

        public static bool RegisterTarget(
            int targetId,
            IDamageReceiver receiver,
            float currentHealth,
            float maximumHealth,
            CombatEntityKind kind,
            CombatArmorClass armorClass,
            float armorValue,
            float shieldValue)
        {
            if (targetId == 0 || receiver == null)
                return false;

            EnsureInitialized();
            if (!CanMutateTargets())
                return false;

            float safeMaxHealth = math.max(0.0001f, maximumHealth);
            float safeHealth = math.clamp(currentHealth, 0f, safeMaxHealth);
            int slot;
            if (_slotByTargetId.TryGetValue(targetId, out slot))
            {
                _receivers[slot] = receiver;
                _health[slot] = safeHealth;
                _maxHealth[slot] = safeMaxHealth;
                _invMaxHealth[slot] = math.rcp(safeMaxHealth);
                _armorValues[slot] = math.max(0f, armorValue);
                _shieldValues[slot] = math.max(0f, shieldValue);
                _targetFlags[slot] = PackTargetFlags(kind, armorClass);
                return true;
            }

            if (_targetCount >= MaxTargets)
                return false;

            slot = _targetCount;
            if (!_slotByTargetId.TryAdd(targetId, slot))
                return false;

            _targetCount++;
            _receivers[slot] = receiver;
            _instanceIds[slot] = targetId;
            _health[slot] = safeHealth;
            _maxHealth[slot] = safeMaxHealth;
            _invMaxHealth[slot] = math.rcp(safeMaxHealth);
            _armorValues[slot] = math.max(0f, armorValue);
            _shieldValues[slot] = math.max(0f, shieldValue);
            _targetFlags[slot] = PackTargetFlags(kind, armorClass);
            _statusMasks[slot] = 0u;
            _statusDurations0123[slot] = float4.zero;
            _brittleDurations[slot] = 0f;
            return true;
        }

        public static bool UnregisterTarget(int targetId, IDamageReceiver receiver)
        {
            if (!_slotByTargetId.IsCreated || targetId == 0)
                return false;

            if (!CanMutateTargets())
                return false;

            int slot;
            if (!_slotByTargetId.TryGetValue(targetId, out slot))
                return false;

            if (receiver != null && !ReferenceEquals(_receivers[slot], receiver))
                return false;

            int lastSlot = _targetCount - 1;
            _slotByTargetId.Remove(targetId);
            if (slot != lastSlot)
            {
                int movedId = _instanceIds[lastSlot];
                _instanceIds[slot] = movedId;
                _health[slot] = _health[lastSlot];
                _maxHealth[slot] = _maxHealth[lastSlot];
                _invMaxHealth[slot] = _invMaxHealth[lastSlot];
                _armorValues[slot] = _armorValues[lastSlot];
                _shieldValues[slot] = _shieldValues[lastSlot];
                _minorDamageAccumulators[slot] = _minorDamageAccumulators[lastSlot];
                _targetFlags[slot] = _targetFlags[lastSlot];
                _statusMasks[slot] = _statusMasks[lastSlot];
                _statusDurations0123[slot] = _statusDurations0123[lastSlot];
                _brittleDurations[slot] = _brittleDurations[lastSlot];
                _receivers[slot] = _receivers[lastSlot];
                _slotByTargetId[movedId] = slot;
            }

            ClearSlot(lastSlot);
            _targetCount = lastSlot;
            return true;
        }

        public static bool IsTargetRegistered(int targetId)
        {
            return _slotByTargetId.IsCreated && _slotByTargetId.ContainsKey(targetId);
        }

        public static bool SyncTargetHealth(int targetId, float currentHealth, float maximumHealth)
        {
            if (!_slotByTargetId.IsCreated || !CanMutateTargets())
                return false;

            int slot;
            if (!_slotByTargetId.TryGetValue(targetId, out slot))
                return false;

            float safeMaxHealth = math.max(0.0001f, maximumHealth);
            _health[slot] = math.clamp(currentHealth, 0f, safeMaxHealth);
            _maxHealth[slot] = safeMaxHealth;
            _invMaxHealth[slot] = math.rcp(safeMaxHealth);
            return true;
        }

        public static bool TryQueueDamage(in CombatDamageSignal signal)
        {
            CombatDamageSignalDetail detail = default;
            return TryQueueDamage(in signal, in detail);
        }

        public static bool TryQueueDamage(in CombatDamageSignal signal, in CombatDamageSignalDetail detail)
        {
            if (signal.TargetId == 0)
                return false;

            EnsureInitialized();
            if (_damageJobScheduled || _queuedSignalCount >= MaxQueuedSignals)
                return false;

            int detailIndex = _queuedSignalCount;
            CombatDamageSignal queuedSignal = signal;
            queuedSignal.PackedMeta = (signal.PackedMeta & MetaDetailIndexClearMask) |
                                      ((uint)detailIndex << MetaDetailIndexShift);
            _signalDetails[detailIndex] = detail;
            _damageSignals.Enqueue(queuedSignal);
            _queuedSignalCount++;
            return true;
        }

        public static void FrameTick(float deltaTime)
        {
            if (!_damageSignals.IsCreated || _queuedSignalCount <= 0 || _damageJobScheduled || _statusJobScheduled)
                return;

            using (_scheduleMarker.Auto())
            {
                ClearCounters();
                ProcessDamageQueueJob job = new ProcessDamageQueueJob
                {
                    Signals = _damageSignals,
                    SignalDetails = _signalDetails,
                    SlotByTargetId = _slotByTargetId,
                    InstanceIds = _instanceIds,
                    Health = _health,
                    MaxHealth = _maxHealth,
                    InvMaxHealth = _invMaxHealth,
                    ArmorValues = _armorValues,
                    ShieldValues = _shieldValues,
                    MinorDamageAccumulators = _minorDamageAccumulators,
                    TargetFlags = _targetFlags,
                    StatusMasks = _statusMasks,
                    StatusDurations0123 = _statusDurations0123,
                    BrittleDurations = _brittleDurations,
                    DamageArmorLut = _damageArmorLut,
                    Results = _results,
                    Counters = _counters,
                    SignalBudget = MaxQueuedSignals,
                    MathLod = _mathLod
                };
                _damageJobHandle = job.Schedule();
                _damageJobScheduled = true;
                JobHandle.ScheduleBatchedJobs();
            }
        }

        public static void SlowTick(float deltaTime)
        {
            if (!_health.IsCreated || _targetCount <= 0 || _damageJobScheduled || _statusJobScheduled)
                return;

            using (_slowTickMarker.Auto())
            {
                ClearCounters();
                ProcessCombatStatusJob job = new ProcessCombatStatusJob
                {
                    DeltaTime = math.max(0f, deltaTime),
                    TargetCount = _targetCount,
                    InstanceIds = _instanceIds,
                    Health = _health,
                    MaxHealth = _maxHealth,
                    InvMaxHealth = _invMaxHealth,
                    TargetFlags = _targetFlags,
                    StatusMasks = _statusMasks,
                    StatusDurations0123 = _statusDurations0123,
                    BrittleDurations = _brittleDurations,
                    ResultsBySlot = _statusResults,
                    ResultActiveBySlot = _statusResultActive
                };
                _statusJobHandle = job.Schedule(_targetCount, StatusBatchSize);
                _statusJobScheduled = true;
                JobHandle.ScheduleBatchedJobs();
            }
        }

        public static void LateFrameTick()
        {
            if (!_damageJobScheduled && !_statusJobScheduled)
                return;

            using (_lateFrameMarker.Auto())
            {
                bool completedAny = false;
                bool completedStatus = false;
                if (_damageJobScheduled && DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: false))
                {
                    _damageJobScheduled = false;
                    _queuedSignalCount = 0;
                    completedAny = true;
                }

                if (_statusJobScheduled && DispatcherJobSwap.TryComplete(ref _statusJobHandle, forceComplete: false))
                {
                    _statusJobScheduled = false;
                    completedAny = true;
                    completedStatus = true;
                }

                if (!completedAny)
                    return;

                DispatchResults();
                if (completedStatus)
                    DispatchStatusResults();
            }
        }

        public static void Shutdown()
        {
            if (_damageJobScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: true);
                _damageJobScheduled = false;
            }

            if (_statusJobScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _statusJobHandle, forceComplete: true);
                _statusJobScheduled = false;
            }

            if (_damageSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(CombatDamageRuntime), nameof(_damageSignals));
                _damageSignals.Dispose();
                _damageSignals = default;
            }

            DisposeNativeArray(ref _signalDetails);
            if (_slotByTargetId.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(CombatDamageRuntime), nameof(_slotByTargetId));
                _slotByTargetId.Dispose();
                _slotByTargetId = default;
            }

            DisposeNativeArray(ref _instanceIds);
            DisposeNativeArray(ref _health);
            DisposeNativeArray(ref _maxHealth);
            DisposeNativeArray(ref _invMaxHealth);
            DisposeNativeArray(ref _armorValues);
            DisposeNativeArray(ref _shieldValues);
            DisposeNativeArray(ref _minorDamageAccumulators);
            DisposeNativeArray(ref _targetFlags);
            DisposeNativeArray(ref _statusMasks);
            DisposeNativeArray(ref _statusDurations0123);
            DisposeNativeArray(ref _brittleDurations);
            DisposeNativeArray(ref _damageArmorLut);
            DisposeNativeArray(ref _results);
            DisposeNativeArray(ref _statusResults);
            DisposeNativeArray(ref _statusResultActive);
            DisposeNativeArray(ref _counters);
            if (_receivers != null)
                System.Array.Clear(_receivers, 0, _receivers.Length);
            _receivers = null;
            _targetCount = 0;
            _queuedSignalCount = 0;
            _listeners.Clear();
        }

        private static void EnsureInitialized()
        {
            if (_damageSignals.IsCreated)
                return;

            _damageSignals = new NativeQueue<CombatDamageSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<CombatDamageSignal>[1024] - combat damage ingress lane - owner: CombatDamageRuntime
            NativeMemorySentinel.RegisterNativeQueue(
                _damageSignals,
                MaxQueuedSignals,
                nameof(CombatDamageRuntime),
                nameof(_damageSignals),
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _damageSignals, MaxQueuedSignals);
            _signalDetails = AllocateArray<CombatDamageSignalDetail>(MaxQueuedSignals, nameof(_signalDetails));

            _slotByTargetId = new NativeParallelHashMap<int, int>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeParallelHashMap<int,int>[2048] - target id to health slot map - owner: CombatDamageRuntime
            NativeMemorySentinel.RegisterNativeParallelHashMap(
                _slotByTargetId,
                nameof(CombatDamageRuntime),
                nameof(_slotByTargetId),
                NativeAllocationLifetime.Session);

            _instanceIds = AllocateArray<int>(MaxTargets, nameof(_instanceIds));
            _health = AllocateArray<float>(MaxTargets, nameof(_health));
            _maxHealth = AllocateArray<float>(MaxTargets, nameof(_maxHealth));
            _invMaxHealth = AllocateArray<float>(MaxTargets, nameof(_invMaxHealth));
            _armorValues = AllocateArray<float>(MaxTargets, nameof(_armorValues));
            _shieldValues = AllocateArray<float>(MaxTargets, nameof(_shieldValues));
            _minorDamageAccumulators = AllocateArray<float>(MaxTargets, nameof(_minorDamageAccumulators));
            _targetFlags = AllocateArray<uint>(MaxTargets, nameof(_targetFlags));
            _statusMasks = AllocateArray<uint>(MaxTargets, nameof(_statusMasks));
            _statusDurations0123 = AllocateArray<float4>(MaxTargets, nameof(_statusDurations0123));
            _brittleDurations = AllocateArray<float>(MaxTargets, nameof(_brittleDurations));
            _damageArmorLut = AllocateArray<float>(DamageArmorLutLength, nameof(_damageArmorLut));
            _results = AllocateArray<CombatDamageResult>(MaxResults, nameof(_results));
            _statusResults = AllocateArray<CombatDamageResult>(MaxTargets, nameof(_statusResults));
            _statusResultActive = AllocateArray<byte>(MaxTargets, nameof(_statusResultActive));
            _counters = AllocateArray<int>(CounterLength, nameof(_counters));
            _receivers = new IDamageReceiver[MaxTargets]; // COLD ALLOC: IDamageReceiver[2048] - managed fanout mirror for native target slots - owner: CombatDamageRuntime
            InitializeDamageArmorLut();
        }

        private static NativeArray<T> AllocateArray<T>(int length, string label)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                array,
                nameof(CombatDamageRuntime),
                label,
                NativeAllocationLifetime.Session);
            return array;
        }

        private static void InitializeDamageArmorLut()
        {
            for (int i = 0; i < DamageArmorLutLength; i++)
                _damageArmorLut[i] = 1f;

            SetLut(ResolveDamageClass(CombatDamageTypes.Impact), CombatArmorClass.Structure, 0.75f);
            SetLut(ResolveDamageClass(CombatDamageTypes.Impact), CombatArmorClass.OrganicHeavy, 0.65f);
            SetLut(ResolveDamageClass(CombatDamageTypes.Impact), CombatArmorClass.Brittle, 1.35f);
            SetLut(ResolveDamageClass(CombatDamageTypes.Thermal), CombatArmorClass.Shell, 0.8f);
            SetLut(ResolveDamageClass(CombatDamageTypes.Thermal), CombatArmorClass.Brittle, 1.2f);
            SetLut(ResolveDamageClass(CombatDamageTypes.Toxic), CombatArmorClass.Structure, 0.2f);
            SetLut(ResolveDamageClass(CombatDamageTypes.Emp), CombatArmorClass.Shielded, 1.35f);
            SetLut(ResolveDamageClass(CombatDamageTypes.Pressure), CombatArmorClass.Suit, 0.85f);
        }

        private static void SetLut(int damageClass, CombatArmorClass armorClass, float value)
        {
            int index = (damageClass * ArmorClassCount) + ((int)armorClass & 7);
            _damageArmorLut[index] = value;
        }

        private static bool CanMutateTargets()
        {
            if (_damageJobScheduled && !_damageJobHandle.IsCompleted)
                return false;
            if (_statusJobScheduled && !_statusJobHandle.IsCompleted)
                return false;

            if (_damageJobScheduled)
            {
                DispatcherJobSwap.TryFinalizeCompleted(ref _damageJobHandle);
                _damageJobScheduled = false;
            }

            if (_statusJobScheduled)
            {
                DispatcherJobSwap.TryFinalizeCompleted(ref _statusJobHandle);
                _statusJobScheduled = false;
            }

            return true;
        }

        private static void DispatchResults()
        {
            int resultCount = math.min(_counters[CounterResultCount], MaxResults);
            for (int resultIndex = 0; resultIndex < resultCount; resultIndex++)
            {
                CombatDamageResult result = _results[resultIndex];
                int slot;
                if (!_slotByTargetId.TryGetValue(result.TargetId, out slot))
                    continue;

                IDamageReceiver receiver = _receivers[slot];
                if (receiver == null)
                    continue;

                DamagePacket packet = new DamagePacket
                {
                    Channel = (DamageChannel)result.Channel,
                    PreviousValue = result.PreviousHealth,
                    NextValue = result.NextHealth,
                    Magnitude = result.AppliedDamage,
                    LocalPoint = result.LocalPoint,
                    DamageType = result.DamageType,
                    IntegrityDelta = QuantizeDelta(result.PreviousHealth, result.NextHealth, result.MaxHealth),
                    Depth = result.Depth,
                    SourceId = (ushort)math.clamp(result.SourceId, 0, ushort.MaxValue),
                    TraumaLevel = result.TraumaLevel
                };
                receiver.ReceiveDamage(in packet);
            }

            ICombatDamageEventListener[] listeners = _listeners.RawArray;
            int listenerCount = _listeners.Count;
            for (int resultIndex = 0; resultIndex < resultCount; resultIndex++)
            {
                CombatDamageResult result = _results[resultIndex];
                for (int listenerIndex = 0; listenerIndex < listenerCount; listenerIndex++)
                    listeners[listenerIndex].OnCombatDamageResolved(in result);
            }

            _counters[CounterResultCount] = 0;
        }

        private static void DispatchStatusResults()
        {
            ICombatDamageEventListener[] listeners = _listeners.RawArray;
            int listenerCount = _listeners.Count;
            int targetCount = _targetCount;
            for (int slot = 0; slot < targetCount; slot++)
            {
                if (_statusResultActive[slot] == 0)
                    continue;

                _statusResultActive[slot] = 0;
                CombatDamageResult result = _statusResults[slot];
                IDamageReceiver receiver = _receivers[slot];
                if (receiver != null)
                {
                    DamagePacket packet = new DamagePacket
                    {
                        Channel = (DamageChannel)result.Channel,
                        PreviousValue = result.PreviousHealth,
                        NextValue = result.NextHealth,
                        Magnitude = result.AppliedDamage,
                        LocalPoint = result.LocalPoint,
                        DamageType = result.DamageType,
                        IntegrityDelta = QuantizeDelta(result.PreviousHealth, result.NextHealth, result.MaxHealth),
                        Depth = result.Depth,
                        SourceId = (ushort)math.clamp(result.SourceId, 0, ushort.MaxValue),
                        TraumaLevel = result.TraumaLevel
                    };
                    receiver.ReceiveDamage(in packet);
                }

                for (int listenerIndex = 0; listenerIndex < listenerCount; listenerIndex++)
                    listeners[listenerIndex].OnCombatDamageResolved(in result);
            }
        }

        private static byte QuantizeDelta(float previousHealth, float nextHealth, float maximumHealth)
        {
            float invMax = math.rcp(math.max(0.0001f, maximumHealth));
            return (byte)math.clamp((int)math.round(math.abs(previousHealth - nextHealth) * invMax * byte.MaxValue), 0, byte.MaxValue);
        }

        private static void ClearCounters()
        {
            for (int i = 0; i < CounterLength; i++)
                _counters[i] = 0;
        }

        private static void ClearSlot(int slot)
        {
            _instanceIds[slot] = 0;
            _health[slot] = 0f;
            _maxHealth[slot] = 0f;
            _invMaxHealth[slot] = 0f;
            _armorValues[slot] = 0f;
            _shieldValues[slot] = 0f;
            _minorDamageAccumulators[slot] = 0f;
            _targetFlags[slot] = 0u;
            _statusMasks[slot] = 0u;
            _statusDurations0123[slot] = float4.zero;
            _brittleDurations[slot] = 0f;
            _receivers[slot] = null;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackTargetFlags(CombatEntityKind kind, CombatArmorClass armorClass)
        {
            return (((uint)kind & 0xFu) << TargetFlagKindShift) | ((uint)armorClass & TargetFlagArmorMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveDamageClass(uint damageType)
        {
            if ((damageType & CombatDamageTypes.Thermal) != 0u)
                return 1;
            if ((damageType & CombatDamageTypes.Toxic) != 0u)
                return 2;
            if ((damageType & CombatDamageTypes.Emp) != 0u)
                return 3;
            if ((damageType & CombatDamageTypes.Pressure) != 0u)
                return 4;
            if ((damageType & CombatDamageTypes.Parasite) != 0u)
                return 5;
            if ((damageType & CombatDamageTypes.Radioactive) != 0u)
                return 6;
            if ((damageType & CombatDamageTypes.MicroFracture) != 0u)
                return 7;

            return 0;
        }

        [BurstCompile]
        private struct ProcessDamageQueueJob : IJob
        {
            public NativeQueue<CombatDamageSignal> Signals;
            [ReadOnly] public NativeArray<CombatDamageSignalDetail> SignalDetails;
            [ReadOnly] public NativeParallelHashMap<int, int> SlotByTargetId;
            [ReadOnly] public NativeArray<int> InstanceIds;
            public NativeArray<float> Health;
            [ReadOnly] public NativeArray<float> MaxHealth;
            [ReadOnly] public NativeArray<float> InvMaxHealth;
            [ReadOnly] public NativeArray<float> ArmorValues;
            public NativeArray<float> ShieldValues;
            public NativeArray<float> MinorDamageAccumulators;
            [ReadOnly] public NativeArray<uint> TargetFlags;
            public NativeArray<uint> StatusMasks;
            public NativeArray<float4> StatusDurations0123;
            public NativeArray<float> BrittleDurations;
            [ReadOnly] public NativeArray<float> DamageArmorLut;
            public NativeArray<CombatDamageResult> Results;
            public NativeArray<int> Counters;
            public int SignalBudget;
            public byte MathLod;

            public void Execute()
            {
                int processed = 0;
                while (processed < SignalBudget && Signals.TryDequeue(out CombatDamageSignal signal))
                {
                    processed++;
                    if (!SlotByTargetId.TryGetValue(signal.TargetId, out int slot))
                    {
                        Counters[CounterMissingTargets] = Counters[CounterMissingTargets] + 1;
                        continue;
                    }

                    float maxHealth = math.max(0.0001f, MaxHealth[slot]);
                    float previousHealth = Health[slot];
                    if (previousHealth <= 0f)
                        continue;

                    uint targetFlags = TargetFlags[slot];
                    byte kind = (byte)((targetFlags >> TargetFlagKindShift) & 0xFu);
                    uint damageType = ReadDamageType(signal.PackedMeta);
                    uint signalStatusBits = ReadStatusBits(signal.PackedMeta);
                    CombatDamageSignalDetail detail = SignalDetails[ReadDetailIndex(signal.PackedMeta)];
                    int armorClass = (int)(targetFlags & TargetFlagArmorMask);
                    int damageClass = ResolveDamageClass(damageType);
                    float armorMultiplier = DamageArmorLut[(damageClass * ArmorClassCount) + armorClass];
                    if (MathLod == (byte)CombatMathLod.High)
                    {
                        float3 projectileDirection = ResolvePreciseDirection(signal.Direction);
                        float3 armorNormal = ResolvePreciseDirection(detail.ArmorNormal);
                        armorMultiplier *= math.saturate(math.dot(projectileDirection, armorNormal) + 0.2f);
                    }

                    int weakspotTier = ReadWeakspotTier(signal.PackedMeta);
                    float weakspotMultiplier = math.select(1f, 3f, weakspotTier == (int)CombatWeakspotTier.Weakspot);
                    float baseAmount = signal.Amount > 0f ? signal.Amount : math.max(0f, signal.ImpulseMagnitude);
                    float damage = math.max(0f, baseAmount * weakspotMultiplier * armorMultiplier);

                    uint statusMask = StatusMasks[slot];
                    uint statusBefore = statusMask;
                    if ((statusMask & CombatStatusBits.Brittle) != 0u && (damageType & CombatDamageTypes.Impact) != 0u)
                        damage *= BrittleImpactMultiplier;

                    float shield = ShieldValues[slot];
                    byte flags = CombatDamageResultFlags.None;
                    if (shield > 0f && damage > 0f)
                    {
                        float shieldAbsorb = math.min(shield, damage * ShieldAbsorbFraction);
                        shield -= shieldAbsorb;
                        damage -= shieldAbsorb;
                        ShieldValues[slot] = shield;
                        flags |= CombatDamageResultFlags.ShieldAbsorbed;
                    }

                    damage = math.max(0f, damage - ArmorValues[slot]);
                    if (detail.LocalTemperatureCelsius > ThermalBurnThresholdCelsius)
                    {
                        statusMask |= CombatStatusBits.Burning;
                        float4 durations = StatusDurations0123[slot];
                        durations.z = math.max(durations.z, DefaultThermalStatusDurationSeconds);
                        StatusDurations0123[slot] = durations;
                    }
                    else if (detail.LocalTemperatureCelsius < ThermalBrittleThresholdCelsius)
                    {
                        statusMask |= CombatStatusBits.Brittle;
                        BrittleDurations[slot] = math.max(BrittleDurations[slot], DefaultThermalStatusDurationSeconds);
                    }

                    if (signalStatusBits != 0u)
                    {
                        statusMask |= signalStatusBits;
                        float duration = detail.StatusDurationSeconds > 0f ? detail.StatusDurationSeconds : ResolveDefaultStatusDuration(signalStatusBits);
                        SetStatusDurations(slot, signalStatusBits, duration);
                    }

                    if (statusMask != statusBefore)
                        flags |= CombatDamageResultFlags.StatusChanged;
                    StatusMasks[slot] = statusMask;

                    if (damage > 0f && damage < 1f)
                    {
                        float accumulatedDamage = MinorDamageAccumulators[slot] + damage;
                        if (accumulatedDamage < 1f)
                        {
                            MinorDamageAccumulators[slot] = accumulatedDamage;
                            continue;
                        }

                        damage = accumulatedDamage;
                        MinorDamageAccumulators[slot] = 0f;
                    }

                    float nextHealth = math.max(0f, previousHealth - damage);
                    Health[slot] = nextHealth;
                    if (nextHealth <= 0f && previousHealth > 0f)
                        flags |= CombatDamageResultFlags.TargetKilled;

                    if (damage >= maxHealth * WoundThresholdFraction)
                        flags |= CombatDamageResultFlags.WoundTrigger;

                    if (kind == (byte)CombatEntityKind.Player && damage > 0f)
                        flags |= CombatDamageResultFlags.HitHud;
                    if (kind == (byte)CombatEntityKind.Fauna && nextHealth < previousHealth)
                        flags |= CombatDamageResultFlags.BloodScent;
                    if ((kind == (byte)CombatEntityKind.Submarine || kind == (byte)CombatEntityKind.Habitat) &&
                        nextHealth <= maxHealth * CriticalFailureHealthFraction)
                    {
                        flags |= CombatDamageResultFlags.CriticalFailure;
                    }

                    if ((flags & CombatDamageResultFlags.WoundTrigger) != 0 && MathLod == (byte)CombatMathLod.High)
                        flags |= CombatDamageResultFlags.HighFidelityWound;

                    WriteResult(slot, signal, detail, damageType, kind, previousHealth, nextHealth, damage, maxHealth, InvMaxHealth[slot], flags);
                }

                Counters[CounterProcessedSignals] = Counters[CounterProcessedSignals] + processed;
            }

            private void SetStatusDurations(int slot, uint statusBits, float duration)
            {
                float4 durations = StatusDurations0123[slot];
                if ((statusBits & CombatStatusBits.Bleeding) != 0u)
                    durations.x = math.max(durations.x, duration);
                if ((statusBits & CombatStatusBits.Poisoned) != 0u)
                    durations.y = math.max(durations.y, duration);
                if ((statusBits & CombatStatusBits.Burning) != 0u)
                    durations.z = math.max(durations.z, duration);
                if ((statusBits & CombatStatusBits.Stunned) != 0u)
                    durations.w = math.max(durations.w, duration);
                StatusDurations0123[slot] = durations;
                if ((statusBits & CombatStatusBits.Brittle) != 0u)
                    BrittleDurations[slot] = math.max(BrittleDurations[slot], duration);
            }

            private static float ResolveDefaultStatusDuration(uint statusBits)
            {
                if ((statusBits & CombatStatusBits.Bleeding) != 0u)
                    return DefaultBleedStatusDurationSeconds;
                if ((statusBits & CombatStatusBits.Poisoned) != 0u)
                    return DefaultPoisonStatusDurationSeconds;
                if ((statusBits & CombatStatusBits.Burning) != 0u)
                    return DefaultThermalStatusDurationSeconds;
                if ((statusBits & CombatStatusBits.Stunned) != 0u)
                    return DefaultStunStatusDurationSeconds;

                return DefaultThermalStatusDurationSeconds;
            }

            private void WriteResult(
                int slot,
                in CombatDamageSignal signal,
                in CombatDamageSignalDetail detail,
                uint damageType,
                byte kind,
                float previousHealth,
                float nextHealth,
                float damage,
                float maxHealth,
                float invMaxHealth,
                byte flags)
            {
                int resultIndex = Counters[CounterResultCount];
                if (resultIndex >= MaxResults)
                {
                    Counters[CounterDroppedResults] = Counters[CounterDroppedResults] + 1;
                    return;
                }

                Counters[CounterResultCount] = resultIndex + 1;
                Results[resultIndex] = new CombatDamageResult
                {
                    TargetId = InstanceIds[slot],
                    SourceId = signal.SourceId,
                    DamageType = damageType,
                    StatusBits = StatusMasks[slot],
                    PreviousHealth = previousHealth,
                    NextHealth = nextHealth,
                    AppliedDamage = damage,
                    MaxHealth = maxHealth,
                    Direction = ResolveCombatDirection(signal.Direction, kind),
                    TraumaLevel = ResolveTraumaLevelFromInvMax(damage, invMaxHealth),
                    Flags = flags,
                    Channel = (byte)DamageChannel.Integrity,
                    DirectionOctant = ResolveDirectionOctant(signal.Direction),
                    LocalPoint = detail.LocalPoint,
                    Depth = 0f
                };
            }
        }

        [BurstCompile]
        private struct ProcessCombatStatusJob : IJobParallelFor
        {
            public float DeltaTime;
            public int TargetCount;
            [ReadOnly] public NativeArray<int> InstanceIds;
            public NativeArray<float> Health;
            [ReadOnly] public NativeArray<float> MaxHealth;
            [ReadOnly] public NativeArray<float> InvMaxHealth;
            [ReadOnly] public NativeArray<uint> TargetFlags;
            public NativeArray<uint> StatusMasks;
            public NativeArray<float4> StatusDurations0123;
            public NativeArray<float> BrittleDurations;
            public NativeArray<CombatDamageResult> ResultsBySlot;
            public NativeArray<byte> ResultActiveBySlot;

            public void Execute(int index)
            {
                if (index >= TargetCount)
                    return;

                ResultActiveBySlot[index] = 0;

                uint status = StatusMasks[index];
                if (status == 0u)
                    return;

                float4 durations = StatusDurations0123[index];
                float brittleDuration = BrittleDurations[index];
                uint previousStatus = status;
                durations = math.max(float4.zero, durations - new float4(DeltaTime));
                brittleDuration = math.max(0f, brittleDuration - DeltaTime);

                status = durations.x > 0f ? status : status & ~CombatStatusBits.Bleeding;
                status = durations.y > 0f ? status : status & ~CombatStatusBits.Poisoned;
                status = durations.z > 0f ? status : status & ~CombatStatusBits.Burning;
                status = durations.w > 0f ? status : status & ~CombatStatusBits.Stunned;
                status = brittleDuration > 0f ? status : status & ~CombatStatusBits.Brittle;

                float previousHealth = Health[index];
                float damage = 0f;
                if ((previousStatus & CombatStatusBits.Poisoned) != 0u)
                    damage += PoisonDamagePerSlowTick;
                if ((previousStatus & CombatStatusBits.Burning) != 0u)
                    damage += BurningDamagePerSlowTick;

                float nextHealth = math.max(0f, previousHealth - damage);
                Health[index] = nextHealth;
                StatusMasks[index] = status;
                StatusDurations0123[index] = durations;
                BrittleDurations[index] = brittleDuration;

                if (damage <= 0f && status == previousStatus)
                    return;

                float maxHealth = math.max(0.0001f, MaxHealth[index]);
                byte flags = status == previousStatus
                    ? CombatDamageResultFlags.None
                    : CombatDamageResultFlags.StatusChanged;
                if (nextHealth <= 0f && previousHealth > 0f)
                    flags |= CombatDamageResultFlags.TargetKilled;

                ResultsBySlot[index] = new CombatDamageResult
                {
                    TargetId = InstanceIds[index],
                    SourceId = DamageSourceIds.EnvironmentHazard,
                    DamageType = CombatDamageTypes.Toxic | CombatDamageTypes.Thermal,
                    StatusBits = status,
                    PreviousHealth = previousHealth,
                    NextHealth = nextHealth,
                    AppliedDamage = damage,
                    MaxHealth = maxHealth,
                    Direction = float3.zero,
                    TraumaLevel = ResolveTraumaLevelFromInvMax(damage, InvMaxHealth[index]),
                    Flags = flags,
                    Channel = (byte)DamageChannel.Integrity,
                    DirectionOctant = 0,
                    LocalPoint = float3.zero,
                    Depth = 0f
                };
                ResultActiveBySlot[index] = 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveCombatDirection(float3 direction, byte kind)
        {
            return kind == (byte)CombatEntityKind.Player
                ? ResolvePreciseDirection(direction)
                : ResolveDominantAxisDirection(direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolvePreciseDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            return lengthSq > 0.0001f && math.all(math.isfinite(direction))
                ? direction * math.rsqrt(lengthSq)
                : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveDominantAxisDirection(float3 direction)
        {
            float ax = math.abs(direction.x);
            float ay = math.abs(direction.y);
            float az = math.abs(direction.z);
            if (ax >= ay && ax >= az)
                return direction.x >= 0f ? new float3(1f, 0f, 0f) : new float3(-1f, 0f, 0f);
            if (az >= ay)
                return direction.z >= 0f ? new float3(0f, 0f, 1f) : new float3(0f, 0f, -1f);
            return direction.y >= 0f ? new float3(0f, 1f, 0f) : new float3(0f, -1f, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveDirectionOctant(float3 direction)
        {
            float ax = math.abs(direction.x);
            float ay = math.abs(direction.y);
            float az = math.abs(direction.z);
            if (ax >= ay && ax >= az)
                return direction.x >= 0f ? (byte)0 : (byte)1;
            if (az >= ay)
                return direction.z >= 0f ? (byte)2 : (byte)3;
            return direction.y >= 0f ? (byte)4 : (byte)5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveTraumaLevelFromInvMax(float damage, float invMaxHealth)
        {
            float severity = damage * math.max(0f, invMaxHealth);
            if (severity >= 0.9f)
                return (byte)TraumaLevel.Catastrophic;
            if (severity >= 0.65f)
                return (byte)TraumaLevel.Critical;
            if (severity >= 0.4f)
                return (byte)TraumaLevel.Significant;
            if (severity >= 0.15f)
                return (byte)TraumaLevel.Minor;

            return (byte)TraumaLevel.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadDamageType(uint packedMeta)
        {
            return (packedMeta >> MetaDamageTypeShift) & MetaDamageTypeMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadStatusBits(uint packedMeta)
        {
            return (packedMeta >> MetaStatusBitsShift) & MetaStatusBitsMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadWeakspotTier(uint packedMeta)
        {
            return (int)((packedMeta >> MetaWeakspotTierShift) & MetaWeakspotTierMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadDetailIndex(uint packedMeta)
        {
            return (int)((packedMeta >> MetaDetailIndexShift) & MetaDetailIndexMask);
        }
    }
}
