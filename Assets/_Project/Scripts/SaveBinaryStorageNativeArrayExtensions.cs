using System;
using System.IO.MemoryMappedFiles;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.SaveSystem
{
    public static unsafe class SaveBinaryStorageNativeArrayExtensions
    {
        public static bool WriteNativeArrayToSector<T>(
            this MemoryMappedViewAccessor accessor,
            long sectorByteOffset,
            NativeArray<T> source,
            out string error)
            where T : unmanaged
        {
            error = string.Empty;
            if (accessor == null)
            {
                error = "MMF accessor is null.";
                return false;
            }

            if (!source.IsCreated)
            {
                error = "NativeArray source is not initialized.";
                return false;
            }

            long byteCountLong = (long)source.Length * UnsafeUtility.SizeOf<T>();
            if (byteCountLong <= 0L)
                return true;

            if (sectorByteOffset < 0L ||
                sectorByteOffset > accessor.Capacity ||
                byteCountLong > accessor.Capacity - sectorByteOffset ||
                byteCountLong > int.MaxValue)
            {
                error = "NativeArray sector write exceeds mapped view bounds.";
                return false;
            }

            byte* mappedPointer = null;
            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedPointer);
                byte* destination = mappedPointer + accessor.PointerOffset + sectorByteOffset;
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                UnsafeUtility.MemCpy(destination, sourcePtr, byteCountLong);
                return true;
            }
            catch (Exception ex)
            {
                error = $"NativeArray sector write failed: {ex.Message}";
                return false;
            }
            finally
            {
                if (mappedPointer != null)
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }
}
