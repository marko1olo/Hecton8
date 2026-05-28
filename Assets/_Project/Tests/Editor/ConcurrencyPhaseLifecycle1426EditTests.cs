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
            Assert.That(text, Does.Contain("forceComplete && !handle.IsCompleted && _activeSwapWindowDepth <= 0"));
            Assert.That(text, Does.Contain("WarnIllegalForcedCompletion();"));
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
            return Path.Combine(RuntimeScriptsRoot(), "Core", "DispatcherJobFence.cs");
        }

        private static string GlobalDataVaultPath()
        {
            return Path.Combine(RuntimeScriptsRoot(), "Core", "Memory", "GlobalDataVault.cs");
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
