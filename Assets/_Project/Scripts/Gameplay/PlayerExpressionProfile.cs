using Hecton8.Tools;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(
        fileName = "PlayerExpressionProfile",
        menuName = "Hecton8/Customization/Player Expression Profile",
        order = 115)]
    public sealed class PlayerExpressionProfile : ScriptableObject
    {
        [Header("── Identity ──────────────────────────────────")]
        [Tooltip("Stable save ID for this expression profile.")]
        [SerializeField] private string profileId = "expression.standard";

        [Tooltip("Player-facing profile name shown in PDA and logs.")]
        [SerializeField] private string displayName = "EXPEDITION STANDARD";

        [Tooltip("Optional HUD suit label override. If empty, HUD profile label is used.")]
        [SerializeField] private string hudLabelOverride = "STANDARD";

        [Tooltip("Short player-facing summary explaining the fantasy of this profile.")]
        [TextArea(2, 4)]
        [SerializeField] private string summary =
            "Balanced expedition identity for general field work.";

        [Tooltip("Whether the profile is available from the start.")]
        [SerializeField] private bool unlockedByDefault = true;

        [Header("── Presentation ──────────────────────────────")]
        [Tooltip("HUD profile applied while this identity is active.")]
        [SerializeField] private SuitHUDProfile hudProfile;

        [Tooltip("Recommended suit shell for this expression profile.")]
        [SerializeField] private SuitData recommendedSuit;

        [Header("── Field Kit ─────────────────────────────────")]
        [Tooltip("Recommended quick-slot preset linked to this identity.")]
        [SerializeField] private ToolLoadoutPreset recommendedLoadout;

        /// <summary>Stable save ID for this expression profile.</summary>
        public string ProfileId => profileId;

        /// <summary>Player-facing profile name shown in PDA and notifications.</summary>
        public string DisplayName => displayName;

        /// <summary>HUD suit label override. Empty means use the HUD profile label.</summary>
        public string HudLabelOverride => hudLabelOverride;

        /// <summary>Short fantasy/utility summary for this identity.</summary>
        public string Summary => summary;

        /// <summary>Whether this profile is available by default.</summary>
        public bool UnlockedByDefault => unlockedByDefault;

        /// <summary>HUD profile applied by this identity.</summary>
        public SuitHUDProfile HudProfile => hudProfile;

        /// <summary>Recommended suit shell for this identity.</summary>
        public SuitData RecommendedSuit => recommendedSuit;

        /// <summary>Recommended tool loadout for this identity.</summary>
        public ToolLoadoutPreset RecommendedLoadout => recommendedLoadout;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(profileId))
                profileId = $"expression.{name.ToLowerInvariant().Replace(' ', '_')}";

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name.Replace('_', ' ').ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(hudLabelOverride))
                hudLabelOverride = displayName;
        }
#endif
    }
}
