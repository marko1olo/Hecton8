using System.Runtime.InteropServices;
using Hecton8.Logistics.Grid.Contracts;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.Outposts
{
    internal static class MarauderOutpostConstants
    {
        public const int FullWidth = 10;
        public const int FullDepth = 10;
        public const int FullHeight = 5;
        public const int LowWidth = 5;
        public const int LowDepth = 5;
        public const int LowHeight = 3;
        public const int FullCellCount = FullWidth * FullDepth * FullHeight;
        public const int LowCellCount = LowWidth * LowDepth * LowHeight;
        public const int MaxShellMatrices = 1024;
        public const int MaxInteractables = 16;
        public const int CounterCount = 8;
        public const int TelemetryFrames = 300;
        public const float HeightUShortToUnit = 0.0000152590219f;

        public const byte Empty = WfcOutpostGridConstants.Empty;
        public const byte Corridor = WfcOutpostGridConstants.Corridor;
        public const byte Room = WfcOutpostGridConstants.Room;
        public const byte Hatch = WfcOutpostGridConstants.Hatch;
        public const byte Datapad = WfcOutpostGridConstants.Datapad;
        public const byte SealedDoor = WfcOutpostGridConstants.SealedDoor;
        public const byte Window = WfcOutpostGridConstants.Window;
        public const byte Pillar = WfcOutpostGridConstants.Pillar;
        public const byte Generator = WfcOutpostGridConstants.Generator;

        public const byte CellMask = WfcOutpostGridConstants.CellMask;
        public const byte North = WfcOutpostGridConstants.North;
        public const byte East = WfcOutpostGridConstants.East;
        public const byte South = WfcOutpostGridConstants.South;
        public const byte West = WfcOutpostGridConstants.West;
        public const byte MutableStateMask = 0x0F;

        public const uint FaultFlag = 1u << 31;
        public const uint LowTierFlag = WfcOutpostGridConstants.DescriptorFlagLowTier;
        public const uint HeightmapFallbackFlag = WfcOutpostGridConstants.DescriptorFlagHeightmapFallback;
        public const uint AupShiftFlag = 1u << 2;

        public static int Flatten(int x, int y, int z, int3 dimensions)
        {
            return x + dimensions.x * (z + dimensions.z * y);
        }

        public static bool IsSolidKind(byte kind)
        {
            return (kind & CellMask) != Empty;
        }

        public static bool IsRoomLike(byte kind)
        {
            byte cell = (byte)(kind & CellMask);
            return cell == Room || cell == Hatch || cell == Datapad || cell == SealedDoor || cell == Window || cell == Generator;
        }
    }

    internal static class MarauderOutpostHash
    {
        public static ulong LcgHash64(ulong value)
        {
            value = unchecked(value * 6364136223846793005UL + 1442695040888963407UL);
            value ^= value >> 33;
            value = unchecked(value * 0xff51afd7ed558ccdUL);
            value ^= value >> 33;
            value = unchecked(value * 0xc4ceb9fe1a85ec53UL);
            value ^= value >> 33;
            return value;
        }

        public static uint LcgHash(ulong value)
        {
            ulong mixed = LcgHash64(value);
            return (uint)(mixed ^ (mixed >> 32));
        }

        public static uint Cell(uint seed, int x, int y, int z)
        {
            uint h = seed;
            h ^= (uint)x * 0x9E3779B9u;
            h ^= (uint)y * 0x85EBCA6Bu;
            h ^= (uint)z * 0xC2B2AE35u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    internal struct OutpostTelemetryEntry
    {
        public uint Frame;
        public uint Flags;
        public ulong SectorHash;
        public uint Seed;
        public uint GenerationSequence;
        public float3 OriginMeters;
        public int3 Dimensions;
        public int MatrixCount;
        public int InteractableCount;
        public int SolidCellCount;
        public int SupportCount;
        public float OutpostAge01;
        public uint ShiftFrameId;
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true)]
    internal struct MarauderOutpostSolveJob : IJob
    {
        public NativeArray<byte> WfcGrid;
        public int3 Dimensions;
        public uint Seed;
        public byte LowTier;

        public void Execute()
        {
            int cellCount = Dimensions.x * Dimensions.y * Dimensions.z;
            for (int i = 0; i < WfcGrid.Length; i++)
                WfcGrid[i] = 0;

            for (int y = 0; y < Dimensions.y; y++)
            {
                for (int z = 0; z < Dimensions.z; z++)
                {
                    for (int x = 0; x < Dimensions.x; x++)
                    {
                        int index = MarauderOutpostConstants.Flatten(x, y, z, Dimensions);
                        if (index >= cellCount)
                            continue;

                        WfcGrid[index] = ResolveInitialKind(x, y, z);
                    }
                }
            }

            EnforceFloorSupport();
            ResolveAdjacencyMasks();
        }

        private byte ResolveInitialKind(int x, int y, int z)
        {
            int centerX = Dimensions.x >> 1;
            int centerZ = Dimensions.z >> 1;
            uint hash = MarauderOutpostHash.Cell(Seed, x, y, z);
            bool spine = x == centerX || z == centerZ;
            bool edge = x == 0 || z == 0 || x == Dimensions.x - 1 || z == Dimensions.z - 1;

            if (y == 0)
            {
                if (x == centerX && z == centerZ)
                    return MarauderOutpostConstants.Generator;

                if (edge && spine)
                    return MarauderOutpostConstants.SealedDoor;

                if (spine)
                    return MarauderOutpostConstants.Corridor;

                if ((hash & 0x0Fu) < (LowTier != 0 ? 5u : 8u))
                    return (hash & 0x30u) == 0x10u ? MarauderOutpostConstants.Datapad : MarauderOutpostConstants.Room;

                return MarauderOutpostConstants.Empty;
            }

            int below = MarauderOutpostConstants.Flatten(x, y - 1, z, Dimensions);
            if (below < 0 || below >= WfcGrid.Length || !MarauderOutpostConstants.IsRoomLike(WfcGrid[below]))
                return MarauderOutpostConstants.Empty;

            if (spine && y == 1 && (hash & 0x07u) < 5u)
                return MarauderOutpostConstants.Hatch;

            uint threshold = LowTier != 0 ? 1u : (uint)math.max(1, 5 - y);
            if ((hash & 0x0Fu) < threshold)
                return (hash & 0x20u) != 0 ? MarauderOutpostConstants.Window : MarauderOutpostConstants.Room;

            return MarauderOutpostConstants.Empty;
        }

        private void EnforceFloorSupport()
        {
            for (int y = 1; y < Dimensions.y; y++)
            {
                for (int z = 0; z < Dimensions.z; z++)
                {
                    for (int x = 0; x < Dimensions.x; x++)
                    {
                        int index = MarauderOutpostConstants.Flatten(x, y, z, Dimensions);
                        if (!MarauderOutpostConstants.IsSolidKind(WfcGrid[index]))
                            continue;

                        int below = MarauderOutpostConstants.Flatten(x, y - 1, z, Dimensions);
                        if (!MarauderOutpostConstants.IsSolidKind(WfcGrid[below]))
                            WfcGrid[index] = MarauderOutpostConstants.Empty;
                    }
                }
            }
        }

        private void ResolveAdjacencyMasks()
        {
            for (int y = 0; y < Dimensions.y; y++)
            {
                for (int z = 0; z < Dimensions.z; z++)
                {
                    for (int x = 0; x < Dimensions.x; x++)
                    {
                        int index = MarauderOutpostConstants.Flatten(x, y, z, Dimensions);
                        byte kind = (byte)(WfcGrid[index] & MarauderOutpostConstants.CellMask);
                        if (kind == MarauderOutpostConstants.Empty)
                            continue;

                        byte mask = 0;
                        bool touchesRoom = false;
                        AppendNeighborMask(x, y, z + 1, MarauderOutpostConstants.North, ref mask, ref touchesRoom);
                        AppendNeighborMask(x + 1, y, z, MarauderOutpostConstants.East, ref mask, ref touchesRoom);
                        AppendNeighborMask(x, y, z - 1, MarauderOutpostConstants.South, ref mask, ref touchesRoom);
                        AppendNeighborMask(x - 1, y, z, MarauderOutpostConstants.West, ref mask, ref touchesRoom);

                        if (kind == MarauderOutpostConstants.Corridor && !touchesRoom)
                            kind = MarauderOutpostConstants.Room;

                        WfcGrid[index] = (byte)(kind | mask);
                    }
                }
            }
        }

        private void AppendNeighborMask(int x, int y, int z, byte bit, ref byte mask, ref bool touchesRoom)
        {
            if (x < 0 || z < 0 || x >= Dimensions.x || z >= Dimensions.z)
                return;

            int index = MarauderOutpostConstants.Flatten(x, y, z, Dimensions);
            byte neighbor = WfcGrid[index];
            if (!MarauderOutpostConstants.IsSolidKind(neighbor))
                return;

            mask |= bit;
            touchesRoom |= MarauderOutpostConstants.IsRoomLike(neighbor);
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true)]
    internal struct MarauderOutpostMatrixExtractionJob : IJob
    {
        [ReadOnly] public NativeArray<byte> WfcGrid;
        [ReadOnly] public NativeArray<byte> MutableGrid;
        [ReadOnly] public NativeArray<ushort> HeightSamples;
        public NativeArray<float4x4> ShellMatrices;
        public NativeArray<uint> CellTypes;
        public NativeArray<OutpostInteractableSpawn> InteractableSpawns;
        public NativeArray<int> Counters;
        public int3 Dimensions;
        public float3 OriginMeters;
        public float3 TerrainPosition;
        public float3 TerrainSize;
        public int HeightResolution;
        public float CellSizeMeters;
        public float FloorHeightMeters;
        public float StiltClearanceMeters;
        public float OutpostAge01;
        public uint Seed;
        public byte LowTier;

        public void Execute()
        {
            for (int i = 0; i < Counters.Length; i++)
                Counters[i] = 0;

            bool hasHeightmap = HeightSamples.IsCreated &&
                                HeightResolution > 1 &&
                                HeightResolution <= 46340 &&
                                HeightSamples.Length >= HeightResolution * HeightResolution &&
                                TerrainSize.x > 0.001f &&
                                TerrainSize.y > 0.001f &&
                                TerrainSize.z > 0.001f;
            float invTerrainSizeX = hasHeightmap ? math.rcp(TerrainSize.x) : 0f;
            float invTerrainSizeZ = hasHeightmap ? math.rcp(TerrainSize.z) : 0f;
            float heightScale = hasHeightmap ? TerrainSize.y * MarauderOutpostConstants.HeightUShortToUnit : 0f;
            float halfWidth = (Dimensions.x - 1) * CellSizeMeters * 0.5f;
            float halfDepth = (Dimensions.z - 1) * CellSizeMeters * 0.5f;
            float baseTerrain = SampleHeight(OriginMeters, OriginMeters.y - StiltClearanceMeters, hasHeightmap, invTerrainSizeX, invTerrainSizeZ, heightScale);
            float baseFloorY = baseTerrain + StiltClearanceMeters;

            for (int y = 0; y < Dimensions.y; y++)
            {
                for (int z = 0; z < Dimensions.z; z++)
                {
                    for (int x = 0; x < Dimensions.x; x++)
                    {
                        int cellIndex = MarauderOutpostConstants.Flatten(x, y, z, Dimensions);
                        byte packed = WfcGrid[cellIndex];
                        byte kind = (byte)(packed & MarauderOutpostConstants.CellMask);
                        if (kind == MarauderOutpostConstants.Empty)
                            continue;

                        byte mutable = (byte)(MutableGrid[cellIndex] & MarauderOutpostConstants.MutableStateMask);

                        float3 position = new float3(
                            OriginMeters.x + x * CellSizeMeters - halfWidth,
                            baseFloorY + y * FloorHeightMeters,
                            OriginMeters.z + z * CellSizeMeters - halfDepth);

                        float terrainHeight = SampleHeight(position, baseTerrain, hasHeightmap, invTerrainSizeX, invTerrainSizeZ, heightScale);
                        if (y == 0)
                            AppendSupportPillars(position, terrainHeight, baseFloorY);

                        AppendShellMatrix(position, x, z, kind, packed, mutable);
                        if (kind == MarauderOutpostConstants.Datapad || kind == MarauderOutpostConstants.SealedDoor)
                            AppendInteractable(position, x, z, cellIndex, kind, packed, mutable);
                    }
                }
            }

            Counters[4] = hasHeightmap ? 0 : 1;
        }

        private void AppendShellMatrix(float3 position, int x, int z, byte kind, byte packed, byte mutable)
        {
            int index = Counters[0];
            if (index >= ShellMatrices.Length)
                return;

            float3 scale = ResolveShellScale(kind);
            quaternion rotation = ResolveShellRotation(kind, packed, x, z);
            ShellMatrices[index] = float4x4.TRS(position, rotation, scale);
            CellTypes[index] = (uint)kind |
                               ((uint)packed << 8) |
                               ((uint)mutable << 16) |
                               ((uint)math.round(math.saturate(OutpostAge01) * 255f) << 24);
            Counters[0] = index + 1;
            Counters[2] = Counters[2] + 1;
        }

        private void AppendSupportPillars(float3 cellPosition, float terrainHeight, float baseFloorY)
        {
            float supportHeight = baseFloorY - terrainHeight;
            if (supportHeight <= 1.1f)
                return;

            int index = Counters[0];
            if (index >= ShellMatrices.Length)
                return;

            float clampedHeight = math.min(supportHeight, LowTier != 0 ? 10f : 20f);
            float3 position = new float3(cellPosition.x, terrainHeight + clampedHeight * 0.5f, cellPosition.z);
            float width = math.max(0.18f, CellSizeMeters * 0.16f);
            ShellMatrices[index] = float4x4.TRS(position, quaternion.identity, new float3(width, clampedHeight, width));
            CellTypes[index] = MarauderOutpostConstants.Pillar |
                               ((uint)math.round(math.saturate(OutpostAge01) * 255f) << 24);
            Counters[0] = index + 1;
            Counters[3] = Counters[3] + 1;
        }

        private void AppendInteractable(float3 position, int x, int z, int cellIndex, byte kind, byte packed, byte mutable)
        {
            int index = Counters[1];
            if (index >= InteractableSpawns.Length)
                return;

            InteractableSpawns[index] = new OutpostInteractableSpawn
            {
                PositionMeters = position + new float3(0f, 0.1f, 0f),
                RotationYRadians = ResolveFacingRadians(packed, x, z),
                CellIndex = (ushort)math.min(cellIndex, ushort.MaxValue),
                Kind = kind,
                Flags = (byte)((packed & ~MarauderOutpostConstants.MutableStateMask) | mutable)
            };
            Counters[1] = index + 1;
        }

        private float3 ResolveShellScale(byte kind)
        {
            float xy = CellSizeMeters * 0.92f;
            float height = FloorHeightMeters * 0.82f;
            switch (kind)
            {
                case MarauderOutpostConstants.Corridor:
                    return new float3(CellSizeMeters * 0.62f, height * 0.82f, CellSizeMeters * 0.62f);
                case MarauderOutpostConstants.Window:
                    return new float3(xy * 0.82f, height * 0.72f, xy * 0.82f);
                case MarauderOutpostConstants.SealedDoor:
                    return new float3(xy * 0.62f, height * 0.95f, xy * 0.28f);
                case MarauderOutpostConstants.Datapad:
                    return new float3(xy * 0.74f, height * 0.7f, xy * 0.74f);
                case MarauderOutpostConstants.Generator:
                    return new float3(xy * 1.08f, height * 0.92f, xy * 1.08f);
                default:
                    return new float3(xy, height, xy);
            }
        }

        private quaternion ResolveShellRotation(byte kind, byte packed, int x, int z)
        {
            return kind == MarauderOutpostConstants.SealedDoor
                ? quaternion.RotateY(ResolveFacingRadians(packed, x, z))
                : quaternion.identity;
        }

        private float ResolveFacingRadians(byte packed, int x, int z)
        {
            if (z <= 0)
                return math.PI;
            if (x >= Dimensions.x - 1)
                return math.PI * 0.5f;
            if (z >= Dimensions.z - 1)
                return 0f;
            if (x <= 0)
                return math.PI * 1.5f;
            if ((packed & MarauderOutpostConstants.North) == 0)
                return 0f;
            if ((packed & MarauderOutpostConstants.East) == 0)
                return math.PI * 0.5f;
            if ((packed & MarauderOutpostConstants.South) == 0)
                return math.PI;
            if ((packed & MarauderOutpostConstants.West) == 0)
                return math.PI * 1.5f;
            return 0f;
        }

        private float SampleHeight(float3 position, float fallbackHeight, bool hasHeightmap, float invTerrainSizeX, float invTerrainSizeZ, float heightScale)
        {
            if (!hasHeightmap)
                return fallbackHeight;

            float u = math.saturate((position.x - TerrainPosition.x) * invTerrainSizeX);
            float v = math.saturate((position.z - TerrainPosition.z) * invTerrainSizeZ);
            int maxPixel = HeightResolution - 1;
            int ix = math.clamp((int)math.round(u * maxPixel), 0, maxPixel);
            int iz = math.clamp((int)math.round(v * maxPixel), 0, maxPixel);
            int sampleIndex = iz * HeightResolution + ix;
            ushort sample = HeightSamples[sampleIndex];
            return TerrainPosition.y + sample * heightScale;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true)]
    internal struct MarauderOutpostAupShiftJob : IJobParallelFor
    {
        public NativeArray<float4x4> ShellMatrices;
        public float3 ShiftMeters;

        public void Execute(int index)
        {
            float4x4 matrix = ShellMatrices[index];
            matrix.c3.x -= ShiftMeters.x;
            matrix.c3.y -= ShiftMeters.y;
            matrix.c3.z -= ShiftMeters.z;
            ShellMatrices[index] = matrix;
        }
    }
}
