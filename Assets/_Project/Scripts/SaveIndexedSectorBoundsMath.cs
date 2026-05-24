using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Hecton8.SaveSystem
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct IndexedSectorBoundsProbe
    {
        public long ByteOffset;
        public int CompressedSize;
        public long MinimumByteOffset;
        public long FileLength;
        public byte ExpectedValid;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct ValidateIndexedSectorBoundsProbeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<IndexedSectorBoundsProbe> Probes;
        [WriteOnly] public NativeArray<byte> Results;

        public void Execute(int index)
        {
            IndexedSectorBoundsProbe probe = Probes[index];
            bool valid = SaveIndexedSectorBoundsMath.IsIndexedSectorBlockWithinFileBounds(
                probe.ByteOffset,
                probe.CompressedSize,
                probe.MinimumByteOffset,
                probe.FileLength);
            Results[index] = valid == (probe.ExpectedValid != 0) ? (byte)1 : (byte)0;
        }
    }

    internal static class SaveIndexedSectorBoundsMath
    {
        internal static bool IsIndexedSectorBlockWithinFileBounds(
            long byteOffset,
            int compressedSize,
            long minimumByteOffset,
            long fileLength)
        {
            if (compressedSize <= 0 ||
                minimumByteOffset < 0L ||
                fileLength < minimumByteOffset ||
                byteOffset < minimumByteOffset)
            {
                return false;
            }

            return byteOffset <= fileLength - compressedSize;
        }
    }
}
