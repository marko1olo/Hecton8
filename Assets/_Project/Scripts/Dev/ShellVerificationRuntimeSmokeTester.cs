// ============================================================================
// HECTON-8 - ShellVerificationRuntimeSmokeTester.cs
// Dev-only runtime smoke coverage for shell verifiers and route recovery.
// Verifies menu -> world handoff, pause recovery, input restoration, optional
// load-from-shell, and world -> menu recovery using the real verifier owners.
// ============================================================================

using System.Collections;
using Hecton.UI.MainMenu;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Shell Verification Runtime Smoke Tester")]
    public sealed class ShellVerificationRuntimeSmokeTester : MonoBehaviour
    {
        private enum ResumePhase
        {
            None = 0,
            AwaitMenuShell = 1,
            AwaitWorldNewGame = 2,
            AwaitPauseRecovery = 3,
            AwaitInputRestoration = 4,
            AwaitReturnToMenu = 5
        }

        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string WorldSceneName = "02_HECTON_WORLD";
        private const float AutoStartRetryWindow = 3f;
        private const float EditorStableWindowSeconds = 0.5f;
        private const string ResumePhaseKey = "Hecton8.ShellSmoke.ResumePhase";
        private const string ResumeSaveSlotKey = "Hecton8.ShellSmoke.ResumeSaveSlot";

        [Header("Execution")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField, Tooltip("Suppress automatic smoke execution while the runtime profiler owns the baseline pass.")]
        private bool suppressAutoStartWhileRuntimeProfilerActive = true;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float actionTimeout = 20f;
        [SerializeField] private float settleDelay = 0.25f;
        [SerializeField] private bool runLoadSlotIfAvailable = true;
        [SerializeField] private bool verboseLogging = false;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private string _debugLastSaveSlot = string.Empty;
#pragma warning restore CS0414

        private bool _isRunning;
        private bool _autoStartScheduled;
        private float _nextMenuRouteDiagnosticTime;
        private bool _menuRouteReadyOverride;
        private float _nextPauseMenuDiagnosticTime;
        private PauseSystemVerifier _pauseVerifier;
        private SceneTransitionVerifier _sceneVerifier;
        private StateRecoveryVerifier _stateVerifier;
        private MainMenuController _mainMenuController;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AutoResolve();
            LogVerbose($"Awake runOnStart={runOnStart} verbose={verboseLogging} scene={SceneManager.GetActiveScene().name}");
        }

        private void Start()
        {
            LogVerbose("Start");
            TryScheduleAutoStart();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LogVerbose("OnEnable");
            TryScheduleAutoStart();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolve();
        }
#endif

        [ContextMenu("Run Shell Verification Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            StartCoroutine(RunSmokePass());
        }

        private void TryScheduleAutoStart()
        {
            if (!Application.isPlaying || _isRunning || _autoStartScheduled || !IsAutoStartSupported())
                return;

            string activeSceneName = SceneManager.GetActiveScene().name;
            bool canAutoStartFromScene =
                string.Equals(activeSceneName, BootstrapSceneName, System.StringComparison.Ordinal) ||
                string.Equals(activeSceneName, MainMenuSceneName, System.StringComparison.Ordinal) ||
                (string.Equals(activeSceneName, WorldSceneName, System.StringComparison.Ordinal) && HasPendingResumeState());
            if (!canAutoStartFromScene)
            {
                LogVerbose($"Auto-start skipped in scene '{activeSceneName}'");
                return;
            }

            if (ShouldSuppressAutoStart())
            {
                LogVerbose("Auto-start suppressed because RuntimePerformanceProfiler is active.");
                return;
            }

            _autoStartScheduled = true;
            LogVerbose("Auto-start scheduled");
            StartCoroutine(DeferredAutoStartRoutine());
        }

        private IEnumerator DeferredAutoStartRoutine()
        {
            if (!IsAutoStartSupported())
            {
                _autoStartScheduled = false;
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + AutoStartRetryWindow;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (runOnStart && !_isRunning)
                    break;

                yield return null;
            }

            _autoStartScheduled = false;

            if (!runOnStart || _isRunning)
            {
                LogVerbose("Auto-start skipped");
                yield break;
            }

            LogVerbose($"Auto-start launching in scene '{SceneManager.GetActiveScene().name}'");
            StartCoroutine(RunSmokePass());
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogVerbose($"Scene loaded: {scene.name}");
            if (string.Equals(scene.name, MainMenuSceneName, System.StringComparison.Ordinal))
            {
                if (_isRunning || WantsAutoStart() || HasPendingResumeState())
                    LogMenuRouteDiagnostics("scene-loaded");
                _menuRouteReadyOverride = IsMenuRouteReady();
            }

            TryScheduleAutoStart();
        }

        private IEnumerator RunSmokePass()
        {
            if (_isRunning)
                yield break;

            _isRunning = true;
            try
            {
                _debugRunCount++;
                _debugLastPhase = "Startup";
                _debugLastPass = false;
                _debugLastIssue = string.Empty;
                _debugLastSaveSlot = string.Empty;

                Debug.Log($"[ShellSmoke] Run start scene={SceneManager.GetActiveScene().name} run={_debugRunCount}");

                if (startupDelay > 0f)
                    yield return new WaitForSecondsRealtime(startupDelay);

                yield return WaitForEditorStability();
                if (string.Equals(_debugLastPhase, "Failed", System.StringComparison.Ordinal))
                    yield break;

                AutoResolve();
                EnsureVerifiers();

                string activeSceneName = SceneManager.GetActiveScene().name;
                ResumePhase resumePhase = LoadResumePhase();
                string resumeSaveSlot = LoadResumeSaveSlot();
                if (CanResumeFromWorld(activeSceneName, resumePhase))
                {
                    yield return ResumeFromWorldPhase(resumePhase, resumeSaveSlot);
                    yield break;
                }

                if (string.Equals(activeSceneName, BootstrapSceneName, System.StringComparison.Ordinal))
                {
                    _debugLastPhase = "BootstrapToMenu";
                    SaveResumeState(ResumePhase.AwaitMenuShell);
                    Debug.Log("[ShellSmoke] Waiting for bootstrap-to-menu route.");
                    yield return WaitUntil(IsMenuRouteReady, "Bootstrap-to-menu route");
                    activeSceneName = SceneManager.GetActiveScene().name;
                    AutoResolve();
                    GameStartContext menuContext = GameStartContextHolder.Current;
                    Debug.Log(
                        $"[ShellSmoke] Bootstrap wait complete scene={activeSceneName} menuReady={IsMenuRouteReady()} " +
                        $"hasMenu={_mainMenuController != null} bootstrapReady={BootstrapController.AreAllSystemsReady()} " +
                        $"contextValid={menuContext.IsValid} startMode={menuContext.StartMode} slot={menuContext.TargetSaveSlot}");
                }

                if (!string.Equals(activeSceneName, MainMenuSceneName, System.StringComparison.Ordinal))
                {
                    Fail($"Expected active scene {MainMenuSceneName}, got {activeSceneName}.");
                    yield break;
                }

                if (_mainMenuController == null)
                {
                    Fail("MainMenuController not found in main menu scene.");
                    yield break;
                }

                Debug.Log("[ShellSmoke] Starting shell verification smoke pass.");

                _debugLastPhase = "NewGameTransition";
                SaveResumeState(ResumePhase.AwaitWorldNewGame);
                _mainMenuController.StartGame(string.Empty);
                _sceneVerifier.VerifyNewGameTransition();
                yield return WaitUntil(IsWorldNewGameReady, "New-game world handoff");
                if (!IsWorldNewGameReady())
                {
                    Fail("New-game handoff did not reach a valid world state.");
                    yield break;
                }

                _debugLastPhase = "PauseRecovery";
                SaveResumeState(ResumePhase.AwaitPauseRecovery);
                yield return WaitUntil(HasPauseMenuInWorld, "Pause menu resolve in world");
                if (!HasPauseMenuInWorld())
                {
                    Fail("PauseMenuController not found after world load.");
                    yield break;
                }

                (int pauseRunBefore, int pausePassBefore, int pauseFailBefore) = _pauseVerifier.GetStats();
                _pauseVerifier.TestPauseMenuNavigation();
                yield return WaitUntil(
                    () => HasVerifierAdvanced(_pauseVerifier.GetStats(), pauseRunBefore, pausePassBefore, pauseFailBefore),
                    "PauseSystemVerifier completion");

                (int pauseRunAfter, int pausePassAfter, int pauseFailAfter) = _pauseVerifier.GetStats();
                if (!HasVerifierPassed(pauseRunBefore, pausePassBefore, pauseFailBefore, pauseRunAfter, pausePassAfter, pauseFailAfter))
                {
                    Fail("PauseSystemVerifier did not report a passing result.");
                    yield break;
                }

                _debugLastPhase = "InputRestoration";
                SaveResumeState(ResumePhase.AwaitInputRestoration);
                (int stateRunBefore, int statePassBefore, int stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyInputRestoration();
                yield return WaitUntil(
                    () => HasVerifierAdvanced(_stateVerifier.GetStats(), stateRunBefore, statePassBefore, stateFailBefore),
                    "StateRecoveryVerifier input completion");

                (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier input restoration failed.");
                    yield break;
                }

                _debugLastPhase = "ReturnToMenu";
                SaveResumeState(ResumePhase.AwaitReturnToMenu);
                (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyReturnToMenuRecovery();
                yield return WaitUntil(IsMenuRouteReady, "Return-to-menu route");

                (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier return-to-menu failed.");
                    yield break;
                }

                ClearResumeState();
                if (runLoadSlotIfAvailable)
                {
                    _debugLastPhase = "LoadSlot";
                    string saveSlot = ResolveExistingSaveSlot();
                    _debugLastSaveSlot = saveSlot;
                    if (!string.IsNullOrEmpty(saveSlot))
                    {
                        (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                        _stateVerifier.VerifyLoadSlotFromShellRecovery();
                        yield return WaitUntil(() => IsWorldLoadReady(saveSlot), "Load-slot world handoff");

                        (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                        if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                        {
                            Fail($"StateRecoveryVerifier load-slot recovery failed for {saveSlot}.");
                            yield break;
                        }

                        _debugLastPhase = "ReturnToMenuAfterLoad";
                        (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                        _stateVerifier.VerifyReturnToMenuRecovery();
                        yield return WaitUntil(IsMenuRouteReady, "Return-to-menu after load");

                        (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                        if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                        {
                            Fail("StateRecoveryVerifier return-to-menu after load failed.");
                            yield break;
                        }
                    }
                    else
                    {
                        LogVerbose("Skipping load-slot verification because no save slot is available.");
                    }
                }

                CompleteRun();
            }
            finally
            {
                _isRunning = false;
            }
        }

        private IEnumerator WaitUntil(System.Func<bool> predicate, string label)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, actionTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                AutoResolve();
                if (predicate() || (string.Equals(label, "Bootstrap-to-menu route", System.StringComparison.Ordinal) && _menuRouteReadyOverride))
                {
                    if (settleDelay > 0f)
                        yield return new WaitForSecondsRealtime(settleDelay);

                    LogVerbose($"PASS {label}");
                    yield break;
                }

                if (string.Equals(label, "Bootstrap-to-menu route", System.StringComparison.Ordinal))
                    TryLogMenuRouteDiagnostics(label);
                else if (string.Equals(label, "Pause menu resolve in world", System.StringComparison.Ordinal))
                    TryLogPauseMenuDiagnostics(label);

                yield return null;
            }

            Fail($"{label} timed out after {actionTimeout:0.00}s.");
        }

        private IEnumerator WaitForEditorStability()
        {
#if UNITY_EDITOR
            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, actionTimeout);
            float stableSince = -1f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool isCompiling = UnityEditor.EditorApplication.isCompiling;
                bool isUpdating = UnityEditor.EditorApplication.isUpdating;
                bool isChangingPlayMode = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
                if (!isCompiling && !isUpdating && !isChangingPlayMode)
                {
                    if (stableSince < 0f)
                        stableSince = Time.realtimeSinceStartup;

                    if ((Time.realtimeSinceStartup - stableSince) >= EditorStableWindowSeconds)
                        yield break;
                }
                else
                {
                    stableSince = -1f;
                }

                yield return null;
            }

            Fail("Editor did not reach a stable non-compiling state before smoke start.");
#else
            yield break;
#endif
        }

        private bool IsWorldNewGameReady()
        {
            GameStartContext context = GameStartContextHolder.Current;
            return string.Equals(SceneManager.GetActiveScene().name, WorldSceneName, System.StringComparison.Ordinal) &&
                   BootstrapController.AreAllSystemsReady() &&
                   context.IsValid &&
                   context.StartMode == GameStartMode.NewGame;
        }

        private bool HasPauseMenuInWorld()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, WorldSceneName, System.StringComparison.Ordinal))
                return false;

            AutoResolve();
            return VerificationRuntimeProbe.ResolvePauseMenu() != null;
        }

        private bool IsMenuRouteReady()
        {
            AutoResolve();
            return string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.Ordinal) &&
                   _mainMenuController != null &&
                   BootstrapController.AreAllSystemsReady() &&
                   !GameStartContextHolder.Current.IsValid;
        }

        private bool IsWorldLoadReady(string slotName)
        {
            GameStartContext context = GameStartContextHolder.Current;
            return string.Equals(SceneManager.GetActiveScene().name, WorldSceneName, System.StringComparison.Ordinal) &&
                   BootstrapController.AreAllSystemsReady() &&
                   context.IsValid &&
                   context.StartMode == GameStartMode.LoadGame &&
                   string.Equals(context.TargetSaveSlot, slotName, System.StringComparison.Ordinal);
        }

        private string ResolveExistingSaveSlot()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
                return string.Empty;

            if (saveManager.SaveExists("slot_1"))
                return "slot_1";

            if (saveManager.SaveExists("slot_2"))
                return "slot_2";

            if (saveManager.SaveExists("slot_3"))
                return "slot_3";

            return string.Empty;
        }

        private void EnsureVerifiers()
        {
            if (_pauseVerifier == null)
                _pauseVerifier = GetComponent<PauseSystemVerifier>() ?? gameObject.AddComponent<PauseSystemVerifier>();

            if (_sceneVerifier == null)
                _sceneVerifier = GetComponent<SceneTransitionVerifier>() ?? gameObject.AddComponent<SceneTransitionVerifier>();

            if (_stateVerifier == null)
                _stateVerifier = GetComponent<StateRecoveryVerifier>() ?? gameObject.AddComponent<StateRecoveryVerifier>();
        }

        private void AutoResolve()
        {
            if (_mainMenuController == null)
                _mainMenuController = VerificationRuntimeProbe.ResolveMainMenuController();

            if (_pauseVerifier == null)
                _pauseVerifier = PauseSystemVerifier.Instance != null ? PauseSystemVerifier.Instance : GetComponent<PauseSystemVerifier>();

            if (_sceneVerifier == null)
                _sceneVerifier = SceneTransitionVerifier.Instance != null ? SceneTransitionVerifier.Instance : GetComponent<SceneTransitionVerifier>();

            if (_stateVerifier == null)
                _stateVerifier = StateRecoveryVerifier.Instance != null ? StateRecoveryVerifier.Instance : GetComponent<StateRecoveryVerifier>();
        }

        private void TryLogMenuRouteDiagnostics(string reason)
        {
            if (Time.realtimeSinceStartup < _nextMenuRouteDiagnosticTime)
                return;

            _nextMenuRouteDiagnosticTime = Time.realtimeSinceStartup + 2f;
            LogMenuRouteDiagnostics(reason);
        }

        private void LogMenuRouteDiagnostics(string reason)
        {
            AutoResolve();

            GameStartContext context = GameStartContextHolder.Current;
            string activeSceneName = SceneManager.GetActiveScene().name;
            bool bootstrapReady = BootstrapController.AreAllSystemsReady();
            bool hasMenu = _mainMenuController != null;

            Debug.Log(
                $"[ShellSmoke] MenuRouteDiag reason={reason} scene={activeSceneName} " +
                $"hasMenu={hasMenu} bootstrapReady={bootstrapReady} contextValid={context.IsValid} " +
                $"startMode={context.StartMode} slot={context.TargetSaveSlot}");
        }

        private void TryLogPauseMenuDiagnostics(string reason)
        {
            if (Time.realtimeSinceStartup < _nextPauseMenuDiagnosticTime)
                return;

            _nextPauseMenuDiagnosticTime = Time.realtimeSinceStartup + 2f;
            LogPauseMenuDiagnostics(reason);
        }

        private void LogPauseMenuDiagnostics(string reason)
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            bool isWorld = string.Equals(activeSceneName, WorldSceneName, System.StringComparison.Ordinal);
            bool hasPauseMenu = VerificationRuntimeProbe.ResolvePauseMenu() != null;

            Debug.Log(
                $"[ShellSmoke] PauseMenuDiag reason={reason} scene={activeSceneName} " +
                $"isWorld={isWorld} hasPauseMenu={hasPauseMenu}");
        }

        private IEnumerator ResumeFromWorldPhase(ResumePhase resumePhase, string resumeSaveSlot)
        {
            Debug.Log($"[ShellSmoke] Resume start phase={resumePhase} scene={SceneManager.GetActiveScene().name} slot={resumeSaveSlot}");

            if (resumePhase == ResumePhase.AwaitWorldNewGame && !IsWorldNewGameReady())
            {
                Fail("Resume requested in world, but new-game handoff state is invalid.");
                yield break;
            }

            if (resumePhase == ResumePhase.AwaitWorldNewGame || resumePhase == ResumePhase.AwaitPauseRecovery)
            {
                _debugLastPhase = "PauseRecovery";
                SaveResumeState(ResumePhase.AwaitPauseRecovery, resumeSaveSlot);
                yield return WaitUntil(HasPauseMenuInWorld, "Pause menu resolve in world");
                if (!HasPauseMenuInWorld())
                {
                    Fail("PauseMenuController not found after world load.");
                    yield break;
                }

                (int pauseRunBefore, int pausePassBefore, int pauseFailBefore) = _pauseVerifier.GetStats();
                _pauseVerifier.TestPauseMenuNavigation();
                yield return WaitUntil(
                    () => HasVerifierAdvanced(_pauseVerifier.GetStats(), pauseRunBefore, pausePassBefore, pauseFailBefore),
                    "PauseSystemVerifier completion");

                (int pauseRunAfter, int pausePassAfter, int pauseFailAfter) = _pauseVerifier.GetStats();
                if (!HasVerifierPassed(pauseRunBefore, pausePassBefore, pauseFailBefore, pauseRunAfter, pausePassAfter, pauseFailAfter))
                {
                    Fail("PauseSystemVerifier did not report a passing result.");
                    yield break;
                }

                resumePhase = ResumePhase.AwaitInputRestoration;
            }

            if (resumePhase == ResumePhase.AwaitInputRestoration)
            {
                _debugLastPhase = "InputRestoration";
                SaveResumeState(ResumePhase.AwaitInputRestoration, resumeSaveSlot);
                (int stateRunBefore, int statePassBefore, int stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyInputRestoration();
                yield return WaitUntil(
                    () => HasVerifierAdvanced(_stateVerifier.GetStats(), stateRunBefore, statePassBefore, stateFailBefore),
                    "StateRecoveryVerifier input completion");

                (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier input restoration failed.");
                    yield break;
                }

                resumePhase = ResumePhase.AwaitReturnToMenu;
            }

            if (resumePhase == ResumePhase.AwaitReturnToMenu)
            {
                _debugLastPhase = "ReturnToMenu";
                SaveResumeState(ResumePhase.AwaitReturnToMenu, resumeSaveSlot);
                (int stateRunBefore, int statePassBefore, int stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyReturnToMenuRecovery();
                yield return WaitUntil(IsMenuRouteReady, "Return-to-menu route");

                (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier return-to-menu failed.");
                    yield break;
                }
            }

            ClearResumeState();
            if (runLoadSlotIfAvailable)
            {
                _debugLastPhase = "LoadSlot";
                string saveSlot = ResolveExistingSaveSlot();
                _debugLastSaveSlot = saveSlot;
                if (!string.IsNullOrEmpty(saveSlot))
                {
                    (int stateRunBefore, int statePassBefore, int stateFailBefore) = _stateVerifier.GetStats();
                    _stateVerifier.VerifyLoadSlotFromShellRecovery();
                    yield return WaitUntil(() => IsWorldLoadReady(saveSlot), "Load-slot world handoff");

                    (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                    if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                    {
                        Fail($"StateRecoveryVerifier load-slot recovery failed for {saveSlot}.");
                        yield break;
                    }

                    _debugLastPhase = "ReturnToMenuAfterLoad";
                    (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                    _stateVerifier.VerifyReturnToMenuRecovery();
                    yield return WaitUntil(IsMenuRouteReady, "Return-to-menu after load");

                    (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                    if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                    {
                        Fail("StateRecoveryVerifier return-to-menu after load failed.");
                        yield break;
                    }
                }
                else
                {
                    LogVerbose("Skipping load-slot verification because no save slot is available.");
                }
            }

            CompleteRun();
        }

        private static bool HasVerifierAdvanced((int testsRun, int testsPassed, int testsFailed) stats, int beforeRun, int beforePass, int beforeFail)
        {
            return stats.testsRun > beforeRun || stats.testsPassed > beforePass || stats.testsFailed > beforeFail;
        }

        private static bool HasVerifierPassed(int beforeRun, int beforePass, int beforeFail, int afterRun, int afterPass, int afterFail)
        {
            return afterRun > beforeRun && afterPass > beforePass && afterFail == beforeFail;
        }

        private void Fail(string issue)
        {
            ClearResumeState();
            _debugLastPass = false;
            _debugLastIssue = string.IsNullOrEmpty(issue) ? "Unknown failure." : issue;
            _debugLastPhase = "Failed";
            Debug.LogWarning($"[ShellSmoke] FAIL {_debugLastIssue}");
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging && _isRunning)
                Debug.Log($"[ShellSmoke] {message}");
        }

        private bool ShouldSuppressAutoStart()
        {
            if (!suppressAutoStartWhileRuntimeProfilerActive)
                return false;

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(activeSceneName, MainMenuSceneName, System.StringComparison.Ordinal))
                return false;

            if (HasPendingResumeState())
                return false;

            RuntimePerformanceProfiler profiler = RuntimePerformanceProfiler.Instance;
            return profiler != null &&
                   profiler.IsProfilingActive &&
                   string.Equals(activeSceneName, BootstrapSceneName, System.StringComparison.Ordinal);
        }

        public bool WantsAutoStart()
        {
            return runOnStart && IsAutoStartSupported();
        }

        private static bool IsAutoStartSupported()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }

        public static bool HasPersistedResumeState()
        {
            return HasPendingResumeState();
        }

        private static bool CanResumeFromWorld(string activeSceneName, ResumePhase resumePhase)
        {
            if (!string.Equals(activeSceneName, WorldSceneName, System.StringComparison.Ordinal))
                return false;

            return resumePhase == ResumePhase.AwaitWorldNewGame ||
                   resumePhase == ResumePhase.AwaitPauseRecovery ||
                   resumePhase == ResumePhase.AwaitInputRestoration ||
                   resumePhase == ResumePhase.AwaitReturnToMenu;
        }

        private static ResumePhase LoadResumePhase()
        {
            int rawPhase = PlayerPrefs.GetInt(ResumePhaseKey, 0);
            if (rawPhase < (int)ResumePhase.None || rawPhase > (int)ResumePhase.AwaitReturnToMenu)
                return ResumePhase.None;

            return (ResumePhase)rawPhase;
        }

        private static string LoadResumeSaveSlot()
        {
            return PlayerPrefs.GetString(ResumeSaveSlotKey, string.Empty);
        }

        private static bool HasPendingResumeState()
        {
            return LoadResumePhase() != ResumePhase.None;
        }

        private static void PersistResumeState(ResumePhase resumePhase, string resumeSaveSlot)
        {
            PlayerPrefs.SetInt(ResumePhaseKey, (int)resumePhase);
            PlayerPrefs.SetString(ResumeSaveSlotKey, resumeSaveSlot ?? string.Empty);
            PlayerPrefs.Save();
        }

        private void SaveResumeState(ResumePhase resumePhase, string resumeSaveSlot = "")
        {
            PersistResumeState(resumePhase, resumeSaveSlot);
        }

        private static void ClearResumeState()
        {
            PlayerPrefs.DeleteKey(ResumePhaseKey);
            PlayerPrefs.DeleteKey(ResumeSaveSlotKey);
            PlayerPrefs.Save();
        }

        private void CompleteRun()
        {
            ClearResumeState();
            _debugLastPhase = "Complete";
            _debugLastPass = true;
            Debug.Log($"[ShellSmoke] COMPLETE pass=True saveSlot={_debugLastSaveSlot}");
        }
    }
}
