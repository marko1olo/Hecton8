#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.QA.Headless.Editor
{
    [InitializeOnLoad]
    public static class HeadlessStressFractureBatchRunner
    {
        private const string ActiveKey = "H8.QA.HeadlessStressFracture.Active";
        private const string StartTimeKey = "H8.QA.HeadlessStressFracture.StartTime";
        private const string ExitRequestedKey = "H8.QA.HeadlessStressFracture.ExitRequested";
        private const string ExitCodeKey = "H8.QA.HeadlessStressFracture.ExitCode";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string FlagRelativePath = "Temp/H8_FRACTURE_TEST.flag";
        private const string ResultRelativePath = "Docs/AgentLogs/HeadlessStressFractureResult_HEADLESS_STRESS_FRACTURE_BOT.json";
        private const string BlackboxRelativePath = "Docs/AgentLogs/Dump_HEADLESS_STRESS_FRACTURE_BOT.bin";
        private const string H8MemoryDumpRelativePath = "Docs/AgentLogs/H8Memory_HEADLESS_STRESS_FRACTURE_BOT.txt";
        private const string RunnerStatusRelativePath = "Docs/AgentLogs/HeadlessStressFractureBatchRunner_HEADLESS_STRESS_FRACTURE_BOT.txt";
        private const double TimeoutSeconds = 7200.0;
        private static readonly byte[] FlagBytes = { (byte)'1' };

        static HeadlessStressFractureBatchRunner()
        {
            if (SessionState.GetBool(ActiveKey, false))
                Attach();
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitRequestedKey, false);
            SessionState.SetString(StartTimeKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            TryDeleteFile(ResolveProjectPath(ResultRelativePath));
            TryDeleteFile(ResolveProjectPath(ResultRelativePath + ".tmp"));
            TryDeleteFile(ResolveProjectPath(BlackboxRelativePath));
            TryDeleteFile(ResolveProjectPath(H8MemoryDumpRelativePath));
            if (!TryWriteFlagFile())
            {
                WriteFallbackResult(1, "FLAG_WRITE_FAILED");
                RequestStop(1, "flag_write_failed");
                return;
            }

            WriteRunnerStatus("started");
            Attach();

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (!TryEnsureBootstrapScene())
            {
                WriteFallbackResult(1, "BOOTSTRAP_SCENE_UNAVAILABLE");
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

            string resultPath = ResolveProjectPath(ResultRelativePath);
            if (File.Exists(resultPath))
            {
                if (TryResolveExitCode(resultPath, out int exitCode))
                    RequestStop(exitCode, exitCode == 0 ? "completed" : "runtime_fault");
                return;
            }

            if (HasTimedOut())
            {
                WriteFallbackResult(2, "BATCH_TIMEOUT");
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
                    WriteFallbackResult(1, "BOOTSTRAP_SCENE_UNAVAILABLE");
                    RequestStop(1, "bootstrap_scene_unavailable");
                    return;
                }

                EditorApplication.isPlaying = true;
            }
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

        private static bool TryResolveExitCode(string resultPath, out int exitCode)
        {
            exitCode = 1;
            try
            {
                string result = File.ReadAllText(resultPath);
                exitCode = result.IndexOf("\"exitCode\":0", StringComparison.Ordinal) >= 0 ? 0 : 1;
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

        private static void WriteFallbackResult(int exitCode, string status)
        {
            try
            {
                string resultPath = ResolveProjectPath(ResultRelativePath);
                if (File.Exists(resultPath))
                    return;

                string tempPath = resultPath + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                using (StreamWriter writer = new StreamWriter(tempPath, false))
                {
                    writer.Write('{');
                    writer.Write("\"agent\":\"HEADLESS_STRESS_FRACTURE_BOT\"");
                    writer.Write(",\"status\":\"");
                    writer.Write(status);
                    writer.Write("\",\"exitCode\":");
                    writer.Write(exitCode.ToString(CultureInfo.InvariantCulture));
                    writer.Write(",\"source\":\"HeadlessStressFractureBatchRunner\"");
                    writer.Write('}');
                }

                if (File.Exists(resultPath))
                {
                    TryDeleteFile(tempPath);
                    return;
                }

                try
                {
                    File.Move(tempPath, resultPath);
                }
                catch (IOException)
                {
                    TryDeleteFile(tempPath);
                }
            }
            catch (Exception)
            {
                WriteRunnerStatus("fallback_result_write_failed");
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
                    writer.Write(Environment.NewLine);
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
