namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Mod-facing mutation overlay that biases deterministic fauna genetics inside one biome.
    /// </summary>
    public sealed class FaunaBiomeMutationDefinition
    {
        /// <summary>Stable biome identifier that should receive the mutation overlay.</summary>
        public int BiomeId { get; set; }

        /// <summary>
        /// Optional creature ID filter.
        /// Empty means the mutation applies to every fauna archetype spawned in the biome.
        /// </summary>
        public string SpeciesId { get; set; } = string.Empty;

        /// <summary>Minimum additional scale multiplier applied by this mutation.</summary>
        public float MinScaleMultiplier { get; set; } = 1f;

        /// <summary>Maximum additional scale multiplier applied by this mutation.</summary>
        public float MaxScaleMultiplier { get; set; } = 1f;

        /// <summary>Speed multiplier applied after the deterministic base genetic pass.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Health multiplier applied after the deterministic base genetic pass.</summary>
        public float HealthMultiplier { get; set; } = 1f;
    }
}
