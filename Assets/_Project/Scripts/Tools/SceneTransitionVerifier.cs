using System;
using System.Globalization;
using System.Threading;
using Hecton.UI.MainMenu;
using Hecton8.Bootstrap;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Tools
{
    /// <summary>
    /// Verifies scene transitions and start-flow ownership using live scene and context truth.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SceneTransitionVerifier : MonoBehaviour
    {
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string OrbitSceneName = "01_ORBIT";
        private const string WorldSceneName = "02_HECTON_WORLD";

        [Header("Verification Settings")]
        [SerializeField, Tooltip("Enable automatic verification logging")]
        private bool _enableLogging = true;

        [SerializeField, Tooltip("Verify scene transitions automatically")]
        private bool _verifyTransitions = true;

        [SerializeField, Tooltip("Transition verification timeout in seconds")]
        private float _transitionTimeout = 10f;

        private string _lastSceneName;
        private double _sceneLoadStartTime;
        private bool _isTransitioning;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void Start()
        {
            _lastSceneName = SceneManager.GetActiveScene().name;
            LogVerification($"Initial scene: {_lastSceneName}");
        }

        public void VerifyNewGameTransition()
        {
            if (!_verifyTransitions)
                return;

            _ = VerifyTransitionAsync(
                "New Game",
                () => string.Equals(SceneManager.GetActiveScene().name, OrbitSceneName, System.StringComparison.Ordinal),
                VerifyNewGameContext,
                "GameStartContext.StartMode should be NewGame and prologue route should be active",
                destroyCancellationToken);
        }

        public void VerifyLoadGameTransition(string expectedSlot)
        {
            if (!_verifyTransitions)
                return;

            _ = VerifyTransitionAsync(
                $"Load Game (Slot {expectedSlot})",
                () => string.Equals(SceneManager.GetActiveScene().name, WorldSceneName, System.StringComparison.Ordinal),
                () => VerifyLoadGameContext(expectedSlot),
                $"GameStartContext should have LoadGame start mode and slot '{expectedSlot}'",
                destroyCancellationToken);
        }

        public void VerifyReturnToMenu()
        {
            if (!_verifyTransitions)
                return;

            _ = VerifyTransitionAsync(
                "Return to Menu",
                () => string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.Ordinal),
                VerifyMenuReturnContext,
                "Menu should be active, bootstrap should be alive, and stale game-start context should be cleared",
                destroyCancellationToken);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            float loadTime = (float)(SystemDispatcher.CurrentUnscaledTimeSeconds - _sceneLoadStartTime);
            _isTransitioning = false;
            LogVerification("Scene loaded: " + scene.name + " (mode: " + mode + ", time: " + loadTime.ToString("0.00", CultureInfo.InvariantCulture) + "s)");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            LogVerification($"Scene unloaded: {scene.name}");
        }

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            _sceneLoadStartTime = SystemDispatcher.CurrentUnscaledTimeSeconds;
            _isTransitioning = true;
            _lastSceneName = current.name;
            LogVerification($"Active scene changed: {previous.name} -> {current.name}");
        }

        private async Awaitable VerifyTransitionAsync(
            string transitionName,
            Func<bool> sceneCheck,
            Func<bool> contextCheck,
            string contextDescription,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogVerification($"Starting verification: {transitionName}");

                double deadline = SystemDispatcher.CurrentUnscaledTimeSeconds + Mathf.Max(0.1f, _transitionTimeout);
                while (SystemDispatcher.CurrentUnscaledTimeSeconds < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (sceneCheck() && contextCheck())
                    {
                        LogVerification($"PASS {transitionName} - verification passed");
                        return;
                    }

                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                }

                if (_isTransitioning)
                {
                    LogVerification("FAIL " + transitionName + " - transition timeout after " + _transitionTimeout.ToString("0.00", CultureInfo.InvariantCulture) + "s");
                    return;
                }

                if (!sceneCheck())
                {
                    LogVerification($"FAIL {transitionName} - scene check failed (current: {SceneManager.GetActiveScene().name})");
                    return;
                }

                LogVerification($"FAIL {transitionName} - context check failed: {contextDescription}");
            }
            catch (OperationCanceledException)
            {
                LogVerification($"Verification cancelled: {transitionName}");
            }
            catch (Exception exception)
            {
                LogVerification($"FAIL {transitionName} - exception {exception.Message}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogException(exception);
#endif
            }
        }

        private static bool VerifyNewGameContext()
        {
            GameStartContext context = GameStartContextHolder.Current;
            return context.IsValid &&
                   context.StartMode == GameStartMode.NewGame &&
                   GameBootstrapper.AreAllSystemsReady();
        }

        private static bool VerifyLoadGameContext(string expectedSlot)
        {
            GameStartContext context = GameStartContextHolder.Current;
            return context.IsValid &&
                   context.StartMode == GameStartMode.LoadGame &&
                   string.Equals(context.TargetSaveSlot, expectedSlot, System.StringComparison.Ordinal) &&
                   GameBootstrapper.AreAllSystemsReady();
        }

        private static bool VerifyMenuReturnContext()
        {
            MainMenuController menuController = VerificationRuntimeProbe.ResolveMainMenuController();
            return menuController != null &&
                   GameBootstrapper.AreAllSystemsReady() &&
                   !GameStartContextHolder.Current.IsValid;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogVerification(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_enableLogging)
                Hecton8.Core.H8Debug.Log($"[SceneTransitionVerifier] {message}");
#endif
        }
    }
}
