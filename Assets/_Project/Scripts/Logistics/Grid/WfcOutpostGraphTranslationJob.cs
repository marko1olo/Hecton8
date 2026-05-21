using Hecton8.Logistics.Grid.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Logistics.Grid
{
    /// <summary>
    /// Converts a packed WFC outpost grid into SOA power nodes and logical adjacency edges.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WfcOutpostGraphTranslationJob : IJob
    {
        [ReadOnly]
        [NoAlias] public NativeArray<byte> Cells;
        public WfcOutpostGridDescriptor Descriptor;
        [NoAlias] public NativeArray<WfcOutpostPowerNode> Nodes;
        [NoAlias] public NativeArray<int> CellToNode;
        [NoAlias] public NativeParallelMultiHashMap<int, int> PowerEdges;
        [NoAlias] public NativeArray<int> Counts;
        [NoAlias] public NativeArray<int> GeneratorNodeIndex;

        public void Execute()
        {
            if (!HasValidOutputBuffers())
            {
                WriteFault(WfcOutpostGraphFaultFlags.InvalidBuffers);
                return;
            }

            ClearOutputs();

            int3 dimensions = Descriptor.Dimensions;
            if (dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0 ||
                dimensions.x > WfcOutpostGridConstants.FullWidth ||
                dimensions.y > WfcOutpostGridConstants.FullHeight ||
                dimensions.z > WfcOutpostGridConstants.FullDepth)
            {
                WriteFault(WfcOutpostGraphFaultFlags.InvalidDimensions);
                return;
            }

            int cellCount = math.min(
                math.min(Cells.Length, CellToNode.Length),
                math.min(Descriptor.CellCount, WfcOutpostGridConstants.MaxCellCount));
            int expectedCount = dimensions.x * dimensions.y * dimensions.z;
            cellCount = math.min(cellCount, expectedCount);
            if (cellCount <= 0)
            {
                WriteFault(WfcOutpostGraphFaultFlags.InvalidDimensions);
                return;
            }

            int nodeCount = 0;
            int doorCount = 0;
            int roomCount = 0;
            int firstPowerNode = -1;
            int generatorNode = -1;
            float cellSize = SanitizeMeters(Descriptor.CellSizeMeters, 1f);
            float floorHeight = SanitizeMeters(Descriptor.FloorHeightMeters, 1f);
            float halfWidth = (dimensions.x - 1) * cellSize * 0.5f;
            float halfDepth = (dimensions.z - 1) * cellSize * 0.5f;

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                byte packed = Cells[cellIndex];
                if (!WfcOutpostGridConstants.IsPowerModuleKind(packed))
                    continue;

                if (nodeCount >= Nodes.Length)
                {
                    WriteFault(WfcOutpostGraphFaultFlags.CapacityExceeded);
                    return;
                }

                int3 cell = Unflatten(cellIndex, dimensions);
                byte kind = (byte)(packed & WfcOutpostGridConstants.CellMask);
                ushort doorId = 0;
                if (kind == WfcOutpostGridConstants.SealedDoor)
                    doorId = (ushort)math.min(++doorCount, ushort.MaxValue);

                ushort roomId = ushort.MaxValue;
                if (WfcOutpostGridConstants.IsRoomLikeKind(packed))
                    roomId = (ushort)math.min(roomCount++, ushort.MaxValue);

                Nodes[nodeCount] = new WfcOutpostPowerNode
                {
                    NodeId = ComputeNodeId(Descriptor.GridHash, cellIndex, kind),
                    Cell = cell,
                    LocalOffsetMeters = new float3(
                        cell.x * cellSize - halfWidth,
                        cell.y * floorHeight,
                        cell.z * cellSize - halfDepth),
                    CellIndex = (ushort)cellIndex,
                    RoomId = roomId,
                    DoorId = doorId,
                    Kind = kind,
                    PriorityTier = ResolvePriorityTier(kind),
                    Flags = packed,
                    Reserved = 0
                };

                CellToNode[cellIndex] = nodeCount;
                if (firstPowerNode < 0)
                    firstPowerNode = nodeCount;
                if (kind == WfcOutpostGridConstants.Generator)
                    generatorNode = nodeCount;

                nodeCount++;
            }

            if (nodeCount <= 0)
            {
                WriteFault(WfcOutpostGraphFaultFlags.NoPowerNodes);
                return;
            }

            if (generatorNode < 0 && firstPowerNode >= 0)
            {
                generatorNode = firstPowerNode;
                WriteFault(WfcOutpostGraphFaultFlags.MissingGenerator);
            }

            int directedEdges = BuildEdges(cellCount, dimensions);
            WriteCount(WfcOutpostGraphCountSlots.NodeCount, nodeCount);
            WriteCount(WfcOutpostGraphCountSlots.DirectedEdgeCount, directedEdges);
            WriteCount(WfcOutpostGraphCountSlots.DoorCount, doorCount);
            WriteCount(WfcOutpostGraphCountSlots.RoomCount, roomCount);
            if (GeneratorNodeIndex.IsCreated && GeneratorNodeIndex.Length > 0)
                GeneratorNodeIndex[0] = generatorNode;
        }

        private void ClearOutputs()
        {
            for (int i = 0; i < CellToNode.Length; i++)
                CellToNode[i] = -1;

            for (int i = 0; i < Counts.Length; i++)
                Counts[i] = 0;

            if (GeneratorNodeIndex.IsCreated && GeneratorNodeIndex.Length > 0)
                GeneratorNodeIndex[0] = -1;
        }

        private bool HasValidOutputBuffers()
        {
            return Cells.IsCreated &&
                   Nodes.IsCreated &&
                   CellToNode.IsCreated &&
                   Counts.IsCreated &&
                   PowerEdges.IsCreated &&
                   Counts.Length >= WfcOutpostGraphCountSlots.Count;
        }

        private int BuildEdges(int cellCount, int3 dimensions)
        {
            int directedEdges = 0;
            int edgeCapacity = ResolveEdgeCapacity();
            if (edgeCapacity <= 0)
            {
                WriteFault(WfcOutpostGraphFaultFlags.CapacityExceeded);
                return 0;
            }

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                int sourceNode = CellToNode[cellIndex];
                if (sourceNode < 0)
                    continue;

                int3 cell = Unflatten(cellIndex, dimensions);
                byte sourcePacked = Cells[cellIndex];
                if (!TryAddHorizontalEdge(ref directedEdges, edgeCapacity, sourceNode, sourcePacked, cell.x + 1, cell.y, cell.z, dimensions, WfcOutpostGridConstants.East, WfcOutpostGridConstants.West) ||
                    !TryAddVerticalEdge(ref directedEdges, edgeCapacity, sourceNode, sourcePacked, cell.x, cell.y + 1, cell.z, dimensions) ||
                    !TryAddHorizontalEdge(ref directedEdges, edgeCapacity, sourceNode, sourcePacked, cell.x, cell.y, cell.z + 1, dimensions, WfcOutpostGridConstants.North, WfcOutpostGridConstants.South))
                {
                    return directedEdges;
                }
            }

            return directedEdges;
        }

        private bool TryAddHorizontalEdge(ref int directedEdges, int edgeCapacity, int sourceNode, byte sourcePacked, int x, int y, int z, int3 dimensions, byte sourceExit, byte destinationExit)
        {
            if (x < 0 || y < 0 || z < 0 || x >= dimensions.x || y >= dimensions.y || z >= dimensions.z)
                return true;

            int destinationCell = WfcOutpostGridConstants.Flatten(x, y, z, dimensions);
            if ((uint)destinationCell >= (uint)CellToNode.Length)
                return true;

            int destinationNode = CellToNode[destinationCell];
            if (destinationNode < 0)
                return true;

            byte destinationPacked = Cells[destinationCell];
            if ((sourcePacked & sourceExit) == 0 || (destinationPacked & destinationExit) == 0)
                return true;

            return TryAddBidirectionalEdge(ref directedEdges, edgeCapacity, sourceNode, destinationNode);
        }

        private bool TryAddVerticalEdge(ref int directedEdges, int edgeCapacity, int sourceNode, byte sourcePacked, int x, int y, int z, int3 dimensions)
        {
            if (x < 0 || y < 0 || z < 0 || x >= dimensions.x || y >= dimensions.y || z >= dimensions.z)
                return true;

            int destinationCell = WfcOutpostGridConstants.Flatten(x, y, z, dimensions);
            if ((uint)destinationCell >= (uint)CellToNode.Length)
                return true;

            int destinationNode = CellToNode[destinationCell];
            if (destinationNode < 0)
                return true;

            if (!IsVerticalBridge(sourcePacked, Cells[destinationCell]))
                return true;

            return TryAddBidirectionalEdge(ref directedEdges, edgeCapacity, sourceNode, destinationNode);
        }

        private bool TryAddBidirectionalEdge(ref int directedEdges, int edgeCapacity, int sourceNode, int destinationNode)
        {
            if (directedEdges + 2 > edgeCapacity)
            {
                WriteFault(WfcOutpostGraphFaultFlags.CapacityExceeded);
                return false;
            }

            PowerEdges.Add(sourceNode, destinationNode);
            PowerEdges.Add(destinationNode, sourceNode);
            directedEdges += 2;
            return true;
        }

        private int ResolveEdgeCapacity()
        {
            return PowerEdges.IsCreated
                ? math.min(PowerEdges.Capacity, WfcOutpostGridConstants.MaxDirectedEdges)
                : 0;
        }

        private static bool IsVerticalBridge(byte sourcePacked, byte destinationPacked)
        {
            byte sourceKind = (byte)(sourcePacked & WfcOutpostGridConstants.CellMask);
            byte destinationKind = (byte)(destinationPacked & WfcOutpostGridConstants.CellMask);
            return sourceKind == WfcOutpostGridConstants.Hatch ||
                   destinationKind == WfcOutpostGridConstants.Hatch;
        }

        private static int3 Unflatten(int index, int3 dimensions)
        {
            int layerSize = dimensions.x * dimensions.z;
            int y = index / layerSize;
            int remainder = index - y * layerSize;
            int z = remainder / dimensions.x;
            int x = remainder - z * dimensions.x;
            return new int3(x, y, z);
        }

        private static uint ComputeNodeId(uint gridHash, int cellIndex, byte kind)
        {
            uint value = gridHash ^ ((uint)cellIndex * 0x9E3779B9u) ^ ((uint)kind << 24);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        private static float SanitizeMeters(float value, float fallback)
        {
            if (!math.isfinite(value) || value < 1f)
                return fallback;
            return value;
        }

        private static byte ResolvePriorityTier(byte kind)
        {
            if (kind == WfcOutpostGridConstants.Generator)
                return 0;
            if (kind == WfcOutpostGridConstants.SealedDoor || kind == WfcOutpostGridConstants.Hatch)
                return 1;
            if (kind == WfcOutpostGridConstants.Room || kind == WfcOutpostGridConstants.Datapad)
                return 2;
            return 3;
        }

        private void WriteFault(int faultFlag)
        {
            if (Counts.IsCreated && Counts.Length > WfcOutpostGraphCountSlots.FaultFlags)
                Counts[WfcOutpostGraphCountSlots.FaultFlags] = Counts[WfcOutpostGraphCountSlots.FaultFlags] | faultFlag;
        }

        private void WriteCount(int slot, int value)
        {
            if (Counts.IsCreated && (uint)slot < (uint)Counts.Length)
                Counts[slot] = value;
        }
    }
}
