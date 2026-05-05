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
        private const string NativeMemoryOwner = nameof(AnomalyTestHarness);
        private const string HeightmapLabel = "heightmap";
        private const string BasinMaskLabel = "basinMask";
        private const string CandidateMaskLabel = "candidateMask";
        private const string BasinRecordsLabel = "basinRecords";
        private const string FloodHeapLabel = "floodHeap";
        private const string VisitedStampLabel = "visitedStamp";
        private const string AcceptedCellsLabel = "acceptedCells";

        /// <summary>
        /// Generates a mathematically exact Chebyshev bowl and validates basin lip and center.
        /// </summary>
        [MenuItem("Tools/Hecton/Dev/Terrain/Run Anomaly Test Harness")]
        public static void Run()
        {
            RunPerfectBowlAssertion();
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
