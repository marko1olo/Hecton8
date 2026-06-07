using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AtmosphereTeardownFenceEditTests
    {
        [TestCase(
            "Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs",
            "private void CompleteScheduledWorkForTeardown()",
            "DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true)",
            "FinishScheduledWork(completeStart);")]
        [TestCase(
            "Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs",
            "private void CompleteSimulationForLifecycle()",
            "DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true)",
            "_simulationScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs",
            "private void CompleteWaveParameterKernelForShutdown()",
            "DispatcherJobFence.TryComplete(ref _waveParameterJobHandle, forceComplete: true)",
            "_waveParameterJobScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs",
            "private void CompleteScheduledJobsForShutdown()",
            "DispatcherJobFence.TryComplete(ref _attenuationJobHandle, forceComplete: true)",
            "_attenuationScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs",
            "private void CompleteScheduledJobsForShutdown()",
            "DispatcherJobFence.TryComplete(ref _mockHurricaneJobHandle, forceComplete: true)",
            "_mockScheduled = false;")]
        public void AtmosphereTeardownForceCompletesInsidePostSimulationSwapWindow(
            string relativePath,
            string signature,
            string completeCall,
            string finishSignal)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodBlock(source, signature);

            Assert.That(method, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow();"));
            Assert.That(method, Does.Contain(completeCall));
            Assert.That(method, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow();"));
            Assert.That(method, Does.Contain(finishSignal));

            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            int beginIndex = method.Substring(0, completeIndex).LastIndexOf(
                "DispatcherJobFence.BeginPostSimulationSwapWindow();",
                StringComparison.Ordinal);
            int endIndex = method.IndexOf(
                "DispatcherJobFence.EndPostSimulationSwapWindow();",
                completeIndex,
                StringComparison.Ordinal);
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
