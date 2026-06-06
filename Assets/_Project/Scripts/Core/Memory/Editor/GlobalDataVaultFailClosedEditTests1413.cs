#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Hecton8.Core.Contracts;
using Unity.Collections;

namespace Hecton8.Core.Memory.Editor
{
    [TestFixture]
    public static class GlobalDataVaultFailClosedEditTests1413
    {
        private const int Attempts = 10000;
        private const SystemID Owner = SystemID.CoreDataVault;
        private static readonly FieldInfo BlockMutationGateField = typeof(GlobalDataVault).GetField("_blockMutationGate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LockedSkipCountField = typeof(GlobalDataVault).GetField("_defragLockedSkipCount", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public static void TryAcquireWriteLock_FailsClosedWithoutGc_WhenMutationGateIsHeld()
        {
            Assert.NotNull(BlockMutationGateField);
            Assert.NotNull(LockedSkipCountField);

            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            {
                VaultGenerationHandle<int> handle = vault.EnsureGenerationHandle<int>(
                    BufferID.ShinobuCrashAtomicState,
                    16,
                    Owner,
                    NativeArrayOptions.ClearMemory);

                BlockMutationGateField.SetValue(vault, 1);
                try
                {
                    if (vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<int> warmup))
                        vault.ReleaseWriteLock(in handle, Owner);

                    long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    bool allFailed = true;
                    for (int i = 0; i < Attempts; i++)
                    {
                        allFailed &= !vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<int> buffer);
                        allFailed &= !buffer.IsCreated;
                    }

                    long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                    int lockedSkipCount = (int)LockedSkipCountField.GetValue(vault);
                    Assert.IsTrue(allFailed);
                    Assert.GreaterOrEqual(lockedSkipCount, Attempts);
                    Assert.AreEqual(0L, afterBytes - beforeBytes);
                }
                finally
                {
                    BlockMutationGateField.SetValue(vault, 0);
                }
            }
        }

        [Test]
        public static void TryAcquireWriteLock_RejectsNestedSameThreadWriterAndRecoversAfterRelease()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            {
                VaultGenerationHandle<int> firstHandle = vault.EnsureGenerationHandle<int>(
                    BufferID.ShinobuCrashAtomicState,
                    4,
                    Owner,
                    NativeArrayOptions.ClearMemory);
                VaultGenerationHandle<int> secondHandle = vault.EnsureGenerationHandle<int>(
                    BufferID.ShinobuCrashWatchdogCounters,
                    4,
                    Owner,
                    NativeArrayOptions.ClearMemory);

                Assert.IsTrue(vault.TryAcquireWriteLock(in firstHandle, Owner, out NativeArray<int> first));
                Assert.IsTrue(first.IsCreated);
                Assert.AreNotEqual(0u, vault.ActiveBurstLockMask);
                first[0] = 11;

                try
                {
                    long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    bool nestedAcquired = vault.TryAcquireWriteLock(in secondHandle, Owner, out NativeArray<int> nested);
                    long afterBytes = GC.GetAllocatedBytesForCurrentThread();

                    Assert.IsFalse(nestedAcquired);
                    Assert.IsFalse(nested.IsCreated);
                    Assert.AreNotEqual(0u, vault.ActiveBurstLockMask);
                    Assert.AreEqual(0L, afterBytes - beforeBytes);
                }
                finally
                {
                    Assert.IsTrue(vault.ReleaseWriteLock(in firstHandle, Owner));
                }
                Assert.AreEqual(0u, vault.ActiveBurstLockMask);

                Assert.IsTrue(vault.TryAcquireWriteLock(in secondHandle, Owner, out NativeArray<int> second));
                Assert.IsTrue(second.IsCreated);
                Assert.AreNotEqual(0u, vault.ActiveBurstLockMask);
                try
                {
                    second[0] = 22;
                }
                finally
                {
                    Assert.IsTrue(vault.ReleaseWriteLock(in secondHandle, Owner));
                }
                Assert.AreEqual(0u, vault.ActiveBurstLockMask);

                Assert.IsTrue(vault.TryReadOnlyHandle(in firstHandle, out NativeArray<int>.ReadOnly firstRead));
                Assert.IsTrue(vault.TryReadOnlyHandle(in secondHandle, out NativeArray<int>.ReadOnly secondRead));
                Assert.AreEqual(11, firstRead[0]);
                Assert.AreEqual(22, secondRead[0]);
            }
        }

        [Test]
        public static void MutationGuard_BlocksSameBufferWriterAndRecoversAfterRelease()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            {
                VaultGenerationHandle<int> handle = vault.EnsureGenerationHandle<int>(
                    BufferID.ShinobuCrashAtomicState,
                    4,
                    Owner,
                    NativeArrayOptions.ClearMemory);
                ulong guardMask = MutationGuardBit(BufferID.ShinobuCrashAtomicState);

                Assert.IsTrue(vault.TryAcquireMutationGuard(guardMask));
                Assert.AreEqual(guardMask, vault.ActiveMutationGuardMask);
                try
                {
                    long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    bool writerAcquired = vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<int> guardedWriter);
                    long afterBytes = GC.GetAllocatedBytesForCurrentThread();

                    Assert.IsFalse(writerAcquired);
                    Assert.IsFalse(guardedWriter.IsCreated);
                    Assert.AreEqual(guardMask, vault.ActiveMutationGuardMask);
                    Assert.AreEqual(0L, afterBytes - beforeBytes);
                }
                finally
                {
                    vault.ReleaseMutationGuard(guardMask);
                }
                Assert.AreEqual(0UL, vault.ActiveMutationGuardMask);

                Assert.IsTrue(vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<int> writer));
                try
                {
                    writer[0] = 33;
                }
                finally
                {
                    Assert.IsTrue(vault.ReleaseWriteLock(in handle, Owner));
                }

                Assert.IsTrue(vault.TryReadOnlyHandle(in handle, out NativeArray<int>.ReadOnly read));
                Assert.AreEqual(33, read[0]);
            }
        }

        [Test]
        public static void WriteLock_BlocksSameBufferMutationGuardAndRecoversAfterRelease()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, GlobalDataVault.MinimumQualityArenaLimitBytes))
            {
                VaultGenerationHandle<int> handle = vault.EnsureGenerationHandle<int>(
                    BufferID.ShinobuCrashAtomicState,
                    4,
                    Owner,
                    NativeArrayOptions.ClearMemory);
                ulong guardMask = MutationGuardBit(BufferID.ShinobuCrashAtomicState);

                Assert.IsTrue(vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<int> writer));
                Assert.IsTrue(writer.IsCreated);
                Assert.AreNotEqual(0u, vault.ActiveBurstLockMask);
                writer[0] = 44;

                try
                {
                    long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    bool guardAcquired = vault.TryAcquireMutationGuard(guardMask);
                    long afterBytes = GC.GetAllocatedBytesForCurrentThread();

                    Assert.IsFalse(guardAcquired);
                    Assert.AreEqual(0UL, vault.ActiveMutationGuardMask);
                    Assert.AreNotEqual(0u, vault.ActiveBurstLockMask);
                    Assert.AreEqual(0L, afterBytes - beforeBytes);
                }
                finally
                {
                    Assert.IsTrue(vault.ReleaseWriteLock(in handle, Owner));
                }
                Assert.AreEqual(0u, vault.ActiveBurstLockMask);

                Assert.IsTrue(vault.TryAcquireMutationGuard(guardMask));
                try
                {
                    Assert.AreEqual(guardMask, vault.ActiveMutationGuardMask);
                }
                finally
                {
                    vault.ReleaseMutationGuard(guardMask);
                }
                Assert.AreEqual(0UL, vault.ActiveMutationGuardMask);

                Assert.IsTrue(vault.TryReadOnlyHandle(in handle, out NativeArray<int>.ReadOnly read));
                Assert.AreEqual(44, read[0]);
            }
        }

        [Test]
        public static void HazardRuntimeBuffers_UseDistinctActiveWriteLockBits()
        {
            AssertDistinctActiveWriteLockBits(
                BufferID.HazardZoneVolumes,
                BufferID.HazardZoneVolumeIds,
                BufferID.HazardZoneSpatialHandles,
                BufferID.HazardZoneCurveLutSamples,
                BufferID.HazardZoneJobVolumes,
                BufferID.HazardZoneCandidateVolumeFlags,
                BufferID.HazardZoneSpatialQueryHandles,
                BufferID.HazardZoneTelemetryRing,
                BufferID.HazardZoneTelemetryCursor,
                BufferID.HazardExposureJobResult);
        }

        [Test]
        public static void HazardRuntimeBuffers_UseDistinctMutationGuardBits()
        {
            AssertDistinctMutationGuardBits(
                BufferID.HazardZoneVolumes,
                BufferID.HazardZoneVolumeIds,
                BufferID.HazardZoneSpatialHandles,
                BufferID.HazardZoneCurveLutSamples,
                BufferID.HazardZoneJobVolumes,
                BufferID.HazardZoneCandidateVolumeFlags,
                BufferID.HazardZoneSpatialQueryHandles,
                BufferID.HazardZoneTelemetryRing,
                BufferID.HazardZoneTelemetryCursor,
                BufferID.HazardExposureJobResult);
        }

        private static void AssertDistinctActiveWriteLockBits(params BufferID[] bufferIds)
        {
            uint seen = 0u;
            for (int i = 0; i < bufferIds.Length; i++)
            {
                int bit = unchecked((int)bufferIds[i]) & 31;
                uint mask = 1u << bit;
                Assert.AreEqual(0u, seen & mask, $"{bufferIds[i]} aliases active write-lock bit {bit}");
                seen |= mask;
            }
        }

        private static void AssertDistinctMutationGuardBits(params BufferID[] bufferIds)
        {
            ulong seen = 0UL;
            for (int i = 0; i < bufferIds.Length; i++)
            {
                int bit = unchecked((int)bufferIds[i]) & 63;
                ulong mask = 1UL << bit;
                Assert.AreEqual(0UL, seen & mask, $"{bufferIds[i]} aliases mutation guard bit {bit}");
                seen |= mask;
            }
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)bufferId) & 63);
        }
    }
}
#endif
