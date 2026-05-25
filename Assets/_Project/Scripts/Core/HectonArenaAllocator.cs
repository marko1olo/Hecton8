using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Double-buffered unmanaged bump arena for frame-transient scratch buffers.
    /// </summary>
    public static unsafe class HectonArenaAllocator
    {
        public const int DefaultArenaBytes = 100 * 1024 * 1024;
        public const int CacheLineAlignment = 64;
        public const int MinimumAllocationAlignment = 16;
        public const uint ArenaOomHash = 0xA0E00A0Fu;

        private const int ArenaBufferCount = 2;
        private const int MaxArenaAlignment = 4096;
        private const int MaxSlabCount = 64;
        private const int OwnerTelemetryCapacity = 32;
        private const string BudgetOwner = nameof(HectonArenaAllocator);
        private const uint DefaultOwnerHash = 0x41524E41u; // ARNA
        private const uint ArenaContextHash = 0x41524E32u; // ARN2

        private static readonly ProfilerMarker _resetProfilerMarker = new ProfilerMarker("H8.Core.HectonArena.EndFrameSwap");

        private static byte* _basePtr;
        private static int _capacityBytes;
        private static int _arenaCapacityBytes;
        private static int _slabCapacityBytes;
        private static int _slabCount;
        private static int _writeArenaIndex;
        private static int _readArenaIndex = 1;
        private static int _initializing;
        private static int _sentinelId;
        private static int _frameSequence;
        private static int _lastFrameHighWaterBytes;
        private static int _oomCount;
        private static int _nextThreadSlab;

        // COLD ALLOC: int[2 * processorCount] - per-arena TLS slab cursors - owner: HectonArenaAllocator
        private static int[] _slabCursorBytes;
        // COLD ALLOC: int[2 * processorCount] - per-arena high-water marks - owner: HectonArenaAllocator
        private static int[] _slabHighWaterBytes;
        // COLD ALLOC: int[32] - fixed owner telemetry keys - owner: HectonArenaAllocator
        private static int[] _ownerHashes;
        // COLD ALLOC: int[32] - current-frame owner byte totals - owner: HectonArenaAllocator
        private static int[] _ownerFrameBytes;
        // COLD ALLOC: int[32] - last-frame owner byte totals - owner: HectonArenaAllocator
        private static int[] _ownerLastFrameBytes;
        // COLD ALLOC: int[32] - owner high-water byte totals - owner: HectonArenaAllocator
        private static int[] _ownerHighWaterBytes;

        [ThreadStatic] private static int _threadSlabIndexPlusOne;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // COLD ALLOC: AtomicSafetyHandle[2] - per-buffer frame-lifetime safety handles - owner: HectonArenaAllocator
        private static AtomicSafetyHandle[] _arenaSafetyHandles;
        // COLD ALLOC: bool[2] - safety handle lifetime flags - owner: HectonArenaAllocator
        private static bool[] _arenaSafetyHandleCreated;
#endif

#if UNITY_EDITOR
        // COLD ALLOC: int[2 * processorCount] - editor-only allocation counts - owner: HectonArenaAllocator
        private static int[] _editorAllocationCounts;
        // COLD ALLOC: int[2 * processorCount] - editor-only allocation byte totals - owner: HectonArenaAllocator
        private static int[] _editorAllocationBytes;
        // COLD ALLOC: int[2 * processorCount] - editor-only previous allocation ends - owner: HectonArenaAllocator
        private static int[] _editorLastAllocationEndBytes;
#endif

        public static bool IsCreated => _basePtr != null;
        public static int CapacityBytes => Volatile.Read(ref _capacityBytes);
        public static int WriteCapacityBytes => Volatile.Read(ref _arenaCapacityBytes);
        public static int ReadCapacityBytes => Volatile.Read(ref _arenaCapacityBytes);
        public static int SlabCount => Volatile.Read(ref _slabCount);
        public static int SlabCapacityBytes => Volatile.Read(ref _slabCapacityBytes);
        public static int UsedBytes => SumArenaUsage(Volatile.Read(ref _writeArenaIndex));
        public static int LastFrameHighWaterBytes => Volatile.Read(ref _lastFrameHighWaterBytes);
        public static int OomCount => Volatile.Read(ref _oomCount);
        public static int CurrentFrameSequence => Volatile.Read(ref _frameSequence);
        internal static IntPtr GlobalArenaPtr => (IntPtr)_basePtr;
        internal static IntPtr WriteArenaPtr => (IntPtr)GetArenaBasePtr(Volatile.Read(ref _writeArenaIndex));
        internal static IntPtr ReadArenaPtr => (IntPtr)GetArenaBasePtr(Volatile.Read(ref _readArenaIndex));

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

                int resolvedSlabCount = ResolveSlabCount();
                int safeCapacity = ResolveAlignedCapacity(capacityBytes, resolvedSlabCount, out int arenaCapacity, out int slabCapacity);
                _slabCount = resolvedSlabCount;
                _capacityBytes = safeCapacity;
                _arenaCapacityBytes = arenaCapacity;
                _slabCapacityBytes = slabCapacity;
                _writeArenaIndex = 0;
                _readArenaIndex = 1;
                _frameSequence = 0;
                _nextThreadSlab = 0;
                _threadSlabIndexPlusOne = 0;
                _lastFrameHighWaterBytes = 0;
                _oomCount = 0;

                int slotCount = ArenaBufferCount * resolvedSlabCount;
                _slabCursorBytes = new int[slotCount];
                _slabHighWaterBytes = new int[slotCount];
                _ownerHashes = new int[OwnerTelemetryCapacity];
                _ownerFrameBytes = new int[OwnerTelemetryCapacity];
                _ownerLastFrameBytes = new int[OwnerTelemetryCapacity];
                _ownerHighWaterBytes = new int[OwnerTelemetryCapacity];
#if UNITY_EDITOR
                _editorAllocationCounts = new int[slotCount];
                _editorAllocationBytes = new int[slotCount];
                _editorLastAllocationEndBytes = new int[slotCount];
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                _arenaSafetyHandles = new AtomicSafetyHandle[ArenaBufferCount];
                _arenaSafetyHandleCreated = new bool[ArenaBufferCount];
#endif

                _basePtr = (byte*)H8Memory.AllocateRaw(
                    _capacityBytes,
                    CacheLineAlignment,
                    SystemID.H8Memory,
                    Allocator.Persistent,
                    clearMemory: true,
                    H8AllocationFlags.Raw);
                if (_basePtr == null)
                {
                    _capacityBytes = 0;
                    _arenaCapacityBytes = 0;
                    _slabCapacityBytes = 0;
                    _slabCount = 0;
                    ResetScalarState();
                    ClearManagedState();
                    return;
                }

                _sentinelId = NativeMemorySentinel.RegisterPointer(
                    _basePtr,
                    _capacityBytes,
                    nameof(HectonArenaAllocator),
                    nameof(_basePtr),
                    NativeAllocationLifetime.TransientArena);

                MemoryBudgetTracker.Register(BudgetOwner, _capacityBytes, _capacityBytes);
                RecreateSafetyHandle(0);
                RecreateSafetyHandle(1);
            }
            finally
            {
                Volatile.Write(ref _initializing, 0);
            }
        }

        public static NativeArenaArray<T> AllocateNativeArenaArray<T>(int count, bool clearMemory = true) where T : unmanaged
        {
            if (!TryAllocateNativeArenaArray(count, clearMemory, DefaultOwnerHash, out NativeArenaArray<T> array))
                return default;

            return array;
        }

        public static NativeArenaArray<T> AllocateNativeArenaArray<T>(int count, NativeArrayOptions options) where T : unmanaged
        {
            if (!TryAllocateNativeArenaArray(count, options, DefaultOwnerHash, out NativeArenaArray<T> array))
                return default;

            return array;
        }

        public static bool TryAllocateNativeArenaArray<T>(int count, bool clearMemory, out NativeArenaArray<T> array) where T : unmanaged
        {
            return TryAllocateNativeArenaArray(count, clearMemory, DefaultOwnerHash, out array);
        }

        public static bool TryAllocateNativeArenaArray<T>(int count, NativeArrayOptions options, out NativeArenaArray<T> array) where T : unmanaged
        {
            return TryAllocateNativeArenaArray(count, options, DefaultOwnerHash, out array);
        }

        public static bool TryAllocateNativeArenaArray<T>(int count, NativeArrayOptions options, uint ownerHash, out NativeArenaArray<T> array) where T : unmanaged
        {
            return TryAllocateNativeArenaArray(count, ShouldClear(options), ownerHash, out array);
        }

        public static bool TryAllocateNativeArenaArray<T>(int count, bool clearMemory, uint ownerHash, out NativeArenaArray<T> array) where T : unmanaged
        {
            array = default;
            if (!TryAllocateBlock<T>(count, ownerHash, out ArenaAllocation allocation))
                return false;

            if (clearMemory)
                UnsafeUtility.MemClear(allocation.Ptr, allocation.ByteCount);

            array = NativeArenaArray<T>.Create(
                allocation.Ptr,
                count,
                allocation.ByteCount,
                allocation.ArenaIndex,
                allocation.SlabIndex,
                allocation.FrameSequence);
            return true;
        }

        /// <summary>
        /// Acquires a cache-line-aligned transient slice from the current write arena.
        /// </summary>
        public static bool TryAcquireSlice<T>(int count, uint ownerHash, out NativeArenaSlice<T> slice) where T : unmanaged
        {
            slice = default;
            if (!TryAllocateBlock<T>(count, ownerHash, out ArenaAllocation allocation))
                return false;

            slice = new NativeArenaSlice<T>(
                allocation.Ptr,
                count,
                UnsafeUtility.SizeOf<T>(),
                allocation.ByteCount,
                allocation.FrameSequence);
            return true;
        }

        public static bool TryAllocateSpan<T>(int count, bool clearMemory, out Span<T> span) where T : unmanaged
        {
            span = default;
            if (!TryAllocateBlock<T>(count, DefaultOwnerHash, out ArenaAllocation allocation))
                return false;

            if (clearMemory)
                UnsafeUtility.MemClear(allocation.Ptr, allocation.ByteCount);

            span = new Span<T>(allocation.Ptr, count);
            return true;
        }

        public static bool TryAllocateCharSpan(int charCount, out Span<char> span)
        {
            return TryAllocateSpan(charCount, true, out span);
        }

        internal static bool TryAllocateBytes(int byteCount, int alignment, out byte* ptr)
        {
            return TryAllocateBytes(byteCount, alignment, DefaultOwnerHash, out ptr);
        }

        internal static bool TryAllocateBytes(int byteCount, int alignment, uint ownerHash, out byte* ptr)
        {
            ptr = null;
            if (!TryAllocateBytesInternal(byteCount, alignment, ownerHash, out ArenaAllocation allocation))
                return false;

            ptr = (byte*)allocation.Ptr;
            return true;
        }

        /// <summary>
        /// Reads the byte total allocated by an owner during the most recently swapped frame.
        /// </summary>
        public static bool TryGetOwnerLastFrameBytes(uint ownerHash, out int bytes)
        {
            bytes = 0;
            if (!TryFindOwnerTelemetrySlot(ownerHash, out int index) || _ownerLastFrameBytes == null)
                return false;

            bytes = Volatile.Read(ref _ownerLastFrameBytes[index]);
            return true;
        }

        /// <summary>
        /// Reads the maximum per-frame byte total observed for an owner since arena initialization.
        /// </summary>
        public static bool TryGetOwnerHighWaterBytes(uint ownerHash, out int bytes)
        {
            bytes = 0;
            if (!TryFindOwnerTelemetrySlot(ownerHash, out int index) || _ownerHighWaterBytes == null)
                return false;

            bytes = Volatile.Read(ref _ownerHighWaterBytes[index]);
            return true;
        }

        /// <summary>
        /// Completes the dispatcher-owned frame boundary: publish high-water, swap read/write arenas, then reset the new write arena.
        /// </summary>
        public static void EndFrameSwap()
        {
            if (_basePtr == null)
                return;

            using (_resetProfilerMarker.Auto())
            {
                int currentWrite = Volatile.Read(ref _writeArenaIndex);
                int nextWrite = 1 - currentWrite;
                Volatile.Write(ref _lastFrameHighWaterBytes, SumArenaHighWater(currentWrite));
                SwapOwnerFrameTelemetry();
                Volatile.Write(ref _readArenaIndex, currentWrite);
                ResetArenaSlots(nextWrite);
                RecreateSafetyHandle(nextWrite);
                Volatile.Write(ref _writeArenaIndex, nextWrite);
                Interlocked.Increment(ref _frameSequence);
            }
        }

        public static void Reset()
        {
            EndFrameSwap();
        }

        public static void Shutdown()
        {
            if (_basePtr == null)
            {
                ReleaseSafetyHandles();
                ResetScalarState();
                ClearManagedState();
                return;
            }

            ReleaseSafetyHandles();
            if (_sentinelId != 0)
            {
                NativeMemorySentinel.Unregister(_sentinelId);
                _sentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            H8Memory.FreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);
            _basePtr = null;
            _capacityBytes = 0;
            _arenaCapacityBytes = 0;
            _slabCapacityBytes = 0;
            _slabCount = 0;
            _writeArenaIndex = 0;
            _readArenaIndex = 1;
            ResetScalarState();
            ClearManagedState();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignOffset16(int offset)
        {
            return (offset + 15) & ~15;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldClear(NativeArrayOptions options)
        {
            return options == NativeArrayOptions.ClearMemory;
        }

        internal static bool TryAllocateBlock<T>(int count, uint ownerHash, out ArenaAllocation allocation) where T : unmanaged
        {
            allocation = default;
            if (count <= 0)
                return false;

            long totalBytes = (long)UnsafeUtility.SizeOf<T>() * count;
            if (totalBytes <= 0L || totalBytes > int.MaxValue)
                return false;

            return TryAllocateBytesInternal((int)totalBytes, UnsafeUtility.AlignOf<T>(), ownerHash, out allocation);
        }

        internal static bool TryAllocateBlock(int count, uint ownerHash, out ArenaAllocation allocation)
        {
            return TryAllocateBlock<byte>(count, ownerHash, out allocation);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal static AtomicSafetyHandle GetSafetyHandle(int arenaIndex)
        {
            return _arenaSafetyHandles != null && (uint)arenaIndex < (uint)_arenaSafetyHandles.Length
                ? _arenaSafetyHandles[arenaIndex]
                : default;
        }
#endif

        private static bool TryAllocateBytesInternal(int byteCount, int alignment, uint ownerHash, out ArenaAllocation allocation)
        {
            allocation = default;
            if (byteCount <= 0)
                return false;

            Initialize();
            if (_basePtr == null || _arenaCapacityBytes <= 0 || _slabCapacityBytes <= 0 || _slabCursorBytes == null)
                return false;

            int safeAlignment = NormalizeAlignment(alignment);
            if (safeAlignment <= 0)
                return false;

            int arenaIndex = Volatile.Read(ref _writeArenaIndex);
            int preferredSlabIndex = ResolveThreadSlabIndex();
            if (TryAllocateFromSlab(arenaIndex, preferredSlabIndex, byteCount, safeAlignment, ownerHash, out allocation))
                return true;

            int slabCount = Volatile.Read(ref _slabCount);
            for (int i = 1; i < slabCount; i++)
            {
                int fallbackSlabIndex = preferredSlabIndex + i;
                if (fallbackSlabIndex >= slabCount)
                    fallbackSlabIndex -= slabCount;

                if (TryAllocateFromSlab(arenaIndex, fallbackSlabIndex, byteCount, safeAlignment, ownerHash, out allocation))
                    return true;
            }

            PublishArenaOom(byteCount, ownerHash);
            return false;
        }

        private static bool TryAllocateFromSlab(
            int arenaIndex,
            int slabIndex,
            int byteCount,
            int safeAlignment,
            uint ownerHash,
            out ArenaAllocation allocation)
        {
            allocation = default;
            int slabCount = Volatile.Read(ref _slabCount);
            if ((uint)slabIndex >= (uint)slabCount)
                return false;

            int slot = ResolveSlotIndex(arenaIndex, slabIndex);
            int slabBaseOffset = slabIndex * _slabCapacityBytes;

            while (true)
            {
                int observedCursor = Volatile.Read(ref _slabCursorBytes[slot]);
                int alignedOffset = AlignOffset(observedCursor, safeAlignment);
                long nextCursorLong = (long)alignedOffset + byteCount;

                if (alignedOffset < 0 || nextCursorLong < alignedOffset || nextCursorLong > Volatile.Read(ref _slabCapacityBytes))
                    return false;

                int nextCursor = (int)nextCursorLong;
                if (Interlocked.CompareExchange(ref _slabCursorBytes[slot], nextCursor, observedCursor) != observedCursor)
                    continue;

#if UNITY_EDITOR
                RecordEditorAllocation(slot, alignedOffset, nextCursor, byteCount);
#endif
                UpdateHighWater(slot, nextCursor);
                UpdateOwnerHighWater(ownerHash, byteCount);
                allocation = new ArenaAllocation(
                    GetArenaBasePtr(arenaIndex) + slabBaseOffset + alignedOffset,
                    byteCount,
                    arenaIndex,
                    slabIndex,
                    Volatile.Read(ref _frameSequence));
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignOffset(int offset, int alignment)
        {
            int aligned16 = AlignOffset16(offset);
            if (alignment <= MinimumAllocationAlignment)
                return aligned16;

            return (aligned16 + (alignment - 1)) & ~(alignment - 1);
        }

        private static int NormalizeAlignment(int alignment)
        {
            int requested = alignment <= MinimumAllocationAlignment ? MinimumAllocationAlignment : alignment;
            if (requested < CacheLineAlignment)
                requested = CacheLineAlignment;

            if (requested > MaxArenaAlignment)
                return 0;

            int safeAlignment = 1;
            while (safeAlignment < requested)
                safeAlignment <<= 1;

            return safeAlignment;
        }

        private static int ResolveSlabCount()
        {
            int processorCount = SystemInfo.processorCount;
            if (processorCount <= 0)
                processorCount = 1;

            return Math.Min(MaxSlabCount, Math.Max(1, processorCount));
        }

        private static int ResolveAlignedCapacity(int requestedCapacity, int slabCount, out int arenaCapacity, out int slabCapacity)
        {
            int safeRequested = requestedCapacity < CacheLineAlignment * ArenaBufferCount * slabCount
                ? DefaultArenaBytes
                : requestedCapacity;
            int half = safeRequested / ArenaBufferCount;
            int rawSlab = half / slabCount;
            slabCapacity = rawSlab & ~(CacheLineAlignment - 1);
            if (slabCapacity < CacheLineAlignment)
                slabCapacity = CacheLineAlignment;

            arenaCapacity = slabCapacity * slabCount;
            return arenaCapacity * ArenaBufferCount;
        }

        private static int ResolveThreadSlabIndex()
        {
            int slabCount = Volatile.Read(ref _slabCount);
            if (slabCount <= 1)
                return 0;

            int assigned = _threadSlabIndexPlusOne;
            if (assigned > 0 && assigned <= slabCount)
                return assigned - 1;

            int next = Interlocked.Increment(ref _nextThreadSlab) - 1;
            int slabIndex = next % slabCount;
            if (slabIndex < 0)
                slabIndex = 0;

            _threadSlabIndexPlusOne = slabIndex + 1;
            return slabIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveSlotIndex(int arenaIndex, int slabIndex)
        {
            return (arenaIndex * Volatile.Read(ref _slabCount)) + slabIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte* GetArenaBasePtr(int arenaIndex)
        {
            if (_basePtr == null)
                return null;

            return arenaIndex == 0 ? _basePtr : _basePtr + Volatile.Read(ref _arenaCapacityBytes);
        }

        private static int SumArenaUsage(int arenaIndex)
        {
            int slabCount = Volatile.Read(ref _slabCount);
            int[] cursors = _slabCursorBytes;
            if (cursors == null || slabCount <= 0)
                return 0;

            int start = arenaIndex * slabCount;
            int total = 0;
            for (int i = 0; i < slabCount; i++)
                total += Volatile.Read(ref cursors[start + i]);

            return total;
        }

        private static int SumArenaHighWater(int arenaIndex)
        {
            int slabCount = Volatile.Read(ref _slabCount);
            int[] highWater = _slabHighWaterBytes;
            if (highWater == null || slabCount <= 0)
                return 0;

            int start = arenaIndex * slabCount;
            int total = 0;
            for (int i = 0; i < slabCount; i++)
                total += Volatile.Read(ref highWater[start + i]);

            return total;
        }

        private static void ResetArenaSlots(int arenaIndex)
        {
            int slabCount = Volatile.Read(ref _slabCount);
            if (_slabCursorBytes == null || _slabHighWaterBytes == null || slabCount <= 0)
                return;

            int start = arenaIndex * slabCount;
            for (int i = 0; i < slabCount; i++)
            {
                int slot = start + i;
                Volatile.Write(ref _slabCursorBytes[slot], 0);
                Volatile.Write(ref _slabHighWaterBytes[slot], 0);
#if UNITY_EDITOR
                if (_editorAllocationCounts != null)
                {
                    _editorAllocationCounts[slot] = 0;
                    _editorAllocationBytes[slot] = 0;
                    _editorLastAllocationEndBytes[slot] = 0;
                }
#endif
            }
        }

        private static void UpdateHighWater(int slot, int usedBytes)
        {
            while (true)
            {
                int observed = Volatile.Read(ref _slabHighWaterBytes[slot]);
                if (usedBytes <= observed)
                    return;

                if (Interlocked.CompareExchange(ref _slabHighWaterBytes[slot], usedBytes, observed) == observed)
                    return;
            }
        }

        private static void UpdateOwnerHighWater(uint ownerHash, int usedBytes)
        {
            if (ownerHash == 0u || _ownerHashes == null || _ownerFrameBytes == null || _ownerHighWaterBytes == null)
                return;

            int ownerKey = unchecked((int)ownerHash);
            for (int attempt = 0; attempt < OwnerTelemetryCapacity; attempt++)
            {
                int emptyIndex = -1;
                for (int i = 0; i < OwnerTelemetryCapacity; i++)
                {
                    int observedHash = Volatile.Read(ref _ownerHashes[i]);
                    if (observedHash == ownerKey)
                    {
                        AddOwnerFrameBytes(i, usedBytes);
                        return;
                    }

                    if (observedHash == 0 && emptyIndex < 0)
                        emptyIndex = i;
                }

                if (emptyIndex < 0)
                    return;

                int previousHash = Interlocked.CompareExchange(ref _ownerHashes[emptyIndex], ownerKey, 0);
                if (previousHash == 0 || previousHash == ownerKey)
                {
                    AddOwnerFrameBytes(emptyIndex, usedBytes);
                    return;
                }
            }
        }

        private static void ResetScalarState()
        {
            _frameSequence = 0;
            _lastFrameHighWaterBytes = 0;
            _oomCount = 0;
            _nextThreadSlab = 0;
            _threadSlabIndexPlusOne = 0;
            Volatile.Write(ref _initializing, 0);
        }

        private static void AddOwnerFrameBytes(int index, int byteCount)
        {
            int frameBytes = Interlocked.Add(ref _ownerFrameBytes[index], byteCount);
            UpdateOwnerHighWaterSlot(index, frameBytes);
        }

        private static void UpdateOwnerHighWaterSlot(int index, int usedBytes)
        {
            while (true)
            {
                int observed = Volatile.Read(ref _ownerHighWaterBytes[index]);
                if (usedBytes <= observed)
                    return;

                if (Interlocked.CompareExchange(ref _ownerHighWaterBytes[index], usedBytes, observed) == observed)
                    return;
            }
        }

        private static bool TryFindOwnerTelemetrySlot(uint ownerHash, out int index)
        {
            index = -1;
            if (ownerHash == 0u || _ownerHashes == null)
                return false;

            int ownerKey = unchecked((int)ownerHash);
            for (int i = 0; i < OwnerTelemetryCapacity; i++)
            {
                if (Volatile.Read(ref _ownerHashes[i]) != ownerKey)
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        private static void SwapOwnerFrameTelemetry()
        {
            if (_ownerHashes == null || _ownerFrameBytes == null || _ownerLastFrameBytes == null)
                return;

            for (int i = 0; i < OwnerTelemetryCapacity; i++)
            {
                if (Volatile.Read(ref _ownerHashes[i]) == 0)
                    continue;

                int frameBytes = Volatile.Read(ref _ownerFrameBytes[i]);
                Volatile.Write(ref _ownerLastFrameBytes[i], frameBytes);
                Volatile.Write(ref _ownerFrameBytes[i], 0);
            }
        }

        private static void PublishArenaOom(int byteCount, uint ownerHash)
        {
            Interlocked.Increment(ref _oomCount);
            GlobalTelemetryBus.PublishPerformanceWarning(
                ArenaOomHash,
                ownerHash != 0u ? ownerHash : ArenaContextHash,
                byteCount);
        }

#if UNITY_EDITOR
        private static void RecordEditorAllocation(int slot, int startBytes, int endBytes, int byteCount)
        {
            if (_editorAllocationCounts == null)
                return;

            int previousEnd = Volatile.Read(ref _editorLastAllocationEndBytes[slot]);
            Debug.Assert(startBytes >= previousEnd, "[HectonArenaAllocator] overlapping allocation window detected.");
            Volatile.Write(ref _editorLastAllocationEndBytes[slot], endBytes);
            Interlocked.Increment(ref _editorAllocationCounts[slot]);
            Interlocked.Add(ref _editorAllocationBytes[slot], byteCount);
        }
#endif

        private static void RecreateSafetyHandle(int arenaIndex)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_arenaSafetyHandles == null || (uint)arenaIndex >= (uint)_arenaSafetyHandles.Length)
                return;

            if (_arenaSafetyHandleCreated[arenaIndex])
                AtomicSafetyHandle.Release(_arenaSafetyHandles[arenaIndex]);

            _arenaSafetyHandles[arenaIndex] = AtomicSafetyHandle.Create();
            _arenaSafetyHandleCreated[arenaIndex] = true;
#endif
        }

        private static void ReleaseSafetyHandles()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_arenaSafetyHandles == null || _arenaSafetyHandleCreated == null)
                return;

            for (int i = 0; i < _arenaSafetyHandles.Length; i++)
            {
                if (!_arenaSafetyHandleCreated[i])
                    continue;

                AtomicSafetyHandle.Release(_arenaSafetyHandles[i]);
                _arenaSafetyHandleCreated[i] = false;
            }
#endif
        }

        private static void ClearManagedState()
        {
            _slabCursorBytes = null;
            _slabHighWaterBytes = null;
            _ownerHashes = null;
            _ownerFrameBytes = null;
            _ownerLastFrameBytes = null;
            _ownerHighWaterBytes = null;
#if UNITY_EDITOR
            _editorAllocationCounts = null;
            _editorAllocationBytes = null;
            _editorLastAllocationEndBytes = null;
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _arenaSafetyHandles = null;
            _arenaSafetyHandleCreated = null;
#endif
        }

        /// <summary>
        /// Raw cache-line-aligned arena slice. Frame lifetime only.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public readonly struct NativeArenaSlice<T> where T : unmanaged
        {
            [FieldOffset(0)]
            internal readonly void* Ptr;

            [FieldOffset(8)]
            public readonly int Length;

            [FieldOffset(12)]
            public readonly int Stride;

            [FieldOffset(16)]
            public readonly int ByteCount;

            [FieldOffset(20)]
            public readonly int FrameSequence;

            [FieldOffset(24)]
            private readonly long _pad0;

            internal NativeArenaSlice(void* ptr, int length, int stride, int byteCount, int frameSequence)
            {
                Ptr = ptr;
                Length = length;
                Stride = stride;
                ByteCount = byteCount;
                FrameSequence = frameSequence;
                _pad0 = 0L;
            }

            public bool IsCreated()
            {
                return Ptr != null && Length > 0;
            }

            public ref T GetElementAsRef(int index)
            {
                if (Ptr == null || (uint)index >= (uint)Length)
                    FatalMemoryException.ThrowStaleVaultHandle();

                Hint.Assume(Ptr != null);
                Hint.Assume(index >= 0);
                Hint.Assume(index < Length);
                return ref UnsafeUtility.AsRef<T>((byte*)Ptr + (index * Stride));
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        internal readonly struct ArenaAllocation
        {
            [FieldOffset(0)]
            internal readonly void* Ptr;

            [FieldOffset(8)]
            public readonly int ByteCount;

            [FieldOffset(12)]
            public readonly int ArenaIndex;

            [FieldOffset(16)]
            public readonly int SlabIndex;

            [FieldOffset(20)]
            public readonly int FrameSequence;

            [FieldOffset(24)]
            private readonly long _pad0;

            internal ArenaAllocation(void* ptr, int byteCount, int arenaIndex, int slabIndex, int frameSequence)
            {
                Ptr = ptr;
                ByteCount = byteCount;
                ArenaIndex = arenaIndex;
                SlabIndex = slabIndex;
                FrameSequence = frameSequence;
                _pad0 = 0L;
            }
        }
    }
}
