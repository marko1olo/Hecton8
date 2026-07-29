using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Dispatcher phase gate for vault compaction. Movement is legal only before jobs are scheduled.
    /// </summary>
    public enum MemoryDefragPhase : byte
    {
        Unspecified = 0,
        PreSimulation = 1,
        VisualSync = 2
    }

    /// <summary>
    /// Registry-facing data-vault contract. Systems request buffers here instead of owning persistent arrays.
    /// </summary>
    public interface IDataVault : IDisposable, IMacroDatabaseNativeCacheOwner
    {
        /// <summary>Total allocated vault bytes.</summary>
        long AllocatedBytes { get; }

        /// <summary>Current reserved vault arena bytes.</summary>
        long ArenaBytes { get; }

        /// <summary>Allocated-to-arena pressure ratio.</summary>
        float CapacityPressure01 { get; }

        /// <summary>True while allocations are blocked for an AUP shift.</summary>
        bool IsAllocationLocked { get; }

        /// <summary>True while vault aliases are fenced by a critical maintenance pass.</summary>
        bool IsCompactionFenceActive { get; }

        /// <summary>Bitmask of vault buffers currently held by scheduled jobs.</summary>
        uint ActiveBurstLockMask { get; }

        /// <summary>64-bit lock-free mutation mask for contested vault writers.</summary>
        ulong ActiveMutationGuardMask { get; }

        /// <summary>True when the most recent gap analysis crossed the fragmentation threshold.</summary>
        bool IsFragmented { get; }

        /// <summary>Fragmentation ratio from the most recent gap analysis.</summary>
        float HeapFragmentationRatio { get; }

        /// <summary>Total free arena space from the most recent gap analysis.</summary>
        long TotalFreeSpaceBytes { get; }

        /// <summary>Largest contiguous free block from the most recent gap analysis.</summary>
        long LargestContiguousBlockBytes { get; }

        /// <summary>Bytes moved by the most recent bounded relocation pass.</summary>
        long LastDefragMovedBytes { get; }

        /// <summary>Largest occupied block that would require a pause/loading mask before relocation.</summary>
        long PendingMassiveMoveBytes { get; }

        /// <summary>True when a relocation pass exceeds its watchdog threshold.</summary>
        bool LastDefragWatchdogExceeded { get; }

        /// <summary>Bitfield describing the most recent defrag telemetry pass.</summary>
        byte LastDefragFlags { get; }

        /// <summary>Occupied buffers that failed the 64-byte alignment audit.</summary>
        int UnalignedBufferCount { get; }

        /// <summary>Total bytes moved by vault relocation since initialization.</summary>
        long TotalDefragMovedBytes { get; }

        /// <summary>System that owned the most recent relocated block.</summary>
        SystemID LastRelocatedSystemID { get; }

        /// <summary>Total number of relocation passes that breached the watchdog.</summary>
        int CompactionWatchdogBreachCount { get; }

        /// <summary>Global vault generation for black-box telemetry and stale-handle audits.</summary>
        uint VaultGenerationID { get; }

        /// <summary>Total generation-checked handles refreshed after a vault generation change.</summary>
        int GenerationHandleMissCount { get; }

        /// <summary>Bitmask describing the most recent starvation fallback path.</summary>
        byte MemoryStarvationWarnings { get; }

        /// <summary>Current number of arena blocks exposed for editor-only memory maps.</summary>
        int MemoryBlockSnapshotCount { get; }

        /// <summary>Records one no-allocation heartbeat into the fixed 300-frame vault telemetry ring.</summary>
        void RecordHeartbeat();

        /// <summary>Relocation records emitted by bounded live relocation.</summary>
        int LastRelocationRecordCount { get; }

        /// <summary>Returns the 16-byte generation descriptor for a persistent buffer, growing the vault buffer when required.</summary>
        VaultGenerationHandle<T> EnsureGenerationHandle<T>(BufferID bufferId, int requiredLength, SystemID requester, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct;

        /// <summary>Attempts to read an existing 16-byte generation descriptor without creating or growing it.</summary>
        bool TryGetGenerationHandle<T>(BufferID bufferId, out VaultGenerationHandle<T> handle) where T : struct;

        /// <summary>Resolves a 16-byte generation descriptor into a transient current-phase view; use a lock or pinned alias for cross-phase/job lifetime.</summary>
        bool TryResolveHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct;

        /// <summary>Legacy current-phase mutable view; prefer <see cref="TryReadOnlyHandle{T}"/> for consumer readbacks and use a lock for cross-phase/job lifetime.</summary>
        bool TryReadHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct;

        /// <summary>Pure current-phase read accessor; it does not pin relocation metadata.</summary>
        bool TryReadOnlyHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct;

        /// <summary>Resolves a generation slice descriptor into a mutable owner-write sub-view for the current phase; use a lock for cross-phase/job lifetime.</summary>
        bool TryResolveSlice<T>(in VaultSliceHandle<T> handle, out NativeArray<T> slice) where T : struct;

        /// <summary>Returns a safe slice descriptor without exposing a raw pointer across phases.</summary>
        bool TryAcquireSliceHandle<T>(
            BufferID bufferId,
            int requiredLength,
            int startIndex,
            int count,
            SystemID requester,
            out VaultSliceHandle<T> slice,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct;

        /// <summary>Attempts to acquire an explicit writer fence for one generation handle.</summary>
        bool TryAcquireWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID, out NativeArray<T> buffer) where T : struct;

        /// <summary>Releases a writer fence acquired by <see cref="TryAcquireWriteLock{T}"/>.</summary>
        bool ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID) where T : struct;

        /// <summary>Releases one reference to a vault buffer and invalidates stale generation descriptors.</summary>
        bool ReleaseBuffer<T>(in VaultGenerationHandle<T> handle) where T : struct;

        /// <summary>Attempts to read the current generation for a buffer.</summary>
        bool TryGetBufferGeneration(BufferID bufferId, out uint generation);

        /// <summary>Pins a relocation-protected read-only alias over an existing buffer and records alias metadata.</summary>
        NativeArray<T>.ReadOnly PinReadOnlyAlias<T>(BufferID bufferId, SystemID requester) where T : struct;

        /// <summary>Compatibility bridge for pinned read-only aliases; prefer <see cref="PinReadOnlyAlias{T}"/>.</summary>
        [Obsolete("Use PinReadOnlyAlias<T>; CreateAlias pins relocation metadata and is not a pure read accessor.", false)]
        NativeArray<T>.ReadOnly CreateAlias<T>(BufferID bufferId, SystemID requester) where T : struct;

        /// <summary>Releases vault buffers owned by one system without shrinking the reusable arena.</summary>
        int ReleaseOwnerBuffers(SystemID owner, out long releasedBytes);

        /// <summary>Releases scene-owned vault buffers before scene-transition baseline verification.</summary>
        int ReleaseSceneOwnedBuffers(out long releasedBytes);

        /// <summary>Releases scene-owned vault buffers and reports any locked or corrupt survivors.</summary>
        int ReleaseSceneOwnedBuffers(out long releasedBytes, out int remainingCount, out long remainingBytes, out int lockedCount);

        /// <summary>PRE_SIMULATION orphan sweep using an unmanaged live-owner table; returns reclaimed buffer count.</summary>
        int SweepOrphanedHandles(
            NativeArray<SystemID> liveOwners,
            int liveOwnerCount,
            MemoryDefragPhase phase,
            uint activeBurstLockMask,
            out long releasedBytes);

        /// <summary>Counts scene-owned vault buffers that still occupy the reusable arena.</summary>
        int CountSceneOwnedBuffers(out long bytes, out int lockedCount);

        /// <summary>Locks a buffer while an external job owns its pointer.</summary>
        [Obsolete("Use the owner-tagged overload so compaction telemetry records the scheduling system.", false)]
        bool TryLockBuffer(BufferID bufferId);

        /// <summary>Locks a buffer while an external job owned by a specific system owns its pointer.</summary>
        bool TryLockBuffer(BufferID bufferId, SystemID lockOwner);

        /// <summary>Unlocks a previously locked buffer.</summary>
        [Obsolete("Use the owner-tagged overload so compaction telemetry records the scheduling system.", false)]
        bool TryUnlockBuffer(BufferID bufferId);

        /// <summary>Unlocks a previously locked buffer owned by a specific system.</summary>
        bool TryUnlockBuffer(BufferID bufferId, SystemID lockOwner);

        /// <summary>Attempts to atomically reserve one or more writer bits.</summary>
        bool TryAcquireMutationGuard(ulong writeMask);

        /// <summary>Releases writer bits acquired by <see cref="TryAcquireMutationGuard"/>.</summary>
        void ReleaseMutationGuard(ulong writeMask);

        /// <summary>Attempts to read one relocation record from future offline relocation.</summary>
        bool TryGetLastRelocationRecord(int index, out VaultRelocationRecord record);

        /// <summary>Attempts to read one immutable memory-block snapshot for diagnostics/editor visualization.</summary>
        bool TryGetMemoryBlockSnapshot(int index, out VaultMemoryBlockSnapshot snapshot);

        /// <summary>Locks vault allocation while AUP positions are being rebased.</summary>
        void LockAllocationsForAupShift(uint shiftFrameId);

        /// <summary>Unlocks vault allocation after an AUP shift barrier resolves.</summary>
        void UnlockAllocationsAfterAupShift(uint shiftFrameId);

        /// <summary>Runs cold fragmentation maintenance.</summary>
        [Obsolete("Use the explicit PRE_SIMULATION overload. Legacy overloads record blocked telemetry and never move memory.", false)]
        void FrostTickDefrag(float elapsedSeconds);

        /// <summary>Runs cold fragmentation maintenance with a caller-provided stress gate.</summary>
        [Obsolete("Use the explicit PRE_SIMULATION overload. Legacy overloads record blocked telemetry and never move memory.", false)]
        void FrostTickDefrag(float elapsedSeconds, float systemStress01);

        /// <summary>Runs cold fragmentation maintenance from a dispatcher phase with explicit job-lock state.</summary>
        void FrostTickDefrag(float elapsedSeconds, float systemStress01, MemoryDefragPhase phase, uint activeBurstLockMask);

        /// <summary>Editor-only command hook; actual relocation still runs through the PRE_SIMULATION fence.</summary>
        void RequestEditorForceDefragmentation();
    }

    /// <summary>
    /// Raw 16-byte generation descriptor. No pointer, no properties, no managed state.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct VaultGenerationHandle<T> where T : struct
    {
        public uint BufferID;
        public uint SystemID;
        public uint Generation;
        public uint Flags;
    }

    /// <summary>
    /// Legacy public name kept for source compatibility. It is now pointer-free and layout-identical to VaultGenerationHandle.
    /// </summary>
    /// <typeparam name="T">Blittable element type.</typeparam>
    [Obsolete("Legacy name. Persist VaultGenerationHandle<T>; this descriptor contains no raw pointer.", false)]
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct VaultBufferHandle<T> where T : struct
    {
        public uint BufferID;
        public uint SystemID;
        public uint Generation;
        public uint Flags;

        /// <summary>Returns the strict generation descriptor form without carrying pointer metadata.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VaultGenerationHandle<T> ToGenerationHandle()
        {
            VaultGenerationHandle<T> handle = default;
            handle.BufferID = BufferID;
            handle.SystemID = SystemID;
            handle.Generation = Generation;
            handle.Flags = Flags;
            return handle;
        }
    }

    /// <summary>
    /// Pointer-free slice descriptor derived from a generation handle. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VaultSliceHandle<T> where T : struct
    {
        public uint BufferID;
        public uint SystemID;
        public uint Generation;
        public uint HandleFlags;
        public int StartIndex;
        public int Length;
        public uint Flags;
        public uint Reserved0;
    }

    /// <summary>
    /// Fixed-size relocation record copied from the memory assembly to the Core signal bridge.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VaultRelocationRecord
    {
        public const byte FlagAddressChanged = 1 << 0;
        public const byte FlagFenceProtected = 1 << 1;
        public const byte FlagWatchdogBreached = 1 << 2;

        [FieldOffset(0)] public long OldOffsetBytes;
        [FieldOffset(8)] public long NewOffsetBytes;
        [FieldOffset(16)] public int BufferId;
        [FieldOffset(20)] public int ByteLength;
        [FieldOffset(24)] public uint Generation;
        [FieldOffset(30)] public ushort Reserved;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte SystemId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct VaultBufferMeta
    {
        [FieldOffset(0)] public long OffsetBytes;
        [FieldOffset(8)] public long Bytes;
        [FieldOffset(16)] public int Length;
        [FieldOffset(20)] public int Stride;
        [FieldOffset(24)] public int Alignment;
        [FieldOffset(28)] public int BlockIndex;
        [FieldOffset(32)] public Allocator Allocator;
        [FieldOffset(36)] public uint Version;
        [FieldOffset(44)] public int ActiveWriterSystemID;
        [FieldOffset(48)] public uint TypeHash;
        [FieldOffset(52)] public uint RefCount;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public int BufferKey;
        [FieldOffset(40)] public SystemID Owner;
        [FieldOffset(42)] public SystemID LastAliasRequester;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultTelemetrySnapshot
    {
        [FieldOffset(0)] public long AllocatedBytes;
        [FieldOffset(8)] public long ArenaBytes;
        [FieldOffset(16)] public long LastMovedBytes;
        [FieldOffset(24)] public long ResolutionTicks;
        [FieldOffset(56)] public long ResolvedHandleCount;
        [FieldOffset(32)] public uint VaultGenerationID;
        [FieldOffset(36)] public uint GenerationMismatchCount;
        [FieldOffset(40)] public int LastFaultBufferID;
        [FieldOffset(44)] public uint LastFaultHandleGeneration;
        [FieldOffset(48)] public uint LastFaultMetaGeneration;
        [FieldOffset(54)] public ushort Reserved1;
        [FieldOffset(52)] public byte LastDefragFlags;
        [FieldOffset(53)] public byte Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VaultMemoryBudgetEntry
    {
        [FieldOffset(8)] public long BudgetBytes;
        [FieldOffset(16)] public long DefragThresholdBytes;
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public int BufferID;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    /// <summary>
    /// Immutable block snapshot used by editor diagnostics. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct VaultMemoryBlockSnapshot
    {
        [FieldOffset(0)] public long OffsetBytes;
        [FieldOffset(8)] public long Bytes;
        [FieldOffset(40)] public long Reserved2;
        [FieldOffset(16)] public int BufferKey;
        [FieldOffset(20)] public int H8BlockIndex;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(36)] public uint Reserved1;
        [FieldOffset(28)] public ushort Owner;
        [FieldOffset(30)] public ushort LockCount;
        [FieldOffset(34)] public ushort Reserved0;
        [FieldOffset(32)] public byte State;
        [FieldOffset(33)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct VaultArenaBlock
    {
        [FieldOffset(0)] public long OffsetBytes;
        [FieldOffset(8)] public long Bytes;
        [FieldOffset(16)] public int BufferKey;
        [FieldOffset(20)] public int H8BlockIndex;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(30)] public ushort Reserved1;
        [FieldOffset(28)] public byte State;
        [FieldOffset(29)] public byte Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct DeferredVaultReleaseRequest
    {
        [FieldOffset(0)] public int State;
        [FieldOffset(4)] public int BufferKey;
        [FieldOffset(8)] public long OffsetBytes;
        [FieldOffset(16)] public int ActiveLockBit;
        [FieldOffset(20)] public int LockOwnerSystemId;
        [FieldOffset(24)] public byte Kind;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] public ushort Reserved16;
        [FieldOffset(28)] public uint Sequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct VaultThreadWriteLockSlot
    {
        [FieldOffset(0)] public int State;
        [FieldOffset(4)] public int ThreadId;
        [FieldOffset(8)] public int BufferKey;
        [FieldOffset(12)] public int SystemId;
        [FieldOffset(16)] public long OffsetBytes;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct MemoryDefragTelemetryEntry
    {
        [FieldOffset(0)] public long TotalFreeSpaceBytes;
        [FieldOffset(8)] public long LargestContiguousBlockBytes;
        [FieldOffset(16)] public long LastMovedBytes;
        [FieldOffset(24)] public long TotalMovedBytes;
        [FieldOffset(32)] public long PendingMassiveMoveBytes;
        [FieldOffset(40)] public ulong ActiveMutationGuardMask;
        [FieldOffset(48)] public uint Sequence;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint VaultGenerationID;
        [FieldOffset(60)] public uint ActiveBurstLockMask;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct MemoryDefragTelemetryDetailEntry
    {
        [FieldOffset(0)] public int BlockCount;
        [FieldOffset(4)] public int ActiveBufferCount;
        [FieldOffset(8)] public int WatchdogBreaches;
        [FieldOffset(12)] public int LockedSkipCount;
        [FieldOffset(16)] public float HeapFragmentationRatio;
        [FieldOffset(20)] public uint GenerationMismatchCount;
        [FieldOffset(24)] public long ResolutionTicks;
        [FieldOffset(32)] public long ResolvedHandleCount;
        [FieldOffset(40)] public int LastFaultBufferID;
        [FieldOffset(44)] public uint LastFaultHandleGeneration;
        [FieldOffset(48)] public uint LastFaultMetaGeneration;
        [FieldOffset(52)] public uint Reserved32;
        [FieldOffset(56)] public ushort LastRelocatedSystemId;
        [FieldOffset(58)] public ushort Reserved16;
        [FieldOffset(60)] public byte Flags;
        [FieldOffset(61)] public byte IsFragmented;
        [FieldOffset(62)] public byte WatchdogExceeded;
        [FieldOffset(63)] public byte MemoryStarvationWarnings;
    }

    /// <summary>
    /// Persistent raw-memory authority for cross-system buffers.
    /// </summary>
    public sealed unsafe class GlobalDataVault : IDataVault
    {
        private const int DefaultBufferCapacity = 128;
        private const int MaxBufferCapacity = 32768;
        private const int MaxGenerationHandleCapacity = 100000;
        private const int MaxBlockCapacity = MaxBufferCapacity << 1;
        internal const int VaultBlockAlignment = 64;
        private const long DefaultArenaBytes = 128L * 1024L * 1024L;
        public const long MinimumQualityArenaLimitBytes = 512L * 1024L * 1024L;
        public const long MaximumQualityArenaLimitBytes = 4L * 1024L * 1024L * 1024L;
        private const float MinimumQualityFragmentationRatioThreshold = 0.15f;
        private const float MaximumQualityFragmentationRatioThreshold = 0.30f;
        private const float StressDefragHaltThreshold = 0.9f;
        private const long MassiveMoveThresholdBytes = 50L * 1024L * 1024L;
        private const long MaxLiveDefragMoveBytesPerSlice = 1024L;
        private const long ArenaGrowSlackBytes = 64L * 1024L * 1024L;
        private const byte MacroDatabasePayloadDirtyFlag = 1 << 0;
        private const int MaxMacroDatabasePayloadBytes = 256 * 1024;
        private const int MaxRelocationRecordCount = 64;
        private const int MaxMemoryBudgetEntries = 128;
        private const int DeferredReleaseRequestCapacity = 256;
        private const int DeferredReleaseRequestMask = DeferredReleaseRequestCapacity - 1;
        internal const byte BlockStateFree = 0;
        internal const byte BlockStateOccupied = 1;
        private const byte BlockFlagExternalView = 1 << 0;
        private const byte BlockFlagLocked = 1 << 1;
        private const byte DeferredReleaseKindWriter = 1;
        private const byte DeferredReleaseKindBufferPin = 2;
        private const int DeferredReleaseStateEmpty = 0;
        private const int DeferredReleaseStateWriting = 1;
        private const int DeferredReleaseStatePending = 2;
        private const int WriterThreadLockSlotCapacity = 128;
        private const int WriterThreadLockSlotStateEmpty = 0;
        private const int WriterThreadLockSlotStateWriting = 1;
        private const int WriterThreadLockSlotStateActive = 2;
        private const uint VaultMetaFlagOrphanCandidate = 1u << 31;
        private const byte DefragFlagFragmented = 1 << 0;
        private const byte DefragFlagHeartbeat = 1 << 1;
        private const byte DefragFlagStressHalt = 1 << 2;
        private const byte DefragFlagMassiveMovePending = 1 << 3;
        private const byte DefragFlagFault = 1 << 4;
        private const byte DefragFlagRelocated = 1 << 5;
        private const byte DefragFlagUnaligned = 1 << 6;
        private const byte DefragFlagAliasBlocked = 1 << 7;
        private const int DefragBlackBoxFrameCount = 300;
        private const int VaultBufferHandleSizeBytes = 16;
        private const int VaultGenerationHandleSizeBytes = 16;
        private const int VaultSliceHandleSizeBytes = 32;
        private const int VaultRelocationRecordSizeBytes = 32;
        private const int VaultBufferMetaSizeBytes = 64;
        private const int VaultMemoryBlockSnapshotSizeBytes = 48;
        private const int VaultArenaBlockSizeBytes = 32;
        private const int MemoryDefragTelemetryEntrySizeBytes = 64;
        private const int MemoryDefragTelemetryDetailEntrySizeBytes = 64;
        private const int VaultTelemetrySnapshotSizeBytes = 64;
        private const int VaultMemoryBudgetEntrySizeBytes = 32;
        private const int DeferredVaultReleaseRequestSizeBytes = 32;
        private const int VaultThreadWriteLockSlotSizeBytes = 24;
        private const int MacroDatabasePayloadHandleSizeBytes = 40;
        private const int MacroDatabasePayloadCacheEntrySizeBytes = 48;
        private const string NativeMemoryOwner = nameof(GlobalDataVault);
        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct MacroDatabasePayloadCacheEntry
        {
            [FieldOffset(0)] public MacroDatabasePayloadHandle Handle;
            [FieldOffset(40)] internal IntPtr Pointer;
        }

        private UnsafeHashMap<int, IntPtr> _buffers;
        private UnsafeHashMap<int, VaultBufferMeta> _metadata;
        private UnsafeHashMap<int, uint> _metadataGenerationByBufferId;
        private NativeArray<VaultBufferMeta> _metadataByBufferId;
        private NativeList<int> _keys;
        private NativeList<VaultArenaBlock> _blocks;
        private NativeArray<MemoryDefragTelemetryEntry> _defragBlackBox;
        private NativeArray<MemoryDefragTelemetryDetailEntry> _defragBlackBoxDetails;
        private NativeArray<VaultRelocationRecord> _lastRelocationRecords;
        private NativeArray<VaultMemoryBudgetEntry> _memoryBudgetEntries;
        private NativeArray<DeferredVaultReleaseRequest> _deferredReleaseRequests;
        private NativeArray<VaultThreadWriteLockSlot> _writerThreadLockSlots;
        private NativeParallelHashMap<ulong, MacroDatabasePayloadCacheEntry> _macroDatabasePayloadCache;
        private NativeParallelHashMap<ulong, uint> _macroDatabasePayloadAccessTicks;
        private NativeList<ulong> _macroDatabasePayloadKeys;
        private int _buffersSentinelId;
        private int _metadataSentinelId;
        private int _metadataGenerationByBufferIdSentinelId;
        private int _keysSentinelId;
        private int _blocksSentinelId;
        private int _macroDatabasePayloadCacheSentinelId;
        private int _macroDatabasePayloadAccessTicksSentinelId;
        private int _macroDatabasePayloadKeysSentinelId;
        private void* _arenaBase;
        private long _arenaBytes;
        private long _arenaCapacityLimitBytes;
        private long _allocationLock;
        private int _compactionFence;
        private int _activeLocks;
        private int _blockMutationGate;
        private int _mutationGuardMaskLow;
        private int _mutationGuardMaskHigh;
        // Mutation-guard-keyed shadow of the same locks _activeLocks summarises. _activeLocks stays a
        // 32-lane residue summary because ActiveBurstLockMask, HasActiveBurstLocks and FrostTickDefrag are
        // built on it; this pair is keyed the way a guard MASK is keyed instead, so the guard conflict test
        // stops folding 64 bits onto 32 lanes. Split into two ints rather than one long to match the proven
        // _mutationGuardMaskLow/_mutationGuardMaskHigh pattern and keep every update a 32-bit Interlocked
        // op on ARM64 as well as x64. Maintained only under the block mutation gate.
        private int _activeGuardLockMaskLow;
        private int _activeGuardLockMaskHigh;
        // Second shadow of the same locks, keyed STRICTLY by (id & 63) instead of by both candidates.
        // The pair above is a union of the tree's two mask conventions, so it stays fail-closed for a caller
        // whose convention cannot be determined; this pair is exact for a caller that provably uses
        // (id & 63). Which one applies is decided by the caller's own mask - see
        // HasActiveLockConflictForMutationMask. Maintained only under the block mutation gate, alongside the
        // union pair, so the two can never disagree about which locks exist.
        private int _activeGuardLock64MaskLow;
        private int _activeGuardLock64MaskHigh;
        private bool _memMoveBlockedByStress;
        private byte _memoryStarvationWarnings;
        private long _allocatedBytes;
        private long _macroDatabasePayloadBytes;
        private int _macroDatabasePayloadEvictions;
        private uint _macroDatabaseCacheAccessClock;
        private long _lastPublishedPointerBits;
        private int _defragBlackBoxCursor;
        private int _defragBlackBoxRecordedCount;
        private int _lastRelocationRecordCount;
        private int _memoryBudgetCount;
        private int _deferredReleaseWriteCursor;
        private int _deferredReleasePendingCount;
        private int _deferredReleaseEnqueueGate;
        private int _generationHandleMissCount;
        private int _lastFaultBufferId;
        private uint _lastFaultHandleGeneration;
        private uint _lastFaultMetaGeneration;
        private long _resolvedHandleCount;
        private long _resolutionTickAccumulator;
        private int _forceDefragRequested;
        private int _lastOrphanSweepCandidateCount;
        private int _lastOrphanReclaimCount;
        private int _compactionWatchdogBreachCount;
        private int _defragLockedSkipCount;
        private int _memorySentryDumpInFlight;
        private int _memorySentryDumpRequested;
        private int _memorySentryDumpWritten;
        private long _totalDefragMovedBytes;
        private long _deferredArenaGrowthBytes;
        private int _arenaGrowthInProgress;
        private uint _defragTickSequence;
        private uint _vaultGenerationId;
        private SystemID _lastRelocatedSystemId;
        private bool _defragDumpWritten;
        private bool _phiVodDumpWritten;
        private bool _shinobu202DumpWritten;
        private bool _initialized;
        private bool _disposed;
        private static GlobalDataVault _latestCreated;

        /// <inheritdoc />
        public long AllocatedBytes => _allocatedBytes;

        /// <inheritdoc />
        public long ArenaBytes => _arenaBytes;

        /// <inheritdoc />
        public float CapacityPressure01 => _arenaBytes > 0L
            ? math.saturate((float)((double)_allocatedBytes / _arenaBytes))
            : 0f;

        /// <inheritdoc />
        public bool IsAllocationLocked => Interlocked.Read(ref _allocationLock) != 0L;

        /// <inheritdoc />
        public bool IsCompactionFenceActive => Volatile.Read(ref _compactionFence) != 0;

        /// <inheritdoc />
        public uint ActiveBurstLockMask => unchecked((uint)Volatile.Read(ref _activeLocks));

        /// <inheritdoc />
        public ulong ActiveMutationGuardMask =>
            ((ulong)(uint)Volatile.Read(ref _mutationGuardMaskHigh) << 32) |
            (uint)Volatile.Read(ref _mutationGuardMaskLow);

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
        public SystemID LastRelocatedSystemID => _lastRelocatedSystemId;

        /// <inheritdoc />
        public int CompactionWatchdogBreachCount => _compactionWatchdogBreachCount;

        /// <inheritdoc />
        public uint VaultGenerationID => _vaultGenerationId;

        /// <inheritdoc />
        public int GenerationHandleMissCount => Volatile.Read(ref _generationHandleMissCount);

        /// <inheritdoc />
        public byte MemoryStarvationWarnings => _memoryStarvationWarnings;

        /// <inheritdoc />
        public int MemoryBlockSnapshotCount => _blocks.IsCreated ? _blocks.Length : 0;

        public long DeferredArenaGrowthBytes => Volatile.Read(ref _deferredArenaGrowthBytes);

        /// <inheritdoc />
        public int LastRelocationRecordCount => _lastRelocationRecordCount;

        /// <summary>
        /// Clears the retained bootstrap vault pointer before a no-domain-reload Play Mode restart.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForSubsystemRegistration()
        {
            DisposeLatestCreatedForNativeMemoryShutdown();
            _latestCreated = null;
        }

        /// <summary>
        /// Creates and initializes the vault for bootstrap registration.
        /// </summary>
        public static GlobalDataVault Create(int capacity = DefaultBufferCapacity, long arenaCapacityLimitBytes = MinimumQualityArenaLimitBytes)
        {
            GlobalDataVault vault = new GlobalDataVault();
            vault.Initialize(capacity, arenaCapacityLimitBytes);
            if (vault._initialized)
            {
                _latestCreated = vault;
                return vault;
            }

            vault.AbortInitialize();
            FatalMemoryException.ThrowVaultInitializationFailed();
            return vault;
        }

        /// <summary>Returns the most recently initialized vault for editor diagnostics.</summary>
        public static bool TryGetLatestCreated(out GlobalDataVault vault)
        {
            vault = _latestCreated;
            return vault != null && vault._initialized;
        }

        internal static void DisposeLatestCreatedForNativeMemoryShutdown()
        {
            GlobalDataVault vault = _latestCreated;
            if (vault == null)
                return;

            _latestCreated = null;
            vault.Dispose();
        }

        /// <summary>
        /// Initializes raw vault maps.
        /// </summary>
        public void Initialize(int capacity = DefaultBufferCapacity, long arenaCapacityLimitBytes = MinimumQualityArenaLimitBytes)
        {
            if (_initialized || _disposed)
                return;

            if (!ValidateAbiLayout())
                return;
            int safeCapacity = ResolveBufferCapacity(capacity);
            int blockCapacity = ResolveBlockCapacity(safeCapacity);

            H8Memory.Initialize();
            if (!H8Memory.IsInitialized)
                return;

            try
            {
                _buffers = new UnsafeHashMap<int, IntPtr>(safeCapacity, Allocator.Persistent);
                _metadata = new UnsafeHashMap<int, VaultBufferMeta>(safeCapacity, Allocator.Persistent);
                _metadataGenerationByBufferId = new UnsafeHashMap<int, uint>(safeCapacity, Allocator.Persistent);
                _metadataByBufferId = H8Memory.Allocate<VaultBufferMeta>(
                    MaxGenerationHandleCapacity,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                if (!_metadataByBufferId.IsCreated)
                {
                    AbortInitialize();
                    return;
                }

                InitializeVaultMetadataJob metadataJob = new InitializeVaultMetadataJob
                {
                    Metadata = _metadataByBufferId
                };
                for (int i = 0; i < _metadataByBufferId.Length; i++)
                    metadataJob.Execute(i);
                _keys = new NativeList<int>(safeCapacity, Allocator.Persistent);
                _blocks = new NativeList<VaultArenaBlock>(blockCapacity, Allocator.Persistent);
                _defragBlackBox = H8Memory.Allocate<MemoryDefragTelemetryEntry>(
                    DefragBlackBoxFrameCount,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                if (!_defragBlackBox.IsCreated)
                {
                    AbortInitialize();
                    return;
                }

                _defragBlackBoxDetails = H8Memory.Allocate<MemoryDefragTelemetryDetailEntry>(
                    DefragBlackBoxFrameCount,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                if (!_defragBlackBoxDetails.IsCreated)
                {
                    AbortInitialize();
                    return;
                }

                _lastRelocationRecords = H8Memory.Allocate<VaultRelocationRecord>(
                    MaxRelocationRecordCount,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                if (!_lastRelocationRecords.IsCreated)
                {
                    AbortInitialize();
                    return;
                }

                _memoryBudgetEntries = H8Memory.Allocate<VaultMemoryBudgetEntry>(
                    MaxMemoryBudgetEntries,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                if (!_memoryBudgetEntries.IsCreated)
                {
                    AbortInitialize();
                    return;
                }

                InitializeVaultBudgetEntriesJob budgetJob = new InitializeVaultBudgetEntriesJob
                {
                    Entries = _memoryBudgetEntries
                };
                for (int i = 0; i < _memoryBudgetEntries.Length; i++)
                    budgetJob.Execute(i);
                _deferredReleaseRequests = H8Memory.Allocate<DeferredVaultReleaseRequest>(
                    DeferredReleaseRequestCapacity,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                if (!_deferredReleaseRequests.IsCreated)
                {
                    AbortInitialize();
                    return;
                }

                _writerThreadLockSlots = H8Memory.Allocate<VaultThreadWriteLockSlot>(
                    WriterThreadLockSlotCapacity,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                if (!_writerThreadLockSlots.IsCreated)
                {
                    AbortInitialize();
                    return;
                }

                _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadCacheEntry>(safeCapacity, Allocator.Persistent);
                _macroDatabasePayloadAccessTicks = new NativeParallelHashMap<ulong, uint>(safeCapacity, Allocator.Persistent);
                _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);
                RegisterNativeSidecarStorage();
                if (!HasInitializedCriticalNativeStorage())
                {
                    AbortInitialize();
                    return;
                }

                _arenaCapacityLimitBytes = ResolveArenaCapacityLimit(arenaCapacityLimitBytes);
                _arenaBytes = AlignUp(math.min(DefaultArenaBytes, _arenaCapacityLimitBytes), VaultBlockAlignment);
                _arenaBase = H8Memory.AllocateRaw(
                    _arenaBytes,
                    VaultBlockAlignment,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    clearMemory: true,
                    H8AllocationFlags.Vault | H8AllocationFlags.SubAllocatorRoot);
                if (_arenaBase == null)
                {
                    AbortInitialize();
                    return;
                }

                Interlocked.Exchange(ref _allocationLock, 0L);
                _compactionFence = 0;
                _activeLocks = 0;
                _blockMutationGate = 0;
                _mutationGuardMaskLow = 0;
                _mutationGuardMaskHigh = 0;
                _activeGuardLockMaskLow = 0;
                _activeGuardLockMaskHigh = 0;
                _activeGuardLock64MaskLow = 0;
                _activeGuardLock64MaskHigh = 0;
                _memoryStarvationWarnings = 0;
                _allocatedBytes = 0L;
                _macroDatabasePayloadBytes = 0L;
                _macroDatabasePayloadEvictions = 0;
                _macroDatabaseCacheAccessClock = 0u;
                _lastPublishedPointerBits = 0L;
                _defragBlackBoxCursor = 0;
                _defragBlackBoxRecordedCount = 0;
                _lastRelocationRecordCount = 0;
                _memoryBudgetCount = 0;
                _deferredReleaseWriteCursor = 0;
                _deferredReleasePendingCount = 0;
                _deferredReleaseEnqueueGate = 0;
                _generationHandleMissCount = 0;
                _lastFaultBufferId = 0;
                _lastFaultHandleGeneration = 0u;
                _lastFaultMetaGeneration = 0u;
                _resolvedHandleCount = 0L;
                _resolutionTickAccumulator = 0L;
                _forceDefragRequested = 0;
                _lastOrphanSweepCandidateCount = 0;
                _lastOrphanReclaimCount = 0;
                _compactionWatchdogBreachCount = 0;
                _defragLockedSkipCount = 0;
                _memorySentryDumpInFlight = 0;
                _memorySentryDumpRequested = 0;
                _memorySentryDumpWritten = 0;
                _totalDefragMovedBytes = 0L;
                _deferredArenaGrowthBytes = 0L;
                _arenaGrowthInProgress = 0;
                _defragTickSequence = 0u;
                _vaultGenerationId = 1u;
                _lastRelocatedSystemId = SystemID.Unknown;
                _defragDumpWritten = false;
                _phiVodDumpWritten = false;
                _shinobu202DumpWritten = false;
                ResetDefragTelemetry();
                if (_arenaBase != null && _blocks.Capacity > 0)
                {
                    VaultArenaBlock freeBlock = default;
                    freeBlock.OffsetBytes = 0L;
                    freeBlock.Bytes = _arenaBytes;
                    freeBlock.BufferKey = 0;
                    freeBlock.Version = 1u;
                    freeBlock.State = BlockStateFree;
                    int h8BlockIndex = H8Memory.RegisterBlockDescriptor(BuildDescriptor(in freeBlock));
                    if (h8BlockIndex < 0)
                    {
                        DumpPhiVodBlackBox();
                        AbortInitialize();
                        return;
                    }

                    freeBlock.H8BlockIndex = h8BlockIndex;
                    if (!TryAppendBlockNoResize(in freeBlock))
                    {
                        ReleaseCommittedH8Descriptor(h8BlockIndex);
                        DumpPhiVodBlackBox();
                        AbortInitialize();
                        return;
                    }
                }

                _initialized = true;
                _latestCreated = this;
            }
            catch
            {
                AbortInitialize();
                throw;
            }
        }

        private bool HasInitializedCriticalNativeStorage()
        {
            return
                _buffers.IsCreated &&
                _metadata.IsCreated &&
                _metadataGenerationByBufferId.IsCreated &&
                _metadataByBufferId.IsCreated &&
                _keys.IsCreated &&
                _blocks.IsCreated &&
                _defragBlackBox.IsCreated &&
                _defragBlackBoxDetails.IsCreated &&
                _lastRelocationRecords.IsCreated &&
                _memoryBudgetEntries.IsCreated &&
                _deferredReleaseRequests.IsCreated &&
                _writerThreadLockSlots.IsCreated &&
                _macroDatabasePayloadCache.IsCreated &&
                _macroDatabasePayloadAccessTicks.IsCreated &&
                _macroDatabasePayloadKeys.IsCreated;
        }

        private void AbortInitialize()
        {
            _initialized = true;
            Dispose();
        }

        private static bool ValidateAbiLayout()
        {
#pragma warning disable 0618
            bool valid =
                UnsafeUtility.SizeOf<VaultBufferHandle<byte>>() == VaultBufferHandleSizeBytes &&
                UnsafeUtility.SizeOf<VaultGenerationHandle<byte>>() == VaultGenerationHandleSizeBytes &&
                UnsafeUtility.SizeOf<VaultSliceHandle<byte>>() == VaultSliceHandleSizeBytes &&
                UnsafeUtility.SizeOf<VaultRelocationRecord>() == VaultRelocationRecordSizeBytes &&
                UnsafeUtility.SizeOf<VaultBufferMeta>() == VaultBufferMetaSizeBytes &&
                UnsafeUtility.SizeOf<VaultMemoryBlockSnapshot>() == VaultMemoryBlockSnapshotSizeBytes &&
                UnsafeUtility.SizeOf<VaultArenaBlock>() == VaultArenaBlockSizeBytes &&
                UnsafeUtility.SizeOf<MemoryDefragTelemetryEntry>() == MemoryDefragTelemetryEntrySizeBytes &&
                UnsafeUtility.SizeOf<MemoryDefragTelemetryDetailEntry>() == MemoryDefragTelemetryDetailEntrySizeBytes &&
                UnsafeUtility.SizeOf<VaultTelemetrySnapshot>() == VaultTelemetrySnapshotSizeBytes &&
                UnsafeUtility.SizeOf<VaultMemoryBudgetEntry>() == VaultMemoryBudgetEntrySizeBytes &&
                UnsafeUtility.SizeOf<DeferredVaultReleaseRequest>() == DeferredVaultReleaseRequestSizeBytes &&
                UnsafeUtility.SizeOf<VaultThreadWriteLockSlot>() == VaultThreadWriteLockSlotSizeBytes &&
                UnsafeUtility.SizeOf<MacroDatabasePayloadHandle>() == MacroDatabasePayloadHandleSizeBytes &&
                UnsafeUtility.SizeOf<MacroDatabasePayloadCacheEntry>() == MacroDatabasePayloadCacheEntrySizeBytes &&
                ValidateDescriptorAbiOffsets() &&
                ValidatePublicDtoAbiOffsets() &&
                ValidateInternalDtoAbiOffsets() &&
                ValidateMacroPayloadAbiOffsets() &&
                CoreMemoryContractAbiGuard.Validate();

            return valid;
#pragma warning restore 0618
        }

#pragma warning disable 0618
        private static bool ValidateDescriptorAbiOffsets()
        {
            VaultGenerationHandle<byte> generation = default;
            VaultBufferHandle<byte> buffer = default;
            VaultSliceHandle<byte> slice = default;
            byte* generationBase = (byte*)&generation;
            byte* bufferBase = (byte*)&buffer;
            byte* sliceBase = (byte*)&slice;

            return
                ByteOffset(generationBase, &generation.BufferID) == 0 &&
                ByteOffset(generationBase, &generation.SystemID) == 4 &&
                ByteOffset(generationBase, &generation.Generation) == 8 &&
                ByteOffset(generationBase, &generation.Flags) == 12 &&
                ByteOffset(bufferBase, &buffer.BufferID) == 0 &&
                ByteOffset(bufferBase, &buffer.SystemID) == 4 &&
                ByteOffset(bufferBase, &buffer.Generation) == 8 &&
                ByteOffset(bufferBase, &buffer.Flags) == 12 &&
                ByteOffset(sliceBase, &slice.BufferID) == 0 &&
                ByteOffset(sliceBase, &slice.SystemID) == 4 &&
                ByteOffset(sliceBase, &slice.Generation) == 8 &&
                ByteOffset(sliceBase, &slice.HandleFlags) == 12 &&
                ByteOffset(sliceBase, &slice.StartIndex) == 16 &&
                ByteOffset(sliceBase, &slice.Length) == 20 &&
                ByteOffset(sliceBase, &slice.Flags) == 24 &&
                ByteOffset(sliceBase, &slice.Reserved0) == 28;
        }
#pragma warning restore 0618

        private static bool ValidatePublicDtoAbiOffsets()
        {
            VaultRelocationRecord relocation = default;
            VaultTelemetrySnapshot telemetry = default;
            VaultMemoryBudgetEntry budget = default;
            VaultMemoryBlockSnapshot block = default;
            byte* relocationBase = (byte*)&relocation;
            byte* telemetryBase = (byte*)&telemetry;
            byte* budgetBase = (byte*)&budget;
            byte* blockBase = (byte*)&block;

            return
                ByteOffset(relocationBase, &relocation.OldOffsetBytes) == 0 &&
                ByteOffset(relocationBase, &relocation.NewOffsetBytes) == 8 &&
                ByteOffset(relocationBase, &relocation.BufferId) == 16 &&
                ByteOffset(relocationBase, &relocation.ByteLength) == 20 &&
                ByteOffset(relocationBase, &relocation.Generation) == 24 &&
                ByteOffset(relocationBase, &relocation.Flags) == 28 &&
                ByteOffset(relocationBase, &relocation.SystemId) == 29 &&
                ByteOffset(relocationBase, &relocation.Reserved) == 30 &&
                ByteOffset(telemetryBase, &telemetry.AllocatedBytes) == 0 &&
                ByteOffset(telemetryBase, &telemetry.ArenaBytes) == 8 &&
                ByteOffset(telemetryBase, &telemetry.LastMovedBytes) == 16 &&
                ByteOffset(telemetryBase, &telemetry.ResolutionTicks) == 24 &&
                ByteOffset(telemetryBase, &telemetry.VaultGenerationID) == 32 &&
                ByteOffset(telemetryBase, &telemetry.GenerationMismatchCount) == 36 &&
                ByteOffset(telemetryBase, &telemetry.LastFaultBufferID) == 40 &&
                ByteOffset(telemetryBase, &telemetry.LastFaultHandleGeneration) == 44 &&
                ByteOffset(telemetryBase, &telemetry.LastFaultMetaGeneration) == 48 &&
                ByteOffset(telemetryBase, &telemetry.LastDefragFlags) == 52 &&
                ByteOffset(telemetryBase, &telemetry.Reserved0) == 53 &&
                ByteOffset(telemetryBase, &telemetry.Reserved1) == 54 &&
                ByteOffset(telemetryBase, &telemetry.ResolvedHandleCount) == 56 &&
                ByteOffset(budgetBase, &budget.SystemHash) == 0 &&
                ByteOffset(budgetBase, &budget.BufferID) == 4 &&
                ByteOffset(budgetBase, &budget.BudgetBytes) == 8 &&
                ByteOffset(budgetBase, &budget.DefragThresholdBytes) == 16 &&
                ByteOffset(budgetBase, &budget.Flags) == 24 &&
                ByteOffset(budgetBase, &budget.Reserved0) == 28 &&
                ByteOffset(blockBase, &block.OffsetBytes) == 0 &&
                ByteOffset(blockBase, &block.Bytes) == 8 &&
                ByteOffset(blockBase, &block.BufferKey) == 16 &&
                ByteOffset(blockBase, &block.H8BlockIndex) == 20 &&
                ByteOffset(blockBase, &block.Version) == 24 &&
                ByteOffset(blockBase, &block.Owner) == 28 &&
                ByteOffset(blockBase, &block.LockCount) == 30 &&
                ByteOffset(blockBase, &block.State) == 32 &&
                ByteOffset(blockBase, &block.Flags) == 33 &&
                ByteOffset(blockBase, &block.Reserved0) == 34 &&
                ByteOffset(blockBase, &block.Reserved1) == 36 &&
                ByteOffset(blockBase, &block.Reserved2) == 40;
        }

        private static bool ValidateInternalDtoAbiOffsets()
        {
            VaultBufferMeta meta = default;
            VaultArenaBlock arena = default;
            MemoryDefragTelemetryEntry defrag = default;
            MemoryDefragTelemetryDetailEntry defragDetail = default;
            byte* metaBase = (byte*)&meta;
            byte* arenaBase = (byte*)&arena;
            byte* defragBase = (byte*)&defrag;
            byte* defragDetailBase = (byte*)&defragDetail;

            return
                ByteOffset(metaBase, &meta.OffsetBytes) == 0 &&
                ByteOffset(metaBase, &meta.Bytes) == 8 &&
                ByteOffset(metaBase, &meta.Length) == 16 &&
                ByteOffset(metaBase, &meta.Stride) == 20 &&
                ByteOffset(metaBase, &meta.Alignment) == 24 &&
                ByteOffset(metaBase, &meta.BlockIndex) == 28 &&
                ByteOffset(metaBase, &meta.Allocator) == 32 &&
                ByteOffset(metaBase, &meta.Version) == 36 &&
                ByteOffset(metaBase, &meta.Owner) == 40 &&
                ByteOffset(metaBase, &meta.LastAliasRequester) == 42 &&
                ByteOffset(metaBase, &meta.ActiveWriterSystemID) == 44 &&
                ByteOffset(metaBase, &meta.TypeHash) == 48 &&
                ByteOffset(metaBase, &meta.RefCount) == 52 &&
                ByteOffset(metaBase, &meta.Flags) == 56 &&
                ByteOffset(metaBase, &meta.BufferKey) == 60 &&
                ByteOffset(arenaBase, &arena.OffsetBytes) == 0 &&
                ByteOffset(arenaBase, &arena.Bytes) == 8 &&
                ByteOffset(arenaBase, &arena.BufferKey) == 16 &&
                ByteOffset(arenaBase, &arena.H8BlockIndex) == 20 &&
                ByteOffset(arenaBase, &arena.Version) == 24 &&
                ByteOffset(arenaBase, &arena.State) == 28 &&
                ByteOffset(arenaBase, &arena.Reserved0) == 29 &&
                ByteOffset(arenaBase, &arena.Reserved1) == 30 &&
                ByteOffset(defragBase, &defrag.TotalFreeSpaceBytes) == 0 &&
                ByteOffset(defragBase, &defrag.LargestContiguousBlockBytes) == 8 &&
                ByteOffset(defragBase, &defrag.LastMovedBytes) == 16 &&
                ByteOffset(defragBase, &defrag.TotalMovedBytes) == 24 &&
                ByteOffset(defragBase, &defrag.PendingMassiveMoveBytes) == 32 &&
                ByteOffset(defragBase, &defrag.ActiveMutationGuardMask) == 40 &&
                ByteOffset(defragBase, &defrag.Sequence) == 48 &&
                ByteOffset(defragBase, &defrag.Frame) == 52 &&
                ByteOffset(defragBase, &defrag.VaultGenerationID) == 56 &&
                ByteOffset(defragBase, &defrag.ActiveBurstLockMask) == 60 &&
                ByteOffset(defragDetailBase, &defragDetail.BlockCount) == 0 &&
                ByteOffset(defragDetailBase, &defragDetail.ActiveBufferCount) == 4 &&
                ByteOffset(defragDetailBase, &defragDetail.WatchdogBreaches) == 8 &&
                ByteOffset(defragDetailBase, &defragDetail.LockedSkipCount) == 12 &&
                ByteOffset(defragDetailBase, &defragDetail.HeapFragmentationRatio) == 16 &&
                ByteOffset(defragDetailBase, &defragDetail.GenerationMismatchCount) == 20 &&
                ByteOffset(defragDetailBase, &defragDetail.ResolutionTicks) == 24 &&
                ByteOffset(defragDetailBase, &defragDetail.ResolvedHandleCount) == 32 &&
                ByteOffset(defragDetailBase, &defragDetail.LastFaultBufferID) == 40 &&
                ByteOffset(defragDetailBase, &defragDetail.LastFaultHandleGeneration) == 44 &&
                ByteOffset(defragDetailBase, &defragDetail.LastFaultMetaGeneration) == 48 &&
                ByteOffset(defragDetailBase, &defragDetail.Reserved32) == 52 &&
                ByteOffset(defragDetailBase, &defragDetail.LastRelocatedSystemId) == 56 &&
                ByteOffset(defragDetailBase, &defragDetail.Reserved16) == 58 &&
                ByteOffset(defragDetailBase, &defragDetail.Flags) == 60 &&
                ByteOffset(defragDetailBase, &defragDetail.IsFragmented) == 61 &&
                ByteOffset(defragDetailBase, &defragDetail.WatchdogExceeded) == 62 &&
                ByteOffset(defragDetailBase, &defragDetail.MemoryStarvationWarnings) == 63;
        }

        private static bool ValidateMacroPayloadAbiOffsets()
        {
            MacroDatabasePayloadHandle handle = default;
            MacroDatabasePayloadCacheEntry cache = default;
            byte* handleBase = (byte*)&handle;
            byte* cacheBase = (byte*)&cache;

            return
                ByteOffset(handleBase, &handle.SectorHash) == 0 &&
                ByteOffset(handleBase, &handle.PayloadToken) == 8 &&
                ByteOffset(handleBase, &handle.FileOffset) == 16 &&
                ByteOffset(handleBase, &handle.ByteLength) == 24 &&
                ByteOffset(handleBase, &handle.Version) == 28 &&
                ByteOffset(handleBase, &handle.Flags) == 32 &&
                ByteOffset(handleBase, &handle.Reserved0) == 33 &&
                ByteOffset(handleBase, &handle.Reserved1) == 34 &&
                ByteOffset(cacheBase, &cache.Handle) == 0 &&
                ByteOffset(cacheBase, &cache.Pointer) == 40;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ByteOffset(void* basePtr, void* fieldPtr)
        {
            return (int)((byte*)fieldPtr - (byte*)basePtr);
        }

        /// <inheritdoc />
        public VaultGenerationHandle<T> EnsureGenerationHandle<T>(
            BufferID bufferId,
            int requiredLength,
            SystemID requester,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            if (!TryEnsureVaultBuffer<T>(
                    bufferId,
                    requiredLength,
                    requester,
                    options,
                    exposeExternalView: false,
                    out _,
                    out _) ||
                !TryBuildGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return default;
            }

            return handle;
        }

        private bool TryEnsureVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            SystemID requester,
            NativeArrayOptions options,
            bool exposeExternalView,
            out IntPtr resolvedPointer,
            out int resolvedLength,
            bool sanitizeFinite = true) where T : struct
        {
            resolvedPointer = default;
            resolvedLength = 0;
            if (requiredLength <= 0)
                return false;
            if (requester == SystemID.Unknown)
                return false;

            EnsureInitialized();
            if (!_initialized)
                return false;

            if (_arenaBase == null)
            {
                DumpPhiVodBlackBox();
                return false;
            }

            int key = (int)bufferId;
            if (key == 0)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            if (stride <= 0 || requiredLength > long.MaxValue / stride)
                return false;

            long requestedBytes = (long)requiredLength * stride;
            long requiredBytes = AlignUp(requestedBytes, VaultBlockAlignment);
            if (requiredBytes <= 0L)
                return false;
            if (Volatile.Read(ref _compactionFence) != 0)
            {
                QueueDeferredArenaGrowth(requiredBytes);
                return false;
            }

            if (requiredBytes > _arenaBytes && !TryGrowArenaForBytes(requiredBytes))
                return false;

            bool hasExistingPointer = _buffers.TryGetValue(key, out IntPtr existingPointer);
            bool hasExistingMeta = _metadata.TryGetValue(key, out VaultBufferMeta existingMeta);
            if (hasExistingPointer != hasExistingMeta)
            {
                DumpPhiVodBlackBox();
                return false;
            }

            if (hasExistingPointer)
            {
                if (existingPointer == IntPtr.Zero)
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (!ValidateType<T>(bufferId, existingMeta, stride, alignment))
                {
                    DumpPhiVodBlackBox();
                    return false;
                }
                if (existingMeta.BufferKey != key ||
                    existingMeta.TypeHash == 0u ||
                    existingMeta.RefCount == 0u)
                {
                    existingMeta.BufferKey = key;
                    existingMeta.TypeHash = ComputeTypeHash<T>();
                    if (existingMeta.RefCount == 0u)
                        existingMeta.RefCount = 1u;
                    WriteMetadata(key, in existingMeta);
                }

                if (!EnsureBufferKeyRegistered(key))
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (existingMeta.Length >= requiredLength)
                {
                    if (!IsPointerAligned(existingPointer, VaultBlockAlignment))
                    {
                        LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                        DumpPhiVodBlackBox();
                        return false;
                    }

                    if (exposeExternalView && !MarkExternalView(key, existingMeta.OffsetBytes))
                    {
                        RecordLockContentionFault(key);
                        return false;
                    }

                    if (sanitizeFinite)
                        SanitizeFinitePayload<T>(existingPointer, existingMeta.Length);
                    resolvedPointer = existingPointer;
                    resolvedLength = existingMeta.Length;
                    return true;
                }

                if (Interlocked.Read(ref _allocationLock) != 0L)
                    return false;

                if (!TryReallocateBlock(key, existingMeta, requiredLength, requiredBytes, ShouldClear(options), out IntPtr resizedPointer, out VaultBufferMeta resizedMeta))
                {
                    if (!TryGrowArenaForBytes(requiredBytes) ||
                        !TryReallocateBlock(key, existingMeta, requiredLength, requiredBytes, ShouldClear(options), out resizedPointer, out resizedMeta))
                    {
                        return false;
                    }
                }

                _buffers[key] = resizedPointer;
                WriteMetadata(key, in resizedMeta);
                BumpVaultGeneration();
                if (!IsPointerAligned(resizedPointer, VaultBlockAlignment))
                {
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (exposeExternalView && !MarkExternalView(key, resizedMeta.OffsetBytes))
                {
                    RecordLockContentionFault(key);
                    return false;
                }

                if (sanitizeFinite)
                    SanitizeFinitePayload<T>(resizedPointer, requiredLength);
                resolvedPointer = resizedPointer;
                resolvedLength = requiredLength;
                return true;
            }

            if (Interlocked.Read(ref _allocationLock) != 0L)
                return false;

            if (_keys.Length >= _keys.Capacity)
                return false;

            if (!TryAllocatePublishedBuffer<T>(
                    key,
                    requiredLength,
                    requester,
                    options,
                    exposeExternalView,
                    sanitizeFinite,
                    requiredBytes,
                    stride,
                    alignment,
                    out IntPtr pointer))
            {
                if (!TryGrowArenaForBytes(requiredBytes) ||
                    !TryAllocatePublishedBuffer<T>(
                        key,
                        requiredLength,
                        requester,
                        options,
                        exposeExternalView,
                        sanitizeFinite,
                        requiredBytes,
                        stride,
                        alignment,
                        out pointer))
                {
                    return false;
                }
            }

            resolvedPointer = pointer;
            resolvedLength = requiredLength;
            return true;
        }

        private bool TryAllocatePublishedBuffer<T>(
            int key,
            int requiredLength,
            SystemID requester,
            NativeArrayOptions options,
            bool exposeExternalView,
            bool sanitizeFinite,
            long requiredBytes,
            int stride,
            int alignment,
            out IntPtr pointer) where T : struct
        {
            pointer = default;
            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(key);
                return false;
            }

            int blockIndex = -1;
            bool blockAllocated = false;
            bool bufferAdded = false;
            bool metadataAdded = false;
            bool countedBytes = false;
            bool success = false;
            try
            {
                if (_keys.Length >= _keys.Capacity)
                    return false;
                if (!TryAllocateBlockLocked(key, requiredBytes, out blockIndex, out pointer))
                    return false;
                blockAllocated = true;

                if (!IsPointerAligned(pointer, VaultBlockAlignment))
                {
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (ShouldClear(options))
                    UnsafeUtility.MemClear(pointer.ToPointer(), requiredBytes);
                if (sanitizeFinite)
                    SanitizeFinitePayload<T>(pointer, requiredLength);

                VaultBufferMeta meta = default;
                meta.Length = requiredLength;
                meta.Stride = stride;
                meta.Alignment = alignment;
                meta.BlockIndex = blockIndex;
                meta.OffsetBytes = _blocks[blockIndex].OffsetBytes;
                meta.Bytes = requiredBytes;
                meta.Owner = requester;
                meta.Allocator = Allocator.Persistent;
                meta.Version = ResolveInitialGenerationForAllocation(key);
                meta.ActiveWriterSystemID = 0;
                meta.TypeHash = ComputeTypeHash<T>();
                meta.RefCount = 1u;
                meta.Flags = 0u;
                meta.BufferKey = key;

                bufferAdded = _buffers.TryAdd(key, pointer);
                metadataAdded = bufferAdded && TryAddMetadata(key, in meta);
                if (!bufferAdded || !metadataAdded)
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (!EnsureBufferKeyRegistered(key))
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                _allocatedBytes += requiredBytes;
                countedBytes = true;
                if (exposeExternalView && !MarkExternalViewLocked(key, meta.OffsetBytes))
                {
                    RecordLockContentionFault(key);
                    DumpPhiVodBlackBox();
                    return false;
                }

                success = true;
                return true;
            }
            finally
            {
                if (!success && blockAllocated)
                {
                    if (countedBytes)
                        _allocatedBytes = _allocatedBytes > requiredBytes ? _allocatedBytes - requiredBytes : 0L;
                    if (bufferAdded)
                        _buffers.Remove(key);
                    if (metadataAdded)
                        RemoveMetadata(key);
                    RemoveBufferKey(key);
                    if (!FreeBlockLocked(blockIndex, clearPayload: true))
                        DumpPhiVodBlackBox();
                    pointer = default;
                }

                ReleaseBlockMutationGate();
            }
        }

        private bool TryOpenAliasBuffer<T>(BufferID bufferId, SystemID requester, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!_initialized || requester == SystemID.Unknown)
                return false;
            if (Volatile.Read(ref _compactionFence) != 0)
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
            if (!ValidateType<T>(bufferId, meta, stride, alignment))
            {
                DumpPhiVodBlackBox();
                return false;
            }
            if (!IsPointerAligned(pointer, VaultBlockAlignment))
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                DumpPhiVodBlackBox();
                return false;
            }

            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(key);
                return false;
            }

            bool opened = false;
            bool hadExternalView = false;
            long lockedOffsetBytes = meta.OffsetBytes;
            SystemID previousAliasRequester = meta.LastAliasRequester;
            try
            {
                if (!_buffers.TryGetValue(key, out pointer) ||
                    !TryReadFlatMetadata(key, out meta) ||
                    pointer == IntPtr.Zero ||
                    meta.Length <= 0 ||
                    meta.OffsetBytes < 0L ||
                    meta.Bytes <= 0L ||
                    meta.OffsetBytes > _arenaBytes - meta.Bytes)
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                stride = UnsafeUtility.SizeOf<T>();
                alignment = UnsafeUtility.AlignOf<T>();
                if (!ValidateType<T>(bufferId, meta, stride, alignment) ||
                    !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                VaultArenaBlock block = _blocks[blockIndex];
                if (block.BufferKey != key ||
                    block.OffsetBytes != meta.OffsetBytes ||
                    (block.Reserved0 & BlockFlagLocked) != 0 ||
                    block.Reserved1 != 0)
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                hadExternalView = (block.Reserved0 & BlockFlagExternalView) != 0;
                if (hadExternalView || meta.LastAliasRequester != SystemID.Unknown)
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                lockedOffsetBytes = meta.OffsetBytes;
                previousAliasRequester = meta.LastAliasRequester;
                if (!MarkExternalViewLocked(key, meta.OffsetBytes) ||
                    !MarkAliasReaderLocked(key, requester))
                {
                    RollbackAliasPublicationLocked(key, lockedOffsetBytes, hadExternalView, previousAliasRequester);
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (!_buffers.TryGetValue(key, out pointer) ||
                    !TryReadFlatMetadata(key, out meta) ||
                    pointer == IntPtr.Zero ||
                    meta.Length <= 0 ||
                    meta.OffsetBytes != lockedOffsetBytes ||
                    meta.Bytes <= 0L ||
                    meta.OffsetBytes > _arenaBytes - meta.Bytes)
                {
                    RollbackAliasPublicationLocked(key, lockedOffsetBytes, hadExternalView, previousAliasRequester);
                    DumpPhiVodBlackBox();
                    return false;
                }

                buffer = H8Memory.CreateNativeArrayView<T>(pointer.ToPointer(), meta.Length);
                if (!buffer.IsCreated)
                {
                    RollbackAliasPublicationLocked(key, lockedOffsetBytes, hadExternalView, previousAliasRequester);
                    DumpPhiVodBlackBox();
                    return false;
                }

                opened = true;
            }
            finally
            {
                ReleaseBlockMutationGate();
            }

            if (!opened)
                return false;

            return true;
        }

        /// <inheritdoc />
        public bool TryGetGenerationHandle<T>(BufferID bufferId, out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (!_initialized || Volatile.Read(ref _compactionFence) != 0)
                return false;

            return TryBuildGenerationHandle(bufferId, out handle);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolveHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!_initialized || Volatile.Read(ref _compactionFence) != 0 || _arenaBase == null)
                return false;

            int key = unchecked((int)handle.BufferID);
            if (key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
                return false;

            if (handle.SystemID != 0u && handle.SystemID != (uint)meta.Owner)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            uint typeHash = ComputeTypeHash<T>();
            if (meta.Stride != stride ||
                meta.Alignment != alignment ||
                (meta.TypeHash != 0u && meta.TypeHash != typeHash) ||
                meta.OffsetBytes < 0L ||
                meta.Bytes <= 0L ||
                meta.OffsetBytes > _arenaBytes - meta.Bytes)
            {
                return false;
            }

            NativeArray<T> resolved = H8Memory.CreateNativeArrayView<T>((byte*)_arenaBase + meta.OffsetBytes, meta.Length);
            Thread.MemoryBarrier();
            if (Volatile.Read(ref _compactionFence) != 0)
                return false;

            buffer = resolved;
            return buffer.IsCreated;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!_initialized || Volatile.Read(ref _compactionFence) != 0 || _arenaBase == null)
                return false;

            int key = unchecked((int)handle.BufferID);
            if (key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
                return false;

            if (handle.SystemID != 0u && handle.SystemID != (uint)meta.Owner)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            uint typeHash = ComputeTypeHash<T>();
            if (meta.Stride != stride ||
                meta.Alignment != alignment ||
                (meta.TypeHash != 0u && meta.TypeHash != typeHash) ||
                meta.OffsetBytes < 0L ||
                meta.Bytes <= 0L ||
                meta.OffsetBytes > _arenaBytes - meta.Bytes)
            {
                return false;
            }

            NativeArray<T> resolved = H8Memory.CreateNativeArrayView<T>((byte*)_arenaBase + meta.OffsetBytes, meta.Length);
            Thread.MemoryBarrier();
            if (Volatile.Read(ref _compactionFence) != 0)
                return false;

            buffer = resolved;
            return buffer.IsCreated;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadOnlyHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (!_initialized || Volatile.Read(ref _compactionFence) != 0 || _arenaBase == null)
                return false;

            int key = unchecked((int)handle.BufferID);
            if (key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
                return false;

            if (handle.SystemID != 0u && handle.SystemID != (uint)meta.Owner)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            uint typeHash = ComputeTypeHash<T>();
            if (meta.Stride != stride ||
                meta.Alignment != alignment ||
                (meta.TypeHash != 0u && meta.TypeHash != typeHash) ||
                meta.OffsetBytes < 0L ||
                meta.Bytes <= 0L ||
                meta.OffsetBytes > _arenaBytes - meta.Bytes)
            {
                return false;
            }

            NativeArray<T>.ReadOnly resolved = H8Memory.CreateReadOnlyNativeArrayView<T>((byte*)_arenaBase + meta.OffsetBytes, meta.Length);
            Thread.MemoryBarrier();
            if (Volatile.Read(ref _compactionFence) != 0)
                return false;

            buffer = resolved;
            return buffer.IsCreated;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolveSlice<T>(in VaultSliceHandle<T> handle, out NativeArray<T> slice) where T : struct
        {
            slice = default;
            if (handle.Length <= 0 || handle.StartIndex < 0 || handle.StartIndex > int.MaxValue - handle.Length)
                return false;

            VaultGenerationHandle<T> baseHandle = default;
            baseHandle.BufferID = handle.BufferID;
            baseHandle.SystemID = handle.SystemID;
            baseHandle.Generation = handle.Generation;
            baseHandle.Flags = handle.HandleFlags;
            if (!TryResolveHandle(in baseHandle, out NativeArray<T> full))
                return false;

            int end = handle.StartIndex + handle.Length;
            if (end > full.Length)
                return false;

            if (Volatile.Read(ref _compactionFence) != 0)
                return false;

            slice = full.GetSubArray(handle.StartIndex, handle.Length);
            Thread.MemoryBarrier();
            if (Volatile.Read(ref _compactionFence) != 0)
            {
                slice = default;
                return false;
            }

            return slice.IsCreated;
        }

        /// <inheritdoc />
        public bool TryAcquireSliceHandle<T>(
            BufferID bufferId,
            int requiredLength,
            int startIndex,
            int count,
            SystemID requester,
            out VaultSliceHandle<T> slice,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct
        {
            slice = default;
            if (count <= 0 || startIndex < 0 || requiredLength <= 0 || startIndex > int.MaxValue - count)
                return false;

            int endIndex = startIndex + count;
            int actualRequiredLength = requiredLength >= endIndex ? requiredLength : endIndex;
            if (!TryEnsureVaultBuffer<T>(
                    bufferId,
                    actualRequiredLength,
                    requester,
                    options,
                    exposeExternalView: false,
                    out _,
                    out _,
                    sanitizeFinite: false) ||
                !TryBuildGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            slice.BufferID = handle.BufferID;
            slice.SystemID = handle.SystemID;
            slice.Generation = handle.Generation;
            slice.HandleFlags = handle.Flags;
            slice.StartIndex = startIndex;
            slice.Length = count;
            slice.Flags = 0u;
            slice.Reserved0 = 0u;
            return true;
        }

        /// <inheritdoc />
        public bool TryAcquireWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (systemID == SystemID.Unknown || !_metadata.IsCreated)
                return false;

            int key = unchecked((int)handle.BufferID);
            if (key == 0)
                return false;

            if (Volatile.Read(ref _compactionFence) != 0)
            {
                RecordLockContentionFault(key);
                return false;
            }
            Thread.MemoryBarrier();

            int activeLockBit = ResolveActiveLockBit((BufferID)key);
            Thread.MemoryBarrier();
            if (Volatile.Read(ref _compactionFence) != 0)
            {
                RecordLockContentionFault(key);
                return false;
            }

            if (HasMutationGuardForActiveLockBit(activeLockBit))
            {
                RecordLockContentionFault(key);
                return false;
            }

            if (!TryReadFlatMetadata(key, out VaultBufferMeta meta))
            {
                return false;
            }

            if (handle.Generation != meta.Version)
            {
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }

            if ((uint)meta.Owner != (uint)SystemID.Unknown && systemID != meta.Owner)
            {
                return false;
            }

            if (meta.ActiveWriterSystemID != 0 ||
                meta.LastAliasRequester != SystemID.Unknown)
            {
                RecordLockContentionFault(key);
                return false;
            }

            if (handle.SystemID != 0u && handle.SystemID != (uint)meta.Owner)
            {
                return false;
            }

            if (Volatile.Read(ref _compactionFence) != 0)
            {
                RecordLockContentionFault(key);
                return false;
            }

            if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                return false;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if ((block.Reserved0 & (BlockFlagLocked | BlockFlagExternalView)) != 0 ||
                block.Reserved1 != 0 ||
                block.Reserved1 == ushort.MaxValue)
            {
                RecordLockContentionFault(key);
                return false;
            }

            NativeArray<T> lockedBuffer = default;
            int writerThreadId = Thread.CurrentThread.ManagedThreadId;
            long writerSlotOffsetBytes = 0L;
            bool releaseThreadWriterSlot = false;
            bool writerLockCommitted = false;
            try
            {
                Thread.MemoryBarrier();
                if (!TryEnterBlockMutationGate())
                {
                    RecordLockContentionFault(key);
                    return false;
                }

                try
                {
                    if (!TryReadFlatMetadata(key, out meta))
                    {
                        return false;
                    }

                    if (handle.Generation != meta.Version ||
                        ((uint)meta.Owner != (uint)SystemID.Unknown && systemID != meta.Owner) ||
                        (handle.SystemID != 0u && handle.SystemID != (uint)meta.Owner))
                    {
                        return false;
                    }

                    if (meta.ActiveWriterSystemID != 0 ||
                        meta.LastAliasRequester != SystemID.Unknown)
                    {
                        RecordLockContentionFault(key);
                        return false;
                    }

                    if (Volatile.Read(ref _compactionFence) != 0)
                    {
                        RecordLockContentionFault(key);
                        return false;
                    }

                    if (HasMutationGuardForActiveLockBit(activeLockBit))
                    {
                        RecordLockContentionFault(key);
                        return false;
                    }

                    if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out blockIndex))
                    {
                        return false;
                    }

                    block = _blocks[blockIndex];
                    if (block.BufferKey != key ||
                        block.OffsetBytes != meta.OffsetBytes)
                    {
                        return false;
                    }

                    if ((block.Reserved0 & (BlockFlagLocked | BlockFlagExternalView)) != 0 ||
                        block.Reserved1 != 0 ||
                        block.Reserved1 == ushort.MaxValue)
                    {
                        RecordLockContentionFault(key);
                        return false;
                    }

                    writerSlotOffsetBytes = meta.OffsetBytes;
                    if (!TryReserveThreadWriterSlot(writerThreadId, key, writerSlotOffsetBytes, (int)systemID))
                    {
                        return false;
                    }
                    releaseThreadWriterSlot = true;

                    SetActiveGuardLockBits(key);
                    SetActiveLockBit(activeLockBit);
                    Thread.MemoryBarrier();
                    meta.ActiveWriterSystemID = (int)systemID;
                    WriteMetadata(key, in meta);

                    block.Reserved0 |= BlockFlagLocked;
                    block.Reserved1++;
                    _blocks[blockIndex] = block;
                    writerLockCommitted = true;
                    Thread.MemoryBarrier();

                    VaultBufferMeta lockedMeta = meta;
                    if (_arenaBase == null ||
                        lockedMeta.OffsetBytes < 0L ||
                        lockedMeta.Bytes <= 0L ||
                        lockedMeta.OffsetBytes > _arenaBytes - lockedMeta.Bytes)
                    {
                        RollbackWriterLockUnlocked(key, lockedMeta.OffsetBytes, activeLockBit, (int)systemID);
                        writerLockCommitted = false;
                        releaseThreadWriterSlot = false;
                        return false;
                    }

                    lockedBuffer = H8Memory.CreateNativeArrayView<T>((byte*)_arenaBase + lockedMeta.OffsetBytes, lockedMeta.Length);
                    if (!lockedBuffer.IsCreated)
                    {
                        RollbackWriterLockUnlocked(key, lockedMeta.OffsetBytes, activeLockBit, (int)systemID);
                        writerLockCommitted = false;
                        releaseThreadWriterSlot = false;
                        return false;
                    }

                    if (Volatile.Read(ref _compactionFence) != 0)
                    {
                        RecordLockContentionFault(key);
                        RollbackWriterLockUnlocked(key, lockedMeta.OffsetBytes, activeLockBit, (int)systemID);
                        writerLockCommitted = false;
                        releaseThreadWriterSlot = false;
                        lockedBuffer = default;
                        return false;
                    }
                }
                catch
                {
                    if (writerLockCommitted)
                    {
                        RollbackWriterLockUnlocked(key, writerSlotOffsetBytes, activeLockBit, (int)systemID);
                        releaseThreadWriterSlot = false;
                    }

                    throw;
                }
                finally
                {
                    ReleaseBlockMutationGate();
                }

                Thread.MemoryBarrier();
                buffer = lockedBuffer;
                if (!buffer.IsCreated)
                    return false;

                releaseThreadWriterSlot = false;
                return true;
            }
            finally
            {
                if (releaseThreadWriterSlot)
                    ReleaseThreadWriterSlotForLock(key, writerSlotOffsetBytes, (int)systemID);
            }
        }

        /// <inheritdoc />
        public bool ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID) where T : struct
        {
            if (systemID == SystemID.Unknown || !_metadata.IsCreated)
                return false;

            int key = unchecked((int)handle.BufferID);
            if (key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
            {
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }

            if ((uint)meta.Owner != (uint)SystemID.Unknown && systemID != meta.Owner)
                return false;

            if (handle.SystemID != 0u && handle.SystemID != (uint)meta.Owner)
                return false;

            int activeLockBit = ResolveActiveLockBit((BufferID)key);
            if (meta.ActiveWriterSystemID != (int)systemID)
                return false;

            if (!TryEnterReleaseMutationGate())
            {
                RecordLockContentionFault(key);
                return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID);
            }

            try
            {
                if (!TryReadFlatMetadata(key, out meta) ||
                    meta.ActiveWriterSystemID != (int)systemID ||
                    meta.OffsetBytes < 0L)
                {
                    return false;
                }

                if (!ReleaseWriterBlockLockUnlocked(key, meta.OffsetBytes))
                    return false;

                meta.ActiveWriterSystemID = 0;
                WriteMetadata(key, in meta);
                Thread.MemoryBarrier();
                ClearActiveLockBitIfUnusedLocked(activeLockBit);
                bool threadSlotReleased = ReleaseThreadWriterSlotForLock(key, meta.OffsetBytes, (int)systemID) ||
                                          ReleaseThreadWriterSlotForLock(key, meta.OffsetBytes, 0);
                if (!threadSlotReleased)
                {
                    RecordLockContentionFault(key);
                    return false;
                }

                return true;
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool ReleaseWriterBlockLock(int bufferKey, long offsetBytes)
        {
            if (!TryEnterReleaseMutationGate())
            {
                RecordLockContentionFault(bufferKey);
                return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0);
            }

            try
            {
                bool released = ReleaseWriterBlockLockUnlocked(bufferKey, offsetBytes);
                if (released)
                    ReleaseThreadWriterSlotForLock(bufferKey, offsetBytes, 0);
                return released;
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool RollbackWriterLockUnlocked(int bufferKey, long offsetBytes, int activeLockBit, int systemID)
        {
            bool released = ReleaseWriterBlockLockUnlocked(bufferKey, offsetBytes);
            if (TryReadFlatMetadata(bufferKey, out VaultBufferMeta meta) &&
                meta.OffsetBytes == offsetBytes &&
                meta.ActiveWriterSystemID == systemID)
            {
                meta.ActiveWriterSystemID = 0;
                WriteMetadata(bufferKey, in meta);
            }

            ClearActiveLockBitIfUnusedLocked(activeLockBit);
            ReleaseThreadWriterSlotForLock(bufferKey, offsetBytes, systemID);
            return released;
        }

        private bool ReleaseWriterBlockLockUnlocked(int bufferKey, long offsetBytes)
        {
            if (!TryFindOccupiedBlockIndex(bufferKey, offsetBytes, out int blockIndex))
                return false;

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.BufferKey != bufferKey || block.OffsetBytes != offsetBytes || block.Reserved1 == 0)
                return false;

            block.Reserved1--;
            if (block.Reserved1 == 0)
                block.Reserved0 &= unchecked((byte)~BlockFlagLocked);

            _blocks[blockIndex] = block;
            return true;
        }

        private bool QueueDeferredWriterRelease(int bufferKey, long offsetBytes, int activeLockBit, int lockOwnerSystemId)
        {
            return QueueDeferredRelease(
                bufferKey,
                offsetBytes,
                activeLockBit,
                lockOwnerSystemId,
                DeferredReleaseKindWriter);
        }

        private bool QueueDeferredBufferPinRelease(int bufferKey, long offsetBytes, int activeLockBit, int lockOwnerSystemId)
        {
            return QueueDeferredRelease(
                bufferKey,
                offsetBytes,
                activeLockBit,
                lockOwnerSystemId,
                DeferredReleaseKindBufferPin);
        }

        private bool QueueDeferredRelease(
            int bufferKey,
            long offsetBytes,
            int activeLockBit,
            int lockOwnerSystemId,
            byte kind)
        {
            if (!_deferredReleaseRequests.IsCreated ||
                bufferKey <= 0 ||
                offsetBytes < 0L ||
                (kind != DeferredReleaseKindWriter && kind != DeferredReleaseKindBufferPin))
                return false;
            if (!TryReadFlatMetadata(bufferKey, out _))
                return false;

            bool enqueueGateAcquired = false;
            if (kind == DeferredReleaseKindWriter)
            {
                enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0;
            }

            try
            {
                DeferredVaultReleaseRequest* requests =
                    (DeferredVaultReleaseRequest*)NativeArrayUnsafeUtility.GetUnsafePtr(_deferredReleaseRequests);

                if (kind == DeferredReleaseKindWriter)
                {
                    if (enqueueGateAcquired)
                    {
                        for (int i = 0; i < DeferredReleaseRequestCapacity; i++)
                        {
                            DeferredVaultReleaseRequest* pending = requests + i;
                            if (Volatile.Read(ref pending->State) != DeferredReleaseStatePending)
                                continue;
                            if (pending->BufferKey == bufferKey &&
                                pending->OffsetBytes == offsetBytes &&
                                pending->ActiveLockBit == activeLockBit &&
                                pending->LockOwnerSystemId == lockOwnerSystemId &&
                                pending->Kind == DeferredReleaseKindWriter)
                            {
                                return true;
                            }
                        }
                    }
                }

                int cursor = Interlocked.Increment(ref _deferredReleaseWriteCursor);
                for (int attempt = 0; attempt < DeferredReleaseRequestCapacity; attempt++)
                {
                    int index = (cursor + attempt) & DeferredReleaseRequestMask;
                    DeferredVaultReleaseRequest* request = requests + index;
                    if (Interlocked.CompareExchange(ref request->State, DeferredReleaseStateWriting, DeferredReleaseStateEmpty) != DeferredReleaseStateEmpty)
                        continue;

                    request->BufferKey = bufferKey;
                    request->OffsetBytes = offsetBytes;
                    request->ActiveLockBit = activeLockBit;
                    request->LockOwnerSystemId = lockOwnerSystemId;
                    request->Kind = kind;
                    request->Flags = 0;
                    request->Reserved16 = 0;
                    request->Sequence = unchecked((uint)cursor);
                    Thread.MemoryBarrier();
                    Volatile.Write(ref request->State, DeferredReleaseStatePending);
                    Interlocked.Increment(ref _deferredReleasePendingCount);
                    return true;
                }

                RecordLockContentionFault(bufferKey);
                return false;
            }
            finally
            {
                if (enqueueGateAcquired)
                    Volatile.Write(ref _deferredReleaseEnqueueGate, 0);
            }
        }

        private void DrainDeferredReleaseRequestsLocked()
        {
            if (!_deferredReleaseRequests.IsCreated || Volatile.Read(ref _deferredReleasePendingCount) <= 0)
                return;

            DeferredVaultReleaseRequest* requests =
                (DeferredVaultReleaseRequest*)NativeArrayUnsafeUtility.GetUnsafePtr(_deferredReleaseRequests);
            for (int i = 0; i < _deferredReleaseRequests.Length; i++)
            {
                DeferredVaultReleaseRequest* request = requests + i;
                if (Volatile.Read(ref request->State) != DeferredReleaseStatePending)
                    continue;

                DeferredVaultReleaseRequest local = *request;
                bool drained = local.Kind == DeferredReleaseKindWriter
                    ? DrainDeferredWriterReleaseLocked(in local)
                    : local.Kind == DeferredReleaseKindBufferPin
                        ? DrainDeferredBufferPinReleaseLocked(in local)
                        : true;
                if (!drained)
                    continue;

                request->BufferKey = 0;
                request->OffsetBytes = 0L;
                request->ActiveLockBit = 0;
                request->LockOwnerSystemId = 0;
                request->Kind = 0;
                request->Flags = 0;
                request->Reserved16 = 0;
                request->Sequence = 0u;
                Thread.MemoryBarrier();
                Volatile.Write(ref request->State, DeferredReleaseStateEmpty);
                Interlocked.Decrement(ref _deferredReleasePendingCount);
            }
        }

        private bool TryDrainDeferredReleaseRequests()
        {
            if (!_deferredReleaseRequests.IsCreated || Volatile.Read(ref _deferredReleasePendingCount) <= 0)
                return true;
            if (!TryAcquireBlockMutationGate())
            {
                RecordLockContentionFault(0);
                return false;
            }

            try
            {
                DrainDeferredReleaseRequestsLocked();
                return Volatile.Read(ref _deferredReleasePendingCount) <= 0;
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool DrainDeferredWriterReleaseLocked(in DeferredVaultReleaseRequest request)
        {
            if (request.BufferKey <= 0)
                return true;

            bool hasMetadata = TryReadFlatMetadata(request.BufferKey, out VaultBufferMeta meta);
            if (!hasMetadata)
            {
                ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, request.LockOwnerSystemId);
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            if (meta.OffsetBytes != request.OffsetBytes)
            {
                if (request.LockOwnerSystemId != 0)
                    ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, request.LockOwnerSystemId);
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            int owner = request.LockOwnerSystemId;
            if (owner != 0 && meta.ActiveWriterSystemID != owner)
            {
                ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, owner);
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            bool released = ReleaseWriterBlockLockUnlocked(request.BufferKey, request.OffsetBytes);
            if (!released && !TryFindOccupiedBlockIndex(request.BufferKey, request.OffsetBytes, out _))
            {
                if (owner != 0)
                    ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, owner);
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            if (!released && meta.ActiveWriterSystemID == 0)
            {
                ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, owner);
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            if (!released)
                return false;

            if (owner != 0 && meta.ActiveWriterSystemID == owner)
                meta.ActiveWriterSystemID = 0;
            WriteMetadata(request.BufferKey, in meta);
            Thread.MemoryBarrier();
            ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
            ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, owner);
            return true;
        }

        private bool DrainDeferredBufferPinReleaseLocked(in DeferredVaultReleaseRequest request)
        {
            if (request.BufferKey <= 0)
                return true;

            if (!TryReadFlatMetadata(request.BufferKey, out VaultBufferMeta meta))
            {
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            if (meta.OffsetBytes != request.OffsetBytes)
            {
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            if (!TryFindOccupiedBlockIndex(request.BufferKey, request.OffsetBytes, out int blockIndex))
            {
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.BufferKey != request.BufferKey || block.OffsetBytes != request.OffsetBytes)
            {
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            SystemID lockOwner = (SystemID)request.LockOwnerSystemId;
            if (block.Reserved1 == 0)
            {
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            if (meta.LastAliasRequester != lockOwner)
            {
                ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
                return true;
            }

            block.Reserved1--;
            if (block.Reserved1 == 0)
            {
                block.Reserved0 &= unchecked((byte)~BlockFlagLocked);
                if ((block.Reserved0 & BlockFlagExternalView) == 0)
                    meta.LastAliasRequester = SystemID.Unknown;
                WriteMetadata(request.BufferKey, in meta);
            }

            _blocks[blockIndex] = block;
            Thread.MemoryBarrier();
            ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit);
            return true;
        }

        /// <inheritdoc />
        public bool ReleaseBuffer<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            int key = unchecked((int)handle.BufferID);
            if (!_initialized || !H8Memory.IsInitialized || key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
            {
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }

            if (meta.ActiveWriterSystemID != 0)
                return false;

            if (meta.RefCount > 1u)
            {
                meta.RefCount--;
                meta.Version = NextGeneration(meta.Version);
                WriteMetadata(key, in meta);
                BumpVaultGeneration();
                return true;
            }

            if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
                return false;

            if (!TryFreeBlock(blockIndex, clearPayload: true))
                return false;

            _allocatedBytes = _allocatedBytes > meta.Bytes ? _allocatedBytes - meta.Bytes : 0L;
            _buffers.Remove(key);
            RemoveMetadata(key);
            RemoveBufferKey(key);
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
        public NativeArray<T>.ReadOnly PinReadOnlyAlias<T>(BufferID bufferId, SystemID requester) where T : struct
        {
            if (requester == SystemID.Unknown || bufferId == BufferID.Unknown)
                return default;

            if (!TryOpenAliasBuffer<T>(bufferId, requester, out NativeArray<T> buffer))
                return default;

            return H8Memory.CreateAlias(buffer, requester);
        }

        /// <inheritdoc />
        [Obsolete("Use PinReadOnlyAlias<T>; CreateAlias pins relocation metadata and is not a pure read accessor.", false)]
        public NativeArray<T>.ReadOnly CreateAlias<T>(BufferID bufferId, SystemID requester) where T : struct
        {
            return PinReadOnlyAlias<T>(bufferId, requester);
        }

        /// <inheritdoc />
        public int ReleaseOwnerBuffers(SystemID owner, out long releasedBytes)
        {
            releasedBytes = 0L;
            if (owner == SystemID.Unknown || !_initialized || !_keys.IsCreated)
                return 0;

            return ReleaseBuffersByOwner(owner, sceneOwnedOnly: false, out releasedBytes);
        }

        /// <inheritdoc />
        public int ReleaseSceneOwnedBuffers(out long releasedBytes)
        {
            int remainingCount;
            long remainingBytes;
            int lockedCount;
            return ReleaseSceneOwnedBuffers(out releasedBytes, out remainingCount, out remainingBytes, out lockedCount);
        }

        /// <inheritdoc />
        public int ReleaseSceneOwnedBuffers(out long releasedBytes, out int remainingCount, out long remainingBytes, out int lockedCount)
        {
            releasedBytes = 0L;
            remainingCount = 0;
            remainingBytes = 0L;
            lockedCount = 0;
            if (!_initialized || !_keys.IsCreated)
                return 0;

            int releasedCount = ReleaseBuffersByOwner(SystemID.Unknown, sceneOwnedOnly: true, out releasedBytes);
            remainingCount = CountSceneOwnedBuffers(out remainingBytes, out lockedCount);

            return releasedCount;
        }

        /// <inheritdoc />
        public int SweepOrphanedHandles(
            NativeArray<SystemID> liveOwners,
            int liveOwnerCount,
            MemoryDefragPhase phase,
            uint activeBurstLockMask,
            out long releasedBytes)
        {
            releasedBytes = 0L;
            _lastOrphanSweepCandidateCount = 0;
            _lastOrphanReclaimCount = 0;
            TryDrainDeferredReleaseRequests();
            if (!_initialized ||
                phase != MemoryDefragPhase.PreSimulation ||
                HasActiveBurstLocks(activeBurstLockMask) ||
                Volatile.Read(ref _compactionFence) != 0 ||
                !_metadataByBufferId.IsCreated ||
                !_metadata.IsCreated ||
                !_buffers.IsCreated ||
                !_blocks.IsCreated ||
                !_keys.IsCreated ||
                !liveOwners.IsCreated ||
                liveOwnerCount < 0)
            {
                return 0;
            }

            if (Interlocked.Exchange(ref _compactionFence, 1) != 0)
                return 0;

            int reclaimedCount = 0;
            int candidateCount = 0;
            try
            {
                SweepOrphanedHandlesJob sweepJob = new SweepOrphanedHandlesJob
                {
                    Metadata = _metadataByBufferId,
                    LiveOwners = liveOwners,
                    LiveOwnerCount = liveOwnerCount
                };
                for (int i = 0; i < _metadataByBufferId.Length; i++)
                    sweepJob.Execute(i);

                for (int i = _keys.Length - 1; i >= 0; i--)
                {
                    int key = _keys[i];
                    if (!TryReadFlatMetadata(key, out VaultBufferMeta meta))
                    {
                        RemoveBufferKey(key);
                        DumpPhiVodBlackBox();
                        continue;
                    }

                    if (_metadata.TryGetValue(key, out VaultBufferMeta storedMeta) && storedMeta.Flags != meta.Flags)
                        WriteMetadata(key, in meta);

                    if ((meta.Flags & VaultMetaFlagOrphanCandidate) == 0u)
                        continue;

                    candidateCount++;
                    if (TryReleaseOrphanedBuffer(key, in meta, out long bytes))
                    {
                        releasedBytes += bytes;
                        reclaimedCount++;
                    }
                    else
                    {
                        meta.Flags &= ~VaultMetaFlagOrphanCandidate;
                        WriteMetadata(key, in meta);
                    }
                }

                if (reclaimedCount > 0)
                {
                    _allocatedBytes = _allocatedBytes > releasedBytes ? _allocatedBytes - releasedBytes : 0L;
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagRelocated);
                    BumpVaultGeneration();
                    DumpShinobu202BlackBox();
                }

                _lastOrphanSweepCandidateCount = candidateCount;
                _lastOrphanReclaimCount = reclaimedCount;
                RecordDefragBlackBox(
                    ++_defragTickSequence,
                    reclaimedCount > 0 ? DefragFlagRelocated : (byte)0);
                return reclaimedCount;
            }
            finally
            {
                Volatile.Write(ref _compactionFence, 0);
            }
        }

        /// <inheritdoc />
        public int CountSceneOwnedBuffers(out long bytes, out int lockedCount)
        {
            bytes = 0L;
            lockedCount = 0;
            if (!_initialized || !_keys.IsCreated || !_metadata.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < _keys.Length; i++)
            {
                int key = _keys[i];
                if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                    !IsSceneOwnedVaultOwner(meta.Owner))
                {
                    continue;
                }

                count++;
                bytes += meta.Bytes;
                if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
                {
                    lockedCount++;
                    DumpPhiVodBlackBox();
                    continue;
                }

                VaultArenaBlock block = _blocks[blockIndex];
                if ((block.Reserved0 & BlockFlagLocked) != 0 || block.Reserved1 != 0)
                    lockedCount++;
            }

            return count;
        }

        /// <inheritdoc />
        [Obsolete("Use the owner-tagged overload so compaction telemetry records the scheduling system.", false)]
        public bool TryLockBuffer(BufferID bufferId)
        {
            return false;
        }

        /// <inheritdoc />
        public bool TryLockBuffer(BufferID bufferId, SystemID lockOwner)
        {
            if (!_initialized || bufferId == BufferID.Unknown || lockOwner == SystemID.Unknown)
                return false;

            int key = (int)bufferId;
            if (Volatile.Read(ref _compactionFence) != 0)
            {
                RecordLockContentionFault(key);
                return false;
            }
            Thread.MemoryBarrier();

            int activeLockBit = ResolveActiveLockBit(bufferId);
            Thread.MemoryBarrier();
            if (Volatile.Read(ref _compactionFence) != 0)
            {
                RecordLockContentionFault(key);
                return false;
            }

            if (HasMutationGuardForActiveLockBit(activeLockBit))
            {
                RecordLockContentionFault(key);
                return false;
            }

            if (!TryReadFlatMetadata(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                return false;
            }

            long lockedOffsetBytes = meta.OffsetBytes;

            if (meta.ActiveWriterSystemID != 0 ||
                (meta.LastAliasRequester != SystemID.Unknown && meta.LastAliasRequester != lockOwner))
            {
                RecordLockContentionFault(key);
                return false;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if ((block.Reserved0 & BlockFlagExternalView) != 0 ||
                block.Reserved1 == ushort.MaxValue)
            {
                RecordLockContentionFault(key);
                return false;
            }

            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(key);
                return false;
            }

            bool pinLockCommitted = false;
            SystemID committedPreviousAliasRequester = SystemID.Unknown;
            try
            {
                if (!TryReadFlatMetadata(key, out meta) ||
                    !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out blockIndex))
                {
                    return false;
                }

                if (meta.ActiveWriterSystemID != 0 ||
                    (meta.LastAliasRequester != SystemID.Unknown && meta.LastAliasRequester != lockOwner))
                {
                    RecordLockContentionFault(key);
                    return false;
                }

                if (HasMutationGuardForActiveLockBit(activeLockBit))
                {
                    RecordLockContentionFault(key);
                    return false;
                }

                lockedOffsetBytes = meta.OffsetBytes;
                SystemID previousAliasRequester = meta.LastAliasRequester;
                committedPreviousAliasRequester = previousAliasRequester;
                block = _blocks[blockIndex];
                if (block.BufferKey != key ||
                    block.OffsetBytes != meta.OffsetBytes)
                {
                    return false;
                }

                if ((block.Reserved0 & BlockFlagExternalView) != 0 ||
                    block.Reserved1 == ushort.MaxValue)
                {
                    RecordLockContentionFault(key);
                    return false;
                }

                SetActiveGuardLockBits(key);
                SetActiveLockBit(activeLockBit);
                Thread.MemoryBarrier();
                block.Reserved1++;
                block.Reserved0 |= BlockFlagLocked;
                _blocks[blockIndex] = block;
                meta.LastAliasRequester = lockOwner;
                WriteMetadata(key, in meta);
                pinLockCommitted = true;
                Thread.MemoryBarrier();

                if (!TryReadFlatMetadata(key, out VaultBufferMeta postLockMeta) ||
                    postLockMeta.ActiveWriterSystemID != 0 ||
                    postLockMeta.LastAliasRequester != lockOwner)
                {
                    RollbackBufferPinUnlocked(key, lockedOffsetBytes, activeLockBit, previousAliasRequester);
                    return false;
                }

                if (Volatile.Read(ref _compactionFence) != 0)
                {
                    RecordLockContentionFault(key);
                    RollbackBufferPinUnlocked(key, lockedOffsetBytes, activeLockBit, previousAliasRequester);
                    return false;
                }

                return true;
            }
            catch
            {
                if (pinLockCommitted)
                    RollbackBufferPinUnlocked(key, lockedOffsetBytes, activeLockBit, committedPreviousAliasRequester);

                throw;
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool RollbackBufferPinUnlocked(
            int bufferKey,
            long offsetBytes,
            int activeLockBit,
            SystemID previousAliasRequester)
        {
            bool released = false;
            if (TryFindOccupiedBlockIndex(bufferKey, offsetBytes, out int blockIndex))
            {
                VaultArenaBlock block = _blocks[blockIndex];
                if (block.BufferKey == bufferKey &&
                    block.OffsetBytes == offsetBytes &&
                    block.Reserved1 > 0)
                {
                    block.Reserved1--;
                    if (block.Reserved1 == 0)
                        block.Reserved0 &= unchecked((byte)~BlockFlagLocked);
                    _blocks[blockIndex] = block;
                    RestorePinOwnerMetadataLocked(bufferKey, offsetBytes, previousAliasRequester);
                    released = true;
                }
            }

            Thread.MemoryBarrier();
            ClearActiveLockBitIfUnusedLocked(activeLockBit);
            return released;
        }

        /// <inheritdoc />
        [Obsolete("Use the owner-tagged overload so compaction telemetry records the scheduling system.", false)]
        public bool TryUnlockBuffer(BufferID bufferId)
        {
            return false;
        }

        /// <inheritdoc />
        public bool TryUnlockBuffer(BufferID bufferId, SystemID lockOwner)
        {
            if (!_initialized || bufferId == BufferID.Unknown || lockOwner == SystemID.Unknown)
                return false;

            int key = (int)bufferId;
            if (!TryReadFlatMetadata(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                return false;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.Reserved1 == 0 || meta.LastAliasRequester != lockOwner)
                return false;

            if (!TryEnterReleaseMutationGate())
            {
                RecordLockContentionFault(key);
                return QueueDeferredBufferPinRelease(key, meta.OffsetBytes, ResolveActiveLockBit(bufferId), (int)lockOwner);
            }

            try
            {
                if (!TryReadFlatMetadata(key, out meta) ||
                    !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out blockIndex))
                {
                    return false;
                }

                block = _blocks[blockIndex];
                if (block.BufferKey != key ||
                    block.OffsetBytes != meta.OffsetBytes ||
                    block.Reserved1 == 0 ||
                    meta.LastAliasRequester != lockOwner)
                {
                    return false;
                }

                block.Reserved1--;
                if (block.Reserved1 == 0)
                {
                    block.Reserved0 &= unchecked((byte)~BlockFlagLocked);
                    if ((block.Reserved0 & BlockFlagExternalView) == 0)
                        meta.LastAliasRequester = SystemID.Unknown;
                    WriteMetadata(key, in meta);
                }

                _blocks[blockIndex] = block;
                Thread.MemoryBarrier();
                ClearActiveLockBitIfUnusedLocked(ResolveActiveLockBit(bufferId));
            }
            finally
            {
                ReleaseBlockMutationGate();
            }

            return true;
        }

        private void RestorePinOwnerMetadataLocked(int key, long offsetBytes, SystemID previousAliasRequester)
        {
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) || meta.OffsetBytes != offsetBytes)
                return;

            meta.LastAliasRequester = previousAliasRequester;
            WriteMetadata(key, in meta);
        }

        /// <inheritdoc />
        public bool TryAcquireMutationGuard(ulong writeMask)
        {
            if (!_initialized || writeMask == 0UL)
                return false;
            if (Volatile.Read(ref _compactionFence) != 0)
            {
                RecordMutationGuardContentionFault(writeMask);
                return false;
            }

            if (!TryEnterBlockMutationGate())
            {
                RecordMutationGuardContentionFault(writeMask);
                return false;
            }

            try
            {
                int lowMask = unchecked((int)(uint)writeMask);
                int highMask = unchecked((int)(uint)(writeMask >> 32));
                int observedLow = Volatile.Read(ref _mutationGuardMaskLow);
                int observedHigh = Volatile.Read(ref _mutationGuardMaskHigh);
                if ((observedLow & lowMask) != 0 || (observedHigh & highMask) != 0)
                {
                    RecordMutationGuardContentionFault(writeMask);
                    return false;
                }

                if (HasActiveLockConflictForMutationMask(writeMask))
                {
                    // Names the buffer, not the residue. Runs under the block mutation gate, so the block
                    // scan behind it reads stable state.
                    RecordMutationGuardLockConflictFault(writeMask);
                    return false;
                }

                bool lowAcquired = false;
                if (lowMask != 0)
                {
                    if (Interlocked.CompareExchange(ref _mutationGuardMaskLow, observedLow | lowMask, observedLow) != observedLow)
                    {
                        RecordMutationGuardContentionFault(writeMask);
                        return false;
                    }

                    lowAcquired = true;
                }

                bool highAcquired = false;
                if (highMask != 0)
                {
                    if (Interlocked.CompareExchange(ref _mutationGuardMaskHigh, observedHigh | highMask, observedHigh) != observedHigh)
                    {
                        if (lowAcquired)
                            ReleaseMutationGuard(unchecked((uint)lowMask));
                        RecordMutationGuardContentionFault(writeMask);
                        return false;
                    }

                    highAcquired = true;
                }

                Thread.MemoryBarrier();
                if (Volatile.Read(ref _compactionFence) == 0 &&
                    !HasActiveLockConflictForMutationMask(writeMask))
                {
                    return true;
                }

                if (lowAcquired || highAcquired)
                {
                    ulong acquiredMask = (lowAcquired ? (uint)lowMask : 0UL) |
                        (highAcquired ? ((ulong)(uint)highMask << 32) : 0UL);
                    ReleaseMutationGuard(acquiredMask);
                }

                RecordMutationGuardContentionFault(writeMask);
                return false;
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        /// <inheritdoc />
        public void ReleaseMutationGuard(ulong writeMask)
        {
            if (writeMask == 0UL)
                return;

            int lowMask = unchecked((int)(uint)writeMask);
            int highMask = unchecked((int)(uint)(writeMask >> 32));
            ClearAtomicBits(ref _mutationGuardMaskLow, lowMask);
            ClearAtomicBits(ref _mutationGuardMaskHigh, highMask);
        }

        private static int ResolveActiveLockBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 31u));
            return 1 << bitIndex;
        }

        // Every guard bit a buffer id can occupy in a TryAcquireMutationGuard mask.
        //
        // The mask a caller passes in is a LOSSY hash of buffer ids and this tree uses TWO conventions for
        // it: 1UL << (id & 63) - InputDispatcher.MutationGuardBit - and 1UL << (id & 31), which is the
        // 208-call-site majority and is asserted verbatim by Audio/Editor/AdvancedAcousticsSmokeTester.cs
        // as "mutation guard uses DataVault active-lock lanes". The vault cannot recover ids from a mask, so
        // the tightest question it can answer is "could this mask name this buffer under either
        // convention". Returning both candidates makes the answer correct for both, and when
        // (id & 63) < 32 the two coincide and this is a single bit.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolveGuardLockBits(int bufferKey)
        {
            uint id = unchecked((uint)bufferKey);
            return (1UL << unchecked((int)(id & 63u))) | (1UL << unchecked((int)(id & 31u)));
        }

        // The strict half of the pair above: the single bit a caller claims when it provably uses
        // InputDispatcher.MutationGuardBit's own convention, 1UL << (id & 63), with no (id & 31) candidate
        // folded in. ResolveGuardLockBits stays the fail-closed default for a mask whose convention cannot
        // be determined; this one is for the shadow that records what is EXACTLY locked, so the two can be
        // compared instead of one hiding the other. When (id & 63) < 32 both functions return the same
        // single bit, which is why the strict shadow can never claim a bit the union shadow lacks.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolveGuard64LockBit(int bufferKey)
        {
            uint id = unchecked((uint)bufferKey);
            return 1UL << unchecked((int)(id & 63u));
        }

        // Both candidates above are congruent mod 32, so one 32-lane active-lock bit 1 << r owns exactly
        // two of the 64 guard bits - r and r + 32 - and no others. That containment is what lets the
        // release path rescope one residue class of the guard shadow without disturbing the other 31.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolveGuardLockClassMask(int activeLockBit)
        {
            uint classBits = unchecked((uint)activeLockBit);
            return classBits | ((ulong)classBits << 32);
        }

        private static long ResolveAllocationLockToken(uint shiftFrameId)
        {
            return shiftFrameId == 0u ? -1L : shiftFrameId;
        }

        private bool TryAcquireBlockMutationGate()
        {
            if (Interlocked.CompareExchange(ref _blockMutationGate, 1, 0) != 0)
                return false;

            Thread.MemoryBarrier();
            return true;
        }

        private bool TryEnterBlockMutationGate()
        {
            if (Volatile.Read(ref _compactionFence) != 0)
                return false;
            if (!TryAcquireBlockMutationGate())
                return false;
            if (Volatile.Read(ref _compactionFence) == 0)
            {
                try
                {
                    DrainDeferredReleaseRequestsLocked();
                    return true;
                }
                catch
                {
                    ReleaseBlockMutationGate();
                    throw;
                }
            }

            ReleaseBlockMutationGate();
            return false;
        }

        private bool TryEnterReleaseMutationGate()
        {
            if (!TryAcquireBlockMutationGate())
                return false;

            try
            {
                DrainDeferredReleaseRequestsLocked();
                return true;
            }
            catch
            {
                ReleaseBlockMutationGate();
                throw;
            }
        }

        private void ReleaseBlockMutationGate()
        {
            Thread.MemoryBarrier();
            Interlocked.Exchange(ref _blockMutationGate, 0);
        }

        private bool TryReserveThreadWriterSlot(int threadId, int bufferKey, long offsetBytes, int systemID)
        {
            if (!_writerThreadLockSlots.IsCreated ||
                threadId <= 0 ||
                bufferKey <= 0 ||
                offsetBytes < 0L ||
                systemID == 0)
            {
                RecordLockContentionFault(bufferKey);
                return false;
            }

            VaultThreadWriteLockSlot* slots =
                (VaultThreadWriteLockSlot*)NativeArrayUnsafeUtility.GetUnsafePtr(_writerThreadLockSlots);
            for (int i = 0; i < _writerThreadLockSlots.Length; i++)
            {
                VaultThreadWriteLockSlot* slot = slots + i;
                int state = Volatile.Read(ref slot->State);
                if (state != WriterThreadLockSlotStateEmpty &&
                    Volatile.Read(ref slot->ThreadId) == threadId)
                {
                    RecordLockContentionFault(bufferKey);
                    return false;
                }
            }

            for (int i = 0; i < _writerThreadLockSlots.Length; i++)
            {
                VaultThreadWriteLockSlot* slot = slots + i;
                if (Volatile.Read(ref slot->State) != WriterThreadLockSlotStateEmpty)
                    continue;

                if (Interlocked.CompareExchange(
                    ref slot->State,
                    WriterThreadLockSlotStateWriting,
                    WriterThreadLockSlotStateEmpty) != WriterThreadLockSlotStateEmpty)
                {
                    continue;
                }

                slot->ThreadId = threadId;
                slot->BufferKey = bufferKey;
                slot->SystemId = systemID;
                slot->OffsetBytes = offsetBytes;
                Thread.MemoryBarrier();
                Volatile.Write(ref slot->State, WriterThreadLockSlotStateActive);
                return true;
            }

            RecordLockContentionFault(bufferKey);
            return false;
        }

        private bool ReleaseThreadWriterSlotForLock(int bufferKey, long offsetBytes, int systemID)
        {
            if (!_writerThreadLockSlots.IsCreated || bufferKey <= 0 || offsetBytes < 0L)
                return false;

            VaultThreadWriteLockSlot* slots =
                (VaultThreadWriteLockSlot*)NativeArrayUnsafeUtility.GetUnsafePtr(_writerThreadLockSlots);
            for (int i = 0; i < _writerThreadLockSlots.Length; i++)
            {
                VaultThreadWriteLockSlot* slot = slots + i;
                if (Volatile.Read(ref slot->State) != WriterThreadLockSlotStateActive ||
                    Volatile.Read(ref slot->BufferKey) != bufferKey ||
                    Volatile.Read(ref slot->OffsetBytes) != offsetBytes ||
                    (systemID != 0 && Volatile.Read(ref slot->SystemId) != systemID))
                {
                    continue;
                }

                if (Interlocked.CompareExchange(
                    ref slot->State,
                    WriterThreadLockSlotStateWriting,
                    WriterThreadLockSlotStateActive) != WriterThreadLockSlotStateActive)
                {
                    continue;
                }

                slot->ThreadId = 0;
                slot->BufferKey = 0;
                slot->SystemId = 0;
                slot->OffsetBytes = 0L;
                Thread.MemoryBarrier();
                Volatile.Write(ref slot->State, WriterThreadLockSlotStateEmpty);
                return true;
            }

            return false;
        }

        private void SetActiveLockBit(int bit)
        {
            int observed;
            int updated;
            do
            {
                observed = Volatile.Read(ref _activeLocks);
                updated = observed | bit;
                if (updated == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref _activeLocks, updated, observed) != observed);
        }

        private void ClearActiveLockBitIfUnused(int bit)
        {
            if (TryAcquireBlockMutationGate())
            {
                try
                {
                    ClearActiveLockBitIfUnusedLocked(bit);
                }
                finally
                {
                    ReleaseBlockMutationGate();
                }

                return;
            }

            RecordLockContentionFault(0);
        }

        private void ClearActiveLockBitIfUnusedLocked(int bit)
        {
            // One scan answers both questions. _activeLocks keeps its original all-or-nothing rule - the
            // lane stays set while ANY block in the residue class is still locked - but the guard shadow is
            // rescoped to exactly the guard bits the scan proved are still claimed, so a released lock
            // frees its guard bit immediately instead of waiting for the whole class to empty. Without that
            // republish the shadow would degenerate back into the residue fold under steady lock traffic.
            bool anyLockedInClass = ScanLockedGuardBitsForClass(bit, out ulong claimedGuardBits);
            RepublishGuardLockClassLocked(bit, claimedGuardBits);
            if (anyLockedInClass)
                return;

            int observed;
            int updated;
            do
            {
                observed = Volatile.Read(ref _activeLocks);
                updated = observed & ~bit;
                if (updated == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref _activeLocks, updated, observed) != observed);
        }

        private bool HasLockedBlockForBit(int bit)
        {
            return ScanLockedGuardBitsForClass(bit, out _);
        }

        // Returns whether any block in the 32-lane residue class is still locked or pinned, and which of
        // the two guard bits that class owns are still claimed. Bails as soon as both class guard bits are
        // claimed, because no later block can change either answer - that keeps the worst case equal to the
        // single-answer scan this replaced.
        private bool ScanLockedGuardBitsForClass(int activeLockBit, out ulong claimedGuardBits)
        {
            claimedGuardBits = 0UL;
            if (!_blocks.IsCreated || activeLockBit == 0)
                return false;

            ulong classMask = ResolveGuardLockClassMask(activeLockBit);
            bool anyLocked = false;
            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State != BlockStateOccupied ||
                    ((block.Reserved0 & BlockFlagLocked) == 0 && block.Reserved1 == 0))
                {
                    continue;
                }

                if (ResolveActiveLockBit((BufferID)block.BufferKey) != activeLockBit)
                    continue;

                anyLocked = true;
                claimedGuardBits |= ResolveGuardLockBits(block.BufferKey) & classMask;
                if (claimedGuardBits == classMask)
                    return true;
            }

            return anyLocked;
        }

        // Replaces - not decrements - the two guard bits this residue class owns with the set the scan
        // proved still claimed. A recompute from block ground truth cannot drift the way a refcount can.
        // The caller holds the block mutation gate, and TryAcquireMutationGuard holds that same gate while
        // it reads the shadow, so the conflict test can never observe this half-applied.
        private void RepublishGuardLockClassLocked(int activeLockBit, ulong claimedGuardBits)
        {
            if (activeLockBit == 0)
                return;

            ulong classMask = ResolveGuardLockClassMask(activeLockBit);
            ulong keepBits = claimedGuardBits & classMask;
            ReplaceAtomicBits(
                ref _activeGuardLockMaskLow,
                unchecked((int)(uint)classMask),
                unchecked((int)(uint)keepBits));
            ReplaceAtomicBits(
                ref _activeGuardLockMaskHigh,
                unchecked((int)(uint)(classMask >> 32)),
                unchecked((int)(uint)(keepBits >> 32)));

            // The strict shadow is rescoped from the same scan, over the same residue class. keepBits already
            // carries only bits this class owns - r and r + 32 - and the (id & 63) bit of every buffer in the
            // class is one of those two, so masking it against the class mask is exact rather than a
            // narrowing. Republished from the identical scan result so the two shadows cannot drift apart.
            ulong keep64Bits = claimedGuardBits & classMask;
            ReplaceAtomicBits(
                ref _activeGuardLock64MaskLow,
                unchecked((int)(uint)classMask),
                unchecked((int)(uint)keep64Bits));
            ReplaceAtomicBits(
                ref _activeGuardLock64MaskHigh,
                unchecked((int)(uint)(classMask >> 32)),
                unchecked((int)(uint)(keep64Bits >> 32)));
        }

        private void SetActiveGuardLockBits(int bufferKey)
        {
            ulong guardBits = ResolveGuardLockBits(bufferKey);
            SetAtomicBits(ref _activeGuardLockMaskLow, unchecked((int)(uint)guardBits));
            SetAtomicBits(ref _activeGuardLockMaskHigh, unchecked((int)(uint)(guardBits >> 32)));

            // Same locks, recorded under the (id & 63) convention alone. Both shadows are written here so a
            // lock can never be present in one and absent from the other.
            ulong guard64Bits = ResolveGuard64LockBit(bufferKey);
            SetAtomicBits(ref _activeGuardLock64MaskLow, unchecked((int)(uint)guard64Bits));
            SetAtomicBits(ref _activeGuardLock64MaskHigh, unchecked((int)(uint)(guard64Bits >> 32)));
        }

        private bool HasActiveBurstLocks(uint externalLockMask)
        {
            uint localLockMask = unchecked((uint)Volatile.Read(ref _activeLocks));
            return (localLockMask | externalLockMask) != 0u ||
                Volatile.Read(ref _mutationGuardMaskLow) != 0 ||
                Volatile.Read(ref _mutationGuardMaskHigh) != 0;
        }

        private bool HasMutationGuardForActiveLockBit(int activeLockBit)
        {
            int guardMask = Volatile.Read(ref _mutationGuardMaskLow) |
                Volatile.Read(ref _mutationGuardMaskHigh);
            return activeLockBit != 0 &&
                (guardMask & activeLockBit) != 0;
        }

        // Refuses a mutation guard while a lock the mask can actually name is outstanding.
        //
        // WAS: (_activeLocks & (lowMask | highMask)) != 0 - the caller's 64-bit mask folded onto the 32
        // active-lock lanes. That over-approximated by construction. InputDispatcher's 14 buffer ids hash to
        // guard bits {0,1,2,3,5,56..63}, which fold to lanes 0xFF00002F - THIRTEEN of the 32 lanes - so any
        // of the project's other buffers whose (id & 31) landed in that set refused an input publish while
        // write-locked or pinned, with nothing to do with input. Comparing against a shadow keyed the way
        // the mask itself is keyed removes that class of refusal.
        //
        // NEVER WEAKER than the fold, for both conventions in the tree:
        //   - a (id & 31) caller's mask bits are all < 32, and a locked buffer always claims its own
        //     (id & 31) bit, so its refusal set is bit-for-bit what the fold produced;
        //   - a (id & 63) caller loses only refusals whose sole overlap was a mask bit >= 32 in a different
        //     mod-64 class than the locked buffer. A mask bit >= 32 can only come from the (id & 63)
        //     convention, and under that convention it names buffers congruent to it mod 64 - which
        //     excludes that buffer. No convention could have meant it.
        // Genuine conflicts still refuse: a locked buffer X claims bit (X & 63) AND bit (X & 31), and any
        // mask that names X contains one of those two by the definition of MutationGuardBit. Fail-closed.
        //
        // Residual imprecision is inherent to the guard API, not to this test: bit 56 is claimed by both
        // ShinobuInputCurrentDto(70520) and ShinobuPredictedInputRing(75000), and no reader of the mask can
        // separate them. Only a caller-side change that carries buffer ids instead of a folded mask can.
        private bool HasActiveLockConflictForMutationMask(ulong guardMask)
        {
            if (guardMask == 0UL)
                return false;

            int lowMask = unchecked((int)(uint)guardMask);
            int highMask = unchecked((int)(uint)(guardMask >> 32));

            // THE CALLER'S CONVENTION IS DECIDABLE FROM ITS OWN MASK, and that removes the residual
            // imprecision the comment above calls inherent. 1UL << (id & 31) cannot set a bit >= 32, by
            // construction. So a mask with ANY high bit set could only have been built by the (id & 63)
            // convention - InputDispatcher.MutationGuardBit, VaultMemoryContracts, H8MacroDatabaseService -
            // and for that caller the union shadow's (id & 31) candidates are bits it never meant. Testing it
            // against the strict shadow instead is exact, not weaker: every locked buffer claims its own
            // (id & 63) bit in _activeGuardLock64Mask*, so a buffer this mask genuinely names is still
            // refused. This is what finally separates the bit-56 pair the union shadow cannot:
            // ShinobuInputCurrentDto(70520) is 70520 & 63 == 56, ShinobuPredictedInputRing(75000) is
            // 75000 & 63 == 8 - distinct under (id & 63), identical only under the folded union.
            //
            // A mask whose bits are ALL < 32 is genuinely ambiguous - either convention could have produced
            // it - so it keeps the union shadow and stays fail-closed. That is the 208-call-site majority and
            // loses nothing: under (id & 31) the union shadow's refusal set is already bit-for-bit exact.
            if (highMask != 0)
            {
                return (Volatile.Read(ref _activeGuardLock64MaskLow) & lowMask) != 0 ||
                    (Volatile.Read(ref _activeGuardLock64MaskHigh) & highMask) != 0;
            }

            return (Volatile.Read(ref _activeGuardLockMaskLow) & lowMask) != 0 ||
                (Volatile.Read(ref _activeGuardLockMaskHigh) & highMask) != 0;
        }

        private bool HasPinnedExternalViews()
        {
            if (!_blocks.IsCreated)
                return false;

            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State == BlockStateOccupied &&
                    (block.Reserved0 & BlockFlagExternalView) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetAtomicBits(ref int target, int bits)
        {
            if (bits == 0)
                return;

            int observed;
            int updated;
            do
            {
                observed = Volatile.Read(ref target);
                updated = observed | bits;
                if (updated == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, updated, observed) != observed);
        }

        // Clears classBits and re-sets keepBits in one CAS so a residue class is never briefly empty - a
        // gap there would be a window where a guard could be granted over a still-locked buffer.
        private static void ReplaceAtomicBits(ref int target, int classBits, int keepBits)
        {
            if (classBits == 0)
                return;

            int observed;
            int updated;
            do
            {
                observed = Volatile.Read(ref target);
                updated = (observed & ~classBits) | keepBits;
                if (updated == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, updated, observed) != observed);
        }

        private static void ClearAtomicBits(ref int target, int bits)
        {
            if (bits == 0)
                return;

            int observed;
            int updated;
            do
            {
                observed = Volatile.Read(ref target);
                updated = observed & ~bits;
                if (updated == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, updated, observed) != observed);
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
        public bool TryGetMemoryBlockSnapshot(int index, out VaultMemoryBlockSnapshot snapshot)
        {
            snapshot = default;
            if (!_blocks.IsCreated || (uint)index >= (uint)_blocks.Length)
                return false;

            VaultArenaBlock block = _blocks[index];
            snapshot.OffsetBytes = block.OffsetBytes;
            snapshot.Bytes = block.Bytes;
            snapshot.BufferKey = block.BufferKey;
            snapshot.H8BlockIndex = block.H8BlockIndex;
            snapshot.Version = block.Version;
            snapshot.Owner = 0;
            if (block.BufferKey != 0 && _metadata.IsCreated && _metadata.TryGetValue(block.BufferKey, out VaultBufferMeta meta))
                snapshot.Owner = (ushort)meta.Owner;
            snapshot.LockCount = block.Reserved1;
            snapshot.State = block.State;
            snapshot.Flags = block.Reserved0;
            return true;
        }

        /// <inheritdoc />
        public void LockAllocationsForAupShift(uint shiftFrameId)
        {
            long lockToken = ResolveAllocationLockToken(shiftFrameId);
            Interlocked.Exchange(ref _allocationLock, lockToken);
        }

        /// <inheritdoc />
        public void UnlockAllocationsAfterAupShift(uint shiftFrameId)
        {
            long observedLockToken = Interlocked.Read(ref _allocationLock);
            if (observedLockToken == 0L)
                return;

            if (shiftFrameId == 0u)
            {
                Interlocked.Exchange(ref _allocationLock, 0L);
                return;
            }

            long lockToken = ResolveAllocationLockToken(shiftFrameId);
            Interlocked.CompareExchange(ref _allocationLock, 0L, lockToken);
        }

        /// <inheritdoc />
        public void RecordHeartbeat()
        {
            if (!_initialized || !_defragBlackBox.IsCreated || !_defragBlackBoxDetails.IsCreated)
                return;

            _memoryStarvationWarnings = 0;
            bool pressureDumpRequired = _arenaBytes > 0L && (_allocatedBytes * 10L) >= (_arenaBytes * 9L);
            byte heartbeatFlags = pressureDumpRequired
                ? (byte)(DefragFlagHeartbeat | DefragFlagStressHalt)
                : DefragFlagHeartbeat;
            RecordDefragBlackBox(++_defragTickSequence, heartbeatFlags);
            if (pressureDumpRequired)
                DumpShinobu202BlackBox();
        }

        /// <inheritdoc />
        public void RequestEditorForceDefragmentation()
        {
            Interlocked.Exchange(ref _forceDefragRequested, 1);
        }

        public bool GenerateMockVaultRelocationForValidation(uint seed, int maxMutations, MemoryDefragPhase phase, uint activeBurstLockMask)
        {
            TryDrainDeferredReleaseRequests();
            if (!_initialized ||
                phase != MemoryDefragPhase.PreSimulation ||
                HasActiveBurstLocks(activeBurstLockMask) ||
                Volatile.Read(ref _compactionFence) != 0 ||
                !_metadataByBufferId.IsCreated)
            {
                return false;
            }

            int mutations = math.clamp(maxMutations, 1, 1024);
            if (Interlocked.Exchange(ref _compactionFence, 1) != 0)
                return false;

            try
            {
                GenerateMockVaultRelocationJob relocationJob = new GenerateMockVaultRelocationJob
                {
                    Metadata = _metadataByBufferId,
                    Seed = seed,
                    MaxMutations = mutations
                };
                relocationJob.Execute();

                if (_keys.IsCreated)
                {
                    for (int i = 0; i < _keys.Length; i++)
                    {
                        int key = _keys[i];
                        if (TryReadFlatMetadata(key, out VaultBufferMeta meta))
                            WriteMetadata(key, in meta);
                    }
                }

                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagRelocated);
                BumpVaultGeneration();
                RecordDefragBlackBox(++_defragTickSequence, DefragFlagRelocated);
                return true;
            }
            finally
            {
                Interlocked.Exchange(ref _compactionFence, 0);
            }
        }

        public bool TryGetVaultTelemetrySnapshot(int ageFromNewest, out VaultTelemetrySnapshot snapshot)
        {
            snapshot = default;
            int recordedCount = Volatile.Read(ref _defragBlackBoxRecordedCount);
            if (!_defragBlackBox.IsCreated || !_defragBlackBoxDetails.IsCreated || recordedCount <= 0)
                return false;

            int safeAge = math.clamp(ageFromNewest, 0, recordedCount - 1);
            int index = Volatile.Read(ref _defragBlackBoxCursor) - 1 - safeAge;
            while (index < 0)
                index += _defragBlackBox.Length;

            MemoryDefragTelemetryEntry entry = _defragBlackBox[index];
            MemoryDefragTelemetryDetailEntry detail = _defragBlackBoxDetails[index];
            snapshot.AllocatedBytes = _allocatedBytes;
            snapshot.ArenaBytes = _arenaBytes;
            snapshot.LastMovedBytes = entry.LastMovedBytes;
            snapshot.ResolutionTicks = detail.ResolutionTicks;
            snapshot.VaultGenerationID = entry.VaultGenerationID;
            snapshot.GenerationMismatchCount = detail.GenerationMismatchCount;
            snapshot.LastFaultBufferID = detail.LastFaultBufferID;
            snapshot.LastFaultHandleGeneration = detail.LastFaultHandleGeneration;
            snapshot.LastFaultMetaGeneration = detail.LastFaultMetaGeneration;
            snapshot.LastDefragFlags = detail.Flags;
            snapshot.ResolvedHandleCount = detail.ResolvedHandleCount;
            return true;
        }

#if UNITY_EDITOR
        public bool TryApplyMemoryBudgetCsv(ReadOnlySpan<byte> csvBytes)
        {
            if (!_memoryBudgetEntries.IsCreated || csvBytes.Length == 0)
                return false;

            int count = 0;
            int lineStart = 0;
            while (lineStart < csvBytes.Length && count < _memoryBudgetEntries.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csvBytes.Length && csvBytes[lineEnd] != (byte)'\n' && csvBytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, lineEnd - lineStart);
                if (TryParseBudgetLine(line, out VaultMemoryBudgetEntry entry))
                    _memoryBudgetEntries[count++] = entry;

                lineStart = lineEnd + 1;
                while (lineStart < csvBytes.Length && (csvBytes[lineStart] == (byte)'\n' || csvBytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            _memoryBudgetCount = count;
            return count > 0;
        }
#endif

        /// <inheritdoc />
        [Obsolete("Use the explicit PRE_SIMULATION overload. Legacy overloads record blocked telemetry and never move memory.", false)]
        public void FrostTickDefrag(float elapsedSeconds)
        {
            FrostTickDefrag(elapsedSeconds, 0f, MemoryDefragPhase.Unspecified, ActiveBurstLockMask);
        }

        /// <inheritdoc />
        [Obsolete("Use the explicit PRE_SIMULATION overload. Legacy overloads record blocked telemetry and never move memory.", false)]
        public void FrostTickDefrag(float elapsedSeconds, float systemStress01)
        {
            FrostTickDefrag(elapsedSeconds, systemStress01, MemoryDefragPhase.Unspecified, ActiveBurstLockMask);
        }

        /// <inheritdoc />
        public void FrostTickDefrag(float elapsedSeconds, float systemStress01, MemoryDefragPhase phase, uint activeBurstLockMask)
        {
            if (!_initialized || _arenaBase == null)
                return;

            uint sequence = ++_defragTickSequence;
            ResetDefragTelemetry();
            if (elapsedSeconds < 0f ||
                float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds) ||
                float.IsNaN(systemStress01) ||
                float.IsInfinity(systemStress01))
            {
                LastDefragFlags |= DefragFlagFault;
                RecordDefragBlackBox(sequence);
                DumpDefragBlackBox();
                return;
            }

            if (phase != MemoryDefragPhase.PreSimulation)
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                RecordDefragBlackBox(sequence);
                return;
            }

            bool burstLocked = HasActiveBurstLocks(activeBurstLockMask);
            if (burstLocked && TryDrainDeferredReleaseRequests())
                burstLocked = HasActiveBurstLocks(activeBurstLockMask);
            bool stressHalted = systemStress01 > StressDefragHaltThreshold;
            _memMoveBlockedByStress = stressHalted || burstLocked;
            if (stressHalted)
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagStressHalt);
            if (burstLocked)
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);

            AnalyzeGaps();
            if (!ValidateDefragTelemetry() || !ValidateBlockMap())
            {
                RecordDefragBlackBox(sequence);
                DumpDefragBlackBox();
                return;
            }

            bool forceDefrag = Interlocked.Exchange(ref _forceDefragRequested, 0) != 0;
            if (IsFragmented || forceDefrag)
            {
                PendingMassiveMoveBytes = EstimateLargestOccupiedMoveCandidate();
                if (PendingMassiveMoveBytes >= MassiveMoveThresholdBytes)
                    LastDefragFlags |= DefragFlagMassiveMovePending;

                if (!stressHalted && !burstLocked)
                {
                    TryRunLiveCompactionSlice(activeBurstLockMask);
                    AnalyzeGaps();
                    if (!ValidateDefragTelemetry() || !ValidateBlockMap())
                    {
                        RecordDefragBlackBox(sequence);
                        DumpDefragBlackBox();
                        return;
                    }
                }
            }

            RecordDefragBlackBox(sequence);
        }

        /// <inheritdoc />
        public bool TryReserveMacroDatabaseCache(int capacity)
        {
            EnsureInitialized();
            if (capacity > MaxBufferCapacity)
                return false;

            int safeCapacity = ResolveBufferCapacity(capacity);
            if (!_macroDatabasePayloadCache.IsCreated)
            {
                _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadCacheEntry>(safeCapacity, Allocator.Persistent);
            }
            if (!_macroDatabasePayloadAccessTicks.IsCreated)
            {
                _macroDatabasePayloadAccessTicks = new NativeParallelHashMap<ulong, uint>(safeCapacity, Allocator.Persistent);
            }
            if (!_macroDatabasePayloadKeys.IsCreated)
            {
                _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);
            }
            RegisterMacroDatabasePayloadCacheSentinels();

            if (_macroDatabasePayloadCache.Capacity < safeCapacity)
                _macroDatabasePayloadCache.Capacity = safeCapacity;
            if (_macroDatabasePayloadAccessTicks.Capacity < safeCapacity)
                _macroDatabasePayloadAccessTicks.Capacity = safeCapacity;
            if (_macroDatabasePayloadKeys.Capacity < safeCapacity)
                _macroDatabasePayloadKeys.Capacity = safeCapacity;
            RefreshMacroDatabasePayloadCacheSentinels();

            return _macroDatabasePayloadCache.Capacity >= safeCapacity &&
                   _macroDatabasePayloadAccessTicks.Capacity >= safeCapacity &&
                   _macroDatabasePayloadKeys.Capacity >= safeCapacity;
        }

        /// <inheritdoc />
        public bool TryStoreMacroDatabasePayload(
            ulong sectorHash,
            NativeArray<byte> source,
            int byteLength,
            long fileOffset,
            byte flags,
            out MacroDatabasePayloadHandle handle)
        {
            handle = default;
            if (sectorHash == 0UL ||
                !source.IsCreated ||
                byteLength <= 0 ||
                byteLength > source.Length ||
                byteLength > MaxMacroDatabasePayloadBytes)
            {
                return false;
            }

            void* sourcePointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            if (sourcePointer == null)
                return false;

            EnsureInitialized();
            if (!_macroDatabasePayloadCache.IsCreated && !TryReserveMacroDatabaseCache(DefaultBufferCapacity))
                return false;

            bool hasExisting = _macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadCacheEntry existing);
            if (!hasExisting && _macroDatabasePayloadKeys.Length >= _macroDatabasePayloadKeys.Capacity)
            {
                if (!TryEvictLeastRecentlyUsedMacroDatabasePayload())
                    return false;
            }
            else if (hasExisting && !EnsureMacroDatabaseKeyRegistered(sectorHash))
            {
                return false;
            }

            void* payloadPointer = H8Memory.AllocateRaw(
                byteLength,
                16,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                clearMemory: false,
                H8AllocationFlags.Vault);
            if (payloadPointer == null)
                return false;

            if (!Hecton8.Core.UnsafeMemoryCopyGuard.SafeCopy(payloadPointer, byteLength, sourcePointer, byteLength))
            {
                FreeMacroDatabasePayloadRollbackOrThrow(payloadPointer, null);
                return false;
            }

            uint nextVersion = hasExisting ? NextGeneration(existing.Handle.Version) : 1u;
            handle = new MacroDatabasePayloadHandle
            {
                SectorHash = sectorHash,
                PayloadToken = MixMacroDatabasePayloadToken(sectorHash, nextVersion),
                FileOffset = fileOffset,
                ByteLength = byteLength,
                Version = nextVersion,
                Flags = flags
            };
            MacroDatabasePayloadCacheEntry entry = new MacroDatabasePayloadCacheEntry
            {
                Handle = handle,
                Pointer = (IntPtr)payloadPointer
            };

            if (hasExisting)
            {
                try
                {
                    if (existing.Pointer != IntPtr.Zero &&
                        !H8Memory.TryFreeRaw(existing.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault))
                    {
                        FreeMacroDatabasePayloadRollbackOrThrow(payloadPointer, null);
                        handle = default;
                        return false;
                    }

                    SubtractMacroDatabasePayloadBytes(existing.Handle.ByteLength);
                    _macroDatabasePayloadCache[sectorHash] = entry;
                    TouchMacroDatabasePayload(sectorHash);
                }
                catch (Exception replaceException)
                {
                    FreeMacroDatabasePayloadRollbackOrThrow(payloadPointer, replaceException);
                    handle = default;
                    throw;
                }
            }
            else
            {
                if (!_macroDatabasePayloadCache.TryAdd(sectorHash, entry))
                {
                    FreeMacroDatabasePayloadRollbackOrThrow(payloadPointer, null);
                    handle = default;
                    return false;
                }

                if (!EnsureMacroDatabaseKeyRegistered(sectorHash))
                {
                    _macroDatabasePayloadCache.Remove(sectorHash);
                    FreeMacroDatabasePayloadRollbackOrThrow(payloadPointer, null);
                    handle = default;
                    return false;
                }

                TouchMacroDatabasePayload(sectorHash);
            }

            _macroDatabasePayloadBytes += byteLength;
            BumpVaultGeneration();
            return true;
        }

        private static void FreeMacroDatabasePayloadRollbackOrThrow(void* payloadPointer, Exception rootException)
        {
            if (payloadPointer == null)
                return;

            try
            {
                if (H8Memory.TryFreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault))
                    return;
            }
            catch (Exception rollbackException)
            {
                if (rootException != null)
                    throw new AggregateException("GlobalDataVault macro database payload replacement rollback failed.", rootException, rollbackException);

                throw;
            }

            Exception cleanupFailure = new InvalidOperationException("GlobalDataVault macro database payload rollback could not free the allocated pointer.");
            if (rootException != null)
                throw new AggregateException("GlobalDataVault macro database payload replacement rollback failed.", rootException, cleanupFailure);

            throw cleanupFailure;
        }

        /// <inheritdoc />
        public bool TryOpenMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle handle)
        {
            handle = default;
            if (!_macroDatabasePayloadCache.IsCreated ||
                !_macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadCacheEntry entry))
            {
                return false;
            }

            handle = entry.Handle;
            TouchMacroDatabasePayload(sectorHash);
            return true;
        }

        /// <inheritdoc />
        public bool TryCopyMacroDatabasePayload(
            ulong sectorHash,
            int sourceOffsetBytes,
            NativeArray<byte> destination,
            int destinationCapacityBytes,
            out int bytesCopied,
            out MacroDatabasePayloadHandle handle)
        {
            bytesCopied = 0;
            handle = default;
            if (sectorHash == 0UL ||
                sourceOffsetBytes < 0 ||
                !destination.IsCreated ||
                destinationCapacityBytes <= 0 ||
                !_macroDatabasePayloadCache.IsCreated ||
                !_macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadCacheEntry entry))
            {
                return false;
            }

            handle = entry.Handle;
            if (entry.Pointer == IntPtr.Zero ||
                handle.ByteLength <= 0 ||
                sourceOffsetBytes >= handle.ByteLength)
            {
                return false;
            }

            int availableBytes = handle.ByteLength - sourceOffsetBytes;
            int destinationBytes = math.min(destinationCapacityBytes, destination.Length);
            int copyBytes = availableBytes < destinationBytes ? availableBytes : destinationBytes;
            if (copyBytes <= 0)
                return false;

            byte* source = (byte*)entry.Pointer.ToPointer() + sourceOffsetBytes;
            void* destinationPointer = NativeArrayUnsafeUtility.GetUnsafePtr(destination);
            if (destinationPointer == null)
                return false;

            if (!Hecton8.Core.UnsafeMemoryCopyGuard.SafeCopy(destinationPointer, destinationBytes, source, copyBytes))
                return false;

            bytesCopied = copyBytes;
            TouchMacroDatabasePayload(sectorHash);
            return true;
        }

        /// <inheritdoc />
        public bool TryCopyMacroDatabasePayload<T>(
            ulong sectorHash,
            int sourceOffsetBytes,
            NativeArray<T> destination,
            int destinationCapacityBytes,
            out int bytesCopied,
            out MacroDatabasePayloadHandle handle)
            where T : struct
        {
            bytesCopied = 0;
            handle = default;
            if (!destination.IsCreated || destinationCapacityBytes <= 0)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            long destinationBytesLong = (long)destination.Length * stride;
            if (stride <= 0 || destinationBytesLong <= 0L)
                return false;

            int destinationBytes = destinationBytesLong > int.MaxValue ? int.MaxValue : (int)destinationBytesLong;
            int safeCapacity = math.min(destinationCapacityBytes, destinationBytes);
            if (safeCapacity <= 0)
                return false;

            void* destinationPointer = NativeArrayUnsafeUtility.GetUnsafePtr(destination);
            NativeArray<byte> byteView = H8Memory.CreateNativeArrayView<byte>(destinationPointer, safeCapacity);
            return TryCopyMacroDatabasePayload(
                sectorHash,
                sourceOffsetBytes,
                byteView,
                safeCapacity,
                out bytesCopied,
                out handle);
        }

        /// <inheritdoc />
        public bool TryRemoveMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle removed)
        {
            removed = default;
            if (!_macroDatabasePayloadCache.IsCreated ||
                !_macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadCacheEntry entry))
            {
                return false;
            }

            removed = entry.Handle;
            if (entry.Pointer != IntPtr.Zero &&
                !H8Memory.TryFreeRaw(entry.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault))
            {
                removed = default;
                return false;
            }

            _macroDatabasePayloadCache.Remove(sectorHash);
            if (_macroDatabasePayloadAccessTicks.IsCreated)
                _macroDatabasePayloadAccessTicks.Remove(sectorHash);
            SubtractMacroDatabasePayloadBytes(removed.ByteLength);
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
            {
                _disposed = true;
                return;
            }

            Arm64AlignmentTelemetry.ReleaseOwnedBuffers(this);

            DisposeMacroDatabasePayloadCache();

            if (_blocks.IsCreated)
            {
                for (int i = 0; i < _blocks.Length; i++)
                {
                    VaultArenaBlock block = _blocks[i];
                    block.State = BlockStateFree;
                    block.Reserved0 = 0;
                    block.Reserved1 = 0;
                    UpdateH8Descriptor(in block);
                }
            }

            ClearNativeStorageBeforeDispose();
            if (_arenaBase != null)
            {
                if (!H8Memory.TryFreeRaw(_arenaBase, Allocator.Persistent, SystemID.CoreDataVault))
                    throw new InvalidOperationException("GlobalDataVault arena root dispose could not free the allocated pointer.");

                _arenaBase = null;
            }
            if (_keys.IsCreated)
            {
                _keys.Dispose();
                UnregisterSidecar(ref _keysSentinelId, nameof(_keys));
            }
            if (_buffers.IsCreated)
            {
                _buffers.Dispose();
                UnregisterSidecar(ref _buffersSentinelId, nameof(_buffers));
            }
            if (_metadata.IsCreated)
            {
                _metadata.Dispose();
                UnregisterSidecar(ref _metadataSentinelId, nameof(_metadata));
            }
            if (_metadataGenerationByBufferId.IsCreated)
            {
                _metadataGenerationByBufferId.Dispose();
                UnregisterSidecar(ref _metadataGenerationByBufferIdSentinelId, nameof(_metadataGenerationByBufferId));
            }
            if (_metadataByBufferId.IsCreated)
            {
                H8Memory.Release(ref _metadataByBufferId, SystemID.CoreDataVault);
            }
            if (_blocks.IsCreated)
            {
                _blocks.Dispose();
                UnregisterSidecar(ref _blocksSentinelId, nameof(_blocks));
            }
            UnregisterNativeSidecarStorage();
            if (_defragBlackBox.IsCreated || _defragBlackBoxDetails.IsCreated)
            {
                bool canReleaseBlackBox = IsMemorySentryDumpIdleOnDispose();
                if (!canReleaseBlackBox)
                    canReleaseBlackBox = Volatile.Read(ref _memorySentryDumpInFlight) == 0;
                if (canReleaseBlackBox)
                {
                    if (_defragBlackBoxDetails.IsCreated)
                        H8Memory.Release(ref _defragBlackBoxDetails, SystemID.CoreDataVault);
                    if (_defragBlackBox.IsCreated)
                        H8Memory.Release(ref _defragBlackBox, SystemID.CoreDataVault);
                }
            }
            if (_lastRelocationRecords.IsCreated)
            {
                H8Memory.Release(ref _lastRelocationRecords, SystemID.CoreDataVault);
            }
            if (_memoryBudgetEntries.IsCreated)
            {
                H8Memory.Release(ref _memoryBudgetEntries, SystemID.CoreDataVault);
            }
            if (_deferredReleaseRequests.IsCreated)
            {
                H8Memory.Release(ref _deferredReleaseRequests, SystemID.CoreDataVault);
            }
            if (_writerThreadLockSlots.IsCreated)
            {
                H8Memory.Release(ref _writerThreadLockSlots, SystemID.CoreDataVault);
            }
            _allocatedBytes = 0L;
            _arenaBytes = 0L;
            _arenaCapacityLimitBytes = 0L;
            Interlocked.Exchange(ref _allocationLock, 0L);
            _compactionFence = 0;
            _activeLocks = 0;
            _blockMutationGate = 0;
            _mutationGuardMaskLow = 0;
            _mutationGuardMaskHigh = 0;
            _activeGuardLockMaskLow = 0;
            _activeGuardLockMaskHigh = 0;
            _activeGuardLock64MaskLow = 0;
            _activeGuardLock64MaskHigh = 0;
            _memoryStarvationWarnings = 0;
            _defragBlackBoxCursor = 0;
            _defragBlackBoxRecordedCount = 0;
            _lastRelocationRecordCount = 0;
            _memoryBudgetCount = 0;
            _deferredReleaseWriteCursor = 0;
            _deferredReleasePendingCount = 0;
            _deferredReleaseEnqueueGate = 0;
            _generationHandleMissCount = 0;
            _lastFaultBufferId = 0;
            _lastFaultHandleGeneration = 0u;
            _lastFaultMetaGeneration = 0u;
            _resolvedHandleCount = 0L;
            _resolutionTickAccumulator = 0L;
            _forceDefragRequested = 0;
            _compactionWatchdogBreachCount = 0;
            _defragLockedSkipCount = 0;
            _memorySentryDumpInFlight = 0;
            _memorySentryDumpRequested = 0;
            _memorySentryDumpWritten = 0;
            _totalDefragMovedBytes = 0L;
            _deferredArenaGrowthBytes = 0L;
            _arenaGrowthInProgress = 0;
            _defragTickSequence = 0u;
            _lastPublishedPointerBits = 0L;
            _lastRelocatedSystemId = SystemID.Unknown;
            _vaultGenerationId = 0u;
            _defragDumpWritten = false;
            _phiVodDumpWritten = false;
            _shinobu202DumpWritten = false;
            ResetDefragTelemetry();
            _initialized = false;
            _disposed = true;
            if (ReferenceEquals(_latestCreated, this))
                _latestCreated = null;
        }

        private void ClearNativeStorageBeforeDispose()
        {
            if (_arenaBase != null && _arenaBytes > 0L)
                UnsafeUtility.MemClear(_arenaBase, _arenaBytes);
            if (_metadataByBufferId.IsCreated && _metadataByBufferId.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_metadataByBufferId), UnsafeUtility.SizeOf<VaultBufferMeta>() * (long)_metadataByBufferId.Length);
            if (_defragBlackBox.IsCreated && _defragBlackBox.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_defragBlackBox), UnsafeUtility.SizeOf<MemoryDefragTelemetryEntry>() * (long)_defragBlackBox.Length);
            if (_defragBlackBoxDetails.IsCreated && _defragBlackBoxDetails.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_defragBlackBoxDetails), UnsafeUtility.SizeOf<MemoryDefragTelemetryDetailEntry>() * (long)_defragBlackBoxDetails.Length);
            if (_lastRelocationRecords.IsCreated && _lastRelocationRecords.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_lastRelocationRecords), UnsafeUtility.SizeOf<VaultRelocationRecord>() * (long)_lastRelocationRecords.Length);
            if (_memoryBudgetEntries.IsCreated && _memoryBudgetEntries.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_memoryBudgetEntries), UnsafeUtility.SizeOf<VaultMemoryBudgetEntry>() * (long)_memoryBudgetEntries.Length);
            if (_deferredReleaseRequests.IsCreated && _deferredReleaseRequests.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_deferredReleaseRequests), UnsafeUtility.SizeOf<DeferredVaultReleaseRequest>() * (long)_deferredReleaseRequests.Length);
            if (_writerThreadLockSlots.IsCreated && _writerThreadLockSlots.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_writerThreadLockSlots), UnsafeUtility.SizeOf<VaultThreadWriteLockSlot>() * (long)_writerThreadLockSlots.Length);
            if (_keys.IsCreated)
            {
                for (int i = 0; i < _keys.Length; i++)
                    _keys[i] = 0;
            }
            if (_blocks.IsCreated)
            {
                for (int i = 0; i < _blocks.Length; i++)
                    _blocks[i] = default;
            }
        }

        private bool IsMemorySentryDumpIdleOnDispose()
        {
            return Volatile.Read(ref _memorySentryDumpInFlight) == 0;
        }

        private void DisposeMacroDatabasePayloadCache()
        {
            if (_macroDatabasePayloadKeys.IsCreated && _macroDatabasePayloadCache.IsCreated)
            {
                for (int i = 0; i < _macroDatabasePayloadKeys.Length; i++)
                {
                    ulong sectorHash = _macroDatabasePayloadKeys[i];
                    if (_macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadCacheEntry entry) &&
                        entry.Pointer != IntPtr.Zero)
                    {
                        if (!H8Memory.TryFreeRaw(entry.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault))
                            throw new InvalidOperationException("GlobalDataVault macro database payload dispose could not free the allocated pointer.");
                    }
                }
            }

            if (_macroDatabasePayloadKeys.IsCreated)
            {
                _macroDatabasePayloadKeys.Dispose();
                UnregisterSidecar(ref _macroDatabasePayloadKeysSentinelId, nameof(_macroDatabasePayloadKeys));
            }
            if (_macroDatabasePayloadAccessTicks.IsCreated)
            {
                _macroDatabasePayloadAccessTicks.Dispose();
                UnregisterSidecar(ref _macroDatabasePayloadAccessTicksSentinelId, nameof(_macroDatabasePayloadAccessTicks));
            }
            if (_macroDatabasePayloadCache.IsCreated)
            {
                _macroDatabasePayloadCache.Dispose();
                UnregisterSidecar(ref _macroDatabasePayloadCacheSentinelId, nameof(_macroDatabasePayloadCache));
            }

            _macroDatabasePayloadBytes = 0L;
            _macroDatabasePayloadEvictions = 0;
            _macroDatabaseCacheAccessClock = 0u;
        }

        private void RegisterNativeSidecarStorage()
        {
            try
            {
                RegisterCoreSidecarSentinels();
                RegisterMacroDatabasePayloadCacheSentinels();
            }
            catch
            {
                UnregisterNativeSidecarStorage();
                throw;
            }
        }

        private void RegisterCoreSidecarSentinels()
        {
            if (_buffersSentinelId <= 0)
            {
                _buffersSentinelId = RequireSentinelRegistration(
                    RegisterUnsafeHashMapSidecar(
                        _buffers,
                        nameof(_buffers)),
                    nameof(_buffers));
            }

            if (_metadataSentinelId <= 0)
            {
                _metadataSentinelId = RequireSentinelRegistration(
                    RegisterUnsafeHashMapSidecar(
                        _metadata,
                        nameof(_metadata)),
                    nameof(_metadata));
            }

            if (_metadataGenerationByBufferIdSentinelId <= 0)
            {
                _metadataGenerationByBufferIdSentinelId = RequireSentinelRegistration(
                    RegisterUnsafeHashMapSidecar(
                        _metadataGenerationByBufferId,
                        nameof(_metadataGenerationByBufferId)),
                    nameof(_metadataGenerationByBufferId));
            }

            if (_keysSentinelId <= 0)
            {
                _keysSentinelId = RequireSentinelRegistration(
                    RegisterNativeListSidecar(
                        _keys,
                        nameof(_keys)),
                    nameof(_keys));
            }

            if (_blocksSentinelId <= 0)
            {
                _blocksSentinelId = RequireSentinelRegistration(
                    RegisterNativeListSidecar(
                        _blocks,
                        nameof(_blocks)),
                    nameof(_blocks));
            }
        }

        private void RegisterMacroDatabasePayloadCacheSentinels()
        {
            if (_macroDatabasePayloadCacheSentinelId <= 0)
            {
                _macroDatabasePayloadCacheSentinelId = RequireSentinelRegistration(
                    RegisterNativeParallelHashMapSidecar(
                        _macroDatabasePayloadCache,
                        nameof(_macroDatabasePayloadCache)),
                    nameof(_macroDatabasePayloadCache));
            }

            if (_macroDatabasePayloadAccessTicksSentinelId <= 0)
            {
                _macroDatabasePayloadAccessTicksSentinelId = RequireSentinelRegistration(
                    RegisterNativeParallelHashMapSidecar(
                        _macroDatabasePayloadAccessTicks,
                        nameof(_macroDatabasePayloadAccessTicks)),
                    nameof(_macroDatabasePayloadAccessTicks));
            }

            if (_macroDatabasePayloadKeysSentinelId <= 0)
            {
                _macroDatabasePayloadKeysSentinelId = RequireSentinelRegistration(
                    RegisterNativeListSidecar(
                        _macroDatabasePayloadKeys,
                        nameof(_macroDatabasePayloadKeys)),
                    nameof(_macroDatabasePayloadKeys));
            }
        }

        private static int RequireSentinelRegistration(int sentinelId, string label)
        {
            if (sentinelId > 0)
                return sentinelId;

            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static int RegisterUnsafeHashMapSidecar<TKey, TValue>(
            UnsafeHashMap<TKey, TValue> map,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return 0;

            return NativeMemoryTrackingBridge.RegisterBytesInstance(
                EstimateNativeHashMapBytes<TKey, TValue>(map.Capacity),
                NativeMemoryOwner,
                label,
                NativeMemoryBridgeLifetime.Session);
        }

        private static int RegisterNativeParallelHashMapSidecar<TKey, TValue>(
            NativeParallelHashMap<TKey, TValue> map,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return 0;

            return NativeMemoryTrackingBridge.RegisterBytesInstance(
                EstimateNativeHashMapBytes<TKey, TValue>(map.Capacity),
                NativeMemoryOwner,
                label,
                NativeMemoryBridgeLifetime.Session);
        }

        private static int RegisterNativeListSidecar<T>(NativeList<T> list, string label)
            where T : unmanaged
        {
            if (!list.IsCreated)
                return 0;

            return NativeMemoryTrackingBridge.RegisterBytesInstance(
                (long)UnsafeUtility.SizeOf<T>() * Math.Max(1, list.Capacity),
                NativeMemoryOwner,
                label,
                NativeMemoryBridgeLifetime.Session);
        }

        private static void RefreshNativeParallelHashMapSidecar<TKey, TValue>(
            NativeParallelHashMap<TKey, TValue> map,
            ref int sentinelId,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            UnregisterSidecar(ref sentinelId, label);
            sentinelId = RequireSentinelRegistration(
                RegisterNativeParallelHashMapSidecar(map, label),
                label);
        }

        private static void RefreshNativeListSidecar<T>(
            NativeList<T> list,
            ref int sentinelId,
            string label)
            where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            UnregisterSidecar(ref sentinelId, label);
            sentinelId = RequireSentinelRegistration(
                RegisterNativeListSidecar(list, label),
                label);
        }

        private static void UnregisterSidecar(ref int sentinelId, string label)
        {
            if (sentinelId <= 0)
                return;

            NativeMemoryTrackingBridge.Unregister(sentinelId);
            sentinelId = 0;
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

        private void RefreshMacroDatabasePayloadCacheSentinels()
        {
            RefreshNativeParallelHashMapSidecar(
                _macroDatabasePayloadCache,
                ref _macroDatabasePayloadCacheSentinelId,
                nameof(_macroDatabasePayloadCache));
            RefreshNativeParallelHashMapSidecar(
                _macroDatabasePayloadAccessTicks,
                ref _macroDatabasePayloadAccessTicksSentinelId,
                nameof(_macroDatabasePayloadAccessTicks));
            RefreshNativeListSidecar(
                _macroDatabasePayloadKeys,
                ref _macroDatabasePayloadKeysSentinelId,
                nameof(_macroDatabasePayloadKeys));
        }

        private void UnregisterNativeSidecarStorage()
        {
            UnregisterSidecar(ref _macroDatabasePayloadKeysSentinelId, nameof(_macroDatabasePayloadKeys));
            UnregisterSidecar(ref _macroDatabasePayloadAccessTicksSentinelId, nameof(_macroDatabasePayloadAccessTicks));
            UnregisterSidecar(ref _macroDatabasePayloadCacheSentinelId, nameof(_macroDatabasePayloadCache));
            UnregisterSidecar(ref _blocksSentinelId, nameof(_blocks));
            UnregisterSidecar(ref _keysSentinelId, nameof(_keys));
            UnregisterSidecar(ref _metadataGenerationByBufferIdSentinelId, nameof(_metadataGenerationByBufferId));
            UnregisterSidecar(ref _metadataSentinelId, nameof(_metadata));
            UnregisterSidecar(ref _buffersSentinelId, nameof(_buffers));
            _macroDatabasePayloadKeysSentinelId = 0;
            _macroDatabasePayloadAccessTicksSentinelId = 0;
            _macroDatabasePayloadCacheSentinelId = 0;
            _blocksSentinelId = 0;
            _keysSentinelId = 0;
            _metadataGenerationByBufferIdSentinelId = 0;
            _metadataSentinelId = 0;
            _buffersSentinelId = 0;
        }

        private void RemoveMacroDatabaseKey(ulong sectorHash)
        {
            if (!_macroDatabasePayloadKeys.IsCreated)
                return;

            for (int i = _macroDatabasePayloadKeys.Length - 1; i >= 0; i--)
            {
                if (_macroDatabasePayloadKeys[i] != sectorHash)
                    continue;

                _macroDatabasePayloadKeys.RemoveAtSwapBack(i);
            }
        }

        private bool EnsureMacroDatabaseKeyRegistered(ulong sectorHash)
        {
            if (!_macroDatabasePayloadKeys.IsCreated)
                return false;

            for (int i = 0; i < _macroDatabasePayloadKeys.Length; i++)
            {
                if (_macroDatabasePayloadKeys[i] == sectorHash)
                    return true;
            }

            if (_macroDatabasePayloadKeys.Length >= _macroDatabasePayloadKeys.Capacity)
                return false;

            try
            {
                _macroDatabasePayloadKeys.AddNoResize(sectorHash);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadFlatMetadata(int key, out VaultBufferMeta meta)
        {
            meta = default;
            if (_metadataByBufferId.IsCreated && (uint)key < (uint)_metadataByBufferId.Length)
            {
                meta = _metadataByBufferId[key];
                return meta.BufferKey == key && meta.Version != 0u && meta.Length > 0;
            }

            return _metadata.IsCreated &&
                _metadata.TryGetValue(key, out meta) &&
                meta.BufferKey == key &&
                meta.Version != 0u &&
                meta.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadMetadata(int key, out VaultBufferMeta meta)
        {
            if (TryReadFlatMetadata(key, out meta))
                return true;

            return _metadata.IsCreated && _metadata.TryGetValue(key, out meta);
        }

        private bool TryAddMetadata(int key, in VaultBufferMeta meta)
        {
            if (!_metadata.IsCreated)
                return false;

            VaultBufferMeta stored = meta;
            stored.BufferKey = key;
            if (!_metadata.TryAdd(key, stored))
                return false;

            WriteMetadataGeneration(key, stored.Version);
            WriteFlatMetadata(key, in stored);
            return true;
        }

        private void WriteMetadata(int key, in VaultBufferMeta meta)
        {
            if (!_metadata.IsCreated)
                return;

            VaultBufferMeta stored = meta;
            stored.BufferKey = key;
            _metadata[key] = stored;
            WriteMetadataGeneration(key, stored.Version);
            WriteFlatMetadata(key, in stored);
        }

        private void WriteFlatMetadata(int key, in VaultBufferMeta meta)
        {
            if (!_metadataByBufferId.IsCreated || (uint)key >= (uint)_metadataByBufferId.Length)
                return;

            _metadataByBufferId[key] = meta;
        }

        private void RemoveMetadata(int key)
        {
            uint tombstoneGeneration = ResolveTombstoneGeneration(key);
            if (_metadata.IsCreated)
                _metadata.Remove(key);
            WriteMetadataGeneration(key, tombstoneGeneration);
            ClearFlatMetadata(key, tombstoneGeneration);
        }

        private uint ResolveInitialGenerationForAllocation(int key)
        {
            uint previous = ReadMetadataGeneration(key);
            return previous == 0u ? 1u : NextGeneration(previous);
        }

        private uint ResolveTombstoneGeneration(int key)
        {
            uint previous = ReadMetadataGeneration(key);
            return previous == 0u ? 1u : NextGeneration(previous);
        }

        private uint ReadMetadataGeneration(int key)
        {
            if (_metadataByBufferId.IsCreated && (uint)key < (uint)_metadataByBufferId.Length)
                return _metadataByBufferId[key].Version;
            if (_metadataGenerationByBufferId.IsCreated &&
                _metadataGenerationByBufferId.TryGetValue(key, out uint generation))
            {
                return generation;
            }
            if (_metadata.IsCreated && _metadata.TryGetValue(key, out VaultBufferMeta meta))
                return meta.Version;

            return 0u;
        }

        private void WriteMetadataGeneration(int key, uint generation)
        {
            if (!_metadataGenerationByBufferId.IsCreated || key == 0 || generation == 0u)
                return;
            if (_metadataByBufferId.IsCreated && (uint)key < (uint)_metadataByBufferId.Length)
                return;

            if (_metadataGenerationByBufferId.TryGetValue(key, out _))
                _metadataGenerationByBufferId[key] = generation;
            else
                _metadataGenerationByBufferId.TryAdd(key, generation);
        }

        private void ClearFlatMetadata(int key, uint tombstoneGeneration)
        {
            if (!_metadataByBufferId.IsCreated || (uint)key >= (uint)_metadataByBufferId.Length)
                return;

            VaultBufferMeta tombstone = default;
            tombstone.BufferKey = -1;
            tombstone.Version = tombstoneGeneration;
            _metadataByBufferId[key] = tombstone;
        }

        private void RemoveBufferKey(int key)
        {
            if (!_keys.IsCreated)
                return;

            for (int i = _keys.Length - 1; i >= 0; i--)
            {
                if (_keys[i] != key)
                    continue;

                _keys.RemoveAtSwapBack(i);
            }
        }

        private bool EnsureBufferKeyRegistered(int key)
        {
            if (!_keys.IsCreated)
                return false;

            for (int i = 0; i < _keys.Length; i++)
            {
                if (_keys[i] == key)
                    return true;
            }

            if (_keys.Length >= _keys.Capacity)
                return false;

            try
            {
                _keys.AddNoResize(key);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int ReleaseBuffersByOwner(SystemID owner, bool sceneOwnedOnly, out long releasedBytes)
        {
            releasedBytes = 0L;
            if (!_keys.IsCreated || !_metadata.IsCreated || !_buffers.IsCreated)
                return 0;

            int releasedCount = 0;
            for (int i = _keys.Length - 1; i >= 0; i--)
            {
                int key = _keys[i];
                if (!_metadata.TryGetValue(key, out VaultBufferMeta meta))
                {
                    RemoveBufferKey(key);
                    DumpPhiVodBlackBox();
                    continue;
                }

                bool shouldRelease = sceneOwnedOnly
                    ? IsSceneOwnedVaultOwner(meta.Owner)
                    : meta.Owner == owner;
                if (!shouldRelease)
                    continue;

                if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
                {
                    DumpPhiVodBlackBox();
                    continue;
                }

                VaultArenaBlock block = _blocks[blockIndex];
                if ((block.Reserved0 & BlockFlagLocked) != 0 || block.Reserved1 != 0)
                {
                    _defragLockedSkipCount++;
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                    continue;
                }

                if (!TryFreeBlock(blockIndex, clearPayload: true))
                {
                    DumpPhiVodBlackBox();
                    continue;
                }

                releasedBytes += meta.Bytes;
                _buffers.Remove(key);
                RemoveMetadata(key);
                RemoveBufferKey(key);
                releasedCount++;
            }

            if (releasedCount > 0)
                _allocatedBytes = _allocatedBytes > releasedBytes ? _allocatedBytes - releasedBytes : 0L;

            return releasedCount;
        }

        private bool TryReleaseOrphanedBuffer(int key, in VaultBufferMeta meta, out long releasedBytes)
        {
            releasedBytes = 0L;
            if (!_metadata.IsCreated ||
                !_buffers.IsCreated ||
                key == 0 ||
                meta.RefCount == 0u ||
                meta.ActiveWriterSystemID != 0)
            {
                return false;
            }

            if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                DumpPhiVodBlackBox();
                return false;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if ((block.Reserved0 & (BlockFlagLocked | BlockFlagExternalView)) != 0 || block.Reserved1 != 0)
            {
                _defragLockedSkipCount++;
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                return false;
            }

            releasedBytes = meta.Bytes;
            if (!TryFreeBlockUnderOwnedFence(blockIndex, clearPayload: true))
            {
                DumpPhiVodBlackBox();
                return false;
            }

            _buffers.Remove(key);
            RemoveMetadata(key);
            RemoveBufferKey(key);
            return true;
        }

        private static bool IsSceneOwnedVaultOwner(SystemID owner)
        {
            switch (owner)
            {
                case SystemID.Unknown:
                case SystemID.CoreDataVault:
                case SystemID.H8Memory:
                case SystemID.Bootstrap:
                case SystemID.CoreDeterminism:
                case SystemID.SystemDispatcher:
                case SystemID.HardwareHomeostasis:
                case SystemID.GlobalPhysicsStateManager:
                case SystemID.Physics:
                    return false;
                default:
                    return true;
            }
        }

        private void TouchMacroDatabasePayload(ulong sectorHash)
        {
            if (!_macroDatabasePayloadAccessTicks.IsCreated)
                return;

            _macroDatabaseCacheAccessClock++;
            if (_macroDatabaseCacheAccessClock == 0u)
            {
                _macroDatabasePayloadAccessTicks.Clear();
                _macroDatabaseCacheAccessClock = 1u;
            }

            if (_macroDatabasePayloadAccessTicks.ContainsKey(sectorHash))
                _macroDatabasePayloadAccessTicks[sectorHash] = _macroDatabaseCacheAccessClock;
            else
                _macroDatabasePayloadAccessTicks.TryAdd(sectorHash, _macroDatabaseCacheAccessClock);
        }

        private static ulong MixMacroDatabasePayloadToken(ulong sectorHash, uint version)
        {
            ulong mixed = sectorHash ^ 0x9E3779B97F4A7C15UL;
            mixed ^= (ulong)version * 0xD6E8FEB86659FD93UL;
            mixed ^= mixed >> 32;
            mixed *= 0xA24BAED4963EE407UL;
            mixed ^= mixed >> 29;
            return mixed == 0UL ? 1UL : mixed;
        }

        private void SubtractMacroDatabasePayloadBytes(int byteLength)
        {
            if (byteLength <= 0)
                return;

            _macroDatabasePayloadBytes = _macroDatabasePayloadBytes > byteLength
                ? _macroDatabasePayloadBytes - byteLength
                : 0L;
        }

        private bool TryEvictLeastRecentlyUsedMacroDatabasePayload()
        {
            if (!_macroDatabasePayloadKeys.IsCreated ||
                !_macroDatabasePayloadCache.IsCreated ||
                _macroDatabasePayloadKeys.Length == 0)
            {
                return false;
            }

            int evictIndex = -1;
            uint oldestTick = uint.MaxValue;
            for (int i = 0; i < _macroDatabasePayloadKeys.Length; i++)
            {
                ulong candidateHash = _macroDatabasePayloadKeys[i];
                if (!_macroDatabasePayloadCache.TryGetValue(candidateHash, out MacroDatabasePayloadCacheEntry candidate))
                    continue;

                if ((candidate.Handle.Flags & MacroDatabasePayloadDirtyFlag) != 0)
                    continue;

                uint accessTick = 0u;
                if (_macroDatabasePayloadAccessTicks.IsCreated)
                    _macroDatabasePayloadAccessTicks.TryGetValue(candidateHash, out accessTick);

                if (evictIndex < 0 || accessTick < oldestTick)
                {
                    oldestTick = accessTick;
                    evictIndex = i;
                }
            }

            if (evictIndex < 0)
                return false;

            ulong sectorHash = _macroDatabasePayloadKeys[evictIndex];
            return TryRemoveMacroDatabasePayload(sectorHash, out _);
        }

        private void EnsureInitialized()
        {
            if (!_initialized && !_disposed)
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
            _defragLockedSkipCount = 0;
            _lastRelocationRecordCount = 0;
            _lastRelocatedSystemId = SystemID.Unknown;
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

            long totalFreeBytes = 0L;
            long largestFreeBytes = 0L;
            int unalignedOccupiedCount = 0;
            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State == BlockStateFree)
                {
                    totalFreeBytes += block.Bytes;
                    if (block.Bytes > largestFreeBytes)
                        largestFreeBytes = block.Bytes;
                    continue;
                }

                if (block.State == BlockStateOccupied &&
                    ((ulong)block.OffsetBytes & (ulong)(VaultBlockAlignment - 1)) != 0UL)
                {
                    unalignedOccupiedCount++;
                }
            }

            TotalFreeSpaceBytes = totalFreeBytes;
            LargestContiguousBlockBytes = largestFreeBytes;
            UnalignedBufferCount = unalignedOccupiedCount;
            HeapFragmentationRatio = totalFreeBytes > 0L
                ? math.saturate((float)((double)(totalFreeBytes - largestFreeBytes) / totalFreeBytes))
                : 0f;
            IsFragmented = HeapFragmentationRatio > ResolveFragmentationRatioThreshold();
            if (IsFragmented)
                LastDefragFlags |= DefragFlagFragmented;
            if (UnalignedBufferCount > 0)
                LastDefragFlags |= DefragFlagUnaligned;
        }

        private float ResolveFragmentationRatioThreshold()
        {
            float profile01 = ResolveArenaCapacityWeight01(_arenaCapacityLimitBytes);
            float curve01 = profile01 * profile01 * (3f - (2f * profile01));
            return math.lerp(MinimumQualityFragmentationRatioThreshold, MaximumQualityFragmentationRatioThreshold, curve01);
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

        private bool TryRunLiveCompactionSlice(uint activeBurstLockMask)
        {
            TryDrainDeferredReleaseRequests();
            if (_memMoveBlockedByStress ||
                Interlocked.Read(ref _allocationLock) != 0L ||
                Volatile.Read(ref _compactionFence) != 0 ||
                HasActiveBurstLocks(activeBurstLockMask) ||
                !_blocks.IsCreated ||
                _blocks.Length < 2 ||
                _arenaBase == null)
            {
                return false;
            }

            long movedBytes = 0L;
            bool movedAny = false;
            bool faulted = false;
            ResetRelocationRecords();
            if (Interlocked.Exchange(ref _compactionFence, 1) != 0)
                return false;
            try
            {
                Thread.MemoryBarrier();
                TryDrainDeferredReleaseRequests();
                if (HasActiveBurstLocks(activeBurstLockMask))
                {
                    _defragLockedSkipCount++;
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                    return false;
                }

                if (!TryAcquireBlockMutationGate())
                {
                    RecordLockContentionFault(0);
                    _defragLockedSkipCount++;
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                    return false;
                }

                try
                {
                    for (int i = 0; i + 1 < _blocks.Length && movedBytes < MaxLiveDefragMoveBytesPerSlice; i++)
                    {
                        if (HasActiveBurstLocks(activeBurstLockMask))
                        {
                            _defragLockedSkipCount++;
                            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                            break;
                        }

                        VaultArenaBlock freeBlock = _blocks[i];
                        if (freeBlock.State != BlockStateFree || freeBlock.Bytes <= 0L)
                            continue;

                        VaultArenaBlock occupiedBlock = _blocks[i + 1];
                        if (occupiedBlock.State != BlockStateOccupied)
                            continue;

                        if ((occupiedBlock.Reserved0 & BlockFlagLocked) != 0 || occupiedBlock.Reserved1 != 0)
                        {
                            _defragLockedSkipCount++;
                            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                            continue;
                        }

                        if ((occupiedBlock.Reserved0 & BlockFlagExternalView) != 0)
                        {
                            _defragLockedSkipCount++;
                            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                            continue;
                        }

                        if (occupiedBlock.Bytes > MaxLiveDefragMoveBytesPerSlice - movedBytes)
                        {
                            if (occupiedBlock.Bytes > PendingMassiveMoveBytes)
                                PendingMassiveMoveBytes = occupiedBlock.Bytes;
                            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagMassiveMovePending);
                            break;
                        }

                        if (!TryMoveOccupiedBlockLeft(i, i + 1, activeBurstLockMask, ref movedBytes))
                        {
                            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);
                            faulted = true;
                            break;
                        }

                        movedAny = true;
                        if (i > 0)
                            i--;
                    }
                }
                finally
                {
                    ReleaseBlockMutationGate();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _compactionFence, 0);
            }

            if (movedAny)
            {
                LastDefragMovedBytes = movedBytes;
                _totalDefragMovedBytes += movedBytes;
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagRelocated);
                BumpVaultGeneration();
            }

            if (faulted)
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);

            return movedAny;
        }

        private bool TryMoveOccupiedBlockLeft(int freeIndex, int occupiedIndex, uint activeBurstLockMask, ref long movedBytes)
        {
            if (HasActiveBurstLocks(activeBurstLockMask))
            {
                _defragLockedSkipCount++;
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                return false;
            }

            if ((uint)freeIndex >= (uint)_blocks.Length ||
                (uint)occupiedIndex >= (uint)_blocks.Length ||
                occupiedIndex != freeIndex + 1)
            {
                return false;
            }

            VaultArenaBlock freeBlock = _blocks[freeIndex];
            VaultArenaBlock occupiedBlock = _blocks[occupiedIndex];
            if (freeBlock.State != BlockStateFree ||
                occupiedBlock.State != BlockStateOccupied ||
                freeBlock.Bytes <= 0L ||
                occupiedBlock.Bytes <= 0L ||
                occupiedBlock.OffsetBytes != freeBlock.OffsetBytes + freeBlock.Bytes ||
                occupiedBlock.BufferKey == 0 ||
                occupiedBlock.Bytes > _arenaBytes - occupiedBlock.OffsetBytes)
            {
                return false;
            }

            if ((occupiedBlock.Reserved0 & (BlockFlagLocked | BlockFlagExternalView)) != 0 ||
                occupiedBlock.Reserved1 != 0)
            {
                _defragLockedSkipCount++;
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                return false;
            }

            if ((freeBlock.OffsetBytes & (VaultBlockAlignment - 1L)) != 0L ||
                (occupiedBlock.OffsetBytes & (VaultBlockAlignment - 1L)) != 0L ||
                (occupiedBlock.Bytes & (VaultBlockAlignment - 1L)) != 0L)
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                return false;
            }

            if ((ulong)freeBlock.OffsetBytes > uint.MaxValue ||
                (ulong)occupiedBlock.OffsetBytes > uint.MaxValue ||
                (ulong)occupiedBlock.Bytes > uint.MaxValue)
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);
                return false;
            }

            int key = occupiedBlock.BufferKey;
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                !_buffers.TryGetValue(key, out IntPtr oldAddress) ||
                oldAddress == IntPtr.Zero ||
                meta.BlockIndex != occupiedIndex ||
                meta.OffsetBytes != occupiedBlock.OffsetBytes ||
                meta.Bytes != occupiedBlock.Bytes)
            {
                return false;
            }

            IntPtr expectedOldAddress = (IntPtr)((byte*)_arenaBase + occupiedBlock.OffsetBytes);
            if (oldAddress != expectedOldAddress)
                return false;

            IntPtr newAddress = (IntPtr)((byte*)_arenaBase + freeBlock.OffsetBytes);
            if (HasActiveBurstLocks(activeBurstLockMask))
            {
                _defragLockedSkipCount++;
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                return false;
            }

            UnsafeUtility.MemMove(newAddress.ToPointer(), oldAddress.ToPointer(), occupiedBlock.Bytes);

            VaultArenaBlock movedBlock = occupiedBlock;
            movedBlock.OffsetBytes = freeBlock.OffsetBytes;
            movedBlock.Version = NextGeneration(occupiedBlock.Version);

            VaultArenaBlock newFreeBlock = freeBlock;
            newFreeBlock.OffsetBytes = movedBlock.OffsetBytes + movedBlock.Bytes;
            newFreeBlock.Bytes = freeBlock.Bytes;
            newFreeBlock.BufferKey = 0;
            newFreeBlock.State = BlockStateFree;
            newFreeBlock.Reserved0 = 0;
            newFreeBlock.Reserved1 = 0;
            newFreeBlock.Version = NextGeneration(freeBlock.Version);

            _blocks[freeIndex] = movedBlock;
            _blocks[occupiedIndex] = newFreeBlock;
            UpdateH8Descriptor(in movedBlock);
            UpdateH8Descriptor(in newFreeBlock);

            meta.BlockIndex = freeIndex;
            meta.OffsetBytes = movedBlock.OffsetBytes;
            // meta.Version (buffer GENERATION) is deliberately PRESERVED here - see the invariant documented
            // on RebuildMetadataBlockIndices. The payload really did move, but the move is TRANSPARENT to an
            // outstanding handle: TryResolveHandle/TryReadHandle/TryReadOnlyHandle recompute the pointer as
            // _arenaBase + meta.OffsetBytes, and meta.OffsetBytes is updated on the line above, so a preserved
            // handle resolves to the relocated payload. Nobody holding a raw alias can be affected, because
            // this method refuses to move any block carrying BlockFlagExternalView or BlockFlagLocked (the
            // write-lock flag), and concurrent readers are excluded by _compactionFence, which
            // TryRunLiveCompactionSlice holds across the whole slice.
            //
            // Bumping the generation here - by ANY mechanism - would permanently kill every outstanding handle
            // to a defragged buffer, because nothing hands the new generation back to a consumer:
            // RecordRelocation only fills a fixed-size telemetry ring, and handle resolution never reads
            // _buffers, so PublishMovedBufferPointer below is not a notification either. That is the same
            // failure shape as the RebuildMetadataBlockIndices defect.
            WriteMetadata(key, in meta);
            PublishMovedBufferPointer(key, newAddress);
            RecordRelocation(key, in meta, oldAddress, newAddress, occupiedBlock.Bytes, movedBlock.Version);
            movedBytes += occupiedBlock.Bytes;

            if (occupiedIndex + 1 < _blocks.Length && IsFree(occupiedIndex) && IsFree(occupiedIndex + 1))
                MergeFreeBlocks(occupiedIndex, occupiedIndex + 1);

            return true;
        }

        private bool MarkExternalView(int key, long offsetBytes)
        {
            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(key);
                return false;
            }

            try
            {
                return MarkExternalViewLocked(key, offsetBytes);
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool MarkExternalViewLocked(int key, long offsetBytes)
        {
            if (!TryFindOccupiedBlockIndex(key, offsetBytes, out int blockIndex))
                return false;

            VaultArenaBlock block = _blocks[blockIndex];
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta))
                return false;

            if ((block.Reserved0 & BlockFlagExternalView) != 0)
            {
                // Re-marking an already-published external view is an INDEX/OFFSET reconciliation only: no
                // payload moves, so meta.Version (buffer GENERATION) must be preserved. The dirty check also
                // no longer compares meta.Version against block.Version - those two counters are unequal BY
                // CONSTRUCTION (see RebuildMetadataBlockIndices), so that term was always true and made this
                // branch rewrite metadata and bump the vault generation on every repeat call.
                if (meta.BlockIndex != blockIndex ||
                    meta.OffsetBytes != block.OffsetBytes)
                {
                    meta.BlockIndex = blockIndex;
                    meta.OffsetBytes = block.OffsetBytes;
                    WriteMetadata(key, in meta);
                    BumpVaultGeneration();
                }

                return true;
            }

            block.Reserved0 |= BlockFlagExternalView;
            block.Version = NextGeneration(block.Version);
            _blocks[blockIndex] = block;
            UpdateH8Descriptor(in block);

            meta.BlockIndex = blockIndex;
            meta.OffsetBytes = block.OffsetBytes;
            // Publishing an external view sets a block FLAG. The payload does not move and the offset does not
            // change, so meta.Version (buffer GENERATION) is preserved; the block-mutation counter bumped above
            // is the correct place to record the flag change. Invalidating handles here would break the very
            // buffer the caller is aliasing.
            WriteMetadata(key, in meta);
            BumpVaultGeneration();
            return true;
        }

        private bool MarkAliasReader(int key, SystemID requester)
        {
            if (requester == SystemID.Unknown)
                return false;

            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(key);
                return false;
            }

            try
            {
                return MarkAliasReaderLocked(key, requester);
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool MarkAliasReaderLocked(int key, SystemID requester)
        {
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                return false;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if ((block.Reserved0 & BlockFlagExternalView) == 0)
                return false;

            meta.LastAliasRequester = requester;
            WriteMetadata(key, in meta);
            return true;
        }

        private void RollbackAliasPublicationLocked(
            int key,
            long offsetBytes,
            bool hadExternalView,
            SystemID previousAliasRequester)
        {
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, offsetBytes, out int blockIndex))
            {
                return;
            }

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.BufferKey != key || block.OffsetBytes != offsetBytes)
                return;

            if (!hadExternalView && (block.Reserved0 & BlockFlagExternalView) != 0)
            {
                block.Reserved0 &= unchecked((byte)~BlockFlagExternalView);
                block.Version = NextGeneration(block.Version);
                _blocks[blockIndex] = block;
                UpdateH8Descriptor(in block);
                meta.BlockIndex = blockIndex;
                meta.OffsetBytes = block.OffsetBytes;
                // Rolling back an alias publication CLEARS a block flag. No payload moves and the offset is
                // unchanged, so meta.Version (buffer GENERATION) is preserved. This path exists to undo a
                // failed publication; invalidating the owner's handle would turn a clean rollback into a
                // permanently unreadable buffer.
                BumpVaultGeneration();
            }

            meta.LastAliasRequester = previousAliasRequester;
            WriteMetadata(key, in meta);
        }

        private void RecordDefragBlackBox(uint sequence, byte extraFlags = 0)
        {
            if (!_defragBlackBox.IsCreated ||
                !_defragBlackBoxDetails.IsCreated ||
                _defragBlackBox.Length == 0 ||
                _defragBlackBoxDetails.Length < _defragBlackBox.Length)
                return;

            int cursor = Volatile.Read(ref _defragBlackBoxCursor);
            if ((uint)cursor >= (uint)_defragBlackBox.Length)
                cursor = 0;

            MemoryDefragTelemetryEntry entry = default;
            entry.Sequence = sequence;
            entry.Frame = H8Memory.ResolveTelemetryFrame(sequence);
            entry.VaultGenerationID = _vaultGenerationId;
            entry.ActiveBurstLockMask = ActiveBurstLockMask;
            entry.ActiveMutationGuardMask = ActiveMutationGuardMask;
            entry.TotalFreeSpaceBytes = TotalFreeSpaceBytes;
            entry.LargestContiguousBlockBytes = LargestContiguousBlockBytes;
            entry.LastMovedBytes = LastDefragMovedBytes;
            entry.TotalMovedBytes = _totalDefragMovedBytes;
            entry.PendingMassiveMoveBytes = PendingMassiveMoveBytes;

            MemoryDefragTelemetryDetailEntry detail = default;
            detail.BlockCount = _blocks.IsCreated ? _blocks.Length : 0;
            detail.ActiveBufferCount = _keys.IsCreated ? _keys.Length : 0;
            detail.HeapFragmentationRatio = HeapFragmentationRatio;
            detail.WatchdogBreaches = _compactionWatchdogBreachCount;
            detail.LockedSkipCount = Volatile.Read(ref _defragLockedSkipCount);
            detail.LastRelocatedSystemId = (ushort)_lastRelocatedSystemId;
            detail.Flags = (byte)(LastDefragFlags | extraFlags);
            detail.IsFragmented = IsFragmented ? (byte)1 : (byte)0;
            detail.WatchdogExceeded = LastDefragWatchdogExceeded ? (byte)1 : (byte)0;
            detail.MemoryStarvationWarnings = _memoryStarvationWarnings;
            detail.GenerationMismatchCount = unchecked((uint)Volatile.Read(ref _generationHandleMissCount));
            detail.ResolutionTicks = Volatile.Read(ref _resolutionTickAccumulator);
            detail.ResolvedHandleCount = Volatile.Read(ref _resolvedHandleCount);
            detail.LastFaultBufferID = Volatile.Read(ref _lastFaultBufferId);
            detail.LastFaultHandleGeneration = _lastFaultHandleGeneration;
            detail.LastFaultMetaGeneration = _lastFaultMetaGeneration;
            detail.Reserved32 =
                ((uint)(ushort)Volatile.Read(ref _lastOrphanSweepCandidateCount) << 16) |
                (ushort)Volatile.Read(ref _lastOrphanReclaimCount);
            _defragBlackBox[cursor] = entry;
            _defragBlackBoxDetails[cursor] = detail;

            cursor++;
            if (cursor >= _defragBlackBox.Length)
                cursor = 0;
            Thread.MemoryBarrier();
            Volatile.Write(ref _defragBlackBoxCursor, cursor);
            int recordedCount;
            do
            {
                recordedCount = Volatile.Read(ref _defragBlackBoxRecordedCount);
                if (recordedCount >= _defragBlackBox.Length)
                    break;
            }
            while (Interlocked.CompareExchange(ref _defragBlackBoxRecordedCount, recordedCount + 1, recordedCount) != recordedCount);
        }

        private void DumpDefragBlackBox()
        {
            if (_defragDumpWritten || !_defragBlackBox.IsCreated || !_defragBlackBoxDetails.IsCreated)
                return;

            if (CommitDefragBlackBoxInMemory())
            {
                _defragDumpWritten = true;
                RequestMemorySentryDump();
            }
        }

        private void DumpPhiVodBlackBox()
        {
            if (_phiVodDumpWritten || !_defragBlackBox.IsCreated || !_defragBlackBoxDetails.IsCreated)
                return;

            if (CommitDefragBlackBoxInMemory())
            {
                _phiVodDumpWritten = true;
                RequestMemorySentryDump();
            }
        }

        private void DumpShinobu202BlackBox()
        {
            if (_shinobu202DumpWritten || !_defragBlackBox.IsCreated || !_defragBlackBoxDetails.IsCreated)
                return;

            if (CommitDefragBlackBoxInMemory())
            {
                _shinobu202DumpWritten = true;
                RequestMemorySentryDump();
            }
        }

        private void RequestMemorySentryDump()
        {
            if (!_defragBlackBox.IsCreated ||
                !_defragBlackBoxDetails.IsCreated ||
                Volatile.Read(ref _memorySentryDumpWritten) != 0 ||
                Interlocked.CompareExchange(ref _memorySentryDumpInFlight, 1, 0) != 0)
            {
                return;
            }

            Volatile.Write(ref _memorySentryDumpRequested, 1);
            try
            {
                if (CommitDefragBlackBoxInMemory())
                    Interlocked.Exchange(ref _memorySentryDumpWritten, 1);
            }
            finally
            {
                Interlocked.Exchange(ref _memorySentryDumpRequested, 0);
                Interlocked.Exchange(ref _memorySentryDumpInFlight, 0);
            }
        }

        private bool CommitDefragBlackBoxInMemory()
        {
            if (!_defragBlackBox.IsCreated || !_defragBlackBoxDetails.IsCreated)
                return false;

            int entrySize = UnsafeUtility.SizeOf<MemoryDefragTelemetryEntry>();
            int detailSize = UnsafeUtility.SizeOf<MemoryDefragTelemetryDetailEntry>();
            int capacity = math.min(_defragBlackBox.Length, _defragBlackBoxDetails.Length);
            int recordedCount = Volatile.Read(ref _defragBlackBoxRecordedCount);
            if (recordedCount < 0)
                recordedCount = 0;
            if (recordedCount > capacity)
                recordedCount = capacity;

            return capacity > 0 &&
                   recordedCount >= 0 &&
                   entrySize == MemoryDefragTelemetryEntrySizeBytes &&
                   detailSize == MemoryDefragTelemetryDetailEntrySizeBytes;
        }

        private bool TryBuildGenerationHandle<T>(BufferID bufferId, out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            int key = (int)bufferId;
            if (key == 0 || !TryReadMetadata(key, out VaultBufferMeta meta) || meta.Length <= 0)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            if (!ValidateType<T>(bufferId, meta, stride, alignment))
            {
                DumpPhiVodBlackBox();
                return false;
            }
            handle.BufferID = unchecked((uint)key);
            handle.SystemID = (uint)meta.Owner;
            handle.Generation = meta.Version;
            handle.Flags = meta.Flags;
            return true;
        }

        private void RecordGenerationFault(int key, uint handleGeneration, uint metaGeneration)
        {
            if (key < 0)
                key = 0;

            Interlocked.Increment(ref _generationHandleMissCount);
            Volatile.Write(ref _lastFaultBufferId, key);
            _lastFaultHandleGeneration = handleGeneration;
            _lastFaultMetaGeneration = metaGeneration;
            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);
            RecordDefragBlackBox(++_defragTickSequence, DefragFlagFault);
            DumpShinobu202BlackBox();
        }

        private void RecordLockContentionFault(int key)
        {
            if (key < 0)
                key = 0;

            Interlocked.Increment(ref _defragLockedSkipCount);
            Volatile.Write(ref _lastFaultBufferId, key);
            _lastFaultHandleGeneration = 0u;
            _lastFaultMetaGeneration = 0u;
            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
        }

        private void RecordMutationGuardContentionFault(ulong writeMask)
        {
            uint foldedMask = unchecked((uint)writeMask ^ (uint)(writeMask >> 32));
            int key = unchecked((int)(foldedMask & 0x7fffffffu));
            if (key == 0 && writeMask != 0UL)
                key = int.MaxValue;
            RecordLockContentionFault(key);
        }

        // Records a guard refusal as the BUFFER that blocked it instead of the folded mask.
        //
        // WHY. RecordMutationGuardContentionFault stamps fold32(writeMask), which is a per-owner CONSTANT:
        // every InputDispatcher guard refusal in Logs/h8_probe7.log stamped 2130706479 == 0x7F00002F ==
        // fold(InputOwnerMutationGuardMask) & 0x7fffffff. That identifies the owner and says nothing about
        // what blocked it, and 1240 refusals in that run all carry the same value. This vault CANNOT log -
        // Hecton8.Core.Memory.asmdef does not reference Hecton8.Core, so H8Debug is out of assembly - so the
        // buffer id goes into the one channel the probe route already prints:
        // MemoryDefragTelemetryDetailEntry.LastFaultBufferID, surfaced by SaveManager as
        // "vaultLastFaultBufferId=" on the SAVEVAULT_REFUSAL line and by the editor VaultXRayWindow.
        //
        // The folded-mask fallback is deliberately kept for the no-match case, so the stamped value stays
        // self-describing: a plausible BufferID means a real lock conflict and names the culprit, while
        // 0x7F00002F-shaped values mean the refusal came from one of the other branches - compaction fence,
        // contended block mutation gate, guard bits already held, or a lost CAS.
        private void RecordMutationGuardLockConflictFault(ulong writeMask)
        {
            int conflictBufferKey = FindGuardConflictBufferKey(writeMask);
            if (conflictBufferKey != 0)
            {
                RecordLockContentionFault(conflictBufferKey);
                return;
            }

            RecordMutationGuardContentionFault(writeMask);
        }

        // Refusal path only, and the only way to get from a guard bit back to a buffer id, because the mask
        // does not carry ids. Cold by construction: it runs after a guard has already been refused. Callers
        // must hold the block mutation gate so the block read is stable.
        private int FindGuardConflictBufferKey(ulong writeMask)
        {
            if (writeMask == 0UL || !_blocks.IsCreated)
                return 0;

            // Must resolve the culprit under the SAME convention the refusal was decided under, or it names a
            // buffer that did not block anything. HasActiveLockConflictForMutationMask uses the strict
            // (id & 63) shadow when the mask carries a high bit; matching that here keeps the telemetry
            // honest, since the union test would happily return the first buffer whose (id & 31) candidate
            // overlaps - a buffer the caller never named.
            bool strictConvention = unchecked((int)(uint)(writeMask >> 32)) != 0;

            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State != BlockStateOccupied ||
                    ((block.Reserved0 & BlockFlagLocked) == 0 && block.Reserved1 == 0))
                {
                    continue;
                }

                ulong blockGuardBits = strictConvention
                    ? ResolveGuard64LockBit(block.BufferKey)
                    : ResolveGuardLockBits(block.BufferKey);
                if ((blockGuardBits & writeMask) != 0UL)
                    return block.BufferKey;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeTypeHash<T>() where T : struct
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)UnsafeUtility.SizeOf<T>()) * 16777619u;
                hash = (hash ^ (uint)UnsafeUtility.AlignOf<T>()) * 16777619u;
                hash = (hash ^ (uint)typeof(T).TypeHandle.Value.ToInt64()) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        private static uint NextGeneration(uint generation)
        {
            uint next = generation + 1u;
            return next == 0u ? 1u : next;
        }

        private static int ResolveBufferCapacity(int capacity)
        {
            if (capacity <= 0)
                return DefaultBufferCapacity;

            return capacity > MaxBufferCapacity ? MaxBufferCapacity : capacity;
        }

        private static int ResolveBlockCapacity(int bufferCapacity)
        {
            if (bufferCapacity <= 0)
                return DefaultBufferCapacity << 1;

            int blockCapacity = bufferCapacity << 1;
            if (blockCapacity < bufferCapacity || blockCapacity > MaxBlockCapacity)
                return MaxBlockCapacity;

            return blockCapacity;
        }

        public static long ResolveArenaCapacityLimit(byte scalabilityProfile)
        {
            float profile01 = DecodeScalabilityProfile01(scalabilityProfile);
            float curve01 = profile01 * profile01 * (3f - (2f * profile01));
            double bytes = MinimumQualityArenaLimitBytes +
                           ((double)(MaximumQualityArenaLimitBytes - MinimumQualityArenaLimitBytes) * curve01);
            return AlignUp((long)math.round(bytes), VaultBlockAlignment);
        }

        public static float DecodeScalabilityProfile01(byte scalabilityProfile)
        {
            return scalabilityProfile <= 3
                ? math.saturate(scalabilityProfile * (1f / 3f))
                : math.saturate(scalabilityProfile * (1f / byte.MaxValue));
        }

        private static float ResolveArenaCapacityWeight01(long arenaCapacityLimitBytes)
        {
            double range = MaximumQualityArenaLimitBytes - MinimumQualityArenaLimitBytes;
            if (range <= 0.0d)
                return 0f;

            return math.saturate((float)((arenaCapacityLimitBytes - MinimumQualityArenaLimitBytes) / range));
        }

        private static long ResolveArenaCapacityLimit(long requestedLimitBytes)
        {
            long safeLimit = requestedLimitBytes;
            if (safeLimit <= 0L)
                safeLimit = MinimumQualityArenaLimitBytes;
            safeLimit = AlignUp(safeLimit, VaultBlockAlignment);
            if (safeLimit < DefaultArenaBytes)
                return AlignUp(DefaultArenaBytes, VaultBlockAlignment);
            return Math.Min(safeLimit, MaximumQualityArenaLimitBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BumpVaultGeneration()
        {
            _vaultGenerationId = NextGeneration(_vaultGenerationId);
        }

        private bool TryGrowArenaForBytes(long requiredContiguousBytes)
        {
            if (requiredContiguousBytes <= 0L ||
                _arenaBase == null ||
                !_blocks.IsCreated ||
                _arenaBytes >= _arenaCapacityLimitBytes)
            {
                return false;
            }

            if (Interlocked.Read(ref _allocationLock) != 0L || Volatile.Read(ref _compactionFence) != 0)
            {
                QueueDeferredArenaGrowth(requiredContiguousBytes);
                return false;
            }

            TryDrainDeferredReleaseRequests();
            if (HasActiveBurstLocks(0u) || HasPinnedExternalViews())
            {
                QueueDeferredArenaGrowth(requiredContiguousBytes);
                return false;
            }

            long deferredBytes = Volatile.Read(ref _deferredArenaGrowthBytes);
            if (deferredBytes > requiredContiguousBytes)
                requiredContiguousBytes = deferredBytes;

            long desiredMinimum = _allocatedBytes + requiredContiguousBytes + ArenaGrowSlackBytes;
            if (desiredMinimum < requiredContiguousBytes)
                desiredMinimum = requiredContiguousBytes;

            long doubled = _arenaBytes <= MaximumQualityArenaLimitBytes / 2L
                ? _arenaBytes << 1
                : _arenaCapacityLimitBytes;
            long desiredBytes = math.max(doubled, desiredMinimum);
            desiredBytes = AlignUp(desiredBytes, VaultBlockAlignment);
            if (desiredBytes > _arenaCapacityLimitBytes)
                desiredBytes = _arenaCapacityLimitBytes;
            if (desiredBytes <= _arenaBytes)
                return false;

            TryDrainDeferredReleaseRequests();
            if (Volatile.Read(ref _compactionFence) != 0 || HasActiveBurstLocks(0u) || HasPinnedExternalViews())
            {
                QueueDeferredArenaGrowth(requiredContiguousBytes);
                return false;
            }

            return TryGrowArena(desiredBytes);
        }

        private bool TryGrowArena(long newArenaBytes)
        {
            newArenaBytes = AlignUp(newArenaBytes, VaultBlockAlignment);
            if (newArenaBytes <= _arenaBytes || newArenaBytes > _arenaCapacityLimitBytes)
                return false;
            TryDrainDeferredReleaseRequests();
            if (Volatile.Read(ref _compactionFence) != 0 || HasActiveBurstLocks(0u) || HasPinnedExternalViews())
            {
                QueueDeferredArenaGrowth(newArenaBytes - _arenaBytes);
                return false;
            }
            if (_memMoveBlockedByStress)
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagStressHalt);
                return false;
            }

            bool fenceRaised = false;
            bool gateAcquired = false;
            bool reservedTailDescriptorCommitted = false;
            int reservedTailH8BlockIndex = -1;
            if (Interlocked.CompareExchange(ref _compactionFence, 1, 0) != 0)
            {
                QueueDeferredArenaGrowth(newArenaBytes - _arenaBytes);
                return false;
            }

            fenceRaised = true;
            Interlocked.Exchange(ref _arenaGrowthInProgress, 1);
            try
            {
                Thread.MemoryBarrier();
                TryDrainDeferredReleaseRequests();
                if (HasActiveBurstLocks(0u) || HasPinnedExternalViews())
                {
                    QueueDeferredArenaGrowth(newArenaBytes - _arenaBytes);
                    return false;
                }

                if (!TryAcquireBlockMutationGate())
                {
                    RecordLockContentionFault(0);
                    QueueDeferredArenaGrowth(newArenaBytes - _arenaBytes);
                    return false;
                }

                gateAcquired = true;
                DrainDeferredReleaseRequestsLocked();
                if (HasActiveBurstLocks(0u) || HasPinnedExternalViews())
                {
                    QueueDeferredArenaGrowth(newArenaBytes - _arenaBytes);
                    return false;
                }

                long targetGrowBytes = newArenaBytes - _arenaBytes;
                if (!TryPrepareArenaGrowthTailMetadata(out reservedTailH8BlockIndex))
                {
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);
                    QueueDeferredArenaGrowth(targetGrowBytes);
                    return false;
                }

                void* oldBase = _arenaBase;
                long oldArenaBytes = _arenaBytes;
                H8RawReallocationGuard relocationGuard = H8RawReallocationGuard.Create(
                    Volatile.Read(ref _compactionFence) != 0,
                    ActiveBurstLockMask,
                    HasPinnedExternalViews());
                void* newBase = H8Memory.ReallocateRaw(
                    oldBase,
                    oldArenaBytes,
                    newArenaBytes,
                    VaultBlockAlignment,
                    SystemID.CoreDataVault,
                    Allocator.Persistent,
                    true,
                    in relocationGuard,
                    H8AllocationFlags.Vault | H8AllocationFlags.SubAllocatorRoot);
                if (newBase == null)
                    return false;

                _arenaBase = newBase;
                _arenaBytes = newArenaBytes;
                long growBytes = newArenaBytes - oldArenaBytes;
                ResetRelocationRecords();
                RefreshBlocksAfterArenaRelocation(oldBase, newBase);
                if (!ExtendFreeTail(growBytes, reservedTailH8BlockIndex))
                {
                    LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);
                    DumpPhiVodBlackBox();
                    return false;
                }

                reservedTailDescriptorCommitted = reservedTailH8BlockIndex >= 0;
                LastDefragMovedBytes = oldArenaBytes;
                _totalDefragMovedBytes += oldArenaBytes;
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagRelocated);
                ClearDeferredArenaGrowthIfSatisfied();
                BumpVaultGeneration();
                return true;
            }
            finally
            {
                if (reservedTailH8BlockIndex >= 0 && !reservedTailDescriptorCommitted)
                    H8Memory.ReleaseReservedBlockDescriptor(reservedTailH8BlockIndex);
                if (gateAcquired)
                    ReleaseBlockMutationGate();
                Interlocked.Exchange(ref _arenaGrowthInProgress, 0);
                if (fenceRaised)
                    Interlocked.Exchange(ref _compactionFence, 0);
            }
        }

        public bool ProcessDeferredArenaGrowth()
        {
            long requiredBytes = Volatile.Read(ref _deferredArenaGrowthBytes);
            if (requiredBytes <= 0L)
                return false;
            if (!_initialized || _arenaBase == null || !_blocks.IsCreated)
                return false;

            if (CanSatisfyContiguousFreeBlock(requiredBytes))
            {
                ClearDeferredArenaGrowthIfSatisfied();
                return Volatile.Read(ref _deferredArenaGrowthBytes) <= 0L;
            }

            if (Interlocked.Read(ref _allocationLock) != 0L ||
                Volatile.Read(ref _arenaGrowthInProgress) != 0 ||
                Volatile.Read(ref _compactionFence) != 0)
            {
                RecordDefragBlackBox(++_defragTickSequence, DefragFlagAliasBlocked);
                return false;
            }

            TryDrainDeferredReleaseRequests();
            if (HasActiveBurstLocks(0u) ||
                HasPinnedExternalViews())
            {
                RecordDefragBlackBox(++_defragTickSequence, DefragFlagAliasBlocked);
                return false;
            }

            return TryGrowArenaForBytes(requiredBytes);
        }

        private void QueueDeferredArenaGrowth(long requiredBytes)
        {
            if (requiredBytes <= 0L)
                return;

            long observed;
            do
            {
                observed = Volatile.Read(ref _deferredArenaGrowthBytes);
                if (observed >= requiredBytes)
                    break;
            }
            while (Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, requiredBytes, observed) != observed);

            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
            _memoryStarvationWarnings = (byte)(_memoryStarvationWarnings | 1);
            RecordDefragBlackBox(++_defragTickSequence, DefragFlagAliasBlocked);
        }

        private void ClearDeferredArenaGrowthIfSatisfied()
        {
            long observed;
            do
            {
                observed = Volatile.Read(ref _deferredArenaGrowthBytes);
                if (observed <= 0L)
                    return;
                if (!CanSatisfyContiguousFreeBlock(observed))
                    return;
            }
            while (Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, 0L, observed) != observed);
        }

        private bool CanSatisfyContiguousFreeBlock(long requiredBytes)
        {
            if (requiredBytes <= 0L || !_blocks.IsCreated)
                return false;

            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State == BlockStateFree && block.Bytes >= requiredBytes)
                    return true;
            }

            return false;
        }

        private bool TryPrepareArenaGrowthTailMetadata(out int reservedTailH8BlockIndex)
        {
            reservedTailH8BlockIndex = -1;
            if (!_blocks.IsCreated || _blocks.Length <= 0)
                return false;

            VaultArenaBlock last = _blocks[_blocks.Length - 1];
            if (last.State == BlockStateFree)
                return true;
            if (_blocks.Length >= _blocks.Capacity)
                return false;

            return H8Memory.TryReserveBlockDescriptorSlot(out reservedTailH8BlockIndex);
        }

        private bool ExtendFreeTail(long growBytes, int reservedTailH8BlockIndex)
        {
            if (growBytes <= 0L)
                return true;

            int lastIndex = _blocks.Length - 1;
            if (lastIndex < 0)
                return false;

            VaultArenaBlock last = _blocks[lastIndex];
            if (last.State == BlockStateFree)
            {
                last.Bytes += growBytes;
                last.Reserved0 = 0;
                last.Reserved1 = 0;
                last.Version = NextGeneration(last.Version);
                _blocks[lastIndex] = last;
                UpdateH8Descriptor(in last);
                return true;
            }

            if (_blocks.Length >= _blocks.Capacity)
                return false;

            VaultArenaBlock freeTail = default;
            freeTail.OffsetBytes = last.OffsetBytes + last.Bytes;
            freeTail.Bytes = growBytes;
            freeTail.BufferKey = 0;
            freeTail.Version = 1u;
            freeTail.State = BlockStateFree;
            freeTail.Reserved0 = 0;
            freeTail.Reserved1 = 0;
            int descriptorIndex = reservedTailH8BlockIndex;
            if (descriptorIndex >= 0)
            {
                if (!H8Memory.TryCommitReservedBlockDescriptor(descriptorIndex, BuildDescriptor(in freeTail)))
                    return false;
            }
            else
            {
                descriptorIndex = H8Memory.RegisterBlockDescriptor(BuildDescriptor(in freeTail));
                if (descriptorIndex < 0)
                    return false;
            }

            freeTail.H8BlockIndex = descriptorIndex;
            if (!TryAppendBlockNoResize(in freeTail))
            {
                ReleaseCommittedH8Descriptor(descriptorIndex);
                return false;
            }

            return true;
        }

        private void RefreshBlocksAfterArenaRelocation(void* oldBase, void* newBase)
        {
            if (oldBase == null || newBase == null || !_blocks.IsCreated)
                return;

            long oldBaseAddress = ((IntPtr)oldBase).ToInt64();
            long newBaseAddress = ((IntPtr)newBase).ToInt64();
            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State == BlockStateOccupied)
                {
                    IntPtr oldPointer = (IntPtr)(oldBaseAddress + block.OffsetBytes);
                    IntPtr newPointer = (IntPtr)(newBaseAddress + block.OffsetBytes);
                    block.Version = NextGeneration(block.Version);
                    _blocks[i] = block;

                    if (_metadata.TryGetValue(block.BufferKey, out VaultBufferMeta meta))
                    {
                        meta.BlockIndex = i;
                        meta.OffsetBytes = block.OffsetBytes;
                        // The ARENA BASE moved, but no buffer moved WITHIN the arena: block.OffsetBytes is not
                        // rewritten anywhere in this loop. Handle resolution is _arenaBase + meta.OffsetBytes
                        // and TryGrowArena assigns the new _arenaBase BEFORE calling this method, so every
                        // preserved handle resolves to the correct new address with no further action. This is
                        // an index fixup, exactly like RebuildMetadataBlockIndices, so meta.Version (buffer
                        // GENERATION) is preserved. The reallocation is also refused outright while
                        // HasPinnedExternalViews() is true, so no aliased raw pointer can be left dangling.
                        WriteMetadata(block.BufferKey, in meta);
                        PublishMovedBufferPointer(block.BufferKey, newPointer);
                        RecordRelocation(block.BufferKey, in meta, oldPointer, newPointer, block.Bytes, block.Version);
                    }
                }
                else
                {
                    _blocks[i] = block;
                }

                UpdateH8Descriptor(in block);
            }
        }

        private void RecordRelocation(
            int key,
            in VaultBufferMeta meta,
            IntPtr oldPointer,
            IntPtr newPointer,
            long bytes,
            uint generation)
        {
            if (!_lastRelocationRecords.IsCreated ||
                _lastRelocationRecordCount >= _lastRelocationRecords.Length)
            {
                return;
            }

            VaultRelocationRecord record = default;
            record.OldOffsetBytes = ResolveArenaOffsetBytes(oldPointer, meta.OffsetBytes);
            record.NewOffsetBytes = ResolveArenaOffsetBytes(newPointer, meta.OffsetBytes);
            record.BufferId = key;
            record.ByteLength = bytes > int.MaxValue ? int.MaxValue : (int)bytes;
            record.Generation = generation;
            record.Flags = oldPointer == newPointer
                ? (byte)0
                : VaultRelocationRecord.FlagAddressChanged;
            int ownerId = (int)meta.Owner;
            record.SystemId = ownerId > byte.MaxValue ? byte.MaxValue : (byte)ownerId;
            _lastRelocatedSystemId = meta.Owner;
            _lastRelocationRecords[_lastRelocationRecordCount++] = record;
        }

        private long ResolveArenaOffsetBytes(IntPtr pointer, long fallbackOffsetBytes)
        {
            if (pointer == IntPtr.Zero || _arenaBase == null)
                return math.max(0L, fallbackOffsetBytes);

            long offsetBytes = (long)((byte*)pointer.ToPointer() - (byte*)_arenaBase);
            return offsetBytes >= 0L && offsetBytes <= _arenaBytes
                ? offsetBytes
                : math.max(0L, fallbackOffsetBytes);
        }

        private void PublishMovedBufferPointer(int key, IntPtr newPointer)
        {
            Interlocked.Exchange(ref _lastPublishedPointerBits, newPointer.ToInt64());
            _buffers[key] = newPointer;
        }

        private void ResetRelocationRecords()
        {
            _lastRelocationRecordCount = 0;
            if (!_lastRelocationRecords.IsCreated)
                return;

            for (int i = 0; i < _lastRelocationRecords.Length; i++)
                _lastRelocationRecords[i] = default;
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
            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(key);
                return false;
            }

            try
            {
                return TryReallocateBlockLocked(
                    key,
                    existingMeta,
                    requiredLength,
                    requiredBytes,
                    clearExtendedBytes,
                    out resizedPointer,
                    out resizedMeta);
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool TryReallocateBlockLocked(
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
            if ((block.Reserved0 & BlockFlagExternalView) != 0)
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
                rightBlock.Reserved0 = 0;
                rightBlock.Reserved1 = 0;
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
            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(key);
                return false;
            }

            try
            {
                return TryAllocateBlockLocked(key, bytes, out blockIndex, out pointer);
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool TryAllocateBlockLocked(int key, long bytes, out int blockIndex, out IntPtr pointer)
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
                    block.Reserved1 = 0;
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
                occupiedBlock.Reserved1 = 0;
                occupiedBlock.Version = NextGeneration(occupiedBlock.Version);

                VaultArenaBlock freeRemainder = block;
                freeRemainder.OffsetBytes += bytes;
                freeRemainder.Bytes -= bytes;
                freeRemainder.BufferKey = 0;
                freeRemainder.State = BlockStateFree;
                freeRemainder.Reserved0 = 0;
                freeRemainder.Reserved1 = 0;
                freeRemainder.Version = NextGeneration(freeRemainder.Version);
                int remainderH8BlockIndex = H8Memory.RegisterBlockDescriptor(BuildDescriptor(in freeRemainder));
                if (remainderH8BlockIndex < 0)
                {
                    DumpPhiVodBlackBox();
                    blockIndex = -1;
                    pointer = default;
                    return false;
                }

                freeRemainder.H8BlockIndex = remainderH8BlockIndex;

                if (!TryInsertBlockAfter(i, in freeRemainder))
                {
                    ReleaseCommittedH8Descriptor(remainderH8BlockIndex);
                    blockIndex = -1;
                    pointer = default;
                    return false;
                }

                _blocks[i] = occupiedBlock;
                UpdateH8Descriptor(in occupiedBlock);
                return true;
            }

            return false;
        }

        private bool TryFreeBlock(int blockIndex, bool clearPayload = false)
        {
            if (!TryEnterBlockMutationGate())
            {
                RecordLockContentionFault(0);
                return false;
            }

            try
            {
                return FreeBlockLocked(blockIndex, clearPayload);
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool TryFreeBlockRollback(int blockIndex, bool clearPayload = false)
        {
            return TryFreeBlockUnderOwnedFence(blockIndex, clearPayload);
        }

        private bool TryFreeBlockUnderOwnedFence(int blockIndex, bool clearPayload = false)
        {
            if (!TryAcquireBlockMutationGate())
            {
                RecordLockContentionFault(0);
                return false;
            }

            try
            {
                return FreeBlockLocked(blockIndex, clearPayload);
            }
            finally
            {
                ReleaseBlockMutationGate();
            }
        }

        private bool FreeBlockLocked(int blockIndex, bool clearPayload = false)
        {
            if ((uint)blockIndex >= (uint)_blocks.Length)
                return false;

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.State != BlockStateOccupied)
                return false;
            if ((block.Reserved0 & (BlockFlagLocked | BlockFlagExternalView)) != 0 || block.Reserved1 != 0)
                return false;

            if (clearPayload)
            {
                if (block.OffsetBytes < 0L ||
                    block.Bytes <= 0L ||
                    block.Bytes > _arenaBytes ||
                    block.OffsetBytes > _arenaBytes - block.Bytes)
                {
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (_arenaBase != null)
                    UnsafeUtility.MemClear((byte*)_arenaBase + block.OffsetBytes, block.Bytes);
            }

            block.BufferKey = 0;
            block.State = BlockStateFree;
            block.Reserved0 = 0;
            block.Reserved1 = 0;
            block.Version = NextGeneration(block.Version);
            _blocks[blockIndex] = block;
            UpdateH8Descriptor(in block);
            BumpVaultGeneration();
            CoalesceFreeBlocksAround(blockIndex);
            return true;
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
            left.Reserved1 = 0;
            left.Version = NextGeneration(left.Version);
            _blocks[leftIndex] = left;
            UpdateH8Descriptor(in left);
            BumpVaultGeneration();

            right.Bytes = 0L;
            right.State = BlockStateFree;
            right.Reserved0 = 0;
            right.Reserved1 = 0;
            UpdateH8Descriptor(in right);
            RemoveBlockAt(rightIndex);
            RebuildMetadataBlockIndices();
        }

        private bool TryAppendBlockNoResize(in VaultArenaBlock block)
        {
            if (!_blocks.IsCreated || _blocks.Length >= _blocks.Capacity)
                return false;

            try
            {
                _blocks.AddNoResize(block);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryInsertBlockAfter(int index, in VaultArenaBlock block)
        {
            if (!_blocks.IsCreated || (uint)index >= (uint)_blocks.Length || _blocks.Length >= _blocks.Capacity)
                return false;

            try
            {
                _blocks.AddNoResize(default);
            }
            catch
            {
                return false;
            }

            for (int i = _blocks.Length - 1; i > index + 1; i--)
                _blocks[i] = _blocks[i - 1];
            _blocks[index + 1] = block;
            RebuildMetadataBlockIndices();
            return true;
        }

        private void ReleaseCommittedH8Descriptor(int descriptorIndex)
        {
            if (descriptorIndex < 0)
                return;

            BlockDescriptor descriptor = default;
            descriptor.State = (byte)H8BlockState.Free;
            H8Memory.TryUpdateBlockDescriptor(descriptorIndex, in descriptor);
        }

        private void RemoveBlockAt(int index)
        {
            int last = _blocks.Length - 1;
            for (int i = index; i < last; i++)
                _blocks[i] = _blocks[i + 1];
            _blocks.RemoveAt(last);
        }

        /// <summary>
        /// Re-points every occupied block's metadata at its current index and offset after <c>_blocks</c>
        /// has shifted, and refreshes the resolved base pointer to match.
        ///
        /// THIS MUST NOT TOUCH meta.Version, and doing so silently invalidated almost every outstanding
        /// handle in the project. VaultBufferMeta.Version and VaultArenaBlock.Version are two unrelated
        /// counters that this method used to conflate:
        ///   - VaultBufferMeta.Version is the buffer GENERATION that handle validation compares a caller's
        ///     cached handle against. There are seven such sites: :1630, :1670, :1710, :1850, :1914, :2049
        ///     and :2435, all of the form `if (handle.Generation != meta.Version) return false;`.
        ///   - VaultArenaBlock.Version is a block-mutation counter bumped on every split and coalesce.
        /// They are unequal BY CONSTRUCTION: a first-time key gets meta.Version =
        /// ResolveInitialGenerationForAllocation(key) = 1 (:1413) while its block was already bumped to >= 2
        /// inside TryAllocateBlockLocked (:6107, :6126).
        ///
        /// This method is reached from TryInsertBlockAfter (:6317), which TryAllocateBlockLocked calls on
        /// every allocation that splits a free block (:6147) - which is essentially every allocation. So the
        /// stamp produced this invariant: after N sequential allocations ONLY the most recently allocated
        /// buffer's outstanding handle still resolved, and every earlier handle was stale while its payload
        /// sat intact and unmoved.
        ///
        /// Measured consequence, from a real headless run: PlayerInventory binds 49 vault lanes in one &amp;&amp;
        /// chain, so each bind invalidated all its predecessors. The bind chain returned TRUE while only the
        /// last-bound lane was resolvable. CanServiceItemAdds() then read _stackCounts (ordinal 1, staled 47
        /// allocations earlier), found it unresolvable, and returned false - so every TryAddItem returned
        /// false, loot could not be queued, ResourceNode.TakeDamage:1199-1203 rolled the depletion back, and
        /// 12 authored quests reported 0 completions. One wrong assignment here blocked tools, resources and
        /// quests simultaneously and presented as three unrelated bugs.
        ///
        /// Dropping the stamp is safe rather than merely less wrong: a handle carries a generation, not an
        /// offset, and the resolved base pointer is recomputed from block.OffsetBytes in this same loop. So
        /// preserving the generation while refreshing the pointer leaves the handle valid AND pointing at the
        /// correct memory. The generation exists to detect reallocation and free - which have their own
        /// explicit paths, ResolveInitialGenerationForAllocation (:1413) and NextGeneration (:2447) - not to
        /// signal an index fixup.
        ///
        /// FOLLOW-UP NOW DONE: the five other sites that assigned a block version into meta.Version have all
        /// been resolved the same way - by PRESERVING the generation - and each carries its own comment:
        /// TryMoveOccupiedBlockLeft, both branches of MarkExternalViewLocked,
        /// RollbackAliasPublicationLocked and RefreshBlocksAfterArenaRelocation. Two of those are genuine
        /// relocation paths, so invalidation was considered and rejected on evidence: relocation in this vault
        /// is TRANSPARENT rather than handle-breaking, because (a) resolution recomputes the pointer from live
        /// _arenaBase plus live meta.OffsetBytes, both updated before the fence drops, (b) both relocation
        /// paths refuse to move anything a reader could be aliasing - TryMoveOccupiedBlockLeft skips
        /// BlockFlagExternalView and BlockFlagLocked blocks, and arena growth aborts while
        /// HasPinnedExternalViews() is true - and (c) there is no channel that would hand a bumped generation
        /// back to a consumer, since RecordRelocation is a telemetry ring and resolution never reads _buffers.
        /// So a bump on a move, by any mechanism, would silently kill every outstanding handle to a relocated
        /// buffer. _compactionFence, not the generation, is what protects readers during a move.
        ///
        /// The only legitimate writers of meta.Version are therefore: ResolveInitialGenerationForAllocation
        /// on allocation (:1413), NextGeneration(meta.Version) on explicit release/invalidation (:2447), and
        /// NextGenerationStatic(meta.Version) on the two recovery paths. All chain off meta's OWN previous
        /// value. If you are about to add a sixth writer sourced from a block, stop and re-read this comment.
        /// </summary>
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
                WriteMetadata(block.BufferKey, in meta);
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
            ushort flags = (ushort)(H8AllocationFlags.Raw | H8AllocationFlags.Vault);
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

        private static bool IsPointerAligned(IntPtr pointer, int alignment)
        {
            if (pointer == IntPtr.Zero || alignment <= 0)
                return false;

            return ((ulong)pointer.ToInt64() & (ulong)(alignment - 1)) == 0UL;
        }

        private static void SanitizeFinitePayload<T>(IntPtr pointer, int length) where T : struct
        {
            if (pointer == IntPtr.Zero || length <= 0)
                return;

            if (typeof(T) == typeof(float))
            {
                float* values = (float*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    if (!math.isfinite(values[i]))
                        values[i] = 0f;
                }

                return;
            }

            if (typeof(T) == typeof(float2))
            {
                float2* values = (float2*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    float2 value = values[i];
                    if (!math.all(math.isfinite(value)))
                        values[i] = default;
                }

                return;
            }

            if (typeof(T) == typeof(float3))
            {
                float3* values = (float3*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    float3 value = values[i];
                    if (!math.all(math.isfinite(value)))
                        values[i] = default;
                }

                return;
            }

            if (typeof(T) == typeof(float4))
            {
                float4* values = (float4*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    float4 value = values[i];
                    if (!math.all(math.isfinite(value)))
                        values[i] = default;
                }

                return;
            }

            if (typeof(T) == typeof(double))
            {
                double* values = (double*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    if (!math.isfinite(values[i]))
                        values[i] = 0d;
                }

                return;
            }

            if (typeof(T) == typeof(double2))
            {
                double2* values = (double2*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    double2 value = values[i];
                    if (!math.all(math.isfinite(value)))
                        values[i] = default;
                }

                return;
            }

            if (typeof(T) == typeof(double3))
            {
                double3* values = (double3*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    double3 value = values[i];
                    if (!math.all(math.isfinite(value)))
                        values[i] = default;
                }

                return;
            }

            if (typeof(T) == typeof(double4))
            {
                double4* values = (double4*)pointer.ToPointer();
                for (int i = 0; i < length; i++)
                {
                    double4 value = values[i];
                    if (!math.all(math.isfinite(value)))
                        values[i] = default;
                }
            }
        }

        private static bool ValidateType<T>(BufferID bufferId, VaultBufferMeta meta, int stride, int alignment) where T : struct
        {
            uint typeHash = ComputeTypeHash<T>();
            if (meta.Stride != stride ||
                meta.Alignment != alignment ||
                (meta.TypeHash != 0u && meta.TypeHash != typeHash))
            {
                return false;
            }

            return true;
        }

#if UNITY_EDITOR
        private static bool TryParseBudgetLine(ReadOnlySpan<byte> line, out VaultMemoryBudgetEntry entry)
        {
            entry = default;
            line = TrimAscii(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            if (!TryTakeCsvCell(line, 0, out ReadOnlySpan<byte> systemName, out int next) ||
                !TryTakeCsvCell(line, next, out ReadOnlySpan<byte> budgetCell, out next) ||
                !TryTakeCsvCell(line, next, out ReadOnlySpan<byte> defragCell, out next))
            {
                return false;
            }

            systemName = TrimAscii(systemName);
            if (systemName.Length == 0 || IsCsvHeader(systemName))
                return false;

            if (!TryParsePositiveLong(TrimAscii(budgetCell), out long budgetBytes) ||
                !TryParsePositiveLong(TrimAscii(defragCell), out long defragBytes))
            {
                return false;
            }

            entry.SystemHash = Fnv1a(systemName);
            entry.BufferID = 0;
            entry.BudgetBytes = budgetBytes;
            entry.DefragThresholdBytes = defragBytes;
            entry.Flags = 0u;
            return true;
        }

        private static bool TryTakeCsvCell(ReadOnlySpan<byte> line, int start, out ReadOnlySpan<byte> cell, out int next)
        {
            cell = default;
            next = start;
            if ((uint)start > (uint)line.Length)
                return false;

            int end = start;
            while (end < line.Length && line[end] != (byte)',')
                end++;

            cell = line.Slice(start, end - start);
            next = end < line.Length ? end + 1 : line.Length;
            return true;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= (byte)' ')
                start++;
            while (end >= start && value[end] <= (byte)' ')
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParsePositiveLong(ReadOnlySpan<byte> value, out long parsed)
        {
            parsed = 0L;
            if (value.Length == 0)
                return false;

            long result = 0L;
            for (int i = 0; i < value.Length; i++)
            {
                byte digit = value[i];
                if (digit < (byte)'0' || digit > (byte)'9')
                    return false;

                long next = (result * 10L) + (digit - (byte)'0');
                if (next < result)
                    return false;

                result = next;
            }

            parsed = result;
            return parsed > 0L;
        }

        private static uint Fnv1a(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ value[i]) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static bool IsCsvHeader(ReadOnlySpan<byte> value)
        {
            return value.Length == 6 &&
                ToLowerAscii(value[0]) == (byte)'s' &&
                ToLowerAscii(value[1]) == (byte)'y' &&
                ToLowerAscii(value[2]) == (byte)'s' &&
                ToLowerAscii(value[3]) == (byte)'t' &&
                ToLowerAscii(value[4]) == (byte)'e' &&
                ToLowerAscii(value[5]) == (byte)'m';
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct InitializeVaultMetadataJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<VaultBufferMeta> Metadata;

            public void Execute(int index)
            {
                VaultBufferMeta meta = default;
                meta.BufferKey = -1;
                Metadata[index] = meta;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct InitializeVaultBudgetEntriesJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<VaultMemoryBudgetEntry> Entries;

            public void Execute(int index)
            {
                Entries[index] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockVaultRelocationJob : IJob
        {
            [NoAlias] public NativeArray<VaultBufferMeta> Metadata;
            public uint Seed;
            public int MaxMutations;

            public void Execute()
            {
                uint state = Seed == 0u ? 0x9E3779B9u : Seed;
                int mutations = 0;
                int length = Metadata.IsCreated ? Metadata.Length : 0;
                for (int i = 1; i < length && mutations < MaxMutations; i++)
                {
                    state = (state * 1664525u) + 1013904223u;
                    if ((state & 7u) != 0u)
                        continue;

                    VaultBufferMeta meta = Metadata[i];
                    if (meta.BufferKey != i || meta.Version == 0u || meta.Length <= 0)
                        continue;

                    meta.Version = NextGenerationStatic(meta.Version);
                    meta.Flags ^= 1u;
                    Metadata[i] = meta;
                    mutations++;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SweepOrphanedHandlesJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<VaultBufferMeta> Metadata;
            [ReadOnly] [NoAlias] public NativeArray<SystemID> LiveOwners;
            public int LiveOwnerCount;

            public void Execute(int index)
            {
                VaultBufferMeta meta = Metadata[index];
                if (meta.BufferKey != index ||
                    meta.Version == 0u ||
                    meta.Length <= 0 ||
                    meta.RefCount == 0u)
                {
                    return;
                }

                if (!IsSceneOwnedVaultOwner(meta.Owner))
                {
                    meta.Flags &= ~VaultMetaFlagOrphanCandidate;
                    Metadata[index] = meta;
                    return;
                }

                int liveCount = math.min(math.max(0, LiveOwnerCount), LiveOwners.Length);
                for (int i = 0; i < liveCount; i++)
                {
                    if (LiveOwners[i] != meta.Owner)
                        continue;

                    meta.Flags &= ~VaultMetaFlagOrphanCandidate;
                    Metadata[index] = meta;
                    return;
                }

                meta.Flags |= VaultMetaFlagOrphanCandidate;
                Metadata[index] = meta;
            }
        }

#pragma warning disable 0649
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct VaultDefragmentationJob : IJob
        {
            // Invariant: ArenaBase is the vault-owned 64-byte aligned arena, defrag is scheduled only inside the owner fence, and metadata writers are excluded until the returned JobHandle completes.
            [NoAlias] [NativeDisableUnsafePtrRestriction] internal void* ArenaBase;
            [NoAlias] public NativeArray<VaultBufferMeta> Metadata;
            public int StartBufferID;
            public int EndBufferID;

            public void Execute()
            {
                if (ArenaBase == null || !Metadata.IsCreated)
                    return;

                int start = math.max(1, StartBufferID);
                int end = math.min(EndBufferID, Metadata.Length - 1);
                for (int i = start; i <= end; i++)
                {
                    VaultBufferMeta meta = Metadata[i];
                    if (meta.BufferKey != i || meta.Version == 0u || meta.Bytes <= 0L)
                        continue;

                    meta.Version = NextGenerationStatic(meta.Version);
                    Metadata[i] = meta;
                }
            }
        }
#pragma warning restore 0649

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NextGenerationStatic(uint generation)
        {
            uint next = generation + 1u;
            return next == 0u ? 1u : next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long AlignUp(long value, int alignment)
        {
            long mask = alignment - 1L;
            return (value + mask) & ~mask;
        }
    }
}
