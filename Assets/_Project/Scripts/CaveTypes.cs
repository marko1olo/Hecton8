// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  CaveTypes.cs — Project HECTON-8 Cave System Data Types                    ║
// ║  Unity 6 (URP) | Burst-compatible | Zero GC                               ║
// ║  v1.0 — Foundation structures for procedural cave generation              ║
// ║                                                                             ║
// ║  CONTENTS:                                                                  ║
// ║  ─────────                                                                  ║
// ║  1. Enums: CaveRoomType, CaveTunnelType, CaveStructureType, CavePresetType ║
// ║  2. Blittable structs: CaveNode, CaveTunnel, CaveEntrance, CaveStructure   ║
// ║  3. CaveGenerationParams: noise/warp/terrace parameters for density job     ║
// ║  4. CavePreset: serializable inspector-friendly preset class                ║
// ║  5. CavePresetLibrary: static factory methods for standard cave types       ║
// ║                                                                             ║
// ║  DESIGN RULES:                                                              ║
// ║  ─────────────                                                              ║
// ║  • All structs used in Burst jobs are BLITTABLE (no managed refs).          ║
// ║  • Padding bytes ensure struct sizes align to cache-friendly boundaries.    ║
// ║  • CavePreset is a [Serializable] class for Inspector — never in Burst.     ║
// ║  • CaveGenerationParams is a blittable struct extracted from CavePreset.    ║
// ║  • NativeArrays of these structs are allocated by CaveGraphGenerator        ║
// ║    and disposed by HectonVoxelEngine after mesh generation completes.       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    // ════════════════════════════════════════════════════════════════════════════
    //  REGION: ENUMS
    // ════════════════════════════════════════════════════════════════════════════
    #region Enums

    /// <summary>
    /// Shape of a cave room. Determines which SDF primitive is used
    /// in VoxelDensityJob to evaluate distance.
    /// </summary>
    public enum CaveRoomType : byte
    {
        /// <summary>Perfect sphere. Radii.x used as uniform radius.</summary>
        Sphere = 0,

        /// <summary>Axis-aligned ellipsoid. All three radii used independently.</summary>
        Ellipsoid = 1,

        /// <summary>Tall narrow cylinder with rounded caps. Vertical pit/chimney.
        /// Radii: x = horizontal radius, y = half-height, z = cap roundness.</summary>
        VerticalShaft = 2,

        /// <summary>Wide flat ellipsoid. Radii.y is compressed.
        /// Creates pancake-shaped halls with low ceilings.</summary>
        FlatHall = 3,

        /// <summary>Narrow vertical crack. Radii.x and z compressed, y stretched.
        /// Players squeeze through sideways.</summary>
        Crevice = 4
    }

    /// <summary>
    /// Cross-section profile of a tunnel connecting two cave nodes.
    /// </summary>
    public enum CaveTunnelType : byte
    {
        /// <summary>Circular cross-section. Default for most tunnels.</summary>
        Round = 0,

        /// <summary>Vertically stretched ellipse. Canyon-like passage.
        /// heightScale > 1, widthScale < 1.</summary>
        Tall = 1,

        /// <summary>Horizontally stretched ellipse. Low crawlspace.
        /// heightScale < 1, widthScale > 1.</summary>
        Wide = 2
    }

    /// <summary>
    /// Type of internal structure added BACK into cave void.
    /// These SDF primitives are UNIONED (added) into the density field
    /// after the cave has been subtracted from terrain.
    /// Reserved for future use — array can be empty (Length = 0).
    /// </summary>
    public enum CaveStructureType : byte
    {
        /// <summary>Vertical cylinder connecting floor to ceiling.
        /// Natural pillar or stalagnat.</summary>
        Column = 0,

        /// <summary>Horizontal capsule spanning between walls.
        /// Natural rock bridge.</summary>
        Bridge = 1,

        /// <summary>Flat box dividing space. Partial wall or ledge.</summary>
        Wall = 2,

        /// <summary>Cone growing upward from floor.</summary>
        Stalagmite = 3,

        /// <summary>Cone hanging downward from ceiling.</summary>
        Stalactite = 4,

        /// <summary>Sphere resting on floor. Fallen boulder.</summary>
        Boulder = 5,

        /// <summary>Half-torus. Natural stone arch.</summary>
        Arch = 6,

        /// <summary>Axis-aligned box. Ruins, foundations, flat shelves.</summary>
        Block = 7
    }

    /// <summary>
    /// Named preset categories. Used by CavePresetLibrary factory methods
    /// and by ScavengePopulator to select cave type per spawn point.
    /// </summary>
    public enum CavePresetType : byte
    {
        /// <summary>Tiny hole. 1-2 rooms. Fish lair, loot stash.</summary>
        Den = 0,

        /// <summary>Single beautiful chamber with one entrance.</summary>
        Grotto = 1,

        /// <summary>5-15 rooms with multiple paths and loops.</summary>
        System = 2,

        /// <summary>15-30 rooms, many dead ends, easy to get lost.</summary>
        Labyrinth = 3,

        /// <summary>Deep vertical system. Shafts, chimneys, wells.</summary>
        Abyss = 4,

        /// <summary>Massive cave. 20-50 rooms, 500m+ span.
        /// Underground biome with unique flora/fauna.</summary>
        Mega = 5,

        /// <summary>Long winding single tunnel with periodic widenings.
        /// Lava tube aesthetic.</summary>
        Tube = 6,

        /// <summary>Custom parameters — no library preset applied.</summary>
        Custom = 255
    }
    /// <summary>
    /// Context in which a resource spawn point was generated.
    /// Determines which loot table ScavengePopulator uses to select prefabs.
    ///
    /// Surface spawns come from HectonScatterOutput (MapMagic Scatter).
    /// Cave spawns come from VoxelSpawnPointJob (HectonVoxelEngine).
    /// </summary>
    public enum SpawnContext : byte
    {
        /// <summary>Open ocean floor. Debris, pipes, standard titanium.</summary>
        Surface = 0,

        /// <summary>Shallow caves: Den, Grotto, System, Tube.
        /// Bioluminescent flora, quartz, cave-adapted fauna.</summary>
        CaveShallow = 1,

        /// <summary>Mid-depth caves: moderate complexity, mixed resources.</summary>
        CaveMid = 2,

        /// <summary>Deep caves: Labyrinth, Abyss, Mega.
        /// Uranium, crystals, aggressive fauna, rare materials.</summary>
        CaveDeep = 3
    }
    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    //  REGION: BLITTABLE STRUCTS (Burst-compatible, NativeArray-safe)
    // ════════════════════════════════════════════════════════════════════════════
    #region Blittable Structs

    /// <summary>
    /// Cave room node. Defines a spherical/ellipsoidal void carved from terrain.
    /// Positions are in WORLD SPACE (not relative to volume origin).
    ///
    /// Size: 48 bytes (3 cache lines on most architectures).
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CaveNode
    {
        /// <summary>World-space center of this room.</summary>
        public float3 position;

        /// <summary>
        /// Half-extents along each axis.
        /// Sphere: only x used (uniform radius).
        /// Ellipsoid: all three used independently.
        /// VerticalShaft: x = horizontal radius, y = half-height, z = cap roundness.
        /// FlatHall: x,z = horizontal spread, y = compressed height.
        /// Crevice: x,z = compressed width, y = stretched height.
        /// </summary>
        public float3 radii;

        /// <summary>Smooth blending radius when merging with adjacent nodes/tunnels.
        /// Higher = more organic, blobby transitions. Range: 4-32.</summary>
        public float blendRadius;

        /// <summary>Scale multiplier for wall noise sampling on this room's surface.
        /// Allows per-room noise variation. Range: 0.5-2.0.</summary>
        public float noiseScale;

        /// <summary>Amplitude multiplier for wall noise on this room.
        /// 0 = perfectly smooth walls. Range: 0-3.</summary>
        public float noiseAmplitude;

        /// <summary>Shape of this room. Determines SDF evaluation path.</summary>
        public CaveRoomType roomType;

        // ── Padding to 48 bytes ──
        public byte _pad0;
        public byte _pad1;
        public byte _pad2;
    }

    /// <summary>
    /// Tunnel connecting two points in the cave system.
    /// Implemented as a conic capsule SDF with optional cross-section scaling.
    /// Positions are in WORLD SPACE.
    ///
    /// Size: 56 bytes.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CaveTunnel
    {
        /// <summary>World-space start point (typically center of source room).</summary>
        public float3 pointA;

        /// <summary>World-space end point (typically center of target room).</summary>
        public float3 pointB;

        /// <summary>Radius at point A. Range: 1-15.</summary>
        public float radiusA;

        /// <summary>Radius at point B. Can differ from radiusA for tapered tunnels.
        /// Range: 1-15.</summary>
        public float radiusB;

        /// <summary>Smooth blending radius when merging with rooms.
        /// Range: 4-32.</summary>
        public float blendRadius;

        /// <summary>Vertical scale of cross-section.
        /// > 1 = tall canyon, < 1 = low crawlspace. 1 = round.</summary>
        public float heightScale;

        /// <summary>Horizontal scale of cross-section.
        /// > 1 = wide passage, < 1 = narrow crack. 1 = round.</summary>
        public float widthScale;

        /// <summary>Additional domain warp amplitude applied specifically to this tunnel.
        /// Stacks with global warp. Makes this particular tunnel more/less curvy.
        /// Range: 0-5.</summary>
        public float warpAmount;

        /// <summary>Cross-section profile type.</summary>
        public CaveTunnelType tunnelType;

        // ── Padding ──
        public byte _pad0;
        public byte _pad1;
        public byte _pad2;
    }

    /// <summary>
    /// Cave entrance point — connects cave void to terrain surface.
    /// Implemented as a conic capsule from surface point inward.
    /// The terrain mesh should have a hole at this location.
    ///
    /// Size: 76 bytes.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CaveEntrance
    {
        /// <summary>World-space position of the entrance on terrain surface.</summary>
        public float3 surfacePosition;

        /// <summary>Unit direction vector pointing INTO the cave (away from surface).
        /// Typically: normalized(nearestRoom - surfacePosition).</summary>
        public float3 inwardDirection;

        /// <summary>Radius of the entrance opening at the surface. Range: 2-10.</summary>
        public float radius;

        /// <summary>How far the entrance funnel extends before connecting to cave interior.
        /// Controls the "throat" length. Range: 5-30.</summary>
        public float funnelLength;

        /// <summary>Radius at the inner end of the funnel. Typically radius * 0.5.
        /// Creates a narrowing entrance that opens into a larger space.</summary>
        public float innerRadius;

        /// <summary>Terrain normal sampled at the cave mouth when MapMagic terrain is available.</summary>
        public float3 terrainNormal;

        /// <summary>0..1 blend weight used to conform the mouth SDF to the terrain normal.</summary>
        public float terrainNormalBlend;

        /// <summary>Terrain splat-derived RGB color at the mouth, A = valid/blend weight.</summary>
        public float4 terrainSplatColor;

        /// <summary>0..1 confidence for terrainSplatColor.</summary>
        public float terrainSplatBlend;
    }

    /// <summary>
    /// Internal cave structure — solid geometry ADDED BACK into cave void.
    /// Used for pillars, bridges, stalactites, boulders, ruins.
    /// These are evaluated AFTER cave subtraction and UNIONED into density.
    ///
    /// FUTURE USE: Array can be empty (Length = 0) with zero performance cost.
    ///
    /// Size: 52 bytes.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CaveStructure
    {
        /// <summary>World-space center/base of the structure.</summary>
        public float3 position;

        /// <summary>
        /// Size parameters (interpretation depends on type):
        /// Column: x = radius, y = height, z = unused.
        /// Bridge: x = radius, y = unused, z = unused. pointB used for end.
        /// Wall: x = width, y = height, z = thickness.
        /// Stalagmite/Stalactite: x = base radius, y = height, z = tip radius.
        /// Boulder: x = radius, y,z = unused.
        /// Arch: x = major radius, y = minor radius, z = thickness.
        /// Block: x,y,z = half-extents.
        /// </summary>
        public float3 size;

        /// <summary>Second point for oriented structures (Bridge end point, etc.).
        /// For non-oriented types, ignored.</summary>
        public float3 pointB;

        /// <summary>Smooth blending radius. Range: 2-16.</summary>
        public float blendRadius;

        /// <summary>Surface noise amplitude. 0 = smooth. Range: 0-1.</summary>
        public float noiseAmount;

        /// <summary>Type of structure. Determines SDF evaluation.</summary>
        public CaveStructureType structureType;

        // ── Padding ──
        public byte _pad0;
        public byte _pad1;
        public byte _pad2;
    }

    /// <summary>
    /// All parameters controlling noise, warping, and surface detail
    /// inside VoxelDensityJob. Extracted from CavePreset at generation time.
    /// Passed as a single struct value (not NativeArray) into the job.
    ///
    /// Blittable. No managed references.
    ///
    /// Size: 84 bytes.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CaveGenerationParams
    {
        // ── Domain Warping ──────────────────────────────────────────────────
        /// <summary>Frequency of 3D noise used to distort world coordinates
        /// before SDF evaluation. Lower = larger-scale bends. Range: 0.01-0.15.</summary>
        public float warpFrequency;

        /// <summary>Maximum displacement in meters from domain warping.
        /// Higher = more distorted tunnels. Range: 0-10.</summary>
        public float warpAmplitude;

        /// <summary>Fractal octaves for domain warp noise. Range: 1-3.</summary>
        public int warpOctaves;

        // ── Wall Surface Noise ──────────────────────────────────────────────
        /// <summary>Frequency of surface detail noise. Higher = finer bumps.
        /// Range: 0.05-0.5.</summary>
        public float wallNoiseFrequency;

        /// <summary>Amplitude of surface noise in meters. Pushes SDF surface
        /// in/out to create rocky texture. Range: 0-5.</summary>
        public float wallNoiseAmplitude;

        /// <summary>Fractal octaves for wall noise. More = more detail. Range: 1-6.</summary>
        public int wallNoiseOctaves;

        /// <summary>Lacunarity (frequency multiplier per octave). Standard: 2.0.</summary>
        public float wallNoiseLacunarity;

        /// <summary>Persistence (amplitude multiplier per octave). Standard: 0.5.</summary>
        public float wallNoisePersistence;

        // ── Horizontal Terraces ─────────────────────────────────────────────
        /// <summary>Frequency of horizontal rock strata layers.
        /// Higher = more frequent ledges. Range: 0-2.</summary>
        public float terraceFrequency;

        /// <summary>Depth of terrace carving in meters. Range: 0-2.</summary>
        public float terraceAmplitude;

        /// <summary>Edge sharpness of terraces. Higher = more defined ledges.
        /// Range: 1-15.</summary>
        public float terraceSharpness;

        // ── Global Blending ─────────────────────────────────────────────────
        /// <summary>Default smooth-min blending factor for rooms/tunnels
        /// that don't specify their own. Range: 4-32.</summary>
        public float globalBlendK;

        /// <summary>Width of the border region where cave density fades to solid.
        /// Prevents cave mesh edges from showing open geometry.
        /// Must match sealMargin in HectonVoxelEngine. Range: 1-10.</summary>
        public float shellThickness;

        // ── Seed ────────────────────────────────────────────────────────────
        /// <summary>Master seed for all noise functions. Deterministic results
        /// for same seed value.</summary>
        public uint seed;

        // ── Wall Noise Detail Threshold ─────────────────────────────────────
        /// <summary>Distance from SDF surface (in meters) within which wall noise
        /// is evaluated. Beyond this distance, noise is skipped for performance.
        /// Larger = more noise evaluated = slower but more consistent.
        /// Range: 2-20. Recommended: shellThickness * 2.</summary>
        public float noiseEvalDistance;

        // ── Floor Flattening ────────────────────────────────────────────────
        /// <summary>How aggressively floors are flattened.
        /// 0 = natural curved floor. 1 = perfectly flat.
        /// Affects bottom 30% of each room. Range: 0-1.</summary>
        public float floorFlatness;

        // ── Structure Blending ──────────────────────────────────────────────
        /// <summary>Default blend radius for CaveStructure primitives
        /// that don't specify their own. Range: 2-16.</summary>
        public float structureBlendK;

        // ── Entrance Blending ───────────────────────────────────────────────
        /// <summary>Blend radius for entrance funnels merging with cave interior.
        /// Range: 4-20.</summary>
        public float entranceBlendK;
        public byte structureOnlyMode;
        // ── Spawn Context ───────────────────────────────────────────
        /// <summary>Determines which loot table is used for spawn points
        /// extracted from this cave's floor geometry.</summary>
        public SpawnContext spawnContext;
    }
        /// <summary>
    /// Spawn point extracted from cave floor mesh.
    /// Contains world position and a deterministic hash ID derived from spatial coordinates.
    /// The hashId is stable across runs — same seed + same position = same hashId.
    /// Used as localIndex in ScavengePopulator to ensure save system consistency.
    ///
    /// Size: 16 bytes.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CaveSpawnData
    {
        /// <summary>World-space position of the spawn point on cave floor.</summary>
        public float3 position;

        /// <summary>Deterministic ID derived from spatial hash of position.
        /// Guaranteed positive (masked to 0x7FFFFFFF).
        /// Same world position always produces same hashId regardless of
        /// thread execution order in parallel jobs.</summary>
        public int hashId;
    }
    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    //  REGION: CAVE PRESET (Inspector-facing, serializable)
    // ════════════════════════════════════════════════════════════════════════════
    #region CavePreset

    /// <summary>
    /// Designer-friendly cave configuration. Exposed in Inspector via
    /// ScavengePopulator or standalone CaveSpawnPoint components.
    ///
    /// NOT used inside Burst jobs. Converted to CaveGenerationParams +
    /// NativeArrays by CaveGraphGenerator before job scheduling.
    ///
    /// All ranges documented for slider clamping in custom editors.
    /// </summary>
    [Serializable]
    public class CavePreset
    {
        /// <summary>Human-readable label for debugging and editor UI.</summary>
        public string presetName = "Custom Cave";

        /// <summary>Preset category. Determines which CavePresetLibrary
        /// defaults are loaded when "Reset to Preset" is clicked.</summary>
        public CavePresetType presetType = CavePresetType.Custom;

        // ═══════════════════════════════════════════════════════════════════
        //  VOLUME SIZING
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ VOLUME ═══")]

        [Tooltip("Grid dimension per axis (voxel points = this + 1).\n" +
                 "48 = small cave, 96 = medium, 128 = large.\n" +
                 "Memory scales cubically!")]
        [Range(32, 128)]
        public int gridDimension = 64;

        [Tooltip("Size of one voxel in meters.\n" +
                 "0.5 = high detail (small caves)\n" +
                 "2.0 = medium (standard caves)\n" +
                 "4.0 = low detail (mega caves)\n" +
                 "Volume coverage = gridDimension × voxelSize")]
        [Range(0.5f, 4f)]
        public float voxelSize = 1.0f;

        // ═══════════════════════════════════════════════════════════════════
        //  ROOM GENERATION
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ ROOMS ═══")]

        [Tooltip("Minimum number of rooms generated.")]
        [Range(1, 50)]
        public int minRooms = 3;

        [Tooltip("Maximum number of rooms generated.")]
        [Range(1, 50)]
        public int maxRooms = 8;

        [Tooltip("Minimum room radius in meters.")]
        [Range(2f, 30f)]
        public float minRoomRadius = 4f;

        [Tooltip("Maximum room radius in meters.")]
        [Range(2f, 60f)]
        public float maxRoomRadius = 15f;

        [Tooltip("Probability that a room is a vertical shaft.\n" +
                 "Shafts create dramatic vertical drops.")]
        [Range(0f, 1f)]
        public float verticalShaftChance = 0.1f;

        [Tooltip("Probability that a room is a flat wide hall.\n" +
                 "Low ceiling, wide floor — cathedral-like.")]
        [Range(0f, 1f)]
        public float flatHallChance = 0.15f;

        [Tooltip("Probability that a room is a narrow crevice.\n" +
                 "Tight squeeze, atmospheric.")]
        [Range(0f, 1f)]
        public float creviceChance = 0.1f;

        [Tooltip("How much vertical variance rooms have.\n" +
                 "0 = all rooms at same depth.\n" +
                 "1 = rooms spread across full depth range.")]
        [Range(0f, 1f)]
        public float verticalSpread = 0.3f;

        [Tooltip("Maximum depth below entrance that rooms can be placed (meters).")]
        [Range(10f, 400f)]
        public float maxDepth = 50f;

        // ═══════════════════════════════════════════════════════════════════
        //  TUNNEL GENERATION
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ TUNNELS ═══")]

        [Tooltip("Minimum tunnel radius in meters.")]
        [Range(1f, 10f)]
        public float minTunnelRadius = 2f;

        [Tooltip("Maximum tunnel radius in meters.")]
        [Range(1f, 20f)]
        public float maxTunnelRadius = 5f;

        [Tooltip("Probability that a tunnel has tall narrow cross-section.")]
        [Range(0f, 1f)]
        public float tallTunnelChance = 0.15f;

        [Tooltip("Probability that a tunnel has wide low cross-section.")]
        [Range(0f, 1f)]
        public float wideTunnelChance = 0.1f;

        [Tooltip("Per-tunnel domain warp amplitude.\n" +
                 "Stacks with global warp. Makes tunnels curvy.")]
        [Range(0f, 8f)]
        public float tunnelWarpAmount = 2f;

        [Tooltip("Probability of extra tunnel between non-adjacent rooms.\n" +
                 "Creates loops and alternative paths.")]
        [Range(0f, 0.8f)]
        public float extraConnectionChance = 0.2f;

        // ═══════════════════════════════════════════════════════════════════
        //  ENTRANCES
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ ENTRANCES ═══")]

        [Tooltip("Minimum number of entrances.")]
        [Range(1, 5)]
        public int minEntrances = 1;

        [Tooltip("Maximum number of entrances.")]
        [Range(1, 5)]
        public int maxEntrances = 1;

        [Tooltip("Radius of entrance opening at surface (meters).")]
        [Range(1.5f, 15f)]
        public float entranceRadius = 3f;

        [Tooltip("Length of the entrance funnel throat (meters).\n" +
                 "Longer = more gradual transition from terrain to cave.")]
        [Range(3f, 40f)]
        public float entranceFunnelLength = 12f;

        // ═══════════════════════════════════════════════════════════════════
        //  DOMAIN WARPING
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ DOMAIN WARPING ═══")]

        [Tooltip("Frequency of coordinate distortion noise.\n" +
                 "Lower = larger, gentler bends. Higher = tighter curves.")]
        [Range(0.005f, 0.2f)]
        public float warpFrequency = 0.04f;

        [Tooltip("Maximum distortion in meters.\n" +
                 "0 = straight tunnels. 5+ = very organic.")]
        [Range(0f, 15f)]
        public float warpAmplitude = 3f;

        [Tooltip("Noise octaves for warping. More = more detail in distortion.")]
        [Range(1, 4)]
        public int warpOctaves = 2;

        // ═══════════════════════════════════════════════════════════════════
        //  WALL DETAIL
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ WALL NOISE ═══")]

        [Tooltip("Frequency of wall surface noise.\n" +
                 "Lower = smoother, larger bumps. Higher = rougher.")]
        [Range(0.02f, 0.8f)]
        public float wallNoiseFrequency = 0.15f;

        [Tooltip("Amplitude of wall noise (meters).\n" +
                 "How far bumps protrude. 0 = smooth.")]
        [Range(0f, 6f)]
        public float wallNoiseAmplitude = 1.5f;

        [Tooltip("Fractal octaves for wall noise. More = finer detail.")]
        [Range(1, 6)]
        public int wallNoiseOctaves = 3;

        [Tooltip("Frequency multiplier between octaves.")]
        [Range(1.5f, 3f)]
        public float wallNoiseLacunarity = 2.0f;

        [Tooltip("Amplitude decay between octaves.")]
        [Range(0.3f, 0.7f)]
        public float wallNoisePersistence = 0.5f;

        // ═══════════════════════════════════════════════════════════════════
        //  TERRACES (Horizontal rock strata)
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ TERRACES ═══")]

        [Tooltip("Frequency of horizontal rock layers.\n" +
                 "0 = none. 0.5 = every 2m. 1 = every 1m.")]
        [Range(0f, 2f)]
        public float terraceFrequency = 0.4f;

        [Tooltip("Depth of terrace grooves (meters).")]
        [Range(0f, 2f)]
        public float terraceAmplitude = 0.4f;

        [Tooltip("Edge sharpness. Low = rounded shelves. High = sharp ledges.")]
        [Range(1f, 15f)]
        public float terraceSharpness = 4f;

        // ═══════════════════════════════════════════════════════════════════
        //  BLENDING & SEALING
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ BLENDING ═══")]

        [Tooltip("Global smooth-min factor for merging rooms/tunnels.\n" +
                 "Higher = more blobby organic shapes. Lower = distinct chambers.")]
        [Range(2f, 40f)]
        public float globalBlendK = 12f;

        [Tooltip("Border thickness where density fades to solid.\n" +
                 "Prevents cave mesh edges from showing holes.")]
        [Range(1f, 10f)]
        public float sealMargin = 3f;

        // ═══════════════════════════════════════════════════════════════════
        //  FLOOR
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ FLOOR ═══")]

        [Tooltip("Floor flattening intensity.\n" +
                 "0 = natural curved floor. 1 = flat walkable surface.")]
        [Range(0f, 1f)]
        public float floorFlatness = 0.6f;
                // ═══════════════════════════════════════════════════════════════════
        //  SPAWN CONTEXT
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ LOOT CONTEXT ═══")]

        [Tooltip("Which loot table ScavengePopulator uses for spawn points\n" +
                 "extracted from this cave type.\n" +
                 "Surface = standard debris\n" +
                 "CaveShallow = quartz, bioluminescent flora\n" +
                 "CaveDeep = uranium, crystals, rare materials")]
        public SpawnContext spawnContext = SpawnContext.CaveShallow;

        // ═══════════════════════════════════════════════════════════════════
        //  INTERIOR STRUCTURES
        // ═══════════════════════════════════════════════════════════════════

        [Header("═══ INTERIOR STRUCTURES ═══")]

        [Tooltip("Enable generation of interior structures (stalactites, boulders, etc.)\n" +
                 "Adds visual interest and readability cues to cave interiors.")]
        public bool enableStructures = true;

        [Tooltip("Maximum number of structures to generate per cave.\n" +
                 "Higher = more cluttered caves. 0 = no structures.")]
        [Range(0, 20)]
        public int maxStructures = 8;

        [Tooltip("Density multiplier for structure placement.\n" +
                 "1.0 = normal density. 0.5 = sparse. 2.0 = crowded.")]
        [Range(0.1f, 3f)]
        public float structureDensity = 1.0f;

        [Tooltip("Which structure types to generate.\n" +
                 "Stalactites/Stalagmites = hanging/standing cones\n" +
                 "Boulders = floor spheres\n" +
                 "Columns = vertical pillars\n" +
                 "Bridges = horizontal spans\n" +
                 "Arches = curved openings")]
        public CaveStructureType[] allowedStructureTypes = new CaveStructureType[]
        {
            CaveStructureType.Stalactite,
            CaveStructureType.Stalagmite,
            CaveStructureType.Boulder,
            CaveStructureType.Column
        };

        [Tooltip("Hazard level of this cave type (0-1).\n" +
                 "0 = safe exploration cave\n" +
                 "1 = maximum danger (predators, traps, radiation)")]
        [Range(0f, 1f)]
        public float hazardLevel = 0f;

        [Tooltip("Mood atmosphere of this cave.\n" +
                 "0 = silent/empty\n" +
                 "0.5 = moderate life activity\n" +
                 "1 = busy with fauna/ecosystem")]
        [Range(0f, 1f)]
        public float moodLevel = 0.3f;

        [Tooltip("Whether this cave contains ancient ruins/structures.\n" +
                 "Adds Block and Wall structures for exploration interest.")]
        public bool isRuinLinked = false;

        // ═══════════════════════════════════════════════════════════════════
        //  CONVERSION TO BURST-COMPATIBLE PARAMS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extract blittable parameters for VoxelDensityJob.
        /// Called on main thread before job scheduling.
        /// </summary>
        public CaveGenerationParams ToGenerationParams(uint seed)
        {
            return new CaveGenerationParams
            {
                warpFrequency       = warpFrequency,
                warpAmplitude       = warpAmplitude,
                warpOctaves         = warpOctaves,
                wallNoiseFrequency  = wallNoiseFrequency,
                wallNoiseAmplitude  = wallNoiseAmplitude,
                wallNoiseOctaves    = wallNoiseOctaves,
                wallNoiseLacunarity = wallNoiseLacunarity,
                wallNoisePersistence = wallNoisePersistence,
                terraceFrequency    = terraceFrequency,
                terraceAmplitude    = terraceAmplitude,
                terraceSharpness    = terraceSharpness,
                globalBlendK        = globalBlendK,
                shellThickness      = sealMargin,
                seed                = seed,
                noiseEvalDistance    = sealMargin * 3f,
                floorFlatness       = floorFlatness,
                structureBlendK     = 6f,
                entranceBlendK      = globalBlendK * 0.8f,
                structureOnlyMode   = 0,
                spawnContext        = spawnContext
            };
        }

        /// <summary>
        /// Computed volume coverage in meters: gridDimension × voxelSize.
        /// </summary>
        public float VolumeCoverage => gridDimension * voxelSize;

        /// <summary>
        /// Deep copy this preset (for runtime modification without affecting asset).
        /// </summary>
        public CavePreset Clone()
        {
            return (CavePreset)MemberwiseClone();
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    //  REGION: PRESET LIBRARY — Factory methods for standard cave types
    // ════════════════════════════════════════════════════════════════════════════
    #region CavePresetLibrary

    /// <summary>
    /// Static factory methods that return pre-configured CavePreset instances.
    /// All values are carefully tuned for underwater cave exploration gameplay.
    ///
    /// Usage:
    ///   CavePreset preset = CavePresetLibrary.Create(CavePresetType.Labyrinth);
    ///   // Optionally tweak individual fields
    ///   preset.maxRooms = 25;
    /// </summary>
    public static class CavePresetLibrary
    {
        /// <summary>
        /// Factory dispatcher. Returns a new CavePreset for the given type.
        /// Custom type returns a default preset with no special tuning.
        /// </summary>
        public static CavePreset Create(CavePresetType type)
        {
            switch (type)
            {
                case CavePresetType.Den:       return Den();
                case CavePresetType.Grotto:    return Grotto();
                case CavePresetType.System:    return System();
                case CavePresetType.Labyrinth: return Labyrinth();
                case CavePresetType.Abyss:     return Abyss();
                case CavePresetType.Mega:      return Mega();
                case CavePresetType.Tube:      return Tube();
                default:                       return new CavePreset();
            }
        }

        /// <summary>
        /// Tiny lair. 1-2 small rooms connected by a short passage.
        /// Quick to generate, cheap to render. For fish lairs, loot stashes.
        /// Volume: 48³ × 0.5m = 24m cube.
        /// </summary>
        public static CavePreset Den()
        {
            return new CavePreset
            {
                presetName              = "Den",
                presetType              = CavePresetType.Den,
                gridDimension           = 48,
                voxelSize               = 0.5f,
                minRooms                = 1,
                maxRooms                = 2,
                minRoomRadius           = 2f,
                maxRoomRadius           = 5f,
                verticalShaftChance     = 0.02f,
                flatHallChance          = 0.2f,
                creviceChance           = 0.05f,
                verticalSpread          = 0.05f,
                maxDepth                = 15f,
                minTunnelRadius         = 1.5f,
                maxTunnelRadius         = 2.5f,
                tallTunnelChance        = 0.05f,
                wideTunnelChance        = 0.1f,
                tunnelWarpAmount        = 1f,
                extraConnectionChance   = 0.05f,
                minEntrances            = 1,
                maxEntrances            = 1,
                entranceRadius          = 2f,
                entranceFunnelLength    = 6f,
                warpFrequency           = 0.08f,
                warpAmplitude           = 1.5f,
                warpOctaves             = 1,
                wallNoiseFrequency      = 0.25f,
                wallNoiseAmplitude      = 0.5f,
                wallNoiseOctaves        = 2,
                wallNoiseLacunarity     = 2.0f,
                wallNoisePersistence    = 0.5f,
                terraceFrequency        = 0.3f,
                terraceAmplitude        = 0.15f,
                terraceSharpness        = 3f,
                globalBlendK            = 6f,
                sealMargin              = 2f,
                floorFlatness           = 0.7f,
                spawnContext            = SpawnContext.CaveShallow  
            };
        }

        /// <summary>
        /// Beautiful single-chamber grotto. One large room with dramatic entrance.
        /// Ideal for scenic POIs, quest locations.
        /// Volume: 64³ × 1m = 64m cube.
        /// </summary>
        public static CavePreset Grotto()
        {
            return new CavePreset
            {
                presetName              = "Grotto",
                presetType              = CavePresetType.Grotto,
                gridDimension           = 64,
                voxelSize               = 1.0f,
                minRooms                = 1,
                maxRooms                = 3,
                minRoomRadius           = 8f,
                maxRoomRadius           = 20f,
                verticalShaftChance     = 0.05f,
                flatHallChance          = 0.35f,
                creviceChance           = 0.05f,
                verticalSpread          = 0.1f,
                maxDepth                = 30f,
                minTunnelRadius         = 3f,
                maxTunnelRadius         = 6f,
                tallTunnelChance        = 0.1f,
                wideTunnelChance        = 0.2f,
                tunnelWarpAmount        = 2f,
                extraConnectionChance   = 0.1f,
                minEntrances            = 1,
                maxEntrances            = 1,
                entranceRadius          = 5f,
                entranceFunnelLength    = 15f,
                warpFrequency           = 0.035f,
                warpAmplitude           = 3f,
                warpOctaves             = 2,
                wallNoiseFrequency      = 0.12f,
                wallNoiseAmplitude      = 2f,
                wallNoiseOctaves        = 4,
                wallNoiseLacunarity     = 2.0f,
                wallNoisePersistence    = 0.5f,
                terraceFrequency        = 0.5f,
                terraceAmplitude        = 0.6f,
                terraceSharpness        = 5f,
                globalBlendK            = 16f,
                sealMargin              = 3f,
                floorFlatness           = 0.8f,
                spawnContext            = SpawnContext.CaveShallow
            };
        }

        /// <summary>
        /// Branching cave system. 5-15 rooms with tunnels, loops, dead ends.
        /// Core exploration content. Multiple path choices.
        /// Volume: 96³ × 2m = 192m cube.
        /// </summary>
        public static CavePreset System()
        {
            return new CavePreset
            {
                presetName              = "System",
                presetType              = CavePresetType.System,
                gridDimension           = 96,
                voxelSize               = 2.0f,
                minRooms                = 5,
                maxRooms                = 15,
                minRoomRadius           = 5f,
                maxRoomRadius           = 18f,
                verticalShaftChance     = 0.12f,
                flatHallChance          = 0.2f,
                creviceChance           = 0.1f,
                verticalSpread          = 0.3f,
                maxDepth                = 80f,
                minTunnelRadius         = 2.5f,
                maxTunnelRadius         = 6f,
                tallTunnelChance        = 0.2f,
                wideTunnelChance        = 0.15f,
                tunnelWarpAmount        = 3f,
                extraConnectionChance   = 0.25f,
                minEntrances            = 1,
                maxEntrances            = 2,
                entranceRadius          = 4f,
                entranceFunnelLength    = 15f,
                warpFrequency           = 0.04f,
                warpAmplitude           = 4f,
                warpOctaves             = 2,
                wallNoiseFrequency      = 0.15f,
                wallNoiseAmplitude      = 1.5f,
                wallNoiseOctaves        = 3,
                wallNoiseLacunarity     = 2.0f,
                wallNoisePersistence    = 0.5f,
                terraceFrequency        = 0.45f,
                terraceAmplitude        = 0.5f,
                terraceSharpness        = 5f,
                globalBlendK            = 14f,
                sealMargin              = 3f,
                floorFlatness           = 0.6f,
                spawnContext            = SpawnContext.CaveShallow
            };
        }

        /// <summary>
        /// Dense labyrinth. 15-30 rooms, many branches and dead ends.
        /// Easy to get lost. Rewarding exploration.
        /// Volume: 96³ × 2m = 192m cube.
        /// </summary>
        public static CavePreset Labyrinth()
        {
            return new CavePreset
            {
                presetName              = "Labyrinth",
                presetType              = CavePresetType.Labyrinth,
                gridDimension           = 96,
                voxelSize               = 2.0f,
                minRooms                = 15,
                maxRooms                = 30,
                minRoomRadius           = 3f,
                maxRoomRadius           = 10f,
                verticalShaftChance     = 0.15f,
                flatHallChance          = 0.1f,
                creviceChance           = 0.25f,
                verticalSpread          = 0.4f,
                maxDepth                = 100f,
                minTunnelRadius         = 1.5f,
                maxTunnelRadius         = 4f,
                tallTunnelChance        = 0.25f,
                wideTunnelChance        = 0.08f,
                tunnelWarpAmount        = 4f,
                extraConnectionChance   = 0.35f,
                minEntrances            = 1,
                maxEntrances            = 3,
                entranceRadius          = 2.5f,
                entranceFunnelLength    = 10f,
                warpFrequency           = 0.06f,
                warpAmplitude           = 3f,
                warpOctaves             = 3,
                wallNoiseFrequency      = 0.2f,
                wallNoiseAmplitude      = 1.0f,
                wallNoiseOctaves        = 3,
                wallNoiseLacunarity     = 2.0f,
                wallNoisePersistence    = 0.5f,
                terraceFrequency        = 0.6f,
                terraceAmplitude        = 0.3f,
                terraceSharpness        = 7f,
                globalBlendK            = 10f,
                sealMargin              = 2.5f,
                floorFlatness           = 0.5f,
                spawnContext            = SpawnContext.CaveShallow
            };
        }

        /// <summary>
        /// Deep vertical cave. Shafts, chimneys, spiral descents.
        /// Strong vertical emphasis. Dramatic sense of depth.
        /// Volume: 96³ × 3m = 288m cube.
        /// </summary>
        public static CavePreset Abyss()
        {
            return new CavePreset
            {
                presetName              = "Abyss",
                presetType              = CavePresetType.Abyss,
                gridDimension           = 96,
                voxelSize               = 3.0f,
                minRooms                = 6,
                maxRooms                = 14,
                minRoomRadius           = 5f,
                maxRoomRadius           = 15f,
                verticalShaftChance     = 0.45f,
                flatHallChance          = 0.1f,
                creviceChance           = 0.15f,
                verticalSpread          = 0.8f,
                maxDepth                = 250f,
                minTunnelRadius         = 2f,
                maxTunnelRadius         = 5f,
                tallTunnelChance        = 0.35f,
                wideTunnelChance        = 0.05f,
                tunnelWarpAmount        = 2f,
                extraConnectionChance   = 0.12f,
                minEntrances            = 1,
                maxEntrances            = 1,
                entranceRadius          = 3.5f,
                entranceFunnelLength    = 18f,
                warpFrequency           = 0.04f,
                warpAmplitude           = 2.5f,
                warpOctaves             = 2,
                wallNoiseFrequency      = 0.18f,
                wallNoiseAmplitude      = 1.2f,
                wallNoiseOctaves        = 3,
                wallNoiseLacunarity     = 2.0f,
                wallNoisePersistence    = 0.5f,
                terraceFrequency        = 0.55f,
                terraceAmplitude        = 0.6f,
                terraceSharpness        = 6f,
                globalBlendK            = 14f,
                sealMargin              = 4f,
                floorFlatness           = 0.5f,
                spawnContext            = SpawnContext.CaveShallow
            };
        }

        /// <summary>
        /// Massive underground realm. 20-50 rooms, 500m+ coverage.
        /// Coarse voxels (4m) compensated by domain warping and noise.
        /// Comparable to a massive mineral trench transitioning into a thermal abyss.
        /// Volume: 128³ × 4m = 512m cube.
        /// </summary>
        public static CavePreset Mega()
        {
            return new CavePreset
            {
                presetName              = "Mega",
                presetType              = CavePresetType.Mega,
                gridDimension           = 128,
                voxelSize               = 4.0f,
                minRooms                = 20,
                maxRooms                = 50,
                minRoomRadius           = 10f,
                maxRoomRadius           = 45f,
                verticalShaftChance     = 0.1f,
                flatHallChance          = 0.3f,
                creviceChance           = 0.05f,
                verticalSpread          = 0.25f,
                maxDepth                = 200f,
                minTunnelRadius         = 5f,
                maxTunnelRadius         = 14f,
                tallTunnelChance        = 0.12f,
                wideTunnelChance        = 0.2f,
                tunnelWarpAmount        = 5f,
                extraConnectionChance   = 0.2f,
                minEntrances            = 2,
                maxEntrances            = 4,
                entranceRadius          = 7f,
                entranceFunnelLength    = 25f,
                warpFrequency           = 0.025f,
                warpAmplitude           = 6f,
                warpOctaves             = 2,
                wallNoiseFrequency      = 0.1f,
                wallNoiseAmplitude      = 3f,
                wallNoiseOctaves        = 4,
                wallNoiseLacunarity     = 2.0f,
                wallNoisePersistence    = 0.5f,
                terraceFrequency        = 0.35f,
                terraceAmplitude        = 1.0f,
                terraceSharpness        = 4f,
                globalBlendK            = 22f,
                sealMargin              = 5f,
                floorFlatness           = 0.7f,
                spawnContext            = SpawnContext.CaveShallow
            };
        }

        /// <summary>
        /// Long winding lava tube. Single main passage with periodic widenings.
        /// Few branches. Strong forward momentum. 
        /// Volume: 96³ × 2m = 192m cube.
        /// </summary>
        public static CavePreset Tube()
        {
            return new CavePreset
            {
                presetName              = "Tube",
                presetType              = CavePresetType.Tube,
                gridDimension           = 96,
                voxelSize               = 2.0f,
                minRooms                = 8,
                maxRooms                = 20,
                minRoomRadius           = 4f,
                maxRoomRadius           = 12f,
                verticalShaftChance     = 0.02f,
                flatHallChance          = 0.05f,
                creviceChance           = 0.02f,
                verticalSpread          = 0.12f,
                maxDepth                = 50f,
                minTunnelRadius         = 3f,
                maxTunnelRadius         = 7f,
                tallTunnelChance        = 0.15f,
                wideTunnelChance        = 0.15f,
                tunnelWarpAmount        = 5f,
                extraConnectionChance   = 0.05f,
                minEntrances            = 1,
                maxEntrances            = 2,
                entranceRadius          = 4f,
                entranceFunnelLength    = 12f,
                warpFrequency           = 0.05f,
                warpAmplitude           = 5f,
                warpOctaves             = 3,
                wallNoiseFrequency      = 0.1f,
                wallNoiseAmplitude      = 2f,
                wallNoiseOctaves        = 4,
                wallNoiseLacunarity     = 2.0f,
                wallNoisePersistence    = 0.5f,
                terraceFrequency        = 0.25f,
                terraceAmplitude        = 0.8f,
                terraceSharpness        = 3f,
                globalBlendK            = 18f,
                sealMargin              = 3f,
                floorFlatness           = 0.85f,
                spawnContext            = SpawnContext.CaveShallow
            };
        }
    }

    #endregion
}
