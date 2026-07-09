# HECTON-8: VOXEL & TERRAIN STITCHING CONTEXT

**Problem Statement:** Current SDF boolean subtraction for caves creates vertical spikes piercing the terrain, or fails to carve ceilings due to crude `math.clamp` and broken `SurfaceProtection` logic. We need a mathematically pure `Polynomial Smooth Minimum` (smin) integration between 2D Heightmap and 3D Voxel Cave Noise.

---

### 1. DATA STRUCTURES & PARAMETERS

Below is the complete C# code of the structures passed to the generation and meshing jobs, representing world parameters, scale constants, and geometry nodes.

#### Structs from [WorldMacroGeologyFields.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs)

```csharp
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WorldMacroGeologyParams
    {
        public uint Seed;
        public float WorldExtentMeters;
        public float ChunkSizeMeters;
        public float WaterSurfaceY;
        public float ShelfDepthMeters;
        public float AbyssDepthMeters;
        public float HadalDepthMeters;
        public float ShelfBreakWidthMeters;
        public float RidgeHeightMeters;
        public float RidgeWidthMeters;
        public float TrenchDepthMeters;
        public float TrenchWidthMeters;
        public float BasinDepthMeters;
        public float DetailProbeMeters;

        public static WorldMacroGeologyParams CreateDefault(uint seed)
        {
            return new WorldMacroGeologyParams
            {
                Seed = seed,
                WorldExtentMeters = WorldMacroGeologyFields.MinimumWorldExtentMeters,
                ChunkSizeMeters = WorldMacroGeologyFields.DefaultChunkSizeMeters,
                WaterSurfaceY = 0f,
                ShelfDepthMeters = 90f,
                AbyssDepthMeters = 2950f,
                HadalDepthMeters = 4600f,
                ShelfBreakWidthMeters = 5200f,
                RidgeHeightMeters = 1550f,
                RidgeWidthMeters = 2350f,
                TrenchDepthMeters = 900f,
                TrenchWidthMeters = 2200f,
                BasinDepthMeters = 620f,
                DetailProbeMeters = 120f
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldMacroGeologySample
    {
        public float HeightMeters;
        public float DepthMeters;
        public float ShelfMask;
        public float ShelfBreakMask;
        public float RidgeMask;
        public float TrenchMask;
        public float BasinMask;
        public float FaultMask;
        public float SedimentMask;
        public float SeepMask;
        public float Slope01;
        public float Curvature01;
        public float ErosionFlow01;
        public float TerraceMask;
        public float SlumpScarMask;
        public float TributaryCanyonMask;
        public float NodulePlainMask;
        public float ReefEligibilityMask;
        public float HardRockExposureMask;
        public float VoxelSeamMask;
        public float CraterMask;
        public WorldMacroGeologyZone PrimaryZone;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldMacroGeologyChunkKey
    {
        public int X;
        public int Z;
        public uint Seed;
        public uint ArtifactVersion;
        public uint ChunkSizeMeters;
    }
```

#### Structs from [CaveTypes.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/CaveTypes.cs)

```csharp
    /// <summary>
    /// Cave room node. Defines a spherical/ellipsoidal void carved from terrain.
    /// Positions are in WORLD SPACE (not relative to volume origin).
    /// Size: 40 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct CaveNode
    {
        /// <summary>World-space center of this room.</summary>
        [FieldOffset(0)]
        public float3 position;

        /// <summary>
        /// Half-extents along each axis.
        /// Sphere: only x used (uniform radius).
        /// Ellipsoid: all three used independently.
        /// VerticalShaft: x = horizontal radius, y = half-height, z = cap roundness.
        /// FlatHall: x,z = horizontal spread, y = compressed height.
        /// Crevice: x,z = compressed width, y = stretched height.
        /// </summary>
        [FieldOffset(12)]
        public float3 radii;

        /// <summary>Smooth blending radius when merging with adjacent nodes/tunnels.
        /// Higher = more organic, blobby transitions. Range: 4-32.</summary>
        [FieldOffset(24)]
        public float blendRadius;

        /// <summary>Scale multiplier for wall noise sampling on this room's surface.
        /// Allows per-room noise variation. Range: 0.5-2.0.</summary>
        [FieldOffset(28)]
        public float noiseScale;

        /// <summary>Amplitude multiplier for wall noise on this room.
        /// 0 = perfectly smooth walls. Range: 0-3.</summary>
        [FieldOffset(32)]
        public float noiseAmplitude;

        /// <summary>Shape of this room. Determines SDF evaluation path.</summary>
        [FieldOffset(36)]
        public CaveRoomType roomType;

        // Padding to 40 bytes.
        [FieldOffset(37)]
        public byte _pad0;
        [FieldOffset(38)]
        public byte _pad1;
        [FieldOffset(39)]
        public byte _pad2;
    }

    /// <summary>
    /// Tunnel connecting two points in the cave system.
    /// Implemented as a conic capsule SDF with optional cross-section scaling.
    /// Positions are in WORLD SPACE.
    /// Size: 56 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct CaveTunnel
    {
        /// <summary>World-space start point (typically center of source room).</summary>
        [FieldOffset(0)]
        public float3 pointA;

        /// <summary>World-space end point (typically center of target room).</summary>
        [FieldOffset(12)]
        public float3 pointB;

        /// <summary>Radius at point A. Range: 1-15.</summary>
        [FieldOffset(24)]
        public float radiusA;

        /// <summary>Radius at point B. Can differ from radiusA for tapered tunnels.
        /// Range: 1-15.</summary>
        [FieldOffset(28)]
        public float radiusB;

        /// <summary>Smooth blending radius when merging with rooms.
        /// Range: 4-32.</summary>
        [FieldOffset(32)]
        public float blendRadius;

        /// <summary>Vertical scale of cross-section.
        /// > 1 = tall canyon, < 1 = low crawlspace. 1 = round.</summary>
        [FieldOffset(36)]
        public float heightScale;

        /// <summary>Horizontal scale of cross-section.
        /// > 1 = wide passage, < 1 = narrow crack. 1 = round.</summary>
        [FieldOffset(40)]
        public float widthScale;

        /// <summary>Additional domain warp amplitude applied specifically to this tunnel.
        /// Stacks with global warp. Makes this particular tunnel more/less curvy.
        /// Range: 0-5.</summary>
        [FieldOffset(44)]
        public float warpAmount;

        /// <summary>Cross-section profile type.</summary>
        [FieldOffset(48)]
        public CaveTunnelType tunnelType;

        // Padding to 56 bytes.
        [FieldOffset(49)]
        public byte _pad0;
        [FieldOffset(50)]
        public byte _pad1;
        [FieldOffset(51)]
        public byte _pad2;
        [FieldOffset(52)]
        private uint _pad3;
    }

    /// <summary>
    /// Cave entrance point — connects cave void to terrain surface.
    /// Implemented as a conic capsule from surface point inward.
    /// The terrain mesh should have a hole at this location.
    /// Size: 72 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public struct CaveEntrance
    {
        /// <summary>World-space position of the entrance on terrain surface.</summary>
        [FieldOffset(0)]
        public float3 surfacePosition;

        /// <summary>Unit direction vector pointing INTO the cave (away from surface).
        /// Typically: normalized(nearestRoom - surfacePosition).</summary>
        [FieldOffset(12)]
        public float3 inwardDirection;

        /// <summary>Radius of the entrance opening at the surface. Range: 2-10.</summary>
        [FieldOffset(24)]
        public float radius;

        /// <summary>How far the entrance funnel extends before connecting to cave interior.
        /// Controls the "throat" length. Range: 5-30.</summary>
        [FieldOffset(28)]
        public float funnelLength;

        /// <summary>Radius at the inner end of the funnel. Typically radius * 0.5.
        /// Creates a narrowing entrance that opens into a larger space.</summary>
        [FieldOffset(32)]
        public float innerRadius;

        /// <summary>Terrain normal sampled at the cave mouth when MapMagic terrain is available.</summary>
        [FieldOffset(36)]
        public float3 terrainNormal;

        /// <summary>0..1 blend weight used to conform the mouth SDF to the terrain normal.</summary>
        [FieldOffset(48)]
        public float terrainNormalBlend;

        /// <summary>Terrain splat-derived RGB color at the mouth, A = valid/blend weight.</summary>
        [FieldOffset(52)]
        public float4 terrainSplatColor;

        /// <summary>0..1 confidence for terrainSplatColor.</summary>
        [FieldOffset(68)]
        public float terrainSplatBlend;
    }

    /// <summary>
    /// Internal cave structure — solid geometry ADDED BACK into cave void.
    /// Used for pillars, bridges, stalactites, boulders, ruins.
    /// These are evaluated AFTER cave subtraction and UNIONED into density.
    /// Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct CaveStructure
    {
        /// <summary>World-space center/base of the structure.</summary>
        [FieldOffset(0)]
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
        [FieldOffset(12)]
        public float3 size;

        /// <summary>Second point for oriented structures (Bridge end point, etc.).
        /// For non-oriented types, ignored.</summary>
        [FieldOffset(24)]
        public float3 pointB;

        /// <summary>Smooth blending radius. Range: 2-16.</summary>
        [FieldOffset(36)]
        public float blendRadius;

        /// <summary>Surface noise amplitude. 0 = smooth. Range: 0-1.</summary>
        [FieldOffset(40)]
        public float noiseAmount;

        /// <summary>Type of structure. Determines SDF evaluation.</summary>
        [FieldOffset(44)]
        public CaveStructureType structureType;

        // Padding to 48 bytes.
        [FieldOffset(45)]
        public byte _pad0;
        [FieldOffset(46)]
        public byte _pad1;
        [FieldOffset(47)]
        public byte _pad2;
    }

    /// <summary>
    /// All parameters controlling noise, warping, and surface detail
    /// inside VoxelDensityJob. Extracted from CavePreset at generation time.
    /// Passed as a single struct value (not NativeArray) into the job.
    /// Blittable. No managed references.
    /// Size: 80 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct CaveGenerationParams
    {
        // ── Domain Warping ──────────────────────────────────────────────────
        [FieldOffset(0)]
        public float warpFrequency;

        [FieldOffset(4)]
        public float warpAmplitude;

        [FieldOffset(8)]
        public int warpOctaves;

        // ── Wall Surface Noise ──────────────────────────────────────────────
        [FieldOffset(12)]
        public float wallNoiseFrequency;

        [FieldOffset(16)]
        public float wallNoiseAmplitude;

        [FieldOffset(20)]
        public int wallNoiseOctaves;

        [FieldOffset(24)]
        public float wallNoiseLacunarity;

        [FieldOffset(28)]
        public float wallNoisePersistence;

        // ── Horizontal Terraces ─────────────────────────────────────────────
        [FieldOffset(32)]
        public float terraceFrequency;

        [FieldOffset(36)]
        public float terraceAmplitude;

        [FieldOffset(40)]
        public float terraceSharpness;

        // ── Global Blending ─────────────────────────────────────────────────
        [FieldOffset(44)]
        public float globalBlendK;

        [FieldOffset(48)]
        public float shellThickness;

        // ── Seed ────────────────────────────────────────────────────────────
        [FieldOffset(52)]
        public uint seed;

        // ── Wall Noise Detail Threshold ─────────────────────────────────────
        [FieldOffset(56)]
        public float noiseEvalDistance;

        // ── Floor Flattening ────────────────────────────────────────────────
        [FieldOffset(60)]
        public float floorFlatness;

        // ── Structure Blending ──────────────────────────────────────────────
        [FieldOffset(64)]
        public float structureBlendK;

        // ── Entrance Blending ───────────────────────────────────────────────
        [FieldOffset(68)]
        public float entranceBlendK;

        [FieldOffset(72)]
        public byte structureOnlyMode;

        // ── Spawn Context ───────────────────────────────────────────
        [FieldOffset(73)]
        public SpawnContext spawnContext;

        [FieldOffset(74)]
        private byte _pad0;
        [FieldOffset(75)]
        private byte _pad1;
        [FieldOffset(76)]
        private byte _pad2;
        [FieldOffset(77)]
        private byte _pad3;
        [FieldOffset(78)]
        private byte _pad4;
        [FieldOffset(79)]
        private byte _pad5;
    }
```

---

### 2. TERRAIN HEIGHTMAP MATH (2D)

The complete code of the static evaluation function `EvaluateHeightMeters` from [WorldMacroGeologyFields.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs). This shows the full multi-scale geological noise pipeline (Simplex, Ridged Multifractal, domain warping, terracing, talus, and crater/pockmark passes).

```csharp
        public static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams parameters)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return 0f;

            return EvaluateHeightMeters(absoluteX, absoluteZ, in p, out _);
        }

        private static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams p, out MacroMasks masks)
        {
            float extent = math.max(MinimumWorldExtentMeters, p.WorldExtentMeters);
            float half = extent * 0.5f;
            float2 pos = new float2(absoluteX, absoluteZ);
            float2 norm = pos / extent;
            float lowWarp = (FractalNoise01(norm * 2.0f + new float2(11.7f, -3.9f), p.Seed ^ 0xB5297A4Du) * 2f - 1f) * 980f;
            float midWarp = (FractalNoise01(norm * 4.4f + new float2(-2.1f, 8.6f), p.Seed ^ 0x4CF5AD43u) * 2f - 1f) * 520f;
            float highWarp = (FractalNoise01(norm * 7.2f + new float2(-17.2f, 29.3f), p.Seed ^ 0x68E31DA4u) * 2f - 1f) * 240f;

            // DOMAIN WARPING: To break the "plastic" value noise look, we perturb the coordinates for high-frequency noise.
            float warpX = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x8A1F3C4Du) * 2f - 1f) * 0.005f; 
            float warpZ = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x3B8E1D2Fu) * 2f - 1f) * 0.005f;
            float2 warpedNorm = norm + new float2(warpX, warpZ);
            float2 warpedPos = warpedNorm * extent;

            // 1. CONTINENTAL SHELF / ABYSS BLEND
            float continentNoise = FractalSimplexNoise01(warpedNorm * 2.8f, p.Seed ^ 0x12345678u);
            float shelfMask = math.smoothstep(0.35f, 0.65f, continentNoise);
            
            // Steep, dramatic continental slope transition (ShelfBreak)
            float shelfBreakMask = 1f - math.saturate(math.abs(continentNoise - 0.5f) * 6.0f);
            shelfBreakMask = math.saturate(shelfBreakMask);
            
            // Canyon cuts on the shelf break
            float canyonNoise = FractalNoise01(warpedPos * 0.0004f, p.Seed ^ 0x0CA14405u);
            float canyonDepthProfile = math.pow(math.smoothstep(0.6f, 0.95f, canyonNoise), 3f);
            float canyonMask = canyonDepthProfile * math.smoothstep(0.1f, 0.9f, shelfBreakMask);
            
            // Base depth blend
            float depth = math.lerp(p.AbyssDepthMeters, p.ShelfDepthMeters, shelfMask);
            depth += canyonMask * 800f; // Deep canyon cuts

            // 2. MOUNTAIN RIDGES
            // Use Ridged Multifractal for sharp, peaked mountain ranges
            float ridgeNoise = RidgedMultifractal01(warpedNorm * 8.0f, p.Seed ^ 0x91E83B37u, 5);
            float ridgeMask = math.smoothstep(0.35f, 0.85f, ridgeNoise);
            depth -= ridgeMask * p.RidgeHeightMeters * (1f - shelfMask * 0.4f);

            // 3. DEEP OCEANIC TRENCHES / FAULTS
            float trenchNoise = RidgedMultifractal01(warpedNorm * 6.0f + new float2(0.4f, -0.6f), p.Seed ^ 0x4B3A2C1Du, 4);
            float trenchMask = math.smoothstep(0.55f, 0.95f, trenchNoise) * (1f - shelfMask);
            depth += trenchMask * p.TrenchDepthMeters;

            // 4. FAULT LINES
            float faultNoise = RidgedMultifractal01(warpedNorm * 12.0f, p.Seed ^ 0xCA97D1F3u, 3);
            float faultMask = math.smoothstep(0.45f, 0.85f, faultNoise) * (1f - shelfMask * 0.5f);
            depth += faultMask * 120f;

            // 5. ABYSSAL BASINS
            float basinMask = math.saturate((1f - shelfMask) * (1f - ridgeMask) * (1f - trenchMask));
            depth += basinMask * p.BasinDepthMeters;

            // 6. ABYSSAL SEAMOUNTS / GUYOTS (warpedPos deforms perfect circular shapes)
            float2 seamountCell = math.floor(warpedPos * 0.0003f); // 3.3km grid
            float2 frac = warpedPos * 0.0003f - seamountCell;
            float minDist = 8.0f;
            float2 seamountHash = new float2(0, 0);
            float2 seamountCenterLocal = new float2(0, 0);
            
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbor = new float2(x, y);
                    float2 pointHash = Hash2((int)(seamountCell.x + neighbor.x), (int)(seamountCell.y + neighbor.y), p.Seed ^ 0x5EA30447u);
                    float2 seamountDiff = neighbor + pointHash - frac;
                    float dist = math.length(seamountDiff);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        seamountHash = pointHash;
                        seamountCenterLocal = seamountDiff; // vector from current warpedPos to seamount center
                    }
                }
            }
            
            float seamountProfile = math.saturate(1f - minDist * 2f);
            if (seamountProfile > 0f)
            {
                // Volcanic exponential profile
                float volProfile = math.exp(-minDist * 6.0f);
                
                float isGuyot = HashToUnitFloat(Hash(unchecked((int)(seamountHash.x * 1000f)), unchecked((int)(seamountHash.y * 1000f)), 0x123456)) > 0.5f ? 1f : 0f;
                
                if (isGuyot > 0f)
                {
                    // Guyot: Flat top
                    volProfile = math.min(volProfile, 0.4f);
                }
                else
                {
                    // Caldera (Depression at the very center)
                    float calderaProfile = 1f - math.smoothstep(0f, 0.045f, minDist);
                    volProfile -= calderaProfile * 0.3f * seamountProfile;
                }
                
                // Radial Erosional Gullies (seam-free organic branching using normalized direction and warpedPos phase shift)
                float2 dir = minDist > 0.0001f ? seamountCenterLocal / minDist : new float2(1f, 0f);
                float gullyPattern = (FractalSimplexNoise01(dir * 3.8f + warpedPos * 0.0005f, p.Seed ^ 0x901177Au) * 2f - 1f);
                float gullyProfile = 1f - math.abs(gullyPattern);
                gullyProfile = math.pow(gullyProfile, 3.0f); // Sharper cuts
                
                // Gullies only form on the flanks
                float flankMask = math.smoothstep(0.05f, 0.3f, minDist) * math.smoothstep(0.4f, 0.25f, minDist);
                volProfile -= gullyProfile * flankMask * 0.15f;
                
                depth -= math.saturate(volProfile) * basinMask * 2600f;
            }

            // 3. INTERNAL PLATE FEATURES (Highlands & Warps)
            float provinceRelief = math.smoothstep(0.36f, 0.92f, FractalNoise01(warpedPos * 0.00006f, p.Seed ^ 0x21DA7F47u));
            depth += provinceRelief * 145f * math.saturate(shelfMask + basinMask);

            // Tectonic Network (Internal smaller faults)
            float internalNetwork = 1f - 2f * math.abs(FractalNoise01(warpedPos * 0.00015f, p.Seed ^ 0xCA97D1F3u) - 0.5f);
            internalNetwork = math.smoothstep(0.85f, 0.98f, internalNetwork); // Tighter faults
            float fractureMask = math.max(faultMask, internalNetwork * 0.5f);
            depth += internalNetwork * 80f; // Reduced from 150f

            float descent01 = 1f - shelfMask;
            // Relief gate controls where chaotic noise is allowed. Keep it near 0 on flat shelves and basins.
            float reliefGate = math.saturate(shelfBreakMask * 0.6f + ridgeMask * 0.8f + faultMask * 0.4f);

            // REALISTIC TECTONIC BREAKUP:
            // Macro uses RidgedMultifractal to create sharp, eroded-looking mountain peaks instead of rounded value noise hills.
            // Meso and Micro use Simplex for natural organic surface roughness without grid artifacts.
            float macroBreakup = RidgedMultifractal01(warpedNorm * 18.0f + new float2(7.7f, 41.3f), p.Seed ^ 0x91E83B37u, 5);
            float mesoBreakup = FractalSimplexNoise01(warpedNorm * 48.0f + new float2(-23.1f, 5.6f), p.Seed ^ 0x6C8E9CF5u) * 2f - 1f;
            float microBreakup = FractalSimplexNoise01(warpedNorm * 220.0f + new float2(33.1f, -14.6f), p.Seed ^ 0x1A2B3C4Du) * 2f - 1f;
            
            // Apply macro breakup as sharp peaks (subtracting depth) where relief is allowed
            depth -= macroBreakup * 350f * reliefGate; 
            depth += mesoBreakup * 140f * math.saturate(reliefGate + shelfBreakMask * 0.2f);
            float microBreakupWeight = math.saturate(ridgeMask * 0.6f + faultMask * 0.4f + reliefGate * 0.5f);
            depth += microBreakup * math.lerp(10f, 60f, microBreakupWeight);
            depth += fractureMask * 60f;

            // MESO/MICRO DETAIL PASS
            float rockDetailNoise = (FractalNoise01(warpedNorm * 150.0f + new float2(-44.2f, 88.1f), p.Seed ^ 0x7B9C1A2Fu) * 2f - 1f);
            float rockyRidgeDetail = 1f - 2f * math.abs(FractalNoise01(warpedNorm * 320.0f + new float2(11.4f, -99.3f), p.Seed ^ 0x5E8A9C1Du) - 0.5f);
            
            float hardRockExposure = math.saturate(ridgeMask * 0.6f + faultMask * 0.4f + math.saturate(descent01 * 1.5f) * 0.20f);
            float mesoDetailWeight = math.saturate(hardRockExposure * 0.8f + reliefGate * 0.3f);
            
            depth += rockDetailNoise * 35f * mesoDetailWeight;
            depth -= rockyRidgeDetail * 30f * mesoDetailWeight * math.saturate(descent01); 

            // SEDIMENT DUNE/RIPPLE DETAIL PASS
            float duneSample = FractalSimplexNoise01(warpedPos * 0.05f, p.Seed ^ 0xD11EBA5Eu);
            duneSample = 1f - math.abs(duneSample); // Create sharp ridges and wide valleys
            duneSample = math.pow(duneSample, 1.8f); // Pin the valleys flatter
            
            // Patch masking: dunes only appear in specific fields
            float duneFieldMask = FractalNoise01(warpedPos * 0.0015f, p.Seed ^ 0xA8B2C41Eu);
            duneFieldMask = math.smoothstep(0.4f, 0.6f, duneFieldMask); // Sharp transition into dune fields
            
            float sedimentDepth = math.saturate(1f - math.saturate(hardRockExposure * 1.5f));
            
            float duneAmplitude = math.lerp(4f, 1f, depth / 6000f);
            float addedHeight = duneSample * duneAmplitude * sedimentDepth * duneFieldMask;
            
            // CELLULAR PITS PASS (Craters / Pockmarks / Subsidence)
            float2 cellHash;
            float cellDist = CellularDistance01(warpedPos * 0.012f, p.Seed ^ 0xF131A21Eu, out cellHash);
            
            // We want deep pits at the center (cellDist near 0).
            float pitProfile = math.saturate(1f - cellDist * 3f); // Only the central 33% of the cell
            pitProfile = math.pow(pitProfile, 2.5f); // Make it a bowl shape
            
            // Pits appear in clusters
            float pitFieldMask = FractalNoise01(warpedPos * 0.0008f, p.Seed ^ 0x99BBE211u);
            pitFieldMask = math.smoothstep(0.5f, 0.7f, pitFieldMask);
            
            // Pits subtract from sediment depth. Max pit depth is 6m.
            float pitDepth = pitProfile * pitFieldMask * sedimentDepth * 6f;
            
            // METEOR CRATERS PASS (with rim-warping to prevent perfect mathematical circles)
            float craterDepthDelta = 0f;
            float craterMask = 0f;
            
            float craterGridSize = 2000f;
            int2 craterCell = new int2((int)math.floor(warpedPos.x / craterGridSize), (int)math.floor(warpedPos.y / craterGridSize));
            
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 craterNeighborCell = craterCell + new int2(dx, dz);
                    uint h = Hash(craterNeighborCell.x, craterNeighborCell.y, unchecked((int)(p.Seed ^ 0x9B3A21EFu)));
                    
                    // ~15% chance of a crater in this 2km cell
                    float probability = HashToUnitFloat(h ^ 0x12345678u);
                    if (probability > 0.15f) continue;
                    
                    float cx = (craterNeighborCell.x + HashToUnitFloat(h ^ 0x87654321u)) * craterGridSize;
                    float cz = (craterNeighborCell.y + HashToUnitFloat(h ^ 0xA1B2C3D4u)) * craterGridSize;
                    
                    // Radius between 120m and 600m
                    float radius = math.lerp(120f, 600f, math.pow(HashToUnitFloat(h ^ 0x1A2B3C4Du), 2.5f)); 
                    
                    float dist = math.length(new float2(warpedPos.x - cx, warpedPos.y - cz));
                    if (dist > radius * 2.0f) continue;
                    
                    // rimWarp: deforms the crater radius so it is NOT a perfect circle
                    float rimWarp = (FractalSimplexNoise01(warpedPos * 0.015f, h ^ 0xDEADBEEFu) * 2f - 1f) * 0.06f;
                    float normalizedDist = dist / radius + rimWarp;
                    
                    // Crater Cavity
                    float bowl = 1f - math.smoothstep(0f, 1f, normalizedDist); 
                    bowl = math.pow(bowl, 1.5f); // Flatten the center due to sedimentation
                    
                    // Crater Rim
                    float rimProfile = math.max(0f, 1f - math.abs(normalizedDist - 1f) * 2.5f);
                    rimProfile = math.smoothstep(0f, 1f, rimProfile);
                    
                    // Central Peak (only in large craters)
                    float peak = 0f;
                    if (radius > 1200f) {
                        float peakRadius = radius * 0.15f;
                        peak = 1f - math.smoothstep(0f, peakRadius, dist);
                        peak = math.smoothstep(0f, 1f, peak) * 0.4f;
                    }
                    
                    // Rim Erosion Noise
                    float angle = math.atan2(warpedPos.y - cz, warpedPos.x - cx);
                    float rimErosion = FractalNoise01(new float2(angle * 4.0f, radius), h ^ 0xDEADBEEFu);
                    rimProfile *= (0.4f + rimErosion * 0.6f);
                    
                    float maxDepth = radius * 0.18f;
                    float maxRimHeight = radius * 0.08f;
                    
                    craterDepthDelta += bowl * maxDepth;     // Depress (add to depth)
                    craterDepthDelta -= peak * maxDepth;     // Raise peak (subtract from depth)
                    craterDepthDelta -= rimProfile * maxRimHeight; // Raise rim
                    
                    craterMask = math.max(craterMask, bowl);
                }
            }
            depth += craterDepthDelta;
            
            depth -= (addedHeight - pitDepth);

            if (depth < -260f)
                depth = -260f + (depth + 260f) * 0.42f;
            depth = math.clamp(depth, -620f, p.HadalDepthMeters);

            // TECTONIC TERRACING — Localized Geological Strata
            float terraceStrength = math.saturate(shelfBreakMask * 0.8f + ridgeMask * 0.4f + faultMask * 0.5f);
            if (terraceStrength > 0.05f)
            {
                // STEP 1: LARGE STEPS → only 3-5 terraces on a 400m mountain.
                float dynamicTerraceScale = math.lerp(80.0f, 180.0f,
                    FractalSimplexNoise01(warpedNorm * 3.0f, p.Seed ^ 0x112233u));

                // STEP 2: STRATA TILT via pos (meters). 50m per km = 1-2 step shifts across mountain.
                float2 tiltDir = math.normalize(new float2(
                    FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0xAB12CD34u) * 2f - 1f,
                    FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0x56EF78ABu) * 2f - 1f
                ));
                float strataCoord = depth + math.dot(tiltDir, pos) * 0.05f;

                // STEP 3: EROSION at mountain scale. ±60m+±25m on 80-180m steps = 0.33-0.75 step shift.
                float terraceErosionC = (FractalSimplexNoise01(warpedNorm * 80.0f,  p.Seed ^ 0x99AA88BBu) * 2f - 1f) * 60.0f;
                float terraceErosionF = (FractalSimplexNoise01(warpedNorm * 250.0f, p.Seed ^ 0x77CC4411u) * 2f - 1f) * 25.0f;
                float terraceErosion  = terraceErosionC + terraceErosionF;

                // STEP 4: QUANTIZE with sharp cliff wall at top of step.
                float hPhase = (strataCoord + terraceErosion) / dynamicTerraceScale;
                float fStep  = math.frac(hPhase);
                float sStep  = math.smoothstep(0.55f, 0.88f, fStep);

                float terracedCoord = (math.floor(hPhase) + sStep) * dynamicTerraceScale - terraceErosion;
                float terracedDepth = terracedCoord - math.dot(tiltDir, pos) * 0.05f;

                // STEP 5: AGGRESSIVE PATCHINESS — only ~30% of mountain gets terracing.
                float terracePatchMask = math.smoothstep(0.60f, 0.92f,
                    FractalSimplexNoise01(warpedNorm * 4.5f, p.Seed ^ 0x992211AAu));

                // STEP 6: MAX BLEND 0.55 — macro shape always reads through.
                depth = math.lerp(depth, terracedDepth, terraceStrength * terracePatchMask * 0.55f);
            }

            // TALUS / SCREE ACCUMULATION
            float rockBase  = math.saturate(ridgeMask * 0.7f + faultMask * 0.4f + math.saturate((1f - shelfMask) * 1.5f) * 0.3f);
            float slope01   = math.saturate(shelfBreakMask * 0.9f + ridgeMask * 0.8f + faultMask * 0.4f);
            float screeMask = math.smoothstep(0.05f, 0.30f, slope01) * (1.0f - math.smoothstep(0.40f, 0.65f, slope01));
            float screeC    = RidgedMultifractal01(warpedNorm * 140.0f, p.Seed ^ 0xE70D1A5Bu, 3);
            float screeF    = RidgedMultifractal01(warpedNorm * 480.0f,  p.Seed ^ 0xC3F19802u, 2);
            float screeRubble = ((screeC * 0.7f + screeF * 0.3f) * 2f - 1f) * 35.0f;
            depth += screeRubble * screeMask * rockBase;

            masks = new MacroMasks
            {
                Shelf = math.saturate(shelfMask),
                ShelfBreak = math.saturate(shelfBreakMask),
                Ridge = math.saturate(ridgeMask),
                Trench = math.saturate(trenchMask),
                Basin = math.saturate(basinMask),
                Fault = math.saturate(fractureMask),
                Crater = math.saturate(craterMask)
            };
            return p.WaterSurfaceY - depth;
        }
```

---

### 3. CAVE SDF GENERATION MATH (3D)

The complete code of `ProceduralCaveSdfCarveJob` from [WorldProceduralCaveSdfJobs.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/World/WorldProceduralCaveSdfJobs.cs). This job runs on the voxel volume points to hollow out cave systems from the solid rock underneath the heightmap, applying Surface Protection limits and Strata flat floor overlays.

```csharp
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ProceduralCaveSdfCarveJob : IJobParallelFor
    {
        /// <summary>SDF density array. Positive means solid rock, negative means air/water.</summary>
        [NoAlias] public NativeArray<float> Sdf;

        public int SdfWidth;
        public int SdfHeight;
        public int SdfDepth;
        public float VoxelSizeMeters;
        public double3 SdfOriginAup;

        /// <summary>Base frequency for the primary cave worm noise (meters^-1). Good range: 0.008..0.020.</summary>
        public float PrimaryFrequency;

        /// <summary>Base frequency for the secondary cave worm noise. Should differ from primary to create intersections.</summary>
        public float SecondaryFrequency;

        /// <summary>How aggressively the noise carves the SDF. Units: meters of density subtracted at full cave mask.</summary>
        public float CarveStrengthMeters;

        /// <summary>Threshold for the combined noise. Higher = fewer caves. Good range: 0.55..0.75.</summary>
        public float CaveThreshold;

        /// <summary>Maximum depth INTO solid rock (density) where caves can form. Beyond this, the voxel is deep core.</summary>
        public float MaxCrustDepthMeters;

        /// <summary>Minimum solid density required before carving is allowed. Protects the terrain surface.</summary>
        public float SurfaceProtectionMeters;

        /// <summary>Vertical period of geological strata shelving (meters). Creates flat cave floors.</summary>
        public float StrataLayerThicknessMeters;

        /// <summary>How much strata shelving pushes density back (meters). Higher = flatter cave floors.</summary>
        public float StrataShelvingStrength;

        /// <summary>World-global seed. Must be the SAME for all chunks so the noise field is continuous.</summary>
        public uint WorldSeed;

        public void Execute(int index)
        {
            float currentDensity = Sdf[index];

            // Early-out 1: Do not touch the water column or barely-solid surface.
            // SurfaceProtectionMeters creates a fade zone near the terrain surface to prevent breakup.
            if (currentDensity < SurfaceProtectionMeters)
                return;

            // Early-out 2: Do not waste cycles carving deep core rock that the player will never reach.
            if (currentDensity > MaxCrustDepthMeters)
                return;

            // Decompose flat index -> (x, y, z)
            int slice = SdfWidth * SdfHeight;
            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / SdfWidth;
            int x = rem - y * SdfWidth;

            // Absolute universe position
            double absX = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absY = SdfOriginAup.y + y * (double)VoxelSizeMeters;
            double absZ = SdfOriginAup.z + z * (double)VoxelSizeMeters;

            // Wrap coordinates into safe snoise range.
            // We use fmod with a large period that is NOT a power of 2 to avoid tiling artifacts.
            // 4096.0 * 1.618... ≈ 6627.0 — irrational-ish period breaks grid alignment.
            const double wrapPeriod = 6627.0;
            float3 p = new float3(
                (float)Fmod(absX, wrapPeriod),
                (float)Fmod(absY, wrapPeriod),
                (float)Fmod(absZ, wrapPeriod)
            );

            // Seed offsets: keep them small and within the wrap period.
            // Use bitwise extraction from the seed to generate small, deterministic offsets.
            float seedOffX = ((WorldSeed & 0xFFu) - 128f) * 0.5f;         // -64..+63.5
            float seedOffY = (((WorldSeed >> 8) & 0xFFu) - 128f) * 0.5f;  // -64..+63.5
            float seedOffZ = (((WorldSeed >> 16) & 0xFFu) - 128f) * 0.5f; // -64..+63.5
            float3 seedOffset = new float3(seedOffX, seedOffY, seedOffZ);

            // === Primary Worm Field (horizontal-biased tunnels) ===
            float primary = EvaluateRidgedWorm(p + seedOffset, PrimaryFrequency, 1.0f);

            // === Secondary Worm Field (vertical-biased fissures) ===
            // Rotate the coordinate space to create an independent noise field that intersects the primary.
            float3 p2 = new float3(p.z + seedOffset.z * 1.7f, p.x + seedOffset.x * 1.3f, p.y + seedOffset.y * 0.9f);
            float secondary = EvaluateRidgedWorm(p2, SecondaryFrequency, 0.7f);

            // Swiss Cheese intersection: cave exists where BOTH worm fields are high.
            // This creates tunnels at the intersection of two independent noise ridges.
            float combined = primary * secondary;

            // Threshold with soft transition
            float caveMask = math.smoothstep(CaveThreshold - 0.05f, CaveThreshold + 0.05f, combined);

            // No cave? Don't touch the density at all.
            if (caveMask < 0.001f)
                return;

            // Depth-based fade: caves get smaller and rarer deeper into the rock.
            // This prevents massive voids deep underground while allowing large chambers near the surface.
            float depthFraction = math.saturate(currentDensity / MaxCrustDepthMeters);
            float depthFade = 1.0f - depthFraction * depthFraction; // Quadratic fade

            // Surface protection fade: smooth transition near the terrain surface.
            // Prevents caves from cleanly slicing through the surface and creating ugly holes.
            float surfaceFade = math.smoothstep(SurfaceProtectionMeters, SurfaceProtectionMeters + 8.0f, currentDensity);

            // Combined carve strength
            float carve = caveMask * CarveStrengthMeters * depthFade * surfaceFade;

            // Strata shelving: periodic vertical density restoration.
            float strataThickness = math.max(4.0f, StrataLayerThicknessMeters);
            float strataPhase = (float)absY / strataThickness;
            float strataFrac = math.abs(math.frac(strataPhase) * 2.0f - 1.0f);
            
            // Only push density back at layer boundaries. 
            float strataRestore = (1.0f - strataFrac) * StrataShelvingStrength * caveMask * surfaceFade;
            
            // Final density modification.
            float targetDensity = currentDensity - carve + strataRestore;

            // === CRITICAL SAFETY NET & ORGANIC BLENDING ===
            // Use Polynomial Smooth Minimum (smin) to organically blend the cave SDF with the base terrain SDF.
            float smoothedDensity = Smin(currentDensity, targetDensity, 5.0f);
            
            // PREVENT CHUNK BOUNDARY TEARS: Smin(A,A) shifts density by -k/4. 
            // We must mask out this shift in solid/air chunks where carve is zero to perfectly align with skipped chunks.
            float caveInfluence = math.saturate(caveMask * surfaceFade * 100.0f);
            float newDensity = math.lerp(currentDensity, smoothedDensity, caveInfluence);
            
            // We also must clamp to currentDensity to NEVER add rock that didn't exist in the base terrain
            newDensity = math.min(newDensity, currentDensity);
            
            Sdf[index] = newDensity;
        }

        /// <summary>
        /// Evaluates a 3-octave ridged multifractal in 3D.
        /// Returns a value where high = ridge centerline (potential tunnel).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateRidgedWorm(float3 p, float frequency, float verticalBias)
        {
            float3 scale = new float3(1.0f, verticalBias, 1.0f);

            // Octave 0: base tunnels
            float3 p0 = p * scale * frequency;
            float n0 = 1.0f - math.abs(noise.snoise(p0));
            n0 *= n0; // Square to sharpen ridges into thin tunnel centerlines

            // Octave 1: medium detail (lacunarity 2.17, gain 0.5)
            float3 p1 = p * scale * (frequency * 2.17f) + 7.31f;
            float n1 = 1.0f - math.abs(noise.snoise(p1));
            n1 *= n1;

            // Octave 2: fine detail (lacunarity 4.71, gain 0.25)
            float3 p2 = p * scale * (frequency * 4.71f) + 13.97f;
            float n2 = 1.0f - math.abs(noise.snoise(p2));
            n2 *= n2;

            // Weight-modulated sum: successive octaves are modulated by the previous.
            // This creates connected tunnels rather than isolated pockets.
            float weight = 1.0f;
            float total = n0 * weight;
            weight = math.saturate(n0 * 2.0f);
            total += n1 * 0.5f * weight;
            weight = math.saturate(n1 * 2.0f);
            total += n2 * 0.25f * weight;

            // Normalize to approximately 0..1
            return total / 1.75f;
        }

        /// <summary>
        /// Polynomial Smooth Minimum (smin) for organic SDF composition.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smin(float a, float b, float k)
        {
            float h = math.saturate(0.5f + 0.5f * (b - a) / k);
            return math.lerp(b, a, h) - k * h * (1.0f - h);
        }

        /// <summary>
        /// Deterministic fmod that always returns a positive value in [0, period).
        /// Standard C# % can return negative values for negative inputs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Fmod(double value, double period)
        {
            double result = value - math.floor(value / period) * period;
            return result;
        }
    }
```

---

### 4. THE MESHING BRIDGE (How Voxels read Terrain)

Voxel points query 2D terrain height to construct the base solid field before carving. Below are the key routines and Burst jobs responsible for this integration.

#### Terrain Density Mapping from [VoxelSeamDirector.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/VoxelSeamDirector.cs)

```csharp
        /// <summary>
        /// Returns signed terrain density. Zero is the exact MapMagic/voxel handoff plane.
        /// Positive values are solid terrain below the seafloor; negative values are open space above it.
        /// </summary>
        public static float ComputeTerrainDensity(float terrainHeight, float sampleHeight)
        {
            return math.clamp(terrainHeight - sampleHeight, -50f, 50f);
        }
```

#### Sampling and Blending terrain heights in `VoxelDensityJob` from [HectonVoxelEngine.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonVoxelEngine.cs)

```csharp
    void EvaluateDensityAt(float3 wp, out float smoothDensityValue, out float finalDensityValue)
    {
        bool structureOnlyMode = caveParams.structureOnlyMode != 0;
        float terrainH = SampleTerrainHeight(wp.xz);
        float terrainDensity = structureOnlyMode
            ? -1f
            : VoxelSeamDirector.ComputeTerrainDensity(terrainH, wp.y);

        smoothDensityValue = terrainDensity;
        finalDensityValue = terrainDensity;

        float smoothCaveSdf = 1f;
        float finalCaveSdf = 1f;
        if (!structureOnlyMode)
        {
            EvaluateCaveSDF(wp, out smoothCaveSdf, out finalCaveSdf);

            if (smoothCaveSdf < caveParams.shellThickness)
                smoothDensityValue = SmoothSubtractionQuadratic(-smoothCaveSdf, terrainDensity, caveParams.shellThickness);

            if (finalCaveSdf < caveParams.shellThickness)
                finalDensityValue = SmoothSubtractionQuadratic(-finalCaveSdf, terrainDensity, caveParams.shellThickness);
        }

        if (!structureOnlyMode && caveEntrances.Length > 0)
        {
            float entranceSkirtSDF = EvaluateEntranceSkirtSDF(wp);
            if (entranceSkirtSDF < caveParams.entranceBlendK)
            {
                float skirtBlend = caveParams.entranceBlendK * 0.45f;
                smoothDensityValue = SmoothMaxQuadratic(smoothDensityValue, -entranceSkirtSDF, skirtBlend);
                finalDensityValue = SmoothMaxQuadratic(finalDensityValue, -entranceSkirtSDF, skirtBlend);
            }
        }

        if (caveStructures.Length > 0 && (structureOnlyMode || smoothCaveSdf < 0f || finalCaveSdf < 0f))
        {
            EvaluateStructuresSDF(wp, out float smoothStructureSdf, out float finalStructureSdf);
            if (smoothStructureSdf < caveParams.structureBlendK)
                smoothDensityValue = SmoothMaxQuadratic(smoothDensityValue, -smoothStructureSdf, caveParams.structureBlendK);

            if (finalStructureSdf < caveParams.structureBlendK)
                finalDensityValue = SmoothMaxQuadratic(finalDensityValue, -finalStructureSdf, caveParams.structureBlendK);
        }

        if (craterStamps.IsCreated && craterStamps.Length > 0)
        {
            smoothDensityValue = EvaluateCraterModifiers(wp, smoothDensityValue);
            finalDensityValue = EvaluateCraterModifiers(wp, finalDensityValue);
        }

        ApplyAlienBiomeSdfModifier(wp, ref smoothDensityValue, ref finalDensityValue);

        if (modifiedCells.IsCreated &&
            modifiedCellCount > 0 &&
            TryResolveModifiedCell(ResolveAbsoluteCell(wp), out VoxelModifiedCell storedCell))
        {
            float deltaDensity = (float)storedCell.Density;
            if ((storedCell.Flags & DeltaModeReplace) != 0)
            {
                smoothDensityValue = deltaDensity;
                finalDensityValue = deltaDensity;
            }
            else if ((storedCell.Flags & DeltaModeAdditive) != 0)
            {
                smoothDensityValue = math.max(smoothDensityValue, deltaDensity);
                finalDensityValue = math.max(finalDensityValue, deltaDensity);
            }
            else
            {
                smoothDensityValue = math.min(smoothDensityValue, deltaDensity);
                finalDensityValue = math.min(finalDensityValue, deltaDensity);
            }
        }

        if (!structureOnlyMode)
        {
            smoothDensityValue = ApplyEdgeSeal(wp, smoothDensityValue);
            finalDensityValue = ApplyEdgeSeal(wp, finalDensityValue);
        }
    }

    float SampleTerrainHeight(float2 worldXZ)
    {
        float localX = (worldXZ.x - volumeOrigin.x) / voxelStep;
        float localZ = (worldXZ.y - volumeOrigin.z) / voxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float h00 = terrainHeights[x0 + z0 * ptsX];
        float h10 = terrainHeights[x1 + z0 * ptsX];
        float h01 = terrainHeights[x0 + z1 * ptsX];
        float h11 = terrainHeights[x1 + z1 * ptsX];
        return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
    }
```

#### Seam Vertex Snapping Job from [HectonVoxelEngine.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonVoxelEngine.cs)

```csharp
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelTerrainSeamSnapJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    public float seamTransitionBand;
    public float seamOverlap;

    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [NoAlias] public NativeArray<float3> positions;

    public void Execute(int idx)
    {
        long terrainGridLength = (long)ptsX * ptsZ;
        if (!terrainHeights.IsCreated ||
            !positions.IsCreated ||
            idx < 0 ||
            idx >= positions.Length ||
            ptsX <= 1 ||
            ptsZ <= 1 ||
            terrainGridLength <= 0L ||
            terrainGridLength > terrainHeights.Length ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f ||
            !math.isfinite(seamTransitionBand) ||
            seamTransitionBand <= 0f ||
            !IsFinite(volumeOrigin))
        {
            return;
        }

        float3 position = positions[idx];
        if (!IsFinite(position))
            return;

        float boundaryDistance = VoxelSeamDirector.ComputeBoundaryDistance(
            position.xz,
            volumeOrigin,
            ptsX,
            ptsZ,
            voxelStep);
        if (!math.isfinite(boundaryDistance) || boundaryDistance > seamTransitionBand)
            return;

        float terrainHeight = SampleTerrainHeight(position.xz);
        if (!math.isfinite(terrainHeight))
            return;

        float blendToTerrain = VoxelSeamDirector.ComputeBoundaryBlend01(boundaryDistance, seamTransitionBand);
        if (!math.isfinite(blendToTerrain))
            return;

        float targetHeight = VoxelSeamDirector.ComputeTargetSnapHeight(terrainHeight, seamOverlap);
        float snappedY = math.lerp(position.y, targetHeight, blendToTerrain);
        if (!math.isfinite(targetHeight) || !math.isfinite(snappedY))
            return;

        positions[idx] = new float3(position.x, snappedY, position.z);
    }

    float SampleTerrainHeight(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - volumeOrigin.x) / voxelStep;
        float localZ = (absoluteWorldXZ.y - volumeOrigin.z) / voxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float h00 = terrainHeights[x0 + z0 * ptsX];
        float h10 = terrainHeights[x1 + z0 * ptsX];
        float h01 = terrainHeights[x0 + z1 * ptsX];
        float h11 = terrainHeights[x1 + z1 * ptsX];
        if (!math.isfinite(h00) || !math.isfinite(h10) || !math.isfinite(h01) || !math.isfinite(h11))
            return float.NaN;

        return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }
}
```

#### Seam Normal Blending Job from [HectonVoxelEngine.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonVoxelEngine.cs)

```csharp
[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct VoxelSeamNormalBlendJob : IJobParallelFor
{
    public int ptsX;
    public int ptsZ;
    public float3 volumeOrigin;
    public float voxelStep;
    public float seamTransitionBand;

    [ReadOnly, NoAlias] public NativeArray<float3> positions;
    [ReadOnly, NoAlias] public NativeArray<float> terrainHeights;
    [NoAlias] public NativeArray<float3> normals;

    public void Execute(int idx)
    {
        long terrainGridLength = (long)ptsX * ptsZ;
        if (!terrainHeights.IsCreated ||
            !positions.IsCreated ||
            !normals.IsCreated ||
            idx < 0 ||
            idx >= positions.Length ||
            idx >= normals.Length ||
            ptsX <= 1 ||
            ptsZ <= 1 ||
            terrainGridLength <= 0L ||
            terrainGridLength > terrainHeights.Length ||
            !math.isfinite(voxelStep) ||
            voxelStep <= 0.0001f ||
            !math.isfinite(seamTransitionBand) ||
            seamTransitionBand <= 0f ||
            !IsFinite(volumeOrigin))
        {
            return;
        }

        float3 position = positions[idx];
        if (!IsFinite(position))
            return;

        float boundaryDistance = VoxelSeamDirector.ComputeBoundaryDistance(
            position.xz,
            volumeOrigin,
            ptsX,
            ptsZ,
            voxelStep);
        if (!math.isfinite(boundaryDistance) || boundaryDistance > seamTransitionBand)
            return;

        float3 terrainNormal = SampleTerrainNormal(position.xz);
        if (!IsFinite(terrainNormal))
            return;

        float3 voxelNormal = NormalizeFastOrDefault(normals[idx], new float3(0f, 1f, 0f));
        float blendToTerrain = VoxelSeamDirector.ComputeBoundaryBlend01(boundaryDistance, seamTransitionBand);
        if (!math.isfinite(blendToTerrain))
            return;

        float3 blendedNormal = BlendNormalsNlerp(voxelNormal, terrainNormal, blendToTerrain);
        if (!IsFinite(blendedNormal))
            return;

        normals[idx] = blendedNormal;
    }

    float3 SampleTerrainNormal(float2 absoluteWorldXZ)
    {
        float localX = (absoluteWorldXZ.x - volumeOrigin.x) / voxelStep;
        float localZ = (absoluteWorldXZ.y - volumeOrigin.z) / voxelStep;
        localX = math.clamp(localX, 0f, ptsX - 1f);
        localZ = math.clamp(localZ, 0f, ptsZ - 1f);

        int x0 = (int)localX;
        int z0 = (int)localZ;
        int x1 = math.min(x0 + 1, ptsX - 1);
        int z1 = math.min(z0 + 1, ptsZ - 1);
        float fx = localX - x0;
        float fz = localZ - z0;

        float3 normal00 = ResolveTerrainGridNormal(x0, z0);
        float3 normal10 = ResolveTerrainGridNormal(x1, z0);
        float3 normal01 = ResolveTerrainGridNormal(x0, z1);
        float3 normal11 = ResolveTerrainGridNormal(x1, z1);
        float3 normalX0 = math.lerp(normal00, normal10, fx);
        float3 normalX1 = math.lerp(normal01, normal11, fx);
        float3 normal = NormalizeFastOrDefault(math.lerp(normalX0, normalX1, fz), new float3(0f, 1f, 0f));
        return IsFinite(normal) ? normal : new float3(0f, 1f, 0f);
    }

    float3 ResolveTerrainGridNormal(int x, int z)
    {
        int xPrev = math.max(x - 1, 0);
        int xNext = math.min(x + 1, ptsX - 1);
        int zPrev = math.max(z - 1, 0);
        int zNext = math.min(z + 1, ptsZ - 1);

        float heightLeft = terrainHeights[xPrev + z * ptsX];
        float heightRight = terrainHeights[xNext + z * ptsX];
        float heightBack = terrainHeights[x + zPrev * ptsX];
        float heightForward = terrainHeights[x + zNext * ptsX];
        if (!math.isfinite(heightLeft) || !math.isfinite(heightRight) || !math.isfinite(heightBack) || !math.isfinite(heightForward))
            return new float3(0f, 1f, 0f);

        float stepX = math.max((xNext - xPrev) * voxelStep, voxelStep);
        float stepZ = math.max((zNext - zPrev) * voxelStep, voxelStep);
        float3 tangentX = new float3(stepX, heightRight - heightLeft, 0f);
        float3 tangentZ = new float3(0f, heightForward - heightBack, stepZ);
        return NormalizeFastOrDefault(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
    }

    static float3 BlendNormalsNlerp(float3 startNormal, float3 endNormal, float t)
    {
        float blend = math.isfinite(t) ? math.saturate(t) : 0f;
        return NormalizeFastOrDefault(math.lerp(startNormal, endNormal, blend), startNormal);
    }

    static float3 NormalizeFastOrDefault(float3 value, float3 fallback)
    {
        if (!IsFinite(value))
            return fallback;

        float lengthSq = math.lengthsq(value);
        return math.isfinite(lengthSq) && lengthSq > 0.0001f ? value / math.max(LengthApprox(value), 0.0001f) : fallback;
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    static float LengthApprox(float3 value)
    {
        float3 axis = math.abs(value);
        float maxAxis = math.cmax(axis);
        float minAxis = math.cmin(axis);
        float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
        return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
    }
}
```

---

#### CSG Math Utilities from [HectonVoxelEngine.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonVoxelEngine.cs)

```csharp
    /// <summary>Polynomial smooth minimum (cubic). Merges shapes organically.</summary>
    static float SmoothMin(float a, float b, float k)
    {
        k = math.max(k, 0.0001f);
        float h = math.max(k - math.abs(a - b), 0f) / k;
        return math.min(a, b) - h * h * h * k * (1f / 6f);
    }

    static float SmoothMinQuadratic(float a, float b, float k)
    {
        float width = math.max(k, 0.0001f);
        float blend = math.max(0f, width - math.abs(a - b));
        float smoothDrop = (blend * blend) * (0.25f / width);
        return math.min(a, b) - smoothDrop;
    }

    /// <summary>Smooth maximum. Inverse of smooth min.</summary>
    static float SmoothMax(float a, float b, float k)
    {
        return -SmoothMin(-a, -b, k);
    }

    static float SmoothMaxQuadratic(float a, float b, float k)
    {
        return -SmoothMinQuadratic(-a, -b, k);
    }

    /// <summary>Smooth subtraction: carve shape B out of shape A.</summary>
    static float SmoothSubtraction(float distCarve, float distBase, float k)
    {
        return SmoothMax(distBase, -distCarve, k);
    }
    static float SmoothSubtractionQuadratic(float distCarve, float distBase, float k)
    {
        return SmoothMaxQuadratic(distBase, -distCarve, k);
    }
}

### 4. RECONNAISSANCE UPDATES: CAVE ENTRANCES, VOXEL SHADING, & MAPMAGIC POST-PROCESSING

Below are the findings from the final reconnaissance regarding Terrain Holes, Voxel Shading, and MapMagic post-processing pipelines.

#### 4.1. TERRAIN HOLES (Cave Entrances)

**Logic:**
When voxel caves exit to the surface, the engine dynamically carves holes in the 2D heightmap. This is driven by [VegetationTerrainHoleSynchronizer.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs), which is a partial class of `HectonMapMagicVegetationBridge`.

1. **Registration:**
   Cave generation code calls:
   ```csharp
   int holeHandle = vegetationBridge.RegisterTerrainHoleHandle(runtimeSurfacePosition, radius);
   ```
   This registers a `TerrainHoleRecord` struct in the bridge's array `_terrainHoleRecords`.

2. **Synchronization Pass:**
   In a slow-tick synchronization loop (`BuildAndApplyTerrainHoleMaskSync`), the engine:
   - Maps each point of the terrain's hole mask grid `(x, y)` to world-space coordinates `(worldX, worldZ)`.
   - Iterates through active `TerrainHoleRecord` items.
   - If the point is within the squared radius of any cave entrance (`SourceType == TerrainHoleSourceType.CaveEntrance`), the surface value at that coordinate is set to `0` (void).
   - Writes the results to a boolean mask `state.TerrainHoleMaskManaged[y, x]`.
   - Applies the mask to the active Unity Terrain via:
     ```csharp
     state.TerrainData.SetHolesDelayLOD(0, 0, state.TerrainHoleMaskManaged);
     state.TerrainData.SyncTexture(TerrainData.HolesTextureName);
     ```

**Code Extract:**
```csharp
                int length = math.min(expectedLength, terrainHoleMask.Length);
                int holeCount = math.min(_terrainHoleCount, _terrainHoleRecords.Length);
                for (int sampleIndex = 0; sampleIndex < length; sampleIndex++)
                {
                    int y = sampleIndex / resolution;
                    int x = sampleIndex - (y * resolution);
                    float normalizedX = resolution <= 1 ? 0f : x / (float)(resolution - 1);
                    float normalizedZ = resolution <= 1 ? 0f : y / (float)(resolution - 1);
                    float worldX = state.TerrainPosition.x + (normalizedX * state.TerrainSize.x);
                    float worldZ = state.TerrainPosition.z + (normalizedZ * state.TerrainSize.z);
                    byte surface = 1;
                    for (int holeIndex = 0; holeIndex < holeCount; holeIndex++)
                    {
                        TerrainHoleRecord hole = _terrainHoleRecords[holeIndex];
                        if (hole.SourceType != TerrainHoleSourceType.CaveEntrance)
                            continue;

                        float dx = worldX - hole.X;
                        float dz = worldZ - hole.Z;
                        if ((dx * dx) + (dz * dz) <= hole.RadiusSq)
                        {
                            surface = 0;
                            break;
                        }
                    }

                    terrainHoleMask[sampleIndex] = surface;
                    state.TerrainHoleMaskManaged[y, x] = surface != 0;
                }
```

#### 4.2. VOXEL SHADING & MATERIALS

**Logic:**
1. **Material Assignment:**
   Voxel chunk meshes generated by Marching Cubes are rendered using a shared material assigned to `HectonVoxelEngine.voxelMaterial` via the inspector. No dynamic runtime material cloning or shader synthesis is performed.
   
2. **Shader and Texture Arrays:**
   The material uses the custom terrain shader `"Hecton8/URP/Terrain_TextureArray"` (source code in `HectonTerrain.shader`). It natively supports 2D Texture Arrays:
   - `_AlbedoArray` (Albedo Array, `2DArray`)
   - `_NormalArray` (Normal Array, `2DArray`)
   - `_MaskArray` (Mask Array, `2DArray`)
   
3. **Biplanar Projection Shading:**
   Since Marching Cubes mesh vertices do not have authored 2D UV maps, the shader performs dynamic biplanar projection in world-space (implemented in `HectonTerrainSampling.hlsl`).
   - It uploads world space coordinates in the `RuntimePositionWS` vertex attribute stream (in `UploadSurfaceMesh`).
   - The shader projects textures along the dominant normal axes using biplanar weights and blends fine/coarse octaves based on camera distance (`camDist`).

**Code Extract (Biplanar Sampling in HectonTerrainSampling.hlsl):**
```hlsl
            // Sample fine + coarse and blend by distance
            [branch] if (biW.y > 0)
            {
                float3 af = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_fine,   (float)k);
                float3 ac = SampleStochastic_Albedo(_AlbedoArray, sampler_LinearRepeat, uvXZ_coarse, (float)k);
                a_y = lerp(ac, af, fineFade);
                float3 nf = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvXZ_fine,   (float)k);
                float3 nc = SampleStochastic_Normal(_NormalArray, sampler_LinearRepeat, uvXZ_coarse, (float)k);
                n_y = lerp(nc, nf, fineFade);
                m_y = SampleStochastic_Mask(_MaskArray, sampler_LinearRepeat, uvXZ_fine, (float)k);
                a += a_y * biW.y;
                n += n_y * biW.y;
                m += m_y * biW.y;
            }
```

#### 4.3. MAPMAGIC GRAPH POST-PROCESSING

**Logic:**
The active terrain graph is [HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset](file:///c:/hades/Hecton8/Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset).

1. **Pipeline Topology:**
   - Base heights are generated by the custom node `HectonSandboxAbyssalShelfMapMagicNode`.
   - Chained directly after it is `HectonBiomeMatrixMapMagicPostProcessNode` (`refId: 565`), which contains thermal weathering (erosion) and tectonic ridge displacement.
   - The output of the post-process node then feeds into `HectonTerrainSplatmapMapMagicNode` for texture distribution, and ultimately to `HeightOutput200` to set the height values in the `TerrainData` object.

2. **Weathering / Smoothing Status (No distortion):**
   - **Crucial Audit Finding:** The post-processing node `HectonBiomeMatrixMapMagicPostProcessNode` is currently **disabled** (`enabled = false`) inside the graph serialization.
   - Other matrix modification nodes in the graph, such as `Erosion200` (`refId: 51`) and `Blur200` (`refId: 194`), are also **disabled** (`enabled = false`).
   - **Conclusion:** There are no active smoothing, blurring, or erosion nodes in the MapMagic pipeline that distort or "wash out" the precise geology heights generated by `WorldMacroGeologyFields.cs` before they are written to `TerrainData`. The 2D heightmap remains mathematically intact for the voxel stitcher.
