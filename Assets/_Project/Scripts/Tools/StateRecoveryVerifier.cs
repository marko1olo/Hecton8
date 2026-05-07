using System;
using System.Threading;
using Hecton.UI.MainMenu;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Tools
{
    /// <summary>
    /// Verifies shell and pause recovery flows against live runtime owners instead of stubbed pass values.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class StateRecoveryVerifier : MonoBehaviour
    {
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string WorldSceneName = "02_HECTON_WORLD";

        [Header("Verification Settings")]
        [SerializeField, Tooltip("Enable automatic state recovery verification")]
        private bool _enableVerification = true;

        [SerializeField, Tooltip("Enable detailed logging")]
        private bool _enableLogging = true;

        [SerializeField, Tooltip("Realtime timeout for scene and state handoffs")]
        private float _actionTimeout = 10f;

        [SerializeField, Tooltip("Extra realtime delay after state changes")]
        private float _stabilizationTime = 0.2f;

        [SerializeField, Tooltip("Save slot probe order for load-state verification")]
        private string[] _saveSlotProbeOrder = { "slot_1", "slot_2", "slot_3" };

        private int _testsRun;
        private int _testsPassed;
        private int _testsFailed;
        private PauseMenuController _pauseMenu;
        private MainMenuController _mainMenuController;

        public static StateRecoveryVerifier Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            GameBootstrapper.PersistRuntimeService(this);
        }

        /// <summary>
        /// Verifies pause open/close recovery back to gameplay.
        /// </summary>
        public void VerifyPauseToGameplayRecovery()
        {
            if (!_enableVerification)
                return;

            _ = VerifyPauseToGameplayRecoveryAsync(destroyCancellationToken);
        }

        /// <summary>
        /// Verifies world-to-menu recovery through the live pause-menu exit flow.
        /// </summary>
        public void VerifyReturnToMenuRecovery()
        {
            if (!_enableVerification)
                return;

            _ = VerifyReturnToMenuRecoveryAsync(destroyCancellationToken);
        }

        /// <summary>
        /// Verifies new-game handoff from shell after confirming an existing save is present.
        /// </summary>
        public void VerifyNewGameAfterSaveRecovery()
        {
            if (!_enableVerification)
                return;

            _ = VerifyNewGameAfterSaveRecoveryAsync(destroyCancellationToken);
        }

        /// <summary>
        /// Verifies loading an existing slot from shell into the world route.
        /// </summary>
        public void VerifyLoadSlotFromShellRecovery()
        {
            if (!_enableVerification)
                return;

            _ = VerifyLoadSlotFromShellRecoveryAsync(destroyCancellationToken);
        }

        /// <summary>
        /// Verifies action-map restoration before, during, and after pause.
        /// </summary>
        public void VerifyInputRestoration()
        {
            if (!_enableVerification)
                return;

            _ = VerifyInputRestorationAsync(destroyCancellationToken);
        }

        /// <summary>
        /// Returns current smoke-verifier counters.
        /// </summary>
        public (int testsRun, int testsPassed, int testsFailed) GetStats()
        {
            return (_testsRun, _testsPassed, _testsFailed);
        }

        private async Awaitable VerifyPauseToGameplayRecoveryAsync(CancellationToken cancellationToken)
        {
            await RunVerificationAsync("Pause to Gameplay", PauseToGameplaySequenceAsync, cancellationToken);
        }

        private async Awaitable VerifyReturnToMenuRecoveryAsync(CancellationToken cancellationToken)
        {
            await RunVerificationAsync("Return to Menu", ReturnToMenuSequenceAsync, cancellationToken);
        }

        private async Awaitable VerifyNewGameAfterSaveRecoveryAsync(CancellationToken cancellationToken)
        {
            await RunVerificationAsync("New Game After Save", NewGameAfterSaveSequenceAsync, cancellationToken);
        }

        private async Awaitable VerifyLoadSlotFromShellRecoveryAsync(CancellationToken cancellationToken)
        {
            await RunVerificationAsync("Load Slot From Shell", LoadSlotFromShellSequenceAsync, cancellationToken);
        }

        private async Awaitable VerifyInputRestorationAsync(CancellationToken cancellationToken)
        {
            await RunVerificationAsync("Input Restoration", InputRestorationSequenceAsync, cancellationToken);
        }

        private async Awaitable RunVerificationAsync(
            string testName,
            Func<CancellationToken, Awaitable> sequenceFactory,
            CancellationToken cancellationToken)
        {
            _testsRun++;
            LogVerification($"Starting verification: {testName}");

            if (sequenceFactory == null)
                return;

            try
            {
                await sequenceFactory(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogVerification($"CANCELLED {testName}");
            }
            catch (Exception exception)
            {
                _testsFailed++;
                LogVerification($"FAIL {testName}: exception {exception.Message}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception, this);
#endif
            }
        }

        private async Awaitable PauseToGameplaySequenceAsync(CancellationToken cancellationToken)
        {
            ResolvePauseMenu();
            if (_pauseMenu == null)
            {
                Fail("Pause to Gameplay", "PauseMenuController not found.");
                return;
            }

            if (_pauseMenu.IsOpen)
            {
                _pauseMenu.Close();
                await WaitForConditionAsync(IsGameplayStateRestored, "Pre-close pause state", cancellationToken);
                if (!IsGameplayStateRestored())
                {
                    Fail("Pause to Gameplay", "Pause menu failed to close before test.");
                    return;
                }
            }

            _pauseMenu.Open();
            await WaitForConditionAsync(IsPauseStateValid, "Pause open", cancellationToken);
            if (!IsPauseStateValid())
            {
                Fail("Pause to Gameplay", "Pause state did not become valid.");
                return;
            }

            _pauseMenu.Close();
            await WaitForConditionAsync(IsGameplayStateRestored, "Gameplay restore", cancellationToken);

            if (!IsGameplayStateRestored())
            {
                Fail("Pause to Gameplay", "Gameplay state was not restored after pause close.");
                return;
            }

            Pass("Pause to Gameplay");
        }

        private async Awaitable ReturnToMenuSequenceAsync(CancellationToken cancellationToken)
        {
            if (string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.Ordinal))
            {
                if (IsMainMenuStateValid())
                    Pass("Return to Menu");
                else
                    Fail("Return to Menu", "Already in main menu but menu state is invalid.");

                return;
            }

            await NavigateToMainMenuAsync(cancellationToken);
            if (!IsMainMenuStateValid())
            {
                Fail("Return to Menu", "Main menu recovery state is invalid after route handoff.");
                return;
            }

            Pass("Return to Menu");
        }

        private async Awaitable NewGameAfterSaveSequenceAsync(CancellationToken cancellationToken)
        {
            if (!HasAnySaveSlot())
            {
                Fail("New Game After Save", "No existing save slot found. Test precondition is false.");
                return;
            }

            await EnsureMainMenuSceneAsync(cancellationToken);
            if (!IsMainMenuStateValid())
            {
                Fail("New Game After Save", "Failed to reach a valid menu shell before new game handoff.");
                return;
            }

            ResolveMainMenuController();
            if (_mainMenuController == null)
            {
                Fail("New Game After Save", "MainMenuController not found.");
                return;
            }

            _mainMenuController.StartGame(string.Empty);
            await WaitForConditionAsync(IsNewGameStateValid, "New game world handoff", cancellationToken);

            if (!IsNewGameStateValid())
            {
                Fail("New Game After Save", "New game context/world state is invalid after StartGame.");
                return;
            }

            Pass("New Game After Save");
        }

        private async Awaitable LoadSlotFromShellSequenceAsync(CancellationToken cancellationToken)
        {
            string slotName = ResolveExistingSaveSlot();
            if (string.IsNullOrEmpty(slotName))
            {
                Fail("Load Slot From Shell", "No existing save slot found.");
                return;
            }

            await EnsureMainMenuSceneAsync(cancellationToken);
            if (!IsMainMenuStateValid())
            {
                Fail("Load Slot From Shell", "Failed to reach a valid menu shell before load handoff.");
                return;
            }

            ResolveMainMenuController();
            if (_mainMenuController == null)
            {
                Fail("Load Slot From Shell", "MainMenuController not found.");
                return;
            }

            _mainMenuController.StartGame(slotName);
            await WaitForConditionAsync(() => IsLoadGameStateValid(slotName), "Load-game world handoff", cancellationToken);

            if (!IsLoadGameStateValid(slotName))
            {
                Fail("Load Slot From Shell", $"Load-game context/world state is invalid for slot '{slotName}'.");
                return;
            }

            Pass("Load Slot From Shell");
        }

        private async Awaitable InputRestorationSequenceAsync(CancellationToken cancellationToken)
        {
            ResolvePauseMenu();
            if (_pauseMenu == null)
            {
                Fail("Input Restoration", "PauseMenuController not found.");
                return;
            }

            if (!IsGameplayInputModeValid())
            {
                Fail("Input Restoration", "Gameplay input mode is not valid before pause.");
                return;
            }

            _pauseMenu.Open();
            await WaitForConditionAsync(IsPauseStateValid, "Pause input mode", cancellationToken);
            if (!IsPauseStateValid())
            {
                Fail("Input Restoration", "Pause state was not valid during input restoration test.");
                return;
            }

            _pauseMenu.Close();
            await WaitForConditionAsync(IsGameplayStateRestored, "Gameplay input restore", cancellationToken);

            if (!IsGameplayStateRestored())
            {
                Fail("Input Restoration", "Gameplay input mode was not restored after pause.");
                return;
            }

            Pass("Input Restoration");
        }

        private async Awaitable EnsureMainMenuSceneAsync(CancellationToken cancellationToken)
        {
            if (IsMainMenuStateValid())
                return;

            await NavigateToMainMenuAsync(cancellationToken);
        }

        private async Awaitable NavigateToMainMenuAsync(CancellationToken cancellationToken)
        {
            ResolvePauseMenu();
            if (_pauseMenu == null)
                return;

            if (!_pauseMenu.IsOpen)
            {
                _pauseMenu.Open();
                await WaitForConditionAsync(IsPauseStateValid, "Pause open for menu return", cancellationToken);
                if (!IsPauseStateValid())
                    return;
            }

            Button exitToMenuButton = VerificationRuntimeProbe.ResolvePauseExitToMenuButton(_pauseMenu);
            if (exitToMenuButton == null)
                return;

            exitToMenuButton.onClick.Invoke();
            await WaitForConditionAsync(IsMainMenuStateValid, "Main-menu scene handoff", cancellationToken);
        }

        private async Awaitable WaitForConditionAsync(Func<bool> predicate, string label, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, _actionTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (predicate())
                {
                    if (_stabilizationTime > 0f)
                        await DelayRealtimeAsync(_stabilizationTime, cancellationToken);

                    LogVerification($"PASS {label}");
                    return;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            LogVerification($"FAIL {label}: timeout after {_actionTimeout:0.00}s");
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

        private bool IsPauseStateValid()
        {
            ResolvePauseMenu();
            return _pauseMenu != null &&
                   _pauseMenu.IsOpen &&
                   VerificationRuntimeProbe.IsPauseMenuVisible(_pauseMenu) &&
                   Mathf.Approximately(Time.timeScale, 0f) &&
                   IsPauseInputModeValid();
        }

        private bool IsGameplayStateRestored()
        {
            ResolvePauseMenu();
            return (_pauseMenu == null || !_pauseMenu.IsOpen) &&
                   !VerificationRuntimeProbe.IsPauseMenuVisible(_pauseMenu) &&
                   Time.timeScale > 0f &&
                   !Cursor.visible &&
                   IsGameplayInputModeValid();
        }

        private bool IsMainMenuStateValid()
        {
            ResolveMainMenuController();
            return string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.Ordinal) &&
                   _mainMenuController != null &&
                   GameBootstrapper.AreAllSystemsReady() &&
                   !GameStartContextHolder.Current.IsValid;
        }

        private bool IsNewGameStateValid()
        {
            GameStartContext context = GameStartContextHolder.Current;
            return string.Equals(SceneManager.GetActiveScene().name, WorldSceneName, System.StringComparison.Ordinal) &&
                   GameBootstrapper.AreAllSystemsReady() &&
                   context.IsValid &&
                   context.StartMode == GameStartMode.NewGame;
        }

        private bool IsLoadGameStateValid(string expectedSlot)
        {
            GameStartContext context = GameStartContextHolder.Current;
            return string.Equals(SceneManager.GetActiveScene().name, WorldSceneName, System.StringComparison.Ordinal) &&
                   GameBootstrapper.AreAllSystemsReady() &&
                   context.IsValid &&
                   context.StartMode == GameStartMode.LoadGame &&
                   string.Equals(context.TargetSaveSlot, expectedSlot, System.StringComparison.Ordinal);
        }

        private bool IsPauseInputModeValid()
        {
            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (inputManager == null || !inputManager.CanSwitchActionMaps)
                return false;

            return inputManager.IsUIInputEnabled && !inputManager.IsPlayerInputEnabled;
        }

        private bool IsGameplayInputModeValid()
        {
            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (inputManager == null || !inputManager.CanSwitchActionMaps)
                return false;

            return inputManager.IsPlayerInputEnabled && !inputManager.IsUIInputEnabled;
        }

        private bool HasAnySaveSlot()
        {
            return !string.IsNullOrEmpty(ResolveExistingSaveSlot());
        }

        private string ResolveExistingSaveSlot()
        {
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null || _saveSlotProbeOrder == null)
                return string.Empty;

            for (int i = 0; i < _saveSlotProbeOrder.Length; i++)
            {
                string slotName = _saveSlotProbeOrder[i];
                if (string.IsNullOrEmpty(slotName))
                    continue;

                if (saveManager.SaveExists(slotName))
                    return slotName;
            }

            return string.Empty;
        }

        private void ResolvePauseMenu()
        {
            if (_pauseMenu != null)
                return;

            _pauseMenu = VerificationRuntimeProbe.ResolvePauseMenu();
        }

        private void ResolveMainMenuController()
        {
            if (_mainMenuController != null)
                return;

            _mainMenuController = VerificationRuntimeProbe.ResolveMainMenuController();
        }

        private void Pass(string testName)
        {
            _testsPassed++;
            LogVerification($"PASS {testName}");
        }

        private void Fail(string testName, string reason)
        {
            _testsFailed++;
            LogVerification($"FAIL {testName}: {reason}");
        }

        private void LogVerification(string message)
        {
            if (_enableLogging)
                Debug.Log($"[StateRecoveryVerifier] {message}");
        }
    }
}
