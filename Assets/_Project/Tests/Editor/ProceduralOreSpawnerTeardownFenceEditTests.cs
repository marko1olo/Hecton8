using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ProceduralOreSpawnerTeardownFenceEditTests
    {
        [Test]
        public void SpawnJobTeardownForceCompleteUsesSinglePostSimulationWindowHelper()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs");
            string helper = ExtractMethodBlock(source, "private void ForceCompleteSpawnJobForTeardownInPostSimulationWindow()");

            AssertForceCompleteInsideNearestPostSimulationWindow(
                helper,
                "DispatcherJobFence.TryComplete(ref _spawnJob, forceComplete: true)",
                "_spawnJobScheduled = false;");
            Assert.That(helper.IndexOf("_spawnJobScheduled = false;", StringComparison.Ordinal),
                Is.LessThan(helper.IndexOf("UnlockVaultWriteBuffers();", StringComparison.Ordinal)));
            Assert.That(CountOccurrences(source, "DispatcherJobFence.TryComplete(ref _spawnJob, forceComplete: true)"), Is.EqualTo(1));
            Assert.That(CountOccurrences(source, "ForceCompleteSpawnJobForTeardownInPostSimulationWindow();"), Is.EqualTo(3));
        }

        [Test]
        public void PendingOreReadDependencyTeardownForceCompleteUsesPostSimulationWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs");
            string method = ExtractMethodBlock(source, "private void CompletePendingOreReadDependencyForTeardown()");

            AssertForceCompleteInsideNearestPostSimulationWindow(
                method,
                "DispatcherJobFence.TryComplete(ref _pendingOreReadDependency, forceComplete: true)",
                "_pendingOreReadDependency = default;");
        }

        private static void AssertForceCompleteInsideNearestPostSimulationWindow(
            string method,
            string completeCall,
            string finishSignal)
        {
            const string beginWindow = "DispatcherJobFence.BeginPostSimulationSwapWindow();";
            const string endWindow = "DispatcherJobFence.EndPostSimulationSwapWindow();";

            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + completeCall);

            int beginIndex = method.LastIndexOf(beginWindow, completeIndex, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, completeIndex, StringComparison.Ordinal);
            int finishIndex = method.IndexOf(finishSignal, completeIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, "Missing post-simulation begin for: " + completeCall);
            Assert.GreaterOrEqual(endIndex, 0, "Missing post-simulation end for: " + completeCall);
            Assert.GreaterOrEqual(finishIndex, 0, "Missing finish signal for: " + completeCall);
            Assert.Less(beginIndex, completeIndex, completeCall);
            Assert.Less(completeIndex, endIndex, completeCall);
            Assert.Less(endIndex, finishIndex, completeCall);
        }

        private static int CountOccurrences(string source, string needle)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
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
