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
                    vault.TryAcquireWriteLock(in handle, Owner, out NativeArray<int> warmup);

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
    }
}
#endif
