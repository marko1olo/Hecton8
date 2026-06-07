using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EncounterDirectorTeardownFenceEditTests
    {
        [Test]
        public void ForcedOutputAndTeardownCompleteEncounterJobInsidePostSimulationSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/EncounterDirector.cs");
            string output = ExtractMethodBlock(source, "internal void CompleteReadyOutput(");
            string teardown = ExtractMethodBlock(source, "internal void ForceCompleteActiveJobForTeardown()");
            string helper = ExtractMethodBlock(source, "private static bool TryCompleteEncounterJobInPostSimulationWindow(");

            Assert.That(output, Does.Contain("TryCompleteEncounterJobInPostSimulationWindow(ref _activeJobHandle, forceComplete)"));
            Assert.That(output, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _activeJobHandle, forceComplete)"));
            Assert.That(teardown, Does.Contain("TryCompleteEncounterJobInPostSimulationWindow(ref _activeJobHandle, forceComplete: true)"));
            Assert.That(teardown, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _activeJobHandle, true)"));
            Assert.That(helper, Does.Contain("if (!forceComplete)"));
            Assert.That(helper, Does.Contain("DispatcherJobSwap.TryComplete(ref handle, forceComplete: false)"));

            AssertForceCompleteInsidePostSimulationWindow(
                helper,
                "DispatcherJobSwap.BeginPostSimulationSwapWindow();",
                "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)",
                "DispatcherJobSwap.EndPostSimulationSwapWindow();");
        }

        [Test]
        public void DisposeCompletesNativeDependencyInsidePostSimulationWindowBeforePredatorAupRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/EncounterDirector.cs");
            string dispose = ExtractMethodBlock(source, "public void Dispose()");
            string helper = ExtractMethodBlock(source, "private static void CompleteEncounterDisposeDependencyInPostSimulationWindow(");

            Assert.That(dispose, Does.Contain("CompleteEncounterDisposeDependencyInPostSimulationWindow(ref disposeHandle);"));
            Assert.That(dispose, Does.Not.Contain("DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true)"));
            Assert.Less(
                dispose.IndexOf("CompleteEncounterDisposeDependencyInPostSimulationWindow(ref disposeHandle);", StringComparison.Ordinal),
                dispose.IndexOf("ReleasePredatorAupBuffer();", StringComparison.Ordinal));

            AssertForceCompleteInsidePostSimulationWindow(
                helper,
                "DispatcherJobFence.BeginPostSimulationSwapWindow();",
                "DispatcherJobFence.TryComplete(ref handle, forceComplete: true)",
                "DispatcherJobFence.EndPostSimulationSwapWindow();");
        }

        private static void AssertForceCompleteInsidePostSimulationWindow(
            string method,
            string beginWindow,
            string completeCall,
            string endWindow)
        {
            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + completeCall);

            int beginIndex = method.LastIndexOf(beginWindow, completeIndex, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, completeIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, beginWindow);
            Assert.GreaterOrEqual(endIndex, 0, endWindow);
            Assert.Less(beginIndex, completeIndex, completeCall);
            Assert.Less(completeIndex, endIndex, completeCall);
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
