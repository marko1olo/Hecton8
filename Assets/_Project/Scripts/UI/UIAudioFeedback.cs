using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Hecton8.Audio;
using System.Collections.Generic;

namespace Hecton8.UI
{
    /// <summary>
    /// Automatic audio feedback for UI interactions.
    /// Attach to Canvas root to enable audio for all buttons/sliders/toggles.
    /// Zero-GC: cached clips, no LINQ, no allocations in hot paths.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("Hecton8/UI/UI Audio Feedback")]
    public sealed class UIAudioFeedback : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== BUTTON SOUNDS ===")]
        [SerializeField, Tooltip("Primary actions: New Game, Resume, Save")]
        private AudioClip clickPrimary;

        [SerializeField, Tooltip("Secondary actions: Back, Settings, navigation")]
        private AudioClip clickSecondary;

        [SerializeField, Tooltip("Destructive actions: Quit, Exit to Menu, Delete")]
        private AudioClip clickDestructive;

        [SerializeField, Tooltip("Button hover sound")]
        private AudioClip hoverButton;

        [Header("=== SLIDER/TOGGLE SOUNDS ===")]
        [SerializeField, Tooltip("Slider value change (throttled)")]
        private AudioClip sliderTick;

        [SerializeField, Tooltip("Toggle on")]
        private AudioClip toggleOn;

        [SerializeField, Tooltip("Toggle off")]
        private AudioClip toggleOff;

        [Header("=== PANEL SOUNDS ===")]
        [SerializeField, Tooltip("Panel open whoosh")]
        private AudioClip panelOpen;

        [SerializeField, Tooltip("Panel close whoosh")]
        private AudioClip panelClose;

        [Header("=== SETTINGS ===")]
        [SerializeField, Range(0f, 1f)]
        private float volume = 0.7f;

        [SerializeField, Range(0f, 1f)]
        private float hoverVolume = 0.4f;

        [SerializeField]
        private float sliderTickThrottle = 0.1f;

        [SerializeField]
        private bool enableHoverSounds = true;

        [Header("=== PITCH VARIATION ===")]
        [SerializeField, Tooltip("Enable pitch randomization for variety")]
        private bool enablePitchVariation = true;

        [SerializeField, Range(0f, 0.2f), Tooltip("Pitch variation range (±)")]
        private float pitchVariation = 0.05f;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Log audio playback events")]
        private bool debugLog = false;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private Hecton8.Core.IAudioService _audioManager;
        private float _lastSliderTickTime;
        private static UIAudioFeedback _instance;
        // COLD ALLOC: List<Button>(64) — UI button registration buffer — owner: UIAudioFeedback
        private readonly List<Button> _buttonResolveBuffer = new List<Button>(64);
        // COLD ALLOC: List<Slider>(32) — UI slider registration buffer — owner: UIAudioFeedback
        private readonly List<Slider> _sliderResolveBuffer = new List<Slider>(32);
        // COLD ALLOC: List<Toggle>(32) — UI toggle registration buffer — owner: UIAudioFeedback
        private readonly List<Toggle> _toggleResolveBuffer = new List<Toggle>(32);

        // Stats
        private int _totalSoundsPlayed;
        private int _throttledSounds;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            _audioManager = Hecton8.Core.GlobalRegistry.Audio;
            RegisterAllButtons();
            RegisterAllSliders();
            RegisterAllToggles();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        public static void PlayPanelOpen()
        {
            if (_instance != null && _instance.panelOpen != null)
                _instance.PlaySound(_instance.panelOpen, _instance.volume);
        }

        public static void PlayPanelClose()
        {
            if (_instance != null && _instance.panelClose != null)
                _instance.PlaySound(_instance.panelClose, _instance.volume);
        }

        public static void PlayClickPrimary()
        {
            if (_instance != null && _instance.clickPrimary != null)
                _instance.PlaySound(_instance.clickPrimary, _instance.volume);
        }

        public static void PlayClickSecondary()
        {
            if (_instance != null && _instance.clickSecondary != null)
                _instance.PlaySound(_instance.clickSecondary, _instance.volume);
        }

        public static void PlayClickDestructive()
        {
            if (_instance != null && _instance.clickDestructive != null)
                _instance.PlaySound(_instance.clickDestructive, _instance.volume);
        }

        /// <summary>
        /// Get audio playback statistics.
        /// </summary>
        public static void GetStats(out int totalPlayed, out int throttled)
        {
            if (_instance != null)
            {
                totalPlayed = _instance._totalSoundsPlayed;
                throttled = _instance._throttledSounds;
            }
            else
            {
                totalPlayed = 0;
                throttled = 0;
            }
        }

        /// <summary>
        /// Reset audio playback statistics.
        /// </summary>
        public static void ResetStats()
        {
            if (_instance != null)
            {
                _instance._totalSoundsPlayed = 0;
                _instance._throttledSounds = 0;
            }
        }

        // ══════════════════════════════════════════════════════════
        // REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterAllButtons()
        {
            _buttonResolveBuffer.Clear();
            GetComponentsInChildren(true, _buttonResolveBuffer);
            for (int i = 0; i < _buttonResolveBuffer.Count; i++)
            {
                Button button = _buttonResolveBuffer[i];
                if (button == null)
                    continue;

                // Determine button type by name
                ButtonType type = GetButtonType(button.gameObject.name);
                RegisterButton(button, type);
            }

            _buttonResolveBuffer.Clear();
        }

        private void RegisterAllSliders()
        {
            _sliderResolveBuffer.Clear();
            GetComponentsInChildren(true, _sliderResolveBuffer);
            for (int i = 0; i < _sliderResolveBuffer.Count; i++)
            {
                Slider slider = _sliderResolveBuffer[i];
                if (slider == null)
                    continue;

                slider.onValueChanged.AddListener(_ => OnSliderChanged());
            }

            _sliderResolveBuffer.Clear();
        }

        private void RegisterAllToggles()
        {
            _toggleResolveBuffer.Clear();
            GetComponentsInChildren(true, _toggleResolveBuffer);
            for (int i = 0; i < _toggleResolveBuffer.Count; i++)
            {
                Toggle toggle = _toggleResolveBuffer[i];
                if (toggle == null)
                    continue;

                toggle.onValueChanged.AddListener(OnToggleChanged);
            }

            _toggleResolveBuffer.Clear();
        }

        private void RegisterButton(Button button, ButtonType type)
        {
            if (button == null)
                return;

            // Add click listener
            button.onClick.AddListener(() => OnButtonClicked(type));

            // Add hover listener if enabled
            if (enableHoverSounds)
            {
                EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = button.gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerEnter
                };
                entry.callback.AddListener(_ => OnButtonHover());
                trigger.triggers.Add(entry);
            }
        }

        private ButtonType GetButtonType(string buttonName)
        {
            string lower = buttonName.ToLowerInvariant();

            // Destructive
            if (lower.Contains("quit") || lower.Contains("exit") || lower.Contains("delete") || lower.Contains("abort"))
                return ButtonType.Destructive;

            // Primary
            if (lower.Contains("new") || lower.Contains("start") || lower.Contains("resume") ||
                lower.Contains("save") || lower.Contains("load") || lower.Contains("apply") ||
                lower.Contains("confirm") || lower.Contains("ok"))
                return ButtonType.Primary;

            // Secondary (default)
            return ButtonType.Secondary;
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS
        // ══════════════════════════════════════════════════════════

        private void OnButtonClicked(ButtonType type)
        {
            AudioClip clip = type switch
            {
                ButtonType.Primary => clickPrimary,
                ButtonType.Destructive => clickDestructive,
                _ => clickSecondary
            };

            if (clip != null)
                PlaySound(clip, volume);
        }

        private void OnButtonHover()
        {
            if (hoverButton != null)
                PlaySound(hoverButton, hoverVolume);
        }

        private void OnSliderChanged()
        {
            float currentTime = Time.unscaledTime;
            if (currentTime - _lastSliderTickTime < sliderTickThrottle)
            {
                _throttledSounds++;
                return;
            }

            _lastSliderTickTime = currentTime;

            if (sliderTick != null)
                PlaySound(sliderTick, volume * 0.6f);
        }

        private void OnToggleChanged(bool isOn)
        {
            AudioClip clip = isOn ? toggleOn : toggleOff;
            if (clip != null)
                PlaySound(clip, volume);
        }

        // ══════════════════════════════════════════════════════════
        // AUDIO PLAYBACK
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip, float vol)
        {
            if (_audioManager == null)
            {
                _audioManager = Hecton8.Core.GlobalRegistry.Audio;
                if (_audioManager == null)
                    return;
            }

            // Pitch variation for variety
            float pitch = 1f;
            if (enablePitchVariation)
            {
                pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            }

            _audioManager.PlayStatic2D(clip, vol, _audioManager.InterfaceGroup);
            _totalSoundsPlayed++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugLog)
            {
                Debug.Log($"[UIAudioFeedback] Played: {clip.name} | Volume: {vol:F2} | Pitch: {pitch:F2} | Total: {_totalSoundsPlayed} | Throttled: {_throttledSounds}");
            }
#endif
        }

        // ══════════════════════════════════════════════════════════
        // TYPES
        // ══════════════════════════════════════════════════════════

        private enum ButtonType
        {
            Primary,
            Secondary,
            Destructive
        }
    }
}
