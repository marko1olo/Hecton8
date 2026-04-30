using System.Collections;
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
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Verifies pause open/close recovery back to gameplay.
        /// </summary>
        public void VerifyPauseToGameplayRecovery()
        {
            if (!_enableVerification)
                return;

            StartCoroutine(VerifyPauseToGameplayRecoveryRoutine());
        }

        /// <summary>
        /// Verifies world-to-menu recovery through the live pause-menu exit flow.
        /// </summary>
        public void VerifyReturnToMenuRecovery()
        {
            if (!_enableVerification)
                return;

            StartCoroutine(VerifyReturnToMenuRecoveryRoutine());
        }

        /// <summary>
        /// Verifies new-game handoff from shell after confirming an existing save is present.
        /// </summary>
        public void VerifyNewGameAfterSaveRecovery()
        {
            if (!_enableVerification)
                return;

            StartCoroutine(VerifyNewGameAfterSaveRecoveryRoutine());
        }

        /// <summary>
        /// Verifies loading an existing slot from shell into the world route.
        /// </summary>
        public void VerifyLoadSlotFromShellRecovery()
        {
            if (!_enableVerification)
                return;

            StartCoroutine(VerifyLoadSlotFromShellRecoveryRoutine());
        }

        /// <summary>
        /// Verifies action-map restoration before, during, and after pause.
        /// </summary>
        public void VerifyInputRestoration()
        {
            if (!_enableVerification)
                return;

            StartCoroutine(VerifyInputRestorationRoutine());
        }

        /// <summary>
        /// Returns current smoke-verifier counters.
        /// </summary>
        public (int testsRun, int testsPassed, int testsFailed) GetStats()
        {
            return (_testsRun, _testsPassed, _testsFailed);
        }

        private IEnumerator VerifyPauseToGameplayRecoveryRoutine()
        {
            yield return RunVerification("Pause to Gameplay", PauseToGameplaySequence);
        }

        private IEnumerator VerifyReturnToMenuRecoveryRoutine()
        {
            yield return RunVerification("Return to Menu", ReturnToMenuSequence);
        }

        private IEnumerator VerifyNewGameAfterSaveRecoveryRoutine()
        {
            yield return RunVerification("New Game After Save", NewGameAfterSaveSequence);
        }

        private IEnumerator VerifyLoadSlotFromShellRecoveryRoutine()
        {
            yield return RunVerification("Load Slot From Shell", LoadSlotFromShellSequence);
        }

        private IEnumerator VerifyInputRestorationRoutine()
        {
            yield return RunVerification("Input Restoration", InputRestorationSequence);
        }

        private IEnumerator RunVerification(string testName, System.Func<IEnumerator> sequenceFactory)
        {
            _testsRun++;
            LogVerification($"Starting verification: {testName}");

            yield return sequenceFactory != null ? sequenceFactory() : null;
        }

        private IEnumerator PauseToGameplaySequence()
        {
            ResolvePauseMenu();
            if (_pauseMenu == null)
            {
                Fail("Pause to Gameplay", "PauseMenuController not found.");
                yield break;
            }

            if (_pauseMenu.IsOpen)
            {
                _pauseMenu.Close();
                yield return WaitForCondition(IsGameplayStateRestored, "Pre-close pause state");
                if (!IsGameplayStateRestored())
                {
                    Fail("Pause to Gameplay", "Pause menu failed to close before test.");
                    yield break;
                }
            }

            _pauseMenu.Open();
            yield return WaitForCondition(IsPauseStateValid, "Pause open");
            if (!IsPauseStateValid())
            {
                Fail("Pause to Gameplay", "Pause state did not become valid.");
                yield break;
            }

            _pauseMenu.Close();
            yield return WaitForCondition(IsGameplayStateRestored, "Gameplay restore");

            if (!IsGameplayStateRestored())
            {
                Fail("Pause to Gameplay", "Gameplay state was not restored after pause close.");
                yield break;
            }

            Pass("Pause to Gameplay");
        }

        private IEnumerator ReturnToMenuSequence()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.Ordinal))
            {
                if (IsMainMenuStateValid())
                    Pass("Return to Menu");
                else
                    Fail("Return to Menu", "Already in main menu but menu state is invalid.");

                yield break;
            }

            yield return NavigateToMainMenu();
            if (!IsMainMenuStateValid())
            {
                Fail("Return to Menu", "Main menu recovery state is invalid after route handoff.");
                yield break;
            }

            Pass("Return to Menu");
        }

        private IEnumerator NewGameAfterSaveSequence()
        {
            if (!HasAnySaveSlot())
            {
                Fail("New Game After Save", "No existing save slot found. Test precondition is false.");
                yield break;
            }

            yield return EnsureMainMenuScene();
            if (!IsMainMenuStateValid())
            {
                Fail("New Game After Save", "Failed to reach a valid menu shell before new game handoff.");
                yield break;
            }

            ResolveMainMenuController();
            if (_mainMenuController == null)
            {
                Fail("New Game After Save", "MainMenuController not found.");
                yield break;
            }

            _mainMenuController.StartGame(string.Empty);
            yield return WaitForCondition(IsNewGameStateValid, "New game world handoff");

            if (!IsNewGameStateValid())
            {
                Fail("New Game After Save", "New game context/world state is invalid after StartGame.");
                yield break;
            }

            Pass("New Game After Save");
        }

        private IEnumerator LoadSlotFromShellSequence()
        {
            string slotName = ResolveExistingSaveSlot();
            if (string.IsNullOrEmpty(slotName))
            {
                Fail("Load Slot From Shell", "No existing save slot found.");
                yield break;
            }

            yield return EnsureMainMenuScene();
            if (!IsMainMenuStateValid())
            {
                Fail("Load Slot From Shell", "Failed to reach a valid menu shell before load handoff.");
                yield break;
            }

            ResolveMainMenuController();
            if (_mainMenuController == null)
            {
                Fail("Load Slot From Shell", "MainMenuController not found.");
                yield break;
            }

            _mainMenuController.StartGame(slotName);
            yield return WaitForCondition(() => IsLoadGameStateValid(slotName), "Load-game world handoff");

            if (!IsLoadGameStateValid(slotName))
            {
                Fail("Load Slot From Shell", $"Load-game context/world state is invalid for slot '{slotName}'.");
                yield break;
            }

            Pass("Load Slot From Shell");
        }

        private IEnumerator InputRestorationSequence()
        {
            ResolvePauseMenu();
            if (_pauseMenu == null)
            {
                Fail("Input Restoration", "PauseMenuController not found.");
                yield break;
            }

            if (!IsGameplayInputModeValid())
            {
                Fail("Input Restoration", "Gameplay input mode is not valid before pause.");
                yield break;
            }

            _pauseMenu.Open();
            yield return WaitForCondition(IsPauseStateValid, "Pause input mode");
            if (!IsPauseStateValid())
            {
                Fail("Input Restoration", "Pause state was not valid during input restoration test.");
                yield break;
            }

            _pauseMenu.Close();
            yield return WaitForCondition(IsGameplayStateRestored, "Gameplay input restore");

            if (!IsGameplayStateRestored())
            {
                Fail("Input Restoration", "Gameplay input mode was not restored after pause.");
                yield break;
            }

            Pass("Input Restoration");
        }

        private IEnumerator EnsureMainMenuScene()
        {
            if (IsMainMenuStateValid())
                yield break;

            yield return NavigateToMainMenu();
        }

        private IEnumerator NavigateToMainMenu()
        {
            ResolvePauseMenu();
            if (_pauseMenu == null)
                yield break;

            if (!_pauseMenu.IsOpen)
            {
                _pauseMenu.Open();
                yield return WaitForCondition(IsPauseStateValid, "Pause open for menu return");
                if (!IsPauseStateValid())
                    yield break;
            }

            Button exitToMenuButton = VerificationRuntimeProbe.ResolvePauseExitToMenuButton(_pauseMenu);
            if (exitToMenuButton == null)
                yield break;

            exitToMenuButton.onClick.Invoke();
            yield return WaitForCondition(IsMainMenuStateValid, "Main-menu scene handoff");
        }

        private IEnumerator WaitForCondition(System.Func<bool> predicate, string label)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, _actionTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (predicate())
                {
                    if (_stabilizationTime > 0f)
                        yield return new WaitForSecondsRealtime(_stabilizationTime);

                    LogVerification($"PASS {label}");
                    yield break;
                }

                yield return null;
            }

            LogVerification($"FAIL {label}: timeout after {_actionTimeout:0.00}s");
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
            InputManager inputManager = InputManager.Instance;
            if (inputManager == null || !inputManager.CanSwitchActionMaps)
                return false;

            return inputManager.IsUIInputEnabled && !inputManager.IsPlayerInputEnabled;
        }

        private bool IsGameplayInputModeValid()
        {
            InputManager inputManager = InputManager.Instance;
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
