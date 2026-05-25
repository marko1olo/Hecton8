using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    internal static unsafe class VoxelUnsafeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte* GetUnsafePtr(this NativeArray<byte> voxels)
        {
            return voxels.IsCreated ? (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(voxels) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte* GetUnsafeReadOnlyPtr(this NativeArray<byte> voxels)
        {
            return voxels.IsCreated ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(voxels) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteByteUnchecked(byte* ptr, int index, byte value)
        {
            *(ptr + index) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryWriteByte(byte* ptr, int length, int index, byte value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (ptr == null || (uint)index >= (uint)length)
                throw new ArgumentOutOfRangeException(nameof(index), "Voxel write is out of bounds.");
#endif
            if (ptr == null || (uint)index >= (uint)length)
                return false;

            *(ptr + index) = value;
            return true;
        }
    }
}
