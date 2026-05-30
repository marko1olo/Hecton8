// ============================================================================
// HECTON-8 — MessageTerminal.cs
// Communication radio / message terminal for lore updates and signals.
//
// ARCHITECTURE:
//   • IInteractable for player interaction
//   • State machine via ITickable (no coroutines)
//   • MaterialPropertyBlock for status light (zero GC)
//   • UnityEvent for audio log playback
//
// STATES:
//   Idle — no new messages, green light
//   NewMessage — pending message, blinking light
//   Playing — message is being played
//
// INTEGRATION:
//   • UnityEvent with message ID for audio system
//   • Tracks read messages for persistence
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Interaction;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Terminal state machine states.
    /// </summary>
    public enum TerminalState
    {
        Idle,       // No new messages
        NewMessage, // Pending message, blinking light
        Playing     // Message is being played
    }

    /// <summary>
    /// Represents a message/audio log entry.
    /// </summary>
    [System.Serializable]
    public class MessageEntry
    {
        [Tooltip("Unique identifier for this message.")]
        public string messageId;

        [Tooltip("Display name for the message.")]
        public string displayName;

        [Tooltip("Audio clip to play.")]
        public AudioClip audioClip;

        [Tooltip("Duration of the audio clip (auto-filled).")]
        public float duration;

        [Tooltip("True if message has been read/played.")]
        public bool isRead;
    }

    /// <summary>
    /// Communication radio / message terminal for lore updates.
    /// Implements IInteractable for player interaction.
    /// Uses ITickable state machine for message playback.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Hecton/Gameplay/Message Terminal")]
    public sealed class MessageTerminal : MonoBehaviour, IInteractable, IInteractableTextProvider, ITickable, IUpdatable, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001MessageTerminalSignalPushDropCount;
        private const uint WfcOutpostDatapadSourceHash = 0x57464354u; // WFCT
        private const byte WfcDatapadLootedFlag = (byte)WfcOutpostCellStateFlags.DatapadLooted;
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Messages ────────────────────────────────────")]
        [Tooltip("All messages available on this terminal.")]
        [SerializeField] private MessageEntry[] messages;

        [Header("── Status Light ───────────────────────────────")]
        [Tooltip("Renderer with the status light material.")]
        [SerializeField] private Renderer statusLightRenderer;

        [Tooltip("Material property name for emission color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Color for Idle state (no new messages).")]
        [SerializeField] private Color idleColor = new Color(0.2f, 0.2f, 0.2f);

        [Tooltip("Color for NewMessage state (blinking).")]
        [SerializeField] private Color newMessageColor = new Color(1f, 0.8f, 0f);

        [Tooltip("Color for Playing state.")]
        [SerializeField] private Color playingColor = new Color(0f, 0.5f, 1f);

        [Tooltip("Blink interval for new message indicator.")]
        [SerializeField, Range(0.1f, 2f)] private float blinkInterval = 0.5f;

        [Header("── Audio ──────────────────────────────────────")]
        [Tooltip("Sound played when terminal is accessed.")]
        [SerializeField] private AudioClip accessSound;

        [Tooltip("Sound played when a new message is received.")]
        [SerializeField] private AudioClip newMessageAlertSound;

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when a message starts playing. Parameter: messageId.")]
        [SerializeField] private UnityEvent<string> OnMessageStarted;

        [Tooltip("Fired when a message finishes playing. Parameter: messageId.")]
        [SerializeField] private UnityEvent<string> OnMessageCompleted;

        [Tooltip("Fired when a new message is received. Parameter: messageId.")]
        [SerializeField] private UnityEvent<string> OnNewMessageReceived;

        [Tooltip("Fired when terminal state changes. Parameter: newState.")]
        [SerializeField] private UnityEvent<TerminalState> OnStateChanged;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private TerminalState _state = TerminalState.Idle;
        private int _currentMessageIndex = -1;
        private float _playbackTimer;
        private float _blinkTimer;
        private bool _blinkOn;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localizationManager;
        private bool _statusLightDirty;
        private Color _pendingStatusLightColor;
        private AudioClip _pendingStaticAudio0;
        private AudioClip _pendingStaticAudio1;
        private float _pendingStaticAudioVolume0;
        private float _pendingStaticAudioVolume1;
        private int _pendingStaticAudioCount;
        private int _emissionPropertyId;

        // Track read messages (for persistence)
        private HashSet<string> _readMessageIds;
        private bool[] _initialReadStates;
        private int _pendingMessageIndex = -1;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private ulong _wfcOutpostSectorHash;
        private ushort _wfcOutpostCellIndex;
        private byte _wfcOutpostFlags;
        private bool _wfcOutpostPersistenceConfigured;

        // Pre-cached interaction text
        private const string DefaultReadText = "Read Messages";
        private const string DefaultNewMessageText = "New Message";
        private const string DefaultPlayingText = "Playing...";
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedReadTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedNewMessageTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedPlayingTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedReadTextLength;
        private int _cachedNewMessageTextLength;
        private int _cachedPlayingTextLength;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Current terminal state.</summary>
        public TerminalState State => _state;

        /// <summary>Index of the currently playing message (-1 if none).</summary>
        public int CurrentMessageIndex => _currentMessageIndex;

        /// <summary>True if there are unread messages.</summary>
        public bool HasUnreadMessages => _pendingMessageIndex >= 0;

        public void ConfigureWfcOutpostPersistence(ulong sectorHash, ushort cellIndex, byte initialFlags)
        {
            if (sectorHash == 0UL || cellIndex >= WfcOutpostPersistenceConstants.CellCount)
            {
                ResetWfcOutpostTransientPlaybackState();
                ClearWfcOutpostPersistence();
                RestoreWfcOutpostDatapadBaselineState();
                return;
            }

            _wfcOutpostSectorHash = sectorHash;
            _wfcOutpostCellIndex = cellIndex;
            _wfcOutpostFlags = (byte)(initialFlags & WfcOutpostPersistenceConstants.MutableFlagMask);
            _wfcOutpostPersistenceConfigured = true;
            ResetWfcOutpostTransientPlaybackState();

            if ((_wfcOutpostFlags & WfcDatapadLootedFlag) != 0)
                ApplyWfcOutpostDatapadLootedState();
            else
                RestoreWfcOutpostDatapadBaselineState();
        }

        public void ClearWfcOutpostPersistence()
        {
            _wfcOutpostPersistenceConfigured = false;
            _wfcOutpostSectorHash = 0UL;
            _wfcOutpostCellIndex = 0;
            _wfcOutpostFlags = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: MessageTerminal

            if (statusLightRenderer == null)
                TryGetComponent(out statusLightRenderer);

            CacheRegistryServicesCold();
            CaptureInitialReadStates();

            // Initialize read messages tracking
            int readMessageCapacity = messages != null ? messages.Length : 0;
            _readMessageIds = new HashSet<string>(readMessageCapacity); // COLD ALLOC: HashSet<string>[messages.Length] - track read messages - owner: MessageTerminal
            RebuildReadMessageSetFromMessageStates();

            // Find first unread message
            UpdatePendingMessage();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            RebuildLocalizedTextCache();
            UpdateState();
            UpdateStatusLight();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearQueuedStaticAudio();
            ClearWfcOutpostPersistence();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearQueuedStaticAudio();
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — STATE MACHINE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Handles playback and blinking.
        /// Zero GC: no allocations, uses cached values.
        /// </summary>
        private void CacheRegistryServicesCold()
        {
            _audioService = GlobalRegistry.Audio;
            _localizationManager = GlobalRegistry.LocalizationText;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registered = false;
                    _registeredLateFrame = false;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            switch (_state)
            {
                case TerminalState.NewMessage:
                    // Blink the status light
                    _blinkTimer += deltaTime;
                    if (_blinkTimer >= blinkInterval)
                    {
                        _blinkTimer = 0f;
                        _blinkOn = !_blinkOn;
                        UpdateStatusLight();
                    }
                    break;

                case TerminalState.Playing:
                    // Count down playback timer
                    _playbackTimer -= deltaTime;
                    if (_playbackTimer <= 0f)
                    {
                        CompletePlayback();
                    }
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        public void LateFrameTick()
        {
            FlushStatusLight();
            FlushQueuedStaticAudio();
        }

        void ILateFrameTickable.LateFrameTick()
        {
            LateFrameTick();
        }

        //  IInteractable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called when player's raycast first hits this object.
        /// </summary>
        public void OnHoverStart()
        {
            // Future: highlight effect, UI prompt
        }

        /// <summary>
        /// Called when player's raycast leaves this object.
        /// </summary>
        public void OnHoverEnd()
        {
            // Future: remove highlight, hide UI prompt
        }

        /// <summary>
        /// Called when player presses interact key while hovering.
        /// Starts playing pending message or shows message list.
        /// </summary>
        public void Interact(Transform interactor)
        {
            // Play access sound
            QueueStaticAudio(accessSound, 0.7f);

            if (_state == TerminalState.NewMessage && _pendingMessageIndex >= 0)
            {
                // Start playing the pending message
                StartPlayback(_pendingMessageIndex);
            }
            else if (_state == TerminalState.Idle)
            {
                // No new messages - could open message list UI
                // For now, just fire an event for UI to handle
                OnStateChanged?.Invoke(_state);
            }
            // If Playing, ignore interaction (or could stop playback)
        }

        /// <summary>
        /// Returns the UI prompt string. Zero GC: returns cached string.
        /// </summary>
        public string GetInteractText()
        {
            return ResolveInteractTextLegacy();
        }

        private string ResolveInteractTextLegacy()
        {
            switch (_state)
            {
                case TerminalState.Idle:
                    return DefaultReadText;
                case TerminalState.NewMessage:
                    return DefaultNewMessageText;
                case TerminalState.Playing:
                    return DefaultPlayingText;
                default:
                    return string.Empty;
            }
        }

        private ReadOnlySpan<char> ResolveInteractTextSpan()
        {
            switch (_state)
            {
                case TerminalState.Idle:
                    return _cachedReadTextBuffer.AsSpan(0, _cachedReadTextLength);
                case TerminalState.NewMessage:
                    return _cachedNewMessageTextBuffer.AsSpan(0, _cachedNewMessageTextLength);
                case TerminalState.Playing:
                    return _cachedPlayingTextBuffer.AsSpan(0, _cachedPlayingTextLength);
                default:
                    return ReadOnlySpan<char>.Empty;
            }
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(ResolveInteractTextSpan(), destination, out length);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Adds a new message to the terminal.
        /// </summary>
        /// <param name="message">The message to add.</param>
        public void AddMessage(MessageEntry message)
        {
            if (message == null)
                return;

            if (messages == null)
            {
                messages = new MessageEntry[] { message };
            }
            else
            {
                System.Array.Resize(ref messages, messages.Length + 1);
                messages[messages.Length - 1] = message;
            }

            EnsureInitialReadStateCapacity(messages.Length);
            _initialReadStates[messages.Length - 1] = message.isRead;

            // Check if this is a new unread message
            EnsureWfcOutpostReadMessageSet();
            if (message.isRead)
            {
                if (!string.IsNullOrEmpty(message.messageId))
                    _readMessageIds.Add(message.messageId);
            }
            else if (!string.IsNullOrEmpty(message.messageId) && !_readMessageIds.Contains(message.messageId))
            {
                _pendingMessageIndex = messages.Length - 1;
                UpdateState();

                // Play new message alert
                QueueStaticAudio(newMessageAlertSound, 0.8f);

                OnNewMessageReceived?.Invoke(message.messageId);
            }
        }

        /// <summary>
        /// Marks a message as read.
        /// </summary>
        /// <param name="messageId">The message ID to mark as read.</param>
        public void MarkMessageRead(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
                return;

            EnsureWfcOutpostReadMessageSet();
            bool wasRead = _readMessageIds.Contains(messageId);
            _readMessageIds.Add(messageId);

            // Update the message entry
            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    MessageEntry entry = messages[i];
                    if (entry != null && entry.messageId == messageId)
                    {
                        entry.isRead = true;
                        break;
                    }
                }
            }

            UpdatePendingMessage();
            UpdateState();

            if (!wasRead)
                SetWfcOutpostFlags(
                    (byte)(_wfcOutpostFlags | WfcDatapadLootedFlag),
                    SystemDispatcher.CurrentFrameId);
        }

        /// <summary>
        /// Checks if a message has been read.
        /// </summary>
        /// <param name="messageId">The message ID to check.</param>
        /// <returns>True if the message has been read.</returns>
        public bool IsMessageRead(string messageId)
        {
            return !string.IsNullOrEmpty(messageId) && _readMessageIds != null && _readMessageIds.Contains(messageId);
        }

        /// <summary>
        /// Starts playing a specific message by index.
        /// </summary>
        /// <param name="messageIndex">Index of the message to play.</param>
        public void PlayMessage(int messageIndex)
        {
            if (messages == null || messageIndex < 0 || messageIndex >= messages.Length)
                return;

            StartPlayback(messageIndex);
        }

        // ══════════════════════════════════════════════════════════
        //  MESSAGE LOGIC
        // ══════════════════════════════════════════════════════════

        private void ApplyWfcOutpostDatapadLootedState()
        {
            EnsureWfcOutpostReadMessageSet();

            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    MessageEntry entry = messages[i];
                    if (entry == null)
                        continue;

                    string messageId = entry.messageId;
                    if (!string.IsNullOrEmpty(messageId))
                        _readMessageIds.Add(messageId);

                    entry.isRead = true;
                }
            }

            _pendingMessageIndex = -1;
            if (_state != TerminalState.Playing)
            {
                _state = TerminalState.Idle;
                _blinkTimer = 0f;
                _blinkOn = false;
                UpdateStatusLight();
            }
        }

        private void RestoreWfcOutpostDatapadBaselineState()
        {
            if (_initialReadStates == null && messages != null)
                CaptureInitialReadStates();

            if (messages != null)
            {
                EnsureInitialReadStateCapacity(messages.Length);
                for (int i = 0; i < messages.Length; i++)
                {
                    MessageEntry entry = messages[i];
                    if (entry == null)
                        continue;

                    bool baselineRead = _initialReadStates != null &&
                                        i < _initialReadStates.Length &&
                                        _initialReadStates[i];
                    entry.isRead = baselineRead;
                }
            }

            RebuildReadMessageSetFromMessageStates();
            UpdatePendingMessage();
            UpdateState();
        }

        private void ResetWfcOutpostTransientPlaybackState()
        {
            _currentMessageIndex = -1;
            _playbackTimer = 0f;
            _blinkTimer = 0f;
            _blinkOn = false;
            if (_state == TerminalState.Playing)
                _state = TerminalState.Idle;
        }

        private void EnsureWfcOutpostReadMessageSet()
        {
            if (_readMessageIds != null)
                return;

            int readMessageCapacity = messages != null ? messages.Length : 0;
            _readMessageIds = new HashSet<string>(readMessageCapacity);
        }

        private void CaptureInitialReadStates()
        {
            int count = messages != null ? messages.Length : 0;
            if (count <= 0)
            {
                _initialReadStates = null;
                return;
            }

            _initialReadStates = new bool[count]; // COLD ALLOC: bool[messages.Length] - WFC pooled datapad read-state baseline - owner: MessageTerminal
            for (int i = 0; i < count; i++)
            {
                MessageEntry entry = messages[i];
                _initialReadStates[i] = entry != null && entry.isRead;
            }
        }

        private void EnsureInitialReadStateCapacity(int count)
        {
            if (count <= 0)
                return;

            if (_initialReadStates == null)
            {
                _initialReadStates = new bool[count];
                return;
            }

            if (_initialReadStates.Length < count)
                System.Array.Resize(ref _initialReadStates, count);
        }

        private void RebuildReadMessageSetFromMessageStates()
        {
            EnsureWfcOutpostReadMessageSet();
            _readMessageIds.Clear();

            if (messages == null)
                return;

            for (int i = 0; i < messages.Length; i++)
            {
                MessageEntry entry = messages[i];
                if (entry != null && entry.isRead && !string.IsNullOrEmpty(entry.messageId))
                    _readMessageIds.Add(entry.messageId);
            }
        }

        private void SetWfcOutpostFlags(byte flags, uint frame)
        {
            byte previous = _wfcOutpostFlags;
            byte current = (byte)(flags & WfcOutpostPersistenceConstants.MutableFlagMask);
            _wfcOutpostFlags = current;
            PublishWfcOutpostFlags(previous, current, frame);
        }

        private void PublishWfcOutpostFlags(byte previous, byte current, uint frame)
        {
            if (!_wfcOutpostPersistenceConfigured)
                return;

            previous = (byte)(previous & WfcOutpostPersistenceConstants.MutableFlagMask);
            current = (byte)(current & WfcOutpostPersistenceConstants.MutableFlagMask);
            if (previous == current)
                return;

            WfcOutpostStateChangedSignal signal = new WfcOutpostStateChangedSignal
            {
                SectorHash = _wfcOutpostSectorHash,
                CellIndex = _wfcOutpostCellIndex,
                PreviousFlags = previous,
                CurrentFlags = current,
                Frame = frame,
                SourceHash = WfcOutpostDatapadSourceHash,
                Flags = 0
            };
            SignalBus<WfcOutpostStateChangedSignal>.TryPushTracked(in signal, ref s_x001MessageTerminalSignalPushDropCount);
        }

        private void UpdatePendingMessage()
        {
            _pendingMessageIndex = -1;

            if (messages == null)
                return;

            EnsureWfcOutpostReadMessageSet();
            for (int i = 0; i < messages.Length; i++)
            {
                MessageEntry entry = messages[i];
                if (entry != null &&
                    !entry.isRead &&
                    (string.IsNullOrEmpty(entry.messageId) || !_readMessageIds.Contains(entry.messageId)))
                {
                    _pendingMessageIndex = i;
                    return;
                }
            }
        }

        private void UpdateState()
        {
            TerminalState newState;

            if (_state == TerminalState.Playing)
            {
                // Don't interrupt playback
                return;
            }

            if (_pendingMessageIndex >= 0)
            {
                newState = TerminalState.NewMessage;
            }
            else
            {
                newState = TerminalState.Idle;
            }

            if (newState != _state)
            {
                _state = newState;
                _blinkTimer = 0f;
                _blinkOn = true;
                UpdateStatusLight();
                OnStateChanged?.Invoke(_state);
            }
        }

        private void StartPlayback(int messageIndex)
        {
            if (messages == null || messageIndex < 0 || messageIndex >= messages.Length)
                return;

            MessageEntry message = messages[messageIndex];
            if (message == null)
                return;

            _currentMessageIndex = messageIndex;
            _state = TerminalState.Playing;

            // Set playback duration
            if (message.audioClip != null)
            {
                _playbackTimer = message.audioClip.length;
            }
            else
            {
                _playbackTimer = message.duration > 0 ? message.duration : 5f;
            }

            // Update status light
            UpdateStatusLight();

            // Fire event for audio system
            OnMessageStarted?.Invoke(message.messageId);

            // Mark as read
            MarkMessageRead(message.messageId);
        }

        private void CompletePlayback()
        {
            if (_currentMessageIndex < 0 || messages == null || _currentMessageIndex >= messages.Length)
            {
                _state = TerminalState.Idle;
                _currentMessageIndex = -1;
                UpdateStatusLight();
                return;
            }

            MessageEntry message = messages[_currentMessageIndex];
            string messageId = message != null ? message.messageId : string.Empty;

            // Reset state
            _currentMessageIndex = -1;
            _playbackTimer = 0f;

            // Fire completion event
            OnMessageCompleted?.Invoke(messageId);

            // Update state
            UpdatePendingMessage();
            UpdateState();
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the status light color using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock and Shader.PropertyToID.
        /// </summary>
        private void UpdateStatusLight()
        {
            Color lightColor;

            switch (_state)
            {
                case TerminalState.Idle:
                    lightColor = idleColor;
                    break;

                case TerminalState.NewMessage:
                    lightColor = _blinkOn ? newMessageColor : Color.black;
                    break;

                case TerminalState.Playing:
                    lightColor = playingColor;
                    break;

                default:
                    lightColor = idleColor;
                    break;
            }

            _pendingStatusLightColor = lightColor;
            _statusLightDirty = true;
        }

        private void FlushStatusLight()
        {
            if (!_statusLightDirty)
                return;

            _statusLightDirty = false;
            if (statusLightRenderer == null)
                return;

            statusLightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, _pendingStatusLightColor);
            statusLightRenderer.SetPropertyBlock(_mpb);
        }

        private void QueueStaticAudio(AudioClip clip, float volume)
        {
            if (clip == null || _audioService == null)
                return;

            switch (_pendingStaticAudioCount)
            {
                case 0:
                    _pendingStaticAudio0 = clip;
                    _pendingStaticAudioVolume0 = Mathf.Clamp01(volume);
                    _pendingStaticAudioCount = 1;
                    break;
                case 1:
                    _pendingStaticAudio1 = clip;
                    _pendingStaticAudioVolume1 = Mathf.Clamp01(volume);
                    _pendingStaticAudioCount = 2;
                    break;
                default:
                    return;
            }
        }

        private void FlushQueuedStaticAudio()
        {
            int count = _pendingStaticAudioCount;
            if (count <= 0)
                return;

            AudioClip clip0 = _pendingStaticAudio0;
            AudioClip clip1 = _pendingStaticAudio1;
            float volume0 = _pendingStaticAudioVolume0;
            float volume1 = _pendingStaticAudioVolume1;
            ClearQueuedStaticAudio();

            IAudioService audioService = _audioService;
            if (audioService == null)
                return;

            if (count > 0 && clip0 != null)
                audioService.PlayStatic2D(clip0, volume0);
            if (count > 1 && clip1 != null)
                audioService.PlayStatic2D(clip1, volume1);
        }

        private void ClearQueuedStaticAudio()
        {
            _pendingStaticAudio0 = null;
            _pendingStaticAudio1 = null;
            _pendingStaticAudioVolume0 = 0f;
            _pendingStaticAudioVolume1 = 0f;
            _pendingStaticAudioCount = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (blinkInterval < 0.1f) blinkInterval = 0.1f;

            // Auto-fill durations from audio clips
            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    MessageEntry entry = messages[i];
                    if (entry != null && entry.audioClip != null)
                        entry.duration = entry.audioClip.length;
                }
            }

            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            _cachedReadTextLength = InteractableTextCopy.CopyLocalizedTruncated(_localizationManager, LocalizationKeys.INTERACT_READ_MESSAGES, DefaultReadText, _cachedReadTextBuffer);
            _cachedNewMessageTextLength = InteractableTextCopy.CopyLocalizedTruncated(_localizationManager, LocalizationKeys.INTERACT_NEW_MESSAGE, DefaultNewMessageText, _cachedNewMessageTextBuffer);
            _cachedPlayingTextLength = InteractableTextCopy.CopyLocalizedTruncated(_localizationManager, LocalizationKeys.INTERACT_PLAYING, DefaultPlayingText, _cachedPlayingTextBuffer);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

    }
}

