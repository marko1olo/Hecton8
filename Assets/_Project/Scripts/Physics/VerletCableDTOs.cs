using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public static class VerletCableLayout
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
        public const int CableSnappedSignalStrideBytes = 64;
        public const int CableTensionForceStrideBytes = 32;
        public const int CableAabbStrideBytes = 32;
        public const int BlackBoxEntryStrideBytes = 64;
        public const int TetherAupNodeStrideBytes = 64;
        public const int CableNodeStrideBytes = 64;
        public const int TetherAupConstraintStrideBytes = 32;
        public const int TetherAupEndpointStrideBytes = 64;
        public const int TetherAupForcePacketStrideBytes = 64;
        public const int TetherSplineVertexStrideBytes = 32;
        public const int TetherSplineIndirectArgsStrideBytes = 16;
        public const int TetherAupTelemetryStrideBytes = 64;
        public const int TetherTelemetryStrideBytes = 64;
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
                   UnsafeUtility.SizeOf<VerletCableBlackBoxEntry>() == BlackBoxEntryStrideBytes &&
                   ValidateTetherAupLayouts() &&
                   ValidateTetherVerletTelemetryLayout();
        }

        public static bool ValidateTetherAupLayouts()
        {
            return UnsafeUtility.SizeOf<TetherNodeDTO>() == TetherAupNodeStrideBytes &&
                   UnsafeUtility.SizeOf<CableNodeDTO>() == CableNodeStrideBytes &&
                   OffsetOf<TetherNodeDTO>(nameof(TetherNodeDTO.CurrentAUP)) == 0 &&
                   OffsetOf<TetherNodeDTO>(nameof(TetherNodeDTO.PreviousAUP)) == 24 &&
                   OffsetOf<TetherNodeDTO>(nameof(TetherNodeDTO.InverseMass)) == 48 &&
                   OffsetOf<TetherNodeDTO>(nameof(TetherNodeDTO.Flags)) == 52 &&
                   OffsetOf<TetherNodeDTO>("_pad0") == 56 &&
                   OffsetOf<CableNodeDTO>(nameof(CableNodeDTO.CurrentAUP)) == 0 &&
                   OffsetOf<CableNodeDTO>(nameof(CableNodeDTO.PreviousAUP)) == 24 &&
                   OffsetOf<CableNodeDTO>(nameof(CableNodeDTO.InverseMass)) == 48 &&
                   OffsetOf<CableNodeDTO>(nameof(CableNodeDTO.Flags)) == 52 &&
                   OffsetOf<CableNodeDTO>("_pad0") == 56 &&
                   OffsetOf<CableNodeDTO>("_pad7") == 63 &&
                   UnsafeUtility.SizeOf<TetherConstraintDTO>() == TetherAupConstraintStrideBytes &&
                   UnsafeUtility.SizeOf<TetherEndpointAupDTO>() == TetherAupEndpointStrideBytes &&
                   UnsafeUtility.SizeOf<TetherForcePacketDTO>() == TetherAupForcePacketStrideBytes &&
                   OffsetOf<TetherForcePacketDTO>(nameof(TetherForcePacketDTO.ApplicationAUP)) == 0 &&
                   OffsetOf<TetherForcePacketDTO>(nameof(TetherForcePacketDTO.Force)) == 24 &&
                   OffsetOf<TetherForcePacketDTO>(nameof(TetherForcePacketDTO.Tension)) == 36 &&
                   OffsetOf<TetherForcePacketDTO>(nameof(TetherForcePacketDTO.CableId)) == 40 &&
                   OffsetOf<TetherForcePacketDTO>(nameof(TetherForcePacketDTO.BodySlot)) == 44 &&
                   OffsetOf<TetherForcePacketDTO>(nameof(TetherForcePacketDTO.Flags)) == 48 &&
                   OffsetOf<TetherForcePacketDTO>(nameof(TetherForcePacketDTO.FrameIndex)) == 52 &&
                   OffsetOf<TetherForcePacketDTO>("_pad0") == 56 &&
                   UnsafeUtility.SizeOf<TetherSplineVertexDTO>() == TetherSplineVertexStrideBytes &&
                   OffsetOf<TetherSplineVertexDTO>(nameof(TetherSplineVertexDTO.Position)) == 0 &&
                   OffsetOf<TetherSplineVertexDTO>(nameof(TetherSplineVertexDTO.U)) == 12 &&
                   OffsetOf<TetherSplineVertexDTO>(nameof(TetherSplineVertexDTO.Tangent)) == 16 &&
                   OffsetOf<TetherSplineVertexDTO>(nameof(TetherSplineVertexDTO.Tension01)) == 28 &&
                   UnsafeUtility.SizeOf<TetherSplineIndirectArgsDTO>() == TetherSplineIndirectArgsStrideBytes &&
                   UnsafeUtility.SizeOf<TetherAupTelemetryEntry>() == TetherAupTelemetryStrideBytes &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.AnchorAUP)) == 0 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.FrameIndex)) == 24 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.NodeCount)) == 28 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.IterationCount)) == 32 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.MaxTension)) == 36 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.StateHash)) == 40 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.Flags)) == 44 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.CpuMicroseconds)) == 48 &&
                   OffsetOf<TetherAupTelemetryEntry>(nameof(TetherAupTelemetryEntry.GlobalQualityWeight)) == 52 &&
                   OffsetOf<TetherAupTelemetryEntry>("_pad0") == 56 &&
                   UnsafeUtility.SizeOf<TetherTelemetryEntry>() == TetherTelemetryStrideBytes &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.AnchorAUP)) == 0 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.FrameIndex)) == 24 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.NodeCount)) == 28 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.IterationCount)) == 32 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.MaxTension)) == 36 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.StateHash)) == 40 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.Flags)) == 44 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.CpuMicroseconds)) == 48 &&
                   OffsetOf<TetherTelemetryEntry>(nameof(TetherTelemetryEntry.GlobalQualityWeight)) == 52 &&
                   OffsetOf<TetherTelemetryEntry>("_pad0") == 56;
        }

        public static bool ValidateTetherVerletTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<TetherVerletTelemetryEntry>() == TetherVerletJobLayout.TelemetryEntryStrideBytes &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.NodeCount)) == 4 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.IterationCount)) == 8 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.PeakConstraintDelta)) == 12 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.PeakCableTension)) == 16 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.AnchorPosition)) == 20 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.PayloadPosition)) == 32 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.Flags)) == 44 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.BufferId)) == 48 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.Generation)) == 52 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.FailureCode)) == 56 &&
                   OffsetOf<TetherVerletTelemetryEntry>(nameof(TetherVerletTelemetryEntry.Reserved0)) == 60;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        public static int ResolveIterationBudget(byte tier, int requested)
        {
            float quality = math.saturate(tier * 0.33333334f);
            return ResolveIterationBudget(quality, requested);
        }

        public static int ResolveIterationBudget(float globalQualityWeight, int requested)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float smooth = q * q * (3f - 2f * q);
            int ceiling = requested > 0 ? math.clamp(requested, 1, 10) : 10;
            if (ceiling <= 3)
                return ceiling;

            return math.clamp((int)math.round(math.lerp(3f, ceiling, smooth)), 3, ceiling);
        }
    }

    public static class VerletCableSimdMath
    {
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const float HalfPi = 1.57079632679f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthFromSq(float lengthSq)
        {
            float finiteSq = math.select(0f, lengthSq, math.isfinite(lengthSq));
            float safeSq = math.max(finiteSq, 0.0001f);
            return math.select(0f, safeSq * math.rsqrt(math.max(safeSq, 0.0001f)), finiteSq > 0.0001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinPolynomial7(float radians)
        {
            float safeRadians = math.select(0f, radians, math.isfinite(radians));
            float x = safeRadians - math.floor((safeRadians + math.PI) * InvTwoPi) * TwoPi;
            x = math.select(x, math.PI - x, x > HalfPi);
            x = math.select(x, -math.PI - x, x < -HalfPi);
            float x2 = x * x;
            return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.00019840874f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosPolynomial7(float radians)
        {
            return SinPolynomial7(radians + HalfPi);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VerletNodeDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float InvMass;
        [FieldOffset(16)] public float3 OldPosition;
        [FieldOffset(28)] public float _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherNodeDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public double3 PreviousAUP;
        [FieldOffset(48)] public float InverseMass;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CableNodeDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public double3 PreviousAUP;
        [FieldOffset(48)] public float InverseMass;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public byte _pad0;
        [FieldOffset(57)] public byte _pad1;
        [FieldOffset(58)] public byte _pad2;
        [FieldOffset(59)] public byte _pad3;
        [FieldOffset(60)] public byte _pad4;
        [FieldOffset(61)] public byte _pad5;
        [FieldOffset(62)] public byte _pad6;
        [FieldOffset(63)] public byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TetherConstraintDTO
    {
        [FieldOffset(0)] public int NodeA;
        [FieldOffset(4)] public int NodeB;
        [FieldOffset(8)] public float RestLength;
        [FieldOffset(12)] public float Stiffness;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint CableId;
        [FieldOffset(24)] private byte _pad0;
        [FieldOffset(25)] private byte _pad1;
        [FieldOffset(26)] private byte _pad2;
        [FieldOffset(27)] private byte _pad3;
        [FieldOffset(28)] private byte _pad4;
        [FieldOffset(29)] private byte _pad5;
        [FieldOffset(30)] private byte _pad6;
        [FieldOffset(31)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherEndpointAupDTO
    {
        [FieldOffset(0)] public double3 AnchorAUP;
        [FieldOffset(24)] public double3 PayloadAUP;
        [FieldOffset(48)] public float3 AbyssalCurrentAcceleration;
        [FieldOffset(60)] public float GlobalQualityWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherForcePacketDTO
    {
        [FieldOffset(0)] public double3 ApplicationAUP;
        [FieldOffset(24)] public float3 Force;
        [FieldOffset(36)] public float Tension;
        [FieldOffset(40)] public int CableId;
        [FieldOffset(44)] public int BodySlot;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint FrameIndex;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TetherSplineVertexDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float U;
        [FieldOffset(16)] public float3 Tangent;
        [FieldOffset(28)] public float Tension01;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TetherSplineIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherAupTelemetryEntry
    {
        [FieldOffset(0)] public double3 AnchorAUP;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public int NodeCount;
        [FieldOffset(32)] public int IterationCount;
        [FieldOffset(36)] public float MaxTension;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float CpuMicroseconds;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherTelemetryEntry
    {
        [FieldOffset(0)] public double3 AnchorAUP;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public int NodeCount;
        [FieldOffset(32)] public int IterationCount;
        [FieldOffset(36)] public float MaxTension;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float CpuMicroseconds;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VerletConstraintDTO
    {
        [FieldOffset(0)] public int NodeA;
        [FieldOffset(4)] public int NodeB;
        [FieldOffset(8)] public float RestLength;
        [FieldOffset(12)] public float Stiffness;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CableSystemDTO
    {
        [FieldOffset(0)] public int NodeOffset;
        [FieldOffset(4)] public int NodeCount;
        [FieldOffset(8)] public int ConstraintOffset;
        [FieldOffset(12)] public int ConstraintCount;
        [FieldOffset(16)] public int ActiveNodeCount;
        [FieldOffset(20)] public int MaterialIndex;
        [FieldOffset(24)] public int Flags;
        [FieldOffset(28)] public int CableId;
        [FieldOffset(32)] public float NodeRadius;
        [FieldOffset(36)] public float TargetLength;
        [FieldOffset(40)] public float ReelingSpeedMetersPerSecond;
        [FieldOffset(44)] public float MaxTension;
        [FieldOffset(48)] public float3 LocalOrigin;
        [FieldOffset(60)] public float VisualQualityWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VerletCableTuningDTO
    {
        [FieldOffset(0)] public float3 Gravity;
        [FieldOffset(12)] public float FluidFriction;
        [FieldOffset(16)] public int ConstraintIterations;
        [FieldOffset(20)] public float StretchThreshold01;
        [FieldOffset(24)] public float BreakForce;
        [FieldOffset(28)] public float RockFriction01;
        [FieldOffset(32)] public float ReelSpeedMetersPerSecond;
        [FieldOffset(36)] public float Reserved0;
        [FieldOffset(40)] public float Reserved1;
        [FieldOffset(44)] public float Reserved2;
        [FieldOffset(48)] public float Reserved3;
        [FieldOffset(52)] public float Reserved4;
        [FieldOffset(56)] public float Reserved5;
        [FieldOffset(60)] public float Reserved6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CableMaterialDTO
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float LinearDensity;
        [FieldOffset(8)] public float YieldStretch01;
        [FieldOffset(12)] public float SnapStretch01;
        [FieldOffset(16)] public float4 SolverTuning;
        [FieldOffset(32)] public float4 VisualTuning;
        [FieldOffset(48)] public float4 LoadTuning;

        public static void GenerateEmergencyMockCables(NativeArray<CableMaterialDTO> materials)
        {
            if (!materials.IsCreated)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                CableMaterialDTO material = default;
                material.MaterialHash = i == 0 ? 0x5645524Cu : 0x5645524Cu + (uint)i;
                material.LinearDensity = 1.45f;
                material.YieldStretch01 = 0.18f;
                material.SnapStretch01 = 0.38f;
                material.SolverTuning = math.float4(0.82f, 0.975f, 0.42f, 0.035f);
                material.VisualTuning = math.float4(0.045f, 0.35f, 0.22f, 0f);
                material.LoadTuning = math.float4(24f, 3f, 5f, 10f);
                materials[i] = material;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SdfSampleDTO
    {
        [FieldOffset(0)] public float3 Normal;
        [FieldOffset(12)] public float Distance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockSDFSampler
    {
        [FieldOffset(0)] public float3 SphereCenter;
        [FieldOffset(12)] public float SphereRadius;
        [FieldOffset(16)] public float3 SecondarySphereCenter;
        [FieldOffset(28)] public float SecondarySphereRadius;
        [FieldOffset(32)] public float PlaneY;
        [FieldOffset(36)] public float Padding0;
        [FieldOffset(40)] public float Padding1;
        [FieldOffset(44)] public float Padding2;
        [FieldOffset(48)] public float Padding3;
        [FieldOffset(52)] public float Padding4;
        [FieldOffset(56)] public float Padding5;
        [FieldOffset(60)] public float Padding6;

        public float SampleDistance(float3 position)
        {
            return Sample(position).Distance;
        }

        public SdfSampleDTO Sample(float3 position)
        {
            float planeDistance = position.y - PlaneY;
            float3 planeNormal = DefaultUp();

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

            SdfSampleDTO sample = default;
            sample.Distance = distance;
            sample.Normal = SafeNormal(normal, planeNormal);
            return sample;
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

            return VerletCableSimdMath.LengthFromSq(lenSq) - safeRadius;
        }

        internal static float3 SafeNormal(float3 vector, float3 fallback)
        {
            float lenSq = math.lengthsq(vector);
            if (!math.isfinite(lenSq) || lenSq <= 0.000001f)
                return math.all(math.isfinite(fallback)) ? fallback : DefaultUp();

            return vector * math.rsqrt(math.max(lenSq, 0.000001f));
        }

        internal static float3 DefaultUp()
        {
            float3 up = default;
            up.y = 1f;
            return up;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public partial struct MockWorldSampler
    {
        [FieldOffset(0)] public MockSDFSampler Sdf;
        [FieldOffset(64)] public float3 FlowVelocity;
        [FieldOffset(76)] public float FlowAccelerationScale;

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
            float3 flowVelocity = math.select(float3.zero, FlowVelocity, math.isfinite(FlowVelocity));
            return flowVelocity * (scale * (0.35f + wave * 0.65f));
        }

        private static float CheapTriangleWave01(float phase)
        {
            if (!math.isfinite(phase))
                return 0.5f;

            float wrapped = math.frac(phase);
            return 1f - math.abs(wrapped + wrapped - 1f);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockWinchSignal
    {
        [FieldOffset(0)] public int SystemIndex;
        [FieldOffset(4)] public int Flags;
        [FieldOffset(8)] public float DeltaMeters;
        [FieldOffset(12)] public float SpeedMetersPerSecond;
        [FieldOffset(16)] public float MinRestLength;
        [FieldOffset(20)] public uint Sequence;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockSubmarineAnchor
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public uint EntityId;
        [FieldOffset(16)] public float3 Velocity;
        [FieldOffset(28)] public float InvMass;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CableSnappedSignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float PeakTension;
        [FieldOffset(16)] public int CableId;
        [FieldOffset(20)] public int ConstraintIndex;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public float SnapThreshold;
        [FieldOffset(32)] public float Severity01;
        [FieldOffset(36)] public uint Reserved;
        [FieldOffset(40)] public uint Reserved1;
        [FieldOffset(44)] public uint Reserved2;
        [FieldOffset(48)] public uint Reserved3;
        [FieldOffset(52)] public uint Reserved4;
        [FieldOffset(56)] public uint Reserved5;
        [FieldOffset(60)] public ushort NodeCount;
        [FieldOffset(62)] public byte Reason;
        [FieldOffset(63)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CableTensionForceDTO
    {
        [FieldOffset(0)] public float3 Force;
        [FieldOffset(12)] public int CableId;
        [FieldOffset(16)] public float3 ApplicationPoint;
        [FieldOffset(28)] public float Tension;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CableAabbDTO
    {
        [FieldOffset(0)] public float3 Min;
        [FieldOffset(12)] public int Visible;
        [FieldOffset(16)] public float3 Max;
        [FieldOffset(28)] public int Dirty;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VerletCableBlackBoxEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int CableId;
        [FieldOffset(8)] public int ActiveNodeCount;
        [FieldOffset(12)] public int ConstraintCount;
        [FieldOffset(16)] public float3 FirstPosition;
        [FieldOffset(28)] public float MaxTension;
        [FieldOffset(32)] public float3 LastPosition;
        [FieldOffset(44)] public float AverageError;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    public unsafe ref struct VerletCableNodeBuffer
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletNodeIntegrationDTOJob : IJobParallelFor
    {
        private const byte PinnedMask = 1;

        [NoAlias] public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<float3> PinnedPositions;
        [ReadOnly, NoAlias] public NativeArray<byte> PinnedState;
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
            float3 acceleration = Sanitize(
                Sanitize(ExternalAcceleration, float3.zero) + WorldSampler.SampleFlowAcceleration(position),
                float3.zero);
            float3 next = position + velocity + acceleration * (safeDt * safeDt);
            next = Sanitize(next, position);

            float radius = math.max(0f, SanitizeNonNegative(NodeRadius, 0.035f));
            SdfSampleDTO sample = WorldSampler.Sample(next);
            if (sample.Distance < radius)
            {
                float3 normal = MockSDFSampler.SafeNormal(sample.Normal, MockSDFSampler.DefaultUp());
                next += normal * (radius - sample.Distance);
                float3 impactVelocity = next - position;
                float3 normalVelocity = normal * math.dot(impactVelocity, normal);
                float3 tangentVelocity = impactVelocity - normalVelocity;
                float roughness = math.saturate(SanitizeNonNegative(RockFriction01, 0f));
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletConstraintRelaxationDTOJob : IJob
    {
        [NoAlias] public NativeArray<VerletNodeDTO> Nodes;
        [NoAlias] public NativeArray<VerletConstraintDTO> Constraints;
        [WriteOnly, NoAlias] public NativeArray<float> SegmentTensions;
        [WriteOnly, NoAlias] public NativeArray<float> SolverStats;
        [WriteOnly, NoAlias] public NativeArray<CableTensionForceDTO> TensionForces;
        [WriteOnly, NoAlias] public NativeArray<CableSnappedSignal> SnapSignals;
        [NoAlias] public NativeArray<int> SnapSignalCount;
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
            if (!Nodes.IsCreated || !Constraints.IsCreated)
            {
                WriteSolverStats(0f, 0f, 0f, 0);
                return;
            }

            int constraintCount = math.min(math.max(0, ActiveConstraintCount), Constraints.Length);
            int iterations = math.clamp(IterationCount, 1, 10);
            float plasticStretch = SanitizeNonNegative(PlasticStretch01);
            float plasticCreep = math.saturate(SanitizeNonNegative(PlasticCreep01));
            float snapStretch = SanitizeNonNegative(SnapStretch01);
            float tensionScale = SanitizeNonNegative(TensionScale);
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
                    float stiffness = math.saturate(SanitizeNonNegative(constraint.Stiffness));
                    if (stiffness <= 0f ||
                        (uint)constraint.NodeA >= (uint)Nodes.Length ||
                        (uint)constraint.NodeB >= (uint)Nodes.Length)
                    {
                        WriteTension(constraintIndex, 0f);
                        continue;
                    }

                    VerletNodeDTO nodeA = Nodes[constraint.NodeA];
                    VerletNodeDTO nodeB = Nodes[constraint.NodeB];
                    float3 nodeAPosition = SanitizeFloat3(nodeA.Position, float3.zero);
                    float3 nodeBPosition = SanitizeFloat3(nodeB.Position, float3.zero);
                    if (!math.all(math.isfinite(nodeA.Position)) || !math.all(math.isfinite(nodeB.Position)))
                    {
                        nodeA.Position = nodeAPosition;
                        nodeA.OldPosition = SanitizeFloat3(nodeA.OldPosition, nodeAPosition);
                        nodeB.Position = nodeBPosition;
                        nodeB.OldPosition = SanitizeFloat3(nodeB.OldPosition, nodeBPosition);
                        Nodes[constraint.NodeA] = nodeA;
                        Nodes[constraint.NodeB] = nodeB;
                        WriteTension(constraintIndex, 0f);
                        continue;
                    }

                    float3 delta = nodeBPosition - nodeAPosition;
                    float lenSq = math.lengthsq(delta);
                    if (!math.isfinite(lenSq) || lenSq <= VerletCableLayout.MinConstraintLengthSq)
                    {
                        WriteTension(constraintIndex, 0f);
                        continue;
                    }

                    float distance = math.max(VerletCableSimdMath.LengthFromSq(lenSq), 0.0001f);
                    float invDistance = math.rcp(distance);
                    float restLength = math.max(
                        VerletCableLayout.MinConstraintLength,
                        SanitizeNonNegative(constraint.RestLength));
                    float error = distance - restLength;
                    float absError = math.abs(error);
                    maxError = math.max(maxError, absError);
                    errorSum += absError;
                    errorSamples++;

                    float stretch01 = math.max(0f, error) * math.rcp(math.max(restLength, VerletCableLayout.MinConstraintLength));
                    float tension = ClampTension(math.max(0f, error) * stiffness * tensionScale);
                    peakTension = math.max(peakTension, tension);
                    WriteTension(constraintIndex, tension);
                    WriteTensionForce(constraintIndex, SanitizeDirection(delta * invDistance), tension, nodeA.Position);

                    if (snapStretch > 0f && stretch01 >= snapStretch)
                    {
                        constraint.Stiffness = 0f;
                        Constraints[constraintIndex] = constraint;
                        snappedCount++;
                        WriteSnapSignal(constraintIndex, nodeA.Position, tension, stretch01);
                        continue;
                    }

                    if (plasticStretch > 0f && stretch01 > plasticStretch)
                    {
                        constraint.RestLength = math.lerp(restLength, distance, plasticCreep);
                        Constraints[constraintIndex] = constraint;
                    }

                    float invMassA = SanitizeNonNegative(nodeA.InvMass);
                    float invMassB = SanitizeNonNegative(nodeB.InvMass);
                    float invMassSum = invMassA + invMassB;
                    if (invMassSum <= 0.000001f)
                        continue;

                    float3 direction = SanitizeDirection(delta * invDistance);
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

            float averageError = errorSamples > 0 && math.isfinite(errorSum)
                ? math.max(0f, errorSum * math.rcp(errorSamples))
                : 0f;
            WriteSolverStats(ClampTension(peakTension), averageError, maxError, snappedCount);
        }

        private void WriteTension(int index, float tension)
        {
            if (SegmentTensions.IsCreated && index >= 0 && index < SegmentTensions.Length)
                SegmentTensions[index] = ClampTension(tension);
        }

        private void WriteTensionForce(int index, float3 direction, float tension, float3 applicationPoint)
        {
            if (!TensionForces.IsCreated || index < 0 || index >= TensionForces.Length)
                return;

            float safeTension = ClampTension(tension);
            float3 safeDirection = SanitizeDirection(direction);
            CableTensionForceDTO force = default;
            force.Force = safeDirection * safeTension;
            force.ApplicationPoint = applicationPoint;
            force.Tension = safeTension;
            force.CableId = CableId;
            TensionForces[index] = force;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float ClampTension(float tension)
        {
            return math.min(SanitizeNonNegative(tension), TetherAupRuntimeConstants.MaxTensionForceNewtons);
        }

        private static float3 SanitizeDirection(float3 direction)
        {
            return math.all(math.isfinite(direction)) ? direction : float3.zero;
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private void WriteSnapSignal(int constraintIndex, float3 position, float tension, float stretch01)
        {
            if (!SnapSignals.IsCreated || !SnapSignalCount.IsCreated || SnapSignalCount.Length == 0 || SnapSignals.Length == 0)
                return;

            int writeIndex = SnapSignalCount[0];
            if ((uint)writeIndex >= (uint)SnapSignals.Length)
                return;

            CableSnappedSignal signal = default;
            signal.Position = math.all(math.isfinite(position)) ? position : float3.zero;
            signal.PeakTension = math.isfinite(tension) ? tension : 0f;
            signal.CableId = CableId;
            signal.ConstraintIndex = constraintIndex;
            signal.FrameIndex = FrameIndex;
            signal.Reason = 1;
            signal.Flags = 0;
            signal.NodeCount = (ushort)math.min(Nodes.Length, ushort.MaxValue);
            signal.SnapThreshold = SanitizeNonNegative(SnapStretch01);
            signal.Severity01 = math.saturate(stretch01 * math.rcp(math.max(signal.SnapThreshold, 0.0001f)));
            signal.Reserved = 0u;
            SnapSignals[writeIndex] = signal;
            SnapSignalCount[0] = writeIndex + 1;
        }

        private void WriteSolverStats(float peakTension, float averageError, float maxError, int snappedCount)
        {
            if (!SolverStats.IsCreated)
                return;

            if (SolverStats.Length > 0)
                SolverStats[0] = ClampTension(peakTension);
            if (SolverStats.Length > 1)
                SolverStats[1] = SanitizeNonNegative(averageError);
            if (SolverStats.Length > 2)
                SolverStats[2] = SanitizeNonNegative(maxError);
            if (SolverStats.Length > 3)
                SolverStats[3] = math.max(0, snappedCount);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletWinchReelDTOJob : IJob
    {
        [NoAlias] public NativeArray<CableSystemDTO> Systems;
        [NoAlias] public NativeArray<VerletConstraintDTO> Constraints;
        [ReadOnly, NoAlias] public NativeArray<MockWinchSignal> WinchSignals;
        public int SystemIndex;
        public int WinchSignalIndex;
        public float DeltaTime;
        public float MinRestLength;

        public void Execute()
        {
            if (!Systems.IsCreated || !Constraints.IsCreated || (uint)SystemIndex >= (uint)Systems.Length)
                return;

            CableSystemDTO system = Systems[SystemIndex];
            int constraintOffset = math.clamp(system.ConstraintOffset, 0, Constraints.Length);
            int constraintCount = math.clamp(system.ConstraintCount, 0, Constraints.Length - constraintOffset);
            if (constraintCount <= 0)
                return;

            float safeDeltaTime = SanitizeNonNegative(DeltaTime);
            float shrink = SanitizeNonNegative(system.ReelingSpeedMetersPerSecond) * safeDeltaTime;
            float minRestLength = math.max(VerletCableLayout.MinConstraintLength, SanitizeNonNegative(MinRestLength));
            if (WinchSignals.IsCreated && (uint)WinchSignalIndex < (uint)WinchSignals.Length)
            {
                MockWinchSignal signal = WinchSignals[WinchSignalIndex];
                if (signal.SystemIndex == SystemIndex || signal.SystemIndex < 0)
                {
                    shrink += SanitizeNonNegative(signal.SpeedMetersPerSecond) * safeDeltaTime;
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
                int constraintIndex = constraintOffset + i;
                VerletConstraintDTO constraint = Constraints[constraintIndex];
                constraint.RestLength = math.max(minRestLength, SanitizeNonNegative(constraint.RestLength, minRestLength) - perConstraintShrink);
                Constraints[constraintIndex] = constraint;
            }

            int lastConstraint = constraintOffset + constraintCount - 1;
            if (constraintCount > 1 && Constraints[lastConstraint].RestLength <= minRestLength + 0.0001f)
            {
                system.ActiveNodeCount = math.max(2, system.ActiveNodeCount - 1);
                system.ConstraintCount = math.max(1, system.ActiveNodeCount - 1);
                Constraints[lastConstraint] = default;
            }

            Systems[SystemIndex] = system;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletCableOriginShiftDTOJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<VerletNodeDTO> Nodes;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletGpuSplineCopyJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<GpuCableSplinePointDTO> GpuPoints;
        public float3 Origin;
        public float InvSnapTension;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Nodes.Length || (uint)index >= (uint)GpuPoints.Length)
                return;

            float tension = 0f;
            if (SegmentTensions.IsCreated && SegmentTensions.Length > 0)
                tension = SegmentTensions[math.min(index, SegmentTensions.Length - 1)];

            GpuCableSplinePointDTO point = default;
            point.Position = SanitizeFloat3(Nodes[index].Position) + SanitizeFloat3(Origin);
            point.Tension01 = math.saturate(SanitizeNonNegative(tension) * SanitizeNonNegative(InvSnapTension));
            GpuPoints[index] = point;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletAabbFrustumCullJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<float4> FrustumPlanes;
        [NoAlias] public NativeArray<CableAabbDTO> Aabbs;
        public int AabbIndex;
        public float3 Origin;
        public float Radius;

        public void Execute()
        {
            if (!Nodes.IsCreated || Nodes.Length == 0 || !Aabbs.IsCreated || (uint)AabbIndex >= (uint)Aabbs.Length)
                return;

            float3 origin = SanitizeFloat3(Origin);
            float3 first = SanitizeFloat3(Nodes[0].Position) + origin;
            float3 minPoint = first;
            float3 maxPoint = first;
            for (int i = 1; i < Nodes.Length; i++)
            {
                float3 point = SanitizeFloat3(Nodes[i].Position) + origin;
                minPoint = math.min(minPoint, point);
                maxPoint = math.max(maxPoint, point);
            }

            float radius = SanitizeNonNegative(Radius);
            minPoint -= radius;
            maxPoint += radius;

            int visible = 1;
            int planeCount = FrustumPlanes.IsCreated ? math.min(6, FrustumPlanes.Length) : 0;
            for (int i = 0; i < planeCount; i++)
            {
                float4 plane = FrustumPlanes[i];
                if (!math.all(math.isfinite(plane)))
                    continue;

                float3 positive = default;
                positive.x = plane.x >= 0f ? maxPoint.x : minPoint.x;
                positive.y = plane.y >= 0f ? maxPoint.y : minPoint.y;
                positive.z = plane.z >= 0f ? maxPoint.z : minPoint.z;
                if (math.dot(plane.xyz, positive) + plane.w < 0f)
                {
                    visible = 0;
                    break;
                }
            }

            CableAabbDTO aabb = default;
            aabb.Min = minPoint;
            aabb.Max = maxPoint;
            aabb.Visible = visible;
            aabb.Dirty = 1;
            Aabbs[AabbIndex] = aabb;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerletBlackBoxWriteJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<VerletNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<float> SolverStats;
        [NoAlias] public NativeArray<VerletCableBlackBoxEntry> Ring;
        [NoAlias] public NativeArray<int> Head;
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
            float3 first = activeCount > 0 ? SanitizeFloat3(Nodes[0].Position) : float3.zero;
            float3 last = activeCount > 0 ? SanitizeFloat3(Nodes[activeCount - 1].Position) : float3.zero;
            uint hash = 2166136261u;
            for (int i = 0; i < activeCount; i++)
            {
                float3 point = SanitizeFloat3(Nodes[i].Position);
                hash = (hash ^ math.asuint(point.x)) * 16777619u;
                hash = (hash ^ math.asuint(point.y)) * 16777619u;
                hash = (hash ^ math.asuint(point.z)) * 16777619u;
            }

            VerletCableBlackBoxEntry entry = default;
            entry.FrameIndex = FrameIndex;
            entry.CableId = CableId;
            entry.ActiveNodeCount = activeCount;
            entry.ConstraintCount = ConstraintCount;
            entry.FirstPosition = first;
            entry.LastPosition = last;
            entry.MaxTension = SanitizeNonNegative(SolverStats.IsCreated && SolverStats.Length > 0 ? SolverStats[0] : 0f);
            entry.AverageError = SanitizeNonNegative(SolverStats.IsCreated && SolverStats.Length > 1 ? SolverStats[1] : 0f);
            entry.Flags = Flags;
            entry.StateHash = hash;
            entry.Reserved0 = 0u;
            entry.Reserved1 = 0u;
            Ring[head] = entry;
            Head[0] = (head + 1) % capacity;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }
    }

#if UNITY_EDITOR
    public static class CableMaterialCsvParser
    {
        private const uint DefaultHash = 0x5645524Cu;

        public static int Parse(ReadOnlySpan<byte> csv, NativeArray<CableMaterialDTO> output)
        {
            if (!output.IsCreated || output.Length == 0 || csv.Length == 0)
                return 0;

            int parsed = 0;
            int cursor = 0;
            while (cursor < csv.Length && parsed < output.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != (byte)'\n' && csv[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> line = csv.Slice(lineStart, cursor - lineStart);
                if (TryParseLine(line, parsed, out CableMaterialDTO material))
                {
                    output[parsed] = material;
                    parsed++;
                }

                while (cursor < csv.Length && (csv[cursor] == (byte)'\n' || csv[cursor] == (byte)'\r'))
                    cursor++;
            }

            return parsed;
        }

        public static int ParseHashTable(ReadOnlySpan<byte> csv, NativeArray<CableMaterialDTO> output)
        {
            if (!output.IsCreated || output.Length == 0 || csv.Length == 0)
                return 0;

            for (int i = 0; i < output.Length; i++)
                output[i] = default;

            int parsed = 0;
            int cursor = 0;
            while (cursor < csv.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != (byte)'\n' && csv[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> line = csv.Slice(lineStart, cursor - lineStart);
                if (TryParseLine(line, parsed, out CableMaterialDTO material) &&
                    TryInsertHashSlot(output, material))
                {
                    parsed++;
                }

                while (cursor < csv.Length && (csv[cursor] == (byte)'\n' || csv[cursor] == (byte)'\r'))
                    cursor++;
            }

            return parsed;
        }

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

        public static bool TryFindHashSlot(NativeArray<CableMaterialDTO> table, uint materialHash, out CableMaterialDTO material)
        {
            material = default;
            if (!table.IsCreated || table.Length == 0 || materialHash == 0u)
                return false;

            int start = (int)(materialHash % (uint)table.Length);
            for (int probe = 0; probe < table.Length; probe++)
            {
                int index = start + probe;
                if (index >= table.Length)
                    index -= table.Length;

                CableMaterialDTO candidate = table[index];
                if (candidate.MaterialHash == materialHash)
                {
                    material = candidate;
                    return true;
                }

                if (candidate.MaterialHash == 0u)
                    return false;
            }

            return false;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, int rowIndex, out CableMaterialDTO material)
        {
            material = default;
            line = Trim(line);
            if (line.Length == 0 || line[0] == (byte)'#')
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

            int fieldIndex = 0;
            int cursor = 0;
            while (cursor <= line.Length)
            {
                int start = cursor;
                while (cursor < line.Length && line[cursor] != (byte)',')
                    cursor++;

                ReadOnlySpan<byte> field = Trim(line.Slice(start, cursor - start));
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

            material.MaterialHash = materialHash;
            material.LinearDensity = math.max(0.001f, density);
            material.YieldStretch01 = math.max(0f, yield);
            material.SnapStretch01 = math.max(yield + 0.01f, snap);
            material.SolverTuning = math.float4(math.saturate(stiffness), math.saturate(damping), math.saturate(friction), math.max(0.001f, radius));
            material.VisualTuning = math.float4(math.max(0.001f, radius), 0.35f, 0.22f, 0f);
            material.LoadTuning = math.float4(24f, 3f, 5f, 10f);
            return true;
        }

        private static bool TryInsertHashSlot(NativeArray<CableMaterialDTO> table, CableMaterialDTO material)
        {
            if (!table.IsCreated || table.Length == 0 || material.MaterialHash == 0u)
                return false;

            int start = (int)(material.MaterialHash % (uint)table.Length);
            for (int probe = 0; probe < table.Length; probe++)
            {
                int index = start + probe;
                if (index >= table.Length)
                    index -= table.Length;

                uint existingHash = table[index].MaterialHash;
                if (existingHash == 0u || existingHash == material.MaterialHash)
                {
                    table[index] = material;
                    return true;
                }
            }

            return false;
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

            material.MaterialHash = materialHash;
            material.LinearDensity = math.max(0.001f, density);
            material.YieldStretch01 = math.max(0f, yield);
            material.SnapStretch01 = math.max(yield + 0.01f, snap);
            material.SolverTuning = math.float4(math.saturate(stiffness), math.saturate(damping), math.saturate(friction), math.max(0.001f, radius));
            material.VisualTuning = math.float4(math.max(0.001f, radius), 0.35f, 0.22f, 0f);
            material.LoadTuning = math.float4(24f, 3f, 5f, 10f);
            return true;
        }

        private static bool StartsWithAlpha(ReadOnlySpan<char> text)
        {
            if (text.Length == 0)
                return false;

            char c = text[0];
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static bool StartsWithAlpha(ReadOnlySpan<byte> text)
        {
            if (text.Length == 0)
                return false;

            byte c = text[0];
            return (c >= (byte)'A' && c <= (byte)'Z') || (c >= (byte)'a' && c <= (byte)'z');
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

        private static uint HashKey(ReadOnlySpan<byte> text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                byte c = text[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
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

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start < text.Length && IsWhite(text[start]))
                start++;
            while (end >= start && IsWhite(text[end]))
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsWhite(char c)
        {
            return c == ' ' || c == '\t';
        }

        private static bool IsWhite(byte c)
        {
            return c == (byte)' ' || c == (byte)'\t';
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

        private static bool TryParseFloat(ReadOnlySpan<byte> text, out float value)
        {
            value = 0f;
            if (text.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (text[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (text[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool any = false;
            while (index < text.Length && text[index] >= (byte)'0' && text[index] <= (byte)'9')
            {
                integer = integer * 10f + (text[index] - (byte)'0');
                index++;
                any = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < text.Length && text[index] == (byte)'.')
            {
                index++;
                while (index < text.Length && text[index] >= (byte)'0' && text[index] <= (byte)'9')
                {
                    fraction = fraction * 10f + (text[index] - (byte)'0');
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
#endif
}
