using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public static class BallisticWeaponHashes
    {
        public const uint FloraSpike = 0x464C4F52u; // FLOR
        public const uint MockNeedle = 0x424D4F43u; // BMOC
    }

    public static class BallisticTrajectoryFlags
    {
        public const uint None = 0u;
        public const uint HostileFlora = 1u << 0;
        public const uint LegacyFacade = 1u << 1;
        public const uint Mock = 1u << 2;
    }

    public static class AABBPrimitiveFlags
    {
        public const uint Active = 1u << 31;
        public const uint Root = 1u << 0;
        public const uint Limb = 1u << 1;
        public const uint Head = 1u << 2;
        public const uint Mock = 1u << 3;
    }

    public static class BallisticHitFlags
    {
        public const uint None = 0u;
        public const uint Hit = 1u << 0;
        public const uint Ricochet = 1u << 1;
        public const uint LethalityExpired = 1u << 2;
        public const uint NanGuard = 1u << 3;
        public const uint Mock = 1u << 4;
        public const uint SignalDropped = 1u << 5;
    }

    internal static class BallisticsVaultBufferIds
    {
        public const BufferID TrajectoriesA = BufferID.BallisticsRuntime_TrajectoriesA;
        public const BufferID TrajectoriesB = BufferID.BallisticsRuntime_TrajectoriesB;
        public const BufferID AabbPrimitives = BufferID.BallisticsRuntime_AabbPrimitives;
        public const BufferID HitResults = BufferID.BallisticsRuntime_HitResults;
        public const BufferID PenetrationLut = BufferID.BallisticsRuntime_PenetrationLut;
        public const BufferID TelemetryRing = BufferID.BallisticsRuntime_TelemetryRing;
        public const BufferID Counters = BufferID.BallisticsRuntime_Counters;
        public const BufferID Tuning = BufferID.BallisticsRuntime_Tuning;
        public const BufferID ImpactVfx = BufferID.BallisticsRuntime_ImpactVfx;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BallisticTrajectoryDTO
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public float3 Direction;
        [FieldOffset(36)] public float Velocity;
        [FieldOffset(40)] public float Mass;
        [FieldOffset(44)] public uint WeaponHash;
        [FieldOffset(48)] public uint SourceEntityID;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct AABBPrimitiveDTO
    {
        [FieldOffset(0)] public double3 CenterAUP;
        [FieldOffset(24)] public float3 HalfExtents;
        [FieldOffset(36)] public uint TargetEntityID;
        [FieldOffset(40)] public quaternion Rotation;
        [FieldOffset(56)] public uint MaterialHash;
        [FieldOffset(60)] public uint PrimitiveHash;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public float DamageMultiplier;
        [FieldOffset(72)] public float ArmorScalar;
        [FieldOffset(76)] public uint _pad0;
        [FieldOffset(80)] public ulong _pad1;
        [FieldOffset(88)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct BallisticHitResultDTO
    {
        [FieldOffset(0)] public double3 HitAUP;
        [FieldOffset(24)] public float3 LocalHitPoint;
        [FieldOffset(36)] public float3 Normal;
        [FieldOffset(48)] public float3 ImpactDirection;
        [FieldOffset(60)] public float Damage;
        [FieldOffset(64)] public float RemainingVelocity;
        [FieldOffset(68)] public float Distance;
        [FieldOffset(72)] public uint TargetEntityID;
        [FieldOffset(76)] public uint SourceEntityID;
        [FieldOffset(80)] public uint WeaponHash;
        [FieldOffset(84)] public uint MaterialHash;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public uint Frame;
        [FieldOffset(96)] public uint RicochetCount;
        [FieldOffset(100)] public uint PrimitiveHash;
        [FieldOffset(104)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct BallisticImpactVfxDTO
    {
        [FieldOffset(0)] public float4x4 Matrix;
        [FieldOffset(64)] public uint MaterialHash;
        [FieldOffset(68)] public uint TargetEntityID;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BallisticsTuningDTO
    {
        [FieldOffset(0)] public float DragCoefficient;
        [FieldOffset(4)] public float LethalityThreshold;
        [FieldOffset(8)] public float RicochetFriction;
        [FieldOffset(12)] public float RicochetIncidenceThreshold;
        [FieldOffset(16)] public float DamageEnergyScale;
        [FieldOffset(20)] public float MaxRangeMeters;
        [FieldOffset(24)] public float FloraBaseVelocity;
        [FieldOffset(28)] public float FloraSpikeMassKg;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float LimbAdmissionFloor;
        [FieldOffset(40)] public float MockGridSpacingMeters;
        [FieldOffset(44)] public uint Revision;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BallisticsTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint TrajectoriesProcessed;
        [FieldOffset(8)] public uint HitCount;
        [FieldOffset(12)] public uint RicochetCount;
        [FieldOffset(16)] public uint NanGuardCount;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float SolveMicroseconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint PrimitiveCount;
        [FieldOffset(36)] public uint SignalCount;
        [FieldOffset(40)] public uint RejectedCount;
        [FieldOffset(44)] public uint ActiveTrajectoryBufferId;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BallisticsCountersDTO
    {
        [FieldOffset(0)] public uint TrajectoriesProcessed;
        [FieldOffset(4)] public uint HitCount;
        [FieldOffset(8)] public uint RicochetCount;
        [FieldOffset(12)] public uint NanGuardCount;
        [FieldOffset(16)] public uint SignalCount;
        [FieldOffset(20)] public uint RejectedCount;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float SolveMicroseconds;
        [FieldOffset(40)] public uint ActiveTrajectoryBufferId;
        [FieldOffset(44)] public uint PrimitiveCount;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    public static class BallisticsRuntime
    {
        public const int MaxTrajectories = 4096;
        public const int MaxAabbPrimitives = 8192;
        public const int MaxHitResults = MaxTrajectories;
        public const int MaxImpactVfx = MaxTrajectories;
        public const int TelemetryRingLength = 300;
        public const int PenetrationLutLength = 64;
#if UNITY_EDITOR
        public const int CsvImportByteCapacity = 16384;
#endif
        public const float FloraSpikeMassKg = 0.018f;

        private const float Epsilon = 0.0001f;
        private const int MaxDamageSignalsPerSolve = 128;
        private const int LowQualityDamageSignalsPerSolve = 16;
        internal const int MaxRicochetsPerTrajectory = 3;

        // Damage-chain bounds. Kinetic energy is already monotonically non-increasing in distance because
        // the drag factor is saturated to [0,1], so the falloff direction is safe; the unbounded axis was
        // the per-primitive multiplier chain, which RegisterAabbPrimitiveFromRuntime only floored at zero.
        // Widest value any live authoring route produces is 1.25 (ResolveArmorScalar/Brittle) against a
        // DamageMultiplier of 1, so these ceilings clear real data by more than 3x and only catch garbage.
        internal const float MaxPenetrationScalar = 4f;
        internal const float MaxPrimitiveDamageMultiplier = 8f;
        internal const float MinPrimitiveArmorScalar = 0.0001f;
        internal const float MaxPrimitiveArmorScalar = 4f;
        private const uint FaultTelemetryFlag = 1u << 0;
        private const uint OverBudgetTelemetryFlag = 1u << 1;
        private const uint DumpedTelemetryFlag = 1u << 2;
        private const double TelemetryDumpThresholdMicroseconds = 500.0d;
        private const SystemID OwnerSystem = SystemID.Physics;
        private const ulong MutationGuardBit = 1UL << 42;
        private const uint SourceHash = 0x53483132u; // SH12
#if UNITY_EDITOR
        private const uint CsvWeaponHeaderHash = 0x6F332041u;
        private const uint CsvWeaponMicroHash = 0x8404523Bu;
        private const uint CsvWeaponNeedleHash = 0x88B14DC2u;
        private const uint CsvWeaponSpikeHash = 0xBBD77415u;
        private const uint CsvWeaponSlugHash = 0x5DD5D0DEu;
        private const uint CsvWeaponHarpoonHash = 0x847D2648u;
        private const uint CsvWeaponRailHash = 0x8BA3B8F9u;
        private const uint CsvWeaponSupercavHash = 0xE2735D5Eu;
        private const uint CsvWeaponAbyssalHash = 0x588F795Au;
        private const uint CsvMaterialFleshHash = 0x02FC484Du;
        private const uint CsvMaterialChitinHash = 0x12B1E2D0u;
        private const uint CsvMaterialPlasteelHash = 0x29E22C37u;
        private const uint CsvMaterialGlassHash = 0xF203A92Bu;
        private const uint CsvMaterialOrganicHeavyHash = 0xC861E916u;
        private const uint CsvMaterialBrittleHash = 0x2BEE83CFu;
        private const uint CsvMaterialShieldedHash = 0x9700E1B1u;
        private const uint CsvMaterialReservedHash = 0xD4B5CAFDu;
#endif

        private static readonly ProfilerMarker _frameMarker = new ProfilerMarker("H8.Ballistics.FrameTick");
        private static readonly ProfilerMarker _queueMarker = new ProfilerMarker("H8.Ballistics.QueueTrajectory");

        private struct VaultLane<T> where T : struct
        {
            public VaultGenerationHandle<T> Handle;
            public uint ExpectedBufferID;
            public int Length;
        }

        private static IDataVault _vault;
        private static VaultLane<BallisticTrajectoryDTO> _trajectoryAHandle;
        private static VaultLane<BallisticTrajectoryDTO> _trajectoryBHandle;
        private static VaultLane<AABBPrimitiveDTO> _primitiveHandle;
        private static VaultLane<BallisticHitResultDTO> _hitHandle;
        private static VaultLane<float> _penetrationLutHandle;
        private static VaultLane<BallisticsTelemetryEntry> _telemetryHandle;
        private static VaultLane<BallisticsCountersDTO> _counterHandle;
        private static VaultLane<BallisticsTuningDTO> _tuningHandle;
        private static VaultLane<BallisticImpactVfxDTO> _impactVfxHandle;
        private static JobHandle _activeHandle;
        private static IDataVault _activeJobMutationGuardVault;
        private static int _pendingTrajectoryCount;
        private static int _primitiveCount;
        private static int _writeTrajectoryBufferIndex;
        private static int _activeReadBufferIndex;
        private static int _activeReadCount;
        private static int _activeTelemetryIndex;
        private static uint _telemetryCursor;
        private static uint _simulationFrame;
        private static long _activeScheduleTicks;
        private static bool _initialized;
        private static bool _jobScheduled;
        private static bool _telemetryDumped;
        private static BallisticsTelemetryEntry _lastTelemetry;

        /// <summary>True when the write trajectory buffer contains shots waiting for the next solve.</summary>
        public static bool HasPendingTrajectories => _pendingTrajectoryCount > 0;

        /// <summary>Finalizes a finished solver before target AABB refresh. Never blocks.</summary>
        public static bool PrepareFrameForTargetRefresh()
        {
            if (!EnsureInitialized())
                return false;

            TryFinalizeScheduledNoWait();
            return !_jobScheduled && _pendingTrajectoryCount > 0;
        }

        /// <summary>Boots the Vault-backed ballistics buffers. Cold path only.</summary>
        public static bool EnsureInitialized(IDataVault explicitVault = null)
        {
            if (explicitVault != null)
                CacheDataVault(explicitVault);

            IDataVault resolvedVault = _vault;
            if (resolvedVault == null)
                return false;

            if (_initialized && ReferenceEquals(_vault, resolvedVault))
                return true;

            bool vaultChanged = _initialized && _vault != null && !ReferenceEquals(_vault, resolvedVault);
            if (vaultChanged)
            {
                CompleteScheduledForTeardown();
                ResetTransientState();
            }
            else if (!_initialized)
            {
                ResetTransientState();
            }

            _vault = resolvedVault;
            _trajectoryAHandle = AcquireVaultLane<BallisticTrajectoryDTO>(
                BallisticsVaultBufferIds.TrajectoriesA,
                MaxTrajectories,
                NativeArrayOptions.UninitializedMemory);
            _trajectoryBHandle = AcquireVaultLane<BallisticTrajectoryDTO>(
                BallisticsVaultBufferIds.TrajectoriesB,
                MaxTrajectories,
                NativeArrayOptions.UninitializedMemory);
            _primitiveHandle = AcquireVaultLane<AABBPrimitiveDTO>(
                BallisticsVaultBufferIds.AabbPrimitives,
                MaxAabbPrimitives,
                NativeArrayOptions.UninitializedMemory);
            _hitHandle = AcquireVaultLane<BallisticHitResultDTO>(
                BallisticsVaultBufferIds.HitResults,
                MaxHitResults,
                NativeArrayOptions.UninitializedMemory);
            _penetrationLutHandle = AcquireVaultLane<float>(
                BallisticsVaultBufferIds.PenetrationLut,
                PenetrationLutLength,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = AcquireVaultLane<BallisticsTelemetryEntry>(
                BallisticsVaultBufferIds.TelemetryRing,
                TelemetryRingLength,
                NativeArrayOptions.ClearMemory);
            _counterHandle = AcquireVaultLane<BallisticsCountersDTO>(
                BallisticsVaultBufferIds.Counters,
                1,
                NativeArrayOptions.ClearMemory);
            _tuningHandle = AcquireVaultLane<BallisticsTuningDTO>(
                BallisticsVaultBufferIds.Tuning,
                1,
                NativeArrayOptions.ClearMemory);
            _impactVfxHandle = AcquireVaultLane<BallisticImpactVfxDTO>(
                BallisticsVaultBufferIds.ImpactVfx,
                MaxImpactVfx,
                NativeArrayOptions.UninitializedMemory);
            if (!AreVaultLanesBound())
                return false;

            SeedDefaultTuning();
            SeedDefaultPenetrationLut();
            _initialized = true;
            return true;
        }

        internal static void CacheDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            if (_vault != null)
            {
                CompleteScheduledForTeardown();
                ReleaseVaultLanes(_vault);
                _initialized = false;
                ResetTransientState();
            }

            _vault = vault;
        }

        /// <summary>Queues one mathematical bullet trajectory from a camera-relative runtime transform.</summary>
        public static bool QueueTrajectoryFromRuntime(
            Vector3 origin,
            Vector3 direction,
            float velocity,
            float mass,
            uint weaponHash,
            uint sourceEntityId,
            uint flags = BallisticTrajectoryFlags.None)
        {
            using (_queueMarker.Auto())
            {
                return QueueResolvedTrajectoryNoMarker(
                    origin,
                    (float3)(direction),
                    velocity,
                    mass,
                    weaponHash,
                    sourceEntityId,
                    flags);
            }
        }

        /// <summary>Returns the deterministic frame id that will be assigned to the next queued ballistic solve.</summary>
        public static uint ResolveNextSimulationFrameCounter()
        {
            return unchecked(_simulationFrame + 1u);
        }

        /// <summary>Queues one trajectory from velocity, preserving legacy projectile facade compatibility.</summary>
        public static bool QueueTrajectoryFromVelocity(
            Vector3 origin,
            Vector3 velocity,
            float mass,
            uint weaponHash,
            uint sourceEntityId,
            uint flags = BallisticTrajectoryFlags.None)
        {
            if (!IsFinite(velocity))
                return false;

            float3 velocity3 = (float3)(velocity);
            float speedSq = math.lengthsq(velocity3);
            if (!math.isfinite(speedSq) || speedSq <= Epsilon)
                return false;

            float invSpeed = math.rsqrt(math.max(speedSq, Epsilon));
            float speed = speedSq * invSpeed;
            using (_queueMarker.Auto())
            {
                return QueueResolvedTrajectoryNoMarker(
                    origin,
                    velocity3 * invSpeed,
                    speed,
                    mass,
                    weaponHash,
                    sourceEntityId,
                    flags);
            }
        }

        private static bool QueueResolvedTrajectoryNoMarker(
            Vector3 origin,
            float3 direction,
            float velocity,
            float mass,
            uint weaponHash,
            uint sourceEntityId,
            uint flags)
        {
            if (!EnsureInitialized())
                return false;

            if (!IsFinite(origin) || !math.all(math.isfinite(direction)))
                return false;

            float3 resolvedDirection = NormalizeOrDefault(direction, new float3(0f, 0f, 1f));
            float safeVelocity = math.max(0f, SelectFinite(velocity, 0f));
            float safeMass = math.max(0.0001f, SelectFinite(mass, 0.0001f));
            if (safeVelocity <= Epsilon)
                return false;

            if (!TryResolveAupDoubleFromRuntimeOrigin(origin, out double3 originAup))
                return false;

            NativeArray<BallisticTrajectoryDTO> writeTrajectories = ResolveWriteTrajectories();
            if (!writeTrajectories.IsCreated || _pendingTrajectoryCount >= math.min(writeTrajectories.Length, MaxTrajectories))
                return false;

            int index = _pendingTrajectoryCount++;
            BallisticTrajectoryDTO trajectory = default;
            trajectory.OriginAUP = originAup;
            trajectory.Direction = resolvedDirection;
            trajectory.Velocity = safeVelocity;
            trajectory.Mass = safeMass;
            trajectory.WeaponHash = weaponHash;
            trajectory.SourceEntityID = sourceEntityId;
            trajectory.Flags = flags;
            unsafe
            {
                byte* trajectoryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(writeTrajectories);
                if (trajectoryPtr == null)
                    return false;

                ref BallisticTrajectoryDTO trajectorySlot = ref UnsafeUtility.AsRef<BallisticTrajectoryDTO>(
                    trajectoryPtr + (index * UnsafeUtility.SizeOf<BallisticTrajectoryDTO>()));
                trajectorySlot = trajectory;
            }

            return true;
        }

        /// <summary>Registers or updates a target AABB primitive in the DataVault. Cold target-registration path.</summary>
        public static bool RegisterAabbPrimitiveFromRuntime(
            uint targetEntityId,
            Vector3 center,
            Vector3 halfExtents,
            Quaternion rotation,
            uint materialHash,
            uint primitiveHash,
            uint flags,
            float damageMultiplier,
            float armorScalar)
        {
            if (targetEntityId == 0u || !EnsureInitialized())
                return false;

            if (_jobScheduled || !IsFinite(center) || !IsFinite(halfExtents))
                return false;

            if (!TryAcquireBallisticsMutationGuard(out IDataVault guardVault))
                return false;

            try
            {
                NativeArray<AABBPrimitiveDTO> primitives = OpenVaultLane(in _primitiveHandle);
                if (!primitives.IsCreated)
                    return false;

                int slot = -1;
                int inactiveSlot = -1;
                int capacity = math.min(primitives.Length, MaxAabbPrimitives);
                int count = math.min(math.max(0, _primitiveCount), capacity);
                for (int i = 0; i < count; i++)
                {
                    AABBPrimitiveDTO existing = primitives[i];
                    if ((existing.Flags & AABBPrimitiveFlags.Active) == 0u)
                    {
                        if (inactiveSlot < 0)
                            inactiveSlot = i;

                        continue;
                    }

                    if (existing.TargetEntityID == targetEntityId && existing.PrimitiveHash == primitiveHash)
                    {
                        slot = i;
                        break;
                    }
                }

                if (slot < 0)
                {
                    if (inactiveSlot >= 0)
                    {
                        slot = inactiveSlot;
                    }
                    else
                    {
                        int nextSlot = math.max(0, _primitiveCount);
                        if (nextSlot >= capacity)
                            return false;

                        slot = nextSlot;
                        _primitiveCount = nextSlot + 1;
                    }
                }

                if (!TryResolveAupDoubleFromRuntimeOrigin(center, out double3 centerAup))
                    return false;

                AABBPrimitiveDTO primitive = default;
                primitive.CenterAUP = centerAup;
                primitive.HalfExtents = math.max(math.abs((float3)(halfExtents)), new float3(0.025f));
                primitive.TargetEntityID = targetEntityId;
                primitive.Rotation = NormalizeOrIdentity(new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
                primitive.MaterialHash = materialHash;
                primitive.PrimitiveHash = primitiveHash;
                primitive.Flags = flags | AABBPrimitiveFlags.Active;
                primitive.DamageMultiplier = math.clamp(SelectFinite(damageMultiplier, 1f), 0f, MaxPrimitiveDamageMultiplier);
                primitive.ArmorScalar = math.clamp(SelectFinite(armorScalar, 1f), MinPrimitiveArmorScalar, MaxPrimitiveArmorScalar);
                primitives[slot] = primitive;
                return true;
            }
            finally
            {
                ReleaseBallisticsMutationGuard(guardVault);
            }
        }

        /// <summary>Registers a conservative root-body AABB for an existing combat target.</summary>
        public static bool RegisterCombatTargetAabb(int targetId, Transform receiverTransform, float height, CombatArmorClass armorClass)
        {
            if (targetId == 0 || receiverTransform == null)
                return false;

            float safeHeight = math.max(0.25f, SelectFinite(height, 1f));
            float radius = math.clamp(safeHeight * 0.22f, 0.18f, 0.75f);
            Vector3 center = receiverTransform.position + (receiverTransform.up * (safeHeight * 0.5f));
            Vector3 halfExtents = new Vector3(radius, safeHeight * 0.5f, radius);
            uint target = unchecked((uint)targetId);
            uint primitiveHash = HashPrimitive(target, 0u);
            uint materialHash = (uint)armorClass & 7u;
            float armorScalar = ResolveArmorScalar(armorClass);
            return RegisterAabbPrimitiveFromRuntime(
                target,
                center,
                halfExtents,
                receiverTransform.rotation,
                materialHash,
                primitiveHash,
                AABBPrimitiveFlags.Root,
                1f,
                armorScalar);
        }

        /// <summary>Marks all primitives for a combat target inactive. Cold unregister path.</summary>
        public static bool TombstonePrimitivesForTarget(uint targetEntityId)
        {
            if (targetEntityId == 0u || !EnsureInitialized() || _jobScheduled)
                return false;

            if (!TryAcquireBallisticsMutationGuard(out IDataVault guardVault))
                return false;

            try
            {
                NativeArray<AABBPrimitiveDTO> primitives = OpenVaultLane(in _primitiveHandle);
                if (!primitives.IsCreated)
                    return false;

                bool mutated = false;
                int count = math.min(math.max(0, _primitiveCount), primitives.Length);
                for (int i = 0; i < count; i++)
                {
                    AABBPrimitiveDTO primitive = primitives[i];
                    if (primitive.TargetEntityID != targetEntityId)
                        continue;

                    primitive.Flags = 0u;
                    primitives[i] = primitive;
                    mutated = true;
                }

                return mutated;
            }
            finally
            {
                ReleaseBallisticsMutationGuard(guardVault);
            }
        }

        /// <summary>Schedules the ballistic solver from the combat router tick without blocking the main thread.</summary>
        public static void FrameTick(float simulationDeltaSeconds)
        {
            using (_frameMarker.Auto())
            {
                if (!EnsureInitialized())
                    return;

                TryFinalizeScheduledNoWait();
                if (_jobScheduled || _pendingTrajectoryCount <= 0 || _primitiveCount <= 0)
                    return;

                if (!TryAcquireBallisticsMutationGuard(out IDataVault guardVault))
                    return;

                bool guardTransferred = false;
                try
                {
                float quality = ResolveGlobalQualityWeight();
                BallisticsTuningDTO tuning = ResolveTuning(quality);
                NativeArray<BallisticTrajectoryDTO> solverTrajectories = ResolveWriteTrajectories();
                NativeArray<AABBPrimitiveDTO> primitives = OpenVaultLane(in _primitiveHandle);
                NativeArray<BallisticHitResultDTO> hitResults = OpenVaultLane(in _hitHandle);
                NativeArray<float> penetrationLut = OpenVaultLane(in _penetrationLutHandle);
                NativeArray<BallisticsTelemetryEntry> telemetry = OpenVaultLane(in _telemetryHandle);
                NativeArray<BallisticsCountersDTO> counters = OpenVaultLane(in _counterHandle);
                NativeArray<BallisticImpactVfxDTO> impactVfx = OpenVaultLane(in _impactVfxHandle);
                if (!solverTrajectories.IsCreated ||
                    !primitives.IsCreated ||
                    !hitResults.IsCreated ||
                    !penetrationLut.IsCreated ||
                    !telemetry.IsCreated ||
                    !counters.IsCreated ||
                    !impactVfx.IsCreated)
                    return;

                if (solverTrajectories.Length <= 0 ||
                    primitives.Length <= 0 ||
                    hitResults.Length <= 0 ||
                    penetrationLut.Length < PenetrationLutLength ||
                    telemetry.Length <= 0 ||
                    counters.Length <= 0 ||
                    impactVfx.Length <= 0)
                    return;

                int primitiveCount = math.min(math.max(0, _primitiveCount), primitives.Length);
                if (primitiveCount <= 0)
                    return;

                int trajectoryCount = math.min(_pendingTrajectoryCount, math.min(solverTrajectories.Length, hitResults.Length));
                if (trajectoryCount <= 0)
                    return;

                if (!ReferenceEquals(_vault, guardVault) || guardVault.IsCompactionFenceActive)
                    return;

                _activeReadCount = trajectoryCount;
                _activeReadBufferIndex = _writeTrajectoryBufferIndex;
                _pendingTrajectoryCount = 0;
                _writeTrajectoryBufferIndex ^= 1;
                int telemetryLength = math.min(telemetry.Length, TelemetryRingLength);
                _activeTelemetryIndex = (int)(_telemetryCursor % (uint)telemetryLength);
                _telemetryCursor++;
                uint frame = ++_simulationFrame;
                uint activeBufferId = (uint)ResolveActiveReadBufferId();
                if (!TryResolveCurrentRuntimeOriginDouble3(out double3 presentationOriginAup))
                    presentationOriginAup = double3.zero;

                int signalEmitBudget = ComputeDamageSignalBudget(quality);

                ClearCounter(counters, frame, quality, activeBufferId, primitiveCount);
                BallisticIntersectionJob intersectionJob = new BallisticIntersectionJob
                {
                    Trajectories = solverTrajectories,
                    Primitives = primitives,
                    PenetrationLut = penetrationLut,
                    HitResults = hitResults,
                    Tuning = tuning,
                    TrajectoryCount = trajectoryCount,
                    PrimitiveCount = primitiveCount,
                    Frame = frame,
                    GlobalQualityWeight = quality
                };

                JobHandle handle = intersectionJob.Schedule(trajectoryCount, 32);
                EmitBallisticDamageSignalsJob emitJob = new EmitBallisticDamageSignalsJob
                {
                    HitResults = hitResults,
                    DamageWriter = SignalBus<CombatDamageSignal>.ParallelWriter,
                    DamageWriterBudget = SignalBus<CombatDamageSignal>.ParallelWriterBudget,
                    HitCount = trajectoryCount,
                    SignalEmitBudget = signalEmitBudget,
                    Frame = frame
                };
                handle = emitJob.Schedule(handle);
                StageImpactVFXJob vfxJob = new StageImpactVFXJob
                {
                    HitResults = hitResults,
                    ImpactVfx = impactVfx,
                    HitCount = trajectoryCount,
                    Frame = frame,
                    GlobalQualityWeight = quality,
                    PresentationOriginAUP = presentationOriginAup
                };
                handle = vfxJob.Schedule(trajectoryCount, 32, handle);
                BallisticsTelemetryJob telemetryJob = new BallisticsTelemetryJob
                {
                    HitResults = hitResults,
                    TelemetryRing = telemetry,
                    Counters = counters,
                    TrajectoryCount = trajectoryCount,
                    PrimitiveCount = primitiveCount,
                    TelemetryIndex = _activeTelemetryIndex,
                    Frame = frame,
                    GlobalQualityWeight = quality,
                    ActiveTrajectoryBufferId = activeBufferId
                };
                handle = telemetryJob.Schedule(handle);

                _activeScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                _activeHandle = handle;
                _activeJobMutationGuardVault = guardVault;
                guardTransferred = true;
                _jobScheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _activeHandle);
                JobHandle.ScheduleBatchedJobs();
                }
                finally
                {
                    if (!guardTransferred)
                        ReleaseBallisticsMutationGuard(guardVault);
                }
            }
        }

        /// <summary>Non-blocking late-frame finalization; only completes already-finished work.</summary>
        public static void LateFrameTick()
        {
            TryFinalizeScheduledNoWait();
        }

        /// <summary>Completes outstanding work only during teardown/simulation barrier ownership.</summary>
        public static void Shutdown()
        {
            CompleteScheduledForTeardown();
            ReleaseVaultLanes(_vault);
            _initialized = false;
            _vault = null;
            ResetTransientState();
        }

        /// <summary>Returns the most recent telemetry entry mirrored from the Vault ring.</summary>
        public static bool TryGetLastTelemetry(out BallisticsTelemetryEntry entry)
        {
            entry = _lastTelemetry;
            return entry.Frame != 0u;
        }

        /// <summary>Reads the current Vault tuning DTO.</summary>
        public static bool TryGetTuning(out BallisticsTuningDTO tuning)
        {
            tuning = default;
            if (!CanReadVaultSnapshots() || _jobScheduled)
                return false;

            NativeArray<BallisticsTuningDTO> buffer = OpenVaultLane(in _tuningHandle);
            if (!buffer.IsCreated || buffer.Length <= 0)
                return false;

            tuning = buffer[0];
            return true;
        }

        /// <summary>Writes the Vault-backed tuning DTO from editor/cold controls.</summary>
        public static bool WriteTuning(in BallisticsTuningDTO tuning)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            if (!TryAcquireBallisticsMutationGuard(out IDataVault guardVault))
                return false;

            try
            {
                NativeArray<BallisticsTuningDTO> buffer = OpenVaultLane(in _tuningHandle);
                if (!buffer.IsCreated || buffer.Length <= 0)
                    return false;

                BallisticsTuningDTO sanitized = SanitizeTuning(tuning);
                sanitized.Revision = tuning.Revision + 1u;
                buffer[0] = sanitized;
                return true;
            }
            finally
            {
                ReleaseBallisticsMutationGuard(guardVault);
            }
        }

        /// <summary>Cold Burst mock used to profile the solver without player or AI dependencies.</summary>
        public static bool GenerateMockBallistics(int trajectoryCount = 1000, int primitiveCount = 128)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            if (!TryAcquireBallisticsMutationGuard(out IDataVault guardVault))
                return false;

            try
            {
                NativeArray<BallisticTrajectoryDTO> trajectories = ResolveWriteTrajectories();
                NativeArray<AABBPrimitiveDTO> primitives = OpenVaultLane(in _primitiveHandle);
                if (!trajectories.IsCreated ||
                    trajectories.Length <= 0 ||
                    !primitives.IsCreated ||
                    primitives.Length <= 0)
                    return false;

                int safeTrajectoryCount = math.clamp(trajectoryCount, 1, math.min(trajectories.Length, MaxTrajectories));
                int safePrimitiveCount = math.clamp(primitiveCount, 1, math.min(primitives.Length, MaxAabbPrimitives));
                if (safeTrajectoryCount <= 0 || safePrimitiveCount <= 0)
                    return false;

                if (_vault == null || _vault.IsCompactionFenceActive)
                    return false;

                BallisticsTuningDTO tuning = ResolveTuning(ResolveGlobalQualityWeight());
                GenerateMockBallisticsJob job = new GenerateMockBallisticsJob
                {
                    Trajectories = trajectories,
                    Primitives = primitives,
                    TrajectoryCount = safeTrajectoryCount,
                    PrimitiveCount = safePrimitiveCount,
                    GridSpacingMeters = math.max(0.5f, tuning.MockGridSpacingMeters),
                    MockOriginAUP = TryResolveCurrentRuntimeOriginDouble3(out double3 mockOriginAup)
                        ? mockOriginAup
                        : double3.zero,
                    Frame = ++_simulationFrame
                };

                JobHandle handle = job.Schedule(math.max(safeTrajectoryCount, safePrimitiveCount), 64);
                H8Memory.RegisterActiveJob(OwnerSystem, handle);
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD SYNC JOB: editor/manual mock injection before profiling solver.
                _pendingTrajectoryCount = safeTrajectoryCount;
                _primitiveCount = safePrimitiveCount;
                _activeReadCount = 0;
                return true;
            }
            finally
            {
                ReleaseBallisticsMutationGuard(guardVault);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only CSV loader for the 8x8 weapon/material penetration matrix.</summary>
        public static bool TryLoadPenetrationCsv(string csvPath)
        {
            if (!EnsureInitialized() || _jobScheduled || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            bool mutationGuarded = false;
            IDataVault guardVault = null;
            try
            {
                FileInfo info = new FileInfo(csvPath);
                if (!info.Exists || info.Length <= 0L || info.Length > CsvImportByteCapacity)
                    return false;

                int expectedBytes = (int)info.Length;
                Span<byte> csvScratch = stackalloc byte[CsvImportByteCapacity];
                int bytesRead = 0;
                using (FileStream stream = File.Open(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    while (bytesRead < expectedBytes)
                    {
                        int chunk = stream.Read(csvScratch.Slice(bytesRead, expectedBytes - bytesRead));
                        if (chunk <= 0)
                            break;

                        bytesRead += chunk;
                    }
                }

                if (bytesRead != expectedBytes)
                    return false;

                mutationGuarded = TryAcquireBallisticsMutationGuard(out guardVault);
                if (!mutationGuarded)
                    return false;

                NativeArray<float> lut = OpenVaultLane(in _penetrationLutHandle);
                if (!lut.IsCreated)
                    return false;

                if (_vault == null || _vault.IsCompactionFenceActive)
                    return false;

                return ApplyPenetrationCsvBytes(csvScratch.Slice(0, bytesRead), lut);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                if (mutationGuarded)
                    ReleaseBallisticsMutationGuard(guardVault);
            }
        }

        /// <summary>Allocation-free span parser for the 8x8 penetration LUT body.</summary>
        public static bool ApplyPenetrationCsvBytes(ReadOnlySpan<byte> bytes, NativeArray<float> lut)
        {
            if (!lut.IsCreated || lut.Length < PenetrationLutLength || bytes.Length <= 0)
                return false;

            Span<float> parsedLut = stackalloc float[PenetrationLutLength];
            for (int i = 0; i < PenetrationLutLength; i++)
                parsedLut[i] = lut[i];

            int sequentialRow = 0;
            int rowsParsed = 0;
            int currentRow = -1;
            int col = 0;
            int tokenStart = 0;
            int header0 = 0;
            int header1 = 1;
            int header2 = 2;
            int header3 = 3;
            int header4 = 4;
            int header5 = 5;
            int header6 = 6;
            int header7 = 7;
            bool firstTokenInLine = true;
            bool parsedAny = false;
            bool headerLine = false;
            bool hasHeaderMap = false;
            bool invalidHeader = false;
            bool invalidData = false;
            int mappedHeaderColumns = 0;

            for (int i = 0; i <= bytes.Length; i++)
            {
                byte c = i < bytes.Length ? bytes[i] : (byte)'\n';
                bool delimiter = c == (byte)',' || c == (byte)'\n' || c == (byte)'\r';
                if (!delimiter)
                    continue;

                ReadOnlySpan<byte> token = TrimAscii(bytes.Slice(tokenStart, i - tokenStart));
                tokenStart = i + 1;
                if (c == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
                {
                    tokenStart = i + 2;
                    i++;
                }

                if (token.Length > 0)
                {
                    uint tokenHash = HashFnv1aLower(token);
                    if (firstTokenInLine && !StartsNumeric(token))
                    {
                        int namedRow = ResolveWeaponCsvIndex(tokenHash);
                        if (namedRow >= 0)
                        {
                            currentRow = namedRow;
                            headerLine = false;
                        }
                        else
                        {
                            headerLine = true;
                            currentRow = -1;
                        }

                        firstTokenInLine = false;
                        continue;
                    }

                    if (headerLine)
                    {
                        int materialIndex = ResolveMaterialCsvIndex(tokenHash);
                        if (materialIndex >= 0 && col < 8)
                        {
                            switch (col)
                            {
                                case 0: header0 = materialIndex; break;
                                case 1: header1 = materialIndex; break;
                                case 2: header2 = materialIndex; break;
                                case 3: header3 = materialIndex; break;
                                case 4: header4 = materialIndex; break;
                                case 5: header5 = materialIndex; break;
                                case 6: header6 = materialIndex; break;
                                default: header7 = materialIndex; break;
                            }

                            hasHeaderMap = true;
                            mappedHeaderColumns++;
                        }
                        else if (col < 8)
                        {
                            invalidHeader = true;
                        }

                        col++;
                    }
                    else if (TryParseFloat(token, out float value))
                    {
                        int targetRow = currentRow >= 0 ? currentRow : sequentialRow;
                        int targetCol = hasHeaderMap
                            ? ResolveHeaderMaterialIndex(col, header0, header1, header2, header3, header4, header5, header6, header7)
                            : col;
                        if (targetRow < 8 && targetCol < 8)
                        {
                            parsedLut[(targetRow * 8) + targetCol] = math.max(0f, value);
                            parsedAny = true;
                        }

                        col++;
                    }
                    else if (sequentialRow == 0 && !parsedAny)
                    {
                        headerLine = true;
                    }
                    else
                    {
                        invalidData = true;
                    }
                }

                if (c == (byte)'\n' || c == (byte)'\r')
                {
                    if (!headerLine && col > 0)
                    {
                        sequentialRow++;
                        rowsParsed++;
                        if (rowsParsed >= 8)
                            break;
                    }

                    col = 0;
                    currentRow = -1;
                    firstTokenInLine = true;
                    headerLine = false;
                }
                else
                {
                    firstTokenInLine = false;
                }
            }

            if (!parsedAny || rowsParsed < 8 || invalidHeader || invalidData || (hasHeaderMap && mappedHeaderColumns < 8))
                return false;

            for (int i = 0; i < PenetrationLutLength; i++)
                lut[i] = parsedLut[i];

            return true;
        }
#endif

        internal static bool TryGetDebugBuffers(
            out NativeArray<BallisticTrajectoryDTO>.ReadOnly trajectories,
            out int trajectoryCount,
            out NativeArray<AABBPrimitiveDTO>.ReadOnly primitives,
            out int primitiveCount,
            out NativeArray<BallisticHitResultDTO>.ReadOnly hits)
        {
            trajectories = default;
            primitives = default;
            hits = default;
            trajectoryCount = 0;
            primitiveCount = 0;
            if (!CanReadVaultSnapshots())
                return false;

            if (_jobScheduled)
                return false;

            NativeArray<BallisticTrajectoryDTO> mutableTrajectories = ResolveActiveOrWriteTrajectories();
            NativeArray<AABBPrimitiveDTO> mutablePrimitives = OpenVaultLane(in _primitiveHandle);
            NativeArray<BallisticHitResultDTO> mutableHits = OpenVaultLane(in _hitHandle);
            if (!mutableTrajectories.IsCreated || !mutablePrimitives.IsCreated || !mutableHits.IsCreated)
                return false;

            trajectories = mutableTrajectories.AsReadOnly();
            primitives = mutablePrimitives.AsReadOnly();
            hits = mutableHits.AsReadOnly();
            int rawTrajectoryCount = _activeReadCount > 0 ? _activeReadCount : _pendingTrajectoryCount;
            trajectoryCount = math.min(
                math.max(0, rawTrajectoryCount),
                math.min(mutableTrajectories.Length, mutableHits.Length));
            primitiveCount = math.min(math.max(0, _primitiveCount), mutablePrimitives.Length);
            return trajectories.Length > 0 && primitives.Length > 0 && hits.Length > 0;
        }

        public static bool TryGetImpactVfxStaging(
            out NativeArray<BallisticImpactVfxDTO>.ReadOnly impactVfx,
            out int stagingCount,
            out uint frame)
        {
            impactVfx = default;
            stagingCount = 0;
            frame = 0u;
            if (!CanReadVaultSnapshots())
                return false;

            if (_jobScheduled)
                return false;

            NativeArray<BallisticImpactVfxDTO> mutableImpactVfx = OpenVaultLane(in _impactVfxHandle);
            NativeArray<BallisticsCountersDTO> counters = OpenVaultLane(in _counterHandle);
            if (!mutableImpactVfx.IsCreated || !counters.IsCreated || counters.Length <= 0)
                return false;

            impactVfx = mutableImpactVfx.AsReadOnly();
            BallisticsCountersDTO counter = counters[0];
            frame = counter.Frame;
            uint clampedCount = math.min(counter.TrajectoriesProcessed, (uint)int.MaxValue);
            stagingCount = math.min((int)clampedCount, impactVfx.Length);
            return frame != 0u;
        }

        private static void TryFinalizeScheduledNoWait()
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _activeHandle))
                return;

            FinishScheduledCompletion();
        }

        private static bool CanReadVaultSnapshots()
        {
            return _initialized && _vault != null && AreVaultLanesBound();
        }

        private static bool TryAcquireBallisticsMutationGuard(out IDataVault guardVault)
        {
            guardVault = _vault;
            return guardVault != null && guardVault.TryAcquireMutationGuard(MutationGuardBit);
        }

        private static void ReleaseBallisticsMutationGuard(IDataVault guardVault)
        {
            guardVault?.ReleaseMutationGuard(MutationGuardBit);
        }

        private static void ReleaseActiveJobMutationGuard()
        {
            IDataVault guardVault = _activeJobMutationGuardVault;
            if (guardVault != null)
                guardVault.ReleaseMutationGuard(MutationGuardBit);
            _activeJobMutationGuardVault = null;
        }

        private static void CompleteScheduledForTeardown()
        {
            if (!_jobScheduled)
                return;

            if (!ForceCompleteActiveJobInPostSimulationWindow())
                return;

            FinishScheduledCompletion();
        }

        private static bool ForceCompleteActiveJobInPostSimulationWindow()
        {
            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }
        }

        private static void FinishScheduledCompletion()
        {
            _jobScheduled = false;
            double elapsedUs =
                (System.Diagnostics.Stopwatch.GetTimestamp() - _activeScheduleTicks) *
                1000000.0d /
                System.Diagnostics.Stopwatch.Frequency;
            try
            {
                RecordCompletedTelemetry(elapsedUs);
            }
            finally
            {
                ReleaseActiveJobMutationGuard();
            }
        }

        private static void RecordCompletedTelemetry(double elapsedUs)
        {
            NativeArray<BallisticsCountersDTO> counters = OpenVaultLane(in _counterHandle);
            NativeArray<BallisticsTelemetryEntry> telemetry = OpenVaultLane(in _telemetryHandle);
            if (!counters.IsCreated || counters.Length <= 0 || !telemetry.IsCreated || telemetry.Length <= 0)
                return;

            BallisticsCountersDTO counter = counters[0];
            int index = math.clamp(_activeTelemetryIndex, 0, telemetry.Length - 1);
            BallisticsTelemetryEntry entry = telemetry[index];
            entry.SolveMicroseconds = (float)math.max(0.0d, elapsedUs);
            if (elapsedUs > TelemetryDumpThresholdMicroseconds)
                entry.Flags |= OverBudgetTelemetryFlag;

            if (counter.NanGuardCount > 0u)
                entry.Flags |= FaultTelemetryFlag;

            if (_telemetryDumped)
                entry.Flags |= DumpedTelemetryFlag;

            telemetry[index] = entry;
            _lastTelemetry = entry;
            counters[0] = counter;

            if (!_telemetryDumped && ((entry.Flags & FaultTelemetryFlag) != 0u || elapsedUs > TelemetryDumpThresholdMicroseconds))
            {
                _telemetryDumped = DumpTelemetry(telemetry);
            }
        }

        private static unsafe bool DumpTelemetry(NativeArray<BallisticsTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return false;

            NativeArray<byte> payload = default;
            try
            {
                const string dumpPath = "Docs/AgentLogs/Dump_BALLISTICS_SURGEON.bin";
                int count = math.min(telemetry.Length, TelemetryRingLength);
                const int HeaderBytes = 8;
                const int EntryStride = 48;
                int totalBytes = HeaderBytes + (count * EntryStride);
                if (count <= 0 || totalBytes <= HeaderBytes)
                    return false;

                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(BallisticsRuntime),
                    "BallisticsTelemetryDumpPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> header = new Span<byte>(target, HeaderBytes);
                header.Clear();
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), SourceHash);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), (uint)TelemetryRingLength);

                for (int i = 0; i < count; i++)
                {
                    Span<byte> entryBytes = new Span<byte>(target + HeaderBytes + (i * EntryStride), EntryStride);
                    WriteBallisticsTelemetryEntry(entryBytes, telemetry[i]);
                }

                return NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, totalBytes);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(BallisticsRuntime),
                    "BallisticsTelemetryDumpPayload");
            }
        }

        private static void WriteBallisticsTelemetryEntry(Span<byte> entryBytes, in BallisticsTelemetryEntry entry)
        {
            entryBytes.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(4, 4), entry.TrajectoriesProcessed);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(8, 4), entry.HitCount);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(12, 4), entry.RicochetCount);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(16, 4), entry.NanGuardCount);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(20, 4), entry.Flags);
            WriteFloatLittleEndian(entryBytes.Slice(24, 4), entry.SolveMicroseconds);
            WriteFloatLittleEndian(entryBytes.Slice(28, 4), entry.GlobalQualityWeight);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(32, 4), entry.PrimitiveCount);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(36, 4), entry.SignalCount);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(40, 4), entry.RejectedCount);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(44, 4), entry.ActiveTrajectoryBufferId);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private static void SeedDefaultTuning()
        {
            NativeArray<BallisticsTuningDTO> tuning = OpenVaultLane(in _tuningHandle);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            BallisticsTuningDTO value = tuning[0];
            if (value.Revision != 0u)
                return;

            value.DragCoefficient = 0.085f;
            value.LethalityThreshold = 4.0f;
            value.RicochetFriction = 0.38f;
            value.RicochetIncidenceThreshold = 0.28f;
            value.DamageEnergyScale = 0.045f;
            value.MaxRangeMeters = 120f;
            value.FloraBaseVelocity = 28f;
            value.FloraSpikeMassKg = FloraSpikeMassKg;
            value.GlobalQualityWeight = ResolveGlobalQualityWeight();
            value.LimbAdmissionFloor = 0.25f;
            value.MockGridSpacingMeters = 1.4f;
            value.Revision = 1u;
            tuning[0] = value;
        }

        private static void SeedDefaultPenetrationLut()
        {
            NativeArray<float> lut = OpenVaultLane(in _penetrationLutHandle);
            if (!lut.IsCreated || lut.Length < PenetrationLutLength)
                return;

            for (int weapon = 0; weapon < 8; weapon++)
            {
                for (int material = 0; material < 8; material++)
                {
                    float weaponScalar = math.lerp(0.55f, 1.4f, weapon * 0.14285715f);
                    float materialScalar = 1f - (material * 0.075f);
                    lut[(weapon * 8) + material] = math.max(0.08f, weaponScalar * materialScalar);
                }
            }
        }

        private static void ResetTransientState()
        {
            ReleaseActiveJobMutationGuard();
            _activeHandle = default;
            _activeJobMutationGuardVault = null;
            _pendingTrajectoryCount = 0;
            _primitiveCount = 0;
            _writeTrajectoryBufferIndex = 0;
            _activeReadBufferIndex = 0;
            _activeReadCount = 0;
            _activeTelemetryIndex = 0;
            _telemetryCursor = 0u;
            _simulationFrame = 0u;
            _activeScheduleTicks = 0L;
            _jobScheduled = false;
            _telemetryDumped = false;
            _lastTelemetry = default;
        }

        private static void ReleaseVaultLanes(IDataVault vault)
        {
            ReleaseActiveJobMutationGuard();

            if (vault == null)
                return;

            ReleaseVaultLane(vault, ref _trajectoryAHandle);
            ReleaseVaultLane(vault, ref _trajectoryBHandle);
            ReleaseVaultLane(vault, ref _primitiveHandle);
            ReleaseVaultLane(vault, ref _hitHandle);
            ReleaseVaultLane(vault, ref _penetrationLutHandle);
            ReleaseVaultLane(vault, ref _telemetryHandle);
            ReleaseVaultLane(vault, ref _counterHandle);
            ReleaseVaultLane(vault, ref _tuningHandle);
            ReleaseVaultLane(vault, ref _impactVfxHandle);
        }

        private static void ReleaseVaultLane<T>(IDataVault vault, ref VaultLane<T> lane)
            where T : struct
        {
            if (lane.Handle.BufferID != 0u)
                vault.ReleaseBuffer(in lane.Handle);

            lane = default;
        }

        private static VaultLane<T> AcquireVaultLane<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (_vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = _vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u)
                return default;

            return new VaultLane<T>
            {
                Handle = handle,
                ExpectedBufferID = expectedBufferId,
                Length = requiredLength
            };
        }

        private static bool IsVaultLaneBound<T>(in VaultLane<T> lane) where T : struct
        {
            return lane.ExpectedBufferID != 0u &&
                   lane.Handle.BufferID == lane.ExpectedBufferID &&
                   lane.Handle.Generation != 0u &&
                   lane.Length > 0;
        }

        private static bool AreVaultLanesBound()
        {
            return IsVaultLaneBound(in _trajectoryAHandle) &&
                   IsVaultLaneBound(in _trajectoryBHandle) &&
                   IsVaultLaneBound(in _primitiveHandle) &&
                   IsVaultLaneBound(in _hitHandle) &&
                   IsVaultLaneBound(in _penetrationLutHandle) &&
                   IsVaultLaneBound(in _telemetryHandle) &&
                   IsVaultLaneBound(in _counterHandle) &&
                   IsVaultLaneBound(in _tuningHandle) &&
                   IsVaultLaneBound(in _impactVfxHandle);
        }

        private static NativeArray<T> OpenVaultLane<T>(in VaultLane<T> lane) where T : struct
        {
            if (_vault == null ||
                !IsVaultLaneBound(in lane) ||
                !_vault.TryResolveHandle(in lane.Handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < lane.Length)
            {
                return default;
            }

            return buffer;
        }

        private static BallisticsTuningDTO ResolveTuning(float quality)
        {
            NativeArray<BallisticsTuningDTO> tuning = OpenVaultLane(in _tuningHandle);
            BallisticsTuningDTO value = tuning.IsCreated && tuning.Length > 0 ? tuning[0] : default;
            value = SanitizeTuning(value);
            value.GlobalQualityWeight = quality;
            return value;
        }

        private static BallisticsTuningDTO SanitizeTuning(BallisticsTuningDTO value)
        {
            value.DragCoefficient = math.clamp(SelectFinite(value.DragCoefficient, 0.085f), 0f, 4f);
            value.LethalityThreshold = math.clamp(SelectFinite(value.LethalityThreshold, 4f), 0.01f, 1000f);
            value.RicochetFriction = math.clamp(SelectFinite(value.RicochetFriction, 0.38f), 0.02f, 0.98f);
            value.RicochetIncidenceThreshold = math.clamp(SelectFinite(value.RicochetIncidenceThreshold, 0.28f), 0.02f, 0.95f);
            value.DamageEnergyScale = math.clamp(SelectFinite(value.DamageEnergyScale, 0.045f), 0.0001f, 20f);
            value.MaxRangeMeters = math.clamp(SelectFinite(value.MaxRangeMeters, 120f), 0.25f, 2000f);
            value.FloraBaseVelocity = math.clamp(SelectFinite(value.FloraBaseVelocity, 28f), 0.25f, 400f);
            value.FloraSpikeMassKg = math.clamp(SelectFinite(value.FloraSpikeMassKg, FloraSpikeMassKg), 0.0001f, 10f);
            value.GlobalQualityWeight = SanitizeQualityWeight(SelectFinite(value.GlobalQualityWeight, ResolveGlobalQualityWeight()));
            value.LimbAdmissionFloor = math.clamp(SelectFinite(value.LimbAdmissionFloor, 0.25f), 0f, 0.9f);
            value.MockGridSpacingMeters = math.clamp(SelectFinite(value.MockGridSpacingMeters, 1.4f), 0.1f, 20f);
            return value;
        }

        private static NativeArray<BallisticTrajectoryDTO> ResolveWriteTrajectories()
        {
            return (_writeTrajectoryBufferIndex & 1) == 0
                ? OpenVaultLane(in _trajectoryAHandle)
                : OpenVaultLane(in _trajectoryBHandle);
        }

        private static NativeArray<BallisticTrajectoryDTO> ResolveActiveOrWriteTrajectories()
        {
            if (_jobScheduled || _activeReadCount > 0)
            {
                return (_activeReadBufferIndex & 1) == 0
                    ? OpenVaultLane(in _trajectoryAHandle)
                    : OpenVaultLane(in _trajectoryBHandle);
            }

            return ResolveWriteTrajectories();
        }

        private static BufferID ResolveWriteBufferId()
        {
            return (_writeTrajectoryBufferIndex & 1) == 0
                ? BallisticsVaultBufferIds.TrajectoriesA
                : BallisticsVaultBufferIds.TrajectoriesB;
        }

        private static BufferID ResolveActiveReadBufferId()
        {
            return (_activeReadBufferIndex & 1) == 0
                ? BallisticsVaultBufferIds.TrajectoriesA
                : BallisticsVaultBufferIds.TrajectoriesB;
        }

        private static void ClearCounter(
            NativeArray<BallisticsCountersDTO> counters,
            uint frame,
            float quality,
            uint activeBufferId,
            int primitiveCount)
        {
            if (!counters.IsCreated || counters.Length <= 0)
                return;

            BallisticsCountersDTO counter = default;
            counter.Frame = frame;
            counter.GlobalQualityWeight = quality;
            counter.ActiveTrajectoryBufferId = activeBufferId;
            counter.PrimitiveCount = (uint)math.max(0, primitiveCount);
            counters[0] = counter;
        }

        private static float ResolveGlobalQualityWeight()
        {
            return SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight);
        }

        private static int ComputeDamageSignalBudget(float quality)
        {
            float smoothed = SmoothQualityWeight(quality);
            return math.clamp(
                (int)math.round(math.lerp(LowQualityDamageSignalsPerSolve, MaxDamageSignalsPerSolve, smoothed)),
                LowQualityDamageSignalsPerSolve,
                MaxDamageSignalsPerSolve);
        }

        private static float ResolveArmorScalar(CombatArmorClass armorClass)
        {
            switch (armorClass)
            {
                case CombatArmorClass.Suit: return 0.9f;
                case CombatArmorClass.Shell: return 0.72f;
                case CombatArmorClass.Structure: return 0.58f;
                case CombatArmorClass.OrganicHeavy: return 0.68f;
                case CombatArmorClass.Brittle: return 1.25f;
                case CombatArmorClass.Shielded: return 0.5f;
                default: return 1f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashPrimitive(uint targetId, uint primitiveIndex)
        {
            return math.hash(new uint2(targetId, primitiveIndex ^ 0x9E3779B9u));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool TryResolveAupDoubleFromRuntimeOrigin(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!positionAup.IsFinite())
                return false;

            absoluteAup = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        private static bool TryResolveCurrentRuntimeOriginDouble3(out double3 absoluteAup)
        {
            absoluteAup = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = originAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & (lengthSq > Epsilon);
            float3 selected = math.select(fallback, value, new bool3(valid));
            return selected * math.rsqrt(math.max(math.lengthsq(selected), Epsilon));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SanitizeQualityWeight(float value)
        {
            return math.saturate(SelectFinite(value, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SelectFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SmoothQualityWeight(float value)
        {
            float t = SanitizeQualityWeight(value);
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static quaternion NormalizeOrIdentity(quaternion value)
        {
            float lengthSq = math.lengthsq(value.value);
            bool valid = math.isfinite(lengthSq) & (lengthSq > Epsilon);
            return new quaternion(math.select(
                quaternion.identity.value,
                value.value * math.rsqrt(math.max(lengthSq, Epsilon)),
                new bool4(valid)));
        }

#if UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length - 1;
            while (start <= end && token[start] <= 32)
                start++;
            while (end >= start && token[end] <= 32)
                end--;
            return start <= end ? token.Slice(start, (end - start) + 1) : ReadOnlySpan<byte>.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool StartsNumeric(ReadOnlySpan<byte> token)
        {
            if (token.Length <= 0)
                return false;

            byte c = token[0];
            return (c >= (byte)'0' && c <= (byte)'9') || c == (byte)'-' || c == (byte)'+' || c == (byte)'.';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveWeaponCsvIndex(uint hash)
        {
            switch (hash)
            {
                case CsvWeaponMicroHash: return 0;
                case CsvWeaponNeedleHash: return 1;
                case CsvWeaponSpikeHash: return 2;
                case CsvWeaponSlugHash: return 3;
                case CsvWeaponHarpoonHash: return 4;
                case CsvWeaponRailHash: return 5;
                case CsvWeaponSupercavHash: return 6;
                case CsvWeaponAbyssalHash: return 7;
                case CsvWeaponHeaderHash: return -2;
                default: return -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveMaterialCsvIndex(uint hash)
        {
            switch (hash)
            {
                case CsvMaterialFleshHash: return 0;
                case CsvMaterialChitinHash: return 1;
                case CsvMaterialPlasteelHash: return 2;
                case CsvMaterialGlassHash: return 3;
                case CsvMaterialOrganicHeavyHash: return 4;
                case CsvMaterialBrittleHash: return 5;
                case CsvMaterialShieldedHash: return 6;
                case CsvMaterialReservedHash: return 7;
                default: return -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveHeaderMaterialIndex(
            int col,
            int header0,
            int header1,
            int header2,
            int header3,
            int header4,
            int header5,
            int header6,
            int header7)
        {
            switch (col)
            {
                case 0: return header0;
                case 1: return header1;
                case 2: return header2;
                case 3: return header3;
                case 4: return header4;
                case 5: return header5;
                case 6: return header6;
                case 7: return header7;
                default: return col;
            }
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length <= 0)
                return false;

            int i = 0;
            float sign = 1f;
            if (token[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }
            else if (token[i] == (byte)'+')
            {
                i++;
            }

            float whole = 0f;
            bool any = false;
            while (i < token.Length && token[i] >= (byte)'0' && token[i] <= (byte)'9')
            {
                whole = (whole * 10f) + (token[i] - (byte)'0');
                i++;
                any = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (i < token.Length && token[i] == (byte)'.')
            {
                i++;
                while (i < token.Length && token[i] >= (byte)'0' && token[i] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (token[i] - (byte)'0');
                    divisor *= 10f;
                    i++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = sign * (whole + (fraction * math.rcp(math.max(divisor, 1f))));
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashFnv1aLower(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }
#endif
    
        #region JulesLink_SplashEntryAngleCalculator
        private static void JulesLink_SplashEntryAngleCalculator() { _ = typeof(Hecton8.PureLogic.Systems.SplashEntryAngleCalculator); }
        #endregion

        #region JulesLink_ProjectileDropCalculator
        private static void JulesLink_ProjectileDropCalculator() { _ = typeof(Hecton8.PureLogic.Systems.ProjectileDropCalculator); }
        #endregion
}

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockBallisticsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<BallisticTrajectoryDTO> Trajectories;
        [NoAlias] public NativeArray<AABBPrimitiveDTO> Primitives;
        public int TrajectoryCount;
        public int PrimitiveCount;
        public float GridSpacingMeters;
        public double3 MockOriginAUP;
        public uint Frame;

        public void Execute(int index)
        {
            int trajectoryStride = UnsafeUtility.SizeOf<BallisticTrajectoryDTO>();
            int primitiveStride = UnsafeUtility.SizeOf<AABBPrimitiveDTO>();
            byte* trajectoryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Trajectories);
            byte* primitivePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Primitives);

            if ((uint)index < (uint)PrimitiveCount)
            {
                float spacing = math.max(0.5f, GridSpacingMeters);
                int x = index & 15;
                int y = (index >> 4) & 7;
                int z = index >> 7;
                AABBPrimitiveDTO primitive = default;
                primitive.CenterAUP = MockOriginAUP + new double3((x - 8) * spacing, (y - 4) * spacing, 16.0 + (z * spacing));
                primitive.HalfExtents = new float3(0.32f, 0.46f, 0.32f);
                primitive.TargetEntityID = (uint)(10000 + index);
                primitive.Rotation = quaternion.identity;
                primitive.MaterialHash = (uint)(index & 7);
                primitive.PrimitiveHash = math.hash(new uint2((uint)index, Frame));
                primitive.Flags = AABBPrimitiveFlags.Active | AABBPrimitiveFlags.Root | AABBPrimitiveFlags.Mock;
                primitive.DamageMultiplier = 1f;
                primitive.ArmorScalar = 1f;
                ref AABBPrimitiveDTO primitiveSlot = ref UnsafeUtility.AsRef<AABBPrimitiveDTO>(primitivePtr + (index * primitiveStride));
                primitiveSlot = primitive;
            }

            if ((uint)index < (uint)TrajectoryCount)
            {
                int lane = index % 32;
                float lateral = (lane - 15.5f) * 0.18f;
                BallisticTrajectoryDTO trajectory = default;
                trajectory.OriginAUP = MockOriginAUP + new double3(lateral, -1.2, -14.0);
                trajectory.Direction = BallisticsRuntime.NormalizeOrDefault(
                    new float3(lateral * -0.02f, 0.03f * ((index & 3) - 1), 1f),
                    new float3(0f, 0f, 1f));
                trajectory.Velocity = 84f + ((index & 15) * 0.75f);
                trajectory.Mass = 0.018f;
                trajectory.WeaponHash = BallisticWeaponHashes.MockNeedle + (uint)(index & 7);
                trajectory.SourceEntityID = 0x53484D4Fu;
                trajectory.Flags = BallisticTrajectoryFlags.Mock;
                ref BallisticTrajectoryDTO trajectorySlot = ref UnsafeUtility.AsRef<BallisticTrajectoryDTO>(trajectoryPtr + (index * trajectoryStride));
                trajectorySlot = trajectory;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BallisticIntersectionJob : IJobParallelFor
    {
        private const float Epsilon = 0.0001f;

        [ReadOnly, NoAlias] public NativeArray<BallisticTrajectoryDTO> Trajectories;
        [ReadOnly, NoAlias] public NativeArray<AABBPrimitiveDTO> Primitives;
        [ReadOnly, NoAlias] public NativeArray<float> PenetrationLut;
        [NoAlias] public NativeArray<BallisticHitResultDTO> HitResults;
        public BallisticsTuningDTO Tuning;
        public int TrajectoryCount;
        public int PrimitiveCount;
        public uint Frame;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)TrajectoryCount || (uint)index >= (uint)HitResults.Length)
                return;

            void* trajectoryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Trajectories);
            void* hitPtr = NativeArrayUnsafeUtility.GetUnsafePtr(HitResults);
            ref readonly BallisticTrajectoryDTO trajectory = ref UnsafeUtility.AsRef<BallisticTrajectoryDTO>(
                (byte*)trajectoryPtr + (index * UnsafeUtility.SizeOf<BallisticTrajectoryDTO>()));
            ref BallisticHitResultDTO result = ref UnsafeUtility.AsRef<BallisticHitResultDTO>(
                (byte*)hitPtr + (index * UnsafeUtility.SizeOf<BallisticHitResultDTO>()));

            result = default;
            float3 direction = BallisticsRuntime.NormalizeOrDefault(trajectory.Direction, new float3(0f, 0f, 1f));
            float velocity = math.max(0f, BallisticsRuntime.SelectFinite(trajectory.Velocity, 0f));
            float mass = math.max(0.0001f, BallisticsRuntime.SelectFinite(trajectory.Mass, 0.0001f));
            if (!math.all(math.isfinite(trajectory.OriginAUP)) || velocity <= 0.0001f)
            {
                result.Flags = BallisticHitFlags.NanGuard;
                return;
            }

            float quality = BallisticsRuntime.SmoothQualityWeight(GlobalQualityWeight);
            int maxRicochets = ComputeRicochetBudget(quality);
            int primitiveCount = math.min(PrimitiveCount, Primitives.Length);
            byte* primitivePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Primitives);
            float* penetrationPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(PenetrationLut);
            int primitiveStride = UnsafeUtility.SizeOf<AABBPrimitiveDTO>();
            double3 originAup = trajectory.OriginAUP;
            uint ricochetCount = 0u;
            uint rejectedCount = 0u;
            // Path length actually flown, summed over completed ricochet segments. closestDistance below is
            // only the CURRENT segment, so a ricochet hit used to report a Distance shorter than the travel.
            float travelledMeters = 0f;

            for (int bounce = 0; bounce <= maxRicochets; bounce++)
            {
                float closestDistance = Tuning.MaxRangeMeters;
                int closestIndex = -1;
                float3 closestLocalHit = float3.zero;
                float3 closestNormal = float3.zero;
                double3 closestHitAup = originAup;

                for (int i = 0; i < primitiveCount; i++)
                {
                    ref readonly AABBPrimitiveDTO primitive = ref UnsafeUtility.AsRef<AABBPrimitiveDTO>(
                        primitivePtr + (i * primitiveStride));
                    if ((primitive.Flags & AABBPrimitiveFlags.Active) == 0u)
                        continue;

                    if (!ShouldEvaluatePrimitive(in primitive, quality))
                    {
                        rejectedCount++;
                        continue;
                    }

                    if (!TryIntersectPrimitive(
                            in primitive,
                            originAup,
                            direction,
                            closestDistance,
                            out float distance,
                            out float3 localHit,
                            out float3 normal,
                            out double3 hitAup))
                        continue;

                    closestDistance = distance;
                    closestIndex = i;
                    closestLocalHit = localHit;
                    closestNormal = normal;
                    closestHitAup = hitAup;
                }

                if (closestIndex < 0)
                {
                    // Flew off without intersecting anything: closestDistance is still the broadphase cap, not a
                    // travel length, so report only the ricochet path already confirmed.
                    result.Distance = travelledMeters;
                    result.Flags = rejectedCount > 0u ? BallisticHitFlags.LethalityExpired : BallisticHitFlags.None;
                    return;
                }

                float finalVelocity = velocity * MathLodApproximation.ApproxExpNegPade33Wide40(math.max(0f, Tuning.DragCoefficient) * closestDistance);
                if (!math.isfinite(finalVelocity) || finalVelocity < Tuning.LethalityThreshold)
                {
                    result.Flags = BallisticHitFlags.LethalityExpired;
                    result.Distance = travelledMeters + closestDistance;
                    result.RemainingVelocity = math.max(0f, math.select(0f, finalVelocity, math.isfinite(finalVelocity)));
                    return;
                }

                ref readonly AABBPrimitiveDTO hitPrimitive = ref UnsafeUtility.AsRef<AABBPrimitiveDTO>(
                    primitivePtr + (closestIndex * primitiveStride));
                float penetrationScalar = ResolvePenetrationScalar(penetrationPtr, PenetrationLut.Length, trajectory.WeaponHash, hitPrimitive.MaterialHash);
                float incidence = math.abs(math.dot(-direction, BallisticsRuntime.NormalizeOrDefault(closestNormal, new float3(0f, 0f, 1f))));
                bool canRicochet = penetrationScalar < 0.42f &&
                                   incidence < Tuning.RicochetIncidenceThreshold &&
                                   bounce < maxRicochets;
                if (canRicochet)
                {
                    travelledMeters += closestDistance;
                    velocity = math.max(0f, finalVelocity * Tuning.RicochetFriction);
                    direction = BallisticsRuntime.NormalizeOrDefault(
                        direction - (2f * math.dot(direction, closestNormal) * closestNormal),
                        direction);
                    originAup = closestHitAup + ((double3)closestNormal * 0.015d);
                    ricochetCount++;
                    continue;
                }

                // Range falloff is ALREADY in this expression: finalVelocity carries exp(-DragCoefficient * distance)
                // from above and kinetic energy squares it, so damage decays as exp(-2 * DragCoefficient * distance).
                // Every factor below is clamped to an explicit interval so the product cannot amplify a hit; the
                // drag factor itself is saturated to [0,1] by ApproxExpNegPade33Wide40, and the sub-lethal case is
                // already rejected by the LethalityThreshold gate above rather than silently reaching zero damage.
                float kineticEnergy = 0.5f * mass * finalVelocity * finalVelocity;
                float boundedPenetration = math.clamp(penetrationScalar, 0f, BallisticsRuntime.MaxPenetrationScalar);
                float boundedDamageMultiplier = math.clamp(
                    BallisticsRuntime.SelectFinite(hitPrimitive.DamageMultiplier, 1f),
                    0f,
                    BallisticsRuntime.MaxPrimitiveDamageMultiplier);
                float boundedArmorScalar = math.clamp(
                    BallisticsRuntime.SelectFinite(hitPrimitive.ArmorScalar, 1f),
                    BallisticsRuntime.MinPrimitiveArmorScalar,
                    BallisticsRuntime.MaxPrimitiveArmorScalar);
                float damage = kineticEnergy * boundedPenetration * boundedDamageMultiplier * boundedArmorScalar *
                               math.max(0.0001f, Tuning.DamageEnergyScale);
                if (!math.isfinite(damage) || damage <= 0.0001f)
                {
                    result.Flags = BallisticHitFlags.NanGuard;
                    return;
                }

                result.HitAUP = closestHitAup;
                result.LocalHitPoint = closestLocalHit;
                result.Normal = closestNormal;
                result.ImpactDirection = direction;
                result.Damage = damage;
                result.RemainingVelocity = finalVelocity;
                result.Distance = travelledMeters + closestDistance;
                result.TargetEntityID = hitPrimitive.TargetEntityID;
                result.SourceEntityID = trajectory.SourceEntityID;
                result.WeaponHash = trajectory.WeaponHash;
                result.MaterialHash = hitPrimitive.MaterialHash;
                result.Flags = BallisticHitFlags.Hit | math.select(0u, BallisticHitFlags.Ricochet, ricochetCount > 0u);
                result.Frame = Frame;
                result.RicochetCount = ricochetCount;
                result.PrimitiveHash = hitPrimitive.PrimitiveHash;

                return;
            }

            result.Distance = travelledMeters;
            result.Flags = BallisticHitFlags.LethalityExpired;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldEvaluatePrimitive(in AABBPrimitiveDTO primitive, float quality)
        {
            if ((primitive.Flags & AABBPrimitiveFlags.Root) != 0u)
                return true;

            float floor = math.clamp(BallisticsRuntime.SelectFinite(Tuning.LimbAdmissionFloor, 0.25f), 0f, 0.9f);
            float admission = math.smoothstep(floor, 0.95f, quality);
            float hash01 = ((primitive.PrimitiveHash * 747796405u) + 2891336453u) * 2.3283064e-10f;
            return hash01 <= admission;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryIntersectPrimitive(
            in AABBPrimitiveDTO primitive,
            double3 originAup,
            float3 direction,
            float maxDistance,
            out float distance,
            out float3 localHit,
            out float3 worldNormal,
            out double3 hitAup)
        {
            distance = 0f;
            localHit = float3.zero;
            worldNormal = new float3(0f, 0f, 1f);
            hitAup = originAup;
            if (!math.all(math.isfinite(primitive.CenterAUP)) || !math.all(math.isfinite(primitive.HalfExtents)))
                return false;

            double3 deltaAup = originAup - primitive.CenterAUP;
            if (!math.all(math.isfinite(deltaAup)))
                return false;

            float3 half = math.max(math.abs(primitive.HalfExtents), new float3(0.01f));
            float3 centerFromOrigin = AupPrecisionMath.DowncastLocalDelta(-deltaAup, float3.zero);
            float radiusSq = math.max(math.lengthsq(half), Epsilon);
            float projected = math.dot(centerFromOrigin, direction);
            if (!math.isfinite(projected))
                return false;

            if (projected < 0f && (projected * projected) > radiusSq)
                return false;

            float excessPastSegment = projected - maxDistance;
            if (excessPastSegment > 0f && (excessPastSegment * excessPastSegment) > radiusSq)
                return false;

            float clampedProjection = math.clamp(projected, 0f, math.max(0f, maxDistance));
            float3 nearestOnRay = direction * clampedProjection;
            if (math.lengthsq(centerFromOrigin - nearestOnRay) > radiusSq)
                return false;

            quaternion rotation = BallisticsRuntime.NormalizeOrIdentity(primitive.Rotation);
            quaternion inverseRotation = math.conjugate(rotation);
            float3 localOrigin = math.mul(inverseRotation, AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero));
            float3 localDirection = BallisticsRuntime.NormalizeOrDefault(math.mul(inverseRotation, direction), new float3(0f, 0f, 1f));
            if (!TryIntersectAabbSlab(localOrigin, localDirection, half, maxDistance, out distance, out localHit, out float3 localNormal))
                return false;

            worldNormal = BallisticsRuntime.NormalizeOrDefault(math.mul(rotation, localNormal), new float3(0f, 0f, 1f));
            float3 relativeWorldHit = math.mul(rotation, localHit);
            hitAup = primitive.CenterAUP + (double3)relativeWorldHit;
            return math.all(math.isfinite(hitAup));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryIntersectAabbSlab(
            float3 origin,
            float3 direction,
            float3 half,
            float maxDistance,
            out float tHit,
            out float3 hitPoint,
            out float3 normal)
        {
            float3 safeDirection = math.select(
                direction,
                math.select(new float3(Epsilon), new float3(-Epsilon), direction < 0f),
                math.abs(direction) < Epsilon);
            float3 invDir = math.rcp(safeDirection);
            float3 t0 = (-half - origin) * invDir;
            float3 t1 = (half - origin) * invDir;
            float3 tMin3 = math.min(t0, t1);
            float3 tMax3 = math.max(t0, t1);
            float tMin = math.max(math.max(tMin3.x, tMin3.y), math.max(tMin3.z, 0f));
            float tMax = math.min(tMax3.x, math.min(tMax3.y, tMax3.z));
            bool hit = tMax >= tMin && tMin <= maxDistance && math.isfinite(tMin);
            tHit = tMin;
            hitPoint = origin + (direction * tMin);
            normal = ResolveAabbNormal(tMin3, tMin, direction);
            return hit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveAabbNormal(float3 tMin3, float tMin, float3 direction)
        {
            float3 normal = float3.zero;
            if (math.abs(tMin - tMin3.x) <= 0.0002f)
                normal = new float3(-math.sign(direction.x), 0f, 0f);
            else if (math.abs(tMin - tMin3.y) <= 0.0002f)
                normal = new float3(0f, -math.sign(direction.y), 0f);
            else
                normal = new float3(0f, 0f, -math.sign(direction.z));

            return BallisticsRuntime.NormalizeOrDefault(normal, new float3(0f, 0f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvePenetrationScalar(float* penetrationPtr, int penetrationLength, uint weaponHash, uint materialHash)
        {
            uint weaponClass = weaponHash & 7u;
            uint materialClass = materialHash & 7u;
            int index = (int)((weaponClass * 8u) + materialClass);
            if (penetrationPtr == null || (uint)index >= (uint)penetrationLength)
                return 0.25f;

            float value = penetrationPtr[index];
            return math.max(0f, BallisticsRuntime.SelectFinite(value, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeRicochetBudget(float quality)
        {
            return math.clamp(
                (int)math.floor(math.lerp(0.05f, BallisticsRuntime.MaxRicochetsPerTrajectory + 0.95f, quality)),
                0,
                BallisticsRuntime.MaxRicochetsPerTrajectory);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EmitBallisticDamageSignalsJob : IJob
    {
        [NoAlias] public NativeArray<BallisticHitResultDTO> HitResults;
        public global::Hecton8.Core.MpscSignalRingBuffer<CombatDamageSignal>.ParallelWriter DamageWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> DamageWriterBudget;
        public int HitCount;
        public int SignalEmitBudget;
        public uint Frame;

        public void Execute()
        {
            int count = math.min(math.max(0, HitCount), HitResults.Length);
            int budget = math.max(0, SignalEmitBudget);
            int emitted = 0;
            byte* hitPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(HitResults);
            int hitStride = UnsafeUtility.SizeOf<BallisticHitResultDTO>();

            for (int i = 0; i < count; i++)
            {
                ref BallisticHitResultDTO hit = ref UnsafeUtility.AsRef<BallisticHitResultDTO>(hitPtr + (i * hitStride));
                if ((hit.Flags & BallisticHitFlags.Hit) == 0u)
                    continue;

                if (emitted < budget &&
                    hit.TargetEntityID != 0u &&
                    math.isfinite(hit.Damage) &&
                    hit.Damage > 0.0001f)
                {
                    CombatDamageSignal signal = default;
                    signal.ImpactAup = hit.HitAUP;
                    signal.Direction = BallisticsRuntime.NormalizeOrDefault(
                        hit.ImpactDirection,
                        BallisticsRuntime.NormalizeOrDefault(-hit.Normal, new float3(0f, 0f, 1f)));
                    signal.Magnitude = hit.Damage;
                    signal.DamageType = CombatDamageTypes.Impact;
                    signal.TargetHash = hit.TargetEntityID;
                    signal.SourceHash = hit.SourceEntityID;
                    signal.Frame = Frame;
                    signal.SourceId = (ushort)math.min(ushort.MaxValue, hit.SourceEntityID);
                    signal.TargetId = (ushort)math.min(ushort.MaxValue, hit.TargetEntityID);
                    signal.Channel = 0;
                    signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
                    if (SignalBus<CombatDamageSignal>.TryEnqueueBounded(DamageWriter, DamageWriterBudget, signal))
                    {
                        emitted++;
                        continue;
                    }
                }

                hit.Flags |= BallisticHitFlags.SignalDropped;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct StageImpactVFXJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<BallisticHitResultDTO> HitResults;
        [NoAlias] public NativeArray<BallisticImpactVfxDTO> ImpactVfx;
        public int HitCount;
        public uint Frame;
        public float GlobalQualityWeight;
        public double3 PresentationOriginAUP;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)HitCount || (uint)index >= (uint)ImpactVfx.Length)
                return;

            byte* hitPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(HitResults);
            byte* vfxPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(ImpactVfx);
            ref readonly BallisticHitResultDTO hit = ref UnsafeUtility.AsRef<BallisticHitResultDTO>(
                hitPtr + (index * UnsafeUtility.SizeOf<BallisticHitResultDTO>()));
            ref BallisticImpactVfxDTO vfxSlot = ref UnsafeUtility.AsRef<BallisticImpactVfxDTO>(
                vfxPtr + (index * UnsafeUtility.SizeOf<BallisticImpactVfxDTO>()));
            BallisticImpactVfxDTO vfx = default;
            if ((hit.Flags & BallisticHitFlags.Hit) == 0u)
            {
                vfxSlot = vfx;
                return;
            }

            float3 up = BallisticsRuntime.NormalizeOrDefault(hit.Normal, new float3(0f, 0f, 1f));
            float3 fallback = math.select(new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), new bool3(math.abs(up.y) < 0.95f));
            float3 right = BallisticsRuntime.NormalizeOrDefault(math.cross(fallback, up), new float3(1f, 0f, 0f));
            float3 forward = BallisticsRuntime.NormalizeOrDefault(math.cross(up, right), new float3(0f, 0f, 1f));
            float scale = math.lerp(0.035f, 0.11f, BallisticsRuntime.SanitizeQualityWeight(GlobalQualityWeight));
            double3 runtimeAup = hit.HitAUP - PresentationOriginAUP;
            float3 runtimeHit = math.select(
                hit.LocalHitPoint,
                AupPrecisionMath.DowncastLocalDelta(runtimeAup, hit.LocalHitPoint),
                new bool3(math.all(math.isfinite(runtimeAup))));
            vfx.Matrix = new float4x4(
                new float4(right * scale, 0f),
                new float4(up * scale, 0f),
                new float4(forward * scale, 0f),
                new float4(runtimeHit, 1f));
            vfx.MaterialHash = hit.MaterialHash;
            vfx.TargetEntityID = hit.TargetEntityID;
            vfx.Flags = hit.Flags;
            vfx.Frame = Frame;
            vfxSlot = vfx;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BallisticsTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<BallisticHitResultDTO> HitResults;
        [NoAlias] public NativeArray<BallisticsTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<BallisticsCountersDTO> Counters;
        public int TrajectoryCount;
        public int PrimitiveCount;
        public int TelemetryIndex;
        public uint Frame;
        public float GlobalQualityWeight;
        public uint ActiveTrajectoryBufferId;

        public void Execute()
        {
            BallisticsCountersDTO counters = default;
            counters.Frame = Frame;
            counters.TrajectoriesProcessed = (uint)math.max(0, TrajectoryCount);
            counters.PrimitiveCount = (uint)math.max(0, PrimitiveCount);
            counters.GlobalQualityWeight = BallisticsRuntime.SanitizeQualityWeight(GlobalQualityWeight);
            counters.ActiveTrajectoryBufferId = ActiveTrajectoryBufferId;

            int count = math.min(TrajectoryCount, HitResults.Length);
            byte* hitPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(HitResults);
            int hitStride = UnsafeUtility.SizeOf<BallisticHitResultDTO>();
            for (int i = 0; i < count; i++)
            {
                ref readonly BallisticHitResultDTO hit = ref UnsafeUtility.AsRef<BallisticHitResultDTO>(hitPtr + (i * hitStride));
                if ((hit.Flags & BallisticHitFlags.Hit) != 0u)
                {
                    counters.HitCount++;
                    if ((hit.Flags & BallisticHitFlags.SignalDropped) == 0u)
                        counters.SignalCount++;
                }

                if ((hit.Flags & BallisticHitFlags.Ricochet) != 0u)
                    counters.RicochetCount += math.max(1u, hit.RicochetCount);

                if ((hit.Flags & BallisticHitFlags.NanGuard) != 0u ||
                    !math.isfinite(hit.Damage) ||
                    !math.isfinite(hit.RemainingVelocity))
                    counters.NanGuardCount++;

                if ((hit.Flags & BallisticHitFlags.LethalityExpired) != 0u)
                    counters.RejectedCount++;

                if ((hit.Flags & BallisticHitFlags.SignalDropped) != 0u)
                    counters.RejectedCount++;
            }

            if ((uint)TelemetryIndex < (uint)TelemetryRing.Length)
            {
                BallisticsTelemetryEntry entry = default;
                entry.Frame = Frame;
                entry.TrajectoriesProcessed = counters.TrajectoriesProcessed;
                entry.HitCount = counters.HitCount;
                entry.RicochetCount = counters.RicochetCount;
                entry.NanGuardCount = counters.NanGuardCount;
                entry.Flags = counters.NanGuardCount > 0u ? 1u : 0u;
                entry.GlobalQualityWeight = counters.GlobalQualityWeight;
                entry.PrimitiveCount = counters.PrimitiveCount;
                entry.SignalCount = counters.SignalCount;
                entry.RejectedCount = counters.RejectedCount;
                entry.ActiveTrajectoryBufferId = ActiveTrajectoryBufferId;
                byte* telemetryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(TelemetryRing);
                ref BallisticsTelemetryEntry telemetrySlot = ref UnsafeUtility.AsRef<BallisticsTelemetryEntry>(
                    telemetryPtr + (TelemetryIndex * UnsafeUtility.SizeOf<BallisticsTelemetryEntry>()));
                telemetrySlot = entry;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                byte* counterPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
                ref BallisticsCountersDTO counterSlot = ref UnsafeUtility.AsRef<BallisticsCountersDTO>(counterPtr);
                counterSlot = counters;
            }
        }
    }
}
