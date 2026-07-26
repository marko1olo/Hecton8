using Hecton8.Core;
using Hecton8.World;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private const float PerfectBowlStepMeters = 8f;
        private const float ExpectedLipHeight = 64f;
        private const int ExpectedBowlMaskedCells = (Resolution - 2) * (Resolution - 2);
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
        private const string BrineBoundsLabel = "brineBounds";
        private const string FloodHeapLabel = "floodHeap";
        private const string VisitedStampLabel = "visitedStamp";
        private const string AcceptedCellsLabel = "acceptedCells";
        private const string SliceStatusLabel = "sliceStatus";
        private const string DeferredStateBudgetLabel = "deferredStateBudget";
        private const string PendingFloodStatesLabel = "pendingFloodStates";
        private const string DeferredFloodStatesLabel = "deferredFloodStates";
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
            RunAnomalySettingsSanitizationAssertion();
            RunPerfectBowlAssertion();
            RunOpenEdgeBowlRejectionAssertion();
            RunBrineBoundsJobRecordIntegrityAssertion();
            RunBrineToxicMudGridAssertion();
            RunBrinePoolGeneratorStaleRootAssertion();
            RunTimeSlicedPerfectBowlAssertion();
            RunTimeSlicedStampOverflowAssertion();
            RunTimeSlicedCorruptStateRecoveryAssertion();
            RunCliffOverhangAssertion();
            RunFeatureDetectionAssertion();
            RunSeamStitchAssertion();
            RunSdfInjectionAssertion();
            H8Debug.Log("ANOMALY_TEST_HARNESS_PASS");
        }

        /// <summary>
        /// Validates NaN/Infinity authoring values are replaced before they can enter Burst jobs.
        /// </summary>
        public static void RunAnomalySettingsSanitizationAssertion()
        {
            var basin = new AnomalyBasinDetectionSettings
            {
                Width = -7,
                Height = 0,
                CellSizeMeters = float.NaN,
                MinimumDepthMeters = float.PositiveInfinity,
                MaxFloodCells = -4,
                EqualHeightEpsilon = float.NaN,
                MaxFloodFillOperationsPerSlice = -16
            }.Sanitized();

            Assert.AreEqual(1, basin.Width, "Basin settings did not clamp width.");
            Assert.AreEqual(1, basin.Height, "Basin settings did not clamp height.");
            Assert.AreEqual(0.001f, basin.CellSizeMeters, "Basin settings accepted a non-finite cell size.");
            Assert.AreEqual(0f, basin.MinimumDepthMeters, "Basin settings accepted a non-finite minimum depth.");
            Assert.AreEqual(8, basin.MaxFloodCells, "Basin settings did not clamp flood cell budget.");
            Assert.AreEqual(0.000001f, basin.EqualHeightEpsilon, "Basin settings accepted a non-finite height epsilon.");
            Assert.AreEqual(64, basin.MaxFloodFillOperationsPerSlice, "Basin settings did not clamp slice operation budget.");

            var ridge = new AnomalyRidgeDetectionSettings
            {
                Width = 0,
                Height = -3,
                CellSizeMeters = float.NaN,
                OriginAup = new double3(double.NaN, 8.0, 16.0),
                MinimumPillarProminenceMeters = float.NaN,
                MinimumPillarRidgeArms = -1,
                MinimumFissureDepthMeters = float.PositiveInfinity,
                EqualHeightEpsilon = float.NaN,
                RequireTectonicBoundary = 2,
                TectonicBoundaryFrequency = float.NaN,
                MinimumTectonicBoundaryMask = float.NaN
            }.Sanitized();

            Assert.AreEqual(1, ridge.Width, "Ridge settings did not clamp width.");
            Assert.AreEqual(1, ridge.Height, "Ridge settings did not clamp height.");
            Assert.AreEqual(0.001f, ridge.CellSizeMeters, "Ridge settings accepted a non-finite cell size.");
            Assert.IsTrue(math.all(ridge.OriginAup == double3.zero), "Ridge settings accepted a non-finite AUP origin.");
            Assert.AreEqual(0f, ridge.MinimumPillarProminenceMeters, "Ridge settings accepted non-finite pillar prominence.");
            Assert.AreEqual(3, ridge.MinimumPillarRidgeArms, "Ridge settings did not clamp ridge arms.");
            Assert.AreEqual(0f, ridge.MinimumFissureDepthMeters, "Ridge settings accepted non-finite fissure depth.");
            Assert.AreEqual(0.000001f, ridge.EqualHeightEpsilon, "Ridge settings accepted a non-finite height epsilon.");
            Assert.AreEqual((byte)1, ridge.RequireTectonicBoundary, "Ridge settings did not normalize tectonic boundary flag.");
            Assert.AreEqual(0.0001f, ridge.TectonicBoundaryFrequency, "Ridge settings accepted non-finite tectonic frequency.");
            Assert.AreEqual(0.55f, ridge.MinimumTectonicBoundaryMask, "Ridge settings accepted non-finite tectonic mask threshold.");
        }

        /// <summary>
        /// Validates brine bounds resolution rejects damaged basin records before mesh generation sees them.
        /// </summary>
        public static void RunBrineBoundsJobRecordIntegrityAssertion()
        {
            NativeArray<byte> basinMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            NativeArray<AnomalyBrinePoolBounds> bounds = default;

            try
            {
                // COLD ALLOC: NativeArray brine bound buffers[4] - deterministic editor brine bound validation - owner: AnomalyTestHarness
                basinMask = AllocateTrackedTempJobArray<byte>(4, BasinMaskLabel, NativeArrayOptions.ClearMemory);
                basinRecords = AllocateTrackedTempJobArray<AnomalyBasinRecord>(1, BasinRecordsLabel, NativeArrayOptions.ClearMemory);
                bounds = AllocateTrackedTempJobArray<AnomalyBrinePoolBounds>(1, BrineBoundsLabel, NativeArrayOptions.ClearMemory);

                basinMask[0] = 1;
                basinRecords[0] = new AnomalyBasinRecord
                {
                    BasinId = 1,
                    MinX = 0,
                    MinZ = 0,
                    MaxX = 1,
                    MaxZ = 1,
                    CellCount = 2,
                    DeepestHeight = 0f,
                    LipHeight = 60f,
                    Valid = 1
                };

                var job = new ResolveBrinePoolBoundsJob
                {
                    BasinMask = basinMask,
                    BasinRecords = basinRecords,
                    Bounds = bounds,
                    Width = 2,
                    Height = 2
                };
                JobHandle handle = job.Schedule(1, 1);
                // COLD SYNC JOB: Editor harness must inspect deterministic brine bound rejection immediately.
                handle.Complete();
                Assert.AreEqual((byte)0, bounds[0].Valid, "Brine bounds accepted a record whose mask count did not match CellCount.");

                basinRecords[0] = new AnomalyBasinRecord
                {
                    BasinId = 1,
                    MinX = 0,
                    MinZ = 0,
                    MaxX = 0,
                    MaxZ = 0,
                    CellCount = 1,
                    DeepestHeight = 10f,
                    LipHeight = 10f,
                    Valid = 1
                };
                handle = job.Schedule(1, 1);
                // COLD SYNC JOB: Editor harness must inspect deterministic brine bound depth rejection immediately.
                handle.Complete();
                Assert.AreEqual((byte)0, bounds[0].Valid, "Brine bounds accepted a record with no positive brine depth.");

                basinRecords[0] = new AnomalyBasinRecord
                {
                    BasinId = 1,
                    MinX = 0,
                    MinZ = 0,
                    MaxX = 0,
                    MaxZ = 0,
                    CellCount = 1,
                    DeepestHeight = 0f,
                    LipHeight = 60f,
                    Valid = 1
                };
                handle = job.Schedule(1, 1);
                // COLD SYNC JOB: Editor harness must inspect deterministic brine bound acceptance immediately.
                handle.Complete();
                Assert.AreEqual((byte)1, bounds[0].Valid, "Brine bounds rejected a valid one-cell basin.");
                Assert.AreEqual(1, bounds[0].MaskedCount, "Brine bounds did not report exact masked cell count.");
                Assert.AreEqual(60f, bounds[0].LipHeight, "Brine bounds did not preserve exact lip height.");
            }
            finally
            {
                DisposeTracked(ref basinMask);
                DisposeTracked(ref basinRecords);
                DisposeTracked(ref bounds);
            }
        }

        /// <summary>
        /// Validates toxic mud registry id, dimension, AUP, and unregister invariants.
        /// </summary>
        public static void RunBrineToxicMudGridAssertion()
        {
            const int validCellId = 990001;
            const int invalidCellId = 990002;
            const int secondCellId = 990003;
            AbsoluteUniversePosition center = AbsoluteUniversePosition.FromAbsolutePosition(new double3(100.0, 20.0, 200.0));
            AbsoluteUniversePosition secondCenter = AbsoluteUniversePosition.FromAbsolutePosition(new double3(130.0, 20.0, 200.0));
            AbsoluteUniversePosition aboveSurface = AbsoluteUniversePosition.FromAbsolutePosition(new double3(100.0, 21.0, 200.0));
            AbsoluteUniversePosition belowVolume = AbsoluteUniversePosition.FromAbsolutePosition(new double3(100.0, 14.0, 200.0));
            AbsoluteUniversePosition edgeOnEllipse = AbsoluteUniversePosition.FromAbsolutePosition(new double3(105.0, 20.0, 200.0));
            AbsoluteUniversePosition outsideNear = AbsoluteUniversePosition.FromAbsolutePosition(new double3(105.25, 20.0, 200.0));
            AbsoluteUniversePosition outsideXZ = AbsoluteUniversePosition.FromAbsolutePosition(new double3(110.0, 20.0, 200.0));
            AbsoluteUniversePosition invalidLocalAup = center;
            invalidLocalAup.LocalY = float.NaN;

            HectonBrineToxicMudGrid.ClearForEditorTests();
            try
            {
                Assert.AreEqual(0, HectonBrineToxicMudGrid.RegisteredCellCount, "Brine toxic mud grid did not start empty.");

                HectonBrineToxicMudGrid.RegisterCell(0, in center, 10f, 12f, 5f);
                HectonBrineToxicMudGrid.RegisterCell(invalidCellId, in center, 0f, 12f, 5f);
                HectonBrineToxicMudGrid.RegisterCell(invalidCellId, in center, 10f, -1f, 5f);
                HectonBrineToxicMudGrid.RegisterCell(invalidCellId, in center, 10f, 12f, 0f);
                HectonBrineToxicMudGrid.RegisterCell(invalidCellId, new Vector3(float.NaN, 20f, 200f), 10f, 12f, 5f);
                Assert.AreEqual(0, HectonBrineToxicMudGrid.RegisteredCellCount, "Brine toxic mud grid accepted an invalid cell.");

                HectonBrineToxicMudGrid.RegisterCell(validCellId, in center, 10f, 12f, 5f);
                Assert.AreEqual(1, HectonBrineToxicMudGrid.RegisteredCellCount, "Brine toxic mud grid did not register exactly one valid cell.");
                Assert.IsTrue(HectonBrineToxicMudGrid.HasRegisteredCells, "Brine toxic mud grid did not report active cells.");
                Assert.IsTrue(HectonBrineToxicMudGrid.IsRegisteredCell(validCellId), "Brine toxic mud grid did not find the valid cell id.");
                Assert.IsFalse(HectonBrineToxicMudGrid.IsRegisteredCell(invalidCellId), "Brine toxic mud grid retained an invalid cell id.");
                Assert.IsTrue(HectonBrineToxicMudGrid.ContainsAupSubmergedPosition(in center), "Brine toxic mud grid did not contain the center AUP.");
                Assert.IsTrue(HectonBrineToxicMudGrid.OverlapsAupSubmergedVolume(in center, 0.5f, 0.5f), "Brine toxic mud grid did not overlap a centered query volume.");
                Assert.IsTrue(HectonBrineToxicMudGrid.ContainsAupSubmergedPosition(in edgeOnEllipse), "Brine toxic mud grid excluded the exact ellipse boundary.");
                Assert.IsTrue(HectonBrineToxicMudGrid.ContainsAupXZ(in aboveSurface), "Brine toxic mud grid lost the XZ footprint above the surface.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupSubmergedPosition(in aboveSurface), "Brine toxic mud grid marked an above-surface point as submerged.");
                Assert.IsFalse(HectonBrineToxicMudGrid.OverlapsAupSubmergedVolume(in aboveSurface, 0.5f, 0.25f), "Brine toxic mud grid marked an above-surface volume as submerged.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupXZ(in invalidLocalAup), "Brine toxic mud grid accepted a non-finite AUP local coordinate.");
                Assert.IsFalse(HectonBrineToxicMudGrid.OverlapsAupXZ(in center, -0.5f), "Brine toxic mud grid accepted a negative XZ query radius.");
                Assert.IsFalse(HectonBrineToxicMudGrid.OverlapsAupXZ(in center, float.PositiveInfinity), "Brine toxic mud grid accepted an infinite XZ query radius.");
                Assert.IsFalse(HectonBrineToxicMudGrid.OverlapsAupSubmergedVolume(in center, 0.5f, float.NaN), "Brine toxic mud grid accepted a NaN vertical query extent.");
                Assert.IsTrue(HectonBrineToxicMudGrid.ContainsAupSubmergedCell(validCellId, in center), "Brine toxic mud grid did not contain the center in the exact registered cell.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupSubmergedCell(invalidCellId, in center), "Brine toxic mud grid accepted an exact-cell query for an invalid id.");
                HectonBrineToxicMudGrid.RegisterCell(secondCellId, in secondCenter, 8f, 8f, 4f);
                Assert.AreEqual(2, HectonBrineToxicMudGrid.RegisteredCellCount, "Brine toxic mud grid did not hold two independent cells.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupSubmergedCell(secondCellId, in center), "Exact-cell toxic mud query leaked from a neighboring brine cell.");
                Assert.IsFalse(HectonBrineToxicMudGrid.OverlapsAupSubmergedCell(secondCellId, in center, 0.5f, 0.5f), "Exact-cell toxic mud overlap leaked from a neighboring brine cell.");
                Assert.IsTrue(HectonBrineToxicMudGrid.ContainsAupSubmergedCell(secondCellId, in secondCenter), "Exact-cell toxic mud query missed the second brine cell center.");
                Assert.IsTrue(HectonBrineToxicMudGrid.OverlapsAupSubmergedCell(secondCellId, in secondCenter, 0.5f, 0.5f), "Exact-cell toxic mud overlap missed the second brine cell center.");
                HectonBrineToxicMudGrid.UnregisterCell(secondCellId);
                Assert.AreEqual(1, HectonBrineToxicMudGrid.RegisteredCellCount, "Brine toxic mud grid did not unregister the second exact-cell fixture.");
                HectonBrineToxicMudGrid.RegisterCell(validCellId, new Vector3(float.NaN, 20f, 200f), 10f, 12f, 5f);
                Assert.AreEqual(0, HectonBrineToxicMudGrid.RegisteredCellCount, "Non-finite runtime cell update did not unregister stale brine state.");

                HectonBrineToxicMudGrid.RegisterCell(validCellId, in center, 10f, 12f, 5f);
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupSubmergedPosition(in belowVolume), "Brine toxic mud grid contained a point below the brine volume.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsRuntimeXZ(new Vector3(float.NaN, 20f, 200f)), "Brine toxic mud grid accepted a non-finite runtime XZ query.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsRuntimeSubmergedPosition(new Vector3(100f, float.PositiveInfinity, 200f)), "Brine toxic mud grid accepted a non-finite runtime submerged query.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupXZ(new float3(float.NaN, 20f, 200f)), "Brine toxic mud grid accepted a non-finite float3 XZ query.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupSubmergedPosition(new float3(100f, float.NaN, 200f)), "Brine toxic mud grid accepted a non-finite float3 submerged query.");
                Assert.IsFalse(HectonBrineToxicMudGrid.OverlapsAupXZ(in outsideNear, float.PositiveInfinity), "Brine toxic mud grid expanded an infinite XZ query radius.");
                Assert.IsFalse(HectonBrineToxicMudGrid.OverlapsAupSubmergedVolume(in outsideNear, float.PositiveInfinity, 0.5f), "Brine toxic mud grid expanded an infinite submerged query radius.");
                Assert.IsFalse(HectonBrineToxicMudGrid.ContainsAupSubmergedPosition(in outsideXZ), "Brine toxic mud grid contained a point outside the brine ellipse.");

                HectonBrineToxicMudGrid.RegisterCell(validCellId, in center, 10f, 0f, 5f);
                Assert.AreEqual(0, HectonBrineToxicMudGrid.RegisteredCellCount, "Invalid cell update did not unregister stale brine state.");
            }
            finally
            {
                HectonBrineToxicMudGrid.ClearForEditorTests();
            }
        }

        /// <summary>
        /// Validates that an existing generated brine root is cleaned even when no pools are currently tracked.
        /// </summary>
        public static void RunBrinePoolGeneratorStaleRootAssertion()
        {
            NativeArray<byte> basinMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            GameObject host = null;

            try
            {
                // COLD ALLOC: NativeArray stale brine generator buffers[1] - deterministic editor brine generator validation - owner: AnomalyTestHarness
                basinMask = AllocateTrackedTempJobArray<byte>(1, BasinMaskLabel, NativeArrayOptions.ClearMemory);
                basinRecords = AllocateTrackedTempJobArray<AnomalyBasinRecord>(1, BasinRecordsLabel, NativeArrayOptions.ClearMemory);

                // COLD ALLOC: GameObject[1] - editor brine generator host - owner: AnomalyTestHarness
                host = new GameObject("AnomalyHarnessBrineGeneratorHost");
                HectonBrinePoolMeshGenerator generator = host.AddComponent<HectonBrinePoolMeshGenerator>();

                // COLD ALLOC: GameObject[1] - stale generated brine pool root - owner: AnomalyTestHarness
                var root = new GameObject("Generated Brine Pools");
                root.transform.SetParent(host.transform, false);
                // COLD ALLOC: GameObject[1] - stale generated brine pool child - owner: AnomalyTestHarness
                var staleChild = new GameObject("StaleBrinePool");
                staleChild.transform.SetParent(root.transform, false);

                int created = generator.BuildBrinePools(basinMask, basinRecords, 1, 1, 1f, Vector3.zero);
                Assert.AreEqual(0, created, "Empty brine generator input created a brine pool.");
                Assert.AreEqual(1, host.transform.childCount, "Brine generator created a duplicate generated-pools root.");
                Assert.IsTrue(root.transform == host.transform.GetChild(0), "Brine generator did not reuse the existing generated-pools root.");
                Assert.AreEqual(HectonLayerMasks.BrineToxicity, root.layer, "Brine generator did not normalize the existing root to the brine toxicity layer.");
                Assert.AreEqual(0, root.transform.childCount, "Brine generator did not clear stale untracked generated pool children.");

                // COLD ALLOC: GameObject[1] - stale child for invalid brine rebuild rollback - owner: AnomalyTestHarness
                var invalidInputChild = new GameObject("InvalidInputStaleBrinePool");
                invalidInputChild.transform.SetParent(root.transform, false);
                created = generator.BuildBrinePools(basinMask, basinRecords, 0, 1, 1f, Vector3.zero);
                Assert.AreEqual(0, created, "Invalid brine generator input created a brine pool.");
                Assert.AreEqual(0, root.transform.childCount, "Brine generator did not clear stale children on invalid input.");

                basinMask[0] = 1;
                basinRecords[0] = new AnomalyBasinRecord
                {
                    BasinId = 1,
                    DeepestIndex = 0,
                    DeepestX = 0,
                    DeepestZ = 0,
                    MinX = 0,
                    MinZ = 0,
                    MaxX = 0,
                    MaxZ = 0,
                    CellCount = 1,
                    DeepestHeight = 10f,
                    LipHeight = 10f,
                    AreaMetersSq = 1f,
                    Valid = 1
                };
                created = generator.BuildBrinePools(basinMask, basinRecords, 1, 1, 1f, Vector3.zero);
                Assert.AreEqual(0, created, "Brine generator accepted a basin record with no positive lip depth.");
                Assert.AreEqual(0, root.transform.childCount, "Brine generator created geometry for an invalid lip record.");
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);

                DisposeTracked(ref basinMask);
                DisposeTracked(ref basinRecords);
            }
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
                // COLD ALLOC: NativeArray anomaly buffers[PixelCount] - deterministic editor anomaly validation - owner: AnomalyTestHarness
                heightmap = AllocateTrackedTempJobArray<float>(PixelCount, HeightmapLabel, NativeArrayOptions.UninitializedMemory);
                basinMask = AllocateTrackedTempJobArray<byte>(PixelCount, BasinMaskLabel, NativeArrayOptions.ClearMemory);
                candidateMask = AllocateTrackedTempJobArray<byte>(PixelCount, CandidateMaskLabel, NativeArrayOptions.ClearMemory);
                basinRecords = AllocateTrackedTempJobArray<AnomalyBasinRecord>(PixelCount, BasinRecordsLabel, NativeArrayOptions.ClearMemory);
                floodHeap = AllocateTrackedTempJobArray<int>(PixelCount, FloodHeapLabel, NativeArrayOptions.UninitializedMemory);
                visitedStamp = AllocateTrackedTempJobArray<int>(PixelCount, VisitedStampLabel, NativeArrayOptions.ClearMemory);
                acceptedCells = AllocateTrackedTempJobArray<int>(PixelCount, AcceptedCellsLabel, NativeArrayOptions.UninitializedMemory);

                FillPerfectBowl(heightmap);
                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = 1f,
                    MinimumDepthMeters = 50f,
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
                Assert.IsTrue(record.CellCount == ExpectedBowlMaskedCells, "Detected basin mask cell count is not exact.");
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
        /// Validates that an edge-open depression is rejected and does not leak basin mask cells.
        /// </summary>
        public static void RunOpenEdgeBowlRejectionAssertion()
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
                // COLD ALLOC: NativeArray open-edge basin buffers[PixelCount] - deterministic editor anomaly validation - owner: AnomalyTestHarness
                heightmap = AllocateTrackedTempJobArray<float>(PixelCount, HeightmapLabel, NativeArrayOptions.UninitializedMemory);
                basinMask = AllocateTrackedTempJobArray<byte>(PixelCount, BasinMaskLabel, NativeArrayOptions.ClearMemory);
                candidateMask = AllocateTrackedTempJobArray<byte>(PixelCount, CandidateMaskLabel, NativeArrayOptions.ClearMemory);
                basinRecords = AllocateTrackedTempJobArray<AnomalyBasinRecord>(PixelCount, BasinRecordsLabel, NativeArrayOptions.ClearMemory);
                floodHeap = AllocateTrackedTempJobArray<int>(PixelCount, FloodHeapLabel, NativeArrayOptions.UninitializedMemory);
                visitedStamp = AllocateTrackedTempJobArray<int>(PixelCount, VisitedStampLabel, NativeArrayOptions.ClearMemory);
                acceptedCells = AllocateTrackedTempJobArray<int>(PixelCount, AcceptedCellsLabel, NativeArrayOptions.UninitializedMemory);

                FillOpenEdgeBowl(heightmap);
                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = 1f,
                    MinimumDepthMeters = 50f,
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

                // COLD SYNC JOB: Editor test harness must inspect deterministic rejection immediately.
                handle.Complete();

                Assert.AreEqual(0, CountValidRecords(basinRecords), "Open-edge bowl emitted a closed basin record.");
                Assert.AreEqual(0, CountMaskCells(basinMask), "Open-edge bowl leaked cells into the basin mask.");
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
        /// Runs the deterministic bowl assertion through the interruptible flood-fill resume path.
        /// </summary>
        public static void RunTimeSlicedPerfectBowlAssertion()
        {
            NativeArray<float> heightmap = default;
            NativeArray<byte> basinMask = default;
            NativeArray<byte> candidateMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            NativeArray<int> floodHeap = default;
            NativeArray<int> visitedStamp = default;
            NativeArray<int> acceptedCells = default;
            NativeArray<int> sliceStatus = default;
            NativeArray<int> deferredStateBudget = default;
            NativeQueue<AnomalyBasinFloodFillState> pendingFloodStates = default;
            NativeQueue<AnomalyBasinFloodFillState> deferredFloodStates = default;
            int pendingFloodStatesSentinelId = 0;
            int deferredFloodStatesSentinelId = 0;

            try
            {
                // COLD ALLOC: NativeArray anomaly slice buffers[PixelCount] - deterministic editor anomaly validation - owner: AnomalyTestHarness
                heightmap = AllocateTrackedTempJobArray<float>(PixelCount, HeightmapLabel, NativeArrayOptions.UninitializedMemory);
                basinMask = AllocateTrackedTempJobArray<byte>(PixelCount, BasinMaskLabel, NativeArrayOptions.ClearMemory);
                candidateMask = AllocateTrackedTempJobArray<byte>(PixelCount, CandidateMaskLabel, NativeArrayOptions.ClearMemory);
                basinRecords = AllocateTrackedTempJobArray<AnomalyBasinRecord>(PixelCount, BasinRecordsLabel, NativeArrayOptions.ClearMemory);
                floodHeap = AllocateTrackedTempJobArray<int>(PixelCount, FloodHeapLabel, NativeArrayOptions.UninitializedMemory);
                visitedStamp = AllocateTrackedTempJobArray<int>(PixelCount, VisitedStampLabel, NativeArrayOptions.ClearMemory);
                acceptedCells = AllocateTrackedTempJobArray<int>(PixelCount, AcceptedCellsLabel, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<int>[2] - sliced flood-fill status slots - owner: AnomalyTestHarness
                sliceStatus = AllocateTrackedTempJobArray<int>(2, SliceStatusLabel, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeArray<int>[2] - sliced flood-fill deferred-state budget/drop slots - owner: AnomalyTestHarness
                deferredStateBudget = AllocateTrackedTempJobArray<int>(2, DeferredStateBudgetLabel, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeQueue<AnomalyBasinFloodFillState>[1] - current sliced flood-fill state lane - owner: AnomalyTestHarness
                pendingFloodStates = AllocateTrackedTempJobQueue<AnomalyBasinFloodFillState>(1, PendingFloodStatesLabel, out pendingFloodStatesSentinelId);
                // COLD ALLOC: NativeQueue<AnomalyBasinFloodFillState>[1] - deferred sliced flood-fill state lane - owner: AnomalyTestHarness
                deferredFloodStates = AllocateTrackedTempJobQueue<AnomalyBasinFloodFillState>(1, DeferredFloodStatesLabel, out deferredFloodStatesSentinelId);

                FillPerfectBowl(heightmap);
                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = 1f,
                    MinimumDepthMeters = 50f,
                    MaxFloodCells = PixelCount,
                    EqualHeightEpsilon = 0.000001f,
                    MaxFloodFillOperationsPerSlice = 64
                };

                var scanJob = new ClosedBasinDetectionJob
                {
                    Heightmap = heightmap,
                    CandidateMask = candidateMask,
                    BasinMask = basinMask,
                    BasinRecords = basinRecords,
                    Settings = settings.Sanitized()
                };
                JobHandle scanHandle = scanJob.Schedule(PixelCount, 64);
                // COLD SYNC JOB: Editor test harness must inspect deterministic scanned candidates immediately.
                scanHandle.Complete();

                NativeQueue<AnomalyBasinFloodFillState> pending = pendingFloodStates;
                NativeQueue<AnomalyBasinFloodFillState> deferred = deferredFloodStates;
                int slices = 0;
                int status = 0;
                while (slices < 32)
                {
                    JobHandle sliceHandle = HectonAnomalyEngine.ScheduleClosedBasinFloodFillSlice(
                        heightmap,
                        basinMask,
                        basinRecords,
                        candidateMask,
                        floodHeap,
                        visitedStamp,
                        acceptedCells,
                        pending,
                        deferred,
                        deferredStateBudget,
                        sliceStatus,
                        settings);

                    // COLD SYNC JOB: Editor test harness must inspect deterministic slice state immediately.
                    sliceHandle.Complete();
                    slices++;
                    status = sliceStatus[0];
                    if (status == 2)
                        break;

                    Assert.AreEqual(1, status, "Sliced basin flood fill did not defer an unfinished basin state.");
                    NativeQueue<AnomalyBasinFloodFillState> swap = pending;
                    pending = deferred;
                    deferred = swap;
                }

                Assert.AreEqual(2, status, "Sliced basin flood fill did not complete within the deterministic slice guard.");
                Assert.IsTrue(slices > 1, "Sliced basin flood fill completed without exercising queue-backed resume.");

                AnomalyBasinRecord record = FindFirstValidRecord(basinRecords);
                Assert.IsTrue(record.Valid == 1, "Sliced closed basin detector did not emit a valid bowl basin.");
                Assert.IsTrue(record.DeepestX == Center, "Sliced basin center X is not exact.");
                Assert.IsTrue(record.DeepestZ == Center, "Sliced basin center Z is not exact.");
                Assert.AreEqual(0f, record.DeepestHeight, "Sliced basin depth is not exact.");
                Assert.AreEqual(ExpectedLipHeight, record.LipHeight, "Sliced basin lip height is not exact.");
                Assert.IsTrue(record.CellCount == ExpectedBowlMaskedCells, "Sliced basin mask cell count is not exact.");
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
                DisposeTracked(ref sliceStatus);
                DisposeTracked(ref deferredStateBudget);
                DisposeTrackedQueue(ref pendingFloodStates, ref pendingFloodStatesSentinelId);
                DisposeTrackedQueue(ref deferredFloodStates, ref deferredFloodStatesSentinelId);
            }
        }

        /// <summary>
        /// Proves stamp overflow clears the visited buffer across resumable slices instead of one blocking pass.
        /// </summary>
        public static void RunTimeSlicedStampOverflowAssertion()
        {
            const int visitedSentinel = 1515870810;
            int seedIndex = Center + Center * Resolution;
            NativeArray<float> heightmap = default;
            NativeArray<byte> basinMask = default;
            NativeArray<byte> candidateMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            NativeArray<int> floodHeap = default;
            NativeArray<int> visitedStamp = default;
            NativeArray<int> acceptedCells = default;
            NativeArray<int> sliceStatus = default;
            NativeArray<int> deferredStateBudget = default;
            NativeQueue<AnomalyBasinFloodFillState> pendingFloodStates = default;
            NativeQueue<AnomalyBasinFloodFillState> deferredFloodStates = default;
            int pendingFloodStatesSentinelId = 0;
            int deferredFloodStatesSentinelId = 0;

            try
            {
                // COLD ALLOC: NativeArray anomaly stamp-overflow buffers[PixelCount] - deterministic editor anomaly validation - owner: AnomalyTestHarness
                heightmap = AllocateTrackedTempJobArray<float>(PixelCount, HeightmapLabel, NativeArrayOptions.UninitializedMemory);
                basinMask = AllocateTrackedTempJobArray<byte>(PixelCount, BasinMaskLabel, NativeArrayOptions.ClearMemory);
                candidateMask = AllocateTrackedTempJobArray<byte>(PixelCount, CandidateMaskLabel, NativeArrayOptions.ClearMemory);
                basinRecords = AllocateTrackedTempJobArray<AnomalyBasinRecord>(PixelCount, BasinRecordsLabel, NativeArrayOptions.ClearMemory);
                floodHeap = AllocateTrackedTempJobArray<int>(PixelCount, FloodHeapLabel, NativeArrayOptions.UninitializedMemory);
                visitedStamp = AllocateTrackedTempJobArray<int>(PixelCount, VisitedStampLabel, NativeArrayOptions.UninitializedMemory);
                acceptedCells = AllocateTrackedTempJobArray<int>(PixelCount, AcceptedCellsLabel, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<int>[2] - stamp-overflow sliced flood-fill status slots - owner: AnomalyTestHarness
                sliceStatus = AllocateTrackedTempJobArray<int>(2, SliceStatusLabel, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeArray<int>[2] - stamp-overflow deferred-state budget/drop slots - owner: AnomalyTestHarness
                deferredStateBudget = AllocateTrackedTempJobArray<int>(2, DeferredStateBudgetLabel, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeQueue<AnomalyBasinFloodFillState>[1] - stamp-overflow current state lane - owner: AnomalyTestHarness
                pendingFloodStates = AllocateTrackedTempJobQueue<AnomalyBasinFloodFillState>(1, PendingFloodStatesLabel, out pendingFloodStatesSentinelId);
                // COLD ALLOC: NativeQueue<AnomalyBasinFloodFillState>[1] - stamp-overflow deferred state lane - owner: AnomalyTestHarness
                deferredFloodStates = AllocateTrackedTempJobQueue<AnomalyBasinFloodFillState>(1, DeferredFloodStatesLabel, out deferredFloodStatesSentinelId);

                FillPerfectBowl(heightmap);
                candidateMask[seedIndex] = 1;
                for (int i = 0; i < visitedStamp.Length; i++)
                    visitedStamp[i] = visitedSentinel;

                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = 1f,
                    MinimumDepthMeters = 50f,
                    MaxFloodCells = PixelCount,
                    EqualHeightEpsilon = 0.000001f,
                    MaxFloodFillOperationsPerSlice = 64
                };

                pendingFloodStates.Enqueue(new AnomalyBasinFloodFillState
                {
                    CandidateIndex = seedIndex,
                    Stamp = int.MaxValue,
                    BasinId = 1,
                    Initialized = 1
                });

                JobHandle sliceHandle = HectonAnomalyEngine.ScheduleClosedBasinFloodFillSlice(
                    heightmap,
                    basinMask,
                    basinRecords,
                    candidateMask,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    pendingFloodStates,
                    deferredFloodStates,
                    deferredStateBudget,
                    sliceStatus,
                    settings);
                // COLD SYNC JOB: Editor harness inspects deterministic stamp-overflow state immediately.
                sliceHandle.Complete();

                Assert.AreEqual(1, sliceStatus[0], "Stamp-overflow flood fill did not defer before clearing.");
                if (!deferredFloodStates.TryDequeue(out AnomalyBasinFloodFillState state))
                {
                    Assert.IsTrue(false, "Stamp-overflow flood fill did not persist deferred state.");
                    return;
                }
                Assert.AreEqual(2, state.Phase, "Stamp-overflow flood fill did not enter visited-stamp clear phase.");
                Assert.AreEqual(0, state.ClearIndex, "Stamp-overflow flood fill cleared cells in the candidate-start slice.");
                Assert.AreEqual(visitedSentinel, visitedStamp[0], "Stamp-overflow flood fill performed a blocking full clear in the candidate-start slice.");

                pendingFloodStates.Enqueue(state);
                sliceHandle = HectonAnomalyEngine.ScheduleClosedBasinFloodFillSlice(
                    heightmap,
                    basinMask,
                    basinRecords,
                    candidateMask,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    pendingFloodStates,
                    deferredFloodStates,
                    deferredStateBudget,
                    sliceStatus,
                    settings);
                // COLD SYNC JOB: Editor harness inspects deterministic partial clear state immediately.
                sliceHandle.Complete();

                Assert.AreEqual(1, sliceStatus[0], "Stamp-overflow flood fill completed the visited clear inside one slice.");
                if (!deferredFloodStates.TryDequeue(out state))
                {
                    Assert.IsTrue(false, "Stamp-overflow flood fill did not persist partial clear state.");
                    return;
                }
                Assert.AreEqual(2, state.Phase, "Stamp-overflow flood fill left clear phase before the visited buffer was fully cleared.");
                Assert.IsTrue(state.ClearIndex > 0 && state.ClearIndex < PixelCount, "Stamp-overflow flood fill clear index was not partial.");
                Assert.AreEqual(0, visitedStamp[0], "Stamp-overflow flood fill did not clear the first visited stamp.");
                Assert.AreEqual(visitedSentinel, visitedStamp[state.ClearIndex], "Stamp-overflow flood fill cleared beyond the saved clear index.");

                pendingFloodStates.Enqueue(state);
                NativeQueue<AnomalyBasinFloodFillState> pending = pendingFloodStates;
                NativeQueue<AnomalyBasinFloodFillState> deferred = deferredFloodStates;
                int status = 0;
                for (int slices = 0; slices < 16; slices++)
                {
                    sliceHandle = HectonAnomalyEngine.ScheduleClosedBasinFloodFillSlice(
                        heightmap,
                        basinMask,
                        basinRecords,
                        candidateMask,
                        floodHeap,
                        visitedStamp,
                        acceptedCells,
                        pending,
                        deferred,
                        deferredStateBudget,
                        sliceStatus,
                        settings);
                    // COLD SYNC JOB: Editor harness advances the deterministic stamp-overflow resume path.
                    sliceHandle.Complete();
                    status = sliceStatus[0];
                    if (status == 2)
                        break;

                    Assert.AreEqual(1, status, "Stamp-overflow flood fill did not preserve resumable state.");
                    NativeQueue<AnomalyBasinFloodFillState> swap = pending;
                    pending = deferred;
                    deferred = swap;
                }

                Assert.AreEqual(2, status, "Stamp-overflow flood fill did not resume to completion.");
                AnomalyBasinRecord record = FindFirstValidRecord(basinRecords);
                Assert.IsTrue(record.Valid == 1, "Stamp-overflow flood fill did not emit a basin after budgeted clear.");
                Assert.AreEqual(ExpectedLipHeight, record.LipHeight, "Stamp-overflow flood fill basin lip changed after budgeted clear.");
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
                DisposeTracked(ref sliceStatus);
                DisposeTracked(ref deferredStateBudget);
                DisposeTrackedQueue(ref pendingFloodStates, ref pendingFloodStatesSentinelId);
                DisposeTrackedQueue(ref deferredFloodStates, ref deferredFloodStatesSentinelId);
            }
        }

        /// <summary>
        /// Proves malformed resumable flood-fill state is clamped back to scan phase instead of stalling a worker.
        /// </summary>
        public static void RunTimeSlicedCorruptStateRecoveryAssertion()
        {
            NativeArray<float> heightmap = default;
            NativeArray<byte> basinMask = default;
            NativeArray<byte> candidateMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            NativeArray<int> floodHeap = default;
            NativeArray<int> visitedStamp = default;
            NativeArray<int> acceptedCells = default;
            NativeArray<int> sliceStatus = default;
            NativeArray<int> deferredStateBudget = default;
            NativeQueue<AnomalyBasinFloodFillState> pendingFloodStates = default;
            NativeQueue<AnomalyBasinFloodFillState> deferredFloodStates = default;
            int pendingFloodStatesSentinelId = 0;
            int deferredFloodStatesSentinelId = 0;

            try
            {
                // COLD ALLOC: NativeArray corrupt-state flood-fill buffers[PixelCount] - deterministic editor anomaly validation - owner: AnomalyTestHarness
                heightmap = AllocateTrackedTempJobArray<float>(PixelCount, HeightmapLabel, NativeArrayOptions.UninitializedMemory);
                basinMask = AllocateTrackedTempJobArray<byte>(PixelCount, BasinMaskLabel, NativeArrayOptions.ClearMemory);
                candidateMask = AllocateTrackedTempJobArray<byte>(PixelCount, CandidateMaskLabel, NativeArrayOptions.ClearMemory);
                basinRecords = AllocateTrackedTempJobArray<AnomalyBasinRecord>(PixelCount, BasinRecordsLabel, NativeArrayOptions.ClearMemory);
                floodHeap = AllocateTrackedTempJobArray<int>(PixelCount, FloodHeapLabel, NativeArrayOptions.UninitializedMemory);
                visitedStamp = AllocateTrackedTempJobArray<int>(PixelCount, VisitedStampLabel, NativeArrayOptions.ClearMemory);
                acceptedCells = AllocateTrackedTempJobArray<int>(PixelCount, AcceptedCellsLabel, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<int>[2] - corrupt-state sliced flood-fill status slots - owner: AnomalyTestHarness
                sliceStatus = AllocateTrackedTempJobArray<int>(2, SliceStatusLabel, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeArray<int>[2] - corrupt-state deferred-state budget/drop slots - owner: AnomalyTestHarness
                deferredStateBudget = AllocateTrackedTempJobArray<int>(2, DeferredStateBudgetLabel, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeQueue<AnomalyBasinFloodFillState>[1] - corrupt-state current state lane - owner: AnomalyTestHarness
                pendingFloodStates = AllocateTrackedTempJobQueue<AnomalyBasinFloodFillState>(1, PendingFloodStatesLabel, out pendingFloodStatesSentinelId);
                // COLD ALLOC: NativeQueue<AnomalyBasinFloodFillState>[1] - corrupt-state deferred state lane - owner: AnomalyTestHarness
                deferredFloodStates = AllocateTrackedTempJobQueue<AnomalyBasinFloodFillState>(1, DeferredFloodStatesLabel, out deferredFloodStatesSentinelId);

                pendingFloodStates.Enqueue(new AnomalyBasinFloodFillState
                {
                    CandidateIndex = -17,
                    Stamp = -3,
                    BasinId = -5,
                    SeedIndex = PixelCount + 8,
                    HeapCount = PixelCount + 8,
                    AcceptedCount = -1,
                    ClearIndex = -4,
                    Phase = 99,
                    LipHeight = float.NaN,
                    DeepestHeight = float.NaN,
                    Initialized = 1
                });

                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = 1f,
                    MinimumDepthMeters = 50f,
                    MaxFloodCells = PixelCount,
                    EqualHeightEpsilon = 0.000001f,
                    MaxFloodFillOperationsPerSlice = PixelCount + 32
                };

                JobHandle sliceHandle = HectonAnomalyEngine.ScheduleClosedBasinFloodFillSlice(
                    heightmap,
                    basinMask,
                    basinRecords,
                    candidateMask,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    pendingFloodStates,
                    deferredFloodStates,
                    deferredStateBudget,
                    sliceStatus,
                    settings);

                // COLD SYNC JOB: Editor test harness must inspect deterministic corrupt-state recovery immediately.
                sliceHandle.Complete();

                Assert.AreEqual(2, sliceStatus[0], "Corrupt flood-fill state was not recovered to a completed scan.");
                Assert.AreEqual(0, CountValidRecords(basinRecords), "Corrupt flood-fill state emitted a basin record.");
                Assert.AreEqual(0, CountMaskCells(basinMask), "Corrupt flood-fill state leaked basin mask cells.");
                Assert.IsFalse(deferredFloodStates.TryDequeue(out _), "Corrupt flood-fill state was deferred again instead of being sanitized.");
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
                DisposeTracked(ref sliceStatus);
                DisposeTracked(ref deferredStateBudget);
                DisposeTrackedQueue(ref pendingFloodStates, ref pendingFloodStatesSentinelId);
                DisposeTrackedQueue(ref deferredFloodStates, ref deferredFloodStatesSentinelId);
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
                // COLD ALLOC: NativeArray cliff SDF buffers[CliffVoxelCount] - deterministic editor anomaly validation - owner: AnomalyTestHarness
                inputSdf = AllocateTrackedTempJobArray<float>(CliffVoxelCount, CliffInputSdfLabel, NativeArrayOptions.UninitializedMemory);
                outputSdf = AllocateTrackedTempJobArray<float>(CliffVoxelCount, CliffOutputSdfLabel, NativeArrayOptions.ClearMemory);

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
                    1f,
                    double3.zero);

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
                heightmap = AllocateTrackedTempJobArray<float>(FeaturePixelCount, FeatureHeightmapLabel, NativeArrayOptions.ClearMemory);
                featureRecords = AllocateTrackedTempJobArray<AnomalyFeatureRecord>(FeaturePixelCount, FeatureRecordsLabel, NativeArrayOptions.ClearMemory);
                fissureMask = AllocateTrackedTempJobArray<byte>(FeaturePixelCount, FeatureFissureMaskLabel, NativeArrayOptions.ClearMemory);

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
                terrainHeights = AllocateTrackedTempJobArray<float>(SeamTerrainCount, SeamTerrainHeightsLabel, NativeArrayOptions.UninitializedMemory);
                sdf = AllocateTrackedTempJobArray<float>(SeamVoxelCount, SeamSdfLabel, NativeArrayOptions.ClearMemory);

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
                pillarSdf = AllocateTrackedTempJobArray<float>(SdfInjectionVoxelCount, PillarSdfLabel, NativeArrayOptions.UninitializedMemory);
                fissureSdf = AllocateTrackedTempJobArray<float>(SdfInjectionVoxelCount, FissureSdfLabel, NativeArrayOptions.UninitializedMemory);
                fissureInfluence = AllocateTrackedTempJobArray<uint>(SdfInjectionVoxelCount, FissureInfluenceLabel, NativeArrayOptions.ClearMemory);

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
                    heightmap[x + z * Resolution] = math.max(dx, dz) * PerfectBowlStepMeters;
                }
            }
        }

        private static void FillOpenEdgeBowl(NativeArray<float> heightmap)
        {
            for (int i = 0; i < heightmap.Length; i++)
                heightmap[i] = ExpectedLipHeight;

            int centerX = 1;
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int dx = math.abs(x - centerX);
                    int dz = math.abs(z - Center);
                    int chebyshev = math.max(dx, dz);
                    if (chebyshev <= Center)
                        heightmap[x + z * Resolution] = chebyshev * PerfectBowlStepMeters;
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

        private static int CountValidRecords(NativeArray<AnomalyBasinRecord> records)
        {
            int count = 0;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].Valid != 0)
                    count++;
            }

            return count;
        }

        private static int CountMaskCells(NativeArray<byte> mask)
        {
            int count = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] != 0)
                    count++;
            }

            return count;
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, string label, NativeArrayOptions options)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (sentinelId > 0)
                    return array;
            }
            catch
            {
                if (array.IsCreated)
                    array.Dispose();

                throw;
            }

            array.Dispose();
            throw new System.InvalidOperationException($"[AnomalyTestHarness] NativeMemorySentinel rejected NativeArray registration for {label}.");
        }

        private static unsafe void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private static void DisposeTrackedQueue<T>(ref NativeQueue<T> queue, ref int sentinelId) where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static NativeQueue<T> AllocateTrackedTempJobQueue<T>(int capacity, string label, out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            NativeQueue<T> queue = new NativeQueue<T>(Allocator.TempJob);
            try
            {
                sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(queue, capacity, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (sentinelId > 0)
                    return queue;
            }
            catch
            {
                System.Exception nativeSentinelCleanupException1 = null;

                if (sentinelId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(sentinelId);
                    }
                    catch (System.Exception nativeSentinelException1)
                    {
                        nativeSentinelCleanupException1 = nativeSentinelException1;
                    }
                    finally
                    {
                        sentinelId = 0;
                    }
                }

                try
                {
                    queue.Dispose();
                }
                catch (System.Exception nativeSentinelException1)
                {
                    if (nativeSentinelCleanupException1 == null)
                        nativeSentinelCleanupException1 = nativeSentinelException1;
                }

                if (nativeSentinelCleanupException1 != null)
                    throw nativeSentinelCleanupException1;

                throw;
            }

            queue.Dispose();
            throw new System.InvalidOperationException($"[AnomalyTestHarness] NativeMemorySentinel rejected NativeQueue registration for {label}.");
        }
    }
}
