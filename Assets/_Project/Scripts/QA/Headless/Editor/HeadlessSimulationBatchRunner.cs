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
    public static class HeadlessSimulationBatchRunner
    {
        private const string ActiveKey = "H8.QA.Headless.Active";
        private const string StartTimeKey = "H8.QA.Headless.StartTime";
        private const string ExitRequestedKey = "H8.QA.Headless.ExitRequested";
        private const string ExitCodeKey = "H8.QA.Headless.ExitCode";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string FlagRelativePath = "Temp/H8_HEADLESS_SIMULATION.flag";
        private const string CsvRelativePath = "Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv";
        private const string ResultRelativePath = "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json";
        private const string BlackboxRelativePath = "Docs/AgentLogs/Dump_HEADLESS_SIMULATION_RUNNER.bin";
        private const string RunnerStatusRelativePath = "Docs/AgentLogs/HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt";
        // Two hours was long enough that a hung run looked like a working one. This poll loop is the ONLY
        // watchdog that can survive the runtime runner failing to start: the runner's own ColdTick check
        // cannot fire until RegisterRuntimeLanes has succeeded, and in batchmode
        // AwaitableDebtMonitor.NextFrameAsync resolves through Task.Yield() rather than a frame boundary, so
        // the runner's startup wait can park without ever re-evaluating its deadline. When that happened,
        // Application.Quit was a no-op in the Editor and play mode simply carried on running the main menu
        // for 45 minutes with no result file, no CSV rows and no log line.
        //
        // HasTimedOut -> WriteFallbackResult(2, "BATCH_TIMEOUT") -> RequestStop(2, "timeout") was already
        // written and is independent of the runtime runner. It just never got to run. Ten minutes covers a
        // cold Bee compile plus a full 100-day run at the harness's own ~36 real seconds per simulated day,
        // and guarantees an artifact from any future hang. Override per-run if a longer target is needed.
        private const double TimeoutSeconds = 600.0;
        private const double PollIntervalSeconds = 0.25;
        private const int ResultReadBufferSize = 4096;
        // COLD ALLOC: byte[1] - batch flag file payload, editor-only setup path - owner: HeadlessSimulationBatchRunner
        private static readonly byte[] FlagBytes = { (byte)'1' };
        private static readonly byte[] ResultReadBuffer = new byte[ResultReadBufferSize];
        private static readonly byte[] ExitCodeJsonKeyBytes = { (byte)'"', (byte)'e', (byte)'x', (byte)'i', (byte)'t', (byte)'C', (byte)'o', (byte)'d', (byte)'e', (byte)'"' };
        private static double _nextPollTime;

        static HeadlessSimulationBatchRunner()
        {
            if (SessionState.GetBool(ActiveKey, false))
                Attach();
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitRequestedKey, false);
            SessionState.SetString(StartTimeKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            _nextPollTime = 0.0;
            TryDeleteFile(ResolveProjectPath(ResultRelativePath));
            TryDeleteFile(ResolveProjectPath(ResultRelativePath + ".tmp"));
            TryDeleteFile(ResolveProjectPath(CsvRelativePath));
            TryDeleteFile(ResolveProjectPath(BlackboxRelativePath));
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

        private static bool TryResolveExitCode(string resultPath, out int exitCode)
        {
            exitCode = 1;
            try
            {
                int bytesRead;
                using (FileStream stream = new FileStream(resultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    bytesRead = stream.Read(ResultReadBuffer, 0, ResultReadBuffer.Length);
                }

                if (!TryParseExitCode(ResultReadBuffer, bytesRead, out exitCode))
                {
                    WriteRunnerStatus("result_exit_code_invalid");
                    exitCode = 1;
                }

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

        private static bool TryParseExitCode(byte[] result, int length, out int exitCode)
        {
            exitCode = 1;
            if (result == null || length <= 0)
                return false;

            int keyIndex = IndexOf(result, length, ExitCodeJsonKeyBytes, 0);
            if (keyIndex < 0)
                return false;

            int colonIndex = IndexOf(result, length, (byte)':', keyIndex + ExitCodeJsonKeyBytes.Length);
            if (colonIndex < 0)
                return false;

            int valueStart = colonIndex + 1;
            while (valueStart < length && IsJsonWhitespace(result[valueStart]))
                valueStart++;

            int valueEnd = valueStart;
            int sign = 1;
            if (valueEnd < length && (result[valueEnd] == (byte)'-' || result[valueEnd] == (byte)'+'))
            {
                if (result[valueEnd] == (byte)'-')
                    sign = -1;

                valueEnd++;
            }

            int digitStart = valueEnd;
            int parsed = 0;
            while (valueEnd < length && result[valueEnd] >= (byte)'0' && result[valueEnd] <= (byte)'9')
            {
                parsed = (parsed * 10) + (result[valueEnd] - (byte)'0');
                valueEnd++;
            }

            if (valueEnd == digitStart)
                return false;

            exitCode = parsed * sign;
            return true;
        }

        private static int IndexOf(byte[] source, int length, byte[] pattern, int startIndex)
        {
            if (pattern.Length == 0 || length < pattern.Length)
                return -1;

            int lastStart = length - pattern.Length;
            for (int i = startIndex; i <= lastStart; i++)
            {
                int j = 0;
                while (j < pattern.Length && source[i + j] == pattern[j])
                    j++;

                if (j == pattern.Length)
                    return i;
            }

            return -1;
        }

        private static int IndexOf(byte[] source, int length, byte value, int startIndex)
        {
            for (int i = startIndex; i < length; i++)
            {
                if (source[i] == value)
                    return i;
            }

            return -1;
        }

        private static bool IsJsonWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
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
                    writer.Write("\"agent\":\"HEADLESS_SIMULATION_RUNNER\"");
                    writer.Write(",\"status\":\"");
                    writer.Write(status);
                    writer.Write("\",\"exitCode\":");
                    writer.Write(exitCode.ToString(CultureInfo.InvariantCulture));
                    writer.Write(",\"source\":\"HeadlessSimulationBatchRunner\"");
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
