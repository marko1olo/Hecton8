using Hecton8.Core;
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
        ResupplyDocked = 10,
        Docking = 11,
        ResupplyCommitPending = 12,
        Wander = 13,
        Reboot = 14,
        Escort = 15,
        SearchGrid = 16
    }

    public enum DroneFleetFormationMode : byte
    {
        Repair = 0,
        Escort = 1,
        SearchGrid = 2
    }

    internal enum DroneFleetTelemetryAccumulatorSlot : int
    {
        ActiveCount = 0,
        BatteryMilliPercent = 1,
        SolderReserve = 2,
        HostileCount = 3,
        LostToHijack = 4,
        Count = 5
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
        public float DockingElapsed;
        public float RebootElapsed;
        public float AvoidanceHysteresisSeconds;
        public float TransactionProgress;
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
        public float3 DockStartPosition;
        public quaternion Rotation;
        public quaternion HomeRotation;
        public quaternion DockStartRotation;
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
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal unsafe struct DroneCognitionJob : IJobParallelFor
    {
        private const int UnclaimedTask = 0;
        private const int MaxContentionClaimFailures = 3;
        private const float MinimumScoreDistanceSq = 0.5625f;
        private const float MinimumVectorLengthSq = 0.0001f;
        private static readonly float3 SafeNormalizeFallback = new float3(0f, 1f, 0f);
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
        private const float DockingStartDistanceMeters = 2f;
        private const float DockingStartDistanceSq = DockingStartDistanceMeters * DockingStartDistanceMeters;
        private const float DockingDurationSeconds = 1f;
        private const float RebootDurationSeconds = 2f;
        private const float SpatialCellSize = 2f;
        private const float SpatialBoundsMin = -512f;
        private const int SpatialGridResolution = 512;
        private const int EmptyTaskIndex = -1;
        private const float FlowLowBatteryThresholdPercent = 20f;
        private const float FlowCriticalBatteryThresholdPercent = 8f;
        private const float FlowDefaultDragCoefficient = 0.85f;
        private const float CollisionAvoidanceHoldSeconds = 0.35f;
        private const float EscortRadiusMeters = 10f;
        private const float SearchGridSpacingMeters = 8f;
        private const float FormationGoldenAngle = 2.3999631f;
        private const int SearchGridSide = 23;

        [ReadOnly] public NativeArray<HeadlessDroneState> ReadDrones;
        public NativeArray<HeadlessDroneState> Drones;
        public NativeArray<float4x4> RenderMatrices;

        [ReadOnly] public NativeParallelMultiHashMap<int, HeadlessDroneTask> TasksByGrid;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> DroneSpatialHash;
        [ReadOnly] public NativeArray<float3> AbyssalFlowVolume;

        [NativeDisableParallelForRestriction] public NativeArray<int> TaskClaimOwners;
        [NativeDisableParallelForRestriction] public NativeArray<int> TelemetryAccumulator;

        public float DeltaTime;
        public float3 PlayerPosition;
        public int PlayerPositionValid;
        public int EmergencyOverclock;
        public int FormationMode;
        public float3 FormationAnchorPosition;
        public int FormationAnchorValid;
        public int AbyssalFlowVolumeValid;
        public int AbyssalFlowResolutionXZ;
        public int AbyssalFlowResolutionY;
        public int AbyssalFlowRingOffsetX;
        public int AbyssalFlowRingOffsetY;
        public int AbyssalFlowRingOffsetZ;
        public float3 AbyssalFlowCenter;
        public float AbyssalFlowHorizontalCellSize;
        public float AbyssalFlowVerticalCellSize;
        public float AbyssalFlowWaterLevel;
        public float AbyssalFlowDepthMeters;
        public float3 BaseFlowVelocity;
        public float PhantomFlowTime;
        public float PhantomFlowNoiseScale;
        public float PhantomFlowTimeScale;
        public float PhantomFlowStrength;
        public float PhantomFlowVerticalFactor;
        public int PhantomFlowEnabled;
        public float FlowDragCoefficient;

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

            SanitizeKinematics(ref drone);
            AccumulateTelemetry(in drone);
            drone.AvoidanceHysteresisSeconds = math.max(0f, drone.AvoidanceHysteresisSeconds - math.max(0f, DeltaTime));

            if (drone.BatteryPercent <= 0f)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
                drone.Velocity = float3.zero;
                Drones[index] = drone;
                RenderMatrices[index] = BuildRenderMatrix(in drone);
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Reboot)
            {
                TickReboot(ref drone);
                Drones[index] = drone;
                RenderMatrices[index] = BuildRenderMatrix(in drone);
                return;
            }

            bool emergency = EmergencyOverclock != 0;
            bool formationOwnsDrone = TryApplyFormationMode(index, ref drone);
            if (!formationOwnsDrone &&
                (drone.State == (byte)HeadlessDroneRuntimeState.Idle ||
                 drone.TargetTaskIndex == EmptyTaskIndex) &&
                TrySelectTask(ref drone, emergency))
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Travel;
            }

            float drainScale = emergency ? EmergencyBatteryDrainMultiplier : 1f;
            drone.BatteryPercent = math.max(0f, drone.BatteryPercent - (drone.BatteryDrainPerSecond * drainScale * DeltaTime));

            if (drone.State == (byte)HeadlessDroneRuntimeState.Docking)
            {
                TickDocking(ref drone);
                Drones[index] = drone;
                RenderMatrices[index] = drone.State == (byte)HeadlessDroneRuntimeState.Completed
                    ? float4x4.zero
                    : BuildRenderMatrix(in drone);
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                drone.State == (byte)HeadlessDroneRuntimeState.Attack ||
                drone.State == (byte)HeadlessDroneRuntimeState.Stasis ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
            {
                drone.Velocity = float3.zero;
                Drones[index] = drone;
                RenderMatrices[index] = BuildRenderMatrix(in drone);
                return;
            }

            float3 destination = ResolveDestination(index, in drone);
            float3 toDestination = destination - drone.Position;
            float distanceSq = math.lengthsq(toDestination);
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return && distanceSq <= DockingStartDistanceSq)
            {
                BeginDocking(ref drone);
                TickDocking(ref drone);
                Drones[index] = drone;
                RenderMatrices[index] = drone.State == (byte)HeadlessDroneRuntimeState.Completed
                    ? float4x4.zero
                    : BuildRenderMatrix(in drone);
                return;
            }

            float serviceRadius = math.max(0.1f, drone.ServiceRadius);
            if (distanceSq <= serviceRadius * serviceRadius)
            {
                ResolveArrival(ref drone);
                Drones[index] = drone;
                RenderMatrices[index] = BuildRenderMatrix(in drone);
                return;
            }

            float3 routeDirection = SafeNormalize(toDestination);
            float3 steering = ResolveBoidSteering(index, ref drone, routeDirection);
            float3 direction = SafeNormalize(steering, routeDirection);
            float maxSpeed = math.max(0.1f, drone.MaxSpeed * (emergency ? EmergencySpeedMultiplier : 1f));
            float3 desiredVelocity = direction * maxSpeed;
            float3 flowVelocity = ResolveFlowVelocity(drone.Position);
            float dragCoefficient = math.max(0f, FlowDragCoefficient > 0f ? FlowDragCoefficient : FlowDefaultDragCoefficient);
            float3 flowAcceleration = (flowVelocity - drone.Velocity) * dragCoefficient;
            float3 flowAdjustedVelocity = drone.Velocity + (flowAcceleration * DeltaTime);
            float counterFlow01 = ResolveFlowCounteract(in drone, emergency);
            float3 targetVelocity = math.lerp(flowVelocity, desiredVelocity, counterFlow01);
            float velocityBlend = drone.AvoidanceHysteresisSeconds > 0f ? 4f : 8f;
            drone.Velocity = math.lerp(flowAdjustedVelocity, targetVelocity, math.saturate(DeltaTime * velocityBlend));
            drone.Position += drone.Velocity * DeltaTime;
            if (math.lengthsq(drone.Velocity) > MinimumVectorLengthSq)
                drone.Rotation = quaternion.LookRotationSafe(SafeNormalize(drone.Velocity, routeDirection), math.up());

            Drones[index] = drone;
            RenderMatrices[index] = BuildRenderMatrix(in drone);
        }

        private bool TryApplyFormationMode(int index, ref HeadlessDroneState drone)
        {
            if (FormationAnchorValid == 0 || FormationMode == (int)DroneFleetFormationMode.Repair)
                return false;

            if (drone.State != (byte)HeadlessDroneRuntimeState.Idle &&
                drone.State != (byte)HeadlessDroneRuntimeState.Wander &&
                drone.State != (byte)HeadlessDroneRuntimeState.Escort &&
                drone.State != (byte)HeadlessDroneRuntimeState.SearchGrid)
            {
                return false;
            }

            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetModuleId = 0;
            drone.TargetPosition = ResolveFormationDestination(index, FormationMode);
            drone.State = FormationMode == (int)DroneFleetFormationMode.Escort
                ? (byte)HeadlessDroneRuntimeState.Escort
                : (byte)HeadlessDroneRuntimeState.SearchGrid;
            return true;
        }

        private bool TrySelectTask(ref HeadlessDroneState drone, bool emergency)
        {
            if (drone.HubGridId == 0)
                return false;

            float firstScore = 0f;
            float secondScore = 0f;
            float thirdScore = 0f;
            float fallbackScore = 0f;
            HeadlessDroneTask firstTask = default;
            HeadlessDroneTask secondTask = default;
            HeadlessDroneTask thirdTask = default;
            HeadlessDroneTask fallbackTask = default;
            bool hasFirst = false;
            bool hasSecond = false;
            bool hasThird = false;
            bool hasFallback = false;

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
                InsertTaskCandidate(
                    in task,
                    score,
                    ref firstTask,
                    ref firstScore,
                    ref hasFirst,
                    ref secondTask,
                    ref secondScore,
                    ref hasSecond,
                    ref thirdTask,
                    ref thirdScore,
                    ref hasThird,
                    ref fallbackTask,
                    ref fallbackScore,
                    ref hasFallback);
            }
            while (TasksByGrid.TryGetNextValue(out task, ref iterator));

            if (!hasFirst)
                return false;

            int failedClaims = 0;
            if (TryClaimRankedTask(ref drone, in firstTask, hasFirst, ref failedClaims))
                return true;

            if (TryClaimRankedTask(ref drone, in secondTask, hasSecond, ref failedClaims))
                return true;

            if (TryClaimRankedTask(ref drone, in thirdTask, hasThird, ref failedClaims))
                return true;

            return failedClaims >= MaxContentionClaimFailures &&
                   TryClaimRankedTask(ref drone, in fallbackTask, hasFallback, ref failedClaims);
        }

        private bool TryClaimTask(int taskIndex, int droneId)
        {
            int* claimPtr = (int*)TaskClaimOwners.GetUnsafePtr();
            int priorOwner = Interlocked.CompareExchange(ref claimPtr[taskIndex], droneId, UnclaimedTask);
            return priorOwner == UnclaimedTask || priorOwner == droneId;
        }

        private float3 ResolveBoidSteering(int selfIndex, ref HeadlessDroneState drone, float3 routeDirection)
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
                                other.State == (byte)HeadlessDroneRuntimeState.Completed ||
                                other.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending ||
                                other.State == (byte)HeadlessDroneRuntimeState.Reboot)
                            {
                                continue;
                            }

                            float3 offset = drone.Position - other.Position;
                            float distanceSq = math.lengthsq(offset);
                            if (distanceSq > SeparationRadiusSq)
                                continue;

                            neighborCount++;
                            separation += SafeNormalize(offset) / math.max(0.04f, distanceSq);
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
                force += (SafeNormalize(alignment * invCount, routeDirection) - SafeNormalize(drone.Velocity, routeDirection)) * AlignmentWeight;
                float cohesionWeight = ResolveCohesionWeight(in drone);
                force += SafeNormalize((cohesion * invCount) - drone.Position) * cohesionWeight;
            }

            if (PlayerPositionValid != 0)
            {
                float3 playerOffset = drone.Position - PlayerPosition;
                float playerDistanceSq = math.lengthsq(playerOffset);
                if (playerDistanceSq <= PlayerSeparationRadiusSq)
                    force += SafeNormalize(playerOffset) * (SeparationWeight * 3f / math.max(0.04f, playerDistanceSq));
            }

            float forceLengthSq = math.lengthsq(force);
            if (forceLengthSq > MaxSteering * MaxSteering)
                force *= math.rsqrt(forceLengthSq) * MaxSteering;

            if (drone.CorridorTight != 0 && forceLengthSq > SeparationWeight * SeparationWeight)
                drone.AvoidanceHysteresisSeconds = math.max(drone.AvoidanceHysteresisSeconds, CollisionAvoidanceHoldSeconds);

            return force;
        }

        private static void ResolveArrival(ref HeadlessDroneState drone)
        {
            drone.Velocity = float3.zero;
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return)
            {
                BeginDocking(ref drone);
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.ResupplyDocked;
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Wander)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Idle;
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Escort ||
                drone.State == (byte)HeadlessDroneRuntimeState.SearchGrid)
            {
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

        private float3 ResolveDestination(int index, in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking)
            {
                return drone.HomePosition;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel)
                return drone.SupplyPosition;

            if (drone.State == (byte)HeadlessDroneRuntimeState.Wander)
                return drone.TargetPosition;

            if (drone.State == (byte)HeadlessDroneRuntimeState.Escort ||
                drone.State == (byte)HeadlessDroneRuntimeState.SearchGrid)
            {
                return ResolveFormationDestination(index, drone.State == (byte)HeadlessDroneRuntimeState.Escort
                    ? (int)DroneFleetFormationMode.Escort
                    : (int)DroneFleetFormationMode.SearchGrid);
            }

            return drone.TargetPosition;
        }

        private float3 ResolveFormationDestination(int index, int formationMode)
        {
            float3 anchor = FormationAnchorValid != 0 ? FormationAnchorPosition : float3.zero;
            if (formationMode == (int)DroneFleetFormationMode.Escort)
            {
                float angle = index * FormationGoldenAngle;
                float ringJitter = ((index % 7) - 3) * 0.35f;
                return anchor + new float3(
                    CinematicMath.FastCos(angle) * EscortRadiusMeters,
                    ringJitter,
                    CinematicMath.FastSin(angle) * EscortRadiusMeters);
            }

            int x = (index % SearchGridSide) - (SearchGridSide >> 1);
            int z = (index / SearchGridSide) - (SearchGridSide >> 1);
            return anchor + new float3(x * SearchGridSpacingMeters, 0f, z * SearchGridSpacingMeters);
        }

        private void TickReboot(ref HeadlessDroneState drone)
        {
            drone.FactionBit = (byte)HeadlessDroneFactionBit.Friendly;
            drone.Velocity = float3.zero;
            drone.RebootElapsed = math.min(RebootDurationSeconds, drone.RebootElapsed + math.max(0f, DeltaTime));
            if (drone.RebootElapsed < RebootDurationSeconds)
                return;

            drone.RebootElapsed = 0f;
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = drone.HubGridId != 0 ? drone.HomePosition : drone.Position + SafeNormalizeFallback;
            drone.State = drone.HubGridId != 0
                ? (byte)HeadlessDroneRuntimeState.Return
                : (byte)HeadlessDroneRuntimeState.Wander;
        }

        private void TickDocking(ref HeadlessDroneState drone)
        {
            float elapsed = math.min(DockingDurationSeconds, drone.DockingElapsed + math.max(0f, DeltaTime));
            float t = math.saturate(elapsed / DockingDurationSeconds);
            quaternion targetRotation = ResolveSafeRotation(drone.HomeRotation);
            drone.DockingElapsed = elapsed;
            drone.Position = math.lerp(drone.DockStartPosition, drone.HomePosition, t);
            drone.Rotation = CinematicMath.FastNlerp(ResolveSafeRotation(drone.DockStartRotation), targetRotation, t);
            drone.Velocity = float3.zero;

            if (t < 1f)
                return;

            drone.Position = drone.HomePosition;
            drone.Rotation = targetRotation;
            drone.State = (byte)HeadlessDroneRuntimeState.Completed;
        }

        private static void BeginDocking(ref HeadlessDroneState drone)
        {
            drone.DockingElapsed = 0f;
            drone.DockStartPosition = drone.Position;
            drone.DockStartRotation = ResolveSafeRotation(drone.Rotation);
            drone.Velocity = float3.zero;
            drone.State = (byte)HeadlessDroneRuntimeState.Docking;
        }

        private static float4x4 BuildRenderMatrix(in HeadlessDroneState drone)
        {
            return float4x4.TRS(drone.Position, ResolveSafeRotation(drone.Rotation), new float3(1f, 1f, 1f));
        }

        private void AccumulateTelemetry(in HeadlessDroneState drone)
        {
            if (!TelemetryAccumulator.IsCreated || TelemetryAccumulator.Length < (int)DroneFleetTelemetryAccumulatorSlot.Count)
                return;

            int* accumulator = (int*)TelemetryAccumulator.GetUnsafePtr();
            Interlocked.Increment(ref accumulator[(int)DroneFleetTelemetryAccumulatorSlot.ActiveCount]);
            Interlocked.Add(
                ref accumulator[(int)DroneFleetTelemetryAccumulatorSlot.BatteryMilliPercent],
                (int)math.round(math.clamp(drone.BatteryPercent, 0f, 100f) * 1000f));
            Interlocked.Add(
                ref accumulator[(int)DroneFleetTelemetryAccumulatorSlot.SolderReserve],
                math.max(0, drone.SolderUnits));

            if (drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
                Interlocked.Increment(ref accumulator[(int)DroneFleetTelemetryAccumulatorSlot.HostileCount]);
        }

        private float3 ResolveFlowVelocity(float3 position)
        {
            float3 flowVelocity = BaseFlowVelocity;
            if (AbyssalFlowVolumeValid != 0 &&
                AbyssalFlowVolume.IsCreated &&
                AbyssalFlowVolume.Length > 0 &&
                AbyssalFlowResolutionXZ > 1 &&
                AbyssalFlowResolutionY > 1 &&
                AbyssalFlowHorizontalCellSize > 0f &&
                AbyssalFlowVerticalCellSize > 0f)
            {
                flowVelocity += SampleAbyssalFlowVolume(position);
            }

            if (PhantomFlowEnabled != 0 && PhantomFlowStrength > 0f && PhantomFlowNoiseScale > 0f)
            {
                flowVelocity += CurrentManager.SampleCurrent(
                    position,
                    PhantomFlowTime,
                    PhantomFlowNoiseScale,
                    math.max(0f, PhantomFlowTimeScale),
                    PhantomFlowStrength,
                    math.max(0f, PhantomFlowVerticalFactor));
            }

            return IsFinite(flowVelocity) ? flowVelocity : float3.zero;
        }

        private float3 SampleAbyssalFlowVolume(float3 position)
        {
            float halfExtent = (AbyssalFlowResolutionXZ - 1) * 0.5f * AbyssalFlowHorizontalCellSize;
            float minX = AbyssalFlowCenter.x - halfExtent;
            float minZ = AbyssalFlowCenter.z - halfExtent;
            float maxY = AbyssalFlowWaterLevel;
            float minY = AbyssalFlowWaterLevel - math.max(0f, AbyssalFlowDepthMeters);
            if (position.x < minX ||
                position.z < minZ ||
                position.x > minX + (halfExtent * 2f) ||
                position.z > minZ + (halfExtent * 2f))
            {
                return float3.zero;
            }

            float clampedY = math.clamp(position.y, minY, maxY);
            float normalizedX = math.clamp((position.x - minX) / AbyssalFlowHorizontalCellSize, 0f, AbyssalFlowResolutionXZ - 1);
            float normalizedZ = math.clamp((position.z - minZ) / AbyssalFlowHorizontalCellSize, 0f, AbyssalFlowResolutionXZ - 1);
            float normalizedY = math.clamp((maxY - clampedY) / AbyssalFlowVerticalCellSize, 0f, AbyssalFlowResolutionY - 1);
            int x0 = math.clamp((int)math.floor(normalizedX), 0, AbyssalFlowResolutionXZ - 1);
            int z0 = math.clamp((int)math.floor(normalizedZ), 0, AbyssalFlowResolutionXZ - 1);
            int y0 = math.clamp((int)math.floor(normalizedY), 0, AbyssalFlowResolutionY - 1);
            int x1 = math.min(x0 + 1, AbyssalFlowResolutionXZ - 1);
            int z1 = math.min(z0 + 1, AbyssalFlowResolutionXZ - 1);
            int y1 = math.min(y0 + 1, AbyssalFlowResolutionY - 1);
            float fracX = normalizedX - x0;
            float fracZ = normalizedZ - z0;
            float fracY = normalizedY - y0;

            float3 sample000 = ReadAbyssalFlowCell(x0, y0, z0);
            float3 sample100 = ReadAbyssalFlowCell(x1, y0, z0);
            float3 sample010 = ReadAbyssalFlowCell(x0, y0, z1);
            float3 sample110 = ReadAbyssalFlowCell(x1, y0, z1);
            float3 sample001 = ReadAbyssalFlowCell(x0, y1, z0);
            float3 sample101 = ReadAbyssalFlowCell(x1, y1, z0);
            float3 sample011 = ReadAbyssalFlowCell(x0, y1, z1);
            float3 sample111 = ReadAbyssalFlowCell(x1, y1, z1);
            float3 sampleX00 = math.lerp(sample000, sample100, fracX);
            float3 sampleX10 = math.lerp(sample010, sample110, fracX);
            float3 sampleX01 = math.lerp(sample001, sample101, fracX);
            float3 sampleX11 = math.lerp(sample011, sample111, fracX);
            float3 sampleZ0 = math.lerp(sampleX00, sampleX10, fracZ);
            float3 sampleZ1 = math.lerp(sampleX01, sampleX11, fracZ);
            return math.lerp(sampleZ0, sampleZ1, fracY);
        }

        private float3 ReadAbyssalFlowCell(int x, int y, int z)
        {
            int physicalIndex = GetAbyssalFlowPhysicalIndex(x, y, z);
            return physicalIndex >= 0 && physicalIndex < AbyssalFlowVolume.Length
                ? AbyssalFlowVolume[physicalIndex]
                : float3.zero;
        }

        private int GetAbyssalFlowPhysicalIndex(int x, int y, int z)
        {
            int wrappedX = PositiveModulo(x + AbyssalFlowRingOffsetX, AbyssalFlowResolutionXZ);
            int wrappedY = PositiveModulo(y + AbyssalFlowRingOffsetY, AbyssalFlowResolutionY);
            int wrappedZ = PositiveModulo(z + AbyssalFlowRingOffsetZ, AbyssalFlowResolutionXZ);
            return (wrappedY * AbyssalFlowResolutionXZ * AbyssalFlowResolutionXZ) +
                   (wrappedZ * AbyssalFlowResolutionXZ) +
                   wrappedX;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
                return 0;

            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static float ResolveFlowCounteract(in HeadlessDroneState drone, bool emergency)
        {
            if (emergency)
                return 1f;

            if (drone.BatteryPercent <= FlowCriticalBatteryThresholdPercent)
                return 0.1f;

            if (drone.BatteryPercent <= FlowLowBatteryThresholdPercent)
                return 0.25f;

            if (drone.State == (byte)HeadlessDroneRuntimeState.Escort)
                return 0.65f;

            if (drone.State == (byte)HeadlessDroneRuntimeState.SearchGrid)
                return 0.75f;

            return drone.TargetTaskIndex >= 0 ||
                   drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                   drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel
                ? 1f
                : 0.55f;
        }

        private static float ResolveCohesionWeight(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.SearchGrid)
                return 0f;

            if (drone.CorridorTight != 0 || drone.AvoidanceHysteresisSeconds > 0f)
                return CorridorCohesionWeight;

            return OpenCohesionWeight;
        }

        private static quaternion ResolveSafeRotation(quaternion rotation)
        {
            return math.lengthsq(rotation.value) > MinimumVectorLengthSq
                ? CinematicMath.NormalizeQuaternionOrIdentity(rotation)
                : quaternion.identity;
        }

        private static void InsertTaskCandidate(
            in HeadlessDroneTask task,
            float score,
            ref HeadlessDroneTask firstTask,
            ref float firstScore,
            ref bool hasFirst,
            ref HeadlessDroneTask secondTask,
            ref float secondScore,
            ref bool hasSecond,
            ref HeadlessDroneTask thirdTask,
            ref float thirdScore,
            ref bool hasThird,
            ref HeadlessDroneTask fallbackTask,
            ref float fallbackScore,
            ref bool hasFallback)
        {
            if (!hasFirst || score > firstScore)
            {
                fallbackTask = thirdTask;
                fallbackScore = thirdScore;
                hasFallback = hasThird;
                thirdTask = secondTask;
                thirdScore = secondScore;
                hasThird = hasSecond;
                secondTask = firstTask;
                secondScore = firstScore;
                hasSecond = hasFirst;
                firstTask = task;
                firstScore = score;
                hasFirst = true;
                return;
            }

            if (!hasSecond || score > secondScore)
            {
                fallbackTask = thirdTask;
                fallbackScore = thirdScore;
                hasFallback = hasThird;
                thirdTask = secondTask;
                thirdScore = secondScore;
                hasThird = hasSecond;
                secondTask = task;
                secondScore = score;
                hasSecond = true;
                return;
            }

            if (!hasThird || score > thirdScore)
            {
                fallbackTask = thirdTask;
                fallbackScore = thirdScore;
                hasFallback = hasThird;
                thirdTask = task;
                thirdScore = score;
                hasThird = true;
                return;
            }

            if (!hasFallback || score > fallbackScore)
            {
                fallbackTask = task;
                fallbackScore = score;
                hasFallback = true;
            }
        }

        private bool TryClaimRankedTask(ref HeadlessDroneState drone, in HeadlessDroneTask task, bool hasTask, ref int failedClaims)
        {
            if (!hasTask)
                return false;

            if (TryClaimTask(task.TaskIndex, drone.DroneId))
            {
                drone.TargetTaskIndex = task.TaskIndex;
                drone.TargetModuleId = task.ModuleId;
                drone.TargetPosition = task.Position;
                return true;
            }

            failedClaims++;
            return false;
        }

        private static void SanitizeKinematics(ref HeadlessDroneState drone)
        {
            if (!IsFinite(drone.Position))
                drone.Position = IsFinite(drone.HomePosition) ? drone.HomePosition : float3.zero;

            if (!IsFinite(drone.Velocity))
                drone.Velocity = float3.zero;
        }

        private static float3 SafeNormalize(float3 value)
        {
            return SafeNormalize(value, SafeNormalizeFallback);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq < MinimumVectorLengthSq)
            {
                float fallbackLengthSq = math.lengthsq(fallback);
                return IsFinite(fallback) && math.isfinite(fallbackLengthSq) && fallbackLengthSq >= MinimumVectorLengthSq
                    ? fallback * math.rsqrt(fallbackLengthSq)
                    : SafeNormalizeFallback;
            }

            return value * math.rsqrt(lengthSq);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
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
