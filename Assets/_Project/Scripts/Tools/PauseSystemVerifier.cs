using System.Collections;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Tools
{
    /// <summary>
    /// Verifies pause menu functionality using real runtime state instead of stubbed pass values.
    /// </summary>
    [DefaultExecutionOrder(800)]
    public sealed class PauseSystemVerifier : MonoBehaviour, ITickable
    {
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
        private bool _isPaused;
        private int _testsRun;
        private int _testsPassed;
        private int _testsFailed;
        private PauseMenuController _pauseMenu;

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
            ResolvePauseMenu();
            _isPaused = IsGamePaused();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
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

            StartCoroutine(TestPauseMenuFlow());
        }

        public (int testsRun, int testsPassed, int testsFailed) GetStats()
        {
            return (_testsRun, _testsPassed, _testsFailed);
        }

        private IEnumerator TestPauseMenuFlow()
        {
            _testsRun++;
            ResolvePauseMenu();

            if (_pauseMenu == null)
            {
                _testsFailed++;
                LogVerification("FAIL pause flow: PauseMenuController not found.");
                yield break;
            }

            LogVerification("Testing pause menu navigation flow");

            if (_pauseMenu.IsOpen)
            {
                _pauseMenu.Close();
                yield return WaitForCondition(() => !_pauseMenu.IsOpen, "Initial pause close");
            }

            bool pauseTriggered = SimulatePauseInput();
            if (!pauseTriggered)
            {
                _testsFailed++;
                LogVerification("FAIL pause flow: could not issue pause open.");
                yield break;
            }

            yield return WaitForCondition(
                () => _pauseMenu.IsOpen && IsPauseMenuVisible(),
                "Pause open");

            bool pauseStateValid = IsGamePaused() && IsPauseMenuVisible() && IsPauseInputModeValid();
            bool navigationWorks = TestMenuNavigation();

            bool unpauseTriggered = SimulateUnpauseInput();
            if (!unpauseTriggered)
            {
                _testsFailed++;
                LogVerification("FAIL pause flow: could not issue pause close.");
                yield break;
            }

            yield return WaitForCondition(
                () => !_pauseMenu.IsOpen && !IsPauseMenuVisible(),
                "Pause close");

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

        private IEnumerator WaitForCondition(System.Func<bool> predicate, string label)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.05f, _actionTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (predicate())
                {
                    if (_settleDelay > 0f)
                        yield return new WaitForSecondsRealtime(_settleDelay);

                    LogVerification($"PASS {label}");
                    yield break;
                }

                yield return null;
            }

            LogVerification($"FAIL {label}: timeout after {_actionTimeout:0.00}s");
        }

        private void OnPauseStateChanged(bool isPaused)
        {
            LogVerification($"Pause state changed: {(isPaused ? "PAUSED" : "UNPAUSED")}");

            if (isPaused)
                VerifyPauseEntry();
            else
                VerifyPauseExit();
        }

        private void VerifyPauseEntry()
        {
            ResolvePauseMenu();

            bool timeStopped = Mathf.Approximately(Time.timeScale, 0f);
            bool cursorVisible = Cursor.visible;
            bool menuVisible = IsPauseMenuVisible();
            bool inputValid = IsPauseInputModeValid();
            bool selectionValid = VerificationRuntimeProbe.HasPauseSelection(_pauseMenu);

            LogVerification("Pause entry verification:");
            LogVerification($"  Time stopped: {(timeStopped ? "PASS" : "FAIL")}");
            LogVerification($"  Cursor visible: {(cursorVisible ? "PASS" : "FAIL")}");
            LogVerification($"  Menu visible: {(menuVisible ? "PASS" : "FAIL")}");
            LogVerification($"  UI input active: {(inputValid ? "PASS" : "FAIL")}");
            LogVerification($"  Pause selection valid: {(selectionValid ? "PASS" : "FAIL")}");
        }

        private void VerifyPauseExit()
        {
            ResolvePauseMenu();

            bool timeResumed = Time.timeScale > 0f;
            bool cursorHidden = !Cursor.visible;
            bool menuHidden = !IsPauseMenuVisible();
            bool inputValid = IsGameplayInputModeValid();

            LogVerification("Pause exit verification:");
            LogVerification($"  Time resumed: {(timeResumed ? "PASS" : "FAIL")}");
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
            return _pauseMenu != null && _pauseMenu.IsOpen && Mathf.Approximately(Time.timeScale, 0f);
        }

        private bool IsPauseMenuVisible()
        {
            ResolvePauseMenu();
            return VerificationRuntimeProbe.IsPauseMenuVisible(_pauseMenu);
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

        private void ResolvePauseMenu()
        {
            if (_pauseMenu != null)
                return;

            _pauseMenu = VerificationRuntimeProbe.ResolvePauseMenu();
        }

        private void LogVerification(string message)
        {
            if (_enableLogging)
                Debug.Log($"[PauseSystemVerifier] {message}");
        }
    }
}
