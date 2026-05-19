using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
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
        public const BufferID TrajectoriesA = (BufferID)71270;
        public const BufferID TrajectoriesB = (BufferID)71271;
        public const BufferID AabbPrimitives = (BufferID)71272;
        public const BufferID HitResults = (BufferID)71273;
        public const BufferID PenetrationLut = (BufferID)71274;
        public const BufferID TelemetryRing = (BufferID)71275;
        public const BufferID Counters = (BufferID)71276;
        public const BufferID Tuning = (BufferID)71277;
        public const BufferID ImpactVfx = (BufferID)71278;
        public const BufferID CsvScratch = (BufferID)71279;
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
        public const int CsvScratchBytes = 16384;
        public const float FloraSpikeMassKg = 0.018f;

        private const float Epsilon = 0.0001f;
        private const int MaxDamageSignalsPerSolve = 128;
        private const int LowQualityDamageSignalsPerSolve = 16;
        private const uint FaultTelemetryFlag = 1u << 0;
        private const uint OverBudgetTelemetryFlag = 1u << 1;
        private const uint DumpedTelemetryFlag = 1u << 2;
        private const double TelemetryDumpThresholdMicroseconds = 500.0d;
        private const SystemID OwnerSystem = SystemID.Physics;
        private const ulong MutationGuardBit = 1UL << 42;
        private const uint SourceHash = 0x53483132u; // SH12

        private static readonly ProfilerMarker _frameMarker = new ProfilerMarker("H8.Ballistics.FrameTick");
        private static readonly ProfilerMarker _queueMarker = new ProfilerMarker("H8.Ballistics.QueueTrajectory");

        private static IDataVault _vault;
        private static VaultBufferHandle<BallisticTrajectoryDTO> _trajectoryAHandle;
        private static VaultBufferHandle<BallisticTrajectoryDTO> _trajectoryBHandle;
        private static VaultBufferHandle<AABBPrimitiveDTO> _primitiveHandle;
        private static VaultBufferHandle<BallisticHitResultDTO> _hitHandle;
        private static VaultBufferHandle<float> _penetrationLutHandle;
        private static VaultBufferHandle<BallisticsTelemetryEntry> _telemetryHandle;
        private static VaultBufferHandle<BallisticsCountersDTO> _counterHandle;
        private static VaultBufferHandle<BallisticsTuningDTO> _tuningHandle;
        private static VaultBufferHandle<BallisticImpactVfxDTO> _impactVfxHandle;
        private static VaultBufferHandle<byte> _csvScratchHandle;

        private static JobHandle _activeHandle;
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
        private static bool _solverBuffersLocked;
        private static bool _telemetryDumped;
        private static BallisticsTelemetryEntry _lastTelemetry;

        /// <summary>True when the write trajectory buffer contains shots waiting for the next solve.</summary>
        public static bool HasPendingTrajectories => _pendingTrajectoryCount > 0;

        /// <summary>Finalizes a finished solver before target AABB refresh. Never blocks.</summary>
        public static bool PrepareFrameForTargetRefresh()
        {
            if (!EnsureInitialized())
                return false;

            TryFinalizeScheduled(forceComplete: false);
            return !_jobScheduled && _pendingTrajectoryCount > 0;
        }

        /// <summary>Boots the Vault-backed ballistics buffers. Cold path only.</summary>
        public static bool EnsureInitialized(IDataVault explicitVault = null)
        {
            IDataVault resolvedVault = explicitVault ?? _vault ?? GlobalRegistry.DataVault;
            if (resolvedVault == null)
                return false;

            if (_initialized && ReferenceEquals(_vault, resolvedVault))
                return true;

            bool vaultChanged = _initialized && _vault != null && !ReferenceEquals(_vault, resolvedVault);
            if (vaultChanged)
            {
                TryFinalizeScheduled(forceComplete: true);
                UnlockSolverBuffers();
                ResetTransientState();
            }
            else if (!_initialized)
            {
                ResetTransientState();
            }

            _vault = resolvedVault;
            _trajectoryAHandle = _vault.GetBufferHandle<BallisticTrajectoryDTO>(
                BallisticsVaultBufferIds.TrajectoriesA,
                MaxTrajectories,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _trajectoryBHandle = _vault.GetBufferHandle<BallisticTrajectoryDTO>(
                BallisticsVaultBufferIds.TrajectoriesB,
                MaxTrajectories,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _primitiveHandle = _vault.GetBufferHandle<AABBPrimitiveDTO>(
                BallisticsVaultBufferIds.AabbPrimitives,
                MaxAabbPrimitives,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _hitHandle = _vault.GetBufferHandle<BallisticHitResultDTO>(
                BallisticsVaultBufferIds.HitResults,
                MaxHitResults,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _penetrationLutHandle = _vault.GetBufferHandle<float>(
                BallisticsVaultBufferIds.PenetrationLut,
                PenetrationLutLength,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _vault.GetBufferHandle<BallisticsTelemetryEntry>(
                BallisticsVaultBufferIds.TelemetryRing,
                TelemetryRingLength,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);
            _counterHandle = _vault.GetBufferHandle<BallisticsCountersDTO>(
                BallisticsVaultBufferIds.Counters,
                1,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);
            _tuningHandle = _vault.GetBufferHandle<BallisticsTuningDTO>(
                BallisticsVaultBufferIds.Tuning,
                1,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);
            _impactVfxHandle = _vault.GetBufferHandle<BallisticImpactVfxDTO>(
                BallisticsVaultBufferIds.ImpactVfx,
                MaxImpactVfx,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = _vault.GetBufferHandle<byte>(
                BallisticsVaultBufferIds.CsvScratch,
                CsvScratchBytes,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);

            SeedDefaultTuning();
            SeedDefaultPenetrationLut();
            _initialized = true;
            return true;
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
                if (!EnsureInitialized())
                    return false;

                if (!IsFinite(origin) || !IsFinite(direction))
                    return false;

                float3 resolvedDirection = NormalizeOrDefault(ToFloat3(direction), new float3(0f, 0f, 1f));
                float safeVelocity = math.max(0f, math.isfinite(velocity) ? velocity : 0f);
                float safeMass = math.max(0.0001f, math.isfinite(mass) ? mass : 0.0001f);
                if (safeVelocity <= Epsilon)
                    return false;

                NativeArray<BallisticTrajectoryDTO> writeTrajectories = ResolveWriteTrajectories();
                if (!writeTrajectories.IsCreated || _pendingTrajectoryCount >= math.min(writeTrajectories.Length, MaxTrajectories))
                    return false;

                int index = _pendingTrajectoryCount++;
                BallisticTrajectoryDTO trajectory = default;
                trajectory.OriginAUP = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(origin);
                trajectory.Direction = resolvedDirection;
                trajectory.Velocity = safeVelocity;
                trajectory.Mass = safeMass;
                trajectory.WeaponHash = weaponHash;
                trajectory.SourceEntityID = sourceEntityId;
                trajectory.Flags = flags;
                writeTrajectories[index] = trajectory;
                return true;
            }
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

            float speed = velocity.magnitude;
            if (!float.IsFinite(speed) || speed <= Epsilon)
                return false;

            return QueueTrajectoryFromRuntime(origin, velocity / speed, speed, mass, weaponHash, sourceEntityId, flags);
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

            if (!_vault.TryAcquireMutationGuard(MutationGuardBit))
                return false;

            try
            {
                NativeArray<AABBPrimitiveDTO> primitives = _primitiveHandle.Resolve(_vault);
                if (!primitives.IsCreated)
                    return false;

                int slot = -1;
                int inactiveSlot = -1;
                int count = math.min(_primitiveCount, primitives.Length);
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
                        if (_primitiveCount >= math.min(primitives.Length, MaxAabbPrimitives))
                            return false;

                        slot = _primitiveCount++;
                    }
                }

                AABBPrimitiveDTO primitive = default;
                primitive.CenterAUP = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(center);
                primitive.HalfExtents = math.max(math.abs(ToFloat3(halfExtents)), new float3(0.025f));
                primitive.TargetEntityID = targetEntityId;
                primitive.Rotation = NormalizeOrIdentity(new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
                primitive.MaterialHash = materialHash;
                primitive.PrimitiveHash = primitiveHash;
                primitive.Flags = flags | AABBPrimitiveFlags.Active;
                primitive.DamageMultiplier = math.max(0f, math.isfinite(damageMultiplier) ? damageMultiplier : 1f);
                primitive.ArmorScalar = math.max(0f, math.isfinite(armorScalar) ? armorScalar : 1f);
                primitives[slot] = primitive;
                return true;
            }
            finally
            {
                _vault.ReleaseMutationGuard(MutationGuardBit);
            }
        }

        /// <summary>Registers a conservative root-body AABB for an existing combat target.</summary>
        public static bool RegisterCombatTargetAabb(int targetId, Transform receiverTransform, float height, CombatArmorClass armorClass)
        {
            if (targetId == 0 || receiverTransform == null)
                return false;

            float safeHeight = math.max(0.25f, math.isfinite(height) ? height : 1f);
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

            if (!_vault.TryAcquireMutationGuard(MutationGuardBit))
                return false;

            try
            {
                NativeArray<AABBPrimitiveDTO> primitives = _primitiveHandle.Resolve(_vault);
                if (!primitives.IsCreated)
                    return false;

                bool mutated = false;
                int count = math.min(_primitiveCount, primitives.Length);
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
                _vault.ReleaseMutationGuard(MutationGuardBit);
            }
        }

        /// <summary>Schedules the ballistic solver from the combat router tick without blocking the main thread.</summary>
        public static void FrameTick(float simulationDeltaSeconds)
        {
            using (_frameMarker.Auto())
            {
                if (!EnsureInitialized())
                    return;

                TryFinalizeScheduled(forceComplete: false);
                if (_jobScheduled || _pendingTrajectoryCount <= 0 || _primitiveCount <= 0)
                    return;

                float quality = ResolveGlobalQualityWeight();
                BallisticsTuningDTO tuning = ResolveTuning(quality);
                NativeArray<BallisticTrajectoryDTO> readTrajectories = ResolveWriteTrajectories();
                NativeArray<AABBPrimitiveDTO> primitives = _primitiveHandle.Resolve(_vault);
                NativeArray<BallisticHitResultDTO> hitResults = _hitHandle.Resolve(_vault);
                NativeArray<float> penetrationLut = _penetrationLutHandle.Resolve(_vault);
                NativeArray<BallisticsTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                NativeArray<BallisticsCountersDTO> counters = _counterHandle.Resolve(_vault);
                NativeArray<BallisticImpactVfxDTO> impactVfx = _impactVfxHandle.Resolve(_vault);
                if (!readTrajectories.IsCreated ||
                    !primitives.IsCreated ||
                    !hitResults.IsCreated ||
                    !penetrationLut.IsCreated ||
                    !telemetry.IsCreated ||
                    !counters.IsCreated ||
                    !impactVfx.IsCreated)
                    return;

                int trajectoryCount = math.min(_pendingTrajectoryCount, math.min(readTrajectories.Length, hitResults.Length));
                if (trajectoryCount <= 0)
                    return;

                if (!TryLockSolverBuffers(ResolveReadBufferId()))
                    return;

                _activeReadCount = trajectoryCount;
                _activeReadBufferIndex = _writeTrajectoryBufferIndex;
                _pendingTrajectoryCount = 0;
                _writeTrajectoryBufferIndex ^= 1;
                _activeTelemetryIndex = (int)(_telemetryCursor % TelemetryRingLength);
                _telemetryCursor++;
                uint frame = ++_simulationFrame;
                uint activeBufferId = (uint)ResolveActiveReadBufferId();
                double3 presentationOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                int signalEmitBudget = ResolveDamageSignalBudget(quality);

                ClearCounter(counters, frame, quality, activeBufferId);
                BallisticIntersectionJob intersectionJob = new BallisticIntersectionJob
                {
                    Trajectories = readTrajectories,
                    Primitives = primitives,
                    PenetrationLut = penetrationLut,
                    HitResults = hitResults,
                    Tuning = tuning,
                    TrajectoryCount = trajectoryCount,
                    PrimitiveCount = math.min(_primitiveCount, primitives.Length),
                    Frame = frame,
                    GlobalQualityWeight = quality
                };

                JobHandle handle = intersectionJob.Schedule(trajectoryCount, 32);
                EmitBallisticDamageSignalsJob emitJob = new EmitBallisticDamageSignalsJob
                {
                    HitResults = hitResults,
                    DamageWriter = GlobalSignals.DamageSignalWriter,
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
                    PrimitiveCount = math.min(_primitiveCount, primitives.Length),
                    TelemetryIndex = _activeTelemetryIndex,
                    Frame = frame,
                    GlobalQualityWeight = quality,
                    ActiveTrajectoryBufferId = activeBufferId
                };
                handle = telemetryJob.Schedule(handle);

                _activeScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                _activeHandle = handle;
                _jobScheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _activeHandle);
                JobHandle.ScheduleBatchedJobs();
            }
        }

        /// <summary>Non-blocking late-frame finalization; only completes already-finished work.</summary>
        public static void LateFrameTick()
        {
            TryFinalizeScheduled(forceComplete: false);
        }

        /// <summary>Completes outstanding work only during teardown/simulation barrier ownership.</summary>
        public static void Shutdown()
        {
            TryFinalizeScheduled(forceComplete: true);
            UnlockSolverBuffers();
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
            if (!EnsureInitialized())
                return false;

            NativeArray<BallisticsTuningDTO> buffer = _tuningHandle.Resolve(_vault);
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

            NativeArray<BallisticsTuningDTO> buffer = _tuningHandle.Resolve(_vault);
            if (!buffer.IsCreated || buffer.Length <= 0)
                return false;

            BallisticsTuningDTO sanitized = SanitizeTuning(tuning);
            sanitized.Revision = tuning.Revision + 1u;
            buffer[0] = sanitized;
            return true;
        }

        /// <summary>Cold Burst mock used to profile the solver without player or AI dependencies.</summary>
        public static bool GenerateMockBallistics(int trajectoryCount = 1000, int primitiveCount = 128)
        {
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<BallisticTrajectoryDTO> trajectories = ResolveWriteTrajectories();
            NativeArray<AABBPrimitiveDTO> primitives = _primitiveHandle.Resolve(_vault);
            if (!trajectories.IsCreated || !primitives.IsCreated)
                return false;

            int safeTrajectoryCount = math.clamp(trajectoryCount, 1, math.min(trajectories.Length, MaxTrajectories));
            int safePrimitiveCount = math.clamp(primitiveCount, 1, math.min(primitives.Length, MaxAabbPrimitives));
            BallisticsTuningDTO tuning = ResolveTuning(ResolveGlobalQualityWeight());
            GenerateMockBallisticsJob job = new GenerateMockBallisticsJob
            {
                Trajectories = trajectories,
                Primitives = primitives,
                TrajectoryCount = safeTrajectoryCount,
                PrimitiveCount = safePrimitiveCount,
                GridSpacingMeters = math.max(0.5f, tuning.MockGridSpacingMeters),
                MockOriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                Frame = ++_simulationFrame
            };

            JobHandle handle = job.Schedule(math.max(safeTrajectoryCount, safePrimitiveCount), 64);
            H8Memory.RegisterActiveJob(OwnerSystem, handle);
            handle.Complete(); // COLD SYNC JOB: editor/manual mock injection before profiling solver.
            _pendingTrajectoryCount = safeTrajectoryCount;
            _primitiveCount = safePrimitiveCount;
            return true;
        }

        /// <summary>Cold CSV loader for the 8x8 weapon/material penetration matrix.</summary>
        public static bool TryLoadPenetrationCsv(string csvPath)
        {
            if (!EnsureInitialized() || _jobScheduled || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(_vault);
            NativeArray<float> lut = _penetrationLutHandle.Resolve(_vault);
            if (!scratch.IsCreated || !lut.IsCreated)
                return false;

            int bytesRead = 0;
            using (FileStream stream = File.OpenRead(csvPath))
            {
                while (bytesRead < scratch.Length)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                        break;

                    scratch[bytesRead++] = (byte)value;
                }
            }

            unsafe
            {
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(ptr, bytesRead);
                return ApplyPenetrationCsvBytes(bytes, lut);
            }
        }

        /// <summary>Allocation-free span parser for the 8x8 penetration LUT body.</summary>
        public static bool ApplyPenetrationCsvBytes(ReadOnlySpan<byte> bytes, NativeArray<float> lut)
        {
            if (!lut.IsCreated || lut.Length < PenetrationLutLength || bytes.Length <= 0)
                return false;

            int row = 0;
            int col = 0;
            int tokenStart = 0;
            bool firstTokenInLine = true;
            bool parsedAny = false;
            bool headerLine = false;

            for (int i = 0; i <= bytes.Length; i++)
            {
                byte c = i < bytes.Length ? bytes[i] : (byte)'\n';
                bool delimiter = c == (byte)',' || c == (byte)'\n' || c == (byte)'\r';
                if (!delimiter)
                    continue;

                ReadOnlySpan<byte> token = TrimAscii(bytes.Slice(tokenStart, i - tokenStart));
                tokenStart = i + 1;
                if (c == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
                    tokenStart = i + 2;

                if (token.Length > 0)
                {
                    uint _ = HashFnv1aLower(token);
                    if (firstTokenInLine && !StartsNumeric(token))
                    {
                        firstTokenInLine = false;
                        continue;
                    }

                    if (TryParseFloat(token, out float value))
                    {
                        if (!headerLine && row < 8 && col < 8)
                        {
                            lut[(row * 8) + col] = math.max(0f, value);
                            parsedAny = true;
                            col++;
                        }
                    }
                    else if (row == 0 && !parsedAny)
                    {
                        headerLine = true;
                    }
                }

                if (c == (byte)'\n' || c == (byte)'\r')
                {
                    if (!headerLine && (col > 0 || parsedAny))
                    {
                        row++;
                        if (row >= 8)
                            break;
                    }

                    col = 0;
                    firstTokenInLine = true;
                    headerLine = false;
                }
                else
                {
                    firstTokenInLine = false;
                }
            }

            return parsedAny;
        }

        internal static bool TryGetDebugBuffers(
            out NativeArray<BallisticTrajectoryDTO> trajectories,
            out int trajectoryCount,
            out NativeArray<AABBPrimitiveDTO> primitives,
            out int primitiveCount,
            out NativeArray<BallisticHitResultDTO> hits)
        {
            trajectories = default;
            primitives = default;
            hits = default;
            trajectoryCount = 0;
            primitiveCount = 0;
            if (!EnsureInitialized())
                return false;

            TryFinalizeScheduled(forceComplete: false);
            if (_jobScheduled)
                return false;

            trajectories = ResolveActiveOrWriteTrajectories();
            primitives = _primitiveHandle.Resolve(_vault);
            hits = _hitHandle.Resolve(_vault);
            trajectoryCount = _activeReadCount > 0 ? _activeReadCount : _pendingTrajectoryCount;
            primitiveCount = _primitiveCount;
            return trajectories.IsCreated && primitives.IsCreated && hits.IsCreated;
        }

        public static bool TryGetImpactVfxStaging(
            out NativeArray<BallisticImpactVfxDTO> impactVfx,
            out int stagingCount,
            out uint frame)
        {
            impactVfx = default;
            stagingCount = 0;
            frame = 0u;
            if (!EnsureInitialized())
                return false;

            TryFinalizeScheduled(forceComplete: false);
            if (_jobScheduled)
                return false;

            impactVfx = _impactVfxHandle.Resolve(_vault);
            NativeArray<BallisticsCountersDTO> counters = _counterHandle.Resolve(_vault);
            if (!impactVfx.IsCreated || !counters.IsCreated || counters.Length <= 0)
                return false;

            BallisticsCountersDTO counter = counters[0];
            frame = counter.Frame;
            uint clampedCount = math.min(counter.TrajectoriesProcessed, (uint)int.MaxValue);
            stagingCount = math.min((int)clampedCount, impactVfx.Length);
            return frame != 0u;
        }

        private static void TryFinalizeScheduled(bool forceComplete)
        {
            if (!_jobScheduled)
                return;

            bool completed = forceComplete
                ? DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: true)
                : DispatcherJobSwap.TryFinalizeCompleted(ref _activeHandle);
            if (!completed)
                return;

            _jobScheduled = false;
            UnlockSolverBuffers();
            double elapsedUs =
                (System.Diagnostics.Stopwatch.GetTimestamp() - _activeScheduleTicks) *
                1000000.0d /
                System.Diagnostics.Stopwatch.Frequency;
            RecordCompletedTelemetry(elapsedUs);
        }

        private static void RecordCompletedTelemetry(double elapsedUs)
        {
            NativeArray<BallisticsCountersDTO> counters = _counterHandle.Resolve(_vault);
            NativeArray<BallisticsTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
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
                DumpTelemetry(telemetry);
                _telemetryDumped = true;
            }
        }

        private static void DumpTelemetry(NativeArray<BallisticsTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, "Docs", "AgentLogs", "Dump_BALLISTICS_SURGEON.bin");
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(SourceHash);
                    writer.Write((uint)TelemetryRingLength);
                    int count = math.min(telemetry.Length, TelemetryRingLength);
                    for (int i = 0; i < count; i++)
                    {
                        BallisticsTelemetryEntry entry = telemetry[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.TrajectoriesProcessed);
                        writer.Write(entry.HitCount);
                        writer.Write(entry.RicochetCount);
                        writer.Write(entry.NanGuardCount);
                        writer.Write(entry.Flags);
                        writer.Write(entry.SolveMicroseconds);
                        writer.Write(entry.GlobalQualityWeight);
                        writer.Write(entry.PrimitiveCount);
                        writer.Write(entry.SignalCount);
                        writer.Write(entry.RejectedCount);
                        writer.Write(entry.ActiveTrajectoryBufferId);
                    }
                }
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BallisticsRuntime] Telemetry dump failed: " + ex.GetType().Name);
#endif
            }
        }

        private static void SeedDefaultTuning()
        {
            NativeArray<BallisticsTuningDTO> tuning = _tuningHandle.Resolve(_vault);
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
            NativeArray<float> lut = _penetrationLutHandle.Resolve(_vault);
            if (!lut.IsCreated || lut.Length < PenetrationLutLength)
                return;

            for (int weapon = 0; weapon < 8; weapon++)
            {
                for (int material = 0; material < 8; material++)
                {
                    float weaponScalar = math.lerp(0.55f, 1.4f, weapon / 7f);
                    float materialScalar = 1f - (material * 0.075f);
                    lut[(weapon * 8) + material] = math.max(0.08f, weaponScalar * materialScalar);
                }
            }
        }

        private static void ResetTransientState()
        {
            _activeHandle = default;
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
            _solverBuffersLocked = false;
            _telemetryDumped = false;
            _lastTelemetry = default;
        }

        private static BallisticsTuningDTO ResolveTuning(float quality)
        {
            NativeArray<BallisticsTuningDTO> tuning = _tuningHandle.Resolve(_vault);
            BallisticsTuningDTO value = tuning.IsCreated && tuning.Length > 0 ? tuning[0] : default;
            value = SanitizeTuning(value);
            value.GlobalQualityWeight = quality;
            return value;
        }

        private static BallisticsTuningDTO SanitizeTuning(BallisticsTuningDTO value)
        {
            value.DragCoefficient = math.clamp(math.isfinite(value.DragCoefficient) ? value.DragCoefficient : 0.085f, 0f, 4f);
            value.LethalityThreshold = math.clamp(math.isfinite(value.LethalityThreshold) ? value.LethalityThreshold : 4f, 0.01f, 1000f);
            value.RicochetFriction = math.clamp(math.isfinite(value.RicochetFriction) ? value.RicochetFriction : 0.38f, 0.02f, 0.98f);
            value.RicochetIncidenceThreshold = math.clamp(math.isfinite(value.RicochetIncidenceThreshold) ? value.RicochetIncidenceThreshold : 0.28f, 0.02f, 0.95f);
            value.DamageEnergyScale = math.clamp(math.isfinite(value.DamageEnergyScale) ? value.DamageEnergyScale : 0.045f, 0.0001f, 20f);
            value.MaxRangeMeters = math.clamp(math.isfinite(value.MaxRangeMeters) ? value.MaxRangeMeters : 120f, 0.25f, 2000f);
            value.FloraBaseVelocity = math.clamp(math.isfinite(value.FloraBaseVelocity) ? value.FloraBaseVelocity : 28f, 0.25f, 400f);
            value.FloraSpikeMassKg = math.clamp(math.isfinite(value.FloraSpikeMassKg) ? value.FloraSpikeMassKg : FloraSpikeMassKg, 0.0001f, 10f);
            value.GlobalQualityWeight = math.saturate(math.isfinite(value.GlobalQualityWeight) ? value.GlobalQualityWeight : ResolveGlobalQualityWeight());
            value.LimbAdmissionFloor = math.saturate(math.isfinite(value.LimbAdmissionFloor) ? value.LimbAdmissionFloor : 0.25f);
            value.MockGridSpacingMeters = math.clamp(math.isfinite(value.MockGridSpacingMeters) ? value.MockGridSpacingMeters : 1.4f, 0.1f, 20f);
            return value;
        }

        private static NativeArray<BallisticTrajectoryDTO> ResolveWriteTrajectories()
        {
            return (_writeTrajectoryBufferIndex & 1) == 0
                ? _trajectoryAHandle.Resolve(_vault)
                : _trajectoryBHandle.Resolve(_vault);
        }

        private static NativeArray<BallisticTrajectoryDTO> ResolveActiveOrWriteTrajectories()
        {
            if (_jobScheduled || _activeReadCount > 0)
            {
                return (_activeReadBufferIndex & 1) == 0
                    ? _trajectoryAHandle.Resolve(_vault)
                    : _trajectoryBHandle.Resolve(_vault);
            }

            return ResolveWriteTrajectories();
        }

        private static BufferID ResolveReadBufferId()
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

        private static bool TryLockSolverBuffers(BufferID readBufferId)
        {
            if (_solverBuffersLocked)
                return true;

            bool locked =
                _vault.TryLockBuffer(readBufferId, OwnerSystem) &&
                _vault.TryLockBuffer(BallisticsVaultBufferIds.AabbPrimitives, OwnerSystem) &&
                _vault.TryLockBuffer(BallisticsVaultBufferIds.HitResults, OwnerSystem) &&
                _vault.TryLockBuffer(BallisticsVaultBufferIds.ImpactVfx, OwnerSystem) &&
                _vault.TryLockBuffer(BallisticsVaultBufferIds.PenetrationLut, OwnerSystem) &&
                _vault.TryLockBuffer(BallisticsVaultBufferIds.TelemetryRing, OwnerSystem) &&
                _vault.TryLockBuffer(BallisticsVaultBufferIds.Counters, OwnerSystem);
            if (!locked)
            {
                _vault.TryUnlockBuffer(BallisticsVaultBufferIds.Counters, OwnerSystem);
                _vault.TryUnlockBuffer(BallisticsVaultBufferIds.TelemetryRing, OwnerSystem);
                _vault.TryUnlockBuffer(BallisticsVaultBufferIds.PenetrationLut, OwnerSystem);
                _vault.TryUnlockBuffer(BallisticsVaultBufferIds.ImpactVfx, OwnerSystem);
                _vault.TryUnlockBuffer(BallisticsVaultBufferIds.HitResults, OwnerSystem);
                _vault.TryUnlockBuffer(BallisticsVaultBufferIds.AabbPrimitives, OwnerSystem);
                _vault.TryUnlockBuffer(readBufferId, OwnerSystem);
                return false;
            }

            _solverBuffersLocked = true;
            return true;
        }

        private static void UnlockSolverBuffers()
        {
            if (!_solverBuffersLocked || _vault == null)
                return;

            BufferID activeReadBufferId = (_activeReadBufferIndex & 1) == 0
                ? BallisticsVaultBufferIds.TrajectoriesA
                : BallisticsVaultBufferIds.TrajectoriesB;
            _vault.TryUnlockBuffer(activeReadBufferId, OwnerSystem);
            _vault.TryUnlockBuffer(BallisticsVaultBufferIds.AabbPrimitives, OwnerSystem);
            _vault.TryUnlockBuffer(BallisticsVaultBufferIds.HitResults, OwnerSystem);
            _vault.TryUnlockBuffer(BallisticsVaultBufferIds.ImpactVfx, OwnerSystem);
            _vault.TryUnlockBuffer(BallisticsVaultBufferIds.PenetrationLut, OwnerSystem);
            _vault.TryUnlockBuffer(BallisticsVaultBufferIds.TelemetryRing, OwnerSystem);
            _vault.TryUnlockBuffer(BallisticsVaultBufferIds.Counters, OwnerSystem);
            _solverBuffersLocked = false;
        }

        private static void ClearCounter(NativeArray<BallisticsCountersDTO> counters, uint frame, float quality, uint activeBufferId)
        {
            if (!counters.IsCreated || counters.Length <= 0)
                return;

            BallisticsCountersDTO counter = default;
            counter.Frame = frame;
            counter.GlobalQualityWeight = quality;
            counter.ActiveTrajectoryBufferId = activeBufferId;
            counter.PrimitiveCount = (uint)math.max(0, _primitiveCount);
            counters[0] = counter;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static int ResolveDamageSignalBudget(float quality)
        {
            float smoothed = Smooth01(quality);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            return lengthSq > Epsilon ? value * math.rsqrt(math.max(lengthSq, Epsilon)) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static quaternion NormalizeOrIdentity(quaternion value)
        {
            float lengthSq = math.lengthsq(value.value);
            if (!math.isfinite(lengthSq) || lengthSq <= Epsilon)
                return quaternion.identity;

            value.value *= math.rsqrt(math.max(lengthSq, Epsilon));
            return value;
        }

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

            value = sign * (whole + (fraction / math.max(divisor, 1f)));
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
            if ((uint)index < (uint)PrimitiveCount)
            {
                float spacing = math.max(0.5f, GridSpacingMeters);
                int x = index % 16;
                int y = (index / 16) % 8;
                int z = index / 128;
                AABBPrimitiveDTO primitive = default;
                primitive.CenterAUP = MockOriginAUP + new double3((x - 8) * spacing, (y - 4) * spacing, 16.0 + (z * spacing));
                primitive.HalfExtents = new float3(0.32f, 0.46f, 0.32f);
                primitive.TargetEntityID = (uint)(10000 + index);
                primitive.Rotation = quaternion.AxisAngle(new float3(0f, 1f, 0f), index * 0.03125f);
                primitive.MaterialHash = (uint)(index & 7);
                primitive.PrimitiveHash = math.hash(new uint2((uint)index, Frame));
                primitive.Flags = AABBPrimitiveFlags.Active | AABBPrimitiveFlags.Root | AABBPrimitiveFlags.Mock;
                primitive.DamageMultiplier = 1f;
                primitive.ArmorScalar = 1f;
                Primitives[index] = primitive;
            }

            if ((uint)index < (uint)TrajectoryCount)
            {
                int lane = index % 32;
                float lateral = (lane - 15.5f) * 0.18f;
                BallisticTrajectoryDTO trajectory = default;
                trajectory.OriginAUP = MockOriginAUP + new double3(lateral, -1.2, -14.0);
                trajectory.Direction = math.normalize(new float3(lateral * -0.02f, 0.03f * ((index & 3) - 1), 1f));
                trajectory.Velocity = 84f + ((index & 15) * 0.75f);
                trajectory.Mass = 0.018f;
                trajectory.WeaponHash = BallisticWeaponHashes.MockNeedle + (uint)(index & 7);
                trajectory.SourceEntityID = 0x53484D4Fu;
                trajectory.Flags = BallisticTrajectoryFlags.Mock;
                Trajectories[index] = trajectory;
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
            float velocity = math.max(0f, math.isfinite(trajectory.Velocity) ? trajectory.Velocity : 0f);
            float mass = math.max(0.0001f, math.isfinite(trajectory.Mass) ? trajectory.Mass : 0.0001f);
            if (!math.all(math.isfinite(trajectory.OriginAUP)) || velocity <= 0.0001f)
            {
                result.Flags = BallisticHitFlags.NanGuard;
                return;
            }

            float quality = Smooth01(GlobalQualityWeight);
            int maxRicochets = ResolveRicochetBudget(quality);
            int primitiveCount = math.min(PrimitiveCount, Primitives.Length);
            double3 originAup = trajectory.OriginAUP;
            uint ricochetCount = 0u;
            uint rejectedCount = 0u;

            for (int bounce = 0; bounce <= maxRicochets; bounce++)
            {
                float closestDistance = Tuning.MaxRangeMeters;
                int closestIndex = -1;
                float3 closestLocalHit = float3.zero;
                float3 closestNormal = float3.zero;
                double3 closestHitAup = originAup;

                void* primitivePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Primitives);
                for (int i = 0; i < primitiveCount; i++)
                {
                    ref readonly AABBPrimitiveDTO primitive = ref UnsafeUtility.AsRef<AABBPrimitiveDTO>(
                        (byte*)primitivePtr + (i * UnsafeUtility.SizeOf<AABBPrimitiveDTO>()));
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
                    result.Flags = rejectedCount > 0u ? BallisticHitFlags.LethalityExpired : BallisticHitFlags.None;
                    return;
                }

                float finalVelocity = velocity * math.exp(-math.max(0f, Tuning.DragCoefficient) * closestDistance);
                if (!math.isfinite(finalVelocity) || finalVelocity < Tuning.LethalityThreshold)
                {
                    result.Flags = BallisticHitFlags.LethalityExpired;
                    result.Distance = closestDistance;
                    result.RemainingVelocity = math.max(0f, math.select(0f, finalVelocity, math.isfinite(finalVelocity)));
                    return;
                }

                AABBPrimitiveDTO hitPrimitive = Primitives[closestIndex];
                float penetrationScalar = ResolvePenetrationScalar(trajectory.WeaponHash, hitPrimitive.MaterialHash);
                float incidence = math.abs(math.dot(-direction, BallisticsRuntime.NormalizeOrDefault(closestNormal, new float3(0f, 0f, 1f))));
                bool canRicochet = penetrationScalar < 0.42f &&
                                   incidence < Tuning.RicochetIncidenceThreshold &&
                                   bounce < maxRicochets;
                if (canRicochet)
                {
                    velocity = math.max(0f, finalVelocity * Tuning.RicochetFriction);
                    direction = BallisticsRuntime.NormalizeOrDefault(
                        direction - (2f * math.dot(direction, closestNormal) * closestNormal),
                        direction);
                    originAup = closestHitAup + ((double3)closestNormal * 0.015d);
                    ricochetCount++;
                    continue;
                }

                float kineticEnergy = 0.5f * mass * finalVelocity * finalVelocity;
                float damage = kineticEnergy * math.max(0f, penetrationScalar) * math.max(0f, hitPrimitive.DamageMultiplier) *
                               math.max(0.0001f, hitPrimitive.ArmorScalar) * math.max(0.0001f, Tuning.DamageEnergyScale);
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
                result.Distance = closestDistance;
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

            result.Flags = BallisticHitFlags.LethalityExpired;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldEvaluatePrimitive(in AABBPrimitiveDTO primitive, float quality)
        {
            if ((primitive.Flags & AABBPrimitiveFlags.Root) != 0u)
                return true;

            float admission = math.smoothstep(Tuning.LimbAdmissionFloor, 0.95f, quality);
            float hash01 = ((primitive.PrimitiveHash * 747796405u) + 2891336453u) * (1.0f / 4294967295.0f);
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
            float3 centerFromOrigin = (float3)-deltaAup;
            float radiusSq = math.max(math.lengthsq(half), Epsilon);
            float radius = math.sqrt(radiusSq);
            float projected = math.dot(centerFromOrigin, direction);
            if (!math.isfinite(projected) || projected < -radius || projected > maxDistance + radius)
                return false;

            float clampedProjection = math.clamp(projected, 0f, math.max(0f, maxDistance));
            float3 nearestOnRay = direction * clampedProjection;
            if (math.lengthsq(centerFromOrigin - nearestOnRay) > radiusSq)
                return false;

            quaternion inverseRotation = math.inverse(BallisticsRuntime.NormalizeOrIdentity(primitive.Rotation));
            float3 localOrigin = math.mul(inverseRotation, (float3)deltaAup);
            float3 localDirection = BallisticsRuntime.NormalizeOrDefault(math.mul(inverseRotation, direction), new float3(0f, 0f, 1f));
            if (!TryIntersectAabbSlab(localOrigin, localDirection, half, maxDistance, out distance, out localHit, out float3 localNormal))
                return false;

            worldNormal = BallisticsRuntime.NormalizeOrDefault(math.mul(BallisticsRuntime.NormalizeOrIdentity(primitive.Rotation), localNormal), new float3(0f, 0f, 1f));
            float3 relativeWorldHit = math.mul(BallisticsRuntime.NormalizeOrIdentity(primitive.Rotation), localHit);
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
            float3 invDir = 1f / safeDirection;
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
        private float ResolvePenetrationScalar(uint weaponHash, uint materialHash)
        {
            uint weaponClass = weaponHash & 7u;
            uint materialClass = materialHash & 7u;
            int index = (int)((weaponClass * 8u) + materialClass);
            if ((uint)index >= (uint)PenetrationLut.Length)
                return 0.25f;

            float value = PenetrationLut[index];
            return math.max(0f, math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 1f);
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveRicochetBudget(float quality)
        {
            return math.clamp((int)math.floor(math.lerp(0.05f, 3.95f, quality)), 0, 3);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EmitBallisticDamageSignalsJob : IJob
    {
        [NoAlias] public NativeArray<BallisticHitResultDTO> HitResults;
        public NativeQueue<CombatDamageSignal>.ParallelWriter DamageWriter;
        public int HitCount;
        public int SignalEmitBudget;
        public uint Frame;

        public void Execute()
        {
            int count = math.min(math.max(0, HitCount), HitResults.Length);
            int budget = math.max(0, SignalEmitBudget);
            int emitted = 0;

            for (int i = 0; i < count; i++)
            {
                BallisticHitResultDTO hit = HitResults[i];
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
                    DamageWriter.Enqueue(signal);
                    emitted++;
                    continue;
                }

                hit.Flags |= BallisticHitFlags.SignalDropped;
                HitResults[i] = hit;
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

            BallisticHitResultDTO hit = HitResults[index];
            BallisticImpactVfxDTO vfx = default;
            if ((hit.Flags & BallisticHitFlags.Hit) == 0u)
            {
                ImpactVfx[index] = vfx;
                return;
            }

            float3 up = BallisticsRuntime.NormalizeOrDefault(hit.Normal, new float3(0f, 0f, 1f));
            float3 fallback = math.abs(up.y) < 0.95f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            float3 right = BallisticsRuntime.NormalizeOrDefault(math.cross(fallback, up), new float3(1f, 0f, 0f));
            float3 forward = BallisticsRuntime.NormalizeOrDefault(math.cross(up, right), new float3(0f, 0f, 1f));
            float scale = math.lerp(0.035f, 0.11f, math.saturate(GlobalQualityWeight));
            double3 runtimeAup = hit.HitAUP - PresentationOriginAUP;
            float3 runtimeHit = math.all(math.isfinite(runtimeAup))
                ? (float3)runtimeAup
                : hit.LocalHitPoint;
            vfx.Matrix = new float4x4(
                new float4(right * scale, 0f),
                new float4(up * scale, 0f),
                new float4(forward * scale, 0f),
                new float4(runtimeHit, 1f));
            vfx.MaterialHash = hit.MaterialHash;
            vfx.TargetEntityID = hit.TargetEntityID;
            vfx.Flags = hit.Flags;
            vfx.Frame = Frame;
            ImpactVfx[index] = vfx;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BallisticsTelemetryJob : IJob
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
            counters.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
            counters.ActiveTrajectoryBufferId = ActiveTrajectoryBufferId;

            int count = math.min(TrajectoryCount, HitResults.Length);
            for (int i = 0; i < count; i++)
            {
                BallisticHitResultDTO hit = HitResults[i];
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
                TelemetryRing[TelemetryIndex] = entry;
            }

            if (Counters.IsCreated && Counters.Length > 0)
                Counters[0] = counters;
        }
    }
}
