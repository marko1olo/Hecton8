#pragma warning disable 619
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    public static class PlanetaryCanvasSmokeTester
    {
        private const string NativeMemoryOwner = nameof(PlanetaryCanvasSmokeTester);
        private const int Resolution = 16;
        private const int CellCount = Resolution * Resolution;

        public struct Result
        {
            public byte Passed;
            public float FlatSandWeight;
            public float SteepRockWeight;
            public float SiltWeight;
            public float CavityWeight;
            public float SlopeWeight;
            public float MacroMaterialDelta;
            public float MacroSandWeight;
            public float MacroRockWeight;
            public float MacroSiltWeight;
            public uint Checksum;
            public uint MacroChecksum;
        }

        public static Result RunSlopeCavitySplatmapSmoke()
        {
            NativeArray<float> heights = default;
            NativeArray<float> sediment = default;
            NativeArray<float4> weights = default;
            NativeArray<float4> macroWeights = default;
            NativeArray<float> slopeWeights = default;
            NativeArray<float> macroSlopeWeights = default;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                heights = AllocateTrackedTempJobArray<float>(CellCount, "heights", NativeArrayOptions.UninitializedMemory);
                sediment = AllocateTrackedTempJobArray<float>(CellCount, "sediment", NativeArrayOptions.ClearMemory);
                weights = AllocateTrackedTempJobArray<float4>(CellCount, "weights", NativeArrayOptions.UninitializedMemory);
                macroWeights = AllocateTrackedTempJobArray<float4>(CellCount, "macroWeights", NativeArrayOptions.UninitializedMemory);
                slopeWeights = AllocateTrackedTempJobArray<float>(CellCount, "slopeWeights", NativeArrayOptions.UninitializedMemory);
                macroSlopeWeights = AllocateTrackedTempJobArray<float>(CellCount, "macroSlopeWeights", NativeArrayOptions.UninitializedMemory);

                FillSmokeInputs(heights, sediment);

                NativeArray<float4> control1 = AllocateTrackedTempJobArray<float4>(CellCount, "control1", NativeArrayOptions.UninitializedMemory);
                NativeArray<float4> control2 = AllocateTrackedTempJobArray<float4>(CellCount, "control2", NativeArrayOptions.UninitializedMemory);
                NativeArray<int> dominantIndices = AllocateTrackedTempJobArray<int>(CellCount, "dominantIndices", NativeArrayOptions.UninitializedMemory);

                try
                {
                    WorldMacroGeologyParams macroParams = WorldMacroGeologyParams.CreateDefault(880031u);
                    macroParams.WaterSurfaceY = 0f;

                    WorldTerrainSurfaceMaterialMaskJob job = new WorldTerrainSurfaceMaterialMaskJob
                    {
                        HeightBufferMeters = heights,
                        Primary = weights,
                        Secondary = macroWeights,
                        Control1 = control1,
                        Control2 = control2,
                        Slope01 = slopeWeights,
                        Curvature01 = macroSlopeWeights,
                        DominantMaterialIndex = dominantIndices,
                        Width = Resolution,
                        Height = Resolution,
                        HeightBufferResolution = Resolution,
                        CellSizeMeters = 1f,
                        HeightCellSizeMeters = 1f,
                        WorldOriginXZ = new double2(5000.0, 5000.0),
                        MacroGeologyParams = macroParams,
                        MaskContrast = 1.2f
                    };

                    handle = job.Schedule(CellCount, 16);
                    scheduled = true;
                    handle.Complete();
                }
                finally
                {
                    DisposeTracked(ref control1);
                    DisposeTracked(ref control2);
                    DisposeTracked(ref dominantIndices);
                }

                Result result = InspectSmokeOutput(weights, slopeWeights);

                Result macroResult = InspectSmokeOutput(macroWeights, macroSlopeWeights);
                result.MacroMaterialDelta = CalculateAverageWeightDelta(weights, macroWeights);
                result.MacroSandWeight = macroResult.FlatSandWeight;
                result.MacroRockWeight = macroResult.SteepRockWeight;
                result.MacroSiltWeight = macroResult.SiltWeight;
                result.MacroChecksum = macroResult.Checksum;
                result.Passed = (result.FlatSandWeight >= 0.05f &&
                    result.SteepRockWeight >= 0.05f &&
                    result.MacroMaterialDelta >= 0.001f &&
                    result.MacroChecksum != result.Checksum) ? (byte)1 : (byte)0;
                return result;
            }
            finally
            {
                if (scheduled)
                {
                    // COLD SYNC JOB: smoke teardown guard.
                    DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                }

                DisposeTracked(ref heights);
                DisposeTracked(ref sediment);
                DisposeTracked(ref weights);
                DisposeTracked(ref macroWeights);
                DisposeTracked(ref slopeWeights);
                DisposeTracked(ref macroSlopeWeights);
            }
        }

        private static void FillSmokeInputs(NativeArray<float> heights, NativeArray<float> sediment)
        {
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    int index = z * Resolution + x;
                    float height = x < 6 ? 0.2f : 0.2f + (x - 5) * 0.05f;
                    if (x == 3 && z == 8)
                        height -= 0.12f;

                    heights[index] = math.saturate(height);
                    sediment[index] = x == 3 && z == 8 ? 0.85f : 0f;
                }
            }
        }

        private static Result InspectSmokeOutput(NativeArray<float4> weights, NativeArray<float> slopeWeights)
        {
            float4 flat = weights[2 * Resolution + 2];
            float4 steep = weights[8 * Resolution + 8];
            float4 sedimentCell = weights[8 * Resolution + 3];
            float slopeWeight = slopeWeights[8 * Resolution + 8];
            uint checksum = 2166136261u;
            for (int i = 0; i < weights.Length; i++)
            {
                float4 value = math.saturate(weights[i]);
                checksum = Fold(checksum, value.x);
                checksum = Fold(checksum, value.y);
                checksum = Fold(checksum, value.z);
                checksum = Fold(checksum, value.w);
                checksum = Fold(checksum, slopeWeights[i]);
            }

            return new Result
            {
                FlatSandWeight = flat.x,
                SteepRockWeight = steep.y,
                SiltWeight = sedimentCell.z,
                CavityWeight = sedimentCell.w,
                SlopeWeight = slopeWeight,
                Checksum = checksum
            };
        }

        private static uint Fold(uint checksum, float value)
        {
            uint packed = (uint)math.round(math.saturate(value) * 65535f);
            checksum ^= packed;
            return checksum * 16777619u;
        }

        private static float CalculateAverageWeightDelta(NativeArray<float4> baseline, NativeArray<float4> candidate)
        {
            int count = math.min(baseline.Length, candidate.Length);
            if (count <= 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < count; i++)
                total += math.csum(math.abs(math.saturate(candidate[i]) - math.saturate(baseline[i])));

            return total / count;
        }

        private static unsafe void DisposeTracked<T>(ref NativeArray<T> array)
            where T : struct
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
            throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }
    }
}
