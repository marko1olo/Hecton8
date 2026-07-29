using System;
using System.Diagnostics;
using System.Globalization;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Input;
using Hecton8.Narrative;
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
            AudioLogEvents.TryRaisePlaybackStarted(loreHash, durationSeconds);
        }

        /// <summary>
        /// Publish a subtitle stop event.
        /// </summary>
        public static void RaisePlaybackStopped(uint loreHash)
        {
            AudioLogEvents.TryRaisePlaybackStopped(loreHash);
        }

        /// <summary>
        /// Publish a subtitle completion event.
        /// </summary>
        public static void RaisePlaybackCompleted(uint loreHash)
        {
            AudioLogEvents.TryRaisePlaybackCompleted(loreHash);
        }
    }

    /// <summary>
    /// Lower-screen subtitle owner for localized notifications and lore-backed spoken playback.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Subtitle Manager")]
    public sealed class SubtitleManager : MonoBehaviour, ILateFrameTickable, INotificationEventListener, IAudioLogEventListener, IGlobalRegistryHotSwapListener
    {
        private enum SubtitleSource
        {
            Generic = 0,
            Notification = 1,
            AudioLog = 2
        }

        private struct BufferedSubtitleCue
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

        private struct PendingAudioLogSubtitleEvent
        {
            public AudioLogEventType Type;
            public uint LogHash;
            public float DurationSeconds;

            public void Clear()
            {
                Type = default;
                LogHash = 0u;
                DurationSeconds = 0f;
            }
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
        private const int WaveformBarCount = 4;
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
        private const float AudioLogCueCameraAmplitudeScale = 0.55f;
        private const float AudioLogCueCameraTranslationGain = 0.25f;
        private const float AudioLogCueCameraRotationGain = 0.45f;
        private const byte PowerTextGlitchBatteryThresholdPercent = 25;
        private const float PowerTextGlitchEnergyThreshold01 = 0.18f;
        private const float PowerTextGlitchRiseSpeed = 11f;
        private const float PowerTextGlitchDecaySpeed = 3.5f;
        private const float PowerTextGlitchMaximumMutationRate = 0.11f;
        private const float SubtitleBaseFontSize = 22f;
        private const float SubtitleMinimumFontSize = 16f;
        private const uint SubtitleSpeakerHashVocalWarning = 0x41565753u; // AVWS
        private const uint SubtitleSpeakerHashVocalWarningSystem = 0x56333532u; // V352
        private const uint SubtitleSpeakerHashBabel = 0xBA150150u;
        private const uint SubtitleSpeakerHashWfcAudioLog = 0x57464341u; // WFCA

        private static readonly Color BackdropColor = new Color(0.01f, 0.04f, 0.06f, 0.64f);
        private static readonly Color TextColor = new Color(0.86f, 0.96f, 1f, 0.96f);
        private static readonly Color WaveformColor = new Color(0.72f, 0.97f, 1f, 0.92f);
        private static readonly char[] EmptyCueBuffer = new char[1]; // COLD ALLOC: char[1] - non-null empty cue sentinel - owner: SubtitleManager
        private static int s_x001SubtitleManagerSignalPushDropCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // One-shot latch for the dead-lane advisory in DrainGlobalSubtitleSignals. That method runs on the
        // per-frame ILateFrameTickable cadence, so the advisory must cost one bool read after the first frame
        // and must never build a string. Reset per play session by ResetStaticState.
        private static bool s_deadSubtitleSignalLaneWarned;
#endif

        private readonly SubtitleCommandDTO[] _subtitleCommandQueue = new SubtitleCommandDTO[MaxQueuedSubtitles]; // COLD ALLOC: SubtitleCommandDTO[8] - zero-string Babel subtitle command ring - owner: SubtitleManager
        private readonly BufferedSubtitleCue[] _bufferedQueue = new BufferedSubtitleCue[MaxQueuedSubtitles]; // COLD ALLOC: BufferedSubtitleCue[8] - zero-GC subtitle cue ring - owner: SubtitleManager
        private readonly char[][] _bufferedQueueBuffers =
        {
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters],
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters],
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters],
            new char[MaxBufferedSubtitleCharacters], new char[MaxBufferedSubtitleCharacters]
        }; // COLD ALLOC: char[8][256] - queued zero-GC subtitle text storage - owner: SubtitleManager
        private readonly TimedSubtitleCue[] _timedAudioLogCues = new TimedSubtitleCue[MaxTimedAudioLogCueCount]; // COLD ALLOC: TimedSubtitleCue[32] - parsed timed subtitle cue metadata - owner: SubtitleManager
        private readonly PendingAudioLogSubtitleEvent[] _pendingAudioLogEvents = new PendingAudioLogSubtitleEvent[MaxQueuedSubtitles]; // COLD ALLOC: PendingAudioLogSubtitleEvent[8] - value-only audio-log event phase bridge - owner: SubtitleManager
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
        private bool _runtimeOwnerAborted;
        private bool _hotSwapListenerRegistered;
        private ILocalizationStressPresentationReadModel _cachedLocalization;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ILoreDatabaseReadModel _cachedLoreDatabase;
        private uint _survivalVitalsSourceId;
        private SubtitleSource _currentSource;
        private SubtitleSource _lastEnqueuedSource;
        private float _lastEnqueueTime = -999f;
        private int _subtitleCommandQueueHead;
        private int _subtitleCommandQueueCount;
        private int _bufferedQueueHead;
        private int _bufferedQueueCount;
        private int _pendingAudioLogEventHead;
        private int _pendingAudioLogEventCount;
        private SubtitleStateDTO _currentSubtitleState;
        private uint _lastEnqueuedCommandTextHash;
        private uint _lastEnqueuedCommandSpeakerHash;
        private uint _lastGlobalSubtitleSignalFrame;
        private int _currentBufferedSubtitleLength;
        private int _lastEnqueuedBufferedSubtitleLength = -1;
        private bool _currentUsesBufferedSubtitle;
        private int _lastRenderedSubtitleLength = -1;
        private bool _tmpTypewriterActive;
        private bool _subtitleRichTextPolicyInitialized;
        private bool _subtitleRichTextEnabled;
        private bool _subtitleRightToLeftEnabled;
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
        private int _audioLogCueChangeVersion;
        private float _audioLogCueSnapshotDuration;
        private char[] _audioLogCueSnapshotBuffer;
        private int _audioLogCueSnapshotStart;
        private int _audioLogCueSnapshotLength;
        private float _audioLogCueSnapshotSpeakerIntensity;
        private float _powerTextGlitchIntensity01;
        private float _powerTextGlitchHeldTarget01;
        private float _powerTextGlitchHoldSeconds;
        private byte _powerTextGlitchBucket;
        private uint _powerTextGlitchPhase;
        private float _appliedSubtitleTextScale = 1f;
        private uint _lastUiRescaleFrame;
        private uint _lastUiRescaleSourceHash;
        private uint _lastUiRescaleFontScaleBits;
        private ushort _lastUiRescaleReason;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeInstance = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_deadSubtitleSignalLaneWarned = false;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (s_activeInstance != null)
                return;

            SuitHUDV4CanvasOverlay overlay = null;
            SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref overlay);
            Canvas targetCanvas = overlay != null
                ? overlay.TargetCanvas
                : null;
            if (targetCanvas == null && overlay != null)
                overlay.TryGetComponent(out targetCanvas);
            if (targetCanvas == null)
                return;

            SubtitleManager authoredSubtitleManager = targetCanvas.GetComponentInChildren<SubtitleManager>(true);
            if (authoredSubtitleManager != null)
            {
                s_activeInstance = authoredSubtitleManager;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameObject owner = new GameObject("SubtitleManager", typeof(RectTransform));
            owner.layer = targetCanvas.gameObject.layer;

            owner.TryGetComponent(out RectTransform rect);
            rect.SetParent(targetCanvas.transform, false);

            owner.AddComponent<SubtitleManager>();
#endif
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            s_activeInstance = this;
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (s_activeInstance == null)
                s_activeInstance = this;
            if (s_activeInstance != this)
                return;

            TryRegisterToGlobalRegistry();
            if (_runtimeOwnerAborted)
                return;

            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            BabelSubtitleSyncRuntime.EnsureInitialized();
            SignalBus<UIRescaleRequestSignal>.EnsureInitialized();
            font = LocalizedFontResolver.ResolveReadableFont(font);
            NotificationEvents.Register(this);
            AudioLogEvents.Register(this);
            EnsureBuilt();
            ApplyCurrentSettingsTextScaleCold();
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
            ClearPendingAudioLogSubtitleEvents();
            ClearTimedAudioLogState();
            _cachedPlayerContext = null;
            _survivalVitalsSourceId = 0u;

            if (s_activeInstance == this)
                s_activeInstance = null;
        }

        private void OnDestroy()
        {
            NotificationEvents.Unregister(this);
            AudioLogEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            UnregisterLateFrameSwap();
            TryUnregisterFromGlobalRegistry();
            ClearPendingAudioLogSubtitleEvents();
            ClearTimedAudioLogState();
            _cachedPlayerContext = null;
            _survivalVitalsSourceId = 0u;

            if (s_activeInstance == this)
                s_activeInstance = null;
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_serviceRegistered || !Application.isPlaying || s_activeInstance != this)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterSubtitleRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Subtitles, this);
            if (_serviceRegistered)
                RegisterToTickManager();
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (_runtimeOwnerAborted)
                return true;

            if (!Application.isPlaying)
                return false;

            SubtitleManager active = s_activeInstance;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsSubtitleRuntimeInstanceUsable(active))
                {
                    AbortDuplicateRuntimeOwner();
                    return true;
                }

                if (ReferenceEquals(s_activeInstance, active))
                    s_activeInstance = null;
                if (ReferenceEquals(GlobalRegistry.Subtitles, active))
                    GlobalRegistry.UnregisterSubtitleRuntime(active);
            }

            SubtitleManager registered = GlobalRegistry.Subtitles;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsSubtitleRegisteredRuntimeUsable(registered))
            {
                s_activeInstance = registered;
                AbortDuplicateRuntimeOwner();
                return true;
            }

            GlobalRegistry.UnregisterSubtitleRuntime(registered);
            if (ReferenceEquals(s_activeInstance, registered))
                s_activeInstance = null;
            return false;
        }

        private static bool IsSubtitleRuntimeInstanceUsable(SubtitleManager manager)
        {
            return manager != null &&
                   !manager._runtimeOwnerAborted &&
                   manager.isActiveAndEnabled;
        }

        private static bool IsSubtitleRegisteredRuntimeUsable(SubtitleManager manager)
        {
            return manager != null &&
                   manager._serviceRegistered &&
                   !manager._runtimeOwnerAborted &&
                   manager.isActiveAndEnabled;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            _runtimeOwnerAborted = true;
            NotificationEvents.Unregister(this);
            AudioLogEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            UnregisterLateFrameSwap();
            TryUnregisterFromGlobalRegistry();
            ClearPendingAudioLogSubtitleEvents();
            ClearTimedAudioLogState();
            ClearQueuedSubtitleState();
            _cachedLocalization = null;
            _cachedPlayerContext = null;
            _cachedLoreDatabase = null;
            _survivalVitalsSourceId = 0u;
            if (ReferenceEquals(s_activeInstance, this))
                s_activeInstance = null;
            enabled = false;
            Destroy(gameObject);
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
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _cachedLocalization = currentService as ILocalizationStressPresentationReadModel;
                _lastStressCorruptionBucket = int.MinValue;
                if (_isShowing && _currentSource != SubtitleSource.AudioLog)
                    RefreshStressCorruptionIfNeeded();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterFromTickManager();
                UnregisterLateFrameSwap();
                if (currentService != null && isActiveAndEnabled)
                {
                    RegisterToTickManager();
                    RegisterLateFrameSwap();
                }
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                RefreshSurvivalVitalsSourceBinding(_cachedPlayerContext);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.LoreDatabaseRuntime)
            {
                _cachedLoreDatabase = currentService as ILoreDatabaseReadModel;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault dataVault = currentService as IDataVault;
                CharBufferPool.BindDataVaultCold(dataVault);
                BabelSubtitleSyncRuntime.BindDataVaultCold(dataVault);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.SettingsRuntime)
            {
                ApplyCurrentSettingsTextScaleCold();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapListenerRegistered || !Application.isPlaying)
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
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationStressPresentation;
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
            RefreshSurvivalVitalsSourceBinding(_cachedPlayerContext);
            _cachedLoreDatabase = Hecton8.Core.GlobalRegistry.LoreDatabaseReadModel;
            IDataVault dataVault = Hecton8.Core.GlobalRegistry.DataVault;
            CharBufferPool.BindDataVaultCold(dataVault);
            BabelSubtitleSyncRuntime.BindDataVaultCold(dataVault);
        }

        private void RefreshSurvivalVitalsSourceBinding(IPlayerRuntimeContext playerContext)
        {
            HectonSurvivalSystem survival = playerContext != null && playerContext.IsInitialized
                ? playerContext.SurvivalSystem
                : null;
            _survivalVitalsSourceId = survival != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(survival.GetEntityId()))
                : 0u;
        }

        /// <summary>
        /// Displays a caller-owned subtitle span through the zero-GC char-buffer path.
        /// </summary>
        /// <param name="text">Subtitle text. The span is copied before the method returns.</param>
        /// <param name="duration">Display duration in seconds.</param>
        /// <returns>True when a non-empty subtitle was accepted.</returns>
        public bool DisplaySubtitle(ReadOnlySpan<char> text, float duration)
        {
            if (_runtimeOwnerAborted)
                return false;

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
            return DisplaySubtitleResolved(textHash, default, duration, formatArgs, false);
        }

        /// <summary>
        /// Resolves a Babel hash and falls back to caller-owned text when the token is absent.
        /// </summary>
        public bool DisplaySubtitle(uint textHash, ReadOnlySpan<char> fallback, float duration)
        {
            return DisplaySubtitleResolved(textHash, fallback, duration, default, true);
        }

        /// <summary>
        /// Resolves a Babel hash with numeric ^0..^3 replacements and a caller-owned fallback span.
        /// </summary>
        public bool DisplaySubtitle(uint textHash, ReadOnlySpan<char> fallback, float duration, BabelFormatArgs formatArgs)
        {
            return DisplaySubtitleResolved(textHash, fallback, duration, formatArgs, true);
        }

        private bool DisplaySubtitleResolved(
            uint textHash,
            ReadOnlySpan<char> fallback,
            float duration,
            BabelFormatArgs formatArgs,
            bool allowFallback)
        {
            if (_runtimeOwnerAborted)
                return false;

            if (textHash == 0u)
                return allowFallback && EnqueueBuffered(fallback, duration, SubtitleSource.Generic, false);

            if (!CharBufferPool.TryAcquireBabel(out CharBufferPool.BabelLease lease))
            {
                if (allowFallback && fallback.Length > 0)
                    return EnqueueBuffered(fallback, duration, SubtitleSource.Generic, false);

                return TryResolveVocalWarningFallbackSubtitle(textHash, out ReadOnlySpan<char> vocalWarningFallback) &&
                       EnqueueBuffered(vocalWarningFallback, duration, SubtitleSource.Generic, false);
            }

            int length = 0;
            bool found = false;
            long decodeStart = Stopwatch.GetTimestamp();
            float decodeMs = 0f;
            try
            {
                found = LocRegistry.TryWriteVisualSpanFromUtf8(
                    textHash,
                    lease.Span,
                    out length,
                    formatArgs,
                    ShouldStripBabelRichText(textHash));
                decodeMs = ResolveStopwatchElapsedMilliseconds(decodeStart);

                if (!found && allowFallback && fallback.Length > 0)
                    length = CopyFallbackSpanToBabelLease(textHash, fallback, lease.Span);
                else if (!found && TryResolveVocalWarningFallbackSubtitle(textHash, out ReadOnlySpan<char> vocalWarningFallback))
                    length = CopyFallbackSpanToBabelLease(textHash, vocalWarningFallback, lease.Span);

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

        private static int CopyFallbackSpanToBabelLease(uint textHash, ReadOnlySpan<char> fallback, Span<char> destination)
        {
            if (fallback.Length <= 0 || destination.Length <= 0)
                return 0;

            int safeLength = math.min(fallback.Length, destination.Length);
            for (int i = 0; i < safeLength; i++)
                destination[i] = fallback[i];

            if (safeLength < fallback.Length)
            {
                BabelSubtitleSyncRuntime.RecordUIOptimizationFailure(
                    textHash,
                    UIOptimizationFailureCode.TextBufferOverflow,
                    fallback.Length,
                    safeLength,
                    destination.Length);
            }

            return safeLength;
        }

        /// <summary>
        /// Displays a Babel subtitle command without creating a managed string.
        /// </summary>
        public bool DisplaySubtitle(in SubtitleCommandDTO command, bool interrupt = false)
        {
            if (_runtimeOwnerAborted)
                return false;

            if (command.TextHash == 0u)
                return false;

            if (!_built || _subtitleText == null || _canvasGroup == null)
                return false;

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
                return DisplaySubtitle(unchecked((uint)keyHash), fallback, duration);

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

        private void AdvanceSubtitlePresentation(float deltaTime)
        {
            if (_runtimeOwnerAborted)
                return;

            if (_root == null)
                return;

            BabelSubtitleSyncRuntime.PreparePresentationFrame();
            bool powerGlitchChanged = RefreshPowerTextGlitch(deltaTime);
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
                    else if (TryDequeueBufferedSubtitle(out BufferedSubtitleCue bufferedNext))
                    {
                        ShowImmediate(bufferedNext);
                    }
                    else
                    {
                        ApplySubtitleBuffer(0);
                    }
                }
            }

            if (_isShowing)
                RefreshStressCorruptionIfNeeded(powerGlitchChanged);

            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;

            if (_audioCueGroup != null)
                _audioCueGroup.alpha = _currentSource == SubtitleSource.AudioLog ? _currentAlpha : 0f;
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            ConsumeUiRescaleRequestsVisualSync();
            DrainPendingAudioLogEventsVisualSync();
            AdvanceSubtitlePresentation(SystemDispatcher.CurrentFrameUnscaledDeltaTime);

            if (!_subtitleSwapPending)
                return;

            FlushPendingSubtitleSwap();
        }

        private void ConsumeUiRescaleRequestsVisualSync()
        {
            ReadOnlySpan<UIRescaleRequestSignal> signals = SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            float scale = _appliedSubtitleTextScale;
            bool hasRequest = false;
            for (int i = 0; i < signals.Length; i++)
            {
                UIRescaleRequestSignal signal = signals[i];
                uint fontScaleBits = math.asuint(signal.FontScale);
                if (signal.Frame == _lastUiRescaleFrame &&
                    signal.SourceHash == _lastUiRescaleSourceHash &&
                    fontScaleBits == _lastUiRescaleFontScaleBits &&
                    signal.Reason == _lastUiRescaleReason)
                {
                    continue;
                }

                _lastUiRescaleFrame = signal.Frame;
                _lastUiRescaleSourceHash = signal.SourceHash;
                _lastUiRescaleFontScaleBits = fontScaleBits;
                _lastUiRescaleReason = signal.Reason;
                scale = ResolveSubtitleTextScale(signal.FontScale);
                hasRequest = true;
            }

            if (hasRequest)
                ApplySubtitleTextScaleVisualSync(scale);
        }

        private void ApplyCurrentSettingsTextScaleCold()
        {
            float scale = 1f;
            if (SettingsManager.TryGetInstance(out SettingsManager settings))
                scale = settings.TextScale;

            ApplySubtitleTextScaleVisualSync(ResolveSubtitleTextScale(scale));
        }

        private static float ResolveSubtitleTextScale(float requestedScale)
        {
            float scale = math.isfinite(requestedScale) && requestedScale > 0f ? requestedScale : 1f;
            return math.clamp(scale, AccessibilitySettings.MinimumTextScale, AccessibilitySettings.MaximumTextScale);
        }

        private void ApplySubtitleTextScaleVisualSync(float scale)
        {
            if (_subtitleText == null)
                return;

            float safeScale = ResolveSubtitleTextScale(scale);
            if (math.abs(safeScale - _appliedSubtitleTextScale) <= 0.001f)
                return;

            _appliedSubtitleTextScale = safeScale;
            float fontSize = SubtitleBaseFontSize * safeScale;
            float minimumFontSize = SubtitleMinimumFontSize * safeScale;
            _subtitleText.fontSize = fontSize;
            _subtitleText.enableAutoSizing = true;
            _subtitleText.fontSizeMin = minimumFontSize;
            _subtitleText.fontSizeMax = math.max(minimumFontSize, fontSize);
            _subtitleText.overflowMode = TextOverflowModes.Ellipsis;
            _subtitleText.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>
        /// Reads the latest audio-log cue snapshot without subscribing a managed callback.
        /// </summary>
        public bool TryGetAudioLogCueSnapshot(
            int lastSeenVersion,
            out int version,
            out float duration,
            out char[] textBuffer,
            out int textStart,
            out int textLength,
            out float speakerIntensity)
        {
            if (_runtimeOwnerAborted)
            {
                version = lastSeenVersion;
                duration = 0f;
                textBuffer = EmptyCueBuffer;
                textStart = 0;
                textLength = 0;
                speakerIntensity = 0f;
                return false;
            }

            version = _audioLogCueChangeVersion;
            if (version == lastSeenVersion)
            {
                duration = 0f;
                textBuffer = EmptyCueBuffer;
                textStart = 0;
                textLength = 0;
                speakerIntensity = 0f;
                return false;
            }

            duration = _audioLogCueSnapshotDuration;
            textBuffer = _audioLogCueSnapshotBuffer ?? EmptyCueBuffer;
            int safeStart = Mathf.Clamp(_audioLogCueSnapshotStart, 0, textBuffer.Length);
            int safeLength = Mathf.Clamp(_audioLogCueSnapshotLength, 0, textBuffer.Length - safeStart);
            textStart = safeStart;
            textLength = safeLength;
            speakerIntensity = math.saturate(_audioLogCueSnapshotSpeakerIntensity);
            return true;
        }

        public void OnNotificationEvent(in NotificationEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if (!NotificationEvents.TryResolveMessageSpan(payload.MessageHash, out ReadOnlySpan<char> message))
                return;

            HandleNotificationPushed(message, payload.Severity);
        }

        private void HandleNotificationPushed(ReadOnlySpan<char> message, ushort severity)
        {
            EnqueueBuffered(message, defaultDuration, SubtitleSource.Notification, false);
        }

        public void OnAudioLogEvent(in AudioLogEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            switch (payload.Type)
            {
                case AudioLogEventType.PlaybackStarted:
                    QueueAudioLogSubtitleEvent(in payload);
                    return;

                case AudioLogEventType.PlaybackStopped:
                case AudioLogEventType.PlaybackCompleted:
                    QueueAudioLogSubtitleEvent(in payload);
                    return;
            }
        }

        private void QueueAudioLogSubtitleEvent(in AudioLogEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            int capacity = _pendingAudioLogEvents.Length;
            if (capacity <= 0)
                return;

            if (_pendingAudioLogEventCount >= capacity)
            {
                _pendingAudioLogEvents[_pendingAudioLogEventHead].Clear();
                _pendingAudioLogEventHead = (_pendingAudioLogEventHead + 1) & BufferedQueueMask;
                _pendingAudioLogEventCount--;
            }

            int slot = (_pendingAudioLogEventHead + _pendingAudioLogEventCount) & BufferedQueueMask;
            _pendingAudioLogEvents[slot].Type = payload.Type;
            _pendingAudioLogEvents[slot].LogHash = payload.LogHash;
            _pendingAudioLogEvents[slot].DurationSeconds = SanitizeAudioLogEventDuration(payload.DurationSeconds);
            _pendingAudioLogEventCount++;
        }

        private void DrainPendingAudioLogEventsVisualSync()
        {
            if (_runtimeOwnerAborted)
                return;

            int guard = _pendingAudioLogEventCount;
            while (guard-- > 0 && TryDequeuePendingAudioLogEvent(out PendingAudioLogSubtitleEvent pending))
            {
                switch (pending.Type)
                {
                    case AudioLogEventType.PlaybackStarted:
                        HandleAudioLogPlaybackStarted(pending.LogHash, pending.DurationSeconds);
                        break;

                    case AudioLogEventType.PlaybackStopped:
                    case AudioLogEventType.PlaybackCompleted:
                        HandleAudioLogPlaybackEnded(pending.LogHash);
                        break;
                }
            }
        }

        private bool TryDequeuePendingAudioLogEvent(out PendingAudioLogSubtitleEvent pending)
        {
            if (_pendingAudioLogEventCount <= 0)
            {
                pending = default;
                return false;
            }

            int slot = _pendingAudioLogEventHead;
            pending = _pendingAudioLogEvents[slot];
            _pendingAudioLogEvents[slot].Clear();
            _pendingAudioLogEventHead = (_pendingAudioLogEventHead + 1) & BufferedQueueMask;
            _pendingAudioLogEventCount--;
            return true;
        }

        private void ClearPendingAudioLogSubtitleEvents()
        {
            int count = _pendingAudioLogEventCount;
            int head = _pendingAudioLogEventHead;
            for (int i = 0; i < count; i++)
            {
                int slot = (head + i) & BufferedQueueMask;
                _pendingAudioLogEvents[slot].Clear();
            }

            _pendingAudioLogEventHead = 0;
            _pendingAudioLogEventCount = 0;
        }

        private void ClearQueuedSubtitleState()
        {
            for (int i = 0; i < _subtitleCommandQueue.Length; i++)
                _subtitleCommandQueue[i] = default;

            for (int i = 0; i < _bufferedQueue.Length; i++)
                _bufferedQueue[i] = default;

            _subtitleCommandQueueHead = 0;
            _subtitleCommandQueueCount = 0;
            _bufferedQueueHead = 0;
            _bufferedQueueCount = 0;
            _timer = 0f;
            _currentAlpha = 0f;
            _isShowing = false;
            _currentSource = SubtitleSource.Generic;
            _lastEnqueuedSource = SubtitleSource.Generic;
            _lastEnqueueTime = -999f;
            _currentSubtitleState = default;
            _lastEnqueuedCommandTextHash = 0u;
            _lastEnqueuedCommandSpeakerHash = 0u;
            _currentBufferedSubtitleLength = 0;
            _lastEnqueuedBufferedSubtitleLength = -1;
            _currentUsesBufferedSubtitle = false;
            _lastRenderedSubtitleLength = -1;
            _tmpTypewriterActive = false;
            _tmpTypewriterElapsed = 0f;
            _tmpTypewriterTargetCharacters = 0;
            _subtitleSwapPending = false;
            _pendingSubtitleSwapLength = -1;
            _currentSubtitleStartAudioFrame = 0u;
            _currentSubtitleDurationFrames = 0u;
            _tmpTypewriterStartAudioFrame = 0u;
            _powerTextGlitchIntensity01 = 0f;
            _powerTextGlitchHeldTarget01 = 0f;
            _powerTextGlitchHoldSeconds = 0f;
            _powerTextGlitchBucket = 0;
            _powerTextGlitchPhase = 0u;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            if (_audioCueGroup != null)
                _audioCueGroup.alpha = 0f;
        }

        private static float SanitizeAudioLogEventDuration(float durationSeconds)
        {
            if (!math.isfinite(durationSeconds) || durationSeconds <= 0f)
                return 0f;

            return math.min(durationSeconds, 86400f);
        }

        private void HandleAudioLogPlaybackStarted(uint loreHash, float durationSeconds)
        {
            ClearTimedAudioLogState();
            if (!TryPrepareAudioLogBuffers(loreHash, durationSeconds, out int initialRenderLength))
                return;

            RegisterToTickManager();
            _currentSource = SubtitleSource.AudioLog;
            _currentUsesBufferedSubtitle = false;
            _currentBufferedSubtitleLength = 0;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Announced before the frame-dedupe guard below on purpose: if BabelSubtitleSyncRuntime never
            // initializes, both CurrentPresentationFrame and CurrentAudioFrame stay 0, the guard returns every
            // frame, and an advisory placed after it would itself be dead code. See the dead-lane block at the
            // SignalBus<SubtitleSignal> read further down for the field mapping and the owner decision needed.
            if (!s_deadSubtitleSignalLaneWarned)
            {
                s_deadSubtitleSignalLaneWarned = true;
                Hecton8.Core.H8Debug.LogWarning(
                    "[SubtitleManager] DEAD SIGNAL LANE: DrainGlobalSubtitleSignals reads SignalBus<SubtitleSignal>, but SubtitleSignal has no producer anywhere in the scripts tree - GlobalSignals.Publish(in SubtitleSignal) (Core/Signals/GlobalSignals.LegacyFacade.cs:324) is never called and nothing else constructs one. The frame snapshot is therefore always empty and NO subtitle is ever displayed through this path. The live lane is SubtitleCueSignal (Core/Contracts/Signals/SubtitleCueSignal.cs:9), pushed by Audio/VocalWarningSystem.cs:1626, Audio/Synthesis/VocalBankPlaybackRuntime.cs:1159 and UI/BabelSubtitleSyncRuntime.cs:408, and it still reaches this manager via DrainBabelCueSignals - so treat this as one dead legacy entry point, not as broken subtitles. Migrating this reader is NOT a type swap: it needs an owner decision on timing semantics (DurationSeconds float -> DurationMilliseconds ushort clamp/rounding, and presentation Frame -> audio-clock StartAudioFrame/AudioFrameLatency). See the field-mapping table at the read site.",
                    this);
            }
#endif
            BabelSubtitleSyncRuntime.PreparePresentationFrame();
            uint frame = BabelSubtitleSyncRuntime.CurrentPresentationFrame;
            if (frame == 0u)
                frame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
            if (_lastGlobalSubtitleSignalFrame == frame)
                return;

            _lastGlobalSubtitleSignalFrame = frame;

            // DEAD LANE - THIS LOOP HAS NEVER EXECUTED. SignalBus<SubtitleSignal> has no producer anywhere in
            // the scripts tree, so GetFrameSnapshot below is permanently empty and no subtitle has ever been
            // displayed through this path. Verified producers-of-SubtitleSignal set is empty:
            //   - GlobalSignals.Publish(in SubtitleSignal)  Core/Signals/GlobalSignals.LegacyFacade.cs:324
            //     is the only push API and has zero call sites.
            //   - GlobalSignals.TryDequeueSubtitle          Core/Signals/GlobalSignals.LegacyFacade.cs:1093
            //     is the only other reader and also has zero call sites.
            //   - Core/Signals/GlobalSignals.RuntimeLifecycle.cs:62 registers the lane and :139 size-checks it;
            //     Core/Signals/SignalBusRuntime.cs:1698 only classifies its pause-flush behaviour. Neither
            //     constructs a payload. Docs/Generated/DEPENDENCY_GRAPH.md:686 independently lists producers as
            //     "none found".
            // The LIVE lane is SubtitleCueSignal (Core/Contracts/Signals/SubtitleCueSignal.cs:9), pushed by
            // Audio/VocalWarningSystem.cs:1626, Audio/Synthesis/VocalBankPlaybackRuntime.cs:1159 and
            // UI/BabelSubtitleSyncRuntime.cs:408. It already reaches this manager through
            // BabelSubtitleSyncRuntime.DrainCueSignals -> TryConsumeReadyCue in DrainBabelCueSignals below, so
            // subtitles are NOT globally broken - only this legacy entry point is.
            //
            // MIGRATION IS NOT A TYPE SWAP. It needs an OWNER DECISION on subtitle timing, which is
            // player-visible and not settleable statically. Field mapping:
            //   dead SubtitleSignal (32 B)          | live SubtitleCueSignal (64 B)
            //   ------------------------------------+-------------------------------------------
            //   SubtitleHash        uint   @0       | TokenHash             uint   @0
            //   SpeakerHash         uint   @4       | SourceHash            uint   @4
            //   DurationSeconds     FLOAT  @8       | DurationMilliseconds  USHORT @16
            //   Frame               uint   @12      | StartAudioFrame       uint   @8
            //   (no equivalent)                     | AudioFrameLatency     uint   @12
            //   Priority            byte   @16      | Priority              byte   @18
            //   Flags               byte   @17      | Flags                 byte   @19
            // Two unresolved semantics: (1) seconds->milliseconds must clamp to ushort, which caps duration at
            // 65.535 s and quantises it - decide the clamp and the rounding; (2) Frame is a presentation frame
            // here but StartAudioFrame/AudioFrameLatency are audio-clock quantities in the live lane, so cue
            // scheduling and lip-sync offset change meaning. Do not guess either one.
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
                    SpeakerHash = cue.SourceHash,
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
            if (_runtimeOwnerAborted)
                return false;

            if (!CharBufferPool.TryAcquireBabel(out CharBufferPool.BabelLease lease))
                return false;

            try
            {
                Span<char> destination = lease.Span;
                bool stripRichText = ShouldStripBabelRichText(command.TextHash);
                bool allowRichPrefix = BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing();
                int prefixLength = 0;
                AppendSpeakerPrefix(command.SpeakerHash, allowRichPrefix, destination, ref prefixLength);
                Span<char> textDestination = destination.Slice(prefixLength);
                long decodeStart = Stopwatch.GetTimestamp();
                bool found = LocRegistry.TryWriteVisualSpanFromUtf8(
                    command.TextHash,
                    textDestination,
                    out int textLength,
                    stripRichText);
                float decodeMs = ResolveStopwatchElapsedMilliseconds(decodeStart);
                if (!found && TryResolveVocalWarningFallbackSubtitle(command.TextHash, out ReadOnlySpan<char> fallback))
                    textLength = CopyFallbackSpanToBabelLease(command.TextHash, fallback, textDestination);

                BabelSubtitleSyncRuntime.RecordDecode(command.TextHash, textLength, !found, decodeMs);
                if (textLength <= 0)
                    return false;

                int length = prefixLength + textLength;
                AppendDirectionalArrow(command._pad0, destination, ref length);
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
                    Flags = (ushort)((stripRichText ? 1u : 0u) | (command._pad0 & ushort.MaxValue))
                };
                return true;
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        private static bool TryResolveVocalWarningFallbackSubtitle(uint textHash, out ReadOnlySpan<char> fallback)
        {
            switch (textHash)
            {
                case VocalWarningHashes.CrushDepth:
                    fallback = "CRUSH DEPTH".AsSpan();
                    return true;
                case VocalWarningHashes.HullBreach:
                case VocalWarningHashes.HullTempCritical:
                    fallback = "HULL BREACH".AsSpan();
                    return true;
                case VocalWarningHashes.OxygenLow:
                    fallback = "OXYGEN LOW".AsSpan();
                    return true;
                case VocalWarningHashes.Radiation:
                    fallback = "RADIATION".AsSpan();
                    return true;
                case VocalWarningHashes.PowerLow:
                    fallback = "POWER LOW".AsSpan();
                    return true;
                case VocalWarningHashes.Toxicity:
                    fallback = "TOXIC EXPOSURE".AsSpan();
                    return true;
                default:
                    fallback = default;
                    return false;
            }
        }

        private static void AppendSpeakerPrefix(uint speakerHash, bool allowRichText, Span<char> destination, ref int length)
        {
            if (speakerHash == 0u || (uint)length >= (uint)destination.Length)
                return;

            if (destination.Length - length < ResolveSpeakerPrefixLength(speakerHash, allowRichText))
                return;

            if (allowRichText)
                AppendSpeakerColorOpen(speakerHash, destination, ref length);

            AppendSpeakerLabel(speakerHash, destination, ref length);

            if (allowRichText)
                AppendLiteral("</color>".AsSpan(), destination, ref length);

            AppendLiteral(" ".AsSpan(), destination, ref length);
        }

        private static int ResolveSpeakerPrefixLength(uint speakerHash, bool allowRichText)
        {
            int labelLength = ResolveSpeakerLabelLength(speakerHash);
            return allowRichText
                ? 15 + labelLength + 8 + 1
                : labelLength + 1;
        }

        private static int ResolveSpeakerLabelLength(uint speakerHash)
        {
            return speakerHash == SubtitleSpeakerHashBabel ? 8 : 6;
        }

        private static void AppendSpeakerColorOpen(uint speakerHash, Span<char> destination, ref int length)
        {
            switch (speakerHash)
            {
                case SubtitleSpeakerHashVocalWarning:
                case SubtitleSpeakerHashVocalWarningSystem:
                    AppendLiteral("<color=#FFB547>".AsSpan(), destination, ref length);
                    return;
                case SubtitleSpeakerHashBabel:
                    AppendLiteral("<color=#8FE8FF>".AsSpan(), destination, ref length);
                    return;
                case SubtitleSpeakerHashWfcAudioLog:
                    AppendLiteral("<color=#B7FFC8>".AsSpan(), destination, ref length);
                    return;
                default:
                    AppendLiteral("<color=#D7F4FF>".AsSpan(), destination, ref length);
                    return;
            }
        }

        private static void AppendSpeakerLabel(uint speakerHash, Span<char> destination, ref int length)
        {
            switch (speakerHash)
            {
                case SubtitleSpeakerHashVocalWarning:
                case SubtitleSpeakerHashVocalWarningSystem:
                    AppendLiteral("[VWS]:".AsSpan(), destination, ref length);
                    return;
                case SubtitleSpeakerHashBabel:
                    AppendLiteral("[BABEL]:".AsSpan(), destination, ref length);
                    return;
                case SubtitleSpeakerHashWfcAudioLog:
                    AppendLiteral("[LOG]:".AsSpan(), destination, ref length);
                    return;
                default:
                    AppendLiteral("[COM]:".AsSpan(), destination, ref length);
                    return;
            }
        }

        private static void AppendLiteral(ReadOnlySpan<char> literal, Span<char> destination, ref int length)
        {
            int safeLength = math.min(literal.Length, math.max(0, destination.Length - length));
            for (int i = 0; i < safeLength; i++)
                destination[length + i] = literal[i];

            length += safeLength;
        }

        private static float ResolveStopwatchElapsedMilliseconds(long startTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks <= 0L)
                return 0f;

            return (float)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
        }

        private bool EnqueueBuffered(ReadOnlySpan<char> message, float duration, SubtitleSource source, bool interrupt)
        {
            if (_runtimeOwnerAborted)
                return false;

            if (!_built || _subtitleText == null || _canvasGroup == null)
                return false;

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

        private void ShowImmediate(BufferedSubtitleCue request)
        {
            if (_runtimeOwnerAborted)
                return;

            int bufferIndex = Mathf.Clamp(request.BufferIndex, 0, _bufferedQueueBuffers.Length - 1);
            int safeLength = Mathf.Clamp(request.Length, 0, MaxBufferedSubtitleCharacters);
            ShowImmediate(_bufferedQueueBuffers[bufferIndex].AsSpan(0, safeLength), request.Duration, request.Source);
        }

        private void ShowImmediate(ReadOnlySpan<char> message, float duration, SubtitleSource source)
        {
            if (_runtimeOwnerAborted)
                return;

            int safeLength = CopySpanToBuffer(message, _currentBufferedSubtitleBuffer);
            _currentBufferedSubtitleLength = safeLength;
            _currentUsesBufferedSubtitle = safeLength > 0;
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

        private bool TryDequeueBufferedSubtitle(out BufferedSubtitleCue request)
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
            _bufferedQueue[slot] = new BufferedSubtitleCue
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
            ILocalizationStressPresentationReadModel manager = _cachedLocalization;
            int renderLength;
            if (source != SubtitleSource.AudioLog &&
                manager != null &&
                manager.TryApplyHullStressCorruptionIfNeeded(sourceSpan, _subtitleRenderBuffer, out int corruptedLength))
            {
                renderLength = Mathf.Clamp(corruptedLength, 0, _subtitleRenderBuffer.Length);
                return ApplyPowerTextGlitchIfNeeded(renderLength);
            }

            renderLength = CopySpanToBuffer(sourceSpan, _subtitleRenderBuffer);
            return ApplyPowerTextGlitchIfNeeded(renderLength);
        }

        private bool RefreshPowerTextGlitch(float deltaTime)
        {
            float safeDelta = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            float signalTarget = ResolvePowerTextGlitchSignalTarget01();
            if (signalTarget > 0f)
            {
                _powerTextGlitchHeldTarget01 = math.max(_powerTextGlitchHeldTarget01, signalTarget);
                _powerTextGlitchHoldSeconds = math.lerp(0.35f, 1.15f, SmoothPowerTextGlitch01(signalTarget));
            }
            else if (_powerTextGlitchHoldSeconds > 0f)
            {
                _powerTextGlitchHoldSeconds = math.max(0f, _powerTextGlitchHoldSeconds - safeDelta);
            }
            else
            {
                _powerTextGlitchHeldTarget01 = 0f;
            }

            float targetIntensity = math.max(signalTarget, _powerTextGlitchHeldTarget01);
            float previousIntensity = _powerTextGlitchIntensity01;
            float speed = targetIntensity > previousIntensity ? PowerTextGlitchRiseSpeed : PowerTextGlitchDecaySpeed;
            _powerTextGlitchIntensity01 = math.lerp(
                previousIntensity,
                targetIntensity,
                FastDecayBlend(speed, safeDelta));

            byte previousBucket = _powerTextGlitchBucket;
            _powerTextGlitchBucket = EncodePowerTextGlitchBucket(_powerTextGlitchIntensity01);
            if (_powerTextGlitchBucket == 0)
                return previousBucket != 0;

            _powerTextGlitchPhase++;
            int cadenceFrames = ResolvePowerTextGlitchCadenceFrames(_powerTextGlitchIntensity01);
            return previousBucket != _powerTextGlitchBucket ||
                   cadenceFrames <= 1 ||
                   (_powerTextGlitchPhase % (uint)cadenceFrames) == 0u;
        }

        private float ResolvePowerTextGlitchSignalTarget01()
        {
            float target = 0f;
            ReadOnlySpan<BatteryLevelSignal> batterySignals = SignalBus<BatteryLevelSignal>.GetFrameSnapshot();
            for (int i = 0; i < batterySignals.Length; i++)
            {
                BatteryLevelSignal signal = batterySignals[i];
                byte percent = signal.BatteryPercent > 100 ? (byte)100 : signal.BatteryPercent;
                if (percent >= PowerTextGlitchBatteryThresholdPercent)
                    continue;

                float severity = (PowerTextGlitchBatteryThresholdPercent - percent) *
                                 (1f / PowerTextGlitchBatteryThresholdPercent);
                target = math.max(target, severity);
            }

            uint survivalVitalsSourceId = _survivalVitalsSourceId;
            if (survivalVitalsSourceId != 0u)
            {
                ReadOnlySpan<SurvivalVitalsChangedSignal> vitalSignals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
                for (int i = 0; i < vitalSignals.Length; i++)
                {
                    SurvivalVitalsChangedSignal signal = vitalSignals[i];
                    if (signal.SourceId != survivalVitalsSourceId ||
                        (signal.Flags & SurvivalVitalsChangedSignalFlags.Energy) == 0u)
                    {
                        continue;
                    }

                    float energy = math.saturate(math.select(0f, signal.Energy01, math.isfinite(signal.Energy01)));
                    if (energy >= PowerTextGlitchEnergyThreshold01)
                        continue;

                    float severity = (PowerTextGlitchEnergyThreshold01 - energy) *
                                     (1f / PowerTextGlitchEnergyThreshold01);
                    target = math.max(target, severity);
                }
            }

            return math.saturate(target);
        }

        private int ApplyPowerTextGlitchIfNeeded(int renderLength)
        {
            int safeLength = Mathf.Clamp(renderLength, 0, _subtitleRenderBuffer.Length);
            if (safeLength <= 0 || _powerTextGlitchBucket == 0)
                return safeLength;

            float intensity = math.saturate(_powerTextGlitchIntensity01);
            int mutationBudget = ResolvePowerTextGlitchMutationBudget(safeLength, intensity);
            if (mutationBudget <= 0)
                return safeLength;

            ReadOnlySpan<char> renderSpan = _subtitleRenderBuffer.AsSpan(0, safeLength);
            int candidateCount = CountPowerTextGlitchCandidates(renderSpan);
            if (candidateCount <= 0)
                return safeLength;

            mutationBudget = math.min(mutationBudget, candidateCount);
            uint seed = MixPowerTextGlitch((uint)safeLength ^
                                           (_powerTextGlitchPhase * 0x9E3779B9u) ^
                                           ((uint)_powerTextGlitchBucket * 0x85EBCA6Bu));
            int remainingCandidates = candidateCount;
            int remainingMutations = mutationBudget;
            bool insideRichTextTag = false;
            for (int index = 0; index < safeLength && remainingMutations > 0; index++)
            {
                char existing = _subtitleRenderBuffer[index];
                if (existing == '<')
                {
                    insideRichTextTag = true;
                    continue;
                }

                if (insideRichTextTag)
                {
                    if (existing == '>')
                        insideRichTextTag = false;
                    continue;
                }

                if (!IsPowerTextGlitchMutableGlyph(existing))
                    continue;

                seed = MixPowerTextGlitch(seed ^ ((uint)index * 0x632BE59Bu) ^ (uint)remainingCandidates);
                if ((seed % (uint)remainingCandidates) < (uint)remainingMutations)
                {
                    _subtitleRenderBuffer[index] = ResolvePowerTextGlitchGlyph(seed);
                    remainingMutations--;
                }

                remainingCandidates--;
            }

            return safeLength;
        }

        private static int CountPowerTextGlitchCandidates(ReadOnlySpan<char> renderSpan)
        {
            int count = 0;
            bool insideRichTextTag = false;
            for (int i = 0; i < renderSpan.Length; i++)
            {
                char value = renderSpan[i];
                if (value == '<')
                {
                    insideRichTextTag = true;
                    continue;
                }

                if (insideRichTextTag)
                {
                    if (value == '>')
                        insideRichTextTag = false;
                    continue;
                }

                if (IsPowerTextGlitchMutableGlyph(value))
                    count++;
            }

            return count;
        }

        private static bool IsPowerTextGlitchMutableGlyph(char value)
        {
            return value != ' ' &&
                   value != '\n' &&
                   value != '\r' &&
                   value != '\t' &&
                   value != '<' &&
                   value != '>';
        }

        private static int ResolvePowerTextGlitchMutationBudget(int length, float intensity01)
        {
            float quality = SmoothPowerTextGlitch01(ResolveSubtitleQualityWeight01());
            float mutationRate = math.lerp(0.018f, PowerTextGlitchMaximumMutationRate, quality) *
                                 SmoothPowerTextGlitch01(intensity01);
            return math.clamp((int)math.ceil(length * mutationRate), 1, length);
        }

        private static int ResolvePowerTextGlitchCadenceFrames(float intensity01)
        {
            float quality = SmoothPowerTextGlitch01(ResolveSubtitleQualityWeight01());
            float intensity = SmoothPowerTextGlitch01(intensity01);
            float cadence = math.lerp(9f, 2f, quality) * math.lerp(1.35f, 0.65f, intensity);
            return math.clamp((int)math.round(cadence), 1, 12);
        }

        private static byte EncodePowerTextGlitchBucket(float intensity01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(intensity01) * 15f), 0, 15);
        }

        private static float ResolveSubtitleQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static float SmoothPowerTextGlitch01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        private static uint MixPowerTextGlitch(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static char ResolvePowerTextGlitchGlyph(uint seed)
        {
            switch (seed % 8u)
            {
                case 0u: return '#';
                case 1u: return '%';
                case 2u: return '/';
                case 3u: return '\\';
                case 4u: return '|';
                case 5u: return '_';
                case 6u: return '-';
                default: return '*';
            }
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

        private static bool ShouldStripBabelRichText(uint textHash)
        {
            return BabelRichTextLodPolicy.ShouldStrip(textHash);
        }

        private void RefreshSubtitleTextLodPolicy()
        {
            if (_subtitleText == null)
                return;

            bool enableRichText = BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing();
            bool enableRightToLeft = LocalizationManager.IsRightToLeftLanguage(LocRegistry.ActiveLanguage);
            if (_subtitleRichTextPolicyInitialized &&
                _subtitleRichTextEnabled == enableRichText &&
                _subtitleRightToLeftEnabled == enableRightToLeft)
            {
                return;
            }

            _subtitleText.richText = enableRichText;
            _subtitleText.isRightToLeftText = enableRightToLeft;
            _subtitleRichTextEnabled = enableRichText;
            _subtitleRightToLeftEnabled = enableRightToLeft;
            _subtitleRichTextPolicyInitialized = true;
        }

        private void RefreshStressCorruptionIfNeeded()
        {
            RefreshStressCorruptionIfNeeded(false);
        }

        private void RefreshStressCorruptionIfNeeded(bool forceVisualRefresh)
        {
            bool allowHullStress = _currentSource != SubtitleSource.AudioLog;
            if (!allowHullStress && !forceVisualRefresh)
                return;

            ILocalizationStressPresentationReadModel manager = allowHullStress ? _cachedLocalization : null;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (!forceVisualRefresh && stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            if (_currentUsesBufferedSubtitle)
            {
                ApplySubtitleBuffer(CopyBufferedDisplayToRenderBuffer(_currentSource));
            }
        }

        private bool TryPrepareAudioLogBuffers(uint loreHash, float durationSeconds, out int initialRenderLength)
        {
            initialRenderLength = 0;
            ILoreDatabaseReadModel database = _cachedLoreDatabase;
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
            _audioLogCueChangeVersion++;
            if (_audioLogCueChangeVersion == 0)
                _audioLogCueChangeVersion = 1;

            _audioLogCueSnapshotDuration = math.max(0f, duration);
            _audioLogCueSnapshotBuffer = textBuffer ?? EmptyCueBuffer;
            int safeStart = Mathf.Clamp(textStart, 0, _audioLogCueSnapshotBuffer.Length);
            _audioLogCueSnapshotStart = safeStart;
            _audioLogCueSnapshotLength = Mathf.Clamp(textLength, 0, _audioLogCueSnapshotBuffer.Length - safeStart);
            _audioLogCueSnapshotSpeakerIntensity = math.saturate(speakerIntensity);
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

            PhysicsEventPayload payload = default;
            payload.RuntimePosition = runtimePosition;
            payload.Direction = direction;
            payload.ForceVector = default;
            payload.ImpulseVector = default;
            payload.RadiusMeters = radiusMeters;
            payload.Scalar0 = energyJoules;
            payload.Scalar1 = volume01;
            payload.Scalar2 = 1f;
            payload.PrimaryId = 0;
            payload.DataHash = 0u;
            payload.StatusBits = 0u;
            payload.EventType = (ushort)PhysicsEventType.AcousticImpulse;
            payload.Reserved = 0;
            SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref s_x001SubtitleManagerSignalPushDropCount);

            CameraJuiceSignals.TryPublishImpact(
                math.min(AudioLogCueMaximumCameraShake, intensity * 0.12f),
                runtimePosition,
                direction,
                CameraJuiceSignals.HighFreqToolVibrationProfileHash,
                AudioLogCueCameraAmplitudeScale,
                CameraJuiceSignals.LowPriority,
                radiusMeters,
                AudioLogCueCameraTranslationGain,
                AudioLogCueCameraRotationGain,
                _currentAudioLogHash);
        }

        private void ResolveAudioLogCueTransform(out Vector3 runtimePosition, out Vector3 direction)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
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
            ReadOnlySpan<char> timeSpan = buffer.AsSpan(timeStart, timeEnd - timeStart);
            if (!TryParseCueFloat(timeSpan, out startTime))
                return false;

            if (separatorIndex < 0)
                return true;

            int intensityStart = separatorIndex + 1;
            int intensityEnd = endExclusive;
            TrimRange(buffer, ref intensityStart, ref intensityEnd);
            if (intensityEnd <= intensityStart)
                return true;

            ReadOnlySpan<char> intensitySpan = buffer.AsSpan(intensityStart, intensityEnd - intensityStart);
            if (!TryParseCueFloat(intensitySpan, out float parsedIntensity))
                return true;

            speakerIntensity = Mathf.Clamp01(parsedIntensity);
            return true;
        }

        private static bool TryParseCueFloat(ReadOnlySpan<char> valueSpan, out float value)
        {
            value = 0f;
            valueSpan = TrimmedSpan(valueSpan);
            if (valueSpan.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (valueSpan[0] == '-')
            {
                sign = -1f;
                index = 1;
            }
            else if (valueSpan[0] == '+')
            {
                index = 1;
            }

            float integer = 0f;
            bool any = false;
            while (index < valueSpan.Length && valueSpan[index] >= '0' && valueSpan[index] <= '9')
            {
                integer = (integer * 10f) + (valueSpan[index] - '0');
                index++;
                any = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < valueSpan.Length && valueSpan[index] == '.')
            {
                index++;
                while (index < valueSpan.Length && valueSpan[index] >= '0' && valueSpan[index] <= '9')
                {
                    fraction = (fraction * 10f) + (valueSpan[index] - '0');
                    divisor *= 10f;
                    index++;
                    any = true;
                }
            }

            if (index != valueSpan.Length || !any)
                return false;

            value = sign * (integer + fraction / divisor);
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static ReadOnlySpan<char> TrimmedSpan(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : default;
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
            if (_runtimeOwnerAborted || _registeredToTickManager || !_serviceRegistered || !Application.isPlaying)
                return;

            _registeredToTickManager = RegisterLateFrameSwap();
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            UnregisterLateFrameSwap();
            _registeredToTickManager = false;
        }

        private bool RegisterLateFrameSwap()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return false;

            if (_registeredLateFrameSwap)
                return true;

            if (!Application.isPlaying)
                return false;

            _registeredLateFrameSwap = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
            return _registeredLateFrameSwap;
        }

        private void UnregisterLateFrameSwap()
        {
            if (!_registeredLateFrameSwap)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
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
            _subtitleText.fontSize = SubtitleBaseFontSize;
            _subtitleText.fontStyle = FontStyles.Bold;
            _subtitleText.alignment = TextAlignmentOptions.BottomGeoAligned;
            _subtitleText.textWrappingMode = TextWrappingModes.Normal;
            _subtitleText.raycastTarget = false;
            _subtitleText.color = TextColor;
            RefreshSubtitleTextLodPolicy();
            LocalizedTMPAutoSizer.Configure(
                _subtitleText,
                SubtitleMinimumFontSize,
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

            RectTransform waveformBar0 = null;
            RectTransform waveformBar1 = null;
            RectTransform waveformBar2 = null;
            RectTransform waveformBar3 = null;
            for (int i = 0; i < WaveformBarCount; i++)
            {
                GameObject barObject = new GameObject(
                    ResolveWaveformBarName(i),
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
                switch (i)
                {
                    case 0:
                        waveformBar0 = barRect;
                        break;
                    case 1:
                        waveformBar1 = barRect;
                        break;
                    case 2:
                        waveformBar2 = barRect;
                        break;
                    case 3:
                        waveformBar3 = barRect;
                        break;
                }
            }

            waveformOwner.TryGetComponent(out _audioWaveformAnimator);
            _audioWaveformAnimator.ConfigureWaveformTargets(waveformBar0, waveformBar1, waveformBar2, waveformBar3);

            _built = true;
        }

        private static string ResolveWaveformBarName(int index)
        {
            switch (index)
            {
                case 0:
                    return "Bar_0";
                case 1:
                    return "Bar_1";
                case 2:
                    return "Bar_2";
                default:
                    return "Bar_3";
            }
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
