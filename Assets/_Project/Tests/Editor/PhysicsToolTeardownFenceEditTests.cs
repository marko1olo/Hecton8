using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class PhysicsToolTeardownFenceEditTests
    {
        [TestCase(
            "Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs",
            "private void CompletePendingFrameForTeardown()",
            "DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true)",
            "FinishPendingFrameCompletion();")]
        [TestCase(
            "Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs",
            "private bool CompletePendingSolverForTeardown()",
            "DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true)",
            "return FinishPendingSolverCompletion();")]
        [TestCase(
            "Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs",
            "private bool CompletePendingJobForRebind()",
            "DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true)",
            "_jobScheduled = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs",
            "public static bool CompleteScheduledForTeardown()",
            "DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true)",
            "return FinishScheduledCompletion();")]
        [TestCase(
            "Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs",
            "private void CompleteIntegratorForLifecycle()",
            "DispatcherJobFence.TryComplete(ref _integratorHandle, forceComplete: true)",
            "_integratorPending = false;")]
        [TestCase(
            "Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs",
            "private static bool CompleteJobHandleForTeardown(ref JobHandle handle)",
            "DispatcherJobFence.TryComplete(ref handle, forceComplete: true)",
            "return completed;")]
        public void PhysicsToolTeardownForceCompletesInsidePostFixedSwapWindow(
            string relativePath,
            string signature,
            string completeCall,
            string finishSignal)
        {
            string source = ReadProjectFile(relativePath);
            string method = ExtractMethodBlock(source, signature);

            Assert.That(method, Does.Contain("DispatcherJobFence.BeginPostFixedSwapWindow();"));
            Assert.That(method, Does.Contain(completeCall));
            Assert.That(method, Does.Contain("DispatcherJobFence.EndPostFixedSwapWindow();"));
            Assert.That(method, Does.Contain(finishSignal));

            Assert.Less(
                method.IndexOf("DispatcherJobFence.BeginPostFixedSwapWindow();", StringComparison.Ordinal),
                method.IndexOf(completeCall, StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf(completeCall, StringComparison.Ordinal),
                method.IndexOf("DispatcherJobFence.EndPostFixedSwapWindow();", StringComparison.Ordinal));
            Assert.Less(
                method.IndexOf("DispatcherJobFence.EndPostFixedSwapWindow();", StringComparison.Ordinal),
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
