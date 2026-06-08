using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EcosystemDirectorTeardownFenceEditTests
    {
        [Test]
        public void ForcedScheduledJobCompletionUsesPostSimulationSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/EcosystemDirector.cs");
            string helper = ExtractMethodBlock(source, "private static bool TryCompleteScheduledJobInPostSimulationWindow(");

            Assert.That(helper, Does.Contain("if (!forceComplete)"));
            Assert.That(helper, Does.Contain("return DispatcherJobSwap.TryComplete(ref handle, forceComplete: false);"));
            AssertCompleteInsidePostSimulationWindow(
                helper,
                "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");
        }

        [Test]
        public void CompletionMethodsUseBracketingHelperForAllScheduledEcosystemJobs()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/EcosystemDirector.cs");

            Assert.That(
                ExtractMethodBlock(source, "private void CompleteScheduledSolve(bool forceComplete)"),
                Does.Contain("TryCompleteScheduledJobInPostSimulationWindow(ref _scheduledSolveHandle, forceComplete)"));
            Assert.That(
                ExtractMethodBlock(source, "private void CompleteScheduledGenomeMutation(bool forceComplete)"),
                Does.Contain("TryCompleteScheduledJobInPostSimulationWindow(ref _scheduledGenomeMutationHandle, forceComplete)"));
            Assert.That(
                ExtractMethodBlock(source, "private void CompleteScheduledMacroSwarmTravel(bool forceComplete)"),
                Does.Contain("TryCompleteScheduledJobInPostSimulationWindow(ref _macroSwarmTravelHandle, forceComplete)"));
            Assert.That(
                ExtractMethodBlock(source, "private void CompleteScheduledApexTerritoryOverlap(bool forceComplete)"),
                Does.Contain("TryCompleteScheduledJobInPostSimulationWindow(ref _scheduledApexTerritoryOverlapHandle, forceComplete)"));
        }

        [Test]
        public void DisposeCompletesScheduledJobsBeforeRuntimeStateRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/EcosystemDirector.cs");
            string method = ExtractMethodBlock(source, "private void DisposeRuntimeState()");

            Assert.That(method, Does.Not.Contain("disposeDependency"));
            AssertOrdered(method, "CompleteScheduledSimulation(forceComplete: true);", "ReleaseBuffer(ref _floraPredatorAupBufferA);");
            AssertOrdered(method, "CompleteScheduledMacroSwarmTravel(forceComplete: true);", "UnlockMacroSwarmTravelJobBuffers();");
            AssertOrdered(method, "CompleteScheduledApexTerritoryOverlap(forceComplete: true);", "UnlockApexTerritoryOverlapJobBuffers();");
        }

        [Test]
        public void MacroSwarmRuntimeClearForceCompletesInsidePostSimulationWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/EcosystemDirector.cs");
            string method = ExtractMethodBlock(source, "private void ClearMacroSwarmRuntimeState()");

            AssertOrdered(
                method,
                "TryCompleteScheduledJobInPostSimulationWindow(ref _macroSwarmTravelHandle, forceComplete: true);",
                "_macroSwarmTravelScheduled = false;");
        }

        private static void AssertCompleteInsidePostSimulationWindow(string method, string completeCall)
        {
            const string beginWindow = "DispatcherJobSwap.BeginPostSimulationSwapWindow();";
            const string endWindow = "DispatcherJobSwap.EndPostSimulationSwapWindow();";

            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + completeCall);

            int beginIndex = method.LastIndexOf(beginWindow, completeIndex, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, completeIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, "Missing post-simulation begin for: " + completeCall);
            Assert.GreaterOrEqual(endIndex, 0, "Missing post-simulation end for: " + completeCall);
            Assert.Less(beginIndex, completeIndex, completeCall);
            Assert.Less(completeIndex, endIndex, completeCall);
        }

        private static void AssertOrdered(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, first);
            Assert.GreaterOrEqual(secondIndex, 0, second);
            Assert.Less(firstIndex, secondIndex, first + " before " + second);
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
