using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Data-owned library for resolving swim presentation profiles per suit family.
    /// Keeps suit-to-profile authoring out of the player prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "SwimPresentationProfileLibrary", menuName = "Hecton8/Swim Presentation Profile Library", order = 126)]
    public sealed class SwimPresentationProfileLibrary : ScriptableObject
    {
        private const float UtilitySuitMassThreshold = 120f;
        private const float HeavySuitMassThreshold = 220f;

        [System.Serializable]
        private struct SuitPresentationBinding
        {
            [Tooltip("Suit asset that should drive this swim presentation profile.")]
            public SuitData suit;

            [Tooltip("Presentation profile used when this suit is active.")]
            public SwimPresentationProfile profile;
        }

        [Header("── Explicit Bindings ──────────────────")]
        [Tooltip("Exact per-suit profile overrides. Use this for hero suits and future special rigs.")]
        [SerializeField] private SuitPresentationBinding[] suitBindings;

        [Header("── Family Fallbacks ───────────────────")]
        [Tooltip("Fallback profile for light / standard suits when no explicit binding exists.")]
        [SerializeField] private SwimPresentationProfile fallbackLightProfile;

        [Tooltip("Fallback profile for technical / utility suits when no explicit binding exists.")]
        [SerializeField] private SwimPresentationProfile fallbackUtilityProfile;

        [Tooltip("Fallback profile for heavy industrial suits when no explicit binding exists.")]
        [SerializeField] private SwimPresentationProfile fallbackHeavyProfile;

        /// <summary>
        /// Resolves the best swim presentation profile for the supplied suit.
        /// </summary>
        /// <param name="suit">Current active suit. Can be null.</param>
        /// <returns>Resolved presentation profile, or null if no usable profile exists.</returns>
        public SwimPresentationProfile ResolveProfile(SuitData suit)
        {
            if (suit != null && suitBindings != null)
            {
                for (int i = 0; i < suitBindings.Length; i++)
                {
                    if (ReferenceEquals(suitBindings[i].suit, suit) && suitBindings[i].profile != null)
                        return suitBindings[i].profile;
                }
            }

            if (suit != null)
            {
                if (suit.mass >= HeavySuitMassThreshold && fallbackHeavyProfile != null)
                    return fallbackHeavyProfile;

                if (suit.mass >= UtilitySuitMassThreshold && fallbackUtilityProfile != null)
                    return fallbackUtilityProfile;
            }

            if (fallbackLightProfile != null)
                return fallbackLightProfile;

            if (fallbackUtilityProfile != null)
                return fallbackUtilityProfile;

            return fallbackHeavyProfile;
        }
    }
}
