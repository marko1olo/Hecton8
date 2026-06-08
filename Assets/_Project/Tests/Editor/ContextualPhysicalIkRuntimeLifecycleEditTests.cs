using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ContextualPhysicalIkRuntimeLifecycleEditTests
    {
        [Test]
        public void GroundResponseBarriersForceCompleteInsidePostSimulationSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs");
            string helper = ExtractMethodBlock(source, "private void ForceCompletePendingGroundResponseInPostSimulationWindow()");
            string originShift = ExtractMethodBlock(source, "private void CompletePendingGroundResponseForOriginShift()");
            string structural = ExtractMethodBlock(source, "private bool CompletePendingGroundResponseForStructuralMutation()");
            string runtimeDisable = ExtractMethodBlock(source, "private void CompletePendingGroundResponseForRuntimeDisable()");
            string lateFrame = ExtractMethodBlock(source, "public void LateFrameTick()");

            AssertCompleteInsidePostSimulationWindow(
                helper,
                "DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: true);");
            AssertBarrierUsesHelperBeforeSwapAndReset(originShift);
            AssertBarrierUsesHelperBeforeSwapAndReset(structural);
            AssertBarrierUsesHelperBeforeSwapAndReset(runtimeDisable);
            Assert.That(lateFrame, Does.Contain("DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: false)"));
            Assert.That(lateFrame, Does.Not.Contain("BeginPostSimulationSwapWindow"));
            Assert.That(lateFrame, Does.Not.Contain("ForceCompletePendingGroundResponseInPostSimulationWindow"));
        }

        [Test]
        public void DisposeBuffersForceCompletesNativeDisposeHandleInsidePostSimulationWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs");
            string disposeBuffers = ExtractMethodBlock(source, "private void DisposeBuffers(JobHandle dependency)");
            string helper = ExtractMethodBlock(source, "private static void ForceCompleteDisposeHandleInPostSimulationWindow(");
            string bufferSetDispose = ExtractMethodBlock(source, "public void Dispose()");

            Assert.That(disposeBuffers, Does.Contain("ForceCompleteDisposeHandleInPostSimulationWindow(ref _disposeHandle);"));
            Assert.That(disposeBuffers, Does.Not.Contain("DispatcherJobFence.TryComplete(ref _disposeHandle, forceComplete: true)"));
            AssertOrdered(disposeBuffers, "_nativeBuffers.Dispose(dependency, ref _disposeHandle);", "ForceCompleteDisposeHandleInPostSimulationWindow(ref _disposeHandle);");
            AssertOrdered(disposeBuffers, "ForceCompleteDisposeHandleInPostSimulationWindow(ref _disposeHandle);", "_groundResponseScheduled = false;");

            AssertCompleteInsideFencePostSimulationWindow(
                helper,
                "DispatcherJobFence.TryComplete(ref handle, forceComplete: true);");
            Assert.That(bufferSetDispose, Does.Contain("ForceCompleteDisposeHandleInPostSimulationWindow(ref disposeHandle);"));
            Assert.That(bufferSetDispose, Does.Not.Contain("DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true)"));
        }

        private static void AssertBarrierUsesHelperBeforeSwapAndReset(string method)
        {
            const string helperCall = "ForceCompletePendingGroundResponseInPostSimulationWindow();";
            Assert.That(method, Does.Contain(helperCall));
            Assert.That(method, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: true)"));
            AssertOrdered(method, helperCall, "SwapTargetBuffers();");
            AssertOrdered(method, helperCall, "_pendingGroundResponseHandle = default;");
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

        private static void AssertCompleteInsideFencePostSimulationWindow(string method, string completeCall)
        {
            const string beginWindow = "DispatcherJobFence.BeginPostSimulationSwapWindow();";
            const string endWindow = "DispatcherJobFence.EndPostSimulationSwapWindow();";

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
