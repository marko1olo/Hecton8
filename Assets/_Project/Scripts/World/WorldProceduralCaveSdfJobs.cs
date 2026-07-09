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

        /// <summary>SDF density array. Positive means solid rock, negative means air/water.</summary>
        [NoAlias] public NativeArray<float> Sdf;

        public int SdfWidth;
        public int SdfHeight;
        public int SdfDepth;
        public float VoxelSizeMeters;
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

            // Wrap coordinates into the stable simplex/cellular range while preserving continuity inside the wrap period.
            const double wrapPeriod = 6627.0;
            float3 p = new float3(
                (float)Fmod(absX, wrapPeriod),
                (float)Fmod(absY, wrapPeriod),
                (float)Fmod(absZ, wrapPeriod));

            float3 seedOffset = ResolveSeedOffset(WorldSeed);
            float caveSdf = EvaluateGyroidCellularCaveSdf(p, seedOffset);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float EvaluateGyroidCellularCaveSdf(float3 p, float3 seedOffset)
        {
            float primaryFrequency = math.max(math.abs(PrimaryFrequency), 0.0005f);
            float secondaryFrequency = math.max(math.abs(SecondaryFrequency), 0.0005f);
            float safeCarveStrength = math.max(CarveStrengthMeters, math.max(VoxelSizeMeters, 1.0f));

            float warpFrequency = math.max(primaryFrequency * 0.47f, 0.0005f);
            float warpAmplitude = math.clamp(safeCarveStrength * 0.35f, 2.0f, 22.0f);
            float3 warpedPos = ApplyDomainWarp(p, seedOffset, warpFrequency, warpAmplitude);

            float strataThickness = math.max(4.0f, StrataLayerThicknessMeters);
            float strataFrequency = Tau / strataThickness;
            float strataAmplitude = math.clamp(StrataShelvingStrength * strataThickness, 0.0f, strataThickness * 0.45f);
            warpedPos.y += math.sin((warpedPos.y + seedOffset.y) * strataFrequency) * strataAmplitude;

            float rarity = math.saturate(CaveThreshold);

            float3 gyroidPos = (warpedPos + seedOffset * 0.37f) * primaryFrequency * Tau;
            float gyroid = math.sin(gyroidPos.x) * math.cos(gyroidPos.y) +
                           math.sin(gyroidPos.y) * math.cos(gyroidPos.z) +
                           math.sin(gyroidPos.z) * math.cos(gyroidPos.x);
            float gyroidBand = math.lerp(0.62f, 0.26f, rarity);
            float gyroidMetricScale = math.max(1.0f / (primaryFrequency * Tau), VoxelSizeMeters);
            float gyroidSdf = (math.abs(gyroid) - gyroidBand) * gyroidMetricScale;

            float chamberFrequency = math.max(secondaryFrequency * 0.55f, 0.0005f);
            float chamberDistance = CellularDistance(warpedPos + seedOffset * 1.91f, chamberFrequency, WorldSeed ^ 0xC0A55123u);
            float chamberNoise = noise.snoise((warpedPos + seedOffset * 2.73f) * chamberFrequency * 1.83f);
            float chamberRadius = math.lerp(0.42f, 0.20f, rarity) + chamberNoise * 0.055f;
            chamberRadius = math.clamp(chamberRadius, 0.14f, 0.48f);
            float chamberSdf = (chamberDistance - chamberRadius) / chamberFrequency;

            float reefNoise = noise.snoise((warpedPos + seedOffset * 4.11f) * primaryFrequency * 2.67f) * safeCarveStrength * 0.08f;
            return math.min(gyroidSdf, chamberSdf) + reefNoise;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplyDomainWarp(float3 p, float3 seedOffset, float frequency, float amplitude)
        {
            float3 q = (p + seedOffset) * frequency;
            float wx = noise.snoise(q + new float3(17.31f, 41.17f, -11.73f));
            float wy = noise.snoise(q + new float3(-29.19f, 7.83f, 53.41f));
            float wz = noise.snoise(q + new float3(61.07f, -23.59f, 5.29f));
            return p + new float3(wx, wy, wz) * amplitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CellularDistance(float3 p, float frequency, uint seed)
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
                        float3 feature = Hash3ToUnitFloat3(neighbor, seed);
                        float3 diff = new float3(dx, dy, dz) + feature - frac;
                        nearestSq = math.min(nearestSq, math.lengthsq(diff));
                    }
                }
            }

            return math.sqrt(nearestSq);
        }

        /// <summary>Polynomial smooth maximum. Canonical SDF subtraction uses smax(A, -B).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smax(float a, float b, float k)
        {
            float width = math.max(k, 0.0001f);
            float h = math.saturate(0.5f + 0.5f * (a - b) / width);
            return math.lerp(b, a, h) + width * h * (1.0f - h);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveSeedOffset(uint seed)
        {
            float seedOffX = ((seed & 0xFFu) - 128f) * 0.5f;
            float seedOffY = (((seed >> 8) & 0xFFu) - 128f) * 0.5f;
            float seedOffZ = (((seed >> 16) & 0xFFu) - 128f) * 0.5f;
            return new float3(seedOffX, seedOffY, seedOffZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 Hash3ToUnitFloat3(int3 cell, uint seed)
        {
            uint hx = Hash(cell.x, cell.y, cell.z, seed ^ 0x9E3779B9u);
            uint hy = Hash(cell.x, cell.y, cell.z, seed ^ 0xBB67AE85u);
            uint hz = Hash(cell.x, cell.y, cell.z, seed ^ 0x3C6EF372u);
            return new float3(HashToUnitFloat(hx), HashToUnitFloat(hy), HashToUnitFloat(hz));
        }

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
