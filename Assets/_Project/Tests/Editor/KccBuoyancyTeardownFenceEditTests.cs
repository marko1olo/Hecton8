using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class KccBuoyancyTeardownFenceEditTests
    {
        [Test]
        public void HydrodynamicKccAbortBatchForceCompletesInsidePostFixedSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs");
            string method = ExtractMethodBlock(source, "private void AbortScheduledBatchForTeardown()");

            string[] forceCompletes =
            {
                "DispatcherJobFence.TryComplete(ref _postSimulationHandle, true)",
                "DispatcherJobFence.TryComplete(ref _sdfCollisionHandle, true)",
                "DispatcherJobFence.TryComplete(ref _collisionHandle, true)",
                "DispatcherJobFence.TryComplete(ref _commandHandle, true)",
                "DispatcherJobFence.TryComplete(ref _integrationHandle, true)",
                "DispatcherJobFence.TryComplete(ref _environmentMockHandle, true)",
                "DispatcherJobFence.TryComplete(ref _inputHandle, true)",
                "DispatcherJobFence.TryComplete(ref _externalInputHandle, true)"
            };

            AssertForceCompletesInsidePostFixedWindow(method, forceCompletes, "ClearScheduledBatchState();");
        }

        [Test]
        public void BuoyancyPendingSolverForceCompletesInsidePostFixedSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs");
            string method = ExtractMethodBlock(source, "private bool CompletePendingSolverForTeardown()");

            AssertForceCompletesInsidePostFixedWindow(
                method,
                new[] { "DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true)" },
                "return FinishPendingSolverCompletion();");
        }

        private static void AssertForceCompletesInsidePostFixedWindow(string method, string[] forceCompletes, string finishSignal)
        {
            const string beginWindow = "DispatcherJobFence.BeginPostFixedSwapWindow();";
            const string endWindow = "DispatcherJobFence.EndPostFixedSwapWindow();";

            Assert.That(method, Does.Contain(beginWindow));
            Assert.That(method, Does.Contain(endWindow));
            Assert.That(method, Does.Contain(finishSignal));

            int beginIndex = method.IndexOf(beginWindow, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, StringComparison.Ordinal);
            int finishIndex = method.IndexOf(finishSignal, StringComparison.Ordinal);

            Assert.Less(beginIndex, endIndex);
            Assert.Less(endIndex, finishIndex);

            foreach (string forceComplete in forceCompletes)
            {
                int completeIndex = method.IndexOf(forceComplete, StringComparison.Ordinal);
                Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + forceComplete);
                Assert.Less(beginIndex, completeIndex, forceComplete);
                Assert.Less(completeIndex, endIndex, forceComplete);
            }
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
