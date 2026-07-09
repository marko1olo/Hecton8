using Hecton8.Core;
using static Hecton8.Core.UnityMathematicsExtensions;
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
        DockingAborts = 5,
        Count = 6
    }

    internal enum HeadlessDroneFactionBit : byte
    {
        Friendly = 1,
        Hostile = 2
    }

    [StructLayout(LayoutKind.Explicit, Size = 448)]
    internal struct HeadlessDroneState
    {
        [FieldOffset(0)]
        public int DroneId;
        [FieldOffset(4)]
        public int HubGridId;
        [FieldOffset(8)]
        public int HubSlot;
        [FieldOffset(12)]
        public int TargetTaskIndex;
        [FieldOffset(16)]
        public int TargetModuleId;
        [FieldOffset(20)]
        public int SolderUnits;
        [FieldOffset(24)]
        public int LoadedSolderCapacity;
        [FieldOffset(28)]
        public byte State;
        [FieldOffset(29)]
        public byte FactionBit;
        [FieldOffset(30)]
        public byte CorridorTight;
        [FieldOffset(31)]
        public byte Reserved0;
        [FieldOffset(32)]
        public float BatteryPercent;
        [FieldOffset(36)]
        public float RepairAccumulator;
        [FieldOffset(40)]
        public float DockingElapsed;
        [FieldOffset(44)]
        public float RebootElapsed;
        [FieldOffset(48)]
        public float AvoidanceHysteresisSeconds;
        [FieldOffset(52)]
        public float TransactionProgress;
        [FieldOffset(56)]
        public float ServiceRadius;
        [FieldOffset(60)]
        public float MaxSpeed;
        [FieldOffset(64)]
        public float BatteryDrainPerSecond;
        [FieldOffset(68)]
        public float RepairRatePerSecond;
        [FieldOffset(72)]
        public float WeldPowerNormalized;
        [FieldOffset(76)]
        public float WeldRangeMeters;
        [FieldOffset(80)]
        public float3 Position;
        [FieldOffset(92)]
        public float3 Velocity;
        [FieldOffset(104)]
        public float3 HomePosition;
        [FieldOffset(116)]
        public float3 TargetPosition;
        [FieldOffset(128)]
        public float3 SupplyPosition;
        [FieldOffset(140)]
        public float3 DockStartPosition;
        [FieldOffset(152)]
        public quaternion Rotation;
        [FieldOffset(168)]
        public quaternion HomeRotation;
        [FieldOffset(184)]
        public quaternion DockStartRotation;
        [FieldOffset(200)]
        public float DockingPathLengthMeters;
        [FieldOffset(204)]
        public uint DockingRequestId;
        [FieldOffset(208)]
        public byte DockingFlags;
        [FieldOffset(209)]
        public byte DockingReserved0;
        [FieldOffset(210)]
        public byte DockingReserved1;
        [FieldOffset(211)]
        public byte DockingReserved2;
        [FieldOffset(212)]
        private uint _dockControlPad0;
        [FieldOffset(216)]
        public double3 DockControlP0;
        [FieldOffset(240)]
        public double3 DockControlP1;
        [FieldOffset(264)]
        public double3 DockControlP2;
        [FieldOffset(288)]
        public double3 DockControlP3;
        [FieldOffset(312)]
        public double3 PositionAup;
        [FieldOffset(336)]
        public double3 HomeAup;
        [FieldOffset(360)]
        public double3 TargetAup;
        [FieldOffset(384)]
        public double3 SupplyAup;
        [FieldOffset(408)]
        public uint ReservedTail0;
        [FieldOffset(412)]
        public uint ReservedTail1;
        [FieldOffset(416)]
        public uint ReservedTail2;
        [FieldOffset(420)]
        public uint ReservedTail3;
        [FieldOffset(424)]
        private uint _tailPad0;
        [FieldOffset(428)]
        private uint _tailPad1;
        [FieldOffset(432)]
        private ulong _tailPad2;
        [FieldOffset(440)]
        private ulong _tailPad3;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct DroneFleetOriginShiftJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<HeadlessDroneState> DroneStates;
        [NoAlias] public NativeArray<HeadlessDroneState> DroneStateBackBuffer;
        [NoAlias] public NativeArray<float4x4> RenderMatrices;
        [NoAlias] public NativeArray<float4x4> RenderMatrixBackBuffer;
        [NoAlias] public NativeArray<float3> DronePositions;
        public float3 RuntimeOffset;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)DroneStates.Length)
                return;

            HeadlessDroneState drone = DroneStates[index];
            if (drone.State == (byte)HeadlessDroneRuntimeState.Empty)
                return;

            double3 doubleOffset = new double3(RuntimeOffset.x, RuntimeOffset.y, RuntimeOffset.z);
            drone.Position += RuntimeOffset;
            drone.HomePosition += RuntimeOffset;
            drone.TargetPosition += RuntimeOffset;
            drone.SupplyPosition += RuntimeOffset;
            drone.DockStartPosition += RuntimeOffset;
            drone.DockControlP0 += doubleOffset;
            drone.DockControlP1 += doubleOffset;
            drone.DockControlP2 += doubleOffset;
            drone.DockControlP3 += doubleOffset;
            DroneStates[index] = drone;

            if (DroneStateBackBuffer.IsCreated && index < DroneStateBackBuffer.Length)
                DroneStateBackBuffer[index] = drone;

            if (DronePositions.IsCreated && index < DronePositions.Length)
                DronePositions[index] = drone.Position;

            if (RenderMatrices.IsCreated && index < RenderMatrices.Length)
            {
                float4x4 matrix = RenderMatrices[index];
                matrix.c3.xyz += RuntimeOffset;
                RenderMatrices[index] = matrix;
            }

            if (RenderMatrixBackBuffer.IsCreated && index < RenderMatrixBackBuffer.Length)
            {
                float4x4 matrix = RenderMatrixBackBuffer[index];
                matrix.c3.xyz += RuntimeOffset;
                RenderMatrixBackBuffer[index] = matrix;
            }
        }
    }

    internal enum DroneFleetSoaState : byte
    {
        Idle = 0,
        Mining = 1,
        Repairing = 2,
        Returning = 3
    }

    internal enum DroneServiceCommandKind : byte
    {
        None = 0,
        Repair = 1,
        Attack = 2,
        DockingHatchOpen = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneServiceCommand
    {
        [FieldOffset(0)] public int Slot;
        [FieldOffset(4)] public int DroneId;
        [FieldOffset(8)] public byte Kind;
        [FieldOffset(9)] public byte State;
        [FieldOffset(10)] public ushort Reserved;
        [FieldOffset(12)] public float DeltaTime;
        [FieldOffset(16)] public float3 Position;
        [FieldOffset(28)] public float3 TargetPosition;
        [FieldOffset(40)] public ulong Pad0;
        [FieldOffset(48)] public ulong Pad1;
        [FieldOffset(56)] public ulong Pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneServiceCommandCursor
    {
        [FieldOffset(0)] public int Count;
        [FieldOffset(8)] public ulong Pad0;
        [FieldOffset(16)] public ulong Pad1;
        [FieldOffset(24)] public ulong Pad2;
        [FieldOffset(32)] public ulong Pad3;
        [FieldOffset(40)] public ulong Pad4;
        [FieldOffset(48)] public ulong Pad5;
        [FieldOffset(56)] public ulong Pad6;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DroneCognitionJob : IJobParallelFor
    {
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
        private const float DockingStartDistanceMeters = 2f;
        private const float DockingStartDistanceSq = DockingStartDistanceMeters * DockingStartDistanceMeters;
        private const float LengthEpsilonSq = 0.00000001f;
        private const float DockingStartForwardMeters = 10f;
        private const float DockingAirlockForwardMeters = 20f;
        private const float DockingArrivalSpeedMetersPerSecond = 0.5f;
        private const float DockingMinimumPathLengthMeters = 1f;
        private const float DockingHatchOpenT = 0.8f;
        private const float DockingVisualSlipWeight = 0.25f;
        internal const byte DockingFlagHatchOpenQueued = 1 << 0;
        internal const byte DockingFlagCompleted = 1 << 1;
        internal const byte DockingFlagHatchOpenPublished = 1 << 2;
        private const float RebootDurationSeconds = 2f;
        private const float SpatialCellSize = 2f;
        private const float SpatialCellSizeInv = 0.5f;
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

        [ReadOnly, NoAlias] public NativeArray<HeadlessDroneState> ReadDrones;
        [NoAlias] public NativeArray<HeadlessDroneState> Drones;
        // SAFETY JUSTIFICATION 1/3: this cognition kernel owns one drone slot per Execute index; DTO,
        // target, position, and state lanes are written only at the current index.
        // SAFETY JUSTIFICATION 2/3: command cursor, service command, claim-owner, and telemetry lanes
        // use Interlocked/CAS helpers for every cross-index write, so disabled range checks do not hide races.
        // SAFETY JUSTIFICATION 3/3: all suppressed arrays are distinct Vault buffers and are annotated
        // `[NoAlias]`; no aliasing is expected between state, target, command, telemetry, or spatial rows.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneStateDTO> DroneStatesDto;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTargetDTO> DroneTargets;
        [NoAlias] public NativeArray<float4x4> RenderMatrices;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float3> DronePositions;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> DroneStates;

        [ReadOnly, NoAlias] public NativeArray<int> DroneSpatialBucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> DroneSpatialNextIndices;
        [ReadOnly, NoAlias] public NativeArray<int> DroneSpatialKeys;
        [ReadOnly, NoAlias] public NativeArray<float3>.ReadOnly AbyssalFlowVolume;
        [ReadOnly, NoAlias] public NativeArray<PathWaypointDTO> MacroWaypoints;
        [ReadOnly, NoAlias] public NativeArray<byte> MacroWaypointStates;

        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TaskClaimOwners;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TelemetryAccumulator;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneServiceCommand> ServiceCommands;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneServiceCommandCursor> ServiceCommandCursor;
        public int ServiceCommandCapacity;

        public float DeltaTime;
        public int ServiceQueueEnabled;
        public float3 PlayerPosition;
        public int PlayerPositionValid;
        public int EmergencyOverclock;
        public int FormationMode;
        public int DroneSpatialBucketMask;
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
        public float CrossCurrentVisualSlipWeight;
        public DroneSdfGrid SdfGrid;
        public int FrameIndex;
        public int SteeringTickModulo;
        public float SdfRepulsionStrength;

        public void Execute(int index)
        {
            HeadlessDroneState drone = ReadDrones[index];
            if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                drone.State == (byte)HeadlessDroneRuntimeState.Completed)
            {
                WriteOutputs(index, in drone, float4x4.zero);
                return;
            }

            SanitizeKinematics(ref drone);
            SanitizeAupFields(ref drone);
            AccumulateTelemetry(in drone);
            drone.AvoidanceHysteresisSeconds = math.max(0f, drone.AvoidanceHysteresisSeconds - math.max(0f, DeltaTime));

            if (drone.BatteryPercent <= 0f)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
                drone.Velocity = float3.zero;
                WriteOutputs(index, in drone, BuildRenderMatrix(in drone));
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Reboot)
            {
                TickReboot(ref drone);
                WriteOutputs(index, in drone, BuildRenderMatrix(in drone));
                return;
            }

            bool emergency = EmergencyOverclock != 0;
            bool formationOwnsDrone = TryApplyFormationMode(index, ref drone);
            if (!formationOwnsDrone &&
                drone.State == (byte)HeadlessDroneRuntimeState.Idle &&
                drone.TargetTaskIndex != EmptyTaskIndex)
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Travel;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Docking)
            {
                TickDocking(index, ref drone);
                WriteOutputs(index, in drone, drone.State == (byte)HeadlessDroneRuntimeState.Completed
                    ? float4x4.zero
                    : BuildRenderMatrix(in drone));
                return;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                drone.State == (byte)HeadlessDroneRuntimeState.Attack ||
                drone.State == (byte)HeadlessDroneRuntimeState.Stasis ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
            {
                drone.Velocity = float3.zero;
                if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Attack)
                {
                    EnqueueServiceCommand(index, in drone);
                }

                WriteOutputs(index, in drone, BuildRenderMatrix(in drone));
                return;
            }

            float3 destination = ResolveDestination(index, in drone);
            float3 toDestination = destination - drone.Position;
            float distanceSq = math.lengthsq(toDestination);
            if (drone.State == (byte)HeadlessDroneRuntimeState.Return && distanceSq <= DockingStartDistanceSq)
            {
                BeginDocking(ref drone);
                TickDocking(index, ref drone);
                WriteOutputs(index, in drone, drone.State == (byte)HeadlessDroneRuntimeState.Completed
                    ? float4x4.zero
                    : BuildRenderMatrix(in drone));
                return;
            }

            float serviceRadius = math.max(0.1f, drone.ServiceRadius);
            if (distanceSq <= serviceRadius * serviceRadius)
            {
                ResolveArrival(ref drone);
                WriteOutputs(index, in drone, BuildRenderMatrix(in drone));
                return;
            }

            float3 routeDirection = SafeNormalize(toDestination);
            float3 steering = ShouldRunSteeringTick(index)
                ? ResolveBoidSteering(index, ref drone, routeDirection)
                : SafeNormalize(drone.Velocity, routeDirection);
            float3 direction = SafeNormalize(steering, routeDirection);
            float maxSpeed = math.max(0.1f, drone.MaxSpeed * (emergency ? EmergencySpeedMultiplier : 1f));
            float3 desiredVelocity = direction * maxSpeed;
            float3 flowVelocity = ResolveFlowVelocity(drone.Position);
            float flowSpeedSq = math.select(0f, math.lengthsq(flowVelocity), math.all(math.isfinite(flowVelocity)));
            float flowStress01 = math.saturate(FastLengthFromSq(flowSpeedSq) * math.rcp(math.max(0.1f, maxSpeed)));
            if (flowStress01 > 0.0001f)
                drone.BatteryPercent = math.max(0f, drone.BatteryPercent - (drone.BatteryDrainPerSecond * flowStress01 * 0.15f * DeltaTime));

            float dragCoefficient = math.max(0f, FlowDragCoefficient > 0f ? FlowDragCoefficient : FlowDefaultDragCoefficient);
            float3 flowAcceleration = (flowVelocity - drone.Velocity) * dragCoefficient;
            float3 flowAdjustedVelocity = drone.Velocity + (flowAcceleration * DeltaTime);
            float counterFlow01 = ResolveFlowCounteract(in drone, emergency);
            float3 targetVelocity = BlendLinear(flowVelocity, desiredVelocity, counterFlow01);
            float velocityBlend = drone.AvoidanceHysteresisSeconds > 0f ? 4f : 8f;
            drone.Velocity = BlendLinear(flowAdjustedVelocity, targetVelocity, math.saturate(DeltaTime * velocityBlend));
            float3 movementDelta = drone.Velocity * DeltaTime;
            drone.Position += movementDelta;
            drone.PositionAup = OffsetAupMeters(drone.PositionAup, movementDelta, drone.Position);
            if (math.lengthsq(drone.Velocity) > MinimumVectorLengthSq)
                drone.Rotation = quaternion.LookRotationSafe(SafeNormalize(drone.Velocity, routeDirection), math.up());

            WriteOutputs(index, in drone, BuildRenderMatrix(in drone));
        }

        private void WriteOutputs(int index, in HeadlessDroneState drone, float4x4 renderMatrix)
        {
            Drones[index] = drone;
            RenderMatrices[index] = renderMatrix;

            if (DroneStatesDto.IsCreated && index < DroneStatesDto.Length)
            {
                DroneStateDTO* statePtr = (DroneStateDTO*)DroneStatesDto.GetUnsafePtr();
                ref DroneStateDTO dto = ref UnsafeUtility.AsRef<DroneStateDTO>(statePtr + index);
                dto.CurrentAUP = IsFinite(drone.PositionAup) ? drone.PositionAup : global::Hecton8.World.AUPMath.ToDouble3(drone.Position);
                dto.Velocity = drone.Velocity;
                dto.CurrentTargetHashID = ResolveCurrentTaskHash(index, in drone);
                dto.TaskStateFlags = ((uint)drone.State) | ((uint)drone.FactionBit << 8) | ((uint)drone.CorridorTight << 16);
                dto.BatteryLevel = math.clamp(drone.BatteryPercent, 0f, 100f);
            }

            if (DronePositions.IsCreated && index < DronePositions.Length)
                DronePositions[index] = drone.Position;

            if (DroneStates.IsCreated && index < DroneStates.Length)
                DroneStates[index] = ResolveSoaState(in drone);
        }

        private void EnqueueServiceCommand(int index, in HeadlessDroneState drone)
        {
            if (ServiceQueueEnabled == 0)
                return;

            if (!ServiceCommands.IsCreated ||
                !ServiceCommandCursor.IsCreated ||
                ServiceCommandCursor.Length <= 0 ||
                ServiceCommandCapacity <= 0)
            {
                return;
            }

            DroneServiceCommandCursor* cursor = (DroneServiceCommandCursor*)ServiceCommandCursor.GetUnsafePtr();
            int commandIndex = Interlocked.Increment(ref cursor[0].Count) - 1;
            if ((uint)commandIndex >= (uint)ServiceCommandCapacity ||
                (uint)commandIndex >= (uint)ServiceCommands.Length)
            {
                return;
            }

            ServiceCommands[commandIndex] = new DroneServiceCommand
            {
                Slot = index,
                DroneId = drone.DroneId,
                Kind = drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile
                    ? (byte)DroneServiceCommandKind.Attack
                    : (byte)DroneServiceCommandKind.Repair,
                State = drone.State,
                Reserved = 0,
                DeltaTime = math.max(0f, DeltaTime),
                Position = drone.Position,
                TargetPosition = drone.TargetPosition
            };
        }

        private static byte ResolveSoaState(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                drone.State == (byte)HeadlessDroneRuntimeState.Attack)
            {
                return (byte)DroneFleetSoaState.Repairing;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
            {
                return (byte)DroneFleetSoaState.Returning;
            }

            return (byte)DroneFleetSoaState.Idle;
        }

        private bool ShouldRunSteeringTick(int index)
        {
            int modulo = math.max(1, SteeringTickModulo);
            return modulo <= 1 || ((FrameIndex + index) % modulo) == 0;
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
            drone.TargetAup = IsFinite(drone.PositionAup)
                ? drone.PositionAup + global::Hecton8.World.AUPMath.ToDouble3(drone.TargetPosition - drone.Position)
                : global::Hecton8.World.AUPMath.ToDouble3(drone.TargetPosition);
            drone.State = FormationMode == (int)DroneFleetFormationMode.Escort
                ? (byte)HeadlessDroneRuntimeState.Escort
                : (byte)HeadlessDroneRuntimeState.SearchGrid;
            return true;
        }

        private float3 ResolveBoidSteering(int selfIndex, ref HeadlessDroneState drone, float3 routeDirection)
        {
            float3 separation = float3.zero;
            float3 alignment = float3.zero;
            float3 cohesion = float3.zero;
            int neighborCount = 0;

            if (DroneSpatialBucketHeads.IsCreated &&
                DroneSpatialNextIndices.IsCreated &&
                DroneSpatialKeys.IsCreated &&
                DroneSpatialBucketMask > 0)
            {
                int3 centerCell = ResolveSpatialCell(drone.Position);
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            int key = PackSpatialKey(centerCell + new int3(x, y, z));
                            int bucket = ResolveSpatialBucket(key);
                            int otherIndex = DroneSpatialBucketHeads[bucket];
                            int guard = 0;

                            while ((uint)otherIndex < (uint)DroneSpatialNextIndices.Length && guard++ < ReadDrones.Length)
                            {
                                int nextIndex = DroneSpatialNextIndices[otherIndex];
                                if (otherIndex != selfIndex &&
                                    (uint)otherIndex < (uint)ReadDrones.Length &&
                                    (uint)otherIndex < (uint)DroneSpatialKeys.Length &&
                                    DroneSpatialKeys[otherIndex] == key)
                                {
                                    HeadlessDroneState other = ReadDrones[otherIndex];
                                    if (other.State != (byte)HeadlessDroneRuntimeState.Empty &&
                                        other.State != (byte)HeadlessDroneRuntimeState.Sacrificed &&
                                        other.State != (byte)HeadlessDroneRuntimeState.Completed &&
                                        other.State != (byte)HeadlessDroneRuntimeState.ResupplyCommitPending &&
                                        other.State != (byte)HeadlessDroneRuntimeState.Reboot)
                                    {
                                        float3 offset = drone.Position - other.Position;
                                        float distanceSq = math.lengthsq(offset);
                                        if (distanceSq <= SeparationRadiusSq)
                                        {
                                            neighborCount++;
                                            separation += SafeNormalize(offset) * math.rcp(math.max(0.04f, distanceSq));
                                            alignment += other.Velocity;
                                            cohesion += other.Position;
                                        }
                                    }
                                }

                                otherIndex = nextIndex;
                            }
                        }
                    }
                }
            }

            float separationWeight = ResolveSeparationWeight();
            float3 force = routeDirection + (separation * separationWeight);
            if (neighborCount > 0)
            {
                float invCount = math.rcp((float)neighborCount);
                force += (SafeNormalize(alignment * invCount, routeDirection) - SafeNormalize(drone.Velocity, routeDirection)) * AlignmentWeight;
                float cohesionWeight = ResolveCohesionWeight(in drone);
                force += SafeNormalize((cohesion * invCount) - drone.Position) * cohesionWeight;
            }

            if (PlayerPositionValid != 0)
            {
                float3 playerOffset = drone.Position - PlayerPosition;
                float playerDistanceSq = math.lengthsq(playerOffset);
                if (playerDistanceSq <= PlayerSeparationRadiusSq)
                    force += SafeNormalize(playerOffset) * (separationWeight * 3f * math.rcp(math.max(0.04f, playerDistanceSq)));
            }

            if (SdfRepulsionStrength > 0f &&
                SdfGrid.TrySampleRepulsion(drone.Position, out float3 sdfNormal, out float sdfDistance))
            {
                float repulsion = SdfRepulsionStrength * math.rcp(math.max(0.04f, sdfDistance * sdfDistance));
                force += sdfNormal * repulsion;
                drone.AvoidanceHysteresisSeconds = math.max(drone.AvoidanceHysteresisSeconds, CollisionAvoidanceHoldSeconds);
            }

            float forceLengthSq = math.lengthsq(force);
            if (forceLengthSq > MaxSteering * MaxSteering)
                force *= math.rsqrt(forceLengthSq) * MaxSteering;

            if (drone.CorridorTight != 0 && forceLengthSq > separationWeight * separationWeight)
                drone.AvoidanceHysteresisSeconds = math.max(drone.AvoidanceHysteresisSeconds, CollisionAvoidanceHoldSeconds);

            return force;
        }

        private float ResolveSeparationWeight()
        {
            return math.max(0f, SdfRepulsionStrength * (SeparationWeight * 0.25f));
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
            if (TryResolveMacroWaypoint(index, in drone, out float3 macroWaypoint))
                return macroWaypoint;

            if (drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking)
            {
                return ResolveAupDestination(drone.HomeAup, drone.PositionAup, drone.Position, drone.HomePosition);
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel)
                return ResolveAupDestination(drone.SupplyAup, drone.PositionAup, drone.Position, drone.SupplyPosition);

            if (drone.State == (byte)HeadlessDroneRuntimeState.Wander)
                return drone.TargetPosition;

            if (drone.State == (byte)HeadlessDroneRuntimeState.Escort ||
                drone.State == (byte)HeadlessDroneRuntimeState.SearchGrid)
            {
                return ResolveFormationDestination(index, drone.State == (byte)HeadlessDroneRuntimeState.Escort
                    ? (int)DroneFleetFormationMode.Escort
                    : (int)DroneFleetFormationMode.SearchGrid);
            }

            if (DroneTargets.IsCreated && (uint)index < (uint)DroneTargets.Length)
            {
                DroneTargetDTO target = DroneTargets[index];
                if ((target.Flags & 1u) != 0u)
                    return ResolveAupDestination(target.TargetAUP, drone.PositionAup, drone.Position, target.LocalPosition);
            }

            return ResolveAupDestination(drone.TargetAup, drone.PositionAup, drone.Position, drone.TargetPosition);
        }

        private bool TryResolveMacroWaypoint(int index, in HeadlessDroneState drone, out float3 waypoint)
        {
            waypoint = default;
            if (!MacroWaypoints.IsCreated ||
                !MacroWaypointStates.IsCreated ||
                (uint)index >= (uint)MacroWaypoints.Length ||
                (uint)index >= (uint)MacroWaypointStates.Length ||
                MacroWaypointStates[index] == 0)
            {
                return false;
            }

            PathWaypointDTO route = MacroWaypoints[index];
            waypoint = IsFinite(route.PositionAUP) && IsFinite(drone.PositionAup)
                ? ResolveAupDestination(route.PositionAUP, drone.PositionAup, drone.Position, route.LocalPosition)
                : route.LocalPosition;

            if (!IsFinite(waypoint))
                return false;

            float3 delta = waypoint - drone.Position;
            return math.lengthsq(delta) > MinimumVectorLengthSq;
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
            drone.TargetAup = drone.HubGridId != 0
                ? drone.HomeAup
                : drone.PositionAup + global::Hecton8.World.AUPMath.ToDouble3(drone.TargetPosition - drone.Position);
            drone.State = drone.HubGridId != 0
                ? (byte)HeadlessDroneRuntimeState.Return
                : (byte)HeadlessDroneRuntimeState.Wander;
        }

        private void TickDocking(int index, ref HeadlessDroneState drone)
        {
            if (drone.DockingPathLengthMeters < DockingMinimumPathLengthMeters ||
                !IsFinite(drone.DockControlP0) ||
                !IsFinite(drone.DockControlP1) ||
                !IsFinite(drone.DockControlP2) ||
                !IsFinite(drone.DockControlP3))
            {
                PrepareDockingSpline(ref drone);
            }

            float progress = math.saturate(drone.DockingElapsed);
            float cubicT = progress * progress * progress;
            float startSpeed = math.max(DockingArrivalSpeedMetersPerSecond, drone.MaxSpeed);
            float speed = startSpeed + ((DockingArrivalSpeedMetersPerSecond - startSpeed) * cubicT);
            float pathLength = math.max(DockingMinimumPathLengthMeters, drone.DockingPathLengthMeters);
            float t = math.saturate(progress + (math.max(0f, DeltaTime) * speed * math.rcp(pathLength)));
            EvaluateDockingBezier(in drone, t, out float3 targetPosition, out float3 tangent);

            float3 dockingDelta = targetPosition - drone.Position;
            drone.DockingElapsed = t;
            drone.Position = targetPosition;
            drone.PositionAup = OffsetAupMeters(drone.PositionAup, dockingDelta, drone.Position);
            drone.Velocity = tangent * speed;
            drone.Rotation = ResolveDockingVisualRotation(in drone, tangent, targetPosition);

            if (t >= DockingHatchOpenT && (drone.DockingFlags & DockingFlagHatchOpenQueued) == 0)
            {
                EnqueueDockingHatchOpenCommand(index, in drone, t);
                drone.DockingFlags |= DockingFlagHatchOpenQueued;
            }

            if (t < 1f)
                return;

            drone.Position = drone.HomePosition;
            drone.PositionAup = drone.HomeAup;
            drone.Rotation = ResolveSafeRotation(drone.HomeRotation);
            drone.Velocity = float3.zero;
            drone.DockingElapsed = 1f;
            drone.DockingFlags |= DockingFlagCompleted;
            drone.State = (byte)HeadlessDroneRuntimeState.Completed;
        }

        internal static void BeginDocking(ref HeadlessDroneState drone)
        {
            drone.DockingElapsed = 0f;
            drone.DockStartPosition = drone.Position;
            drone.DockStartRotation = ResolveSafeRotation(drone.Rotation);
            drone.DockingFlags = 0;
            PrepareDockingSpline(ref drone);
            drone.Velocity = float3.zero;
            drone.State = (byte)HeadlessDroneRuntimeState.Docking;
        }

        internal static void PrepareDockingSpline(ref HeadlessDroneState drone)
        {
            quaternion startRotation = ResolveSafeRotation(drone.DockStartRotation);
            quaternion targetRotation = ResolveSafeRotation(drone.HomeRotation);
            float3 startForward = SafeNormalize(math.mul(startRotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
            float3 airlockForward = SafeNormalize(math.mul(targetRotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));

            drone.DockControlP0 = global::Hecton8.World.AUPMath.ToDouble3(IsFinite(drone.DockStartPosition) ? drone.DockStartPosition : drone.Position);
            drone.DockControlP1 = drone.DockControlP0 + (global::Hecton8.World.AUPMath.ToDouble3(startForward) * DockingStartForwardMeters);
            drone.DockControlP2 = global::Hecton8.World.AUPMath.ToDouble3(drone.HomePosition) + (global::Hecton8.World.AUPMath.ToDouble3(airlockForward) * DockingAirlockForwardMeters);
            drone.DockControlP3 = global::Hecton8.World.AUPMath.ToDouble3(drone.HomePosition);

            float estimate =
                DockingStartForwardMeters +
                DockingAirlockForwardMeters +
                ApproximateDistanceNoSqrt(ToFloat3(drone.DockControlP2 - drone.DockControlP1));
            drone.DockingPathLengthMeters = math.max(DockingMinimumPathLengthMeters, estimate);
        }

        private void EnqueueDockingHatchOpenCommand(int index, in HeadlessDroneState drone, float t)
        {
            if (ServiceQueueEnabled == 0)
                return;

            if (!ServiceCommands.IsCreated ||
                !ServiceCommandCursor.IsCreated ||
                ServiceCommandCursor.Length <= 0 ||
                ServiceCommandCapacity <= 0)
            {
                return;
            }

            DroneServiceCommandCursor* cursor = (DroneServiceCommandCursor*)ServiceCommandCursor.GetUnsafePtr();
            int commandIndex = Interlocked.Increment(ref cursor[0].Count) - 1;
            if ((uint)commandIndex >= (uint)ServiceCommandCapacity ||
                (uint)commandIndex >= (uint)ServiceCommands.Length)
            {
                return;
            }

            ServiceCommands[commandIndex] = new DroneServiceCommand
            {
                Slot = index,
                DroneId = drone.DroneId,
                Kind = (byte)DroneServiceCommandKind.DockingHatchOpen,
                State = drone.State,
                Reserved = 0,
                DeltaTime = t,
                Position = drone.Position,
                TargetPosition = drone.HomePosition
            };
        }

        private quaternion ResolveDockingVisualRotation(in HeadlessDroneState drone, float3 tangent, float3 position)
        {
            float3 visualForward = tangent;
            float slipQuality = math.saturate(CrossCurrentVisualSlipWeight);
            if (slipQuality > 0f)
            {
                float3 flowVelocity = ResolveFlowVelocity(position);
                float3 crossCurrent = flowVelocity - (tangent * math.dot(flowVelocity, tangent));
                crossCurrent -= math.up() * math.dot(crossCurrent, math.up());
                float crossLengthSq = math.lengthsq(crossCurrent);
                if (math.isfinite(crossLengthSq) && crossLengthSq > MinimumVectorLengthSq)
                {
                    float slip = math.saturate(ApproximateDistanceNoSqrt(crossCurrent) * DockingVisualSlipWeight * slipQuality);
                    visualForward = SafeNormalize(tangent + (SafeNormalize(crossCurrent, tangent) * slip), tangent);
                }
            }

            return quaternion.LookRotationSafe(visualForward, math.up());
        }

        private static void EvaluateDockingBezier(in HeadlessDroneState drone, float t, out float3 position, out float3 tangent)
        {
            double clampedT = math.saturate(t);
            double oneMinusT = 1.0 - clampedT;
            double oneMinusT2 = oneMinusT * oneMinusT;
            double t2 = clampedT * clampedT;
            double3 p0 = drone.DockControlP0;
            double3 p1 = drone.DockControlP1;
            double3 p2 = drone.DockControlP2;
            double3 p3 = drone.DockControlP3;

            double3 positionDouble =
                (oneMinusT2 * oneMinusT * p0) +
                (3.0 * oneMinusT2 * clampedT * p1) +
                (3.0 * oneMinusT * t2 * p2) +
                (t2 * clampedT * p3);

            double3 derivativeDouble =
                (3.0 * oneMinusT2 * (p1 - p0)) +
                (6.0 * oneMinusT * clampedT * (p2 - p1)) +
                (3.0 * t2 * (p3 - p2));
            position = ToFloat3(positionDouble);
            if (!IsFinite(position))
                position = IsFinite(drone.HomePosition) ? drone.HomePosition : drone.Position;

            float3 derivative = ToFloat3(derivativeDouble);
            tangent = SafeNormalize(derivative, SafeNormalize(drone.HomePosition - drone.DockStartPosition, new float3(0f, 0f, 1f)));
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
            float horizontalCellSizeInv = math.rcp(AbyssalFlowHorizontalCellSize);
            float verticalCellSizeInv = math.rcp(AbyssalFlowVerticalCellSize);
            float normalizedX = math.clamp((position.x - minX) * horizontalCellSizeInv, 0f, AbyssalFlowResolutionXZ - 1);
            float normalizedZ = math.clamp((position.z - minZ) * horizontalCellSizeInv, 0f, AbyssalFlowResolutionXZ - 1);
            float normalizedY = math.clamp((maxY - clampedY) * verticalCellSizeInv, 0f, AbyssalFlowResolutionY - 1);
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
            float3 sampleX00 = BlendLinear(sample000, sample100, fracX);
            float3 sampleX10 = BlendLinear(sample010, sample110, fracX);
            float3 sampleX01 = BlendLinear(sample001, sample101, fracX);
            float3 sampleX11 = BlendLinear(sample011, sample111, fracX);
            float3 sampleZ0 = BlendLinear(sampleX00, sampleX10, fracZ);
            float3 sampleZ1 = BlendLinear(sampleX01, sampleX11, fracZ);
            return BlendLinear(sampleZ0, sampleZ1, fracY);
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

        private static void SanitizeKinematics(ref HeadlessDroneState drone)
        {
            if (!IsFinite(drone.Position))
                drone.Position = IsFinite(drone.HomePosition) ? drone.HomePosition : float3.zero;

            if (!IsFinite(drone.Velocity))
                drone.Velocity = float3.zero;
        }

        private static void SanitizeAupFields(ref HeadlessDroneState drone)
        {
            if (!IsFinite(drone.PositionAup))
                drone.PositionAup = global::Hecton8.World.AUPMath.ToDouble3(drone.Position);

            if (!IsFinite(drone.HomeAup))
                drone.HomeAup = global::Hecton8.World.AUPMath.ToDouble3(IsFinite(drone.HomePosition) ? drone.HomePosition : drone.Position);

            if (!IsFinite(drone.TargetAup))
                drone.TargetAup = global::Hecton8.World.AUPMath.ToDouble3(IsFinite(drone.TargetPosition) ? drone.TargetPosition : drone.Position);

            if (!IsFinite(drone.SupplyAup))
                drone.SupplyAup = global::Hecton8.World.AUPMath.ToDouble3(IsFinite(drone.SupplyPosition) ? drone.SupplyPosition : drone.HomePosition);
        }

        private static float3 ResolveAupDestination(double3 targetAup, double3 originAup, float3 originLocal, float3 fallbackLocal)
        {
            if (IsFinite(targetAup) && IsFinite(originAup))
            {
                double3 delta = targetAup - originAup;
                float3 localDelta = ToFloat3(delta);
                if (IsFinite(localDelta))
                    return originLocal + localDelta;
            }

            return IsFinite(fallbackLocal) ? fallbackLocal : originLocal;
        }

        private static double3 OffsetAupMeters(double3 originAup, float3 deltaMeters, float3 fallbackLocalPosition)
        {
            double3 delta = global::Hecton8.World.AUPMath.ToDouble3(deltaMeters);
            double3 result = originAup + delta;
            return IsFinite(result) ? result : global::Hecton8.World.AUPMath.ToDouble3(fallbackLocalPosition);
        }

        private static uint ResolveCurrentTaskHash(int index, in HeadlessDroneState drone)
        {
            return math.hash(new uint3(
                (uint)math.max(0, index + 1),
                (uint)math.max(0, drone.TargetTaskIndex + 1),
                (uint)drone.State));
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

        private static float FastLengthFromSq(float lengthSq)
        {
            float safeLengthSq = math.max(0f, lengthSq);
            return safeLengthSq * math.rsqrt(math.max(safeLengthSq, LengthEpsilonSq));
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

        private static float3 BlendLinear(float3 from, float3 to, float t)
        {
            float clampedT = math.saturate(t);
            return from + ((to - from) * clampedT);
        }

        private static float ApproximateDistanceNoSqrt(float3 delta)
        {
            float3 absolute = math.abs(delta);
            float maxAxis = math.cmax(absolute);
            float minAxis = math.cmin(absolute);
            float midAxis = (absolute.x + absolute.y + absolute.z) - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.25f);
        }

        internal static int PackSpatialKey(float3 position)
        {
            return PackSpatialKey(ResolveSpatialCell(position));
        }

        private static int3 ResolveSpatialCell(float3 position)
        {
            int x = math.clamp((int)math.floor((position.x - SpatialBoundsMin) * SpatialCellSizeInv), 0, SpatialGridResolution - 1);
            int y = math.clamp((int)math.floor((position.y - SpatialBoundsMin) * SpatialCellSizeInv), 0, SpatialGridResolution - 1);
            int z = math.clamp((int)math.floor((position.z - SpatialBoundsMin) * SpatialCellSizeInv), 0, SpatialGridResolution - 1);
            return new int3(x, y, z);
        }

        private static int PackSpatialKey(int3 cell)
        {
            return cell.x + (cell.y * SpatialGridResolution) + (cell.z * SpatialGridResolution * SpatialGridResolution);
        }

        private int ResolveSpatialBucket(int key)
        {
            uint hash = (uint)key;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            return (int)(hash & (uint)DroneSpatialBucketMask);
        }
    }
}
