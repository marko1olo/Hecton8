using System.Runtime.InteropServices;
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
    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true)]
    public struct WfcOutpostGraphTranslationJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Cells;
        public WfcOutpostGridDescriptor Descriptor;
        public NativeArray<WfcOutpostPowerNode> Nodes;
        public NativeArray<int> CellToNode;
        public NativeParallelMultiHashMap<int, int> PowerEdges;
        public NativeArray<int> Counts;
        public NativeArray<int> GeneratorNodeIndex;

        public void Execute()
        {
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
                    break;
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

            if (generatorNode < 0 && firstPowerNode >= 0)
            {
                generatorNode = firstPowerNode;
                WriteFault(WfcOutpostGraphFaultFlags.MissingGenerator);
            }

            int directedEdges = BuildEdges(cellCount, dimensions);
            Counts[WfcOutpostGraphCountSlots.NodeCount] = nodeCount;
            Counts[WfcOutpostGraphCountSlots.DirectedEdgeCount] = directedEdges;
            Counts[WfcOutpostGraphCountSlots.DoorCount] = doorCount;
            Counts[WfcOutpostGraphCountSlots.RoomCount] = roomCount;
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

        private int BuildEdges(int cellCount, int3 dimensions)
        {
            int directedEdges = 0;
            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                int sourceNode = CellToNode[cellIndex];
                if (sourceNode < 0)
                    continue;

                int3 cell = Unflatten(cellIndex, dimensions);
                directedEdges += TryAddEdge(sourceNode, cell.x + 1, cell.y, cell.z, dimensions);
                directedEdges += TryAddEdge(sourceNode, cell.x, cell.y + 1, cell.z, dimensions);
                directedEdges += TryAddEdge(sourceNode, cell.x, cell.y, cell.z + 1, dimensions);
            }

            return directedEdges;
        }

        private int TryAddEdge(int sourceNode, int x, int y, int z, int3 dimensions)
        {
            if (x < 0 || y < 0 || z < 0 || x >= dimensions.x || y >= dimensions.y || z >= dimensions.z)
                return 0;

            int destinationCell = WfcOutpostGridConstants.Flatten(x, y, z, dimensions);
            if ((uint)destinationCell >= (uint)CellToNode.Length)
                return 0;

            int destinationNode = CellToNode[destinationCell];
            if (destinationNode < 0)
                return 0;

            PowerEdges.Add(sourceNode, destinationNode);
            PowerEdges.Add(destinationNode, sourceNode);
            return 2;
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
    }
}
