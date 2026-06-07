using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ConcurrencyPhaseLifecycle1426EditTests
    {
        private const int CompletedCounter = 0;
        private const int FailedCounter = 1;
        private const int NestedViolationCounter = 2;

        [Test]
        public void MemorySentinelValidationJob_SchedulesInSimulationAndFinalizesInPostSimulation()
        {
            string text = File.ReadAllText(MemorySentinelPath());
            string schedule = ExtractMethodBody(text, "ScheduleSimulation");
            string postSimulation = ExtractMethodBody(text, "PostSimulationTick");
            string visualSync = ExtractMethodBody(text, "VisualSyncTick");

            Assert.That(text, Does.Contain("RegisterDispatcherPhases();"));
            Assert.That(text, Does.Contain("PostSimulationPhaseSystem"));
            Assert.That(schedule, Does.Contain("new MemorySentinelValidationJob"));
            Assert.That(schedule, Does.Contain(".Schedule(_targetCount, DefaultTargetBatch, dependsOn)"));
            Assert.That(schedule, Does.Contain("H8Memory.RegisterActiveJob(OwnerSystemId, _validationHandle);"));
            Assert.That(postSimulation, Does.Contain("CompleteValidationJob(forceComplete: false)"));
            Assert.AreEqual(0, Count(visualSync, @"\.Schedule(?:Parallel)?\s*\("), "VisualSync job scheduling");
            Assert.AreEqual(0, Count(visualSync, @"TryLockBuffer|TryAcquireWriteLock|CompleteValidationJob"), "VisualSync lock or completion");
        }

        [Test]
        public void RuntimeVisualSyncTicks_DoNotScheduleJobs()
        {
            string[] files = Directory.GetFiles(RuntimeScriptsRoot(), "*.cs", SearchOption.AllDirectories);
            int violations = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string path = NormalizePath(files[i]);
                if (IsEditorOrDevPath(path))
                    continue;

                string text = File.ReadAllText(files[i]);
                foreach (Match match in Regex.Matches(text, @"(?m)^\s*(?:(?:public|private|protected|internal|static|readonly|unsafe|virtual|override|sealed|partial|new)\s+)*void\s+VisualSyncTick\s*\("))
                {
                    string body = ExtractBodyFromDeclaration(text, match.Index, path + " VisualSyncTick");
                    if (Count(body, @"\.Schedule(?:Parallel)?\s*\(") > 0)
                        violations++;
                }
            }

            Assert.AreEqual(0, violations, "VisualSyncTick scheduling violations");
        }

        [Test]
        public void RuntimeVisualSyncTicks_DoNotAcquireVaultLocks()
        {
            string[] files = Directory.GetFiles(RuntimeScriptsRoot(), "*.cs", SearchOption.AllDirectories);
            int violations = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string path = NormalizePath(files[i]);
                if (IsEditorOrDevPath(path))
                    continue;

                string text = File.ReadAllText(files[i]);
                foreach (Match match in Regex.Matches(text, @"(?m)^\s*(?:(?:public|private|protected|internal|static|readonly|unsafe|virtual|override|sealed|partial|new)\s+)*void\s+VisualSyncTick\s*\("))
                {
                    string body = ExtractBodyFromDeclaration(text, match.Index, path + " VisualSyncTick");
                    if (Count(body, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b") > 0)
                        violations++;
                }
            }

            Assert.AreEqual(0, violations, "VisualSyncTick Vault lock violations");
        }

        [Test]
        public void RuntimePreSimulationTicks_DoNotUploadGraphicsBuffers()
        {
            string[] files = Directory.GetFiles(RuntimeScriptsRoot(), "*.cs", SearchOption.AllDirectories);
            int violations = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string path = NormalizePath(files[i]);
                if (IsEditorOrDevPath(path))
                    continue;

                string text = File.ReadAllText(files[i]);
                foreach (Match match in Regex.Matches(text, @"(?m)^\s*(?:(?:public|private|protected|internal|static|readonly|unsafe|virtual|override|sealed|partial|new)\s+)*void\s+PreSimulationTick\s*\("))
                {
                    string body = ExtractBodyFromDeclaration(text, match.Index, path + " PreSimulationTick");
                    if (Count(body, @"\bGraphicsBuffer\s*\.\s*SetData\s*\(") > 0)
                        violations++;
                }
            }

            Assert.AreEqual(0, violations, "PreSimulation GraphicsBuffer.SetData violations");
        }

        [Test]
        public void DispatcherJobFence_FlagsForcedCompletionsOutsideSwapWindow()
        {
            string text = File.ReadAllText(DispatcherJobFencePath());

            Assert.That(text, Does.Contain("IllegalForcedCompletionWarningMessage"));
            Assert.That(text, Does.Contain("forceComplete && !handle.IsCompleted && !IsInsideSwapWindow()"));
            Assert.That(text, Does.Contain("WarnIllegalForcedCompletion();"));
            Assert.That(text, Does.Contain("Volatile.Read(ref _activeSwapWindowDepth) > 0"));
            Assert.That(text, Does.Contain("Interlocked.Exchange(ref _illegalForcedCompletionWarningLogged, 1)"));
        }

        [Test]
        public void VoxelAStarForcedCompletions_RunInsideDispatcherSwapWindow()
        {
            string text = File.ReadAllText(PathFunnelVoxelAStarPath());
            string teardown = ExtractMethodBody(text, "ForceCompleteVoxelAStarJobsForTeardown");
            string mockSdf = ExtractMethodBody(text, "EnsureVoxelAStarMockSdfCold");

            Assert.That(teardown, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.TryComplete(ref _voxelAStarEvaluateHandle, forceComplete: true)"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.TryComplete(ref _voxelAStarSmoothHandle, forceComplete: true)"));
            Assert.That(mockSdf, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(mockSdf, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(mockSdf, Does.Contain("DispatcherJobFence.TryComplete(ref handle, forceComplete: true)"));
        }

        [Test]
        public void GlobalDataVault_WriteLockAndPinPaths_RollBackWhenCompactionFenceRises()
        {
            string text = File.ReadAllText(GlobalDataVaultPath());

            Assert.That(text, Does.Contain("Volatile.Read(ref _compactionFence) != 0"));
            Assert.That(text, Does.Contain("RollbackWriterLockUnlocked("));
            Assert.That(text, Does.Contain("lockedBuffer = default;"));
            Assert.That(text, Does.Contain("RollbackBufferPinUnlocked("));
            Assert.That(text, Does.Contain("ReleaseMutationGuard(writeMask);"));
            Assert.That(text, Does.Contain("Volatile.Read(ref _mutationGuardMaskHigh) != 0"));
        }

        [Test]
        public void VisualPressureAgingFaultSnapshot_UsesTransientReadbackOnly()
        {
            string text = File.ReadAllText(VisualPressureAgingPath());
            string cursorReadback = ExtractMethodBody(text, "TryReadTelemetryCursor");
            string ringReadback = ExtractMethodBody(text, "TryCopyTelemetryEntries");

            Assert.AreEqual(0, Count(cursorReadback, @"\bTryLockBuffer\b|\bTryUnlockBuffer\b"), "telemetry cursor readback lock");
            Assert.AreEqual(0, Count(ringReadback, @"\bTryLockBuffer\b|\bTryUnlockBuffer\b"), "telemetry ring readback lock");
            Assert.That(cursorReadback, Does.Contain("TryResolveHandle"));
            Assert.That(ringReadback, Does.Contain("TryResolveHandle"));
        }

        [Test]
        public void AnalyticsWorkerVaultLifetime_UsesSingleMutationGuardNotBufferPins()
        {
            string text = File.ReadAllText(AnalyticsExporterPath());
            string lockBody = ExtractMethodBody(text, "LockWorkerVaultBuffers");
            string unlockBody = ExtractMethodBody(text, "UnlockWorkerVaultBuffers");

            Assert.That(text, Does.Contain("WorkerVaultMutationGuardMask"));
            Assert.That(lockBody, Does.Contain("TryAcquireMutationGuard(WorkerVaultMutationGuardMask)"));
            Assert.That(unlockBody, Does.Contain("ReleaseMutationGuard(WorkerVaultMutationGuardMask)"));
            Assert.AreEqual(0, Count(lockBody, @"\bTryLockBuffer\b|\bTryUnlockBuffer\b"), "analytics worker lock body buffer pins");
            Assert.AreEqual(0, Count(unlockBody, @"\bTryLockBuffer\b|\bTryUnlockBuffer\b"), "analytics worker unlock body buffer pins");
        }

        [Test]
        public void BulkheadWriters_UseMutationGuardsInsteadOfNestedWriteLocks()
        {
            string runtime = File.ReadAllText(BulkheadRuntimePath());
            string intentBus = File.ReadAllText(BulkheadIntentBusPath());
            string busAcquire = ExtractMethodBody(intentBus, "TryAcquireIntentWriteViews");
            string busRelease = ExtractMethodBody(intentBus, "ReleaseIntentWriteViews");
            string loadProfilesBytes = ExtractMethodBody(runtime, "TryLoadProfilesFromCsvBytes");
            string loadProfilesFile = ExtractMethodBody(runtime, "TryLoadProfilesFromCsvFile");
            string consumeIntents = ExtractMethodBody(runtime, "ConsumePublishedIntents");
            string recordTelemetry = ExtractMethodBody(runtime, "RecordLayoutFaultTelemetry");

            Assert.That(intentBus, Does.Contain("IntentMutationGuardMask"));
            Assert.That(busAcquire, Does.Contain("TryAcquireMutationGuard(IntentMutationGuardMask)"));
            Assert.That(busRelease, Does.Contain("ReleaseMutationGuard(IntentMutationGuardMask)"));
            Assert.AreEqual(0, Count(busAcquire, @"\b(?:TryAcquireWriteLane|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "intent bus nested write lock");
            Assert.AreEqual(0, Count(busRelease, @"\b(?:TryAcquireWriteLane|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "intent bus release write lock");

            Assert.That(runtime, Does.Contain("BulkheadProfileImportMutationGuardMask"));
            Assert.That(runtime, Does.Contain("BulkheadRefreshMutationGuardMask"));
            Assert.That(runtime, Does.Contain("BulkheadTelemetryMutationGuardMask"));
            Assert.That(loadProfilesBytes, Does.Contain("TryAcquireMutationGuard(BulkheadProfileImportMutationGuardMask)"));
            Assert.That(loadProfilesBytes, Does.Contain("ReleaseMutationGuard(BulkheadProfileImportMutationGuardMask)"));
            Assert.That(loadProfilesFile, Does.Contain("TryAcquireMutationGuard(BulkheadProfileImportMutationGuardMask)"));
            Assert.That(loadProfilesFile, Does.Contain("ReleaseMutationGuard(BulkheadProfileImportMutationGuardMask)"));
            Assert.That(consumeIntents, Does.Contain("TryAcquireMutationGuard(BulkheadContainmentIntentBus.IntentMutationGuardMask)"));
            Assert.That(recordTelemetry, Does.Contain("TryAcquireMutationGuard(BulkheadTelemetryMutationGuardMask)"));
            Assert.That(recordTelemetry, Does.Contain("ReleaseMutationGuard(BulkheadTelemetryMutationGuardMask)"));
            Assert.That(recordTelemetry, Does.Contain("TryResolveHandle"));
            Assert.AreEqual(0, Count(loadProfilesBytes, @"\b(?:TryAcquireWriteLane|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "profile byte import write lock");
            Assert.AreEqual(0, Count(loadProfilesFile, @"\b(?:TryAcquireWriteLane|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "profile import nested write lock");
            Assert.AreEqual(0, Count(consumeIntents, @"\b(?:TryAcquireWriteLane|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "consume intents nested write lock");
            Assert.AreEqual(0, Count(recordTelemetry, @"\b(?:TryAcquireWriteLane|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "layout fault telemetry nested write lock");
        }

        [Test]
        public void ConstructionPresentationAndTelemetryWriters_UseSingleMutationGuard()
        {
            string blueprint = File.ReadAllText(HectonBlueprintPreviewBatchPath());
            string pipe = File.ReadAllText(VRPipeBlueprintPreviewPath());
            string foundation = File.ReadAllText(FoundationSnappingCalculatorDataPath());
            string vehicleDock = File.ReadAllText(VehicleDockingModulePath());
            string bulkhead = File.ReadAllText(BulkheadRuntimePath());
            string hatch = File.ReadAllText(BulkheadHatchLocksPath());

            string blueprintAcquire = ExtractMethodBody(blueprint, "TryAcquirePreviewBuildWriteBuffers");
            string pipeAcquire = ExtractMethodBody(pipe, "TryAcquirePreviewWriteBuffers");
            string foundationAcquire = ExtractMethodBody(foundation, "TryBeginProfileEditLocks");
            string vehicleAcquire = ExtractMethodBody(vehicleDock, "TryAcquireDockTelemetryWrite");
            string bulkheadPins = ExtractMethodBody(bulkhead, "TryEnsureBulkheadJobPins");
            string bulkheadOptional = ExtractMethodBody(bulkhead, "TryLockOptionalBulkheadJobPin");
            string hatchDump = ExtractMethodBody(hatch, "DumpHatchBlackBoxIfRequested");

            Assert.That(blueprint, Does.Contain("PreviewBuildMutationGuardMask"));
            Assert.That(pipe, Does.Contain("PreviewBuildMutationGuardMask"));
            Assert.That(foundation, Does.Contain("ProfileEditMutationGuardMask"));
            Assert.That(vehicleDock, Does.Contain("DockTelemetryMutationGuardMask"));
            Assert.That(bulkhead, Does.Contain("BulkheadJobMutationGuardMask"));
            Assert.That(hatch, Does.Contain("HatchTelemetryDumpMutationGuardMask"));
            Assert.That(blueprintAcquire, Does.Contain("TryAcquireMutationGuard(PreviewBuildMutationGuardMask)"));
            Assert.That(pipeAcquire, Does.Contain("TryAcquireMutationGuard(PreviewBuildMutationGuardMask)"));
            Assert.That(foundationAcquire, Does.Contain("TryAcquireMutationGuard(ProfileEditMutationGuardMask)"));
            Assert.That(vehicleAcquire, Does.Contain("TryAcquireMutationGuard(DockTelemetryMutationGuardMask)"));
            Assert.That(bulkheadPins, Does.Contain("TryAcquireMutationGuard(BulkheadJobMutationGuardMask)"));
            Assert.That(bulkheadOptional, Does.Not.Contain("TryLockBuffer"));
            Assert.That(hatchDump, Does.Contain("TryAcquireMutationGuard(HatchTelemetryDumpMutationGuardMask)"));
            Assert.That(hatchDump, Does.Contain("ReleaseMutationGuard(HatchTelemetryDumpMutationGuardMask)"));
            Assert.AreEqual(0, Count(blueprintAcquire, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "blueprint preview nested locks");
            Assert.AreEqual(0, Count(pipeAcquire, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "VR pipe preview nested locks");
            Assert.AreEqual(0, Count(foundationAcquire, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "foundation profile nested locks");
            Assert.AreEqual(0, Count(vehicleAcquire, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "vehicle dock telemetry nested locks");
            Assert.AreEqual(0, Count(bulkheadPins, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "bulkhead job pin nested locks");
            Assert.AreEqual(0, Count(hatchDump, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "hatch dump nested locks");
        }

        [Test]
        public void GlobalShaderDispatcherLateFrame_UsesGuardedSnapshotsNotVaultPins()
        {
            string text = File.ReadAllText(GlobalShaderDispatcherPath());
            string lateFrame = ExtractMethodBody(text, "LateFrameTick");
            string thermalSnapshot = ExtractMethodBody(text, "BuildThermalPackedSnapshot");

            Assert.That(text, Does.Contain("ShaderGlobalStateMutationGuardMask"));
            Assert.That(text, Does.Contain("ThermalSourceReadGuardMask"));
            Assert.That(lateFrame, Does.Contain("stackalloc float4[ThermalAnomalyCapacity]"));
            Assert.That(lateFrame, Does.Contain("TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask)"));
            Assert.That(lateFrame, Does.Contain("ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask)"));
            Assert.That(thermalSnapshot, Does.Contain("TryAcquireMutationGuard(ThermalSourceReadGuardMask)"));
            Assert.That(thermalSnapshot, Does.Contain("TryReadOnlyHandle"));
            Assert.That(thermalSnapshot, Does.Contain("ReleaseMutationGuard(ThermalSourceReadGuardMask)"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "shader dispatcher Vault pins");
            Assert.AreEqual(0, Count(lateFrame, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "LateFrame Vault pins");
            Assert.AreEqual(0, Count(thermalSnapshot, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "thermal snapshot Vault pins");
        }

        [Test]
        public void ShinobuLogisticsRouter_UsesSingleRouteGuardInsteadOfBufferPins()
        {
            string text = File.ReadAllText(ShinobuLogisticsRouterPath());
            string acquire = ExtractMethodBody(text, "TryAcquireRouterMutationGuard");
            string release = ExtractMethodBody(text, "ReleaseRouterMutationGuard");
            string releaseIdle = ExtractMethodBody(text, "ReleaseRouterJobMutationGuardIfIdle");
            string forceComplete = ExtractMethodBody(text, "ForceCompletePendingRouterJobsInPostSimulationWindow");
            string localShiftSchedule = ExtractMethodBody(text, "TryScheduleLocalShiftJob");
            string lateFrame = ExtractMethodBodyFromDeclaration(text, "public void LateFrameTick(float now)", "ShinobuLogisticsRouter.LateFrameTick");

            Assert.That(text, Does.Contain("RouterMutationGuardMask"));
            Assert.That(text, Does.Contain("RouterBufferGuardBit"));
            Assert.That(text, Does.Contain("_routerJobMutationGuardHeld"));
            Assert.That(text, Does.Contain("_routerJobMutationGuardVault"));
            Assert.That(acquire, Does.Contain("guardVault = _dataVault"));
            Assert.That(acquire, Does.Contain("IDataVault vault = guardVault"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(RouterMutationGuardMask)"));
            Assert.That(release, Does.Contain("guardVault?.ReleaseMutationGuard(RouterMutationGuardMask)"));
            Assert.That(releaseIdle, Does.Contain("IDataVault guardVault = _routerJobMutationGuardVault"));
            Assert.That(releaseIdle, Does.Contain("_routerJobMutationGuardVault = null"));
            Assert.That(releaseIdle, Does.Contain("ReleaseRouterMutationGuard(guardVault)"));
            Assert.That(text, Does.Not.Contain("ReleaseRouterMutationGuard();"));
            Assert.That(forceComplete, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(forceComplete, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(forceComplete, Does.Contain("ReleaseRouterJobMutationGuardIfIdle()"));
            Assert.That(localShiftSchedule, Does.Contain("LocalShiftResolverJob"));
            Assert.That(localShiftSchedule, Does.Contain(".Schedule(_nodeCount, 64)"));
            Assert.That(text, Does.Not.Contain("TryLockRouterMutationBuffers"));
            Assert.That(text, Does.Not.Contain("UnlockRouterMutationBuffers"));
            Assert.That(text, Does.Not.Contain("TryLockRouterBuffer"));
            Assert.AreEqual(0, Count(lateFrame, @"\bSchedule(?:Parallel)?\s*\("), "logistics LateFrame job schedule");
            Assert.AreEqual(0, Count(lateFrame, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "logistics LateFrame Vault pins");
        }

        [Test]
        public void SubmarineThermalGrid_UsesRouteGuardsAndBracketedForcedCompletions()
        {
            string text = File.ReadAllText(SubmarineOsThermalGridRuntimePath());
            string acquire = ExtractMethodBody(text, "TryAcquireThermalGridMutationGuard");
            string release = ExtractMethodBody(text, "ReleaseThermalGridMutationGuard");
            string topologyRebuild = ExtractMethodBody(text, "TryLockTopologyRebuildBuffers");
            string topologyCommit = ExtractMethodBody(text, "TryLockTopologyCommitTargetBuffers");
            string solve = ExtractMethodBody(text, "TryLockSolveBuffers");
            string externalHeat = ExtractMethodBody(text, "TryLockExternalHeatBuffers");
            string completeSolve = ExtractMethodBody(text, "TryCompleteSolvePostSimulation");
            string commitTopology = ExtractMethodBody(text, "TryCommitTopologyRebuildPostSimulation");
            string completeExternal = ExtractMethodBody(text, "TryCompleteExternalThermalInjectionPostSimulation");
            string csvImport = ExtractMethodBody(text, "TryAcquireCsvImportViews");
            string csvRelease = ExtractMethodBody(text, "ReleaseCsvImportViews");
            string forceTopology = ExtractMethodBody(text, "ForceCompleteTopologyRebuildInPostSimulationWindow");
            string forceAll = ExtractMethodBody(text, "ForceCompletePendingJobsInPostSimulationWindow");

            Assert.That(text, Does.Contain("TopologyRebuildMutationGuardMask"));
            Assert.That(text, Does.Contain("TopologyCommitMutationGuardMask"));
            Assert.That(text, Does.Contain("SolveMutationGuardMask"));
            Assert.That(text, Does.Contain("ExternalHeatMutationGuardMask"));
            Assert.That(text, Does.Contain("CsvImportMutationGuardMask"));
            Assert.That(text, Does.Contain("ThermalGridBufferGuardBit"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(mutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(mutationGuardMask)"));
            Assert.That(topologyRebuild, Does.Contain("TopologyRebuildMutationGuardMask"));
            Assert.That(topologyCommit, Does.Contain("TopologyCommitMutationGuardMask"));
            Assert.That(solve, Does.Contain("SolveMutationGuardMask"));
            Assert.That(externalHeat, Does.Contain("ExternalHeatMutationGuardMask"));
            Assert.That(forceTopology, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(forceTopology, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(forceAll, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(forceAll, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(completeSolve, Does.Contain("finally"));
            Assert.That(completeSolve, Does.Contain("UnlockSolveBuffers();"));
            Assert.That(commitTopology, Does.Contain("finally"));
            Assert.That(commitTopology, Does.Contain("UnlockTopologyCommitTargetBuffers(commitLockedCount);"));
            Assert.That(commitTopology, Does.Contain("UnlockTopologyRebuildBuffers();"));
            Assert.That(completeExternal, Does.Contain("finally"));
            Assert.That(completeExternal, Does.Contain("UnlockExternalHeatBuffers();"));
            Assert.That(csvImport, Does.Contain("TryAcquireThermalGridMutationGuard(CsvImportMutationGuardMask"));
            Assert.That(csvRelease, Does.Contain("ReleaseThermalGridMutationGuard(CsvImportMutationGuardMask"));
            Assert.AreEqual(0, Count(topologyRebuild + topologyCommit + solve + externalHeat, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "thermal grid acquire route pins");
            Assert.AreEqual(0, Count(release, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "thermal grid release route pins");
            Assert.AreEqual(0, Count(csvImport + csvRelease, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "thermal grid CSV import nested locks");
        }

        [Test]
        public void SolarGenerationJob_UsesSingleMutationGuardForScheduledViews()
        {
            string text = File.ReadAllText(PowerGridSolarContractsPath());
            string schedule = ExtractMethodBody(text, "TrySchedule");
            string acquire = ExtractMethodBody(text, "TryAcquireJobMutationGuard");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string sdf = ExtractMethodBody(text, "TryAcquireVoxelSdfPayload");
            string finalize = ExtractMethodBody(text, "TryFinalize");
            string forceComplete = ExtractMethodBody(text, "ForceCompletePendingJobInPostSimulationWindow");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("s_jobMutationGuardHeld"));
            Assert.That(text, Does.Contain("SolarBufferGuardBit"));
            Assert.That(schedule, Does.Contain("TryAcquireJobMutationGuard()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(sdf, Does.Contain("TryReadOnlyHandle"));
            Assert.That(finalize, Does.Contain("finally"));
            Assert.That(finalize, Does.Contain("UnlockJobBuffers();"));
            Assert.That(forceComplete, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(forceComplete, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(text, Does.Not.Contain("TryLockJobBuffers"));
            Assert.AreEqual(0, Count(acquire + release + sdf, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "solar scheduled guard route pins");
        }

        [Test]
        public void ScavengingLootOracle_SchedulesInSimulationNotLateFrame()
        {
            string text = File.ReadAllText(ScavengingLootOraclePath());
            string lateFrame = ExtractMethodBody(text, "LateFrameTick");
            string schedule = ExtractMethodBody(text, "ScheduleSimulation");
            string postSimulation = ExtractMethodBody(text, "PostSimulationTick");
            string forcePending = ExtractMethodBody(text, "ForceCompletePendingPublishForLifecycle");
            string forceCold = ExtractMethodBody(text, "ForceCompleteColdJobInPostSimulationWindow");

            Assert.That(text, Does.Contain("SimulationPhaseSystem"));
            Assert.That(text, Does.Contain("PostSimulationPhaseSystem"));
            Assert.That(text, Does.Contain("TryRegisterDispatcherPhases();"));
            Assert.That(schedule, Does.Contain("LootResolutionJob"));
            Assert.That(schedule, Does.Contain("PublishLootYieldsJob"));
            Assert.That(schedule, Does.Contain("EnsureLootTableJob(dependsOn)"));
            Assert.That(postSimulation, Does.Contain("TryCompletePendingPublish(forceComplete: false)"));
            Assert.That(forcePending, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(forcePending, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(forceCold, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(forceCold, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.AreEqual(0, Count(lateFrame, @"\.Schedule(?:Parallel)?\s*\("), "scavenging LateFrame job schedule");
        }

        [Test]
        public void CrashTelemetryExport_UsesSingleRouteGuardsForSnapshotAndScratch()
        {
            string text = File.ReadAllText(CrashTelemetryBufferPath());
            string snapshot = ExtractMethodBody(text, "SnapshotRecentEntries");
            string scratch = ExtractMethodBody(text, "BuildExportScratch");

            Assert.That(text, Does.Contain("ExportSnapshotMutationGuardMask"));
            Assert.That(text, Does.Contain("ExportScratchMutationGuardMask"));
            Assert.That(snapshot, Does.Contain("TryAcquireMutationGuard(ExportSnapshotMutationGuardMask)"));
            Assert.That(snapshot, Does.Contain("ReleaseMutationGuard(ExportSnapshotMutationGuardMask)"));
            Assert.That(snapshot, Does.Contain("TryReadOnlyHandle"));
            Assert.That(snapshot, Does.Contain("TryResolveHandle"));
            Assert.That(scratch, Does.Contain("TryAcquireMutationGuard(ExportScratchMutationGuardMask)"));
            Assert.That(scratch, Does.Contain("ReleaseMutationGuard(ExportScratchMutationGuardMask)"));
            Assert.That(scratch, Does.Contain("TryReadOnlyHandle"));
            Assert.That(scratch, Does.Contain("TryResolveHandle"));
            Assert.AreEqual(0, Count(snapshot, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "crash export snapshot nested locks");
            Assert.AreEqual(0, Count(scratch, @"\b(?:TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "crash export scratch nested locks");
        }

        [Test]
        public void FoveatedForcedCompletions_RunInsideDispatcherSwapWindow()
        {
            string text = File.ReadAllText(FoveatedSimulationManagerPath());
            string helper = ExtractMethodBody(text, "ForceCompleteFrameJobsInPostSimulationWindow");
            string complete = ExtractMethodBodyFromDeclaration(text, "public void CompleteFrameJobs()", "CompleteFrameJobs");
            string reset = ExtractMethodBodyFromDeclaration(text, "public void ResetRuntimeState()", "ResetRuntimeState");
            string originShift = ExtractMethodBodyFromDeclaration(text, "public void OnOriginShift", "OnOriginShift");
            string rebind = ExtractMethodBodyFromDeclaration(text, "private void RebindDataVaultForOwnerRoute", "RebindDataVaultForOwnerRoute");

            Assert.That(text, Does.Contain("ForceCompleteFrameJobsInPostSimulationWindow"));
            Assert.That(helper, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(helper, Does.Contain("TryCompleteFrameJobsInternal(forceComplete: true)"));
            Assert.That(helper, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(complete, Does.Contain("ForceCompleteFrameJobsInPostSimulationWindow()"));
            Assert.That(reset, Does.Contain("ForceCompleteFrameJobsInPostSimulationWindow()"));
            Assert.That(originShift, Does.Contain("ForceCompleteFrameJobsInPostSimulationWindow()"));
            Assert.That(rebind, Does.Contain("ForceCompleteFrameJobsInPostSimulationWindow()"));
            Assert.AreEqual(1, Count(text, @"TryCompleteFrameJobsInternal\(forceComplete:\s*true\)"), "foveated forced completion route count");
        }

        [Test]
        public void CoreColdForcedCompletions_RunInsideDispatcherSwapWindow()
        {
            string telemetry = File.ReadAllText(GlobalTelemetryBusPath());
            string uiState = File.ReadAllText(UIStateStorePath());
            string h8Memory = File.ReadAllText(H8MemoryPath());
            string memorySentinel = File.ReadAllText(MemorySentinelPath());
            string foveated = File.ReadAllText(FoveatedSimulationManagerPath());
            string homeostasis = File.ReadAllText(HomeostasisBrainPath());
            string dispatcher = File.ReadAllText(SystemDispatcherPath());
            string bucketer = File.ReadAllText(ModuloSimulationBucketerPath());
            string babel = File.ReadAllText(BabelDictionaryStorePath());
            string lockstep = File.ReadAllText(LockstepStateValidatorPath());
            string signalWarden = File.ReadAllText(SignalWardenRuntimePath());

            string telemetryDispose = ExtractMethodBody(telemetry, "DisposeNativeArray");
            string telemetryHelper = ExtractMethodBody(telemetry, "ForceCompleteDisposeHandleInPostSimulationWindow");
            string uiShutdown = ExtractMethodBody(uiState, "Shutdown");
            string uiHelper = ExtractMethodBody(uiState, "ForceCompleteDisposeHandleInPostSimulationWindow");
            string h8OwnerComplete = ExtractMethodBody(h8Memory, "TryCompleteOwnerJobHandle");
            string sentinelHelper = ExtractMethodBody(memorySentinel, "ForceCompleteValidationJobInPostSimulationWindow");
            string sentinelComplete = ExtractMethodBody(memorySentinel, "CompleteValidationJob");
            string sentinelDisable = ExtractMethodBody(memorySentinel, "OnDisable");
            string sentinelRebind = ExtractMethodBody(memorySentinel, "RebindVaultDependencyCold");
            string sentinelCheat = ExtractMethodBody(memorySentinel, "SimulateCheatEngineWriteInternal");
            string foveatedTryComplete = ExtractMethodBody(foveated, "TryCompleteJob");
            string foveatedDispose = ExtractMethodBody(foveated, "DisposeNativeBuffers");
            string homeostasisHelper = ExtractMethodBody(homeostasis, "ForceCompleteMockTerrainSamplerJobInPostSimulationWindow");
            string dispatcherFixed = ExtractMethodBody(dispatcher, "CompleteMasterFixedSimulationBridge");
            string dispatcherProbeDispose = ExtractMethodBodyFromDeclaration(
                dispatcher,
                "private static void DisposeDispatcherSurfaceProbeBuffers(IDataVault dataVault)",
                "DisposeDispatcherSurfaceProbeBuffers(IDataVault)");
            string bucketerComplete = ExtractMethodBody(bucketer, "CompleteRebalanceHandle");
            string babelClose = ExtractMethodBody(babel, "CompleteActiveLoreReadsForClose");
            string lockstepHash = ExtractMethodBody(lockstep, "ExecuteHashJobs");
            string signalWardenMock = ExtractMethodBody(signalWarden, "RunMockContentionEditorBlocking");

            Assert.That(telemetryDispose, Does.Contain("ForceCompleteDisposeHandleInPostSimulationWindow(ref disposeHandle)"));
            Assert.That(telemetryHelper, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(telemetryHelper, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(uiShutdown, Does.Contain("ForceCompleteDisposeHandleInPostSimulationWindow(ref disposeHandle)"));
            Assert.That(uiHelper, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(uiHelper, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(h8OwnerComplete, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(h8OwnerComplete, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(sentinelHelper, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(sentinelHelper, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(sentinelComplete, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(sentinelComplete, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(sentinelDisable, Does.Contain("ForceCompleteValidationJobInPostSimulationWindow()"));
            Assert.That(sentinelRebind, Does.Contain("ForceCompleteValidationJobInPostSimulationWindow()"));
            Assert.That(sentinelCheat, Does.Contain("ForceCompleteValidationJobInPostSimulationWindow()"));
            Assert.That(foveatedTryComplete, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(foveatedTryComplete, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(foveatedDispose, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(foveatedDispose, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(homeostasisHelper, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(homeostasisHelper, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(dispatcherFixed, Does.Contain("DispatcherJobFence.BeginPostFixedSwapWindow()"));
            Assert.That(dispatcherFixed, Does.Contain("DispatcherJobFence.EndPostFixedSwapWindow()"));
            Assert.That(dispatcherProbeDispose, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(dispatcherProbeDispose, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(bucketerComplete, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(bucketerComplete, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(babelClose, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(babelClose, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(lockstepHash, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(lockstepHash, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(signalWardenMock, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(signalWardenMock, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.AreEqual(1, Count(memorySentinel, @"CompleteValidationJob\(forceComplete:\s*true\)"), "MemorySentinel direct forced completion route count");
        }

        [Test]
        public void ChemicalInfluenceGridSimulation_UsesSingleGuardAndBracketedTeardown()
        {
            string text = File.ReadAllText(ChemicalInfluenceGridPath());
            string schedule = ExtractMethodBody(text, "ScheduleSimulation");
            string acquire = ExtractMethodBody(text, "TryLockSimulationBuffers");
            string release = ExtractMethodBody(text, "UnlockSimulationBuffers");
            string complete = ExtractMethodBody(text, "CompleteScheduledWorkForTeardown");
            string finish = ExtractMethodBody(text, "FinishScheduledWorkCompletion");
            string initialize = ExtractMethodBody(text, "TryInitializeVaultBuffers");

            Assert.That(text, Does.Contain("SimulationMutationGuardMask"));
            Assert.That(schedule, Does.Contain("try"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("UnlockSimulationBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(SimulationMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(SimulationMutationGuardMask)"));
            Assert.That(complete, Does.Contain("DispatcherJobSwap.BeginPostSimulationSwapWindow()"));
            Assert.That(complete, Does.Contain("DispatcherJobSwap.EndPostSimulationSwapWindow()"));
            Assert.That(initialize, Does.Contain("DispatcherJobSwap.BeginPostSimulationSwapWindow()"));
            Assert.That(initialize, Does.Contain("DispatcherJobSwap.EndPostSimulationSwapWindow()"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.AreEqual(0, Count(acquire, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "chemical scheduled acquire legacy locks");
            Assert.AreEqual(0, Count(release, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "chemical scheduled release legacy locks");
        }

        [Test]
        public void VolcanicUpdraftFixedPipeline_UsesSingleGuardAndBracketedTeardown()
        {
            string text = File.ReadAllText(VolcanicUpdraftDirectorPath());
            string schedule = ExtractMethodBody(text, "ScheduleFixedSimulation");
            string force = ExtractMethodBody(text, "ForceCompleteFixedPipelineInPostFixedWindow");
            string acquire = ExtractMethodBody(text, "LockOwnBuffers");
            string release = ExtractMethodBody(text, "UnlockOwnBuffers");
            string player = ExtractMethodBody(text, "TryLockPlayerBuffer");
            string leviathan = ExtractMethodBody(text, "TryLockLeviathanBuffers");
            string external = ExtractMethodBody(text, "UnlockExternalBuffers");

            Assert.That(text, Does.Contain("FixedPipelineMutationGuardMask"));
            Assert.That(schedule, Does.Contain("try"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("UnlockOwnBuffers()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostFixedSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostFixedSwapWindow()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(FixedPipelineMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(FixedPipelineMutationGuardMask)"));
            Assert.AreEqual(0, Count(acquire, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "volcanic fixed acquire legacy locks");
            Assert.AreEqual(0, Count(release, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "volcanic fixed release legacy locks");
            Assert.AreEqual(0, Count(player + leviathan + external, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "volcanic external fixed legacy locks");
        }

        [Test]
        public void ShinobuPhysiologyJobRoute_UsesSingleGuardAndBracketedTeardown()
        {
            string text = File.ReadAllText(ShinobuPhysiologyRuntimePath());
            string schedule = ExtractMethodBody(text, "SchedulePhysiologyTick");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string teardown = ExtractMethodBody(text, "CompleteFrameJobForTeardown");
            string finish = ExtractMethodBody(text, "FinishFrameJobCompletion");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(schedule, Does.Contain("try"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("keepJobGuard"));
            Assert.That(schedule, Does.Contain("UnlockJobBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.AreEqual(0, Count(acquire, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockLockedJobBuffers)\b"), "physiology scheduled acquire legacy locks");
            Assert.AreEqual(0, Count(release, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockLockedJobBuffers)\b"), "physiology scheduled release legacy locks");
            Assert.That(text, Does.Not.Contain("UnlockLockedJobBuffers"));
        }

        [Test]
        public void ShinobuMetabolismRuntime_UsesSingleJobGuardAndReleasesBeforePresentation()
        {
            string text = File.ReadAllText(ShinobuMetabolismRuntimePath());
            string slowTick = ExtractMethodBody(text, "SlowTick");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string finish = ExtractMethodBody(text, "FinishFrameJobCompletion");
            string chemical = ExtractMethodBody(text, "TryResolveChemicalGrid");
            string teardown = ExtractMethodBody(text, "CompleteFrameJobForTeardown");
            string helper = ExtractMethodBody(text, "ForceCompleteInPostSimulationWindow");
            string bootstrap = ExtractMethodBody(text, "GenerateMockEcosystemMetabolism");
            string rulesOnly = ExtractMethodBody(text, "InitializeRulesAndTuningOnly");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("BootstrapMutationGuardMask"));
            Assert.That(text, Does.Contain("BiologicalProfileImportMutationGuardMask"));
            Assert.That(text, Does.Contain("SuitProfileImportMutationGuardMask"));
            Assert.That(text, Does.Contain("TuningMutationGuardMask"));
            Assert.That(text, Does.Contain("SuitProfileSelectionMutationGuardMask"));
            Assert.That(text, Does.Contain("ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask |"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(slowTick, Does.Contain("try"));
            Assert.That(slowTick, Does.Contain("finally"));
            Assert.That(slowTick, Does.Contain("UnlockJobBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(chemical, Does.Contain("!_jobLocksHeld"));
            Assert.That(helper, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(helper, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(teardown, Does.Contain("ForceCompleteInPostSimulationWindow(ref _activeJobHandle)"));
            Assert.That(bootstrap, Does.Contain("TryAcquireMutationGuard(BootstrapMutationGuardMask)"));
            Assert.That(bootstrap, Does.Contain("ReleaseMutationGuard(BootstrapMutationGuardMask)"));
            Assert.That(rulesOnly, Does.Contain("TryAcquireMutationGuard(BootstrapMutationGuardMask)"));
            Assert.That(rulesOnly, Does.Contain("ReleaseMutationGuard(BootstrapMutationGuardMask)"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.Less(finish.IndexOf("UnlockJobBuffers()", StringComparison.Ordinal), finish.IndexOf("PublishShaderGlobals", StringComparison.Ordinal));
            Assert.Less(finish.IndexOf("UnlockJobBuffers()", StringComparison.Ordinal), finish.IndexOf("PublishStagedSignals", StringComparison.Ordinal));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockLockedJobBuffers|UnlockLockedChemicalReadbackBuffers)\b"), "metabolism legacy DataVault lock tokens");
        }

        [Test]
        public void ShinobuSuitIntegrityJob_UsesSingleGuardAndPublishesAfterRelease()
        {
            string text = File.ReadAllText(ShinobuSuitIntegrityRuntimePath());
            string slowTick = ExtractMethodBody(text, "SlowTick");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string finish = ExtractMethodBody(text, "FinishFrameJobCompletion");
            string teardown = ExtractMethodBody(text, "CompleteFrameJobForTeardown");
            string force = ExtractMethodBody(text, "ForceCompleteFrameJobInPostSimulationWindow");
            string capture = ExtractMethodBody(text, "TryCaptureVisualSyncScalars");
            string publish = ExtractMethodBody(text, "PublishVisualSyncScalars");
            string dump = ExtractMethodBody(text, "TryDumpAutopsyIfFaulted");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("DumpMutationGuardMask"));
            Assert.That(text, Does.Contain("MutationGuardBit(ShinobuSuitIntegrityConstants.StateBuffer)"));
            Assert.That(text, Does.Contain("MutationGuardBit(ShinobuSuitIntegrityConstants.MockAupBuffer)"));
            Assert.That(text, Does.Contain("_jobGuardVault"));
            Assert.That(slowTick, Does.Contain("TryLockJobBuffers(vault)"));
            Assert.That(slowTick, Does.Contain("try"));
            Assert.That(slowTick, Does.Contain("finally"));
            Assert.That(slowTick, Does.Contain("UnlockJobBuffers()"));
            Assert.That(slowTick, Does.Contain("keepJobGuard = true"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(teardown, Does.Contain("ForceCompleteFrameJobInPostSimulationWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("finally"));
            Assert.That(finish, Does.Contain("TryCaptureVisualSyncScalars(out visualSyncPayload)"));
            Assert.That(finish, Does.Contain("UnlockJobBuffers()"));
            Assert.That(finish, Does.Contain("PublishVisualSyncScalars(visualSyncPayload)"));
            Assert.Less(finish.IndexOf("UnlockJobBuffers()", StringComparison.Ordinal), finish.IndexOf("PublishVisualSyncScalars(visualSyncPayload)", StringComparison.Ordinal));
            Assert.That(capture, Does.Contain("OpenVaultArray(ref _visualHandle"));
            Assert.That(publish, Does.Not.Contain("OpenVaultArray"));
            Assert.That(dump, Does.Contain("TryAcquireMutationGuard(DumpMutationGuardMask)"));
            Assert.That(dump, Does.Contain("ReleaseMutationGuard(DumpMutationGuardMask)"));
            Assert.That(dump, Does.Contain("finally"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockLockedJobBuffers|LockBufferCount)\b"), "suit integrity legacy DataVault lock tokens");
            Assert.That(text, Does.Not.Contain("_jobLocksHeld"));
        }

        [Test]
        public void ShinobuRadiationMutationRuntime_UsesRouteGuardsInsteadOfBufferPins()
        {
            string text = File.ReadAllText(ShinobuRadiationMutationRuntimePath());
            string evaluation = ExtractMethodBody(text, "RunEvaluation");
            string defaults = ExtractMethodBody(text, "InitializeDefaults");
            string metabolicBridge = ExtractMethodBody(text, "RunMetabolicBridge");
            string tuning = ExtractMethodBody(text, "SetEditorTuning");
            string mockDose = ExtractMethodBody(text, "InjectMockDose");
            string telemetry = ExtractMethodBody(text, "PatchLatestTelemetry");
            string csv = ExtractMethodBody(text, "TryLoadCsvProfilesCold");

            Assert.That(text, Does.Contain("EvaluationMutationGuardMask"));
            Assert.That(text, Does.Contain("DefaultInitializationMutationGuardMask"));
            Assert.That(text, Does.Contain("MetabolicBridgeMutationGuardMask"));
            Assert.That(text, Does.Contain("TelemetryMutationGuardMask"));
            Assert.That(text, Does.Contain("TuningMutationGuardMask"));
            Assert.That(text, Does.Contain("MockDoseMutationGuardMask"));
            Assert.That(text, Does.Contain("CsvImportMutationGuardMask"));
            Assert.That(text, Does.Contain("MutationGuardBit(ShinobuRadiationMutationConstants.MutationStateBuffer)"));
            Assert.That(text, Does.Contain("MutationGuardBit(ShinobuRadiationMutationConstants.MutationMockDoseBuffer)"));
            Assert.That(text, Does.Contain("MutationGuardBit(BufferID.ShinobuMetabolismStates)"));
            Assert.That(text, Does.Contain("ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask |"));
            Assert.That(evaluation, Does.Contain("TryAcquireMutationGuard(EvaluationMutationGuardMask)"));
            Assert.That(evaluation, Does.Contain("ReleaseMutationGuard(EvaluationMutationGuardMask)"));
            Assert.That(evaluation, Does.Contain("finally"));
            Assert.That(defaults, Does.Contain("TryAcquireMutationGuard(DefaultInitializationMutationGuardMask)"));
            Assert.That(defaults, Does.Contain("ReleaseMutationGuard(DefaultInitializationMutationGuardMask)"));
            Assert.That(defaults, Does.Contain("finally"));
            Assert.That(metabolicBridge, Does.Contain("TryAcquireMutationGuard(MetabolicBridgeMutationGuardMask)"));
            Assert.That(metabolicBridge, Does.Contain("ReleaseMutationGuard(MetabolicBridgeMutationGuardMask)"));
            Assert.That(metabolicBridge, Does.Contain("finally"));
            Assert.That(tuning, Does.Contain("TryAcquireMutationGuard(TuningMutationGuardMask)"));
            Assert.That(tuning, Does.Contain("ReleaseMutationGuard(TuningMutationGuardMask)"));
            Assert.That(tuning, Does.Contain("finally"));
            Assert.That(mockDose, Does.Contain("TryAcquireMutationGuard(MockDoseMutationGuardMask)"));
            Assert.That(mockDose, Does.Contain("ReleaseMutationGuard(MockDoseMutationGuardMask)"));
            Assert.That(mockDose, Does.Contain("finally"));
            Assert.That(telemetry, Does.Contain("TryAcquireMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(telemetry, Does.Contain("ReleaseMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(telemetry, Does.Contain("finally"));
            Assert.That(csv, Does.Contain("TryAcquireMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(csv, Does.Contain("ReleaseMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(csv, Does.Contain("finally"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "radiation mutation legacy DataVault lock tokens");
        }

        [Test]
        public void CombatDamageVaultLease_FailsClosedInsteadOfHoldingMultipleVaultGuards()
        {
            string text = File.ReadAllText(CombatDamageRuntimeVaultViewsPath());
            int structStart = text.IndexOf("private struct CombatVaultMutationGuardLease", StringComparison.Ordinal);
            int structEnd = text.IndexOf("private static ulong CombatVaultMutationGuardBit", structStart, StringComparison.Ordinal);
            Assert.GreaterOrEqual(structStart, 0, "Missing CombatVaultMutationGuardLease");
            Assert.Greater(structEnd, structStart, "Missing CombatVaultMutationGuardLease end marker");
            string lease = text.Substring(structStart, structEnd - structStart);
            string add = ExtractMethodBody(text, "Add");
            string acquire = ExtractMethodBody(text, "TryAcquire");
            string release = ExtractMethodBody(text, "Release");

            Assert.That(lease, Does.Not.Contain("_vault0"));
            Assert.That(lease, Does.Not.Contain("_vault1"));
            Assert.That(lease, Does.Not.Contain("_vault2"));
            Assert.That(lease, Does.Not.Contain("_mask0"));
            Assert.That(lease, Does.Not.Contain("_mask1"));
            Assert.That(lease, Does.Not.Contain("_mask2"));
            Assert.That(lease, Does.Not.Contain("_acquired0"));
            Assert.That(lease, Does.Not.Contain("_acquired1"));
            Assert.That(lease, Does.Not.Contain("_acquired2"));
            Assert.That(add, Does.Contain("object.ReferenceEquals(_vault, vault)"));
            Assert.That(add, Does.Contain("_mask |= mask;"));
            Assert.Less(add.IndexOf("object.ReferenceEquals(_vault, vault)", StringComparison.Ordinal), add.LastIndexOf("return false;", StringComparison.Ordinal));
            Assert.AreEqual(1, Count(acquire, @"TryAcquireMutationGuard\(_mask\)"), "lease acquire must touch one DataVault guard");
            Assert.AreEqual(1, Count(release, @"ReleaseMutationGuard\(mask\)"), "lease release must touch one DataVault guard");
        }

        [Test]
        public void LootMagnetScheduledJob_UsesSingleMutationGuardForVaultLifetime()
        {
            string text = File.ReadAllText(LootMagnetSystemPath());
            string fastTick = ExtractMethodBody(text, "FastTick");
            string schedule = ExtractMethodBody(text, "SchedulePull");
            string acquire = ExtractMethodBody(text, "TryAcquireScheduledVaultGuard");
            string release = ExtractMethodBody(text, "ReleaseScheduledVaultGuard");
            string lateFrame = ExtractMethodBody(text, "LateFrameTick");
            string barrier = ExtractMethodBody(text, "ForceCompleteAndCommitScheduledJobForBarrier");
            string telemetry = ExtractMethodBody(text, "RecordTelemetry");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("MutationGuardBit(BufferID.EntityAUPs)"));
            Assert.That(text, Does.Contain("MutationGuardBit(BufferID.EntityLootMagnetSignalEvents)"));
            Assert.That(text, Does.Contain("MutationGuardBit(BufferID.EntityLootMagnetTelemetry)"));
            Assert.That(text, Does.Contain("_scheduledVaultGuardVault"));
            Assert.That(fastTick, Does.Contain("SchedulePull(dt, playerAup);"));
            Assert.That(schedule, Does.Contain("TryAcquireScheduledVaultGuard()"));
            Assert.That(schedule, Does.Contain("TryResolveVaultViews(out LootMagnetVaultViews views, Capacity, allowAllocate: true)"));
            Assert.That(schedule, Does.Contain("try"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("ReleaseScheduledVaultGuard()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(lateFrame, Does.Contain("finally"));
            Assert.That(lateFrame, Does.Contain("ReleaseScheduledVaultGuard()"));
            Assert.That(barrier, Does.Contain("finally"));
            Assert.That(barrier, Does.Contain("ReleaseScheduledVaultGuard()"));
            Assert.That(telemetry, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(telemetry, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryLockBuffer|TryUnlockBuffer)\b"), "loot magnet scheduled buffer pins");
            Assert.That(text, Does.Not.Contain("_vaultBuffersLocked"));
            Assert.That(text, Does.Not.Contain("TryLockScheduledVaultBuffers"));
            Assert.That(text, Does.Not.Contain("UnlockScheduledVaultBuffers"));
        }

        [Test]
        public void DebrisManagerSimulationAndBurstFlush_UseSingleStateMutationGuard()
        {
            string text = File.ReadAllText(DebrisManagerPath());
            string tick = ExtractMethodBody(text, "Tick");
            string lateFrame = ExtractMethodBody(text, "LateFrameTick");
            string flush = ExtractMethodBody(text, "FlushPendingBursts");
            string acquire = ExtractMethodBody(text, "TryAcquireSimulationJobGuard");
            string release = ExtractMethodBody(text, "ReleaseSimulationJobGuard");
            string acquireFlush = ExtractMethodBody(text, "TryAcquireStateMutationGuard");
            string teardown = ExtractMethodBody(text, "ReleaseNativeState");
            string force = ExtractMethodBody(text, "ForceCompleteSimulationInPostSimulationWindow");

            Assert.That(text, Does.Contain("StateMutationGuardMask"));
            Assert.That(text, Does.Contain("MutationGuardBit(FrontStatesBufferId)"));
            Assert.That(text, Does.Contain("MutationGuardBit(BackStatesBufferId)"));
            Assert.That(text, Does.Contain("_simulationJobGuardVault"));
            Assert.That(tick, Does.Contain("TryAcquireSimulationJobGuard()"));
            Assert.That(tick, Does.Contain("TryOpenVaultBuffer(_simulationJobGuardVault"));
            Assert.That(tick, Does.Contain("ReleaseSimulationJobGuard()"));
            Assert.That(lateFrame, Does.Contain("ReleaseSimulationJobGuard()"));
            Assert.That(flush, Does.Contain("TryAcquireStateMutationGuard(out IDataVault guardVault)"));
            Assert.That(flush, Does.Contain("TryOpenVaultBuffer(guardVault"));
            Assert.That(flush, Does.Contain("finally"));
            Assert.That(flush, Does.Contain("guardVault.ReleaseMutationGuard(StateMutationGuardMask)"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(StateMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(StateMutationGuardMask)"));
            Assert.That(acquireFlush, Does.Contain("TryAcquireMutationGuard(StateMutationGuardMask)"));
            Assert.That(teardown, Does.Contain("ForceCompleteSimulationInPostSimulationWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("finally"));
            Assert.AreEqual(0, Count(acquire + release + flush, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "debris state guard methods must not reacquire DataVault write locks");
            Assert.That(text, Does.Not.Contain("TryLockSimulationJobBuffers"));
            Assert.That(text, Does.Not.Contain("UnlockSimulationJobBuffers"));
            Assert.That(text, Does.Not.Contain("_simulationJobBuffersLocked"));
        }

        [Test]
        public void PlayerHandIkJob_UsesSingleMutationGuardAndBracketedTeardown()
        {
            string text = File.ReadAllText(PlayerKinematicsRuntimeHandIkPath());
            string schedule = ExtractMethodBody(text, "ScheduleHandFabrikIk");
            string acquire = ExtractMethodBody(text, "TryAcquireHandIkJobGuard");
            string release = ExtractMethodBody(text, "ReleaseHandIkJobGuard");
            string publish = ExtractMethodBody(text, "PublishHandIkStatesForAnimation");
            string teardown = ExtractMethodBody(text, "CompleteHandFabrikIkForTeardown");
            string force = ExtractMethodBody(text, "ForceCompleteHandFabrikIkInPostSimulationWindow");

            Assert.That(text, Does.Contain("HandIkJobMutationGuardMask"));
            Assert.That(text, Does.Contain("MutationGuardBit(HandIkStatesBuffer)"));
            Assert.That(text, Does.Contain("MutationGuardBit(HandIkPublishedStatesBuffer)"));
            Assert.That(text, Does.Contain("MutationGuardBit(HandIkConfigBuffer)"));
            Assert.That(text, Does.Contain("_handIkJobGuardVault"));
            Assert.That(schedule, Does.Contain("TryAcquireHandIkJobGuard()"));
            Assert.That(schedule, Does.Contain("try"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("ReleaseHandIkJobGuard()"));
            Assert.Less(schedule.IndexOf("TryAcquireHandIkJobGuard()", StringComparison.Ordinal), schedule.IndexOf("TryResolveHandIkViews", StringComparison.Ordinal));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(HandIkJobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(HandIkJobMutationGuardMask)"));
            Assert.That(publish, Does.Contain("_handIkJobGuardHeld"));
            Assert.AreEqual(0, Count(publish, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "hand IK published-state copy must run under the job guard only");
            Assert.That(teardown, Does.Contain("ForceCompleteHandFabrikIkInPostSimulationWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("finally"));
            Assert.AreEqual(0, Count(schedule + acquire + release + publish, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "hand IK job route legacy lock tokens");
            Assert.That(text, Does.Not.Contain("_handIkLockedBuffers"));
            Assert.That(text, Does.Not.Contain("TryLockHandIkJobBuffers"));
            Assert.That(text, Does.Not.Contain("TryLockHandIkRequired"));
            Assert.That(text, Does.Not.Contain("UnlockHandIkBuffer"));
        }

        [Test]
        public void HazardZoneExposureJob_UsesSingleMutationGuardAndBracketedTeardown()
        {
            string text = File.ReadAllText(HazardZoneManagerPath());
            string schedule = ExtractMethodBody(text, "ScheduleExposureJob");
            string prepareResult = ExtractMethodBody(text, "TryPrepareHazardExposureResultBuffer");
            string acquire = ExtractMethodBody(text, "TryAcquireExposureJobGuard");
            string release = ExtractMethodBody(text, "ReleaseExposureJobLocks");
            string teardown = ExtractMethodBody(text, "DisposeNativeState");
            string force = ExtractMethodBody(text, "ForceCompleteExposureJobInPostSimulationWindow");

            Assert.That(text, Does.Contain("ExposureJobMutationGuardMask"));
            Assert.That(text, Does.Contain("BufferID.HazardZoneJobVolumes"));
            Assert.That(text, Does.Contain("BufferID.HazardZoneCurveLutSamples"));
            Assert.That(text, Does.Contain("BufferID.HazardExposureJobResult"));
            Assert.That(text, Does.Contain("_exposureJobGuardVault"));
            Assert.That(schedule, Does.Contain("TryAcquireExposureJobGuard()"));
            Assert.That(schedule, Does.Contain("try"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("ReleaseExposureJobLocks()"));
            Assert.That(schedule, Does.Contain("_jobVolumes.TryResolveMutable(out NativeArray<HazardVolumeData> lockedJobVolumes)"));
            Assert.That(schedule, Does.Contain("_jobVolumes.TryReadOnly"));
            Assert.That(schedule, Does.Contain("_volumeCurveLutSamples.TryReadOnly"));
            Assert.That(schedule, Does.Contain("TryPrepareHazardExposureResultBuffer"));
            Assert.That(prepareResult, Does.Contain("_exposureJobGuardHeld"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(ExposureJobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(ExposureJobMutationGuardMask)"));
            Assert.That(teardown, Does.Contain("ForceCompleteExposureJobInPostSimulationWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("finally"));
            Assert.AreEqual(0, Count(text, @"\b(?:PinReadOnlyAlias|ReleasePinnedReadOnlyAlias|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "hazard exposure legacy DataVault lock tokens");
            Assert.That(text, Does.Not.Contain("_jobBuffersLocked"));
            Assert.That(text, Does.Not.Contain("_jobResultLocked"));
        }

        [Test]
        public void MacroEcosystemFrostJob_UsesSingleMutationGuardAndBracketedTeardown()
        {
            string text = File.ReadAllText(MacroEcosystemMathematicianRuntimePath());
            string frostTick = ExtractMethodBody(text, "FrostTick");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string finish = ExtractMethodBody(text, "FinishCompletedScheduledJob");
            string barrier = ExtractMethodBody(text, "CompleteScheduledJobForTeardownOrVaultSwapBarrierBlocking");
            string force = ExtractMethodBody(text, "ForceCompleteScheduledJobInPostSimulationWindow");
            string bootstrap = ExtractMethodBody(text, "GenerateEmergencyMockEcosystem");
            string bootstrapForce = ExtractMethodBody(text, "ForceCompleteColdBootstrapInPostSimulationWindow");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("MutationGuardBit(BufferID.ShinobuMacroEcosystemSectorFront)"));
            Assert.That(text, Does.Contain("MutationGuardBit(BufferID.ShinobuMacroEcosystemTelemetryRing)"));
            Assert.That(text, Does.Contain("_jobGuardVault"));
            Assert.That(frostTick, Does.Contain("TryLockJobBuffers(vault)"));
            Assert.That(frostTick, Does.Contain("try"));
            Assert.That(frostTick, Does.Contain("finally"));
            Assert.That(frostTick, Does.Contain("UnlockJobBuffers()"));
            Assert.That(frostTick, Does.Contain("keepJobGuard = true"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.That(finish, Does.Contain("UnlockJobBuffers()"));
            Assert.That(barrier, Does.Contain("ForceCompleteScheduledJobInPostSimulationWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("finally"));
            Assert.That(bootstrap, Does.Contain("ForceCompleteColdBootstrapInPostSimulationWindow(ref indexHandle)"));
            Assert.That(bootstrapForce, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(bootstrapForce, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(bootstrapForce, Does.Contain("finally"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock|UnlockLockedJobBuffers)\b"), "macro ecosystem legacy DataVault lock tokens");
            Assert.That(text, Does.Not.Contain("_jobLocksHeld"));
        }

        [Test]
        public void MigrationDirectorFieldJob_UsesSingleGuardAndBracketedForcedCompletion()
        {
            string text = File.ReadAllText(MigrationDirectorPath());
            string schedule = ExtractMethodBody(text, "ScheduleMigrationFieldBuild");
            string complete = ExtractMethodBody(text, "CompleteMigrationFieldJob");
            string completeHandle = ExtractMethodBody(text, "TryCompleteMigrationFieldHandle");
            string acquire = ExtractMethodBody(text, "TryLockMigrationFieldJobBuffers");
            string release = ExtractMethodBody(text, "UnlockMigrationFieldJobBuffers");

            Assert.That(text, Does.Contain("_migrationFieldGuardMask"));
            Assert.That(text, Does.Contain("_migrationFieldGuardVault"));
            Assert.That(text, Does.Contain("MigrationFieldGuardBit(writeBufferId) | MigrationFieldGuardBit(poiBufferId)"));
            Assert.That(schedule, Does.Contain("TryLockMigrationFieldJobBuffers()"));
            Assert.That(complete, Does.Contain("TryCompleteMigrationFieldHandle(forceComplete)"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(guardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(guardMask)"));
            Assert.That(completeHandle, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(completeHandle, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(completeHandle, Does.Contain("finally"));
            Assert.AreEqual(0, Count(acquire + release, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "migration field job legacy DataVault lock tokens");
        }

        [Test]
        public void NutrientDriftJobRoutes_UseCombinedGuardsAndBracketedCompletion()
        {
            string text = File.ReadAllText(NutrientDriftRuntimePath());
            string carrion = File.ReadAllText(NutrientDriftCarrionPath());
            string frost = ExtractMethodBody(text, "FrostTick");
            string ensure = ExtractMethodBody(text, "EnsureVaultState");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string teardown = ExtractMethodBody(text, "CompleteScheduledJobForTeardown");
            string vaultSwap = ExtractMethodBody(text, "CompleteScheduledJobForVaultSwapBarrier");
            string finish = ExtractMethodBody(text, "FinishCompletedScheduledJob");
            string carrionAcquire = ExtractMethodBody(carrion, "TryLockCarrionJobBuffers");
            string carrionRelease = ExtractMethodBody(carrion, "UnlockCarrionJobBuffers");
            string carrionEnsure = ExtractMethodBody(carrion, "EnsureCarrionVaultState");

            Assert.That(text, Does.Contain("NutrientJobMutationGuardMask"));
            Assert.That(text, Does.Contain("CarrionJobMutationGuardMask"));
            Assert.That(text, Does.Contain("CombinedJobMutationGuardMask"));
            Assert.That(text, Does.Contain("InitializationMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(frost, Does.Contain("keepJobGuard"));
            Assert.That(frost, Does.Contain("finally"));
            Assert.That(frost, Does.Contain("UnlockJobBuffers()"));
            Assert.That(frost, Does.Not.Contain("TryLockCarrionJobBuffers"));
            Assert.That(ensure, Does.Contain("TryAcquireMutationGuard(InitializationMutationGuardMask)"));
            Assert.That(ensure, Does.Contain("ReleaseMutationGuard(InitializationMutationGuardMask)"));
            Assert.That(ensure, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(CombinedJobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(CombinedJobMutationGuardMask)"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(vaultSwap, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(vaultSwap, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.That(carrionAcquire, Does.Contain("_jobLocksHeld"));
            Assert.That(carrionAcquire, Does.Contain("TryAcquireMutationGuard(CarrionJobMutationGuardMask)"));
            Assert.That(carrionRelease, Does.Contain("ReleaseMutationGuard(CarrionJobMutationGuardMask)"));
            Assert.That(carrionEnsure, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.AreEqual(0, Count(acquire, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockLockedJobBuffers)\b"), "nutrient job acquire legacy locks");
            Assert.AreEqual(0, Count(release, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockLockedJobBuffers)\b"), "nutrient job release legacy locks");
            Assert.AreEqual(0, Count(carrionAcquire, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockCarrionLockedJobBuffers)\b"), "carrion job acquire legacy locks");
            Assert.AreEqual(0, Count(carrionRelease, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockCarrionLockedJobBuffers)\b"), "carrion job release legacy locks");
            Assert.That(text, Does.Not.Contain("UnlockLockedJobBuffers"));
            Assert.That(carrion, Does.Not.Contain("UnlockCarrionLockedJobBuffers"));
        }

        [Test]
        public void RespawnReconciliation_UsesSingleGuardsAndBracketedTeardown()
        {
            string text = File.ReadAllText(ShinobuRespawnReconciliationRuntimePath());
            string schedule = ExtractMethodBody(text, "ScheduleSimulation");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string rejectedTelemetry = ExtractMethodBody(text, "TryWriteRejectedDeathTelemetry");
            string droppedTelemetry = ExtractMethodBody(text, "TryWriteDroppedItemTelemetry");
            string teardown = ExtractMethodBody(text, "CompleteActiveJobForTeardown");
            string finish = ExtractMethodBody(text, "TryFinalizeActiveJobNoWait");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("TelemetryMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(schedule, Does.Contain("keepJobGuard"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("UnlockJobBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(rejectedTelemetry, Does.Contain("TryAcquireMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(rejectedTelemetry, Does.Contain("ReleaseMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(droppedTelemetry, Does.Contain("TryAcquireMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(droppedTelemetry, Does.Contain("ReleaseMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(teardown, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(teardown, Does.Contain("finally"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockLockedJobBuffers|TryLockJobBuffer)\b"), "respawn reconciliation legacy locks");
        }

        [Test]
        public void SensoryImpairment_UsesRouteGuardsInsteadOfBufferPins()
        {
            string text = File.ReadAllText(ShinobuSensoryImpairmentRuntimePath());
            string defaults = ExtractMethodBody(text, "InitializeDefaults");
            string evaluation = ExtractMethodBody(text, "RunEvaluation");
            string input = ExtractMethodBody(text, "RunInputCorruption");
            string telemetry = ExtractMethodBody(text, "PatchLatestTelemetryGas");
#if UNITY_EDITOR
            string csv = ExtractMethodBody(text, "TryLoadCsvProfilesCold");
#endif

            Assert.That(text, Does.Contain("DefaultsMutationGuardMask"));
            Assert.That(text, Does.Contain("EvaluationMutationGuardMask"));
            Assert.That(text, Does.Contain("InputMutationGuardMask"));
            Assert.That(text, Does.Contain("TelemetryMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(defaults, Does.Contain("TryAcquireMutationGuard(DefaultsMutationGuardMask)"));
            Assert.That(defaults, Does.Contain("ReleaseMutationGuard(DefaultsMutationGuardMask)"));
            Assert.That(evaluation, Does.Contain("TryAcquireMutationGuard(EvaluationMutationGuardMask)"));
            Assert.That(evaluation, Does.Contain("ReleaseMutationGuard(EvaluationMutationGuardMask)"));
            Assert.That(input, Does.Contain("TryAcquireMutationGuard(InputMutationGuardMask)"));
            Assert.That(input, Does.Contain("ReleaseMutationGuard(InputMutationGuardMask)"));
            Assert.That(telemetry, Does.Contain("TryAcquireMutationGuard(TelemetryMutationGuardMask)"));
            Assert.That(telemetry, Does.Contain("ReleaseMutationGuard(TelemetryMutationGuardMask)"));
#if UNITY_EDITOR
            Assert.That(csv, Does.Contain("TryAcquireMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(csv, Does.Contain("ReleaseMutationGuard(CsvImportMutationGuardMask)"));
#endif
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "sensory impairment legacy locks");
        }

        [Test]
        public void VehicleDamageRuntime_UsesRouteGuardsAndPostFixedForcedCompletions()
        {
            string text = File.ReadAllText(VehicleComponentDamageRuntimePath());
            string fixedTick = ExtractMethodBody(text, "FixedTick");
            string acquire = ExtractMethodBody(text, "LockDamageBuffers");
            string release = ExtractMethodBody(text, "UnlockDamageBuffers");
            string force = ExtractMethodBody(text, "ForceCompleteDamageInPostFixedWindow");
            string initialize = ExtractMethodBody(text, "InitializeGridBuffers");
            string csv = ExtractMethodBody(text, "TryLoadCsvLayout");
            string dump = ExtractMethodBody(text, "DumpBlackBoxIfFaulted");
            string tuning = ExtractMethodBody(text, "TryWriteEditorTuning");
            string pose = ExtractMethodBody(text, "TryRefreshRootPoseSnapshot");

            Assert.That(text, Does.Contain("DamageMutationGuardMask"));
            Assert.That(text, Does.Contain("CsvImportMutationGuardMask"));
            Assert.That(text, Does.Contain("BlackboxReadMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(fixedTick, Does.Contain("keepDamageGuard"));
            Assert.That(fixedTick, Does.Contain("finally"));
            Assert.That(fixedTick, Does.Contain("UnlockDamageBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(DamageMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(DamageMutationGuardMask)"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostFixedSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostFixedSwapWindow()"));
            Assert.That(initialize, Does.Contain("DispatcherJobFence.BeginPostFixedSwapWindow()"));
            Assert.That(initialize, Does.Contain("DispatcherJobFence.EndPostFixedSwapWindow()"));
            Assert.That(csv, Does.Contain("TryAcquireMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(csv, Does.Contain("ReleaseMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(dump, Does.Contain("TryAcquireMutationGuard(BlackboxReadMutationGuardMask)"));
            Assert.That(dump, Does.Contain("ReleaseMutationGuard(BlackboxReadMutationGuardMask)"));
            Assert.That(tuning, Does.Contain("TryAcquireMutationGuard(EditorTuningMutationGuardMask)"));
            Assert.That(pose, Does.Contain("TryReadOnlyHandle"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "vehicle damage legacy locks");
        }

        [Test]
        public void AuxiliaryEquipmentRuntime_UsesRouteGuardAndBracketedTeardown()
        {
            string text = File.ReadAllText(AuxiliaryEquipmentRouterRuntimePath());
            string tick = ExtractMethodBody(text, "Tick");
            string acquire = ExtractMethodBody(text, "TryLockRuntimeBuffers");
            string release = ExtractMethodBody(text, "UnlockRuntimeBuffers");
            string tuningAcquire = ExtractMethodBody(text, "TryLockTuningBuffer");
            string tuningRelease = ExtractMethodBody(text, "UnlockTuningBuffer");
            string releaseOwned = ExtractMethodBody(text, "ReleaseOwnedVaultHandles");
            string force = ExtractMethodBody(text, "ForceCompletePendingJobInPostSimulationWindow");
            string finish = ExtractMethodBody(text, "FinalizeCompletedPendingJob");

            Assert.That(text, Does.Contain("RuntimeMutationGuardMask"));
            Assert.That(text, Does.Contain("_runtimeGuardVault"));
            Assert.That(text, Does.Contain("TuningMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(tick, Does.Contain("keepRuntimeGuard"));
            Assert.That(tick, Does.Contain("finally"));
            Assert.That(tick, Does.Contain("UnlockRuntimeBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(RuntimeMutationGuardMask)"));
            Assert.That(acquire, Does.Contain("_runtimeGuardVault = vault"));
            Assert.That(release, Does.Contain("IDataVault vault = _runtimeGuardVault"));
            Assert.That(release, Does.Contain("_runtimeGuardVault = null"));
            Assert.That(release, Does.Contain("vault?.ReleaseMutationGuard(RuntimeMutationGuardMask)"));
            Assert.That(release, Does.Not.Contain("IDataVault vault = _dataVault"));
            Assert.That(releaseOwned, Does.Contain("UnlockRuntimeBuffers();"));
            Assert.That(tuningAcquire, Does.Contain("TryAcquireMutationGuard(TuningMutationGuardMask)"));
            Assert.That(tuningRelease, Does.Contain("ReleaseMutationGuard(TuningMutationGuardMask)"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.That(finish, Does.Contain("UnlockRuntimeBuffers()"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "auxiliary equipment legacy locks");
        }

        [Test]
        public void SeedShipAnomalyRuntime_ReleasesJobGuardBeforePresentation()
        {
            string text = File.ReadAllText(SeedShipAnomalyRuntimePath());
            string shader = File.ReadAllText(SeedShipAnomalyShaderBridgePath());
            string tick = ExtractMethodBody(text, "Tick");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string force = ExtractMethodBody(text, "ForceCompleteFrameJobInPostSimulationWindow");
            string finish = ExtractMethodBody(text, "FinishFrameJobCompletion");
            string dump = ExtractMethodBody(text, "TryDumpTelemetry");
            string csv = ExtractMethodBody(text, "MonitorCsvOverrides");
            string parseCsv = ExtractMethodBody(text, "ParseCsvOverrides");
            string shaderPublish = ExtractMethodBody(shader, "Publish");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("DumpMutationGuardMask"));
            Assert.That(text, Does.Contain("CsvImportMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(tick, Does.Contain("keepJobGuard"));
            Assert.That(tick, Does.Contain("finally"));
            Assert.That(tick, Does.Contain("UnlockJobBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.That(finish, Does.Contain("finally"));
            Assert.That(finish, Does.Contain("UnlockJobBuffers()"));
            Assert.Less(finish.IndexOf("UnlockJobBuffers()", StringComparison.Ordinal), finish.IndexOf("PublishLateFrameSignals", StringComparison.Ordinal));
            Assert.That(dump, Does.Contain("TryAcquireMutationGuard(DumpMutationGuardMask)"));
            Assert.That(dump, Does.Contain("ReleaseMutationGuard(DumpMutationGuardMask)"));
            Assert.That(csv, Does.Contain("TryAcquireMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(csv, Does.Contain("ReleaseMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(parseCsv, Does.Not.Contain("TryAcquireMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(shader, Does.Contain("ShaderGlobalStateMutationGuardMask"));
            Assert.That(shaderPublish, Does.Contain("TryAcquireMutationGuard(ShaderGlobalStateMutationGuardMask)"));
            Assert.That(shaderPublish, Does.Contain("ReleaseMutationGuard(ShaderGlobalStateMutationGuardMask)"));
            Assert.AreEqual(0, Count(text + shader, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockPartial)\b"), "seed ship anomaly legacy locks");
        }

        [Test]
        public void AbyssalShadowCulling_UsesSingleJobGuardAndBracketedBarrier()
        {
            string text = File.ReadAllText(AbyssalShadowCullingRuntimePath());
            string schedule = ExtractMethodBody(text, "ScheduleCullingPass");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBodyFromDeclaration(text, "private void UnlockJobBuffers(IDataVault vault, int lockedCount)", "UnlockJobBuffers overload");
            string force = ExtractMethodBody(text, "ForceCompleteCullingJobInPostSimulationWindow");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(schedule, Does.Contain("keepJobGuard"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("UnlockJobBuffers(vault, lockedCount)"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer|UnlockPartial)\b"), "abyssal shadow culling legacy locks");
        }

        [Test]
        public void DynamicPointLightCulling_UsesRouteGuardsAndBracketedForcedCompletions()
        {
            string text = File.ReadAllText(DynamicPointLightCullingDirectorPath());
            string schedule = ExtractMethodBody(text, "ScheduleCullingPipeline");
            string acquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string release = ExtractMethodBody(text, "UnlockJobBuffers");
            string mockSeed = ExtractMethodBody(text, "TryLockMockSeedBuffers");
            string mockSdf = ExtractMethodBody(text, "TryLockMockSdfBuffer");
            string manifest = ExtractMethodBody(text, "TryLockSourceManifestBuffer");
            string force = ExtractMethodBody(text, "ForceCompleteJobInPostSimulationWindow");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("MockSeedMutationGuardMask"));
            Assert.That(text, Does.Contain("MockSdfMutationGuardMask"));
            Assert.That(text, Does.Contain("SourceManifestMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(schedule, Does.Contain("keepJobGuard"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("UnlockJobBuffers()"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(JobMutationGuardMask)"));
            Assert.That(mockSeed, Does.Contain("TryAcquireMutationGuard(MockSeedMutationGuardMask)"));
            Assert.That(mockSdf, Does.Contain("TryAcquireMutationGuard(MockSdfMutationGuardMask)"));
            Assert.That(manifest, Does.Contain("TryAcquireMutationGuard(SourceManifestMutationGuardMask)"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryAcquireWriteLock|TryLockBuffer|ReleaseWriteLock|TryUnlockBuffer)\b"), "dynamic point light culling legacy locks");
        }

        [Test]
        public void BatteryChargerLogistics_UsesRouteGuardsAndBracketedTeardown()
        {
            string text = File.ReadAllText(BatteryChargerLogisticsRuntimePath());
            string schedule = ExtractMethodBody(text, "ScheduleSimulation");
            string post = ExtractMethodBody(text, "PostSimulationTick");
            string jobAcquire = ExtractMethodBody(text, "TryLockJobBuffers");
            string jobRelease = ExtractMethodBody(text, "UnlockJobBuffers");
            string mockSchedule = ExtractMethodBody(text, "ScheduleEmergencyMockNetwork");
            string mockAcquire = ExtractMethodBody(text, "TryLockMockBuffers");
            string mockRelease = ExtractMethodBody(text, "UnlockMockBuffers");
            string linkAcquire = ExtractMethodBody(text, "TryLockLinkMutationBuffers");
            string linkRelease = ExtractMethodBody(text, "UnlockLinkMutationBuffers");
            string tuning = ExtractMethodBody(text, "ApplyTuning");
            string csv = ExtractMethodBody(text, "MonitorProfileCsv");
            string force = ExtractMethodBody(text, "ForceCompleteInPostSimulationWindow");

            Assert.That(text, Does.Contain("JobMutationGuardMask"));
            Assert.That(text, Does.Contain("MockGenerationMutationGuardMask"));
            Assert.That(text, Does.Contain("LinkMutationGuardMask"));
            Assert.That(text, Does.Contain("TuningMutationGuardMask"));
            Assert.That(text, Does.Contain("CsvImportMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(schedule, Does.Contain("keepJobGuard"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("UnlockJobBuffers()"));
            Assert.That(post, Does.Contain("finally"));
            Assert.That(post, Does.Contain("UnlockJobBuffers()"));
            Assert.Less(post.IndexOf("UnlockJobBuffers()", StringComparison.Ordinal), post.IndexOf("EmitHumSignal", StringComparison.Ordinal));
            Assert.That(jobAcquire, Does.Contain("TryAcquireMutationGuard(JobMutationGuardMask)"));
            Assert.That(jobRelease, Does.Contain("ReleaseMutationGuard(lockedMask)"));
            Assert.That(mockSchedule, Does.Contain("keepMockGuard"));
            Assert.That(mockSchedule, Does.Contain("finally"));
            Assert.That(mockAcquire, Does.Contain("TryAcquireMutationGuard(MockGenerationMutationGuardMask)"));
            Assert.That(mockRelease, Does.Contain("ReleaseMutationGuard(lockedMask)"));
            Assert.That(linkAcquire, Does.Contain("TryAcquireMutationGuard(lockMask)"));
            Assert.That(linkRelease, Does.Contain("ReleaseMutationGuard(lockMask)"));
            Assert.That(tuning, Does.Contain("TryAcquireMutationGuard(TuningMutationGuardMask)"));
            Assert.That(tuning, Does.Contain("ReleaseMutationGuard(TuningMutationGuardMask)"));
            Assert.That(csv, Does.Contain("TryAcquireMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(csv, Does.Contain("ReleaseMutationGuard(CsvImportMutationGuardMask)"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryLockBuffer|TryUnlockBuffer)\b"), "battery charger logistics buffer pins");
        }

        [Test]
        public void WfcOutpostPowerBoot_UsesRouteGuardForTranslationLease()
        {
            string text = File.ReadAllText(WfcOutpostPowerBootRuntimePath());
            string acquire = ExtractMethodBody(text, "TryLockTranslationBuffers");
            string release = ExtractMethodBody(text, "UnlockTranslationBuffers");
            string schedule = ExtractMethodBody(text, "TryScheduleTranslation");
            string pendingRelease = ExtractMethodBody(text, "ReleasePendingTranslationLocks");
            string dispose = ExtractMethodBody(text, "Dispose");
            string force = ExtractMethodBody(text, "ForceCompleteTranslationInPostSimulationWindow");

            Assert.That(text, Does.Contain("TranslationMutationGuardMask"));
            Assert.That(text, Does.Contain("unchecked((int)(uint)(int)bufferId) & 31"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(lockMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(lockMask)"));
            Assert.That(schedule, Does.Contain("try"));
            Assert.That(schedule, Does.Contain("finally"));
            Assert.That(schedule, Does.Contain("UnlockTranslationBuffers(lockMask)"));
            Assert.That(pendingRelease, Does.Contain("UnlockTranslationBuffers(_translationBufferLockMask)"));
            Assert.That(dispose, Does.Contain("ForceCompleteTranslationInPostSimulationWindow(ref dependency)"));
            Assert.That(force, Does.Contain("DispatcherJobFence.BeginPostSimulationSwapWindow()"));
            Assert.That(force, Does.Contain("DispatcherJobFence.EndPostSimulationSwapWindow()"));
            Assert.AreEqual(0, Count(text, @"\b(?:TryLockBuffer|TryUnlockBuffer)\b"), "wfc outpost translation buffer pins");
        }

        [Test]
        public void SargassumScrapDisintegration_UsesPooledRigidbodyCache()
        {
            string sargassum = File.ReadAllText(SargassumCollapseChunkPath());
            string pool = File.ReadAllText(ObjectPoolManagerPath());
            string contracts = File.ReadAllText(GlobalRegistryContractsPath());
            string execute = ExtractMethodBody(sargassum, "ExecuteDisintegrationPoolCommands");

            Assert.That(contracts, Does.Contain("TryGetPooledRootRigidbody"));
            Assert.That(pool, Does.Contain("RootRigidbody"));
            Assert.That(pool, Does.Contain("TryGetPooledRootRigidbody"));
            Assert.That(execute, Does.Contain("TryGetPooledRootRigidbody"));
            Assert.AreEqual(0, Count(execute, @"\b(?:GetComponent|TryGetComponent)\s*\("), "sargassum hot scrap component lookup");
        }

        [Test]
        public void ContentAuthorityPairedWriters_UseMutationGuardsInsteadOfNestedWriteLocks()
        {
            string text = File.ReadAllText(ContentRuntimeServicesPath());
            string bundleRefs = ExtractMethodBody(text, "OpenOrAcquireWriteViews");
            string telemetry = ExtractMethodBody(text, "OpenOrAcquireTelemetryWriteBuffers");
            string pendingLoads = ExtractMethodBody(text, "OpenOrAcquirePendingLoadWritePointers");

            Assert.That(text, Does.Contain("BundleRefMutationGuardMask"));
            Assert.That(text, Does.Contain("ContentTelemetryMutationGuardMask"));
            Assert.That(text, Does.Contain("ContentPendingLoadMutationGuardMask"));
            Assert.That(bundleRefs, Does.Contain("TryAcquireMutationGuard(BundleRefMutationGuardMask)"));
            Assert.That(bundleRefs, Does.Contain("ReleaseBundleRefMutationGuard(vault)"));
            Assert.That(telemetry, Does.Contain("TryAcquireMutationGuard(ContentTelemetryMutationGuardMask)"));
            Assert.That(telemetry, Does.Contain("ReleaseTelemetryMutationGuard(vault)"));
            Assert.That(pendingLoads, Does.Contain("TryAcquireMutationGuard(ContentPendingLoadMutationGuardMask)"));
            Assert.That(pendingLoads, Does.Contain("ReleasePendingLoadMutationGuard(vault)"));
            Assert.AreEqual(0, Count(bundleRefs, @"\b(?:TryAcquireWriteView|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "content bundle refs nested write lock");
            Assert.AreEqual(0, Count(telemetry, @"\b(?:TryAcquireWriteView|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "content telemetry nested write lock");
            Assert.AreEqual(0, Count(pendingLoads, @"\b(?:TryAcquireWriteView|TryAcquireWriteLock|ReleaseWriteLock|TryLockBuffer|TryUnlockBuffer)\b"), "content pending loads nested write lock");
        }

        [Test]
        public void SimulationBucketerRebalance_UsesSingleGuardForJobPointerLifetime()
        {
            string bucketer = File.ReadAllText(ModuloSimulationBucketerPath());
            string vault = File.ReadAllText(GlobalDataVaultPath());
            string clear = ExtractMethodBody(bucketer, "ClearEntityState");
            string acquire = ExtractMethodBody(bucketer, "TryAcquireRebalanceVaultGuard");
            string release = ExtractMethodBody(bucketer, "ReleaseRebalanceVaultGuard");

            Assert.That(bucketer, Does.Contain("RebalanceVaultMutationGuardMask"));
            Assert.That(bucketer, Does.Contain("EntityStateVaultMutationGuardMask"));
            Assert.That(clear, Does.Contain("TryAcquireVaultMutationGuard(EntityStateVaultMutationGuardMask)"));
            Assert.That(clear, Does.Contain("ReleaseVaultMutationGuard(EntityStateVaultMutationGuardMask)"));
            Assert.That(acquire, Does.Contain("TryAcquireMutationGuard(RebalanceVaultMutationGuardMask)"));
            Assert.That(release, Does.Contain("ReleaseMutationGuard(RebalanceVaultMutationGuardMask)"));
            Assert.AreEqual(0, Count(clear, @"\b(?:TryAcquireWriteView|TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "clear entity state nested write locks");
            Assert.AreEqual(0, Count(acquire, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "rebalance acquire buffer pins");
            Assert.AreEqual(0, Count(release, @"\b(?:TryLockBuffer|TryUnlockBuffer|TryAcquireWriteLock|ReleaseWriteLock)\b"), "rebalance release buffer pins");
            Assert.That(vault, Does.Contain("HasMutationGuardForActiveLockBit(activeLockBit)"));
            Assert.That(vault, Does.Contain("HasActiveLockConflictForMutationMask(lowMask)"));
        }

        [Test]
        public void AupOriginShiftSchedule_UsesSingleGuardForScheduledJobViews()
        {
            string text = File.ReadAllText(AupOriginShiftCoordinatorPath());
            string schedule = ExtractMethodBody(text, "ScheduleVaultOriginRebase");
            string release = ExtractMethodBody(text, "ReleaseScheduledRebaseLocks");
            string marker = ExtractMethodBody(text, "TryMarkScheduledBuffer");

            Assert.That(text, Does.Contain("RebaseScheduleMutationGuardMask"));
            Assert.That(schedule, Does.Contain("TryAcquireMutationGuard(RebaseScheduleMutationGuardMask)"));
            Assert.That(schedule, Does.Contain("TryOpenVaultBuffer(vault, in _runtimeStateHandle"));
            Assert.That(schedule, Does.Not.Contain("TryAcquireWriteView(vault, in _runtimeStateHandle"));
            Assert.AreEqual(0, Count(schedule, @"\b(?:TryLockBuffer|TryUnlockBuffer)\b"), "AUP scheduled job buffer pins");
            Assert.That(release, Does.Contain("ReleaseMutationGuard(RebaseScheduleMutationGuardMask)"));
            Assert.AreEqual(0, Count(release, @"\b(?:TryLockBuffer|TryUnlockBuffer|ReleaseWriteLock)\b"), "AUP scheduled release pins");
            Assert.AreEqual(0, Count(marker, @"\b(?:TryLockBuffer|TryAcquireWriteLock)\b"), "AUP marker lock acquisition");
        }

        [Test]
        public void MockSequentialVaultLocks_ReverseOrderContention_FailsClosedWithoutDeadlock()
        {
            const int iterations = 512;
            MockVaultLockTable table = new MockVaultLockTable(2);
            int[] counters = new int[3];

            Thread workerA = new Thread(() => RunSequentialLockWorker(table, 0, 1, iterations, counters));
            Thread workerB = new Thread(() => RunSequentialLockWorker(table, 1, 0, iterations, counters));
            workerA.IsBackground = true;
            workerB.IsBackground = true;

            workerA.Start();
            workerB.Start();

            bool joinedA = workerA.Join(3000);
            bool joinedB = workerB.Join(3000);

            Assert.IsTrue(joinedA && joinedB, "sequential mock lock workers timed out");
            Assert.AreEqual(0, Volatile.Read(ref counters[NestedViolationCounter]), "worker held more than one vault lock");
            Assert.Greater(Volatile.Read(ref counters[CompletedCounter]), 0, "no lock acquisition completed");
            Assert.GreaterOrEqual(Volatile.Read(ref counters[FailedCounter]), 0, "contention counter sanity");
        }

        private static void RunSequentialLockWorker(
            MockVaultLockTable table,
            int first,
            int second,
            int iterations,
            int[] counters)
        {
            for (int i = 0; i < iterations; i++)
            {
                TryAcquireOne(table, first, counters);
                TryAcquireOne(table, second, counters);
            }
        }

        private static void TryAcquireOne(
            MockVaultLockTable table,
            int index,
            int[] counters)
        {
            int held = 0;
            if (!table.TryAcquire(index))
            {
                Interlocked.Increment(ref counters[FailedCounter]);
                return;
            }

            try
            {
                held++;
                if (held > 1)
                    Interlocked.Increment(ref counters[NestedViolationCounter]);
                Thread.SpinWait(32);
                Interlocked.Increment(ref counters[CompletedCounter]);
            }
            finally
            {
                table.Release(index);
                held--;
            }
        }

        private static string RuntimeScriptsRoot()
        {
            return Path.Combine(ProjectRoot(), "Assets", "_Project", "Scripts");
        }

        private static string MemorySentinelPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "MemorySentinelRuntime.cs");
        }

        private static string DispatcherJobFencePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Contracts", "CoreLowLevelUtilities.cs");
        }

        private static string GlobalDataVaultPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Memory", "GlobalDataVault.cs");
        }

        private static string PathFunnelVoxelAStarPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "AI", "Pathfinding", "PathFunnelNavmeshRuntime_VoxelAStar.cs");
        }

        private static string VisualPressureAgingPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Graphics", "Materials", "VisualPressureAgingRuntime.cs");
        }

        private static string AnalyticsExporterPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Diagnostics", "AsynchronousTelemetryExporter.cs");
        }

        private static string BulkheadRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Construction", "BulkheadContainmentRuntime.cs");
        }

        private static string BulkheadIntentBusPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "BulkheadContainmentIntentBus.cs");
        }

        private static string BulkheadHatchLocksPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Construction", "BulkheadContainmentRuntime_HatchLocks.cs");
        }

        private static string HectonBlueprintPreviewBatchPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Construction", "HectonBlueprintPreviewBatch.cs");
        }

        private static string VRPipeBlueprintPreviewPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Construction", "VRPipeBlueprintPreview.cs");
        }

        private static string FoundationSnappingCalculatorDataPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Construction", "FoundationSnappingCalculatorData.cs");
        }

        private static string VehicleDockingModulePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Construction", "VehicleDockingModule.cs");
        }

        private static string GlobalShaderDispatcherPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Rendering", "GlobalShaderDispatcher.cs");
        }

        private static string ShinobuLogisticsRouterPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Power", "ShinobuLogisticsRouter.cs");
        }

        private static string SubmarineOsThermalGridRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Power", "SubmarineOsThermalGridRuntime.cs");
        }

        private static string PowerGridSolarContractsPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Power", "PowerGridSolarContracts.cs");
        }

        private static string BatteryChargerLogisticsRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Power", "BatteryChargerLogistics", "BatteryChargerLogisticsRuntime.cs");
        }

        private static string WfcOutpostPowerBootRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Power", "WfcOutpostPowerBootRuntime.cs");
        }

        private static string ScavengingLootOraclePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Scavenging", "ScavengingLootOracle.cs");
        }

        private static string CrashTelemetryBufferPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "CrashTelemetryBuffer.cs");
        }

        private static string FoveatedSimulationManagerPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "FoveatedSimulationManager.cs");
        }

        private static string GlobalTelemetryBusPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "GlobalTelemetryBus.cs");
        }

        private static string UIStateStorePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "UIStateStore.cs");
        }

        private static string H8MemoryPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Memory", "H8Memory.cs");
        }

        private static string HomeostasisBrainPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "HomeostasisBrain.ScalabilityDictator.cs");
        }

        private static string SystemDispatcherPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "SystemDispatcher.cs");
        }

        private static string BabelDictionaryStorePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Data", "BabelDictionaryStore.cs");
        }

        private static string LockstepStateValidatorPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Determinism", "LockstepStateValidator.cs");
        }

        private static string SignalWardenRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Signals", "SignalWardenRuntime.cs");
        }

        private static string ChemicalInfluenceGridPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "World", "ChemicalInfluenceGrid.cs");
        }

        private static string VolcanicUpdraftDirectorPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "World", "VolcanicUpdraftDirector.cs");
        }

        private static string ShinobuPhysiologyRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Physiology", "ShinobuPhysiologyRuntime.cs");
        }

        private static string ShinobuMetabolismRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Physiology", "ShinobuMetabolismRuntime.cs");
        }

        private static string ShinobuSuitIntegrityRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Physiology", "ShinobuSuitIntegrityRuntime.cs");
        }

        private static string ShinobuRadiationMutationRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Physiology", "ShinobuRadiationMutationRuntime.cs");
        }

        private static string CombatDamageRuntimeVaultViewsPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "Combat", "CombatDamageRuntime_VaultViews.cs");
        }

        private static string LootMagnetSystemPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "Loot", "LootMagnetSystem.cs");
        }

        private static string DebrisManagerPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "DebrisManager.cs");
        }

        private static string PlayerKinematicsRuntimeHandIkPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "PlayerKinematicsRuntime_HandIK.cs");
        }

        private static string HazardZoneManagerPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Gameplay", "HazardZoneManager.cs");
        }

        private static string MacroEcosystemMathematicianRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Ecosystem", "MacroEcosystemMathematicianRuntime.cs");
        }

        private static string MigrationDirectorPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Ecosystem", "MigrationDirector.cs");
        }

        private static string ShinobuRespawnReconciliationRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Physiology", "ShinobuRespawnReconciliationRuntime.cs");
        }

        private static string ShinobuSensoryImpairmentRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Physiology", "ShinobuSensoryImpairmentRuntime.cs");
        }

        private static string NutrientDriftRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Ecosystem", "NutrientDriftRuntime.cs");
        }

        private static string NutrientDriftCarrionPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Ecosystem", "NutrientDriftRuntime_Carrion.cs");
        }

        private static string SargassumCollapseChunkPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "World", "SargassumCollapseChunk.cs");
        }

        private static string VehicleComponentDamageRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Physics", "Vehicles", "VehicleComponentDamageRuntime.cs");
        }

        private static string AuxiliaryEquipmentRouterRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Equipment", "Auxiliary", "AuxiliaryEquipmentRouterRuntime.cs");
        }

        private static string SeedShipAnomalyRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "World", "SeedShipAnomaly", "SeedShipAnomalyRuntime.cs");
        }

        private static string SeedShipAnomalyShaderBridgePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "World", "SeedShipAnomaly", "SeedShipAnomalyShaderBridge.cs");
        }

        private static string AbyssalShadowCullingRuntimePath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Graphics", "Culling", "AbyssalShadowCullingRuntime.cs");
        }

        private static string DynamicPointLightCullingDirectorPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Lighting", "DynamicPointLightCulling", "DynamicPointLightCullingDirector.cs");
        }

        private static string ObjectPoolManagerPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "ObjectPoolManager.cs");
        }

        private static string GlobalRegistryContractsPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "GlobalRegistryContracts.cs");
        }

        private static string ContentRuntimeServicesPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Content", "ContentRuntimeServices.cs");
        }

        private static string ModuloSimulationBucketerPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Bucketing", "ModuloSimulationBucketer.cs");
        }

        private static string AupOriginShiftCoordinatorPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Origin", "AupOriginShiftCoordinator.cs");
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static bool IsEditorOrDevPath(string path)
        {
            return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("/Dev/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static int Count(string text, string pattern)
        {
            return Regex.Matches(text, pattern).Count;
        }

        private static string ExtractMethodBody(string text, string methodName)
        {
            Regex declaration = new Regex(
                @"(?m)^\s*(?:(?:public|private|protected|internal|static|readonly|unsafe|virtual|override|sealed|partial|new)\s+)*(?:[\w<>\[\],\.\?]+\s+)+" +
                Regex.Escape(methodName) +
                @"(?:\s*<[^>\r\n]+>)?\s*\(",
                RegexOptions.CultureInvariant);
            Match match = declaration.Match(text);
            Assert.IsTrue(match.Success, "Missing method " + methodName);
            return ExtractBodyFromDeclaration(text, match.Index, methodName);
        }

        private static string ExtractMethodBodyFromDeclaration(string text, string declarationSnippet, string label)
        {
            int declarationIndex = text.IndexOf(declarationSnippet, StringComparison.Ordinal);
            Assert.GreaterOrEqual(declarationIndex, 0, "Missing method " + label);
            return ExtractBodyFromDeclaration(text, declarationIndex, label);
        }

        private static string ExtractBodyFromDeclaration(string text, int declarationIndex, string label)
        {
            int open = text.IndexOf('{', declarationIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method body " + label);

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return text.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Unclosed method body " + label);
            return string.Empty;
        }

        private sealed class MockVaultLockTable
        {
            private readonly int[] _owners;

            public MockVaultLockTable(int count)
            {
                _owners = new int[count];
            }

            public bool TryAcquire(int index)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                return Interlocked.CompareExchange(ref _owners[index], threadId, 0) == 0;
            }

            public void Release(int index)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Interlocked.CompareExchange(ref _owners[index], 0, threadId);
            }
        }
    }
}
