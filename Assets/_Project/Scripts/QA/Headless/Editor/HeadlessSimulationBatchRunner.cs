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
        // written and is independent of the runtime runner. It just never got to run.
        //
        // THE OLD CONSTANT COULD NOT PASS ITS OWN DEFAULT RUN, and that is very likely why this harness has
        // never been run to completion. It was 600 s with a comment claiming ten minutes "covers a cold Bee
        // compile plus a full 100-day run at ~36 real seconds per simulated day" — but 100 days x 36 s is
        // 3600 s by that comment's own arithmetic, six times the budget it was justifying. A default run was
        // therefore guaranteed to end in BATCH_TIMEOUT with roughly zero days simulated.
        //
        // The real delivered rate is worse again, and it is a dispatcher property rather than anything this
        // file controls. SystemDispatcher.RunFastTick is a FIXED-STEP substep loop: it accumulates the dilated
        // frame delta but calls FastTick(1.0/60.0) at most MaxCadenceSubstepsPerFrame = 4 times per frame and
        // then DISCARDS the overflow (SystemDispatcher.cs:6245-6246 clamps the accumulator back to one
        // interval). The runner advances its day counter by that fixed 1/60, so simulated seconds per real
        // second is 4 * fps / 60 = fps / 15 — reaching the runner's nominal TimeDilationScalar of 100 would
        // need 1500 fps of full-world player loop. At a realistic batchmode 60-200 fps the harness delivers
        // 4x-13x, so the 100-day default is 7.5 to 25 hours of wall clock, not one.
        //
        // So the watchdog now DERIVES from the workload it is watching instead of asserting a number. A
        // watchdog that does not know what it guards is not a safety net, it is a coin flip: too small and
        // every honest run is killed, too large and a hang costs a night. The fixed part covers a cold Bee
        // compile and play-mode entry; the variable part is the simulated span converted at a deliberately
        // PESSIMISTIC 4x, because being killed at the finish line destroys the whole run while overshooting
        // only costs idle minutes on a genuine hang.
        //
        // Practical consequence, worth stating because it is the difference between a useful first run and a
        // no-op: pass -h8headlessDays 5 -h8headlessDaySeconds 60 for a smoke run. That is 300 simulated
        // seconds, minutes rather than hours, and it exercises every lane the 100-day run does.
        private const double TimeoutFixedSeconds = 420.0;
        private const double PessimisticDilation = 4.0;
        private const double TimeoutCeilingSeconds = 6.0 * 60.0 * 60.0;

        private static double ResolveTimeoutSeconds()
        {
            double simulatedSpan = ReadSimulatedSpanSecondsFromArgs();
            double budget = TimeoutFixedSeconds + (simulatedSpan / PessimisticDilation);
            return budget > TimeoutCeilingSeconds ? TimeoutCeilingSeconds : budget;
        }
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

        /// <summary>
        /// True when `-h8headless` (or `-headless`) is on this process's command line, which is the ONLY
        /// thing that puts GameBootstrapper into headless boot mode.
        /// </summary>
        /// <remarks>
        /// The two sides of this harness read DIFFERENT triggers, and the asymmetry silently produced a
        /// 45-minute no-op run. HeadlessSimulationRunner.ShouldRunStatic accepts argv OR the
        /// H8_HEADLESS_SIMULATION env var OR the Temp flag file this class writes. But
        /// GameBootstrapper._headlessBootMode comes only from IsHeadlessBootRequested()
        /// (GameBootstrapper.cs:6647, assigned :2585), which is argv-ONLY. So calling Run() without
        /// `-h8headless` on the command line installs the runtime runner while the bootstrapper boots as a
        /// full PLAYER: it keeps the audio listener, initialises SpatialAudioManager, RenderDispatcher and
        /// ConnectionSplineBatchRenderer for real, falls past the headless early-out at
        /// GameBootstrapper.cs:3120-3123 and LOADS 01_MAIN_MENU. That is precisely the symptom the comment
        /// above this class describes: "play mode simply carried on running the main menu for 45 minutes
        /// with no result file, no CSV rows and no log line."
        ///
        /// WHY THE REFUSAL LIVES HERE RATHER THAN WIDENING THE BOOTSTRAPPER'S TRIGGER. Teaching
        /// IsHeadlessBootRequested to also accept the env var and the flag file would "fix" this too, and it
        /// would be the more dangerous repair: a stale Temp flag file left behind by a killed run would then
        /// put a developer's ordinary editor session into headless boot mode, with the audio listener
        /// stripped and the main menu never loaded, and nothing on screen explaining why. That is not
        /// hypothetical in this project - a leftover `run.flag` from the geology atlas task hijacked EVERY
        /// batchmode launch on this machine, permanently and silently, until it was found by hand
        /// (fixed in 105d27df6). Narrowing the caller cannot be defeated by a file on disk; widening the
        /// bootstrapper can.
        ///
        /// System.Environment is fully qualified deliberately: Hecton8.Environment shadows
        /// System.Environment for any file inside the Hecton8.* namespace root, and a bare `Environment`
        /// here fails CS0234. CONTRIBUTING.md records that trap.
        /// </remarks>
        private static bool HasHeadlessCommandLineArg()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-h8headless", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "-headless", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Run()
        {
            // Refuse rather than produce a run that measures a main menu. Checked BEFORE any state is
            // written - no SessionState, no flag file, no deleted artifacts - so a refused invocation leaves
            // the project exactly as it found it and cannot arm the Tick loop it never intends to use.
            if (!HasHeadlessCommandLineArg())
            {
                Debug.LogError(
                    "[HeadlessSimulationBatchRunner] REFUSED: -h8headless is absent from the command line. " +
                    "The flag file this class writes starts the runtime runner, but only argv puts " +
                    "GameBootstrapper into headless boot mode - so this run would boot a full player and " +
                    "load 01_MAIN_MENU, then sit there until the watchdog fired, with no result file and no " +
                    "CSV rows. Relaunch as: Unity.exe -batchmode -h8headless -h8headlessDays 5 " +
                    "-h8headlessDaySeconds 60 -executeMethod " +
                    "Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run, with the working " +
                    "directory set to the project root.");

                // Exit only in batchmode. There a nonzero code is the only thing the host job can read, and
                // silently returning would hand it a green run that measured nothing. Interactively,
                // killing a developer's editor over a bad argument is not a proportionate response - the
                // LogError above is already in the Console. Run() is public static and currently reachable
                // only through -executeMethod, but that is a fact about today's callers, not a guarantee.
                if (Application.isBatchMode)
                    EditorApplication.Exit(2);

                return;
            }

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

        /// <summary>
        /// One 0.25 s poll of the run. Branch order is load-bearing; do not reorder without reading the
        /// remarks.
        /// </summary>
        /// <remarks>
        /// The exit branch used to sit THIRD, below an unconditional `return` in the result-file branch, and
        /// that made it dead code for every run that got as far as writing a verdict - which is every run
        /// that reaches any RequestStop call site, because all four of them are preceded by a result file
        /// existing on disk (the runtime runner's own, or WriteFallbackResult's). Two consequences, one
        /// observed and one latent:
        ///
        /// 1. Stop was re-requested on every poll for as long as the result file existed. The real status
        ///    file recorded `runtime_fault` TWICE for the 2026-07-29 run, at 04:48:14 and again at 04:55:56,
        ///    and the 2026-07-16 run did the same 1.3 s apart. Each repeat re-appended a status line,
        ///    re-deleted the flag file and re-wrote SessionState. What finally ended both runs was
        ///    RequestStop falling through to CompleteAfterPlayStopped once play mode was down - never the
        ///    branch written for that job.
        ///
        ///    Honest scope note, because the duplicate looks more expensive than it was: the 7m42s between
        ///    those two lines was NOT burned by the duplicate. `exit_nonzero` follows the second
        ///    `runtime_fault` by 3.4 ms, and the gap is Unity recompiling assemblies for a concurrent editor
        ///    session (`headless_run_unity.log:21336`, `:21375` "Reloading assemblies after forced synchronous
        ///    recompile", `:21578`, `:25738`) while Tick correctly bailed on isCompiling/isUpdating (:137).
        ///    The duplicate is the fingerprint of the missing latch, not the cost of it.
        ///
        /// 2. The latent one is worse. TryResolveExitCode returns false on IOException and
        ///    UnauthorizedAccessException, and the old branch returned anyway - so while a result file existed
        ///    but could not be read, HasTimedOut() was never evaluated. That is the ONLY watchdog able to
        ///    survive the runtime runner dying (see the constant block above), and a persistently locked
        ///    result file removed it entirely: infinite 0.25 s polling, no timeout, Unity slot held. The
        ///    unreadable case therefore falls THROUGH to the watchdog now instead of returning.
        /// </remarks>
        private static void PollRunState()
        {
            // First, not third. Once the stop is latched the only remaining job is to reach the terminal
            // path, and nothing below may pre-empt it or re-decide the exit code.
            if (SessionState.GetBool(ExitRequestedKey, false))
            {
                CompleteAfterPlayStopped(SessionState.GetInt(ExitCodeKey, 1));
                return;
            }

            string resultPath = ResolveProjectPath(ResultRelativePath);
            bool resultExists = File.Exists(resultPath);
            if (resultExists && TryResolveExitCode(resultPath, out int exitCode))
            {
                RequestStop(exitCode, exitCode == 0 ? "completed" : "runtime_fault");
                return;
            }

            if (HasTimedOut())
            {
                WriteFallbackResult(2, "BATCH_TIMEOUT");
                RequestStop(2, "timeout");
                return;
            }

            // Reached only when the result file exists and is unreadable. Falling through this far kept the
            // watchdog alive; falling further would re-enter play mode on top of a run that has already
            // produced a verdict, so this is where that case stops.
            if (resultExists)
                return;

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

            return EditorApplication.timeSinceStartup - startTime > ResolveTimeoutSeconds();
        }

        /// <summary>
        /// Simulated span the runtime runner was asked for, in simulated seconds, read from the same argv the
        /// runner parses. Returns the default 100 x 3600 when the args are absent.
        /// </summary>
        /// <remarks>
        /// Parsed here rather than read off the runner because this is the EDITOR side and it has to size its
        /// watchdog before play mode exists, so there is no runner instance to ask. The defaults are duplicated
        /// from HeadlessSimulationRunner.DefaultTargetDays / DefaultDaySeconds; if those change and this does
        /// not, the watchdog under-sizes silently, which is the failure this whole block exists to remove. That
        /// duplication is the cost of the editor/runtime split and is cheaper than a shared constants asset for
        /// two numbers.
        /// </remarks>
        private static double ReadSimulatedSpanSecondsFromArgs()
        {
            const double defaultDays = 100.0;
            const double defaultDaySeconds = 3600.0;
            double days = defaultDays;
            double daySeconds = defaultDaySeconds;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-h8headlessDays", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDays) &&
                    parsedDays >= 1.0)
                {
                    days = parsedDays;
                }
                else if (string.Equals(args[i], "-h8headlessDaySeconds", StringComparison.OrdinalIgnoreCase) &&
                         double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDaySeconds) &&
                         parsedDaySeconds >= 1.0)
                {
                    daySeconds = parsedDaySeconds;
                }
            }

            return days * daySeconds;
        }

        /// <summary>
        /// Latches the run's verdict and asks play mode to stop. Idempotent: the first call owns the exit
        /// code and the status line, every later call is routed straight to the terminal path.
        /// </summary>
        /// <remarks>
        /// The latch lives here as well as in PollRunState's branch order so the "requested once" property is
        /// a local invariant rather than a property of one call site's ordering. Run() clears
        /// ExitRequestedKey (:87) before its own failure paths can call this, so a stale latch from a previous
        /// editor session cannot swallow a fresh run's stop.
        /// </remarks>
        private static void RequestStop(int exitCode, string status)
        {
            if (SessionState.GetBool(ExitRequestedKey, false))
            {
                CompleteAfterPlayStopped(SessionState.GetInt(ExitCodeKey, exitCode));
                return;
            }

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
            {
                // Re-assert the stop, but WITHOUT re-entering RequestStop. Play mode does not come down on
                // the frame it is asked to, so this branch runs several times per stop; the old code reached
                // the same re-assertion through RequestStop and paid for it with a duplicate status line, a
                // duplicate flag delete and a re-written exit code every 0.25 s. Keeping the assignment here
                // preserves the one useful thing that duplicate did - it also cancels a play-mode ENTRY that
                // is still in flight, which the old `return` let proceed.
                EditorApplication.isPlaying = false;
                return;
            }

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

        /// <summary>
        /// Absolute path for a project-relative path, resolved from the PROJECT rather than from the
        /// process working directory.
        /// </summary>
        /// <remarks>
        /// WAS `Directory.GetCurrentDirectory()`, and that made this class disagree with the runtime half of
        /// the same harness. `HeadlessSimulationRunner.ResolveProjectPathStatic` uses
        /// `Path.GetFullPath(Path.Combine(Application.dataPath, ".."))`, which is the real project root and
        /// does not care where the process was launched from. Two resolvers for one question is one too
        /// many, and the failure it produced was silent and expensive: launch Unity with `-projectPath`
        /// from any other working directory and (a) the flag file lands outside the project, so
        /// `ShouldRunStatic`'s file check misses it, and (b) the runtime runner writes the result JSON under
        /// the project while this class's poll loop watches the CWD - so it never sees the verdict, waits out
        /// the full `420 + simulatedSpan/4`, and writes `BATCH_TIMEOUT` over a run that had already
        /// succeeded. The artifacts then say the harness timed out when what actually happened is that the
        /// two halves wrote into different trees.
        ///
        /// `Application.dataPath` is valid in the Editor - it returns `&lt;project&gt;/Assets` - so this needs no
        /// editor-only special case and now matches the runtime side exactly.
        ///
        /// Consequence worth knowing: "cd to the project root before launching" stops being load-bearing.
        /// It remains good practice, but it is no longer the difference between a verdict and a timeout.
        /// </remarks>
        private static string ResolveProjectPath(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
