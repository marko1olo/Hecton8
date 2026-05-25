using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Hecton8.Audio;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Automatic audio trigger for UI buttons.
    /// Detects button type (Primary/Secondary/Destructive) and plays appropriate sound.
    /// Zero-GC: cached AudioClip references, no allocations on click.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Hecton8/UI/UI Button Audio Trigger")]
    public sealed class UIButtonAudioTrigger : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        // TYPES
        // ══════════════════════════════════════════════════════════

        public enum ButtonType
        {
            Primary,      // New Game, Resume, Save, Apply
            Secondary,    // Back, Cancel, Settings
            Destructive   // Quit, Delete, Reset
        }

        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== BUTTON TYPE ===")]
        [SerializeField] private ButtonType buttonType = ButtonType.Primary;

        [Header("=== AUDIO CLIPS ===")]
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private float volume = 1f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private Button _button;
        private IAudioService _audioManager;
        private UnityAction _cachedClickAction;
        private bool _hotSwapListenerRegistered;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            TryGetComponent(out _button);
            _audioManager = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
            _cachedClickAction = OnButtonClicked; // COLD ALLOC: UnityAction[1] — cached UI click audio listener — owner: UIButtonAudioTrigger
        }

        private void OnEnable()
        {
            _audioManager = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
            TryRegisterHotSwapListener();
            if (_button != null)
                _button.onClick.AddListener(_cachedClickAction);
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(_cachedClickAction);
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                _audioManager = currentService as IAudioService;
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS
        // ══════════════════════════════════════════════════════════

        private void OnButtonClicked()
        {
            if (_audioManager == null || clickSound == null)
                return;

            _audioManager.PlayStatic2D(clickSound, volume, _audioManager.InterfaceGroup);
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

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Set button type dynamically.
        /// </summary>
        public void SetButtonType(ButtonType type)
        {
            buttonType = type;
        }

        /// <summary>
        /// Set custom click sound.
        /// </summary>
        public void SetClickSound(AudioClip clip)
        {
            clickSound = clip;
        }
    }
}
