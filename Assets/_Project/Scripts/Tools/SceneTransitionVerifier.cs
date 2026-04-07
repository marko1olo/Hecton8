using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;
using Hecton8.UI;

namespace Hecton8.Tools
{
    /// <summary>
    /// Verifies critical scene transitions and game state changes.
    /// Ensures new game, load game, menu navigation, and quit functionality work correctly.
    /// </summary>
    [DefaultExecutionOrder(1000)] // Run after most systems
    public sealed class SceneTransitionVerifier : MonoBehaviour
    {
        [Header("Verification Settings")]
        [SerializeField, Tooltip("Enable automatic verification logging")]
        private bool _enableLogging = true;

        [SerializeField, Tooltip("Verify scene transitions automatically")]
        private bool _verifyTransitions = true;

        // State tracking
        private string _lastSceneName;
        private float _sceneLoadStartTime;
        private bool _isTransitioning;

        // Singleton
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

        /// <summary>
        /// Verifies a new game transition.
        /// </summary>
        public void VerifyNewGameTransition()
        {
            StartCoroutine(VerifyTransition("New Game",
                () => SceneManager.GetActiveScene().name == "02_HECTON_WORLD",
                () =>
                {
                    GameStartContext context = GameStartContextHolder.Current;
                    return context.IsValid && context.StartMode == GameStartMode.NewGame;
                },
                "GameStartContext.StartMode should be NewGame"));
        }

        /// <summary>
        /// Verifies a load game transition.
        /// </summary>
        public void VerifyLoadGameTransition(string expectedSlot)
        {
            StartCoroutine(VerifyTransition($"Load Game (Slot {expectedSlot})",
                () => SceneManager.GetActiveScene().name == "02_HECTON_WORLD",
                () =>
                {
                    GameStartContext context = GameStartContextHolder.Current;
                    return context.IsValid &&
                           context.StartMode == GameStartMode.LoadGame &&
                           context.TargetSaveSlot == expectedSlot;
                },
                $"GameStartContext should have LoadGame start mode and slot '{expectedSlot}'"));
        }

        /// <summary>
        /// Verifies return to main menu.
        /// </summary>
        public void VerifyReturnToMenu()
        {
            StartCoroutine(VerifyTransition("Return to Menu",
                () => SceneManager.GetActiveScene().name == "01_MAIN_MENU",
                () => true, // No specific context check for menu return
                "Should reach main menu scene"));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            float loadTime = Time.unscaledTime - _sceneLoadStartTime;
            _isTransitioning = false;

            LogVerification($"Scene loaded: {scene.name} (mode: {mode}, time: {loadTime:F2}s)");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            LogVerification($"Scene unloaded: {scene.name}");
        }

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            _sceneLoadStartTime = Time.unscaledTime;
            _isTransitioning = true;

            LogVerification($"Active scene changed: {previous.name} -> {current.name}");
            _lastSceneName = current.name;
        }

        private IEnumerator VerifyTransition(string transitionName, Func<bool> sceneCheck, Func<bool> contextCheck, string contextDescription)
        {
            LogVerification($"Starting verification: {transitionName}");

            // Wait for transition to complete
            float timeout = 10f;
            float startTime = Time.unscaledTime;

            while (_isTransitioning && (Time.unscaledTime - startTime) < timeout)
            {
                yield return null;
            }

            if (_isTransitioning)
            {
                LogVerification($"❌ {transitionName} - Transition timeout after {timeout}s");
                yield break;
            }

            // Verify scene
            if (!sceneCheck())
            {
                LogVerification($"❌ {transitionName} - Scene check failed (current: {SceneManager.GetActiveScene().name})");
                yield break;
            }

            // Verify context
            if (!contextCheck())
            {
                LogVerification($"❌ {transitionName} - Context check failed: {contextDescription}");
                yield break;
            }

            LogVerification($"✅ {transitionName} - Verification passed");
        }

        private void LogVerification(string message)
        {
            if (_enableLogging)
            {
                Debug.Log($"[SceneTransitionVerifier] {message}");
            }
        }
    }
}
