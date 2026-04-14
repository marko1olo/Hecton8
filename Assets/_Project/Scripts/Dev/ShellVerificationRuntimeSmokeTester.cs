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
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string WorldSceneName = "02_HECTON_WORLD";
        private const float AutoStartRetryWindow = 3f;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float actionTimeout = 20f;
        [SerializeField] private float settleDelay = 0.25f;
        [SerializeField] private bool runLoadSlotIfAvailable = true;
        [SerializeField] private bool verboseLogging = true;

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
            Debug.Log($"[ShellSmoke] Awake runOnStart={runOnStart} verbose={verboseLogging} scene={SceneManager.GetActiveScene().name}");
            LogVerbose("Awake");
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
            if (!Application.isPlaying || _isRunning || _autoStartScheduled)
                return;

            _autoStartScheduled = true;
            LogVerbose("Auto-start scheduled");
            StartCoroutine(DeferredAutoStartRoutine());
        }

        private IEnumerator DeferredAutoStartRoutine()
        {
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

                AutoResolve();
                EnsureVerifiers();

                string activeSceneName = SceneManager.GetActiveScene().name;
                if (string.Equals(activeSceneName, BootstrapSceneName, System.StringComparison.Ordinal))
                {
                    _debugLastPhase = "BootstrapToMenu";
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
                _mainMenuController.StartGame(string.Empty);
                _sceneVerifier.VerifyNewGameTransition();
                yield return WaitUntil(IsWorldNewGameReady, "New-game world handoff");
                if (!IsWorldNewGameReady())
                {
                    Fail("New-game handoff did not reach a valid world state.");
                    yield break;
                }

                _debugLastPhase = "PauseRecovery";
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
                (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyReturnToMenuRecovery();
                yield return WaitUntil(IsMenuRouteReady, "Return-to-menu route");

                (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier return-to-menu failed.");
                    yield break;
                }

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

                _debugLastPhase = "Complete";
                _debugLastPass = true;
                Debug.Log($"[ShellSmoke] COMPLETE pass=True saveSlot={_debugLastSaveSlot}");
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
            _debugLastPass = false;
            _debugLastIssue = string.IsNullOrEmpty(issue) ? "Unknown failure." : issue;
            _debugLastPhase = "Failed";
            Debug.LogWarning($"[ShellSmoke] FAIL {_debugLastIssue}");
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log($"[ShellSmoke] {message}");
        }
    }
}
