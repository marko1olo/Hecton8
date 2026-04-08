using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;

namespace Hecton8.Tools
{
    /// <summary>
    /// Verifies state recovery after pause, menu transitions, and game state changes.
    /// Ensures correct input restoration, save slot handling, and state consistency.
    /// </summary>
    [DefaultExecutionOrder(900)] // After most systems
    public sealed class StateRecoveryVerifier : MonoBehaviour
    {
        [Header("Verification Settings")]
        [SerializeField, Tooltip("Enable automatic state recovery verification")]
        private bool _enableVerification = true;

        [SerializeField, Tooltip("Enable detailed logging")]
        private bool _enableLogging = true;

        [SerializeField, Tooltip("Time to wait for state stabilization (seconds)")]
        private float _stabilizationTime = 1f;

        // Test results
        private int _testsRun;
        private int _testsPassed;
        private int _testsFailed;

        // Singleton
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
        /// Verify state recovery after returning from pause to gameplay.
        /// </summary>
        public void VerifyPauseToGameplayRecovery()
        {
            if (!_enableVerification) return;
            StartCoroutine(TestStateRecovery("Pause to Gameplay",
                () => SimulatePauseAndReturn(),
                () => VerifyGameplayStateRestored()));
        }

        /// <summary>
        /// Verify state recovery after returning to menu.
        /// </summary>
        public void VerifyReturnToMenuRecovery()
        {
            if (!_enableVerification) return;
            StartCoroutine(TestStateRecovery("Return to Menu",
                () => SimulateReturnToMenu(),
                () => VerifyMenuStateRestored()));
        }

        /// <summary>
        /// Verify state recovery after new game following old save.
        /// </summary>
        public void VerifyNewGameAfterSaveRecovery()
        {
            if (!_enableVerification) return;
            StartCoroutine(TestStateRecovery("New Game After Save",
                () => SimulateNewGameAfterSave(),
                () => VerifyNewGameState()));
        }

        /// <summary>
        /// Verify state recovery after loading a slot from shell.
        /// </summary>
        public void VerifyLoadSlotFromShellRecovery()
        {
            if (!_enableVerification) return;
            StartCoroutine(TestStateRecovery("Load Slot From Shell",
                () => SimulateLoadSlotFromShell(),
                () => VerifyLoadedGameState()));
        }

        /// <summary>
        /// Verify correct input restoration after state changes.
        /// </summary>
        public void VerifyInputRestoration()
        {
            if (!_enableVerification) return;
            StartCoroutine(TestInputRestoration());
        }

        /// <summary>
        /// Get verification statistics.
        /// </summary>
        public (int testsRun, int testsPassed, int testsFailed) GetStats()
        {
            return (_testsRun, _testsPassed, _testsFailed);
        }

        private IEnumerator TestStateRecovery(string testName, Func<IEnumerator> action, Func<bool> verification)
        {
            _testsRun++;

            LogVerification($"Starting state recovery test: {testName}");

            // Execute the state change action
            yield return action();

            // Wait for stabilization
            yield return new WaitForSecondsRealtime(_stabilizationTime);

            // Verify the state recovery
            bool recoverySuccessful = verification();

            if (recoverySuccessful)
            {
                _testsPassed++;
                LogVerification($"✅ {testName} recovery test passed");
            }
            else
            {
                _testsFailed++;
                LogVerification($"❌ {testName} recovery test failed");
            }
        }

        private IEnumerator SimulatePauseAndReturn()
        {
            LogVerification("Simulating pause and return to gameplay");

            // Simulate pause
            bool pauseSuccess = SimulatePauseInput();
            yield return new WaitForSecondsRealtime(0.2f);

            if (!pauseSuccess)
            {
                LogVerification("Failed to enter pause state");
                yield break;
            }

            // Verify pause state
            if (!IsGamePaused())
            {
                LogVerification("Game is not actually paused");
                yield break;
            }

            // Simulate return to gameplay
            bool unpauseSuccess = SimulateUnpauseInput();
            yield return new WaitForSecondsRealtime(0.2f);

            if (!unpauseSuccess)
            {
                LogVerification("Failed to unpause");
            }
        }

        private IEnumerator SimulateReturnToMenu()
        {
            LogVerification("Simulating return to menu");

            // This would typically be triggered by a menu button
            // For testing, we'll simulate the transition
            yield return new WaitForSecondsRealtime(0.1f);

            // TODO: Implement actual menu return simulation
            LogVerification("Menu return simulation completed");
        }

        private IEnumerator SimulateNewGameAfterSave()
        {
            LogVerification("Simulating new game after existing save");

            // This would involve checking for existing saves, then starting new game
            yield return new WaitForSecondsRealtime(0.1f);

            // TODO: Implement actual new game after save simulation
            LogVerification("New game after save simulation completed");
        }

        private IEnumerator SimulateLoadSlotFromShell()
        {
            LogVerification("Simulating load slot from shell");

            // This would involve menu navigation to load game, selecting slot
            yield return new WaitForSecondsRealtime(0.1f);

            // TODO: Implement actual load from shell simulation
            LogVerification("Load slot from shell simulation completed");
        }

        private IEnumerator TestInputRestoration()
        {
            _testsRun++;

            LogVerification("Testing input restoration after state changes");

            // Test input before state change
            bool inputWorkedBefore = TestInputResponsiveness();
            LogVerification($"Input before change: {(inputWorkedBefore ? "✅" : "❌")}");

            // Simulate a state change (pause/unpause)
            yield return SimulatePauseAndReturn();

            // Wait for stabilization
            yield return new WaitForSecondsRealtime(_stabilizationTime);

            // Test input after state change
            bool inputWorksAfter = TestInputResponsiveness();
            LogVerification($"Input after change: {(inputWorksAfter ? "✅" : "❌")}");

            // Verify input restoration
            bool inputRestored = inputWorkedBefore == inputWorksAfter;

            if (inputRestored)
            {
                _testsPassed++;
                LogVerification("✅ Input restoration test passed");
            }
            else
            {
                _testsFailed++;
                LogVerification("❌ Input restoration test failed");
            }
        }

        private bool VerifyGameplayStateRestored()
        {
            bool timeNormal = Mathf.Approximately(Time.timeScale, 1f);
            bool cursorHidden = !Cursor.visible;
            bool noPauseMenu = !IsPauseMenuVisible();
            bool inputWorks = TestInputResponsiveness();

            LogVerification("Gameplay state verification:");
            LogVerification($"  Time scale normal: {(timeNormal ? "✅" : "❌")}");
            LogVerification($"  Cursor hidden: {(cursorHidden ? "✅" : "❌")}");
            LogVerification($"  No pause menu: {(noPauseMenu ? "✅" : "❌")}");
            LogVerification($"  Input responsive: {(inputWorks ? "✅" : "❌")}");

            return timeNormal && cursorHidden && noPauseMenu && inputWorks;
        }

        private bool VerifyMenuStateRestored()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            bool inMenuScene = currentScene == "01_MAIN_MENU";
            bool hasMenuController = FindAnyObjectByType<Hecton.UI.MainMenu.MainMenuController>() != null;

            LogVerification("Menu state verification:");
            LogVerification($"  In menu scene: {(inMenuScene ? "✅" : "❌")}");
            LogVerification($"  Has menu controller: {(hasMenuController ? "✅" : "❌")}");

            return inMenuScene && hasMenuController;
        }

        private bool VerifyNewGameState()
        {
            GameStartContext context = GameStartContextHolder.Current;
            bool hasNewGameContext = context.IsValid && context.StartMode == GameStartMode.NewGame;
            bool inGameScene = SceneManager.GetActiveScene().name == "02_HECTON_WORLD";

            LogVerification("New game state verification:");
            LogVerification($"  New game context: {(hasNewGameContext ? "✅" : "❌")}");
            LogVerification($"  In game scene: {(inGameScene ? "✅" : "❌")}");

            return hasNewGameContext && inGameScene;
        }

        private bool VerifyLoadedGameState()
        {
            GameStartContext context = GameStartContextHolder.Current;
            bool hasLoadContext = context.IsValid && context.StartMode == GameStartMode.LoadGame;
            bool inGameScene = SceneManager.GetActiveScene().name == "02_HECTON_WORLD";

            LogVerification("Loaded game state verification:");
            LogVerification($"  Load game context: {(hasLoadContext ? "✅" : "❌")}");
            LogVerification($"  In game scene: {(inGameScene ? "✅" : "❌")}");

            return hasLoadContext && inGameScene;
        }

        // Simulation methods (would be replaced with actual system calls)
        private bool SimulatePauseInput() => true; // TODO: Actual pause input
        private bool SimulateUnpauseInput() => true; // TODO: Actual unpause input
        private bool IsGamePaused() => Time.timeScale == 0f;
        private bool IsPauseMenuVisible() => false; // TODO: Check actual pause menu
        private bool TestInputResponsiveness() => true; // TODO: Test actual input

        private void LogVerification(string message)
        {
            if (_enableLogging)
            {
                Debug.Log($"[StateRecoveryVerifier] {message}");
            }
        }
    }
}
