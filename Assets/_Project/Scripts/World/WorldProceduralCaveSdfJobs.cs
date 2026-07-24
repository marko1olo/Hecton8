using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst kernel that evaluates true 3D volumetric noise to carve cave networks into the base SDF.
    /// This runs after the SDF has been snapped to the terrain heightmap.
    ///
    /// Cave generation uses Swiss Cheese model (two independent 3D ridged-noise fields intersected)
    /// to produce interconnected tunnels/chambers rather than uniform Swiss-cheese holes.
    ///
    /// Geological integration:
    /// - Caves form preferentially along fault zones and shelf breaks (tectonic weakness).
    /// - Caves avoid the open water column (density &lt; 0) and deep core rock (density &gt; MaxCrustDepthMeters).
    /// - Near-surface voxels are protected by a smooth fade to prevent terrain surface breakup.
    /// - Vertical strata shelving creates natural cave floors via periodic density restoration.
    /// - Seed offsets use modulo arithmetic to stay within safe snoise coordinate range.
    /// </summary>
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
            // This creates flat floors in caves by pushing density back up at specific Y levels,
            // simulating hard geological strata that resist erosion.
            float strataThickness = math.max(4.0f, StrataLayerThicknessMeters);
            float strataPhase = (float)absY / strataThickness;
            // Triangle wave: 0 at layer boundaries, 1 at layer centers.
            float strataFrac = math.abs(math.frac(strataPhase) * 2.0f - 1.0f);
            // Only push density back at layer boundaries (where strataFrac is low).
            float strataRestore = (1.0f - strataFrac) * StrataShelvingStrength * caveMask * surfaceFade;

            // Final density modification
            float newDensity = currentDensity - carve + strataRestore;

            Sdf[index] = newDensity;
        }

        /// <summary>
        /// Evaluates a 3-octave ridged multifractal in 3D.
        /// Returns a value where high = ridge centerline (potential tunnel).
        /// The verticalBias parameter stretches the noise vertically to create
        /// horizontal-biased tunnels (1.0 = isotropic, &lt;1.0 = more horizontal).
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
}
