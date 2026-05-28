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

            StringAssert.Contains("return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID)", release);
            StringAssert.Contains("return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0)", blockRelease);
            StringAssert.Contains("if (kind == DeferredReleaseKindWriter)", queue);
            StringAssert.Contains("Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)", queue);
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

        private static void AssertMethodChecksFence(string source, string signature)
        {
            string block = ExtractMethod(source, signature);
            Assert.That(
                block.Contains("Volatile.Read(ref _compactionFence)") ||
                block.Contains("TryResolveHandle(in baseHandle"),
                signature);
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
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
