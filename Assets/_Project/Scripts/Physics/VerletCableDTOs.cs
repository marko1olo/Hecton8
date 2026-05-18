using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    internal static class VerletCableLayout
    {
        public const int NodeStrideBytes = 32;
        public const int ConstraintStrideBytes = 16;
        public const int GpuSplinePointStrideBytes = 16;
        public const int GpuDrawParamsStrideBytes = 80;
        public const int CableSystemStrideBytes = 64;
        public const int TuningStrideBytes = 64;
        public const int MaterialStrideBytes = 64;
        public const int SdfSampleStrideBytes = 16;
        public const int MockSdfStrideBytes = 64;
        public const int MockWorldSamplerStrideBytes = 80;
        public const int MockWinchSignalStrideBytes = 32;
        public const int MockSubmarineAnchorStrideBytes = 32;
        public const int CableSnappedSignalStrideBytes = 48;
        public const int CableTensionForceStrideBytes = 32;
        public const int CableAabbStrideBytes = 32;
        public const int BlackBoxEntryStrideBytes = 64;
        public const int BlackBoxCapacity = 300;
        public const float MinConstraintLength = 0.0001f;
        public const float MinConstraintLengthSq = MinConstraintLength * MinConstraintLength;

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<VerletNodeDTO>() == NodeStrideBytes &&
                   UnsafeUtility.SizeOf<VerletConstraintDTO>() == ConstraintStrideBytes &&
                   UnsafeUtility.SizeOf<GpuCableSplinePointDTO>() == GpuSplinePointStrideBytes &&
                   UnsafeUtility.SizeOf<GpuCableDrawParamsDTO>() == GpuDrawParamsStrideBytes &&
                   UnsafeUtility.SizeOf<CableSystemDTO>() == CableSystemStrideBytes &&
                   UnsafeUtility.SizeOf<VerletCableTuningDTO>() == TuningStrideBytes &&
                   UnsafeUtility.SizeOf<CableMaterialDTO>() == MaterialStrideBytes &&
                   UnsafeUtility.SizeOf<SdfSampleDTO>() == SdfSampleStrideBytes &&
                   UnsafeUtility.SizeOf<MockSDFSampler>() == MockSdfStrideBytes &&
                   UnsafeUtility.SizeOf<MockWorldSampler>() == MockWorldSamplerStrideBytes &&
                   UnsafeUtility.SizeOf<MockWinchSignal>() == MockWinchSignalStrideBytes &&
                   UnsafeUtility.SizeOf<MockSubmarineAnchor>() == MockSubmarineAnchorStrideBytes &&
                   UnsafeUtility.SizeOf<CableSnappedSignal>() == CableSnappedSignalStrideBytes &&
                   UnsafeUtility.SizeOf<CableTensionForceDTO>() == CableTensionForceStrideBytes &&
                   UnsafeUtility.SizeOf<CableAabbDTO>() == CableAabbStrideBytes &&
                   UnsafeUtility.SizeOf<VerletCableBlackBoxEntry>() == BlackBoxEntryStrideBytes;
        }

        public static int ResolveIterationBudget(byte tier, int requested)
        {
            if (requested > 0)
                return math.clamp(requested, 1, 10);

            switch (tier)
            {
                case 0:
                case 1:
                    return 3;
                case 2:
                    return 5;
                case 3:
                    return 8;
                default:
                    return 10;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VerletNodeDTO
    {
        public float3 Position;
        public float InvMass;
        public float3 OldPosition;
        public float _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct VerletConstraintDTO
    {
        public int NodeA;
        public int NodeB;
        public float RestLength;
        public float Stiffness;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct GpuCableSplinePointDTO
    {
        public float3 Position;
        public float Tension01;
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct GpuCableDrawParamsDTO
    {
        public float4 Color;
        public float4 StressColor;
        public float4 Params0; // x=global stress, y=segment stress scale, z=point count, w=radius.
        public float4 Params1; // x=indirect mode, y=visual tier, z=crystal density, w=silt intensity.
        public float4 Params2; // x=visual clock, yzw reserved for visual-only overkill.
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct CableSystemDTO
    {
        public int NodeOffset;
        public int NodeCount;
        public int ConstraintOffset;
        public int ConstraintCount;
        public int ActiveNodeCount;
        public int MaterialIndex;
        public int Flags;
        public int CableId;
        public float NodeRadius;
        public float TargetLength;
        public float ReelingSpeedMetersPerSecond;
        public float MaxTension;
        public float3 LocalOrigin;
        public float HardwareTier;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VerletCableTuningDTO
    {
        public float3 Gravity;
        public float FluidFriction;
        public int ConstraintIterations;
        public float StretchThreshold01;
        public float BreakForce;
        public float RockFriction01;
        public float ReelSpeedMetersPerSecond;
        public float Reserved0;
        public float Reserved1;
        public float Reserved2;
        public float Reserved3;
        public float Reserved4;
        public float Reserved5;
        public float Reserved6;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct CableMaterialDTO
    {
        public uint MaterialHash;
        public float LinearDensity;
        public float YieldStretch01;
        public float SnapStretch01;
        public float4 SolverTuning;
        public float4 VisualTuning;
        public float4 LoadTuning;

        public static void GenerateEmergencyMockCables(NativeArray<CableMaterialDTO> materials)
        {
            if (!materials.IsCreated)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new CableMaterialDTO
                {
                    MaterialHash = i == 0 ? 0x5645524Cu : 0x5645524Cu + (uint)i,
                    LinearDensity = 1.45f,
                    YieldStretch01 = 0.18f,
                    SnapStretch01 = 0.38f,
                    SolverTuning = new float4(0.82f, 0.975f, 0.42f, 0.035f),
                    VisualTuning = new float4(0.045f, 0.35f, 0.22f, 0f),
                    LoadTuning = new float4(24f, 3f, 5f, 10f)
                };
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct SdfSampleDTO
    {
        public float3 Normal;
        public float Distance;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct MockSDFSampler
    {
        public float3 SphereCenter;
        public float SphereRadius;
        public float3 SecondarySphereCenter;
        public float SecondarySphereRadius;
        public float PlaneY;
        public float Padding0;
        public float Padding1;
        public float Padding2;
        public float Padding3;
        public float Padding4;
        public float Padding5;
        public float Padding6;

        public float SampleDistance(float3 position)
        {
            return Sample(position).Distance;
        }

        public SdfSampleDTO Sample(float3 position)
        {
            float planeDistance = position.y - PlaneY;
            float3 planeNormal = new float3(0f, 1f, 0f);

            float primaryDistance = SampleSphereDistance(position, SphereCenter, SphereRadius);
            float3 primaryNormal = SafeNormal(position - SphereCenter, planeNormal);

            float secondaryDistance = SampleSphereDistance(position, SecondarySphereCenter, SecondarySphereRadius);
            float3 secondaryNormal = SafeNormal(position - SecondarySphereCenter, planeNormal);

            float distance = planeDistance;
            float3 normal = planeNormal;
            if (primaryDistance < distance)
            {
                distance = primaryDistance;
                normal = primaryNormal;
            }

            if (secondaryDistance < distance)
            {
                distance = secondaryDistance;
                normal = secondaryNormal;
            }

            if (!math.isfinite(distance))
                distance = 1f;

            return new SdfSampleDTO
            {
                Distance = distance,
                Normal = SafeNormal(normal, planeNormal)
            };
        }

        private static float SampleSphereDistance(float3 position, float3 center, float radius)
        {
            float safeRadius = math.max(0f, math.isfinite(radius) ? radius : 0f);
            if (safeRadius <= 0f)
                return float.MaxValue;

            float3 delta = position - center;
            float lenSq = math.lengthsq(delta);
            if (!math.isfinite(lenSq))
                return float.MaxValue;

            return math.sqrt(math.max(lenSq, 0f)) - safeRadius;
        }

        internal static float3 SafeNormal(float3 vector, float3 fallback)
        {
            float lenSq = math.lengthsq(vector);
            if (!math.isfinite(lenSq) || lenSq <= 0.000001f)
                return math.all(math.isfinite(fallback)) ? fallback : new float3(0f, 1f, 0f);

            return vector * math.rsqrt(lenSq);
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public partial struct MockWorldSampler
    {
        public MockSDFSampler Sdf;
        public float3 FlowVelocity;
        public float FlowAccelerationScale;

        public float SampleDistance(float3 position)
        {
            return Sdf.SampleDistance(position);
        }

        public SdfSampleDTO Sample(float3 position)
        {
            return Sdf.Sample(position);
        }

        public float3 SampleFlowAcceleration(float3 position)
        {
            float phase = (position.x * 0.071f + position.z * 0.047f) * 0.159154943f;
            float wave = CheapTriangleWave01(phase);
            float scale = math.max(0f, math.isfinite(FlowAccelerationScale) ? FlowAccelerationScale : 0f);
            return FlowVelocity * (scale * (0.35f + wave * 0.65f));
        }

        private static float CheapTriangleWave01(float phase)
        {
            if (!math.isfinite(phase))
                return 0.5f;

            float wrapped = math.frac(phase);
            return 1f - math.abs(wrapped + wrapped - 1f);
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct MockWinchSignal
    {
        public int SystemIndex;
        public int Flags;
        public float DeltaMeters;
        public float SpeedMetersPerSecond;
        public float MinRestLength;
        public uint Sequence;
        public uint FrameIndex;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct MockSubmarineAnchor
    {
        public float3 Position;
        public uint EntityId;
        public float3 Velocity;
        public float InvMass;
    }

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct CableSnappedSignal
    {
        public float3 Position;
        public float PeakTension;
        public int CableId;
        public int ConstraintIndex;
        public uint FrameIndex;
        public byte Reason;
        public byte Flags;
        public ushort NodeCount;
        public float SnapThreshold;
        public float Severity01;
        public uint Reserved;
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct CableTensionForceDTO
    {
        public float3 Force;
        public int CableId;
        public float3 ApplicationPoint;
        public float Tension;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct CableAabbDTO
    {
        public float3 Min;
        public int Visible;
        public float3 Max;
        public int Dirty;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VerletCableBlackBoxEntry
    {
        public uint FrameIndex;
        public int CableId;
        public int ActiveNodeCount;
        public int ConstraintCount;
        public float3 FirstPosition;
        public float MaxTension;
        public float3 LastPosition;
        public float AverageError;
        public uint Flags;
        public uint StateHash;
        public uint Reserved0;
        public uint Reserved1;
    }

    public unsafe struct VerletCableNodeBuffer
    {
        public NativeArray<VerletNodeDTO> Nodes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref VerletNodeDTO GetNodeRef(int index)
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Nodes);
            return ref UnsafeUtility.AsRef<VerletNodeDTO>((byte*)basePtr + index * VerletCableLayout.NodeStrideBytes);
        }
    }

    public static class LocalShiftResolver
    {
        public static bool IsValidShift(float3 shift)
        {
            return math.all(math.isfinite(shift)) && math.lengthsq(shift) > 0.000001f;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletNodeIntegrationDTOJob : IJobParallelFor
    {
        private const byte PinnedMask = 1;

        public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly] public NativeArray<float3> PinnedPositions;
        [ReadOnly] public NativeArray<byte> PinnedState;
        public MockWorldSampler WorldSampler;
        public float3 ExternalAcceleration;
        public float DeltaTime;
        public float VelocityDamping;
        public float MaxVelocity;
        public float NodeRadius;
        public float RockFriction01;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Nodes.Length)
                return;

            VerletNodeDTO node = Nodes[index];
            bool pinned = node.InvMass <= 0f ||
                          (PinnedState.IsCreated && index < PinnedState.Length && (PinnedState[index] & PinnedMask) != 0);
            if (pinned)
            {
                float3 pinnedPosition = PinnedPositions.IsCreated && index < PinnedPositions.Length
                    ? Sanitize(PinnedPositions[index], node.Position)
                    : Sanitize(node.Position, float3.zero);
                node.Position = pinnedPosition;
                node.OldPosition = pinnedPosition;
                Nodes[index] = node;
                return;
            }

            float3 position = Sanitize(node.Position, float3.zero);
            float3 oldPosition = Sanitize(node.OldPosition, position);
            float3 velocity = (position - oldPosition) * SanitizeNonNegative(VelocityDamping, 0.98f);
            float velocityLengthSq = math.lengthsq(velocity);
            float maxVelocity = SanitizeNonNegative(MaxVelocity, 0f);
            if (maxVelocity > 0f && math.isfinite(velocityLengthSq) && velocityLengthSq > maxVelocity * maxVelocity)
                velocity *= maxVelocity * math.rsqrt(math.max(velocityLengthSq, 0.000001f));

            float safeDt = SanitizeNonNegative(DeltaTime, 0f);
            float3 acceleration = Sanitize(ExternalAcceleration, float3.zero) + WorldSampler.SampleFlowAcceleration(position);
            float3 next = position + velocity + acceleration * (safeDt * safeDt);
            next = Sanitize(next, position);

            float radius = math.max(0f, SanitizeNonNegative(NodeRadius, 0.035f));
            SdfSampleDTO sample = WorldSampler.Sample(next);
            if (sample.Distance < radius)
            {
                float3 normal = MockSDFSampler.SafeNormal(sample.Normal, new float3(0f, 1f, 0f));
                next += normal * (radius - sample.Distance);
                float3 impactVelocity = next - position;
                float3 normalVelocity = normal * math.dot(impactVelocity, normal);
                float3 tangentVelocity = impactVelocity - normalVelocity;
                float roughness = math.saturate(RockFriction01);
                float3 dampedTangent = tangentVelocity * (1f - roughness);
                oldPosition = next - dampedTangent;
            }
            else
            {
                oldPosition = position;
            }

            node.Position = Sanitize(next, position);
            node.OldPosition = Sanitize(oldPosition, position);
            node.InvMass = math.isfinite(node.InvMass) ? math.max(0f, node.InvMass) : 0f;
            node._pad0 = 0f;
            Nodes[index] = node;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static float3 Sanitize(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletConstraintRelaxationDTOJob : IJob
    {
        public NativeArray<VerletNodeDTO> Nodes;
        public NativeArray<VerletConstraintDTO> Constraints;
        public NativeArray<float> SegmentTensions;
        public NativeArray<float> SolverStats;
        public NativeArray<CableTensionForceDTO> TensionForces;
        public NativeArray<CableSnappedSignal> SnapSignals;
        public NativeArray<int> SnapSignalCount;
        public int IterationCount;
        public int ActiveConstraintCount;
        public int CableId;
        public uint FrameIndex;
        public float PlasticStretch01;
        public float PlasticCreep01;
        public float SnapStretch01;
        public float TensionScale;

        public void Execute()
        {
            int constraintCount = math.min(math.max(0, ActiveConstraintCount), Constraints.Length);
            int iterations = math.clamp(IterationCount, 1, 10);
            float peakTension = 0f;
            float maxError = 0f;
            float errorSum = 0f;
            int errorSamples = 0;
            int snappedCount = 0;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int constraintIndex = 0; constraintIndex < constraintCount; constraintIndex++)
                {
                    VerletConstraintDTO constraint = Constraints[constraintIndex];
                    if (constraint.Stiffness <= 0f ||
                        (uint)constraint.NodeA >= (uint)Nodes.Length ||
                        (uint)constraint.NodeB >= (uint)Nodes.Length)
                    {
                        WriteTension(constraintIndex, 0f);
                        continue;
                    }

                    VerletNodeDTO nodeA = Nodes[constraint.NodeA];
                    VerletNodeDTO nodeB = Nodes[constraint.NodeB];
                    float3 delta = nodeB.Position - nodeA.Position;
                    float lenSq = math.lengthsq(delta);
                    if (!math.isfinite(lenSq) || lenSq <= VerletCableLayout.MinConstraintLengthSq)
                    {
                        WriteTension(constraintIndex, 0f);
                        continue;
                    }

                    float distance = math.sqrt(lenSq);
                    float restLength = math.max(VerletCableLayout.MinConstraintLength, constraint.RestLength);
                    float error = distance - restLength;
                    float absError = math.abs(error);
                    maxError = math.max(maxError, absError);
                    errorSum += absError;
                    errorSamples++;

                    float stretch01 = math.max(0f, error) * math.rcp(math.max(restLength, VerletCableLayout.MinConstraintLength));
                    float stiffness = math.saturate(constraint.Stiffness);
                    float tension = math.max(0f, error) * stiffness * math.max(0f, TensionScale);
                    peakTension = math.max(peakTension, tension);
                    WriteTension(constraintIndex, tension);
                    WriteTensionForce(constraintIndex, delta * math.rsqrt(lenSq), tension, nodeA.Position);

                    if (SnapStretch01 > 0f && stretch01 >= SnapStretch01)
                    {
                        constraint.Stiffness = 0f;
                        Constraints[constraintIndex] = constraint;
                        snappedCount++;
                        WriteSnapSignal(constraintIndex, nodeA.Position, tension, stretch01);
                        continue;
                    }

                    if (PlasticStretch01 > 0f && stretch01 > PlasticStretch01)
                    {
                        float creep = math.saturate(PlasticCreep01);
                        constraint.RestLength = math.lerp(restLength, distance, creep);
                        Constraints[constraintIndex] = constraint;
                    }

                    float invMassA = math.max(0f, nodeA.InvMass);
                    float invMassB = math.max(0f, nodeB.InvMass);
                    float invMassSum = invMassA + invMassB;
                    if (invMassSum <= 0.000001f)
                        continue;

                    float3 direction = delta * math.rsqrt(lenSq);
                    float3 correction = direction * (error * stiffness);
                    if (invMassA > 0f)
                    {
                        nodeA.Position += correction * (invMassA * math.rcp(invMassSum));
                        Nodes[constraint.NodeA] = nodeA;
                    }

                    if (invMassB > 0f)
                    {
                        nodeB.Position -= correction * (invMassB * math.rcp(invMassSum));
                        Nodes[constraint.NodeB] = nodeB;
                    }
                }
            }

            if (SolverStats.IsCreated)
            {
                if (SolverStats.Length > 0)
                    SolverStats[0] = peakTension;
                if (SolverStats.Length > 1)
                    SolverStats[1] = errorSamples > 0 ? errorSum * math.rcp(errorSamples) : 0f;
                if (SolverStats.Length > 2)
                    SolverStats[2] = maxError;
                if (SolverStats.Length > 3)
                    SolverStats[3] = snappedCount;
            }
        }

        private void WriteTension(int index, float tension)
        {
            if (SegmentTensions.IsCreated && index >= 0 && index < SegmentTensions.Length)
                SegmentTensions[index] = math.isfinite(tension) ? math.max(0f, tension) : 0f;
        }

        private void WriteTensionForce(int index, float3 direction, float tension, float3 applicationPoint)
        {
            if (!TensionForces.IsCreated || index < 0 || index >= TensionForces.Length)
                return;

            float safeTension = math.isfinite(tension) ? math.max(0f, tension) : 0f;
            TensionForces[index] = new CableTensionForceDTO
            {
                Force = direction * safeTension,
                ApplicationPoint = applicationPoint,
                Tension = safeTension,
                CableId = CableId
            };
        }

        private void WriteSnapSignal(int constraintIndex, float3 position, float tension, float stretch01)
        {
            if (!SnapSignals.IsCreated || !SnapSignalCount.IsCreated || SnapSignalCount.Length == 0 || SnapSignals.Length == 0)
                return;

            int writeIndex = SnapSignalCount[0];
            if ((uint)writeIndex >= (uint)SnapSignals.Length)
                return;

            SnapSignals[writeIndex] = new CableSnappedSignal
            {
                Position = math.all(math.isfinite(position)) ? position : float3.zero,
                PeakTension = math.isfinite(tension) ? tension : 0f,
                CableId = CableId,
                ConstraintIndex = constraintIndex,
                FrameIndex = FrameIndex,
                Reason = 1,
                Flags = 0,
                NodeCount = (ushort)math.min(Nodes.Length, ushort.MaxValue),
                SnapThreshold = math.max(0f, SnapStretch01),
                Severity01 = math.saturate(stretch01 * math.rcp(math.max(SnapStretch01, 0.0001f))),
                Reserved = 0u
            };
            SnapSignalCount[0] = writeIndex + 1;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletWinchReelDTOJob : IJob
    {
        public NativeArray<CableSystemDTO> Systems;
        public NativeArray<VerletConstraintDTO> Constraints;
        [ReadOnly] public NativeArray<MockWinchSignal> WinchSignals;
        public int SystemIndex;
        public int WinchSignalIndex;
        public float DeltaTime;
        public float MinRestLength;

        public void Execute()
        {
            if ((uint)SystemIndex >= (uint)Systems.Length)
                return;

            CableSystemDTO system = Systems[SystemIndex];
            int constraintCount = math.clamp(system.ConstraintCount, 0, Constraints.Length - system.ConstraintOffset);
            if (constraintCount <= 0)
                return;

            float shrink = math.max(0f, system.ReelingSpeedMetersPerSecond) * math.max(0f, DeltaTime);
            float minRestLength = math.max(VerletCableLayout.MinConstraintLength, MinRestLength);
            if (WinchSignals.IsCreated && (uint)WinchSignalIndex < (uint)WinchSignals.Length)
            {
                MockWinchSignal signal = WinchSignals[WinchSignalIndex];
                if (signal.SystemIndex == SystemIndex || signal.SystemIndex < 0)
                {
                    shrink += math.max(0f, signal.SpeedMetersPerSecond) * math.max(0f, DeltaTime);
                    if (math.isfinite(signal.MinRestLength) && signal.MinRestLength > 0f)
                        minRestLength = math.max(VerletCableLayout.MinConstraintLength, signal.MinRestLength);
                    if (math.isfinite(signal.DeltaMeters))
                        shrink += math.max(0f, -signal.DeltaMeters);
                }
            }

            if (shrink <= 0f)
                return;

            float perConstraintShrink = shrink * math.rcp(constraintCount);
            for (int i = 0; i < constraintCount; i++)
            {
                int constraintIndex = system.ConstraintOffset + i;
                VerletConstraintDTO constraint = Constraints[constraintIndex];
                constraint.RestLength = math.max(minRestLength, constraint.RestLength - perConstraintShrink);
                Constraints[constraintIndex] = constraint;
            }

            int lastConstraint = system.ConstraintOffset + constraintCount - 1;
            if (constraintCount > 1 && Constraints[lastConstraint].RestLength <= minRestLength + 0.0001f)
            {
                system.ActiveNodeCount = math.max(2, system.ActiveNodeCount - 1);
                system.ConstraintCount = math.max(1, system.ActiveNodeCount - 1);
                Constraints[lastConstraint] = default;
            }

            Systems[SystemIndex] = system;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletCableOriginShiftDTOJob : IJobParallelFor
    {
        public NativeArray<VerletNodeDTO> Nodes;
        public float3 ShiftOffset;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Nodes.Length)
                return;

            if (!LocalShiftResolver.IsValidShift(ShiftOffset))
                return;

            VerletNodeDTO node = Nodes[index];
            node.Position -= ShiftOffset;
            node.OldPosition -= ShiftOffset;
            Nodes[index] = node;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletGpuSplineCopyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly] public NativeArray<float> SegmentTensions;
        public NativeArray<GpuCableSplinePointDTO> GpuPoints;
        public float3 Origin;
        public float InvSnapTension;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Nodes.Length || (uint)index >= (uint)GpuPoints.Length)
                return;

            float tension = 0f;
            if (SegmentTensions.IsCreated && SegmentTensions.Length > 0)
                tension = SegmentTensions[math.min(index, SegmentTensions.Length - 1)];

            GpuPoints[index] = new GpuCableSplinePointDTO
            {
                Position = Nodes[index].Position + Origin,
                Tension01 = math.saturate(tension * math.max(0f, InvSnapTension))
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletAabbFrustumCullJob : IJob
    {
        [ReadOnly] public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly] public NativeArray<float4> FrustumPlanes;
        public NativeArray<CableAabbDTO> Aabbs;
        public int AabbIndex;
        public float3 Origin;
        public float Radius;

        public void Execute()
        {
            if (!Nodes.IsCreated || Nodes.Length == 0 || !Aabbs.IsCreated || (uint)AabbIndex >= (uint)Aabbs.Length)
                return;

            float3 first = Nodes[0].Position + Origin;
            float3 minPoint = first;
            float3 maxPoint = first;
            for (int i = 1; i < Nodes.Length; i++)
            {
                float3 point = Nodes[i].Position + Origin;
                minPoint = math.min(minPoint, point);
                maxPoint = math.max(maxPoint, point);
            }

            float radius = math.max(0f, Radius);
            minPoint -= radius;
            maxPoint += radius;

            int visible = 1;
            int planeCount = FrustumPlanes.IsCreated ? math.min(6, FrustumPlanes.Length) : 0;
            for (int i = 0; i < planeCount; i++)
            {
                float4 plane = FrustumPlanes[i];
                float3 positive = new float3(
                    plane.x >= 0f ? maxPoint.x : minPoint.x,
                    plane.y >= 0f ? maxPoint.y : minPoint.y,
                    plane.z >= 0f ? maxPoint.z : minPoint.z);
                if (math.dot(plane.xyz, positive) + plane.w < 0f)
                {
                    visible = 0;
                    break;
                }
            }

            Aabbs[AabbIndex] = new CableAabbDTO
            {
                Min = minPoint,
                Max = maxPoint,
                Visible = visible,
                Dirty = 1
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletBlackBoxWriteJob : IJob
    {
        [ReadOnly] public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly] public NativeArray<float> SolverStats;
        public NativeArray<VerletCableBlackBoxEntry> Ring;
        public NativeArray<int> Head;
        public int CableId;
        public int ActiveNodeCount;
        public int ConstraintCount;
        public uint FrameIndex;
        public uint Flags;

        public void Execute()
        {
            if (!Ring.IsCreated || Ring.Length == 0 || !Head.IsCreated || Head.Length == 0)
                return;

            int capacity = math.min(VerletCableLayout.BlackBoxCapacity, Ring.Length);
            int head = Head[0];
            if ((uint)head >= (uint)capacity)
                head = 0;

            int activeCount = math.clamp(ActiveNodeCount, 0, Nodes.IsCreated ? Nodes.Length : 0);
            float3 first = activeCount > 0 ? Nodes[0].Position : float3.zero;
            float3 last = activeCount > 0 ? Nodes[activeCount - 1].Position : float3.zero;
            uint hash = 2166136261u;
            for (int i = 0; i < activeCount; i++)
            {
                float3 point = Nodes[i].Position;
                hash = (hash ^ math.asuint(point.x)) * 16777619u;
                hash = (hash ^ math.asuint(point.y)) * 16777619u;
                hash = (hash ^ math.asuint(point.z)) * 16777619u;
            }

            Ring[head] = new VerletCableBlackBoxEntry
            {
                FrameIndex = FrameIndex,
                CableId = CableId,
                ActiveNodeCount = activeCount,
                ConstraintCount = ConstraintCount,
                FirstPosition = first,
                LastPosition = last,
                MaxTension = SolverStats.IsCreated && SolverStats.Length > 0 ? SolverStats[0] : 0f,
                AverageError = SolverStats.IsCreated && SolverStats.Length > 1 ? SolverStats[1] : 0f,
                Flags = Flags,
                StateHash = hash,
                Reserved0 = 0u,
                Reserved1 = 0u
            };
            Head[0] = (head + 1) % capacity;
        }
    }

    public static class CableMaterialCsvParser
    {
        private const uint DefaultHash = 0x5645524Cu;

        public static int Parse(ReadOnlySpan<char> csv, NativeArray<CableMaterialDTO> output)
        {
            if (!output.IsCreated || output.Length == 0 || csv.Length == 0)
                return 0;

            int parsed = 0;
            int cursor = 0;
            while (cursor < csv.Length && parsed < output.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != '\n' && csv[cursor] != '\r')
                    cursor++;

                ReadOnlySpan<char> line = csv.Slice(lineStart, cursor - lineStart);
                if (TryParseLine(line, parsed, out CableMaterialDTO material))
                {
                    output[parsed] = material;
                    parsed++;
                }

                while (cursor < csv.Length && (csv[cursor] == '\n' || csv[cursor] == '\r'))
                    cursor++;
            }

            return parsed;
        }

        private static bool TryParseLine(ReadOnlySpan<char> line, int rowIndex, out CableMaterialDTO material)
        {
            material = default;
            line = Trim(line);
            if (line.Length == 0 || line[0] == '#')
                return false;

            uint materialHash = DefaultHash + (uint)rowIndex;
            bool hasKey = false;
            float density = 1.45f;
            float yield = 0.18f;
            float snap = 0.38f;
            float stiffness = 0.82f;
            float damping = 0.975f;
            float friction = 0.42f;
            float radius = 0.035f;

            ReadOnlySpan<char> field;
            int fieldIndex = 0;
            int cursor = 0;
            while (cursor <= line.Length)
            {
                int start = cursor;
                while (cursor < line.Length && line[cursor] != ',')
                    cursor++;

                field = Trim(line.Slice(start, cursor - start));
                if (fieldIndex == 0 && field.Length > 0 && !TryParseFloat(field, out _))
                {
                    if (StartsWithAlpha(field))
                    {
                        materialHash = HashKey(field);
                        hasKey = true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (field.Length > 0 && TryParseFloat(field, out float value))
                {
                    int numericIndex = hasKey ? fieldIndex - 1 : fieldIndex;
                    switch (numericIndex)
                    {
                        case 0:
                            density = value;
                            break;
                        case 1:
                            yield = value;
                            break;
                        case 2:
                            snap = value;
                            break;
                        case 3:
                            stiffness = value;
                            break;
                        case 4:
                            damping = value;
                            break;
                        case 5:
                            friction = value;
                            break;
                        case 6:
                            radius = value;
                            break;
                    }
                }
                else if (field.Length > 0 && hasKey && fieldIndex == 1 && StartsWithAlpha(field))
                {
                    return false;
                }

                fieldIndex++;
                cursor++;
                if (cursor > line.Length)
                    break;
            }

            material = new CableMaterialDTO
            {
                MaterialHash = materialHash,
                LinearDensity = math.max(0.001f, density),
                YieldStretch01 = math.max(0f, yield),
                SnapStretch01 = math.max(yield + 0.01f, snap),
                SolverTuning = new float4(math.saturate(stiffness), math.saturate(damping), math.saturate(friction), math.max(0.001f, radius)),
                VisualTuning = new float4(math.max(0.001f, radius), 0.35f, 0.22f, 0f),
                LoadTuning = new float4(24f, 3f, 5f, 10f)
            };
            return true;
        }

        private static bool StartsWithAlpha(ReadOnlySpan<char> text)
        {
            if (text.Length == 0)
                return false;

            char c = text[0];
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static uint HashKey(ReadOnlySpan<char> text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? DefaultHash : hash;
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start < text.Length && IsWhite(text[start]))
                start++;
            while (end >= start && IsWhite(text[end]))
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool IsWhite(char c)
        {
            return c == ' ' || c == '\t';
        }

        private static bool TryParseFloat(ReadOnlySpan<char> text, out float value)
        {
            value = 0f;
            if (text.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (text[index] == '-')
            {
                sign = -1f;
                index++;
            }
            else if (text[index] == '+')
            {
                index++;
            }

            float integer = 0f;
            bool any = false;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9')
            {
                integer = integer * 10f + (text[index] - '0');
                index++;
                any = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < text.Length && text[index] == '.')
            {
                index++;
                while (index < text.Length && text[index] >= '0' && text[index] <= '9')
                {
                    fraction = fraction * 10f + (text[index] - '0');
                    divisor *= 10f;
                    index++;
                    any = true;
                }
            }

            if (!any || index != text.Length)
                return false;

            value = sign * (integer + fraction / divisor);
            return math.isfinite(value);
        }
    }
}
