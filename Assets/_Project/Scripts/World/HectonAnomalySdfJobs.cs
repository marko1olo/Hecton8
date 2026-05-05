using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst kernel that forces SDF density to exactly match a terrain heightfield top surface.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SnapSDFToTerrainJob : IJobParallelFor
    {
        /// <summary>Terrain heights in meters.</summary>
        [ReadOnly] public NativeArray<float> TerrainHeights;

        /// <summary>Terrain sample width.</summary>
        public int TerrainWidth;

        /// <summary>Terrain sample depth.</summary>
        public int TerrainDepth;

        /// <summary>Terrain sample size in meters.</summary>
        public float TerrainCellSizeMeters;

        /// <summary>Absolute universe origin of the terrain heightmap.</summary>
        public double3 TerrainOriginAup;

        /// <summary>SDF density array. Positive means solid.</summary>
        public NativeArray<float> Sdf;

        /// <summary>SDF sample width.</summary>
        public int SdfWidth;

        /// <summary>SDF sample height.</summary>
        public int SdfHeight;

        /// <summary>SDF sample depth.</summary>
        public int SdfDepth;

        /// <summary>SDF voxel size in meters.</summary>
        public float VoxelSizeMeters;

        /// <summary>Absolute universe origin of the SDF volume.</summary>
        public double3 SdfOriginAup;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int slice = SdfWidth * SdfHeight;
            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / SdfWidth;
            int x = rem - y * SdfWidth;

            double absX = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absY = SdfOriginAup.y + y * (double)VoxelSizeMeters;
            double absZ = SdfOriginAup.z + z * (double)VoxelSizeMeters;
            float terrainHeight = SampleTerrainHeight(absX, absZ);
            Sdf[index] = terrainHeight - (float)absY;
        }

        private float SampleTerrainHeight(double absX, double absZ)
        {
            float tx = (float)((absX - TerrainOriginAup.x) / TerrainCellSizeMeters);
            float tz = (float)((absZ - TerrainOriginAup.z) / TerrainCellSizeMeters);
            tx = math.clamp(tx, 0f, TerrainWidth - 1f);
            tz = math.clamp(tz, 0f, TerrainDepth - 1f);

            int x0 = (int)math.floor(tx);
            int z0 = (int)math.floor(tz);
            int x1 = math.min(x0 + 1, TerrainWidth - 1);
            int z1 = math.min(z0 + 1, TerrainDepth - 1);
            float fx = tx - x0;
            float fz = tz - z0;

            float h00 = TerrainHeights[x0 + z0 * TerrainWidth];
            float h10 = TerrainHeights[x1 + z0 * TerrainWidth];
            float h01 = TerrainHeights[x0 + z1 * TerrainWidth];
            float h11 = TerrainHeights[x1 + z1 * TerrainWidth];
            float hx0 = math.lerp(h00, h10, fx);
            float hx1 = math.lerp(h01, h11, fx);
            return math.lerp(hx0, hx1, fz);
        }
    }

    /// <summary>
    /// Burst kernel that forces the nearest terrain-roof voxel in each XZ column to exact zero density.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SnapSDFTopCellsToTerrainJob : IJobParallelFor
    {
        /// <summary>Terrain heights in meters.</summary>
        [ReadOnly] public NativeArray<float> TerrainHeights;

        /// <summary>Terrain sample width.</summary>
        public int TerrainWidth;

        /// <summary>Terrain sample depth.</summary>
        public int TerrainDepth;

        /// <summary>Terrain sample size in meters.</summary>
        public float TerrainCellSizeMeters;

        /// <summary>Absolute universe origin of the terrain heightmap.</summary>
        public double3 TerrainOriginAup;

        /// <summary>SDF density array. Positive means solid.</summary>
        public NativeArray<float> Sdf;

        /// <summary>SDF sample width.</summary>
        public int SdfWidth;

        /// <summary>SDF sample height.</summary>
        public int SdfHeight;

        /// <summary>SDF sample depth.</summary>
        public int SdfDepth;

        /// <summary>SDF voxel size in meters.</summary>
        public float VoxelSizeMeters;

        /// <summary>Absolute universe origin of the SDF volume.</summary>
        public double3 SdfOriginAup;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int x = index % SdfWidth;
            int z = index / SdfWidth;
            if (z >= SdfDepth)
                return;

            double absX = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absZ = SdfOriginAup.z + z * (double)VoxelSizeMeters;
            float terrainHeight = SampleTerrainHeight(absX, absZ);
            int y = (int)math.round((terrainHeight - (float)SdfOriginAup.y) / VoxelSizeMeters);
            y = math.clamp(y, 0, SdfHeight - 1);
            Sdf[x + y * SdfWidth + z * SdfWidth * SdfHeight] = 0f;
        }

        private float SampleTerrainHeight(double absX, double absZ)
        {
            float tx = (float)((absX - TerrainOriginAup.x) / TerrainCellSizeMeters);
            float tz = (float)((absZ - TerrainOriginAup.z) / TerrainCellSizeMeters);
            tx = math.clamp(tx, 0f, TerrainWidth - 1f);
            tz = math.clamp(tz, 0f, TerrainDepth - 1f);

            int x0 = (int)math.floor(tx);
            int z0 = (int)math.floor(tz);
            int x1 = math.min(x0 + 1, TerrainWidth - 1);
            int z1 = math.min(z0 + 1, TerrainDepth - 1);
            float fx = tx - x0;
            float fz = tz - z0;

            float h00 = TerrainHeights[x0 + z0 * TerrainWidth];
            float h10 = TerrainHeights[x1 + z0 * TerrainWidth];
            float h01 = TerrainHeights[x0 + z1 * TerrainWidth];
            float h11 = TerrainHeights[x1 + z1 * TerrainWidth];
            float hx0 = math.lerp(h00, h10, fx);
            float hx1 = math.lerp(h01, h11, fx);
            return math.lerp(hx0, hx1, fz);
        }
    }

    /// <summary>
    /// Burst kernel that unions a 1 km chthonic pillar into an SDF density array.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic, CompileSynchronously = true)]
    public struct InjectMegaPillarSDFJob : IJobParallelFor
    {
        /// <summary>SDF density array. Positive means solid.</summary>
        public NativeArray<float> Sdf;

        /// <summary>SDF sample width.</summary>
        public int SdfWidth;

        /// <summary>SDF sample height.</summary>
        public int SdfHeight;

        /// <summary>SDF sample depth.</summary>
        public int SdfDepth;

        /// <summary>SDF voxel size in meters.</summary>
        public float VoxelSizeMeters;

        /// <summary>Absolute universe origin of the SDF volume.</summary>
        public double3 SdfOriginAup;

        /// <summary>Absolute universe base center of the pillar.</summary>
        public double3 PillarBaseAup;

        /// <summary>Base pillar radius in meters.</summary>
        public float RadiusMeters;

        /// <summary>Pillar height in meters.</summary>
        public float HeightMeters;

        /// <summary>Maximum radius warp in meters.</summary>
        public float EdgeWarpMeters;

        /// <summary>Noise frequency in reciprocal meters.</summary>
        public float NoiseFrequency;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int slice = SdfWidth * SdfHeight;
            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / SdfWidth;
            int x = rem - y * SdfWidth;

            double3 abs = new double3(
                SdfOriginAup.x + x * (double)VoxelSizeMeters,
                SdfOriginAup.y + y * (double)VoxelSizeMeters,
                SdfOriginAup.z + z * (double)VoxelSizeMeters);

            float3 local = new float3(
                (float)(abs.x - PillarBaseAup.x),
                (float)(abs.y - (PillarBaseAup.y + HeightMeters * 0.5f)),
                (float)(abs.z - PillarBaseAup.z));

            float3 noisePosition = new float3((float)abs.x, (float)abs.y * 0.35f, (float)abs.z) * NoiseFrequency;
            float warp = (AnomalySdfNoise.FractalNoise3D(noisePosition) * 2f - 1f) * EdgeWarpMeters;
            float warpedRadius = math.max(0.001f, RadiusMeters + warp);

            float radial = math.length(local.xz) - warpedRadius;
            float vertical = math.abs(local.y) - HeightMeters * 0.5f;
            float signedDistance = math.max(radial, vertical);
            Sdf[index] = math.max(Sdf[index], -signedDistance);
        }
    }

    /// <summary>
    /// Burst kernel that carves a sharp vertical fissure into an SDF density array.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic, CompileSynchronously = true)]
    public struct InjectDeepFissureSDFJob : IJobParallelFor
    {
        /// <summary>SDF density array. Positive means solid.</summary>
        public NativeArray<float> Sdf;

        /// <summary>Optional packed biome influence cells, indexed like the SDF array.</summary>
        public NativeArray<uint> BiomeInfluencePacked;

        /// <summary>SDF sample width.</summary>
        public int SdfWidth;

        /// <summary>SDF sample height.</summary>
        public int SdfHeight;

        /// <summary>SDF sample depth.</summary>
        public int SdfDepth;

        /// <summary>SDF voxel size in meters.</summary>
        public float VoxelSizeMeters;

        /// <summary>Absolute universe origin of the SDF volume.</summary>
        public double3 SdfOriginAup;

        /// <summary>Absolute universe top-center coordinate of the fissure.</summary>
        public double3 FissureTopAup;

        /// <summary>Normalized fissure line direction in XZ.</summary>
        public float2 DirectionXZ;

        /// <summary>Half length of the fissure line in meters.</summary>
        public float HalfLengthMeters;

        /// <summary>Fissure half width in meters.</summary>
        public float RadiusMeters;

        /// <summary>Vertical depth carved downward from <see cref="FissureTopAup"/>.</summary>
        public float DepthMeters;

        /// <summary>Packed biome influence id written for voxels inside the fissure.</summary>
        public uint FissureInfluencePacked;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int slice = SdfWidth * SdfHeight;
            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / SdfWidth;
            int x = rem - y * SdfWidth;

            double3 abs = new double3(
                SdfOriginAup.x + x * (double)VoxelSizeMeters,
                SdfOriginAup.y + y * (double)VoxelSizeMeters,
                SdfOriginAup.z + z * (double)VoxelSizeMeters);

            float2 direction = math.normalizesafe(DirectionXZ, new float2(1f, 0f));
            float2 delta = new float2(
                (float)(abs.x - FissureTopAup.x),
                (float)(abs.z - FissureTopAup.z));
            float along = math.clamp(math.dot(delta, direction), -HalfLengthMeters, HalfLengthMeters);
            float2 nearest = direction * along;
            float horizontalDistance = math.length(delta - nearest);
            float horizontalSignedDistance = horizontalDistance - RadiusMeters;
            float depthBelowTop = (float)(FissureTopAup.y - abs.y);
            float verticalSignedDistance = math.max(-depthBelowTop, depthBelowTop - DepthMeters);
            float signedDistance = math.max(horizontalSignedDistance, verticalSignedDistance);
            if (signedDistance >= 0f)
                return;

            float core01 = math.saturate(1f - horizontalDistance / math.max(0.001f, RadiusMeters));
            float depth01 = math.saturate(depthBelowTop / math.max(0.001f, DepthMeters));
            float negativeDensity = -math.max(math.abs(signedDistance), DepthMeters * core01 * depth01);
            Sdf[index] = math.min(Sdf[index], negativeDensity);

            if (BiomeInfluencePacked.IsCreated && BiomeInfluencePacked.Length > index)
                BiomeInfluencePacked[index] = FissureInfluencePacked;
        }
    }

    /// <summary>
    /// Burst kernel that applies lateral noise displacement to steep SDF slopes.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic, CompileSynchronously = true)]
    public struct VoxelCliffOverhangNoiseJob : IJobParallelFor
    {
        /// <summary>Input stitched SDF density array.</summary>
        [ReadOnly] public NativeArray<float> InputSdf;

        /// <summary>Output displaced SDF density array.</summary>
        [WriteOnly] public NativeArray<float> OutputSdf;

        /// <summary>SDF sample width.</summary>
        public int SdfWidth;

        /// <summary>SDF sample height.</summary>
        public int SdfHeight;

        /// <summary>SDF sample depth.</summary>
        public int SdfDepth;

        /// <summary>SDF voxel size in meters.</summary>
        public float VoxelSizeMeters;

        /// <summary>Minimum horizontal-to-vertical gradient ratio required for displacement.</summary>
        public float SlopeThreshold;

        /// <summary>Maximum lateral displacement in meters.</summary>
        public float LateralAmplitudeMeters;

        /// <summary>Noise frequency in reciprocal meters.</summary>
        public float NoiseFrequency;

        /// <summary>Blend strength from original SDF to displaced SDF.</summary>
        public float Strength;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int slice = SdfWidth * SdfHeight;
            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / SdfWidth;
            int x = rem - y * SdfWidth;

            float baseValue = InputSdf[index];
            if (x <= 0 || y <= 0 || z <= 0 || x >= SdfWidth - 1 || y >= SdfHeight - 1 || z >= SdfDepth - 1)
            {
                OutputSdf[index] = baseValue;
                return;
            }

            float gx = InputSdf[FlatIndex(x + 1, y, z)] - InputSdf[FlatIndex(x - 1, y, z)];
            float gy = InputSdf[FlatIndex(x, y + 1, z)] - InputSdf[FlatIndex(x, y - 1, z)];
            float gz = InputSdf[FlatIndex(x, y, z + 1)] - InputSdf[FlatIndex(x, y, z - 1)];
            float horizontal = math.sqrt(gx * gx + gz * gz);
            float slope = horizontal / (math.abs(gy) + 0.0001f);
            if (slope < SlopeThreshold || horizontal < 0.0001f)
            {
                OutputSdf[index] = baseValue;
                return;
            }

            float3 gridPos = new float3(x, y, z);
            float3 noisePos = gridPos * VoxelSizeMeters * NoiseFrequency;
            float noise = AnomalySdfNoise.FractalNoise3D(noisePos) * 2f - 1f;
            float2 lateralDir = math.normalize(new float2(gx, gz));
            float2 displacementCells = lateralDir * (noise * LateralAmplitudeMeters / VoxelSizeMeters);
            float displaced = SampleTrilinear(gridPos - new float3(displacementCells.x, 0f, displacementCells.y));
            OutputSdf[index] = math.lerp(baseValue, displaced, Strength);
        }

        private int FlatIndex(int x, int y, int z)
        {
            return x + y * SdfWidth + z * SdfWidth * SdfHeight;
        }

        private float SampleTrilinear(float3 p)
        {
            p.x = math.clamp(p.x, 0f, SdfWidth - 1f);
            p.y = math.clamp(p.y, 0f, SdfHeight - 1f);
            p.z = math.clamp(p.z, 0f, SdfDepth - 1f);

            int x0 = (int)math.floor(p.x);
            int y0 = (int)math.floor(p.y);
            int z0 = (int)math.floor(p.z);
            int x1 = math.min(x0 + 1, SdfWidth - 1);
            int y1 = math.min(y0 + 1, SdfHeight - 1);
            int z1 = math.min(z0 + 1, SdfDepth - 1);
            float fx = p.x - x0;
            float fy = p.y - y0;
            float fz = p.z - z0;

            float c000 = InputSdf[FlatIndex(x0, y0, z0)];
            float c100 = InputSdf[FlatIndex(x1, y0, z0)];
            float c010 = InputSdf[FlatIndex(x0, y1, z0)];
            float c110 = InputSdf[FlatIndex(x1, y1, z0)];
            float c001 = InputSdf[FlatIndex(x0, y0, z1)];
            float c101 = InputSdf[FlatIndex(x1, y0, z1)];
            float c011 = InputSdf[FlatIndex(x0, y1, z1)];
            float c111 = InputSdf[FlatIndex(x1, y1, z1)];

            float x00 = math.lerp(c000, c100, fx);
            float x10 = math.lerp(c010, c110, fx);
            float x01 = math.lerp(c001, c101, fx);
            float x11 = math.lerp(c011, c111, fx);
            float yInterp0 = math.lerp(x00, x10, fy);
            float yInterp1 = math.lerp(x01, x11, fy);
            return math.lerp(yInterp0, yInterp1, fz);
        }
    }

    internal static class AnomalySdfNoise
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FractalNoise3D(float3 p)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            for (int i = 0; i < 4; i++)
            {
                sum += ValueNoise3D(p * freq) * amp;
                freq *= 2.03f;
                amp *= 0.5f;
            }

            return math.saturate(sum * 1.0666667f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ValueNoise3D(float3 p)
        {
            int3 i = (int3)math.floor(p);
            float3 f = math.frac(p);
            f = f * f * (3f - 2f * f);

            float v000 = HashToUnit(i);
            float v100 = HashToUnit(i + new int3(1, 0, 0));
            float v010 = HashToUnit(i + new int3(0, 1, 0));
            float v110 = HashToUnit(i + new int3(1, 1, 0));
            float v001 = HashToUnit(i + new int3(0, 0, 1));
            float v101 = HashToUnit(i + new int3(1, 0, 1));
            float v011 = HashToUnit(i + new int3(0, 1, 1));
            float v111 = HashToUnit(i + new int3(1, 1, 1));

            float x00 = math.lerp(v000, v100, f.x);
            float x10 = math.lerp(v010, v110, f.x);
            float x01 = math.lerp(v001, v101, f.x);
            float x11 = math.lerp(v011, v111, f.x);
            float y0 = math.lerp(x00, x10, f.y);
            float y1 = math.lerp(x01, x11, f.y);
            return math.lerp(y0, y1, f.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToUnit(int3 p)
        {
            uint h = unchecked((uint)p.x) * 374761393u;
            h ^= unchecked((uint)p.y) * 668265263u;
            h ^= unchecked((uint)p.z) * 2147483647u;
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
