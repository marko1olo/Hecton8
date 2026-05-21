using System;
using System.Globalization;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.Physics;
using Hecton8.World;
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

            public readonly PlaybackEventKind Kind;
            public readonly uint LoreHash;
            public readonly float DurationSeconds;
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
    public sealed class SubtitleManager : MonoBehaviour, ITickable, ILateFrameTickable, INotificationEventListener, IAudioLogEventListener, IGlobalRegistryHotSwapListener
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

        private struct BufferedSubtitleRequest
        {
            public int BufferIndex;
            public int Length;
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

        internal readonly struct SubtitleLineSlice
        {
            public SubtitleLineSlice(int start, int length, int nextStart)
            {
                Start = start;
                Length = length;
                NextStart = nextStart;
            }

            public readonly int Start;
            public readonly int Length;
            public readonly int NextStart;
        }

        private ref struct SubtitleSpanBuilder
        {
            private Span<char> _destination;
            public int Length;

            public SubtitleSpanBuilder(Span<char> destination)
            {
                _destination = destination;
                Length = 0;
            }

            public void Append(char value)
            {
                if ((uint)Length >= (uint)_destination.Length)
                    return;

                _destination[Length++] = value;
            }

            public void Append(char[] source, int start, int length)
            {
                if (source == null || length <= 0 || Length >= _destination.Length)
                    return;

                int safeStart = Mathf.Clamp(start, 0, source.Length);
                int safeLength = Mathf.Clamp(
                    length,
                    0,
                    Mathf.Min(source.Length - safeStart, _destination.Length - Length));
                if (safeLength <= 0)
                    return;

                source.AsSpan(safeStart, safeLength).CopyTo(_destination.Slice(Length));
                Length += safeLength;
            }
        }

        private const int MaxQueuedSubtitles = 8; // Power-of-two ring capacity; BufferedQueueMask depends on it.
        private const int BufferedQueueMask = MaxQueuedSubtitles - 1;
        private const int MaxBufferedSubtitleCharacters = CharBufferPool.RequiredVrTextCapacity;
        private const int MaxTimedAudioLogCueCount = 32;
        private const int MaxSubtitleRenderCharacters = 2048;
        private const byte SubtitleSignalInterruptFlag = 1;
        private const byte SubtitleSignalCriticalPriority = 200;
        private const float AudioLogCueMinimumShakeIntensity = 0.025f;
        private const float AudioLogCueMinimumImpulseEnergyJoules = 8f;
        private const float AudioLogCueMaximumImpulseEnergyJoules = 120f;
        private const float AudioLogCueMinimumImpulseVolume = 0.05f;
        private const float AudioLogCueMaximumImpulseVolume = 0.22f;
        private const float AudioLogCueMinimumImpulseRadius = 1.5f;
        private const float AudioLogCueMaximumImpulseRadius = 5.5f;
        private const float AudioLogCueMaximumCameraShake = 0.18f;

        private static readonly Color BackdropColor = new Color(0.01f, 0.04f, 0.06f, 0.64f);
        private static readonly Color TextColor = new Color(0.86f, 0.96f, 1f, 0.96f);
        private static readonly Color WaveformColor = new Color(0.72f, 0.97f, 1f, 0.92f);
        private static readonly char[] EmptyCueBuffer = new char[1]; // COLD ALLOC: char[1] - non-null empty cue sentinel - owner: SubtitleManager

        private readonly SubtitleRequest[] _stringQueue = new SubtitleRequest[MaxQueuedSubtitles]; // COLD ALLOC: SubtitleRequest[8] - fixed legacy string request ring - owner: SubtitleManager
        private readonly SubtitleCommandDTO[] _subtitleCommandQueue = new SubtitleCommandDTO[MaxQueuedSubtitles]; // COLD ALLOC: SubtitleCommandDTO[8] - zero-string Babel subtitle command ring - owner: SubtitleManager
        private readonly BufferedSubtitleRequest[] _bufferedQueue = new BufferedSubtitleRequest[MaxQueuedSubtitles]; // COLD ALLOC: BufferedSubtitleRequest[8] - zero-GC subtitle request ring - owner: SubtitleManager
        private readonly char[][] _bufferedQueueBuffers =
        {
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters],
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters],
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters],
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters]
        }; // COLD ALLOC: char[8][256] - queued zero-GC subtitle text storage - owner: SubtitleManager
        private readonly TimedSubtitleCue[] _timedAudioLogCues = new TimedSubtitleCue[MaxTimedAudioLogCueCount]; // COLD ALLOC: TimedSubtitleCue[32] - parsed timed subtitle cue metadata - owner: SubtitleManager
        private readonly char[] _subtitleRenderBuffer = new char[MaxSubtitleRenderCharacters]; // COLD ALLOC: char[2048] - subtitle TMP render buffer - owner: SubtitleManager
        private readonly char[] _lastRenderedSubtitleBuffer = new char[MaxSubtitleRenderCharacters]; // COLD ALLOC: char[2048] - subtitle change cache - owner: SubtitleManager
        private readonly char[] _pendingSubtitleSwapBuffer = new char[MaxSubtitleRenderCharacters]; // COLD ALLOC: char[2048] - LateUpdate TMP swap buffer - owner: SubtitleManager
        private readonly char[] _currentBufferedSubtitleBuffer = new char[MaxBufferedSubtitleCharacters]; // COLD ALLOC: char[256] - active zero-GC subtitle source cache - owner: SubtitleManager
        private readonly char[] _lastEnqueuedBufferedSubtitleBuffer = new char[MaxBufferedSubtitleCharacters]; // COLD ALLOC: char[256] - zero-GC repeat suppression cache - owner: SubtitleManager

        private static SubtitleManager s_activeInstance;

        [Header("Settings")]
        [SerializeField, Range(1.5f, 8f)] private float defaultDuration = 3.25f;
        [SerializeField, Range(1f, 12f)] private float fadeSpeed = 5f;
        [SerializeField, Range(1, 8)] private int maxQueuedSubtitles = 6;
        [SerializeField, Range(0.1f, 2f)] private float repeatSuppressWindow = 0.4f;
        [SerializeField, Range(12f, 96f)] private float typewriterCharactersPerSecond = 56f;
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
        private bool _registeredLateFrameSwap;
        private bool _serviceRegistered;
        private bool _hotSwapListenerRegistered;
        private LocalizationManager _cachedLocalization;
        private string _currentMessage;
        private SubtitleSource _currentSource;
        private string _lastEnqueuedMessage;
        private SubtitleSource _lastEnqueuedSource;
        private float _lastEnqueueTime = -999f;
        private int _subtitleCommandQueueHead;
        private int _subtitleCommandQueueCount;
        private int _bufferedQueueHead;
        private int _bufferedQueueCount;
        private SubtitleStateDTO _currentSubtitleState;
        private uint _lastEnqueuedCommandTextHash;
        private uint _lastEnqueuedCommandSpeakerHash;
        private int _lastGlobalSubtitleSignalFrame = -1;
        private int _stringQueueHead;
        private int _stringQueueCount;
        private int _currentBufferedSubtitleLength;
        private int _lastEnqueuedBufferedSubtitleLength = -1;
        private bool _currentUsesBufferedSubtitle;
        private int _lastRenderedSubtitleLength = -1;
        private bool _tmpTypewriterActive;
        private bool _subtitleRichTextPolicyInitialized;
        private bool _subtitleRichTextEnabled;
        private float _tmpTypewriterElapsed;
        private int _tmpTypewriterTargetCharacters;
        private bool _timedAudioLogActive;
        private bool _subtitleSwapPending;
        private float _timedAudioLogElapsed;
        private float _timedAudioLogTotalDuration;
        private int _timedAudioLogCueCount;
        private int _timedAudioLogNextCueIndex;
        private int _timedAudioLogCurrentCueStartIndex;
        private int _timedAudioLogCurrentCueLength;
        private int _timedAudioLogCurrentRevealLength;
        private float _timedAudioLogCurrentCueDuration;
        private char[] _timedAudioLogTitleBuffer;
        private int _timedAudioLogTitleLength;
        private char[] _timedAudioLogBodyBuffer;
        private int _timedAudioLogBodyLength;
        private uint _currentAudioLogHash;
        private int _lastStressCorruptionBucket = int.MinValue;
        private int _pendingSubtitleSwapLength = -1;
        private uint _currentSubtitleStartAudioFrame;
        private uint _currentSubtitleDurationFrames;
        private uint _tmpTypewriterStartAudioFrame;
        private uint _audioLogPlaybackStartAudioFrame;
        private uint _timedAudioLogCueRevealStartFrame;

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
                : null;
            if (targetCanvas == null && overlay != null)
                overlay.TryGetComponent(out targetCanvas);
            if (targetCanvas == null)
                return;

            GameObject owner = new GameObject("SubtitleManager", typeof(RectTransform));
            owner.layer = targetCanvas.gameObject.layer;

            owner.TryGetComponent(out RectTransform rect);
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
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            BabelSubtitleSyncRuntime.EnsureInitialized();
            font = LocalizedFontResolver.ResolveReadableFont(font);
            NotificationEvents.Register(this);
            AudioLogEvents.Register(this);
            EnsureBuilt();
            RegisterToTickManager();
            RegisterLateFrameSwap();
        }

        private void OnDisable()
        {
            NotificationEvents.Unregister(this);
            AudioLogEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            UnregisterLateFrameSwap();
            TryUnregisterFromGlobalRegistry();

            if (s_activeInstance == this)
                s_activeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            UnregisterLateFrameSwap();
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
            if (_serviceRegistered)
                RegisterToTickManager();
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSubtitleRuntime(this);
            _serviceRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _cachedLocalization = currentService as LocalizationManager;
                _lastStressCorruptionBucket = int.MinValue;
                if (_isShowing && _currentSource != SubtitleSource.AudioLog)
                    RefreshStressCorruptionIfNeeded();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && isActiveAndEnabled)
            {
                RegisterToTickManager();
                RegisterLateFrameSwap();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedLocalization = Hecton8.Core.GlobalRegistry.Localization;
        }

        /// <summary>
        /// Resolves a localization key and displays the subtitle for the requested duration.
        /// </summary>
        public void DisplaySubtitle(string key, float duration)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            uint keyHash = unchecked((uint)LocHash.Compute(key.AsSpan()));
            if (DisplaySubtitle(keyHash, duration))
                return;

            EnqueueBuffered(key.AsSpan(), duration, SubtitleSource.Generic, false);
        }

        /// <summary>
        /// Displays a caller-owned subtitle span through the zero-GC char-buffer path.
        /// </summary>
        /// <param name="text">Subtitle text. The span is copied before the method returns.</param>
        /// <param name="duration">Display duration in seconds.</param>
        /// <returns>True when a non-empty subtitle was accepted.</returns>
        public bool DisplaySubtitle(ReadOnlySpan<char> text, float duration)
        {
            return EnqueueBuffered(text, duration, SubtitleSource.Generic, false);
        }

        /// <summary>
        /// Resolves a Babel hash and displays it through the zero-string subtitle command ring.
        /// </summary>
        public bool DisplaySubtitle(uint textHash, float duration)
        {
            SubtitleCommandDTO command = new SubtitleCommandDTO
            {
                TextHash = textHash,
                Duration = duration
            };
            return DisplaySubtitle(in command, false);
        }

        /// <summary>
        /// Resolves a Babel hash with numeric ^0..^3 replacements and displays it without a managed string.
        /// </summary>
        public bool DisplaySubtitle(uint textHash, float duration, BabelFormatArgs formatArgs)
        {
            if (textHash == 0u || !CharBufferPool.TryAcquireBabel(out CharBufferPool.BabelLease lease))
                return false;

            try
            {
                float decodeStart = Time.realtimeSinceStartup;
                bool found = LocRegistry.TryWriteVisualSpanFromUtf8(
                    textHash,
                    lease.Span,
                    out int length,
                    formatArgs,
                    ShouldStripBabelRichTextForCurrentTier());
                float decodeMs = math.max(0f, (Time.realtimeSinceStartup - decodeStart) * 1000f);
                BabelSubtitleSyncRuntime.RecordDecode(textHash, length, !found, decodeMs);
                length = lease.CopyToTmpBuffer(length);
                if (length <= 0)
                    return false;

                bool accepted = EnqueueBuffered(lease.TmpBuffer.AsSpan(0, length), duration, SubtitleSource.Generic, false);
                if (accepted)
                    BabelSubtitleSyncRuntime.TryRegisterImmediateCue(textHash, duration, 0u);
                return accepted;
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        /// <summary>
        /// Displays a Babel subtitle command without creating a managed string.
        /// </summary>
        public bool DisplaySubtitle(in SubtitleCommandDTO command, bool interrupt = false)
        {
            if (command.TextHash == 0u)
                return false;

            EnsureBuilt();
            SubtitleCommandDTO normalized = command;
            if (float.IsNaN(normalized.Duration) || float.IsInfinity(normalized.Duration))
            {
                LocRegistry.DumpTelemetryForFault(normalized.TextHash);
                normalized.Duration = defaultDuration;
            }

            normalized.Duration = Mathf.Max(0.5f, normalized.Duration > 0f ? normalized.Duration : defaultDuration);

            if (_currentSubtitleState.TextHash == normalized.TextHash &&
                _currentSubtitleState.SpeakerHash == normalized.SpeakerHash &&
                _timer > 0f)
            {
                _timer = normalized.Duration;
                _currentSubtitleState.TimeRemaining = normalized.Duration;
                return true;
            }

            float now = BabelSubtitleSyncRuntime.ResolveCurrentAudioTimeSeconds();
            if (!interrupt &&
                normalized.TextHash == _lastEnqueuedCommandTextHash &&
                normalized.SpeakerHash == _lastEnqueuedCommandSpeakerHash &&
                now - _lastEnqueueTime < repeatSuppressWindow)
            {
                return true;
            }

            _lastEnqueuedCommandTextHash = normalized.TextHash;
            _lastEnqueuedCommandSpeakerHash = normalized.SpeakerHash;
            _lastEnqueuedSource = SubtitleSource.Generic;
            _lastEnqueueTime = now;

            if (interrupt)
                return ShowSubtitleCommand(in normalized);

            if (_timer <= 0f &&
                _stringQueueCount == 0 &&
                _subtitleCommandQueueCount == 0 &&
                _bufferedQueueCount == 0 &&
                !_isShowing &&
                _currentAlpha <= 0.01f)
            {
                return ShowSubtitleCommand(in normalized);
            }

            EnqueueSubtitleCommand(in normalized);
            return true;
        }

        /// <summary>
        /// Resolves a localized hash and displays it through the zero-GC char-buffer path.
        /// </summary>
        /// <param name="keyHash">Stable localization hash.</param>
        /// <param name="fallback">Fallback span used when Babel has no raw buffer.</param>
        /// <param name="duration">Display duration in seconds.</param>
        /// <returns>True when a non-empty subtitle was accepted.</returns>
        public bool DisplaySubtitle(int keyHash, ReadOnlySpan<char> fallback, float duration)
        {
            if (keyHash != 0)
                return DisplaySubtitle(unchecked((uint)keyHash), duration);

            return EnqueueBuffered(fallback, duration, SubtitleSource.Generic, false);
        }

        /// <summary>
        /// Displays a slice of a caller-owned character buffer without creating a string.
        /// </summary>
        /// <param name="buffer">Source character buffer.</param>
        /// <param name="start">Start index in the source buffer.</param>
        /// <param name="length">Number of characters to read.</param>
        /// <param name="duration">Display duration in seconds.</param>
        /// <returns>True when a non-empty subtitle was accepted.</returns>
        public bool DisplaySubtitle(char[] buffer, int start, int length, float duration)
        {
            if (buffer == null || length <= 0)
                return false;

            int safeStart = Mathf.Clamp(start, 0, buffer.Length);
            int safeLength = Mathf.Clamp(length, 0, buffer.Length - safeStart);
            return safeLength > 0 &&
                   EnqueueBuffered(buffer.AsSpan(safeStart, safeLength), duration, SubtitleSource.Generic, false);
        }

        public void Tick(float deltaTime)
        {
            if (_root == null)
                return;

            BabelSubtitleSyncRuntime.PreparePresentationFrame();
            DrainGlobalSubtitleSignals();
            DrainBabelCueSignals();

            if (_timedAudioLogActive && _currentSource == SubtitleSource.AudioLog)
                AdvanceTimedAudioLog();
            else if (_tmpTypewriterActive)
                AdvanceTmpTypewriter();

            _timer = ResolveCurrentSubtitleTimeRemaining();
            if (_timer > 0f)
            {
                _currentSubtitleState.TimeRemaining = math.max(0f, _timer);
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
                    _currentUsesBufferedSubtitle = false;
                    _currentBufferedSubtitleLength = 0;
                    _currentSource = SubtitleSource.Generic;
                    _currentSubtitleState = default;
                    _currentSubtitleDurationFrames = 0u;
                    StopTmpTypewriter(0);

                    if (TryDequeueSubtitleCommand(out SubtitleCommandDTO commandNext) &&
                        ShowSubtitleCommand(in commandNext))
                    {
                    }
                    else if (TryDequeueBufferedSubtitle(out BufferedSubtitleRequest bufferedNext))
                    {
                        ShowImmediate(bufferedNext);
                    }
                    else if (TryDequeueStringSubtitle(out SubtitleRequest next))
                    {
                        ShowImmediate(next.Message, next.Duration, next.Source);
                    }
                    else
                    {
                        ApplySubtitleBuffer(0);
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

        public void LateFrameTick()
        {
            if (!_subtitleSwapPending)
                return;

            FlushPendingSubtitleSwap();
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
            float resolvedDuration = durationSeconds > 0.01f
                ? Mathf.Clamp(durationSeconds, 1.5f, 30f)
                : defaultDuration;
            ArmSubtitleAudioTimer(resolvedDuration);
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
            float now = BabelSubtitleSyncRuntime.ResolveCurrentAudioTimeSeconds();

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

            if (_timer <= 0f &&
                _stringQueueCount == 0 &&
                _subtitleCommandQueueCount == 0 &&
                !_isShowing &&
                _currentAlpha <= 0.01f)
            {
                ShowImmediate(normalized, resolvedDuration, source);
                return;
            }

            EnqueueStringSubtitle(normalized, resolvedDuration, source);
        }

        private bool TryDequeueStringSubtitle(out SubtitleRequest request)
        {
            if (_stringQueueCount <= 0)
            {
                request = default;
                _stringQueueHead = 0;
                return false;
            }

            request = _stringQueue[_stringQueueHead];
            _stringQueue[_stringQueueHead] = default;
            _stringQueueHead = (_stringQueueHead + 1) & BufferedQueueMask;
            _stringQueueCount--;
            if (_stringQueueCount == 0)
                _stringQueueHead = 0;

            return true;
        }

        private void EnqueueStringSubtitle(string message, float duration, SubtitleSource source)
        {
            int capacity = Mathf.Clamp(maxQueuedSubtitles, 1, _stringQueue.Length);
            if (_stringQueueCount >= capacity)
            {
                _stringQueue[_stringQueueHead] = default;
                _stringQueueHead = (_stringQueueHead + 1) & BufferedQueueMask;
                _stringQueueCount--;
            }

            int slot = (_stringQueueHead + _stringQueueCount) & BufferedQueueMask;
            _stringQueue[slot] = new SubtitleRequest
            {
                Message = message,
                Duration = duration,
                Source = source
            };
            _stringQueueCount++;
        }

        private void EnqueueSubtitleCommand(in SubtitleCommandDTO command)
        {
            int capacity = Mathf.Clamp(maxQueuedSubtitles, 1, _subtitleCommandQueue.Length);
            if (_subtitleCommandQueueCount >= capacity)
            {
                _subtitleCommandQueueHead = (_subtitleCommandQueueHead + 1) & BufferedQueueMask;
                _subtitleCommandQueueCount--;
            }

            int slot = (_subtitleCommandQueueHead + _subtitleCommandQueueCount) & BufferedQueueMask;
            _subtitleCommandQueue[slot] = command;
            _subtitleCommandQueueCount++;
        }

        private void DrainGlobalSubtitleSignals()
        {
            int frame = Time.frameCount;
            if (_lastGlobalSubtitleSignalFrame == frame)
                return;

            _lastGlobalSubtitleSignalFrame = frame;
            ReadOnlySpan<SubtitleSignal> signals = SignalBus<SubtitleSignal>.GetFrameSnapshot();
            int count = math.min(signals.Length, MaxQueuedSubtitles);
            for (int i = 0; i < count; i++)
            {
                SubtitleSignal signal = signals[i];
                if (signal.SubtitleHash == 0u)
                    continue;

                SubtitleCommandDTO command = new SubtitleCommandDTO
                {
                    SpeakerHash = signal.SpeakerHash,
                    TextHash = signal.SubtitleHash,
                    Duration = signal.DurationSeconds,
                    _pad0 = (signal.Flags & SubtitleSignalInterruptFlag) != 0
                        ? BabelSubtitleSyncRuntime.FlagInterrupt
                        : 0u
                };
                bool interrupt = signal.Priority >= SubtitleSignalCriticalPriority ||
                                 (signal.Flags & SubtitleSignalInterruptFlag) != 0;
                DisplaySubtitle(in command, interrupt);
            }
        }

        private void DrainBabelCueSignals()
        {
            int consumed = 0;
            while (consumed < MaxQueuedSubtitles && BabelSubtitleSyncRuntime.TryConsumeReadyCue(out SubtitleCueDTO cue))
            {
                consumed++;
                SubtitleCommandDTO command = new SubtitleCommandDTO
                {
                    TextHash = cue.TokenHash,
                    Duration = cue.DisplayDuration,
                    _pad0 = cue.Flags
                };
                bool interrupt = (cue.Flags & BabelSubtitleSyncRuntime.FlagInterrupt) != 0u;
                DisplaySubtitle(in command, interrupt);
            }
        }

        private bool TryDequeueSubtitleCommand(out SubtitleCommandDTO command)
        {
            if (_subtitleCommandQueueCount <= 0)
            {
                command = default;
                _subtitleCommandQueueHead = 0;
                return false;
            }

            command = _subtitleCommandQueue[_subtitleCommandQueueHead];
            _subtitleCommandQueue[_subtitleCommandQueueHead] = default;
            _subtitleCommandQueueHead = (_subtitleCommandQueueHead + 1) & BufferedQueueMask;
            _subtitleCommandQueueCount--;
            if (_subtitleCommandQueueCount == 0)
                _subtitleCommandQueueHead = 0;

            return true;
        }

        private bool ShowSubtitleCommand(in SubtitleCommandDTO command)
        {
            if (!CharBufferPool.TryAcquireBabel(out CharBufferPool.BabelLease lease))
                return false;

            try
            {
                Span<char> destination = lease.Span;
                float decodeStart = Time.realtimeSinceStartup;
                bool found = LocRegistry.TryWriteVisualSpanFromUtf8(
                    command.TextHash,
                    destination,
                    out int length,
                    ShouldStripBabelRichTextForCurrentTier());
                AppendDirectionalArrow(command._pad0, destination, ref length);
                float decodeMs = math.max(0f, (Time.realtimeSinceStartup - decodeStart) * 1000f);
                BabelSubtitleSyncRuntime.RecordDecode(command.TextHash, length, !found, decodeMs);
                length = lease.CopyToTmpBuffer(length);
                if (length <= 0)
                    return false;

                ShowImmediate(lease.TmpBuffer.AsSpan(0, length), command.Duration, SubtitleSource.Generic);
                if ((command._pad0 & BabelSubtitleSyncRuntime.FlagPresented) == 0u)
                    BabelSubtitleSyncRuntime.TryRegisterImmediateCue(command.TextHash, command.Duration, command._pad0);
                _currentSubtitleState = new SubtitleStateDTO
                {
                    SpeakerHash = command.SpeakerHash,
                    TextHash = command.TextHash,
                    TimeRemaining = command.Duration,
                    VisibleCharacters = 0,
                    Flags = (ushort)((ShouldStripBabelRichTextForCurrentTier() ? 1u : 0u) | (command._pad0 & ushort.MaxValue))
                };
                return true;
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        private bool EnqueueBuffered(ReadOnlySpan<char> message, float duration, SubtitleSource source, bool interrupt)
        {
            EnsureBuilt();
            if (!TryCopyNormalizedSubtitleToPool(message, out CharBufferPool.Lease lease, out int normalizedLength))
                return false;

            try
            {
                if (normalizedLength <= 0)
                    return false;

                float resolvedDuration = Mathf.Max(0.5f, duration);
                float now = BabelSubtitleSyncRuntime.ResolveCurrentAudioTimeSeconds();
                char[] normalized = lease.Buffer;

                if (_currentUsesBufferedSubtitle &&
                    source == _currentSource &&
                    normalizedLength == _currentBufferedSubtitleLength &&
                    BuffersMatch(normalized, _currentBufferedSubtitleBuffer, normalizedLength) &&
                    _timer > 0f)
                {
                    _timer = resolvedDuration;
                    return true;
                }

                if (!interrupt &&
                    source == _lastEnqueuedSource &&
                    normalizedLength == _lastEnqueuedBufferedSubtitleLength &&
                    BuffersMatch(normalized, _lastEnqueuedBufferedSubtitleBuffer, normalizedLength) &&
                    now - _lastEnqueueTime < repeatSuppressWindow)
                {
                    return true;
                }

                CopyBuffer(normalized, _lastEnqueuedBufferedSubtitleBuffer, normalizedLength);
                _lastEnqueuedBufferedSubtitleLength = normalizedLength;
                _lastEnqueuedSource = source;
                _lastEnqueueTime = now;

                if (interrupt)
                {
                    ShowImmediate(normalized.AsSpan(0, normalizedLength), resolvedDuration, source);
                    return true;
                }

                if (_timer <= 0f &&
                    _stringQueueCount == 0 &&
                    _subtitleCommandQueueCount == 0 &&
                    _bufferedQueueCount == 0 &&
                    !_isShowing &&
                    _currentAlpha <= 0.01f)
                {
                    ShowImmediate(normalized.AsSpan(0, normalizedLength), resolvedDuration, source);
                    return true;
                }

                EnqueueBufferedSubtitle(normalized, normalizedLength, resolvedDuration, source);
                return true;
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        private void ShowImmediate(string message, float duration, SubtitleSource source)
        {
            _currentMessage = message;
            _currentUsesBufferedSubtitle = false;
            _currentBufferedSubtitleLength = 0;
            _currentSource = source;
            ArmSubtitleAudioTimer(duration);
            _currentAlpha = 0f;
            _isShowing = true;
            _currentSubtitleState = default;
            _lastStressCorruptionBucket = int.MinValue;

            if (source == SubtitleSource.AudioLog)
            {
                StopTmpTypewriter(int.MaxValue);
                ApplySubtitleBuffer(BuildCurrentAudioLogFrame());
            }
            else
            {
                string displayMessage = ResolveDisplayMessage(source, message);
                CopyStringToRenderBuffer(displayMessage);
                int renderLength = Mathf.Min(displayMessage != null ? displayMessage.Length : 0, _subtitleRenderBuffer.Length);
                ApplySubtitleBuffer(renderLength);
                StartTmpTypewriter(renderLength);
            }

            if (_audioCueGroup != null)
                _audioCueGroup.alpha = source == SubtitleSource.AudioLog ? _currentAlpha : 0f;
        }

        private void ShowImmediate(BufferedSubtitleRequest request)
        {
            int bufferIndex = Mathf.Clamp(request.BufferIndex, 0, _bufferedQueueBuffers.Length - 1);
            int safeLength = Mathf.Clamp(request.Length, 0, MaxBufferedSubtitleCharacters);
            ShowImmediate(_bufferedQueueBuffers[bufferIndex].AsSpan(0, safeLength), request.Duration, request.Source);
        }

        private void ShowImmediate(ReadOnlySpan<char> message, float duration, SubtitleSource source)
        {
            int safeLength = CopySpanToBuffer(message, _currentBufferedSubtitleBuffer);
            _currentBufferedSubtitleLength = safeLength;
            _currentUsesBufferedSubtitle = safeLength > 0;
            _currentMessage = string.Empty;
            _currentSource = source;
            ArmSubtitleAudioTimer(duration);
            _currentAlpha = 0f;
            _isShowing = safeLength > 0;
            _currentSubtitleState = default;
            _lastStressCorruptionBucket = int.MinValue;

            int renderLength = CopyBufferedDisplayToRenderBuffer(source);
            ApplySubtitleBuffer(renderLength);
            if (source == SubtitleSource.AudioLog)
                StopTmpTypewriter(int.MaxValue);
            else
                StartTmpTypewriter(renderLength);

            if (_audioCueGroup != null)
                _audioCueGroup.alpha = 0f;
        }

        private bool TryCopyNormalizedSubtitleToPool(
            ReadOnlySpan<char> message,
            out CharBufferPool.Lease lease,
            out int normalizedLength)
        {
            lease = default;
            normalizedLength = 0;

            int start = 0;
            int end = message.Length;
            while (start < end && char.IsWhiteSpace(message[start]))
                start++;

            while (end > start && char.IsWhiteSpace(message[end - 1]))
                end--;

            if (end <= start || !CharBufferPool.TryAcquire(out lease))
                return false;

            normalizedLength = CopySpanToBuffer(message.Slice(start, end - start), lease.Buffer);
            if (normalizedLength > 0)
                return true;

            CharBufferPool.Release(in lease);
            lease = default;
            return false;
        }

        private bool TryDequeueBufferedSubtitle(out BufferedSubtitleRequest request)
        {
            if (_bufferedQueueCount <= 0)
            {
                request = default;
                _bufferedQueueHead = 0;
                return false;
            }

            request = _bufferedQueue[_bufferedQueueHead];
            _bufferedQueue[_bufferedQueueHead] = default;
            _bufferedQueueHead = (_bufferedQueueHead + 1) & BufferedQueueMask;
            _bufferedQueueCount--;
            if (_bufferedQueueCount == 0)
                _bufferedQueueHead = 0;

            return true;
        }

        private void EnqueueBufferedSubtitle(
            char[] normalized,
            int normalizedLength,
            float duration,
            SubtitleSource source)
        {
            if (normalized == null || normalizedLength <= 0)
                return;

            int capacity = Mathf.Clamp(maxQueuedSubtitles, 1, _bufferedQueue.Length);
            if (_bufferedQueueCount >= capacity)
            {
                _bufferedQueueHead = (_bufferedQueueHead + 1) & BufferedQueueMask;
                _bufferedQueueCount--;
            }

            int slot = (_bufferedQueueHead + _bufferedQueueCount) & BufferedQueueMask;
            int safeLength = CopyBuffer(normalized, _bufferedQueueBuffers[slot], normalizedLength);
            _bufferedQueue[slot] = new BufferedSubtitleRequest
            {
                BufferIndex = slot,
                Length = safeLength,
                Duration = duration,
                Source = source
            };
            _bufferedQueueCount++;
        }

        private int CopyBufferedDisplayToRenderBuffer(SubtitleSource source)
        {
            if (!_currentUsesBufferedSubtitle || _currentBufferedSubtitleLength <= 0)
                return 0;

            int safeLength = Mathf.Clamp(_currentBufferedSubtitleLength, 0, _currentBufferedSubtitleBuffer.Length);
            ReadOnlySpan<char> sourceSpan = _currentBufferedSubtitleBuffer.AsSpan(0, safeLength);
            LocalizationManager manager = _cachedLocalization;
            if (source != SubtitleSource.AudioLog &&
                manager != null &&
                manager.TryApplyHullStressCorruptionIfNeeded(sourceSpan, _subtitleRenderBuffer, out int corruptedLength))
            {
                return Mathf.Clamp(corruptedLength, 0, _subtitleRenderBuffer.Length);
            }

            return CopySpanToBuffer(sourceSpan, _subtitleRenderBuffer);
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> source, char[] destination)
        {
            if (destination == null || source.Length <= 0)
                return 0;

            int safeLength = Mathf.Min(source.Length, destination.Length);
            for (int i = 0; i < safeLength; i++)
                destination[i] = source[i];

            return safeLength;
        }

        private static void AppendDirectionalArrow(uint flags, Span<char> destination, ref int length)
        {
            if (!BabelSubtitleSyncRuntime.TryResolveCueArrow(flags, out char arrow) ||
                length < 0 ||
                length >= destination.Length)
            {
                return;
            }

            if (length + 2 <= destination.Length)
                destination[length++] = ' ';

            if (length < destination.Length)
                destination[length++] = arrow;
        }

        private static int CopyBuffer(char[] source, char[] destination, int length)
        {
            if (source == null || destination == null || length <= 0)
                return 0;

            int safeLength = Mathf.Min(length, Mathf.Min(source.Length, destination.Length));
            for (int i = 0; i < safeLength; i++)
                destination[i] = source[i];

            return safeLength;
        }

        private void StartTmpTypewriter(int targetCharacters)
        {
            _tmpTypewriterTargetCharacters = Mathf.Clamp(targetCharacters, 0, _subtitleRenderBuffer.Length);
            _tmpTypewriterElapsed = 0f;
            _tmpTypewriterStartAudioFrame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
            _tmpTypewriterActive = _tmpTypewriterTargetCharacters > 0;
            if (_subtitleText == null)
                return;

            _subtitleText.maxVisibleCharacters = 0;
        }

        private void StopTmpTypewriter(int visibleCharacters)
        {
            _tmpTypewriterActive = false;
            _tmpTypewriterElapsed = 0f;
            _tmpTypewriterTargetCharacters = 0;
            if (_subtitleText != null)
                _subtitleText.maxVisibleCharacters = visibleCharacters;
        }

        private void ArmSubtitleAudioTimer(float durationSeconds)
        {
            float safeDuration = Mathf.Max(0.05f, durationSeconds);
            BabelSubtitleSyncRuntime.PreparePresentationFrame();
            _currentSubtitleStartAudioFrame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
            _currentSubtitleDurationFrames = BabelSubtitleSyncRuntime.ResolveDurationFrames(safeDuration);
            _timer = _currentSubtitleDurationFrames / (float)Mathf.Max(1, BabelSubtitleSyncRuntime.CurrentSampleRate);
        }

        private float ResolveCurrentSubtitleTimeRemaining()
        {
            if (!_isShowing)
                return 0f;

            if (_currentSubtitleDurationFrames == 0u)
                return Mathf.Max(0f, _timer);

            uint elapsedFrames = unchecked(BabelSubtitleSyncRuntime.CurrentAudioFrame - _currentSubtitleStartAudioFrame);
            if (elapsedFrames >= _currentSubtitleDurationFrames)
                return 0f;

            uint remainingFrames = _currentSubtitleDurationFrames - elapsedFrames;
            return remainingFrames / (float)Mathf.Max(1, BabelSubtitleSyncRuntime.CurrentSampleRate);
        }

        private void AdvanceTmpTypewriter()
        {
            if (_subtitleText == null || _tmpTypewriterTargetCharacters <= 0)
            {
                _tmpTypewriterActive = false;
                return;
            }

            _tmpTypewriterElapsed = math.max(0f, BabelSubtitleSyncRuntime.ResolveElapsedSecondsSince(_tmpTypewriterStartAudioFrame));
            int visible = Mathf.Clamp(
                Mathf.CeilToInt(_tmpTypewriterElapsed * math.max(1f, typewriterCharactersPerSecond)),
                0,
                _tmpTypewriterTargetCharacters);
            if (_subtitleText.maxVisibleCharacters != visible)
                _subtitleText.maxVisibleCharacters = visible;

            _currentSubtitleState.VisibleCharacters = (ushort)math.min(visible, ushort.MaxValue);
            if (visible >= _tmpTypewriterTargetCharacters)
                _tmpTypewriterActive = false;
        }

        private static bool ShouldStripBabelRichTextForCurrentTier()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.select(0f, quality, math.isfinite(quality)));
            float richTextWeight = math.saturate((quality - 0.35f) * 2.5f);
            return richTextWeight < 0.5f;
        }

        private void RefreshSubtitleTextLodPolicy()
        {
            if (_subtitleText == null)
                return;

            bool enableRichText = !ShouldStripBabelRichTextForCurrentTier();
            if (_subtitleRichTextPolicyInitialized && _subtitleRichTextEnabled == enableRichText)
                return;

            _subtitleText.richText = enableRichText;
            _subtitleRichTextEnabled = enableRichText;
            _subtitleRichTextPolicyInitialized = true;
        }

        private void RefreshStressCorruptionIfNeeded()
        {
            if (_currentSource == SubtitleSource.AudioLog)
                return;

            LocalizationManager manager = _cachedLocalization;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            if (_currentUsesBufferedSubtitle)
            {
                ApplySubtitleBuffer(CopyBufferedDisplayToRenderBuffer(_currentSource));
                return;
            }

            string displayMessage = ResolveDisplayMessage(_currentSource, _currentMessage);
            CopyStringToRenderBuffer(displayMessage);
            ApplySubtitleBuffer(Mathf.Min(displayMessage != null ? displayMessage.Length : 0, _subtitleRenderBuffer.Length));
        }

        private string ResolveDisplayMessage(SubtitleSource source, string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            LocalizationManager manager = _cachedLocalization;
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
            _audioLogPlaybackStartAudioFrame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
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
            _timedAudioLogCueRevealStartFrame = _audioLogPlaybackStartAudioFrame;
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
                    _timedAudioLogCueRevealStartFrame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
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
                    NotifyCueChanged(0f, EmptyCueBuffer, 0, 0, 0f);
                }
            }
            else
            {
                _timedAudioLogCurrentCueStartIndex = 0;
                _timedAudioLogCurrentCueLength = hasBody ? _timedAudioLogBodyLength : 0;
                _timedAudioLogCurrentCueDuration = _timedAudioLogTotalDuration;
                _timedAudioLogCueRevealStartFrame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
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

        private void AdvanceTimedAudioLog()
        {
            _timedAudioLogElapsed = BabelSubtitleSyncRuntime.ResolveElapsedSecondsSince(_audioLogPlaybackStartAudioFrame);
            bool changed = false;
            int lastCueIndex = -1;
            while (_timedAudioLogNextCueIndex < _timedAudioLogCueCount &&
                   _timedAudioLogElapsed >= _timedAudioLogCues[_timedAudioLogNextCueIndex].StartTime)
            {
                lastCueIndex = _timedAudioLogNextCueIndex;
                _timedAudioLogCurrentCueStartIndex = _timedAudioLogCues[lastCueIndex].StartIndex;
                _timedAudioLogCurrentCueLength = _timedAudioLogCues[lastCueIndex].Length;
                _timedAudioLogCurrentCueDuration = GetCueDuration(lastCueIndex);
                _timedAudioLogCueRevealStartFrame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
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
            _timedAudioLogCueRevealStartFrame = 0u;
            _audioLogPlaybackStartAudioFrame = 0u;
            _timedAudioLogTitleBuffer = null;
            _timedAudioLogTitleLength = 0;
            _timedAudioLogBodyBuffer = null;
            _timedAudioLogBodyLength = 0;
            _currentAudioLogHash = 0u;
            _lastStressCorruptionBucket = int.MinValue;
            NotifyCueChanged(0f, EmptyCueBuffer, 0, 0, 0f);
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
            float elapsed = Mathf.Max(0f, BabelSubtitleSyncRuntime.ResolveElapsedSecondsSince(_timedAudioLogCueRevealStartFrame));
            float normalized = Mathf.Clamp01(elapsed / revealDuration);
            return Mathf.Clamp(
                Mathf.CeilToInt(_timedAudioLogCurrentCueLength * normalized),
                0,
                _timedAudioLogCurrentCueLength);
        }

        private void NotifyCueChanged(float duration, char[] textBuffer, int textStart, int textLength, float speakerIntensity)
        {
            OnCueChanged?.Invoke(duration, textBuffer, textStart, textLength, speakerIntensity);
            EmitAudioLogCueSensoryPulse(duration, speakerIntensity);
        }

        private void EmitAudioLogCueSensoryPulse(float duration, float speakerIntensity)
        {
            if (_currentAudioLogHash == 0u || duration <= 0f)
                return;

            float intensity = math.saturate(speakerIntensity);
            if (intensity <= AudioLogCueMinimumShakeIntensity)
                return;

            ResolveAudioLogCueTransform(out Vector3 runtimePosition, out Vector3 direction);
            float energyJoules = math.lerp(
                AudioLogCueMinimumImpulseEnergyJoules,
                AudioLogCueMaximumImpulseEnergyJoules,
                intensity);
            float volume01 = math.lerp(
                AudioLogCueMinimumImpulseVolume,
                AudioLogCueMaximumImpulseVolume,
                intensity);
            float radiusMeters = math.lerp(
                AudioLogCueMinimumImpulseRadius,
                AudioLogCueMaximumImpulseRadius,
                intensity);

            AcousticImpulseEvent impulseEvent = new AcousticImpulseEvent(
                runtimePosition,
                direction,
                energyJoules,
                volume01,
                1f,
                radiusMeters,
                0,
                0,
                AcousticImpulseFlags.None);
            PhysicsEventBus.NotifyAcousticImpulse(in impulseEvent);

            CameraJuiceSignals.PublishImpact(
                math.min(AudioLogCueMaximumCameraShake, intensity * 0.12f),
                runtimePosition,
                direction);
        }

        private static void ResolveAudioLogCueTransform(out Vector3 runtimePosition, out Vector3 direction)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                runtimePosition = Vector3.zero;
                direction = Vector3.forward;
                return;
            }

            runtimePosition = (Vector3)snapshot.RuntimePosition;
            float3 forward = snapshot.Forward;
            float forwardSq = math.lengthsq(forward);
            if (!math.all(math.isfinite(forward)) || forwardSq <= 0.0001f)
            {
                direction = Vector3.forward;
                return;
            }

            direction = (Vector3)(forward * math.rsqrt(math.max(forwardSq, 0.0001f)));
            runtimePosition += direction;
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
            for (int i = 0; i < safeLength; i++)
                _pendingSubtitleSwapBuffer[i] = _subtitleRenderBuffer[i];

            _pendingSubtitleSwapLength = safeLength;
            _subtitleSwapPending = true;
            if (!_registeredLateFrameSwap)
                FlushPendingSubtitleSwap();
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
        }

        private bool RegisterLateFrameSwap()
        {
            if (_registeredLateFrameSwap)
                return true;

            if (!Application.isPlaying)
                return false;

            _registeredLateFrameSwap = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            return _registeredLateFrameSwap;
        }

        private void UnregisterLateFrameSwap()
        {
            if (!_registeredLateFrameSwap)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrameSwap = false;
        }

        private void FlushPendingSubtitleSwap()
        {
            if (!_subtitleSwapPending)
                return;

            int safeLength = Mathf.Clamp(_pendingSubtitleSwapLength, 0, _pendingSubtitleSwapBuffer.Length);
            RefreshSubtitleTextLodPolicy();
            if (_subtitleText != null)
                _subtitleText.SetCharArray(_pendingSubtitleSwapBuffer, 0, safeLength);

            _pendingSubtitleSwapLength = -1;
            _subtitleSwapPending = false;
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

            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            if (!TryGetComponent(out _backdrop))
                _backdrop = gameObject.AddComponent<Image>();
            _backdrop.color = BackdropColor;
            _backdrop.raycastTarget = false;

            GameObject textOwner = new GameObject("SubtitleText", typeof(RectTransform));
            textOwner.layer = gameObject.layer;
            textOwner.TryGetComponent(out RectTransform textRect);
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
            RefreshSubtitleTextLodPolicy();
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

            waveformOwner.TryGetComponent(out RectTransform waveformRoot);
            waveformRoot.SetParent(_root, false);
            waveformRoot.anchorMin = new Vector2(0f, 0.5f);
            waveformRoot.anchorMax = new Vector2(0f, 0.5f);
            waveformRoot.pivot = new Vector2(0f, 0.5f);
            waveformRoot.anchoredPosition = new Vector2(18f, 0f);
            waveformRoot.sizeDelta = new Vector2(42f, 34f);

            waveformOwner.TryGetComponent(out _audioCueGroup);
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

                barObject.TryGetComponent(out RectTransform barRect);
                barRect.SetParent(waveformRoot, false);
                barRect.anchorMin = new Vector2(0f, 0.5f);
                barRect.anchorMax = new Vector2(0f, 0.5f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.sizeDelta = new Vector2(5f, 18f);
                barRect.anchoredPosition = new Vector2(5f + i * 9f, 0f);

                barObject.TryGetComponent(out Image barImage);
                barImage.color = WaveformColor;
                barImage.raycastTarget = false;
                waveformBars[i] = barRect;
            }

            waveformOwner.TryGetComponent(out _audioWaveformAnimator);
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

        internal static bool TrySliceSubtitleLine(
            ReadOnlySpan<char> source,
            int start,
            int maxCharacters,
            out SubtitleLineSlice slice)
        {
            slice = default;
            int safeStart = Mathf.Clamp(start, 0, source.Length);
            int safeMax = Mathf.Max(1, maxCharacters);
            while (safeStart < source.Length && char.IsWhiteSpace(source[safeStart]))
                safeStart++;

            if (safeStart >= source.Length)
                return false;

            int hardEnd = Mathf.Min(source.Length, safeStart + safeMax);
            int punctuationEnd = -1;
            int whitespaceEnd = -1;
            for (int i = safeStart; i < hardEnd; i++)
            {
                char value = source[i];
                if (IsSubtitleSlicePunctuation(value))
                    punctuationEnd = i + 1;
                else if (char.IsWhiteSpace(value))
                    whitespaceEnd = i;
            }

            int end = hardEnd >= source.Length
                ? source.Length
                : punctuationEnd > safeStart
                    ? punctuationEnd
                    : whitespaceEnd > safeStart
                        ? whitespaceEnd
                        : hardEnd;

            while (end > safeStart && char.IsWhiteSpace(source[end - 1]))
                end--;

            int nextStart = Mathf.Max(end, safeStart);
            while (nextStart < source.Length && char.IsWhiteSpace(source[nextStart]))
                nextStart++;

            slice = new SubtitleLineSlice(safeStart, end - safeStart, nextStart);
            return slice.Length > 0;
        }

        private static bool IsSubtitleSlicePunctuation(char value)
        {
            return value == '.' ||
                   value == ',' ||
                   value == ';' ||
                   value == ':' ||
                   value == '!' ||
                   value == '?';
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
