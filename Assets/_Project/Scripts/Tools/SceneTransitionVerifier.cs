using System.Collections;
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
        [Header("Verification Settings")]
        [SerializeField, Tooltip("Enable automatic verification logging")]
        private bool _enableLogging = true;

        [SerializeField, Tooltip("Verify scene transitions automatically")]
        private bool _verifyTransitions = true;

        [SerializeField, Tooltip("Transition verification timeout in seconds")]
        private float _transitionTimeout = 10f;

        private string _lastSceneName;
        private float _sceneLoadStartTime;
        private bool _isTransitioning;

        public static SceneTransitionVerifier Instance { get; private set; }

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

            StartCoroutine(VerifyTransition(
                "New Game",
                () => string.Equals(SceneManager.GetActiveScene().name, "02_HECTON_WORLD", System.StringComparison.Ordinal),
                VerifyNewGameContext,
                "GameStartContext.StartMode should be NewGame and bootstrap should be alive"));
        }

        public void VerifyLoadGameTransition(string expectedSlot)
        {
            if (!_verifyTransitions)
                return;

            StartCoroutine(VerifyTransition(
                $"Load Game (Slot {expectedSlot})",
                () => string.Equals(SceneManager.GetActiveScene().name, "02_HECTON_WORLD", System.StringComparison.Ordinal),
                () => VerifyLoadGameContext(expectedSlot),
                $"GameStartContext should have LoadGame start mode and slot '{expectedSlot}'"));
        }

        public void VerifyReturnToMenu()
        {
            if (!_verifyTransitions)
                return;

            StartCoroutine(VerifyTransition(
                "Return to Menu",
                () => string.Equals(SceneManager.GetActiveScene().name, "01_MAIN_MENU", System.StringComparison.Ordinal),
                VerifyMenuReturnContext,
                "Menu should be active, bootstrap should be alive, and stale game-start context should be cleared"));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            float loadTime = Time.unscaledTime - _sceneLoadStartTime;
            _isTransitioning = false;
            LogVerification($"Scene loaded: {scene.name} (mode: {mode}, time: {loadTime:0.00}s)");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            LogVerification($"Scene unloaded: {scene.name}");
        }

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            _sceneLoadStartTime = Time.unscaledTime;
            _isTransitioning = true;
            _lastSceneName = current.name;
            LogVerification($"Active scene changed: {previous.name} -> {current.name}");
        }

        private IEnumerator VerifyTransition(string transitionName, System.Func<bool> sceneCheck, System.Func<bool> contextCheck, string contextDescription)
        {
            LogVerification($"Starting verification: {transitionName}");

            float deadline = Time.unscaledTime + Mathf.Max(0.1f, _transitionTimeout);
            while (_isTransitioning && Time.unscaledTime < deadline)
                yield return null;

            if (_isTransitioning)
            {
                LogVerification($"FAIL {transitionName} - transition timeout after {_transitionTimeout:0.00}s");
                yield break;
            }

            if (!sceneCheck())
            {
                LogVerification($"FAIL {transitionName} - scene check failed (current: {SceneManager.GetActiveScene().name})");
                yield break;
            }

            if (!contextCheck())
            {
                LogVerification($"FAIL {transitionName} - context check failed: {contextDescription}");
                yield break;
            }

            LogVerification($"PASS {transitionName} - verification passed");
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

        private void LogVerification(string message)
        {
            if (_enableLogging)
                Debug.Log($"[SceneTransitionVerifier] {message}");
        }
    }
}
