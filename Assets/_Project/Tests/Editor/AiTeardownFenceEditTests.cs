using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AiTeardownFenceEditTests
    {
        [TestCase(
            "Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs",
            "private void CompleteActiveJobForTeardown()",
            "DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true)",
            "_jobPending = false;")]
        [TestCase(
            "Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs",
            "private void CompleteFrameJobForTeardown()",
            "DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true)",
            "FinishFrameJobCompletion();")]
        [TestCase(
            "Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs",
            "private void CompleteFrameJobForTeardown()",
            "DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true)",
            "FinishFrameJobCompletion();")]
        public void AiTeardownForceCompletesInsidePostSimulationSwapWindow(
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

            Assert.Less(
                method.IndexOf("DispatcherJobFence.BeginPostSimulationSwapWindow();", StringComparison.Ordinal),
                method.IndexOf(completeCall, StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf(completeCall, StringComparison.Ordinal),
                method.IndexOf("DispatcherJobFence.EndPostSimulationSwapWindow();", StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf("DispatcherJobFence.EndPostSimulationSwapWindow();", StringComparison.Ordinal),
                method.IndexOf(finishSignal, StringComparison.Ordinal));
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
