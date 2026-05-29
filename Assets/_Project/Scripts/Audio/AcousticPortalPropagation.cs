using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Propagation
{
    public static class AcousticPortalConstants
    {
        public const int MaxPathNodes = 30;
        public const int MaxPathEdges = MaxPathNodes * 2;
        public const int TelemetryFrameCount = 300;
        public const float SoundSpeedWaterMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
        public const float OpenLowPassCutoffHertz = 22000f;
        public const float MinimumLowPassCutoffHertz = 80f;
        public const float CornerLowPassHertz = 2000f;
        public const float SealedBulkheadLowPassHertz = 400f;
        public const float SealedBulkheadDelaySeconds = 0.010f;
        public const float CornerGain = 0.70794576f;
        public const float MaximumItdSeconds = 0.00065f;
        public const int AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;
    }

    [Flags]
    public enum AcousticPortalFlags : byte
    {
        None = 0,
        Voxel = 1 << 0,
        Habitat = 1 << 1,
        SealedBulkhead = 1 << 2,
        Solid = 1 << 3,
        StationaryEmitter = 1 << 4
    }

    public enum AcousticPathStatus : byte
    {
        None = 0,
        SurvivalBudgetFallback = 1,
        LowTierFallback = SurvivalBudgetFallback,
        NoGraph = 2,
        NoPath = 3,
        PathFound = 4,
        InvalidInput = 5
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct AcousticPortalNode
    {
        [FieldOffset(0)]
        public AcousticAup Position;
        [FieldOffset(40)]
        public int FirstEdge;
        [FieldOffset(44)]
        public int EdgeCount;
        [FieldOffset(48)]
        public float RoomVolumeCubicMeters;
        [FieldOffset(52)]
        public AcousticPortalFlags Flags;
        [FieldOffset(53)]
        private byte _pad0;
        [FieldOffset(54)]
        private byte _pad1;
        [FieldOffset(55)]
        private byte _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AcousticPortalEdge
    {
        [FieldOffset(0)]
        public int ToNode;
        [FieldOffset(4)]
        public float DistanceMeters;
        [FieldOffset(8)]
        public AcousticPortalFlags Flags;
        [FieldOffset(9)]
        private byte _pad0;
        [FieldOffset(10)]
        private byte _pad1;
        [FieldOffset(11)]
        private byte _pad2;
        [FieldOffset(12)]
        private byte _pad3;
        [FieldOffset(13)]
        private byte _pad4;
        [FieldOffset(14)]
        private byte _pad5;
        [FieldOffset(15)]
        private byte _pad6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct AcousticPathQuery
    {
        [FieldOffset(0)]
        public AcousticAup SourceAup;
        [FieldOffset(40)]
        public AcousticAup ListenerAup;
        [FieldOffset(80)]
        public float3 ListenerRight;
        [FieldOffset(92)]
        public int NodeCount;
        [FieldOffset(96)]
        public int EdgeCount;
        [FieldOffset(100)]
        public int MaxNodeExpansions;
        [FieldOffset(104)]
        public float GlobalQualityWeight;
        [FieldOffset(108)]
        public byte DisablePortalPath;
        [FieldOffset(109)]
        private byte _pad0;
        [FieldOffset(110)]
        private byte _pad1;
        [FieldOffset(111)]
        private byte _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public readonly struct SoundEmissionSignal
    {
        [FieldOffset(0)]
        public readonly AcousticAup SourceAup;
        [FieldOffset(40)]
        public readonly float Volume;
        [FieldOffset(44)]
        public readonly float Pitch;
        [FieldOffset(48)]
        public readonly uint EventID;
        [FieldOffset(52)]
        public readonly int StationaryCacheKey;
        [FieldOffset(56)]
        public readonly AcousticPortalFlags Flags;
        [FieldOffset(57)]
        private readonly byte _pad0;
        [FieldOffset(58)]
        private readonly byte _pad1;
        [FieldOffset(59)]
        private readonly byte _pad2;
        [FieldOffset(60)]
        private readonly byte _pad3;
        [FieldOffset(61)]
        private readonly byte _pad4;
        [FieldOffset(62)]
        private readonly byte _pad5;
        [FieldOffset(63)]
        private readonly byte _pad6;

        public SoundEmissionSignal(
            uint eventID,
            AcousticAup sourceAup,
            float volume,
            float pitch,
            int stationaryCacheKey,
            AcousticPortalFlags flags)
        {
            EventID = eventID;
            SourceAup = sourceAup;
            Volume = volume;
            Pitch = pitch;
            StationaryCacheKey = stationaryCacheKey;
            Flags = flags;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
            _pad4 = 0;
            _pad5 = 0;
            _pad6 = 0;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 104)]
    public struct AcousticPathResult
    {
        [FieldOffset(0)]
        public AcousticAup LastPortalAup;
        [FieldOffset(40)]
        public float TrueDistanceMeters;
        [FieldOffset(44)]
        public float DelaySeconds;
        [FieldOffset(48)]
        public float Transmission01;
        [FieldOffset(52)]
        public float LowPassCutoffHz;
        [FieldOffset(56)]
        public float ItdSeconds;
        [FieldOffset(60)]
        public float RoomVolumeCubicMeters;
        [FieldOffset(64)]
        public float PathfindingMs;
        [FieldOffset(68)]
        public int NodeCount;
        [FieldOffset(72)]
        public int CornerCount;
        [FieldOffset(76)]
        public int ExpandedNodeCount;
        [FieldOffset(80)]
        public int SourceNodeIndex;
        [FieldOffset(84)]
        public int ListenerNodeIndex;
        [FieldOffset(88)]
        public uint StateHash;
        [FieldOffset(92)]
        public AcousticPathStatus Status;
        [FieldOffset(93)]
        public byte UsedPortalPath;
        [FieldOffset(94)]
        public byte UsedSealedBulkhead;
        [FieldOffset(95)]
        public byte UsedReprojectionCache;
        [FieldOffset(96)]
        private byte _pad0;
        [FieldOffset(97)]
        private byte _pad1;
        [FieldOffset(98)]
        private byte _pad2;
        [FieldOffset(99)]
        private byte _pad3;
        [FieldOffset(100)]
        private byte _pad4;
        [FieldOffset(101)]
        private byte _pad5;
        [FieldOffset(102)]
        private byte _pad6;
        [FieldOffset(103)]
        private byte _pad7;

        public static AcousticPathResult Fallback(AcousticPathStatus status, in AcousticPathQuery query)
        {
            bool sourceFinite = AcousticAup.IsFinite(in query.SourceAup);
            bool listenerFinite = AcousticAup.IsFinite(in query.ListenerAup);
            float distance = sourceFinite && listenerFinite
                ? AcousticAup.DistanceMeters(in query.SourceAup, in query.ListenerAup)
                : 0f;
            if (!math.isfinite(distance) || distance < 0f)
                distance = 0f;
            float delay = distance * math.rcp(AcousticPortalConstants.SoundSpeedWaterMetersPerSecond);
            if (!math.isfinite(delay) || delay < 0f)
                delay = 0f;
            AcousticAup lastPortalAup = sourceFinite ? query.SourceAup : default;
            return new AcousticPathResult
            {
                Status = status,
                UsedPortalPath = 0,
                UsedSealedBulkhead = 0,
                UsedReprojectionCache = 0,
                NodeCount = 0,
                CornerCount = 0,
                ExpandedNodeCount = 0,
                SourceNodeIndex = -1,
                ListenerNodeIndex = -1,
                TrueDistanceMeters = distance,
                DelaySeconds = delay,
                Transmission01 = 1f,
                LowPassCutoffHz = AcousticPortalConstants.OpenLowPassCutoffHertz,
                ItdSeconds = 0f,
                RoomVolumeCubicMeters = 0f,
                PathfindingMs = 0f,
                LastPortalAup = lastPortalAup,
                StateHash = 0u,
                _pad0 = 0,
                _pad1 = 0,
                _pad2 = 0,
                _pad3 = 0,
                _pad4 = 0,
                _pad5 = 0,
                _pad6 = 0,
                _pad7 = 0
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticTelemetryEntry
    {
        [FieldOffset(0)]
        public long StopwatchTicks;
        [FieldOffset(8)]
        public int Frame;
        [FieldOffset(12)]
        public int NodeCount;
        [FieldOffset(16)]
        public int CornerCount;
        [FieldOffset(20)]
        public int ExpandedNodeCount;
        [FieldOffset(24)]
        public float PathfindingMs;
        [FieldOffset(28)]
        public float TrueDistanceMeters;
        [FieldOffset(32)]
        public float DelaySeconds;
        [FieldOffset(36)]
        public float LowPassCutoffHz;
        [FieldOffset(40)]
        public uint Flags;
        [FieldOffset(44)]
        public uint StateHash;
        [FieldOffset(48)]
        public uint BufferId;
        [FieldOffset(52)]
        public uint Generation;
        [FieldOffset(56)]
        public uint FailureCode;
        [FieldOffset(60)]
        private byte _pad0;
        [FieldOffset(61)]
        private byte _pad1;
        [FieldOffset(62)]
        private byte _pad2;
        [FieldOffset(63)]
        private byte _pad3;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct AcousticPathJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AcousticPortalNode> Nodes;
        [ReadOnly, NoAlias] public NativeArray<AcousticPortalEdge> Edges;
        [NoAlias]
        public NativeArray<int> OpenSet;
        [NoAlias]
        public NativeArray<int> ClosedSet;
        [NoAlias]
        public NativeArray<float> Costs;
        [NoAlias]
        public NativeArray<int> CameFrom;
        [NoAlias]
        public NativeArray<byte> States;
        [WriteOnly, NoAlias] public NativeArray<AcousticPathResult> Result;
        public AcousticPathQuery Query;

        public void Execute()
        {
            float portalBudget01 = ResolvePortalBudget01(Query.GlobalQualityWeight);
            AcousticPathResult fallback = AcousticPathResult.Fallback(
                Query.DisablePortalPath != 0 || portalBudget01 <= 0.0001f
                    ? AcousticPathStatus.SurvivalBudgetFallback
                    : AcousticPathStatus.NoGraph,
                in Query);

            if (!Result.IsCreated || Result.Length <= 0)
                return;

            Result[0] = fallback;

            if (Query.DisablePortalPath != 0 || portalBudget01 <= 0.0001f)
                return;

            if (!AcousticAup.IsFinite(in Query.SourceAup) ||
                !AcousticAup.IsFinite(in Query.ListenerAup))
            {
                fallback.Status = AcousticPathStatus.InvalidInput;
                Result[0] = fallback;
                return;
            }

            if (!Nodes.IsCreated ||
                !Edges.IsCreated ||
                !Costs.IsCreated ||
                !CameFrom.IsCreated ||
                !States.IsCreated ||
                !OpenSet.IsCreated ||
                !ClosedSet.IsCreated ||
                OpenSet.Length <= 0 ||
                ClosedSet.Length <= 0)
            {
                Result[0] = fallback;
                return;
            }

            int nodeCount = math.min(
                AcousticPortalConstants.MaxPathNodes,
                math.min(Query.NodeCount, math.min(Nodes.Length, math.min(Costs.Length, math.min(CameFrom.Length, States.Length)))));
            int edgeCount = math.min(AcousticPortalConstants.MaxPathEdges, math.min(Query.EdgeCount, Edges.Length));
            int requestedExpansions = Query.MaxNodeExpansions <= 0 ? AcousticPortalConstants.MaxPathNodes : Query.MaxNodeExpansions;
            int continuousExpansionBudget = (int)math.round(math.lerp(2f, requestedExpansions, portalBudget01));
            int maxExpansions = math.clamp(
                continuousExpansionBudget,
                1,
                AcousticPortalConstants.MaxPathNodes);

            if (nodeCount < 2 || edgeCount <= 0)
            {
                fallback.Status = AcousticPathStatus.NoGraph;
                Result[0] = fallback;
                return;
            }

            int sourceNode = FindNearestNode(in Query.SourceAup, nodeCount);
            int listenerNode = FindNearestNode(in Query.ListenerAup, nodeCount);
            if (sourceNode < 0 || listenerNode < 0)
            {
                fallback.Status = AcousticPathStatus.InvalidInput;
                Result[0] = fallback;
                return;
            }

            for (int i = 0; i < nodeCount; i++)
            {
                Costs[i] = float.PositiveInfinity;
                CameFrom[i] = -1;
                States[i] = 0;
            }

            int openCount = 0;
            int closedCount = 0;
            Costs[sourceNode] = 0f;
            States[sourceNode] = 1;
            OpenSet[openCount++] = sourceNode;

            int expanded = 0;
            bool found = false;
            while (openCount > 0 && expanded < maxExpansions)
            {
                int openIndex = FindLowestCostOpenIndex(openCount);
                int current = OpenSet[openIndex];
                openCount--;
                OpenSet[openIndex] = OpenSet[openCount];

                if ((uint)current >= (uint)nodeCount || States[current] == 2)
                    continue;

                States[current] = 2;
                if (closedCount < ClosedSet.Length)
                    ClosedSet[closedCount++] = current;
                expanded++;

                if (current == listenerNode)
                {
                    found = true;
                    break;
                }

                AcousticPortalNode node = Nodes[current];
                int start = math.clamp(node.FirstEdge, 0, edgeCount);
                int end = math.clamp(node.FirstEdge + node.EdgeCount, start, edgeCount);
                for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
                {
                    AcousticPortalEdge edge = Edges[edgeIndex];
                    int next = edge.ToNode;
                    if ((uint)next >= (uint)nodeCount || States[next] == 2)
                        continue;

                    AcousticPortalNode nextNode = Nodes[next];
                    if ((nextNode.Flags & AcousticPortalFlags.Solid) != 0)
                        continue;

                    float edgeDistance = edge.DistanceMeters;
                    if (!math.isfinite(edgeDistance) || edgeDistance <= 0.001f)
                    {
                        AcousticAup nodePosition = node.Position;
                        AcousticAup nextNodePosition = nextNode.Position;
                        edgeDistance = math.max(0.001f, AcousticAup.DistanceMeters(in nodePosition, in nextNodePosition));
                    }

                    float nextCost = Costs[current] + edgeDistance;
                    if (nextCost >= Costs[next])
                        continue;

                    Costs[next] = nextCost;
                    CameFrom[next] = current;
                    if (States[next] == 0 && openCount < OpenSet.Length)
                    {
                        States[next] = 1;
                        OpenSet[openCount++] = next;
                    }
                }
            }

            if (!found)
            {
                fallback.Status = AcousticPathStatus.NoPath;
                fallback.ExpandedNodeCount = expanded;
                Result[0] = fallback;
                return;
            }

            Result[0] = BuildResult(sourceNode, listenerNode, nodeCount, expanded);
        }

        private static float ResolvePortalBudget01(float globalQualityWeight)
        {
            float quality = math.isfinite(globalQualityWeight)
                ? math.saturate(globalQualityWeight)
                : 0f;
            return math.smoothstep(0.12f, 0.92f, quality);
        }

        private int FindNearestNode(in AcousticAup aup, int nodeCount)
        {
            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < nodeCount; i++)
            {
                AcousticPortalNode node = Nodes[i];
                AcousticAup nodePosition = node.Position;
                if ((node.Flags & AcousticPortalFlags.Solid) != 0 ||
                    !AcousticAup.IsFinite(in nodePosition))
                {
                    continue;
                }

                float distance = AcousticAup.DistanceMeters(in aup, in nodePosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private int FindLowestCostOpenIndex(int openCount)
        {
            int bestOpenIndex = 0;
            float bestCost = float.PositiveInfinity;
            for (int i = 0; i < openCount; i++)
            {
                int nodeIndex = OpenSet[i];
                float cost = (uint)nodeIndex < (uint)Costs.Length ? Costs[nodeIndex] : float.PositiveInfinity;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestOpenIndex = i;
                }
            }

            return bestOpenIndex;
        }

        private AcousticPathResult BuildResult(int sourceNode, int listenerNode, int nodeCount, int expanded)
        {
            int pathNode = listenerNode;
            int pathNodeCount = 1;
            int lastPortalIndex = listenerNode;
            int sealedBulkhead = 0;
            uint stateHash = 2166136261u;

            int predecessor = CameFrom[listenerNode];
            if ((uint)predecessor < (uint)nodeCount)
                lastPortalIndex = predecessor;

            while (pathNode != sourceNode && pathNodeCount < AcousticPortalConstants.MaxPathNodes)
            {
                int previous = CameFrom[pathNode];
                if ((uint)previous >= (uint)nodeCount)
                    break;

                if (TryFindEdge(previous, pathNode, out AcousticPortalEdge edge) &&
                    (edge.Flags & AcousticPortalFlags.SealedBulkhead) != 0)
                {
                    sealedBulkhead = 1;
                }

                stateHash = (stateHash ^ (uint)(pathNode + 1)) * 16777619u;
                pathNode = previous;
                pathNodeCount++;
            }

            if (pathNode != sourceNode)
            {
                AcousticPathResult invalid = AcousticPathResult.Fallback(AcousticPathStatus.NoPath, in Query);
                invalid.ExpandedNodeCount = expanded;
                invalid.StateHash = stateHash;
                return invalid;
            }

            int corners = math.max(0, pathNodeCount - 2);
            AcousticPortalNode sourcePortalNode = Nodes[sourceNode];
            AcousticPortalNode listenerPortalNode = Nodes[listenerNode];
            AcousticAup sourcePortalPosition = sourcePortalNode.Position;
            AcousticAup listenerPortalPosition = listenerPortalNode.Position;
            float distance = Costs[listenerNode] +
                AcousticAup.DistanceMeters(in Query.SourceAup, in sourcePortalPosition) +
                AcousticAup.DistanceMeters(in Query.ListenerAup, in listenerPortalPosition);
            distance = math.max(0.001f, distance);

            float delay = distance * math.rcp(AcousticPortalConstants.SoundSpeedWaterMetersPerSecond);
            if (sealedBulkhead != 0)
                delay += AcousticPortalConstants.SealedBulkheadDelaySeconds;

            float transmission = 1f;
            for (int i = 0; i < corners; i++)
                transmission *= AcousticPortalConstants.CornerGain;
            if (sealedBulkhead != 0)
                transmission *= 0.55f;
            transmission = math.saturate(transmission);

            float cutoff = AcousticPortalConstants.OpenLowPassCutoffHertz;
            if (corners > 0)
                cutoff = math.min(cutoff, AcousticPortalConstants.CornerLowPassHertz * math.rcp(corners));
            if (sealedBulkhead != 0)
                cutoff = math.min(cutoff, AcousticPortalConstants.SealedBulkheadLowPassHertz);
            cutoff = math.clamp(
                cutoff,
                AcousticPortalConstants.MinimumLowPassCutoffHertz,
                AcousticPortalConstants.OpenLowPassCutoffHertz);

            AcousticAup lastPortal = Nodes[math.clamp(lastPortalIndex, 0, nodeCount - 1)].Position;
            float3 toPortal = AcousticAup.RelativeFloat3(in lastPortal, in Query.ListenerAup);
            float distanceSq = math.lengthsq(toPortal);
            float itd = 0f;
            if (distanceSq > 0.0001f && math.all(math.isfinite(toPortal)))
            {
                float3 direction = toPortal * math.rsqrt(distanceSq);
                float lateral = math.dot(direction, Query.ListenerRight);
                itd = math.clamp(
                    lateral * AcousticPortalConstants.MaximumItdSeconds,
                    -AcousticPortalConstants.MaximumItdSeconds,
                    AcousticPortalConstants.MaximumItdSeconds);
            }

            float roomVolume = Nodes[listenerNode].RoomVolumeCubicMeters;
            if (!math.isfinite(roomVolume) || roomVolume < 0f)
                roomVolume = 0f;

            if (!math.isfinite(distance) ||
                !math.isfinite(delay) ||
                !math.isfinite(transmission) ||
                !math.isfinite(cutoff) ||
                !math.isfinite(itd))
            {
                AcousticPathResult invalid = AcousticPathResult.Fallback(AcousticPathStatus.InvalidInput, in Query);
                invalid.ExpandedNodeCount = expanded;
                invalid.StateHash = stateHash;
                return invalid;
            }

            return new AcousticPathResult
            {
                Status = AcousticPathStatus.PathFound,
                UsedPortalPath = 1,
                UsedSealedBulkhead = (byte)sealedBulkhead,
                UsedReprojectionCache = 0,
                NodeCount = pathNodeCount,
                CornerCount = corners,
                ExpandedNodeCount = expanded,
                SourceNodeIndex = sourceNode,
                ListenerNodeIndex = listenerNode,
                TrueDistanceMeters = distance,
                DelaySeconds = delay,
                Transmission01 = transmission,
                LowPassCutoffHz = cutoff,
                ItdSeconds = itd,
                RoomVolumeCubicMeters = roomVolume,
                PathfindingMs = 0f,
                LastPortalAup = lastPortal,
                StateHash = stateHash
            };
        }

        private bool TryFindEdge(int from, int to, out AcousticPortalEdge edge)
        {
            edge = default;
            if ((uint)from >= (uint)Nodes.Length)
                return false;

            AcousticPortalNode node = Nodes[from];
            int edgeLimit = math.min(Query.EdgeCount, Edges.Length);
            int start = math.clamp(node.FirstEdge, 0, edgeLimit);
            int end = math.clamp(node.FirstEdge + node.EdgeCount, start, edgeLimit);
            for (int i = start; i < end; i++)
            {
                AcousticPortalEdge candidate = Edges[i];
                if (candidate.ToNode == to)
                {
                    edge = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct GenerateMockAcousticLoadJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<AcousticPortalNode> Nodes;
        [WriteOnly, NoAlias] public NativeArray<AcousticPortalEdge> Edges;
        [WriteOnly, NoAlias] public NativeArray<AcousticPathQuery> QueryOutput;
        public int RequestedNodeCount;
        public uint Seed;
        public float GlobalQualityWeight;
        public byte DisablePortalPath;

        public void Execute()
        {
            int nodeCapacity = math.min(Nodes.Length, AcousticPortalConstants.MaxPathNodes);
            int edgeCapacity = math.min(Edges.Length, AcousticPortalConstants.MaxPathEdges);
            int requestedNodes = RequestedNodeCount <= 0 ? nodeCapacity : RequestedNodeCount;
            int nodeCount = math.clamp(requestedNodes, 0, nodeCapacity);
            int edgeCursor = 0;

            for (int i = 0; i < nodeCapacity; i++)
                Nodes[i] = default;
            for (int i = 0; i < edgeCapacity; i++)
                Edges[i] = default;

            if (nodeCount <= 0)
                return;

            for (int i = 0; i < nodeCount; i++)
            {
                AcousticPortalNode node = new AcousticPortalNode
                {
                    Position = BuildAup(i, Seed),
                    FirstEdge = edgeCursor,
                    EdgeCount = 0,
                    RoomVolumeCubicMeters = ResolveMockRoomVolume(i, Seed),
                    Flags = AcousticPortalFlags.Voxel
                };

                TryAppendMockEdge(i, i - 1, nodeCount, edgeCapacity, ref edgeCursor);
                TryAppendMockEdge(i, i + 1, nodeCount, edgeCapacity, ref edgeCursor);
                node.EdgeCount = edgeCursor - node.FirstEdge;
                Nodes[i] = node;
            }

            int queryCount = QueryOutput.Length;
            int maxExpansions = ResolveMaxNodeExpansions(nodeCount, GlobalQualityWeight);
            for (int i = 0; i < queryCount; i++)
            {
                int sourceIndex = i % nodeCount;
                int listenerIndex = (i * 7 + 3) % nodeCount;
                QueryOutput[i] = new AcousticPathQuery
                {
                    SourceAup = BuildAup(sourceIndex, Seed),
                    ListenerAup = BuildAup(listenerIndex, Seed),
                    ListenerRight = new float3(1f, 0f, 0f),
                    NodeCount = nodeCount,
                    EdgeCount = edgeCursor,
                    MaxNodeExpansions = maxExpansions,
                    GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                    DisablePortalPath = DisablePortalPath
                };
            }
        }

        private void TryAppendMockEdge(int from, int to, int nodeCount, int edgeCapacity, ref int edgeCursor)
        {
            if (to < 0 || to >= nodeCount || edgeCursor >= edgeCapacity)
                return;

            AcousticAup a = BuildAup(from, Seed);
            AcousticAup b = BuildAup(to, Seed);
            float distance = AcousticAup.DistanceMeters(in a, in b);
            if (!math.isfinite(distance) || distance <= 0f)
                distance = 1f;

            AcousticPortalFlags flags = ((from + to + (int)(Seed & 7u)) % 9) == 0
                ? AcousticPortalFlags.SealedBulkhead
                : AcousticPortalFlags.None;
            Edges[edgeCursor++] = new AcousticPortalEdge
            {
                ToNode = to,
                DistanceMeters = distance,
                Flags = flags
            };
        }

        private static int ResolveMaxNodeExpansions(int nodeCount, float qualityWeight)
        {
            float quality = math.saturate(qualityWeight);
            float scaled = math.lerp(2f, nodeCount, quality);
            return math.clamp((int)math.round(scaled), 1, math.max(1, nodeCount));
        }

        private static float ResolveMockRoomVolume(int index, uint seed)
        {
            uint mixed = Mix((uint)index, seed);
            float t = ((mixed >> 8) & 255u) * (1f / 255f);
            return math.lerp(8f, 96f, t);
        }

        private static AcousticAup BuildAup(int index, uint seed)
        {
            uint mixed = Mix((uint)index, seed);
            int gridX = (int)(mixed & 7u) - 3;
            int gridY = (int)((mixed >> 3) & 3u) - 1;
            int gridZ = (int)((mixed >> 5) & 7u) - 3;
            float cell = AcousticPortalConstants.AupCellSizeMeters;
            float3 local = new float3(
                (((mixed >> 8) & 1023u) * (cell / 1024f)) - (cell * 0.5f),
                (((mixed >> 18) & 255u) * (cell / 256f)) - (cell * 0.5f),
                (((mixed >> 24) & 255u) * (cell / 256f)) - (cell * 0.5f));
            return new AcousticAup(gridX, gridY, gridZ, local);
        }

        private static uint Mix(uint value, uint seed)
        {
            uint mixed = value * 747796405u + seed * 2891336453u + 0x9E3779B9u;
            mixed ^= mixed >> 16;
            mixed *= 2246822519u;
            mixed ^= mixed >> 13;
            mixed *= 3266489917u;
            mixed ^= mixed >> 16;
            return mixed;
        }
    }
}
