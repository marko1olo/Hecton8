using Hecton8.Core;
using Unity.Collections;
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
            public uint Checksum;
        }

        public static Result RunSlopeCavitySplatmapSmoke()
        {
            NativeArray<float> heights = default;
            NativeArray<float> sediment = default;
            NativeArray<float4> weights = default;
            NativeArray<float> slopeWeights = default;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                heights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sediment = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weights = new NativeArray<float4>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                slopeWeights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(heights, NativeMemoryOwner, "heights", NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(sediment, NativeMemoryOwner, "sediment", NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(weights, NativeMemoryOwner, "weights", NativeAllocationLifetime.TempJob);
                NativeMemorySentinel.RegisterNativeArray(slopeWeights, NativeMemoryOwner, "slopeWeights", NativeAllocationLifetime.TempJob);

                FillSmokeInputs(heights, sediment);

                var job = new WorldProceduralTerrainSlopeCavitySplatmapJob
                {
                    Heights01 = heights,
                    Sediment01 = sediment,
                    Weights = weights,
                    SlopeWeights01 = slopeWeights,
                    Width = Resolution,
                    Height = Resolution,
                    CellSizeMeters = 1f,
                    HeightScaleMeters = 40f,
                    RockSlopeThresholdDegrees = 45f,
                    SlopeBlendWidthDegrees = 5f,
                    CavityStrength = 0.16f,
                    SedimentStrength = 1f
                };

                handle = job.Schedule(CellCount, 32);
                scheduled = true;

                // COLD SYNC JOB: editor smoke test must inspect concrete kernel output.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                scheduled = false;

                Result result = InspectSmokeOutput(weights, slopeWeights);
                result.Passed = (result.FlatSandWeight >= 0.85f &&
                    result.SteepRockWeight >= 0.55f &&
                    result.SiltWeight >= 0.45f &&
                    result.CavityWeight >= 0.25f &&
                    result.SlopeWeight >= 0.55f) ? (byte)1 : (byte)0;
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
                DisposeTracked(ref slopeWeights);
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

        private static void DisposeTracked<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }
    }
}
