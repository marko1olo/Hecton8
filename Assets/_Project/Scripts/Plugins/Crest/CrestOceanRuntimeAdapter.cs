using Crest;
using Hecton8.Core;
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
        [SerializeField, Min(0f)] private float seaLevelFallback;

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
                RequestCount = count,
                OceanRootAUP = oceanRootAUP,
                SeaLevel = waterLevel,
                GlobalQualityWeight = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)))
            };
            return job.Schedule(count, ResolveInnerLoopBatchCount(count), inputDeps);
        }

        public bool TryReadGlobalWaterLevel(out float waterLevel)
        {
            waterLevel = _cachedWaterLevel;
            return math.isfinite(waterLevel);
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

            return (float)oceanRootAUP.y;
        }

        private static float SanitizeSeaLevel(float value)
        {
            return math.select(0f, value, math.isfinite(value));
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
            public int RequestCount;
            public float SeaLevel;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                Fluids.OceanSampleRequestDTO request = Requests[index];
                double3 localDouble = request.RequestAUP - OceanRootAUP;
                bool finite = math.all(math.isfinite(localDouble));
                float3 local = finite ? (float3)localDouble : float3.zero;

                float quality = math.saturate(GlobalQualityWeight);
                float budgetCurve = math.smoothstep(0.05f, 1f, quality);
                int budget = math.max(1, (int)math.ceil(RequestCount * math.lerp(0.03f, 1f, budgetCurve)));
                bool simplified = index >= budget;

                float2 xz = local.xz;
                float phase = math.dot(xz, new float2(0.0027f, -0.0036f));
                float amplitude = math.lerp(0.03f, 0.85f, quality * quality);
                MathLodApproximation.ApproxSinCosBhaskara(phase, out float phaseSin, out float phaseCos);
                float height = SeaLevel + phaseSin * amplitude;
                if (!simplified)
                {
                    float detailWeight = math.smoothstep(0.35f, 0.95f, quality);
                    float detailPhase = math.dot(xz, new float2(0.0071f, 0.0053f)) + 2.4f;
                    height += MathLodApproximation.ApproxSinBhaskara(detailPhase) * amplitude * 0.22f * detailWeight;
                }

                float slopeX = phaseCos * 0.0027f * amplitude;
                float slopeZ = -phaseCos * 0.0036f * amplitude;
                float3 normal = math.normalize(new float3(-slopeX, 1f, -slopeZ));
                float3 velocity = new float3(-slopeZ, 0f, slopeX);

                uint flags = (uint)(Fluids.OceanSampleStatus.Valid | Fluids.OceanSampleStatus.DelayedOneToThreeFrames);
                if (simplified)
                    flags |= (uint)Fluids.OceanSampleStatus.SimplifiedByQualityBudget;
                if (!finite)
                    flags |= (uint)Fluids.OceanSampleStatus.NonFiniteInput;

                Fluids.OceanSampleResultDTO result = default;
                result.SourceAUP = request.RequestAUP;
                result.WaterHeight = math.select(height, SeaLevel, !math.isfinite(height));
                result.SurfaceVelocity = math.select(velocity, float3.zero, !finite);
                result.WaveNormal = math.select(normal, new float3(0f, 1f, 0f), !finite);
                result.LatencyMilliseconds = simplified ? 3f : 1f;
                result.StatusFlags = flags;
                Results[index] = result;
            }
        }
    }
}
