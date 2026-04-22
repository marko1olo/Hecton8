using System;
using System.Collections.Generic;
using System.Globalization;
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

        private struct TimedSubtitleCue
        {
            public float StartTime;
            public float SpeakerIntensity;
            public string Text;
        }

        private static readonly Color BackdropColor = new Color(0.01f, 0.04f, 0.06f, 0.64f);
        private static readonly Color TextColor = new Color(0.86f, 0.96f, 1f, 0.96f);
        private static readonly Color WaveformColor = new Color(0.72f, 0.97f, 1f, 0.92f);

        public static SubtitleManager Instance { get; private set; }

        [Header("── Settings ────────────────────────────────────────────────")]
        [SerializeField, Range(1.5f, 8f)] private float defaultDuration = 3.25f;
        [SerializeField, Range(1f, 12f)] private float fadeSpeed = 5f;
        [SerializeField, Range(1, 10)] private int maxQueuedSubtitles = 6;
        [SerializeField, Range(0.1f, 2f)] private float repeatSuppressWindow = 0.4f;
        [SerializeField] private TMP_FontAsset font;

        private readonly List<SubtitleRequest> _queue = new List<SubtitleRequest>(8); // COLD ALLOC: List[8] — queued subtitle requests — owner: SubtitleManager
        private readonly List<TimedSubtitleCue> _timedAudioLogCues = new List<TimedSubtitleCue>(16); // COLD ALLOC: List[16] — parsed audio-log subtitle cues — owner: SubtitleManager

        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private CanvasGroup _audioCueGroup;
        private Image _backdrop;
        private TextMeshProUGUI _subtitleText;
        private AudioWaveformAnimator _audioWaveformAnimator;
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
        private bool _timedAudioLogActive;
        private float _timedAudioLogElapsed;
        private float _timedAudioLogTotalDuration;
        private int _timedAudioLogNextCueIndex;
        private string _timedAudioLogTitleLine;
        private string _timedAudioLogCurrentBody;
        private string _currentAudioLogId;
        private int _lastStressCorruptionBucket = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        /// <summary>
        /// Raised when an audio-log subtitle cue changes. Args: cue duration, cue text, speaker intensity [0..1].
        /// </summary>
        public event Action<float, string, float> OnCueChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (Instance != null)
                return;

            SuitHUDV4CanvasOverlay overlay = UnityEngine.Object.FindAnyObjectByType<SuitHUDV4CanvasOverlay>();
            Canvas targetCanvas = overlay != null
                ? overlay.TargetCanvas
                : UnityEngine.Object.FindAnyObjectByType<Canvas>();

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
            font = LocalizedFontResolver.ResolveReadableFont(font);

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
                ? manager.GetExpandedOrFallback(manager.CurrentLanguage, key, key)
                : key;

            Enqueue(resolved, duration, SubtitleSource.Generic, false);
        }

        public void Tick(float deltaTime)
        {
            if (_root == null)
                return;

            if (_timedAudioLogActive && _currentSource == SubtitleSource.AudioLog)
                AdvanceTimedAudioLog(deltaTime);

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

            if (_isShowing)
                RefreshStressCorruptionIfNeeded();

            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;

            if (_audioCueGroup != null)
                _audioCueGroup.alpha = _currentSource == SubtitleSource.AudioLog ? _currentAlpha : 0f;
        }

        private void HandleNotificationPushed(string message, int severity)
        {
            Enqueue(message, defaultDuration, SubtitleSource.Notification, false);
        }

        private void HandleAudioLogPlaybackStarted(AudioLogData data)
        {
            if (data == null || !data.HasSubtitleText)
                return;

            ClearTimedAudioLogState();
            _currentAudioLogId = data.SafeLogId;

            float duration = data.Duration > 0.01f
                ? Mathf.Clamp(data.Duration, 1.5f, 30f)
                : defaultDuration;

            if (TryPrepareTimedAudioLog(data, out string timedMessage))
            {
                Enqueue(timedMessage, duration, SubtitleSource.AudioLog, true);
                return;
            }

            NotifyCueChanged(duration, data.VisibleSubtitleOrFallback, 1f);
            Enqueue(BuildAudioLogSubtitle(data), duration, SubtitleSource.AudioLog, true);
        }

        private void HandleAudioLogPlaybackEnded(string logId)
        {
            ClearTimedAudioLogState();
            _currentAudioLogId = string.Empty;

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
            _lastStressCorruptionBucket = int.MinValue;

            string displayMessage = ResolveDisplayMessage(source, message);
            if (_subtitleText != null && !string.Equals(_subtitleText.text, displayMessage, System.StringComparison.Ordinal))
                _subtitleText.text = displayMessage;

            if (_audioCueGroup != null)
                _audioCueGroup.alpha = source == SubtitleSource.AudioLog ? _currentAlpha : 0f;
        }

        private static string BuildAudioLogSubtitle(AudioLogData data)
        {
            if (data == null)
                return string.Empty;

            string subtitleBody = data.VisibleSubtitleOrFallback;
            if (string.IsNullOrWhiteSpace(subtitleBody))
                return string.Empty;

            string displayTitle = data.DisplayTitleOrFallback;
            if (string.IsNullOrWhiteSpace(displayTitle))
                return subtitleBody;

            LocalizationManager manager = LocalizationManager.Instance;
            string titleLine = manager != null
                ? manager.GetFormatted(LocalizationKeys.AUDIOLOG_PLAYING, displayTitle)
                : "PLAYING: " + displayTitle;

            return string.Concat(titleLine, "\n", subtitleBody);
        }

        private bool TryPrepareTimedAudioLog(AudioLogData data, out string initialMessage)
        {
            _timedAudioLogCues.Clear();
            _timedAudioLogElapsed = 0f;
            _timedAudioLogTotalDuration = data != null ? Mathf.Max(0.5f, data.Duration) : 0f;
            _timedAudioLogNextCueIndex = 0;
            _timedAudioLogCurrentBody = string.Empty;

            string subtitleBody = data != null ? data.SubtitleOrFallback : string.Empty;
            if (!TryParseTimedSubtitleCues(subtitleBody, _timedAudioLogCues))
            {
                initialMessage = string.Empty;
                return false;
            }

            string displayTitle = data.DisplayTitleOrFallback;
            if (string.IsNullOrWhiteSpace(displayTitle))
            {
                _timedAudioLogTitleLine = string.Empty;
            }
            else
            {
                LocalizationManager manager = LocalizationManager.Instance;
                _timedAudioLogTitleLine = manager != null
                    ? manager.GetFormatted(LocalizationKeys.AUDIOLOG_PLAYING, displayTitle)
                    : "PLAYING: " + displayTitle;
            }

            _timedAudioLogActive = true;
            while (_timedAudioLogNextCueIndex < _timedAudioLogCues.Count &&
                   _timedAudioLogCues[_timedAudioLogNextCueIndex].StartTime <= 0f)
            {
                int cueIndex = _timedAudioLogNextCueIndex;
                _timedAudioLogCurrentBody = _timedAudioLogCues[_timedAudioLogNextCueIndex].Text;
                _timedAudioLogNextCueIndex++;
                NotifyCueChanged(GetCueDuration(cueIndex), _timedAudioLogCurrentBody, _timedAudioLogCues[cueIndex].SpeakerIntensity);
            }

            initialMessage = BuildTimedAudioLogFrame();
            return true;
        }

        private void AdvanceTimedAudioLog(float deltaTime)
        {
            _timedAudioLogElapsed += deltaTime;
            bool changed = false;
            int lastCueIndex = -1;

            while (_timedAudioLogNextCueIndex < _timedAudioLogCues.Count &&
                   _timedAudioLogElapsed >= _timedAudioLogCues[_timedAudioLogNextCueIndex].StartTime)
            {
                lastCueIndex = _timedAudioLogNextCueIndex;
                _timedAudioLogCurrentBody = _timedAudioLogCues[_timedAudioLogNextCueIndex].Text;
                _timedAudioLogNextCueIndex++;
                changed = true;
            }

            if (!changed)
                return;

            if (lastCueIndex >= 0)
                NotifyCueChanged(GetCueDuration(lastCueIndex), _timedAudioLogCurrentBody, _timedAudioLogCues[lastCueIndex].SpeakerIntensity);

            string frameMessage = BuildTimedAudioLogFrame();
            _currentMessage = frameMessage;
            string displayMessage = ResolveDisplayMessage(SubtitleSource.AudioLog, frameMessage);
            if (_subtitleText != null && !string.Equals(_subtitleText.text, displayMessage, System.StringComparison.Ordinal))
                _subtitleText.text = displayMessage;
        }

        private string BuildTimedAudioLogFrame()
        {
            if (string.IsNullOrWhiteSpace(_timedAudioLogCurrentBody))
                return _timedAudioLogTitleLine ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_timedAudioLogTitleLine))
                return _timedAudioLogCurrentBody;

            return string.Concat(_timedAudioLogTitleLine, "\n", _timedAudioLogCurrentBody);
        }

        private void ClearTimedAudioLogState()
        {
            _timedAudioLogActive = false;
            _timedAudioLogElapsed = 0f;
            _timedAudioLogTotalDuration = 0f;
            _timedAudioLogNextCueIndex = 0;
            _timedAudioLogTitleLine = string.Empty;
            _timedAudioLogCurrentBody = string.Empty;
            _timedAudioLogCues.Clear();
            _lastStressCorruptionBucket = int.MinValue;
            NotifyCueChanged(0f, string.Empty, 0f);
        }

        private float GetCueDuration(int cueIndex)
        {
            if ((uint)cueIndex >= (uint)_timedAudioLogCues.Count)
                return 0f;

            float currentStart = Mathf.Max(0f, _timedAudioLogCues[cueIndex].StartTime);
            float nextStart = cueIndex + 1 < _timedAudioLogCues.Count
                ? Mathf.Max(currentStart, _timedAudioLogCues[cueIndex + 1].StartTime)
                : Mathf.Max(currentStart, _timedAudioLogTotalDuration);

            float duration = nextStart - currentStart;
            return duration > 0.05f ? duration : 0.05f;
        }

        private void NotifyCueChanged(float duration, string text, float speakerIntensity)
        {
            OnCueChanged?.Invoke(Mathf.Max(0f, duration), text ?? string.Empty, Mathf.Clamp01(speakerIntensity));
        }

        private void RefreshStressCorruptionIfNeeded()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            string displayMessage = ResolveDisplayMessage(_currentSource, _currentMessage);
            if (_subtitleText != null && !string.Equals(_subtitleText.text, displayMessage, System.StringComparison.Ordinal))
                _subtitleText.text = displayMessage;
        }

        private string ResolveDisplayMessage(SubtitleSource source, string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null)
                return message;

            if (source == SubtitleSource.AudioLog)
            {
                string sourceToken = string.IsNullOrWhiteSpace(_currentAudioLogId)
                    ? "audio_log"
                    : _currentAudioLogId;
                return manager.ApplyPdaLoreCorruptionIfNeeded(sourceToken, message);
            }

            return manager.ApplyHullStressCorruptionIfNeeded(message);
        }

        private static bool TryParseTimedSubtitleCues(string subtitle, List<TimedSubtitleCue> target)
        {
            if (target == null)
                return false;

            target.Clear();
            if (string.IsNullOrEmpty(subtitle) || subtitle.IndexOf('[', System.StringComparison.Ordinal) < 0)
                return false;

            int cursor = 0;
            while (cursor < subtitle.Length)
            {
                if (subtitle[cursor] != '[')
                {
                    cursor++;
                    continue;
                }

                int markerStart = cursor;
                int markerEnd = subtitle.IndexOf(']', markerStart + 1);
                if (markerEnd <= markerStart + 1)
                {
                    cursor++;
                    continue;
                }

                string markerBody = subtitle.Substring(markerStart + 1, markerEnd - markerStart - 1);
                float speakerIntensity = 1f;
                string startToken = markerBody;
                int separatorIndex = markerBody.IndexOf('|');
                if (separatorIndex < 0)
                    separatorIndex = markerBody.IndexOf(',');

                if (separatorIndex > 0)
                {
                    startToken = markerBody.Substring(0, separatorIndex).Trim();
                    string intensityToken = markerBody.Substring(separatorIndex + 1).Trim();
                    if (float.TryParse(intensityToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedIntensity))
                        speakerIntensity = Mathf.Clamp01(parsedIntensity);
                }

                if (!float.TryParse(startToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float startTime))
                {
                    cursor++;
                    continue;
                }

                int textStart = markerEnd + 1;
                int nextMarker = subtitle.IndexOf('[', textStart);
                int textEnd = nextMarker >= 0 ? nextMarker : subtitle.Length;
                string cueText = subtitle.Substring(textStart, textEnd - textStart).Trim();
                if (!string.IsNullOrWhiteSpace(cueText))
                {
                    target.Add(new TimedSubtitleCue
                    {
                        StartTime = Mathf.Max(0f, startTime),
                        SpeakerIntensity = speakerIntensity,
                        Text = cueText
                    });
                }

                cursor = textEnd;
            }

            return target.Count > 0;
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
            textRect.offsetMin = new Vector2(78f, 10f);
            textRect.offsetMax = new Vector2(-20f, -10f);

            _subtitleText = textOwner.AddComponent<TextMeshProUGUI>();
            _subtitleText.font = font;
            _subtitleText.fontSize = 22f;
            _subtitleText.fontStyle = FontStyles.Bold;
            _subtitleText.alignment = TextAlignmentOptions.BottomGeoAligned;
            _subtitleText.textWrappingMode = TextWrappingModes.Normal;
            _subtitleText.raycastTarget = false;
            _subtitleText.color = TextColor;
            LocalizedTMPAutoSizer.Configure(
                _subtitleText,
                16f,
                _subtitleText.fontSize,
                TextOverflowModes.Ellipsis,
                TextWrappingModes.Normal);

            GameObject waveformOwner = new GameObject(
                "AudioWaveformIcon",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(AudioWaveformAnimator));
            waveformOwner.layer = gameObject.layer;

            RectTransform waveformRoot = waveformOwner.GetComponent<RectTransform>();
            waveformRoot.SetParent(_root, false);
            waveformRoot.anchorMin = new Vector2(0f, 0.5f);
            waveformRoot.anchorMax = new Vector2(0f, 0.5f);
            waveformRoot.pivot = new Vector2(0f, 0.5f);
            waveformRoot.anchoredPosition = new Vector2(18f, 0f);
            waveformRoot.sizeDelta = new Vector2(42f, 34f);

            _audioCueGroup = waveformOwner.GetComponent<CanvasGroup>();
            _audioCueGroup.alpha = 0f;
            _audioCueGroup.interactable = false;
            _audioCueGroup.blocksRaycasts = false;

            // COLD ALLOC: RectTransform[4] — runtime subtitle waveform bars bound to AudioWaveformAnimator — owner: SubtitleManager
            RectTransform[] waveformBars = new RectTransform[4];
            for (int i = 0; i < waveformBars.Length; i++)
            {
                GameObject barObject = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
                barObject.layer = gameObject.layer;
                RectTransform barRect = barObject.GetComponent<RectTransform>();
                barRect.SetParent(waveformRoot, false);
                barRect.anchorMin = new Vector2(0f, 0.5f);
                barRect.anchorMax = new Vector2(0f, 0.5f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.sizeDelta = new Vector2(5f, 18f);
                barRect.anchoredPosition = new Vector2(5f + i * 9f, 0f);

                Image barImage = barObject.GetComponent<Image>();
                barImage.color = WaveformColor;
                barImage.raycastTarget = false;
                waveformBars[i] = barRect;
            }

            _audioWaveformAnimator = waveformOwner.GetComponent<AudioWaveformAnimator>();
            _audioWaveformAnimator.ConfigureWaveformTargets(waveformBars);

            _built = true;
        }
    }
}
