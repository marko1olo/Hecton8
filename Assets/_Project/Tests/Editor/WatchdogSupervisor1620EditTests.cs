using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Editor.QA;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class WatchdogSupervisor1620EditTests
    {
        [Test]
        public void LogParser_CatchesCompilerAndRuntimeFailures()
        {
            byte[] compilerLine = Encoding.ASCII.GetBytes("Assets/_Project/Scripts/Core/Foo.cs(42,13): error CS1002: ; expected");
            int compilerCode;
            int compilerLineNumber;

            Assert.IsTrue(WatchdogSupervisor1620.TryParseLogBytesForTest(compilerLine, compilerLine.Length, out compilerCode, out compilerLineNumber));
            Assert.AreEqual(10, compilerCode);
            Assert.AreEqual(42, compilerLineNumber);

            byte[] nullRefLine = Encoding.ASCII.GetBytes("NullReferenceException: Object reference not set (at Assets/_Project/Scripts/Core/Foo.cs:line 77)");
            int nullRefCode;
            int nullRefLineNumber;

            Assert.IsTrue(WatchdogSupervisor1620.TryParseLogBytesForTest(nullRefLine, nullRefLine.Length, out nullRefCode, out nullRefLineNumber));
            Assert.AreEqual(11, nullRefCode);
            Assert.AreEqual(77, nullRefLineNumber);
        }

        [Test]
        public void DeadlockDetector_TripsAfterTwoLongFramesOrOneConfirmedSecond()
        {
            WatchdogSupervisor1620.ResetDeadlockDetectorForTest();

            Assert.IsFalse(WatchdogSupervisor1620.ObserveFrameDeltaForTest(0.49d));
            Assert.IsFalse(WatchdogSupervisor1620.ObserveFrameDeltaForTest(0.51d));
            Assert.IsTrue(WatchdogSupervisor1620.ObserveFrameDeltaForTest(0.52d));

            WatchdogSupervisor1620.ResetDeadlockDetectorForTest();
            Assert.IsTrue(WatchdogSupervisor1620.ObserveFrameDeltaForTest(1.01d));
        }

        [Test]
        public void DeadlockSnapshot_CapturesVaultScalarsWithoutDiskIo()
        {
            WatchdogSupervisor1620.DeadlockSnapshot1620 snapshot;
            WatchdogSupervisor1620.CaptureDeadlockSnapshotForTest(out snapshot);

            Assert.AreEqual(1620, snapshot.Version);
            Assert.GreaterOrEqual(snapshot.LongFrameCount, 0);
            Assert.GreaterOrEqual(snapshot.AccumulatedSeconds, 0.0d);
        }

        [Test]
        public void CsvAnalyzer_ComputesP95PeakVramAndGcFromWatchdogReport()
        {
            string path = Path.Combine("Temp", "WatchdogSupervisor1620_MockCsv.csv");
            File.WriteAllText(
                path,
                "frame,state,frame_time_ms,gc_alloc_bytes,vram_mb,batches,setpass,aup_x,aup_y,aup_z,flags,fail_reason_code,fail_reason,distance_m,rolling_p95_ms,consecutive_spike_frames,vault_write_failures,defrag_requests,menu_resolve_attempts,scene_fallback_attempts,foveation_level,mip_limit,global_quality_weight\n" +
                "0,Simulation,10.0,0,1200,0,0,0,0,0,0,0,None,1.0,10.0,0,0,0,0,0,0.0,0,1.0\n" +
                "1,Simulation,20.0,0,1700,0,0,0,0,0,0,0,None,2.0,20.0,0,0,0,0,0,0.4,1,0.8\n" +
                "2,Completed,30.0,0,1300,0,0,0,0,0,0,0,None,10000.0,30.0,0,0,0,0,0,0.4,1,0.8\n",
                Encoding.ASCII);

            double p95;
            int peakVram;
            long totalGc;

            Assert.IsTrue(WatchdogSupervisor1620.AnalyzeCsvFileForTest(path, out p95, out peakVram, out totalGc));
            Assert.AreEqual(1700, peakVram);
            Assert.AreEqual(0L, totalGc);
            Assert.GreaterOrEqual(p95, 29.75d);
            Assert.LessOrEqual(p95, 30.25d);
        }

        [Test]
        public void CsvAnalyzer_TreatsFailedTerminalStateAsFailure()
        {
            string path = Path.Combine("Temp", "WatchdogSupervisor1620_FailedCsv.csv");
            File.WriteAllText(
                path,
                "frame,state,frame_time_ms,gc_alloc_bytes,vram_mb,batches,setpass,aup_x,aup_y,aup_z,flags,fail_reason_code,fail_reason,distance_m,rolling_p95_ms\n" +
                "0,Simulation,10.0,0,1200,0,0,0,0,0,0,0,None,1.0,10.0\n" +
                "1,Failed,18.0,0,1300,0,0,0,0,0,0,77,NativeLeak,12.0,18.0\n",
                Encoding.ASCII);

            double p95;
            int peakVram;
            long totalGc;

            Assert.IsTrue(WatchdogSupervisor1620.AnalyzeCsvFileForTest(path, out p95, out peakVram, out totalGc));
            WatchdogSupervisor1620.GetCsvTerminalStateForTest(out bool terminalObserved, out bool terminalFailed, out int failReasonCode);
            Assert.IsTrue(terminalObserved);
            Assert.IsTrue(terminalFailed);
            Assert.AreEqual(77, failReasonCode);
        }

        [Test]
        public void CsvAnalyzer_FlagsOverBudgetVramWithoutScalabilityResponse()
        {
            string path = Path.Combine("Temp", "WatchdogSupervisor1620_VramNoResponse.csv");
            File.WriteAllText(
                path,
                "frame,state,frame_time_ms,gc_alloc_bytes,vram_mb,batches,setpass,aup_x,aup_y,aup_z,flags,fail_reason_code,fail_reason,distance_m,rolling_p95_ms,foveation_level,mip_limit,global_quality_weight\n" +
                "0,Simulation,10.0,0,1701,0,0,0,0,0,0,0,None,1.0,10.0,0.0,0,1.0\n" +
                "1,Completed,11.0,0,1702,0,0,0,0,0,0,0,None,10000.0,11.0,0.0,0,1.0\n",
                Encoding.ASCII);

            double p95;
            int peakVram;
            long totalGc;

            Assert.IsTrue(WatchdogSupervisor1620.AnalyzeCsvFileForTest(path, out p95, out peakVram, out totalGc));
            Assert.AreEqual(1702, peakVram);
            Assert.IsTrue(WatchdogSupervisor1620.IsHomeostasisUnprovenForTest());
        }

        [Test]
        public void NativeLeakGate_ReportsSentinelRegisteredLeak()
        {
            NativeMemorySentinel.ResetForSubsystemReload();
            void* pointer = UnsafeUtility.Malloc(100, 16, Allocator.Persistent);
            int id = 0;

            try
            {
                id = NativeMemorySentinel.RegisterPointer(
                    pointer,
                    100,
                    "WatchdogSupervisor1620Test",
                    "IntentionalLeak100",
                    NativeAllocationLifetime.Session);

                int activeCount;
                long trackedBytes;
                bool clean = WatchdogSupervisor1620.ValidateNativeLeaksForTest(out activeCount, out trackedBytes);

                Assert.IsFalse(clean);
                Assert.GreaterOrEqual(activeCount, 1);
                Assert.GreaterOrEqual(trackedBytes, 100L);
            }
            finally
            {
                if (id > 0)
                    NativeMemorySentinel.Unregister(id);
                if (pointer != null)
                    UnsafeUtility.Free(pointer, Allocator.Persistent);
                NativeMemorySentinel.ResetForSubsystemReload();
            }
        }

        [Test]
        public void HotParsers_DoNotUseStringSplitLinqOrReferenceConstruction()
        {
            string source = ReadProjectFile("Assets/_Project/Editor/QA/WatchdogSupervisor1620.cs");

            AssertNoHotAllocationConstructs(ExtractMethod(source, "private static bool TryParseLogLineForFailure"));
            AssertNoHotAllocationConstructs(ExtractMethod(source, "private static void AppendLogByte"));
            AssertNoHotAllocationConstructs(ExtractMethod(source, "private static int IndexOf"));
            AssertNoHotAllocationConstructs(ExtractMethod(source, "private static bool TryGetFieldBounds"));
            AssertNoHotAllocationConstructs(ExtractMethod(source, "private static bool TryParseDouble"));

            StringAssert.Contains("FileShare.ReadWrite | FileShare.Delete", source);
            StringAssert.Contains("ProfilerRecorder.StartNew(ProfilerCategory.Memory, \"GC Allocated In Frame\", 1)", source);
            StringAssert.Contains("AssertNoAllocationsAfterServiceShutdown", source);
            Assert.Less(source.IndexOf("PollGcRecorder();", StringComparison.Ordinal), source.IndexOf("if (now >= _nextPollTime)", StringComparison.Ordinal), source);
            StringAssert.Contains("WatchdogRuntimeFailed", source);
            StringAssert.Contains("PlayModeStartIssuedKey", source);
            StringAssert.Contains("PlayModeObservedKey", source);
            StringAssert.Contains("StartDelayLoggedKey", source);
            StringAssert.Contains("PlayModeExitedUnexpectedly", source);
            int unexpectedExitIndex = source.IndexOf("PLAYMODE_EXITED_BEFORE_TERMINAL", StringComparison.Ordinal);
            Assert.Greater(unexpectedExitIndex, 0, source);
            int csvPollBeforeExit = source.LastIndexOf("PollCsvCold();", unexpectedExitIndex, StringComparison.Ordinal);
            int logFlushBeforeExit = source.LastIndexOf("FlushPartialLogLine();", unexpectedExitIndex, StringComparison.Ordinal);
            int partialFlushBeforeExit = source.LastIndexOf("FlushPartialCsvLine();", unexpectedExitIndex, StringComparison.Ordinal);
            int terminalStopBeforeExit = source.LastIndexOf("RequestTerminalStop();", unexpectedExitIndex, StringComparison.Ordinal);
            Assert.Greater(csvPollBeforeExit, 0, source);
            Assert.Greater(logFlushBeforeExit, 0, source);
            Assert.Greater(partialFlushBeforeExit, 0, source);
            Assert.Greater(terminalStopBeforeExit, 0, source);
            Assert.Less(csvPollBeforeExit, unexpectedExitIndex, source);
            Assert.Less(logFlushBeforeExit, unexpectedExitIndex, source);
            Assert.Less(partialFlushBeforeExit, unexpectedExitIndex, source);
            Assert.Less(terminalStopBeforeExit, unexpectedExitIndex, source);
            string finalize = ExtractMethod(source, "private static void FinalizeRun");
            StringAssert.Contains("FlushPartialLogLine();", finalize);
            string tick = ExtractMethod(source, "private static void Tick");
            StringAssert.Contains("ResetDeadlockDetectorCold();", tick);
            Assert.Less(tick.IndexOf("ResetDeadlockDetectorCold();", StringComparison.Ordinal), tick.IndexOf("ObserveFrameDelta(delta)", StringComparison.Ordinal), tick);
            StringAssert.Contains("_csvTerminalFailed", source);
            StringAssert.Contains("!hasResponseColumn || !responseActive", source);
            StringAssert.Contains("raw is ulong", source);
            StringAssert.DoesNotContain("PROJECT_STABILITY_VERDICT_1620.json", source);
            StringAssert.DoesNotContain("Dump_1620_Deadlock.bin", source);
        }

        [Test]
        public void ApexQaDomain_HotLoopsHaveNoColdLookupsAndPresentationIsLateFrame()
        {
            string watchdog = ReadProjectFile("Assets/_Project/Scripts/QA/QA_WatchdogBot.cs");
            string endurance = ReadProjectFile("Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs");
            string fuzzer = ReadProjectFile("Assets/_Project/Scripts/QA/QAWatchdogGcAllocationFuzzer1524.cs");
            string supervisor = ReadProjectFile("Assets/_Project/Editor/QA/WatchdogSupervisor1620.cs");

            AssertHotMethodHasNoColdLookup(watchdog, "public void FastTick");
            AssertHotMethodHasNoColdLookup(watchdog, "private void FastTickSimulation");
            AssertHotMethodHasNoColdLookup(watchdog, "private void PublishDriveInputHot");
            AssertHotMethodHasNoColdLookup(watchdog, "private void IntegrateDistanceHot");
            AssertHotMethodHasNoColdLookup(watchdog, "public void LateFrameTick");

            AssertHotMethodHasNoColdLookup(endurance, "public void FastTick");
            AssertHotMethodHasNoColdLookup(endurance, "public void LateFrameTick");
            AssertHotMethodHasNoColdLookup(fuzzer, "public void FastTick");
            AssertHotMethodHasNoColdLookup(supervisor, "private static void Tick");

            string headlessSimulation = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs");
            string stressFracture = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs");
            string shinobu38 = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs");
            AssertHotMethodHasNoColdLookup(headlessSimulation, "public void FastTick");
            AssertHotMethodHasNoColdLookup(headlessSimulation, "public void LateFrameTick");
            AssertHotMethodHasNoColdLookup(stressFracture, "public void FastTick");
            AssertHotMethodHasNoColdLookup(stressFracture, "public void LateFrameTick");
            AssertHotMethodHasNoColdLookup(shinobu38, "public void FastTick");
            AssertHotMethodHasNoColdLookup(shinobu38, "public void LateFrameTick");

            StringAssert.Contains("private void TryRegisterTickLaneCold()", fuzzer);
            StringAssert.Contains("public void FastTick(float deltaTime)", fuzzer);
            StringAssert.DoesNotContain("private void Update()", fuzzer);
            AssertTokenOccursInsideAny(
                watchdog,
                "TryGetComponent",
                "private MainMenuController ResolveMainMenuControllerCold",
                "private static MainMenuController FindMainMenuControllerInChildrenCold");
            AssertTokenOccursInsideAny(
                fuzzer,
                "TryGetComponent",
                "private static void EnsureInstanceCold");
            StringAssert.Contains("public void LateFrameTick()", watchdog);
            StringAssert.Contains("public void LateFrameTick()", endurance);
        }

        [Test]
        public void ApexQaDomain_JobExecuteMethodsHaveNoColdLookups()
        {
            string shinobu38 = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs");
            string jacobi = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs");

            AssertAllMethodsHaveNoColdLookup(shinobu38, "public void Execute");
            AssertAllMethodsHaveNoColdLookup(jacobi, "public void Execute");
            StringAssert.DoesNotContain(".Complete(", shinobu38);
            StringAssert.DoesNotContain(".Complete(", jacobi);
            StringAssert.Contains("DispatcherJobFence.TryComplete", shinobu38);
            StringAssert.Contains("DispatcherJobFence.TryComplete", jacobi);
        }

        [Test]
        public void ApexQaDomain_DataVaultWriteLocksAreSingleAndFinallyReleased()
        {
            string watchdog = ReadProjectFile("Assets/_Project/Scripts/QA/QA_WatchdogBot.cs");
            string endurance = ReadProjectFile("Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs");
            string headlessSimulation = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs");
            string stressFracture = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs");

            AssertSingleWriteLockWithFinally(ExtractMethod(watchdog, "private void WriteMetricHot"));
            AssertSingleWriteLockWithFinally(ExtractMethod(watchdog, "private void WriteBlackBoxHot"));
            AssertSingleWriteLockWithFinally(ExtractMethod(endurance, "private void WriteBlackBox"));
            AssertSingleWriteLockWithFinally(ExtractMethod(headlessSimulation, "private bool WriteMemoryWindowLongSample"));
            AssertSingleWriteLockWithFinally(ExtractMethod(headlessSimulation, "private bool WriteMemoryWindowIntSample"));
            AssertSingleWriteLockWithFinally(ExtractMethod(headlessSimulation, "private void RecordBlackbox"));
            AssertSingleWriteLockWithFinally(ExtractMethod(headlessSimulation, "private bool TryInitializeGhostState"));
            AssertSingleWriteLockWithFinally(ExtractMethod(headlessSimulation, "private bool TryCommitPendingGhostState"));
            AssertSingleWriteLockWithFinally(ExtractMethod(stressFracture, "private bool AcquireScratchBlock"));
            AssertSingleWriteLockWithFinally(ExtractMethod(stressFracture, "private void RecordBlackbox"));
        }

        [Test]
        public void ApexQaHeadlessResultsPromoteAtomically()
        {
            string headlessSimulation = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs");
            string stressFracture = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs");

            AssertHeadlessResultUsesAtomicPromote(headlessSimulation);
            AssertHeadlessResultUsesAtomicPromote(stressFracture);
        }

        [Test]
        public void ApexQaDomain_CompilationThrottleDoesNotLaunchBuildProcess()
        {
            string supervisor = ReadProjectFile("Assets/_Project/Editor/QA/WatchdogSupervisor1620.cs");
            string tests = ReadProjectFile("Assets/_Project/Tests/Editor/WatchdogSupervisor1620EditTests.cs");
            string buildToken = "dotnet " + "build";

            StringAssert.DoesNotContain(buildToken, supervisor);
            StringAssert.DoesNotContain("ProcessStartInfo", supervisor);
            StringAssert.DoesNotContain("System.Diagnostics.Process", supervisor);
            StringAssert.DoesNotContain(buildToken, tests);
        }

        [Test]
        public void ApexQaBatchRunners_UseSharedReadByteScannersNotLineSplitParsers()
        {
            string[] paths =
            {
                "Assets/_Project/Scripts/QA/Editor/QAWatchdogBatchRunner1524.cs",
                "Assets/_Project/Scripts/QA/Editor/QAEnduranceBatchRunner.cs",
                "Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs",
                "Assets/_Project/Scripts/QA/Headless/Editor/HeadlessStressFractureBatchRunner.cs",
                "Assets/_Project/Scripts/QA/Headless/Editor/Shinobu38QaWatchdogCommanderWindow.cs",
            };

            for (int i = 0; i < paths.Length; i++)
            {
                string source = ReadProjectFile(paths[i]);
                StringAssert.DoesNotContain("File.ReadLines", source, paths[i]);
                StringAssert.DoesNotContain(".Split(", source, paths[i]);
                StringAssert.DoesNotContain("ReadByte(", source, paths[i]);
                StringAssert.Contains("FileShare.ReadWrite | FileShare.Delete", source, paths[i]);
                StringAssert.Contains("Detach();", ExtractMethod(source, "private static void Tick"));
                if (paths[i].Contains("QAWatchdogBatchRunner1524"))
                    StringAssert.Contains("_csvReadOffset", source, paths[i]);
                if (paths[i].Contains("QAEnduranceBatchRunner"))
                {
                    StringAssert.Contains("private static bool TryWriteFlagFile()", source, paths[i]);
                    StringAssert.Contains("private static bool TryEnsureBootstrapScene()", source, paths[i]);
                }
            }
        }

        [Test]
        public void ApexQaEditorRunners_DetachCallbacksAndDisposePendingJobsInFinally()
        {
            string jacobiWindow = ReadProjectFile("Assets/_Project/Scripts/QA/Headless/Editor/JacobiStressFuzzer/JacobiStressFuzzerWindow.cs");
            string poll = ExtractMethod(jacobiWindow, "private void PollPendingRun");
            string finish = ExtractMethod(jacobiWindow, "private void FinishPendingRun");

            StringAssert.Contains("if (_pendingRun == null)", poll);
            StringAssert.Contains("EditorApplication.update -= PollPendingRun;", poll);
            StringAssert.Contains("EditorUtility.ClearProgressBar();", poll);
            StringAssert.Contains("try", finish);
            StringAssert.Contains("catch (Exception exception)", finish);
            StringAssert.Contains("finally", finish);
            StringAssert.Contains("run.Dispose();", finish);
            StringAssert.Contains("EditorApplication.update -= PollPendingRun;", finish);
            StringAssert.Contains("EditorUtility.ClearProgressBar();", finish);
            StringAssert.Contains("Debug.LogException(exception);", finish);
            Assert.Less(finish.IndexOf("run.Complete(out result);", StringComparison.Ordinal), finish.IndexOf("finally", StringComparison.Ordinal), finish);
            Assert.Less(finish.IndexOf("finally", StringComparison.Ordinal), finish.IndexOf("run.Dispose();", StringComparison.Ordinal), finish);
        }

        private static void AssertNoHotAllocationConstructs(string method)
        {
            Assert.IsFalse(method.Contains(".Split("), method);
            Assert.IsFalse(method.Contains("File.ReadLines"), method);
            Assert.IsFalse(method.Contains("File.ReadAllText"), method);
            Assert.IsFalse(method.Contains("foreach"), method);
            Assert.IsFalse(method.Contains("Enumerable"), method);
            Assert.IsFalse(method.Contains("new "), method);
            Assert.IsFalse(method.Contains("string.Concat"), method);
        }

        private static void AssertHotMethodHasNoColdLookup(string source, string signature)
        {
            string method = ExtractMethod(source, signature);
            AssertMethodBodyHasNoColdLookup(method);
        }

        private static void AssertAllMethodsHaveNoColdLookup(string source, string signature)
        {
            int count = 0;
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int start = source.IndexOf(signature, searchIndex, StringComparison.Ordinal);
                if (start < 0)
                    break;

                string method = ExtractMethodAt(source, start, signature);
                AssertMethodBodyHasNoColdLookup(method);
                count++;
                searchIndex = start + signature.Length;
            }

            Assert.Greater(count, 0, signature);
        }

        private static void AssertTokenOccursInsideAny(string source, string token, params string[] signatures)
        {
            string[] methods = new string[signatures.Length];
            int[] starts = new int[signatures.Length];
            int[] ends = new int[signatures.Length];
            for (int i = 0; i < signatures.Length; i++)
            {
                starts[i] = source.IndexOf(signatures[i], StringComparison.Ordinal);
                Assert.GreaterOrEqual(starts[i], 0, signatures[i]);
                methods[i] = ExtractMethodAt(source, starts[i], signatures[i]);
                ends[i] = starts[i] + methods[i].Length;
            }

            int tokenCount = 0;
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int tokenIndex = source.IndexOf(token, searchIndex, StringComparison.Ordinal);
                if (tokenIndex < 0)
                    break;

                bool allowed = false;
                for (int i = 0; i < starts.Length; i++)
                {
                    if (tokenIndex >= starts[i] && tokenIndex < ends[i])
                    {
                        allowed = true;
                        break;
                    }
                }

                Assert.IsTrue(allowed, token + " at " + tokenIndex);
                tokenCount++;
                searchIndex = tokenIndex + token.Length;
            }

            Assert.Greater(tokenCount, 0, token);
        }

        private static void AssertMethodBodyHasNoColdLookup(string method)
        {
            StringAssert.DoesNotContain("GlobalRegistry.Get<", method);
            StringAssert.DoesNotContain("GlobalRegistry.Get(", method);
            StringAssert.DoesNotContain("GlobalRegistry.DataVault", method);
            StringAssert.DoesNotContain("GetComponent(", method);
            StringAssert.DoesNotContain("GetComponent<", method);
            StringAssert.DoesNotContain("TryGetComponent(", method);
            StringAssert.DoesNotContain("TryGetComponent<", method);
        }

        private static void AssertSingleWriteLockWithFinally(string method)
        {
            Assert.AreEqual(1, CountToken(method, "TryAcquireWriteLock("), method);
            Assert.AreEqual(1, CountToken(method, "ReleaseWriteLock("), method);
            StringAssert.Contains("finally", method);
            Assert.Less(method.IndexOf("TryAcquireWriteLock(", StringComparison.Ordinal), method.IndexOf("finally", StringComparison.Ordinal), method);
            Assert.Less(method.IndexOf("finally", StringComparison.Ordinal), method.IndexOf("ReleaseWriteLock(", StringComparison.Ordinal), method);
        }

        private static void AssertHeadlessResultUsesAtomicPromote(string source)
        {
            StringAssert.Contains("PromoteResultFileCold(tempPath);", source);
            StringAssert.Contains("private void PromoteResultFileCold(string tempPath)", source);
            StringAssert.Contains("File.Replace(tempPath, _resultPath, null, true);", source);
            StringAssert.DoesNotContain("File.Delete(_resultPath);\r\n            File.Move(tempPath, _resultPath);", source);
            StringAssert.DoesNotContain("File.Delete(_resultPath);\n            File.Move(tempPath, _resultPath);", source);
            StringAssert.DoesNotContain("File.Delete(_resultPath);\r\n                File.Move(tempPath, _resultPath);", source);
            StringAssert.DoesNotContain("File.Delete(_resultPath);\n                File.Move(tempPath, _resultPath);", source);
        }

        private static int CountToken(string source, string token)
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

        private static string ReadProjectFile(string relativePath)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            return File.ReadAllText(path);
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);

            return ExtractMethodAt(source, start, signature);
        }

        private static string ExtractMethodAt(string source, int start, string signature)
        {
            int bodyStart = source.IndexOf('{', start);
            Assert.GreaterOrEqual(bodyStart, 0, signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;

                if (depth == 0)
                    return source.Substring(start, i - start + 1);
            }

            Assert.Fail("Method body not closed: " + signature);
            return string.Empty;
        }
    }
}
