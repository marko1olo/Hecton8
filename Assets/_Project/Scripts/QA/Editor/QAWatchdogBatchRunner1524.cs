#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
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
        private static readonly byte[] FlagBytes = { (byte)'1' }; // COLD ALLOC: batch flag payload - owner: QAWatchdogBatchRunner1524
        private static double _nextPollTime;

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
                return;

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
                foreach (string line in File.ReadLines(csvPath))
                {
                    if (line.IndexOf(",Completed,", StringComparison.Ordinal) >= 0)
                    {
                        exitCode = 0;
                        status = "completed";
                        sawTerminal = true;
                    }
                    else if (line.IndexOf(",Failed,", StringComparison.Ordinal) >= 0)
                    {
                        exitCode = 1;
                        status = ResolveFailedStatus(line);
                        sawTerminal = true;
                    }
                }

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

        private static string ResolveFailedStatus(string csvLine)
        {
            string[] columns = csvLine.Split(',');
            if (columns.Length > 12 && !string.IsNullOrEmpty(columns[12]))
                return "runtime_fault_" + columns[12];

            return "runtime_fault";
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
