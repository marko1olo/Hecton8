using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only deterministic harness for the isolated anomaly basin jobs.
    /// </summary>
    public static class AnomalyTestHarness
    {
        private const int Resolution = 17;
        private const int PixelCount = Resolution * Resolution;
        private const int Center = Resolution / 2;
        private const float ExpectedLipHeight = 8f;
        private const int CliffSdfWidth = 9;
        private const int CliffSdfHeight = 9;
        private const int CliffSdfDepth = 9;
        private const int CliffVoxelCount = CliffSdfWidth * CliffSdfHeight * CliffSdfDepth;
        private const int CliffCenter = CliffSdfWidth / 2;
        private const int FeatureResolution = 9;
        private const int FeaturePixelCount = FeatureResolution * FeatureResolution;
        private const int FeaturePillarX = 2;
        private const int FeaturePillarZ = 2;
        private const int FeatureFissureX = 6;
        private const int FeatureFissureZ = 6;
        private const int SeamSdfWidth = 5;
        private const int SeamSdfHeight = 5;
        private const int SeamSdfDepth = 5;
        private const int SeamVoxelCount = SeamSdfWidth * SeamSdfHeight * SeamSdfDepth;
        private const int SeamTerrainCount = SeamSdfWidth * SeamSdfDepth;
        private const int SdfInjectionWidth = 7;
        private const int SdfInjectionHeight = 8;
        private const int SdfInjectionDepth = 7;
        private const int SdfInjectionVoxelCount = SdfInjectionWidth * SdfInjectionHeight * SdfInjectionDepth;
        private const string NativeMemoryOwner = nameof(AnomalyTestHarness);
        private const string HeightmapLabel = "heightmap";
        private const string BasinMaskLabel = "basinMask";
        private const string CandidateMaskLabel = "candidateMask";
        private const string BasinRecordsLabel = "basinRecords";
        private const string FloodHeapLabel = "floodHeap";
        private const string VisitedStampLabel = "visitedStamp";
        private const string AcceptedCellsLabel = "acceptedCells";
        private const string CliffInputSdfLabel = "cliffInputSdf";
        private const string CliffOutputSdfLabel = "cliffOutputSdf";
        private const string FeatureHeightmapLabel = "featureHeightmap";
        private const string FeatureRecordsLabel = "featureRecords";
        private const string FeatureFissureMaskLabel = "featureFissureMask";
        private const string SeamTerrainHeightsLabel = "seamTerrainHeights";
        private const string SeamSdfLabel = "seamSdf";
        private const string PillarSdfLabel = "pillarSdf";
        private const string FissureSdfLabel = "fissureSdf";
        private const string FissureInfluenceLabel = "fissureInfluence";

        /// <summary>
        /// Generates a mathematically exact Chebyshev bowl and validates basin lip and center.
        /// </summary>
        [MenuItem("Tools/Hecton/Dev/Terrain/Run Anomaly Test Harness")]
        public static void Run()
        {
            RunPerfectBowlAssertion();
            RunCliffOverhangAssertion();
            RunFeatureDetectionAssertion();
            RunSeamStitchAssertion();
            RunSdfInjectionAssertion();
            Debug.Log("ANOMALY_TEST_HARNESS_PASS");
        }

        /// <summary>
        /// Runs the deterministic bowl assertion without writing assets.
        /// </summary>
        public static void RunPerfectBowlAssertion()
        {
            NativeArray<float> heightmap = default;
            NativeArray<byte> basinMask = default;
            NativeArray<byte> candidateMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            NativeArray<int> floodHeap = default;
            NativeArray<int> visitedStamp = default;
            NativeArray<int> acceptedCells = default;

            try
            {
                // COLD ALLOC: NativeArray anomaly buffers[PixelCount] — deterministic editor anomaly validation — owner: AnomalyTestHarness
                heightmap = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                basinMask = new NativeArray<byte>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                candidateMask = new NativeArray<byte>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                basinRecords = new NativeArray<AnomalyBasinRecord>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                floodHeap = new NativeArray<int>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                visitedStamp = new NativeArray<int>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                acceptedCells = new NativeArray<int>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobBuffers(heightmap, basinMask, candidateMask, basinRecords, floodHeap, visitedStamp, acceptedCells);

                FillPerfectBowl(heightmap);
                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = 1f,
                    MinimumDepthMeters = 1f,
                    MaxFloodCells = PixelCount,
                    EqualHeightEpsilon = 0.000001f
                };

                JobHandle handle = HectonAnomalyEngine.ScheduleClosedBasinDetection(
                    heightmap,
                    basinMask,
                    basinRecords,
                    candidateMask,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    settings);

                // COLD SYNC JOB: Editor test harness must inspect deterministic results immediately.
                handle.Complete();

                AnomalyBasinRecord record = FindFirstValidRecord(basinRecords);
                Assert.IsTrue(record.Valid == 1, "Closed basin detector did not emit a valid bowl basin.");
                Assert.IsTrue(record.DeepestX == Center, "Detected basin center X is not exact.");
                Assert.IsTrue(record.DeepestZ == Center, "Detected basin center Z is not exact.");
                Assert.AreEqual(0f, record.DeepestHeight, "Detected basin depth is not exact.");
                Assert.AreEqual(ExpectedLipHeight, record.LipHeight, "Detected basin lip height is not exact.");
                Assert.IsTrue(record.CellCount == PixelCount, "Detected basin mask cell count is not exact.");
                Assert.IsTrue(basinMask[Center + Center * Resolution] == 1, "Detected basin mask does not include the exact center.");
            }
            finally
            {
                DisposeTracked(ref heightmap);
                DisposeTracked(ref basinMask);
                DisposeTracked(ref candidateMask);
                DisposeTracked(ref basinRecords);
                DisposeTracked(ref floodHeap);
                DisposeTracked(ref visitedStamp);
                DisposeTracked(ref acceptedCells);
            }
        }

        /// <summary>
        /// Runs a deterministic stitched-cliff SDF assertion for lateral overhang displacement.
        /// </summary>
        public static void RunCliffOverhangAssertion()
        {
            NativeArray<float> inputSdf = default;
            NativeArray<float> outputSdf = default;

            try
            {
                // COLD ALLOC: NativeArray cliff SDF buffers[CliffVoxelCount] — deterministic editor anomaly validation — owner: AnomalyTestHarness
                inputSdf = new NativeArray<float>(CliffVoxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                outputSdf = new NativeArray<float>(CliffVoxelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(inputSdf, NativeMemoryOwner, CliffInputSdfLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(outputSdf, NativeMemoryOwner, CliffOutputSdfLabel, NativeAllocationLifetime.TempJob);

                FillVerticalCliffSdf(inputSdf);
                JobHandle handle = HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise(
                    inputSdf,
                    outputSdf,
                    CliffSdfWidth,
                    CliffSdfHeight,
                    CliffSdfDepth,
                    1f,
                    0.5f,
                    0.73f,
                    1f,
                    1f);

                // COLD SYNC JOB: Editor test harness must inspect deterministic SDF output immediately.
                handle.Complete();

                int changedInteriorCells = 0;
                for (int z = 0; z < CliffSdfDepth; z++)
                {
                    for (int y = 0; y < CliffSdfHeight; y++)
                    {
                        for (int x = 0; x < CliffSdfWidth; x++)
                        {
                            int index = FlatCliffIndex(x, y, z);
                            bool boundary =
                                x == 0 ||
                                y == 0 ||
                                z == 0 ||
                                x == CliffSdfWidth - 1 ||
                                y == CliffSdfHeight - 1 ||
                                z == CliffSdfDepth - 1;

                            if (boundary)
                            {
                                Assert.AreEqual(inputSdf[index], outputSdf[index], "Overhang displacement modified a boundary SDF cell.");
                                continue;
                            }

                            if (math.abs(outputSdf[index] - inputSdf[index]) > 0.0001f)
                                changedInteriorCells++;
                        }
                    }
                }

                Assert.IsTrue(changedInteriorCells > 0, "Cliff overhang SDF noise did not modify any steep interior cells.");
            }
            finally
            {
                DisposeTracked(ref inputSdf);
                DisposeTracked(ref outputSdf);
            }
        }

        /// <summary>
        /// Runs a deterministic ridge feature assertion for pillar anchors and fissure masks.
        /// </summary>
        public static void RunFeatureDetectionAssertion()
        {
            NativeArray<float> heightmap = default;
            NativeArray<AnomalyFeatureRecord> featureRecords = default;
            NativeArray<byte> fissureMask = default;

            try
            {
                // COLD ALLOC: NativeArray feature buffers[FeaturePixelCount] - deterministic editor anomaly feature validation - owner: AnomalyTestHarness
                heightmap = new NativeArray<float>(FeaturePixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                featureRecords = new NativeArray<AnomalyFeatureRecord>(FeaturePixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                fissureMask = new NativeArray<byte>(FeaturePixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(heightmap, NativeMemoryOwner, FeatureHeightmapLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(featureRecords, NativeMemoryOwner, FeatureRecordsLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(fissureMask, NativeMemoryOwner, FeatureFissureMaskLabel, NativeAllocationLifetime.TempJob);

                int pillarIndex = FeaturePillarX + FeaturePillarZ * FeatureResolution;
                int fissureIndex = FeatureFissureX + FeatureFissureZ * FeatureResolution;
                heightmap[pillarIndex] = 80f;
                heightmap[fissureIndex] = -80f;
                uint packedFissure = HectonAnomalyEngine.PackBiomeInfluenceCell(79, 0, 255, 0x14);

                var settings = new AnomalyRidgeDetectionSettings
                {
                    Width = FeatureResolution,
                    Height = FeatureResolution,
                    CellSizeMeters = 2f,
                    OriginAup = new double3(100.0, 5.0, 200.0),
                    MinimumPillarProminenceMeters = 20f,
                    MinimumPillarRidgeArms = 3,
                    MinimumFissureDepthMeters = 20f,
                    EqualHeightEpsilon = 0.000001f,
                    FissureInfluencePacked = packedFissure
                };

                JobHandle handle = HectonAnomalyEngine.ScheduleRidgeFeatureDetection(
                    heightmap,
                    featureRecords,
                    fissureMask,
                    settings);

                // COLD SYNC JOB: Editor test harness must inspect deterministic feature output immediately.
                handle.Complete();

                AnomalyFeatureRecord pillar = featureRecords[pillarIndex];
                Assert.IsTrue(pillar.Valid == 1, "Pillar feature detector did not emit a valid record.");
                Assert.IsTrue(pillar.Kind == (byte)AnomalyFeatureKind.ChthonicPillar, "Pillar feature kind mismatch.");
                Assert.AreEqual(104.0, pillar.AupX, "Pillar AUP X mismatch.");
                Assert.AreEqual(85.0, pillar.AupY, "Pillar AUP Y mismatch.");
                Assert.AreEqual(204.0, pillar.AupZ, "Pillar AUP Z mismatch.");

                AnomalyFeatureRecord fissure = featureRecords[fissureIndex];
                Assert.IsTrue(fissure.Valid == 1, "Fissure feature detector did not emit a valid record.");
                Assert.IsTrue(fissure.Kind == (byte)AnomalyFeatureKind.DeepFissure, "Fissure feature kind mismatch.");
                Assert.IsTrue(fissureMask[fissureIndex] == 1, "Fissure mask was not written.");
                Assert.AreEqual(packedFissure, fissure.BiomeInfluencePacked, "Fissure packed biome influence mismatch.");
            }
            finally
            {
                DisposeTracked(ref heightmap);
                DisposeTracked(ref featureRecords);
                DisposeTracked(ref fissureMask);
            }
        }

        /// <summary>
        /// Runs a deterministic terrain-to-SDF seam assertion for exact zero-density top cells.
        /// </summary>
        public static void RunSeamStitchAssertion()
        {
            NativeArray<float> terrainHeights = default;
            NativeArray<float> sdf = default;

            try
            {
                // COLD ALLOC: NativeArray seam buffers[SeamVoxelCount] - deterministic editor SDF seam validation - owner: AnomalyTestHarness
                terrainHeights = new NativeArray<float>(SeamTerrainCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sdf = new NativeArray<float>(SeamVoxelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(terrainHeights, NativeMemoryOwner, SeamTerrainHeightsLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(sdf, NativeMemoryOwner, SeamSdfLabel, NativeAllocationLifetime.TempJob);

                for (int i = 0; i < terrainHeights.Length; i++)
                    terrainHeights[i] = 2f;

                JobHandle handle = HectonAnomalyEngine.SnapSDFToTerrain(
                    terrainHeights,
                    SeamSdfWidth,
                    SeamSdfDepth,
                    1f,
                    new double3(0.0, 0.0, 0.0),
                    sdf,
                    SeamSdfWidth,
                    SeamSdfHeight,
                    SeamSdfDepth,
                    1f,
                    new double3(0.0, 0.0, 0.0));

                // COLD SYNC JOB: Editor test harness must inspect deterministic SDF seam output immediately.
                handle.Complete();

                for (int z = 0; z < SeamSdfDepth; z++)
                {
                    for (int x = 0; x < SeamSdfWidth; x++)
                    {
                        Assert.AreEqual(1f, sdf[FlatSdfIndex(x, 1, z, SeamSdfWidth, SeamSdfHeight)], "SDF below terrain seam is not positive solid density.");
                        Assert.AreEqual(0f, sdf[FlatSdfIndex(x, 2, z, SeamSdfWidth, SeamSdfHeight)], "SDF terrain seam top cell is not exact zero density.");
                        Assert.AreEqual(-1f, sdf[FlatSdfIndex(x, 3, z, SeamSdfWidth, SeamSdfHeight)], "SDF above terrain seam is not negative air density.");
                    }
                }
            }
            finally
            {
                DisposeTracked(ref terrainHeights);
                DisposeTracked(ref sdf);
            }
        }

        /// <summary>
        /// Runs deterministic pillar union and fissure subtraction assertions against caller-owned SDF buffers.
        /// </summary>
        public static void RunSdfInjectionAssertion()
        {
            NativeArray<float> pillarSdf = default;
            NativeArray<float> fissureSdf = default;
            NativeArray<uint> fissureInfluence = default;

            try
            {
                // COLD ALLOC: NativeArray SDF injection buffers[SdfInjectionVoxelCount] - deterministic editor SDF anomaly validation - owner: AnomalyTestHarness
                pillarSdf = new NativeArray<float>(SdfInjectionVoxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                fissureSdf = new NativeArray<float>(SdfInjectionVoxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                fissureInfluence = new NativeArray<uint>(SdfInjectionVoxelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(pillarSdf, NativeMemoryOwner, PillarSdfLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(fissureSdf, NativeMemoryOwner, FissureSdfLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(fissureInfluence, NativeMemoryOwner, FissureInfluenceLabel, NativeAllocationLifetime.TempJob);

                for (int i = 0; i < SdfInjectionVoxelCount; i++)
                {
                    pillarSdf[i] = -10f;
                    fissureSdf[i] = 10f;
                }

                JobHandle pillarHandle = HectonAnomalyEngine.InjectMegaPillarSDF(
                    pillarSdf,
                    SdfInjectionWidth,
                    SdfInjectionHeight,
                    SdfInjectionDepth,
                    1f,
                    new double3(0.0, 0.0, 0.0),
                    new double3(3.0, 1.0, 3.0),
                    1f,
                    4f,
                    0f,
                    0.01f);

                uint packedFissure = HectonAnomalyEngine.PackBiomeInfluenceCell(79, 0, 255, 0x14);
                JobHandle fissureHandle = HectonAnomalyEngine.InjectDeepFissureSDF(
                    fissureSdf,
                    fissureInfluence,
                    SdfInjectionWidth,
                    SdfInjectionHeight,
                    SdfInjectionDepth,
                    1f,
                    new double3(0.0, 0.0, 0.0),
                    new double3(3.0, 6.0, 3.0),
                    new float2(1f, 0f),
                    1f,
                    1f,
                    4f,
                    packedFissure,
                    pillarHandle);

                // COLD SYNC JOB: Editor test harness must inspect deterministic SDF injection output immediately.
                fissureHandle.Complete();

                int pillarCoreIndex = FlatSdfIndex(3, 3, 3, SdfInjectionWidth, SdfInjectionHeight);
                Assert.IsTrue(pillarSdf[pillarCoreIndex] > 0f, "Pillar SDF injection did not create positive solid density at the pillar core.");

                int fissureCoreIndex = FlatSdfIndex(3, 4, 3, SdfInjectionWidth, SdfInjectionHeight);
                int fissureOutsideIndex = FlatSdfIndex(0, 0, 0, SdfInjectionWidth, SdfInjectionHeight);
                Assert.IsTrue(fissureSdf[fissureCoreIndex] < 0f, "Deep fissure SDF injection did not carve negative density at the trench core.");
                Assert.AreEqual(packedFissure, fissureInfluence[fissureCoreIndex], "Deep fissure biome influence was not written at the trench core.");
                Assert.AreEqual(10f, fissureSdf[fissureOutsideIndex], "Deep fissure SDF injection modified an outside cell.");
                Assert.AreEqual(0u, fissureInfluence[fissureOutsideIndex], "Deep fissure biome influence modified an outside cell.");
            }
            finally
            {
                DisposeTracked(ref pillarSdf);
                DisposeTracked(ref fissureSdf);
                DisposeTracked(ref fissureInfluence);
            }
        }

        private static void FillPerfectBowl(NativeArray<float> heightmap)
        {
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int dx = math.abs(x - Center);
                    int dz = math.abs(z - Center);
                    heightmap[x + z * Resolution] = math.max(dx, dz);
                }
            }
        }

        private static void FillVerticalCliffSdf(NativeArray<float> sdf)
        {
            for (int z = 0; z < CliffSdfDepth; z++)
            {
                for (int y = 0; y < CliffSdfHeight; y++)
                {
                    for (int x = 0; x < CliffSdfWidth; x++)
                    {
                        sdf[FlatCliffIndex(x, y, z)] = x - CliffCenter;
                    }
                }
            }
        }

        private static int FlatCliffIndex(int x, int y, int z)
        {
            return x + y * CliffSdfWidth + z * CliffSdfWidth * CliffSdfHeight;
        }

        private static int FlatSdfIndex(int x, int y, int z, int width, int height)
        {
            return x + y * width + z * width * height;
        }

        private static AnomalyBasinRecord FindFirstValidRecord(NativeArray<AnomalyBasinRecord> records)
        {
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].Valid != 0)
                    return records[i];
            }

            return default;
        }

        private static void RegisterTempJobBuffers(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<byte> candidateMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells)
        {
            NativeMemorySentinel.RegisterNativeArray(heightmap, NativeMemoryOwner, HeightmapLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(basinMask, NativeMemoryOwner, BasinMaskLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(candidateMask, NativeMemoryOwner, CandidateMaskLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(basinRecords, NativeMemoryOwner, BasinRecordsLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(floodHeap, NativeMemoryOwner, FloodHeapLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(visitedStamp, NativeMemoryOwner, VisitedStampLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(acceptedCells, NativeMemoryOwner, AcceptedCellsLabel, NativeAllocationLifetime.TempJob);
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }
    }
}
