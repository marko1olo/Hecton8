using Hecton8.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path smoke harness for Y-axis volumetric biome classification.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/World/Volumetric Biome Smoke Tester")]
    public sealed class VolumetricBiomeSmokeTester : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(VolumetricBiomeSmokeTester);
        private const string MatricesLabel = "volumetricBiomeMatrices";
        private const string InputsLabel = "volumetricBiomeInputs";
        private const string ResultsLabel = "volumetricBiomeResults";
        private const string ExpectedBiomeIdsLabel = "volumetricBiomeExpectedIds";
        private const string ExpectedFlagMasksLabel = "volumetricBiomeExpectedFlags";
        private const string AuditResultsLabel = "volumetricBiomeAuditResults";
        private const string BlockSummariesLabel = "volumetricBiomeBlockSummaries";
        private const string SummaryLabel = "volumetricBiomeStressSummary";
        private const int StressSampleCount = 256;
        private const int StressSamplesPerBlock = 16;
        private const int StressBlockCount = (StressSampleCount + StressSamplesPerBlock - 1) / StressSamplesPerBlock;
        private const int ShallowBiomeId = 11;
        private const int TwilightBiomeId = 12;
        private const int HadalBiomeId = 13;
        private const byte VolumetricDepthFlag =
            (byte)WorldProceduralFieldSampler.BiomeInfluenceFlags.VolumetricDepth;

        [SerializeField] private bool runOnStart;
        [SerializeField] private bool logResult = true;
        [SerializeField] private bool _debugPassed;
        [SerializeField] private int _debugShallowBiomeId;
        [SerializeField] private int _debugTwilightBiomeId;
        [SerializeField] private int _debugHadalBiomeId;
        [SerializeField] private int _debugTwilightFlags;
        [SerializeField] private int _debugStressSampleCount;
        [SerializeField] private int _debugStressFailureCount;
        [SerializeField] private uint _debugPackedChecksum;
        [SerializeField] private int _debugSentinelBefore;
        [SerializeField] private int _debugSentinelAfter;
        [SerializeField] private int _debugSentinelDelta;

        /// <summary>
        /// True when the most recent smoke run passed.
        /// </summary>
        public bool LastRunPassed => _debugPassed;

        /// <summary>
        /// Immutable headless volumetric biome smoke report.
        /// </summary>
        public readonly struct VolumetricBiomeSmokeReport
        {
            public VolumetricBiomeSmokeReport(
                bool passed,
                int shallowBiomeId,
                int twilightBiomeId,
                int hadalBiomeId,
                int twilightFlags,
                int stressSampleCount,
                int stressFailureCount,
                uint packedChecksum,
                int sentinelBefore,
                int sentinelAfter,
                int sentinelDelta)
            {
                Passed = passed;
                ShallowBiomeId = shallowBiomeId;
                TwilightBiomeId = twilightBiomeId;
                HadalBiomeId = hadalBiomeId;
                TwilightFlags = twilightFlags;
                StressSampleCount = stressSampleCount;
                StressFailureCount = stressFailureCount;
                PackedChecksum = packedChecksum;
                SentinelBefore = sentinelBefore;
                SentinelAfter = sentinelAfter;
                SentinelDelta = sentinelDelta;
            }

            public bool Passed { get; }
            public int ShallowBiomeId { get; }
            public int TwilightBiomeId { get; }
            public int HadalBiomeId { get; }
            public int TwilightFlags { get; }
            public int StressSampleCount { get; }
            public int StressFailureCount { get; }
            public uint PackedChecksum { get; }
            public int SentinelBefore { get; }
            public int SentinelAfter { get; }
            public int SentinelDelta { get; }
        }

        private void Start()
        {
            if (runOnStart)
                RunSmokeTest();
        }

        /// <summary>
        /// Runs the scene-attached smoke test and mirrors results into inspector diagnostics.
        /// </summary>
        [ContextMenu("Run Volumetric Biome Smoke Test")]
        public bool RunSmokeTest()
        {
            _debugPassed = RunHeadlessSmokeTest(out VolumetricBiomeSmokeReport report);
            _debugShallowBiomeId = report.ShallowBiomeId;
            _debugTwilightBiomeId = report.TwilightBiomeId;
            _debugHadalBiomeId = report.HadalBiomeId;
            _debugTwilightFlags = report.TwilightFlags;
            _debugStressSampleCount = report.StressSampleCount;
            _debugStressFailureCount = report.StressFailureCount;
            _debugPackedChecksum = report.PackedChecksum;
            _debugSentinelBefore = report.SentinelBefore;
            _debugSentinelAfter = report.SentinelAfter;
            _debugSentinelDelta = report.SentinelDelta;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logResult)
            {
                Debug.Log(
                    _debugPassed
                        ? "[VolumetricBiomeSmokeTester] PASS stress=256"
                        : "[VolumetricBiomeSmokeTester] FAIL",
                    this);
            }
#endif
            return _debugPassed;
        }

        /// <summary>
        /// Runs a deterministic headless classification smoke test without requiring scene objects.
        /// </summary>
        public static bool RunHeadlessSmokeTest(
            out int shallowBiomeId,
            out int twilightBiomeId,
            out int hadalBiomeId,
            out int twilightFlags)
        {
            bool passed = RunHeadlessSmokeTest(out VolumetricBiomeSmokeReport report);
            shallowBiomeId = report.ShallowBiomeId;
            twilightBiomeId = report.TwilightBiomeId;
            hadalBiomeId = report.HadalBiomeId;
            twilightFlags = report.TwilightFlags;
            return passed;
        }

        /// <summary>
        /// Runs the stress variant and returns a complete deterministic report.
        /// </summary>
        public static bool RunHeadlessSmokeTest(out VolumetricBiomeSmokeReport report)
        {
            NativeArray<WorldProceduralFieldSampler.BiomeMatrixData> matrices = default;
            NativeArray<VolumetricBiomeClassificationInput> inputs = default;
            NativeArray<VolumetricBiomeClassificationResult> results = default;
            NativeArray<int> expectedBiomeIds = default;
            NativeArray<byte> expectedFlagMasks = default;
            NativeArray<VolumetricBiomeStressAuditResult> auditResults = default;
            NativeArray<VolumetricBiomeStressBlockSummary> blockSummaries = default;
            NativeArray<VolumetricBiomeStressSummaryResult> summary = default;
            int sentinelBefore = NativeMemorySentinel.ActiveAllocationCount;
            int sentinelAfter = sentinelBefore;
            int shallowBiomeId = 0;
            int twilightBiomeId = 0;
            int hadalBiomeId = 0;
            int twilightFlags = 0;
            int stressFailureCount = int.MaxValue;
            uint packedChecksum = 2166136261u;
            bool classificationPassed = false;

            try
            {
                // COLD ALLOC: NativeArray<BiomeMatrixData>[3] - volumetric biome smoke fixture - owner: VolumetricBiomeSmokeTester
                matrices = AllocateSmokeArray<WorldProceduralFieldSampler.BiomeMatrixData>(3, MatricesLabel);
                // COLD ALLOC: NativeArray<VolumetricBiomeClassificationInput>[256] - stress job input - owner: VolumetricBiomeSmokeTester
                inputs = AllocateSmokeArray<VolumetricBiomeClassificationInput>(StressSampleCount, InputsLabel);
                // COLD ALLOC: NativeArray<VolumetricBiomeClassificationResult>[256] - stress job output - owner: VolumetricBiomeSmokeTester
                results = AllocateSmokeArray<VolumetricBiomeClassificationResult>(StressSampleCount, ResultsLabel);
                // COLD ALLOC: NativeArray<int>[256] - expected biome ids for stress audit - owner: VolumetricBiomeSmokeTester
                expectedBiomeIds = AllocateSmokeArray<int>(StressSampleCount, ExpectedBiomeIdsLabel);
                // COLD ALLOC: NativeArray<byte>[256] - expected flag masks for stress audit - owner: VolumetricBiomeSmokeTester
                expectedFlagMasks = AllocateSmokeArray<byte>(StressSampleCount, ExpectedFlagMasksLabel);
                // COLD ALLOC: NativeArray<VolumetricBiomeStressAuditResult>[256] - stress audit output - owner: VolumetricBiomeSmokeTester
                auditResults = AllocateSmokeArray<VolumetricBiomeStressAuditResult>(StressSampleCount, AuditResultsLabel);
                // COLD ALLOC: NativeArray<VolumetricBiomeStressBlockSummary>[16] - Burst block reduction output - owner: VolumetricBiomeSmokeTester
                blockSummaries = AllocateSmokeArray<VolumetricBiomeStressBlockSummary>(StressBlockCount, BlockSummariesLabel);
                // COLD ALLOC: NativeArray<VolumetricBiomeStressSummaryResult>[1] - Burst final reduction output - owner: VolumetricBiomeSmokeTester
                summary = AllocateSmokeArray<VolumetricBiomeStressSummaryResult>(1, SummaryLabel);

                matrices[0] = BuildMatrix(ShallowBiomeId, 7, 0f, 500f);
                matrices[1] = BuildMatrix(TwilightBiomeId, 7, 500f, 2000f);
                matrices[2] = BuildMatrix(HadalBiomeId, 7, 2000f, 5000f);

                VolumetricBiomeStressInputBuildJob inputBuildJob = new VolumetricBiomeStressInputBuildJob
                {
                    Inputs = inputs,
                    ExpectedBiomeIds = expectedBiomeIds,
                    ExpectedFlagMasks = expectedFlagMasks,
                    ShallowBiomeId = ShallowBiomeId,
                    TwilightBiomeId = TwilightBiomeId,
                    HadalBiomeId = HadalBiomeId,
                    PreferredFamilyDataIndex = 7,
                    VolumetricDepthFlag = VolumetricDepthFlag
                };

                VolumetricBiomeClassificationJob job = new VolumetricBiomeClassificationJob
                {
                    Inputs = inputs,
                    BiomeMatrices = matrices,
                    Results = results,
                    BiomeMatrixCount = matrices.Length
                };

                VolumetricBiomeStressAuditJob auditJob = new VolumetricBiomeStressAuditJob
                {
                    Results = results,
                    ExpectedBiomeIds = expectedBiomeIds,
                    ExpectedFlagMasks = expectedFlagMasks,
                    AuditResults = auditResults
                };

                VolumetricBiomeStressBlockReduceJob blockReduceJob = new VolumetricBiomeStressBlockReduceJob
                {
                    AuditResults = auditResults,
                    BlockSummaries = blockSummaries,
                    SampleCount = inputs.Length,
                    SamplesPerBlock = StressSamplesPerBlock
                };

                VolumetricBiomeStressFinalReduceJob finalReduceJob = new VolumetricBiomeStressFinalReduceJob
                {
                    BlockSummaries = blockSummaries,
                    Summary = summary,
                    BlockCount = StressBlockCount
                };

                JobHandle inputBuildHandle = inputBuildJob.Schedule(inputs.Length, 16);
                JobHandle classificationHandle = job.Schedule(inputs.Length, 16, inputBuildHandle);
                JobHandle auditHandle = auditJob.Schedule(inputs.Length, 16, classificationHandle);
                JobHandle blockReduceHandle = blockReduceJob.Schedule(StressBlockCount, 1, auditHandle);
                JobHandle finalReduceHandle = finalReduceJob.Schedule(blockReduceHandle);
                // COLD SYNC JOB: headless smoke tester synchronizes once after the scheduled Burst stress chain; no runtime Tick/Update path calls this method.
                DispatcherJobSwap.TryComplete(ref finalReduceHandle, forceComplete: true);

                shallowBiomeId = results[0].InfluenceCell.PrimaryBiomeId;
                twilightBiomeId = results[1].InfluenceCell.PrimaryBiomeId;
                hadalBiomeId = results[2].InfluenceCell.PrimaryBiomeId;
                twilightFlags = results[1].InfluenceCell.Flags;
                VolumetricBiomeStressSummaryResult stressSummary = summary[0];
                stressFailureCount = stressSummary.FailureCount;
                packedChecksum = stressSummary.PackedChecksum;

                classificationPassed = shallowBiomeId == ShallowBiomeId &&
                                       twilightBiomeId == TwilightBiomeId &&
                                       hadalBiomeId == HadalBiomeId &&
                                       (twilightFlags & VolumetricDepthFlag) != 0 &&
                                       stressFailureCount == 0;
            }
            finally
            {
                DisposeSmokeArray(ref matrices);
                DisposeSmokeArray(ref inputs);
                DisposeSmokeArray(ref results);
                DisposeSmokeArray(ref expectedBiomeIds);
                DisposeSmokeArray(ref expectedFlagMasks);
                DisposeSmokeArray(ref auditResults);
                DisposeSmokeArray(ref blockSummaries);
                DisposeSmokeArray(ref summary);
                sentinelAfter = NativeMemorySentinel.ActiveAllocationCount;
            }

            int sentinelDelta = sentinelAfter - sentinelBefore;
            bool passed = classificationPassed && sentinelDelta == 0;
            report = new VolumetricBiomeSmokeReport(
                passed,
                shallowBiomeId,
                twilightBiomeId,
                hadalBiomeId,
                twilightFlags,
                StressSampleCount,
                stressFailureCount,
                packedChecksum,
                sentinelBefore,
                sentinelAfter,
                sentinelDelta);
            return passed;
        }

        public static void RunBatchmode()
        {
            bool passed = RunHeadlessSmokeTest(out VolumetricBiomeSmokeReport report);

            Debug.Log(
                $"[VolumetricBiomeSmokeTester] {(passed ? "PASS" : "FAIL")} " +
                $"shallow={report.ShallowBiomeId} twilight={report.TwilightBiomeId} hadal={report.HadalBiomeId} " +
                $"flags={report.TwilightFlags} stressSamples={report.StressSampleCount} stressFailures={report.StressFailureCount} " +
                $"sentinelDelta={report.SentinelDelta} packedChecksum={report.PackedChecksum}");

#if UNITY_EDITOR
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
#endif
        }

        private static NativeArray<T> AllocateSmokeArray<T>(int length, string label)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
            return array;
        }

        private static void DisposeSmokeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static WorldProceduralFieldSampler.BiomeMatrixData BuildMatrix(
            int matrixIndex,
            int familyDataIndex,
            float minDepth,
            float maxDepth)
        {
            return new WorldProceduralFieldSampler.BiomeMatrixData
            {
                MatrixIndex = matrixIndex,
                FamilyDataIndex = familyDataIndex,
                MinDepthMeters = minDepth,
                MaxDepthMeters = maxDepth,
                IsPlaceholder = 0
            };
        }
    }
}
