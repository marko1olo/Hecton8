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
        private const int MaxArenaAlignment = 4096;

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

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorShutdownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.quitting -= Shutdown;
            UnityEditor.EditorApplication.quitting += Shutdown;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange stateChange)
        {
            if (stateChange == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                stateChange == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Shutdown();
            }
        }
#endif

        public static void Initialize(int capacityBytes = DefaultArenaBytes)
        {
            if (_basePtr != null)
                return;

            _capacityBytes = Math.Max(1024, capacityBytes);
            _basePtr = (byte*)UnsafeUtility.MallocTracked(_capacityBytes, 16, Allocator.Persistent, 1);
            NativeMemorySentinel.RegisterPointer(
                _basePtr,
                _capacityBytes,
                nameof(NativeArenaAllocator),
                nameof(_basePtr),
                NativeAllocationLifetime.TransientArena);
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
            if (_basePtr == null || _capacityBytes <= 0)
                return false;

            int safeAlignment = NormalizeAlignment(alignment);
            if (safeAlignment <= 0)
                return false;

            long baseAddress = (long)_basePtr;
            long rawAddress = baseAddress + _cursorBytes;
            long alignedAddress = (rawAddress + (safeAlignment - 1L)) & ~(safeAlignment - 1L);
            int alignedOffset = (int)(alignedAddress - (long)_basePtr);
            int nextCursor = alignedOffset + byteCount;
            if (alignedOffset < 0 || nextCursor < alignedOffset || nextCursor > _capacityBytes)
                return false;

            ptr = _basePtr + alignedOffset;
            _cursorBytes = nextCursor;
            return true;
        }

        private static int NormalizeAlignment(int alignment)
        {
            if (alignment <= 1)
                return 1;

            if (alignment > MaxArenaAlignment)
                return 0;

            int safeAlignment = 1;
            while (safeAlignment < alignment)
                safeAlignment <<= 1;

            return safeAlignment;
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

            NativeMemorySentinel.UnregisterPointer(_basePtr);
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
