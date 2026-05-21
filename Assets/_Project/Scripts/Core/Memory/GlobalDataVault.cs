using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

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

        /// <summary>Returns a persistent buffer view, growing the vault buffer when required.</summary>
        NativeArray<T> GetBuffer<T>(BufferID bufferId, int requiredLength, SystemID requester, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct;

        /// <summary>Returns a generation-checked handle for a persistent buffer, growing the vault buffer when required.</summary>
        VaultBufferHandle<T> GetBufferHandle<T>(BufferID bufferId, int requiredLength, SystemID requester, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct;

        /// <summary>Returns the 16-byte generation descriptor for a persistent buffer, growing the vault buffer when required.</summary>
        VaultGenerationHandle<T> GetGenerationHandle<T>(BufferID bufferId, int requiredLength, SystemID requester, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct;

        /// <summary>Attempts to read an existing buffer without creating or growing it.</summary>
        bool TryGetBuffer<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct;

        /// <summary>Attempts to read an existing generation-checked handle without creating or growing it.</summary>
        bool TryGetBufferHandle<T>(BufferID bufferId, out VaultBufferHandle<T> handle) where T : struct;

        /// <summary>Attempts to read an existing 16-byte generation descriptor without creating or growing it.</summary>
        bool TryGetGenerationHandle<T>(BufferID bufferId, out VaultGenerationHandle<T> handle) where T : struct;

        /// <summary>Resolves a 16-byte generation descriptor into a transient native view for the current phase.</summary>
        bool TryResolveHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct;

        /// <summary>Pure read accessor path: resolves an existing generation descriptor without fault telemetry mutation.</summary>
        bool TryReadHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct;

        /// <summary>Legacy bridge: resolves a cached VaultBufferHandle without trusting its cached pointer.</summary>
        bool TryResolveHandle<T>(in VaultBufferHandle<T> handle, out NativeArray<T> buffer) where T : struct;

        /// <summary>Resolves a generation slice descriptor into a transient native sub-view for the current phase.</summary>
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

        /// <summary>Legacy bridge: attempts to acquire a writer fence without trusting the cached pointer.</summary>
        bool TryAcquireWriteLock<T>(in VaultBufferHandle<T> handle, SystemID systemID, out NativeArray<T> buffer) where T : struct;

        /// <summary>Releases a writer fence acquired by <see cref="TryAcquireWriteLock{T}"/>.</summary>
        bool ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID) where T : struct;

        /// <summary>Legacy bridge: releases a writer fence without trusting the cached pointer.</summary>
        bool ReleaseWriteLock<T>(in VaultBufferHandle<T> handle, SystemID systemID) where T : struct;

        /// <summary>Releases one reference to a vault buffer and invalidates stale generation descriptors.</summary>
        bool ReleaseBuffer<T>(in VaultGenerationHandle<T> handle) where T : struct;

        /// <summary>Legacy bridge: releases one reference to a vault buffer without trusting the cached pointer.</summary>
        bool ReleaseBuffer<T>(in VaultBufferHandle<T> handle) where T : struct;

        /// <summary>Attempts to acquire a pointer slice from a cache-line-aligned vault block; non-zero startIndex alignment depends on stride and offset.</summary>
        bool TryAcquireSlice<T>(
            BufferID bufferId,
            int requiredLength,
            int startIndex,
            int count,
            SystemID requester,
            out VaultBufferSlice<T> slice,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct;

        /// <summary>Validates or refreshes a generation-checked handle; unsafe stale metadata fails fast.</summary>
        [Obsolete("Legacy pointer-refresh path. Use TryResolveHandle(in handle, out NativeArray<T>) so cached ptr is ignored.", false)]
        bool ResolveBuffer<T>(ref VaultBufferHandle<T> handle) where T : struct;

        /// <summary>Attempts to read the current generation for a buffer.</summary>
        bool TryGetBufferGeneration(BufferID bufferId, out uint generation);

        /// <summary>Returns a read-only alias over an existing buffer.</summary>
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
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VaultGenerationHandle<T> where T : struct
    {
        [FieldOffset(0)] public uint BufferID;
        [FieldOffset(4)] public uint SystemID;
        [FieldOffset(8)] public uint Generation;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Generation-checked vault buffer handle. Resolve before dereferencing across frames.
    /// </summary>
    /// <typeparam name="T">Blittable element type.</typeparam>
    [Obsolete("Legacy pointer-bearing handle. Persist VaultGenerationHandle<T>; use this type only as a migration bridge.", false)]
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public unsafe struct VaultBufferHandle<T> where T : struct
    {
        /// <summary>Cached raw pointer. Resolver refreshes it after allowed generation bumps; unsafe stale metadata fails fast.</summary>
        [Obsolete("Do not store or dereference cached Vault pointers. Persist VaultGenerationHandle<T> and resolve a transient NativeArray each phase.", false)]
        [FieldOffset(0)]
        public void* ptr;

        /// <summary>Cached buffer generation.</summary>
        [FieldOffset(8)]
        public uint generation;

        /// <summary>Cached buffer generation exposed under the batch contract name.</summary>
        public uint GenerationID => generation;

        /// <summary>Vault buffer identifier.</summary>
        [FieldOffset(12)]
        public BufferID BufferId;

        /// <summary>Current element count.</summary>
        [FieldOffset(16)]
        public int Length;

        /// <summary>Element stride captured at handle creation.</summary>
        [FieldOffset(20)]
        public int Stride;

        /// <summary>True when the handle currently points at a vault buffer.</summary>
        public bool IsCreated => ptr != null && BufferId != BufferID.Unknown && Length > 0;

        /// <summary>Returns the pointer-free descriptor form used by the strict generation resolver.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VaultGenerationHandle<T> ToGenerationHandle(SystemID requester = SystemID.Unknown)
        {
            VaultGenerationHandle<T> handle = default;
            handle.BufferID = unchecked((uint)(int)BufferId);
            handle.SystemID = requester == SystemID.Unknown ? 0u : (uint)requester;
            handle.Generation = generation;
            handle.Flags = 0u;
            return handle;
        }

        /// <summary>
        /// Resolves the handle and returns a NativeArray view over the current pointer.
        /// </summary>
        /// <param name="vault">Owning vault.</param>
        /// <returns>Current buffer view, or default when unavailable.</returns>
        [Obsolete("Use IDataVault.TryResolveHandle(in handle, out NativeArray<T>) and keep the NativeArray local to the execution phase.", false)]
        public NativeArray<T> Resolve(IDataVault vault)
        {
            if (vault == null || !vault.TryResolveHandle(in this, out NativeArray<T> buffer))
                return default;

            return buffer;
        }

        /// <summary>
        /// Resolves the handle and returns the current raw pointer.
        /// </summary>
        /// <param name="vault">Owning vault.</param>
        /// <returns>Current pointer, or null when unavailable.</returns>
        [Obsolete("Raw pointer leases are phase-local only. Prefer TryResolveHandle and pass NativeArray<T> into the job immediately.", false)]
        public void* ResolvePointer(IDataVault vault)
        {
            if (vault == null || !vault.TryResolveHandle(in this, out NativeArray<T> buffer) || !buffer.IsCreated)
                return null;

            return NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
        }

        /// <summary>
        /// Resolves and returns a mutable reference to the element in vault memory.
        /// </summary>
        /// <param name="vault">Owning vault.</param>
        /// <param name="index">Element index inside the buffer.</param>
        /// <returns>Reference to the exact element address.</returns>
        [Obsolete("Resolve a phase-local NativeArray through IDataVault.TryResolveHandle, then take refs from that local view.", false)]
        public ref T GetElementAsRef(IDataVault vault, int index)
        {
            NativeArray<T> buffer = Resolve(vault);
            if (!buffer.IsCreated || (uint)index >= (uint)buffer.Length)
                FatalMemoryException.ThrowStaleVaultHandle();

            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            Hint.Assume(basePtr != null);
            Hint.Assume(index >= 0);
            Hint.Assume(index < buffer.Length);
            return ref UnsafeUtility.AsRef<T>((byte*)basePtr + (index * UnsafeUtility.SizeOf<T>()));
        }

        /// <summary>
        /// Resolves and returns a read-only reference to the element in vault memory.
        /// </summary>
        /// <param name="vault">Owning vault.</param>
        /// <param name="index">Element index inside the buffer.</param>
        /// <returns>Read-only reference to the exact element address.</returns>
        [Obsolete("Resolve a phase-local NativeArray through IDataVault.TryResolveHandle, then take refs from that local view.", false)]
        public ref readonly T GetElementAsReadOnlyRef(IDataVault vault, int index)
        {
            return ref GetElementAsRef(vault, index);
        }

        /// <summary>
        /// Clears one tombstoned element in-place while preserving the buffer index.
        /// </summary>
        /// <param name="vault">Owning vault.</param>
        /// <param name="index">Element index to clear.</param>
        /// <param name="aliveMask">Single 64-entity alive mask owned by the caller.</param>
        /// <returns>True when the payload was cleared.</returns>
        [Obsolete("Use a generation-resolved phase-local NativeArray and tombstone through the owning system's write phase.", false)]
        public bool TryTombstoneElement(IDataVault vault, int index, ref ulong aliveMask)
        {
            NativeArray<T> buffer = Resolve(vault);
            if (!buffer.IsCreated || (uint)index >= (uint)buffer.Length)
                return false;

            aliveMask &= ~(1UL << (index & 63));
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            int stride = UnsafeUtility.SizeOf<T>();
            Hint.Assume(basePtr != null);
            Hint.Assume(index >= 0);
            Hint.Assume(index < buffer.Length);
            UnsafeUtility.MemClear((byte*)basePtr + (index * stride), stride);
            return true;
        }
    }

    /// <summary>
    /// Pointer-free slice descriptor derived from a generation handle. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VaultSliceHandle<T> where T : struct
    {
        [FieldOffset(0)] public uint BufferID;
        [FieldOffset(4)] public uint SystemID;
        [FieldOffset(8)] public uint Generation;
        [FieldOffset(12)] public uint HandleFlags;
        [FieldOffset(16)] public int StartIndex;
        [FieldOffset(20)] public int Length;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    /// <summary>
    /// Raw slice into vault-owned or emergency overflow memory. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public unsafe struct VaultBufferSlice<T> where T : struct
    {
        public const byte FlagPrimary = 1 << 0;
        public const byte FlagEmergencyOverflow = 1 << 1;
        public const byte FlagReadOnlyDummy = 1 << 2;

        [FieldOffset(0)]
        public void* Ptr;

        [FieldOffset(8)]
        public uint Generation;

        [FieldOffset(12)]
        public BufferID BufferId;

        [FieldOffset(16)]
        public int StartIndex;

        [FieldOffset(20)]
        public int Length;

        [FieldOffset(24)]
        public int Stride;

        [FieldOffset(28)]
        public byte Flags;

        [FieldOffset(29)]
        private byte _pad0;

        [FieldOffset(30)]
        private byte _pad1;

        [FieldOffset(31)]
        private byte _pad2;

        public bool IsCreated => Ptr != null && Length > 0 && Stride == UnsafeUtility.SizeOf<T>();
        public bool IsReadOnlyDummy => (Flags & FlagReadOnlyDummy) != 0;

        /// <summary>Returns a mutable reference to an element inside the slice.</summary>
        public ref T GetElementAsRef(int localIndex)
        {
            if (Ptr == null || (uint)localIndex >= (uint)Length || IsReadOnlyDummy)
                FatalMemoryException.ThrowStaleVaultHandle();

            Hint.Assume(Ptr != null);
            Hint.Assume(localIndex >= 0);
            Hint.Assume(localIndex < Length);
            return ref UnsafeUtility.AsRef<T>((byte*)Ptr + (localIndex * Stride));
        }

        /// <summary>Returns a read-only reference to an element inside the slice.</summary>
        public ref readonly T GetElementAsReadOnlyRef(int localIndex)
        {
            if (Ptr == null || (uint)localIndex >= (uint)Length)
                FatalMemoryException.ThrowStaleVaultHandle();

            Hint.Assume(Ptr != null);
            Hint.Assume(localIndex >= 0);
            Hint.Assume(localIndex < Length);
            return ref UnsafeUtility.AsRef<T>((byte*)Ptr + (localIndex * Stride));
        }
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

        [FieldOffset(0)] public long OldPointer;
        [FieldOffset(8)] public long NewPointer;
        [FieldOffset(16)] public int BufferId;
        [FieldOffset(20)] public int ByteLength;
        [FieldOffset(24)] public uint Generation;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte SystemId;
        [FieldOffset(30)] public ushort Reserved;
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
        [FieldOffset(40)] public SystemID Owner;
        [FieldOffset(42)] public SystemID LastAliasRequester;
        [FieldOffset(44)] public int ActiveWriterSystemID;
        [FieldOffset(48)] public uint TypeHash;
        [FieldOffset(52)] public uint RefCount;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public int BufferKey;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultTelemetrySnapshot
    {
        [FieldOffset(0)] public long AllocatedBytes;
        [FieldOffset(8)] public long ArenaBytes;
        [FieldOffset(16)] public long LastMovedBytes;
        [FieldOffset(24)] public long ResolutionTicks;
        [FieldOffset(32)] public uint VaultGenerationID;
        [FieldOffset(36)] public uint GenerationMismatchCount;
        [FieldOffset(40)] public int LastFaultBufferID;
        [FieldOffset(44)] public uint LastFaultHandleGeneration;
        [FieldOffset(48)] public uint LastFaultMetaGeneration;
        [FieldOffset(52)] public byte LastDefragFlags;
        [FieldOffset(53)] public byte Reserved0;
        [FieldOffset(54)] public ushort Reserved1;
        [FieldOffset(56)] public long ResolvedHandleCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VaultMemoryBudgetEntry
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public int BufferID;
        [FieldOffset(8)] public long BudgetBytes;
        [FieldOffset(16)] public long DefragThresholdBytes;
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
        [FieldOffset(16)] public int BufferKey;
        [FieldOffset(20)] public int H8BlockIndex;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public ushort Owner;
        [FieldOffset(30)] public ushort LockCount;
        [FieldOffset(32)] public byte State;
        [FieldOffset(33)] public byte Flags;
        [FieldOffset(34)] public ushort Reserved0;
        [FieldOffset(36)] public uint Reserved1;
        [FieldOffset(40)] public long Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct VaultArenaBlock
    {
        [FieldOffset(0)] public long OffsetBytes;
        [FieldOffset(8)] public long Bytes;
        [FieldOffset(16)] public int BufferKey;
        [FieldOffset(20)] public int H8BlockIndex;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public byte State;
        [FieldOffset(29)] public byte Reserved0;
        [FieldOffset(30)] public ushort Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
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
        [FieldOffset(64)] public int BlockCount;
        [FieldOffset(68)] public int ActiveBufferCount;
        [FieldOffset(72)] public int WatchdogBreaches;
        [FieldOffset(76)] public int EmergencyOverflowCursorBytes;
        [FieldOffset(80)] public float HeapFragmentationRatio;
        [FieldOffset(84)] public ushort LastRelocatedSystemId;
        [FieldOffset(86)] public ushort Reserved16;
        [FieldOffset(88)] public byte Flags;
        [FieldOffset(89)] public byte IsFragmented;
        [FieldOffset(90)] public byte WatchdogExceeded;
        [FieldOffset(91)] public byte MemoryStarvationWarnings;
        [FieldOffset(92)] public uint GenerationMismatchCount;
        [FieldOffset(96)] public long ResolutionTicks;
        [FieldOffset(104)] public long ResolvedHandleCount;
        [FieldOffset(112)] public int LastFaultBufferID;
        [FieldOffset(116)] public uint LastFaultHandleGeneration;
        [FieldOffset(120)] public uint LastFaultMetaGeneration;
        [FieldOffset(124)] public uint Reserved32;
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
        public const long LowTierArenaLimitBytes = 512L * 1024L * 1024L;
        public const long HighTierArenaLimitBytes = 4L * 1024L * 1024L * 1024L;
        private const float LowTierFragmentationRatioThreshold = 0.15f;
        private const float HighTierFragmentationRatioThreshold = 0.30f;
        private const float StressDefragHaltThreshold = 0.9f;
        private const long MassiveMoveThresholdBytes = 50L * 1024L * 1024L;
        private const long MaxLiveDefragMoveBytesPerSlice = 1024L;
        private const long ArenaGrowSlackBytes = 64L * 1024L * 1024L;
        private const byte MacroDatabasePayloadDirtyFlag = 1 << 0;
        private const int MaxMacroDatabasePayloadBytes = 256 * 1024;
        private const int MaxRelocationRecordCount = 64;
        private const int MaxMemoryBudgetEntries = 128;
        private const int EmergencyOverflowBufferBytes = 256 * 1024;
        private const int ReadOnlyDummyBufferBytes = 64;
        internal const byte BlockStateFree = 0;
        internal const byte BlockStateOccupied = 1;
        private const byte BlockFlagExternalView = 1 << 0;
        private const byte BlockFlagLocked = 1 << 1;
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
        private const int VaultBufferHandleSizeBytes = 24;
        private const int VaultGenerationHandleSizeBytes = 16;
        private const int VaultSliceHandleSizeBytes = 32;
        private const int VaultRelocationRecordSizeBytes = 32;
        private const int VaultBufferMetaSizeBytes = 64;
        private const int VaultMemoryBlockSnapshotSizeBytes = 48;
        private const int VaultArenaBlockSizeBytes = 32;
        private const int MemoryDefragTelemetryEntrySizeBytes = 128;
        private const int VaultTelemetrySnapshotSizeBytes = 64;
        private const int VaultMemoryBudgetEntrySizeBytes = 32;
        private const ulong DefragDumpMagic = 0x3147445648384848UL; // HH8HVDG1
        private const int DefragDumpVersion = 2;
        private const string DefragDumpPath = "Docs/AgentLogs/Dump_MEMORY_DEFRAGMENTATION_OVERSEER.bin";
        private const string PhiVodDumpPath = "Docs/AgentLogs/Dump_MEMORY_DEFRAGMENTATION_OVERSEER_PHIVOD.bin";
        private const string ShinobuDumpPath = "Docs/AgentLogs/Dump_SHINOBU_01.bin";
        private const string ShinobuH8DumpPath = "Docs/AgentLogs/Dump_SHINOBU_01.h8dump";
        private const string Shinobu202DumpPath = "Docs/AgentLogs/Dump_SHINOBU_202.bin";

        private UnsafeHashMap<int, IntPtr> _buffers;
        private UnsafeHashMap<int, VaultBufferMeta> _metadata;
        private NativeArray<VaultBufferMeta> _metadataByBufferId;
        private NativeList<int> _keys;
        private NativeList<VaultArenaBlock> _blocks;
        private NativeArray<MemoryDefragTelemetryEntry> _defragBlackBox;
        private NativeArray<VaultRelocationRecord> _lastRelocationRecords;
        private NativeArray<VaultMemoryBudgetEntry> _memoryBudgetEntries;
        private NativeParallelHashMap<ulong, MacroDatabasePayloadHandle> _macroDatabasePayloadCache;
        private NativeParallelHashMap<ulong, uint> _macroDatabasePayloadAccessTicks;
        private NativeList<ulong> _macroDatabasePayloadKeys;
        private void* _arenaBase;
        private void* _emergencyOverflowBase;
        private void* _readOnlyDummyBase;
        private long _arenaBytes;
        private long _arenaCapacityLimitBytes;
        private int _emergencyOverflowCursorBytes;
        private int _allocationLock;
        private int _compactionFence;
        private int _activeLocks;
        private int _mutationGuardMaskLow;
        private int _mutationGuardMaskHigh;
        private bool _memMoveBlockedByStress;
        private byte _memoryStarvationWarnings;
        private uint _lockedShiftFrameId;
        private long _allocatedBytes;
        private long _macroDatabasePayloadBytes;
        private int _macroDatabasePayloadEvictions;
        private uint _macroDatabaseCacheAccessClock;
        private long _lastPublishedPointerBits;
        private int _defragBlackBoxCursor;
        private int _defragBlackBoxRecordedCount;
        private int _lastRelocationRecordCount;
        private int _memoryBudgetCount;
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
        private long _totalDefragMovedBytes;
        private uint _defragTickSequence;
        private uint _vaultGenerationId;
        private SystemID _lastRelocatedSystemId;
        private bool _defragDumpWritten;
        private bool _phiVodDumpWritten;
        private bool _shinobu202DumpWritten;
        private bool _initialized;
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
        public bool IsAllocationLocked => _allocationLock != 0;

        /// <inheritdoc />
        public bool IsCompactionFenceActive => _compactionFence != 0;

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

        /// <inheritdoc />
        public int LastRelocationRecordCount => _lastRelocationRecordCount;

        /// <summary>
        /// Creates and initializes the vault for bootstrap registration.
        /// </summary>
        public static GlobalDataVault Create(int capacity = DefaultBufferCapacity, long arenaCapacityLimitBytes = LowTierArenaLimitBytes)
        {
            GlobalDataVault vault = new GlobalDataVault();
            vault.Initialize(capacity, arenaCapacityLimitBytes);
            _latestCreated = vault;
            return vault;
        }

        /// <summary>Returns the most recently initialized vault for editor diagnostics.</summary>
        public static bool TryGetLatestCreated(out GlobalDataVault vault)
        {
            vault = _latestCreated;
            return vault != null && vault._initialized;
        }

        /// <summary>
        /// Initializes raw vault maps.
        /// </summary>
        public void Initialize(int capacity = DefaultBufferCapacity, long arenaCapacityLimitBytes = LowTierArenaLimitBytes)
        {
            if (_initialized)
                return;

            ValidateAbiLayout();
            int safeCapacity = ResolveBufferCapacity(capacity);
            int blockCapacity = ResolveBlockCapacity(safeCapacity);

            H8Memory.Initialize();
            _buffers = new UnsafeHashMap<int, IntPtr>(safeCapacity, Allocator.Persistent);
            _metadata = new UnsafeHashMap<int, VaultBufferMeta>(safeCapacity, Allocator.Persistent);
            _metadataByBufferId = H8Memory.Allocate<VaultBufferMeta>(
                MaxGenerationHandleCapacity,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
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
            _lastRelocationRecords = H8Memory.Allocate<VaultRelocationRecord>(
                MaxRelocationRecordCount,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            _memoryBudgetEntries = H8Memory.Allocate<VaultMemoryBudgetEntry>(
                MaxMemoryBudgetEntries,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            InitializeVaultBudgetEntriesJob budgetJob = new InitializeVaultBudgetEntriesJob
            {
                Entries = _memoryBudgetEntries
            };
            for (int i = 0; i < _memoryBudgetEntries.Length; i++)
                budgetJob.Execute(i);
            _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadHandle>(safeCapacity, Allocator.Persistent);
            _macroDatabasePayloadAccessTicks = new NativeParallelHashMap<ulong, uint>(safeCapacity, Allocator.Persistent);
            _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);
            _arenaCapacityLimitBytes = ResolveArenaCapacityLimit(arenaCapacityLimitBytes);
            _arenaBytes = AlignUp(math.min(DefaultArenaBytes, _arenaCapacityLimitBytes), VaultBlockAlignment);
            _arenaBase = H8Memory.AllocateRaw(
                _arenaBytes,
                VaultBlockAlignment,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                clearMemory: true,
                H8AllocationFlags.Vault | H8AllocationFlags.SubAllocatorRoot);
            _emergencyOverflowBase = H8Memory.AllocateRaw(
                EmergencyOverflowBufferBytes,
                VaultBlockAlignment,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                clearMemory: true,
                H8AllocationFlags.Vault);
            _readOnlyDummyBase = H8Memory.AllocateRaw(
                ReadOnlyDummyBufferBytes,
                VaultBlockAlignment,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                clearMemory: true,
                H8AllocationFlags.Vault);
            if (_arenaBase == null)
            {
                _initialized = true;
                Dispose();
                return;
            }

            _allocationLock = 0;
            _compactionFence = 0;
            _activeLocks = 0;
            _mutationGuardMaskLow = 0;
            _mutationGuardMaskHigh = 0;
            _emergencyOverflowCursorBytes = 0;
            _memoryStarvationWarnings = 0;
            _lockedShiftFrameId = 0u;
            _allocatedBytes = 0L;
            _macroDatabasePayloadBytes = 0L;
            _macroDatabasePayloadEvictions = 0;
            _macroDatabaseCacheAccessClock = 0u;
            _lastPublishedPointerBits = 0L;
            _defragBlackBoxCursor = 0;
            _defragBlackBoxRecordedCount = 0;
            _lastRelocationRecordCount = 0;
            _memoryBudgetCount = 0;
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
            _totalDefragMovedBytes = 0L;
            _defragTickSequence = 0u;
            _vaultGenerationId = 1u;
            _lastRelocatedSystemId = SystemID.Unknown;
            _defragDumpWritten = false;
            _phiVodDumpWritten = false;
            _shinobu202DumpWritten = false;
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
                int h8BlockIndex = H8Memory.RegisterBlockDescriptor(BuildDescriptor(in freeBlock));
                if (h8BlockIndex < 0)
                {
                    DumpPhiVodBlackBox();
                    _initialized = true;
                    Dispose();
                    FatalMemoryException.ThrowAllocationTrackingFailed();
                    return;
                }

                freeBlock.H8BlockIndex = h8BlockIndex;
                _blocks.AddNoResize(freeBlock);
            }

            _initialized = true;
            _latestCreated = this;
        }

        private static void ValidateAbiLayout()
        {
            if (UnsafeUtility.SizeOf<VaultBufferHandle<byte>>() != VaultBufferHandleSizeBytes ||
                UnsafeUtility.SizeOf<VaultGenerationHandle<byte>>() != VaultGenerationHandleSizeBytes ||
                UnsafeUtility.SizeOf<VaultSliceHandle<byte>>() != VaultSliceHandleSizeBytes ||
                UnsafeUtility.SizeOf<VaultBufferSlice<byte>>() != VaultArenaBlockSizeBytes ||
                UnsafeUtility.SizeOf<VaultRelocationRecord>() != VaultRelocationRecordSizeBytes ||
                UnsafeUtility.SizeOf<VaultBufferMeta>() != VaultBufferMetaSizeBytes ||
                UnsafeUtility.SizeOf<VaultMemoryBlockSnapshot>() != VaultMemoryBlockSnapshotSizeBytes ||
                UnsafeUtility.SizeOf<VaultArenaBlock>() != VaultArenaBlockSizeBytes ||
                UnsafeUtility.SizeOf<MemoryDefragTelemetryEntry>() != MemoryDefragTelemetryEntrySizeBytes ||
                UnsafeUtility.SizeOf<VaultTelemetrySnapshot>() != VaultTelemetrySnapshotSizeBytes ||
                UnsafeUtility.SizeOf<VaultMemoryBudgetEntry>() != VaultMemoryBudgetEntrySizeBytes)
            {
                FatalMemoryException.ThrowAbiLayoutMismatch();
            }
        }

        /// <inheritdoc />
        public NativeArray<T> GetBuffer<T>(
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
                    exposeExternalView: true,
                    out IntPtr pointer,
                    out int resolvedLength))
            {
                return default;
            }

            return H8Memory.CreateNativeArrayView<T>(pointer.ToPointer(), resolvedLength);
        }

        /// <inheritdoc />
        public VaultBufferHandle<T> GetBufferHandle<T>(
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
                !TryBuildHandle(bufferId, out VaultBufferHandle<T> handle))
            {
                return default;
            }

            return handle;
        }

        /// <inheritdoc />
        public VaultGenerationHandle<T> GetGenerationHandle<T>(
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
                FatalMemoryException.ThrowUnknownAllocationOwner();

            EnsureInitialized();
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

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            if (stride <= 0 || requiredLength > long.MaxValue / stride)
                return false;

            long requestedBytes = (long)requiredLength * stride;
            long requiredBytes = AlignUp(requestedBytes, VaultBlockAlignment);
            if (requiredBytes <= 0L)
                return false;
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

                ValidateType<T>(bufferId, existingMeta, stride, alignment);
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
                        DumpPhiVodBlackBox();
                        return false;
                    }

                    if (sanitizeFinite)
                        SanitizeFinitePayload<T>(existingPointer, existingMeta.Length);
                    resolvedPointer = existingPointer;
                    resolvedLength = existingMeta.Length;
                    return true;
                }

                if (_allocationLock != 0)
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
                    DumpPhiVodBlackBox();
                    return false;
                }

                if (sanitizeFinite)
                    SanitizeFinitePayload<T>(resizedPointer, requiredLength);
                resolvedPointer = resizedPointer;
                resolvedLength = requiredLength;
                return true;
            }

            if (_allocationLock != 0)
                return false;

            if (_keys.Length >= _keys.Capacity)
                return false;

            if (!TryAllocateBlock(key, requiredBytes, out int blockIndex, out IntPtr pointer))
            {
                if (!TryGrowArenaForBytes(requiredBytes) ||
                    !TryAllocateBlock(key, requiredBytes, out blockIndex, out pointer))
                {
                    return false;
                }
            }

            if (!IsPointerAligned(pointer, VaultBlockAlignment))
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                FreeBlock(blockIndex);
                DumpPhiVodBlackBox();
                return false;
            }

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
                Owner = requester,
                Allocator = Allocator.Persistent,
                Version = ResolveInitialGenerationForAllocation(key),
                ActiveWriterSystemID = 0,
                TypeHash = ComputeTypeHash<T>(),
                RefCount = 1u,
                Flags = 0u,
                BufferKey = key
            };

            bool bufferAdded = _buffers.TryAdd(key, pointer);
            bool metadataAdded = bufferAdded && TryAddMetadata(key, in meta);
            if (!bufferAdded || !metadataAdded)
            {
                if (bufferAdded)
                    _buffers.Remove(key);
                if (!metadataAdded)
                    RemoveMetadata(key);

                FreeBlock(blockIndex);
                DumpPhiVodBlackBox();
                return false;
            }

            if (!EnsureBufferKeyRegistered(key))
            {
                _buffers.Remove(key);
                RemoveMetadata(key);
                FreeBlock(blockIndex);
                DumpPhiVodBlackBox();
                return false;
            }

            _allocatedBytes += requiredBytes;
            if (exposeExternalView && !MarkExternalView(key, meta.OffsetBytes))
            {
                RemoveBufferKey(key);
                _buffers.Remove(key);
                RemoveMetadata(key);
                _allocatedBytes = _allocatedBytes > requiredBytes ? _allocatedBytes - requiredBytes : 0L;
                FreeBlock(blockIndex);
                DumpPhiVodBlackBox();
                return false;
            }

            if (sanitizeFinite)
                SanitizeFinitePayload<T>(pointer, requiredLength);
            resolvedPointer = pointer;
            resolvedLength = requiredLength;
            return true;
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
            if (!IsPointerAligned(pointer, VaultBlockAlignment))
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                DumpPhiVodBlackBox();
                return false;
            }

            SanitizeFinitePayload<T>(pointer, meta.Length);
            buffer = H8Memory.CreateNativeArrayView<T>(pointer.ToPointer(), meta.Length);
            if (buffer.IsCreated)
            {
                if (!MarkExternalView(key, meta.OffsetBytes))
                {
                    buffer = default;
                    DumpPhiVodBlackBox();
                    return false;
                }

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
        public bool TryGetGenerationHandle<T>(BufferID bufferId, out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (!_initialized || _compactionFence != 0)
                return false;

            return TryBuildGenerationHandle(bufferId, out handle);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolveHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            if (!_initialized || _compactionFence != 0 || _arenaBase == null)
            {
                RecordGenerationFault(unchecked((int)handle.BufferID), handle.Generation, 0u);
                return false;
            }

            int key = unchecked((int)handle.BufferID);
            if (key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
            {
                RecordGenerationFault(key, handle.Generation, 0u);
                return false;
            }

            if (handle.Generation != meta.Version)
            {
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (handle.SystemID != 0u && handle.SystemID != (uint)meta.Owner)
            {
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }

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
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }
#endif

            buffer = H8Memory.CreateNativeArrayView<T>((byte*)_arenaBase + meta.OffsetBytes, meta.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Interlocked.Increment(ref _resolvedHandleCount);
            Interlocked.Add(ref _resolutionTickAccumulator, System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);
#endif
            return buffer.IsCreated;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!_initialized || _compactionFence != 0 || _arenaBase == null)
                return false;

            int key = unchecked((int)handle.BufferID);
            if (key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
                return false;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
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
#endif

            buffer = H8Memory.CreateNativeArrayView<T>((byte*)_arenaBase + meta.OffsetBytes, meta.Length);
            return buffer.IsCreated;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolveHandle<T>(in VaultBufferHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (handle.BufferId == BufferID.Unknown ||
                handle.generation == 0u ||
                handle.Length <= 0 ||
                handle.Stride != UnsafeUtility.SizeOf<T>())
            {
                return false;
            }

            VaultGenerationHandle<T> descriptor = handle.ToGenerationHandle();
            return TryResolveHandle(in descriptor, out buffer);
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
            {
                RecordGenerationFault(unchecked((int)handle.BufferID), handle.Generation, handle.Generation);
                return false;
            }

            slice = full.GetSubArray(handle.StartIndex, handle.Length);
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
            if (systemID == SystemID.Unknown || !_metadataByBufferId.IsCreated)
                return false;

            int key = unchecked((int)handle.BufferID);
            if ((uint)key >= (uint)_metadataByBufferId.Length || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
            {
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }

            if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
                return false;

            VaultArenaBlock block = _blocks[blockIndex];
            if ((block.Reserved0 & BlockFlagLocked) != 0 || block.Reserved1 != 0)
                return false;

            VaultBufferMeta* metadata = (VaultBufferMeta*)NativeArrayUnsafeUtility.GetUnsafePtr(_metadataByBufferId);
            ref int activeWriter = ref metadata[key].ActiveWriterSystemID;
            if (Interlocked.CompareExchange(ref activeWriter, (int)systemID, 0) != 0)
                return false;

            if (!TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out blockIndex))
            {
                Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID);
                WriteMetadata(key, in metadata[key]);
                return false;
            }

            block = _blocks[blockIndex];
            if ((block.Reserved0 & BlockFlagLocked) != 0 || block.Reserved1 != 0)
            {
                Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID);
                WriteMetadata(key, in metadata[key]);
                buffer = default;
                return false;
            }

            WriteMetadata(key, in metadata[key]);
            if (TryResolveHandle(in handle, out buffer))
                return true;

            Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID);
            WriteMetadata(key, in metadata[key]);
            buffer = default;
            return false;
        }

        /// <inheritdoc />
        public bool TryAcquireWriteLock<T>(in VaultBufferHandle<T> handle, SystemID systemID, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (handle.BufferId == BufferID.Unknown ||
                handle.generation == 0u ||
                handle.Length <= 0 ||
                handle.Stride != UnsafeUtility.SizeOf<T>())
            {
                return false;
            }

            VaultGenerationHandle<T> descriptor = handle.ToGenerationHandle(systemID);
            return TryAcquireWriteLock(in descriptor, systemID, out buffer);
        }

        /// <inheritdoc />
        public bool ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID) where T : struct
        {
            if (systemID == SystemID.Unknown || !_metadataByBufferId.IsCreated)
                return false;

            int key = unchecked((int)handle.BufferID);
            if ((uint)key >= (uint)_metadataByBufferId.Length || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
                return false;

            if (handle.Generation != meta.Version)
            {
                RecordGenerationFault(key, handle.Generation, meta.Version);
                return false;
            }

            VaultBufferMeta* metadata = (VaultBufferMeta*)NativeArrayUnsafeUtility.GetUnsafePtr(_metadataByBufferId);
            ref int activeWriter = ref metadata[key].ActiveWriterSystemID;
            if (Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID) != (int)systemID)
                return false;

            WriteMetadata(key, in metadata[key]);
            return true;
        }

        /// <inheritdoc />
        public bool ReleaseWriteLock<T>(in VaultBufferHandle<T> handle, SystemID systemID) where T : struct
        {
            if (handle.BufferId == BufferID.Unknown ||
                handle.generation == 0u ||
                handle.Length <= 0 ||
                handle.Stride != UnsafeUtility.SizeOf<T>())
            {
                return false;
            }

            VaultGenerationHandle<T> descriptor = handle.ToGenerationHandle(systemID);
            return ReleaseWriteLock(in descriptor, systemID);
        }

        /// <inheritdoc />
        public bool ReleaseBuffer<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            int key = unchecked((int)handle.BufferID);
            if (!_initialized || key == 0 || !TryReadFlatMetadata(key, out VaultBufferMeta meta))
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

            _allocatedBytes = _allocatedBytes > meta.Bytes ? _allocatedBytes - meta.Bytes : 0L;
            _buffers.Remove(key);
            RemoveMetadata(key);
            RemoveBufferKey(key);
            FreeBlock(blockIndex, clearPayload: true);
            return true;
        }

        /// <inheritdoc />
        public bool ReleaseBuffer<T>(in VaultBufferHandle<T> handle) where T : struct
        {
            if (handle.BufferId == BufferID.Unknown ||
                handle.generation == 0u ||
                handle.Length <= 0 ||
                handle.Stride != UnsafeUtility.SizeOf<T>())
            {
                return false;
            }

            VaultGenerationHandle<T> descriptor = handle.ToGenerationHandle();
            return ReleaseBuffer(in descriptor);
        }

        /// <inheritdoc />
        public bool TryAcquireSlice<T>(
            BufferID bufferId,
            int requiredLength,
            int startIndex,
            int count,
            SystemID requester,
            out VaultBufferSlice<T> slice,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct
        {
            slice = default;
            if (count <= 0 || startIndex < 0 || requiredLength <= 0)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            if (stride <= 0 || startIndex > int.MaxValue - count)
                return false;

            int endIndex = startIndex + count;
            int actualRequiredLength = requiredLength >= endIndex ? requiredLength : endIndex;
            if (TryEnsureVaultBuffer<T>(
                    bufferId,
                    actualRequiredLength,
                    requester,
                    options,
                    exposeExternalView: false,
                    out IntPtr pointer,
                    out int resolvedLength,
                    sanitizeFinite: false) &&
                endIndex <= resolvedLength &&
                TryBuildHandle(bufferId, out VaultBufferHandle<T> handle))
            {
                Hint.Assume(pointer != IntPtr.Zero);
                Hint.Assume(startIndex >= 0);
                Hint.Assume(count > 0);
                slice.Ptr = (byte*)pointer.ToPointer() + (startIndex * stride);
                slice.Generation = handle.generation;
                slice.BufferId = bufferId;
                slice.StartIndex = startIndex;
                slice.Length = count;
                slice.Stride = stride;
                slice.Flags = VaultBufferSlice<T>.FlagPrimary;
                return true;
            }

            SetMemoryStarvationWarning(1);
            if (TryAcquireEmergencyOverflowSlice(count, stride, out void* emergencyPtr))
            {
                slice.Ptr = emergencyPtr;
                slice.Generation = _vaultGenerationId;
                slice.BufferId = bufferId;
                slice.StartIndex = 0;
                slice.Length = count;
                slice.Stride = stride;
                slice.Flags = VaultBufferSlice<T>.FlagEmergencyOverflow;
                return true;
            }

            SetMemoryStarvationWarning(2);
            slice = BuildReadOnlyDummySlice<T>(bufferId, count, stride);
            return false;
        }

        /// <inheritdoc />
        [Obsolete("Legacy pointer-refresh path. Use TryResolveHandle(in handle, out NativeArray<T>) so cached ptr is ignored.", false)]
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
            if (!IsPointerAligned(pointer, VaultBlockAlignment))
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                DumpPhiVodBlackBox();
                FatalMemoryException.ThrowStaleVaultHandle();
            }

            bool matchesMetadata =
                handle.generation == meta.Version &&
                handle.ptr != null &&
                (IntPtr)handle.ptr == pointer &&
                handle.Length == meta.Length &&
                handle.Stride == meta.Stride;
            if (!matchesMetadata)
            {
                if (hasCachedIdentity && !CanRefreshHandleAfterGenerationBump(in handle, in meta, pointer))
                {
                    DumpPhiVodBlackBox();
                    FatalMemoryException.ThrowStaleVaultHandle();
                }

                handle.ptr = pointer.ToPointer();
                handle.generation = meta.Version;
                handle.BufferId = (BufferID)key;
                handle.Length = meta.Length;
                handle.Stride = meta.Stride;
                if (hasCachedIdentity)
                    Interlocked.Increment(ref _generationHandleMissCount);
            }

            SanitizeFinitePayload<T>(pointer, meta.Length);
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
            if (requester == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAliasReader();

            if (!TryGetBuffer<T>(bufferId, out NativeArray<T> buffer))
                return default;

            if (!MarkAliasReader((int)bufferId, requester))
            {
                DumpPhiVodBlackBox();
                return default;
            }

            return H8Memory.CreateAlias(buffer, requester);
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
            if (remainingCount > 0)
                DumpPhiVodBlackBox();

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
            if (!_initialized ||
                phase != MemoryDefragPhase.PreSimulation ||
                HasActiveBurstLocks(activeBurstLockMask) ||
                _compactionFence != 0 ||
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
            return TryLockBuffer(bufferId, SystemID.Unknown);
        }

        /// <inheritdoc />
        public bool TryLockBuffer(BufferID bufferId, SystemID lockOwner)
        {
            if (!_initialized || bufferId == BufferID.Unknown)
                return false;

            int key = (int)bufferId;
            if (!TryReadFlatMetadata(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out int blockIndex))
            {
                return false;
            }

            if (meta.ActiveWriterSystemID != 0)
                return false;

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.Reserved1 == ushort.MaxValue)
                return false;

            block.Reserved1++;
            block.Reserved0 |= BlockFlagLocked;
            _blocks[blockIndex] = block;
            if (!TryReadFlatMetadata(key, out VaultBufferMeta postLockMeta) ||
                postLockMeta.ActiveWriterSystemID != 0)
            {
                block = _blocks[blockIndex];
                if (block.Reserved1 > 0)
                {
                    block.Reserved1--;
                    if (block.Reserved1 == 0)
                        block.Reserved0 &= unchecked((byte)~BlockFlagLocked);
                    _blocks[blockIndex] = block;
                }

                ClearActiveLockBitIfUnused(ResolveActiveLockBit(bufferId));
                return false;
            }

            SetActiveLockBit(ResolveActiveLockBit(bufferId));
            return true;
        }

        /// <inheritdoc />
        [Obsolete("Use the owner-tagged overload so compaction telemetry records the scheduling system.", false)]
        public bool TryUnlockBuffer(BufferID bufferId)
        {
            return TryUnlockBuffer(bufferId, SystemID.Unknown);
        }

        /// <inheritdoc />
        public bool TryUnlockBuffer(BufferID bufferId, SystemID lockOwner)
        {
            if (!_initialized || bufferId == BufferID.Unknown)
                return false;

            int key = (int)bufferId;
            if (!TryReadFlatMetadata(key, out VaultBufferMeta meta) ||
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
            ClearActiveLockBitIfUnused(ResolveActiveLockBit(bufferId));
            return true;
        }

        /// <inheritdoc />
        public bool TryAcquireMutationGuard(ulong writeMask)
        {
            if (writeMask == 0UL)
                return false;

            int lowMask = unchecked((int)(uint)writeMask);
            int highMask = unchecked((int)(uint)(writeMask >> 32));
            while (true)
            {
                int observedLow = Volatile.Read(ref _mutationGuardMaskLow);
                int observedHigh = Volatile.Read(ref _mutationGuardMaskHigh);
                if ((observedLow & lowMask) != 0 || (observedHigh & highMask) != 0)
                    return false;

                if (Interlocked.CompareExchange(ref _mutationGuardMaskLow, observedLow | lowMask, observedLow) != observedLow)
                    continue;

                if (Interlocked.CompareExchange(ref _mutationGuardMaskHigh, observedHigh | highMask, observedHigh) == observedHigh)
                    return true;

                ReleaseMutationGuard((uint)lowMask);
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
            if (HasLockedBlockForBit(bit))
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
            if (!_blocks.IsCreated)
                return false;

            for (int i = 0; i < _blocks.Length; i++)
            {
                VaultArenaBlock block = _blocks[i];
                if (block.State != BlockStateOccupied ||
                    ((block.Reserved0 & BlockFlagLocked) == 0 && block.Reserved1 == 0))
                {
                    continue;
                }

                if (ResolveActiveLockBit((BufferID)block.BufferKey) == bit)
                    return true;
            }

            return false;
        }

        private bool HasActiveBurstLocks(uint externalLockMask)
        {
            uint localLockMask = unchecked((uint)Volatile.Read(ref _activeLocks));
            return (localLockMask | externalLockMask) != 0u;
        }

        private void SetMemoryStarvationWarning(byte bit)
        {
            _memoryStarvationWarnings = (byte)(_memoryStarvationWarnings | (byte)(1 << bit));
            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);
        }

        private bool TryAcquireEmergencyOverflowSlice(int count, int stride, out void* ptr)
        {
            ptr = null;
            if (_emergencyOverflowBase == null || count <= 0 || stride <= 0)
                return false;

            long payloadBytes = (long)count * stride;
            if (payloadBytes <= 0L || payloadBytes > int.MaxValue)
                return false;

            int byteCount = (int)AlignUp(payloadBytes, VaultBlockAlignment);
            while (true)
            {
                int observed = Volatile.Read(ref _emergencyOverflowCursorBytes);
                int alignedOffset = (int)AlignUp(observed, VaultBlockAlignment);
                long next = (long)alignedOffset + byteCount;
                if (next > EmergencyOverflowBufferBytes)
                    return false;

                if (Interlocked.CompareExchange(ref _emergencyOverflowCursorBytes, (int)next, observed) != observed)
                    continue;

                ptr = (byte*)_emergencyOverflowBase + alignedOffset;
                UnsafeUtility.MemClear(ptr, byteCount);
                return true;
            }
        }

        private VaultBufferSlice<T> BuildReadOnlyDummySlice<T>(BufferID bufferId, int count, int stride) where T : struct
        {
            VaultBufferSlice<T> slice = default;
            slice.Ptr = _readOnlyDummyBase;
            slice.Generation = _vaultGenerationId;
            slice.BufferId = bufferId;
            slice.StartIndex = 0;
            slice.Stride = stride > 0 ? stride : UnsafeUtility.SizeOf<T>();
            int maxDummyCount = slice.Stride > 0 ? ReadOnlyDummyBufferBytes / slice.Stride : 1;
            if (maxDummyCount <= 0)
                maxDummyCount = 1;
            slice.Length = math.min(count > 0 ? count : 1, maxDummyCount);
            slice.Flags = (byte)(VaultBufferSlice<T>.FlagEmergencyOverflow | VaultBufferSlice<T>.FlagReadOnlyDummy);
            return slice;
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
        public void RecordHeartbeat()
        {
            if (!_initialized || !_defragBlackBox.IsCreated)
                return;

            Volatile.Write(ref _emergencyOverflowCursorBytes, 0);
            _memoryStarvationWarnings = 0;
            RecordDefragBlackBox(++_defragTickSequence, DefragFlagHeartbeat);
            if (_arenaBytes > 0L && (_allocatedBytes * 10L) >= (_arenaBytes * 9L))
                DumpShinobu202BlackBox();
        }

        /// <inheritdoc />
        public void RequestEditorForceDefragmentation()
        {
            Interlocked.Exchange(ref _forceDefragRequested, 1);
        }

        public bool GenerateMockVaultRelocationForValidation(uint seed, int maxMutations, MemoryDefragPhase phase, uint activeBurstLockMask)
        {
            if (!_initialized ||
                phase != MemoryDefragPhase.PreSimulation ||
                HasActiveBurstLocks(activeBurstLockMask) ||
                _compactionFence != 0 ||
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
            if (!_defragBlackBox.IsCreated || _defragBlackBoxRecordedCount <= 0)
                return false;

            int safeAge = math.clamp(ageFromNewest, 0, _defragBlackBoxRecordedCount - 1);
            int index = _defragBlackBoxCursor - 1 - safeAge;
            while (index < 0)
                index += _defragBlackBox.Length;

            MemoryDefragTelemetryEntry entry = _defragBlackBox[index];
            snapshot.AllocatedBytes = _allocatedBytes;
            snapshot.ArenaBytes = _arenaBytes;
            snapshot.LastMovedBytes = entry.LastMovedBytes;
            snapshot.ResolutionTicks = entry.ResolutionTicks;
            snapshot.VaultGenerationID = entry.VaultGenerationID;
            snapshot.GenerationMismatchCount = entry.GenerationMismatchCount;
            snapshot.LastFaultBufferID = entry.LastFaultBufferID;
            snapshot.LastFaultHandleGeneration = entry.LastFaultHandleGeneration;
            snapshot.LastFaultMetaGeneration = entry.LastFaultMetaGeneration;
            snapshot.LastDefragFlags = entry.Flags;
            snapshot.ResolvedHandleCount = entry.ResolvedHandleCount;
            return true;
        }

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
                _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadHandle>(safeCapacity, Allocator.Persistent);
            if (!_macroDatabasePayloadAccessTicks.IsCreated)
                _macroDatabasePayloadAccessTicks = new NativeParallelHashMap<ulong, uint>(safeCapacity, Allocator.Persistent);
            if (!_macroDatabasePayloadKeys.IsCreated)
                _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);

            if (_macroDatabasePayloadCache.Capacity < safeCapacity)
                _macroDatabasePayloadCache.Capacity = safeCapacity;
            if (_macroDatabasePayloadAccessTicks.Capacity < safeCapacity)
                _macroDatabasePayloadAccessTicks.Capacity = safeCapacity;
            if (_macroDatabasePayloadKeys.Capacity < safeCapacity)
                _macroDatabasePayloadKeys.Capacity = safeCapacity;

            return _macroDatabasePayloadCache.Capacity >= safeCapacity &&
                   _macroDatabasePayloadAccessTicks.Capacity >= safeCapacity &&
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
            if (sectorHash == 0UL ||
                source == IntPtr.Zero ||
                byteLength <= 0 ||
                byteLength > MaxMacroDatabasePayloadBytes)
            {
                return false;
            }

            EnsureInitialized();
            if (!_macroDatabasePayloadCache.IsCreated && !TryReserveMacroDatabaseCache(DefaultBufferCapacity))
                return false;

            bool hasExisting = _macroDatabasePayloadCache.TryGetValue(sectorHash, out MacroDatabasePayloadHandle existing);
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
                SubtractMacroDatabasePayloadBytes(existing.ByteLength);
                _macroDatabasePayloadCache[sectorHash] = handle;
                TouchMacroDatabasePayload(sectorHash);
            }
            else
            {
                if (!_macroDatabasePayloadCache.TryAdd(sectorHash, handle))
                {
                    H8Memory.FreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault);
                    handle = default;
                    return false;
                }

                if (!EnsureMacroDatabaseKeyRegistered(sectorHash))
                {
                    _macroDatabasePayloadCache.Remove(sectorHash);
                    H8Memory.FreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault);
                    handle = default;
                    return false;
                }

                TouchMacroDatabasePayload(sectorHash);
            }

            _macroDatabasePayloadBytes += byteLength;
            BumpVaultGeneration();
            return true;
        }

        /// <inheritdoc />
        public bool TryGetMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle handle)
        {
            handle = default;
            if (!_macroDatabasePayloadCache.IsCreated ||
                !_macroDatabasePayloadCache.TryGetValue(sectorHash, out handle))
            {
                return false;
            }

            TouchMacroDatabasePayload(sectorHash);
            return true;
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
                return;

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

            if (_arenaBase != null)
            {
                H8Memory.FreeRaw(_arenaBase, Allocator.Persistent, SystemID.CoreDataVault);
                _arenaBase = null;
            }
            if (_emergencyOverflowBase != null)
            {
                H8Memory.FreeRaw(_emergencyOverflowBase, Allocator.Persistent, SystemID.CoreDataVault);
                _emergencyOverflowBase = null;
            }
            if (_readOnlyDummyBase != null)
            {
                H8Memory.FreeRaw(_readOnlyDummyBase, Allocator.Persistent, SystemID.CoreDataVault);
                _readOnlyDummyBase = null;
            }

            if (_keys.IsCreated)
                _keys.Dispose();
            if (_buffers.IsCreated)
                _buffers.Dispose();
            if (_metadata.IsCreated)
                _metadata.Dispose();
            if (_metadataByBufferId.IsCreated)
            {
                H8Memory.Release(ref _metadataByBufferId, SystemID.CoreDataVault);
            }
            if (_blocks.IsCreated)
                _blocks.Dispose();
            if (_defragBlackBox.IsCreated)
            {
                H8Memory.Release(ref _defragBlackBox, SystemID.CoreDataVault);
            }
            if (_lastRelocationRecords.IsCreated)
            {
                H8Memory.Release(ref _lastRelocationRecords, SystemID.CoreDataVault);
            }
            if (_memoryBudgetEntries.IsCreated)
            {
                H8Memory.Release(ref _memoryBudgetEntries, SystemID.CoreDataVault);
            }
            _allocatedBytes = 0L;
            _arenaBytes = 0L;
            _arenaCapacityLimitBytes = 0L;
            _allocationLock = 0;
            _compactionFence = 0;
            _activeLocks = 0;
            _mutationGuardMaskLow = 0;
            _mutationGuardMaskHigh = 0;
            _emergencyOverflowCursorBytes = 0;
            _memoryStarvationWarnings = 0;
            _defragBlackBoxCursor = 0;
            _defragBlackBoxRecordedCount = 0;
            _lastRelocationRecordCount = 0;
            _memoryBudgetCount = 0;
            _generationHandleMissCount = 0;
            _lastFaultBufferId = 0;
            _lastFaultHandleGeneration = 0u;
            _lastFaultMetaGeneration = 0u;
            _resolvedHandleCount = 0L;
            _resolutionTickAccumulator = 0L;
            _forceDefragRequested = 0;
            _compactionWatchdogBreachCount = 0;
            _totalDefragMovedBytes = 0L;
            _defragTickSequence = 0u;
            _lastPublishedPointerBits = 0L;
            _lastRelocatedSystemId = SystemID.Unknown;
            _vaultGenerationId = 0u;
            _defragDumpWritten = false;
            _phiVodDumpWritten = false;
            _shinobu202DumpWritten = false;
            ResetDefragTelemetry();
            _initialized = false;
            if (ReferenceEquals(_latestCreated, this))
                _latestCreated = null;
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
            if (_macroDatabasePayloadAccessTicks.IsCreated)
                _macroDatabasePayloadAccessTicks.Dispose();
            if (_macroDatabasePayloadCache.IsCreated)
                _macroDatabasePayloadCache.Dispose();

            _macroDatabasePayloadBytes = 0L;
            _macroDatabasePayloadEvictions = 0;
            _macroDatabaseCacheAccessClock = 0u;
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

            _macroDatabasePayloadKeys.AddNoResize(sectorHash);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadFlatMetadata(int key, out VaultBufferMeta meta)
        {
            meta = default;
            if (!_metadataByBufferId.IsCreated || (uint)key >= (uint)_metadataByBufferId.Length)
                return false;

            meta = _metadataByBufferId[key];
            return meta.BufferKey == key && meta.Version != 0u && meta.Length > 0;
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
            ClearFlatMetadata(key, tombstoneGeneration);
        }

        private uint ResolveInitialGenerationForAllocation(int key)
        {
            uint previous = ReadFlatMetadataGeneration(key);
            return previous == 0u ? 1u : NextGeneration(previous);
        }

        private uint ResolveTombstoneGeneration(int key)
        {
            uint previous = ReadFlatMetadataGeneration(key);
            return previous == 0u ? 1u : NextGeneration(previous);
        }

        private uint ReadFlatMetadataGeneration(int key)
        {
            if (!_metadataByBufferId.IsCreated || (uint)key >= (uint)_metadataByBufferId.Length)
                return 0u;

            return _metadataByBufferId[key].Version;
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

            _keys.AddNoResize(key);
            return true;
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
                    DumpPhiVodBlackBox();
                    continue;
                }

                releasedBytes += meta.Bytes;
                _buffers.Remove(key);
                RemoveMetadata(key);
                RemoveBufferKey(key);
                FreeBlock(blockIndex, clearPayload: true);
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
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                DumpPhiVodBlackBox();
                return false;
            }

            VaultBufferMeta tombstone = meta;
            tombstone.Version = NextGeneration(tombstone.Version);
            tombstone.RefCount = 0u;
            tombstone.Flags |= VaultMetaFlagOrphanCandidate;
            WriteMetadata(key, in tombstone);

            releasedBytes = meta.Bytes;
            _buffers.Remove(key);
            RemoveMetadata(key);
            RemoveBufferKey(key);
            FreeBlock(blockIndex, clearPayload: true);
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
                if (!_macroDatabasePayloadCache.TryGetValue(candidateHash, out MacroDatabasePayloadHandle candidate))
                    continue;

                if ((candidate.Flags & MacroDatabasePayloadDirtyFlag) != 0)
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
            return math.lerp(LowTierFragmentationRatioThreshold, HighTierFragmentationRatioThreshold, curve01);
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
            if (_memMoveBlockedByStress ||
                _allocationLock != 0 ||
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
                for (int i = 0; i + 1 < _blocks.Length && movedBytes < MaxLiveDefragMoveBytesPerSlice; i++)
                {
                    if (HasActiveBurstLocks(activeBurstLockMask))
                    {
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
                        LastDefragFlags = (byte)(LastDefragFlags | DefragFlagAliasBlocked);
                        continue;
                    }

                    if ((occupiedBlock.Reserved0 & BlockFlagExternalView) != 0)
                    {
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

                    if (!TryMoveOccupiedBlockLeft(i, i + 1, ref movedBytes))
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

        private bool TryMoveOccupiedBlockLeft(int freeIndex, int occupiedIndex, ref long movedBytes)
        {
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
                !_buffers.TryGetValue(key, out IntPtr oldPointer) ||
                oldPointer == IntPtr.Zero ||
                meta.BlockIndex != occupiedIndex ||
                meta.OffsetBytes != occupiedBlock.OffsetBytes ||
                meta.Bytes != occupiedBlock.Bytes)
            {
                return false;
            }

            IntPtr expectedOldPointer = (IntPtr)((byte*)_arenaBase + occupiedBlock.OffsetBytes);
            if (oldPointer != expectedOldPointer)
                return false;

            IntPtr newPointer = (IntPtr)((byte*)_arenaBase + freeBlock.OffsetBytes);
            UnsafeUtility.MemMove(newPointer.ToPointer(), oldPointer.ToPointer(), occupiedBlock.Bytes);

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
            meta.Version = movedBlock.Version;
            WriteMetadata(key, in meta);
            PublishMovedBufferPointer(key, newPointer);
            RecordRelocation(key, in meta, oldPointer, newPointer, occupiedBlock.Bytes, movedBlock.Version);
            movedBytes += occupiedBlock.Bytes;

            if (occupiedIndex + 1 < _blocks.Length && IsFree(occupiedIndex) && IsFree(occupiedIndex + 1))
                MergeFreeBlocks(occupiedIndex, occupiedIndex + 1);

            return true;
        }

        private bool MarkExternalView(int key, long offsetBytes)
        {
            if (!TryFindOccupiedBlockIndex(key, offsetBytes, out int blockIndex))
                return false;

            VaultArenaBlock block = _blocks[blockIndex];
            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta))
                return false;

            if ((block.Reserved0 & BlockFlagExternalView) != 0)
            {
                if (meta.BlockIndex != blockIndex ||
                    meta.OffsetBytes != block.OffsetBytes ||
                    meta.Version != block.Version)
                {
                    meta.BlockIndex = blockIndex;
                    meta.OffsetBytes = block.OffsetBytes;
                    meta.Version = block.Version;
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
            meta.Version = block.Version;
            WriteMetadata(key, in meta);
            BumpVaultGeneration();
            return true;
        }

        private bool MarkAliasReader(int key, SystemID requester)
        {
            if (requester == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAliasReader();

            if (!_metadata.TryGetValue(key, out VaultBufferMeta meta) ||
                !TryFindOccupiedBlockIndex(key, meta.OffsetBytes, out _))
            {
                return false;
            }

            meta.LastAliasRequester = requester;
            WriteMetadata(key, in meta);
            return true;
        }

        private void RecordDefragBlackBox(uint sequence, byte extraFlags = 0)
        {
            if (!_defragBlackBox.IsCreated || _defragBlackBox.Length == 0)
                return;

            int cursor = _defragBlackBoxCursor;
            if ((uint)cursor >= (uint)_defragBlackBox.Length)
                cursor = 0;

            MemoryDefragTelemetryEntry entry = default;
            entry.Sequence = sequence;
            entry.Frame = H8Memory.ResolveTelemetryFrame(sequence);
            entry.VaultGenerationID = _vaultGenerationId;
            entry.ActiveBurstLockMask = ActiveBurstLockMask;
            entry.ActiveMutationGuardMask = ActiveMutationGuardMask;
            entry.BlockCount = _blocks.IsCreated ? _blocks.Length : 0;
            entry.ActiveBufferCount = _keys.IsCreated ? _keys.Length : 0;
            entry.TotalFreeSpaceBytes = TotalFreeSpaceBytes;
            entry.LargestContiguousBlockBytes = LargestContiguousBlockBytes;
            entry.LastMovedBytes = LastDefragMovedBytes;
            entry.TotalMovedBytes = _totalDefragMovedBytes;
            entry.PendingMassiveMoveBytes = PendingMassiveMoveBytes;
            entry.HeapFragmentationRatio = HeapFragmentationRatio;
            entry.WatchdogBreaches = _compactionWatchdogBreachCount;
            entry.EmergencyOverflowCursorBytes = Volatile.Read(ref _emergencyOverflowCursorBytes);
            entry.LastRelocatedSystemId = (ushort)_lastRelocatedSystemId;
            entry.Flags = (byte)(LastDefragFlags | extraFlags);
            entry.IsFragmented = IsFragmented ? (byte)1 : (byte)0;
            entry.WatchdogExceeded = LastDefragWatchdogExceeded ? (byte)1 : (byte)0;
            entry.MemoryStarvationWarnings = _memoryStarvationWarnings;
            entry.GenerationMismatchCount = unchecked((uint)Volatile.Read(ref _generationHandleMissCount));
            entry.ResolutionTicks = Volatile.Read(ref _resolutionTickAccumulator);
            entry.ResolvedHandleCount = Volatile.Read(ref _resolvedHandleCount);
            entry.LastFaultBufferID = Volatile.Read(ref _lastFaultBufferId);
            entry.LastFaultHandleGeneration = _lastFaultHandleGeneration;
            entry.LastFaultMetaGeneration = _lastFaultMetaGeneration;
            entry.Reserved32 =
                ((uint)(ushort)Volatile.Read(ref _lastOrphanSweepCandidateCount) << 16) |
                (ushort)Volatile.Read(ref _lastOrphanReclaimCount);
            _defragBlackBox[cursor] = entry;

            cursor++;
            if (cursor >= _defragBlackBox.Length)
                cursor = 0;
            _defragBlackBoxCursor = cursor;
            if (_defragBlackBoxRecordedCount < _defragBlackBox.Length)
                _defragBlackBoxRecordedCount++;
        }

        private void DumpDefragBlackBox()
        {
            if (_defragDumpWritten || !_defragBlackBox.IsCreated)
                return;

            try
            {
                string directory = Path.GetDirectoryName(DefragDumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                WriteDefragBlackBoxFile(DefragDumpPath);
                WriteDefragBlackBoxFile(ShinobuDumpPath);
                WriteDefragBlackBoxFile(ShinobuH8DumpPath);
                WriteDefragBlackBoxFile(Shinobu202DumpPath);

                _defragDumpWritten = true;
            }
            catch
            {
            }
        }

        private void DumpPhiVodBlackBox()
        {
            if (_phiVodDumpWritten || !_defragBlackBox.IsCreated)
                return;

            try
            {
                string directory = Path.GetDirectoryName(PhiVodDumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                WriteDefragBlackBoxFile(PhiVodDumpPath);
                WriteDefragBlackBoxFile(ShinobuDumpPath);
                WriteDefragBlackBoxFile(ShinobuH8DumpPath);
                WriteDefragBlackBoxFile(Shinobu202DumpPath);

                _phiVodDumpWritten = true;
            }
            catch
            {
            }
        }

        private void DumpShinobu202BlackBox()
        {
            if (_shinobu202DumpWritten || !_defragBlackBox.IsCreated)
                return;

            try
            {
                string directory = Path.GetDirectoryName(Shinobu202DumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                WriteDefragBlackBoxFile(Shinobu202DumpPath);
                _shinobu202DumpWritten = true;
            }
            catch
            {
            }
        }

        private void WriteDefragBlackBoxFile(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int entrySize = UnsafeUtility.SizeOf<MemoryDefragTelemetryEntry>();
                int recordedCount = _defragBlackBoxRecordedCount;
                if (recordedCount < 0)
                    recordedCount = 0;
                if (recordedCount > _defragBlackBox.Length)
                    recordedCount = _defragBlackBox.Length;

                writer.Write(DefragDumpMagic);
                writer.Write(DefragDumpVersion);
                writer.Write(recordedCount);
                writer.Write(entrySize);
                writer.Write(_defragBlackBox.Length);
                writer.Flush();

                if (recordedCount == 0)
                    return;

                int start = recordedCount < _defragBlackBox.Length ? 0 : _defragBlackBoxCursor;
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_defragBlackBox);
                for (int i = 0; i < recordedCount; i++)
                {
                    int index = start + i;
                    if (index >= _defragBlackBox.Length)
                        index -= _defragBlackBox.Length;

                    stream.Write(new ReadOnlySpan<byte>((byte*)source + (index * entrySize), entrySize));
                }
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
            if (!IsPointerAligned(pointer, VaultBlockAlignment))
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagUnaligned);
                DumpPhiVodBlackBox();
                return false;
            }

            handle.ptr = pointer.ToPointer();
            handle.generation = meta.Version;
            handle.BufferId = bufferId;
            handle.Length = meta.Length;
            handle.Stride = meta.Stride;
            return true;
        }

        private bool TryBuildGenerationHandle<T>(BufferID bufferId, out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            int key = (int)bufferId;
            if (key == 0 || !TryReadMetadata(key, out VaultBufferMeta meta) || meta.Length <= 0)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            ValidateType<T>(bufferId, meta, stride, alignment);
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

        private static bool CanRefreshHandleAfterGenerationBump<T>(
            in VaultBufferHandle<T> handle,
            in VaultBufferMeta meta,
            IntPtr pointer) where T : struct
        {
            if (pointer == IntPtr.Zero ||
                handle.BufferId == BufferID.Unknown ||
                handle.ptr == null ||
                handle.generation == 0u)
            {
                return false;
            }

            if (meta.Version <= handle.generation)
                return false;

            if (handle.Stride != 0 && handle.Stride != meta.Stride)
                return false;

            return handle.Length <= 0 || meta.Length >= handle.Length;
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
            double bytes = LowTierArenaLimitBytes +
                           ((double)(HighTierArenaLimitBytes - LowTierArenaLimitBytes) * curve01);
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
            double range = HighTierArenaLimitBytes - LowTierArenaLimitBytes;
            if (range <= 0.0d)
                return 0f;

            return math.saturate((float)((arenaCapacityLimitBytes - LowTierArenaLimitBytes) / range));
        }

        private static long ResolveArenaCapacityLimit(long requestedLimitBytes)
        {
            long safeLimit = requestedLimitBytes;
            if (safeLimit <= 0L)
                safeLimit = LowTierArenaLimitBytes;
            safeLimit = AlignUp(safeLimit, VaultBlockAlignment);
            if (safeLimit < DefaultArenaBytes)
                return AlignUp(DefaultArenaBytes, VaultBlockAlignment);
            return Math.Min(safeLimit, HighTierArenaLimitBytes);
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
                _allocationLock != 0 ||
                _compactionFence != 0 ||
                _arenaBytes >= _arenaCapacityLimitBytes)
            {
                return false;
            }

            int lastIndex = _blocks.Length - 1;
            if (lastIndex < 0)
                return false;
            if (_blocks[lastIndex].State != BlockStateFree && _blocks.Length >= _blocks.Capacity)
                return false;

            long desiredMinimum = _allocatedBytes + requiredContiguousBytes + ArenaGrowSlackBytes;
            if (desiredMinimum < requiredContiguousBytes)
                desiredMinimum = requiredContiguousBytes;

            long doubled = _arenaBytes <= HighTierArenaLimitBytes / 2L
                ? _arenaBytes << 1
                : _arenaCapacityLimitBytes;
            long desiredBytes = math.max(doubled, desiredMinimum);
            desiredBytes = AlignUp(desiredBytes, VaultBlockAlignment);
            if (desiredBytes > _arenaCapacityLimitBytes)
                desiredBytes = _arenaCapacityLimitBytes;
            if (desiredBytes <= _arenaBytes)
                return false;

            return TryGrowArena(desiredBytes);
        }

        private bool TryGrowArena(long newArenaBytes)
        {
            newArenaBytes = AlignUp(newArenaBytes, VaultBlockAlignment);
            if (newArenaBytes <= _arenaBytes || newArenaBytes > _arenaCapacityLimitBytes)
                return false;
            if (_memMoveBlockedByStress)
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagStressHalt);
                return false;
            }

            void* oldBase = _arenaBase;
            long oldArenaBytes = _arenaBytes;
            void* newBase = H8Memory.ReallocateRaw(
                oldBase,
                oldArenaBytes,
                newArenaBytes,
                VaultBlockAlignment,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                clearExtendedBytes: true,
                H8AllocationFlags.Vault | H8AllocationFlags.SubAllocatorRoot);
            if (newBase == null)
                return false;

            _arenaBase = newBase;
            _arenaBytes = newArenaBytes;
            long growBytes = newArenaBytes - oldArenaBytes;
            if (!ExtendFreeTail(growBytes))
            {
                LastDefragFlags = (byte)(LastDefragFlags | DefragFlagFault);
                DumpPhiVodBlackBox();
                return false;
            }

            ResetRelocationRecords();
            RefreshBlocksAfterArenaRelocation(oldBase, newBase);
            LastDefragMovedBytes = oldArenaBytes;
            _totalDefragMovedBytes += oldArenaBytes;
            LastDefragFlags = (byte)(LastDefragFlags | DefragFlagRelocated);
            BumpVaultGeneration();
            return true;
        }

        private bool ExtendFreeTail(long growBytes)
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

            VaultArenaBlock freeTail = new VaultArenaBlock
            {
                OffsetBytes = last.OffsetBytes + last.Bytes,
                Bytes = growBytes,
                BufferKey = 0,
                Version = 1u,
                State = BlockStateFree,
                Reserved0 = 0,
                Reserved1 = 0
            };
            int descriptorIndex = H8Memory.RegisterBlockDescriptor(BuildDescriptor(in freeTail));
            if (descriptorIndex < 0)
                return false;

            freeTail.H8BlockIndex = descriptorIndex;
            _blocks.AddNoResize(freeTail);
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
                        meta.Version = block.Version;
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
            record.OldPointer = oldPointer.ToInt64();
            record.NewPointer = newPointer.ToInt64();
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

                InsertBlockAfter(i, in freeRemainder);
                _blocks[i] = occupiedBlock;
                UpdateH8Descriptor(in occupiedBlock);
                return true;
            }

            return false;
        }

        private void FreeBlock(int blockIndex, bool clearPayload = false)
        {
            if ((uint)blockIndex >= (uint)_blocks.Length)
                return;

            VaultArenaBlock block = _blocks[blockIndex];
            if (block.State != BlockStateOccupied)
                return;

            if (clearPayload && _arenaBase != null && block.Bytes > 0L && block.OffsetBytes <= _arenaBytes - block.Bytes)
                UnsafeUtility.MemClear((byte*)_arenaBase + block.OffsetBytes, block.Bytes);

            block.BufferKey = 0;
            block.State = BlockStateFree;
            block.Reserved0 = 0;
            block.Reserved1 = 0;
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

        private static void ValidateType<T>(BufferID bufferId, VaultBufferMeta meta, int stride, int alignment) where T : struct
        {
            uint typeHash = ComputeTypeHash<T>();
            if (meta.Stride != stride ||
                meta.Alignment != alignment ||
                (meta.TypeHash != 0u && meta.TypeHash != typeHash))
            {
                FatalMemoryException.ThrowVaultTypeMismatch();
            }
        }

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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct VaultDefragmentationJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public void* ArenaBase;
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
