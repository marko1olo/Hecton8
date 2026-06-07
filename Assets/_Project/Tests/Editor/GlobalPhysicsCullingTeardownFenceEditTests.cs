using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GlobalPhysicsCullingTeardownFenceEditTests
    {
        [Test]
        public void DiscardBarrierForceCompletesInsidePostFixedSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/GlobalPhysicsStateManager.cs");
            string barrier = ExtractMethodBlock(source, "private void CompletePhysicsCullingJobForStateMutationBarrier(bool discardResults)");
            string helper = ExtractMethodBlock(source, "private static bool TryCompletePhysicsCullingJobForStateMutationBarrier(ref JobHandle handle)");

            Assert.That(barrier, Does.Contain("? TryCompletePhysicsCullingJobForStateMutationBarrier(ref _physicsCullingJobHandle)"));
            Assert.That(barrier, Does.Contain(": DispatcherJobSwap.TryFinalizeCompleted(ref _physicsCullingJobHandle)"));
            Assert.That(barrier, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _physicsCullingJobHandle, forceComplete: true)"));

            const string beginWindow = "DispatcherJobSwap.BeginPostFixedSwapWindow();";
            const string completeCall = "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)";
            const string endWindow = "DispatcherJobSwap.EndPostFixedSwapWindow();";

            Assert.That(helper, Does.Contain(beginWindow));
            Assert.That(helper, Does.Contain(completeCall));
            Assert.That(helper, Does.Contain(endWindow));
            Assert.That(helper, Does.Contain("return completed;"));

            Assert.Less(helper.IndexOf(beginWindow, StringComparison.Ordinal), helper.IndexOf(completeCall, StringComparison.Ordinal));
            Assert.Less(helper.IndexOf(completeCall, StringComparison.Ordinal), helper.IndexOf(endWindow, StringComparison.Ordinal));
            Assert.Less(helper.IndexOf(endWindow, StringComparison.Ordinal), helper.IndexOf("return completed;", StringComparison.Ordinal));
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
