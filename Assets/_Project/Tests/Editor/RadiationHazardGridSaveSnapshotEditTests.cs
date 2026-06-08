using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class RadiationHazardGridSaveSnapshotEditTests
    {
        [Test]
        public void PopulateSaveDataCompletesRadiationSnapshotBeforeWritingDose()
        {
            string source = ReadRadiationGridSource();
            string populateBody = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string saveSnapshotBody = ExtractMethodBody(source, "private void CompleteRadiationJobsForSaveSnapshot()");
            string diffusionBody = ExtractMethodBody(source, "private void CompleteDiffusionJobForForcedSwapWindow()");
            string forcedRadiationBody = ExtractMethodBody(source, "private void CompleteRadiationSimulationJobForForcedSwapWindow()");
            string teardownBody = ExtractMethodBody(source, "private void CompleteRadiationJobsForTeardownRelease()");

            StringAssert.Contains("CompleteRadiationJobsForSaveSnapshot();", populateBody);
            StringAssert.DoesNotContain("CompleteDiffusionJobIfReady();", populateBody);
            AssertSourceOrder(populateBody, "CompleteRadiationJobsForSaveSnapshot();", "data.radiationDose =");
            AssertSourceOrder(populateBody, "data.radiationDose =", "data.radiationGridRleLength = EncodeSparseRle");

            StringAssert.Contains("if (HasActiveRadiationJobs() || _radiationSdfSnapshotLocked)", saveSnapshotBody);
            StringAssert.Contains("DispatcherJobFence.BeginPostSimulationSwapWindow();", saveSnapshotBody);
            StringAssert.Contains("DispatcherJobFence.EndPostSimulationSwapWindow();", saveSnapshotBody);
            StringAssert.Contains("CompleteRadiationSimulationJobForForcedSwapWindow();", saveSnapshotBody);
            StringAssert.Contains("CompleteDiffusionJobForForcedSwapWindow();", saveSnapshotBody);
            StringAssert.Contains("ReleaseRadiationSdfSnapshotLock();", saveSnapshotBody);
            StringAssert.Contains("CaptureSanitizedRadiationStateFromRuntimeBuffer();", saveSnapshotBody);
            StringAssert.Contains("if (HasDeferredStructuralOperations() && !HasActiveRadiationJobs())", saveSnapshotBody);
            StringAssert.Contains("TryApplyDeferredStructuralOperations();", saveSnapshotBody);
            AssertSourceOrder(saveSnapshotBody, "CompleteRadiationSimulationJobForForcedSwapWindow();", "CompleteDiffusionJobForForcedSwapWindow();");
            AssertSourceOrder(saveSnapshotBody, "CompleteDiffusionJobForForcedSwapWindow();", "ReleaseRadiationSdfSnapshotLock();");
            AssertSourceOrder(saveSnapshotBody, "ReleaseRadiationSdfSnapshotLock();", "CaptureSanitizedRadiationStateFromRuntimeBuffer();");
            AssertSourceOrderAfter(saveSnapshotBody, "TryApplyDeferredStructuralOperations();", "CaptureSanitizedRadiationStateFromRuntimeBuffer();");
            Assert.AreEqual(2, Count(saveSnapshotBody, "CaptureSanitizedRadiationStateFromRuntimeBuffer();"));

            StringAssert.Contains("DispatcherJobFence.TryComplete(ref _diffusionJobHandle, forceComplete: true);", diffusionBody);
            StringAssert.Contains("_diffusionJobActive = false;", diffusionBody);
            StringAssert.Contains("_gridVersion++;", diffusionBody);

            StringAssert.Contains("DispatcherJobFence.TryComplete(ref _radiationSimulationJobHandle, forceComplete: true);", forcedRadiationBody);
            StringAssert.Contains("_radiationSimulationJobActive = false;", forcedRadiationBody);
            StringAssert.Contains("ReleaseRadiationSdfSnapshotLock();", forcedRadiationBody);
            StringAssert.Contains("_lastBurstExecutionMicroseconds = TicksToMicroseconds(Stopwatch.GetTimestamp() - _radiationSimulationStartTicks);", forcedRadiationBody);

            StringAssert.Contains("if (!HasActiveRadiationJobs() && !_radiationSdfSnapshotLocked)", teardownBody);
            StringAssert.Contains("DispatcherJobFence.BeginPostSimulationSwapWindow();", teardownBody);
            StringAssert.Contains("CompleteRadiationSimulationJobForForcedSwapWindow();", teardownBody);
            StringAssert.Contains("CompleteDiffusionJobForForcedSwapWindow();", teardownBody);
            StringAssert.Contains("ReleaseRadiationSdfSnapshotLock();", teardownBody);
            StringAssert.Contains("DispatcherJobFence.EndPostSimulationSwapWindow();", teardownBody);
        }

        [Test]
        public void SaveSnapshotCommitsSanitizedRuntimeStateWithoutGameplaySignalFanout()
        {
            string source = ReadRadiationGridSource();
            string postSimulationBody = ExtractMethodBody(source, "private void PostSimulationRadiation(");
            string saveSnapshotBody = ExtractMethodBody(source, "private void CompleteRadiationJobsForSaveSnapshot()");
            string captureBody = ExtractMethodBody(source, "private void CaptureSanitizedRadiationStateFromRuntimeBuffer()");

            StringAssert.Contains("CaptureSanitizedRadiationStateFromRuntimeBuffer();", postSimulationBody);
            StringAssert.DoesNotContain("state.CumulativeDoseRad = SanitizeNonNegative(state.CumulativeDoseRad);", postSimulationBody);

            StringAssert.Contains("_radiationStates.IsCreated && _radiationStates.Length > 0", captureBody);
            StringAssert.Contains("!IsRadiationStateFinite(in state)", captureBody);
            StringAssert.Contains("DumpBlackBox();", captureBody);
            StringAssert.Contains("state = default;", captureBody);
            StringAssert.Contains("state.CumulativeDoseRad = SanitizeNonNegative(state.CumulativeDoseRad);", captureBody);
            StringAssert.Contains("state.CurrentExposureRate = SanitizeNonNegative(state.CurrentExposureRate);", captureBody);
            StringAssert.Contains("state.ShieldingFactor01 = Sanitize01(state.ShieldingFactor01);", captureBody);
            StringAssert.Contains("state.CellularDegradation01 = Sanitize01(state.CellularDegradation01);", captureBody);
            StringAssert.Contains("_radiationStates[0] = state;", captureBody);
            StringAssert.Contains("_lastRadiationState = state;", captureBody);
            StringAssert.Contains("_lastGridIntensity01 = state.CurrentExposureRate;", captureBody);
            StringAssert.Contains("_accumulatedRadiationDose = state.CumulativeDoseRad;", captureBody);
            StringAssert.Contains("_lastShieldingFactor01 = state.ShieldingFactor01;", captureBody);
            StringAssert.Contains("_lastCellularDegradation01 = state.CellularDegradation01;", captureBody);

            StringAssert.DoesNotContain("PublishDoseSignal", saveSnapshotBody);
            StringAssert.DoesNotContain("EmitGeigerIfNeeded", saveSnapshotBody);
            StringAssert.DoesNotContain("ApplyDoseToPlayerContext", saveSnapshotBody);
            StringAssert.DoesNotContain("PublishPendingRadiationStatusSignal", saveSnapshotBody);
        }

        [Test]
        public void LoadRestoresFullRadiationStateWithoutPreservingStaleRuntimeFields()
        {
            string source = ReadRadiationGridSource();
            string applyLoadBody = ExtractMethodBody(source, "private void ApplySaveDataImmediate(");
            string storeBody = ExtractMethodBody(source, "private void StoreRestoredRadiationState(");

            StringAssert.Contains("StoreRestoredRadiationState(0f, 0f, 0f);", applyLoadBody);
            StringAssert.Contains("float restoredRadiationDose = math.max(0f, math.isfinite(data.radiationDose) ? data.radiationDose : 0f);", applyLoadBody);
            StringAssert.Contains("StoreRestoredRadiationState(restoredRadiationDose, 0f, 0f);", applyLoadBody);
            AssertSourceOrder(applyLoadBody, "float restoredRadiationDose =", "StoreRestoredRadiationState(restoredRadiationDose, 0f, 0f);");
            StringAssert.DoesNotContain("RadiationStateDTO state = _radiationStates[0];", applyLoadBody);
            StringAssert.DoesNotContain("state.CumulativeDoseRad = _accumulatedRadiationDose;", applyLoadBody);
            StringAssert.DoesNotContain("state.CurrentExposureRate = SanitizeNonNegative(_lastGridIntensity01);", applyLoadBody);

            StringAssert.Contains("RadiationStateDTO state = new RadiationStateDTO", storeBody);
            StringAssert.Contains("CumulativeDoseRad = SanitizeNonNegative(cumulativeDoseRad)", storeBody);
            StringAssert.Contains("CurrentExposureRate = SanitizeNonNegative(exposureRate)", storeBody);
            StringAssert.Contains("ShieldingFactor01 = 0f", storeBody);
            StringAssert.Contains("CellularDegradation01 = Sanitize01(cellularDegradation01)", storeBody);
            StringAssert.Contains("EntityHashID = RadiationSystemHash", storeBody);
            StringAssert.Contains("Flags = ResolveRestoredRadiationStateFlags(cellularDegradation01)", storeBody);
            StringAssert.Contains("_radiationStates[0] = state;", storeBody);
            StringAssert.Contains("_accumulatedRadiationDose = state.CumulativeDoseRad;", storeBody);
            StringAssert.Contains("_lastRadiationState = state;", storeBody);
            StringAssert.Contains("_lastGridIntensity01 = state.CurrentExposureRate;", storeBody);
            StringAssert.Contains("_lastShieldingFactor01 = state.ShieldingFactor01;", storeBody);
            StringAssert.Contains("_lastCellularDegradation01 = state.CellularDegradation01;", storeBody);
        }

        private static string ReadRadiationGridSource()
        {
            return File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static void AssertSourceOrder(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing source token: " + after);
            Assert.Less(beforeIndex, afterIndex);
        }

        private static void AssertSourceOrderAfter(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
            int afterIndex = source.IndexOf(after, beforeIndex + before.Length, StringComparison.Ordinal);

            Assert.GreaterOrEqual(afterIndex, 0, "Missing source token after '" + before + "': " + after);
            Assert.Less(beforeIndex, afterIndex);
        }

        private static int Count(string source, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
