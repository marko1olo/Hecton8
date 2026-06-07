using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class SubmarineStructuralGridLifecycleEditTests
    {
        [Test]
        public void DataVaultHotSwapReleasesOldNativeStateBeforeRebinding()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SubmarineStructuralGrid.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("DisposeNativeStateDeferred();"));
            Assert.That(listener, Does.Contain("_dataVault = currentVault;"));
            Assert.That(listener, Does.Contain("EnsureNativeState();"));
            Assert.That(listener, Does.Contain("SeedStructuralState();"));
            Assert.That(listener, Does.Contain("_breachGpuDirty = true;"));

            Assert.Less(
                listener.IndexOf("DisposeNativeStateDeferred();", StringComparison.Ordinal),
                listener.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                listener.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal),
                listener.IndexOf("EnsureNativeState();", StringComparison.Ordinal));
            Assert.Less(
                listener.IndexOf("EnsureNativeState();", StringComparison.Ordinal),
                listener.IndexOf("SeedStructuralState();", StringComparison.Ordinal));
        }

        [Test]
        public void TeardownForceCompletesStructuralJobsInsidePostFixedSwapWindowBeforeVaultRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SubmarineStructuralGrid.cs");
            string teardown = ExtractMethodBlock(source, "private void CompleteStructuralJobsForTeardown()");
            string dispose = ExtractMethodBlock(source, "private void DisposeNativeStateDeferred()");

            const string beginWindow = "DispatcherJobFence.BeginPostFixedSwapWindow();";
            const string endWindow = "DispatcherJobFence.EndPostFixedSwapWindow();";
            string[] completeCalls =
            {
                "DispatcherJobFence.TryComplete(ref _damageJobHandle, forceComplete: true)",
                "DispatcherJobFence.TryComplete(ref _mappingJobHandle, forceComplete: true)",
                "DispatcherJobFence.TryComplete(ref _fatigueJobHandle, forceComplete: true)",
                "DispatcherJobFence.TryComplete(ref _breachRepairJobHandle, forceComplete: true)"
            };

            Assert.That(teardown, Does.Contain(beginWindow));
            Assert.That(teardown, Does.Contain(endWindow));

            foreach (string completeCall in completeCalls)
            {
                Assert.That(teardown, Does.Contain(completeCall));
                Assert.Less(
                    teardown.IndexOf(beginWindow, StringComparison.Ordinal),
                    teardown.IndexOf(completeCall, StringComparison.Ordinal));
                Assert.Less(
                    teardown.IndexOf(completeCall, StringComparison.Ordinal),
                    teardown.IndexOf(endWindow, StringComparison.Ordinal));
            }

            Assert.Less(
                dispose.IndexOf("CompleteStructuralJobsForTeardown();", StringComparison.Ordinal),
                dispose.IndexOf("UnlockStructuralJobBuffers(_damageJobLockMask", StringComparison.Ordinal));
            Assert.Less(
                dispose.IndexOf("UnlockStructuralJobBuffers(_breachRepairJobLockMask", StringComparison.Ordinal),
                dispose.IndexOf("ReleaseVaultHandle(vault, ref _cellIntegrityFrontHandle);", StringComparison.Ordinal));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);

            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
