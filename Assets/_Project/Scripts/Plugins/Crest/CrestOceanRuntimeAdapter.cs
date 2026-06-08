using Crest;
using Hecton8.Core;
using Hecton8.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Fluids = Hecton8.Environment.Fluids;

namespace Hecton8.Crest.Bridge
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Ocean/Crest Runtime Adapter")]
    public sealed class CrestOceanRuntimeAdapter : MonoBehaviour, Fluids.IHectonOceanKinematics
    {
        [SerializeField] private OceanRenderer oceanRenderer;
        [SerializeField, Min(0f)] private float seaLevelFallback = AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;

        private double3 _cachedOceanRootAUP;
        private float _cachedWaterLevel;
        private byte _hasAuthoritativeRootAUP;

        public void Bind(OceanRenderer renderer, double3 oceanRootAUP)
        {
            oceanRenderer = renderer;
            _cachedOceanRootAUP = SanitizeAUP(oceanRootAUP);
            _hasAuthoritativeRootAUP = math.all(math.isfinite(oceanRootAUP)) ? (byte)1 : (byte)0;
            _cachedWaterLevel = ResolveWaterLevelFromAUP(_cachedOceanRootAUP, seaLevelFallback, _hasAuthoritativeRootAUP);
        }

        public JobHandle ScheduleWaveHeightRequests(
            NativeArray<Fluids.OceanSampleRequestDTO> requests,
            NativeArray<Fluids.OceanSampleResultDTO> results,
            int requestCount,
            double3 activeOriginAUP,
            float globalQualityWeight,
            JobHandle inputDeps)
        {
            _ = globalQualityWeight;

            if (!requests.IsCreated || !results.IsCreated || requestCount <= 0)
                return inputDeps;

            int count = math.min(requestCount, math.min(requests.Length, results.Length));
            if (count <= 0)
                return inputDeps;

            double3 oceanRootAUP = _hasAuthoritativeRootAUP != 0
                ? _cachedOceanRootAUP
                : SanitizeAUP(activeOriginAUP);
            float waterLevel = _cachedWaterLevel;

            CrestDeferredApproximationJob job = new CrestDeferredApproximationJob
            {
                Requests = requests,
                Results = results,
                OceanRootAUP = oceanRootAUP,
                SeaLevel = waterLevel
            };
            return job.Schedule(count, ResolveInnerLoopBatchCount(count), inputDeps);
        }

        public bool TryReadGlobalWaterLevel(out float waterLevel)
        {
            waterLevel = _cachedWaterLevel;
            return TryResolveSeaLevel(waterLevel, out waterLevel);
        }

        private void Awake()
        {
            if (oceanRenderer == null)
                TryGetComponent(out oceanRenderer);

            _hasAuthoritativeRootAUP = 0;
            _cachedOceanRootAUP = double3.zero;
            _cachedWaterLevel = SanitizeSeaLevel(seaLevelFallback);
        }

        private static double3 SanitizeAUP(double3 value)
        {
            return math.select(double3.zero, value, math.isfinite(value));
        }

        private static float ResolveWaterLevelFromAUP(double3 oceanRootAUP, float fallback, byte hasAuthoritativeRootAUP)
        {
            float safeFallback = SanitizeSeaLevel(fallback);
            if (hasAuthoritativeRootAUP == 0 || !math.isfinite(oceanRootAUP.y) || math.abs(oceanRootAUP.y) > 100000.0)
                return safeFallback;

            return AnalyticalGerstnerWaveConstants.ResolveSeaLevelY((float)oceanRootAUP.y);
        }

        private static float SanitizeSeaLevel(float value)
        {
            return TryResolveSeaLevel(value, out float seaLevel)
                ? seaLevel
                : AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;
        }

        private static bool TryResolveSeaLevel(float value, out float seaLevel)
        {
            if (math.isfinite(value) && math.abs(value) > 0.0001f)
            {
                seaLevel = value;
                return true;
            }

            seaLevel = AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;
            return false;
        }

        private static int ResolveInnerLoopBatchCount(int count)
        {
            if (count >= 1024)
                return 64;

            if (count >= 128)
                return 32;

            return 16;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CrestDeferredApproximationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Fluids.OceanSampleRequestDTO> Requests;
            [WriteOnly, NoAlias] public NativeArray<Fluids.OceanSampleResultDTO> Results;
            public double3 OceanRootAUP;
            public float SeaLevel;

            public void Execute(int index)
            {
                Fluids.OceanSampleRequestDTO request = Requests[index];
                double3 localDouble = request.RequestAUP - OceanRootAUP;
                bool finite = math.all(math.isfinite(localDouble));
                float3 local = finite ? (float3)localDouble : float3.zero;

                float2 xz = local.xz;
                float phase = math.dot(xz, new float2(0.0027f, -0.0036f));
                const float amplitude = 0.85f;
                MathLodApproximation.ApproxSinCosBhaskara(phase, out float phaseSin, out float phaseCos);
                float height = SeaLevel + phaseSin * amplitude;
                float detailPhase = math.dot(xz, new float2(0.0071f, 0.0053f)) + 2.4f;
                height += MathLodApproximation.ApproxSinBhaskara(detailPhase) * amplitude * 0.22f;

                float slopeX = phaseCos * 0.0027f * amplitude;
                float slopeZ = -phaseCos * 0.0036f * amplitude;
                float3 normal = math.normalize(new float3(-slopeX, 1f, -slopeZ));
                float3 velocity = new float3(-slopeZ, 0f, slopeX);

                uint flags = (uint)(Fluids.OceanSampleStatus.Valid | Fluids.OceanSampleStatus.DelayedOneToThreeFrames);
                if (!finite)
                    flags |= (uint)Fluids.OceanSampleStatus.NonFiniteInput;

                Fluids.OceanSampleResultDTO result = default;
                result.SourceAUP = request.RequestAUP;
                result.WaterHeight = math.select(height, SeaLevel, !math.isfinite(height));
                result.SurfaceVelocity = math.select(velocity, float3.zero, !finite);
                result.WaveNormal = math.select(normal, new float3(0f, 1f, 0f), !finite);
                result.LatencyMilliseconds = 1f;
                result.StatusFlags = flags;
                Results[index] = result;
            }
        }
    }
}
