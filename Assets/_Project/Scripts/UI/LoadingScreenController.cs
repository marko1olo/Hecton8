using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hecton8.Audio;

namespace Hecton8.UI
{
    /// <summary>
    /// Standardized loading screen system that provides consistent loading feel across all scene transitions.
    /// Prevents broken bootstrap appearance by maintaining visual continuity during async operations.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LoadingScreenController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("Main loading panel CanvasGroup")]
        private CanvasGroup _loadingPanel;

        [SerializeField, Tooltip("Progress bar slider (0-1 range)")]
        private Slider _progressBar;

        [SerializeField, Tooltip("Progress percentage text")]
        private TMP_Text _progressText;

        [SerializeField, Tooltip("Loading status message")]
        private TMP_Text _statusText;

        [SerializeField, Tooltip("Loading tip text")]
        private TMP_Text _tipText;

        [Header("Animation Settings")]
        [SerializeField, Tooltip("Fade in/out duration in seconds")]
        private float _fadeDuration = 0.5f;

        [SerializeField, Tooltip("Minimum display time to prevent flicker")]
        private float _minimumDisplayTime = 1.0f;

        [Header("Loading Tips")]
        [SerializeField, Tooltip("Random tips to show during loading")]
        private string[] _loadingTips = new string[]
        {
            "Deep sea exploration requires patience...",
            "The ocean holds many secrets...",
            "Check your oxygen levels regularly...",
            "Some ruins are better left unexplored...",
            "The abyss stares back...",
            "Pressure increases with depth...",
            "Bioluminescent life guides the way...",
            "Ancient technology powers the depths..."
        };

        // State
        private bool _isShowing;
        private float _showStartTime;
        private Coroutine _currentFadeCoroutine;

        // Cached components
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                Debug.LogError("[LoadingScreenController] Missing CanvasGroup component!");
                enabled = false;
                return;
            }

            // Start hidden
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isShowing = false;

            // Validate references
            if (_loadingPanel == null)
            {
                Debug.LogError("[LoadingScreenController] Loading panel not assigned!");
                enabled = false;
                return;
            }

            // Set initial UI state
            if (_progressBar != null)
            {
                _progressBar.value = 0f;
                _progressBar.interactable = false;
            }

            if (_progressText != null)
                _progressText.text = "0%";

            if (_statusText != null)
                _statusText.text = "Loading...";

            if (_tipText != null)
                _tipText.text = GetRandomTip();
        }

        /// <summary>
        /// Shows the loading screen with fade animation.
        /// </summary>
        public void Show()
        {
            if (_isShowing) return;

            _isShowing = true;
            _showStartTime = Time.unscaledTime;

            if (_currentFadeCoroutine != null)
                StopCoroutine(_currentFadeCoroutine);

            _currentFadeCoroutine = StartCoroutine(FadeIn());

            // Play loading audio if available
            if (SpatialAudioManager.Instance != null)
            {
                // TODO: Add loading music/sting when audio system is ready
            }
        }

        /// <summary>
        /// Hides the loading screen with fade animation.
        /// </summary>
        public void Hide()
        {
            if (!_isShowing) return;

            // Enforce minimum display time
            float elapsed = Time.unscaledTime - _showStartTime;
            if (elapsed < _minimumDisplayTime)
            {
                StartCoroutine(DelayedHide(_minimumDisplayTime - elapsed));
                return;
            }

            if (_currentFadeCoroutine != null)
                StopCoroutine(_currentFadeCoroutine);

            _currentFadeCoroutine = StartCoroutine(FadeOut());
        }

        /// <summary>
        /// Updates loading progress (0-1 range).
        /// </summary>
        public void UpdateProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (_progressBar != null)
                _progressBar.value = progress;

            if (_progressText != null)
                _progressText.text = $"{(int)(progress * 100)}%";
        }

        /// <summary>
        /// Updates the loading status message.
        /// </summary>
        public void UpdateStatus(string status)
        {
            if (_statusText != null)
                _statusText.text = status;
        }

        /// <summary>
        /// Updates the loading tip.
        /// </summary>
        public void UpdateTip(string tip)
        {
            if (_tipText != null)
                _tipText.text = tip;
        }

        /// <summary>
        /// Sets a random loading tip.
        /// </summary>
        public void SetRandomTip()
        {
            UpdateTip(GetRandomTip());
        }

        private string GetRandomTip()
        {
            if (_loadingTips == null || _loadingTips.Length == 0)
                return "Loading...";

            return _loadingTips[UnityEngine.Random.Range(0, _loadingTips.Length)];
        }

        private IEnumerator FadeIn()
        {
            _canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _fadeDuration;
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _currentFadeCoroutine = null;
        }

        private IEnumerator FadeOut()
        {
            float startAlpha = _canvasGroup.alpha;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _fadeDuration;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isShowing = false;
            _currentFadeCoroutine = null;
        }

        private IEnumerator DelayedHide(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Hide();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (_loadingPanel == null)
                _loadingPanel = GetComponent<CanvasGroup>();
        }
        #endif
    }
}