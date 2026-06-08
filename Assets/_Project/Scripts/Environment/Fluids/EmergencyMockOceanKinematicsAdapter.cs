using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Environment.Fluids
{
    public readonly struct EmergencyMockOceanKinematicsAdapter : IHectonOceanKinematics
    {
        private const float DefaultSeaLevel = 14.02f;

        private readonly float _seaLevel;

        private EmergencyMockOceanKinematicsAdapter(float seaLevel)
        {
            _seaLevel = ResolveSeaLevel(seaLevel);
        }

        public static EmergencyMockOceanKinematicsAdapter GenerateEmergencyMockOceanAdapter(float seaLevel = DefaultSeaLevel)
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
            _ = globalQualityWeight;

            if (!requests.IsCreated || !results.IsCreated || requestCount <= 0)
                return inputDeps;

            int count = math.min(requestCount, math.min(requests.Length, results.Length));
            if (count <= 0)
                return inputDeps;

            GenerateEmergencyMockOceanJob job = new GenerateEmergencyMockOceanJob
            {
                Requests = requests,
                Results = results,
                ActiveOriginAUP = activeOriginAUP,
                SeaLevel = _seaLevel
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSeaLevel(float seaLevel)
        {
            return math.isfinite(seaLevel) &&
                math.abs(seaLevel) > 0.0001f &&
                math.abs(seaLevel) <= 1000f
                ? seaLevel
                : DefaultSeaLevel;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateEmergencyMockOceanJob : IJobParallelFor
        {
            private const float TwoPi = 6.28318530718f;
            private const float InvTwoPi = 0.15915494309f;
            private const float Pi = 3.14159265359f;
            private const float Epsilon = 0.0001f;

            [ReadOnly, NoAlias] public NativeArray<OceanSampleRequestDTO> Requests;
            [WriteOnly, NoAlias] public NativeArray<OceanSampleResultDTO> Results;
            public double3 ActiveOriginAUP;
            public float SeaLevel;

            public void Execute(int index)
            {
                OceanSampleRequestDTO request = Requests[index];
                double3 localDouble = request.RequestAUP - ActiveOriginAUP;
                bool finite = math.all(math.isfinite(localDouble));
                float3 local = finite ? (float3)localDouble : float3.zero;

                float2 xz = local.xz;
                const float lowAmp = 0.35f;
                const float highAmp = 1.25f;
                float phase0 = math.dot(xz, new float2(0.0031f, 0.0023f));
                float height = SeaLevel + ApproxSinBhaskara(phase0) * highAmp;

                height += ApproxSinBhaskara(math.dot(xz, new float2(-0.0067f, 0.0049f)) + 1.7f) * (0.35f * highAmp);
                height += ApproxSinBhaskara(math.dot(xz, new float2(0.011f, -0.008f)) + 3.1f) * (0.16f * highAmp);

                float slopeX = ApproxCosBhaskara(phase0) * 0.0031f * lowAmp;
                float slopeZ = ApproxCosBhaskara(phase0) * 0.0023f * lowAmp;
                float3 normal = math.normalize(new float3(-slopeX, 1f, -slopeZ));
                float3 velocity = new float3(slopeZ, 0f, -slopeX);

                uint flags = (uint)(OceanSampleStatus.Valid | OceanSampleStatus.Mocked | OceanSampleStatus.DelayedOneToThreeFrames);
                if (!finite)
                    flags |= (uint)OceanSampleStatus.NonFiniteInput;

                OceanSampleResultDTO result = default;
                result.SourceAUP = request.RequestAUP;
                result.WaterHeight = math.select(height, SeaLevel, !math.isfinite(height));
                result.SurfaceVelocity = math.select(velocity, float3.zero, !finite);
                result.WaveNormal = math.select(normal, new float3(0f, 1f, 0f), !finite);
                result.LatencyMilliseconds = 1f;
                result.StatusFlags = flags;
                Results[index] = result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ApproxSinBhaskara(float radians)
            {
                float angle = math.select(0f, radians, math.isfinite(radians));
                float cycle = angle * InvTwoPi;
                float wrapped = cycle - math.floor(cycle);
                float x = wrapped * TwoPi;
                float mirrored = math.select(x, TwoPi - x, x > Pi);
                float sign = math.select(1f, -1f, x > Pi);
                float shape = mirrored * (Pi - mirrored);
                float numerator = 16f * shape;
                float denominator = math.max(Epsilon, (5f * Pi * Pi) - (4f * shape));
                float sine = sign * numerator * math.rcp(denominator);
                return math.clamp(math.select(0f, sine, math.isfinite(sine)), -1f, 1f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float ApproxCosBhaskara(float radians)
            {
                return ApproxSinBhaskara(radians + (0.5f * Pi));
            }
        }
    }
}
