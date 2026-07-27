using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Relies on the project having Enter Play Mode Options set to DisableDomainReload
    /// (ProjectSettings/EditorSettings.asset, m_EnterPlayModeOptions: 1), which is why a plain
    /// static state machine survives the transition. Should that ever change, the statics reset
    /// mid-run and the probe stalls rather than lying - always run it under an external timeout.
    /// </summary>
    public static class H8_HeadlessPlayModeProbe
    {
        private const string Marker = "[H8_PLAYPROBE]";
        private const string DefaultScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
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
            Reporting,
            LeavingPlayMode,
        }

        private const string MenuSceneName = "01_MAIN_MENU";
        private const int MenuWarmupFrames = 120;

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

        public static void Run()
        {
            string scenePath = ReadStringArg("-h8Scene", DefaultScene);
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

            Debug.Log(
                $"{Marker} START scene={scenePath} warmupFrames={_warmupFrames} " +
                $"gameplayFrames={_gameplayFramesTarget} batchmode={Application.isBatchMode}");

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

            _phase = Phase.WaitingForPlayMode;
            _startedAt = EditorApplication.timeSinceStartup;
            _frames = 0;
            _failures = 0;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
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
                        _phase = Phase.WarmingUp;
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
                        _phase = _startNewGame ? Phase.LoadingMenu : Phase.Reporting;
                    }
                    break;

                case Phase.LoadingMenu:
                    TickLoadingMenu();
                    break;

                case Phase.MenuWarmup:
                    if (++_menuFrames >= MenuWarmupFrames)
                        _phase = Phase.StartingGame;
                    break;

                case Phase.StartingGame:
                    _phaseStartedAt = EditorApplication.timeSinceStartup;
                    // One shot. If the menu is not there, say so and report on the menu-state
                    // runtime rather than silently pretending a game was started.
                    _phase = TryStartNewGame() ? Phase.WaitingForSettle : Phase.Reporting;
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
                        _phase = Phase.Reporting;
                    break;

                case Phase.Reporting:
                    RunChecks();
                    _phase = Phase.LeavingPlayMode;
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
        /// </summary>
        private static void ReportClockRates()
        {
            int gameFrames = Time.frameCount - _gameFrameAtPlayStart;
            double wall = EditorApplication.timeSinceStartup - _playStartedAt;

            Debug.Log(
                $"{Marker} CLOCKS probeTicks={_frames + _menuFrames + _settleFrames + _gameplayFrames} " +
                $"gameFrames={gameFrames} wallSeconds={wall:F1} " +
                $"gameFramesPerProbeTick={(_frames + _menuFrames + _settleFrames + _gameplayFrames > 0 ? gameFrames / (double)(_frames + _menuFrames + _settleFrames + _gameplayFrames) : 0):F2} " +
                $"timeScale={Time.timeScale} unscaledTime={Time.unscaledTime:F1} captureFramerate={Time.captureFramerate}");
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
                _phaseStartedAt = EditorApplication.timeSinceStartup;
                _phase = Phase.GameplayWarmup;
                return;
            }

            if (waited >= _settleWaitSeconds)
            {
                Debug.Log($"{Marker} NOT SETTLED after {_settleWaitSeconds:F0}s - still loading: {pending}");
                _phaseStartedAt = EditorApplication.timeSinceStartup;
                _phase = Phase.GameplayWarmup;
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
                    _phase = Phase.StartingGame;
                    return;
                }

                double waited = EditorApplication.timeSinceStartup - _phaseStartedAt;
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
                    _phase = Phase.Reporting;
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
                    _phase = Phase.Reporting;
                }

                return;
            }

            if (_menuLoad.isDone)
            {
                Debug.Log($"{Marker} MENU fallback scene loaded");
                _menuLoad = null;
                _menuFrames = 0;
                _phase = Phase.MenuWarmup;
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
                return false;
            }
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

            Debug.Log($"{Marker} RESULT failures={_failures}");
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

            _phase = Phase.Idle;

            Debug.Log($"{Marker} DONE exitCode={exitCode}");

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
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
