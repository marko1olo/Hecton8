using System;

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
        public long chunkX;
        public long chunkY;
        public long chunkZ;
        public float voxelSize;
        public int cellCount;
        public VoxelDeltaCellDTO[] cells;

        public void EnsureCapacity(int requiredCellCount)
        {
            if (requiredCellCount <= 0)
            {
                if (cells == null)
                    cells = Array.Empty<VoxelDeltaCellDTO>();

                cellCount = 0;
                return;
            }

            if (cells == null || cells.Length < requiredCellCount)
            {
                // COLD ALLOC: VoxelDeltaCellDTO[requiredCellCount] — voxel delta snapshot chunk payload — owner: VoxelDeltaChunkDTO
                cells = new VoxelDeltaCellDTO[requiredCellCount];
            }
        }
    }

    [Serializable]
    public struct VoxelDeltaPersistenceDTO
    {
        public int chunkCount;
        public int totalCellCount;
        public VoxelDeltaChunkDTO[] chunks;

        public static VoxelDeltaPersistenceDTO CreateDefault()
        {
            return new VoxelDeltaPersistenceDTO
            {
                chunkCount = 0,
                totalCellCount = 0,
                chunks = Array.Empty<VoxelDeltaChunkDTO>()
            };
        }

        public void EnsureCapacity(int requiredChunkCount)
        {
            if (requiredChunkCount <= 0)
            {
                if (chunks == null)
                    chunks = Array.Empty<VoxelDeltaChunkDTO>();

                chunkCount = 0;
                totalCellCount = 0;
                return;
            }

            if (chunks == null || chunks.Length < requiredChunkCount)
            {
                // COLD ALLOC: VoxelDeltaChunkDTO[requiredChunkCount] — voxel delta persistence chunk registry — owner: VoxelDeltaPersistenceDTO
                chunks = new VoxelDeltaChunkDTO[requiredChunkCount];
            }
        }
    }
#pragma warning restore CS0649
}
