using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Physics.KCC
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicKccInputDTO
    {
        [FieldOffset(0)] public double3 TargetAup;
        [FieldOffset(24)] public float3 MoveAxis;
        [FieldOffset(36)] public float3 LookAxis;
        [FieldOffset(48)] public uint SimulationFrame;
        [FieldOffset(52)] public uint Sequence;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint SourceHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicKccTuningDTO
    {
        [FieldOffset(0)] public float BaseDrag;
        [FieldOffset(4)] public float FluidDensity;
        [FieldOffset(8)] public float MaxSpeed;
        [FieldOffset(12)] public float GravityMultiplier;
        [FieldOffset(16)] public float BuoyancyScalar;
        [FieldOffset(20)] public float CapsuleRadius;
        [FieldOffset(24)] public float CapsuleHeight;
        [FieldOffset(28)] public float SkinWidth;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float WaterSurfaceY;
        [FieldOffset(40)] public float MockInputFrequency;
        [FieldOffset(44)] public float MockInputAmplitude;
        [FieldOffset(48)] public float VisualSyncSharpness;
        [FieldOffset(52)] public float WakeThreshold;
        [FieldOffset(56)] public uint ProfileHash;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicKccVisualOutputDTO
    {
        [FieldOffset(0)] public double3 SourceAup;
        [FieldOffset(24)] public float3 LocalPosition;
        [FieldOffset(36)] public float3 PreviousLocalPosition;
        [FieldOffset(48)] public float SmoothingAlpha;
        [FieldOffset(52)] public float Speed;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicWakePacketDTO
    {
        [FieldOffset(0)] public double3 AupPosition;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float TurbulenceScalar;
        [FieldOffset(40)] public float WakeRadius;
        [FieldOffset(44)] public float WakeMagnitude;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicKccDebugOutputDTO
    {
        [FieldOffset(0)] public float3 CurrentLocal;
        [FieldOffset(12)] public float3 PredictedLocal;
        [FieldOffset(24)] public float3 CollisionNormal;
        [FieldOffset(36)] public float HitDistance;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicKccCollisionHitDTO
    {
        [FieldOffset(0)] public float3 Point;
        [FieldOffset(12)] public float Distance;
        [FieldOffset(16)] public float3 Normal;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float PenetrationDepth;
        [FieldOffset(36)] public uint SampleIndex;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct KinematicTelemetryEntry
    {
        [FieldOffset(0)] public double3 AupPosition;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float Speed;
        [FieldOffset(40)] public float TurbulenceScalar;
        [FieldOffset(44)] public float ComputeMicroseconds;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Iterations;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicFluidProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float BaseDrag;
        [FieldOffset(8)] public float FluidDensity;
        [FieldOffset(12)] public float MaxSpeed;
        [FieldOffset(16)] public float GravityMultiplier;
        [FieldOffset(20)] public float BuoyancyScalar;
        [FieldOffset(24)] public int NextIndex;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicKccFaultFlagDTO
    {
        [FieldOffset(0)] public int FaultMask;
        [FieldOffset(4)] public uint _pad0;
        [FieldOffset(8)] public ulong _pad1;
        [FieldOffset(16)] public ulong _pad2;
        [FieldOffset(24)] public ulong _pad3;
        [FieldOffset(32)] public ulong _pad4;
        [FieldOffset(40)] public ulong _pad5;
        [FieldOffset(48)] public ulong _pad6;
        [FieldOffset(56)] public ulong _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct KccEnvironmentProfileDTO
    {
        [FieldOffset(0)] public float MaxSlopeAngle;
        [FieldOffset(4)] public float CurrentAdvectionScalar;
        [FieldOffset(8)] public float FrictionCoefficient;
        [FieldOffset(12)] public float ExhaustionPenaltyMax;
        [FieldOffset(16)] public byte _pad0;
        [FieldOffset(17)] public byte _pad1;
        [FieldOffset(18)] public byte _pad2;
        [FieldOffset(19)] public byte _pad3;
        [FieldOffset(20)] public byte _pad4;
        [FieldOffset(21)] public byte _pad5;
        [FieldOffset(22)] public byte _pad6;
        [FieldOffset(23)] public byte _pad7;
        [FieldOffset(24)] public byte _pad8;
        [FieldOffset(25)] public byte _pad9;
        [FieldOffset(26)] public byte _pad10;
        [FieldOffset(27)] public byte _pad11;
        [FieldOffset(28)] public byte _pad12;
        [FieldOffset(29)] public byte _pad13;
        [FieldOffset(30)] public byte _pad14;
        [FieldOffset(31)] public byte _pad15;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct KccEnvironmentGridDTO
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public int3 Dimensions;
        [FieldOffset(36)] public float CellSizeMeters;
        [FieldOffset(40)] public float SdfSurfaceMeters;
        [FieldOffset(44)] public float SdfFrictionBandMeters;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct KccEnvironmentDebugOutputDTO
    {
        [FieldOffset(0)] public float3 AppliedFlow;
        [FieldOffset(12)] public float3 SlopeSlideVector;
        [FieldOffset(24)] public float ExhaustionPenalty;
        [FieldOffset(28)] public float SdfFriction;
        [FieldOffset(32)] public float SlopeAngleDegrees;
        [FieldOffset(36)] public float ComputeMicroseconds;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct KccEnvironmentTelemetryEntry
    {
        [FieldOffset(0)] public double3 AupPosition;
        [FieldOffset(24)] public float3 AppliedFlow;
        [FieldOffset(36)] public float SlopeAngleDegrees;
        [FieldOffset(40)] public float ExhaustionPenalty;
        [FieldOffset(44)] public float ComputeMicroseconds;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint SampleMode;
    }

#if UNITY_EDITOR
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HydrodynamicKccLayoutReport
    {
        [FieldOffset(0)] public int StateSize;
        [FieldOffset(4)] public int OffsetAup;
        [FieldOffset(8)] public int OffsetVelocity;
        [FieldOffset(12)] public int OffsetAngularVelocity;
        [FieldOffset(16)] public int OffsetMass;
        [FieldOffset(20)] public int OffsetFlags;
        [FieldOffset(24)] public int OffsetDragCoefficient;
        [FieldOffset(28)] public int OffsetRestingFrameCount;
        [FieldOffset(32)] public int TuningSize;
        [FieldOffset(36)] public int TelemetrySize;
        [FieldOffset(40)] public int WakePacketSize;
        [FieldOffset(44)] public int DebugSize;
        [FieldOffset(48)] public int FaultFlagSize;
        [FieldOffset(52)] public int CollisionHitSize;
        [FieldOffset(56)] public int InputSize;
        [FieldOffset(60)] public int _pad0;
    }

    public static class HydrodynamicKccLayoutValidator
    {
        public const int KinematicStateSize = 64;
        public const int KinematicStateAupOffset = 0;
        public const int KinematicStateVelocityOffset = 24;
        public const int KinematicStateAngularVelocityOffset = 36;
        public const int KinematicStateMassOffset = 48;
        public const int KinematicStateFlagsOffset = 52;
        public const int KinematicStateDragOffset = 56;
        public const int KinematicStateRestingFrameOffset = 60;
        public const int EnvironmentProfileSize = 32;
        public const int EnvironmentProfileMaxSlopeOffset = 0;
        public const int EnvironmentProfileCurrentOffset = 4;
        public const int EnvironmentProfileFrictionOffset = 8;
        public const int EnvironmentProfileExhaustionOffset = 12;
        public const int CollisionHitPenetrationOffset = 32;
        public const int CollisionHitSampleOffset = 36;

        public static bool ValidateRuntimeLayout(out HydrodynamicKccLayoutReport report)
        {
            report = new HydrodynamicKccLayoutReport
            {
                StateSize = UnsafeUtility.SizeOf<KinematicStateDTO>(),
                OffsetAup = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.AUP_Position)),
                OffsetVelocity = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Velocity)),
                OffsetAngularVelocity = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.AngularVelocity)),
                OffsetMass = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Mass)),
                OffsetFlags = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Flags)),
                OffsetDragCoefficient = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.DragCoefficient)),
                OffsetRestingFrameCount = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.RestingFrameCount)),
                TuningSize = UnsafeUtility.SizeOf<HydrodynamicKccTuningDTO>(),
                TelemetrySize = UnsafeUtility.SizeOf<KinematicTelemetryEntry>(),
                WakePacketSize = UnsafeUtility.SizeOf<HydrodynamicWakePacketDTO>(),
                DebugSize = UnsafeUtility.SizeOf<HydrodynamicKccDebugOutputDTO>(),
                FaultFlagSize = UnsafeUtility.SizeOf<HydrodynamicKccFaultFlagDTO>(),
                CollisionHitSize = UnsafeUtility.SizeOf<HydrodynamicKccCollisionHitDTO>(),
                InputSize = UnsafeUtility.SizeOf<HydrodynamicKccInputDTO>()
            };

            return report.StateSize == KinematicStateSize &&
                   report.OffsetAup == KinematicStateAupOffset &&
                   report.OffsetVelocity == KinematicStateVelocityOffset &&
                   report.OffsetAngularVelocity == KinematicStateAngularVelocityOffset &&
                   report.OffsetMass == KinematicStateMassOffset &&
                   report.OffsetFlags == KinematicStateFlagsOffset &&
                   report.OffsetDragCoefficient == KinematicStateDragOffset &&
                   report.OffsetRestingFrameCount == KinematicStateRestingFrameOffset &&
                   report.TuningSize == 64 &&
                   report.TelemetrySize == 64 &&
                   report.WakePacketSize == 64 &&
                   report.DebugSize == 64 &&
                   report.FaultFlagSize == 64 &&
                   report.CollisionHitSize == 64 &&
                   OffsetOf<HydrodynamicKccCollisionHitDTO>(nameof(HydrodynamicKccCollisionHitDTO.PenetrationDepth)) == CollisionHitPenetrationOffset &&
                   OffsetOf<HydrodynamicKccCollisionHitDTO>(nameof(HydrodynamicKccCollisionHitDTO.SampleIndex)) == CollisionHitSampleOffset &&
                   report.InputSize == 64 &&
                   UnsafeUtility.SizeOf<KccEnvironmentProfileDTO>() == EnvironmentProfileSize &&
                   UnsafeUtility.AlignOf<KccEnvironmentProfileDTO>() > 0 &&
                   OffsetOf<KccEnvironmentProfileDTO>(nameof(KccEnvironmentProfileDTO.MaxSlopeAngle)) == EnvironmentProfileMaxSlopeOffset &&
                   OffsetOf<KccEnvironmentProfileDTO>(nameof(KccEnvironmentProfileDTO.CurrentAdvectionScalar)) == EnvironmentProfileCurrentOffset &&
                   OffsetOf<KccEnvironmentProfileDTO>(nameof(KccEnvironmentProfileDTO.FrictionCoefficient)) == EnvironmentProfileFrictionOffset &&
                   OffsetOf<KccEnvironmentProfileDTO>(nameof(KccEnvironmentProfileDTO.ExhaustionPenaltyMax)) == EnvironmentProfileExhaustionOffset &&
                   OffsetOf<KccEnvironmentProfileDTO>(nameof(KccEnvironmentProfileDTO._pad0)) == 16 &&
                   OffsetOf<KccEnvironmentProfileDTO>(nameof(KccEnvironmentProfileDTO._pad15)) == 31 &&
                   UnsafeUtility.SizeOf<KccEnvironmentGridDTO>() == 64 &&
                   UnsafeUtility.SizeOf<KccEnvironmentDebugOutputDTO>() == 64 &&
                   UnsafeUtility.SizeOf<KccEnvironmentTelemetryEntry>() == 64;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
#endif

    public static class HydrodynamicKccMath
    {
        public const uint FlagFaultNaN = 1u;
        public const uint FlagCollision = 1u << 1;
        public const uint FlagWake = 1u << 2;
        public const uint FlagMockInput = 1u << 3;
        public const uint FlagVisualBypass = 1u << 4;
        public const uint FlagInputRejected = 1u << 5;
        public const uint FlagRespawnCollisionBypass = 1u << 6;
        public const uint FlagEnvironmentalForces = 1u << 7;
        public const uint FlagSlopeSlide = 1u << 8;
        public const uint FlagMetabolicPenalty = 1u << 9;
        public const uint FlagSdfFriction = 1u << 10;
        public const uint FlagEnvironmentMock = 1u << 11;
        public const uint FlagTrilinearFlowSample = 1u << 12;
        public const uint FlagExternalAcceleration = 1u << 13;
        public const uint FlagExternalVelocityChange = 1u << 14;
        public const uint FlagExternalVelocityTarget = 1u << 15;
        public const uint FlagExternalPositionTarget = 1u << 16;
        public const uint FlagSignalDrop = 1u << 17;
        public const uint FlagExternalRotationTarget = 1u << 18;
        public const uint FlagMediumCurrentDrag = 1u << 19;
        public const uint FlagMediumThermocline = 1u << 20;
        public const uint FlagMediumDensityBuoyancy = 1u << 21;

        /// <summary>
        /// Converts the 0-1 resistance scalar from ThermoclineResistanceCalculator into a first-order
        /// drag rate (1/s). The scalar saturates at 1.0 for any non-trivial speed, so it is fed through
        /// the same implicit 1/(1 + k*dt) denominator the base drag uses: at resistance 1.0 that decays
        /// speed instead of zeroing it, which an explicit (1 - resistance) multiply would do.
        /// </summary>
        public const float ThermoclineDragRatePerSecond = 3.2f;
        public const uint InputFlagMask = 0x0000FFFFu;
        public const int InputGenerationShift = 16;
        public const float MinDenominator = 0.0001f;
        public const float AuthoritativeQualityWeight = 1f;
        public const float DefaultWaterSurfaceY = Hecton8.World.WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        public const float MillimeterScale = 1000f;
        public const float InvMillimeterScale = 0.001f;
        public const float MaxLocalFloatMagnitude = 131072f;
        public const double MaxAupMagnitudeMeters = 9000000000000d;
        public const uint SourceHash = 0x53484B43u;
        public const uint WakeSourcePlayer = 1u;
        public const uint HitFlagValid = 1u;
        public const uint HitFlagSdfSpeculative = 1u << 1;
        public const uint HitFlagPenetrating = 1u << 2;
        public const float DuplicateContactPlaneDotThreshold = 0.9995f;
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const float HalfPi = 1.57079632679f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Sanitize(float3 value, float3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 Sanitize(double3 value, double3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveWaterSurfaceY(float candidateWaterSurfaceY)
        {
            return math.isfinite(candidateWaterSurfaceY) &&
                   math.abs(candidateWaterSurfaceY) > MinDenominator &&
                   math.abs(candidateWaterSurfaceY) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY
                ? candidateWaterSurfaceY
                : DefaultWaterSurfaceY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveRuntimeWaterSurfaceY(float candidateWaterSurfaceY)
        {
            return math.isfinite(candidateWaterSurfaceY) &&
                   math.abs(candidateWaterSurfaceY) > MinDenominator &&
                   math.abs(candidateWaterSurfaceY) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY
                ? candidateWaterSurfaceY
                : DefaultWaterSurfaceY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return lenSq > 0.000001f && math.isfinite(lenSq)
                ? value * math.rsqrt(math.max(lenSq, 0.000001f))
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthSafe(float3 value)
        {
            float lenSq = math.lengthsq(value);
            return lenSq > 0.000001f && math.isfinite(lenSq)
                ? lenSq * math.rsqrt(math.max(lenSq, 0.000001f))
                : 0f;
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
        public static float ExpNegRational(float value)
        {
            float x = math.min(8f, math.max(0f, math.select(0f, value, math.isfinite(value))));
            float x2 = x * x;
            return math.rcp(1f + x + 0.48f * x2 + 0.235f * x2 * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 QuantizeMillimeter(double3 aup)
        {
            return math.round(aup * MillimeterScale) * (double)InvMillimeterScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveLocalFloat3(double3 aup, double3 sectorOriginAup)
        {
            double3 delta = Sanitize(aup - sectorOriginAup, double3.zero);
            double maxAbs = math.cmax(math.abs(delta));
            if (!math.isfinite(maxAbs))
                return float3.zero;

            double limit = MaxLocalFloatMagnitude;
            delta = math.clamp(delta, new double3(-limit), new double3(limit));
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveIterationCount(float globalQualityWeight)
        {
            float quality = ResolveQuality01(globalQualityWeight);
            return math.clamp((int)math.round(math.lerp(3f, 8f, quality)), 3, 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveQuality01(float globalQualityWeight)
        {
            return math.saturate(math.select(AuthoritativeQualityWeight, globalQualityWeight, math.isfinite(globalQualityWeight)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDynamicPenetrationEpsilon(float globalQualityWeight, float skinWidth)
        {
            float quality = ResolveQuality01(globalQualityWeight);
            float baseEpsilon = math.max(0.0005f, skinWidth * 0.02f);
            float maxAllowedEpsilon = math.max(baseEpsilon, skinWidth * 0.75f);
            return math.lerp(baseEpsilon, maxAllowedEpsilon, 1f - quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSpeculativeSampleCount(
            float globalQualityWeight,
            float castDistance,
            float capsuleRadius,
            float skinWidth,
            int maxStride)
        {
            int stride = math.clamp(maxStride, 1, 8);
            float quality = ResolveQuality01(globalQualityWeight);
            int qualitySteps = math.clamp((int)math.round(math.lerp(3f, (float)stride, quality)), 1, stride);
            float safeCastDistance = math.max(0f, math.isfinite(castDistance) ? castDistance : 0f);
            float radius = math.max(0.05f, math.isfinite(capsuleRadius) ? capsuleRadius : 0.35f);
            float skin = math.max(0.001f, math.isfinite(skinWidth) ? skinWidth : 0.02f);
            float conservativeSpan = math.max(0.15f, (radius + skin) * 0.75f);
            int speedSteps = math.clamp((int)math.ceil(safeCastDistance * math.rcp(conservativeSpan)), 1, stride);
            return math.clamp(math.max(qualitySteps, speedSteps), 1, stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EstimateIntegrationMicroseconds(float globalQualityWeight, float speed)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float safeSpeed = math.max(0f, math.isfinite(speed) ? speed : 0f);
            return math.lerp(0.55f, 1.6f, quality) + math.saturate(safeSpeed * 0.125f) * math.lerp(0.08f, 0.36f, quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EstimateResolutionMicroseconds(float globalQualityWeight, uint iterations, bool hasCollision, float speed)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float safeSpeed = math.max(0f, math.isfinite(speed) ? speed : 0f);
            float iterationCost = math.max(1f, (float)iterations) * math.lerp(0.08f, 0.22f, quality);
            float collisionCost = math.select(0.04f, math.lerp(0.28f, 0.95f, quality), hasCollision);
            return math.lerp(0.35f, 0.9f, quality) + iterationCost + collisionCost + math.saturate(safeSpeed * 0.1f) * 0.2f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EstimateEnvironmentMicroseconds(float globalQualityWeight, bool trilinear, bool slopeSlide, float flowMagnitude)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float flowCost = math.saturate(math.max(0f, flowMagnitude) * 0.08f) * math.lerp(0.02f, 0.11f, quality);
            float sampleCost = math.select(0.08f, 0.31f, trilinear);
            float slopeCost = math.select(0.02f, 0.18f, slopeSlide);
            return math.lerp(0.18f, 0.52f, quality) + sampleCost + slopeCost + flowCost;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackWakeSourceFlags(uint sourceKind, float magnitude, float radius)
        {
            float safeMagnitude = math.max(0f, math.isfinite(magnitude) ? magnitude : 0f);
            float safeRadius = math.max(0f, math.isfinite(radius) ? radius : 0f);
            uint magnitudeQ = (uint)math.clamp((int)math.round(safeMagnitude * 64f), 0, 4095);
            uint radiusQ = (uint)math.clamp((int)math.round(safeRadius * 64f), 0, 4095);
            return (sourceKind & 0xFFu) | (magnitudeQ << 8) | (radiusQ << 20);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeSectorGeneration(double3 sectorOriginAup)
        {
            return HashState(Sanitize(sectorOriginAup, double3.zero), float3.zero, 0u, 0xA113C0DEu) & InputFlagMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackInputFlags(uint flags, uint sectorGeneration)
        {
            return (flags & InputFlagMask) | ((sectorGeneration & InputFlagMask) << InputGenerationShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExtractInputFlags(uint packedFlags)
        {
            return packedFlags & InputFlagMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InputGenerationMatches(uint packedFlags, uint sectorGeneration)
        {
            return ((packedFlags >> InputGenerationShift) & InputFlagMask) == (sectorGeneration & InputFlagMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SeedNonZero(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashState(double3 aup, float3 velocity, uint frame, uint flags)
        {
            double3 safeAup = Sanitize(aup, double3.zero);
            uint hash = 2166136261u;
            hash = Fnv(hash, QuantizedMeterLow32(safeAup.x));
            hash = Fnv(hash, QuantizedMeterHigh32(safeAup.x));
            hash = Fnv(hash, QuantizedMeterLow32(safeAup.y));
            hash = Fnv(hash, QuantizedMeterHigh32(safeAup.y));
            hash = Fnv(hash, QuantizedMeterLow32(safeAup.z));
            hash = Fnv(hash, QuantizedMeterHigh32(safeAup.z));
            hash = Fnv(hash, math.asuint(velocity.x));
            hash = Fnv(hash, math.asuint(velocity.y));
            hash = Fnv(hash, math.asuint(velocity.z));
            hash = Fnv(hash, frame);
            hash = Fnv(hash, flags);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteUniversePosition ToAup48(double3 absolutePosition)
        {
            double3 safe = Sanitize(absolutePosition, double3.zero);
            safe = math.clamp(safe, new double3(-MaxAupMagnitudeMeters), new double3(MaxAupMagnitudeMeters));
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double invCellSize = math.rcp(math.max(0.0001d, cellSize));
            long gridX = (long)math.floor(safe.x * invCellSize);
            long gridY = (long)math.floor(safe.y * invCellSize);
            long gridZ = (long)math.floor(safe.z * invCellSize);
            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(safe.x - gridX * cellSize),
                LocalY = (float)(safe.y - gridY * cellSize),
                LocalZ = (float)(safe.z - gridZ * cellSize)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantizedMeterLow32(double value)
        {
            long quantized = QuantizedMillimeterLong(value);
            return unchecked((uint)quantized);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantizedMeterHigh32(double value)
        {
            long quantized = QuantizedMillimeterLong(value);
            return unchecked((uint)(quantized >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long QuantizedMillimeterLong(double value)
        {
            double safe = math.isfinite(value) ? value : 0d;
            safe = math.clamp(safe, -MaxAupMagnitudeMeters, MaxAupMagnitudeMeters);
            return (long)math.round(safe * MillimeterScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Fnv(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockMovementInputJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;
        public double3 AnchorAup;
        public HydrodynamicKccTuningDTO Tuning;
        public uint SimulationFrame;
        public uint SectorHash;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            Inputs[index] = BuildInput(index, AnchorAup, Tuning, SimulationFrame, SectorHash, SimulationTickDelta);
        }

        internal static HydrodynamicKccInputDTO BuildInput(
            int index,
            double3 anchorAup,
            HydrodynamicKccTuningDTO tuning,
            uint frame,
            uint sectorHash,
            float dt)
        {
            uint seed = HydrodynamicKccMath.SeedNonZero(sectorHash ^ (uint)(index * 0x9E3779B9) ^ (frame * 0x85EBCA6Bu));
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);
            float safeDt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(dt) ? dt : 0.016666667f);
            float quality = HydrodynamicKccMath.ResolveQuality01(tuning.GlobalQualityWeight);
            float frequency = math.max(0.01f, math.isfinite(tuning.MockInputFrequency) ? tuning.MockInputFrequency : 0.35f);
            float amplitude = math.max(0f, math.isfinite(tuning.MockInputAmplitude) ? tuning.MockInputAmplitude : 1f);
            float phase = rng.NextFloat(0f, 6.2831855f);
            float t = (frame + (uint)index) * safeDt;
            float forward = (0.55f + 0.45f * HydrodynamicKccMath.SinPolynomial7(t * frequency + phase)) * amplitude;
            float strafe = HydrodynamicKccMath.SinPolynomial7(t * (frequency * 0.37f) + phase * 0.5f) * math.lerp(0.05f, 0.25f, quality);

            return new HydrodynamicKccInputDTO
            {
                TargetAup = anchorAup,
                MoveAxis = new float3(strafe, 0f, forward),
                LookAxis = new float3(strafe, 0f, 1f),
                SimulationFrame = frame,
                Sequence = (uint)index,
                Flags = HydrodynamicKccMath.PackInputFlags(HydrodynamicKccMath.FlagMockInput, sectorHash),
                SourceHash = HydrodynamicKccMath.SourceHash
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockMovementInputQueueJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SignalBus owns this queue lane; the job is a producer only and the returned handle fences any drain.
        // Unity safety cannot see the external SignalBus lifetime contract for the ParallelWriter field.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Writing a managed input list was rejected because mock input generation must remain deterministic and
        // allocation-free. A post-job main-thread relay would also force same-frame readback from Burst output.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: the queue writer is provided only by the KCC input owner, and consumers drain it after the
        // scheduled JobHandle completes. This job never reads from the queue and has no alias with state arrays.
        [NoAlias] public NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter InputWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> InputWriterBudget;
        public double3 AnchorAup;
        public HydrodynamicKccTuningDTO Tuning;
        public uint SimulationFrame;
        public uint SectorHash;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            if (!TryEnqueueBounded(
                InputWriter,
                InputWriterBudget,
                GenerateMockMovementInputJob.BuildInput(index, AnchorAup, Tuning, SimulationFrame, SectorHash, SimulationTickDelta)))
            {
                return;
            }
        }

        private static unsafe bool TryEnqueueBounded(
            NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter writer,
            NativeArray<int> writerBudget,
            HydrodynamicKccInputDTO input)
        {
            const int remainingIndex = 0;
            const int droppedIndex = 1;
            const int budgetLength = 2;
            if (!writerBudget.IsCreated || writerBudget.Length < budgetLength)
                return false;

            int* budget = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writerBudget);
            int remainingAfterClaim = Interlocked.Decrement(ref budget[remainingIndex]);
            if (remainingAfterClaim < 0)
            {
                Interlocked.Increment(ref budget[droppedIndex]);
                return false;
            }

            writer.Enqueue(input);
            return true;
        }
    }

    public static class HydrodynamicKccInputContract
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HydrodynamicKccInputDTO BuildExternalInput(
            int entityIndex,
            double3 targetAup,
            float3 moveAxis,
            float3 lookAxis,
            uint simulationFrame,
            uint sectorGeneration,
            uint sourceHash,
            uint flags = 0u)
        {
            float3 safeMove = HydrodynamicKccMath.Sanitize(moveAxis, float3.zero);
            float moveLenSq = math.lengthsq(safeMove);
            if (moveLenSq > 1f)
                safeMove *= math.rsqrt(math.max(moveLenSq, 0.000001f));

            return new HydrodynamicKccInputDTO
            {
                TargetAup = HydrodynamicKccMath.Sanitize(targetAup, double3.zero),
                MoveAxis = safeMove,
                LookAxis = HydrodynamicKccMath.NormalizeSafe(lookAxis, new float3(0f, 0f, 1f)),
                SimulationFrame = simulationFrame,
                Sequence = (uint)math.max(0, entityIndex),
                Flags = HydrodynamicKccMath.PackInputFlags(flags, sectorGeneration),
                SourceHash = sourceHash == 0u ? HydrodynamicKccMath.SourceHash : sourceHash
            };
        }
    }

    /// <summary>Deterministic mock-input harness for profiling the hydrodynamic KCC without managed input systems.</summary>
    public static class HydrodynamicKccMockInput
    {
        /// <summary>Schedules deterministic movement input packets into a caller-owned native queue.</summary>
        public static JobHandle GenerateMockMovementInput(
            NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter inputWriter,
            NativeArray<int> inputWriterBudget,
            int count,
            double3 anchorAup,
            HydrodynamicKccTuningDTO tuning,
            uint simulationFrame,
            uint sectorHash,
            float simulationTickDelta,
            JobHandle dependency = default)
        {
            int safeCount = math.max(0, count);
            if (safeCount == 0)
                return dependency;

            return new GenerateMockMovementInputQueueJob
            {
                InputWriter = inputWriter,
                InputWriterBudget = inputWriterBudget,
                AnchorAup = anchorAup,
                Tuning = tuning,
                SimulationFrame = simulationFrame,
                SectorHash = sectorHash,
                SimulationTickDelta = simulationTickDelta
            }.Schedule(safeCount, 32, dependency);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearKccFaultFlagsJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;

        public void Execute(int index)
        {
            FaultFlags[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearKccInputBufferJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;

        public void Execute(int index)
        {
            Inputs[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SanitizeKccInputBufferJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;
        public double3 SectorOriginAup;
        public uint SimulationFrame;
        public uint SectorGeneration;

        public void Execute(int index)
        {
            HydrodynamicKccInputDTO input = index < Inputs.Length ? Inputs[index] : default;
            bool valid = input.SimulationFrame == SimulationFrame &&
                         input.Sequence == (uint)index &&
                         input.SourceHash != 0u &&
                         HydrodynamicKccMath.InputGenerationMatches(input.Flags, SectorGeneration) &&
                         HydrodynamicKccMath.IsFinite(input.TargetAup) &&
                         HydrodynamicKccMath.IsFinite(input.MoveAxis) &&
                         HydrodynamicKccMath.IsFinite(input.LookAxis);

            double3 targetDelta = HydrodynamicKccMath.Sanitize(input.TargetAup - SectorOriginAup, double3.zero);
            valid &= math.cmax(math.abs(targetDelta)) <= HydrodynamicKccMath.MaxLocalFloatMagnitude;

            if (!valid)
            {
                Inputs[index] = new HydrodynamicKccInputDTO
                {
                    TargetAup = SectorOriginAup,
                    MoveAxis = float3.zero,
                    LookAxis = new float3(0f, 0f, 1f),
                    SimulationFrame = SimulationFrame,
                    Sequence = (uint)index,
                    Flags = HydrodynamicKccMath.PackInputFlags(HydrodynamicKccMath.FlagInputRejected, SectorGeneration),
                    SourceHash = HydrodynamicKccMath.SourceHash
                };
                return;
            }

            float moveLenSq = math.lengthsq(input.MoveAxis);
            if (moveLenSq > 1f)
                input.MoveAxis *= math.rsqrt(math.max(moveLenSq, 0.000001f));
            input.LookAxis = HydrodynamicKccMath.NormalizeSafe(input.LookAxis, new float3(0f, 0f, 1f));
            input.Flags = HydrodynamicKccMath.PackInputFlags(HydrodynamicKccMath.ExtractInputFlags(input.Flags), SectorGeneration);
            Inputs[index] = input;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockEnvironmentalForcesJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<float3> FlowField;
        [WriteOnly, NoAlias] public NativeArray<float> SdfDistances;
        [WriteOnly, NoAlias] public NativeArray<MetabolicStateDTO> MockMetabolism;
        public KccEnvironmentGridDTO Grid;
        public HydrodynamicKccTuningDTO Tuning;
        public uint SimulationFrame;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            int3 dimensions = new int3(
                math.max(1, Grid.Dimensions.x),
                math.max(1, Grid.Dimensions.y),
                math.max(1, Grid.Dimensions.z));
            float cellSize = math.max(0.25f, math.isfinite(Grid.CellSizeMeters) ? Grid.CellSizeMeters : 2f);
            float quality = HydrodynamicKccMath.ResolveQuality01(Tuning.GlobalQualityWeight);
            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float t = (float)SimulationFrame * dt;

            if (FlowField.IsCreated && index < FlowField.Length)
            {
                int cellsPerLayer = dimensions.x * dimensions.z;
                int y = index / math.max(1, cellsPerLayer);
                int layerRemainder = index - y * cellsPerLayer;
                int z = layerRemainder / math.max(1, dimensions.x);
                int x = layerRemainder - z * dimensions.x;
                float3 invDims = math.rcp(new float3(math.max(1, dimensions.x - 1), math.max(1, dimensions.y - 1), math.max(1, dimensions.z - 1)));
                float3 uvw = new float3(x, y, z) * invDims;
                float3 centered = uvw * 2f - 1f;
                float swirlPhase = (centered.x * 2.31f + centered.z * 1.73f + centered.y * 0.41f) + t * 0.23f;
                float liftPhase = (centered.z * 2.11f - centered.x * 1.37f) + t * 0.17f;
                float3 tangent = HydrodynamicKccMath.NormalizeSafe(new float3(-centered.z, 0f, centered.x), new float3(0f, 0f, 1f));
                float swirl = HydrodynamicKccMath.SinPolynomial7(swirlPhase);
                float lift = HydrodynamicKccMath.SinPolynomial7(liftPhase) * math.lerp(0.04f, 0.18f, quality);
                float speed = math.lerp(0.2f, 1.9f, quality) * (0.65f + 0.35f * math.abs(swirl));
                FlowField[index] = tangent * speed + new float3(centered.x * 0.12f, lift, centered.z * -0.08f);
            }

            if (SdfDistances.IsCreated && index < SdfDistances.Length)
            {
                int cellsPerLayer = dimensions.x * dimensions.z;
                int y = index / math.max(1, cellsPerLayer);
                int layerRemainder = index - y * cellsPerLayer;
                int z = layerRemainder / math.max(1, dimensions.x);
                int x = layerRemainder - z * dimensions.x;
                float ripple = HydrodynamicKccMath.SinPolynomial7((x * 0.37f + z * 0.19f) + t * 0.11f) * math.lerp(0.05f, 0.35f, quality);
                SdfDistances[index] = (y * cellSize) - (cellSize * 1.35f + ripple);
            }

            if (MockMetabolism.IsCreated && index < MockMetabolism.Length)
            {
                float starvation = math.saturate(0.18f + 0.52f * (0.5f + 0.5f * HydrodynamicKccMath.SinPolynomial7(t * 0.09f + index * 0.61f)));
                float hydration = math.lerp(1f, 0.36f, starvation);
                float toxicity = math.lerp(0.04f, 0.42f, math.saturate(starvation * 0.75f + quality * 0.08f));
                MockMetabolism[index] = new MetabolicStateDTO
                {
                    Calories = math.lerp(1f, 0.18f, starvation),
                    Hydration = hydration,
                    CoreTemperature = 37f,
                    Toxicity = toxicity,
                    EntityHashID = HydrodynamicKccMath.SourceHash,
                    Flags = ShinobuMetabolismVaultContract.FlagMockEntity,
                    RealO2 = 1f,
                    AgonyTimeRemaining = 0f,
                    IsInHypoxia = 0
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyEnvironmentalForcesJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Each Execute index mutates exactly one KinematicStateDTO row through pointer access, then writes only
        // matching output/fault rows. Unity cannot prove this row mapping because the state mutation bypasses the
        // NativeArray indexer and the fault lane is a phase-owned mutable diagnostic buffer.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Copying state into a smaller temporary KCC view was rejected because it duplicates rollback-critical
        // facts and adds a memory pass. Keeping direct state reads plus disjoint output lanes preserves data
        // locality and the existing authority route.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: States and every output array are separate Vault lanes scheduled by HydrodynamicKccRuntime.
        // ProposedVelocities, WakePackets, EnvironmentDebugOutputs, and FaultFlags are consumed only after this
        // job's returned dependency completes.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;
        [ReadOnly, NoAlias] public NativeArray<KccEnvironmentProfileDTO> EnvironmentProfiles;
        [ReadOnly, NoAlias] public NativeArray<KccEnvironmentGridDTO> EnvironmentGrids;
        [ReadOnly, NoAlias] public NativeArray<float3> FlowField;
        [ReadOnly, NoAlias] public NativeArray<float> SdfDistances;
        [ReadOnly, NoAlias] public NativeArray<MetabolicStateDTO> MetabolismStates;
        [WriteOnly, NoAlias] public NativeArray<float3> ProposedVelocities;
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicWakePacketDTO> WakePackets;
        [WriteOnly, NoAlias] public NativeArray<KccEnvironmentDebugOutputDTO> EnvironmentDebugOutputs;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        public HydrodynamicKccTuningDTO Tuning;
        public double3 SectorOriginAup;
        public float3 ExternalAcceleration;
        public float3 ExternalVelocityChange;
        public float3 ExternalVelocityTarget;
        public double3 ExternalPositionTargetAup;
        public uint SimulationFrame;
        public uint ExternalControlFlags;
        public float SimulationTickDelta;

        // WATER-AS-MEDIUM inputs, resolved on the main thread in HydrodynamicKccRuntime.UpdateWaterMediumForces
        // and injected here as plain blittable values. The four PureLogic models that produce them
        // (OceanCurrentDragCalculator / ThermoclineResistanceCalculator / BuoyancyDensityRatioMath /
        // PressureCrushDamageModel) live in the Hecton8.PureLogic assembly, which is noEngineReferences and
        // built on System.Numerics.Vector3 + System.Math. They cannot be invoked from inside this
        // [BurstCompile] job, so only their already-reduced scalar/vector results cross the boundary.
        /// <summary>
        /// Ocean-current drag force in Newtons, world space. Divided by safeMass here. Computed against the
        /// same FlowField cell and the same CurrentAdvectionScalar this job advects with, in m/s; when it is
        /// applied it REPLACES the raw `appliedFlow * dt` add so the current couples once, not twice.
        /// </summary>
        public float3 MediumCurrentDragForce;
        /// <summary>Thermocline resistance scalar 0-1; folded into the drag denominator.</summary>
        public float MediumThermoclineResistance01;
        /// <summary>Net density buoyancy in Newtons (buoyant force minus body weight) at full submersion.</summary>
        public float MediumDensityBuoyancyNewtons;
        /// <summary>Validity bits for the three medium terms above.</summary>
        public uint MediumFlags;

        public void Execute(int index)
        {
            if (!States.IsCreated ||
                !ProposedVelocities.IsCreated ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)ProposedVelocities.Length)
            {
                return;
            }

            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States) + (index * stateSize);
            ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statePtr);

            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float quality = HydrodynamicKccMath.ResolveQuality01(Tuning.GlobalQualityWeight);
            float visualQuality = quality;
            HydrodynamicKccInputDTO input = Inputs.IsCreated && index < Inputs.Length ? Inputs[index] : default;
            KccEnvironmentProfileDTO environmentProfile = ResolveEnvironmentProfile();
            KccEnvironmentGridDTO environmentGrid = ResolveEnvironmentGrid();

            uint externalFlags = 0u;
            uint controlFlags = index == 0 ? ExternalControlFlags : 0u;
            if ((controlFlags & HydrodynamicKccMath.FlagExternalPositionTarget) != 0u)
            {
                double3 externalPositionTarget = HydrodynamicKccMath.Sanitize(ExternalPositionTargetAup, state.AUP_Position);
                state.AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(externalPositionTarget);
                externalFlags |= HydrodynamicKccMath.FlagExternalPositionTarget;
            }

            if ((controlFlags & HydrodynamicKccMath.FlagExternalRotationTarget) != 0u)
                externalFlags |= HydrodynamicKccMath.FlagExternalRotationTarget;

            float3 velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
            float3 lateExternalVelocityChange = float3.zero;
            float3 lateExternalVelocityTarget = velocity;
            bool hasLateVelocityChange = false;
            bool hasLateVelocityTarget = false;
            if (index == 0)
            {
                float3 externalAcceleration = HydrodynamicKccMath.Sanitize(ExternalAcceleration, float3.zero);
                float3 externalVelocityChange = HydrodynamicKccMath.Sanitize(ExternalVelocityChange, float3.zero);
                float3 externalVelocityTarget = HydrodynamicKccMath.Sanitize(ExternalVelocityTarget, velocity);
                if ((controlFlags & HydrodynamicKccMath.FlagExternalAcceleration) != 0u)
                {
                    velocity += externalAcceleration * dt;
                    externalFlags |= HydrodynamicKccMath.FlagExternalAcceleration;
                }

                if ((controlFlags & HydrodynamicKccMath.FlagExternalVelocityChange) != 0u)
                {
                    lateExternalVelocityChange = externalVelocityChange;
                    hasLateVelocityChange = true;
                    externalFlags |= HydrodynamicKccMath.FlagExternalVelocityChange;
                }

                if ((controlFlags & HydrodynamicKccMath.FlagExternalVelocityTarget) != 0u)
                {
                    lateExternalVelocityTarget = externalVelocityTarget;
                    hasLateVelocityTarget = true;
                    externalFlags |= HydrodynamicKccMath.FlagExternalVelocityTarget;
                }
            }

            float3 moveAxis = HydrodynamicKccMath.Sanitize(input.MoveAxis, float3.zero);
            float moveLenSq = math.lengthsq(moveAxis);
            float3 moveDir = moveLenSq > 1f ? moveAxis * math.rsqrt(math.max(moveLenSq, 0.000001f)) : moveAxis;
            float maxSpeed = math.max(0.1f, math.isfinite(Tuning.MaxSpeed) ? Tuning.MaxSpeed : 6f);
            float mass = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(state.Mass) ? state.Mass : 80f);
            float stateDrag = math.max(0f, math.isfinite(state.DragCoefficient) ? state.DragCoefficient : 0f);
            float radius = math.max(0.05f, math.isfinite(Tuning.CapsuleRadius) ? Tuning.CapsuleRadius : 0.35f);
            float height = math.max(radius * 2f, math.isfinite(Tuning.CapsuleHeight) ? Tuning.CapsuleHeight : 1.8f);
            float fluidDensity = math.max(0f, math.isfinite(Tuning.FluidDensity) ? Tuning.FluidDensity : 1f);
            float addedMass = fluidDensity * radius * radius * height * math.lerp(0.08f, 0.22f, quality);
            float safeMass = math.max(HydrodynamicKccMath.MinDenominator, mass + addedMass);
            float exhaustionPenalty = ResolveExhaustionPenalty(index, environmentProfile);
            float acceleration = math.lerp(maxSpeed * 2.2f, maxSpeed * 5.4f, quality) * mass * math.rcp(safeMass);
            acceleration *= 1f - exhaustionPenalty;
            velocity += moveDir * acceleration * dt;

            float3 localPosition = HydrodynamicKccMath.ResolveLocalFloat3(state.AUP_Position, SectorOriginAup);
            uint sampleMode;
            float3 sampledFlow = SampleFlow(state.AUP_Position, environmentGrid, quality, out sampleMode);
            float3 appliedFlow = sampledFlow * math.max(0f, math.isfinite(environmentProfile.CurrentAdvectionScalar) ? environmentProfile.CurrentAdvectionScalar : 1f);

            // OceanCurrentDragCalculator result. This is a FORCE, so it is summed into velocity here --
            // before the drag solve at dragDenominator below. Applying it after integration would not be
            // drag: it would be a post-hoc velocity edit that never passes through the resistive solve or
            // the max-speed clamp.
            // Scoped to index 0 for the same reason ExternalAcceleration above is: these three terms are
            // single job fields derived from entity 0's depth and velocity on the main thread. Entity N sits
            // at a different depth in a different part of the flow field, so reusing entity 0's buoyancy or
            // thermocline resistance for it would be wrong. Capacity is 1 for the player controller.
            bool mediumOwner = index == 0;
            uint mediumAppliedFlags = 0u;
            float3 mediumCurrentDragForce = HydrodynamicKccMath.Sanitize(MediumCurrentDragForce, float3.zero);
            if (mediumOwner &&
                (MediumFlags & HydrodynamicKccMath.FlagMediumCurrentDrag) != 0u &&
                math.lengthsq(mediumCurrentDragForce) > 0.000001f)
            {
                float3 mediumCurrentDragAcceleration = mediumCurrentDragForce * math.rcp(safeMass);
                if (HydrodynamicKccMath.IsFinite(mediumCurrentDragAcceleration))
                {
                    velocity += mediumCurrentDragAcceleration * dt;
                    mediumAppliedFlags |= HydrodynamicKccMath.FlagMediumCurrentDrag;
                }
            }

            // UNIT LAW FOR THE FLOW FIELD -- ONE COUPLING, NOT TWO.
            // FlowField holds a water VELOCITY in m/s, not an acceleration. Three independent facts fix that:
            // GenerateMockEnvironmentalForcesJob builds the magnitude as `speed = lerp(0.2, 1.9, quality)`;
            // the drag path's declared fallback source is IHectonOceanKinematics.TrySampleWaterVelocity,
            // documented as "one surface-velocity vector"; and IWeatherService.GlobalCurrentVector, the
            // project's other current source, is likewise m/s. So `velocity += appliedFlow * dt` reads a m/s
            // field as m/s^2, and once OceanCurrentDragCalculator feeds the SAME cell value in as
            // oceanCurrentVelocity the current couples into the body twice over.
            // Drag against the relative velocity IS the advection mechanism: it drives the body toward the
            // water velocity and stops there. So the raw kinematic add survives only as the fallback for
            // when no drag force was applied -- models disabled, flag clear, non-finite force, or a
            // non-owner index whose depth-derived medium terms were never computed.
            if ((mediumAppliedFlags & HydrodynamicKccMath.FlagMediumCurrentDrag) == 0u)
                velocity += appliedFlow * dt;

            float waterSurfaceY = HydrodynamicKccMath.ResolveRuntimeWaterSurfaceY(Tuning.WaterSurfaceY);
            float depth = math.max(0f, waterSurfaceY - localPosition.y);
            float submersion = math.saturate(depth * math.rcp(math.max(0.1f, height)));
            submersion = submersion * submersion * (3f - 2f * submersion);
            float gravity = 9.80665f * math.max(0f, math.isfinite(Tuning.GravityMultiplier) ? Tuning.GravityMultiplier : 1f);
            float buoyancy = (math.max(0f, Tuning.BuoyancyScalar) * submersion * mass - mass) * gravity * math.rcp(safeMass);
            if (mediumOwner &&
                (MediumFlags & HydrodynamicKccMath.FlagMediumDensityBuoyancy) != 0u &&
                math.isfinite(MediumDensityBuoyancyNewtons))
            {
                // BuoyancyDensityRatioMath returns the NET force at full submersion:
                //   net = (fluidDensity - playerDensity) * displacedVolume * gravity = buoyantForce - weight.
                // Weight must stay unconditional or the controller stops falling out of water, so the
                // buoyant half is recovered, scaled by submersion, and weight is re-subtracted.
                float weightNewtons = mass * gravity;
                float buoyantForceNewtons = MediumDensityBuoyancyNewtons + weightNewtons;
                float densityBuoyancy = ((buoyantForceNewtons * submersion) - weightNewtons) * math.rcp(safeMass);
                if (math.isfinite(densityBuoyancy))
                {
                    buoyancy = densityBuoyancy;
                    mediumAppliedFlags |= HydrodynamicKccMath.FlagMediumDensityBuoyancy;
                }
            }
            velocity += new float3(0f, buoyancy * dt, 0f);

            float sdfDistance = SampleSdf(state.AUP_Position, environmentGrid, quality);
            float sdfBand = math.max(0.05f, math.isfinite(environmentGrid.SdfFrictionBandMeters) ? environmentGrid.SdfFrictionBandMeters : 0.65f);
            float sdfFriction = math.saturate((sdfBand - sdfDistance) * math.rcp(sdfBand));
            if (sdfFriction > 0.0001f)
            {
                float friction = sdfFriction *
                                 math.max(0f, math.isfinite(environmentProfile.FrictionCoefficient) ? environmentProfile.FrictionCoefficient : 0.85f) *
                                 math.lerp(0.75f, 2.2f, quality);
                float lateralScale = math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + friction * dt));
                velocity.x *= lateralScale;
                velocity.z *= lateralScale;
            }

            float3 preSlopeSlide = float3.zero;
            float preSlopeAngle = 0f;
            double3 footAup = state.AUP_Position - new double3(0d, (double)math.max(0f, (height * 0.5f) - radius), 0d);
            float footSdfDistance = SampleSdf(footAup, environmentGrid, quality);
            float slopeContact = math.saturate((sdfBand - math.abs(footSdfDistance)) * math.rcp(sdfBand));
            float3 sdfNormal = SampleSdfNormal(footAup, environmentGrid, quality);
            float upDot = math.saturate(math.dot(sdfNormal, new float3(0f, 1f, 0f)));
            preSlopeAngle = math.select(0f, Acos01ApproxDegrees(upDot), slopeContact > 0.0001f);
            float slopeOverLimit = math.saturate((preSlopeAngle - environmentProfile.MaxSlopeAngle) *
                                                 math.rcp(math.max(1f, 89f - environmentProfile.MaxSlopeAngle)));
            float preSlopeWeight = slopeContact * slopeOverLimit;
            if (preSlopeWeight > 0.0001f)
            {
                float3 down = new float3(0f, -1f, 0f);
                float3 gravityVelocity = down * (9.80665f * dt);
                float3 projectedDown = gravityVelocity - sdfNormal * math.dot(gravityVelocity, sdfNormal);
                float friction = math.max(0f, environmentProfile.FrictionCoefficient) * math.lerp(0.45f, 1.35f, quality);
                float slideScale = preSlopeWeight *
                                   math.lerp(0.55f, 1.15f, quality) *
                                   math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + friction * dt));
                float intoNormal = math.dot(velocity, sdfNormal);
                velocity -= sdfNormal * math.min(0f, intoNormal) * preSlopeWeight;
                preSlopeSlide = projectedDown * slideScale;
                velocity += preSlopeSlide;
            }

            float speedBeforeDrag = HydrodynamicKccMath.LengthSafe(velocity);
            float baseDrag = math.max(0f, math.isfinite(Tuning.BaseDrag) ? Tuning.BaseDrag : 0.18f);
            float metabolicDrag = exhaustionPenalty * math.lerp(0.35f, 1.4f, quality);
            float drag = ((stateDrag + baseDrag) * math.lerp(0.35f, 1.15f, quality)) + metabolicDrag;
            // ThermoclineResistanceCalculator result. Resistance belongs in the SAME implicit denominator as
            // base drag so the two resistive terms compose in one solve rather than stacking two multiplies.
            float mediumThermoclineResistance01 = mediumOwner &&
                                                  (MediumFlags & HydrodynamicKccMath.FlagMediumThermocline) != 0u &&
                                                  math.isfinite(MediumThermoclineResistance01)
                ? math.saturate(MediumThermoclineResistance01)
                : 0f;
            float thermoclineDragRate = mediumThermoclineResistance01 * HydrodynamicKccMath.ThermoclineDragRatePerSecond;
            if (thermoclineDragRate > 0.0001f)
                mediumAppliedFlags |= HydrodynamicKccMath.FlagMediumThermocline;
            float dragDenominator = math.max(
                HydrodynamicKccMath.MinDenominator,
                1f + (drag * speedBeforeDrag * dt) + (thermoclineDragRate * dt));
            velocity *= math.rcp(dragDenominator);

            float speedSq = math.lengthsq(velocity);
            if (speedSq > maxSpeed * maxSpeed)
                velocity *= maxSpeed * math.rsqrt(math.max(speedSq, 0.000001f));

            if (hasLateVelocityChange)
                velocity = HydrodynamicKccMath.Sanitize(velocity + lateExternalVelocityChange, velocity);
            if (hasLateVelocityTarget)
                velocity = lateExternalVelocityTarget;

            bool invalid = !HydrodynamicKccMath.IsFinite(state.AUP_Position) || !HydrodynamicKccMath.IsFinite(velocity);
            if (invalid)
            {
                velocity = float3.zero;
                state.AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(HydrodynamicKccMath.Sanitize(state.AUP_Position, SectorOriginAup));
                WriteFault(index, HydrodynamicKccMath.FlagFaultNaN);
            }

            float speed = HydrodynamicKccMath.LengthSafe(velocity);
            float normalizedSpeed = speed * math.rcp(math.max(0.1f, maxSpeed));
            float turbulence = math.saturate(normalizedSpeed * normalizedSpeed) * math.lerp(0.18f, 1f, visualQuality);
            float wakeRadius = math.lerp(1.2f, 3.6f, math.saturate(turbulence * math.lerp(0.72f, 1.18f, visualQuality)));
            uint flags = HydrodynamicKccMath.ExtractInputFlags(input.Flags) |
                         externalFlags |
                         HydrodynamicKccMath.FlagEnvironmentalForces |
                          math.select(0u, HydrodynamicKccMath.FlagFaultNaN, invalid) |
                          math.select(0u, HydrodynamicKccMath.FlagMetabolicPenalty, exhaustionPenalty > 0.0001f) |
                          math.select(0u, HydrodynamicKccMath.FlagSdfFriction, sdfFriction > 0.0001f) |
                          math.select(0u, HydrodynamicKccMath.FlagSlopeSlide, preSlopeWeight > 0.0001f) |
                          math.select(0u, HydrodynamicKccMath.FlagEnvironmentMock, IsMockEnvironment(environmentGrid)) |
                          math.select(0u, HydrodynamicKccMath.FlagTrilinearFlowSample, sampleMode != 0u) |
                          mediumAppliedFlags;
            uint wakeFlags = math.select(0u, HydrodynamicKccMath.FlagWake, speed > math.max(0.01f, Tuning.WakeThreshold));

            state.Velocity = velocity;
            state.AngularVelocity = HydrodynamicKccMath.Sanitize(state.AngularVelocity, float3.zero) *
                                    math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + drag * dt));
            state.Mass = mass;
            state.DragCoefficient = stateDrag;
            ProposedVelocities[index] = velocity;
            if (WakePackets.IsCreated && index < WakePackets.Length)
            {
                WakePackets[index] = new HydrodynamicWakePacketDTO
                {
                    AupPosition = state.AUP_Position,
                    Velocity = velocity,
                    TurbulenceScalar = turbulence,
                    WakeRadius = wakeRadius,
                    WakeMagnitude = speed,
                    Frame = SimulationFrame,
                    SourceHash = HydrodynamicKccMath.SourceHash,
                    Flags = wakeFlags | flags
                };
            }

            if (EnvironmentDebugOutputs.IsCreated && index < EnvironmentDebugOutputs.Length)
            {
                EnvironmentDebugOutputs[index] = new KccEnvironmentDebugOutputDTO
                {
                    AppliedFlow = appliedFlow,
                    SlopeSlideVector = preSlopeSlide,
                    ExhaustionPenalty = exhaustionPenalty,
                    SdfFriction = sdfFriction,
                    SlopeAngleDegrees = preSlopeAngle,
                    ComputeMicroseconds = HydrodynamicKccMath.EstimateEnvironmentMicroseconds(quality, sampleMode != 0u, preSlopeWeight > 0.0001f, HydrodynamicKccMath.LengthSafe(appliedFlow)),
                    Frame = SimulationFrame,
                    Flags = flags
                };
            }
        }

        private KccEnvironmentProfileDTO ResolveEnvironmentProfile()
        {
            KccEnvironmentProfileDTO profile = EnvironmentProfiles.IsCreated && EnvironmentProfiles.Length > 0
                ? EnvironmentProfiles[0]
                : DefaultEnvironmentProfile();
            return SanitizeEnvironmentProfile(profile);
        }

        private KccEnvironmentGridDTO ResolveEnvironmentGrid()
        {
            KccEnvironmentGridDTO grid = EnvironmentGrids.IsCreated && EnvironmentGrids.Length > 0
                ? EnvironmentGrids[0]
                : default;

            if (!HydrodynamicKccMath.IsFinite(grid.GridOriginAup))
                grid.GridOriginAup = SectorOriginAup;
            grid.Dimensions = new int3(
                math.max(1, grid.Dimensions.x),
                math.max(1, grid.Dimensions.y),
                math.max(1, grid.Dimensions.z));
            grid.CellSizeMeters = math.max(0.25f, math.isfinite(grid.CellSizeMeters) ? grid.CellSizeMeters : 2f);
            grid.SdfSurfaceMeters = math.isfinite(grid.SdfSurfaceMeters) ? grid.SdfSurfaceMeters : 0f;
            grid.SdfFrictionBandMeters = math.max(0.05f, math.isfinite(grid.SdfFrictionBandMeters) ? grid.SdfFrictionBandMeters : 0.65f);
            return grid;
        }

        private float ResolveExhaustionPenalty(int index, KccEnvironmentProfileDTO profile)
        {
            if (!MetabolismStates.IsCreated || index >= MetabolismStates.Length)
                return 0f;

            MetabolicStateDTO metabolism = MetabolismStates[index];
            float calories01 = NormalizeMetabolicReservoir01(metabolism.Calories);
            float hydration01 = NormalizeMetabolicReservoir01(metabolism.Hydration);
            float toxicity01 = math.saturate((math.isfinite(metabolism.Toxicity) ? metabolism.Toxicity : 0f) * 0.125f);
            float starvation = 1f - calories01;
            float dehydration = 1f - hydration01;
            float fatigueScalar = metabolism.Fatigue01;
            fatigueScalar = math.saturate(math.isfinite(fatigueScalar) ? fatigueScalar : 0f);
            float fatigue = math.max(fatigueScalar, math.select(0f, 0.35f, (metabolism.Flags & ShinobuMetabolismVaultContract.FlagFatigue) != 0u));
            starvation = math.max(starvation, math.select(0f, 0.75f, (metabolism.Flags & ShinobuMetabolismVaultContract.FlagStarving) != 0u));
            dehydration = math.max(dehydration, math.select(0f, 0.55f, (metabolism.Flags & ShinobuMetabolismVaultContract.FlagDehydrated) != 0u));
            toxicity01 = math.max(toxicity01, math.select(0f, 0.35f, (metabolism.Flags & ShinobuMetabolismVaultContract.FlagToxic) != 0u));
            float raw = math.max(math.max(starvation, dehydration), math.max(toxicity01, fatigue));
            return math.saturate(raw * math.max(0f, profile.ExhaustionPenaltyMax));
        }

        private static float NormalizeMetabolicReservoir01(float value)
        {
            float finite = math.max(0f, math.isfinite(value) ? value : 1f);
            return finite > 1f ? math.saturate(finite * 0.01f) : math.saturate(finite);
        }

        private float3 SampleFlow(double3 aup, KccEnvironmentGridDTO grid, float quality, out uint sampleMode)
        {
            sampleMode = 0u;
            if (!FlowField.IsCreated || FlowField.Length == 0)
                return float3.zero;

            int3 dimensions = ResolveSampleDimensions(grid.Dimensions, FlowField.Length);
            float3 cell = ResolveGridCell(aup, grid, dimensions);
            int3 nearestCoord = new int3(
                (int)math.round(cell.x),
                (int)math.round(cell.y),
                (int)math.round(cell.z));
            float3 nearest = SampleFlowAt(ClampCell(nearestCoord, dimensions), dimensions);
            int3 baseCoord = new int3(
                (int)math.floor(cell.x),
                (int)math.floor(cell.y),
                (int)math.floor(cell.z));
            float3 frac = math.saturate(cell - new float3(baseCoord.x, baseCoord.y, baseCoord.z));
            baseCoord = ClampCell(baseCoord, dimensions);
            int3 nextCoord = ClampCell(baseCoord + new int3(1, 1, 1), dimensions);

            float3 c000 = SampleFlowAt(new int3(baseCoord.x, baseCoord.y, baseCoord.z), dimensions);
            float3 c100 = SampleFlowAt(new int3(nextCoord.x, baseCoord.y, baseCoord.z), dimensions);
            float3 c010 = SampleFlowAt(new int3(baseCoord.x, nextCoord.y, baseCoord.z), dimensions);
            float3 c110 = SampleFlowAt(new int3(nextCoord.x, nextCoord.y, baseCoord.z), dimensions);
            float3 c001 = SampleFlowAt(new int3(baseCoord.x, baseCoord.y, nextCoord.z), dimensions);
            float3 c101 = SampleFlowAt(new int3(nextCoord.x, baseCoord.y, nextCoord.z), dimensions);
            float3 c011 = SampleFlowAt(new int3(baseCoord.x, nextCoord.y, nextCoord.z), dimensions);
            float3 c111 = SampleFlowAt(new int3(nextCoord.x, nextCoord.y, nextCoord.z), dimensions);
            float3 c00 = math.lerp(c000, c100, frac.x);
            float3 c10 = math.lerp(c010, c110, frac.x);
            float3 c01 = math.lerp(c001, c101, frac.x);
            float3 c11 = math.lerp(c011, c111, frac.x);
            float3 c0 = math.lerp(c00, c10, frac.y);
            float3 c1 = math.lerp(c01, c11, frac.y);
            float3 trilinear = math.lerp(c0, c1, frac.z);
            float blend = math.saturate(quality);
            blend = blend * blend * (3f - 2f * blend);
            sampleMode = (uint)math.round(blend * 65535f);
            return HydrodynamicKccMath.Sanitize(math.lerp(nearest, trilinear, blend), float3.zero);
        }

        private float SampleSdf(double3 aup, KccEnvironmentGridDTO grid, float quality)
        {
            if (!SdfDistances.IsCreated || SdfDistances.Length == 0)
                return grid.SdfFrictionBandMeters * 2f;

            int3 dimensions = ResolveSampleDimensions(grid.Dimensions, SdfDistances.Length);
            float3 cell = ResolveGridCell(aup, grid, dimensions);
            int3 nearestCoord = new int3(
                (int)math.round(cell.x),
                (int)math.round(cell.y),
                (int)math.round(cell.z));
            float nearest = SampleSdfAt(ClampCell(nearestCoord, dimensions), dimensions);
            int3 baseCoord = new int3(
                (int)math.floor(cell.x),
                (int)math.floor(cell.y),
                (int)math.floor(cell.z));
            float3 frac = math.saturate(cell - new float3(baseCoord.x, baseCoord.y, baseCoord.z));
            baseCoord = ClampCell(baseCoord, dimensions);
            int3 nextCoord = ClampCell(baseCoord + new int3(1, 1, 1), dimensions);

            float c000 = SampleSdfAt(new int3(baseCoord.x, baseCoord.y, baseCoord.z), dimensions);
            float c100 = SampleSdfAt(new int3(nextCoord.x, baseCoord.y, baseCoord.z), dimensions);
            float c010 = SampleSdfAt(new int3(baseCoord.x, nextCoord.y, baseCoord.z), dimensions);
            float c110 = SampleSdfAt(new int3(nextCoord.x, nextCoord.y, baseCoord.z), dimensions);
            float c001 = SampleSdfAt(new int3(baseCoord.x, baseCoord.y, nextCoord.z), dimensions);
            float c101 = SampleSdfAt(new int3(nextCoord.x, baseCoord.y, nextCoord.z), dimensions);
            float c011 = SampleSdfAt(new int3(baseCoord.x, nextCoord.y, nextCoord.z), dimensions);
            float c111 = SampleSdfAt(new int3(nextCoord.x, nextCoord.y, nextCoord.z), dimensions);
            float c00 = math.lerp(c000, c100, frac.x);
            float c10 = math.lerp(c010, c110, frac.x);
            float c01 = math.lerp(c001, c101, frac.x);
            float c11 = math.lerp(c011, c111, frac.x);
            float c0 = math.lerp(c00, c10, frac.y);
            float c1 = math.lerp(c01, c11, frac.y);
            float trilinear = math.lerp(c0, c1, frac.z);
            float blend = math.saturate(quality);
            blend = blend * blend * (3f - 2f * blend);
            float sampled = math.lerp(nearest, trilinear, blend) - grid.SdfSurfaceMeters;
            return math.isfinite(sampled) ? sampled : grid.SdfFrictionBandMeters * 2f;
        }

        private float3 SampleSdfNormal(double3 aup, KccEnvironmentGridDTO grid, float quality)
        {
            if (!SdfDistances.IsCreated || SdfDistances.Length == 0)
                return new float3(0f, 1f, 0f);

            int3 dimensions = ResolveSampleDimensions(grid.Dimensions, SdfDistances.Length);
            float3 cell = ResolveGridCell(aup, grid, dimensions);
            int3 center = ClampCell(new int3(
                (int)math.round(cell.x),
                (int)math.round(cell.y),
                (int)math.round(cell.z)), dimensions);

            float dx = SampleSdfAt(ClampCell(center + new int3(1, 0, 0), dimensions), dimensions) -
                       SampleSdfAt(ClampCell(center - new int3(1, 0, 0), dimensions), dimensions);
            float dy = SampleSdfAt(ClampCell(center + new int3(0, 1, 0), dimensions), dimensions) -
                       SampleSdfAt(ClampCell(center - new int3(0, 1, 0), dimensions), dimensions);
            float dz = SampleSdfAt(ClampCell(center + new int3(0, 0, 1), dimensions), dimensions) -
                       SampleSdfAt(ClampCell(center - new int3(0, 0, 1), dimensions), dimensions);

            float invCellSpan = math.rcp(math.max(HydrodynamicKccMath.MinDenominator, grid.CellSizeMeters * 2f));
            float3 gradientNormal = HydrodynamicKccMath.NormalizeSafe(new float3(dx, dy, dz) * invCellSpan, new float3(0f, 1f, 0f));
            float gradientBlend = math.saturate(quality);
            gradientBlend = gradientBlend * gradientBlend * (3f - 2f * gradientBlend);
            return HydrodynamicKccMath.NormalizeSafe(math.lerp(new float3(0f, 1f, 0f), gradientNormal, gradientBlend), new float3(0f, 1f, 0f));
        }

        private float3 ResolveGridCell(double3 aup, KccEnvironmentGridDTO grid, int3 dimensions)
        {
            double3 delta = HydrodynamicKccMath.Sanitize(aup - grid.GridOriginAup, double3.zero);
            float invCell = math.rcp(math.max(0.25f, grid.CellSizeMeters));
            float3 cell = new float3((float)delta.x, (float)delta.y, (float)delta.z) * invCell;
            float3 maxCell = new float3(dimensions.x - 1, dimensions.y - 1, dimensions.z - 1);
            return math.clamp(HydrodynamicKccMath.Sanitize(cell, float3.zero), float3.zero, maxCell);
        }

        private float3 SampleFlowAt(int3 coord, int3 dimensions)
        {
            int sampleIndex = FlattenCell(coord, dimensions);
            return sampleIndex >= 0 && sampleIndex < FlowField.Length
                ? HydrodynamicKccMath.Sanitize(FlowField[sampleIndex], float3.zero)
                : float3.zero;
        }

        private float SampleSdfAt(int3 coord, int3 dimensions)
        {
            int sampleIndex = FlattenCell(coord, dimensions);
            float value = sampleIndex >= 0 && sampleIndex < SdfDistances.Length
                ? SdfDistances[sampleIndex]
                : 2f;
            return math.isfinite(value) ? value : 2f;
        }

        private static int FlattenCell(int3 coord, int3 dimensions)
        {
            int x = math.clamp(coord.x, 0, math.max(0, dimensions.x - 1));
            int y = math.clamp(coord.y, 0, math.max(0, dimensions.y - 1));
            int z = math.clamp(coord.z, 0, math.max(0, dimensions.z - 1));
            return x + z * math.max(1, dimensions.x) + y * math.max(1, dimensions.x) * math.max(1, dimensions.z);
        }

        private static int3 ClampCell(int3 coord, int3 dimensions)
        {
            return new int3(
                math.clamp(coord.x, 0, math.max(0, dimensions.x - 1)),
                math.clamp(coord.y, 0, math.max(0, dimensions.y - 1)),
                math.clamp(coord.z, 0, math.max(0, dimensions.z - 1)));
        }

        private static int3 ResolveSampleDimensions(int3 requested, int length)
        {
            int3 dimensions = new int3(math.max(1, requested.x), math.max(1, requested.y), math.max(1, requested.z));
            int requestedLength = dimensions.x * dimensions.y * dimensions.z;
            if (requestedLength > 0 && requestedLength <= length)
                return dimensions;

            int side = IntegerCubeRootFloor(length);
            return new int3(side, side, math.max(1, length / math.max(1, side * side)));
        }

        private static int IntegerCubeRootFloor(int length)
        {
            int safeLength = math.max(1, length);
            int low = 1;
            int high = math.min(1290, safeLength);
            int result = 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                long cube = (long)mid * mid * mid;
                if (cube <= safeLength)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return math.max(1, result);
        }

        private static KccEnvironmentProfileDTO DefaultEnvironmentProfile()
        {
            return new KccEnvironmentProfileDTO
            {
                MaxSlopeAngle = 48f,
                CurrentAdvectionScalar = 1f,
                FrictionCoefficient = 0.85f,
                ExhaustionPenaltyMax = 0.35f
            };
        }

        private static KccEnvironmentProfileDTO SanitizeEnvironmentProfile(KccEnvironmentProfileDTO profile)
        {
            profile.MaxSlopeAngle = math.clamp(math.isfinite(profile.MaxSlopeAngle) ? profile.MaxSlopeAngle : 48f, 1f, 89f);
            profile.CurrentAdvectionScalar = math.clamp(math.isfinite(profile.CurrentAdvectionScalar) ? profile.CurrentAdvectionScalar : 1f, 0f, 8f);
            profile.FrictionCoefficient = math.clamp(math.isfinite(profile.FrictionCoefficient) ? profile.FrictionCoefficient : 0.85f, 0f, 8f);
            profile.ExhaustionPenaltyMax = math.saturate(math.isfinite(profile.ExhaustionPenaltyMax) ? profile.ExhaustionPenaltyMax : 0.35f);
            return profile;
        }

        private static bool IsMockEnvironment(KccEnvironmentGridDTO grid)
        {
            return (grid.Flags & HydrodynamicKccMath.FlagEnvironmentMock) != 0u;
        }

        private void WriteFault(int index, uint faultMask)
        {
            if (!FaultFlags.IsCreated || index >= FaultFlags.Length)
                return;

            HydrodynamicKccFaultFlagDTO entry = FaultFlags[index];
            entry.FaultMask |= (int)faultMask;
            FaultFlags[index] = entry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Acos01ApproxDegrees(float x)
        {
            x = math.saturate(x);
            float oneMinusX = math.max(0f, 1f - x);
            float root = oneMinusX * math.rsqrt(math.max(oneMinusX, 0.000001f));
            float radians = (((-0.0187293f * x + 0.0742610f) * x - 0.2121144f) * x + 1.5707288f) * root;
            return radians * 57.2957795131f;
        }

    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildSdfCollisionHitsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<float3> ProposedVelocities;
        [ReadOnly, NoAlias] public NativeArray<KccEnvironmentGridDTO> EnvironmentGrids;
        [ReadOnly, NoAlias] public NativeArray<float> SdfDistances;
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccCollisionHitDTO> Hits;
        public HydrodynamicKccTuningDTO Tuning;
        public double3 SectorOriginAup;
        public float SimulationTickDelta;
        public int MaxHitsPerEntity;

        public void Execute(int index)
        {
            int stride = math.clamp(MaxHitsPerEntity, 1, 8);
            int hitBase = index * stride;
            for (int i = 0; i < stride; i++)
            {
                int clearIndex = hitBase + i;
                if (Hits.IsCreated && clearIndex < Hits.Length)
                    Hits[clearIndex] = default;
            }

            if (!States.IsCreated ||
                !ProposedVelocities.IsCreated ||
                !Hits.IsCreated ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)ProposedVelocities.Length ||
                hitBase >= Hits.Length ||
                !SdfDistances.IsCreated ||
                SdfDistances.Length == 0)
            {
                return;
            }

            KccEnvironmentGridDTO grid = ResolveEnvironmentGrid();
            int3 dimensions = ResolveSampleDimensions(grid.Dimensions, SdfDistances.Length);
            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                return;

            KinematicStateDTO state = States[index];
            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float quality = HydrodynamicKccMath.ResolveQuality01(Tuning.GlobalQualityWeight);
            float3 velocity = HydrodynamicKccMath.Sanitize(ProposedVelocities[index], float3.zero);
            float3 delta = velocity * dt;
            double3 deltaAup = new double3(delta.x, delta.y, delta.z);
            float castDistance = HydrodynamicKccMath.LengthSafe(delta);
            float radius = math.max(0.05f, math.isfinite(Tuning.CapsuleRadius) ? Tuning.CapsuleRadius : 0.35f);
            float skin = math.max(0.001f, math.isfinite(Tuning.SkinWidth) ? Tuning.SkinWidth : 0.02f);
            float height = math.max(radius * 2f, math.isfinite(Tuning.CapsuleHeight) ? Tuning.CapsuleHeight : 1.8f);
            float halfSegment = math.max(0f, (height * 0.5f) - radius);
            int sampleSteps = HydrodynamicKccMath.ResolveSpeculativeSampleCount(
                quality,
                castDistance,
                radius,
                skin,
                stride);
            int writeCount = 0;

            for (int step = 0; step < sampleSteps && writeCount < stride; step++)
            {
                float t = castDistance > HydrodynamicKccMath.MinDenominator
                    ? ((float)(step + 1) * math.rcp(math.max(1f, sampleSteps)))
                    : 0f;
                double3 centerAup = state.AUP_Position + deltaAup * (double)t;

                for (int probe = 0; probe < 3 && writeCount < stride; probe++)
                {
                    float yOffset = probe == 0 ? -halfSegment : (probe == 1 ? 0f : halfSegment);
                    double3 probeAup = centerAup + new double3(0d, (double)yOffset, 0d);
                    if (!TrySampleSdf(probeAup, grid, dimensions, quality, out float sdfDistance))
                        continue;

                    float penetration = (radius + skin) - sdfDistance;
                    if (penetration <= 0f)
                        continue;

                    if (!TrySampleSdfNormal(probeAup, grid, dimensions, out float3 normal))
                        normal = new float3(0f, 1f, 0f);

                    float3 safeNormal = HydrodynamicKccMath.NormalizeSafe(normal, new float3(0f, 1f, 0f));
                    float3 probeLocal = HydrodynamicKccMath.ResolveLocalFloat3(probeAup, SectorOriginAup);
                    float3 surfacePoint = probeLocal - safeNormal * sdfDistance;
                    float hitDistance = math.max(0f, castDistance * t - penetration);
                    uint flags = HydrodynamicKccMath.HitFlagValid |
                                 HydrodynamicKccMath.HitFlagSdfSpeculative |
                                 math.select(0u, HydrodynamicKccMath.HitFlagPenetrating, penetration > skin);
                    Hits[hitBase + writeCount] = new HydrodynamicKccCollisionHitDTO
                    {
                        Point = HydrodynamicKccMath.Sanitize(surfacePoint, float3.zero),
                        Distance = math.isfinite(hitDistance) ? hitDistance : 0f,
                        Normal = safeNormal,
                        Flags = flags,
                        PenetrationDepth = math.max(0f, math.isfinite(penetration) ? penetration : 0f),
                        SampleIndex = (uint)(step * 3 + probe)
                    };
                    writeCount++;
                }
            }
        }

        private KccEnvironmentGridDTO ResolveEnvironmentGrid()
        {
            KccEnvironmentGridDTO grid = EnvironmentGrids.IsCreated && EnvironmentGrids.Length > 0
                ? EnvironmentGrids[0]
                : default;

            if (!HydrodynamicKccMath.IsFinite(grid.GridOriginAup))
                grid.GridOriginAup = SectorOriginAup;
            grid.Dimensions = new int3(
                math.max(1, grid.Dimensions.x),
                math.max(1, grid.Dimensions.y),
                math.max(1, grid.Dimensions.z));
            grid.CellSizeMeters = math.max(0.25f, math.isfinite(grid.CellSizeMeters) ? grid.CellSizeMeters : 2f);
            grid.SdfSurfaceMeters = math.isfinite(grid.SdfSurfaceMeters) ? grid.SdfSurfaceMeters : 0f;
            grid.SdfFrictionBandMeters = math.max(0.05f, math.isfinite(grid.SdfFrictionBandMeters) ? grid.SdfFrictionBandMeters : 0.65f);
            return grid;
        }

        private bool TrySampleSdf(double3 aup, KccEnvironmentGridDTO grid, int3 dimensions, float quality, out float distance)
        {
            distance = grid.SdfFrictionBandMeters * 2f;
            if (!SdfDistances.IsCreated || SdfDistances.Length == 0)
                return false;

            float3 cell = ResolveGridCell(aup, grid, dimensions);
            int3 nearestCoord = new int3(
                (int)math.round(cell.x),
                (int)math.round(cell.y),
                (int)math.round(cell.z));
            float nearest = SampleSdfAt(ClampCell(nearestCoord, dimensions), dimensions);
            int3 baseCoord = new int3(
                (int)math.floor(cell.x),
                (int)math.floor(cell.y),
                (int)math.floor(cell.z));
            float3 frac = math.saturate(cell - new float3(baseCoord.x, baseCoord.y, baseCoord.z));
            baseCoord = ClampCell(baseCoord, dimensions);
            int3 nextCoord = ClampCell(baseCoord + new int3(1, 1, 1), dimensions);

            float c000 = SampleSdfAt(new int3(baseCoord.x, baseCoord.y, baseCoord.z), dimensions);
            float c100 = SampleSdfAt(new int3(nextCoord.x, baseCoord.y, baseCoord.z), dimensions);
            float c010 = SampleSdfAt(new int3(baseCoord.x, nextCoord.y, baseCoord.z), dimensions);
            float c110 = SampleSdfAt(new int3(nextCoord.x, nextCoord.y, baseCoord.z), dimensions);
            float c001 = SampleSdfAt(new int3(baseCoord.x, baseCoord.y, nextCoord.z), dimensions);
            float c101 = SampleSdfAt(new int3(nextCoord.x, baseCoord.y, nextCoord.z), dimensions);
            float c011 = SampleSdfAt(new int3(baseCoord.x, nextCoord.y, nextCoord.z), dimensions);
            float c111 = SampleSdfAt(new int3(nextCoord.x, nextCoord.y, nextCoord.z), dimensions);
            float c00 = math.lerp(c000, c100, frac.x);
            float c10 = math.lerp(c010, c110, frac.x);
            float c01 = math.lerp(c001, c101, frac.x);
            float c11 = math.lerp(c011, c111, frac.x);
            float c0 = math.lerp(c00, c10, frac.y);
            float c1 = math.lerp(c01, c11, frac.y);
            float trilinear = math.lerp(c0, c1, frac.z);
            float blend = math.saturate(quality);
            blend = blend * blend * (3f - 2f * blend);
            distance = math.lerp(nearest, trilinear, blend) - grid.SdfSurfaceMeters;
            return math.isfinite(distance);
        }

        private bool TrySampleSdfNormal(double3 aup, KccEnvironmentGridDTO grid, int3 dimensions, out float3 normal)
        {
            normal = new float3(0f, 1f, 0f);
            if (!SdfDistances.IsCreated || SdfDistances.Length == 0)
                return false;

            float3 cell = ResolveGridCell(aup, grid, dimensions);
            int3 center = ClampCell(new int3(
                (int)math.round(cell.x),
                (int)math.round(cell.y),
                (int)math.round(cell.z)), dimensions);

            float dx = SampleSdfAt(ClampCell(center + new int3(1, 0, 0), dimensions), dimensions) -
                       SampleSdfAt(ClampCell(center - new int3(1, 0, 0), dimensions), dimensions);
            float dy = SampleSdfAt(ClampCell(center + new int3(0, 1, 0), dimensions), dimensions) -
                       SampleSdfAt(ClampCell(center - new int3(0, 1, 0), dimensions), dimensions);
            float dz = SampleSdfAt(ClampCell(center + new int3(0, 0, 1), dimensions), dimensions) -
                       SampleSdfAt(ClampCell(center - new int3(0, 0, 1), dimensions), dimensions);
            float invCellSpan = math.rcp(math.max(HydrodynamicKccMath.MinDenominator, grid.CellSizeMeters * 2f));
            normal = HydrodynamicKccMath.NormalizeSafe(new float3(dx, dy, dz) * invCellSpan, new float3(0f, 1f, 0f));
            return HydrodynamicKccMath.IsFinite(normal) && math.lengthsq(normal) > 0.0001f;
        }

        private static float3 ResolveGridCell(double3 aup, KccEnvironmentGridDTO grid, int3 dimensions)
        {
            double3 delta = HydrodynamicKccMath.Sanitize(aup - grid.GridOriginAup, double3.zero);
            float invCell = math.rcp(math.max(0.25f, grid.CellSizeMeters));
            float3 cell = new float3((float)delta.x, (float)delta.y, (float)delta.z) * invCell;
            float3 maxCell = new float3(dimensions.x - 1, dimensions.y - 1, dimensions.z - 1);
            return math.clamp(HydrodynamicKccMath.Sanitize(cell, float3.zero), float3.zero, maxCell);
        }

        private float SampleSdfAt(int3 coord, int3 dimensions)
        {
            int sampleIndex = FlattenCell(coord, dimensions);
            float value = sampleIndex >= 0 && sampleIndex < SdfDistances.Length
                ? SdfDistances[sampleIndex]
                : 2f;
            return math.isfinite(value) ? value : 2f;
        }

        private static int FlattenCell(int3 coord, int3 dimensions)
        {
            int x = math.clamp(coord.x, 0, math.max(0, dimensions.x - 1));
            int y = math.clamp(coord.y, 0, math.max(0, dimensions.y - 1));
            int z = math.clamp(coord.z, 0, math.max(0, dimensions.z - 1));
            return x + z * math.max(1, dimensions.x) + y * math.max(1, dimensions.x) * math.max(1, dimensions.z);
        }

        private static int3 ClampCell(int3 coord, int3 dimensions)
        {
            return new int3(
                math.clamp(coord.x, 0, math.max(0, dimensions.x - 1)),
                math.clamp(coord.y, 0, math.max(0, dimensions.y - 1)),
                math.clamp(coord.z, 0, math.max(0, dimensions.z - 1)));
        }

        private static int3 ResolveSampleDimensions(int3 requested, int length)
        {
            int3 dimensions = new int3(math.max(1, requested.x), math.max(1, requested.y), math.max(1, requested.z));
            int requestedLength = dimensions.x * dimensions.y * dimensions.z;
            if (requestedLength > 0 && requestedLength <= length)
                return dimensions;

            int side = IntegerCubeRootFloor(length);
            return new int3(side, side, math.max(1, length / math.max(1, side * side)));
        }

        private static int IntegerCubeRootFloor(int length)
        {
            int safeLength = math.max(1, length);
            int low = 1;
            int high = math.min(1290, safeLength);
            int result = 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                long cube = (long)mid * mid * mid;
                if (cube <= safeLength)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return math.max(1, result);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateSlopeFrictionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> ProposedVelocities;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccCollisionHitDTO> CollisionHits;
        [ReadOnly, NoAlias] public NativeArray<KccEnvironmentProfileDTO> EnvironmentProfiles;
        [NoAlias] public NativeArray<KccEnvironmentDebugOutputDTO> EnvironmentDebugOutputs;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // FaultFlags is a row-matched diagnostic lane owned by this slope pass. Unity cannot prove that each
        // Execute index writes only its own fault row after collision-hit reduction.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A separate fault aggregation job was rejected because it would repeat slope classification and add a
        // tiny job/readback edge. Writing the diagnostic lane beside velocity adjustment keeps the pass fused.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: FaultFlags does not alias ProposedVelocities or EnvironmentDebugOutputs, and no other job
        // mutates it until the scheduler observes this job's output handle.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        public HydrodynamicKccTuningDTO Tuning;
        public uint SimulationFrame;
        public float SimulationTickDelta;
        public int MaxHitsPerCommand;
        public int CollisionBypass;

        public void Execute(int index)
        {
            if (!ProposedVelocities.IsCreated || index >= ProposedVelocities.Length)
                return;

            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float quality = HydrodynamicKccMath.ResolveQuality01(Tuning.GlobalQualityWeight);
            KccEnvironmentProfileDTO profile = EnvironmentProfiles.IsCreated && EnvironmentProfiles.Length > 0
                ? SanitizeEnvironmentProfile(EnvironmentProfiles[0])
                : DefaultEnvironmentProfile();
            float3 velocity = HydrodynamicKccMath.Sanitize(ProposedVelocities[index], float3.zero);
            float3 slideVector = float3.zero;
            float steepestAngle = 0f;
            uint flags = 0u;

            if (CollisionBypass == 0 && CollisionHits.IsCreated && MaxHitsPerCommand > 0)
            {
                int stride = math.clamp(MaxHitsPerCommand, 1, 8);
                int hitBase = index * stride;
                float3 selectedNormal = float3.zero;
                for (int i = 0; i < stride; i++)
                {
                    int hitIndex = hitBase + i;
                    if (hitIndex >= CollisionHits.Length)
                        break;

                    HydrodynamicKccCollisionHitDTO hit = CollisionHits[hitIndex];
                    if ((hit.Flags & HydrodynamicKccMath.HitFlagValid) == 0u)
                        continue;

                    float3 normal = HydrodynamicKccMath.NormalizeSafe(hit.Normal, new float3(0f, 1f, 0f));
                    float upDot = math.saturate(math.dot(normal, new float3(0f, 1f, 0f)));
                    float angle = Acos01ApproxDegrees(upDot);
                    if (angle > steepestAngle)
                    {
                        steepestAngle = angle;
                        selectedNormal = normal;
                    }
                }

                if (steepestAngle > profile.MaxSlopeAngle && math.lengthsq(selectedNormal) > 0.0001f)
                {
                    float3 down = new float3(0f, -1f, 0f);
                    float3 gravityVelocity = down * (9.80665f * dt);
                    float3 projectedDown = gravityVelocity - selectedNormal * math.dot(gravityVelocity, selectedNormal);
                    float3 slideDirection = HydrodynamicKccMath.NormalizeSafe(projectedDown, float3.zero);
                    float overLimit = math.saturate((steepestAngle - profile.MaxSlopeAngle) * math.rcp(math.max(1f, 89f - profile.MaxSlopeAngle)));
                    float friction = math.max(0f, profile.FrictionCoefficient) * math.lerp(0.45f, 1.35f, quality);
                    float slideScale = overLimit * math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + friction * dt));
                    float intoNormal = math.dot(velocity, selectedNormal);
                    velocity -= selectedNormal * math.min(0f, intoNormal);
                    slideVector = projectedDown * slideScale;
                    velocity += slideVector;
                    flags |= HydrodynamicKccMath.FlagSlopeSlide;
                    _ = slideDirection;
                }
            }

            bool invalid = !HydrodynamicKccMath.IsFinite(velocity);
            if (invalid)
            {
                velocity = float3.zero;
                flags |= HydrodynamicKccMath.FlagFaultNaN;
                WriteFault(index, HydrodynamicKccMath.FlagFaultNaN);
            }

            ProposedVelocities[index] = velocity;
            if (EnvironmentDebugOutputs.IsCreated && index < EnvironmentDebugOutputs.Length)
            {
                KccEnvironmentDebugOutputDTO debug = EnvironmentDebugOutputs[index];
                if ((flags & HydrodynamicKccMath.FlagSlopeSlide) != 0u || math.lengthsq(debug.SlopeSlideVector) < 0.000001f)
                    debug.SlopeSlideVector = slideVector;
                debug.SlopeAngleDegrees = math.max(debug.SlopeAngleDegrees, steepestAngle);
                debug.ComputeMicroseconds += HydrodynamicKccMath.EstimateEnvironmentMicroseconds(quality, (debug.Flags & HydrodynamicKccMath.FlagTrilinearFlowSample) != 0u, flags != 0u, HydrodynamicKccMath.LengthSafe(debug.AppliedFlow));
                debug.Frame = SimulationFrame;
                debug.Flags |= flags;
                EnvironmentDebugOutputs[index] = debug;
            }
        }

        private void WriteFault(int index, uint faultMask)
        {
            if (!FaultFlags.IsCreated || index >= FaultFlags.Length)
                return;

            HydrodynamicKccFaultFlagDTO entry = FaultFlags[index];
            entry.FaultMask |= (int)faultMask;
            FaultFlags[index] = entry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Acos01ApproxDegrees(float x)
        {
            x = math.saturate(x);
            float oneMinusX = math.max(0f, 1f - x);
            float root = oneMinusX * math.rsqrt(math.max(oneMinusX, 0.000001f));
            float radians = (((-0.0187293f * x + 0.0742610f) * x - 0.2121144f) * x + 1.5707288f) * root;
            return radians * 57.2957795131f;
        }

        private static KccEnvironmentProfileDTO DefaultEnvironmentProfile()
        {
            return new KccEnvironmentProfileDTO
            {
                MaxSlopeAngle = 48f,
                CurrentAdvectionScalar = 1f,
                FrictionCoefficient = 0.85f,
                ExhaustionPenaltyMax = 0.35f
            };
        }

        private static KccEnvironmentProfileDTO SanitizeEnvironmentProfile(KccEnvironmentProfileDTO profile)
        {
            profile.MaxSlopeAngle = math.clamp(math.isfinite(profile.MaxSlopeAngle) ? profile.MaxSlopeAngle : 48f, 1f, 89f);
            profile.CurrentAdvectionScalar = math.clamp(math.isfinite(profile.CurrentAdvectionScalar) ? profile.CurrentAdvectionScalar : 1f, 0f, 8f);
            profile.FrictionCoefficient = math.clamp(math.isfinite(profile.FrictionCoefficient) ? profile.FrictionCoefficient : 0.85f, 0f, 8f);
            profile.ExhaustionPenaltyMax = math.saturate(math.isfinite(profile.ExhaustionPenaltyMax) ? profile.ExhaustionPenaltyMax : 0.35f);
            return profile;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct KinematicResolutionJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Each Execute index mutates one KinematicStateDTO row and one fault row after the scheduler constrains
        // the active entity count. Pointer access is used to avoid copying the 64-byte DTO, which Unity safety
        // cannot map back to the guarded row.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Resolving into a temporary transform/state buffer was rejected because it creates shadow authority and
        // breaks the rollback memcpy contract. The job writes the owner row and emits separate debug artifacts.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: States, PreviousAup, DebugOutputs, and FaultFlags are disjoint Vault lanes. CollisionHits
        // and ProposedVelocities are read-only for this phase, and consumers wait on the returned JobHandle.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        [WriteOnly, NoAlias] public NativeArray<double3> PreviousAup;
        [ReadOnly, NoAlias] public NativeArray<float3> ProposedVelocities;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccCollisionHitDTO> CollisionHits;
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccDebugOutputDTO> DebugOutputs;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        public HydrodynamicKccTuningDTO Tuning;
        public double3 SectorOriginAup;
        public uint SimulationFrame;
        public float SimulationTickDelta;
        public int MaxHitsPerCommand;
        public int CollisionBypass;

        public void Execute(int index)
        {
            if (!States.IsCreated ||
                !PreviousAup.IsCreated ||
                !ProposedVelocities.IsCreated ||
                !DebugOutputs.IsCreated ||
                !FaultFlags.IsCreated ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)PreviousAup.Length ||
                (uint)index >= (uint)ProposedVelocities.Length ||
                (uint)index >= (uint)DebugOutputs.Length ||
                (uint)index >= (uint)FaultFlags.Length)
            {
                return;
            }

            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States) + (index * stateSize);
            ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statePtr);

            double3 previous = state.AUP_Position;
            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float3 velocity = HydrodynamicKccMath.Sanitize(ProposedVelocities[index], float3.zero);
            float3 displacement = velocity * dt;
            float castDistance = HydrodynamicKccMath.LengthSafe(displacement);
            float3 direction = HydrodynamicKccMath.NormalizeSafe(displacement, new float3(0f, 0f, 1f));
            float skin = math.max(0.001f, math.isfinite(Tuning.SkinWidth) ? Tuning.SkinWidth : 0.02f);
            float dynamicEpsilon = HydrodynamicKccMath.ResolveDynamicPenetrationEpsilon(Tuning.GlobalQualityWeight, skin);
            bool collisionBypassed = CollisionBypass != 0;
            uint flags = math.select(0u, HydrodynamicKccMath.FlagRespawnCollisionBypass, collisionBypassed);
            int scheduledHitStride = math.clamp(MaxHitsPerCommand, 1, 8);
            bool hasCollisionHitLane = CollisionHits.IsCreated;
            int executedIterations = collisionBypassed || !hasCollisionHitLane ? 0 : scheduledHitStride;
            int hitBase = index * scheduledHitStride;
            bool hasHit = false;
            float nearestDistance = castDistance + skin;
            float maxPenetration = 0f;
            float3 nearestNormal = float3.zero;
            float3* contactNormals = stackalloc float3[8];
            int contactCount = 0;

            if (!collisionBypassed && hasCollisionHitLane)
            {
                for (int i = 0; i < executedIterations; i++)
                {
                    int hitIndex = hitBase + i;
                    if (hitIndex >= CollisionHits.Length)
                        break;

                    HydrodynamicKccCollisionHitDTO hit = CollisionHits[hitIndex];
                    bool validHit = (hit.Flags & HydrodynamicKccMath.HitFlagValid) != 0u &&
                                    hit.Distance >= 0f &&
                                    hit.Distance <= castDistance + skin + dynamicEpsilon &&
                                    HydrodynamicKccMath.IsFinite(hit.Normal) &&
                                    math.lengthsq(hit.Normal) > 0.0001f;
                    if (!validHit)
                        continue;

                    float3 contactNormal = HydrodynamicKccMath.NormalizeSafe(hit.Normal, new float3(0f, 1f, 0f));
                    hasHit = true;
                    flags |= HydrodynamicKccMath.FlagCollision;
                    if (hit.Distance <= nearestDistance)
                    {
                        nearestDistance = hit.Distance;
                        nearestNormal = contactNormal;
                    }

                    maxPenetration = math.max(maxPenetration, math.max(0f, math.isfinite(hit.PenetrationDepth) ? hit.PenetrationDepth : 0f));

                    if (contactCount < 8 && !HasDuplicateContactPlane(contactNormals, contactCount, contactNormal))
                    {
                        contactNormals[contactCount] = contactNormal;
                        contactCount++;
                    }
                }

                int projectionPasses = math.min(executedIterations, 8);
                for (int pass = 0; pass < projectionPasses; pass++)
                {
                    bool changed = false;
                    for (int contactIndex = 0; contactIndex < contactCount; contactIndex++)
                    {
                        float3 normal = contactNormals[contactIndex];
                        float intoNormal = math.dot(velocity, normal);
                        if (intoNormal >= -dynamicEpsilon)
                            continue;

                        velocity -= normal * intoNormal;
                        changed = true;
                    }

                    if (!changed)
                        break;
                }
            }

            if (hasHit)
            {
                float allowedDistance = math.max(0f, nearestDistance - skin);
                float consumedFraction = math.saturate(allowedDistance * math.rcp(math.max(castDistance, HydrodynamicKccMath.MinDenominator)));
                float remainingDt = dt * (1f - consumedFraction);
                float depenetrationCap = math.min(math.max(0.05f, Tuning.CapsuleRadius), math.max(0f, maxPenetration - dynamicEpsilon) + skin);
                float3 depenetration = nearestNormal * math.max(0f, depenetrationCap);
                displacement = direction * allowedDistance + velocity * remainingDt + depenetration;
            }
            else
            {
                displacement = velocity * dt;
            }

            bool invalid = !HydrodynamicKccMath.IsFinite(previous) ||
                           !HydrodynamicKccMath.IsFinite(velocity) ||
                           !HydrodynamicKccMath.IsFinite(displacement);
            if (invalid)
            {
                velocity = float3.zero;
                displacement = float3.zero;
                flags |= HydrodynamicKccMath.FlagFaultNaN;
                WriteFault(index, HydrodynamicKccMath.FlagFaultNaN);
            }

            float3 currentLocal = HydrodynamicKccMath.ResolveLocalFloat3(previous, SectorOriginAup);
            state.Velocity = velocity;
            state.AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(previous + new double3(displacement.x, displacement.y, displacement.z));
            float3 predictedLocal = HydrodynamicKccMath.ResolveLocalFloat3(state.AUP_Position, SectorOriginAup);
            if (index < PreviousAup.Length)
                PreviousAup[index] = previous;
            if (index < DebugOutputs.Length)
            {
                DebugOutputs[index] = new HydrodynamicKccDebugOutputDTO
                {
                    CurrentLocal = currentLocal,
                    PredictedLocal = predictedLocal,
                    CollisionNormal = hasHit ? nearestNormal : float3.zero,
                    HitDistance = hasHit ? nearestDistance : 0f,
                    Frame = SimulationFrame,
                    Flags = flags
                };
            }

            _ = executedIterations;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasDuplicateContactPlane(float3* contactNormals, int contactCount, float3 candidate)
        {
            for (int i = 0; i < contactCount; i++)
            {
                float alignment = math.dot(contactNormals[i], candidate);
                if (alignment >= HydrodynamicKccMath.DuplicateContactPlaneDotThreshold)
                    return true;
            }

            return false;
        }

        private void WriteFault(int index, uint faultMask)
        {
            if (!FaultFlags.IsCreated || index >= FaultFlags.Length)
                return;

            HydrodynamicKccFaultFlagDTO entry = FaultFlags[index];
            entry.FaultMask |= (int)faultMask;
            FaultFlags[index] = entry;
        }

    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct KinematicTelemetryAggregateJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccDebugOutputDTO> DebugOutputs;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        [NoAlias] public NativeArray<KinematicTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public HydrodynamicKccTuningDTO Tuning;
        public uint SimulationFrame;
        public int EntityCount;
        public int ExecutedIterations;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0 || !States.IsCreated || States.Length == 0)
                return;

            int count = math.clamp(EntityCount, 0, States.Length);
            if (count <= 0)
                return;

            double3 firstAup = States[0].AUP_Position;
            float3 averageVelocity = float3.zero;
            float maxSpeed = 0f;
            float computeUs = 0f;
            uint flags = 0u;
            uint hash = 2166136261u;
            float quality = HydrodynamicKccMath.ResolveQuality01(Tuning.GlobalQualityWeight);
            float maxConfiguredSpeed = math.max(0.1f, math.isfinite(Tuning.MaxSpeed) ? Tuning.MaxSpeed : 6f);
            uint executedIterations = (uint)math.max(0, ExecutedIterations);

            for (int i = 0; i < count; i++)
            {
                KinematicStateDTO state = States[i];
                float3 velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
                float speed = HydrodynamicKccMath.LengthSafe(velocity);
                averageVelocity += velocity;
                maxSpeed = math.max(maxSpeed, speed);
                uint entityFlags = 0u;
                if (i < DebugOutputs.Length)
                    entityFlags |= DebugOutputs[i].Flags;
                if (i < FaultFlags.Length)
                    entityFlags |= (uint)FaultFlags[i].FaultMask;

                flags |= entityFlags;
                uint entityHash = HydrodynamicKccMath.HashState(state.AUP_Position, velocity, SimulationFrame, entityFlags);
                entityHash ^= (uint)i * 0x9E3779B9u;
                hash += entityHash;
                computeUs += HydrodynamicKccMath.EstimateIntegrationMicroseconds(quality, speed) +
                             HydrodynamicKccMath.EstimateResolutionMicroseconds(quality, executedIterations, (entityFlags & HydrodynamicKccMath.FlagCollision) != 0u, speed);
            }

            averageVelocity *= math.rcp(math.max(1f, count));
            float normalizedSpeed = maxSpeed * math.rcp(maxConfiguredSpeed);
            float turbulence = math.saturate(normalizedSpeed * normalizedSpeed) * math.lerp(0.18f, 1f, quality);
            int ringIndex = (int)(SimulationFrame % (uint)TelemetryRing.Length);
            TelemetryRing[ringIndex] = new KinematicTelemetryEntry
            {
                AupPosition = firstAup,
                Velocity = averageVelocity,
                Speed = maxSpeed,
                TurbulenceScalar = turbulence,
                ComputeMicroseconds = computeUs,
                Frame = SimulationFrame,
                StateHash = hash,
                Flags = flags,
                Iterations = executedIterations
            };

            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = ringIndex;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct KccEnvironmentTelemetryAggregateJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<KccEnvironmentDebugOutputDTO> EnvironmentDebugOutputs;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        [NoAlias] public NativeArray<KccEnvironmentTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint SimulationFrame;
        public int EntityCount;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0 || !States.IsCreated || States.Length == 0)
                return;

            int count = math.clamp(EntityCount, 0, States.Length);
            if (count <= 0)
                return;

            double3 firstAup = States[0].AUP_Position;
            float3 averageFlow = float3.zero;
            float maxFlowMagnitude = 0f;
            float maxSlopeAngle = 0f;
            float maxExhaustion = 0f;
            float computeUs = 0f;
            uint flags = 0u;
            uint sampleMode = 0u;
            uint hash = 2166136261u;

            for (int i = 0; i < count; i++)
            {
                KccEnvironmentDebugOutputDTO debug = EnvironmentDebugOutputs.IsCreated && i < EnvironmentDebugOutputs.Length ? EnvironmentDebugOutputs[i] : default;
                float3 appliedFlow = HydrodynamicKccMath.Sanitize(debug.AppliedFlow, float3.zero);
                float flowMagnitude = HydrodynamicKccMath.LengthSafe(appliedFlow);
                averageFlow += appliedFlow;
                maxFlowMagnitude = math.max(maxFlowMagnitude, flowMagnitude);
                maxSlopeAngle = math.max(maxSlopeAngle, math.max(0f, math.isfinite(debug.SlopeAngleDegrees) ? debug.SlopeAngleDegrees : 0f));
                maxExhaustion = math.max(maxExhaustion, math.saturate(math.isfinite(debug.ExhaustionPenalty) ? debug.ExhaustionPenalty : 0f));
                computeUs += math.max(0f, math.isfinite(debug.ComputeMicroseconds) ? debug.ComputeMicroseconds : 0f);
                flags |= debug.Flags;
                sampleMode = math.max(sampleMode, (debug.Flags & HydrodynamicKccMath.FlagTrilinearFlowSample) != 0u ? 1u : 0u);
                if (FaultFlags.IsCreated && i < FaultFlags.Length)
                    flags |= (uint)FaultFlags[i].FaultMask;

                KinematicStateDTO state = States[i];
                uint entityHash = HydrodynamicKccMath.HashState(state.AUP_Position, appliedFlow, SimulationFrame, debug.Flags);
                entityHash ^= (uint)i * 0x9E3779B9u;
                hash += entityHash;
            }

            averageFlow *= math.rcp(math.max(1f, count));
            int ringIndex = (int)(SimulationFrame % (uint)TelemetryRing.Length);
            TelemetryRing[ringIndex] = new KccEnvironmentTelemetryEntry
            {
                AupPosition = firstAup,
                AppliedFlow = averageFlow,
                SlopeAngleDegrees = maxSlopeAngle,
                ExhaustionPenalty = maxExhaustion,
                ComputeMicroseconds = computeUs + maxFlowMagnitude * 0.01f,
                Frame = SimulationFrame,
                StateHash = hash,
                Flags = flags,
                SampleMode = sampleMode
            };

            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = ringIndex;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct KinematicVisualSyncJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<double3> PreviousAup;
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccVisualOutputDTO> VisualOutputs;
        public double3 CameraOrSectorAup;
        public HydrodynamicKccTuningDTO Tuning;
        public uint SimulationFrame;
        public float VisualDeltaTime;
        public byte BypassVisualSync;

        public void Execute(int index)
        {
            KinematicStateDTO state = States[index];
            double3 previous = index < PreviousAup.Length ? PreviousAup[index] : state.AUP_Position;
            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(VisualDeltaTime) ? VisualDeltaTime : 0.016666667f);
            float sharpness = math.max(0.01f, math.isfinite(Tuning.VisualSyncSharpness) ? Tuning.VisualSyncSharpness : 18f);
            float quality = HydrodynamicKccMath.ResolveQuality01(Tuning.GlobalQualityWeight);
            float alpha = 1f - HydrodynamicKccMath.ExpNegRational(sharpness * dt);
            alpha = math.saturate(alpha * math.lerp(0.35f, 1f, quality));
            alpha = math.select(alpha, 1f, BypassVisualSync != 0);
            float3 previousLocal = HydrodynamicKccMath.ResolveLocalFloat3(previous, CameraOrSectorAup);
            float3 currentLocal = HydrodynamicKccMath.ResolveLocalFloat3(state.AUP_Position, CameraOrSectorAup);
            float3 local = math.lerp(previousLocal, currentLocal, alpha);
            uint flags = math.select(0u, HydrodynamicKccMath.FlagVisualBypass, BypassVisualSync != 0);

            VisualOutputs[index] = new HydrodynamicKccVisualOutputDTO
            {
                SourceAup = state.AUP_Position,
                LocalPosition = local,
                PreviousLocalPosition = previousLocal,
                SmoothingAlpha = alpha,
                Speed = HydrodynamicKccMath.LengthSafe(state.Velocity),
                Flags = flags,
                Frame = SimulationFrame
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct KinematicRollbackFenceJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // RollbackBytes is a raw byte snapshot lane whose widened byte count is validated against capacity before
        // UnsafeUtility.MemCpy. Unity safety cannot prove this byte view is a non-overlapping destination
        // for the typed States source.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Per-row serialization was rejected because rollback needs a blind blittable copy and must preserve DTO
        // byte layout exactly. The byte lane keeps the state-ring path deterministic and allocation-free.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: RollbackBytes is owned exclusively by the rollback fence during this IJob, is at least
        // EntityCount * sizeof(KinematicStateDTO), and is consumed only after this job completes.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> RollbackBytes;
        public int EntityCount;

        public void Execute()
        {
            int count = math.clamp(EntityCount, 0, States.Length);
            int stateBytes = UnsafeUtility.SizeOf<KinematicStateDTO>();
            long bytes = (long)count * stateBytes;
            if (bytes <= 0L || !RollbackBytes.IsCreated || bytes > RollbackBytes.Length)
                return;

            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(States);
            void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(RollbackBytes);
            UnsafeUtility.MemCpy(destination, source, bytes);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EmitWakeSignalsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicWakePacketDTO> WakePackets;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SignalBus owns this queue lane; the job is a producer only and the returned handle fences any drain.
        // Unity's container safety cannot encode that external queue ownership on the ParallelWriter field.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Writing wake events to a managed list or applying them directly to neighboring systems was rejected
        // because it would allocate or introduce cross-domain hot coupling. Queue emission keeps the route
        // first-party and dependency-fenced.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: WakePackets is read-only, WakeWriter only enqueues, and downstream wake consumers drain the
        // SignalBus queue after the returned JobHandle completes.
        [NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<WakeGeneratedSignal>.ParallelWriter WakeWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> WakeWriterBudget;
        public void Execute(int index)
        {
            HydrodynamicWakePacketDTO packet = WakePackets[index];
            if ((packet.Flags & HydrodynamicKccMath.FlagWake) == 0u)
                return;

            float magnitude = math.max(packet.WakeMagnitude, HydrodynamicKccMath.LengthSafe(packet.Velocity));
            float radius = math.max(0.1f, packet.WakeRadius);
            float3 direction = HydrodynamicKccMath.NormalizeSafe(packet.Velocity, new float3(0f, 0f, 1f));
            WakeGeneratedSignal signal = new WakeGeneratedSignal
            {
                PositionAup = HydrodynamicKccMath.ToAup48(packet.AupPosition),
                Velocity = direction * magnitude,
                SourceFlags = HydrodynamicKccMath.PackWakeSourceFlags(HydrodynamicKccMath.WakeSourcePlayer, magnitude, radius)
            };
            if (!SignalBus<WakeGeneratedSignal>.TryEnqueueBounded(WakeWriter, WakeWriterBudget, signal))
                MarkSignalDrop(index);
        }

        private void MarkSignalDrop(int index)
        {
            if (!FaultFlags.IsCreated || (uint)index >= (uint)FaultFlags.Length)
                return;

            HydrodynamicKccFaultFlagDTO entry = FaultFlags[index];
            entry.FaultMask |= (int)HydrodynamicKccMath.FlagSignalDrop;
            FaultFlags[index] = entry;
        }
    }

#if UNITY_EDITOR
    public static class HydrodynamicFluidProfileCsvParser
    {
        private const byte Comma = (byte)',';
        private const byte NewLine = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';
        private const byte Comment = (byte)'#';

        public static int ParseProfiles(
            ReadOnlySpan<byte> bytes,
            NativeArray<HydrodynamicFluidProfileDTO> profiles,
            NativeArray<int> buckets)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return 0;

            if (buckets.IsCreated)
            {
                for (int i = 0; i < buckets.Length; i++)
                    buckets[i] = -1;
            }

            int count = 0;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i != bytes.Length && bytes[i] != NewLine)
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && bytes[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(bytes.Slice(lineStart, lineEnd - lineStart), out HydrodynamicFluidProfileDTO profile))
                {
                    int profileIndex = count;
                    if (profileIndex >= profiles.Length)
                        return count;

                    profile.NextIndex = -1;
                    profiles[profileIndex] = profile;
                    if (buckets.IsCreated && buckets.Length > 0)
                    {
                        int bucket = (int)(profile.ProfileHash % (uint)buckets.Length);
                        profile.NextIndex = buckets[bucket];
                        profiles[profileIndex] = profile;
                        buckets[bucket] = profileIndex;
                    }

                    count++;
                }

                lineStart = i + 1;
            }

            return count;
        }

        public static int ParseProfiles(
            ReadOnlySpan<byte> bytes,
            Span<HydrodynamicFluidProfileDTO> profiles,
            Span<int> buckets)
        {
            if (profiles.Length == 0)
                return 0;

            profiles.Clear();
            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = -1;

            int count = 0;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i != bytes.Length && bytes[i] != NewLine)
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && bytes[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(bytes.Slice(lineStart, lineEnd - lineStart), out HydrodynamicFluidProfileDTO profile))
                {
                    int profileIndex = count;
                    if (profileIndex >= profiles.Length)
                        return count;

                    profile.NextIndex = -1;
                    profiles[profileIndex] = profile;
                    if (buckets.Length > 0)
                    {
                        int bucket = (int)(profile.ProfileHash % (uint)buckets.Length);
                        profile.NextIndex = buckets[bucket];
                        profiles[profileIndex] = profile;
                        buckets[bucket] = profileIndex;
                    }

                    count++;
                }

                lineStart = i + 1;
            }

            return count;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out HydrodynamicFluidProfileDTO profile)
        {
            profile = default;
            int start = TrimStart(line);
            if (start >= line.Length || line[start] == Comment)
                return false;

            int cursor = start;
            ReadOnlySpan<byte> name = ReadField(line, ref cursor);
            if (name.Length == 0 || EqualsAscii(name, "profile"))
                return false;

            profile.ProfileHash = Fnv1A(name);
            profile.BaseDrag = ReadFloatField(line, ref cursor, 0.18f);
            profile.FluidDensity = ReadFloatField(line, ref cursor, 1f);
            profile.MaxSpeed = ReadFloatField(line, ref cursor, 6f);
            profile.GravityMultiplier = ReadFloatField(line, ref cursor, 1f);
            profile.BuoyancyScalar = ReadFloatField(line, ref cursor, 1.05f);
            profile.Flags = 1u;
            return profile.ProfileHash != 0u;
        }

        private static int TrimStart(ReadOnlySpan<byte> line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == (byte)' ' || line[i] == (byte)'\t'))
                i++;
            return i;
        }

        private static ReadOnlySpan<byte> ReadField(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != Comma)
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == Comma)
                cursor++;

            while (start < end && (line[start] == (byte)' ' || line[start] == (byte)'\t'))
                start++;

            while (end > start && (line[end - 1] == (byte)' ' || line[end - 1] == (byte)'\t'))
                end--;

            return line.Slice(start, end - start);
        }

        private static float ReadFloatField(ReadOnlySpan<byte> line, ref int cursor, float fallback)
        {
            ReadOnlySpan<byte> field = ReadField(line, ref cursor);
            return TryParseFloat(field, out float value) ? value : fallback;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> field, out float value)
        {
            value = 0f;
            if (field.Length == 0)
                return false;

            int i = 0;
            float sign = 1f;
            if (field[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }

            float integer = 0f;
            bool any = false;
            while (i < field.Length && field[i] >= (byte)'0' && field[i] <= (byte)'9')
            {
                integer = integer * 10f + (field[i] - (byte)'0');
                i++;
                any = true;
            }

            float fraction = 0f;
            float scale = 1f;
            if (i < field.Length && field[i] == (byte)'.')
            {
                i++;
                while (i < field.Length && field[i] >= (byte)'0' && field[i] <= (byte)'9')
                {
                    fraction = fraction * 10f + (field[i] - (byte)'0');
                    scale *= 10f;
                    i++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = sign * (integer + fraction * math.rcp(scale));
            return math.isfinite(value);
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> value, string literal)
        {
            if (value.Length != literal.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte a = value[i];
                byte b = (byte)literal[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }

        private static uint Fnv1A(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }
    }

    public static class KccEnvironmentProfileCsvParser
    {
        private const byte Comma = (byte)',';
        private const byte NewLine = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';
        private const byte Comment = (byte)'#';

        public static int ParseProfiles(
            ReadOnlySpan<byte> bytes,
            NativeArray<KccEnvironmentProfileDTO> profiles,
            NativeArray<uint> profileHashes,
            NativeArray<int> buckets)
        {
            if (!profiles.IsCreated || profiles.Length == 0 || !profileHashes.IsCreated || profileHashes.Length < profiles.Length)
                return 0;

            for (int i = 0; i < profileHashes.Length; i++)
                profileHashes[i] = 0u;

            if (buckets.IsCreated)
            {
                for (int i = 0; i < buckets.Length; i++)
                    buckets[i] = -1;
            }

            int count = 0;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i != bytes.Length && bytes[i] != NewLine)
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && bytes[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(bytes.Slice(lineStart, lineEnd - lineStart), out uint profileHash, out KccEnvironmentProfileDTO profile))
                {
                    int profileIndex = count;
                    if (profileIndex >= profiles.Length)
                        return count;

                    profiles[profileIndex] = profile;
                    profileHashes[profileIndex] = profileHash;
                    if (buckets.IsCreated && buckets.Length > 0)
                    {
                        int bucket = (int)(profileHash % (uint)buckets.Length);
                        InsertProfileBucket(profileHash, profileIndex, profileHashes, buckets, bucket);
                    }

                    count++;
                }

                lineStart = i + 1;
            }

            return count;
        }

        public static int ParseProfiles(
            ReadOnlySpan<byte> bytes,
            Span<KccEnvironmentProfileDTO> profiles,
            Span<uint> profileHashes,
            Span<int> buckets)
        {
            if (profiles.Length == 0 || profileHashes.Length < profiles.Length)
                return 0;

            profiles.Clear();
            profileHashes.Clear();
            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = -1;

            int count = 0;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i != bytes.Length && bytes[i] != NewLine)
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && bytes[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(bytes.Slice(lineStart, lineEnd - lineStart), out uint profileHash, out KccEnvironmentProfileDTO profile))
                {
                    int profileIndex = count;
                    if (profileIndex >= profiles.Length)
                        return count;

                    profiles[profileIndex] = profile;
                    profileHashes[profileIndex] = profileHash;
                    if (buckets.Length > 0)
                    {
                        int bucket = (int)(profileHash % (uint)buckets.Length);
                        InsertProfileBucket(profileHash, profileIndex, profileHashes, buckets, bucket);
                    }

                    count++;
                }

                lineStart = i + 1;
            }

            return count;
        }

        private static void InsertProfileBucket(uint profileHash, int profileIndex, NativeArray<uint> profileHashes, NativeArray<int> buckets, int startBucket)
        {
            for (int probe = 0; probe < buckets.Length; probe++)
            {
                int bucket = startBucket + probe;
                if (bucket >= buckets.Length)
                    bucket -= buckets.Length;

                int existing = buckets[bucket];
                if (existing < 0 ||
                    ((uint)existing < (uint)profileHashes.Length && profileHashes[existing] == profileHash))
                {
                    buckets[bucket] = profileIndex;
                    return;
                }
            }
        }

        private static void InsertProfileBucket(uint profileHash, int profileIndex, ReadOnlySpan<uint> profileHashes, Span<int> buckets, int startBucket)
        {
            for (int probe = 0; probe < buckets.Length; probe++)
            {
                int bucket = startBucket + probe;
                if (bucket >= buckets.Length)
                    bucket -= buckets.Length;

                int existing = buckets[bucket];
                if (existing < 0 ||
                    ((uint)existing < (uint)profileHashes.Length && profileHashes[existing] == profileHash))
                {
                    buckets[bucket] = profileIndex;
                    return;
                }
            }
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out uint profileHash, out KccEnvironmentProfileDTO profile)
        {
            profileHash = 0u;
            profile = default;
            int start = TrimStart(line);
            if (start >= line.Length || line[start] == Comment)
                return false;

            int cursor = start;
            ReadOnlySpan<byte> name = ReadField(line, ref cursor);
            if (name.Length == 0 || EqualsAscii(name, "profile"))
                return false;

            profileHash = Fnv1A(name);
            profile.MaxSlopeAngle = ReadFloatField(line, ref cursor, 48f);
            profile.CurrentAdvectionScalar = ReadFloatField(line, ref cursor, 1f);
            profile.FrictionCoefficient = ReadFloatField(line, ref cursor, 0.85f);
            profile.ExhaustionPenaltyMax = ReadFloatField(line, ref cursor, 0.35f);
            profile.MaxSlopeAngle = math.clamp(math.isfinite(profile.MaxSlopeAngle) ? profile.MaxSlopeAngle : 48f, 1f, 89f);
            profile.CurrentAdvectionScalar = math.clamp(math.isfinite(profile.CurrentAdvectionScalar) ? profile.CurrentAdvectionScalar : 1f, 0f, 8f);
            profile.FrictionCoefficient = math.clamp(math.isfinite(profile.FrictionCoefficient) ? profile.FrictionCoefficient : 0.85f, 0f, 8f);
            profile.ExhaustionPenaltyMax = math.saturate(math.isfinite(profile.ExhaustionPenaltyMax) ? profile.ExhaustionPenaltyMax : 0.35f);
            return profileHash != 0u;
        }

        private static int TrimStart(ReadOnlySpan<byte> line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == (byte)' ' || line[i] == (byte)'\t'))
                i++;
            return i;
        }

        private static ReadOnlySpan<byte> ReadField(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != Comma)
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == Comma)
                cursor++;

            while (start < end && (line[start] == (byte)' ' || line[start] == (byte)'\t'))
                start++;

            while (end > start && (line[end - 1] == (byte)' ' || line[end - 1] == (byte)'\t'))
                end--;

            return line.Slice(start, end - start);
        }

        private static float ReadFloatField(ReadOnlySpan<byte> line, ref int cursor, float fallback)
        {
            ReadOnlySpan<byte> field = ReadField(line, ref cursor);
            return TryParseFloat(field, out float value) ? value : fallback;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> field, out float value)
        {
            value = 0f;
            if (field.Length == 0)
                return false;

            int i = 0;
            float sign = 1f;
            if (field[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }

            float integer = 0f;
            bool any = false;
            while (i < field.Length && field[i] >= (byte)'0' && field[i] <= (byte)'9')
            {
                integer = integer * 10f + (field[i] - (byte)'0');
                i++;
                any = true;
            }

            float fraction = 0f;
            float scale = 1f;
            if (i < field.Length && field[i] == (byte)'.')
            {
                i++;
                while (i < field.Length && field[i] >= (byte)'0' && field[i] <= (byte)'9')
                {
                    fraction = fraction * 10f + (field[i] - (byte)'0');
                    scale *= 10f;
                    i++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = sign * (integer + fraction * math.rcp(scale));
            return math.isfinite(value);
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> value, string literal)
        {
            if (value.Length != literal.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte a = value[i];
                byte b = (byte)literal[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }

        private static uint Fnv1A(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }
    }
#endif

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed partial class HydrodynamicKccRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int DefaultCapacity = 1;
        private const int TelemetryCapacity = 300;
        private const int MaxCollisionHitsPerCommand = 8;
        private const int DefaultFluidProfileCapacity = 64;
        private const int DefaultFluidProfileBucketCount = 128;
        private const int EnvironmentGridAxisX = 16;
        private const int EnvironmentGridAxisY = 8;
        private const int EnvironmentGridAxisZ = 16;
        private const int EnvironmentGridCellCount = EnvironmentGridAxisX * EnvironmentGridAxisY * EnvironmentGridAxisZ;
        private const float MinQuaternionLengthSq = 0.000001f;
        internal const float DefaultWaterSurfaceY = HydrodynamicKccMath.DefaultWaterSurfaceY;
        private const uint MetabolismFatigueFlag = 1u << 9;
        private const uint ScheduledVaultPinStates = 1u << 0;
        private const uint ScheduledVaultPinInputs = 1u << 1;
        private const uint ScheduledVaultPinProposedVelocities = 1u << 2;
        private const uint ScheduledVaultPinResolvedHits = 1u << 3;
        private const uint ScheduledVaultPinFaultFlags = 1u << 4;
        private const uint ScheduledVaultPinWakePackets = 1u << 5;
        private const uint ScheduledVaultPinTuning = 1u << 6;
        private const uint ScheduledVaultPinEnvironmentProfile = 1u << 7;
        private const uint ScheduledVaultPinEnvironmentGrid = 1u << 8;
        private const uint ScheduledVaultPinEnvironmentFlowField = 1u << 9;
        private const uint ScheduledVaultPinEnvironmentSdf = 1u << 10;
        private const uint ScheduledVaultPinEnvironmentMockMetabolism = 1u << 11;
        private const uint ScheduledVaultPinEnvironmentDebug = 1u << 12;
        private const uint ScheduledVaultPinPreviousAup = 1u << 13;
        private const uint ScheduledVaultPinVisualOutputs = 1u << 14;
        private const uint ScheduledVaultPinTelemetryRing = 1u << 15;
        private const uint ScheduledVaultPinTelemetryCursor = 1u << 16;
        private const uint ScheduledVaultPinRollbackBytes = 1u << 17;
        private const uint ScheduledVaultPinDebugOutputs = 1u << 18;
        private const uint ScheduledVaultPinEnvironmentTelemetryRing = 1u << 19;
        private const uint ScheduledVaultPinEnvironmentTelemetryCursor = 1u << 20;
        private const uint ScheduledVaultPinPublishedMetabolismStates = 1u << 21;
        private const uint KccFaultEventHash = 0x4B464654u; // KFFT
        private const uint KccFaultDumpHash = 0x4B464450u; // KFDP
        private const uint KccCrushDamageEventHash = 0x4B435244u; // KCRD

        // WATER-AS-MEDIUM reference constants.
        // Tuning.FluidDensity is a NORMALIZED scalar (DefaultTuning ships 1f, not 1025f), so it must be
        // multiplied by this reference before it can be handed to a model that wants kg/m^3. Feeding the
        // raw 1f in as a density would make the body ~1000x denser than the water and sink it like a stone.
        private const float SeawaterReferenceDensityKgPerM3 = 1025f;
        // Suited-diver body density. Displaced volume is derived as mass/this rather than from the capsule
        // collider: the collider is ~0.6 m^3 while an 80 kg body displaces ~0.076 m^3, and using the capsule
        // would yield ~130 kg/m^3 and launch the player at the surface at ~66 m/s^2.
        private const float PlayerBodyDensityKgPerM3 = 1050f;
        // Thermocline band from the project's own creature data: "Thermocline boundary (1000-1200 m)"
        // (FAUNA_CREATURE_THERMAL_PHANTOM_HABITAT), i.e. centre 1100 m, thickness 200 m.
        private const float DefaultThermoclineDepthMeters = 1100f;
        private const float DefaultThermoclineThicknessMeters = 200f;
        private const float DefaultThermoclineResistanceForce = 0.35f;
        // Suit crush rating. BaseModule ships hullCrushDepthMeters = 4000 for habitat modules; a soft suit
        // fails far shallower, so the player threshold is authored well above the thermocline band.
        private const float DefaultCrushDepthThresholdMeters = 1400f;
        private const float DefaultCrushMaxDamageRatePerSecond = 4f;
        private const float DefaultCrushDamageExponent = 2f;
        private const float DefaultMediumDragCoefficient = 0.82f;
        // _mediumDragCoefficient is [Min(0f)] with no upper bound, and the drag force is quadratic in the
        // relative speed, so an out-of-range coefficient scales straight into the integrator. 2.0 is the
        // bluff-body ceiling (flat plate is ~1.28, sphere ~0.47, a suited diver ~0.8-1.2); anything above it
        // is authoring error, not physics.
        private const float MaxMediumDragCoefficient = 2f;
        // BuoyancyScalar > 0 lowers the effective body density (see UpdateDensityBuoyancy). As the scalar
        // approaches 0 that division diverges: 0.05 already yields 21000 kg/m^3 and ~-19 g of sink. 0.5 is
        // the real floor -- it maps to 2100 kg/m^3, a diver carrying ballast equal to their own displaced
        // water mass, which lands at ~-1.02 g and therefore meets the BuoyancyScalar = 0 disable idiom
        // (plain gravity, -1 g) continuously instead of exploding through it.
        private const float MinBuoyancyTrimScalar = 0.5f;
        // Outer safety net on the density model against pathological FluidDensity/mass data, expressed in
        // gravities of NET force. The legacy scalar term spans -1 g (scalar 0) to +1.5 g (tuner slider max
        // 2.5) at full submersion, so +-2 g covers the authored range it replaces and nothing wider.
        private const float MaxDensityBuoyancyGravities = 2f;
        // PressureCrushDamageModel is evaluated every fixed tick but flushed to the combat damage queue on
        // this interval: CombatDamageRuntime.TryQueueDamage has a bounded queue and rejects when full, so a
        // 50 Hz ingress from one source would both spam it and starve other producers.
        private const float CrushDamageFlushIntervalSeconds = 0.5f;
        // HARD per-second cap on crush damage. PressureCrushDamageModel.Evaluate returns
        // (pow(depth/threshold, e) - 1) * maxDamageRate with rawDamage UNBOUNDED -- maxDamageRate is a
        // SCALE, not a cap -- and it returns float.MaxValue on internal overflow. Uncapped, the authored
        // defaults (1400 m / e=2 / 4) give 28.6 dmg/s at 4000 m and 74.5 dmg/s at 6200 m
        // (ShinobuStormPropagationConstants.DefaultMaxDepthMeters) against HectonPlayerHealth's
        // maxHealth = 100, i.e. death in 3.5 s and 1.3 s: an instakill, not a threat.
        // 12 dmg/s is chosen against that health pool -- 100 / 12 = 8.3 s from full health -- and is not an
        // arbitrary number: it is the model's own rate at exactly TWICE the suit crush rating
        // ((2^2 - 1) * 4 = 12), so the curve is untouched from 1400 m to 2800 m and only saturates below
        // double-rated depth. Resulting time-to-death from 100 HP, ignoring armour/status/regen:
        // 2000 m -> 4.16 dmg/s -> 24.0 s; 2800 m -> 12 dmg/s -> 8.3 s; 4000 m and 6200 m -> capped 12 -> 8.3 s.
        private const float MaxCrushDamagePerSecond = 12f;
        // Derived, so the per-flush clamp actually BINDS. The previous literal 250f was 500 dmg/s across a
        // 0.5 s flush -- five times the whole health pool -- so it could never fire and protected nothing.
        private const float MaxCrushDamagePerFlush = MaxCrushDamagePerSecond * CrushDamageFlushIntervalSeconds;

        [SerializeField] private int _entityCapacity = DefaultCapacity;
        [SerializeField] private float _waterSurfaceY = DefaultWaterSurfaceY;
        [SerializeField] private bool _applyVisualToTransform = true;
        [SerializeField] private bool _runMockInput = true;
        [SerializeField] private bool _consumeExternalInputBuffer;
        [SerializeField] private int _maxRollbackFastForwardFrames = 8;

        [Header("Water as medium")]
        [SerializeField] private bool _enableWaterMediumForces = true;
        [SerializeField, Min(0f)] private float _mediumDragCoefficient = DefaultMediumDragCoefficient;
        [SerializeField, Min(0f)] private float _thermoclineDepthMeters = DefaultThermoclineDepthMeters;
        [SerializeField, Min(0f)] private float _thermoclineThicknessMeters = DefaultThermoclineThicknessMeters;
        [SerializeField, Min(0f)] private float _thermoclineResistanceForce = DefaultThermoclineResistanceForce;
        [SerializeField] private bool _requireThermoclineWeatherState = true;
        [SerializeField, Min(0f)] private float _crushDepthThresholdMeters = DefaultCrushDepthThresholdMeters;
        [SerializeField, Min(0f)] private float _crushMaxDamageRatePerSecond = DefaultCrushMaxDamageRatePerSecond;
        [SerializeField, Min(1f)] private float _crushDamageExponent = DefaultCrushDamageExponent;

        private IDataVault _dataVault;
        private Transform _cachedTransform;
        private CapsuleCollider _capsule;
        private VaultGenerationHandle<KinematicStateDTO> _statesHandle;
        private VaultGenerationHandle<HydrodynamicKccInputDTO> _inputsHandle;
        private VaultGenerationHandle<float3> _proposedVelocitiesHandle;
        private VaultGenerationHandle<double3> _previousAupHandle;
        private VaultGenerationHandle<HydrodynamicKccVisualOutputDTO> _visualOutputsHandle;
        private VaultGenerationHandle<KinematicTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<HydrodynamicKccTuningDTO> _tuningHandle;
        private VaultGenerationHandle<byte> _rollbackBytesHandle;
        private VaultGenerationHandle<HydrodynamicKccFaultFlagDTO> _faultFlagsHandle;
        private VaultGenerationHandle<HydrodynamicWakePacketDTO> _wakePacketsHandle;
        private VaultGenerationHandle<HydrodynamicKccDebugOutputDTO> _debugOutputsHandle;
        private VaultGenerationHandle<HydrodynamicKccCollisionHitDTO> _resolvedHitsHandle;
        private VaultGenerationHandle<HydrodynamicFluidProfileDTO> _fluidProfilesHandle;
        private VaultGenerationHandle<int> _fluidProfileBucketsHandle;
        private VaultGenerationHandle<KccEnvironmentProfileDTO> _environmentProfileHandle;
        private VaultGenerationHandle<KccEnvironmentGridDTO> _environmentGridHandle;
        private VaultGenerationHandle<float3> _environmentFlowFieldHandle;
        private VaultGenerationHandle<float> _environmentSdfHandle;
        private VaultGenerationHandle<MetabolicStateDTO> _environmentMockMetabolismHandle;
        private VaultGenerationHandle<MetabolicStateDTO> _metabolismStatesHandle;
        private VaultGenerationHandle<KccEnvironmentDebugOutputDTO> _environmentDebugHandle;
        private VaultGenerationHandle<KccEnvironmentTelemetryEntry> _environmentTelemetryRingHandle;
        private VaultGenerationHandle<int> _environmentTelemetryCursorHandle;
        private VaultGenerationHandle<KccEnvironmentProfileDTO> _environmentProfilesHandle;
        private VaultGenerationHandle<int> _environmentProfileBucketsHandle;
        private VaultGenerationHandle<uint> _environmentProfileHashesHandle;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private IWeatherService _weatherService;
        private float3 _mediumCurrentDragForce;
        private float _mediumThermoclineResistance01;
        private float _mediumDensityBuoyancyNewtons;
        private uint _mediumFlags;
        private float _mediumDepthMeters;
        private float _pendingCrushDamage;
        private float _crushDamageFlushTimer;
        private int _combatDamageTargetId;
        private bool _rollbackResimulationActive;
        private JobHandle _inputHandle;
        private JobHandle _environmentMockHandle;
        private JobHandle _integrationHandle;
        private JobHandle _commandHandle;
        private JobHandle _collisionHandle;
        private JobHandle _sdfCollisionHandle;
        private JobHandle _postSimulationHandle;
        private JobHandle _externalInputHandle;
        private bool _registeredFixedTick;
        private bool _registeredPostFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwap;
        private bool _collisionScheduled;
        private bool _postScheduled;
        private bool _externalInputArmed;
        private bool _metabolismStateReadPinHeld;
        private bool _coreBlackboxWarmed;
        private uint _scheduledVaultBufferPinMask;
        private IDataVault _scheduledVaultBufferPinVault;
        private IDataVault _metabolismStateReadPinVault;
        private int _dumpedFaultMask;
        private int _rollbackVisualBypassFrames;
        private int _respawnCollisionBypassFrames;
        private int _lastRespawnCollisionSnapshotGeneration;
        private int _resolvedBufferCapacity;
        private int _scheduledEntityCount;
        private int _scheduledMaxHitsPerCommand;
        private int _droppedSignalCount;
        private uint _simulationFrame;
        private float _globalQualityWeight = 1f;
        private float3 _queuedExternalAcceleration;
        private float3 _queuedExternalVelocityChange;
        private float3 _queuedExternalVelocityTarget;
        private double3 _queuedExternalPositionTargetAup;
        private quaternion _queuedExternalRotationTarget = quaternion.identity;
        private uint _queuedExternalControlFlags;
        private Vector3 _pendingVisualPositionTarget;
        private quaternion _pendingVisualRotationTarget = quaternion.identity;
        private bool _hasPendingVisualPositionTarget;
        private bool _hasPendingVisualRotationTarget;
        private float3 _lastGizmoCurrent;
        private float3 _lastGizmoPredicted;
        private float3 _lastGizmoNormal;
        private float3 _lastGizmoFlow;
        private float3 _lastGizmoSlopeSlide;

        public bool IsAuthorityRouteActive => Application.isPlaying && isActiveAndEnabled && _dataVault != null;
        public int DroppedSignalCount => _droppedSignalCount;

        /// <summary>
        /// Depth in metres below the resolved runtime water surface, as used by the water-medium models.
        /// Same convention and datum as the integration job: 0 at or above the surface, positive downward,
        /// surface falling back to <c>WorldWaterLevelCalibrationMath.DefaultWaterLevelY</c>, never to 0.
        /// </summary>
        public float MediumDepthMeters => _mediumDepthMeters;

        /// <summary>Thermocline resistance scalar 0-1 applied on the last fixed tick.</summary>
        public float MediumThermoclineResistance01 => _mediumThermoclineResistance01;

        /// <summary>Crush damage accumulated since the last flush to the combat damage ingress.</summary>
        public float PendingCrushDamage => _pendingCrushDamage;

#if UNITY_EDITOR
        private static HydrodynamicKccRuntime EditorActiveRuntime;

        public static bool TryGetEditorTelemetryVaultView(out NativeArray<KinematicTelemetryEntry>.ReadOnly telemetry, out int cursor, out int length)
        {
            telemetry = default;
            cursor = 0;
            length = 0;

            HydrodynamicKccRuntime runtime = EditorActiveRuntime;
            if (runtime == null ||
                runtime._collisionScheduled ||
                runtime._postScheduled ||
                runtime._dataVault == null ||
                !IsVaultHandle(in runtime._telemetryRingHandle, BufferID.ShinobuHydroKccTelemetryRing, SystemID.Physics))
            {
                return false;
            }

            if (!TryReadOnlyVaultBuffer(runtime._dataVault, in runtime._telemetryRingHandle, BufferID.ShinobuHydroKccTelemetryRing, SystemID.Physics, TelemetryCapacity, out NativeArray<KinematicTelemetryEntry>.ReadOnly telemetryBuffer))
                return false;

            NativeArray<int>.ReadOnly cursorBuffer = TryReadOnlyVaultBuffer(
                runtime._dataVault,
                in runtime._telemetryCursorHandle,
                BufferID.ShinobuHydroKccTelemetryCursor,
                SystemID.Physics,
                1,
                out NativeArray<int>.ReadOnly resolvedCursor)
                ? resolvedCursor
                : default;
            if (!telemetryBuffer.IsCreated || telemetryBuffer.Length == 0)
                return false;

            telemetry = telemetryBuffer;
            length = math.min(TelemetryCapacity, telemetry.Length);
            cursor = cursorBuffer.IsCreated && cursorBuffer.Length > 0
                ? math.clamp(cursorBuffer[0], 0, length - 1)
                : 0;

            return true;
        }

        public static bool TryReadEditorTelemetryVault(int index, out KinematicTelemetryEntry entry, out int cursor, out int length)
        {
            entry = default;
            if (!TryGetEditorTelemetryVaultView(out NativeArray<KinematicTelemetryEntry>.ReadOnly telemetry, out cursor, out length))
                return false;

            if (index < 0 || index >= length)
                return false;

            entry = telemetry[index];
            return true;
        }

        public static bool TryGetEditorEnvironmentTelemetryVaultView(out NativeArray<KccEnvironmentTelemetryEntry>.ReadOnly telemetry, out int cursor, out int length)
        {
            telemetry = default;
            cursor = 0;
            length = 0;

            HydrodynamicKccRuntime runtime = EditorActiveRuntime;
            if (runtime == null ||
                runtime._collisionScheduled ||
                runtime._postScheduled ||
                runtime._dataVault == null ||
                !IsVaultHandle(in runtime._environmentTelemetryRingHandle, BufferID.ShinobuKccEnvironmentTelemetryRing, SystemID.Physics))
            {
                return false;
            }

            if (!TryReadOnlyVaultBuffer(runtime._dataVault, in runtime._environmentTelemetryRingHandle, BufferID.ShinobuKccEnvironmentTelemetryRing, SystemID.Physics, TelemetryCapacity, out NativeArray<KccEnvironmentTelemetryEntry>.ReadOnly telemetryBuffer))
                return false;

            NativeArray<int>.ReadOnly cursorBuffer = TryReadOnlyVaultBuffer(
                runtime._dataVault,
                in runtime._environmentTelemetryCursorHandle,
                BufferID.ShinobuKccEnvironmentTelemetryCursor,
                SystemID.Physics,
                1,
                out NativeArray<int>.ReadOnly resolvedCursor)
                ? resolvedCursor
                : default;
            if (!telemetryBuffer.IsCreated || telemetryBuffer.Length == 0)
                return false;

            telemetry = telemetryBuffer;
            length = math.min(TelemetryCapacity, telemetry.Length);
            cursor = cursorBuffer.IsCreated && cursorBuffer.Length > 0
                ? math.clamp(cursorBuffer[0], 0, length - 1)
                : 0;

            return true;
        }
#endif

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _capsule);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            CacheOceanKinematicsRuntimeCold();
            _droppedSignalCount = 0;
            _globalQualityWeight = ResolveGlobalQualityWeight();
#if UNITY_EDITOR
            EditorActiveRuntime = this;
#endif
            SignalBus<WakeGeneratedSignal>.EnsureInitialized();
            EnsureVaultBuffers();
            WarmCoreBlackboxRoute();
            TryRegisterFixedTick();
            TryRegisterPostFixedTick();
            TryRegisterLateFrameTick();
            TryRegisterHotSwap();
        }

        private void OnDisable()
        {
            DrainPendingJobsForTeardown();
            _postScheduled = false;
            _collisionScheduled = false;
            ClearQueuedExternalTargets();
            ClearPendingExternalVisualTargets();
#if UNITY_EDITOR
            if (EditorActiveRuntime == this)
                EditorActiveRuntime = null;
#endif
            TryUnregisterHotSwap();
            TryUnregisterLateFrameTick();
            TryUnregisterPostFixedTick();
            TryUnregisterFixedTick();
            _oceanKinematicsService = null;
            _weatherService = null;
            ClearWaterMediumState();
            _coreBlackboxWarmed = false;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_collisionScheduled || _postScheduled || !HasVaultBuffersReady())
                return;

            bool collisionBypass = ConsumeRespawnCollisionSuspendSignals();
            int capacity = math.min(_entityCapacity, _resolvedBufferCapacity);
            int maxHits = MaxCollisionHitsPerCommand;
            int rawHitCapacity = capacity * maxHits;
            if (!TryPinScheduledVaultBuffers())
                return;

            bool scheduled = false;
            try
            {
            if (!TryOpenVaultBuffer(_dataVault, ref _statesHandle, BufferID.ShinobuHydroKccStates, SystemID.Physics, capacity, out NativeArray<KinematicStateDTO> states) ||
                !TryOpenVaultBuffer(_dataVault, ref _inputsHandle, BufferID.ShinobuHydroKccInputs, SystemID.Physics, capacity, out NativeArray<HydrodynamicKccInputDTO> inputs) ||
                !TryOpenVaultBuffer(_dataVault, ref _proposedVelocitiesHandle, BufferID.ShinobuHydroKccProposedVelocities, SystemID.Physics, capacity, out NativeArray<float3> proposed) ||
                !TryOpenVaultBuffer(_dataVault, ref _resolvedHitsHandle, BufferID.ShinobuHydroKccResolvedHits, SystemID.Physics, rawHitCapacity, out NativeArray<HydrodynamicKccCollisionHitDTO> resolvedHits) ||
                !TryOpenVaultBuffer(_dataVault, ref _faultFlagsHandle, BufferID.ShinobuHydroKccFaultFlags, SystemID.Physics, capacity, out NativeArray<HydrodynamicKccFaultFlagDTO> faults) ||
                !TryOpenVaultBuffer(_dataVault, ref _wakePacketsHandle, BufferID.ShinobuHydroKccWakePackets, SystemID.Physics, capacity, out NativeArray<HydrodynamicWakePacketDTO> wakePackets) ||
                !TryOpenVaultBuffer(_dataVault, ref _tuningHandle, BufferID.ShinobuHydroKccTuning, SystemID.Physics, 1, out NativeArray<HydrodynamicKccTuningDTO> tuningBuffer) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentProfileHandle, BufferID.ShinobuKccEnvironmentProfile, SystemID.Physics, 1, out NativeArray<KccEnvironmentProfileDTO> environmentProfile) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentGridHandle, BufferID.ShinobuKccEnvironmentGrid, SystemID.Physics, 1, out NativeArray<KccEnvironmentGridDTO> environmentGrid) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentFlowFieldHandle, BufferID.ShinobuKccEnvironmentFlowField, SystemID.Physics, EnvironmentGridCellCount, out NativeArray<float3> environmentFlow) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentSdfHandle, BufferID.ShinobuKccEnvironmentSdf, SystemID.Physics, EnvironmentGridCellCount, out NativeArray<float> environmentSdf) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentMockMetabolismHandle, BufferID.ShinobuKccEnvironmentMockMetabolism, SystemID.Physics, capacity, out NativeArray<MetabolicStateDTO> environmentMockMetabolism) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentDebugHandle, BufferID.ShinobuKccEnvironmentDebug, SystemID.Physics, capacity, out NativeArray<KccEnvironmentDebugOutputDTO> environmentDebug))
                return;

            HydrodynamicKccTuningDTO tuning = UpdateTuningSnapshot(tuningBuffer);
            double3 sectorOrigin = ResolveSectorOriginAup();
            KccEnvironmentProfileDTO environmentProfileSnapshot = UpdateEnvironmentProfileSnapshot(environmentProfile);
            KccEnvironmentGridDTO gridSnapshot = UpdateEnvironmentGridSnapshot(environmentGrid, sectorOrigin);
            uint sectorGeneration = HydrodynamicKccMath.ComputeSectorGeneration(sectorOrigin);
            NativeArray<MetabolicStateDTO> environmentMetabolism = OpenPublishedMetabolismStateView(capacity, out NativeArray<MetabolicStateDTO> publishedMetabolism)
                ? publishedMetabolism
                : environmentMockMetabolism;
            if (capacity <= 0 ||
                inputs.Length < capacity ||
                proposed.Length < capacity ||
                resolvedHits.Length < rawHitCapacity ||
                !faults.IsCreated ||
                faults.Length < capacity ||
                !wakePackets.IsCreated ||
                wakePackets.Length < capacity ||
                !environmentProfile.IsCreated ||
                environmentProfile.Length < 1 ||
                !environmentGrid.IsCreated ||
                environmentGrid.Length < 1 ||
                !environmentFlow.IsCreated ||
                environmentFlow.Length < EnvironmentGridCellCount ||
                !environmentSdf.IsCreated ||
                environmentSdf.Length < EnvironmentGridCellCount ||
                !environmentMockMetabolism.IsCreated ||
                environmentMockMetabolism.Length < capacity ||
                !environmentMetabolism.IsCreated ||
                environmentMetabolism.Length < capacity ||
                !environmentDebug.IsCreated ||
                environmentDebug.Length < capacity)
            {
                ReleaseMetabolismStateReadGuard();
                _scheduledEntityCount = 0;
                _scheduledMaxHitsPerCommand = 0;
                return;
            }

            _scheduledMaxHitsPerCommand = maxHits;
            _scheduledEntityCount = capacity;
            SeedInitialStateIfNeeded(states, tuning, sectorOrigin, capacity);
            // WATER AS MEDIUM: the four PureLogic models are evaluated here, on the main thread, BEFORE the
            // integration job is scheduled. They cannot run inside ApplyEnvironmentalForcesJob (see the
            // MediumCurrentDragForce field comment), and this is the only point in the tick where the state
            // buffer is quiescent -- the same window SeedInitialStateIfNeeded and states[0] below already use.
            UpdateWaterMediumForces(states, environmentFlow, gridSnapshot, environmentProfileSnapshot, tuning, sectorOrigin, fixedDeltaTime);
            _simulationFrame++;
            float3 externalAcceleration = _queuedExternalAcceleration;
            float3 externalVelocityChange = _queuedExternalVelocityChange;
            float3 externalVelocityTarget = _queuedExternalVelocityTarget;
            double3 externalPositionTargetAup = _queuedExternalPositionTargetAup;
            quaternion externalRotationTarget = _queuedExternalRotationTarget;
            uint externalControlFlags = _queuedExternalControlFlags;
            bool hasExternalRotationTarget = (externalControlFlags & HydrodynamicKccMath.FlagExternalRotationTarget) != 0u &&
                IsFiniteUnitQuaternion(externalRotationTarget);
            _queuedExternalAcceleration = float3.zero;
            _queuedExternalVelocityChange = float3.zero;
            _queuedExternalVelocityTarget = float3.zero;
            _queuedExternalPositionTargetAup = double3.zero;
            _queuedExternalRotationTarget = quaternion.identity;
            _queuedExternalControlFlags = 0u;
            JobHandle clearFaultsHandle = faults.IsCreated
                ? new ClearKccFaultFlagsJob { FaultFlags = faults }.Schedule(capacity, 32)
                : default;

            if (_runMockInput)
            {
                _externalInputHandle = default;
                _externalInputArmed = false;
                _inputHandle = new GenerateMockMovementInputJob
                {
                    Inputs = inputs,
                    AnchorAup = states[0].AUP_Position,
                    Tuning = tuning,
                    SimulationFrame = _simulationFrame,
                    SectorHash = sectorGeneration,
                    SimulationTickDelta = fixedDeltaTime
                }.Schedule(capacity, 32, clearFaultsHandle);
            }
            else if (_consumeExternalInputBuffer && _externalInputArmed)
            {
                _inputHandle = JobHandle.CombineDependencies(clearFaultsHandle, _externalInputHandle);
                _externalInputHandle = default;
                _externalInputArmed = false;
            }
            else
            {
                _externalInputHandle = default;
                _externalInputArmed = false;
                _inputHandle = new ClearKccInputBufferJob
                {
                    Inputs = inputs
                }.Schedule(capacity, 32, clearFaultsHandle);
            }

            _inputHandle = new SanitizeKccInputBufferJob
            {
                Inputs = inputs,
                SectorOriginAup = sectorOrigin,
                SimulationFrame = _simulationFrame,
                SectorGeneration = sectorGeneration
            }.Schedule(capacity, 32, _inputHandle);

            int environmentMockCount = math.max(EnvironmentGridCellCount, capacity);
            _environmentMockHandle = new GenerateMockEnvironmentalForcesJob
            {
                FlowField = environmentFlow,
                SdfDistances = environmentSdf,
                MockMetabolism = environmentMockMetabolism,
                Grid = gridSnapshot,
                Tuning = tuning,
                SimulationFrame = _simulationFrame,
                SimulationTickDelta = fixedDeltaTime
            }.Schedule(environmentMockCount, 64, clearFaultsHandle);

            _integrationHandle = new ApplyEnvironmentalForcesJob
            {
                States = states,
                Inputs = inputs,
                EnvironmentProfiles = environmentProfile,
                EnvironmentGrids = environmentGrid,
                FlowField = environmentFlow,
                SdfDistances = environmentSdf,
                MetabolismStates = environmentMetabolism,
                ProposedVelocities = proposed,
                WakePackets = wakePackets,
                EnvironmentDebugOutputs = environmentDebug,
                FaultFlags = faults,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                ExternalAcceleration = externalAcceleration,
                ExternalVelocityChange = externalVelocityChange,
                ExternalVelocityTarget = externalVelocityTarget,
                ExternalPositionTargetAup = externalPositionTargetAup,
                SimulationFrame = _simulationFrame,
                ExternalControlFlags = externalControlFlags,
                SimulationTickDelta = fixedDeltaTime,
                MediumCurrentDragForce = _mediumCurrentDragForce,
                MediumThermoclineResistance01 = _mediumThermoclineResistance01,
                MediumDensityBuoyancyNewtons = _mediumDensityBuoyancyNewtons,
                MediumFlags = _mediumFlags
            }.Schedule(capacity, 32, JobHandle.CombineDependencies(_inputHandle, _environmentMockHandle));

            if (collisionBypass)
            {
                _scheduledMaxHitsPerCommand = 0;
                _commandHandle = _integrationHandle;
                _collisionHandle = _integrationHandle;
                _collisionScheduled = true;
                QueuePendingExternalRotationVisual(hasExternalRotationTarget, externalRotationTarget);
                scheduled = true;
                return;
            }

            _commandHandle = _integrationHandle;
            _collisionHandle = new BuildSdfCollisionHitsJob
            {
                States = states,
                ProposedVelocities = proposed,
                EnvironmentGrids = environmentGrid,
                SdfDistances = environmentSdf,
                Hits = resolvedHits,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                SimulationTickDelta = fixedDeltaTime,
                MaxHitsPerEntity = maxHits
            }.Schedule(capacity, 32, _integrationHandle);
            _collisionScheduled = true;
            QueuePendingExternalRotationVisual(hasExternalRotationTarget, externalRotationTarget);
            scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseScheduledVaultBufferPins();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_collisionScheduled || _postScheduled)
                return;

            if (_dataVault == null)
            {
                TryAbortScheduledBatchNoWait();
                return;
            }

            int capacity = math.max(0, _scheduledEntityCount);
            bool collisionBypass = _respawnCollisionBypassFrames > 0 || _scheduledMaxHitsPerCommand <= 0;
            int maxHits = collisionBypass ? 1 : math.clamp(_scheduledMaxHitsPerCommand, 1, MaxCollisionHitsPerCommand);
            int bypassVisualSync = _rollbackVisualBypassFrames > 0 ? 1 : 0;
            int hitOpenLength = collisionBypass ? 1 : capacity * maxHits;
            int rollbackByteCapacity = capacity * UnsafeUtility.SizeOf<KinematicStateDTO>();
            if (capacity <= 0 ||
                !TryOpenVaultBuffer(_dataVault, ref _statesHandle, BufferID.ShinobuHydroKccStates, SystemID.Physics, capacity, out NativeArray<KinematicStateDTO> states) ||
                !TryOpenVaultBuffer(_dataVault, ref _proposedVelocitiesHandle, BufferID.ShinobuHydroKccProposedVelocities, SystemID.Physics, capacity, out NativeArray<float3> proposed) ||
                !TryOpenVaultBuffer(_dataVault, ref _resolvedHitsHandle, BufferID.ShinobuHydroKccResolvedHits, SystemID.Physics, hitOpenLength, out NativeArray<HydrodynamicKccCollisionHitDTO> resolvedHits) ||
                !TryOpenVaultBuffer(_dataVault, ref _previousAupHandle, BufferID.ShinobuHydroKccPreviousAup, SystemID.Physics, capacity, out NativeArray<double3> previous) ||
                !TryOpenVaultBuffer(_dataVault, ref _visualOutputsHandle, BufferID.ShinobuHydroKccVisualOutputs, SystemID.Physics, capacity, out NativeArray<HydrodynamicKccVisualOutputDTO> visual) ||
                !TryOpenVaultBuffer(_dataVault, ref _telemetryRingHandle, BufferID.ShinobuHydroKccTelemetryRing, SystemID.Physics, TelemetryCapacity, out NativeArray<KinematicTelemetryEntry> telemetry) ||
                !TryOpenVaultBuffer(_dataVault, ref _telemetryCursorHandle, BufferID.ShinobuHydroKccTelemetryCursor, SystemID.Physics, 1, out NativeArray<int> cursor) ||
                !TryOpenVaultBuffer(_dataVault, ref _faultFlagsHandle, BufferID.ShinobuHydroKccFaultFlags, SystemID.Physics, capacity, out NativeArray<HydrodynamicKccFaultFlagDTO> faults) ||
                !TryOpenVaultBuffer(_dataVault, ref _rollbackBytesHandle, BufferID.ShinobuHydroKccRollbackBytes, SystemID.Physics, rollbackByteCapacity, out NativeArray<byte> rollbackBytes) ||
                !TryOpenVaultBuffer(_dataVault, ref _wakePacketsHandle, BufferID.ShinobuHydroKccWakePackets, SystemID.Physics, capacity, out NativeArray<HydrodynamicWakePacketDTO> wakePackets) ||
                !TryOpenVaultBuffer(_dataVault, ref _debugOutputsHandle, BufferID.ShinobuHydroKccDebugOutputs, SystemID.Physics, capacity, out NativeArray<HydrodynamicKccDebugOutputDTO> debugOutputs) ||
                !TryOpenVaultBuffer(_dataVault, ref _tuningHandle, BufferID.ShinobuHydroKccTuning, SystemID.Physics, 1, out NativeArray<HydrodynamicKccTuningDTO> tuningBuffer) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentProfileHandle, BufferID.ShinobuKccEnvironmentProfile, SystemID.Physics, 1, out NativeArray<KccEnvironmentProfileDTO> environmentProfile) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentDebugHandle, BufferID.ShinobuKccEnvironmentDebug, SystemID.Physics, capacity, out NativeArray<KccEnvironmentDebugOutputDTO> environmentDebug) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentTelemetryRingHandle, BufferID.ShinobuKccEnvironmentTelemetryRing, SystemID.Physics, TelemetryCapacity, out NativeArray<KccEnvironmentTelemetryEntry> environmentTelemetry) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentTelemetryCursorHandle, BufferID.ShinobuKccEnvironmentTelemetryCursor, SystemID.Physics, 1, out NativeArray<int> environmentCursor))
            {
                TryAbortScheduledBatchNoWait();
                return;
            }

            HydrodynamicKccTuningDTO tuning = UpdateTuningSnapshot(tuningBuffer);
            double3 sectorOrigin = ResolveSectorOriginAup();
            int executedIterations = collisionBypass ? 0 : math.clamp(maxHits, 1, MaxCollisionHitsPerCommand);

            _sdfCollisionHandle = _collisionHandle;
            JobHandle resolutionDependency = _sdfCollisionHandle;

            JobHandle slopeHandle = new EvaluateSlopeFrictionJob
            {
                ProposedVelocities = proposed,
                CollisionHits = resolvedHits,
                EnvironmentProfiles = environmentProfile,
                EnvironmentDebugOutputs = environmentDebug,
                FaultFlags = faults,
                Tuning = tuning,
                SimulationFrame = _simulationFrame,
                SimulationTickDelta = fixedDeltaTime,
                MaxHitsPerCommand = maxHits,
                CollisionBypass = collisionBypass ? 1 : 0
            }.Schedule(capacity, 32, resolutionDependency);

            JobHandle resolutionHandle = new KinematicResolutionJob
            {
                States = states,
                PreviousAup = previous,
                ProposedVelocities = proposed,
                CollisionHits = resolvedHits,
                DebugOutputs = debugOutputs,
                FaultFlags = faults,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                SimulationFrame = _simulationFrame,
                SimulationTickDelta = fixedDeltaTime,
                MaxHitsPerCommand = maxHits,
                CollisionBypass = collisionBypass ? 1 : 0
            }.Schedule(capacity, 32, slopeHandle);

            JobHandle visualHandle = new KinematicVisualSyncJob
            {
                States = states,
                PreviousAup = previous,
                VisualOutputs = visual,
                CameraOrSectorAup = sectorOrigin,
                Tuning = tuning,
                SimulationFrame = _simulationFrame,
                VisualDeltaTime = fixedDeltaTime,
                BypassVisualSync = (byte)bypassVisualSync
            }.Schedule(capacity, 32, resolutionHandle);

            JobHandle telemetryHandle = new KinematicTelemetryAggregateJob
            {
                States = states,
                DebugOutputs = debugOutputs,
                FaultFlags = faults,
                TelemetryRing = telemetry,
                TelemetryCursor = cursor,
                Tuning = tuning,
                SimulationFrame = _simulationFrame,
                EntityCount = capacity,
                ExecutedIterations = executedIterations
            }.Schedule(resolutionHandle);

            JobHandle environmentTelemetryHandle = new KccEnvironmentTelemetryAggregateJob
            {
                States = states,
                EnvironmentDebugOutputs = environmentDebug,
                FaultFlags = faults,
                TelemetryRing = environmentTelemetry,
                TelemetryCursor = environmentCursor,
                SimulationFrame = _simulationFrame,
                EntityCount = capacity
            }.Schedule(resolutionHandle);

            JobHandle rollbackHandle = new KinematicRollbackFenceJob
            {
                States = states,
                RollbackBytes = rollbackBytes,
                EntityCount = capacity
            }.Schedule(resolutionHandle);

            JobHandle wakeHandle = new EmitWakeSignalsJob
            {
                WakePackets = wakePackets,
                FaultFlags = faults,
                WakeWriter = SignalBus<WakeGeneratedSignal>.ParallelWriter,
                WakeWriterBudget = SignalBus<WakeGeneratedSignal>.ParallelWriterBudget
            }.Schedule(capacity, 32, resolutionHandle);

            JobHandle telemetryCombinedHandle = JobHandle.CombineDependencies(telemetryHandle, environmentTelemetryHandle);
            _postSimulationHandle = JobHandle.CombineDependencies(JobHandle.CombineDependencies(visualHandle, rollbackHandle, wakeHandle), telemetryCombinedHandle);
            if (_rollbackVisualBypassFrames > 0)
                _rollbackVisualBypassFrames--;
            if (collisionBypass && _respawnCollisionBypassFrames > 0)
                _respawnCollisionBypassFrames--;
            _postScheduled = true;
            _collisionScheduled = false;
            _scheduledEntityCount = 0;
            _scheduledMaxHitsPerCommand = 0;
        }

        public bool TryRunRollbackResimulation(int requestedFrames, float fixedDeltaTime)
        {
            if (requestedFrames <= 0 || !Application.isPlaying)
                return false;

            if (!TryAbortScheduledBatchNoWait())
                return false;

            _postScheduled = false;
            _collisionScheduled = false;
            _scheduledEntityCount = 0;
            _scheduledMaxHitsPerCommand = 0;
            int maxFrames = math.max(1, _maxRollbackFastForwardFrames);
            int frames = math.clamp(requestedFrames, 1, maxFrames);
            _rollbackVisualBypassFrames = math.max(_rollbackVisualBypassFrames, frames);

            // Suppresses crush-damage ingress for the replayed frames only; try/finally because the loop has
            // three early-exit paths and a stuck flag would disable depth damage for the rest of the session.
            _rollbackResimulationActive = true;
            try
            {
                for (int i = 0; i < frames; i++)
                {
                    uint beforeFrame = _simulationFrame;
                    FixedTick(fixedDeltaTime);
                    if (_simulationFrame == beforeFrame || !_collisionScheduled)
                    {
                        _rollbackVisualBypassFrames = 0;
                        return false;
                    }

                    PostFixedTick(fixedDeltaTime);
                    if (!_postScheduled)
                    {
                        _rollbackVisualBypassFrames = 0;
                        return false;
                    }

                    if (!DispatcherJobFence.TryFinalizeCompleted(ref _postSimulationHandle))
                    {
                        _rollbackVisualBypassFrames = 0;
                        return false;
                    }

                    ReleaseMetabolismStateReadGuard();
                    ReleaseScheduledVaultBufferPins();
                    _postScheduled = false;
                    _collisionScheduled = false;
                    _scheduledEntityCount = 0;
                    _scheduledMaxHitsPerCommand = 0;
                }
            }
            finally
            {
                _rollbackResimulationActive = false;
            }

            return true;
        }

        public bool TryRegisterExternalInputWriter(JobHandle writerHandle)
        {
            if (!Application.isPlaying || _runMockInput || !_consumeExternalInputBuffer || _collisionScheduled || _postScheduled)
                return false;

            _externalInputHandle = _externalInputArmed
                ? JobHandle.CombineDependencies(_externalInputHandle, writerHandle)
                : writerHandle;
            _externalInputArmed = true;
            return true;
        }

        public bool TryQueueExternalAcceleration(Vector3 acceleration)
        {
            if (!IsAuthorityRouteActive || !MathGuard.TryAcceptFinite(acceleration, out Vector3 acceptedAcceleration))
                return false;

            _queuedExternalAcceleration = HydrodynamicKccMath.Sanitize(
                _queuedExternalAcceleration + new float3(acceptedAcceleration.x, acceptedAcceleration.y, acceptedAcceleration.z),
                float3.zero);
            _queuedExternalControlFlags |= HydrodynamicKccMath.FlagExternalAcceleration;
            return true;
        }

        public bool TryQueueExternalVelocityChange(Vector3 velocityChange)
        {
            if (!IsAuthorityRouteActive || !MathGuard.TryAcceptFinite(velocityChange, out Vector3 acceptedVelocityChange))
                return false;

            _queuedExternalVelocityChange = HydrodynamicKccMath.Sanitize(
                _queuedExternalVelocityChange + new float3(acceptedVelocityChange.x, acceptedVelocityChange.y, acceptedVelocityChange.z),
                float3.zero);
            _queuedExternalControlFlags |= HydrodynamicKccMath.FlagExternalVelocityChange;
            return true;
        }

        public bool TryQueueExternalVelocityTarget(Vector3 velocityTarget)
        {
            if (!IsAuthorityRouteActive || !MathGuard.TryAcceptFinite(velocityTarget, out Vector3 acceptedVelocityTarget))
                return false;

            _queuedExternalVelocityTarget = new float3(acceptedVelocityTarget.x, acceptedVelocityTarget.y, acceptedVelocityTarget.z);
            _queuedExternalControlFlags |= HydrodynamicKccMath.FlagExternalVelocityTarget;
            return true;
        }

        public bool TryQueueExternalPositionTarget(Vector3 runtimePosition)
        {
            if (!IsAuthorityRouteActive ||
                !TryResolveRuntimePositionTargetAup(runtimePosition, out Vector3 acceptedRuntimePosition, out double3 absolute))
                return false;

            _queuedExternalPositionTargetAup = absolute;
            _queuedExternalControlFlags |= HydrodynamicKccMath.FlagExternalPositionTarget;
            QueuePendingExternalPositionVisual(acceptedRuntimePosition);
            return true;
        }

        public bool TryQueueExternalRotationTarget(Quaternion rotation)
        {
            if (!IsAuthorityRouteActive || !TryNormalizeRotation(rotation, out quaternion normalizedRotation))
                return false;

            _queuedExternalRotationTarget = normalizedRotation;
            _queuedExternalControlFlags |= HydrodynamicKccMath.FlagExternalRotationTarget;
            QueuePendingExternalRotationVisual(true, normalizedRotation);
            return true;
        }

        public bool TryQueueExternalPoseTarget(Vector3 runtimePosition, Quaternion rotation)
        {
            if (!IsAuthorityRouteActive ||
                !TryResolveRuntimePositionTargetAup(runtimePosition, out Vector3 acceptedRuntimePosition, out double3 absolute) ||
                !TryNormalizeRotation(rotation, out quaternion normalizedRotation))
                return false;

            _queuedExternalPositionTargetAup = absolute;
            _queuedExternalRotationTarget = normalizedRotation;
            _queuedExternalControlFlags |= HydrodynamicKccMath.FlagExternalPositionTarget |
                HydrodynamicKccMath.FlagExternalRotationTarget;
            QueuePendingExternalPositionVisual(acceptedRuntimePosition);
            QueuePendingExternalRotationVisual(true, normalizedRotation);
            return true;
        }

        private static bool TryResolveRuntimePositionTargetAup(
            Vector3 runtimePosition,
            out Vector3 acceptedRuntimePosition,
            out double3 absolute)
        {
            acceptedRuntimePosition = default;
            absolute = double3.zero;
            if (!MathGuard.TryAcceptFinite(runtimePosition, out acceptedRuntimePosition))
                return false;

            AbsoluteUniversePosition aup = default;
            if (!RuntimeOriginRoute.TryRuntimePositionToAup(acceptedRuntimePosition, ref aup))
                return false;

            absolute = aup.ToAbsoluteDouble3();
            return HydrodynamicKccMath.IsFinite(absolute);
        }

        private static bool TryNormalizeRotation(Quaternion rotation, out quaternion normalizedRotation)
        {
            float4 value = new float4(rotation.x, rotation.y, rotation.z, rotation.w);
            float lengthSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lengthSq) || lengthSq <= MinQuaternionLengthSq)
            {
                normalizedRotation = quaternion.identity;
                return false;
            }

            value *= math.rsqrt(math.max(lengthSq, MinQuaternionLengthSq));
            if (value.w < 0f)
                value = -value;

            normalizedRotation = new quaternion(value);
            return true;
        }

        private static bool IsFiniteUnitQuaternion(quaternion rotation)
        {
            float4 value = rotation.value;
            float lengthSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) &&
                math.isfinite(lengthSq) &&
                lengthSq > MinQuaternionLengthSq;
        }

        private static Quaternion ToUnityQuaternion(quaternion rotation)
        {
            return new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
        }

        private void QueuePendingExternalPositionVisual(Vector3 runtimePosition)
        {
            _pendingVisualPositionTarget = runtimePosition;
            _hasPendingVisualPositionTarget = true;
            ApplyPendingExternalPositionVisual(clearAfterApply: false);
        }

        private void QueuePendingExternalRotationVisual(bool hasExternalRotationTarget, quaternion rotation)
        {
            if (!hasExternalRotationTarget)
            {
                ClearPendingExternalRotationVisual();
                return;
            }

            _pendingVisualRotationTarget = rotation;
            _hasPendingVisualRotationTarget = true;
            ApplyPendingExternalRotationVisual(clearAfterApply: false);
        }

        private void ApplyPendingExternalPositionVisual(bool clearAfterApply)
        {
            if (!_hasPendingVisualPositionTarget || !_applyVisualToTransform || _cachedTransform == null)
                return;

            _cachedTransform.position = _pendingVisualPositionTarget;
            if (clearAfterApply)
                ClearPendingExternalPositionVisual();
        }

        private void ApplyPendingExternalRotationVisual(bool clearAfterApply)
        {
            if (!_hasPendingVisualRotationTarget || !_applyVisualToTransform || _cachedTransform == null)
                return;

            _cachedTransform.rotation = ToUnityQuaternion(_pendingVisualRotationTarget);
            if (clearAfterApply)
                ClearPendingExternalRotationVisual();
        }

        private void ClearPendingExternalPositionVisual()
        {
            _pendingVisualPositionTarget = default;
            _hasPendingVisualPositionTarget = false;
        }

        private void ClearPendingExternalRotationVisual()
        {
            _pendingVisualRotationTarget = quaternion.identity;
            _hasPendingVisualRotationTarget = false;
        }

        private void ClearPendingExternalVisualTargets()
        {
            ClearPendingExternalPositionVisual();
            ClearPendingExternalRotationVisual();
        }

        private void ClearQueuedExternalTargets()
        {
            _queuedExternalAcceleration = float3.zero;
            _queuedExternalVelocityChange = float3.zero;
            _queuedExternalVelocityTarget = float3.zero;
            _queuedExternalPositionTargetAup = double3.zero;
            _queuedExternalRotationTarget = quaternion.identity;
            _queuedExternalControlFlags = 0u;
        }

        public uint ResolveNextInputFrame()
        {
            return _simulationFrame + 1u;
        }

        public uint ResolveCurrentInputGeneration()
        {
            return HydrodynamicKccMath.ComputeSectorGeneration(ResolveSectorOriginAup());
        }

        public void LateFrameTick()
        {
            if (!_postScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _postSimulationHandle))
                return;

            ReleaseMetabolismStateReadGuard();
            ReleaseScheduledVaultBufferPins();
            _postScheduled = false;
            if (!HasVaultBuffersReady())
            {
                ClearPendingExternalVisualTargets();
                return;
            }

            int entityCapacity = math.max(DefaultCapacity, _entityCapacity);
            if (!TryOpenVaultBuffer(_dataVault, ref _visualOutputsHandle, BufferID.ShinobuHydroKccVisualOutputs, SystemID.Physics, 1, out NativeArray<HydrodynamicKccVisualOutputDTO> visual) ||
                !TryOpenVaultBuffer(_dataVault, ref _statesHandle, BufferID.ShinobuHydroKccStates, SystemID.Physics, 1, out NativeArray<KinematicStateDTO> states) ||
                !TryOpenVaultBuffer(_dataVault, ref _debugOutputsHandle, BufferID.ShinobuHydroKccDebugOutputs, SystemID.Physics, 1, out NativeArray<HydrodynamicKccDebugOutputDTO> debugOutputs) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentDebugHandle, BufferID.ShinobuKccEnvironmentDebug, SystemID.Physics, 1, out NativeArray<KccEnvironmentDebugOutputDTO> environmentDebugOutputs) ||
                !TryOpenVaultBuffer(_dataVault, ref _faultFlagsHandle, BufferID.ShinobuHydroKccFaultFlags, SystemID.Physics, entityCapacity, out NativeArray<HydrodynamicKccFaultFlagDTO> faults) ||
                !TryOpenVaultBuffer(_dataVault, ref _telemetryRingHandle, BufferID.ShinobuHydroKccTelemetryRing, SystemID.Physics, TelemetryCapacity, out NativeArray<KinematicTelemetryEntry> telemetry) ||
                !TryOpenVaultBuffer(_dataVault, ref _environmentTelemetryRingHandle, BufferID.ShinobuKccEnvironmentTelemetryRing, SystemID.Physics, TelemetryCapacity, out NativeArray<KccEnvironmentTelemetryEntry> environmentTelemetry))
            {
                ClearPendingExternalVisualTargets();
                return;
            }

            int faultMask = ResolveFaultMask(faults);
            if (faultMask != 0 && faultMask != _dumpedFaultMask)
            {
                DumpTelemetry(faultMask, telemetry, environmentTelemetry);
                _dumpedFaultMask = faultMask;
            }
            else if (faultMask == 0)
            {
                _dumpedFaultMask = 0;
            }

            if (!visual.IsCreated || visual.Length == 0)
            {
                ClearPendingExternalVisualTargets();
                return;
            }

            HydrodynamicKccVisualOutputDTO output = visual[0];
            PublishKccVelocitySnapshot(states);
            if (debugOutputs.IsCreated && debugOutputs.Length > 0)
            {
                HydrodynamicKccDebugOutputDTO debug = debugOutputs[0];
                _lastGizmoCurrent = debug.CurrentLocal;
                _lastGizmoPredicted = debug.PredictedLocal;
                _lastGizmoNormal = debug.CollisionNormal;
            }
            else
            {
                _lastGizmoCurrent = output.PreviousLocalPosition;
                _lastGizmoPredicted = output.LocalPosition;
                _lastGizmoNormal = float3.zero;
            }
            if (environmentDebugOutputs.IsCreated && environmentDebugOutputs.Length > 0)
            {
                KccEnvironmentDebugOutputDTO environmentDebug = environmentDebugOutputs[0];
                _lastGizmoFlow = environmentDebug.AppliedFlow;
                _lastGizmoSlopeSlide = environmentDebug.SlopeSlideVector;
            }
            else
            {
                _lastGizmoFlow = float3.zero;
                _lastGizmoSlopeSlide = float3.zero;
            }

            if (!_applyVisualToTransform || _cachedTransform == null)
            {
                ClearPendingExternalVisualTargets();
                return;
            }

            Vector3 local = new Vector3(output.LocalPosition.x, output.LocalPosition.y, output.LocalPosition.z);
            _cachedTransform.localPosition = local;
            ApplyPendingExternalPositionVisual(clearAfterApply: true);
            ApplyPendingExternalRotationVisual(clearAfterApply: true);
        }

        private void PublishKccVelocitySnapshot(NativeArray<KinematicStateDTO> states)
        {
            if (!states.IsCreated || states.Length == 0)
                return;

            KinematicStateDTO state = states[0];
            if (!HydrodynamicKccMath.IsFinite(state.AUP_Position) || !HydrodynamicKccMath.IsFinite(state.Velocity))
                return;

            AbsoluteUniversePosition bodyAup = HydrodynamicKccMath.ToAup48(state.AUP_Position);
            byte qualityPressureQ8 = ResolveQualityPressureQ8(_globalQualityWeight);
            KccVelocitySignal signal = default;
            signal.BodyAup = bodyAup;
            signal.Velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
            float planarVelocityX = signal.Velocity.x;
            float planarVelocityZ = signal.Velocity.z;
            signal.PlanarSpeedSq = (planarVelocityX * planarVelocityX) + (planarVelocityZ * planarVelocityZ);
            signal.Frame = _simulationFrame;
            signal.SourceId = HydrodynamicKccMath.SourceHash;
            signal.Flags = 0;
            signal.QualityPressureQ8 = qualityPressureQ8;
            bool accepted = CoreDeterminismSignals.TryPublishKccVelocity(in signal);
            if (!accepted)
                IncrementDroppedSignalCount();
        }

        private static byte ResolveQualityPressureQ8(float qualityWeight01)
        {
            float quality = math.saturate(math.select(1f, qualityWeight01, math.isfinite(qualityWeight01)));
            float survivalPressure01 = math.saturate((0.35f - quality) * math.rcp(0.35f));
            float curvedPressure01 = survivalPressure01 * survivalPressure01 * (3f - (2f * survivalPressure01));
            return (byte)math.clamp((int)math.round(curvedPressure01 * 255f), 0, 255);
        }

        private void IncrementDroppedSignalCount()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;

            PatchLatestTelemetryFlag(HydrodynamicKccMath.FlagSignalDrop);
        }

        private void PatchLatestTelemetryFlag(uint flag)
        {
            if (flag == 0u ||
                _dataVault == null ||
                !TryOpenVaultBuffer(_dataVault, ref _telemetryRingHandle, BufferID.ShinobuHydroKccTelemetryRing, SystemID.Physics, TelemetryCapacity, out NativeArray<KinematicTelemetryEntry> telemetry) ||
                !TryOpenVaultBuffer(_dataVault, ref _telemetryCursorHandle, BufferID.ShinobuHydroKccTelemetryCursor, SystemID.Physics, 1, out NativeArray<int> cursor) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0 ||
                !cursor.IsCreated ||
                cursor.Length <= 0)
            {
                return;
            }

            int index = math.clamp(cursor[0], 0, telemetry.Length - 1);
            KinematicTelemetryEntry entry = telemetry[index];
            entry.Flags |= flag;
            telemetry[index] = entry;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DrainPendingJobsForTeardown();
                _postScheduled = false;
                _collisionScheduled = false;
                ClearQueuedExternalTargets();
                ClearPendingExternalVisualTargets();
                _scheduledEntityCount = 0;
                _scheduledMaxHitsPerCommand = 0;
                ResetVaultHandles();
                _dataVault = currentService as IDataVault;
                EnsureVaultBuffers();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
            {
                _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                return;
            }

            // Weather was cold-cached in CacheOceanKinematicsRuntimeCold but had no rebind branch, so after a
            // weather hot swap IsThermoclineWeatherActive kept reading the replaced director's last state mask
            // for the rest of the session -- and with _requireThermoclineWeatherState defaulting to true, that
            // silently disables the thermocline band instead of failing loudly.
            if (serviceSlot == GlobalRegistryServiceSlot.Weather)
            {
                _weatherService = currentService as IWeatherService;
                _mediumThermoclineResistance01 = 0f;
                _mediumFlags &= ~HydrodynamicKccMath.FlagMediumThermocline;
            }
        }

#if UNITY_EDITOR
        /// <summary>Parses designer fluid-profile CSV bytes into Vault-backed profile and bucket buffers.</summary>
        public bool TryIngestFluidProfiles(ReadOnlySpan<byte> csvBytes)
        {
            if (csvBytes.Length == 0 || !EnsureVaultBuffers())
                return false;

            Span<HydrodynamicFluidProfileDTO> profileScratch = stackalloc HydrodynamicFluidProfileDTO[DefaultFluidProfileCapacity];
            Span<int> bucketScratch = stackalloc int[DefaultFluidProfileBucketCount];
            int count = HydrodynamicFluidProfileCsvParser.ParseProfiles(csvBytes, profileScratch, bucketScratch);
            if (count <= 0)
                return false;

            uint firstProfileHash = profileScratch[0].ProfileHash;
            return TryCommitFluidProfileScratch(profileScratch, bucketScratch, firstProfileHash);
        }

        /// <summary>Applies a previously ingested fluid profile to the Vault-backed KCC tuning record.</summary>
        public bool TryApplyFluidProfile(uint profileHash)
        {
            if (profileHash == 0u || !EnsureVaultBuffers())
                return false;

            IDataVault vault = _dataVault;
            return vault != null && TryApplyFluidProfileFromVault(vault, profileHash);
        }

        /// <summary>Parses locomotion_environment_profiles.csv bytes into Vault-backed environmental profile buffers.</summary>
        public bool TryIngestEnvironmentProfiles(ReadOnlySpan<byte> csvBytes)
        {
            if (csvBytes.Length == 0 || !EnsureVaultBuffers())
                return false;

            Span<KccEnvironmentProfileDTO> profileScratch = stackalloc KccEnvironmentProfileDTO[DefaultFluidProfileCapacity];
            Span<uint> hashScratch = stackalloc uint[DefaultFluidProfileCapacity];
            Span<int> bucketScratch = stackalloc int[DefaultFluidProfileBucketCount];
            int count = KccEnvironmentProfileCsvParser.ParseProfiles(csvBytes, profileScratch, hashScratch, bucketScratch);
            if (count <= 0)
                return false;

            return TryCommitEnvironmentProfileScratch(profileScratch, hashScratch, bucketScratch, SanitizeEnvironmentProfile(profileScratch[0]));
        }
#endif

        /// <summary>Applies a cold-ingested locomotion environment profile by FNV-1a hash bucket.</summary>
        public bool TryApplyEnvironmentProfile(uint profileHash)
        {
            if (profileHash == 0u || !EnsureVaultBuffers())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryReadOnlyVaultBuffer(vault, in _environmentProfilesHandle, BufferID.ShinobuKccEnvironmentProfiles, SystemID.Physics, DefaultFluidProfileCapacity, out NativeArray<KccEnvironmentProfileDTO>.ReadOnly profiles) ||
                !TryReadOnlyVaultBuffer(vault, in _environmentProfileBucketsHandle, BufferID.ShinobuKccEnvironmentProfileBuckets, SystemID.Physics, DefaultFluidProfileBucketCount, out NativeArray<int>.ReadOnly buckets) ||
                !TryReadOnlyVaultBuffer(vault, in _environmentProfileHashesHandle, BufferID.ShinobuKccEnvironmentProfileHashes, SystemID.Physics, DefaultFluidProfileCapacity, out NativeArray<uint>.ReadOnly hashes))
            {
                return false;
            }

            int profileIndex = FindEnvironmentProfileIndex(profileHash, hashes, buckets);
            if (profileIndex < 0 || profileIndex >= profiles.Length)
                return false;

            KccEnvironmentProfileDTO activeProfile = SanitizeEnvironmentProfile(profiles[profileIndex]);
            return TryWriteActiveEnvironmentProfile(vault, in activeProfile);
        }

        private bool TryCommitFluidProfileScratch(
            ReadOnlySpan<HydrodynamicFluidProfileDTO> profiles,
            ReadOnlySpan<int> buckets,
            uint activeProfileHash)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                profiles.Length < DefaultFluidProfileCapacity ||
                buckets.Length < DefaultFluidProfileBucketCount)
            {
                return false;
            }

            return TryWriteFluidProfiles(vault, profiles) &&
                   TryWriteFluidProfileBuckets(vault, buckets) &&
                   TryApplyFluidProfileFromVault(vault, activeProfileHash);
        }

        private bool TryApplyFluidProfileFromVault(IDataVault vault, uint profileHash)
        {
            if (!TryReadOnlyVaultBuffer(vault, in _fluidProfilesHandle, BufferID.ShinobuHydroKccFluidProfiles, SystemID.Physics, DefaultFluidProfileCapacity, out NativeArray<HydrodynamicFluidProfileDTO>.ReadOnly profiles) ||
                !TryReadOnlyVaultBuffer(vault, in _fluidProfileBucketsHandle, BufferID.ShinobuHydroKccFluidProfileBuckets, SystemID.Physics, DefaultFluidProfileBucketCount, out NativeArray<int>.ReadOnly buckets) ||
                !TryReadOnlyVaultBuffer(vault, in _tuningHandle, BufferID.ShinobuHydroKccTuning, SystemID.Physics, 1, out NativeArray<HydrodynamicKccTuningDTO>.ReadOnly tuningBuffer))
            {
                return false;
            }

            int bucket = (int)(profileHash % (uint)buckets.Length);
            int profileIndex = buckets[bucket];
            int guard = profiles.Length;
            while (profileIndex >= 0 && profileIndex < profiles.Length && guard > 0)
            {
                HydrodynamicFluidProfileDTO profile = profiles[profileIndex];
                if (profile.ProfileHash == profileHash && (profile.Flags & 1u) != 0u)
                {
                    HydrodynamicKccTuningDTO tuning = tuningBuffer[0];
                    tuning.BaseDrag = profile.BaseDrag;
                    tuning.FluidDensity = profile.FluidDensity;
                    tuning.MaxSpeed = profile.MaxSpeed;
                    tuning.GravityMultiplier = profile.GravityMultiplier;
                    tuning.BuoyancyScalar = profile.BuoyancyScalar;
                    tuning.ProfileHash = profile.ProfileHash;
                    tuning.Flags |= 1u;
                    return TryWriteActiveFluidTuning(vault, in tuning);
                }

                profileIndex = profile.NextIndex;
                guard--;
            }

            return false;
        }

        private bool TryCommitEnvironmentProfileScratch(
            ReadOnlySpan<KccEnvironmentProfileDTO> profiles,
            ReadOnlySpan<uint> hashes,
            ReadOnlySpan<int> buckets,
            in KccEnvironmentProfileDTO activeProfile)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                profiles.Length < DefaultFluidProfileCapacity ||
                hashes.Length < DefaultFluidProfileCapacity ||
                buckets.Length < DefaultFluidProfileBucketCount)
            {
                return false;
            }

            return TryWriteEnvironmentProfiles(vault, profiles) &&
                   TryWriteEnvironmentProfileHashes(vault, hashes) &&
                   TryWriteEnvironmentProfileBuckets(vault, buckets) &&
                   TryWriteActiveEnvironmentProfile(vault, in activeProfile);
        }

        private bool TryWriteFluidProfiles(IDataVault vault, ReadOnlySpan<HydrodynamicFluidProfileDTO> source)
        {
            if (!IsVaultHandle(in _fluidProfilesHandle, BufferID.ShinobuHydroKccFluidProfiles, SystemID.Physics) ||
                !vault.TryAcquireWriteLock(in _fluidProfilesHandle, SystemID.Physics, out NativeArray<HydrodynamicFluidProfileDTO> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < DefaultFluidProfileCapacity)
                    return false;

                for (int i = 0; i < DefaultFluidProfileCapacity; i++)
                    destination[i] = source[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _fluidProfilesHandle, SystemID.Physics);
            }
        }

        private bool TryWriteFluidProfileBuckets(IDataVault vault, ReadOnlySpan<int> source)
        {
            if (!IsVaultHandle(in _fluidProfileBucketsHandle, BufferID.ShinobuHydroKccFluidProfileBuckets, SystemID.Physics) ||
                !vault.TryAcquireWriteLock(in _fluidProfileBucketsHandle, SystemID.Physics, out NativeArray<int> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < DefaultFluidProfileBucketCount)
                    return false;

                for (int i = 0; i < DefaultFluidProfileBucketCount; i++)
                    destination[i] = source[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _fluidProfileBucketsHandle, SystemID.Physics);
            }
        }

        private bool TryWriteActiveFluidTuning(IDataVault vault, in HydrodynamicKccTuningDTO tuning)
        {
            if (!IsVaultHandle(in _tuningHandle, BufferID.ShinobuHydroKccTuning, SystemID.Physics) ||
                !vault.TryAcquireWriteLock(in _tuningHandle, SystemID.Physics, out NativeArray<HydrodynamicKccTuningDTO> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < 1)
                    return false;

                destination[0] = SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.Physics);
            }
        }

        private bool TryWriteEnvironmentProfiles(IDataVault vault, ReadOnlySpan<KccEnvironmentProfileDTO> source)
        {
            if (!IsVaultHandle(in _environmentProfilesHandle, BufferID.ShinobuKccEnvironmentProfiles, SystemID.Physics) ||
                !vault.TryAcquireWriteLock(in _environmentProfilesHandle, SystemID.Physics, out NativeArray<KccEnvironmentProfileDTO> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < DefaultFluidProfileCapacity)
                    return false;

                for (int i = 0; i < DefaultFluidProfileCapacity; i++)
                    destination[i] = source[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _environmentProfilesHandle, SystemID.Physics);
            }
        }

        private bool TryWriteEnvironmentProfileHashes(IDataVault vault, ReadOnlySpan<uint> source)
        {
            if (!IsVaultHandle(in _environmentProfileHashesHandle, BufferID.ShinobuKccEnvironmentProfileHashes, SystemID.Physics) ||
                !vault.TryAcquireWriteLock(in _environmentProfileHashesHandle, SystemID.Physics, out NativeArray<uint> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < DefaultFluidProfileCapacity)
                    return false;

                for (int i = 0; i < DefaultFluidProfileCapacity; i++)
                    destination[i] = source[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _environmentProfileHashesHandle, SystemID.Physics);
            }
        }

        private bool TryWriteEnvironmentProfileBuckets(IDataVault vault, ReadOnlySpan<int> source)
        {
            if (!IsVaultHandle(in _environmentProfileBucketsHandle, BufferID.ShinobuKccEnvironmentProfileBuckets, SystemID.Physics) ||
                !vault.TryAcquireWriteLock(in _environmentProfileBucketsHandle, SystemID.Physics, out NativeArray<int> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < DefaultFluidProfileBucketCount)
                    return false;

                for (int i = 0; i < DefaultFluidProfileBucketCount; i++)
                    destination[i] = source[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _environmentProfileBucketsHandle, SystemID.Physics);
            }
        }

        private bool TryWriteActiveEnvironmentProfile(IDataVault vault, in KccEnvironmentProfileDTO profile)
        {
            if (!IsVaultHandle(in _environmentProfileHandle, BufferID.ShinobuKccEnvironmentProfile, SystemID.Physics) ||
                !vault.TryAcquireWriteLock(in _environmentProfileHandle, SystemID.Physics, out NativeArray<KccEnvironmentProfileDTO> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < 1)
                    return false;

                destination[0] = SanitizeEnvironmentProfile(profile);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _environmentProfileHandle, SystemID.Physics);
            }
        }

        private static int FindEnvironmentProfileIndex(uint profileHash, NativeArray<uint> hashes, NativeArray<int> buckets)
        {
            if (profileHash == 0u || !hashes.IsCreated || !buckets.IsCreated || buckets.Length == 0)
                return -1;

            int start = (int)(profileHash % (uint)buckets.Length);
            for (int probe = 0; probe < buckets.Length; probe++)
            {
                int bucket = start + probe;
                if (bucket >= buckets.Length)
                    bucket -= buckets.Length;

                int index = buckets[bucket];
                if (index < 0)
                    return -1;
                if ((uint)index < (uint)hashes.Length && hashes[index] == profileHash)
                    return index;
            }

            return -1;
        }

        private static int FindEnvironmentProfileIndex(uint profileHash, NativeArray<uint>.ReadOnly hashes, NativeArray<int>.ReadOnly buckets)
        {
            if (profileHash == 0u || !hashes.IsCreated || !buckets.IsCreated || buckets.Length == 0)
                return -1;

            int start = (int)(profileHash % (uint)buckets.Length);
            for (int probe = 0; probe < buckets.Length; probe++)
            {
                int bucket = start + probe;
                if (bucket >= buckets.Length)
                    bucket -= buckets.Length;

                int index = buckets[bucket];
                if (index < 0)
                    return -1;
                if ((uint)index < (uint)hashes.Length && hashes[index] == profileHash)
                    return index;
            }

            return -1;
        }

        private bool TryPinScheduledVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _scheduledVaultBufferPinMask != 0u)
                return false;

            _scheduledVaultBufferPinVault = vault;
            bool pinned = false;
            try
            {
                if (!TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccStates, ScheduledVaultPinStates) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccInputs, ScheduledVaultPinInputs) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccProposedVelocities, ScheduledVaultPinProposedVelocities) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccResolvedHits, ScheduledVaultPinResolvedHits) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccFaultFlags, ScheduledVaultPinFaultFlags) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccWakePackets, ScheduledVaultPinWakePackets) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccTuning, ScheduledVaultPinTuning) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentProfile, ScheduledVaultPinEnvironmentProfile) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentGrid, ScheduledVaultPinEnvironmentGrid) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentFlowField, ScheduledVaultPinEnvironmentFlowField) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentSdf, ScheduledVaultPinEnvironmentSdf) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentMockMetabolism, ScheduledVaultPinEnvironmentMockMetabolism) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentDebug, ScheduledVaultPinEnvironmentDebug) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccPreviousAup, ScheduledVaultPinPreviousAup) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccVisualOutputs, ScheduledVaultPinVisualOutputs) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccTelemetryRing, ScheduledVaultPinTelemetryRing) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccTelemetryCursor, ScheduledVaultPinTelemetryCursor) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccRollbackBytes, ScheduledVaultPinRollbackBytes) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuHydroKccDebugOutputs, ScheduledVaultPinDebugOutputs) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentTelemetryRing, ScheduledVaultPinEnvironmentTelemetryRing) ||
                    !TryLockScheduledVaultBuffer(vault, BufferID.ShinobuKccEnvironmentTelemetryCursor, ScheduledVaultPinEnvironmentTelemetryCursor))
                {
                    return false;
                }

                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseScheduledVaultBufferPins();
            }
        }

        private void ReleaseScheduledVaultBufferPins()
        {
            IDataVault vault = _scheduledVaultBufferPinVault;
            uint pinMask = _scheduledVaultBufferPinMask;
            _scheduledVaultBufferPinVault = null;
            _scheduledVaultBufferPinMask = 0u;
            _metabolismStateReadPinHeld = false;
            _metabolismStateReadPinVault = null;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinPublishedMetabolismStates, BufferID.ShinobuMetabolismStates);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentTelemetryCursor, BufferID.ShinobuKccEnvironmentTelemetryCursor);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentTelemetryRing, BufferID.ShinobuKccEnvironmentTelemetryRing);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinDebugOutputs, BufferID.ShinobuHydroKccDebugOutputs);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinRollbackBytes, BufferID.ShinobuHydroKccRollbackBytes);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinTelemetryCursor, BufferID.ShinobuHydroKccTelemetryCursor);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinTelemetryRing, BufferID.ShinobuHydroKccTelemetryRing);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinVisualOutputs, BufferID.ShinobuHydroKccVisualOutputs);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinPreviousAup, BufferID.ShinobuHydroKccPreviousAup);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentDebug, BufferID.ShinobuKccEnvironmentDebug);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentMockMetabolism, BufferID.ShinobuKccEnvironmentMockMetabolism);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentSdf, BufferID.ShinobuKccEnvironmentSdf);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentFlowField, BufferID.ShinobuKccEnvironmentFlowField);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentGrid, BufferID.ShinobuKccEnvironmentGrid);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinEnvironmentProfile, BufferID.ShinobuKccEnvironmentProfile);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinTuning, BufferID.ShinobuHydroKccTuning);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinWakePackets, BufferID.ShinobuHydroKccWakePackets);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinFaultFlags, BufferID.ShinobuHydroKccFaultFlags);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinResolvedHits, BufferID.ShinobuHydroKccResolvedHits);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinProposedVelocities, BufferID.ShinobuHydroKccProposedVelocities);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinInputs, BufferID.ShinobuHydroKccInputs);
            TryUnlockScheduledVaultBuffer(vault, pinMask, ScheduledVaultPinStates, BufferID.ShinobuHydroKccStates);
        }

        private bool TryLockScheduledVaultBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_scheduledVaultBufferPinMask & pinBit) != 0u)
                return true;

            if (vault == null ||
                (_scheduledVaultBufferPinVault != null && !ReferenceEquals(_scheduledVaultBufferPinVault, vault)) ||
                !vault.TryLockBuffer(bufferId, SystemID.Physics))
            {
                return false;
            }

            _scheduledVaultBufferPinVault = vault;
            _scheduledVaultBufferPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockScheduledVaultBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.Physics);
        }

        private bool OpenPublishedMetabolismStateView(int requiredLength, out NativeArray<MetabolicStateDTO> states)
        {
            states = default;
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0 || _metabolismStateReadPinHeld)
                return false;

            BufferID bufferId = BufferID.ShinobuMetabolismStates;
            bool coveredByScheduledPins = (_scheduledVaultBufferPinMask & ScheduledVaultPinPublishedMetabolismStates) != 0u &&
                                          ReferenceEquals(_scheduledVaultBufferPinVault, vault);
            bool acquiredReadPin = false;
            bool success = false;
            try
            {
                if (!coveredByScheduledPins)
                {
                    if (!TryLockScheduledVaultBuffer(vault, bufferId, ScheduledVaultPinPublishedMetabolismStates))
                        return false;

                    _metabolismStateReadPinVault = vault;
                    _metabolismStateReadPinHeld = true;
                    acquiredReadPin = true;
                }

                if (!IsVaultHandle(in _metabolismStatesHandle, bufferId, SystemID.GameplayPlayer))
                {
                    if (!vault.TryGetGenerationHandle(bufferId, out _metabolismStatesHandle) ||
                        !IsVaultHandle(in _metabolismStatesHandle, bufferId, SystemID.GameplayPlayer))
                    {
                        _metabolismStatesHandle = default;
                        return false;
                    }
                }

                if (!TryReadVaultBuffer(
                        vault,
                        in _metabolismStatesHandle,
                        bufferId,
                        SystemID.GameplayPlayer,
                        requiredLength,
                        out states))
                {
                    states = default;
                    _metabolismStatesHandle = default;
                    return false;
                }

                success = true;
                return true;
            }
            finally
            {
                if (!success && acquiredReadPin)
                    ReleaseMetabolismStateReadGuard();
            }
        }

        private void ReleaseMetabolismStateReadGuard()
        {
            if (!_metabolismStateReadPinHeld)
                return;

            IDataVault vault = _metabolismStateReadPinVault;
            _metabolismStateReadPinVault = null;
            _metabolismStateReadPinHeld = false;
            if (vault == null ||
                !ReferenceEquals(_scheduledVaultBufferPinVault, vault) ||
                (_scheduledVaultBufferPinMask & ScheduledVaultPinPublishedMetabolismStates) == 0u)
            {
                return;
            }

            vault.TryUnlockBuffer(BufferID.ShinobuMetabolismStates, SystemID.Physics);
            _scheduledVaultBufferPinMask &= ~ScheduledVaultPinPublishedMetabolismStates;
            if (_scheduledVaultBufferPinMask == 0u)
                _scheduledVaultBufferPinVault = null;
        }

        private bool OpenOrAcquirePhysicsVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, SystemID.Physics, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return TryOpenVaultBuffer(vault, ref handle, bufferId, SystemID.Physics, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Physics,
                options);
            return TryOpenVaultBuffer(vault, ref handle, bufferId, SystemID.Physics, requiredLength, out buffer);
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID systemId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsVaultHandle(in handle, bufferId, systemId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID systemId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultHandle(in handle, bufferId, systemId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadOnlyVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID systemId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultHandle(in handle, bufferId, systemId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID systemId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)systemId &&
                   handle.Generation != 0u;
        }

        private bool EnsureVaultBuffers(bool allowAcquire = true)
        {
            if (_dataVault == null)
                return false;

            _entityCapacity = math.max(DefaultCapacity, _entityCapacity);
            if (AreVaultBuffersReady(_entityCapacity))
                return true;

            if (!allowAcquire)
            {
                _resolvedBufferCapacity = 0;
                return false;
            }

            int hitCapacity = _entityCapacity * MaxCollisionHitsPerCommand;
            int rollbackByteCapacity = _entityCapacity * UnsafeUtility.SizeOf<KinematicStateDTO>();
            if (!OpenOrAcquirePhysicsVaultBuffer(ref _statesHandle, BufferID.ShinobuHydroKccStates, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _inputsHandle, BufferID.ShinobuHydroKccInputs, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _proposedVelocitiesHandle, BufferID.ShinobuHydroKccProposedVelocities, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _previousAupHandle, BufferID.ShinobuHydroKccPreviousAup, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _visualOutputsHandle, BufferID.ShinobuHydroKccVisualOutputs, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _telemetryRingHandle, BufferID.ShinobuHydroKccTelemetryRing, TelemetryCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _telemetryCursorHandle, BufferID.ShinobuHydroKccTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _tuningHandle, BufferID.ShinobuHydroKccTuning, 1, NativeArrayOptions.ClearMemory, out NativeArray<HydrodynamicKccTuningDTO> tuning) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _rollbackBytesHandle, BufferID.ShinobuHydroKccRollbackBytes, rollbackByteCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _faultFlagsHandle, BufferID.ShinobuHydroKccFaultFlags, _entityCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _wakePacketsHandle, BufferID.ShinobuHydroKccWakePackets, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _debugOutputsHandle, BufferID.ShinobuHydroKccDebugOutputs, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _resolvedHitsHandle, BufferID.ShinobuHydroKccResolvedHits, hitCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _fluidProfilesHandle, BufferID.ShinobuHydroKccFluidProfiles, DefaultFluidProfileCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _fluidProfileBucketsHandle, BufferID.ShinobuHydroKccFluidProfileBuckets, DefaultFluidProfileBucketCount, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentProfileHandle, BufferID.ShinobuKccEnvironmentProfile, 1, NativeArrayOptions.ClearMemory, out NativeArray<KccEnvironmentProfileDTO> environmentProfile) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentGridHandle, BufferID.ShinobuKccEnvironmentGrid, 1, NativeArrayOptions.ClearMemory, out NativeArray<KccEnvironmentGridDTO> environmentGrid) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentFlowFieldHandle, BufferID.ShinobuKccEnvironmentFlowField, EnvironmentGridCellCount, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentSdfHandle, BufferID.ShinobuKccEnvironmentSdf, EnvironmentGridCellCount, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentMockMetabolismHandle, BufferID.ShinobuKccEnvironmentMockMetabolism, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentDebugHandle, BufferID.ShinobuKccEnvironmentDebug, _entityCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentTelemetryRingHandle, BufferID.ShinobuKccEnvironmentTelemetryRing, TelemetryCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentTelemetryCursorHandle, BufferID.ShinobuKccEnvironmentTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentProfilesHandle, BufferID.ShinobuKccEnvironmentProfiles, DefaultFluidProfileCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentProfileBucketsHandle, BufferID.ShinobuKccEnvironmentProfileBuckets, DefaultFluidProfileBucketCount, NativeArrayOptions.ClearMemory, out _) ||
                !OpenOrAcquirePhysicsVaultBuffer(ref _environmentProfileHashesHandle, BufferID.ShinobuKccEnvironmentProfileHashes, DefaultFluidProfileCapacity, NativeArrayOptions.ClearMemory, out _))
            {
                _resolvedBufferCapacity = 0;
                return false;
            }

            if (tuning.IsCreated && tuning.Length > 0 && tuning[0].MaxSpeed <= 0f)
                tuning[0] = SanitizeTuning(DefaultTuning());
            if (environmentProfile.IsCreated && environmentProfile.Length > 0 && environmentProfile[0].MaxSlopeAngle <= 0f)
                environmentProfile[0] = DefaultEnvironmentProfile();
            if (environmentGrid.IsCreated && environmentGrid.Length > 0 && environmentGrid[0].CellSizeMeters <= 0f)
                environmentGrid[0] = DefaultEnvironmentGrid(ResolveSectorOriginAup());

            _resolvedBufferCapacity = _entityCapacity;
            bool ready = AreVaultBuffersReady(_entityCapacity);
            if (!ready)
                _resolvedBufferCapacity = 0;
            return ready;
        }

        private bool HasVaultBuffersReady()
        {
            if (_dataVault == null)
                return false;

            int entityCapacity = math.max(DefaultCapacity, _entityCapacity);
            return _resolvedBufferCapacity == entityCapacity &&
                   AreVaultBuffersReady(entityCapacity);
        }

        private void ResetVaultHandles()
        {
            _statesHandle = default;
            _inputsHandle = default;
            _proposedVelocitiesHandle = default;
            _previousAupHandle = default;
            _visualOutputsHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _tuningHandle = default;
            _rollbackBytesHandle = default;
            _faultFlagsHandle = default;
            _wakePacketsHandle = default;
            _debugOutputsHandle = default;
            _resolvedHitsHandle = default;
            _fluidProfilesHandle = default;
            _fluidProfileBucketsHandle = default;
            _environmentProfileHandle = default;
            _environmentGridHandle = default;
            _environmentFlowFieldHandle = default;
            _environmentSdfHandle = default;
            _environmentMockMetabolismHandle = default;
            _metabolismStatesHandle = default;
            _environmentDebugHandle = default;
            _environmentTelemetryRingHandle = default;
            _environmentTelemetryCursorHandle = default;
            _environmentProfilesHandle = default;
            _environmentProfileBucketsHandle = default;
            _environmentProfileHashesHandle = default;
            _resolvedBufferCapacity = 0;
            _scheduledEntityCount = 0;
            _scheduledMaxHitsPerCommand = 0;
            _externalInputHandle = default;
            _environmentMockHandle = default;
            _externalInputArmed = false;
            _respawnCollisionBypassFrames = 0;
        }

        private bool ConsumeRespawnCollisionSuspendSignals()
        {
            if (_respawnCollisionBypassFrames > 0)
                return true;

            int snapshotGeneration = SignalBus<PlayerRespawnSignal>.SnapshotGeneration;
            if (snapshotGeneration == _lastRespawnCollisionSnapshotGeneration)
                return false;

            ReadOnlySpan<PlayerRespawnSignal> signals = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerRespawnSignal signal = signals[i];
                uint signalFlags = signal.Flags;
                bool requestPacket = signal.Phase == PlayerRespawnSignalPhase.Request &&
                                     (signalFlags & PlayerRespawnSignalFlags.Requested) != 0u &&
                                     (signalFlags & PlayerRespawnSignalFlags.Committed) == 0u;
                bool committedPacket = signal.Phase == PlayerRespawnSignalPhase.Committed &&
                                       (signalFlags & PlayerRespawnSignalFlags.Committed) != 0u;
                bool respawnPacket = requestPacket || committedPacket;
                if (!respawnPacket ||
                    signal.Sequence == 0u ||
                    (signalFlags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u ||
                    (signalFlags & PlayerRespawnSignalFlags.SuspendCollision) == 0u ||
                    signal.SuspendCollisionFrames == 0)
                {
                    continue;
                }

                _respawnCollisionBypassFrames = 1;
                _lastRespawnCollisionSnapshotGeneration = snapshotGeneration;
                return true;
            }

            _lastRespawnCollisionSnapshotGeneration = snapshotGeneration;
            return false;
        }

        private bool TryAbortScheduledBatchNoWait()
        {
            if (!_postSimulationHandle.IsCompleted ||
                !_sdfCollisionHandle.IsCompleted ||
                !_collisionHandle.IsCompleted ||
                !_commandHandle.IsCompleted ||
                !_integrationHandle.IsCompleted ||
                !_environmentMockHandle.IsCompleted ||
                !_inputHandle.IsCompleted ||
                !_externalInputHandle.IsCompleted)
            {
                return false;
            }

            DispatcherJobFence.TryFinalizeCompleted(ref _postSimulationHandle);
            DispatcherJobFence.TryFinalizeCompleted(ref _sdfCollisionHandle);
            DispatcherJobFence.TryFinalizeCompleted(ref _collisionHandle);
            DispatcherJobFence.TryFinalizeCompleted(ref _commandHandle);
            DispatcherJobFence.TryFinalizeCompleted(ref _integrationHandle);
            DispatcherJobFence.TryFinalizeCompleted(ref _environmentMockHandle);
            DispatcherJobFence.TryFinalizeCompleted(ref _inputHandle);
            DispatcherJobFence.TryFinalizeCompleted(ref _externalInputHandle);
            ClearScheduledBatchState();
            return true;
        }

        private void AbortScheduledBatchForTeardown()
        {
            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _postSimulationHandle, true);
                DispatcherJobFence.TryComplete(ref _sdfCollisionHandle, true);
                DispatcherJobFence.TryComplete(ref _collisionHandle, true);
                DispatcherJobFence.TryComplete(ref _commandHandle, true);
                DispatcherJobFence.TryComplete(ref _integrationHandle, true);
                DispatcherJobFence.TryComplete(ref _environmentMockHandle, true);
                DispatcherJobFence.TryComplete(ref _inputHandle, true);
                DispatcherJobFence.TryComplete(ref _externalInputHandle, true);
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            ClearScheduledBatchState();
        }

        private void ClearScheduledBatchState()
        {
            ReleaseMetabolismStateReadGuard();
            ReleaseScheduledVaultBufferPins();
            _collisionScheduled = false;
            _postScheduled = false;
            _scheduledEntityCount = 0;
            _scheduledMaxHitsPerCommand = 0;
            _environmentMockHandle = default;
            _externalInputArmed = false;
        }

        private bool AreVaultBuffersReady(int capacity)
        {
            int entityCapacity = math.max(1, capacity);
            int hitCapacity = entityCapacity * MaxCollisionHitsPerCommand;
            int rollbackByteCapacity = entityCapacity * UnsafeUtility.SizeOf<KinematicStateDTO>();
            return _resolvedBufferCapacity >= entityCapacity &&
                   TryOpenVaultBuffer(_dataVault, ref _statesHandle, BufferID.ShinobuHydroKccStates, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _inputsHandle, BufferID.ShinobuHydroKccInputs, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _proposedVelocitiesHandle, BufferID.ShinobuHydroKccProposedVelocities, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _resolvedHitsHandle, BufferID.ShinobuHydroKccResolvedHits, SystemID.Physics, hitCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _previousAupHandle, BufferID.ShinobuHydroKccPreviousAup, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _visualOutputsHandle, BufferID.ShinobuHydroKccVisualOutputs, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _telemetryRingHandle, BufferID.ShinobuHydroKccTelemetryRing, SystemID.Physics, TelemetryCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _telemetryCursorHandle, BufferID.ShinobuHydroKccTelemetryCursor, SystemID.Physics, 1, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _tuningHandle, BufferID.ShinobuHydroKccTuning, SystemID.Physics, 1, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _rollbackBytesHandle, BufferID.ShinobuHydroKccRollbackBytes, SystemID.Physics, rollbackByteCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _faultFlagsHandle, BufferID.ShinobuHydroKccFaultFlags, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _wakePacketsHandle, BufferID.ShinobuHydroKccWakePackets, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _debugOutputsHandle, BufferID.ShinobuHydroKccDebugOutputs, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _fluidProfilesHandle, BufferID.ShinobuHydroKccFluidProfiles, SystemID.Physics, DefaultFluidProfileCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _fluidProfileBucketsHandle, BufferID.ShinobuHydroKccFluidProfileBuckets, SystemID.Physics, DefaultFluidProfileBucketCount, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentProfileHandle, BufferID.ShinobuKccEnvironmentProfile, SystemID.Physics, 1, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentGridHandle, BufferID.ShinobuKccEnvironmentGrid, SystemID.Physics, 1, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentFlowFieldHandle, BufferID.ShinobuKccEnvironmentFlowField, SystemID.Physics, EnvironmentGridCellCount, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentSdfHandle, BufferID.ShinobuKccEnvironmentSdf, SystemID.Physics, EnvironmentGridCellCount, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentMockMetabolismHandle, BufferID.ShinobuKccEnvironmentMockMetabolism, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentDebugHandle, BufferID.ShinobuKccEnvironmentDebug, SystemID.Physics, entityCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentTelemetryRingHandle, BufferID.ShinobuKccEnvironmentTelemetryRing, SystemID.Physics, TelemetryCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentTelemetryCursorHandle, BufferID.ShinobuKccEnvironmentTelemetryCursor, SystemID.Physics, 1, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentProfilesHandle, BufferID.ShinobuKccEnvironmentProfiles, SystemID.Physics, DefaultFluidProfileCapacity, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentProfileBucketsHandle, BufferID.ShinobuKccEnvironmentProfileBuckets, SystemID.Physics, DefaultFluidProfileBucketCount, out _) &&
                   TryOpenVaultBuffer(_dataVault, ref _environmentProfileHashesHandle, BufferID.ShinobuKccEnvironmentProfileHashes, SystemID.Physics, DefaultFluidProfileCapacity, out _);
        }

        private int ResolveFaultMask(NativeArray<HydrodynamicKccFaultFlagDTO> faults)
        {
            if (!faults.IsCreated || faults.Length == 0)
                return 0;

            int scanCount = math.min(_entityCapacity, faults.Length);
            int mask = 0;
            for (int i = 0; i < scanCount; i++)
                mask |= faults[i].FaultMask;

            return mask;
        }

        private HydrodynamicKccTuningDTO UpdateTuningSnapshot(NativeArray<HydrodynamicKccTuningDTO> tuningBuffer)
        {
            HydrodynamicKccTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = _globalQualityWeight;
            tuning.WaterSurfaceY = _waterSurfaceY;
            tuning = SanitizeTuning(tuning);
            tuning.WaterSurfaceY = ResolveRuntimeWaterSurfaceY();
            tuningBuffer[0] = tuning;
            return tuning;
        }

        private HydrodynamicKccTuningDTO DefaultTuning()
        {
            return new HydrodynamicKccTuningDTO
            {
                BaseDrag = 0.18f,
                FluidDensity = 1f,
                MaxSpeed = 6f,
                GravityMultiplier = 1f,
                BuoyancyScalar = 1.08f,
                CapsuleRadius = _capsule != null ? math.max(0.05f, _capsule.radius) : 0.35f,
                CapsuleHeight = _capsule != null ? math.max(0.1f, _capsule.height) : 1.8f,
                SkinWidth = 0.025f,
                GlobalQualityWeight = ResolveGlobalQualityWeight(),
                WaterSurfaceY = ResolveRuntimeWaterSurfaceY(),
                MockInputFrequency = 0.35f,
                MockInputAmplitude = 1f,
                VisualSyncSharpness = 18f,
                WakeThreshold = 0.25f,
                ProfileHash = HydrodynamicKccMath.SourceHash,
                Flags = 1u
            };
        }

        private static HydrodynamicKccTuningDTO SanitizeTuning(HydrodynamicKccTuningDTO tuning)
        {
            tuning.BaseDrag = math.max(0f, math.isfinite(tuning.BaseDrag) ? tuning.BaseDrag : 0.18f);
            tuning.FluidDensity = math.max(0f, math.isfinite(tuning.FluidDensity) ? tuning.FluidDensity : 1f);
            tuning.MaxSpeed = math.max(0.1f, math.isfinite(tuning.MaxSpeed) ? tuning.MaxSpeed : 6f);
            tuning.GravityMultiplier = math.max(0f, math.isfinite(tuning.GravityMultiplier) ? tuning.GravityMultiplier : 1f);
            tuning.BuoyancyScalar = math.max(0f, math.isfinite(tuning.BuoyancyScalar) ? tuning.BuoyancyScalar : 1.05f);
            tuning.CapsuleRadius = math.max(0.05f, math.isfinite(tuning.CapsuleRadius) ? tuning.CapsuleRadius : 0.35f);
            tuning.CapsuleHeight = math.max(tuning.CapsuleRadius * 2f, math.isfinite(tuning.CapsuleHeight) ? tuning.CapsuleHeight : 1.8f);
            tuning.SkinWidth = math.max(0.001f, math.isfinite(tuning.SkinWidth) ? tuning.SkinWidth : 0.025f);
            tuning.GlobalQualityWeight = HydrodynamicKccMath.ResolveQuality01(tuning.GlobalQualityWeight);
            tuning.WaterSurfaceY = HydrodynamicKccMath.ResolveWaterSurfaceY(tuning.WaterSurfaceY);
            tuning.MockInputFrequency = math.max(0.01f, math.isfinite(tuning.MockInputFrequency) ? tuning.MockInputFrequency : 0.35f);
            tuning.MockInputAmplitude = math.max(0f, math.isfinite(tuning.MockInputAmplitude) ? tuning.MockInputAmplitude : 1f);
            tuning.VisualSyncSharpness = math.max(0.01f, math.isfinite(tuning.VisualSyncSharpness) ? tuning.VisualSyncSharpness : 18f);
            tuning.WakeThreshold = math.max(0.01f, math.isfinite(tuning.WakeThreshold) ? tuning.WakeThreshold : 0.25f);
            return tuning;
        }

        /// <summary>
        /// Turns water into a medium. Evaluates the four Hecton8.PureLogic.Kinematics models that describe
        /// how the water column resists, carries, floats and crushes the controller, and stages their results
        /// for <see cref="ApplyEnvironmentalForcesJob"/>.
        ///
        /// Runs on the main thread inside <see cref="FixedTick"/> because every one of the four models is
        /// built on System.Numerics.Vector3 / System.Math in a noEngineReferences assembly and none of them
        /// is Burst-compilable. Allocation-free: all four are static methods over structs, the ocean sample
        /// is a single-point non-batch query, and no managed collection is touched.
        /// </summary>
        private void UpdateWaterMediumForces(
            NativeArray<KinematicStateDTO> states,
            NativeArray<float3> environmentFlow,
            KccEnvironmentGridDTO grid,
            KccEnvironmentProfileDTO environmentProfile,
            HydrodynamicKccTuningDTO tuning,
            double3 sectorOrigin,
            float fixedDeltaTime)
        {
            _mediumCurrentDragForce = float3.zero;
            _mediumThermoclineResistance01 = 0f;
            _mediumDensityBuoyancyNewtons = 0f;
            _mediumFlags = 0u;

            if (!_enableWaterMediumForces || !states.IsCreated || states.Length == 0)
                return;

            KinematicStateDTO state = states[0];
            if (!HydrodynamicKccMath.IsFinite(state.AUP_Position))
                return;

            // Same depth convention and same datum as the integration job: depth is metres BELOW the
            // resolved runtime water surface, which falls back to
            // WorldWaterLevelCalibrationMath.DefaultWaterLevelY (14.02), never to 0.
            float3 localPosition = HydrodynamicKccMath.ResolveLocalFloat3(state.AUP_Position, sectorOrigin);
            float waterSurfaceY = HydrodynamicKccMath.ResolveRuntimeWaterSurfaceY(tuning.WaterSurfaceY);
            float depthMeters = math.max(0f, waterSurfaceY - localPosition.y);
            _mediumDepthMeters = depthMeters;

            float3 velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
            float speed = HydrodynamicKccMath.LengthSafe(velocity);
            // Mass is sanitized with the identical rule the job uses so the buoyancy weight term the job
            // reconstructs from its own `mass` matches the weight folded into the model's net force.
            float mass = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(state.Mass) ? state.Mass : 80f);
            float gravity = 9.80665f * math.max(0f, math.isfinite(tuning.GravityMultiplier) ? tuning.GravityMultiplier : 1f);
            float fluidDensityKgPerM3 = SeawaterReferenceDensityKgPerM3 *
                math.max(0f, math.isfinite(tuning.FluidDensity) ? tuning.FluidDensity : 1f);

            UpdateOceanCurrentDrag(
                state.AUP_Position,
                localPosition,
                velocity,
                environmentFlow,
                grid,
                environmentProfile,
                tuning,
                fluidDensityKgPerM3,
                depthMeters,
                mass,
                fixedDeltaTime);
            UpdateThermoclineResistance(depthMeters, speed);
            UpdateDensityBuoyancy(mass, fluidDensityKgPerM3, gravity, tuning);
            AdvanceCrushDamage(depthMeters, state.AUP_Position, fixedDeltaTime);
        }

        /// <summary>
        /// OceanCurrentDragCalculator: drag from the relative velocity between the water column and the body.
        /// This is the ONLY ocean-current coupling for the owner entity -- ApplyEnvironmentalForcesJob drops
        /// its raw <c>appliedFlow * dt</c> add whenever this force lands, so the cell value must be read as a
        /// velocity in m/s and must carry the same <c>CurrentAdvectionScalar</c> the job would have applied.
        /// </summary>
        private void UpdateOceanCurrentDrag(
            double3 aupPosition,
            float3 localPosition,
            float3 velocity,
            NativeArray<float3> environmentFlow,
            KccEnvironmentGridDTO grid,
            KccEnvironmentProfileDTO environmentProfile,
            HydrodynamicKccTuningDTO tuning,
            float fluidDensityKgPerM3,
            float depthMeters,
            float mass,
            float fixedDeltaTime)
        {
            if (depthMeters <= 0f || fluidDensityKgPerM3 <= 0f)
                return;

            // The environment flow field is preferred over the ocean surface provider because it is the same
            // field ApplyEnvironmentalForcesJob advects with. If drag were computed against a different
            // current than the advection term uses, the two would fight and the body would be dragged toward
            // a velocity it is never carried to.
            if (!TryReadEnvironmentFlow(aupPosition, environmentFlow, grid, out float3 currentVelocity) &&
                !TrySampleOceanCurrentVelocity(localPosition, out currentVelocity))
                return;

            // Byte-for-byte the job's own reading of the scalar (ApplyEnvironmentalForcesJob.Execute,
            // `appliedFlow`). Without it the 0-8 designer knob would be dead for the owner entity -- setting
            // it to 0 to switch the current off would leave full-strength drag against the raw field.
            currentVelocity *= math.max(0f, math.isfinite(environmentProfile.CurrentAdvectionScalar) ? environmentProfile.CurrentAdvectionScalar : 1f);
            if (!HydrodynamicKccMath.IsFinite(currentVelocity))
                return;

            float radius = math.max(0.05f, math.isfinite(tuning.CapsuleRadius) ? tuning.CapsuleRadius : 0.35f);
            float crossSectionalArea = math.PI * radius * radius;
            float dragCoefficient = math.clamp(
                math.isfinite(_mediumDragCoefficient) ? _mediumDragCoefficient : DefaultMediumDragCoefficient,
                0f,
                MaxMediumDragCoefficient);

            System.Numerics.Vector3 modelForce = Hecton8.PureLogic.Kinematics.OceanCurrentDragCalculator.Compute(
                new System.Numerics.Vector3(currentVelocity.x, currentVelocity.y, currentVelocity.z),
                new System.Numerics.Vector3(velocity.x, velocity.y, velocity.z),
                dragCoefficient,
                crossSectionalArea);

            // The model computes 0.5 * Cd * A * v^2 and OMITS fluid density, so its output is a force per
            // unit density, not Newtons. Multiplying by kg/m^3 here completes the drag equation.
            float3 force = new float3(modelForce.X, modelForce.Y, modelForce.Z) * fluidDensityKgPerM3;
            if (!HydrodynamicKccMath.IsFinite(force) || math.lengthsq(force) <= 0.000001f)
                return;

            // NON-OVERSHOOT BOUND. The job integrates this force explicitly (velocity += F/safeMass * dt) and
            // the force is quadratic in the relative speed, so above a critical relative speed one tick
            // overshoots the water velocity and the sign of the drag flips -- the body oscillates instead of
            // settling into the current. The physical fixed point is v == currentVelocity, so cap the impulse
            // at exactly the relative momentum. mass (not mass + addedMass, which the job divides by) keeps
            // the cap conservative.
            float relativeSpeed = HydrodynamicKccMath.LengthSafe(currentVelocity - velocity);
            float safeFixedDelta = math.isfinite(fixedDeltaTime) ? math.max(HydrodynamicKccMath.MinDenominator, fixedDeltaTime) : 0.02f;
            float maxForceNewtons = mass * relativeSpeed * math.rcp(safeFixedDelta);
            float forceMagnitude = HydrodynamicKccMath.LengthSafe(force);
            if (forceMagnitude > maxForceNewtons)
            {
                force *= maxForceNewtons * math.rcp(math.max(HydrodynamicKccMath.MinDenominator, forceMagnitude));
                if (!HydrodynamicKccMath.IsFinite(force) || math.lengthsq(force) <= 0.000001f)
                    return;
            }

            _mediumCurrentDragForce = force;
            _mediumFlags |= HydrodynamicKccMath.FlagMediumCurrentDrag;
        }

        /// <summary>
        /// Nearest-cell read of the environment flow field on the main thread. The cell-space transform and the
        /// flatten order are copied from <c>ApplyEnvironmentalForcesJob.ResolveGridCell</c> / <c>FlattenCell</c>
        /// so both read the same cell for the same position. It stays nearest-cell (no trilinear) because this
        /// is one sample per fixed tick feeding a force term, not the advection sample itself.
        ///
        /// It does NOT copy <c>ResolveSampleDimensions</c>: rather than deriving fallback dimensions when the
        /// grid does not fit the buffer, it returns false and lets the caller fall back to the ocean provider.
        ///
        /// Reads the previous tick's contents: GenerateMockEnvironmentalForcesJob is scheduled later in this
        /// same FixedTick, and the early-out on _collisionScheduled/_postScheduled guarantees no job is in
        /// flight over this buffer at this point. One-tick staleness matches the staleness of state.Velocity,
        /// which this force term is already computed against.
        /// </summary>
        private static bool TryReadEnvironmentFlow(
            double3 aupPosition,
            NativeArray<float3> environmentFlow,
            KccEnvironmentGridDTO grid,
            out float3 currentVelocity)
        {
            currentVelocity = float3.zero;
            if (!environmentFlow.IsCreated || environmentFlow.Length == 0)
                return false;

            if (!HydrodynamicKccMath.IsFinite(grid.GridOriginAup) || !(grid.CellSizeMeters > 0f))
                return false;

            int3 dimensions = new int3(
                math.clamp(grid.Dimensions.x, 1, EnvironmentGridAxisX),
                math.clamp(grid.Dimensions.y, 1, EnvironmentGridAxisY),
                math.clamp(grid.Dimensions.z, 1, EnvironmentGridAxisZ));
            if (dimensions.x * dimensions.y * dimensions.z > environmentFlow.Length)
                return false;

            double3 delta = HydrodynamicKccMath.Sanitize(aupPosition - grid.GridOriginAup, double3.zero);
            float invCell = math.rcp(math.max(0.25f, grid.CellSizeMeters));
            float3 cell = new float3((float)delta.x, (float)delta.y, (float)delta.z) * invCell;
            float3 maxCell = new float3(dimensions.x - 1, dimensions.y - 1, dimensions.z - 1);
            cell = math.clamp(HydrodynamicKccMath.Sanitize(cell, float3.zero), float3.zero, maxCell);

            int x = math.clamp((int)math.round(cell.x), 0, dimensions.x - 1);
            int y = math.clamp((int)math.round(cell.y), 0, dimensions.y - 1);
            int z = math.clamp((int)math.round(cell.z), 0, dimensions.z - 1);
            int index = x + (z * dimensions.x) + (y * dimensions.x * dimensions.z);
            if ((uint)index >= (uint)environmentFlow.Length)
                return false;

            float3 sampled = HydrodynamicKccMath.Sanitize(environmentFlow[index], float3.zero);
            if (math.lengthsq(sampled) <= 0.000001f)
                return false;

            currentVelocity = sampled;
            return true;
        }

        private bool TrySampleOceanCurrentVelocity(float3 localPosition, out float3 currentVelocity)
        {
            currentVelocity = float3.zero;
            IHectonOceanKinematicsService service = _oceanKinematicsService;
            IHectonOceanKinematics provider = service != null && service.IsInitialized ? service.ActiveProvider : null;
            if (provider == null || !provider.IsAvailable)
                return false;

            // 1f matches every other single-point call site in the project
            // (HectonOceanKinematicsBridgeBase.GetFlowAt / GetWaveHeight); the bridge reuses preallocated
            // single-sample arrays, so this does not allocate.
            if (!provider.TrySampleWaterVelocity(localPosition, 1f, out float3 sampled))
                return false;

            if (!HydrodynamicKccMath.IsFinite(sampled))
                return false;

            currentVelocity = sampled;
            return true;
        }

        /// <summary>
        /// ThermoclineResistanceCalculator: extra resistance while crossing the thermocline band.
        /// </summary>
        private void UpdateThermoclineResistance(float depthMeters, float speed)
        {
            if (depthMeters <= 0f)
                return;

            // Gated on the canonical weather bitmask so the band is only "live" when the weather director
            // says a thermocline exists, rather than being an unconditional invisible wall.
            if (_requireThermoclineWeatherState && !IsThermoclineWeatherActive())
                return;

            float thermoclineDepth = math.max(0f, math.isfinite(_thermoclineDepthMeters) ? _thermoclineDepthMeters : DefaultThermoclineDepthMeters);
            float thickness = math.max(0f, math.isfinite(_thermoclineThicknessMeters) ? _thermoclineThicknessMeters : DefaultThermoclineThicknessMeters);
            float resistanceForce = math.max(0f, math.isfinite(_thermoclineResistanceForce) ? _thermoclineResistanceForce : DefaultThermoclineResistanceForce);
            if (thickness <= 0f || resistanceForce <= 0f)
                return;

            float resistance01 = Hecton8.PureLogic.Kinematics.ThermoclineResistanceCalculator.Compute(
                depthMeters,
                thermoclineDepth,
                thickness,
                speed,
                resistanceForce);

            if (!math.isfinite(resistance01) || resistance01 <= 0f)
                return;

            _mediumThermoclineResistance01 = math.saturate(resistance01);
            _mediumFlags |= HydrodynamicKccMath.FlagMediumThermocline;
        }

        private bool IsThermoclineWeatherActive()
        {
            IWeatherService weatherService = _weatherService;
            if (weatherService == null || !weatherService.IsInitialized)
                return false;

            return ((uint)weatherService.CurrentWeatherState & (uint)WeatherState.ThermoclineActive) != 0u;
        }

        /// <summary>
        /// BuoyancyDensityRatioMath: rise/sink follows the density ratio instead of a constant scalar.
        ///
        /// <c>BuoyancyScalar = 0</c> is the project's documented DISABLE-BUOYANCY idiom, not a trim value:
        /// <c>ReplayDeterminismValidator1626.BuildTuning</c> sets it, <c>SanitizeTuning</c> explicitly permits
        /// it through <c>math.max(0f, ...)</c>, and the tuner slider bottoms out there. Under the legacy
        /// scalar term it means plain gravity -- <c>(0 * submersion * mass - mass) * gravity / safeMass</c>,
        /// i.e. -1 g. This model divides BY the scalar, so it must not see 0: it is skipped instead, leaving
        /// the job's legacy term to deliver exactly the old meaning. Floored-but-applied would be a silent
        /// reinterpretation of a documented idiom, which is why the previous
        /// <c>math.max(MinDenominator, ...)</c> was a defect and not a guard -- 0.0001 turned into
        /// 1.05e7 kg/m^3 of body density, ~-7.8e6 N, about -98,000 m/s^2 of sink. The max-speed clamp then
        /// hid the number while pinning the controller at full downward speed with no way to swim up.
        /// </summary>
        private void UpdateDensityBuoyancy(
            float mass,
            float fluidDensityKgPerM3,
            float gravity,
            HydrodynamicKccTuningDTO tuning)
        {
            if (fluidDensityKgPerM3 <= 0f || gravity <= 0f)
                return;

            // DISABLE IDIOM: fall through to the job's legacy plain-gravity term, do not set the flag.
            // NaN is not the idiom -- it is corrupt data -- so it takes the authored default instead.
            float authoredScalar = math.isfinite(tuning.BuoyancyScalar) ? tuning.BuoyancyScalar : 1.08f;
            if (authoredScalar <= 0f)
                return;

            // Displaced volume is derived from body density, not from the capsule collider -- see the
            // PlayerBodyDensityKgPerM3 comment for why the collider volume is the wrong input.
            float displacedVolume = mass * math.rcp(PlayerBodyDensityKgPerM3);
            // BuoyancyScalar keeps its existing meaning as a trim/ballast knob: >1 means positively buoyant,
            // so it lowers the effective body density rather than scaling the force directly. The floor is a
            // real physical bound on that division -- see MinBuoyancyTrimScalar.
            float buoyancyScalar = math.max(MinBuoyancyTrimScalar, authoredScalar);
            float playerDensity = PlayerBodyDensityKgPerM3 * math.rcp(buoyancyScalar);

            float netNewtons = Hecton8.PureLogic.Kinematics.BuoyancyDensityRatioMath.Calculate(
                playerDensity,
                fluidDensityKgPerM3,
                displacedVolume,
                gravity);

            if (!math.isfinite(netNewtons))
                return;

            // Outer bound in gravities of net force. FluidDensity and Mass are designer/runtime data on the
            // same struct, and the job turns this straight into acceleration, so nothing upstream of here may
            // be able to author a launch or a weld to the sea floor.
            float maxNetNewtons = MaxDensityBuoyancyGravities * mass * gravity;
            netNewtons = math.clamp(netNewtons, -maxNetNewtons, maxNetNewtons);
            if (!math.isfinite(netNewtons))
                return;

            _mediumDensityBuoyancyNewtons = netNewtons;
            _mediumFlags |= HydrodynamicKccMath.FlagMediumDensityBuoyancy;
        }

        /// <summary>
        /// PressureCrushDamageModel: depth becomes a threat below the suit's crush rating. Accumulated every
        /// fixed tick, flushed to the canonical combat damage ingress on
        /// <see cref="CrushDamageFlushIntervalSeconds"/>.
        /// </summary>
        private void AdvanceCrushDamage(float depthMeters, double3 impactAup, float fixedDeltaTime)
        {
            // Rollback resimulation replays frames that already ran and already charged their crush damage.
            // The force terms are recomputed (they must be, the state changed) but damage is an irreversible
            // side effect and re-queuing it would charge the player twice for one descent.
            if (_rollbackResimulationActive)
                return;

            float fdt = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            if (fdt <= 0f)
                return;

            float threshold = math.max(0f, math.isfinite(_crushDepthThresholdMeters) ? _crushDepthThresholdMeters : DefaultCrushDepthThresholdMeters);
            float maxRate = math.max(0f, math.isfinite(_crushMaxDamageRatePerSecond) ? _crushMaxDamageRatePerSecond : DefaultCrushMaxDamageRatePerSecond);
            float exponent = math.max(1f, math.isfinite(_crushDamageExponent) ? _crushDamageExponent : DefaultCrushDamageExponent);

            float damagePerSecond = threshold > 0f && maxRate > 0f
                ? Hecton8.PureLogic.Kinematics.PressureCrushDamageModel.Evaluate(depthMeters, threshold, maxRate, exponent)
                : 0f;

            // Cap the RATE, not just the flush packet. The model is unbounded in depth and returns
            // float.MaxValue on overflow, so clamping only at the flush would still let the accumulator carry
            // an arbitrary figure across ticks and across a rejected-ingress requeue. Capping here makes
            // MaxCrushDamagePerFlush a consistent consequence of MaxCrushDamagePerSecond rather than a second,
            // looser opinion. See MaxCrushDamagePerSecond for the depth/time-to-death table.
            if (math.isfinite(damagePerSecond) && damagePerSecond > 0f)
                _pendingCrushDamage += math.min(damagePerSecond, MaxCrushDamagePerSecond) * fdt;

            if (_pendingCrushDamage <= 0f)
            {
                _crushDamageFlushTimer = 0f;
                return;
            }

            _crushDamageFlushTimer += fdt;
            if (_crushDamageFlushTimer < CrushDamageFlushIntervalSeconds)
                return;

            _crushDamageFlushTimer = 0f;
            float pending = math.min(_pendingCrushDamage, MaxCrushDamagePerFlush);
            _pendingCrushDamage = 0f;
            if (!math.isfinite(pending) || pending <= 0f)
                return;

            TryQueueCrushDamage(pending, impactAup);
        }

        private void TryQueueCrushDamage(float amount, double3 impactAup)
        {
            if (!TryResolveRegisteredCrushDamageTarget(out int targetId))
                return;

            // Routed through the project's existing damage ingress -- the same path EnvironmentalHazard uses
            // -- never a direct health write. Armor, status and death resolution stay owned by
            // CombatDamageRuntime and HectonPlayerHealth.ReceiveDamage.
            Hecton8.Gameplay.CombatDamageRequest request = new Hecton8.Gameplay.CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = Hecton8.Gameplay.DamageSourceIds.EnvironmentHazard,
                Amount = amount,
                ImpulseMagnitude = 0f,
                // Crush loads the body inward from every side; there is no impact direction. Down is used so
                // downstream direction consumers stay finite and normalized.
                Direction = new float3(0f, -1f, 0f),
                PackedMeta = Hecton8.Gameplay.CombatDamageRuntime.PackSignalMeta(
                    Hecton8.Gameplay.CombatDamageTypes.Pressure,
                    0u,
                    Hecton8.Gameplay.CombatWeakspotTier.None)
            };

            Hecton8.Gameplay.CombatDamageSignalDetail detail = new Hecton8.Gameplay.CombatDamageSignalDetail
            {
                LocalPoint = float3.zero,
                ArmorNormal = new float3(0f, 1f, 0f),
                LocalTemperatureCelsius = 0f,
                StatusDurationSeconds = CrushDamageFlushIntervalSeconds
            };

            double3 safeImpactAup = HydrodynamicKccMath.IsFinite(impactAup) ? impactAup : double3.zero;
            if (!Hecton8.Gameplay.CombatDamageRuntime.TryQueueDamage(in request, in detail, safeImpactAup))
            {
                // Rejected ingress (queue busy/full) must not vanish silently or depth would stop hurting
                // for the rest of that window. Put it back for the next flush.
                _pendingCrushDamage = math.min(MaxCrushDamagePerFlush, _pendingCrushDamage + amount);
                return;
            }

            if (_coreBlackboxWarmed)
                GlobalTelemetryBus.PushEvent(KccCrushDamageEventHash, amount, unchecked((uint)targetId));
        }

        /// <summary>
        /// Resolves a combat target id that is proven REGISTERED before any crush damage is enqueued.
        ///
        /// Two separate defects live here. First, <c>CombatDamageRuntime.TryQueueDamage</c> rejects only
        /// <c>TargetId == 0</c>, so an unregistered id is accepted, silently discarded inside the damage job,
        /// and reported back as <c>true</c> -- the accumulator gets zeroed and a telemetry "success" is pushed
        /// for damage that never landed. Every other producer in the project gates on
        /// <c>IsTargetRegistered</c> first (EnvironmentalHazard, HectonPlayerHealth, HazardZoneManager,
        /// RandomEventSystem, BioReactor, and the rest). Second, the cold cache resolved
        /// <c>ResolveTargetId(gameObject)</c> on the KCC's OWN GameObject, while the cited precedent
        /// (<c>EnvironmentalHazard.TryQueueCentralHazardDamage</c>) resolves it on
        /// <c>playerHealth.gameObject</c>. <c>ResolveTargetId</c> returns nonzero for ANY non-null GameObject,
        /// so hosting this controller anywhere other than the exact GameObject that registered with the combat
        /// runtime yields a plausible-but-wrong id rather than 0 -- and with no registration gate that failure
        /// is completely unobservable at runtime.
        ///
        /// <c>TryResolveRegisteredTarget</c> walks up to six parents and only returns ids that resolve in the
        /// combat target lookup, so it fixes both: it finds the real damage receiver whether it sits on this
        /// GameObject or on an ancestor, and it cannot return an unregistered id. Cost is one call per flush
        /// (<see cref="CrushDamageFlushIntervalSeconds"/>), not per tick, and the cached id short-circuits the
        /// walk while it stays valid.
        /// </summary>
        private bool TryResolveRegisteredCrushDamageTarget(out int targetId)
        {
            targetId = _combatDamageTargetId;
            if (targetId != 0 && Hecton8.Gameplay.CombatDamageRuntime.IsTargetRegistered(targetId))
                return true;

            Transform host = _cachedTransform != null ? _cachedTransform : transform;
            if (Hecton8.Gameplay.CombatDamageRuntime.TryResolveRegisteredTarget(host, out int resolvedId, out _) &&
                resolvedId != 0)
            {
                _combatDamageTargetId = resolvedId;
                targetId = resolvedId;
                return true;
            }

            // No registered receiver: drop the packet rather than enqueue into a void. The caller has already
            // zeroed the accumulator, so depth stops charging until a target registers again.
            _combatDamageTargetId = 0;
            targetId = 0;
            return false;
        }

        private void CacheOceanKinematicsRuntimeCold()
        {
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            // COLD RESOLVE: the weather service is resolved once here so the fixed-tick medium path never
            // touches GlobalRegistry per frame. Hot-swap rebind for it lives in
            // OnGlobalRegistryServiceReplaced, GlobalRegistryServiceSlot.Weather.
            _weatherService = GlobalRegistry.Weather;
            // The combat target id is deliberately NOT resolved from this GameObject here. It is cleared and
            // resolved lazily against the combat target lookup on the first crush flush -- see
            // TryResolveRegisteredCrushDamageTarget for why an own-GameObject id is a silent wrong-target trap
            // and why cold time is too early (combat registration order is not guaranteed relative to OnEnable).
            _combatDamageTargetId = 0;
            ClearWaterMediumState();
        }

        private void ClearWaterMediumState()
        {
            _mediumCurrentDragForce = float3.zero;
            _mediumThermoclineResistance01 = 0f;
            _mediumDensityBuoyancyNewtons = 0f;
            _mediumFlags = 0u;
            _mediumDepthMeters = 0f;
            _pendingCrushDamage = 0f;
            _crushDamageFlushTimer = 0f;
        }

        private float ResolveRuntimeWaterSurfaceY()
        {
            return TryResolveOceanWaterSurfaceY(out float waterSurfaceY)
                ? waterSurfaceY
                : HydrodynamicKccMath.ResolveWaterSurfaceY(_waterSurfaceY);
        }

        private bool TryResolveOceanWaterSurfaceY(out float waterSurfaceY)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceY(oceanKinematics.SeaLevel, out waterSurfaceY))
            {
                return true;
            }

            waterSurfaceY = DefaultWaterSurfaceY;
            return false;
        }

        private static bool TryResolveOceanWaterSurfaceY(float candidateWaterSurfaceY, out float waterSurfaceY)
        {
            if (math.isfinite(candidateWaterSurfaceY) &&
                math.abs(candidateWaterSurfaceY) > HydrodynamicKccMath.MinDenominator &&
                math.abs(candidateWaterSurfaceY) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceY = candidateWaterSurfaceY;
                return true;
            }

            waterSurfaceY = DefaultWaterSurfaceY;
            return false;
        }

        private static KccEnvironmentProfileDTO DefaultEnvironmentProfile()
        {
            return new KccEnvironmentProfileDTO
            {
                MaxSlopeAngle = 48f,
                CurrentAdvectionScalar = 1f,
                FrictionCoefficient = 0.85f,
                ExhaustionPenaltyMax = 0.35f
            };
        }

        private static KccEnvironmentGridDTO DefaultEnvironmentGrid(double3 sectorOrigin)
        {
            return new KccEnvironmentGridDTO
            {
                GridOriginAup = sectorOrigin - new double3(
                    EnvironmentGridAxisX * 0.5d * 2d,
                    2d,
                    EnvironmentGridAxisZ * 0.5d * 2d),
                Dimensions = new int3(EnvironmentGridAxisX, EnvironmentGridAxisY, EnvironmentGridAxisZ),
                CellSizeMeters = 2f,
                SdfSurfaceMeters = 0f,
                SdfFrictionBandMeters = 0.65f,
                Flags = HydrodynamicKccMath.FlagEnvironmentMock
            };
        }

        private KccEnvironmentProfileDTO UpdateEnvironmentProfileSnapshot(NativeArray<KccEnvironmentProfileDTO> profileBuffer)
        {
            if (!profileBuffer.IsCreated || profileBuffer.Length == 0)
                return DefaultEnvironmentProfile();

            KccEnvironmentProfileDTO profile = profileBuffer[0];
            if (profile.MaxSlopeAngle <= 0f)
                profile = DefaultEnvironmentProfile();
            profile = SanitizeEnvironmentProfile(profile);
            profileBuffer[0] = profile;
            return profile;
        }

        private KccEnvironmentGridDTO UpdateEnvironmentGridSnapshot(NativeArray<KccEnvironmentGridDTO> gridBuffer, double3 sectorOrigin)
        {
            if (!gridBuffer.IsCreated || gridBuffer.Length == 0)
                return DefaultEnvironmentGrid(sectorOrigin);

            KccEnvironmentGridDTO grid = gridBuffer[0];
            if (grid.CellSizeMeters <= 0f || !HydrodynamicKccMath.IsFinite(grid.GridOriginAup))
                grid = DefaultEnvironmentGrid(sectorOrigin);
            grid = SanitizeEnvironmentGrid(grid, sectorOrigin);
            grid.Frame = _simulationFrame;
            grid.Flags |= HydrodynamicKccMath.FlagEnvironmentMock;
            gridBuffer[0] = grid;
            return grid;
        }

        private static KccEnvironmentProfileDTO SanitizeEnvironmentProfile(KccEnvironmentProfileDTO profile)
        {
            profile.MaxSlopeAngle = math.clamp(math.isfinite(profile.MaxSlopeAngle) ? profile.MaxSlopeAngle : 48f, 1f, 89f);
            profile.CurrentAdvectionScalar = math.clamp(math.isfinite(profile.CurrentAdvectionScalar) ? profile.CurrentAdvectionScalar : 1f, 0f, 8f);
            profile.FrictionCoefficient = math.clamp(math.isfinite(profile.FrictionCoefficient) ? profile.FrictionCoefficient : 0.85f, 0f, 8f);
            profile.ExhaustionPenaltyMax = math.saturate(math.isfinite(profile.ExhaustionPenaltyMax) ? profile.ExhaustionPenaltyMax : 0.35f);
            return profile;
        }

        private static KccEnvironmentGridDTO SanitizeEnvironmentGrid(KccEnvironmentGridDTO grid, double3 sectorOrigin)
        {
            if (!HydrodynamicKccMath.IsFinite(grid.GridOriginAup))
                grid.GridOriginAup = sectorOrigin;
            grid.Dimensions = new int3(
                math.clamp(grid.Dimensions.x, 1, EnvironmentGridAxisX),
                math.clamp(grid.Dimensions.y, 1, EnvironmentGridAxisY),
                math.clamp(grid.Dimensions.z, 1, EnvironmentGridAxisZ));
            grid.CellSizeMeters = math.max(0.25f, math.isfinite(grid.CellSizeMeters) ? grid.CellSizeMeters : 2f);
            grid.SdfSurfaceMeters = math.isfinite(grid.SdfSurfaceMeters) ? grid.SdfSurfaceMeters : 0f;
            grid.SdfFrictionBandMeters = math.max(0.05f, math.isfinite(grid.SdfFrictionBandMeters) ? grid.SdfFrictionBandMeters : 0.65f);
            return grid;
        }

        private void SeedInitialStateIfNeeded(NativeArray<KinematicStateDTO> states, HydrodynamicKccTuningDTO tuning, double3 sectorOrigin, int capacity)
        {
            if (!states.IsCreated || states.Length == 0)
                return;

            Vector3 local = _cachedTransform != null ? _cachedTransform.localPosition : Vector3.zero;
            int count = math.clamp(capacity, 0, states.Length);
            float spacing = math.max(0.25f, tuning.CapsuleRadius * 2.5f);
            for (int i = 0; i < count; i++)
            {
                KinematicStateDTO state = states[i];
                if (HydrodynamicKccMath.IsFinite(state.AUP_Position) &&
                    HydrodynamicKccMath.IsFinite(state.Velocity) &&
                    HydrodynamicKccMath.IsFinite(state.AngularVelocity) &&
                    math.isfinite(state.Mass) &&
                    state.Mass > 0f &&
                    math.isfinite(state.DragCoefficient))
                {
                    continue;
                }

                float offsetX = (float)(i & 3) * spacing;
                float offsetZ = (float)(i >> 2) * spacing;
                states[i] = new KinematicStateDTO
                {
                    AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(sectorOrigin + new double3(local.x + offsetX, local.y, local.z + offsetZ)),
                    Velocity = float3.zero,
                    AngularVelocity = float3.zero,
                    Mass = 80f,
                    DragCoefficient = tuning.BaseDrag
                };
            }
        }

        private double3 ResolveSectorOriginAup()
        {
            return HectonFloatingOrigin.CurrentTotalOffsetDouble;
        }

        private float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float value = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(value) ? value : 1f);
        }

        private void DumpTelemetry(int faultMask, NativeArray<KinematicTelemetryEntry> telemetry, NativeArray<KccEnvironmentTelemetryEntry> environmentTelemetry)
        {
            if ((!telemetry.IsCreated || telemetry.Length == 0) &&
                (!environmentTelemetry.IsCreated || environmentTelemetry.Length == 0))
                return;

            if (!_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            uint stateHash = unchecked((uint)faultMask);
            float scalar = faultMask;
            if (telemetry.IsCreated && telemetry.Length > 0)
            {
                int index = (int)(_simulationFrame % (uint)telemetry.Length);
                KinematicTelemetryEntry sample = telemetry[index];
                scalar = math.isfinite(sample.Speed) ? sample.Speed : scalar;
                stateHash = sample.StateHash != 0u ? sample.StateHash : stateHash;
            }
            else if (environmentTelemetry.IsCreated && environmentTelemetry.Length > 0)
            {
                int index = (int)(_simulationFrame % (uint)environmentTelemetry.Length);
                KccEnvironmentTelemetryEntry sample = environmentTelemetry[index];
                scalar = math.isfinite(sample.ComputeMicroseconds) ? sample.ComputeMicroseconds : scalar;
                stateHash = sample.StateHash != 0u ? sample.StateHash : stateHash;
            }

            GlobalTelemetryBus.PushEvent(KccFaultEventHash, scalar, stateHash);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(KccFaultDumpHash);
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed || !Application.isPlaying)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredFixedTick || !Application.isPlaying)
                return;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterFixedTick()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixedTick = false;
        }

        private void TryRegisterPostFixedTick()
        {
            if (_registeredPostFixedTick || !Application.isPlaying)
                return;

            _registeredPostFixedTick = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPostFixedTick()
        {
            if (!_registeredPostFixedTick)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixedTick = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void DrainPendingJobsForTeardown()
        {
            AbortScheduledBatchForTeardown();
        }

        private void OnDrawGizmos()
        {
            CapsuleCollider capsule = _capsule;
            float radius = capsule != null ? math.max(0.05f, capsule.radius) : 0.35f;
            float height = capsule != null ? math.max(radius * 2f, capsule.height) : 1.8f;
            float halfHeight = math.max(radius, (height * 0.5f) - radius);
            Vector3 current = transform.position;
            Vector3 predicted = current + new Vector3(_lastGizmoPredicted.x - _lastGizmoCurrent.x, _lastGizmoPredicted.y - _lastGizmoCurrent.y, _lastGizmoPredicted.z - _lastGizmoCurrent.z);
            Gizmos.color = Color.green;
            DrawCapsuleGizmo(current, halfHeight, radius);
            Gizmos.color = Color.yellow;
            DrawCapsuleGizmo(predicted, halfHeight, radius);
            Gizmos.color = Color.red;
            Vector3 normal = new Vector3(_lastGizmoNormal.x, _lastGizmoNormal.y, _lastGizmoNormal.z);
            if (normal.sqrMagnitude > 0.0001f)
            {
                float3 normalDirection = HydrodynamicKccMath.NormalizeSafe(_lastGizmoNormal, new float3(0f, 1f, 0f));
                Gizmos.DrawLine(predicted, predicted + new Vector3(normalDirection.x, normalDirection.y, normalDirection.z));
            }
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.9f);
            Vector3 flow = new Vector3(_lastGizmoFlow.x, _lastGizmoFlow.y, _lastGizmoFlow.z);
            if (flow.sqrMagnitude > 0.0001f)
                Gizmos.DrawLine(predicted, predicted + flow);
            Gizmos.color = new Color(1f, 0.35f, 0f, 0.9f);
            Vector3 slide = new Vector3(_lastGizmoSlopeSlide.x, _lastGizmoSlopeSlide.y, _lastGizmoSlopeSlide.z);
            if (slide.sqrMagnitude > 0.0001f)
                Gizmos.DrawLine(predicted, predicted + slide);
        }

        private static void DrawCapsuleGizmo(Vector3 center, float halfHeight, float radius)
        {
            Vector3 top = center + Vector3.up * halfHeight;
            Vector3 bottom = center - Vector3.up * halfHeight;
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
            Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
        }
        // The four JulesLink_* keep-alive stubs that used to sit here (ThermoclineResistanceCalculator,
        // PressureCrushDamageModel, OceanCurrentDragCalculator, BuoyancyDensityRatioMath) are removed: all
        // four models now have real call sites in UpdateWaterMediumForces, so a `_ = typeof(X)` stub would
        // only keep advertising them as unwired.
    }
}
