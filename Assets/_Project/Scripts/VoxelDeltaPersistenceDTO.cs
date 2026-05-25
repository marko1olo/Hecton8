using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory.Layout;
using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    internal static class VoxelDeltaPersistenceLayout
    {
        public const int VoxelDeltaCellDTOStrideBytes = 24;
        public const int VoxelCarvingOperationDTOStrideBytes = 24;
    }

#pragma warning disable CS0649
    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = VoxelDeltaPersistenceLayout.VoxelDeltaCellDTOStrideBytes)]
    public struct VoxelDeltaCellDTO
    {
        [FieldOffset(0)]
        public ulong universeKey;

        [FieldOffset(8)]
        public float sdfValue;

        [FieldOffset(12)]
        public byte materialId;

        [FieldOffset(13)]
        public byte flags;

        [FieldOffset(14)]
        public ushort metadata;

        [FieldOffset(16)]
        public uint reserved;

        [FieldOffset(20)]
        public uint _pad0;
    }

    [Serializable]
    public struct VoxelDeltaChunkDTO
    {
        public const int ChunkResolution = 32;
        public const int CellCount = ChunkResolution * ChunkResolution * ChunkResolution;
        public const int DirtyMaskWordCount = CellCount / 32;
        public const byte StorageDense = 0;
        public const byte StorageUniformSdfRle = 1 << 0;
        public const int UniformSdfRlePayloadBytes = sizeof(ushort);

        public long chunkX;
        public long chunkY;
        public long chunkZ;
        public float voxelSize;
        public int cellCount;
        public byte storageFlags;
        public byte reservedStorage;
        public ushort uniformSdfValueBits;
        public uint[] dirtyMaskWords;
        public ushort[] sdfValueBits;
        public byte[] materialIds;
        public byte[] cellFlags;
        public VoxelDeltaCellDTO[] cells;

        public void EnsureCapacity(int requiredCellCount)
        {
            if (requiredCellCount <= 0)
            {
                dirtyMaskWords = Array.Empty<uint>();
                sdfValueBits = Array.Empty<ushort>();
                materialIds = Array.Empty<byte>();
                cellFlags = Array.Empty<byte>();
                cells = Array.Empty<VoxelDeltaCellDTO>();
                cellCount = 0;
                storageFlags = StorageDense;
                reservedStorage = 0;
                uniformSdfValueBits = 0;
                return;
            }

            storageFlags = StorageDense;
            reservedStorage = 0;
            uniformSdfValueBits = 0;

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
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = VoxelDeltaPersistenceLayout.VoxelCarvingOperationDTOStrideBytes)]
    public struct VoxelCarvingOperationDTO
    {
        [FieldOffset(0)]
        public float3 localPosition;

        [FieldOffset(12)]
        public float radius;

        [FieldOffset(16)]
        public VoxelCarvingOperationKind operation;

        [FieldOffset(17)]
        public byte materialId;

        [FieldOffset(18)]
        public ushort flags;

        [FieldOffset(20)]
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
