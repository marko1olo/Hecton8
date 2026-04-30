using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    internal enum HeadlessDroneRuntimeState : byte
    {
        Empty = 0,
        Idle = 1,
        Travel = 2,
        Repair = 3,
        Return = 4,
        ResupplyTravel = 5,
        Stasis = 6,
        Attack = 7,
        Sacrificed = 8,
        Completed = 9,
        ResupplyDocked = 10
    }

    internal enum HeadlessDroneFactionBit : byte
    {
        Friendly = 1,
        Hostile = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HeadlessDroneState
    {
        public int DroneId;
        public int HubGridId;
        public int HubSlot;
        public int TargetTaskIndex;
        public int TargetModuleId;
        public int SolderUnits;
        public int LoadedSolderCapacity;
        public byte State;
        public byte FactionBit;
        public byte CorridorTight;
        public byte Reserved0;
        public float BatteryPercent;
        public float RepairAccumulator;
        public float ServiceRadius;
        public float MaxSpeed;
        public float BatteryDrainPerSecond;
        public float RepairRatePerSecond;
        public float WeldPowerNormalized;
        public float WeldRangeMeters;
        public float3 Position;
        public float3 Velocity;
        public float3 HomePosition;
        public float3 TargetPosition;
        public float3 SupplyPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HeadlessDroneTask
    {
        public int TaskIndex;
        public int ModuleId;
        public int HubGridId;
        public byte Kind;
        public byte RequiredFaction;
        public byte Reserved0;
        public byte Reserved1;
        public float Criticality;
        public float Radius;
        public float3 Position;
    }

    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DroneCognitionJob : IJobParallelFor
    {
        private const int UnclaimedTask = 0;
        private const float MinimumScoreDistanceSq = 0.5625f;
        private const float MinimumVectorLengthSq = 0.000001f;
        private const float SeparationRadiusMeters = 2f;
        private const float SeparationRadiusSq = SeparationRadiusMeters * SeparationRadiusMeters;
        private const float PlayerSeparationRadiusSq = 6.25f;
        private const float SeparationWeight = 3.25f;
        private const float AlignmentWeight = 0.25f;
        private const float OpenCohesionWeight = 0.8f;
        private const float CorridorCohesionWeight = 0.1f;
        private const float MaxSteering = 8f;
        private const float EmergencySpeedMultiplier = 3f;
        private const float EmergencyBatteryDrainMultiplier = 5f;
        private const float SpatialCellSize = 2f;
        private const float SpatialBoundsMin = -512f;
        private const int SpatialGridResolution = 512;
        private const int EmptyTaskIndex = -1;

        [ReadOnly] public NativeArray<HeadlessDroneState> ReadDrones;
        public NativeArray<HeadlessDroneState> Drones;
        public NativeArray<float4x4> RenderMatrices;

        [ReadOnly] public NativeParallelMultiHashMap<int, HeadlessDroneTask> TasksByGrid;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> DroneSpatialHash;

        [NativeDisableParallelForRestriction] public NativeArray<int> TaskClaimOwners;

        public float DeltaTime;
        public float3 PlayerPosition;
        public int PlayerPositionValid;
        public int EmergencyOverclock;

        public void Execute(int index)
        {
            HeadlessDroneState drone = ReadDrones[index];
            if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                drone.State == (byte)HeadlessDroneRuntimeState.Completed)
            {
                Drones[index] = drone;
                RenderMatrices[index] = float4x4.zero;
                return;
            }

            if (drone.BatteryPercent <= 0f)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
                drone.Velocity = float3.zero;
                Drones[index] = drone;
                RenderMatrices[index] = BuildRenderMatrix(drone.Position);
                return;
            }

            bool emergency = EmergencyOverclock != 0;
            if ((drone.State == (byte)HeadlessDroneRuntimeState.Idle ||
                 drone.TargetTaskIndex == EmptyTaskIndex) &&
                TrySelectTask(ref drone, emergency))
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Travel;
            }

            float drainScale = emergency ? EmergencyBatteryDrainMultiplier : 1f;
            drone.BatteryPercent = math.max(0f, drone.BatteryPercent - (drone.BatteryDrainPerSecond * drainScale * DeltaTime));

            if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                drone.State == (byte)HeadlessDroneRuntimeState.Attack ||
                drone.State == (byte)HeadlessDroneRuntimeState.Stasis ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked)
            {
                drone.Velocity = float3.zero;
                Drones[index] = drone;
                RenderMatrices[index] = BuildRenderMatrix(drone.Position);
                return;
            }

            float3 destination = ResolveDestination(in drone);
            float3 toDestination = destination - drone.Position;
            float distanceSq = math.lengthsq(toDestination);
            float serviceRadius = math.max(0.1f, drone.ServiceRadius);
            if (distanceSq <= serviceRadius * serviceRadius)
            {
                ResolveArrival(ref drone);
                Drones[index] = drone;
                RenderMatrices[index] = BuildRenderMatrix(drone.Position);
                return;
            }

            float3 routeDirection = math.normalizesafe(toDestination, float3.zero);
            float3 steering = ResolveBoidSteering(index, in drone, routeDirection);
            float3 direction = math.normalizesafe(steering, routeDirection);
            float maxSpeed = math.max(0.1f, drone.MaxSpeed * (emergency ? EmergencySpeedMultiplier : 1f));
            float3 desiredVelocity = direction * maxSpeed;
            drone.Velocity = math.lerp(drone.Velocity, desiredVelocity, math.saturate(DeltaTime * 8f));
            drone.Position += drone.Velocity * DeltaTime;

            Drones[index] = drone;
            RenderMatrices[index] = BuildRenderMatrix(drone.Position);
        }

        private bool TrySelectTask(ref HeadlessDroneState drone, bool emergency)
        {
            if (drone.HubGridId == 0)
                return false;

            float bestScore = 0f;
            HeadlessDroneTask bestTask = default;
            bool found = false;

            if (!TasksByGrid.TryGetFirstValue(drone.HubGridId, out HeadlessDroneTask task, out NativeParallelMultiHashMapIterator<int> iterator))
                return false;

            do
            {
                if (task.TaskIndex < 0 || task.TaskIndex >= TaskClaimOwners.Length)
                    continue;

                if (emergency && task.Kind == (byte)DroneFleetTaskKind.CutParasite)
                    continue;

                float distanceSq = math.max(MinimumScoreDistanceSq, math.lengthsq(task.Position - drone.Position));
                float score = (task.Criticality / distanceSq) * math.saturate(drone.BatteryPercent * 0.01f);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestTask = task;
                found = true;
            }
            while (TasksByGrid.TryGetNextValue(out task, ref iterator));

            if (!found || !TryClaimTask(bestTask.TaskIndex, drone.DroneId))
                return false;

            drone.TargetTaskIndex = bestTask.TaskIndex;
            drone.TargetModuleId = bestTask.ModuleId;
            drone.TargetPosition = bestTask.Position;
            return true;
        }

        private bool TryClaimTask(int taskIndex, int droneId)
        {
            int* claimPtr = (int*)TaskClaimOwners.GetUnsafePtr();
            int priorOwner = Interlocked.CompareExchange(ref claimPtr[taskIndex], droneId, UnclaimedTask);
            return priorOwner == UnclaimedTask || priorOwner == droneId;
        }

        private float3 ResolveBoidSteering(int selfIndex, in HeadlessDroneState drone, float3 routeDirection)
        {
            float3 separation = float3.zero;
            float3 alignment = float3.zero;
            float3 cohesion = float3.zero;
            int neighborCount = 0;

            int3 centerCell = ResolveSpatialCell(drone.Position);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        int key = PackSpatialKey(centerCell + new int3(x, y, z));
                        if (!DroneSpatialHash.TryGetFirstValue(key, out int otherIndex, out NativeParallelMultiHashMapIterator<int> iterator))
                            continue;

                        do
                        {
                            if (otherIndex == selfIndex || otherIndex < 0 || otherIndex >= ReadDrones.Length)
                                continue;

                            HeadlessDroneState other = ReadDrones[otherIndex];
                            if (other.State == (byte)HeadlessDroneRuntimeState.Empty ||
                                other.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                                other.State == (byte)HeadlessDroneRuntimeState.Completed)
                            {
                                continue;
                            }

                            float3 offset = drone.Position - other.Position;
                            float distanceSq = math.lengthsq(offset);
                            if (distanceSq <= MinimumVectorLengthSq || distanceSq > SeparationRadiusSq)
                                continue;

                            neighborCount++;
                            separation += math.normalizesafe(offset, float3.zero) / math.max(0.04f, distanceSq);
                            alignment += other.Velocity;
                            cohesion += other.Position;
                        }
                        while (DroneSpatialHash.TryGetNextValue(out otherIndex, ref iterator));
                    }
                }
            }

            float3 force = routeDirection + (separation * SeparationWeight);
            if (neighborCount > 0)
            {
                float invCount = 1f / neighborCount;
                force += (math.normalizesafe(alignment * invCount, routeDirection) - math.normalizesafe(drone.Velocity, routeDirection)) * AlignmentWeight;
                float cohesionWeight = drone.CorridorTight != 0 ? CorridorCohesionWeight : OpenCohesionWeight;
                force += math.normalizesafe((cohesion * invCount) - drone.Position, float3.zero) * cohesionWeight;
            }

            if (PlayerPositionValid != 0)
            {
                float3 playerOffset = drone.Position - PlayerPosition;
                float playerDistanceSq = math.lengthsq(playerOffset);
                if (playerDistanceSq > MinimumVectorLengthSq && playerDistanceSq <= PlayerSeparationRadiusSq)
                    force += math.normalizesafe(playerOffset, float3.zero) * (SeparationWeight * 3f / math.max(0.04f, playerDistanceSq));
            }

            float forceLengthSq = math.lengthsq(force);
            if (forceLengthSq > MaxSteering * MaxSteering)
                force *= math.rsqrt(forceLengthSq) * MaxSteering;

            return force;
        }

        private static void ResolveArrival(ref HeadlessDroneState drone)
        {
            drone.Velocity = float3.zero;
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Completed;
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.ResupplyDocked;
                return;
            }

            if (drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Attack;
                return;
            }

            drone.State = drone.TargetTaskIndex >= 0
                ? (byte)HeadlessDroneRuntimeState.Repair
                : (byte)HeadlessDroneRuntimeState.Idle;
        }

        private static float3 ResolveDestination(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return)
                return drone.HomePosition;

            if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel)
                return drone.SupplyPosition;

            return drone.TargetPosition;
        }

        private static float4x4 BuildRenderMatrix(float3 position)
        {
            return float4x4.TRS(position, quaternion.identity, new float3(1f, 1f, 1f));
        }

        internal static int PackSpatialKey(float3 position)
        {
            return PackSpatialKey(ResolveSpatialCell(position));
        }

        private static int3 ResolveSpatialCell(float3 position)
        {
            int x = math.clamp((int)math.floor((position.x - SpatialBoundsMin) / SpatialCellSize), 0, SpatialGridResolution - 1);
            int y = math.clamp((int)math.floor((position.y - SpatialBoundsMin) / SpatialCellSize), 0, SpatialGridResolution - 1);
            int z = math.clamp((int)math.floor((position.z - SpatialBoundsMin) / SpatialCellSize), 0, SpatialGridResolution - 1);
            return new int3(x, y, z);
        }

        private static int PackSpatialKey(int3 cell)
        {
            return cell.x + (cell.y * SpatialGridResolution) + (cell.z * SpatialGridResolution * SpatialGridResolution);
        }
    }
}
