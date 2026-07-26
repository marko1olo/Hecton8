using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst kernel that locks SDF density to a terrain heightfield, with hysteresis for micro-deltas.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SnapSDFToTerrainJob : Unity.Jobs.IJobParallelFor
    {
        private const double TerrainSampleTruncationScale = AUPDeterminism.AUP_DETERMINISM_MULTIPLIER;

        /// <summary>Terrain heights in meters.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> TerrainHeights;

        /// <summary>Terrain sample width.</summary>
        public int TerrainWidth;

        /// <summary>Terrain sample depth.</summary>
        public int TerrainDepth;

        /// <summary>Terrain sample size in meters.</summary>
        public float TerrainCellSizeMeters;

        /// <summary>Absolute universe origin of the terrain heightmap.</summary>
        public double3 TerrainOriginAup;

        /// <summary>SDF density array. Positive means solid.</summary>
        [NoAlias] public NativeArray<float> Sdf;

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

        /// <summary>Density delta below this meter threshold is left untouched to suppress precision micro-tears.</summary>
        public float SnapHysteresisMeters;

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
            float density = terrainHeight - (float)absY;
            if (SnapHysteresisMeters > 0f && math.abs(density - Sdf[index]) < SnapHysteresisMeters)
                return;

            Sdf[index] = density;
        }

        private float SampleTerrainHeight(double absX, double absZ)
        {
            absX = TruncateAupForTerrainSample(absX);
            absZ = TruncateAupForTerrainSample(absZ);
            float tx = (float)((absX - TerrainOriginAup.x) / TerrainCellSizeMeters);
            float tz = (float)((absZ - TerrainOriginAup.z) / TerrainCellSizeMeters);
            tx = math.clamp(tx, 0f, TerrainWidth - 1f);
            tz = math.clamp(tz, 0f, TerrainDepth - 1f);

            int x0 = (int)math.trunc(tx);
            int z0 = (int)math.trunc(tz);
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

        private static double TruncateAupForTerrainSample(double value)
        {
            return math.trunc(value * TerrainSampleTruncationScale) / TerrainSampleTruncationScale;
        }
    }

    /// <summary>
    /// Burst kernel that locks the nearest terrain-roof voxel in each XZ column, with hysteresis for micro-deltas.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SnapSDFTopCellsToTerrainJob : Unity.Jobs.IJobParallelFor
    {
        private const double TerrainSampleTruncationScale = AUPDeterminism.AUP_DETERMINISM_MULTIPLIER;

        /// <summary>Terrain heights in meters.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> TerrainHeights;

        /// <summary>Terrain sample width.</summary>
        public int TerrainWidth;

        /// <summary>Terrain sample depth.</summary>
        public int TerrainDepth;

        /// <summary>Terrain sample size in meters.</summary>
        public float TerrainCellSizeMeters;

        /// <summary>Absolute universe origin of the terrain heightmap.</summary>
        public double3 TerrainOriginAup;

        /// <summary>SDF density array. Positive means solid.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity cannot infer this column-lane mapping because the scheduled index is an XZ column, not the final
        // SDF element index. The mapping is still injective: one scheduled lane owns exactly one (x,z) column and
        // writes only the lower/upper terrain-crossing y cells inside that column. No other lane can produce the
        // same (x,z), so no other lane can write the same flat SDF index.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // The job is dependency-chained after the full SDF terrain snap and before pillar injection / Marching
        // Cubes. No concurrent job writes the same SDF array while this seam lock is scheduled. The dual version
        // applies the same column ownership rule to both arrays.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // A full SDF-index pass was rejected for the production path because it schedules SdfWidth*SdfHeight*SdfDepth
        // lanes to update two cells per column. This column pass schedules only SdfWidth*SdfDepth lanes while keeping
        // deterministic AUP sampling and bounded top-surface density writes.
        [NativeDisableParallelForRestriction, NoAlias]
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

        /// <summary>Density delta below this meter threshold is left untouched to suppress precision micro-tears.</summary>
        public float SnapHysteresisMeters;

        /// <summary>
        /// Applies the terrain seam lock for one XZ column lane.
        /// </summary>
        public void Execute(int index)
        {
            if (SdfWidth <= 0 || SdfHeight <= 0 || SdfDepth <= 0)
                return;

            int columnCount = SdfWidth * SdfDepth;
            if ((uint)index >= (uint)columnCount)
                return;

            int z = index / SdfWidth;
            int x = index - z * SdfWidth;
            double absX = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absZ = SdfOriginAup.z + z * (double)VoxelSizeMeters;
            float terrainHeight = SampleTerrainHeight(absX, absZ);
            float terrainY = (terrainHeight - (float)SdfOriginAup.y) / VoxelSizeMeters;
            int lowerY = (int)math.trunc(terrainY);
            int upperY = lowerY + 1;

            WriteTerrainDensityWithHysteresis(x, lowerY, z, terrainHeight);
            WriteTerrainDensityWithHysteresis(x, upperY, z, terrainHeight);
        }

        private void WriteTerrainDensityWithHysteresis(int x, int y, int z, float terrainHeight)
        {
            if (y < 0 || y >= SdfHeight)
                return;

            int index = x + y * SdfWidth + z * SdfWidth * SdfHeight;
            float absY = (float)(SdfOriginAup.y + y * (double)VoxelSizeMeters);
            float density = terrainHeight - absY;
            if (SnapHysteresisMeters > 0f && math.abs(density - Sdf[index]) < SnapHysteresisMeters)
                return;

            Sdf[index] = density;
        }

        private float SampleTerrainHeight(double absX, double absZ)
        {
            absX = TruncateAupForTerrainSample(absX);
            absZ = TruncateAupForTerrainSample(absZ);
            float tx = (float)((absX - TerrainOriginAup.x) / TerrainCellSizeMeters);
            float tz = (float)((absZ - TerrainOriginAup.z) / TerrainCellSizeMeters);
            tx = math.clamp(tx, 0f, TerrainWidth - 1f);
            tz = math.clamp(tz, 0f, TerrainDepth - 1f);

            int x0 = (int)math.trunc(tx);
            int z0 = (int)math.trunc(tz);
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

        private static double TruncateAupForTerrainSample(double value)
        {
            return math.trunc(value * TerrainSampleTruncationScale) / TerrainSampleTruncationScale;
        }
    }

    /// <summary>
    /// Burst kernel that writes the same terrain seam lock into primary and secondary SDF arrays, with micro-delta hysteresis.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SnapDualSDFTopCellsToTerrainJob : Unity.Jobs.IJobParallelFor
    {
        private const double TerrainSampleTruncationScale = AUPDeterminism.AUP_DETERMINISM_MULTIPLIER;
        /// <summary>Terrain heights in meters.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> TerrainHeights;

        /// <summary>Terrain sample width.</summary>
        public int TerrainWidth;

        /// <summary>Terrain sample depth.</summary>
        public int TerrainDepth;

        /// <summary>Terrain sample size in meters.</summary>
        public float TerrainCellSizeMeters;

        /// <summary>Absolute universe origin of the terrain heightmap.</summary>
        public double3 TerrainOriginAup;

        /// <summary>SDF density array. Positive means solid.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity cannot infer this column-lane mapping because the scheduled index is an XZ column, not the final
        // SDF element index. The mapping is still injective: one scheduled lane owns exactly one (x,z) column and
        // writes only the lower/upper terrain-crossing y cells inside that column. No other lane can produce the
        // same (x,z), so no other lane can write the same flat SDF index.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // The job is dependency-chained after the full SDF terrain snap and before pillar injection / Marching
        // Cubes. No concurrent job writes either target SDF array while this dual seam lock is scheduled.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // A full SDF-index pass was rejected for the production path because it schedules SdfWidth*SdfHeight*SdfDepth
        // lanes to update two cells per column. This column pass schedules only SdfWidth*SdfDepth lanes while keeping
        // deterministic AUP sampling and bounded top-surface density writes.
        [NativeDisableParallelForRestriction, NoAlias]
        public NativeArray<float> Sdf;

        /// <summary>Second SDF density array written only when an offline validation path explicitly requests it.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SecondarySdf mirrors the exact same XZ-column ownership used by Sdf when WriteSecondary is non-zero.
        // Unity cannot infer that the scheduled index is a column id rather than a flat SDF index, but each lane
        // owns one unique (x,z) column and writes only that column's bounded lower/upper terrain-crossing y cells.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Alternatives rejected: scheduling a second full-grid validation pass would add SdfWidth*SdfHeight*SdfDepth
        // lanes to validate two samples per column; duplicating the primary job externally would double terrain
        // sampling and break the single dependency fence used by the cold validation path.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is guarded by WriteSecondary. Production keeps it zero, so SecondarySdf is untouched. When
        // validation enables it, Sdf and SecondarySdf are distinct caller-owned arrays, and this job is the only
        // writer in the dependency chain before any consumer reads either field.
        [NativeDisableParallelForRestriction, NoAlias]
        public NativeArray<float> SecondarySdf;

        /// <summary>Non-zero only for cold validation jobs that intentionally mirror the seam lock.</summary>
        public byte WriteSecondary;

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

        /// <summary>Density delta below this meter threshold is left untouched to suppress precision micro-tears.</summary>
        public float SnapHysteresisMeters;

        /// <inheritdoc />
        public void Execute(int index)
        {
            if (SdfWidth <= 0 || SdfHeight <= 0 || SdfDepth <= 0)
                return;

            int columnCount = SdfWidth * SdfDepth;
            if ((uint)index >= (uint)columnCount)
                return;

            int z = index / SdfWidth;
            int x = index - z * SdfWidth;
            double absX = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absZ = SdfOriginAup.z + z * (double)VoxelSizeMeters;
            float terrainHeight = SampleTerrainHeight(absX, absZ);
            float terrainY = (terrainHeight - (float)SdfOriginAup.y) / VoxelSizeMeters;
            int lowerY = (int)math.trunc(terrainY);
            int upperY = lowerY + 1;

            WriteTerrainDensityWithHysteresis(x, lowerY, z, terrainHeight);
            WriteTerrainDensityWithHysteresis(x, upperY, z, terrainHeight);
        }

        private void WriteTerrainDensityWithHysteresis(int x, int y, int z, float terrainHeight)
        {
            if (y < 0 || y >= SdfHeight)
                return;

            int index = x + y * SdfWidth + z * SdfWidth * SdfHeight;
            float absY = (float)(SdfOriginAup.y + y * (double)VoxelSizeMeters);
            float density = terrainHeight - absY;
            if (ShouldSnap(Sdf[index], density))
                Sdf[index] = density;

            if (WriteSecondary == 0)
                return;

            if (SecondarySdf.IsCreated &&
                (uint)index < (uint)SecondarySdf.Length)
            {
                if (ShouldSnap(SecondarySdf[index], density))
                    SecondarySdf[index] = density;
            }
        }

        private bool ShouldSnap(float currentDensity, float targetDensity)
        {
            return SnapHysteresisMeters <= 0f ||
                   math.abs(targetDensity - currentDensity) >= SnapHysteresisMeters;
        }

        private float SampleTerrainHeight(double absX, double absZ)
        {
            absX = TruncateAupForTerrainSample(absX);
            absZ = TruncateAupForTerrainSample(absZ);
            float tx = (float)((absX - TerrainOriginAup.x) / TerrainCellSizeMeters);
            float tz = (float)((absZ - TerrainOriginAup.z) / TerrainCellSizeMeters);
            tx = math.clamp(tx, 0f, TerrainWidth - 1f);
            tz = math.clamp(tz, 0f, TerrainDepth - 1f);

            int x0 = (int)math.trunc(tx);
            int z0 = (int)math.trunc(tz);
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

        private static double TruncateAupForTerrainSample(double value)
        {
            return math.trunc(value * TerrainSampleTruncationScale) / TerrainSampleTruncationScale;
        }
    }

    /// <summary>
    /// Burst kernel that unions a 1 km chthonic pillar into an SDF density array.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct InjectMegaPillarSDFJob : Unity.Jobs.IJobParallelFor
    {
        /// <summary>SDF density array. Positive means solid.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's safety system cannot prove this remapped write because the scheduled lane index is not the
        // final NativeArray index. The mapping is still injective: lane -> (localX,y,localZ) inside one pillar
        // envelope -> (x,y,z) around PillarBaseAup -> flat SDF index. For one scalar pillar center, no two lanes
        // can produce the same (x,y,z), and invalid out-of-volume lanes return before touching Sdf.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Alternatives rejected: full-grid one-write-per-index parallel injection measured above the 0.5 ms
        // SDF-injection budget because every voxel lane was scheduled; single-thread bounded injection avoided
        // suppression but measured worse due to serial loop cost. Duplicating the whole SDF field for a merge pass
        // would add native memory bandwidth and another full-grid pass before Marching Cubes.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is established by HectonAnomalyEngine.ResolvePillarEnvelopeRadiusCells and the laneCount
        // schedule: exactly (diameter * SdfHeight * diameter) lanes are launched for a single pillar envelope.
        // ExecuteLane derives x/y/z only from that lane and the immutable pillar base; the only write is Sdf[sdfIndex]
        // for that unique coordinate, after bounds and radius-envelope rejection.
        [NativeDisableParallelForRestriction, NoAlias]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> Sdf;

        public int SdfWidth;
        public int SdfHeight;
        public int SdfDepth;
        public float VoxelSizeMeters;
        public double3 SdfOriginAup;
        public double3 PillarBaseAup;
        public float RadiusMeters;
        public float HeightMeters;
        public float EdgeWarpMeters;
        public float NoiseFrequency;

        /// <inheritdoc />
        public void Execute(int index)
        {
            ExecuteLane(index, PillarBaseAup);
        }

        private void ExecuteLane(int laneIndex, double3 pillarBaseAup)
        {
            float halfHeight = HeightMeters * 0.5f;
            float maxRadius = RadiusMeters + EdgeWarpMeters + VoxelSizeMeters;
            int radiusCells = (int)math.ceil(maxRadius / VoxelSizeMeters);
            int diameter = radiusCells * 2 + 1;
            int localZ = laneIndex / (diameter * SdfHeight);
            int rem = laneIndex - localZ * diameter * SdfHeight;
            int localYIndex = rem / diameter;
            int localX = rem - localYIndex * diameter;
            int centerX = (int)math.trunc((pillarBaseAup.x - SdfOriginAup.x) / VoxelSizeMeters);
            int centerZ = (int)math.trunc((pillarBaseAup.z - SdfOriginAup.z) / VoxelSizeMeters);
            int x = centerX + localX - radiusCells;
            int z = centerZ + localZ - radiusCells;
            if (x < 0 || x >= SdfWidth || z < 0 || z >= SdfDepth)
                return;

            double absXd = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absYd = SdfOriginAup.y + localYIndex * (double)VoxelSizeMeters;
            double absZd = SdfOriginAup.z + z * (double)VoxelSizeMeters;
            float3 local = new float3(
                (float)(absXd - pillarBaseAup.x),
                (float)(absYd - (pillarBaseAup.y + (double)halfHeight)),
                (float)(absZd - pillarBaseAup.z));

            float radialSq = math.lengthsq(local.xz);
            if (math.abs(local.y) > halfHeight + VoxelSizeMeters ||
                radialSq > maxRadius * maxRadius)
                return;

            float innerRadius = math.max(0f, RadiusMeters - EdgeWarpMeters - VoxelSizeMeters);
            float radial = -math.max(VoxelSizeMeters, EdgeWarpMeters);
            if (radialSq > innerRadius * innerRadius)
            {
                float3 noisePosition = new float3((float)absXd, (float)(absYd * 0.35d), (float)absZd) * NoiseFrequency;
                float warp = (AnomalySdfNoise.FastHashNoise3D(noisePosition) * 2f - 1f) * EdgeWarpMeters;
                float warpedRadius = math.max(0.001f, RadiusMeters + warp);
                radial = AnomalySdfNoise.FastMagnitude(radialSq) - warpedRadius;
            }

            float vertical = math.abs(local.y) - halfHeight;
            int sdfIndex = x + localYIndex * SdfWidth + z * SdfWidth * SdfHeight;
            Sdf[sdfIndex] = math.max(Sdf[sdfIndex], -math.max(radial, vertical));
        }
    }

    /// <summary>
    /// Burst kernel that unions the selected strongest pillar record into an SDF density array.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct InjectSelectedMegaPillarSDFJob : Unity.Jobs.IJobParallelFor
    {
        /// <summary>SDF density array. Positive means solid.</summary>
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // This job schedules only the selected pillar XZ envelope, not the full SDF volume. The scheduled lane
        // index maps to exactly one (localX,localZ) envelope column and then writes bounded Y samples inside that
        // column after bounds checks. For one immutable selected pillar record, two lanes cannot resolve to the
        // same (x,z) column or the same flat SDF sample.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // The write is intentionally not tied to the parallel-for lane index because the envelope is centered on
        // the selected AUP and one lane owns a contiguous vertical column. A full-grid pass would restore safety
        // inference but would reintroduce work for unrelated voxels before Marching Cubes.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The broadphase radius check executes before noise and before any SDF write. The visual warp is a
        // lateral XZ cinematic fake computed once per column; invalid lanes return without touching the NativeArray,
        // preserving single-writer behavior per SDF sample.
        [NativeDisableParallelForRestriction, NoAlias]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> Sdf;

        [ReadOnly, NoAlias] public NativeArray<AnomalyFeatureRecord> SelectedFeature;
        public int SdfWidth;
        public int SdfHeight;
        public int SdfDepth;
        public float VoxelSizeMeters;
        public double3 SdfOriginAup;
        public double3 ChunkMinAup;
        public double3 ChunkMaxAup;
        public float RadiusMeters;
        public float HeightMeters;
        public float EdgeWarpMeters;
        public float NoiseFrequency;

        /// <inheritdoc />
        public void Execute(int index)
        {
            if (!SelectedFeature.IsCreated || SelectedFeature.Length <= 0)
                return;

            AnomalyFeatureRecord record = SelectedFeature[0];
            if (record.Valid == 0 || record.Kind != (byte)AnomalyFeatureKind.ChthonicPillar)
                return;

            double3 pillarBaseAup = new double3(record.AupX, record.AupY, record.AupZ);
            if (!math.all(math.isfinite(pillarBaseAup)))
                return;

            float boundsRadius = RadiusMeters + EdgeWarpMeters + VoxelSizeMeters;
            if (!PillarAabbIntersectsChunk(pillarBaseAup, boundsRadius, HeightMeters, ChunkMinAup, ChunkMaxAup))
                return;

            ExecuteLane(index, pillarBaseAup);
        }

        private void ExecuteLane(int laneIndex, double3 pillarBaseAup)
        {
            float halfHeight = HeightMeters * 0.5f;
            float maxRadius = RadiusMeters + EdgeWarpMeters + VoxelSizeMeters;
            float maxRadiusSq = maxRadius * maxRadius;
            float innerRadius = math.max(0f, RadiusMeters - EdgeWarpMeters - VoxelSizeMeters);
            float innerRadiusSq = innerRadius * innerRadius;
            int radiusCells = (int)math.ceil(maxRadius / VoxelSizeMeters);
            int diameter = radiusCells * 2 + 1;
            int localZ = laneIndex / diameter;
            int localX = laneIndex - localZ * diameter;
            int centerX = (int)math.trunc((pillarBaseAup.x - SdfOriginAup.x) / VoxelSizeMeters);
            int centerZ = (int)math.trunc((pillarBaseAup.z - SdfOriginAup.z) / VoxelSizeMeters);
            float centerY = (float)(pillarBaseAup.y + halfHeight);
            int x = centerX + localX - radiusCells;
            int z = centerZ + localZ - radiusCells;

            if (x < 0 || x >= SdfWidth || z < 0 || z >= SdfDepth)
                return;

            double absXd = SdfOriginAup.x + x * (double)VoxelSizeMeters;
            double absZd = SdfOriginAup.z + z * (double)VoxelSizeMeters;

            float dx = (float)(absXd - pillarBaseAup.x);
            float dz = (float)(absZd - pillarBaseAup.z);
            float radialSq = dx * dx + dz * dz;
            if (radialSq > maxRadiusSq)
                return;

            float radialDistance = 0f;
            float baseWarp = 0f;
            float angle = 0f;
            bool isBoundary = radialSq > innerRadiusSq;
            
            if (isBoundary)
            {
                radialDistance = AnomalySdfNoise.FastMagnitude(radialSq);
                float3 noisePosition = new float3((float)absXd, 0f, (float)absZd) * NoiseFrequency;
                baseWarp = (AnomalySdfNoise.FastHashNoise3D(noisePosition) * 2f - 1f) * (EdgeWarpMeters * 0.45f);
                angle = math.atan2(dx, dz);
            }

            int zOffset = z * SdfWidth * SdfHeight;
            for (int sampleY = 0; sampleY < SdfHeight; sampleY++)
            {
                float absY = (float)(SdfOriginAup.y + sampleY * (double)VoxelSizeMeters);
                float localY = absY - centerY;
                float vertical = math.abs(localY) - halfHeight;
                if (vertical > VoxelSizeMeters)
                    continue;

                float radial = -math.max(VoxelSizeMeters, EdgeWarpMeters);
                if (isBoundary)
                {
                    // Vertical Fluting: radial grooving around the pillar
                    float flute = math.sin(angle * 14f + localY * 0.015f) * (EdgeWarpMeters * 0.25f);
                    
                    // Tectonic Terracing: creates climbable horizontal ledges as you descend
                    float terraceStep = 24f;
                    float terraceFract = localY - math.floor(localY / terraceStep) * terraceStep;
                    float terraceWarp = math.smoothstep(0f, 8f, terraceFract) * (EdgeWarpMeters * 0.15f);

                    // Stalactite / Overhang Noise: high frequency organic roughness
                    float3 overhangPos = new float3((float)absXd, absY, (float)absZd) * (NoiseFrequency * 2.8f);
                    float overhang = (AnomalySdfNoise.FastHashNoise3D(overhangPos) * 2f - 1f) * (EdgeWarpMeters * 0.15f);

                    float warpedRadius = math.max(0.001f, RadiusMeters + baseWarp + flute + terraceWarp + overhang);
                    radial = radialDistance - warpedRadius;
                }

                int sdfIndex = x + sampleY * SdfWidth + zOffset;
                Sdf[sdfIndex] = math.max(Sdf[sdfIndex], -math.max(radial, vertical));
            }
        }

        private static bool PillarAabbIntersectsChunk(
            double3 pillarBaseAup,
            float radiusMeters,
            float heightMeters,
            double3 chunkMinAup,
            double3 chunkMaxAup)
        {
            if (!math.all(math.isfinite(pillarBaseAup)) ||
                !math.all(math.isfinite(chunkMinAup)) ||
                !math.all(math.isfinite(chunkMaxAup)))
            {
                return false;
            }

            double radius = math.max(0.001d, (double)radiusMeters);
            double height = math.max(0.001d, (double)heightMeters);
            double3 pillarMin = new double3(
                pillarBaseAup.x - radius,
                pillarBaseAup.y,
                pillarBaseAup.z - radius);
            double3 pillarMax = new double3(
                pillarBaseAup.x + radius,
                pillarBaseAup.y + height,
                pillarBaseAup.z + radius);

            return !(chunkMinAup.x > pillarMax.x ||
                     chunkMaxAup.x < pillarMin.x ||
                     chunkMinAup.y > pillarMax.y ||
                     chunkMaxAup.y < pillarMin.y ||
                     chunkMinAup.z > pillarMax.z ||
                     chunkMaxAup.z < pillarMin.z);
        }
    }

    /// <summary>
    /// Burst kernel that carves a sharp vertical fissure into an SDF density array.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct InjectDeepFissureSDFJob : Unity.Jobs.IJobParallelFor
    {
        /// <summary>SDF density array. Positive means solid.</summary>
        [NoAlias] public NativeArray<float> Sdf;

        /// <summary>Optional packed biome influence cells, indexed like the SDF array.</summary>
        [NoAlias] public NativeArray<uint> BiomeInfluencePacked;

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

            float2 direction = DirectionXZ;
            float2 delta = new float2(
                (float)(abs.x - FissureTopAup.x),
                (float)(abs.z - FissureTopAup.z));
            float along = math.clamp(math.dot(delta, direction), -HalfLengthMeters, HalfLengthMeters);
            float2 nearest = direction * along;
            float horizontalDistance = AnomalySdfNoise.FastMagnitude(math.lengthsq(delta - nearest));
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
    /// Burst kernel that carves vertical canyons into the SDF density array based on a 2D mask.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CarveFissureMaskSDFJob : Unity.Jobs.IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> Sdf;

        [ReadOnly, NoAlias] public NativeArray<byte> FissureMask;
        [ReadOnly, NoAlias] public NativeArray<float> TerrainHeights;

        public int SdfWidth;
        public int SdfHeight;
        public int SdfDepth;
        public float VoxelSizeMeters;
        public double3 SdfOriginAup;
        public float DepthMeters;

        public void Execute(int index)
        {
            if (FissureMask[index] == 0)
                return;

            int z = index / SdfWidth;
            int x = index - z * SdfWidth;
            
            float terrainY = TerrainHeights[index];
            float fissureTopY = terrainY + 15f; // Start carving slightly above the terrain

            int slice = SdfWidth * SdfHeight;
            int zOffset = z * slice;
            
            for (int y = 0; y < SdfHeight; y++)
            {
                float absY = (float)(SdfOriginAup.y + y * (double)VoxelSizeMeters);
                float depthBelowTop = fissureTopY - absY;
                if (depthBelowTop < 0f || depthBelowTop > DepthMeters) continue;

                int sdfIndex = x + y * SdfWidth + zOffset;
                float verticalSignedDistance = math.max(-depthBelowTop, depthBelowTop - DepthMeters);
                float depth01 = math.saturate(depthBelowTop / math.max(0.001f, DepthMeters));
                float negativeDensity = -math.max(math.abs(verticalSignedDistance), DepthMeters * depth01);
                Sdf[sdfIndex] = math.min(Sdf[sdfIndex], negativeDensity);
            }
        }
    }

    /// <summary>
    /// Burst kernel that applies lateral noise displacement to steep SDF slopes.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VoxelCliffOverhangNoiseJob : Unity.Jobs.IJobParallelFor
    {
        /// <summary>Input stitched SDF density array.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> InputSdf;

        /// <summary>Output displaced SDF density array.</summary>
        [WriteOnly, NoAlias] public NativeArray<float> OutputSdf;

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

        /// <summary>SDF chunk origin in Absolute Universal Position (AUP).</summary>
        public double3 OriginAup;

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

            float visibleSurfaceEnvelope = math.max(VoxelSizeMeters * 2f, math.abs(LateralAmplitudeMeters) + VoxelSizeMeters);
            if (math.abs(baseValue) > visibleSurfaceEnvelope)
            {
                OutputSdf[index] = baseValue;
                return;
            }

            float gx = InputSdf[FlatIndex(x + 1, y, z)] - InputSdf[FlatIndex(x - 1, y, z)];
            float gy = InputSdf[FlatIndex(x, y + 1, z)] - InputSdf[FlatIndex(x, y - 1, z)];
            float gz = InputSdf[FlatIndex(x, y, z + 1)] - InputSdf[FlatIndex(x, y, z - 1)];
            float horizontal = AnomalySdfNoise.FastMagnitude(gx * gx + gz * gz);
            float slope = horizontal / (math.abs(gy) + 0.0001f);
            if (slope < SlopeThreshold || horizontal < 0.0001f)
            {
                OutputSdf[index] = baseValue;
                return;
            }

            float3 gridPos = new float3(x, y, z);
            double3 worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);
            float3 noisePos = (float3)worldPosAup * NoiseFrequency;
            float noise = AnomalySdfNoise.FractalNoise3D(noisePos) * 2f - 1f;
            float lateralSq = gx * gx + gz * gz;
            float invLateral = math.rsqrt(math.max(lateralSq, 0.0000001f));
            float2 displacementCells = new float2(gx, gz) * (invLateral * noise * LateralAmplitudeMeters / VoxelSizeMeters);
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
        public static float FastHashNoise3D(float3 p)
        {
            return HashToUnit((int3)math.floor(p));
        }

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
        public static float FastMagnitude(float magnitudeSq)
        {
            float x = math.max(0f, magnitudeSq);
            float safe = math.max(x, 0.000000000001f);
            int estimateBits = (math.asint(safe) >> 1) + 0x1FBD1DF5;
            float estimate = math.asfloat(estimateBits);
            return math.select(0f, 0.5f * (estimate + safe / math.max(estimate, 0.000000000001f)), x > 0f);
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
