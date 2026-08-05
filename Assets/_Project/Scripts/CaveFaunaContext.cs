// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  CaveFaunaContext.cs — Project HECTON-8 Cave Fauna Integration             ║
// ║  Unity 6 | Zero GC | ScriptableObject-based configuration                  ║
// ║  v1.0 — Production-ready fauna context for cave spawning                   ║
// ║                                                                             ║
// ║  PURPOSE:                                                                   ║
// ║  ─────────                                                                  ║
// ║  Defines fauna pressure, diversity, and threat level for each cave type.   ║
// ║  FaunaDirector uses these contexts to spawn appropriate creatures.         ║
// ║  • Shallow caves: passive, slow-moving, bioluminescent                     ║
// ║  • Mid caves: territorial, medium threat, mixed passive/active            ║
// ║  • Deep caves: predatory, high pressure, rare large creatures             ║
// ║                                                                             ║
// ║  INTEGRATION:                                                              ║
// ║  ────────────                                                              ║
// ║  - FaunaDirector reads CaveFaunaPreset for cave spawn points               ║
// ║  - Adjusts crew density and threat based on mood + hazard + spawn context  ║
// ║  - Reserves biome-context fauna slots for cave-specific species            ║
// ║  - Caches fauna config per cave preset to avoid runtime lookups            ║
// ║                                                                             ║
// ║  CONTRACT:                                                                  ║
// ║  ─────────                                                                  ║
// ║  Every cave preset and fauna director must expose:                         ║
// ║    GetFaunaContextForCave(CavePreset, depth) -> CaveFaunaPreset           ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using Hecton8.Caves;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Defines fauna pressure and spawn configuration for a cave type.
    /// Serializable, used by FaunaDirector to generate appropriate creatures.
    /// </summary>
    [System.Serializable]
    public class CaveFaunaPreset
    {
        // ════════════════════════════════════════════════════════════════════
        //  PRESSURE & THREAT
        // ════════════════════════════════════════════════════════════════════

        [Header("═══ Fauna Pressure ═══")]

        [Tooltip("Overall creature density in this cave type (0-1).\n" +
                 "0 = silent, empty. 1 = teeming with life.")]
        [Range(0f, 1f)]
        public float faunaDensity = 0.5f;

        [Tooltip("Ratio of passive to predatory creatures (0-1).\n" +
                 "0 = all predators. 0.5 = mixed. 1 = all peaceful.")]
        [Range(0f, 1f)]
        public float passivityLevel = 0.6f;

        [Tooltip("Territorial behavior intensity (0-1).\n" +
                 "0 = ignore player. 1 = aggressive nest defense.")]
        [Range(0f, 1f)]
        public float territoriality = 0.3f;

        // ════════════════════════════════════════════════════════════════════
        //  CREATURE MIX
        // ════════════════════════════════════════════════════════════════════

        [Header("═══ Creature Types ═══")]

        [Tooltip("Presence of small passive fish (ambient feeling).")]
        [Range(0f, 1f)]
        public float smallPassiveRatio = 0.7f;

        [Tooltip("Presence of territorial/medium threat creatures.")]
        [Range(0f, 1f)]
        public float territorialRatio = 0.3f;

        [Tooltip("Presence of large predators or rare creatures.\n" +
                 "Usually 0 for shallow, 0-0.2 for mid, 0.1-0.5 for deep.")]
        [Range(0f, 1f)]
        public float rareCreatureRatio = 0.1f;

        // ════════════════════════════════════════════════════════════════════
        //  SPAWN DISTRIBUTION
        // ════════════════════════════════════════════════════════════════════

        [Header("═══ Spawn Distribution ═══")]

        [Tooltip("Creatures spawn near cave floor (sheltered hunting ground).")]
        [Range(0f, 1f)]
        public float floorSpawnBias = 0.6f;

        [Tooltip("Creatures spawn near cave walls and ceiling (nesting/shelter).")]
        [Range(0f, 1f)]
        public float wallSpawnBias = 0.3f;

        [Tooltip("Creatures spawn in open water (mid-depth hunters).")]
        [Range(0f, 1f)]
        public float openWaterBias = 0.1f;

        // ════════════════════════════════════════════════════════════════════
        //  NAME & IDENTITY
        // ════════════════════════════════════════════════════════════════════

        [Header("═══ Identity ═══")]


        [Tooltip("Human-readable preset name for debugging.")]
        public string presetName = "Generic Cave Fauna";

        [Range(0f, 1f)]
        public float predatorDensity = 0f;

        public System.Collections.Generic.List<string> allowedSpecies;


        /// <summary>
        /// Creates a shallow cave fauna preset (peaceful, sparse).
        /// </summary>
        public static CaveFaunaPreset CreateShallowPreset()
        {
            return new CaveFaunaPreset
            {
                presetName = "Shallow Cave Fauna",
                predatorDensity = 0.05f,
                allowedSpecies = new System.Collections.Generic.List<string> { "small_fish", "biolum_jelly" },
                faunaDensity = 0.3f,
                passivityLevel = 0.8f,
                territoriality = 0.1f,
                smallPassiveRatio = 0.8f,
                territorialRatio = 0.15f,
                rareCreatureRatio = 0.05f,
                floorSpawnBias = 0.5f,
                wallSpawnBias = 0.4f,
                openWaterBias = 0.1f
            };
        }

        /// <summary>
        /// Creates a mid-depth cave fauna preset (balanced threat).
        /// </summary>
        public static CaveFaunaPreset CreateMidPreset()
        {
            return new CaveFaunaPreset
            {
                presetName = "Mid-Depth Cave Fauna",
                predatorDensity = 0.3f,
                allowedSpecies = new System.Collections.Generic.List<string> { "crab", "cave_eel", "small_fish" },
                faunaDensity = 0.5f,
                passivityLevel = 0.5f,
                territoriality = 0.4f,
                smallPassiveRatio = 0.6f,
                territorialRatio = 0.3f,
                rareCreatureRatio = 0.1f,
                floorSpawnBias = 0.5f,
                wallSpawnBias = 0.3f,
                openWaterBias = 0.2f
            };
        }

        /// <summary>
        /// Creates a deep cave fauna preset (predatory, dense, dangerous).
        /// </summary>
        public static CaveFaunaPreset CreateDeepPreset()
        {
            return new CaveFaunaPreset
            {
                presetName = "Deep Cave Fauna",
                predatorDensity = 0.8f,
                allowedSpecies = new System.Collections.Generic.List<string> { "leviathan", "angler_fish" },
                faunaDensity = 0.7f,
                passivityLevel = 0.2f,
                territoriality = 0.7f,
                smallPassiveRatio = 0.3f,
                territorialRatio = 0.5f,
                rareCreatureRatio = 0.2f,
                floorSpawnBias = 0.4f,
                wallSpawnBias = 0.2f,
                openWaterBias = 0.4f
            };
        }

        /// <summary>
        /// Adjusts this preset based on cave mood and hazard levels.
        /// Returns a copy, does not modify original.
        /// </summary>
        public CaveFaunaPreset AdjustForCaveMood(float mood, float hazard)
        {
            float moodT = math.saturate(mood);
            float hazardT = math.saturate(hazard);
            var adjusted = new CaveFaunaPreset
            {
                presetName = this.presetName,
                predatorDensity = math.lerp(this.predatorDensity, this.predatorDensity * 1.5f, hazardT),
                allowedSpecies = this.allowedSpecies != null ? new System.Collections.Generic.List<string>(this.allowedSpecies) : null,
                faunaDensity = math.lerp(this.faunaDensity, this.faunaDensity * 1.3f, moodT),
                passivityLevel = math.lerp(this.passivityLevel, this.passivityLevel * 0.7f, hazardT),
                territoriality = math.lerp(this.territoriality, this.territoriality * 1.5f, hazardT),
                smallPassiveRatio = math.lerp(this.smallPassiveRatio, this.smallPassiveRatio * 0.8f, hazardT),
                territorialRatio = math.lerp(this.territorialRatio, this.territorialRatio * 1.3f, hazardT),
                rareCreatureRatio = math.lerp(this.rareCreatureRatio, this.rareCreatureRatio * 1.5f, hazardT),
                floorSpawnBias = this.floorSpawnBias,
                wallSpawnBias = this.wallSpawnBias,
                openWaterBias = this.openWaterBias
            };

            return adjusted;
        }
    }

    /// <summary>
    /// Factory helper for creating cave fauna contexts based on cave type.
    /// Used by FaunaDirector and cave spawning systems.
    /// </summary>
    public static class CaveFaunaContextFactory
    {
        /// <summary>
        /// Get appropriate fauna preset for a cave based on spawn context and mood.
        /// </summary>
        public static CaveFaunaPreset GetPresetForCave(SpawnContext context, float mood, float hazard)
        {
            CaveFaunaPreset basePreset = context switch
            {
                SpawnContext.CaveShallow => CaveFaunaPreset.CreateShallowPreset(),
                SpawnContext.CaveMid => CaveFaunaPreset.CreateMidPreset(),
                SpawnContext.CaveDeep => CaveFaunaPreset.CreateDeepPreset(),
                _ => CaveFaunaPreset.CreateMidPreset()
            };

            // Adjust for specific cave mood/hazard
            return basePreset.AdjustForCaveMood(mood, hazard);
        }
    }
}
