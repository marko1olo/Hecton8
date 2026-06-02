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
        private const string ExitCodeKey = "H8.QA.Endurance.ExitCode";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string FlagRelativePath = "Temp/H8_QA_ENDURANCE_10KM.flag";
        private const string ResultRelativePath = "Docs/AgentLogs/QAEnduranceResult_QA_WATCHDOG_BOT.txt";
        private const string RunnerStatusRelativePath = "Docs/AgentLogs/QAEnduranceBatchRunner_QA_WATCHDOG_BOT.txt";
        private const double TimeoutSeconds = 7200.0;
        private const double PollIntervalSeconds = 0.25;
        private const int ResultReadBufferSize = 4096;
        private static readonly byte[] FlagBytes = { (byte)'1' };
        private static readonly byte[] ResultReadBuffer = new byte[ResultReadBufferSize];
        private static readonly byte[] ExitCodeZeroPattern = { (byte)'e', (byte)'x', (byte)'i', (byte)'t', (byte)'C', (byte)'o', (byte)'d', (byte)'e', (byte)'=', (byte)'0' };
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
            string resultPath = ResolveProjectPath(ResultRelativePath);
            if (File.Exists(resultPath))
            {
                if (TryResolveExitCode(resultPath, out int exitCode))
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
            if (!double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double startTime))
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

        private static bool TryResolveExitCode(string resultPath, out int exitCode)
        {
            exitCode = 1;
            try
            {
                exitCode = FileContainsPattern(resultPath, ExitCodeZeroPattern) ? 0 : 1;
                return true;
            }
            catch (IOException)
            {
                WriteRunnerStatus("result_read_pending");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                WriteRunnerStatus("result_read_pending");
                return false;
            }
        }

        private static bool FileContainsPattern(string path, byte[] pattern)
        {
            int matched = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                int bytesRead;
                while ((bytesRead = stream.Read(ResultReadBuffer, 0, ResultReadBuffer.Length)) > 0)
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        byte value = ResultReadBuffer[i];
                        if (value == pattern[matched])
                        {
                            matched++;
                            if (matched == pattern.Length)
                                return true;
                        }
                        else
                        {
                            matched = value == pattern[0] ? 1 : 0;
                        }
                    }
                }
            }

            return false;
        }

        private static string ResolveProjectPath(string relativePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), relativePath.Replace('/', Path.DirectorySeparatorChar));
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

        private static void WriteRunnerStatus(string status)
        {
            try
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
