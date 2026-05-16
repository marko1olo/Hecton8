#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Dev
{
    /// <summary>
    /// Cold-path smoke harness for the SpaceEngine 0.9.8.0 terrain Burst translation.
    /// </summary>
    public static class SpaceEngine098TerrainSmokeTester
    {
        private const string NativeMemoryOwner = nameof(SpaceEngine098TerrainSmokeTester);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.TempJob;
        private const int WarmupWidth = 16;
        private const int TimedWidth = 64;
        private const float CellSizeMeters = 16f;
        private const double AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
        private const uint Seed = 880031u;

        private readonly struct PipelineResult
        {
            public PipelineResult(
                bool passed,
                int sampleCount,
                float elapsedMs,
                float minHeight,
                float maxHeight,
                float ridgedDelta,
                float craterDelta,
                float rilleDelta,
                float ridgedMs,
                float craterMs,
                float rilleMs,
                float metricsMs,
                int checksum,
                int nativeAllocationDelta,
                long nativeByteDelta)
            {
                Passed = passed;
                SampleCount = sampleCount;
                ElapsedMs = elapsedMs;
                MinHeight = minHeight;
                MaxHeight = maxHeight;
                RidgedDelta = ridgedDelta;
                CraterDelta = craterDelta;
                RilleDelta = rilleDelta;
                RidgedMs = ridgedMs;
                CraterMs = craterMs;
                RilleMs = rilleMs;
                MetricsMs = metricsMs;
                Checksum = checksum;
                NativeAllocationDelta = nativeAllocationDelta;
                NativeByteDelta = nativeByteDelta;
            }

            public bool Passed { get; }
            public int SampleCount { get; }
            public float ElapsedMs { get; }
            public float MinHeight { get; }
            public float MaxHeight { get; }
            public float RidgedDelta { get; }
            public float CraterDelta { get; }
            public float RilleDelta { get; }
            public float RidgedMs { get; }
            public float CraterMs { get; }
            public float RilleMs { get; }
            public float MetricsMs { get; }
            public bool NodeBudgetPassed => RidgedMs <= 2f && CraterMs <= 2f && RilleMs <= 2f;
            public int Checksum { get; }
            public int NativeAllocationDelta { get; }
            public long NativeByteDelta { get; }
        }

        public static bool Run(out string json)
        {
            PipelineResult warmup = RunPipeline(WarmupWidth, measureElapsed: false);
            PipelineResult timed = RunPipeline(TimedWidth, measureElapsed: true);
            bool passed = warmup.Passed && timed.Passed;
            json = "{"
                + "\"tester\":\"SpaceEngine098TerrainSmokeTester\","
                + "\"status\":\"" + (passed ? "PASS" : "FAIL") + "\","
                + "\"warmupSamples\":" + warmup.SampleCount + ","
                + "\"samples\":" + timed.SampleCount + ","
                + "\"elapsedMsX1000\":" + Milli(timed.ElapsedMs) + ","
                + "\"minHeightX1000\":" + Milli(timed.MinHeight) + ","
                + "\"maxHeightX1000\":" + Milli(timed.MaxHeight) + ","
                + "\"ridgedDeltaX100000\":" + HundredK(timed.RidgedDelta) + ","
                + "\"craterDeltaX100000\":" + HundredK(timed.CraterDelta) + ","
                + "\"rilleDeltaX100000\":" + HundredK(timed.RilleDelta) + ","
                + "\"ridgedMsX1000\":" + Milli(timed.RidgedMs) + ","
                + "\"craterMsX1000\":" + Milli(timed.CraterMs) + ","
                + "\"rilleMsX1000\":" + Milli(timed.RilleMs) + ","
                + "\"metricsMsX1000\":" + Milli(timed.MetricsMs) + ","
                + "\"nodeBudgetPassed\":" + (timed.NodeBudgetPassed ? "true" : "false") + ","
                + "\"checksum\":" + timed.Checksum + ","
                + "\"nativeAllocationDelta\":" + timed.NativeAllocationDelta + ","
                + "\"nativeByteDelta\":" + timed.NativeByteDelta + "}";
            return passed;
        }

        private static PipelineResult RunPipeline(int width, bool measureElapsed)
        {
            int sampleCount = width * width;
            int nativeBefore = NativeMemorySentinel.ActiveAllocationCount;
            long bytesBefore = NativeMemorySentinel.TrackedBytes;
            NativeArray<float> input = default;
            NativeArray<float> ridged = default;
            NativeArray<float> crater = default;
            NativeArray<float> rille = default;
            NativeArray<float3> craterCenters = default;
            NativeArray<SpaceEngine098PipelineMetricSample> metrics = default;
            JobHandle handle = default;
            bool scheduled = false;
            long start = 0L;
            float elapsedMs = 0f;
            float ridgedDelta = 0f;
            float craterDelta = 0f;
            float rilleDelta = 0f;
            float ridgedMs = 0f;
            float craterMs = 0f;
            float rilleMs = 0f;
            float metricsMs = 0f;
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            int checksum = 17;
            bool finite = true;

            try
            {
                // COLD ALLOC: NativeArray<float>[sampleCount * 4] + NativeArray<float3>[4] + NativeArray<SpaceEngine098PipelineMetricSample>[sampleCount] - dev-only Burst terrain pipeline probe - owner: SpaceEngine098TerrainSmokeTester
                input = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                ridged = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                crater = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                rille = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                craterCenters = new NativeArray<float3>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                metrics = new NativeArray<SpaceEngine098PipelineMetricSample>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                Register(input, nameof(input));
                Register(ridged, nameof(ridged));
                Register(crater, nameof(crater));
                Register(rille, nameof(rille));
                Register(craterCenters, nameof(craterCenters));
                Register(metrics, nameof(metrics));

                for (int i = 0; i < sampleCount; i++)
                {
                    int x = i % width;
                    int z = i / width;
                    input[i] = 0.42f + x * 0.0007f + z * 0.0003f;
                }

                if (measureElapsed)
                    start = Stopwatch.GetTimestamp();

                long stageStart = measureElapsed ? Stopwatch.GetTimestamp() : 0L;
                var ridgedParams = new SpaceEngine098RidgedMultifractalParams
                {
                    Frequency = 0.00042f,
                    Strength01 = 0.075f,
                    Gain = 2f,
                    Warp = 0.72f,
                    FirstOctaveValue = 0.86f,
                    Lacunarity = SpaceEngine098TerrainMath.DefaultLacunarity,
                    H = SpaceEngine098TerrainMath.DefaultH,
                    Offset = SpaceEngine098TerrainMath.DefaultOffset,
                    RidgeSmooth = SpaceEngine098TerrainMath.DefaultRidgeSmooth,
                    Octaves = 6
                };

                handle = new SpaceEngine098RidgedMultifractalJob
                {
                    InputHeights01 = input,
                    OutputHeights01 = ridged,
                    Width = width,
                    WorldOriginXZ = new double2(AupCellSizeMeters * 2.0, -AupCellSizeMeters),
                    CellSizeMeters = CellSizeMeters,
                    Parameters = ridgedParams,
                    Seed = SpaceEngine098TerrainMath.MixSeed(Seed, 2, -1)
                }.Schedule(sampleCount, ResolveBatchCount(sampleCount));
                scheduled = true;

                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                scheduled = false;
                if (measureElapsed)
                    ridgedMs = ElapsedMsSince(stageStart);

                stageStart = measureElapsed ? Stopwatch.GetTimestamp() : 0L;
                handle = new SpaceEngine098CraterPlacementJob
                {
                    CraterAupCenters = craterCenters,
                    WorldOriginXZ = new double2(AupCellSizeMeters * 2.0, -AupCellSizeMeters),
                    WorldSizeXZ = new double2(width * CellSizeMeters, width * CellSizeMeters),
                    RadiusMeters = 220f,
                    Seed = SpaceEngine098TerrainMath.MixSeed(Seed ^ 0x43525452u, 2, -1)
                }.Schedule(craterCenters.Length, ResolveBatchCount(craterCenters.Length), handle);

                handle = new SpaceEngine098ApplyCraterHeightJob
                {
                    InputHeights01 = ridged,
                    OutputHeights01 = crater,
                    CraterAupCenters = craterCenters,
                    Width = width,
                    WorldOriginXZ = new double2(AupCellSizeMeters * 2.0, -AupCellSizeMeters),
                    CellSizeMeters = CellSizeMeters,
                    RadiusMeters = 220f,
                    Amplitude01 = 0.045f,
                    Profile = SpaceEngine098CraterProfile.OldDefault()
                }.Schedule(sampleCount, ResolveBatchCount(sampleCount), handle);
                scheduled = true;

                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                scheduled = false;
                if (measureElapsed)
                    craterMs = ElapsedMsSince(stageStart);

                stageStart = measureElapsed ? Stopwatch.GetTimestamp() : 0L;
                var rilleParams = new SpaceEngine098RilleParams
                {
                    CellFrequency = 0.004f,
                    Depth01 = 0.035f,
                    Narrowness = 250f,
                    Sharpness = 1.75f,
                    DomainWarpMeters = 80f,
                    DomainWarpFrequency = 0.0012f,
                    RimLift01 = 0.004f
                };

                handle = new SpaceEngine098RilleFissureJob
                {
                    InputHeights01 = crater,
                    OutputHeights01 = rille,
                    Width = width,
                    WorldOriginXZ = new double2(AupCellSizeMeters * 2.0, -AupCellSizeMeters),
                    CellSizeMeters = CellSizeMeters,
                    Parameters = rilleParams,
                    Seed = SpaceEngine098TerrainMath.MixSeed(Seed ^ 0x52494C4Cu, 2, -1)
                }.Schedule(sampleCount, ResolveBatchCount(sampleCount), handle);
                scheduled = true;

                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                scheduled = false;
                if (measureElapsed)
                    rilleMs = ElapsedMsSince(stageStart);

                stageStart = measureElapsed ? Stopwatch.GetTimestamp() : 0L;
                int checksumStride = math.max(1, sampleCount / 32);
                handle = new SpaceEngine098PipelineMetricsJob
                {
                    InputHeights01 = input,
                    RidgedHeights01 = ridged,
                    CraterHeights01 = crater,
                    RilleHeights01 = rille,
                    Metrics = metrics,
                    ChecksumStride = checksumStride
                }.Schedule(sampleCount, ResolveBatchCount(sampleCount), handle);
                scheduled = true;

                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                scheduled = false;
                if (measureElapsed)
                    metricsMs = ElapsedMsSince(stageStart);

                if (measureElapsed)
                {
                    elapsedMs = ElapsedMsSince(start);
                }

                for (int i = 0; i < sampleCount; i++)
                {
                    SpaceEngine098PipelineMetricSample sample = metrics[i];
                    finite &= sample.IsFinite != 0;
                    ridgedDelta = math.max(ridgedDelta, sample.RidgedDelta);
                    craterDelta = math.max(craterDelta, sample.CraterDelta);
                    rilleDelta = math.max(rilleDelta, sample.RilleDelta);
                    minHeight = math.min(minHeight, sample.MinHeight);
                    maxHeight = math.max(maxHeight, sample.MaxHeight);

                    if (sample.HasChecksumContribution != 0)
                        checksum = unchecked(checksum * 31 + sample.ChecksumContribution);
                }
            }
            finally
            {
                if (scheduled)
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref input);
                DisposeTracked(ref ridged);
                DisposeTracked(ref crater);
                DisposeTracked(ref rille);
                DisposeTracked(ref craterCenters);
                DisposeTracked(ref metrics);
            }

            int nativeDelta = NativeMemorySentinel.ActiveAllocationCount - nativeBefore;
            long byteDelta = NativeMemorySentinel.TrackedBytes - bytesBefore;
            bool changed = ridgedDelta > 0.000001f &&
                           craterDelta > 0.000001f &&
                           rilleDelta > 0.000001f;
            bool bounded = minHeight >= 0f && maxHeight <= 1f;
            bool memoryBalanced = nativeDelta == 0 && byteDelta == 0L;
            bool nodeBudgetPassed = !measureElapsed || (ridgedMs <= 2f && craterMs <= 2f && rilleMs <= 2f);
            bool passed = finite && changed && bounded && memoryBalanced && nodeBudgetPassed;

            return new PipelineResult(
                passed,
                sampleCount,
                elapsedMs,
                minHeight,
                maxHeight,
                ridgedDelta,
                craterDelta,
                rilleDelta,
                ridgedMs,
                craterMs,
                rilleMs,
                metricsMs,
                checksum,
                nativeDelta,
                byteDelta);
        }

        private static int ResolveBatchCount(int sampleCount)
        {
            return math.max(1, math.min(64, sampleCount / 16));
        }

        private static void Register<T>(NativeArray<T> array, string label)
            where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static int Milli(float value)
        {
            return (int)math.round(value * 1000f);
        }

        private static int HundredK(float value)
        {
            return (int)math.round(value * 100000f);
        }

        private static float ElapsedMsSince(long startTimestamp)
        {
            return (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
        }
    }
}
#endif
