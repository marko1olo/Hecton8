using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Hecton8.Audio;
using Hecton8.Core;

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
    public sealed class UIAudioFeedback : MonoBehaviour, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        // ----------------------------------------------------------
        // INSPECTOR
        // ----------------------------------------------------------

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

        [SerializeField, Tooltip("Hover sounds bind only to pre-authored EventTrigger PointerEnter entries. Runtime EventTrigger/Entry creation is forbidden.")]
        private bool enableHoverSounds = true;

        [Header("=== AUTHORED BUTTON ROLES ===")]
        [SerializeField, Tooltip("Buttons explicitly using the primary click cue. Default unlisted buttons use the secondary cue.")]
        private Button[] primaryButtons = System.Array.Empty<Button>();

        [SerializeField, Tooltip("Buttons explicitly using the destructive click cue. Default unlisted buttons use the secondary cue.")]
        private Button[] destructiveButtons = System.Array.Empty<Button>();

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Log audio playback events")]
        private bool debugLog = false;

        // ----------------------------------------------------------
        // FIELDS
        // ----------------------------------------------------------

        private IAudioService _audioManager;
        private float _lastSliderTickTime;
        private bool _runtimeRegistered;
        private bool _hotSwapRegistered;
        private bool _controlsRegistered;
        private float _nextDebugLogTime;
        private UnityAction _primaryButtonClickAction;
        private UnityAction _secondaryButtonClickAction;
        private UnityAction _destructiveButtonClickAction;
        private UnityAction<float> _sliderChangedAction;
        private UnityAction<bool> _toggleChangedAction;
        private UnityAction<BaseEventData> _buttonHoverAction;

        // Stats
        private int _totalSoundsPlayed;
        private int _throttledSounds;
        private static UIAudioFeedback s_activeRuntime;
        public ServiceHeartbeatState HeartbeatState => _runtimeRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _runtimeRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            s_activeRuntime = null;
        }

        // ----------------------------------------------------------
        // LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            UIAudioFeedback registered = GlobalRegistry.UIAudioFeedback;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }
            _primaryButtonClickAction = OnPrimaryButtonClicked; // COLD ALLOC: UnityAction[1] — cached primary button audio listener — owner: UIAudioFeedback
            _secondaryButtonClickAction = OnSecondaryButtonClicked; // COLD ALLOC: UnityAction[1] — cached secondary button audio listener — owner: UIAudioFeedback
            _destructiveButtonClickAction = OnDestructiveButtonClicked; // COLD ALLOC: UnityAction[1] — cached destructive button audio listener — owner: UIAudioFeedback
            _sliderChangedAction = OnSliderChanged; // COLD ALLOC: UnityAction<float>[1] — cached slider audio listener — owner: UIAudioFeedback
            _toggleChangedAction = OnToggleChanged; // COLD ALLOC: UnityAction<bool>[1] — cached toggle audio listener — owner: UIAudioFeedback
            _buttonHoverAction = OnButtonHoverEvent; // COLD ALLOC: UnityAction<BaseEventData>[1] — cached button hover audio listener — owner: UIAudioFeedback
        }

        private void Start()
        {
            if (!_runtimeRegistered && !TryRegisterRuntime())
                return;

            BindAudioAndRegisterControls();
            TryRegisterHotSwapListener();
        }

        private void OnEnable()
        {
            if (!TryRegisterRuntime())
                return;

            BindAudioAndRegisterControls();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterControls();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
            _audioManager = null;
        }

        private void OnDestroy()
        {
            TryUnregisterControls();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
        }

        public void OnServiceShutdown()
        {
            TryUnregisterControls();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
            _audioManager = null;
        }

        // ----------------------------------------------------------
        // PUBLIC API
        // ----------------------------------------------------------

        public static void PlayPanelOpen()
        {
            UIAudioFeedback instance = s_activeRuntime;
            if (instance != null && instance.panelOpen != null)
                instance.PlaySound(instance.panelOpen, instance.volume);
        }

        public static void PlayPanelClose()
        {
            UIAudioFeedback instance = s_activeRuntime;
            if (instance != null && instance.panelClose != null)
                instance.PlaySound(instance.panelClose, instance.volume);
        }

        public static void PlayClickPrimary()
        {
            UIAudioFeedback instance = s_activeRuntime;
            if (instance != null && instance.clickPrimary != null)
                instance.PlaySound(instance.clickPrimary, instance.volume);
        }

        public static void PlayClickSecondary()
        {
            UIAudioFeedback instance = s_activeRuntime;
            if (instance != null && instance.clickSecondary != null)
                instance.PlaySound(instance.clickSecondary, instance.volume);
        }

        public static void PlayClickDestructive()
        {
            UIAudioFeedback instance = s_activeRuntime;
            if (instance != null && instance.clickDestructive != null)
                instance.PlaySound(instance.clickDestructive, instance.volume);
        }

        /// <summary>
        /// Get audio playback statistics.
        /// </summary>
        public static void GetStats(out int totalPlayed, out int throttled)
        {
            UIAudioFeedback instance = s_activeRuntime;
            if (instance != null)
            {
                totalPlayed = instance._totalSoundsPlayed;
                throttled = instance._throttledSounds;
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
            UIAudioFeedback instance = s_activeRuntime;
            if (instance != null)
            {
                instance._totalSoundsPlayed = 0;
                instance._throttledSounds = 0;
            }
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Audio)
                return;

            _audioManager = currentService as IAudioService;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                _audioManager = currentService as IAudioService;
        }

        private bool TryRegisterRuntime()
        {
            if (_runtimeRegistered)
                return true;

            if (!Application.isPlaying)
                return false;

            UIAudioFeedback registered = GlobalRegistry.UIAudioFeedback;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterUIAudioFeedbackRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.UIAudioFeedback, this);
            if (_runtimeRegistered)
                s_activeRuntime = this;
            return _runtimeRegistered;
        }

        private void TryUnregisterRuntime()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterUIAudioFeedbackRuntime(this);
            _runtimeRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
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

            GlobalRegistry.UnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void BindAudioAndRegisterControls()
        {
            _audioManager = GlobalRegistry.Audio;

            if (_controlsRegistered)
                return;

            RegisterAllButtons();
            RegisterAllSliders();
            RegisterAllToggles();
            _controlsRegistered = true;
        }

        private void TryUnregisterControls()
        {
            if (!_controlsRegistered)
                return;

            UnregisterAllButtons();
            UnregisterAllSliders();
            UnregisterAllToggles();
            _controlsRegistered = false;
        }

        // ----------------------------------------------------------
        // REGISTRATION
        // ----------------------------------------------------------

        private void RegisterAllButtons()
        {
            RegisterButtonsInHierarchy(transform);
        }

        private void RegisterAllSliders()
        {
            RegisterSlidersInHierarchy(transform);
        }

        private void RegisterAllToggles()
        {
            RegisterTogglesInHierarchy(transform);
        }

        private void UnregisterAllButtons()
        {
            UnregisterButtonsInHierarchy(transform);
        }

        private void UnregisterAllSliders()
        {
            UnregisterSlidersInHierarchy(transform);
        }

        private void UnregisterAllToggles()
        {
            UnregisterTogglesInHierarchy(transform);
        }

        private void RegisterButtonsInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out Button button))
            {
                ButtonType type = ResolveButtonType(button);
                RegisterButton(button, type);
            }

            for (int i = 0; i < root.childCount; i++)
                RegisterButtonsInHierarchy(root.GetChild(i));
        }

        private void RegisterSlidersInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out Slider slider))
            {
                slider.onValueChanged.RemoveListener(_sliderChangedAction);
                slider.onValueChanged.AddListener(_sliderChangedAction);
            }

            for (int i = 0; i < root.childCount; i++)
                RegisterSlidersInHierarchy(root.GetChild(i));
        }

        private void RegisterTogglesInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out Toggle toggle))
            {
                toggle.onValueChanged.RemoveListener(_toggleChangedAction);
                toggle.onValueChanged.AddListener(_toggleChangedAction);
            }

            for (int i = 0; i < root.childCount; i++)
                RegisterTogglesInHierarchy(root.GetChild(i));
        }

        private void UnregisterButtonsInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out Button button))
            {
                button.onClick.RemoveListener(_primaryButtonClickAction);
                button.onClick.RemoveListener(_secondaryButtonClickAction);
                button.onClick.RemoveListener(_destructiveButtonClickAction);

                if (button.TryGetComponent(out EventTrigger trigger))
                {
                    var entries = trigger.triggers;
                    for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                    {
                        EventTrigger.Entry entry = entries[entryIndex];
                        if (entry != null && entry.eventID == EventTriggerType.PointerEnter)
                            entry.callback.RemoveListener(_buttonHoverAction);
                    }
                }
            }

            for (int i = 0; i < root.childCount; i++)
                UnregisterButtonsInHierarchy(root.GetChild(i));
        }

        private void UnregisterSlidersInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out Slider slider))
                slider.onValueChanged.RemoveListener(_sliderChangedAction);

            for (int i = 0; i < root.childCount; i++)
                UnregisterSlidersInHierarchy(root.GetChild(i));
        }

        private void UnregisterTogglesInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out Toggle toggle))
                toggle.onValueChanged.RemoveListener(_toggleChangedAction);

            for (int i = 0; i < root.childCount; i++)
                UnregisterTogglesInHierarchy(root.GetChild(i));
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

            if (enableHoverSounds)
            {
                if (button.TryGetComponent(out EventTrigger trigger) &&
                    TryGetPointerEnterEntry(trigger, out EventTrigger.Entry entry))
                {
                    entry.callback.RemoveListener(_buttonHoverAction);
                    entry.callback.AddListener(_buttonHoverAction);
                }
            }
        }

        private static bool TryGetPointerEnterEntry(EventTrigger trigger, out EventTrigger.Entry pointerEnterEntry)
        {
            pointerEnterEntry = null;
            if (trigger == null)
                return false;

            var entries = trigger.triggers;
            for (int i = 0; i < entries.Count; i++)
            {
                EventTrigger.Entry entry = entries[i];
                if (entry != null && entry.eventID == EventTriggerType.PointerEnter)
                {
                    pointerEnterEntry = entry;
                    return true;
                }
            }

            return false;
        }

        private ButtonType ResolveButtonType(Button button)
        {
            if (IsButtonInArray(button, primaryButtons))
                return ButtonType.Primary;

            if (IsButtonInArray(button, destructiveButtons))
                return ButtonType.Destructive;

            return ButtonType.Secondary;
        }

        private static bool IsButtonInArray(Button button, Button[] buttons)
        {
            if (button == null || buttons == null)
                return false;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (ReferenceEquals(buttons[i], button))
                    return true;
            }

            return false;
        }

        // ----------------------------------------------------------
        // CALLBACKS
        // ----------------------------------------------------------

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
            float currentTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
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

        // ----------------------------------------------------------
        // AUDIO PLAYBACK
        // ----------------------------------------------------------

        private void PlaySound(AudioClip clip, float vol)
        {
            if (_audioManager == null)
                return;

            _audioManager.PlayStatic2D(clip, vol, _audioManager.InterfaceGroup);
            _totalSoundsPlayed++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugLog)
            {
                float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
                if (now >= _nextDebugLogTime)
                {
                    _nextDebugLogTime = now + 1f;
                    Hecton8.Core.H8Debug.Log("[UIAudioFeedback] Playback event.", this);
                }
            }
#endif
        }

        // ----------------------------------------------------------
        // TYPES
        // ----------------------------------------------------------

        private enum ButtonType
        {
            Primary,
            Secondary,
            Destructive
        }
    }
}
