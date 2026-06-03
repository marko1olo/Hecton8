#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.Editor.QA
{
    [InitializeOnLoad]
    public static class WatchdogSupervisor1620
    {
        private const string ActiveKey = "H8.QA.Supervisor1620.Active";
        private const string WaitingFinalizeKey = "H8.QA.Supervisor1620.WaitingFinalize";
        private const string FailureCodeKey = "H8.QA.Supervisor1620.FailureCode";
        private const string FailureTextKey = "H8.QA.Supervisor1620.FailureText";
        private const string StartTimeKey = "H8.QA.Supervisor1620.StartTime";
        private const string EditorLogOffsetKey = "H8.QA.Supervisor1620.EditorLogOffset";
        private const string CsvOffsetKey = "H8.QA.Supervisor1620.CsvOffset";
        private const string PlayModeStartIssuedKey = "H8.QA.Supervisor1620.PlayModeStartIssued";
        private const string PlayModeObservedKey = "H8.QA.Supervisor1620.PlayModeObserved";
        private const string StartDelayLoggedKey = "H8.QA.Supervisor1620.StartDelayLogged";

        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string WatchdogFlagPath = "Temp/H8_QA_WATCHDOG_1524.flag";
        private const string WatchdogCsvPath = "Docs/Reports/QA_WATCHDOG_ENDURANCE_REPORT_1524.csv";
        private const string SupervisorLogPath = "Docs/AgentLogs/WatchdogSupervisor1620.log";

        private const int ReadBufferSize = 8192;
        private const int MaxLineBytes = 4096;
        private const int HistogramBuckets = 1024;
        private const int HistogramBucketMicroseconds = 250;
        private const int TargetVramMegabytes = 1600;
        private const int DeadlockSnapshotVersion = 1620;
        private const int DeadlockConfirmFrames = 2;
        private const double PollIntervalSeconds = 0.25d;
        private const double DeadlockFrameSeconds = 0.5d;
        private const double DeadlockConfirmSeconds = 1.0d;
        private const double MaxRunSeconds = 7200.0d;

        private static readonly byte[] OneByteFlag = { (byte)'1' };
        private static readonly byte[] LogReadBuffer = new byte[ReadBufferSize];
        private static readonly byte[] LogLineBuffer = new byte[MaxLineBytes];
        private static readonly byte[] CsvReadBuffer = new byte[ReadBufferSize];
        private static readonly byte[] CsvLineBuffer = new byte[MaxLineBytes];
        private static readonly int[] FrameTimeBuckets = new int[HistogramBuckets];

        private static readonly byte[] PatternCsError = Encoding.ASCII.GetBytes("error CS");
        private static readonly byte[] PatternNullReference = Encoding.ASCII.GetBytes("NullReferenceException");
        private static readonly byte[] PatternIndexOutOfRange = Encoding.ASCII.GetBytes("IndexOutOfRangeException");
        private static readonly byte[] PatternAccessViolation = Encoding.ASCII.GetBytes("AccessViolationException");
        private static readonly byte[] PatternFatalLeak = Encoding.ASCII.GetBytes("FatalMemoryLeakException");
        private static readonly byte[] PatternLineSuffix = Encoding.ASCII.GetBytes(":line ");
        private static readonly byte[] PatternFrameTime = Encoding.ASCII.GetBytes("frame_time_ms");
        private static readonly byte[] PatternState = Encoding.ASCII.GetBytes("state");
        private static readonly byte[] PatternGc = Encoding.ASCII.GetBytes("gc_alloc_bytes");
        private static readonly byte[] PatternVram = Encoding.ASCII.GetBytes("vram_mb");
        private static readonly byte[] PatternDistance = Encoding.ASCII.GetBytes("distance_m");
        private static readonly byte[] PatternFailReasonCode = Encoding.ASCII.GetBytes("fail_reason_code");
        private static readonly byte[] PatternRollingP95 = Encoding.ASCII.GetBytes("rolling_p95_ms");
        private static readonly byte[] PatternCompleted = Encoding.ASCII.GetBytes("Completed");
        private static readonly byte[] PatternFailed = Encoding.ASCII.GetBytes("Failed");
        private static readonly byte[] PatternSimulation = Encoding.ASCII.GetBytes("Simulation");
        private static readonly byte[] PatternFoveationLevel = Encoding.ASCII.GetBytes("foveation_level");
        private static readonly byte[] PatternFoveation01 = Encoding.ASCII.GetBytes("foveation_level01");
        private static readonly byte[] PatternMipmapLimit = Encoding.ASCII.GetBytes("mip_limit");
        private static readonly byte[] PatternTextureMipmapLimit = Encoding.ASCII.GetBytes("texture_mip_limit");
        private static readonly byte[] PatternQualityWeight = Encoding.ASCII.GetBytes("global_quality_weight");

        private static ProfilerRecorder _gcAllocRecorder;
        private static long _editorLogOffset;
        private static long _csvOffset;
        private static int _logLineLength;
        private static int _csvLineLength;
        private static int _logFailureCode;
        private static int _logFailureLine;
        private static int _failureCode;
        private static int _finalFailReasonCode;
        private static int _longFrameCount;
        private static int _sampleCount;
        private static int _simulationSampleCount;
        private static int _peakVramMegabytes;
        private static int _vramOverBudgetSamples;
        private static int _vramResponseSamples;
        private static int _foveationColumn;
        private static int _mipmapColumn;
        private static int _qualityWeightColumn;
        private static int _frameTimeColumn;
        private static int _stateColumn;
        private static int _gcColumn;
        private static int _vramColumn;
        private static int _distanceColumn;
        private static int _failReasonCodeColumn;
        private static int _rollingP95Column;
        private static bool _csvHeaderParsed;
        private static bool _lastRowWasSimulation;
        private static bool _terminalObserved;
        private static bool _csvTerminalFailed;
        private static bool _deadlockDetected;
        private static bool _homeostasisUnproven;
        private static bool _leakCheckPassed;
        private static long _totalGcBytes;
        private static long _nativeTrackedBytes;
        private static int _nativeActiveAllocations;
        private static double _lastEditorUpdateTime;
        private static double _nextPollTime;
        private static double _deadlockAccumulatedSeconds;
        private static double _maxEditorFrameSeconds;
        private static double _lastDistanceMeters;
        private static double _lastRollingP95Milliseconds;
        private static DeadlockSnapshot1620 _lastDeadlockSnapshot;

        static WatchdogSupervisor1620()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                RestoreSessionState();
                AttachUpdate();
            }
        }

        [MenuItem("Hecton8/QA/1620/Run Integration Watchdog", false, 16200)]
        public static void RunMenu()
        {
            StartSupervisorRun();
        }

        [MenuItem("Hecton8/QA/1620/Stop Integration Watchdog", false, 16201)]
        public static void StopMenu()
        {
            RequestStop(WatchdogFailureCode.ManualStop, "MANUAL_STOP");
        }

        public static void StartSupervisorRun()
        {
            ResetRunState();
            Directory.CreateDirectory(Path.GetDirectoryName(WatchdogFlagPath));
            Directory.CreateDirectory(Path.GetDirectoryName(WatchdogCsvPath));
            Directory.CreateDirectory(Path.GetDirectoryName(SupervisorLogPath));

            TryDeleteFile(WatchdogCsvPath);
            File.WriteAllBytes(WatchdogFlagPath, OneByteFlag);

            _editorLogOffset = ResolveExistingLength(ResolveEditorLogPath());
            _csvOffset = 0L;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(WaitingFinalizeKey, false);
            SessionState.SetInt(FailureCodeKey, 0);
            SessionState.SetString(FailureTextKey, string.Empty);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetString(EditorLogOffsetKey, _editorLogOffset.ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(CsvOffsetKey, "0");
            SessionState.SetBool(PlayModeStartIssuedKey, false);
            SessionState.SetBool(PlayModeObservedKey, false);
            SessionState.SetBool(StartDelayLoggedKey, false);

            StartProfilerRecorders();
            AttachUpdate();
            AppendSupervisorLog("START");
            TryStartPlayMode();
        }

        public static void ResetDeadlockDetectorForTest()
        {
            ResetDeadlockDetectorCold();
        }

        public static bool ObserveFrameDeltaForTest(double deltaSeconds)
        {
            return ObserveFrameDelta(deltaSeconds);
        }

        public static bool TryParseLogBytesForTest(byte[] bytes, int count, out int failureCode, out int sourceLineNumber)
        {
            if (bytes == null)
            {
                failureCode = (int)WatchdogFailureCode.None;
                sourceLineNumber = -1;
                return false;
            }

            int safeCount = count;
            if (safeCount < 0)
                safeCount = 0;
            if (safeCount > bytes.Length)
                safeCount = bytes.Length;

            return TryParseLogLineForFailure(bytes, safeCount, out failureCode, out sourceLineNumber);
        }

        public static bool AnalyzeCsvFileForTest(string path, out double p95Milliseconds, out int peakVramMegabytes, out long totalGcBytes)
        {
            ResetCsvAccumulator();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                p95Milliseconds = 0.0d;
                peakVramMegabytes = 0;
                totalGcBytes = 0L;
                return false;
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                int read;
                while ((read = stream.Read(CsvReadBuffer, 0, CsvReadBuffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                        AppendCsvByte(CsvReadBuffer[i]);
                }
            }

            if (_csvLineLength > 0)
            {
                EvaluateCsvLine(CsvLineBuffer, _csvLineLength);
                _csvLineLength = 0;
            }

            p95Milliseconds = CalculateP95Milliseconds();
            peakVramMegabytes = _peakVramMegabytes;
            totalGcBytes = _totalGcBytes;
            return _sampleCount > 0;
        }

        public static bool ValidateNativeLeaksForTest(out int activeAllocationCount, out long trackedBytes)
        {
            return ValidateNativeLeaksCold(out activeAllocationCount, out trackedBytes);
        }

        public static void GetCsvTerminalStateForTest(out bool terminalObserved, out bool terminalFailed, out int failReasonCode)
        {
            terminalObserved = _terminalObserved;
            terminalFailed = _csvTerminalFailed;
            failReasonCode = _finalFailReasonCode;
        }

        public static bool IsHomeostasisUnprovenForTest()
        {
            return _homeostasisUnproven;
        }

        public static bool CaptureDeadlockSnapshotForTest(out DeadlockSnapshot1620 snapshot)
        {
            return CaptureDeadlockSnapshotCold(out snapshot);
        }

        private static void AttachUpdate()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void DetachUpdate()
        {
            EditorApplication.update -= Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                DetachUpdate();
                return;
            }

            if (SessionState.GetBool(WaitingFinalizeKey, false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    FinalizeRun();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                double editorWaitStart = SessionState.GetFloat(StartTimeKey, (float)now);
                if (now - editorWaitStart > MaxRunSeconds)
                {
                    RequestStop(WatchdogFailureCode.Timeout, "MAX_RUN_SECONDS_EXCEEDED");
                    return;
                }

                if (!SessionState.GetBool(PlayModeStartIssuedKey, false))
                {
                    TryStartPlayMode();
                }
                else
                {
                    PollEditorLogCold();
                    FlushPartialLogLine();
                    PollCsvCold();
                    FlushPartialCsvLine();
                    PersistOffsets();

                    if (!SessionState.GetBool(ActiveKey, false))
                        return;

                    if (_terminalObserved)
                        RequestTerminalStop();
                    else
                        RequestStop(WatchdogFailureCode.PlayModeExitedUnexpectedly, "PLAYMODE_EXITED_BEFORE_TERMINAL");
                }

                return;
            }

            double delta = _lastEditorUpdateTime <= 0.0d ? 0.0d : now - _lastEditorUpdateTime;
            if (EditorApplication.isPlaying && !SessionState.GetBool(PlayModeObservedKey, false))
            {
                SessionState.SetBool(PlayModeObservedKey, true);
                ResetDeadlockDetectorCold();
                delta = 0.0d;
            }

            _lastEditorUpdateTime = now;

            if (EditorApplication.isPlaying && ObserveFrameDelta(delta))
            {
                DeclareDeadlock();
                return;
            }

            PollGcRecorder();

            if (now >= _nextPollTime)
            {
                _nextPollTime = now + PollIntervalSeconds;
                PollEditorLogCold();
                PollCsvCold();
                PersistOffsets();
            }

            double playModeRunStart = SessionState.GetFloat(StartTimeKey, (float)now);
            if (now - playModeRunStart > MaxRunSeconds)
                RequestStop(WatchdogFailureCode.Timeout, "MAX_RUN_SECONDS_EXCEEDED");

            if (_terminalObserved)
                RequestTerminalStop();
        }

        private static void RequestTerminalStop()
        {
            WatchdogFailureCode terminalCode = _csvTerminalFailed ? WatchdogFailureCode.WatchdogRuntimeFailed : WatchdogFailureCode.None;
            RequestStop(terminalCode, _csvTerminalFailed ? "CSV_FAILED_STATE" : "CSV_COMPLETED_STATE");
        }

        private static bool ObserveFrameDelta(double deltaSeconds)
        {
            if (deltaSeconds > _maxEditorFrameSeconds)
                _maxEditorFrameSeconds = deltaSeconds;

            if (deltaSeconds > DeadlockFrameSeconds)
            {
                _longFrameCount++;
                _deadlockAccumulatedSeconds += deltaSeconds;
            }
            else
            {
                _longFrameCount = 0;
                _deadlockAccumulatedSeconds = 0.0d;
            }

            if (_longFrameCount >= DeadlockConfirmFrames || _deadlockAccumulatedSeconds >= DeadlockConfirmSeconds)
            {
                _deadlockDetected = true;
                return true;
            }

            return false;
        }

        private static void ResetDeadlockDetectorCold()
        {
            _longFrameCount = 0;
            _deadlockAccumulatedSeconds = 0.0d;
            _maxEditorFrameSeconds = 0.0d;
            _deadlockDetected = false;
        }

        private static void DeclareDeadlock()
        {
            if (!_deadlockDetected)
                _deadlockDetected = true;

            CaptureDeadlockSnapshotCold(out _lastDeadlockSnapshot);

            RequestStop(WatchdogFailureCode.Deadlock, "DEADLOCK_CONFIRMED");

            if (Application.isBatchMode)
            {
                StopProfilerRecorders();
                EditorApplication.Exit((int)WatchdogFailureCode.Deadlock);
            }
        }

        private static void TryStartPlayMode()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (!SessionState.GetBool(StartDelayLoggedKey, false))
                {
                    AppendSupervisorLog("START_DELAYED_EDITOR_BUSY");
                    SessionState.SetBool(StartDelayLoggedKey, true);
                }

                return;
            }

            SessionState.SetBool(StartDelayLoggedKey, false);
            if (!File.Exists(BootstrapScenePath))
            {
                RequestStop(WatchdogFailureCode.BootstrapSceneMissing, "BOOTSTRAP_SCENE_MISSING");
                return;
            }

            try
            {
                EditorSceneManager.OpenScene(BootstrapScenePath);
                SessionState.SetBool(PlayModeStartIssuedKey, true);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                RequestStop(WatchdogFailureCode.BootstrapLoadFailed, "BOOTSTRAP_LOAD_FAILED_" + exception.GetType().Name);
            }
        }

        private static void RequestStop(WatchdogFailureCode code, string reason)
        {
            int numericCode = (int)code;
            if (_failureCode == 0)
            {
                _failureCode = numericCode;
                SessionState.SetInt(FailureCodeKey, numericCode);
                SessionState.SetString(FailureTextKey, reason ?? string.Empty);
            }

            SessionState.SetBool(WaitingFinalizeKey, true);
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                FinalizeRun();
        }

        private static void FinalizeRun()
        {
            SessionState.SetBool(WaitingFinalizeKey, false);
            SessionState.SetBool(PlayModeStartIssuedKey, false);
            SessionState.SetBool(PlayModeObservedKey, false);
            SessionState.SetBool(StartDelayLoggedKey, false);
            _failureCode = SessionState.GetInt(FailureCodeKey, _failureCode);

            PollEditorLogCold();
            FlushPartialLogLine();
            PollCsvCold();
            FlushPartialCsvLine();
            StopProfilerRecorders();

            if (_csvTerminalFailed && _failureCode == 0)
                _failureCode = (int)WatchdogFailureCode.WatchdogRuntimeFailed;

            _leakCheckPassed = ValidateNativeLeaksCold(out _nativeActiveAllocations, out _nativeTrackedBytes);

            if (!_leakCheckPassed && _failureCode == 0)
                _failureCode = (int)WatchdogFailureCode.NativeLeak;

            if (_homeostasisUnproven && _failureCode == 0)
                _failureCode = (int)WatchdogFailureCode.HomeostasisUnproven;

            TryDeleteFile(WatchdogFlagPath);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(PlayModeStartIssuedKey, false);
            SessionState.SetBool(PlayModeObservedKey, false);
            SessionState.SetBool(StartDelayLoggedKey, false);
            DetachUpdate();
            AppendSupervisorLog("FINALIZED");
        }

        private static void PollGcRecorder()
        {
            if (!_lastRowWasSimulation || !_gcAllocRecorder.Valid)
                return;

            long lastValue = _gcAllocRecorder.LastValue;
            if (lastValue > 0L)
            {
                _totalGcBytes += lastValue;
                RequestStop(WatchdogFailureCode.GcAllocInSimulation, "GC_ALLOC_IN_SIMULATION");
            }
        }

        private static void PollEditorLogCold()
        {
            string path = ResolveEditorLogPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long length = stream.Length;
                    if (_editorLogOffset > length)
                        _editorLogOffset = 0L;

                    stream.Position = _editorLogOffset;
                    int read;
                    while ((read = stream.Read(LogReadBuffer, 0, LogReadBuffer.Length)) > 0)
                    {
                        _editorLogOffset += read;
                        for (int i = 0; i < read; i++)
                            AppendLogByte(LogReadBuffer[i]);
                    }
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }

        private static void AppendLogByte(byte value)
        {
            if (value == (byte)'\r')
                return;

            if (value == (byte)'\n')
            {
                EvaluateLogLine(LogLineBuffer, _logLineLength);
                _logLineLength = 0;
                return;
            }

            if (_logLineLength < LogLineBuffer.Length)
                LogLineBuffer[_logLineLength++] = value;
        }

        private static void FlushPartialLogLine()
        {
            if (_logLineLength <= 0)
                return;

            EvaluateLogLine(LogLineBuffer, _logLineLength);
            _logLineLength = 0;
        }

        private static void EvaluateLogLine(byte[] line, int count)
        {
            int lineNumber;
            int failureCode;
            if (!TryParseLogLineForFailure(line, count, out failureCode, out lineNumber))
                return;

            _logFailureCode = failureCode;
            _logFailureLine = lineNumber;
            RequestStop((WatchdogFailureCode)failureCode, "EDITOR_LOG_FAILURE");
        }

        private static bool TryParseLogLineForFailure(byte[] line, int count, out int failureCode, out int sourceLineNumber)
        {
            sourceLineNumber = ExtractLineNumber(line, count);

            if (IndexOf(line, count, PatternCsError) >= 0)
            {
                failureCode = (int)WatchdogFailureCode.CompileError;
                return true;
            }

            if (IndexOf(line, count, PatternNullReference) >= 0)
            {
                failureCode = (int)WatchdogFailureCode.NullReference;
                return true;
            }

            if (IndexOf(line, count, PatternIndexOutOfRange) >= 0)
            {
                failureCode = (int)WatchdogFailureCode.IndexOutOfRange;
                return true;
            }

            if (IndexOf(line, count, PatternAccessViolation) >= 0)
            {
                failureCode = (int)WatchdogFailureCode.AccessViolation;
                return true;
            }

            if (IndexOf(line, count, PatternFatalLeak) >= 0)
            {
                failureCode = (int)WatchdogFailureCode.NativeLeak;
                return true;
            }

            failureCode = (int)WatchdogFailureCode.None;
            return false;
        }

        private static int ExtractLineNumber(byte[] line, int count)
        {
            int lineSuffix = IndexOf(line, count, PatternLineSuffix);
            if (lineSuffix >= 0)
                return ParsePositiveInt(line, lineSuffix + PatternLineSuffix.Length, count);

            for (int i = 0; i + 4 < count; i++)
            {
                if (line[i] != (byte)'.' || line[i + 1] != (byte)'c' || line[i + 2] != (byte)'s')
                    continue;

                if (line[i + 3] == (byte)':')
                    return ParsePositiveInt(line, i + 4, count);

                if (line[i + 3] == (byte)'(')
                    return ParsePositiveInt(line, i + 4, count);
            }

            return -1;
        }

        private static void PollCsvCold()
        {
            if (!File.Exists(WatchdogCsvPath))
                return;

            try
            {
                using (FileStream stream = new FileStream(WatchdogCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long length = stream.Length;
                    if (_csvOffset > length)
                    {
                        _csvOffset = 0L;
                        ResetCsvAccumulator();
                    }

                    stream.Position = _csvOffset;
                    int read;
                    while ((read = stream.Read(CsvReadBuffer, 0, CsvReadBuffer.Length)) > 0)
                    {
                        _csvOffset += read;
                        for (int i = 0; i < read; i++)
                            AppendCsvByte(CsvReadBuffer[i]);
                    }
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }

        private static void AppendCsvByte(byte value)
        {
            if (value == (byte)'\r')
                return;

            if (value == (byte)'\n')
            {
                EvaluateCsvLine(CsvLineBuffer, _csvLineLength);
                _csvLineLength = 0;
                return;
            }

            if (_csvLineLength < CsvLineBuffer.Length)
                CsvLineBuffer[_csvLineLength++] = value;
        }

        private static void EvaluateCsvLine(byte[] line, int count)
        {
            if (count <= 0)
                return;

            if (!_csvHeaderParsed)
            {
                ParseCsvHeader(line, count);
                _csvHeaderParsed = true;
                return;
            }

            AccumulateCsvRow(line, count);
        }

        private static void ParseCsvHeader(byte[] line, int count)
        {
            _frameTimeColumn = FindColumnIndex(line, count, PatternFrameTime, 2);
            _stateColumn = FindColumnIndex(line, count, PatternState, 1);
            _gcColumn = FindColumnIndex(line, count, PatternGc, 3);
            _vramColumn = FindColumnIndex(line, count, PatternVram, 4);
            _distanceColumn = FindColumnIndex(line, count, PatternDistance, 13);
            _failReasonCodeColumn = FindColumnIndex(line, count, PatternFailReasonCode, 11);
            _rollingP95Column = FindColumnIndex(line, count, PatternRollingP95, 14);
            _foveationColumn = FindColumnIndex(line, count, PatternFoveationLevel, -1);
            if (_foveationColumn < 0)
                _foveationColumn = FindColumnIndex(line, count, PatternFoveation01, -1);
            _mipmapColumn = FindColumnIndex(line, count, PatternMipmapLimit, -1);
            if (_mipmapColumn < 0)
                _mipmapColumn = FindColumnIndex(line, count, PatternTextureMipmapLimit, -1);
            _qualityWeightColumn = FindColumnIndex(line, count, PatternQualityWeight, -1);
        }

        private static void AccumulateCsvRow(byte[] line, int count)
        {
            double frameMs;
            if (TryParseDoubleField(line, count, _frameTimeColumn, out frameMs))
                AddFrameTimeSample(frameMs);

            long gcBytes;
            if (TryParseLongField(line, count, _gcColumn, out gcBytes))
            {
                if (gcBytes > 0L)
                    _totalGcBytes += gcBytes;
            }

            long vramMb;
            if (TryParseLongField(line, count, _vramColumn, out vramMb))
            {
                if (vramMb > _peakVramMegabytes)
                    _peakVramMegabytes = (int)vramMb;

                if (vramMb > TargetVramMegabytes)
                    AuditVramResponse(line, count);
            }

            double distance;
            if (TryParseDoubleField(line, count, _distanceColumn, out distance))
                _lastDistanceMeters = distance;

            double rollingP95;
            if (TryParseDoubleField(line, count, _rollingP95Column, out rollingP95))
                _lastRollingP95Milliseconds = rollingP95;

            long failReasonCode;
            if (TryParseLongField(line, count, _failReasonCodeColumn, out failReasonCode))
                _finalFailReasonCode = (int)failReasonCode;

            _lastRowWasSimulation = FieldEquals(line, count, _stateColumn, PatternSimulation);
            if (_lastRowWasSimulation)
                _simulationSampleCount++;

            bool completed = FieldEquals(line, count, _stateColumn, PatternCompleted);
            bool failed = FieldEquals(line, count, _stateColumn, PatternFailed);
            if (completed || failed)
            {
                _terminalObserved = true;
                if (failed)
                    _csvTerminalFailed = true;
            }

            _sampleCount++;
        }

        private static void AuditVramResponse(byte[] line, int count)
        {
            _vramOverBudgetSamples++;

            bool hasResponseColumn = false;
            bool responseActive = false;
            double foveation;
            double mip;
            double quality;

            if (_foveationColumn >= 0 && TryParseDoubleField(line, count, _foveationColumn, out foveation))
            {
                hasResponseColumn = true;
                responseActive |= foveation > 0.01d;
            }

            if (_mipmapColumn >= 0 && TryParseDoubleField(line, count, _mipmapColumn, out mip))
            {
                hasResponseColumn = true;
                responseActive |= mip > 0.01d;
            }

            if (_qualityWeightColumn >= 0 && TryParseDoubleField(line, count, _qualityWeightColumn, out quality))
            {
                hasResponseColumn = true;
                responseActive |= quality < 0.99d;
            }

            if (responseActive)
                _vramResponseSamples++;

            if (!hasResponseColumn || !responseActive)
                _homeostasisUnproven = true;
        }

        private static int FindColumnIndex(byte[] line, int count, byte[] name, int fallback)
        {
            int column = 0;
            int start = 0;
            for (int i = 0; i <= count; i++)
            {
                if (i < count && line[i] != (byte)',')
                    continue;

                int length = i - start;
                if (FieldMatches(line, start, length, name))
                    return column;

                column++;
                start = i + 1;
            }

            return fallback;
        }

        private static bool TryParseDoubleField(byte[] line, int count, int column, out double value)
        {
            int start;
            int length;
            if (!TryGetFieldBounds(line, count, column, out start, out length))
            {
                value = 0.0d;
                return false;
            }

            return TryParseDouble(line, start, length, out value);
        }

        private static bool TryParseLongField(byte[] line, int count, int column, out long value)
        {
            int start;
            int length;
            if (!TryGetFieldBounds(line, count, column, out start, out length))
            {
                value = 0L;
                return false;
            }

            return TryParseLong(line, start, length, out value);
        }

        private static bool FieldEquals(byte[] line, int count, int column, byte[] pattern)
        {
            int start;
            int length;
            if (!TryGetFieldBounds(line, count, column, out start, out length))
                return false;

            return FieldMatches(line, start, length, pattern);
        }

        private static bool TryGetFieldBounds(byte[] line, int count, int column, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (column < 0)
                return false;

            int currentColumn = 0;
            int currentStart = 0;
            for (int i = 0; i <= count; i++)
            {
                if (i < count && line[i] != (byte)',')
                    continue;

                if (currentColumn == column)
                {
                    start = currentStart;
                    length = i - currentStart;
                    return true;
                }

                currentColumn++;
                currentStart = i + 1;
            }

            return false;
        }

        private static bool FieldMatches(byte[] line, int start, int length, byte[] pattern)
        {
            if (pattern == null || length != pattern.Length)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (line[start + i] != pattern[i])
                    return false;
            }

            return true;
        }

        private static bool TryParseLong(byte[] line, int start, int length, out long value)
        {
            value = 0L;
            if (length <= 0)
                return false;

            int index = start;
            int end = start + length;
            bool negative = false;
            if (line[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            long result = 0L;
            bool foundDigit = false;
            for (int i = index; i < end; i++)
            {
                byte b = line[i];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                result = (result * 10L) + (b - (byte)'0');
                foundDigit = true;
            }

            if (!foundDigit)
                return false;

            value = negative ? -result : result;
            return true;
        }

        private static bool TryParseDouble(byte[] line, int start, int length, out double value)
        {
            value = 0.0d;
            if (length <= 0)
                return false;

            int index = start;
            int end = start + length;
            bool negative = false;
            if (line[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            double whole = 0.0d;
            bool foundDigit = false;
            while (index < end)
            {
                byte b = line[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                whole = (whole * 10.0d) + (b - (byte)'0');
                foundDigit = true;
                index++;
            }

            double fraction = 0.0d;
            double divisor = 1.0d;
            if (index < end && line[index] == (byte)'.')
            {
                index++;
                while (index < end)
                {
                    byte b = line[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fraction = (fraction * 10.0d) + (b - (byte)'0');
                    divisor *= 10.0d;
                    foundDigit = true;
                    index++;
                }
            }

            if (!foundDigit)
                return false;

            double result = whole + (fraction / divisor);
            value = negative ? -result : result;
            return true;
        }

        private static int ParsePositiveInt(byte[] line, int start, int count)
        {
            int value = 0;
            bool foundDigit = false;
            for (int i = start; i < count; i++)
            {
                byte b = line[i];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                value = (value * 10) + (b - (byte)'0');
                foundDigit = true;
            }

            return foundDigit ? value : -1;
        }

        private static int IndexOf(byte[] source, int count, byte[] pattern)
        {
            if (source == null || pattern == null || pattern.Length == 0 || count < pattern.Length)
                return -1;

            int limit = count - pattern.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool matched = true;
                for (int p = 0; p < pattern.Length; p++)
                {
                    if (source[i + p] == pattern[p])
                        continue;

                    matched = false;
                    break;
                }

                if (matched)
                    return i;
            }

            return -1;
        }

        private static void AddFrameTimeSample(double frameMs)
        {
            if (frameMs < 0.0d)
                frameMs = 0.0d;

            int microseconds = (int)(frameMs * 1000.0d);
            int bucket = microseconds / HistogramBucketMicroseconds;
            if (bucket < 0)
                bucket = 0;
            if (bucket >= FrameTimeBuckets.Length)
                bucket = FrameTimeBuckets.Length - 1;

            FrameTimeBuckets[bucket]++;
        }

        private static double CalculateP95Milliseconds()
        {
            if (_sampleCount <= 0)
                return 0.0d;

            int target = (int)Math.Ceiling(_sampleCount * 0.95d);
            int accumulated = 0;
            for (int i = 0; i < FrameTimeBuckets.Length; i++)
            {
                accumulated += FrameTimeBuckets[i];
                if (accumulated >= target)
                    return (i * HistogramBucketMicroseconds) / 1000.0d;
            }

            return ((FrameTimeBuckets.Length - 1) * HistogramBucketMicroseconds) / 1000.0d;
        }

        private static bool ValidateNativeLeaksCold(out int activeAllocationCount, out long trackedBytes)
        {
            activeAllocationCount = 0;
            trackedBytes = 0L;

            Type sentinelType = ResolveType("Hecton8.Core.NativeMemorySentinel");
            if (sentinelType == null)
                return false;

            PropertyInfo activeProperty = sentinelType.GetProperty("ActiveAllocationCount", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo bytesProperty = sentinelType.GetProperty("TrackedBytes", BindingFlags.Public | BindingFlags.Static);
            if (activeProperty != null)
                activeAllocationCount = Convert.ToInt32(activeProperty.GetValue(null, null), CultureInfo.InvariantCulture);
            if (bytesProperty != null)
                trackedBytes = Convert.ToInt64(bytesProperty.GetValue(null, null), CultureInfo.InvariantCulture);

            try
            {
                MethodInfo validate = sentinelType.GetMethod("ValidateZeroLeaks", BindingFlags.Public | BindingFlags.Static);
                if (validate != null)
                {
                    object result = validate.Invoke(null, null);
                    if (result is bool)
                        return (bool)result;
                    return activeAllocationCount == 0 && trackedBytes == 0L;
                }

                MethodInfo assert = sentinelType.GetMethod("AssertNoAllocationsAfterServiceShutdown", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (assert != null)
                {
                    object result = assert.Invoke(null, new object[] { "WatchdogSupervisor1620" });
                    if (result is bool)
                        return (bool)result;
                    return activeAllocationCount == 0 && trackedBytes == 0L;
                }
            }
            catch (TargetInvocationException)
            {
                if (activeProperty != null)
                    activeAllocationCount = Convert.ToInt32(activeProperty.GetValue(null, null), CultureInfo.InvariantCulture);
                if (bytesProperty != null)
                    trackedBytes = Convert.ToInt64(bytesProperty.GetValue(null, null), CultureInfo.InvariantCulture);
                return false;
            }

            return activeAllocationCount == 0 && trackedBytes == 0L;
        }

        private static bool CaptureDeadlockSnapshotCold(out DeadlockSnapshot1620 snapshot)
        {
            snapshot = default;
            snapshot.Version = DeadlockSnapshotVersion;
            snapshot.LongFrameCount = _longFrameCount;
            snapshot.AccumulatedSeconds = _deadlockAccumulatedSeconds;
            snapshot.MaxEditorFrameSeconds = _maxEditorFrameSeconds;
            snapshot.FailureCode = _failureCode;

            Type vaultType = ResolveType("Hecton8.Core.Memory.GlobalDataVault");
            if (vaultType == null)
                return false;

            MethodInfo tryGet = vaultType.GetMethod("TryGetLatestCreated", BindingFlags.Public | BindingFlags.Static);
            if (tryGet == null)
                return false;

            object[] args = { null };
            bool resolved = false;
            try
            {
                object result = tryGet.Invoke(null, args);
                if (result is bool)
                    resolved = (bool)result;
            }
            catch (TargetInvocationException)
            {
                resolved = false;
            }

            object vault = resolved ? args[0] : null;
            if (vault == null)
                return false;

            snapshot.VaultResolved = 1;
            snapshot.IsAllocationLocked = ReadBoolProperty(vaultType, vault, "IsAllocationLocked") ? 1 : 0;
            snapshot.IsCompactionFenceActive = ReadBoolProperty(vaultType, vault, "IsCompactionFenceActive") ? 1 : 0;
            snapshot.ActiveBurstLockMask = ReadLongProperty(vaultType, vault, "ActiveBurstLockMask");
            snapshot.ActiveMutationGuardMask = ReadLongProperty(vaultType, vault, "ActiveMutationGuardMask");
            snapshot.TotalFreeSpaceBytes = ReadLongProperty(vaultType, vault, "TotalFreeSpaceBytes");
            snapshot.LargestContiguousBlockBytes = ReadLongProperty(vaultType, vault, "LargestContiguousBlockBytes");
            snapshot.PendingMassiveMoveBytes = ReadLongProperty(vaultType, vault, "PendingMassiveMoveBytes");
            snapshot.DeferredArenaGrowthBytes = ReadLongProperty(vaultType, vault, "DeferredArenaGrowthBytes");
            snapshot.CompactionWatchdogBreachCount = ReadLongProperty(vaultType, vault, "CompactionWatchdogBreachCount");
            snapshot.VaultGenerationID = ReadLongProperty(vaultType, vault, "VaultGenerationID");
            return true;
        }

        private static bool ReadBoolProperty(Type type, object instance, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                object raw = property.GetValue(instance, null);
                if (raw is bool)
                    return (bool)raw;
            }

            return false;
        }

        private static long ReadLongProperty(Type type, object instance, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                object raw = property.GetValue(instance, null);
                if (raw is long)
                    return (long)raw;
                if (raw is int)
                    return (int)raw;
                if (raw is uint)
                    return (uint)raw;
                if (raw is ulong)
                    return unchecked((long)(ulong)raw);
                if (raw != null)
                {
                    try
                    {
                        return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                    }
                    catch (OverflowException)
                    {
                        return 0L;
                    }
                    catch (InvalidCastException)
                    {
                        return 0L;
                    }
                }
            }

            return 0L;
        }

        private static void StartProfilerRecorders()
        {
            StopProfilerRecorders();
            try
            {
                _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            }
            catch (Exception exception)
            {
                AppendSupervisorLog("GC_RECORDER_UNAVAILABLE " + exception.GetType().Name);
            }
        }

        private static void StopProfilerRecorders()
        {
            if (_gcAllocRecorder.Valid)
                _gcAllocRecorder.Dispose();
        }

        private static void ResetRunState()
        {
            _editorLogOffset = 0L;
            _csvOffset = 0L;
            _logLineLength = 0;
            _csvLineLength = 0;
            _logFailureCode = 0;
            _logFailureLine = -1;
            _failureCode = 0;
            _finalFailReasonCode = 0;
            _longFrameCount = 0;
            _deadlockAccumulatedSeconds = 0.0d;
            _maxEditorFrameSeconds = 0.0d;
            _lastEditorUpdateTime = EditorApplication.timeSinceStartup;
            _nextPollTime = _lastEditorUpdateTime;
            _terminalObserved = false;
            _deadlockDetected = false;
            _homeostasisUnproven = false;
            _leakCheckPassed = false;
            _nativeTrackedBytes = 0L;
            _nativeActiveAllocations = 0;
            ResetCsvAccumulator();
        }

        private static void ResetCsvAccumulator()
        {
            for (int i = 0; i < FrameTimeBuckets.Length; i++)
                FrameTimeBuckets[i] = 0;

            _sampleCount = 0;
            _simulationSampleCount = 0;
            _peakVramMegabytes = 0;
            _vramOverBudgetSamples = 0;
            _vramResponseSamples = 0;
            _foveationColumn = -1;
            _mipmapColumn = -1;
            _qualityWeightColumn = -1;
            _frameTimeColumn = 2;
            _stateColumn = 1;
            _gcColumn = 3;
            _vramColumn = 4;
            _distanceColumn = 13;
            _failReasonCodeColumn = 11;
            _rollingP95Column = 14;
            _csvHeaderParsed = false;
            _lastRowWasSimulation = false;
            _csvTerminalFailed = false;
            _totalGcBytes = 0L;
            _lastDistanceMeters = 0.0d;
            _lastRollingP95Milliseconds = 0.0d;
            _terminalObserved = false;
            _homeostasisUnproven = false;
        }

        private static void FlushPartialCsvLine()
        {
            if (_csvLineLength <= 0)
                return;

            EvaluateCsvLine(CsvLineBuffer, _csvLineLength);
            _csvLineLength = 0;
        }

        private static void RestoreSessionState()
        {
            long.TryParse(SessionState.GetString(EditorLogOffsetKey, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _editorLogOffset);
            long.TryParse(SessionState.GetString(CsvOffsetKey, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _csvOffset);
            _failureCode = SessionState.GetInt(FailureCodeKey, 0);
            _lastEditorUpdateTime = EditorApplication.timeSinceStartup;
            _nextPollTime = _lastEditorUpdateTime;
        }

        private static void PersistOffsets()
        {
            SessionState.SetString(EditorLogOffsetKey, _editorLogOffset.ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(CsvOffsetKey, _csvOffset.ToString(CultureInfo.InvariantCulture));
        }

        private static string ResolveEditorLogPath()
        {
            string path = Application.consoleLogPath;
            if (!string.IsNullOrEmpty(path))
                return path;

            string localAppData = global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.LocalApplicationData);
            if (Application.platform == RuntimePlatform.WindowsEditor)
                return Path.Combine(localAppData, "Unity", "Editor", "Editor.log");

            return string.Empty;
        }

        private static long ResolveExistingLength(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0L;

            try
            {
                FileInfo info = new FileInfo(path);
                return info.Length;
            }
            catch (IOException)
            {
                return 0L;
            }
            catch (UnauthorizedAccessException)
            {
                return 0L;
            }
        }

        private static Type ResolveType(string fullName)
        {
            global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void AppendSupervisorLog(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SupervisorLogPath));
                File.AppendAllText(SupervisorLogPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + message + global::System.Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public struct DeadlockSnapshot1620
        {
            public int Version;
            public int LongFrameCount;
            public int FailureCode;
            public int VaultResolved;
            public int IsAllocationLocked;
            public int IsCompactionFenceActive;
            public long ActiveBurstLockMask;
            public long ActiveMutationGuardMask;
            public long TotalFreeSpaceBytes;
            public long LargestContiguousBlockBytes;
            public long PendingMassiveMoveBytes;
            public long DeferredArenaGrowthBytes;
            public long CompactionWatchdogBreachCount;
            public long VaultGenerationID;
            public double AccumulatedSeconds;
            public double MaxEditorFrameSeconds;
        }

        private enum WatchdogFailureCode
        {
            None = 0,
            CompileError = 10,
            NullReference = 11,
            IndexOutOfRange = 12,
            AccessViolation = 13,
            NativeLeak = 14,
            Deadlock = 20,
            GcAllocInSimulation = 30,
            HomeostasisUnproven = 40,
            BootstrapSceneMissing = 50,
            BootstrapLoadFailed = 51,
            Timeout = 60,
            WatchdogRuntimeFailed = 61,
            PlayModeExitedUnexpectedly = 62,
            ManualStop = 70
        }
    }
}
#endif
