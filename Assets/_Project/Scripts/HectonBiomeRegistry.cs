using System;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Definitive registry for the 108 biomes of Hecton8.
    /// Stores names, depth ranges, and cardinal regions.
    /// </summary>
    [CreateAssetMenu(fileName = "HectonBiomeRegistry", menuName = "Hecton8/Registry/Biome Registry")]
    public sealed class HectonBiomeRegistry : ScriptableObject
    {
        [Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct BiomeEntry
        {
            public int id;
            public string name;
            public string region; // North, South, East, West
            public int tier;      // 1-27
            [TextArea(3, 5)] public string description;
        }

        public BiomeEntry[] biomes = new BiomeEntry[108];

        public BiomeEntry GetBiome(int id)
        {
            if (id < 1 || id > 108) return default;
            return biomes[id - 1];
        }

        /// <summary>
        /// Populates registry from a raw string format (for automated parsing).
        /// </summary>
        public void BatchUpdate(int index, string name, string region, int tier, string desc)
        {
            if (index < 0 || index >= 108) return;
            biomes[index] = new BiomeEntry
            {
                id = index + 1,
                name = name,
                region = region,
                tier = tier,
                description = desc
            };
        }
    }
}
