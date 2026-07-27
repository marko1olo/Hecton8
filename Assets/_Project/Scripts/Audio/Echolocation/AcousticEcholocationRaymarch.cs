using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Echolocation
{
    /// <summary>
    /// Burst SDF ray fan that converts one active ping into virtual acoustic reflection taps.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct AcousticEcholocationRaymarchJob : IJobParallelFor
    {
        public const byte AudioMaterialDefault = 0;
        public const byte AudioMaterialMetal = 1;
        public const byte AudioMaterialRock = 2;
        public const byte AudioMaterialGlass = 3;
        public const byte AudioMaterialBiological = 4;

        [ReadOnly] public NativeArray<byte>.ReadOnly EncodedSdf;
        [ReadOnly] public NativeArray<byte>.ReadOnly AudioMaterialIds;
        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public float3 PingOrigin;
        public float3 ListenerPosition;
        public float3 Forward;
        public float3 Right;
        public float3 Up;
        public float MaxDistanceMeters;
        public float StepMeters;
        public float Intensity01;
        public float ReflectivityConstant;
        public float SoundSpeedInv;
        public float DensityThreshold01;
        public float MinimumLowPassHertz;
        public float OpenLowPassHertz;
        public float AbsorptionCoefficient;
        public float ReferenceDistanceMeters;
        public int RayCount;
        public NativeArray<AcousticEcholocationRayHit> Hits;

        public void Execute(int index)
        {
            if (!Hits.IsCreated || index < 0 || index >= Hits.Length)
                return;

            Hits[index] = default;
            if (index >= RayCount ||
                !EncodedSdf.IsCreated ||
                GridDimensions.x <= 1 ||
                GridDimensions.y <= 1 ||
                GridDimensions.z <= 1 ||
                SdfRange <= 0.0001f ||
                MaxDistanceMeters <= 0.0001f)
            {
                return;
            }

            float3 direction = ResolveRayDirection(index, math.max(1, RayCount));
            float step = math.clamp(StepMeters, 0.05f, math.max(0.05f, MaxDistanceMeters));
            float previousDensity = 0f;
            float3 previousPosition = PingOrigin;
            float previousDistance = 0f;
            bool hasPrevious = false;
            for (float distance = 0f; distance <= MaxDistanceMeters; distance += step)
            {
                float3 position = PingOrigin + direction * distance;
                if (!TrySampleDensity(position, out float density, out float density01, out byte audioMaterialId))
                    continue;

                bool canReturnEcho = distance > 0f;
                bool thresholdHit = canReturnEcho && density01 >= math.saturate(DensityThreshold01);
                bool surfaceHit = hasPrevious && previousDensity < 0f && density >= 0f;
                bool initialSolidHit = canReturnEcho && !hasPrevious && density >= 0f;
                if (!thresholdHit && !surfaceHit && !initialSolidHit)
                {
                    previousDensity = density;
                    previousPosition = position;
                    previousDistance = distance;
                    hasPrevious = true;
                    continue;
                }

                // Default to the sampled point. density and audioMaterialId were both read at
                // `position`, so leaving t at 0 reported the echo at `previousPosition` - one whole
                // `step` short of the surface that produced it, and at PingOrigin itself whenever no
                // in-bounds sample preceded the hit. Only a sign crossing has a sub-step surface to
                // interpolate towards.
                float t = 1f;
                if (surfaceHit)
                {
                    float denom = math.max(0.0001f, density - previousDensity);
                    t = math.saturate(-previousDensity * math.rcp(denom));
                }

                // previousDistance instead of `distance - step` so the interpolated range stays
                // affine-consistent with hitPoint even when intermediate samples fell outside the
                // SDF volume and the real gap was wider than one step.
                float3 hitPoint = math.lerp(previousPosition, position, t);
                float rayDistance = math.max(0f, math.lerp(previousDistance, distance, t));
                float returnDistance = math.length(hitPoint - ListenerPosition);
                float totalDistance = math.max(0.001f, rayDistance + returnDistance);

                float soundSpeedMps = math.max(0.001f, math.rcp(math.max(SoundSpeedInv, 0.000001f)));
                var sonarResult = Hecton8.PureLogic.Systems.SonarPingReturnTimeCalculator.Compute(
                    totalDistance * 0.5f,
                    soundSpeedMps,
                    0f,
                    0f,
                    0f,
                    0f,
                    0.001f,
                    5000f
                );
                float delaySeconds = sonarResult.returnTimeSeconds;
                float totalTimeSq = math.max(delaySeconds * delaySeconds, 0.000001f);
                float materialReflectivity = ResolveMaterialReflectivity(audioMaterialId);
                float absorption = ApproxExpNeg(totalDistance * math.max(0f, AbsorptionCoefficient));
                float reference = math.max(0.001f, ReferenceDistanceMeters);
                float nearFieldLimiter = reference * math.rcp(math.max(reference, totalDistance));
                float gain = math.saturate(
                    math.saturate(Intensity01) *
                    math.max(0f, ReflectivityConstant) *
                    math.rcp(totalTimeSq) *
                    absorption *
                    nearFieldLimiter *
                    materialReflectivity);
                if (!math.isfinite(gain) || gain <= 0.000001f)
                    return;

                Hits[index] = new AcousticEcholocationRayHit
                {
                    Point = hitPoint,
                    Direction = direction,
                    RayDistanceMeters = rayDistance,
                    ReturnDistanceMeters = returnDistance,
                    DelaySeconds = delaySeconds,
                    Gain = gain,
                    LowPassCutoffHertz = ResolveLowPassCutoff(totalDistance, audioMaterialId),
                    AudioMaterialId = audioMaterialId,
                    Hit = 1,
                    StateHash = Hash(index, hitPoint, audioMaterialId)
                };
                return;
            }
        }

        private bool TrySampleDensity(float3 worldPosition, out float density, out float density01, out byte audioMaterialId)
        {
            density = 0f;
            density01 = 0f;
            audioMaterialId = AudioMaterialRock;

            float3 safeCell = math.max(CellSize, new float3(0.0001f));
            float3 sample = (worldPosition - VolumeOrigin) * math.rcp(safeCell);
            if (sample.x < 0f || sample.y < 0f || sample.z < 0f ||
                sample.x > GridDimensions.x - 1.001f ||
                sample.y > GridDimensions.y - 1.001f ||
                sample.z > GridDimensions.z - 1.001f)
            {
                return false;
            }

            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, GridDimensions.x - 1);
            int y1 = math.min(y0 + 1, GridDimensions.y - 1);
            int z1 = math.min(z0 + 1, GridDimensions.z - 1);
            float tx = sample.x - x0;
            float ty = sample.y - y0;
            float tz = sample.z - z0;

            float c000 = DecodeAt(x0, y0, z0);
            float c100 = DecodeAt(x1, y0, z0);
            float c010 = DecodeAt(x0, y1, z0);
            float c110 = DecodeAt(x1, y1, z0);
            float c001 = DecodeAt(x0, y0, z1);
            float c101 = DecodeAt(x1, y0, z1);
            float c011 = DecodeAt(x0, y1, z1);
            float c111 = DecodeAt(x1, y1, z1);
            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            density = math.lerp(math.lerp(c00, c10, ty), math.lerp(c01, c11, ty), tz);
            density01 = math.saturate(math.max(0f, density) * math.rcp(math.max(0.0001f, SdfRange)));
            audioMaterialId = ResolveAudioMaterialIdNearest(sample);
            return math.isfinite(density);
        }

        private float DecodeAt(int x, int y, int z)
        {
            int index = x + GridDimensions.x * (y + GridDimensions.y * z);
            if ((uint)index >= (uint)EncodedSdf.Length)
                return -SdfRange;

            return ((EncodedSdf[index] * 0.00392156862f) * 2f - 1f) * SdfRange;
        }

        private byte ResolveAudioMaterialIdNearest(float3 sample)
        {
            if (!AudioMaterialIds.IsCreated || AudioMaterialIds.Length != EncodedSdf.Length)
                return AudioMaterialRock;

            int x = math.clamp((int)(sample.x + 0.5f), 0, GridDimensions.x - 1);
            int y = math.clamp((int)(sample.y + 0.5f), 0, GridDimensions.y - 1);
            int z = math.clamp((int)(sample.z + 0.5f), 0, GridDimensions.z - 1);
            int index = x + GridDimensions.x * (y + GridDimensions.y * z);
            if ((uint)index >= (uint)AudioMaterialIds.Length)
                return AudioMaterialRock;

            byte materialId = AudioMaterialIds[index];
            switch (materialId)
            {
                case AudioMaterialMetal:
                case AudioMaterialRock:
                case AudioMaterialGlass:
                case AudioMaterialBiological:
                    return materialId;
                default:
                    return AudioMaterialRock;
            }
        }

        private float3 ResolveRayDirection(int index, int rayCount)
        {
            float3 forward = NormalizeSafe(Forward, new float3(0f, 0f, 1f));
            float3 right = NormalizeSafe(Right, new float3(1f, 0f, 0f));
            float3 up = NormalizeSafe(Up, new float3(0f, 1f, 0f));
            if (rayCount <= 8)
            {
                float sx = (index & 1) == 0 ? 1f : -1f;
                float sy = (index & 2) == 0 ? 1f : -1f;
                float sz = (index & 4) == 0 ? 1f : -1f;
                return NormalizeSafe(right * sx + up * sy + forward * sz, forward);
            }

            int lane = index & 31;
            float laneSx = (lane & 1) == 0 ? 1f : -1f;
            float laneSy = (lane & 2) == 0 ? 1f : -1f;
            float laneSz = (lane & 4) == 0 ? 1f : -1f;
            int weightSet = (lane >> 3) & 3;
            float forwardWeight = weightSet == 0 ? 1f : weightSet == 1 ? 0.55f : weightSet == 2 ? 0.25f : 0.75f;
            float rightWeight = weightSet == 1 ? 1f : weightSet == 2 ? 0.55f : weightSet == 3 ? 0.25f : 0.75f;
            float upWeight = weightSet == 2 ? 1f : weightSet == 3 ? 0.55f : weightSet == 0 ? 0.25f : 0.75f;
            return NormalizeSafe(
                (right * (laneSx * rightWeight)) +
                (up * (laneSy * upWeight)) +
                (forward * (laneSz * forwardWeight)),
                forward);
        }

        private float ResolveLowPassCutoff(float totalDistance, byte audioMaterialId)
        {
            float distanceT = math.saturate(totalDistance * math.rcp(math.max(1f, MaxDistanceMeters)));
            float cutoff = math.lerp(
                math.max(MinimumLowPassHertz, 1f),
                math.max(OpenLowPassHertz, MinimumLowPassHertz + 1f),
                1f - distanceT);
            switch (audioMaterialId)
            {
                case AudioMaterialMetal:
                    return math.clamp(math.max(cutoff, 6800f), MinimumLowPassHertz, OpenLowPassHertz);
                case AudioMaterialGlass:
                    return math.clamp(math.max(cutoff, 5600f), MinimumLowPassHertz, OpenLowPassHertz);
                case AudioMaterialBiological:
                    return math.clamp(math.min(cutoff, 1150f), MinimumLowPassHertz, OpenLowPassHertz);
                case AudioMaterialRock:
                    return math.clamp(math.min(cutoff, 2400f), MinimumLowPassHertz, OpenLowPassHertz);
                default:
                    return math.clamp(cutoff, MinimumLowPassHertz, OpenLowPassHertz);
            }
        }

        private static float ResolveMaterialReflectivity(byte audioMaterialId)
        {
            switch (audioMaterialId)
            {
                case AudioMaterialMetal:
                    return 1.35f;
                case AudioMaterialGlass:
                    return 1.12f;
                case AudioMaterialBiological:
                    return 0.78f;
                case AudioMaterialRock:
                    return 0.64f;
                default:
                    return 0.86f;
            }
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f ? value * math.rsqrt(lengthSq) : fallback;
        }

        private static float ApproxExpNeg(float value)
        {
            float x = math.max(0f, value);
            return math.rcp(1f + x + x * x * 0.48f + x * x * x * 0.235f);
        }

        private static uint Hash(int index, float3 point, byte materialId)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)index) * 16777619u;
            hash = (hash ^ (uint)math.asint(point.x)) * 16777619u;
            hash = (hash ^ (uint)math.asint(point.y)) * 16777619u;
            hash = (hash ^ (uint)math.asint(point.z)) * 16777619u;
            hash = (hash ^ materialId) * 16777619u;
            return hash;
        }
    }
}
