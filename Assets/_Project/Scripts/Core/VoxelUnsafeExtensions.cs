using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    public static unsafe class VoxelUnsafeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* GetUnsafePtr(this NativeArray<byte> voxels)
        {
            return voxels.IsCreated ? (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(voxels) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* GetUnsafeReadOnlyPtr(this NativeArray<byte> voxels)
        {
            return voxels.IsCreated ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(voxels) : null;
        }
    }
}
