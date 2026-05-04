using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Expected lifetime for persistent native allocations registered with <see cref="NativeMemorySentinel"/>.
    /// </summary>
    public enum NativeAllocationLifetime : byte
    {
        Scene = 0,
        Session = 1,
        Permanent = 2,
        TransientArena = 3,
        Temp = 4,
        TempJob = 5
    }

    /// <summary>
    /// Central cold-path registry for persistent native allocations.
    /// </summary>
    public static unsafe class NativeMemorySentinel
    {
        private const int MaxTrackedAllocations = 1024;
        private const int LongLivedTransientFrameThreshold = 10000;
        private const string CriticalMemoryViolationPrefix = "CRITICAL_MEMORY_VIOLATION";
        private const string MemoryLeakDetectedPrefix = "MEMORY_LEAK_DETECTED";

        private struct NativeAllocationRecord
        {
            public int Id;
            public IntPtr Pointer;
            public long Bytes;
            public int AllocationFrame;
            public NativeAllocationLifetime Lifetime;
            public bool LeakReported;
            public string Owner;
            public string Label;
            public string StackTrace;
        }

        // COLD ALLOC: NativeAllocationRecord[1024] - persistent native allocation ownership registry - owner: NativeMemorySentinel
        private static readonly NativeAllocationRecord[] _records = new NativeAllocationRecord[MaxTrackedAllocations];
        private static int _count;
        private static int _nextId = 1;
        private static long _trackedBytes;
        private static int _sceneLeakViolationCount;
        private static bool _sceneHooksRegistered;

        /// <summary>Active tracked allocation count.</summary>
        public static int ActiveAllocationCount => Volatile.Read(ref _count);

        /// <summary>Tracked persistent native bytes.</summary>
        public static long TrackedBytes => Volatile.Read(ref _trackedBytes);

        /// <summary>Scene lifetime leak violation count reported by the sentinel.</summary>
        public static int SceneLeakViolationCount => Volatile.Read(ref _sceneLeakViolationCount);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ResetForSubsystemReload();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHooksAfterLoad()
        {
            RegisterSceneHooks();
        }

        /// <summary>
        /// Clears all retained sentinel state during subsystem reload.
        /// </summary>
        public static void ResetForSubsystemReload()
        {
            for (int i = 0; i < _count; i++)
                _records[i] = default;

            _count = 0;
            _nextId = 1;
            _trackedBytes = 0L;
            _sceneLeakViolationCount = 0;
            _sceneHooksRegistered = false;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        /// <summary>
        /// Registers a native array allocation.
        /// </summary>
        public static int RegisterNativeArray<T>(
            NativeArray<T> array,
            string owner,
            string label,
            NativeAllocationLifetime lifetime) where T : struct
        {
            if (!array.IsCreated)
                return 0;

            void* pointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            long bytes = (long)UnsafeUtility.SizeOf<T>() * array.Length;
            return RegisterPointer(pointer, bytes, owner, label, lifetime);
        }

        /// <summary>
        /// Registers a native list allocation by capacity.
        /// </summary>
        public static int RegisterNativeList<T>(
            NativeList<T> list,
            string owner,
            string label,
            NativeAllocationLifetime lifetime) where T : unmanaged
        {
            if (!list.IsCreated)
                return 0;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * list.Capacity;
            return RegisterPointer(null, bytes, owner, label, lifetime);
        }

        /// <summary>
        /// Registers a native hash map allocation by capacity. Unity does not expose stable hash map block pointers.
        /// </summary>
        public static int RegisterNativeHashMap<TKey, TValue>(
            NativeHashMap<TKey, TValue> map,
            string owner,
            string label,
            NativeAllocationLifetime lifetime)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return 0;

            long bytes = EstimateNativeHashMapBytes<TKey, TValue>(map.Capacity);
            return RegisterPointer(null, bytes, owner, label, lifetime);
        }

        /// <summary>
        /// Registers a native parallel hash map allocation by capacity. Unity does not expose stable hash map block pointers.
        /// </summary>
        public static int RegisterNativeParallelHashMap<TKey, TValue>(
            NativeParallelHashMap<TKey, TValue> map,
            string owner,
            string label,
            NativeAllocationLifetime lifetime)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return 0;

            long bytes = EstimateNativeHashMapBytes<TKey, TValue>(map.Capacity);
            return RegisterPointer(null, bytes, owner, label, lifetime);
        }

        /// <summary>
        /// Registers a native parallel hash set allocation by capacity. Unity does not expose stable hash set block pointers.
        /// </summary>
        public static int RegisterNativeParallelHashSet<TKey>(
            NativeParallelHashSet<TKey> set,
            string owner,
            string label,
            NativeAllocationLifetime lifetime)
            where TKey : unmanaged, IEquatable<TKey>
        {
            if (!set.IsCreated)
                return 0;

            long bytes = EstimateNativeHashSetBytes<TKey>(set.Capacity);
            return RegisterPointer(null, bytes, owner, label, lifetime);
        }

        /// <summary>
        /// Registers a native parallel multi-hash map allocation by capacity. Unity does not expose stable multi-hash map block pointers.
        /// </summary>
        public static int RegisterNativeParallelMultiHashMap<TKey, TValue>(
            NativeParallelMultiHashMap<TKey, TValue> map,
            string owner,
            string label,
            NativeAllocationLifetime lifetime)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return 0;

            long bytes = EstimateNativeMultiHashMapBytes<TKey, TValue>(map.Capacity);
            return RegisterPointer(null, bytes, owner, label, lifetime);
        }

        /// <summary>
        /// Registers a native queue allocation. Unity does not expose the queue block pointer, so pointer is zero.
        /// </summary>
        public static int RegisterNativeQueue<T>(
            NativeQueue<T> queue,
            int expectedCapacity,
            string owner,
            string label,
            NativeAllocationLifetime lifetime) where T : unmanaged
        {
            if (!queue.IsCreated)
                return 0;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * Math.Max(1, expectedCapacity);
            return RegisterPointer(null, bytes, owner, label, lifetime);
        }

        /// <summary>
        /// Refreshes a tracked native list allocation after an explicit capacity change.
        /// </summary>
        public static void RefreshNativeList<T>(
            NativeList<T> list,
            string owner,
            string label) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * list.Capacity;
            RefreshPointerlessBytes(owner, label, bytes);
        }

        /// <summary>
        /// Refreshes a tracked native hash map allocation after an explicit capacity change.
        /// </summary>
        public static void RefreshNativeHashMap<TKey, TValue>(
            NativeHashMap<TKey, TValue> map,
            string owner,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            RefreshPointerlessBytes(owner, label, EstimateNativeHashMapBytes<TKey, TValue>(map.Capacity));
        }

        /// <summary>
        /// Refreshes a tracked native parallel hash map allocation after an explicit capacity change.
        /// </summary>
        public static void RefreshNativeParallelHashMap<TKey, TValue>(
            NativeParallelHashMap<TKey, TValue> map,
            string owner,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            RefreshPointerlessBytes(owner, label, EstimateNativeHashMapBytes<TKey, TValue>(map.Capacity));
        }

        /// <summary>
        /// Refreshes a tracked native parallel hash set allocation after an explicit capacity change.
        /// </summary>
        public static void RefreshNativeParallelHashSet<TKey>(
            NativeParallelHashSet<TKey> set,
            string owner,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
        {
            if (!set.IsCreated)
                return;

            RefreshPointerlessBytes(owner, label, EstimateNativeHashSetBytes<TKey>(set.Capacity));
        }

        /// <summary>
        /// Refreshes a tracked native parallel multi-hash map allocation after an explicit capacity change.
        /// </summary>
        public static void RefreshNativeParallelMultiHashMap<TKey, TValue>(
            NativeParallelMultiHashMap<TKey, TValue> map,
            string owner,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            RefreshPointerlessBytes(owner, label, EstimateNativeMultiHashMapBytes<TKey, TValue>(map.Capacity));
        }

        /// <summary>
        /// Registers a raw persistent native pointer.
        /// </summary>
        public static int RegisterPointer(
            void* pointer,
            long bytes,
            string owner,
            string label,
            NativeAllocationLifetime lifetime)
        {
            if (bytes <= 0L)
                return 0;

            IntPtr pointerValue = (IntPtr)pointer;
            for (int i = 0; i < _count; i++)
            {
                NativeAllocationRecord existing = _records[i];
                bool pointerMatches = pointerValue != IntPtr.Zero && existing.Pointer == pointerValue;
                bool pointerlessOwnerMatches =
                    pointerValue == IntPtr.Zero &&
                    existing.Pointer == IntPtr.Zero &&
                    string.Equals(existing.Owner, owner, StringComparison.Ordinal) &&
                    string.Equals(existing.Label, label, StringComparison.Ordinal);
                if (pointerMatches || pointerlessOwnerMatches)
                    return existing.Id;
            }

            int id = _nextId++;
            if (_count >= MaxTrackedAllocations)
            {
                Debug.LogError(
                    $"{CriticalMemoryViolationPrefix}: NativeMemorySentinel registry capacity exceeded. owner={owner} label={label}");
                return 0;
            }

            _records[_count++] = new NativeAllocationRecord
            {
                Id = id,
                Pointer = pointerValue,
                Bytes = bytes,
                AllocationFrame = Application.isPlaying ? Time.frameCount : 0,
                Lifetime = lifetime,
                Owner = owner,
                Label = label,
                StackTrace = CaptureStackTrace()
            };
            _trackedBytes += bytes;
            return id;
        }

        /// <summary>
        /// Unregisters a native array allocation.
        /// </summary>
        public static void UnregisterNativeArray<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* pointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            UnregisterPointer(pointer);
        }

        /// <summary>
        /// Unregisters a native list allocation.
        /// </summary>
        public static void UnregisterNativeList(string owner, string label)
        {
            Unregister(owner, label);
        }

        /// <summary>
        /// Unregisters a pointerless native hash map allocation by owner and label.
        /// </summary>
        public static void UnregisterNativeHashMap(string owner, string label)
        {
            Unregister(owner, label);
        }

        /// <summary>
        /// Unregisters a pointerless native parallel hash map allocation by owner and label.
        /// </summary>
        public static void UnregisterNativeParallelHashMap(string owner, string label)
        {
            Unregister(owner, label);
        }

        /// <summary>
        /// Unregisters a pointerless native parallel hash set allocation by owner and label.
        /// </summary>
        public static void UnregisterNativeParallelHashSet(string owner, string label)
        {
            Unregister(owner, label);
        }

        /// <summary>
        /// Unregisters a pointerless native parallel multi-hash map allocation by owner and label.
        /// </summary>
        public static void UnregisterNativeParallelMultiHashMap(string owner, string label)
        {
            Unregister(owner, label);
        }

        /// <summary>
        /// Unregisters a pointerless native queue allocation by owner and label.
        /// </summary>
        public static void UnregisterNativeQueue(string owner, string label)
        {
            Unregister(owner, label);
        }

        /// <summary>
        /// Unregisters a raw persistent native pointer.
        /// </summary>
        public static void UnregisterPointer(void* pointer)
        {
            IntPtr target = (IntPtr)pointer;
            if (target == IntPtr.Zero)
                return;

            for (int i = 0; i < _count; i++)
            {
                if (_records[i].Pointer != target)
                    continue;

                RemoveAt(i);
                return;
            }
        }

        /// <summary>
        /// Unregisters the latest matching owner/label record.
        /// </summary>
        public static void Unregister(string owner, string label)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                if (!string.Equals(_records[i].Owner, owner, StringComparison.Ordinal) ||
                    !string.Equals(_records[i].Label, label, StringComparison.Ordinal))
                {
                    continue;
                }

                RemoveAt(i);
                return;
            }
        }

        /// <summary>
        /// Reports scene-lifetime native allocations that survived a scene unload.
        /// </summary>
        public static void ReportSceneLifetimeLeaks(string context)
        {
            int reported = 0;
            for (int i = 0; i < _count; i++)
            {
                NativeAllocationRecord record = _records[i];
                if (record.Lifetime != NativeAllocationLifetime.Scene)
                    continue;

                reported++;
                Interlocked.Increment(ref _sceneLeakViolationCount);
                Debug.LogError(
                    $"{CriticalMemoryViolationPrefix}: scene allocation survived unload. context={context} owner={record.Owner} label={record.Label} bytes={record.Bytes} pointer=0x{record.Pointer.ToInt64():X}\nALLOCATOR_STACK:\n{record.StackTrace}");
            }

#if HECTON_FULL_NATIVE_LEAK_SCAN_ON_SCENE_UNLOAD
            int unsafeLeakCount = UnsafeUtility.CheckForLeaks();
            if (unsafeLeakCount > reported)
            {
                Interlocked.Increment(ref _sceneLeakViolationCount);
                Debug.LogError(
                    $"{CriticalMemoryViolationPrefix}: UnsafeUtility leak detector reported {unsafeLeakCount} leak(s), sentinel scene records={reported}. context={context}");
            }
#endif
        }

        /// <summary>
        /// Reports Temp/TempJob native allocations that survived far beyond their legal transient window.
        /// </summary>
        public static void AuditLongLivedTransientAllocations(int currentFrame)
        {
            if (currentFrame <= 0)
                return;

            for (int i = 0; i < _count; i++)
            {
                NativeAllocationRecord record = _records[i];
                if (record.LeakReported ||
                    (record.Lifetime != NativeAllocationLifetime.Temp &&
                     record.Lifetime != NativeAllocationLifetime.TempJob))
                {
                    continue;
                }

                int allocationFrame = record.AllocationFrame;
                if (allocationFrame <= 0 ||
                    currentFrame - allocationFrame <= LongLivedTransientFrameThreshold)
                {
                    continue;
                }

                record.LeakReported = true;
                _records[i] = record;
                Debug.LogError(
                    $"{MemoryLeakDetectedPrefix}: {record.Lifetime} allocation survived {currentFrame - allocationFrame} frames. owner={record.Owner} label={record.Label} bytes={record.Bytes} pointer=0x{record.Pointer.ToInt64():X}\nALLOCATOR_STACK:\n{record.StackTrace}");
            }
        }

        private static void RegisterSceneHooks()
        {
            if (_sceneHooksRegistered)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            _sceneHooksRegistered = true;
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            ReportSceneLifetimeLeaks(scene.name);
        }

        private static void RemoveAt(int index)
        {
            _trackedBytes -= _records[index].Bytes;
            _count--;
            _records[index] = _records[_count];
            _records[_count] = default;
        }

        private static void RefreshPointerlessBytes(string owner, string label, long bytes)
        {
            if (bytes <= 0L)
                return;

            for (int i = _count - 1; i >= 0; i--)
            {
                NativeAllocationRecord record = _records[i];
                if (record.Pointer != IntPtr.Zero ||
                    !string.Equals(record.Owner, owner, StringComparison.Ordinal) ||
                    !string.Equals(record.Label, label, StringComparison.Ordinal))
                {
                    continue;
                }

                long delta = bytes - record.Bytes;
                if (delta == 0L)
                    return;

                record.Bytes = bytes;
                _records[i] = record;
                _trackedBytes += delta;
                return;
            }
        }

        private static long EstimateNativeHashMapBytes<TKey, TValue>(int capacity)
            where TKey : unmanaged
            where TValue : unmanaged
        {
            long safeCapacity = Math.Max(1, capacity);
            long bytesPerEntry =
                UnsafeUtility.SizeOf<TKey>() +
                UnsafeUtility.SizeOf<TValue>() +
                sizeof(int) +
                1L;
            return safeCapacity * bytesPerEntry;
        }

        private static long EstimateNativeHashSetBytes<TKey>(int capacity)
            where TKey : unmanaged
        {
            long safeCapacity = Math.Max(1, capacity);
            long bytesPerEntry =
                UnsafeUtility.SizeOf<TKey>() +
                sizeof(int) +
                sizeof(int);
            return safeCapacity * bytesPerEntry;
        }

        private static long EstimateNativeMultiHashMapBytes<TKey, TValue>(int capacity)
            where TKey : unmanaged
            where TValue : unmanaged
        {
            long safeCapacity = Math.Max(1, capacity);
            long bytesPerEntry =
                UnsafeUtility.SizeOf<TKey>() +
                UnsafeUtility.SizeOf<TValue>() +
                sizeof(int) +
                sizeof(int);
            return safeCapacity * bytesPerEntry;
        }

        private static string CaptureStackTrace()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return StackTraceUtility.ExtractStackTrace();
#else
            return string.Empty;
#endif
        }
    }
}
