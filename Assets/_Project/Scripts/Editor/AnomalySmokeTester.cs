using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor smoke tester for the isolated anomaly engine.
    /// </summary>
    public static class AnomalySmokeTester
    {
        private const int Resolution = 33;
        private const int PixelCount = Resolution * Resolution;
        private const int Center = Resolution / 2;
        private const int PerfectBowlMaskedCells = (Resolution - 2) * (Resolution - 2);
        private const float SmokeHeightStepMeters = 10f;
        private const float SmokeFlatPlaneHeightMeters = 90f;
        private const double PerfectBowlBudgetMilliseconds = 1.0d;
        private const int PillarSdfWidth = 7;
        private const int PillarSdfHeight = 8;
        private const int PillarSdfDepth = 7;
        private const int PillarSdfVoxelCount = PillarSdfWidth * PillarSdfHeight * PillarSdfDepth;
        private const int PillarTerrainCount = PillarSdfWidth * PillarSdfDepth;
        private const int PillarCenter = PillarSdfWidth / 2;
        private const float PillarBaseHeightMeters = 2f;
        private const string NativeMemoryOwner = nameof(AnomalySmokeTester);
        private const string HeightmapLabel = "heightmap";
        private const string BasinMaskLabel = "basinMask";
        private const string CandidateMaskLabel = "candidateMask";
        private const string BasinRecordsLabel = "basinRecords";
        private const string FloodHeapLabel = "floodHeap";
        private const string VisitedStampLabel = "visitedStamp";
        private const string AcceptedCellsLabel = "acceptedCells";
        private const string PillarTerrainHeightsLabel = "pillarTerrainHeights";
        private const string PillarSdfLabel = "pillarSdf";
        private const string OutputFolder = "CodexArtifacts";
        private const string OutputFileName = "anomaly-smoke-report.json";

        /// <summary>
        /// Runs synthetic anomaly stress cases and writes a JSON report.
        /// </summary>
        [MenuItem("Tools/Hecton/Dev/Terrain/Run Anomaly Smoke Tester")]
        public static void Run()
        {
            SmokeReport report = RunSmoke();
            string path = WriteReport(report);
            Hecton8.Core.H8Debug.Log("ANOMALY_SMOKE_PASS " + path);
        }

        /// <summary>
        /// Executes the smoke cases and returns aggregate metrics.
        /// </summary>
        public static SmokeReport RunSmoke()
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
                // COLD ALLOC: NativeArray smoke buffers[PixelCount] — deterministic editor anomaly stress pass — owner: AnomalySmokeTester
                heightmap = new NativeArray<float>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                basinMask = new NativeArray<byte>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                candidateMask = new NativeArray<byte>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                basinRecords = new NativeArray<AnomalyBasinRecord>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                floodHeap = new NativeArray<int>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                visitedStamp = new NativeArray<int>(PixelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                acceptedCells = new NativeArray<int>(PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobBuffers(heightmap, basinMask, candidateMask, basinRecords, floodHeap, visitedStamp, acceptedCells);

                RunCase(
                    heightmap,
                    basinMask,
                    candidateMask,
                    basinRecords,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    SmokeCase.PerfectBowl);

                SmokeCaseResult perfectBowl = default;
                double perfectBowlMilliseconds = double.MaxValue;
                for (int pass = 0; pass < 8; pass++)
                {
                    SmokeCaseResult measuredBowl = RunCase(
                        heightmap,
                        basinMask,
                        candidateMask,
                        basinRecords,
                        floodHeap,
                        visitedStamp,
                        acceptedCells,
                        SmokeCase.PerfectBowl,
                        out double measuredMilliseconds);
                    if (measuredMilliseconds < perfectBowlMilliseconds)
                    {
                        perfectBowlMilliseconds = measuredMilliseconds;
                        perfectBowl = measuredBowl;
                    }
                }

                SmokeCaseResult flatPlane = RunCase(
                    heightmap,
                    basinMask,
                    candidateMask,
                    basinRecords,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    SmokeCase.FlatPlane);

                SmokeCaseResult openEdgeBowl = RunCase(
                    heightmap,
                    basinMask,
                    candidateMask,
                    basinRecords,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    SmokeCase.OpenEdgeBowl);

                SmokeCaseResult dualBowl = RunCase(
                    heightmap,
                    basinMask,
                    candidateMask,
                    basinRecords,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    SmokeCase.DualBowl);

                Assert.AreEqual(1, perfectBowl.ValidBasins, "Perfect bowl basin count mismatch.");
                Assert.AreEqual(Center, perfectBowl.FirstDeepestX, "Perfect bowl deepest X mismatch.");
                Assert.AreEqual(Center, perfectBowl.FirstDeepestZ, "Perfect bowl deepest Z mismatch.");
                Assert.AreEqual(160f, perfectBowl.FirstLipHeight, "Perfect bowl lip mismatch.");
                Assert.AreEqual(PerfectBowlMaskedCells, perfectBowl.FirstMaskedCells, "Perfect bowl included lip/rim cells in basin mask.");
                Assert.AreEqual(PerfectBowlMaskedCells, perfectBowl.TotalMaskedCells, "Perfect bowl total mask mismatch.");
                Assert.IsTrue(perfectBowlMilliseconds <= PerfectBowlBudgetMilliseconds, "Perfect bowl basin detector exceeded the 1 ms editor smoke budget.");
                Assert.AreEqual(0, flatPlane.ValidBasins, "Flat plane emitted a false basin.");
                Assert.AreEqual(0, openEdgeBowl.ValidBasins, "Open edge bowl emitted a false closed basin.");
                Assert.AreEqual(0, openEdgeBowl.TotalMaskedCells, "Open edge bowl leaked cells into basin mask.");
                Assert.AreEqual(2, dualBowl.ValidBasins, "Dual bowl basin count mismatch.");
                AssertPillarBaseInjectionDoesNotCreateAirGap();

                return new SmokeReport
                {
                    PerfectBowl = perfectBowl,
                    FlatPlane = flatPlane,
                    OpenEdgeBowl = openEdgeBowl,
                    DualBowl = dualBowl,
                    PerfectBowlMilliseconds = perfectBowlMilliseconds,
                    TotalCases = 4,
                    PassedCases = 4
                };
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

        private static void AssertPillarBaseInjectionDoesNotCreateAirGap()
        {
            NativeArray<float> terrainHeights = default;
            NativeArray<float> sdf = default;

            try
            {
                // COLD ALLOC: NativeArray pillar seam buffers - editor-only anomaly smoke validation - owner: AnomalySmokeTester
                terrainHeights = new NativeArray<float>(PillarTerrainCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sdf = new NativeArray<float>(PillarSdfVoxelCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(terrainHeights, NativeMemoryOwner, PillarTerrainHeightsLabel, NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(sdf, NativeMemoryOwner, PillarSdfLabel, NativeAllocationLifetime.TempJob);

                for (int i = 0; i < terrainHeights.Length; i++)
                    terrainHeights[i] = PillarBaseHeightMeters;

                JobHandle seamHandle = HectonAnomalyEngine.SnapSDFToTerrain(
                    terrainHeights,
                    PillarSdfWidth,
                    PillarSdfDepth,
                    1f,
                    new double3(0.0, 0.0, 0.0),
                    sdf,
                    PillarSdfWidth,
                    PillarSdfHeight,
                    PillarSdfDepth,
                    1f,
                    new double3(0.0, 0.0, 0.0));

                JobHandle pillarHandle = HectonAnomalyEngine.InjectMegaPillarSDF(
                    sdf,
                    PillarSdfWidth,
                    PillarSdfHeight,
                    PillarSdfDepth,
                    1f,
                    new double3(0.0, 0.0, 0.0),
                    new double3(PillarCenter, PillarBaseHeightMeters, PillarCenter),
                    2f,
                    8f,
                    0f,
                    0.01f,
                    seamHandle);

                pillarHandle.Complete();

                int belowBase = FlatSdfIndex(PillarCenter, 1, PillarCenter, PillarSdfWidth, PillarSdfHeight);
                int atBase = FlatSdfIndex(PillarCenter, 2, PillarCenter, PillarSdfWidth, PillarSdfHeight);
                int aboveBase = FlatSdfIndex(PillarCenter, 3, PillarCenter, PillarSdfWidth, PillarSdfHeight);
                int exteriorVoid = FlatSdfIndex(0, 3, 0, PillarSdfWidth, PillarSdfHeight);
                Assert.IsTrue(sdf[belowBase] > 0f, "Pillar base smoke lost solid terrain below the seam.");
                Assert.AreEqual(0f, sdf[atBase], "Pillar base smoke lost the exact seam lock.");
                Assert.IsTrue(sdf[aboveBase] > 0f, "Pillar base smoke left an air gap above the base.");
                Assert.IsTrue(sdf[exteriorVoid] < 0f, "Pillar base smoke filled exterior negative void.");
            }
            finally
            {
                DisposeTracked(ref terrainHeights);
                DisposeTracked(ref sdf);
            }
        }

        private static SmokeCaseResult RunCase(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<byte> candidateMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells,
            SmokeCase smokeCase)
        {
            return RunCase(
                heightmap,
                basinMask,
                candidateMask,
                basinRecords,
                floodHeap,
                visitedStamp,
                acceptedCells,
                smokeCase,
                out _);
        }

        private static SmokeCaseResult RunCase(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<byte> candidateMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells,
            SmokeCase smokeCase,
            out double detectionMilliseconds)
        {
            FillHeightmap(heightmap, smokeCase);
            var settings = new AnomalyBasinDetectionSettings
            {
                Width = Resolution,
                Height = Resolution,
                CellSizeMeters = 1f,
                MinimumDepthMeters = 50f,
                MaxFloodCells = PixelCount,
                EqualHeightEpsilon = 0.000001f
            };

            long detectionStartTicks = Stopwatch.GetTimestamp();
            JobHandle handle = HectonAnomalyEngine.ScheduleClosedBasinDetection(
                heightmap,
                basinMask,
                basinRecords,
                candidateMask,
                floodHeap,
                visitedStamp,
                acceptedCells,
                settings);

            // COLD SYNC JOB: editor smoke test must inspect deterministic output synchronously.
            handle.Complete();
            detectionMilliseconds = (Stopwatch.GetTimestamp() - detectionStartTicks) * 1000.0d / Stopwatch.Frequency;
            return ExtractResult(basinRecords, basinMask);
        }

        private static void FillHeightmap(NativeArray<float> heightmap, SmokeCase smokeCase)
        {
            for (int i = 0; i < heightmap.Length; i++)
                heightmap[i] = SmokeFlatPlaneHeightMeters;

            if (smokeCase == SmokeCase.FlatPlane)
                return;

            if (smokeCase == SmokeCase.OpenEdgeBowl)
            {
                FillChebyshevBowl(heightmap, 1, Center, 16);
                return;
            }

            if (smokeCase == SmokeCase.PerfectBowl)
            {
                FillChebyshevBowl(heightmap, Center, Center, 16);
                return;
            }

            FillChebyshevBowl(heightmap, 8, 8, 6);
            FillChebyshevBowl(heightmap, 24, 24, 6);
        }

        private static void FillChebyshevBowl(NativeArray<float> heightmap, int centerX, int centerZ, int radius)
        {
            for (int z = math.max(0, centerZ - radius); z <= math.min(Resolution - 1, centerZ + radius); z++)
            {
                for (int x = math.max(0, centerX - radius); x <= math.min(Resolution - 1, centerX + radius); x++)
                {
                    int dx = math.abs(x - centerX);
                    int dz = math.abs(z - centerZ);
                    heightmap[x + z * Resolution] = math.max(dx, dz) * SmokeHeightStepMeters;
                }
            }
        }

        private static int FlatSdfIndex(int x, int y, int z, int width, int height)
        {
            return x + y * width + z * width * height;
        }

        private static SmokeCaseResult ExtractResult(NativeArray<AnomalyBasinRecord> records, NativeArray<byte> basinMask)
        {
            int validCount = 0;
            int maskedCells = 0;
            SmokeCaseResult result = default;

            for (int i = 0; i < basinMask.Length; i++)
            {
                if (basinMask[i] != 0)
                    maskedCells++;
            }

            for (int i = 0; i < records.Length; i++)
            {
                AnomalyBasinRecord record = records[i];
                if (record.Valid == 0)
                    continue;

                if (validCount == 0)
                {
                    result.FirstDeepestX = record.DeepestX;
                    result.FirstDeepestZ = record.DeepestZ;
                    result.FirstLipHeight = record.LipHeight;
                    result.FirstMaskedCells = record.CellCount;
                }

                validCount++;
            }

            result.ValidBasins = validCount;
            result.TotalMaskedCells = maskedCells;
            return result;
        }

        private static string WriteReport(SmokeReport report)
        {
            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            string path = Path.Combine(OutputFolder, OutputFileName);
            // COLD ALLOC: StringBuilder[512] — editor-only smoke JSON writer — owner: AnomalySmokeTester
            var builder = new StringBuilder(512);
            builder.AppendLine("{");
            builder.AppendLine("  \"status\": \"PENDING_VERIFICATION\",");
            builder.AppendLine("  \"totalCases\": " + report.TotalCases + ",");
            builder.AppendLine("  \"passedCases\": " + report.PassedCases + ",");
            builder.AppendLine("  \"perfectBowlMilliseconds\": " + report.PerfectBowlMilliseconds.ToString(CultureInfo.InvariantCulture) + ",");
            AppendCase(builder, "perfectBowl", report.PerfectBowl, false);
            AppendCase(builder, "flatPlane", report.FlatPlane, false);
            AppendCase(builder, "openEdgeBowl", report.OpenEdgeBowl, false);
            AppendCase(builder, "dualBowl", report.DualBowl, true);
            builder.AppendLine("}");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return path;
        }

        private static void AppendCase(StringBuilder builder, string name, SmokeCaseResult result, bool last)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.AppendLine("\": {");
            builder.AppendLine("    \"validBasins\": " + result.ValidBasins + ",");
            builder.AppendLine("    \"firstDeepestX\": " + result.FirstDeepestX + ",");
            builder.AppendLine("    \"firstDeepestZ\": " + result.FirstDeepestZ + ",");
            builder.AppendLine("    \"firstLipHeight\": " + result.FirstLipHeight.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("    \"firstMaskedCells\": " + result.FirstMaskedCells + ",");
            builder.AppendLine("    \"totalMaskedCells\": " + result.TotalMaskedCells);
            builder.Append("  }");
            builder.AppendLine(last ? string.Empty : ",");
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

        private enum SmokeCase
        {
            PerfectBowl,
            FlatPlane,
            OpenEdgeBowl,
            DualBowl
        }

        /// <summary>
        /// Aggregate smoke report.
        /// </summary>
        public struct SmokeReport
        {
            /// <summary>Perfect bowl result.</summary>
            public SmokeCaseResult PerfectBowl;

            /// <summary>Flat plane result.</summary>
            public SmokeCaseResult FlatPlane;

            /// <summary>Open edge bowl rejection result.</summary>
            public SmokeCaseResult OpenEdgeBowl;

            /// <summary>Dual bowl result.</summary>
            public SmokeCaseResult DualBowl;

            /// <summary>Measured warmed perfect bowl detector runtime in milliseconds.</summary>
            public double PerfectBowlMilliseconds;

            /// <summary>Total executed cases.</summary>
            public int TotalCases;

            /// <summary>Total passed cases.</summary>
            public int PassedCases;
        }

        /// <summary>
        /// One smoke case result.
        /// </summary>
        public struct SmokeCaseResult
        {
            /// <summary>Valid basin count.</summary>
            public int ValidBasins;

            /// <summary>First basin deepest X.</summary>
            public int FirstDeepestX;

            /// <summary>First basin deepest Z.</summary>
            public int FirstDeepestZ;

            /// <summary>First basin lip height.</summary>
            public float FirstLipHeight;

            /// <summary>First basin masked cell count.</summary>
            public int FirstMaskedCells;

            /// <summary>Total masked cell count.</summary>
            public int TotalMaskedCells;
        }
    }
}
