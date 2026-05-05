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
        private const string NativeMemoryOwner = nameof(AnomalySmokeTester);
        private const string HeightmapLabel = "heightmap";
        private const string BasinMaskLabel = "basinMask";
        private const string CandidateMaskLabel = "candidateMask";
        private const string BasinRecordsLabel = "basinRecords";
        private const string FloodHeapLabel = "floodHeap";
        private const string VisitedStampLabel = "visitedStamp";
        private const string AcceptedCellsLabel = "acceptedCells";
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
            Debug.Log("ANOMALY_SMOKE_PASS " + path);
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

                SmokeCaseResult perfectBowl = RunCase(
                    heightmap,
                    basinMask,
                    candidateMask,
                    basinRecords,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    SmokeCase.PerfectBowl);

                SmokeCaseResult flatPlane = RunCase(
                    heightmap,
                    basinMask,
                    candidateMask,
                    basinRecords,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    SmokeCase.FlatPlane);

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
                Assert.AreEqual(16f, perfectBowl.FirstLipHeight, "Perfect bowl lip mismatch.");
                Assert.AreEqual(0, flatPlane.ValidBasins, "Flat plane emitted a false basin.");
                Assert.AreEqual(2, dualBowl.ValidBasins, "Dual bowl basin count mismatch.");

                return new SmokeReport
                {
                    PerfectBowl = perfectBowl,
                    FlatPlane = flatPlane,
                    DualBowl = dualBowl,
                    TotalCases = 3,
                    PassedCases = 3
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
            FillHeightmap(heightmap, smokeCase);
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

            // COLD SYNC JOB: editor smoke test must inspect deterministic output synchronously.
            handle.Complete();
            return ExtractResult(basinRecords, basinMask);
        }

        private static void FillHeightmap(NativeArray<float> heightmap, SmokeCase smokeCase)
        {
            for (int i = 0; i < heightmap.Length; i++)
                heightmap[i] = 9f;

            if (smokeCase == SmokeCase.FlatPlane)
                return;

            if (smokeCase == SmokeCase.PerfectBowl)
            {
                FillChebyshevBowl(heightmap, Center, Center, 16);
                return;
            }

            FillChebyshevBowl(heightmap, 10, 10, 6);
            FillChebyshevBowl(heightmap, 23, 23, 6);
        }

        private static void FillChebyshevBowl(NativeArray<float> heightmap, int centerX, int centerZ, int radius)
        {
            for (int z = math.max(0, centerZ - radius); z <= math.min(Resolution - 1, centerZ + radius); z++)
            {
                for (int x = math.max(0, centerX - radius); x <= math.min(Resolution - 1, centerX + radius); x++)
                {
                    int dx = math.abs(x - centerX);
                    int dz = math.abs(z - centerZ);
                    heightmap[x + z * Resolution] = math.max(dx, dz);
                }
            }
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
            AppendCase(builder, "perfectBowl", report.PerfectBowl, false);
            AppendCase(builder, "flatPlane", report.FlatPlane, false);
            AppendCase(builder, "dualBowl", report.DualBowl, true);
            builder.AppendLine("}");
            File.WriteAllText(path, builder.ToString());
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
            builder.AppendLine("    \"firstLipHeight\": " + result.FirstLipHeight + ",");
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

            /// <summary>Dual bowl result.</summary>
            public SmokeCaseResult DualBowl;

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
