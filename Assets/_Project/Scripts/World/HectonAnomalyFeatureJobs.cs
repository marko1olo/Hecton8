using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Anomaly feature record kind.
    /// </summary>
    public enum AnomalyFeatureKind : byte
    {
        /// <summary>No anomaly feature.</summary>
        None = 0,

        /// <summary>Local ridge-intersection maximum that can anchor a chthonic pillar.</summary>
        ChthonicPillar = 1,

        /// <summary>Local narrow low point that can anchor a deep fissure.</summary>
        DeepFissure = 2
    }

    /// <summary>
    /// Configuration for ridge-derived pillar and fissure detection.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AnomalyRidgeDetectionSettings
    {
        /// <summary>Heightmap width in samples.</summary>
        public int Width;

        /// <summary>Heightmap height in samples.</summary>
        public int Height;

        /// <summary>Heightmap cell size in meters.</summary>
        public float CellSizeMeters;

        /// <summary>Absolute-universe origin for the heightmap sample 0,0.</summary>
        public double3 OriginAup;

        /// <summary>Minimum crossed-ridge prominence required for a pillar candidate.</summary>
        public float MinimumPillarProminenceMeters;

        /// <summary>Minimum number of descending ridge arms required for a pillar junction.</summary>
        public int MinimumPillarRidgeArms;

        /// <summary>Minimum local trough depth required for a fissure candidate.</summary>
        public float MinimumFissureDepthMeters;

        /// <summary>Height comparison epsilon in meters.</summary>
        public float EqualHeightEpsilon;

        /// <summary>Pre-packed biome influence cell written for fissure candidates.</summary>
        public uint FissureInfluencePacked;

        /// <summary>One when pillar candidates must sit on sandbox Voronoi tectonic boundaries.</summary>
        public byte RequireTectonicBoundary;

        /// <summary>Sandbox Voronoi tectonic frequency in reciprocal meters.</summary>
        public float TectonicBoundaryFrequency;

        /// <summary>Sandbox Voronoi tectonic seed.</summary>
        public uint TectonicBoundarySeed;

        /// <summary>Minimum sandbox Voronoi boundary mask required for pillar candidates.</summary>
        public float MinimumTectonicBoundaryMask;

        /// <summary>Returns a bounded copy of the settings.</summary>
        public AnomalyRidgeDetectionSettings Sanitized()
        {
            return new AnomalyRidgeDetectionSettings
            {
                Width = math.max(1, Width),
                Height = math.max(1, Height),
                CellSizeMeters = ResolvePositiveFinite(CellSizeMeters, 0.001f),
                OriginAup = math.all(math.isfinite(OriginAup)) ? OriginAup : double3.zero,
                MinimumPillarProminenceMeters = ResolveNonNegativeFinite(MinimumPillarProminenceMeters, 0f),
                MinimumPillarRidgeArms = math.clamp(MinimumPillarRidgeArms <= 0 ? 3 : MinimumPillarRidgeArms, 3, 8),
                MinimumFissureDepthMeters = ResolveNonNegativeFinite(MinimumFissureDepthMeters, 0f),
                EqualHeightEpsilon = ResolvePositiveFinite(EqualHeightEpsilon, 0.000001f),
                FissureInfluencePacked = FissureInfluencePacked,
                RequireTectonicBoundary = RequireTectonicBoundary != 0 ? (byte)1 : (byte)0,
                TectonicBoundaryFrequency = ResolvePositiveFinite(TectonicBoundaryFrequency, 0.0001f),
                TectonicBoundarySeed = TectonicBoundarySeed,
                MinimumTectonicBoundaryMask = ResolveUnitIntervalOrDefault(MinimumTectonicBoundaryMask, 0.55f)
            };
        }

        private static float ResolvePositiveFinite(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static float ResolveNonNegativeFinite(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        private static float ResolveUnitIntervalOrDefault(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? math.clamp(value, 0f, 1f) : fallback;
        }
    }

    /// <summary>
    /// Spawn-ready pillar or fissure anomaly feature.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AnomalyFeatureRecord
    {
        /// <summary>One when the record is valid.</summary>
        public byte Valid;

        /// <summary>Feature kind as <see cref="AnomalyFeatureKind"/>.</summary>
        public byte Kind;

        /// <summary>Flat heightmap index.</summary>
        public int Index;

        /// <summary>Heightmap X sample.</summary>
        public int X;

        /// <summary>Heightmap Z sample.</summary>
        public int Z;

        /// <summary>Absolute-universe X coordinate in meters.</summary>
        public double AupX;

        /// <summary>Absolute-universe Y coordinate in meters.</summary>
        public double AupY;

        /// <summary>Absolute-universe Z coordinate in meters.</summary>
        public double AupZ;

        /// <summary>Source height in meters.</summary>
        public float HeightMeters;

        /// <summary>Normalized feature strength.</summary>
        public float Strength01;

        /// <summary>Packed biome influence id for fog/audio consumers.</summary>
        public uint BiomeInfluencePacked;
    }

    /// <summary>
    /// Burst kernel that detects ridge-local maxima for pillars and narrow low troughs for fissures.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic)]
    public struct AnomalyRidgeFeatureDetectionJob : IJobParallelFor
    {
        /// <summary>Input heightmap in meters.</summary>
        [ReadOnly] public NativeArray<float> Heightmap;

        /// <summary>Output feature records indexed by heightmap cell.</summary>
        [WriteOnly] public NativeArray<AnomalyFeatureRecord> FeatureRecords;

        /// <summary>Output fissure mask. One means fissure candidate.</summary>
        [WriteOnly] public NativeArray<byte> FissureMask;

        /// <summary>Detection settings.</summary>
        public AnomalyRidgeDetectionSettings Settings;

        /// <inheritdoc />
        public void Execute(int index)
        {
            FeatureRecords[index] = default;
            FissureMask[index] = 0;

            int width = Settings.Width;
            int height = Settings.Height;
            int x = index % width;
            int z = index / width;
            if (x <= 0 || z <= 0 || x >= width - 1 || z >= height - 1)
                return;

            float center = Heightmap[index];
            float north = Heightmap[index + width];
            float south = Heightmap[index - width];
            float east = Heightmap[index + 1];
            float west = Heightmap[index - 1];
            float northEast = Heightmap[index + width + 1];
            float northWest = Heightmap[index + width - 1];
            float southEast = Heightmap[index - width + 1];
            float southWest = Heightmap[index - width - 1];

            if (!AllFinite(center, north, south, east, west, northEast, northWest, southEast, southWest))
                return;

            float epsilon = Settings.EqualHeightEpsilon;
            float pillarProminence = ComputePillarProminence(
                center,
                north,
                south,
                east,
                west,
                northEast,
                northWest,
                southEast,
                southWest);

            bool localMaximum =
                center >= north - epsilon &&
                center >= south - epsilon &&
                center >= east - epsilon &&
                center >= west - epsilon &&
                center >= northEast - epsilon &&
                center >= northWest - epsilon &&
                center >= southEast - epsilon &&
                center >= southWest - epsilon &&
                center > math.min(math.min(north, south), math.min(east, west)) + epsilon;

            int ridgeArms = CountPillarRidgeArms(
                center,
                north,
                south,
                east,
                west,
                northEast,
                northWest,
                southEast,
                southWest,
                math.max(Settings.MinimumPillarProminenceMeters, epsilon));

            if (localMaximum &&
                pillarProminence >= Settings.MinimumPillarProminenceMeters &&
                ridgeArms >= Settings.MinimumPillarRidgeArms &&
                IsAllowedTectonicPillarSite(x, z))
            {
                FeatureRecords[index] = BuildRecord(
                    index,
                    x,
                    z,
                    center,
                    AnomalyFeatureKind.ChthonicPillar,
                    math.saturate(pillarProminence / math.max(0.001f, Settings.MinimumPillarProminenceMeters)),
                    0u);
                return;
            }

            float fissureDepth = ComputeFissureDepth(
                center,
                north,
                south,
                east,
                west,
                northEast,
                northWest,
                southEast,
                southWest);

            float localMean =
                (north + south + east + west + northEast + northWest + southEast + southWest) * 0.125f;
            bool narrowTrough =
                fissureDepth >= Settings.MinimumFissureDepthMeters &&
                localMean - center >= Settings.MinimumFissureDepthMeters * 0.5f;

            if (!narrowTrough)
                return;

            FissureMask[index] = 1;
            FeatureRecords[index] = BuildRecord(
                index,
                x,
                z,
                center,
                AnomalyFeatureKind.DeepFissure,
                math.saturate(fissureDepth / math.max(0.001f, Settings.MinimumFissureDepthMeters)),
                Settings.FissureInfluencePacked);
        }

        private AnomalyFeatureRecord BuildRecord(
            int index,
            int x,
            int z,
            float heightMeters,
            AnomalyFeatureKind kind,
            float strength01,
            uint biomeInfluencePacked)
        {
            double cellSize = Settings.CellSizeMeters;
            return new AnomalyFeatureRecord
            {
                Valid = 1,
                Kind = (byte)kind,
                Index = index,
                X = x,
                Z = z,
                AupX = Settings.OriginAup.x + x * cellSize,
                AupY = Settings.OriginAup.y + heightMeters,
                AupZ = Settings.OriginAup.z + z * cellSize,
                HeightMeters = heightMeters,
                Strength01 = strength01,
                BiomeInfluencePacked = biomeInfluencePacked
            };
        }

        private bool IsAllowedTectonicPillarSite(int x, int z)
        {
            if (Settings.RequireTectonicBoundary == 0)
                return true;

            double cellSize = Settings.CellSizeMeters;
            float2 worldXZ = new float2(
                (float)(Settings.OriginAup.x + x * cellSize),
                (float)(Settings.OriginAup.z + z * cellSize));
            float boundaryMask = WorldProceduralTerrainTectonicDisplacementJob.EvaluateTectonicBoundaryMask(
                worldXZ,
                Settings.TectonicBoundaryFrequency,
                Settings.TectonicBoundarySeed);
            return boundaryMask >= Settings.MinimumTectonicBoundaryMask;
        }

        private static bool AllFinite(
            float center,
            float north,
            float south,
            float east,
            float west,
            float northEast,
            float northWest,
            float southEast,
            float southWest)
        {
            return math.isfinite(center) &&
                   math.isfinite(north) &&
                   math.isfinite(south) &&
                   math.isfinite(east) &&
                   math.isfinite(west) &&
                   math.isfinite(northEast) &&
                   math.isfinite(northWest) &&
                   math.isfinite(southEast) &&
                   math.isfinite(southWest);
        }

        private static float ComputePillarProminence(
            float center,
            float north,
            float south,
            float east,
            float west,
            float northEast,
            float northWest,
            float southEast,
            float southWest)
        {
            float northSouth = math.min(center - north, center - south);
            float eastWest = math.min(center - east, center - west);
            float diagonalA = math.min(center - northEast, center - southWest);
            float diagonalB = math.min(center - northWest, center - southEast);
            return math.max(math.min(northSouth, eastWest), math.min(diagonalA, diagonalB));
        }

        private static int CountPillarRidgeArms(
            float center,
            float north,
            float south,
            float east,
            float west,
            float northEast,
            float northWest,
            float southEast,
            float southWest,
            float armDropMeters)
        {
            int count = 0;
            count += center - north >= armDropMeters ? 1 : 0;
            count += center - south >= armDropMeters ? 1 : 0;
            count += center - east >= armDropMeters ? 1 : 0;
            count += center - west >= armDropMeters ? 1 : 0;
            count += center - northEast >= armDropMeters ? 1 : 0;
            count += center - northWest >= armDropMeters ? 1 : 0;
            count += center - southEast >= armDropMeters ? 1 : 0;
            count += center - southWest >= armDropMeters ? 1 : 0;
            return count;
        }

        private static float ComputeFissureDepth(
            float center,
            float north,
            float south,
            float east,
            float west,
            float northEast,
            float northWest,
            float southEast,
            float southWest)
        {
            float northSouth = math.min(north, south) - center;
            float eastWest = math.min(east, west) - center;
            float diagonalA = math.min(northEast, southWest) - center;
            float diagonalB = math.min(northWest, southEast) - center;
            return math.max(math.max(northSouth, eastWest), math.max(diagonalA, diagonalB));
        }
    }

    /// <summary>
    /// Burst reduction job that keeps one strongest pillar candidate for bounded SDF injection.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic)]
    public struct SelectStrongestPillarFeatureJob : IJob
    {
        /// <summary>Detected feature records.</summary>
        [ReadOnly] public NativeArray<AnomalyFeatureRecord> FeatureRecords;

        /// <summary>Selected feature output. Index zero is written.</summary>
        [WriteOnly] public NativeArray<AnomalyFeatureRecord> SelectedFeature;

        /// <inheritdoc />
        public void Execute()
        {
            if (!SelectedFeature.IsCreated || SelectedFeature.Length <= 0)
                return;

            AnomalyFeatureRecord best = default;
            float bestStrength = -1f;
            for (int i = 0; i < FeatureRecords.Length; i++)
            {
                AnomalyFeatureRecord record = FeatureRecords[i];
                if (record.Valid == 0 || record.Kind != (byte)AnomalyFeatureKind.ChthonicPillar)
                    continue;

                if (!math.isfinite(record.Strength01) ||
                    !math.all(math.isfinite(new double3(record.AupX, record.AupY, record.AupZ))))
                {
                    continue;
                }

                if (record.Strength01 <= bestStrength)
                    continue;

                bestStrength = record.Strength01;
                best = record;
            }

            SelectedFeature[0] = best;
        }
    }
}
