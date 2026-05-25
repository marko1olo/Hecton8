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
        private const string BlackboxManifestRelativePath = "Docs/AgentLogs/Dump_HEADLESS_STRESS_FRACTURE_BOT.json";
        private const string H8MemoryDumpRelativePath = "Docs/AgentLogs/H8Memory_HEADLESS_STRESS_FRACTURE_BOT.txt";
        private const string RunnerStatusRelativePath = "Docs/AgentLogs/HeadlessStressFractureBatchRunner_HEADLESS_STRESS_FRACTURE_BOT.txt";
        private const string ExitCodeJsonKey = "\"exitCode\"";
        private const string AgentName = "HEADLESS_STRESS_FRACTURE_BOT";
        private const int ResultSchemaVersion = 8;
        private const int BlackboxFrameCapacity = 300;
        private const int BlackboxHeaderSizeBytes = 16;
        private const int BlackboxHeaderOffsetMagic = 0;
        private const int BlackboxHeaderOffsetValidEntryCount = 4;
        private const int BlackboxHeaderOffsetEntrySizeBytes = 8;
        private const int BlackboxHeaderOffsetCursor = 12;
        private const int BlackboxEntrySizeBytes = 64;
        private const int BlackboxEntryOffsetFrame = 0;
        private const int BlackboxEntryOffsetExtremeFrame = 4;
        private const int BlackboxEntryOffsetShiftSequence = 8;
        private const int BlackboxEntryOffsetEventHash = 12;
        private const int BlackboxEntryOffsetNativeBytes = 16;
        private const int BlackboxEntryOffsetH8Bytes = 24;
        private const int BlackboxEntryOffsetNativeAllocations = 32;
        private const int BlackboxEntryOffsetH8Allocations = 36;
        private const int BlackboxEntryOffsetDispatcherPhaseMs = 40;
        private const int BlackboxEntryOffsetDataVaultFragmentation = 44;
        private const int BlackboxEntryOffsetLastShiftMetersX = 48;
        private const int BlackboxEntryOffsetLastShiftMetersY = 52;
        private const int BlackboxEntryOffsetLastShiftMetersZ = 56;
        private const int BlackboxEntryOffsetFlags = 60;
        private const uint BlackboxMagic = 0x48534642u;
        private const double TimeoutSeconds = 7200.0;
        // COLD ALLOC: byte[1] - batch flag file payload, editor-only setup path - owner: HeadlessStressFractureBatchRunner
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
            TryDeleteFile(ResolveProjectPath(BlackboxManifestRelativePath));
            TryDeleteFile(ResolveProjectPath(H8MemoryDumpRelativePath));
            TryDeleteFile(ResolveProjectPath(FlagRelativePath));
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
            {
                WriteRunnerStatus("start_time_invalid");
                return true;
            }

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
                bool parsed = false;
                foreach (string line in File.ReadLines(resultPath))
                {
                    if (!TryParseExitCode(line, out exitCode))
                        continue;

                    parsed = true;
                    break;
                }

                if (!parsed)
                    WriteRunnerStatus("result_exit_code_invalid");

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

        private static bool TryParseExitCode(string result, out int exitCode)
        {
            exitCode = 1;
            if (string.IsNullOrEmpty(result))
                return false;

            int keyIndex = result.IndexOf(ExitCodeJsonKey, StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;

            int colonIndex = result.IndexOf(':', keyIndex + ExitCodeJsonKey.Length);
            if (colonIndex < 0)
                return false;

            int valueStart = colonIndex + 1;
            while (valueStart < result.Length && char.IsWhiteSpace(result[valueStart]))
                valueStart++;

            int valueEnd = valueStart;
            if (valueEnd < result.Length && (result[valueEnd] == '-' || result[valueEnd] == '+'))
                valueEnd++;

            int digitStart = valueEnd;
            while (valueEnd < result.Length && result[valueEnd] >= '0' && result[valueEnd] <= '9')
                valueEnd++;

            if (valueEnd == digitStart)
                return false;

            return int.TryParse(result.AsSpan(valueStart, valueEnd - valueStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out exitCode);
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
                    writer.Write("\"agent\":\"");
                    writer.Write(AgentName);
                    writer.Write('"');
                    writer.Write(",\"resultSchemaVersion\":");
                    WriteInvariant(writer, ResultSchemaVersion);
                    writer.Write(",\"status\":\"");
                    WriteJsonEscaped(writer, status);
                    writer.Write("\",\"exitCode\":");
                    WriteInvariant(writer, exitCode);
                    writer.Write(",\"source\":\"HeadlessStressFractureBatchRunner\"");
                    writer.Write(",\"fallbackResult\":1");
                    writer.Write(",\"blackboxMagic\":");
                    WriteInvariant(writer, BlackboxMagic);
                    writer.Write(",\"blackboxFrameCapacity\":");
                    WriteInvariant(writer, BlackboxFrameCapacity);
                    writer.Write(",\"blackboxHeaderSizeBytes\":");
                    WriteInvariant(writer, BlackboxHeaderSizeBytes);
                    writer.Write(",\"blackboxEntrySizeBytes\":");
                    WriteInvariant(writer, BlackboxEntrySizeBytes);
                    WriteBlackboxHeaderLayout(writer);
                    writer.Write(",\"blackboxManifestRelativePath\":\"");
                    WriteJsonEscaped(writer, BlackboxManifestRelativePath);
                    writer.Write('"');
                    writer.Write(",\"blackboxBinaryRelativePath\":\"");
                    WriteJsonEscaped(writer, BlackboxRelativePath);
                    writer.Write('"');
                    writer.Write(",\"blackboxBinaryDumpSucceeded\":0");
                    writer.Write(",\"blackboxBinaryExistsAfterDump\":0");
                    writer.Write(",\"blackboxManifestDumpSucceeded\":0");
                    WriteBlackboxEntryOffsets(writer);
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

        private static void WriteJsonEscaped(StreamWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        writer.Write("\\\\");
                        break;
                    case '"':
                        writer.Write("\\\"");
                        break;
                    case '\n':
                        writer.Write("\\n");
                        break;
                    case '\r':
                        writer.Write("\\r");
                        break;
                    case '\t':
                        writer.Write("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            writer.Write("\\u00");
                            WriteJsonHexNibble(writer, c >> 4);
                            WriteJsonHexNibble(writer, c);
                        }
                        else
                        {
                            writer.Write(c);
                        }

                        break;
                }
            }
        }

        private static void WriteBlackboxEntryOffsets(StreamWriter writer)
        {
            writer.Write(",\"blackboxEntryOffsetFrame\":");
            WriteInvariant(writer, BlackboxEntryOffsetFrame);
            writer.Write(",\"blackboxEntryOffsetExtremeFrame\":");
            WriteInvariant(writer, BlackboxEntryOffsetExtremeFrame);
            writer.Write(",\"blackboxEntryOffsetShiftSequence\":");
            WriteInvariant(writer, BlackboxEntryOffsetShiftSequence);
            writer.Write(",\"blackboxEntryOffsetEventHash\":");
            WriteInvariant(writer, BlackboxEntryOffsetEventHash);
            writer.Write(",\"blackboxEntryOffsetNativeBytes\":");
            WriteInvariant(writer, BlackboxEntryOffsetNativeBytes);
            writer.Write(",\"blackboxEntryOffsetH8Bytes\":");
            WriteInvariant(writer, BlackboxEntryOffsetH8Bytes);
            writer.Write(",\"blackboxEntryOffsetNativeAllocations\":");
            WriteInvariant(writer, BlackboxEntryOffsetNativeAllocations);
            writer.Write(",\"blackboxEntryOffsetH8Allocations\":");
            WriteInvariant(writer, BlackboxEntryOffsetH8Allocations);
            writer.Write(",\"blackboxEntryOffsetDispatcherPhaseMs\":");
            WriteInvariant(writer, BlackboxEntryOffsetDispatcherPhaseMs);
            writer.Write(",\"blackboxEntryOffsetDataVaultFragmentation\":");
            WriteInvariant(writer, BlackboxEntryOffsetDataVaultFragmentation);
            writer.Write(",\"blackboxEntryOffsetLastShiftMetersX\":");
            WriteInvariant(writer, BlackboxEntryOffsetLastShiftMetersX);
            writer.Write(",\"blackboxEntryOffsetLastShiftMetersY\":");
            WriteInvariant(writer, BlackboxEntryOffsetLastShiftMetersY);
            writer.Write(",\"blackboxEntryOffsetLastShiftMetersZ\":");
            WriteInvariant(writer, BlackboxEntryOffsetLastShiftMetersZ);
            writer.Write(",\"blackboxEntryOffsetFlags\":");
            WriteInvariant(writer, BlackboxEntryOffsetFlags);
        }

        private static void WriteBlackboxHeaderLayout(StreamWriter writer)
        {
            writer.Write(",\"blackboxByteOrder\":\"little_endian\"");
            writer.Write(",\"blackboxFloatFormat\":\"ieee754_binary32\"");
            writer.Write(",\"blackboxHeaderOffsetMagic\":");
            WriteInvariant(writer, BlackboxHeaderOffsetMagic);
            writer.Write(",\"blackboxHeaderOffsetValidEntryCount\":");
            WriteInvariant(writer, BlackboxHeaderOffsetValidEntryCount);
            writer.Write(",\"blackboxHeaderOffsetEntrySizeBytes\":");
            WriteInvariant(writer, BlackboxHeaderOffsetEntrySizeBytes);
            writer.Write(",\"blackboxHeaderOffsetCursor\":");
            WriteInvariant(writer, BlackboxHeaderOffsetCursor);
        }

        private static void WriteJsonHexNibble(StreamWriter writer, int value)
        {
            int nibble = value & 0xF;
            writer.Write((char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10)));
        }

        private static void WriteInvariant(StreamWriter writer, int value)
        {
            Span<char> scratch = stackalloc char[16];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, uint value)
        {
            Span<char> scratch = stackalloc char[16];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
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
