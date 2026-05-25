using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

    [System.Flags]
    public enum StatusFlags : uint
    {
        Bleeding = 1u << 0,
        Crushed = 1u << 1,
        Irradiated = 1u << 2,
        Hypoxia = 1u << 3
    }

    public enum CombatLimbRegion : byte
    {
        None = 0,
        Tail = 1
    }

    public static class CombatStatusBits
    {
        public const uint Bleeding = (uint)StatusFlags.Bleeding;
        public const uint Crushed = (uint)StatusFlags.Crushed;
        public const uint Irradiated = (uint)StatusFlags.Irradiated;
        public const uint Hypoxia = (uint)StatusFlags.Hypoxia;
        public const uint Poisoned = 1u << 4;
        public const uint Burning = 1u << 5;
        public const uint Stunned = 1u << 6;
        public const uint Brittle = 1u << 7;
        public const uint Crippled = 1u << 8;
        public const uint Fractured = 1u << 9;

        public const ulong Bleeding64 = Bleeding;
        public const ulong Crushed64 = Crushed;
        public const ulong Irradiated64 = Irradiated;
        public const ulong Hypoxia64 = Hypoxia;
        public const ulong Poisoned64 = Poisoned;
        public const ulong Burning64 = Burning;
        public const ulong Stunned64 = Stunned;
        public const ulong Brittle64 = Brittle;
        public const ulong Crippled64 = Crippled;
        public const ulong Fractured64 = Fractured;
        public const ulong KnownRuntimeMask64 = Bleeding64 |
                                                Crushed64 |
                                                Irradiated64 |
                                                Hypoxia64 |
                                                Poisoned64 |
                                                Burning64 |
                                                Stunned64 |
                                                Brittle64 |
                                                Crippled64 |
                                                Fractured64;
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
        public const ushort None = 0;
        public const ushort WoundTrigger = 1 << 0;
        public const ushort ShieldAbsorbed = 1 << 1;
        public const ushort TargetKilled = 1 << 2;
        public const ushort StatusChanged = 1 << 3;
        public const ushort HitHud = 1 << 4;
        public const ushort BloodScent = 1 << 5;
        public const ushort CriticalFailure = 1 << 6;
        public const ushort HighFidelityWound = 1 << 7;
        public const ushort Deflected = 1 << 8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CombatDamageRequest
    {
        [FieldOffset(0)] public int TargetId;
        [FieldOffset(4)] public int SourceId;
        [FieldOffset(8)] public float Amount;
        [FieldOffset(12)] public float ImpulseMagnitude;
        [FieldOffset(16)] public float3 Direction;
        [FieldOffset(28)] public uint PackedMeta;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CombatDamageSignalDetail
    {
        [FieldOffset(0)] public float3 LocalPoint;
        [FieldOffset(12)] public float3 ArmorNormal;
        [FieldOffset(24)] public float LocalTemperatureCelsius;
        [FieldOffset(28)] public float StatusDurationSeconds;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CombatDamageResult
    {
        [FieldOffset(0)] public int TargetId;
        [FieldOffset(4)] public int SourceId;
        [FieldOffset(8)] public uint DamageType;
        [FieldOffset(12)] public uint StatusBits;
        [FieldOffset(16)] public float PreviousHealth;
        [FieldOffset(20)] public float NextHealth;
        [FieldOffset(24)] public float AppliedDamage;
        [FieldOffset(28)] public float MaxHealth;
        [FieldOffset(32)] public float3 Direction;
        [FieldOffset(44)] public byte TraumaLevel;
        [FieldOffset(45)] private byte _pad0;
        [FieldOffset(46)] public ushort Flags;
        [FieldOffset(48)] public byte Channel;
        [FieldOffset(49)] public byte DirectionOctant;
        [FieldOffset(50)] private ushort _pad1;
        [FieldOffset(52)] public float3 LocalPoint;
        [FieldOffset(64)] public float3 SurfaceNormal;
        [FieldOffset(76)] public float Depth;
        [FieldOffset(80)] private ulong _pad2;
        [FieldOffset(88)] private ulong _pad3;
        [FieldOffset(96)] private ulong _pad4;
        [FieldOffset(104)] private ulong _pad5;
        [FieldOffset(112)] private ulong _pad6;
        [FieldOffset(120)] private ulong _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct CombatTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint Sequence;
        [FieldOffset(8)] public uint PhaseHash;
        [FieldOffset(12)] public uint TargetHash;
        [FieldOffset(16)] public uint SourceHash;
        [FieldOffset(20)] public uint StatusBits;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint AnomalyHash;
        [FieldOffset(32)] public float PreviousHealth;
        [FieldOffset(36)] public float NextHealth;
        [FieldOffset(40)] public float AppliedDamage;
        [FieldOffset(44)] public float3 LocalPoint;
        [FieldOffset(56)] public ushort Flags;
        [FieldOffset(58)] public byte TraumaLevel;
        [FieldOffset(59)] public byte DirectionOctant;
        [FieldOffset(60)] public uint Reserved;
    }

    public interface ICombatMobilityModifierReceiver
    {
        void SetCombatMobilityScale(float speedScale, float durationSeconds);
    }

    /// <summary>
    /// Optional receiver-owned hit profile for directional armor and local-space critical-hit checks.
    /// </summary>
    public interface ICombatHitProfileSource
    {
        /// <summary>Current world-space forward vector used by front-armor deflection.</summary>
        Vector3 CombatForward { get; }

        /// <summary>Current target height used by local-y headshot fake logic.</summary>
        float CombatHeight { get; }
    }

    /// <summary>
    /// Optional receiver-owned rigidbody provider for combat pushback. Cached during target registration.
    /// </summary>
    public interface ICombatPushbackBodySource
    {
        /// <summary>Rigidbody that receives deferred combat impulses, or null when movement is kinematic-only.</summary>
        Rigidbody CombatPushbackBody { get; }
    }

    public static partial class CombatDamageRuntime
    {
        private const int MaxTargets = 2048;
        private const int MaxQueuedSignals = 1024;
        private const int MaxGlobalDamageSignalsPerFrame = 64;
        private const int MaxResults = 1024;
        private const int AtomicHealthCasRetryLimit = MaxQueuedSignals;
        private const int TelemetryFrameCapacity = 300;
        private const int ArmorTelemetryCapacity = TelemetryFrameCapacity;
        private const int TelemetryStateLength = 2;
        private const int TelemetryWriteCursorIndex = 0;
        private const int TelemetryLastAnomalyIndex = 1;
        private const int CombatTelemetryEntrySizeBytes = 64;
        private const int PoisonDiffusionBufferLength = 16;
        private const int DamageClassCount = 8;
        private const int ArmorClassCount = 8;
        private const int DamageArmorLutLength = DamageClassCount * ArmorClassCount;
        private const float ThermalBurnThresholdCelsius = 100f;
        private const float ThermalBrittleThresholdCelsius = 0f;
        private const float DefaultThermalStatusDurationSeconds = 4f;
        private const float DefaultPoisonStatusDurationSeconds = 5f;
        private const float DefaultBleedStatusDurationSeconds = 6f;
        private const float DefaultStunStatusDurationSeconds = 0.75f;
        private const float PoisonDiffusionRadiusMeters = 2f;
        private const float ShieldAbsorbFraction = 0.8f;
        private const float BrittleImpactMultiplier = 1.25f;
        private const float BleedingDamagePerSlowTick = 0.5f;
        private const float CrushedDamagePerSlowTick = 0.75f;
        private const float IrradiatedDamagePerSlowTick = 0.5f;
        private const float HypoxiaDamagePerSlowTick = 1.25f;
        private const float PoisonDamagePerSlowTick = 1f;
        private const float BurningDamagePerSlowTick = 1.5f;
        private const float HeadshotHeightFraction = 0.8f;
        private const float HeadshotDamageMultiplier = 3f;
        private const float DirectionalDeflectDot = -0.7f;
        private const float DirectionalDeflectDamageScalar = 0.1f;
        private const float ArmorDegradationDamageThreshold = 8f;
        private const float ArmorDegradationPerDamage = 0.08f;
        private const float MaxMomentumDamageMultiplier = 16f;
        private const byte BloodDebrisKind = 2;
        private const float WoundThresholdFraction = 0.2f;
        private const float CriticalFailureHealthFraction = 0.1f;
        public const float CrippledMobilitySpeedScale = 0.55f;
        public const float CrippledMobilityDurationSeconds = 3f;
        private const uint TargetFlagArmorMask = 0xFu;
        private const int TargetFlagKindShift = 4;
        private const int CounterResultCount = 0;
        private const int CounterDroppedResults = 1;
        private const int CounterMissingTargets = 2;
        private const int CounterProcessedSignals = 3;
        private const int CounterLength = 4;
        private const int MetaDamageTypeShift = 0;
        private const int MetaStatusBitsShift = 8;
        private const int MetaWeakspotTierShift = 17;
        private const int MetaDetailIndexShift = 19;
        private const int MetaDamageClassShift = 29;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;
        private const uint MetaDamageTypeMask = 0xFFu;
        private const uint MetaStatusBitsMask = 0x1FFu;
        private const uint MetaWeakspotTierMask = 0x3u;
        private const uint MetaDetailIndexMask = 0x3FFu;
        private const uint MetaDamageClassMask = 0x7u;
        private const uint MetaDetailIndexClearMask = ~(MetaDetailIndexMask << MetaDetailIndexShift);
        private const uint MetaDamageClassClearMask = ~(MetaDamageClassMask << MetaDamageClassShift);
        private const uint CombatTelemetryPhaseDamage = 0x444D4748u;
        private const uint CombatTelemetryPhaseStatus = 0x53544154u;
        private const uint CombatTelemetryMagicLow = 0x434F4D42u;
        private const uint CombatTelemetryMagicHigh = 0x41544C55u;
        private const uint CombatTelemetrySystemHash = 0x434F4D42u;
        private const byte TelemetrySeverityWarning = 1;
        private const byte TelemetrySeverityCritical = 3;
        private const byte TelemetryFlagIngressSanitized = 1 << 0;
        private const byte TelemetryFlagResultAnomaly = 1 << 1;
        private const byte TelemetryFlagQueueRejected = 1 << 2;
        private const uint TelemetryAnomalyQueueBusy = 0xC0BA0010u;
        private const uint TelemetryAnomalyQueueFull = 0xC0BA0011u;
        private const uint TelemetryAnomalyQueueStorage = 0xC0BA0012u;

        private static readonly ProfilerMarker _scheduleMarker = new ProfilerMarker("CombatDamageRuntime.Schedule");
        private static readonly ProfilerMarker _lateFrameMarker = new ProfilerMarker("CombatDamageRuntime.LateFrame");
        private static readonly ProfilerMarker _slowTickMarker = new ProfilerMarker("CombatDamageRuntime.SlowTick");
        private static readonly SpatialQueryHit[] _poisonDiffusionHits =
            new SpatialQueryHit[PoisonDiffusionBufferLength]; // COLD ALLOC: SpatialQueryHit[16] - poison spread fanout scratch - owner: CombatDamageRuntime
        private static readonly int[] _poisonDiffusionTargetIds =
            new int[PoisonDiffusionBufferLength]; // COLD ALLOC: int[16] - poison spread duplicate-target filter - owner: CombatDamageRuntime

        private static NativeQueue<CombatDamageRequest> _damageSignals;
        private static NativeArray<CombatDamageSignalDetail> _signalDetails;
        private static NativeParallelHashMap<int, int> _slotByTargetId;
        private static NativeArray<int> _instanceIds;
        private static NativeArray<float> _health;
        private static NativeArray<float> _maxHealth;
        private static NativeArray<float> _invMaxHealth;
        private static NativeArray<int> _armorValues;
        private static NativeArray<float> _shieldValues;
        private static NativeArray<float> _minorDamageAccumulators;
        private static NativeArray<float3> _targetForwardVectors;
        private static NativeArray<float> _targetHeights;
        private static NativeArray<uint> _targetFlags;
        private static NativeArray<uint> _statusMasks;
        private static NativeArray<float4> _statusDurations0123;
        private static NativeArray<float4> _legacyStatusDurations4567;
        private static NativeArray<float> _brittleDurations;
        private static NativeArray<float> _damageArmorLut;
        private static NativeArray<CombatDamageResult> _results;
        private static NativeArray<CombatDamageResult> _statusResults;
        private static NativeArray<byte> _statusResultActive;
        private static NativeArray<int> _counters;
        private static NativeArray<CombatTelemetryEntry> _telemetryRing;
        private static NativeArray<uint> _telemetryState;
        private static IDamageReceiver[] _receivers;
        private static Transform[] _receiverTransforms;
        private static Rigidbody[] _targetBodies;
        private static int _targetCount;
        private static int _queuedSignalCount;
        private static JobHandle _damageJobHandle;
        private static JobHandle _statusJobHandle;
        private static bool _damageJobScheduled;
        private static bool _statusJobScheduled;
        private static bool _telemetryDumpedThisSession;
        private static uint _lastQueueRejectFrame;
        private static uint _lastQueueRejectAnomalyHash;
        private static float _requestedVisualQualityWeight01 = 1f;
        private static float _visualQualityWeight01 = 1f;
        private static IDataVault _combatDataVault;
        private static bool _combatHotSwapRegistered;
        private static bool _combatDataVaultColdCacheAttempted;
        private static readonly CombatRegistryHotSwapBridge _combatHotSwapBridge = new CombatRegistryHotSwapBridge();

        public static bool IsInitialized => _damageSignals.IsCreated;
        public static int PendingSignalCount => _queuedSignalCount;

        public static void SetCombatMathLod(CombatMathLod lod)
        {
            _requestedVisualQualityWeight01 = lod == CombatMathLod.Low ? 0f : 1f;
            RefreshRuntimePolicy();
        }

        public static void SetCombatVisualQualityWeight(float weight01)
        {
            _requestedVisualQualityWeight01 = SanitizeQualityWeight01(weight01);
            RefreshRuntimePolicy();
        }

        public static uint PackSignalMeta(
            uint damageType,
            uint statusBits,
            CombatWeakspotTier weakspotTier)
        {
            uint clippedDamageType = damageType & MetaDamageTypeMask;
            return PackDamageClassMetaFast(
                (clippedDamageType << MetaDamageTypeShift) |
                ((statusBits & MetaStatusBitsMask) << MetaStatusBitsShift) |
                (((uint)weakspotTier & MetaWeakspotTierMask) << MetaWeakspotTierShift));
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

        private static void RegisterCombatRegistryHotSwapBridge()
        {
            if (!_combatHotSwapRegistered)
                _combatHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(_combatHotSwapBridge);

            if (!_combatDataVaultColdCacheAttempted)
            {
                ApplyCombatDataVaultRebind(null, GlobalRegistry.DataVault);
                _combatDataVaultColdCacheAttempted = true;
            }
        }

        private static void UnregisterCombatRegistryHotSwapBridge()
        {
            if (!_combatHotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(_combatHotSwapBridge);
            _combatHotSwapRegistered = false;
        }

        public static void Prewarm()
        {
            EnsureInitialized();
        }

        private static void ApplyCombatDataVaultRebind(IDataVault previousVault, IDataVault currentVault)
        {
            _combatDataVault = currentVault;
            BallisticsRuntime.CacheDataVault(currentVault);
            RequestStatusEffectVaultRebind(previousVault, currentVault);
        }

        private sealed class CombatRegistryHotSwapBridge : IGlobalRegistryHotSwapListener
        {
            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                    return;

                ApplyCombatDataVaultRebind(previousService as IDataVault ?? _combatDataVault, currentService as IDataVault);
            }
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
            float3 targetForward = ResolveReceiverForward(receiver);
            float targetHeight = ResolveReceiverHeight(receiver);
            int slot;
            if (_slotByTargetId.TryGetValue(targetId, out slot))
            {
                if (!CanUseExistingTargetSlot(slot))
                    return false;

                CaptureReceiverManagedRefs(slot, receiver);
                _health[slot] = safeHealth;
                _maxHealth[slot] = safeMaxHealth;
                _invMaxHealth[slot] = math.rcp(safeMaxHealth);
                _armorValues[slot] = QuantizeArmorValue(armorValue);
                _shieldValues[slot] = math.max(0f, shieldValue);
                _targetForwardVectors[slot] = targetForward;
                _targetHeights[slot] = targetHeight;
                _targetFlags[slot] = PackTargetFlags(kind, armorClass);
                SeedTargetArmorProfile(slot, targetId, kind, armorClass, safeMaxHealth, armorValue);
                RegisterBallisticRootPrimitive(targetId, receiver, targetHeight, armorClass);
                return true;
            }

            if (_targetCount < 0 || _targetCount >= MaxTargets)
                return false;

            slot = _targetCount;
            if (!CanUseRegistrationTargetSlot(slot))
                return false;

            if (!_slotByTargetId.TryAdd(targetId, slot))
                return false;

            _targetCount++;
            CaptureReceiverManagedRefs(slot, receiver);
            _instanceIds[slot] = targetId;
            _health[slot] = safeHealth;
            _maxHealth[slot] = safeMaxHealth;
            _invMaxHealth[slot] = math.rcp(safeMaxHealth);
            _armorValues[slot] = QuantizeArmorValue(armorValue);
            _shieldValues[slot] = math.max(0f, shieldValue);
            _targetForwardVectors[slot] = targetForward;
            _targetHeights[slot] = targetHeight;
            _targetFlags[slot] = PackTargetFlags(kind, armorClass);
            SeedTargetArmorProfile(slot, targetId, kind, armorClass, safeMaxHealth, armorValue);
            _statusMasks[slot] = 0u;
            _statusDurations0123[slot] = float4.zero;
            _legacyStatusDurations4567[slot] = float4.zero;
            _brittleDurations[slot] = 0f;
            _statusResults[slot] = default;
            _statusResultActive[slot] = 0;
            ResetStatusEffectSlot(slot);
            RegisterBallisticRootPrimitive(targetId, receiver, targetHeight, armorClass);
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

            if (!CanUseExistingTargetSlot(slot))
                return false;

            if (receiver != null && !ReferenceEquals(_receivers[slot], receiver))
                return false;

            BallisticsRuntime.TombstonePrimitivesForTarget(unchecked((uint)targetId));
            int lastSlot = _targetCount - 1;
            if (!CanUseExistingTargetSlot(lastSlot))
                return false;

            if (slot != lastSlot && _instanceIds[lastSlot] == 0)
                return false;

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
                _targetForwardVectors[slot] = _targetForwardVectors[lastSlot];
                _targetHeights[slot] = _targetHeights[lastSlot];
                _targetFlags[slot] = _targetFlags[lastSlot];
                MoveTargetArmorState(lastSlot, slot);
                _statusMasks[slot] = _statusMasks[lastSlot];
                _statusDurations0123[slot] = _statusDurations0123[lastSlot];
                _legacyStatusDurations4567[slot] = _legacyStatusDurations4567[lastSlot];
                _brittleDurations[slot] = _brittleDurations[lastSlot];
                _statusResults[slot] = _statusResults[lastSlot];
                _statusResultActive[slot] = _statusResultActive[lastSlot];
                CopyStatusEffectSlot(slot, lastSlot);
                _receivers[slot] = _receivers[lastSlot];
                _receiverTransforms[slot] = _receiverTransforms[lastSlot];
                _targetBodies[slot] = _targetBodies[lastSlot];
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

            if (!CanUseExistingTargetSlot(slot))
                return false;

            float safeMaxHealth = math.max(0.0001f, maximumHealth);
            _health[slot] = math.clamp(currentHealth, 0f, safeMaxHealth);
            _maxHealth[slot] = safeMaxHealth;
            _invMaxHealth[slot] = math.rcp(safeMaxHealth);
            return true;
        }

        public static bool SyncTargetProtection(int targetId, float armorValue, float shieldValue)
        {
            if (!_slotByTargetId.IsCreated || !CanMutateTargets())
                return false;

            if (!_slotByTargetId.TryGetValue(targetId, out int slot))
                return false;

            if (!CanUseExistingTargetSlot(slot))
                return false;

            _armorValues[slot] = QuantizeArmorValue(armorValue);
            _shieldValues[slot] = math.max(0f, shieldValue);
            RefreshTargetArmorBase(slot, armorValue);
            return true;
        }

        public static bool SyncTargetHitProfile(int targetId, Vector3 targetForward, float targetHeight)
        {
            if (!_slotByTargetId.IsCreated || !CanMutateTargets())
                return false;

            if (!_slotByTargetId.TryGetValue(targetId, out int slot))
                return false;

            if (!CanUseExistingTargetSlot(slot))
                return false;

            _targetForwardVectors[slot] = NormalizeOrDefault(
                new float3(targetForward.x, targetForward.y, targetForward.z),
                new float3(0f, 0f, 1f));
            _targetHeights[slot] = math.max(0.0001f, targetHeight);
            return true;
        }

        public static bool TryGetTargetHealthFraction(int targetId, out float health01)
        {
            health01 = 0f;
            if (!_slotByTargetId.IsCreated || !_health.IsCreated || !_invMaxHealth.IsCreated)
                return false;

            if (!_slotByTargetId.TryGetValue(targetId, out int slot))
                return false;

            if ((uint)slot >= (uint)_health.Length ||
                (uint)slot >= (uint)_invMaxHealth.Length)
                return false;

            health01 = math.saturate(_health[slot] * _invMaxHealth[slot]);
            return true;
        }

        [Obsolete("Use TryQueueDamage(in signal, in detail, impactAup). Combat damage ingress must carry explicit AUP metadata.", true)]
        public static bool TryQueueDamage(in CombatDamageRequest signal)
        {
            CombatDamageSignalDetail detail = default;
            return TryQueueDamage(in signal, in detail, double3.zero);
        }

        [Obsolete("Use TryQueueDamage(in signal, in detail, impactAup). Combat damage ingress must carry explicit AUP metadata.", true)]
        public static bool TryQueueDamage(in CombatDamageRequest signal, in CombatDamageSignalDetail detail)
        {
            return TryQueueDamage(in signal, in detail, double3.zero);
        }

        public static bool TryQueueDamage(in CombatDamageRequest signal, in CombatDamageSignalDetail detail, double3 impactAup)
        {
            if (signal.TargetId == 0)
                return false;

            if (!_damageSignals.IsCreated)
                return false;

            if (_damageJobScheduled)
            {
                PublishQueueRejectAnomaly(TelemetryAnomalyQueueBusy, signal.Amount);
                return false;
            }

            if (_queuedSignalCount >= MaxQueuedSignals)
            {
                PublishQueueRejectAnomaly(TelemetryAnomalyQueueFull, signal.Amount);
                return false;
            }

            int detailIndex = _queuedSignalCount;
            if (!CanUseDamageIngressSlot(detailIndex))
            {
                PublishQueueRejectAnomaly(TelemetryAnomalyQueueStorage, signal.Amount);
                return false;
            }

            if (_slotByTargetId.TryGetValue(signal.TargetId, out int targetSlot))
                RefreshTargetHitProfile(targetSlot);

            SanitizeQueuedSignal(in signal, in detail, out CombatDamageRequest queuedSignal, out CombatDamageSignalDetail queuedDetail, out uint ingressAnomalyHash);
            uint packedMeta = PackDamageClassMetaFast(queuedSignal.PackedMeta);
            queuedSignal.PackedMeta = (packedMeta & MetaDetailIndexClearMask) |
                                      ((uint)detailIndex << MetaDetailIndexShift);
            _signalDetails[detailIndex] = queuedDetail;
            WriteSignalImpactAup(detailIndex, impactAup);
            _damageSignals.Enqueue(queuedSignal);
            _queuedSignalCount++;
            if (ingressAnomalyHash != 0u)
                PublishCombatTelemetryAnomaly(ingressAnomalyHash, queuedSignal.Amount, TelemetrySeverityWarning, TelemetryFlagIngressSanitized);
            return true;
        }

        private static bool CanUseDamageIngressSlot(int detailIndex)
        {
            if (!_damageSignals.IsCreated ||
                !_signalDetails.IsCreated ||
                (uint)detailIndex >= (uint)MaxQueuedSignals ||
                (uint)detailIndex >= (uint)_signalDetails.Length)
            {
                return false;
            }

            return TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) &&
                   views.SignalImpactAups.IsCreated &&
                   (uint)detailIndex < (uint)views.SignalImpactAups.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SpliceFloraTraitMask(ulong parentA, ulong parentB, uint seed)
        {
            uint hi = math.hash(new uint2(seed, (uint)(parentA ^ (parentA >> 32))));
            uint lo = math.hash(new uint2(seed ^ 0x9E3779B9u, (uint)(parentB ^ (parentB >> 32))));
            ulong selector = ((ulong)hi << 32) | lo;
            selector ^= RotateLeft64(parentA, 17) ^ RotateLeft64(parentB, 41);
            return (parentA & selector) | (parentB & ~selector);
        }

        public static void FrameTick(float deltaTime)
        {
            if (BallisticsRuntime.PrepareFrameForTargetRefresh())
                RefreshBallisticTargetAabbs();

            BallisticsRuntime.FrameTick(deltaTime);
            TryApplyPendingStatusEffectVaultRebind();
            DrainGlobalDamageSignals(MaxGlobalDamageSignalsPerFrame);

            if (!_damageSignals.IsCreated || _queuedSignalCount <= 0 || _damageJobScheduled || _statusJobScheduled)
                return;

            using (_scheduleMarker.Auto())
            {
                if (!_statusEffectStates.IsCreated && !EnsureStatusEffectStorage())
                    return;
                if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews armorViews, ensure: false))
                    return;
                if (!CanUseDamageJobBuffers(in armorViews))
                    return;

                RefreshArmorTargetSnapshots(ref armorViews);
                if (!TryLockArmorVaultBuffersForJobs())
                    return;

                bool armorVaultLockOwnedByScheduledJob = false;
                try
                {
                    RefreshRuntimePolicy();
                    ClearCounters();
                    ProcessDamageQueueJob job = new ProcessDamageQueueJob
                    {
                        Signals = _damageSignals,
                        SignalDetails = _signalDetails,
                        SignalImpactAups = armorViews.SignalImpactAups,
                        SlotByTargetId = _slotByTargetId,
                        InstanceIds = _instanceIds,
                        Health = _health,
                        MaxHealth = _maxHealth,
                        InvMaxHealth = _invMaxHealth,
                        ArmorValues = _armorValues,
                        ShieldValues = _shieldValues,
                        MinorDamageAccumulators = _minorDamageAccumulators,
                        TargetForwardVectors = _targetForwardVectors,
                        TargetHeights = _targetHeights,
                        TargetFlags = _targetFlags,
                        TargetRootAups = armorViews.TargetRootAups,
                        TargetRotations = armorViews.TargetRotations,
                        TargetHalfExtents = armorViews.TargetHalfExtents,
                        TargetArmorProfiles = armorViews.TargetArmorProfiles,
                        StatusEffectStates = _statusEffectStates,
                        StatusMasks = _statusMasks,
                        StatusDurations0123 = _statusDurations0123,
                        LegacyStatusDurations4567 = _legacyStatusDurations4567,
                        BrittleDurations = _brittleDurations,
                        DamageArmorLut = _damageArmorLut,
                        ArmorTelemetryRing = armorViews.TelemetryRing,
                        ArmorDebugHits = armorViews.DebugHits,
                        Results = _results,
                        Counters = _counters,
                        DeflectSignalWriter = SignalBus<DeflectSignal>.ParallelWriter,
                        DeflectSignalWriterBudget = SignalBus<DeflectSignal>.ParallelWriterBudget,
                        ImpactSignalWriter = SignalBus<ImpactSignal>.ParallelWriter,
                        ImpactSignalWriterBudget = SignalBus<ImpactSignal>.ParallelWriterBudget,
                        SignalBudget = MaxQueuedSignals,
                        VisualQualityWeight01 = _visualQualityWeight01,
                        ArmorTuning = PrepareArmorTuningForJob(ref armorViews),
                        ArmorTelemetryIndex = BeginArmorPenetrationSchedule(),
                        ArmorFrameIndex = unchecked((uint)Time.frameCount)
                    };
                    _damageJobHandle = job.Schedule();
                    _damageJobScheduled = true;
                    armorVaultLockOwnedByScheduledJob = true;
                    H8Memory.RegisterActiveJob(ArmorMemoryOwner, _damageJobHandle);
                    JobHandle.ScheduleBatchedJobs();
                }
                finally
                {
                    if (!armorVaultLockOwnedByScheduledJob)
                        UnlockArmorVaultBuffersForJobs();
                }
            }
        }

        private static bool CanUseDamageJobBuffers(in ArmorPenetrationVaultViews armorViews)
        {
            int targetCount = math.max(0, _targetCount);
            return _damageSignals.IsCreated &&
                   _signalDetails.IsCreated &&
                   _signalDetails.Length >= MaxQueuedSignals &&
                   armorViews.SignalImpactAups.IsCreated &&
                   armorViews.SignalImpactAups.Length >= MaxQueuedSignals &&
                   _slotByTargetId.IsCreated &&
                   _instanceIds.IsCreated &&
                   (uint)targetCount <= (uint)_instanceIds.Length &&
                   _health.IsCreated &&
                   (uint)targetCount <= (uint)_health.Length &&
                   _maxHealth.IsCreated &&
                   (uint)targetCount <= (uint)_maxHealth.Length &&
                   _invMaxHealth.IsCreated &&
                   (uint)targetCount <= (uint)_invMaxHealth.Length &&
                   _armorValues.IsCreated &&
                   (uint)targetCount <= (uint)_armorValues.Length &&
                   _shieldValues.IsCreated &&
                   (uint)targetCount <= (uint)_shieldValues.Length &&
                   _minorDamageAccumulators.IsCreated &&
                   (uint)targetCount <= (uint)_minorDamageAccumulators.Length &&
                   _targetForwardVectors.IsCreated &&
                   (uint)targetCount <= (uint)_targetForwardVectors.Length &&
                   _targetHeights.IsCreated &&
                   (uint)targetCount <= (uint)_targetHeights.Length &&
                   _targetFlags.IsCreated &&
                   (uint)targetCount <= (uint)_targetFlags.Length &&
                   armorViews.TargetRootAups.IsCreated &&
                   (uint)targetCount <= (uint)armorViews.TargetRootAups.Length &&
                   armorViews.TargetRotations.IsCreated &&
                   (uint)targetCount <= (uint)armorViews.TargetRotations.Length &&
                   armorViews.TargetHalfExtents.IsCreated &&
                   (uint)targetCount <= (uint)armorViews.TargetHalfExtents.Length &&
                   armorViews.TargetArmorProfiles.IsCreated &&
                   (uint)targetCount <= (uint)armorViews.TargetArmorProfiles.Length &&
                   _statusEffectStates.IsCreated &&
                   (uint)targetCount <= (uint)_statusEffectStates.Length &&
                   _statusMasks.IsCreated &&
                   (uint)targetCount <= (uint)_statusMasks.Length &&
                   _statusDurations0123.IsCreated &&
                   (uint)targetCount <= (uint)_statusDurations0123.Length &&
                   _legacyStatusDurations4567.IsCreated &&
                   (uint)targetCount <= (uint)_legacyStatusDurations4567.Length &&
                   _brittleDurations.IsCreated &&
                   (uint)targetCount <= (uint)_brittleDurations.Length &&
                   _damageArmorLut.IsCreated &&
                   _damageArmorLut.Length >= DamageArmorLutLength &&
                   armorViews.TelemetryRing.IsCreated &&
                   armorViews.TelemetryRing.Length > 0 &&
                   _results.IsCreated &&
                   _results.Length >= MaxResults &&
                   _counters.IsCreated &&
                   _counters.Length >= CounterLength;
        }

        private static void DrainGlobalDamageSignals(int maxSignals)
        {
            if (maxSignals <= 0 || _damageJobScheduled || _statusJobScheduled)
                return;

            ReadOnlySpan<Hecton8.Core.Contracts.Signals.CombatDamageSignal> globalSignals =
                SignalBus<Hecton8.Core.Contracts.Signals.CombatDamageSignal>.GetFrameSnapshot();
            int signalCount = math.min(maxSignals, globalSignals.Length);
            for (int i = 0; i < signalCount; i++)
            {
                Hecton8.Core.Contracts.Signals.CombatDamageSignal globalSignal = globalSignals[i];
                if (!TryBuildCombatSignal(in globalSignal, out CombatDamageRequest combatSignal, out CombatDamageSignalDetail detail, out double3 impactAup))
                    continue;

                if (!TryQueueDamage(in combatSignal, in detail, impactAup))
                    return;
            }
        }

        private static bool TryBuildCombatSignal(
            in Hecton8.Core.Contracts.Signals.CombatDamageSignal globalSignal,
            out CombatDamageRequest combatSignal,
            out CombatDamageSignalDetail detail,
            out double3 impactAup)
        {
            combatSignal = default;
            detail = default;
            impactAup = globalSignal.ImpactAup;

            float magnitude = math.max(0f, globalSignal.Magnitude);
            uint targetId = math.select(globalSignal.TargetId, globalSignal.TargetHash, globalSignal.TargetHash != 0u);
            if (targetId == 0u || !(magnitude > 0f))
                return false;

            float3 localPoint = CombatDamageSignalCodec.ToRuntimePointOrZero(in globalSignal);
            bool directionValid = (math.lengthsq(globalSignal.Direction) > 0.0001f) & math.all(math.isfinite(globalSignal.Direction));
            float3 direction = math.select(ResolveDominantAxisDirection(localPoint), globalSignal.Direction, new bool3(directionValid));
            uint damageType = math.select(CombatDamageTypes.Impact, globalSignal.DamageType, globalSignal.DamageType != 0u);

            float3 safeDirection = NormalizeOrDefault(direction, float3.zero);
            combatSignal = new CombatDamageRequest
            {
                TargetId = unchecked((int)targetId),
                SourceId = globalSignal.SourceId,
                Amount = magnitude,
                ImpulseMagnitude = magnitude,
                Direction = safeDirection,
                PackedMeta = PackSignalMeta(damageType, 0u, CombatWeakspotTier.None)
            };
            detail = new CombatDamageSignalDetail
            {
                LocalPoint = localPoint,
                ArmorNormal = safeDirection,
                LocalTemperatureCelsius = 0f,
                StatusDurationSeconds = 0f
            };
            return true;
        }

        private static void SanitizeQueuedSignal(
            in CombatDamageRequest input,
            in CombatDamageSignalDetail inputDetail,
            out CombatDamageRequest signal,
            out CombatDamageSignalDetail detail,
            out uint anomalyHash)
        {
            signal = input;
            detail = inputDetail;
            anomalyHash = 0u;

            signal.Amount = SanitizeNonNegativeFinite(signal.Amount, 0xC0BA0101u, ref anomalyHash);
            signal.ImpulseMagnitude = SanitizeNonNegativeFinite(signal.ImpulseMagnitude, 0xC0BA0102u, ref anomalyHash);
            signal.Direction = SanitizeFiniteVector(signal.Direction, float3.zero, 0xC0BA0103u, ref anomalyHash);
            detail.LocalPoint = SanitizeFiniteVector(detail.LocalPoint, float3.zero, 0xC0BA0104u, ref anomalyHash);
            detail.ArmorNormal = SanitizeFiniteVector(detail.ArmorNormal, float3.zero, 0xC0BA0105u, ref anomalyHash);
            if (!math.isfinite(detail.LocalTemperatureCelsius))
            {
                detail.LocalTemperatureCelsius = 0f;
                anomalyHash = MergeAnomalyHash(anomalyHash, 0xC0BA0106u);
            }

            detail.StatusDurationSeconds = SanitizeNonNegativeFinite(detail.StatusDurationSeconds, 0xC0BA0107u, ref anomalyHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegativeFinite(float value, uint anomalyCode, ref uint anomalyHash)
        {
            if (math.isfinite(value) && value >= 0f)
                return value;

            anomalyHash = MergeAnomalyHash(anomalyHash, anomalyCode);
            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFiniteVector(float3 value, float3 fallback, uint anomalyCode, ref uint anomalyHash)
        {
            if (math.all(math.isfinite(value)))
                return value;

            anomalyHash = MergeAnomalyHash(anomalyHash, anomalyCode);
            return fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MergeAnomalyHash(uint currentHash, uint anomalyCode)
        {
            return currentHash == 0u
                ? anomalyCode
                : math.hash(new uint2(currentHash, anomalyCode));
        }

        public static void SlowTick(float deltaTime)
        {
            if (!_health.IsCreated || _targetCount <= 0 || _damageJobScheduled || _statusJobScheduled)
                return;

            using (_slowTickMarker.Auto())
            {
                TryScheduleStatusEffectJobs(deltaTime);
            }
        }

        public static void LateFrameTick()
        {
            BallisticsRuntime.LateFrameTick();
            TryApplyPendingStatusEffectVaultRebind();
            if (!_damageJobScheduled && !_statusJobScheduled)
                return;

            using (_lateFrameMarker.Auto())
            {
                bool completedAny = false;
                bool completedStatus = false;
                if (_damageJobScheduled && DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: false))
                {
                    _damageJobScheduled = false;
                    FinishArmorPenetrationScheduledCompletion();
                    _queuedSignalCount = 0;
                    completedAny = true;
                }

                if (_statusJobScheduled && DispatcherJobSwap.TryComplete(ref _statusJobHandle, forceComplete: false))
                {
                    _statusJobScheduled = false;
                    CompleteStatusEffectFrame();
                    completedAny = true;
                    completedStatus = true;
                }

                if (!completedAny)
                    return;

                DispatchResults();
                if (completedStatus)
                    DispatchStatusResults();
                TryApplyPendingStatusEffectVaultRebind();
            }
        }

        public static void Shutdown()
        {
            UnregisterCombatRegistryHotSwapBridge();
            BallisticsRuntime.Shutdown();

            if (_damageJobScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: true);
                _damageJobScheduled = false;
                FinishArmorPenetrationScheduledCompletion();
            }

            if (_statusJobScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _statusJobHandle, forceComplete: true);
                _statusJobScheduled = false;
                CompleteStatusEffectFrame();
            }

            if (_damageSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(CombatDamageRuntime), nameof(_damageSignals));
                _damageSignals.Dispose();
                _damageSignals = default;
            }

            DisposeNativeArray(ref _signalDetails);
            DisposeArmorPenetrationNativeState();
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
            DisposeNativeArray(ref _targetForwardVectors);
            DisposeNativeArray(ref _targetHeights);
            DisposeNativeArray(ref _targetFlags);
            DisposeNativeArray(ref _statusMasks);
            DisposeNativeArray(ref _statusDurations0123);
            DisposeNativeArray(ref _legacyStatusDurations4567);
            DisposeNativeArray(ref _brittleDurations);
            DisposeNativeArray(ref _damageArmorLut);
            DisposeNativeArray(ref _results);
            DisposeNativeArray(ref _statusResults);
            DisposeNativeArray(ref _statusResultActive);
            DisposeNativeArray(ref _counters);
            DisposeNativeArray(ref _telemetryRing);
            DisposeNativeArray(ref _telemetryState);
            ShutdownStatusEffectStorage();
            if (_receivers != null)
                System.Array.Clear(_receivers, 0, _receivers.Length);
            if (_receiverTransforms != null)
                System.Array.Clear(_receiverTransforms, 0, _receiverTransforms.Length);
            if (_targetBodies != null)
                System.Array.Clear(_targetBodies, 0, _targetBodies.Length);
            _receivers = null;
            _receiverTransforms = null;
            _targetBodies = null;
            _targetCount = 0;
            _queuedSignalCount = 0;
            ResetArmorPenetrationTransientState();
            _requestedVisualQualityWeight01 = 1f;
            _visualQualityWeight01 = 1f;
            _telemetryDumpedThisSession = false;
            _combatDataVault = null;
            _combatDataVaultColdCacheAttempted = false;
        }

        private static void EnsureInitialized()
        {
            RegisterCombatRegistryHotSwapBridge();

            if (_damageSignals.IsCreated)
                return;

            _damageSignals = new NativeQueue<CombatDamageRequest>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<CombatDamageRequest>[1024] - combat damage ingress lane - owner: CombatDamageRuntime
            NativeMemorySentinel.RegisterNativeQueue(
                _damageSignals,
                MaxQueuedSignals,
                nameof(CombatDamageRuntime),
                nameof(_damageSignals),
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _damageSignals, MaxQueuedSignals);
            _signalDetails = AllocateArray<CombatDamageSignalDetail>(MaxQueuedSignals, nameof(_signalDetails));

            _slotByTargetId = new NativeParallelHashMap<int, int>(MaxTargets, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeParallelHashMap<int,int>[2048] - target id to health slot map - owner: CombatDamageRuntime
            NativeMemorySentinel.RegisterNativeParallelHashMap(
                _slotByTargetId,
                nameof(CombatDamageRuntime),
                nameof(_slotByTargetId),
                NativeAllocationLifetime.Session);

            _instanceIds = AllocateArray<int>(MaxTargets, nameof(_instanceIds));
            _health = AllocateArray<float>(MaxTargets, nameof(_health));
            _maxHealth = AllocateArray<float>(MaxTargets, nameof(_maxHealth));
            _invMaxHealth = AllocateArray<float>(MaxTargets, nameof(_invMaxHealth));
            _armorValues = AllocateArray<int>(MaxTargets, nameof(_armorValues));
            _shieldValues = AllocateArray<float>(MaxTargets, nameof(_shieldValues));
            _minorDamageAccumulators = AllocateArray<float>(MaxTargets, nameof(_minorDamageAccumulators));
            _targetForwardVectors = AllocateArray<float3>(MaxTargets, nameof(_targetForwardVectors));
            _targetHeights = AllocateArray<float>(MaxTargets, nameof(_targetHeights));
            _targetFlags = AllocateArray<uint>(MaxTargets, nameof(_targetFlags));
            _statusMasks = AllocateArray<uint>(MaxTargets, nameof(_statusMasks));
            _statusDurations0123 = AllocateArray<float4>(MaxTargets, nameof(_statusDurations0123));
            _legacyStatusDurations4567 = AllocateArray<float4>(MaxTargets, nameof(_legacyStatusDurations4567));
            _brittleDurations = AllocateArray<float>(MaxTargets, nameof(_brittleDurations));
            _damageArmorLut = AllocateArray<float>(DamageArmorLutLength, nameof(_damageArmorLut));
            _results = AllocateArray<CombatDamageResult>(MaxResults, nameof(_results));
            _statusResults = AllocateArray<CombatDamageResult>(MaxTargets, nameof(_statusResults));
            _statusResultActive = AllocateArray<byte>(MaxTargets, nameof(_statusResultActive));
            _counters = AllocateArray<int>(CounterLength, nameof(_counters));
            _telemetryRing = AllocateArray<CombatTelemetryEntry>(TelemetryFrameCapacity, nameof(_telemetryRing));
            _telemetryState = AllocateArray<uint>(TelemetryStateLength, nameof(_telemetryState));
            EnsureStatusEffectStorage();
            _receivers = new IDamageReceiver[MaxTargets]; // COLD ALLOC: IDamageReceiver[2048] - managed fanout mirror for native target slots - owner: CombatDamageRuntime
            _receiverTransforms = new Transform[MaxTargets]; // COLD ALLOC: Transform[2048] - world/local conversion mirror for combat receivers - owner: CombatDamageRuntime
            _targetBodies = new Rigidbody[MaxTargets]; // COLD ALLOC: Rigidbody[2048] - cached pushback bodies for combat receivers - owner: CombatDamageRuntime
            EnsureArmorPenetrationNativeState();
            InitializeDamageArmorLut();
            RefreshRuntimePolicy();
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
            return !_damageJobScheduled && !_statusJobScheduled;
        }

        private static bool CanUseExistingTargetSlot(int slot)
        {
            return _targetCount > 0 &&
                   (uint)slot < (uint)_targetCount &&
                   CanUseTargetStorageSlot(slot);
        }

        private static bool CanUseRegistrationTargetSlot(int slot)
        {
            return (uint)slot < (uint)MaxTargets &&
                   CanUseTargetStorageSlot(slot);
        }

        private static bool CanUseTargetStorageSlot(int slot)
        {
            return _slotByTargetId.IsCreated &&
                   _instanceIds.IsCreated &&
                   (uint)slot < (uint)_instanceIds.Length &&
                   _health.IsCreated &&
                   (uint)slot < (uint)_health.Length &&
                   _maxHealth.IsCreated &&
                   (uint)slot < (uint)_maxHealth.Length &&
                   _invMaxHealth.IsCreated &&
                   (uint)slot < (uint)_invMaxHealth.Length &&
                   _armorValues.IsCreated &&
                   (uint)slot < (uint)_armorValues.Length &&
                   _shieldValues.IsCreated &&
                   (uint)slot < (uint)_shieldValues.Length &&
                   _minorDamageAccumulators.IsCreated &&
                   (uint)slot < (uint)_minorDamageAccumulators.Length &&
                   _targetForwardVectors.IsCreated &&
                   (uint)slot < (uint)_targetForwardVectors.Length &&
                   _targetHeights.IsCreated &&
                   (uint)slot < (uint)_targetHeights.Length &&
                   _targetFlags.IsCreated &&
                   (uint)slot < (uint)_targetFlags.Length &&
                   _statusMasks.IsCreated &&
                   (uint)slot < (uint)_statusMasks.Length &&
                   _statusDurations0123.IsCreated &&
                   (uint)slot < (uint)_statusDurations0123.Length &&
                   _legacyStatusDurations4567.IsCreated &&
                   (uint)slot < (uint)_legacyStatusDurations4567.Length &&
                   _brittleDurations.IsCreated &&
                   (uint)slot < (uint)_brittleDurations.Length &&
                   _statusResults.IsCreated &&
                   (uint)slot < (uint)_statusResults.Length &&
                   _statusResultActive.IsCreated &&
                   (uint)slot < (uint)_statusResultActive.Length &&
                   _statusEffectStates.IsCreated &&
                   (uint)slot < (uint)_statusEffectStates.Length &&
                   IsManagedMirrorSlotReadable(slot);
        }

        private static void DispatchResults()
        {
            if (!_counters.IsCreated ||
                (uint)CounterResultCount >= (uint)_counters.Length ||
                !_results.IsCreated)
            {
                return;
            }

            int resultCount = math.min(math.max(0, _counters[CounterResultCount]), math.min(MaxResults, _results.Length));
            for (int resultIndex = 0; resultIndex < resultCount; resultIndex++)
            {
                CombatDamageResult result = _results[resultIndex];
                RecordTelemetry(in result, resultIndex, CombatTelemetryPhaseDamage);
                int slot;
                if (!_slotByTargetId.TryGetValue(result.TargetId, out slot))
                    continue;
                if (!IsManagedMirrorSlotReadable(slot))
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
                DispatchManagedSideEffects(in result, receiver, slot);
            }

            _counters[CounterResultCount] = 0;
        }

        private static bool IsManagedMirrorSlotReadable(int slot)
        {
            return _receivers != null &&
                   _receiverTransforms != null &&
                   _targetBodies != null &&
                   (uint)slot < (uint)_receivers.Length &&
                   (uint)slot < (uint)_receiverTransforms.Length &&
                   (uint)slot < (uint)_targetBodies.Length;
        }

        private static void DispatchStatusResults()
        {
            int targetCount = math.min(
                math.max(0, _targetCount),
                math.min(_statusResultActive.IsCreated ? _statusResultActive.Length : 0, _statusResults.IsCreated ? _statusResults.Length : 0));
            for (int slot = 0; slot < targetCount; slot++)
            {
                if (_statusResultActive[slot] == 0)
                    continue;

                _statusResultActive[slot] = 0;
                CombatDamageResult result = _statusResults[slot];
                RecordTelemetry(in result, slot, CombatTelemetryPhaseStatus);
            }
        }

        private static void RecordTelemetry(in CombatDamageResult result, int sequence, uint phaseHash)
        {
            if (!_telemetryRing.IsCreated ||
                !_telemetryState.IsCreated ||
                _telemetryRing.Length <= 0 ||
                _telemetryState.Length < TelemetryStateLength)
            {
                return;
            }

            int ringLength = math.min(TelemetryFrameCapacity, _telemetryRing.Length);
            uint writeCursor = _telemetryState[TelemetryWriteCursorIndex];
            int writeIndex = (int)(writeCursor % (uint)ringLength);
            uint anomalyHash = ResolveTelemetryAnomalyHash(in result);
            _telemetryRing[writeIndex] = new CombatTelemetryEntry
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                Sequence = unchecked((uint)math.max(0, sequence)),
                PhaseHash = phaseHash,
                TargetHash = unchecked((uint)result.TargetId),
                SourceHash = unchecked((uint)result.SourceId),
                StatusBits = result.StatusBits,
                StateHash = math.hash(new uint4(
                    unchecked((uint)result.TargetId),
                    unchecked((uint)result.SourceId),
                    result.StatusBits,
                    unchecked((uint)result.Flags))),
                AnomalyHash = anomalyHash,
                PreviousHealth = result.PreviousHealth,
                NextHealth = result.NextHealth,
                AppliedDamage = result.AppliedDamage,
                LocalPoint = math.select(float3.zero, result.LocalPoint, new bool3(math.all(math.isfinite(result.LocalPoint)))),
                Flags = result.Flags,
                TraumaLevel = result.TraumaLevel,
                DirectionOctant = result.DirectionOctant,
                Reserved = 0u
            };
            _telemetryState[TelemetryWriteCursorIndex] = writeCursor + 1u;

            if (anomalyHash == 0u)
                return;

            _telemetryState[TelemetryLastAnomalyIndex] = anomalyHash;
            PublishCombatTelemetryAnomaly(anomalyHash, result.AppliedDamage, TelemetrySeverityCritical, TelemetryFlagResultAnomaly);
            TryDumpCombatTelemetry(anomalyHash);
        }

        private static uint ResolveTelemetryAnomalyHash(in CombatDamageResult result)
        {
            if (!math.isfinite(result.PreviousHealth))
                return 0xC0BA0001u;
            if (!math.isfinite(result.NextHealth))
                return 0xC0BA0002u;
            if (!math.isfinite(result.AppliedDamage))
                return 0xC0BA0003u;
            if (!math.isfinite(result.MaxHealth))
                return 0xC0BA0004u;
            if (!math.all(math.isfinite(result.LocalPoint)))
                return 0xC0BA0005u;
            if (!math.all(math.isfinite(result.Direction)))
                return 0xC0BA0006u;
            if (!math.all(math.isfinite(result.SurfaceNormal)))
                return 0xC0BA0007u;

            return 0u;
        }

        private static void PublishCombatTelemetryAnomaly(uint anomalyHash, float scalar, byte severity, byte flags)
        {
            if (anomalyHash == 0u)
                return;

            SignalBus<TelemetryAnomalySignal>.TryPush(new TelemetryAnomalySignal
            {
                SystemHash = CombatTelemetrySystemHash,
                AnomalyHash = anomalyHash,
                Scalar = math.select(0f, scalar, math.isfinite(scalar)),
                Frame = unchecked((uint)Time.frameCount),
                Severity = severity,
                Flags = flags
            });
        }

        private static void PublishQueueRejectAnomaly(uint anomalyHash, float amount)
        {
            if (anomalyHash == 0u)
                return;

            uint frame = unchecked((uint)Time.frameCount);
            if (_lastQueueRejectFrame == frame && _lastQueueRejectAnomalyHash == anomalyHash)
                return;

            _lastQueueRejectFrame = frame;
            _lastQueueRejectAnomalyHash = anomalyHash;
            PublishCombatTelemetryAnomaly(
                anomalyHash,
                amount,
                TelemetrySeverityWarning,
                TelemetryFlagQueueRejected);
        }

        private static void TryDumpCombatTelemetry(uint anomalyHash)
        {
            if (_telemetryDumpedThisSession ||
                !_telemetryRing.IsCreated ||
                _telemetryRing.Length <= 0)
            {
                return;
            }

            int count = math.min(_telemetryRing.Length, TelemetryFrameCapacity);
            if (count <= 0)
                return;

            bool stateReadable = _telemetryState.IsCreated &&
                (uint)TelemetryWriteCursorIndex < (uint)_telemetryState.Length;
            uint cursor = stateReadable ? _telemetryState[TelemetryWriteCursorIndex] : 0u;

            try
            {
                string dumpPath = Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "AgentLogs",
                    "Dump_SHINOBU_318_Combat.bin");
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(CombatTelemetryMagicLow);
                    writer.Write(CombatTelemetryMagicHigh);
                    writer.Write((uint)count);
                    writer.Write((uint)CombatTelemetryEntrySizeBytes);
                    writer.Write(cursor);
                    writer.Write(anomalyHash);
                    int start = cursor >= (uint)count && count > 0
                        ? (int)(cursor % (uint)count)
                        : 0;

                    for (int i = 0; i < count; i++)
                    {
                        int index = (start + i) % count;
                        WriteTelemetryEntry(writer, _telemetryRing[index]);
                    }
                }

                _telemetryDumpedThisSession = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteTelemetryEntry(BinaryWriter writer, in CombatTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.Sequence);
            writer.Write(entry.PhaseHash);
            writer.Write(entry.TargetHash);
            writer.Write(entry.SourceHash);
            writer.Write(entry.StatusBits);
            writer.Write(entry.StateHash);
            writer.Write(entry.AnomalyHash);
            writer.Write(entry.PreviousHealth);
            writer.Write(entry.NextHealth);
            writer.Write(entry.AppliedDamage);
            writer.Write(entry.LocalPoint.x);
            writer.Write(entry.LocalPoint.y);
            writer.Write(entry.LocalPoint.z);
            writer.Write(entry.Flags);
            writer.Write(entry.TraumaLevel);
            writer.Write(entry.DirectionOctant);
            writer.Write(entry.Reserved);
        }

        private static void DispatchManagedSideEffects(in CombatDamageResult result, IDamageReceiver receiver, int slot)
        {
            if ((result.StatusBits & CombatStatusBits.Crippled) != 0u &&
                receiver is ICombatMobilityModifierReceiver mobilityReceiver)
            {
                mobilityReceiver.SetCombatMobilityScale(CrippledMobilitySpeedScale, CrippledMobilityDurationSeconds);
            }

            if ((result.StatusBits & CombatStatusBits.Fractured) != 0u &&
                receiver is ICombatMobilityModifierReceiver fractureReceiver)
            {
                fractureReceiver.SetCombatMobilityScale(CrippledMobilitySpeedScale, CrippledMobilityDurationSeconds);
            }

            if ((result.StatusBits & CombatStatusBits.Stunned) != 0u &&
                receiver is ICombatMobilityModifierReceiver stunnedReceiver)
            {
                stunnedReceiver.SetCombatMobilityScale(
                    ResolveStatusMobilityScale(result.StatusBits, ReadStatusEffectTuning()),
                    DefaultStunStatusDurationSeconds);
            }

            if ((result.Flags & CombatDamageResultFlags.BloodScent) != 0)
                TryEmitBloodScent(in result, slot);

            if (result.AppliedDamage > 0f)
                TryApplyKineticPushback(in result, slot);

            if ((result.Flags & CombatDamageResultFlags.TargetKilled) != 0)
                TryEmitEntityDeathSignal(in result, slot);

            if ((result.StatusBits & CombatStatusBits.Poisoned) != 0u &&
                (result.Flags & CombatDamageResultFlags.StatusChanged) != 0)
            {
                TryDiffusePoison(in result, slot);
            }

        }

        private static void TryEmitBloodScent(in CombatDamageResult result, int slot)
        {
            if (result.AppliedDamage <= 0f || !TryResolveWorldPoint(in result, slot, out Vector3 worldPoint))
                return;

            float intensity = math.saturate(result.AppliedDamage * math.rcp(math.max(0.0001f, result.MaxHealth)));
            ChemicalInfluenceGrid.QueueBloodScent(worldPoint, intensity);
            if (!TryResolveAupFromRuntimeOrigin(worldPoint, out AbsoluteUniversePosition positionAup))
                return;

            SignalBus<DebrisSpawnSignal>.TryPush(new DebrisSpawnSignal
            {
                PositionAup = positionAup,
                SpeciesHash = unchecked((uint)result.TargetId),
                SourceEntityId = unchecked((uint)result.SourceId),
                Intensity01 = intensity,
                DebrisKind = BloodDebrisKind,
                Flags = 0
            });
        }

        private static void TryApplyKineticPushback(in CombatDamageResult result, int slot)
        {
            if (_targetBodies == null || (uint)slot >= (uint)_targetBodies.Length)
                return;

            Rigidbody body = _targetBodies[slot];
            if (body == null)
                return;

            float3 pushDirection = ResolveExactDirection(result.Direction);
            if (!math.all(math.isfinite(pushDirection)) || math.lengthsq(pushDirection) <= 0.0001f)
                return;

            Vector3 force = new Vector3(pushDirection.x, pushDirection.y, pushDirection.z) *
                            math.max(0f, result.AppliedDamage) * 10f;
            PhysicsForceRouter.QueueForce(body, force, ForceMode.Impulse);
        }

        private static void TryEmitEntityDeathSignal(in CombatDamageResult result, int slot)
        {
            if (!TryResolveWorldPoint(in result, slot, out Vector3 worldPoint))
                return;

            if (!TryResolveAupFromRuntimeOrigin(worldPoint, out AbsoluteUniversePosition positionAup))
                return;

            SignalBus<EntityDeathSignal>.TryPush(new EntityDeathSignal
            {
                PositionAup = positionAup,
                EntityHash = unchecked((uint)result.TargetId),
                SourceHash = unchecked((uint)result.SourceId),
                Intensity01 = 1f,
                Flags = 0
            });
        }

        private static void TryDiffusePoison(in CombatDamageResult result, int sourceSlot)
        {
            if (!TryResolveWorldPoint(in result, sourceSlot, out Vector3 worldPoint))
                return;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                worldPoint,
                PoisonDiffusionRadiusMeters,
                SpatialTargetKind.Bioform,
                _poisonDiffusionHits);

            int queuedTargetCount = 0;
            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit hit = _poisonDiffusionHits[i];
                if (!TryResolveRegisteredSpatialTarget(in hit, out int targetId, out _))
                    continue;

                if (targetId == 0 || targetId == result.TargetId)
                    continue;

                bool duplicateTarget = false;
                for (int j = 0; j < queuedTargetCount; j++)
                {
                    if (_poisonDiffusionTargetIds[j] == targetId)
                    {
                        duplicateTarget = true;
                        break;
                    }
                }

                if (duplicateTarget)
                    continue;

                _poisonDiffusionTargetIds[queuedTargetCount] = targetId;
                queuedTargetCount++;
                if (!TryQueueStatusEffect(
                        targetId,
                        CombatStatusBits.Poisoned64,
                        DefaultPoisonStatusDurationSeconds,
                        result.SourceId,
                        1f))
                {
                    return;
                }
            }
        }

        private static bool TryResolveRegisteredSpatialTarget(
            in SpatialQueryHit hit,
            out int targetId,
            out Transform receiverTransform)
        {
            targetId = 0;
            receiverTransform = null;

            if (hit.Owner != null &&
                TryResolveRegisteredTargetFromTransform(hit.Owner.transform, out targetId, out receiverTransform))
            {
                return true;
            }

            if (hit.Transform != null &&
                TryResolveRegisteredTargetFromTransform(hit.Transform, out targetId, out receiverTransform))
            {
                return true;
            }

            return hit.Rigidbody != null &&
                   TryResolveRegisteredTargetFromTransform(hit.Rigidbody.transform, out targetId, out receiverTransform);
        }

        private static bool TryResolveRegisteredTargetFromTransform(
            Transform candidate,
            out int targetId,
            out Transform receiverTransform)
        {
            targetId = 0;
            receiverTransform = null;
            Transform current = candidate;
            for (int depth = 0; depth < 6 && current != null; depth++)
            {
                int candidateId = ResolveTargetId(current.gameObject);
                if (candidateId != 0 &&
                    _slotByTargetId.TryGetValue(candidateId, out int slot))
                {
                    if (_receiverTransforms == null || (uint)slot >= (uint)_receiverTransforms.Length)
                        return false;

                    targetId = candidateId;
                    receiverTransform = _receiverTransforms[slot] != null ? _receiverTransforms[slot] : current;
                    return receiverTransform != null;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool TryResolveWorldPoint(
            in CombatDamageResult result,
            int slot,
            out Vector3 worldPoint)
        {
            worldPoint = default;
            if (_receiverTransforms == null || (uint)slot >= (uint)_receiverTransforms.Length)
                return false;

            Transform receiverTransform = _receiverTransforms[slot];
            if (receiverTransform == null)
                return false;

            Vector3 localPoint = new Vector3(result.LocalPoint.x, result.LocalPoint.y, result.LocalPoint.z);
            worldPoint = receiverTransform.TransformPoint(localPoint);
            return math.all(math.isfinite(new float3(worldPoint.x, worldPoint.y, worldPoint.z)));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static byte QuantizeDelta(float previousHealth, float nextHealth, float maximumHealth)
        {
            float invMax = math.rcp(math.max(0.0001f, maximumHealth));
            return (byte)math.clamp((int)math.round(math.abs(previousHealth - nextHealth) * invMax * byte.MaxValue), 0, byte.MaxValue);
        }

        private static void ClearCounters()
        {
            if (!_counters.IsCreated)
                return;

            int count = math.min(CounterLength, _counters.Length);
            for (int i = 0; i < count; i++)
                _counters[i] = 0;
        }

        private static void ClearSlot(int slot)
        {
            _instanceIds[slot] = 0;
            _health[slot] = 0f;
            _maxHealth[slot] = 0f;
            _invMaxHealth[slot] = 0f;
            _armorValues[slot] = 0;
            _shieldValues[slot] = 0f;
            _minorDamageAccumulators[slot] = 0f;
            _targetForwardVectors[slot] = float3.zero;
            _targetHeights[slot] = 0f;
            _targetFlags[slot] = 0u;
            ClearTargetArmorState(slot);
            _statusMasks[slot] = 0u;
            _statusDurations0123[slot] = float4.zero;
            _legacyStatusDurations4567[slot] = float4.zero;
            _brittleDurations[slot] = 0f;
            ResetStatusEffectSlot(slot);
            _statusResults[slot] = default;
            _statusResultActive[slot] = 0;
            _receivers[slot] = null;
            _receiverTransforms[slot] = null;
            _targetBodies[slot] = null;
        }

        private static void CaptureReceiverManagedRefs(int slot, IDamageReceiver receiver)
        {
            _receivers[slot] = receiver;
            _receiverTransforms[slot] = ResolveReceiverTransform(receiver);
            _targetBodies[slot] = ResolveReceiverBody(receiver);
        }

        private static void RegisterBallisticRootPrimitive(
            int targetId,
            IDamageReceiver receiver,
            float targetHeight,
            CombatArmorClass armorClass)
        {
            Transform receiverTransform = ResolveReceiverTransform(receiver);
            if (receiverTransform == null)
                return;

            BallisticsRuntime.RegisterCombatTargetAabb(targetId, receiverTransform, targetHeight, armorClass);
        }

        private static void RefreshBallisticTargetAabbs()
        {
            if (_targetCount <= 0 ||
                _receiverTransforms == null ||
                !_instanceIds.IsCreated ||
                !_targetFlags.IsCreated ||
                !_targetHeights.IsCreated)
            {
                return;
            }

            int count = math.min(
                math.max(0, _targetCount),
                math.min(_receiverTransforms.Length, math.min(_instanceIds.Length, math.min(_targetFlags.Length, _targetHeights.Length))));
            for (int i = 0; i < count; i++)
            {
                Transform receiverTransform = _receiverTransforms[i];
                int targetId = _instanceIds[i];
                if (receiverTransform == null || targetId == 0)
                    continue;

                CombatArmorClass armorClass = (CombatArmorClass)(_targetFlags[i] & TargetFlagArmorMask);
                BallisticsRuntime.RegisterCombatTargetAabb(targetId, receiverTransform, _targetHeights[i], armorClass);
            }
        }

        private static Transform ResolveReceiverTransform(IDamageReceiver receiver)
        {
            return receiver is Component component && component != null
                ? component.transform
                : null;
        }

        private static Rigidbody ResolveReceiverBody(IDamageReceiver receiver)
        {
            if (receiver is ICombatPushbackBodySource bodySource)
                return bodySource.CombatPushbackBody;

            if (!(receiver is Component component) || component == null)
                return null;

            if (component.TryGetComponent(out Rigidbody body))
                return body;

            return null;
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

        private static float3 ResolveReceiverForward(IDamageReceiver receiver)
        {
            if (receiver is ICombatHitProfileSource hitProfile)
            {
                Vector3 combatForward = hitProfile.CombatForward;
                return NormalizeOrDefault(
                    new float3(combatForward.x, combatForward.y, combatForward.z),
                    new float3(0f, 0f, 1f));
            }

            if (receiver is Component component && component != null)
            {
                Vector3 forward = component.transform.forward;
                return NormalizeOrDefault(new float3(forward.x, forward.y, forward.z), new float3(0f, 0f, 1f));
            }

            return new float3(0f, 0f, 1f);
        }

        private static float ResolveReceiverHeight(IDamageReceiver receiver)
        {
            if (receiver is ICombatHitProfileSource hitProfile)
                return math.max(0.0001f, hitProfile.CombatHeight);

            if (receiver is Component component && component != null)
            {
                Vector3 scale = component.transform.lossyScale;
                return math.max(0.0001f, math.abs(scale.y));
            }

            return 1f;
        }

        private static void RefreshTargetHitProfile(int slot)
        {
            if (_receivers == null ||
                (uint)slot >= (uint)_receivers.Length ||
                !_targetForwardVectors.IsCreated ||
                (uint)slot >= (uint)_targetForwardVectors.Length ||
                !_targetHeights.IsCreated ||
                (uint)slot >= (uint)_targetHeights.Length)
            {
                return;
            }

            IDamageReceiver receiver = _receivers[slot];
            if (receiver == null)
                return;

            _targetForwardVectors[slot] = ResolveReceiverForward(receiver);
            _targetHeights[slot] = ResolveReceiverHeight(receiver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int QuantizeArmorValue(float armorValue)
        {
            return math.max(0, (int)math.round(armorValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & (lengthSq > 0.0001f);
            float3 selected = math.select(fallback, value, new bool3(valid));
            return selected * math.rsqrt(math.max(math.lengthsq(selected), 0.0001f));
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackDamageClassMetaFast(uint packedMeta)
        {
            uint damageClass = (uint)ResolveDamageClass(ReadDamageType(packedMeta));
            return (packedMeta & MetaDamageClassClearMask) |
                   ((damageClass & MetaDamageClassMask) << MetaDamageClassShift);
        }

        private static void RefreshRuntimePolicy()
        {
            float qualityWeight01 = SignalBusRegistry.GlobalQualityWeight01;
            float requestedWeight01 = SanitizeQualityWeight01(_requestedVisualQualityWeight01);
            _visualQualityWeight01 = SanitizeQualityWeight01(qualityWeight01) * requestedWeight01;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeQualityWeight01(float qualityWeight01)
        {
            return math.saturate(math.select(1f, qualityWeight01, math.isfinite(qualityWeight01)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ProcessDamageQueueJob : IJob
        {
            [NoAlias]
            public NativeQueue<CombatDamageRequest> Signals;
            [ReadOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> SignalDetails;
            [ReadOnly, NoAlias] public NativeArray<double3> SignalImpactAups;
            [ReadOnly, NoAlias] public NativeParallelHashMap<int, int> SlotByTargetId;
            [ReadOnly, NoAlias] public NativeArray<int> InstanceIds;
            [NoAlias]
            public NativeArray<float> Health;
            [ReadOnly, NoAlias] public NativeArray<float> MaxHealth;
            [ReadOnly, NoAlias] public NativeArray<float> InvMaxHealth;
            [NoAlias]
            public NativeArray<int> ArmorValues;
            [NoAlias]
            public NativeArray<float> ShieldValues;
            [NoAlias]
            public NativeArray<float> MinorDamageAccumulators;
            [ReadOnly, NoAlias] public NativeArray<float3> TargetForwardVectors;
            [ReadOnly, NoAlias] public NativeArray<float> TargetHeights;
            [ReadOnly, NoAlias] public NativeArray<uint> TargetFlags;
            [ReadOnly, NoAlias] public NativeArray<double3> TargetRootAups;
            [ReadOnly, NoAlias] public NativeArray<quaternion> TargetRotations;
            [ReadOnly, NoAlias] public NativeArray<float3> TargetHalfExtents;
            [ReadOnly, NoAlias] public NativeArray<ArmorProfileDTO> TargetArmorProfiles;
            [NoAlias]
            public NativeArray<CombatStatusEffectState> StatusEffectStates;
            [NoAlias]
            public NativeArray<uint> StatusMasks;
            [NoAlias]
            public NativeArray<float4> StatusDurations0123;
            [NoAlias]
            public NativeArray<float4> LegacyStatusDurations4567;
            [NoAlias]
            public NativeArray<float> BrittleDurations;
            [ReadOnly, NoAlias] public NativeArray<float> DamageArmorLut;
            [NoAlias] public NativeArray<ArmorPenetrationTelemetryEntry> ArmorTelemetryRing;
            [NoAlias] public NativeArray<ArmorPenetrationDebugHitDTO> ArmorDebugHits;
            [WriteOnly, NoAlias] public NativeArray<CombatDamageResult> Results;
            [NoAlias]
            public NativeArray<int> Counters;
            public NativeQueue<DeflectSignal>.ParallelWriter DeflectSignalWriter;
            [NativeDisableParallelForRestriction] public NativeArray<int> DeflectSignalWriterBudget;
            public NativeQueue<ImpactSignal>.ParallelWriter ImpactSignalWriter;
            [NativeDisableParallelForRestriction] public NativeArray<int> ImpactSignalWriterBudget;
            public int SignalBudget;
            public float VisualQualityWeight01;
            public ArmorPenetrationTuningDTO ArmorTuning;
            public int ArmorTelemetryIndex;
            public uint ArmorFrameIndex;

            public void Execute()
            {
                int processed = 0;
                uint armorDeflectCount = 0u;
                uint armorWeakPointHits = 0u;
                float armorMitigatedSum = 0f;
                while (processed < SignalBudget && Signals.TryDequeue(out CombatDamageRequest signal))
                {
                    processed++;
                    if (!SlotByTargetId.TryGetValue(signal.TargetId, out int slot))
                    {
                        Counters[CounterMissingTargets] = Counters[CounterMissingTargets] + 1;
                        continue;
                    }

                    if (!IsValidDamageSlot(slot))
                    {
                        Counters[CounterDroppedResults] = Counters[CounterDroppedResults] + 1;
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
                    int detailIndex = ReadDetailIndex(signal.PackedMeta);
                    if ((uint)detailIndex >= (uint)SignalDetails.Length)
                    {
                        Counters[CounterDroppedResults] = Counters[CounterDroppedResults] + 1;
                        continue;
                    }

                    CombatDamageSignalDetail detail = SignalDetails[detailIndex];
                    int armorClass = math.clamp((int)(targetFlags & TargetFlagArmorMask), 0, ArmorClassCount - 1);
                    int damageClass = math.clamp((int)ReadDamageClass(signal.PackedMeta), 0, DamageClassCount - 1);
                    float armorMultiplier = DamageArmorLut[(damageClass * ArmorClassCount) + armorClass];
                    ArmorPenetrationSample armorSample;
                    unsafe
                    {
                        armorSample = EvaluateArmorPenetrationForSignal(
                            slot,
                            detailIndex,
                            in signal,
                            in detail,
                            in SignalImpactAups,
                            in TargetRootAups,
                            in TargetRotations,
                            in TargetHalfExtents,
                            in TargetArmorProfiles,
                            in ArmorTuning);
                    }

                    detail.LocalPoint = armorSample.LocalPoint;
                    detail.ArmorNormal = armorSample.SurfaceNormal;
                    float3 projectileDirection = ResolveExactDirection(signal.Direction);
                    float3 armorNormal = ResolveExactDirection(detail.ArmorNormal);
                    float directionalArmorMultiplier = math.saturate(math.dot(projectileDirection, armorNormal) + 0.2f);
                    bool hasDirectionalArmorProof = math.lengthsq(projectileDirection) > 0.0001f &&
                                                     math.lengthsq(armorNormal) > 0.0001f;
                    armorMultiplier *= math.select(1f, directionalArmorMultiplier, hasDirectionalArmorProof);

                    int weakspotTier = ReadWeakspotTier(signal.PackedMeta);
                    float weakspotMultiplier = math.select(1f, 3f, weakspotTier == (int)CombatWeakspotTier.Weakspot);
                    weakspotMultiplier = math.select(
                        weakspotMultiplier,
                        math.max(weakspotMultiplier, HeadshotDamageMultiplier),
                        IsHeadshotFake(detail.LocalPoint, TargetHeights[slot]));
                    float baseAmount = ResolveBranchlessBaseDamage(signal.Amount, signal.Direction, signal.ImpulseMagnitude, kind);
                    float momentumMultiplier = ResolveBranchlessMomentumMultiplier(signal.Amount, signal.Direction);
                    float damageBeforeArmorLut = math.max(0f, baseAmount * momentumMultiplier * weakspotMultiplier * armorMultiplier * armorSample.DamageScalar);
                    float damage = math.max(0f, damageBeforeArmorLut - armorSample.EffectiveArmor);
                    armorMitigatedSum += math.max(0f, damageBeforeArmorLut - damage);
                    armorWeakPointHits += math.select(0u, 1u, armorSample.LutByte <= ArmorWeakPointLutThreshold);
                    WriteArmorDebugHit(ArmorDebugHits, processed - 1, in signal, in armorSample, ArmorFrameIndex);

                    CombatStatusEffectState statusState = StatusEffectStates[slot];
                    uint statusMask = (uint)(statusState.StatusEffectMask & uint.MaxValue);
                    uint statusBefore = statusMask;
                    if ((statusMask & CombatStatusBits.Brittle) != 0u && (damageType & CombatDamageTypes.Impact) != 0u)
                        damage *= BrittleImpactMultiplier;

                    ushort flags = CombatDamageResultFlags.None;
                    float3 attackDirection = ResolveExactDirection(signal.Direction);
                    if (IsHeavilyArmoredFront(armorClass) &&
                        math.lengthsq(attackDirection) > 0.0001f &&
                        TryApplyFrontDeflection(
                            attackDirection,
                            TargetForwardVectors[slot],
                            ref damage,
                            ref flags,
                        out float frontDot))
                    {
                        if (!SignalBus<DeflectSignal>.TryEnqueueBounded(DeflectSignalWriter, DeflectSignalWriterBudget, new DeflectSignal
                        {
                            LocalPoint = detail.LocalPoint,
                            FrontDot = frontDot,
                            TargetHash = unchecked((uint)signal.TargetId),
                            SourceHash = unchecked((uint)signal.SourceId),
                            DamageScalar = DirectionalDeflectDamageScalar,
                            Flags = 0,
                            ArmorClass = (byte)armorClass,
                            Reserved = 0
                        }))
                        {
                            Counters[CounterDroppedResults] = Counters[CounterDroppedResults] + 1;
                        }

                        EmitArmorImpactFeedback(
                            ImpactSignalWriter,
                            ImpactSignalWriterBudget,
                            in armorSample,
                            damageBeforeArmorLut,
                            VisualQualityWeight01,
                            ArmorImpactSignalFlagDirectionalDeflect);
                    }

                    if (armorSample.Deflected != 0u && damage <= ArmorDeflectDamageFloor)
                    {
                        flags |= CombatDamageResultFlags.Deflected;
                        armorDeflectCount++;
                        EmitArmorDeflectFeedback(
                            DeflectSignalWriter,
                            DeflectSignalWriterBudget,
                            ImpactSignalWriter,
                            ImpactSignalWriterBudget,
                            in signal,
                            in detail,
                            in armorSample,
                            armorClass,
                            damageBeforeArmorLut,
                            VisualQualityWeight01);
                    }

                    float shield = ShieldValues[slot];
                    if (shield > 0f && damage > 0f)
                    {
                        float shieldAbsorb = math.min(shield, damage * ShieldAbsorbFraction);
                        shield -= shieldAbsorb;
                        damage -= shieldAbsorb;
                        ShieldValues[slot] = shield;
                        flags |= CombatDamageResultFlags.ShieldAbsorbed;
                    }

                    int armorValue = math.max(0, ArmorValues[slot]);
                    float damageBeforeArmor = damageBeforeArmorLut;
                    if (armorValue > 0 && damageBeforeArmor >= ArmorDegradationDamageThreshold)
                    {
                        ArmorValues[slot] = math.max(0, armorValue - ResolveArmorDegradation(damageBeforeArmor));
                    }

                    if (detail.LocalTemperatureCelsius > ThermalBurnThresholdCelsius)
                    {
                        statusMask |= CombatStatusBits.Burning;
                        SetStatusDurations(slot, CombatStatusBits.Burning, DefaultThermalStatusDurationSeconds, ref statusState);
                    }
                    else if (detail.LocalTemperatureCelsius < ThermalBrittleThresholdCelsius)
                    {
                        statusMask |= CombatStatusBits.Brittle;
                        SetStatusDurations(slot, CombatStatusBits.Brittle, DefaultThermalStatusDurationSeconds, ref statusState);
                    }

                    if (signalStatusBits != 0u)
                    {
                        statusMask |= signalStatusBits;
                        float duration = detail.StatusDurationSeconds > 0f ? detail.StatusDurationSeconds : ResolveDefaultStatusDuration(signalStatusBits);
                        SetStatusDurations(slot, signalStatusBits, duration, ref statusState);
                    }

                    statusMask = (uint)(statusState.StatusEffectMask & uint.MaxValue);
                    if (statusMask != statusBefore)
                        flags |= CombatDamageResultFlags.StatusChanged;
                    StatusMasks[slot] = statusMask;
                    StatusEffectStates[slot] = statusState;

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

                    if (!TryAtomicSubtractHealth(Health, slot, damage, out previousHealth, out float nextHealth))
                        continue;

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

                    if ((flags & CombatDamageResultFlags.WoundTrigger) != 0 &&
                        ShouldEmitHighFidelityWound(slot, signal.SourceId, VisualQualityWeight01))
                    {
                        flags |= CombatDamageResultFlags.HighFidelityWound;
                    }

                    WriteResult(slot, signal, detail, damageType, kind, previousHealth, nextHealth, damage, maxHealth, InvMaxHealth[slot], VisualQualityWeight01, flags);
                }

                Counters[CounterProcessedSignals] = Counters[CounterProcessedSignals] + processed;
                WriteArmorTelemetry(
                    ArmorTelemetryRing,
                    ArmorTelemetryIndex,
                    ArmorFrameIndex,
                    (uint)processed,
                    armorWeakPointHits,
                    armorDeflectCount,
                    armorMitigatedSum,
                    ArmorTuning.GlobalQualityWeight);
            }

            private bool IsValidDamageSlot(int slot)
            {
                return (uint)slot < (uint)InstanceIds.Length &&
                       (uint)slot < (uint)Health.Length &&
                       (uint)slot < (uint)MaxHealth.Length &&
                       (uint)slot < (uint)InvMaxHealth.Length &&
                       (uint)slot < (uint)ArmorValues.Length &&
                       (uint)slot < (uint)ShieldValues.Length &&
                       (uint)slot < (uint)MinorDamageAccumulators.Length &&
                       (uint)slot < (uint)TargetForwardVectors.Length &&
                       (uint)slot < (uint)TargetHeights.Length &&
                       (uint)slot < (uint)TargetFlags.Length &&
                       (uint)slot < (uint)TargetRootAups.Length &&
                       (uint)slot < (uint)TargetRotations.Length &&
                       (uint)slot < (uint)TargetHalfExtents.Length &&
                       (uint)slot < (uint)TargetArmorProfiles.Length &&
                       (uint)slot < (uint)StatusEffectStates.Length &&
                       (uint)slot < (uint)StatusMasks.Length &&
                       (uint)slot < (uint)StatusDurations0123.Length &&
                       (uint)slot < (uint)LegacyStatusDurations4567.Length &&
                       (uint)slot < (uint)BrittleDurations.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsHeadshotFake(float3 localHit, float targetHeight)
            {
                return math.all(math.isfinite(localHit)) &&
                       targetHeight > 0.0001f &&
                       localHit.y > targetHeight * HeadshotHeightFraction;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsHeavilyArmoredFront(int armorClass)
            {
                return armorClass == (int)CombatArmorClass.Structure ||
                       armorClass == (int)CombatArmorClass.OrganicHeavy ||
                       armorClass == (int)CombatArmorClass.Shell ||
                       armorClass == (int)CombatArmorClass.Shielded;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryApplyFrontDeflection(
                float3 attackDirection,
                float3 targetForward,
                ref float damage,
                ref ushort flags,
                out float frontDot)
            {
                float3 forward = ResolveExactDirection(targetForward);
                frontDot = math.dot(attackDirection, forward);
                if (frontDot >= DirectionalDeflectDot)
                    return false;

                damage *= DirectionalDeflectDamageScalar;
                flags |= CombatDamageResultFlags.Deflected;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolveArmorDegradation(float damageBeforeArmor)
            {
                return math.max(1, (int)math.floor(damageBeforeArmor * ArmorDegradationPerDamage));
            }

            private void SetStatusDurations(int slot, uint statusBits, float duration, ref CombatStatusEffectState statusState)
            {
                statusState = ApplyStatusBitsToState(statusState, statusBits, duration, ArmorFrameIndex);
                float4 durations = StatusDurations0123[slot];
                if ((statusBits & CombatStatusBits.Bleeding) != 0u)
                    durations.x = math.max(durations.x, duration);
                if ((statusBits & CombatStatusBits.Crushed) != 0u)
                    durations.y = math.max(durations.y, duration);
                if ((statusBits & CombatStatusBits.Irradiated) != 0u)
                    durations.z = math.max(durations.z, duration);
                if ((statusBits & CombatStatusBits.Hypoxia) != 0u)
                    durations.w = math.max(durations.w, duration);
                StatusDurations0123[slot] = durations;

                float4 legacyDurations = LegacyStatusDurations4567[slot];
                if ((statusBits & CombatStatusBits.Poisoned) != 0u)
                    legacyDurations.x = math.max(legacyDurations.x, duration);
                if ((statusBits & CombatStatusBits.Burning) != 0u)
                    legacyDurations.y = math.max(legacyDurations.y, duration);
                if ((statusBits & CombatStatusBits.Stunned) != 0u)
                    legacyDurations.z = math.max(legacyDurations.z, duration);
                LegacyStatusDurations4567[slot] = legacyDurations;
                if ((statusBits & CombatStatusBits.Brittle) != 0u)
                    BrittleDurations[slot] = math.max(BrittleDurations[slot], duration);
            }

            private static float ResolveDefaultStatusDuration(uint statusBits)
            {
                if ((statusBits & CombatStatusBits.Bleeding) != 0u)
                    return DefaultBleedStatusDurationSeconds;
                if ((statusBits & (CombatStatusBits.Crushed | CombatStatusBits.Irradiated | CombatStatusBits.Hypoxia)) != 0u)
                    return DefaultThermalStatusDurationSeconds;
                if ((statusBits & CombatStatusBits.Poisoned) != 0u)
                    return DefaultPoisonStatusDurationSeconds;
                if ((statusBits & CombatStatusBits.Burning) != 0u)
                    return DefaultThermalStatusDurationSeconds;
                if ((statusBits & CombatStatusBits.Stunned) != 0u)
                    return DefaultStunStatusDurationSeconds;
                if ((statusBits & CombatStatusBits.Fractured) != 0u)
                    return CrippledMobilityDurationSeconds;

                return DefaultThermalStatusDurationSeconds;
            }

            private void WriteResult(
                int slot,
                in CombatDamageRequest signal,
                in CombatDamageSignalDetail detail,
                uint damageType,
                byte kind,
                float previousHealth,
                float nextHealth,
                float damage,
                float maxHealth,
                float invMaxHealth,
                float visualQualityWeight01,
                ushort flags)
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
                    SurfaceNormal = ResolveVisualSurfaceNormal(detail.ArmorNormal, visualQualityWeight01),
                    Depth = 0f
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveCombatDirection(float3 direction, byte kind)
        {
            if (kind == (byte)CombatEntityKind.Player)
                return ResolveExactDirection(direction);

            return ResolveDominantAxisDirection(direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveExactDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            bool valid = (lengthSq > 0.0001f) & math.all(math.isfinite(direction));
            float3 normalized = direction * math.rsqrt(math.max(lengthSq, 0.0001f));
            return math.select(float3.zero, normalized, new bool3(valid));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveApproximateDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            bool valid = (lengthSq > 0.0001f) & math.all(math.isfinite(direction));
            float3 normalized = direction * math.rsqrt(math.max(lengthSq, 0.0001f));
            return math.select(float3.zero, normalized, new bool3(valid));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveVisualSurfaceNormal(float3 normal, float visualQualityWeight01)
        {
            return ResolveExactDirection(normal) * SmoothStep01(visualQualityWeight01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldEmitHighFidelityWound(int slot, int sourceId, float visualQualityWeight01)
        {
            float weight = SmoothStep01(visualQualityWeight01);
            if (weight <= 0f)
                return false;

            uint hash = math.hash(new uint3(unchecked((uint)slot), unchecked((uint)sourceId), 0xC0BADA7Au));
            float threshold = (hash & 0xFFFFu) * (1f / 65535f);
            return threshold <= weight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveDominantAxisDirection(float3 direction)
        {
            return DistanceMath.DominantAxisOrDefault(direction, float3.zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveDirectionOctant(float3 direction)
        {
            float ax = math.abs(direction.x);
            float ay = math.abs(direction.y);
            float az = math.abs(direction.z);
            bool xMajor = (ax >= ay) & (ax >= az);
            bool zMajor = (!xMajor) & (az >= ay);
            int xOctant = math.select(1, 0, direction.x >= 0f);
            int zOctant = math.select(3, 2, direction.z >= 0f);
            int yOctant = math.select(5, 4, direction.y >= 0f);
            int octant = math.select(yOctant, zOctant, zMajor);
            return (byte)math.select(octant, xOctant, xMajor);
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
        private static ulong RotateLeft64(ulong value, int shift)
        {
            return (value << shift) | (value >> (64 - shift));
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
        private static uint ReadDamageClass(uint packedMeta)
        {
            return (packedMeta >> MetaDamageClassShift) & MetaDamageClassMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadDetailIndex(uint packedMeta)
        {
            return (int)((packedMeta >> MetaDetailIndexShift) & MetaDetailIndexMask);
        }
    }
}
