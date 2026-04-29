using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Authored territory tuning for apex predator disputes and intimidation pressure.
    /// </summary>
    [CreateAssetMenu(fileName = "ApexTerritoryProfile_", menuName = "Hecton8/Fauna/Apex Territory Profile", order = 113)]
    public sealed class ApexTerritoryProfile : ScriptableObject
    {
        [Header("Territory")]
        [Tooltip("Radius in meters used when resolving rival apex territory overlap.")]
        [Min(25f)]
        public float territoryRadiusMeters = 500f;

        [Tooltip("Multiplier injected into apex-on-apex aggression utility while a rival remains inside the active territory band.")]
        [Min(1f)]
        public float aggressionMultiplierAgainstRivals = 1.35f;

        [Tooltip("Seconds of forced migration/flee pressure applied to the losing apex after a territorial defeat.")]
        [Min(1f)]
        public float forcedRetreatDurationSeconds = 18f;

        [Header("Intimidation")]
        [Tooltip("Seconds the dominant apex keeps its intimidation aura after winning a dispute.")]
        [Min(1f)]
        public float intimidationDurationSeconds = 24f;

        [Tooltip("Radius in meters smaller predators should avoid while the dominant apex intimidation aura is active.")]
        [Min(1f)]
        public float intimidationRadiusMeters = 100f;

        [Tooltip("Preferred biome tokens used for authored filtering and ledger documentation.")]
        public string[] preferredBiomeTokens = { "abyss", "trench", "reef" };
    }
}
