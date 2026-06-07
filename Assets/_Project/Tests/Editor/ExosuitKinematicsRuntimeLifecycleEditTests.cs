using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ExosuitKinematicsRuntimeLifecycleEditTests
    {
        [Test]
        public void DataVaultHotSwapCompletesJobBeforeReleasingVaultBuffers()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("CompletePendingJobForRebind()"));
            Assert.That(listener, Does.Contain("ReleaseVaultBuffers();"));
            Assert.That(listener, Does.Contain("_dataVault = currentVault;"));
            Assert.That(listener, Does.Contain("EnsureBuffers(true)"));
            Assert.That(listener, Does.Contain("WarmCoreBlackboxRoute();"));

            Assert.Less(
                listener.IndexOf("CompletePendingJobForRebind()", StringComparison.Ordinal),
                listener.IndexOf("ReleaseVaultBuffers();", StringComparison.Ordinal));
            Assert.Less(
                listener.IndexOf("ReleaseVaultBuffers();", StringComparison.Ordinal),
                listener.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                listener.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal),
                listener.IndexOf("EnsureBuffers(true)", StringComparison.Ordinal));
        }

        [Test]
        public void ForcedRebindCompletionUsesSharedCompletionFinishBeforeRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs");
            string forced = ExtractMethodBlock(source, "private bool CompletePendingJobForRebind()");

            Assert.That(forced, Does.Contain("DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true)"));
            Assert.That(forced, Does.Contain("_jobScheduled = false;"));
            Assert.That(forced, Does.Contain("FinishCompletedJob();"));
            Assert.Less(
                forced.IndexOf("DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true)", StringComparison.Ordinal),
                forced.IndexOf("FinishCompletedJob();", StringComparison.Ordinal));
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
