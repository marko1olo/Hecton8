using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.GPR
{
    public static class GroundRadarConstants
    {
        public const int MaxRays = 64;
        public const int LowTierRays = 16;
        public const int MaxRaymarchSteps = 10;
        public const int MaxPings = 128;
        public const int TelemetryFrames = 300;
        public const float SolidDensityThreshold = 0.5f;
        public const float OreMatchDistanceMeters = 5f;
        public const float OreMatchDistanceSq = OreMatchDistanceMeters * OreMatchDistanceMeters;
        public const float ScanDecaySeconds = 3f;
    }

    public struct GroundRadarTelemetryEntry
    {
        public uint Frame;
        public int ActiveGprPings;
        public int AddedGprPings;
        public int RayCount;
        public float HighestSignalStrength;
        public float3 ProbeOrigin;
        public uint Flags;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GroundRadarRaymarchJob : IJob
    {
        [ReadOnly] public NativeArray<byte> EncodedSdf;
        [ReadOnly] public NativeArray<float3> OrePositions;

        public NativeArray<float3> GprHits;
        public NativeArray<float> GprSignalStrength;
        public NativeArray<float> GprAgeSeconds;
        public NativeArray<float4> GprPingGpu;
        public NativeArray<int> Counters;
        public NativeArray<float> MaxSignalStrength;

        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public int OreScanCount;
        public int PreviousActiveCount;
        public int RequestedRayCount;
        public int MaxSteps;
        public float3 ProbeOrigin;
        public float ScanRadiusMeters;
        public float StepMeters;
        public float DeltaTime;
        public float3 RuntimeShift;
        public byte ShouldScan;
        public byte ApplyShift;

        public void Execute()
        {
            int writeIndex = CompactExistingPings();
            int addedCount = 0;
            float highestSignal = MaxSignalStrength.IsCreated && MaxSignalStrength.Length > 0
                ? math.max(0f, MaxSignalStrength[0])
                : 0f;

            int rayCount = math.clamp(RequestedRayCount, 1, GroundRadarConstants.MaxRays);
            int maxSteps = math.clamp(MaxSteps, 1, GroundRadarConstants.MaxRaymarchSteps);
            if (ShouldScan != 0 && HasValidSdf() && OrePositions.IsCreated && OreScanCount > 0)
            {
                float scanRadius = math.max(1f, ScanRadiusMeters);
                float stepMeters = math.max(0.5f, StepMeters);
                int side = rayCount <= GroundRadarConstants.LowTierRays ? 4 : 8;
                float invSideMinusOne = math.rcp(math.max(1, side - 1));
                int oreCount = math.min(OreScanCount, OrePositions.Length);

                for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
                {
                    int x = rayIndex % side;
                    int z = rayIndex / side;
                    float2 uv = new float2(x, z) * invSideMinusOne;
                    float2 centered = uv * 2f - 1f;
                    float3 rayOrigin = ProbeOrigin + new float3(centered.x * scanRadius, 0f, centered.y * scanRadius);

                    for (int stepIndex = 1; stepIndex <= maxSteps; stepIndex++)
                    {
                        float depth = stepIndex * stepMeters;
                        float3 samplePosition = rayOrigin + new float3(0f, -depth, 0f);
                        float density = SampleDensity(samplePosition);
                        if (density <= GroundRadarConstants.SolidDensityThreshold)
                            continue;

                        if (!TryResolveOreHit(samplePosition, oreCount, out float3 orePosition))
                            break;

                        if (writeIndex < GprHits.Length)
                        {
                            float signalStrength = math.saturate(math.rcp(math.max(1f, depth * depth)));
                            GprHits[writeIndex] = orePosition;
                            GprSignalStrength[writeIndex] = signalStrength;
                            GprAgeSeconds[writeIndex] = 0f;
                            GprPingGpu[writeIndex] = new float4(orePosition, signalStrength);
                            highestSignal = math.max(highestSignal, signalStrength);
                            writeIndex++;
                            addedCount++;
                        }

                        break;
                    }
                }
            }

            if (Counters.IsCreated)
            {
                if (Counters.Length > 0)
                    Counters[0] = writeIndex;
                if (Counters.Length > 1)
                    Counters[1] = addedCount;
                if (Counters.Length > 2)
                    Counters[2] = rayCount;
                if (Counters.Length > 3)
                    Counters[3] = maxSteps;
            }

            if (MaxSignalStrength.IsCreated && MaxSignalStrength.Length > 0)
                MaxSignalStrength[0] = highestSignal;
        }

        private int CompactExistingPings()
        {
            if (!GprHits.IsCreated || !GprSignalStrength.IsCreated || !GprAgeSeconds.IsCreated || !GprPingGpu.IsCreated)
                return 0;

            int previousCount = math.min(math.max(0, PreviousActiveCount), GprHits.Length);
            float safeDelta = math.max(0f, DeltaTime);
            float3 shift = ApplyShift != 0 ? RuntimeShift : float3.zero;
            int writeIndex = 0;

            for (int i = 0; i < previousCount; i++)
            {
                float age = GprAgeSeconds[i] + safeDelta;
                if (age >= GroundRadarConstants.ScanDecaySeconds)
                    continue;

                float strength = GprSignalStrength[i] * math.saturate(1f - age * math.rcp(GroundRadarConstants.ScanDecaySeconds));
                if (strength <= 0.0001f)
                    continue;

                float3 hit = GprHits[i] - shift;
                if (!math.all(math.isfinite(hit)) || !math.isfinite(strength))
                    continue;

                GprHits[writeIndex] = hit;
                GprSignalStrength[writeIndex] = strength;
                GprAgeSeconds[writeIndex] = age;
                GprPingGpu[writeIndex] = new float4(hit, strength);
                writeIndex++;
            }

            return writeIndex;
        }

        private bool HasValidSdf()
        {
            return EncodedSdf.IsCreated &&
                   EncodedSdf.Length > 0 &&
                   GridDimensions.x > 1 &&
                   GridDimensions.y > 1 &&
                   GridDimensions.z > 1 &&
                   math.all(CellSize > new float3(0.0001f)) &&
                   math.isfinite(SdfRange) &&
                   SdfRange > 0f &&
                   math.all(math.isfinite(ProbeOrigin));
        }

        private float SampleDensity(float3 runtimePosition)
        {
            float3 local = runtimePosition - VolumeOrigin;
            float3 grid = local / CellSize;
            int ix = (int)math.floor(grid.x);
            int iy = (int)math.floor(grid.y);
            int iz = (int)math.floor(grid.z);
            if ((uint)ix >= (uint)GridDimensions.x ||
                (uint)iy >= (uint)GridDimensions.y ||
                (uint)iz >= (uint)GridDimensions.z)
            {
                return 0f;
            }

            int index = ix + GridDimensions.x * (iy + GridDimensions.y * iz);
            if ((uint)index >= (uint)EncodedSdf.Length)
                return 0f;

            float normalized = EncodedSdf[index] * math.rcp(255f);
            float signedDistance = (normalized * 2f - 1f) * SdfRange;
            return math.saturate(0.5f - signedDistance * math.rcp(math.max(0.001f, SdfRange)));
        }

        private bool TryResolveOreHit(float3 samplePosition, int oreCount, out float3 orePosition)
        {
            orePosition = default;
            float bestDistanceSq = GroundRadarConstants.OreMatchDistanceSq;
            int bestIndex = -1;
            for (int i = 0; i < oreCount; i++)
            {
                float3 candidate = OrePositions[i];
                float distanceSq = math.lengthsq(candidate - samplePosition);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestIndex = i;
                orePosition = candidate;
            }

            return bestIndex >= 0;
        }
    }
}
