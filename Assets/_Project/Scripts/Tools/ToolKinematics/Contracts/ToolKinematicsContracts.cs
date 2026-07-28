using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Tools.ToolKinematics.Contracts
{
    [Flags]
    public enum ToolKinematicsFlags : uint
    {
        Idle = 1u << 0,
        Active = 1u << 1,
        Busy = 1u << 2,
        Overheated = 1u << 3,
        LowPower = 1u << 4,
        TargetLock = 1u << 5,
        Cooling = 1u << 6,
        Fault = 1u << 7,
        RayHit = 1u << 8,
        RecoilActive = 1u << 9,
        LowTierSnap = 1u << 10,
        SdfPenetrating = 1u << 11,
        BeamActive = 1u << 12,
        RaymarchBudgetExceeded = 1u << 13,
        CsvIoFault = 1u << 14,
        LastChargeClutch = 1u << 15,
        PowerDepletedSignalQueued = 1u << 16,
        PowerDepletedSignalSent = 1u << 17
    }

    public enum ToolKinematicsMathLod : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    public static class ToolKinematicsHashes
    {
        public const uint LaserCutter = 0x4C435554u;
        public const uint Scanner = 0x5343414Eu;
        public const uint Welder = 0x57454C44u;
        public const uint RivetGun = 0x52565654u;
        public const uint ProceduralRock = 0x524F434Bu;
        public const uint ProceduralMetal = 0x4D45544Cu;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToolStateDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Forward;
        [FieldOffset(36)] public float HeatLevel;
        [FieldOffset(40)] public uint ToolTypeHash;
        [FieldOffset(44)] public float EnergyRemaining;
        [FieldOffset(48)] public float MaxEnergyCapacity;
        [FieldOffset(52)] public uint StateFlags;
        [FieldOffset(56)] public float LastOutputPower01;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolHitResultDTO
    {
        [FieldOffset(0)] public float3 HitPoint;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public uint MaterialHash;
        [FieldOffset(28)] public float Distance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ToolScreenExportDTO
    {
        [FieldOffset(0)] public float HitDistance;
        [FieldOffset(4)] public uint MaterialHash;
        [FieldOffset(8)] public float HeatLevel;
        [FieldOffset(12)] public uint StateFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct ToolKinematicsTuningDTO
    {
        [FieldOffset(0)] public float LaserRange;
        [FieldOffset(4)] public float HeatRampRate;
        [FieldOffset(8)] public float CoolingRate;
        [FieldOffset(12)] public float MaxHeat;
        [FieldOffset(16)] public float EnergyDrainRate;
        [FieldOffset(20)] public float RecoilStrength;
        [FieldOffset(24)] public float SpringDamping;
        [FieldOffset(28)] public float CollisionSpring;
        [FieldOffset(32)] public float BeamRadius;
        [FieldOffset(36)] public float SystemHealthIndex;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint _pad0;
    }

    /// <summary>
    /// Burst-read frame input, vault-resident (<c>BufferID.ToolKinematicsFrameInputs</c>).
    /// <c>ControllerRotation</c> is a 16-byte <c>float4</c> lane and therefore sits at a 16-byte-aligned
    /// offset, not at 36 where it used to: data.md:20/32/34 require natural alignment for a NativeArray
    /// record a Burst job loads whole, and a 4-aligned vector lane is exactly the "misaligned read on
    /// ARM64" that rule names. Total size stays 96 - a multiple of 16 - so element k of the vault buffer
    /// keeps that alignment, not just element 0 (arena base is 64-aligned,
    /// Core/Memory/GlobalDataVault.cs:448 <c>VaultBlockAlignment</c>).
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct ToolKinematicsFrameInputDTO
    {
        [FieldOffset(0)] public double3 CameraAup;
        [FieldOffset(24)] public uint TriggerFlags;
        [FieldOffset(28)] public uint FrameIndex;
        [FieldOffset(32)] public quaternion ControllerRotation;
        [FieldOffset(48)] public float3 ControllerLocalPosition;
        [FieldOffset(60)] public float DeltaTime;
        [FieldOffset(64)] public float3 ShoulderLocalPosition;
        [FieldOffset(76)] public float SystemHealthIndex;
        [FieldOffset(80)] public float3 PoleLocalDirection;
        [FieldOffset(92)] public uint _pad0;
    }

    /// <summary>
    /// Burst-written IK result, vault-resident (<c>BufferID.ToolKinematicsIkOutputs</c>). Same rule as
    /// <see cref="ToolKinematicsFrameInputDTO"/>: the 16-byte <c>UpperRotation</c> lane leads the record
    /// so it is 16-byte aligned at every element of a 64-byte stride.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToolIkOutputDTO
    {
        [FieldOffset(0)] public quaternion UpperRotation;
        [FieldOffset(16)] public float3 Shoulder;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float3 Elbow;
        [FieldOffset(44)] public float ComputeMicrosecondsEstimate;
        [FieldOffset(48)] public float3 Wrist;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToolRecoilStateDTO
    {
        [FieldOffset(0)] public float3 PositionOffset;
        [FieldOffset(12)] public float3 AngularOffsetAxis;
        [FieldOffset(24)] public float KickVelocity;
        [FieldOffset(28)] public float SpringVelocity;
        [FieldOffset(32)] public float RecoilTime;
        [FieldOffset(36)] public float Recoil01;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float3 PivotLocal;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolBeamVertexDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 Normal;
        [FieldOffset(28)] public float U;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct ToolPoseOutputDTO
    {
        [FieldOffset(0)] public float4 MatrixColumn0;
        [FieldOffset(16)] public float4 MatrixColumn1;
        [FieldOffset(32)] public float4 MatrixColumn2;
        [FieldOffset(48)] public float4 MatrixColumn3;
        [FieldOffset(64)] public float3 RecoilOffset;
        [FieldOffset(76)] public float RecoilRadians;
        [FieldOffset(80)] public uint Flags;
        [FieldOffset(84)] public uint _pad0;
        [FieldOffset(88)] public uint _pad1;
        [FieldOffset(92)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToolKinematicsTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint ToolHash;
        [FieldOffset(8)] public float ToolHeatLevel;
        [FieldOffset(12)] public float EnergyRemaining;
        [FieldOffset(16)] public float HitDistance;
        [FieldOffset(20)] public int RaymarchStepCount;
        [FieldOffset(24)] public float IkComputeTimeMicroseconds;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float3 ToolLocalPosition;
        [FieldOffset(44)] public float3 HitPoint;
        [FieldOffset(56)] public uint MaterialHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct ToolTriggerPullSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x54323254u; // T22T

        [FieldOffset(0)] public uint ToolSlot;
        [FieldOffset(4)] public uint ToolHash;
        [FieldOffset(8)] public float Trigger01;
        [FieldOffset(12)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct ToolCarveRequestSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 2;
        public const uint LaneHash = 0x54323243u; // T22C

        [FieldOffset(0)] public float3 HitPoint;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public uint ToolHash;
        [FieldOffset(28)] public uint MaterialHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public float Power01;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct ToolHeatSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x54323248u; // T22H

        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Heat01;
        [FieldOffset(12)] public float Energy01;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct ToolPowerDepletedSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x54323250u; // T22P

        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Energy01;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public ulong _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct VfxSparkRequestSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 2;
        public const uint LaneHash = 0x54323253u; // T22S

        [FieldOffset(0)] public float3 HitPoint;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public uint MaterialHash;
        [FieldOffset(28)] public uint ToolHash;
        [FieldOffset(32)] public float Intensity01;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public ulong _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct ToolProceduralSdfSample
    {
        [FieldOffset(0)] public float Distance;
        [FieldOffset(4)] public uint MaterialHash;
    }

    public static class ToolKinematicsMath
    {
        public const uint TriggerPressed = 1u << 0;
        public const uint TriggerLaserMode = 1u << 1;
        public const uint TriggerScannerMode = 1u << 2;
        public const uint TriggerWelderMode = 1u << 3;
        public const uint TriggerLegacySnap = 1u << 4;
        public const uint TriggerLowTierSnap = TriggerLegacySnap;
        public const int BlackBoxCapacity = 300;
        public const int MaxRaymarchStepsLow = 24;
        public const int MaxRaymarchStepsMiddle = 40;
        public const int MaxRaymarchStepsHigh = 56;
        public const int MaxRaymarchStepsUltra = 72;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ToolKinematicsTuningDTO DefaultTuning()
        {
            return new ToolKinematicsTuningDTO
            {
                LaserRange = 18f,
                HeatRampRate = 0.62f,
                CoolingRate = 0.38f,
                MaxHeat = 1f,
                EnergyDrainRate = 0.075f,
                RecoilStrength = 0.18f,
                SpringDamping = 12f,
                CollisionSpring = 0.42f,
                BeamRadius = 0.018f,
                SystemHealthIndex = 0f,
                Flags = 0u,
                _pad0 = 0u
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveVisualQuality01FromStress(float systemHealthIndex)
        {
            return math.saturate(1f - Clamp01Finite(systemHealthIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveRaymarchSteps()
        {
            return MaxRaymarchStepsUltra;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveBeamRingSides(float systemHealthIndex)
        {
            float quality01 = ResolveVisualQuality01FromStress(systemHealthIndex);
            float curved = quality01 * quality01 * (3f - (2f * quality01));
            return math.clamp((int)math.round(math.lerp(4f, 8f, curved)), 4, 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp01Finite(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ClampPositiveFinite(float value, float fallback)
        {
            return math.isfinite(value) && value > 0.0001f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveLastChargePower01(float energyRemaining, float maxEnergyCapacity, out uint clutchFlag)
        {
            float capacity = ClampPositiveFinite(maxEnergyCapacity, 1f);
            float energy01 = Clamp01Finite(energyRemaining * math.rcp(capacity));
            bool clutch = (energy01 > 0.0001f) & (energy01 < 0.01f);
            float multiplier = math.select(1f, 2.5f, clutch);
            clutchFlag = math.select(0u, (uint)ToolKinematicsFlags.LastChargeClutch, clutch);
            return Clamp01Finite(energy01 * multiplier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApproxSinCosBhaskara(float radians, out float sine, out float cosine)
        {
            sine = ApproxSinBhaskara(radians);
            cosine = ApproxSinBhaskara(radians + (0.5f * math.PI));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproxSinBhaskara(float radians)
        {
            float angle = math.select(0f, radians, math.isfinite(radians));
            float cycle = angle * 0.15915494309189535f;
            float wrapped = cycle - math.floor(cycle);
            float x = wrapped * (2f * math.PI);
            float mirrored = math.select(x, (2f * math.PI) - x, x > math.PI);
            float sign = math.select(1f, -1f, x > math.PI);
            float shape = mirrored * (math.PI - mirrored);
            float numerator = 16f * shape;
            float denominator = math.max(0.0001f, (5f * math.PI * math.PI) - (4f * shape));
            float sine = sign * numerator * math.rcp(denominator);
            return math.clamp(math.select(0f, sine, math.isfinite(sine)), -1f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f ? value * math.rsqrt(lengthSq) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion SafeNormalizeQuaternion(quaternion value, quaternion fallback)
        {
            float lengthSq = math.lengthsq(value.value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? new quaternion(value.value * math.rsqrt(lengthSq))
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion FastSmallAngleRotation(float3 axis, float radians)
        {
            float3 safeAxis = SafeNormalize(axis, new float3(1f, 0f, 0f));
            float half = math.clamp(math.select(0f, radians, math.isfinite(radians)), -0.5f, 0.5f) * 0.5f;
            float halfSq = half * half;
            return SafeNormalizeQuaternion(new quaternion(new float4(safeAxis * half, 1f - (0.5f * halfSq))), quaternion.identity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToLocalFloat3(double3 absolute, double3 cameraAup, float3 fallback)
        {
            double3 local = absolute - cameraAup;
            if (!math.all(math.isfinite(local)))
                return fallback;

            float3 result = new float3((float)local.x, (float)local.y, (float)local.z);
            return math.all(math.isfinite(result)) ? result : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint a, uint b)
        {
            uint h = a ^ 0x9E3779B9u;
            h ^= b + 0x85EBCA6Bu + (h << 6) + (h >> 2);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h == 0u ? 1u : h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ToolProceduralSdfSample SampleToolProceduralSdf(float3 localPosition)
        {
            float3 sphereCenter = new float3(0f, 1.15f, 5.5f);
            float sphereDistance = math.length(localPosition - sphereCenter) - 1.2f;
            float floorDistance = localPosition.y + 1.2f;
            float3 boxQ = math.abs(localPosition - new float3(1.45f, 0.25f, 4.25f)) - new float3(0.55f, 0.9f, 0.55f);
            float boxDistance = math.length(math.max(boxQ, 0f)) + math.min(math.max(boxQ.x, math.max(boxQ.y, boxQ.z)), 0f);
            float selected = math.min(sphereDistance, math.min(floorDistance, boxDistance));
            uint material = selected == boxDistance ? ToolKinematicsHashes.ProceduralMetal : ToolKinematicsHashes.ProceduralRock;
            return new ToolProceduralSdfSample
            {
                Distance = math.isfinite(selected) ? selected : 1000f,
                MaterialHash = material
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EstimateToolProceduralSdfNormal(float3 localPosition)
        {
            const float h = 0.015625f;
            float dx = SampleToolProceduralSdf(localPosition + new float3(h, 0f, 0f)).Distance -
                       SampleToolProceduralSdf(localPosition - new float3(h, 0f, 0f)).Distance;
            float dy = SampleToolProceduralSdf(localPosition + new float3(0f, h, 0f)).Distance -
                       SampleToolProceduralSdf(localPosition - new float3(0f, h, 0f)).Distance;
            float dz = SampleToolProceduralSdf(localPosition + new float3(0f, 0f, h)).Distance -
                       SampleToolProceduralSdf(localPosition - new float3(0f, 0f, h)).Distance;
            return SafeNormalize(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float NoiseSigned(uint seed)
        {
            uint mixed = Mix(seed, seed ^ 0xA341316Cu);
            return ((mixed & 0xFFFFu) * (1f / 32767.5f)) - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct TwoBoneIKJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ToolStateDTO> ToolStates;
        [ReadOnly, NoAlias] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        [WriteOnly, NoAlias] public NativeArray<ToolIkOutputDTO> IkOutputs;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)FrameInputs.Length || (uint)index >= (uint)IkOutputs.Length)
                return;

            ToolKinematicsFrameInputDTO input = FrameInputs[index];
            quaternion controllerRotation = ToolKinematicsMath.SafeNormalizeQuaternion(input.ControllerRotation, quaternion.identity);
            float3 shoulder = input.ShoulderLocalPosition;
            float3 target = input.ControllerLocalPosition;
            float3 pole = ToolKinematicsMath.SafeNormalize(input.PoleLocalDirection, new float3(0f, 1f, 0f));

            ToolIkOutputDTO output = default;
            output.Shoulder = shoulder;
            output.Wrist = target;
            output.Flags = 0u;
            output.ComputeMicrosecondsEstimate = 8.0f;

            float upper = 0.34f;
            float lower = 0.34f;
            if ((uint)index < (uint)ToolStates.Length)
            {
                ToolStateDTO state = ToolStates[index];
                upper += (state.ToolTypeHash == ToolKinematicsHashes.RivetGun ? 0.02f : 0f);
            }

            float3 shoulderToTarget = target - shoulder;
            float targetDistanceSq = math.lengthsq(shoulderToTarget);
            float3 targetDir = ToolKinematicsMath.SafeNormalize(shoulderToTarget, new float3(0f, 0f, 1f));
            float targetDistance = targetDistanceSq > 0.000001f ? targetDistanceSq * math.rsqrt(targetDistanceSq) : 0.001f;
            float maxReach = math.max(0.05f, upper + lower - 0.02f);
            float minReach = math.max(0.02f, math.abs(upper - lower) + 0.02f);
            float c = math.clamp(targetDistance, minReach, maxReach);
            target = shoulder + targetDir * c;

            float cosUpper = math.clamp(((upper * upper) + (c * c) - (lower * lower)) * math.rcp(math.max(0.0001f, 2f * upper * c)), -1f, 1f);
            float sinUpper = math.sqrt(math.max(0f, 1f - cosUpper * cosUpper));
            float3 projectedPole = pole - targetDir * math.dot(pole, targetDir);
            float3 bendDir = ToolKinematicsMath.SafeNormalize(projectedPole, new float3(0f, 1f, 0f));
            float3 upperDir = ToolKinematicsMath.SafeNormalize((targetDir * cosUpper) + (bendDir * sinUpper), targetDir);
            float3 elbow = shoulder + upperDir * upper;

            output.Elbow = elbow;
            output.Wrist = target;
            output.UpperRotation = quaternion.LookRotationSafe(upperDir, bendDir);
            IkOutputs[index] = output;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SdfRaymarchJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ToolStateDTO> ToolStates;
        [NoAlias] public NativeArray<ToolRecoilStateDTO> RecoilStates;
        [ReadOnly, NoAlias] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        [ReadOnly, NoAlias] public NativeArray<ToolKinematicsTuningDTO> Tuning;
        [WriteOnly, NoAlias] public NativeArray<ToolHitResultDTO> HitResults;
        [NoAlias] public NativeArray<ToolScreenExportDTO> ScreenExports;
        [WriteOnly, NoAlias] public NativeArray<ToolPoseOutputDTO> PoseOutputs;
        [WriteOnly, NoAlias] public NativeArray<ToolHeatSignal> HeatSignals;
        [WriteOnly, NoAlias] public NativeArray<VfxSparkRequestSignal> SparkRequests;
        [WriteOnly, NoAlias] public NativeArray<ToolKinematicsTelemetryEntry> TelemetryRing;
        public int TelemetryCursor;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ToolStates.Length || (uint)index >= (uint)FrameInputs.Length)
                return;

            ToolKinematicsFrameInputDTO input = FrameInputs[index];
            ToolKinematicsTuningDTO tuning = Tuning.Length > 0 ? Tuning[0] : ToolKinematicsMath.DefaultTuning();
            ToolStateDTO state = ToolStates[index];
            ToolRecoilStateDTO recoil = (uint)index < (uint)RecoilStates.Length ? RecoilStates[index] : default;
            quaternion controllerRotation = ToolKinematicsMath.SafeNormalizeQuaternion(input.ControllerRotation, quaternion.identity);
            float dt = math.clamp(ToolKinematicsMath.ClampPositiveFinite(input.DeltaTime, 0.0166667f), 0f, 0.05f);
            float maxEnergyCapacity = ToolKinematicsMath.ClampPositiveFinite(state.MaxEnergyCapacity, 1f);
            state.MaxEnergyCapacity = maxEnergyCapacity;
            state.EnergyRemaining = math.clamp(math.select(0f, state.EnergyRemaining, math.isfinite(state.EnergyRemaining)), 0f, maxEnergyCapacity);
            uint persistentFlags = state.StateFlags & (uint)ToolKinematicsFlags.PowerDepletedSignalSent;
            uint flags = (uint)index < (uint)ScreenExports.Length
                ? ScreenExports[index].StateFlags
                : (uint)ToolKinematicsFlags.Idle;
            flags |= persistentFlags;
            if ((tuning.Flags & (uint)ToolKinematicsFlags.CsvIoFault) != 0u)
                flags |= (uint)ToolKinematicsFlags.CsvIoFault;
            else
                flags &= ~(uint)ToolKinematicsFlags.CsvIoFault;

            bool trigger = (input.TriggerFlags & ToolKinematicsMath.TriggerPressed) != 0;
            flags &= ~(
                (uint)ToolKinematicsFlags.LowTierSnap |
                (uint)ToolKinematicsFlags.LastChargeClutch |
                (uint)ToolKinematicsFlags.PowerDepletedSignalQueued);

            float maxHeat = ToolKinematicsMath.ClampPositiveFinite(tuning.MaxHeat, 1f);
            bool overheated = (flags & (uint)ToolKinematicsFlags.Overheated) != 0;
            float energy01 = ToolKinematicsMath.Clamp01Finite(state.EnergyRemaining * math.rcp(maxEnergyCapacity));
            float preDrainEnergyRemaining = state.EnergyRemaining;
            float preDrainEnergy01 = energy01;
            bool hasEnergy = energy01 > 0.0001f;
            bool active = trigger && !overheated && hasEnergy;
            if (active)
            {
                state.HeatLevel = math.min(maxHeat, state.HeatLevel + math.max(0f, tuning.HeatRampRate) * dt);
                state.EnergyRemaining = math.max(0f, state.EnergyRemaining - math.max(0f, tuning.EnergyDrainRate) * dt);
                flags |= (uint)(ToolKinematicsFlags.Active | ToolKinematicsFlags.BeamActive);
                flags &= ~(uint)(ToolKinematicsFlags.Idle | ToolKinematicsFlags.Cooling);
            }
            else
            {
                state.HeatLevel = math.max(0f, state.HeatLevel - math.max(0f, tuning.CoolingRate) * dt);
                flags |= (uint)ToolKinematicsFlags.Idle;
                flags &= ~(uint)(ToolKinematicsFlags.Active | ToolKinematicsFlags.BeamActive);
                if (state.HeatLevel > 0.001f)
                    flags |= (uint)ToolKinematicsFlags.Cooling;
                else
                    flags &= ~(uint)ToolKinematicsFlags.Cooling;
            }

            energy01 = ToolKinematicsMath.Clamp01Finite(state.EnergyRemaining * math.rcp(maxEnergyCapacity));
            bool lastChargeFrame = active & (preDrainEnergy01 > 0.0001f) & (preDrainEnergy01 < 0.01f);
            float powerEnergyRemaining = math.select(state.EnergyRemaining, preDrainEnergyRemaining, lastChargeFrame);
            float cuttingPower01 = ToolKinematicsMath.ResolveLastChargePower01(powerEnergyRemaining, maxEnergyCapacity, out uint clutchFlag);
            flags |= math.select(0u, clutchFlag, active);

            if (state.HeatLevel >= maxHeat - 0.0001f)
            {
                flags |= (uint)ToolKinematicsFlags.Overheated;
                flags &= ~(uint)(ToolKinematicsFlags.Active | ToolKinematicsFlags.BeamActive);
                active = false;
            }
            else if (state.HeatLevel <= maxHeat * 0.15f)
            {
                flags &= ~(uint)ToolKinematicsFlags.Overheated;
            }

            if (energy01 <= 0.0001f)
            {
                flags |= (uint)ToolKinematicsFlags.LowPower;
                if (!lastChargeFrame)
                {
                    flags &= ~(uint)(ToolKinematicsFlags.Active | ToolKinematicsFlags.BeamActive);
                    active = false;
                }
            }
            else
                flags &= ~(uint)ToolKinematicsFlags.LowPower;

            if (!active)
                flags &= ~(uint)ToolKinematicsFlags.LastChargeClutch;

            float3 forward = math.rotate(controllerRotation, new float3(0f, 0f, 1f));
            forward = ToolKinematicsMath.SafeNormalize(forward, new float3(0f, 0f, 1f));
            state.Forward = forward;
            float3 toolLocal = ToolKinematicsMath.ToLocalFloat3(state.AUP, input.CameraAup, input.ControllerLocalPosition);
            float3 tipLocal = toolLocal + forward * 0.28f;
            ToolProceduralSdfSample tipSample = ToolKinematicsMath.SampleToolProceduralSdf(tipLocal);

            if (tipSample.Distance < 0f)
            {
                flags |= (uint)ToolKinematicsFlags.SdfPenetrating;
                float depth = math.min(0.35f, -tipSample.Distance);
                recoil.SpringVelocity += depth * math.max(0f, tuning.CollisionSpring);
                recoil.AngularOffsetAxis = math.cross(forward, ToolKinematicsMath.EstimateToolProceduralSdfNormal(tipLocal));
            }
            else
            {
                flags &= ~(uint)ToolKinematicsFlags.SdfPenetrating;
            }

            if (active)
                recoil.KickVelocity -= math.max(0f, tuning.RecoilStrength) * dt;

            float damping = math.max(0f, tuning.SpringDamping);
            float springAccel = (-recoil.Recoil01 * damping * damping) - (2f * damping * recoil.SpringVelocity);
            recoil.SpringVelocity += springAccel * dt;
            recoil.Recoil01 += (recoil.SpringVelocity + recoil.KickVelocity) * dt;
            recoil.KickVelocity *= math.saturate(1f - damping * dt);
            recoil.Recoil01 = math.clamp(recoil.Recoil01, -0.35f, 0.35f);
            recoil.PositionOffset = -forward * math.abs(recoil.Recoil01);
            recoil.RecoilTime = active ? recoil.RecoilTime + dt : math.max(0f, recoil.RecoilTime - dt);
            recoil.Flags = math.abs(recoil.Recoil01) > 0.0001f ? (uint)ToolKinematicsFlags.RecoilActive : 0u;

            ToolHitResultDTO hit = default;
            int stepCount = 0;
            float traveled = 0f;
            float maxRange = math.max(0.1f, tuning.LaserRange);
            bool hasHit = false;
            int maxSteps = ToolKinematicsMath.ResolveRaymarchSteps();
            if (active && hasEnergy)
            {
                float3 marchPos = tipLocal;
                for (int step = 0; step < maxSteps && traveled <= maxRange; step++)
                {
                    ToolProceduralSdfSample sample = ToolKinematicsMath.SampleToolProceduralSdf(marchPos);
                    float distance = math.max(0.015625f, math.abs(sample.Distance));
                    stepCount = step + 1;
                    if (sample.Distance <= 0.01f)
                    {
                        hit.HitPoint = marchPos;
                        hit.Normal = ToolKinematicsMath.EstimateToolProceduralSdfNormal(marchPos);
                        hit.MaterialHash = sample.MaterialHash;
                        hit.Distance = traveled;
                        hasHit = true;
                        break;
                    }

                    traveled += distance;
                    marchPos = tipLocal + forward * traveled;
                }

                if (!hasHit && stepCount >= maxSteps && traveled < maxRange * 0.25f)
                    flags |= (uint)(ToolKinematicsFlags.RaymarchBudgetExceeded | ToolKinematicsFlags.Fault);
                else
                    flags &= ~(uint)ToolKinematicsFlags.RaymarchBudgetExceeded;
            }

            if (hasHit)
                flags |= (uint)ToolKinematicsFlags.RayHit;
            else
            {
                flags &= ~(uint)ToolKinematicsFlags.RayHit;
                hit.HitPoint = tipLocal + forward * math.min(maxRange, traveled);
                hit.Normal = -forward;
                hit.MaterialHash = 0u;
                hit.Distance = math.min(maxRange, traveled);
            }

            if ((uint)index < (uint)RecoilStates.Length)
                RecoilStates[index] = recoil;
            if ((uint)index < (uint)HitResults.Length)
                HitResults[index] = hit;

            if ((uint)index < (uint)PoseOutputs.Length)
            {
                float3 recoilAxis = ToolKinematicsMath.SafeNormalize(recoil.AngularOffsetAxis, new float3(1f, 0f, 0f));
                float recoilRadians = math.clamp(recoil.Recoil01, -0.35f, 0.35f);
                quaternion recoilRotation = ToolKinematicsMath.FastSmallAngleRotation(recoilAxis, recoilRadians);
                quaternion finalRotation = ToolKinematicsMath.SafeNormalizeQuaternion(math.mul(controllerRotation, recoilRotation), controllerRotation);
                float3 basePivot = math.rotate(controllerRotation, recoil.PivotLocal);
                float3 rotatedPivot = math.rotate(finalRotation, recoil.PivotLocal);
                float3 pivotCompensation = basePivot - rotatedPivot;
                float3 finalPosition = toolLocal + recoil.PositionOffset + pivotCompensation;
                float4x4 matrix = float4x4.TRS(finalPosition, finalRotation, new float3(1f, 1f, 1f));
                if (!math.all(math.isfinite(matrix.c0)) ||
                    !math.all(math.isfinite(matrix.c1)) ||
                    !math.all(math.isfinite(matrix.c2)) ||
                    !math.all(math.isfinite(matrix.c3)))
                {
                    flags |= (uint)ToolKinematicsFlags.Fault;
                    matrix = float4x4.identity;
                    finalPosition = toolLocal;
                    recoilRadians = 0f;
                }

                PoseOutputs[index] = new ToolPoseOutputDTO
                {
                    MatrixColumn0 = matrix.c0,
                    MatrixColumn1 = matrix.c1,
                    MatrixColumn2 = matrix.c2,
                    MatrixColumn3 = matrix.c3,
                    RecoilOffset = finalPosition - toolLocal,
                    RecoilRadians = recoilRadians,
                    Flags = flags,
                    _pad0 = 0u,
                    _pad1 = 0u,
                    _pad2 = 0u
                };
            }

            bool depletedNow = energy01 <= 0.0001f;
            bool depletionAlreadySent = (persistentFlags & (uint)ToolKinematicsFlags.PowerDepletedSignalSent) != 0u;
            bool queueDepletionSignal = depletedNow && !depletionAlreadySent;
            persistentFlags = math.select(0u, persistentFlags, depletedNow);
            persistentFlags = math.select(
                persistentFlags,
                persistentFlags | (uint)ToolKinematicsFlags.PowerDepletedSignalSent,
                queueDepletionSignal);
            flags |= persistentFlags;
            flags |= math.select(0u, (uint)ToolKinematicsFlags.PowerDepletedSignalQueued, queueDepletionSignal);
            state.StateFlags = persistentFlags;
            state.LastOutputPower01 = math.select(0f, cuttingPower01, active);
            state._pad0 = 0u;
            ToolStates[index] = state;

            if ((uint)index < (uint)ScreenExports.Length)
            {
                ScreenExports[index] = new ToolScreenExportDTO
                {
                    HitDistance = hit.Distance,
                    MaterialHash = hit.MaterialHash,
                    HeatLevel = ToolKinematicsMath.Clamp01Finite(state.HeatLevel * math.rcp(maxHeat)),
                    StateFlags = flags
                };
            }

            if ((uint)index < (uint)HeatSignals.Length)
            {
                HeatSignals[index] = new ToolHeatSignal
                {
                    ToolHash = state.ToolTypeHash,
                    Frame = input.FrameIndex,
                    Heat01 = ToolKinematicsMath.Clamp01Finite(state.HeatLevel * math.rcp(maxHeat)),
                    Energy01 = energy01,
                    Flags = flags,
                    _pad0 = 0u
                };
            }

            if ((uint)index < (uint)SparkRequests.Length)
            {
                SparkRequests[index] = hasHit
                    ? new VfxSparkRequestSignal
                    {
                        HitPoint = hit.HitPoint,
                        Normal = hit.Normal,
                        MaterialHash = hit.MaterialHash,
                        ToolHash = state.ToolTypeHash,
                        Intensity01 = active ? math.max(ToolKinematicsMath.Clamp01Finite(state.HeatLevel * math.rcp(maxHeat)), cuttingPower01) : 0f,
                        Frame = input.FrameIndex
                    }
                    : default;
            }

            int telemetryIndex = (index * ToolKinematicsMath.BlackBoxCapacity) + TelemetryCursor;
            if ((uint)telemetryIndex < (uint)TelemetryRing.Length)
            {
                TelemetryRing[telemetryIndex] = new ToolKinematicsTelemetryEntry
                {
                    FrameIndex = input.FrameIndex,
                    ToolHash = state.ToolTypeHash,
                    ToolHeatLevel = ToolKinematicsMath.Clamp01Finite(state.HeatLevel * math.rcp(maxHeat)),
                    EnergyRemaining = energy01,
                    HitDistance = hit.Distance,
                    RaymarchStepCount = stepCount,
                    IkComputeTimeMicroseconds = 8f,
                    Flags = flags,
                    ToolLocalPosition = toolLocal,
                    HitPoint = hit.HitPoint,
                    MaterialHash = hit.MaterialHash,
                    _pad0 = 0u
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ToolCarveRequestJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ToolHitResultDTO> HitResults;
        [ReadOnly, NoAlias] public NativeArray<ToolStateDTO> ToolStates;
        [ReadOnly, NoAlias] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        [ReadOnly, NoAlias] public NativeArray<ToolScreenExportDTO> ScreenExports;
        [WriteOnly, NoAlias] public NativeArray<ToolCarveRequestSignal> CarveRequests;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)HitResults.Length ||
                (uint)index >= (uint)ToolStates.Length ||
                (uint)index >= (uint)FrameInputs.Length ||
                (uint)index >= (uint)ScreenExports.Length ||
                (uint)index >= (uint)CarveRequests.Length)
            {
                return;
            }

            ToolStateDTO state = ToolStates[index];
            ToolHitResultDTO hit = HitResults[index];
            ToolKinematicsFrameInputDTO input = FrameInputs[index];
            uint flags = ScreenExports[index].StateFlags;
            if ((flags & (uint)ToolKinematicsFlags.RayHit) == 0 ||
                (flags & (uint)ToolKinematicsFlags.Active) == 0 ||
                hit.MaterialHash == 0u)
            {
                CarveRequests[index] = default;
                return;
            }

            uint roll = ToolKinematicsMath.Mix(input.FrameIndex, (uint)index + state.ToolTypeHash);
            bool fire = (roll & 3u) != 0u;
            CarveRequests[index] = fire
                ? new ToolCarveRequestSignal
                {
                    HitPoint = hit.HitPoint,
                    Normal = hit.Normal,
                    ToolHash = state.ToolTypeHash,
                    MaterialHash = hit.MaterialHash,
                    Frame = input.FrameIndex,
                    Power01 = ToolKinematicsMath.Clamp01Finite(state.LastOutputPower01),
                    Flags = flags,
                    _pad0 = 0u
                }
                : default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ProceduralBeamMeshJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ToolHitResultDTO> HitResults;
        [ReadOnly, NoAlias] public NativeArray<ToolStateDTO> ToolStates;
        [ReadOnly, NoAlias] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        [ReadOnly, NoAlias] public NativeArray<ToolScreenExportDTO> ScreenExports;
        [ReadOnly, NoAlias] public NativeArray<ToolKinematicsTuningDTO> Tuning;
        [WriteOnly, NoAlias] public NativeArray<ToolBeamVertexDTO> BeamVertices;
        [WriteOnly, NoAlias] public NativeArray<int> BeamVertexCounts;
        public int VerticesPerTool;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)HitResults.Length ||
                (uint)index >= (uint)ToolStates.Length ||
                (uint)index >= (uint)FrameInputs.Length ||
                (uint)index >= (uint)ScreenExports.Length ||
                (uint)index >= (uint)BeamVertexCounts.Length ||
                VerticesPerTool <= 0)
            {
                return;
            }

            int start = index * VerticesPerTool;
            if (start < 0 || start >= BeamVertices.Length)
                return;

            ToolStateDTO state = ToolStates[index];
            uint flags = ScreenExports[index].StateFlags;
            if ((flags & (uint)ToolKinematicsFlags.BeamActive) == 0)
            {
                BeamVertexCounts[index] = 0;
                return;
            }

            ToolKinematicsFrameInputDTO input = FrameInputs[index];
            ToolKinematicsTuningDTO tuning = Tuning.Length > 0 ? Tuning[0] : ToolKinematicsMath.DefaultTuning();
            float3 startPos = ToolKinematicsMath.ToLocalFloat3(state.AUP, input.CameraAup, input.ControllerLocalPosition) + state.Forward * 0.28f;
            float3 endPos = HitResults[index].HitPoint;
            float3 axis = ToolKinematicsMath.SafeNormalize(endPos - startPos, state.Forward);
            float3 tangent = ToolKinematicsMath.SafeNormalize(math.cross(axis, new float3(0f, 1f, 0f)), new float3(1f, 0f, 0f));
            float3 bitangent = ToolKinematicsMath.SafeNormalize(math.cross(axis, tangent), new float3(0f, 1f, 0f));
            int ringSides = ToolKinematicsMath.ResolveBeamRingSides(input.SystemHealthIndex);

            int rings = math.max(2, VerticesPerTool / ringSides);
            int written = 0;
            float radius = math.max(0.002f, tuning.BeamRadius);
            for (int r = 0; r < rings && written < VerticesPerTool; r++)
            {
                float u = rings <= 1 ? 0f : (float)r * math.rcp(rings - 1);
                float3 center = math.lerp(startPos, endPos, u);
                float noise = ToolKinematicsMath.NoiseSigned(ToolKinematicsMath.Mix((uint)index, (uint)r)) * 0.0075f;
                for (int side = 0; side < ringSides && written < VerticesPerTool; side++)
                {
                    float a = (side * 6.2831855f) * math.rcp(ringSides);
                    ToolKinematicsMath.ApproxSinCosBhaskara(a, out float s, out float c);
                    float3 normal = tangent * c + bitangent * s;
                    BeamVertices[start + written] = new ToolBeamVertexDTO
                    {
                        Position = center + normal * (radius + noise),
                        Radius = radius,
                        Normal = normal,
                        U = u
                    };
                    written++;
                }
            }

            BeamVertexCounts[index] = written;
        }
    }
}
