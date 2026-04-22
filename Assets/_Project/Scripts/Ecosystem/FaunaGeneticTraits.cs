using System;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Deterministic runtime variation payload applied to one spawned fauna instance.
    /// </summary>
    [Serializable]
    public struct FaunaGeneticTraits
    {
        public float ScaleMultiplier;
        public float SpeedMultiplier;
        public float HealthMultiplier;
        public uint VariationHash;
    }
}
