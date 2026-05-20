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
        CsvIoFault = 1u << 14
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
        public const uint MockRock = 0x524F434Bu;
        public const uint MockMetal = 0x4D45544Cu;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct ToolStateDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Forward;
        [FieldOffset(36)] public float HeatLevel;
        [FieldOffset(40)] public uint ToolTypeHash;
        [FieldOffset(44)] public float EnergyRemaining;
        [FieldOffset(48)] public uint _pad0;
        [FieldOffset(52)] public uint _pad1;
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

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct ToolKinematicsFrameInputDTO
    {
        [FieldOffset(0)] public double3 CameraAup;
        [FieldOffset(24)] public float3 ControllerLocalPosition;
        [FieldOffset(36)] public quaternion ControllerRotation;
        [FieldOffset(52)] public float3 ShoulderLocalPosition;
        [FieldOffset(64)] public float3 PoleLocalDirection;
        [FieldOffset(76)] public float DeltaTime;
        [FieldOffset(80)] public float SystemHealthIndex;
        [FieldOffset(84)] public uint TriggerFlags;
        [FieldOffset(88)] public uint FrameIndex;
        [FieldOffset(92)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ToolIkOutputDTO
    {
        [FieldOffset(0)] public float3 Shoulder;
        [FieldOffset(12)] public float3 Elbow;
        [FieldOffset(24)] public float3 Wrist;
        [FieldOffset(36)] public quaternion UpperRotation;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float ComputeMicrosecondsEstimate;
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
    public partial struct MockTriggerPullSignal : ISignal
    {
        [FieldOffset(0)] public uint ToolSlot;
        [FieldOffset(4)] public uint ToolHash;
        [FieldOffset(8)] public float Trigger01;
        [FieldOffset(12)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockCarveRequestSignal : ISignal
    {
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
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Heat01;
        [FieldOffset(12)] public float Energy01;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct VfxSparkRequestSignal : ISignal
    {
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
    public struct MockSdfSample
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
        public const uint TriggerLowTierSnap = 1u << 4;
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
        public static ToolKinematicsMathLod ResolveLod(float systemHealthIndex)
        {
            float stress = Clamp01Finite(systemHealthIndex);
            if (stress > 0.85f)
                return ToolKinematicsMathLod.Low;
            if (stress > 0.62f)
                return ToolKinematicsMathLod.Middle;
            if (stress > 0.28f)
                return ToolKinematicsMathLod.High;
            return ToolKinematicsMathLod.Ultra;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveRaymarchSteps(ToolKinematicsMathLod lod)
        {
            switch (lod)
            {
                case ToolKinematicsMathLod.Low:
                    return MaxRaymarchStepsLow;
                case ToolKinematicsMathLod.Middle:
                    return MaxRaymarchStepsMiddle;
                case ToolKinematicsMathLod.High:
                    return MaxRaymarchStepsHigh;
                default:
                    return MaxRaymarchStepsUltra;
            }
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
        public static float3 ToLocalFloat3(double3 absolute, double3 cameraAup, float3 fallback)
        {
            double3 local = absolute - cameraAup;
            if (!math.all(math.isfinite(local)))
                return fallback;

            float3 result = new float3((float)local.x, (float)local.y, (float)local.z);
            return math.all(math.isfinite(result)) ? result : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
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
        public static MockSdfSample SampleMockSdf(float3 localPosition)
        {
            float3 sphereCenter = new float3(0f, 1.15f, 5.5f);
            float sphereDistance = math.length(localPosition - sphereCenter) - 1.2f;
            float floorDistance = localPosition.y + 1.2f;
            float3 boxQ = math.abs(localPosition - new float3(1.45f, 0.25f, 4.25f)) - new float3(0.55f, 0.9f, 0.55f);
            float boxDistance = math.length(math.max(boxQ, 0f)) + math.min(math.max(boxQ.x, math.max(boxQ.y, boxQ.z)), 0f);
            float selected = math.min(sphereDistance, math.min(floorDistance, boxDistance));
            uint material = selected == boxDistance ? ToolKinematicsHashes.MockMetal : ToolKinematicsHashes.MockRock;
            return new MockSdfSample
            {
                Distance = math.isfinite(selected) ? selected : 1000f,
                MaterialHash = material
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EstimateMockSdfNormal(float3 localPosition)
        {
            const float h = 0.015625f;
            float dx = SampleMockSdf(localPosition + new float3(h, 0f, 0f)).Distance -
                       SampleMockSdf(localPosition - new float3(h, 0f, 0f)).Distance;
            float dy = SampleMockSdf(localPosition + new float3(0f, h, 0f)).Distance -
                       SampleMockSdf(localPosition - new float3(0f, h, 0f)).Distance;
            float dz = SampleMockSdf(localPosition + new float3(0f, 0f, h)).Distance -
                       SampleMockSdf(localPosition - new float3(0f, 0f, h)).Distance;
            return SafeNormalize(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float NoiseSigned(uint seed)
        {
            uint mixed = Mix(seed, seed ^ 0xA341316Cu);
            return ((mixed & 0xFFFFu) * (1f / 32767.5f)) - 1f;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct TwoBoneIKJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ToolStateDTO> ToolStates;
        [ReadOnly] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        public NativeArray<ToolIkOutputDTO> IkOutputs;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)FrameInputs.Length || (uint)index >= (uint)IkOutputs.Length)
                return;

            ToolKinematicsFrameInputDTO input = FrameInputs[index];
            quaternion controllerRotation = ToolKinematicsMath.SafeNormalizeQuaternion(input.ControllerRotation, quaternion.identity);
            float3 shoulder = input.ShoulderLocalPosition;
            float3 target = input.ControllerLocalPosition;
            float3 pole = ToolKinematicsMath.SafeNormalize(input.PoleLocalDirection, new float3(0f, 1f, 0f));
            bool lowTierSnap = (input.TriggerFlags & ToolKinematicsMath.TriggerLowTierSnap) != 0 ||
                               ToolKinematicsMath.ResolveLod(input.SystemHealthIndex) == ToolKinematicsMathLod.Low;

            ToolIkOutputDTO output = default;
            output.Shoulder = shoulder;
            output.Wrist = target;
            output.Flags = lowTierSnap ? (uint)ToolKinematicsFlags.LowTierSnap : 0u;
            output.ComputeMicrosecondsEstimate = lowTierSnap ? 1.5f : 8.0f;

            if (lowTierSnap)
            {
                output.Elbow = math.lerp(shoulder, target, 0.5f);
                output.UpperRotation = controllerRotation;
                IkOutputs[index] = output;
                return;
            }

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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SdfRaymarchJob : IJobParallelFor
    {
        public NativeArray<ToolStateDTO> ToolStates;
        public NativeArray<ToolRecoilStateDTO> RecoilStates;
        [ReadOnly] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        [ReadOnly] public NativeArray<ToolKinematicsTuningDTO> Tuning;
        public NativeArray<ToolHitResultDTO> HitResults;
        public NativeArray<ToolScreenExportDTO> ScreenExports;
        public NativeArray<ToolPoseOutputDTO> PoseOutputs;
        public NativeArray<ToolHeatSignal> HeatSignals;
        public NativeArray<VfxSparkRequestSignal> SparkRequests;
        public NativeArray<ToolKinematicsTelemetryEntry> TelemetryRing;
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
            float stress = ToolKinematicsMath.Clamp01Finite(input.SystemHealthIndex);
            ToolKinematicsMathLod lod = ToolKinematicsMath.ResolveLod(stress);
            uint flags = (uint)index < (uint)ScreenExports.Length
                ? ScreenExports[index].StateFlags
                : (uint)ToolKinematicsFlags.Idle;
            if ((tuning.Flags & (uint)ToolKinematicsFlags.CsvIoFault) != 0u)
                flags |= (uint)ToolKinematicsFlags.CsvIoFault;
            else
                flags &= ~(uint)ToolKinematicsFlags.CsvIoFault;

            bool trigger = (input.TriggerFlags & ToolKinematicsMath.TriggerPressed) != 0;
            bool lowTierSnap = lod == ToolKinematicsMathLod.Low;
            if (lowTierSnap)
                flags |= (uint)ToolKinematicsFlags.LowTierSnap;
            else
                flags &= ~(uint)ToolKinematicsFlags.LowTierSnap;

            float maxHeat = ToolKinematicsMath.ClampPositiveFinite(tuning.MaxHeat, 1f);
            bool overheated = (flags & (uint)ToolKinematicsFlags.Overheated) != 0;
            bool hasEnergy = state.EnergyRemaining > 0.0001f;
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

            if (state.EnergyRemaining <= 0.0001f)
            {
                flags |= (uint)ToolKinematicsFlags.LowPower;
                flags &= ~(uint)(ToolKinematicsFlags.Active | ToolKinematicsFlags.BeamActive);
                active = false;
            }
            else
                flags &= ~(uint)ToolKinematicsFlags.LowPower;

            float3 forward = math.rotate(controllerRotation, new float3(0f, 0f, 1f));
            forward = ToolKinematicsMath.SafeNormalize(forward, new float3(0f, 0f, 1f));
            state.Forward = forward;
            float3 toolLocal = ToolKinematicsMath.ToLocalFloat3(state.AUP, input.CameraAup, input.ControllerLocalPosition);
            float3 tipLocal = toolLocal + forward * 0.28f;
            MockSdfSample tipSample = ToolKinematicsMath.SampleMockSdf(tipLocal);

            if (tipSample.Distance < 0f)
            {
                flags |= (uint)ToolKinematicsFlags.SdfPenetrating;
                if (!lowTierSnap)
                {
                    float depth = math.min(0.35f, -tipSample.Distance);
                    recoil.SpringVelocity += depth * math.max(0f, tuning.CollisionSpring);
                    recoil.AngularOffsetAxis = math.cross(forward, ToolKinematicsMath.EstimateMockSdfNormal(tipLocal));
                }
            }
            else
            {
                flags &= ~(uint)ToolKinematicsFlags.SdfPenetrating;
            }

            if (lowTierSnap)
            {
                recoil.PositionOffset = default;
                recoil.AngularOffsetAxis = default;
                recoil.KickVelocity = 0f;
                recoil.SpringVelocity = 0f;
                recoil.RecoilTime = 0f;
                recoil.Recoil01 = 0f;
                recoil.Flags = 0u;
            }
            else
            {
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
            }

            ToolHitResultDTO hit = default;
            int stepCount = 0;
            float traveled = 0f;
            float maxRange = math.max(0.1f, tuning.LaserRange);
            bool hasHit = false;
            int maxSteps = ToolKinematicsMath.ResolveRaymarchSteps(lod);
            if (active && hasEnergy)
            {
                float3 marchPos = tipLocal;
                for (int step = 0; step < maxSteps && traveled <= maxRange; step++)
                {
                    MockSdfSample sample = ToolKinematicsMath.SampleMockSdf(marchPos);
                    float distance = math.max(0.015625f, math.abs(sample.Distance));
                    stepCount = step + 1;
                    if (sample.Distance <= 0.01f)
                    {
                        hit.HitPoint = marchPos;
                        hit.Normal = ToolKinematicsMath.EstimateMockSdfNormal(marchPos);
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
                float recoilRadians = lowTierSnap ? 0f : math.clamp(recoil.Recoil01, -0.35f, 0.35f);
                quaternion recoilRotation = quaternion.AxisAngle(recoilAxis, recoilRadians);
                quaternion finalRotation = lowTierSnap
                    ? controllerRotation
                    : ToolKinematicsMath.SafeNormalizeQuaternion(math.mul(controllerRotation, recoilRotation), controllerRotation);
                float3 pivotCompensation = default;
                if (!lowTierSnap)
                {
                    float3 basePivot = math.rotate(controllerRotation, recoil.PivotLocal);
                    float3 rotatedPivot = math.rotate(finalRotation, recoil.PivotLocal);
                    pivotCompensation = basePivot - rotatedPivot;
                }

                float3 finalPosition = lowTierSnap ? toolLocal : toolLocal + recoil.PositionOffset + pivotCompensation;
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

            state._pad0 = 0u;
            state._pad1 = 0u;
            ToolStates[index] = state;

            if ((uint)index < (uint)HeatSignals.Length)
            {
                HeatSignals[index] = new ToolHeatSignal
                {
                    ToolHash = state.ToolTypeHash,
                    Frame = input.FrameIndex,
                    Heat01 = ToolKinematicsMath.Clamp01Finite(state.HeatLevel * math.rcp(maxHeat)),
                    Energy01 = ToolKinematicsMath.Clamp01Finite(state.EnergyRemaining),
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
                        Intensity01 = active ? ToolKinematicsMath.Clamp01Finite(state.HeatLevel * math.rcp(maxHeat)) : 0f,
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
                    EnergyRemaining = ToolKinematicsMath.Clamp01Finite(state.EnergyRemaining),
                    HitDistance = hit.Distance,
                    RaymarchStepCount = stepCount,
                    IkComputeTimeMicroseconds = lowTierSnap ? 1.5f : 8f,
                    Flags = flags,
                    ToolLocalPosition = toolLocal,
                    HitPoint = hit.HitPoint,
                    MaterialHash = hit.MaterialHash,
                    _pad0 = 0u
                };
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockCarveRequestJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ToolHitResultDTO> HitResults;
        [ReadOnly] public NativeArray<ToolStateDTO> ToolStates;
        [ReadOnly] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        [ReadOnly] public NativeArray<ToolScreenExportDTO> ScreenExports;
        public NativeArray<MockCarveRequestSignal> CarveRequests;

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
                ? new MockCarveRequestSignal
                {
                    HitPoint = hit.HitPoint,
                    Normal = hit.Normal,
                    ToolHash = state.ToolTypeHash,
                    MaterialHash = hit.MaterialHash,
                    Frame = input.FrameIndex,
                    Power01 = ToolKinematicsMath.Clamp01Finite(state.EnergyRemaining),
                    Flags = flags,
                    _pad0 = 0u
                }
                : default;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ProceduralBeamMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ToolHitResultDTO> HitResults;
        [ReadOnly] public NativeArray<ToolStateDTO> ToolStates;
        [ReadOnly] public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
        [ReadOnly] public NativeArray<ToolScreenExportDTO> ScreenExports;
        [ReadOnly] public NativeArray<ToolKinematicsTuningDTO> Tuning;
        public NativeArray<ToolBeamVertexDTO> BeamVertices;
        public NativeArray<int> BeamVertexCounts;
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
            int ringSides = 4;
            ToolKinematicsMathLod lod = ToolKinematicsMath.ResolveLod(input.SystemHealthIndex);
            if (lod == ToolKinematicsMathLod.High)
                ringSides = 6;
            else if (lod == ToolKinematicsMathLod.Ultra)
                ringSides = 8;

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
                    float s = math.sin(a);
                    float c = math.cos(a);
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
