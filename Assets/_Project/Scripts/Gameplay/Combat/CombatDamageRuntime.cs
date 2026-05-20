using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Physics;
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

    [StructLayout(LayoutKind.Explicit, Size = 80)]
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

    public interface ICombatDamageEventListener
    {
        void OnCombatDamageResolved(in CombatDamageResult result);
    }

    public interface ICombatDamageFeedbackReceiver
    {
        void OnCombatDamageFeedback(in CombatDamageResult result, CombatMathLod lod);
    }

    public interface ICombatWeakspot
    {
        CombatWeakspotTier WeakspotTier { get; }
    }

    public interface ICombatLimbHealthSource
    {
        CombatLimbRegion LimbRegion { get; }
        float NormalizedLimbHealth { get; }
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

    public static class CombatDamageRuntime
    {
        private const int MaxTargets = 2048;
        private const int MaxQueuedSignals = 1024;
        private const int MaxGlobalDamageSignalsPerFrame = 64;
        private const int MaxResults = 1024;
        private const int TelemetryFrameCapacity = 300;
        private const int TelemetryStateLength = 2;
        private const int TelemetryWriteCursorIndex = 0;
        private const int TelemetryLastAnomalyIndex = 1;
        private const int CombatTelemetryEntrySizeBytes = 64;
        private const int PoisonDiffusionBufferLength = 16;
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

        private static readonly ProfilerMarker _scheduleMarker = new ProfilerMarker("CombatDamageRuntime.Schedule");
        private static readonly ProfilerMarker _lateFrameMarker = new ProfilerMarker("CombatDamageRuntime.LateFrame");
        private static readonly ProfilerMarker _slowTickMarker = new ProfilerMarker("CombatDamageRuntime.SlowTick");
        private static readonly RegistryBucket<ICombatDamageEventListener> _listeners =
            new RegistryBucket<ICombatDamageEventListener>(ListenerCapacity);
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
        private static byte _mathLod = (byte)CombatMathLod.Low;
        private static byte _requestedMathLod = (byte)CombatMathLod.High;
        private static MathPrecisionLevel _cachedMathPrecision = MathPrecisionLevel.Low;
        private static HectonQualityTier _cachedScalabilityTier = HectonQualityTier.Unknown;

        public static bool IsInitialized => _damageSignals.IsCreated;
        public static int PendingSignalCount => _queuedSignalCount;

        public static void SetCombatMathLod(CombatMathLod lod)
        {
            _requestedMathLod = (byte)lod;
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

        public static void ResolveLocalizedHit(
            Component source,
            out CombatWeakspotTier weakspotTier,
            out uint statusBits)
        {
            weakspotTier = CombatWeakspotTier.None;
            statusBits = 0u;
            if (source == null)
                return;

            ICombatWeakspot weakspot = source.GetComponent<ICombatWeakspot>();
            if (weakspot == null)
                weakspot = source.GetComponentInParent<ICombatWeakspot>();
            if (weakspot != null)
                weakspotTier = weakspot.WeakspotTier;

            if (weakspotTier == CombatWeakspotTier.None &&
                FieldTargetDescriptor.TryResolve(source, out FieldTargetDescriptor descriptor) &&
                descriptor.Role == FieldTargetRole.BioformFractured)
            {
                weakspotTier = CombatWeakspotTier.Weakspot;
            }

            ICombatLimbHealthSource limb = source.GetComponent<ICombatLimbHealthSource>();
            if (limb == null)
                limb = source.GetComponentInParent<ICombatLimbHealthSource>();
            if (limb == null || limb.LimbRegion != CombatLimbRegion.Tail)
                return;

            float health01 = limb.NormalizedLimbHealth;
            if (math.isfinite(health01) && health01 < 0.5f)
                statusBits |= CombatStatusBits.Crippled;
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
            float3 targetForward = ResolveReceiverForward(receiver);
            float targetHeight = ResolveReceiverHeight(receiver);
            int slot;
            if (_slotByTargetId.TryGetValue(targetId, out slot))
            {
                CaptureReceiverManagedRefs(slot, receiver);
                _health[slot] = safeHealth;
                _maxHealth[slot] = safeMaxHealth;
                _invMaxHealth[slot] = math.rcp(safeMaxHealth);
                _armorValues[slot] = QuantizeArmorValue(armorValue);
                _shieldValues[slot] = math.max(0f, shieldValue);
                _targetForwardVectors[slot] = targetForward;
                _targetHeights[slot] = targetHeight;
                _targetFlags[slot] = PackTargetFlags(kind, armorClass);
                RegisterBallisticRootPrimitive(targetId, receiver, targetHeight, armorClass);
                return true;
            }

            if (_targetCount >= MaxTargets)
                return false;

            slot = _targetCount;
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
            _statusMasks[slot] = 0u;
            _statusDurations0123[slot] = float4.zero;
            _legacyStatusDurations4567[slot] = float4.zero;
            _brittleDurations[slot] = 0f;
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

            if (receiver != null && !ReferenceEquals(_receivers[slot], receiver))
                return false;

            BallisticsRuntime.TombstonePrimitivesForTarget(unchecked((uint)targetId));
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
                _targetForwardVectors[slot] = _targetForwardVectors[lastSlot];
                _targetHeights[slot] = _targetHeights[lastSlot];
                _targetFlags[slot] = _targetFlags[lastSlot];
                _statusMasks[slot] = _statusMasks[lastSlot];
                _statusDurations0123[slot] = _statusDurations0123[lastSlot];
                _legacyStatusDurations4567[slot] = _legacyStatusDurations4567[lastSlot];
                _brittleDurations[slot] = _brittleDurations[lastSlot];
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

            _armorValues[slot] = QuantizeArmorValue(armorValue);
            _shieldValues[slot] = math.max(0f, shieldValue);
            return true;
        }

        public static bool SyncTargetHitProfile(int targetId, Vector3 targetForward, float targetHeight)
        {
            if (!_slotByTargetId.IsCreated || !CanMutateTargets())
                return false;

            if (!_slotByTargetId.TryGetValue(targetId, out int slot))
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
            if (!_slotByTargetId.IsCreated)
                return false;

            if (!_slotByTargetId.TryGetValue(targetId, out int slot))
                return false;

            health01 = math.saturate(_health[slot] * _invMaxHealth[slot]);
            return true;
        }

        public static bool TryQueueDamage(in CombatDamageRequest signal)
        {
            CombatDamageSignalDetail detail = default;
            return TryQueueDamage(in signal, in detail);
        }

        public static bool TryQueueDamage(in CombatDamageRequest signal, in CombatDamageSignalDetail detail)
        {
            if (signal.TargetId == 0)
                return false;

            EnsureInitialized();
            if (_damageJobScheduled || _queuedSignalCount >= MaxQueuedSignals)
                return false;

            if (_slotByTargetId.TryGetValue(signal.TargetId, out int targetSlot))
                RefreshTargetHitProfile(targetSlot);

            int detailIndex = _queuedSignalCount;
            SanitizeQueuedSignal(in signal, in detail, out CombatDamageRequest queuedSignal, out CombatDamageSignalDetail queuedDetail, out uint ingressAnomalyHash);
            uint packedMeta = PackDamageClassMetaFast(queuedSignal.PackedMeta);
            queuedSignal.PackedMeta = (packedMeta & MetaDetailIndexClearMask) |
                                      ((uint)detailIndex << MetaDetailIndexShift);
            _signalDetails[detailIndex] = queuedDetail;
            _damageSignals.Enqueue(queuedSignal);
            _queuedSignalCount++;
            if (ingressAnomalyHash != 0u)
                PublishCombatTelemetryAnomaly(ingressAnomalyHash, queuedSignal.Amount, TelemetrySeverityWarning, TelemetryFlagIngressSanitized);
            return true;
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
            DrainGlobalDamageSignals(MaxGlobalDamageSignalsPerFrame);

            if (!_damageSignals.IsCreated || _queuedSignalCount <= 0 || _damageJobScheduled || _statusJobScheduled)
                return;

            using (_scheduleMarker.Auto())
            {
                _mathLod = ResolveRuntimeMathLod();
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
                    TargetForwardVectors = _targetForwardVectors,
                    TargetHeights = _targetHeights,
                    TargetFlags = _targetFlags,
                    StatusMasks = _statusMasks,
                    StatusDurations0123 = _statusDurations0123,
                    LegacyStatusDurations4567 = _legacyStatusDurations4567,
                    BrittleDurations = _brittleDurations,
                    DamageArmorLut = _damageArmorLut,
                    Results = _results,
                    Counters = _counters,
                    DeflectSignalWriter = GlobalSignals.DeflectSignalWriter,
                    SignalBudget = MaxQueuedSignals,
                    MathLod = _mathLod
                };
                _damageJobHandle = job.Schedule();
                _damageJobScheduled = true;
                JobHandle.ScheduleBatchedJobs();
            }
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
                if (!TryBuildCombatSignal(in globalSignal, out CombatDamageRequest combatSignal, out CombatDamageSignalDetail detail))
                    continue;

                if (!TryQueueDamage(in combatSignal, in detail))
                    return;
            }
        }

        private static bool TryBuildCombatSignal(
            in Hecton8.Core.Contracts.Signals.CombatDamageSignal globalSignal,
            out CombatDamageRequest combatSignal,
            out CombatDamageSignalDetail detail)
        {
            combatSignal = default;
            detail = default;

            float magnitude = math.max(0f, globalSignal.Magnitude);
            uint targetId = globalSignal.TargetId != 0
                ? globalSignal.TargetId
                : globalSignal.TargetHash;
            if (targetId == 0u || !(magnitude > 0f))
                return false;

            float3 localPoint = CombatDamageSignalCodec.ToRuntimePointOrZero(in globalSignal);
            float3 direction = math.lengthsq(globalSignal.Direction) > 0.0001f && math.all(math.isfinite(globalSignal.Direction))
                ? globalSignal.Direction
                : ResolveDominantAxisDirection(localPoint);
            uint damageType = globalSignal.DamageType != 0u
                ? globalSignal.DamageType
                : CombatDamageTypes.Impact;

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
                    LegacyStatusDurations4567 = _legacyStatusDurations4567,
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
            BallisticsRuntime.LateFrameTick();
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
            BallisticsRuntime.Shutdown();

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
            _mathLod = (byte)CombatMathLod.Low;
            _requestedMathLod = (byte)CombatMathLod.High;
            _cachedMathPrecision = MathPrecisionLevel.Low;
            _cachedScalabilityTier = HectonQualityTier.Unknown;
            _telemetryDumpedThisSession = false;
            _listeners.Clear();
        }

        private static void EnsureInitialized()
        {
            if (_damageSignals.IsCreated)
                return;

            _damageSignals = new NativeQueue<CombatDamageRequest>(Allocator.Persistent); // COLD ALLOC: NativeQueue<CombatDamageRequest>[1024] - combat damage ingress lane - owner: CombatDamageRuntime
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
            _receivers = new IDamageReceiver[MaxTargets]; // COLD ALLOC: IDamageReceiver[2048] - managed fanout mirror for native target slots - owner: CombatDamageRuntime
            _receiverTransforms = new Transform[MaxTargets]; // COLD ALLOC: Transform[2048] - world/local conversion mirror for combat receivers - owner: CombatDamageRuntime
            _targetBodies = new Rigidbody[MaxTargets]; // COLD ALLOC: Rigidbody[2048] - cached pushback bodies for combat receivers - owner: CombatDamageRuntime
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
                RecordTelemetry(in result, resultIndex, CombatTelemetryPhaseDamage);
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
                if (receiver is ICombatDamageFeedbackReceiver feedbackReceiver)
                    feedbackReceiver.OnCombatDamageFeedback(in result, (CombatMathLod)_mathLod);
                DispatchManagedSideEffects(in result, receiver, slot);
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
                RecordTelemetry(in result, slot, CombatTelemetryPhaseStatus);
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
                    if (receiver is ICombatDamageFeedbackReceiver feedbackReceiver)
                        feedbackReceiver.OnCombatDamageFeedback(in result, (CombatMathLod)_mathLod);
                    DispatchManagedSideEffects(in result, receiver, slot);
                }

                for (int listenerIndex = 0; listenerIndex < listenerCount; listenerIndex++)
                    listeners[listenerIndex].OnCombatDamageResolved(in result);
            }
        }

        private static void RecordTelemetry(in CombatDamageResult result, int sequence, uint phaseHash)
        {
            if (!_telemetryRing.IsCreated || !_telemetryState.IsCreated)
                return;

            uint writeCursor = _telemetryState[TelemetryWriteCursorIndex];
            int writeIndex = (int)(writeCursor % TelemetryFrameCapacity);
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
                LocalPoint = math.all(math.isfinite(result.LocalPoint)) ? result.LocalPoint : float3.zero,
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

            GlobalSignals.Publish(new TelemetryAnomalySignal
            {
                SystemHash = CombatTelemetrySystemHash,
                AnomalyHash = anomalyHash,
                Scalar = math.isfinite(scalar) ? scalar : 0f,
                Frame = unchecked((uint)Time.frameCount),
                Severity = severity,
                Flags = flags
            });
        }

        private static void TryDumpCombatTelemetry(uint anomalyHash)
        {
            if (_telemetryDumpedThisSession || !_telemetryRing.IsCreated)
                return;

            _telemetryDumpedThisSession = true;
            try
            {
                string dumpPath = Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "AgentLogs",
                    "Dump_COMBAT_ARMOR_PENETRATION.bin");
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(CombatTelemetryMagicLow);
                    writer.Write(CombatTelemetryMagicHigh);
                    writer.Write((uint)TelemetryFrameCapacity);
                    writer.Write((uint)CombatTelemetryEntrySizeBytes);
                    writer.Write(_telemetryState.IsCreated ? _telemetryState[TelemetryWriteCursorIndex] : 0u);
                    writer.Write(anomalyHash);

                    for (int i = 0; i < TelemetryFrameCapacity; i++)
                        WriteTelemetryEntry(writer, _telemetryRing[i]);
                }
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
            GlobalSignals.Publish(new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromRuntimePosition(worldPoint),
                SpeciesHash = unchecked((uint)result.TargetId),
                SourceEntityId = unchecked((uint)result.SourceId),
                Intensity01 = intensity,
                DebrisKind = BloodDebrisKind,
                Flags = 0
            });
        }

        private static void TryApplyKineticPushback(in CombatDamageResult result, int slot)
        {
            if ((uint)slot >= (uint)MaxTargets)
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

            GlobalSignals.Publish(new EntityDeathSignal
            {
                PositionAup = AbsoluteUniversePosition.FromRuntimePosition(worldPoint),
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
                if (!TryResolveRegisteredSpatialTarget(in hit, out int targetId, out Transform receiverTransform))
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
                Vector3 localPoint = receiverTransform.InverseTransformPoint(worldPoint);
                CombatDamageRequest poisonSignal = new CombatDamageRequest
                {
                    TargetId = targetId,
                    SourceId = result.SourceId,
                    Amount = 0f,
                    ImpulseMagnitude = 0f,
                    Direction = float3.zero,
                    PackedMeta = PackSignalMeta(
                        CombatDamageTypes.Toxic,
                        CombatStatusBits.Poisoned,
                        CombatWeakspotTier.None)
                };
                CombatDamageSignalDetail detail = new CombatDamageSignalDetail
                {
                    LocalPoint = new float3(localPoint.x, localPoint.y, localPoint.z),
                    ArmorNormal = float3.zero,
                    LocalTemperatureCelsius = 20f,
                    StatusDurationSeconds = DefaultPoisonStatusDurationSeconds
                };

                if (!TryQueueDamage(in poisonSignal, in detail))
                    return;
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
            if ((uint)slot >= (uint)MaxTargets)
                return false;

            Transform receiverTransform = _receiverTransforms[slot];
            if (receiverTransform == null)
                return false;

            Vector3 localPoint = new Vector3(result.LocalPoint.x, result.LocalPoint.y, result.LocalPoint.z);
            worldPoint = receiverTransform.TransformPoint(localPoint);
            return math.all(math.isfinite(new float3(worldPoint.x, worldPoint.y, worldPoint.z)));
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
            _armorValues[slot] = 0;
            _shieldValues[slot] = 0f;
            _minorDamageAccumulators[slot] = 0f;
            _targetForwardVectors[slot] = float3.zero;
            _targetHeights[slot] = 0f;
            _targetFlags[slot] = 0u;
            _statusMasks[slot] = 0u;
            _statusDurations0123[slot] = float4.zero;
            _legacyStatusDurations4567[slot] = float4.zero;
            _brittleDurations[slot] = 0f;
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
            if (_targetCount <= 0 || _receiverTransforms == null)
                return;

            for (int i = 0; i < _targetCount; i++)
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
            if ((uint)slot >= (uint)MaxTargets)
                return;

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
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.0001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveRuntimeMathLod()
        {
            if (_requestedMathLod == (byte)CombatMathLod.Low ||
                _cachedMathPrecision != MathPrecisionLevel.High)
            {
                return (byte)CombatMathLod.Low;
            }

            HectonQualityTier tier = _cachedScalabilityTier;
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra
                ? (byte)CombatMathLod.High
                : (byte)CombatMathLod.Low;
        }

        private static void RefreshRuntimePolicy()
        {
            _cachedMathPrecision = GlobalRegistry.MathPrecision;
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
        }

        [BurstCompile]
        private struct ProcessDamageQueueJob : IJob
        {
            public NativeQueue<CombatDamageRequest> Signals;
            [ReadOnly] public NativeArray<CombatDamageSignalDetail> SignalDetails;
            [ReadOnly] public NativeParallelHashMap<int, int> SlotByTargetId;
            [ReadOnly] public NativeArray<int> InstanceIds;
            public NativeArray<float> Health;
            [ReadOnly] public NativeArray<float> MaxHealth;
            [ReadOnly] public NativeArray<float> InvMaxHealth;
            public NativeArray<int> ArmorValues;
            public NativeArray<float> ShieldValues;
            public NativeArray<float> MinorDamageAccumulators;
            [ReadOnly] public NativeArray<float3> TargetForwardVectors;
            [ReadOnly] public NativeArray<float> TargetHeights;
            [ReadOnly] public NativeArray<uint> TargetFlags;
            public NativeArray<uint> StatusMasks;
            public NativeArray<float4> StatusDurations0123;
            public NativeArray<float4> LegacyStatusDurations4567;
            public NativeArray<float> BrittleDurations;
            [ReadOnly] public NativeArray<float> DamageArmorLut;
            public NativeArray<CombatDamageResult> Results;
            public NativeArray<int> Counters;
            public NativeQueue<DeflectSignal>.ParallelWriter DeflectSignalWriter;
            public int SignalBudget;
            public byte MathLod;

            public void Execute()
            {
                int processed = 0;
                while (processed < SignalBudget && Signals.TryDequeue(out CombatDamageRequest signal))
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
                    int damageClass = (int)ReadDamageClass(signal.PackedMeta);
                    float armorMultiplier = DamageArmorLut[(damageClass * ArmorClassCount) + armorClass];
                    if (MathLod == (byte)CombatMathLod.High)
                    {
                        float3 projectileDirection = ResolveExactDirection(signal.Direction);
                        float3 armorNormal = ResolveExactDirection(detail.ArmorNormal);
                        armorMultiplier *= math.saturate(math.dot(projectileDirection, armorNormal) + 0.2f);
                    }

                    int weakspotTier = ReadWeakspotTier(signal.PackedMeta);
                    float weakspotMultiplier = math.select(1f, 3f, weakspotTier == (int)CombatWeakspotTier.Weakspot);
                    weakspotMultiplier = math.select(
                        weakspotMultiplier,
                        math.max(weakspotMultiplier, HeadshotDamageMultiplier),
                        IsHeadshotFake(detail.LocalPoint, TargetHeights[slot]));
                    float baseAmount = signal.Amount > 0f
                        ? signal.Amount
                        : ResolveKineticDamage(signal.Direction, signal.ImpulseMagnitude, kind);
                    float momentumMultiplier = signal.Amount > 0f ? ResolveMomentumMultiplier(signal.Direction) : 1f;
                    float damage = math.max(0f, baseAmount * momentumMultiplier * weakspotMultiplier * armorMultiplier);

                    uint statusMask = StatusMasks[slot];
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
                        DeflectSignalWriter.Enqueue(new DeflectSignal
                        {
                            LocalPoint = detail.LocalPoint,
                            FrontDot = frontDot,
                            TargetHash = unchecked((uint)signal.TargetId),
                            SourceHash = unchecked((uint)signal.SourceId),
                            DamageScalar = DirectionalDeflectDamageScalar,
                            Flags = 0,
                            ArmorClass = (byte)armorClass,
                            Reserved = 0
                        });
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
                    float damageBeforeArmor = damage;
                    damage = math.max(0f, damage - armorValue);
                    if (armorValue > 0 && damageBeforeArmor >= ArmorDegradationDamageThreshold)
                    {
                        ArmorValues[slot] = math.max(0, armorValue - ResolveArmorDegradation(damageBeforeArmor));
                    }

                    if (detail.LocalTemperatureCelsius > ThermalBurnThresholdCelsius)
                    {
                        statusMask |= CombatStatusBits.Burning;
                        float4 durations = LegacyStatusDurations4567[slot];
                        durations.y = math.max(durations.y, DefaultThermalStatusDurationSeconds);
                        LegacyStatusDurations4567[slot] = durations;
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

                    WriteResult(slot, signal, detail, damageType, kind, previousHealth, nextHealth, damage, maxHealth, InvMaxHealth[slot], MathLod, flags);
                }

                Counters[CounterProcessedSignals] = Counters[CounterProcessedSignals] + processed;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveKineticDamage(float3 impulseVector, float impulseMagnitude, byte kind)
            {
                if (impulseMagnitude > 0f && math.isfinite(impulseMagnitude))
                    return impulseMagnitude;

                float lengthSq = math.lengthsq(impulseVector);
                if (lengthSq <= 0.0001f || !math.all(math.isfinite(impulseVector)))
                    return 0f;

                return kind == (byte)CombatEntityKind.Player
                    ? lengthSq
                    : lengthSq * math.rsqrt(math.max(lengthSq, 0.0001f));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ResolveMomentumMultiplier(float3 attackerVelocity)
            {
                if (!math.all(math.isfinite(attackerVelocity)))
                    return 1f;

                float lengthSq = math.lengthsq(attackerVelocity);
                return lengthSq > 0.0001f
                    ? math.clamp(lengthSq, 1f, MaxMomentumDamageMultiplier)
                    : 1f;
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

            private void SetStatusDurations(int slot, uint statusBits, float duration)
            {
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
                byte mathLod,
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
                    Direction = ResolveCombatDirection(signal.Direction, kind, mathLod),
                    TraumaLevel = ResolveTraumaLevelFromInvMax(damage, invMaxHealth),
                    Flags = flags,
                    Channel = (byte)DamageChannel.Integrity,
                    DirectionOctant = ResolveDirectionOctant(signal.Direction),
                    LocalPoint = detail.LocalPoint,
                    SurfaceNormal = mathLod == (byte)CombatMathLod.High
                        ? ResolveExactDirection(detail.ArmorNormal)
                        : float3.zero,
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
            public NativeArray<float4> LegacyStatusDurations4567;
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
                float4 legacyDurations = LegacyStatusDurations4567[index];
                float brittleDuration = BrittleDurations[index];
                uint previousStatus = status;
                durations = math.max(float4.zero, durations - new float4(DeltaTime));
                legacyDurations = math.max(float4.zero, legacyDurations - new float4(DeltaTime));
                brittleDuration = math.max(0f, brittleDuration - DeltaTime);

                status = durations.x > 0f ? status : status & ~CombatStatusBits.Bleeding;
                status = durations.y > 0f ? status : status & ~CombatStatusBits.Crushed;
                status = durations.z > 0f ? status : status & ~CombatStatusBits.Irradiated;
                status = durations.w > 0f ? status : status & ~CombatStatusBits.Hypoxia;
                status = legacyDurations.x > 0f ? status : status & ~CombatStatusBits.Poisoned;
                status = legacyDurations.y > 0f ? status : status & ~CombatStatusBits.Burning;
                status = legacyDurations.z > 0f ? status : status & ~CombatStatusBits.Stunned;
                status = brittleDuration > 0f ? status : status & ~CombatStatusBits.Brittle;

                float previousHealth = Health[index];
                float damage = 0f;
                if ((previousStatus & CombatStatusBits.Bleeding) != 0u)
                    damage += BleedingDamagePerSlowTick;
                if ((previousStatus & CombatStatusBits.Crushed) != 0u)
                    damage += CrushedDamagePerSlowTick;
                if ((previousStatus & CombatStatusBits.Irradiated) != 0u)
                    damage += IrradiatedDamagePerSlowTick;
                if ((previousStatus & CombatStatusBits.Hypoxia) != 0u)
                    damage += HypoxiaDamagePerSlowTick;
                if ((previousStatus & CombatStatusBits.Poisoned) != 0u)
                    damage += PoisonDamagePerSlowTick;
                if ((previousStatus & CombatStatusBits.Burning) != 0u)
                    damage += BurningDamagePerSlowTick;

                float nextHealth = math.max(0f, previousHealth - damage);
                Health[index] = nextHealth;
                StatusMasks[index] = status;
                StatusDurations0123[index] = durations;
                LegacyStatusDurations4567[index] = legacyDurations;
                BrittleDurations[index] = brittleDuration;

                if (damage <= 0f && status == previousStatus)
                    return;

                float maxHealth = math.max(0.0001f, MaxHealth[index]);
                ushort flags = status == previousStatus
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
                    SurfaceNormal = float3.zero,
                    Depth = 0f
                };
                ResultActiveBySlot[index] = 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveCombatDirection(float3 direction, byte kind, byte mathLod)
        {
            if (kind == (byte)CombatEntityKind.Player)
                return ResolveExactDirection(direction);

            return ResolveDominantAxisDirection(direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveExactDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            return lengthSq > 0.0001f && math.all(math.isfinite(direction))
                ? direction * math.rsqrt(lengthSq)
                : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveApproximateDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            return lengthSq > 0.0001f && math.all(math.isfinite(direction))
                ? direction * math.rsqrt(lengthSq)
                : float3.zero;
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
