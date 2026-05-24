using System;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Deterministic runtime variation payload applied to one spawned fauna instance.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FaunaGeneticTraits
    {
        public ulong Genome;
        public ulong BaseGenome;
        public float BaseScaleMultiplier;
        public float BaseSpeedMultiplier;
        public float BaseHealthMultiplier;
        public float ScaleMultiplier;
        public float SpeedMultiplier;
        public float HealthMultiplier;
        public float AggressionMultiplier;
        public float HueOffset01;
        public float MutationHueShift01;
        public float MutationTwitch01;
        public uint MutationFlags;
        public uint ContaminatedMeatHash;
        public uint VariationHash;
    }
}
