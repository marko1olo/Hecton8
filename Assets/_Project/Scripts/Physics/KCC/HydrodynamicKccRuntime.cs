using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics.KCC
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct KinematicStateDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float3 AngularVelocity;
        [FieldOffset(48)] public float Mass;
        [FieldOffset(52)] public float DragCoefficient;
        [FieldOffset(56)] public byte _pad0;
        [FieldOffset(57)] public byte _pad1;
        [FieldOffset(58)] public byte _pad2;
        [FieldOffset(59)] public byte _pad3;
        [FieldOffset(60)] public byte _pad4;
        [FieldOffset(61)] public byte _pad5;
        [FieldOffset(62)] public byte _pad6;
        [FieldOffset(63)] public byte _pad7;
    }

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
        [FieldOffset(32)] public ulong _pad0;
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

#if UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential, Size = 56)]
    public struct HydrodynamicKccLayoutReport
    {
        public int StateSize;
        public int OffsetAup;
        public int OffsetVelocity;
        public int OffsetAngularVelocity;
        public int OffsetMass;
        public int OffsetDragCoefficient;
        public int TuningSize;
        public int TelemetrySize;
        public int WakePacketSize;
        public int DebugSize;
        public int FaultFlagSize;
        public int CollisionHitSize;
        public int InputSize;
        public int _pad0;
    }

    public static class HydrodynamicKccLayoutValidator
    {
        public const int KinematicStateSize = 64;
        public const int KinematicStateAupOffset = 0;
        public const int KinematicStateVelocityOffset = 24;
        public const int KinematicStateAngularVelocityOffset = 36;
        public const int KinematicStateMassOffset = 48;
        public const int KinematicStateDragOffset = 52;

        public static bool ValidateRuntimeLayout(out HydrodynamicKccLayoutReport report)
        {
            report = new HydrodynamicKccLayoutReport
            {
                StateSize = UnsafeUtility.SizeOf<KinematicStateDTO>(),
                OffsetAup = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.AUP_Position)),
                OffsetVelocity = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Velocity)),
                OffsetAngularVelocity = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.AngularVelocity)),
                OffsetMass = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Mass)),
                OffsetDragCoefficient = OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.DragCoefficient)),
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
                   report.OffsetDragCoefficient == KinematicStateDragOffset &&
                   report.TuningSize == 64 &&
                   report.TelemetrySize == 64 &&
                   report.WakePacketSize == 64 &&
                   report.DebugSize == 64 &&
                   report.FaultFlagSize == 64 &&
                   report.CollisionHitSize == 64 &&
                   report.InputSize == 64;
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
        public const float MinDenominator = 0.0001f;
        public const float MillimeterScale = 1000f;
        public const float MaxLocalFloatMagnitude = 131072f;
        public const uint SourceHash = 0x53484B43u;
        public const uint WakeSourcePlayer = 1u;
        public const uint HitFlagValid = 1u;

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
        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return lenSq > 0.000001f && math.isfinite(lenSq)
                ? value * math.rsqrt(math.max(lenSq, 0.000001f))
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 QuantizeMillimeter(double3 aup)
        {
            return math.round(aup * MillimeterScale) / MillimeterScale;
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
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return math.max(2, (int)math.lerp(2f, 8f, quality));
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
        public static uint PackWakeSourceFlags(uint sourceKind, float magnitude, float radius)
        {
            float safeMagnitude = math.max(0f, math.isfinite(magnitude) ? magnitude : 0f);
            float safeRadius = math.max(0f, math.isfinite(radius) ? radius : 0f);
            uint magnitudeQ = (uint)math.clamp((int)math.round(safeMagnitude * 64f), 0, 4095);
            uint radiusQ = (uint)math.clamp((int)math.round(safeRadius * 64f), 0, 4095);
            return (sourceKind & 0xFFu) | (magnitudeQ << 8) | (radiusQ << 20);
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
            uint hash = 2166136261u;
            hash = Fnv(hash, math.asuint((float)(aup.x - math.floor(aup.x))));
            hash = Fnv(hash, math.asuint((float)(aup.y - math.floor(aup.y))));
            hash = Fnv(hash, math.asuint((float)(aup.z - math.floor(aup.z))));
            hash = Fnv(hash, math.asuint(velocity.x));
            hash = Fnv(hash, math.asuint(velocity.y));
            hash = Fnv(hash, math.asuint(velocity.z));
            hash = Fnv(hash, frame);
            hash = Fnv(hash, flags);
            return hash;
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
            float quality = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            float frequency = math.max(0.01f, math.isfinite(tuning.MockInputFrequency) ? tuning.MockInputFrequency : 0.35f);
            float amplitude = math.max(0f, math.isfinite(tuning.MockInputAmplitude) ? tuning.MockInputAmplitude : 1f);
            float phase = rng.NextFloat(0f, 6.2831855f);
            float t = (frame + (uint)index) * safeDt;
            float forward = (0.55f + 0.45f * math.sin(t * frequency + phase)) * amplitude;
            float strafe = math.sin(t * (frequency * 0.37f) + phase * 0.5f) * math.lerp(0.05f, 0.25f, quality);

            return new HydrodynamicKccInputDTO
            {
                TargetAup = anchorAup,
                MoveAxis = new float3(strafe, 0f, forward),
                LookAxis = new float3(strafe, 0f, 1f),
                SimulationFrame = frame,
                Sequence = (uint)index,
                Flags = HydrodynamicKccMath.FlagMockInput,
                SourceHash = HydrodynamicKccMath.SourceHash
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockMovementInputQueueJob : IJobParallelFor
    {
        [NoAlias] public NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter InputWriter;
        public double3 AnchorAup;
        public HydrodynamicKccTuningDTO Tuning;
        public uint SimulationFrame;
        public uint SectorHash;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            InputWriter.Enqueue(GenerateMockMovementInputJob.BuildInput(index, AnchorAup, Tuning, SimulationFrame, SectorHash, SimulationTickDelta));
        }
    }

    /// <summary>Deterministic mock-input harness for profiling the hydrodynamic KCC without managed input systems.</summary>
    public static class HydrodynamicKccMockInput
    {
        /// <summary>Schedules deterministic movement input packets into a caller-owned native queue.</summary>
        public static JobHandle GenerateMockMovementInput(
            NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter inputWriter,
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
    public unsafe struct HydrodynamicIntegrationJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;
        [WriteOnly, NoAlias] public NativeArray<float3> ProposedVelocities;
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicWakePacketDTO> WakePackets;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicTelemetryEntry> TelemetryRing;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TelemetryCursor;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        public HydrodynamicKccTuningDTO Tuning;
        public double3 SectorOriginAup;
        public uint SimulationFrame;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States) + (index * stateSize);
            ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statePtr);

            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float quality = math.saturate(math.isfinite(Tuning.GlobalQualityWeight) ? Tuning.GlobalQualityWeight : 1f);
            HydrodynamicKccInputDTO input = index < Inputs.Length ? Inputs[index] : default;

            float3 velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
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
            float acceleration = math.lerp(maxSpeed * 2.2f, maxSpeed * 5.4f, quality) * mass * math.rcp(safeMass);
            velocity += moveDir * acceleration * dt;

            float3 localPosition = HydrodynamicKccMath.ResolveLocalFloat3(state.AUP_Position, SectorOriginAup);
            float waterSurfaceY = math.isfinite(Tuning.WaterSurfaceY) ? Tuning.WaterSurfaceY : 0f;
            float depth = math.max(0f, waterSurfaceY - localPosition.y);
            float submersion = math.saturate(depth * math.rcp(math.max(0.1f, height)));
            submersion = submersion * submersion * (3f - 2f * submersion);
            float gravity = 9.80665f * math.max(0f, math.isfinite(Tuning.GravityMultiplier) ? Tuning.GravityMultiplier : 1f);
            float buoyancy = (math.max(0f, Tuning.BuoyancyScalar) * submersion * mass - mass) * gravity * math.rcp(safeMass);
            velocity += new float3(0f, buoyancy * dt, 0f);

            float speedBeforeDrag = math.length(velocity);
            float baseDrag = math.max(0f, math.isfinite(Tuning.BaseDrag) ? Tuning.BaseDrag : 0.18f);
            float drag = (stateDrag + baseDrag) * math.lerp(0.35f, 1.15f, quality);
            float dragDenominator = math.max(HydrodynamicKccMath.MinDenominator, 1f + drag * speedBeforeDrag * dt);
            velocity *= math.rcp(dragDenominator);

            float speedSq = math.lengthsq(velocity);
            if (speedSq > maxSpeed * maxSpeed)
                velocity *= maxSpeed * math.rsqrt(math.max(speedSq, 0.000001f));

            bool invalid = !HydrodynamicKccMath.IsFinite(state.AUP_Position) || !HydrodynamicKccMath.IsFinite(velocity);
            if (invalid)
            {
                velocity = float3.zero;
                state.AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(HydrodynamicKccMath.Sanitize(state.AUP_Position, SectorOriginAup));
                WriteFault(index, HydrodynamicKccMath.FlagFaultNaN);
            }

            float speed = math.length(velocity);
            float normalizedSpeed = speed * math.rcp(math.max(0.1f, maxSpeed));
            float turbulence = math.saturate(normalizedSpeed * normalizedSpeed) * math.lerp(0.18f, 1f, quality);
            float wakeRadius = math.lerp(1.2f, 3.6f, math.saturate(turbulence * math.lerp(0.72f, 1.18f, quality)));
            uint flags = input.Flags | math.select(0u, HydrodynamicKccMath.FlagFaultNaN, invalid);
            uint wakeFlags = math.select(0u, HydrodynamicKccMath.FlagWake, speed > math.max(0.01f, Tuning.WakeThreshold));

            state.Velocity = velocity;
            state.AngularVelocity = HydrodynamicKccMath.Sanitize(state.AngularVelocity, float3.zero) *
                                    math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + drag * dt));
            state.Mass = mass;
            state.DragCoefficient = stateDrag;
            ProposedVelocities[index] = velocity;
            if (index < WakePackets.Length)
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
                    Flags = wakeFlags
                };
            }

            float computeUs = HydrodynamicKccMath.EstimateIntegrationMicroseconds(quality, speed);
            WriteTelemetry(index, state.AUP_Position, velocity, speed, turbulence, computeUs, SimulationFrame, flags, 0u);
        }

        private void WriteFault(int index, uint faultMask)
        {
            if (!FaultFlags.IsCreated || index >= FaultFlags.Length)
                return;

            HydrodynamicKccFaultFlagDTO entry = FaultFlags[index];
            entry.FaultMask |= (int)faultMask;
            FaultFlags[index] = entry;
        }

        private void WriteTelemetry(int index, double3 aup, float3 velocity, float speed, float turbulence, float computeUs, uint frame, uint flags, uint iterations)
        {
            if (index != 0 || !TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                return;

            int ringIndex = (int)(frame % (uint)TelemetryRing.Length);
            TelemetryRing[ringIndex] = new KinematicTelemetryEntry
            {
                AupPosition = aup,
                Velocity = velocity,
                Speed = speed,
                TurbulenceScalar = turbulence,
                ComputeMicroseconds = computeUs,
                Frame = frame,
                StateHash = HydrodynamicKccMath.HashState(aup, velocity, frame, flags),
                Flags = flags,
                Iterations = iterations
            };

            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = ringIndex;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildCapsuleCastCommandsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<float3> ProposedVelocities;
        [WriteOnly, NoAlias] public NativeArray<CapsulecastCommand> Commands;
        public HydrodynamicKccTuningDTO Tuning;
        public double3 SectorOriginAup;
        public QueryParameters QueryParameters;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            KinematicStateDTO state = States[index];
            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float3 velocity = HydrodynamicKccMath.Sanitize(ProposedVelocities[index], float3.zero);
            float3 delta = velocity * dt;
            float castDistance = math.length(delta);
            float3 direction = HydrodynamicKccMath.NormalizeSafe(delta, new float3(0f, 0f, 1f));
            float radius = math.max(0.05f, math.isfinite(Tuning.CapsuleRadius) ? Tuning.CapsuleRadius : 0.35f);
            float skin = math.max(0.001f, math.isfinite(Tuning.SkinWidth) ? Tuning.SkinWidth : 0.02f);
            float halfHeight = math.max(radius, (math.max(radius * 2f, Tuning.CapsuleHeight) * 0.5f) - radius);
            float3 center = HydrodynamicKccMath.ResolveLocalFloat3(state.AUP_Position, SectorOriginAup);
            float3 point1 = center - new float3(0f, halfHeight, 0f);
            float3 point2 = center + new float3(0f, halfHeight, 0f);
            Vector3 commandPoint1 = new Vector3(point1.x, point1.y, point1.z);
            Vector3 commandPoint2 = new Vector3(point2.x, point2.y, point2.z);
            Vector3 commandDirection = new Vector3(direction.x, direction.y, direction.z);

            Commands[index] = new CapsulecastCommand(
                commandPoint1,
                commandPoint2,
                radius,
                commandDirection,
                QueryParameters,
                castDistance + skin);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ExtractCapsuleCastHitsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<RaycastHit> RawHits;
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccCollisionHitDTO> Hits;
        public int MaxHitsPerCommand;

        public void Execute(int index)
        {
            if (index >= Hits.Length)
                return;

            RaycastHit hit = index < RawHits.Length ? RawHits[index] : default;
            Vector3 point = hit.point;
            Vector3 normalVector = hit.normal;
            float3 point3 = new float3(point.x, point.y, point.z);
            float3 normal = HydrodynamicKccMath.NormalizeSafe(
                new float3(normalVector.x, normalVector.y, normalVector.z),
                float3.zero);
            float distance = math.max(0f, math.isfinite(hit.distance) ? hit.distance : 0f);
            bool valid = MaxHitsPerCommand > 0 &&
                         math.isfinite(distance) &&
                         HydrodynamicKccMath.IsFinite(point3) &&
                         HydrodynamicKccMath.IsFinite(normal) &&
                         math.lengthsq(normal) > 0.0001f;

            Hits[index] = new HydrodynamicKccCollisionHitDTO
            {
                Point = valid ? point3 : float3.zero,
                Distance = valid ? distance : 0f,
                Normal = valid ? normal : float3.zero,
                Flags = math.select(0u, HydrodynamicKccMath.HitFlagValid, valid)
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct KinematicResolutionJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        [WriteOnly, NoAlias] public NativeArray<double3> PreviousAup;
        [ReadOnly, NoAlias] public NativeArray<float3> ProposedVelocities;
        [ReadOnly, NoAlias] public NativeArray<HydrodynamicKccCollisionHitDTO> CollisionHits;
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccDebugOutputDTO> DebugOutputs;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicTelemetryEntry> TelemetryRing;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TelemetryCursor;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        public HydrodynamicKccTuningDTO Tuning;
        public double3 SectorOriginAup;
        public uint SimulationFrame;
        public float SimulationTickDelta;
        public int MaxHitsPerCommand;

        public void Execute(int index)
        {
            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States) + (index * stateSize);
            ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statePtr);

            double3 previous = state.AUP_Position;
            float dt = math.max(HydrodynamicKccMath.MinDenominator, math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
            float3 velocity = HydrodynamicKccMath.Sanitize(ProposedVelocities[index], float3.zero);
            float3 displacement = velocity * dt;
            float castDistance = math.length(displacement);
            float3 direction = HydrodynamicKccMath.NormalizeSafe(displacement, new float3(0f, 0f, 1f));
            float skin = math.max(0.001f, math.isfinite(Tuning.SkinWidth) ? Tuning.SkinWidth : 0.02f);
            int iterations = HydrodynamicKccMath.ResolveIterationCount(Tuning.GlobalQualityWeight);
            uint flags = 0u;
            int scheduledHitStride = math.max(1, MaxHitsPerCommand);
            int executedIterations = math.clamp(iterations, 1, scheduledHitStride);
            int hitBase = index * scheduledHitStride;
            bool hasHit = false;
            float nearestDistance = castDistance + skin;
            float3 nearestNormal = float3.zero;

            for (int i = 0; i < executedIterations; i++)
            {
                int hitIndex = hitBase + i;
                if (hitIndex >= CollisionHits.Length)
                    break;

                HydrodynamicKccCollisionHitDTO hit = CollisionHits[hitIndex];
                bool validHit = (hit.Flags & HydrodynamicKccMath.HitFlagValid) != 0u &&
                                hit.Distance > 0f &&
                                hit.Distance <= castDistance + skin + 0.001f &&
                                HydrodynamicKccMath.IsFinite(hit.Normal) &&
                                math.lengthsq(hit.Normal) > 0.0001f;
                if (!validHit)
                    continue;

                hasHit = true;
                flags |= HydrodynamicKccMath.FlagCollision;
                if (hit.Distance <= nearestDistance)
                {
                    nearestDistance = hit.Distance;
                    nearestNormal = hit.Normal;
                }

                float intoNormal = math.dot(velocity, hit.Normal);
                float contactWeight = 1f - math.step(0f, intoNormal);
                velocity -= hit.Normal * intoNormal * contactWeight;
            }

            if (hasHit)
            {
                float allowedDistance = math.max(0f, nearestDistance - skin);
                displacement = direction * allowedDistance + velocity * dt * math.saturate(1f - math.step(castDistance, 0.0001f));
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

            float speed = math.length(velocity);
            float quality = math.saturate(math.isfinite(Tuning.GlobalQualityWeight) ? Tuning.GlobalQualityWeight : 1f);
            float maxSpeed = math.max(0.1f, math.isfinite(Tuning.MaxSpeed) ? Tuning.MaxSpeed : 6f);
            float normalizedSpeed = speed * math.rcp(math.max(0.1f, maxSpeed));
            float turbulence = math.saturate(normalizedSpeed * normalizedSpeed) * math.lerp(0.18f, 1f, quality);
            float computeUs = HydrodynamicKccMath.EstimateIntegrationMicroseconds(quality, speed) +
                              HydrodynamicKccMath.EstimateResolutionMicroseconds(quality, (uint)executedIterations, hasHit, speed);
            WriteTelemetry(index, state.AUP_Position, velocity, speed, turbulence, computeUs, SimulationFrame, flags, (uint)executedIterations);
        }

        private void WriteFault(int index, uint faultMask)
        {
            if (!FaultFlags.IsCreated || index >= FaultFlags.Length)
                return;

            HydrodynamicKccFaultFlagDTO entry = FaultFlags[index];
            entry.FaultMask |= (int)faultMask;
            FaultFlags[index] = entry;
        }

        private void WriteTelemetry(int index, double3 aup, float3 velocity, float speed, float turbulence, float computeUs, uint frame, uint flags, uint iterations)
        {
            if (index != 0 || !TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                return;

            int ringIndex = (int)(frame % (uint)TelemetryRing.Length);
            TelemetryRing[ringIndex] = new KinematicTelemetryEntry
            {
                AupPosition = aup,
                Velocity = velocity,
                Speed = speed,
                TurbulenceScalar = turbulence,
                ComputeMicroseconds = computeUs,
                Frame = frame,
                StateHash = HydrodynamicKccMath.HashState(aup, velocity, frame, flags),
                Flags = flags,
                Iterations = iterations
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
            float quality = math.saturate(math.isfinite(Tuning.GlobalQualityWeight) ? Tuning.GlobalQualityWeight : 1f);
            float alpha = 1f - math.exp(-sharpness * dt);
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
                Speed = math.length(state.Velocity),
                Flags = flags,
                Frame = SimulationFrame
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct KinematicRollbackFenceJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> RollbackBytes;
        public int EntityCount;

        public void Execute()
        {
            int count = math.clamp(EntityCount, 0, States.Length);
            int bytes = count * UnsafeUtility.SizeOf<KinematicStateDTO>();
            if (bytes <= 0 || !RollbackBytes.IsCreated || RollbackBytes.Length < bytes)
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
        [NoAlias] public NativeQueue<WakeGeneratedSignal>.ParallelWriter WakeWriter;

        public void Execute(int index)
        {
            HydrodynamicWakePacketDTO packet = WakePackets[index];
            if ((packet.Flags & HydrodynamicKccMath.FlagWake) == 0u)
                return;

            float magnitude = math.max(packet.WakeMagnitude, math.length(packet.Velocity));
            float radius = math.max(0.1f, packet.WakeRadius);
            float3 direction = HydrodynamicKccMath.NormalizeSafe(packet.Velocity, new float3(0f, 0f, 1f));
            WakeGeneratedSignal signal = new WakeGeneratedSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(packet.AupPosition),
                Velocity = direction * magnitude,
                SourceFlags = HydrodynamicKccMath.PackWakeSourceFlags(HydrodynamicKccMath.WakeSourcePlayer, magnitude, radius)
            };
            WakeWriter.Enqueue(signal);
        }
    }

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

    public sealed class HydrodynamicKccRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IScalabilityChangedEventListener
    {
        private const int DefaultCapacity = 1;
        private const int TelemetryCapacity = 300;
        private const int MaxCollisionHitsPerCommand = 8;
        private const int DefaultFluidProfileCapacity = 64;
        private const int DefaultFluidProfileBucketCount = 128;
        private const string DumpFileName = "Dump_KINEMATICS_SURGEON.bin";

        [SerializeField] private int _entityCapacity = DefaultCapacity;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private float _waterSurfaceY;
        [SerializeField] private bool _applyVisualToTransform = true;
        [SerializeField] private bool _runMockInput = true;
        [SerializeField] private bool _consumeExternalInputBuffer;
        [SerializeField] private int _maxRollbackFastForwardFrames = 8;

        private IDataVault _dataVault;
        private Transform _cachedTransform;
        private CapsuleCollider _capsule;
        private VaultBufferHandle<KinematicStateDTO> _statesHandle;
        private VaultBufferHandle<HydrodynamicKccInputDTO> _inputsHandle;
        private VaultBufferHandle<float3> _proposedVelocitiesHandle;
        private VaultBufferHandle<CapsulecastCommand> _collisionCommandsHandle;
        private VaultBufferHandle<RaycastHit> _collisionHitsHandle;
        private VaultBufferHandle<double3> _previousAupHandle;
        private VaultBufferHandle<HydrodynamicKccVisualOutputDTO> _visualOutputsHandle;
        private VaultBufferHandle<KinematicTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<HydrodynamicKccTuningDTO> _tuningHandle;
        private VaultBufferHandle<byte> _rollbackBytesHandle;
        private VaultBufferHandle<HydrodynamicKccFaultFlagDTO> _faultFlagsHandle;
        private VaultBufferHandle<HydrodynamicWakePacketDTO> _wakePacketsHandle;
        private VaultBufferHandle<HydrodynamicKccDebugOutputDTO> _debugOutputsHandle;
        private VaultBufferHandle<HydrodynamicKccCollisionHitDTO> _resolvedHitsHandle;
        private VaultBufferHandle<HydrodynamicFluidProfileDTO> _fluidProfilesHandle;
        private VaultBufferHandle<int> _fluidProfileBucketsHandle;
        private JobHandle _inputHandle;
        private JobHandle _integrationHandle;
        private JobHandle _commandHandle;
        private JobHandle _collisionHandle;
        private JobHandle _hitExtractHandle;
        private JobHandle _postSimulationHandle;
        private JobHandle _externalInputHandle;
        private bool _registeredFixedTick;
        private bool _registeredPostFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwap;
        private bool _registeredScalability;
        private bool _collisionScheduled;
        private bool _postScheduled;
        private bool _externalInputArmed;
        private int _dumpedFaultMask;
        private int _rollbackVisualBypassFrames;
        private int _resolvedBufferCapacity;
        private int _scheduledMaxHitsPerCommand;
        private uint _simulationFrame;
        private float _globalQualityWeight = 1f;
        private float3 _lastGizmoCurrent;
        private float3 _lastGizmoPredicted;
        private float3 _lastGizmoNormal;

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _capsule);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _dataVault = GlobalRegistry.DataVault;
            _globalQualityWeight = ResolveGlobalQualityWeight();
            SignalBus<WakeGeneratedSignal>.EnsureInitialized();
            EnsureVaultBuffers();
            TryRegisterFixedTick();
            TryRegisterPostFixedTick();
            TryRegisterLateFrameTick();
            TryRegisterHotSwap();
            TryRegisterScalability();
        }

        private void OnDisable()
        {
            DrainPendingJobsForTeardown();
            _postScheduled = false;
            _collisionScheduled = false;
            TryUnregisterScalability();
            TryUnregisterHotSwap();
            TryUnregisterLateFrameTick();
            TryUnregisterPostFixedTick();
            TryUnregisterFixedTick();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_collisionScheduled || _postScheduled || !EnsureVaultBuffers())
                return;

            NativeArray<KinematicStateDTO> states = _statesHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccInputDTO> inputs = _inputsHandle.Resolve(_dataVault);
            NativeArray<float3> proposed = _proposedVelocitiesHandle.Resolve(_dataVault);
            NativeArray<CapsulecastCommand> commands = _collisionCommandsHandle.Resolve(_dataVault);
            NativeArray<RaycastHit> hits = _collisionHitsHandle.Resolve(_dataVault);
            NativeArray<KinematicTelemetryEntry> telemetry = _telemetryRingHandle.Resolve(_dataVault);
            NativeArray<int> cursor = _telemetryCursorHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccFaultFlagDTO> faults = _faultFlagsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicWakePacketDTO> wakePackets = _wakePacketsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccTuningDTO> tuningBuffer = _tuningHandle.Resolve(_dataVault);

            if (!states.IsCreated || !inputs.IsCreated || !proposed.IsCreated || !commands.IsCreated || !hits.IsCreated || !tuningBuffer.IsCreated)
                return;

            HydrodynamicKccTuningDTO tuning = ResolveTuning(tuningBuffer);
            double3 sectorOrigin = ResolveSectorOriginAup();
            int capacity = math.min(_entityCapacity, states.Length);
            int maxHits = HydrodynamicKccMath.ResolveIterationCount(tuning.GlobalQualityWeight);
            if (capacity <= 0)
                return;

            _scheduledMaxHitsPerCommand = maxHits;
            SeedInitialStateIfNeeded(states, tuning, sectorOrigin, capacity);
            _simulationFrame++;
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
                    SectorHash = HydrodynamicKccMath.HashState(sectorOrigin, float3.zero, _simulationFrame, 0u),
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

            _integrationHandle = new HydrodynamicIntegrationJob
            {
                States = states,
                Inputs = inputs,
                ProposedVelocities = proposed,
                WakePackets = wakePackets,
                TelemetryRing = telemetry,
                TelemetryCursor = cursor,
                FaultFlags = faults,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                SimulationFrame = _simulationFrame,
                SimulationTickDelta = fixedDeltaTime
            }.Schedule(capacity, 32, _inputHandle);

            _commandHandle = new BuildCapsuleCastCommandsJob
            {
                States = states,
                ProposedVelocities = proposed,
                Commands = commands,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                QueryParameters = new QueryParameters(_collisionMask.value, false, QueryTriggerInteraction.Ignore),
                SimulationTickDelta = fixedDeltaTime
            }.Schedule(capacity, 32, _integrationHandle);

            _collisionHandle = CapsulecastCommand.ScheduleBatch(commands, hits, 1, maxHits, _commandHandle);
            _collisionScheduled = true;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_collisionScheduled || _postScheduled || !EnsureVaultBuffers())
                return;

            NativeArray<KinematicStateDTO> states = _statesHandle.Resolve(_dataVault);
            NativeArray<float3> proposed = _proposedVelocitiesHandle.Resolve(_dataVault);
            NativeArray<RaycastHit> hits = _collisionHitsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccCollisionHitDTO> resolvedHits = _resolvedHitsHandle.Resolve(_dataVault);
            NativeArray<double3> previous = _previousAupHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccVisualOutputDTO> visual = _visualOutputsHandle.Resolve(_dataVault);
            NativeArray<KinematicTelemetryEntry> telemetry = _telemetryRingHandle.Resolve(_dataVault);
            NativeArray<int> cursor = _telemetryCursorHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccFaultFlagDTO> faults = _faultFlagsHandle.Resolve(_dataVault);
            NativeArray<byte> rollbackBytes = _rollbackBytesHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicWakePacketDTO> wakePackets = _wakePacketsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccDebugOutputDTO> debugOutputs = _debugOutputsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccTuningDTO> tuningBuffer = _tuningHandle.Resolve(_dataVault);
            if (!states.IsCreated || !proposed.IsCreated || !hits.IsCreated || !resolvedHits.IsCreated || !previous.IsCreated || !visual.IsCreated || !tuningBuffer.IsCreated)
                return;

            HydrodynamicKccTuningDTO tuning = ResolveTuning(tuningBuffer);
            double3 sectorOrigin = ResolveSectorOriginAup();
            int capacity = math.min(_entityCapacity, states.Length);
            int maxHits = math.clamp(_scheduledMaxHitsPerCommand, 1, MaxCollisionHitsPerCommand);
            int bypassVisualSync = _rollbackVisualBypassFrames > 0 ? 1 : 0;

            _hitExtractHandle = new ExtractCapsuleCastHitsJob
            {
                RawHits = hits,
                Hits = resolvedHits,
                MaxHitsPerCommand = maxHits
            }.Schedule(capacity * maxHits, 32, _collisionHandle);

            JobHandle resolutionHandle = new KinematicResolutionJob
            {
                States = states,
                PreviousAup = previous,
                ProposedVelocities = proposed,
                CollisionHits = resolvedHits,
                DebugOutputs = debugOutputs,
                TelemetryRing = telemetry,
                TelemetryCursor = cursor,
                FaultFlags = faults,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                SimulationFrame = _simulationFrame,
                SimulationTickDelta = fixedDeltaTime,
                MaxHitsPerCommand = maxHits
            }.Schedule(capacity, 32, _hitExtractHandle);

            JobHandle visualHandle = new KinematicVisualSyncJob
            {
                States = states,
                PreviousAup = previous,
                VisualOutputs = visual,
                CameraOrSectorAup = sectorOrigin,
                Tuning = tuning,
                SimulationFrame = _simulationFrame,
                VisualDeltaTime = fixedDeltaTime,
                BypassVisualSync = bypassVisualSync
            }.Schedule(capacity, 32, resolutionHandle);

            JobHandle rollbackHandle = new KinematicRollbackFenceJob
            {
                States = states,
                RollbackBytes = rollbackBytes,
                EntityCount = capacity
            }.Schedule(resolutionHandle);

            JobHandle wakeHandle = new EmitWakeSignalsJob
            {
                WakePackets = wakePackets,
                WakeWriter = SignalBus<WakeGeneratedSignal>.ParallelWriter
            }.Schedule(capacity, 32, resolutionHandle);

            _postSimulationHandle = JobHandle.CombineDependencies(visualHandle, rollbackHandle, wakeHandle);
            if (_rollbackVisualBypassFrames > 0)
                _rollbackVisualBypassFrames--;
            _postScheduled = true;
            _collisionScheduled = false;
            _scheduledMaxHitsPerCommand = 0;
        }

        public bool TryRunRollbackResimulation(int requestedFrames, float fixedDeltaTime)
        {
            if (requestedFrames <= 0 || !Application.isPlaying)
                return false;

            DrainPendingJobsForTeardown();
            _postScheduled = false;
            _collisionScheduled = false;
            _scheduledMaxHitsPerCommand = 0;
            int maxFrames = math.max(1, _maxRollbackFastForwardFrames);
            int qualityBudget = math.max(1, (int)math.lerp(1f, maxFrames, math.saturate(_globalQualityWeight)));
            int frames = math.clamp(requestedFrames, 1, qualityBudget);
            _rollbackVisualBypassFrames = math.max(_rollbackVisualBypassFrames, frames);

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

                Hecton8.World.DispatcherJobSwap.TryComplete(ref _postSimulationHandle, true);
                _postScheduled = false;
                _collisionScheduled = false;
                _scheduledMaxHitsPerCommand = 0;
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

        public void LateFrameTick()
        {
            if (!_postScheduled)
                return;

            if (!Hecton8.World.DispatcherJobSwap.TryComplete(ref _postSimulationHandle, false))
                return;

            _postScheduled = false;
            if (!EnsureVaultBuffers())
                return;

            NativeArray<HydrodynamicKccVisualOutputDTO> visual = _visualOutputsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccDebugOutputDTO> debugOutputs = _debugOutputsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccFaultFlagDTO> faults = _faultFlagsHandle.Resolve(_dataVault);
            NativeArray<KinematicTelemetryEntry> telemetry = _telemetryRingHandle.Resolve(_dataVault);
            int faultMask = ResolveFaultMask(faults);
            if (faultMask != 0 && faultMask != _dumpedFaultMask)
            {
                DumpTelemetry(telemetry);
                _dumpedFaultMask = faultMask;
            }

            if (!_applyVisualToTransform || !visual.IsCreated || visual.Length == 0 || _cachedTransform == null)
                return;

            HydrodynamicKccVisualOutputDTO output = visual[0];
            Vector3 local = new Vector3(output.LocalPosition.x, output.LocalPosition.y, output.LocalPosition.z);
            _cachedTransform.localPosition = local;
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
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DrainPendingJobsForTeardown();
                _postScheduled = false;
                _collisionScheduled = false;
                _scheduledMaxHitsPerCommand = 0;
                ResetVaultHandles();
                _dataVault = currentService as IDataVault;
                EnsureVaultBuffers();
            }
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _globalQualityWeight = ResolveGlobalQualityWeight();
            if (_dataVault == null || !_tuningHandle.IsCreated)
                return;

            NativeArray<HydrodynamicKccTuningDTO> tuningBuffer = _tuningHandle.Resolve(_dataVault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            HydrodynamicKccTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = _globalQualityWeight;
            tuningBuffer[0] = SanitizeTuning(tuning);
        }

        /// <summary>Parses designer fluid-profile CSV bytes into Vault-backed profile and bucket buffers.</summary>
        public bool TryIngestFluidProfiles(ReadOnlySpan<byte> csvBytes)
        {
            if (csvBytes.Length == 0 || !EnsureVaultBuffers())
                return false;

            NativeArray<HydrodynamicFluidProfileDTO> profiles = _fluidProfilesHandle.Resolve(_dataVault);
            NativeArray<int> buckets = _fluidProfileBucketsHandle.Resolve(_dataVault);
            if (!profiles.IsCreated || profiles.Length == 0 || !buckets.IsCreated || buckets.Length == 0)
                return false;

            int count = HydrodynamicFluidProfileCsvParser.ParseProfiles(csvBytes, profiles, buckets);
            return count > 0 && TryApplyFluidProfile(profiles[0].ProfileHash);
        }

        /// <summary>Applies a previously ingested fluid profile to the Vault-backed KCC tuning record.</summary>
        public bool TryApplyFluidProfile(uint profileHash)
        {
            if (profileHash == 0u || !EnsureVaultBuffers())
                return false;

            NativeArray<HydrodynamicFluidProfileDTO> profiles = _fluidProfilesHandle.Resolve(_dataVault);
            NativeArray<int> buckets = _fluidProfileBucketsHandle.Resolve(_dataVault);
            NativeArray<HydrodynamicKccTuningDTO> tuningBuffer = _tuningHandle.Resolve(_dataVault);
            if (!profiles.IsCreated || profiles.Length == 0 ||
                !buckets.IsCreated || buckets.Length == 0 ||
                !tuningBuffer.IsCreated || tuningBuffer.Length == 0)
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
                    tuningBuffer[0] = SanitizeTuning(tuning);
                    return true;
                }

                profileIndex = profile.NextIndex;
                guard--;
            }

            return false;
        }

        private bool EnsureVaultBuffers()
        {
            if (_dataVault == null)
                return false;

            _entityCapacity = math.max(DefaultCapacity, _entityCapacity);
            if (AreVaultBuffersReady(_entityCapacity))
                return true;

            _statesHandle = _dataVault.GetBufferHandle<KinematicStateDTO>(BufferID.ShinobuHydroKccStates, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _inputsHandle = _dataVault.GetBufferHandle<HydrodynamicKccInputDTO>(BufferID.ShinobuHydroKccInputs, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _proposedVelocitiesHandle = _dataVault.GetBufferHandle<float3>(BufferID.ShinobuHydroKccProposedVelocities, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _collisionCommandsHandle = _dataVault.GetBufferHandle<CapsulecastCommand>(BufferID.ShinobuHydroKccCollisionCommands, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _collisionHitsHandle = _dataVault.GetBufferHandle<RaycastHit>(BufferID.ShinobuHydroKccCollisionHits, _entityCapacity * MaxCollisionHitsPerCommand, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _previousAupHandle = _dataVault.GetBufferHandle<double3>(BufferID.ShinobuHydroKccPreviousAup, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _visualOutputsHandle = _dataVault.GetBufferHandle<HydrodynamicKccVisualOutputDTO>(BufferID.ShinobuHydroKccVisualOutputs, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = _dataVault.GetBufferHandle<KinematicTelemetryEntry>(BufferID.ShinobuHydroKccTelemetryRing, TelemetryCapacity, SystemID.Physics, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = _dataVault.GetBufferHandle<int>(BufferID.ShinobuHydroKccTelemetryCursor, 1, SystemID.Physics, NativeArrayOptions.ClearMemory);
            _tuningHandle = _dataVault.GetBufferHandle<HydrodynamicKccTuningDTO>(BufferID.ShinobuHydroKccTuning, 1, SystemID.Physics, NativeArrayOptions.ClearMemory);
            _rollbackBytesHandle = _dataVault.GetBufferHandle<byte>(BufferID.ShinobuHydroKccRollbackBytes, _entityCapacity * UnsafeUtility.SizeOf<KinematicStateDTO>(), SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _faultFlagsHandle = _dataVault.GetBufferHandle<HydrodynamicKccFaultFlagDTO>(BufferID.ShinobuHydroKccFaultFlags, _entityCapacity, SystemID.Physics, NativeArrayOptions.ClearMemory);
            _wakePacketsHandle = _dataVault.GetBufferHandle<HydrodynamicWakePacketDTO>(BufferID.ShinobuHydroKccWakePackets, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _debugOutputsHandle = _dataVault.GetBufferHandle<HydrodynamicKccDebugOutputDTO>(BufferID.ShinobuHydroKccDebugOutputs, _entityCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _resolvedHitsHandle = _dataVault.GetBufferHandle<HydrodynamicKccCollisionHitDTO>(BufferID.ShinobuHydroKccResolvedHits, _entityCapacity * MaxCollisionHitsPerCommand, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _fluidProfilesHandle = _dataVault.GetBufferHandle<HydrodynamicFluidProfileDTO>(BufferID.ShinobuHydroKccFluidProfiles, DefaultFluidProfileCapacity, SystemID.Physics, NativeArrayOptions.ClearMemory);
            _fluidProfileBucketsHandle = _dataVault.GetBufferHandle<int>(BufferID.ShinobuHydroKccFluidProfileBuckets, DefaultFluidProfileBucketCount, SystemID.Physics, NativeArrayOptions.ClearMemory);

            NativeArray<HydrodynamicKccTuningDTO> tuning = _tuningHandle.Resolve(_dataVault);
            if (tuning.IsCreated && tuning.Length > 0 && tuning[0].MaxSpeed <= 0f)
                tuning[0] = SanitizeTuning(DefaultTuning());

            _resolvedBufferCapacity = _entityCapacity;
            bool ready = AreVaultBuffersReady(_entityCapacity);
            if (!ready)
                _resolvedBufferCapacity = 0;
            return ready;
        }

        private void ResetVaultHandles()
        {
            _statesHandle = default;
            _inputsHandle = default;
            _proposedVelocitiesHandle = default;
            _collisionCommandsHandle = default;
            _collisionHitsHandle = default;
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
            _resolvedBufferCapacity = 0;
            _scheduledMaxHitsPerCommand = 0;
            _externalInputHandle = default;
            _externalInputArmed = false;
        }

        private bool AreVaultBuffersReady(int capacity)
        {
            int entityCapacity = math.max(1, capacity);
            int hitCapacity = entityCapacity * MaxCollisionHitsPerCommand;
            int rollbackByteCapacity = entityCapacity * UnsafeUtility.SizeOf<KinematicStateDTO>();
            return _resolvedBufferCapacity >= entityCapacity &&
                   _statesHandle.IsCreated &&
                   _statesHandle.Length >= entityCapacity &&
                   _inputsHandle.IsCreated &&
                   _inputsHandle.Length >= entityCapacity &&
                   _proposedVelocitiesHandle.IsCreated &&
                   _proposedVelocitiesHandle.Length >= entityCapacity &&
                   _collisionCommandsHandle.IsCreated &&
                   _collisionCommandsHandle.Length >= entityCapacity &&
                   _collisionHitsHandle.IsCreated &&
                   _collisionHitsHandle.Length >= hitCapacity &&
                   _resolvedHitsHandle.IsCreated &&
                   _resolvedHitsHandle.Length >= hitCapacity &&
                   _previousAupHandle.IsCreated &&
                   _previousAupHandle.Length >= entityCapacity &&
                   _visualOutputsHandle.IsCreated &&
                   _visualOutputsHandle.Length >= entityCapacity &&
                   _telemetryRingHandle.IsCreated &&
                   _telemetryRingHandle.Length >= TelemetryCapacity &&
                   _telemetryCursorHandle.IsCreated &&
                   _telemetryCursorHandle.Length >= 1 &&
                   _tuningHandle.IsCreated &&
                   _tuningHandle.Length >= 1 &&
                   _rollbackBytesHandle.IsCreated &&
                   _rollbackBytesHandle.Length >= rollbackByteCapacity &&
                   _faultFlagsHandle.IsCreated &&
                   _faultFlagsHandle.Length >= entityCapacity &&
                   _wakePacketsHandle.IsCreated &&
                   _wakePacketsHandle.Length >= entityCapacity &&
                   _debugOutputsHandle.IsCreated &&
                   _debugOutputsHandle.Length >= entityCapacity &&
                   _fluidProfilesHandle.IsCreated &&
                   _fluidProfilesHandle.Length >= DefaultFluidProfileCapacity &&
                   _fluidProfileBucketsHandle.IsCreated &&
                   _fluidProfileBucketsHandle.Length >= DefaultFluidProfileBucketCount;
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

        private HydrodynamicKccTuningDTO ResolveTuning(NativeArray<HydrodynamicKccTuningDTO> tuningBuffer)
        {
            HydrodynamicKccTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = _globalQualityWeight;
            tuning.WaterSurfaceY = _waterSurfaceY;
            tuning = SanitizeTuning(tuning);
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
                WaterSurfaceY = _waterSurfaceY,
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
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            tuning.WaterSurfaceY = math.isfinite(tuning.WaterSurfaceY) ? tuning.WaterSurfaceY : 0f;
            tuning.MockInputFrequency = math.max(0.01f, math.isfinite(tuning.MockInputFrequency) ? tuning.MockInputFrequency : 0.35f);
            tuning.MockInputAmplitude = math.max(0f, math.isfinite(tuning.MockInputAmplitude) ? tuning.MockInputAmplitude : 1f);
            tuning.VisualSyncSharpness = math.max(0.01f, math.isfinite(tuning.VisualSyncSharpness) ? tuning.VisualSyncSharpness : 18f);
            tuning.WakeThreshold = math.max(0.01f, math.isfinite(tuning.WakeThreshold) ? tuning.WakeThreshold : 0.25f);
            return tuning;
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
            float value = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(value) ? value : 1f);
        }

        private void DumpTelemetry(NativeArray<KinematicTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(root, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, DumpFileName);
            unsafe
            {
                int bytes = telemetry.Length * UnsafeUtility.SizeOf<KinematicTelemetryEntry>();
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
                stream.Write(new ReadOnlySpan<byte>(source, bytes));
            }
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

            GlobalRegistry.RegisterHotSwapListener(this);
            _registeredHotSwap = true;
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.UnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void TryRegisterScalability()
        {
            if (_registeredScalability || !Application.isPlaying)
                return;

            ScalabilityEvents.Register(this);
            _registeredScalability = true;
        }

        private void TryUnregisterScalability()
        {
            if (!_registeredScalability)
                return;

            ScalabilityEvents.Unregister(this);
            _registeredScalability = false;
        }

        private void DrainPendingJobsForTeardown()
        {
            Hecton8.World.DispatcherJobSwap.TryComplete(ref _postSimulationHandle, true);
            Hecton8.World.DispatcherJobSwap.TryComplete(ref _hitExtractHandle, true);
            Hecton8.World.DispatcherJobSwap.TryComplete(ref _collisionHandle, true);
            Hecton8.World.DispatcherJobSwap.TryComplete(ref _commandHandle, true);
            Hecton8.World.DispatcherJobSwap.TryComplete(ref _integrationHandle, true);
            Hecton8.World.DispatcherJobSwap.TryComplete(ref _inputHandle, true);
            Hecton8.World.DispatcherJobSwap.TryComplete(ref _externalInputHandle, true);
        }

        private void OnDrawGizmos()
        {
            CapsuleCollider capsule = _capsule != null ? _capsule : GetComponent<CapsuleCollider>();
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
                Gizmos.DrawLine(predicted, predicted + normal.normalized);
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
    }
}
