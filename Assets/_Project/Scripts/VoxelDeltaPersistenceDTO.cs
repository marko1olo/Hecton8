using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
#pragma warning disable CS0649
    [Serializable]
    public struct VoxelDeltaCellDTO
    {
        public ulong universeKey;
        public float sdfValue;
        public byte materialId;
        public byte flags;
        public ushort metadata;
        public uint reserved;
    }

    [Serializable]
    public struct VoxelDeltaChunkDTO
    {
        public const int ChunkResolution = 32;
        public const int CellCount = ChunkResolution * ChunkResolution * ChunkResolution;
        public const int DirtyMaskWordCount = CellCount / 32;

        public long chunkX;
        public long chunkY;
        public long chunkZ;
        public float voxelSize;
        public int cellCount;
        public uint[] dirtyMaskWords;
        public ushort[] sdfValueBits;
        public byte[] materialIds;
        public byte[] cellFlags;
        public VoxelDeltaCellDTO[] cells;

        public void EnsureCapacity(int requiredCellCount)
        {
            if (requiredCellCount <= 0)
            {
                if (dirtyMaskWords == null)
                    dirtyMaskWords = Array.Empty<uint>();

                if (sdfValueBits == null)
                    sdfValueBits = Array.Empty<ushort>();

                if (materialIds == null)
                    materialIds = Array.Empty<byte>();

                if (cellFlags == null)
                    cellFlags = Array.Empty<byte>();

                if (cells == null)
                    cells = Array.Empty<VoxelDeltaCellDTO>();

                cellCount = 0;
                return;
            }

            if (dirtyMaskWords == null || dirtyMaskWords.Length != DirtyMaskWordCount)
                dirtyMaskWords = new uint[DirtyMaskWordCount];

            if (sdfValueBits == null || sdfValueBits.Length != CellCount)
                sdfValueBits = new ushort[CellCount];

            if (materialIds == null || materialIds.Length != CellCount)
                materialIds = new byte[CellCount];

            if (cellFlags == null || cellFlags.Length != CellCount)
                cellFlags = new byte[CellCount];

            if (cells == null)
                cells = Array.Empty<VoxelDeltaCellDTO>();
        }
    }

    public enum VoxelCarvingOperationKind : byte
    {
        Subtract = 0,
        Add = 1
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
    public struct VoxelCarvingOperationDTO
    {
        public float3 localPosition;
        public float radius;
        public VoxelCarvingOperationKind operation;
        public byte materialId;
        public ushort flags;
        public uint sequence;
    }

    [Serializable]
    public struct VoxelDeltaPersistenceDTO
    {
        public int chunkCount;
        public int totalCellCount;
        public VoxelDeltaChunkDTO[] chunks;
        public int carvingOperationCount;
        public VoxelCarvingOperationDTO[] carvingOperations;

        public static VoxelDeltaPersistenceDTO CreateDefault()
        {
            return new VoxelDeltaPersistenceDTO
            {
                chunkCount = 0,
                totalCellCount = 0,
                chunks = Array.Empty<VoxelDeltaChunkDTO>(),
                carvingOperationCount = 0,
                carvingOperations = Array.Empty<VoxelCarvingOperationDTO>()
            };
        }

        public void EnsureCapacity(int requiredChunkCount)
        {
            if (requiredChunkCount <= 0)
            {
                if (chunks == null)
                    chunks = Array.Empty<VoxelDeltaChunkDTO>();

                if (carvingOperations == null)
                    carvingOperations = Array.Empty<VoxelCarvingOperationDTO>();

                chunkCount = 0;
                totalCellCount = 0;
                carvingOperationCount = 0;
                return;
            }

            if (chunks == null || chunks.Length < requiredChunkCount)
            {
                // COLD ALLOC: VoxelDeltaChunkDTO[requiredChunkCount] - voxel delta persistence chunk registry - owner: VoxelDeltaPersistenceDTO
                chunks = new VoxelDeltaChunkDTO[requiredChunkCount];
            }

            if (carvingOperations == null)
                carvingOperations = Array.Empty<VoxelCarvingOperationDTO>();
        }
    }
#pragma warning restore CS0649
}
