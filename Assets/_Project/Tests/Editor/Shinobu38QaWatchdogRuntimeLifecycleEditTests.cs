using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class Shinobu38QaWatchdogRuntimeLifecycleEditTests
    {
        [Test]
        public void DataVaultHotSwapCompletesNavigationAndReleasesOldVaultBeforeRebuild()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string rebind = ExtractMethodBlock(source, "private void RebindDataVault(");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("RebindDataVault(currentService as IDataVault);"));
            Assert.Less(
                listener.IndexOf("GlobalRegistryServiceSlot.DataVault", StringComparison.Ordinal),
                listener.IndexOf("GlobalRegistryServiceSlot.Dispatcher", StringComparison.Ordinal));

            Assert.That(rebind, Does.Contain("CompletePendingNavigationForRebind()"));
            Assert.That(rebind, Does.Contain("StopFileWriter(flushPending: true);"));
            Assert.That(rebind, Does.Contain("UnlockRuntimeBuffers();"));
            Assert.That(rebind, Does.Contain("ReleaseWatchdogVaultHandles(_dataVault);"));
            Assert.That(rebind, Does.Contain("_dataVault = currentVault;"));
            Assert.That(rebind, Does.Contain("TryRebuildVaultStateAfterRebind()"));
            Assert.That(rebind, Does.Contain("RegisterRuntimeLanes();"));

            Assert.Less(
                rebind.IndexOf("CompletePendingNavigationForRebind()", StringComparison.Ordinal),
                rebind.IndexOf("StopFileWriter(flushPending: true);", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("StopFileWriter(flushPending: true);", StringComparison.Ordinal),
                rebind.IndexOf("UnlockRuntimeBuffers();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("UnlockRuntimeBuffers();", StringComparison.Ordinal),
                rebind.IndexOf("ReleaseWatchdogVaultHandles(_dataVault);", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("ReleaseWatchdogVaultHandles(_dataVault);", StringComparison.Ordinal),
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("_dataVault = currentVault;", StringComparison.Ordinal),
                rebind.IndexOf("TryRebuildVaultStateAfterRebind()", StringComparison.Ordinal));
        }

        [Test]
        public void RebuildSeedsWatchdogBuffersBeforeRuntimeLanesResume()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs");
            string complete = ExtractMethodBlock(source, "private bool CompletePendingNavigationForRebind()");
            string rebuild = ExtractMethodBlock(source, "private bool TryRebuildVaultStateAfterRebind()");

            Assert.That(complete, Does.Contain("DispatcherJobFence.TryComplete(ref _navigationHandle, forceComplete: true)"));
            Assert.That(complete, Does.Contain("_navigationHandle = default;"));
            Assert.That(complete, Does.Contain("_navigationPending = false;"));

            Assert.That(rebuild, Does.Contain("EnsureVaultHandles();"));
            Assert.That(rebuild, Does.Contain("LockRuntimeBuffers()"));
            Assert.That(rebuild, Does.Contain("DispatcherJobFence.TryComplete(ref clearHandle, forceComplete: true);"));
            Assert.That(rebuild, Does.Contain("tuningBuffer[0] = _pendingTuning;"));
            Assert.That(rebuild, Does.Contain("GenerateEmergencyMockRoute(stateBuffer, waypoints, mockVault);"));
            Assert.That(rebuild, Does.Contain("StartFileWriter();"));
            Assert.That(rebuild, Does.Contain("QueueCsvHeader(csvScratch);"));

            Assert.Less(
                rebuild.IndexOf("EnsureVaultHandles();", StringComparison.Ordinal),
                rebuild.IndexOf("LockRuntimeBuffers()", StringComparison.Ordinal));
            Assert.Less(
                rebuild.IndexOf("DispatcherJobFence.TryComplete(ref clearHandle, forceComplete: true);", StringComparison.Ordinal),
                rebuild.IndexOf("tuningBuffer[0] = _pendingTuning;", StringComparison.Ordinal));
            Assert.Less(
                rebuild.IndexOf("GenerateEmergencyMockRoute(stateBuffer, waypoints, mockVault);", StringComparison.Ordinal),
                rebuild.IndexOf("StartFileWriter();", StringComparison.Ordinal));
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
