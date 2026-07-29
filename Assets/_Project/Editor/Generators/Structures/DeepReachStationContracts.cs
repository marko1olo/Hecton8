#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.Structures
{
    public static class DeepReachStationConstants
    {
        public const int DirectionCount = 6;
        public const int EmptyModuleId = 0;
        public const int MaxModuleRules = 16;
        public const int MaxMaterialSlots = 16;
        public const int MaxDamageSpheres = 5;
        public const int WfcCellStrideBytes = 16;
        public const int SocketStrideBytes = 48;
        public const int ModuleRuleStrideBytes = 64;
        public const int PlacementStrideBytes = 96;
        public const int MeshSliceStrideBytes = 32;
        public const int TriangleStrideBytes = 32;
        public const int MeshVertexStrideBytes = 48;
        public const int RenderVertexStrideBytes = 40;
        public const int WeldBucketStrideBytes = 48;
        public const int BakeCounterStrideBytes = 64;
        public const float Epsilon = 0.0001f;
        public const float WeldNormalDotMin = 0.65f;
        public const float WeldUvDistanceSqMax = 0.0625f;
        public const uint GenericConnectorMask = 1u;
        public const uint FaultNoRules = 1u << 0;
        public const uint FaultContradiction = 1u << 1;
        public const uint FaultCapacity = 1u << 2;
        public const uint FaultNonFinite = 1u << 3;
        public const uint FaultInvalidTopology = 1u << 4;
        public const uint CellInsideFlag = 1u;
        public const int CellRotationShift = 8;
        public const uint CellRotationMask = 3u << CellRotationShift;
    }

    public static class DeepReachStationDirections
    {
        public const int North = 0;
        public const int East = 1;
        public const int South = 2;
        public const int West = 3;
        public const int Top = 4;
        public const int Bottom = 5;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.SocketStrideBytes)]
    public struct StationSocketDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(16)] public quaternion LocalRotation;
        [FieldOffset(32)] public uint ConnectorMask;
        [FieldOffset(36)] public uint StableHash;
        [FieldOffset(40)] public ushort ModuleId;
        [FieldOffset(42)] public byte Direction;
        [FieldOffset(43)] public byte Flags;
        [FieldOffset(44)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.ModuleRuleStrideBytes)]
    public struct StationModuleRuleDTO
    {
        [FieldOffset(0)] public uint ModuleHash;
        [FieldOffset(4)] public ushort SocketNorth;
        [FieldOffset(6)] public ushort SocketEast;
        [FieldOffset(8)] public ushort SocketSouth;
        [FieldOffset(10)] public ushort SocketWest;
        [FieldOffset(12)] public ushort SocketTop;
        [FieldOffset(14)] public ushort SocketBottom;
        [FieldOffset(16)] public float3 BoundsExtents;
        [FieldOffset(28)] public float Weight;
        [FieldOffset(32)] public uint PrefabHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public byte ModuleId;
        [FieldOffset(41)] public byte DrawPriority;
        [FieldOffset(42)] public ushort SourceSocketCount;
        [FieldOffset(44)] public uint SourceVertexCount;
        [FieldOffset(48)] public uint SourceTriangleCount;
        [FieldOffset(52)] public uint SourceSocketStart;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.WfcCellStrideBytes)]
    public struct StationWfcCellDTO
    {
        [FieldOffset(0)] public ushort PossibleModuleMask;
        [FieldOffset(2)] public byte CollapsedModuleId;
        [FieldOffset(3)] public byte SocketConstraints;
        [FieldOffset(4)] public float Entropy;
        [FieldOffset(8)] public uint ParentIndex;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.PlacementStrideBytes)]
    public struct StationPlacementDTO
    {
        [FieldOffset(0)] public float4x4 LocalToStation;
        [FieldOffset(64)] public int3 GridCoord;
        [FieldOffset(76)] public uint StableHash;
        [FieldOffset(80)] public ushort ConnectedDirectionMask;
        [FieldOffset(82)] public byte ModuleId;
        [FieldOffset(83)] public byte RotationQuarterTurns;
        [FieldOffset(84)] public uint Flags;
        [FieldOffset(88)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.MeshSliceStrideBytes)]
    public struct StationMeshSliceDTO
    {
        [FieldOffset(0)] public int VertexStart;
        [FieldOffset(4)] public int VertexCount;
        [FieldOffset(8)] public int TriangleStart;
        [FieldOffset(12)] public int TriangleCount;
        [FieldOffset(16)] public uint MaterialHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.TriangleStrideBytes)]
    public struct StationTriangleDTO
    {
        [FieldOffset(0)] public int Index0;
        [FieldOffset(4)] public int Index1;
        [FieldOffset(8)] public int Index2;
        [FieldOffset(12)] public ushort CullDirectionMask;
        [FieldOffset(14)] public ushort SubMesh;
        [FieldOffset(16)] public uint SourceHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.MeshVertexStrideBytes)]
    public struct StationMeshVertexDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float2 Uv0;
        [FieldOffset(32)] public uint ColorRgba;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.RenderVertexStrideBytes)]
    public struct StationRenderVertexDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float2 Uv0;
        [FieldOffset(32)] public uint ColorRgba;
        [FieldOffset(36)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.WeldBucketStrideBytes)]
    public struct StationWeldBucketDTO
    {
        [FieldOffset(0)] public uint Key;
        [FieldOffset(4)] public int VertexIndex;
        [FieldOffset(8)] public int3 QuantizedCoord;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float3 Position;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = DeepReachStationConstants.BakeCounterStrideBytes)]
    public struct StationBakeCountersDTO
    {
        [FieldOffset(0)] public uint PlacementCount;
        [FieldOffset(4)] public uint FaultFlags;
        [FieldOffset(8)] public uint StateHash;
        [FieldOffset(12)] public uint SourceVertexCount;
        [FieldOffset(16)] public uint SourceIndexCount;
        [FieldOffset(20)] public uint CulledTriangleCount;
        [FieldOffset(24)] public uint WeldedVertexCount;
        [FieldOffset(28)] public uint WeldedIndexCount;
        [FieldOffset(32)] public uint MergedVertexCount;
        [FieldOffset(36)] public uint DamageVertexCount;
        [FieldOffset(40)] public float WfcMilliseconds;
        [FieldOffset(44)] public float FusionMilliseconds;
        [FieldOffset(48)] public float WeldMilliseconds;
        [FieldOffset(52)] public float DamageMilliseconds;
        [FieldOffset(56)] public ulong _pad0;
    }

    public static class DeepReachStationMath
    {
        public static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        public static uint Hash(int3 value, uint seed)
        {
            uint hash = seed == 0u ? 2166136261u : seed;
            hash = (hash ^ (uint)value.x) * 16777619u;
            hash = (hash ^ (uint)value.y) * 16777619u;
            hash = (hash ^ (uint)value.z) * 16777619u;
            return Hash(hash);
        }

        public static uint HashAsciiLower(byte value, uint hash)
        {
            byte c = value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
            if (c == (byte)' ' || c == (byte)'\t')
                return hash;

            hash ^= c;
            hash *= 16777619u;
            return hash;
        }

        public static float HashToUnit(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        public static uint MultiplyHighToRange(uint value, uint range)
        {
            if (range == 0u)
                return 0u;

            return (uint)(((ulong)value * range) >> 32);
        }

        public static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        public static ushort BuildRuleMask(int activeRuleCount)
        {
            int safeCount = math.clamp(activeRuleCount, 1, DeepReachStationConstants.MaxModuleRules);
            return safeCount >= 16 ? ushort.MaxValue : (ushort)((1u << safeCount) - 1u);
        }

        public static int PopCount(ushort value)
        {
            return math.countbits((uint)value);
        }

        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(float2 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        public static bool IsFinite(quaternion value)
        {
            return math.all(math.isfinite(value.value));
        }

        public static int ToIndex(int3 coord, int3 dims)
        {
            return coord.x + (coord.z * dims.x) + (coord.y * dims.x * dims.z);
        }

        public static int3 ToCoord(int index, int3 dims)
        {
            int layer = math.max(dims.x * dims.z, 1);
            int y = index / layer;
            int rem = index - (y * layer);
            int z = rem / math.max(dims.x, 1);
            int x = rem - (z * dims.x);
            return new int3(x, y, z);
        }

        public static int3 DirectionOffset(int direction)
        {
            switch (direction)
            {
                case DeepReachStationDirections.North:
                    return new int3(0, 0, 1);
                case DeepReachStationDirections.East:
                    return new int3(1, 0, 0);
                case DeepReachStationDirections.South:
                    return new int3(0, 0, -1);
                case DeepReachStationDirections.West:
                    return new int3(-1, 0, 0);
                case DeepReachStationDirections.Top:
                    return new int3(0, 1, 0);
                default:
                    return new int3(0, -1, 0);
            }
        }

        public static int OppositeDirection(int direction)
        {
            switch (direction)
            {
                case DeepReachStationDirections.North:
                    return DeepReachStationDirections.South;
                case DeepReachStationDirections.East:
                    return DeepReachStationDirections.West;
                case DeepReachStationDirections.South:
                    return DeepReachStationDirections.North;
                case DeepReachStationDirections.West:
                    return DeepReachStationDirections.East;
                case DeepReachStationDirections.Top:
                    return DeepReachStationDirections.Bottom;
                default:
                    return DeepReachStationDirections.Top;
            }
        }

        public static ushort SocketAt(in StationModuleRuleDTO rule, int direction)
        {
            switch (direction)
            {
                case DeepReachStationDirections.North:
                    return rule.SocketNorth;
                case DeepReachStationDirections.East:
                    return rule.SocketEast;
                case DeepReachStationDirections.South:
                    return rule.SocketSouth;
                case DeepReachStationDirections.West:
                    return rule.SocketWest;
                case DeepReachStationDirections.Top:
                    return rule.SocketTop;
                default:
                    return rule.SocketBottom;
            }
        }

        public static int RotateHorizontalDirection(int direction, int quarterTurns)
        {
            if ((uint)direction >= 4u)
                return direction;

            return (direction + (quarterTurns & 3)) & 3;
        }

        public static int UnrotateHorizontalDirection(int direction, int quarterTurns)
        {
            if ((uint)direction >= 4u)
                return direction;

            return (direction - (quarterTurns & 3) + 4) & 3;
        }

        public static ushort SocketAtRotated(in StationModuleRuleDTO rule, int stationDirection, int quarterTurns)
        {
            return SocketAt(rule, UnrotateHorizontalDirection(stationDirection, quarterTurns));
        }

        public static quaternion RotationFromQuarterTurns(int quarterTurns)
        {
            return quaternion.RotateY((quarterTurns & 3) * 1.5707963267948966f);
        }

        public static byte CellRotation(uint flags)
        {
            return (byte)((flags & DeepReachStationConstants.CellRotationMask) >> DeepReachStationConstants.CellRotationShift);
        }

        public static uint WithCellRotation(uint flags, byte quarterTurns)
        {
            return (flags & ~DeepReachStationConstants.CellRotationMask) |
                   (((uint)quarterTurns & 3u) << DeepReachStationConstants.CellRotationShift);
        }

        public static bool SocketsCompatible(ushort lhs, ushort rhs)
        {
            if (lhs == 0 || rhs == 0)
                return false;
            if (lhs == DeepReachStationConstants.GenericConnectorMask || rhs == DeepReachStationConstants.GenericConnectorMask)
                return true;
            return (lhs & rhs) != 0;
        }

        public static float3 LocalCenterFromCoord(int3 coord, int3 dims, float cellSize)
        {
            float3 center = ((float3)dims - 1f) * 0.5f;
            return ((float3)coord - center) * math.max(cellSize, DeepReachStationConstants.Epsilon);
        }

        public static bool IsInsideStationVolume(int3 coord, int3 dims, float quality, uint seed)
        {
            float3 center = ((float3)dims - 1f) * 0.5f;
            float3 denom = math.max(center, new float3(1f));
            float3 p = ((float3)coord - center) / denom;
            float q = Smooth01(quality);
            float width = math.lerp(0.30f, 0.58f, q);
            float height = math.lerp(0.24f, 0.52f, q);
            float length = math.lerp(0.48f, 0.82f, q);
            float shell = (p.x * p.x) / math.max(width * width, DeepReachStationConstants.Epsilon) +
                          (p.y * p.y) / math.max(height * height, DeepReachStationConstants.Epsilon) +
                          (p.z * p.z) / math.max(length * length, DeepReachStationConstants.Epsilon);
            float noise = (HashToUnit(Hash(coord, seed)) - 0.5f) * math.lerp(0.04f, 0.15f, q);
            return shell + noise <= 1f;
        }

        public static byte SelectNthSetBit(ushort mask, uint ordinal)
        {
            int count = PopCount(mask);
            if (count <= 0)
                return 0;

            int target = (int)MultiplyHighToRange(ordinal, (uint)count);
            for (int bit = 0; bit < 16; bit++)
            {
                if (((mask >> bit) & 1) == 0)
                    continue;

                if (target == 0)
                    return (byte)bit;

                target--;
            }

            return 0;
        }

        public static uint EncodeColor(byte r, byte g, byte b, byte a)
        {
            return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }

        public static uint QuantizedPositionHash(float3 position, float epsilon)
        {
            return QuantizedPositionHash(QuantizedPosition(position, epsilon));
        }

        public static int3 QuantizedPosition(float3 position, float epsilon)
        {
            float inv = math.rcp(math.max(epsilon, 0.000001f));
            return (int3)math.round(position * inv);
        }

        public static uint QuantizedPositionHash(int3 quantized)
        {
            return Hash(quantized, 2166136261u);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StationWfcSolverJob : IJob
    {
        [NoAlias] public NativeArray<StationWfcCellDTO> Grid;
        [ReadOnly, NoAlias] public NativeArray<StationModuleRuleDTO> Rules;
        [WriteOnly, NoAlias] public NativeArray<StationPlacementDTO> Placements;
        [NoAlias] public NativeArray<StationBakeCountersDTO> Counters;

        public int3 GridDims;
        public uint Seed;
        public int MaxPlacements;
        public float CellSize;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Grid.IsCreated || !Rules.IsCreated || !Placements.IsCreated || !Counters.IsCreated || Counters.Length == 0)
                return;

            StationBakeCountersDTO counters = Counters[0];
            counters.PlacementCount = 0;
            counters.FaultFlags = 0;
            counters.StateHash = Seed == 0u ? 1u : Seed;

            int activeRuleCount = ResolveActiveRuleCount();
            if (activeRuleCount <= 1)
                counters.FaultFlags |= DeepReachStationConstants.FaultNoRules;

            int3 dims = new int3(
                math.clamp(GridDims.x, 1, 64),
                math.clamp(GridDims.y, 1, 16),
                math.clamp(GridDims.z, 1, 64));
            int cellCount = math.min(Grid.Length, dims.x * dims.y * dims.z);
            int maxPlacements = math.clamp(MaxPlacements, 1, math.min(Placements.Length, cellCount));
            float rawQuality = GlobalQualityWeight;
            if (!math.isfinite(rawQuality) || !math.isfinite(CellSize))
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                counters.PlacementCount = 0;
                Counters[0] = counters;
                return;
            }

            float quality = math.saturate(rawQuality);
            float cellSize = math.max(CellSize, DeepReachStationConstants.Epsilon);
            uint seed = Seed == 0u ? 1u : Seed;

            ushort structuralMask = (ushort)(DeepReachStationMath.BuildRuleMask(activeRuleCount) & 0xFFFEu);
            if (structuralMask == 0)
                structuralMask = 0x0002;

            InitializeGrid(cellCount, dims, structuralMask, quality, seed);

            int collapsedStructural = 0;
            int iterations = 0;
            while (iterations < cellCount && collapsedStructural < maxPlacements)
            {
                int selected = SelectNextCell(cellCount, dims, collapsedStructural > 0, seed ^ (uint)(iterations * 977));
                if (selected < 0)
                    break;

                StationWfcCellDTO cell = Grid[selected];
                ushort candidateMask = cell.PossibleModuleMask;
                byte moduleId = DeepReachStationConstants.EmptyModuleId;
                byte rotation = 0;
                bool selectedCandidate = false;
                for (int attempt = 0; attempt < DeepReachStationConstants.MaxModuleRules && candidateMask != 0; attempt++)
                {
                    byte candidateModuleId = SelectModule(candidateMask, seed ^ (uint)selected ^ (uint)(iterations * 131) ^ (uint)(attempt * 6151));
                    if (SelectRotationForCell(selected, candidateModuleId, dims, cellCount, seed ^ (uint)selected ^ (uint)(iterations * 4099) ^ (uint)(attempt * 8191), out rotation))
                    {
                        moduleId = candidateModuleId;
                        selectedCandidate = true;
                        break;
                    }

                    candidateMask = (ushort)(candidateMask & ~(1u << candidateModuleId));
                }

                if (!selectedCandidate)
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultContradiction;
                    cell.CollapsedModuleId = DeepReachStationConstants.EmptyModuleId;
                    cell.PossibleModuleMask = 0x0001;
                    cell.Entropy = 0f;
                    Grid[selected] = cell;
                    break;
                }

                cell.CollapsedModuleId = moduleId;
                cell.PossibleModuleMask = (ushort)(1u << moduleId);
                cell.Entropy = 0f;
                cell.Flags = DeepReachStationMath.WithCellRotation(cell.Flags, rotation);
                Grid[selected] = cell;

                ConstrainNeighbors(selected, moduleId, rotation, dims, cellCount, ref counters);
                PropagateCollapsedConstraints(cellCount, dims, ref counters);
                if (moduleId != DeepReachStationConstants.EmptyModuleId)
                    collapsedStructural++;
                if ((counters.FaultFlags & DeepReachStationConstants.FaultContradiction) != 0u)
                    break;

                iterations++;
            }

            if ((counters.FaultFlags & DeepReachStationConstants.FaultContradiction) != 0u)
            {
                counters.PlacementCount = 0;
                Counters[0] = counters;
                return;
            }

            if (!ValidateStructuralConnectivity(cellCount, dims, ref counters))
            {
                counters.PlacementCount = 0;
                Counters[0] = counters;
                return;
            }

            EmitPlacements(cellCount, dims, cellSize, seed, maxPlacements, ref counters);
            Counters[0] = counters;
        }

        private int ResolveActiveRuleCount()
        {
            int max = math.min(Rules.Length, DeepReachStationConstants.MaxModuleRules);
            int count = 0;
            for (int i = 0; i < max; i++)
            {
                StationModuleRuleDTO rule = Rules[i];
                if (i > 0 && rule.ModuleHash == 0u)
                    break;

                count = i + 1;
            }

            return math.max(count, 1);
        }

        private void InitializeGrid(int cellCount, int3 dims, ushort structuralMask, float quality, uint seed)
        {
            for (int i = 0; i < cellCount; i++)
            {
                int3 coord = DeepReachStationMath.ToCoord(i, dims);
                bool inside = DeepReachStationMath.IsInsideStationVolume(coord, dims, quality, seed);
                ushort volumeMask = inside ? BuildVolumeCompatibleMask(coord, dims, structuralMask, quality, seed) : (ushort)0;
                bool hasVolumeCandidate = volumeMask != 0;
                StationWfcCellDTO cell = default;
                cell.PossibleModuleMask = hasVolumeCandidate ? volumeMask : (ushort)1;
                cell.CollapsedModuleId = hasVolumeCandidate ? (byte)255 : (byte)DeepReachStationConstants.EmptyModuleId;
                cell.SocketConstraints = 0;
                cell.Entropy = hasVolumeCandidate ? math.max(1f, math.log2(math.max(DeepReachStationMath.PopCount(volumeMask), 1))) : 0f;
                cell.ParentIndex = uint.MaxValue;
                cell.Flags = inside ? DeepReachStationConstants.CellInsideFlag : 0u;
                Grid[i] = cell;
            }
        }

        private ushort BuildVolumeCompatibleMask(int3 coord, int3 dims, ushort structuralMask, float quality, uint seed)
        {
            ushort mask = 0;
            int maxRule = math.min(Rules.Length, DeepReachStationConstants.MaxModuleRules);
            for (int module = 1; module < maxRule; module++)
            {
                if (((structuralMask >> module) & 1) == 0)
                    continue;

                StationModuleRuleDTO rule = Rules[module];
                for (byte rotation = 0; rotation < 4; rotation++)
                {
                    if (ModuleFitsStationVolumeAt(rule, coord, dims, rotation, quality, seed))
                    {
                        mask |= (ushort)(1u << module);
                        break;
                    }
                }
            }

            return mask;
        }

        private static bool ModuleFitsStationVolumeAt(in StationModuleRuleDTO rule, int3 coord, int3 dims, byte rotation, float quality, uint seed)
        {
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                ushort socket = DeepReachStationMath.SocketAtRotated(rule, direction, rotation);
                if (socket == 0)
                    continue;

                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    return false;

                if (!DeepReachStationMath.IsInsideStationVolume(neighborCoord, dims, quality, seed))
                    return false;
            }

            return true;
        }

        private int SelectNextCell(int cellCount, int3 dims, bool frontierOnly, uint salt)
        {
            int selected = -1;
            float bestEntropy = 3.40282347e+38f;
            uint bestTie = uint.MaxValue;
            for (int i = 0; i < cellCount; i++)
            {
                StationWfcCellDTO cell = Grid[i];
                if (cell.CollapsedModuleId != 255 || cell.PossibleModuleMask == 0)
                    continue;
                if (frontierOnly && !HasCollapsedStructuralNeighbor(i, dims, cellCount))
                    continue;

                uint tie = DeepReachStationMath.Hash(salt ^ (uint)i);
                float entropy = cell.Entropy + ((tie & 1023u) * 0.0000001f);
                if (entropy < bestEntropy || (math.abs(entropy - bestEntropy) <= 0.000001f && tie < bestTie))
                {
                    selected = i;
                    bestEntropy = entropy;
                    bestTie = tie;
                }
            }

            if (selected < 0 && frontierOnly)
                return SelectNextCell(cellCount, dims, false, salt ^ 0xA511E9B3u);

            return selected;
        }

        private bool HasCollapsedStructuralNeighbor(int cellIndex, int3 dims, int cellCount)
        {
            int3 coord = DeepReachStationMath.ToCoord(cellIndex, dims);
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    continue;

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    continue;

                StationWfcCellDTO neighbor = Grid[neighborIndex];
                if (neighbor.CollapsedModuleId != 255 && neighbor.CollapsedModuleId != DeepReachStationConstants.EmptyModuleId)
                    return true;
            }

            return false;
        }

        private byte SelectModule(ushort mask, uint salt)
        {
            ushort safeMask = mask == 0 ? (ushort)0x0002 : mask;
            if ((safeMask & 0xFFFEu) != 0)
                safeMask = (ushort)(safeMask & 0xFFFEu);

            int maxRule = math.min(Rules.Length, DeepReachStationConstants.MaxModuleRules);
            uint totalWeight = 0u;
            for (int module = 0; module < maxRule; module++)
            {
                if (((safeMask >> module) & 1) != 0)
                    totalWeight += ResolveRuleWeightUnits(module);
            }

            if (totalWeight == 0u)
                return DeepReachStationMath.SelectNthSetBit(safeMask, DeepReachStationMath.Hash(salt));

            uint target = DeepReachStationMath.MultiplyHighToRange(DeepReachStationMath.Hash(salt), totalWeight);
            uint runningWeight = 0u;
            byte selected = 0;
            for (int module = 0; module < maxRule; module++)
            {
                if (((safeMask >> module) & 1) == 0)
                    continue;

                selected = (byte)module;
                runningWeight += ResolveRuleWeightUnits(module);
                if (target < runningWeight)
                    return selected;
            }

            return selected;
        }

        private uint ResolveRuleWeightUnits(int module)
        {
            if ((uint)module >= (uint)Rules.Length)
                return 1024u;

            float weight = Rules[module].Weight;
            float safeWeight = math.isfinite(weight) && weight > 0f ? weight : 1f;
            return (uint)math.clamp((int)math.round(safeWeight * 1024f), 1, 65535);
        }

        private bool SelectRotationForCell(int cellIndex, byte moduleId, int3 dims, int cellCount, uint salt, out byte selectedRotation)
        {
            selectedRotation = 0;
            if (moduleId == DeepReachStationConstants.EmptyModuleId)
                return true;

            byte bestRotation = 0;
            int bestScore = int.MinValue;
            uint bestTie = uint.MaxValue;
            bool found = false;
            for (byte rotation = 0; rotation < 4; rotation++)
            {
                if (!RotationKeepsSocketsInsideStationVolume(cellIndex, moduleId, rotation, dims, cellCount))
                    continue;

                if (!RotationFitsCollapsedNeighbors(cellIndex, moduleId, rotation, dims, cellCount))
                    continue;

                int score = ScoreRotationAgainstStationVolume(cellIndex, moduleId, rotation, dims, cellCount);
                uint tie = DeepReachStationMath.Hash(salt ^ ((uint)rotation * 0x9E3779B9u));
                if (!found || score > bestScore || (score == bestScore && tie < bestTie))
                {
                    bestRotation = rotation;
                    bestScore = score;
                    bestTie = tie;
                    found = true;
                }
            }

            if (!found)
                return false;

            selectedRotation = bestRotation;
            return true;
        }

        private bool RotationKeepsSocketsInsideStationVolume(int cellIndex, byte moduleId, byte rotation, int3 dims, int cellCount)
        {
            StationModuleRuleDTO currentRule = Rules[math.min((int)moduleId, Rules.Length - 1)];
            int3 coord = DeepReachStationMath.ToCoord(cellIndex, dims);
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                ushort socket = DeepReachStationMath.SocketAtRotated(currentRule, direction, rotation);
                if (socket == 0)
                    continue;

                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    return false;

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    return false;

                StationWfcCellDTO neighborCell = Grid[neighborIndex];
                if ((neighborCell.Flags & DeepReachStationConstants.CellInsideFlag) == 0u)
                    return false;
                if (neighborCell.CollapsedModuleId == DeepReachStationConstants.EmptyModuleId || neighborCell.PossibleModuleMask == 1)
                    return false;
            }

            return true;
        }

        private bool RotationFitsCollapsedNeighbors(int cellIndex, byte moduleId, byte rotation, int3 dims, int cellCount)
        {
            StationModuleRuleDTO currentRule = Rules[math.min((int)moduleId, Rules.Length - 1)];
            int3 coord = DeepReachStationMath.ToCoord(cellIndex, dims);
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    continue;

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    continue;

                StationWfcCellDTO neighborCell = Grid[neighborIndex];
                if (neighborCell.CollapsedModuleId == DeepReachStationConstants.EmptyModuleId || neighborCell.CollapsedModuleId == 255)
                    continue;

                StationModuleRuleDTO neighborRule = Rules[math.min((int)neighborCell.CollapsedModuleId, Rules.Length - 1)];
                ushort currentSocket = DeepReachStationMath.SocketAtRotated(currentRule, direction, rotation);
                ushort neighborSocket = DeepReachStationMath.SocketAtRotated(
                    neighborRule,
                    DeepReachStationMath.OppositeDirection(direction),
                    DeepReachStationMath.CellRotation(neighborCell.Flags));
                if (DeepReachStationMath.SocketsCompatible(currentSocket, neighborSocket))
                    continue;

                if (currentSocket == 0 && neighborSocket == 0 && CanClosedFacesAbut(direction))
                    continue;

                return false;
            }

            return true;
        }

        private int ScoreRotationAgainstStationVolume(int cellIndex, byte moduleId, byte rotation, int3 dims, int cellCount)
        {
            StationModuleRuleDTO currentRule = Rules[math.min((int)moduleId, Rules.Length - 1)];
            int3 coord = DeepReachStationMath.ToCoord(cellIndex, dims);
            int score = 0;
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                ushort socket = DeepReachStationMath.SocketAtRotated(currentRule, direction, rotation);
                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                {
                    score += socket == 0 ? 1 : -3;
                    continue;
                }

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                {
                    score += socket == 0 ? 1 : -3;
                    continue;
                }

                StationWfcCellDTO neighborCell = Grid[neighborIndex];
                if (neighborCell.CollapsedModuleId == DeepReachStationConstants.EmptyModuleId || neighborCell.PossibleModuleMask == 1)
                    score += socket == 0 ? 1 : -3;
                else if (neighborCell.CollapsedModuleId == 255)
                    score += socket == 0 ? 0 : 2;
                else
                    score += socket == 0 ? -4 : 8;
            }

            return score;
        }

        private void ConstrainNeighbors(int selected, byte moduleId, byte rotation, int3 dims, int cellCount, ref StationBakeCountersDTO counters)
        {
            StationModuleRuleDTO currentRule = Rules[math.min((int)moduleId, Rules.Length - 1)];
            int3 coord = DeepReachStationMath.ToCoord(selected, dims);
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    continue;

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    continue;

                StationWfcCellDTO neighbor = Grid[neighborIndex];
                if (neighbor.CollapsedModuleId != 255)
                    continue;

                ushort compatible = BuildCompatibleMask(currentRule, rotation, direction, neighbor.PossibleModuleMask);
                ushort nextMask = (ushort)(neighbor.PossibleModuleMask & compatible);
                if (nextMask == 0)
                {
                    nextMask = 0x0001;
                    counters.FaultFlags |= DeepReachStationConstants.FaultContradiction;
                }

                if (nextMask != neighbor.PossibleModuleMask)
                {
                    neighbor.PossibleModuleMask = nextMask;
                    neighbor.Entropy = math.max(0f, math.log2(math.max(DeepReachStationMath.PopCount(nextMask), 1)));
                    neighbor.SocketConstraints = (byte)(neighbor.SocketConstraints | (1 << DeepReachStationMath.OppositeDirection(direction)));
                    Grid[neighborIndex] = neighbor;
                }
            }
        }

        private void PropagateCollapsedConstraints(int cellCount, int3 dims, ref StationBakeCountersDTO counters)
        {
            bool changed = true;
            int pass = 0;
            while (changed && pass < cellCount)
            {
                changed = false;
                for (int i = 0; i < cellCount; i++)
                {
                    StationWfcCellDTO cell = Grid[i];
                    if (cell.CollapsedModuleId != 255)
                        continue;

                    ushort nextMask = ReduceMaskAgainstCollapsedNeighbors(i, dims, cellCount, cell.PossibleModuleMask);
                    if (nextMask == 0)
                    {
                        nextMask = 0x0001;
                        counters.FaultFlags |= DeepReachStationConstants.FaultContradiction;
                    }

                    if (nextMask == cell.PossibleModuleMask)
                        continue;

                    cell.PossibleModuleMask = nextMask;
                    cell.Entropy = math.max(0f, math.log2(math.max(DeepReachStationMath.PopCount(nextMask), 1)));
                    Grid[i] = cell;
                    changed = true;
                }

                pass++;
            }

            if (changed)
                counters.FaultFlags |= DeepReachStationConstants.FaultContradiction;
        }

        private ushort ReduceMaskAgainstCollapsedNeighbors(int cellIndex, int3 dims, int cellCount, ushort candidateMask)
        {
            ushort mask = candidateMask;
            int3 coord = DeepReachStationMath.ToCoord(cellIndex, dims);
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    continue;

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    continue;

                StationWfcCellDTO neighbor = Grid[neighborIndex];
                if (neighbor.CollapsedModuleId == 255)
                    continue;

                if (neighbor.CollapsedModuleId == DeepReachStationConstants.EmptyModuleId)
                {
                    ushort compatibleWithVoid = BuildClosedFaceMask(direction, mask);
                    mask = (ushort)(mask & compatibleWithVoid);
                    if (mask == 0)
                        break;
                    continue;
                }

                StationModuleRuleDTO neighborRule = Rules[math.min((int)neighbor.CollapsedModuleId, Rules.Length - 1)];
                int neighborToCellDirection = DeepReachStationMath.OppositeDirection(direction);
                ushort compatible = BuildCompatibleMask(
                    neighborRule,
                    DeepReachStationMath.CellRotation(neighbor.Flags),
                    neighborToCellDirection,
                    mask);
                mask = (ushort)(mask & compatible);
                if (mask == 0)
                    break;
            }

            return mask;
        }

        private ushort BuildClosedFaceMask(int directionToEmpty, ushort candidateMask)
        {
            ushort mask = 0x0001;
            int maxRule = math.min(Rules.Length, DeepReachStationConstants.MaxModuleRules);
            for (int module = 1; module < maxRule; module++)
            {
                if (((candidateMask >> module) & 1) == 0)
                    continue;

                StationModuleRuleDTO candidate = Rules[module];
                for (byte candidateRotation = 0; candidateRotation < 4; candidateRotation++)
                {
                    if (DeepReachStationMath.SocketAtRotated(candidate, directionToEmpty, candidateRotation) == 0)
                    {
                        mask |= (ushort)(1 << module);
                        break;
                    }
                }
            }

            return mask;
        }

        private ushort BuildCompatibleMask(in StationModuleRuleDTO currentRule, byte currentRotation, int direction, ushort neighborMask)
        {
            ushort mask = 0;
            ushort currentSocket = DeepReachStationMath.SocketAtRotated(currentRule, direction, currentRotation);
            int opposite = DeepReachStationMath.OppositeDirection(direction);
            int maxRule = math.min(Rules.Length, DeepReachStationConstants.MaxModuleRules);
            for (int module = 0; module < maxRule; module++)
            {
                if (((neighborMask >> module) & 1) == 0)
                    continue;

                StationModuleRuleDTO candidate = Rules[module];
                if (currentSocket == 0)
                {
                    if (module == DeepReachStationConstants.EmptyModuleId)
                    {
                        mask |= (ushort)(1 << module);
                        continue;
                    }

                    if (CanClosedFacesAbut(direction) &&
                        ModuleHasClosedFace(candidate, opposite))
                        mask |= (ushort)(1 << module);

                    continue;
                }

                for (byte candidateRotation = 0; candidateRotation < 4; candidateRotation++)
                {
                    ushort candidateSocket = DeepReachStationMath.SocketAtRotated(candidate, opposite, candidateRotation);
                    if (DeepReachStationMath.SocketsCompatible(currentSocket, candidateSocket))
                    {
                        mask |= (ushort)(1 << module);
                        break;
                    }
                }
            }

            return mask;
        }

        private static bool CanClosedFacesAbut(int direction)
        {
            return direction != DeepReachStationDirections.Top &&
                   direction != DeepReachStationDirections.Bottom;
        }

        private static bool ModuleHasClosedFace(in StationModuleRuleDTO rule, int direction)
        {
            for (byte candidateRotation = 0; candidateRotation < 4; candidateRotation++)
            {
                if (DeepReachStationMath.SocketAtRotated(rule, direction, candidateRotation) == 0)
                    return true;
            }

            return false;
        }

        private bool ValidateStructuralConnectivity(int cellCount, int3 dims, ref StationBakeCountersDTO counters)
        {
            int first = -1;
            int structuralCount = 0;
            for (int i = 0; i < cellCount; i++)
            {
                StationWfcCellDTO cell = Grid[i];
                if (!IsCollapsedStructural(cell))
                    continue;

                cell.ParentIndex = uint.MaxValue;
                Grid[i] = cell;
                if (first < 0)
                    first = i;
                structuralCount++;
            }

            if (structuralCount <= 1)
                return true;

            StationWfcCellDTO root = Grid[first];
            root.ParentIndex = (uint)first;
            Grid[first] = root;

            int visitedCount = 1;
            bool changed = true;
            int pass = 0;
            while (changed && pass < structuralCount)
            {
                changed = false;
                for (int i = 0; i < cellCount; i++)
                {
                    StationWfcCellDTO cell = Grid[i];
                    if (!IsCollapsedStructural(cell) || cell.ParentIndex != uint.MaxValue)
                        continue;

                    if (!HasVisitedCompatibleNeighbor(i, cell, dims, cellCount))
                        continue;

                    cell.ParentIndex = (uint)i;
                    Grid[i] = cell;
                    visitedCount++;
                    changed = true;
                }

                pass++;
            }

            if (visitedCount == structuralCount)
                return true;

            counters.FaultFlags |= DeepReachStationConstants.FaultInvalidTopology;
            return false;
        }

        private static bool IsCollapsedStructural(in StationWfcCellDTO cell)
        {
            return cell.CollapsedModuleId != DeepReachStationConstants.EmptyModuleId &&
                   cell.CollapsedModuleId != 255;
        }

        private bool HasVisitedCompatibleNeighbor(int cellIndex, in StationWfcCellDTO cell, int3 dims, int cellCount)
        {
            int3 coord = DeepReachStationMath.ToCoord(cellIndex, dims);
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    continue;

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    continue;

                StationWfcCellDTO neighbor = Grid[neighborIndex];
                if (!IsCollapsedStructural(neighbor) || neighbor.ParentIndex == uint.MaxValue)
                    continue;

                if (StructuralSocketsCompatible(cell, neighbor, direction))
                    return true;
            }

            return false;
        }

        private bool StructuralSocketsCompatible(in StationWfcCellDTO currentCell, in StationWfcCellDTO neighborCell, int currentToNeighborDirection)
        {
            if (Rules.Length <= 0)
                return false;

            StationModuleRuleDTO current = Rules[math.min((int)currentCell.CollapsedModuleId, Rules.Length - 1)];
            StationModuleRuleDTO neighbor = Rules[math.min((int)neighborCell.CollapsedModuleId, Rules.Length - 1)];
            ushort currentSocket = DeepReachStationMath.SocketAtRotated(
                current,
                currentToNeighborDirection,
                DeepReachStationMath.CellRotation(currentCell.Flags));
            ushort neighborSocket = DeepReachStationMath.SocketAtRotated(
                neighbor,
                DeepReachStationMath.OppositeDirection(currentToNeighborDirection),
                DeepReachStationMath.CellRotation(neighborCell.Flags));
            return DeepReachStationMath.SocketsCompatible(currentSocket, neighborSocket);
        }

        private void EmitPlacements(int cellCount, int3 dims, float cellSize, uint seed, int maxPlacements, ref StationBakeCountersDTO counters)
        {
            uint placementCount = 0u;
            for (int i = 0; i < cellCount && placementCount < (uint)maxPlacements; i++)
            {
                StationWfcCellDTO cell = Grid[i];
                if (cell.CollapsedModuleId == DeepReachStationConstants.EmptyModuleId || cell.CollapsedModuleId == 255)
                    continue;

                int3 coord = DeepReachStationMath.ToCoord(i, dims);
                float3 local = DeepReachStationMath.LocalCenterFromCoord(coord, dims, cellSize);
                byte rotation = DeepReachStationMath.CellRotation(cell.Flags);
                quaternion orientation = DeepReachStationMath.RotationFromQuarterTurns(rotation);
                StationPlacementDTO placement = default;
                placement.LocalToStation = float4x4.TRS(local, orientation, new float3(1f));
                placement.GridCoord = coord;
                placement.StableHash = DeepReachStationMath.Hash(seed ^ (uint)i ^ ((uint)cell.CollapsedModuleId << 24) ^ ((uint)rotation << 16));
                placement.ConnectedDirectionMask = ResolveConnectedDirectionMask(i, cell.CollapsedModuleId, rotation, dims, cellCount);
                placement.ModuleId = cell.CollapsedModuleId;
                placement.RotationQuarterTurns = rotation;
                placement.Flags = 1u;

                if (!DeepReachStationMath.IsFinite(placement.LocalToStation))
                {
                    placement.LocalToStation = float4x4.identity;
                    counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                }

                Placements[(int)placementCount] = placement;
                counters.StateHash = DeepReachStationMath.Hash(counters.StateHash ^ placement.StableHash);
                placementCount++;
            }

            counters.PlacementCount = placementCount;
        }

        private ushort ResolveConnectedDirectionMask(int cellIndex, byte moduleId, byte rotation, int3 dims, int cellCount)
        {
            ushort mask = 0;
            StationModuleRuleDTO current = Rules[math.min((int)moduleId, Rules.Length - 1)];
            int3 coord = DeepReachStationMath.ToCoord(cellIndex, dims);
            for (int direction = 0; direction < DeepReachStationConstants.DirectionCount; direction++)
            {
                int3 neighborCoord = coord + DeepReachStationMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    continue;

                int neighborIndex = DeepReachStationMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    continue;

                StationWfcCellDTO neighborCell = Grid[neighborIndex];
                if (neighborCell.CollapsedModuleId == DeepReachStationConstants.EmptyModuleId || neighborCell.CollapsedModuleId == 255)
                    continue;

                StationModuleRuleDTO neighbor = Rules[math.min((int)neighborCell.CollapsedModuleId, Rules.Length - 1)];
                ushort currentSocket = DeepReachStationMath.SocketAtRotated(current, direction, rotation);
                ushort neighborSocket = DeepReachStationMath.SocketAtRotated(
                    neighbor,
                    DeepReachStationMath.OppositeDirection(direction),
                    DeepReachStationMath.CellRotation(neighborCell.Flags));
                if (DeepReachStationMath.SocketsCompatible(currentSocket, neighborSocket))
                {
                    int localDirection = DeepReachStationMath.UnrotateHorizontalDirection(direction, rotation);
                    mask |= (ushort)(1 << localDirection);
                }
            }

            return mask;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StationMeshFusionJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<StationPlacementDTO> Placements;
        [ReadOnly, NoAlias] public NativeArray<StationMeshSliceDTO> MeshSlices;
        [ReadOnly, NoAlias] public NativeArray<StationMeshVertexDTO> SourceVertices;
        [ReadOnly, NoAlias] public NativeArray<StationTriangleDTO> SourceTriangles;
        [NoAlias] public NativeArray<StationMeshVertexDTO> TransformedVertices;
        [NoAlias] public NativeArray<int> RawIndices;
        [NoAlias] public NativeArray<ushort> RawTriangleMaterials;
        [NoAlias] public NativeArray<StationBakeCountersDTO> Counters;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length == 0)
                return;

            StationBakeCountersDTO counters = Counters[0];
            int placementCount = (int)math.min(counters.PlacementCount, (uint)Placements.Length);
            int vertexWrite = 0;
            int indexWrite = 0;
            uint culledTriangles = 0u;

            for (int p = 0; p < placementCount; p++)
            {
                StationPlacementDTO placement = Placements[p];
                int moduleId = placement.ModuleId;
                if ((uint)moduleId >= (uint)MeshSlices.Length)
                    continue;

                StationMeshSliceDTO slice = MeshSlices[moduleId];
                if (!IsValidSlice(slice))
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultInvalidTopology;
                    break;
                }

                if (!DeepReachStationMath.IsFinite(placement.LocalToStation))
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                    break;
                }

                if (!HasFiniteVertices(slice))
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                    break;
                }

                int placementVertexBase = vertexWrite;
                float3x3 normalMatrix = new float3x3(
                    placement.LocalToStation.c0.xyz,
                    placement.LocalToStation.c1.xyz,
                    placement.LocalToStation.c2.xyz);

                if (vertexWrite + slice.VertexCount > TransformedVertices.Length)
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                    break;
                }

                int visibleTriangleCount = 0;
                uint placementCulledTriangles = 0u;
                bool placementTopologyValid = true;
                for (int t = 0; t < slice.TriangleCount; t++)
                {
                    StationTriangleDTO triangle = SourceTriangles[slice.TriangleStart + t];
                    if ((triangle.CullDirectionMask & placement.ConnectedDirectionMask) != 0)
                    {
                        placementCulledTriangles++;
                        continue;
                    }

                    if (!IsValidTriangle(triangle, slice.VertexCount) ||
                        IsDegenerateTriangle(slice, triangle))
                    {
                        counters.FaultFlags |= DeepReachStationConstants.FaultInvalidTopology;
                        placementTopologyValid = false;
                        break;
                    }

                    visibleTriangleCount++;
                }

                if (!placementTopologyValid)
                    break;

                if (indexWrite + visibleTriangleCount * 3 > RawIndices.Length)
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                    break;
                }

                if (RawTriangleMaterials.IsCreated && indexWrite / 3 + visibleTriangleCount > RawTriangleMaterials.Length)
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                    break;
                }

                culledTriangles += placementCulledTriangles;

                for (int v = 0; v < slice.VertexCount; v++)
                {
                    StationMeshVertexDTO source = SourceVertices[slice.VertexStart + v];
                    StationMeshVertexDTO transformed = source;
                    transformed.Position = math.transform(placement.LocalToStation, source.Position);
                    transformed.Normal = math.normalizesafe(math.mul(normalMatrix, source.Normal), new float3(0f, 1f, 0f));
                    TransformedVertices[vertexWrite++] = transformed;
                }

                int triangleWrite = indexWrite / 3;
                for (int t = 0; t < slice.TriangleCount; t++)
                {
                    StationTriangleDTO triangle = SourceTriangles[slice.TriangleStart + t];
                    if ((triangle.CullDirectionMask & placement.ConnectedDirectionMask) != 0)
                        continue;

                    if (RawTriangleMaterials.IsCreated)
                        RawTriangleMaterials[triangleWrite] = triangle.SubMesh;

                    RawIndices[indexWrite++] = placementVertexBase + triangle.Index0;
                    RawIndices[indexWrite++] = placementVertexBase + triangle.Index1;
                    RawIndices[indexWrite++] = placementVertexBase + triangle.Index2;
                    triangleWrite++;
                }
            }

            uint fatal = DeepReachStationConstants.FaultCapacity |
                         DeepReachStationConstants.FaultNonFinite |
                         DeepReachStationConstants.FaultInvalidTopology;
            if ((counters.FaultFlags & fatal) != 0u)
            {
                counters.SourceVertexCount = 0u;
                counters.SourceIndexCount = 0u;
                counters.CulledTriangleCount = 0u;
            }
            else
            {
                counters.SourceVertexCount = (uint)vertexWrite;
                counters.SourceIndexCount = (uint)indexWrite;
                counters.CulledTriangleCount = culledTriangles;
            }

            Counters[0] = counters;
        }

        private bool IsValidSlice(in StationMeshSliceDTO slice)
        {
            if (slice.VertexStart < 0 || slice.VertexCount < 0 || slice.TriangleStart < 0 || slice.TriangleCount < 0)
                return false;

            long vertexEnd = (long)slice.VertexStart + slice.VertexCount;
            long triangleEnd = (long)slice.TriangleStart + slice.TriangleCount;
            return vertexEnd <= SourceVertices.Length && triangleEnd <= SourceTriangles.Length;
        }

        private static bool IsValidTriangle(in StationTriangleDTO triangle, int sliceVertexCount)
        {
            return (uint)triangle.Index0 < (uint)sliceVertexCount &&
                   (uint)triangle.Index1 < (uint)sliceVertexCount &&
                   (uint)triangle.Index2 < (uint)sliceVertexCount &&
                   triangle.Index0 != triangle.Index1 &&
                   triangle.Index1 != triangle.Index2 &&
                   triangle.Index2 != triangle.Index0;
        }

        private bool IsDegenerateTriangle(in StationMeshSliceDTO slice, in StationTriangleDTO triangle)
        {
            float3 a = SourceVertices[slice.VertexStart + triangle.Index0].Position;
            float3 b = SourceVertices[slice.VertexStart + triangle.Index1].Position;
            float3 c = SourceVertices[slice.VertexStart + triangle.Index2].Position;
            float3 areaVector = math.cross(b - a, c - a);
            return math.lengthsq(areaVector) <= DeepReachStationConstants.Epsilon * DeepReachStationConstants.Epsilon;
        }

        private bool HasFiniteVertices(in StationMeshSliceDTO slice)
        {
            for (int i = 0; i < slice.VertexCount; i++)
            {
                StationMeshVertexDTO vertex = SourceVertices[slice.VertexStart + i];
                if (!DeepReachStationMath.IsFinite(vertex.Position) ||
                    !DeepReachStationMath.IsFinite(vertex.Normal) ||
                    !DeepReachStationMath.IsFinite(vertex.Uv0))
                    return false;
            }

            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StationVertexWeldingJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<StationMeshVertexDTO> SourceVertices;
        [ReadOnly, NoAlias] public NativeArray<int> SourceIndices;
        [ReadOnly, NoAlias] public NativeArray<ushort> SourceTriangleMaterials;
        [NoAlias] public NativeArray<StationMeshVertexDTO> WeldedVertices;
        [NoAlias] public NativeArray<int> WeldedIndices;
        [NoAlias] public NativeArray<ushort> WeldedTriangleMaterials;
        [NoAlias] public NativeArray<int> SourceToWeldedRemap;
        [NoAlias] public NativeArray<StationWeldBucketDTO> Buckets;
        [NoAlias] public NativeArray<StationBakeCountersDTO> Counters;

        public float WeldEpsilon;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length == 0)
                return;

            StationBakeCountersDTO counters = Counters[0];
            if (!math.isfinite(WeldEpsilon))
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                counters.WeldedVertexCount = 0u;
                counters.WeldedIndexCount = 0u;
                counters.MergedVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            if (!SourceVertices.IsCreated ||
                !SourceIndices.IsCreated ||
                !WeldedVertices.IsCreated ||
                !WeldedIndices.IsCreated ||
                !SourceToWeldedRemap.IsCreated ||
                !Buckets.IsCreated)
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                counters.WeldedVertexCount = 0u;
                counters.WeldedIndexCount = 0u;
                counters.MergedVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            int bucketCount = Buckets.Length;
            if (!IsPowerOfTwo(bucketCount))
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                counters.WeldedVertexCount = 0u;
                counters.WeldedIndexCount = 0u;
                counters.MergedVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            int sourceVertexCount = (int)math.min(counters.SourceVertexCount, (uint)SourceVertices.Length);
            int sourceIndexCount = (int)math.min(counters.SourceIndexCount, (uint)SourceIndices.Length);
            if (sourceIndexCount % 3 != 0)
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultInvalidTopology;
                counters.WeldedVertexCount = 0u;
                counters.WeldedIndexCount = 0u;
                counters.MergedVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            bool copyTriangleMaterials = SourceTriangleMaterials.IsCreated || WeldedTriangleMaterials.IsCreated;
            if (copyTriangleMaterials && (!SourceTriangleMaterials.IsCreated || !WeldedTriangleMaterials.IsCreated))
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                counters.WeldedVertexCount = 0u;
                counters.WeldedIndexCount = 0u;
                counters.MergedVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            int sourceTriangleCount = sourceIndexCount / 3;
            if (copyTriangleMaterials &&
                (sourceTriangleCount > SourceTriangleMaterials.Length || sourceTriangleCount > WeldedTriangleMaterials.Length))
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                counters.WeldedVertexCount = 0u;
                counters.WeldedIndexCount = 0u;
                counters.MergedVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            if (sourceVertexCount > SourceToWeldedRemap.Length)
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                sourceVertexCount = SourceToWeldedRemap.Length;
            }

            if (sourceIndexCount > WeldedIndices.Length)
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                sourceIndexCount = WeldedIndices.Length;
            }

            float epsilon = math.max(WeldEpsilon, 0.00001f);
            float epsilonSq = epsilon * epsilon;

            for (int i = 0; i < sourceVertexCount; i++)
                SourceToWeldedRemap[i] = -1;

            for (int i = 0; i < bucketCount; i++)
            {
                StationWeldBucketDTO bucket = default;
                bucket.Key = uint.MaxValue;
                bucket.VertexIndex = -1;
                Buckets[i] = bucket;
            }

            int weldedCount = 0;
            int weldedIndexWrite = 0;
            int referencedSourceVertices = 0;
            ushort currentTriangleMaterial = 0;
            for (int i = 0; i < sourceIndexCount; i++)
            {
                if (copyTriangleMaterials && i % 3 == 0)
                    currentTriangleMaterial = SourceTriangleMaterials[i / 3];

                int sourceIndex = SourceIndices[i];
                if ((uint)sourceIndex >= (uint)sourceVertexCount)
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                    continue;
                }

                StationMeshVertexDTO sourceVertex = SourceVertices[sourceIndex];
                if (!DeepReachStationMath.IsFinite(sourceVertex.Position) ||
                    !DeepReachStationMath.IsFinite(sourceVertex.Normal) ||
                    !DeepReachStationMath.IsFinite(sourceVertex.Uv0))
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                    continue;
                }

                int remapped = SourceToWeldedRemap[sourceIndex];
                if (remapped < 0)
                {
                    referencedSourceVertices++;
                    remapped = ResolveOrInsertVertex(sourceVertex, ref weldedCount, bucketCount, epsilon, epsilonSq, ref counters);
                    SourceToWeldedRemap[sourceIndex] = remapped;
                }

                WeldedIndices[weldedIndexWrite++] = remapped;
                if (copyTriangleMaterials && i % 3 == 2)
                    WeldedTriangleMaterials[weldedIndexWrite / 3 - 1] = currentTriangleMaterial;
            }

            uint fatal = DeepReachStationConstants.FaultCapacity |
                         DeepReachStationConstants.FaultNonFinite |
                         DeepReachStationConstants.FaultInvalidTopology;
            if ((counters.FaultFlags & fatal) != 0u)
            {
                counters.WeldedVertexCount = 0u;
                counters.WeldedIndexCount = 0u;
                counters.MergedVertexCount = 0u;
            }
            else
            {
                counters.WeldedVertexCount = (uint)weldedCount;
                counters.WeldedIndexCount = (uint)weldedIndexWrite;
                counters.MergedVertexCount = referencedSourceVertices > weldedCount ? (uint)(referencedSourceVertices - weldedCount) : 0u;
            }

            Counters[0] = counters;
        }

        private int ResolveOrInsertVertex(StationMeshVertexDTO vertex, ref int weldedCount, int bucketCount, float epsilon, float epsilonSq, ref StationBakeCountersDTO counters)
        {
            if (bucketCount <= 0)
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                return 0;
            }

            int3 quantized = DeepReachStationMath.QuantizedPosition(vertex.Position, epsilon);
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int3 neighborQuantized = quantized + new int3(x, y, z);
                        uint neighborKey = DeepReachStationMath.QuantizedPositionHash(neighborQuantized);
                        if (TryFindExistingVertex(neighborQuantized, neighborKey, vertex, bucketCount, epsilonSq, out int existingIndex))
                            return existingIndex;
                    }
                }
            }

            uint key = DeepReachStationMath.QuantizedPositionHash(quantized);
            return InsertVertex(vertex, quantized, key, ref weldedCount, bucketCount, ref counters);
        }

        private bool TryFindExistingVertex(int3 quantized, uint key, StationMeshVertexDTO vertex, int bucketCount, float epsilonSq, out int vertexIndex)
        {
            int start = BucketSlot(key, bucketCount);
            for (int probe = 0; probe < bucketCount; probe++)
            {
                int slot = ProbeSlot(start, probe, bucketCount);
                StationWeldBucketDTO bucket = Buckets[slot];
                if (bucket.VertexIndex < 0)
                    break;

                if (bucket.Key == key &&
                    math.all(bucket.QuantizedCoord == quantized) &&
                    math.distancesq(bucket.Position, vertex.Position) <= epsilonSq &&
                    CanWeldVertices(WeldedVertices[bucket.VertexIndex], vertex, epsilonSq))
                {
                    vertexIndex = bucket.VertexIndex;
                    return true;
                }
            }

            vertexIndex = -1;
            return false;
        }

        private static bool CanWeldVertices(in StationMeshVertexDTO existing, in StationMeshVertexDTO candidate, float epsilonSq)
        {
            if (math.distancesq(existing.Position, candidate.Position) > epsilonSq)
                return false;

            float3 existingNormal = math.normalizesafe(existing.Normal, new float3(0f, 1f, 0f));
            float3 candidateNormal = math.normalizesafe(candidate.Normal, new float3(0f, 1f, 0f));
            if (math.dot(existingNormal, candidateNormal) < DeepReachStationConstants.WeldNormalDotMin)
                return false;

            return math.lengthsq(existing.Uv0 - candidate.Uv0) <= DeepReachStationConstants.WeldUvDistanceSqMax;
        }

        private int InsertVertex(StationMeshVertexDTO vertex, int3 quantized, uint key, ref int weldedCount, int bucketCount, ref StationBakeCountersDTO counters)
        {
            int start = BucketSlot(key, bucketCount);
            for (int probe = 0; probe < bucketCount; probe++)
            {
                int slot = ProbeSlot(start, probe, bucketCount);
                StationWeldBucketDTO bucket = Buckets[slot];
                if (bucket.VertexIndex >= 0)
                    continue;

                if (weldedCount >= WeldedVertices.Length)
                {
                    counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
                    return math.max(weldedCount - 1, 0);
                }

                int index = weldedCount++;
                WeldedVertices[index] = vertex;
                bucket.Key = key;
                bucket.VertexIndex = index;
                bucket.QuantizedCoord = quantized;
                bucket.Position = vertex.Position;
                bucket.Flags = 1u;
                Buckets[slot] = bucket;
                return index;
            }

            counters.FaultFlags |= DeepReachStationConstants.FaultCapacity;
            return 0;
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static int BucketSlot(uint key, int bucketCount)
        {
            return (int)(key & (uint)(bucketCount - 1));
        }

        private static int ProbeSlot(int start, int probe, int bucketCount)
        {
            return (start + probe) & (bucketCount - 1);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StationProceduralDamageJob : IJob
    {
        [NoAlias] public NativeArray<StationMeshVertexDTO> Vertices;
        [NoAlias] public NativeArray<StationBakeCountersDTO> Counters;

        public uint Seed;
        public float GlobalQualityWeight;
        public float3 StationHalfExtents;

        /// <summary>
        /// Neutral value for vertex colour A on this route: no soot authored.
        ///
        /// There is no soot data in this route, stated plainly rather than substituted. This job
        /// carries Position, unitNormal, a crush-derived damage01 and the quality weight. damage01 is
        /// impact deformation, not combustion, and it already drives rust, algae and wearBlocker;
        /// reusing it a fourth time would smear one mechanical field across all four channels and
        /// call the result soot. The real signal would have to come from a burn/thermal event the
        /// station bake does not model, or from the texture route the shader already consults via
        /// bakedCarbonization.
        ///
        /// 0 matches the compliant hard-surface writer -- h8forge/vertexcolor.py
        /// write_hard_surface_channels passes <c>get_a = channel(emission_mask, 0.0)</c>. Note that
        /// hard-surface A defaults to 0.0 whereas the ORGANIC contract's A defaults to 1.0; the two
        /// are not interchangeable, and 3dmodel.md section 4 makes this channel an OPTIONAL
        /// emission / warning paint / decal eligibility mask, so absent is a legal state.
        ///
        /// NOT A CONTRACT ROTATION, deliberately: R still carries rust and B still carries
        /// wearBlocker, which section 4 assigns to edge wear and baked ambient occlusion. That
        /// mismatch is a five-signals-into-four-channels problem -- the wreck route wants rust,
        /// algae, grime, soot and vertex AO, while section 4 collapses the first three into G alone,
        /// and unlike a redundant authored phase none of the five is synthesizable. It is an owner
        /// decision, not a repack, and this change fixes only the absent-data default.
        /// </summary>
        private const byte NoSootMask = 0;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length == 0 || !Vertices.IsCreated)
                return;

            StationBakeCountersDTO counters = Counters[0];
            int vertexCount = (int)math.min(counters.WeldedVertexCount, (uint)Vertices.Length);
            bool invalidInput = false;
            float quality = GlobalQualityWeight;
            if (!math.isfinite(quality))
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                counters.DamageVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            if (!DeepReachStationMath.IsFinite(StationHalfExtents))
            {
                counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                counters.DamageVertexCount = 0u;
                Counters[0] = counters;
                return;
            }

            float q = DeepReachStationMath.Smooth01(quality);
            int sphereCount = math.clamp((int)math.round(math.lerp(1f, DeepReachStationConstants.MaxDamageSpheres, q)), 1, DeepReachStationConstants.MaxDamageSpheres);
            float3 extents = math.max(StationHalfExtents, new float3(1f));
            uint damaged = 0u;

            for (int i = 0; i < vertexCount; i++)
            {
                StationMeshVertexDTO vertex = Vertices[i];
                if (!DeepReachStationMath.IsFinite(vertex.Position) ||
                    !DeepReachStationMath.IsFinite(vertex.Normal) ||
                    !DeepReachStationMath.IsFinite(vertex.Uv0))
                {
                    invalidInput = true;
                    counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                    break;
                }

                float3 unitNormal = math.normalizesafe(vertex.Normal, new float3(0f, 1f, 0f));
                vertex.Normal = unitNormal;

                float damage01 = 0f;
                for (int s = 0; s < DeepReachStationConstants.MaxDamageSpheres; s++)
                {
                    if (s >= sphereCount)
                        continue;

                    uint h = DeepReachStationMath.Hash(Seed ^ ((uint)s * 0x9E3779B9u));
                    float3 center = new float3(
                        (DeepReachStationMath.HashToUnit(h ^ 0xA1u) * 2f - 1f) * extents.x,
                        (DeepReachStationMath.HashToUnit(h ^ 0xB2u) * 2f - 1f) * extents.y * 0.55f,
                        (DeepReachStationMath.HashToUnit(h ^ 0xC3u) * 2f - 1f) * extents.z);
                    float radius = math.lerp(1.2f, 4.5f, DeepReachStationMath.HashToUnit(h ^ 0xD4u)) * math.lerp(0.7f, 1.35f, q);
                    float radiusSq = math.max(radius * radius, DeepReachStationConstants.Epsilon);
                    float distSq = math.lengthsq(vertex.Position - center);
                    float localDamage = math.saturate(1f - (distSq / radiusSq));
                    damage01 = math.max(damage01, localDamage * localDamage);
                }

                if (damage01 > 0f)
                {
                    float crush = damage01 * math.lerp(0.08f, 0.42f, q);
                    vertex.Position -= unitNormal * crush;
                    damaged++;
                }

                if (!DeepReachStationMath.IsFinite(vertex.Position))
                {
                    invalidInput = true;
                    counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite;
                    break;
                }

                byte rust = (byte)math.clamp((int)math.round(math.saturate(0.22f + damage01 * 0.78f) * 255f), 0, 255);
                byte algae = (byte)math.clamp((int)math.round(math.saturate((0.5f - unitNormal.y * 0.35f) + damage01 * 0.35f) * 255f), 0, 255);
                byte wearBlocker = (byte)math.clamp((int)math.round(math.saturate(0.28f - damage01 * 0.28f) * 255f), 0, 255);
                // Channel A is read by Hecton_WreckIndirectLit.shader :249 as the soot mask
                // (vertexSoot = saturate(COLOR.a * _WreckSootStrength)), so A is an AMOUNT of soot
                // and 255 was not "no soot data" -- it was MAXIMUM soot. Same absent-data defect
                // class as a baked-AO channel written as 0, where 0 is not absent occlusion but
                // maximal occlusion: a default parked at the maximum of its range rather than the
                // neutral end, which reads as fully-authored data and never errors.
                //
                // Measured consequence of the old 255 at the shader's _WreckSootStrength default of
                // 0.92: vertexSoot = 0.92, and :250 folds it in with max(), so sootResponse >= 0.92
                // on every vertex regardless of the texture -- driving albedo 92% toward
                // _WreckSootTint (:272), ambient occlusion to 0.52x (:273) and smoothness to 0.36x
                // (:274) across the entire station.
                //
                // 0 is VERIFIED neutral, not assumed: sootResponse appears only as a lerp t at
                // :272-274 where t = 0 returns each value unmodified, it is never used inverted as
                // (1 - sootResponse), and the max() at :250 means A = 0 hands the result entirely to
                // bakedCarbonization -- "no vertex soot authored, defer to the texture".
                vertex.ColorRgba = DeepReachStationMath.EncodeColor(rust, algae, wearBlocker, NoSootMask);
                Vertices[i] = vertex;
            }

            counters.DamageVertexCount = invalidInput ? 0u : damaged;
            Counters[0] = counters;
        }
    }
}
#endif
