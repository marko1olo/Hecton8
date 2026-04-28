using System;
using Hecton8.Audio;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Standardized loading screen system that provides consistent loading feel across all scene transitions.
    /// Prevents broken bootstrap appearance by maintaining visual continuity during async operations.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LoadingScreenController : MonoBehaviour, ITickable, IUpdatable
    {
        private enum VisibilityState
        {
            Hidden,
            FadingIn,
            Visible,
            DelayBeforeHide,
            FadingOut
        }

        private static readonly string[] PercentStrings =
        {
            "0%", "1%", "2%", "3%", "4%", "5%", "6%", "7%", "8%", "9%",
            "10%", "11%", "12%", "13%", "14%", "15%", "16%", "17%", "18%", "19%",
            "20%", "21%", "22%", "23%", "24%", "25%", "26%", "27%", "28%", "29%",
            "30%", "31%", "32%", "33%", "34%", "35%", "36%", "37%", "38%", "39%",
            "40%", "41%", "42%", "43%", "44%", "45%", "46%", "47%", "48%", "49%",
            "50%", "51%", "52%", "53%", "54%", "55%", "56%", "57%", "58%", "59%",
            "60%", "61%", "62%", "63%", "64%", "65%", "66%", "67%", "68%", "69%",
            "70%", "71%", "72%", "73%", "74%", "75%", "76%", "77%", "78%", "79%",
            "80%", "81%", "82%", "83%", "84%", "85%", "86%", "87%", "88%", "89%",
            "90%", "91%", "92%", "93%", "94%", "95%", "96%", "97%", "98%", "99%",
            "100%"
        };

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
        private string[] _loadingTips =
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

        private bool _isShowing;
        private bool _registeredToTickManager;
        private float _showStartTime;
        private float _transitionElapsed;
        private float _fadeStartAlpha;
        private float _delayRemaining;
        private float _lastUnscaledTickTime;
        private VisibilityState _visibilityState;
        private string _currentProgressText = "0%";
        private string _currentStatusText = "Loading...";
        private string _currentTipText = "Loading...";

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

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isShowing = false;
            _visibilityState = VisibilityState.Hidden;

            if (_loadingPanel == null)
            {
                Debug.LogError("[LoadingScreenController] Loading panel not assigned!");
                enabled = false;
                return;
            }

            if (_progressBar != null)
            {
                _progressBar.value = 0f;
                _progressBar.interactable = false;
            }

            UpdateProgress(0f);
            UpdateStatus("Loading...");
            UpdateTip(GetRandomTip());
        }

        private void OnEnable()
        {
            TryRegisterToTickManager();
            _lastUnscaledTickTime = Time.unscaledTime;
        }

        private void Start()
        {
            TryRegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            _lastUnscaledTickTime = 0f;
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
        }

        /// <summary>
        /// Shows the loading screen with fade animation.
        /// </summary>
        public void Show()
        {
            if (_isShowing &&
                (_visibilityState == VisibilityState.FadingIn ||
                 _visibilityState == VisibilityState.Visible ||
                 _visibilityState == VisibilityState.DelayBeforeHide))
            {
                return;
            }

            _isShowing = true;
            _showStartTime = Time.unscaledTime;
            _delayRemaining = 0f;
            _transitionElapsed = 0f;
            _fadeStartAlpha = _canvasGroup.alpha;
            _visibilityState = VisibilityState.FadingIn;
            _canvasGroup.blocksRaycasts = true;
            TryRegisterToTickManager();

            if (SpatialAudioManager.Instance != null)
            {
                // Loading audio hook stays cold-path only.
            }
        }

        /// <summary>
        /// Hides the loading screen with fade animation.
        /// </summary>
        public void Hide()
        {
            if (!_isShowing)
                return;

            float elapsed = Time.unscaledTime - _showStartTime;
            if (elapsed < _minimumDisplayTime)
            {
                _delayRemaining = _minimumDisplayTime - elapsed;
                _visibilityState = VisibilityState.DelayBeforeHide;
                return;
            }

            BeginFadeOut();
        }

        /// <summary>
        /// Updates loading progress (0-1 range).
        /// </summary>
        public void UpdateProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (_progressBar != null)
                _progressBar.value = progress;

            if (_progressText == null)
                return;

            int percent = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            string nextText = PercentStrings[percent];
            if (string.Equals(_currentProgressText, nextText, StringComparison.Ordinal))
                return;

            _currentProgressText = nextText;
            _progressText.SetText(nextText);
        }

        /// <summary>
        /// Updates the loading status message.
        /// </summary>
        public void UpdateStatus(string status)
        {
            if (_statusText == null || string.Equals(_currentStatusText, status, StringComparison.Ordinal))
                return;

            _currentStatusText = status;
            _statusText.SetText(status);
        }

        /// <summary>
        /// Updates the loading tip.
        /// </summary>
        public void UpdateTip(string tip)
        {
            if (_tipText == null || string.Equals(_currentTipText, tip, StringComparison.Ordinal))
                return;

            _currentTipText = tip;
            _tipText.SetText(tip);
        }

        /// <summary>
        /// Sets a random loading tip.
        /// </summary>
        public void SetRandomTip()
        {
            UpdateTip(GetRandomTip());
        }

        public void Tick(float deltaTime)
        {
            float unscaledDeltaTime = GetUnscaledDeltaTime();
            if (unscaledDeltaTime <= 0f)
                return;

            switch (_visibilityState)
            {
                case VisibilityState.FadingIn:
                    UpdateFadeIn(unscaledDeltaTime);
                    break;

                case VisibilityState.DelayBeforeHide:
                    _delayRemaining -= unscaledDeltaTime;
                    if (_delayRemaining <= 0f)
                        BeginFadeOut();
                    break;

                case VisibilityState.FadingOut:
                    UpdateFadeOut(unscaledDeltaTime);
                    break;
            }
        }

        private string GetRandomTip()
        {
            if (_loadingTips == null || _loadingTips.Length == 0)
                return "Loading...";

            return _loadingTips[UnityEngine.Random.Range(0, _loadingTips.Length)];
        }

        private void UpdateFadeIn(float unscaledDeltaTime)
        {
            float duration = Mathf.Max(0.0001f, _fadeDuration);
            _transitionElapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionElapsed / duration);
            _canvasGroup.alpha = Mathf.Lerp(_fadeStartAlpha, 1f, t);

            if (t >= 1f)
                _visibilityState = VisibilityState.Visible;
        }

        private void BeginFadeOut()
        {
            _transitionElapsed = 0f;
            _fadeStartAlpha = _canvasGroup.alpha;
            _visibilityState = VisibilityState.FadingOut;
        }

        private void UpdateFadeOut(float unscaledDeltaTime)
        {
            float duration = Mathf.Max(0.0001f, _fadeDuration);
            _transitionElapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionElapsed / duration);
            _canvasGroup.alpha = Mathf.Lerp(_fadeStartAlpha, 0f, t);

            if (t < 1f)
                return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isShowing = false;
            _visibilityState = VisibilityState.Hidden;
        }

        private float GetUnscaledDeltaTime()
        {
            float currentTime = Time.unscaledTime;
            if (_lastUnscaledTickTime <= 0f)
            {
                _lastUnscaledTickTime = currentTime;
                return 0f;
            }

            float delta = currentTime - _lastUnscaledTickTime;
            _lastUnscaledTickTime = currentTime;
            return delta > 0f ? delta : 0f;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
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
