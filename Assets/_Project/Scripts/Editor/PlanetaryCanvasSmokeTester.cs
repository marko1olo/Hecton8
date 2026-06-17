using System;
using System.Globalization;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class PlanetaryCanvasSmokeTester
    {
        private const string NativeMemoryOwner = nameof(PlanetaryCanvasSmokeTester);
        private const string InfluenceCellsLabel = "influenceCells";
        private const float BorderEpsilon = 0.000001f;
        private const byte ExpectedBiome42VisualFamilyId = 5; // Volcanic
        private const byte ExpectedBiome43VisualFamilyId = 1; // Rock

        [MenuItem("Hecton8/World/Run Planetary Canvas Smoke Test")]
        public static void RunMenu()
        {
            if (!RunBiomeBorderBlendSmokeTest(out float blend01, out byte blend255, out uint packedInfluence))
            {
                throw new InvalidOperationException(
                    "Planetary canvas border blend smoke failed. blend01=" +
                    blend01.ToString("F6", CultureInfo.InvariantCulture) +
                    " blend255=" +
                    blend255);
            }

            Debug.Log(
                "[PlanetaryCanvasSmokeTester] PASS borderBlend01=" +
                blend01.ToString("F6", CultureInfo.InvariantCulture) +
                " blend255=" +
                blend255 +
                " packed=" +
                packedInfluence);
        }

        public static bool RunBiomeBorderBlendSmokeTest(out float blend01, out byte blend255, out uint packedInfluence)
        {
            packedInfluence = 0u;
            blend01 = WorldProceduralFieldSampler.EvaluateBiomeBorderSmoothstepBlend01(
                0f,
                WorldProceduralFieldSampler.BiomeBorderOverlapMeters);
            blend255 = WorldProceduralFieldSampler.EvaluateBiomeBorderSmoothstepBlend255(
                0f,
                WorldProceduralFieldSampler.BiomeBorderOverlapMeters);

            NativeArray<WorldProceduralFieldSampler.BiomeInfluenceCell> influenceCells = default;
            try
            {
                // COLD ALLOC: NativeArray<BiomeInfluenceCell>[1] - editor-only 50m biome border blend smoke assertion - owner: PlanetaryCanvasSmokeTester
                influenceCells = AllocateTrackedTempJobArray<WorldProceduralFieldSampler.BiomeInfluenceCell>(
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    InfluenceCellsLabel);
                influenceCells[0] = WorldProceduralFieldSampler.BiomeInfluenceCell.CreateFromBiomeIds(
                    42,
                    43,
                    blend255,
                    (byte)WorldProceduralFieldSampler.BiomeInfluenceFlags.TransitionEdge);
                WorldProceduralFieldSampler.BiomeInfluenceCell cell = influenceCells[0];
                packedInfluence = cell.Packed;

                return math.abs(blend01 - 0.5f) <= BorderEpsilon &&
                       blend255 == 128 &&
                       cell.Blend255 == 128 &&
                       cell.PrimaryVisualFamilyId == ExpectedBiome42VisualFamilyId &&
                       cell.SecondaryVisualFamilyId == ExpectedBiome43VisualFamilyId;
            }
            finally
            {
                DisposeTracked(ref influenceCells);
            }
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, NativeArrayOptions options, string label) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[PlanetaryCanvasSmokeTester] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[PlanetaryCanvasSmokeTester] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static unsafe void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
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
    }
}
