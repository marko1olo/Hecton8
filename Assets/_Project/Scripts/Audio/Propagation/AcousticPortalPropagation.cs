using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Propagation
{
    public static class AcousticPortalConstants
    {
        public const int MaxPathNodes = 30;
        public const int MaxPathEdges = MaxPathNodes * 2;
        public const int TelemetryFrameCount = 300;
        public const float SoundSpeedWaterMetersPerSecond = 1480f;
        public const float OpenLowPassCutoffHertz = 22000f;
        public const float MinimumLowPassCutoffHertz = 80f;
        public const float CornerLowPassHertz = 2000f;
        public const float SealedBulkheadLowPassHertz = 400f;
        public const float SealedBulkheadDelaySeconds = 0.010f;
        public const float CornerGain = 0.70794576f;
        public const float MaximumItdSeconds = 0.00065f;
        public const int AupCellSizeMeters = 5000;
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
        LowTierFallback = 1,
        NoGraph = 2,
        NoPath = 3,
        PathFound = 4,
        InvalidInput = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AcousticPortalNode
    {
        public AcousticAup Position;
        public int FirstEdge;
        public int EdgeCount;
        public float RoomVolumeCubicMeters;
        public AcousticPortalFlags Flags;
        private byte _reserved0;
        private ushort _reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AcousticPortalEdge
    {
        public int ToNode;
        public float DistanceMeters;
        public AcousticPortalFlags Flags;
        private byte _reserved0;
        private ushort _reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AcousticPathQuery
    {
        public AcousticAup SourceAup;
        public AcousticAup ListenerAup;
        public float3 ListenerRight;
        public int NodeCount;
        public int EdgeCount;
        public int MaxNodeExpansions;
        public byte QualityTier;
        public byte DisablePortalPath;
        private ushort _reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SoundEmissionSignal
    {
        public readonly uint EventID;
        public readonly AcousticAup SourceAup;
        public readonly float Volume;
        public readonly float Pitch;
        public readonly int StationaryCacheKey;
        public readonly AcousticPortalFlags Flags;

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
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AcousticPathResult
    {
        public AcousticPathStatus Status;
        public byte UsedPortalPath;
        public byte UsedSealedBulkhead;
        public byte UsedReprojectionCache;
        public int NodeCount;
        public int CornerCount;
        public int ExpandedNodeCount;
        public int SourceNodeIndex;
        public int ListenerNodeIndex;
        public float TrueDistanceMeters;
        public float DelaySeconds;
        public float Transmission01;
        public float LowPassCutoffHz;
        public float ItdSeconds;
        public float RoomVolumeCubicMeters;
        public float PathfindingMs;
        public AcousticAup LastPortalAup;
        public uint StateHash;

        public static AcousticPathResult Fallback(AcousticPathStatus status, in AcousticPathQuery query)
        {
            float distance = AcousticAup.DistanceMeters(in query.SourceAup, in query.ListenerAup);
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
                DelaySeconds = distance * math.rcp(AcousticPortalConstants.SoundSpeedWaterMetersPerSecond),
                Transmission01 = 1f,
                LowPassCutoffHz = AcousticPortalConstants.OpenLowPassCutoffHertz,
                ItdSeconds = 0f,
                RoomVolumeCubicMeters = 0f,
                PathfindingMs = 0f,
                LastPortalAup = query.SourceAup,
                StateHash = 0u
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AcousticTelemetryEntry
    {
        public int Frame;
        public int NodeCount;
        public int CornerCount;
        public int ExpandedNodeCount;
        public float PathfindingMs;
        public float TrueDistanceMeters;
        public float DelaySeconds;
        public float LowPassCutoffHz;
        public uint Flags;
        public uint StateHash;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct AcousticPathJob : IJob
    {
        [ReadOnly] public NativeArray<AcousticPortalNode> Nodes;
        [ReadOnly] public NativeArray<AcousticPortalEdge> Edges;
        public NativeList<int> OpenSet;
        public NativeList<int> ClosedSet;
        public NativeArray<float> Costs;
        public NativeArray<int> CameFrom;
        public NativeArray<byte> States;
        public NativeArray<AcousticPathResult> Result;
        public AcousticPathQuery Query;

        public void Execute()
        {
            AcousticPathResult fallback = AcousticPathResult.Fallback(
                Query.DisablePortalPath != 0 || Query.QualityTier <= 2
                    ? AcousticPathStatus.LowTierFallback
                    : AcousticPathStatus.NoGraph,
                in Query);

            if (!Result.IsCreated || Result.Length <= 0)
                return;

            Result[0] = fallback;

            if (Query.DisablePortalPath != 0 || Query.QualityTier <= 2)
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
                OpenSet.Capacity <= 0 ||
                ClosedSet.Capacity <= 0)
            {
                Result[0] = fallback;
                return;
            }

            int nodeCount = math.min(
                AcousticPortalConstants.MaxPathNodes,
                math.min(Query.NodeCount, math.min(Nodes.Length, math.min(Costs.Length, math.min(CameFrom.Length, States.Length)))));
            int edgeCount = math.min(AcousticPortalConstants.MaxPathEdges, math.min(Query.EdgeCount, Edges.Length));
            int maxExpansions = math.clamp(
                Query.MaxNodeExpansions <= 0 ? AcousticPortalConstants.MaxPathNodes : Query.MaxNodeExpansions,
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

            OpenSet.Clear();
            ClosedSet.Clear();
            Costs[sourceNode] = 0f;
            States[sourceNode] = 1;
            OpenSet.AddNoResize(sourceNode);

            int expanded = 0;
            bool found = false;
            while (OpenSet.Length > 0 && expanded < maxExpansions)
            {
                int openIndex = FindLowestCostOpenIndex();
                int current = OpenSet[openIndex];
                OpenSet.RemoveAtSwapBack(openIndex);

                if ((uint)current >= (uint)nodeCount || States[current] == 2)
                    continue;

                States[current] = 2;
                if (ClosedSet.Length < ClosedSet.Capacity)
                    ClosedSet.AddNoResize(current);
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
                    if (States[next] == 0 && OpenSet.Length < OpenSet.Capacity)
                    {
                        States[next] = 1;
                        OpenSet.AddNoResize(next);
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

        private int FindLowestCostOpenIndex()
        {
            int bestOpenIndex = 0;
            float bestCost = float.PositiveInfinity;
            for (int i = 0; i < OpenSet.Length; i++)
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

            while (pathNode != sourceNode && pathNodeCount <= AcousticPortalConstants.MaxPathNodes)
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
}
