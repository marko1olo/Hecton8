using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Environment.Fluids
{
    /// <summary>
    /// Builds a bounded splash impulse vector field for GPU-side abyssal flow blending.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct FluidImpulseJob : IJob
    {
        private const float Epsilon = 0.0001f;

        public NativeArray<float4> ImpulseField;
        public NativeArray<int> ImpulseStats;
        public float3 FieldCenterWS;
        public float3 ImpactPositionWS;
        public float WorldSizeMeters;
        public float RadiusMeters;
        public float ImpulseStrength;
        public float UpwardBiasMeters;
        public float MaxVelocityMetersPerSecond;
        public int Resolution;

        /// <inheritdoc />
        public void Execute()
        {
            int resolution = math.max(1, Resolution);
            int cellCount = math.min(ImpulseField.Length, resolution * resolution * resolution);
            float worldSize = math.max(1f, WorldSizeMeters);
            float radius = math.max(0.01f, RadiusMeters);
            float radiusSq = radius * radius;
            float maxVelocity = math.max(0.01f, MaxVelocityMetersPerSecond);
            float maxVelocitySq = maxVelocity * maxVelocity;
            float inverseResolution = math.rcp((float)resolution);
            int affectedCount = 0;
            uint invalidFlag = 0u;

            if (!math.all(math.isfinite(FieldCenterWS)) ||
                !math.all(math.isfinite(ImpactPositionWS)) ||
                !math.isfinite(ImpulseStrength))
            {
                invalidFlag = 1u;
                for (int i = 0; i < cellCount; i++)
                    ImpulseField[i] = float4.zero;
                WriteStats(0, invalidFlag);
                return;
            }

            for (int index = 0; index < cellCount; index++)
            {
                int y = index / (resolution * resolution);
                int remainder = index - y * resolution * resolution;
                int z = remainder / resolution;
                int x = remainder - z * resolution;

                float3 uvw = (new float3(x, y, z) + 0.5f) * inverseResolution;
                float3 cellPosition = FieldCenterWS + (uvw - 0.5f) * worldSize;
                float3 delta = cellPosition - ImpactPositionWS;
                float distanceSq = math.lengthsq(delta);
                if (distanceSq > radiusSq)
                {
                    ImpulseField[index] = float4.zero;
                    continue;
                }

                float3 liftedDelta = delta;
                liftedDelta.y += UpwardBiasMeters;
                float liftedDistanceSq = math.lengthsq(liftedDelta);
                float3 direction = liftedDistanceSq > Epsilon
                    ? liftedDelta * math.rsqrt(math.max(liftedDistanceSq, Epsilon))
                    : new float3(0f, 1f, 0f);

                float impulseGain = math.max(0f, ImpulseStrength) * math.rcp(math.max(distanceSq, 1f));
                float3 impulse = direction * impulseGain;
                float impulseVelocitySq = math.lengthsq(impulse);
                if (impulseVelocitySq > maxVelocitySq)
                    impulse *= maxVelocity * math.rsqrt(math.max(impulseVelocitySq, Epsilon));

                if (!math.all(math.isfinite(impulse)))
                {
                    impulse = float3.zero;
                    invalidFlag = 1u;
                }

                float falloff = 1f - math.saturate(distanceSq * math.rcp(math.max(radiusSq, Epsilon)));
                ImpulseField[index] = new float4(impulse, falloff);
                affectedCount++;
            }

            WriteStats(affectedCount, invalidFlag);
        }

        private void WriteStats(int affectedCount, uint invalidFlag)
        {
            if (!ImpulseStats.IsCreated || ImpulseStats.Length < 2)
                return;

            ImpulseStats[0] = math.max(0, affectedCount);
            ImpulseStats[1] = unchecked((int)invalidFlag);
        }
    }
}
