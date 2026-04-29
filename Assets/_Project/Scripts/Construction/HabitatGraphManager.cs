using System;
using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Power;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Rebuilds the placed habitat into a CSR adjacency graph for downstream power and atmosphere solvers.
    /// Owns only base-module topology. Point-to-point crate pipes remain under LogisticsPipeNode.
    /// </summary>
    internal sealed class HabitatGraphManager : IDisposable
    {
        private const float DefaultSocketQuantization = 0.05f;
        private const float OppositeDirectionDotThreshold = -0.85f;
        private const float EdgeResistancePerMeter = 0.05f;
        private const float MinimumEdgeResistance = 0.1f;
        private const float SupportCaptureRadiusMeters = 3f;
        private const float SupportCaptureRadiusSq = SupportCaptureRadiusMeters * SupportCaptureRadiusMeters;
        private const int InitialSocketCapacity = 32;
        private const int InitialNodeCapacity = 64;
        private const int InitialEdgeCapacity = 128;
        private static readonly Color PipeSplineColor = new Color(0.30f, 0.82f, 0.95f, 0.88f);

        private readonly List<ModuleSocket> _socketBuffer;
        private readonly List<ModuleRecord> _moduleBuffer;
        private readonly List<EdgeRecord> _edgeBuffer;
        private readonly List<long> _submittedLinkIds;
        private readonly Dictionary<SocketKey, SocketMatchEntry> _socketLookup;

        private NativeArray<LogisticsNetworkGraph.LogisticsNode> _nodes;
        private NativeArray<int> _edgeOffsets;
        private NativeArray<int> _edgeDestinations;
        private NativeArray<float> _edgeResistance;
        private NativeArray<int> _edgeWriteCursor;

        private readonly LogisticsNetworkGraph _graph;
        private int _nodeCount;
        private int _edgeCount;

        internal HabitatGraphManager(int initialModuleCapacity)
        {
            int safeModuleCapacity = math.max(1, initialModuleCapacity);
            // COLD ALLOC: List<ModuleSocket>[32] — reusable module socket scan buffer for base graph rebuilds — owner: HabitatGraphManager
            _socketBuffer = new List<ModuleSocket>(InitialSocketCapacity);
            // COLD ALLOC: List<ModuleRecord>[64] — reusable module staging buffer for CSR rebuilds — owner: HabitatGraphManager
            _moduleBuffer = new List<ModuleRecord>(safeModuleCapacity);
            // COLD ALLOC: List<EdgeRecord>[128] — reusable undirected base-link staging buffer for CSR rebuilds — owner: HabitatGraphManager
            _edgeBuffer = new List<EdgeRecord>(InitialEdgeCapacity);
            // COLD ALLOC: List<Int64>[128] — submitted visual spline link ids for removal during rebuild — owner: HabitatGraphManager
            _submittedLinkIds = new List<long>(InitialEdgeCapacity);
            // COLD ALLOC: Dictionary<SocketKey,SocketMatchEntry>[128] — quantized socket lookup for zero-GC adjacency assembly — owner: HabitatGraphManager
            _socketLookup = new Dictionary<SocketKey, SocketMatchEntry>(InitialEdgeCapacity);

            _graph = new LogisticsNetworkGraph(safeModuleCapacity, InitialEdgeCapacity * 2, 0);
            AllocateNativeBuffers(safeModuleCapacity, InitialEdgeCapacity * 2);
        }

        internal int NodeCount => _nodeCount;
        internal int EdgeCount => _edgeCount;
        internal NativeArray<LogisticsNetworkGraph.LogisticsNode> Nodes => _nodes;
        internal NativeArray<int> EdgeOffsets => _edgeOffsets;
        internal NativeArray<int> EdgeDestinations => _edgeDestinations;
        internal NativeArray<float> EdgeResistance => _edgeResistance;
        internal LogisticsNetworkGraph Graph => _graph;

        public void Dispose()
        {
            ClearVisualLinks();
            DisposeNativeBuffers();
            _graph.Dispose();
        }

        internal void Rebuild(IReadOnlyList<GameObject> modules)
        {
            ClearVisualLinks();
            _moduleBuffer.Clear();
            _edgeBuffer.Clear();
            _socketLookup.Clear();
            _nodeCount = 0;
            _edgeCount = 0;

            if (modules == null || modules.Count <= 0)
            {
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                return;
            }

            PopulateModuleBuffer(modules);
            _nodeCount = _moduleBuffer.Count;
            if (_nodeCount <= 0)
            {
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                return;
            }

            EnsureNodeCapacity(_nodeCount);
            BuildSocketAdjacency();
            BuildNodeRecords();
            BuildEdgeRecords();
            PublishGraphKernel();
            PublishVisualLinks();
        }

        private void PopulateModuleBuffer(IReadOnlyList<GameObject> modules)
        {
            int count = modules.Count;
            if (_moduleBuffer.Capacity < count)
                _moduleBuffer.Capacity = count;

            for (int i = 0; i < count; i++)
            {
                GameObject moduleObject = modules[i];
                if (moduleObject == null)
                    continue;

                ModuleMarker marker = moduleObject.TryGetComponent(out ModuleMarker resolvedMarker) ? resolvedMarker : null;
                BaseModule baseModule = moduleObject.TryGetComponent(out BaseModule resolvedBaseModule) ? resolvedBaseModule : null;

                _moduleBuffer.Add(new ModuleRecord
                {
                    ModuleObject = moduleObject,
                    Marker = marker,
                    BaseModule = baseModule,
                    Position = moduleObject.transform.position,
                    NodeId = unchecked((uint)EntityId.ToULong(moduleObject.GetEntityId()))
                });
            }
        }

        private void BuildSocketAdjacency()
        {
            int quantizationScale = math.max(1, (int)math.round(1f / DefaultSocketQuantization));
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
                IndexSockets(moduleIndex, _moduleBuffer[moduleIndex].ModuleObject, quantizationScale);
        }

        private void IndexSockets(int moduleIndex, GameObject root, int quantizationScale)
        {
            if (root == null)
                return;

            _socketBuffer.Clear();
            root.GetComponentsInChildren(true, _socketBuffer);

            for (int i = 0; i < _socketBuffer.Count; i++)
            {
                ModuleSocket socket = _socketBuffer[i];
                if (socket == null)
                    continue;

                Transform socketTransform = socket.transform;
                int axis = QuantizeAxis(socketTransform.forward);
                SocketKey oppositeKey = SocketKey.Create(socketTransform.position, OppositeAxis(axis), quantizationScale);

                if (_socketLookup.TryGetValue(oppositeKey, out SocketMatchEntry existing))
                {
                    if (existing.ModuleIndex != moduleIndex &&
                        AreSocketsCompatible(existing.CompatibleType, socket.CompatibleType) &&
                        Vector3.Dot(existing.Forward, socketTransform.forward) <= OppositeDirectionDotThreshold)
                    {
                        _edgeBuffer.Add(new EdgeRecord
                        {
                            SourceIndex = existing.ModuleIndex,
                            DestinationIndex = moduleIndex,
                            StartSocketPosition = existing.Position,
                            EndSocketPosition = socketTransform.position,
                            StartForward = existing.Forward,
                            EndForward = socketTransform.forward,
                            Flags = PipeRenderFlags.None
                        });
                    }

                    continue;
                }

                SocketKey ownKey = SocketKey.Create(socketTransform.position, axis, quantizationScale);
                _socketLookup[ownKey] = new SocketMatchEntry(moduleIndex, socket.CompatibleType, socketTransform.position, socketTransform.forward);
            }
        }

        private void BuildNodeRecords()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                _nodes[nodeIndex] = new LogisticsNetworkGraph.LogisticsNode
                {
                    Id = module.NodeId,
                    Capacity = ResolveNodeCapacity(module.Marker, module.BaseModule),
                    Resistance = ResolveNodeResistance(module.BaseModule),
                    CurrentLoad = 0f,
                    Potential = 0f,
                    Priority = ResolveNodePriority(module.Marker),
                    Flags = ResolveNodeFlags(module.BaseModule),
                    NetworkId = 0,
                    Reserved = (byte)ResolveReservedState(module.BaseModule)
                };
            }
        }

        private void BuildEdgeRecords()
        {
            int directedEdgeCount = _edgeBuffer.Count * 2;
            EnsureEdgeCapacity(directedEdgeCount);

            for (int nodeIndex = 0; nodeIndex <= _nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = 0;

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                float distance = math.distance(edge.StartSocketPosition, edge.EndSocketPosition);
                bool unsupported = distance > LogisticsPipeBuilder.UnsupportedSpanMeters &&
                                   !HasIntermediateSupport(edge.SourceIndex, edge.DestinationIndex, edge.StartSocketPosition, edge.EndSocketPosition);

                if (unsupported)
                {
                    edge.Flags |= PipeRenderFlags.MaskRuptured;
                    LogisticsNetworkGraph.LogisticsNode sourceNode = _nodes[edge.SourceIndex];
                    sourceNode.Flags |= LogisticsNodeFlags.Ruptured;
                    _nodes[edge.SourceIndex] = sourceNode;

                    LogisticsNetworkGraph.LogisticsNode destinationNode = _nodes[edge.DestinationIndex];
                    destinationNode.Flags |= LogisticsNodeFlags.Ruptured;
                    _nodes[edge.DestinationIndex] = destinationNode;
                }

                edge.Resistance = math.max(MinimumEdgeResistance, distance * EdgeResistancePerMeter);
                _edgeBuffer[edgeIndex] = edge;

                _edgeOffsets[edge.SourceIndex + 1] = _edgeOffsets[edge.SourceIndex + 1] + 1;
                _edgeOffsets[edge.DestinationIndex + 1] = _edgeOffsets[edge.DestinationIndex + 1] + 1;
            }

            for (int nodeIndex = 1; nodeIndex <= _nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = _edgeOffsets[nodeIndex] + _edgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _edgeWriteCursor[nodeIndex] = _edgeOffsets[nodeIndex];

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];

                int forwardWriteIndex = _edgeWriteCursor[edge.SourceIndex];
                _edgeWriteCursor[edge.SourceIndex] = forwardWriteIndex + 1;
                _edgeDestinations[forwardWriteIndex] = edge.DestinationIndex;
                _edgeResistance[forwardWriteIndex] = edge.Resistance;

                int reverseWriteIndex = _edgeWriteCursor[edge.DestinationIndex];
                _edgeWriteCursor[edge.DestinationIndex] = reverseWriteIndex + 1;
                _edgeDestinations[reverseWriteIndex] = edge.SourceIndex;
                _edgeResistance[reverseWriteIndex] = edge.Resistance;
            }

            _edgeCount = directedEdgeCount;
        }

        private void PublishGraphKernel()
        {
            _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, _nodeCount, math.max(1, _edgeCount), 0);

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                _graph.AddNode(node.Id, node.Capacity, node.Resistance, node.Priority, node.Flags, node.Reserved);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                _graph.AddEdge(edge.SourceIndex, edge.DestinationIndex, edge.Resistance);
                _graph.AddEdge(edge.DestinationIndex, edge.SourceIndex, edge.Resistance);
            }

            _graph.FinalizeBuild();
        }

        private void PublishVisualLinks()
        {
            int edgeCount = _edgeBuffer.Count;
            if (_submittedLinkIds.Capacity < edgeCount)
                _submittedLinkIds.Capacity = edgeCount;

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                long linkId = ComposeLinkId(_moduleBuffer[edge.SourceIndex].NodeId, _moduleBuffer[edge.DestinationIndex].NodeId);
                SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                    edge.StartSocketPosition,
                    edge.EndSocketPosition,
                    edge.StartForward,
                    edge.EndForward,
                    LogisticsPipeBuilder.DefaultPipeRadiusMeters,
                    edge.Flags);

                ConnectionSplineBatchRenderer.SubmitPipeLink(linkId, descriptor, PipeSplineColor);
                _submittedLinkIds.Add(linkId);
            }
        }

        private void ClearVisualLinks()
        {
            for (int i = 0; i < _submittedLinkIds.Count; i++)
                ConnectionSplineBatchRenderer.RemovePipeLink(_submittedLinkIds[i]);

            _submittedLinkIds.Clear();
        }

        private bool HasIntermediateSupport(int sourceIndex, int destinationIndex, float3 start, float3 end)
        {
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                if (moduleIndex == sourceIndex || moduleIndex == destinationIndex)
                    continue;

                float projection;
                float distanceSq = DistancePointToSegmentSq(_moduleBuffer[moduleIndex].Position, start, end, out projection);
                if (projection > 0.1f &&
                    projection < 0.9f &&
                    distanceSq <= SupportCaptureRadiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistancePointToSegmentSq(float3 point, float3 start, float3 end, out float projection)
        {
            float3 segment = end - start;
            float segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.000001f)
            {
                projection = 0f;
                return math.lengthsq(point - start);
            }

            projection = math.saturate(math.dot(point - start, segment) / segmentLengthSq);
            float3 closestPoint = start + segment * projection;
            return math.lengthsq(point - closestPoint);
        }

        private static float ResolveNodeCapacity(ModuleMarker marker, BaseModule baseModule)
        {
            float capacity = 8f;
            if (marker != null && marker.Data != null)
                capacity += math.abs(marker.Data.powerRating) * 0.01f;

            if (baseModule != null)
                capacity += math.max(0f, baseModule.MaxIntegrity * 0.05f);

            return math.max(1f, capacity);
        }

        private static float ResolveNodeResistance(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0.25f;

            float resistance = 0.15f;
            if (baseModule.IsFlooded)
                resistance += 0.15f;

            if (!baseModule.HasPower)
                resistance += 0.1f;

            return resistance;
        }

        private static byte ResolveNodePriority(ModuleMarker marker)
        {
            if (marker == null || marker.Data == null)
                return 48;

            switch (marker.Data.family)
            {
                case BuildableFamily.Habitat:
                    return 12;

                case BuildableFamily.Utility:
                    return 24;

                case BuildableFamily.Logistics:
                    return 36;

                default:
                    return 48;
            }
        }

        private static LogisticsNodeFlags ResolveNodeFlags(BaseModule baseModule)
        {
            LogisticsNodeFlags flags = LogisticsNodeFlags.Active;
            if (baseModule != null && baseModule.HasCascadeFailure)
                flags |= LogisticsNodeFlags.Dirty;

            return flags;
        }

        private static LogisticsModuleStatusBits ResolveReservedState(BaseModule baseModule)
        {
            if (baseModule == null)
                return LogisticsModuleStatusBits.None;

            LogisticsModuleStatusBits bits = LogisticsModuleStatusBits.None;
            if (baseModule.HasPower)
                bits |= LogisticsModuleStatusBits.Powered;
            if (baseModule.IsFlooded)
                bits |= LogisticsModuleStatusBits.Flooded;
            if (baseModule.CurrentIntegrity < baseModule.MaxIntegrity)
                bits |= LogisticsModuleStatusBits.Damaged;

            return bits;
        }

        private static bool AreSocketsCompatible(string lhs, string rhs)
        {
            if (string.IsNullOrEmpty(lhs) || string.IsNullOrEmpty(rhs))
                return true;

            return string.Equals(lhs, rhs, StringComparison.OrdinalIgnoreCase);
        }

        private static int QuantizeAxis(Vector3 direction)
        {
            float3 normalized = math.normalizesafe((float3)direction, new float3(0f, 0f, 1f));
            float absX = math.abs(normalized.x);
            float absY = math.abs(normalized.y);
            float absZ = math.abs(normalized.z);

            if (absX >= absY && absX >= absZ)
                return normalized.x >= 0f ? 0 : 1;

            if (absY >= absX && absY >= absZ)
                return normalized.y >= 0f ? 2 : 3;

            return normalized.z >= 0f ? 4 : 5;
        }

        private static int OppositeAxis(int axis)
        {
            switch (axis)
            {
                case 0: return 1;
                case 1: return 0;
                case 2: return 3;
                case 3: return 2;
                case 4: return 5;
                default: return 4;
            }
        }

        private static long ComposeLinkId(uint left, uint right)
        {
            uint min = math.min(left, right);
            uint max = math.max(left, right);
            return ((long)min << 32) | max;
        }

        private void AllocateNativeBuffers(int nodeCapacity, int edgeCapacity)
        {
            // COLD ALLOC: NativeArray<LogisticsNode>[64] — habitat node snapshot buffer — owner: HabitatGraphManager
            _nodes = new NativeArray<LogisticsNetworkGraph.LogisticsNode>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[65] — habitat CSR edge-offset buffer — owner: HabitatGraphManager
            _edgeOffsets = new NativeArray<int>(nodeCapacity + 1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[128] — habitat CSR destination buffer — owner: HabitatGraphManager
            _edgeDestinations = new NativeArray<int>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[128] — habitat CSR edge-resistance buffer — owner: HabitatGraphManager
            _edgeResistance = new NativeArray<float>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[64] — CSR write-cursor scratch buffer — owner: HabitatGraphManager
            _edgeWriteCursor = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void EnsureNodeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_nodes.IsCreated && _nodes.Length >= safeLength && _edgeOffsets.Length >= safeLength + 1 && _edgeWriteCursor.Length >= safeLength)
                return;

            DisposeNativeBuffers();
            int nodeCapacity = NextPowerOfTwo(math.max(safeLength, InitialNodeCapacity));
            int edgeCapacity = NextPowerOfTwo(math.max(nodeCapacity * 4, InitialEdgeCapacity));
            AllocateNativeBuffers(nodeCapacity, edgeCapacity);
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_edgeDestinations.IsCreated && _edgeDestinations.Length >= safeLength && _edgeResistance.Length >= safeLength)
                return;

            int nodeCapacity = math.max(1, _nodes.Length);
            DisposeNativeBuffers();
            AllocateNativeBuffers(nodeCapacity, NextPowerOfTwo(math.max(safeLength, InitialEdgeCapacity)));
        }

        private void DisposeNativeBuffers()
        {
            if (_nodes.IsCreated)
                _nodes.Dispose();

            if (_edgeOffsets.IsCreated)
                _edgeOffsets.Dispose();

            if (_edgeDestinations.IsCreated)
                _edgeDestinations.Dispose();

            if (_edgeResistance.IsCreated)
                _edgeResistance.Dispose();

            if (_edgeWriteCursor.IsCreated)
                _edgeWriteCursor.Dispose();
        }

        private static int NextPowerOfTwo(int value)
        {
            if (value <= 1)
                return 1;

            int power = 1;
            while (power < value && power > 0)
                power <<= 1;

            return power > 0 ? power : int.MaxValue;
        }

        private struct ModuleRecord
        {
            public GameObject ModuleObject;
            public ModuleMarker Marker;
            public BaseModule BaseModule;
            public float3 Position;
            public uint NodeId;
        }

        private struct EdgeRecord
        {
            public int SourceIndex;
            public int DestinationIndex;
            public float3 StartSocketPosition;
            public float3 EndSocketPosition;
            public float3 StartForward;
            public float3 EndForward;
            public float Resistance;
            public PipeRenderFlags Flags;
        }

        private readonly struct SocketKey : IEquatable<SocketKey>
        {
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;
            private readonly int _axis;

            private SocketKey(int x, int y, int z, int axis)
            {
                _x = x;
                _y = y;
                _z = z;
                _axis = axis;
            }

            public static SocketKey Create(Vector3 position, int axis, int quantizationScale)
            {
                float scale = quantizationScale > 0 ? quantizationScale : 1f;
                float3 scaledPosition = (float3)position * scale;
                int3 quantizedPosition = (int3)math.round(scaledPosition);
                return new SocketKey(quantizedPosition.x, quantizedPosition.y, quantizedPosition.z, axis);
            }

            public bool Equals(SocketKey other)
            {
                return _x == other._x &&
                       _y == other._y &&
                       _z == other._z &&
                       _axis == other._axis;
            }

            public override bool Equals(object obj)
            {
                return obj is SocketKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _x;
                    hash = (hash * 397) ^ _y;
                    hash = (hash * 397) ^ _z;
                    hash = (hash * 397) ^ _axis;
                    return hash;
                }
            }
        }

        private readonly struct SocketMatchEntry
        {
            public readonly int ModuleIndex;
            public readonly string CompatibleType;
            public readonly float3 Position;
            public readonly float3 Forward;

            public SocketMatchEntry(int moduleIndex, string compatibleType, float3 position, float3 forward)
            {
                ModuleIndex = moduleIndex;
                CompatibleType = compatibleType;
                Position = position;
                Forward = forward;
            }
        }
    }
}
