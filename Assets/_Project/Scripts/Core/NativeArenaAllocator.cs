using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed-capacity tracked native arena for zero-GC transient scratch lanes that must not use Allocator.TempJob in runtime hot paths.
    /// </summary>
    internal static unsafe class NativeArenaAllocator
    {
        private const int DefaultArenaBytes = 256 * 1024;

        private static readonly ProfilerMarker _resetProfilerMarker = new ProfilerMarker("H8.Core.NativeArena.Reset");

        private static byte* _basePtr;
        private static int _capacityBytes;
        private static int _cursorBytes;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static AtomicSafetyHandle _sharedSafetyHandle;
        private static bool _sharedSafetyHandleCreated;
#endif

        public static int CapacityBytes => _capacityBytes;
        public static int UsedBytes => _cursorBytes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        public static void Initialize(int capacityBytes = DefaultArenaBytes)
        {
            if (_basePtr != null)
                return;

            _capacityBytes = Math.Max(1024, capacityBytes);
            _basePtr = (byte*)UnsafeUtility.MallocTracked(_capacityBytes, 16, Allocator.Persistent, 1);
            UnsafeUtility.MemClear(_basePtr, _capacityBytes);
            _cursorBytes = 0;
            RecreateSafetyHandle();
        }

        public static NativeArray<T> Allocate<T>(int count) where T : unmanaged
        {
            if (count <= 0)
                return default;

            long totalBytes = (long)UnsafeUtility.SizeOf<T>() * count;
            if (totalBytes <= 0 || totalBytes > int.MaxValue)
                return default;

            if (!TryAllocateBytes((int)totalBytes, UnsafeUtility.AlignOf<T>(), out byte* ptr))
                return default;

            UnsafeUtility.MemClear(ptr, totalBytes);
            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, count, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, _sharedSafetyHandle);
#endif
            return array;
        }

        internal static bool TryAllocateBytes(int byteCount, int alignment, out byte* ptr)
        {
            ptr = null;
            if (byteCount <= 0)
                return false;

            Initialize();

            int safeAlignment = Math.Max(1, alignment);
            long alignedAddress = ((long)_basePtr + _cursorBytes + (safeAlignment - 1)) & ~((long)safeAlignment - 1);
            int alignedOffset = (int)(alignedAddress - (long)_basePtr);
            int nextCursor = alignedOffset + byteCount;
            if (nextCursor > _capacityBytes)
                return false;

            ptr = _basePtr + alignedOffset;
            _cursorBytes = nextCursor;
            return true;
        }

        public static void Reset()
        {
            if (_basePtr == null)
                return;

            using (_resetProfilerMarker.Auto())
            {
                _cursorBytes = 0;
                RecreateSafetyHandle();
            }
        }

        public static void Shutdown()
        {
            if (_basePtr == null)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_sharedSafetyHandleCreated)
            {
                AtomicSafetyHandle.Release(_sharedSafetyHandle);
                _sharedSafetyHandleCreated = false;
            }
#endif

            UnsafeUtility.FreeTracked(_basePtr, Allocator.Persistent);
            _basePtr = null;
            _capacityBytes = 0;
            _cursorBytes = 0;
        }

        private static void RecreateSafetyHandle()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_sharedSafetyHandleCreated)
                AtomicSafetyHandle.Release(_sharedSafetyHandle);

            _sharedSafetyHandle = AtomicSafetyHandle.Create();
            _sharedSafetyHandleCreated = true;
#endif
        }
    }
}
