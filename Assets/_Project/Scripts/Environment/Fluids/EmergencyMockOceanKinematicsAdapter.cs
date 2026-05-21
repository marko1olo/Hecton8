using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Environment.Fluids
{
    public readonly struct EmergencyMockOceanKinematicsAdapter : IHectonOceanKinematics
    {
        private readonly float _seaLevel;

        private EmergencyMockOceanKinematicsAdapter(float seaLevel)
        {
            _seaLevel = math.select(0f, seaLevel, math.isfinite(seaLevel));
        }

        public static EmergencyMockOceanKinematicsAdapter GenerateEmergencyMockOceanAdapter(float seaLevel = 0f)
        {
            return new EmergencyMockOceanKinematicsAdapter(seaLevel);
        }

        public JobHandle ScheduleWaveHeightRequests(
            NativeArray<OceanSampleRequestDTO> requests,
            NativeArray<OceanSampleResultDTO> results,
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

            GenerateEmergencyMockOceanJob job = new GenerateEmergencyMockOceanJob
            {
                Requests = requests,
                Results = results,
                RequestCount = count,
                ActiveOriginAUP = activeOriginAUP,
                SeaLevel = _seaLevel,
                GlobalQualityWeight = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)))
            };
            return job.Schedule(count, ResolveInnerLoopBatchCount(count), inputDeps);
        }

        public bool TryReadGlobalWaterLevel(out float waterLevel)
        {
            waterLevel = _seaLevel;
            return true;
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
        private struct GenerateEmergencyMockOceanJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<OceanSampleRequestDTO> Requests;
            [WriteOnly, NoAlias] public NativeArray<OceanSampleResultDTO> Results;
            public double3 ActiveOriginAUP;
            public int RequestCount;
            public float SeaLevel;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                OceanSampleRequestDTO request = Requests[index];
                double3 localDouble = request.RequestAUP - ActiveOriginAUP;
                bool finite = math.all(math.isfinite(localDouble));
                float3 local = finite ? (float3)localDouble : float3.zero;

                float quality = math.saturate(GlobalQualityWeight);
                float budgetCurve = math.smoothstep(0.05f, 1f, quality);
                int budget = math.max(1, (int)math.ceil(RequestCount * math.lerp(0.05f, 1f, budgetCurve)));
                bool simplified = index >= budget;

                float2 xz = local.xz;
                float lowAmp = math.lerp(0.025f, 0.35f, quality);
                float highAmp = math.lerp(0.1f, 1.25f, quality * quality);
                float phase0 = math.dot(xz, new float2(0.0031f, 0.0023f));
                float height = SeaLevel + math.sin(phase0) * math.select(highAmp, lowAmp, simplified);

                if (!simplified)
                {
                    float detailWeight0 = math.smoothstep(0.2f, 0.65f, quality);
                    float detailWeight1 = math.smoothstep(0.45f, 0.95f, quality);
                    height += math.sin(math.dot(xz, new float2(-0.0067f, 0.0049f)) + 1.7f) * (0.35f * highAmp * detailWeight0);
                    height += math.sin(math.dot(xz, new float2(0.011f, -0.008f)) + 3.1f) * (0.16f * highAmp * detailWeight1);
                }

                float slopeX = math.cos(phase0) * 0.0031f * lowAmp;
                float slopeZ = math.cos(phase0) * 0.0023f * lowAmp;
                float3 normal = math.normalize(new float3(-slopeX, 1f, -slopeZ));
                float3 velocity = new float3(slopeZ, 0f, -slopeX);

                uint flags = (uint)(OceanSampleStatus.Valid | OceanSampleStatus.Mocked | OceanSampleStatus.DelayedOneToThreeFrames);
                if (simplified)
                    flags |= (uint)OceanSampleStatus.SimplifiedByQualityBudget;
                if (!finite)
                    flags |= (uint)OceanSampleStatus.NonFiniteInput;

                OceanSampleResultDTO result = default;
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
