#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.QA.Editor
{
    [InitializeOnLoad]
    public static class QAEnduranceBatchRunner
    {
        private const string ActiveKey = "H8.QA.Endurance.Active";
        private const string StartTimeKey = "H8.QA.Endurance.StartTime";
        private const string ExitRequestedKey = "H8.QA.Endurance.ExitRequested";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string FlagRelativePath = "Temp/H8_QA_ENDURANCE_10KM.flag";
        private const string ResultRelativePath = "Docs/AgentLogs/QAEnduranceResult_QA_WATCHDOG_BOT.txt";
        private const string RunnerStatusRelativePath = "Docs/AgentLogs/QAEnduranceBatchRunner_QA_WATCHDOG_BOT.txt";
        private const double TimeoutSeconds = 7200.0;
        private const double PollIntervalSeconds = 0.25;
        private static readonly byte[] FlagBytes = { (byte)'1' };
        private static double _nextPollTime;

        static QAEnduranceBatchRunner()
        {
            if (SessionState.GetBool(ActiveKey, false))
                Attach();
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitRequestedKey, false);
            SessionState.SetString(StartTimeKey, EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            _nextPollTime = 0.0;
            TryDeleteFile(ResolveProjectPath(ResultRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(ResolveProjectPath(FlagRelativePath)));
            File.WriteAllBytes(ResolveProjectPath(FlagRelativePath), FlagBytes);
            WriteRunnerStatus("started");
            Attach();

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (File.Exists(BootstrapScenePath))
                EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

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
            string resultPath = ResolveProjectPath(ResultRelativePath);
            if (File.Exists(resultPath))
            {
                int exitCode = ResolveExitCode(resultPath);
                RequestStop(exitCode, exitCode == 0 ? "completed" : "runtime_fault");
                return;
            }

            if (HasTimedOut())
            {
                RequestStop(2, "timeout");
                return;
            }

            if (SessionState.GetBool(ExitRequestedKey, false))
            {
                CompleteAfterPlayStopped(SessionState.GetInt("H8.QA.Endurance.ExitCode", 1));
                return;
            }

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = true;
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
            if (!double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double startTime))
                startTime = EditorApplication.timeSinceStartup;

            return EditorApplication.timeSinceStartup - startTime > TimeoutSeconds;
        }

        private static void RequestStop(int exitCode, string status)
        {
            WriteRunnerStatus(status);
            TryDeleteFile(ResolveProjectPath(FlagRelativePath));
            SessionState.SetInt("H8.QA.Endurance.ExitCode", exitCode);
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

        private static int ResolveExitCode(string resultPath)
        {
            foreach (string line in File.ReadLines(resultPath))
            {
                if (string.Equals(line, "exitCode=0", StringComparison.Ordinal))
                    return 0;
            }

            return 1;
        }

        private static string ResolveProjectPath(string relativePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteRunnerStatus(string status)
        {
            string path = ResolveProjectPath(RunnerStatusRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.Write(DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(status);
                writer.Write(System.Environment.NewLine);
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
#endif
