#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.QA.Editor
{
    [InitializeOnLoad]
    public static class QAWatchdogBatchRunner1524
    {
        private const string ActiveKey = "H8.QA.Watchdog1524.Active";
        private const string StartTimeKey = "H8.QA.Watchdog1524.StartTime";
        private const string ExitRequestedKey = "H8.QA.Watchdog1524.ExitRequested";
        private const string ExitCodeKey = "H8.QA.Watchdog1524.ExitCode";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string FlagRelativePath = "Temp/H8_QA_WATCHDOG_1524.flag";
        private const string LegacyEnduranceFlagRelativePath = "Temp/H8_QA_ENDURANCE_10KM.flag";
        private const string CsvRelativePath = "Docs/Reports/QA_WATCHDOG_ENDURANCE_REPORT_1524.csv";
        private const string RunnerStatusRelativePath = "Docs/AgentLogs/QAWatchdogBatchRunner_1524.txt";
        private const double TimeoutSeconds = 7200.0;
        private const double PollIntervalSeconds = 0.25;
        private const int CsvReadBufferSize = 8192;
        private const int CsvLineBufferSize = 4096;
        private static readonly byte[] FlagBytes = { (byte)'1' }; // COLD ALLOC: batch flag payload - owner: QAWatchdogBatchRunner1524
        private static readonly byte[] CsvReadBuffer = new byte[CsvReadBufferSize];
        private static readonly byte[] CsvLineBuffer = new byte[CsvLineBufferSize];
        private static readonly byte[] CsvCompletedPattern = Encoding.ASCII.GetBytes(",Completed,");
        private static readonly byte[] CsvFailedPattern = Encoding.ASCII.GetBytes(",Failed,");
        private static double _nextPollTime;
        private static long _csvReadOffset;
        private static int _csvPendingLineLength;
        private static bool _csvPendingLineOverflow;

        static QAWatchdogBatchRunner1524()
        {
            if (SessionState.GetBool(ActiveKey, false))
                Attach();
        }

        [MenuItem("Hecton8/QA/1524/Run Watchdog 10KM", false, 15230)]
        public static void RunMenu()
        {
            Run();
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitRequestedKey, false);
            SessionState.SetString(StartTimeKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            _nextPollTime = 0.0;
            ResetCsvTailParser();

            TryDeleteFile(ResolveProjectPath(CsvRelativePath));
            TryDeleteFile(ResolveProjectPath(FlagRelativePath));
            TryDeleteFile(ResolveProjectPath(LegacyEnduranceFlagRelativePath));
            if (!TryWriteFlagFile())
            {
                RequestStop(1, "flag_write_failed");
                return;
            }

            WriteRunnerStatus("started");
            Attach();

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (!TryEnsureBootstrapScene())
            {
                RequestStop(1, "bootstrap_scene_unavailable");
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = true;
        }

        private static void Attach()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Detach()
        {
            EditorApplication.update -= Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                Detach();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (!ShouldPollNow())
                return;

            PollRunState();
        }

        private static void PollRunState()
        {
            string csvPath = ResolveProjectPath(CsvRelativePath);
            if (File.Exists(csvPath))
            {
                if (TryResolveExitCode(csvPath, out int exitCode, out string status))
                    RequestStop(exitCode, status);
                return;
            }

            if (HasTimedOut())
            {
                RequestStop(2, "timeout");
                return;
            }

            if (SessionState.GetBool(ExitRequestedKey, false))
            {
                CompleteAfterPlayStopped(SessionState.GetInt(ExitCodeKey, 1));
                return;
            }

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!TryEnsureBootstrapScene())
                {
                    RequestStop(1, "bootstrap_scene_unavailable");
                    return;
                }

                EditorApplication.isPlaying = true;
            }
        }

        private static bool ShouldPollNow()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPollTime)
                return false;

            _nextPollTime = now + PollIntervalSeconds;
            return true;
        }

        private static bool HasTimedOut()
        {
            string raw = SessionState.GetString(StartTimeKey, "0");
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double startTime))
                startTime = EditorApplication.timeSinceStartup;

            return EditorApplication.timeSinceStartup - startTime > TimeoutSeconds;
        }

        private static void RequestStop(int exitCode, string status)
        {
            WriteRunnerStatus(status);
            TryDeleteFile(ResolveProjectPath(FlagRelativePath));
            SessionState.SetInt(ExitCodeKey, exitCode);
            SessionState.SetBool(ExitRequestedKey, true);

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            CompleteAfterPlayStopped(exitCode);
        }

        private static void CompleteAfterPlayStopped(int exitCode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitRequestedKey, false);
            Detach();
            WriteRunnerStatus(exitCode == 0 ? "exit_0" : "exit_nonzero");
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        private static bool TryResolveExitCode(string csvPath, out int exitCode, out string status)
        {
            exitCode = 1;
            status = "runtime_fault";
            try
            {
                bool sawTerminal = false;

                using (FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (_csvReadOffset > stream.Length)
                        ResetCsvTailParser();

                    stream.Position = _csvReadOffset;
                    int bytesRead;
                    while ((bytesRead = stream.Read(CsvReadBuffer, 0, CsvReadBuffer.Length)) > 0)
                    {
                        for (int i = 0; i < bytesRead; i++)
                        {
                            byte value = CsvReadBuffer[i];
                            if (value == (byte)'\n' || value == (byte)'\r')
                            {
                                if (!_csvPendingLineOverflow && _csvPendingLineLength > 0)
                                    ConsumeCsvLine(_csvPendingLineLength, ref sawTerminal, ref exitCode, ref status);

                                _csvPendingLineLength = 0;
                                _csvPendingLineOverflow = false;
                                continue;
                            }

                            if (_csvPendingLineOverflow)
                                continue;

                            if (_csvPendingLineLength >= CsvLineBuffer.Length)
                            {
                                _csvPendingLineOverflow = true;
                                continue;
                            }

                            CsvLineBuffer[_csvPendingLineLength++] = value;
                        }
                    }

                    _csvReadOffset = stream.Position;
                }

                if (!_csvPendingLineOverflow && _csvPendingLineLength > 0)
                    ConsumeCsvLine(_csvPendingLineLength, ref sawTerminal, ref exitCode, ref status);

                return sawTerminal;
            }
            catch (IOException)
            {
                WriteRunnerStatus("csv_read_pending");
                status = "csv_read_pending";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                WriteRunnerStatus("csv_read_pending");
                status = "csv_read_pending";
                return false;
            }
        }

        private static void ResetCsvTailParser()
        {
            _csvReadOffset = 0L;
            _csvPendingLineLength = 0;
            _csvPendingLineOverflow = false;
        }

        private static void ConsumeCsvLine(int lineLength, ref bool sawTerminal, ref int exitCode, ref string status)
        {
            if (ContainsBytes(CsvLineBuffer, lineLength, CsvCompletedPattern))
            {
                exitCode = 0;
                status = "completed";
                sawTerminal = true;
                return;
            }

            if (ContainsBytes(CsvLineBuffer, lineLength, CsvFailedPattern))
            {
                exitCode = 1;
                status = ResolveFailedStatus(CsvLineBuffer, lineLength);
                sawTerminal = true;
            }
        }

        private static string ResolveFailedStatus(byte[] csvLine, int length)
        {
            if (!TryGetCsvFieldBounds(csvLine, length, 12, out int fieldStart, out int fieldLength))
                return "runtime_fault";

            TrimCsvField(csvLine, ref fieldStart, ref fieldLength);
            if (fieldLength <= 0)
                return "runtime_fault";

            return "runtime_fault_" + Encoding.ASCII.GetString(csvLine, fieldStart, fieldLength);
        }

        private static bool ContainsBytes(byte[] source, int length, byte[] pattern)
        {
            if (pattern.Length == 0 || length < pattern.Length)
                return false;

            int lastStart = length - pattern.Length;
            for (int i = 0; i <= lastStart; i++)
            {
                int j = 0;
                while (j < pattern.Length && source[i + j] == pattern[j])
                    j++;

                if (j == pattern.Length)
                    return true;
            }

            return false;
        }

        private static bool TryGetCsvFieldBounds(byte[] line, int length, int fieldIndex, out int fieldStart, out int fieldLength)
        {
            int currentField = 0;
            int currentStart = 0;

            for (int i = 0; i <= length; i++)
            {
                if (i < length && line[i] != (byte)',')
                    continue;

                if (currentField == fieldIndex)
                {
                    fieldStart = currentStart;
                    fieldLength = i - currentStart;
                    return true;
                }

                currentField++;
                currentStart = i + 1;
            }

            fieldStart = 0;
            fieldLength = 0;
            return false;
        }

        private static void TrimCsvField(byte[] line, ref int fieldStart, ref int fieldLength)
        {
            while (fieldLength > 0 && (line[fieldStart] == (byte)' ' || line[fieldStart] == (byte)'"'))
            {
                fieldStart++;
                fieldLength--;
            }

            while (fieldLength > 0)
            {
                byte value = line[fieldStart + fieldLength - 1];
                if (value != (byte)' ' && value != (byte)'"')
                    break;

                fieldLength--;
            }
        }

        private static bool TryWriteFlagFile()
        {
            try
            {
                string flagPath = ResolveProjectPath(FlagRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(flagPath));
                File.WriteAllBytes(flagPath, FlagBytes);
                return true;
            }
            catch (Exception)
            {
                WriteRunnerStatus("flag_write_failed");
                return false;
            }
        }

        private static bool TryEnsureBootstrapScene()
        {
            try
            {
                if (!File.Exists(BootstrapScenePath))
                {
                    WriteRunnerStatus("bootstrap_scene_missing");
                    return false;
                }

                string activePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                if (!string.Equals(activePath, BootstrapScenePath, StringComparison.Ordinal))
                    EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

                return true;
            }
            catch (Exception)
            {
                WriteRunnerStatus("bootstrap_scene_open_failed");
                return false;
            }
        }

        private static string ResolveProjectPath(string relativePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteRunnerStatus(string status)
        {
            try
            {
                string path = ResolveProjectPath(RunnerStatusRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.Write(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    writer.Write(' ');
                    writer.Write(status);
                    writer.Write(System.Environment.NewLine);
                }
            }
            catch (Exception)
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
            catch (Exception)
            {
                WriteRunnerStatus("delete_failed");
            }
        }
    }
}
#endif
