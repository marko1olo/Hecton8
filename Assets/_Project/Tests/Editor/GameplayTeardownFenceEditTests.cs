using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GameplayTeardownFenceEditTests
    {
        [Test]
        public void RadiationTeardownForceCompletesInsidePostSimulationSwapWindows()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs");

            AssertForceCompleteInsideWindow(
                ExtractMethodBlock(source, "private void CompleteDiffusionJobForTeardownRelease()"),
                "DispatcherJobFence.BeginPostSimulationSwapWindow();",
                "DispatcherJobFence.TryComplete(ref _diffusionJobHandle, forceComplete: true)",
                "DispatcherJobFence.EndPostSimulationSwapWindow();",
                "_diffusionJobActive = false;");

            AssertForceCompleteInsideWindow(
                ExtractMethodBlock(source, "private void CompleteRadiationJobsForTeardownRelease()"),
                "DispatcherJobFence.BeginPostSimulationSwapWindow();",
                "DispatcherJobFence.TryComplete(ref _radiationSimulationJobHandle, forceComplete: true)",
                "DispatcherJobFence.EndPostSimulationSwapWindow();",
                "_radiationSimulationJobActive = false;");
        }

        [Test]
        public void ScannerDestroyForceCompletesInsideLateFrameSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs");
            string method = ExtractMethodBlock(source, "private void OnDestroy()");

            AssertForceCompleteInsideWindow(
                method,
                "DispatcherJobFence.BeginLateFrameSwapWindow();",
                "DispatcherJobFence.TryComplete(ref _queryHandle, forceComplete: true)",
                "DispatcherJobFence.EndLateFrameSwapWindow();",
                "_queryScheduled = false;");
        }

        private static void AssertForceCompleteInsideWindow(
            string method,
            string beginWindow,
            string completeCall,
            string endWindow,
            string finishSignal)
        {
            int beginIndex = method.IndexOf(beginWindow, StringComparison.Ordinal);
            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, StringComparison.Ordinal);
            int finishIndex = method.IndexOf(finishSignal, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, "Missing begin window: " + beginWindow);
            Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + completeCall);
            Assert.GreaterOrEqual(endIndex, 0, "Missing end window: " + endWindow);
            Assert.GreaterOrEqual(finishIndex, 0, "Missing finish signal: " + finishSignal);
            Assert.Less(beginIndex, completeIndex, completeCall);
            Assert.Less(completeIndex, endIndex, completeCall);
            Assert.Less(endIndex, finishIndex, completeCall);
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
