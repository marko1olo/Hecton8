// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  CaveDressingConfig.cs — Project HECTON-8 Cave Interior Dressing System    ║
// ║  Unity 6 | URP Shaders | Zero runtime allocation                          ║
// ║  v1.0 — Production-ready cheap cave detail layer                           ║
// ║                                                                             ║
// ║  PURPOSE:                                                                   ║
// ║  ─────────                                                                  ║
// ║  Defines visual dressing for cave interiors (cheap, shader-based, or       ║
// ║  simple mesh overlays). Includes mineral crust, sediment shelves, fungi.   ║
// ║  All dressing is biome-specific and hazard-aware.                          ║
// ║                                                                             ║
// ║  CATEGORIES:                                                                ║
// ║  ────────────                                                              ║
// ║  • Mineral Crust: procedural shader overlay on cave walls/ceiling          ║
// ║  • Sediment Shelf: simple mesh planes on floor, auto-positioned            ║
// ║  • Glowing Fungi: particle system or billboard cloud near walls/floor      ║
// ║  • Wall Growth: animated shader effect (sway, flicker)                     ║
// ║  • Deep Anomaly: blue/green glow markers for interesting cave features     ║
// ║                                                                             ║
// ║  ZERO-ALLOC GUARANTEE:                                                     ║
// ║  ────────────────────                                                      ║
// ║  • All configs are [Serializable] struct/class (no runtime allocation)     ║
// ║  • Mesh placement uses pre-allocated arrays                                ║
// ║  • Particle systems spawned once per cave, not per frame                   ║
// ║  • Shader properties cached via PropertyToID                               ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Configuration for mineral crust shader overlay on cave surfaces.
    /// Applied via material property block, no additional meshes required.
    /// </summary>
    [System.Serializable]
    public class MineralCrustConfig
    {
        [Tooltip("Enable mineral crust overlay.")]
        public bool enabled = true;

        [Tooltip("Crust overlay intensity (0-1). Higher = more visible crystal/mineral detail.")]
        [Range(0f, 1f)]
        public float intensity = 0.6f;

        [Tooltip("Color tint for the crust layer.")]
        public Color tint = new Color(0.9f, 0.85f, 0.7f); // pale yellow/tan

        [Tooltip("Scale of the crust pattern (smaller = finer detail).")]
        [Range(0.1f, 2f)]
        public float scale = 0.5f;

        [Tooltip("Roughness increase from crust (makes surface less reflective).")]
        [Range(0f, 1f)]
        public float roughnessBoost = 0.3f;
    }

    /// <summary>
    /// Configuration for sediment/silt shelves on cave floor.
    /// Cheap simple mesh placement, auto-oriented horizontally.
    /// </summary>
    [System.Serializable]
    public class SedimentShelfConfig
    {
        [Tooltip("Enable sediment shelf meshes.")]
        public bool enabled = true;

        [Tooltip("Maximum number of shelf meshes per cave.")]
        [Range(0, 20)]
        public int maxCount = 8;

        [Tooltip("Shelf prefab (simple plane or low-poly mesh).")]
        public GameObject shelfPrefab;

        [Tooltip("Scale range for shelf size variation.")]
        public Vector2 scaleRange = new Vector2(2f, 8f);

        [Tooltip("Height offset from floor (controls depth appearance).")]
        [Range(0f, 2f)]
        public float floorOffset = 0.3f;

        [Tooltip("Opacity of shelf material.")]
        [Range(0f, 1f)]
        public float opacity = 0.7f;

        [Tooltip("Tint color for shelves.")]
        public Color tint = new Color(0.6f, 0.55f, 0.5f); // dark silt
    }

    /// <summary>
    /// Configuration for glowing cave fungi (particle system or billboard).
    /// </summary>
    [System.Serializable]
    public class DeepFungiConfig
    {
        [Tooltip("Enable deep fungi particles/effects.")]
        public bool enabled = true;

        [Tooltip("Fungi intensity/density (0-1).")]
        [Range(0f, 1f)]
        public float density = 0.5f;

        [Tooltip("Glow color of fungi (spectral cue for depth).")]
        public Color glowColor = new Color(0.3f, 1f, 0.7f); // cyan-green

        [Tooltip("Particle emission rate per second.")]
        [Range(0f, 50f)]
        public float emissionRate = 10f;

        [Tooltip("Particle lifespan (seconds).")]
        [Range(0.5f, 5f)]
        public float lifetime = 2f;

        [Tooltip("Particle size in meters.")]
        [Range(0.01f, 0.5f)]
        public float particleSize = 0.1f;

        [Tooltip("Preferred spawn locations: 0=floor, 0.5=mid, 1=ceiling/walls.")]
        [Range(0f, 1f)]
        public float verticalBias = 0.3f;
    }

    /// <summary>
    /// Configuration for animated wall growth (shader-based sway/pulse).
    /// </summary>
    [System.Serializable]
    public class WallGrowthConfig
    {
        [Tooltip("Enable animated wall growth.")]
        public bool enabled = true;

        [Tooltip("Growth sway amplitude (0-1).")]
        [Range(0f, 1f)]
        public float swayAmount = 0.3f;

        [Tooltip("Sway frequency in Hz.")]
        [Range(0.1f, 2f)]
        public float swayFrequency = 0.5f;

        [Tooltip("Color of growing tissue.")]
        public Color growthColor = new Color(0.4f, 0.8f, 0.6f); // pale green

        [Tooltip("Pulse intensity (0-1).")]
        [Range(0f, 1f)]
        public float pulseAmount = 0.2f;
    }

    /// <summary>
    /// Configuration for emissive tissue pads attached to cave walls/ceilings.
    /// Cheap mesh-based glow accents, not dynamic organisms.
    /// </summary>
    [System.Serializable]
    public class GlowingTissueConfig
    {
        [Tooltip("Enable glowing tissue accents.")]
        public bool enabled = true;

        [Tooltip("Maximum number of tissue patches per cave.")]
        [Range(0, 24)]
        public int maxCount = 10;

        [Tooltip("Density/intensity multiplier for tissue placement.")]
        [Range(0f, 1f)]
        public float density = 0.5f;

        [Tooltip("Dark base color of the tissue body.")]
        public Color baseColor = new Color(0.12f, 0.2f, 0.16f);

        [Tooltip("Emission color of the tissue glow.")]
        public Color glowColor = new Color(0.22f, 0.95f, 0.86f);

        [Tooltip("Pulse bias for emissive intensity.")]
        [Range(0f, 1f)]
        public float pulseAmount = 0.3f;
    }

    /// <summary>
    /// Configuration for cheap service-tech remnants inside caves.
    /// Broken pipes, brackets, and panel fragments.
    /// </summary>
    [System.Serializable]
    public class ServiceRemnantConfig
    {
        [Tooltip("Enable service remnant accents.")]
        public bool enabled = true;

        [Tooltip("Spawn only in ruin-linked cave presets.")]
        public bool ruinLinkedOnly = true;

        [Tooltip("Maximum number of remnant pieces per cave.")]
        [Range(0, 12)]
        public int maxCount = 3;

        [Tooltip("Minimum remnant scale.")]
        [Range(0.1f, 6f)]
        public float minScale = 0.35f;

        [Tooltip("Maximum remnant scale.")]
        [Range(0.1f, 8f)]
        public float maxScale = 1.4f;

        [Tooltip("Primary weathered metal color.")]
        public Color baseColor = new Color(0.26f, 0.3f, 0.34f);

        [Tooltip("Accent strip/emission color for tech remnants.")]
        public Color accentColor = new Color(0.16f, 0.72f, 0.9f);

        [Tooltip("Emission strength for accent strips.")]
        [Range(0f, 2f)]
        public float accentEmission = 0.35f;
    }

    /// <summary>
    /// Complete cave dressing configuration (all layers).
    /// One instance per cave or shared across biome family.
    /// </summary>
    [System.Serializable]
    public class CaveDressingConfig
    {
        private static readonly CaveDressingConfig _SharedShallowConfig = CreateShallowConfig(); // COLD ALLOC: one shared shallow dressing graph.
        private static readonly CaveDressingConfig _SharedMidConfig = CreateMidConfig(); // COLD ALLOC: one shared mid-depth dressing graph.
        private static readonly CaveDressingConfig _SharedDeepConfig = CreateDeepConfig(); // COLD ALLOC: one shared deep dressing graph.

        [Header("═══ Cave Dressing Layers ═══")]
        
        public MineralCrustConfig mineralCrust = new MineralCrustConfig();
        public SedimentShelfConfig sedimentShelves = new SedimentShelfConfig();
        public DeepFungiConfig deepFungi = new DeepFungiConfig();
        public WallGrowthConfig wallGrowth = new WallGrowthConfig();
        public GlowingTissueConfig glowingTissue = new GlowingTissueConfig();
        public ServiceRemnantConfig serviceRemnants = new ServiceRemnantConfig();

        [Tooltip("Overall dressing intensity multiplier (0-1).")]
        [Range(0f, 1f)]
        public float globalIntensity = 1f;

        /// <summary>
        /// Creates a shallow cave dressing config (sandy, warm, sparse details).
        /// </summary>
        public static CaveDressingConfig CreateShallowConfig()
        {
            return new CaveDressingConfig
            {
                mineralCrust = new MineralCrustConfig
                {
                    enabled = true,
                    intensity = 0.4f,
                    tint = new Color(1f, 0.9f, 0.7f), // sand-colored
                    scale = 0.7f
                },
                sedimentShelves = new SedimentShelfConfig
                {
                    enabled = true,
                    maxCount = 4,
                    opacity = 0.5f,
                    tint = new Color(0.8f, 0.7f, 0.5f) // light sand
                },
                deepFungi = new DeepFungiConfig
                {
                    enabled = true,
                    density = 0.2f,
                    glowColor = new Color(1f, 0.8f, 0.2f), // warm biolum
                    emissionRate = 5f
                },
                wallGrowth = new WallGrowthConfig
                {
                    enabled = false // sparse in shallow caves
                },
                glowingTissue = new GlowingTissueConfig
                {
                    enabled = true,
                    maxCount = 4,
                    density = 0.22f,
                    baseColor = new Color(0.18f, 0.16f, 0.1f),
                    glowColor = new Color(1f, 0.72f, 0.25f),
                    pulseAmount = 0.18f
                },
                serviceRemnants = new ServiceRemnantConfig
                {
                    enabled = false,
                    maxCount = 0
                },
                globalIntensity = 0.7f
            };
        }

        /// <summary>
        /// Creates a mid-depth cave dressing config (balanced detail).
        /// </summary>
        public static CaveDressingConfig CreateMidConfig()
        {
            return new CaveDressingConfig
            {
                mineralCrust = new MineralCrustConfig
                {
                    enabled = true,
                    intensity = 0.6f,
                    tint = new Color(0.8f, 0.8f, 0.85f), // grey stone
                    scale = 0.5f
                },
                sedimentShelves = new SedimentShelfConfig
                {
                    enabled = true,
                    maxCount = 8,
                    opacity = 0.6f
                },
                deepFungi = new DeepFungiConfig
                {
                    enabled = true,
                    density = 0.4f,
                    glowColor = new Color(0.5f, 1f, 0.8f), // cyan
                    emissionRate = 12f
                },
                wallGrowth = new WallGrowthConfig
                {
                    enabled = true,
                    swayAmount = 0.2f,
                    pulseAmount = 0.15f
                },
                glowingTissue = new GlowingTissueConfig
                {
                    enabled = true,
                    maxCount = 8,
                    density = 0.45f,
                    baseColor = new Color(0.16f, 0.22f, 0.2f),
                    glowColor = new Color(0.48f, 0.95f, 0.84f),
                    pulseAmount = 0.28f
                },
                serviceRemnants = new ServiceRemnantConfig
                {
                    enabled = true,
                    ruinLinkedOnly = true,
                    maxCount = 3,
                    minScale = 0.4f,
                    maxScale = 1.6f,
                    accentEmission = 0.42f
                },
                globalIntensity = 1.0f
            };
        }

        /// <summary>
        /// Creates a deep cave dressing config (alien, exotic, intense).
        /// </summary>
        public static CaveDressingConfig CreateDeepConfig()
        {
            return new CaveDressingConfig
            {
                mineralCrust = new MineralCrustConfig
                {
                    enabled = true,
                    intensity = 0.8f,
                    tint = new Color(0.3f, 0.6f, 0.9f), // deep blue/purple
                    scale = 0.3f
                },
                sedimentShelves = new SedimentShelfConfig
                {
                    enabled = true,
                    maxCount = 12,
                    opacity = 0.7f,
                    tint = new Color(0.2f, 0.3f, 0.4f) // very dark
                },
                deepFungi = new DeepFungiConfig
                {
                    enabled = true,
                    density = 0.7f,
                    glowColor = new Color(0.2f, 0.9f, 1f), // bright cyan/phosphorescent
                    emissionRate = 25f,
                    lifetime = 3f
                },
                wallGrowth = new WallGrowthConfig
                {
                    enabled = true,
                    swayAmount = 0.4f,
                    pulseAmount = 0.3f,
                    growthColor = new Color(0.2f, 1f, 0.8f)
                },
                glowingTissue = new GlowingTissueConfig
                {
                    enabled = true,
                    maxCount = 14,
                    density = 0.7f,
                    baseColor = new Color(0.08f, 0.16f, 0.18f),
                    glowColor = new Color(0.18f, 0.94f, 1f),
                    pulseAmount = 0.45f
                },
                serviceRemnants = new ServiceRemnantConfig
                {
                    enabled = true,
                    ruinLinkedOnly = true,
                    maxCount = 2,
                    minScale = 0.32f,
                    maxScale = 1.2f,
                    baseColor = new Color(0.18f, 0.22f, 0.28f),
                    accentColor = new Color(0.12f, 0.82f, 1f),
                    accentEmission = 0.55f
                },
                globalIntensity = 1.2f
            };
        }

        /// <summary>
        /// Gets shared dressing config based on spawn context.
        /// Returned instances are treated as read-only runtime templates.
        /// </summary>
        public static CaveDressingConfig GetConfigForContext(SpawnContext context)
        {
            return context switch
            {
                SpawnContext.CaveShallow => _SharedShallowConfig,
                SpawnContext.CaveMid => _SharedMidConfig,
                SpawnContext.CaveDeep => _SharedDeepConfig,
                _ => _SharedMidConfig
            };
        }
    }
}
