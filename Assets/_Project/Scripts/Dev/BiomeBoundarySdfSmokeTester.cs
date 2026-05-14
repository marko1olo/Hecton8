#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Hecton8.Core;
using Hecton8.World.Biomes;
using Hecton8.World.Biomes.Contracts;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Dev
{
    /// <summary>
    /// Dev-only probe for deterministic biome boundary SDF blending.
    /// </summary>
    public static class BiomeBoundarySdfSmokeTester
    {
        private const string NativeMemoryOwner = nameof(BiomeBoundarySdfSmokeTester);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.TempJob;
        private const int Resolution = 5;
        private const int CellCount = Resolution * Resolution;
        private const float CellSizeMeters = 10f;
        private const uint BiomeAHash = 0xA6100001u;
        private const uint BiomeBHash = 0xB7100002u;

        public static bool Run(out string json)
        {
            int nativeAllocationsBefore = NativeMemorySentinel.ActiveAllocationCount;
            long nativeBytesBefore = NativeMemorySentinel.TrackedBytes;

            NativeArray<byte> map = default;
            NativeArray<uint> hashes = default;
            NativeArray<BiomeBoundarySdfResult> result = default;
            bool boundaryPass = false;
            bool lowTierPass = false;
            bool outOfBoundsPass = false;
            bool hashCollisionPass = false;

            try
            {
                // COLD ALLOC: NativeArray biome smoke buffers - dev-only deterministic SDF probe - owner: BiomeBoundarySdfSmokeTester
                map = new NativeArray<byte>(CellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                hashes = new NativeArray<uint>(CellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                result = new NativeArray<BiomeBoundarySdfResult>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                Register(map, nameof(map));
                Register(hashes, nameof(hashes));
                Register(result, nameof(result));

                FillTwoBiomeMap(map, hashes, collideBytes: false);
                boundaryPass = RunSample(map, hashes, result, new double2(20d, 20d), 2, BiomeBoundarySdfFlags.None, out BiomeBoundarySdfResult boundary) &&
                               boundary.BlendFactor01 > 0f &&
                               boundary.BiomeA != 0 &&
                               boundary.BiomeB != 0 &&
                               boundary.BiomeA != boundary.BiomeB &&
                               (boundary.Flags & (byte)BiomeBoundarySdfFlags.HasSecondaryBiome) != 0;

                lowTierPass = RunSample(map, hashes, result, new double2(20d, 20d), 1, BiomeBoundarySdfFlags.LowTierKernel, out BiomeBoundarySdfResult lowTier) &&
                              lowTier.SampleDiameter == 3 &&
                              (lowTier.Flags & (byte)BiomeBoundarySdfFlags.LowTierKernel) != 0;

                outOfBoundsPass = RunSample(map, hashes, result, new double2(-80d, 20d), 2, BiomeBoundarySdfFlags.None, out BiomeBoundarySdfResult outOfBounds) &&
                                  outOfBounds.BiomeA == 1 &&
                                  math.isfinite(outOfBounds.BlendFactor01) &&
                                  (outOfBounds.Flags & (byte)BiomeBoundarySdfFlags.OutOfBounds) != 0;

                FillTwoBiomeMap(map, hashes, collideBytes: true);
                hashCollisionPass = RunSample(map, hashes, result, new double2(20d, 20d), 2, BiomeBoundarySdfFlags.None, out BiomeBoundarySdfResult collision) &&
                                    collision.BiomeA == collision.BiomeB &&
                                    collision.BiomeAHash != 0u &&
                                    collision.BiomeBHash != 0u &&
                                    collision.BiomeAHash != collision.BiomeBHash &&
                                    collision.BlendFactor01 > 0f;
            }
            finally
            {
                DisposeTracked(ref result);
                DisposeTracked(ref hashes);
                DisposeTracked(ref map);
            }

            int nativeAllocationDelta = NativeMemorySentinel.ActiveAllocationCount - nativeAllocationsBefore;
            long nativeByteDelta = NativeMemorySentinel.TrackedBytes - nativeBytesBefore;
            bool nativeBalancePass = nativeAllocationDelta == 0 && nativeByteDelta == 0L;
            bool pass = boundaryPass && lowTierPass && outOfBoundsPass && hashCollisionPass && nativeBalancePass;

            json = "{\"tester\":\"BiomeBoundarySdfSmokeTester\"," +
                   "\"pass\":" + ToJsonBool(pass) + "," +
                   "\"boundary\":" + ToJsonBool(boundaryPass) + "," +
                   "\"lowTier\":" + ToJsonBool(lowTierPass) + "," +
                   "\"outOfBounds\":" + ToJsonBool(outOfBoundsPass) + "," +
                   "\"hashCollision\":" + ToJsonBool(hashCollisionPass) + "," +
                   "\"nativeAllocationDelta\":" + nativeAllocationDelta + "," +
                   "\"nativeByteDelta\":" + nativeByteDelta + "}";
            return pass;
        }

        private static bool RunSample(
            NativeArray<byte> map,
            NativeArray<uint> hashes,
            NativeArray<BiomeBoundarySdfResult> result,
            double2 sampleAupXZ,
            int sampleRadius,
            BiomeBoundarySdfFlags flags,
            out BiomeBoundarySdfResult sample)
        {
            result[0] = default;
            var job = new BiomeBoundarySdfJobs.BiomeBoundarySdfSampleJob
            {
                GlobalBiomeMap = map,
                BiomeHashMap = hashes,
                Result = result,
                Settings = new BiomeBoundarySdfSettings
                {
                    Resolution = new int2(Resolution, Resolution),
                    OriginAupXZ = double2.zero,
                    CellSizeMeters = CellSizeMeters,
                    BlendWidthMeters = CellSizeMeters,
                    SampleRadiusCells = sampleRadius,
                    Flags = (byte)flags
                },
                SampleAupXZ = sampleAupXZ
            };

            job.Run();
            sample = result[0];
            return sample.BiomeA != 0 &&
                   math.isfinite(sample.BlendFactor01) &&
                   math.isfinite(sample.BoundaryDistanceMeters) &&
                   math.isfinite(sample.PrimaryWeight) &&
                   math.isfinite(sample.SecondaryWeight);
        }

        private static void FillTwoBiomeMap(NativeArray<byte> map, NativeArray<uint> hashes, bool collideBytes)
        {
            for (int y = 0; y < Resolution; y++)
            {
                int rowOffset = y * Resolution;
                for (int x = 0; x < Resolution; x++)
                {
                    bool rightBiome = x >= 2;
                    int index = rowOffset + x;
                    map[index] = collideBytes ? (byte)7 : (byte)(rightBiome ? 2 : 1);
                    hashes[index] = rightBiome ? BiomeBHash : BiomeAHash;
                }
            }
        }

        private static void Register<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static string ToJsonBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
#endif
