using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed unmanaged bump arena for frame-transient scratch buffers.
    /// </summary>
    public static unsafe class HectonArenaAllocator
    {
        public const int DefaultArenaBytes = 16 * 1024 * 1024;

        private const int CacheLineAlignment = 64;
        private const int MaxArenaAlignment = 4096;
        private const string BudgetOwner = nameof(HectonArenaAllocator);

        private static readonly ProfilerMarker _resetProfilerMarker = new ProfilerMarker("H8.Core.HectonArena.Reset");

        private static byte* _basePtr;
        private static int _capacityBytes;
        private static int _cursorBytes;
        private static int _initializing;
        private static int _sentinelId;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static AtomicSafetyHandle _sharedSafetyHandle;
        private static bool _sharedSafetyHandleCreated;
#endif

        public static bool IsCreated => _basePtr != null;
        public static int CapacityBytes => Volatile.Read(ref _capacityBytes);
        public static int UsedBytes => Volatile.Read(ref _cursorBytes);

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

            if (Interlocked.CompareExchange(ref _initializing, 1, 0) != 0)
            {
                SpinWait spinWait = default;
                while (Volatile.Read(ref _initializing) != 0 && _basePtr == null)
                    spinWait.SpinOnce();

                return;
            }

            try
            {
                if (_basePtr != null)
                    return;

                int safeCapacity = capacityBytes < CacheLineAlignment ? DefaultArenaBytes : capacityBytes;
                _capacityBytes = safeCapacity;
                _basePtr = (byte*)UnsafeUtility.Malloc(_capacityBytes, CacheLineAlignment, Allocator.Persistent);
                UnsafeUtility.MemClear(_basePtr, _capacityBytes);
                Interlocked.Exchange(ref _cursorBytes, 0);

                _sentinelId = NativeMemorySentinel.RegisterPointer(
                    _basePtr,
                    _capacityBytes,
                    nameof(HectonArenaAllocator),
                    nameof(_basePtr),
                    NativeAllocationLifetime.TransientArena);

                MemoryBudgetTracker.Register(BudgetOwner, _capacityBytes, _capacityBytes);
                RecreateSafetyHandle();
            }
            finally
            {
                Volatile.Write(ref _initializing, 0);
            }
        }

        public static NativeArray<T> Allocate<T>(int count, bool clearMemory = true) where T : unmanaged
        {
            if (count <= 0)
                return default;

            long totalBytes = (long)UnsafeUtility.SizeOf<T>() * count;
            if (totalBytes <= 0L || totalBytes > int.MaxValue)
                return default;

            if (!TryAllocateBytes((int)totalBytes, UnsafeUtility.AlignOf<T>(), out byte* ptr))
                return default;

            if (clearMemory)
                UnsafeUtility.MemClear(ptr, totalBytes);

            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, count, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, _sharedSafetyHandle);
#endif
            return array;
        }

        public static bool TryAllocateSpan<T>(int count, bool clearMemory, out Span<T> span) where T : unmanaged
        {
            span = default;
            if (count <= 0)
                return false;

            long totalBytes = (long)UnsafeUtility.SizeOf<T>() * count;
            if (totalBytes <= 0L || totalBytes > int.MaxValue)
                return false;

            if (!TryAllocateBytes((int)totalBytes, UnsafeUtility.AlignOf<T>(), out byte* ptr))
                return false;

            if (clearMemory)
                UnsafeUtility.MemClear(ptr, totalBytes);

            span = new Span<T>(ptr, count);
            return true;
        }

        public static bool TryAllocateCharSpan(int charCount, out Span<char> span)
        {
            return TryAllocateSpan(charCount, true, out span);
        }

        public static bool TryAllocateBytes(int byteCount, int alignment, out byte* ptr)
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

            while (true)
            {
                int observedCursor = Volatile.Read(ref _cursorBytes);
                long rawAddress = (long)_basePtr + observedCursor;
                long alignedAddress = (rawAddress + (safeAlignment - 1L)) & ~(safeAlignment - 1L);
                int alignedOffset = (int)(alignedAddress - (long)_basePtr);
                int nextCursor = alignedOffset + byteCount;

                if (alignedOffset < 0 || nextCursor < alignedOffset || nextCursor > _capacityBytes)
                    return false;

                if (Interlocked.CompareExchange(ref _cursorBytes, nextCursor, observedCursor) != observedCursor)
                    continue;

                ptr = _basePtr + alignedOffset;
                return true;
            }
        }

        public static void Reset()
        {
            if (_basePtr == null)
                return;

            using (_resetProfilerMarker.Auto())
            {
                Interlocked.Exchange(ref _cursorBytes, 0);
                RecreateSafetyHandle();
            }
        }

        public static void Shutdown()
        {
            if (_basePtr == null)
                return;

            ReleaseSafetyHandle();
            if (_sentinelId != 0)
            {
                NativeMemorySentinel.Unregister(_sentinelId);
                _sentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            UnsafeUtility.Free(_basePtr, Allocator.Persistent);
            _basePtr = null;
            _capacityBytes = 0;
            Interlocked.Exchange(ref _cursorBytes, 0);
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

        private static void RecreateSafetyHandle()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ReleaseSafetyHandle();
            _sharedSafetyHandle = AtomicSafetyHandle.Create();
            _sharedSafetyHandleCreated = true;
#endif
        }

        private static void ReleaseSafetyHandle()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!_sharedSafetyHandleCreated)
                return;

            AtomicSafetyHandle.Release(_sharedSafetyHandle);
            _sharedSafetyHandleCreated = false;
#endif
        }
    }
}
