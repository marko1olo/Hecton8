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
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool logResult = true;
        [SerializeField] private bool _debugPassed;
        [SerializeField] private int _debugShallowBiomeId;
        [SerializeField] private int _debugTwilightBiomeId;
        [SerializeField] private int _debugHadalBiomeId;
        [SerializeField] private int _debugTwilightFlags;

        /// <summary>
        /// True when the most recent smoke run passed.
        /// </summary>
        public bool LastRunPassed => _debugPassed;

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
            _debugPassed = RunHeadlessSmokeTest(
                out _debugShallowBiomeId,
                out _debugTwilightBiomeId,
                out _debugHadalBiomeId,
                out _debugTwilightFlags);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logResult)
            {
                Debug.Log(
                    _debugPassed
                        ? "[VolumetricBiomeSmokeTester] PASS"
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
            shallowBiomeId = 0;
            twilightBiomeId = 0;
            hadalBiomeId = 0;
            twilightFlags = 0;

            NativeArray<WorldProceduralFieldSampler.BiomeMatrixData> matrices = default;
            NativeArray<VolumetricBiomeClassificationInput> inputs = default;
            NativeArray<VolumetricBiomeClassificationResult> results = default;

            try
            {
                // COLD ALLOC: NativeArray<BiomeMatrixData>[3] - volumetric biome smoke fixture - owner: VolumetricBiomeSmokeTester
                matrices = new NativeArray<WorldProceduralFieldSampler.BiomeMatrixData>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeArray<VolumetricBiomeClassificationInput>[3] - smoke job input - owner: VolumetricBiomeSmokeTester
                inputs = new NativeArray<VolumetricBiomeClassificationInput>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeArray<VolumetricBiomeClassificationResult>[3] - smoke job output - owner: VolumetricBiomeSmokeTester
                results = new NativeArray<VolumetricBiomeClassificationResult>(3, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                matrices[0] = BuildMatrix(11, 7, 0f, 500f);
                matrices[1] = BuildMatrix(12, 7, 500f, 2000f);
                matrices[2] = BuildMatrix(13, 7, 2000f, 5000f);

                inputs[0] = BuildInput(100f, 0, 7);
                inputs[1] = BuildInput(1000f, 0, 7);
                inputs[2] = BuildInput(3500f, 0, 7);

                VolumetricBiomeClassificationJob job = new VolumetricBiomeClassificationJob
                {
                    Inputs = inputs,
                    BiomeMatrices = matrices,
                    Results = results,
                    BiomeMatrixCount = matrices.Length
                };

                job.Schedule(inputs.Length, 1).Complete();

                shallowBiomeId = results[0].InfluenceCell.PrimaryBiomeId;
                twilightBiomeId = results[1].InfluenceCell.PrimaryBiomeId;
                hadalBiomeId = results[2].InfluenceCell.PrimaryBiomeId;
                twilightFlags = results[1].InfluenceCell.Flags;

                return shallowBiomeId == 11 &&
                       twilightBiomeId == 12 &&
                       hadalBiomeId == 13 &&
                       (twilightFlags & (byte)WorldProceduralFieldSampler.BiomeInfluenceFlags.VolumetricDepth) != 0;
            }
            finally
            {
                if (matrices.IsCreated) matrices.Dispose();
                if (inputs.IsCreated) inputs.Dispose();
                if (results.IsCreated) results.Dispose();
            }
        }

        public static void RunBatchmode()
        {
            bool passed = RunHeadlessSmokeTest(
                out int shallowBiomeId,
                out int twilightBiomeId,
                out int hadalBiomeId,
                out int twilightFlags);

            Debug.Log(
                $"[VolumetricBiomeSmokeTester] {(passed ? "PASS" : "FAIL")} " +
                $"shallow={shallowBiomeId} twilight={twilightBiomeId} hadal={hadalBiomeId} flags={twilightFlags}");

#if UNITY_EDITOR
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
#endif
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

        private static VolumetricBiomeClassificationInput BuildInput(
            float depthMeters,
            int primaryBiomeMatrixDataIndex,
            int preferredFamilyDataIndex)
        {
            return new VolumetricBiomeClassificationInput
            {
                DepthMeters = depthMeters,
                PrimaryBiomeMatrixDataIndex = primaryBiomeMatrixDataIndex,
                PreferredFamilyDataIndex = preferredFamilyDataIndex,
                SecondaryBiomeMatrixDataIndex = -1,
                Blend255 = 0,
                Flags = 0
            };
        }
    }
}
