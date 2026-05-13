using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Stable native-memory owner identifiers. Values below 256 are reserved for registry service slots.
    /// </summary>
    public enum SystemID : ushort
    {
        Unknown = 0,
        CoreDataVault = 1,
        H8Memory = 2,
        Bootstrap = 3,
        CoreDeterminism = 4,
        SystemDispatcher = 30,
        GlobalPhysicsStateManager = 32,
        Physics = 64,
        VehiclesPhysics = 65,
        Fluid = 66,
        GameplayLoot = 67,
        WorldStreaming = 128,
        TerrainSeams = 129,
        SimulationBucketer = 161,
        Vfx = 192,
        UI = 224,
        External = 65534
    }

    /// <summary>
    /// Allocation-free global data-vault buffer identifiers.
    /// </summary>
    public enum BufferID : int
    {
        Unknown = 0,
        Silt = 1,
        RigidbodyAUPs = 2,
        RigidbodyCullingState = 3,
        RigidbodyAwakeResults = 4,
        RigidbodyCullingCommands = 5,
        RigidbodyDistanceSq = 6,
        PhysicsCullingTelemetry = 7,
        DispatcherRaycastHits = 8,
        H8Time = 9,
        TerrainSeamHeightmap = 10,
        PlayerKinematicState = 11,
        RoomWaterLevels = 12,
        EntityAUPs = 13,
        VoxelSdfTexture3D = 14,
        RoomVolumes = 15,
        RoomLocalAUPs = 16,
        OceanGerstnerWaves = 17,
        OceanGerstnerWaveMeta = 18,
        WfcOutpostGrid = 19,
        LoreEntityAUPs = 20,
        LoreEntityHashes = 21,
        SubmarineBallastFill01 = 22,
        SubmarineBallastTankLocalPositions = 23,
        SubmarineBallastPidOutput = 24,
        SubmarineDynamicFloodMassOutput = 25,
        SubmarinePidTelemetry = 26,
        CarveDebris = 27,
        CarveDebrisVelocity = 28,
        EntityFlags = 29,
        EntityVelocities = 30,
        EntityItemHashes = 31,
        EntityQuantities = 32,
        EntityLootMagnetTelemetry = 33
    }

    [Flags]
    public enum H8AllocationFlags : ushort
    {
        None = 0,
        NativeArray = 1 << 0,
        Raw = 1 << 1,
        Vault = 1 << 2,
        Alias = 1 << 3,
        Freed = 1 << 4,
        Relocatable = 1 << 5,
        SubAllocatorRoot = 1 << 6
    }

    public enum H8BlockState : byte
    {
        Free = 0,
        Occupied = 1
    }

    /// <summary>
    /// Native memory-map descriptor for occupied/free regions owned by <see cref="H8Memory"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BlockDescriptor
    {
        public IntPtr BasePointer;
        public long OffsetBytes;
        public long Bytes;
        public int OwnerKey;
        public int Generation;
        public SystemID Owner;
        public ushort Flags;
        public byte State;
        public byte Reserved;
    }

    /// <summary>
    /// Blittable record copied to crash dumps and leak-reap passes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct H8AllocationRecord
    {
        public IntPtr Pointer;
        public long Bytes;
        public int Length;
        public int Stride;
        public int Alignment;
        public int AllocationIndex;
        public SystemID Owner;
        public Allocator Allocator;
        public ushort Flags;
        public ushort Reserved;
    }

    public sealed class FatalMemoryException : InvalidOperationException
    {
        private FatalMemoryException(string message) : base(message)
        {
        }

        public static void ThrowUnknownFreeOwner()
        {
            throw new FatalMemoryException("H8Memory free owner is unknown.");
        }

        public static void ThrowWrongFreeOwner()
        {
            throw new FatalMemoryException("H8Memory free owner mismatch.");
        }

        public static void ThrowUntrackedPointer()
        {
            throw new FatalMemoryException("H8Memory free pointer is untracked.");
        }

        public static void ThrowStaleVaultHandle()
        {
            throw new FatalMemoryException("GlobalDataVault handle generation mismatch.");
        }
    }

    /// <summary>
    /// Zero-managed-hot-path memory sentinel for native allocations.
    /// </summary>
    public static unsafe class H8Memory
    {
        private const int DefaultCapacity = 4096;
        private const int MaxTrackingCapacity = 65536;
        private const int OwnerByteSlots = 65536;
        private const long LowTierPoolCapBytes = 512L * 1024L * 1024L;

        private static NativeParallelHashMap<long, SystemID> _allocationOwners;
        private static NativeArray<H8AllocationRecord> _records;
        private static NativeArray<long> _ownerBytes;
        private static NativeList<BlockDescriptor> _blockDescriptors;
        private static int _recordCount;
        private static long _totalBytes;
        private static long _poolCapBytes = LowTierPoolCapBytes;
        private static int _fatalLeakPreventedCount;
        private static bool _initialized;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static AtomicSafetyHandle _aliasSafetyHandle;
        private static bool _aliasSafetyHandleCreated;
#endif

        /// <summary>Tracked allocation count.</summary>
        public static int ActiveAllocationCount => _recordCount;

        /// <summary>Total tracked bytes.</summary>
        public static long TotalBytes => _totalBytes;

        /// <summary>Tracked memory-map descriptor count.</summary>
        public static int BlockDescriptorCount => _blockDescriptors.IsCreated ? _blockDescriptors.Length : 0;

        /// <summary>Configured native pool cap in bytes.</summary>
        public static long PoolCapBytes => _poolCapBytes;

        /// <summary>Number of owner-unregister leaks force-reaped by the sentinel.</summary>
        public static int FatalLeakPreventedCount => _fatalLeakPreventedCount;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorShutdownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.EditorApplication.quitting -= Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
        }
#endif

        /// <summary>
        /// Initializes native tracking tables. Safe to call more than once.
        /// </summary>
        public static void Initialize(int capacity = DefaultCapacity, long poolCapBytes = LowTierPoolCapBytes)
        {
            if (_initialized)
                return;

            int safeCapacity = capacity > 0 ? capacity : DefaultCapacity;
            _allocationOwners = new NativeParallelHashMap<long, SystemID>(safeCapacity, Allocator.Persistent);
            _records = new NativeArray<H8AllocationRecord>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _ownerBytes = new NativeArray<long>(OwnerByteSlots, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _blockDescriptors = new NativeList<BlockDescriptor>(safeCapacity, Allocator.Persistent);
            _recordCount = 0;
            _totalBytes = 0L;
            _poolCapBytes = poolCapBytes > 0L ? poolCapBytes : LowTierPoolCapBytes;
            _fatalLeakPreventedCount = 0;
            _initialized = true;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _aliasSafetyHandle = AtomicSafetyHandle.Create();
            _aliasSafetyHandleCreated = true;
#endif
        }

        /// <summary>
        /// Allocates a native array and records its owner before it can be exposed to jobs.
        /// </summary>
        public static NativeArray<T> Allocate<T>(
            int length,
            SystemID owner,
            Allocator allocator,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            if (!_initialized)
                Initialize();

            if (length <= 0)
                return default;

            int stride = UnsafeUtility.SizeOf<T>();
            long bytes = (long)stride * length;
            if (!TryReserveBytes(owner, bytes) || !EnsureTrackingCapacity())
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            RegisterPointer(pointer, bytes, length, stride, UnsafeUtility.AlignOf<T>(), owner, allocator, H8AllocationFlags.NativeArray);
            return array;
        }

        /// <summary>
        /// Releases a native array allocated by <see cref="Allocate{T}"/> and removes it from the leak tracker.
        /// </summary>
        public static void Release<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnregisterPointer(pointer);
            array.Dispose();
            array = default;
        }

        /// <summary>
        /// Defers native-array disposal behind an active job dependency and retires leak ownership immediately.
        /// </summary>
        public static JobHandle Release<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return dependency;

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnregisterPointer(pointer);
            JobHandle disposeHandle = array.Dispose(dependency);
            array = default;
            return disposeHandle;
        }

        /// <summary>
        /// Allocates raw native memory for vault-owned buffers.
        /// </summary>
        public static void* AllocateRaw(
            long bytes,
            int alignment,
            SystemID owner,
            Allocator allocator,
            bool clearMemory,
            H8AllocationFlags extraFlags = H8AllocationFlags.None)
        {
            if (!_initialized)
                Initialize();

            if (bytes <= 0L)
                return null;

            int safeAlignment = alignment > 0 ? alignment : 16;
            if (!TryReserveBytes(owner, bytes) || !EnsureTrackingCapacity())
                return null;

            void* pointer = UnsafeUtility.Malloc(bytes, safeAlignment, allocator);
            if (pointer == null)
                return null;

            if (clearMemory)
                UnsafeUtility.MemClear(pointer, bytes);

            RegisterPointer(pointer, bytes, 0, 0, safeAlignment, owner, allocator, H8AllocationFlags.Raw | extraFlags);
            return pointer;
        }

        /// <summary>
        /// Reallocates a raw vault buffer with copy/free semantics and refreshed sentinel ownership.
        /// </summary>
        public static void* ReallocateRaw(
            void* oldPointer,
            long oldBytes,
            long newBytes,
            int alignment,
            SystemID owner,
            Allocator allocator,
            bool clearExtendedBytes,
            H8AllocationFlags extraFlags = H8AllocationFlags.None)
        {
            if (!_initialized)
                Initialize();

            if (newBytes <= 0L)
                return null;

            if (oldPointer == null || oldBytes <= 0L)
                return AllocateRaw(newBytes, alignment, owner, allocator, clearExtendedBytes, extraFlags);

            int safeAlignment = alignment > 0 ? alignment : 16;
            if (!TryReserveReplacementBytes(oldBytes, newBytes) || !EnsureTrackingCapacity())
                return null;

            void* newPointer = UnsafeUtility.Malloc(newBytes, safeAlignment, allocator);
            if (newPointer == null)
                return null;

            long copyBytes = oldBytes < newBytes ? oldBytes : newBytes;
            UnsafeUtility.MemMove(newPointer, oldPointer, copyBytes);
            if (clearExtendedBytes && newBytes > copyBytes)
                UnsafeUtility.MemClear((byte*)newPointer + copyBytes, newBytes - copyBytes);

            UnregisterPointer(oldPointer, owner);
            UnsafeUtility.Free(oldPointer, allocator);
            RegisterPointer(newPointer, newBytes, 0, 0, safeAlignment, owner, allocator, H8AllocationFlags.Raw | extraFlags);

            return newPointer;
        }

        /// <summary>
        /// Frees raw native memory and removes it from the leak tracker.
        /// </summary>
        public static void FreeRaw(void* pointer, Allocator allocator)
        {
            FreeRaw(pointer, allocator, SystemID.Unknown);
        }

        /// <summary>
        /// Frees raw native memory only when the caller matches the recorded allocation owner.
        /// </summary>
        public static void FreeRaw(void* pointer, Allocator allocator, SystemID requester)
        {
            if (pointer == null)
                return;

            UnregisterPointer(pointer, requester);
            UnsafeUtility.Free(pointer, allocator);
        }

        /// <summary>
        /// Creates a read-only alias over an existing buffer without copying.
        /// </summary>
        public static NativeArray<T>.ReadOnly CreateAlias<T>(NativeArray<T> source, SystemID reader) where T : struct
        {
            if (!source.IsCreated)
                return default;

            return source.AsReadOnly();
        }

        /// <summary>
        /// Creates a read-only alias over raw vault memory without copying.
        /// </summary>
        internal static NativeArray<T>.ReadOnly CreateAlias<T>(void* pointer, int length, SystemID reader) where T : struct
        {
            NativeArray<T> array = CreateNativeArrayView<T>(pointer, length);
            return array.AsReadOnly();
        }

        /// <summary>
        /// Converts owned raw memory into a NativeArray view.
        /// </summary>
        internal static NativeArray<T> CreateNativeArrayView<T>(void* pointer, int length) where T : struct
        {
            if (pointer == null || length <= 0)
                return default;

            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(pointer, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_aliasSafetyHandleCreated)
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, _aliasSafetyHandle);
#endif
            return array;
        }

        /// <summary>
        /// Force-frees all tracked memory for an unregistered owner.
        /// </summary>
        public static int ReapOwnerLeaks(SystemID owner)
        {
            if (!_initialized || owner == SystemID.Unknown)
                return 0;

            int reaped = 0;
            for (int index = _recordCount - 1; index >= 0; index--)
            {
                H8AllocationRecord record = _records[index];
                if (record.Owner != owner || record.Pointer == IntPtr.Zero)
                    continue;

                UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
                RemoveRecordAt(index);
                reaped++;
            }

            _fatalLeakPreventedCount += reaped;
            return reaped;
        }

        /// <summary>
        /// Dumps the current allocation table to a text file for post-mortem triage.
        /// </summary>
        public static bool DumpAllocationTableText(string path)
        {
            if (!_initialized || string.IsNullOrEmpty(path))
                return false;

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("H8MEMORY_ALLOCATION_TABLE");
                writer.Write("TotalBytes=");
                writer.WriteLine(_totalBytes);
                writer.Write("ActiveAllocationCount=");
                writer.WriteLine(_recordCount);
                for (int i = 0; i < _recordCount; i++)
                {
                    H8AllocationRecord record = _records[i];
                    writer.Write("Index=");
                    writer.Write(record.AllocationIndex);
                    writer.Write(" Ptr=");
                    writer.Write(record.Pointer.ToInt64());
                    writer.Write(" Bytes=");
                    writer.Write(record.Bytes);
                    writer.Write(" Owner=");
                    writer.Write((int)record.Owner);
                    writer.Write(" Allocator=");
                    writer.Write((int)record.Allocator);
                    writer.Write(" Flags=");
                    writer.WriteLine(record.Flags);
                }
            }

            return true;
        }

        /// <summary>
        /// Registers or reuses a memory-map descriptor slot. Cold path only.
        /// </summary>
        public static int RegisterBlockDescriptor(in BlockDescriptor descriptor)
        {
            if (!_initialized)
                Initialize();

            return RegisterBlockDescriptorNoInit(in descriptor);
        }

        /// <summary>
        /// Updates a memory-map descriptor in-place.
        /// </summary>
        public static bool TryUpdateBlockDescriptor(int index, in BlockDescriptor descriptor)
        {
            if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                return false;

            _blockDescriptors[index] = descriptor;
            return true;
        }

        /// <summary>
        /// Reads a memory-map descriptor without allocation.
        /// </summary>
        public static bool TryGetBlockDescriptor(int index, out BlockDescriptor descriptor)
        {
            descriptor = default;
            if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                return false;

            descriptor = _blockDescriptors[index];
            return true;
        }

        /// <summary>
        /// Shuts down tracking tables. Only call from service shutdown after users released their buffers.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
                return;

            for (int i = _recordCount - 1; i >= 0; i--)
            {
                H8AllocationRecord record = _records[i];
                if (record.Pointer != IntPtr.Zero)
                    UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
            }

            _recordCount = 0;
            _totalBytes = 0L;
            if (_allocationOwners.IsCreated)
                _allocationOwners.Dispose();
            if (_records.IsCreated)
                _records.Dispose();
            if (_ownerBytes.IsCreated)
                _ownerBytes.Dispose();
            if (_blockDescriptors.IsCreated)
                _blockDescriptors.Dispose();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_aliasSafetyHandleCreated)
            {
                AtomicSafetyHandle.Release(_aliasSafetyHandle);
                _aliasSafetyHandleCreated = false;
            }
#endif
            _initialized = false;
        }

        private static bool TryReserveBytes(SystemID owner, long bytes)
        {
            if (bytes <= 0L)
                return false;

            if (_poolCapBytes > 0L && bytes > _poolCapBytes - _totalBytes)
                return false;

            return true;
        }

        private static bool TryReserveReplacementBytes(long oldBytes, long newBytes)
        {
            if (newBytes <= 0L)
                return false;

            if (_poolCapBytes <= 0L)
                return true;

            long retainedBytes = _totalBytes > oldBytes ? _totalBytes - oldBytes : 0L;
            return newBytes <= _poolCapBytes - retainedBytes;
        }

        private static bool EnsureTrackingCapacity()
        {
            if (_recordCount < _records.Length)
                return true;

            int oldCapacity = _records.Length;
            if (oldCapacity >= MaxTrackingCapacity)
                return false;

            int newCapacity = oldCapacity > 0 ? oldCapacity << 1 : DefaultCapacity;
            if (newCapacity < oldCapacity || newCapacity > MaxTrackingCapacity)
                newCapacity = MaxTrackingCapacity;

            NativeArray<H8AllocationRecord> newRecords =
                new NativeArray<H8AllocationRecord>(newCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeParallelHashMap<long, SystemID> newOwners =
                new NativeParallelHashMap<long, SystemID>(newCapacity, Allocator.Persistent);

            for (int i = 0; i < _recordCount; i++)
            {
                H8AllocationRecord record = _records[i];
                newRecords[i] = record;
                if (record.Pointer != IntPtr.Zero)
                    newOwners.TryAdd(record.Pointer.ToInt64(), record.Owner);
            }

            if (_records.IsCreated)
                _records.Dispose();
            if (_allocationOwners.IsCreated)
                _allocationOwners.Dispose();

            _records = newRecords;
            _allocationOwners = newOwners;
            EnsureBlockDescriptorCapacity(newCapacity);
            return true;
        }

        private static void RegisterPointer(
            void* pointer,
            long bytes,
            int length,
            int stride,
            int alignment,
            SystemID owner,
            Allocator allocator,
            H8AllocationFlags flags)
        {
            if (pointer == null || bytes <= 0L || _recordCount >= _records.Length)
                return;

            IntPtr pointerValue = (IntPtr)pointer;
            H8AllocationRecord record = new H8AllocationRecord
            {
                Pointer = pointerValue,
                Bytes = bytes,
                Length = length,
                Stride = stride,
                Alignment = alignment,
                AllocationIndex = _recordCount,
                Owner = owner,
                Allocator = allocator,
                Flags = (ushort)flags
            };

            _records[_recordCount++] = record;
            _allocationOwners.TryAdd(pointerValue.ToInt64(), owner);
            _totalBytes += bytes;
            int ownerIndex = (int)owner;
            if ((uint)ownerIndex < (uint)_ownerBytes.Length)
                _ownerBytes[ownerIndex] += bytes;

            if ((flags & H8AllocationFlags.SubAllocatorRoot) != 0)
                return;

            RegisterBlockDescriptorNoInit(new BlockDescriptor
            {
                BasePointer = pointerValue,
                OffsetBytes = 0L,
                Bytes = bytes,
                OwnerKey = record.AllocationIndex,
                Generation = 1,
                Owner = owner,
                Flags = (ushort)flags,
                State = (byte)H8BlockState.Occupied
            });
        }

        private static void UnregisterPointer(void* pointer)
        {
            UnregisterPointer(pointer, SystemID.Unknown, requireOwnerMatch: false);
        }

        private static void UnregisterPointer(void* pointer, SystemID requester)
        {
            UnregisterPointer(pointer, requester, requireOwnerMatch: true);
        }

        private static void UnregisterPointer(void* pointer, SystemID requester, bool requireOwnerMatch)
        {
            if (!_initialized || pointer == null)
                return;

            if (requireOwnerMatch && requester == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownFreeOwner();

            long pointerKey = ((IntPtr)pointer).ToInt64();
            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (_records[i].Pointer.ToInt64() != pointerKey)
                    continue;

                if (requireOwnerMatch && _records[i].Owner != requester)
                    FatalMemoryException.ThrowWrongFreeOwner();

                RemoveRecordAt(i);
                return;
            }

            if (requireOwnerMatch)
                FatalMemoryException.ThrowUntrackedPointer();
        }

        private static void RemoveRecordAt(int index)
        {
            H8AllocationRecord record = _records[index];
            _allocationOwners.Remove(record.Pointer.ToInt64());
            MarkBlockDescriptorFree(record.Pointer, 0L);
            _totalBytes -= record.Bytes;
            int ownerIndex = (int)record.Owner;
            if ((uint)ownerIndex < (uint)_ownerBytes.Length)
                _ownerBytes[ownerIndex] -= record.Bytes;

            _recordCount--;
            if (index != _recordCount)
            {
                H8AllocationRecord moved = _records[_recordCount];
                moved.AllocationIndex = index;
                _records[index] = moved;
                UpdateBlockDescriptorOwnerKey(moved.Pointer, 0L, index);
            }

            _records[_recordCount] = default;
        }

        private static int RegisterBlockDescriptorNoInit(in BlockDescriptor descriptor)
        {
            if (!_blockDescriptors.IsCreated)
                return -1;

            for (int i = 0; i < _blockDescriptors.Length; i++)
            {
                BlockDescriptor existing = _blockDescriptors[i];
                if (existing.Bytes != 0L)
                    continue;

                _blockDescriptors[i] = descriptor;
                return i;
            }

            if (_blockDescriptors.Length >= _blockDescriptors.Capacity)
                return -1;

            int index = _blockDescriptors.Length;
            _blockDescriptors.AddNoResize(descriptor);
            return index;
        }

        private static void EnsureBlockDescriptorCapacity(int requiredCapacity)
        {
            if (!_blockDescriptors.IsCreated || requiredCapacity <= _blockDescriptors.Capacity)
                return;

            _blockDescriptors.Capacity = requiredCapacity;
        }

        private static void MarkBlockDescriptorFree(IntPtr basePointer, long offsetBytes)
        {
            if (!_blockDescriptors.IsCreated || basePointer == IntPtr.Zero)
                return;

            for (int i = _blockDescriptors.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descriptor = _blockDescriptors[i];
                if (descriptor.BasePointer != basePointer || descriptor.OffsetBytes != offsetBytes)
                    continue;

                descriptor.State = (byte)H8BlockState.Free;
                descriptor.Flags |= (ushort)H8AllocationFlags.Freed;
                descriptor.Generation++;
                _blockDescriptors[i] = descriptor;
                return;
            }
        }

        private static void UpdateBlockDescriptorOwnerKey(IntPtr basePointer, long offsetBytes, int ownerKey)
        {
            if (!_blockDescriptors.IsCreated || basePointer == IntPtr.Zero)
                return;

            for (int i = _blockDescriptors.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descriptor = _blockDescriptors[i];
                if (descriptor.BasePointer != basePointer || descriptor.OffsetBytes != offsetBytes)
                    continue;

                if (descriptor.State != (byte)H8BlockState.Occupied)
                    return;

                descriptor.OwnerKey = ownerKey;
                descriptor.Generation++;
                _blockDescriptors[i] = descriptor;
                return;
            }
        }
    }
}
