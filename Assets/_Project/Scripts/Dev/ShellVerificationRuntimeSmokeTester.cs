// ============================================================================
// HECTON-8 - ShellVerificationRuntimeSmokeTester.cs
// Dev-only runtime smoke coverage for shell verifiers and route recovery.
// Verifies menu -> orbit new-game handoff plus optional direct save load
// recovery using the real verifier owners.
// ============================================================================

using System;
using System.Threading;
using Hecton.UI.MainMenu;
using Hecton8.Bootstrap;
using Hecton8.Core;
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
            AwaitNewGameOrbit = 2,
            AwaitPauseRecovery = 3,
            AwaitInputRestoration = 4,
            AwaitReturnToMenu = 5
        }

        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string OrbitSceneName = "01_ORBIT";
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
            if (!IsAutoStartSupported())
            {
                enabled = false;
                return;
            }

            AutoResolve();
            LogVerbose($"Awake runOnStart={runOnStart} verbose={verboseLogging} scene={SceneManager.GetActiveScene().name}");
        }

        private void Start()
        {
            if (!IsAutoStartSupported())
                return;

            LogVerbose("Start");
            TryScheduleAutoStart();
        }

        private void OnEnable()
        {
            if (!IsAutoStartSupported())
                return;

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
            if (!IsAutoStartSupported())
                return;

            if (_isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
        }

        private void TryScheduleAutoStart()
        {
            if (!Application.isPlaying || _isRunning || _autoStartScheduled || !IsAutoStartSupported())
                return;

            string activeSceneName = SceneManager.GetActiveScene().name;
            bool canAutoStartFromScene =
                string.Equals(activeSceneName, BootstrapSceneName, System.StringComparison.Ordinal) ||
                string.Equals(activeSceneName, MainMenuSceneName, System.StringComparison.Ordinal) ||
                (string.Equals(activeSceneName, OrbitSceneName, System.StringComparison.Ordinal) && HasPendingResumeState()) ||
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
            _ = DeferredAutoStartRoutineAsync(destroyCancellationToken);
        }

        private async Awaitable DeferredAutoStartRoutineAsync(CancellationToken cancellationToken)
        {
            if (!IsAutoStartSupported())
            {
                _autoStartScheduled = false;
                return;
            }

            try
            {
                float deadline = Time.realtimeSinceStartup + AutoStartRetryWindow;
                while (Time.realtimeSinceStartup < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (runOnStart && !_isRunning)
                        break;

                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
                }

                if (!runOnStart || _isRunning)
                {
                    LogVerbose("Auto-start skipped");
                    return;
                }

                LogVerbose($"Auto-start launching in scene '{SceneManager.GetActiveScene().name}'");
                _ = RunSmokePassAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogVerbose("Auto-start cancelled.");
            }
            finally
            {
                _autoStartScheduled = false;
            }
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

        private async Awaitable RunSmokePassAsync(CancellationToken cancellationToken)
        {
            if (!IsAutoStartSupported() || _isRunning)
                return;

            _isRunning = true;
            try
            {
                _debugRunCount++;
                _debugLastPhase = "Startup";
                _debugLastPass = false;
                _debugLastIssue = string.Empty;
                _debugLastSaveSlot = string.Empty;

                LogDiagnostic($"[ShellSmoke] Run start scene={SceneManager.GetActiveScene().name} run={_debugRunCount}");

                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay, cancellationToken);

                await WaitForEditorStabilityAsync(cancellationToken);
                if (string.Equals(_debugLastPhase, "Failed", System.StringComparison.Ordinal))
                    return;

                AutoResolve();
                EnsureVerifiers();

                string activeSceneName = SceneManager.GetActiveScene().name;
                ResumePhase resumePhase = LoadResumePhase();
                string resumeSaveSlot = LoadResumeSaveSlot();
                if (CanResumeFromScene(activeSceneName, resumePhase))
                {
                    await ResumeFromScenePhaseAsync(resumePhase, resumeSaveSlot, cancellationToken);
                    return;
                }

                if (string.Equals(activeSceneName, BootstrapSceneName, System.StringComparison.Ordinal))
                {
                    _debugLastPhase = "BootstrapToMenu";
                    SaveResumeState(ResumePhase.AwaitMenuShell);
                    LogDiagnostic("[ShellSmoke] Waiting for bootstrap-to-menu route.");
                    await WaitUntilAsync(IsMenuRouteReady, "Bootstrap-to-menu route", cancellationToken);
                    activeSceneName = SceneManager.GetActiveScene().name;
                    AutoResolve();
                    GameStartContext menuContext = GameStartContextHolder.Current;
                    LogDiagnostic(
                        $"[ShellSmoke] Bootstrap wait complete scene={activeSceneName} menuReady={IsMenuRouteReady()} " +
                        $"hasMenu={_mainMenuController != null} bootstrapReady={GameBootstrapper.AreAllSystemsReady()} " +
                        $"contextValid={menuContext.IsValid} startMode={menuContext.StartMode} slot={menuContext.TargetSaveSlot}");
                }

                if (!string.Equals(activeSceneName, MainMenuSceneName, System.StringComparison.Ordinal))
                {
                    Fail($"Expected active scene {MainMenuSceneName}, got {activeSceneName}.");
                    return;
                }

                if (_mainMenuController == null)
                {
                    Fail("MainMenuController not found in main menu scene.");
                    return;
                }

                LogDiagnostic("[ShellSmoke] Starting shell verification smoke pass.");

                _debugLastPhase = "NewGameOrbitTransition";
                SaveResumeState(ResumePhase.AwaitNewGameOrbit);
                _mainMenuController.StartGame(string.Empty);
                _sceneVerifier.VerifyNewGameTransition();
                await WaitUntilAsync(IsNewGameOrbitReady, "New-game orbit handoff", cancellationToken);
                bool newGameOrbitReady = IsNewGameOrbitReady();
                if (!newGameOrbitReady)
                {
                    Fail("New-game handoff did not reach a valid orbit prologue state.");
                    return;
                }

                if (newGameOrbitReady)
                {
                    ClearResumeState();
                    CompleteRun();
                    return;
                }

                _debugLastPhase = "PauseRecovery";
                SaveResumeState(ResumePhase.AwaitPauseRecovery);
                await WaitUntilAsync(HasPauseMenuInWorld, "Pause menu resolve in world", cancellationToken);
                if (!HasPauseMenuInWorld())
                {
                    Fail("PauseMenuController not found after world load.");
                    return;
                }

                (int pauseRunBefore, int pausePassBefore, int pauseFailBefore) = _pauseVerifier.GetStats();
                _pauseVerifier.TestPauseMenuNavigation();
                await WaitUntilAsync(
                    () => HasVerifierAdvanced(_pauseVerifier.GetStats(), pauseRunBefore, pausePassBefore, pauseFailBefore),
                    "PauseSystemVerifier completion",
                    cancellationToken);

                (int pauseRunAfter, int pausePassAfter, int pauseFailAfter) = _pauseVerifier.GetStats();
                if (!HasVerifierPassed(pauseRunBefore, pausePassBefore, pauseFailBefore, pauseRunAfter, pausePassAfter, pauseFailAfter))
                {
                    Fail("PauseSystemVerifier did not report a passing result.");
                    return;
                }

                _debugLastPhase = "InputRestoration";
                SaveResumeState(ResumePhase.AwaitInputRestoration);
                (int stateRunBefore, int statePassBefore, int stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyInputRestoration();
                await WaitUntilAsync(
                    () => HasVerifierAdvanced(_stateVerifier.GetStats(), stateRunBefore, statePassBefore, stateFailBefore),
                    "StateRecoveryVerifier input completion",
                    cancellationToken);

                (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier input restoration failed.");
                    return;
                }

                _debugLastPhase = "ReturnToMenu";
                SaveResumeState(ResumePhase.AwaitReturnToMenu);
                (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyReturnToMenuRecovery();
                await WaitUntilAsync(IsMenuRouteReady, "Return-to-menu route", cancellationToken);

                (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier return-to-menu failed.");
                    return;
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
                        await WaitUntilAsync(() => IsWorldLoadReady(saveSlot), "Load-slot world handoff", cancellationToken);

                        (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                        if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                        {
                            Fail($"StateRecoveryVerifier load-slot recovery failed for {saveSlot}.");
                            return;
                        }

                        _debugLastPhase = "ReturnToMenuAfterLoad";
                        (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                        _stateVerifier.VerifyReturnToMenuRecovery();
                        await WaitUntilAsync(IsMenuRouteReady, "Return-to-menu after load", cancellationToken);

                        (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                        if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                        {
                            Fail("StateRecoveryVerifier return-to-menu after load failed.");
                            return;
                        }
                    }
                    else
                    {
                        LogVerbose("Skipping load-slot verification because no save slot is available.");
                    }
                }

                CompleteRun();
            }
            catch (OperationCanceledException)
            {
                _debugLastIssue = "Cancelled";
                LogVerbose("Cancelled.");
            }
            catch (Exception ex)
            {
                Fail("Unhandled shell smoke exception.");
                LogDiagnosticError($"[ShellSmoke] UNHANDLED EXCEPTION: {ex}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Awaitable<bool> WaitUntilAsync(Func<bool> predicate, string label, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.25f, actionTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AutoResolve();
                if (predicate() || (string.Equals(label, "Bootstrap-to-menu route", System.StringComparison.Ordinal) && _menuRouteReadyOverride))
                {
                    if (settleDelay > 0f)
                        await DelayRealtimeAsync(settleDelay, cancellationToken);

                    LogVerbose($"PASS {label}");
                    return true;
                }

                if (string.Equals(label, "Bootstrap-to-menu route", System.StringComparison.Ordinal))
                    TryLogMenuRouteDiagnostics(label);
                else if (string.Equals(label, "Pause menu resolve in world", System.StringComparison.Ordinal))
                    TryLogPauseMenuDiagnostics(label);

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            Fail($"{label} timed out after {actionTimeout:0.00}s.");
            return false;
        }

        private async Awaitable<bool> WaitForEditorStabilityAsync(CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, actionTimeout);
            float stableSince = -1f;
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool isCompiling = UnityEditor.EditorApplication.isCompiling;
                bool isUpdating = UnityEditor.EditorApplication.isUpdating;
                bool isChangingPlayMode = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
                if (!isCompiling && !isUpdating && !isChangingPlayMode)
                {
                    if (stableSince < 0f)
                        stableSince = Time.realtimeSinceStartup;

                    if ((Time.realtimeSinceStartup - stableSince) >= EditorStableWindowSeconds)
                        return true;
                }
                else
                {
                    stableSince = -1f;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            Fail("Editor did not reach a stable non-compiling state before smoke start.");
            return false;
#else
            return true;
#endif
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private bool IsNewGameOrbitReady()
        {
            GameStartContext context = GameStartContextHolder.Current;
            return string.Equals(SceneManager.GetActiveScene().name, OrbitSceneName, System.StringComparison.Ordinal) &&
                   GameBootstrapper.AreAllSystemsReady() &&
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
                   GameBootstrapper.AreAllSystemsReady() &&
                   !GameStartContextHolder.Current.IsValid;
        }

        private bool IsWorldLoadReady(string slotName)
        {
            GameStartContext context = GameStartContextHolder.Current;
            return string.Equals(SceneManager.GetActiveScene().name, WorldSceneName, System.StringComparison.Ordinal) &&
                   GameBootstrapper.AreAllSystemsReady() &&
                   context.IsValid &&
                   context.StartMode == GameStartMode.LoadGame &&
                   string.Equals(context.TargetSaveSlot, slotName, System.StringComparison.Ordinal);
        }

        private string ResolveExistingSaveSlot()
        {
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.Save as SaveManager;
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
            {
                if (!TryGetComponent(out _pauseVerifier))
                    _pauseVerifier = gameObject.AddComponent<PauseSystemVerifier>();
            }

            if (_sceneVerifier == null)
            {
                if (!TryGetComponent(out _sceneVerifier))
                    _sceneVerifier = gameObject.AddComponent<SceneTransitionVerifier>();
            }

            if (_stateVerifier == null)
            {
                if (!TryGetComponent(out _stateVerifier))
                    _stateVerifier = gameObject.AddComponent<StateRecoveryVerifier>();
            }
        }

        private void AutoResolve()
        {
            if (_mainMenuController == null)
                _mainMenuController = VerificationRuntimeProbe.ResolveMainMenuController();

            if (_pauseVerifier == null)
                TryGetComponent(out _pauseVerifier);

            if (_sceneVerifier == null)
                TryGetComponent(out _sceneVerifier);

            if (_stateVerifier == null)
                TryGetComponent(out _stateVerifier);
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
            bool bootstrapReady = GameBootstrapper.AreAllSystemsReady();
            bool hasMenu = _mainMenuController != null;

            LogDiagnostic(
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

            LogDiagnostic(
                $"[ShellSmoke] PauseMenuDiag reason={reason} scene={activeSceneName} " +
                $"isWorld={isWorld} hasPauseMenu={hasPauseMenu}");
        }

        private async Awaitable ResumeFromScenePhaseAsync(
            ResumePhase resumePhase,
            string resumeSaveSlot,
            CancellationToken cancellationToken)
        {
            LogDiagnostic($"[ShellSmoke] Resume start phase={resumePhase} scene={SceneManager.GetActiveScene().name} slot={resumeSaveSlot}");

            if (resumePhase == ResumePhase.AwaitNewGameOrbit)
            {
                if (!IsNewGameOrbitReady())
                {
                    Fail("Resume requested in orbit, but new-game prologue handoff state is invalid.");
                    return;
                }

                CompleteRun();
                return;
            }

            if (resumePhase == ResumePhase.AwaitPauseRecovery)
            {
                _debugLastPhase = "PauseRecovery";
                SaveResumeState(ResumePhase.AwaitPauseRecovery, resumeSaveSlot);
                await WaitUntilAsync(HasPauseMenuInWorld, "Pause menu resolve in world", cancellationToken);
                if (!HasPauseMenuInWorld())
                {
                    Fail("PauseMenuController not found after world load.");
                    return;
                }

                (int pauseRunBefore, int pausePassBefore, int pauseFailBefore) = _pauseVerifier.GetStats();
                _pauseVerifier.TestPauseMenuNavigation();
                await WaitUntilAsync(
                    () => HasVerifierAdvanced(_pauseVerifier.GetStats(), pauseRunBefore, pausePassBefore, pauseFailBefore),
                    "PauseSystemVerifier completion",
                    cancellationToken);

                (int pauseRunAfter, int pausePassAfter, int pauseFailAfter) = _pauseVerifier.GetStats();
                if (!HasVerifierPassed(pauseRunBefore, pausePassBefore, pauseFailBefore, pauseRunAfter, pausePassAfter, pauseFailAfter))
                {
                    Fail("PauseSystemVerifier did not report a passing result.");
                    return;
                }

                resumePhase = ResumePhase.AwaitInputRestoration;
            }

            if (resumePhase == ResumePhase.AwaitInputRestoration)
            {
                _debugLastPhase = "InputRestoration";
                SaveResumeState(ResumePhase.AwaitInputRestoration, resumeSaveSlot);
                (int stateRunBefore, int statePassBefore, int stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyInputRestoration();
                await WaitUntilAsync(
                    () => HasVerifierAdvanced(_stateVerifier.GetStats(), stateRunBefore, statePassBefore, stateFailBefore),
                    "StateRecoveryVerifier input completion",
                    cancellationToken);

                (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier input restoration failed.");
                    return;
                }

                resumePhase = ResumePhase.AwaitReturnToMenu;
            }

            if (resumePhase == ResumePhase.AwaitReturnToMenu)
            {
                _debugLastPhase = "ReturnToMenu";
                SaveResumeState(ResumePhase.AwaitReturnToMenu, resumeSaveSlot);
                (int stateRunBefore, int statePassBefore, int stateFailBefore) = _stateVerifier.GetStats();
                _stateVerifier.VerifyReturnToMenuRecovery();
                await WaitUntilAsync(IsMenuRouteReady, "Return-to-menu route", cancellationToken);

                (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                {
                    Fail("StateRecoveryVerifier return-to-menu failed.");
                    return;
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
                    await WaitUntilAsync(() => IsWorldLoadReady(saveSlot), "Load-slot world handoff", cancellationToken);

                    (int stateRunAfter, int statePassAfter, int stateFailAfter) = _stateVerifier.GetStats();
                    if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                    {
                        Fail($"StateRecoveryVerifier load-slot recovery failed for {saveSlot}.");
                        return;
                    }

                    _debugLastPhase = "ReturnToMenuAfterLoad";
                    (stateRunBefore, statePassBefore, stateFailBefore) = _stateVerifier.GetStats();
                    _stateVerifier.VerifyReturnToMenuRecovery();
                    await WaitUntilAsync(IsMenuRouteReady, "Return-to-menu after load", cancellationToken);

                    (stateRunAfter, statePassAfter, stateFailAfter) = _stateVerifier.GetStats();
                    if (!HasVerifierPassed(stateRunBefore, statePassBefore, stateFailBefore, stateRunAfter, statePassAfter, stateFailAfter))
                    {
                        Fail("StateRecoveryVerifier return-to-menu after load failed.");
                        return;
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
            LogDiagnosticWarning($"[ShellSmoke] FAIL {_debugLastIssue}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogVerbose(string message)
        {
            if (verboseLogging && _isRunning)
                LogDiagnostic($"[ShellSmoke] {message}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiagnostic(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log(message);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiagnosticWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(message);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiagnosticError(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError(message);
#endif
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

            RuntimePerformanceProfiler profiler = RuntimePerformanceProfiler.ActiveRuntime;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return HasPendingResumeState();
#else
            return false;
#endif
        }

        private static bool CanResumeFromScene(string activeSceneName, ResumePhase resumePhase)
        {
            if (string.Equals(activeSceneName, OrbitSceneName, System.StringComparison.Ordinal))
                return resumePhase == ResumePhase.AwaitNewGameOrbit;

            if (!string.Equals(activeSceneName, WorldSceneName, System.StringComparison.Ordinal))
                return false;

            return resumePhase == ResumePhase.AwaitPauseRecovery ||
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
            LogDiagnostic($"[ShellSmoke] COMPLETE pass=True saveSlot={_debugLastSaveSlot}");
        }
    }
}
