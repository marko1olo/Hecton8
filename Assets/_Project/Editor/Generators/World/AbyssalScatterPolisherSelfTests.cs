#if UNITY_EDITOR
using System.Diagnostics;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Generators.World
{
    internal static class AbyssalScatterPolisherSelfTests
    {
        [MenuItem("Hecton8/World Scatter/1614 Run Self Tests")]
        public static void RunSelfTestsMenu()
        {
            bool passed = RunSelfTests(out string report);
            if (passed)
            {
                Debug.Log(report);
                return;
            }

            Debug.LogError(report);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        internal static bool RunSelfTests(out string report)
        {
            Stopwatch timer = Stopwatch.StartNew();
            bool layout = RunLayoutTest();
            bool determinant = RunDeterminantTest();
            bool boundsCap = RunBoundsCapTest();
            bool fuzzer = AbyssalScatterPolisherPipeline.RunFuzzerForKnownInsideBounds(out int culledCount);
            bool stress = AbyssalScatterPolisherPipeline.BakeMockScatterChunk(
                100000,
                500,
                1f,
                "scatter_1614_stress_100k.brgdata",
                out AbyssalScatterBakeResult stressResult);
            timer.Stop();

            bool stressBudget = stress && stressResult.CullingMilliseconds <= 150f;
            bool passed = layout && determinant && boundsCap && fuzzer && stress && stressBudget;
            report = "[1614] SelfTests layout=" + layout +
                     " determinant=" + determinant +
                     " boundsCap=" + boundsCap +
                     " forcedInsideCulled=" + culledCount +
                     " stress=" + stress +
                     " stressCullMs=" + stressResult.CullingMilliseconds.ToString("0.###") +
                     " totalMs=" + timer.Elapsed.TotalMilliseconds.ToString("0.###") +
                     " status=" + (passed ? "PASS" : "FAIL");
            return passed;
        }

        private static bool RunLayoutTest()
        {
            try
            {
                AbyssalScatterPolisherPipeline.ValidateLayoutsOrThrow();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool RunDeterminantTest()
        {
            float scale = 1.75f;
            float3 normal = math.normalize(new float3(0.42f, 1f, -0.27f));
            quaternion rotation = AlignScatterToTerrainNormalJob.BuildNormalAlignedRotation(normal, 1.25f);
            float4x4 matrix = float4x4.TRS(new float3(3f, -7f, 11f), rotation, new float3(scale));
            return AbyssalScatterPolisherPipeline.ValidateMatrixDeterminant(matrix, scale, 0.0001f);
        }

        private static bool RunBoundsCapTest()
        {
            return AbyssalScatterPolisherPipeline.IsCullingBoundsCountWithinBakeCap(AbyssalScatterPolisherPipeline.MaxCullingBounds) &&
                   !AbyssalScatterPolisherPipeline.IsCullingBoundsCountWithinBakeCap(AbyssalScatterPolisherPipeline.MaxCullingBounds + 1);
        }
    }
}
#endif
