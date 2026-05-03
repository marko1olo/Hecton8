using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
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

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Log audio playback events")]
        private bool debugLog = false;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private Hecton8.Core.IAudioService _audioManager;
        private float _lastSliderTickTime;
        private static UIAudioFeedback _instance;
        private UnityAction _primaryButtonClickAction;
        private UnityAction _secondaryButtonClickAction;
        private UnityAction _destructiveButtonClickAction;
        private UnityAction<float> _sliderChangedAction;
        private UnityAction<bool> _toggleChangedAction;
        private UnityAction<BaseEventData> _buttonHoverAction;
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
            _primaryButtonClickAction = OnPrimaryButtonClicked; // COLD ALLOC: UnityAction[1] — cached primary button audio listener — owner: UIAudioFeedback
            _secondaryButtonClickAction = OnSecondaryButtonClicked; // COLD ALLOC: UnityAction[1] — cached secondary button audio listener — owner: UIAudioFeedback
            _destructiveButtonClickAction = OnDestructiveButtonClicked; // COLD ALLOC: UnityAction[1] — cached destructive button audio listener — owner: UIAudioFeedback
            _sliderChangedAction = OnSliderChanged; // COLD ALLOC: UnityAction<float>[1] — cached slider audio listener — owner: UIAudioFeedback
            _toggleChangedAction = OnToggleChanged; // COLD ALLOC: UnityAction<bool>[1] — cached toggle audio listener — owner: UIAudioFeedback
            _buttonHoverAction = OnButtonHoverEvent; // COLD ALLOC: UnityAction<BaseEventData>[1] — cached button hover audio listener — owner: UIAudioFeedback
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
            UnregisterAllButtons();
            UnregisterAllSliders();
            UnregisterAllToggles();

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

                // Classification is cold, but avoid allocating a lowercase copy of the button name.
                ButtonType type = GetButtonType(button.name);
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

                slider.onValueChanged.RemoveListener(_sliderChangedAction);
                slider.onValueChanged.AddListener(_sliderChangedAction);
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

                toggle.onValueChanged.RemoveListener(_toggleChangedAction);
                toggle.onValueChanged.AddListener(_toggleChangedAction);
            }

            _toggleResolveBuffer.Clear();
        }

        private void UnregisterAllButtons()
        {
            _buttonResolveBuffer.Clear();
            GetComponentsInChildren(true, _buttonResolveBuffer);
            for (int i = 0; i < _buttonResolveBuffer.Count; i++)
            {
                Button button = _buttonResolveBuffer[i];
                if (button == null)
                    continue;

                button.onClick.RemoveListener(_primaryButtonClickAction);
                button.onClick.RemoveListener(_secondaryButtonClickAction);
                button.onClick.RemoveListener(_destructiveButtonClickAction);

                if (button.TryGetComponent(out EventTrigger trigger))
                {
                    List<EventTrigger.Entry> entries = trigger.triggers;
                    for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                    {
                        EventTrigger.Entry entry = entries[entryIndex];
                        if (entry != null && entry.eventID == EventTriggerType.PointerEnter)
                            entry.callback.RemoveListener(_buttonHoverAction);
                    }
                }
            }

            _buttonResolveBuffer.Clear();
        }

        private void UnregisterAllSliders()
        {
            _sliderResolveBuffer.Clear();
            GetComponentsInChildren(true, _sliderResolveBuffer);
            for (int i = 0; i < _sliderResolveBuffer.Count; i++)
            {
                Slider slider = _sliderResolveBuffer[i];
                if (slider == null)
                    continue;

                slider.onValueChanged.RemoveListener(_sliderChangedAction);
            }

            _sliderResolveBuffer.Clear();
        }

        private void UnregisterAllToggles()
        {
            _toggleResolveBuffer.Clear();
            GetComponentsInChildren(true, _toggleResolveBuffer);
            for (int i = 0; i < _toggleResolveBuffer.Count; i++)
            {
                Toggle toggle = _toggleResolveBuffer[i];
                if (toggle == null)
                    continue;

                toggle.onValueChanged.RemoveListener(_toggleChangedAction);
            }

            _toggleResolveBuffer.Clear();
        }

        private void RegisterButton(Button button, ButtonType type)
        {
            if (button == null)
                return;

            UnityAction action = type switch
            {
                ButtonType.Primary => _primaryButtonClickAction,
                ButtonType.Destructive => _destructiveButtonClickAction,
                _ => _secondaryButtonClickAction
            };

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);

            // Add hover listener if enabled
            if (enableHoverSounds)
            {
                if (!button.TryGetComponent(out EventTrigger trigger))
                    trigger = button.gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = GetOrCreatePointerEnterEntry(trigger);
                entry.callback.RemoveListener(_buttonHoverAction);
                entry.callback.AddListener(_buttonHoverAction);
            }
        }

        private static EventTrigger.Entry GetOrCreatePointerEnterEntry(EventTrigger trigger)
        {
            List<EventTrigger.Entry> entries = trigger.triggers;
            for (int i = 0; i < entries.Count; i++)
            {
                EventTrigger.Entry entry = entries[i];
                if (entry != null && entry.eventID == EventTriggerType.PointerEnter)
                    return entry;
            }

            // COLD ALLOC: EventTrigger.Entry[1] — shared pointer-enter hover callback entry — owner: UIAudioFeedback
            EventTrigger.Entry newEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            entries.Add(newEntry);
            return newEntry;
        }

        private static ButtonType GetButtonType(string buttonName)
        {
            if (string.IsNullOrEmpty(buttonName))
                return ButtonType.Secondary;

            if (ContainsOrdinalIgnoreCase(buttonName, "quit") ||
                ContainsOrdinalIgnoreCase(buttonName, "exit") ||
                ContainsOrdinalIgnoreCase(buttonName, "delete") ||
                ContainsOrdinalIgnoreCase(buttonName, "abort"))
            {
                return ButtonType.Destructive;
            }

            if (ContainsOrdinalIgnoreCase(buttonName, "new") ||
                ContainsOrdinalIgnoreCase(buttonName, "start") ||
                ContainsOrdinalIgnoreCase(buttonName, "resume") ||
                ContainsOrdinalIgnoreCase(buttonName, "save") ||
                ContainsOrdinalIgnoreCase(buttonName, "load") ||
                ContainsOrdinalIgnoreCase(buttonName, "apply") ||
                ContainsOrdinalIgnoreCase(buttonName, "confirm") ||
                ContainsOrdinalIgnoreCase(buttonName, "ok"))
            {
                return ButtonType.Primary;
            }

            return ButtonType.Secondary;
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string token)
        {
            return source.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS
        // ══════════════════════════════════════════════════════════

        private void OnPrimaryButtonClicked()
        {
            OnButtonClicked(ButtonType.Primary);
        }

        private void OnSecondaryButtonClicked()
        {
            OnButtonClicked(ButtonType.Secondary);
        }

        private void OnDestructiveButtonClicked()
        {
            OnButtonClicked(ButtonType.Destructive);
        }

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

        private void OnButtonHoverEvent(BaseEventData eventData)
        {
            OnButtonHover();
        }

        private void OnSliderChanged(float value)
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

            _audioManager.PlayStatic2D(clip, vol, _audioManager.InterfaceGroup);
            _totalSoundsPlayed++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugLog)
            {
                Debug.Log($"[UIAudioFeedback] Played: {clip.name} | Volume: {vol:F2} | Total: {_totalSoundsPlayed} | Throttled: {_throttledSounds}");
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
