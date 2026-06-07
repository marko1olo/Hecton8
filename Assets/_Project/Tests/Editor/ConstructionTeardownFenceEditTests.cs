using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ConstructionTeardownFenceEditTests
    {
        [TestCase(
            "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs",
            "private void DrainScheduledJobsForTeardown()",
            "DispatcherJobFence.BeginPreSimulationSwapWindow();",
            "DispatcherJobFence.EndPreSimulationSwapWindow();",
            "DispatcherJobFence.TryComplete(ref _preSimulationHandle, forceComplete: true)",
            "_preSimulationScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs",
            "private void DrainScheduledJobsForTeardown()",
            "DispatcherJobFence.BeginPostSimulationSwapWindow();",
            "DispatcherJobFence.EndPostSimulationSwapWindow();",
            "DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true)",
            "_simulationScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Construction/HabitatGraphManager.cs",
            "private bool CompleteFloodPropagationJobForTeardown()",
            "DispatcherJobFence.BeginPostSimulationSwapWindow();",
            "DispatcherJobFence.EndPostSimulationSwapWindow();",
            "DispatcherJobFence.TryComplete(ref _floodPropagationHandle, forceComplete: true)",
            "return FinishFloodPropagationJob();")]
        [TestCase(
            "Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs",
            "private void CompleteScheduledSolverForTeardown()",
            "DispatcherJobFence.BeginPostSimulationSwapWindow();",
            "DispatcherJobFence.EndPostSimulationSwapWindow();",
            "DispatcherJobFence.TryComplete(ref _solverHandle, forceComplete: true)",
            "_solverScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs",
            "private bool ScheduleDrainageSolve(float deltaTime, float quality)",
            "DispatcherJobFence.BeginPostSimulationSwapWindow();",
            "DispatcherJobFence.EndPostSimulationSwapWindow();",
            "DispatcherJobFence.TryComplete(ref pendingJob, forceComplete: true)",
            "ReleaseDrainageSolverBufferPins();")]
        public void ConstructionTeardownForceCompletesInsideDispatcherSwapWindow(
            string relativePath,
            string signature,
            string beginCall,
            string endCall,
            string completeCall,
            string finishSignal)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodBlock(source, signature);

            Assert.That(method, Does.Contain(beginCall));
            Assert.That(method, Does.Contain(completeCall));
            Assert.That(method, Does.Contain(endCall));
            Assert.That(method, Does.Contain(finishSignal));

            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            int beginIndex = method.Substring(0, completeIndex).LastIndexOf(beginCall, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endCall, completeIndex, StringComparison.Ordinal);
            int finishIndex = method.IndexOf(finishSignal, completeIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0);
            Assert.Greater(endIndex, completeIndex);
            Assert.Greater(finishIndex, endIndex);
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
