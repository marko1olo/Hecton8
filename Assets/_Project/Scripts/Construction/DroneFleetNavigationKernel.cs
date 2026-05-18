using Hecton8.Core.Contracts.Signals;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct DroneFleetTuningConstants
    {
        public float MaxDroneSpeed;
        public float BatteryDrainRate;
        public float SdfRepulsionStrength;
        public float RepairSpeed;
        public float CargoCapacity;
        public float MiningHoldSeconds;
        public float LowTierSteeringHz;
        public float MidTierSteeringHz;
        public float HighTierSteeringHz;
        public float UltraTierSteeringHz;
        public float AStarCellSize;
        public float LowTierSolveBudget;
        public float MidTierSolveBudget;
        public float HighTierSolveBudget;
        public float UltraTierSolveBudget;
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

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct DroneStateDTO
    {
        public double3 AUP;
        public float3 Velocity;
        public uint TargetHash;
        public uint CurrentTask;
        public float Battery;
        public uint Reserved0;
        public uint Reserved1;
        public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal struct PathWaypointDTO
    {
        public float3 LocalPosition;
        public uint ActionCode;
    }

    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct DroneFleetDebugRoute
    {
        public float3 Position;
        public float3 Target;
        public float3 Waypoint;
        public float3 SdfNormal;
        public float3 RoutePoint0;
        public float3 RoutePoint1;
        public float3 RoutePoint2;
        public float3 RoutePoint3;
        public int RoutePointCount;
        public int DroneId;
        public int PathStatus;
        public float BatteryPercent;
        public byte State;
        public byte Flags;
        public ushort Reserved0;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
    }

    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct DroneFleetAutomationStats
    {
        public int ActiveDrones;
        public int PathSolves;
        public int PathFailures;
        public int PathIterations;
        public int TasksCompleted;
        public int LastAStarStatus;
        public int SteeringTickModulo;
        public float AveragePathfindingTimeMs;
        public float SdfRepulsionStrength;
        public float AStarCellSize;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct DroneFleetMockRepairSignal : ISignal
    {
        public int DroneId;
        public int TargetModuleId;
        public float RepairUnits;
        public float3 Position;
        public uint Flags;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct DroneFleetMockMiningSignal : ISignal
    {
        public int DroneId;
        public int TargetNodeId;
        public float WorkSeconds;
        public float3 Position;
        public uint Flags;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct DroneFleetInventoryTransactionSignal : ISignal
    {
        public int DroneId;
        public int SourceId;
        public int DestinationId;
        public int ItemHash;
        public int Quantity;
        public float3 Position;
        public uint Flags;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct DroneTaskDTO
    {
        public double3 TargetAup;
        public float3 LocalPosition;
        public float Priority;
        public float Score;
        public float CriticalityWeight;
        public float Radius;
        public int ModuleIndex;
        public int TaskKind;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct MockSDFGrid
    {
        public float3 BoundsMin;
        public float RepulsionDistance;
        public float3 BoundsMax;
        public float SeamSpacing;
        public float3 SeamNormal;
        public float SeamHalfWidth;
        public int Enabled;
        public int Reserved0;
        public float Reserved1;
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

    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal struct DroneNativeMinHeapNode
    {
        public float Cost;
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

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct DroneAStarTelemetry
    {
        public int SolvedCount;
        public int FailedCount;
        public int IterationCount;
        public int LastStatus;
        public int ActiveCandidateCount;
        public int Reserved0;
        public int Reserved1;
        public int Reserved2;
    }

    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct DroneMacroAStarJob : IJob
    {
        private const int GridSide = 8;
        private const int GridSideSq = GridSide * GridSide;
        private const int NodeCapacity = GridSide * GridSide * GridSide;
        private const int StartCoord = GridSide >> 1;
        private const int StartNode = StartCoord + (StartCoord * GridSide) + (StartCoord * GridSideSq);
        private const float VerticalPenalty = 1.85f;
        private const float HugeCost = 3.402823e+38f;

        [ReadOnly] public NativeArray<HeadlessDroneState> Drones;
        public NativeArray<PathWaypointDTO> Waypoints;
        public NativeArray<byte> WaypointStates;
        public NativeArray<DroneNativeMinHeapNode> OpenHeap;
        public NativeArray<float> GCosts;
        public NativeArray<int> CameFrom;
        public NativeArray<byte> NodeStates;
        public NativeArray<int> RouteNodes;
        public NativeArray<byte> RouteNodeCounts;
        public NativeArray<DroneAStarTelemetry> Telemetry;
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
