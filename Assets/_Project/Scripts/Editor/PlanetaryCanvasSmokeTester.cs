using System;
using System.Globalization;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class PlanetaryCanvasSmokeTester
    {
        private const float BorderEpsilon = 0.000001f;
        private const byte ExpectedBiome42VisualFamilyId = 5; // Volcanic
        private const byte ExpectedBiome43VisualFamilyId = 1; // Rock

        [MenuItem("HECTON-8/World/Run Planetary Canvas Smoke Test")]
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
            bool influenceCellsRegistered = false;
            try
            {
                // COLD ALLOC: NativeArray<BiomeInfluenceCell>[1] - editor-only 50m biome border blend smoke assertion - owner: PlanetaryCanvasSmokeTester
                influenceCells = new NativeArray<WorldProceduralFieldSampler.BiomeInfluenceCell>(
                    1,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    influenceCells,
                    nameof(PlanetaryCanvasSmokeTester),
                    nameof(influenceCells),
                    NativeAllocationLifetime.TempJob);
                influenceCellsRegistered = true;
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
                if (influenceCells.IsCreated)
                {
                    if (influenceCellsRegistered)
                        NativeMemorySentinel.UnregisterNativeArray(influenceCells);

                    influenceCells.Dispose();
                }
            }
        }
    }
}
