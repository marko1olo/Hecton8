using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Registry-facing data-vault contract. Systems request buffers here instead of owning persistent arrays.
    /// </summary>
    public interface IDataVault : IDisposable, IMacroDatabaseNativeCacheOwner
    {
        /// <summary>Total allocated vault bytes.</summary>
        long AllocatedBytes { get; }

        /// <summary>True while allocations are blocked for an AUP shift.</summary>
        bool IsAllocationLocked { get; }

        /// <summary>True while vault aliases are fenced by a critical maintenance pass.</summary>
        bool IsCompactionFenceActive { get; }

        /// <summary>True when the most recent gap analysis crossed the fragmentation threshold.</summary>
        bool IsFragmented { get; }

        /// <summary>Fragmentation ratio from the most recent gap analysis.</summary>
        float HeapFragmentationRatio { get; }

        /// <summary>Total free arena space from the most recent gap analysis.</summary>
        long TotalFreeSpaceBytes { get; }

        /// <summary>Largest contiguous free block from the most recent gap analysis.</summary>
        long LargestContiguousBlockBytes { get; }

        /// <summary>Bytes moved by the most recent relocation pass; telemetry-only defrag keeps this at zero.</summary>
        long LastDefragMovedBytes { get; }

        /// <summary>Largest occupied block that would require a pause/loading mask before any future relocation pass.</summary>
        long PendingMassiveMoveBytes { get; }

        /// <summary>True when a future relocation pass exceeds its watchdog threshold.</summary>
        bool LastDefragWatchdogExceeded { get; }

        /// <summary>Bitfield describing the most recent defrag telemetry pass.</summary>
        byte LastDefragFlags { get; }

        /// <summary>Occupied buffers that failed the 64-byte alignment audit.</summary>
        int UnalignedBufferCount { get; }

        /// <summary>Total bytes moved by future vault relocation since initialization.</summary>
        long TotalDefragMovedBytes { get; }

        /// <summary>Total number of future relocation passes that breached the watchdog.</summary>
        int CompactionWatchdogBreachCount { get; }

        /// <summary>Global vault generation for black-box telemetry and stale-handle audits.</summary>
        uint VaultGenerationID { get; }

        /// <summary>Relocation records emitted by future offline relocation; telemetry-only defrag emits none.</summary>
        int LastRelocationRecordCount { get; }

        /// <summary>Returns a persistent buffer view, growing the vault buffer when required.</summary>
        NativeArray<T> GetBuffer<T>(BufferID bufferId, int requiredLength, SystemID requester, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct;

        /// <summary>Returns a relocatable handle for a persistent buffer, growing the vault buffer when required.</summary>
        VaultBufferHandle<T> GetBufferHandle<T>(BufferID bufferId, int requiredLength, SystemID requester, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct;

        /// <summary>Attempts to read an existing buffer without creating or growing it.</summary>
        bool TryGetBuffer<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct;

        /// <summary>Attempts to read an existing relocatable handle without creating or growing it.</summary>
        bool TryGetBufferHandle<T>(BufferID bufferId, out VaultBufferHandle<T> handle) where T : struct;

        /// <summary>Validates a relocatable handle; stale cached metadata fails fast.</summary>
        bool ResolveBuffer<T>(ref VaultBufferHandle<T> handle) where T : struct;

        /// <summary>Attempts to read the current generation for a buffer.</summary>
        bool TryGetBufferGeneration(BufferID bufferId, out uint generation);

        /// <summary>Returns a read-only alias over an existing buffer.</summary>
        NativeArray<T>.ReadOnly CreateAlias<T>(BufferID bufferId, SystemID requester) where T : struct;

        /// <summary>Locks a buffer against relocation while an external job owns its pointer.</summary>
        bool TryLockBuffer(BufferID bufferId);

        /// <summary>Unlocks a previously locked buffer.</summary>
        bool TryUnlockBuffer(BufferID bufferId);

        /// <summary>Attempts to read one relocation record from future offline relocation.</summary>
        bool TryGetLastRelocationRecord(int index, out VaultRelocationRecord record);

        /// <summary>Locks vault allocation while AUP positions are being rebased.</summary>
        void LockAllocationsForAupShift(uint shiftFrameId);

        /// <summary>Unlocks vault allocation after an AUP shift barrier resolves.</summary>
        void UnlockAllocationsAfterAupShift(uint shiftFrameId);

        /// <summary>Runs cold fragmentation maintenance.</summary>
        void FrostTickDefrag(float elapsedSeconds);

        /// <summary>Runs cold fragmentation maintenance with a caller-provided stress gate.</summary>
        void FrostTickDefrag(float elapsedSeconds, float systemStress01);
    }

    /// <summary>
    /// Relocatable vault buffer handle. Resolve before dereferencing across frames.
    /// </summary>
    /// <typeparam name="T">Blittable element type.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VaultBufferHandle<T> where T : struct
    {
        /// <summary>Cached raw pointer. Invalid after a generation mismatch; resolver fails fast.</summary>
        public void* ptr;

        /// <summary>Cached buffer generation.</summary>
        public uint generation;

        /// <summary>Cached buffer generation exposed under the batch contract name.</summary>
        public uint GenerationID => generation;

        /// <summary>Vault buffer identifier.</summary>
        public BufferID BufferId;

        /// <summary>Current element count.</summary>
        public int Length;

        /// <summary>Element stride captured at handle creation.</summary>
        public int Stride;

        /// <summary>True when the handle currently points at a vault buffer.</summary>
        public bool IsCreated => ptr != null && BufferId != BufferID.Unknown && Length > 0;

        /// <summary>
        /// Resolves the handle and returns a NativeArray view over the current pointer.
        /// </summary>
        /// <param name="vault">Owning vault.</param>
        /// <returns>Current buffer view, or default when unavailable.</returns>
        public NativeArray<T> Resolve(IDataVault vault)
        {
            if (vault == null || !vault.ResolveBuffer(ref this))
                return default;

            return H8Memory.CreateNativeArrayView<T>(ptr, Length);
        }

        /// <summary>
        /// Resolves the handle and returns the current raw pointer.
        /// </summary>
        /// <param name="vault">Owning vault.</param>
        /// <returns>Current pointer, or null when unavailable.</returns>
        public void* ResolvePointer(IDataVault vault)
        {
            return vault != null && vault.ResolveBuffer(ref this) ? ptr : null;
        }
    }

    /// <summary>
    /// Fixed-size relocation record copied from the memory assembly to the Core signal bridge.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VaultRelocationRecord
    {
        public const byte FlagAddressChanged = 1 << 0;
        public const byte FlagFenceProtected = 1 << 1;
        public const byte FlagWatchdogBreached = 1 << 2;

        public long OldPointer;
        public long NewPointer;
        public int BufferId;
        public int ByteLength;
        public uint Generation;
        public byte Flags;
        public byte SystemId;
        public ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VaultBufferMeta
    {
        public int Length;
        public int Stride;
        public int Alignment;
        public int BlockIndex;
        public long OffsetBytes;
        public long Bytes;
        public SystemID Owner;
        public Allocator Allocator;
        public uint Version;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VaultArenaBlock
    {
        public long OffsetBytes;
        public long Bytes;
        public int BufferKey;
        public int H8BlockIndex;
        public uint Version;
        public byte State;
        public byte Reserved0;
        public ushort Reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryDefragTelemetryEntry
    {
        public uint Sequence;
        public uint VaultGenerationID;
        public int BlockCount;
        public long TotalFreeSpaceBytes;
        public long LargestContiguousBlockBytes;
        public long LastMovedBytes;
        public long TotalMovedBytes;
        public long PendingMassiveMoveBytes;
        public float HeapFragmentationRatio;
        public int WatchdogBreaches;
        public byte Flags;
        public byte IsFragmented;
        public byte WatchdogExceeded;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VaultGapAuditResult
    {
        public long TotalFreeBytes;
        public long LargestFreeBytes;
        public float FragmentationRatio;
        public int FreeBlockCount;
        public int OccupiedBlockCount;
        public int UnalignedOccupiedCount;
    }

    internal struct VaultGapAuditJob : IJob
    {
        [ReadOnly] public NativeArray<VaultArenaBlock> Blocks;
        public NativeArray<VaultGapAuditResult> Result;

        public void Execute()
        {
            VaultGapAuditResult result = default;
            for (int i = 0; i < Blocks.Length; i++)
            {
                VaultArenaBlock block = Blocks[i];
                if (block.State == GlobalDataVault.BlockStateFree)
                {
                    result.TotalFreeBytes += block.Bytes;
                    result.FreeBlockCount++;
                    if (block.Bytes > result.LargestFreeBytes)
                        result.LargestFreeBytes = block.Bytes;
                    continue;
                }

                if (block.State != GlobalDataVault.BlockStateOccupied)
                    continue;

                result.OccupiedBlockCount++;
                if (((ulong)block.OffsetBytes & (ulong)(GlobalDataVault.VaultBlockAlignment - 1)) != 0UL)
                    result.UnalignedOccupiedCount++;
            }

            if (result.TotalFreeBytes > 0L)
            {
                float fragmentedBytes = (float)(result.TotalFreeBytes - result.LargestFreeBytes);
                result.FragmentationRatio = fragmentedBytes / (float)result.TotalFreeBytes;
            }

            Result[0] = result;
        }
    }

    /// <summary>
    /// Persistent raw-memory authority for cross-system buffers.
    /// </summary>
    public sealed unsafe class GlobalDataVault : IDataVault
    {
        private const int DefaultBufferCapacity = 128;
        internal const int VaultBlockAlignment = 64;
        private const long DefaultArenaBytes = 128L * 1024L * 1024L;
        private const float FragmentationRatioThreshold = 0.15f;
        private const long MassiveMoveThresholdBytes = 50L * 1024L * 1024L;
        private const int RelocationRecordCapacity = 64;
        internal const byte BlockStateFree = 0;
        internal const byte BlockStateOccupied = 1;
        private const byte BlockFlagExternalView = 1 << 0;
        private const byte BlockFlagLocked = 1 << 1;
        private const byte DefragFlagFragmented = 1 << 0;
        private const byte DefragFlagMassiveMovePending = 1 << 3;
        private const byte DefragFlagFault = 1 << 4;
        private const byte DefragFlagUnaligned = 1 << 6;
        private const int DefragBlackBoxFrameCount = 300;
        private const string DefragDumpPath = "Docs/AgentLogs/Dump_VAULT_MEMORY_RELOCATOR.bin";
        private const string PhiVodDumpPath = "Docs/AgentLogs/Dump_PHI_VOD.bin";

        private UnsafeHashMap<int, IntPtr> _buffers;
        private UnsafeHashMap<int, VaultBufferMeta> _metadata;
        private NativeList<int> _keys;
        private NativeList<VaultArenaBlock> _blocks;
        private NativeArray<MemoryDefragTelemetryEntry> _defragBlackBox;
        private NativeArray<VaultGapAuditResult> _gapAuditResult;
        private NativeArray<VaultRelocationRecord> _lastRelocationRecords;
        private NativeParallelHashMap<ulong, MacroDatabasePayloadHandle> _macroDatabasePayloadCache;
        private NativeList<ulong> _macroDatabasePayloadKeys;
        private void* _arenaBase;
        private long _arenaBytes;
        private int _allocationLock;
        private int _compactionFence;
        private uint _lockedShiftFrameId;
        private long _allocatedBytes;
        private long _macroDatabasePayloadBytes;
        private int _macroDatabasePayloadEvictions;
        private int _defragBlackBoxCursor;
        private int _lastRelocationRecordCount;
        private int _compactionWatchdogBreachCount;
        private long _totalDefragMovedBytes;
        private uint _defragTickSequence;
        private uint _vaultGenerationId;
        private bool _defragDumpWritten;
        private bool _phiVodDumpWritten;
        private bool _initialized;

        /// <inheritdoc />
        public long AllocatedBytes => _allocatedBytes;

        /// <inheritdoc />
        public bool IsAllocationLocked => _allocationLock != 0;

        /// <inheritdoc />
        public bool IsCompactionFenceActive => _compactionFence != 0;

        /// <inheritdoc />
        public bool IsFragmented { get; private set; }

        /// <inheritdoc />
        public float HeapFragmentationRatio { get; private set; }

        /// <inheritdoc />
        public long TotalFreeSpaceBytes { get; private set; }

        /// <inheritdoc />
        public long LargestContiguousBlockBytes { get; private set; }

        /// <inheritdoc />
        public long LastDefragMovedBytes { get; private set; }

        /// <inheritdoc />
        public long PendingMassiveMoveBytes { get; private set; }

        /// <inheritdoc />
        public bool LastDefragWatchdogExceeded { get; private set; }

        /// <inheritdoc />
        public byte LastDefragFlags { get; private set; }

        /// <inheritdoc />
        public int UnalignedBufferCount { get; private set; }

        /// <inheritdoc />
        public long TotalDefragMovedBytes => _totalDefragMovedBytes;

        /// <inheritdoc />
        public int CompactionWatchdogBreachCount => _compactionWatchdogBreachCount;

        /// <inheritdoc />
        public uint VaultGenerationID => _vaultGenerationId;

        /// <inheritdoc />
        public int LastRelocationRecordCount => _lastRelocationRecordCount;

        /// <summary>
        /// Creates and initializes the vault for bootstrap registration.
        /// </summary>
        public static GlobalDataVault Create(int capacity = DefaultBufferCapacity)
        {
            GlobalDataVault vault = new GlobalDataVault();
            vault.Initialize(capacity);
            return vault;
        }

        /// <summary>
        /// Initializes raw vault maps.
        /// </summary>
        public void Initialize(int capacity = DefaultBufferCapacity)
        {
            if (_initialized)
                return;

            int safeCapacity = capacity > 0 ? capacity : DefaultBufferCapacity;
            int blockCapacity = safeCapacity << 1;
            if (blockCapacity < safeCapacity)
                blockCapacity = safeCapacity;

            H8Memory.Initialize();
            _buffers = new UnsafeHashMap<int, IntPtr>(safeCapacity, Allocator.Persistent);
            _metadata = new UnsafeHashMap<int, VaultBufferMeta>(safeCapacity, Allocator.Persistent);
            _keys = new NativeList<int>(safeCapacity, Allocator.Persistent);
            _blocks = new NativeList<VaultArenaBlock>(blockCapacity, Allocator.Persistent);
            _defragBlackBox = new NativeArray<MemoryDefragTelemetryEntry>(
                DefragBlackBoxFrameCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            _gapAuditResult = new NativeArray<VaultGapAuditResult>(
                1,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            _lastRelocationRecords = new NativeArray<VaultRelocationRecord>(
                RelocationRecordCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadHandle>(safeCapacity, Allocator.Persistent);
            _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);
            _arenaBytes = AlignUp(DefaultArenaBytes, VaultBlockAlignment);
            _arenaBase = H8Memory.AllocateRaw(
                _arenaBytes,
                VaultBlockAlignment,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                clearMemory: true,
                H8AllocationFlags.Vault | H8AllocationFlags.SubAllocatorRoot);
            if (_arenaBase == null)
            {
                _initialized = true;
                Dispose();
                return;
            }

            _allocationLock = 0;
            _compactionFence = 0;
            _lockedShiftFrameId = 0u;
            _allocatedBytes = 0L;
            _macroDatabasePayloadBytes = 0L;
            _macroDatabasePayloadEvictions = 0;
            _defragBlackBoxCursor = 0;
            _lastRelocationRecordCount = 0;
            _compactionWatchdogBreachCount = 0;
            _totalDefragMovedBytes = 0L;
            _defragTickSequence = 0u;
            _vaultGenerationId = 1u;
            _defragDumpWritten = false;
            _phiVodDumpWritten = false;
            ResetDefragTelemetry();
            if (_arenaBase != null && _blocks.Capacity > 0)
            {
                VaultArenaBlock freeBlock = new VaultArenaBlock
                {
                    OffsetBytes = 0L,
                    Bytes = _arenaBytes,
                    BufferKey = 0,
                    Version = 1u,
                    State = BlockStateFree
                };
                freeBlock.H8BlockIndex = H8Memory.RegisterBlockDescriptor(BuildDescriptor(in freeBlock));
                _blocks.AddNoResize(freeBlock);
            }

            _initialized = true;
        }

        /// <inheritdoc />
        public NativeArray<T> GetBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            SystemID requester,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            if (requiredLength <= 0)
                return default;

            EnsureInitialized();
            if (_compactionFence != 0)
                return default;

            if (_arenaBase == null)
            {
                DumpPhiVodBlackBox();
                return default;
            }

            int key = (int)bufferId;
            if (key == 0)
                return default;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            if (stride <= 0 || requiredLength > long.MaxValue / stride)
                return default;

            long requestedBytes = (long)requiredLength * stride;
            long requiredBytes = AlignUp(requestedBytes, VaultBlockAlignment);
            if (requiredBytes <= 0L || requiredBytes > _arenaBytes)
                return default;

            bool hasExistingPointer = _buffers.TryGetValue(key, out IntPtr existingPointer);
            bool hasExistingMeta = _metadata.TryGetValue(key, out VaultBufferMeta existingMeta);
            if (hasExistingPointer != hasExistingMeta)
            {
                DumpPhiVodBlackBox();
                return default;
            }

            if (hasExistingPointer)
            {
                if (existingPointer == IntPtr.Zero)
                {
                    DumpPhiVodBlackBox();
                    return default;
                }

                ValidateType<T>(bufferId, existingMeta, stride, alignment);
                if (existingMeta.Length >= requiredLength)
                {
                    MarkExternalView(key, existingMeta.OffsetBytes);
                    return H8Memory.CreateNativeArrayView<T>(existingPointer.ToPointer(), existingMeta.Length);
                }

                if (_allocationLock != 0)
                    return default;

                if (!TryReallocateBlock(key, existingMeta, requiredLength, requiredBytes, ShouldClear(options), out IntPtr resizedPointer, out VaultBufferMeta resizedMeta))
                    return default;

                _buffers[key] = resizedPointer;
                _metadata[key] = resizedMeta;
                BumpVaultGeneration();
                MarkExternalView(key, resizedMeta.OffsetBytes);
                return H8Memory.CreateNativeArrayView<T>(resizedPointer.ToPointer(), requiredLength);
            }

            if (_allocationLock != 0)
                return default;

            if (_keys.Length >= _keys.Capacity)
                return default;

            if (!TryAllocateBlock(key, requiredBytes, out int blockIndex, out IntPtr pointer))
                return default;

            if (ShouldClear(options))
                UnsafeUtility.MemClear(pointer.ToPointer(), requiredBytes);

            VaultBufferMeta meta = new VaultBufferMeta
            {
                Length = requiredLength,
                Stride = stride,
                Alignment = alignment,
                BlockIndex = blockIndex,
                OffsetBytes = _blocks[blockIndex].OffsetBytes,
                Bytes = requiredBytes,
                Owner = requester == SystemID.Unknown ? SystemID.CoreDataVault : requester,
                Allocator = Allocator.Persistent,
                Version = 1u
            };

            bool bufferAdded = _buffers.TryAdd(key, pointer);
            bool metadataAdded = bufferAdded && _metadata.TryAdd(key, meta);
            if (!bufferAdded || !metadataAdded)
            {
                if (bufferAdded)
                    _buffers.Remove(key);
                if (!metadataAdded)
                    _metadata.Remove(key);

                FreeBlock(blockIndex);
                DumpPhiVodBlackBox();
                return default;
            }

            _keys.AddNoResize(key);
            _allocatedBytes += requiredBytes;
            MarkExternalView(key, meta.OffsetBytes);
            return H8Memory.CreateNativeArrayView<T>(pointer.ToPointer(), requiredLength);
        }

        /// <inheritdoc />
        public VaultBufferHandle<T> GetBufferHandle<T>(
            BufferID bufferId,
            int requiredLength,
            SystemID requester,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            NativeArray<T> buffer = GetBuffer<T>(bufferId, requiredLength, requester, options);
            if (!buffer.IsCreated || !TryBuildHandle(bufferId, out VaultBufferHandle<T> handle))
                return default;

            return handle;
        }

        /// <inheritdoc />
        public bool TryGetBuffer<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!_initialized)
                return false;
            if (_compactionFence != 0)
                return false;
            if (_arenaBase == null)
            {
                DumpPhiVodBlackBox();
                return false;
            }

            int key = (int)bufferId;
            if (key == 0)
                return false;

            bool hasPointer = _buffers.TryGetValue(key, out IntPtr pointer);
            bool hasMeta = _metadata.TryGetValue(key, out VaultBufferMeta meta);
            if (!hasPointer && !hasMeta)
                return false;

            if (hasPointer != hasMeta)
            {
                DumpPhiVodBlackBox();
                return false;
            }

            if (pointer == IntPtr.Zero || meta.Length <= 0)
            {
                DumpPhiVodBlackBox();
                return false;
            }

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            ValidateType<T>(bufferId, meta, stride, alignment);
            buffer = H8Memory.CreateNativeArrayView<T>(pointer.ToPointer(), meta.Length);
            if (buffer.IsCreated)
            {
                MarkExternalView(key, meta.OffsetBytes);
                return true;
            }

            DumpPhiVodBlackBox();
            return false;
        }

        /// <inheritdoc />
        public bool TryGetBufferHandle<T>(BufferID bufferId, out VaultBufferHandle<T> handle) where T : struct
        {
            handle = default;
            if (!_initialized || _compactionFence != 0)
                return false;

            return TryBuildHandle(bufferId, out handle);
        }

        /// <inheritdoc />
        public bool ResolveBuffer<T>(ref VaultBufferHandle<T> handle) where T : struct
        {
            bool hasCachedIdentity =
                handle.ptr != null ||
                handle.generation != 0u ||
                handle.Length != 0 ||
                handle.Stride != 0;

            if (!_initialized || _compactionFence != 0 || _arenaBase == null)
            {
                if (hasCachedIdentity)
                {
                    DumpPhiVodBlackBox();
                    FatalMemoryException.ThrowStaleVaultHandle();
                }

                return false;
            }

            int key = (int)handle.BufferId;
            if (key == 0)
            {
                if (hasCachedIdentity)
                {
                    DumpPhiVodBlackBox();
                    FatalMemoryException.ThrowStaleVaultHandle();
                }

                return false;
            }

            bool hasPointer = _buffers.TryGetValue(key, out IntPtr pointer);
            bool hasMeta = _metadata.TryGetValue(key, out VaultBufferMeta meta);
            if (!hasPointer && !hasMeta)
            {
                if (hasCachedIdentity)
                {
                    DumpPhiVodBlackBox();
                    FatalMemoryException.ThrowStaleVaultHandle();
                }

                return false;
            }

            if (hasPointer != hasMeta || pointer == IntPtr.Zero || meta.Length <= 0)
            {
                DumpPhiVodBlackBox();
                if (hasCachedIdentity)
                    FatalMemoryException.ThrowStaleVaultHandle();

                return false;
            }

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            ValidateType<T>(handle.BufferId, meta, stride, alignment);
            bool matchesMetadata =
                handle.generation == meta.Version &&
                handle.ptr != null &&
                (IntPtr)handle.ptr == pointer &&
                handle.Length == meta.Length &&
                handle.Stride == meta.Stride;
            if (!matchesMetadata)
            {
                if (hasCachedIdentity)
                {
                    DumpPhiVodBlackBox();
                    FatalMemoryException.ThrowStaleVaultHandle();
                }

                handle.ptr = pointer.ToPointer();
                handle.generation = meta.Version;
                handle.BufferId = (BufferID)key;
                handle.Length = meta.Length;
                handle.Stride = meta.Stride;
            }

            return true;
        }

        /// <inheritdoc />
        public bool TryGetBufferGeneration(BufferID bufferId, out uint generation)
        {
            generation = 0u;
            if (!_initialized || bufferId == BufferID.Unknown)
                return false;

            if (!_metadata.TryGetValue((int)bufferId, out VaultBufferMeta meta))
                return false;

            generation = meta.Version;
            return true;
        }

        /// <inheritdoc />
        public NativeArray<T>.ReadOnly CreateAlias<T>(BufferID bufferId, SystemID requester) where T : struct
        {
            if (!TryGetBuffer<T>(bufferId, out NativeArray<T> buffer))
                return default;

            return H8Memory.CreateAlias(buffer, requester);
        }

        /// <inheritdoc />
        public bool TryLockBuffer(BufferID bufferId)
        {
            if (!_initialized || bufferId == BufferID.Unknown)
                return false;

            int key = (int)bufferId;
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                return false;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.Reserved1 == ushort.MaxValue)
                return false;

            block.Reserved1++;
            block.Reserved0 |= BlockFlagLocked;
            _blocks[blockIndex] = block;
            return true;
        }

        /// <inheritdoc />
        public bool TryUnlockBuffer(BufferID bufferId)
        {
            if (!_initialized || bufferId == BufferID.Unknown)
                return false;

            int key = (int)bufferId;
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                return false;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.Reserved1 == 0)
                return false;

            block.Reserved1--;
            if (block.Reserved1 == 0)
                block.Reserved0 &= unchecked((byte)~BlockFlagLocked);

            _blocks[blockIndex] = block;
            return true;
        }

        /// <inheritdoc />
        public bool TryGetLastRelocationRecord(int index, out VaultRelocationRecord record)
        {
            record = default;
            if (!_lastRelocationRecords.IsCreated ||
                index < 0 ||
                index >= _lastRelocationRecordCount ||
                index >= _lastRelocationRecords.Length)
            {
                return false;
            }

            record = _lastRelocationRecords[index];
            return true;
        }

        /// <inheritdoc />
        public void LockAllocationsForAupShift(uint shiftFrameId)
        {
            _allocationLock = 1;
            _lockedShiftFrameId = shiftFrameId;
        }

        /// <inheritdoc />
        public void UnlockAllocationsAfterAupShift(uint shiftFrameId)
        {
            if (_allocationLock == 0)
                return;

            if (_lockedShiftFrameId != 0u && shiftFrameId != 0u && _lockedShiftFrameId != shiftFrameId)
                return;

            _lockedShiftFrameId = 0u;
            _allocationLock = 0;
        }

        /// <inheritdoc />
        public void FrostTickDefrag(float elapsedSeconds)
        {
            FrostTickDefrag(elapsedSeconds, 1f);
        }

        /// <inheritdoc />
        public void FrostTickDefrag(float elapsedSeconds, float systemStress01)
        {
            if (!_initialized || _arenaBase == null || elapsedSeconds < 0f)
                return;

            _ = systemStress01;
            uint sequence = ++_defragTickSequence;
            ResetDefragTelemetry();
            AnalyzeGaps();
            if (!ValidateDefragTelemetry() || !ValidateBlockMap())
            {
                RecordDefragBlackBox(sequence);
                DumpDefragBlackBox();
                return;
            }

            if (IsFragmented)
            {
                PendingMassiveMoveBytes = EstimateLargestOccupiedMoveCandidate();
                if (PendingMassiveMoveBytes >= MassiveMoveThresholdBytes)
                    LastDefragFlags |= DefragFlagMassiveMovePending;
            }

            RecordDefragBlackBox(sequence);
        }

        /// <inheritdoc />
        public bool TryReserveMacroDatabaseCache(int capacity)
        {
            EnsureInitialized();
            int safeCapacity = capacity > 0 ? capacity : DefaultBufferCapacity;
            if (!_macroDatabasePayloadCache.IsCreated)
                _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadHandle>(safeCapacity, Allocator.Persistent);
            if (!_macroDatabasePayloadKeys.IsCreated)
                _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);

            if (_macroDatabasePayloadCache.Capacity < safeCapacity)
                _macroDatabasePayloadCache.Capacity = safeCapacity;
            if (_macroDatabasePayloadKeys.Capacity < safeCapacity)
                _macroDatabasePayloadKeys.Capacity = safeCapacity;

            return _macroDatabasePayloadCache.Capacity >= safeCapacity &&
                   _macroDatabasePayloadKeys.Capacity >= safeCapacity;
        }

        /// <inheritdoc />
        public bool TryStoreMacroDatabasePayload(
            ulong sectorHash,
            IntPtr source,
            int byteLength,
            long fileOffset,
            byte flags,
            out MacroDatabasePayloadHandle handle)
        {
            handle = default;
            if (sectorHash == 0UL || source == IntPtr.Zero || byteLength <= 0)
                return false;

            EnsureInitialized();
            if (!_macroDatabasePayloadCache.IsCreated && !TryReserveMacroDatabaseCache(DefaultBufferCapacity))
                return false;

            bool hasExisting = _macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadHandle existing);
            if (!hasExisting && _macroDatabasePayloadKeys.Length >= _macroDatabasePayloadKeys.Capacity)
                return false;

            void* payloadPointer = H8Memory.AllocateRaw(
                byteLength,
                16,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                clearMemory: false,
                H8AllocationFlags.Vault);
            if (payloadPointer == null)
                return false;

            UnsafeUtility.MemCpy(payloadPointer, source.ToPointer(), byteLength);
            handle = new MacroDatabasePayloadHandle
            {
                SectorHash = sectorHash,
                Pointer = (IntPtr)payloadPointer,
                FileOffset = fileOffset,
                ByteLength = byteLength,
                Version = hasExisting ? NextGeneration(existing.Version) : 1u,
                Flags = flags
            };

            if (hasExisting)
            {
                if (existing.Pointer != IntPtr.Zero)
                    H8Memory.FreeRaw(existing.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault);
                _macroDatabasePayloadBytes -= existing.ByteLength;
                _macroDatabasePayloadCache[sectorHash] = handle;
            }
            else
            {
                if (!_macroDatabasePayloadCache.TryAdd(sectorHash, handle))
                {
                    H8Memory.FreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault);
                    handle = default;
                    return false;
                }

                _macroDatabasePayloadKeys.AddNoResize(sectorHash);
            }

            _macroDatabasePayloadBytes += byteLength;
            BumpVaultGeneration();
            return true;
        }

        /// <inheritdoc />
        public bool TryGetMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle handle)
        {
            handle = default;
            return _macroDatabasePayloadCache.IsCreated &&
                   _macroDatabasePayloadCache.TryGetValue(sectorHash, out handle);
        }

        /// <inheritdoc />
        public bool TryRemoveMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle removed)
        {
            removed = default;
            if (!_macroDatabasePayloadCache.IsCreated ||
                !_macroDatabasePayloadCache.TryGetValue(sectorHash, out removed))
            {
                return false;
            }

            if (removed.Pointer != IntPtr.Zero)
                H8Memory.FreeRaw(removed.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault);

            _macroDatabasePayloadCache.Remove(sectorHash);
            _macroDatabasePayloadBytes -= removed.ByteLength;
            _macroDatabasePayloadEvictions++;
            RemoveMacroDatabaseKey(sectorHash);
            BumpVaultGeneration();
            return true;
        }

        /// <inheritdoc />
        public int CopyMacroDatabasePayloadKeys(NativeArray<ulong> destination)
        {
            if (!_macroDatabasePayloadKeys.IsCreated || !destination.IsCreated || destination.Length == 0)
                return 0;

            int count = _macroDatabasePayloadKeys.Length < destination.Length
                ? _macroDatabasePayloadKeys.Length
                : destination.Length;
            for (int i = 0; i < count; i++)
                destination[i] = _macroDatabasePayloadKeys[i];

            return count;
        }

        /// <inheritdoc />
        public int EvictMacroDatabasePayloads(NativeArray<ulong> sectorHashes, int count)
        {
            if (!sectorHashes.IsCreated || count <= 0)
                return 0;

            int limit = count < sectorHashes.Length ? count : sectorHashes.Length;
            int evicted = 0;
            for (int i = 0; i < limit; i++)
            {
                if (TryRemoveMacroDatabasePayload(sectorHashes[i], out _))
                    evicted++;
            }

            return evicted;
        }

        /// <inheritdoc />
        public MacroDatabaseNativeCacheStats GetMacroDatabaseCacheStats()
        {
            return new MacroDatabaseNativeCacheStats
            {
                Bytes = _macroDatabasePayloadBytes,
                Entries = _macroDatabasePayloadCache.IsCreated ? _macroDatabasePayloadCache.Count() : 0,
                Capacity = _macroDatabasePayloadCache.IsCreated ? _macroDatabasePayloadCache.Capacity : 0,
                Evictions = _macroDatabasePayloadEvictions
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_initialized)
                return;

            DisposeMacroDatabasePayloadCache();

            if (_blocks.IsCreated)
            {
                for (int i = 0; i < _blocks.Length; i++)
                {
                    VaultArenaBlock block = _blocks[i];
                    block.State = BlockStateFree;
                    block.Reserved0 = 0;
                    UpdateH8Descriptor(in block);
                }
            }

            if (_arenaBase != null)
            {
                H8Memory.FreeRaw(_arenaBase, Allocator.Persistent, SystemID.CoreDataVault);
                _arenaBase = null;
            }

            if (_keys.IsCreated)
                _keys.Dispose();
            if (_buffers.IsCreated)
                _buffers.Dispose();
            if (_metadata.IsCreated)
                _metadata.Dispose();
            if (_blocks.IsCreated)
                _blocks.Dispose();
            if (_defragBlackBox.IsCreated)
            {
                _defragBlackBox.Dispose();
            }
            if (_gapAuditResult.IsCreated)
            {
                _gapAuditResult.Dispose();
            }
            if (_lastRelocationRecords.IsCreated)
            {
                _lastRelocationRecords.Dispose();
            }
            _allocatedBytes = 0L;
            _arenaBytes = 0L;
            _allocationLock = 0;
            _compactionFence = 0;
            _defragBlackBoxCursor = 0;
            _lastRelocationRecordCount = 0;
            _compactionWatchdogBreachCount = 0;
            _totalDefragMovedBytes = 0L;
            _defragTickSequence = 0u;
            _vaultGenerationId = 0u;
            _defragDumpWritten = false;
            _phiVodDumpWritten = false;
            ResetDefragTelemetry();
            _initialized = false;
        }

        private void DisposeMacroDatabasePayloadCache()
        {
            if (_macroDatabasePayloadKeys.IsCreated && _macroDatabasePayloadCache.IsCreated)
            {
                for (int i = 0; i < _macroDatabasePayloadKeys.Length; i++)
                {
                    ulong sectorHash = _macroDatabasePayloadKeys[i];
                    if (_macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadHandle handle) &&
                        handle.Pointer != IntPtr.Zero)
                    {
                        H8Memory.FreeRaw(handle.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault);
                    }
                }
            }

            if (_macroDatabasePayloadKeys.IsCreated)
                _macroDatabasePayloadKeys.Dispose();
            if (_macroDatabasePayloadCache.IsCreated)
                _macroDatabasePayloadCache.Dispose();

            _macroDatabasePayloadBytes = 0L;
            _macroDatabasePayloadEvictions = 0;
        }

        private void RemoveMacroDatabaseKey(ulong sectorHash)
        {
            if (!_macroDatabasePayloadKeys.IsCreated)
                return;

            for (int i = 0; i < _macroDatabasePayloadKeys.Length; i++)
            {
                if (_macroDatabasePayloadKeys[i] != sectorHash)
                    continue;

                _macroDatabasePayloadKeys.RemoveAtSwapBack(i);
                return;
            }
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        private void ResetDefragTelemetry()
        {
            IsFragmented = false;
            HeapFragmentationRatio = 0f;
            TotalFreeSpaceBytes = 0L;
            LargestContiguousBlockBytes = 0L;
            LastDefragMovedBytes = 0L;
            PendingMassiveMoveBytes = 0L;
            LastDefragWatchdogExceeded = false;
            LastDefragFlags = 0;
            UnalignedBufferCount = 0;
            _lastRelocationRecordCount = 0;
        }

        private void AnalyzeGaps()
        {
            if (!_blocks.IsCreated || _blocks.Length == 0)
            {
                TotalFreeSpaceBytes = 0L;
                LargestContiguousBlockBytes = 0L;
                HeapFragmentationRatio = 0f;
                IsFragmented = false;
                UnalignedBufferCount = 0;
                return;
            }

            VaultGapAuditJob auditJob = default;
            auditJob.Blocks = _blocks.AsArray();
            auditJob.Result = _gapAuditResult;
            auditJob.Run();

            VaultGapAuditResult result = _gapAuditResult[0];
            TotalFreeSpaceBytes = result.TotalFreeBytes;
            LargestContiguousBlockBytes = result.LargestFreeBytes;
            UnalignedBufferCount = result.UnalignedOccupiedCount;
            HeapFragmentationRatio = result.FragmentationRatio;
            IsFragmented = HeapFragmentationRatio > FragmentationRatioThreshold;
            if (IsFragmented)
                LastDefragFlags |= DefragFlagFragmented;
            if (UnalignedBufferCount > 0)
                LastDefragFlags |= DefragFlagUnaligned;
        }

        private bool ValidateDefragTelemetry()
        {
            if (float.IsNaN(HeapFragmentationRatio) ||
                float.IsInfinity(HeapFragmentationRatio) ||
                TotalFreeSpaceBytes < 0L ||
                LargestContiguousBlockBytes < 0L ||
                LargestContiguousBlockBytes > TotalFreeSpaceBytes)
            {
                LastDefragFlags |= DefragFlagFault;
                return false;
            }

            return true;
        }

        private bool ValidateBlockMap()
        {
            if (!_blocks.IsCreated || _arenaBase == null || _arenaBytes <= 0L)
            {
                LastDefragFlags |= DefragFlagFault;
                return false;
            }

            long expectedOffset = 0L;
            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.Bytes <= 0L ||
                    block.OffsetBytes < 0L ||
                    block.OffsetBytes != expectedOffset ||
                    block.Bytes > _arenaBytes - block.OffsetBytes ||
                    (block.State != BlockStateFree && block.State != BlockStateOccupied))
                {
                    LastDefragFlags |= DefragFlagFault;
                    return false;
                }

                expectedOffset += block.Bytes;
            }

            if (expectedOffset != _arenaBytes)
            {
                LastDefragFlags |= DefragFlagFault;
                return false;
            }

            return true;
        }

        private long EstimateLargestOccupiedMoveCandidate()
        {
            if (!_blocks.IsCreated || _blocks.Length < 2)
                return 0L;

            long largest = 0L;
            for (int i = 0; i + 1 < _blocks.Length; i++)
            {
                VaultArenaBlock freeBlock = _blocks[i];
                VaultArenaBlock occupiedBlock = _blocks[i + 1];
                if (freeBlock.State != BlockStateFree ||
                    occupiedBlock.State != BlockStateOccupied)
                {
                    continue;
                }

                if (occupiedBlock.Bytes > largest)
                    largest = occupiedBlock.Bytes;
            }

            return largest;
        }

        private void MarkExternalView(int key, long offsetBytes)
        {
            if (!TryFindOccupiedBlockIndex(key, offsetBytes, out int blockIndex))
                return;

            VaultArenaBlock block = _blocks[blockIndex];
            if ((block.Reserved0 & BlockFlagExternalView) != 0)
                return;

            block.Reserved0 |= BlockFlagExternalView;
            block.Version = NextGeneration(block.Version);
            _blocks[blockIndex] = block;
            UpdateH8Descriptor(in block);

            if (_metadata.TryGetValue(key, out VaultBufferMeta meta))
            {
                meta.BlockIndex = blockIndex;
                meta.OffsetBytes = block.OffsetBytes;
                meta.Version = block.Version;
                _metadata[key] = meta;
                BumpVaultGeneration();
            }
        }

        private void RecordDefragBlackBox(uint sequence)
        {
            if (!_defragBlackBox.IsCreated || _defragBlackBox.Length == 0)
                return;

            int cursor = _defragBlackBoxCursor;
            if ((uint)cursor >= (uint)_defragBlackBox.Length)
                cursor = 0;

            MemoryDefragTelemetryEntry entry = default;
            entry.Sequence = sequence;
            entry.VaultGenerationID = _vaultGenerationId;
            entry.BlockCount = _blocks.IsCreated ? _blocks.Length : 0;
            entry.TotalFreeSpaceBytes = TotalFreeSpaceBytes;
            entry.LargestContiguousBlockBytes = LargestContiguousBlockBytes;
            entry.LastMovedBytes = LastDefragMovedBytes;
            entry.TotalMovedBytes = _totalDefragMovedBytes;
            entry.PendingMassiveMoveBytes = PendingMassiveMoveBytes;
            entry.HeapFragmentationRatio = HeapFragmentationRatio;
            entry.WatchdogBreaches = _compactionWatchdogBreachCount;
            entry.Flags = LastDefragFlags;
            entry.IsFragmented = IsFragmented ? (byte)1 : (byte)0;
            entry.WatchdogExceeded = LastDefragWatchdogExceeded ? (byte)1 : (byte)0;
            _defragBlackBox[cursor] = entry;

            cursor++;
            if (cursor >= _defragBlackBox.Length)
                cursor = 0;
            _defragBlackBoxCursor = cursor;
        }

        private void DumpDefragBlackBox()
        {
            if (_defragDumpWritten || !_defragBlackBox.IsCreated)
                return;

            _defragDumpWritten = true;
            try
            {
                string directory = Path.GetDirectoryName(DefragDumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(DefragDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    int bytes = _defragBlackBox.Length * UnsafeUtility.SizeOf<MemoryDefragTelemetryEntry>();
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_defragBlackBox);
                    stream.Write(new ReadOnlySpan<byte>(source, bytes));
                }
            }
            catch
            {
            }
        }

        private void DumpPhiVodBlackBox()
        {
            if (_phiVodDumpWritten)
                return;

            _phiVodDumpWritten = true;
            try
            {
                string directory = Path.GetDirectoryName(PhiVodDumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(PhiVodDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    if (!_defragBlackBox.IsCreated)
                        return;

                    int bytes = _defragBlackBox.Length * UnsafeUtility.SizeOf<MemoryDefragTelemetryEntry>();
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_defragBlackBox);
                    stream.Write(new ReadOnlySpan<byte>(source, bytes));
                }
            }
            catch
            {
            }
        }

        private bool TryBuildHandle<T>(BufferID bufferId, out VaultBufferHandle<T> handle) where T : struct
        {
            handle = default;
            int key = (int)bufferId;
            if (key == 0 ||
                !_buffers.TryGetValue(key, out IntPtr pointer) ||
                !_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                pointer == IntPtr.Zero ||
                meta.Length <= 0)
            {
                return false;
            }

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            ValidateType<T>(bufferId, meta, stride, alignment);
            handle.ptr = pointer.ToPointer();
            handle.generation = meta.Version;
            handle.BufferId = bufferId;
            handle.Length = meta.Length;
            handle.Stride = meta.Stride;
            return true;
        }

        private static uint NextGeneration(uint generation)
        {
            uint next = generation + 1u;
            return next == 0u ? 1u : next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BumpVaultGeneration()
        {
            _vaultGenerationId = NextGeneration(_vaultGenerationId);
        }

        private bool TryReallocateBlock(
            int key,
            VaultBufferMeta existingMeta,
            int requiredLength,
            long requiredBytes,
            bool clearExtendedBytes,
            out IntPtr resizedPointer,
            out VaultBufferMeta resizedMeta)
        {
            resizedPointer = default;
            resizedMeta = existingMeta;
            if (!TryFindOccupiedBlockIndex(key, existingMeta.OffsetBytes, out int blockIndex))
                return false;

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.Bytes < existingMeta.Bytes)
                return false;
            if ((block.Reserved0 & BlockFlagLocked) != 0 || block.Reserved1 != 0)
                return false;

            resizedPointer = (IntPtr)((byte*)_arenaBase + block.OffsetBytes);
            if (requiredBytes > block.Bytes)
            {
                long extraBytes = requiredBytes - block.Bytes;
                int rightIndex = blockIndex + 1;
                if ((uint)rightIndex >= (uint)_blocks.Length)
                    return false;

                VaultArenaBlock rightBlock = _blocks[rightIndex];
                if (rightBlock.State != BlockStateFree || rightBlock.Bytes < extraBytes)
                    return false;

                block.Bytes = requiredBytes;
                block.Version = NextGeneration(block.Version);
                _blocks[blockIndex] = block;
                UpdateH8Descriptor(in block);

                rightBlock.OffsetBytes += extraBytes;
                rightBlock.Bytes -= extraBytes;
                rightBlock.Version = NextGeneration(rightBlock.Version);
                if (rightBlock.Bytes == 0L)
                {
                    rightBlock.State = BlockStateFree;
                    UpdateH8Descriptor(in rightBlock);
                    RemoveBlockAt(rightIndex);
                    RebuildMetadataBlockIndices();
                }
                else
                {
                    _blocks[rightIndex] = rightBlock;
                    UpdateH8Descriptor(in rightBlock);
                }

                _allocatedBytes += extraBytes;
            }
            else if (requiredLength != existingMeta.Length || requiredBytes != existingMeta.Bytes)
            {
                block.Version = NextGeneration(block.Version);
                _blocks[blockIndex] = block;
                UpdateH8Descriptor(in block);
            }

            if (clearExtendedBytes && requiredLength > existingMeta.Length)
            {
                long oldPayloadBytes = (long)existingMeta.Length * existingMeta.Stride;
                long newPayloadBytes = (long)requiredLength * existingMeta.Stride;
                if (newPayloadBytes > oldPayloadBytes)
                    UnsafeUtility.MemClear((byte*)resizedPointer.ToPointer() + oldPayloadBytes, newPayloadBytes - oldPayloadBytes);
            }

            resizedMeta.Length = requiredLength;
            resizedMeta.BlockIndex = blockIndex;
            resizedMeta.OffsetBytes = block.OffsetBytes;
            resizedMeta.Bytes = requiredBytes;
            resizedMeta.Version = block.Version;
            return true;
        }

        private bool TryFindOccupiedBlockIndex(int key, long offsetBytes, out int blockIndex)
        {
            blockIndex = -1;
            if (!_blocks.IsCreated)
                return false;

            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State == BlockStateOccupied &&
                    block.BufferKey == key &&
                    block.OffsetBytes == offsetBytes)
                {
                    blockIndex = i;
                    return true;
                }
            }

            return false;
        }

        private bool TryAllocateBlock(int key, long bytes, out int blockIndex, out IntPtr pointer)
        {
            blockIndex = -1;
            pointer = default;
            if (bytes <= 0L || !_blocks.IsCreated)
                return false;

            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State != BlockStateFree || block.Bytes < bytes)
                    continue;

                blockIndex = i;
                pointer = (IntPtr)((byte*)_arenaBase + block.OffsetBytes);
                if (block.Bytes == bytes)
                {
                    block.BufferKey = key;
                    block.State = BlockStateOccupied;
                    block.Reserved0 = 0;
                    block.Version = NextGeneration(block.Version);
                    _blocks[i] = block;
                    UpdateH8Descriptor(in block);
                    return true;
                }

                if (_blocks.Length >= _blocks.Capacity)
                {
                    blockIndex = -1;
                    pointer = default;
                    return false;
                }

                VaultArenaBlock occupiedBlock = block;
                occupiedBlock.Bytes = bytes;
                occupiedBlock.BufferKey = key;
                occupiedBlock.State = BlockStateOccupied;
                occupiedBlock.Reserved0 = 0;
                occupiedBlock.Version = NextGeneration(occupiedBlock.Version);

                VaultArenaBlock freeRemainder = block;
                freeRemainder.OffsetBytes += bytes;
                freeRemainder.Bytes -= bytes;
                freeRemainder.BufferKey = 0;
                freeRemainder.State = BlockStateFree;
                freeRemainder.Reserved0 = 0;
                freeRemainder.Version = NextGeneration(freeRemainder.Version);
                freeRemainder.H8BlockIndex = H8Memory.RegisterBlockDescriptor(BuildDescriptor(in freeRemainder));

                InsertBlockAfter(i, in freeRemainder);
                _blocks[i] = occupiedBlock;
                UpdateH8Descriptor(in occupiedBlock);
                return true;
            }

            return false;
        }

        private void FreeBlock(int blockIndex)
        {
            if ((uint)blockIndex >= (uint)_blocks.Length)
                return;

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.State != BlockStateOccupied)
                return;

            block.BufferKey = 0;
            block.State = BlockStateFree;
            block.Reserved0 = 0;
            block.Version = NextGeneration(block.Version);
            _blocks[blockIndex] = block;
            UpdateH8Descriptor(in block);
            BumpVaultGeneration();
            CoalesceFreeBlocksAround(blockIndex);
        }

        private void CoalesceFreeBlocksAround(int index)
        {
            if (_blocks.Length <= 1 || (uint)index >= (uint)_blocks.Length)
                return;

            int current = index;
            if (current > 0 && IsFree(current - 1) && IsFree(current))
            {
                MergeFreeBlocks(current - 1, current);
                current--;
            }

            if (current + 1 < _blocks.Length && IsFree(current) && IsFree(current + 1))
                MergeFreeBlocks(current, current + 1);
        }

        private bool IsFree(int index)
        {
            return (uint)index < (uint)_blocks.Length && _blocks[index].State == BlockStateFree;
        }

        private void MergeFreeBlocks(int leftIndex, int rightIndex)
        {
            VaultArenaBlock left = _blocks[leftIndex];
            VaultArenaBlock right = _blocks[rightIndex];
            left.Bytes += right.Bytes;
            left.Reserved0 = 0;
            left.Version = NextGeneration(left.Version);
            _blocks[leftIndex] = left;
            UpdateH8Descriptor(in left);
            BumpVaultGeneration();

            right.Bytes = 0L;
            right.State = BlockStateFree;
            right.Reserved0 = 0;
            UpdateH8Descriptor(in right);
            RemoveBlockAt(rightIndex);
            RebuildMetadataBlockIndices();
        }

        private void InsertBlockAfter(int index, in VaultArenaBlock block)
        {
            _blocks.AddNoResize(default);
            for (int i = _blocks.Length - 1; i > index + 1; i--)
                _blocks[i] = _blocks[i - 1];
            _blocks[index + 1] = block;
            RebuildMetadataBlockIndices();
        }

        private void RemoveBlockAt(int index)
        {
            int last = _blocks.Length - 1;
            for (int i = index; i < last; i++)
                _blocks[i] = _blocks[i + 1];
            _blocks.RemoveAt(last);
        }

        private void RebuildMetadataBlockIndices()
        {
            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State != BlockStateOccupied || block.BufferKey == 0)
                    continue;

                if (!_metadata.TryGetValue(block.BufferKey, out VaultBufferMeta meta))
                    continue;

                meta.BlockIndex = i;
                meta.OffsetBytes = block.OffsetBytes;
                meta.Version = block.Version;
                _metadata[block.BufferKey] = meta;
                _buffers[block.BufferKey] = (IntPtr)((byte*)_arenaBase + block.OffsetBytes);
            }
        }

        private void UpdateH8Descriptor(in VaultArenaBlock block)
        {
            if (block.H8BlockIndex < 0)
                return;

            H8Memory.TryUpdateBlockDescriptor(block.H8BlockIndex, BuildDescriptor(in block));
        }

        private BlockDescriptor BuildDescriptor(in VaultArenaBlock block)
        {
            ushort flags = (ushort)(H8AllocationFlags.Raw | H8AllocationFlags.Vault | H8AllocationFlags.Relocatable);
            BlockDescriptor descriptor = default;
            descriptor.BasePointer = (IntPtr)_arenaBase;
            descriptor.OffsetBytes = block.OffsetBytes;
            descriptor.Bytes = block.Bytes;
            descriptor.OwnerKey = block.BufferKey;
            descriptor.Generation = unchecked((int)block.Version);
            descriptor.Owner = SystemID.CoreDataVault;
            descriptor.Flags = flags;
            descriptor.State = block.State == BlockStateOccupied ? (byte)H8BlockState.Occupied : (byte)H8BlockState.Free;
            return descriptor;
        }

        private static bool ShouldClear(NativeArrayOptions options)
        {
            return options != NativeArrayOptions.UninitializedMemory;
        }

        private static void ValidateType<T>(BufferID bufferId, VaultBufferMeta meta, int stride, int alignment) where T : struct
        {
            if (meta.Stride != stride || meta.Alignment != alignment)
                FatalMemoryException.ThrowVaultTypeMismatch();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long AlignUp(long value, int alignment)
        {
            long mask = alignment - 1L;
            return (value + mask) & ~mask;
        }
    }
}
