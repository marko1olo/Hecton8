using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Developer-only smoke harness for biome influence packing and AUP fog transition blending.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/World/Biome Transition Smoke Tester")]
    public sealed class BiomeTransitionSmokeTester : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool logResult = true;
        [SerializeField] private bool _debugPassed;
        [SerializeField] private uint _debugPackedInfluence;
        [SerializeField] private float _debugFogDensity;
        [SerializeField] private float _debugAbsorption;

        private void Start()
        {
            if (runOnStart)
                RunSmokeTest();
        }

        /// <summary>
        /// Runs the biome transition smoke test and mirrors the result into inspector debug fields.
        /// </summary>
        [ContextMenu("Run Biome Transition Smoke Test")]
        public bool RunSmokeTest()
        {
            bool passed = RunHeadlessSmokeTest(out float fogDensity, out float absorption, out uint packedInfluence);
            _debugPassed = passed;
            _debugFogDensity = fogDensity;
            _debugAbsorption = absorption;
            _debugPackedInfluence = packedInfluence;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logResult)
            {
                Debug.Log(
                    _debugPassed
                        ? "[BiomeTransitionSmokeTester] PASS"
                        : "[BiomeTransitionSmokeTester] FAIL",
                    this);
            }
#endif
            return _debugPassed;
        }

        /// <summary>
        /// Runs the biome transition smoke test without requiring a scene object.
        /// </summary>
        /// <param name="fogDensity">Interpolated fog density from the AUP transition sample.</param>
        /// <param name="absorption">Interpolated absorption from the AUP transition sample.</param>
        /// <param name="packedInfluence">Packed biome influence value using primary/secondary/blend/flags bytes.</param>
        public static bool RunHeadlessSmokeTest(out float fogDensity, out float absorption, out uint packedInfluence)
        {
            bool fogPassed = RunFogBlendSmokeTest(out fogDensity, out absorption);
            bool packPassed = RunBiomeInfluencePackSmokeTest(out packedInfluence);
            return fogPassed && packPassed;
        }

        private static bool RunBiomeInfluencePackSmokeTest(out uint packedInfluence)
        {
            WorldProceduralFieldSampler.BiomeInfluenceCell cell =
                WorldProceduralFieldSampler.BiomeInfluenceCell.Create(42, 43, 128, 5);
            packedInfluence = cell.Packed;
            const uint Expected = 42u | (43u << 8) | (128u << 16) | (5u << 24);
            return packedInfluence == Expected &&
                   cell.PrimaryBiomeId == 42 &&
                   cell.SecondaryBiomeId == 43 &&
                   cell.Blend255 == 128 &&
                   cell.Flags == 5;
        }

        private static bool RunFogBlendSmokeTest(out float fogDensity, out float absorption)
        {
            fogDensity = 0f;
            absorption = 0f;
            NativeArray<BiomeTransitionSample> samples = default;
            NativeArray<BiomeTransitionFogSource> sources = default;
            NativeArray<AbsoluteUniversePositionBlit128> fromAup = default;
            NativeArray<AbsoluteUniversePositionBlit128> toAup = default;
            NativeArray<AbsoluteUniversePositionBlit128> playerAup = default;
            NativeArray<BiomeTransitionFogResult> results = default;

            try
            {
                samples = new NativeArray<BiomeTransitionSample>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                sources = new NativeArray<BiomeTransitionFogSource>(64, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                fromAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                toAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                playerAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                results = new NativeArray<BiomeTransitionFogResult>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                samples[0] = new BiomeTransitionSample
                {
                    FromBiomeId = 42,
                    ToBiomeId = 43,
                    Blend255 = 0,
                    Flags = 1
                };
                sources[42] = new BiomeTransitionFogSource
                {
                    FogColor = new float4(0f, 0.1f, 0.2f, 1f),
                    Density = 0.02f,
                    Turbidity = 0.75f,
                    Absorption = 0.2f
                };
                sources[43] = new BiomeTransitionFogSource
                {
                    FogColor = new float4(0.2f, 0.3f, 0.4f, 1f),
                    Density = 0.06f,
                    Turbidity = 1.25f,
                    Absorption = 0.8f
                };
                fromAup[0] = BuildAup(0f, 0f, 0f);
                toAup[0] = BuildAup(100f, 0f, 0f);
                playerAup[0] = BuildAup(50f, 0f, 0f);

                BiomeTransitionFogBlendJob job = new BiomeTransitionFogBlendJob
                {
                    Samples = samples,
                    FogSourcesByBiomeId = sources,
                    FromAup = fromAup,
                    ToAup = toAup,
                    PlayerAup = playerAup,
                    Results = results,
                    TransitionLengthMeters = 100f
                };

                // COLD SYNC JOB: deterministic smoke validation path, never part of gameplay sampling or per-frame scatter execution.
                job.Schedule(1, 1).Complete();
                BiomeTransitionFogResult result = results[0];
                fogDensity = result.Density;
                absorption = result.Absorption;
                return result.Sample.FromBiomeId == 42 &&
                       result.Sample.ToBiomeId == 43 &&
                       result.Sample.Blend255 == 128 &&
                       math.abs(result.Density - 0.04f) <= 0.0001f &&
                       math.abs(result.Turbidity - 1f) <= 0.0001f &&
                       math.abs(result.Absorption - 0.5f) <= 0.0001f;
            }
            finally
            {
                if (samples.IsCreated) samples.Dispose();
                if (sources.IsCreated) sources.Dispose();
                if (fromAup.IsCreated) fromAup.Dispose();
                if (toAup.IsCreated) toAup.Dispose();
                if (playerAup.IsCreated) playerAup.Dispose();
                if (results.IsCreated) results.Dispose();
            }
        }

        private static AbsoluteUniversePositionBlit128 BuildAup(float x, float y, float z)
        {
            return new AbsoluteUniversePositionBlit128
            {
                GridX = 0,
                GridY = 0,
                GridZ = 0,
                Local = new float4(x, y, z, 0f),
                Reserved = 0UL
            };
        }
    }
}
