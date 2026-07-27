using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    internal static class HectonAnomalyEngineLayout
    {
        public const int AnomalyBasinDetectionSettingsStrideBytes = 32;
        public const int AnomalyBasinRecordStrideBytes = 56;
        public const int AnomalyBasinFloodFillStateStrideBytes = 48;
    }

    /// <summary>
    /// Configuration for closed basin detection on a 2D heightmap.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = HectonAnomalyEngineLayout.AnomalyBasinDetectionSettingsStrideBytes)]
    public struct AnomalyBasinDetectionSettings
    {
        /// <summary>Heightmap width in samples.</summary>
        [FieldOffset(0)]
        public int Width;

        /// <summary>Heightmap height in samples.</summary>
        [FieldOffset(4)]
        public int Height;

        /// <summary>Heightmap cell size in meters.</summary>
        [FieldOffset(8)]
        public float CellSizeMeters;

        /// <summary>Minimum basin depth required before a basin is accepted.</summary>
        [FieldOffset(12)]
        public float MinimumDepthMeters;

        /// <summary>Maximum number of cells a candidate flood may visit.</summary>
        [FieldOffset(16)]
        public int MaxFloodCells;

        /// <summary>Height epsilon used for plateau and lip comparisons.</summary>
        [FieldOffset(20)]
        public float EqualHeightEpsilon;

        /// <summary>Maximum heap/neighbor operations allowed in one interruptible flood-fill slice.</summary>
        [FieldOffset(24)]
        public int MaxFloodFillOperationsPerSlice;
        [FieldOffset(28)]
        private uint _pad0;

        /// <summary>Returns a bounded copy of the settings.</summary>
        public AnomalyBasinDetectionSettings Sanitized()
        {
            return new AnomalyBasinDetectionSettings
            {
                Width = math.max(1, Width),
                Height = math.max(1, Height),
                CellSizeMeters = ResolvePositiveFinite(CellSizeMeters, 0.001f),
                MinimumDepthMeters = ResolveNonNegativeFinite(MinimumDepthMeters, 0f),
                MaxFloodCells = math.max(8, MaxFloodCells),
                EqualHeightEpsilon = ResolvePositiveFinite(EqualHeightEpsilon, 0.000001f),
                MaxFloodFillOperationsPerSlice = math.max(64, MaxFloodFillOperationsPerSlice == 0 ? 512 : MaxFloodFillOperationsPerSlice)
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
    }

    /// <summary>
    /// Closed basin record emitted by the anomaly detector.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = HectonAnomalyEngineLayout.AnomalyBasinRecordStrideBytes)]
    public struct AnomalyBasinRecord
    {
        /// <summary>One-based basin identifier. Zero means no valid basin.</summary>
        [FieldOffset(0)]
        public int BasinId;

        /// <summary>Flat heightmap index of the deepest point.</summary>
        [FieldOffset(4)]
        public int DeepestIndex;

        /// <summary>Deepest point X sample.</summary>
        [FieldOffset(8)]
        public int DeepestX;

        /// <summary>Deepest point Z sample.</summary>
        [FieldOffset(12)]
        public int DeepestZ;

        /// <summary>Inclusive minimum X sample in the basin mask.</summary>
        [FieldOffset(16)]
        public int MinX;

        /// <summary>Inclusive minimum Z sample in the basin mask.</summary>
        [FieldOffset(20)]
        public int MinZ;

        /// <summary>Inclusive maximum X sample in the basin mask.</summary>
        [FieldOffset(24)]
        public int MaxX;

        /// <summary>Inclusive maximum Z sample in the basin mask.</summary>
        [FieldOffset(28)]
        public int MaxZ;

        /// <summary>Number of masked cells in the basin.</summary>
        [FieldOffset(32)]
        public int CellCount;

        /// <summary>Height at the deepest point in meters.</summary>
        [FieldOffset(36)]
        public float DeepestHeight;

        /// <summary>Spill lip height in meters.</summary>
        [FieldOffset(40)]
        public float LipHeight;

        /// <summary>Approximate basin area in square meters.</summary>
        [FieldOffset(44)]
        public float AreaMetersSq;

        /// <summary>One when the record is valid.</summary>
        [FieldOffset(48)]
        public byte Valid;
        [FieldOffset(49)]
        private byte _pad0;
        [FieldOffset(50)]
        private ushort _pad1;
        [FieldOffset(52)]
        private uint _pad2;
    }

    /// <summary>
    /// Serializable continuation state for the interruptible closed-basin flood fill.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = HectonAnomalyEngineLayout.AnomalyBasinFloodFillStateStrideBytes)]
    public struct AnomalyBasinFloodFillState
    {
        [FieldOffset(0)]
        public int CandidateIndex;
        [FieldOffset(4)]
        public int Stamp;
        [FieldOffset(8)]
        public int BasinId;
        [FieldOffset(12)]
        public int SeedIndex;
        [FieldOffset(16)]
        public int HeapCount;
        [FieldOffset(20)]
        public int AcceptedCount;
        [FieldOffset(24)]
        public int ClearIndex;
        [FieldOffset(28)]
        public int Phase;
        [FieldOffset(32)]
        public float LipHeight;
        [FieldOffset(36)]
        public float DeepestHeight;
        [FieldOffset(40)]
        public byte FoundSpill;
        [FieldOffset(41)]
        public byte OpenBoundary;
        [FieldOffset(42)]
        public byte Initialized;
        [FieldOffset(43)]
        private byte _pad0;
        [FieldOffset(44)]
        private uint _pad1;
    }

    /// <summary>
    /// Standalone anomaly and SDF processors. All hot-path storage is caller-owned.
    /// </summary>
    public static class HectonAnomalyEngine
    {
        private const int SerialBasinDetectionCellThreshold = 2048;
        private const int PillarEnvelopeBatchCount = 32768;
        private const float TerrainSdfSnapHysteresisMeters = 0.05f;

#if UNITY_EDITOR
        private static int _editorMainThreadId;
#endif

        /// <summary>
        /// Schedules closed basin detection against a heightmap.
        /// </summary>
        public static JobHandle ScheduleClosedBasinDetection(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<byte> candidateMask,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells,
            AnomalyBasinDetectionSettings settings,
            JobHandle dependency = default)
        {
            AnomalyBasinDetectionSettings safeSettings = settings.Sanitized();
            int cellCount = checked(safeSettings.Width * safeSettings.Height);
            if (heightmap.Length < cellCount)
                throw new ArgumentException("Heightmap length is smaller than Width * Height.", nameof(heightmap));
            if (basinMask.Length < cellCount)
                throw new ArgumentException("Basin mask length is smaller than Width * Height.", nameof(basinMask));
            if (basinRecords.Length < cellCount)
                throw new ArgumentException("Basin records length is smaller than Width * Height.", nameof(basinRecords));
            if (candidateMask.Length < cellCount)
                throw new ArgumentException("Candidate mask length is smaller than Width * Height.", nameof(candidateMask));
            if (floodHeap.Length < cellCount)
                throw new ArgumentException("Flood heap length is smaller than Width * Height.", nameof(floodHeap));
            if (visitedStamp.Length < cellCount)
                throw new ArgumentException("Visited stamp length is smaller than Width * Height.", nameof(visitedStamp));
            if (acceptedCells.Length < cellCount)
                throw new ArgumentException("Accepted cells length is smaller than Width * Height.", nameof(acceptedCells));

            var scanJob = new ClosedBasinDetectionJob
            {
                Heightmap = heightmap,
                CandidateMask = candidateMask,
                BasinMask = basinMask,
                BasinRecords = basinRecords,
                Settings = safeSettings
            };

            var floodJob = new ClosedBasinFloodFillJob
            {
                Heightmap = heightmap,
                CandidateMask = candidateMask,
                BasinMask = basinMask,
                BasinRecords = basinRecords,
                FloodHeap = floodHeap,
                VisitedStamp = visitedStamp,
                AcceptedCells = acceptedCells,
                Settings = safeSettings,
                ClearAndScanCandidates = cellCount <= SerialBasinDetectionCellThreshold ? (byte)1 : (byte)0
            };

#if UNITY_EDITOR
            if (ShouldUseEditorDirectExecution(dependency))
            {
                if (floodJob.ClearAndScanCandidates != 0)
                    floodJob.Execute();
                else
                    ExecuteClosedBasinDetectionDirect(ref scanJob, ref floodJob, cellCount);
                return default;
            }
#endif

            if (floodJob.ClearAndScanCandidates != 0)
                return floodJob.Schedule(dependency);

            JobHandle scanHandle = scanJob.Schedule(cellCount, 64, dependency);
            return floodJob.Schedule(scanHandle);
        }

        /// <summary>
        /// Schedules one interruptible closed-basin flood-fill slice against an already-scanned candidate mask.
        /// </summary>
        public static JobHandle ScheduleClosedBasinFloodFillSlice(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<byte> candidateMask,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells,
            NativeQueue<AnomalyBasinFloodFillState> pendingStates,
            NativeQueue<AnomalyBasinFloodFillState> deferredStates,
            NativeArray<int> deferredStateBudget,
            NativeArray<int> sliceStatus,
            AnomalyBasinDetectionSettings settings,
            JobHandle dependency = default)
        {
            AnomalyBasinDetectionSettings safeSettings = settings.Sanitized();
            int cellCount = checked(safeSettings.Width * safeSettings.Height);
            if (heightmap.Length < cellCount)
                throw new ArgumentException("Heightmap length is smaller than Width * Height.", nameof(heightmap));
            if (basinMask.Length < cellCount)
                throw new ArgumentException("Basin mask length is smaller than Width * Height.", nameof(basinMask));
            if (basinRecords.Length < cellCount)
                throw new ArgumentException("Basin records length is smaller than Width * Height.", nameof(basinRecords));
            if (candidateMask.Length < cellCount)
                throw new ArgumentException("Candidate mask length is smaller than Width * Height.", nameof(candidateMask));
            if (floodHeap.Length < cellCount)
                throw new ArgumentException("Flood heap length is smaller than Width * Height.", nameof(floodHeap));
            if (visitedStamp.Length < cellCount)
                throw new ArgumentException("Visited stamp length is smaller than Width * Height.", nameof(visitedStamp));
            if (acceptedCells.Length < cellCount)
                throw new ArgumentException("Accepted cells length is smaller than Width * Height.", nameof(acceptedCells));
            if (!pendingStates.IsCreated)
                throw new ArgumentException("Pending flood-fill state queue is not created.", nameof(pendingStates));
            if (!deferredStates.IsCreated)
                throw new ArgumentException("Deferred flood-fill state queue is not created.", nameof(deferredStates));
            if (!deferredStateBudget.IsCreated || deferredStateBudget.Length < 2)
                throw new ArgumentException("Deferred flood-fill state budget requires at least two integer slots.", nameof(deferredStateBudget));
            if (!sliceStatus.IsCreated || sliceStatus.Length < 2)
                throw new ArgumentException("Slice status requires at least two integer slots.", nameof(sliceStatus));

            deferredStateBudget[0] = 1;
            deferredStateBudget[1] = 0;

            var job = new ClosedBasinFloodFillSliceJob
            {
                Heightmap = heightmap,
                CandidateMask = candidateMask,
                BasinMask = basinMask,
                BasinRecords = basinRecords,
                FloodHeap = floodHeap,
                VisitedStamp = visitedStamp,
                AcceptedCells = acceptedCells,
                PendingStates = pendingStates,
                DeferredStates = deferredStates,
                DeferredStateBudget = deferredStateBudget,
                SliceStatus = sliceStatus,
                Settings = safeSettings
            };

            return job.Schedule(dependency);
        }

        /// <summary>
        /// Schedules exact terrain-to-SDF top-surface snapping.
        /// </summary>
        public static JobHandle SnapSDFToTerrain(
            NativeArray<float> terrainHeights,
            int terrainWidth,
            int terrainDepth,
            float terrainCellSizeMeters,
            double3 terrainOriginAup,
            NativeArray<float> sdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            JobHandle dependency = default)
        {
            ValidateTerrainSdfBuffers(terrainHeights, terrainWidth, terrainDepth, sdf, sdfWidth, sdfHeight, sdfDepth);
            if (!IsFiniteAup(terrainOriginAup) || !IsFiniteAup(sdfOriginAup))
                return dependency;

            int safeSdfWidth = math.max(1, sdfWidth);
            int safeSdfHeight = math.max(1, sdfHeight);
            int safeSdfDepth = math.max(1, sdfDepth);
            float safeTerrainCellSize = ResolvePositiveFinite(terrainCellSizeMeters, 0.001f);
            float safeVoxelSize = ResolvePositiveFinite(voxelSizeMeters, 0.001f);

            var job = new SnapSDFToTerrainJob
            {
                TerrainHeights = terrainHeights,
                TerrainWidth = math.max(1, terrainWidth),
                TerrainDepth = math.max(1, terrainDepth),
                TerrainCellSizeMeters = safeTerrainCellSize,
                TerrainOriginAup = terrainOriginAup,
                Sdf = sdf,
                SdfWidth = safeSdfWidth,
                SdfHeight = safeSdfHeight,
                SdfDepth = safeSdfDepth,
                VoxelSizeMeters = safeVoxelSize,
                SdfOriginAup = sdfOriginAup,
                SnapHysteresisMeters = TerrainSdfSnapHysteresisMeters
            };

            JobHandle densityHandle = job.Schedule(safeSdfWidth * safeSdfHeight * safeSdfDepth, 64, dependency);
            var topCellJob = new SnapSDFTopCellsToTerrainJob
            {
                TerrainHeights = terrainHeights,
                TerrainWidth = math.max(1, terrainWidth),
                TerrainDepth = math.max(1, terrainDepth),
                TerrainCellSizeMeters = safeTerrainCellSize,
                TerrainOriginAup = terrainOriginAup,
                Sdf = sdf,
                SdfWidth = safeSdfWidth,
                SdfHeight = safeSdfHeight,
                SdfDepth = safeSdfDepth,
                VoxelSizeMeters = safeVoxelSize,
                SdfOriginAup = sdfOriginAup,
                SnapHysteresisMeters = TerrainSdfSnapHysteresisMeters
            };

            JobHandle topCellHandle = Unity.Jobs.IJobParallelForExtensions.Schedule(
                topCellJob,
                safeSdfWidth * safeSdfDepth,
                64,
                densityHandle);

            // Carve 3D Volumetric Procedural Caves into the SDF.
            // Uses gyroid reef networks plus cellular chambers; all cave subtraction is centralized here.
            var caveJob = new ProceduralCaveSdfCarveJob
            {
                Sdf = sdf,
                SdfWidth = safeSdfWidth,
                SdfHeight = safeSdfHeight,
                SdfDepth = safeSdfDepth,
                VoxelSizeMeters = safeVoxelSize,
                SdfOriginAup = sdfOriginAup,

                PrimaryFrequency = 0.012f,
                SecondaryFrequency = 0.017f,
                CarveStrengthMeters = 28.0f,
                CaveThreshold = 0.65f,
                MaxCrustDepthMeters = 400.0f,
                // R100: aligned to the voxels.md constant. The bible requires carving density to fade
                // to zero "within 30 meters of the terrain surface (depthToTerrainSurface < 30f)", i.e.
                // 30 m is the point where carving reaches FULL strength, not where it starts.
                // ProceduralCaveSdfCarveJob hard-rejects at depth <= SurfaceProtectionMeters and then
                // smoothsteps over the next 15 m, so the fade endpoint is P + 15. The previous 50.0f put
                // that endpoint at 65 m - more than twice the mandated shell - which is why cave mouths
                // could never reach a cliff face. 15.0f lands the endpoint exactly on 30 m while keeping
                // a hard 15 m no-carve shield against punching through the 2D heightmap.
                SurfaceProtectionMeters = 15.0f,
                StrataLayerThicknessMeters = 24.0f,
                StrataShelvingStrength = 0.4f,

                // We use a global fixed seed here since HectonAnomalyEngine doesn't receive a WorldSeed.
                // In a production call, this should be wired up to the Map/World seed.
                // For now, this static seed guarantees continuous cave networks across chunks.
                WorldSeed = 0x98BE7A1Eu
            };

            return caveJob.Schedule(safeSdfWidth * safeSdfHeight * safeSdfDepth, 64, topCellHandle);
        }

        /// <summary>
        /// Schedules ridge-derived pillar coordinate and fissure mask detection.
        /// </summary>
        public static JobHandle ScheduleRidgeFeatureDetection(
            NativeArray<float> heightmap,
            NativeArray<AnomalyFeatureRecord> featureRecords,
            NativeArray<byte> fissureMask,
            AnomalyRidgeDetectionSettings settings,
            JobHandle dependency = default)
        {
            AnomalyRidgeDetectionSettings safeSettings = settings.Sanitized();
            int cellCount = checked(safeSettings.Width * safeSettings.Height);
            if (heightmap.Length < cellCount)
                throw new ArgumentException("Heightmap length is smaller than Width * Height.", nameof(heightmap));
            if (featureRecords.Length < cellCount)
                throw new ArgumentException("Feature record length is smaller than Width * Height.", nameof(featureRecords));
            if (fissureMask.Length < cellCount)
                throw new ArgumentException("Fissure mask length is smaller than Width * Height.", nameof(fissureMask));

            var job = new AnomalyRidgeFeatureDetectionJob
            {
                Heightmap = heightmap,
                FeatureRecords = featureRecords,
                FissureMask = fissureMask,
                Settings = safeSettings
            };

#if UNITY_EDITOR
            if (ShouldUseEditorDirectExecution(dependency))
            {
                ExecuteRidgeFeatureDetectionDirect(ref job, cellCount);
                return default;
            }
#endif

            return job.Schedule(cellCount, 64, dependency);
        }

        /// <summary>
        /// Schedules injection of a solid chthonic pillar into a signed density field.
        /// </summary>
        public static JobHandle InjectMegaPillarSDF(
            NativeArray<float> sdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            double3 pillarBaseAup,
            float radiusMeters,
            float heightMeters = 1000f,
            float edgeWarpMeters = 24f,
            float noiseFrequency = 0.004f,
            JobHandle dependency = default)
        {
            ValidateSdfBuffer(sdf, sdfWidth, sdfHeight, sdfDepth);
            if (!IsFiniteAup(sdfOriginAup) || !IsFiniteAup(pillarBaseAup))
                return dependency;

            var job = new InjectMegaPillarSDFJob
            {
                Sdf = sdf,
                SdfWidth = math.max(1, sdfWidth),
                SdfHeight = math.max(1, sdfHeight),
                SdfDepth = math.max(1, sdfDepth),
                VoxelSizeMeters = ResolvePositiveFinite(voxelSizeMeters, 0.001f),
                SdfOriginAup = sdfOriginAup,
                PillarBaseAup = pillarBaseAup,
                RadiusMeters = ResolvePositiveFinite(radiusMeters, 0.001f),
                HeightMeters = ResolvePositiveFinite(heightMeters, 0.001f),
                EdgeWarpMeters = ResolveNonNegativeFinite(edgeWarpMeters, 0f),
                NoiseFrequency = ResolvePositiveFinite(noiseFrequency, 0.000001f)
            };

            double3 chunkMinAup = sdfOriginAup;
            double3 chunkMaxAup = ResolveSdfChunkMaxAup(
                sdfOriginAup,
                job.SdfWidth,
                job.SdfHeight,
                job.SdfDepth,
                job.VoxelSizeMeters);
            float boundsRadius = job.RadiusMeters + job.EdgeWarpMeters + job.VoxelSizeMeters;
            if (!PillarAabbIntersectsChunk(pillarBaseAup, boundsRadius, job.HeightMeters, chunkMinAup, chunkMaxAup))
                return dependency;

            int safeSdfHeight = math.max(1, sdfHeight);
            int radiusCells = ResolvePillarEnvelopeRadiusCells(job.RadiusMeters, job.EdgeWarpMeters, job.VoxelSizeMeters);
            int diameter = checked(radiusCells * 2 + 1);
            int laneCount = checked(diameter * safeSdfHeight * diameter);
            return job.Schedule(laneCount, PillarEnvelopeBatchCount, dependency);
        }

        /// <summary>
        /// Schedules injection of one selected chthonic pillar feature into a signed density field.
        /// </summary>
        public static JobHandle InjectSelectedMegaPillarSDF(
            NativeArray<float> sdf,
            NativeArray<AnomalyFeatureRecord> selectedFeature,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            float radiusMeters,
            float heightMeters = 1000f,
            float edgeWarpMeters = 24f,
            float noiseFrequency = 0.004f,
            JobHandle dependency = default)
        {
            if (!selectedFeature.IsCreated || selectedFeature.Length <= 0)
                throw new ArgumentException("Selected feature buffer is not valid.", nameof(selectedFeature));
            ValidateSdfBuffer(sdf, sdfWidth, sdfHeight, sdfDepth);
            if (!IsFiniteAup(sdfOriginAup))
                return dependency;

            int safeSdfWidth = math.max(1, sdfWidth);
            int safeSdfHeight = math.max(1, sdfHeight);
            int safeSdfDepth = math.max(1, sdfDepth);
            var job = new InjectSelectedMegaPillarSDFJob
            {
                Sdf = sdf,
                SelectedFeature = selectedFeature,
                SdfWidth = safeSdfWidth,
                SdfHeight = safeSdfHeight,
                SdfDepth = safeSdfDepth,
                VoxelSizeMeters = ResolvePositiveFinite(voxelSizeMeters, 0.001f),
                SdfOriginAup = sdfOriginAup,
                RadiusMeters = ResolvePositiveFinite(radiusMeters, 0.001f),
                HeightMeters = ResolvePositiveFinite(heightMeters, 0.001f),
                EdgeWarpMeters = ResolveNonNegativeFinite(edgeWarpMeters, 0f),
                NoiseFrequency = ResolvePositiveFinite(noiseFrequency, 0.000001f)
            };
            job.ChunkMinAup = sdfOriginAup;
            job.ChunkMaxAup = ResolveSdfChunkMaxAup(
                sdfOriginAup,
                job.SdfWidth,
                job.SdfHeight,
                job.SdfDepth,
                job.VoxelSizeMeters);

            int radiusCells = ResolvePillarEnvelopeRadiusCells(job.RadiusMeters, job.EdgeWarpMeters, job.VoxelSizeMeters);
            int diameter = checked(radiusCells * 2 + 1);
            int laneCount = checked(diameter * diameter);
            return job.Schedule(laneCount, PillarEnvelopeBatchCount, dependency);
        }

        /// <summary>
        /// Schedules injection of a deep negative fissure trench into a signed density field.
        /// </summary>
        public static JobHandle InjectDeepFissureSDF(
            NativeArray<float> sdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            double3 fissureTopAup,
            float2 directionXz,
            float halfLengthMeters,
            float radiusMeters,
            float depthMeters = 1000f,
            uint fissureInfluencePacked = 0u,
            JobHandle dependency = default)
        {
            NativeArray<uint> emptyInfluence = default;
            return InjectDeepFissureSDF(
                sdf,
                emptyInfluence,
                sdfWidth,
                sdfHeight,
                sdfDepth,
                voxelSizeMeters,
                sdfOriginAup,
                fissureTopAup,
                directionXz,
                halfLengthMeters,
                radiusMeters,
                depthMeters,
                fissureInfluencePacked,
                dependency);
        }

        /// <summary>
        /// Schedules injection of a canyon network based on a pre-computed 2D fissure mask.
        /// </summary>
        public static JobHandle InjectFissureNetworkSDF(
            NativeArray<float> sdf,
            NativeArray<byte> fissureMask,
            NativeArray<float> terrainHeights,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            float depthMeters = 500f,
            JobHandle dependency = default)
        {
            ValidateSdfBuffer(sdf, sdfWidth, sdfHeight, sdfDepth);
            if (!IsFiniteAup(sdfOriginAup))
                return dependency;

            int safeSdfWidth = math.max(1, sdfWidth);
            int safeSdfHeight = math.max(1, sdfHeight);
            int safeSdfDepth = math.max(1, sdfDepth);
            int cellCount = checked(safeSdfWidth * safeSdfDepth);
            
            if (fissureMask.Length < cellCount)
                throw new ArgumentException("Fissure mask array is smaller than sdfWidth * sdfDepth.", nameof(fissureMask));
            if (terrainHeights.Length < cellCount)
                throw new ArgumentException("Terrain heights array is smaller than sdfWidth * sdfDepth.", nameof(terrainHeights));

            var job = new CarveFissureMaskSDFJob
            {
                Sdf = sdf,
                FissureMask = fissureMask,
                TerrainHeights = terrainHeights,
                SdfWidth = safeSdfWidth,
                SdfHeight = safeSdfHeight,
                SdfDepth = safeSdfDepth,
                VoxelSizeMeters = ResolvePositiveFinite(voxelSizeMeters, 0.001f),
                SdfOriginAup = sdfOriginAup,
                DepthMeters = ResolvePositiveFinite(depthMeters, 0.001f)
            };

            int laneCount = cellCount;
            return job.Schedule(laneCount, 64, dependency);
        }

        /// <summary>
        /// Schedules injection of a deep negative fissure trench and optional packed biome influence cells.
        /// </summary>
        public static JobHandle InjectDeepFissureSDF(
            NativeArray<float> sdf,
            NativeArray<uint> biomeInfluencePacked,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            double3 fissureTopAup,
            float2 directionXz,
            float halfLengthMeters,
            float radiusMeters,
            float depthMeters = 1000f,
            uint fissureInfluencePacked = 0u,
            JobHandle dependency = default)
        {
            ValidateSdfBuffer(sdf, sdfWidth, sdfHeight, sdfDepth);
            if (!IsFiniteAup(sdfOriginAup) || !IsFiniteAup(fissureTopAup))
                return dependency;

            int safeSdfWidth = math.max(1, sdfWidth);
            int safeSdfHeight = math.max(1, sdfHeight);
            int safeSdfDepth = math.max(1, sdfDepth);
            int sdfCount = checked(safeSdfWidth * safeSdfHeight * safeSdfDepth);
            if (biomeInfluencePacked.IsCreated && biomeInfluencePacked.Length < sdfCount)
                throw new ArgumentException("Biome influence array is smaller than sdfWidth * sdfHeight * sdfDepth.", nameof(biomeInfluencePacked));

            var job = new InjectDeepFissureSDFJob
            {
                Sdf = sdf,
                BiomeInfluencePacked = biomeInfluencePacked,
                SdfWidth = safeSdfWidth,
                SdfHeight = safeSdfHeight,
                SdfDepth = safeSdfDepth,
                VoxelSizeMeters = ResolvePositiveFinite(voxelSizeMeters, 0.001f),
                SdfOriginAup = sdfOriginAup,
                FissureTopAup = fissureTopAup,
                DirectionXZ = ResolveSafeDirectionXz(directionXz),
                HalfLengthMeters = ResolvePositiveFinite(halfLengthMeters, 0.001f),
                RadiusMeters = ResolvePositiveFinite(radiusMeters, 0.001f),
                DepthMeters = ResolvePositiveFinite(depthMeters, 0.001f),
                FissureInfluencePacked = fissureInfluencePacked
            };

            return job.Schedule(sdfCount, 64, dependency);
        }

        /// <summary>
        /// Schedules lateral SDF displacement that turns steep stitched slopes into overhangs.
        /// </summary>
        public static JobHandle ApplyVoxelCliffOverhangNoise(
            NativeArray<float> inputSdf,
            NativeArray<float> outputSdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            float slopeThreshold,
            float lateralAmplitudeMeters,
            float noiseFrequency,
            float strength,
            double3 originAup = default,
            JobHandle dependency = default)
        {
            ValidateSdfBuffer(inputSdf, sdfWidth, sdfHeight, sdfDepth);
            ValidateSdfBuffer(outputSdf, sdfWidth, sdfHeight, sdfDepth);

            var job = new VoxelCliffOverhangNoiseJob
            {
                InputSdf = inputSdf,
                OutputSdf = outputSdf,
                SdfWidth = math.max(1, sdfWidth),
                SdfHeight = math.max(1, sdfHeight),
                SdfDepth = math.max(1, sdfDepth),
                VoxelSizeMeters = ResolvePositiveFinite(voxelSizeMeters, 0.001f),
                SlopeThreshold = ResolveNonNegativeFinite(slopeThreshold, 0f),
                LateralAmplitudeMeters = ResolveNonNegativeFinite(lateralAmplitudeMeters, 0f),
                NoiseFrequency = ResolvePositiveFinite(noiseFrequency, 0.000001f),
                Strength = math.isfinite(strength) ? math.saturate(strength) : 0f,
                OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero
            };

            int safeSdfWidth = math.max(1, sdfWidth);
            int safeSdfHeight = math.max(1, sdfHeight);
            int safeSdfDepth = math.max(1, sdfDepth);
            return job.Schedule(safeSdfWidth * safeSdfHeight * safeSdfDepth, 64, dependency);
        }

        /// <summary>
        /// Packs a procedural biome influence cell into the project-standard byte layout.
        /// </summary>
        public static uint PackBiomeInfluenceCell(byte primaryBiomeId, byte secondaryBiomeId, byte blend255, byte flags)
        {
            return HectonBiomeVisualFamilyUtility.PackCellFromBiomeIds(
                primaryBiomeId,
                secondaryBiomeId,
                blend255,
                flags);
        }

        private static void ValidateTerrainSdfBuffers(
            NativeArray<float> terrainHeights,
            int terrainWidth,
            int terrainDepth,
            NativeArray<float> sdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth)
        {
            int terrainCount = checked(math.max(1, terrainWidth) * math.max(1, terrainDepth));
            if (terrainHeights.Length < terrainCount)
                throw new ArgumentException("Terrain height array is smaller than terrainWidth * terrainDepth.", nameof(terrainHeights));

            ValidateSdfBuffer(sdf, sdfWidth, sdfHeight, sdfDepth);
        }

        private static void ValidateSdfBuffer(NativeArray<float> sdf, int sdfWidth, int sdfHeight, int sdfDepth)
        {
            int count = checked(math.max(1, sdfWidth) * math.max(1, sdfHeight) * math.max(1, sdfDepth));
            if (sdf.Length < count)
                throw new ArgumentException("SDF array is smaller than sdfWidth * sdfHeight * sdfDepth.", nameof(sdf));
        }

        private static bool IsFiniteAup(double3 aup)
        {
            return math.all(math.isfinite(aup));
        }

        private static float ResolvePositiveFinite(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static float ResolveNonNegativeFinite(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        private static float2 ResolveSafeDirectionXz(float2 directionXz)
        {
            if (!math.all(math.isfinite(directionXz)) || math.lengthsq(directionXz) <= 0.000001f)
                return new float2(1f, 0f);

            float2 absDirection = math.abs(directionXz);
            return absDirection.x >= absDirection.y
                ? new float2(directionXz.x < 0f ? -1f : 1f, 0f)
                : new float2(0f, directionXz.y < 0f ? -1f : 1f);
        }

        private static int ResolvePillarEnvelopeRadiusCells(float radiusMeters, float edgeWarpMeters, float voxelSizeMeters)
        {
            float safeVoxel = ResolvePositiveFinite(voxelSizeMeters, 0.001f);
            float maxRadius = ResolvePositiveFinite(radiusMeters, 0.001f) + ResolveNonNegativeFinite(edgeWarpMeters, 0f) + safeVoxel;
            return math.max(0, (int)math.ceil(maxRadius / safeVoxel));
        }

        private static double3 ResolveSdfChunkMaxAup(double3 sdfOriginAup, int sdfWidth, int sdfHeight, int sdfDepth, float voxelSizeMeters)
        {
            double safeVoxel = ResolvePositiveFinite(voxelSizeMeters, 0.001f);
            return new double3(
                sdfOriginAup.x + math.max(0, sdfWidth - 1) * safeVoxel,
                sdfOriginAup.y + math.max(0, sdfHeight - 1) * safeVoxel,
                sdfOriginAup.z + math.max(0, sdfDepth - 1) * safeVoxel);
        }

        private static bool PillarAabbIntersectsChunk(
            double3 pillarBaseAup,
            float radiusMeters,
            float heightMeters,
            double3 chunkMinAup,
            double3 chunkMaxAup)
        {
            if (!IsFiniteAup(pillarBaseAup) || !IsFiniteAup(chunkMinAup) || !IsFiniteAup(chunkMaxAup))
                return false;

            double radius = ResolvePositiveFinite(radiusMeters, 0.001f);
            double height = ResolvePositiveFinite(heightMeters, 0.001f);
            double minX = pillarBaseAup.x - radius;
            double maxX = pillarBaseAup.x + radius;
            double minY = pillarBaseAup.y;
            double maxY = pillarBaseAup.y + height;
            double minZ = pillarBaseAup.z - radius;
            double maxZ = pillarBaseAup.z + radius;

            return maxX >= chunkMinAup.x &&
                   minX <= chunkMaxAup.x &&
                   maxY >= chunkMinAup.y &&
                   minY <= chunkMaxAup.y &&
                   maxZ >= chunkMinAup.z &&
                   minZ <= chunkMaxAup.z;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void CaptureEditorMainThread()
        {
            Volatile.Write(ref _editorMainThreadId, Thread.CurrentThread.ManagedThreadId);
        }

        private static bool ShouldUseEditorDirectExecution(JobHandle dependency)
        {
            if (!dependency.IsCompleted)
                return false;
            if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref _editorMainThreadId))
                return false;

            return UnityEditor.EditorApplication.isCompiling ||
                   UnityEditor.EditorApplication.isUpdating;
        }

        private static void ExecuteClosedBasinDetectionDirect(
            ref ClosedBasinDetectionJob scanJob,
            ref ClosedBasinFloodFillJob floodJob,
            int cellCount)
        {
            for (int i = 0; i < cellCount; i++)
                scanJob.Execute(i);

            floodJob.Execute();
        }

        private static void ExecuteRidgeFeatureDetectionDirect(
            ref AnomalyRidgeFeatureDetectionJob job,
            int cellCount)
        {
            for (int i = 0; i < cellCount; i++)
                job.Execute(i);
        }
#endif
    }

    /// <summary>
    /// Burst parallel kernel that scans the heightmap, clears outputs, and marks local minimum candidates.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Deterministic)]
    public struct ClosedBasinDetectionJob : IJobParallelFor
    {
        /// <summary>Input heightmap in meters.</summary>
        [ReadOnly, NoAlias] public NativeArray<float> Heightmap;

        /// <summary>Candidate minima mask. One means the cell is a flood-fill seed.</summary>
        [NoAlias]
        public NativeArray<byte> CandidateMask;

        /// <summary>Basin extent mask. One means the cell belongs to an accepted basin.</summary>
        [NoAlias]
        public NativeArray<byte> BasinMask;

        /// <summary>Record array indexed by candidate cell.</summary>
        [NoAlias]
        public NativeArray<AnomalyBasinRecord> BasinRecords;

        /// <summary>Detection settings.</summary>
        public AnomalyBasinDetectionSettings Settings;

        /// <inheritdoc />
        public void Execute(int index)
        {
            CandidateMask[index] = 0;
            BasinMask[index] = 0;
            BasinRecords[index] = default;

            int width = Settings.Width;
            int height = Settings.Height;
            int x = index % width;
            int z = index / width;
            if (x <= 0 || z <= 0 || x >= width - 1 || z >= height - 1)
                return;

            float center = Heightmap[index];
            if (!math.isfinite(center))
                return;

            float epsilon = Settings.EqualHeightEpsilon;
            bool hasHigherNeighbor = false;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    int neighborIndex = index + dx + dz * width;
                    float neighbor = Heightmap[neighborIndex];
                    if (!math.isfinite(neighbor))
                        return;

                    if (neighbor < center - epsilon)
                        return;

                    if (math.abs(neighbor - center) <= epsilon && neighborIndex < index)
                        return;

                    if (neighbor > center + epsilon)
                        hasHigherNeighbor = true;
                }
            }

            if (!hasHigherNeighbor)
                return;

            CandidateMask[index] = 1;
        }
    }

    /// <summary>
    /// Burst flood-fill kernel that expands candidate minima to their spill lip and writes basin extents.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Deterministic)]
    public struct ClosedBasinFloodFillJob : IJob
    {
        /// <summary>Input heightmap in meters.</summary>
        [ReadOnly, NoAlias]
        public NativeArray<float> Heightmap;

        /// <summary>Candidate minima mask.</summary>
        [NoAlias]
        public NativeArray<byte> CandidateMask;

        /// <summary>Output basin mask.</summary>
        [NoAlias]
        public NativeArray<byte> BasinMask;

        /// <summary>Output records indexed by candidate cell.</summary>
        [NoAlias]
        public NativeArray<AnomalyBasinRecord> BasinRecords;

        /// <summary>Binary min-heap scratch. Caller owns storage.</summary>
        [NoAlias]
        public NativeArray<int> FloodHeap;

        /// <summary>Visited stamp scratch. Caller owns storage.</summary>
        [NoAlias]
        public NativeArray<int> VisitedStamp;

        /// <summary>Accepted cell scratch. Caller owns storage.</summary>
        [NoAlias]
        public NativeArray<int> AcceptedCells;

        /// <summary>Detection settings.</summary>
        public AnomalyBasinDetectionSettings Settings;

        /// <summary>When one, clears outputs and scans local minima inside this single job.</summary>
        public byte ClearAndScanCandidates;

        /// <inheritdoc />
        public void Execute()
        {
            int cellCount = Settings.Width * Settings.Height;
            int stamp = 1;
            int basinId = 1;

            if (ClearAndScanCandidates != 0)
                ClearAndScanLocalMinima(cellCount);

            for (int i = 0; i < cellCount; i++)
                VisitedStamp[i] = 0;

            for (int candidateIndex = 0; candidateIndex < cellCount; candidateIndex++)
            {
                if (CandidateMask[candidateIndex] == 0 || BasinMask[candidateIndex] != 0)
                    continue;

                AnomalyBasinRecord record = ResolveCandidate(candidateIndex, stamp, basinId, out int nextStamp);
                stamp = nextStamp;
                if (record.Valid == 0)
                {
                    BasinRecords[candidateIndex] = default;
                    continue;
                }

                basinId++;
                BasinRecords[candidateIndex] = record;
            }
        }

        private void ClearAndScanLocalMinima(int cellCount)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            float epsilon = Settings.EqualHeightEpsilon;
            for (int index = 0; index < cellCount; index++)
            {
                CandidateMask[index] = 0;
                BasinMask[index] = 0;
                BasinRecords[index] = default;

                int x = index % width;
                int z = index / width;
                if (x <= 0 || z <= 0 || x >= width - 1 || z >= height - 1)
                    continue;

                float center = Heightmap[index];
                if (!math.isfinite(center))
                    continue;

                bool hasHigherNeighbor = false;
                bool rejected = false;
                for (int dz = -1; dz <= 1 && !rejected; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int neighborIndex = index + dx + dz * width;
                        float neighbor = Heightmap[neighborIndex];
                        if (!math.isfinite(neighbor) ||
                            neighbor < center - epsilon ||
                            (math.abs(neighbor - center) <= epsilon && neighborIndex < index))
                        {
                            rejected = true;
                            break;
                        }

                        if (neighbor > center + epsilon)
                            hasHigherNeighbor = true;
                    }
                }

                if (!rejected && hasHigherNeighbor)
                    CandidateMask[index] = 1;
            }
        }

        private AnomalyBasinRecord ResolveCandidate(int seedIndex, int stamp, int basinId, out int nextStamp)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            int cellCount = width * height;
            int maxFloodCells = math.min(Settings.MaxFloodCells, cellCount);
            float epsilon = Settings.EqualHeightEpsilon;
            float deepestHeight = Heightmap[seedIndex];
            if (!math.isfinite(deepestHeight))
            {
                nextStamp = stamp;
                return default;
            }

            if (!NextStamp(ref stamp))
            {
                ClearVisited(cellCount);
                stamp = 1;
            }

            int heapCount = 0;
            int acceptedCount = 0;
            float lipHeight = deepestHeight;
            bool foundSpill = false;
            bool openBoundary = false;

            MarkVisited(seedIndex, stamp);
            HeapPush(ref heapCount, seedIndex);

            while (heapCount > 0 && acceptedCount < maxFloodCells)
            {
                int cellIndex = HeapPop(ref heapCount);
                float cellHeight = Heightmap[cellIndex];
                AcceptedCells[acceptedCount++] = cellIndex;
                lipHeight = math.max(lipHeight, cellHeight);

                int x = cellIndex % width;
                int z = cellIndex / width;
                if (IsOpenBoundaryEscape(
                        x,
                        z,
                        width,
                        height,
                        cellHeight,
                        deepestHeight,
                        Settings.MinimumDepthMeters,
                        epsilon))
                {
                    openBoundary = true;
                    break;
                }

                if (cellHeight > deepestHeight + epsilon && HasUnvisitedLowerNeighbor(cellIndex, cellHeight, stamp, epsilon))
                {
                    lipHeight = cellHeight;
                    foundSpill = true;
                    break;
                }

                for (int dz = -1; dz <= 1; dz++)
                {
                    int nz = z + dz;
                    if (nz < 0 || nz >= height)
                        continue;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int nx = x + dx;
                        if (nx < 0 || nx >= width)
                            continue;

                        int neighborIndex = nx + nz * width;
                        if (VisitedStamp[neighborIndex] == stamp || BasinMask[neighborIndex] != 0)
                            continue;

                        float neighborHeight = Heightmap[neighborIndex];
                        if (!math.isfinite(neighborHeight))
                            continue;

                        MarkVisited(neighborIndex, stamp);
                        HeapPush(ref heapCount, neighborIndex);
                    }
                }
            }

            nextStamp = stamp;
            if (openBoundary || acceptedCount <= 0 || (acceptedCount >= maxFloodCells && heapCount > 0 && !foundSpill))
                return default;

            float depth = lipHeight - deepestHeight;
            if (depth + epsilon < Settings.MinimumDepthMeters)
                return default;

            int deepestIndex = seedIndex;
            int deepestX = seedIndex % width;
            int deepestZ = seedIndex / width;
            int minX = width;
            int minZ = height;
            int maxX = 0;
            int maxZ = 0;
            int maskedCount = 0;

            for (int i = 0; i < acceptedCount; i++)
            {
                int cellIndex = AcceptedCells[i];
                float cellHeight = Heightmap[cellIndex];
                if (!IsBelowLip(cellHeight, lipHeight, epsilon))
                    continue;

                int x = cellIndex % width;
                int z = cellIndex / width;
                BasinMask[cellIndex] = 1;
                minX = math.min(minX, x);
                minZ = math.min(minZ, z);
                maxX = math.max(maxX, x);
                maxZ = math.max(maxZ, z);
                maskedCount++;

                if (cellHeight < deepestHeight - epsilon)
                {
                    deepestHeight = cellHeight;
                    deepestIndex = cellIndex;
                    deepestX = x;
                    deepestZ = z;
                }
            }

            if (maskedCount <= 0)
                return default;

            float cellSize = Settings.CellSizeMeters;
            return new AnomalyBasinRecord
            {
                BasinId = basinId,
                DeepestIndex = deepestIndex,
                DeepestX = deepestX,
                DeepestZ = deepestZ,
                MinX = minX,
                MinZ = minZ,
                MaxX = maxX,
                MaxZ = maxZ,
                CellCount = maskedCount,
                DeepestHeight = deepestHeight,
                LipHeight = lipHeight,
                AreaMetersSq = maskedCount * cellSize * cellSize,
                Valid = 1
            };
        }

        private bool HasUnvisitedLowerNeighbor(int cellIndex, float cellHeight, int stamp, float epsilon)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            int x = cellIndex % width;
            int z = cellIndex / width;

            for (int dz = -1; dz <= 1; dz++)
            {
                int nz = z + dz;
                if (nz < 0 || nz >= height)
                    continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    int nx = x + dx;
                    if (nx < 0 || nx >= width)
                        continue;

                    int neighborIndex = nx + nz * width;
                    if (VisitedStamp[neighborIndex] == stamp)
                        continue;

                    float neighborHeight = Heightmap[neighborIndex];
                    if (math.isfinite(neighborHeight) && neighborHeight < cellHeight - epsilon)
                        return true;
                }
            }

            return false;
        }

        private void HeapPush(ref int heapCount, int index)
        {
            int heapIndex = heapCount++;
            FloodHeap[heapIndex] = index;

            while (heapIndex > 0)
            {
                int parent = (heapIndex - 1) >> 1;
                if (Heightmap[FloodHeap[parent]] <= Heightmap[index])
                    break;

                FloodHeap[heapIndex] = FloodHeap[parent];
                heapIndex = parent;
            }

            FloodHeap[heapIndex] = index;
        }

        private int HeapPop(ref int heapCount)
        {
            int result = FloodHeap[0];
            int last = FloodHeap[--heapCount];
            if (heapCount <= 0)
                return result;

            int heapIndex = 0;
            while (true)
            {
                int left = heapIndex * 2 + 1;
                if (left >= heapCount)
                    break;

                int right = left + 1;
                int child = right < heapCount && Heightmap[FloodHeap[right]] < Heightmap[FloodHeap[left]] ? right : left;
                if (Heightmap[FloodHeap[child]] >= Heightmap[last])
                    break;

                FloodHeap[heapIndex] = FloodHeap[child];
                heapIndex = child;
            }

            FloodHeap[heapIndex] = last;
            return result;
        }

        private void MarkVisited(int index, int stamp)
        {
            VisitedStamp[index] = stamp;
        }

        private static bool IsOpenBoundaryEscape(
            int x,
            int z,
            int width,
            int height,
            float cellHeight,
            float deepestHeight,
            float minimumDepthMeters,
            float epsilon)
        {
            return (x <= 0 || z <= 0 || x >= width - 1 || z >= height - 1) &&
                   cellHeight - deepestHeight + epsilon < minimumDepthMeters;
        }

        private static bool IsBelowLip(float cellHeight, float lipHeight, float epsilon)
        {
            return math.isfinite(cellHeight) && cellHeight < lipHeight - epsilon;
        }

        private static bool NextStamp(ref int stamp)
        {
            if (stamp == int.MaxValue)
                return false;

            stamp++;
            return true;
        }

        private void ClearVisited(int cellCount)
        {
            for (int i = 0; i < cellCount; i++)
                VisitedStamp[i] = 0;
        }
    }

    /// <summary>
    /// Burst flood-fill slice that defers active basin state instead of monopolizing a worker on huge basins.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Deterministic)]
    public struct ClosedBasinFloodFillSliceJob : IJob
    {
        private const int PhaseScanCandidate = 0;
        private const int PhaseFlood = 1;
        private const int PhaseClearVisitedStamp = 2;
        private const int StatusDeferred = 1;
        private const int StatusCompleted = 2;
        private const int StatusDeferredOverflow = 3;

        [ReadOnly, NoAlias] public NativeArray<float> Heightmap;
        [NoAlias] public NativeArray<byte> CandidateMask;
        [NoAlias] public NativeArray<byte> BasinMask;
        [NoAlias] public NativeArray<AnomalyBasinRecord> BasinRecords;
        [NoAlias] public NativeArray<int> FloodHeap;
        [NoAlias] public NativeArray<int> VisitedStamp;
        [NoAlias] public NativeArray<int> AcceptedCells;
        public NativeQueue<AnomalyBasinFloodFillState> PendingStates;
        public NativeQueue<AnomalyBasinFloodFillState> DeferredStates;
        [NoAlias]
        public NativeArray<int> DeferredStateBudget;
        [NoAlias]
        public NativeArray<int> SliceStatus;
        public AnomalyBasinDetectionSettings Settings;

        public void Execute()
        {
            int operationBudget = math.max(64, Settings.MaxFloodFillOperationsPerSlice);
            int operations = 0;
            AnomalyBasinFloodFillState state;
            if (!PendingStates.TryDequeue(out state) || state.Initialized == 0)
            {
                state = new AnomalyBasinFloodFillState
                {
                    CandidateIndex = 0,
                    Stamp = 1,
                    BasinId = 1,
                    Phase = PhaseScanCandidate,
                    Initialized = 1
                };
            }

            int cellCount = Settings.Width * Settings.Height;
            SanitizeContinuationState(ref state, cellCount);
            SliceStatus[0] = 0;
            while (state.CandidateIndex < cellCount)
            {
                if (state.Phase == PhaseClearVisitedStamp)
                {
                    if (!TryContinueVisitedStampClear(ref state, ref operations, operationBudget, cellCount))
                    {
                        Defer(state, operations);
                        return;
                    }

                    state.Phase = PhaseScanCandidate;
                    state.ClearIndex = 0;
                }

                if (state.Phase == PhaseScanCandidate)
                {
                    if (!TryStartNextCandidate(ref state, ref operations, operationBudget, cellCount))
                    {
                        Defer(state, operations);
                        return;
                    }

                    if (state.CandidateIndex >= cellCount)
                        break;
                }

                if (state.Phase == PhaseFlood)
                {
                    if (!TryContinueFlood(ref state, ref operations, operationBudget))
                    {
                        Defer(state, operations);
                        return;
                    }

                    FinalizeCandidate(ref state);
                    state.CandidateIndex++;
                    state.Phase = PhaseScanCandidate;
                }
            }

            SliceStatus[0] = StatusCompleted;
            SliceStatus[1] = operations;
        }

        private bool TryStartNextCandidate(
            ref AnomalyBasinFloodFillState state,
            ref int operations,
            int operationBudget,
            int cellCount)
        {
            while (state.CandidateIndex < cellCount)
            {
                if (++operations >= operationBudget)
                    return false;

                int candidateIndex = state.CandidateIndex;
                if (CandidateMask[candidateIndex] == 0 || BasinMask[candidateIndex] != 0)
                {
                    state.CandidateIndex++;
                    continue;
                }

                int stamp = math.max(1, state.Stamp);
                if (!NextStamp(ref stamp))
                {
                    state.Stamp = 1;
                    state.ClearIndex = 0;
                    state.Phase = PhaseClearVisitedStamp;
                    return false;
                }

                float deepestHeight = Heightmap[candidateIndex];
                if (!math.isfinite(deepestHeight))
                {
                    BasinRecords[candidateIndex] = default;
                    state.CandidateIndex++;
                    continue;
                }

                state.Stamp = stamp;
                state.SeedIndex = candidateIndex;
                state.HeapCount = 0;
                state.AcceptedCount = 0;
                state.ClearIndex = 0;
                state.LipHeight = deepestHeight;
                state.DeepestHeight = deepestHeight;
                state.FoundSpill = 0;
                state.OpenBoundary = 0;
                state.Phase = PhaseFlood;
                MarkVisited(candidateIndex, stamp);
                HeapPush(ref state.HeapCount, candidateIndex);
                return true;
            }

            return true;
        }

        private bool TryContinueVisitedStampClear(
            ref AnomalyBasinFloodFillState state,
            ref int operations,
            int operationBudget,
            int cellCount)
        {
            int clearIndex = math.clamp(state.ClearIndex, 0, cellCount);
            while (clearIndex < cellCount)
            {
                if (++operations >= operationBudget)
                {
                    state.ClearIndex = clearIndex;
                    return false;
                }

                VisitedStamp[clearIndex] = 0;
                clearIndex++;
            }

            state.ClearIndex = 0;
            return true;
        }

        private static void SanitizeContinuationState(ref AnomalyBasinFloodFillState state, int cellCount)
        {
            state.CandidateIndex = math.clamp(state.CandidateIndex, 0, cellCount);
            state.Stamp = math.max(1, state.Stamp);
            state.BasinId = math.max(1, state.BasinId);

            if (state.Phase < PhaseScanCandidate || state.Phase > PhaseClearVisitedStamp)
            {
                ResetToScanPhase(ref state);
                return;
            }

            if (state.Phase == PhaseScanCandidate)
            {
                state.HeapCount = 0;
                state.AcceptedCount = 0;
                state.ClearIndex = 0;
                return;
            }

            if (state.Phase == PhaseClearVisitedStamp)
            {
                state.ClearIndex = math.clamp(state.ClearIndex, 0, cellCount);
                state.HeapCount = 0;
                state.AcceptedCount = 0;
                return;
            }

            if (state.SeedIndex < 0 ||
                state.SeedIndex >= cellCount ||
                state.HeapCount <= 0 ||
                state.HeapCount > cellCount ||
                state.AcceptedCount < 0 ||
                state.AcceptedCount > cellCount ||
                !math.isfinite(state.LipHeight) ||
                !math.isfinite(state.DeepestHeight))
            {
                ResetToScanPhase(ref state);
            }
        }

        private static void ResetToScanPhase(ref AnomalyBasinFloodFillState state)
        {
            state.SeedIndex = 0;
            state.HeapCount = 0;
            state.AcceptedCount = 0;
            state.ClearIndex = 0;
            state.Phase = PhaseScanCandidate;
            state.LipHeight = 0f;
            state.DeepestHeight = 0f;
            state.FoundSpill = 0;
            state.OpenBoundary = 0;
            state.Initialized = 1;
        }

        private bool TryContinueFlood(ref AnomalyBasinFloodFillState state, ref int operations, int operationBudget)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            int cellCount = width * height;
            int maxFloodCells = math.min(Settings.MaxFloodCells, cellCount);
            float epsilon = Settings.EqualHeightEpsilon;

            while (state.HeapCount > 0 && state.AcceptedCount < maxFloodCells)
            {
                if (++operations >= operationBudget)
                    return false;

                int cellIndex = HeapPop(ref state.HeapCount);
                float cellHeight = Heightmap[cellIndex];
                AcceptedCells[state.AcceptedCount++] = cellIndex;
                state.LipHeight = math.max(state.LipHeight, cellHeight);

                int x = cellIndex % width;
                int z = cellIndex / width;
                if (IsOpenBoundaryEscape(
                        x,
                        z,
                        width,
                        height,
                        cellHeight,
                        state.DeepestHeight,
                        Settings.MinimumDepthMeters,
                        epsilon))
                {
                    state.OpenBoundary = 1;
                    break;
                }

                if (cellHeight > state.DeepestHeight + epsilon &&
                    HasUnvisitedLowerNeighbor(cellIndex, cellHeight, state.Stamp, epsilon))
                {
                    state.LipHeight = cellHeight;
                    state.FoundSpill = 1;
                    break;
                }

                for (int dz = -1; dz <= 1; dz++)
                {
                    int nz = z + dz;
                    if (nz < 0 || nz >= height)
                        continue;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int nx = x + dx;
                        if (nx < 0 || nx >= width)
                            continue;

                        int neighborIndex = nx + nz * width;
                        if (VisitedStamp[neighborIndex] == state.Stamp || BasinMask[neighborIndex] != 0)
                            continue;

                        float neighborHeight = Heightmap[neighborIndex];
                        if (!math.isfinite(neighborHeight))
                            continue;

                        MarkVisited(neighborIndex, state.Stamp);
                        HeapPush(ref state.HeapCount, neighborIndex);
                    }
                }
            }

            return true;
        }

        private void FinalizeCandidate(ref AnomalyBasinFloodFillState state)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            int cellCount = width * height;
            int maxFloodCells = math.min(Settings.MaxFloodCells, cellCount);
            float epsilon = Settings.EqualHeightEpsilon;
            int seedIndex = state.SeedIndex;

            if (state.OpenBoundary != 0 ||
                state.AcceptedCount <= 0 ||
                (state.AcceptedCount >= maxFloodCells && state.HeapCount > 0 && state.FoundSpill == 0))
            {
                BasinRecords[seedIndex] = default;
                return;
            }

            float depth = state.LipHeight - state.DeepestHeight;
            if (depth + epsilon < Settings.MinimumDepthMeters)
            {
                BasinRecords[seedIndex] = default;
                return;
            }

            int deepestIndex = seedIndex;
            int deepestX = seedIndex % width;
            int deepestZ = seedIndex / width;
            int minX = width;
            int minZ = height;
            int maxX = 0;
            int maxZ = 0;
            int maskedCount = 0;
            float deepestHeight = state.DeepestHeight;

            for (int i = 0; i < state.AcceptedCount; i++)
            {
                int cellIndex = AcceptedCells[i];
                float cellHeight = Heightmap[cellIndex];
                if (!IsBelowLip(cellHeight, state.LipHeight, epsilon))
                    continue;

                int x = cellIndex % width;
                int z = cellIndex / width;
                BasinMask[cellIndex] = 1;
                minX = math.min(minX, x);
                minZ = math.min(minZ, z);
                maxX = math.max(maxX, x);
                maxZ = math.max(maxZ, z);
                maskedCount++;

                if (cellHeight < deepestHeight - epsilon)
                {
                    deepestHeight = cellHeight;
                    deepestIndex = cellIndex;
                    deepestX = x;
                    deepestZ = z;
                }
            }

            if (maskedCount <= 0)
            {
                BasinRecords[seedIndex] = default;
                return;
            }

            float cellSize = Settings.CellSizeMeters;
            BasinRecords[seedIndex] = new AnomalyBasinRecord
            {
                BasinId = state.BasinId,
                DeepestIndex = deepestIndex,
                DeepestX = deepestX,
                DeepestZ = deepestZ,
                MinX = minX,
                MinZ = minZ,
                MaxX = maxX,
                MaxZ = maxZ,
                CellCount = maskedCount,
                DeepestHeight = deepestHeight,
                LipHeight = state.LipHeight,
                AreaMetersSq = maskedCount * cellSize * cellSize,
                Valid = 1
            };
            state.BasinId++;
        }

        private void Defer(AnomalyBasinFloodFillState state, int operations)
        {
            SliceStatus[1] = operations;
            if (!TryEnqueueDeferredStateBounded(DeferredStates, DeferredStateBudget, in state))
            {
                SliceStatus[0] = StatusDeferredOverflow;
                return;
            }

            SliceStatus[0] = StatusDeferred;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool TryEnqueueDeferredStateBounded(
            NativeQueue<AnomalyBasinFloodFillState> queue,
            NativeArray<int> budgetArray,
            in AnomalyBasinFloodFillState state)
        {
            if (!budgetArray.IsCreated || budgetArray.Length < 2)
                return false;

            int* budget = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(budgetArray);
            int remainingAfterClaim = Interlocked.Decrement(ref budget[0]);
            if (remainingAfterClaim < 0)
            {
                Interlocked.Increment(ref budget[1]);
                return false;
            }

            queue.Enqueue(state);
            return true;
        }

        private bool HasUnvisitedLowerNeighbor(int cellIndex, float cellHeight, int stamp, float epsilon)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            int x = cellIndex % width;
            int z = cellIndex / width;

            for (int dz = -1; dz <= 1; dz++)
            {
                int nz = z + dz;
                if (nz < 0 || nz >= height)
                    continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    int nx = x + dx;
                    if (nx < 0 || nx >= width)
                        continue;

                    int neighborIndex = nx + nz * width;
                    if (VisitedStamp[neighborIndex] == stamp)
                        continue;

                    float neighborHeight = Heightmap[neighborIndex];
                    if (math.isfinite(neighborHeight) && neighborHeight < cellHeight - epsilon)
                        return true;
                }
            }

            return false;
        }

        private void HeapPush(ref int heapCount, int index)
        {
            int heapIndex = heapCount++;
            FloodHeap[heapIndex] = index;

            while (heapIndex > 0)
            {
                int parent = (heapIndex - 1) >> 1;
                if (Heightmap[FloodHeap[parent]] <= Heightmap[index])
                    break;

                FloodHeap[heapIndex] = FloodHeap[parent];
                heapIndex = parent;
            }

            FloodHeap[heapIndex] = index;
        }

        private int HeapPop(ref int heapCount)
        {
            int result = FloodHeap[0];
            int last = FloodHeap[--heapCount];
            if (heapCount <= 0)
                return result;

            int heapIndex = 0;
            while (true)
            {
                int left = heapIndex * 2 + 1;
                if (left >= heapCount)
                    break;

                int right = left + 1;
                int child = right < heapCount && Heightmap[FloodHeap[right]] < Heightmap[FloodHeap[left]] ? right : left;
                if (Heightmap[FloodHeap[child]] >= Heightmap[last])
                    break;

                FloodHeap[heapIndex] = FloodHeap[child];
                heapIndex = child;
            }

            FloodHeap[heapIndex] = last;
            return result;
        }

        private void MarkVisited(int index, int stamp)
        {
            VisitedStamp[index] = stamp;
        }

        private static bool IsOpenBoundaryEscape(
            int x,
            int z,
            int width,
            int height,
            float cellHeight,
            float deepestHeight,
            float minimumDepthMeters,
            float epsilon)
        {
            return (x <= 0 || z <= 0 || x >= width - 1 || z >= height - 1) &&
                   cellHeight - deepestHeight + epsilon < minimumDepthMeters;
        }

        private static bool IsBelowLip(float cellHeight, float lipHeight, float epsilon)
        {
            return math.isfinite(cellHeight) && cellHeight < lipHeight - epsilon;
        }

        private static bool NextStamp(ref int stamp)
        {
            if (stamp == int.MaxValue)
                return false;

            stamp++;
            return true;
        }
    }

}
