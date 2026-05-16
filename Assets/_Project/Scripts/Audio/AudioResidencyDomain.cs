using System;

namespace Hecton8.Audio
{
    /// <summary>
    /// Fixed first-party audio residency domains used by import tooling and runtime residency guards.
    /// </summary>
    public enum AudioResidencyDomain : byte
    {
        /// <summary>Long-form music, stingers, and score beds.</summary>
        Music = 0,

        /// <summary>Player body, locomotion, breath, suit, and held-equipment audio.</summary>
        Player = 1,

        /// <summary>Creature, predator, fauna, and threat audio banks.</summary>
        Creatures = 2,

        /// <summary>Biome, ambient, weather, machinery, and world bed audio.</summary>
        Environment = 3,

        /// <summary>Interface, visor, PDA, menu, and helmet UI audio.</summary>
        Interface = 4
    }

    /// <summary>
    /// Utility helpers for the fixed audio residency domains.
    /// </summary>
    public static class AudioResidencyDomainUtility
    {
        /// <summary>
        /// Number of first-party audio residency domains.
        /// </summary>
        public const int DomainCount = 5;

        /// <summary>
        /// Returns true when the domain value is one of the fixed first-party domains.
        /// </summary>
        public static bool IsValid(AudioResidencyDomain domain)
        {
            return domain >= AudioResidencyDomain.Music && domain <= AudioResidencyDomain.Interface;
        }

        /// <summary>
        /// Returns the stable domain label for diagnostics.
        /// </summary>
        public static ReadOnlySpan<char> GetLabel(AudioResidencyDomain domain)
        {
            switch (domain)
            {
                case AudioResidencyDomain.Music:
                    return "Music".AsSpan();
                case AudioResidencyDomain.Player:
                    return "Player".AsSpan();
                case AudioResidencyDomain.Creatures:
                    return "Creatures".AsSpan();
                case AudioResidencyDomain.Environment:
                    return "Environment".AsSpan();
                case AudioResidencyDomain.Interface:
                    return "Interface".AsSpan();
                default:
                    return "Unknown".AsSpan();
            }
        }
    }
}
