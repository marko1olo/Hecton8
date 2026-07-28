using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Enters Play Mode from batchmode, lets the game boot, reports what the runtime actually
    /// contains, and exits.
    ///
    /// This exists because nothing in the project could prove runtime behaviour. Every static
    /// probe here answers "is it wired", never "does it run". The PlayMode test assembly is
    /// disabled by a NEVER_COMPILE_TESTS define constraint, so no test can execute without
    /// changing project-wide settings, and a headless machine has no other way in. The result was
    /// a steady stream of changes verified by "0 CS errors" and nothing else.
    ///
    /// Usage - note there must be NO -quit, or the editor closes before Play Mode ever starts:
    ///   Unity.exe -batchmode -nographics -projectPath . -logFile Logs/playprobe.log \
    ///             -executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run \
    ///             -h8Scene Assets/_Project/Scenes/02_HECTON_WORLD.unity -h8WarmupFrames 240
    ///
    /// The probe calls EditorApplication.Exit itself: 0 if every check passed, 1 otherwise.
    ///
    /// EVERY run leaves one machine-readable artifact, by default at
    /// &lt;projectRoot&gt;/Logs/h8_playprobe_route.json, overridable with -h8RouteArtifact. It carries the
    /// per-phase clock table, the save-directory diff, and a verdict for all ten rows of the First 20
    /// Minutes Required Route - rows with no producer report NOT_EXERCISED rather than staying
    /// silent. Before that artifact existed, four runs on the same day used four different argument
    /// sets, emitted only log text, and could not be compared with one another.
    ///
    /// Save-leg arguments: -h8SaveSeconds (default 60, clamped so the leg cannot push the run past
    /// -h8TimeoutSeconds), -h8SaveSlot (0..2), -h8SkipSaveLeg to disable it. The leg only runs after
    /// a game was actually started and the world scene settled.
    ///
    /// Relies on the project having Enter Play Mode Options set to DisableDomainReload
    /// (ProjectSettings/EditorSettings.asset, m_EnterPlayModeOptions: 1), which is why a plain
    /// static state machine survives the transition. Should that ever change, the statics reset
    /// mid-run and the probe stalls rather than lying - always run it under an external timeout.
    /// </summary>
    public static class H8_HeadlessPlayModeProbe
    {
        private const string Marker = "[H8_PLAYPROBE]";
        private const string DefaultScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

        /// <summary>
        /// Where a New Game run has to start. The normative flow is 00_BOOTSTRAP -> 01_MAIN_MENU ->
        /// 02_HECTON_WORLD (AGENTS.md:162); entering play anywhere else is the recovery scenario, not
        /// the product route.
        /// </summary>
        private const string BootstrapSceneAssetPath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const int DefaultWarmupFrames = 240;
        private static double _hardTimeoutSeconds = 240.0;

        private enum Phase
        {
            Idle,
            WaitingForPlayMode,
            WarmingUp,
            LoadingMenu,
            MenuWarmup,
            StartingGame,
            WaitingForSettle,
            GameplayWarmup,
            SaveRoundTrip,
            Reporting,
            LeavingPlayMode,
        }

        /// <summary>
        /// Verdict for one row of the First 20 Minutes Required Route table
        /// (<c>Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:68-90</c>).
        ///
        /// <see cref="Partial"/> is a real state with a narrow meaning: SOME of the row's minimum
        /// acceptance was exercised and observed, and the row is NOT accepted. It exists because the
        /// contract's "Save/load" row is one row covering two operations, and reporting a proven save
        /// with an unattempted load as <see cref="Pass"/> is exactly the half-closed seam this harness
        /// is supposed to expose.
        ///
        /// Declared in ascending severity so <see cref="RecordMoment"/> can enforce "the worse verdict
        /// wins" with a single comparison: an early optimistic reading can never mask a later failure.
        /// </summary>
        private enum MomentVerdict : byte
        {
            NotExercised = 0,
            Pass = 1,
            Partial = 2,
            Blocked = 3,
            Fail = 4,
        }

        private struct RouteMoment
        {
            public string Name;
            public MomentVerdict Verdict;
            public string Detail;
        }

        private struct PhaseSample
        {
            public string Phase;
            public double WallSeconds;
            public int GameFrames;
            public int ProbeTicks;
            public bool DuringPlay;
        }

        private struct SaveFileFacts
        {
            public string Name;
            public long Length;
            public long WriteTicksUtc;
            public ulong ContentHash;
            public bool Hashed;
        }

        private struct SaveDirectoryDiff
        {
            public int Added;
            public int Removed;
            public int Changed;
            public long ByteDelta;
            public string Lines;

            public int TotalChanges => Added + Removed + Changed;
        }

        private const string MenuSceneName = "01_MAIN_MENU";
        private const int MenuWarmupFrames = 120;

        /// <summary>
        /// How long a DISABLED <c>MainMenuController</c> is tolerated before the probe stops waiting for
        /// it to enable itself. Not a timeout in the usual sense - the condition is decided in the first
        /// second and this grace only covers the window where the controller may not have run
        /// <c>Awake</c> yet. Kept short because the alternative, measured, was 201 wall seconds and
        /// 11490 game frames spent waiting for a flag that cannot flip.
        /// </summary>
        private const double DisabledMenuGraceSeconds = 12.0d;

        // How long to let the game bring up its OWN menu. Default is generous on purpose: the
        // bootstrap -> menu handoff is a Single load with an activation gate, and rushing it is how
        // the earlier versions of this probe went wrong twice.
        // SECONDS, not ticks. Measured: 3300 EditorApplication.update callbacks advanced the game
        // by 19 frames in 13.7s wall - about one game frame per second, with or without -nographics.
        // Boot frames genuinely cost that much in the editor, so every frame budget this probe used
        // before was worth a couple of dozen game frames and proved nothing. Wall time is the only
        // honest unit here.
        private static double _menuWaitSeconds = 300.0;

        // Loading a menu ourselves is OFF by default. When the probe did it additively on top of a
        // still-active 00_BOOTSTRAP, New Game then issued a Single load that unloaded both scenes,
        // SceneRuntimeService's activation-gate loop exited on isActiveAndEnabled without ever
        // calling ReleaseSceneActivation, and 02_HECTON_WORLD sat at roots=0 isLoaded=false forever.
        // No watchdog or emergency-release line appeared in the log, which is how we know that loop
        // was not running. That deadlock is almost certainly probe-induced - it is a runtime state
        // the game never produces - so it must not be created by default and must not be reported
        // as a game defect.
        private static bool _forceMenuLoad;

        // After New Game the world scene load is in flight for a long time in batchmode - 2400
        // gameplay frames were not enough for 02_HECTON_WORLD to report isLoaded. Counting frames
        // measures the wrong thing; wait for the loads to actually finish.
        private static double _settleWaitSeconds = 300.0;
        private static double _gameplaySeconds = 60.0;
        private static double _phaseStartedAt;

        private static Phase _phase = Phase.Idle;
        private static double _startedAt;
        private static int _frames;
        private static int _gameplayFrames;
        private static int _warmupFrames = DefaultWarmupFrames;
        private static int _gameplayFramesTarget;
        private static bool _startNewGame;
        private static int _menuFrames;
        private static int _settleFrames;
        private static int _gameFrameAtPlayStart;
        private static double _playStartedAt;
        private static AsyncOperation _menuLoad;
        private static int _failures;

        private const string ReadyLockRejection = "Ready-locked registry rejected registration";
        private static readonly List<string> _rejectedServices = new List<string>();
        private static bool _logHookInstalled;

        // ---- per-phase clock segments -------------------------------------------------------
        // One blended average over the whole session was the only rate this probe reported, and it
        // is worthless: a run that stalls before world load measures the ~1 fps bootstrap phase, a
        // run that reaches 02_HECTON_WORLD measures 35-53 game frames per wall second, and the
        // average of the two describes neither. Every frame budget derived from that single number
        // was wrong by more than an order of magnitude. Segments are closed on every phase
        // transition so each phase reports its own rate.
        private static readonly List<PhaseSample> _phaseSamples = new List<PhaseSample>();
        private static Phase _clockPhase = Phase.Idle;
        private static bool _clockSegmentOpen;
        private static double _clockWallStart;
        private static int _clockGameFrameStart;
        private static int _clockTickStart;
        private static int _totalTicks;

        // ---- First 20 Minutes route moments -------------------------------------------------
        private const string MomentBoot = "Boot";
        private const string MomentWorldLoad = "WorldLoad";
        private const string MomentFirstExit = "FirstExit";
        private const string MomentSwim = "Swim";
        private const string MomentResource = "Resource";
        private const string MomentTool = "Tool";
        private const string MomentCraft = "CraftRepairBuild";
        private const string MomentHazard = "Hazard";
        private const string MomentSaveLoad = "SaveLoad";
        private const string MomentProof = "Proof";

        private static readonly List<RouteMoment> _routeMoments = new List<RouteMoment>();

        // ---- save round trip ------------------------------------------------------------------
        // Stable caller hash for IAsyncPersistenceService telemetry, same convention as
        // SaveStation.cs:42 (SSVE). PROB = this probe.
        private const uint ProbeSaveSourceHash = 0x50524F42u;
        private const int HashChunkBytes = 1 << 16;
        private const long MaxHashedFileBytes = 64L * 1024L * 1024L;
        private const int MaxDiffLines = 32;
        private const double SaveDiffPollSeconds = 1.0;
        private const double SaveFlushGraceSeconds = 3.0;
        private const double SaveLegTimeoutMarginSeconds = 5.0;

        private static bool _saveLegEnabled = true;
        private static byte _saveSlotIndex;
        private static double _saveWaitSeconds = 60.0;
        private static double _saveWaitBudget;
        private static bool _saveRequestIssued;
        private static bool _saveAccepted;
        private static bool _saveBusyObserved;
        private static double _saveBusyClearedAt;
        private static double _saveLegStartedAt;
        private static double _saveLastPollAt;
        private static double _saveWaitedSeconds;
        private static string _saveRoot = string.Empty;
        private static string _saveSlotName = string.Empty;
        private static string _saveError = string.Empty;
        private static SaveFileFacts[] _saveBefore = Array.Empty<SaveFileFacts>();
        private static int _saveFilesAfter;
        private static SaveDirectoryDiff _saveDiff;
        private static byte[] _hashBuffer;

        // ---- run artifact ---------------------------------------------------------------------
        // Four runs today used four different argument sets and produced only a megabyte of log
        // each, so none of them are comparable. Every terminal path now writes one JSON with the
        // same schema at the same default path.
        private static string _artifactPath = string.Empty;
        private static string _scenePath = string.Empty;
        private static bool _artifactWritten;

        public static void Run()
        {
            ResetRunState();

            // When the probe is going to press New Game it MUST start where the product starts.
            // AGENTS.md:162 fixes the normative flow as 00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD,
            // and opening the world scene directly is not that route - it is the RECOVERY scenario, which
            // the product handles by design: HandleSceneLoadedGuard (GameBootstrapper.cs:7114) sees a
            // non-bootstrap scene while _isBootstrapComplete is false and calls TryRecoverEntryVector,
            // which loads 00_BOOTSTRAP as LoadSceneMode.Single (:7164-7166) and so DESTROYS the scene the
            // probe just opened, along with everything in it.
            // Measured consequence of getting this wrong: a run that opened 02_HECTON_WORLD logged the
            // world spawner's Awake TWICE against a single "Step 0: Loading 02_HECTON_WORLD", the first
            // player died with the first world scene, and the probe reported that as a product defect.
            // The recovery was working correctly; the instrument was standing in the wrong place.
            // An explicit -h8Scene still wins - starting mid-route is a legitimate thing to measure, as
            // long as it is asked for rather than defaulted into.
            // -h8StartGame is parsed into _startNewGame further down, AFTER this point, so the argument
            // is read directly here rather than depending on a field that is still false.
            bool willPressNewGame = ReadStringArg("-h8StartGame", null) != null;
            string startScenePath = willPressNewGame ? BootstrapSceneAssetPath : DefaultScene;
            string scenePath = ReadStringArg("-h8Scene", startScenePath);
            _scenePath = scenePath;
            _warmupFrames = Math.Max(1, ReadIntArg("-h8WarmupFrames", DefaultWarmupFrames));

            // A headless boot legitimately stops at 01_MAIN_MENU: gameplay systems - ecosystem,
            // terrain, the world-seed owner - are installed only once a game is actually started.
            // Without this the probe can only ever inspect the menu.
            _gameplayFramesTarget = Math.Max(0, ReadIntArg("-h8GameplayFrames", 0));
            _startNewGame = ReadStringArg("-h8StartGame", null) != null || _gameplayFramesTarget > 0;
            _hardTimeoutSeconds = Math.Max(30.0, ReadIntArg("-h8TimeoutSeconds", 240));
            _menuWaitSeconds = Math.Max(5, ReadIntArg("-h8MenuSeconds", 300));
            _settleWaitSeconds = Math.Max(5, ReadIntArg("-h8SettleSeconds", 300));
            _gameplaySeconds = Math.Max(1, ReadIntArg("-h8GameplaySeconds", 60));
            _forceMenuLoad = ReadStringArg("-h8ForceMenuLoad", null) != null;
            _saveWaitSeconds = Math.Max(1, ReadIntArg("-h8SaveSeconds", 60));
            _saveSlotIndex = (byte)Math.Clamp(
                ReadIntArg("-h8SaveSlot", 0), 0, Hecton8.SaveSystem.SaveEvents.ManualSlotCount - 1);
            _saveLegEnabled = ReadStringArg("-h8SkipSaveLeg", null) == null;
            _artifactPath = ReadStringArg("-h8RouteArtifact", ResolveDefaultArtifactPath());

            Debug.Log(
                $"{Marker} START scene={scenePath} warmupFrames={_warmupFrames} " +
                $"gameplayFrames={_gameplayFramesTarget} batchmode={Application.isBatchMode} " +
                $"saveLeg={(_saveLegEnabled ? "on" : "off")} saveSlot={_saveSlotIndex} " +
                $"saveSeconds={_saveWaitSeconds:F0} artifact='{_artifactPath}'");

            InstallRegistryRejectionHook();

            // EnterPlaymode() silently does NOTHING when scripts have compiler errors, and says
            // nothing about why. A broken build therefore presents as "Play Mode never starts": one
            // run burned its whole 1300s budget in phase=WaitingForPlayMode with frames=0 because
            // another agent had landed a CS0234 elsewhere in the project. Fail loudly instead.
            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.Log(
                    $"{Marker} ABORT scripts failed to compile - Play Mode cannot start. Fix the C# " +
                    "errors first; a timeout here reads like a game defect and is not one.");
                Finish(1);
                return;
            }

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.Log($"{Marker} FAILED to open {scenePath}: {ex.GetType().Name}: {ex.Message}");
                Finish(1);
                return;
            }

            _startedAt = EditorApplication.timeSinceStartup;
            SetPhase(Phase.WaitingForPlayMode);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            _totalTicks++;

            if (EditorApplication.timeSinceStartup - _startedAt > _hardTimeoutSeconds)
            {
                Debug.Log($"{Marker} TIMEOUT after {_hardTimeoutSeconds}s in phase={_phase} frames={_frames}");
                Finish(1);
                return;
            }

            switch (_phase)
            {
                case Phase.WaitingForPlayMode:
                    if (EditorApplication.isPlaying)
                    {
                        _gameFrameAtPlayStart = Time.frameCount;
                        _playStartedAt = EditorApplication.timeSinceStartup;
                        Debug.Log(
                            $"{Marker} PLAYING (entered after {EditorApplication.timeSinceStartup - _startedAt:F1}s) " +
                            $"gameFrame={_gameFrameAtPlayStart} timeScale={Time.timeScale}");
                        SetPhase(Phase.WarmingUp);
                    }
                    break;

                case Phase.WarmingUp:
                    // Leaving play mode unexpectedly means the game aborted its own boot. That is a
                    // result, not a probe failure to hide.
                    if (!EditorApplication.isPlaying)
                    {
                        Debug.Log($"{Marker} LEFT PLAY MODE early at frame {_frames} - boot aborted or a script called Exit");
                        Finish(1);
                        return;
                    }

                    if (++_frames >= _warmupFrames)
                    {
                        _phaseStartedAt = EditorApplication.timeSinceStartup;
                        SetPhase(_startNewGame ? Phase.LoadingMenu : Phase.Reporting);
                    }
                    break;

                case Phase.LoadingMenu:
                    TickLoadingMenu();
                    break;

                case Phase.MenuWarmup:
                    if (++_menuFrames >= MenuWarmupFrames)
                        SetPhase(Phase.StartingGame);
                    break;

                case Phase.StartingGame:
                    _phaseStartedAt = EditorApplication.timeSinceStartup;
                    // One shot. If the menu is not there, say so and report on the menu-state
                    // runtime rather than silently pretending a game was started.
                    SetPhase(TryStartNewGame() ? Phase.WaitingForSettle : Phase.Reporting);
                    break;

                case Phase.WaitingForSettle:
                    TickWaitingForSettle();
                    break;

                case Phase.GameplayWarmup:
                    if (!EditorApplication.isPlaying)
                    {
                        Debug.Log($"{Marker} LEFT PLAY MODE during gameplay warmup at frame {_gameplayFrames}");
                        Finish(1);
                        return;
                    }

                    _gameplayFrames++;
                    if (EditorApplication.timeSinceStartup - _phaseStartedAt >= _gameplaySeconds)
                        SetPhase(_saveLegEnabled ? Phase.SaveRoundTrip : Phase.Reporting);
                    break;

                case Phase.SaveRoundTrip:
                    TickSaveRoundTrip();
                    break;

                case Phase.Reporting:
                    RunChecks();
                    SetPhase(Phase.LeavingPlayMode);
                    EditorApplication.ExitPlaymode();
                    break;

                case Phase.LeavingPlayMode:
                    if (!EditorApplication.isPlaying)
                        Finish(_failures == 0 ? 0 : 1);
                    break;
            }
        }

        /// <summary>
        /// The probe counts EditorApplication.update callbacks and calls them "frames". Whether one
        /// of those equals one game frame is an assumption, and every conclusion drawn from a frame
        /// budget rests on it. If the player loop advances far more slowly than the editor loop then
        /// "no menu after 6000 frames" means "no menu after rather few game frames" and says much
        /// less than it appears to.
        ///
        /// This used to print ONE average over the whole session, and that number is not a rate of
        /// anything. Runs that stalled before world load measured a bootstrap phase advancing at
        /// roughly one game frame per wall second; runs that reached 02_HECTON_WORLD measured 35.2,
        /// 51.7 and 52.9 game frames per wall second. A single mean over both describes neither
        /// phase, and the project-wide "batchmode runs about one game frame per second" belief -
        /// which is what every gameplay-window budget in this probe was sized against - came from
        /// exactly that blend. Segments are closed on each phase transition, so each phase now
        /// reports its own rate and the blended line is kept only as a labelled trap.
        /// </summary>
        private static void ReportClockRates()
        {
            int gameFrames = Time.frameCount - _gameFrameAtPlayStart;
            double wall = EditorApplication.timeSinceStartup - _playStartedAt;
            double blended = wall > 0.0 ? gameFrames / wall : 0.0;

            Debug.Log(
                $"{Marker} CLOCKS SESSION probeTicks={_totalTicks} gameFrames={gameFrames} " +
                $"wallSeconds={wall:F1} gameFramesPerWallSecond={blended:F2} " +
                $"timeScale={Time.timeScale} unscaledTime={Time.unscaledTime:F1} captureFramerate={Time.captureFramerate} " +
                "-- BLENDED, DO NOT DERIVE A FRAME BUDGET FROM THIS LINE: it averages the ~1 fps " +
                "bootstrap phase with the ~50 fps world phase. Use the per-phase rows below.");

            if (_phaseSamples.Count == 0)
            {
                Debug.Log($"{Marker} CLOCKS   no completed phase segment - the run never left its first phase");
                return;
            }

            for (int i = 0; i < _phaseSamples.Count; i++)
            {
                PhaseSample sample = _phaseSamples[i];
                double phaseFps = sample.WallSeconds > 0.0 ? sample.GameFrames / sample.WallSeconds : 0.0;
                double tickRate = sample.WallSeconds > 0.0 ? sample.ProbeTicks / sample.WallSeconds : 0.0;
                Debug.Log(
                    $"{Marker} CLOCKS   {sample.Phase,-18} wall={sample.WallSeconds,8:F1}s " +
                    $"probeTicks={sample.ProbeTicks,7} gameFrames={sample.GameFrames,7} " +
                    $"gameFramesPerWallSecond={phaseFps,8:F2} probeTicksPerWallSecond={tickRate,8:F1} " +
                    $"duringPlay={sample.DuringPlay}");
            }
        }

        /// <summary>
        /// Closes the in-flight clock segment and opens one for <paramref name="next"/>. Every phase
        /// transition goes through here; a bare <c>_phase =</c> assignment loses that phase's rate.
        /// </summary>
        private static void SetPhase(Phase next)
        {
            if (next == _phase && _clockSegmentOpen)
                return;

            CloseClockSegment();
            _phase = next;
            _clockPhase = next;
            _clockSegmentOpen = true;
            _clockWallStart = EditorApplication.timeSinceStartup;
            _clockGameFrameStart = Time.frameCount;
            _clockTickStart = _totalTicks;
        }

        private static void CloseClockSegment()
        {
            if (!_clockSegmentOpen)
                return;

            _clockSegmentOpen = false;
            _phaseSamples.Add(new PhaseSample
            {
                Phase = _clockPhase.ToString(),
                WallSeconds = EditorApplication.timeSinceStartup - _clockWallStart,
                GameFrames = Time.frameCount - _clockGameFrameStart,
                ProbeTicks = _totalTicks - _clockTickStart,
                DuringPlay = EditorApplication.isPlaying,
            });
        }

        /// <summary>
        /// SceneRuntimeService is the strongest suspect for the held menu activation. It sets
        /// allowSceneActivation = false and only releases it from inside
        /// `while (Application.isPlaying and _isInitialized and isActiveAndEnabled and !isDone)`.
        /// If that component is not active the loop body never runs, the activation is never
        /// released, and nothing is logged - which matches what the clean runs show exactly:
        /// 01_MAIN_MENU at isLoaded=false roots=0, no watchdog, no exception.
        ///
        /// GlobalRegistry.SceneRuntime is internal, but the public GlobalRegistry.Scene exposes the same
        /// owner as ISceneService, so the registry side is readable from this assembly after all.
        /// _isInitialized is private and cannot be read; isActiveAndEnabled and the public CanLoadScene
        /// are the observable parts.
        ///
        /// This used to walk SceneManager.GetSceneAt + GetRootGameObjects, which is the exact blind spot
        /// called out in ReportRuntimeComponentCensus: that traversal cannot enumerate the
        /// DontDestroyOnLoad scene, so a persistent scene service reads as "not found". It reported
        /// SCENERUNTIME absent while the service was scene-owned in 00_BOOTSTRAP and destroyed by that
        /// scene's unload - the right answer for the wrong reason, and it would report absent just the
        /// same once the service is correctly made persistent. A check that cannot distinguish "fixed"
        /// from "broken" is not a check. FindObjectsByType sees both cases.
        ///
        /// Self-test, same discipline as the component census: the registry and the object census are
        /// read IN THIS RUN and disagreement is reported as an instrument fault rather than a finding.
        /// Registry holds an owner the census cannot see =&gt; the census is broken. Census sees an
        /// instance the registry does not hold =&gt; registration was lost or evicted.
        /// </summary>
        private static void ReportSceneRuntimeService()
        {
            Hecton8.Core.ISceneService registered = Hecton8.Core.GlobalRegistry.Scene;
            string registeredLabel = registered == null ? "null" : registered.GetType().Name;

            // No FindObjectsSortMode overload: it is deprecated in 6000.5 (CS0618) and this check only
            // enumerates, so sort order is irrelevant. The census at ReportRuntimeComponentCensus still
            // uses the deprecated overload and still warns - pre-existing, not changed here, because
            // dropping its explicit None could alter that census's ordering assumptions.
            Hecton8.Core.SceneRuntimeService[] found =
                UnityEngine.Object.FindObjectsByType<Hecton8.Core.SceneRuntimeService>(FindObjectsInactive.Include);

            Debug.Log($"{Marker} SCENERUNTIME registry={registeredLabel} instances={found.Length}");

            for (int i = 0; i < found.Length; i++)
            {
                Hecton8.Core.SceneRuntimeService service = found[i];
                if (service == null)
                    continue;

                Debug.Log(
                    $"{Marker} SCENERUNTIME[{i}] on '{service.gameObject.name}' in scene " +
                    $"'{service.gameObject.scene.name}' isRegistered={ReferenceEquals(service, registered)} " +
                    $"activeAndEnabled={service.isActiveAndEnabled} " +
                    $"goActive={service.gameObject.activeInHierarchy} enabled={service.enabled} " +
                    $"canLoadScene={service.CanLoadScene}");
            }

            if (found.Length == 0 && registered != null)
            {
                Debug.LogError(
                    $"{Marker} SCENERUNTIME INSTRUMENT FAULT - registry holds {registeredLabel} but " +
                    "FindObjectsByType returned 0 instances. Every instance count in this run is suspect.");
                return;
            }

            if (found.Length > 0 && registered == null)
            {
                Debug.LogError(
                    $"{Marker} SCENERUNTIME REGISTRATION LOST - {found.Length} live instance(s) exist but " +
                    "GlobalRegistry.Scene is null, so no ISceneService owner is published.");
                return;
            }

            if (found.Length == 0)
                Debug.Log($"{Marker} SCENERUNTIME absent - no instance anywhere, including DontDestroyOnLoad");
        }

        /// <summary>
        /// Waits for every in-flight scene load to finish before starting the gameplay warmup.
        ///
        /// New Game does complete - 02_HECTON_WORLD appears in the scene list - but it is still
        /// isLoaded=false after 2400 gameplay frames, because a batchmode frame is not a unit of
        /// loading progress. Reporting at a frame count therefore measured the world scene while it
        /// was still streaming in and concluded, wrongly, that nothing had installed.
        /// </summary>
        private static void TickWaitingForSettle()
        {
            var pending = new StringBuilder();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    continue;

                if (pending.Length > 0)
                    pending.Append(", ");

                pending.Append(scene.name);
            }

            double waited = EditorApplication.timeSinceStartup - _phaseStartedAt;

            if (pending.Length == 0)
            {
                Debug.Log($"{Marker} SETTLED after {waited:F0}s - no scene load in flight");
                RecordMoment(
                    MomentWorldLoad,
                    MomentVerdict.Pass,
                    $"every scene load finished after {waited:F0}s; loaded scenes={UnityEngine.SceneManagement.SceneManager.sceneCount}");
                _phaseStartedAt = EditorApplication.timeSinceStartup;
                SetPhase(Phase.GameplayWarmup);
                return;
            }

            if (waited >= _settleWaitSeconds)
            {
                Debug.Log($"{Marker} NOT SETTLED after {_settleWaitSeconds:F0}s - still loading: {pending}");
                RecordMoment(
                    MomentWorldLoad,
                    MomentVerdict.Fail,
                    $"still loading after {_settleWaitSeconds:F0}s: {pending}");
                _phaseStartedAt = EditorApplication.timeSinceStartup;
                SetPhase(Phase.GameplayWarmup);
                return;
            }

            if (++_settleFrames % 400 == 0)
                Debug.Log(
                    $"{Marker} settling... {waited:F0}s of {_settleWaitSeconds:F0}s, " +
                    $"gameFrames={Time.frameCount - _gameFrameAtPlayStart}, loading: {pending}");
        }

        /// <summary>
        /// Brings up the menu AFTER boot has finished, which is the whole trick.
        ///
        /// During a normal headless run the menu scene is entered while the bootstrap is still
        /// initialising, so MainMenuController.Awake sees AreAllSystemsReady() == false, calls
        /// BootstrapRouteEnforcer, and sets enabled = false on itself. The component survives but
        /// is inert, and the enforcer's own recovery LoadSceneAsync returns null. By the time this
        /// probe looks, no usable menu exists.
        ///
        /// Loading the menu here, once allSystemsReady is true, lets that same Awake take the happy
        /// path. Additive on purpose: Single would unload 00_BOOTSTRAP and risk taking the very
        /// services the route check is about with it.
        /// </summary>
        private static void TickLoadingMenu()
        {
            if (_menuLoad == null)
            {
                // Prefer the menu the game brings up itself. At the point boot reports ready its
                // 01_MAIN_MENU load is often still in flight, and GetSceneAt reports that scene as
                // isLoaded=false - which is exactly why an earlier version of this probe concluded
                // "no MainMenuController anywhere" and then loaded a second copy on top.
                // A live menu only counts once nothing is still streaming: the game's own
                // 01_MAIN_MENU reports isLoaded=false while its Single load is in flight, and acting
                // during that window is what produced two simultaneous menus last time.
                if (!IsAnySceneLoadInFlight() &&
                    TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController existing) &&
                    existing.enabled)
                {
                    Debug.Log(
                        $"{Marker} MENU live in scene '{existing.gameObject.scene.name}' after " +
                        $"{EditorApplication.timeSinceStartup - _phaseStartedAt:F0}s of waiting - using the game's own");
                    SetPhase(Phase.StartingGame);
                    return;
                }

                double waited = EditorApplication.timeSinceStartup - _phaseStartedAt;

                // A menu that EXISTS but is DISABLED is not a menu that has not loaded yet, and waiting
                // the full window for it is waiting for something that cannot happen.
                // MainMenuController.Awake disables itself when GameBootstrapper.AreAllSystemsReady() is
                // false, and that gate (GameBootstrapper.cs:675-681) is
                // _isBootstrapComplete && Dispatcher != null && TickManager != null && Save != null &&
                // ObjectPool != null. _isBootstrapComplete is written true at exactly one place
                // (:2373), reached only after all six boot phases succeed. So a disabled menu means boot
                // did not finish, the enabled flag will never flip, and the remaining wait is dead time.
                // Measured before this branch existed: 201 wall seconds and 11490 game frames burned on
                // a condition that was decided in the first second, and the run was killed externally
                // before any terminal phase - so it also left no artifact.
                if (waited >= DisabledMenuGraceSeconds &&
                    !IsAnySceneLoadInFlight() &&
                    TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController inertMenu) &&
                    !inertMenu.enabled)
                {
                    Debug.Log(
                        $"{Marker} MENU EXISTS BUT IS DISABLED in scene '{inertMenu.gameObject.scene.name}' " +
                        $"after {waited:F0}s - boot did not complete, so the enabled flag will never flip. " +
                        "Advancing to the New Game attempt, which reports the disabled controller precisely, " +
                        "instead of burning the rest of the window on a condition already decided.");
                    SetPhase(Phase.StartingGame);
                    return;
                }

                if (waited < _menuWaitSeconds)
                {
                    if (++_menuFrames % 400 == 0)
                        Debug.Log(
                            $"{Marker} waiting for the game's own menu... {waited:F0}s of {_menuWaitSeconds:F0}s, " +
                            $"gameFrames={Time.frameCount - _gameFrameAtPlayStart}");

                    return;
                }

                if (!_forceMenuLoad)
                {
                    Debug.Log(
                        $"{Marker} MENU never became live in {_menuWaitSeconds:F0}s of play. Not loading one: " +
                        "doing that additively deadlocks the world-scene activation gate and the " +
                        "resulting state is not one the game produces. Pass -h8ForceMenuLoad to override.");
                    _failures++;
                    RecordMoment(
                        MomentWorldLoad,
                        MomentVerdict.Blocked,
                        $"no live MainMenuController in {_menuWaitSeconds:F0}s of play, so New Game was never pressed");
                    SetPhase(Phase.Reporting);
                    return;
                }

                Debug.Log(
                    $"{Marker} MENU none live after {_menuWaitSeconds:F0}s - FORCED load of " +
                    $"'{MenuSceneName}' additively (probe-induced state, results are suspect)");
                _menuLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    MenuSceneName,
                    UnityEngine.SceneManagement.LoadSceneMode.Additive);

                if (_menuLoad == null)
                {
                    Debug.Log($"{Marker} MENU LoadSceneAsync returned null - cannot reach gameplay");
                    _failures++;
                    RecordMoment(
                        MomentWorldLoad,
                        MomentVerdict.Blocked,
                        "forced menu LoadSceneAsync returned null, so New Game was never pressed");
                    SetPhase(Phase.Reporting);
                }

                return;
            }

            if (_menuLoad.isDone)
            {
                Debug.Log($"{Marker} MENU fallback scene loaded");
                _menuLoad = null;
                _menuFrames = 0;
                SetPhase(Phase.MenuWarmup);
            }
        }

        private static bool IsAnySceneLoadInFlight()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isLoaded)
                    return true;
            }

            return false;
        }

        private static bool TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController menu)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    menu = root.GetComponentInChildren<Hecton.UI.MainMenu.MainMenuController>(true);
                    if (menu != null)
                        return true;
                }
            }

            menu = null;
            return false;
        }

        /// <summary>
        /// Presses "New Game" the way the menu button does. Root traversal rather than any
        /// FindObjectOfType variant: those are banned project-wide and the ban is worth honouring
        /// in tooling too.
        /// </summary>
        private static bool TryStartNewGame()
        {
            if (!TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController menu))
            {
                Debug.Log($"{Marker} NO MainMenuController found in any loaded scene - reporting on the current runtime instead");
                _failures++;
                RecordMoment(
                    MomentWorldLoad,
                    MomentVerdict.Blocked,
                    "no MainMenuController in any loaded scene, so New Game was never pressed");
                return false;
            }

            // A controller that disabled itself in Awake will not respond, and calling into it
            // anyway would look like success in the log.
            if (!menu.enabled)
            {
                Debug.Log(
                    $"{Marker} MENU FOUND BUT DISABLED in scene '{menu.gameObject.scene.name}' - it failed its own " +
                    "bootstrap route check in Awake, so New Game cannot be pressed");
                _failures++;
                RecordMoment(
                    MomentWorldLoad,
                    MomentVerdict.Blocked,
                    $"MainMenuController in '{menu.gameObject.scene.name}' disabled itself in Awake, so New Game cannot be pressed");
                return false;
            }

            Debug.Log($"{Marker} STARTING NEW GAME via MainMenuController in scene '{menu.gameObject.scene.name}'");
            try
            {
                menu.ReadableStartNewGame();
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"{Marker} NEW GAME THREW {ex.GetType().Name}: {ex.Message}");
                _failures++;
                RecordMoment(
                    MomentWorldLoad,
                    MomentVerdict.Fail,
                    $"New Game threw {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Exercises the SAVE half of the Required Route's save/load row
        /// (<c>FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:88</c>) and produces the "save directory
        /// diff" the contract's Proof row asks for (<c>:90</c>).
        ///
        /// Before this existed the probe spent its whole gameplay window incrementing a counter and
        /// then reported eight static snapshots of registry slots and shader globals. Nothing in the
        /// project read <c>Application.persistentDataPath</c> to check whether a save actually reached
        /// disk, so the save directory diff had no producer at all.
        ///
        /// The request goes through <c>IAsyncPersistenceService.TryRequestSave</c>, which is what the
        /// in-world Save Station uses (<c>Interaction/SaveStation.cs:179</c>) - not a private path
        /// invented for the harness. <c>SaveManager.ProcessSaveRequest</c> fires
        /// <c>SaveGameAsyncInternal</c> and returns immediately, so completion has to be observed, not
        /// assumed: this polls the directory on a wall clock and cross-checks <c>ISaveService.IsBusy</c>.
        /// Polling happens on EditorApplication.update callbacks, which is the only thing that advances
        /// the engine headlessly (COMMON_SENSE.md:32 - "yield return null" hangs forever in batchmode).
        /// </summary>
        private static void TickSaveRoundTrip()
        {
            if (!EditorApplication.isPlaying)
            {
                FinishSaveLeg(MomentVerdict.Fail, "left Play Mode during the save round trip", countFailure: true);
                return;
            }

            if (!_saveRequestIssued)
                BeginSaveRoundTrip();
            else
                PollSaveRoundTrip();
        }

        private static void BeginSaveRoundTrip()
        {
            _saveRequestIssued = true;
            _saveLegStartedAt = EditorApplication.timeSinceStartup;
            _saveLastPollAt = _saveLegStartedAt;
            _saveBusyClearedAt = 0.0;

            // Never let this leg turn a run that used to finish into a TIMEOUT. The hard timeout is
            // the outer contract; the save budget lives inside whatever is left of it.
            double remaining = _hardTimeoutSeconds - (_saveLegStartedAt - _startedAt);
            _saveWaitBudget = Math.Min(_saveWaitSeconds, remaining - SaveLegTimeoutMarginSeconds);
            if (_saveWaitBudget <= 0.0)
            {
                FinishSaveLeg(
                    MomentVerdict.Blocked,
                    $"only {remaining:F0}s left of the {_hardTimeoutSeconds:F0}s probe hard timeout - raise " +
                    "-h8TimeoutSeconds or lower -h8GameplaySeconds so the save leg fits",
                    countFailure: false);
                return;
            }

            _saveRoot = Hecton8.Core.HectonPersistentPathPolicy.RootPath;
            _saveBefore = SnapshotSaveDirectory(_saveRoot, hashSaveFiles: true, out string snapshotError);
            _saveError = snapshotError ?? string.Empty;

            Debug.Log(
                $"{Marker} SAVE root='{_saveRoot}' filesBefore={_saveBefore.Length} budget={_saveWaitBudget:F0}s" +
                (snapshotError != null ? $" snapshotError={snapshotError}" : string.Empty));
            LogSaveDirectoryListing("BEFORE", _saveBefore);

            Hecton8.Core.IAsyncPersistenceService service = GlobalRegistry.AsyncPersistence;
            if (service == null)
            {
                FinishSaveLeg(
                    MomentVerdict.Blocked,
                    "GlobalRegistry.AsyncPersistence is null after world load - no persistence owner is " +
                    "published, so no save can be requested on this route",
                    countFailure: true);
                return;
            }

            _saveSlotName = Hecton8.SaveSystem.SaveEvents.ResolveManualSlotName(_saveSlotIndex);
            _saveBusyObserved = service.IsBusy;

            bool accepted;
            try
            {
                accepted = service.TryRequestSave(_saveSlotIndex, ProbeSaveSourceHash);
            }
            catch (Exception ex)
            {
                FinishSaveLeg(
                    MomentVerdict.Fail,
                    $"TryRequestSave threw {ex.GetType().Name}: {ex.Message}",
                    countFailure: true);
                return;
            }

            _saveAccepted = accepted;
            Debug.Log(
                $"{Marker} SAVE TryRequestSave(slot={_saveSlotIndex} '{_saveSlotName}') -> {accepted} " +
                $"owner={service.GetType().Name} isInitialized={service.IsInitialized} " +
                $"isBusyAtRequest={_saveBusyObserved} playTimeSeconds={service.CurrentPlayTimeSeconds:F1}");

            if (!accepted)
            {
                FinishSaveLeg(
                    MomentVerdict.Fail,
                    $"TryRequestSave(slot '{_saveSlotName}') was refused: {ReadSaveServiceError()}",
                    countFailure: true);
            }
        }

        private static void PollSaveRoundTrip()
        {
            double now = EditorApplication.timeSinceStartup;
            _saveWaitedSeconds = now - _saveLegStartedAt;

            Hecton8.Core.IAsyncPersistenceService service = GlobalRegistry.AsyncPersistence;
            bool busy = service != null && service.IsBusy;
            if (busy)
            {
                _saveBusyObserved = true;
                _saveBusyClearedAt = 0.0;
            }
            else if (_saveBusyObserved && _saveBusyClearedAt <= 0.0)
            {
                // IsBusy can clear a moment before the last buffer reaches the filesystem, so a cleared
                // flag opens a short grace window rather than deciding the verdict on the spot.
                _saveBusyClearedAt = now;
            }

            bool budgetSpent = _saveWaitedSeconds >= _saveWaitBudget;
            bool graceSpent = _saveBusyClearedAt > 0.0 && now - _saveBusyClearedAt >= SaveFlushGraceSeconds;
            if (!budgetSpent && !graceSpent && now - _saveLastPollAt < SaveDiffPollSeconds)
                return;

            _saveLastPollAt = now;

            // Metadata-only while polling. Hashing every save file once a second for the whole budget
            // would re-read the very files being written and slow the run this probe is measuring;
            // name, byte length and UTC write ticks are enough to notice that something moved.
            SaveFileFacts[] after = SnapshotSaveDirectory(_saveRoot, hashSaveFiles: false, out _);
            SaveDirectoryDiff cheapDiff = DiffSaveDirectory(_saveBefore, after);

            bool decided = cheapDiff.TotalChanges > 0 || budgetSpent || graceSpent;
            if (!decided)
                return;

            // One hashed pass, at the decision point only, so the recorded diff is byte-true rather
            // than "the timestamp moved".
            after = SnapshotSaveDirectory(_saveRoot, hashSaveFiles: true, out _);
            _saveFilesAfter = after.Length;
            _saveDiff = DiffSaveDirectory(_saveBefore, after);
            LogSaveDirectoryListing("AFTER", after);

            if (_saveDiff.TotalChanges > 0)
            {
                Debug.Log(
                    $"{Marker} SAVE DIFF added={_saveDiff.Added} removed={_saveDiff.Removed} " +
                    $"changed={_saveDiff.Changed} byteDelta={_saveDiff.ByteDelta} " +
                    $"after {_saveWaitedSeconds:F1}s{_saveDiff.Lines}");
                FinishSaveLeg(
                    MomentVerdict.Partial,
                    $"save half observed: TryRequestSave(slot '{_saveSlotName}') changed " +
                    $"{_saveDiff.TotalChanges} file(s) under '{_saveRoot}' in {_saveWaitedSeconds:F1}s " +
                    $"(byteDelta={_saveDiff.ByteDelta}). The LOAD half of this row is not exercised by " +
                    "this probe, so the row is not accepted.",
                    countFailure: false);
                return;
            }

            string why = graceSpent
                ? $"the persistence owner reported IsBusy=false at least {SaveFlushGraceSeconds:F0}s ago"
                : $"the {_saveWaitBudget:F0}s save budget elapsed";
            FinishSaveLeg(
                MomentVerdict.Fail,
                $"TryRequestSave(slot '{_saveSlotName}') was accepted but not one byte changed under " +
                $"'{_saveRoot}' in {_saveWaitedSeconds:F1}s ({why}); {ReadSaveServiceError()}",
                countFailure: true);
        }

        private static void FinishSaveLeg(MomentVerdict verdict, string detail, bool countFailure)
        {
            if (countFailure)
                _failures++;

            _saveError = detail;
            RecordMoment(MomentSaveLoad, verdict, detail);
            Debug.Log($"{Marker} SAVE {DescribeVerdict(verdict)} {detail}");
            SetPhase(Phase.Reporting);
        }

        private static string ReadSaveServiceError()
        {
            Hecton8.SaveSystem.SaveManager runtime = GlobalRegistry.SaveRuntime;
            if (runtime == null)
                return "concrete SaveManager not reachable through GlobalRegistry.SaveRuntime";

            string error = runtime.LastOperationError;
            string slot = runtime.LastOperationSlot;
            return string.IsNullOrEmpty(error)
                ? $"SaveManager published no LastOperationError (LastOperationSlot='{slot}')"
                : $"SaveManager.LastOperationError='{error}' LastOperationSlot='{slot}'";
        }

        /// <summary>
        /// Lists the persistent root at file granularity: name, byte length, UTC write ticks, and an
        /// FNV-1a 64 content hash for the save-slot files themselves.
        ///
        /// Deliberately does not use <c>File.ReadAllBytes</c> - it is on the project's banned API list
        /// and would materialise whole save files. Content is streamed through one reusable 64 KB
        /// buffer, and anything above <see cref="MaxHashedFileBytes"/> is reported as unhashed rather
        /// than silently hashed to zero, so "hash matched" can never mean "hash was never taken".
        /// </summary>
        private static SaveFileFacts[] SnapshotSaveDirectory(string root, bool hashSaveFiles, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                error = $"persistent root '{root}' does not exist";
                return Array.Empty<SaveFileFacts>();
            }

            string[] paths;
            try
            {
                paths = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly);
            }
            catch (IOException ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return Array.Empty<SaveFileFacts>();
            }
            catch (UnauthorizedAccessException ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return Array.Empty<SaveFileFacts>();
            }

            Array.Sort(paths, StringComparer.Ordinal);

            var facts = new SaveFileFacts[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                string name = Path.GetFileName(paths[i]);
                long length = -1L;
                long writeTicks = 0L;

                try
                {
                    var info = new FileInfo(paths[i]);
                    length = info.Length;
                    writeTicks = info.LastWriteTimeUtc.Ticks;
                }
                catch (IOException)
                {
                    // A file being rewritten right now still counts as present; length stays -1 and the
                    // diff reads that as a change, which is the correct conclusion.
                }
                catch (UnauthorizedAccessException)
                {
                }

                bool hashed = false;
                ulong hash = 0UL;
                if (hashSaveFiles && length >= 0L && IsSaveSlotFile(name))
                    hash = ComputeFileHashFnv1a64(paths[i], length, out hashed);

                facts[i] = new SaveFileFacts
                {
                    Name = name,
                    Length = length,
                    WriteTicksUtc = writeTicks,
                    ContentHash = hash,
                    Hashed = hashed,
                };
            }

            return facts;
        }

        private static bool IsSaveSlotFile(string fileName)
        {
            // SaveManager.cs:6830-6841 names slot files "<slot>.sav", "<slot>.sav.bak[N]" and
            // "<slot>.sav.tmp"; ".diag" at :6842 is a sidecar of the same transaction.
            return fileName.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".diag", StringComparison.OrdinalIgnoreCase) ||
                   fileName.IndexOf(".sav.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ulong ComputeFileHashFnv1a64(string path, long length, out bool hashed)
        {
            hashed = false;
            if (length > MaxHashedFileBytes)
                return 0UL;

            if (_hashBuffer == null)
            {
                // COLD ALLOC: byte[65536] - reused save-file hash window - owner: H8_HeadlessPlayModeProbe
                _hashBuffer = new byte[HashChunkBytes];
            }

            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;

            try
            {
                using (var stream = new FileStream(
                           path,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite,
                           HashChunkBytes,
                           FileOptions.SequentialScan))
                {
                    int read;
                    while ((read = stream.Read(_hashBuffer, 0, HashChunkBytes)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            hash ^= _hashBuffer[i];
                            hash *= prime;
                        }
                    }
                }
            }
            catch (IOException)
            {
                return 0UL;
            }
            catch (UnauthorizedAccessException)
            {
                return 0UL;
            }

            hashed = true;
            return hash;
        }

        /// <summary>
        /// Ordered merge of two name-sorted listings. Added, removed, and changed are counted
        /// separately because they mean different things: a fresh slot adds, a rewrite changes, and a
        /// temp file that appears and disappears within one poll window is invisible to both - which
        /// is why the byte delta and the write ticks are carried as well as the name.
        /// </summary>
        private static SaveDirectoryDiff DiffSaveDirectory(SaveFileFacts[] before, SaveFileFacts[] after)
        {
            var diff = new SaveDirectoryDiff();
            var lines = new StringBuilder(256);
            int emitted = 0;
            int i = 0;
            int j = 0;

            while (i < before.Length || j < after.Length)
            {
                int order;
                if (i >= before.Length)
                    order = 1;
                else if (j >= after.Length)
                    order = -1;
                else
                    order = string.CompareOrdinal(before[i].Name, after[j].Name);

                if (order < 0)
                {
                    diff.Removed++;
                    diff.ByteDelta -= Math.Max(0L, before[i].Length);
                    AppendDiffLine(lines, ref emitted, "-", before[i].Name, before[i].Length, -1L);
                    i++;
                }
                else if (order > 0)
                {
                    diff.Added++;
                    diff.ByteDelta += Math.Max(0L, after[j].Length);
                    AppendDiffLine(lines, ref emitted, "+", after[j].Name, -1L, after[j].Length);
                    j++;
                }
                else
                {
                    bool contentDiffers = before[i].Hashed && after[j].Hashed &&
                                          before[i].ContentHash != after[j].ContentHash;
                    if (before[i].Length != after[j].Length ||
                        before[i].WriteTicksUtc != after[j].WriteTicksUtc ||
                        contentDiffers)
                    {
                        diff.Changed++;
                        diff.ByteDelta += after[j].Length - before[i].Length;
                        AppendDiffLine(lines, ref emitted, "~", after[j].Name, before[i].Length, after[j].Length);
                    }

                    i++;
                    j++;
                }
            }

            diff.Lines = lines.ToString();
            return diff;
        }

        private static void AppendDiffLine(
            StringBuilder builder,
            ref int emitted,
            string sign,
            string name,
            long beforeLength,
            long afterLength)
        {
            if (emitted == MaxDiffLines)
            {
                builder.Append("\n  ... further entries truncated at ").Append(MaxDiffLines);
                emitted++;
                return;
            }

            if (emitted > MaxDiffLines)
                return;

            builder.Append("\n  ").Append(sign).Append(' ').Append(name)
                .Append(" bytesBefore=").Append(beforeLength)
                .Append(" bytesAfter=").Append(afterLength);
            emitted++;
        }

        private static void LogSaveDirectoryListing(string label, SaveFileFacts[] facts)
        {
            var builder = new StringBuilder(256);
            int emitted = 0;
            for (int i = 0; i < facts.Length; i++)
            {
                if (!IsSaveSlotFile(facts[i].Name))
                    continue;

                if (emitted >= MaxDiffLines)
                {
                    builder.Append("\n  ... further save files truncated at ").Append(MaxDiffLines);
                    break;
                }

                builder.Append("\n  ").Append(facts[i].Name)
                    .Append(" bytes=").Append(facts[i].Length)
                    .Append(" writeUtcTicks=").Append(facts[i].WriteTicksUtc)
                    .Append(" fnv1a64=")
                    .Append(facts[i].Hashed
                        ? facts[i].ContentHash.ToString("X16", CultureInfo.InvariantCulture)
                        : "unread");
                emitted++;
            }

            Debug.Log($"{Marker} SAVE {label} files={facts.Length} saveSlotFiles={emitted}{builder}");
        }

        private static void RunChecks()
        {
            Debug.Log($"{Marker} --- checks at frame {_frames} ---");

            try
            {
                ReportClockRates();
                ReportSceneRuntimeService();
                ReportBootstrapReadiness();
                CheckWorldSeed();
                ReportRegistryPresence();
                ReportRegistryRejections();
                ReportRuntimeComponentCensus();
                ReportVegetationVertexInputs();
            }
            catch (Exception ex)
            {
                Debug.Log($"{Marker} CHECK THREW {ex.GetType().Name}: {ex.Message}");
                _failures++;
            }

            RecordProofMoment();
            ReportRouteMoments();

            Debug.Log($"{Marker} RESULT failures={_failures}");
        }

        /// <summary>
        /// Prints all ten rows of the First 20 Minutes Required Route table with the verdict this run
        /// actually produced. Rows with no producer print <c>NOT_EXERCISED</c> by construction, so the
        /// gap between "the harness ran" and "the route is proven" is visible in the harness's own
        /// output instead of having to be reconstructed by grepping a megabyte of log.
        /// </summary>
        private static void ReportRouteMoments()
        {
            int pass = 0;
            int partial = 0;
            int fail = 0;
            int blocked = 0;
            int notExercised = 0;

            for (int i = 0; i < _routeMoments.Count; i++)
            {
                RouteMoment moment = _routeMoments[i];
                switch (moment.Verdict)
                {
                    case MomentVerdict.Pass:
                        pass++;
                        break;
                    case MomentVerdict.Partial:
                        partial++;
                        break;
                    case MomentVerdict.Fail:
                        fail++;
                        break;
                    case MomentVerdict.Blocked:
                        blocked++;
                        break;
                    default:
                        notExercised++;
                        break;
                }

                Debug.Log(
                    $"{Marker} MOMENT   {DescribeVerdict(moment.Verdict),-13} {moment.Name,-18} {moment.Detail}");
            }

            Debug.Log(
                $"{Marker} MOMENTS pass={pass} partial={partial} fail={fail} blocked={blocked} " +
                $"notExercised={notExercised} of {_routeMoments.Count} Required Route rows. " +
                "Only pass is acceptance; partial means one half of a two-part row was observed and " +
                "the row is NOT accepted.");
        }

        private static string DescribeVerdict(MomentVerdict verdict)
        {
            switch (verdict)
            {
                case MomentVerdict.Pass:
                    return "PASS";
                case MomentVerdict.Partial:
                    return "PARTIAL";
                case MomentVerdict.Fail:
                    return "FAIL";
                case MomentVerdict.Blocked:
                    return "BLOCKED";
                default:
                    return "NOT_EXERCISED";
            }
        }

        private static void SeedRouteMoments()
        {
            _routeMoments.Clear();
            AddRouteMoment(MomentBoot, "00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD");
            AddRouteMoment(MomentWorldLoad, "selected semi-open shallow route loads");
            AddRouteMoment(MomentFirstExit, "player exits into readable photic water");
            AddRouteMoment(MomentSwim, "navigate, surface/dive, read oxygen/pressure/depth");
            AddRouteMoment(MomentResource, "world object reaches inventory");
            AddRouteMoment(MomentTool, "one tool interaction is useful on the route");
            AddRouteMoment(MomentCraft, "one recipe/repair consumes the resource");
            AddRouteMoment(MomentHazard, "one fair hazard creates a decision");
            AddRouteMoment(MomentSaveLoad, "save, quit/reload, return to the same state");
            AddRouteMoment(MomentProof, "console, run, profiler, GC, memory, capture, save directory diff");
        }

        private static void AddRouteMoment(string name, string minimumAcceptance)
        {
            _routeMoments.Add(new RouteMoment
            {
                Name = name,
                Verdict = MomentVerdict.NotExercised,
                Detail = "no producer in this probe - minimum acceptance: " + minimumAcceptance,
            });
        }

        /// <summary>
        /// Records the verdict for one Required Route row. A worse verdict never overwrites a better
        /// one silently: the first non-<see cref="MomentVerdict.NotExercised"/> verdict wins unless the
        /// new one is more severe, so a late blocked/failed observation cannot be masked by an early
        /// optimistic one.
        /// </summary>
        private static void RecordMoment(string name, MomentVerdict verdict, string detail)
        {
            for (int i = 0; i < _routeMoments.Count; i++)
            {
                RouteMoment moment = _routeMoments[i];
                if (!string.Equals(moment.Name, name, StringComparison.Ordinal))
                    continue;

                if (verdict < moment.Verdict)
                    return;

                moment.Verdict = verdict;
                moment.Detail = detail ?? string.Empty;
                _routeMoments[i] = moment;
                return;
            }
        }

        /// <summary>
        /// A headless boot reaches 01_MAIN_MENU and stops there, because gameplay systems are
        /// installed only once a game is actually started. On arrival BootstrapRouteEnforcer finds
        /// AreAllSystemsReady() false and tries to reload 00_BOOTSTRAP, which then fails with
        /// "Failed to schedule async bootstrap recovery load".
        ///
        /// AreAllSystemsReady is `_isBootstrapComplete && Dispatcher && TickManager && Save &&
        /// ObjectPool`, so this breaks the verdict into its parts: the aggregate says "not ready"
        /// without saying which term failed, and the difference between "boot never completed" and
        /// "one service is missing" decides where to look next.
        ///
        /// Reports only; a headless boot legitimately does not finish, so this fails nothing.
        /// </summary>
        private static void ReportBootstrapReadiness()
        {
            bool ready = global::Hecton8.Bootstrap.GameBootstrapper.AreAllSystemsReady();

            Debug.Log(
                $"{Marker} BOOTSTRAP allSystemsReady={ready} " +
                $"Dispatcher={(IsAlive(GlobalRegistry.Dispatcher) ? "ok" : "null")} " +
                $"TickManager={(IsAlive(GlobalRegistry.TickManager) ? "ok" : "null")} " +
                $"Save={(IsAlive(GlobalRegistry.Save) ? "ok" : "null")} " +
                $"ObjectPool={(IsAlive(GlobalRegistry.ObjectPool) ? "ok" : "null")}");

            // Every loaded scene, not just the active one: "sceneCount=2" with no menu in sight was
            // the single most misleading line the first version of this probe printed.
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scenes = new StringBuilder();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scenes.Length > 0)
                    scenes.Append(", ");

                scenes.Append('[').Append(i).Append("] '").Append(scene.name).Append("' loaded=")
                    .Append(scene.isLoaded)
                    .Append(" roots=")
                    .Append(scene.isLoaded ? scene.rootCount : 0);
            }

            Debug.Log($"{Marker} SCENE active='{active.name}' count={UnityEngine.SceneManagement.SceneManager.sceneCount} -> {scenes}");

            // Verdict only; _failures is deliberately untouched here so the exit code keeps its
            // existing meaning. A headless boot that stops at 01_MAIN_MENU is a legitimate outcome
            // of the harness, and it is still a Boot-row failure against the Required Route.
            RecordMoment(
                MomentBoot,
                ready ? MomentVerdict.Pass : MomentVerdict.Fail,
                $"allSystemsReady={ready} Dispatcher={IsAlive(GlobalRegistry.Dispatcher)} " +
                $"TickManager={IsAlive(GlobalRegistry.TickManager)} Save={IsAlive(GlobalRegistry.Save)} " +
                $"ObjectPool={IsAlive(GlobalRegistry.ObjectPool)} activeScene='{active.name}'");
        }

        /// <summary>
        /// The world seed reached every procedural generator as 0 because nothing implemented
        /// IWorldSeedProvider. This is the check that says whether that is still true.
        /// </summary>
        private static void CheckWorldSeed()
        {
            IWorldSeedProvider provider = GlobalRegistry.WorldSeedProvider;
            if (provider == null)
            {
                Debug.Log($"{Marker} WORLDSEED provider=NULL - every procedural generator is running on seed 0");
                _failures++;
                return;
            }

            Debug.Log(
                $"{Marker} WORLDSEED provider={provider.GetType().Name} initialized={provider.IsInitialized} " +
                $"seed={provider.RuntimeWorldSeed} versionId={provider.RuntimeWorldGenerationVersionId}");

            if (!provider.IsInitialized)
            {
                Debug.Log($"{Marker} WORLDSEED provider present but NOT initialized - consumers still take the 0 path");
                _failures++;
                return;
            }

            // The seven MapMagic / procedural consumers go through this static, not the registry.
            bool resolved = global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int staticSeed);
            Debug.Log($"{Marker} WORLDSEED static TryGetActiveRuntimeWorldSeed -> {resolved} seed={staticSeed}");

            if (!resolved || staticSeed == 0)
            {
                Debug.Log($"{Marker} WORLDSEED static path still returns nothing - the MapMagic nodes remain on 0");
                _failures++;
                return;
            }

            if (staticSeed != provider.RuntimeWorldSeed)
            {
                Debug.Log($"{Marker} WORLDSEED MISMATCH static={staticSeed} registry={provider.RuntimeWorldSeed}");
                _failures++;
            }
        }

        /// <summary>
        /// Reports presence only, and fails nothing. A missing service here is information about
        /// what this scene boots, not a defect on its own.
        /// </summary>
        private static void ReportRegistryPresence()
        {
            var slots = new List<KeyValuePair<string, object>>
            {
                new("Terrain", GlobalRegistry.Terrain),
                new("FaunaGenetics", GlobalRegistry.FaunaGenetics),
                new("Player", GlobalRegistry.Player),
                new("Save", GlobalRegistry.Save),
                new("TickDispatcher", GlobalRegistry.TickDispatcher),
                new("VoxelEngine", GlobalRegistry.VoxelEngine),
            };

            var builder = new StringBuilder();
            foreach (KeyValuePair<string, object> slot in slots)
            {
                if (builder.Length > 0)
                    builder.Append("  ");

                builder.Append(slot.Key).Append('=');
                builder.Append(IsAlive(slot.Value) ? slot.Value.GetType().Name : "null");
            }

            Debug.Log($"{Marker} REGISTRY {builder}");
        }

        /// <summary>
        /// Captures every "Ready-locked registry rejected registration" the run produces.
        ///
        /// This exists because the failure it catches is INTERMITTENT and its symptom looks trivial.
        /// Three identical runs: two came up with 02_HECTON_WORLD roots=37 and a live terrain
        /// provider, the third with roots=30, Terrain=null, FaunaGenetics=null. The summary made
        /// that look like a slow load. It was not. The third run's log carried 31 distinct
        /// CriticalBootException rejections - MapMagicBridge, HectonPlayerMotor, QuestManager,
        /// HectonCelestialEngine, WorldProceduralFieldSampler, DepthZoneDirector, EndingSystem,
        /// SoundscapeSystem and 23 more - and the good runs carried zero. The scene-owned service
        /// layer simply did not register, and the only way to know was grepping a 1 MB log for a
        /// string nobody had thought to look for.
        ///
        /// GlobalRegistry.GuardServicePublication throws whenever Phase == Ready and no
        /// ForceOverrideToken is present. GameBootstrapper opens a window for exactly this
        /// (BeginSceneRuntimePublicationGate / EndSceneRuntimePublicationGate, three call sites);
        /// inside it a hot-swap token is issued and these registrations pass. So the race is
        /// whether 02_HECTON_WORLD's scene-owned OnEnable calls land inside that window. Diagnosis
        /// only - the ordering fix belongs to whoever owns GlobalRegistry and the bootstrapper, and
        /// guessing at activation order is how this class of bug gets made.
        ///
        /// This used to be true and is no longer: only the FIRST rejection was logged as an error,
        /// because GlobalRegistry gated it on a single global _readyLockViolationLogged flag, so
        /// counting LogError hits undercounted 31 down to 1. That flag is now a per-type latch stamped
        /// with a reset generation, and each message carries a running "#N" count, so every distinct
        /// rejected service reports itself once and the scale is visible on the first line.
        ///
        /// This hook is still the belt to that braces: it catches repeats of an already-logged type,
        /// and it does not depend on GlobalRegistry's own DEVELOPMENT_BUILD-guarded logging being
        /// compiled in at all.
        /// </summary>
        private static void InstallRegistryRejectionHook()
        {
            if (_logHookInstalled)
                return;

            _logHookInstalled = true;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(condition) || condition.IndexOf(ReadyLockRejection, StringComparison.Ordinal) < 0)
                return;

            // The service name is the tail of "...rejected registration: Name".
            int colon = condition.LastIndexOf(':');
            string service = colon >= 0 && colon < condition.Length - 1
                ? condition.Substring(colon + 1).Trim()
                : condition.Trim();

            if (!_rejectedServices.Contains(service))
                _rejectedServices.Add(service);
        }

        private static void ReportRegistryRejections()
        {
            if (_rejectedServices.Count == 0)
            {
                Debug.Log($"{Marker} REGISTRYLOCK none - every scene-owned service registered inside the publication gate");
                return;
            }

            Debug.Log(
                $"{Marker} REGISTRYLOCK {_rejectedServices.Count} services were REJECTED by the ready-locked " +
                "registry. This world is a shell: the scene-owned service layer did not publish. " +
                "Intermittent - a rerun may come up clean, which is what makes it dangerous.");

            var builder = new StringBuilder();
            for (int i = 0; i < _rejectedServices.Count; i++)
            {
                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(_rejectedServices[i]);
            }

            Debug.Log($"{Marker} REGISTRYLOCK   {builder}");
            _failures++;
        }

        /// <summary>
        /// Counts live instances of the runtime owners whose existence is in question.
        ///
        /// H8_SceneCompositionCensus answers whether a component is AUTHORED into a scene. It
        /// found HectonIndirectVegetationRenderer, HectonFluidEngine, FloraInteractionManager,
        /// GpuScatterLodManager and HectonVoxelEngine absent from all three boot scenes - but it
        /// also found FaunaGeneticsManager absent, and this probe proves that one exists at
        /// runtime because EcosystemRuntimeInstaller AddComponents it. Authoring absence is
        /// therefore not runtime absence, and only this side of the pair can close the question.
        ///
        /// FindObjectsByType also fixes the blind spot in every other search here:
        /// SceneManager.GetSceneAt does not enumerate the DontDestroyOnLoad scene, so root
        /// traversal cannot see persistent objects. This can.
        ///
        /// Self-test: cross-check the census against GlobalRegistry IN THIS RUN. If the registry
        /// holds a live FaunaGeneticsManager and the census cannot see it, the census is broken
        /// and every "0 instances" below is worthless.
        ///
        /// The first version of this self-test asserted FaunaGeneticsManager unconditionally,
        /// because two consecutive runs had it. The third run did not: the world came up with
        /// roots=30 instead of 37, Terrain and FaunaGenetics both null. The assertion fired and
        /// suppressed a census that was working correctly. A known-answer case has to be read
        /// from the same run, not from the last one - which is also why the boot being
        /// non-deterministic matters more than any single reading here.
        /// </summary>
        private static void ReportRuntimeComponentCensus()
        {
            string[] watched =
            {
                "HectonIndirectVegetationRenderer",
                "HectonFluidEngine",
                "FloraInteractionManager",
                "GpuScatterLodManager",
                "HectonVoxelEngine",
                "HectonWorldGenerator",
                "GameBootstrapper",
                "FaunaGeneticsManager",

                // The three player components that no prefab carries and no shipped code created until
                // PlayerRuntimeContextService started installing them. Named here because the aggregate
                // "N live MonoBehaviours" line cannot answer whether they exist: subtracting one run's total
                // from another's is invalid across a boot-quality boundary, since a run where the registry
                // ready-lock rejected scene services aborts each owner's OnEnable partway and therefore
                // builds a different component population, not merely a smaller one.
                "HectonPlayerHealth",
                "TraumaDispatcher",
                "PlayerTransportCoordinator",

                // Two controls, and they are the point of the addition rather than padding.
                // PlayerKinematicsRuntime is installed by the SAME method on the same line group and predates
                // that change, so it proves the cold sync path ran at all - if it is NONE, the three above
                // being NONE says nothing about their install and everything about the path never executing.
                // HectonSurvivalSystem is authored on Player.prefab, so it proves the player root still exists
                // at census time. Read the enabled= column on all five: the bootstrap disables the player root
                // during the Kinematic Arrest Gate, so instances=1 enabled=0 is a materially different
                // outcome from instances=0 and the two must not be conflated.
                "PlayerKinematicsRuntime",
                "HectonSurvivalSystem",
            };

            MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var total = new Dictionary<string, int>(StringComparer.Ordinal);
            var enabled = new Dictionary<string, int>(StringComparer.Ordinal);
            var where = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour behaviour = all[i];
                if (behaviour == null)
                    continue;

                string typeName = behaviour.GetType().Name;
                total.TryGetValue(typeName, out int seen);
                total[typeName] = seen + 1;
                if (behaviour.isActiveAndEnabled)
                {
                    enabled.TryGetValue(typeName, out int live);
                    enabled[typeName] = live + 1;
                }

                if (!where.ContainsKey(typeName))
                    where[typeName] = behaviour.gameObject.scene.name ?? "<no scene>";
            }

            Debug.Log($"{Marker} RUNTIME CENSUS {all.Length} live MonoBehaviours, {total.Count} distinct types");

            object registryFauna = GlobalRegistry.FaunaGenetics;
            bool registryHasFauna = IsAlive(registryFauna);
            if (registryHasFauna && !total.ContainsKey("FaunaGeneticsManager"))
            {
                Debug.Log(
                    $"{Marker} RUNTIME CENSUS SELF-TEST FAILED - GlobalRegistry holds a live " +
                    "FaunaGeneticsManager in this run and the census cannot see it. Every zero below is void.");
                _failures++;
                return;
            }

            Debug.Log(
                registryHasFauna
                    ? $"{Marker} RUNTIME CENSUS   self-test ok - census agrees with GlobalRegistry on FaunaGeneticsManager"
                    : $"{Marker} RUNTIME CENSUS   self-test SKIPPED - GlobalRegistry has no FaunaGenetics this run, " +
                      "so there is no positive case to check the census against. Zeros below are UNVALIDATED.");

            foreach (string typeName in watched)
            {
                total.TryGetValue(typeName, out int count);
                enabled.TryGetValue(typeName, out int live);
                string scene = count > 0 && where.TryGetValue(typeName, out string found) ? found : "-";
                string verdict = count == 0 ? "NONE  " : "EXISTS";
                Debug.Log($"{Marker} RUNTIME CENSUS   {verdict} {typeName,-34} instances={count} enabled={live} firstScene={scene}");
            }
        }

        /// <summary>
        /// Reports whether the four vegetation passes are actually FED anything.
        ///
        /// H8_VegetationPassParityProbe proves the ForwardLit / Shadow / DepthOnly / MotionVectors
        /// passes compute the same vertex position from the same inputs. It says nothing about
        /// whether those inputs carry a value at runtime, and a term whose uniform is never
        /// published contributes exactly zero in all four passes - agreement on nothing. Every
        /// input below is a C# global (Shader.SetGlobal*), not a material property, so it is
        /// readable from here.
        ///
        /// HONEST LIMIT, do not read past it: Shader.GetGlobal* returns zero for a name that was
        /// never set AND for a name deliberately set to zero. ZERO therefore means "contributes
        /// nothing right now", never "nobody publishes it". Only PUBLISHED is a positive result.
        /// A single sample at one instant also cannot see a value that is only non-zero while the
        /// player is moving through flora.
        ///
        /// _HectonImpactSphereCount is the known-answer case that keeps this honest. It was found
        /// statically to have no producer anywhere in C# (GpuScatterLodManager only ever writes 0
        /// and the _HectonImpactSpheres buffer is never bound), so it MUST read zero. If it ever
        /// reads non-zero, that static finding was wrong and ResolveImpactOffset is live code whose
        /// lit-vs-DepthOnly divergence has been shipping.
        /// </summary>
        private static void ReportVegetationVertexInputs()
        {
            Debug.Log(
                $"{Marker} VEGINPUT (ZERO = contributes nothing at this instant, NOT proof of a missing publisher)");

            ReportGlobalVector("_AbyssalGridResolution", "abyssal flow grid - zero disables ResolveAbyssalFlowField in all 4 passes");
            ReportGlobalFloat("_AbyssalFlowTextureActive", "abyssal flow texture path");
            ReportGlobalVector("_AbyssalFlowSpacing", "abyssal flow cell size");
            ReportGlobalFloat("_HectonFloraInteractionCount", "ResolveInteractionOffset input count");
            ReportGlobalFloat("_HectonFloraWakeCount", "ResolveWakeTrailOffset input count");
            ReportGlobalVector("_HectonFloraWakeParams", "wake trail tuning");
            ReportGlobalVector("_HectonPlayerFloraInteractionParams", "ResolvePlayerBendOffset input");
            ReportGlobalVector("_HectonFloraSwayFieldParams", "sway field - couples to the fieldDrivenBend suppression");
            ReportGlobalFloat("_HectonShallowWaterFieldActive", "shallow water field feeding the wake trail");
            ReportGlobalVector("_HectonFlowSynchronyParams", "ResolveFlowSynchronyOffset tuning");
            ReportGlobalFloat("_HectonFloraSnapFlagsEnabled", "flora snap subsystem");

            float impactSpheres = Shader.GetGlobalFloat("_HectonImpactSphereCount");
            if (impactSpheres > 0.5f)
            {
                Debug.Log(
                    $"{Marker} VEGINPUT   CONTRADICTION _HectonImpactSphereCount={impactSpheres} - " +
                    "ResolveImpactOffset was recorded as dead code with no producer. It is not. " +
                    "Its lit-vs-DepthOnly divergence is live and must be converged.");
                _failures++;
            }
            else
            {
                Debug.Log(
                    $"{Marker} VEGINPUT   self-test ok _HectonImpactSphereCount=0 as the static audit predicted");
            }
        }

        private static void ReportGlobalVector(string name, string meaning)
        {
            Vector4 value = Shader.GetGlobalVector(name);
            string verdict = value == Vector4.zero ? "ZERO     " : "PUBLISHED";
            Debug.Log($"{Marker} VEGINPUT   {verdict} {name,-42} {value.ToString("F3", CultureInfo.InvariantCulture)}  {meaning}");
        }

        private static void ReportGlobalFloat(string name, string meaning)
        {
            float value = Shader.GetGlobalFloat(name);
            string verdict = value == 0f ? "ZERO     " : "PUBLISHED";
            Debug.Log($"{Marker} VEGINPUT   {verdict} {name,-42} {value.ToString("F3", CultureInfo.InvariantCulture)}  {meaning}");
        }

        private static bool IsAlive(object service)
        {
            if (service is UnityEngine.Object unityObject)
                return unityObject != null;

            return service != null;
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            if (_logHookInstalled)
            {
                Application.logMessageReceived -= OnLogMessage;
                _logHookInstalled = false;
            }

            CloseClockSegment();
            Phase finalPhase = _phase;
            _phase = Phase.Idle;
            _clockPhase = Phase.Idle;

            // Every terminal path writes the artifact, including ABORT, TIMEOUT and a boot that left
            // Play Mode early. That is the point: a run that failed early is exactly the run whose
            // numbers someone will want to compare against a run that succeeded.
            WriteRouteArtifact(exitCode, finalPhase);

            Debug.Log($"{Marker} DONE exitCode={exitCode}");

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Clears every static this probe carries across a run.
        ///
        /// The class relies on DisableDomainReload to survive the Play Mode transition, which also
        /// means nothing resets these between two invocations in one editor session. A second run
        /// would otherwise inherit the first run's phase table, moment verdicts and rejected-service
        /// list and report them as its own.
        /// </summary>
        private static void ResetRunState()
        {
            _phase = Phase.Idle;
            _clockPhase = Phase.Idle;
            _clockSegmentOpen = false;
            _clockWallStart = 0.0;
            _clockGameFrameStart = 0;
            _clockTickStart = 0;
            _totalTicks = 0;
            _phaseSamples.Clear();
            _rejectedServices.Clear();
            SeedRouteMoments();

            _frames = 0;
            _failures = 0;
            _menuFrames = 0;
            _settleFrames = 0;
            _gameplayFrames = 0;
            _menuLoad = null;
            _phaseStartedAt = 0.0;
            _playStartedAt = 0.0;
            _gameFrameAtPlayStart = 0;

            _saveRequestIssued = false;
            _saveAccepted = false;
            _saveBusyObserved = false;
            _saveBusyClearedAt = 0.0;
            _saveLegStartedAt = 0.0;
            _saveLastPollAt = 0.0;
            _saveWaitedSeconds = 0.0;
            _saveWaitBudget = 0.0;
            _saveRoot = string.Empty;
            _saveSlotName = string.Empty;
            _saveError = string.Empty;
            _saveBefore = Array.Empty<SaveFileFacts>();
            _saveFilesAfter = 0;
            _saveDiff = default;

            _artifactWritten = false;
        }

        private static string ResolveArtifactPath()
        {
            return string.IsNullOrEmpty(_artifactPath) ? ResolveDefaultArtifactPath() : _artifactPath;
        }

        /// <summary>
        /// The contract's Proof row (<c>FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:90</c>) wants
        /// console, run, profiler, GC, memory, screenshot/clip AND the save directory diff. This run
        /// produces the run log, the per-phase clock table and the save directory diff; profiler, GC,
        /// memory and capture have no producer here, so the row is Partial and names what is missing.
        ///
        /// Recorded from both the console report and the artifact writer so the two outputs of one run
        /// can never disagree about it.
        /// </summary>
        private static void RecordProofMoment()
        {
            RecordMoment(
                MomentProof,
                MomentVerdict.Partial,
                $"run log + per-phase clock table + save directory diff written to '{ResolveArtifactPath()}'; " +
                "profiler capture, GC evidence, memory snapshot and screenshot/clip have no producer in this probe");
        }

        private static string ResolveDefaultArtifactPath()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            string root = projectRoot != null ? projectRoot.FullName : Application.dataPath;

            // Logs/ is already gitignored, so a run artifact cannot be swept into a commit by the
            // working-tree cement job, and the probe's own usage line already writes its log there.
            return Path.Combine(root, "Logs", "h8_playprobe_route.json");
        }

        /// <summary>
        /// Writes one machine-readable record of the run.
        ///
        /// Four headless runs today used four different argument sets and left nothing behind but a
        /// megabyte of log each, so none of them could be compared with each other. One JSON with a
        /// fixed schema at a fixed default path is the fix; <c>-h8RouteArtifact</c> moves it when two
        /// runs must not overwrite one another.
        /// </summary>
        private static void WriteRouteArtifact(int exitCode, Phase finalPhase)
        {
            if (_artifactWritten)
                return;

            _artifactWritten = true;

            string path = ResolveArtifactPath();
            RecordProofMoment();

            var builder = new StringBuilder(4096);

            builder.Append("{\n");
            AppendJsonField(builder, "schema", "hecton8.playprobe.route.v1");
            AppendJsonField(builder, "utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            AppendJsonField(builder, "scene", _scenePath);
            AppendJsonNumber(builder, "exitCode", exitCode);
            AppendJsonNumber(builder, "failures", _failures);
            AppendJsonField(builder, "finalPhase", finalPhase.ToString());
            AppendJsonBool(builder, "batchmode", Application.isBatchMode);
            AppendJsonNumber(builder, "probeTicks", _totalTicks);
            AppendJsonNumber(builder, "gameFrames", Time.frameCount - _gameFrameAtPlayStart);
            AppendJsonNumber(
                builder,
                "playWallSeconds",
                _playStartedAt > 0.0 ? EditorApplication.timeSinceStartup - _playStartedAt : 0.0);

            builder.Append("  \"args\": {\n");
            AppendJsonNumber(builder, "warmupFrames", _warmupFrames, "    ");
            AppendJsonNumber(builder, "hardTimeoutSeconds", _hardTimeoutSeconds, "    ");
            AppendJsonNumber(builder, "menuWaitSeconds", _menuWaitSeconds, "    ");
            AppendJsonNumber(builder, "settleWaitSeconds", _settleWaitSeconds, "    ");
            AppendJsonNumber(builder, "gameplaySeconds", _gameplaySeconds, "    ");
            AppendJsonNumber(builder, "saveWaitSeconds", _saveWaitSeconds, "    ");
            AppendJsonNumber(builder, "saveSlot", _saveSlotIndex, "    ");
            AppendJsonBool(builder, "startNewGame", _startNewGame, "    ");
            AppendJsonBool(builder, "forceMenuLoad", _forceMenuLoad, "    ");
            AppendJsonBoolLast(builder, "saveLegEnabled", _saveLegEnabled, "    ");
            builder.Append("  },\n");

            builder.Append("  \"phases\": [\n");
            for (int i = 0; i < _phaseSamples.Count; i++)
            {
                PhaseSample sample = _phaseSamples[i];
                double phaseFps = sample.WallSeconds > 0.0 ? sample.GameFrames / sample.WallSeconds : 0.0;
                builder.Append("    { \"phase\": \"").Append(EscapeJson(sample.Phase))
                    .Append("\", \"wallSeconds\": ").Append(FormatNumber(sample.WallSeconds))
                    .Append(", \"probeTicks\": ").Append(sample.ProbeTicks)
                    .Append(", \"gameFrames\": ").Append(sample.GameFrames)
                    .Append(", \"gameFramesPerWallSecond\": ").Append(FormatNumber(phaseFps))
                    .Append(", \"duringPlay\": ").Append(sample.DuringPlay ? "true" : "false")
                    .Append(" }")
                    .Append(i == _phaseSamples.Count - 1 ? "\n" : ",\n");
            }

            builder.Append("  ],\n");

            builder.Append("  \"save\": {\n");
            AppendJsonField(builder, "root", _saveRoot, "    ");
            AppendJsonField(builder, "slot", _saveSlotName, "    ");
            AppendJsonBool(builder, "requested", _saveRequestIssued, "    ");
            AppendJsonBool(builder, "accepted", _saveAccepted, "    ");
            AppendJsonBool(builder, "busyObserved", _saveBusyObserved, "    ");
            AppendJsonNumber(builder, "waitedSeconds", _saveWaitedSeconds, "    ");
            AppendJsonNumber(builder, "filesBefore", _saveBefore.Length, "    ");
            AppendJsonNumber(builder, "filesAfter", _saveFilesAfter, "    ");
            AppendJsonNumber(builder, "added", _saveDiff.Added, "    ");
            AppendJsonNumber(builder, "removed", _saveDiff.Removed, "    ");
            AppendJsonNumber(builder, "changed", _saveDiff.Changed, "    ");
            AppendJsonNumber(builder, "byteDelta", _saveDiff.ByteDelta, "    ");
            AppendJsonField(builder, "diff", _saveDiff.Lines ?? string.Empty, "    ");
            AppendJsonFieldLast(builder, "note", _saveError ?? string.Empty, "    ");
            builder.Append("  },\n");

            builder.Append("  \"moments\": [\n");
            for (int i = 0; i < _routeMoments.Count; i++)
            {
                RouteMoment moment = _routeMoments[i];
                builder.Append("    { \"moment\": \"").Append(EscapeJson(moment.Name))
                    .Append("\", \"verdict\": \"").Append(DescribeVerdict(moment.Verdict))
                    .Append("\", \"detail\": \"").Append(EscapeJson(moment.Detail ?? string.Empty))
                    .Append("\" }")
                    .Append(i == _routeMoments.Count - 1 ? "\n" : ",\n");
            }

            builder.Append("  ],\n");

            builder.Append("  \"rejectedServices\": [");
            for (int i = 0; i < _rejectedServices.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append('"').Append(EscapeJson(_rejectedServices[i])).Append('"');
            }

            builder.Append("]\n}\n");

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
                Debug.Log($"{Marker} ARTIFACT {path}");
            }
            catch (IOException ex)
            {
                Debug.Log($"{Marker} ARTIFACT FAILED {path}: {ex.GetType().Name}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.Log($"{Marker} ARTIFACT FAILED {path}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void AppendJsonField(StringBuilder builder, string name, string value, string indent = "  ")
        {
            builder.Append(indent).Append('"').Append(name).Append("\": \"")
                .Append(EscapeJson(value ?? string.Empty)).Append("\",\n");
        }

        private static void AppendJsonFieldLast(StringBuilder builder, string name, string value, string indent = "  ")
        {
            builder.Append(indent).Append('"').Append(name).Append("\": \"")
                .Append(EscapeJson(value ?? string.Empty)).Append("\"\n");
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, double value, string indent = "  ")
        {
            builder.Append(indent).Append('"').Append(name).Append("\": ").Append(FormatNumber(value)).Append(",\n");
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, long value, string indent = "  ")
        {
            builder.Append(indent).Append('"').Append(name).Append("\": ")
                .Append(value.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        }

        private static void AppendJsonBool(StringBuilder builder, string name, bool value, string indent = "  ")
        {
            builder.Append(indent).Append('"').Append(name).Append("\": ").Append(value ? "true" : "false").Append(",\n");
        }

        private static void AppendJsonBoolLast(StringBuilder builder, string name, bool value, string indent = "  ")
        {
            builder.Append(indent).Append('"').Append(name).Append("\": ").Append(value ? "true" : "false").Append('\n');
        }

        private static string FormatNumber(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? "0"
                : value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 16);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                            builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(c);

                        break;
                }
            }

            return builder.ToString();
        }

        private static string ReadStringArg(string flag, string fallback)
        {
            // Fully qualified: the project has its own Hecton8.Environment namespace, which wins
            // over System here and resolves to a type that has no GetCommandLineArgs.
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return fallback;
        }

        private static int ReadIntArg(string flag, int fallback)
        {
            string raw = ReadStringArg(flag, null);
            return raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }
    }
}
