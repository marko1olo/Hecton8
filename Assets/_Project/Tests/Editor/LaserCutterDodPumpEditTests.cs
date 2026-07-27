using System;
using System.IO;
using Hecton8.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Proves the laser cutter DOD request ring is actually drained. Before the late-frame pump existed,
    /// LaserCutter staged rows through QueueLiveRequest and nothing in the project ever called
    /// TryScheduleSdfProbeBatch or TryCompleteScheduledSdfProbesAndEvaluate, so every cut result was
    /// computed by nobody and applied to nothing.
    /// </summary>
    public sealed class LaserCutterDodPumpEditTests
    {
        [Test]
        public void PumpIsIdleWhenNoVaultIsBound()
        {
            Assert.AreEqual(
                LaserCutterDodPumpAction.Idle,
                LaserCutterDodRuntime.ResolvePumpAction(false, false, false, true));
            Assert.AreEqual(
                LaserCutterDodPumpAction.Idle,
                LaserCutterDodRuntime.ResolvePumpAction(false, true, false, true));
            Assert.AreEqual(
                LaserCutterDodPumpAction.Idle,
                LaserCutterDodRuntime.ResolvePumpAction(false, false, true, true));
        }

        [Test]
        public void PumpIsIdleWhenBoundButNothingIsStaged()
        {
            Assert.AreEqual(
                LaserCutterDodPumpAction.Idle,
                LaserCutterDodRuntime.ResolvePumpAction(true, false, false, false));
        }

        [Test]
        public void PumpSchedulesProbeBatchWhenRequestsAreStagedAndJobsAreFree()
        {
            Assert.AreEqual(
                LaserCutterDodPumpAction.ScheduleProbeBatch,
                LaserCutterDodRuntime.ResolvePumpAction(true, false, false, true));
        }

        [Test]
        public void PumpAdvancesInFlightBatchBeforeStagingAnother()
        {
            Assert.AreEqual(
                LaserCutterDodPumpAction.AdvanceScheduledBatch,
                LaserCutterDodRuntime.ResolvePumpAction(true, true, false, false));
            Assert.AreEqual(
                LaserCutterDodPumpAction.AdvanceScheduledBatch,
                LaserCutterDodRuntime.ResolvePumpAction(true, false, true, false));
            Assert.AreEqual(
                LaserCutterDodPumpAction.AdvanceScheduledBatch,
                LaserCutterDodRuntime.ResolvePumpAction(true, true, true, true));
        }

        [Test]
        public void PumpNeverSchedulesWhileAJobIsInFlight()
        {
            for (int mask = 0; mask < 8; mask++)
            {
                bool probeActive = (mask & 1) != 0;
                bool evaluationActive = (mask & 2) != 0;
                bool hasQueued = (mask & 4) != 0;
                if (!probeActive && !evaluationActive)
                    continue;

                Assert.AreNotEqual(
                    LaserCutterDodPumpAction.ScheduleProbeBatch,
                    LaserCutterDodRuntime.ResolvePumpAction(true, probeActive, evaluationActive, hasQueued),
                    "Scheduling a second batch over an in-flight job would overwrite the request ring.");
            }
        }

        [Test]
        public void PumpDecisionIsConsumedByTheLateFrameDrain()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs");
            string pump = ExtractMethodBlock(source, "internal static void PumpScheduledCutBatch()");
            string ensure = ExtractMethodBlock(source, "public static bool EnsureInitialized(");
            string register = ExtractMethodBlock(source, "private static void TryRegisterLateFramePump()");
            string unregister = ExtractMethodBlock(source, "private static void TryUnregisterLateFramePump()");

            Assert.That(pump, Does.Contain("ResolvePumpAction("));
            Assert.That(pump, Does.Contain("TryCompleteScheduledSdfProbesAndEvaluate(_lastPublishedToolHeat01);"));
            Assert.That(pump, Does.Contain("TryScheduleSdfProbeBatch("));
            Assert.That(pump, Does.Contain("QueryTriggerInteraction.Ignore"));
            Assert.That(pump, Does.Contain("CutterProbeLayerMask"));

            Assert.That(ensure, Does.Contain("TryRegisterLateFramePump();"));
            Assert.That(ensure, Does.Contain("TryUnregisterLateFramePump();"));
            Assert.Less(
                ensure.IndexOf("TryUnregisterLateFramePump();", StringComparison.Ordinal),
                ensure.IndexOf("TryRegisterLateFramePump();", StringComparison.Ordinal),
                "The vault-null teardown branch must release the pump before the ready branch re-registers it.");

            Assert.That(register, Does.Contain("GlobalRegistry.TryRegisterLateFrameTickable(_lateFramePump, PriorityLayer.Player)"));
            Assert.That(unregister, Does.Contain("GlobalRegistry.UnregisterLateFrameTickable(_lateFramePump, PriorityLayer.Player);"));
            Assert.That(source, Does.Contain("LaserCutterDodRuntime.PumpScheduledCutBatch();"));
        }

        [Test]
        public void CutterProbeMaskExcludesTriggersAndTheWaterSurface()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs");

            Assert.That(
                source,
                Does.Contain("HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask"));
            Assert.That(
                ExtractMethodBlock(source, "internal static void PumpScheduledCutBatch()"),
                Does.Not.Contain("-1"),
                "COMMON_SENSE.md:17 - the probe mask must stay explicit, never the catch-all.");
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
