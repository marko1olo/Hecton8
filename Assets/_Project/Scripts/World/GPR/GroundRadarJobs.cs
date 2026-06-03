using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.GPR
{
    internal static class GroundRadarJobLayout
    {
        public const int GroundRadarTelemetryEntryStrideBytes = 64;
    }

    public static class GroundRadarConstants
    {
        public const int MaxRays = 64;
        public const int MaxRaymarchSteps = 10;
        public const int MaxPings = 128;
        public const int TelemetryFrames = 300;
        public const int SdfSnapshotGridSide = 64;
        public const int SdfSnapshotByteCapacity = SdfSnapshotGridSide * SdfSnapshotGridSide * SdfSnapshotGridSide;
        public const float SolidDensityThreshold = 0.5f;
        public const float OreMatchDistanceMeters = 5f;
        public const float OreMatchDistanceSq = OreMatchDistanceMeters * OreMatchDistanceMeters;
        public const float FilteredOreAlphaScale = 0.1f;
        public const float ScanDecaySeconds = 3f;
        public const float InverseByteMax = 0.0039215686f;
        public const uint ScanFlag = 1u << 0;
        public const uint AupShiftFlag = 1u << 1;

        public static int ResolveRayGridSide(int rayCount)
        {
            int clamped = math.clamp(rayCount, 1, MaxRays);
            int side = 2;
            side += math.select(0, 1, clamped > 4);
            side += math.select(0, 1, clamped > 9);
            side += math.select(0, 1, clamped > 16);
            side += math.select(0, 1, clamped > 25);
            side += math.select(0, 1, clamped > 36);
            side += math.select(0, 1, clamped > 49);
            return side;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = GroundRadarJobLayout.GroundRadarTelemetryEntryStrideBytes)]
    public struct GroundRadarTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int ActiveGprPings;
        [FieldOffset(8)] public int AddedGprPings;
        [FieldOffset(12)] public int RayCount;
        [FieldOffset(16)] public float HighestSignalStrength;
        [FieldOffset(20)] public float3 ProbeOrigin;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GroundRadarRaymarchJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly EncodedSdf;
        [ReadOnly, NoAlias] public NativeArray<float3>.ReadOnly OrePositions;
        [ReadOnly, NoAlias] public NativeArray<int>.ReadOnly OreTypes;

        [NoAlias] public NativeSlice<float3> GprHits;
        [NoAlias] public NativeSlice<float> GprSignalStrength;
        [NoAlias] public NativeSlice<float> GprAgeSeconds;
        [NoAlias] public NativeSlice<int> GprOreTypes;
        [NoAlias] public NativeSlice<float4> GprPingGpu;
        [NoAlias] public NativeSlice<int> Counters;
        [NoAlias] public NativeSlice<float> MaxSignalStrength;

        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public int OreScanCount;
        public int OreFilterType;
        public int PreviousActiveCount;
        public int RequestedRayCount;
        public int MaxSteps;
        public float3 ProbeOrigin;
        public float ScanRadiusMeters;
        public float StepMeters;
        public float DeltaTime;
        public float3 RuntimeShift;
        public uint Flags;

        public void Execute()
        {
            int writeIndex = CompactExistingPings(out float highestSignal);
            int addedCount = 0;

            int rayCount = math.clamp(RequestedRayCount, 1, GroundRadarConstants.MaxRays);
            int maxSteps = math.clamp(MaxSteps, 1, GroundRadarConstants.MaxRaymarchSteps);
            if ((Flags & GroundRadarConstants.ScanFlag) != 0u && HasWritablePingStorage() && HasValidSdf() && OrePositions.Length > 0 && OreTypes.Length > 0 && OreScanCount > 0)
            {
                float scanRadius = math.max(1f, ScanRadiusMeters);
                float stepMeters = math.max(0.5f, StepMeters);
                int side = GroundRadarConstants.ResolveRayGridSide(rayCount);
                float invSideMinusOne = math.rcp(math.max(1, side - 1));
                int oreCount = math.min(math.min(OreScanCount, OrePositions.Length), OreTypes.Length);

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

                        if (!TryReadOreHit(samplePosition, oreCount, out float3 orePosition, out int oreType))
                            break;

                        if (writeIndex < GprHits.Length)
                        {
                            float signalStrength = math.saturate(math.rcp(math.max(1f, depth * depth)));
                            float displayStrength = ApplyOreFilter(signalStrength, oreType);
                            GprHits[writeIndex] = orePosition;
                            GprSignalStrength[writeIndex] = signalStrength;
                            GprAgeSeconds[writeIndex] = 0f;
                            GprOreTypes[writeIndex] = oreType;
                            GprPingGpu[writeIndex] = new float4(orePosition, displayStrength);
                            highestSignal = math.max(highestSignal, displayStrength);
                            writeIndex++;
                            addedCount++;
                        }

                        break;
                    }
                }
            }

            if (Counters.Length > 0)
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

            if (MaxSignalStrength.Length > 0)
                MaxSignalStrength[0] = highestSignal;
        }

        private int CompactExistingPings(out float highestSignal)
        {
            highestSignal = 0f;
            if (!HasWritablePingStorage())
                return 0;

            int previousCount = math.min(math.max(0, PreviousActiveCount), GprHits.Length);
            float safeDelta = math.max(0f, DeltaTime);
            float3 shift = (Flags & GroundRadarConstants.AupShiftFlag) != 0u ? RuntimeShift : float3.zero;
            int writeIndex = 0;

            for (int i = 0; i < previousCount; i++)
            {
                float age = GprAgeSeconds[i] + safeDelta;
                if (age >= GroundRadarConstants.ScanDecaySeconds)
                    continue;

                float rawStrength = math.saturate(GprSignalStrength[i]);
                float displayStrength = rawStrength * math.saturate(1f - age * math.rcp(GroundRadarConstants.ScanDecaySeconds));
                if (displayStrength <= 0.0001f)
                    continue;

                float3 hit = GprHits[i] - shift;
                if (!math.all(math.isfinite(hit)) || !math.isfinite(displayStrength))
                    continue;

                int oreType = GprOreTypes[i];
                float filteredStrength = ApplyOreFilter(displayStrength, oreType);
                GprHits[writeIndex] = hit;
                GprSignalStrength[writeIndex] = rawStrength;
                GprAgeSeconds[writeIndex] = age;
                GprOreTypes[writeIndex] = oreType;
                GprPingGpu[writeIndex] = new float4(hit, filteredStrength);
                highestSignal = math.max(highestSignal, filteredStrength);
                writeIndex++;
            }

            return writeIndex;
        }

        private bool HasValidSdf()
        {
            return EncodedSdf.Length > 0 &&
                   GridDimensions.x > 1 &&
                   GridDimensions.y > 1 &&
                   GridDimensions.z > 1 &&
                   math.all(CellSize > new float3(0.0001f)) &&
                   math.isfinite(SdfRange) &&
                   SdfRange > 0f &&
                   math.all(math.isfinite(ProbeOrigin));
        }

        private bool HasWritablePingStorage()
        {
            return GprHits.Length > 0 &&
                   GprSignalStrength.Length > 0 &&
                   GprAgeSeconds.Length > 0 &&
                   GprOreTypes.Length > 0 &&
                   GprPingGpu.Length > 0;
        }

        private float SampleDensity(float3 runtimePosition)
        {
            float3 local = runtimePosition - VolumeOrigin;
            float3 grid = local * math.rcp(CellSize);
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

            float normalized = EncodedSdf[index] * GroundRadarConstants.InverseByteMax;
            float signedDistance = (normalized * 2f - 1f) * SdfRange;
            return math.saturate(0.5f - signedDistance * math.rcp(math.max(0.001f, SdfRange)));
        }

        private float ApplyOreFilter(float signalStrength, int oreType)
        {
            if (OreFilterType == 0 || oreType == OreFilterType)
                return signalStrength;

            return signalStrength * GroundRadarConstants.FilteredOreAlphaScale;
        }

        private bool TryReadOreHit(float3 samplePosition, int oreCount, out float3 orePosition, out int oreType)
        {
            orePosition = default;
            oreType = 0;
            float bestDistanceSq = GroundRadarConstants.OreMatchDistanceSq;
            int bestIndex = -1;
            for (int i = 0; i < oreCount; i++)
            {
                int candidateType = OreTypes[i];
                if (candidateType == 0)
                    continue;

                float3 candidate = OrePositions[i];
                float distanceSq = math.lengthsq(candidate - samplePosition);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestIndex = i;
                orePosition = candidate;
                oreType = candidateType;
            }

            return bestIndex >= 0;
        }
    }
}
