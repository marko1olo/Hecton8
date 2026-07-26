using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst kernel that evaluates continuous 3D cave SDFs and subtracts them from the terrain-owned density field.
    /// The terrain heightmap produces the base density first; this job is the sole owner of procedural cave carving.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ProceduralCaveSdfCarveJob : IJobParallelFor
    {
        private const float Tau = 6.2831853071795864769f;

        /// <summary>
        /// AUP wrap period in meters. Every noise term in this job is exactly periodic over this
        /// distance (frequency-quantized waves + wrapped integer noise lattices), so the carved
        /// cave field is seamless across all wrap-plane boundaries (R95 seam fix).
        /// </summary>
        public const double WrapPeriodMeters = 6627.0;

        /// <summary>SDF density array. Positive means solid rock, negative means air/water.</summary>
        [NoAlias] public NativeArray<float> Sdf;

        /// <summary>Width of the target 3D SDF volume in voxels.</summary>
        public int SdfWidth;
        /// <summary>Height of the target 3D SDF volume in voxels.</summary>
        public int SdfHeight;
        /// <summary>Depth of the target 3D SDF volume in voxels.</summary>
        public int SdfDepth;
        /// <summary>Size of each voxel cell in meters.</summary>
        public float VoxelSizeMeters;
        /// <summary>64-bit Absolute Universe Position (AUP) origin of the SDF volume chunk.</summary>
        public double3 SdfOriginAup;

        /// <summary>Base gyroid frequency in meters^-1. Good range: 0.008..0.020.</summary>
        public float PrimaryFrequency;

        /// <summary>Base cellular chamber frequency in meters^-1. Good range: 0.006..0.018.</summary>
        public float SecondaryFrequency;

        /// <summary>Maximum cave radius/strength in density meters.</summary>
        public float CarveStrengthMeters;

        /// <summary>Rarity threshold. Higher values narrow the gyroid pores and shrink cellular chambers.</summary>
        public float CaveThreshold;

        /// <summary>Maximum depth INTO solid rock (density) where caves can form. Beyond this, the voxel is deep core.</summary>
        public float MaxCrustDepthMeters;

        /// <summary>Minimum solid density required before carving is allowed. Protects the terrain surface.</summary>
        public float SurfaceProtectionMeters;

        /// <summary>Vertical period of geological strata shelving (meters). Creates flat cave floors.</summary>
        public float StrataLayerThicknessMeters;

        /// <summary>Y-domain strata compression strength. Interpreted as a fraction of layer thickness.</summary>
        public float StrataShelvingStrength;

        /// <summary>World-global seed. Must be the SAME for all chunks so the noise field is continuous.</summary>
        public uint WorldSeed;

        /// <summary>
        /// Optional 3D cave entrance mask for cliff breaching. MUST be sized SdfWidth*SdfHeight*SdfDepth
        /// (indexed by the flat 3D voxel index). Under-sized arrays are ignored entirely: a 2D W*D mask
        /// would silently cover only the low-index slab with a wrong 3D->2D mapping, so it is rejected.
        /// </summary>
        [NoAlias, ReadOnly] public NativeArray<float> CaveEntranceMask;
        /// <summary>Optional 3D brine pool depression mask for cave floors. Same full-3D sizing contract as CaveEntranceMask.</summary>
        [NoAlias, ReadOnly] public NativeArray<float> BrinePoolMask;
        /// <summary>Optional 3D steep cliff rock mask restricting cave mouths to vertical faces. Same full-3D sizing contract.</summary>
        [NoAlias, ReadOnly] public NativeArray<float> SteepRockMask;

        /// <summary>
        /// Burst execution entry point for per-voxel 3D cave density evaluation.
        /// </summary>
        /// <param name="index">Flat 3D index into the Sdf array.</param>
        public void Execute(int index)
        {
            float originalDensity = Sdf[index];
            if (!math.isfinite(originalDensity))
                return;

            if (!math.isfinite(CarveStrengthMeters) || CarveStrengthMeters <= 0.0001f)
                return;

            // Positive density is physical depth below the heightmap-owned terrain surface.
            float depthBelowSurface = originalDensity;
            if (depthBelowSurface <= SurfaceProtectionMeters)
                return;

            float safeMaxDepth = math.max(SurfaceProtectionMeters + 1.0f, MaxCrustDepthMeters);
            if (depthBelowSurface >= safeMaxDepth)
                return;

            int slice = SdfWidth * SdfHeight;
            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / SdfWidth;
            int x = rem - y * SdfWidth;

            double absX = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absY = SdfOriginAup.y + y * (double)VoxelSizeMeters;
            double absZ = SdfOriginAup.z + z * (double)VoxelSizeMeters;

            // Wrap coordinates into the stable noise range while preserving continuity inside the wrap period.
            // R95 SEAM FIX: the wrapped domain is only continuous ACROSS wrap-period boundaries if every
            // noise term evaluated on it is exactly WrapPeriodMeters-periodic. Non-periodic snoise plus a
            // floor-fmod sawtooth previously produced hard field discontinuities on the planes
            // X/Y/Z = k * 6627 m (including the Y = 0 sea-level plane) — visible straight cave-wall seams.
            // All noise below now runs on wrapped integer lattices / frequency-quantized waves, making the
            // whole cave field exactly periodic, hence C-continuous across every wrap boundary.
            float3 p = new float3(
                (float)Fmod(absX, WrapPeriodMeters),
                (float)Fmod(absY, WrapPeriodMeters),
                (float)Fmod(absZ, WrapPeriodMeters));

            float3 seedOffset = ResolveSeedOffset(WorldSeed);
            float caveSdf = EvaluateGyroidCellularCaveSdf(p, seedOffset);

            // Coupling Macro Cave Entrance & Brine Pool Masks into 3D SDF Density Field.
            // Masks are honored only when sized as full 3D voxel arrays (see field contract above).
            int requiredMaskLength = SdfWidth * SdfHeight * SdfDepth;
            if (CaveEntranceMask.IsCreated && CaveEntranceMask.Length >= requiredMaskLength)
            {
                float caveEntrance = CaveEntranceMask[index];
                float steepRock = SteepRockMask.IsCreated && SteepRockMask.Length >= requiredMaskLength ? SteepRockMask[index] : 0.5f;
                if (caveEntrance > 0.35f && steepRock > 0.4f)
                {
                    // Carve horizontal 3D cave entrances through cliff walls by biasing 3D SDF density negative
                    caveSdf -= caveEntrance * 14.0f;
                }
            }

            if (BrinePoolMask.IsCreated && BrinePoolMask.Length >= requiredMaskLength)
            {
                float brinePool = BrinePoolMask[index];
                if (brinePool > 0.5f)
                {
                    // Flatten cave floors and carve toxic brine basin depressions
                    caveSdf -= brinePool * 8.0f;
                }
            }

            if (!math.isfinite(caveSdf))
                return;

            // Smooth anti-spike shield: cave influence is exactly zero at the protected surface shell,
            // then rises with C1 smoothstep over the next 15 meters of rock.
            float surfaceFade = math.smoothstep(
                SurfaceProtectionMeters,
                SurfaceProtectionMeters + 15.0f,
                depthBelowSurface);

            float depthFraction = math.saturate(depthBelowSurface / safeMaxDepth);
            float depthFade = 1.0f - depthFraction * depthFraction;
            float caveInfluence = math.saturate(surfaceFade * depthFade);
            if (caveInfluence <= 0.0001f)
                return;

            // Fade cave amplitude by moving the cave SDF out of the subtraction band instead of clamping density.
            float safeCarveStrength = math.max(CarveStrengthMeters, VoxelSizeMeters);
            float effectiveCaveSdf = math.lerp(safeCarveStrength, caveSdf, caveInfluence);

            float booleanBlendRadius = math.max(math.max(VoxelSizeMeters * 2.5f, 2.0f), safeCarveStrength * 0.18f);
            if (effectiveCaveSdf >= booleanBlendRadius)
                return;

            // Standard SDF subtraction is smax(A, -B). The project density convention is inverted
            // (positive = solid), so the terrain and result are negated around the canonical operation.
            float terrainStandardSdf = -originalDensity;
            float carvedStandardSdf = Smax(terrainStandardSdf, -effectiveCaveSdf, booleanBlendRadius);
            float newDensity = -carvedStandardSdf;

            // Failsafe: procedural caves are only allowed to remove terrain-owned rock, never add it.
            Sdf[index] = math.min(newDensity, originalDensity);
        }

        /// <summary>
        /// Evaluates combined Gyroid + Cellular 3D cave SDF density at point p.
        /// High-frequency noise is smoothly gated by wall distance to prevent floating voxel islands.
        /// R95: every term is exactly WrapPeriodMeters-periodic. Waves use frequency quantized to an
        /// integer number of cycles per wrap period; lattice noise wraps its integer cells. The
        /// quantization shifts requested frequencies by at most 0.5 cycle over 6627 m (&lt;1%),
        /// visually identical while eliminating the wrap-plane seam entirely.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float EvaluateGyroidCellularCaveSdf(float3 p, float3 seedOffset)
        {
            float primaryFrequency = math.max(math.abs(PrimaryFrequency), 0.0005f);
            float secondaryFrequency = math.max(math.abs(SecondaryFrequency), 0.0005f);
            float safeCarveStrength = math.max(CarveStrengthMeters, math.max(VoxelSizeMeters, 1.0f));

            float warpFrequency = math.max(primaryFrequency * 0.47f, 0.0005f);
            int warpCells = QuantizeCellsPerPeriod(warpFrequency);
            warpFrequency = CellsToFrequency(warpCells);
            float warpAmplitude = math.clamp(safeCarveStrength * 0.35f, 2.0f, 22.0f);
            float3 warpedPos = ApplyDomainWarp(p, seedOffset, warpFrequency, warpCells, warpAmplitude, WorldSeed ^ 0x5A17D2E9u);

            // Strata shelving. Frequency quantized to whole cycles per wrap period (keeps Y periodicity),
            // and amplitude clamped BELOW the topology-inversion threshold: the Y remap
            // y' = y + A * sin(w * y) is monotonic only while A * w < 1, i.e. A < thickness / Tau
            // (~0.159 * thickness). The previous 0.45 * thickness cap allowed A * w up to 2.83 —
            // a folded (non-monotonic) domain producing mirrored duplicate cave bands (banned
            // triangle-wave/kaleidoscope artifact class). 0.14 keeps a safety margin (A * w <= 0.88).
            float strataThickness = math.max(4.0f, StrataLayerThicknessMeters);
            int strataCycles = math.max(1, (int)math.round(WrapPeriodMeters / strataThickness));
            float strataFrequency = (float)(strataCycles * (Tau / WrapPeriodMeters));
            // Clamp against the ACTUAL quantized wavelength (period / cycles), not the requested
            // thickness, so the monotonicity bound A * w < 1 holds even for extreme inputs.
            float strataWavelength = (float)(WrapPeriodMeters / strataCycles);
            float strataAmplitude = math.clamp(StrataShelvingStrength * strataThickness, 0.0f, strataWavelength * 0.14f);
            warpedPos.y += math.sin((warpedPos.y + seedOffset.y) * strataFrequency) * strataAmplitude;

            float rarity = math.saturate(CaveThreshold);

            // Gyroid frequency quantized so sin/cos complete whole cycles over the wrap period.
            int gyroidCycles = QuantizeCellsPerPeriod(primaryFrequency);
            float gyroidFrequency = CellsToFrequency(gyroidCycles);
            float3 gyroidPos = (warpedPos + seedOffset * 0.37f) * gyroidFrequency * Tau;
            float gyroid = math.sin(gyroidPos.x) * math.cos(gyroidPos.y) +
                           math.sin(gyroidPos.y) * math.cos(gyroidPos.z) +
                           math.sin(gyroidPos.z) * math.cos(gyroidPos.x);
            float gyroidBand = math.lerp(0.62f, 0.26f, rarity);
            float gyroidMetricScale = math.max(1.0f / (gyroidFrequency * Tau), VoxelSizeMeters);
            float gyroidSdf = (math.abs(gyroid) - gyroidBand) * gyroidMetricScale;

            float chamberFrequency = math.max(secondaryFrequency * 0.55f, 0.0005f);
            int chamberCells = QuantizeCellsPerPeriod(chamberFrequency);
            chamberFrequency = CellsToFrequency(chamberCells);
            float chamberDistance = CellularDistance(warpedPos + seedOffset * 1.91f, chamberFrequency, chamberCells, WorldSeed ^ 0xC0A55123u);
            int chamberNoiseCells = QuantizeCellsPerPeriod(chamberFrequency * 1.83f);
            float chamberNoise = PeriodicGradientNoise((warpedPos + seedOffset * 2.73f) * CellsToFrequency(chamberNoiseCells), chamberNoiseCells, WorldSeed ^ 0x7B2E44D1u);
            float chamberRadius = math.lerp(0.42f, 0.20f, rarity) + chamberNoise * 0.055f;
            chamberRadius = math.clamp(chamberRadius, 0.14f, 0.48f);
            float chamberSdf = (chamberDistance - chamberRadius) / chamberFrequency;

            float baseCaveSdf = math.min(gyroidSdf, chamberSdf);
            // Gate high-frequency micro-roughness so it only alters existing cave walls, never spawns disconnected islands in open air.
            float noiseGate = math.saturate(1.0f - math.abs(baseCaveSdf) / math.max(2.0f, safeCarveStrength * 0.25f));
            int reefCells = QuantizeCellsPerPeriod(primaryFrequency * 2.67f);
            float reefNoise = PeriodicGradientNoise((warpedPos + seedOffset * 4.11f) * CellsToFrequency(reefCells), reefCells, WorldSeed ^ 0x19C3A57Fu) * safeCarveStrength * 0.08f;
            return baseCaveSdf + reefNoise * noiseGate;
        }

        /// <summary>Quantizes a frequency (cycles per meter) to a whole number of cycles per wrap period.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int QuantizeCellsPerPeriod(float frequency)
        {
            return math.max(1, (int)math.round(frequency * (float)WrapPeriodMeters));
        }

        /// <summary>Converts whole cycles-per-period back to a frequency in cycles per meter.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CellsToFrequency(int cells)
        {
            return (float)(cells / WrapPeriodMeters);
        }

        /// <summary>
        /// Applies continuous, exactly wrap-periodic 3D domain warping to position p.
        /// The three decorrelated warp channels use periodic gradient-lattice noise, so warped
        /// positions remain continuous across wrap-plane boundaries.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplyDomainWarp(float3 p, float3 seedOffset, float frequency, int cellsPerPeriod, float amplitude, uint seed)
        {
            float3 q = (p + seedOffset) * frequency;
            float wx = PeriodicGradientNoise(q, cellsPerPeriod, seed ^ 0x11A3F2C5u);
            float wy = PeriodicGradientNoise(q, cellsPerPeriod, seed ^ 0x8D9B41E7u);
            float wz = PeriodicGradientNoise(q, cellsPerPeriod, seed ^ 0x3F60AB19u);
            return p + new float3(wx, wy, wz) * amplitude;
        }

        /// <summary>
        /// Evaluates 3D cellular/Worley distance for cavern chambers on a lattice that wraps every
        /// cellsPerPeriod cells, making the chamber field exactly periodic over the AUP wrap period.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CellularDistance(float3 p, float frequency, int cellsPerPeriod, uint seed)
        {
            float3 cellPos = p * frequency;
            int3 baseCell = new int3(
                (int)math.floor(cellPos.x),
                (int)math.floor(cellPos.y),
                (int)math.floor(cellPos.z));
            float3 frac = cellPos - new float3(baseCell.x, baseCell.y, baseCell.z);

            float nearestSq = 99999.0f;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int3 neighbor = baseCell + new int3(dx, dy, dz);
                        int3 wrapped = WrapCell3(neighbor, cellsPerPeriod);
                        float3 feature = Hash3ToUnitFloat3(wrapped, seed);
                        float3 diff = new float3(dx, dy, dz) + feature - frac;
                        nearestSq = math.min(nearestSq, math.lengthsq(diff));
                    }
                }
            }

            return math.sqrt(nearestSq);
        }

        /// <summary>Wraps integer lattice coordinates into [0, cellsPerPeriod) per axis (true modulo, negative-safe).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 WrapCell3(int3 cell, int cellsPerPeriod)
        {
            int period = math.max(1, cellsPerPeriod);
            int3 m = new int3(cell.x % period, cell.y % period, cell.z % period);
            return math.select(m, m + period, m < 0);
        }

        /// <summary>
        /// C2-smooth (quintic-fade) trilinear gradient-lattice noise whose integer lattice wraps every
        /// cellsPerPeriod cells. Output approximately in [-1, 1]. Deterministic, Burst-safe, and exactly
        /// periodic — the R95 replacement for non-periodic snoise in the wrapped cave domain.
        /// </summary>
        private static float PeriodicGradientNoise(float3 q, int cellsPerPeriod, uint seed)
        {
            float3 cellF = math.floor(q);
            int3 cell = new int3((int)cellF.x, (int)cellF.y, (int)cellF.z);
            float3 f = q - cellF;
            // Quintic fade: 6t^5 - 15t^4 + 10t^3 (C2 continuity at cell boundaries).
            float3 u = f * f * f * (f * (f * 6.0f - 15.0f) + 10.0f);

            float n000 = CornerGradientDot(cell, new int3(0, 0, 0), f, cellsPerPeriod, seed);
            float n100 = CornerGradientDot(cell, new int3(1, 0, 0), f, cellsPerPeriod, seed);
            float n010 = CornerGradientDot(cell, new int3(0, 1, 0), f, cellsPerPeriod, seed);
            float n110 = CornerGradientDot(cell, new int3(1, 1, 0), f, cellsPerPeriod, seed);
            float n001 = CornerGradientDot(cell, new int3(0, 0, 1), f, cellsPerPeriod, seed);
            float n101 = CornerGradientDot(cell, new int3(1, 0, 1), f, cellsPerPeriod, seed);
            float n011 = CornerGradientDot(cell, new int3(0, 1, 1), f, cellsPerPeriod, seed);
            float n111 = CornerGradientDot(cell, new int3(1, 1, 1), f, cellsPerPeriod, seed);

            float nx00 = math.lerp(n000, n100, u.x);
            float nx10 = math.lerp(n010, n110, u.x);
            float nx01 = math.lerp(n001, n101, u.x);
            float nx11 = math.lerp(n011, n111, u.x);
            float nxy0 = math.lerp(nx00, nx10, u.y);
            float nxy1 = math.lerp(nx01, nx11, u.y);
            // Edge-direction gradients have |g| = sqrt(2); 1.154 rescales the interpolated result to ~[-1, 1].
            return math.lerp(nxy0, nxy1, u.z) * 1.154f;
        }

        /// <summary>Dot of the wrapped-lattice corner gradient with the offset from that corner.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CornerGradientDot(int3 cell, int3 corner, float3 f, int cellsPerPeriod, uint seed)
        {
            int3 wrapped = WrapCell3(cell + corner, cellsPerPeriod);
            uint h = Hash(wrapped.x, wrapped.y, wrapped.z, seed);
            // 12 edge-direction gradients (improved-Perlin style): pick signs from bits, zero one axis.
            float3 g = new float3(
                (h & 1u) != 0u ? -1.0f : 1.0f,
                (h & 2u) != 0u ? -1.0f : 1.0f,
                (h & 4u) != 0u ? -1.0f : 1.0f);
            uint axis = (h >> 3) % 3u;
            g = math.select(g, new float3(0.0f, g.y, g.z), axis == 0u);
            g = math.select(g, new float3(g.x, 0.0f, g.z), axis == 1u);
            g = math.select(g, new float3(g.x, g.y, 0.0f), axis == 2u);
            float3 d = f - new float3(corner.x, corner.y, corner.z);
            return math.dot(g, d);
        }

        /// <summary>Polynomial smooth maximum. Canonical SDF subtraction uses smax(A, -B).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smax(float a, float b, float k)
        {
            float width = math.max(k, 0.0001f);
            float h = math.saturate(0.5f + 0.5f * (a - b) / width);
            return math.lerp(b, a, h) + width * h * (1.0f - h);
        }

        /// <summary>Resolves continuous seed offset vector for 3D Simplex domain.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveSeedOffset(uint seed)
        {
            float seedOffX = ((seed & 0xFFu) - 128f) * 0.5f;
            float seedOffY = (((seed >> 8) & 0xFFu) - 128f) * 0.5f;
            float seedOffZ = (((seed >> 16) & 0xFFu) - 128f) * 0.5f;
            return new float3(seedOffX, seedOffY, seedOffZ);
        }

        /// <summary>Hashes integer 3D cell coordinates into a deterministic float3 in [0, 1)^3.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 Hash3ToUnitFloat3(int3 cell, uint seed)
        {
            uint hx = Hash(cell.x, cell.y, cell.z, seed ^ 0x9E3779B9u);
            uint hy = Hash(cell.x, cell.y, cell.z, seed ^ 0xBB67AE85u);
            uint hz = Hash(cell.x, cell.y, cell.z, seed ^ 0x3C6EF372u);
            return new float3(HashToUnitFloat(hx), HashToUnitFloat(hy), HashToUnitFloat(hz));
        }

        /// <summary>Integer hash function for 3D coordinates and seed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(int x, int y, int z, uint seed)
        {
            unchecked
            {
                uint h = seed;
                h ^= (uint)x * 0x8DA6B343u;
                h ^= (uint)y * 0xD8163841u;
                h ^= (uint)z * 0xCB1AB31Fu;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Converts 32-bit hash value to float in [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToUnitFloat(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1.0f / 16777216.0f);
        }

        /// <summary>
        /// Deterministic fmod that always returns a positive value in [0, period).
        /// Standard C# % can return negative values for negative inputs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Fmod(double value, double period)
        {
            return value - math.floor(value / period) * period;
        }
    }
}
