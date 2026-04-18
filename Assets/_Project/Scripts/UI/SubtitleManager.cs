using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Lower-screen subtitle owner for localized notifications and spoken log playback.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Subtitle Manager")]
    public sealed class SubtitleManager : MonoBehaviour, ITickable
    {
        private enum SubtitleSource
        {
            Generic = 0,
            Notification = 1,
            AudioLog = 2
        }

        private struct SubtitleRequest
        {
            public string Message;
            public float Duration;
            public SubtitleSource Source;
        }

        private static readonly Color BackdropColor = new Color(0.01f, 0.04f, 0.06f, 0.64f);
        private static readonly Color TextColor = new Color(0.86f, 0.96f, 1f, 0.96f);

        public static SubtitleManager Instance { get; private set; }

        [Header("── Settings ────────────────────────────────────────────────")]
        [SerializeField, Range(1.5f, 8f)] private float defaultDuration = 3.25f;
        [SerializeField, Range(1f, 12f)] private float fadeSpeed = 5f;
        [SerializeField, Range(1, 10)] private int maxQueuedSubtitles = 6;
        [SerializeField, Range(0.1f, 2f)] private float repeatSuppressWindow = 0.4f;
        [SerializeField] private TMP_FontAsset font;

        private readonly List<SubtitleRequest> _queue = new List<SubtitleRequest>(8); // COLD ALLOC: List[8] — queued subtitle requests — owner: SubtitleManager

        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private Image _backdrop;
        private TextMeshProUGUI _subtitleText;
        private float _timer;
        private float _currentAlpha;
        private bool _built;
        private bool _isShowing;
        private bool _registeredToTickManager;
        private string _currentMessage;
        private SubtitleSource _currentSource;
        private string _lastEnqueuedMessage;
        private SubtitleSource _lastEnqueuedSource;
        private float _lastEnqueueTime = -999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (Instance != null)
                return;

            SuitHUDV4CanvasOverlay overlay = Object.FindAnyObjectByType<SuitHUDV4CanvasOverlay>();
            Canvas targetCanvas = overlay != null
                ? overlay.TargetCanvas
                : Object.FindAnyObjectByType<Canvas>();

            if (targetCanvas == null)
                return;

            GameObject owner = new GameObject("SubtitleManager", typeof(RectTransform));
            owner.layer = targetCanvas.gameObject.layer;

            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.SetParent(targetCanvas.transform, false);

            owner.AddComponent<SubtitleManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (font == null)
                font = TMP_Settings.defaultFontAsset;

            NotificationEvents.OnPushNotification += HandleNotificationPushed;
            AudioLogEvents.OnLogPlaybackStarted += HandleAudioLogPlaybackStarted;
            AudioLogEvents.OnLogPlaybackStopped += HandleAudioLogPlaybackEnded;
            AudioLogEvents.OnLogPlaybackCompleted += HandleAudioLogPlaybackEnded;

            EnsureBuilt();
        }

        private void OnDisable()
        {
            NotificationEvents.OnPushNotification -= HandleNotificationPushed;
            AudioLogEvents.OnLogPlaybackStarted -= HandleAudioLogPlaybackStarted;
            AudioLogEvents.OnLogPlaybackStopped -= HandleAudioLogPlaybackEnded;
            AudioLogEvents.OnLogPlaybackCompleted -= HandleAudioLogPlaybackEnded;

            UnregisterFromTickManager();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Resolves a localization key and displays the subtitle for the requested duration.
        /// </summary>
        /// <param name="key">Localization table key.</param>
        /// <param name="duration">Display duration in seconds.</param>
        public void DisplaySubtitle(string key, float duration)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            string resolved = manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, key)
                : key;

            Enqueue(resolved, duration, SubtitleSource.Generic, false);
        }

        public void Tick(float deltaTime)
        {
            if (_root == null)
                return;

            if (_timer > 0f)
            {
                _timer -= deltaTime;
                _currentAlpha = Mathf.Lerp(_currentAlpha, 1f, 1f - Mathf.Exp(-fadeSpeed * deltaTime));
            }
            else
            {
                _currentAlpha = Mathf.Lerp(_currentAlpha, 0f, 1f - Mathf.Exp(-fadeSpeed * deltaTime));
                if (_currentAlpha < 0.01f)
                {
                    _currentAlpha = 0f;
                    _isShowing = false;
                    _currentMessage = string.Empty;
                    _currentSource = SubtitleSource.Generic;

                    if (_queue.Count > 0)
                    {
                        SubtitleRequest next = _queue[0];
                        _queue.RemoveAt(0);
                        ShowImmediate(next.Message, next.Duration, next.Source);
                    }
                    else
                    {
                        UnregisterFromTickManager();
                    }
                }
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;
        }

        private void HandleNotificationPushed(string message, int severity)
        {
            Enqueue(message, defaultDuration, SubtitleSource.Notification, false);
        }

        private void HandleAudioLogPlaybackStarted(AudioLogData data)
        {
            if (data == null || !data.HasSubtitleText)
                return;

            float duration = data.Duration > 0.01f
                ? Mathf.Clamp(data.Duration, 1.5f, 30f)
                : defaultDuration;

            Enqueue(data.SubtitleOrFallback, duration, SubtitleSource.AudioLog, true);
        }

        private void HandleAudioLogPlaybackEnded(string logId)
        {
            if (_currentSource == SubtitleSource.AudioLog)
                _timer = 0f;
        }

        private void Enqueue(string message, float duration, SubtitleSource source, bool interrupt)
        {
            EnsureBuilt();

            if (string.IsNullOrWhiteSpace(message))
                return;

            string normalized = message.Trim();
            float resolvedDuration = Mathf.Max(0.5f, duration);
            float now = Time.unscaledTime;

            if (normalized == _currentMessage && source == _currentSource && _timer > 0f)
            {
                _timer = resolvedDuration;
                return;
            }

            if (!interrupt &&
                normalized == _lastEnqueuedMessage &&
                source == _lastEnqueuedSource &&
                now - _lastEnqueueTime < repeatSuppressWindow)
            {
                return;
            }

            _lastEnqueuedMessage = normalized;
            _lastEnqueuedSource = source;
            _lastEnqueueTime = now;

            if (interrupt)
            {
                ShowImmediate(normalized, resolvedDuration, source);
                return;
            }

            if (_timer <= 0f && _queue.Count == 0 && !_isShowing && _currentAlpha <= 0.01f)
            {
                ShowImmediate(normalized, resolvedDuration, source);
                return;
            }

            if (_queue.Count >= Mathf.Max(1, maxQueuedSubtitles))
                _queue.RemoveAt(0);

            _queue.Add(new SubtitleRequest
            {
                Message = normalized,
                Duration = resolvedDuration,
                Source = source
            });
        }

        private void ShowImmediate(string message, float duration, SubtitleSource source)
        {
            RegisterToTickManager();

            _currentMessage = message;
            _currentSource = source;
            _timer = duration;
            _currentAlpha = 0f;
            _isShowing = true;

            if (_subtitleText != null && !string.Equals(_subtitleText.text, message, System.StringComparison.Ordinal))
                _subtitleText.text = message;
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registeredToTickManager = false;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            _root = transform as RectTransform;
            if (_root == null)
                return;

            _root.anchorMin = new Vector2(0.5f, 0f);
            _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.anchoredPosition = new Vector2(0f, 72f);
            _root.sizeDelta = new Vector2(940f, 92f);

            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _backdrop = gameObject.GetComponent<Image>();
            if (_backdrop == null)
                _backdrop = gameObject.AddComponent<Image>();
            _backdrop.color = BackdropColor;
            _backdrop.raycastTarget = false;

            GameObject textOwner = new GameObject("SubtitleText", typeof(RectTransform));
            textOwner.layer = gameObject.layer;
            RectTransform textRect = textOwner.GetComponent<RectTransform>();
            textRect.SetParent(_root, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 10f);
            textRect.offsetMax = new Vector2(-20f, -10f);

            _subtitleText = textOwner.AddComponent<TextMeshProUGUI>();
            _subtitleText.font = font;
            _subtitleText.fontSize = 22f;
            _subtitleText.fontStyle = FontStyles.Bold;
            _subtitleText.alignment = TextAlignmentOptions.BottomGeoAligned;
            _subtitleText.textWrappingMode = TextWrappingModes.Normal;
            _subtitleText.raycastTarget = false;
            _subtitleText.color = TextColor;

            _built = true;
        }
    }
}
