using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.IK
{
    public static class LeviathanTerrainIkConstants
    {
        public const int MaxSegments = 20;
        public const int LowTierSegments = 8;
        public const int TerrainHugSegmentCount = 5;
        public const int TelemetryCapacity = 300;
        public const uint TelemetryFlagActive = 1u << 0;
        public const uint TelemetryFlagSdf = 1u << 1;
        public const uint TelemetryFlagMapMagic = 1u << 2;
        public const uint TelemetryFlagTailWhip = 1u << 3;
        public const uint TelemetryFlagLowTier = 1u << 4;
        public const uint TelemetryFlagInvalid = 1u << 31;
        public const uint RuntimeFlagSdfHugging = 1u << 0;
        public const uint RuntimeFlagTerrainFallback = 1u << 1;
        public const uint RuntimeFlagLowTier = 1u << 2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 96)]
    public struct LeviathanTerrainIkTelemetryEntry
    {
        public int FrameIndex;
        public int ActiveSegmentCount;
        public uint Flags;
        public uint StateHash;
        public float3 HeadPosition;
        public float3 TailPosition;
        public float3 IntendedVelocity;
        public float MaxTerrainPushMeters;
        public float TailWhipSecondsRemaining;
        public float Padding0;
        public float Padding1;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct LeviathanTerrainIkJob : IJob
    {
        private const float MinLengthSq = 0.000001f;
        private const float InvEncodedByteMax = 0.0039215686274509803f;

        public NativeArray<float3> SegmentPositions;
        public NativeArray<float3> PreviousSegmentPositions;
        public NativeArray<float4x4> LeviathanBones;
        public NativeArray<LeviathanTerrainIkTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        [ReadOnly] public NativeArray<byte> VoxelSdfTexture3D;
        [ReadOnly] public NativeArray<ushort> TerrainHeightSamples;
        public int3 VoxelSdfDimensions;
        public float3 VoxelSdfOrigin;
        public float3 VoxelSdfCellSize;
        public float VoxelSdfRange;
        public float3 TerrainOrigin;
        public float3 TerrainSize;
        public int TerrainResolution;
        public float DeltaTime;
        public float Damping;
        public float SegmentLength;
        public float BodyRadius;
        public float TerrainClearance;
        public float PhaseTimeSeconds;
        public float TailWhipSecondsRemaining;
        public float TailWhipDurationSeconds;
        public float TailWhipAmplitudeMeters;
        public float3 HeadTargetPosition;
        public float3 IntendedVelocity;
        public float3 OwnerForward;
        public float3 WorldUp;
        public int RequestedSegmentCount;
        public int ConstraintIterations;
        public int FrameIndex;
        public uint RuntimeFlags;

        public void Execute()
        {
            if (!SegmentPositions.IsCreated ||
                !PreviousSegmentPositions.IsCreated ||
                !LeviathanBones.IsCreated ||
                SegmentPositions.Length < 2 ||
                PreviousSegmentPositions.Length < 2 ||
                LeviathanBones.Length < 2)
            {
                return;
            }

            int maxUsableSegments = math.min(LeviathanTerrainIkConstants.MaxSegments, math.min(SegmentPositions.Length, LeviathanBones.Length));
            bool lowTier = (RuntimeFlags & LeviathanTerrainIkConstants.RuntimeFlagLowTier) != 0u;
            int requested = lowTier
                ? LeviathanTerrainIkConstants.LowTierSegments
                : RequestedSegmentCount;
            int activeCount = math.clamp(requested, 2, maxUsableSegments);
            int iterations = math.clamp(ConstraintIterations, 1, 4);
            float dt = math.select(0f, math.min(DeltaTime, 0.05f), math.isfinite(DeltaTime) && DeltaTime > 0f);
            float damping = SanitizeFiniteClamp(Damping, 0.87f, 0f, 1f);
            float segmentLength = SanitizePositiveFinite(SegmentLength, 2.5f, 0.05f);
            float bodyRadius = SanitizePositiveFinite(BodyRadius, 1.15f, 0.01f);
            float clearance = SanitizePositiveFinite(TerrainClearance, 0f, 0f);
            float tailWhipSecondsRemaining = SanitizePositiveFinite(TailWhipSecondsRemaining, 0f, 0f);
            float tailWhipDurationSeconds = SanitizePositiveFinite(TailWhipDurationSeconds, 1f, 0.1f);
            float tailWhipAmplitudeMeters = SanitizePositiveFinite(TailWhipAmplitudeMeters, 0f, 0f);
            float3 ownerForward = NormalizeSafe(OwnerForward, new float3(0f, 0f, 1f));
            float3 up = NormalizeSafe(WorldUp, new float3(0f, 1f, 0f));
            float3 intended = SanitizeFinite(IntendedVelocity, float3.zero);
            float maxTerrainPush = 0f;
            uint telemetryFlags = LeviathanTerrainIkConstants.TelemetryFlagActive;
            if (lowTier)
                telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagLowTier;

            MoveHead(dt, segmentLength, intended, ownerForward);
            IntegrateFollowers(activeCount, dt, damping, intended);

            for (int iteration = 0; iteration < iterations; iteration++)
                PullDistanceConstraints(activeCount, segmentLength, ownerForward);

            if (tailWhipSecondsRemaining > 0f)
            {
                telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagTailWhip;
                ApplyTailWhip(activeCount, segmentLength, ownerForward, up, tailWhipSecondsRemaining, tailWhipDurationSeconds, tailWhipAmplitudeMeters);
                PullDistanceConstraints(activeCount, segmentLength, ownerForward);
            }

            float sdfRange = SanitizePositiveFinite(VoxelSdfRange, 0f, 0f);
            float3 sdfCellSize = SanitizePositiveFinite(VoxelSdfCellSize, new float3(0.0001f), new float3(0.0001f));
            float3 sdfGradientStep = math.max(sdfCellSize, new float3(0.05f));
            bool canUseSdf = !lowTier &&
                             (RuntimeFlags & LeviathanTerrainIkConstants.RuntimeFlagSdfHugging) != 0u &&
                             VoxelSdfTexture3D.IsCreated &&
                             math.all(math.isfinite(VoxelSdfOrigin)) &&
                             TryResolveSdfVoxelCount(VoxelSdfDimensions, out int expectedSdfLength) &&
                             VoxelSdfTexture3D.Length >= expectedSdfLength &&
                             sdfRange > 0.0001f;
            float3 sdfInvCellSize = canUseSdf
                ? math.rcp(sdfCellSize)
                : float3.zero;
            bool canUseHeight = (RuntimeFlags & LeviathanTerrainIkConstants.RuntimeFlagTerrainFallback) != 0u &&
                                TerrainHeightSamples.IsCreated &&
                                TryResolveTerrainHeightSampleCount(TerrainResolution, out int expectedTerrainLength) &&
                                TerrainHeightSamples.Length >= expectedTerrainLength &&
                                math.all(math.isfinite(TerrainOrigin)) &&
                                math.all(math.isfinite(TerrainSize)) &&
                                TerrainSize.x > 0.0001f &&
                                TerrainSize.y > 0.0001f &&
                                TerrainSize.z > 0.0001f;

            int terrainStart = math.max(0, activeCount - LeviathanTerrainIkConstants.TerrainHugSegmentCount);
            for (int index = terrainStart; index < activeCount; index++)
            {
                bool tailBypass = tailWhipSecondsRemaining > 0f && index >= activeCount >> 1;
                if (tailBypass)
                    continue;

                float3 position = SegmentPositions[index];
                float appliedPush = 0f;
                if (canUseSdf &&
                    TrySampleSdfTrilinear(position, sdfInvCellSize, sdfRange, out float density) &&
                    density > 0f &&
                    TryResolveSdfGradient(position, sdfInvCellSize, sdfRange, sdfGradientStep, out float3 normal))
                {
                    appliedPush = density + clearance;
                    SegmentPositions[index] = SanitizeFinite(position + normal * appliedPush, position);
                    telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagSdf;
                }
                else if (canUseHeight &&
                         TrySampleTerrainHeight(position.x, position.z, out float height, out float3 terrainNormal))
                {
                    float targetHeight = height + clearance;
                    if (position.y < targetHeight)
                    {
                        appliedPush = targetHeight - position.y;
                        SegmentPositions[index] = SanitizeFinite(position + terrainNormal * appliedPush, new float3(position.x, targetHeight, position.z));
                        telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagMapMagic;
                    }
                }

                maxTerrainPush = math.max(maxTerrainPush, appliedPush);
            }

            PullDistanceConstraints(activeCount, segmentLength, ownerForward);
            WriteMatrices(activeCount, maxUsableSegments, segmentLength, bodyRadius, up, ownerForward);

            bool invalid = HasInvalidSegment(activeCount);
            if (invalid)
                telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagInvalid;

            WriteTelemetry(activeCount, telemetryFlags, intended, maxTerrainPush, tailWhipSecondsRemaining);
        }

        private void MoveHead(float dt, float segmentLength, float3 intended, float3 ownerForward)
        {
            float3 current = SanitizeFinite(SegmentPositions[0], HeadTargetPosition);
            float3 target = SanitizeFinite(HeadTargetPosition, current + ownerForward * segmentLength);
            float3 delta = target - current;
            float distanceSq = math.lengthsq(delta);
            float intendedSpeed = ResolveLength(intended);
            float maxStep = math.max(segmentLength * 0.25f, intendedSpeed * dt + segmentLength * 0.5f);
            if (distanceSq > maxStep * maxStep && distanceSq > MinLengthSq)
                target = current + delta * math.rsqrt(distanceSq) * maxStep;

            SegmentPositions[0] = SanitizeFinite(target, current);
            PreviousSegmentPositions[0] = SegmentPositions[0];
        }

        private void IntegrateFollowers(int activeCount, float dt, float damping, float3 intended)
        {
            float dtSq = dt * dt;
            for (int i = 1; i < activeCount; i++)
            {
                float3 current = SanitizeFinite(SegmentPositions[i], SegmentPositions[i - 1]);
                float3 previous = SanitizeFinite(PreviousSegmentPositions[i], current);
                float3 velocity = (current - previous) * damping;
                float taper = 1f - i * math.rcp(math.max(1, activeCount - 1));
                float3 drift = intended * (0.04f * taper) * dtSq;
                PreviousSegmentPositions[i] = current;
                SegmentPositions[i] = SanitizeFinite(current + velocity + drift, current);
            }
        }

        private void PullDistanceConstraints(int activeCount, float segmentLength, float3 ownerForward)
        {
            for (int i = 1; i < activeCount; i++)
            {
                float3 parent = SegmentPositions[i - 1];
                float3 child = SanitizeFinite(SegmentPositions[i], parent - ownerForward * segmentLength);
                float3 delta = child - parent;
                float lengthSq = math.lengthsq(delta);
                float3 direction = lengthSq > MinLengthSq
                    ? delta * math.rsqrt(lengthSq)
                    : -ownerForward;
                SegmentPositions[i] = SanitizeFinite(parent + direction * segmentLength, parent - ownerForward * segmentLength);
            }
        }

        private void ApplyTailWhip(
            int activeCount,
            float segmentLength,
            float3 ownerForward,
            float3 up,
            float tailWhipSecondsRemaining,
            float tailWhipDurationSeconds,
            float tailWhipAmplitudeMeters)
        {
            float normalizedAge = math.saturate(1f - tailWhipSecondsRemaining * math.rcp(tailWhipDurationSeconds));
            float3 side = NormalizeSafe(math.cross(up, ownerForward), new float3(1f, 0f, 0f));
            int firstTail = math.max(1, activeCount >> 1);
            for (int i = firstTail; i < activeCount; i++)
            {
                float t = (i - firstTail) * math.rcp(math.max(1, activeCount - firstTail));
                float wave = CheapSinSigned((normalizedAge * 3.2f) + t * 1.7f);
                float falloff = t * t;
                float3 impulse = side * (wave * tailWhipAmplitudeMeters * falloff);
                SegmentPositions[i] = SanitizeFinite(SegmentPositions[i] + impulse, SegmentPositions[i - 1] - ownerForward * segmentLength);
            }
        }

        private void WriteMatrices(int activeCount, int maxUsableSegments, float segmentLength, float bodyRadius, float3 up, float3 ownerForward)
        {
            float3 tailForward = ownerForward;
            for (int i = 0; i < activeCount; i++)
            {
                float3 position = SanitizeFinite(SegmentPositions[i], float3.zero);
                float3 tangent;
                if (i + 1 < activeCount)
                    tangent = position - SegmentPositions[i + 1];
                else
                    tangent = SegmentPositions[i - 1] - position;

                tangent = NormalizeSafe(tangent, tailForward);
                tailForward = tangent;
                quaternion rotation = quaternion.LookRotationSafe(tangent, up);
                LeviathanBones[i] = float4x4.TRS(position, rotation, new float3(bodyRadius, bodyRadius, segmentLength));
            }

            float3 tail = SegmentPositions[activeCount - 1];
            for (int i = activeCount; i < maxUsableSegments; i++)
            {
                tail -= tailForward * segmentLength;
                SegmentPositions[i] = tail;
                PreviousSegmentPositions[i] = tail;
                LeviathanBones[i] = float4x4.TRS(tail, quaternion.LookRotationSafe(tailForward, up), new float3(bodyRadius, bodyRadius, segmentLength));
            }
        }

        private bool TrySampleSdfTrilinear(float3 worldPosition, float3 invCellSize, float sdfRange, out float density)
        {
            density = 0f;
            if (!VoxelSdfTexture3D.IsCreated ||
                VoxelSdfDimensions.x <= 1 ||
                VoxelSdfDimensions.y <= 1 ||
                VoxelSdfDimensions.z <= 1 ||
                sdfRange <= 0.0001f)
            {
                return false;
            }

            float3 sample = (worldPosition - VoxelSdfOrigin) * invCellSize;
            if (sample.x < 0f || sample.y < 0f || sample.z < 0f ||
                sample.x > VoxelSdfDimensions.x - 1f ||
                sample.y > VoxelSdfDimensions.y - 1f ||
                sample.z > VoxelSdfDimensions.z - 1f)
            {
                return false;
            }

            sample = math.clamp(sample, float3.zero, new float3(VoxelSdfDimensions.x - 1.001f, VoxelSdfDimensions.y - 1.001f, VoxelSdfDimensions.z - 1.001f));
            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, VoxelSdfDimensions.x - 1);
            int y1 = math.min(y0 + 1, VoxelSdfDimensions.y - 1);
            int z1 = math.min(z0 + 1, VoxelSdfDimensions.z - 1);
            float3 f = sample - new float3(x0, y0, z0);
            float c000 = DecodeSdf(SdfIndex(x0, y0, z0), sdfRange);
            float c100 = DecodeSdf(SdfIndex(x1, y0, z0), sdfRange);
            float c010 = DecodeSdf(SdfIndex(x0, y1, z0), sdfRange);
            float c110 = DecodeSdf(SdfIndex(x1, y1, z0), sdfRange);
            float c001 = DecodeSdf(SdfIndex(x0, y0, z1), sdfRange);
            float c101 = DecodeSdf(SdfIndex(x1, y0, z1), sdfRange);
            float c011 = DecodeSdf(SdfIndex(x0, y1, z1), sdfRange);
            float c111 = DecodeSdf(SdfIndex(x1, y1, z1), sdfRange);
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            density = math.lerp(c0, c1, f.z);
            return math.isfinite(density);
        }

        private bool TryResolveSdfGradient(float3 worldPosition, float3 invCellSize, float sdfRange, float3 step, out float3 normal)
        {
            normal = new float3(0f, 1f, 0f);
            bool x0 = TrySampleSdfTrilinear(worldPosition - new float3(step.x, 0f, 0f), invCellSize, sdfRange, out float dx0);
            bool x1 = TrySampleSdfTrilinear(worldPosition + new float3(step.x, 0f, 0f), invCellSize, sdfRange, out float dx1);
            bool y0 = TrySampleSdfTrilinear(worldPosition - new float3(0f, step.y, 0f), invCellSize, sdfRange, out float dy0);
            bool y1 = TrySampleSdfTrilinear(worldPosition + new float3(0f, step.y, 0f), invCellSize, sdfRange, out float dy1);
            bool z0 = TrySampleSdfTrilinear(worldPosition - new float3(0f, 0f, step.z), invCellSize, sdfRange, out float dz0);
            bool z1 = TrySampleSdfTrilinear(worldPosition + new float3(0f, 0f, step.z), invCellSize, sdfRange, out float dz1);
            if (!x0 || !x1 || !y0 || !y1 || !z0 || !z1)
                return false;

            float3 gradient = new float3(dx1 - dx0, dy1 - dy0, dz1 - dz0);
            normal = NormalizeSafe(gradient, new float3(0f, 1f, 0f));
            return math.all(math.isfinite(normal));
        }

        private bool TrySampleTerrainHeight(float worldX, float worldZ, out float height, out float3 normal)
        {
            height = 0f;
            normal = new float3(0f, 1f, 0f);
            if (!TerrainHeightSamples.IsCreated ||
                !TryResolveTerrainHeightSampleCount(TerrainResolution, out int expectedLength) ||
                TerrainHeightSamples.Length < expectedLength ||
                TerrainSize.x <= 0.0001f ||
                TerrainSize.y <= 0.0001f ||
                TerrainSize.z <= 0.0001f)
            {
                return false;
            }

            float normalizedX = math.saturate((worldX - TerrainOrigin.x) * math.rcp(TerrainSize.x));
            float normalizedZ = math.saturate((worldZ - TerrainOrigin.z) * math.rcp(TerrainSize.z));
            float sampleX = normalizedX * (TerrainResolution - 1);
            float sampleZ = normalizedZ * (TerrainResolution - 1);
            int x0 = math.clamp((int)math.floor(sampleX), 0, TerrainResolution - 1);
            int z0 = math.clamp((int)math.floor(sampleZ), 0, TerrainResolution - 1);
            int x1 = math.min(x0 + 1, TerrainResolution - 1);
            int z1 = math.min(z0 + 1, TerrainResolution - 1);
            float fracX = sampleX - x0;
            float fracZ = sampleZ - z0;
            float h00 = DecodeTerrainHeight(x0, z0);
            float h10 = DecodeTerrainHeight(x1, z0);
            float h01 = DecodeTerrainHeight(x0, z1);
            float h11 = DecodeTerrainHeight(x1, z1);
            float h0 = math.lerp(h00, h10, fracX);
            float h1 = math.lerp(h01, h11, fracX);
            height = TerrainOrigin.y + math.lerp(h0, h1, fracZ);
            float gradientX = (h10 - h00) * math.rcp(math.max(0.0001f, TerrainSize.x * math.rcp(TerrainResolution - 1)));
            float gradientZ = (h01 - h00) * math.rcp(math.max(0.0001f, TerrainSize.z * math.rcp(TerrainResolution - 1)));
            normal = NormalizeSafe(new float3(-gradientX, 1f, -gradientZ), new float3(0f, 1f, 0f));
            return math.isfinite(height);
        }

        private float DecodeTerrainHeight(int x, int z)
        {
            int index = math.clamp(z, 0, TerrainResolution - 1) * TerrainResolution + math.clamp(x, 0, TerrainResolution - 1);
            return TerrainHeightSamples[index] * (1f / 65535f) * TerrainSize.y;
        }

        private float DecodeSdf(int index, float sdfRange)
        {
            if ((uint)index >= (uint)VoxelSdfTexture3D.Length)
                return -sdfRange;

            return ((VoxelSdfTexture3D[index] * InvEncodedByteMax) * 2f - 1f) * sdfRange;
        }

        private int SdfIndex(int x, int y, int z)
        {
            return (z * VoxelSdfDimensions.y + y) * VoxelSdfDimensions.x + x;
        }

        private void WriteTelemetry(int activeCount, uint flags, float3 intended, float maxTerrainPush, float tailWhipSecondsRemaining)
        {
            if (!TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length <= 0 || TelemetryCursor.Length <= 0)
                return;

            int cursor = TelemetryCursor[0];
            int index = cursor % TelemetryRing.Length;
            if (index < 0)
                index += TelemetryRing.Length;

            float3 head = SegmentPositions[0];
            float3 tail = SegmentPositions[activeCount - 1];
            LeviathanTerrainIkTelemetryEntry entry = new LeviathanTerrainIkTelemetryEntry
            {
                FrameIndex = FrameIndex,
                ActiveSegmentCount = activeCount,
                Flags = flags,
                StateHash = ComputeTelemetryHash(head, tail, intended, activeCount),
                HeadPosition = SanitizeFinite(head, float3.zero),
                TailPosition = SanitizeFinite(tail, float3.zero),
                IntendedVelocity = SanitizeFinite(intended, float3.zero),
                MaxTerrainPushMeters = math.select(0f, maxTerrainPush, math.isfinite(maxTerrainPush)),
                TailWhipSecondsRemaining = tailWhipSecondsRemaining
            };
            TelemetryRing[index] = entry;
            if (cursor == int.MaxValue)
            {
                int nextIndex = index + 1;
                if (nextIndex >= TelemetryRing.Length)
                    nextIndex = 0;

                TelemetryCursor[0] = TelemetryRing.Length + nextIndex;
            }
            else
            {
                TelemetryCursor[0] = cursor + 1;
            }
        }

        private bool HasInvalidSegment(int activeCount)
        {
            for (int i = 0; i < activeCount; i++)
            {
                if (!math.all(math.isfinite(SegmentPositions[i])))
                    return true;
            }

            return false;
        }

        private static uint ComputeTelemetryHash(float3 head, float3 tail, float3 intended, int activeCount)
        {
            uint hash = 2166136261u;
            hash = HashFloat3(hash, head);
            hash = HashFloat3(hash, tail);
            hash = HashFloat3(hash, intended);
            hash = (hash ^ (uint)activeCount) * 16777619u;
            return hash;
        }

        private static uint HashFloat3(uint hash, float3 value)
        {
            hash = (hash ^ (uint)math.asint(value.x)) * 16777619u;
            hash = (hash ^ (uint)math.asint(value.y)) * 16777619u;
            hash = (hash ^ (uint)math.asint(value.z)) * 16777619u;
            return hash;
        }

        private static float ResolveLength(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > MinLengthSq ? lengthSq * math.rsqrt(lengthSq) : 0f;
        }

        public static bool TryResolveSdfVoxelCount(int3 dimensions, out int voxelCount)
        {
            voxelCount = 0;
            if (dimensions.x <= 1 || dimensions.y <= 1 || dimensions.z <= 1)
                return false;

            long count = (long)dimensions.x * dimensions.y * dimensions.z;
            if (count <= 0L || count > int.MaxValue)
                return false;

            voxelCount = (int)count;
            return true;
        }

        public static bool TryResolveTerrainHeightSampleCount(int resolution, out int sampleCount)
        {
            sampleCount = 0;
            if (resolution <= 1)
                return false;

            long count = (long)resolution * resolution;
            if (count <= 0L || count > int.MaxValue)
                return false;

            sampleCount = (int)count;
            return true;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinLengthSq)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float SanitizePositiveFinite(float value, float fallback, float minValue)
        {
            return math.isfinite(value) ? math.max(value, minValue) : fallback;
        }

        private static float3 SanitizePositiveFinite(float3 value, float3 fallback, float3 minValue)
        {
            return math.all(math.isfinite(value)) ? math.max(value, minValue) : fallback;
        }

        private static float SanitizeFiniteClamp(float value, float fallback, float minValue, float maxValue)
        {
            return math.isfinite(value) ? math.clamp(value, minValue, maxValue) : fallback;
        }

        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private static float CheapSinSigned(float cycle)
        {
            float triangle = math.abs(math.frac(cycle) * 2f - 1f);
            return 1f - triangle * 2f;
        }
    }
}
