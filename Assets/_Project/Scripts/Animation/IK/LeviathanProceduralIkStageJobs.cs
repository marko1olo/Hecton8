using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.IK
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LeviathanMockTargetDTO
    {
        [FieldOffset(0)] public double3 TargetAup;
        [FieldOffset(24)] public uint SectorHash;
        [FieldOffset(28)] public int FrameIndex;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct MockLeviathanTargetJob : IJob
    {
        [NoAlias] public NativeArray<LeviathanMockTargetDTO> TargetOutput;
        public double3 RootAup;
        public uint SectorHash;
        public int SimulationFrame;
        public double SimulationTickDelta;
        public double OrbitRadiusMeters;
        public double VerticalAmplitudeMeters;

        public void Execute()
        {
            if (!TargetOutput.IsCreated || TargetOutput.Length <= 0)
                return;

            double safeTickDelta = math.select(0.016666666666666666d, math.min(math.max(SimulationTickDelta, 0.001d), 0.05d), math.isfinite(SimulationTickDelta) && SimulationTickDelta > 0d);
            double radius = math.select(18d, math.max(1d, OrbitRadiusMeters), math.isfinite(OrbitRadiusMeters) && OrbitRadiusMeters > 0d);
            double vertical = math.select(4d, math.max(0d, VerticalAmplitudeMeters), math.isfinite(VerticalAmplitudeMeters) && VerticalAmplitudeMeters >= 0d);
            uint seed = (SectorHash ^ (uint)math.max(0, SimulationFrame) * 747796405u) | 1u;
            double seedPhase = (seed & 1023u) * 0.006135923151542565d;
            double phase = SimulationFrame * safeTickDelta * 0.47d + seedPhase;
            double3 target = RootAup + new double3(
                math.cos(phase) * radius,
                math.sin(phase * 0.37d) * vertical,
                math.sin(phase) * radius);

            LeviathanMockTargetDTO dto = default;
            dto.TargetAup = math.all(math.isfinite(target)) ? target : RootAup;
            dto.SectorHash = SectorHash;
            dto.FrameIndex = SimulationFrame;
            TargetOutput[0] = dto;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct ProceduralSpineMotionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> SegmentPositions;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        public float SimulationTimeSeconds;
        public float ForwardVelocityMetersPerSecond;
        public float GlobalQualityWeight;
        public float BaseAmplitudeMeters;
        public float WaveFrequencyHz;
        public float PhaseOffset;
        public float3 SideAxis;
        public int ActiveSegmentCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SegmentPositions.Length || index >= ActiveSegmentCount)
                return;

            float quality = LeviathanProceduralMath.Smooth01(GlobalQualityWeight);
            float speed = math.max(0f, LeviathanProceduralMath.SanitizeFinite(ForwardVelocityMetersPerSecond, 0f));
            float amplitude = math.max(0f, LeviathanProceduralMath.SanitizeFinite(BaseAmplitudeMeters, 0.25f)) *
                math.lerp(0.2f, 1f, quality) *
                math.saturate(speed * 0.2f + 0.15f);
            float frequency = math.max(0.01f, LeviathanProceduralMath.SanitizeFinite(WaveFrequencyHz, 0.6f));
            float phaseOffset = math.max(0f, LeviathanProceduralMath.SanitizeFinite(PhaseOffset, 0.45f));
            float phase = SimulationTimeSeconds * frequency - index * phaseOffset;
            float wave = LeviathanProceduralMath.CheapSinSigned(phase);
            float taper = index * math.rcp(math.max(1, ActiveSegmentCount - 1));
            float3 side = LeviathanProceduralMath.NormalizeSafe(SideAxis, new float3(1f, 0f, 0f));
            float3 position = LeviathanProceduralMath.SanitizeFinite(SegmentPositions[index], float3.zero);
            SegmentPositions[index] = position + side * (wave * amplitude * taper);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct InverseKinematicsFABRIKJob : IJob
    {
        [NoAlias] public NativeArray<float3> ChainPositions;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        public float3 RootPosition;
        public float3 TargetPosition;
        public float GlobalQualityWeight;
        public float DefaultSegmentLength;
        public float ToleranceMeters;
        public int ChainStartIndex;
        public int ChainCount;

        public void Execute()
        {
            if (!ChainPositions.IsCreated || ChainPositions.Length <= 1)
                return;

            int start = math.clamp(ChainStartIndex, 0, ChainPositions.Length - 1);
            int count = math.clamp(ChainCount, 2, ChainPositions.Length - start);
            int end = start + count - 1;
            int iterations = math.clamp((int)math.round(math.lerp(1f, 10f, LeviathanProceduralMath.Smooth01(GlobalQualityWeight))), 1, 10);
            float toleranceSq = math.max(0.000001f, ToleranceMeters * ToleranceMeters);
            float3 root = LeviathanProceduralMath.SanitizeFinite(RootPosition, ChainPositions[start]);
            float3 target = LeviathanProceduralMath.SanitizeFinite(TargetPosition, ChainPositions[end]);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                ChainPositions[end] = target;
                for (int i = end - 1; i >= start; i--)
                {
                    float3 child = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i + 1], target);
                    float3 current = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i], child);
                    float length = ResolveLength(i + 1);
                    ChainPositions[i] = child + LeviathanProceduralMath.NormalizeSafe(current - child, new float3(0f, 0f, -1f)) * length;
                }

                ChainPositions[start] = root;
                for (int i = start + 1; i <= end; i++)
                {
                    float3 parent = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i - 1], root);
                    float3 current = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i], parent);
                    float length = ResolveLength(i);
                    ChainPositions[i] = parent + LeviathanProceduralMath.NormalizeSafe(current - parent, new float3(0f, 0f, 1f)) * length;
                }

                float errorSq = math.lengthsq(ChainPositions[end] - target);
                if (math.isfinite(errorSq) && errorSq <= toleranceSq)
                    break;
            }
        }

        private float ResolveLength(int index)
        {
            if (BoneConstraints.IsCreated && (uint)index < (uint)BoneConstraints.Length)
                return math.max(LeviathanTerrainIkConstants.MinSegmentLength, BoneConstraints[index].SegmentLengthMeters);

            return math.max(LeviathanTerrainIkConstants.MinSegmentLength, DefaultSegmentLength);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct SecondaryMotionSpringJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> BonePositions;
        [NoAlias] public NativeArray<float3> BoneVelocities;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        public float DeltaTime;
        public float GlobalQualityWeight;
        public float SpringStrength;
        public float Damping;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)BonePositions.Length || index == 0)
                return;

            float dt = math.select(0f, math.min(DeltaTime, 0.05f), math.isfinite(DeltaTime) && DeltaTime > 0f);
            float spring = math.max(0f, LeviathanProceduralMath.SanitizeFinite(SpringStrength, 18f)) *
                math.lerp(0.25f, 1f, LeviathanProceduralMath.Smooth01(GlobalQualityWeight));
            float damping = math.saturate(LeviathanProceduralMath.SanitizeFinite(Damping, 0.82f));
            float3 parent = LeviathanProceduralMath.SanitizeFinite(BonePositions[index - 1], float3.zero);
            float3 current = LeviathanProceduralMath.SanitizeFinite(BonePositions[index], parent);
            float length = BoneConstraints.IsCreated && (uint)index < (uint)BoneConstraints.Length
                ? math.max(LeviathanTerrainIkConstants.MinSegmentLength, BoneConstraints[index].SegmentLengthMeters)
                : LeviathanTerrainIkConstants.DefaultSegmentLength;
            float3 target = parent + LeviathanProceduralMath.NormalizeSafe(current - parent, new float3(0f, 0f, -1f)) * length;
            float3 velocity = LeviathanProceduralMath.SanitizeFinite(BoneVelocities[index], float3.zero);
            velocity = (velocity + (target - current) * spring * dt) * damping;
            BoneVelocities[index] = velocity;
            BonePositions[index] = LeviathanProceduralMath.SanitizeFinite(current + velocity * dt, target);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct ComputeFinalBoneMatricesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<float3> BonePositions;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        [NoAlias] public NativeArray<LeviathanBoneDTO> BoneMatrices;
        public float3 Up;
        public float BodyRadius;
        public float DefaultSegmentLength;
        public int ActiveBoneCount;

        public void Execute()
        {
            if (!BonePositions.IsCreated || !BoneMatrices.IsCreated)
                return;

            int count = math.clamp(ActiveBoneCount, 1, math.min(BonePositions.Length, BoneMatrices.Length));
            float3 up = LeviathanProceduralMath.NormalizeSafe(Up, new float3(0f, 1f, 0f));
            float3 lastForward = new float3(0f, 0f, 1f);
            for (int i = 0; i < count; i++)
            {
                float3 position = LeviathanProceduralMath.SanitizeFinite(BonePositions[i], float3.zero);
                float3 next = i + 1 < count ? LeviathanProceduralMath.SanitizeFinite(BonePositions[i + 1], position + lastForward) : position + lastForward;
                float3 forward = LeviathanProceduralMath.NormalizeSafe(next - position, lastForward);
                lastForward = forward;
                float length = BoneConstraints.IsCreated && (uint)i < (uint)BoneConstraints.Length
                    ? math.max(LeviathanTerrainIkConstants.MinSegmentLength, BoneConstraints[i].SegmentLengthMeters)
                    : math.max(LeviathanTerrainIkConstants.MinSegmentLength, DefaultSegmentLength);
                float radius = math.max(0.01f, BodyRadius);
                LeviathanBoneDTO dto = default;
                dto.LocalToWorld = float4x4.TRS(position, quaternion.LookRotationSafe(forward, up), new float3(radius, radius, length));
                BoneMatrices[i] = dto;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct StageCreatureCollidersJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneDTO> BoneMatrices;
        [NoAlias] public NativeArray<LeviathanCapsuleColliderDTO> ColliderProxies;
        public float BodyRadius;
        public uint OwnerHash;
        public int FrameIndex;
        public int ActiveBoneCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)BoneMatrices.Length ||
                (uint)index >= (uint)ColliderProxies.Length ||
                index >= ActiveBoneCount)
            {
                return;
            }

            float4x4 matrix = BoneMatrices[index].LocalToWorld;
            float3 center = new float3(matrix.c3.x, matrix.c3.y, matrix.c3.z);
            float3 axisRaw = new float3(matrix.c2.x, matrix.c2.y, matrix.c2.z);
            float3 axis = LeviathanProceduralMath.NormalizeSafe(axisRaw, new float3(0f, 0f, 1f));
            float radius = math.max(0.01f, BodyRadius);
            float halfHeight = math.max(radius, LeviathanProceduralMath.ResolveLength(axisRaw) * 0.5f);
            LeviathanCapsuleColliderDTO collider = default;
            collider.Center = LeviathanProceduralMath.SanitizeFinite(center, float3.zero);
            collider.Radius = radius;
            collider.Axis = axis;
            collider.HalfHeight = halfHeight;
            collider.OwnerHash = OwnerHash;
            collider.Flags = 1u;
            collider.BoneIndex = index;
            collider.FrameIndex = FrameIndex;
            collider.AabbExtents = math.abs(axis) * halfHeight + new float3(radius);
            ColliderProxies[index] = collider;
        }
    }

    internal static class LeviathanProceduralMath
    {
        public static float Smooth01(float value)
        {
            float t = math.saturate(math.select(1f, value, math.isfinite(value)));
            return t * t * (3f - 2f * t);
        }

        public static float CheapSinSigned(float phase)
        {
            float wrapped = phase - math.floor(phase);
            float tri = 1f - math.abs(wrapped * 2f - 1f);
            return (tri * 2f - 1f) * (1f - 0.225f * tri * tri);
        }

        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        public static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        public static float ResolveLength(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? lengthSq * math.rsqrt(lengthSq)
                : LeviathanTerrainIkConstants.DefaultSegmentLength;
        }
    }
}
