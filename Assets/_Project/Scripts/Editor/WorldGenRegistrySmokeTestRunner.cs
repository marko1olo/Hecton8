using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class WorldGenRegistrySmokeTestRunner
    {
        public static void Run()
        {
            bool passed = WorldGenRegistrySmokeTester.RunHeadlessSmokeTest(
                out WorldGenRegistrySmokeTester.WorldGenRegistrySmokeReport report);

            Debug.Log(
                "[WorldGenRegistrySmokeTestRunner] " +
                "{\"tester\":\"WorldGenRegistrySmokeTester\"," +
                "\"status\":\"" + (passed ? "PASS" : "FAIL") + "\"," +
                "\"fieldSamplerRegistered\":" + Bool01(report.FieldSamplerRegistered) + "," +
                "\"fieldSamplerReleased\":" + Bool01(report.FieldSamplerReleased) + "," +
                "\"resourceDistributionRegistered\":" + Bool01(report.ResourceDistributionRegistered) + "," +
                "\"resourceDistributionReleased\":" + Bool01(report.ResourceDistributionReleased) + "," +
                "\"geologyTerrainSeamRegistered\":" + Bool01(report.GeologyTerrainSeamRegistered) + "," +
                "\"geologyTerrainSeamReleased\":" + Bool01(report.GeologyTerrainSeamReleased) + "," +
                "\"geologyVoxelBridgeRegistered\":" + Bool01(report.GeologyVoxelBridgeRegistered) + "," +
                "\"geologyVoxelBridgeReleased\":" + Bool01(report.GeologyVoxelBridgeReleased) + "," +
                "\"worldGenRegistered\":" + Bool01(report.WorldGenRegistered) + "," +
                "\"worldGenReleased\":" + Bool01(report.WorldGenReleased) + "," +
                "\"pendingBefore\":" + report.PendingReboundCountBefore + "," +
                "\"pendingAfter\":" + report.PendingReboundCountAfter + "," +
                "\"nativeBefore\":" + report.NativeAllocationCountBefore + "," +
                "\"nativeAfter\":" + report.NativeAllocationCountAfter + "," +
                "\"nativeDelta\":" + report.NativeAllocationDelta + "," +
                "\"expectedNativeDeltaMax\":" + report.ExpectedNativeAllocationDelta + "}");

            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static int Bool01(byte value)
        {
            return value != 0 ? 1 : 0;
        }
    }
}
