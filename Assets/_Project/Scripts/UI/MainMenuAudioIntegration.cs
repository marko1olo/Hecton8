using UnityEngine;
using Hecton.UI.MainMenu;

namespace Hecton8.UI
{
    /// <summary>
    /// Audio integration for MainMenuController.
    /// Plays panel open/close sounds and button clicks.
    /// Zero-GC: static calls to UIAudioFeedback, no allocations.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MainMenuController))]
    [AddComponentMenu("Hecton8/UI/Main Menu Audio Integration")]
    public sealed class MainMenuAudioIntegration : MonoBehaviour
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

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private MainMenuController _controller;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            TryGetComponent(out _controller);
        }

        private void Start()
        {
            if (!enableAudio)
                return;

            // Hook into MainMenuController events (if available)
            // For now, provide public methods for manual integration
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Play panel open sound.
        /// Call from MainMenuController when opening a panel.
        /// </summary>
        public void OnPanelOpened()
        {
            if (!enableAudio || !playPanelSounds)
                return;

            UIAudioFeedback.PlayPanelOpen();
        }

        /// <summary>
        /// Play panel close sound.
        /// Call from MainMenuController when closing a panel.
        /// </summary>
        public void OnPanelClosed()
        {
            if (!enableAudio || !playPanelSounds)
                return;

            UIAudioFeedback.PlayPanelClose();
        }

        /// <summary>
        /// Play primary button click sound.
        /// Call for main actions: New Game, Resume, Load.
        /// </summary>
        public void OnPrimaryButtonClicked()
        {
            if (!enableAudio || !playButtonSounds)
                return;

            UIAudioFeedback.PlayClickPrimary();
        }

        /// <summary>
        /// Play secondary button click sound.
        /// Call for navigation: Back, Settings.
        /// </summary>
        public void OnSecondaryButtonClicked()
        {
            if (!enableAudio || !playButtonSounds)
                return;

            UIAudioFeedback.PlayClickSecondary();
        }

        /// <summary>
        /// Play destructive button click sound.
        /// Call for destructive actions: Quit, Exit to Menu.
        /// </summary>
        public void OnDestructiveButtonClicked()
        {
            if (!enableAudio || !playButtonSounds)
                return;

            UIAudioFeedback.PlayClickDestructive();
        }
    }
}
