using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldProceduralScatterTeardownFenceEditTests
    {
        [Test]
        public void ScatterSamplingTeardownForceCompletesInsidePostSimulationWindowBeforeFrameRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirector.cs");
            string teardown = ExtractMethodBlock(source, "private void CompleteSamplingJobForTeardown()");
            string helper = ExtractMethodBlock(source, "private static bool TryCompleteScatterSamplingJobForTeardown(ref JobHandle handle)");

            Assert.That(teardown, Does.Contain("TryCompleteScatterSamplingJobForTeardown(ref _samplingJobHandle);"));
            Assert.That(teardown, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _samplingJobHandle, forceComplete: true);"));
            AssertOrdered(teardown, "TryCompleteScatterSamplingJobForTeardown(ref _samplingJobHandle);", "fieldSampler.EndScatterSamplingFrame();");
            AssertPostSimulationWindow(helper, "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");
        }

        [Test]
        public void MigratorySargassumDisposeForceCompletesInsidePostSimulationWindowBeforeBufferRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs");
            string dispose = ExtractMethodBlock(source, "private void CompleteMigratorySargassumJobForDispose()");
            string helper = ExtractMethodBlock(source, "private static bool TryCompleteMigratorySargassumJobForDispose(ref JobHandle handle)");

            Assert.That(dispose, Does.Contain("TryCompleteMigratorySargassumJobForDispose(ref _migratorySargassumJobHandle);"));
            Assert.That(dispose, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _migratorySargassumJobHandle, forceComplete: true)"));
            AssertOrdered(dispose, "TryCompleteMigratorySargassumJobForDispose(ref _migratorySargassumJobHandle);", "ReleaseMigratorySargassumJobBufferLocks();");
            AssertPostSimulationWindow(helper, "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");
        }

        private static void AssertPostSimulationWindow(string method, string completeCall)
        {
            const string beginWindow = "DispatcherJobSwap.BeginPostSimulationSwapWindow();";
            const string endWindow = "DispatcherJobSwap.EndPostSimulationSwapWindow();";

            Assert.That(method, Does.Contain(beginWindow));
            Assert.That(method, Does.Contain(completeCall));
            Assert.That(method, Does.Contain(endWindow));
            Assert.That(method, Does.Contain("return completed;"));

            AssertOrdered(method, beginWindow, completeCall);
            AssertOrdered(method, completeCall, endWindow);
            AssertOrdered(method, endWindow, "return completed;");
        }

        private static void AssertOrdered(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), first);
            Assert.That(secondIndex, Is.GreaterThan(firstIndex), second);
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
