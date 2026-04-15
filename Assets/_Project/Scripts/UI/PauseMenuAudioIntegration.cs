using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Audio integration for PauseMenuController.
    /// Plays panel open/close sounds and button clicks.
    /// Supports unscaled time (works when Time.timeScale = 0).
    /// Zero-GC: static calls to UIAudioFeedback, no allocations.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Pause Menu Audio Integration")]
    public sealed class PauseMenuAudioIntegration : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Enable audio feedback")]
        private bool enableAudio = true;

        [SerializeField, Tooltip("Play panel sounds on open/close")]
        private bool playPanelSounds = true;

        [SerializeField, Tooltip("Play button click sounds")]
        private bool playButtonSounds = true;

        [Header("=== PAUSE MENU SPECIFIC ===")]
        [SerializeField, Tooltip("Play audio even when Time.timeScale = 0")]
        private bool playWhenPaused = true;

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Play pause menu open sound.
        /// Call when pause menu opens (Time.timeScale = 0).
        /// </summary>
        public void OnPauseMenuOpened()
        {
            if (!enableAudio || !playPanelSounds)
                return;

            if (!playWhenPaused && Time.timeScale == 0f)
                return;

            UIAudioFeedback.PlayPanelOpen();
        }

        /// <summary>
        /// Play pause menu close sound.
        /// Call when pause menu closes (Time.timeScale restored).
        /// </summary>
        public void OnPauseMenuClosed()
        {
            if (!enableAudio || !playPanelSounds)
                return;

            if (!playWhenPaused && Time.timeScale == 0f)
                return;

            UIAudioFeedback.PlayPanelClose();
        }

        /// <summary>
        /// Play primary button click sound.
        /// Call for main actions: Resume, Save.
        /// </summary>
        public void OnPrimaryButtonClicked()
        {
            if (!enableAudio || !playButtonSounds)
                return;

            if (!playWhenPaused && Time.timeScale == 0f)
                return;

            UIAudioFeedback.PlayClickPrimary();
        }

        /// <summary>
        /// Play secondary button click sound.
        /// Call for navigation: Settings, Field Guide.
        /// </summary>
        public void OnSecondaryButtonClicked()
        {
            if (!enableAudio || !playButtonSounds)
                return;

            if (!playWhenPaused && Time.timeScale == 0f)
                return;

            UIAudioFeedback.PlayClickSecondary();
        }

        /// <summary>
        /// Play destructive button click sound.
        /// Call for destructive actions: Exit to Main Menu.
        /// </summary>
        public void OnDestructiveButtonClicked()
        {
            if (!enableAudio || !playButtonSounds)
                return;

            if (!playWhenPaused && Time.timeScale == 0f)
                return;

            UIAudioFeedback.PlayClickDestructive();
        }
    }
}
