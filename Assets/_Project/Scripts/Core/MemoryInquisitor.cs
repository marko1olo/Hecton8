using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    /// <summary>
    /// Guarded bulk-copy utilities for native buffers.
    /// </summary>
    public static unsafe class MemoryInquisitor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Blit<T>(NativeArray<T> source, NativeArray<T> destination) where T : unmanaged
        {
            if (!source.IsCreated || !destination.IsCreated)
                return false;

            int count = source.Length < destination.Length ? source.Length : destination.Length;
            return Blit(source, 0, destination, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Blit<T>(
            NativeArray<T> source,
            int sourceIndex,
            NativeArray<T> destination,
            int destinationIndex,
            int count) where T : unmanaged
        {
            if (!source.IsCreated || !destination.IsCreated)
                return false;

            if (!IsRangeLegal(source.Length, sourceIndex, destination.Length, destinationIndex, count))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new ArgumentOutOfRangeException(nameof(count), "Native blit range is out of bounds.");
#else
                return false;
#endif
            }

            if (count == 0)
                return true;

            long elementSize = UnsafeUtility.SizeOf<T>();
            long byteCount = elementSize * count;
            byte* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + (sourceIndex * elementSize);
            byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(destination) + (destinationIndex * elementSize);
            long destinationBytes = elementSize * (destination.Length - destinationIndex);

            return UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, destinationBytes, sourcePtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteUnmanaged<T>(
            in T value,
            NativeArray<byte> destination,
            int destinationByteOffset,
            out int bytesWritten) where T : unmanaged
        {
            bytesWritten = 0;
            if (!destination.IsCreated)
                return false;

            int byteCount = UnsafeUtility.SizeOf<T>();
            if (!IsByteRangeLegal(destination.Length, destinationByteOffset, byteCount))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new ArgumentOutOfRangeException(nameof(destinationByteOffset), "Unmanaged pickler destination range is out of bounds.");
#else
                return false;
#endif
            }

            T local = value;
            void* sourcePtr = UnsafeUtility.AddressOf(ref local);
            byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(destination) + destinationByteOffset;
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, byteCount);
            bytesWritten = byteCount;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadUnmanaged<T>(
            NativeArray<byte> source,
            int sourceByteOffset,
            out T value) where T : unmanaged
        {
            value = default;
            if (!source.IsCreated)
                return false;

            int byteCount = UnsafeUtility.SizeOf<T>();
            if (!IsByteRangeLegal(source.Length, sourceByteOffset, byteCount))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new ArgumentOutOfRangeException(nameof(sourceByteOffset), "Unmanaged pickler source range is out of bounds.");
#else
                return false;
#endif
            }

            byte* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + sourceByteOffset;
            void* destinationPtr = UnsafeUtility.AddressOf(ref value);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, byteCount);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRangeLegal(
            int sourceLength,
            int sourceIndex,
            int destinationLength,
            int destinationIndex,
            int count)
        {
            return sourceIndex >= 0 &&
                   destinationIndex >= 0 &&
                   count >= 0 &&
                   sourceIndex <= sourceLength &&
                   destinationIndex <= destinationLength &&
                   count <= sourceLength - sourceIndex &&
                   count <= destinationLength - destinationIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsByteRangeLegal(int bufferLength, int byteOffset, int byteCount)
        {
            return byteOffset >= 0 &&
                   byteCount >= 0 &&
                   byteOffset <= bufferLength &&
                   byteCount <= bufferLength - byteOffset;
        }
    }
}
