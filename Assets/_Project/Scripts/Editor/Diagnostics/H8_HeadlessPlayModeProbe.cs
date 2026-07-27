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

        // How long to let the game bring up its OWN menu before loading one. The first version
        // raced it and ended up with two copies of 01_MAIN_MENU loaded at once, pressing New Game
        // while a scene load was already in flight.
        private const int MenuWaitFrames = 900;

        // After New Game the world scene load is in flight for a long time in batchmode - 2400
        // gameplay frames were not enough for 02_HECTON_WORLD to report isLoaded. Counting frames
        // measures the wrong thing; wait for the loads to actually finish.
        private const int SettleWaitFrames = 6000;

        private static Phase _phase = Phase.Idle;
        private static double _startedAt;
        private static int _frames;
        private static int _gameplayFrames;
        private static int _warmupFrames = DefaultWarmupFrames;
        private static int _gameplayFramesTarget;
        private static bool _startNewGame;
        private static int _menuFrames;
        private static int _settleFrames;
        private static AsyncOperation _menuLoad;
        private static int _failures;

        public static void Run()
        {
            string scenePath = ReadStringArg("-h8Scene", DefaultScene);
            _warmupFrames = Math.Max(1, ReadIntArg("-h8WarmupFrames", DefaultWarmupFrames));

            // A headless boot legitimately stops at 01_MAIN_MENU: gameplay systems - ecosystem,
            // terrain, the world-seed owner - are installed only once a game is actually started.
            // Without this the probe can only ever inspect the menu.
            _gameplayFramesTarget = Math.Max(0, ReadIntArg("-h8GameplayFrames", 0));
            _startNewGame = _gameplayFramesTarget > 0;
            _hardTimeoutSeconds = Math.Max(30.0, ReadIntArg("-h8TimeoutSeconds", 240));

            Debug.Log(
                $"{Marker} START scene={scenePath} warmupFrames={_warmupFrames} " +
                $"gameplayFrames={_gameplayFramesTarget} batchmode={Application.isBatchMode}");

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
                        Debug.Log($"{Marker} PLAYING (entered after {EditorApplication.timeSinceStartup - _startedAt:F1}s)");
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
                        _phase = _startNewGame ? Phase.LoadingMenu : Phase.Reporting;
                    break;

                case Phase.LoadingMenu:
                    TickLoadingMenu();
                    break;

                case Phase.MenuWarmup:
                    if (++_menuFrames >= MenuWarmupFrames)
                        _phase = Phase.StartingGame;
                    break;

                case Phase.StartingGame:
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

                    if (++_gameplayFrames >= _gameplayFramesTarget)
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

            if (pending.Length == 0)
            {
                Debug.Log($"{Marker} SETTLED after {_settleFrames} frames - no scene load in flight");
                _phase = Phase.GameplayWarmup;
                return;
            }

            if (++_settleFrames >= SettleWaitFrames)
            {
                Debug.Log($"{Marker} NOT SETTLED after {SettleWaitFrames} frames - still loading: {pending}");
                _phase = Phase.GameplayWarmup;
                return;
            }

            if (_settleFrames % 1500 == 0)
                Debug.Log($"{Marker} settling... frame {_settleFrames}, loading: {pending}");
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
                if (TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController existing) && existing.enabled)
                {
                    Debug.Log(
                        $"{Marker} MENU live in scene '{existing.gameObject.scene.name}' after " +
                        $"{_menuFrames} waited frames - using the game's own");
                    _phase = Phase.StartingGame;
                    return;
                }

                if (++_menuFrames < MenuWaitFrames)
                    return;

                Debug.Log(
                    $"{Marker} MENU none live after {MenuWaitFrames} frames - loading '{MenuSceneName}' " +
                    "additively as a fallback (boot is ready, so its route check should pass)");
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
                ReportBootstrapReadiness();
                CheckWorldSeed();
                ReportRegistryPresence();
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

        private static bool IsAlive(object service)
        {
            if (service is UnityEngine.Object unityObject)
                return unityObject != null;

            return service != null;
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
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
