#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.QA.Headless.Editor
{
    public sealed class Shinobu38QaWatchdogCommanderWindow : EditorWindow
    {
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string FlagRelativePath = "Temp/H8_QA_ENDURANCE_10KM.flag";
        private float _swimSpeed = 85f;
        private float _avoidanceStrength = 1.35f;
        private float _telemetryHz = 4f;

        [MenuItem("Hecton8/QA Bot Commander")]
        public static void Open()
        {
            GetWindow<Shinobu38QaWatchdogCommanderWindow>("QA Bot Commander");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private void OnGUI()
        {
            _swimSpeed = EditorGUILayout.Slider("Swim Speed", _swimSpeed, 1f, 250f);
            _avoidanceStrength = EditorGUILayout.Slider("Obstacle Avoidance Strength", _avoidanceStrength, 0f, 4f);
            _telemetryHz = EditorGUILayout.Slider("Telemetry Write Frequency", _telemetryHz, 0.25f, 20f);

            if (GUILayout.Button("Launch 10KM Endurance Run"))
                Shinobu38QaWatchdogBatchRunner.Run(_swimSpeed, _avoidanceStrength, _telemetryHz);

            Shinobu38QaWatchdogRuntime.TryWriteTuning(_swimSpeed, _avoidanceStrength, _telemetryHz);
        }

        private static void DrawSceneGizmos(SceneView sceneView)
        {
            if (!Shinobu38QaWatchdogRuntime.TryGetDebugPath(out double3 current, out double3 target, out float3 normal))
                return;

            Vector3 currentPosition = Vector3.zero;
            Vector3 targetPosition = ToLocalVector(target - current);
            Handles.color = Color.yellow;
            Handles.DrawAAPolyLine(6f, currentPosition, targetPosition);
            Handles.color = Color.red;
            Handles.DrawAAPolyLine(4f, currentPosition, currentPosition + ToFiniteVector(normal) * 20f);
        }

        private static Vector3 ToLocalVector(double3 value)
        {
            const double clip = 250000d;
            if (!math.all(math.isfinite(value)))
                return Vector3.zero;

            return new Vector3(
                (float)math.clamp(value.x, -clip, clip),
                (float)math.clamp(value.y, -clip, clip),
                (float)math.clamp(value.z, -clip, clip));
        }

        private static Vector3 ToFiniteVector(float3 value)
        {
            return math.all(math.isfinite(value)) ? new Vector3(value.x, value.y, value.z) : Vector3.up;
        }
    }

    [InitializeOnLoad]
    public static class Shinobu38QaWatchdogBatchRunner
    {
        private const string ActiveKey = "H8.SHINOBU79.Active";
        private const string ExitRequestedKey = "H8.SHINOBU79.ExitRequested";
        private const string StartTimeKey = "H8.SHINOBU79.StartTime";
        private const string ExitCodeKey = "H8.SHINOBU79.ExitCode";
        private const string SpeedKey = "H8.SHINOBU79.Speed";
        private const string AvoidanceKey = "H8.SHINOBU79.Avoidance";
        private const string TelemetryKey = "H8.SHINOBU79.Telemetry";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string FlagRelativePath = "Temp/H8_QA_ENDURANCE_10KM.flag";
        private const string ResultRelativePath = "Docs/AgentLogs/SHINOBU_79_QA_Endurance_Result.json";
        private const string RunnerStatusRelativePath = "Docs/AgentLogs/SHINOBU_79_QA_BatchRunner.txt";
        private const double TimeoutSeconds = 900.0;
        private const double PollIntervalSeconds = 0.25;
        private const float ManualBatchDeltaSeconds = 1f / 60f;
        private const int ManualBatchStepsPerEditorUpdate = 128;
        private const int ResultReadBufferSize = 4096;
        private static readonly byte[] ResultReadBuffer = new byte[ResultReadBufferSize];
        private static readonly byte[] StatusZeroTokenBytes = { (byte)'"', (byte)'s', (byte)'t', (byte)'a', (byte)'t', (byte)'u', (byte)'s', (byte)'"', (byte)':', (byte)'0' };
        private static double _nextPollTime;
        private static int _manualBatchTickCounter;
        private static bool _batchProcessResolved;
        private static bool _isBatchProcess;

        static Shinobu38QaWatchdogBatchRunner()
        {
            if (SessionState.GetBool(ActiveKey, false))
                Attach();
        }

        [MenuItem("Hecton8/QA/Run SHINOBU_79 10KM")]
        public static void RunMenu()
        {
            Run(85f, 1.35f, 4f);
        }

        public static void Run()
        {
            Run(85f, 1.35f, 4f);
        }

        public static void Run(float speed, float avoidance, float telemetryHz)
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitRequestedKey, false);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(SpeedKey, speed);
            SessionState.SetFloat(AvoidanceKey, avoidance);
            SessionState.SetFloat(TelemetryKey, telemetryHz);
            _manualBatchTickCounter = 0;
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

            Shinobu38QaWatchdogRuntime.TryWriteTuning(
                SessionState.GetFloat(SpeedKey, 85f),
                SessionState.GetFloat(AvoidanceKey, 1.35f),
                SessionState.GetFloat(TelemetryKey, 4f));

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            DriveRuntimeManuallyInBatchMode();

            if (!ShouldPollNow())
                return;

            PollBatchState();
        }

        private static void PollBatchState()
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

        private static void DriveRuntimeManuallyInBatchMode()
        {
            if (!IsBatchProcess() || !EditorApplication.isPlaying)
                return;

            Shinobu38QaWatchdogRuntime runtime = Shinobu38QaWatchdogRuntime.Active;
            if (runtime == null || !runtime.IsRunning)
                return;

            for (int i = 0; i < ManualBatchStepsPerEditorUpdate; i++)
            {
                runtime = Shinobu38QaWatchdogRuntime.Active;
                if (runtime == null || !runtime.IsRunning)
                    return;

                runtime.FastTick(ManualBatchDeltaSeconds);
                runtime.LateFrameTick();
                _manualBatchTickCounter++;
                if ((_manualBatchTickCounter & 15) == 0)
                    runtime.ColdTick();
            }
        }

        private static bool HasTimedOut()
        {
            double startTime = SessionState.GetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            return EditorApplication.timeSinceStartup - startTime > TimeoutSeconds;
        }

        private static void RequestStop(int exitCode, string status)
        {
            WriteRunnerStatus(status);
            TryDeleteFile(ResolveProjectPath(FlagRelativePath));
            SessionState.SetInt(ExitCodeKey, exitCode);
            SessionState.SetBool(ExitRequestedKey, true);

            if (IsBatchProcess())
            {
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(ExitRequestedKey, false);
                Detach();
                EditorApplication.Exit(exitCode);
                return;
            }

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
            if (IsBatchProcess())
                EditorApplication.Exit(exitCode);
        }

        private static bool IsBatchProcess()
        {
            if (_batchProcessResolved)
                return _isBatchProcess;

            string[] args = System.Environment.GetCommandLineArgs();
            bool isBatch = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-batchmode", StringComparison.OrdinalIgnoreCase))
                {
                    isBatch = true;
                    break;
                }
            }

            _isBatchProcess = isBatch;
            _batchProcessResolved = true;
            return isBatch;
        }

        private static bool TryResolveExitCode(string resultPath, out int exitCode)
        {
            exitCode = 1;
            try
            {
                exitCode = FileContainsPattern(resultPath, StatusZeroTokenBytes) ? 0 : 1;
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

        private static void WriteRunnerStatus(string status)
        {
            try
            {
                string path = ResolveProjectPath(RunnerStatusRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    WriteAscii(stream, status);
                    stream.WriteByte((byte)'\n');
                }
            }
            catch (Exception)
            {
            }
        }

        private static bool TryWriteFlagFile()
        {
            try
            {
                string path = ResolveProjectPath(FlagRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    stream.WriteByte((byte)'1');
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

        private static void WriteAscii(FileStream stream, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                stream.WriteByte(c <= 127 ? (byte)c : (byte)'?');
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
    }
}
#endif
