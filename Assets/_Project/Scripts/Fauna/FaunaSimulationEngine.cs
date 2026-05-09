using System;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI
{
    internal struct FaunaParasiteAttachInput
    {
        public AbsoluteUniversePositionBlit128 HostAup;
        public float3 HostLocalAttachOffset;
        public float HostHealth;
        public float ParasiteHunger01;
        public float DrainPerSecond;
        public float DeltaTimeSeconds;
        public byte Attached;
    }

    internal struct FaunaParasiteAttachResult
    {
        public AbsoluteUniversePositionBlit128 ParasiteAup;
        public float HostHealth;
        public float ParasiteHunger01;
        public byte Attached;
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DataOnlyFaunaLodJob : IJobParallelFor
        {
            public NativeArray<PoolSlotData> PoolSlots;
            public NativeArray<float3> LinearVelocities;
            [ReadOnly] public NativeArray<byte> SimulationFlags;
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

                PoolSlotData slotData = PoolSlots[index];
                double3 positionAbsolute = ToAbsolutePosition(slotData);
                double3 delta = positionAbsolute - PlayerAbsolutePosition;
                double distanceSq = math.dot(delta, delta);
                if (distanceSq <= DehydrationDistanceSq || distanceSq > HibernationDistanceSq)
                    return;

                float step = math.max(0f, DeltaTime);
                float3 nextVelocity = LinearVelocities[index] * math.saturate(1f - (step * 0.12f));
                positionAbsolute += (double3)(nextVelocity * step);
                LinearVelocities[index] = nextVelocity;
                WriteAbsolutePosition(ref slotData, positionAbsolute);
                PoolSlots[index] = slotData;
            }

            private static double3 ToAbsolutePosition(PoolSlotData slotData)
            {
                const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                return new double3(
                    (slotData.AupCell.x * cellSize) + slotData.LocalOffset.x,
                    (slotData.AupCell.y * cellSize) + slotData.LocalOffset.y,
                    (slotData.AupCell.z * cellSize) + slotData.LocalOffset.z);
            }

            private static void WriteAbsolutePosition(ref PoolSlotData slotData, double3 absolutePosition)
            {
                const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                long gridX = (long)math.floor(absolutePosition.x / cellSize);
                long gridY = (long)math.floor(absolutePosition.y / cellSize);
                long gridZ = (long)math.floor(absolutePosition.z / cellSize);

                slotData.AupCell = new int3((int)gridX, (int)gridY, (int)gridZ);
                slotData.LocalOffset = new float3(
                    (float)(absolutePosition.x - (gridX * cellSize)),
                    (float)(absolutePosition.y - (gridY * cellSize)),
                    (float)(absolutePosition.z - (gridZ * cellSize)));
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ParasiteAttachJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<FaunaParasiteAttachInput> Inputs;
            public NativeArray<FaunaParasiteAttachResult> Results;

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

    /// <summary>
    /// IDisposable owner for fauna residency native memory.
    /// </summary>
    internal struct FaunaSimulationMemory : IDisposable
    {
        public NativeArray<PoolSlotData> PoolSlots;
        public NativeArray<float3> LinearVelocities;
        public NativeArray<byte> SimulationFlags;
        public NativeQueue<int> FreeSlots;
        public int Capacity;

        public bool IsCreated =>
            PoolSlots.IsCreated &&
            LinearVelocities.IsCreated &&
            SimulationFlags.IsCreated &&
            FreeSlots.IsCreated;

        public void Allocate(int capacity)
        {
            Dispose();
            Capacity = math.max(0, capacity);
            if (Capacity <= 0)
                return;

            // COLD ALLOC: NativeArray<PoolSlotData>[Capacity] - fauna residency slot metadata for dehydration and restore - owner: FaunaSimulationMemory
            PoolSlots = new NativeArray<PoolSlotData>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - dehydrated fauna linear velocity cache for Burst LOD updates - owner: FaunaSimulationMemory
            LinearVelocities = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[Capacity] - resident/dehydrated simulation flags for Burst LOD updates - owner: FaunaSimulationMemory
            SimulationFlags = new NativeArray<byte>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterArrays();
            // COLD ALLOC: NativeQueue<int>(Persistent) - free fauna residency slot queue - owner: FaunaSimulationMemory
            FreeSlots = new NativeQueue<int>(Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeQueue(
                FreeSlots,
                Capacity,
                nameof(FaunaSimulationMemory),
                nameof(FreeSlots),
                NativeAllocationLifetime.Session);

            for (int i = 0; i < Capacity; i++)
                FreeSlots.Enqueue(i);
        }

        public void Reset()
        {
            if (PoolSlots.IsCreated)
            {
                for (int i = 0; i < PoolSlots.Length; i++)
                    PoolSlots[i] = default;
            }

            if (LinearVelocities.IsCreated)
            {
                for (int i = 0; i < LinearVelocities.Length; i++)
                    LinearVelocities[i] = default;
            }

            if (SimulationFlags.IsCreated)
            {
                for (int i = 0; i < SimulationFlags.Length; i++)
                    SimulationFlags[i] = 0;
            }

            if (!FreeSlots.IsCreated)
                return;

            FreeSlots.Clear();
            for (int i = 0; i < Capacity; i++)
                FreeSlots.Enqueue(i);
        }

        public void Dispose()
        {
            if (PoolSlots.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(PoolSlots);
                PoolSlots.Dispose();
            }

            if (LinearVelocities.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(LinearVelocities);
                LinearVelocities.Dispose();
            }

            if (SimulationFlags.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(SimulationFlags);
                SimulationFlags.Dispose();
            }

            if (FreeSlots.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FaunaSimulationMemory), nameof(FreeSlots));
                FreeSlots.Dispose();
            }

            Capacity = 0;
        }

        public void Dispose(JobHandle dependency)
        {
            if (PoolSlots.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(PoolSlots);
                PoolSlots.Dispose(dependency);
                PoolSlots = default;
            }

            if (LinearVelocities.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(LinearVelocities);
                LinearVelocities.Dispose(dependency);
                LinearVelocities = default;
            }

            if (SimulationFlags.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(SimulationFlags);
                SimulationFlags.Dispose(dependency);
                SimulationFlags = default;
            }

            if (FreeSlots.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FaunaSimulationMemory), nameof(FreeSlots));
                FreeSlots.Dispose(dependency);
                FreeSlots = default;
            }

            Capacity = 0;
        }

        private void RegisterArrays()
        {
            NativeMemorySentinel.RegisterNativeArray(PoolSlots, nameof(FaunaSimulationMemory), nameof(PoolSlots), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(LinearVelocities, nameof(FaunaSimulationMemory), nameof(LinearVelocities), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(SimulationFlags, nameof(FaunaSimulationMemory), nameof(SimulationFlags), NativeAllocationLifetime.Session);
        }
    }
}
