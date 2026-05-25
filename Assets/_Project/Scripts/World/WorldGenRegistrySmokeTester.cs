using Hecton8.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path smoke harness for registry-backed world-generation runtime ownership.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/World/World Gen Registry Smoke Tester")]
    public sealed class WorldGenRegistrySmokeTester : MonoBehaviour
    {
        public readonly struct WorldGenRegistrySmokeReport
        {
            public const int ExpectedRegistryQueueNativeAllocationDelta = 2;

            public WorldGenRegistrySmokeReport(
                bool passed,
                bool fieldSamplerRegistered,
                bool fieldSamplerReleased,
                bool resourceDistributionRegistered,
                bool resourceDistributionReleased,
                bool geologyTerrainSeamRegistered,
                bool geologyTerrainSeamReleased,
                bool geologyVoxelBridgeRegistered,
                bool geologyVoxelBridgeReleased,
                bool worldGenRegistered,
                bool worldGenReleased,
                int pendingReboundCountBefore,
                int pendingReboundCountAfter,
                int nativeAllocationCountBefore,
                int nativeAllocationCountAfter,
                int expectedNativeAllocationDelta)
            {
                Passed = passed ? (byte)1 : (byte)0;
                FieldSamplerRegistered = fieldSamplerRegistered ? (byte)1 : (byte)0;
                FieldSamplerReleased = fieldSamplerReleased ? (byte)1 : (byte)0;
                ResourceDistributionRegistered = resourceDistributionRegistered ? (byte)1 : (byte)0;
                ResourceDistributionReleased = resourceDistributionReleased ? (byte)1 : (byte)0;
                GeologyTerrainSeamRegistered = geologyTerrainSeamRegistered ? (byte)1 : (byte)0;
                GeologyTerrainSeamReleased = geologyTerrainSeamReleased ? (byte)1 : (byte)0;
                GeologyVoxelBridgeRegistered = geologyVoxelBridgeRegistered ? (byte)1 : (byte)0;
                GeologyVoxelBridgeReleased = geologyVoxelBridgeReleased ? (byte)1 : (byte)0;
                WorldGenRegistered = worldGenRegistered ? (byte)1 : (byte)0;
                WorldGenReleased = worldGenReleased ? (byte)1 : (byte)0;
                PendingReboundCountBefore = pendingReboundCountBefore;
                PendingReboundCountAfter = pendingReboundCountAfter;
                NativeAllocationCountBefore = nativeAllocationCountBefore;
                NativeAllocationCountAfter = nativeAllocationCountAfter;
                ExpectedNativeAllocationDelta = expectedNativeAllocationDelta;
                NativeAllocationDelta = nativeAllocationCountAfter - nativeAllocationCountBefore;
                NativeAllocationDeltaWithinExpectedRegistryQueueBudget =
                    NativeAllocationDelta >= 0 && NativeAllocationDelta <= ExpectedNativeAllocationDelta
                        ? (byte)1
                        : (byte)0;
            }

            public readonly byte Passed;
            public readonly byte FieldSamplerRegistered;
            public readonly byte FieldSamplerReleased;
            public readonly byte ResourceDistributionRegistered;
            public readonly byte ResourceDistributionReleased;
            public readonly byte GeologyTerrainSeamRegistered;
            public readonly byte GeologyTerrainSeamReleased;
            public readonly byte GeologyVoxelBridgeRegistered;
            public readonly byte GeologyVoxelBridgeReleased;
            public readonly byte WorldGenRegistered;
            public readonly byte WorldGenReleased;
            public readonly int PendingReboundCountBefore;
            public readonly int PendingReboundCountAfter;
            public readonly int NativeAllocationCountBefore;
            public readonly int NativeAllocationCountAfter;
            public readonly int NativeAllocationDelta;
            public readonly int ExpectedNativeAllocationDelta;
            public readonly byte NativeAllocationDeltaWithinExpectedRegistryQueueBudget;
        }

        [ContextMenu("Run World Gen Registry Smoke Test")]
        public void RunSmokeTest()
        {
            RunHeadlessSmokeTest(out WorldGenRegistrySmokeReport report);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log(
                report.Passed != 0
                    ? "[WorldGenRegistrySmokeTester] PASS"
                    : "[WorldGenRegistrySmokeTester] FAIL",
                this);
#endif
        }

        public static bool RunHeadlessSmokeTest(out WorldGenRegistrySmokeReport report)
        {
            int pendingBefore = GlobalRegistry.PendingServiceReboundCount;
            int nativeBefore = GlobalRegistry.NativeAllocationCount;
            bool fieldSamplerRegistered = false;
            bool fieldSamplerReleased = false;
            bool resourceDistributionRegistered = false;
            bool resourceDistributionReleased = false;
            bool geologyTerrainSeamRegistered = false;
            bool geologyTerrainSeamReleased = false;
            bool geologyVoxelBridgeRegistered = false;
            bool geologyVoxelBridgeReleased = false;
            bool worldGenRegistered = false;
            bool worldGenReleased = false;

            GameObject fieldSamplerObject = null;
            GameObject resourceObject = null;
            GameObject terrainSeamObject = null;
            GameObject voxelBridgeObject = null;
            SmokeWorldGenService worldGenService = null;

            try
            {
                if (GlobalRegistry.ProceduralFieldSampler == null)
                {
                    fieldSamplerObject = new GameObject("[WorldGenRegistrySmokeTester.FieldSampler]"); // COLD ALLOC: GameObject[1] - registry smoke field sampler owner - owner: WorldGenRegistrySmokeTester
                    WorldProceduralFieldSampler fieldSampler = fieldSamplerObject.AddComponent<WorldProceduralFieldSampler>();
                    GlobalRegistry.RegisterProceduralFieldSampler(fieldSampler);
                    fieldSamplerRegistered = ReferenceEquals(GlobalRegistry.ProceduralFieldSampler, fieldSampler) &&
                                             ReferenceEquals(WorldProceduralFieldSampler.ActiveRuntimeInstance, fieldSampler);
                    GlobalRegistry.UnregisterProceduralFieldSampler(fieldSampler);
                    Object.DestroyImmediate(fieldSamplerObject);
                    fieldSamplerObject = null;
                    fieldSamplerReleased = GlobalRegistry.ProceduralFieldSampler == null &&
                                           WorldProceduralFieldSampler.ActiveRuntimeInstance == null;
                }

                if (GlobalRegistry.ResourceDistribution == null)
                {
                    resourceObject = new GameObject("[WorldGenRegistrySmokeTester.ResourceDistribution]"); // COLD ALLOC: GameObject[1] - registry smoke resource-distribution owner - owner: WorldGenRegistrySmokeTester
                    ResourceDistributionDirector resourceDistribution = resourceObject.AddComponent<ResourceDistributionDirector>();
                    GlobalRegistry.RegisterResourceDistribution(resourceDistribution);
                    resourceDistributionRegistered = ReferenceEquals(GlobalRegistry.ResourceDistribution, resourceDistribution) &&
                                                     ReferenceEquals(ResourceDistributionDirector.ActiveRuntimeInstance, resourceDistribution);
                    GlobalRegistry.UnregisterResourceDistribution(resourceDistribution);
                    resourceDistributionReleased = GlobalRegistry.ResourceDistribution == null &&
                                                   ResourceDistributionDirector.ActiveRuntimeInstance == null;
                    Object.DestroyImmediate(resourceObject);
                    resourceObject = null;
                }

                if (GlobalRegistry.GeologyTerrainSeam == null)
                {
                    terrainSeamObject = new GameObject("[WorldGenRegistrySmokeTester.TerrainSeam]"); // COLD ALLOC: GameObject[1] - registry smoke geology terrain seam owner - owner: WorldGenRegistrySmokeTester
                    WorldGenerativeGeologyTerrainSeamApplier terrainSeam = terrainSeamObject.AddComponent<WorldGenerativeGeologyTerrainSeamApplier>();
                    GlobalRegistry.RegisterGeologyTerrainSeamRuntime(terrainSeam);
                    geologyTerrainSeamRegistered = ReferenceEquals(GlobalRegistry.GeologyTerrainSeam, terrainSeam) &&
                                                   ReferenceEquals(WorldGenerativeGeologyTerrainSeamApplier.ActiveRuntimeInstance, terrainSeam);
                    GlobalRegistry.UnregisterGeologyTerrainSeamRuntime(terrainSeam);
                    geologyTerrainSeamReleased = GlobalRegistry.GeologyTerrainSeam == null &&
                                                 WorldGenerativeGeologyTerrainSeamApplier.ActiveRuntimeInstance == null;
                    Object.DestroyImmediate(terrainSeamObject);
                    terrainSeamObject = null;
                }

                if (GlobalRegistry.GeologyVoxelBridge == null)
                {
                    voxelBridgeObject = new GameObject("[WorldGenRegistrySmokeTester.VoxelBridge]"); // COLD ALLOC: GameObject[1] - registry smoke geology voxel bridge owner - owner: WorldGenRegistrySmokeTester
                    WorldGenerativeGeologyVoxelBridgeDirector voxelBridge = voxelBridgeObject.AddComponent<WorldGenerativeGeologyVoxelBridgeDirector>();
                    GlobalRegistry.RegisterGeologyVoxelBridgeRuntime(voxelBridge);
                    geologyVoxelBridgeRegistered = ReferenceEquals(GlobalRegistry.GeologyVoxelBridge, voxelBridge) &&
                                                   ReferenceEquals(WorldGenerativeGeologyVoxelBridgeDirector.ActiveRuntimeInstance, voxelBridge);
                    GlobalRegistry.UnregisterGeologyVoxelBridgeRuntime(voxelBridge);
                    geologyVoxelBridgeReleased = GlobalRegistry.GeologyVoxelBridge == null &&
                                                 WorldGenerativeGeologyVoxelBridgeDirector.ActiveRuntimeInstance == null;
                    Object.DestroyImmediate(voxelBridgeObject);
                    voxelBridgeObject = null;
                }

                if (GlobalRegistry.WorldGen == null)
                {
                    worldGenService = new SmokeWorldGenService(); // COLD ALLOC: SmokeWorldGenService[1] - registry smoke service stub - owner: WorldGenRegistrySmokeTester
                    GlobalRegistry.RegisterWorldGenService(worldGenService);
                    worldGenRegistered = ReferenceEquals(GlobalRegistry.WorldGen, worldGenService);
                    GlobalRegistry.UnregisterWorldGenService(worldGenService);
                    worldGenReleased = GlobalRegistry.WorldGen == null;
                }
            }
            finally
            {
                if (fieldSamplerObject != null)
                    Object.DestroyImmediate(fieldSamplerObject);

                if (resourceObject != null)
                    Object.DestroyImmediate(resourceObject);

                if (terrainSeamObject != null)
                    Object.DestroyImmediate(terrainSeamObject);

                if (voxelBridgeObject != null)
                    Object.DestroyImmediate(voxelBridgeObject);

                if (worldGenService != null && ReferenceEquals(GlobalRegistry.WorldGen, worldGenService))
                    GlobalRegistry.UnregisterWorldGenService(worldGenService);
            }

            GlobalRegistry.FlushPendingServiceReboundEvents();

            int pendingAfter = GlobalRegistry.PendingServiceReboundCount;
            int nativeAfter = GlobalRegistry.NativeAllocationCount;
            bool passed =
                fieldSamplerRegistered &&
                fieldSamplerReleased &&
                resourceDistributionRegistered &&
                resourceDistributionReleased &&
                geologyTerrainSeamRegistered &&
                geologyTerrainSeamReleased &&
                geologyVoxelBridgeRegistered &&
                geologyVoxelBridgeReleased &&
                worldGenRegistered &&
                worldGenReleased &&
                pendingAfter == 0 &&
                nativeAfter >= nativeBefore &&
                nativeAfter - nativeBefore <= WorldGenRegistrySmokeReport.ExpectedRegistryQueueNativeAllocationDelta;

            report = new WorldGenRegistrySmokeReport(
                passed,
                fieldSamplerRegistered,
                fieldSamplerReleased,
                resourceDistributionRegistered,
                resourceDistributionReleased,
                geologyTerrainSeamRegistered,
                geologyTerrainSeamReleased,
                geologyVoxelBridgeRegistered,
                geologyVoxelBridgeReleased,
                worldGenRegistered,
                worldGenReleased,
                pendingBefore,
                pendingAfter,
                nativeBefore,
                nativeAfter,
                WorldGenRegistrySmokeReport.ExpectedRegistryQueueNativeAllocationDelta);
            return passed;
        }

        public static void RunBatchmode()
        {
            bool passed = RunHeadlessSmokeTest(out WorldGenRegistrySmokeReport report);
            Hecton8.Core.H8Debug.Log(
                "[WorldGenRegistrySmokeTester] " +
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

#if UNITY_EDITOR
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
#endif
        }

        private static int Bool01(byte value)
        {
            return value != 0 ? 1 : 0;
        }

        private sealed class SmokeWorldGenService : IWorldGenService
        {
            public bool IsInitialized => ReferenceEquals(GlobalRegistry.WorldGen, this);

            public bool TryPrimeBootstrapScatterPass()
            {
                return true;
            }

            public void RebuildScatterPreview()
            {
            }

            public void ClearScatterPreview()
            {
            }
        }
    }
}
