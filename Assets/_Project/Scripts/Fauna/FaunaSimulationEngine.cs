using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct FaunaParasiteAttachInput
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit128 HostAup;
        [FieldOffset(48)] public float3 HostLocalAttachOffset;
        [FieldOffset(60)] public float HostHealth;
        [FieldOffset(64)] public float ParasiteHunger01;
        [FieldOffset(68)] public float DrainPerSecond;
        [FieldOffset(72)] public float DeltaTimeSeconds;
        [FieldOffset(76)] public byte Attached;
        [FieldOffset(77)] private byte _pad0;
        [FieldOffset(78)] private ushort _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct FaunaParasiteAttachResult
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit128 ParasiteAup;
        [FieldOffset(48)] public float HostHealth;
        [FieldOffset(52)] public float ParasiteHunger01;
        [FieldOffset(56)] public byte Attached;
        [FieldOffset(57)] private byte _pad0;
        [FieldOffset(58)] private ushort _pad1;
        [FieldOffset(60)] private uint _pad2;
    }

    /// <summary>
    /// Data-only fauna simulation service. Owns Burst job scheduling and keeps visual/GameObject logic out of LOD math.
    /// </summary>
    public sealed class FaunaSimulationEngine : IFaunaSim, IServiceHeartbeat, IServiceShutdown
    {
        private FaunaDirector _owner;

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public int ResidentSlotCapacity { get; private set; }

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => IsReady;

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            if (_owner != null)
            {
                _owner.OnServiceShutdown();
                return;
            }

            Shutdown();
        }

        internal void BindOwner(FaunaDirector owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Marks the service ready after residency buffers have been allocated by the owning director.
        /// </summary>
        public void Initialize(int residentSlotCapacity)
        {
            ResidentSlotCapacity = math.max(0, residentSlotCapacity);
            IsReady = ResidentSlotCapacity > 0;
        }

        /// <summary>
        /// Clears registry-facing state before the owning director disposes its native buffers.
        /// </summary>
        public void Shutdown()
        {
            IsReady = false;
            ResidentSlotCapacity = 0;
        }

        /// <summary>
        /// Clears transient service readiness while preserving the resident slot contract owned by the director.
        /// </summary>
        public void Reset()
        {
            int residentSlotCapacity = ResidentSlotCapacity;
            Shutdown();
            Initialize(residentSlotCapacity);
        }

        internal JobHandle ScheduleResidentDataOnlyLod(
            NativeArray<PoolSlotData> poolSlots,
            NativeArray<float3> linearVelocities,
            NativeArray<byte> simulationFlags,
            in AbsoluteUniversePosition playerAup,
            float deltaTime,
            double dehydrationDistanceSq,
            double hibernationDistanceSq,
            byte residentSimulationFlag,
            byte dehydratedSimulationFlag)
        {
            DataOnlyFaunaLodJob job = new DataOnlyFaunaLodJob
            {
                PoolSlots = poolSlots,
                LinearVelocities = linearVelocities,
                SimulationFlags = simulationFlags,
                PlayerAbsolutePosition = AUPMath.ToAbsoluteDouble3(in playerAup),
                DeltaTime = deltaTime,
                DehydrationDistanceSq = dehydrationDistanceSq,
                HibernationDistanceSq = hibernationDistanceSq,
                ResidentSimulationFlag = residentSimulationFlag,
                DehydratedSimulationFlag = dehydratedSimulationFlag
            };

            return job.Schedule(poolSlots.Length, 32);
        }

        internal JobHandle ScheduleParasiteAttach(
            NativeArray<FaunaParasiteAttachInput> inputs,
            NativeArray<FaunaParasiteAttachResult> results,
            int count,
            JobHandle dependency = default)
        {
            int safeCount = math.min(math.max(0, count), math.min(inputs.Length, results.Length));
            if (safeCount <= 0)
                return dependency;

            ParasiteAttachJob job = new ParasiteAttachJob
            {
                Inputs = inputs,
                Results = results
            };

            return job.Schedule(safeCount, 32, dependency);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct DataOnlyFaunaLodJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<PoolSlotData> PoolSlots;
            [NoAlias] public NativeArray<float3> LinearVelocities;
            [ReadOnly, NoAlias] public NativeArray<byte> SimulationFlags;
            public double3 PlayerAbsolutePosition;
            public float DeltaTime;
            public double DehydrationDistanceSq;
            public double HibernationDistanceSq;
            public byte ResidentSimulationFlag;
            public byte DehydratedSimulationFlag;

            public void Execute(int index)
            {
                byte flags = SimulationFlags[index];
                if ((flags & (ResidentSimulationFlag | DehydratedSimulationFlag)) != (ResidentSimulationFlag | DehydratedSimulationFlag))
                    return;

                if (!IsFinite(PlayerAbsolutePosition) ||
                    !math.isfinite(DehydrationDistanceSq) ||
                    !math.isfinite(HibernationDistanceSq))
                {
                    return;
                }

                PoolSlotData slotData = PoolSlots[index];
                double3 positionAbsolute = ToAbsolutePosition(slotData);
                if (!IsFinite(positionAbsolute))
                {
                    LinearVelocities[index] = float3.zero;
                    return;
                }

                double3 delta = positionAbsolute - PlayerAbsolutePosition;
                double distanceSq = math.dot(delta, delta);
                if (!math.isfinite(distanceSq) ||
                    distanceSq <= DehydrationDistanceSq ||
                    distanceSq > HibernationDistanceSq)
                    return;

                float step = math.isfinite(DeltaTime) ? math.max(0f, DeltaTime) : 0f;
                if (step <= 0f)
                    return;

                float3 cachedVelocity = LinearVelocities[index];
                if (!IsFinite(cachedVelocity))
                {
                    LinearVelocities[index] = float3.zero;
                    return;
                }

                float3 nextVelocity = cachedVelocity * math.saturate(1f - (step * 0.12f));
                if (!IsFinite(nextVelocity))
                    nextVelocity = float3.zero;

                double3 nextAbsolute = positionAbsolute + (double3)(nextVelocity * step);
                if (!IsFinite(nextAbsolute))
                {
                    LinearVelocities[index] = float3.zero;
                    return;
                }

                LinearVelocities[index] = nextVelocity;
                WriteAbsolutePosition(ref slotData, nextAbsolute);
                PoolSlots[index] = slotData;
            }

            private static double3 ToAbsolutePosition(PoolSlotData slotData)
            {
                const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                return new double3(
                    (slotData.GridX * cellSize) + slotData.LocalOffset.x,
                    (slotData.GridY * cellSize) + slotData.LocalOffset.y,
                    (slotData.GridZ * cellSize) + slotData.LocalOffset.z);
            }

            private static void WriteAbsolutePosition(ref PoolSlotData slotData, double3 absolutePosition)
            {
                if (!IsFinite(absolutePosition))
                    return;

                const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                long gridX = (long)math.floor(absolutePosition.x / cellSize);
                long gridY = (long)math.floor(absolutePosition.y / cellSize);
                long gridZ = (long)math.floor(absolutePosition.z / cellSize);

                slotData.GridX = gridX;
                slotData.GridY = gridY;
                slotData.GridZ = gridZ;
                slotData.LocalOffset = new float3(
                    (float)(absolutePosition.x - (gridX * cellSize)),
                    (float)(absolutePosition.y - (gridY * cellSize)),
                    (float)(absolutePosition.z - (gridZ * cellSize)));
            }

            private static bool IsFinite(float3 value)
            {
                return math.isfinite(value.x) &&
                       math.isfinite(value.y) &&
                       math.isfinite(value.z);
            }

            private static bool IsFinite(double3 value)
            {
                return math.isfinite(value.x) &&
                       math.isfinite(value.y) &&
                       math.isfinite(value.z);
            }

            private static int ClampToInt(long value)
            {
                if (value < int.MinValue)
                    return int.MinValue;

                if (value > int.MaxValue)
                    return int.MaxValue;

                return (int)value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ParasiteAttachJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<FaunaParasiteAttachInput> Inputs;
            [NoAlias] public NativeArray<FaunaParasiteAttachResult> Results;

            public void Execute(int index)
            {
                FaunaParasiteAttachInput input = Inputs[index];
                FaunaParasiteAttachResult result = default;
                result.Attached = input.Attached;
                result.HostHealth = math.max(0f, input.HostHealth);
                result.ParasiteHunger01 = math.saturate(input.ParasiteHunger01);
                result.ParasiteAup = input.HostAup;
                if (input.Attached == 0)
                {
                    Results[index] = result;
                    return;
                }

                float drain = math.max(0f, input.DrainPerSecond) * math.max(0f, input.DeltaTimeSeconds);
                float appliedDrain = math.min(result.HostHealth, drain);
                result.HostHealth = math.max(0f, result.HostHealth - appliedDrain);
                result.ParasiteHunger01 = math.saturate(result.ParasiteHunger01 - appliedDrain);
                double3 parasiteAbsolute = ToAbsoluteDouble3(input.HostAup) + (double3)input.HostLocalAttachOffset;
                result.ParasiteAup = ToAup(parasiteAbsolute);
                Results[index] = result;
            }

            private static double3 ToAbsoluteDouble3(AbsoluteUniversePositionBlit128 position)
            {
                const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                return new double3(
                    (position.GridX * cellSize) + position.Local.x,
                    (position.GridY * cellSize) + position.Local.y,
                    (position.GridZ * cellSize) + position.Local.z);
            }

            private static AbsoluteUniversePositionBlit128 ToAup(double3 absolutePosition)
            {
                const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                long gridX = (long)math.floor(absolutePosition.x / cellSize);
                long gridY = (long)math.floor(absolutePosition.y / cellSize);
                long gridZ = (long)math.floor(absolutePosition.z / cellSize);
                return new AbsoluteUniversePositionBlit128
                {
                    GridX = gridX,
                    GridY = gridY,
                    GridZ = gridZ,
                    Local = new float4(
                        (float)(absolutePosition.x - (gridX * cellSize)),
                        (float)(absolutePosition.y - (gridY * cellSize)),
                        (float)(absolutePosition.z - (gridZ * cellSize)),
                        0f)
                };
            }
        }
    }

    internal static class FaunaVaultBufferRoutes
    {
        private static VaultGenerationHandle<T> OpenOrAcquire<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsCompactionFenceActive)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                owner,
                options);

            return TryOpen(vault, in handle, bufferId, owner, requiredLength, out buffer) ? handle : default;
        }

        private static bool TryOpen<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength < 0 ||
                !IsOwnedVaultHandle(in handle, bufferId, owner))
            {
                return false;
            }

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static void Release<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner)
            where T : struct
        {
            if (vault != null && IsOwnedVaultHandle(in handle, bufferId, owner))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)owner;
        }

    }

    /// <summary>
    /// GlobalDataVault-backed fauna residency memory facade.
    /// </summary>
    internal struct FaunaSimulationMemory : IDisposable
    {
        private const ulong FaunaSimulationMutationGuardMask =
            (1UL << ((int)BufferID.FaunaSimulationPoolSlots & 31)) |
            (1UL << ((int)BufferID.FaunaSimulationLinearVelocities & 31)) |
            (1UL << ((int)BufferID.FaunaSimulationFlags & 31)) |
            (1UL << ((int)BufferID.FaunaSimulationFreeSlots & 31));

        private IDataVault _vault;
        private VaultGenerationHandle<PoolSlotData> _poolSlotsHandle;
        private VaultGenerationHandle<float3> _linearVelocitiesHandle;
        private VaultGenerationHandle<byte> _simulationFlagsHandle;
        private IDataVault _mutationGuardVault;
        private bool _mutationGuardHeld;
        private bool _scheduledDataOnlyLodGuardHeld;

        public FaunaSimulationFreeSlotStack FreeSlots;
        public int Capacity;

        public bool IsCreated =>
            HasResidentBuffers &&
            HasFreeSlots;

        public bool HasResidentBuffers
        {
            get
            {
                if (!TryEnterMutationGuard(out _, out bool acquiredGuard, allowScheduledOwner: true))
                    return false;

                try
                {
                    return TryResolvePoolSlots(out NativeArray<PoolSlotData> poolSlots) &&
                           TryResolveLinearVelocities(out NativeArray<float3> linearVelocities) &&
                           TryResolveSimulationFlags(out NativeArray<byte> simulationFlags) &&
                           poolSlots.IsCreated &&
                           linearVelocities.IsCreated &&
                           simulationFlags.IsCreated;
                }
                finally
                {
                    ReleaseMutationGuard(acquiredGuard);
                }
            }
        }

        public bool HasFreeSlots
        {
            get
            {
                if (!TryEnterMutationGuard(out _, out bool acquiredGuard, allowScheduledOwner: true))
                    return false;

                try
                {
                    return FreeSlots.IsCreated;
                }
                finally
                {
                    ReleaseMutationGuard(acquiredGuard);
                }
            }
        }

        public bool HasPoolSlot(int index)
        {
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard, allowScheduledOwner: true))
                return false;

            try
            {
                return TryResolvePoolSlots(out NativeArray<PoolSlotData> poolSlots) &&
                       (uint)index < (uint)poolSlots.Length;
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryReadPoolSlot(int index, out PoolSlotData slotData)
        {
            slotData = default;
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return false;

            try
            {
                NativeArray<PoolSlotData> poolSlots = ResolvePoolSlots();
                if (!poolSlots.IsCreated || (uint)index >= (uint)poolSlots.Length)
                    return false;

                slotData = poolSlots[index];
                return true;
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryWritePoolSlot(int index, in PoolSlotData slotData)
        {
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return false;

            try
            {
                NativeArray<PoolSlotData> poolSlots = ResolvePoolSlots();
                if (!poolSlots.IsCreated || (uint)index >= (uint)poolSlots.Length)
                    return false;

                poolSlots[index] = slotData;
                return true;
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryReadLinearVelocity(int index, out float3 velocity)
        {
            velocity = default;
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return false;

            try
            {
                NativeArray<float3> linearVelocities = ResolveLinearVelocities();
                if (!linearVelocities.IsCreated || (uint)index >= (uint)linearVelocities.Length)
                    return false;

                velocity = linearVelocities[index];
                return true;
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryWriteLinearVelocity(int index, float3 velocity)
        {
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return false;

            try
            {
                NativeArray<float3> linearVelocities = ResolveLinearVelocities();
                if (!linearVelocities.IsCreated || (uint)index >= (uint)linearVelocities.Length)
                    return false;

                linearVelocities[index] = velocity;
                return true;
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryWriteSimulationFlag(int index, byte flag)
        {
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return false;

            try
            {
                NativeArray<byte> simulationFlags = ResolveSimulationFlags();
                if (!simulationFlags.IsCreated || (uint)index >= (uint)simulationFlags.Length)
                    return false;

                simulationFlags[index] = flag;
                return true;
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryClearSlot(int index)
        {
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return false;

            try
            {
                bool wroteAny = false;
                NativeArray<PoolSlotData> poolSlots = ResolvePoolSlots();
                if (poolSlots.IsCreated && (uint)index < (uint)poolSlots.Length)
                {
                    poolSlots[index] = default;
                    wroteAny = true;
                }

                NativeArray<float3> linearVelocities = ResolveLinearVelocities();
                if (linearVelocities.IsCreated && (uint)index < (uint)linearVelocities.Length)
                {
                    linearVelocities[index] = default;
                    wroteAny = true;
                }

                NativeArray<byte> simulationFlags = ResolveSimulationFlags();
                if (simulationFlags.IsCreated && (uint)index < (uint)simulationFlags.Length)
                {
                    simulationFlags[index] = 0;
                    wroteAny = true;
                }

                return wroteAny;
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryDequeueFreeSlot(out int slotIndex)
        {
            slotIndex = -1;
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return false;

            try
            {
                return FreeSlots.IsCreated && FreeSlots.TryDequeue(out slotIndex);
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public void EnqueueFreeSlot(int slotIndex)
        {
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return;

            try
            {
                if (FreeSlots.IsCreated)
                    FreeSlots.Enqueue(slotIndex);
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public bool TryScheduleResidentDataOnlyLod(
            FaunaSimulationEngine engine,
            in AbsoluteUniversePosition playerAup,
            float deltaTime,
            double dehydrationDistanceSq,
            double hibernationDistanceSq,
            byte residentSimulationFlag,
            byte dehydratedSimulationFlag,
            out JobHandle handle)
        {
            handle = default;
            if (engine == null || _scheduledDataOnlyLodGuardHeld)
                return false;

            if (!TryAcquireRetainedMutationGuard())
            {
                return false;
            }

            bool retainGuard = false;
            try
            {
                if (!TryResolvePoolSlots(out NativeArray<PoolSlotData> poolSlots) ||
                    !TryResolveLinearVelocities(out NativeArray<float3> linearVelocities) ||
                    !TryResolveSimulationFlags(out NativeArray<byte> simulationFlags))
                {
                    return false;
                }

                handle = engine.ScheduleResidentDataOnlyLod(
                    poolSlots,
                    linearVelocities,
                    simulationFlags,
                    in playerAup,
                    deltaTime,
                    dehydrationDistanceSq,
                    hibernationDistanceSq,
                    residentSimulationFlag,
                    dehydratedSimulationFlag);
                _scheduledDataOnlyLodGuardHeld = true;
                retainGuard = true;
                return true;
            }
            finally
            {
                if (!retainGuard)
                    ReleaseRetainedMutationGuard();
            }
        }

        public void Allocate(int capacity)
        {
            Dispose();
            Capacity = math.max(0, capacity);
            if (Capacity <= 0)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsAllocationLocked)
            {
                Capacity = 0;
                return;
            }

            _vault = vault;
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
            {
                _vault = null;
                Capacity = 0;
                return;
            }

            try
            {
                _poolSlotsHandle = OpenOrAcquireVaultBuffer(
                    vault,
                    BufferID.FaunaSimulationPoolSlots,
                    Capacity,
                    SystemID.AICognition,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<PoolSlotData> poolSlots);
                _linearVelocitiesHandle = OpenOrAcquireVaultBuffer(
                    vault,
                    BufferID.FaunaSimulationLinearVelocities,
                    Capacity,
                    SystemID.AICognition,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<float3> linearVelocities);
                _simulationFlagsHandle = OpenOrAcquireVaultBuffer(
                    vault,
                    BufferID.FaunaSimulationFlags,
                    Capacity,
                    SystemID.AICognition,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<byte> simulationFlags);

                if (!poolSlots.IsCreated ||
                    !linearVelocities.IsCreated ||
                    !simulationFlags.IsCreated ||
                    poolSlots.Length < Capacity ||
                    linearVelocities.Length < Capacity ||
                    simulationFlags.Length < Capacity)
                {
                    ReleaseVaultAliases();
                    return;
                }

                ClearArrays(poolSlots, linearVelocities, simulationFlags);
                FreeSlots.Allocate(
                    vault,
                    Capacity,
                    BufferID.FaunaSimulationFreeSlots,
                    SystemID.AICognition);
                if (!FreeSlots.IsCreated)
                    ReleaseVaultAliases();
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public void Reset()
        {
            if (!TryEnterMutationGuard(out _, out bool acquiredGuard))
                return;

            try
            {
                NativeArray<PoolSlotData> poolSlots = ResolvePoolSlots();
                NativeArray<float3> linearVelocities = ResolveLinearVelocities();
                NativeArray<byte> simulationFlags = ResolveSimulationFlags();
                ClearArrays(poolSlots, linearVelocities, simulationFlags);
                FreeSlots.Reset();
            }
            finally
            {
                ReleaseMutationGuard(acquiredGuard);
            }
        }

        public void Dispose()
        {
            ReleaseVaultAliases();
        }

        public void Dispose(JobHandle dependency)
        {
            DispatcherJobFence.TryComplete(ref dependency, forceComplete: true);
            ReleaseVaultAliases();
        }

        public void ReleaseScheduledDataOnlyLodGuard()
        {
            _scheduledDataOnlyLodGuardHeld = false;
            ReleaseRetainedMutationGuard();
        }

        private void ReleaseVaultAliases()
        {
            if (!_mutationGuardHeld && !_scheduledDataOnlyLodGuardHeld)
                TryEnterMutationGuard(out _, out _);

            IDataVault vault = _vault;
            _scheduledDataOnlyLodGuardHeld = false;
            try
            {
                ReleaseVaultBuffer(vault, ref _poolSlotsHandle, BufferID.FaunaSimulationPoolSlots, SystemID.AICognition);
                ReleaseVaultBuffer(vault, ref _linearVelocitiesHandle, BufferID.FaunaSimulationLinearVelocities, SystemID.AICognition);
                ReleaseVaultBuffer(vault, ref _simulationFlagsHandle, BufferID.FaunaSimulationFlags, SystemID.AICognition);
                FreeSlots.Dispose();
            }
            finally
            {
                _vault = null;
                Capacity = 0;
                ReleaseRetainedMutationGuard();
            }
        }

        private bool TryEnterMutationGuard(out IDataVault vault, out bool acquired, bool allowScheduledOwner = false)
        {
            acquired = false;
            vault = _vault;
            if (vault == null || (_scheduledDataOnlyLodGuardHeld && !allowScheduledOwner))
                return false;

            if (_mutationGuardHeld)
                return true;

            if (!vault.TryAcquireMutationGuard(FaunaSimulationMutationGuardMask))
            {
                vault = null;
                return false;
            }

            _mutationGuardVault = vault;
            _mutationGuardHeld = true;
            acquired = true;
            return true;
        }

        private bool TryAcquireRetainedMutationGuard()
        {
            IDataVault vault = _vault;
            if (vault == null || _mutationGuardHeld || _scheduledDataOnlyLodGuardHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(FaunaSimulationMutationGuardMask))
                return false;

            _mutationGuardVault = vault;
            _mutationGuardHeld = true;
            return true;
        }

        private void ReleaseMutationGuard(bool acquired)
        {
            if (acquired)
                ReleaseRetainedMutationGuard();
        }

        private void ReleaseRetainedMutationGuard()
        {
            if (!_mutationGuardHeld)
                return;

            IDataVault vault = _mutationGuardVault;
            _mutationGuardHeld = false;
            _mutationGuardVault = null;
            vault?.ReleaseMutationGuard(FaunaSimulationMutationGuardMask);
        }

        private static VaultGenerationHandle<T> OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsCompactionFenceActive)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                owner,
                options);

            return TryOpenVaultBuffer(vault, in handle, bufferId, owner, requiredLength, out buffer) ? handle : default;
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength < 0 ||
                !IsOwnedVaultHandle(in handle, bufferId, owner))
            {
                return false;
            }

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner)
            where T : struct
        {
            if (vault != null && IsOwnedVaultHandle(in handle, bufferId, owner))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)owner;
        }

        private NativeArray<PoolSlotData> ResolvePoolSlots()
        {
            return TryResolvePoolSlots(out NativeArray<PoolSlotData> buffer) ? buffer : default;
        }

        private NativeArray<float3> ResolveLinearVelocities()
        {
            return TryResolveLinearVelocities(out NativeArray<float3> buffer) ? buffer : default;
        }

        private NativeArray<byte> ResolveSimulationFlags()
        {
            return TryResolveSimulationFlags(out NativeArray<byte> buffer) ? buffer : default;
        }

        private bool TryResolvePoolSlots(out NativeArray<PoolSlotData> buffer)
        {
            return TryOpenVaultBuffer(
                _vault,
                in _poolSlotsHandle,
                BufferID.FaunaSimulationPoolSlots,
                SystemID.AICognition,
                Capacity,
                out buffer);
        }

        private bool TryResolveLinearVelocities(out NativeArray<float3> buffer)
        {
            return TryOpenVaultBuffer(
                _vault,
                in _linearVelocitiesHandle,
                BufferID.FaunaSimulationLinearVelocities,
                SystemID.AICognition,
                Capacity,
                out buffer);
        }

        private bool TryResolveSimulationFlags(out NativeArray<byte> buffer)
        {
            return TryOpenVaultBuffer(
                _vault,
                in _simulationFlagsHandle,
                BufferID.FaunaSimulationFlags,
                SystemID.AICognition,
                Capacity,
                out buffer);
        }

        private static void ClearArrays(
            NativeArray<PoolSlotData> poolSlots,
            NativeArray<float3> linearVelocities,
            NativeArray<byte> simulationFlags)
        {
            if (poolSlots.IsCreated)
            {
                for (int i = 0; i < poolSlots.Length; i++)
                    poolSlots[i] = default;
            }

            if (linearVelocities.IsCreated)
            {
                for (int i = 0; i < linearVelocities.Length; i++)
                    linearVelocities[i] = default;
            }

            if (simulationFlags.IsCreated)
            {
                for (int i = 0; i < simulationFlags.Length; i++)
                    simulationFlags[i] = 0;
            }
        }
    }

    /// <summary>
    /// Fixed-capacity free-slot stack backed by GlobalDataVault.
    /// </summary>
    internal struct FaunaSimulationFreeSlotStack
    {
        private IDataVault _vault;
        private VaultGenerationHandle<int> _slotsHandle;
        private BufferID _bufferId;
        private SystemID _owner;
        private int _count;
        private int _capacity;

        public bool IsCreated =>
            _capacity > 0 &&
            TryOpenVaultBuffer(_vault, in _slotsHandle, _bufferId, _owner, _capacity, out NativeArray<int> slots) &&
            slots.IsCreated;

        public void Allocate(IDataVault vault, int capacity, BufferID bufferId, SystemID owner)
        {
            Dispose();
            _vault = vault;
            _bufferId = bufferId;
            _owner = owner;
            _capacity = math.max(0, capacity);
            if (_vault == null || _capacity <= 0)
            {
                Dispose();
                return;
            }

            _slotsHandle = OpenOrAcquireVaultBuffer(
                _vault,
                bufferId,
                _capacity,
                owner,
                NativeArrayOptions.ClearMemory,
                out NativeArray<int> slots);

            if (!slots.IsCreated)
            {
                Dispose();
                return;
            }

            Reset();
        }

        public void Reset()
        {
            Clear();
            NativeArray<int> slots = ResolveSlots(_vault, in _slotsHandle, _bufferId, _owner, _capacity);
            if (!slots.IsCreated)
                return;

            int capacity = math.min(_capacity, slots.Length);
            for (int i = capacity - 1; i >= 0; i--)
            {
                slots[_count] = i;
                _count++;
            }
        }

        public void Clear()
        {
            _count = 0;
        }

        public bool TryDequeue(out int slotIndex)
        {
            slotIndex = -1;
            NativeArray<int> slots = ResolveSlots(_vault, in _slotsHandle, _bufferId, _owner, _capacity);
            if (!slots.IsCreated || _count <= 0)
                return false;

            _count--;
            slotIndex = slots[_count];
            if ((uint)slotIndex >= (uint)_capacity)
            {
                slotIndex = -1;
                return false;
            }

            return true;
        }

        public void Enqueue(int slotIndex)
        {
            NativeArray<int> slots = ResolveSlots(_vault, in _slotsHandle, _bufferId, _owner, _capacity);
            if (!slots.IsCreated ||
                (uint)slotIndex >= (uint)_capacity ||
                _count >= _capacity ||
                _count >= slots.Length)
            {
                return;
            }

            slots[_count] = slotIndex;
            _count++;
        }

        public void Dispose()
        {
            ReleaseVaultBuffer(_vault, ref _slotsHandle, _bufferId, _owner);
            _vault = null;
            _bufferId = default;
            _owner = default;
            _count = 0;
            _capacity = 0;
        }

        private static NativeArray<int> ResolveSlots(
            IDataVault vault,
            in VaultGenerationHandle<int> slotsHandle,
            BufferID bufferId,
            SystemID owner,
            int capacity)
        {
            return TryOpenVaultBuffer(vault, in slotsHandle, bufferId, owner, capacity, out NativeArray<int> slots)
                ? slots
                : default;
        }

        private static VaultGenerationHandle<T> OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsCompactionFenceActive)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                owner,
                options);

            return TryOpenVaultBuffer(vault, in handle, bufferId, owner, requiredLength, out buffer) ? handle : default;
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength < 0 ||
                !IsOwnedVaultHandle(in handle, bufferId, owner))
            {
                return false;
            }

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner)
            where T : struct
        {
            if (vault != null && IsOwnedVaultHandle(in handle, bufferId, owner))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)owner;
        }
    }
}
