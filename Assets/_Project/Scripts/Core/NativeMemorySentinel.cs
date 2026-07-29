using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Fatal native memory leak detected during teardown or subsystem reload.
    /// </summary>
    public sealed class FatalMemoryLeakException : InvalidOperationException
    {
        public FatalMemoryLeakException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Central cold-path registry for persistent native allocations.
    /// </summary>
    public static unsafe class NativeMemorySentinel
    {
        private const int MaxTrackedAllocations = 1024;
        private const int MutationGateSpinWait = 4;
        private const int TempAllocationFrameThreshold = 1;
        private const int TempJobAllocationFrameThreshold = 4;
        private const int MaxPersistentReallocationRecords = 128;
        private const int PersistentReallocationThreshold = 3;
        private const float PersistentReallocationWindowSeconds = 60f;
        private const uint StableHashFnvOffset = 2166136261u;
        private const uint StableHashFnvPrime = 16777619u;
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
        private const string SceneScopeOwnerDeclaredLabel = "owner-declared";
        private const string SceneScopeActiveSceneAtAllocLabel = "active-scene-at-alloc";

        private static readonly uint _nativeMemoryContextHash = ComputeStableHash(nameof(NativeMemorySentinel));
        private static readonly uint _criticalMemoryViolationHash = ComputeStableHash(CriticalMemoryViolationPrefix);
        private static readonly uint _memoryLeakDetectedHash = ComputeStableHash(MemoryLeakDetectedPrefix);
        private static readonly uint _staleBufferCrimeHash = ComputeStableHash(StaleBufferCrimePrefix);
        private static readonly uint _persistentFragmentationRiskHash = ComputeStableHash(PersistentFragmentationRiskPrefix);

        [StructLayout(LayoutKind.Explicit, Size = 312)]
        private struct NativeAllocationRecord
        {
            [FieldOffset(0)] internal IntPtr Pointer;
            [FieldOffset(8)] public long Bytes;
            [FieldOffset(16)] public FixedString128Bytes Owner;
            [FieldOffset(144)] public FixedString128Bytes Label;
            [FieldOffset(272)] public int Id;
            [FieldOffset(276)] public int AllocationFrame;
            [FieldOffset(280)] public Allocator Allocator;
            [FieldOffset(284)] public uint OwnerHash;
            [FieldOffset(288)] public uint LabelHash;
            [FieldOffset(292)] public NativeAllocationLifetime Lifetime;
            [FieldOffset(293)] private byte _leakReported;
            [FieldOffset(294)] private byte _sceneIdentityOwnerDeclared;
            [FieldOffset(295)] private byte _pad0;
            [FieldOffset(296)] public int SceneIdentityHash;
            [FieldOffset(300)] public int SceneBuildIndex;
            [FieldOffset(304)] private ulong _pad1;

            public bool LeakReported
            {
                get => _leakReported != 0;
                set => _leakReported = value ? (byte)1 : (byte)0;
            }

            /// <summary>
            /// True when the owner passed its own <see cref="Scene"/> at registration, so
            /// <see cref="SceneIdentityHash"/> is an ownership fact. False when the sentinel inferred the
            /// binding from <c>SceneManager.GetActiveScene()</c> at allocation time, which is a guess that
            /// is provably wrong during an additive scene transition - see ResolveCurrentSceneIdentity.
            /// Reporting must say which of the two it has; the gate treats both identically.
            /// </summary>
            public bool SceneIdentityOwnerDeclared
            {
                get => _sceneIdentityOwnerDeclared != 0;
                set => _sceneIdentityOwnerDeclared = value ? (byte)1 : (byte)0;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 288)]
        private struct PersistentReallocationRecord
        {
            [FieldOffset(0)] public FixedString128Bytes Owner;
            [FieldOffset(128)] public FixedString128Bytes Label;
            [FieldOffset(256)] public long LastBytes;
            [FieldOffset(264)] public uint OwnerHash;
            [FieldOffset(268)] public uint LabelHash;
            [FieldOffset(272)] public int ReallocationCount;
            [FieldOffset(276)] public float WindowStartTime;
            [FieldOffset(280)] private byte _reported;
            [FieldOffset(281)] private byte _pad0;
            [FieldOffset(282)] private ushort _pad1;
            [FieldOffset(284)] private uint _pad2;

            public bool Reported
            {
                get => _reported != 0;
                set => _reported = value ? (byte)1 : (byte)0;
            }
        }

        // COLD ALLOC: NativeAllocationRecord[1024] - persistent native allocation ownership registry - owner: NativeMemorySentinel
        private static readonly NativeAllocationRecord[] _records = new NativeAllocationRecord[MaxTrackedAllocations];
        // COLD ALLOC: PersistentReallocationRecord[128] - persistent native allocation resize telemetry window - owner: NativeMemorySentinel
        private static readonly PersistentReallocationRecord[] _persistentReallocationRecords = new PersistentReallocationRecord[MaxPersistentReallocationRecords];
        // COLD ALLOC: NativeAllocationRecord[1024] - scene leak telemetry snapshot outside mutation gate - owner: NativeMemorySentinel
        private static readonly NativeAllocationRecord[] _sceneLeakReportScratch = new NativeAllocationRecord[MaxTrackedAllocations];
        private static int _count;
        private static int _persistentReallocationRecordCount;
        private static int _nextId = 1;
        private static long _trackedBytes;
        private static int _sceneLeakViolationCount;
        private static int _activeTempAllocationCount;
        private static int _activeTempJobAllocationCount;
        private static int _telemetryPublishInProgress;
        private static int _mutationGate;
        private static int _sceneLeakReportGate;
        private static int _diagnosticSceneLeakLogSuppressions;
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
        internal static int CopySnapshotSources(NativeArray<NativeAllocationSnapshotSource> destination, uint excludedOwnerHash = 0u)
        {
            if (!destination.IsCreated)
                return 0;

            EnterMutationGate();
            try
            {
                int writeIndex = 0;
                int count = _count;
                for (int i = 0; i < count && writeIndex < destination.Length; i++)
                {
                    NativeAllocationRecord record = _records[i];
                    if (!CanCopySnapshotSource(in record, excludedOwnerHash))
                        continue;

                    NativeAllocationSnapshotSource snapshot = default;
                    snapshot.SourcePointerValue = unchecked((ulong)record.Pointer.ToInt64());
                    snapshot.Bytes = record.Bytes;
                    snapshot.OwnerHash = record.OwnerHash;
                    snapshot.LabelHash = record.LabelHash;
                    snapshot.AllocationFrame = record.AllocationFrame;
                    snapshot.Lifetime = (byte)record.Lifetime;
                    snapshot.Allocator = (byte)record.Allocator;
                    destination[writeIndex++] = snapshot;
                }

                return writeIndex;
            }
            finally
            {
                ExitMutationGate();
            }
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
            Interlocked.Exchange(ref _mutationGate, 0);
            Interlocked.Exchange(ref _sceneLeakReportGate, 0);
            int activeBeforeReset = Volatile.Read(ref _count);
            if (activeBeforeReset > 0)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning(BuildFatalLeakMessage("SubsystemRegistration-EditorBypass", activeBeforeReset));
#else
                throw new FatalMemoryLeakException(BuildFatalLeakMessage("SubsystemRegistration", activeBeforeReset));
#endif
            }

            EnterMutationGate();
            try
            {
                for (int i = 0; i < _count; i++)
                    _records[i] = default;
                for (int i = 0; i < _persistentReallocationRecordCount; i++)
                    _persistentReallocationRecords[i] = default;
                for (int i = 0; i < MaxTrackedAllocations; i++)
                    _sceneLeakReportScratch[i] = default;

                _count = 0;
                _persistentReallocationRecordCount = 0;
                _nextId = 1;
                _trackedBytes = 0L;
                _sceneLeakViolationCount = 0;
                _activeTempAllocationCount = 0;
                _activeTempJobAllocationCount = 0;
                _telemetryPublishInProgress = 0;
                _sceneLeakReportGate = 0;
                _diagnosticSceneLeakLogSuppressions = 0;
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _sceneHooksRegistered = false;
                SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            }
            finally
            {
                ExitMutationGate();
            }
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
        /// Registers a native hash map as a distinct pointerless instance. Caller must keep the returned id.
        /// </summary>
        public static int RegisterNativeHashMapInstance<TKey, TValue>(
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
            return RegisterPointer(null, bytes, owner, label, lifetime, false);
        }

        /// <summary>
        /// Registers an unsafe hash map as a distinct pointerless instance. Caller must keep the returned id.
        /// </summary>
        public static int RegisterUnsafeHashMapInstance<TKey, TValue>(
            UnsafeHashMap<TKey, TValue> map,
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
        /// Registers a native parallel hash set as a distinct pointerless instance. Caller must keep the returned id.
        /// </summary>
        public static int RegisterNativeParallelHashSetInstance<TKey>(
            NativeParallelHashSet<TKey> set,
            string owner,
            string label,
            NativeAllocationLifetime lifetime)
            where TKey : unmanaged, IEquatable<TKey>
        {
            if (!set.IsCreated)
                return 0;

            long bytes = EstimateNativeHashSetBytes<TKey>(set.Capacity);
            return RegisterPointer(null, bytes, owner, label, lifetime, false);
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
        /// Registers a native parallel multi-hash map as a distinct pointerless instance. Caller must keep the returned id.
        /// </summary>
        public static int RegisterNativeParallelMultiHashMapInstance<TKey, TValue>(
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
            return RegisterPointer(null, bytes, owner, label, lifetime, false);
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
        /// Registers a native queue as a distinct pointerless instance. Caller must keep the returned id.
        /// </summary>
        public static int RegisterNativeQueueInstance<T>(
            NativeQueue<T> queue,
            int expectedCapacity,
            string owner,
            string label,
            NativeAllocationLifetime lifetime) where T : unmanaged
        {
            if (!queue.IsCreated)
                return 0;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * Math.Max(1, expectedCapacity);
            return RegisterPointer(null, bytes, owner, label, lifetime, false);
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
        /// Refreshes a tracked native list instance after an explicit capacity change.
        /// </summary>
        public static void RefreshNativeListInstance<T>(
            NativeList<T> list,
            int id) where T : unmanaged
        {
            if (!list.IsCreated || id <= 0)
                return;

            long bytes = (long)UnsafeUtility.SizeOf<T>() * list.Capacity;
            RefreshPointerlessBytes(id, bytes);
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
        /// Refreshes a tracked unsafe hash map allocation after an explicit capacity change.
        /// </summary>
        public static void RefreshUnsafeHashMap<TKey, TValue>(
            UnsafeHashMap<TKey, TValue> map,
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
        /// Refreshes a tracked native parallel multi-hash map instance after an explicit capacity change.
        /// </summary>
        public static void RefreshNativeParallelMultiHashMapInstance<TKey, TValue>(
            NativeParallelMultiHashMap<TKey, TValue> map,
            int id)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated || id <= 0)
                return;

            RefreshPointerlessBytes(id, EstimateNativeMultiHashMapBytes<TKey, TValue>(map.Capacity));
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

        internal static int RegisterPointerlessBridgeRecord(
            long bytes,
            string owner,
            string label,
            NativeAllocationLifetime lifetime)
        {
            return RegisterPointer(null, bytes, owner, label, lifetime, false);
        }

        /// <summary>
        /// Registers a raw persistent native pointer against an explicit scene context.
        /// Use this from additive scene owners instead of relying on the active scene.
        /// </summary>
        public static int RegisterPointer(
            void* pointer,
            long bytes,
            string owner,
            string label,
            NativeAllocationLifetime lifetime,
            Scene scene)
        {
            return RegisterPointer(pointer, bytes, owner, label, lifetime, true, scene);
        }

        /// <summary>
        /// Registers a raw persistent native pointer using caller-owned fixed labels.
        /// Use this overload when the allocation owner already stores non-managed labels.
        /// </summary>
        public static int RegisterPointer(
            void* pointer,
            long bytes,
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            NativeAllocationLifetime lifetime)
        {
            return RegisterPointerFixed(pointer, bytes, in owner, in label, lifetime, true);
        }

        /// <summary>
        /// Registers a raw persistent native pointer using fixed labels and an explicit scene context.
        /// </summary>
        public static int RegisterPointer(
            void* pointer,
            long bytes,
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            NativeAllocationLifetime lifetime,
            Scene scene)
        {
            return RegisterPointerFixed(pointer, bytes, in owner, in label, lifetime, true, scene);
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

            uint ownerHash = ComputeStableHash(owner);
            uint labelHash = ComputeStableHash(label);
            FixedString128Bytes ownerFixed = ToFixedString128(owner);
            FixedString128Bytes labelFixed = ToFixedString128(label);
            return RegisterPointerFixed(
                pointer,
                bytes,
                in ownerFixed,
                in labelFixed,
                ownerHash,
                labelHash,
                lifetime,
                coalescePointerlessOwnerLabel);
        }

        private static int RegisterPointer(
            void* pointer,
            long bytes,
            string owner,
            string label,
            NativeAllocationLifetime lifetime,
            bool coalescePointerlessOwnerLabel,
            Scene scene)
        {
            if (bytes <= 0L)
                return 0;

            uint ownerHash = ComputeStableHash(owner);
            uint labelHash = ComputeStableHash(label);
            FixedString128Bytes ownerFixed = ToFixedString128(owner);
            FixedString128Bytes labelFixed = ToFixedString128(label);
            return RegisterPointerFixed(
                pointer,
                bytes,
                in ownerFixed,
                in labelFixed,
                ownerHash,
                labelHash,
                lifetime,
                coalescePointerlessOwnerLabel,
                scene);
        }

        private static int RegisterPointerFixed(
            void* pointer,
            long bytes,
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            NativeAllocationLifetime lifetime,
            bool coalescePointerlessOwnerLabel)
        {
            if (bytes <= 0L)
                return 0;

            return RegisterPointerFixed(
                pointer,
                bytes,
                in owner,
                in label,
                ComputeStableHash(in owner),
                ComputeStableHash(in label),
                lifetime,
                coalescePointerlessOwnerLabel);
        }

        private static int RegisterPointerFixed(
            void* pointer,
            long bytes,
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            NativeAllocationLifetime lifetime,
            bool coalescePointerlessOwnerLabel,
            Scene scene)
        {
            if (bytes <= 0L)
                return 0;

            return RegisterPointerFixed(
                pointer,
                bytes,
                in owner,
                in label,
                ComputeStableHash(in owner),
                ComputeStableHash(in label),
                lifetime,
                coalescePointerlessOwnerLabel,
                scene);
        }

        private static int RegisterPointerFixed(
            void* pointer,
            long bytes,
            in FixedString128Bytes ownerFixed,
            in FixedString128Bytes labelFixed,
            uint ownerHash,
            uint labelHash,
            NativeAllocationLifetime lifetime,
            bool coalescePointerlessOwnerLabel)
        {
            if (bytes <= 0L)
                return 0;

            ResolveRegistrationSceneIdentity(lifetime, out int currentSceneIdentityHash, out int currentSceneBuildIndex);
            return RegisterPointerFixed(
                pointer,
                bytes,
                in ownerFixed,
                in labelFixed,
                ownerHash,
                labelHash,
                lifetime,
                coalescePointerlessOwnerLabel,
                currentSceneIdentityHash,
                currentSceneBuildIndex,
                false);
        }

        private static int RegisterPointerFixed(
            void* pointer,
            long bytes,
            in FixedString128Bytes ownerFixed,
            in FixedString128Bytes labelFixed,
            uint ownerHash,
            uint labelHash,
            NativeAllocationLifetime lifetime,
            bool coalescePointerlessOwnerLabel,
            Scene scene)
        {
            if (bytes <= 0L)
                return 0;

            ResolveRegistrationSceneIdentity(lifetime, scene, out int currentSceneIdentityHash, out int currentSceneBuildIndex);
            return RegisterPointerFixed(
                pointer,
                bytes,
                in ownerFixed,
                in labelFixed,
                ownerHash,
                labelHash,
                lifetime,
                coalescePointerlessOwnerLabel,
                currentSceneIdentityHash,
                currentSceneBuildIndex,
                true);
        }

        private static int RegisterPointerFixed(
            void* pointer,
            long bytes,
            in FixedString128Bytes ownerFixed,
            in FixedString128Bytes labelFixed,
            uint ownerHash,
            uint labelHash,
            NativeAllocationLifetime lifetime,
            bool coalescePointerlessOwnerLabel,
            int currentSceneIdentityHash,
            int currentSceneBuildIndex,
            bool sceneIdentityOwnerDeclared)
        {
            if (bytes <= 0L)
                return 0;

            IntPtr pointerValue = (IntPtr)pointer;

            EnterMutationGate();
            try
            {
                for (int i = 0; i < _count; i++)
                {
                    NativeAllocationRecord existing = _records[i];
                    bool pointerMatches =
                        pointerValue != IntPtr.Zero &&
                        existing.Pointer == pointerValue &&
                        CanCoalesceAllocationRecord(in existing, lifetime, currentSceneIdentityHash);
                    bool pointerlessOwnerMatches =
                        coalescePointerlessOwnerLabel &&
                        pointerValue == IntPtr.Zero &&
                        existing.Pointer == IntPtr.Zero &&
                        existing.OwnerHash == ownerHash &&
                        existing.LabelHash == labelHash &&
                        FixedStringEquals(in existing.Owner, in ownerFixed) &&
                        FixedStringEquals(in existing.Label, in labelFixed) &&
                        CanCoalesceAllocationRecord(in existing, lifetime, currentSceneIdentityHash);
                    if (pointerMatches || pointerlessOwnerMatches)
                    {
                        bool recordChanged = false;
                        if (existing.Bytes != bytes)
                        {
                            TrackPersistentReallocationFixed(
                                in ownerFixed,
                                in labelFixed,
                                ownerHash,
                                labelHash,
                                bytes,
                                lifetime);
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
                            if (lifetime == NativeAllocationLifetime.Scene)
                            {
                                existing.SceneIdentityHash = currentSceneIdentityHash;
                                existing.SceneBuildIndex = currentSceneBuildIndex;
                                existing.SceneIdentityOwnerDeclared = sceneIdentityOwnerDeclared;
                            }
                            else
                            {
                                existing.SceneIdentityHash = 0;
                                existing.SceneBuildIndex = -1;
                                existing.SceneIdentityOwnerDeclared = false;
                            }

                            recordChanged = true;
                        }

                        if (lifetime == NativeAllocationLifetime.Scene &&
                            existing.SceneIdentityHash == currentSceneIdentityHash &&
                            existing.SceneBuildIndex != currentSceneBuildIndex)
                        {
                            existing.SceneBuildIndex = currentSceneBuildIndex;
                            recordChanged = true;
                        }

                        if (existing.LeakReported)
                        {
                            existing.LeakReported = false;
                            existing.AllocationFrame = ResolveCurrentFrame(existing.AllocationFrame);
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
                TrackPersistentReallocationFixed(
                    in ownerFixed,
                    in labelFixed,
                    ownerHash,
                    labelHash,
                    bytes,
                    lifetime);

                NativeAllocationRecord record = default;
                record.Id = id;
                record.Pointer = pointerValue;
                record.Bytes = bytes;
                record.AllocationFrame = ResolveCurrentFrame(0);
                record.Lifetime = lifetime;
                record.Allocator = ResolveAllocator(lifetime);
                record.OwnerHash = ownerHash;
                record.LabelHash = labelHash;
                record.Owner = ownerFixed;
                record.Label = labelFixed;
                if (lifetime == NativeAllocationLifetime.Scene)
                {
                    record.SceneIdentityHash = currentSceneIdentityHash;
                    record.SceneBuildIndex = currentSceneBuildIndex;
                    record.SceneIdentityOwnerDeclared = sceneIdentityOwnerDeclared;
                }
                else
                    record.SceneBuildIndex = -1;
                _records[_count++] = record;
                _trackedBytes += bytes;
                AdjustTransientAllocationCount(lifetime, 1);
                return id;
            }
            finally
            {
                ExitMutationGate();
            }
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
        /// Unregisters a raw persistent native pointer from the Core sentinel owner path.
        /// </summary>
        public static void UnregisterPointer(void* pointer)
        {
            UnregisterPointer((IntPtr)pointer);
        }

        /// <summary>
        /// Unregisters a raw persistent native pointer from reflection and editor bridge paths.
        /// </summary>
        public static void UnregisterPointer(IntPtr pointer)
        {
            IntPtr target = pointer;
            if (target == IntPtr.Zero)
                return;

            EnterMutationGate();
            try
            {
                for (int i = _count - 1; i >= 0; i--)
                {
                    if (_records[i].Pointer != target)
                        continue;

                    RemoveAt(i);
                    return;
                }
            }
            finally
            {
                ExitMutationGate();
            }
        }

        /// <summary>
        /// Unregisters a tracked allocation by stable registration id.
        /// </summary>
        public static void Unregister(int id)
        {
            if (id <= 0)
                return;

            EnterMutationGate();
            try
            {
                for (int i = _count - 1; i >= 0; i--)
                {
                    if (_records[i].Id != id)
                        continue;

                    RemoveAt(i);
                    return;
                }
            }
            finally
            {
                ExitMutationGate();
            }
        }

        /// <summary>
        /// Unregisters the latest matching owner/label record.
        /// </summary>
        public static void Unregister(string owner, string label)
        {
            uint ownerHash = ComputeStableHash(owner);
            uint labelHash = ComputeStableHash(label);
            FixedString128Bytes ownerFixed = ToFixedString128(owner);
            FixedString128Bytes labelFixed = ToFixedString128(label);
            UnregisterFixed(in ownerFixed, in labelFixed, ownerHash, labelHash, false, 0);
        }

        /// <summary>
        /// Unregisters the latest matching owner/label record for an explicit scene.
        /// </summary>
        public static void Unregister(string owner, string label, Scene scene)
        {
            uint ownerHash = ComputeStableHash(owner);
            uint labelHash = ComputeStableHash(label);
            FixedString128Bytes ownerFixed = ToFixedString128(owner);
            FixedString128Bytes labelFixed = ToFixedString128(label);
            ResolveSceneIdentity(scene, out int sceneIdentityHash, out _);
            UnregisterFixed(in ownerFixed, in labelFixed, ownerHash, labelHash, true, sceneIdentityHash);
        }

        /// <summary>
        /// Unregisters the latest matching fixed owner/label record without allocating strings.
        /// </summary>
        public static void Unregister(in FixedString128Bytes owner, in FixedString128Bytes label)
        {
            UnregisterFixed(
                in owner,
                in label,
                ComputeStableHash(in owner),
                ComputeStableHash(in label),
                false,
                0);
        }

        /// <summary>
        /// Unregisters the latest matching fixed owner/label record for an explicit scene.
        /// </summary>
        public static void Unregister(in FixedString128Bytes owner, in FixedString128Bytes label, Scene scene)
        {
            ResolveSceneIdentity(scene, out int sceneIdentityHash, out _);
            UnregisterFixed(
                in owner,
                in label,
                ComputeStableHash(in owner),
                ComputeStableHash(in label),
                true,
                sceneIdentityHash);
        }

        private static void UnregisterFixed(
            in FixedString128Bytes ownerFixed,
            in FixedString128Bytes labelFixed,
            uint ownerHash,
            uint labelHash,
            bool hasExplicitSceneIdentity,
            int explicitSceneIdentityHash)
        {
            int currentSceneIdentityHash = 0;
            bool currentSceneIdentityResolved = hasExplicitSceneIdentity;
            int fallbackSceneIndex = -1;
            int fallbackSceneMatchCount = 0;
            if (hasExplicitSceneIdentity)
                currentSceneIdentityHash = explicitSceneIdentityHash;

            EnterMutationGate();
            try
            {
                for (int i = _count - 1; i >= 0; i--)
                {
                    NativeAllocationRecord record = _records[i];
                    if (record.OwnerHash != ownerHash ||
                        record.LabelHash != labelHash ||
                        !FixedStringEquals(in record.Owner, in ownerFixed) ||
                        !FixedStringEquals(in record.Label, in labelFixed))
                    {
                        continue;
                    }

                    if (record.Lifetime == NativeAllocationLifetime.Scene)
                    {
                        if (!currentSceneIdentityResolved)
                        {
                            ResolveCurrentSceneIdentity(out currentSceneIdentityHash, out _);
                            currentSceneIdentityResolved = true;
                        }

                        if (record.SceneIdentityHash == currentSceneIdentityHash)
                        {
                            RemoveAt(i);
                            return;
                        }

                        if (!hasExplicitSceneIdentity)
                        {
                            fallbackSceneIndex = i;
                            fallbackSceneMatchCount++;
                        }

                        continue;
                    }

                    RemoveAt(i);
                    return;
                }

                if (!hasExplicitSceneIdentity && fallbackSceneMatchCount == 1)
                    RemoveAt(fallbackSceneIndex);
            }
            finally
            {
                ExitMutationGate();
            }
        }

        /// <summary>
        /// Reports scene-lifetime native allocations that survived a scene unload.
        /// </summary>
        public static void ReportSceneLifetimeLeaks(string context)
        {
            ReportSceneLifetimeLeaks(context, 0);
        }

        /// <summary>
        /// Reports scene-lifetime native allocations for an explicit scene context.
        /// </summary>
        public static void ReportSceneLifetimeLeaks(string context, Scene scene)
        {
            ResolveSceneIdentity(scene, out int sceneIdentityHash, out _);
            ReportSceneLifetimeLeaks(context, sceneIdentityHash);
        }

        private static void ReportSceneLifetimeLeaks(string context, int sceneIdentityHash)
        {
            int reported = 0;
            EnterSceneLeakReportGate();
            try
            {
                EnterMutationGate();
                try
                {
                    for (int i = 0; i < _count; i++)
                    {
                        NativeAllocationRecord record = _records[i];
                        if (record.LeakReported ||
                            record.Lifetime != NativeAllocationLifetime.Scene ||
                            !MatchesSceneLeakFilter(in record, sceneIdentityHash))
                        {
                            continue;
                        }

                        reported++;
                        record.LeakReported = true;
                        _records[i] = record;
                        _sceneLeakReportScratch[reported - 1] = record;
                    }
                }
                finally
                {
                    ExitMutationGate();
                }

                for (int i = 0; i < reported; i++)
                    PublishSceneLifetimeLeak(in _sceneLeakReportScratch[i]);

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
            finally
            {
                for (int i = 0; i < reported; i++)
                    _sceneLeakReportScratch[i] = default;

                ExitSceneLeakReportGate();
            }
        }

        private static void PublishSceneLifetimeLeak(in NativeAllocationRecord record)
        {
            Interlocked.Increment(ref _sceneLeakViolationCount);
            PublishPerformanceWarningNoReentry(
                _criticalMemoryViolationHash,
                _nativeMemoryContextHash,
                record.Bytes <= 0L ? 0f : record.Bytes > float.MaxValue ? float.MaxValue : (float)record.Bytes);
            uint allocationHash = ComputeOwnerLabelHash(record.OwnerHash, record.LabelHash);
            CrashTelemetryBuffer.ReportNativeTransientLeak(allocationHash, 0, record.Bytes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Volatile.Read(ref _diagnosticSceneLeakLogSuppressions) <= 0)
                Debug.LogError(DescribeSceneLifetimeLeak(record));
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Builds the editor-only leak line. The record already carries readable Owner and Label strings, the
        /// byte count, the allocating frame and the allocator; emitting only the bare constant threw all of
        /// that away and left a critical error that names nothing. A production run logged ten of these and
        /// the log could not say which ten allocations they were.
        /// </summary>
        private static string DescribeSceneLifetimeLeak(in NativeAllocationRecord record)
        {
            // COLD ALLOC: StringBuilder[192] - editor-only scene-unload leak report - owner: NativeMemorySentinel
            // Cold by construction: raised from HandleSceneUnloaded, never from a tick, and the Debug.LogError
            // it feeds allocates far more than this does.
            System.Text.StringBuilder builder = new System.Text.StringBuilder(192);
            builder.Append(CriticalMemoryViolationSceneLeakMessage);
            builder.Append(" owner=").Append(record.Owner.IsEmpty ? "<unnamed>" : record.Owner.ToString());
            builder.Append(" label=").Append(record.Label.IsEmpty ? "<unlabelled>" : record.Label.ToString());
            builder.Append(" bytes=").Append(record.Bytes);
            builder.Append(" allocator=").Append(record.Allocator);
            builder.Append(" allocFrame=").Append(record.AllocationFrame);
            builder.Append(" id=").Append(record.Id);
            builder.Append(" sceneIdentity=").Append(record.SceneIdentityHash);
            builder.Append(" sceneBuildIndex=").Append(record.SceneBuildIndex);
            builder.Append(" sceneScope=").Append(record.SceneIdentityOwnerDeclared
                ? SceneScopeOwnerDeclaredLabel
                : SceneScopeActiveSceneAtAllocLabel);
            builder.Append(" ownerHash=0x").Append(record.OwnerHash.ToString("X8"));
            builder.Append(" labelHash=0x").Append(record.LabelHash.ToString("X8"));
            return builder.ToString();
        }
#endif

        /// <summary>
        /// Fails closed when scene lifetime native allocations survive a scene unload.
        /// </summary>
        public static bool AssertNoSceneLifetimeAllocations(string context)
        {
            return AssertNoSceneLifetimeAllocations(context, 0);
        }

        /// <summary>
        /// Fails closed when an explicit scene still owns scene-lifetime native allocations.
        /// </summary>
        public static bool AssertNoSceneLifetimeAllocations(string context, Scene scene)
        {
            ResolveSceneIdentity(scene, out int sceneIdentityHash, out _);
            return AssertNoSceneLifetimeAllocations(context, sceneIdentityHash);
        }

        private static bool AssertNoSceneLifetimeAllocations(string context, int sceneIdentityHash)
        {
            int sceneAllocationCount = CountSceneLifetimeAllocations(sceneIdentityHash);
            if (sceneAllocationCount <= 0)
                return true;

            ReportSceneLifetimeLeaks(context, sceneIdentityHash);
            throw new FatalMemoryLeakException(BuildFatalLeakMessage(context, sceneAllocationCount, true, sceneIdentityHash));
        }

        /// <summary>
        /// Counts scene-lifetime allocations without mutating leak state.
        /// </summary>
        public static int CountSceneLifetimeAllocations()
        {
            return CountSceneLifetimeAllocations(0);
        }

        /// <summary>
        /// Counts scene-lifetime allocations for an explicit scene without mutating leak state.
        /// </summary>
        public static int CountSceneLifetimeAllocations(Scene scene)
        {
            ResolveSceneIdentity(scene, out int sceneIdentityHash, out _);
            return CountSceneLifetimeAllocations(sceneIdentityHash);
        }

        /// <summary>
        /// Diagnostic-only scene leak assertion used by editor stress probes.
        /// Does not publish telemetry, mark records as reported, or write Console errors.
        /// Runtime scene-unload validation must use AssertNoSceneLifetimeAllocations instead.
        /// </summary>
        public static bool AssertNoSceneLifetimeAllocationsForDiagnostics(string context)
        {
            int sceneAllocationCount = CountSceneLifetimeAllocations(0);
            if (sceneAllocationCount <= 0)
                return true;

            throw new FatalMemoryLeakException(BuildFatalLeakMessage(context, sceneAllocationCount, true, 0));
        }

        private static int CountSceneLifetimeAllocations(int sceneIdentityHash)
        {
            EnterMutationGate();
            try
            {
                int sceneAllocationCount = 0;
                for (int i = 0; i < _count; i++)
                {
                    NativeAllocationRecord record = _records[i];
                    if (record.Lifetime == NativeAllocationLifetime.Scene &&
                        MatchesSceneLeakFilter(in record, sceneIdentityHash))
                    {
                        sceneAllocationCount++;
                    }
                }

                return sceneAllocationCount;
            }
            finally
            {
                ExitMutationGate();
            }
        }

        /// <summary>
        /// Suppresses expected scene leak error logging during diagnostic probes only.
        /// Fatal exception and telemetry behavior remain active.
        /// </summary>
        public static void BeginDiagnosticSceneLeakLogSuppression()
        {
            Interlocked.Increment(ref _diagnosticSceneLeakLogSuppressions);
        }

        /// <summary>
        /// Ends a diagnostic scene leak log suppression scope.
        /// </summary>
        public static void EndDiagnosticSceneLeakLogSuppression()
        {
            int remaining = Interlocked.Decrement(ref _diagnosticSceneLeakLogSuppressions);
            if (remaining >= 0)
                return;

            Interlocked.Exchange(ref _diagnosticSceneLeakLogSuppressions, 0);
        }

        /// <summary>
        /// Diagnostic-only exact allocation lookup used by editor stress probes.
        /// </summary>
        public static bool ContainsTrackedAllocationForDiagnostics(
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            NativeAllocationLifetime lifetime,
            Scene scene)
        {
            ResolveSceneIdentity(scene, out int sceneIdentityHash, out _);
            return ContainsTrackedAllocationForDiagnostics(
                in owner,
                in label,
                ComputeStableHash(in owner),
                ComputeStableHash(in label),
                lifetime,
                true,
                sceneIdentityHash);
        }

        /// <summary>
        /// Diagnostic-only exact allocation lookup used by editor stress probes.
        /// </summary>
        public static bool ContainsTrackedAllocationForDiagnostics(
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            NativeAllocationLifetime lifetime)
        {
            return ContainsTrackedAllocationForDiagnostics(
                in owner,
                in label,
                ComputeStableHash(in owner),
                ComputeStableHash(in label),
                lifetime,
                false,
                0);
        }

        private static bool ContainsTrackedAllocationForDiagnostics(
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            uint ownerHash,
            uint labelHash,
            NativeAllocationLifetime lifetime,
            bool hasSceneIdentity,
            int sceneIdentityHash)
        {
            EnterMutationGate();
            try
            {
                for (int i = _count - 1; i >= 0; i--)
                {
                    NativeAllocationRecord record = _records[i];
                    if (record.Lifetime != lifetime ||
                        record.OwnerHash != ownerHash ||
                        record.LabelHash != labelHash ||
                        !FixedStringEquals(in record.Owner, in owner) ||
                        !FixedStringEquals(in record.Label, in label))
                    {
                        continue;
                    }

                    if (hasSceneIdentity &&
                        record.Lifetime == NativeAllocationLifetime.Scene &&
                        record.SceneIdentityHash != sceneIdentityHash)
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }
            finally
            {
                ExitMutationGate();
            }
        }
        /// <summary>
        /// Reports and force-frees scene-lifetime native arrays that survived a scene unload.
        /// </summary>
        public static int ReapSceneLifetimeLeaks(string context)
        {
            EnterMutationGate();
            try
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
                        if (Volatile.Read(ref _diagnosticSceneLeakLogSuppressions) <= 0)
                            Debug.LogError(DescribeSceneLifetimeLeak(record));
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
                        record.OwnerHash,
                        record.LabelHash,
                        record.Bytes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        NativeLeakReapedMessage +
                        " context=" + context +
                        " ownerHash=" + record.OwnerHash +
                        " labelHash=" + record.LabelHash);
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
            finally
            {
                ExitMutationGate();
            }
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
            throw new FatalMemoryLeakException(BuildFatalLeakMessage(context, activeCount));
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

            EnterMutationGate();
            try
            {
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
                    uint allocationHash = ComputeOwnerLabelHash(record.OwnerHash, record.LabelHash);
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
            finally
            {
                ExitMutationGate();
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
            ResolveSceneIdentity(scene, out int sceneIdentityHash, out _);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AssertNoSceneLifetimeAllocations(scene.name, sceneIdentityHash);
#else
            AssertNoSceneLifetimeAllocations(string.Empty, sceneIdentityHash);
#endif
        }

        private static void EnterMutationGate()
        {
            while (Interlocked.CompareExchange(ref _mutationGate, 1, 0) != 0)
                Thread.SpinWait(MutationGateSpinWait);

            Thread.MemoryBarrier();
        }

        private static void ExitMutationGate()
        {
            Thread.MemoryBarrier();
            Interlocked.Exchange(ref _mutationGate, 0);
        }

        private static void EnterSceneLeakReportGate()
        {
            while (Interlocked.CompareExchange(ref _sceneLeakReportGate, 1, 0) != 0)
                Thread.SpinWait(MutationGateSpinWait);

            Thread.MemoryBarrier();
        }

        private static void ExitSceneLeakReportGate()
        {
            Thread.MemoryBarrier();
            Interlocked.Exchange(ref _sceneLeakReportGate, 0);
        }

        private static string BuildFatalLeakMessage(string context, int activeCount)
        {
            return BuildFatalLeakMessage(context, activeCount, false);
        }

        private static string BuildFatalLeakMessage(string context, int activeCount, bool sceneOnly)
        {
            return BuildFatalLeakMessage(context, activeCount, sceneOnly, 0);
        }

        private static string BuildFatalLeakMessage(string context, int activeCount, bool sceneOnly, int sceneIdentityHash)
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append("FATAL_MEMORY_LEAK: context=");
            builder.Append(context ?? string.Empty);
            builder.Append(" active=");
            builder.Append(activeCount);

            EnterMutationGate();
            try
            {
                builder.Append(" trackedBytes=");
                builder.Append(_trackedBytes);
                int count = _count;
                for (int i = 0; i < count; i++)
                {
                    NativeAllocationRecord record = _records[i];
                    if (sceneOnly &&
                        (record.Lifetime != NativeAllocationLifetime.Scene ||
                         !MatchesSceneLeakFilter(in record, sceneIdentityHash)))
                    {
                        continue;
                    }

                    builder.Append(" | bufferId=");
                    builder.Append(record.Id);
                    builder.Append(" owner=");
                    AppendFixedString(builder, in record.Owner);
                    builder.Append(" label=");
                    AppendFixedString(builder, in record.Label);
                    builder.Append(" bytes=");
                    builder.Append(record.Bytes);
                    builder.Append(" lifetime=");
                    builder.Append((byte)record.Lifetime);
                    builder.Append(" sceneIdentity=");
                    builder.Append(record.SceneIdentityHash);
                    builder.Append(" sceneBuildIndex=");
                    builder.Append(record.SceneBuildIndex);
                    builder.Append(" sceneScope=");
                    builder.Append(record.SceneIdentityOwnerDeclared
                        ? SceneScopeOwnerDeclaredLabel
                        : SceneScopeActiveSceneAtAllocLabel);
                }
            }
            finally
            {
                ExitMutationGate();
            }

            return builder.ToString();
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

            uint ownerHash = ComputeStableHash(owner);
            uint labelHash = ComputeStableHash(label);
            FixedString128Bytes ownerFixed = ToFixedString128(owner);
            FixedString128Bytes labelFixed = ToFixedString128(label);
            int currentSceneIdentityHash = 0;
            int currentSceneBuildIndex = -1;
            bool currentSceneIdentityResolved = false;

            EnterMutationGate();
            try
            {
                for (int i = _count - 1; i >= 0; i--)
                {
                    NativeAllocationRecord record = _records[i];
                    if (record.Pointer != IntPtr.Zero ||
                        record.OwnerHash != ownerHash ||
                        record.LabelHash != labelHash ||
                        !FixedStringEquals(in record.Owner, in ownerFixed) ||
                        !FixedStringEquals(in record.Label, in labelFixed))
                    {
                        continue;
                    }

                    if (record.Lifetime == NativeAllocationLifetime.Scene)
                    {
                        if (!currentSceneIdentityResolved)
                        {
                            ResolveCurrentSceneIdentity(out currentSceneIdentityHash, out currentSceneBuildIndex);
                            currentSceneIdentityResolved = true;
                        }

                        if (!MatchesPointerlessRefreshScene(in record, currentSceneIdentityHash))
                            continue;
                    }

                    long delta = bytes - record.Bytes;
                    if (delta == 0L)
                    {
                        if (record.Lifetime == NativeAllocationLifetime.Scene &&
                            record.SceneIdentityHash == currentSceneIdentityHash &&
                            record.SceneBuildIndex != currentSceneBuildIndex)
                        {
                            record.SceneBuildIndex = currentSceneBuildIndex;
                            _records[i] = record;
                        }

                        return;
                    }

                    TrackPersistentReallocationFixed(
                        in ownerFixed,
                        in labelFixed,
                        ownerHash,
                        labelHash,
                        bytes,
                        record.Lifetime);
                    record.Bytes = bytes;
                    if (record.Lifetime == NativeAllocationLifetime.Scene &&
                        record.SceneIdentityHash == currentSceneIdentityHash &&
                        record.SceneBuildIndex != currentSceneBuildIndex)
                    {
                        record.SceneBuildIndex = currentSceneBuildIndex;
                    }

                    _records[i] = record;
                    _trackedBytes += delta;
                    return;
                }
            }
            finally
            {
                ExitMutationGate();
            }
        }

        private static void RefreshPointerlessBytes(int id, long bytes)
        {
            if (id <= 0 || bytes <= 0L)
                return;

            int currentSceneIdentityHash = 0;
            int currentSceneBuildIndex = -1;
            bool currentSceneIdentityResolved = false;

            EnterMutationGate();
            try
            {
                for (int i = _count - 1; i >= 0; i--)
                {
                    NativeAllocationRecord record = _records[i];
                    if (record.Id != id || record.Pointer != IntPtr.Zero)
                        continue;

                    if (record.Lifetime == NativeAllocationLifetime.Scene)
                    {
                        ResolveCurrentSceneIdentity(out currentSceneIdentityHash, out currentSceneBuildIndex);
                        currentSceneIdentityResolved = true;
                    }

                    long delta = bytes - record.Bytes;
                    if (delta != 0L)
                    {
                        FixedString128Bytes owner = record.Owner;
                        FixedString128Bytes label = record.Label;
                        TrackPersistentReallocationFixed(
                            in owner,
                            in label,
                            record.OwnerHash,
                            record.LabelHash,
                            bytes,
                            record.Lifetime);
                        record.Bytes = bytes;
                        _trackedBytes += delta;
                    }

                    if (currentSceneIdentityResolved &&
                        record.SceneIdentityHash == currentSceneIdentityHash &&
                        record.SceneBuildIndex != currentSceneBuildIndex)
                    {
                        record.SceneBuildIndex = currentSceneBuildIndex;
                    }

                    _records[i] = record;
                    return;
                }
            }
            finally
            {
                ExitMutationGate();
            }
        }

        private static int ResolveCurrentFrame(int fallbackFrame)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return fallbackFrame;

            return SystemDispatcher.ActiveRuntimeInstance != null ? SystemDispatcher.CurrentFrameIndex : fallbackFrame;
        }

        private static float ResolveCurrentUnscaledTime()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return 0f;

            return SystemDispatcher.ActiveRuntimeInstance != null ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f;
        }

        /// <summary>
        /// Resolves the ACTIVE scene, which is a proxy for ownership and not ownership itself. Nothing here
        /// asks who owns the allocation; there is no owner handle in a record, only strings.
        ///
        /// The proxy is provably wrong during an additive scene transition, and HECTON-8's menu-to-world
        /// transition is additive. SceneRuntimeService.LoadSceneAsync picks
        /// `LoadSceneMode.Additive` for the cinematic menu handoff, so 02_HECTON_WORLD loads while
        /// 01_MAIN_MENU is still the active scene; every world object's Awake/OnEnable therefore registers
        /// its buffers against the MENU. SetActiveScene(02_HECTON_WORLD) runs afterwards in
        /// CompleteMainMenuCinematicTransitionAsync, then UnloadSceneAsync(01_MAIN_MENU) fires
        /// HandleSceneUnloaded and every one of those records matches the menu's identity.
        ///
        /// That produced ten CRITICAL_MEMORY_VIOLATION scene-leak errors plus a FatalMemoryLeakException
        /// naming `context=01_MAIN_MENU active=10 sceneBuildIndex=1` against QuestStateManager and
        /// WorldProceduralScatterDirector.ScatterWorkingMemory - two live gameplay owners that dispose
        /// correctly and have no business in the main menu. The gate was right that ten Scene-lifetime
        /// records survived the menu unload; the scene it named was this guess.
        ///
        /// Do not "fix" that by loosening MatchesSceneLeakFilter or by skipping the stamp mid-transition:
        /// an unstamped record has SceneIdentityHash 0, which that filter matches against EVERY unload, and
        /// re-binding a survivor to the newly active scene would let a genuine menu-scene leak walk. The
        /// two correct fixes both live at the call site - an owner that outlives the scene declares
        /// NativeAllocationLifetime.Session (which is what QuestGraphEvaluator, QuestDagResolverRuntime and
        /// WorldProceduralFieldSampler, the direct siblings of the two leak owners, already declare), and an
        /// owner that really is scene-scoped passes its own Scene through the explicit-scene overload.
        ///
        /// API gap worth closing when a caller needs it: only RegisterPointer accepts an explicit Scene.
        /// RegisterNativeListInstance, RegisterNativeParallelMultiHashMapInstance and the other collection
        /// registrars do not, so a collection-backed additive-scene owner currently cannot follow the advice
        /// in the RegisterPointer(..., Scene) doc comment even when it wants to.
        /// </summary>
        private static void ResolveCurrentSceneIdentity(out int sceneIdentityHash, out int sceneBuildIndex)
        {
            sceneIdentityHash = 0;
            sceneBuildIndex = -1;
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return;

            ResolveSceneIdentity(SceneManager.GetActiveScene(), out sceneIdentityHash, out sceneBuildIndex);
        }

        /// <summary>
        /// Inferred registration binding: the caller named no scene, so the active scene stands in. Records
        /// written from here carry SceneIdentityOwnerDeclared = false and every report must say so.
        /// </summary>
        private static void ResolveRegistrationSceneIdentity(
            NativeAllocationLifetime lifetime,
            out int sceneIdentityHash,
            out int sceneBuildIndex)
        {
            sceneIdentityHash = 0;
            sceneBuildIndex = -1;
            if (lifetime != NativeAllocationLifetime.Scene)
                return;

            ResolveCurrentSceneIdentity(out sceneIdentityHash, out sceneBuildIndex);
        }

        private static void ResolveRegistrationSceneIdentity(
            NativeAllocationLifetime lifetime,
            Scene scene,
            out int sceneIdentityHash,
            out int sceneBuildIndex)
        {
            sceneIdentityHash = 0;
            sceneBuildIndex = -1;
            if (lifetime != NativeAllocationLifetime.Scene)
                return;

            ResolveSceneIdentity(scene, out sceneIdentityHash, out sceneBuildIndex);
        }

        private static void ResolveSceneIdentity(Scene scene, out int sceneIdentityHash, out int sceneBuildIndex)
        {
            sceneIdentityHash = 0;
            sceneBuildIndex = -1;
            if (!scene.IsValid())
                return;

            sceneIdentityHash = scene.GetHashCode();
            sceneBuildIndex = scene.buildIndex;
        }

        private static bool MatchesSceneLeakFilter(in NativeAllocationRecord record, int sceneIdentityHash)
        {
            return sceneIdentityHash == 0 ||
                   record.SceneIdentityHash == 0 ||
                   record.SceneIdentityHash == sceneIdentityHash;
        }

        private static bool CanCoalesceAllocationRecord(
            in NativeAllocationRecord record,
            NativeAllocationLifetime lifetime,
            int currentSceneIdentityHash)
        {
            if (record.Lifetime != NativeAllocationLifetime.Scene &&
                lifetime != NativeAllocationLifetime.Scene)
            {
                return true;
            }

            return record.Lifetime == NativeAllocationLifetime.Scene &&
                   lifetime == NativeAllocationLifetime.Scene &&
                   record.SceneIdentityHash == currentSceneIdentityHash;
        }

        private static bool MatchesPointerlessRefreshScene(
            in NativeAllocationRecord record,
            int currentSceneIdentityHash)
        {
            return record.Lifetime != NativeAllocationLifetime.Scene ||
                   record.SceneIdentityHash == currentSceneIdentityHash;
        }

        private static void TrackPersistentReallocation(
            string owner,
            string label,
            long bytes,
            NativeAllocationLifetime lifetime)
        {
            if (!IsPersistentLifetime(lifetime) || bytes <= 0L)
                return;

            uint ownerHash = ComputeStableHash(owner);
            uint labelHash = ComputeStableHash(label);
            FixedString128Bytes ownerFixed = ToFixedString128(owner);
            FixedString128Bytes labelFixed = ToFixedString128(label);
            TrackPersistentReallocationFixed(
                in ownerFixed,
                in labelFixed,
                ownerHash,
                labelHash,
                bytes,
                lifetime);
        }

        private static void TrackPersistentReallocationFixed(
            in FixedString128Bytes ownerFixed,
            in FixedString128Bytes labelFixed,
            uint ownerHash,
            uint labelHash,
            long bytes,
            NativeAllocationLifetime lifetime)
        {
            if (!IsPersistentLifetime(lifetime) || bytes <= 0L)
                return;

            float now = ResolveCurrentUnscaledTime();
            int recordIndex = FindPersistentReallocationRecord(
                in ownerFixed,
                in labelFixed,
                ownerHash,
                labelHash);
            if (recordIndex < 0)
            {
                if (_persistentReallocationRecordCount >= MaxPersistentReallocationRecords)
                    return;

                PersistentReallocationRecord freshRecord = default;
                freshRecord.Owner = ownerFixed;
                freshRecord.Label = labelFixed;
                freshRecord.OwnerHash = ownerHash;
                freshRecord.LabelHash = labelHash;
                freshRecord.LastBytes = bytes;
                freshRecord.WindowStartTime = now;
                _persistentReallocationRecords[_persistentReallocationRecordCount++] = freshRecord;
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
                uint allocationHash = ComputeOwnerLabelHash(ownerHash, labelHash);
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
                : ComputeStableHash(value.AsSpan());
        }

        private static uint ComputeStableHash(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = StableHashFnvOffset;
                for (int i = 0; i < value.Length; i++)
                    HashUtf16CodeUnit(ref hash, value[i]);

                return hash;
            }
        }

        private static uint ComputeStableHash(in FixedString128Bytes value)
        {
            if (value.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = StableHashFnvOffset;
                int cursor = 0;
                while (cursor < value.Length)
                {
                    if (TryReadUtf8Scalar(in value, cursor, out int scalar, out int consumed))
                    {
                        if (scalar <= 0xFFFF)
                        {
                            HashUtf16CodeUnit(ref hash, (char)scalar);
                        }
                        else
                        {
                            int supplementary = scalar - 0x10000;
                            HashUtf16CodeUnit(ref hash, (char)(0xD800 + (supplementary >> 10)));
                            HashUtf16CodeUnit(ref hash, (char)(0xDC00 + (supplementary & 0x3FF)));
                        }

                        cursor += consumed;
                        continue;
                    }

                    HashUtf16CodeUnit(ref hash, '\uFFFD');
                    cursor++;
                }

                return hash;
            }
        }

        private static void HashUtf16CodeUnit(ref uint hash, char current)
        {
            unchecked
            {
                hash ^= (byte)current;
                hash *= StableHashFnvPrime;
                hash ^= (byte)(current >> 8);
                hash *= StableHashFnvPrime;
            }
        }

        private static bool TryReadUtf8Scalar(
            in FixedString128Bytes value,
            int index,
            out int scalar,
            out int consumed)
        {
            scalar = 0;
            consumed = 1;
            byte lead = value[index];
            if (lead < 0x80)
            {
                scalar = lead;
                return true;
            }

            if ((lead & 0xE0) == 0xC0)
            {
                if (index + 1 >= value.Length || !IsUtf8Continuation(value[index + 1]))
                    return false;

                scalar = ((lead & 0x1F) << 6) | (value[index + 1] & 0x3F);
                if (scalar < 0x80)
                    return false;

                consumed = 2;
                return true;
            }

            if ((lead & 0xF0) == 0xE0)
            {
                if (index + 2 >= value.Length ||
                    !IsUtf8Continuation(value[index + 1]) ||
                    !IsUtf8Continuation(value[index + 2]))
                {
                    return false;
                }

                scalar = ((lead & 0x0F) << 12) |
                         ((value[index + 1] & 0x3F) << 6) |
                         (value[index + 2] & 0x3F);
                if (scalar < 0x800 || (scalar >= 0xD800 && scalar <= 0xDFFF))
                    return false;

                consumed = 3;
                return true;
            }

            if ((lead & 0xF8) == 0xF0)
            {
                if (index + 3 >= value.Length ||
                    !IsUtf8Continuation(value[index + 1]) ||
                    !IsUtf8Continuation(value[index + 2]) ||
                    !IsUtf8Continuation(value[index + 3]))
                {
                    return false;
                }

                scalar = ((lead & 0x07) << 18) |
                         ((value[index + 1] & 0x3F) << 12) |
                         ((value[index + 2] & 0x3F) << 6) |
                         (value[index + 3] & 0x3F);
                if (scalar < 0x10000 || scalar > 0x10FFFF)
                    return false;

                consumed = 4;
                return true;
            }

            return false;
        }

        private static bool IsUtf8Continuation(byte value)
        {
            return (value & 0xC0) == 0x80;
        }

        private static FixedString128Bytes ToFixedString128(string value)
        {
            FixedString128Bytes fixedValue = default;
            if (!string.IsNullOrEmpty(value))
                fixedValue.CopyFromTruncated(value);
            return fixedValue;
        }

        private static bool FixedStringEquals(in FixedString128Bytes left, in FixedString128Bytes right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static void AppendFixedString(StringBuilder builder, in FixedString128Bytes value)
        {
            int cursor = 0;
            while (cursor < value.Length)
            {
                if (TryReadUtf8Scalar(in value, cursor, out int scalar, out int consumed))
                {
                    if (scalar <= 0xFFFF)
                    {
                        builder.Append((char)scalar);
                    }
                    else
                    {
                        int supplementary = scalar - 0x10000;
                        builder.Append((char)(0xD800 + (supplementary >> 10)));
                        builder.Append((char)(0xDC00 + (supplementary & 0x3FF)));
                    }

                    cursor += consumed;
                    continue;
                }

                builder.Append('\uFFFD');
                cursor++;
            }
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

        private static int FindPersistentReallocationRecord(
            in FixedString128Bytes owner,
            in FixedString128Bytes label,
            uint ownerHash,
            uint labelHash)
        {
            for (int i = 0; i < _persistentReallocationRecordCount; i++)
            {
                PersistentReallocationRecord record = _persistentReallocationRecords[i];
                if (record.OwnerHash == ownerHash &&
                    record.LabelHash == labelHash &&
                    FixedStringEquals(in record.Owner, in owner) &&
                    FixedStringEquals(in record.Label, in label))
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

        private static uint ComputeOwnerLabelHash(uint ownerHash, uint labelHash)
        {
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

    }
}
