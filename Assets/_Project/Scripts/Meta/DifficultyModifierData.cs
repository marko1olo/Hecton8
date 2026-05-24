using System;

namespace Hecton8.Meta
{
    /// <summary>
    /// Runtime difficulty modifiers derived from player performance telemetry.
    /// Systems may read this snapshot to scale survival pressure without hard difficulty toggles.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct DifficultyModifierData
    {
        /// <summary>
        /// Multiplier applied to incoming integrity damage.
        /// Values below one make the run easier; values above one increase lethality.
        /// </summary>
        public float DamageMultiplier;

        /// <summary>
        /// Multiplier applied to oxygen depletion.
        /// Values below one slow oxygen drain.
        /// </summary>
        public float OxygenDepletionRate;

        /// <summary>
        /// Multiplier exposed for fauna and threat systems to scale aggression.
        /// </summary>
        public float PredatorAggressionScale;

        /// <summary>
        /// Returns the neutral modifier set that preserves authored balance.
        /// </summary>
        public static DifficultyModifierData Default => new DifficultyModifierData
        {
            DamageMultiplier = 1f,
            OxygenDepletionRate = 1f,
            PredatorAggressionScale = 1f
        };
    }
}
