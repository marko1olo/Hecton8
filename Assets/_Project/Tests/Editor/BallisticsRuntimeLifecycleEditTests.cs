using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BallisticsRuntimeLifecycleEditTests
    {
        [Test]
        public void VaultLaneReleaseAndResetReleaseActiveJobMutationGuardBeforeStateLoss()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs");
            string releaseVaultLanes = ExtractMethodBlock(source, "private static void ReleaseVaultLanes(IDataVault vault)");
            string resetTransient = ExtractMethodBlock(source, "private static void ResetTransientState()");

            Assert.That(releaseVaultLanes, Does.Contain("ReleaseActiveJobMutationGuard();"));
            Assert.That(releaseVaultLanes, Does.Contain("if (vault == null)"));
            Assert.That(releaseVaultLanes, Does.Contain("ReleaseVaultLane(vault, ref _trajectoryAHandle);"));
            Assert.Less(
                releaseVaultLanes.IndexOf("ReleaseActiveJobMutationGuard();", StringComparison.Ordinal),
                releaseVaultLanes.IndexOf("if (vault == null)", StringComparison.Ordinal));
            Assert.Less(
                releaseVaultLanes.IndexOf("ReleaseActiveJobMutationGuard();", StringComparison.Ordinal),
                releaseVaultLanes.IndexOf("ReleaseVaultLane(vault, ref _trajectoryAHandle);", StringComparison.Ordinal));

            Assert.That(resetTransient, Does.Contain("ReleaseActiveJobMutationGuard();"));
            Assert.That(resetTransient, Does.Contain("_activeJobMutationGuardVault = null;"));
            Assert.Less(
                resetTransient.IndexOf("ReleaseActiveJobMutationGuard();", StringComparison.Ordinal),
                resetTransient.IndexOf("_activeJobMutationGuardVault = null;", StringComparison.Ordinal));
        }

        [Test]
        public void TeardownForceCompletesActiveJobInsidePostSimulationSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs");
            string teardown = ExtractMethodBlock(source, "private static void CompleteScheduledForTeardown()");
            string helper = ExtractMethodBlock(source, "private static bool ForceCompleteActiveJobInPostSimulationWindow()");
            string noWait = ExtractMethodBlock(source, "private static void TryFinalizeScheduledNoWait()");

            Assert.That(teardown, Does.Contain("ForceCompleteActiveJobInPostSimulationWindow()"));
            Assert.That(teardown, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: true)"));
            Assert.That(helper, Does.Contain("DispatcherJobSwap.BeginPostSimulationSwapWindow();"));
            Assert.That(helper, Does.Contain("DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: true);"));
            Assert.That(helper, Does.Contain("DispatcherJobSwap.EndPostSimulationSwapWindow();"));
            Assert.Less(
                helper.IndexOf("DispatcherJobSwap.BeginPostSimulationSwapWindow();", StringComparison.Ordinal),
                helper.IndexOf("DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: true);", StringComparison.Ordinal));
            Assert.Less(
                helper.IndexOf("DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: true);", StringComparison.Ordinal),
                helper.IndexOf("DispatcherJobSwap.EndPostSimulationSwapWindow();", StringComparison.Ordinal));
            Assert.That(noWait, Does.Contain("DispatcherJobSwap.TryFinalizeCompleted(ref _activeHandle)"));
            Assert.That(noWait, Does.Not.Contain("BeginPostSimulationSwapWindow"));
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
