using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class VegetationAsyncJobFence1408EditTests
    {
        private static readonly string[] s_assignedSources =
        {
            "World/VegetationChunkResidencyDirector.cs",
            "World/VegetationFlowFieldIntegrator.cs",
            "World/HectonMapMagicVegetationBridge.cs",
            "World/VegetationTileCacheResidency.cs",
            "World/VegetationTerrainHoleSynchronizer.cs",
            "World/GroundPenetratingRadarRuntime.cs",
            "World/VegetationNavGridSynchronizer.cs"
        };

        [Test]
        public void AssignedVegetationSources_ContainNoRawRunOrCompleteTokens()
        {
            for (int i = 0; i < s_assignedSources.Length; i++)
            {
                string source = ReadProjectScript(s_assignedSources[i]);
                Assert.AreEqual(0, CountOccurrences(source, ".Run("), s_assignedSources[i]);
                Assert.AreEqual(0, CountOccurrences(source, ".Complete("), s_assignedSources[i]);
            }
        }

        [Test]
        public void ChunkBuildFinalization_UsesNonForcedDispatcherFenceBeforeReadingJobOutput()
        {
            string source = ReadProjectScript("World/VegetationChunkResidencyDirector.cs");
            string body = ExtractMethodBody(source, "FinalizeChunkBuildJob");

            AssertOrdered(
                body,
                "DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: false)",
                "BuildChunkPayloadFromJob");
            AssertOrdered(body, "BuildChunkPayloadFromJob", "ReleaseChunkBuildPendingJob(ref pending)");
            Assert.That(body, Does.Contain("_chunkBuildJobs[slot] = default"));
        }

        [Test]
        public void ChunkBuildTileCacheLifetimes_UseInactiveWritesRevisionRejectAndForcedDispose()
        {
            string residencySource = ReadProjectScript("World/VegetationChunkResidencyDirector.cs");
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string tileResidencySource = ReadProjectScript("World/VegetationTileCacheResidency.cs");
            string terrainHoleSource = ReadProjectScript("World/VegetationTerrainHoleSynchronizer.cs");

            string scheduleBody = ExtractMethodBody(residencySource, "ScheduleChunkBuild");
            string finalizeBody = ExtractMethodBody(residencySource, "FinalizeChunkBuildJob");
            string cacheMasksBody = ExtractMethodBody(bridgeSource, "CacheTileMasks");
            string readbackSweepBody = ExtractMethodBody(tileResidencySource, "FinalizePendingTileHeightReadbacks");
            string finalizeReadbackBody = ExtractMethodBody(tileResidencySource, "TryFinalizeTileHeightReadback");
            string disposeBody = ExtractMethodBody(terrainHoleSource, "DisposeTileNativeCaches");

            AssertOrdered(scheduleBody, "TryGetActiveTileCache", "grassJob.Schedule");
            Assert.That(scheduleBody, Does.Contain("TileCacheRevision = state.CacheRevision"));
            AssertOrdered(finalizeBody, "IsJobStateCurrent(pending.JobState)", "BuildChunkPayloadFromJob");

            AssertOrdered(cacheMasksBody, "int writeBufferIndex = state.ActiveCacheBufferIndex == 0 ? 1 : 0", "EnsureTileNativeCacheBufferCapacity");
            AssertOrdered(readbackSweepBody, "TryFinalizeTileHeightReadback(state)", "InvalidateTileChunks");
            AssertOrdered(finalizeReadbackBody, "state.ActiveCacheBufferIndex = state.PendingCacheBufferIndex", "state.CacheRevision++");
            AssertOrdered(disposeBody, "CompleteAndReleaseChunkBuildJobsForTile", "DisposeTileNativeCacheBuffer(ref state.PrimaryCacheBuffer)");
        }

        [Test]
        public void FlowFieldCompletion_UsesDispatcherFenceBeforeDataVaultPublishAndNativeRelease()
        {
            string source = ReadProjectScript("World/VegetationFlowFieldIntegrator.cs");

            AssertFlowCompletionContract(source, "CompleteThreatPropagationJob", "ReleaseThreatPropagationPendingJob");
            AssertFlowCompletionContract(source, "CompleteFlowFieldJob", "ReleaseFlowFieldPendingJob");
            AssertFlowCompletionContract(source, "CompleteThermalGridJob", "ReleaseThermalGridPendingJob");
        }

        [Test]
        public void ThreatAndFlowScheduling_CopyThreatGridBeforeCrossFrameJobs()
        {
            string source = ReadProjectScript("World/VegetationFlowFieldIntegrator.cs");
            string threatSchedule = ExtractMethodBody(source, "ScheduleThreatPropagationJob");
            string flowSchedule = ExtractMethodBody(source, "ScheduleFlowFieldJob");
            string threatRelease = ExtractMethodBody(source, "ReleaseThreatPropagationPendingJob");
            string flowRelease = ExtractMethodBody(source, "ReleaseFlowFieldPendingJob");

            AssertOrdered(threatSchedule, "NativeArray<float>.Copy(currentThreat, previousThreat", "CurrentThreat = previousThreat");
            AssertOrdered(threatSchedule, "CurrentThreat = previousThreat", "PreviousThreat = previousThreat");
            Assert.That(threatRelease, Does.Contain("pending.PreviousThreat"));

            AssertOrdered(flowSchedule, "NativeArray<float>.Copy(currentThreatGrid, threatGridSnapshot", "ThreatGrid = threatGridSnapshot");
            AssertOrdered(flowSchedule, "ThreatGrid = threatGridSnapshot", "ThreatGridSnapshot = threatGridSnapshot");
            Assert.That(flowRelease, Does.Contain("pending.ThreatGridSnapshot"));
        }

        [Test]
        public void HotSchedulingPhase_DoesNotReadBackOrFenceWorkerJobs()
        {
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string slowTick = ExtractMethodBody(bridgeSource, "SlowTick");

            Assert.That(slowTick, Does.Not.Contain("TryComplete"));
            Assert.That(slowTick, Does.Not.Contain("TryCopyVegetationMemorySnapshot"));
            Assert.That(slowTick, Does.Not.Contain(".Run("));
            Assert.That(slowTick, Does.Not.Contain(".Complete("));
        }

        [Test]
        public void LateFrameAndTeardown_AreTheExplicitVegetationCompletionGates()
        {
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string lateFrame = ExtractMethodBody(bridgeSource, "LateFrameTick");
            string onDisable = ExtractMethodBody(bridgeSource, "OnDisable");
            string onDestroy = ExtractMethodBody(bridgeSource, "OnDestroy");

            Assert.That(lateFrame, Does.Contain("FinalizeCompletedChunkBuilds()"));
            Assert.That(lateFrame, Does.Contain("CompleteThreatPropagationJob(forceComplete: false)"));
            Assert.That(lateFrame, Does.Contain("CompleteFlowFieldJob(forceComplete: false)"));
            Assert.That(lateFrame, Does.Contain("CompleteThermalGridJob(forceComplete: false)"));
            Assert.That(lateFrame, Does.Contain("CompleteAbyssalPathJob(forceComplete: false)"));

            AssertTeardownForcesBeforeDispose(onDisable);
            AssertTeardownForcesBeforeDispose(onDestroy);
        }

        [Test]
        public void StaticHotPathAudit_RejectsSynchronousTokensOutsideLateFrameAndTeardown()
        {
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            AssertNoSynchronousTokensInMethod(bridgeSource, "Tick");
            AssertNoSynchronousTokensInMethod(bridgeSource, "SlowTick");

            string residencySource = ReadProjectScript("World/VegetationChunkResidencyDirector.cs");
            AssertNoSynchronousTokensInMethod(residencySource, "ScheduleChunkBuild");
            AssertNoSynchronousTokensInMethod(residencySource, "FinalizeCompletedChunkBuilds");

            string flowSource = ReadProjectScript("World/VegetationFlowFieldIntegrator.cs");
            AssertNoSynchronousTokensInMethod(flowSource, "ScheduleThreatPropagationJob");
            AssertNoSynchronousTokensInMethod(flowSource, "ScheduleFlowFieldJob");
            AssertNoSynchronousTokensInMethod(flowSource, "ScheduleThermalGridJob");

            string groundRadarSource = ReadProjectScript("World/GroundPenetratingRadarRuntime.cs");
            AssertNoSynchronousTokensInMethod(groundRadarSource, "ScheduleRadarJob");
            AssertNoSynchronousTokensInMethod(groundRadarSource, "LateFrameTick");

            string navSource = ReadProjectScript("World/VegetationNavGridSynchronizer.cs");
            AssertNoSynchronousTokensInMethod(navSource, "CompleteAbyssalPathJob");
        }

        [Test]
        public void GroundRadarScheduling_DefersReadbackUntilDispatcherCompletion()
        {
            string source = ReadProjectScript("World/GroundPenetratingRadarRuntime.cs");
            string scheduleBody = ExtractMethodBody(source, "ScheduleRadarJob");
            string completeBody = ExtractMethodBody(source, "CompleteRadarJob");

            AssertOrdered(scheduleBody, "TryCreateRadarPendingJob(out pending)", "GroundRadarRaymarchJob job = new GroundRadarRaymarchJob");
            AssertOrdered(scheduleBody, "TryCopyCurrentGprStateToPending(ref pending)", "GroundRadarRaymarchJob job = new GroundRadarRaymarchJob");
            AssertOrdered(scheduleBody, "JobHandle handle = job.Schedule()", "_radarJobScheduled = 1");
            AssertOrdered(scheduleBody, "_radarSdfSnapshotLocked = sdfSnapshotLocked", "oreDependencySink.RegisterOreReadDependency(handle)");
            AssertOrdered(scheduleBody, "oreDependencySink == null", "GroundRadarRaymarchJob job = new GroundRadarRaymarchJob");
            Assert.That(scheduleBody, Does.Not.Contain("CommitCompletedScan"));
            Assert.That(scheduleBody, Does.Not.Contain(".Run("));

            AssertOrdered(completeBody, "DispatcherJobFence.TryComplete(ref _radarJobHandle, forceComplete)", "CommitCompletedScan(ref pending)");
            AssertOrdered(completeBody, "CommitCompletedScan(ref pending)", "ReleaseRadarPendingJob(ref pending)");
            Assert.That(completeBody, Does.Contain("_radarJobScheduled = 0"));
            Assert.That(completeBody, Does.Not.Contain(".Complete("));
        }

        [Test]
        public void GroundRadarRaymarch_UsesTempStagingAndShortWriteLockPublish()
        {
            string source = ReadProjectScript("World/GroundPenetratingRadarRuntime.cs");
            string scheduleBody = ExtractMethodBody(source, "ScheduleRadarJob");
            string copyBody = ExtractMethodBody(source, "TryCopyCurrentGprStateToPending");
            string publishBody = ExtractMethodBody(source, "TryPublishRadarPendingJob");

            Assert.That(scheduleBody, Does.Contain("new NativeSlice<float3>(pending.Hits)"));
            Assert.That(scheduleBody, Does.Contain("new NativeSlice<float>(pending.SignalStrength)"));
            Assert.That(scheduleBody, Does.Contain("new NativeSlice<float4>(pending.PingGpu)"));
            Assert.That(scheduleBody, Does.Not.Contain("new NativeSlice<float3>(hits)"));
            Assert.That(scheduleBody, Does.Not.Contain("new NativeSlice<float4>(pingGpu)"));

            AssertOrdered(copyBody, "TryLockScanJobBuffers()", "NativeArray<float3>.Copy(hits, pending.Hits");
            AssertOrdered(copyBody, "NativeArray<float4>.Copy(pingGpu, pending.PingGpu", "ReleaseScanJobBufferLocks()");

            AssertOrdered(publishBody, "TryAcquireWriteLock(in _gprHitsHandle", "NativeArray<float3>.Copy(pending.Hits, hits");
            AssertOrdered(publishBody, "TryAcquireWriteLock(in _gprPingGpuHandle", "NativeArray<float4>.Copy(pending.PingGpu, pingGpu");
            AssertOrdered(publishBody, "NativeArray<float4>.Copy(pending.PingGpu, pingGpu", "ReleaseWriteLock(in _gprPingGpuHandle");
            AssertOrdered(publishBody, "NativeArray<float3>.Copy(pending.Hits, hits", "ReleaseWriteLock(in _gprHitsHandle");
            Assert.That(publishBody, Does.Contain("ReleaseWriteLock(in _maxSignalStrengthHandle"));
            Assert.That(publishBody, Does.Contain("finally"));
        }

        [Test]
        public void AbyssalPathScheduling_DefersReadbackUntilDispatcherCompletion()
        {
            string source = ReadProjectScript("World/VegetationNavGridSynchronizer.cs");
            string scheduleBody = ExtractMethodBodyFromSignature(
                source,
                "public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, int traversalSpeciesId");
            string completeBody = ExtractMethodBody(source, "CompleteAbyssalPathJob");

            AssertOrdered(scheduleBody, "JobHandle smoothingHandle = smoothingJob.Schedule(pathSourceHandle)", "_abyssalPathJob = new AbyssalPathPendingJob");
            AssertOrdered(scheduleBody, "_abyssalPathJob = new AbyssalPathPendingJob", "handle = smoothingHandle");
            AssertOrdered(scheduleBody, "threatGridSource.AsReadOnly()", "ThreatGrid = threatGridForJob");
            AssertOrdered(scheduleBody, "threatVoxelGridSource.AsReadOnly()", "ThreatVoxelGrid = threatVoxelGridForJob");
            string afterAStarThreatInputs = scheduleBody.Substring(
                scheduleBody.IndexOf("ThreatVoxelGrid = threatVoxelGridForJob", StringComparison.Ordinal));
            Assert.That(afterAStarThreatInputs, Does.Contain("ref threatGridForJob"));
            Assert.That(afterAStarThreatInputs, Does.Contain("ref threatVoxelGridForJob"));
            Assert.That(scheduleBody, Does.Not.Contain("ForceCompleteAbyssalPathDependency(ref smoothingHandle)"));
            Assert.That(scheduleBody, Does.Not.Contain("CompleteAbyssalPathJob"));
            Assert.That(scheduleBody, Does.Not.Contain("CommitAbyssalPathResult"));
            Assert.That(source, Does.Not.Contain("ForceCompleteAbyssalPathDependency"));

            AssertOrdered(completeBody, "DispatcherJobSwap.TryComplete(ref handle, forceComplete)", "CommitAbyssalPathResult");
            AssertOrdered(completeBody, "CommitAbyssalPathResult", "ReleaseAbyssalPathPendingJob(ref pending)");
            Assert.That(completeBody, Does.Contain("_abyssalPathScheduled = false"));
        }

        [Test]
        public void AbyssalPathBudgets_UseContinuousGlobalQualityWeight()
        {
            string source = ReadProjectScript("World/VegetationNavGridSynchronizer.cs");
            string portalBody = ExtractMethodBody(source, "ResolveAbyssalPathPortalLookAhead");
            string sampleBody = ExtractMethodBody(source, "ResolveAbyssalPathDdaSampleCap");
            string budgetBody = ExtractMethodBody(source, "ResolveAbyssalPathQualityBudget");

            Assert.That(source, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
            Assert.That(portalBody, Does.Not.Contain("return HighTierAbyssalPathPortalLookAhead"));
            AssertOrdered(portalBody, "ResolveAbyssalPathQualityWeight()", "ResolveAbyssalPathQualityBudget");
            AssertOrdered(sampleBody, "ResolveAbyssalPathQualityWeight()", "ResolveAbyssalPathQualityBudget");
            Assert.That(budgetBody, Does.Contain("math.lerp"));
            Assert.That(budgetBody, Does.Not.Contain("isLowEnd"));
        }

        [Test]
        public void HlodReadAndSlowTickPaths_DoNotCompleteJobsOutsideDispatcherGate()
        {
            string source = ReadProjectScript("World/VegetationNavGridSynchronizer.cs");
            string readBody = ExtractMethodBody(source, "TryGetVisibleHLODPayload");
            string rebuildBody = ExtractMethodBody(source, "RebuildHLODRegistrySnapshot");
            string completeBody = ExtractMethodBody(source, "CompleteHLODCullJob");

            Assert.That(readBody, Does.Not.Contain("CompleteHLODCullJob"));
            Assert.That(readBody, Does.Not.Contain("TryComplete"));
            Assert.That(rebuildBody, Does.Not.Contain("CompleteHLODCullJob"));
            Assert.That(rebuildBody, Does.Not.Contain("TryComplete"));
            Assert.That(completeBody, Does.Contain("DispatcherJobSwap.TryComplete"));
        }

        private static void AssertFlowCompletionContract(string source, string completeMethodName, string releaseMethodName)
        {
            string body = ExtractMethodBody(source, completeMethodName);

            AssertOrdered(body, "DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete)", "TryCopyVegetationMemorySnapshot");
            AssertOrdered(body, "TryCopyVegetationMemorySnapshot", releaseMethodName + "(ref pending)");
            Assert.That(body, Does.Contain("= default"));
            Assert.That(body, Does.Contain("= false"));
        }

        private static void AssertTeardownForcesBeforeDispose(string body)
        {
            AssertOrdered(body, "CompleteThreatPropagationJob(forceComplete: true)", "DisposeThreatGridState()");
            AssertOrdered(body, "CompleteFlowFieldJob(forceComplete: true)", "DisposeFlowFieldState()");
            AssertOrdered(body, "CompleteThermalGridJob(forceComplete: true)", "DisposeThermalGridState()");
            AssertOrdered(body, "DisposeAllChunkBuildJobs()", "DisposeAllTileNativeCaches()");
        }

        private static void AssertNoSynchronousTokensInMethod(string source, string methodName)
        {
            string body = ExtractMethodBody(source, methodName);
            Assert.That(body, Does.Not.Contain(".Run("), methodName);
            Assert.That(body, Does.Not.Contain(".Complete("), methodName);
        }

        private static void AssertOrdered(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);

            Assert.GreaterOrEqual(firstIndex, 0, first);
            Assert.GreaterOrEqual(secondIndex, 0, second);
            Assert.Less(firstIndex, secondIndex, first + " before " + second);
        }

        private static string ReadProjectScript(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts", relativePath));
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int methodIndex = source.IndexOf(methodName + "(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, methodName);
            return ExtractBodyFromIndex(source, methodIndex, methodName);
        }

        private static string ExtractMethodBodyFromSignature(string source, string signature)
        {
            int methodIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, signature);
            return ExtractBodyFromIndex(source, methodIndex, signature);
        }

        private static string ExtractBodyFromIndex(string source, int methodIndex, string diagnosticName)
        {
            int openBrace = source.IndexOf('{', methodIndex);
            Assert.GreaterOrEqual(openBrace, 0, diagnosticName);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char value = source[i];
                if (value == '{')
                    depth++;
                else if (value == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail(diagnosticName);
            return string.Empty;
        }
    }
}
