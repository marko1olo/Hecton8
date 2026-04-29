using UnityEngine;
using UnityEngine.UI;
using Hecton8.Audio;

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
    public sealed class UIButtonAudioTrigger : MonoBehaviour
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
        private Hecton8.Core.IAudioService _audioManager;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _button = GetComponent<Button>();
            _audioManager = Hecton8.Core.GlobalRegistry.Audio;
        }

        private void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(OnButtonClicked);
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClicked);
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
