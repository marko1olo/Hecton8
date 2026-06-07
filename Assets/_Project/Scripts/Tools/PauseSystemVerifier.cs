using System;
using System.Threading;
using Hecton8.Core;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Tools
{
    /// <summary>
    /// Verifies pause menu functionality using real runtime state instead of stubbed pass values.
    /// </summary>
    [DefaultExecutionOrder(800)]
    public sealed class PauseSystemVerifier : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const string PauseChangedPausedLog = "[PauseSystemVerifier] Pause state changed: PAUSED";
        private const string PauseChangedUnpausedLog = "[PauseSystemVerifier] Pause state changed: UNPAUSED";

        [Header("Verification Settings")]
        [SerializeField, Tooltip("Enable automatic pause verification")]
        private bool _enableVerification = true;

        [SerializeField, Tooltip("Enable detailed logging")]
        private bool _enableLogging = true;

        [SerializeField, Tooltip("Pause/open close wait timeout in seconds")]
        private float _actionTimeout = 1.25f;

        [SerializeField, Tooltip("Realtime settle delay after state changes")]
        private float _settleDelay = 0.1f;

        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _isPaused;
        private int _testsRun;
        private int _testsPassed;
        private int _testsFailed;
        private PauseMenuController _pauseMenu;
        private ITickDispatcher _tickDispatcher;
        private INativeInputManagerRuntime _inputManager;

        private void Awake()
        {
            CacheRegistryServicesCold();
            ResolvePauseMenu();
            _isPaused = IsGamePaused();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _tickDispatcher = currentService as ITickDispatcher;
                TryUnregisterTick();
                if (currentService != null)
                    TryRegisterTick();

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                _inputManager = currentService as INativeInputManagerRuntime;
        }

        private void TryRegisterTick()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registered = false;
        }

        public void Tick(float dt)
        {
            if (!_enableVerification)
                return;

            bool currentlyPaused = IsGamePaused();
            if (currentlyPaused == _isPaused)
                return;

            OnPauseStateChanged(currentlyPaused);
            _isPaused = currentlyPaused;
        }

        public void VerifyCurrentPauseState()
        {
            ResolvePauseMenu();
            LogVerification("Manual pause verification triggered");
            if (IsGamePaused())
                VerifyPauseEntry();
            else
                VerifyPauseExit();
        }

        public void TestPauseMenuNavigation()
        {
            if (!_enableVerification)
                return;

            _ = TestPauseMenuFlowAsync(destroyCancellationToken);
        }

        public (int testsRun, int testsPassed, int testsFailed) GetStats()
        {
            return (_testsRun, _testsPassed, _testsFailed);
        }

        private async Awaitable TestPauseMenuFlowAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _testsRun++;
                ResolvePauseMenu();

                if (_pauseMenu == null)
                {
                    _testsFailed++;
                    LogVerification("FAIL pause flow: PauseMenuController not found.");
                    return;
                }

                LogVerification("Testing pause menu navigation flow");

                if (_pauseMenu.IsOpen)
                {
                    _pauseMenu.Close();
                    await WaitForConditionAsync(() => !_pauseMenu.IsOpen, "Initial pause close", cancellationToken);
                }

                bool pauseTriggered = SimulatePauseInput();
                if (!pauseTriggered)
                {
                    _testsFailed++;
                    LogVerification("FAIL pause flow: could not issue pause open.");
                    return;
                }

                await WaitForConditionAsync(
                    () => _pauseMenu.IsOpen && IsPauseMenuVisible(),
                    "Pause open",
                    cancellationToken);

                bool pauseStateValid = IsGamePaused() && IsPauseMenuVisible() && IsPauseInputModeValid();
                bool navigationWorks = TestMenuNavigation();

                bool unpauseTriggered = SimulateUnpauseInput();
                if (!unpauseTriggered)
                {
                    _testsFailed++;
                    LogVerification("FAIL pause flow: could not issue pause close.");
                    return;
                }

                await WaitForConditionAsync(
                    () => !_pauseMenu.IsOpen && !IsPauseMenuVisible(),
                    "Pause close",
                    cancellationToken);

                bool returnedToGameplay = !IsGamePaused() && IsGameplayInputModeValid();

                if (pauseStateValid && navigationWorks && returnedToGameplay)
                {
                    _testsPassed++;
                    LogVerification("PASS pause menu navigation flow");
                }
                else
                {
                    _testsFailed++;
                    LogVerification(
                        $"FAIL pause menu navigation flow: pauseState={pauseStateValid}, nav={navigationWorks}, gameplay={returnedToGameplay}");
                }
            }
            catch (OperationCanceledException)
            {
                LogVerification("Pause menu navigation verification cancelled.");
            }
            catch (Exception exception)
            {
                _testsFailed++;
                LogVerification($"FAIL pause menu navigation flow: exception {exception.Message}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogException(exception);
#endif
            }
        }

        private async Awaitable<bool> WaitForConditionAsync(Func<bool> predicate, string label, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.05f, _actionTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (predicate())
                {
                    if (_settleDelay > 0f)
                        await DelayRealtimeAsync(_settleDelay, cancellationToken);

                    LogVerification($"PASS {label}");
                    return true;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }

            LogVerification($"FAIL {label}: timeout after {_actionTimeout:0.00}s");
            return false;
        }

        private static async Awaitable DelayRealtimeAsync(float duration, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, duration);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }
        }

        private void OnPauseStateChanged(bool isPaused)
        {
            LogPauseStateChanged(isPaused);

            if (isPaused)
                VerifyPauseEntry();
            else
                VerifyPauseExit();
        }

        private void VerifyPauseEntry()
        {
            ResolvePauseMenu();

            bool simulationPaused = IsSimulationPaused();
            bool cursorVisible = Cursor.visible;
            bool menuVisible = IsPauseMenuVisible();
            bool inputValid = IsPauseInputModeValid();
            bool selectionValid = VerificationRuntimeProbe.HasPauseSelection(_pauseMenu);

            LogVerification("Pause entry verification:");
            LogVerification($"  Simulation paused: {(simulationPaused ? "PASS" : "FAIL")}");
            LogVerification($"  Cursor visible: {(cursorVisible ? "PASS" : "FAIL")}");
            LogVerification($"  Menu visible: {(menuVisible ? "PASS" : "FAIL")}");
            LogVerification($"  UI input active: {(inputValid ? "PASS" : "FAIL")}");
            LogVerification($"  Pause selection valid: {(selectionValid ? "PASS" : "FAIL")}");
        }

        private void VerifyPauseExit()
        {
            ResolvePauseMenu();

            bool simulationResumed = !IsSimulationPaused();
            bool cursorHidden = !Cursor.visible;
            bool menuHidden = !IsPauseMenuVisible();
            bool inputValid = IsGameplayInputModeValid();

            LogVerification("Pause exit verification:");
            LogVerification($"  Simulation resumed: {(simulationResumed ? "PASS" : "FAIL")}");
            LogVerification($"  Cursor hidden: {(cursorHidden ? "PASS" : "FAIL")}");
            LogVerification($"  Menu hidden: {(menuHidden ? "PASS" : "FAIL")}");
            LogVerification($"  Gameplay input active: {(inputValid ? "PASS" : "FAIL")}");
        }

        private bool SimulatePauseInput()
        {
            ResolvePauseMenu();
            if (_pauseMenu == null || _pauseMenu.IsOpen)
                return false;

            _pauseMenu.Open();
            return true;
        }

        private bool SimulateUnpauseInput()
        {
            ResolvePauseMenu();
            if (_pauseMenu == null || !_pauseMenu.IsOpen)
                return false;

            _pauseMenu.Close();
            return true;
        }

        private bool TestMenuNavigation()
        {
            ResolvePauseMenu();
            if (_pauseMenu == null || !_pauseMenu.IsOpen)
                return false;

            return VerificationRuntimeProbe.HasPauseSelection(_pauseMenu);
        }

        private bool IsGamePaused()
        {
            ResolvePauseMenu();
            return _pauseMenu != null && _pauseMenu.IsOpen && IsSimulationPaused();
        }

        private bool IsSimulationPaused()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            return dispatcher != null ? dispatcher.SimulationPaused : SimulationSignalRoute.SimulationPaused;
        }

        private bool IsPauseMenuVisible()
        {
            ResolvePauseMenu();
            return VerificationRuntimeProbe.IsPauseMenuVisible(_pauseMenu);
        }

        private bool IsPauseInputModeValid()
        {
            INativeInputManagerRuntime inputManager = _inputManager;
            if (inputManager == null || !inputManager.CanSwitchActionMaps)
                return false;

            return inputManager.IsUIInputEnabled && !inputManager.IsPlayerInputEnabled;
        }

        private bool IsGameplayInputModeValid()
        {
            INativeInputManagerRuntime inputManager = _inputManager;
            if (inputManager == null || !inputManager.CanSwitchActionMaps)
                return false;

            return inputManager.IsPlayerInputEnabled && !inputManager.IsUIInputEnabled;
        }

        private void ResolvePauseMenu()
        {
            if (_pauseMenu != null)
                return;

            _pauseMenu = VerificationRuntimeProbe.ResolvePauseMenu();
        }

        private void CacheRegistryServicesCold()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _inputManager = GlobalRegistry.NativeInputRuntime;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogVerification(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_enableLogging)
                Hecton8.Core.H8Debug.Log($"[PauseSystemVerifier] {message}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogPauseStateChanged(bool isPaused)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_enableLogging)
                Hecton8.Core.H8Debug.Log(isPaused ? PauseChangedPausedLog : PauseChangedUnpausedLog);
#endif
        }
    }
}
