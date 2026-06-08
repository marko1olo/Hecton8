using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ThermodynamicsTeardownFenceEditTests
    {
        [Test]
        public void AbyssalThermalLifecycleForceCompletesInsideLateFrameSwapWindows()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs");
            string method = ExtractMethodBlock(source, "private bool CompleteThermalJobsForLifecycle()");

            AssertForceCompleteInsideNearestLateFrameWindow(
                method,
                "DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true)",
                "_hasPendingJob = false;");
            AssertForceCompleteInsideNearestLateFrameWindow(
                method,
                "DispatcherJobFence.TryComplete(ref _sampleReadHandle, forceComplete: true)",
                "H8Memory.RegisterActiveJob(SystemID.Thermodynamics, default);");
        }

        [Test]
        public void HazardGridReleaseNativeStateForceCompletesInsideLateFrameSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs");
            string method = ExtractMethodBlock(source, "private void ReleaseNativeState()");

            AssertForceCompleteInsideNearestLateFrameWindow(
                method,
                "DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true)",
                "H8Memory.RegisterActiveJob(MemoryOwner, default);");
        }

        private static void AssertForceCompleteInsideNearestLateFrameWindow(string method, string completeCall, string finishSignal)
        {
            const string beginWindow = "DispatcherJobFence.BeginLateFrameSwapWindow();";
            const string endWindow = "DispatcherJobFence.EndLateFrameSwapWindow();";

            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(completeIndex, 0, "Missing force complete: " + completeCall);

            int beginIndex = method.LastIndexOf(beginWindow, completeIndex, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, completeIndex, StringComparison.Ordinal);
            int finishIndex = method.IndexOf(finishSignal, completeIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, "Missing late-frame begin for: " + completeCall);
            Assert.GreaterOrEqual(endIndex, 0, "Missing late-frame end for: " + completeCall);
            Assert.GreaterOrEqual(finishIndex, 0, "Missing finish marker after: " + completeCall);
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
