using Hecton8.Core.Contracts.Signals;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DroneFleetTuningConstants
    {
        [FieldOffset(0)]
        public float MaxDroneSpeed;
        [FieldOffset(4)]
        public float BatteryDrainRate;
        [FieldOffset(8)]
        public float SdfRepulsionStrength;
        [FieldOffset(12)]
        public float RepairSpeed;
        [FieldOffset(16)]
        public float CargoCapacity;
        [FieldOffset(20)]
        public float MiningHoldSeconds;
        [FieldOffset(24)]
        public float LowTierSteeringHz;
        [FieldOffset(28)]
        public float MidTierSteeringHz;
        [FieldOffset(32)]
        public float HighTierSteeringHz;
        [FieldOffset(36)]
        public float UltraTierSteeringHz;
        [FieldOffset(40)]
        public float AStarCellSize;
        [FieldOffset(44)]
        public float LowTierSolveBudget;
        [FieldOffset(48)]
        public float MidTierSolveBudget;
        [FieldOffset(52)]
        public float HighTierSolveBudget;
        [FieldOffset(56)]
        public float UltraTierSolveBudget;
        [FieldOffset(60)]
        public float Reserved0;

        public static DroneFleetTuningConstants CreateDefault()
        {
            return new DroneFleetTuningConstants
            {
                MaxDroneSpeed = 6.5f,
                BatteryDrainRate = 2.5f,
                SdfRepulsionStrength = 4f,
                RepairSpeed = 1f,
                CargoCapacity = 10f,
                MiningHoldSeconds = 0.35f,
                LowTierSteeringHz = 15f,
                MidTierSteeringHz = 30f,
                HighTierSteeringHz = 60f,
                UltraTierSteeringHz = 60f,
                AStarCellSize = 4f,
                LowTierSolveBudget = 2f,
                MidTierSolveBudget = 4f,
                HighTierSolveBudget = 8f,
                UltraTierSolveBudget = 12f,
                Reserved0 = 0f
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneStateDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public uint CurrentTaskHash;
        [FieldOffset(40)] public float BatteryLevel;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint _pad0;
        [FieldOffset(52)] public uint _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneTargetDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float3 LocalPosition;
        [FieldOffset(36)] public uint TaskHash;
        [FieldOffset(40)] public int TaskIndex;
        [FieldOffset(44)] public int TargetModuleId;
        [FieldOffset(48)] public float Radius;
        [FieldOffset(52)] public uint TaskKind;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct DroneProceduralIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DroneChassisSpecDTO
    {
        [FieldOffset(0)] public uint TypeHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float MaxSpeed;
        [FieldOffset(12)] public float BatteryCapacity;
        [FieldOffset(16)] public float BatteryDrainRate;
        [FieldOffset(20)] public float RepairSpeed;
        [FieldOffset(24)] public float CargoCapacity;
        [FieldOffset(28)] public float MiningHoldSeconds;
        [FieldOffset(32)] public float SdfRepulsionScale;
        [FieldOffset(36)] public float Reserved0;
        [FieldOffset(40)] public ulong _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    internal static class DroneFleetLayoutSentinel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneStateDTO()
        {
            return UnsafeUtility.SizeOf<DroneStateDTO>() == 64 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.AUP_Position)) == 0 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.Velocity)) == 24 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.CurrentTaskHash)) == 36 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.BatteryLevel)) == 40 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.Flags)) == 44 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO._pad0)) == 48 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO._pad1)) == 52 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO._pad2)) == 56;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneTargetDTO()
        {
            return UnsafeUtility.SizeOf<DroneTargetDTO>() == 64 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TargetAUP)) == 0 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.LocalPosition)) == 24 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TaskHash)) == 36 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TaskIndex)) == 40 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TargetModuleId)) == 44 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.Radius)) == 48 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TaskKind)) == 52 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.Flags)) == 56 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.Reserved0)) == 60;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneTaskDTO()
        {
            return UnsafeUtility.SizeOf<DroneTaskDTO>() == 64 &&
                UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>() == 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneChassisSpecDTO()
        {
            return UnsafeUtility.SizeOf<DroneChassisSpecDTO>() == 64 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.TypeHash)) == 0 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.Flags)) == 4 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.MaxSpeed)) == 8 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.BatteryCapacity)) == 12 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.BatteryDrainRate)) == 16 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.RepairSpeed)) == 20 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.CargoCapacity)) == 24 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.MiningHoldSeconds)) == 28 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.SdfRepulsionScale)) == 32 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.Reserved0)) == 36 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO._pad0)) == 40 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO._pad1)) == 48 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO._pad2)) == 56;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
    }

    internal static class DroneFleetMockTasks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GenerateMockDroneTasks(NativeArray<DroneTaskDTO> tasks, double3 fleetAup, int requestedCount)
        {
            if (!tasks.IsCreated || requestedCount <= 0)
                return 0;

            int count = math.min(tasks.Length, requestedCount);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.3999631f;
                float radius = 6f + ((i & 7) * 2.5f);
                float3 local = new float3(math.cos(angle) * radius, -1.5f + ((i % 3) * 1.5f), math.sin(angle) * radius);
                tasks[i] = new DroneTaskDTO
                {
                    TargetAup = fleetAup + new double3(local.x, local.y, local.z),
                    LocalPosition = local,
                    Priority = 1f + ((i & 3) * 0.25f),
                    Score = 0f,
                    CriticalityWeight = 1f + ((i % 5) * 0.2f),
                    Radius = 1.25f,
                    ModuleIndex = i,
                    TaskKind = (i & 1) == 0 ? 1 : 3,
                    Reserved0 = 0u
                };
            }

            return count;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockDroneTasksJob : IJob
    {
        [NoAlias] public NativeArray<DroneTaskDTO> Tasks;
        public double3 FleetAup;
        public int RequestedCount;

        public void Execute()
        {
            DroneFleetMockTasks.GenerateMockDroneTasks(Tasks, FleetAup, RequestedCount);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockDroneTasksQueueJob : IJob
    {
        public NativeQueue<DroneTaskDTO>.ParallelWriter Tasks;
        public double3 FleetAup;
        public int RequestedCount;

        public void Execute()
        {
            int count = math.max(0, RequestedCount);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.3999631f;
                float radius = 6f + ((i & 7) * 2.5f);
                float3 local = new float3(math.cos(angle) * radius, -1.5f + ((i % 3) * 1.5f), math.sin(angle) * radius);
                Tasks.Enqueue(new DroneTaskDTO
                {
                    TargetAup = FleetAup + new double3(local.x, local.y, local.z),
                    LocalPosition = local,
                    Priority = 1f + ((i & 3) * 0.25f),
                    Score = 0f,
                    CriticalityWeight = 1f + ((i % 5) * 0.2f),
                    Radius = 1.25f,
                    ModuleIndex = i,
                    TaskKind = (i & 1) == 0 ? 1 : 3,
                    Reserved0 = 0u
                });
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DroneTaskAssignmentJob : IJobParallelFor
    {
        private const int UnclaimedTask = 0;
        private const int EmptyTaskIndex = -1;
        private const float MinimumScoreDistanceSq = 0.5625f;

        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessDroneState> Drones;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneStateDTO> DroneStatesDto;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTargetDTO> DroneTargets;
        [ReadOnly, NoAlias] public NativeArray<DroneTaskDTO> Tasks;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TaskClaimOwners;

        public int TaskCount;
        public int EmergencyOverclock;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Drones.Length)
                return;

            HeadlessDroneState drone = Drones[index];
            if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                drone.State == (byte)HeadlessDroneRuntimeState.Completed ||
                drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                drone.State == (byte)HeadlessDroneRuntimeState.Attack ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending ||
                drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.TargetTaskIndex != EmptyTaskIndex)
            {
                MirrorDto(index, in drone, ResolveTaskHash(index, in drone));
                return;
            }

            if (TaskCount <= 0 || !Tasks.IsCreated || !TaskClaimOwners.IsCreated)
            {
                MirrorDto(index, in drone, ResolveTaskHash(index, in drone));
                return;
            }

            double3 droneAup = ResolveDroneAup(index, in drone);
            float battery01 = math.saturate(drone.BatteryPercent * 0.01f);
            float bestScore = -1f;
            int bestTaskIndex = -1;
            DroneTaskDTO bestTask = default;
            int count = math.min(TaskCount, math.min(Tasks.Length, TaskClaimOwners.Length));
            for (int taskIndex = 0; taskIndex < count; taskIndex++)
            {
                DroneTaskDTO task = Tasks[taskIndex];
                if (task.ModuleIndex < 0)
                    continue;

                if (drone.HubGridId != 0 && task.Reserved0 != 0u && (uint)drone.HubGridId != task.Reserved0)
                    continue;

                if (EmergencyOverclock != 0 && task.TaskKind == (int)DroneFleetTaskKind.CutParasite)
                    continue;

                float3 delta = ToLocalDelta(task.TargetAup, droneAup);
                if (!IsFinite(delta))
                    delta = task.LocalPosition - drone.Position;

                float distanceSq = math.max(MinimumScoreDistanceSq, math.lengthsq(delta));
                float priority = math.max(0.1f, task.Priority + task.CriticalityWeight);
                float score = priority * battery01 * math.rcp(distanceSq);
                if (!math.isfinite(score) || score <= bestScore)
                    continue;

                bestScore = score;
                bestTaskIndex = taskIndex;
                bestTask = task;
            }

            if (bestTaskIndex < 0 || !TryClaimTask(bestTaskIndex, drone.DroneId))
            {
                MirrorDto(index, in drone, ResolveTaskHash(index, in drone));
                return;
            }

            uint taskHash = ResolveTaskHash(bestTaskIndex, in bestTask);
            drone.TargetTaskIndex = bestTask.ModuleIndex;
            drone.TargetPosition = bestTask.LocalPosition;
            drone.TargetAup = bestTask.TargetAup;
            drone.ServiceRadius = math.max(0.1f, bestTask.Radius);
            drone.State = (byte)HeadlessDroneRuntimeState.Travel;
            Drones[index] = drone;

            if (DroneTargets.IsCreated && (uint)index < (uint)DroneTargets.Length)
            {
                DroneTargets[index] = new DroneTargetDTO
                {
                    TargetAUP = bestTask.TargetAup,
                    LocalPosition = bestTask.LocalPosition,
                    TaskHash = taskHash,
                    TaskIndex = bestTask.ModuleIndex,
                    TargetModuleId = bestTask.ModuleIndex,
                    Radius = bestTask.Radius,
                    TaskKind = (uint)math.max(0, bestTask.TaskKind),
                    Flags = 1u,
                    Reserved0 = 0u
                };
            }

            MirrorDto(index, in drone, taskHash);
        }

        private bool TryClaimTask(int taskIndex, int droneId)
        {
            int* claimPtr = (int*)TaskClaimOwners.GetUnsafePtr();
            int priorOwner = Interlocked.CompareExchange(ref claimPtr[taskIndex], droneId, UnclaimedTask);
            return priorOwner == UnclaimedTask || priorOwner == droneId;
        }

        private double3 ResolveDroneAup(int index, in HeadlessDroneState drone)
        {
            if (IsFinite(drone.PositionAup))
                return drone.PositionAup;

            if (DroneStatesDto.IsCreated && (uint)index < (uint)DroneStatesDto.Length)
            {
                DroneStateDTO* statePtr = (DroneStateDTO*)DroneStatesDto.GetUnsafePtr();
                ref DroneStateDTO dto = ref UnsafeUtility.AsRef<DroneStateDTO>(statePtr + index);
                if (IsFinite(dto.AUP_Position))
                    return dto.AUP_Position;
            }

            return ToDouble3(drone.Position);
        }

        private void MirrorDto(int index, in HeadlessDroneState drone, uint taskHash)
        {
            if (!DroneStatesDto.IsCreated || (uint)index >= (uint)DroneStatesDto.Length)
                return;

            DroneStateDTO* statePtr = (DroneStateDTO*)DroneStatesDto.GetUnsafePtr();
            ref DroneStateDTO dto = ref UnsafeUtility.AsRef<DroneStateDTO>(statePtr + index);
            dto.AUP_Position = ResolveDroneAup(index, in drone);
            dto.Velocity = drone.Velocity;
            dto.CurrentTaskHash = taskHash;
            dto.BatteryLevel = math.clamp(drone.BatteryPercent, 0f, 100f);
            dto.Flags = PackFlags(in drone);
        }

        private static uint ResolveTaskHash(int taskIndex, in DroneTaskDTO task)
        {
            uint3 hashInput = new uint3(
                (uint)math.max(0, taskIndex + 1),
                (uint)math.max(0, task.ModuleIndex + 1),
                (uint)math.max(0, task.TaskKind + 1));
            return math.hash(hashInput);
        }

        private static uint ResolveTaskHash(int index, in HeadlessDroneState drone)
        {
            uint3 hashInput = new uint3(
                (uint)math.max(0, index + 1),
                (uint)math.max(0, drone.TargetTaskIndex + 1),
                (uint)drone.State);
            return math.hash(hashInput);
        }

        private static uint PackFlags(in HeadlessDroneState drone)
        {
            return ((uint)drone.State) |
                   ((uint)drone.FactionBit << 8) |
                   ((uint)drone.CorridorTight << 16);
        }

        private static float3 ToLocalDelta(double3 targetAup, double3 originAup)
        {
            double3 delta = targetAup - originAup;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DroneMetabolismJob : IJobParallelFor
    {
        private const int EmptyTaskIndex = -1;
        private const float ReturnBatteryThresholdPercent = 15f;

        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessDroneState> Drones;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneStateDTO> DroneStatesDto;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTargetDTO> DroneTargets;

        public float DeltaTime;
        public int EmergencyOverclock;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Drones.Length)
                return;

            HeadlessDroneState drone = Drones[index];
            if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                drone.State == (byte)HeadlessDroneRuntimeState.Completed)
            {
                MirrorDto(index, in drone);
                return;
            }

            float safeDt = math.max(0f, DeltaTime);
            float speed = math.sqrt(math.max(0f, math.lengthsq(drone.Velocity)));
            float maxSpeed = math.max(0.1f, drone.MaxSpeed);
            float speed01 = math.saturate(speed * math.rcp(maxSpeed));
            float drainScale = EmergencyOverclock != 0 ? 5f : 1f;
            float drain = drone.BatteryDrainPerSecond * math.lerp(0.25f, 1f, speed01) * drainScale * safeDt;
            drone.BatteryPercent = math.max(0f, drone.BatteryPercent - drain);

            if (drone.BatteryPercent <= 0f)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
                drone.Velocity = float3.zero;
            }
            else if (drone.BatteryPercent <= ReturnBatteryThresholdPercent &&
                drone.State != (byte)HeadlessDroneRuntimeState.Return &&
                drone.State != (byte)HeadlessDroneRuntimeState.Docking &&
                drone.State != (byte)HeadlessDroneRuntimeState.ResupplyTravel &&
                drone.State != (byte)HeadlessDroneRuntimeState.ResupplyDocked &&
                drone.State != (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
            {
                drone.TargetTaskIndex = EmptyTaskIndex;
                drone.TargetModuleId = 0;
                drone.TargetPosition = drone.HomePosition;
                drone.TargetAup = IsFinite(drone.HomeAup) ? drone.HomeAup : drone.PositionAup;
                drone.State = (byte)HeadlessDroneRuntimeState.Return;

                if (DroneTargets.IsCreated && (uint)index < (uint)DroneTargets.Length)
                {
                    DroneTargets[index] = new DroneTargetDTO
                    {
                        TargetAUP = drone.TargetAup,
                        LocalPosition = drone.HomePosition,
                        TaskHash = math.hash(new uint3((uint)math.max(0, drone.DroneId), 0x52544E55u, 15u)),
                        TaskIndex = EmptyTaskIndex,
                        TargetModuleId = 0,
                        Radius = math.max(0.1f, drone.ServiceRadius),
                        TaskKind = 0u,
                        Flags = 2u,
                        Reserved0 = 0u
                    };
                }
            }

            Drones[index] = drone;
            MirrorDto(index, in drone);
        }

        private void MirrorDto(int index, in HeadlessDroneState drone)
        {
            if (!DroneStatesDto.IsCreated || (uint)index >= (uint)DroneStatesDto.Length)
                return;

            DroneStateDTO* statePtr = (DroneStateDTO*)DroneStatesDto.GetUnsafePtr();
            ref DroneStateDTO dto = ref UnsafeUtility.AsRef<DroneStateDTO>(statePtr + index);
            dto.AUP_Position = IsFinite(drone.PositionAup) ? drone.PositionAup : ToDouble3(drone.Position);
            dto.Velocity = drone.Velocity;
            dto.CurrentTaskHash = math.hash(new uint3((uint)math.max(0, drone.TargetTaskIndex + 1), (uint)math.max(0, drone.DroneId), (uint)drone.State));
            dto.BatteryLevel = math.clamp(drone.BatteryPercent, 0f, 100f);
            dto.Flags = ((uint)drone.State) | ((uint)drone.FactionBit << 8) | ((uint)drone.CorridorTight << 16);
        }

        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ClearDroneMacroWaypointsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<PathWaypointDTO> Waypoints;
        [NoAlias] public NativeArray<byte> WaypointStates;

        public void Execute(int index)
        {
            if (WaypointStates.IsCreated && (uint)index < (uint)WaypointStates.Length)
                WaypointStates[index] = 0;

            if (Waypoints.IsCreated && (uint)index < (uint)Waypoints.Length)
                Waypoints[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ExtractDroneMatricesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<HeadlessDroneState> Drones;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneStateDTO> DroneStatesDto;
        [NoAlias] public NativeArray<float4x4> Matrices;
        public double3 CameraAup;
        public float ScaleMeters;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Drones.Length || (uint)index >= (uint)Matrices.Length)
                return;

            HeadlessDroneState drone = Drones[index];
            if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                drone.State == (byte)HeadlessDroneRuntimeState.Completed)
            {
                Matrices[index] = float4x4.zero;
                return;
            }

            double3 positionAup = drone.PositionAup;
            if (DroneStatesDto.IsCreated && (uint)index < (uint)DroneStatesDto.Length)
            {
                DroneStateDTO* statePtr = (DroneStateDTO*)DroneStatesDto.GetUnsafePtr();
                ref DroneStateDTO dto = ref UnsafeUtility.AsRef<DroneStateDTO>(statePtr + index);
                if (IsFinite(dto.AUP_Position))
                    positionAup = dto.AUP_Position;
            }

            float3 localPosition = IsFinite(positionAup)
                ? ToFloat3(positionAup - CameraAup)
                : drone.Position;
            if (!IsFinite(localPosition))
            {
                Matrices[index] = float4x4.zero;
                return;
            }

            quaternion rotation = ResolveSafeRotation(drone.Rotation);
            float scale = math.max(0.01f, ScaleMeters);
            Matrices[index] = float4x4.TRS(localPosition, rotation, new float3(scale, scale, scale));
        }

        private static quaternion ResolveSafeRotation(quaternion value)
        {
            float4 raw = value.value;
            float lengthSq = math.lengthsq(raw);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return quaternion.identity;

            return new quaternion(raw * math.rsqrt(lengthSq));
        }

        private static float3 ToFloat3(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildDroneProceduralArgsJob : IJob
    {
        [NoAlias] public NativeArray<DroneProceduralIndirectArgsDTO> Args;
        public uint VertexCountPerInstance;
        public uint InstanceCount;

        public void Execute()
        {
            if (!Args.IsCreated || Args.Length <= 0)
                return;

            Args[0] = new DroneProceduralIndirectArgsDTO
            {
                VertexCountPerInstance = VertexCountPerInstance,
                InstanceCount = InstanceCount,
                StartVertex = 0u,
                StartInstance = 0u
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct PathWaypointDTO
    {
        [FieldOffset(0)]
        public float3 LocalPosition;
        [FieldOffset(12)]
        public uint ActionCode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct DroneFleetDebugRoute
    {
        [FieldOffset(0)]
        public float3 Position;
        [FieldOffset(12)]
        public float3 Target;
        [FieldOffset(24)]
        public float3 Waypoint;
        [FieldOffset(36)]
        public float3 SdfNormal;
        [FieldOffset(48)]
        public float3 Velocity;
        [FieldOffset(60)]
        public float3 RoutePoint0;
        [FieldOffset(72)]
        public float3 RoutePoint1;
        [FieldOffset(84)]
        public float3 RoutePoint2;
        [FieldOffset(96)]
        public float3 RoutePoint3;
        [FieldOffset(108)]
        public int RoutePointCount;
        [FieldOffset(112)]
        public int DroneId;
        [FieldOffset(116)]
        public int PathStatus;
        [FieldOffset(120)]
        public float BatteryPercent;
        [FieldOffset(124)]
        public byte State;
        [FieldOffset(125)]
        public byte Flags;
        [FieldOffset(126)]
        public ushort Reserved0;
        [FieldOffset(128)]
        public uint Reserved1;
        [FieldOffset(132)]
        public uint Reserved2;
        [FieldOffset(136)]
        public uint Reserved3;
        [FieldOffset(140)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct DroneFleetAutomationStats
    {
        [FieldOffset(0)]
        public int ActiveDrones;
        [FieldOffset(4)]
        public int PathSolves;
        [FieldOffset(8)]
        public int PathFailures;
        [FieldOffset(12)]
        public int PathIterations;
        [FieldOffset(16)]
        public int TasksCompleted;
        [FieldOffset(20)]
        public int LastAStarStatus;
        [FieldOffset(24)]
        public int SteeringTickModulo;
        [FieldOffset(28)]
        public int ChassisSpecCount;
        [FieldOffset(32)]
        public float AveragePathfindingTimeMs;
        [FieldOffset(36)]
        public float SdfRepulsionStrength;
        [FieldOffset(40)]
        public float AStarCellSize;
        [FieldOffset(44)]
        public float AverageBatteryPercent;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DroneFleetMockRepairSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int TargetModuleId;
        [FieldOffset(8)] public float RepairUnits;
        [FieldOffset(12)] public float3 Position;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DroneFleetMockMiningSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int TargetNodeId;
        [FieldOffset(8)] public float WorkSeconds;
        [FieldOffset(12)] public float3 Position;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DroneFleetInventoryTransactionSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int SourceId;
        [FieldOffset(8)] public int DestinationId;
        [FieldOffset(12)] public int ItemHash;
        [FieldOffset(16)] public int Quantity;
        [FieldOffset(20)] public float3 Position;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneTaskDTO
    {
        [FieldOffset(0)]
        public double3 TargetAup;
        [FieldOffset(24)]
        public float3 LocalPosition;
        [FieldOffset(36)]
        public float Priority;
        [FieldOffset(40)]
        public float Score;
        [FieldOffset(44)]
        public float CriticalityWeight;
        [FieldOffset(48)]
        public float Radius;
        [FieldOffset(52)]
        public int ModuleIndex;
        [FieldOffset(56)]
        public int TaskKind;
        [FieldOffset(60)]
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct MockSDFGrid
    {
        [FieldOffset(0)]
        public float3 BoundsMin;
        [FieldOffset(12)]
        public float RepulsionDistance;
        [FieldOffset(16)]
        public float3 BoundsMax;
        [FieldOffset(28)]
        public float SeamSpacing;
        [FieldOffset(32)]
        public float3 SeamNormal;
        [FieldOffset(44)]
        public float SeamHalfWidth;
        [FieldOffset(48)]
        public int Enabled;
        [FieldOffset(52)]
        public int Reserved0;
        [FieldOffset(56)]
        public float Reserved1;
        [FieldOffset(60)]
        public float Reserved2;

        public static MockSDFGrid CreateDefault()
        {
            return new MockSDFGrid
            {
                BoundsMin = new float3(-256f, -96f, -256f),
                BoundsMax = new float3(256f, 96f, 256f),
                RepulsionDistance = 2.25f,
                SeamSpacing = 17f,
                SeamNormal = math.normalize(new float3(1f, 0f, 1f)),
                SeamHalfWidth = 0.18f,
                Enabled = 1,
                Reserved0 = 0,
                Reserved1 = 0f,
                Reserved2 = 0f
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlocked(float3 position)
        {
            if (Enabled == 0 || !IsFinite(position))
                return false;

            if (position.x <= BoundsMin.x || position.y <= BoundsMin.y || position.z <= BoundsMin.z ||
                position.x >= BoundsMax.x || position.y >= BoundsMax.y || position.z >= BoundsMax.z)
            {
                return true;
            }

            return ResolveSeamDistance(position, out _) <= SeamHalfWidth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySampleRepulsion(float3 position, out float3 normal, out float distance)
        {
            normal = float3.zero;
            distance = float.MaxValue;

            if (Enabled == 0 || !IsFinite(position))
                return false;

            float3 minDelta = position - BoundsMin;
            float3 maxDelta = BoundsMax - position;
            TryUseCandidate(minDelta.x, new float3(1f, 0f, 0f), ref normal, ref distance);
            TryUseCandidate(maxDelta.x, new float3(-1f, 0f, 0f), ref normal, ref distance);
            TryUseCandidate(minDelta.y, new float3(0f, 1f, 0f), ref normal, ref distance);
            TryUseCandidate(maxDelta.y, new float3(0f, -1f, 0f), ref normal, ref distance);
            TryUseCandidate(minDelta.z, new float3(0f, 0f, 1f), ref normal, ref distance);
            TryUseCandidate(maxDelta.z, new float3(0f, 0f, -1f), ref normal, ref distance);

            float seamDistance = ResolveSeamDistance(position, out float seamSign);
            if (seamDistance < distance)
            {
                normal = SafeNormalize(SeamNormal * seamSign, new float3(1f, 0f, 0f));
                distance = seamDistance;
            }

            return distance <= RepulsionDistance && IsFinite(normal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSeamDistance(float3 position, out float seamSign)
        {
            float spacing = math.max(1f, SeamSpacing);
            float coord = math.dot(position, SafeNormalize(SeamNormal, new float3(1f, 0f, 1f))) * math.rcp(spacing);
            float fraction = math.frac(coord);
            float centered = fraction - 0.5f;
            seamSign = centered >= 0f ? 1f : -1f;
            return math.abs(centered) * spacing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryUseCandidate(float candidateDistance, float3 candidateNormal, ref float3 normal, ref float distance)
        {
            if (candidateDistance >= distance)
                return;

            distance = candidateDistance;
            normal = candidateNormal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct DroneNativeMinHeapNode
    {
        [FieldOffset(0)]
        public float Cost;
        [FieldOffset(4)]
        public int NodeIndex;
    }

    internal struct DroneNativeMinHeap
    {
        public NativeArray<DroneNativeMinHeapNode> Nodes;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }

        public bool TryPush(int nodeIndex, float cost)
        {
            if (!Nodes.IsCreated || Count >= Nodes.Length)
                return false;

            int cursor = Count++;
            Nodes[cursor] = new DroneNativeMinHeapNode { NodeIndex = nodeIndex, Cost = cost };
            while (cursor > 0)
            {
                int parent = (cursor - 1) >> 1;
                if (Nodes[parent].Cost <= Nodes[cursor].Cost)
                    break;

                Swap(parent, cursor);
                cursor = parent;
            }

            return true;
        }

        public bool TryPop(out int nodeIndex, out float cost)
        {
            nodeIndex = -1;
            cost = 0f;
            if (Count <= 0 || !Nodes.IsCreated)
                return false;

            DroneNativeMinHeapNode root = Nodes[0];
            Count--;
            if (Count > 0)
                Nodes[0] = Nodes[Count];

            int cursor = 0;
            while (true)
            {
                int left = (cursor << 1) + 1;
                int right = left + 1;
                if (left >= Count)
                    break;

                int best = right < Count && Nodes[right].Cost < Nodes[left].Cost ? right : left;
                if (Nodes[cursor].Cost <= Nodes[best].Cost)
                    break;

                Swap(cursor, best);
                cursor = best;
            }

            nodeIndex = root.NodeIndex;
            cost = root.Cost;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Swap(int a, int b)
        {
            DroneNativeMinHeapNode tmp = Nodes[a];
            Nodes[a] = Nodes[b];
            Nodes[b] = tmp;
        }
    }

    internal struct DroneTaskNativeMinHeap
    {
        public NativeArray<DroneTaskDTO> Nodes;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }

        public bool TryPush(in DroneTaskDTO node)
        {
            if (!Nodes.IsCreated || Count >= Nodes.Length)
                return false;

            int cursor = Count++;
            Nodes[cursor] = node;
            while (cursor > 0)
            {
                int parent = (cursor - 1) >> 1;
                DroneTaskDTO parentNode = Nodes[parent];
                DroneTaskDTO cursorNode = Nodes[cursor];
                if (LessThanOrEqual(in parentNode, in cursorNode))
                    break;

                Swap(parent, cursor);
                cursor = parent;
            }

            return true;
        }

        public bool TryPop(out DroneTaskDTO node)
        {
            node = default;
            if (Count <= 0 || !Nodes.IsCreated)
                return false;

            node = Nodes[0];
            Count--;
            if (Count > 0)
                Nodes[0] = Nodes[Count];

            int cursor = 0;
            while (true)
            {
                int left = (cursor << 1) + 1;
                int right = left + 1;
                if (left >= Count)
                    break;

                int best = left;
                if (right < Count)
                {
                    DroneTaskDTO rightNode = Nodes[right];
                    DroneTaskDTO leftNode = Nodes[left];
                    if (LessThan(in rightNode, in leftNode))
                        best = right;
                }

                DroneTaskDTO cursorNode = Nodes[cursor];
                DroneTaskDTO bestNode = Nodes[best];
                if (LessThanOrEqual(in cursorNode, in bestNode))
                    break;

                Swap(cursor, best);
                cursor = best;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LessThan(in DroneTaskDTO a, in DroneTaskDTO b)
        {
            if (a.Priority < b.Priority)
                return true;
            if (a.Priority > b.Priority)
                return false;

            return a.Score > b.Score;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LessThanOrEqual(in DroneTaskDTO a, in DroneTaskDTO b)
        {
            if (a.Priority < b.Priority)
                return true;
            if (a.Priority > b.Priority)
                return false;

            return a.Score >= b.Score;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Swap(int a, int b)
        {
            DroneTaskDTO tmp = Nodes[a];
            Nodes[a] = Nodes[b];
            Nodes[b] = tmp;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct DroneAStarTelemetry
    {
        [FieldOffset(0)]
        public int SolvedCount;
        [FieldOffset(4)]
        public int FailedCount;
        [FieldOffset(8)]
        public int IterationCount;
        [FieldOffset(12)]
        public int LastStatus;
        [FieldOffset(16)]
        public int ActiveCandidateCount;
        [FieldOffset(20)]
        public int Reserved0;
        [FieldOffset(24)]
        public int Reserved1;
        [FieldOffset(28)]
        public int Reserved2;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct DroneMacroAStarJob : IJob
    {
        private const int GridSide = 8;
        private const int GridSideSq = GridSide * GridSide;
        private const int NodeCapacity = GridSide * GridSide * GridSide;
        private const int StartCoord = GridSide >> 1;
        private const int StartNode = StartCoord + (StartCoord * GridSide) + (StartCoord * GridSideSq);
        private const float VerticalPenalty = 1.85f;
        private const float HugeCost = 3.402823e+38f;

        [ReadOnly, NoAlias] public NativeArray<HeadlessDroneState> Drones;
        [NoAlias] public NativeArray<PathWaypointDTO> Waypoints;
        [NoAlias] public NativeArray<byte> WaypointStates;
        [NoAlias] public NativeArray<DroneNativeMinHeapNode> OpenHeap;
        [NoAlias] public NativeArray<float> GCosts;
        [NoAlias] public NativeArray<int> CameFrom;
        [NoAlias] public NativeArray<byte> NodeStates;
        [NoAlias] public NativeArray<int> RouteNodes;
        [NoAlias] public NativeArray<byte> RouteNodeCounts;
        [NoAlias] public NativeArray<DroneAStarTelemetry> Telemetry;
        public MockSDFGrid SdfGrid;
        public int FrameIndex;
        public int MaxSolves;
        public int RouteNodeStride;
        public float CellSize;

        public void Execute()
        {
            if (!Drones.IsCreated || !Waypoints.IsCreated || !WaypointStates.IsCreated ||
                !OpenHeap.IsCreated || !GCosts.IsCreated || !CameFrom.IsCreated || !NodeStates.IsCreated)
            {
                return;
            }

            int droneLimit = math.min(Drones.Length, math.min(Waypoints.Length, WaypointStates.Length));
            for (int i = 0; i < droneLimit; i++)
            {
                Waypoints[i] = default;
                WaypointStates[i] = 0;
                if (RouteNodeCounts.IsCreated && i < RouteNodeCounts.Length)
                    RouteNodeCounts[i] = 0;
            }

            int solveBudget = math.clamp(MaxSolves, 1, droneLimit);
            int solved = 0;
            int failed = 0;
            int iterations = 0;
            int candidates = 0;
            int lastStatus = 0;
            float cell = math.max(0.5f, CellSize);

            for (int i = 0; i < droneLimit && (solved + failed) < solveBudget; i++)
            {
                HeadlessDroneState drone = Drones[i];
                if (!ShouldSolve(in drone))
                    continue;

                if (((FrameIndex + i) & 1) != 0 && solveBudget < 8)
                    continue;

                candidates++;
                byte status = SolveDronePath(i, in drone, cell, ref iterations);
                lastStatus = status;
                if (status == 1)
                    solved++;
                else if (status == 2)
                    failed++;
            }

            if (Telemetry.IsCreated && Telemetry.Length > 0)
            {
                Telemetry[0] = new DroneAStarTelemetry
                {
                    SolvedCount = solved,
                    FailedCount = failed,
                    IterationCount = iterations,
                    LastStatus = lastStatus,
                    ActiveCandidateCount = candidates,
                    Reserved0 = 0,
                    Reserved1 = 0,
                    Reserved2 = 0
                };
            }
        }

        private byte SolveDronePath(int droneIndex, in HeadlessDroneState drone, float cell, ref int iterationAccumulator)
        {
            float3 destination = ResolveDestination(in drone);
            float3 toDestination = destination - drone.Position;
            if (!IsFinite(toDestination))
                return 2;

            if (math.lengthsq(toDestination) <= cell * cell)
            {
                Waypoints[droneIndex] = new PathWaypointDTO { LocalPosition = destination, ActionCode = 1u };
                WaypointStates[droneIndex] = 1;
                WriteRouteNodes(droneIndex, StartNode, math.max(1, RouteNodeStride));
                return 1;
            }

            int3 goalCoord = ResolveGoalCoord(toDestination, cell);
            int goalNode = PackNode(goalCoord);
            ClearScratch();

            DroneNativeMinHeap heap = new DroneNativeMinHeap { Nodes = OpenHeap, Count = 0 };
            GCosts[StartNode] = 0f;
            CameFrom[StartNode] = -1;
            NodeStates[StartNode] = 1;
            heap.TryPush(StartNode, ResolveHeuristic(UnpackNode(StartNode), goalCoord, cell));

            int bestNode = StartNode;
            float bestHeuristic = ResolveHeuristic(UnpackNode(StartNode), goalCoord, cell);
            int localIterations = 0;
            bool complete = false;

            while (heap.TryPop(out int current, out _) && localIterations < NodeCapacity)
            {
                localIterations++;
                if (NodeStates[current] == 2)
                    continue;

                NodeStates[current] = 2;
                int3 currentCoord = UnpackNode(current);
                float heuristic = ResolveHeuristic(currentCoord, goalCoord, cell);
                if (heuristic < bestHeuristic)
                {
                    bestHeuristic = heuristic;
                    bestNode = current;
                }

                if (current == goalNode)
                {
                    bestNode = current;
                    complete = true;
                    break;
                }

                for (int direction = 0; direction < 6; direction++)
                    TryVisitNeighbor(current, currentCoord, goalNode, goalCoord, drone.Position, cell, direction, ref heap);
            }

            iterationAccumulator += localIterations;
            int pathNode = complete ? goalNode : bestNode;
            if (pathNode == StartNode)
            {
                if (SdfGrid.TrySampleRepulsion(drone.Position, out float3 normal, out _))
                {
                    Waypoints[droneIndex] = new PathWaypointDTO
                    {
                        LocalPosition = drone.Position + (normal * cell),
                        ActionCode = 2u
                    };
                    WaypointStates[droneIndex] = 2;
                    return 2;
                }

                return 2;
            }

            float3 waypoint = ResolveFirstStep(pathNode, drone.Position, destination, cell);
            WriteRouteNodes(droneIndex, pathNode, math.max(1, RouteNodeStride));
            Waypoints[droneIndex] = new PathWaypointDTO
            {
                LocalPosition = waypoint,
                ActionCode = complete ? 1u : 2u
            };
            WaypointStates[droneIndex] = complete ? (byte)1 : (byte)2;
            return complete ? (byte)1 : (byte)2;
        }

        private void WriteRouteNodes(int droneIndex, int pathNode, int stride)
        {
            if (!RouteNodes.IsCreated || !RouteNodeCounts.IsCreated ||
                droneIndex < 0 || droneIndex >= RouteNodeCounts.Length ||
                stride <= 0)
            {
                return;
            }

            int offset = droneIndex * stride;
            if (offset < 0 || offset >= RouteNodes.Length)
                return;

            int count = 0;
            int current = pathNode;
            int guard = 0;
            while (current >= 0 && current != StartNode && count < stride && offset + count < RouteNodes.Length && guard++ < NodeCapacity)
            {
                RouteNodes[offset + count] = current;
                current = CameFrom[current];
            }

            RouteNodeCounts[droneIndex] = (byte)math.min(count, 255);
        }

        private void TryVisitNeighbor(
            int current,
            int3 currentCoord,
            int goalNode,
            int3 goalCoord,
            float3 origin,
            float cell,
            int direction,
            ref DroneNativeMinHeap heap)
        {
            int3 neighborCoord = currentCoord + ResolveDirection(direction);
            if (neighborCoord.x < 0 || neighborCoord.y < 0 || neighborCoord.z < 0 ||
                neighborCoord.x >= GridSide || neighborCoord.y >= GridSide || neighborCoord.z >= GridSide)
            {
                return;
            }

            int neighbor = PackNode(neighborCoord);
            if (NodeStates[neighbor] == 2)
                return;

            float3 world = WorldFromCoord(neighborCoord, origin, cell);
            if (neighbor != goalNode && SdfGrid.IsBlocked(world))
                return;

            float tentativeG = GCosts[current] + ResolveStepCost(currentCoord, neighborCoord, world, cell);
            if (tentativeG >= GCosts[neighbor])
                return;

            CameFrom[neighbor] = current;
            GCosts[neighbor] = tentativeG;
            NodeStates[neighbor] = 1;
            heap.TryPush(neighbor, tentativeG + ResolveHeuristic(neighborCoord, goalCoord, cell));
        }

        private float3 ResolveFirstStep(int pathNode, float3 origin, float3 destination, float cell)
        {
            int current = pathNode;
            int parent = CameFrom[current];
            int guard = 0;
            while (parent >= 0 && parent != StartNode && guard++ < NodeCapacity)
            {
                current = parent;
                parent = CameFrom[current];
            }

            if (current == pathNode && parent < 0)
                return destination;

            return WorldFromCoord(UnpackNode(current), origin, cell);
        }

        private void ClearScratch()
        {
            int limit = math.min(NodeCapacity, math.min(GCosts.Length, math.min(CameFrom.Length, NodeStates.Length)));
            for (int i = 0; i < limit; i++)
            {
                GCosts[i] = HugeCost;
                CameFrom[i] = -1;
                NodeStates[i] = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldSolve(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Travel ||
                drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel ||
                drone.State == (byte)HeadlessDroneRuntimeState.Wander)
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveDestination(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking)
            {
                return drone.HomePosition;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel)
                return drone.SupplyPosition;

            return drone.TargetPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 ResolveGoalCoord(float3 toDestination, float cell)
        {
            return math.clamp(
                new int3(
                    StartCoord + (int)math.round(toDestination.x * math.rcp(cell)),
                    StartCoord + (int)math.round(toDestination.y * math.rcp(cell)),
                    StartCoord + (int)math.round(toDestination.z * math.rcp(cell))),
                new int3(0, 0, 0),
                new int3(GridSide - 1, GridSide - 1, GridSide - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 ResolveDirection(int direction)
        {
            switch (direction)
            {
                case 0: return new int3(1, 0, 0);
                case 1: return new int3(-1, 0, 0);
                case 2: return new int3(0, 1, 0);
                case 3: return new int3(0, -1, 0);
                case 4: return new int3(0, 0, 1);
                default: return new int3(0, 0, -1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveStepCost(int3 current, int3 neighbor, float3 world, float cell)
        {
            float cost = current.y != neighbor.y ? VerticalPenalty * cell : cell;
            float textureSeamBias = math.frac((world.x + (world.z * 0.37f)) * 0.0625f);
            return cost + (math.abs(textureSeamBias - 0.5f) * 0.02f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveHeuristic(int3 coord, int3 goal, float cell)
        {
            int3 delta = math.abs(goal - coord);
            return ((delta.x + delta.z) + (delta.y * VerticalPenalty)) * cell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 WorldFromCoord(int3 coord, float3 origin, float cell)
        {
            return origin + ((new float3(coord.x, coord.y, coord.z) - new float3(StartCoord, StartCoord, StartCoord)) * cell);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PackNode(int3 coord)
        {
            return coord.x + (coord.y * GridSide) + (coord.z * GridSideSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 UnpackNode(int node)
        {
            int z = node / GridSideSq;
            int remainder = node - (z * GridSideSq);
            int y = remainder / GridSide;
            int x = remainder - (y * GridSide);
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }
    }
}
