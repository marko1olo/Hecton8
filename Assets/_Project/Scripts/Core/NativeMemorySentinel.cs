using System;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Hecton.Localization;
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
    /// Blittable source descriptor for deterministic replay snapshot capture.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NativeAllocationSnapshotSource
    {
        /// <summary>Raw pointer to a stable pointer-backed native allocation.</summary>
        [FieldOffset(0)]
        public ulong Pointer;

        /// <summary>Allocation byte count.</summary>
        [FieldOffset(8)]
        public long Bytes;

        /// <summary>Stable owner hash.</summary>
        [FieldOffset(16)]
        public uint OwnerHash;

        /// <summary>Stable label hash.</summary>
        [FieldOffset(20)]
        public uint LabelHash;

        /// <summary>Frame where the allocation was registered.</summary>
        [FieldOffset(24)]
        public int AllocationFrame;

        /// <summary>Stored <see cref="NativeAllocationLifetime"/> value.</summary>
        [FieldOffset(28)]
        public byte Lifetime;

        /// <summary>Stored <see cref="Allocator"/> value.</summary>
        [FieldOffset(29)]
        public byte Allocator;

        /// <summary>Reserved padding for fixed 32-byte layout.</summary>
        [FieldOffset(30)]
        public ushort Reserved;
    }

    /// <summary>
    /// Central cold-path registry for persistent native allocations.
    /// </summary>
    public static unsafe class NativeMemorySentinel
    {
        private const int MaxTrackedAllocations = 1024;
        private const int TempAllocationFrameThreshold = 1;
        private const int TempJobAllocationFrameThreshold = 4;
        private const int MaxPersistentReallocationRecords = 128;
        private const int PersistentReallocationThreshold = 3;
        private const float PersistentReallocationWindowSeconds = 60f;
        private const string CriticalMemoryViolationPrefix = "CRITICAL_MEMORY_VIOLATION";
        private const string MemoryLeakDetectedPrefix = "MEMORY_LEAK_DETECTED";
        private const string StaleBufferCrimePrefix = "STALE_BUFFER_CRIME";
        private const string PersistentFragmentationRiskPrefix = "PERSISTENT_FRAGMENTATION_RISK";
        private const string CriticalMemoryViolationRegistryCapacityMessage = "CRITICAL_MEMORY_VIOLATION: NativeMemorySentinel registry capacity exceeded.";
        private const string CriticalMemoryViolationSceneLeakMessage = "CRITICAL_MEMORY_VIOLATION: scene allocation survived unload.";
        private const string CriticalMemoryViolationUnsafeLeakMessage = "CRITICAL_MEMORY_VIOLATION: UnsafeUtility leak detector reported leaks.";
        private const string CriticalMemoryViolationServiceShutdownLeakMessage = "CRITICAL_MEMORY_VIOLATION: native allocations survived service shutdown.";
        private const string MemoryLeakDetectedRetentionMessage = "MEMORY_LEAK_DETECTED: transient allocation exceeded legal frame window.";
        private const string StaleBufferCrimeRetentionMessage = "STALE_BUFFER_CRIME: TempJob allocation exceeded 4-frame legal window.";
        private const string PersistentFragmentationRiskMessage = "PERSISTENT_FRAGMENTATION_RISK: persistent native allocation changed size more than 3 times in 60 seconds.";
        private const string NativeLeakReapedMessage = "NATIVE_LEAK_REAPED: RuntimeWatchdog force-freed a scene native allocation.";

        private static readonly uint _nativeMemoryContextHash = unchecked((uint)LocHash.Compute(nameof(NativeMemorySentinel)));
        private static readonly uint _criticalMemoryViolationHash = unchecked((uint)LocHash.Compute(CriticalMemoryViolationPrefix));
        private static readonly uint _memoryLeakDetectedHash = unchecked((uint)LocHash.Compute(MemoryLeakDetectedPrefix));
        private static readonly uint _staleBufferCrimeHash = unchecked((uint)LocHash.Compute(StaleBufferCrimePrefix));
        private static readonly uint _persistentFragmentationRiskHash = unchecked((uint)LocHash.Compute(PersistentFragmentationRiskPrefix));

        private struct NativeAllocationRecord
        {
            public int Id;
            public IntPtr Pointer;
            public long Bytes;
            public int AllocationFrame;
            public NativeAllocationLifetime Lifetime;
            public Allocator Allocator;
            public uint OwnerHash;
            public uint LabelHash;
            public bool LeakReported;
            public string Owner;
            public string Label;
            public string StackTrace;
        }

        private struct PersistentReallocationRecord
        {
            public string Owner;
            public string Label;
            public long LastBytes;
            public int ReallocationCount;
            public float WindowStartTime;
            public bool Reported;
        }

        // COLD ALLOC: NativeAllocationRecord[1024] - persistent native allocation ownership registry - owner: NativeMemorySentinel
        private static readonly NativeAllocationRecord[] _records = new NativeAllocationRecord[MaxTrackedAllocations];
        // COLD ALLOC: PersistentReallocationRecord[128] - persistent native allocation resize telemetry window - owner: NativeMemorySentinel
        private static readonly PersistentReallocationRecord[] _persistentReallocationRecords = new PersistentReallocationRecord[MaxPersistentReallocationRecords];
        private static int _count;
        private static int _persistentReallocationRecordCount;
        private static int _nextId = 1;
        private static long _trackedBytes;
        private static int _sceneLeakViolationCount;
        private static int _activeTempAllocationCount;
        private static int _activeTempJobAllocationCount;
        private static int _telemetryPublishInProgress;
        private static int _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        private static bool _sceneHooksRegistered;

        /// <summary>Active tracked allocation count.</summary>
        public static int ActiveAllocationCount => Volatile.Read(ref _count);

        /// <summary>Tracked persistent native bytes.</summary>
        public static long TrackedBytes => Volatile.Read(ref _trackedBytes);

        /// <summary>Scene lifetime leak violation count reported by the sentinel.</summary>
        public static int SceneLeakViolationCount => Volatile.Read(ref _sceneLeakViolationCount);

        /// <summary>
        /// Computes the same stable numeric hash used by native allocation records.
        /// </summary>
        public static uint ComputeSnapshotHash(string value)
        {
            return ComputeStableHash(value);
        }

        /// <summary>
        /// Copies pointer-backed replay snapshot sources into a caller-owned native buffer.
        /// </summary>
        public static int CopySnapshotSources(NativeArray<NativeAllocationSnapshotSource> destination, uint excludedOwnerHash = 0u)
        {
            if (!destination.IsCreated)
                return 0;

            int writeIndex = 0;
            int count = Volatile.Read(ref _count);
            for (int i = 0; i < count && writeIndex < destination.Length; i++)
            {
                NativeAllocationRecord record = _records[i];
                if (!CanCopySnapshotSource(in record, excludedOwnerHash))
                    continue;

                destination[writeIndex++] = new NativeAllocationSnapshotSource
                {
                    Pointer = unchecked((ulong)record.Pointer.ToInt64()),
                    Bytes = record.Bytes,
                    OwnerHash = record.OwnerHash,
                    LabelHash = record.LabelHash,
                    AllocationFrame = record.AllocationFrame,
                    Lifetime = (byte)record.Lifetime,
                    Allocator = (byte)record.Allocator
                };
            }

            return writeIndex;
        }

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
            for (int i = 0; i < _persistentReallocationRecordCount; i++)
                _persistentReallocationRecords[i] = default;

            _count = 0;
            _persistentReallocationRecordCount = 0;
            _nextId = 1;
            _trackedBytes = 0L;
            _sceneLeakViolationCount = 0;
            _activeTempAllocationCount = 0;
            _activeTempJobAllocationCount = 0;
            _telemetryPublishInProgress = 0;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
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
        /// Registers a native list as a distinct pointerless instance. Caller must keep the returned id.
        /// </summary>
        public static int RegisterNativeListInstance<T>(
            NativeList<T> list,
            string owner,
            string label,
            NativeAllocationLifetime lifetime) where T : unmanaged
        {
            if (!list.IsCreated)
                return 0;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * list.Capacity;
            return RegisterPointer(null, bytes, owner, label, lifetime, false);
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
        /// Registers a native parallel hash map as a distinct pointerless instance. Caller must keep the returned id.
        /// </summary>
        public static int RegisterNativeParallelHashMapInstance<TKey, TValue>(
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
            return RegisterPointer(null, bytes, owner, label, lifetime, false);
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
            return RegisterPointer(pointer, bytes, owner, label, lifetime, true);
        }

        private static int RegisterPointer(
            void* pointer,
            long bytes,
            string owner,
            string label,
            NativeAllocationLifetime lifetime,
            bool coalescePointerlessOwnerLabel)
        {
            if (bytes <= 0L)
                return 0;

            IntPtr pointerValue = (IntPtr)pointer;
            uint ownerHash = ComputeStableHash(owner);
            uint labelHash = ComputeStableHash(label);
            for (int i = 0; i < _count; i++)
            {
                NativeAllocationRecord existing = _records[i];
                bool pointerMatches = pointerValue != IntPtr.Zero && existing.Pointer == pointerValue;
                bool pointerlessOwnerMatches =
                    coalescePointerlessOwnerLabel &&
                    pointerValue == IntPtr.Zero &&
                    existing.Pointer == IntPtr.Zero &&
                    string.Equals(existing.Owner, owner, StringComparison.Ordinal) &&
                    string.Equals(existing.Label, label, StringComparison.Ordinal);
                if (pointerMatches || pointerlessOwnerMatches)
                {
                    bool recordChanged = false;
                    if (existing.Bytes != bytes)
                    {
                        TrackPersistentReallocation(owner, label, bytes, lifetime);
                        _trackedBytes += bytes - existing.Bytes;
                        existing.Bytes = bytes;
                        recordChanged = true;
                    }

                    if (existing.Lifetime != lifetime)
                    {
                        AdjustTransientAllocationCount(existing.Lifetime, -1);
                        AdjustTransientAllocationCount(lifetime, 1);
                        existing.Lifetime = lifetime;
                        existing.Allocator = ResolveAllocator(lifetime);
                        existing.AllocationFrame = ResolveCurrentFrame(0);
                        existing.StackTrace = CaptureStackTrace(lifetime);
                        recordChanged = true;
                    }

                    if (existing.LeakReported)
                    {
                        existing.LeakReported = false;
                        existing.AllocationFrame = ResolveCurrentFrame(existing.AllocationFrame);
                        existing.StackTrace = CaptureStackTrace(existing.Lifetime);
                        recordChanged = true;
                    }

                    if (existing.OwnerHash != ownerHash || existing.LabelHash != labelHash)
                    {
                        existing.OwnerHash = ownerHash;
                        existing.LabelHash = labelHash;
                        recordChanged = true;
                    }

                    if (recordChanged)
                        _records[i] = existing;

                    return existing.Id;
                }
            }

            if (_count >= MaxTrackedAllocations)
            {
                PublishPerformanceWarningNoReentry(
                    _criticalMemoryViolationHash,
                    _nativeMemoryContextHash,
                    MaxTrackedAllocations);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(CriticalMemoryViolationRegistryCapacityMessage);
#endif
                return 0;
            }

            int id = _nextId++;
            TrackPersistentReallocation(owner, label, bytes, lifetime);

            _records[_count++] = new NativeAllocationRecord
            {
                Id = id,
                Pointer = pointerValue,
                Bytes = bytes,
                AllocationFrame = ResolveCurrentFrame(0),
                Lifetime = lifetime,
                Allocator = ResolveAllocator(lifetime),
                OwnerHash = ownerHash,
                LabelHash = labelHash,
                Owner = owner,
                Label = label,
                StackTrace = CaptureStackTrace(lifetime)
            };
            _trackedBytes += bytes;
            AdjustTransientAllocationCount(lifetime, 1);
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
        /// Unregisters a tracked allocation by stable registration id.
        /// </summary>
        public static void Unregister(int id)
        {
            if (id <= 0)
                return;

            for (int i = _count - 1; i >= 0; i--)
            {
                if (_records[i].Id != id)
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
                if (record.LeakReported || record.Lifetime != NativeAllocationLifetime.Scene)
                    continue;

                reported++;
                Interlocked.Increment(ref _sceneLeakViolationCount);
                PublishPerformanceWarningNoReentry(
                    _criticalMemoryViolationHash,
                    _nativeMemoryContextHash,
                    record.Bytes <= 0L ? 0f : record.Bytes > float.MaxValue ? float.MaxValue : (float)record.Bytes);
                record.LeakReported = true;
                _records[i] = record;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(CriticalMemoryViolationSceneLeakMessage);
#endif
            }

#if HECTON_FULL_NATIVE_LEAK_SCAN_ON_SCENE_UNLOAD
            int unsafeLeakCount = UnsafeUtility.CheckForLeaks();
            if (unsafeLeakCount > reported)
            {
                Interlocked.Increment(ref _sceneLeakViolationCount);
                PublishPerformanceWarningNoReentry(
                    _criticalMemoryViolationHash,
                    _nativeMemoryContextHash,
                    unsafeLeakCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(CriticalMemoryViolationUnsafeLeakMessage);
#endif
            }
#endif
        }

        /// <summary>
        /// Reports and force-frees scene-lifetime native arrays that survived a scene unload.
        /// </summary>
        public static int ReapSceneLifetimeLeaks(string context)
        {
            int reaped = 0;
            for (int i = _count - 1; i >= 0; i--)
            {
                NativeAllocationRecord record = _records[i];
                if (record.Lifetime != NativeAllocationLifetime.Scene ||
                    (record.LeakReported && record.Pointer == IntPtr.Zero))
                {
                    continue;
                }

                if (record.Pointer == IntPtr.Zero)
                {
                    Interlocked.Increment(ref _sceneLeakViolationCount);
                    PublishPerformanceWarningNoReentry(
                        _criticalMemoryViolationHash,
                        _nativeMemoryContextHash,
                        record.Bytes <= 0L ? 0f : record.Bytes > float.MaxValue ? float.MaxValue : (float)record.Bytes);
                    record.LeakReported = true;
                    _records[i] = record;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(CriticalMemoryViolationSceneLeakMessage);
#endif
                    continue;
                }

                if (!record.LeakReported)
                {
                    Interlocked.Increment(ref _sceneLeakViolationCount);
                    PublishPerformanceWarningNoReentry(
                        _criticalMemoryViolationHash,
                        _nativeMemoryContextHash,
                        record.Bytes <= 0L ? 0f : record.Bytes > float.MaxValue ? float.MaxValue : (float)record.Bytes);
                }

                Allocator allocator = (int)record.Allocator == 0
                    ? Allocator.Persistent
                    : record.Allocator;
                H8Memory.ReleaseSentinelReapedRaw(record.Pointer.ToPointer(), allocator);
                RuntimeWatchdog.ReportNativeLeakReaped(
                    ComputeStableHash(record.Owner),
                    ComputeStableHash(record.Label),
                    record.Bytes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    NativeLeakReapedMessage +
                    " context=" + context +
                    " owner=" + record.Owner +
                    " label=" + record.Label +
                    "\n" + record.StackTrace);
#endif
                RemoveAt(i);
                reaped++;
            }

#if HECTON_FULL_NATIVE_LEAK_SCAN_ON_SCENE_UNLOAD
            int unsafeLeakCount = UnsafeUtility.CheckForLeaks();
            if (unsafeLeakCount > reaped)
            {
                Interlocked.Increment(ref _sceneLeakViolationCount);
                PublishPerformanceWarningNoReentry(
                    _criticalMemoryViolationHash,
                    _nativeMemoryContextHash,
                    unsafeLeakCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(CriticalMemoryViolationUnsafeLeakMessage);
#endif
            }
#endif
            return reaped;
        }

        /// <summary>
        /// Asserts that registry-owned services released every tracked native allocation before slot reset.
        /// </summary>
        public static bool AssertNoAllocationsAfterServiceShutdown(string context)
        {
            int activeCount = ActiveAllocationCount;
            if (activeCount <= 0)
                return true;

            PublishPerformanceWarningNoReentry(
                _criticalMemoryViolationHash,
                _nativeMemoryContextHash,
                activeCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(CriticalMemoryViolationServiceShutdownLeakMessage);
#endif
            return false;
        }

        /// <summary>
        /// Reports Temp/TempJob native allocations that survived beyond their legal transient window.
        /// </summary>
        public static void AuditLongLivedTransientAllocations(int currentFrame)
        {
            if (currentFrame <= 0)
                return;

            if (Volatile.Read(ref _activeTempAllocationCount) == 0 &&
                Volatile.Read(ref _activeTempJobAllocationCount) == 0)
            {
                return;
            }

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
                int retentionFrames = currentFrame - allocationFrame;
                int legalFrameWindow = record.Lifetime == NativeAllocationLifetime.TempJob
                    ? TempJobAllocationFrameThreshold
                    : TempAllocationFrameThreshold;
                if (allocationFrame <= 0 || retentionFrames <= legalFrameWindow)
                {
                    continue;
                }

                record.LeakReported = true;
                _records[i] = record;
                uint warningHash = record.Lifetime == NativeAllocationLifetime.TempJob
                    ? _staleBufferCrimeHash
                    : _memoryLeakDetectedHash;
                PublishPerformanceWarningNoReentry(
                    warningHash,
                    _nativeMemoryContextHash,
                    retentionFrames);
                uint allocationHash = ComputeOwnerLabelHash(record.Owner, record.Label);
                if (record.Lifetime == NativeAllocationLifetime.TempJob)
                {
                    CrashTelemetryBuffer.ReportStaleBufferCrime(allocationHash, retentionFrames, record.Bytes);
                }
                else
                {
                    CrashTelemetryBuffer.ReportNativeTransientLeak(allocationHash, retentionFrames, record.Bytes);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    record.Lifetime == NativeAllocationLifetime.TempJob
                        ? StaleBufferCrimeRetentionMessage
                        : MemoryLeakDetectedRetentionMessage);
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.ReapNativeSceneLeaks(scene.name);
#else
            RuntimeWatchdog.ReapNativeSceneLeaks(string.Empty);
#endif
        }

        private static void RemoveAt(int index)
        {
            NativeAllocationLifetime lifetime = _records[index].Lifetime;
            _trackedBytes -= _records[index].Bytes;
            _count--;
            _records[index] = _records[_count];
            _records[_count] = default;
            AdjustTransientAllocationCount(lifetime, -1);
        }

        private static void AdjustTransientAllocationCount(NativeAllocationLifetime lifetime, int delta)
        {
            if (lifetime == NativeAllocationLifetime.Temp)
            {
                AdjustCounterNonNegative(ref _activeTempAllocationCount, delta);
                return;
            }

            if (lifetime == NativeAllocationLifetime.TempJob)
                AdjustCounterNonNegative(ref _activeTempJobAllocationCount, delta);
        }

        private static void AdjustCounterNonNegative(ref int counter, int delta)
        {
            int current;
            int next;
            do
            {
                current = Volatile.Read(ref counter);
                next = Math.Max(0, current + delta);
            }
            while (Interlocked.CompareExchange(ref counter, next, current) != current);
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

                TrackPersistentReallocation(owner, label, bytes, record.Lifetime);
                record.Bytes = bytes;
                _records[i] = record;
                _trackedBytes += delta;
                return;
            }
        }

        private static int ResolveCurrentFrame(int fallbackFrame)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return fallbackFrame;

            return Application.isPlaying ? Time.frameCount : fallbackFrame;
        }

        private static float ResolveCurrentUnscaledTime()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return 0f;

            return Application.isPlaying ? Time.unscaledTime : 0f;
        }

        private static void TrackPersistentReallocation(
            string owner,
            string label,
            long bytes,
            NativeAllocationLifetime lifetime)
        {
            if (!IsPersistentLifetime(lifetime) || bytes <= 0L)
                return;

            float now = ResolveCurrentUnscaledTime();
            int recordIndex = FindPersistentReallocationRecord(owner, label);
            if (recordIndex < 0)
            {
                if (_persistentReallocationRecordCount >= MaxPersistentReallocationRecords)
                    return;

                _persistentReallocationRecords[_persistentReallocationRecordCount++] = new PersistentReallocationRecord
                {
                    Owner = owner,
                    Label = label,
                    LastBytes = bytes,
                    ReallocationCount = 0,
                    WindowStartTime = now,
                    Reported = false
                };
                return;
            }

            PersistentReallocationRecord record = _persistentReallocationRecords[recordIndex];
            if (record.LastBytes == bytes)
                return;

            if (now - record.WindowStartTime > PersistentReallocationWindowSeconds)
            {
                record.WindowStartTime = now;
                record.ReallocationCount = 1;
                record.Reported = false;
            }
            else
            {
                record.ReallocationCount++;
            }

            record.LastBytes = bytes;
            if (!record.Reported && record.ReallocationCount > PersistentReallocationThreshold)
            {
                record.Reported = true;
                uint allocationHash = ComputeOwnerLabelHash(owner, label);
                PublishPerformanceWarningNoReentry(
                    _persistentFragmentationRiskHash,
                    allocationHash,
                    record.ReallocationCount);
                CrashTelemetryBuffer.ReportNativeFragmentationRisk(allocationHash, record.ReallocationCount, bytes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(PersistentFragmentationRiskMessage);
#endif
            }

            _persistentReallocationRecords[recordIndex] = record;
        }

        private static Allocator ResolveAllocator(NativeAllocationLifetime lifetime)
        {
            switch (lifetime)
            {
                case NativeAllocationLifetime.Temp:
                    return Allocator.Temp;
                case NativeAllocationLifetime.TempJob:
                    return Allocator.TempJob;
                default:
                    return Allocator.Persistent;
            }
        }

        private static bool CanCopySnapshotSource(in NativeAllocationRecord record, uint excludedOwnerHash)
        {
            if (record.Pointer == IntPtr.Zero ||
                record.Bytes <= 0L ||
                record.LeakReported ||
                (excludedOwnerHash != 0u && record.OwnerHash == excludedOwnerHash))
            {
                return false;
            }

            switch (record.Lifetime)
            {
                case NativeAllocationLifetime.Scene:
                case NativeAllocationLifetime.Session:
                case NativeAllocationLifetime.Permanent:
                case NativeAllocationLifetime.TransientArena:
                    return true;
                default:
                    return false;
            }
        }

        private static uint ComputeStableHash(string value)
        {
            return string.IsNullOrEmpty(value)
                ? 0u
                : unchecked((uint)LocHash.Compute(value));
        }

        public static void ReportQueueOverflow(uint warningHash, uint overflowCount, uint contextHash)
        {
            PublishPerformanceWarningNoReentry(warningHash, contextHash, overflowCount);
        }

        private static void PublishPerformanceWarningNoReentry(uint warningHash, uint contextHash, float scalarValue)
        {
            if (Interlocked.CompareExchange(ref _telemetryPublishInProgress, 1, 0) != 0)
                return;

            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, scalarValue);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            finally
            {
                Volatile.Write(ref _telemetryPublishInProgress, 0);
            }
        }

        private static int FindPersistentReallocationRecord(string owner, string label)
        {
            for (int i = 0; i < _persistentReallocationRecordCount; i++)
            {
                PersistentReallocationRecord record = _persistentReallocationRecords[i];
                if (string.Equals(record.Owner, owner, StringComparison.Ordinal) &&
                    string.Equals(record.Label, label, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsPersistentLifetime(NativeAllocationLifetime lifetime)
        {
            return lifetime == NativeAllocationLifetime.Scene ||
                   lifetime == NativeAllocationLifetime.Session ||
                   lifetime == NativeAllocationLifetime.Permanent;
        }

        private static uint ComputeOwnerLabelHash(string owner, string label)
        {
            uint ownerHash = unchecked((uint)LocHash.Compute(owner ?? string.Empty));
            uint labelHash = unchecked((uint)LocHash.Compute(label ?? string.Empty));
            return ownerHash ^ (labelHash * 16777619u);
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

        private static string CaptureStackTrace(NativeAllocationLifetime lifetime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (lifetime == NativeAllocationLifetime.Temp ||
                lifetime == NativeAllocationLifetime.TempJob)
            {
                return string.Empty;
            }

            return StackTraceUtility.ExtractStackTrace();
#else
            return string.Empty;
#endif
        }
    }
}
