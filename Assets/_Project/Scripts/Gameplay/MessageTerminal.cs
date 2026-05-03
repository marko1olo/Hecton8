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
using Hecton8.Interaction;
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
    public sealed class MessageTerminal : MonoBehaviour, IInteractable, ITickable, IUpdatable, ILocalizationLanguageChangedListener
    {
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
        private int _emissionPropertyId;

        // Track read messages (for persistence)
        private HashSet<string> _readMessageIds;
        private int _pendingMessageIndex = -1;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Pre-cached interaction text
        private const string DefaultReadText = "Read Messages";
        private const string DefaultNewMessageText = "New Message";
        private const string DefaultPlayingText = "Playing...";
        private string _cachedReadText;
        private string _cachedNewMessageText;
        private string _cachedPlayingText;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Current terminal state.</summary>
        public TerminalState State => _state;

        /// <summary>Index of the currently playing message (-1 if none).</summary>
        public int CurrentMessageIndex => _currentMessageIndex;

        /// <summary>True if there are unread messages.</summary>
        public bool HasUnreadMessages => _pendingMessageIndex >= 0;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: MessageTerminal

            if (statusLightRenderer == null)
                statusLightRenderer = GetComponent<Renderer>();

            // Initialize read messages tracking
            _readMessageIds = new HashSet<string>(); // COLD ALLOC: HashSet<string> — track read messages — owner: MessageTerminal

            // Mark already-read messages
            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    if (messages[i].isRead && !string.IsNullOrEmpty(messages[i].messageId))
                    {
                        _readMessageIds.Add(messages[i].messageId);
                    }
                }
            }

            // Find first unread message
            UpdatePendingMessage();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            RebuildLocalizedTextCache();
            UpdateState();
            UpdateStatusLight();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — STATE MACHINE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Handles playback and blinking.
        /// Zero GC: no allocations, uses cached values.
        /// </summary>
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
            if (accessSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayStatic2D(accessSound, 0.7f);
            }

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
            switch (_state)
            {
                case TerminalState.Idle:
                    return _cachedReadText;
                case TerminalState.NewMessage:
                    return _cachedNewMessageText;
                case TerminalState.Playing:
                    return _cachedPlayingText;
                default:
                    return string.Empty;
            }
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
            if (messages == null)
            {
                messages = new MessageEntry[] { message };
            }
            else
            {
                System.Array.Resize(ref messages, messages.Length + 1);
                messages[messages.Length - 1] = message;
            }

            // Check if this is a new unread message
            if (!message.isRead && !string.IsNullOrEmpty(message.messageId) && !_readMessageIds.Contains(message.messageId))
            {
                _pendingMessageIndex = messages.Length - 1;
                UpdateState();

                // Play new message alert
                if (newMessageAlertSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
                {
                    audio.PlayStatic2D(newMessageAlertSound, 0.8f);
                }

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

            _readMessageIds.Add(messageId);

            // Update the message entry
            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    if (messages[i].messageId == messageId)
                    {
                        messages[i].isRead = true;
                        break;
                    }
                }
            }

            UpdatePendingMessage();
            UpdateState();
        }

        /// <summary>
        /// Checks if a message has been read.
        /// </summary>
        /// <param name="messageId">The message ID to check.</param>
        /// <returns>True if the message has been read.</returns>
        public bool IsMessageRead(string messageId)
        {
            return !string.IsNullOrEmpty(messageId) && _readMessageIds.Contains(messageId);
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

        private void UpdatePendingMessage()
        {
            _pendingMessageIndex = -1;

            if (messages == null)
                return;

            for (int i = 0; i < messages.Length; i++)
            {
                if (!messages[i].isRead && !_readMessageIds.Contains(messages[i].messageId))
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

            string messageId = messages[_currentMessageIndex].messageId;

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
            if (statusLightRenderer == null)
                return;

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

            statusLightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, lightColor);
            statusLightRenderer.SetPropertyBlock(_mpb);
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
                    if (messages[i].audioClip != null)
                    {
                        messages[i].duration = messages[i].audioClip.length;
                    }
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
            _cachedReadText = ResolveLocalized(LocalizationKeys.INTERACT_READ_MESSAGES, DefaultReadText);
            _cachedNewMessageText = ResolveLocalized(LocalizationKeys.INTERACT_NEW_MESSAGE, DefaultNewMessageText);
            _cachedPlayingText = ResolveLocalized(LocalizationKeys.INTERACT_PLAYING, DefaultPlayingText);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}

