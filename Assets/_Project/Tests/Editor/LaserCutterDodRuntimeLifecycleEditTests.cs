using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class LaserCutterDodRuntimeLifecycleEditTests
    {
        [Test]
        public void LaserCutterDataVaultHotSwapRebindsDodRuntimes()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/LaserCutter.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string ensure = ExtractMethodBlock(source, "private static void EnsureDodRuntimesInitialized(");

            Assert.That(listener, Does.Contain("case GlobalRegistryServiceSlot.DataVault:"));
            Assert.That(listener, Does.Contain("EnsureDodRuntimesInitialized(currentService as IDataVault);"));
            Assert.That(ensure, Does.Contain("LaserCutterDodRuntime.EnsureInitialized(vault);"));
            Assert.That(ensure, Does.Contain("WfcLaserCutRuntime.EnsureInitialized(vault)"));
        }

        [Test]
        public void DodRuntimeCompletesScheduledJobsInsidePostSimulationSwapWindowBeforeReleasingVault()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs");
            string release = ExtractMethodBlock(source, "private static void ReleaseVaultHandles(");
            string complete = ExtractMethodBlock(source, "private static void CompleteScheduledJobsForVaultRebind()");

            Assert.That(release, Does.Contain("CompleteScheduledJobsForVaultRebind();"));
            Assert.That(release, Does.Contain("ReleaseVaultHandle(vault, LaserCutterDodConstants.RequestsBuffer, ref _requestsHandle);"));
            Assert.Less(
                release.IndexOf("CompleteScheduledJobsForVaultRebind();", StringComparison.Ordinal),
                release.IndexOf("ReleaseVaultHandle(vault, LaserCutterDodConstants.RequestsBuffer, ref _requestsHandle);", StringComparison.Ordinal));

            Assert.That(complete, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow();"));
            Assert.That(complete, Does.Contain("DispatcherJobFence.TryComplete(ref _scheduledSdfProbeHandle, forceComplete: true);"));
            Assert.That(complete, Does.Contain("DispatcherJobFence.TryComplete(ref _scheduledEvaluationHandle, forceComplete: true);"));
            Assert.That(complete, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow();"));
            Assert.Less(
                complete.IndexOf("DispatcherJobFence.BeginPostSimulationSwapWindow();", StringComparison.Ordinal),
                complete.IndexOf("DispatcherJobFence.TryComplete(ref _scheduledSdfProbeHandle, forceComplete: true);", StringComparison.Ordinal));
            Assert.Less(
                complete.IndexOf("DispatcherJobFence.TryComplete(ref _scheduledEvaluationHandle, forceComplete: true);", StringComparison.Ordinal),
                complete.IndexOf("DispatcherJobFence.EndPostSimulationSwapWindow();", StringComparison.Ordinal));
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
