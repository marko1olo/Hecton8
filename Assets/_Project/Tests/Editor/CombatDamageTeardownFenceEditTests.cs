using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class CombatDamageTeardownFenceEditTests
    {
        [Test]
        public void ForcedCombatJobCompletionUsesPostSimulationSwapWindow()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string helper = ExtractMethodBlock(source, "private static bool ForceCompleteCombatJobInPostSimulationWindow(");

            AssertCompleteInsidePostSimulationWindow(
                helper,
                "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");
        }

        [Test]
        public void ShutdownForceCompletesDamageAndStatusBeforeStateRelease()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string shutdown = ExtractMethodBlock(source, "public static void Shutdown()");

            AssertOrdered(shutdown, "ForceCompleteCombatJobInPostSimulationWindow(ref _damageJobHandle);", "_damageJobScheduled = false;");
            AssertOrdered(shutdown, "ForceCompleteCombatJobInPostSimulationWindow(ref _damageJobHandle);", "FinishArmorPenetrationScheduledCompletion();");
            AssertOrdered(shutdown, "ForceCompleteCombatJobInPostSimulationWindow(ref _statusJobHandle);", "_statusJobScheduled = false;");
            AssertOrdered(shutdown, "ForceCompleteCombatJobInPostSimulationWindow(ref _statusJobHandle);", "CompleteStatusEffectFrame();");
            AssertOrdered(shutdown, "ForceCompleteCombatJobInPostSimulationWindow(ref _statusJobHandle);", "DisposeArmorPenetrationNativeState();");
            Assert.That(shutdown, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: true)"));
            Assert.That(shutdown, Does.Not.Contain("DispatcherJobSwap.TryComplete(ref _statusJobHandle, forceComplete: true)"));
        }

        [Test]
        public void LateFrameTickKeepsNonBlockingCombatJobPolling()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs");
            string lateFrame = ExtractMethodBlock(source, "public static void LateFrameTick()");

            Assert.That(lateFrame, Does.Contain("DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: false)"));
            Assert.That(lateFrame, Does.Contain("DispatcherJobSwap.TryComplete(ref _statusJobHandle, forceComplete: false)"));
            Assert.That(lateFrame, Does.Not.Contain("BeginPostSimulationSwapWindow"));
            Assert.That(lateFrame, Does.Not.Contain("ForceCompleteCombatJobInPostSimulationWindow"));
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
