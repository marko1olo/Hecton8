using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TerrainChunkGeneratedSignal
    {
        [FieldOffset(0)] public int ChunkX;
        [FieldOffset(4)] public int ChunkZ;
        [FieldOffset(8)] public uint TerrainEntityHash;
        [FieldOffset(12)] public int HeightmapResolution;
        [FieldOffset(16)] public int CacheRevision;
        [FieldOffset(20)] public float3 TerrainPosition;
        [FieldOffset(32)] public float3 TerrainSize;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public byte Flags;
        [FieldOffset(49)] public byte Reserved0;
        [FieldOffset(50)] public ushort Reserved1;
        [FieldOffset(52)] public uint Reserved2;
        [FieldOffset(56)] private ulong _pad0;

        public static bool IsValid(in TerrainChunkGeneratedSignal signal)
        {
            return signal.TerrainEntityHash != 0u &&
                signal.HeightmapResolution > 1 &&
                math.all(math.isfinite(signal.TerrainPosition)) &&
                math.all(math.isfinite(signal.TerrainSize)) &&
                signal.TerrainSize.x > 0f &&
                signal.TerrainSize.z > 0f;
        }
    }
}
