using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct TerrainChunkGeneratedSignal
    {
        public int ChunkX;
        public int ChunkZ;
        public uint TerrainEntityHash;
        public int HeightmapResolution;
        public int CacheRevision;
        public float3 TerrainPosition;
        public float3 TerrainSize;
        public uint Frame;
        public byte Flags;
        public byte Reserved0;
        public ushort Reserved1;
        public uint Reserved2;

        public bool IsValid =>
            TerrainEntityHash != 0u &&
            HeightmapResolution > 1 &&
            math.all(math.isfinite(TerrainPosition)) &&
            math.all(math.isfinite(TerrainSize)) &&
            TerrainSize.x > 0f &&
            TerrainSize.z > 0f;
    }
}
