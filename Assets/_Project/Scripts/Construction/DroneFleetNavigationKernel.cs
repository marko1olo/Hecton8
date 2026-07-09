using Hecton8.Core.Contracts.Signals;
using Hecton8.Core;
using static Hecton8.Core.UnityMathematicsExtensions;
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
        public float SurvivalSteeringHz;
        [FieldOffset(28)]
        public float StandardSteeringHz;
        [FieldOffset(32)]
        public float HighFidelitySteeringHz;
        [FieldOffset(36)]
        public float OverkillSteeringHz;
        [FieldOffset(40)]
        public float AStarCellSize;
        [FieldOffset(44)]
        public float SurvivalSolveBudget;
        [FieldOffset(48)]
        public float StandardSolveBudget;
        [FieldOffset(52)]
        public float HighFidelitySolveBudget;
        [FieldOffset(56)]
        public float OverkillSolveBudget;
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
                SurvivalSteeringHz = 15f,
                StandardSteeringHz = 30f,
                HighFidelitySteeringHz = 60f,
                OverkillSteeringHz = 60f,
                AStarCellSize = 4f,
                SurvivalSolveBudget = 2f,
                StandardSolveBudget = 4f,
                HighFidelitySolveBudget = 8f,
                OverkillSolveBudget = 12f,
                Reserved0 = 0f
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneStateDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public uint CurrentTargetHashID;
        [FieldOffset(40)] public uint TaskStateFlags;
        [FieldOffset(44)] public float BatteryLevel;
        [FieldOffset(48)] private byte _pad0;
        [FieldOffset(49)] private byte _pad1;
        [FieldOffset(50)] private byte _pad2;
        [FieldOffset(51)] private byte _pad3;
        [FieldOffset(52)] private byte _pad4;
        [FieldOffset(53)] private byte _pad5;
        [FieldOffset(54)] private byte _pad6;
        [FieldOffset(55)] private byte _pad7;
        [FieldOffset(56)] private byte _pad8;
        [FieldOffset(57)] private byte _pad9;
        [FieldOffset(58)] private byte _pad10;
        [FieldOffset(59)] private byte _pad11;
        [FieldOffset(60)] private byte _pad12;
        [FieldOffset(61)] private byte _pad13;
        [FieldOffset(62)] private byte _pad14;
        [FieldOffset(63)] private byte _pad15;
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
        [FieldOffset(36)] public float ClearanceRadiusMeters;
        [FieldOffset(40)] private byte _pad0;
        [FieldOffset(41)] private byte _pad1;
        [FieldOffset(42)] private byte _pad2;
        [FieldOffset(43)] private byte _pad3;
        [FieldOffset(44)] private byte _pad4;
        [FieldOffset(45)] private byte _pad5;
        [FieldOffset(46)] private byte _pad6;
        [FieldOffset(47)] private byte _pad7;
        [FieldOffset(48)] private byte _pad8;
        [FieldOffset(49)] private byte _pad9;
        [FieldOffset(50)] private byte _pad10;
        [FieldOffset(51)] private byte _pad11;
        [FieldOffset(52)] private byte _pad12;
        [FieldOffset(53)] private byte _pad13;
        [FieldOffset(54)] private byte _pad14;
        [FieldOffset(55)] private byte _pad15;
        [FieldOffset(56)] private byte _pad16;
        [FieldOffset(57)] private byte _pad17;
        [FieldOffset(58)] private byte _pad18;
        [FieldOffset(59)] private byte _pad19;
        [FieldOffset(60)] private byte _pad20;
        [FieldOffset(61)] private byte _pad21;
        [FieldOffset(62)] private byte _pad22;
        [FieldOffset(63)] private byte _pad23;
    }

    internal static class DroneFleetLayoutSentinel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneStateDTO()
        {
            if (UnsafeUtility.SizeOf<DroneStateDTO>() != 64)
                return false;

#if UNITY_EDITOR
            return OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.CurrentAUP)) == 0 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.Velocity)) == 24 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.CurrentTargetHashID)) == 36 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.TaskStateFlags)) == 40 &&
                OffsetOf<DroneStateDTO>(nameof(DroneStateDTO.BatteryLevel)) == 44 &&
                OffsetOf<DroneStateDTO>("_pad0") == 48 &&
                OffsetOf<DroneStateDTO>("_pad4") == 52 &&
                OffsetOf<DroneStateDTO>("_pad8") == 56 &&
                OffsetOf<DroneStateDTO>("_pad15") == 63;
#else
            return true;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneTargetDTO()
        {
            if (UnsafeUtility.SizeOf<DroneTargetDTO>() != 64)
                return false;

#if UNITY_EDITOR
            return OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TargetAUP)) == 0 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.LocalPosition)) == 24 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TaskHash)) == 36 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TaskIndex)) == 40 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TargetModuleId)) == 44 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.Radius)) == 48 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.TaskKind)) == 52 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.Flags)) == 56 &&
                OffsetOf<DroneTargetDTO>(nameof(DroneTargetDTO.Reserved0)) == 60;
#else
            return true;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneTaskDTO()
        {
            if (UnsafeUtility.SizeOf<DroneTaskDTO>() != 32 ||
                UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>() != 16)
            {
                return false;
            }

#if UNITY_EDITOR
            return OffsetOf<DroneTaskDTO>(nameof(DroneTaskDTO.TargetEntityHash)) == 0 &&
                OffsetOf<DroneTaskDTO>(nameof(DroneTaskDTO.TaskTypeHash)) == 4 &&
                OffsetOf<DroneTaskDTO>(nameof(DroneTaskDTO.TaskProgress01)) == 8 &&
                OffsetOf<DroneTaskDTO>(nameof(DroneTaskDTO.TaskEfficiencyScalar)) == 12 &&
                OffsetOf<DroneTaskDTO>(nameof(DroneTaskDTO.InventoryPayloadHash)) == 16 &&
                OffsetOf<DroneTaskDTO>("_pad0") == 20 &&
                OffsetOf<DroneTaskDTO>("_pad4") == 24 &&
                OffsetOf<DroneTaskDTO>("_pad8") == 28 &&
                OffsetOf<DroneTaskDTO>("_pad11") == 31;
#else
            return true;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneAssignmentTaskDTO()
        {
            if (UnsafeUtility.SizeOf<DroneAssignmentTaskDTO>() != 64 ||
                UnsafeUtility.SizeOf<PathWaypointDTO>() != 64 ||
                UnsafeUtility.SizeOf<DroneAStarPersistentState>() != 64 ||
                UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>() != 16)
            {
                return false;
            }

#if UNITY_EDITOR
            return OffsetOf<PathWaypointDTO>(nameof(PathWaypointDTO.PositionAUP)) == 0 &&
                OffsetOf<PathWaypointDTO>(nameof(PathWaypointDTO.LocalPosition)) == 24 &&
                OffsetOf<PathWaypointDTO>(nameof(PathWaypointDTO.ActionCode)) == 36 &&
                OffsetOf<PathWaypointDTO>(nameof(PathWaypointDTO.NodeIndex)) == 40 &&
                OffsetOf<PathWaypointDTO>(nameof(PathWaypointDTO.Flags)) == 44 &&
                OffsetOf<PathWaypointDTO>("_pad0") == 48 &&
                OffsetOf<PathWaypointDTO>("_pad4") == 52 &&
                OffsetOf<PathWaypointDTO>("_pad8") == 56 &&
                OffsetOf<PathWaypointDTO>("_pad15") == 63 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.SearchHash)) == 0 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.OpenCount)) == 4 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.BestNode)) == 8 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.GoalNode)) == 12 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.IterationCount)) == 16 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.Active)) == 20 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.BestHeuristic)) == 24 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.CellSize)) == 28 &&
                OffsetOf<DroneAStarPersistentState>(nameof(DroneAStarPersistentState.Flags)) == 32 &&
                OffsetOf<DroneAStarPersistentState>("_pad0") == 40 &&
                OffsetOf<DroneAStarPersistentState>("_pad8") == 48 &&
                OffsetOf<DroneAStarPersistentState>("_pad16") == 56 &&
                OffsetOf<DroneAStarPersistentState>("_pad23") == 63;
#else
            return true;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneChassisSpecDTO()
        {
            if (UnsafeUtility.SizeOf<DroneChassisSpecDTO>() != 64)
                return false;

#if UNITY_EDITOR
            return OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.TypeHash)) == 0 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.Flags)) == 4 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.MaxSpeed)) == 8 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.BatteryCapacity)) == 12 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.BatteryDrainRate)) == 16 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.RepairSpeed)) == 20 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.CargoCapacity)) == 24 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.MiningHoldSeconds)) == 28 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.SdfRepulsionScale)) == 32 &&
                OffsetOf<DroneChassisSpecDTO>(nameof(DroneChassisSpecDTO.ClearanceRadiusMeters)) == 36 &&
                OffsetOf<DroneChassisSpecDTO>("_pad0") == 40 &&
                OffsetOf<DroneChassisSpecDTO>("_pad8") == 48 &&
                OffsetOf<DroneChassisSpecDTO>("_pad16") == 56 &&
                OffsetOf<DroneChassisSpecDTO>("_pad23") == 63;
#else
            return true;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateDroneSnapshotPayload()
        {
            if (UnsafeUtility.SizeOf<HectonDroneFleetSnapshotPayload>() != 48)
                return false;

#if UNITY_EDITOR
            return OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.ActiveHubCount)) == 0 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.ActiveDroneCount)) == 4 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.AssignedTaskCount)) == 8 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.DockedStasisSlotCount)) == 12 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.DestroyedDroneCount)) == 16 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.EmergencyLevel)) == 20 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.AverageBatteryPercent)) == 24 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.SolderReserve)) == 28 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.HostileDroneCount)) == 32 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.LogicLeechHijackCount)) == 36 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>(nameof(HectonDroneFleetSnapshotPayload.EmergencyOverclockActive)) == 40 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>("_padding0") == 41 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>("_padding1") == 42 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>("_padding2") == 43 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>("_padding3") == 44 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>("_padding4") == 45 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>("_padding5") == 46 &&
                OffsetOf<HectonDroneFleetSnapshotPayload>("_padding6") == 47;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
#endif
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DroneTaskAssignmentJob : IJobParallelFor
    {
        private const int UnclaimedTask = 0;
        private const int EmptyTaskIndex = -1;
        private const float MinimumScoreDistanceSq = 0.5625f;

        // SAFETY JUSTIFICATION 1/3: this assignment kernel executes one job index per drone slot.
        // `Drones`, `DroneStatesDto`, and `DroneTargets` are indexed only by `index`, so two workers do not
        // write the same row unless Unity violates the IJobParallelFor contract.
        // SAFETY JUSTIFICATION 2/3: `TaskClaimOwners` is the only cross-index write lane. It is accessed
        // through Interlocked.CompareExchange, so contested task claims are atomic and deterministic.
        // SAFETY JUSTIFICATION 3/3: all five arrays are separate Vault lanes with independent BufferIDs;
        // `[NoAlias]` documents that the slices do not overlap and lets Burst keep vector-safe assumptions.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessDroneState> Drones;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneStateDTO> DroneStatesDto;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTargetDTO> DroneTargets;
        [ReadOnly, NoAlias] public NativeArray<DroneAssignmentTaskDTO> Tasks;
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
            DroneAssignmentTaskDTO bestTask = default;
            int count = math.min(TaskCount, math.min(Tasks.Length, TaskClaimOwners.Length));
            for (int taskIndex = 0; taskIndex < count; taskIndex++)
            {
                DroneAssignmentTaskDTO task = Tasks[taskIndex];
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
                if (IsFinite(dto.CurrentAUP))
                    return dto.CurrentAUP;
            }

            return global::Hecton8.World.AUPMath.ToDouble3(drone.Position);
        }

        private void MirrorDto(int index, in HeadlessDroneState drone, uint taskHash)
        {
            if (!DroneStatesDto.IsCreated || (uint)index >= (uint)DroneStatesDto.Length)
                return;

            DroneStateDTO* statePtr = (DroneStateDTO*)DroneStatesDto.GetUnsafePtr();
            ref DroneStateDTO dto = ref UnsafeUtility.AsRef<DroneStateDTO>(statePtr + index);
            dto.CurrentAUP = ResolveDroneAup(index, in drone);
            dto.Velocity = drone.Velocity;
            dto.CurrentTargetHashID = taskHash;
            dto.TaskStateFlags = PackFlags(in drone);
            dto.BatteryLevel = math.clamp(drone.BatteryPercent, 0f, 100f);
        }

        private static uint ResolveTaskHash(int taskIndex, in DroneAssignmentTaskDTO task)
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
            if (!IsFinite(delta) || math.any(math.abs(delta) > (double)float.MaxValue))
                return new float3(float.NaN);

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
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DroneMetabolismJob : IJobParallelFor
    {
        private const int EmptyTaskIndex = -1;
        private const float ReturnBatteryThresholdPercent = 15f;
        private const float LengthEpsilonSq = 0.00000001f;

        // SAFETY JUSTIFICATION 1/3: metabolism mutates only the drone row matching Execute index.
        // SAFETY JUSTIFICATION 2/3: DTO mirrors are written at the same index as the drone source row;
        // no cross-drone accumulation or shared counter mutation occurs in this job.
        // SAFETY JUSTIFICATION 3/3: drone state, DTO, and target lanes are separate Vault buffers, so
        // `[NoAlias]` is valid and the disabled parallel restriction does not hide overlapping storage.
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
            float speed = FastLengthFromSq(math.lengthsq(drone.Velocity));
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
            else
            {
                float distToBase = FastLengthFromSq(math.distancesq(drone.Position, drone.HomePosition));
                float batteryLevel01 = drone.BatteryPercent * 0.01f;
                float batteryDrainPerMeter = ((drone.BatteryDrainPerSecond * 0.01f) * drainScale) / maxSpeed;
                bool mustReturn = Hecton8.PureLogic.Systems.DroneBatteryReturnThresholdCalculator.Compute(
                    batteryLevel01, distToBase, batteryDrainPerMeter, ReturnBatteryThresholdPercent * 0.01f);

                if (mustReturn &&
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
            dto.CurrentAUP = IsFinite(drone.PositionAup) ? drone.PositionAup : global::Hecton8.World.AUPMath.ToDouble3(drone.Position);
            dto.Velocity = drone.Velocity;
            dto.CurrentTargetHashID = math.hash(new uint3((uint)math.max(0, drone.TargetTaskIndex + 1), (uint)math.max(0, drone.DroneId), (uint)drone.State));
            dto.TaskStateFlags = ((uint)drone.State) | ((uint)drone.FactionBit << 8) | ((uint)drone.CorridorTight << 16);
            dto.BatteryLevel = math.clamp(drone.BatteryPercent, 0f, 100f);
        }

        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastLengthFromSq(float lengthSq)
        {
            float safeLengthSq = math.max(0f, lengthSq);
            return safeLengthSq * math.rsqrt(math.max(safeLengthSq, LengthEpsilonSq));
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
        // SAFETY JUSTIFICATION 1/3: matrix extraction writes `DroneStatesDto[index]` only for the current
        // drone row; workers do not write neighboring DTO rows through shared indices.
        // SAFETY JUSTIFICATION 2/3: render matrix output is a separate lane and is also written at index.
        // SAFETY JUSTIFICATION 3/3: source drones, DTO mirror, and matrix lanes are independent Vault buffers;
        // disabling the restriction removes false-positive alias constraints without changing ownership.
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
                if (IsFinite(dto.CurrentAUP))
                    positionAup = dto.CurrentAUP;
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PathWaypointDTO
    {
        [FieldOffset(0)]
        public double3 PositionAUP;
        [FieldOffset(24)]
        public float3 LocalPosition;
        [FieldOffset(36)]
        public uint ActionCode;
        [FieldOffset(40)]
        public uint NodeIndex;
        [FieldOffset(44)]
        public uint Flags;
        [FieldOffset(48)] private byte _pad0;
        [FieldOffset(49)] private byte _pad1;
        [FieldOffset(50)] private byte _pad2;
        [FieldOffset(51)] private byte _pad3;
        [FieldOffset(52)] private byte _pad4;
        [FieldOffset(53)] private byte _pad5;
        [FieldOffset(54)] private byte _pad6;
        [FieldOffset(55)] private byte _pad7;
        [FieldOffset(56)] private byte _pad8;
        [FieldOffset(57)] private byte _pad9;
        [FieldOffset(58)] private byte _pad10;
        [FieldOffset(59)] private byte _pad11;
        [FieldOffset(60)] private byte _pad12;
        [FieldOffset(61)] private byte _pad13;
        [FieldOffset(62)] private byte _pad14;
        [FieldOffset(63)] private byte _pad15;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
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
        [FieldOffset(144)]
        public float3 ClosedPoint0;
        [FieldOffset(156)]
        public float3 ClosedPoint1;
        [FieldOffset(168)]
        public float3 ClosedPoint2;
        [FieldOffset(180)]
        public float3 ClosedPoint3;
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DroneTransactionTelemetrySnapshot
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int TransactionCount;
        [FieldOffset(12)] public int RepairCount;
        [FieldOffset(16)] public int MiningCount;
        [FieldOffset(20)] public int InventoryAdds;
        [FieldOffset(24)] public int AtomicConflicts;
        [FieldOffset(28)] public int VfxSignals;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float EstimatedMicroseconds;
        [FieldOffset(40)] public uint FaultFlags;
        [FieldOffset(44)] public uint LastTargetHash;
        [FieldOffset(48)] public int ActiveInventorySlots;
        [FieldOffset(52)] public int CommandCount;
        [FieldOffset(56)] public uint LayoutHash;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct DroneTransactionDebugTask
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Target;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public uint TargetEntityHash;
        [FieldOffset(40)] public uint TaskTypeHash;
        [FieldOffset(44)] public float Progress01;
        [FieldOffset(48)] public float VfxIntensity01;
        [FieldOffset(52)] public int DroneId;
        [FieldOffset(56)] public int Slot;
        [FieldOffset(60)] public int InventorySlot;
        [FieldOffset(64)] public uint InventoryHash;
        [FieldOffset(68)] public int InventoryQuantityAdded;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] public uint ActiveInventorySlots;
        [FieldOffset(80)] public uint AtomicConflicts;
        [FieldOffset(84)] public float BatteryPercent;
        [FieldOffset(88)] public uint StateFlags;
        [FieldOffset(92)] private byte _pad0;
        [FieldOffset(93)] private byte _pad1;
        [FieldOffset(94)] private byte _pad2;
        [FieldOffset(95)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DroneFleetRepairServiceSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int TargetModuleId;
        [FieldOffset(8)] public float RepairUnits;
        [FieldOffset(12)] public float3 Position;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DroneFleetMiningServiceSignal : ISignal
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct DroneTaskDTO
    {
        [FieldOffset(0)]
        public uint TargetEntityHash;
        [FieldOffset(4)]
        public uint TaskTypeHash;
        [FieldOffset(8)]
        public float TaskProgress01;
        [FieldOffset(12)]
        public float TaskEfficiencyScalar;
        [FieldOffset(16)]
        public uint InventoryPayloadHash;
        [FieldOffset(20)] private byte _pad0;
        [FieldOffset(21)] private byte _pad1;
        [FieldOffset(22)] private byte _pad2;
        [FieldOffset(23)] private byte _pad3;
        [FieldOffset(24)] private byte _pad4;
        [FieldOffset(25)] private byte _pad5;
        [FieldOffset(26)] private byte _pad6;
        [FieldOffset(27)] private byte _pad7;
        [FieldOffset(28)] private byte _pad8;
        [FieldOffset(29)] private byte _pad9;
        [FieldOffset(30)] private byte _pad10;
        [FieldOffset(31)] private byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneAssignmentTaskDTO
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

    internal struct DroneSdfGrid
    {
        private const float InvEncodedByteMax = 0.0039215686274509803f;
        private const float MinimumCellSize = 0.0001f;

        [ReadOnly] public NativeArray<byte>.ReadOnly EncodedSdf;
        public int3 Dimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public float RepulsionDistance;
        public int Enabled;
        public uint Version;

        public static bool TryCreate(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 dimensions,
            float3 volumeOrigin,
            float3 cellSize,
            float sdfRange,
            float repulsionDistance,
            int version,
            out DroneSdfGrid grid)
        {
            grid = default;
            if (!TryResolveExpectedVoxelCount(dimensions, out int expectedLength) ||
                !encodedSdf.IsCreated ||
                encodedSdf.Length < expectedLength ||
                !IsFinite(volumeOrigin) ||
                !IsFinite(cellSize) ||
                !math.all(cellSize > new float3(MinimumCellSize)) ||
                !math.isfinite(sdfRange) ||
                sdfRange <= MinimumCellSize)
            {
                return false;
            }

            grid = new DroneSdfGrid
            {
                EncodedSdf = encodedSdf,
                Dimensions = dimensions,
                VolumeOrigin = volumeOrigin,
                CellSize = cellSize,
                SdfRange = sdfRange,
                RepulsionDistance = math.max(0.001f, FiniteOrFallback(repulsionDistance, 2.25f)),
                Enabled = 1,
                Version = version > 0 ? (uint)version : 1u
            };
            return true;
        }

        public bool IsValid =>
            Enabled != 0 &&
            EncodedSdf.IsCreated &&
            TryResolveExpectedVoxelCount(Dimensions, out int expectedLength) &&
            EncodedSdf.Length >= expectedLength &&
            IsFinite(VolumeOrigin) &&
            IsFinite(CellSize) &&
            math.all(CellSize > new float3(MinimumCellSize)) &&
            math.isfinite(SdfRange) &&
            SdfRange > MinimumCellSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlocked(float3 position)
        {
            return IsBlockedForRadius(position, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlockedForRadius(float3 position, float requiredRadius)
        {
            if (!TrySampleClearance(position, out float clearance))
                return true;

            return clearance < math.max(0f, requiredRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleClearance(float3 position)
        {
            return TrySampleClearance(position, out float clearance) ? clearance : -math.max(0.001f, SdfRange);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySampleRepulsion(float3 position, out float3 normal, out float distance)
        {
            normal = float3.zero;
            distance = float.MaxValue;

            if (!TrySampleClearance(position, out float clearance))
                return false;

            float repelDistance = math.max(0.001f, RepulsionDistance);
            if (clearance > repelDistance)
                return false;

            float step = math.max(MinimumCellSize, math.cmin(CellSize) * 0.75f);
            float center = clearance;
            float3 gradient = new float3(
                SampleClearanceOr(position + new float3(step, 0f, 0f), center) - SampleClearanceOr(position - new float3(step, 0f, 0f), center),
                SampleClearanceOr(position + new float3(0f, step, 0f), center) - SampleClearanceOr(position - new float3(0f, step, 0f), center),
                SampleClearanceOr(position + new float3(0f, 0f, step), center) - SampleClearanceOr(position - new float3(0f, 0f, step), center));

            normal = SafeNormalize(gradient, new float3(0f, 1f, 0f));
            distance = math.max(0.04f, math.max(0f, clearance));
            return IsFinite(normal);
        }

        private bool TrySampleClearance(float3 position, out float clearance)
        {
            clearance = -math.max(0.001f, SdfRange);
            if (!IsValid || !IsFinite(position))
                return false;

            float3 sample = (position - VolumeOrigin) * math.rcp(math.max(CellSize, new float3(MinimumCellSize)));
            float3 maxSample = new float3(Dimensions.x - 1, Dimensions.y - 1, Dimensions.z - 1);
            if (math.any(sample < float3.zero) || math.any(sample > maxSample))
            {
                clearance = -math.max(0.001f, SdfRange);
                return true;
            }

            sample = math.clamp(sample, float3.zero, math.max(float3.zero, maxSample - new float3(0.001f)));
            int3 p0 = new int3((int)math.floor(sample.x), (int)math.floor(sample.y), (int)math.floor(sample.z));
            int3 p1 = new int3(
                math.min(p0.x + 1, Dimensions.x - 1),
                math.min(p0.y + 1, Dimensions.y - 1),
                math.min(p0.z + 1, Dimensions.z - 1));
            float3 t = sample - new float3(p0.x, p0.y, p0.z);

            float c000 = DecodeSdf(SdfIndex(p0.x, p0.y, p0.z), SdfRange);
            float c100 = DecodeSdf(SdfIndex(p1.x, p0.y, p0.z), SdfRange);
            float c010 = DecodeSdf(SdfIndex(p0.x, p1.y, p0.z), SdfRange);
            float c110 = DecodeSdf(SdfIndex(p1.x, p1.y, p0.z), SdfRange);
            float c001 = DecodeSdf(SdfIndex(p0.x, p0.y, p1.z), SdfRange);
            float c101 = DecodeSdf(SdfIndex(p1.x, p0.y, p1.z), SdfRange);
            float c011 = DecodeSdf(SdfIndex(p0.x, p1.y, p1.z), SdfRange);
            float c111 = DecodeSdf(SdfIndex(p1.x, p1.y, p1.z), SdfRange);

            float cx00 = math.lerp(c000, c100, t.x);
            float cx10 = math.lerp(c010, c110, t.x);
            float cx01 = math.lerp(c001, c101, t.x);
            float cx11 = math.lerp(c011, c111, t.x);
            float cy0 = math.lerp(cx00, cx10, t.y);
            float cy1 = math.lerp(cx01, cx11, t.y);
            clearance = math.lerp(cy0, cy1, t.z);
            return math.isfinite(clearance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleClearanceOr(float3 position, float fallback)
        {
            return TrySampleClearance(position, out float clearance) ? clearance : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DecodeSdf(int index, float sdfRange)
        {
            if ((uint)index >= (uint)EncodedSdf.Length)
                return -math.max(0.001f, sdfRange);

            return ((EncodedSdf[index] * InvEncodedByteMax) * 2f - 1f) * sdfRange;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SdfIndex(int x, int y, int z)
        {
            return (z * Dimensions.y + y) * Dimensions.x + x;
        }

        private static bool TryResolveExpectedVoxelCount(int3 dimensions, out int expectedLength)
        {
            expectedLength = 0;
            if (!math.all(dimensions > 1))
                return false;

            long expectedLong = (long)dimensions.x * dimensions.y * dimensions.z;
            if (expectedLong <= 0L || expectedLong > int.MaxValue)
                return false;

            expectedLength = (int)expectedLong;
            return true;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOrFallback(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
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

    internal ref struct DroneNativeMinHeap
    {
        public NativeArray<DroneNativeMinHeapNode> Nodes;
        public int BaseOffset;
        public int Capacity;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }

        public bool TryPush(int nodeIndex, float cost)
        {
            int capacity = ResolveCapacity();
            if (!Nodes.IsCreated || Count >= capacity)
                return false;

            int cursor = Count++;
            Set(cursor, new DroneNativeMinHeapNode { NodeIndex = nodeIndex, Cost = cost });
            while (cursor > 0)
            {
                int parent = (cursor - 1) >> 1;
                if (Get(parent).Cost <= Get(cursor).Cost)
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

            DroneNativeMinHeapNode root = Get(0);
            Count--;
            if (Count > 0)
                Set(0, Get(Count));

            int cursor = 0;
            while (true)
            {
                int left = (cursor << 1) + 1;
                int right = left + 1;
                if (left >= Count)
                    break;

                int best = right < Count && Get(right).Cost < Get(left).Cost ? right : left;
                if (Get(cursor).Cost <= Get(best).Cost)
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
            DroneNativeMinHeapNode tmp = Get(a);
            Set(a, Get(b));
            Set(b, tmp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveCapacity()
        {
            int remaining = Nodes.IsCreated ? Nodes.Length - math.max(0, BaseOffset) : 0;
            int requested = Capacity > 0 ? Capacity : remaining;
            return math.max(0, math.min(requested, remaining));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DroneNativeMinHeapNode Get(int index)
        {
            return Nodes[BaseOffset + index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Set(int index, DroneNativeMinHeapNode value)
        {
            Nodes[BaseOffset + index] = value;
        }
    }

    internal ref struct DroneTaskNativeMinHeap
    {
        public NativeArray<DroneAssignmentTaskDTO> Nodes;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }

        public bool TryPush(in DroneAssignmentTaskDTO node)
        {
            if (!Nodes.IsCreated || Count >= Nodes.Length)
                return false;

            int cursor = Count++;
            Nodes[cursor] = node;
            while (cursor > 0)
            {
                int parent = (cursor - 1) >> 1;
                DroneAssignmentTaskDTO parentNode = Nodes[parent];
                DroneAssignmentTaskDTO cursorNode = Nodes[cursor];
                if (LessThanOrEqual(in parentNode, in cursorNode))
                    break;

                Swap(parent, cursor);
                cursor = parent;
            }

            return true;
        }

        public bool TryPop(out DroneAssignmentTaskDTO node)
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
                    DroneAssignmentTaskDTO rightNode = Nodes[right];
                    DroneAssignmentTaskDTO leftNode = Nodes[left];
                    if (LessThan(in rightNode, in leftNode))
                        best = right;
                }

                DroneAssignmentTaskDTO cursorNode = Nodes[cursor];
                DroneAssignmentTaskDTO bestNode = Nodes[best];
                if (LessThanOrEqual(in cursorNode, in bestNode))
                    break;

                Swap(cursor, best);
                cursor = best;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LessThan(in DroneAssignmentTaskDTO a, in DroneAssignmentTaskDTO b)
        {
            if (a.Priority < b.Priority)
                return true;
            if (a.Priority > b.Priority)
                return false;

            return a.Score > b.Score;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LessThanOrEqual(in DroneAssignmentTaskDTO a, in DroneAssignmentTaskDTO b)
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
            DroneAssignmentTaskDTO tmp = Nodes[a];
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneAStarPersistentState
    {
        [FieldOffset(0)] public uint SearchHash;
        [FieldOffset(4)] public int OpenCount;
        [FieldOffset(8)] public int BestNode;
        [FieldOffset(12)] public int GoalNode;
        [FieldOffset(16)] public int IterationCount;
        [FieldOffset(20)] public int Active;
        [FieldOffset(24)] public float BestHeuristic;
        [FieldOffset(28)] public float CellSize;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] private byte _pad0;
        [FieldOffset(41)] private byte _pad1;
        [FieldOffset(42)] private byte _pad2;
        [FieldOffset(43)] private byte _pad3;
        [FieldOffset(44)] private byte _pad4;
        [FieldOffset(45)] private byte _pad5;
        [FieldOffset(46)] private byte _pad6;
        [FieldOffset(47)] private byte _pad7;
        [FieldOffset(48)] private byte _pad8;
        [FieldOffset(49)] private byte _pad9;
        [FieldOffset(50)] private byte _pad10;
        [FieldOffset(51)] private byte _pad11;
        [FieldOffset(52)] private byte _pad12;
        [FieldOffset(53)] private byte _pad13;
        [FieldOffset(54)] private byte _pad14;
        [FieldOffset(55)] private byte _pad15;
        [FieldOffset(56)] private byte _pad16;
        [FieldOffset(57)] private byte _pad17;
        [FieldOffset(58)] private byte _pad18;
        [FieldOffset(59)] private byte _pad19;
        [FieldOffset(60)] private byte _pad20;
        [FieldOffset(61)] private byte _pad21;
        [FieldOffset(62)] private byte _pad22;
        [FieldOffset(63)] private byte _pad23;
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
        private const float LengthEpsilonSq = 0.00000001f;

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
        [NoAlias] public NativeArray<DroneAStarPersistentState> SearchStates;
        public DroneSdfGrid SdfGrid;
        public int FrameIndex;
        public int MaxSolves;
        public int RouteNodeStride;
        public float CellSize;
        public int MaxNodesExpandedPerDrone;
        public float HeuristicWeight;
        public float RequiredDroneRadius;

        public void Execute()
        {
            if (!Drones.IsCreated || !Waypoints.IsCreated || !WaypointStates.IsCreated ||
                !OpenHeap.IsCreated || !GCosts.IsCreated || !CameFrom.IsCreated || !NodeStates.IsCreated ||
                !SearchStates.IsCreated)
            {
                return;
            }

            int droneLimit = math.min(Drones.Length, math.min(SearchStates.Length, math.min(Waypoints.Length, WaypointStates.Length)));
            if (droneLimit <= 0)
                return;

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
                WriteWaypoint(droneIndex, destination, 1u, (uint)StartNode, 1u, in drone);
                WaypointStates[droneIndex] = 1;
                WriteRouteNodes(droneIndex, StartNode, math.max(1, RouteNodeStride));
                return 1;
            }

            int3 goalCoord = ResolveGoalCoord(toDestination, cell);
            int goalNode = PackNode(goalCoord);
            int nodeBase = ResolveNodeBase(droneIndex);
            if (!HasNodeSlice(nodeBase))
                return 2;

            float heuristicWeight = math.max(1f, HeuristicWeight);
            float requiredRadius = ResolveDroneRequiredRadius(in drone);
            int maxExpansions = math.clamp(MaxNodesExpandedPerDrone, 16, NodeCapacity);
            uint searchHash = ResolveSearchHash(in drone, goalCoord);
            DroneAStarPersistentState search = SearchStates[droneIndex];
            bool resume = search.Active != 0 &&
                search.SearchHash == searchHash &&
                search.GoalNode == goalNode &&
                math.abs(search.CellSize - cell) <= 0.0001f &&
                search.OpenCount > 0;

            DroneNativeMinHeap heap = new DroneNativeMinHeap
            {
                Nodes = OpenHeap,
                BaseOffset = nodeBase,
                Capacity = NodeCapacity,
                Count = resume ? math.clamp(search.OpenCount, 0, NodeCapacity) : 0
            };

            int bestNode = resume ? math.clamp(search.BestNode, 0, NodeCapacity - 1) : StartNode;
            float bestHeuristic = resume && math.isfinite(search.BestHeuristic)
                ? search.BestHeuristic
                : ResolveHeuristic(UnpackNode(StartNode), goalCoord, cell, heuristicWeight);

            if (!resume)
            {
                ClearScratch(nodeBase);
                SetGCost(nodeBase, StartNode, 0f);
                SetCameFrom(nodeBase, StartNode, -1);
                SetNodeState(nodeBase, StartNode, 1);
                heap.TryPush(StartNode, ResolveHeuristic(UnpackNode(StartNode), goalCoord, cell, heuristicWeight));
            }

            int localIterations = 0;
            bool complete = false;

            while (heap.TryPop(out int current, out _) && localIterations < maxExpansions)
            {
                localIterations++;
                if (GetNodeState(nodeBase, current) == 2)
                    continue;

                SetNodeState(nodeBase, current, 2);
                int3 currentCoord = UnpackNode(current);
                float heuristic = ResolveHeuristic(currentCoord, goalCoord, cell, heuristicWeight);
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
                    TryVisitNeighbor(nodeBase, current, currentCoord, goalNode, goalCoord, drone.Position, cell, requiredRadius, heuristicWeight, direction, ref heap);
            }

            iterationAccumulator += localIterations;
            int pathNode = complete ? goalNode : bestNode;
            search.SearchHash = searchHash;
            search.OpenCount = heap.Count;
            search.BestNode = bestNode;
            search.GoalNode = goalNode;
            search.IterationCount = math.min(int.MaxValue, search.IterationCount + localIterations);
            search.Active = complete || heap.Count <= 0 ? 0 : 1;
            search.BestHeuristic = bestHeuristic;
            search.CellSize = cell;
            search.Flags = complete ? 1u : (heap.Count > 0 ? 2u : 4u);
            SearchStates[droneIndex] = search;

            if (pathNode == StartNode)
            {
                if (SdfGrid.TrySampleRepulsion(drone.Position, out float3 normal, out _))
                {
                    WriteWaypoint(droneIndex, drone.Position + (normal * cell), 2u, (uint)StartNode, 4u, in drone);
                    WaypointStates[droneIndex] = 2;
                    return 2;
                }

                return 2;
            }

            float3 waypoint = ResolveStringPulledWaypoint(nodeBase, pathNode, goalNode, complete, drone.Position, destination, cell, requiredRadius);
            WriteRouteNodes(nodeBase, droneIndex, pathNode, math.max(1, RouteNodeStride));
            WriteWaypoint(droneIndex, waypoint, complete ? 1u : 2u, (uint)math.max(0, pathNode), complete ? 1u : 2u, in drone);
            WaypointStates[droneIndex] = complete ? (byte)1 : (byte)2;
            return complete ? (byte)1 : (byte)2;
        }

        private void WriteWaypoint(
            int droneIndex,
            float3 localPosition,
            uint actionCode,
            uint nodeIndex,
            uint flags,
            in HeadlessDroneState drone)
        {
            if ((uint)droneIndex >= (uint)Waypoints.Length)
                return;

            Waypoints[droneIndex] = new PathWaypointDTO
            {
                PositionAUP = ResolveWaypointAup(in drone, localPosition),
                LocalPosition = localPosition,
                ActionCode = actionCode,
                NodeIndex = nodeIndex,
                Flags = flags
            };
        }

        private void WriteRouteNodes(int droneIndex, int pathNode, int stride)
        {
            WriteRouteNodes(ResolveNodeBase(droneIndex), droneIndex, pathNode, stride);
        }

        private void WriteRouteNodes(int nodeBase, int droneIndex, int pathNode, int stride)
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
                current = GetCameFrom(nodeBase, current);
            }

            RouteNodeCounts[droneIndex] = (byte)math.min(count, 255);
        }

        private void TryVisitNeighbor(
            int nodeBase,
            int current,
            int3 currentCoord,
            int goalNode,
            int3 goalCoord,
            float3 origin,
            float cell,
            float requiredRadius,
            float heuristicWeight,
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
            if (GetNodeState(nodeBase, neighbor) == 2)
                return;

            float3 world = WorldFromCoord(neighborCoord, origin, cell);
            if (neighbor != goalNode && SdfGrid.IsBlockedForRadius(world, requiredRadius))
                return;

            float tentativeG = GetGCost(nodeBase, current) + ResolveStepCost(currentCoord, neighborCoord, world, cell);
            if (tentativeG >= GetGCost(nodeBase, neighbor))
                return;

            SetCameFrom(nodeBase, neighbor, current);
            SetGCost(nodeBase, neighbor, tentativeG);
            SetNodeState(nodeBase, neighbor, 1);
            heap.TryPush(neighbor, tentativeG + ResolveHeuristic(neighborCoord, goalCoord, cell, heuristicWeight));
        }

        private float3 ResolveStringPulledWaypoint(
            int nodeBase,
            int pathNode,
            int goalNode,
            bool complete,
            float3 origin,
            float3 destination,
            float cell,
            float requiredRadius)
        {
            float3 fallback = ResolveFirstStep(nodeBase, pathNode, origin, destination, cell);
            int current = pathNode;
            int guard = 0;
            while (current >= 0 && current != StartNode && guard++ < NodeCapacity)
            {
                float3 candidate = complete && current == goalNode
                    ? destination
                    : WorldFromCoord(UnpackNode(current), origin, cell);
                if (HasLineClearance(origin, candidate, cell, requiredRadius))
                    return candidate;

                current = GetCameFrom(nodeBase, current);
            }

            return fallback;
        }

        private bool HasLineClearance(float3 start, float3 end, float cell, float requiredRadius)
        {
            float3 delta = end - start;
            float distanceSq = math.lengthsq(delta);
            if (!math.isfinite(distanceSq))
                return false;

            if (distanceSq <= 0.0001f)
                return true;

            float distance = FastLengthFromSq(distanceSq);
            int samples = math.clamp((int)math.ceil(distance * math.rcp(math.max(0.25f, cell * 0.5f))), 1, 16);
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i * math.rcp((float)samples + 1f);
                float3 point = start + (delta * t);
                if (SdfGrid.IsBlockedForRadius(point, requiredRadius))
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastLengthFromSq(float lengthSq)
        {
            float safeLengthSq = math.max(0f, lengthSq);
            return safeLengthSq * math.rsqrt(math.max(safeLengthSq, LengthEpsilonSq));
        }

        private float3 ResolveFirstStep(int nodeBase, int pathNode, float3 origin, float3 destination, float cell)
        {
            int current = pathNode;
            int parent = GetCameFrom(nodeBase, current);
            int guard = 0;
            while (parent >= 0 && parent != StartNode && guard++ < NodeCapacity)
            {
                current = parent;
                parent = GetCameFrom(nodeBase, current);
            }

            if (current == pathNode && parent < 0)
                return destination;

            return WorldFromCoord(UnpackNode(current), origin, cell);
        }

        private void ClearScratch(int nodeBase)
        {
            int limit = math.min(NodeCapacity, math.min(GCosts.Length - nodeBase, math.min(CameFrom.Length - nodeBase, NodeStates.Length - nodeBase)));
            for (int i = 0; i < limit; i++)
            {
                GCosts[nodeBase + i] = HugeCost;
                CameFrom[nodeBase + i] = -1;
                NodeStates[nodeBase + i] = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveNodeBase(int droneIndex)
        {
            return math.max(0, droneIndex) * NodeCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasNodeSlice(int nodeBase)
        {
            return nodeBase >= 0 &&
                nodeBase + NodeCapacity <= OpenHeap.Length &&
                nodeBase + NodeCapacity <= GCosts.Length &&
                nodeBase + NodeCapacity <= CameFrom.Length &&
                nodeBase + NodeCapacity <= NodeStates.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetGCost(int nodeBase, int node)
        {
            return GCosts[nodeBase + node];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetGCost(int nodeBase, int node, float value)
        {
            GCosts[nodeBase + node] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCameFrom(int nodeBase, int node)
        {
            return CameFrom[nodeBase + node];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCameFrom(int nodeBase, int node, int value)
        {
            CameFrom[nodeBase + node] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetNodeState(int nodeBase, int node)
        {
            return NodeStates[nodeBase + node];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetNodeState(int nodeBase, int node, byte value)
        {
            NodeStates[nodeBase + node] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveSearchHash(in HeadlessDroneState drone, int3 goalCoord)
        {
            return math.hash(new uint4(
                (uint)math.max(0, drone.TargetTaskIndex + 1),
                (uint)drone.State,
                (uint)PackNode(goalCoord),
                (uint)math.max(0, drone.DroneId)));
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
            double3 destinationAup = ResolveDestinationAup(in drone);
            if (IsFinite(destinationAup) && IsFinite(drone.PositionAup))
            {
                float3 localDelta = ToFloat3(destinationAup - drone.PositionAup);
                if (IsFinite(localDelta))
                    return drone.Position + localDelta;
            }

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
        private static double3 ResolveDestinationAup(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking)
            {
                return drone.HomeAup;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel)
                return drone.SupplyAup;

            return drone.TargetAup;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ResolveWaypointAup(in HeadlessDroneState drone, float3 localPosition)
        {
            if (IsFinite(drone.PositionAup) && IsFinite(drone.Position) && IsFinite(localPosition))
                return drone.PositionAup + global::Hecton8.World.AUPMath.ToDouble3(localPosition - drone.Position);

            return global::Hecton8.World.AUPMath.ToDouble3(localPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveDroneRequiredRadius(in HeadlessDroneState drone)
        {
            float encodedRadius = math.asfloat(drone.ReservedTail0);
            float radius = math.max(math.max(0f, RequiredDroneRadius), math.isfinite(encodedRadius) ? encodedRadius : 0f);
            return math.clamp(radius, 0.2f, 2f);
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
            float textureSeamBias = math.frac((world.x + (world.z * 0.37f)) * 0.0625f);
            int3 delta = math.abs(neighbor - current);
            int distanceSq = (delta.x * delta.x) + (delta.y * delta.y) + (delta.z * delta.z);
            float distance = distanceSq == 1 ? 1f : distanceSq == 2 ? 1.41421356f : distanceSq == 3 ? 1.73205081f : math.sqrt(math.max(0f, distanceSq));
            float verticalHazard = current.y != neighbor.y ? (VerticalPenalty - 1f) * cell : 0f;
            float seamHazard = math.abs(textureSeamBias - 0.5f) * 0.02f;
            float hazardSum = math.max(0f, verticalHazard) + math.max(0f, seamHazard);
            float safeBaseCost = math.max(0f, cell);
            float result = (distance * safeBaseCost) + hazardSum;
            return math.isfinite(result) && result >= 0f ? result : float.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveHeuristic(int3 coord, int3 goal, float cell, float heuristicWeight)
        {
            int3 delta = math.abs(goal - coord);
            return ((delta.x + delta.z) + (delta.y * VerticalPenalty)) * cell * math.max(1f, heuristicWeight);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
        }
    }
}
