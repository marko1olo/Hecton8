using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class NutrientDriftLifecycleEditTests
    {
        [Test]
        public void ReleaseHandlesUnlocksJobGuardsBeforeHandleReleaseOrReset()
        {
            string nutrient = ReadProjectFile("Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs");
            string carrion = ReadProjectFile("Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs");

            string releaseHandles = ExtractMethodBlock(nutrient, "private void ReleaseVaultHandles(IDataVault vault)");
            string carrionReleaseHandles = ExtractMethodBlock(carrion, "private void ReleaseCarrionVaultHandles(IDataVault vault)");

            Assert.That(releaseHandles, Does.Contain("UnlockJobBuffers();"));
            Assert.That(releaseHandles, Does.Contain("ReleaseCarrionVaultHandles(vault);"));
            Assert.Less(
                releaseHandles.IndexOf("UnlockJobBuffers();", StringComparison.Ordinal),
                releaseHandles.IndexOf("ReleaseCarrionVaultHandles(vault);", StringComparison.Ordinal));

            Assert.That(carrionReleaseHandles, Does.Contain("UnlockCarrionJobBuffers();"));
            Assert.Less(
                carrionReleaseHandles.IndexOf("UnlockCarrionJobBuffers();", StringComparison.Ordinal),
                carrionReleaseHandles.IndexOf("if (vault == null)", StringComparison.Ordinal));
            Assert.Less(
                carrionReleaseHandles.IndexOf("UnlockCarrionJobBuffers();", StringComparison.Ordinal),
                carrionReleaseHandles.IndexOf("ReleaseVaultHandle(vault, ref _carrionStateHandle", StringComparison.Ordinal));
        }

        [Test]
        public void AbyssalFlowPayloadRejectsStaleHighWaterlineBeforeNutrientJobCopy()
        {
            string nutrient = ReadProjectFile("Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs");
            string method = ExtractMethodBlock(nutrient, "private bool TryReadAbyssalFlowPayload(");

            Assert.That(method, Does.Contain("math.isfinite(waterLevel)"));
            Assert.That(method, Does.Contain("math.abs(waterLevel) <= 1000f"));
            Assert.That(method, Does.Contain("waterLevel = 0f;"));
        }

        [Test]
        public void DroneFleetRejectsStaleHighAbyssalFlowWaterlineBeforeCognitionJob()
        {
            string droneFleet = ReadProjectFile("Assets/_Project/Scripts/Construction/DroneFleetManager.cs");
            string method = ExtractMethodBlock(droneFleet, "private static bool TryResolveAbyssalFlowVolumePayload(");

            Assert.That(method, Does.Contain("math.isfinite(surfaceY)"));
            Assert.That(method, Does.Contain("math.abs(surfaceY) <= 1000f"));
            Assert.That(method, Does.Contain("surfaceY = 0f;"));
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
        [Test]
        public void DataVaultRebind_CompletesScheduledJobsBeforeReleasingVaultHandles()
        {
            string nutrient = ReadProjectFile("Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs");
            string rebind = ExtractMethodBlock(nutrient, "private void RebindDataVaultForLifecycle");

            int barrierIndex = rebind.IndexOf("CompleteScheduledJobForVaultSwapBarrier();", System.StringComparison.Ordinal);
            int releaseIndex = rebind.IndexOf("ReleaseVaultHandles(_vault ?? releaseVaultOverride);", System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(barrierIndex, 0, "DataVault rebind must complete its scheduled job fence before releasing native vault ownership.");
            Assert.GreaterOrEqual(releaseIndex, 0, "DataVault rebind must release the old vault handles.");
            Assert.Less(barrierIndex, releaseIndex, "Native jobs must be completed before their DataVault-backed buffers are released.");
        }
    }
}
