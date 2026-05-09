using System;
using System.Collections.Generic;
using System.Globalization;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Narrative;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-allocation subtitle playback bus for lore-backed voice and audio-log UI sync.
    /// </summary>
    public static class SubtitleEventBus
    {
        public enum PlaybackEventKind : byte
        {
            Started = 0,
            Stopped = 1,
            Completed = 2
        }

        public readonly struct PlaybackEvent
        {
            public PlaybackEvent(PlaybackEventKind kind, uint loreHash, float durationSeconds)
            {
                Kind = kind;
                LoreHash = loreHash;
                DurationSeconds = durationSeconds;
            }

            /// <summary>
            /// Playback transition type.
            /// </summary>
            public PlaybackEventKind Kind { get; }

            /// <summary>
            /// Stable FNV-1a lore hash emitted by the audio owner.
            /// </summary>
            public uint LoreHash { get; }

            /// <summary>
            /// Active playback duration. Only meaningful for start events.
            /// </summary>
            public float DurationSeconds { get; }
        }

        /// <summary>
        /// Publish a subtitle start event.
        /// </summary>
        public static void RaisePlaybackStarted(uint loreHash, float durationSeconds)
        {
            AudioLogEvents.RaisePlaybackStarted(loreHash, durationSeconds);
        }

        /// <summary>
        /// Publish a subtitle stop event.
        /// </summary>
        public static void RaisePlaybackStopped(uint loreHash)
        {
            AudioLogEvents.RaisePlaybackStopped(loreHash);
        }

        /// <summary>
        /// Publish a subtitle completion event.
        /// </summary>
        public static void RaisePlaybackCompleted(uint loreHash)
        {
            AudioLogEvents.RaisePlaybackCompleted(loreHash);
        }
    }

    /// <summary>
    /// Lower-screen subtitle owner for localized notifications and lore-backed spoken playback.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Subtitle Manager")]
    public sealed class SubtitleManager : MonoBehaviour, ITickable, INotificationEventListener, IAudioLogEventListener
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
            public int StartIndex;
            public int Length;
        }

        private ref struct SubtitleSpanBuilder
        {
            private Span<char> _destination;
            private int _length;

            public SubtitleSpanBuilder(Span<char> destination)
            {
                _destination = destination;
                _length = 0;
            }

            public int Length => _length;

            public void Append(char value)
            {
                if ((uint)_length >= (uint)_destination.Length)
                    return;

                _destination[_length++] = value;
            }

            public void Append(char[] source, int start, int length)
            {
                if (source == null || length <= 0 || _length >= _destination.Length)
                    return;

                int safeStart = Mathf.Clamp(start, 0, source.Length);
                int safeLength = Mathf.Clamp(
                    length,
                    0,
                    Mathf.Min(source.Length - safeStart, _destination.Length - _length));
                if (safeLength <= 0)
                    return;

                source.AsSpan(safeStart, safeLength).CopyTo(_destination.Slice(_length));
                _length += safeLength;
            }
        }

        private const int MaxQueuedSubtitles = 8;
        private const int MaxTimedAudioLogCueCount = 32;
        private const int MaxSubtitleRenderCharacters = 2048;

        private static readonly Color BackdropColor = new Color(0.01f, 0.04f, 0.06f, 0.64f);
        private static readonly Color TextColor = new Color(0.86f, 0.96f, 1f, 0.96f);
        private static readonly Color WaveformColor = new Color(0.72f, 0.97f, 1f, 0.92f);

        private readonly List<SubtitleRequest> _queue = new List<SubtitleRequest>(MaxQueuedSubtitles); // COLD ALLOC: List[8] - queued subtitle requests - owner: SubtitleManager
        private readonly TimedSubtitleCue[] _timedAudioLogCues = new TimedSubtitleCue[MaxTimedAudioLogCueCount]; // COLD ALLOC: TimedSubtitleCue[32] - parsed timed subtitle cue metadata - owner: SubtitleManager
        private readonly char[] _subtitleRenderBuffer = new char[MaxSubtitleRenderCharacters]; // COLD ALLOC: char[2048] - subtitle TMP render buffer - owner: SubtitleManager
        private readonly char[] _lastRenderedSubtitleBuffer = new char[MaxSubtitleRenderCharacters]; // COLD ALLOC: char[2048] - subtitle change cache - owner: SubtitleManager

        private static SubtitleManager s_activeInstance;

        [Header("Settings")]
        [SerializeField, Range(1.5f, 8f)] private float defaultDuration = 3.25f;
        [SerializeField, Range(1f, 12f)] private float fadeSpeed = 5f;
        [SerializeField, Range(1, 10)] private int maxQueuedSubtitles = 6;
        [SerializeField, Range(0.1f, 2f)] private float repeatSuppressWindow = 0.4f;
        [SerializeField] private TMP_FontAsset font;

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
        private bool _serviceRegistered;
        private string _currentMessage;
        private SubtitleSource _currentSource;
        private string _lastEnqueuedMessage;
        private SubtitleSource _lastEnqueuedSource;
        private float _lastEnqueueTime = -999f;
        private int _lastRenderedSubtitleLength = -1;
        private bool _timedAudioLogActive;
        private float _timedAudioLogElapsed;
        private float _timedAudioLogTotalDuration;
        private int _timedAudioLogCueCount;
        private int _timedAudioLogNextCueIndex;
        private int _timedAudioLogCurrentCueStartIndex;
        private int _timedAudioLogCurrentCueLength;
        private int _timedAudioLogCurrentRevealLength;
        private float _timedAudioLogCurrentCueDuration;
        private float _timedAudioLogCueRevealStartTime;
        private char[] _timedAudioLogTitleBuffer;
        private int _timedAudioLogTitleLength;
        private char[] _timedAudioLogBodyBuffer;
        private int _timedAudioLogBodyLength;
        private uint _currentAudioLogHash;
        private int _lastStressCorruptionBucket = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeInstance = null;
        }

        /// <summary>
        /// Raised when an audio-log cue changes. Args: cue duration, cue source buffer, cue start, cue length, speaker intensity [0..1].
        /// </summary>
        public event Action<float, char[], int, int, float> OnCueChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (s_activeInstance != null)
                return;

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            Canvas targetCanvas = overlay != null
                ? overlay.TargetCanvas
                : (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
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
            if (s_activeInstance != null && s_activeInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_activeInstance = this;
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (s_activeInstance == null)
                s_activeInstance = this;
            if (s_activeInstance != this)
                return;

            TryRegisterToGlobalRegistry();
            font = LocalizedFontResolver.ResolveReadableFont(font);
            NotificationEvents.Register(this);
            AudioLogEvents.Register(this);
            EnsureBuilt();
        }

        private void OnDisable()
        {
            NotificationEvents.Unregister(this);
            AudioLogEvents.Unregister(this);
            UnregisterFromTickManager();
            TryUnregisterFromGlobalRegistry();

            if (s_activeInstance == this)
                s_activeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterFromGlobalRegistry();

            if (s_activeInstance == this)
                s_activeInstance = null;
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying || s_activeInstance != this)
                return;

            GlobalRegistry.RegisterSubtitleRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Subtitles, this);
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSubtitleRuntime(this);
            _serviceRegistered = false;
        }

        /// <summary>
        /// Resolves a localization key and displays the subtitle for the requested duration.
        /// </summary>
        public void DisplaySubtitle(string key, float duration)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
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
                _currentAlpha = math.lerp(_currentAlpha, 1f, FastDecayBlend(fadeSpeed, deltaTime));
            }
            else
            {
                _currentAlpha = math.lerp(_currentAlpha, 0f, FastDecayBlend(fadeSpeed, deltaTime));
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
                        ApplySubtitleBuffer(0);
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

        public void OnNotificationEvent(in NotificationEventPayload payload)
        {
            if (!NotificationEvents.TryResolveMessage(payload.MessageHash, out string message))
                return;

            HandleNotificationPushed(message, payload.Severity);
        }

        private void HandleNotificationPushed(string message, ushort severity)
        {
            Enqueue(message, defaultDuration, SubtitleSource.Notification, false);
        }

        public void OnAudioLogEvent(in AudioLogEventPayload payload)
        {
            switch (payload.Type)
            {
                case AudioLogEventType.PlaybackStarted:
                    HandleAudioLogPlaybackStarted(payload.LogHash, payload.DurationSeconds);
                    return;

                case AudioLogEventType.PlaybackStopped:
                case AudioLogEventType.PlaybackCompleted:
                    HandleAudioLogPlaybackEnded(payload.LogHash);
                    return;
            }
        }

        private void HandleAudioLogPlaybackStarted(uint loreHash, float durationSeconds)
        {
            ClearTimedAudioLogState();
            if (!TryPrepareAudioLogBuffers(loreHash, durationSeconds, out int initialRenderLength))
                return;

            RegisterToTickManager();
            _currentSource = SubtitleSource.AudioLog;
            _currentMessage = string.Empty;
            _timer = durationSeconds > 0.01f
                ? Mathf.Clamp(durationSeconds, 1.5f, 30f)
                : defaultDuration;
            _currentAlpha = 0f;
            _isShowing = true;
            _lastStressCorruptionBucket = int.MinValue;
            ApplySubtitleBuffer(initialRenderLength);

            if (_audioCueGroup != null)
                _audioCueGroup.alpha = _currentAlpha;
        }

        private void HandleAudioLogPlaybackEnded(uint loreHash)
        {
            if (_currentAudioLogHash != 0u && loreHash != 0u && loreHash != _currentAudioLogHash)
                return;

            ClearTimedAudioLogState();
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

            if (source == SubtitleSource.AudioLog)
            {
                ApplySubtitleBuffer(BuildCurrentAudioLogFrame());
            }
            else
            {
                string displayMessage = ResolveDisplayMessage(source, message);
                CopyStringToRenderBuffer(displayMessage);
                ApplySubtitleBuffer(Mathf.Min(displayMessage != null ? displayMessage.Length : 0, _subtitleRenderBuffer.Length));
            }

            if (_audioCueGroup != null)
                _audioCueGroup.alpha = source == SubtitleSource.AudioLog ? _currentAlpha : 0f;
        }

        private void RefreshStressCorruptionIfNeeded()
        {
            if (_currentSource == SubtitleSource.AudioLog)
                return;

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            string displayMessage = ResolveDisplayMessage(_currentSource, _currentMessage);
            CopyStringToRenderBuffer(displayMessage);
            ApplySubtitleBuffer(Mathf.Min(displayMessage != null ? displayMessage.Length : 0, _subtitleRenderBuffer.Length));
        }

        private string ResolveDisplayMessage(SubtitleSource source, string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null)
                return message;

            return source == SubtitleSource.AudioLog
                ? message
                : manager.ApplyHullStressCorruptionIfNeeded(message);
        }

        private bool TryPrepareAudioLogBuffers(uint loreHash, float durationSeconds, out int initialRenderLength)
        {
            initialRenderLength = 0;
            LoreDatabaseManager database = Hecton8.Core.GlobalRegistry.LoreDatabase;
            if (database == null || loreHash == 0u)
                return false;

            _currentAudioLogHash = loreHash;
            _timedAudioLogElapsed = 0f;
            _timedAudioLogTotalDuration = durationSeconds > 0.01f
                ? Mathf.Max(0.5f, durationSeconds)
                : defaultDuration;
            _timedAudioLogCueCount = 0;
            _timedAudioLogNextCueIndex = 0;
            _timedAudioLogCurrentCueStartIndex = 0;
            _timedAudioLogCurrentCueLength = 0;
            _timedAudioLogCurrentRevealLength = 0;
            _timedAudioLogCurrentCueDuration = 0f;
            _timedAudioLogCueRevealStartTime = 0f;
            _timedAudioLogTitleBuffer = null;
            _timedAudioLogTitleLength = 0;
            _timedAudioLogBodyBuffer = null;
            _timedAudioLogBodyLength = 0;

            database.TryGetTitleBuffer(loreHash, out _timedAudioLogTitleBuffer, out _timedAudioLogTitleLength, out _);
            database.TryGetBodyBuffer(loreHash, out _timedAudioLogBodyBuffer, out _timedAudioLogBodyLength, out _);

            bool hasTitle = _timedAudioLogTitleBuffer != null && _timedAudioLogTitleLength > 0;
            bool hasBody = _timedAudioLogBodyBuffer != null && _timedAudioLogBodyLength > 0;
            if (!hasTitle && !hasBody)
                return false;

            _timedAudioLogActive = hasBody && TryParseTimedSubtitleCues(_timedAudioLogBodyBuffer, _timedAudioLogBodyLength);
            if (_timedAudioLogActive)
            {
                if (_timedAudioLogCueCount > 0 && _timedAudioLogCues[0].StartTime <= 0f)
                {
                    _timedAudioLogCurrentCueStartIndex = _timedAudioLogCues[0].StartIndex;
                    _timedAudioLogCurrentCueLength = _timedAudioLogCues[0].Length;
                    _timedAudioLogCurrentCueDuration = GetCueDuration(0);
                    _timedAudioLogCueRevealStartTime = Time.unscaledTime;
                    _timedAudioLogCurrentRevealLength = 0;
                    _timedAudioLogNextCueIndex = 1;
                    NotifyCueChanged(
                        _timedAudioLogCurrentCueDuration,
                        _timedAudioLogBodyBuffer,
                        _timedAudioLogCurrentCueStartIndex,
                        _timedAudioLogCurrentCueLength,
                        _timedAudioLogCues[0].SpeakerIntensity);
                }
                else
                {
                    NotifyCueChanged(0f, Array.Empty<char>(), 0, 0, 0f);
                }
            }
            else
            {
                _timedAudioLogCurrentCueStartIndex = 0;
                _timedAudioLogCurrentCueLength = hasBody ? _timedAudioLogBodyLength : 0;
                _timedAudioLogCurrentCueDuration = _timedAudioLogTotalDuration;
                _timedAudioLogCueRevealStartTime = Time.unscaledTime;
                _timedAudioLogCurrentRevealLength = 0;
                NotifyCueChanged(
                    _timedAudioLogTotalDuration,
                    _timedAudioLogBodyBuffer,
                    _timedAudioLogCurrentCueStartIndex,
                    _timedAudioLogCurrentCueLength,
                    1f);
            }

            initialRenderLength = BuildCurrentAudioLogFrame();
            return initialRenderLength > 0;
        }

        private void AdvanceTimedAudioLog(float deltaTime)
        {
            _timedAudioLogElapsed += deltaTime;
            bool changed = false;
            int lastCueIndex = -1;
            while (_timedAudioLogNextCueIndex < _timedAudioLogCueCount &&
                   _timedAudioLogElapsed >= _timedAudioLogCues[_timedAudioLogNextCueIndex].StartTime)
            {
                lastCueIndex = _timedAudioLogNextCueIndex;
                _timedAudioLogCurrentCueStartIndex = _timedAudioLogCues[lastCueIndex].StartIndex;
                _timedAudioLogCurrentCueLength = _timedAudioLogCues[lastCueIndex].Length;
                _timedAudioLogCurrentCueDuration = GetCueDuration(lastCueIndex);
                _timedAudioLogCueRevealStartTime = Time.unscaledTime;
                _timedAudioLogCurrentRevealLength = 0;
                _timedAudioLogNextCueIndex++;
                changed = true;
            }

            if (changed && lastCueIndex >= 0)
            {
                NotifyCueChanged(
                    _timedAudioLogCurrentCueDuration,
                    _timedAudioLogBodyBuffer,
                    _timedAudioLogCurrentCueStartIndex,
                    _timedAudioLogCurrentCueLength,
                    _timedAudioLogCues[lastCueIndex].SpeakerIntensity);
            }

            int revealLength = ResolveCurrentCueRevealLength();
            if (!changed && revealLength == _timedAudioLogCurrentRevealLength)
                return;

            _timedAudioLogCurrentRevealLength = revealLength;
            ApplySubtitleBuffer(BuildCurrentAudioLogFrame());
        }

        private int BuildCurrentAudioLogFrame()
        {
            bool hasTitle = _timedAudioLogTitleBuffer != null && _timedAudioLogTitleLength > 0;
            int bodyLength = Mathf.Min(_timedAudioLogCurrentCueLength, _timedAudioLogCurrentRevealLength);
            bool hasBody = _timedAudioLogBodyBuffer != null && bodyLength > 0;
            SubtitleSpanBuilder builder = new SubtitleSpanBuilder(_subtitleRenderBuffer);

            if (hasTitle)
                builder.Append(_timedAudioLogTitleBuffer, 0, _timedAudioLogTitleLength);

            if (hasTitle && hasBody)
                builder.Append('\n');

            if (hasBody)
            {
                builder.Append(
                    _timedAudioLogBodyBuffer,
                    _timedAudioLogCurrentCueStartIndex,
                    bodyLength);
            }

            return builder.Length;
        }

        private void ClearTimedAudioLogState()
        {
            _timedAudioLogActive = false;
            _timedAudioLogElapsed = 0f;
            _timedAudioLogTotalDuration = 0f;
            _timedAudioLogCueCount = 0;
            _timedAudioLogNextCueIndex = 0;
            _timedAudioLogCurrentCueStartIndex = 0;
            _timedAudioLogCurrentCueLength = 0;
            _timedAudioLogCurrentRevealLength = 0;
            _timedAudioLogCurrentCueDuration = 0f;
            _timedAudioLogCueRevealStartTime = 0f;
            _timedAudioLogTitleBuffer = null;
            _timedAudioLogTitleLength = 0;
            _timedAudioLogBodyBuffer = null;
            _timedAudioLogBodyLength = 0;
            _currentAudioLogHash = 0u;
            _lastStressCorruptionBucket = int.MinValue;
            NotifyCueChanged(0f, Array.Empty<char>(), 0, 0, 0f);
        }

        private float GetCueDuration(int cueIndex)
        {
            if ((uint)cueIndex >= (uint)_timedAudioLogCueCount)
                return 0f;

            float currentStart = Mathf.Max(0f, _timedAudioLogCues[cueIndex].StartTime);
            float nextStart = cueIndex + 1 < _timedAudioLogCueCount
                ? Mathf.Max(currentStart, _timedAudioLogCues[cueIndex + 1].StartTime)
                : Mathf.Max(currentStart, _timedAudioLogTotalDuration);
            return Mathf.Max(0.1f, nextStart - currentStart);
        }

        private int ResolveCurrentCueRevealLength()
        {
            if (_timedAudioLogCurrentCueLength <= 0)
                return 0;

            float revealDuration = Mathf.Max(0.1f, _timedAudioLogCurrentCueDuration);
            float elapsed = Mathf.Max(0f, Time.unscaledTime - _timedAudioLogCueRevealStartTime);
            float normalized = Mathf.Clamp01(elapsed / revealDuration);
            return Mathf.Clamp(
                Mathf.CeilToInt(_timedAudioLogCurrentCueLength * normalized),
                0,
                _timedAudioLogCurrentCueLength);
        }

        private void NotifyCueChanged(float duration, char[] textBuffer, int textStart, int textLength, float speakerIntensity)
        {
            OnCueChanged?.Invoke(duration, textBuffer, textStart, textLength, speakerIntensity);
        }

        private bool TryParseTimedSubtitleCues(char[] subtitleBuffer, int subtitleLength)
        {
            _timedAudioLogCueCount = 0;
            if (subtitleBuffer == null || subtitleLength <= 0)
                return false;

            int cursor = 0;
            bool sawMarker = false;
            while (cursor < subtitleLength)
            {
                if (subtitleBuffer[cursor] != '[')
                {
                    cursor++;
                    continue;
                }

                int markerEnd = FindChar(subtitleBuffer, cursor + 1, subtitleLength, ']');
                if (markerEnd <= cursor + 1)
                {
                    cursor++;
                    continue;
                }

                sawMarker = true;
                if (!TryParseCueHeader(subtitleBuffer, cursor + 1, markerEnd, out float startTime, out float speakerIntensity))
                {
                    cursor = markerEnd + 1;
                    continue;
                }

                int textStart = markerEnd + 1;
                int nextMarker = FindChar(subtitleBuffer, textStart, subtitleLength, '[');
                int textEnd = nextMarker >= 0 ? nextMarker : subtitleLength;
                TrimRange(subtitleBuffer, ref textStart, ref textEnd);
                if (textEnd > textStart && _timedAudioLogCueCount < _timedAudioLogCues.Length)
                {
                    _timedAudioLogCues[_timedAudioLogCueCount] = new TimedSubtitleCue
                    {
                        StartTime = Mathf.Max(0f, startTime),
                        SpeakerIntensity = speakerIntensity,
                        StartIndex = textStart,
                        Length = textEnd - textStart
                    };
                    _timedAudioLogCueCount++;
                }

                cursor = textEnd;
            }

            return sawMarker && _timedAudioLogCueCount > 0;
        }

        private static bool TryParseCueHeader(
            char[] buffer,
            int start,
            int endExclusive,
            out float startTime,
            out float speakerIntensity)
        {
            speakerIntensity = 1f;
            startTime = 0f;

            int separatorIndex = -1;
            for (int i = start; i < endExclusive; i++)
            {
                char current = buffer[i];
                if (current == '|' || current == ',')
                {
                    separatorIndex = i;
                    break;
                }
            }

            int timeStart = start;
            int timeEnd = separatorIndex >= 0 ? separatorIndex : endExclusive;
            TrimRange(buffer, ref timeStart, ref timeEnd);
            if (!float.TryParse(new ReadOnlySpan<char>(buffer, timeStart, timeEnd - timeStart), NumberStyles.Float, CultureInfo.InvariantCulture, out startTime))
                return false;

            if (separatorIndex < 0)
                return true;

            int intensityStart = separatorIndex + 1;
            int intensityEnd = endExclusive;
            TrimRange(buffer, ref intensityStart, ref intensityEnd);
            if (intensityEnd <= intensityStart)
                return true;

            if (!float.TryParse(new ReadOnlySpan<char>(buffer, intensityStart, intensityEnd - intensityStart), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedIntensity))
                return true;

            speakerIntensity = Mathf.Clamp01(parsedIntensity);
            return true;
        }

        private void CopyStringToRenderBuffer(string text)
        {
            int safeLength = 0;
            if (!string.IsNullOrEmpty(text))
            {
                safeLength = Mathf.Min(text.Length, _subtitleRenderBuffer.Length);
                for (int i = 0; i < safeLength; i++)
                    _subtitleRenderBuffer[i] = text[i];
            }

            for (int i = safeLength; i < _lastRenderedSubtitleLength && i < _subtitleRenderBuffer.Length; i++)
                _subtitleRenderBuffer[i] = '\0';
        }

        private void ApplySubtitleBuffer(int length)
        {
            if (_subtitleText == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, _subtitleRenderBuffer.Length);
            if (safeLength == _lastRenderedSubtitleLength &&
                BuffersMatch(_subtitleRenderBuffer, _lastRenderedSubtitleBuffer, safeLength))
            {
                return;
            }

            for (int i = 0; i < safeLength; i++)
                _lastRenderedSubtitleBuffer[i] = _subtitleRenderBuffer[i];

            _lastRenderedSubtitleLength = safeLength;
            _subtitleText.SetCharArray(_subtitleRenderBuffer, 0, safeLength);
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
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
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(68f, 8f);
            textRect.offsetMax = new Vector2(-22f, -12f);

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

            RectTransform[] waveformBars = new RectTransform[4]; // COLD ALLOC: RectTransform[4] - runtime waveform bars - owner: SubtitleManager
            for (int i = 0; i < waveformBars.Length; i++)
            {
                GameObject barObject = new GameObject(
                    "Bar_" + i,
                    typeof(RectTransform),
                    typeof(Image));
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

        private static bool BuffersMatch(char[] source, char[] comparison, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (source[i] != comparison[i])
                    return false;
            }

            return true;
        }

        private static int FindChar(char[] buffer, int start, int endExclusive, char value)
        {
            if (buffer == null)
                return -1;

            int safeStart = Mathf.Max(0, start);
            int safeEnd = Mathf.Min(endExclusive, buffer.Length);
            for (int i = safeStart; i < safeEnd; i++)
            {
                if (buffer[i] == value)
                    return i;
            }

            return -1;
        }

        private static void TrimRange(char[] buffer, ref int start, ref int endExclusive)
        {
            if (buffer == null)
            {
                start = 0;
                endExclusive = 0;
                return;
            }

            int safeStart = Mathf.Clamp(start, 0, buffer.Length);
            int safeEnd = Mathf.Clamp(endExclusive, safeStart, buffer.Length);
            while (safeStart < safeEnd && char.IsWhiteSpace(buffer[safeStart]))
                safeStart++;

            while (safeEnd > safeStart && char.IsWhiteSpace(buffer[safeEnd - 1]))
                safeEnd--;

            start = safeStart;
            endExclusive = safeEnd;
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }
    }
}
