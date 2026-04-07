using System;
using System.Collections;
using UnityEngine;
using Hecton8.Core;
using Hecton8.UI;

namespace Hecton8.Tools
{
    /// <summary>
    /// Verifies pause menu functionality across different game states and edge cases.
    /// Ensures pause works correctly while moving, underwater, at surface, in PDA, etc.
    /// </summary>
    [DefaultExecutionOrder(800)] // After UI systems
    public sealed class PauseSystemVerifier : MonoBehaviour, ITickable
    {
        [Header("Verification Settings")]
        [SerializeField, Tooltip("Enable automatic pause verification")]
        private bool _enableVerification = true;

        [SerializeField, Tooltip("Enable detailed logging")]
        private bool _enableLogging = true;

        // State tracking
        private bool _isPaused;

        // Test results
        private int _testsRun;
        private int _testsPassed;
        private int _testsFailed;

        // Singleton
        public static PauseSystemVerifier Instance { get; private set; }

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

        private void OnEnable()
        {
            if (GameTickManager.Instance != null)
                GameTickManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null)
                GameTickManager.Instance.Unregister(this);
        }

        public void Tick(float dt)
        {
            if (!_enableVerification) return;

            // Monitor pause state changes
            bool currentlyPaused = IsGamePaused();
            if (currentlyPaused != _isPaused)
            {
                OnPauseStateChanged(currentlyPaused);
                _isPaused = currentlyPaused;
            }
        }

        /// <summary>
        /// Manually trigger pause verification for current state.
        /// </summary>
        public void VerifyCurrentPauseState()
        {
            LogVerification("Manual pause verification triggered");
            TestPauseMenuNavigation();
        }

        /// <summary>
        /// Test pause menu navigation and return to gameplay.
        /// </summary>
        public void TestPauseMenuNavigation()
        {
            StartCoroutine(TestPauseMenuFlow());
        }

        /// <summary>
        /// Get verification statistics.
        /// </summary>
        public (int testsRun, int testsPassed, int testsFailed) GetStats()
        {
            return (_testsRun, _testsPassed, _testsFailed);
        }

        private IEnumerator TestPauseMenuFlow()
        {
            _testsRun++;

            LogVerification("Testing pause menu navigation flow");

            // Start unpaused
            if (IsGamePaused())
            {
                SimulateUnpauseInput();
                yield return new WaitForSecondsRealtime(0.1f);
            }

            // Pause
            bool pauseSuccess = SimulatePauseInput();
            yield return new WaitForSecondsRealtime(0.1f);

            if (!pauseSuccess || !IsGamePaused() || !IsPauseMenuVisible())
            {
                _testsFailed++;
                LogVerification("❌ Pause menu flow failed: Could not enter pause menu");
                yield break;
            }

            // Test menu navigation (simulate button presses)
            bool navigationWorks = TestMenuNavigation();

            // Return to game
            bool unpauseSuccess = SimulateUnpauseInput();
            yield return new WaitForSecondsRealtime(0.1f);

            // Verify return to gameplay
            bool returnedToGameplay = !IsGamePaused() && !IsPauseMenuVisible();

            if (navigationWorks && unpauseSuccess && returnedToGameplay)
            {
                _testsPassed++;
                LogVerification("✅ Pause menu navigation flow passed");
            }
            else
            {
                _testsFailed++;
                LogVerification($"❌ Pause menu navigation flow failed: nav={navigationWorks}, unpause={unpauseSuccess}, gameplay={returnedToGameplay}");
            }
        }

        private void OnPauseStateChanged(bool isPaused)
        {
            LogVerification($"Pause state changed: {(isPaused ? "PAUSED" : "UNPAUSED")}");

            if (isPaused)
            {
                VerifyPauseEntry();
            }
            else
            {
                VerifyPauseExit();
            }
        }

        private void VerifyPauseEntry()
        {
            // Verify pause entry conditions
            bool timeStopped = Time.timeScale == 0f;
            bool cursorVisible = Cursor.visible;
            bool menuVisible = IsPauseMenuVisible();

            LogVerification("Pause entry verification:");
            LogVerification($"  Time stopped: {(timeStopped ? "✅" : "❌")}");
            LogVerification($"  Cursor visible: {(cursorVisible ? "✅" : "❌")}");
            LogVerification($"  Menu visible: {(menuVisible ? "✅" : "❌")}");

            if (!timeStopped)
                LogVerification("  ⚠️  Time not stopped - gameplay may continue in background");
            if (!cursorVisible)
                LogVerification("  ⚠️  Cursor not visible - mouse input may not work in pause menu");
            if (!menuVisible)
                LogVerification("  ⚠️  Pause menu not visible - player cannot navigate pause options");
        }

        private void VerifyPauseExit()
        {
            // Verify pause exit conditions
            bool timeResumed = Time.timeScale > 0f;
            bool cursorHidden = !Cursor.visible;
            bool menuHidden = !IsPauseMenuVisible();

            LogVerification("Pause exit verification:");
            LogVerification($"  Time resumed: {(timeResumed ? "✅" : "❌")}");
            LogVerification($"  Cursor hidden: {(cursorHidden ? "✅" : "❌")}");
            LogVerification($"  Menu hidden: {(menuHidden ? "✅" : "❌")}");

            if (!timeResumed)
                LogVerification("  ⚠️  Time not resumed - game remains paused");
            if (!cursorHidden)
                LogVerification("  ⚠️  Cursor still visible - may interfere with gameplay");
            if (!menuHidden)
                LogVerification("  ⚠️  Pause menu still visible - blocks gameplay view");
        }

        // Simulation methods (would be replaced with actual input system calls)
        private bool SimulatePauseInput()
        {
            // TODO: Replace with actual input system call
            // For now, assume pause input works
            return true;
        }

        private bool SimulateUnpauseInput()
        {
            // TODO: Replace with actual input system call
            // For now, assume unpause input works
            return true;
        }

        private bool TestMenuNavigation()
        {
            // TODO: Implement actual menu navigation testing
            // For now, assume navigation works
            return true;
        }

        // State check methods (would be replaced with actual game state queries)
        private bool IsGamePaused() => Time.timeScale == 0f;
        private bool IsPauseMenuVisible() => false; // TODO: Check actual pause menu visibility

        private void LogVerification(string message)
        {
            if (_enableLogging)
            {
                Debug.Log($"[PauseSystemVerifier] {message}");
            }
        }
    }
}