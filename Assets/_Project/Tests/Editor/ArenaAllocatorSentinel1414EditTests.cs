using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class ArenaAllocatorSentinel1414EditTests
    {
        [Test]
        public void GlobalDataVault_ReallocateRaw_CallRequiresRelocationGuard()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");

            StringAssert.Contains("in H8RawReallocationGuard relocationGuard", h8Memory);
            StringAssert.Contains("if (!relocationGuard.AllowsRelocation)", h8Memory);
            StringAssert.Contains("H8RawReallocationGuard.Create", vault);
            StringAssert.Contains("ActiveBurstLockMask", vault);
            StringAssert.Contains("HasPinnedExternalViews()", vault);
        }

        [Test]
        public void GlobalDataVault_DeferredGrowth_ClearUsesCompareExchange()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string queueBlock = ExtractMethod(vault, "private void QueueDeferredArenaGrowth");
            string clearBlock = ExtractMethod(vault, "private void ClearDeferredArenaGrowthIfSatisfied");
            string processBlock = ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth");

            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes", queueBlock);
            StringAssert.Contains("if (observed >= requiredBytes)", queueBlock);
            StringAssert.Contains("CanSatisfyContiguousFreeBlock(observed)", clearBlock);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, 0L, observed)", clearBlock);
            StringAssert.Contains("HasActiveBurstLocks(0u)", processBlock);
            StringAssert.Contains("HasPinnedExternalViews()", processBlock);
        }

        [Test]
        public void GlobalDataVault_PublicPointerRoutes_CheckCompactionFence()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            AssertMethodChecksFence(vault, "public bool TryResolveHandle<T>");
            AssertMethodChecksFence(vault, "public bool TryReadHandle<T>");
            AssertMethodChecksFence(vault, "public bool TryReadOnlyHandle<T>");
            AssertMethodChecksFence(vault, "public bool TryResolveSlice<T>");
            AssertMethodChecksFence(vault, "public bool TryAcquireWriteLock<T>");
            AssertMethodChecksFence(vault, "public bool TryLockBuffer(BufferID bufferId, SystemID lockOwner)");
        }

        [Test]
        public void GlobalDataVault_SparseBufferIdMetadataReadFallsBackToMap()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string metadataRead = ExtractMethod(vault, "private bool TryReadFlatMetadata");

            StringAssert.Contains("(uint)key < (uint)_metadataByBufferId.Length", metadataRead);
            StringAssert.Contains("_metadata.TryGetValue(key, out meta)", metadataRead);
        }

        [Test]
        public void GlobalDataVault_SparseBufferIdWriteLocksAvoidFlatArrayReject()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string acquire = ExtractMethod(vault, "public bool TryAcquireWriteLock<T>");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string queue = ExtractMethod(vault, "private bool QueueDeferredRelease");

            Assert.IsFalse(acquire.Contains("(uint)key >= (uint)_metadataByBufferId.Length"));
            Assert.IsFalse(release.Contains("(uint)key >= (uint)_metadataByBufferId.Length"));
            Assert.IsFalse(queue.Contains("(uint)bufferKey >= (uint)_metadataByBufferId.Length"));
            StringAssert.Contains("WriteMetadata(key, in meta)", acquire);
            StringAssert.Contains("WriteMetadata(key, in meta)", release);
            StringAssert.Contains("TryReadFlatMetadata(bufferKey, out _)", queue);
        }

        [Test]
        public void GlobalDataVault_DeferredReleaseAcceptanceAndDeduplicationStayContractSafe()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string blockRelease = ExtractMethod(vault, "private bool ReleaseWriterBlockLock");
            string queue = ExtractMethod(vault, "private bool QueueDeferredRelease");

            StringAssert.Contains("bool queuedRelease = QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID)", release);
            StringAssert.Contains("bool queuedRelease = QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0)", blockRelease);
            StringAssert.Contains("return queuedRelease;", release);
            StringAssert.Contains("return queuedRelease;", blockRelease);
            StringAssert.Contains("if (kind == DeferredReleaseKindWriter)", queue);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)", queue);
            StringAssert.Contains("finally", queue);
            StringAssert.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0)", queue);
            StringAssert.Contains("pending->Kind == DeferredReleaseKindWriter", queue);
            Assert.IsFalse(queue.Contains("pending->Kind == kind"));
            StringAssert.Contains("pending->LockOwnerSystemId == lockOwnerSystemId", queue);
        }

        [Test]
        public void GlobalDataVault_NewAllocationPublishAndRollbackStayUnderOneMutationGate()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string ensure = ExtractMethod(vault, "private bool TryEnsureVaultBuffer<T>");
            string publish = ExtractMethod(vault, "private bool TryAllocatePublishedBuffer<T>");

            StringAssert.Contains("TryAllocatePublishedBuffer<T>", ensure);
            StringAssert.Contains("TryEnterBlockMutationGate()", publish);
            StringAssert.Contains("TryAllocateBlockLocked(key, requiredBytes", publish);
            StringAssert.Contains("_buffers.TryAdd(key, pointer)", publish);
            StringAssert.Contains("SanitizeFinitePayload<T>(pointer, requiredLength)", publish);
            Assert.Less(
                publish.IndexOf("SanitizeFinitePayload<T>(pointer, requiredLength)", StringComparison.Ordinal),
                publish.IndexOf("_buffers.TryAdd(key, pointer)", StringComparison.Ordinal));
            StringAssert.Contains("TryAddMetadata(key, in meta)", publish);
            StringAssert.Contains("EnsureBufferKeyRegistered(key)", publish);
            StringAssert.Contains("MarkExternalViewLocked(key, meta.OffsetBytes)", publish);
            StringAssert.Contains("if (!success && blockAllocated)", publish);
            StringAssert.Contains("_allocatedBytes = _allocatedBytes > requiredBytes ? _allocatedBytes - requiredBytes : 0L", publish);
            StringAssert.Contains("_buffers.Remove(key)", publish);
            StringAssert.Contains("RemoveMetadata(key)", publish);
            StringAssert.Contains("RemoveBufferKey(key)", publish);
            StringAssert.Contains("FreeBlockLocked(blockIndex, clearPayload: true)", publish);
            StringAssert.Contains("finally", publish);
            StringAssert.Contains("ReleaseBlockMutationGate()", publish);
        }

        [Test]
        public void GlobalDataVault_SparseBufferIdGenerationSurvivesRelease()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string initialize = ExtractMethod(vault, "public void Initialize");
            string addMetadata = ExtractMethod(vault, "private bool TryAddMetadata");
            string writeMetadata = ExtractMethod(vault, "private void WriteMetadata");
            string removeMetadata = ExtractMethod(vault, "private void RemoveMetadata");
            string resolveInitial = ExtractMethod(vault, "private uint ResolveInitialGenerationForAllocation");
            string readGeneration = ExtractMethod(vault, "private uint ReadMetadataGeneration");
            string writeGeneration = ExtractMethod(vault, "private void WriteMetadataGeneration");

            StringAssert.Contains("_metadataGenerationByBufferId = new UnsafeHashMap<int, uint>", initialize);
            StringAssert.Contains("WriteMetadataGeneration(key, stored.Version)", addMetadata);
            StringAssert.Contains("WriteMetadataGeneration(key, stored.Version)", writeMetadata);
            StringAssert.Contains("WriteMetadataGeneration(key, tombstoneGeneration)", removeMetadata);
            StringAssert.Contains("ReadMetadataGeneration(key)", resolveInitial);
            StringAssert.Contains("_metadataGenerationByBufferId.TryGetValue(key, out uint generation)", readGeneration);
            StringAssert.Contains("(uint)key < (uint)_metadataByBufferId.Length", writeGeneration);
        }

        [Test]
        public void GlobalDataVault_DeferredGrowthChecksBurstLocksOnce()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string processBlock = ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth");
            int first = processBlock.IndexOf("HasActiveBurstLocks(0u)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(first, 0);
            Assert.AreEqual(-1, processBlock.IndexOf("HasActiveBurstLocks(0u)", first + 1, StringComparison.Ordinal));
        }

        [Test]
        public void SystemDispatcher_DeferredArenaGrowthUsesCachedPostSimulationPath()
        {
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/SystemDispatcher.cs");
            string postSimulation = ExtractMethod(dispatcher, "private void RunMasterPostSimulationPhase");
            string processDeferred = ExtractMethod(dispatcher, "private void ProcessDeferredArenaGrowthPostSimulation");

            Assert.Less(
                postSimulation.IndexOf("system.PostSimulationTick(in timing)", StringComparison.Ordinal),
                postSimulation.IndexOf("ProcessDeferredArenaGrowthPostSimulation()", StringComparison.Ordinal));
            StringAssert.Contains("IDataVault dataVault = _dataVault;", processDeferred);
            StringAssert.Contains("TryResolveCachedDataVault(out dataVault)", processDeferred);
            StringAssert.Contains("_dataVault = dataVault;", processDeferred);
            StringAssert.Contains("globalDataVault.ProcessDeferredArenaGrowth();", processDeferred);
            Assert.IsFalse(processDeferred.Contains("GlobalRegistry.Get"));
            Assert.IsFalse(processDeferred.Contains("GetComponent"));
        }

        [Test]
        public void GlobalDataVault_WriteLockMutationGatesStayFlatAndFinallyReleased()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string acquire = ExtractMethod(vault, "public bool TryAcquireWriteLock<T>");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string queue = ExtractMethod(vault, "private bool QueueDeferredRelease");

            Assert.AreEqual(1, CountOccurrences(acquire, "TryEnterBlockMutationGate()"));
            StringAssert.Contains("finally", acquire);
            StringAssert.Contains("ReleaseBlockMutationGate();", acquire);
            Assert.IsFalse(acquire.Contains("TryEnterReleaseMutationGate()"));

            Assert.AreEqual(1, CountOccurrences(release, "TryEnterReleaseMutationGate()"));
            StringAssert.Contains("bool queuedRelease = QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID)", release);
            StringAssert.Contains("return queuedRelease;", release);
            StringAssert.Contains("finally", release);
            StringAssert.Contains("ReleaseBlockMutationGate();", release);
            Assert.IsFalse(release.Contains("TryEnterBlockMutationGate()"));

            StringAssert.Contains("enqueueGateAcquired = Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) == 0", queue);
            Assert.IsFalse(queue.Contains("Thread.SpinWait"));
            Assert.IsFalse(queue.Contains("while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate"));
            StringAssert.Contains("finally", queue);
            StringAssert.Contains("Volatile.Write(ref _deferredReleaseEnqueueGate, 0)", queue);
        }

        [Test]
        public void GlobalDataVault_WriteLockThreadSlotsPreventNestedWritersAndReleaseInFinally()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string initialize = ExtractMethod(vault, "public void Initialize");
            string dispose = ExtractMethod(vault, "public void Dispose");
            string acquire = ExtractMethod(vault, "public bool TryAcquireWriteLock<T>");
            string release = ExtractMethod(vault, "public bool ReleaseWriteLock<T>");
            string rollback = ExtractMethod(vault, "private bool RollbackWriterLockUnlocked");
            string drain = ExtractMethod(vault, "private bool DrainDeferredWriterReleaseLocked");
            string reserveSlot = ExtractMethod(vault, "private bool TryReserveThreadWriterSlot");
            string releaseSlot = ExtractMethod(vault, "private bool ReleaseThreadWriterSlotForLock");

            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 24)]", vault);
            StringAssert.Contains("private const int WriterThreadLockSlotCapacity = 128", vault);
            StringAssert.Contains("private NativeArray<VaultThreadWriteLockSlot> _writerThreadLockSlots;", vault);
            StringAssert.Contains("_writerThreadLockSlots = H8Memory.Allocate<VaultThreadWriteLockSlot>", initialize);
            StringAssert.Contains("_writerThreadLockSlots.IsCreated", initialize);
            StringAssert.Contains("H8Memory.Release(ref _writerThreadLockSlots, SystemID.CoreDataVault)", dispose);

            StringAssert.Contains("int writerThreadId = Thread.CurrentThread.ManagedThreadId;", acquire);
            StringAssert.Contains("writerSlotOffsetBytes = meta.OffsetBytes;", acquire);
            StringAssert.Contains("TryReserveThreadWriterSlot(writerThreadId, key, writerSlotOffsetBytes, (int)systemID)", acquire);
            StringAssert.Contains("bool releaseThreadWriterSlot = false;", acquire);
            Assert.Less(
                acquire.IndexOf("TryEnterBlockMutationGate()", StringComparison.Ordinal),
                acquire.IndexOf("TryReserveThreadWriterSlot(writerThreadId, key, writerSlotOffsetBytes, (int)systemID)", StringComparison.Ordinal));
            StringAssert.Contains("finally", acquire);
            StringAssert.Contains("if (releaseThreadWriterSlot)", acquire);
            StringAssert.Contains("ReleaseThreadWriterSlotForLock(key, writerSlotOffsetBytes, (int)systemID)", acquire);
            Assert.AreEqual(1, CountOccurrences(acquire, "TryReserveThreadWriterSlot("));

            StringAssert.Contains("state != WriterThreadLockSlotStateEmpty", reserveSlot);
            StringAssert.Contains("Volatile.Read(ref slot->ThreadId) == threadId", reserveSlot);
            StringAssert.Contains("slot->OffsetBytes = offsetBytes;", reserveSlot);
            StringAssert.Contains("Interlocked.CompareExchange(", reserveSlot);
            StringAssert.Contains("Volatile.Write(ref slot->State, WriterThreadLockSlotStateActive)", reserveSlot);
            StringAssert.Contains("Volatile.Read(ref slot->OffsetBytes) != offsetBytes", releaseSlot);
            StringAssert.Contains("slot->OffsetBytes = 0L;", releaseSlot);
            StringAssert.Contains("Volatile.Write(ref slot->State, WriterThreadLockSlotStateEmpty)", releaseSlot);

            StringAssert.Contains("ReleaseThreadWriterSlotForLock(key, meta.OffsetBytes, (int)systemID)", release);
            StringAssert.Contains("ReleaseThreadWriterSlotForLock(bufferKey, offsetBytes, systemID)", rollback);
            StringAssert.Contains("ReleaseThreadWriterSlotForLock(request.BufferKey, request.OffsetBytes, owner)", drain);
        }

        [Test]
        public void CoreMemoryHotPaths_DoNotResolveColdDependencies()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/SystemDispatcher.cs");

            AssertNoColdLookup(ExtractMethod(vault, "public bool TryAcquireWriteLock<T>"));
            AssertNoColdLookup(ExtractMethod(vault, "public bool ReleaseWriteLock<T>"));
            AssertNoColdLookup(ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth"));
            AssertNoColdLookup(ExtractMethod(vault, "private bool TryGrowArena("));
            AssertNoColdLookup(ExtractMethod(h8Memory, "private static int RegisterBlockDescriptorThreadSafe"));
            AssertNoColdLookup(ExtractMethod(h8Memory, "private static bool TryEnterBlockDescriptorMutationGate"));
            AssertNoColdLookup(ExtractMethod(sentinel, "private static int RegisterPointer("));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private void ProcessDeferredArenaGrowthPostSimulation"));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private void RunMasterVisualSyncPhase"));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private void RunDispatcherLateFrame"));
            AssertNoColdLookup(ExtractMethod(dispatcher, "private static bool TryResolveCachedDataVault"));
        }

        [Test]
        public void GlobalDataVault_DeferredGrowthStateTransferIsInterlockedAndAllocationFree()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string queue = ExtractMethod(vault, "private void QueueDeferredArenaGrowth");
            string clear = ExtractMethod(vault, "private void ClearDeferredArenaGrowthIfSatisfied");
            string process = ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth");
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/SystemDispatcher.cs");
            string bridge = ExtractMethod(dispatcher, "private void ProcessDeferredArenaGrowthPostSimulation");

            StringAssert.Contains("Volatile.Read(ref _deferredArenaGrowthBytes)", queue);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, requiredBytes, observed)", queue);
            StringAssert.Contains("Volatile.Read(ref _deferredArenaGrowthBytes)", clear);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredArenaGrowthBytes, 0L, observed)", clear);
            StringAssert.Contains("Volatile.Read(ref _deferredArenaGrowthBytes)", process);
            StringAssert.Contains("IDataVault dataVault = _dataVault;", bridge);
            AssertNoForbiddenManagedHotPathConstructs(queue);
            AssertNoForbiddenManagedHotPathConstructs(clear);
            AssertNoForbiddenManagedHotPathConstructs(process);
            AssertNoForbiddenManagedHotPathConstructs(bridge);
        }

        [Test]
        public void GlobalDataVault_AupAllocationLockUsesVolatilePublication()
        {
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string lockAllocations = ExtractMethod(vault, "public void LockAllocationsForAupShift");
            string unlockAllocations = ExtractMethod(vault, "public void UnlockAllocationsAfterAupShift");

            StringAssert.Contains("public bool IsAllocationLocked => Interlocked.Read(ref _allocationLock) != 0L;", vault);
            StringAssert.Contains("private long _allocationLock;", vault);
            Assert.IsFalse(vault.Contains("_lockedShiftFrameId"));
            Assert.IsFalse(vault.Contains("Volatile.Read(ref _allocationLock)"));
            StringAssert.Contains("private static long ResolveAllocationLockToken", vault);
            StringAssert.Contains("return shiftFrameId == 0u ? -1L : shiftFrameId;", vault);
            StringAssert.Contains("long lockToken = ResolveAllocationLockToken(shiftFrameId);", lockAllocations);
            Assert.Less(
                lockAllocations.IndexOf("long lockToken = ResolveAllocationLockToken(shiftFrameId);", StringComparison.Ordinal),
                lockAllocations.IndexOf("Interlocked.Exchange(ref _allocationLock, lockToken);", StringComparison.Ordinal));
            StringAssert.Contains("long observedLockToken = Interlocked.Read(ref _allocationLock);", unlockAllocations);
            StringAssert.Contains("if (observedLockToken == 0L)", unlockAllocations);
            StringAssert.Contains("Interlocked.Exchange(ref _allocationLock, 0L);", unlockAllocations);
            StringAssert.Contains("Interlocked.CompareExchange(ref _allocationLock, 0L, lockToken);", unlockAllocations);
            StringAssert.Contains("Interlocked.Read(ref _allocationLock) != 0L", ExtractMethod(vault, "private bool TryGrowArenaForBytes"));
            StringAssert.Contains("Interlocked.Read(ref _allocationLock) != 0L", ExtractMethod(vault, "public bool ProcessDeferredArenaGrowth"));
            StringAssert.Contains("Interlocked.Read(ref _allocationLock) != 0L", ExtractMethod(vault, "private bool TryRunLiveCompactionSlice"));
            Assert.AreEqual(0, CountOccurrences(vault, "_allocationLock != 0"));
            Assert.AreEqual(0, CountOccurrences(vault, "_allocationLock == 0"));
        }

        [Test]
        public void GlobalDataVault_ArenaGrowthPreflightsTailMetadataBeforeRawReallocate()
        {
            string h8Memory = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/H8Memory.cs");
            string vault = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs");
            string growForBytes = ExtractMethod(vault, "private bool TryGrowArenaForBytes");
            string growArena = ExtractMethod(vault, "private bool TryGrowArena(");
            string prepare = ExtractMethod(vault, "private bool TryPrepareArenaGrowthTailMetadata");
            string extend = ExtractMethod(vault, "private bool ExtendFreeTail");
            string reserveSlot = ExtractMethod(h8Memory, "internal static bool TryReserveBlockDescriptorSlot");
            string reserveSlotNoLock = ExtractMethod(h8Memory, "private static bool TryReserveBlockDescriptorSlotNoLock");
            string releaseSlot = ExtractMethod(h8Memory, "internal static void ReleaseReservedBlockDescriptor");
            string commitSlot = ExtractMethod(h8Memory, "internal static bool TryCommitReservedBlockDescriptor");
            string registerThreadSafe = ExtractMethod(h8Memory, "private static int RegisterBlockDescriptorThreadSafe");
            string updateDescriptor = ExtractMethod(h8Memory, "internal static bool TryUpdateBlockDescriptor");

            Assert.IsFalse(growForBytes.Contains("_blocks[lastIndex].State != BlockStateFree && _blocks.Length >= _blocks.Capacity"));
            StringAssert.Contains("return TryGrowArena(desiredBytes)", growForBytes);
            Assert.Less(
                growArena.IndexOf("if (!TryPrepareArenaGrowthTailMetadata(out reservedTailH8BlockIndex))", StringComparison.Ordinal),
                growArena.IndexOf("H8Memory.ReallocateRaw", StringComparison.Ordinal));
            StringAssert.Contains("H8Memory.TryReserveBlockDescriptorSlot(out reservedTailH8BlockIndex)", prepare);
            StringAssert.Contains("H8Memory.ReleaseReservedBlockDescriptor(reservedTailH8BlockIndex)", growArena);
            StringAssert.Contains("if (!TryEnterBlockDescriptorMutationGate())", reserveSlot);
            StringAssert.Contains("ReleaseBlockDescriptorMutationGate();", reserveSlot);
            StringAssert.Contains("BlockDescriptor reservation = default;", reserveSlotNoLock);
            StringAssert.Contains("TryReserveReusableBlockDescriptorSlot(in reservation, out index)", reserveSlotNoLock);
            StringAssert.Contains("EnsureBlockDescriptorCapacity(newCapacity)", reserveSlotNoLock);
            StringAssert.Contains("descriptor.State != (byte)H8BlockState.Reserved", releaseSlot);
            StringAssert.Contains("current.State != (byte)H8BlockState.Reserved", commitSlot);
            StringAssert.Contains("if (committed.Generation < nextGeneration)", commitSlot);
            StringAssert.Contains("return RegisterBlockDescriptorNoInit(in descriptor)", registerThreadSafe);
            StringAssert.Contains("if (!TryEnterBlockDescriptorMutationGate())", updateDescriptor);
            Assert.IsFalse(h8Memory.Contains("Thread.SpinWait"));
            Assert.IsFalse(extend.Contains("new VaultArenaBlock"));
            StringAssert.Contains("VaultArenaBlock freeTail = default;", extend);
            StringAssert.Contains("H8Memory.TryCommitReservedBlockDescriptor(descriptorIndex, BuildDescriptor(in freeTail))", extend);
        }

        [Test]
        public void NativeMemorySentinel_FatalLeakAssertion_IdentifiesLeakedBuffer()
        {
            NativeMemorySentinel.ResetForSubsystemReload();
            void* pointer = UnsafeUtility.Malloc(64, 16, Allocator.Persistent);
            int id = 0;

            try
            {
                id = NativeMemorySentinel.RegisterPointer(
                    pointer,
                    64,
                    "Agent1414SentinelTest",
                    "MockLeakedBuffer",
                    NativeAllocationLifetime.Session);

                FatalMemoryLeakException exception = Assert.Throws<FatalMemoryLeakException>(
                    () => NativeMemorySentinel.AssertNoAllocationsAfterServiceShutdown("Agent1414Test"));

                StringAssert.Contains("MockLeakedBuffer", exception.Message);
                StringAssert.Contains("Agent1414SentinelTest", exception.Message);
            }
            finally
            {
                if (id > 0)
                    NativeMemorySentinel.Unregister(id);
                if (pointer != null)
                    UnsafeUtility.Free(pointer, Allocator.Persistent);
                NativeMemorySentinel.ResetForSubsystemReload();
            }
        }

        [Test]
        public void NativeMemorySentinel_RegisterPathUsesFixedStringStorage()
        {
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string register = ExtractMethod(sentinel, "private static int RegisterPointer(");
            string persistent = ExtractMethod(sentinel, "private static void TrackPersistentReallocation");
            string findPersistent = ExtractMethod(sentinel, "private static int FindPersistentReallocationRecord");

            Assert.IsFalse(register.Contains("new NativeAllocationRecord"));
            Assert.IsFalse(persistent.Contains("new PersistentReallocationRecord"));
            Assert.IsFalse(register.Contains("string.Equals(existing.Owner"));
            Assert.IsFalse(findPersistent.Contains("string.Equals(record.Owner"));
            Assert.IsFalse(sentinel.Contains("string StackTrace"));
            Assert.IsFalse(sentinel.Contains("CaptureStackTrace"));
            Assert.IsFalse(sentinel.Contains("StackTraceUtility.ExtractStackTrace"));
            Assert.IsFalse(sentinel.Contains("public string Owner"));
            Assert.IsFalse(sentinel.Contains("public string Label"));
            Assert.IsFalse(sentinel.Contains("public bool LeakReported;"));
            Assert.IsFalse(sentinel.Contains("public bool Reported;"));
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 304)]", sentinel);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = 288)]", sentinel);
            StringAssert.Contains("[FieldOffset(0)] internal IntPtr Pointer", sentinel);
            StringAssert.Contains("[FieldOffset(16)] public FixedString128Bytes Owner", sentinel);
            StringAssert.Contains("[FieldOffset(144)] public FixedString128Bytes Label", sentinel);
            StringAssert.Contains("[FieldOffset(293)] private byte _leakReported", sentinel);
            StringAssert.Contains("[FieldOffset(280)] private byte _reported", sentinel);
            StringAssert.Contains("public FixedString128Bytes Owner", sentinel);
            StringAssert.Contains("public FixedString128Bytes Label", sentinel);
            StringAssert.Contains("NativeAllocationRecord record = default", register);
            StringAssert.Contains("PersistentReallocationRecord freshRecord = default", persistent);
            StringAssert.Contains("ToFixedString128(owner)", register);
            StringAssert.Contains("FixedStringEquals(in existing.Owner, in ownerFixed)", register);
            StringAssert.Contains("AppendFixedString(builder, in record.Owner)", sentinel);
        }

        [Test]
        public void NativeMemorySentinel_DiagnosticRecordReadsUseMutationGate()
        {
            string sentinel = ReadProjectFile("Assets/_Project/Scripts/Core/NativeMemorySentinel.cs");
            string snapshotCopy = ExtractMethod(sentinel, "internal static int CopySnapshotSources");
            string fatalMessage = ExtractMethod(sentinel, "private static string BuildFatalLeakMessage");

            StringAssert.Contains("EnterMutationGate();", snapshotCopy);
            StringAssert.Contains("finally", snapshotCopy);
            StringAssert.Contains("ExitMutationGate();", snapshotCopy);
            Assert.IsFalse(snapshotCopy.Contains("new NativeAllocationSnapshotSource"));
            StringAssert.Contains("NativeAllocationSnapshotSource snapshot = default;", snapshotCopy);

            StringAssert.Contains("EnterMutationGate();", fatalMessage);
            StringAssert.Contains("finally", fatalMessage);
            StringAssert.Contains("ExitMutationGate();", fatalMessage);
            StringAssert.Contains("builder.Append(_trackedBytes);", fatalMessage);
        }

        private static void AssertMethodChecksFence(string source, string signature)
        {
            string block = ExtractMethod(source, signature);
            Assert.That(
                block.Contains("Volatile.Read(ref _compactionFence)") ||
                block.Contains("TryResolveHandle(in baseHandle"),
                signature);
        }

        private static void AssertNoColdLookup(string block)
        {
            Assert.IsFalse(block.Contains("GlobalRegistry.Get<"));
            Assert.IsFalse(block.Contains("GetComponent<"));
            Assert.IsFalse(block.Contains("GetComponent("));
            Assert.IsFalse(block.Contains("TryGetComponent("));
        }

        private static void AssertNoForbiddenManagedHotPathConstructs(string block)
        {
            Assert.IsFalse(block.Contains("new "));
            Assert.IsFalse(block.Contains("string.Format"));
            Assert.IsFalse(block.Contains(".ToString("));
            Assert.IsFalse(block.Contains("foreach"));
            Assert.IsFalse(block.Contains(".Select("));
            Assert.IsFalse(block.Contains(".Where("));
            Assert.IsFalse(block.Contains(".ToArray("));
            Assert.IsFalse(block.Contains(".ToList("));
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(value, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += value.Length;
            }

            return count;
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);

            int brace = source.IndexOf((char)123, start);
            Assert.GreaterOrEqual(brace, 0, signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == (char)123)
                    depth++;
                else if (c == (char)125)
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail(signature);
            return string.Empty;
        }
    }
}
