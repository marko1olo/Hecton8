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
            Assert.That(cacheMasksBody, Does.Not.Contain("SyncHeightmap"));
            AssertOrdered(cacheMasksBody, "Texture heightTexture = state.HeightTextureCache", "AsyncGPUReadback.Request");
        }

        [Test]
        public void FlowFieldCompletion_UsesDispatcherFenceBeforeDataVaultPublishAndNativeRelease()
        {
            string source = ReadProjectScript("World/VegetationFlowFieldIntegrator.cs");
            string helperBody = ExtractMethodBody(source, "TryCompleteVegetationSimulationJob");

            AssertFlowCompletionContract(source, "CompleteThreatPropagationJob", "ReleaseThreatPropagationPendingJob");
            AssertFlowCompletionContract(source, "CompleteFlowFieldJob", "ReleaseFlowFieldPendingJob");
            AssertFlowCompletionContract(source, "CompleteThermalGridJob", "ReleaseThermalGridPendingJob");
            Assert.That(helperBody, Does.Contain("DispatcherJobSwap.TryComplete(ref handle, forceComplete: false)"));
            AssertCompleteInsidePostSimulationWindow(
                helperBody,
                "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");

            string cancelBody = ExtractMethodBody(source, "CancelVegetationSimulationJobsForResidencyClear");
            Assert.That(cancelBody, Does.Contain("pending.Cancelled = true"));
            Assert.That(cancelBody, Does.Contain("_threatPropagationJob = pending"));
            Assert.That(cancelBody, Does.Contain("_flowFieldJob = pending"));
            Assert.That(cancelBody, Does.Contain("_thermalGridJob = pending"));
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
        public void OriginShift_QueuesInsteadOfForceCompletingAsyncVegetationJobs()
        {
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string applyBody = ExtractMethodBodyFromSignature(bridgeSource, "private bool TryApplyWorldOffsetToAllChunks");
            string pendingBody = ExtractMethodBodyFromSignature(bridgeSource, "private void TryApplyPendingWorldOffset");
            string immediateBody = ExtractMethodBodyFromSignature(bridgeSource, "private void ApplyWorldOffsetToAllChunksImmediate");
            string lateFrame = ExtractMethodBody(bridgeSource, "LateFrameTick");

            AssertOrdered(applyBody, "HasAsyncWorldJobsInFlight()", "QueuePendingWorldOffset");
            AssertOrdered(pendingBody, "HasAsyncWorldJobsInFlight()", "ApplyWorldOffsetToAllChunksImmediate");
            AssertOrdered(lateFrame, "CompleteThermalGridJob(forceComplete: false)", "TryApplyPendingWorldOffset()");
            Assert.That(immediateBody, Does.Not.Contain("forceComplete: true"));
            Assert.That(immediateBody, Does.Not.Contain("DisposeAllChunkBuildJobs"));
        }

        [Test]
        public void ClearResidency_CancelsAsyncJobsInsteadOfForceDrainingChunkJobs()
        {
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string clearBody = ExtractMethodBody(bridgeSource, "ClearAllResidency");
            string cancelBody = ExtractMethodBody(bridgeSource, "CancelAsyncWorldJobsForResidencyClear");
            string chunkCancelBody = ExtractMethodBody(bridgeSource, "CancelAllChunkBuildJobs");
            string disposeBody = ExtractMethodBody(bridgeSource, "DisposeAllChunkBuildJobs");

            Assert.That(clearBody, Does.Contain("CancelAsyncWorldJobsForResidencyClear()"));
            Assert.That(clearBody, Does.Not.Contain("DisposeAllChunkBuildJobs"));
            Assert.That(clearBody, Does.Not.Contain("forceComplete: true"));
            Assert.That(cancelBody, Does.Contain("CancelAllChunkBuildJobs()"));
            Assert.That(cancelBody, Does.Contain("CancelVegetationSimulationJobsForResidencyClear()"));
            Assert.That(cancelBody, Does.Contain("InvalidateAbyssalPathState()"));
            Assert.That(chunkCancelBody, Does.Contain("MarkChunkBuildJobCancelled(i)"));
            Assert.That(disposeBody, Does.Contain("CompleteAndReleaseChunkBuildJob(i)"));
        }

        [Test]
        public void ChunkBuildTeardown_ForceCompletesInsidePostSimulationWindowBeforeReadPinRelease()
        {
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string releaseBody = ExtractMethodBody(bridgeSource, "CompleteAndReleaseChunkBuildJob");
            string helperBody = ExtractMethodBody(bridgeSource, "ForceCompleteChunkBuildJobInPostSimulationWindow");

            AssertOrdered(
                releaseBody,
                "ForceCompleteChunkBuildJobInPostSimulationWindow(ref pending.Handle)",
                "ReleaseChunkBuildPendingJob(ref pending)");
            Assert.That(
                releaseBody,
                Does.Not.Contain("DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: true)"));
            Assert.That(releaseBody, Does.Contain("_chunkBuildJobs[slot] = pending;"));
            AssertCompleteInsidePostSimulationWindow(
                helperBody,
                "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");
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

            AssertOrdered(scheduleBody, "TryCreateRadarPendingJob()", "GroundRadarRaymarchJob job = new GroundRadarRaymarchJob");
            AssertOrdered(scheduleBody, "TryCopyCurrentGprStateToPending(ref _radarJob)", "GroundRadarRaymarchJob job = new GroundRadarRaymarchJob");
            AssertOrdered(scheduleBody, "TryStageNearestSdf(probeOrigin, ref _radarJob", "GroundRadarRaymarchJob job = new GroundRadarRaymarchJob");
            AssertOrdered(scheduleBody, "JobHandle handle = job.Schedule()", "_radarJobScheduled = 1");
            AssertOrdered(scheduleBody, "oreDependencySink == null", "GroundRadarRaymarchJob job = new GroundRadarRaymarchJob");
            AssertOrdered(scheduleBody, "TryStageNearestSdf(probeOrigin, ref _radarJob", "EncodedSdf = encodedSdf");
            Assert.That(scheduleBody, Does.Not.Contain("_radarSdfSnapshotLocked"));
            Assert.That(scheduleBody, Does.Not.Contain("CommitCompletedScan"));
            Assert.That(scheduleBody, Does.Not.Contain("ReleaseRadarPendingJob"));
            Assert.That(scheduleBody, Does.Not.Contain(".Run("));

            AssertOrdered(completeBody, "DispatcherJobFence.TryComplete(ref _radarJobHandle, forceComplete)", "CommitCompletedScan(ref _radarJob)");
            AssertOrdered(completeBody, "CommitCompletedScan(ref _radarJob)", "RetireRadarPendingJobForReuse(ref _radarJob)");
            Assert.That(completeBody, Does.Contain("_radarJobScheduled = 0"));
            Assert.That(completeBody, Does.Not.Contain("ReleaseRadarPendingJob"));
            Assert.That(completeBody, Does.Not.Contain(".Complete("));
        }

        [Test]
        public void GroundRadarRaymarch_UsesReusableStagingAndShortWriteLockPublish()
        {
            string source = ReadProjectScript("World/GroundPenetratingRadarRuntime.cs");
            string scheduleBody = ExtractMethodBody(source, "ScheduleRadarJob");
            string createBody = ExtractMethodBody(source, "TryCreateRadarPendingJob");
            string copyBody = ExtractMethodBody(source, "TryCopyCurrentGprStateToPending");
            string sdfStageBody = ExtractMethodBody(source, "TryStageSdfLeaseToPendingSnapshot");
            string releaseBody = ExtractMethodBody(source, "ReleaseRadarPendingJob");
            string publishBody = ExtractMethodBody(source, "TryPublishRadarPendingJob");
            string publishHelperBody = ExtractMethodBody(source, "TryCopyPendingBufferToVault");

            Assert.That(createBody, Does.Contain("Allocator.Persistent"));
            Assert.That(createBody, Does.Not.Contain("Allocator.TempJob"));
            Assert.That(scheduleBody, Does.Contain("new NativeSlice<float3>(_radarJob.Hits)"));
            Assert.That(scheduleBody, Does.Contain("new NativeSlice<float>(_radarJob.SignalStrength)"));
            Assert.That(scheduleBody, Does.Contain("new NativeSlice<float4>(_radarJob.PingGpu)"));
            Assert.That(scheduleBody, Does.Not.Contain("new NativeSlice<float3>(hits)"));
            Assert.That(scheduleBody, Does.Not.Contain("new NativeSlice<float4>(pingGpu)"));

            AssertOrdered(copyBody, "TryPinScanJobBuffers(vault)", "NativeArray<float3>.Copy(hits, pending.Hits");
            AssertOrdered(copyBody, "NativeArray<float4>.Copy(pingGpu, pending.PingGpu", "ReleaseScanJobBufferPins()");
            Assert.That(sdfStageBody, Does.Contain("Allocator.Persistent"));
            Assert.That(sdfStageBody, Does.Not.Contain("Allocator.TempJob"));
            AssertOrdered(sdfStageBody, "H8Memory.Allocate<byte>", "snapshotSdf = pending.SdfSnapshot.AsReadOnly()");
            Assert.That(sdfStageBody, Does.Not.Contain("TryLockBuffer"));
            Assert.That(sdfStageBody, Does.Not.Contain("TryResolveHandle"));
            Assert.That(releaseBody, Does.Contain("pending.SdfSnapshot"));

            AssertOrdered(publishBody, "in _maxSignalStrengthHandle", "in _gprCountersHandle");
            Assert.That(publishBody, Does.Contain("TryCopyPendingBufferToVault"));
            Assert.That(publishBody, Does.Not.Contain("TryResolveHandle"));
            AssertOrdered(publishHelperBody, "TryAcquireWriteLock(in handle", "NativeArray<T>.Copy(source, target, copyLength)");
            AssertOrdered(publishHelperBody, "NativeArray<T>.Copy(source, target, copyLength)", "ReleaseWriteLock(in handle");
            Assert.That(publishHelperBody, Does.Contain("finally"));
        }

        [Test]
        public void GroundRadarPingUploadBufferMutation_IsNotNamedAsPureResolveAccessor()
        {
            string source = ReadProjectScript("World/GroundPenetratingRadarRuntime.cs");
            string acquireBody = ExtractMethodBody(source, "TryAcquireGprPingWriteBuffer");

            Assert.That(source, Does.Not.Contain("TryResolveGprPingWriteBuffer"));
            Assert.That(source, Does.Contain("TryAcquireGprPingWriteBuffer"));
            Assert.That(acquireBody, Does.Contain("_gprUploadBufferIndex ^= 1"));
        }

        [Test]
        public void AbyssalPathScheduling_DefersReadbackUntilDispatcherCompletion()
        {
            string source = ReadProjectScript("World/VegetationNavGridSynchronizer.cs");
            string scheduleBody = ExtractMethodBodyFromSignature(
                source,
                "public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, int traversalSpeciesId");
            string completeBody = ExtractMethodBody(source, "CompleteAbyssalPathJob");
            string helperBody = ExtractMethodBody(source, "TryCompleteVegetationNavJob");
            string invalidateBody = ExtractMethodBody(source, "InvalidateAbyssalPathState");

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

            AssertOrdered(completeBody, "TryCompleteVegetationNavJob(ref handle, forceComplete)", "CommitAbyssalPathResult");
            AssertOrdered(completeBody, "CommitAbyssalPathResult", "ReleaseAbyssalPathPendingJob(ref pending)");
            Assert.That(helperBody, Does.Contain("DispatcherJobSwap.TryComplete(ref handle, forceComplete: false)"));
            AssertCompleteInsidePostSimulationWindow(
                helperBody,
                "DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)");
            Assert.That(completeBody, Does.Contain("_abyssalPathScheduled = false"));
            Assert.That(completeBody, Does.Contain("!pending.Cancelled"));
            Assert.That(invalidateBody, Does.Contain("pending.Cancelled = true"));
            Assert.That(invalidateBody, Does.Not.Contain("CompleteAbyssalPathJob"));
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
            Assert.That(source, Does.Not.Contain("_lastAbyssalPathPortalLookAhead <= LowTierAbyssalPathPortalLookAhead"));
        }

        [Test]
        public void GrassLodBudget_UsesContinuousGlobalQualityWeight()
        {
            string source = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string tierBody = ExtractMethodBody(source, "GetGrassLodTier");
            string stepBody = ExtractMethodBody(source, "GetGrassStepForTier");
            string qualityBody = ExtractMethodBody(source, "ResolveGrassQualityWeight");

            Assert.That(source, Does.Not.Contain("_MATH_LOD_LOW"));
            Assert.That(tierBody, Does.Contain("ResolveGrassQualityWeight()"));
            Assert.That(tierBody, Does.Contain("math.lerp"));
            Assert.That(tierBody, Does.Contain("byte.MaxValue"));
            Assert.That(stepBody, Does.Contain("ResolveGrassQualityWeight()"));
            Assert.That(stepBody, Does.Contain("math.lerp"));
            Assert.That(stepBody, Does.Not.Contain("grassLodTier == 0"));
            Assert.That(qualityBody, Does.Contain("HomeostasisBrain.GlobalQualityWeight"));
        }

        [Test]
        public void CameraCacheRefresh_IsNotNamedAsPureResolveAccessor()
        {
            string bridgeSource = ReadProjectScript("World/HectonMapMagicVegetationBridge.cs");
            string residencySource = ReadProjectScript("World/VegetationChunkResidencyDirector.cs");
            string navSource = ReadProjectScript("World/VegetationNavGridSynchronizer.cs");

            Assert.That(bridgeSource, Does.Not.Contain("ResolveActiveViewCamera"));
            Assert.That(residencySource, Does.Contain("RefreshActiveViewCameraCache()"));
            Assert.That(navSource, Does.Contain("RefreshActiveViewCameraCache()"));
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
            Assert.That(completeBody, Does.Contain("TryCompleteVegetationNavJob(ref _hlodCullHandle, forceComplete)"));
        }

        private static void AssertCompleteInsidePostSimulationWindow(string method, string completeCall)
        {
            const string beginWindow = "BeginPostSimulationSwapWindow();";
            const string endWindow = "EndPostSimulationSwapWindow();";

            int completeIndex = method.IndexOf(completeCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(completeIndex, 0, completeCall);

            int beginIndex = method.LastIndexOf(beginWindow, completeIndex, StringComparison.Ordinal);
            int endIndex = method.IndexOf(endWindow, completeIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beginIndex, 0, completeCall);
            Assert.GreaterOrEqual(endIndex, 0, completeCall);
            Assert.Less(beginIndex, completeIndex, completeCall);
            Assert.Less(completeIndex, endIndex, completeCall);
        }

        private static void AssertFlowCompletionContract(string source, string completeMethodName, string releaseMethodName)
        {
            string body = ExtractMethodBody(source, completeMethodName);

            AssertOrdered(body, "pending.Cancelled", "TryCopyVegetationMemorySnapshot");
            AssertOrdered(body, "TryCompleteVegetationSimulationJob(ref pending.Handle, forceComplete)", "TryCopyVegetationMemorySnapshot");
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
            int methodIndex = FindMethodDeclarationIndex(source, methodName);
            Assert.GreaterOrEqual(methodIndex, 0, methodName);
            return ExtractBodyFromIndex(source, methodIndex, methodName);
        }

        private static int FindMethodDeclarationIndex(string source, string methodName)
        {
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int methodIndex = source.IndexOf(methodName, searchIndex, StringComparison.Ordinal);
                if (methodIndex < 0)
                    return -1;

                int afterName = methodIndex + methodName.Length;
                if (afterName < source.Length && source[afterName] == '<')
                {
                    int genericClose = source.IndexOf('>', afterName + 1);
                    if (genericClose < 0)
                        return -1;

                    afterName = genericClose + 1;
                }

                while (afterName < source.Length && char.IsWhiteSpace(source[afterName]))
                    afterName++;

                if (afterName < source.Length &&
                    source[afterName] == '(' &&
                    IsMethodDeclarationLine(source, methodIndex))
                {
                    return methodIndex;
                }

                searchIndex = methodIndex + methodName.Length;
            }

            return -1;
        }

        private static bool IsMethodDeclarationLine(string source, int methodIndex)
        {
            int lineStart = source.LastIndexOf('\n', Math.Max(0, methodIndex - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int firstNonWhitespace = lineStart;
            while (firstNonWhitespace < methodIndex && char.IsWhiteSpace(source[firstNonWhitespace]))
                firstNonWhitespace++;

            return StartsWithOrdinal(source, firstNonWhitespace, "public ") ||
                   StartsWithOrdinal(source, firstNonWhitespace, "private ") ||
                   StartsWithOrdinal(source, firstNonWhitespace, "internal ") ||
                   StartsWithOrdinal(source, firstNonWhitespace, "protected ");
        }

        private static bool StartsWithOrdinal(string source, int index, string prefix)
        {
            return index >= 0 &&
                   index + prefix.Length <= source.Length &&
                   string.CompareOrdinal(source, index, prefix, 0, prefix.Length) == 0;
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
