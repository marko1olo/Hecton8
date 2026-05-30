using System;
using System.Runtime.CompilerServices;
using Hecton8.Core.Memory.Layout;
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
            if (!IsBinaryBlittableSafe<T>())
                return RejectUnsafeBinaryBlitType<T>();

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
            if (!IsBinaryBlittableSafe<T>())
                return RejectUnsafeBinaryBlitType<T>();

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
            int destinationBytes = destination.Length - destinationByteOffset;
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, destinationBytes, sourcePtr, byteCount))
                return false;

            bytesWritten = byteCount;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PickleUnmanaged<T>(
            in T value,
            NativeArray<byte> destination,
            int destinationByteOffset,
            out int bytesWritten) where T : unmanaged
        {
            return WriteUnmanaged(in value, destination, destinationByteOffset, out bytesWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadUnmanaged<T>(
            NativeArray<byte> source,
            int sourceByteOffset,
            out T value) where T : unmanaged
        {
            value = default;
            if (!IsBinaryBlittableSafe<T>())
                return RejectUnsafeBinaryBlitType<T>();

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
            return UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, byteCount, sourcePtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UnpickleUnmanaged<T>(
            NativeArray<byte> source,
            int sourceByteOffset,
            out T value) where T : unmanaged
        {
            return ReadUnmanaged(source, sourceByteOffset, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MemClear<T>(NativeArray<T> buffer) where T : unmanaged
        {
            if (!buffer.IsCreated)
                return false;

            long byteCount = (long)UnsafeUtility.SizeOf<T>() * buffer.Length;
            if (byteCount <= 0L)
                return true;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            UnsafeUtility.MemClear(ptr, byteCount);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MemCpyStride(
            void* source,
            int sourceStrideBytes,
            void* destination,
            int destinationStrideBytes,
            int elementSizeBytes,
            int elementCount)
        {
            if (elementCount < 0 || elementSizeBytes < 0 || sourceStrideBytes < elementSizeBytes || destinationStrideBytes < elementSizeBytes)
                return false;

            if (elementCount == 0 || elementSizeBytes == 0)
                return true;

            if (source == null || destination == null)
                return false;

            byte* src = (byte*)source;
            byte* dst = (byte*)destination;
            for (int i = 0; i < elementCount; i++)
            {
                if (!UnsafeMemoryCopyGuard.SafeCopy(dst, elementSizeBytes, src, elementSizeBytes))
                    return false;

                src += sourceStrideBytes;
                dst += destinationStrideBytes;
            }

            return true;
        }

        /// <summary>
        /// Forces the one-time attribute cache for a binary DTO during cold boot.
        /// </summary>
        /// <typeparam name="T">Blittable DTO type.</typeparam>
        /// <returns>True when the DTO is explicitly marked as binary-blit safe.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PrewarmBinaryBlittableSafety<T>() where T : unmanaged
        {
            return BinaryBlittableTypeCache<T>.IsSafe;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBinaryBlittableSafe<T>() where T : unmanaged
        {
            return BinaryBlittableTypeCache<T>.IsSafe;
        }

        private static bool RejectUnsafeBinaryBlitType<T>() where T : unmanaged
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new FatalMemoryCorruptionException(
                "[MemoryInquisitor] Rejected unmanaged blit for a type missing BinaryBlittableSafeAttribute.");
#else
            return false;
#endif
        }

        private static class BinaryBlittableTypeCache<T> where T : unmanaged
        {
            internal static readonly bool IsSafe =
                typeof(T).IsDefined(typeof(BinaryBlittableSafeAttribute), false);
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
