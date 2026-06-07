using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class SargassumMicroFaunaTeardownFenceEditTests
    {
        [Test]
        public void ForcedSargassumJobCompletionUsesPostSimulationSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs");
            string helper = ExtractMethodBlock(source, "private static bool TryCompleteSargassumJobInPostSimulationWindow(");
            string completeFoveated = ExtractMethodBlock(source, "private void CompletePendingFoveatedSimulationDecision(bool forceComplete)");
            string clearVaultHandles = ExtractMethodBlock(source, "private void ClearVaultHandles(JobHandle disposeDependency)");
            string disposeFoveated = ExtractMethodBlock(source, "private void DisposeFoveatedSimulationBuffers(JobHandle externalDependency)");

            Assert.That(helper, Does.Contain("return DispatcherJobSwap.TryComplete(ref handle, forceComplete: false);"));
            AssertCompleteInsidePostSimulationWindow(
                helper,
                "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");
            Assert.That(completeFoveated, Does.Contain("TryCompleteSargassumJobInPostSimulationWindow(ref _foveatedSimulationHandle, forceComplete)"));
            Assert.That(completeFoveated, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _foveatedSimulationHandle, forceComplete)"));
            AssertOrdered(
                clearVaultHandles,
                "TryCompleteSargassumJobInPostSimulationWindow(ref disposeDependency, forceComplete: true);",
                "_grazingAnchorsHandle = default;");
            AssertOrdered(
                disposeFoveated,
                "TryCompleteSargassumJobInPostSimulationWindow(ref disposeDependency, forceComplete: true);",
                "_foveatedSimulationInputHandle = default;");
            Assert.That(clearVaultHandles, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref disposeDependency, forceComplete: true)"));
            Assert.That(disposeFoveated, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref disposeDependency, forceComplete: true)"));
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
